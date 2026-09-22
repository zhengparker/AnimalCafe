#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.Core.Time;
using AnimalCafe.Decoration;
using AnimalCafe.UI;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    // Real MainCafe checks: no delayed game speed, duplicate highlights or pause-frozen motion.
    // 使用真实场景，验证状态立即生效、滑块复用和暂停时动画不冻结。
    public sealed class P8RTimeSelectionMotionTests
    {
        private Scene scene;
        private Vector2? previousProfile;
        private float previousTimeScale;
        [SetUp] public void Remember()
        {
            previousProfile = P8RMobileMetrics.EditorLogicalViewportOverride;
            previousTimeScale = Time.timeScale;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                var assets = Phase8SceneInputTestCleanup.CaptureAssets(scene);
                Find<DecorationModeController>().enabled = false;
                SceneManager.SetActiveScene(SceneManager.CreateScene("P8RTimeMotionCleanup"));
                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone) yield return null;
                Phase8SceneInputTestCleanup.DisposeReleasedAssets(assets);
            }
            P8RMobileMetrics.EditorLogicalViewportOverride = previousProfile;
            Time.timeScale = previousTimeScale;
            yield return null;
        }
        private IEnumerator Load()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null; yield return null;
            Canvas.ForceUpdateCanvases();
        }
        private T Find<T>() where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static T Field<T>(object owner, string name) => (T)owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static RectTransform Strip(TimeControlPanel hud) => (RectTransform)hud.transform.Find("P8RTimeStrip");
        private static RectTransform Selection(TimeControlPanel hud)
        {
            var selection = Strip(hud).Find("P8RTimeSelection") as RectTransform;
            Assert.That(selection, Is.Not.Null, "One moving orange selection is required.");
            return selection;
        }
        private static float X(TimeControlPanel hud) => Selection(hud).anchoredPosition.x / P8RMobileMetrics.For(hud).Units(1);
        private static Button[] Buttons(TimeControlPanel hud) => new[] { "pauseButton", "normalButton", "fastButton" }
            .Select(name => Field<Button>(hud, name)).ToArray();

        [UnityTest] public IEnumerator ModeAndTime_AreEqualSizedAndAdjacent_AcrossResponsiveResize()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(new Vector2(1080, 1920));
                P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360, 640);
                yield return Load();
                var hud = Find<TimeControlPanel>();
                var strip = Strip(hud);
                foreach (var logical in new[] { new Vector2(360, 640), new Vector2(320, 569),
                    new Vector2(800, 360), new Vector2(768, 1024), new Vector2(360, 640) })
                {
                    screen.Resize(logical * 2);
                    P8RMobileMetrics.EditorLogicalViewportOverride = logical;
                    yield return new WaitForSecondsRealtime(.25f);
                    Canvas.ForceUpdateCanvases();
                    var unit = P8RMobileMetrics.For(hud).Units(1);
                    var badge = (RectTransform)hud.transform.Find("P8RModeBadge");
                    Assert.That(strip, Is.SameAs(Strip(hud)), "Resize reuses the strip.");
                    Assert.That(badge.rect.width, Is.EqualTo(strip.rect.width).Within(.05f), logical + " equal widths");
                    Assert.That(badge.rect.height / unit, Is.EqualTo(32).Within(.05f), logical + " compact badge");
                    Assert.That(strip.rect.height / unit, Is.EqualTo(32).Within(.05f), logical + " compact time");
                    if (logical.x > logical.y)
                    {
                        Assert.That(strip.anchoredPosition.y, Is.EqualTo(badge.anchoredPosition.y).Within(.05f));
                        Assert.That((strip.anchoredPosition.x - badge.anchoredPosition.x - badge.rect.width) / unit,
                            Is.EqualTo(8).Within(.05f), "Side-by-side landscape gap");
                    }
                    else
                    {
                        Assert.That(strip.anchoredPosition.x, Is.EqualTo(badge.anchoredPosition.x).Within(.05f));
                        Assert.That((badge.anchoredPosition.y - strip.anchoredPosition.y - badge.rect.height) / unit,
                            Is.EqualTo(8).Within(.05f), "Stacked portrait gap");
                    }
                    foreach (var button in Buttons(hud))
                    {
                        var size = ((RectTransform)button.transform).rect.size / unit;
                        Assert.That(size.x, Is.EqualTo(48).Within(.05f));
                        Assert.That(size.y, Is.EqualTo(48).Within(.05f));
                    }
                }
            }
        }

        [UnityTest] public IEnumerator Selection_SlidesAndRetargets_WhileGameSpeedChangesImmediately()
        {
            yield return Load();
            var hud = Find<TimeControlPanel>();
            var buttons = Buttons(hud);
            var time = Find<GameTimeService>();
            time.SetNormal();
            yield return new WaitForSecondsRealtime(.22f);
            Assert.That(X(hud), Is.EqualTo(48.015625f).Within(.05f), "Fresh stable normal selection");
            var iconCenters = buttons.Select(button => P8RCompleteFlowTests.MeasuredInk(
                button.transform.Find("Icon").GetComponent<Image>()).center).ToArray();
            var start = X(hud);
            buttons[2].onClick.Invoke();
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
            Assert.That(X(hud), Is.EqualTo(start).Within(.05f), "Speed changes before the visual begins travelling.");
            yield return new WaitForSecondsRealtime(.045f);
            var middle = X(hud);
            Assert.That(middle, Is.InRange(start + .01f, 96.03125f - .01f), "A real intermediate position, not an instant swap.");
            buttons[0].onClick.Invoke();
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(X(hud), Is.EqualTo(middle).Within(.05f), "Rapid input retargets from current visual position.");
            yield return new WaitForSecondsRealtime(.23f);
            Assert.That(X(hud), Is.Zero.Within(.05f), "Motion finishes while paused.");
            for (var index = 0; index < buttons.Length; index++)
            {
                var center = P8RCompleteFlowTests.MeasuredInk(buttons[index].transform.Find("Icon").GetComponent<Image>()).center;
                Assert.That(Vector2.Distance(center, iconCenters[index]) / P8RMobileMetrics.For(hud).PixelsPerLogicalUnit,
                    Is.LessThan(.03f), "Only the background moves; Pause/Resume PNG padding is not visible ink.");
            }
            Assert.That(buttons.All(button => !button.image.enabled), Is.True,
                "Instant segment backgrounds must not cover the sliding highlight.");
            Assert.That(Selection(hud).GetComponentsInChildren<Graphic>().All(image => !image.raycastTarget), Is.True);
        }

        [UnityTest] public IEnumerator Selection_LockDisableAndResize_DoNotLeaveStaleMotionOrDuplicateNodes()
        {
            using var screen = new P8RReferenceLayoutTests.NativeScreenSize();
            screen.Resize(new Vector2(1080, 1920));
            P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360, 640);
            yield return Load();
            var hud = Find<TimeControlPanel>();
            var selection = Selection(hud);
            var time = Find<GameTimeService>();
            time.SetFast();
            hud.SetDecorationPauseLock(true);
            Assert.That(selection.gameObject.activeSelf, Is.False, "Locked controls have no orange selection.");
            Assert.That(Buttons(hud).All(button => !button.interactable && button.image.enabled), Is.True);
            hud.SetDecorationPauseLock(false);
            Assert.That(X(hud), Is.EqualTo(96.03125f).Within(.05f), "Unlock reflects actual restored speed.");
            time.SetNormal();
            hud.enabled = false;
            time.SetPaused();
            hud.enabled = true;
            Assert.That(X(hud), Is.Zero.Within(.05f), "Reenable snaps to current state, not stale motion.");
            yield return new WaitForSecondsRealtime(.23f);
            Assert.That(X(hud), Is.Zero.Within(.05f));
            Assert.That(Selection(hud), Is.SameAs(selection));
            Assert.That(hud.GetComponentsInChildren<RectTransform>(true).Count(rect => rect.name == "P8RTimeSelection"), Is.EqualTo(1));
            time.SetFast();
            screen.Resize(new Vector2(1600, 720));
            P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(800, 360);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(X(hud), Is.EqualTo(96.03125f).Within(.05f), "Resize resolves the in-flight selection.");
            yield return new WaitForSecondsRealtime(.23f);
            Assert.That(X(hud), Is.EqualTo(96.03125f).Within(.05f), "The canceled animation cannot write stale coordinates.");
        }
    }
}
#endif

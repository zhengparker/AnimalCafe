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
        private static Button[] Buttons(TimeControlPanel hud) => new[] { "normalButton", "fastButton" }
            .Select(name => Field<Button>(hud, name)).ToArray();
        private static string NormalIcon(TimeControlPanel hud) =>
            Field<Button>(hud, "normalButton").transform.Find("Icon").GetComponent<Image>().sprite.name;
        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        [UnityTest] public IEnumerator ModeAndTime_StayAdjacentAndDoNotOverlap_AcrossResponsiveResize()
        {
            using var screen = new P8RReferenceLayoutTests.NativeScreenSize();
            screen.Resize(new Vector2(1080, 1920));
            P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360, 640);
            yield return Load();
            var hud = Find<TimeControlPanel>();
            var buttons = Buttons(hud);
            var strip = (RectTransform)hud.transform.Find("P8RTimeStrip");
            foreach (var logical in new[] { new Vector2(360, 640), new Vector2(320, 569),
                new Vector2(800, 360), new Vector2(768, 1024), new Vector2(360, 640) })
            {
                screen.Resize(logical * 2);
                P8RMobileMetrics.EditorLogicalViewportOverride = logical;
                yield return new WaitForSecondsRealtime(.25f);
                Canvas.ForceUpdateCanvases();
                var density = P8RMobileMetrics.For(hud).PixelsPerLogicalUnit;
                var badge = Bounds((RectTransform)hud.transform.Find("P8RModeBadge"));
                var mode = Bounds((RectTransform)hud.transform.Find("DecorationModeButton"));
                var stripBounds = Bounds(strip);
                Assert.That(strip, Is.SameAs(hud.transform.Find("P8RTimeStrip")), "Resize reuses the time container.");
                Assert.That(badge.center.y, Is.EqualTo(mode.center.y).Within(1f), logical + " mode row alignment");
                Assert.That(badge.Overlaps(stripBounds) || mode.Overlaps(stripBounds), Is.False,
                    logical + " time controls must not cover either mode control");
                if (stripBounds.xMin > badge.xMax)
                {
                    Assert.That(stripBounds.center.y, Is.EqualTo(badge.center.y).Within(1f));
                    Assert.That((stripBounds.xMin - badge.xMax) / density, Is.EqualTo(8).Within(.1f));
                }
                else
                {
                    Assert.That(stripBounds.xMin, Is.EqualTo(badge.xMin).Within(1f));
                    Assert.That((badge.yMin - stripBounds.yMax) / density, Is.EqualTo(8).Within(.1f));
                }
                foreach (var button in buttons)
                {
                    var target = Bounds((RectTransform)button.transform);
                    Assert.That(target.width / density, Is.GreaterThanOrEqualTo(47.9f));
                    Assert.That(target.height / density, Is.GreaterThanOrEqualTo(47.9f));
                }
                Assert.That(Bounds((RectTransform)buttons[0].transform)
                    .Overlaps(Bounds((RectTransform)buttons[1].transform)), Is.False);
            }
        }

        [UnityTest] public IEnumerator RapidPauseAndSpeedChanges_UpdateStateImmediatelyWithoutDelayedReversion()
        {
            yield return Load();
            var hud = Find<TimeControlPanel>();
            var buttons = Buttons(hud);
            var time = Find<GameTimeService>();
            time.SetNormal();
            buttons[1].onClick.Invoke();
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
            Assert.That(buttons[1].image.sprite.name, Is.EqualTo("tab_selected"));
            buttons[1].onClick.Invoke();
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(buttons.All(button => button.interactable), Is.True);
            Assert.That(NormalIcon(hud), Is.EqualTo("pause_cocoa"));
            buttons[0].onClick.Invoke();
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal), "Paused Normal always selects 1x.");
            buttons[0].onClick.Invoke();
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            buttons[1].onClick.Invoke();
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Fast), "Fast resumes directly at 2x.");
            buttons[0].onClick.Invoke();
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
            Assert.That(buttons[0].image.sprite.name, Is.EqualTo("tab_selected"), "No delayed refresh restores obsolete state.");
        }

        [UnityTest] public IEnumerator LockDisableAndResize_ReuseControlsAndReflectCurrentServiceState()
        {
            using var screen = new P8RReferenceLayoutTests.NativeScreenSize();
            screen.Resize(new Vector2(1080, 1920));
            P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360, 640);
            yield return Load();
            var hud = Find<TimeControlPanel>();
            var buttons = Buttons(hud);
            var faces = buttons.Select(button => button.image).ToArray();
            var time = Find<GameTimeService>();
            time.SetFast();
            hud.SetDecorationPauseLock(true);
            Assert.That(buttons.All(button => !button.interactable), Is.True);
            Assert.That(buttons.All(button => button.image.sprite.name == "tab_unavailable"), Is.True);
            hud.SetDecorationPauseLock(false);
            Assert.That(NormalIcon(hud), Is.EqualTo(time.CurrentSpeed == GameSpeed.Paused ? "pause_cocoa" : "resume_cocoa"));
            time.SetNormal();
            hud.enabled = false;
            time.SetFast(); time.SetPaused();
            hud.enabled = true;
            Assert.That(buttons[1].interactable, Is.True);
            Assert.That(NormalIcon(hud), Is.EqualTo(time.CurrentSpeed == GameSpeed.Paused ? "pause_cocoa" : "resume_cocoa"));
            screen.Resize(new Vector2(1600, 720));
            P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(800, 360);
            yield return new WaitForSecondsRealtime(.25f);
            Canvas.ForceUpdateCanvases();
            Assert.That(NormalIcon(hud), Is.EqualTo(time.CurrentSpeed == GameSpeed.Paused ? "pause_cocoa" : "resume_cocoa"));
            Assert.That(buttons.Select(button => button.image), Is.EqualTo(faces));
            Assert.That(hud.GetComponentsInChildren<RectTransform>(true)
                .Count(rect => rect.name == "P8RTimeStrip"), Is.EqualTo(1));
            foreach (var button in buttons)
                Assert.That(button.GetComponentsInChildren<Image>(true).Count(image => image.name == "P8RFace"), Is.EqualTo(1));
            buttons[0].onClick.Invoke();
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
            Assert.That(NormalIcon(hud), Is.EqualTo(time.CurrentSpeed == GameSpeed.Paused ? "pause_cocoa" : "resume_cocoa"));
        }
    }
}
#endif
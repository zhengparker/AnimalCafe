#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.Core.Time;
using AnimalCafe.Decoration;
using AnimalCafe.UI;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    // Approved two-control contract, exercised against the real MainCafe HUD.
    // 真实场景验证两按钮、暂停所有权和左右模式外框，不用新测试 API。
    public sealed class P8RTwoButtonTimeControlTests
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
                SceneManager.SetActiveScene(SceneManager.CreateScene("P8RTwoButtonTimeCleanup"));
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
        private Button Normal => Field<Button>(Find<TimeControlPanel>(), "normalButton");
        private Button Fast => Field<Button>(Find<TimeControlPanel>(), "fastButton");
        private Button Mode => Find<TimeControlPanel>().transform.Find("DecorationModeButton").GetComponent<Button>();
        private static string VisibleText(Button button) => string.Join(" ", button
            .GetComponentsInChildren<TMP_Text>().Where(label => label.enabled).Select(label => label.text));
        private static void Click(Button button) => ExecuteEvents.Execute(button.gameObject,
            new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
            ExecuteEvents.pointerClickHandler);
        private static string Icon(Button button) => button.transform.Find("Icon").GetComponent<Image>().sprite.name;

        [UnityTest] public IEnumerator TwoTriangles_SelectPauseResumeAndDirectFast()
        {
            yield return Load();
            var time = Find<GameTimeService>();
            var visible = Find<TimeControlPanel>().GetComponentsInChildren<Button>().Where(button => button != Mode).ToArray();
            Assert.That(visible, Is.EquivalentTo(new[] { Normal, Fast }));
            Assert.That(Icon(Normal), Does.Contain("resume"));
            Assert.That(Icon(Fast), Does.Contain("resume"));
            Click(Fast);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
            Assert.That(Fast.image.sprite.name, Does.Contain("selected"));
            Click(Fast);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Assert.That(Icon(Normal), Does.Contain("pause"));
            Click(Normal);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
            Assert.That(Normal.image.sprite.name, Does.Contain("selected"));
            Click(Normal);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Click(Fast);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
        }

        [UnityTest] public IEnumerator Readiness_OnlyShowsChecklistWhileDecorating()
        {
            yield return Load();
            var view = Find<AnimalCafe.UI.Feedback.ValidationMessageView>();
            Assert.That(view.CurrentMessage, Is.EqualTo("Setup incomplete · Enter Decor to finish"));
            Assert.That(view.transform.Find("ChecklistRow0").gameObject.activeSelf, Is.False);
            Click(Mode); yield return null;
            Assert.That(view.transform.Find("ChecklistRow0").gameObject.activeSelf, Is.True);
            Assert.That(view.CurrentMessage, Does.Contain("Cash Register"));
            Click(Mode); yield return null;
            Assert.That(view.transform.Find("ChecklistRow0").gameObject.activeSelf, Is.False);
            Assert.That(view.CurrentMessage, Is.EqualTo("Setup incomplete · Enter Decor to finish"));
        }

        [UnityTest] public IEnumerator Triangles_HaveEqualVisibleHeightAndFitExistingButtons()
        {
            yield return Load();
            var normal = P8RCompleteFlowTests.MeasuredInk(Normal.transform.Find("Icon").GetComponent<Image>());
            var fast = P8RCompleteFlowTests.MeasuredInk(Fast.transform.Find("Icon").GetComponent<Image>());
            var firstIcon = Fast.transform.Find("Icon").GetComponent<Image>();
            var secondIcon = Fast.transform.Find("SecondTriangle").GetComponent<Image>();
            Assert.That(secondIcon.sprite, Is.SameAs(firstIcon.sprite));
            Assert.That(secondIcon.rectTransform.sizeDelta, Is.EqualTo(firstIcon.rectTransform.sizeDelta));
            var secondInk = P8RCompleteFlowTests.MeasuredInk(secondIcon);
            Assert.That(secondInk.xMin, Is.GreaterThan(fast.xMax));
            var density = P8RMobileMetrics.For(Find<TimeControlPanel>()).PixelsPerLogicalUnit;
            Assert.That((secondInk.xMax - fast.xMin) / density, Is.LessThanOrEqualTo(36.1f));
            Assert.That(normal.height / density, Is.EqualTo(20).Within(.5f));
            Assert.That(fast.height / density, Is.EqualTo(normal.height / density).Within(.5f));
            Assert.That(fast.width / density, Is.LessThanOrEqualTo(36.1f));
        }

        [UnityTest] public IEnumerator Decoration_LocksEnteringSpeedAndRestoresIt()
        {
            yield return Load();
            var time = Find<GameTimeService>();
            foreach (var speed in new[] { GameSpeed.Normal, GameSpeed.Fast, GameSpeed.Paused })
            {
                time.TrySetSpeed(speed);
                Click(Mode);
                Assert.That(Icon(speed == GameSpeed.Fast ? Fast : Normal), Does.Contain("lock"));
                Assert.That(Icon(speed == GameSpeed.Fast ? Normal : Fast), Does.Not.Contain("lock"));
                Assert.That(Normal.interactable || Fast.interactable, Is.False);
                Click(Normal); Click(Fast);
                Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
                Click(Mode);
                yield return null;
                Assert.That(time.CurrentSpeed, Is.EqualTo(speed));
            }
        }

        [UnityTest] public IEnumerator ReenabledPausedHud_FirstButtonAlwaysSelectsNormal()
        {
            yield return Load();
            var hud = Find<TimeControlPanel>();
            var time = Find<GameTimeService>();
            hud.enabled = false;
            time.SetFast(); time.SetPaused();
            hud.enabled = true;
            Assert.That(Icon(Normal), Does.Contain("pause"));
            Click(Normal);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
        }

        [UnityTest] public IEnumerator ModeFaces_StayEqualHeightAndVisuallyAlignedAcrossMetrics()
        {
            using var screen = new P8RReferenceLayoutTests.NativeScreenSize();
            screen.Resize(new Vector2(1600, 720));
            P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(800, 360);
            yield return Load();
            var hud = Find<TimeControlPanel>();
            foreach (var logical in new[] { new Vector2(800, 360), new Vector2(569, 320),
                new Vector2(360, 640), new Vector2(1024, 768) })
            {
                screen.Resize(logical * 2);
                P8RMobileMetrics.EditorLogicalViewportOverride = logical;
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (var decorating in new[] { false, true })
                {
                    if (Find<DecorationModeController>().IsOpen != decorating) Click(Mode);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    var badge = Field<TMP_Text>(hud, "modeBadgeLabel").GetComponentInParent<Image>();
                    var density = P8RMobileMetrics.For(hud).PixelsPerLogicalUnit;
                    var left = P8RCompleteFlowTests.MeasuredInk(badge);
                    var right = P8RCompleteFlowTests.MeasuredInk(Mode.image);
                    Assert.That(left.height / density, Is.EqualTo(48).Within(2), logical + " mode visible height");
                    Assert.That(right.height / density, Is.EqualTo(left.height / density).Within(.5f),
                        logical + " equal visible faces, not just equal touch roots");
                    Assert.That(Mathf.Abs(left.center.y - right.center.y) / density, Is.LessThan(.5f),
                        logical + " shared visible centerline");
                    var target = ScreenRect((RectTransform)Mode.transform);
                    Assert.That(target.height / density, Is.GreaterThanOrEqualTo(47.9f));
                    Assert.That(target.width / density, Is.GreaterThanOrEqualTo(47.9f));
                    Assert.That(VisibleText(Mode), Is.EqualTo(decorating ? "Done" : "Decor"));
                }
                if (Find<DecorationModeController>().IsOpen) Click(Mode);
            }
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
#endif

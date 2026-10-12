#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.Core.Time;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
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
            PrepareBusinessLayout();
            yield return null;
            Click(Normal);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
            Assert.That(Icon(Normal), Does.Contain("pause"));
            Assert.That(Icon(Fast), Does.Contain("resume"));
            Click(Fast);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
            Assert.That(Fast.image.sprite.name, Does.Contain("selected"));
            Click(Fast);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Assert.That(Icon(Normal), Does.Contain("resume"));
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
            PrepareBusinessLayout();
            yield return null;
            var time = Find<GameTimeService>();
            foreach (var speed in new[] { GameSpeed.Normal, GameSpeed.Fast, GameSpeed.Paused })
            {
                time.TrySetSpeed(speed);
                var badge = Field<TMP_Text>(Find<TimeControlPanel>(), "modeBadgeLabel");
                var expected = speed == GameSpeed.Normal ? "1x" : speed == GameSpeed.Fast ? "2x" : "Pause";
                Assert.That(badge.text, Is.EqualTo(expected));
                Click(Mode);
                Assert.That(badge.text, Is.EqualTo("Decor"));
                Assert.That(Icon(speed == GameSpeed.Fast ? Fast : Normal), Does.Contain("lock"));
                Assert.That(Icon(speed == GameSpeed.Fast ? Normal : Fast), Does.Not.Contain("lock"));
                Assert.That(Normal.interactable || Fast.interactable, Is.False);
                Click(Normal); Click(Fast);
                Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
                Click(Mode);
                yield return null;
                Assert.That(time.CurrentSpeed, Is.EqualTo(speed));
                Assert.That(badge.text, Is.EqualTo(expected));
            }
        }

        [UnityTest] public IEnumerator ReenabledPausedHud_FirstButtonAlwaysSelectsNormal()
        {
            yield return Load();
            PrepareBusinessLayout();
            yield return null;
            var hud = Find<TimeControlPanel>();
            var time = Find<GameTimeService>();
            hud.enabled = false;
            time.SetFast(); time.SetPaused();
            hud.enabled = true;
            Assert.That(Field<TMP_Text>(hud, "modeBadgeLabel").text, Is.EqualTo("Pause"));
            Assert.That(Icon(Normal), Does.Contain("resume"));
            Click(Normal);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
            Assert.That(Field<TMP_Text>(hud, "modeBadgeLabel").text, Is.EqualTo("1x"));
        }

        [UnityTest] public IEnumerator IncompleteSetup_BlocksBothSpeedsAndShowsClickableFeedback()
        {
            using var screen = new P8RReferenceLayoutTests.NativeScreenSize();
            screen.Resize(new Vector2(1080, 1920));
            P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360, 640);
            yield return Load();
            var time = Find<GameTimeService>();
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Assert.That(time.IsResumeBlocked, Is.True);
            Assert.That(Icon(Normal), Does.Contain("resume").And.Contain("muted"));
            Assert.That(Normal.interactable && Fast.interactable, Is.True,
                "Muted start controls must still receive clicks to explain incomplete setup");
            Assert.That(time.TrySetSpeed(GameSpeed.Fast), Is.False);
            foreach (var button in new[] { Normal, Fast })
            {
                Click(button); yield return null;
                Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
                Assert.That(Find<AnimalCafe.UI.Feedback.ValidationMessageView>().CurrentMessage,
                    Is.EqualTo("Setup incomplete · Enter Decor to finish"));
            }
            Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Any(node => node.name == "MainCafe Setup Toast"), Is.False, "Remove the obsolete setup popup");
            Click(Mode); yield return null;
            Assert.That(Find<DecorationModeController>().IsOpen, Is.True,
                "Setup gate must allow entering Decor to repair the layout");
        }

        private Image SetupFlash(AnimalCafe.UI.Feedback.ValidationMessageView view)
        {
            var node = view.transform.Find("ChecklistPanelFill/SetupAttentionFlash");
            Assert.That(node, Is.Not.Null, "Blocked start should flash the existing readiness card");
            return node.GetComponent<Image>();
        }

        [UnityTest] public IEnumerator SetupFeedback_ShakesAndFlashesWhilePausedWithoutMovingLayout()
        {
            using var screen = new P8RReferenceLayoutTests.NativeScreenSize();
            screen.Resize(new Vector2(1080, 1920));
            P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360, 640);
            yield return Load();
            var view = Find<AnimalCafe.UI.Feedback.ValidationMessageView>();
            var root = (RectTransform)view.transform;
            var summary = view.transform.Find("NormalReadinessSummary").GetComponent<TMP_Text>();
            var originalRoot = root.anchoredPosition;
            var originalSize = root.sizeDelta;
            var originalText = summary.text;
            var originalTextColor = summary.color;
            var moved = false;
            Click(Normal);
            var flash = SetupFlash(view);
            Assert.That(flash.raycastTarget, Is.False);
            Assert.That(flash.color.r, Is.GreaterThan(flash.color.g));
            Assert.That(flash.color.a, Is.GreaterThan(0).And.LessThanOrEqualTo(.181f));
            var start = Time.unscaledTime;
            while (Time.unscaledTime - start < .5f)
            {
                yield return null;
                var offset = summary.rectTransform.anchoredPosition;
                var logicalOffset = offset.x / P8RMobileMetrics.For(view).Units(1);
                moved |= Mathf.Abs(logicalOffset) > .05f;
                Assert.That(Mathf.Abs(logicalOffset), Is.LessThanOrEqualTo(3.01f));
                Assert.That(offset.y, Is.EqualTo(0).Within(.001f));
                Assert.That(root.anchoredPosition, Is.EqualTo(originalRoot));
                Assert.That(root.sizeDelta, Is.EqualTo(originalSize));
                Assert.That(Find<GameTimeService>().CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            }
            Assert.That(moved, Is.True, "The existing status card should visibly shake");
            Assert.That(flash.enabled, Is.False);
            Assert.That(summary.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
            Assert.That(summary.text, Is.EqualTo(originalText));
            Assert.That(summary.color, Is.EqualTo(originalTextColor));
            Assert.That(summary.isTextOverflowing, Is.False);
        }

        [UnityTest] public IEnumerator SetupFeedback_RepeatedClicksRestorePositionAndNeverShowPopupOrGlyphWarnings()
        {
            yield return Load();
            var view = Find<AnimalCafe.UI.Feedback.ValidationMessageView>();
            var warnings = new System.Collections.Generic.List<string>();
            Application.LogCallback capture = (message, trace, type) =>
            {
                if (type == LogType.Warning && message.Contains("Unicode value")) warnings.Add(message);
            };
            Application.logMessageReceived += capture;
            try
            {
                for (var i = 0; i < 6; i++)
                {
                    Click(i % 2 == 0 ? Normal : Fast);
                    yield return new WaitForSecondsRealtime(.04f);
                }
                Assert.That(SetupFlash(view).enabled, Is.True);
                yield return new WaitForSecondsRealtime(.5f);
                foreach (var name in new[] { "NormalReadinessSummary", "ChecklistPanelFill", "ChecklistPanelBorder" })
                    Assert.That(((RectTransform)view.transform.Find(name)).anchoredPosition, Is.EqualTo(Vector2.zero), name);
                Assert.That(SetupFlash(view).enabled, Is.False);
                Assert.That(warnings, Is.Empty);
                Assert.That(view.CurrentMessage, Is.EqualTo("Setup incomplete · Enter Decor to finish"));
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Any(node => node.name == "MainCafe Setup Toast"), Is.False);
            }
            finally { Application.logMessageReceived -= capture; }
        }

        [UnityTest] public IEnumerator SetupFeedback_EnteringDecorDisablingAndCompletingSetupCancelEffect()
        {
            yield return Load();
            var view = Find<AnimalCafe.UI.Feedback.ValidationMessageView>();
            Click(Normal);
            yield return new WaitForSecondsRealtime(.05f);
            var flash = SetupFlash(view);
            Click(Mode); yield return null;
            Assert.That(flash.enabled, Is.False, "Decor checklist must not keep flashing");
            Assert.That(view.transform.Find("ChecklistRow0").gameObject.activeSelf, Is.True);
            Click(Mode); yield return null;
            Click(Fast); yield return new WaitForSecondsRealtime(.05f);
            view.enabled = false;
            Assert.That(flash.enabled, Is.False);
            Assert.That(((RectTransform)view.transform.Find("NormalReadinessSummary")).anchoredPosition, Is.EqualTo(Vector2.zero));
            view.enabled = true;
            Click(Normal); yield return null;
            PrepareBusinessLayout(); yield return null; yield return null;
            Assert.That(flash.enabled, Is.False, "Completing setup cancels feedback immediately");
            Assert.That(view.IsVisible, Is.False);
            Assert.That(Find<GameTimeService>().CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
        }

        [UnityTest] public IEnumerator ConfirmedSetup_UnlocksStartAndRemovingRequiredEquipmentPausesAgain()
        {
            yield return Load();
            var time = Find<GameTimeService>();
            var runtime = Find<CafeLayoutRuntime>();
            PrepareBusinessLayout();
            yield return null; yield return null;
            Assert.That(runtime.CurrentReadiness.CanOpenForBusiness, Is.True);
            Assert.That(time.IsResumeBlocked, Is.False);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Click(Normal);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
            Assert.That(Icon(Normal), Does.Contain("pause").And.Not.Contain("muted"));
            Click(Normal);
            Assert.That(Icon(Normal), Does.Contain("resume"));
            Click(Fast);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
            Assert.That(Icon(Normal), Does.Contain("pause"));
            Click(Normal);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused),
                "The pause icon must pause even when running at 2x");
            Click(Normal);
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
            Assert.That(runtime.FunctionalSurfaceLayout.RemoveMounted("13000000000000000000000000000005").Succeeded, Is.True);
            runtime.RecalculateReadiness();
            yield return null;
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Assert.That(time.TrySetSpeed(GameSpeed.Normal), Is.False);
        }

        private void PrepareBusinessLayout() => PrepareBusinessLayout(scene);

        internal static void PrepareBusinessLayout(Scene scene)
        {
            var runtime = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CafeLayoutRuntime>(true)).Single();
            var decor = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true)).Single();
            var catalog = Field<FurnitureContentCatalog>(decor, "contentCatalog");
            var support = runtime.Layout.FurnitureInstances.Single();
            var slot = catalog.BuildSurfaceSlotCatalog(runtime.Layout.GridSettings)
                .GetForSupport(support.DefinitionId).First().SlotId;
            Assert.That(runtime.Layout.PlaceFurniture(FurnitureInstance.Restore("13000000000000000000000000000002",
                support.DefinitionId, new GridPosition(4, 3), FurnitureRotation.Degrees0)).Succeeded, Is.True);
            Assert.That(runtime.Layout.PlaceFurniture(FurnitureInstance.Restore("13000000000000000000000000000003",
                support.DefinitionId, new GridPosition(6, 3), FurnitureRotation.Degrees0)).Succeeded, Is.True);
            Assert.That(runtime.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance("13000000000000000000000000000004",
                "equipment.cash-register.01", new SurfaceSlotAddress(support.InstanceId, slot), FurnitureRotation.Degrees0)).Succeeded, Is.True);
            Assert.That(runtime.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance("13000000000000000000000000000005",
                "equipment.coffee-machine.01", new SurfaceSlotAddress("13000000000000000000000000000002", slot), FurnitureRotation.Degrees0)).Succeeded, Is.True);
            Assert.That(runtime.FunctionalSurfaceLayout.PlacePickUp(new PickUpPointInstance("13000000000000000000000000000006",
                new SurfaceSlotAddress("13000000000000000000000000000003", slot))).Succeeded, Is.True);
            decor.RefreshConfirmedNavigationPresentation();
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

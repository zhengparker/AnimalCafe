#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.UI;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    /// <summary>Real-scene readiness must follow its existing safe-area parent after inset changes.
    /// 验证动态安全区内的提示卡、详情按钮和可见正文；不修改 Screen.safeArea 或场景资产。</summary>
    public sealed class P8RReadinessSafeAreaTests
    {
        private Scene ownedScene;
        private Vector2? previousProfile;
        private float previousTimeScale;

        [SetUp]
        public void RecordBoundary()
        {
            previousProfile = P8RMobileMetrics.EditorLogicalViewportOverride;
            previousTimeScale = Time.timeScale;
        }

        [UnityTearDown]
        public IEnumerator ReleaseScene()
        {
            if (ownedScene.IsValid() && ownedScene.isLoaded)
            {
                var assets = Phase8SceneInputTestCleanup.CaptureAssets(ownedScene);
                foreach (var controller in ownedScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true)))
                    controller.enabled = false;
                SceneManager.SetActiveScene(SceneManager.CreateScene("P8RReadinessCleanup"));
                var unload = SceneManager.UnloadSceneAsync(ownedScene);
                while (unload != null && !unload.isDone) yield return null;
                Phase8SceneInputTestCleanup.DisposeReleasedAssets(assets);
            }
            ownedScene = default;
            P8RMobileMetrics.EditorLogicalViewportOverride = previousProfile;
            Time.timeScale = previousTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator Phone360_InsetAfterShowingKeepsReadinessInsideSafeArea()
            => CheckInset(new Vector2(1080, 1920), new Vector2(360, 640), new Rect(72, 60, 984, 1728));

        [UnityTest]
        public IEnumerator Phone320_InsetAfterShowingKeepsReadinessInsideSafeArea()
            => CheckInset(new Vector2(960, 1704), new Vector2(320, 568), new Rect(72, 60, 864, 1512));

        [UnityTest]
        public IEnumerator Landscape640_InsetAfterShowingKeepsReadinessInsideSafeArea()
            => CheckInset(new Vector2(1920, 1080), new Vector2(640, 360), new Rect(132, 60, 1752, 984));

        [UnityTest]
        public IEnumerator Tablet_ParentSafeAreaMotionPublishesReadinessBoundsToInstruction()
        {
            var pixels = new Vector2(1536, 2048);
            var safePixels = new Rect(48, 40, 1472, 1920);
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(pixels);
                P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(768, 1024);
                ownedScene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
                yield return null; yield return null; yield return null;
                Assert.That(new Vector2(Screen.width, Screen.height), Is.EqualTo(pixels));
                var controller = SceneComponent<DecorationModeController>();
                var readiness = SceneComponent<ValidationMessageView>();
                var hud = SceneComponent<TimeControlPanel>();
                var action = SceneComponent<DecorationActionBarView>();
                controller.EnterDecorationMode();
                Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                Assert.That(controller.TrySelectFloorRange(SurfaceEditScope.SingleGridFloor), Is.True);
                yield return new WaitForSecondsRealtime(.3f);
                Canvas.ForceUpdateCanvases();
                Assert.That(action.VisibleInstructionRect, Is.Not.Null);
                var oldReadiness = Box(readiness);
                AssertInstructionClear(readiness, action);
                var hudSafe = hud.GetComponentInParent<SafeAreaContainer>(true);
                var readinessSafe = readiness.GetComponentInParent<SafeAreaContainer>(true);
                Assert.That(hudSafe, Is.Not.SameAs(readinessSafe), "Exercise independent presentation branches.");
                // The instruction can observe intermediate geometry before the readiness parent moves.
                // HUD先动、ActionBar等分支随后、readiness最后；440上限使提示本身尺寸不变。
                foreach (var area in ownedScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SafeAreaContainer>(true))
                    .OrderBy(area => area == hudSafe ? 0 : area == readinessSafe ? 2 : 1))
                {
                    area.AutoApplyRuntimeSafeArea = false;
                    area.ApplySafeArea(safePixels, pixels);
                }
                yield return new WaitForSecondsRealtime(.3f);
                Canvas.ForceUpdateCanvases();
                Assert.That(Box(readiness).yMin, Is.LessThan(oldReadiness.yMin - 1f));
                Assert.That(Box(readiness).width, Is.EqualTo(oldReadiness.width).Within(1f),
                    "Tablet keeps its width cap: position-only movement still needs notification.");
                AssertPresentation(readiness, safePixels, hud, 2f);
                AssertInstructionClear(readiness, action);
                var changes = 0;
                System.Action countChange = () => changes++;
                readiness.DetailsVisibilityChanged += countChange;
                try
                {
                    yield return new WaitForSecondsRealtime(.2f);
                    Assert.That(changes, Is.Zero, "Stable screen bounds must not publish every frame.");
                }
                finally { readiness.DetailsVisibilityChanged -= countChange; }
            }
        }

        private T SceneComponent<T>() where T : Component => ownedScene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

        private void AssertInstructionClear(ValidationMessageView readiness, DecorationActionBarView action)
        {
            var instruction = action.VisibleInstructionRect;
            Assert.That(instruction, Is.Not.Null);
            var notice = Box(instruction);
            Assert.That(notice.yMax, Is.LessThanOrEqualTo(Box(readiness).yMin - 1f),
                "Published readiness bounds must move the waiting instruction below the card.");
            foreach (var tab in SceneComponent<DecorationModeTabsView>().GetComponentsInChildren<Button>())
                Assert.That(notice.Overlaps(Box(tab)), Is.False, "Instruction covers tab " + tab.name);
            Assert.That(notice.Overlaps(Box(SceneComponent<DecorationCatalogueView>().VerticalScroll.viewport)), Is.False,
                "Instruction must not cover catalogue content.");
        }

        [UnityTest]
        public IEnumerator OpenStore_InsetChangeReflowsCardWithoutShowingAgain()
        {
            var pixels = new Vector2(960, 1704);
            var full = new Rect(0, 0, 960, 1704);
            // Explicit stress inset: 320x568 logical, left44/right12/bottom20/top44.
            // 模拟非对称安全区，不宣称某款真机的实际 inset。
            var inset = new Rect(132, 60, 792, 1512);
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(pixels);
                P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(320, 568);
                ownedScene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
                yield return null; yield return null; yield return null;
                Assert.That(new Vector2(Screen.width, Screen.height), Is.EqualTo(pixels));
                var store = ownedScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<DecorationStoreModalView>(true)).Single();
                var safeArea = store.ContentRect.GetComponentInParent<SafeAreaContainer>(true);
                Assert.That(safeArea, Is.Not.Null, "Exercise the prefab's existing inner safe-area container.");
                safeArea.AutoApplyRuntimeSafeArea = false;
                safeArea.ApplySafeArea(full, pixels);
                store.ShowFunctionalSurface(DecorationCatalogueItemKind.PickUpPoint);
                yield return new WaitForSecondsRealtime(.3f);
                Canvas.ForceUpdateCanvases();
                Assert.That(store.IsOpen, Is.True);
                AssertStorePresentation(store, full);
                var previousWidth = Box(store.ContentRect).width;
                safeArea.ApplySafeArea(inset, pixels);
                // No Show, metric override, or private layout call after the inset changes.
                // 安全区改变后不再次Show，确保实际子容器通知能驱动已打开弹窗重排。
                yield return new WaitForSecondsRealtime(.3f);
                Canvas.ForceUpdateCanvases();
                TestContext.WriteLine("Open store previous width=" + previousWidth
                    + "; current=" + Box(store.ContentRect) + "; expected safe=" + inset);
                AssertStorePresentation(store, inset);
                Assert.That(Box(store.ContentRect).width, Is.LessThan(previousWidth));
                store.CloseForOwnerShutdown();
                safeArea.ApplySafeArea(full, pixels);
                yield return null;
                store.ShowFunctionalSurface(DecorationCatalogueItemKind.PickUpPoint);
                yield return new WaitForSecondsRealtime(.3f);
                Canvas.ForceUpdateCanvases();
                AssertStorePresentation(store, full);
                Assert.That(safeArea.GetComponents<P8RModalSafeAreaHost>().Length, Is.EqualTo(1),
                    "Reopening reuses one geometry notifier rather than accumulating hosts.");
                store.CloseForOwnerShutdown();
            }
        }

        private static void AssertStorePresentation(DecorationStoreModalView store, Rect safePixels)
        {
            var card = Box(store.ContentRect);
            AssertContained(card, safePixels, "Open store card");
            Assert.That(card.width, Is.LessThanOrEqualTo(safePixels.width - 96f + 1f),
                "Keep the existing16logical horizontal card margin after safe-area changes.");
            var buttons = new[] { Field<Button>(store, "cancelButton"), Field<Button>(store, "confirmButton") };
            foreach (var button in buttons)
            {
                var bounds = Box(button);
                AssertContained(bounds, card, "Store button " + button.name);
                Assert.That(bounds.width / 3f, Is.GreaterThanOrEqualTo(47.9f));
                Assert.That(bounds.height / 3f, Is.GreaterThanOrEqualTo(47.9f));
            }
            Assert.That(Box(buttons[0]).Overlaps(Box(buttons[1])), Is.False);
            foreach (var name in new[] { "titleLabel", "bodyLabel" })
            {
                var label = Field<TMP_Text>(store, name);
                AssertContained(Box(label), card, "Store " + name);
                Assert.That(label.fontSize * label.GetComponentInParent<Canvas>().rootCanvas.scaleFactor / 3f,
                    Is.GreaterThanOrEqualTo(name == "titleLabel" ? 15.9f : 13.9f));
                Assert.That(label.GetPreferredValues(label.text, label.rectTransform.rect.width, Mathf.Infinity).y,
                    Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f), "Store text must not clip after reflow.");
                foreach (var button in buttons) Assert.That(Box(label).Overlaps(Box(button)), Is.False);
            }
        }

        private IEnumerator CheckInset(Vector2 pixels, Vector2 logical, Rect safePixels)
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(pixels);
                P8RMobileMetrics.EditorLogicalViewportOverride = logical;
                ownedScene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",
                    new LoadSceneParameters(LoadSceneMode.Single));
                yield return null; yield return null; yield return null;
                Assert.That(new Vector2(Screen.width, Screen.height), Is.EqualTo(pixels));
                var readiness = ownedScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<ValidationMessageView>(true)).Single();
                Assert.That(readiness.IsVisible, Is.True, "Use the actual confirmed scene report.");
                var report = readiness.FullReadinessMessage;
                var ids = readiness.DiagnosticIds.ToArray();
                var hud = ownedScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<TimeControlPanel>(true)).Single();
                var hudSafeArea = hud.GetComponentInParent<SafeAreaContainer>(true);
                // Reproduce the real independent-branch order: readiness first, HUD safe area last.
                // 提示先更新、HUD后更新；不能依赖不同SafeArea分支恰好按有利顺序回调。
                foreach (var area in ownedScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SafeAreaContainer>(true))
                    .OrderBy(area => area == hudSafeArea ? 1 : 0))
                {
                    area.AutoApplyRuntimeSafeArea = false;
                    area.ApplySafeArea(safePixels, pixels);
                }
                // No direct readiness refresh: existing production layout notifications own this change.
                // 不由测试主动调用 readiness 的布局方法，防止掩盖父安全区变更通知遗漏。
                yield return new WaitForSecondsRealtime(.3f);
                Canvas.ForceUpdateCanvases();
                TestContext.WriteLine("Readiness parent=" + ((RectTransform)readiness.transform.parent).rect
                    + "; card=" + Box(readiness) + "; expected safe=" + safePixels);
                AssertPresentation(readiness, safePixels, hud);
                var disclosure = Field<Button>(readiness, "disclosureButton");
                Assert.That(disclosure.gameObject.activeInHierarchy, Is.True);
                disclosure.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.1f);
                Canvas.ForceUpdateCanvases();
                Assert.That(readiness.IsDetailsExpanded, Is.True);
                AssertPresentation(readiness, safePixels, hud);
                Assert.That(readiness.FullReadinessMessage, Is.EqualTo(report));
                CollectionAssert.AreEqual(ids, readiness.DiagnosticIds);
            }
        }

        private static void AssertPresentation(ValidationMessageView readiness, Rect safePixels,
            TimeControlPanel hud, float density = 3f)
        {
            var card = Box(readiness);
            AssertContained(card, safePixels, "Readiness card");
            Assert.That(hud, Is.Not.Null);
            Assert.That(Field<RectTransform>(readiness, "p8rHud"), Is.SameAs(hud.transform),
                "Production controller must bind the actual scene HUD; tests do not configure it manually.");
            var hudButtons = hud.GetComponentsInChildren<Button>();
            Assert.That(card.yMax, Is.LessThanOrEqualTo(hudButtons.Min(button => Box(button).yMin) - density),
                "Readiness follows the final HUD bottom after independent safe-area branches update.");
            foreach (var hudButton in hudButtons)
                Assert.That(card.Overlaps(Box(hudButton)), Is.False, "Readiness covers HUD " + hudButton.name);
            var disclosure = Field<Button>(readiness, "disclosureButton");
            var button = Box(disclosure);
            AssertContained(button, card, "Disclosure in card");
            AssertContained(button, safePixels, "Disclosure in safe area");
            Assert.That(button.width / density, Is.GreaterThanOrEqualTo(47.9f));
            Assert.That(button.height / density, Is.GreaterThanOrEqualTo(47.9f));
            var label = Field<TMP_Text>(readiness, "messageLabel");
            var scroll = readiness.GetComponent<ScrollRect>();
            var viewport = Box(scroll.viewport);
            AssertContained(viewport, card, "Visible text viewport");
            AssertContained(viewport, safePixels, "Text safe area");
            var text = Box(label);
            Assert.That(text.xMin, Is.GreaterThanOrEqualTo(viewport.xMin - 1f), "Text left edge");
            Assert.That(text.xMax, Is.LessThanOrEqualTo(viewport.xMax + 1f), "Text right edge");
            Assert.That(viewport.Overlaps(button), Is.False, "Details cannot cover readable text.");
            Assert.That(label.fontSize * label.GetComponentInParent<Canvas>().rootCanvas.scaleFactor / density,
                Is.GreaterThanOrEqualTo(13.9f));
            // Expanded diagnostics may exceed the viewport vertically; their existing ScrollRect owns clipping.
            // 详情允许纵向滚动，不能把完整 content 高度误当作可见正文边界。
            if (text.height > viewport.height + 1f) Assert.That(scroll.vertical, Is.True);
        }

        private static void AssertContained(Rect inner, Rect outer, string label)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - 1f), label + " left");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + 1f), label + " right");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - 1f), label + " bottom");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + 1f), label + " top");
        }

        private static T Field<T>(object owner, string name) => (T)owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private static Rect Box(Component component)
        {
            var corners = new Vector3[4]; ((RectTransform)component.transform).GetWorldCorners(corners);
            var canvas = component.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
    }
}
#endif

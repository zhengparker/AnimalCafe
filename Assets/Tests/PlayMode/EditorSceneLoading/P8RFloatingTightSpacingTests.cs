#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Layout;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    /// <summary>
    /// Real preview transitions must tighten the visible group without stealing adjacent taps.
    /// 真实预览验证约9.4 logical间距及获批44×48触控范围，外观和图标尺寸不变。
    /// </summary>
    public sealed class P8RFloatingTightSpacingTests
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
            yield return UnloadScene();
            P8RMobileMetrics.EditorLogicalViewportOverride = previousProfile;
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator ActualTwoThreeFourActions_TightenFacesWithoutOverlappingTouchTargets()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                foreach (var logical in new[] { new Vector2(360, 640), new Vector2(800, 360) })
                {
                    screen.Resize(logical * 3f);
                    yield return LoadScene(logical);
                    var controller = Component<DecorationModeController>();
                    controller.EnterDecorationMode();
                    yield return BeginWallDecor();
                    AssertFloatingGroup(new[] { "CancelButton", "ConfirmButton" });
                    Cancel();

                    Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
                    Select("furniture.counter.module.01");
                    yield return Settle();
                    AssertFloatingGroup(new[] { "CancelButton", "RotateButton", "ConfirmButton" });
                    Cancel();

                    BeginExistingFurniture(controller);
                    yield return Settle();
                    AssertFloatingGroup(new[] { "StoreButton", "CancelButton", "RotateButton", "ConfirmButton" });
                    Cancel();
                }
            }
        }

        [UnityTest]
        public IEnumerator ModeAndCountChanges_ResetOffsetsInsteadOfAccumulatingOrLeakingIntoFooter()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                var logical = new Vector2(320, 569);
                screen.Resize(logical * 3f);
                yield return LoadScene(logical);
                var controller = Component<DecorationModeController>();
                controller.EnterDecorationMode();
                for (var pass = 0; pass < 2; pass++)
                {
                    Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
                    BeginExistingFurniture(controller);
                    yield return Settle();
                    AssertFloatingGroup(new[] { "StoreButton", "CancelButton", "RotateButton", "ConfirmButton" });
                    Cancel();

                    Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
                    Assert.That(controller.TryHandleSceneTap(new DecorationTouchHit(
                        DecorationTouchHitKind.WallSurface, surfaceId: "wall.back-left")), Is.True);
                    Select("paint.sage");
                    yield return Settle();
                    foreach (var name in new[] { "cancelButton", "confirmButton" })
                    {
                        var button = Action(name);
                        Assert.That(Vector2.Distance(Box(button.image).center, Box(button).center),
                            Is.LessThan(.2f), "Floating offsets must reset for the Wall footer.");
                    }
                    Cancel();

                    yield return BeginWallDecor();
                    var view = Component<DecorationActionBarView>();
                    view.enabled = false;
                    view.enabled = true;
                    yield return Settle();
                    AssertFloatingGroup(new[] { "CancelButton", "ConfirmButton" });
                    Cancel();

                    Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
                    Select("furniture.counter.module.01");
                    yield return Settle();
                    AssertFloatingGroup(new[] { "CancelButton", "RotateButton", "ConfirmButton" });
                    Cancel();
                }
            }
        }

        private IEnumerator BeginWallDecor()
        {
            var controller = Component<DecorationModeController>();
            Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            Assert.That(controller.TryBeginWallMountedPreview("wall-decor.wood-shelf.01",
                "wall.back-left", new WallSlotPosition(4, 0)), Is.True);
            yield return Settle();
        }

        private void BeginExistingFurniture(DecorationModeController controller)
        {
            var item = Component<CafeLayoutRuntime>().Layout.FurnitureInstances
                .First(candidate => candidate.DefinitionId == "furniture.counter.module.01");
            // Invoke the real input callback; no substitute layout or button visibility is injected.
            // 直接调用实际输入回调，预览状态与按钮数量都由production controller产生。
            controller.GetType().GetMethod("HandleFurnitureBegan", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { item.InstanceId });
        }

        private void Select(string id)
        {
            Component<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .First(tile => tile.ItemId == id && tile.gameObject.activeInHierarchy)
                .GetComponent<Button>().onClick.Invoke();
        }

        private void Cancel() => Action("cancelButton").onClick.Invoke();

        private void AssertFloatingGroup(string[] expectedNames)
        {
            var view = Component<DecorationActionBarView>();
            var density = P8RMobileMetrics.For(view).PixelsPerLogicalUnit;
            var buttons = new[] { "storeButton", "cancelButton", "rotateButton", "confirmButton" }
                .Select(Action).Where(button => button.gameObject.activeInHierarchy)
                .OrderBy(button => Box(button).xMin).ToArray();
            Assert.That(buttons.Select(button => button.name), Is.EqualTo(expectedNames));
            // Hand-checked positions for each real count, with room for the 1/64 boundary guard.
            // 独立列出2/3/4按钮的期望位置，避免复用production公式掩盖计算错误。
            var expectedOffsets = buttons.Length == 2 ? new[] { 2.3125f, -2.3125f }
                : buttons.Length == 3 ? new[] { 4.625f, 0f, -4.625f }
                : new[] { 6.9375f, 2.3125f, -2.3125f, -6.9375f };
            for (var index = 0; index < buttons.Length; index++)
            {
                var button = buttons[index];
                var root = Box(button);
                var face = Box(button.image);
                var ink = P8RCompleteFlowTests.MeasuredInk(button.transform.Find("Icon").GetComponent<Image>());
                Assert.That(face.width / density, Is.EqualTo(30f).Within(.08f), button.name + " smaller face");
                Assert.That(face.height / density, Is.EqualTo(30f).Within(.08f), button.name);
                Assert.That(Mathf.Max(ink.width, ink.height) / density, Is.EqualTo(18f).Within(.08f), button.name + " ink");
                Assert.That(root.width / density, Is.EqualTo(44f).Within(.01f), button.name + " approved floating touch width");
                Assert.That(root.height / density, Is.GreaterThanOrEqualTo(47.99f), button.name + " touch height");
                Assert.That(face.xMin, Is.GreaterThanOrEqualTo(root.xMin - .01f * density), button.name + " face left");
                Assert.That(face.xMax, Is.LessThanOrEqualTo(root.xMax + .01f * density), button.name + " face right");
                Assert.That(face.yMin, Is.GreaterThanOrEqualTo(root.yMin - .01f * density), button.name + " face bottom");
                Assert.That(face.yMax, Is.LessThanOrEqualTo(root.yMax + .01f * density), button.name + " face top");
                Assert.That(Vector2.Distance(ink.center, face.center) / density, Is.LessThan(.05f), button.name + " centered ink");
                Assert.That((face.center.x - root.center.x) / density,
                    Is.EqualTo(expectedOffsets[index]).Within(.08f), button.name + " count-dependent inward offset");
                if (index > 0)
                    Assert.That((face.xMin - Box(buttons[index - 1].image).xMax) / density,
                        Is.EqualTo(9.4f).Within(.08f), "Visible gaps stay tight when action count changes.");
                for (var earlier = 0; earlier < index; earlier++)
                    Assert.That(root.Overlaps(Box(buttons[earlier])), Is.False, button.name + " touch roots must not overlap");

                AssertRaycastOwner(button, face.center);
                AssertRaycastOwner(button, new Vector2(root.xMin + .125f * density, root.center.y));
                AssertRaycastOwner(button, new Vector2(root.xMax - .125f * density, root.center.y));
            }
            var visualCenter = (Box(buttons[0].image).xMin + Box(buttons[buttons.Length - 1].image).xMax) * .5f;
            var targetCenter = (Box(buttons[0]).xMin + Box(buttons[buttons.Length - 1]).xMax) * .5f;
            Assert.That(visualCenter, Is.EqualTo(targetCenter).Within(.08f * density), "Visible group remains centered.");
        }

        private static void AssertRaycastOwner(Button expected, Vector2 point)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.That(hits, Is.Not.Empty, expected.name + " must remain reachable at " + point);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(expected),
                expected.name + " owns its visible center and both padded edges");
        }

        private IEnumerator LoadScene(Vector2 logical)
        {
            yield return UnloadScene();
            P8RMobileMetrics.EditorLogicalViewportOverride = logical;
            ownedScene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            yield return null;
            var pixels = logical * 3f;
            foreach (var area in Components<SafeAreaContainer>())
            {
                area.AutoApplyRuntimeSafeArea = false;
                area.ApplySafeArea(new Rect(0, 0, pixels.x, pixels.y), pixels);
            }
            yield return Settle();
        }

        private IEnumerator UnloadScene()
        {
            if (!ownedScene.IsValid() || !ownedScene.isLoaded) yield break;
            var assets = Phase8SceneInputTestCleanup.CaptureAssets(ownedScene);
            foreach (var controller in Components<DecorationModeController>()) controller.enabled = false;
            SceneManager.SetActiveScene(SceneManager.CreateScene("P8RFloatingTightSpacingCleanup"));
            var unload = SceneManager.UnloadSceneAsync(ownedScene);
            while (unload != null && !unload.isDone) yield return null;
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(assets);
            ownedScene = default;
        }

        private Button Action(string name) => (Button)typeof(DecorationActionBarView)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Component<DecorationActionBarView>());
        private T Component<T>() where T : Component => Components<T>().Single();
        private T[] Components<T>() where T : Component => ownedScene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private static IEnumerator Settle()
        {
            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();
        }
        private static Rect Box(Component component)
        {
            var rect = (RectTransform)component.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var canvas = component.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
                points.Max(point => point.x), points.Max(point => point.y));
        }
    }
}
#endif

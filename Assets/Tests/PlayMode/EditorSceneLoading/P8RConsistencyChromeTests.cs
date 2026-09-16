#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    public sealed class P8RConsistencyChromeTests
    {
        private Scene ownedScene;
        private Vector2? previousLogicalViewport;

        [SetUp]
        public void CaptureRuntimeBoundary()
        {
            ownedScene = default;
            previousLogicalViewport = P8RMobileMetrics.EditorLogicalViewportOverride;
        }

        [UnityTearDown]
        public IEnumerator ReleaseOwnedSceneAndRestoreMetrics()
        {
            if (ownedScene.IsValid() && ownedScene.isLoaded)
            {
                var inputAssets = Phase8SceneInputTestCleanup.CaptureAssets(ownedScene);
                foreach (var controller in ownedScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true)))
                {
                    controller.enabled = false;
                }

                var cleanup = SceneManager.CreateScene("P8RConsistencyChromeCleanup");
                SceneManager.SetActiveScene(cleanup);
                var unload = SceneManager.UnloadSceneAsync(ownedScene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }

                Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets);
            }

            P8RMobileMetrics.EditorLogicalViewportOverride = previousLogicalViewport;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SurfaceApplyCancel_FloorWallStateAndViewportRefreshesKeepTheSameCompactGeometry()
        {
            IDisposable screen;
            Action<Vector2> resize;
            if (Application.isBatchMode)
            {
                var nativeScreen = new P8RReferenceLayoutTests.NativeScreenSize();
                screen = nativeScreen;
                resize = nativeScreen.Resize;
            }
            else
            {
                var gameView = new P8RReferenceLayoutTests.RealGameViewSize();
                screen = gameView;
                resize = gameView.Resize;
            }

            using (screen)
            {
                resize(new Vector2(1080f, 1920f));
                yield return WaitForScreenSize(new Vector2Int(1080, 1920));
                P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360f, 640f);
                yield return LoadMainCafe();

                var controller = Find<DecorationModeController>();
                controller.EnterDecorationMode();
                Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                SelectCatalogueItem("floor.warm-wood");
                yield return Settle();

                var actionBar = Find<DecorationActionBarView>();
                actionBar.Show(false, false, PlacementFeedbackKey.None);
                yield return Settle();
                var floorDisabled = CaptureSurfacePair(actionBar, false, "Floor disabled");

                actionBar.Show(false, true, PlacementFeedbackKey.None);
                yield return Settle();
                var floorEnabled = CaptureSurfacePair(actionBar, true, "Floor enabled");
                AssertSamePair(floorDisabled, floorEnabled, "Floor enabled-state refresh");

                Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
                Assert.That(controller.TryHandleSceneTap(new DecorationTouchHit(
                    DecorationTouchHitKind.WallSurface, surfaceId: "wall.back-left")), Is.True);
                SelectCatalogueItem("paint.sage");
                yield return Settle();

                actionBar.Show(false, false, PlacementFeedbackKey.None);
                yield return Settle();
                var wallDisabled = CaptureSurfacePair(actionBar, false, "Wall disabled");

                actionBar.Show(false, true, PlacementFeedbackKey.None);
                yield return Settle();
                var wallEnabled = CaptureSurfacePair(actionBar, true, "Wall enabled");
                AssertSamePair(wallDisabled, wallEnabled, "Wall enabled-state refresh");
                AssertSamePair(floorEnabled, wallEnabled, "Floor and Wall");

                resize(new Vector2(1600f, 720f));
                yield return WaitForScreenSize(new Vector2Int(1600, 720));
                P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(800f, 360f);
                yield return Settle();
                var wallAfterResize = CaptureSurfacePair(actionBar, true, "Wall after viewport refresh");
                AssertSamePair(wallEnabled, wallAfterResize, "Wall viewport refresh");

                Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                SelectCatalogueItem("floor.warm-wood");
                yield return Settle();
                actionBar.Show(false, true, PlacementFeedbackKey.None);
                yield return Settle();
                var floorAfterResize = CaptureSurfacePair(actionBar, true, "Floor after viewport refresh");
                AssertSamePair(floorEnabled, floorAfterResize, "Floor viewport refresh");
                AssertSamePair(floorAfterResize, wallAfterResize, "Floor and Wall after viewport refresh");
            }
        }

        [UnityTest]
        public IEnumerator TargetInstructions_UseInfoArtworkWhileBlockedFeedbackStaysWarning()
        {
            yield return LoadMainCafe();

            var controller = Find<DecorationModeController>();
            controller.EnterDecorationMode();
            Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            Field<Button>(Find<DecorationFloorRangeView>(), "singleGridButton").onClick.Invoke();
            yield return Settle();

            var actionBar = Find<DecorationActionBarView>();
            var appearance = Field<P8RAppearance>(actionBar, "appearance");
            var stateImage = Field<GameObject>(actionBar, "feedbackStateShape").GetComponent<Image>();
            var feedbackLabel = Field<TMP_Text>(actionBar, "feedbackLabel");
            Assert.That(actionBar.VisibleInstructionRect, Is.Not.Null);
            Assert.That(feedbackLabel.text, Is.EqualTo(appearance.Text("feedback.SelectFloorGridTarget")));
            var floorInstructionSprite = stateImage.sprite;

            Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
            yield return Settle();
            Assert.That(actionBar.VisibleInstructionRect, Is.Not.Null);
            Assert.That(feedbackLabel.text, Is.EqualTo(appearance.Text("feedback.SelectWallTarget")));
            var wallInstructionSprite = stateImage.sprite;

            actionBar.ShowInstruction(PlacementFeedbackKey.Blocked);
            yield return Settle();
            Assert.That(feedbackLabel.text, Is.EqualTo(appearance.Text("feedback.Blocked")));
            var blockedSprite = stateImage.sprite;

            Assert.That(
                new[] { floorInstructionSprite, wallInstructionSprite, blockedSprite },
                Is.EqualTo(new[]
                {
                    appearance.Sprite("status_info"),
                    appearance.Sprite("status_info"),
                    appearance.Sprite("status_warning")
                }),
                "Floor and Wall guidance must use info artwork; Blocked must remain a warning.");
        }

        private IEnumerator LoadMainCafe()
        {
            ownedScene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/MainCafe.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            yield return null;
            Assert.That(ownedScene.isLoaded, Is.True);
            Canvas.ForceUpdateCanvases();
        }

        private static IEnumerator Settle()
        {
            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();
        }

        private static IEnumerator WaitForScreenSize(Vector2Int requested)
        {
            for (var frame = 0; frame < 120
                && new Vector2Int(Screen.width, Screen.height) != requested; frame++)
            {
                yield return null;
            }

            Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(requested),
                "Screen resize must update the real render target before geometry is measured. Runner="
                + (Application.isBatchMode ? "batch/NativeScreenSize" : "normal Editor/RealGameViewSize"));
            Canvas.ForceUpdateCanvases();
        }

        private static T Find<T>() where T : Object => Object.FindObjectsByType<T>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Single();

        private static T Field<T>(object owner, string name) => (T)owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private static void SelectCatalogueItem(string itemId)
        {
            var catalogue = Find<DecorationCatalogueView>();
            catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .First(tile => tile.ItemId == itemId && tile.gameObject.activeInHierarchy)
                .GetComponent<Button>().onClick.Invoke();
            catalogue.ShowCatalogue();
        }

        private static SurfacePair CaptureSurfacePair(
            DecorationActionBarView actionBar,
            bool expectedApplyInteractable,
            string context)
        {
            var cancel = Field<Button>(actionBar, "cancelButton");
            var apply = Field<Button>(actionBar, "confirmButton");
            var cancelGeometry = CaptureSurfaceButton(cancel, "Cancel", true, context);
            var applyGeometry = CaptureSurfaceButton(apply, "Apply", expectedApplyInteractable, context);
            Assert.That(Box(cancel).Overlaps(Box(apply)), Is.False, context + ": touch roots overlap.");
            return new SurfacePair(cancelGeometry, applyGeometry);
        }

        private static SurfaceGeometry CaptureSurfaceButton(
            Button button,
            string expectedCopy,
            bool expectedInteractable,
            string context)
        {
            var metrics = P8RMobileMetrics.For(button);
            var units = metrics.UnitsPerLogicalUnit;
            var label = button.transform.Find("Label").GetComponent<TMP_Text>();
            label.ForceMeshUpdate(true, true);
            var preferred = label.GetPreferredValues(label.text);
            var root = (RectTransform)button.transform;
            var face = button.image.rectTransform;
            var expectedFaceWidth = preferred.x / units + 12f;

            Assert.That(label.text, Is.EqualTo(expectedCopy), context + " " + button.name + " final copy");
            Assert.That(button.interactable, Is.EqualTo(expectedInteractable), context + " " + button.name + " state");
            Assert.That(label.fontSize / units, Is.EqualTo(12f).Within(.1f), context + " " + button.name + " font");
            Assert.That(face.rect.height / units, Is.EqualTo(32f).Within(.2f), context + " " + button.name + " face height");
            Assert.That(face.rect.width / units, Is.EqualTo(expectedFaceWidth).Within(.25f),
                context + " " + button.name + " needs 6 logical units on each side of its final copy");
            if (expectedCopy == "Apply")
            {
                Assert.That(root.rect.width / units, Is.EqualTo(Mathf.Max(48f, expectedFaceWidth)).Within(.25f),
                    context + " " + button.name + " root must be measured from final Apply, not transient Confirm");
            }
            Assert.That(root.rect.height / units, Is.GreaterThanOrEqualTo(47.99f), context + " " + button.name + " touch height");
            Assert.That(root.rect.width / units, Is.GreaterThanOrEqualTo(47.99f), context + " " + button.name + " touch width");
            Assert.That(preferred.x, Is.LessThanOrEqualTo(face.rect.width - metrics.Units(12f) + .25f),
                context + " " + button.name + " label width containment");
            Assert.That(preferred.y, Is.LessThanOrEqualTo(face.rect.height + .25f),
                context + " " + button.name + " label height containment");
            Assert.That(label.isTextTruncated, Is.False, context + " " + button.name + " label truncation");
            Assert.That(button.GetComponent<Image>().raycastTarget, Is.True, context + " " + button.name + " hit graphic");
            Assert.That(button.image.raycastTarget, Is.False, context + " " + button.name + " face graphic");

            var screenRect = new Rect(0f, 0f, Screen.width, Screen.height);
            AssertInside(Box(button), screenRect, context + " " + button.name);
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Box(button).center }, hits);
            Assert.That(hits, Is.Not.Empty, context + " " + button.name + " must receive a real raycast.");
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button),
                context + " " + button.name + " must be the topmost target, not hidden behind another UI.");

            return new SurfaceGeometry(
                root.rect.width / units,
                root.rect.height / units,
                face.rect.width / units,
                face.rect.height / units,
                label.fontSize / units);
        }

        private static Rect Box(Component component)
        {
            var rect = (RectTransform)component.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
            return Rect.MinMaxRect(
                points.Min(point => point.x), points.Min(point => point.y),
                points.Max(point => point.x), points.Max(point => point.y));
        }

        private static void AssertInside(Rect inner, Rect outer, string context)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - .5f), context + " left");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - .5f), context + " bottom");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + .5f), context + " right");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + .5f), context + " top");
        }

        private static void AssertSamePair(SurfacePair expected, SurfacePair actual, string context)
        {
            AssertSameGeometry(expected.Cancel, actual.Cancel, context + " Cancel");
            AssertSameGeometry(expected.Apply, actual.Apply, context + " Apply");
        }

        private static void AssertSameGeometry(SurfaceGeometry expected, SurfaceGeometry actual, string context)
        {
            Assert.That(actual.RootWidth, Is.EqualTo(expected.RootWidth).Within(.1f), context + " root width");
            Assert.That(actual.RootHeight, Is.EqualTo(expected.RootHeight).Within(.1f), context + " root height");
            Assert.That(actual.FaceWidth, Is.EqualTo(expected.FaceWidth).Within(.1f), context + " face width");
            Assert.That(actual.FaceHeight, Is.EqualTo(expected.FaceHeight).Within(.1f), context + " face height");
            Assert.That(actual.FontSize, Is.EqualTo(expected.FontSize).Within(.1f), context + " font size");
        }

        private readonly struct SurfacePair
        {
            public SurfacePair(SurfaceGeometry cancel, SurfaceGeometry apply)
            {
                Cancel = cancel;
                Apply = apply;
            }

            public SurfaceGeometry Cancel { get; }
            public SurfaceGeometry Apply { get; }
        }

        private readonly struct SurfaceGeometry
        {
            public SurfaceGeometry(float rootWidth, float rootHeight, float faceWidth, float faceHeight, float fontSize)
            {
                RootWidth = rootWidth;
                RootHeight = rootHeight;
                FaceWidth = faceWidth;
                FaceHeight = faceHeight;
                FontSize = fontSize;
            }

            public float RootWidth { get; }
            public float RootHeight { get; }
            public float FaceWidth { get; }
            public float FaceHeight { get; }
            public float FontSize { get; }
        }
    }
}
#endif

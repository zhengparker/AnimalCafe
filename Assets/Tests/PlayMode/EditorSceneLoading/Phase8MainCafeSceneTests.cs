#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    public sealed class Phase8MainCafeSceneTests
    {
        private const string ScenePath = "Assets/Scenes/MainCafe.unity";

        [UnityTearDown]
        public IEnumerator RestoreCleanScene()
        {
            Time.timeScale = 1f;
            var active = SceneManager.GetActiveScene();
            var cleanup = SceneManager.CreateScene("Phase8MainCafeCleanup");
            SceneManager.SetActiveScene(cleanup);
            if (active.IsValid() && active.isLoaded && active != cleanup)
            {
                var unload = SceneManager.UnloadSceneAsync(active);
                while (unload != null && !unload.isDone) yield return null;
            }
        }

        [UnityTest]
        public IEnumerator FreshLoad_AwakeHasOneReadyRuntimeAndNoDebugOrPickUpIndicators()
        {
            yield return Load();

            var controller = Object.FindObjectsByType<DecorationModeController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            var runtime = Object.FindFirstObjectByType<CafeLayoutRuntime>();
            Assert.That(runtime.Layout, Is.Not.Null);
            Assert.That(runtime.FunctionalSurfaceLayout, Is.Not.Null);
            Assert.That(runtime.CurrentReadiness, Is.Not.Null);
            Assert.That(controller.IsOpen, Is.False);
            Assert.That(Object.FindObjectsByType<SurfaceMountedSceneRegistry>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<SurfaceMountedPreviewView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<PickUpPointIndicatorView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Select(canvas => canvas.name),
                Is.EquivalentTo(new[] { "HUD Canvas", "Screen Canvas", "Toast Canvas" }));
            Assert.That(Object.FindObjectsByType<InteractionAnchorDebugView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(Object.FindObjectsByType<Transform>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(item => item.name.StartsWith("AnchorDebug_")
                    || item.name.StartsWith("PickUpPoint_")), Is.False);
        }

        [UnityTest]
        public IEnumerator FurnitureTab_UsesThreeProductionRowsOnePickUpButtonAndStartsControllerFlow()
        {
            yield return Load();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode();
            yield return null;
            yield return null;

            var catalogue = Object.FindObjectsByType<DecorationCatalogueView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            Assert.That(catalogue.CategoryRows, Has.Count.EqualTo(3));
            Assert.That(catalogue.CategoryRows.Select(row => row.HorizontalScroll.content.name),
                Is.All.Not.Empty);
            var tiles = catalogue.CategoryRows
                .SelectMany(row => row.HorizontalScroll.content
                    .GetComponentsInChildren<DecorationCatalogueTileView>(false))
                .ToArray();
            Assert.That(tiles.Count(tile => tile.ItemId == "equipment.cash-register.01"),
                Is.EqualTo(1));
            Assert.That(tiles.Count(tile => tile.ItemId == "equipment.coffee-machine.01"),
                Is.EqualTo(1));
            Assert.That(tiles.Select(tile => tile.ItemId).Distinct().Count(),
                Is.EqualTo(tiles.Length));
            var pickUpButtons = catalogue.GetComponentsInChildren<Button>(true)
                .Where(button => button.name == "PickUpPointButton").ToArray();
            Assert.That(pickUpButtons, Has.Length.EqualTo(1));

            tiles.Single(tile => tile.ItemId == "equipment.cash-register.01")
                .GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(controller.ActiveFunctionalSurfacePreview, Is.Not.Null);
            Assert.That(controller.ActiveFunctionalSurfacePreview.Kind,
                Is.EqualTo(FunctionalSurfacePreviewKind.MountedEquipment));
            Assert.That(controller.ActiveFunctionalSurfacePreview.DefinitionId,
                Is.EqualTo("equipment.cash-register.01"));
            Assert.That(Object.FindFirstObjectByType<SurfaceMountedPreviewView>().CurrentGhost,
                Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator Reload_DoesNotDuplicateRowsViewsOrControllerSubscriptions()
        {
            yield return Load();
            yield return AssertRuntimeCountsAndSingleSelection();
            yield return Load();
            yield return AssertRuntimeCountsAndSingleSelection();
        }

        [UnityTest]
        public IEnumerator ClosedMode_UsesEffectiveCanvasVisibilityThenEnterEnablesLegacyTabs()
        {
            yield return Load();
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>(
                FindObjectsInactive.Include);
            var tabs = Object.FindFirstObjectByType<DecorationModeTabsView>(
                FindObjectsInactive.Include);
            var group = catalogue.GetComponent<CanvasGroup>();
            Assert.That(group.alpha, Is.EqualTo(0f).Within(.001f));
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(tabs.gameObject.activeInHierarchy && group.alpha > 0f,
                Is.False, "Closed tabs must not be effectively visible.");

            Object.FindFirstObjectByType<DecorationModeController>().EnterDecorationMode();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(group.alpha, Is.EqualTo(1f).Within(.001f));
            Assert.That(group.blocksRaycasts, Is.True);
            Assert.That(tabs.gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator LegacyTabs_ClickFloorAndSingleGridThenWallDecorShowsTwoWindows()
        {
            yield return Load();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode();
            yield return null;
            var tabs = Object.FindFirstObjectByType<DecorationModeTabsView>();

            Assert.That(tabs.RequestMode(DecorationModeKind.Floor), Is.True);
            yield return null;
            Assert.That(controller.ActiveMode, Is.EqualTo(DecorationModeKind.Floor));
            var range = Object.FindFirstObjectByType<DecorationFloorRangeView>();
            Assert.That(range, Is.Not.Null);
            var singleGrid = range.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SingleGridButton");
            singleGrid.onClick.Invoke();
            yield return null;
            Assert.That(controller.FloorRange, Is.EqualTo(SurfaceEditScope.SingleGridFloor));

            Assert.That(tabs.RequestMode(DecorationModeKind.WallDecor), Is.True);
            yield return null;
            Assert.That(controller.ActiveMode, Is.EqualTo(DecorationModeKind.WallDecor));
            var windowTiles = Object.FindFirstObjectByType<DecorationCatalogueView>()
                .GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Where(tile => tile.ItemId != null && tile.ItemId.StartsWith("window."))
                .ToArray();
            Assert.That(windowTiles, Has.Length.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator BeginnerThreeCounterExample_RealCatalogueConfirmProducesReadyFeedback()
        {
            yield return Load();
            var runtime = Object.FindFirstObjectByType<CafeLayoutRuntime>();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            var supports = new[]
            {
                runtime.Layout.FurnitureInstances.Single(),
                FurnitureInstance.CreateNew("furniture.counter.module.01",
                    new GridPosition(4, 3), FurnitureRotation.Degrees0),
                FurnitureInstance.CreateNew("furniture.counter.module.01",
                    new GridPosition(6, 3), FurnitureRotation.Degrees0)
            };
            foreach (var support in supports.Skip(1))
                Assert.That(runtime.Layout.PlaceFurniture(support).Succeeded, Is.True);
            Object.FindFirstObjectByType<FurnitureSceneRegistry>()
                .Rebuild(runtime.Layout.FurnitureInstances);
            controller.EnterDecorationMode();
            yield return new WaitForSecondsRealtime(.2f);
            var definitions = new[] { "equipment.cash-register.01", "equipment.coffee-machine.01", null };
            for (var index = 0; index < supports.Length; index++)
            {
                if (catalogue.IsCollapsed)
                {
                    var handle = catalogue.GetComponentsInChildren<Button>(true)
                        .Single(button => button.name == "CollapsedHandle");
                    Assert.That(handle.gameObject.activeInHierarchy && handle.interactable, Is.True,
                        $"Step {index + 1}: the visible handle must reopen the catalogue.");
                    handle.onClick.Invoke();
                }
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(catalogue.SheetState, Is.EqualTo(DecorationSheetState.Expanded),
                    $"Step {index + 1}: expanding with the real handle must update the sheet state.");
                Assert.That(catalogue.IsCollapsed, Is.False);
                if (definitions[index] != null)
                    catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                        .Single(tile => tile.ItemId == definitions[index])
                        .GetComponent<Button>().onClick.Invoke();
                else
                    catalogue.GetComponentsInChildren<Button>(true)
                        .Single(button => button.name == "PickUpPointButton").onClick.Invoke();
                Assert.That(controller.ActiveFunctionalSurfacePreview, Is.Not.Null,
                    $"Step {index + 1}: {definitions[index] ?? "Pick-up Point"} must begin a preview.");
                Assert.That(controller.TryMoveFunctionalSurfacePreview(
                    new SurfaceSlotAddress(supports[index].InstanceId, "slot.0")), Is.True);
                Object.FindFirstObjectByType<DecorationActionBarView>()
                    .GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "ConfirmButton").onClick.Invoke();
                Assert.That(controller.ActiveFunctionalSurfacePreview, Is.Null,
                    "The actual Confirm button must finish preview and update the sheet state.");
                yield return new WaitForSecondsRealtime(.2f);
                if (index == 0) yield return CaptureReviewFrame("p8r-fix-missing-functions.png");
            }

            var report = runtime.CurrentReadiness;
            Assert.That(report.CanOpenForBusiness, Is.True);
            Assert.That(report.Failures, Is.Empty);
            Assert.That(report.Stations, Has.Count.EqualTo(3));
            foreach (var station in report.Stations)
            {
                var support = supports.Single(item => item.InstanceId == station.SupportFurnitureInstanceId);
                Assert.That(station.Anchors.TryGetAnchor(InteractionRole.Employee, out var employee), Is.True);
                Assert.That(employee.Position, Is.EqualTo(new GridPosition(support.Position.X, 4)));
                if (station.FunctionType == LayoutStationType.CoffeeMachine) continue;
                Assert.That(station.Anchors.TryGetAnchor(InteractionRole.Customer, out var customer), Is.True);
                Assert.That(customer.Position, Is.EqualTo(new GridPosition(support.Position.X, 2)));
            }
            var feedback = Object.FindFirstObjectByType<ValidationMessageView>();
            Assert.That(feedback.IsVisible, Is.True);
            Assert.That(feedback.CurrentMessage, Is.EqualTo("布局已准备好，可以营业"));
            Assert.That(feedback.GetComponent<ScrollRect>(), Is.Not.Null,
                "The handoff Scene must include the actual bounded feedback viewport.");
            yield return CaptureReviewFrame("p8r-fix-ready-layout.png");
            yield return CapturePickUpAndFeedbackReview(runtime, controller);
        }

        private static IEnumerator CapturePickUpAndFeedbackReview(
            CafeLayoutRuntime runtime, DecorationModeController controller)
        {
            // Optional real-scene evidence; never treated as owner visual acceptance.
            // 只记录真实运行时的截图，不保存或改写生产 Scene。
            if (System.Environment.GetEnvironmentVariable("ANIMALCAFE_P8_REVIEW_CAPTURE") != "1") yield break;
            const string folder = "phase8-manual-m6-20260908/";
            var point = runtime.FunctionalSurfaceLayout.PickUpPoints.Single();
            Assert.That(controller.TryBeginExistingFunctionalSurfacePreview(
                FunctionalSurfacePreviewKind.PickUpPoint, point.InstanceId), Is.True);
            yield return CaptureReviewFrame(folder + "pickup-table.png");
            controller.TryMoveFunctionalSurfacePreview(default);
            Assert.That(controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.False);
            Assert.That(Object.FindFirstObjectByType<PickUpPointIndicatorView>().CurrentPreview,
                Is.Not.Null, "An invalid ground Pick-up must remain visible.");
            yield return CaptureReviewFrame(folder + "pickup-ground.png");
            controller.CancelFunctionalSurfacePreview();

            var blocker = FurnitureInstance.CreateNew("furniture.counter.module.01",
                new GridPosition(2, 4), FurnitureRotation.Degrees0);
            Assert.That(runtime.Layout.PlaceFurniture(blocker).Succeeded, Is.True);
            Object.FindFirstObjectByType<FurnitureSceneRegistry>()
                .Rebuild(runtime.Layout.FurnitureInstances);
            runtime.RecalculateReadiness();
            var view = Object.FindFirstObjectByType<ValidationMessageView>();
            view.ShowReadiness(runtime.CurrentReadiness);
            Assert.That(view.CurrentMessage, Does.Contain("员工").And.Contain("阻挡")
                .And.Not.Contain("Employee").And.Not.Contain("slot.0"));
            foreach (var id in view.DiagnosticIds)
                Assert.That(view.CurrentMessage, Does.Not.Contain(id));
            yield return CaptureReviewFrame(folder + "readable-feedback.png");
        }

        private static IEnumerator CaptureReviewFrame(string filename)
        {
            // Opt-in automated evidence only; this does not count as owner manual acceptance.
            if (System.Environment.GetEnvironmentVariable("ANIMALCAFE_P8_REVIEW_CAPTURE") != "1") yield break;
            var camera = Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsSortMode.None)
                .Single(item => item.CompareTag("MainCamera"));
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(item => item.isRootCanvas).Select(item => new
                { Canvas = item, item.renderMode, item.worldCamera, item.planeDistance }).ToArray();
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            // Preserve Game view dimensions so screen-space UI uses the same pixel coordinates.
            var captureWidth = camera.pixelWidth;
            var captureHeight = camera.pixelHeight;
            var target = new RenderTexture(captureWidth, captureHeight, 24);
            var pixels = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
            target.Create();
            try
            {
                camera.targetTexture = target;
                foreach (var state in canvases)
                {
                    state.Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    state.Canvas.worldCamera = camera;
                    state.Canvas.planeDistance = Mathf.Max(camera.nearClipPlane + .1f, .5f);
                }
                Canvas.ForceUpdateCanvases();
                // Capture changes the Camera pixelRect; refresh screen-anchored actions too.
                // 截图切换了投影尺寸，需同步按钮锚点，并让现有 UI transition 结束。
                RefreshReviewActionPresentation();
                yield return new WaitForSecondsRealtime(.3f);
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
                pixels.Apply();
                System.IO.Directory.CreateDirectory("outputs");
                System.IO.File.WriteAllBytes(System.IO.Path.Combine("outputs", filename), pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                foreach (var state in canvases)
                {
                    state.Canvas.renderMode = state.renderMode;
                    state.Canvas.worldCamera = state.worldCamera;
                    state.Canvas.planeDistance = state.planeDistance;
                }
                Canvas.ForceUpdateCanvases();
                RefreshReviewActionPresentation();
                target.Release();
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        private static void RefreshReviewActionPresentation()
        {
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            if (controller == null) return;
            typeof(DecorationModeController).GetMethod("UpdateActionPresentation",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(controller, null);
        }

        private static IEnumerator AssertRuntimeCountsAndSingleSelection()
        {
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode();
            yield return null;
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            Assert.That(catalogue.CategoryRows, Has.Count.EqualTo(3));
            Assert.That(Object.FindObjectsByType<DecorationCatalogueView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<DecorationActionBarView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
            var tile = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Single(candidate => candidate.ItemId == "equipment.cash-register.01");
            tile.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(controller.ActiveFunctionalSurfacePreview, Is.Not.Null);
            var instanceId = controller.ActiveFunctionalSurfacePreview.InstanceId;
            tile.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(controller.ActiveFunctionalSurfacePreview.InstanceId, Is.EqualTo(instanceId),
                "A duplicate subscription must not replace the one active Preview.");
            controller.CancelFunctionalSurfacePreview();
        }

        private static IEnumerator Load()
        {
            EditorSceneManager.LoadSceneInPlayMode(
                ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            yield return null;
        }
    }
}
#endif

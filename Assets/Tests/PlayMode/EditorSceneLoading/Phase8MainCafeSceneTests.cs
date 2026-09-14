#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using NUnit.Framework;
using TMPro;
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
        private static bool UsesP8R(Component view) => view.GetType().GetField("appearance",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(view) is AnimalCafe.UI.P8R.P8RAppearance;

        [UnityTest]
        public IEnumerator CatalogueMemory_RealPrefabRestoresTabScrollAndResetsOnReentry()
        {
            yield return Load();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            // Exercise the real prefab layout at a narrow panel width; do not fabricate row bounds.
            // 只改变临时实例宽度模拟窄屏，保留全部生产 LayoutGroup 和 ScrollRect。
            ((RectTransform)catalogue.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, 320f);
            controller.EnterDecorationMode();
            yield return new WaitForSecondsRealtime(.25f);
            var vertical = (ScrollRect)typeof(DecorationCatalogueView).GetField("verticalScroll",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(catalogue);
            Canvas.ForceUpdateCanvases();
            Assert.That(((RectTransform)catalogue.transform).rect.width, Is.EqualTo(320f).Within(.1f));
            Assert.That(vertical.content.rect.height, Is.GreaterThan(vertical.viewport.rect.height));
            vertical.verticalNormalizedPosition = .35f;
            Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
            yield return new WaitForSecondsRealtime(.25f);
            var rowIndex = catalogue.CategoryRows.ToList().FindIndex(row => row.HorizontalScroll.content.rect.width
                > (row.HorizontalScroll.viewport != null ? row.HorizontalScroll.viewport.rect.width
                    : ((RectTransform)row.HorizontalScroll.transform).rect.width));
            Assert.That(rowIndex, Is.GreaterThanOrEqualTo(0), "Use an actually scrollable production row, not synthetic bounds.");
            catalogue.CategoryRows[rowIndex].HorizontalScroll.horizontalNormalizedPosition = .6f;
            Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(vertical.verticalNormalizedPosition, Is.EqualTo(.35f).Within(.015f));
            Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(catalogue.CategoryRows[rowIndex].HorizontalScroll.horizontalNormalizedPosition,
                Is.EqualTo(.6f).Within(.015f));
            controller.ExitDecorationMode();
            controller.EnterDecorationMode();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(vertical.verticalNormalizedPosition, Is.EqualTo(1f).Within(.015f));
            Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(catalogue.CategoryRows[rowIndex].HorizontalScroll.horizontalNormalizedPosition,
                Is.EqualTo(0f).Within(.015f));
        }

        [UnityTest]
        public IEnumerator FootprintLight_ProductionControllerWiresFloorAndWallFills()
        {
            yield return Load();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode();
            yield return new WaitForSecondsRealtime(.25f);
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            var furniture = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .First(tile => tile.gameObject.activeInHierarchy && !string.IsNullOrEmpty(tile.ItemId)
                    && tile.ItemId.StartsWith("furniture."));
            furniture.GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(Object.FindFirstObjectByType<DecorationActionBarView>().IsVisible, Is.True);
            var fills = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(renderer => renderer.name == "Fill" && renderer.transform.parent.name.StartsWith("FootprintCell_")).ToArray();
            Assert.That(fills, Is.Not.Empty);
            Assert.That(fills.All(renderer => renderer.sharedMaterial.shader.name == "AnimalCafe/Phase8/FootprintLight"), Is.True);
            var furniturePreview = Object.FindFirstObjectByType<FurniturePreviewView>();
            Assert.That(furniturePreview.TryGetWorldBounds(out var furnitureBounds), Is.True);
            yield return CaptureReviewDetailFrame("furniture-footprint.png", furnitureBounds.center);
            Assert.That(furniturePreview.CurrentPreviewTransform, Is.Not.Null);
            Object.FindFirstObjectByType<DecorationActionBarView>().GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "CancelButton").onClick.Invoke();
            Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            yield return new WaitForSecondsRealtime(.25f);
            catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .First(tile => tile.gameObject.activeInHierarchy && !string.IsNullOrEmpty(tile.ItemId))
                .GetComponent<Button>().onClick.Invoke();
            var projection = Object.FindFirstObjectByType<WallMountedPreviewView>();
            var wallFills = projection.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.sharedMaterial.shader.name == "AnimalCafe/Phase8/FootprintLight").ToArray();
            Assert.That(wallFills, Has.Length.EqualTo(1));
            yield return CaptureReviewFrame("wall-footprint.png");
            controller.CancelActivePhase7Preview();
        }

        [UnityTest]
        public IEnumerator WallDecor_ProductionPaintingAndShelfHoverWhileProjectionKeepsRealSlots()
        {
            yield return Load();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode();
            Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            var definitions = new[] { "wall-decor.shiba-painting.01", "wall-decor.wood-shelf.01" };
            var surfaces = new[] { "wall.back-right", "wall.back-left" };
            var filenames = new[] { "wall-painting-preview.png", "wall-shelf-preview.png" };
            for (var i = 0; i < definitions.Length; i++)
            {
                Assert.That(controller.TryBeginWallMountedPreview(
                    definitions[i], surfaces[i], new WallSlotPosition(4, 0)), Is.True);
                var view = Object.FindFirstObjectByType<WallMountedPreviewView>();
                var wall = Object.FindObjectsByType<WallSurfaceAuthoring>(FindObjectsSortMode.None)
                    .Single(item => item.SurfaceId == surfaces[i]);
                var preview = controller.ActiveWallMountedPreview;
                var projectionLocal = wall.transform.InverseTransformPoint(view.CurrentProjection.transform.position);
                var contactCenter = wall.GetWallMountedWorldPosition(
                    new Vector3(projectionLocal.x, projectionLocal.y, 0f),
                    WallSurfaceAuthoring.WallMountedPlaneEpsilon);
                var expected = contactCenter - wall.transform.up * (preview.Footprint.Height * wall.SlotSize * .5f)
                    - wall.transform.forward * .2f;
                Assert.That(Vector3.Distance(view.CurrentGhost.transform.position, expected), Is.LessThan(.0001f));
                var projectionSize = view.CurrentProjection.GetComponent<MeshFilter>().sharedMesh.bounds.size;
                Assert.That(projectionSize.x, Is.EqualTo(preview.Footprint.Width * wall.SlotSize).Within(.0001f));
                Assert.That(projectionSize.y, Is.EqualTo(preview.Footprint.Height * wall.SlotSize).Within(.0001f));
                var renderers = view.CurrentGhost.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers, Is.Not.Empty);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    Assert.That(block.isEmpty, Is.True, "Production wall models retain their original appearance.");
                }
                yield return CaptureReviewDetailFrame(filenames[i], bounds.center);
                controller.CancelActivePhase7Preview();
                Assert.That(view.CurrentGhost, Is.Null);
                Assert.That(view.CurrentProjection, Is.Null);
            }
        }

        [UnityTearDown]
        public IEnumerator RestoreCleanScene()
        {
            Time.timeScale = 1f;
            var active = SceneManager.GetActiveScene();
            var cleanup = SceneManager.CreateScene("Phase8MainCafeCleanup");
            SceneManager.SetActiveScene(cleanup);
            if (active.IsValid() && active.isLoaded && active != cleanup)
            {
                var inputAssets = Phase8SceneInputTestCleanup.CaptureAssets(active);
                var unload = SceneManager.UnloadSceneAsync(active);
                while (unload != null && !unload.isDone) yield return null;
                Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets);
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
                if (index == 0)
                {
                    var notice = Object.FindFirstObjectByType<ValidationMessageView>();
                    var versionBeforeDetails = runtime.ReadinessVersion;
                    var detailsButton = notice.GetComponentsInChildren<Button>(true).Single(button => button.name == "ReadinessDetails");
                    yield return CaptureReviewFrame("readiness-compact.png");
                    detailsButton.onClick.Invoke();
                    if (UsesP8R(notice)) Assert.That(notice.CurrentMessage,
                        Does.Contain("Blocking: Coffee Machine: Add at least one Coffee Machine.")
                            .And.Contain("Blocking: Pickup Point: Add at least one Pickup Point."));
                    else Assert.That(notice.CurrentMessage, Does.Contain("咖啡机").And.Contain("取餐点"));
                    Assert.That(runtime.ReadinessVersion, Is.EqualTo(versionBeforeDetails));
                    yield return CaptureReviewFrame("readiness-details.png");
                    detailsButton.onClick.Invoke();
                }
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
            Assert.That(feedback.IsVisible, Is.False,
                "A confirmed layout with no warnings or blocking failures must not keep a success card visible.");
            Assert.That(feedback.CurrentMessage, Is.Empty);
            Assert.That(feedback.FullReadinessMessage,
                Does.Contain(UsesP8R(feedback) ? "Confirmed Layout: Ready to open" : "已确认布局：已就绪"),
                "The latest healthy report remains available without occupying HUD space.");
            var feedbackGroup = feedback.GetComponent<CanvasGroup>();
            Assert.That(feedbackGroup.alpha, Is.Zero);
            Assert.That(feedbackGroup.interactable, Is.False);
            Assert.That(feedbackGroup.blocksRaycasts, Is.False);
            Assert.That(feedback.GetComponent<ScrollRect>(), Is.Not.Null,
                "The handoff Scene must include the actual bounded feedback viewport.");
            yield return CaptureReviewFrame("p8r-fix-ready-layout.png");
            yield return CaptureHealthyReadyNativeOverlay(feedback);
            yield return ReviewEditingFeedbackInProductionScene(runtime, controller);
            yield return CapturePickUpAndFeedbackReview(runtime, controller);
        }

        [UnityTest]
        public IEnumerator ApprovedEditingUx_FloorAndWallSwatchesRemainInsideTheSamePreview()
        {
            yield return Load();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode();
            yield return null;
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            foreach (var mode in new[] { DecorationModeKind.Floor, DecorationModeKind.Wall })
            {
                Assert.That(controller.TryChangeMode(mode), Is.True);
                if (mode == DecorationModeKind.Wall)
                {
                    var wall = Object.FindObjectsByType<AnimalCafe.Content.WallSurfaceAuthoring>(FindObjectsSortMode.None)
                        .First(item => !string.IsNullOrEmpty(item.SurfaceId));
                    Assert.That(controller.TryHandleSceneTap(new DecorationTouchHit(
                        DecorationTouchHitKind.WallSurface, surfaceId: wall.SurfaceId)), Is.True);
                }
                yield return new WaitForSecondsRealtime(.2f);
                var tiles = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                    .Where(item => item.gameObject.activeInHierarchy && !string.IsNullOrEmpty(item.ItemId)).Take(2).ToArray();
                Assert.That(tiles, Has.Length.EqualTo(2));
                tiles[0].GetComponent<Button>().onClick.Invoke();
                Assert.That(controller.ActiveSurfacePreview, Is.Not.Null);
                var preview = controller.ActiveSurfacePreview;
                tiles[1].GetComponent<Button>().onClick.Invoke();
                // SurfacePreviewTransaction is an immutable snapshot; style edits publish a new snapshot
                // of the same target/baseline, not the same C# object reference.
                Assert.That(controller.ActiveSurfacePreview.Scope, Is.EqualTo(preview.Scope));
                Assert.That(controller.ActiveSurfacePreview.TargetWallSurfaceId, Is.EqualTo(preview.TargetWallSurfaceId));
                Assert.That(controller.ActiveSurfacePreview.UsingStyleId, Is.EqualTo(preview.UsingStyleId));
                Assert.That(controller.ActiveSurfacePreview.PreviewStyleId, Is.EqualTo(tiles[1].ItemId));
                Assert.That(catalogue.State, Is.EqualTo(DecorationCatalogueState.Expanded));
                var button = catalogue.GetComponentsInChildren<Button>(true).Single(item => item.name == "ReturnToEditing");
                Assert.That(button.gameObject.activeInHierarchy, Is.False);
                controller.CancelActivePhase7Preview();
            }
        }

        [UnityTest]
        public IEnumerator ReadinessVisibility_HealthyErrorHealthyPublishesOneEventPerTransition()
        {
            yield return Load();
            var view = Object.FindFirstObjectByType<ValidationMessageView>();
            Assert.That(UsesP8R(view), Is.True, "Exercise the real P8R geometry publisher.");
            view.ShowReadiness(P8RCompleteFlowTests.Report(true));
            Assert.That(view.IsVisible, Is.False);

            var changes = 0;
            System.Action countChange = () => changes++;
            view.DetailsVisibilityChanged += countChange;
            try
            {
                view.ShowReadiness(P8RCompleteFlowTests.Report(false,
                    P8RCompleteFlowTests.Failure(LayoutReadinessSeverity.Blocking,
                        LayoutReadinessFailureCode.MissingCoffeeMachine, 0)));
                Assert.That(view.IsVisible, Is.True);
                Assert.That(changes, Is.EqualTo(1),
                    "Showing one actionable card must publish one geometry change.");

                view.ShowReadiness(P8RCompleteFlowTests.Report(true));
                Assert.That(view.IsVisible, Is.False);
                Assert.That(changes, Is.EqualTo(2),
                    "Removing the healthy card must publish one additional geometry change.");
            }
            finally { view.DetailsVisibilityChanged -= countChange; }
        }

        [UnityTest]
        public IEnumerator FloorUx_ShowsScopeAndChangedCellCount_AndUndoOnlyTouchesCurrentPreview()
        {
            yield return Load();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            var runtime = Object.FindFirstObjectByType<CafeLayoutRuntime>();
            controller.EnterDecorationMode();
            yield return null;
            Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            Assert.That(controller.TrySelectFloorRange(SurfaceEditScope.SingleGridFloor), Is.True);
            Assert.That(controller.TrySelectFloorTarget(new GridPosition(1, 1)), Is.True);
            var original = runtime.RoomSurfaceLayout.CaptureSnapshot();
            var baselineStyle = original.FloorTiles.Single(tile => tile.X == 1 && tile.Y == 1).StyleId;
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            yield return new WaitForSecondsRealtime(.2f);
            var tileButton = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .First(tile => tile.gameObject.activeInHierarchy && !string.IsNullOrEmpty(tile.ItemId)
                    && tile.ItemId != baselineStyle);
            tileButton.GetComponent<Button>().onClick.Invoke();
            Assert.That(controller.ActiveSurfacePreview, Is.Not.Null);
            var banner = catalogue.GetComponentsInChildren<TMP_Text>(true)
                .Single(label => label.name == "Message" && label.transform.parent.name == "EditingContext");
            var p8r = UsesP8R(catalogue);
            Assert.That(banner.text, p8r ? Does.Contain("Single Grid - 1 tile changed").And.Contain("Undo affects this preview only.")
                : Does.Contain("逐格涂抹").And.Contain("已改 1 格").And.Contain("本次预览"));
            Assert.That(controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.FloorGrid, floorPosition: new GridPosition(2, 1))), Is.True);
            Assert.That(banner.text, Does.Contain(p8r ? "Single Grid - 2 tiles changed" : "已改 2 格"));
            var action = Object.FindFirstObjectByType<DecorationActionBarView>();
            yield return CaptureReviewFrame("floor-scope.png");
            var apply = action.GetComponentsInChildren<Button>(true).Single(button => button.name == "ApplyAllButton");
            var undo = action.GetComponentsInChildren<Button>(true).Single(button => button.name == "UndoLastButton");
            // Compact mobile rows retain these commands as icons; their owned labels keep the copy.
            // 紧凑手机布局可隐藏文字，但 Apply All / Undo 命令必须仍可见、可用。
            Assert.That(apply.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo(p8r ? "Apply All" : "铺满整个房间"));
            Assert.That(undo.GetComponentInChildren<TMP_Text>(true).text, Does.Contain(p8r ? "Undo" : "撤销"));
            Assert.That(apply.gameObject.activeInHierarchy && apply.interactable, Is.True);
            Assert.That(undo.gameObject.activeInHierarchy && undo.interactable, Is.True);
            apply.onClick.Invoke();
            Assert.That(banner.text, Does.Contain(p8r ? "Single Grid - 64 tiles changed" : "已改 64 格"));
            undo.onClick.Invoke();
            Assert.That(banner.text, Does.Contain(p8r ? "Single Grid - 2 tiles changed" : "已改 2 格"));
            undo.onClick.Invoke();
            Assert.That(banner.text, Does.Contain(p8r ? "Single Grid - 1 tile changed" : "已改 1 格"));
            undo.onClick.Invoke();
            Assert.That(banner.text, Does.Contain(p8r ? "Single Grid - 0 tiles changed" : "已改 0 格"));
            Assert.That(controller.ActiveSurfacePreview.HasChanges, Is.False);
            Assert.That(runtime.RoomSurfaceLayout.CaptureSnapshot().FloorTiles
                .Select(tile => (tile.X, tile.Y, tile.StyleId, tile.Rotation)),
                Is.EquivalentTo(original.FloorTiles.Select(tile => (tile.X, tile.Y, tile.StyleId, tile.Rotation))));
            controller.CancelActivePhase7Preview();
            Assert.That(controller.ActiveSurfacePreview, Is.Null);
            Assert.That(controller.TrySelectFloorRange(SurfaceEditScope.WholeRoomFloor), Is.True);
            tileButton.GetComponent<Button>().onClick.Invoke();
            Assert.That(banner.text, p8r ? Does.Contain("Whole Room - 64 tiles changed") : Does.Contain("整个房间").And.Contain("已改 64 格"));
            Assert.That(undo.interactable, Is.False, "Whole Room keeps its original Confirm/Cancel contract.");
            controller.CancelActivePhase7Preview();
        }

        private static IEnumerator ReviewEditingFeedbackInProductionScene(
            CafeLayoutRuntime runtime, DecorationModeController controller)
        {
            var status = Object.FindFirstObjectByType<ValidationMessageView>();
            var report = status.CurrentMessage;
            var version = runtime.ReadinessVersion;
            var point = runtime.FunctionalSurfaceLayout.PickUpPoints.Single();
            Assert.That(controller.TryBeginExistingFunctionalSurfacePreview(
                FunctionalSurfacePreviewKind.PickUpPoint, point.InstanceId), Is.True);
            var action = Object.FindFirstObjectByType<DecorationActionBarView>();
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            // The same owned feedback is now hosted beside the HUD, not beneath the floating actions.
            // 使用原组件绑定；顶部必要提示不再是 action bar 的子节点。
            var feedback = (TMP_Text)typeof(DecorationActionBarView).GetField("feedbackLabel",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(action);
            var feedbackRoot = (RectTransform)typeof(DecorationActionBarView).GetField("feedbackRoot",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(action);
            Assert.That(feedback, Is.Not.Null);
            Assert.That(feedbackRoot, Is.Not.Null);
            Assert.That(feedback.text, Does.Contain("Current Preview: Pickup Point").And.Contain("Ready to place"));
            var p8r = UsesP8R(catalogue);
            Canvas.ForceUpdateCanvases();
            var editingCorners = new Vector3[4];
            var statusCorners = new Vector3[4];
            ((RectTransform)feedback.transform.parent).GetWorldCorners(editingCorners);
            ((RectTransform)status.transform).GetWorldCorners(statusCorners);
            if (p8r)
            {
                var hud = (Component)typeof(DecorationModeController).GetField("timeControlPanel",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(controller);
                Assert.That(feedbackRoot.parent, Is.SameAs(hud.transform.parent),
                    "The existing notice must use the HUD's safe-area host.");
                Assert.That(feedbackRoot.IsChildOf(action.transform), Is.False);
                Assert.That(feedbackRoot.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
                Assert.That(feedback.gameObject.activeInHierarchy, Is.False,
                    "Moving necessary instructions to the top must not restore general preview explanations.");
            }
            else
            {
                Assert.That(editingCorners[1].y, Is.LessThan(statusCorners[0].y),
                    "Legacy editing feedback must sit below the confirmed-layout status.");
                Assert.That(feedback.fontSize, Is.GreaterThanOrEqualTo(22));
            }
            yield return CaptureReviewFrame("ux-20260909-valid-preview.png");
            controller.TryMoveFunctionalSurfacePreview(default);
            Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.False);
            yield return new WaitForSecondsRealtime(2.3f);
            Assert.That(feedback.gameObject.activeInHierarchy, Is.EqualTo(!p8r));
            Assert.That(feedback.GetComponentInParent<CanvasGroup>(true).alpha, Is.EqualTo(p8r ? 0f : 1f));
            Assert.That(feedback.text, Does.Contain("Move this item onto a counter surface slot."));
            Assert.That(status.CurrentMessage, Is.EqualTo(report));
            Assert.That(runtime.ReadinessVersion, Is.EqualTo(version));
            yield return CaptureReviewFrame("ux-20260909-invalid-preview.png");
            catalogue.ShowCatalogue();
            Assert.That(controller.TryBeginFunctionalSurfacePreview(
                DecorationCatalogueItemKind.PickUpPoint, null, default), Is.False,
                "Return-to-editing remains available after a blocked new-item selection.");
            yield return new WaitForSecondsRealtime(.25f);
            var button = catalogue.GetComponentsInChildren<Button>(true)
                .Single(item => item.name == "ReturnToEditing");
            var message = button.transform.parent.Find("Message").GetComponent<TMP_Text>();
            Assert.That(message.text, Does.Contain("Confirm or cancel this preview").And.Contain("Move this item onto a counter surface slot."));
            Assert.That(message.gameObject.activeInHierarchy, Is.EqualTo(!p8r));
            Assert.That(button.gameObject.activeInHierarchy, Is.True, "Return remains available without the explanatory card.");
            Canvas.ForceUpdateCanvases();
            if (!p8r) Assert.That(message.preferredHeight, Is.LessThanOrEqualTo(message.rectTransform.rect.height + 1f),
                "The legacy catalogue must fit the underlying failure reason.");
            yield return CaptureReviewFrame("ux-20260909-expanded-catalogue.png");
            button.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(catalogue.State, Is.EqualTo(DecorationCatalogueState.Collapsed));
            Assert.That(action.IsVisible, Is.True);
            Assert.That(controller.ActiveFunctionalSurfacePreview.InstanceId, Is.EqualTo(point.InstanceId));
            Assert.That(feedback.text, Does.Contain("Move this item onto a counter surface slot."));
            Assert.That(status.CurrentMessage, Is.EqualTo(report));
            Assert.That(runtime.ReadinessVersion, Is.EqualTo(version));
            controller.CancelFunctionalSurfacePreview();
            catalogue.ShowCatalogue();
            Assert.That(button.gameObject.activeInHierarchy, Is.False);
            Assert.That(status.CurrentMessage, Is.EqualTo(report));
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
            view.GetComponentsInChildren<Button>(true).Single(button => button.name == "ReadinessDetails")
                .onClick.Invoke();
            Assert.That(view.IsDetailsExpanded, Is.True);
            Assert.That(view.CurrentMessage, Does.Contain("员工").And.Contain("阻挡")
                .And.Not.Contain("Employee").And.Not.Contain("slot.0"));
            foreach (var id in view.DiagnosticIds)
                Assert.That(view.CurrentMessage, Does.Not.Contain(id));
            yield return CaptureReviewFrame(folder + "readable-feedback.png");
        }

        private static IEnumerator CaptureReviewDetailFrame(string filename, Vector3 focus)
        {
            if (System.Environment.GetEnvironmentVariable("ANIMALCAFE_P8_REVIEW_CAPTURE") != "1") yield break;
            var camera = Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsSortMode.None)
                .Single(item => item.CompareTag("MainCamera"));
            var originalPosition = camera.transform.position;
            var originalSize = camera.orthographicSize;
            try
            {
                // Temporary close-up evidence only; never save or change gameplay camera rules.
                // 仅截图时平移/放大相机，结束后恢复，不写入 Scene。
                camera.transform.position += Vector3.ProjectOnPlane(
                    focus - camera.transform.up * .35f - originalPosition, camera.transform.forward);
                camera.orthographicSize = 1.8f;
                yield return CaptureReviewFrame(filename);
            }
            finally
            {
                camera.transform.position = originalPosition;
                camera.orthographicSize = originalSize;
                RefreshReviewActionPresentation();
            }
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
                var path = System.IO.Path.Combine("outputs", "preview-natural-20260909", filename);
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
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

        private static IEnumerator CaptureHealthyReadyNativeOverlay(ValidationMessageView feedback)
        {
            if (System.Environment.GetEnvironmentVariable(
                    "ANIMALCAFE_P8_SPACING_HEALTHY_NATIVE_CAPTURE") != "1") yield break;
            Assert.That(Application.isBatchMode, Is.False,
                "Native Overlay evidence requires a normal Editor GameView.");
            var tag = System.Environment.GetEnvironmentVariable(
                "ANIMALCAFE_P8_SPACING_HEALTHY_NATIVE_RUN");
            Assert.That(tag, Does.Match("^[a-z0-9-]+$"));
            var previousProfile = AnimalCafe.UI.P8R.P8RMobileMetrics.EditorLogicalViewportOverride;
            try
            {
                using (var screen = new P8RReferenceLayoutTests.RealGameViewSize())
                {
                    screen.Resize(new Vector2(1080, 1920));
                    AnimalCafe.UI.P8R.P8RMobileMetrics.EditorLogicalViewportOverride =
                        new Vector2(360, 640);
                    yield return new WaitForSecondsRealtime(.3f);
                    Assert.That(new Vector2(Screen.width, Screen.height),
                        Is.EqualTo(new Vector2(1080, 1920)));
                    Assert.That(new Vector2(UnityEngine.Camera.main.pixelWidth,
                        UnityEngine.Camera.main.pixelHeight), Is.EqualTo(new Vector2(1080, 1920)));
                    Assert.That(feedback.IsVisible, Is.False);
                    var canvas = feedback.GetComponentInParent<Canvas>()?.rootCanvas;
                    Assert.That(canvas, Is.Not.Null);
                    Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                    Assert.That(UnityEngine.Camera.main.targetTexture, Is.Null);
                    var folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                        "outputs", "p8r-spacing-ui-20260913", tag, "1080x1920-360x640"));
                    System.IO.Directory.CreateDirectory(folder);
                    var path = System.IO.Path.Combine(folder, "healthy-ready-overlay.png");
                    Assert.That(System.IO.File.Exists(path), Is.False,
                        "Never overwrite earlier native healthy-state evidence.");
                    Canvas.ForceUpdateCanvases();
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(path, 1);
                    yield return new WaitForEndOfFrame();
                    var deadline = Time.realtimeSinceStartup + 8f;
                    while (!System.IO.File.Exists(path) && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    Assert.That(System.IO.File.Exists(path), Is.True,
                        "Native Overlay screenshot was not written by the normal Editor.");
                }
            }
            finally
            {
                AnimalCafe.UI.P8R.P8RMobileMetrics.EditorLogicalViewportOverride =
                    previousProfile;
            }
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

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode
{
    /// <summary>
    /// Loads the real production MainCafe Scene. These tests do not invoke an
    /// Editor setup command and do not create/delete persistence content.
    /// </summary>
    public sealed class Phase6DecorationMainCafeSceneTests : InputTestFixture
    {
        private const string MainCafePath = "Assets/Scenes/MainCafe.unity";
        private const string InitialInstanceId = "00000000000000000000000000000001";
        private static readonly string Phase6PersistencePath = Path.Combine(
            Application.persistentDataPath,
            "AnimalCafe",
            "Phase6");

        [UnityTearDown]
        public IEnumerator RestoreCleanSceneAndTime()
        {
            Time.timeScale = 1f;
            var active = SceneManager.GetActiveScene();
            var cleanup = SceneManager.CreateScene("Phase6Task8SceneTestCleanup");
            SceneManager.SetActiveScene(cleanup);
            if (active.IsValid() && active.isLoaded && active != cleanup)
            {
                var unload = SceneManager.UnloadSceneAsync(active);
                if (unload != null)
                {
                    while (!unload.isDone) yield return null;
                }
            }
        }

        [UnityTest]
        public IEnumerator MainCafe_LoadsOneInitialFormalCounterBeforeDecorationEntry()
        {
            yield return LoadMainCafe();
            var scene = SceneManager.GetActiveScene();
            var controller = FindAll<DecorationModeController>(scene).Single();
            var runtime = FindAll<CafeLayoutRuntime>(scene).Single();
            var registry = FindAll<FurnitureSceneRegistry>(scene).Single();
            var representationRoot = Find(scene, "FurnitureRepresentationRoot");

            Assert.That(controller.IsOpen, Is.False);
            Assert.That(controller.State, Is.EqualTo(DecorationSessionState.Closed));
            Assert.That(runtime.Layout, Is.Not.Null);
            Assert.That(runtime.Layout.FurnitureInstances, Has.Count.EqualTo(1));
            var initial = runtime.Layout.FurnitureInstances.Single();
            Assert.That(initial.InstanceId, Is.EqualTo(InitialInstanceId));
            Assert.That(initial.DefinitionId, Is.EqualTo("furniture.counter.module.01"));
            Assert.That(initial.Position, Is.EqualTo(new GridPosition(2, 3)));
            Assert.That(initial.Rotation, Is.EqualTo(FurnitureRotation.Degrees0));
            Assert.That(representationRoot.transform.childCount, Is.EqualTo(1));
            Assert.That(registry.TryGet(InitialInstanceId, out var representation), Is.True);
            Assert.That(representation, Is.Not.Null.And.SameAs(
                representationRoot.transform.GetChild(0).gameObject));
            Assert.That(Vector3.Distance(
                representation.transform.position,
                new Vector3(-1.5f, 0f, -0.5f)), Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator MainCafe_ReloadRestoresApprovedInitialLayoutAndLeavesExactPhase6PersistenceNamespaceUnchanged()
        {
            var beforeFiles = PersistenceSnapshot.Capture(Phase6PersistencePath);
            yield return LoadMainCafe();
            var firstRuntime = FindAll<CafeLayoutRuntime>(SceneManager.GetActiveScene()).Single();
            var temporary = FurnitureInstance.CreateNew(
                "furniture.counter.module.01",
                new GridPosition(0, 0),
                FurnitureRotation.Degrees0);
            Assert.That(firstRuntime.Layout.PlaceFurniture(temporary).Succeeded, Is.True);
            Assert.That(firstRuntime.Layout.FurnitureInstances, Has.Count.EqualTo(2));
            var firstHandle = SceneManager.GetActiveScene().handle;

            yield return LoadMainCafe();
            var secondScene = SceneManager.GetActiveScene();
            Assert.That(secondScene.handle, Is.Not.EqualTo(firstHandle));
            Assert.That(Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt)
                    .Any(candidate => candidate.handle == firstHandle),
                Is.False,
                "Reload must unload the first real MainCafe Scene before observing a new handle.");
            var secondRuntime = FindAll<CafeLayoutRuntime>(secondScene).Single();
            Assert.That(secondRuntime.Layout.FurnitureInstances, Has.Count.EqualTo(1));
            Assert.That(secondRuntime.Layout.FurnitureInstances.Single().InstanceId,
                Is.EqualTo(InitialInstanceId));

            var afterFiles = PersistenceSnapshot.Capture(Phase6PersistencePath);
            Assert.That(afterFiles, Is.EqualTo(beforeFiles));
        }

        [UnityTest]
        public IEnumerator MainCafe_LoadsCanonicalUiInfrastructureAndNoTemporarySurfaceEquipment()
        {
            yield return LoadMainCafe();
            var scene = SceneManager.GetActiveScene();

            Assert.That(FindAll(scene, "UI Root"), Has.Length.EqualTo(1));
            Assert.That(FindAll<Canvas>(scene).Select(canvas => canvas.name),
                Is.EquivalentTo(new[] { "HUD Canvas", "Screen Canvas", "Toast Canvas" }));
            Assert.That(FindAll<EventSystem>(scene), Has.Length.EqualTo(1));
            Assert.That(FindAll<InputSystemUIInputModule>(scene), Has.Length.EqualTo(1));
            Assert.That(FindAll<StandaloneInputModule>(scene), Is.Empty);
            Assert.That(FindAll(scene, "TEMP_P4_ManualReviewFixtures_DELETE_LATER"), Is.Empty);
            Assert.That(FindAll(scene, "DecorationModeButton"), Has.Length.EqualTo(1));
            Assert.That(FindAll<DecorationCatalogueView>(scene), Has.Length.EqualTo(1));
            Assert.That(FindAll<DecorationActionBarView>(scene), Has.Length.EqualTo(1));
            Assert.That(FindAll<DecorationStoreModalView>(scene), Has.Length.EqualTo(1));

            var layout = FindAll<CafeLayoutRuntime>(scene).Single().Layout;
            Assert.That(layout.FurnitureInstances.Select(item => item.DefinitionId),
                Is.EqualTo(new[] { "furniture.counter.module.01" }));
            Assert.That(layout.FurnitureInstances.Select(item => item.DefinitionId),
                Does.Not.Contain("furniture.work-table.01")
                    .And.Not.Contain("equipment.cash-register.01")
                    .And.Not.Contain("equipment.coffee-machine.01"));

            var controller = FindAll<DecorationModeController>(scene).Single();
            var content = ReadPrivate<AnimalCafe.Content.FurnitureContentCatalog>(
                controller,
                "contentCatalog");
            foreach (var definitionId in new[]
                     {
                         "furniture.work-table.01",
                         "equipment.cash-register.01",
                         "equipment.coffee-machine.01"
                     })
            {
                Assert.That(content.TryGetPrefab(definitionId, out var prefab), Is.True,
                    definitionId);
                var authoredClones = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Where(transform => transform.name == prefab.name
                        || transform.name == prefab.name + "(Clone)")
                    .Select(transform => transform.name)
                    .ToArray();
                Assert.That(authoredClones, Is.Empty,
                    definitionId + " must not exist as an orphan Scene clone outside Layout.");
            }
        }

        [UnityTest]
        public IEnumerator MainCafe_HudReferencesAreAssignedBeforeDirectControllerEntry()
        {
            yield return LoadMainCafe();
            var scene = SceneManager.GetActiveScene();
            var controller = FindAll<DecorationModeController>(scene).Single();
            var button = ReadPrivate<Button>(controller, "decorationModeButton");
            var label = ReadPrivate<TMP_Text>(controller, "decorationModeButtonLabel");
            var catalogue = FindAll<DecorationCatalogueView>(scene).Single();
            var actionBar = FindAll<DecorationActionBarView>(scene).Single();
            var storeModal = FindAll<DecorationStoreModalView>(scene).Single();
            var catalogueGroup = catalogue.GetComponent<CanvasGroup>();
            var actionGroup = actionBar.GetComponent<CanvasGroup>();
            var modalGroup = storeModal.GetComponent<CanvasGroup>();

            Assert.That(button, Is.Not.Null);
            Assert.That(label, Is.Not.Null);
            Assert.That(label.text, Is.EqualTo("Decoration"));
            AssertClosedActiveUiRoot(catalogue.gameObject, catalogueGroup);
            AssertClosedActiveUiRoot(actionBar.gameObject, actionGroup);
            AssertClosedActiveUiRoot(storeModal.gameObject, modalGroup);

            controller.EnterDecorationMode();
            yield return WaitForUnscaledSeconds(0.2f);
            Assert.That(controller.IsOpen, Is.True);
            Assert.That(label.text, Is.EqualTo("Done"));
            Assert.That(catalogue.IsCatalogueVisible, Is.True);
            Assert.That(catalogueGroup.alpha, Is.EqualTo(1f).Within(0.001f));
            Assert.That(catalogueGroup.interactable, Is.True);
            Assert.That(catalogueGroup.blocksRaycasts, Is.True);
            Assert.That(actionBar.IsVisible, Is.False);
            Assert.That(actionGroup.alpha, Is.EqualTo(0f).Within(0.001f));

            actionBar.Show(false, true, PlacementFeedbackKey.None);
            yield return WaitForUnscaledSeconds(0.16f);
            Assert.That(actionBar.IsVisible, Is.True);
            Assert.That(actionGroup.alpha, Is.EqualTo(1f).Within(0.001f));
            Assert.That(actionGroup.interactable, Is.True);
            Assert.That(actionGroup.blocksRaycasts, Is.True);

            controller.ExitDecorationMode();
            yield return WaitForUnscaledSeconds(0.2f);
            Assert.That(controller.IsOpen, Is.False);
            Assert.That(label.text, Is.EqualTo("Decoration"));
            Assert.That(catalogue.gameObject.activeSelf, Is.True);
            Assert.That(actionBar.gameObject.activeSelf, Is.True);
            Assert.That(storeModal.gameObject.activeSelf, Is.True);
            Assert.That(catalogueGroup.alpha, Is.EqualTo(0f).Within(0.001f));
            Assert.That(actionGroup.alpha, Is.EqualTo(0f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator MainCafe_LandscapeCatalogueLeavesActualRightRailVisible()
        {
            yield return LoadMainCafe();
            var scene = SceneManager.GetActiveScene();
            var camera = FindAll<UnityEngine.Camera>(scene)
                .Single(item => item.CompareTag("MainCamera"));
            var canvases = FindAll<Canvas>(scene)
                .Where(item => item.isRootCanvas && item.transform.root.name == "UI Root")
                .ToArray();
            var previousTarget = camera.targetTexture;
            var canvasStates = canvases.Select(item => new
            {
                Canvas = item,
                item.renderMode,
                item.worldCamera,
                item.planeDistance
            }).ToArray();
            var target = new RenderTexture(2400, 1080, 24);
            DecorationModeController controller = null;
            target.Create();
            try
            {
                camera.targetTexture = target;
                foreach (var item in canvases)
                {
                    item.renderMode = RenderMode.ScreenSpaceCamera;
                    item.worldCamera = camera;
                    item.planeDistance = Mathf.Max(camera.nearClipPlane + 0.1f, 0.5f);
                }
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                Assert.That(canvases.Select(item => item.rootCanvas.renderingDisplaySize.x),
                    Is.All.EqualTo(2400f).Within(0.5f));
                Assert.That(canvases.Select(item => item.rootCanvas.renderingDisplaySize.y),
                    Is.All.EqualTo(1080f).Within(0.5f));

                var timePanel = FindAll<AnimalCafe.UI.TimeControlPanel>(scene).Single();
                var safeAreas = FindAll<AnimalCafe.UI.Components.SafeAreaContainer>(scene);
                Assert.That(safeAreas, Is.Not.Empty);
                foreach (var safeArea in safeAreas)
                {
                    safeArea.AutoApplyRuntimeSafeArea = false;
                    safeArea.ApplySafeArea(
                        new Rect(96f, 48f, 2208f, 984f),
                        new Vector2(2400f, 1080f));
                }

                var catalogue = FindAll<DecorationCatalogueView>(scene).Single();
                controller = FindAll<DecorationModeController>(scene).Single();
                controller.EnterDecorationMode();
                catalogue.ShowCatalogue();
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();

                Assert.That(camera.pixelWidth, Is.EqualTo(2400));
                Assert.That(camera.pixelHeight, Is.EqualTo(1080));
                var catalogueCanvas = catalogue.GetComponentInParent<Canvas>()?.rootCanvas;
                Assert.That(catalogueCanvas, Is.Not.Null);
                Assert.That(catalogueCanvas.renderingDisplaySize.x,
                    Is.EqualTo(2400f).Within(0.5f));
                Assert.That(catalogueCanvas.renderingDisplaySize.y,
                    Is.EqualTo(1080f).Within(0.5f));
                var expandedTransform =
                    catalogue.transform.Find("ExpandedSheet") as RectTransform;
                var expanded = ScreenRect(expandedTransform);
                var rail = ScreenRect(timePanel.transform as RectTransform);
                Assert.That(expanded.Overlaps(rail), Is.False,
                    $"Landscape Catalogue {expanded} overlaps the actual MainCafe RightRail {rail}.");
                Assert.That(expanded.xMax, Is.LessThanOrEqualTo(rail.xMin));
            }
            finally
            {
                controller?.ExitDecorationMode();
                camera.targetTexture = previousTarget;
                foreach (var state in canvasStates)
                {
                    state.Canvas.renderMode = state.renderMode;
                    state.Canvas.worldCamera = state.worldCamera;
                    state.Canvas.planeDistance = state.planeDistance;
                }
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        [UnityTest]
        public IEnumerator MainCafe_RealMouseDragsExistingFurnitureAndWheelZoomsWithoutFakeInput()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            DecorationModeController controller = null;
            try
            {
                yield return LoadMainCafe();
                var scene = SceneManager.GetActiveScene();
                controller = FindAll<DecorationModeController>(scene).Single();
                var source = FindAll<MouseDecorationInputSource>(scene).Single();
                var camera = FindAll<UnityEngine.Camera>(scene).Single(item => item.CompareTag("MainCamera"));
                var registry = FindAll<FurnitureSceneRegistry>(scene).Single();
                var catalogue = FindAll<DecorationCatalogueView>(scene).Single();

                Assert.That(ReadPrivate<MonoBehaviour>(controller, "mouseSourceBehaviour"),
                    Is.SameAs(source));
                Assert.That(registry.TryGet(InitialInstanceId, out var formal), Is.True);
                controller.EnterDecorationMode();
                catalogue.ShowCollapsedHandle();
                yield return WaitForUnscaledSeconds(0.2f);

                var collider = formal.GetComponentInChildren<Collider>(true);
                Assert.That(collider, Is.Not.Null);
                var start = FindUiFreeScreenPointOnFormal(camera, formal, collider.bounds);
                var end = start + new Vector2(100f, 70f);
                QueueMouseState(mouse, start, false);
                yield return null;
                QueueMouseState(mouse, start, true);
                yield return null;
                yield return null;
                Assert.That(source.HasActivePointer, Is.True,
                    $"Mouse source did not own the pressed pointer at {start}; deviceTime={mouse.lastUpdateTime}.");
                Assert.That(controller.State,
                    Is.EqualTo(DecorationSessionState.EditingExistingFurniture));

                var session = ReadPrivate<DecorationSession>(controller, "session");
                var initialCell = session.ActivePreview.ProposedPosition;
                QueueMouseState(mouse, end, true);
                yield return null;
                yield return null;
                Assert.That(session.ActivePreview.ProposedPosition, Is.Not.EqualTo(initialCell),
                    "A real Mouse drag must move the active preview to another grid cell.");
                QueueMouseState(mouse, end, false);
                yield return null;
                yield return null;
                Assert.That(source.HasActivePointer, Is.False);

                controller.CancelActivePreview();
                var beforeZoom = camera.orthographicSize;
                QueueMouseState(mouse,
                    new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                    false,
                    new Vector2(0f, 120f));
                yield return null;
                yield return null;
                Assert.That(camera.orthographicSize, Is.Not.EqualTo(beforeZoom),
                    "A real Mouse wheel event must reach the decoration camera driver.");
            }
            finally
            {
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                controller?.ExitDecorationMode();
            }
        }

        [UnityTest]
        public IEnumerator MainCafe_RealMouseCatalogueCoversEveryFootprintAndShowsVisiblePreview()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            DecorationModeController controller = null;
            try
            {
                yield return LoadMainCafe();
                var scene = SceneManager.GetActiveScene();
                controller = FindAll<DecorationModeController>(scene).Single();
                var catalogue = FindAll<DecorationCatalogueView>(scene).Single();
                var actionBar = FindAll<DecorationActionBarView>(scene).Single();
                var source = FindAll<MouseDecorationInputSource>(scene).Single();
                var camera = FindAll<UnityEngine.Camera>(scene)
                    .Single(item => item.CompareTag("MainCamera"));
                var floor = ReadPrivate<Collider>(controller, "floorCollider");
                var previewView = FindAll<FurniturePreviewView>(scene).Single();
                var previewRoot = Find(scene, "FurniturePreviewRoot").transform;
                var gridVisualRoot = Find(scene, "GridVisualRoot").transform;
                var expected = new[]
                {
                    (Id: "furniture.counter.module.01", Width: 1, Depth: 1),
                    (Id: "counter.preset.1x2", Width: 1, Depth: 2),
                    (Id: "counter.preset.1x3", Width: 1, Depth: 3),
                    (Id: "counter.preset.2x3", Width: 2, Depth: 3)
                };

                controller.EnterDecorationMode();
                yield return WaitUntil(() => catalogue.IsCatalogueVisible
                        && !catalogue.IsCollapsed,
                    2f, "Catalogue did not become expanded in real MainCafe.");

                foreach (var item in expected)
                {
                    var tile = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                        .Single(candidate => candidate.gameObject.activeInHierarchy
                            && candidate.Definition != null
                            && candidate.Definition.DefinitionId == item.Id);
                    Assert.That(tile.Definition.FootprintWidth, Is.EqualTo(item.Width), item.Id);
                    Assert.That(tile.Definition.FootprintDepth, Is.EqualTo(item.Depth), item.Id);

                    yield return ClickButtonWithRealMouse(mouse, tile.GetComponent<Button>());
                    yield return WaitUntil(() =>
                            controller.State == DecorationSessionState.PreviewingNewFurniture
                            && previewRoot.childCount == 1,
                        2f, item.Id + " did not create a visible Preview.");

                    Assert.That(previewRoot.childCount, Is.EqualTo(1), item.Id);
                    Assert.That(previewRoot.GetChild(0).gameObject.activeInHierarchy, Is.True, item.Id);
                    Assert.That(previewRoot.GetChild(0).localPosition.y, Is.GreaterThan(0f),
                        item.Id + " Preview must visibly hover above the floor.");
                    Assert.That(previewView.TryGetWorldBounds(out var bounds), Is.True, item.Id);
                    Assert.That(bounds.size.x, Is.GreaterThan(0.01f), item.Id);
                    Assert.That(bounds.size.y, Is.GreaterThan(0.01f), item.Id);
                    Assert.That(bounds.size.z, Is.GreaterThan(0.01f), item.Id);
                    var previewBefore = previewRoot.GetChild(0).position;
                    var boundsBefore = bounds.center;
                    var dragStart = FindUiFreePreviewScreenPoint(camera, floor, bounds);
                    var dragEnd = FindUiFreeFloorDragDestination(
                        camera, floor, bounds);
                    QueueMouseState(mouse, dragStart, false);
                    yield return null;
                    QueueMouseState(mouse, dragStart, true);
                    yield return null;
                    yield return null;
                    Assert.That(source.HasActivePointer, Is.True, item.Id + " drag Began");
                    QueueMouseState(mouse, dragEnd, true);
                    yield return null;
                    yield return null;
                    Assert.That(previewView.TryGetWorldBounds(out var movedBounds), Is.True,
                        item.Id + " moved Preview bounds");
                    Assert.That(Vector3.Distance(movedBounds.center, boundsBefore),
                        Is.GreaterThanOrEqualTo(0.9f),
                        item.Id + " real Mouse drag must cross at least one grid cell.");
                    Assert.That(Vector3.Distance(
                            previewRoot.GetChild(0).position,
                            previewBefore),
                        Is.GreaterThanOrEqualTo(0.9f),
                        item.Id + " Scene-visible Preview child must move with the drag.");
                    QueueMouseState(mouse, dragEnd, false);
                    yield return null;
                    yield return null;
                    Assert.That(source.HasActivePointer, Is.False, item.Id + " terminal cleanup");
                    Assert.That(controller.State,
                        Is.EqualTo(DecorationSessionState.PreviewingNewFurniture), item.Id);
                    Assert.That(gridVisualRoot.Cast<Transform>().Count(child =>
                            child.gameObject.activeInHierarchy
                            && child.name.StartsWith("FootprintCell", StringComparison.Ordinal)),
                        Is.EqualTo(item.Width * item.Depth), item.Id + " footprint highlight");

                    var cancel = actionBar.transform.Find("ActionPanel/CancelButton")
                        .GetComponent<Button>();
                    yield return ClickButtonWithRealMouse(mouse, cancel);
                    yield return WaitUntil(() =>
                            controller.State == DecorationSessionState.BrowsingCatalogue
                            && previewRoot.childCount == 0
                            && catalogue.IsCatalogueVisible && !catalogue.IsCollapsed,
                        2f, item.Id + " Cancel did not restore the expanded Catalogue.");
                }
            }
            finally
            {
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                controller?.ExitDecorationMode();
            }
        }

        [UnityTest]
        public IEnumerator MainCafe_RealMouseBlankPanAndUiClickNeverPassesThrough()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            DecorationModeController controller = null;
            try
            {
                yield return LoadMainCafe();
                var scene = SceneManager.GetActiveScene();
                controller = FindAll<DecorationModeController>(scene).Single();
                var source = FindAll<MouseDecorationInputSource>(scene).Single();
                var camera = FindAll<UnityEngine.Camera>(scene)
                    .Single(item => item.CompareTag("MainCamera"));
                var catalogue = FindAll<DecorationCatalogueView>(scene).Single();
                var floor = ReadPrivate<Collider>(controller, "floorCollider");
                var previewRoot = Find(scene, "FurniturePreviewRoot").transform;

                controller.EnterDecorationMode();
                yield return WaitUntil(() => catalogue.IsCatalogueVisible
                        && !catalogue.IsCollapsed,
                    2f, "Catalogue did not become expanded.");
                var collapse = catalogue.transform.Find("ExpandedSheet/CollapseButton")
                    .GetComponent<Button>();
                yield return ClickButtonWithRealMouse(mouse, collapse);
                yield return WaitUntil(() => catalogue.IsCollapsed, 2f,
                    "Real Mouse did not collapse the Catalogue.");

                var blankPoints = FindUiFreeFloorScreenPoints(camera, floor, 2, 96f);
                var cameraBeforePan = camera.transform.position;
                QueueMouseState(mouse, blankPoints[0], false);
                yield return null;
                QueueMouseState(mouse, blankPoints[0], true);
                yield return null;
                yield return null;
                Assert.That(source.HasActivePointer, Is.True);
                QueueMouseState(mouse, blankPoints[1], true);
                yield return null;
                yield return null;
                QueueMouseState(mouse, blankPoints[1], false);
                yield return null;
                yield return null;
                Assert.That(Vector3.Distance(camera.transform.position, cameraBeforePan),
                    Is.GreaterThan(0.001f), "A blank-floor Mouse drag must pan the camera.");
                Assert.That(controller.State, Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
                Assert.That(previewRoot.childCount, Is.Zero);

                var expand = catalogue.transform.Find("CollapsedHandle")
                    .GetComponent<Button>();
                var cameraBeforeUi = camera.transform.position;
                yield return ClickButtonWithRealMouse(mouse, expand);
                yield return WaitUntil(() => !catalogue.IsCollapsed, 2f,
                    "Real Mouse did not expand the Catalogue.");
                Assert.That(Vector3.Distance(camera.transform.position, cameraBeforeUi),
                    Is.LessThan(0.001f), "UI release must not pan the Scene.");
                Assert.That(controller.State, Is.EqualTo(DecorationSessionState.BrowsingCatalogue),
                    "UI release must not select or restart Scene furniture.");
                Assert.That(previewRoot.childCount, Is.Zero);

                Button floorBackedTile = null;
                var floorBackedPoint = Vector2.zero;
                foreach (var button in catalogue
                             .GetComponentsInChildren<DecorationCatalogueTileView>(true)
                             .Where(tile => tile.gameObject.activeInHierarchy)
                             .Select(tile => tile.GetComponent<Button>()))
                {
                    if (!TryFindFloorBackedButtonPoint(
                            camera, floor, button, out floorBackedPoint))
                    {
                        continue;
                    }
                    floorBackedTile = button;
                    break;
                }
                Assert.That(floorBackedTile, Is.Not.Null,
                    "UI no-pass evidence requires a real Catalogue tile above the floor.");
                cameraBeforeUi = camera.transform.position;
                yield return ClickButtonWithRealMouse(
                    mouse, floorBackedTile, floorBackedPoint);
                yield return WaitUntil(() =>
                        controller.State == DecorationSessionState.PreviewingNewFurniture
                        && previewRoot.childCount == 1,
                    2f,
                    "The floor-backed Catalogue tile did not perform its UI action.");
                Assert.That(Vector3.Distance(camera.transform.position, cameraBeforeUi),
                    Is.LessThan(0.001f),
                    "A floor-backed UI press/release must not pan the lower Scene layer.");
                Assert.That(source.HasActivePointer, Is.False);
            }
            finally
            {
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                controller?.ExitDecorationMode();
            }
        }

        [UnityTest]
        public IEnumerator MainCafe_RealTouchAndMouseHandoffKeepsOneFamilyAndRecoversFreshGestures()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            var touchscreen = InputSystem.AddDevice<Touchscreen>();
            mouse.MakeCurrent();
            DecorationModeController controller = null;
            try
            {
                yield return LoadMainCafe();
                var scene = SceneManager.GetActiveScene();
                controller = FindAll<DecorationModeController>(scene).Single();
                var camera = FindAll<UnityEngine.Camera>(scene)
                    .Single(item => item.CompareTag("MainCamera"));
                var catalogue = FindAll<DecorationCatalogueView>(scene).Single();
                var floor = ReadPrivate<Collider>(controller, "floorCollider");

                controller.EnterDecorationMode();
                var router = ReadPrivate<DecorationTouchRouter>(controller, "touchRouter");
                catalogue.ShowCollapsedHandle();
                yield return WaitUntil(() => catalogue.IsCollapsed, 2f,
                    "Catalogue did not expose the floor for handoff evidence.");
                var points = FindUiFreeFloorScreenPoints(camera, floor, 3, 72f);

                QueueMouseState(mouse, points[0], false);
                yield return null;
                QueueMouseState(mouse, points[0], true);
                yield return null;
                yield return null;
                Assert.That(ActivePointerFamily(controller), Is.EqualTo("Mouse"));
                Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Camera));

                QueueTouchState(touchscreen, 31,
                    UnityEngine.InputSystem.TouchPhase.Began, points[1]);
                yield return null;
                yield return null;
                Assert.That(ActivePointerFamily(controller), Is.EqualTo("Mouse"),
                    "Touch Began cannot steal an active Mouse gesture.");
                QueueMouseState(mouse, points[0], false);
                yield return null;
                yield return null;
                Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.None));
                QueueTouchState(touchscreen, 31,
                    UnityEngine.InputSystem.TouchPhase.Ended, points[1]);
                yield return null;
                yield return null;

                var beforeTouchPan = camera.transform.position;
                QueueTouchState(touchscreen, 32,
                    UnityEngine.InputSystem.TouchPhase.Began, points[1]);
                yield return null;
                yield return null;
                Assert.That(ActivePointerFamily(controller), Is.EqualTo("Touch"));
                Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Camera));
                QueueMouseState(mouse, points[2], true);
                QueueTouchState(touchscreen, 32,
                    UnityEngine.InputSystem.TouchPhase.Moved, points[2], points[2] - points[1]);
                yield return null;
                yield return null;
                Assert.That(ActivePointerFamily(controller), Is.EqualTo("Touch"),
                    "Mouse Began cannot steal an active Touch gesture.");
                Assert.That(Vector3.Distance(camera.transform.position, beforeTouchPan),
                    Is.GreaterThan(0.001f));
                QueueMouseState(mouse, points[2], false);
                QueueTouchState(touchscreen, 32,
                    UnityEngine.InputSystem.TouchPhase.Ended, points[2]);
                yield return null;
                yield return null;
                Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.None));

                var beforeFreshMousePan = camera.transform.position;
                QueueMouseState(mouse, points[2], false);
                yield return null;
                QueueMouseState(mouse, points[2], true);
                yield return null;
                yield return null;
                Assert.That(ActivePointerFamily(controller), Is.EqualTo("Mouse"));
                Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Camera));
                QueueMouseState(mouse, points[0], true);
                yield return null;
                yield return null;
                QueueMouseState(mouse, points[0], false);
                yield return null;
                yield return null;
                Assert.That(Vector3.Distance(camera.transform.position, beforeFreshMousePan),
                    Is.GreaterThan(0.001f), "Fresh Mouse must recover after Touch terminal.");
                Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.None));
            }
            finally
            {
                if (touchscreen.added) InputSystem.RemoveDevice(touchscreen);
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                controller?.ExitDecorationMode();
            }
        }

        private static void AssertClosedActiveUiRoot(GameObject root, CanvasGroup group)
        {
            Assert.That(root.activeSelf, Is.True, root.name);
            Assert.That(root.activeInHierarchy, Is.True, root.name);
            Assert.That(group, Is.Not.Null, root.name);
            Assert.That(group.alpha, Is.EqualTo(0f).Within(0.001f), root.name);
            Assert.That(group.interactable, Is.False, root.name);
            Assert.That(group.blocksRaycasts, Is.False, root.name);
        }

        private static IEnumerator WaitForUnscaledSeconds(float duration)
        {
            var deadline = Time.realtimeSinceStartup + duration;
            while (Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeout, string message)
        {
            var deadline = Time.realtimeSinceStartup + timeout;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(condition(), Is.True, message);
        }

        private static IEnumerator ClickButtonWithRealMouse(
            Mouse mouse,
            Button button,
            Vector2? explicitPoint = null)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.gameObject.activeInHierarchy, Is.True, button.name);
            Assert.That(button.interactable, Is.True, button.name);
            var point = explicitPoint ?? ButtonCenter(button);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(
                new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.That(hits, Is.Not.Empty, button.name + " has no real UI raycast owner.");
            Assert.That(hits[0].gameObject == button.gameObject
                    || hits[0].gameObject.transform.IsChildOf(button.transform),
                Is.True, button.name + " is not the top real UI owner.");

            QueueMouseState(mouse, point, false);
            yield return null;
            QueueMouseState(mouse, point, true);
            yield return null;
            yield return null;
            QueueMouseState(mouse, point, false);
            yield return null;
            yield return null;
        }

        private static bool TryFindFloorBackedButtonPoint(
            UnityEngine.Camera camera,
            Collider floor,
            Button button,
            out Vector2 point)
        {
            var rect = button.transform as RectTransform;
            var canvas = button.GetComponentInParent<Canvas>();
            var eventCamera = canvas != null
                && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera
                : null;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var minimum = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
            var maximum = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
            for (var y = 1; y <= 4; y++)
            for (var x = 1; x <= 4; x++)
            {
                var candidate = new Vector2(
                    Mathf.Lerp(minimum.x, maximum.x, x / 5f),
                    Mathf.Lerp(minimum.y, maximum.y, y / 5f));
                if (!Physics.RaycastAll(camera.ScreenPointToRay(candidate))
                        .Any(hit => hit.collider == floor))
                {
                    continue;
                }

                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(
                    new PointerEventData(EventSystem.current) { position = candidate }, hits);
                if (hits.Count == 0
                    || (hits[0].gameObject != button.gameObject
                        && !hits[0].gameObject.transform.IsChildOf(button.transform)))
                {
                    continue;
                }

                point = candidate;
                return true;
            }

            point = default;
            return false;
        }

        private static Vector2 ButtonCenter(Button button)
        {
            var rect = button.transform as RectTransform;
            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.rootCanvas.worldCamera
                : null;
            return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            Assert.That(rect, Is.Not.Null);
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            var eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var minimum = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
            var maximum = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        private static Vector2[] FindUiFreeFloorScreenPoints(
            UnityEngine.Camera camera,
            Collider floor,
            int count,
            float minimumSeparation)
        {
            var points = new List<Vector2>();
            for (var y = 1; y <= 8 && points.Count < count; y++)
            for (var x = 1; x <= 8 && points.Count < count; x++)
            {
                var point = new Vector2(Screen.width * x / 9f, Screen.height * y / 9f);
                var uiHits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(
                    new PointerEventData(EventSystem.current) { position = point }, uiHits);
                if (uiHits.Count != 0
                    || !Physics.RaycastAll(camera.ScreenPointToRay(point))
                        .Any(hit => hit.collider == floor)
                    || points.Any(existing => Vector2.Distance(existing, point) < minimumSeparation))
                {
                    continue;
                }
                points.Add(point);
            }

            Assert.That(points, Has.Count.EqualTo(count),
                "Could not find enough UI-free configured-floor points in real MainCafe.");
            return points.ToArray();
        }

        private static Vector2 FindUiFreePreviewScreenPoint(
            UnityEngine.Camera camera,
            Collider floor,
            Bounds previewBounds)
        {
            var y = floor.bounds.max.y;
            var candidates = new[]
            {
                new Vector3(previewBounds.center.x, y, previewBounds.center.z),
                new Vector3(previewBounds.min.x + 0.1f, y, previewBounds.min.z + 0.1f),
                new Vector3(previewBounds.max.x - 0.1f, y, previewBounds.min.z + 0.1f),
                new Vector3(previewBounds.min.x + 0.1f, y, previewBounds.max.z - 0.1f),
                new Vector3(previewBounds.max.x - 0.1f, y, previewBounds.max.z - 0.1f)
            };
            foreach (var world in candidates)
            {
                var point = (Vector2)camera.WorldToScreenPoint(world);
                var uiHits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(
                    new PointerEventData(EventSystem.current) { position = point }, uiHits);
                if (uiHits.Count == 0
                    && Physics.RaycastAll(camera.ScreenPointToRay(point))
                        .Any(hit => hit.collider == floor))
                {
                    return point;
                }
            }

            Assert.Fail("No UI-free floor projection was found inside the visible Preview bounds.");
            return default;
        }

        private static Vector2 FindUiFreeFloorDragDestination(
            UnityEngine.Camera camera,
            Collider floor,
            Bounds previewBounds)
        {
            var floorY = floor.bounds.max.y;
            var origin = new Vector3(
                previewBounds.center.x,
                floorY,
                previewBounds.center.z);
            var offsets = new[]
            {
                new Vector3(2f, 0f, 0f), new Vector3(-2f, 0f, 0f),
                new Vector3(0f, 0f, 2f), new Vector3(0f, 0f, -2f),
                new Vector3(3f, 0f, 0f), new Vector3(-3f, 0f, 0f),
                new Vector3(0f, 0f, 3f), new Vector3(0f, 0f, -3f)
            };
            foreach (var offset in offsets)
            {
                var point = (Vector2)camera.WorldToScreenPoint(origin + offset);
                var uiHits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(
                    new PointerEventData(EventSystem.current) { position = point }, uiHits);
                if (uiHits.Count == 0
                    && Physics.RaycastAll(camera.ScreenPointToRay(point))
                        .Any(hit => hit.collider == floor))
                {
                    return point;
                }
            }

            Assert.Fail(
                "Could not find a UI-free floor destination for a real Mouse preview drag.");
            return default;
        }

        private static string ActivePointerFamily(DecorationModeController controller)
        {
            var field = typeof(DecorationModeController).GetField(
                "activePointerDeviceFamily", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(controller).ToString();
        }

        private static IEnumerator LoadMainCafe()
        {
            EditorSceneManager.LoadSceneInPlayMode(
                MainCafePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(MainCafePath));
        }

        private static Vector2 FindUiFreeScreenPointOnFormal(
            UnityEngine.Camera camera,
            GameObject formal,
            Bounds bounds)
        {
            var min = bounds.min;
            var max = bounds.max;
            var candidates = new[]
            {
                bounds.center,
                new Vector3(min.x, min.y, min.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z), new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z), new Vector3(max.x, max.y, max.z)
            };
            foreach (var world in candidates)
            {
                var screen = (Vector2)camera.WorldToScreenPoint(world);
                var physicsHit = Physics.RaycastAll(camera.ScreenPointToRay(screen)).Any(hit =>
                    hit.collider.transform.IsChildOf(formal.transform)
                    || hit.collider.gameObject == formal);
                var uiHits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(
                    new PointerEventData(EventSystem.current) { position = screen }, uiHits);
                if (physicsHit && uiHits.Count == 0)
                    return screen;
            }

            Assert.Fail("No UI-free screen point raycasted to the registered formal furniture.");
            return default;
        }

        private static void QueueMouseState(
            Mouse mouse,
            Vector2 position,
            bool leftDown,
            Vector2 scroll = default)
        {
            var state = new MouseState
            {
                position = position,
                scroll = scroll
            };
            if (leftDown) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state, mouse.lastUpdateTime + 0.000001d);
        }

        private static void QueueTouchState(
            Touchscreen touchscreen,
            int touchId,
            UnityEngine.InputSystem.TouchPhase phase,
            Vector2 position,
            Vector2 delta = default)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState
            {
                touchId = touchId,
                phase = phase,
                position = position,
                delta = delta,
                pressure = phase == UnityEngine.InputSystem.TouchPhase.Ended ? 0f : 1f
            }, touchscreen.lastUpdateTime + 0.000001d);
        }

        private static T ReadPrivate<T>(object owner, string fieldName)
            where T : class
        {
            var field = owner.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(owner) as T;
        }

        private static GameObject Find(Scene scene, string name) =>
            FindAll(scene, name).Single();

        private static GameObject[] FindAll(Scene scene, string name) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name == name)
                .Select(transform => transform.gameObject)
                .ToArray();

        private static T[] FindAll<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();

        private readonly struct PersistenceSnapshot : IEquatable<PersistenceSnapshot>
        {
            private readonly string value;
            private PersistenceSnapshot(string value) => this.value = value;

            public static PersistenceSnapshot Capture(string path)
            {
                if (!Directory.Exists(path))
                    return new PersistenceSnapshot("<absent>");

                var directories = Directory.GetDirectories(
                        path,
                        "*",
                        SearchOption.AllDirectories)
                    .Select(directory => "D|" + Path.GetRelativePath(path, directory)
                        .Replace('\\', '/'));
                var files = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                    .Select(file =>
                    {
                        var relative = Path.GetRelativePath(path, file).Replace('\\', '/');
                        using var sha = System.Security.Cryptography.SHA256.Create();
                        return "F|" + relative + "|" + BitConverter.ToString(
                            sha.ComputeHash(File.ReadAllBytes(file)))
                            .Replace("-", string.Empty);
                    });
                return new PersistenceSnapshot(string.Join("\n", directories.Concat(files)
                    .OrderBy(record => record, StringComparer.Ordinal)));
            }

            public bool Equals(PersistenceSnapshot other) => value == other.value;
            public override bool Equals(object obj) =>
                obj is PersistenceSnapshot other && Equals(other);
            public override int GetHashCode() => value?.GetHashCode() ?? 0;
        }
    }
}
#endif

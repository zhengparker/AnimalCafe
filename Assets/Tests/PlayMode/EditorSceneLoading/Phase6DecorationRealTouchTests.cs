#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Interaction;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Tests.PlayMode
{
    /// <summary>
    /// Task 9 acceptance uses the production MainCafe Scene and the real Input System UI/Touch path.
    /// No setup command, Scene save or injected Decoration touch source is used here.
    /// </summary>
    public sealed class Phase6DecorationRealTouchTests : InputTestFixture
    {
        private const string MainCafePath = "Assets/Scenes/MainCafe.unity";
        private const string Phase5Path = "Assets/Scenes/Validation/Phase5UiFoundation.unity";
        private const string InitialInstanceId = "00000000000000000000000000000001";

        private readonly HashSet<int> activeTouchIds = new HashSet<int>();
        private readonly Dictionary<int, Vector2> activeTouchPositions = new Dictionary<int, Vector2>();
        private GameObject touchPumpObject;
        private Task9TouchPump touchPump;
        private PlayerLoopSystem playerLoopBeforePump;
        private bool playerLoopPumpInstalled;
        private UnityEngine.Object selectionBefore;
        private float timeScaleBefore;
        private Vector2Int screenBefore;
        private Rect safeAreaBefore;
        private Scene activeSceneBefore;
        private Scene cleanupScene;
        private string cleanupSceneName;
        private readonly HashSet<SceneHandle> fixtureSceneHandles = new HashSet<SceneHandle>();

        [UnitySetUp]
        public IEnumerator EstablishFailureSafeBoundary()
        {
            activeTouchIds.Clear();
            activeTouchPositions.Clear();
            selectionBefore = Selection.activeObject;
            timeScaleBefore = Time.timeScale;
            screenBefore = new Vector2Int(Screen.width, Screen.height);
            safeAreaBefore = Screen.safeArea;
            activeSceneBefore = SceneManager.GetActiveScene();
            fixtureSceneHandles.Clear();
            cleanupSceneName = "Phase6Task9Cleanup_" + Guid.NewGuid().ToString("N");
            cleanupScene = SceneManager.CreateScene(cleanupSceneName);
            Assert.That(cleanupScene.IsValid() && cleanupScene.isLoaded, Is.True,
                "Task 9 must establish an empty cleanup Scene before any production Scene load.");
            Assert.That(cleanupScene.rootCount, Is.Zero);
            Assert.That(SceneManager.SetActiveScene(cleanupScene), Is.True,
                "The test-owned cleanup Scene must become the failure-safe active boundary.");
            touchPumpObject = new GameObject("Phase6Task9TouchPump");
            touchPump = touchPumpObject.AddComponent<Task9TouchPump>();
            UnityEngine.Object.DontDestroyOnLoad(touchPumpObject);
            InstallTouchPumpPlayerLoop();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator RestoreFailureSafeBoundary()
        {
            var cleanupFailures = new List<string>();
            try
            {
                touchPump?.ClearPending();
                RestoreTouchPumpPlayerLoop();
                if (touchPumpObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(touchPumpObject);
                    if (touchPumpObject != null || touchPump != null)
                        cleanupFailures.Add("Fixture Touch pump was not destroyed immediately.");
                }
            }
            catch (Exception exception)
            {
                cleanupFailures.Add("Touch pump cleanup failed: " + exception.Message);
            }

            foreach (var controller in FindAllInFixtureScenes<DecorationModeController>())
            {
                try
                {
                    var driver = ReadPrivate<DecorationCameraDriver>(controller, "cameraDriver");
                    var boundary = ReadPrivate<UiPointerBoundary>(controller, "pointerBoundary");
                    controller.enabled = false;
                    if (controller.State != DecorationSessionState.Closed)
                        cleanupFailures.Add("Decoration controller did not close on disable.");
                    if (ActivePreview(controller) != null)
                        cleanupFailures.Add("Decoration Preview survived owner disable.");
                    if (driver != null && driver.IsEdgeAutoPanning)
                        cleanupFailures.Add("Edge auto-pan survived owner disable.");
                    if (boundary != null)
                    {
                        var ownership = ReadPrivate<Dictionary<int, UiPointerOwnership>>(
                            boundary, "ownershipByPointer");
                        if (ownership.Count > 0)
                            cleanupFailures.Add("UI/Scene pointer ownership survived terminal cleanup: "
                                + string.Join(",", ownership.Select(pair => $"{pair.Key}:{pair.Value}")));
                        var sceneBlocks = ReadPrivate<int>(boundary, "sceneBlockCount");
                        if (sceneBlocks != 0)
                            cleanupFailures.Add("Modal Scene block survived terminal cleanup: " + sceneBlocks);
                    }
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add("Controller cleanup failed: " + exception.Message);
                }
            }

            foreach (var source in FindAllInFixtureScenes<InputSystemDecorationTouchSource>())
            {
                try
                {
                    source.enabled = false;
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add("Touch source cleanup failed: " + exception.Message);
                }
            }

            Time.timeScale = timeScaleBefore;
            if (!ReferenceEquals(Selection.activeObject, selectionBefore))
                cleanupFailures.Add("UnityEditor.Selection changed during runtime fixture.");
            if (new Vector2Int(Screen.width, Screen.height) != screenBefore)
                cleanupFailures.Add("Screen size changed during runtime fixture.");
            if (Screen.safeArea != safeAreaBefore)
                cleanupFailures.Add("Screen.safeArea changed during runtime fixture.");

            if (!cleanupScene.IsValid() || !cleanupScene.isLoaded)
            {
                try
                {
                    cleanupScene = SceneManager.CreateScene(cleanupSceneName + "_teardown");
                }
                catch (Exception exception)
                {
                    cleanupFailures.Add("Cleanup Scene recreation failed: " + exception.Message);
                }
            }
            if (cleanupScene.IsValid() && cleanupScene.isLoaded
                && !SceneManager.SetActiveScene(cleanupScene))
            {
                cleanupFailures.Add("The test-owned cleanup Scene could not become active before fixture unload.");
            }

            foreach (var scene in LoadedScenes().Where(scene => fixtureSceneHandles.Contains(scene.handle)).ToArray())
            {
                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }
            }

            var recordedScene = LoadedScenes().FirstOrDefault(scene =>
                activeSceneBefore.IsValid() && scene.handle == activeSceneBefore.handle);
            if (recordedScene.IsValid() && recordedScene.isLoaded)
            {
                if (!SceneManager.SetActiveScene(recordedScene))
                    cleanupFailures.Add("The recorded active Scene identity could not be restored.");
                if (cleanupScene.IsValid() && cleanupScene.isLoaded
                    && cleanupScene.handle != recordedScene.handle)
                {
                    var unloadCleanup = SceneManager.UnloadSceneAsync(cleanupScene);
                    while (unloadCleanup != null && !unloadCleanup.isDone)
                        yield return null;
                }
            }
            else if (!cleanupScene.IsValid() || !cleanupScene.isLoaded
                     || SceneManager.GetActiveScene().handle != cleanupScene.handle)
            {
                cleanupFailures.Add("The cleanup Scene was not retained as the restored Test Runner boundary.");
            }
            fixtureSceneHandles.Clear();
            Assert.That(cleanupFailures, Is.Empty, string.Join(" | ", cleanupFailures));
        }

        [UnityTest]
        public IEnumerator MainCafeRealTouch_HudEnterExitOwnsPauseGridCatalogueAndRestoresCleanly()
        {
            yield return LoadMainCafe();
            var context = CaptureMainCafe();
            var touch = AddTouchscreen();
            try
            {
                var before = SnapshotLayout(context.Layout);
                yield return TapButton(touch, 101, context.HudButton);
                yield return WaitUntil(() => context.Controller.IsOpen, 2f, "HUD Touch did not enter Decoration Mode.");
                Assert.That(context.Controller.State, Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(context.Catalogue.IsCatalogueVisible, Is.True);
                Assert.That(context.Grid.gameObject.activeInHierarchy, Is.True);
                Assert.That(ActiveBaseGridVisualCount(context), Is.EqualTo(64));
                Assert.That(SnapshotLayout(context.Layout), Is.EqualTo(before));
                Assert.That(FindAll<EventSystem>(context.Scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<DecorationModeController>(context.Scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<Transform>(context.Scene).Count(item => item.name == "UI Root"), Is.EqualTo(1));

                yield return TapButton(touch, 102, context.HudButton);
                yield return WaitUntil(() => !context.Controller.IsOpen, 2f, "HUD Touch did not exit Decoration Mode.");
                Assert.That(context.Controller.State, Is.EqualTo(DecorationSessionState.Closed));
                Assert.That(context.Catalogue.IsCatalogueVisible, Is.False);
                Assert.That(ActiveBaseGridVisualCount(context), Is.Zero);
                Assert.That(ActiveFootprintVisualCount(context), Is.Zero);
                Assert.That(Time.timeScale, Is.EqualTo(timeScaleBefore));

                yield return TapButton(touch, 103, context.HudButton);
                Assert.That(context.Controller.IsOpen, Is.True, "A fresh Touch must re-enter after cleanup.");
            }
            finally
            {
                CleanupTouchscreen(touch);
            }
        }

        [UnityTest]
        public IEnumerator MainCafeRealTouch_CatalogueTileCreatesNearestPreviewAndDragUsesOffsetWithoutCameraPan()
        {
            yield return LoadMainCafe();
            var context = CaptureMainCafe();
            var touch = AddTouchscreen();
            try
            {
                yield return EnterByTouch(context, touch, 111);
                yield return SelectTileByTouch(context, touch, 112, 0);
                var beforeCamera = context.Camera.transform.position;
                var previewBefore = ActivePreview(context.Controller);
                Assert.That(previewBefore, Is.Not.Null);
                Assert.That(context.Controller.State, Is.EqualTo(DecorationSessionState.PreviewingNewFurniture));
                AssertGridPosition(previewBefore.ProposedPosition,
                    FindContainingGridCellAtScreen(context, context.Camera.pixelRect.center),
                    "Catalogue selection must start at the Grid cell nearest Camera centre.");
                Assert.That(context.Catalogue.State, Is.EqualTo(DecorationCatalogueState.Collapsed));
                Assert.That(context.Catalogue.IsCatalogueVisible, Is.True);
                Assert.That(context.Catalogue.IsCollapsed, Is.True);
                Assert.That(context.Catalogue.CollapsedHandleRect.gameObject.activeInHierarchy, Is.True);
                Assert.That(FindNamed<Button>(context.Scene, "StoreButton").gameObject.activeInHierarchy,
                    Is.False);

                var offset = ReadPrivate<float>(context.Controller, "sanitizedFurnitureDragOffsetPixels");
                var threshold = ReadPrivate<AnimalCafe.Camera.CameraSettings>(
                    context.Controller, "cameraSettings").DragThresholdPixels;
                var start = GridCellScreenCenter(context, previewBefore.ProposedPosition);
                var targetCandidates = new[]
                {
                    new GridPosition(previewBefore.ProposedPosition.X + 2, previewBefore.ProposedPosition.Y),
                    new GridPosition(previewBefore.ProposedPosition.X - 2, previewBefore.ProposedPosition.Y),
                    new GridPosition(previewBefore.ProposedPosition.X, previewBefore.ProposedPosition.Y + 2),
                    new GridPosition(previewBefore.ProposedPosition.X, previewBefore.ProposedPosition.Y - 2)
                };
                var targetCell = targetCandidates.First(candidate =>
                {
                    if (!context.GridSpace.Bounds.Contains(candidate)
                        || !context.Layout.ValidateFurniturePlacement(
                            previewBefore.DefinitionId,
                            candidate,
                            previewBefore.ProposedRotation).Succeeded)
                    {
                        return false;
                    }

                    var rawPoint = GridCellScreenCenter(context, candidate) - Vector2.up * offset;
                    return context.Camera.pixelRect.Contains(rawPoint)
                        && TopGraphicAt(context.EventSystem, rawPoint) == null;
                });
                var target = GridCellScreenCenter(context, targetCell) - Vector2.up * offset;
                AssertWorldPointIsUiFree(context.EventSystem, start);
                AssertWorldPointIsUiFree(context.EventSystem, target);
                yield return BeginContact(touch, 113, start);
                yield return MoveContact(touch, 113, start + Vector2.right * (threshold * 0.5f));
                AssertGridPosition(ActivePreview(context.Controller).ProposedPosition,
                    previewBefore.ProposedPosition,
                    "Within-threshold movement must not move the Preview.");
                Assert.That(context.Camera.transform.position, Is.EqualTo(beforeCamera));
                yield return MoveContact(touch, 113, target);

                var previewAfter = ActivePreview(context.Controller);
                AssertGridPosition(previewAfter.ProposedPosition, targetCell,
                    "The upward-offset coordinate must project to the visible target cell.");
                Assert.That(context.Camera.transform.position, Is.EqualTo(beforeCamera));
                Assert.That(context.Controller.State, Is.EqualTo(DecorationSessionState.PreviewingNewFurniture));
                yield return Release(touch, 113, target);
                Assert.That(ActivePreview(context.Controller), Is.Not.Null,
                    "Release without Confirm must retain the active Preview.");

                var cancel = FindNamed<Button>(context.Scene, "CancelButton");
                var crossedUiRecorder = cancel.gameObject.AddComponent<PointerRecorder>();
                var freshStart = GridCellScreenCenter(
                    context, ActivePreview(context.Controller).ProposedPosition);
                var blank = UiFreeCell(context,
                    new[] { new GridPosition(6, 6), new GridPosition(0, 6), new GridPosition(6, 1) });
                yield return BeginContact(touch, 114, freshStart);
                var router = ReadPrivate<DecorationTouchRouter>(context.Controller, "touchRouter");
                Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Furniture));
                yield return MoveContact(touch, 114, blank);
                var cellBeforeUiCross = ActivePreview(context.Controller).ProposedPosition;
                yield return MoveContact(touch, 114, ButtonCenter(cancel));
                Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Furniture));
                AssertGridPosition(ActivePreview(context.Controller).ProposedPosition, cellBeforeUiCross,
                    "Crossing UI from a Furniture-origin hold must not move or switch the Preview.");
                Assert.That(context.Camera.transform.position, Is.EqualTo(beforeCamera));
                Assert.That(crossedUiRecorder.DownCount, Is.Zero);
                Assert.That(crossedUiRecorder.ClickCount, Is.Zero);
                yield return Release(touch, 114, ButtonCenter(cancel));
                Assert.That(crossedUiRecorder.ClickCount, Is.Zero);
                Assert.That(context.Controller.State, Is.EqualTo(DecorationSessionState.PreviewingNewFurniture));
                AssertGridPosition(ActivePreview(context.Controller).ProposedPosition, cellBeforeUiCross,
                    "Release over UI must keep the Furniture-origin selection latched.");
            }
            finally
            {
                CleanupTouchscreen(touch);
            }
        }

        [UnityTest]
        public IEnumerator MainCafeRealTouch_BlankFloorDragPansOnlyCameraAndCannotMovePreview()
        {
            yield return LoadMainCafe();
            var context = CaptureMainCafe();
            var touch = AddTouchscreen();
            try
            {
                yield return EnterByTouch(context, touch, 121);
                yield return TapButton(touch, 122, FindNamed<Button>(context.Scene, "CollapseButton"));
                yield return WaitUntil(() => context.Catalogue.IsCollapsed, 2f,
                    "Catalogue did not collapse before blank Scene ownership coverage.");
                var blank = UiFreeCell(context, new[] { new GridPosition(6, 6), new GridPosition(6, 1), new GridPosition(0, 6) });
                var beforeCamera = context.Camera.transform.position;
                var beforeLayout = SnapshotLayout(context.Layout);
                var beforeSelection = context.SceneInteraction.CurrentSelection;
                var furniturePoint = FormalFurnitureScreenPoint(context, InitialInstanceId);
                var collapsedHandle = FindNamed<Button>(context.Scene, "CollapsedHandle");
                var crossedUiRecorder = collapsedHandle.gameObject.AddComponent<PointerRecorder>();
                yield return BeginContact(touch, 123, blank);
                yield return MoveContact(touch, 123, blank + new Vector2(90f, 35f));
                Assert.That(context.Camera.transform.position, Is.Not.EqualTo(beforeCamera));
                var router = ReadPrivate<DecorationTouchRouter>(context.Controller, "touchRouter");
                Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Camera));
                yield return MoveContact(touch, 123, furniturePoint);
                Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Camera),
                    "Scene-origin ownership must not switch when crossing formal Furniture.");
                yield return MoveContact(touch, 123, ButtonCenter(collapsedHandle));
                Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Camera),
                    "Scene-origin ownership must not switch when crossing UI.");
                yield return Release(touch, 123, ButtonCenter(collapsedHandle));
                Assert.That(SnapshotLayout(context.Layout), Is.EqualTo(beforeLayout));
                Assert.That(ActivePreview(context.Controller), Is.Null);
                Assert.That(context.SceneInteraction.CurrentSelection, Is.SameAs(beforeSelection));
                Assert.That(crossedUiRecorder.DownCount, Is.Zero);
                Assert.That(crossedUiRecorder.ClickCount, Is.Zero);
                Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.None));

                var freshBlank = UiFreeCell(context,
                    new[] { new GridPosition(6, 6), new GridPosition(6, 1), new GridPosition(0, 6) });
                var beforeFreshCamera = context.Camera.transform.position;
                yield return Drag(touch, 124, freshBlank, freshBlank + new Vector2(-75f, 30f));
                Assert.That(context.Camera.transform.position, Is.Not.EqualTo(beforeFreshCamera),
                    "Only a later fresh Scene Touch may begin a new Camera gesture.");
                Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.None));
            }
            finally
            {
                CleanupTouchscreen(touch);
            }
        }

        [UnityTest]
        public IEnumerator MainCafeRealTouch_UiBeganMoveAcrossWorldRetainsUiOwnerAndReleaseCannotSelectOrMove()
        {
            yield return LoadMainCafe();
            var context = CaptureMainCafe();
            var touch = AddTouchscreen();
            try
            {
                yield return EnterByTouch(context, touch, 131);
                var collapse = FindNamed<Button>(context.Scene, "CollapseButton");
                var recorder = collapse.gameObject.AddComponent<PointerRecorder>();
                var boundary = ReadPrivate<UiPointerBoundary>(context.Controller, "pointerBoundary");
                recorder.Boundary = boundary;
                var start = ButtonCenter(collapse);
                var furniture = FormalFurnitureScreenPoint(context, InitialInstanceId);
                var blank = UiFreeCell(context,
                    new[] { new GridPosition(6, 6), new GridPosition(6, 1), new GridPosition(0, 6) });
                var cameraBefore = context.Camera.transform.position;
                var layoutBefore = SnapshotLayout(context.Layout);
                var selectionBeforeGesture = context.SceneInteraction.CurrentSelection;
                yield return BeginUiContact(touch, 132, start);
                Assert.That(recorder.DownCount, Is.EqualTo(1));
                foreach (var pointerId in recorder.CompositePointerIds.Distinct())
                {
                    Assert.That(boundary.GetOwnership(pointerId), Is.EqualTo(UiPointerOwnership.Ui),
                        "The completed UI Began frame must have stable UI ownership.");
                }
                yield return MoveContact(touch, 132, furniture);
                yield return MoveContact(touch, 132, blank);
                yield return ReleaseUiContact(touch, 132, blank);
                Assert.That(recorder.DownCount, Is.EqualTo(1));
                Assert.That(recorder.UpCount, Is.EqualTo(1));
                AssertRecorderNamespace(recorder, 132);
                AssertRecorderOrdering(recorder);
                Assert.That(context.Camera.transform.position, Is.EqualTo(cameraBefore));
                Assert.That(SnapshotLayout(context.Layout), Is.EqualTo(layoutBefore));
                Assert.That(ActivePreview(context.Controller), Is.Null);
                Assert.That(context.SceneInteraction.CurrentSelection, Is.SameAs(selectionBeforeGesture));
                Assert.That(ReadPrivate<DecorationTouchRouter>(context.Controller, "touchRouter").Owner,
                    Is.EqualTo(DecorationGestureOwner.None));

                Assert.That(context.Registry.TryGet(InitialInstanceId, out var releaseUnderlay), Is.True);
                PlaceFurnitureBehindScreenPoint(context, releaseUnderlay, start);
                AssertFurnitureUnderScreenPoint(context, start, InitialInstanceId);
                var recordsBeforeCollapseTap = recorder.Events.Count;
                yield return TapButton(touch, 133, collapse);
                Assert.That(context.Catalogue.IsCollapsed, Is.True,
                    "The actual Collapse Button click must complete once.");
                Assert.That(recorder.ClickCount, Is.EqualTo(1));
                AssertRecorderNamespace(recorder, 133, recordsBeforeCollapseTap);
                Assert.That(context.SceneInteraction.CurrentSelection, Is.SameAs(selectionBeforeGesture),
                    "Collapse release must not pass through to the proven Furniture target underneath.");
                Assert.That(ActivePreview(context.Controller), Is.Null);
                Assert.That(SnapshotLayout(context.Layout), Is.EqualTo(layoutBefore));
                var freshBlank = UiFreeCell(context,
                    new[] { new GridPosition(6, 6), new GridPosition(6, 1), new GridPosition(0, 6) });
                yield return Drag(touch, 134, freshBlank, freshBlank + Vector2.right * 70f);
                Assert.That(context.Camera.transform.position, Is.Not.EqualTo(cameraBefore),
                    "Only a later fresh Scene Touch may pan after the UI release.");
                AssertPointerBoundaryClean(boundary);
            }
            finally
            {
                CleanupTouchscreen(touch);
            }
        }

        [UnityTest]
        public IEnumerator MainCafeRealTouch_SecondTouchOnBlankRotateOrCancelPromotesDragToPinchWithoutUiOrDomainMutation()
        {
            var secondOrigins = new[] { "Blank", "RotateButton", "CancelButton" };
            var branch = 0;
            foreach (var origin in secondOrigins)
            {
                yield return LoadMainCafe();
                var context = CaptureMainCafe();
                var touch = AddTouchscreen();
                var rotateRequested = 0;
                var cancelRequested = 0;
                Action onRotate = () => rotateRequested++;
                Action onCancel = () => cancelRequested++;
                context.ActionBar.RotateRequested += onRotate;
                context.ActionBar.CancelRequested += onCancel;
                try
                {
                    var id = 141 + branch * 10;
                    yield return EnterByTouch(context, touch, id);
                    yield return SelectTileByTouch(context, touch, id + 1, 1);
                    var preview = ActivePreview(context.Controller);
                    var primary = GridCellScreenCenter(context, preview.ProposedPosition);
                    yield return BeginContact(touch, id + 2, primary);
                    yield return MoveContact(touch, id + 2, primary + Vector2.right * 70f);
                    var held = ActivePreview(context.Controller);
                    var layoutBefore = SnapshotLayout(context.Layout);
                    var heldCell = held.ProposedPosition;
                    var heldRotation = held.ProposedRotation;
                    var heldState = context.Controller.State;
                    var selectionBeforePinch = context.SceneInteraction.CurrentSelection;
                    var cameraSizeBefore = context.Camera.orthographicSize;
                    Vector2 second;
                    PointerRecorder recorder = null;
                    if (origin == "Blank")
                    {
                        second = UiFreeCell(context, new[] { new GridPosition(6, 6), new GridPosition(0, 6) });
                    }
                    else
                    {
                        var button = FindNamed<Button>(context.Scene, origin);
                        AssertTopButton(context.EventSystem, button);
                        recorder = button.gameObject.AddComponent<PointerRecorder>();
                        recorder.Boundary = ReadPrivate<UiPointerBoundary>(context.Controller, "pointerBoundary");
                        second = ButtonCenter(button);
                    }

                    yield return BeginContact(touch, id + 3, second);
                    var router = ReadPrivate<DecorationTouchRouter>(context.Controller, "touchRouter");
                    Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Pinch), origin);
                    if (recorder != null)
                    {
                        Assert.That(recorder.DownCount, Is.EqualTo(1), origin);
                        foreach (var pointerId in recorder.CompositePointerIds.Distinct())
                        {
                            Assert.That(recorder.Boundary.GetOwnership(pointerId),
                                Is.EqualTo(UiPointerOwnership.Ui),
                                origin + " completed UI Began frame must have UI ownership.");
                        }
                    }
                    AssertGridPosition(ActivePreview(context.Controller).ProposedPosition, heldCell,
                        origin + " second Began must stop Furniture movement immediately.");
                    yield return MoveContact(touch, id + 2, primary + Vector2.right * 150f);
                    Assert.That(context.Camera.orthographicSize, Is.Not.EqualTo(cameraSizeBefore), origin);
                    var settings = ReadPrivate<AnimalCafe.Camera.CameraSettings>(
                        context.Controller, "cameraSettings");
                    Assert.That(context.Camera.orthographicSize,
                        Is.InRange(settings.MinOrthographicSize, settings.MaxOrthographicSize), origin);
                    Assert.That(SnapshotLayout(context.Layout), Is.EqualTo(layoutBefore), origin);
                    AssertGridPosition(ActivePreview(context.Controller).ProposedPosition, heldCell, origin);
                    Assert.That(ActivePreview(context.Controller).ProposedRotation, Is.EqualTo(heldRotation), origin);
                    Assert.That(context.Controller.State, Is.EqualTo(heldState), origin);
                    Assert.That(context.SceneInteraction.CurrentSelection, Is.SameAs(selectionBeforePinch), origin);
                    Assert.That(rotateRequested, Is.Zero, origin);
                    Assert.That(cancelRequested, Is.Zero, origin);

                    var beforeRebaseCell = ActivePreview(context.Controller).ProposedPosition;
                    var beforeRebaseRotation = ActivePreview(context.Controller).ProposedRotation;
                    var beforeRebaseCamera = context.Camera.transform.position;
                    var beforeRebaseSize = context.Camera.orthographicSize;
                    yield return Release(touch, id + 3, second);
                    AssertGridPosition(ActivePreview(context.Controller).ProposedPosition, beforeRebaseCell,
                        origin + " secondary terminal frame must provide a zero-command rebase.");
                    Assert.That(ActivePreview(context.Controller).ProposedRotation,
                        Is.EqualTo(beforeRebaseRotation), origin);
                    Assert.That(context.Camera.transform.position, Is.EqualTo(beforeRebaseCamera), origin);
                    Assert.That(context.Camera.orthographicSize, Is.EqualTo(beforeRebaseSize), origin);
                    var expectedRotatePresentation = origin == "RotateButton" ? 1 : 0;
                    var expectedCancelPresentation = origin == "CancelButton" ? 1 : 0;
                    Assert.That(rotateRequested, Is.EqualTo(expectedRotatePresentation),
                        origin + " may emit only its matching presentation request on release.");
                    Assert.That(cancelRequested, Is.EqualTo(expectedCancelPresentation),
                        origin + " may emit only its matching presentation request on release.");
                    if (recorder != null)
                    {
                        AssertRecorderNamespace(recorder, id + 3);
                        AssertRecorderOrdering(recorder);
                        foreach (var pointerId in recorder.CompositePointerIds.Distinct())
                        {
                            Assert.That(recorder.Boundary.GetOwnership(pointerId), Is.EqualTo(UiPointerOwnership.None));
                        }
                    }

                    yield return MoveContact(touch, id + 2, primary + Vector2.right * 220f);
                    var resumedCell = ActivePreview(context.Controller).ProposedPosition;
                    Assert.That(resumedCell.X != beforeRebaseCell.X || resumedCell.Y != beforeRebaseCell.Y,
                        Is.True, origin + " must resume Furniture drag.");
                    yield return Release(touch, id + 2, primary + Vector2.right * 220f);
                    Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.None));
                    Assert.That(rotateRequested, Is.EqualTo(expectedRotatePresentation), origin);
                    Assert.That(cancelRequested, Is.EqualTo(expectedCancelPresentation), origin);

                    if (origin == "CancelButton")
                    {
                        yield return TapButton(touch, id + 4,
                            FindNamed<Button>(context.Scene, "CancelButton"));
                        Assert.That(cancelRequested, Is.EqualTo(expectedCancelPresentation + 1));
                        Assert.That(context.Controller.State,
                            Is.EqualTo(DecorationSessionState.BrowsingCatalogue));
                        Assert.That(ActivePreview(context.Controller), Is.Null,
                            "The rejected Pinch click must restore the terminal latch for a fresh Cancel.");
                    }
                    else
                    {
                        var rotationBeforeFreshAction = ActivePreview(context.Controller).ProposedRotation;
                        yield return TapButton(touch, id + 4,
                            FindNamed<Button>(context.Scene, "RotateButton"));
                        Assert.That(rotateRequested, Is.EqualTo(expectedRotatePresentation + 1));
                        Assert.That(ActivePreview(context.Controller).ProposedRotation,
                            Is.Not.EqualTo(rotationBeforeFreshAction),
                            "A fresh UI-owned Rotate must work after the Pinch gesture fully ends.");
                    }
                }
                finally
                {
                    context.ActionBar.RotateRequested -= onRotate;
                    context.ActionBar.CancelRequested -= onCancel;
                    CleanupTouchscreen(touch);
                }
                branch++;
            }
        }

        [UnityTest]
        public IEnumerator MainCafeRealTouch_FurnitureEdgeDragStartsBoundedPanAndEveryExcludedOrTerminalStateStopsIt()
        {
            var edges = new[]
            {
                (Name: "Left", Intent: new Vector2(1f, 0f)),
                (Name: "Right", Intent: new Vector2(-1f, 0f)),
                (Name: "Bottom", Intent: new Vector2(0f, 1f)),
                (Name: "Top", Intent: new Vector2(0f, -1f))
            };
            var usableEdges = 0;
            var usableHorizontalEdge = false;
            var usableVerticalEdge = false;
            var reprojectedUsableEdges = 0;
            var branch = 0;
            foreach (var edgeCase in edges)
            {
                yield return LoadMainCafe();
                var context = CaptureMainCafe();
                var touch = AddTouchscreen();
                try
                {
                    var id = 151 + branch * 10;
                    yield return EnterByTouch(context, touch, id);
                    yield return SelectTileByTouch(context, touch, id + 1, 0);
                    var edge = FindUiFreeEdgePoint(context, edgeCase.Name);
                    if (!edge.HasValue)
                    {
                        Debug.Log($"TASK9_RT06 edge={edgeCase.Name} usable=false reason=ui-coverage");
                        branch++;
                        continue;
                    }

                    var preview = ActivePreview(context.Controller);
                    var start = GridCellScreenCenter(context, preview.ProposedPosition);
                    var driver = ReadPrivate<DecorationCameraDriver>(context.Controller, "cameraDriver");
                    var before = context.Camera.transform.position;
                    var flatForward = Vector3.ProjectOnPlane(context.Camera.transform.forward, Vector3.up).normalized;
                    var flatRight = Vector3.ProjectOnPlane(context.Camera.transform.right, Vector3.up).normalized;
                    var expectedWorldDirection = -(flatRight * edgeCase.Intent.x + flatForward * edgeCase.Intent.y);
                    yield return BeginContact(touch, id + 2, start);
                    yield return MoveContact(touch, id + 2, edge.Value);
                    if (!driver.IsEdgeAutoPanning)
                    {
                        yield return Cancel(touch, id + 2, edge.Value);
                        Debug.Log($"TASK9_RT06 edge={edgeCase.Name} usable=false reason=edge-pan-not-started");
                        branch++;
                        continue;
                    }

                    usableEdges++;
                    if (edgeCase.Name == "Left" || edgeCase.Name == "Right")
                    {
                        usableHorizontalEdge = true;
                    }
                    else
                    {
                        usableVerticalEdge = true;
                    }
                    var cameraDelta = context.Camera.transform.position - before;
                    Assert.That(Vector3.Dot(cameraDelta, expectedWorldDirection), Is.GreaterThan(0f),
                        edgeCase.Name + " edge Camera direction");
                    var maxSpeed = driver.MaxEdgeSpeedPixelsPerSecond
                        * Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
                    Assert.That(cameraDelta.magnitude, Is.LessThanOrEqualTo(maxSpeed + 0.1f), edgeCase.Name);

                    var cellBeforeStationary = ActivePreview(context.Controller).ProposedPosition;
                    var reprojected = false;
                    var stationaryDeadline = Time.realtimeSinceStartup + 2f;
                    for (var frame = 0;
                         frame < 80 && !reprojected && Time.realtimeSinceStartup < stationaryDeadline;
                         frame++)
                    {
                        yield return MoveContact(touch, id + 2, edge.Value);
                        var current = ActivePreview(context.Controller).ProposedPosition;
                        reprojected = current.X != cellBeforeStationary.X || current.Y != cellBeforeStationary.Y;
                    }
                    if (reprojected)
                    {
                        reprojectedUsableEdges++;
                    }
                    var actionPoint = ButtonCenter(FindNamed<Button>(context.Scene, "CancelButton"));
                    yield return MoveContact(touch, id + 2, actionPoint);
                    Assert.That(driver.IsEdgeAutoPanning, Is.False,
                        "Visible Bottom Sheet/Action UI must stop edge auto-pan in the same frame.");

                    yield return MoveContact(touch, id + 2, start);
                    Assert.That(driver.IsEdgeAutoPanning, Is.False,
                        edgeCase.Name + " interior recovery must remain outside edge auto-pan.");
                    yield return MoveContact(touch, id + 2, edge.Value);
                    Assert.That(driver.IsEdgeAutoPanning, Is.True, edgeCase.Name + " re-entry");
                    var outside = edge.Value + edgeCase.Intent * -200f;
                    yield return MoveContact(touch, id + 2, outside);
                    Assert.That(driver.IsEdgeAutoPanning, Is.False,
                        "Leaving the usable viewport must stop edge auto-pan in the same frame.");

                    yield return MoveContact(touch, id + 2, start);
                    Assert.That(driver.IsEdgeAutoPanning, Is.False,
                        edgeCase.Name + " outside recovery must return through the interior.");
                    yield return MoveContact(touch, id + 2, edge.Value);
                    Assert.That(driver.IsEdgeAutoPanning, Is.True, edgeCase.Name + " before Cancel");
                    yield return Cancel(touch, id + 2, edge.Value);
                    Assert.That(driver.IsEdgeAutoPanning, Is.False, edgeCase.Name + " terminal Cancel");

                    var restart = GridCellScreenCenter(context, ActivePreview(context.Controller).ProposedPosition);
                    var router = ReadPrivate<DecorationTouchRouter>(context.Controller, "touchRouter");
                    yield return WaitUntil(
                        () => UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches.Count == 0
                            && router.Owner == DecorationGestureOwner.None
                            && router.PrimaryTouchId == DecorationTouchRouter.NoTouchId
                            && router.SecondaryTouchId == DecorationTouchRouter.NoTouchId
                            && touch.touches.All(contact =>
                            {
                                var phase = contact.phase.ReadValue();
                                return phase == InputTouchPhase.None
                                    || phase == InputTouchPhase.Ended
                                    || phase == InputTouchPhase.Canceled;
                            }),
                        2f,
                        edgeCase.Name + " terminal Touch did not clear before the independent owner-disable gesture.");
                    var disableEdge = FindUiFreeEdgePoint(context, edgeCase.Name);
                    Assert.That(disableEdge.HasValue, Is.True,
                        edgeCase.Name + " must expose a fresh UI-free point for owner-disable coverage.");
                    yield return BeginContact(touch, id + 3, restart);
                    yield return MoveContact(touch, id + 3, disableEdge.Value);
                    Assert.That(driver.IsEdgeAutoPanning, Is.True, edgeCase.Name + " before owner disable");
                    context.Controller.enabled = false;
                    Assert.That(driver.IsEdgeAutoPanning, Is.False, edgeCase.Name + " owner disable");
                    yield return Cancel(touch, id + 3, disableEdge.Value);
                }
                finally
                {
                    CleanupTouchscreen(touch);
                }
                branch++;
            }
            Assert.That(usableEdges, Is.GreaterThanOrEqualTo(2),
                "The active mobile composition must expose at least two usable edge zones.");
            Assert.That(usableHorizontalEdge, Is.True,
                "The active mobile composition must expose a usable horizontal edge zone.");
            Assert.That(usableVerticalEdge, Is.True,
                "The active mobile composition must expose a usable vertical edge zone.");
            Assert.That(reprojectedUsableEdges, Is.Zero,
                "Edge auto-pan must keep every furniture preview clamped inside the floor.");

            yield return LoadMainCafe();
            var modalContext = CaptureMainCafe();
            var modalTouch = AddTouchscreen();
            try
            {
                yield return EnterByTouch(modalContext, modalTouch, 195);
                yield return SelectInitialCounter(modalContext, modalTouch, 196);
                yield return TapButton(modalTouch, 197, FindNamed<Button>(modalContext.Scene, "StoreButton"));
                var modalDriver = ReadPrivate<DecorationCameraDriver>(modalContext.Controller, "cameraDriver");
                var blocker = FindNamed<Button>(modalContext.Scene, "ModalBlocker");
                yield return BeginUiContact(modalTouch, 198, ButtonCenter(blocker));
                Assert.That(modalDriver.IsEdgeAutoPanning, Is.False,
                    "Modal coverage must exclude Furniture edge auto-pan.");
                yield return ReleaseUiContact(modalTouch, 198, ButtonCenter(blocker));
            }
            finally
            {
                CleanupTouchscreen(modalTouch);
            }
        }

        [UnityTest]
        public IEnumerator MainCafeRealTouch_StoreModalWinsBeganBlocksAllLowerLayersAndDismissReleaseDoesNotPassThrough()
        {
            yield return LoadMainCafe();
            var context = CaptureMainCafe();
            var touch = AddTouchscreen();
            var storeRequested = 0;
            var dismissRequested = 0;
            var confirmRequested = 0;
            Action onStore = () => storeRequested++;
            Action onDismiss = () => dismissRequested++;
            Action onConfirm = () => confirmRequested++;
            FurnitureInstance dismissUnderlayInstance = null;
            GameObject dismissUnderlayRepresentation = null;
            Vector3 dismissUnderlayPosition = default;
            Quaternion dismissUnderlayRotation = default;
            context.ActionBar.StoreRequested += onStore;
            context.StoreModal.DismissRequested += onDismiss;
            context.StoreModal.ConfirmRequested += onConfirm;
            try
            {
                yield return EnterByTouch(context, touch, 161);
                var formalPointBeforeSelection = FormalFurnitureScreenPoint(context, InitialInstanceId);
                yield return SelectInitialCounter(context, touch, 162);
                Assert.That(context.Controller.State, Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
                var before = SnapshotLayout(context.Layout);
                var selectionBeforeModal = context.SceneInteraction.CurrentSelection;
                var cameraBeforeModal = context.Camera.transform.position;
                var previewBeforeModal = ActivePreview(context.Controller);
                var previewCellBeforeModal = previewBeforeModal.ProposedPosition;
                var previewRotationBeforeModal = previewBeforeModal.ProposedRotation;
                yield return TapButton(touch, 163, FindNamed<Button>(context.Scene, "StoreButton"));
                Assert.That(storeRequested, Is.EqualTo(1));
                Assert.That(context.Controller.State, Is.EqualTo(DecorationSessionState.ConfirmingStore));
                Assert.That(context.StoreModal.IsOpen, Is.True);
                Assert.That(SnapshotLayout(context.Layout), Is.EqualTo(before));

                var modalRoot = context.StoreModal.transform;
                var coveredPoints = new[]
                {
                    ButtonCenter(FindNamed<Button>(context.Scene, "StoreButton")),
                    formalPointBeforeSelection,
                    GridCellScreenCenter(context, new GridPosition(6, 6))
                };
                var coveredId = 164;
                foreach (var coveredPoint in coveredPoints)
                {
                    var top = TopGraphicAt(context.EventSystem, coveredPoint);
                    Assert.That(top, Is.Not.Null);
                    Assert.That(top.transform.IsChildOf(modalRoot), Is.True,
                        "Every covered Action/Scene/Furniture point must be owned by the top Modal hierarchy.");
                    yield return BeginUiContact(touch, coveredId, coveredPoint);
                    yield return ReleaseUiContact(touch, coveredId, coveredPoint);
                    coveredId++;
                }
                Assert.That(context.Controller.State, Is.EqualTo(DecorationSessionState.ConfirmingStore));
                Assert.That(SnapshotLayout(context.Layout), Is.EqualTo(before));
                Assert.That(context.Camera.transform.position, Is.EqualTo(cameraBeforeModal));
                Assert.That(context.SceneInteraction.CurrentSelection, Is.SameAs(selectionBeforeModal));
                AssertGridPosition(ActivePreview(context.Controller).ProposedPosition, previewCellBeforeModal,
                    "Modal coverage must not move the existing Preview.");
                Assert.That(ActivePreview(context.Controller).ProposedRotation, Is.EqualTo(previewRotationBeforeModal));
                Assert.That(storeRequested, Is.EqualTo(1));
                Assert.That(dismissRequested, Is.Zero);
                Assert.That(confirmRequested, Is.Zero);

                var modalCancel = context.StoreModal.transform.Find("SafeArea/Content/CancelButton").GetComponent<Button>();
                var modalCancelPoint = ButtonCenter(modalCancel);
                var underlayDefinition = context.Layout.FurnitureInstances
                    .Single(item => item.InstanceId == InitialInstanceId).DefinitionId;
                dismissUnderlayInstance = FurnitureInstance.CreateNew(
                    underlayDefinition,
                    new GridPosition(6, 6),
                    FurnitureRotation.Degrees0);
                Assert.That(context.Layout.PlaceFurniture(dismissUnderlayInstance).Succeeded, Is.True,
                    "RT07 needs a second formal layout-backed Furniture instance under Modal Cancel.");
                context.Registry.Rebuild(context.Layout.FurnitureInstances);
                Assert.That(context.Registry.TryGet(
                    dismissUnderlayInstance.InstanceId, out dismissUnderlayRepresentation), Is.True);
                dismissUnderlayPosition = dismissUnderlayRepresentation.transform.position;
                dismissUnderlayRotation = dismissUnderlayRepresentation.transform.rotation;
                PlaceFurnitureBehindScreenPoint(
                    context, dismissUnderlayRepresentation, modalCancelPoint);
                AssertFurnitureUnderScreenPoint(
                    context, modalCancelPoint, dismissUnderlayInstance.InstanceId);
                var layoutBeforeDismiss = SnapshotLayout(context.Layout);
                var selectionBeforeDismiss = context.SceneInteraction.CurrentSelection;
                var cellBeforeDismiss = ActivePreview(context.Controller).ProposedPosition;
                var rotationBeforeDismiss = ActivePreview(context.Controller).ProposedRotation;
                yield return TapButton(touch, 168, modalCancel);
                Assert.That(dismissRequested, Is.EqualTo(1));
                Assert.That(context.Controller.State, Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
                Assert.That(ActivePreview(context.Controller).SourceInstanceId, Is.EqualTo(InitialInstanceId));
                AssertGridPosition(ActivePreview(context.Controller).ProposedPosition, cellBeforeDismiss,
                    "Modal Cancel release must not restart or move the selected Counter.");
                Assert.That(ActivePreview(context.Controller).ProposedRotation, Is.EqualTo(rotationBeforeDismiss));
                Assert.That(SnapshotLayout(context.Layout), Is.EqualTo(layoutBeforeDismiss));
                Assert.That(context.SceneInteraction.CurrentSelection, Is.SameAs(selectionBeforeDismiss),
                    "Modal Cancel release must not pass through to the proven formal Furniture route underneath.");

                dismissUnderlayRepresentation.transform.SetPositionAndRotation(
                    dismissUnderlayPosition, dismissUnderlayRotation);
                Physics.SyncTransforms();
                var freshUnderlayPoint = FormalFurnitureScreenPoint(
                    context, dismissUnderlayInstance.InstanceId);
                AssertWorldPointIsUiFree(context.EventSystem, freshUnderlayPoint);
                yield return Tap(touch, 169, freshUnderlayPoint);
                Assert.That(ActivePreview(context.Controller).SourceInstanceId,
                    Is.EqualTo(dismissUnderlayInstance.InstanceId),
                    "A later fresh Scene Touch must still select the registry-backed underlay Furniture.");
                var freshInitialPoint = FormalFurnitureScreenPoint(context, InitialInstanceId);
                AssertWorldPointIsUiFree(context.EventSystem, freshInitialPoint);
                yield return Tap(touch, 170, freshInitialPoint);
                Assert.That(ActivePreview(context.Controller).SourceInstanceId,
                    Is.EqualTo(InitialInstanceId));

                yield return TapButton(touch, 171, FindNamed<Button>(context.Scene, "StoreButton"));
                var modalStore = context.StoreModal.transform.Find("SafeArea/Content/StoreButton").GetComponent<Button>();
                yield return TapButton(touch, 172, modalStore);
                Assert.That(storeRequested, Is.EqualTo(2));
                Assert.That(confirmRequested, Is.EqualTo(1));
                Assert.That(context.Layout.FurnitureInstances.Select(item => item.InstanceId),
                    Does.Not.Contain(InitialInstanceId));
                Assert.That(context.Layout.FurnitureInstances.Select(item => item.InstanceId),
                    Is.EqualTo(new[] { dismissUnderlayInstance.InstanceId }).AsCollection,
                    "Modal Store must remove exactly the selected initial Instance once.");
            }
            finally
            {
                context.ActionBar.StoreRequested -= onStore;
                context.StoreModal.DismissRequested -= onDismiss;
                context.StoreModal.ConfirmRequested -= onConfirm;
                if (dismissUnderlayRepresentation != null)
                {
                    dismissUnderlayRepresentation.transform.SetPositionAndRotation(
                        dismissUnderlayPosition, dismissUnderlayRotation);
                    Physics.SyncTransforms();
                }
                CleanupTouchscreen(touch);
            }
        }

        [UnityTest]
        public IEnumerator MainCafeRealTouch_TwoPointerEndedCanceledOrdersAndControllerDisableLeaveNoStaleOwner()
        {
            yield return LoadMainCafe();
            var context = CaptureMainCafe();
            var touch = AddTouchscreen();
            try
            {
                yield return EnterByTouch(context, touch, 171);
                var orders = new[]
                {
                    (PrimaryFirst: false, First: InputTouchPhase.Ended,
                        Second: InputTouchPhase.Ended, Label: "secondary-ended then primary-ended"),
                    (PrimaryFirst: false, First: InputTouchPhase.Canceled,
                        Second: InputTouchPhase.Canceled, Label: "secondary-canceled then primary-canceled"),
                    (PrimaryFirst: true, First: InputTouchPhase.Ended,
                        Second: InputTouchPhase.Ended, Label: "primary-ended then secondary-ended"),
                    (PrimaryFirst: true, First: InputTouchPhase.Canceled,
                        Second: InputTouchPhase.Canceled, Label: "primary-canceled then secondary-canceled")
                };
                var router = ReadPrivate<DecorationTouchRouter>(context.Controller, "touchRouter");
                var driver = ReadPrivate<DecorationCameraDriver>(context.Controller, "cameraDriver");
                var id = 180;
                foreach (var order in orders)
                {
                    var start = UiFreeCell(context,
                        new[] { new GridPosition(6, 6), new GridPosition(0, 6), new GridPosition(6, 1) });
                    yield return BeginContact(touch, id, start);
                    yield return BeginContact(touch, id + 1, start + Vector2.right * 90f);
                    Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Pinch), order.Label);
                    Assert.That(router.PrimaryTouchId, Is.EqualTo(id), order.Label);
                    Assert.That(router.SecondaryTouchId, Is.EqualTo(id + 1), order.Label);
                    var secondaryPosition = start + Vector2.right * 90f;
                    var firstId = order.PrimaryFirst ? id : id + 1;
                    var firstPosition = order.PrimaryFirst ? start : secondaryPosition;
                    var secondId = order.PrimaryFirst ? id + 1 : id;
                    var secondPosition = order.PrimaryFirst ? secondaryPosition : start;
                    yield return TerminateContact(touch, firstId, firstPosition, order.First);
                    Assert.That(activeTouchIds.Contains(firstId), Is.False, order.Label);
                    Assert.That(activeTouchIds.Contains(secondId), Is.True, order.Label);
                    if (order.PrimaryFirst)
                    {
                        Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.None), order.Label);
                        Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.True, order.Label);
                    }
                    else
                    {
                        Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Camera), order.Label);
                        Assert.That(router.PrimaryTouchId, Is.EqualTo(id), order.Label);
                        Assert.That(router.SecondaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId), order.Label);
                        Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.False, order.Label);
                    }
                    yield return TerminateContact(touch, secondId, secondPosition, order.Second);
                    AssertRouterClean(router);
                    Assert.That(driver.IsEdgeAutoPanning, Is.False);

                    var fresh = UiFreeCell(context,
                        new[] { new GridPosition(6, 6), new GridPosition(0, 6), new GridPosition(6, 1) });
                    var beforeFresh = context.Camera.transform.position;
                    yield return Drag(touch, id + 2, fresh, fresh + new Vector2(75f, 35f));
                    Assert.That(context.Camera.transform.position, Is.Not.EqualTo(beforeFresh),
                        "Fresh Scene gesture must recover after " + order.Label + ".");
                    AssertRouterClean(router);
                    Assert.That(driver.IsEdgeAutoPanning, Is.False);
                    id += 3;
                }

                var finalStart = UiFreeCell(context,
                    new[] { new GridPosition(6, 6), new GridPosition(0, 6), new GridPosition(6, 1) });
                yield return BeginContact(touch, 220, finalStart);
                yield return BeginContact(touch, 221, finalStart + Vector2.right * 60f);
                context.Controller.enabled = false;
                yield return Cancel(touch, 221, finalStart + Vector2.right * 60f);
                yield return Cancel(touch, 220, finalStart);
                AssertRouterClean(router);
                Assert.That(driver.IsEdgeAutoPanning, Is.False);
                Assert.That(context.Controller.State, Is.EqualTo(DecorationSessionState.Closed));
                Assert.That(ActivePreview(context.Controller), Is.Null);
                Assert.That(context.StoreModal.IsOpen, Is.False);
                Assert.That(ActiveFootprintVisualCount(context), Is.Zero);
                Assert.That(ActiveBaseGridVisualCount(context), Is.Zero);
                Assert.That(Time.timeScale, Is.EqualTo(timeScaleBefore));
                context.Controller.enabled = true;
                yield return TapButton(touch, 222, context.HudButton);
                Assert.That(context.Controller.IsOpen, Is.True);
                var recovered = UiFreeCell(context,
                    new[] { new GridPosition(6, 6), new GridPosition(0, 6), new GridPosition(6, 1) });
                var beforeRecovered = context.Camera.transform.position;
                yield return Drag(touch, 223, recovered, recovered + new Vector2(-80f, 25f));
                Assert.That(context.Camera.transform.position, Is.Not.EqualTo(beforeRecovered));
                AssertRouterClean(router);
            }
            finally
            {
                CleanupTouchscreen(touch);
            }
        }

        [UnityTest]
        public IEnumerator MainCafeRealTouch_ExitAutoCancelsNewAndExistingPreviewAndFreshReentryWorks()
        {
            yield return LoadMainCafe();
            var newContext = CaptureMainCafe();
            var newTouch = AddTouchscreen();
            try
            {
                var initial = SnapshotLayout(newContext.Layout);
                yield return EnterByTouch(newContext, newTouch, 211);
                yield return SelectTileByTouch(newContext, newTouch, 212, 2);
                var newPreview = ActivePreview(newContext.Controller);
                var moved = FindValidFreeCell(
                    newContext.Layout, newPreview.DefinitionId, newPreview.ProposedRotation);
                yield return MovePreviewDirectlyThroughTouch(newContext, newTouch, 213, moved);
                Assert.That(ActivePreview(newContext.Controller), Is.Not.Null);
                yield return ContinueThenDiscardExitByTouch(newContext, newTouch, 214);
                Assert.That(SnapshotLayout(newContext.Layout), Is.EqualTo(initial));
                Assert.That(ActivePreview(newContext.Controller), Is.Null);
                Assert.That(ActiveFootprintVisualCount(newContext), Is.Zero);
                Assert.That(newContext.Layout.FurnitureInstances, Has.Count.EqualTo(1));
                Assert.That(newContext.Registry.TryGet(InitialInstanceId, out var restoredInitial), Is.True);
                Assert.That(restoredInitial.activeInHierarchy, Is.True);
                yield return TapButton(newTouch, 215, newContext.HudButton);
                Assert.That(newContext.Controller.IsOpen, Is.True);
                Assert.That(newContext.Catalogue.IsCatalogueVisible, Is.True);
                Assert.That(ActiveTiles(newContext.Catalogue), Has.Length.EqualTo(4));
            }
            finally
            {
                CleanupTouchscreen(newTouch);
            }

            yield return LoadMainCafe();
            var existingContext = CaptureMainCafe();
            var existingTouch = AddTouchscreen();
            try
            {
                var initial = SnapshotLayout(existingContext.Layout);
                Assert.That(existingContext.Registry.TryGet(InitialInstanceId, out var originalRepresentation), Is.True);
                var originalPosition = originalRepresentation.transform.position;
                var originalRotation = originalRepresentation.transform.rotation;
                yield return EnterByTouch(existingContext, existingTouch, 221);
                yield return SelectInitialCounter(existingContext, existingTouch, 222);
                var preview = ActivePreview(existingContext.Controller);
                var target = FindValidFreeCellDifferent(
                    existingContext.Layout, preview.DefinitionId, preview.ProposedRotation,
                    preview.ProposedPosition);
                yield return MovePreviewDirectlyThroughTouch(existingContext, existingTouch, 223, target);
                yield return TapButton(existingTouch, 224, FindNamed<Button>(existingContext.Scene, "RotateButton"));
                Assert.That(SnapshotLayout(existingContext.Layout), Is.EqualTo(initial),
                    "Existing edit remains transactional before exit.");
                yield return ContinueThenDiscardExitByTouch(existingContext, existingTouch, 225);
                Assert.That(SnapshotLayout(existingContext.Layout), Is.EqualTo(initial));
                Assert.That(existingContext.Controller.State, Is.EqualTo(DecorationSessionState.Closed));
                Assert.That(ActivePreview(existingContext.Controller), Is.Null);
                Assert.That(ActiveFootprintVisualCount(existingContext), Is.Zero);
                Assert.That(existingContext.Registry.TryGet(InitialInstanceId, out var restored), Is.True);
                Assert.That(restored.transform.position, Is.EqualTo(originalPosition));
                Assert.That(restored.transform.rotation, Is.EqualTo(originalRotation));

                yield return TapButton(existingTouch, 226, existingContext.HudButton);
                Assert.That(existingContext.Controller.IsOpen, Is.True);
                Assert.That(existingContext.Catalogue.IsCatalogueVisible, Is.True);
                Assert.That(existingContext.Catalogue.IsCollapsed, Is.False);
                Assert.That(ActiveTiles(existingContext.Catalogue), Has.Length.EqualTo(4));
                Assert.That(FindAll<DecorationModeController>(existingContext.Scene), Has.Length.EqualTo(1));
            }
            finally
            {
                CleanupTouchscreen(existingTouch);
            }
        }

        [UnityTest]
        public IEnumerator MainCafeRealTouch_ConfirmExitReenterPreservesLayoutRepresentationAndCatalogueAvailability()
        {
            yield return LoadMainCafe();
            var context = CaptureMainCafe();
            var touch = AddTouchscreen();
            try
            {
                yield return EnterByTouch(context, touch, 221);
                var definitionId = context.CatalogueAsset.Entries[0].Definition.DefinitionId;
                var idsBefore = context.Layout.FurnitureInstances.Select(item => item.InstanceId).ToHashSet();
                AssertNoCommerceContract(context);
                yield return SelectTileByTouch(context, touch, 222, 0);
                yield return TapButton(touch, 223, FindNamed<Button>(context.Scene, "ConfirmButton"));
                Assert.That(context.Layout.FurnitureInstances.Count(item => item.DefinitionId == definitionId), Is.EqualTo(2));
                Assert.That(context.Catalogue.IsCollapsed, Is.True);
                Assert.That(ActiveTiles(context.Catalogue), Is.Empty);
                yield return TapButton(touch, 2231, FindNamed<Button>(context.Scene, "CollapsedHandle"));
                Assert.That(ActiveTiles(context.Catalogue), Has.Length.EqualTo(4));
                var firstNew = context.Layout.FurnitureInstances.Single(item => !idsBefore.Contains(item.InstanceId));
                Assert.That(context.Registry.TryGet(firstNew.InstanceId, out var firstRepresentation), Is.True);
                var firstFormalPosition = firstRepresentation.transform.position;
                var firstFormalRotation = firstRepresentation.transform.rotation;

                yield return TapButton(touch, 224, context.HudButton);
                yield return TapButton(touch, 225, context.HudButton);
                yield return TapButton(touch, 226, FindNamed<Button>(context.Scene, "CollapseButton"));
                var handle = FindNamed<Button>(context.Scene, "CollapsedHandle");
                yield return TapButton(touch, 227, handle);
                Assert.That(context.Registry.TryGet(firstNew.InstanceId, out var persistedRepresentation), Is.True);
                Assert.That(persistedRepresentation.transform.position, Is.EqualTo(firstFormalPosition));
                Assert.That(persistedRepresentation.transform.rotation, Is.EqualTo(firstFormalRotation));
                yield return SelectTileByTouch(context, touch, 226, 0);
                var preview = ActivePreview(context.Controller);
                var candidate = FindValidFreeCell(context.Layout, preview.DefinitionId, preview.ProposedRotation);
                yield return MovePreviewDirectlyThroughTouch(context, touch, 228, candidate);
                yield return TapButton(touch, 229, FindNamed<Button>(context.Scene, "ConfirmButton"));
                var matching = context.Layout.FurnitureInstances.Where(item => item.DefinitionId == definitionId).ToArray();
                Assert.That(matching, Has.Length.EqualTo(3));
                Assert.That(matching.Select(item => item.InstanceId).Distinct().ToArray(), Has.Length.EqualTo(3));
                Assert.That(context.Catalogue.IsCollapsed, Is.True);
                Assert.That(ActiveTiles(context.Catalogue), Is.Empty);
                yield return TapButton(touch, 2291, FindNamed<Button>(context.Scene, "CollapsedHandle"));
                Assert.That(ActiveTiles(context.Catalogue), Has.Length.EqualTo(4));
                var placed = matching.Where(item => !idsBefore.Contains(item.InstanceId)).ToArray();
                Assert.That(placed, Has.Length.EqualTo(2));
                Assert.That(placed[0].InstanceId, Is.Not.EqualTo(placed[1].InstanceId));
                Assert.That(context.Registry.TryGet(placed[0].InstanceId, out _), Is.True);
                Assert.That(context.Registry.TryGet(placed[1].InstanceId, out _), Is.True);
                var firstCells = context.Layout.GetFurnitureFootprintCells(
                    placed[0].DefinitionId, placed[0].Position, placed[0].Rotation);
                var secondCells = context.Layout.GetFurnitureFootprintCells(
                    placed[1].DefinitionId, placed[1].Position, placed[1].Rotation);
                Assert.That(firstCells.Intersect(secondCells), Is.Empty,
                    "Repeated Catalogue placements must own independent non-overlapping footprints.");
                Assert.That(ActiveTiles(context.Catalogue).Single(
                    tile => tile.Definition.DefinitionId == definitionId).gameObject.activeInHierarchy, Is.True);
                AssertNoCommerceContract(context);
            }
            finally
            {
                CleanupTouchscreen(touch);
            }
        }

        [UnityTest]
        public IEnumerator MainCafeRealTouch_DistinctFurnitureBeganCancelsPriorPreviewAndReleaseCannotSwitchAgain()
        {
            yield return LoadMainCafe();
            var context = CaptureMainCafe();
            var touch = AddTouchscreen();
            try
            {
                yield return EnterByTouch(context, touch, 231);
                yield return SelectTileByTouch(context, touch, 232, 0);
                yield return TapButton(touch, 233, FindNamed<Button>(context.Scene, "ConfirmButton"));
                var ids = context.Layout.FurnitureInstances.Select(item => item.InstanceId).ToArray();
                Assert.That(ids, Has.Length.EqualTo(2));
                var layoutBeforeReplacement = SnapshotLayout(context.Layout);

                yield return SelectInitialCounter(context, touch, 234);
                var a = ActivePreview(context.Controller).SourceInstanceId;
                Assert.That(a, Is.EqualTo(InitialInstanceId));
                var other = context.Layout.FurnitureInstances.Single(item => item.InstanceId != a);
                var otherPoint = FormalFurnitureScreenPoint(context, other.InstanceId);
                AssertWorldPointIsUiFree(context.EventSystem, otherPoint);
                yield return Tap(touch, 235, otherPoint);
                var b = ActivePreview(context.Controller).SourceInstanceId;
                Assert.That(b, Is.EqualTo(other.InstanceId));
                Assert.That(context.Registry.TryGet(a, out var restoredA), Is.True);
                Assert.That(restoredA.activeInHierarchy, Is.True,
                    "A must restore when a distinct Furniture Began replaces its Preview.");
                Assert.That(context.Registry.TryGet(b, out var hiddenB), Is.True);
                Assert.That(hiddenB.activeInHierarchy, Is.False,
                    "Only B may be hidden behind the one active Preview.");
                Assert.That(FindAll<Transform>(context.Scene).Count(item =>
                    item.name.StartsWith("FurniturePreview_", StringComparison.Ordinal)
                    && item.gameObject.activeInHierarchy), Is.EqualTo(1));

                var releaseOverA = FormalFurnitureScreenPoint(context, a);
                yield return Drag(touch, 236, otherPoint, releaseOverA);
                Assert.That(ActivePreview(context.Controller).SourceInstanceId, Is.EqualTo(b));
                Assert.That(SnapshotLayout(context.Layout), Is.EqualTo(layoutBeforeReplacement));
            }
            finally
            {
                CleanupTouchscreen(touch);
            }
        }

        [UnityTest]
        public IEnumerator MainCafeRealTouch_RapidCatalogueActionModalTransitionsEndWithOneUsableOwnerAndNoDuplicateCallback()
        {
            yield return LoadMainCafe();
            var context = CaptureMainCafe();
            var touch = AddTouchscreen();
            var catalogueSelected = 0;
            var cancelRequested = 0;
            var storeRequested = 0;
            var modalDismissed = 0;
            var hudClicked = 0;
            var collapseClicked = 0;
            var expandClicked = 0;
            var enterStartedAt = -1f;
            var collapseStartedAt = -1f;
            var expandStartedAt = -1f;
            var firstSelectionStartedAt = -1f;
            var cancelStartedAt = -1f;
            var secondSelectionStartedAt = -1f;
            var existingEditStartedAt = -1f;
            var modalStartedAt = -1f;
            var modalDismissStartedAt = -1f;
            var exitStartedAt = -1f;
            Action<FurnitureDefinitionAsset> onSelected = _ =>
            {
                catalogueSelected++;
                if (catalogueSelected == 1) firstSelectionStartedAt = Time.unscaledTime;
                else if (catalogueSelected == 2) secondSelectionStartedAt = Time.unscaledTime;
            };
            Action onCancel = () =>
            {
                cancelRequested++;
                cancelStartedAt = Time.unscaledTime;
            };
            Action onStore = () =>
            {
                storeRequested++;
                modalStartedAt = Time.unscaledTime;
            };
            Action onDismiss = () =>
            {
                modalDismissed++;
                modalDismissStartedAt = Time.unscaledTime;
            };
            UnityEngine.Events.UnityAction onHud = () =>
            {
                hudClicked++;
                if (hudClicked == 1) enterStartedAt = Time.unscaledTime;
                else if (hudClicked == 2) exitStartedAt = Time.unscaledTime;
            };
            UnityEngine.Events.UnityAction onCollapse = () =>
            {
                collapseClicked++;
                collapseStartedAt = Time.unscaledTime;
            };
            UnityEngine.Events.UnityAction onExpand = () =>
            {
                expandClicked++;
                expandStartedAt = Time.unscaledTime;
            };
            context.Catalogue.Selected += onSelected;
            context.ActionBar.CancelRequested += onCancel;
            context.ActionBar.StoreRequested += onStore;
            context.StoreModal.DismissRequested += onDismiss;
            var collapse = FindNamed<Button>(context.Scene, "CollapseButton");
            var expand = FindNamed<Button>(context.Scene, "CollapsedHandle");
            context.HudButton.onClick.AddListener(onHud);
            collapse.onClick.AddListener(onCollapse);
            expand.onClick.AddListener(onExpand);
            try
            {
                var catalogueGroup = ReadPrivate<CanvasGroup>(context.Catalogue, "canvasGroup");
                var actionGroup = ReadPrivate<CanvasGroup>(context.ActionBar, "canvasGroup");
                var modalGroup = ReadPrivate<CanvasGroup>(context.StoreModal, "canvasGroup");

                yield return TapButton(touch, 241, context.HudButton);
                Assert.That(context.Controller.IsOpen, Is.True);
                Assert.That(enterStartedAt, Is.GreaterThanOrEqualTo(0f));
                yield return TapButton(touch, 242, collapse);
                AssertTransitionCommandTiming(
                    enterStartedAt, collapseStartedAt, 0.16f, "enter -> collapse");
                Assert.That(context.Catalogue.IsCollapsed, Is.True);

                yield return TapButton(touch, 243, expand);
                AssertTransitionCommandTiming(
                    collapseStartedAt, expandStartedAt, 0.16f, "collapse -> expand");
                Assert.That(context.Catalogue.IsCollapsed, Is.False);

                var firstTile = ActiveTiles(context.Catalogue)[0].GetComponent<Button>();
                yield return TapButton(touch, 244, firstTile);
                AssertTransitionCommandTiming(
                    expandStartedAt, firstSelectionStartedAt, 0.16f, "expand -> select");
                Assert.That(catalogueSelected, Is.EqualTo(1));
                Assert.That(ActivePreviewObjectCount(context), Is.EqualTo(1));
                var cancel = FindNamed<Button>(context.Scene, "CancelButton");
                AssertTopButton(context.EventSystem, cancel);
                yield return TapButton(touch, 245, cancel);
                AssertTransitionCommandTiming(
                    firstSelectionStartedAt, cancelStartedAt, 0.12f, "select -> Cancel");
                Assert.That(cancelRequested, Is.EqualTo(1));
                Assert.That(ActivePreviewObjectCount(context), Is.Zero);

                firstTile = ActiveTiles(context.Catalogue)[0].GetComponent<Button>();
                yield return TapButton(touch, 246, firstTile);
                AssertTransitionCommandTiming(
                    cancelStartedAt, secondSelectionStartedAt, 0.16f, "Cancel -> select again");
                Assert.That(catalogueSelected, Is.EqualTo(2));
                Assert.That(ActivePreviewObjectCount(context), Is.EqualTo(1));

                var initialPoint = FormalFurnitureScreenPoint(context, InitialInstanceId);
                AssertWorldPointIsUiFree(context.EventSystem, initialPoint);
                existingEditStartedAt = Time.unscaledTime;
                yield return Tap(touch, 247, initialPoint);
                Assert.That(ActivePreview(context.Controller).SourceInstanceId,
                    Is.EqualTo(InitialInstanceId),
                    "The second new Preview must switch through the formal existing-Furniture route.");
                Assert.That(ActivePreviewObjectCount(context), Is.EqualTo(1));
                yield return TapButton(touch, 248, FindNamed<Button>(context.Scene, "StoreButton"));
                AssertTransitionCommandTiming(
                    existingEditStartedAt, modalStartedAt, 0.12f, "existing edit -> Store");
                Assert.That(storeRequested, Is.EqualTo(1));
                Assert.That(context.StoreModal.IsOpen, Is.True,
                    "The Store callback inside the Action transition must open the real Modal once.");
                var modalCancel = context.StoreModal.transform.Find("SafeArea/Content/CancelButton").GetComponent<Button>();
                AssertTopButton(context.EventSystem, modalCancel);
                yield return TapButton(touch, 249, modalCancel);
                AssertTransitionCommandTiming(
                    modalStartedAt, modalDismissStartedAt, 0.16f, "Modal open -> dismiss");
                Assert.That(modalDismissed, Is.EqualTo(1));

                yield return ContinueThenDiscardExitByTouch(context, touch, 250);
                AssertTransitionCommandTiming(
                    modalDismissStartedAt, exitStartedAt, 0.16f, "Modal dismiss -> exit");
                yield return WaitUntil(
                    () => context.Controller.State == DecorationSessionState.Closed
                        && !context.Controller.IsOpen
                        && Time.unscaledTime - exitStartedAt > 0.20f
                        && Mathf.Approximately(catalogueGroup.alpha, 0f)
                        && Mathf.Approximately(actionGroup.alpha, 0f)
                        && Mathf.Approximately(modalGroup.alpha, 0f)
                        && CountActiveRaycastOwners(context) == 0
                        && IsTopButton(context.EventSystem, context.HudButton),
                    2f,
                    "Interrupted exit did not settle to the one usable HUD owner with no Decoration raycast owner.");
                yield return null;
                Assert.That(modalGroup.alpha, Is.EqualTo(0f).Within(0.001f));
                Assert.That(actionGroup.alpha, Is.EqualTo(0f).Within(0.001f));
                Assert.That(catalogueGroup.alpha, Is.EqualTo(0f).Within(0.001f));
                Assert.That(ActivePreviewObjectCount(context), Is.Zero);
                Assert.That(context.StoreModal.IsOpen, Is.False);
                Assert.That(FindAll<EventSystem>(context.Scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<DecorationModeController>(context.Scene), Has.Length.EqualTo(1));
                Assert.That(CountActiveRaycastOwners(context), Is.Zero);
                AssertTopButton(context.EventSystem, context.HudButton);
                Assert.That(ActivePreview(context.Controller), Is.Null);
                Assert.That(catalogueSelected, Is.EqualTo(2));
                Assert.That(cancelRequested, Is.EqualTo(1));
                Assert.That(storeRequested, Is.EqualTo(1));
                Assert.That(modalDismissed, Is.EqualTo(1));
                Assert.That(hudClicked, Is.EqualTo(3));
                Assert.That(collapseClicked, Is.EqualTo(1));
                Assert.That(expandClicked, Is.EqualTo(1));
                AssertPointerBoundaryClean(ReadPrivate<UiPointerBoundary>(context.Controller, "pointerBoundary"));
            }
            finally
            {
                context.Catalogue.Selected -= onSelected;
                context.ActionBar.CancelRequested -= onCancel;
                context.ActionBar.StoreRequested -= onStore;
                context.StoreModal.DismissRequested -= onDismiss;
                context.HudButton.onClick.RemoveListener(onHud);
                collapse.onClick.RemoveListener(onCollapse);
                expand.onClick.RemoveListener(onExpand);
                CleanupTouchscreen(touch);
            }
        }

        [UnityTest]
        public IEnumerator MainCafeRealTouch_RawTouchIdIsNotForwardedAsCompositeUiPointerId()
        {
            yield return LoadMainCafe();
            var context = CaptureMainCafe();
            var touch = AddTouchscreen();
            try
            {
                yield return EnterByTouch(context, touch, 2701);
                var boundary = ReadPrivate<UiPointerBoundary>(context.Controller, "pointerBoundary");
                var collapse = FindNamed<Button>(context.Scene, "CollapseButton");
                var recorder = collapse.gameObject.AddComponent<PointerRecorder>();
                var collapseActions = 0;
                UnityEngine.Events.UnityAction countCollapseAction = () => collapseActions++;
                collapse.onClick.AddListener(countCollapseAction);
                const int rawId = 2719;
                try
                {
                    var collapseCenter = ButtonCenter(collapse);
                    yield return BeginUiContact(touch, rawId, collapseCenter);
                    Assert.That(recorder.DownCount, Is.EqualTo(1));
                    AssertRecorderNamespace(recorder, rawId);
                    foreach (var composite in recorder.CompositePointerIds.Distinct())
                    {
                        Assert.That(boundary.GetOwnership(composite), Is.EqualTo(UiPointerOwnership.Ui),
                            "The completed real UI Began frame must register the composite pointer as UI.");
                    }
                    yield return ReleaseUiContact(touch, rawId, collapseCenter);
                    Assert.That(recorder.UpCount, Is.EqualTo(1));
                    Assert.That(recorder.ClickCount, Is.EqualTo(1));
                    AssertRecorderNamespace(recorder, rawId);
                    Assert.That(recorder.Events, Is.EqualTo(new[] { "Down", "Up", "Click" }));
                    Assert.That(collapseActions, Is.EqualTo(1));
                    Assert.That(context.Catalogue.IsCollapsed, Is.True,
                        "The configured Phase 6 Catalogue action must fire exactly once.");
                    foreach (var composite in recorder.CompositePointerIds.Distinct())
                    {
                        Assert.That(boundary.GetOwnership(composite), Is.EqualTo(UiPointerOwnership.None),
                            "UI pointer ownership must be released when the real Touch ends.");
                    }

                    var collapsedHandle = FindNamed<Button>(context.Scene, "CollapsedHandle");
                    yield return WaitUntil(() => IsTopButton(context.EventSystem, collapsedHandle), 2f,
                        "CollapsedHandle did not become the actionable Phase 6 UI target.");
                    var movingRecorder = collapsedHandle.gameObject.AddComponent<PointerRecorder>();
                    var expandActions = 0;
                    UnityEngine.Events.UnityAction countExpandAction = () => expandActions++;
                    collapsedHandle.onClick.AddListener(countExpandAction);
                    const int movingRawId = 8871;
                    try
                    {
                        var handleCenter = ButtonCenter(collapsedHandle);
                        yield return BeginUiContact(touch, movingRawId, handleCenter);
                        var movingCompositeId = movingRecorder.CompositePointerIds.Single();
                        Assert.That(boundary.GetOwnership(movingCompositeId), Is.EqualTo(UiPointerOwnership.Ui),
                            "A moving real UI press must own its composite pointer throughout the contact.");
                        yield return MoveContact(touch, movingRawId, handleCenter + Vector2.right * 140f);
                        yield return ReleaseUiContact(touch, movingRawId, handleCenter + Vector2.right * 140f);
                        Assert.That(movingRecorder.BeginDragCount, Is.GreaterThanOrEqualTo(1));
                        Assert.That(movingRecorder.DragCount, Is.GreaterThanOrEqualTo(1));
                        Assert.That(movingRecorder.EndDragCount, Is.EqualTo(1));
                        AssertRecorderNamespace(movingRecorder, movingRawId);
                        AssertRecorderOrdering(movingRecorder);
                        Assert.That(expandActions, Is.Zero,
                            "A moving press must not fire the Catalogue expand action.");
                        Assert.That(context.Catalogue.IsCollapsed, Is.True);
                        foreach (var composite in movingRecorder.CompositePointerIds.Distinct())
                        {
                            Assert.That(boundary.GetOwnership(composite), Is.EqualTo(UiPointerOwnership.None));
                        }
                    }
                    finally
                    {
                        collapsedHandle.onClick.RemoveListener(countExpandAction);
                    }
                    AssertPointerBoundaryClean(boundary);
                }
                finally
                {
                    collapse.onClick.RemoveListener(countCollapseAction);
                }
            }
            finally
            {
                CleanupTouchscreen(touch);
            }
        }

        [UnityTest]
        public IEnumerator MainCafeRealTouch_TimeScaleZeroCatalogueActionsAndStoreModalRemainInteractive()
        {
            yield return LoadMainCafe();
            var context = CaptureMainCafe();
            var touch = AddTouchscreen();
            var selected = 0;
            var rotated = 0;
            var canceled = 0;
            var stored = 0;
            var dismissed = 0;
            Action<FurnitureDefinitionAsset> onSelected = _ => selected++;
            Action onRotated = () => rotated++;
            Action onCanceled = () => canceled++;
            Action onStored = () => stored++;
            Action onDismissed = () => dismissed++;
            context.Catalogue.Selected += onSelected;
            context.ActionBar.RotateRequested += onRotated;
            context.ActionBar.CancelRequested += onCanceled;
            context.ActionBar.StoreRequested += onStored;
            context.StoreModal.DismissRequested += onDismissed;
            try
            {
                yield return EnterByTouch(context, touch, 251);
                Assert.That(Time.timeScale, Is.Zero);
                yield return SelectTileByTouch(context, touch, 252, 0);
                Assert.That(selected, Is.EqualTo(1));
                var rotation = ActivePreview(context.Controller).ProposedRotation;
                yield return TapButton(touch, 253, FindNamed<Button>(context.Scene, "RotateButton"));
                Assert.That(rotated, Is.EqualTo(1));
                Assert.That(ActivePreview(context.Controller).ProposedRotation, Is.Not.EqualTo(rotation));
                yield return TapButton(touch, 254, FindNamed<Button>(context.Scene, "CancelButton"));
                Assert.That(canceled, Is.EqualTo(1));
                yield return SelectInitialCounter(context, touch, 255);
                yield return TapButton(touch, 256, FindNamed<Button>(context.Scene, "StoreButton"));
                Assert.That(stored, Is.EqualTo(1));
                Assert.That(context.StoreModal.IsOpen, Is.True);
                var cancel = context.StoreModal.transform.Find("SafeArea/Content/CancelButton").GetComponent<Button>();
                yield return TapButton(touch, 257, cancel);
                Assert.That(dismissed, Is.EqualTo(1));
                Assert.That(context.Controller.State, Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
                Assert.That(Time.timeScale, Is.Zero);
                Assert.That(selected, Is.EqualTo(1));
                Assert.That(rotated, Is.EqualTo(1));
                Assert.That(canceled, Is.EqualTo(1));
                Assert.That(stored, Is.EqualTo(1));
                Assert.That(dismissed, Is.EqualTo(1));
            }
            finally
            {
                context.Catalogue.Selected -= onSelected;
                context.ActionBar.RotateRequested -= onRotated;
                context.ActionBar.CancelRequested -= onCanceled;
                context.ActionBar.StoreRequested -= onStored;
                context.StoreModal.DismissRequested -= onDismissed;
                CleanupTouchscreen(touch);
            }
        }

        [UnityTest]
        public IEnumerator MainCafeRealTouch_InvalidPreviewDisablesConfirmShowsSpecificNonColorFeedbackAndCancelCleans()
        {
            var branches = new[]
            {
                (Reason: PlacementFailureReason.Overlap, Copy: "Space already occupied", Interruption: "Exit"),
                (Reason: PlacementFailureReason.ReservedEntranceClearance, Copy: "Keep the entrance clear", Interruption: "Disable")
            };
            var branch = 0;
            foreach (var invalidCase in branches)
            {
                yield return LoadMainCafe();
                var context = CaptureMainCafe();
                var touch = AddTouchscreen();
                try
                {
                    var id = 261 + branch * 10;
                    yield return EnterByTouch(context, touch, id);
                    yield return SelectTileByTouch(context, touch, id + 1, 3);
                    var initialLayout = SnapshotLayout(context.Layout);
                    var preview = ActivePreview(context.Controller);
                    var target = FindUiReachableCellWithFailure(
                        context,
                        preview.DefinitionId,
                        preview.ProposedRotation,
                        invalidCase.Reason,
                        preview.ProposedPosition);
                    yield return MovePreviewDirectlyThroughTouch(context, touch, id + 2, target);
                    var confirm = FindNamed<Button>(context.Scene, "ConfirmButton");
                    var feedback = context.ActionBar.transform.Find("FeedbackToast/Message").GetComponent<TMP_Text>();
                    var stateShape = FindNamed<Transform>(context.Scene, "StateShape").gameObject;
                    var invalid = ActivePreview(context.Controller);
                    Assert.That(invalid.ProposedPosition, Is.EqualTo(target),
                        $"{invalidCase.Interruption}: requested invalid cell " +
                        $"({target.X},{target.Y}), but Touch settled at " +
                        $"({invalid.ProposedPosition.X},{invalid.ProposedPosition.Y}).");
                    Assert.That(invalid.PlacementResult.Succeeded, Is.False, invalidCase.Interruption);
                    Assert.That(invalid.PlacementResult.FailureReason, Is.EqualTo(invalidCase.Reason));
                    Assert.That(confirm.interactable, Is.False);
                    Assert.That(feedback.text, Is.EqualTo(invalidCase.Copy));
                    Assert.That(stateShape.activeInHierarchy, Is.True);
                    var footprint = context.Layout.GetFurnitureFootprintCells(
                        invalid.DefinitionId, invalid.ProposedPosition, invalid.ProposedRotation);
                    Assert.That(footprint, Has.Count.EqualTo(6),
                        "The 2x3 acceptance preset must retain its full six-cell footprint.");
                    Assert.That(ActiveFootprintVisualCount(context), Is.EqualTo(footprint.Count));
                    Assert.That(ActiveInvalidGeometryCount(context), Is.GreaterThanOrEqualTo(footprint.Count));
                    yield return TapButton(touch, id + 3, confirm);
                    Assert.That(SnapshotLayout(context.Layout), Is.EqualTo(initialLayout));
                    Assert.That(ActivePreview(context.Controller), Is.Not.Null,
                        "Disabled Confirm Touch must not mutate or close the invalid Preview.");

                    if (invalidCase.Interruption == "Cancel")
                    {
                        yield return TapButton(touch, id + 4, FindNamed<Button>(context.Scene, "CancelButton"));
                    }
                    else if (invalidCase.Interruption == "Exit")
                    {
                        yield return ContinueThenDiscardExitByTouch(context, touch, id + 4);
                    }
                    else
                    {
                        context.Controller.enabled = false;
                        yield return null;
                    }

                    Assert.That(ActivePreview(context.Controller), Is.Null, invalidCase.Interruption);
                    Assert.That(SnapshotLayout(context.Layout), Is.EqualTo(initialLayout), invalidCase.Interruption);
                    Assert.That(ActiveFootprintVisualCount(context), Is.Zero, invalidCase.Interruption);
                    Assert.That(context.StoreModal.IsOpen, Is.False, invalidCase.Interruption);
                    var router = ReadPrivate<DecorationTouchRouter>(context.Controller, "touchRouter");
                    if (invalidCase.Interruption != "Cancel")
                    {
                        Assert.That(router, Is.Null,
                            "Exit or controller disable must dispose the owned Touch router.");
                    }
                    else
                    {
                        AssertRouterClean(router);
                    }
                    Assert.That(ReadPrivate<DecorationCameraDriver>(context.Controller, "cameraDriver")
                        .IsEdgeAutoPanning, Is.False);
                    AssertPointerBoundaryClean(ReadPrivate<UiPointerBoundary>(context.Controller, "pointerBoundary"));
                    Assert.That(context.Registry.TryGet(InitialInstanceId, out var formal), Is.True);
                    Assert.That(formal.activeInHierarchy, Is.True);
                }
                finally
                {
                    CleanupTouchscreen(touch);
                }
                branch++;
            }
        }

        [UnityTest]
        public IEnumerator MainCafeActiveResolution_CanvasScalerWorldCornersStayOnScreenAndActualEventSystemRaycastsEssentialControls()
        {
            yield return LoadMainCafe();
            var context = CaptureMainCafe();
            var touch = AddTouchscreen();
            try
            {
                var screenCanvas = FindAll<Canvas>(context.Scene).Single(canvas => canvas.name == "Screen Canvas");
                var scaler = screenCanvas.GetComponent<CanvasScaler>();
                Assert.That(scaler.uiScaleMode, Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1080f, 1920f)));
                Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));
                Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(FindAll<InputSystemUIInputModule>(context.Scene)
                    .Count(module => module.isActiveAndEnabled), Is.EqualTo(1));
                Assert.That(FindAll<GraphicRaycaster>(context.Scene)
                    .Count(raycaster => raycaster.isActiveAndEnabled), Is.GreaterThanOrEqualTo(1));

                yield return EnterByTouch(context, touch, 271);
                var catalogueControls = ActiveTiles(context.Catalogue).Select(tile => tile.GetComponent<Button>())
                    .Concat(new[] { FindNamed<Button>(context.Scene, "CollapseButton") })
                    .ToArray();
                foreach (var button in catalogueControls)
                {
                    AssertEssentialButton(context.EventSystem, button);
                }

                Assert.That(FindUiFreeLogicalArea(context.EventSystem), Is.True,
                    "Visible UI must leave a non-empty logical Scene complement.");

                yield return SelectTileByTouch(context, touch, 272, 0);
                var newActionControls = new[]
                {
                    FindNamed<Button>(context.Scene, "RotateButton"),
                    FindNamed<Button>(context.Scene, "CancelButton"),
                    FindNamed<Button>(context.Scene, "ConfirmButton")
                };
                foreach (var button in newActionControls) AssertEssentialButton(context.EventSystem, button);
                yield return TapButton(touch, 273, FindNamed<Button>(context.Scene, "CancelButton"));

                yield return SelectInitialCounter(context, touch, 274);
                var existingActionControls = new[]
                {
                    FindNamed<Button>(context.Scene, "StoreButton"),
                    FindNamed<Button>(context.Scene, "RotateButton"),
                    FindNamed<Button>(context.Scene, "CancelButton"),
                    FindNamed<Button>(context.Scene, "ConfirmButton")
                };
                foreach (var button in existingActionControls) AssertEssentialButton(context.EventSystem, button);

                yield return TapButton(touch, 275, FindNamed<Button>(context.Scene, "StoreButton"));
                var modalControls = new[]
                {
                    context.StoreModal.transform.Find("SafeArea/Content/StoreButton").GetComponent<Button>(),
                    context.StoreModal.transform.Find("SafeArea/Content/CancelButton").GetComponent<Button>()
                };
                foreach (var button in modalControls) AssertEssentialButton(context.EventSystem, button);
                var modalBlocker = FindNamed<Button>(context.Scene, "ModalBlocker");
                Assert.That(IsTopButton(context.EventSystem, modalBlocker), Is.True,
                    "The full-screen ModalBlocker must remain the real top dismiss target outside modal content.");
                yield return TapButton(touch, 276, modalControls[1]);
            }
            finally
            {
                CleanupTouchscreen(touch);
            }
        }

        [UnityTest]
        public IEnumerator CompositeOrder_Phase5ProductionTouchThenMainCafeDecorationTouchRestoresRuntimeIsolation()
        {
            yield return ExerciseCompositeOrder(phase5First: true);
        }

        [UnityTest]
        public IEnumerator CompositeOrder_MainCafeDecorationTouchThenPhase5ProductionTouchRestoresRuntimeIsolation()
        {
            yield return ExerciseCompositeOrder(phase5First: false);
        }

        private IEnumerator ExerciseCompositeOrder(bool phase5First)
        {
            Mouse mouse = null;
            Keyboard keyboard = null;
            var touch = AddTouchscreen();
            try
            {
                mouse = InputSystem.AddDevice<Mouse>();
                keyboard = InputSystem.AddDevice<Keyboard>();
                mouse.MakeCurrent();
                keyboard.MakeCurrent();
                var expectedMouse = Mouse.current;
                var expectedKeyboard = Keyboard.current;
                var enhancedBefore = EnhancedTouchSupport.enabled;

                if (phase5First)
                {
                    yield return ExercisePhase5Route(touch, 301);
                    AssertCompositeBoundary(touch, mouse, keyboard, expectedMouse, expectedKeyboard,
                        enhancedBefore, timeScaleBefore);
                    yield return ExerciseMainCafeRoute(touch, 310);
                }
                else
                {
                    yield return ExerciseMainCafeRoute(touch, 320);
                    AssertCompositeBoundary(touch, mouse, keyboard, expectedMouse, expectedKeyboard,
                        enhancedBefore, timeScaleBefore);
                    yield return ExercisePhase5Route(touch, 330);
                }

                AssertCompositeBoundary(touch, mouse, keyboard, expectedMouse, expectedKeyboard,
                    enhancedBefore, timeScaleBefore);
                Assert.That(Time.timeScale, Is.EqualTo(timeScaleBefore));
                Assert.That(FindAllInFixtureScenes<DecorationModeController>().Any(item => item.IsOpen), Is.False);
            }
            finally
            {
                CleanupTouchscreen(touch);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            }
        }

        private IEnumerator ExercisePhase5Route(Touchscreen touch, int id)
        {
            yield return LoadFixtureScene(Phase5Path);
            var scene = SceneManager.GetActiveScene();
            var eventSystem = FindAll<EventSystem>(scene).Single();
            Assert.That(eventSystem.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
            var selector = FindNamed<Button>(scene, "Feedback Page Selector");
            yield return TapButton(touch, id, selector);
            var toastButton = FindNamed<Button>(scene, "Show Toast Button");
            yield return TapButton(touch, id + 1, toastButton);
            Assert.That(FindAll<ToastView>(scene).Single().GetComponentInChildren<TMP_Text>(true).text,
                Does.Contain("Saved"));
        }

        private IEnumerator ExerciseMainCafeRoute(Touchscreen touch, int id)
        {
            yield return LoadMainCafe();
            var context = CaptureMainCafe();
            try
            {
                yield return EnterByTouch(context, touch, id);
                yield return TapButton(touch, id + 1, context.HudButton);
                Assert.That(context.Controller.IsOpen, Is.False);
                Assert.That(Time.timeScale, Is.EqualTo(timeScaleBefore));
            }
            finally
            {
                context.Controller.enabled = false;
                foreach (var source in FindAll<InputSystemDecorationTouchSource>(context.Scene))
                {
                    source.enabled = false;
                }
            }
        }

        private void AssertCompositeBoundary(
            Touchscreen touch,
            Mouse mouse,
            Keyboard keyboard,
            Mouse expectedMouse,
            Keyboard expectedKeyboard,
            bool expectedEnhancedTouch,
            float expectedTimeScale)
        {
            Assert.That(touch.added, Is.True);
            Assert.That(InputSystem.devices.OfType<Touchscreen>().Count(device => ReferenceEquals(device, touch)),
                Is.EqualTo(1));
            Assert.That(mouse.added, Is.True);
            Assert.That(keyboard.added, Is.True);
            Assert.That(Mouse.current, Is.SameAs(expectedMouse));
            Assert.That(Keyboard.current, Is.SameAs(expectedKeyboard));
            Assert.That(EnhancedTouchSupport.enabled, Is.EqualTo(expectedEnhancedTouch));
            var activeScene = SceneManager.GetActiveScene();
            Assert.That(fixtureSceneHandles.Contains(activeScene.handle), Is.True,
                "The active route Scene must be fixture-owned.");
            Assert.That(FindAll<EventSystem>(activeScene).Count(item => item.isActiveAndEnabled), Is.EqualTo(1));
            Assert.That(FindAll<InputSystemUIInputModule>(activeScene).Count(item => item.isActiveAndEnabled), Is.EqualTo(1));
            Assert.That(FindAll<GraphicRaycaster>(activeScene).Where(item => item.isActiveAndEnabled), Is.Not.Empty);
            Assert.That(Time.timeScale, Is.EqualTo(expectedTimeScale));
            foreach (var controller in FindAllInFixtureScenes<DecorationModeController>())
            {
                Assert.That(controller.gameObject.scene, Is.EqualTo(activeScene));
                Assert.That(controller.State, Is.EqualTo(DecorationSessionState.Closed));
                Assert.That(ActivePreview(controller), Is.Null);
                Assert.That(ReadPrivate<DecorationStoreModalView>(controller, "storeModalView").IsOpen, Is.False);
                Assert.That(ReadPrivate<DecorationCameraDriver>(controller, "cameraDriver").IsEdgeAutoPanning, Is.False);
                AssertPointerBoundaryClean(ReadPrivate<UiPointerBoundary>(controller, "pointerBoundary"));
            }
        }

        private IEnumerator LoadMainCafe()
        {
            yield return LoadFixtureScene(MainCafePath);
            yield return WaitUntil(
                () => FindAll<CafeLayoutRuntime>(SceneManager.GetActiveScene())
                    .Any(runtime => runtime.Layout != null),
                2f,
                "MainCafe runtime bootstrap did not complete.");
            Canvas.ForceUpdateCanvases();
        }

        private IEnumerator LoadFixtureScene(string path)
        {
            if (cleanupScene.IsValid() && cleanupScene.isLoaded
                && SceneManager.GetActiveScene().handle != cleanupScene.handle)
                Assert.That(SceneManager.SetActiveScene(cleanupScene), Is.True);
            foreach (var scene in LoadedScenes().Where(scene => fixtureSceneHandles.Contains(scene.handle)).ToArray())
            {
                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                    yield return null;
                fixtureSceneHandles.Remove(scene.handle);
            }

            EditorSceneManager.LoadSceneInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var loadedTarget = LoadedScenes().SingleOrDefault(scene => scene.path == path);
            Assert.That(loadedTarget.IsValid() && loadedTarget.isLoaded, Is.True,
                "Fixture target Scene was not loaded through the required Single route: " + path);
            fixtureSceneHandles.Add(loadedTarget.handle);
            cleanupScene = SceneManager.CreateScene(cleanupSceneName);
            Assert.That(cleanupScene.IsValid() && cleanupScene.isLoaded, Is.True,
                "Single production load must be followed by a fresh empty cleanup Scene boundary.");
            yield return null;
            loadedTarget = LoadedScenes().SingleOrDefault(scene => scene.path == path);
            Assert.That(loadedTarget.IsValid() && loadedTarget.isLoaded, Is.True,
                "Production Scene identity must remain loaded after the cleanup boundary is created.");
            if (SceneManager.GetActiveScene().handle != loadedTarget.handle)
            {
                Assert.That(SceneManager.SetActiveScene(loadedTarget), Is.True,
                    "Fixture target Scene could not become active: " + path);
            }
            AssertProductionSceneIsolation(loadedTarget, path);
        }

        private void AssertProductionSceneIsolation(Scene productionScene, string expectedPath)
        {
            var loaded = LoadedScenes();
            Assert.That(loaded, Has.Length.EqualTo(2),
                "Only the expected production Scene and the empty Task 9 cleanup Scene may be loaded.");
            Assert.That(loaded.Count(scene => scene.handle == productionScene.handle), Is.EqualTo(1));
            Assert.That(productionScene.path, Is.EqualTo(expectedPath));
            Assert.That(loaded.Count(scene => scene.handle == cleanupScene.handle), Is.EqualTo(1));
            Assert.That(cleanupScene.path, Is.Empty,
                "The Task 9 cleanup boundary must be a test-created unsaved Scene.");
            Assert.That(cleanupScene.rootCount, Is.Zero,
                "The cleanup Scene must remain empty while the production route executes.");
            Assert.That(FindAll<EventSystem>(cleanupScene), Is.Empty,
                "No external EventSystem may survive beside the production Scene.");
            Assert.That(FindAll<UnityEngine.Camera>(cleanupScene), Is.Empty,
                "No external Camera may survive beside the production Scene.");
            Assert.That(FindAll<Collider>(cleanupScene), Is.Empty,
                "No external physics Collider root may survive beside the production Scene.");
            Assert.That(FindAll<Rigidbody>(cleanupScene), Is.Empty,
                "No external physics Rigidbody root may survive beside the production Scene.");
        }

        private IEnumerator EnterByTouch(MainCafeContext context, Touchscreen touch, int id)
        {
            yield return TapButton(touch, id, context.HudButton);
            yield return WaitUntil(() => context.Controller.IsOpen, 2f, "Decoration HUD Touch did not enter.");
            yield return WaitUntil(
                () => CatalogueExpandedAndSettled(context.Catalogue)
                    && ActiveTiles(context.Catalogue).Length == 4
                    && IsTopButton(context.EventSystem,
                        ActiveTiles(context.Catalogue)[0].GetComponent<Button>()),
                2f,
                "Entered Catalogue did not expose an actionable tile.");
        }

        private static bool CatalogueExpandedAndSettled(DecorationCatalogueView catalogue)
        {
            if (catalogue == null || !catalogue.IsCatalogueVisible || catalogue.IsCollapsed
                || catalogue.transform is not RectTransform rect)
            {
                return false;
            }

            var target = ReadPrivate<Vector2>(catalogue, "expandedAnchoredPosition");
            return Vector2.SqrMagnitude(rect.anchoredPosition - target) <= 0.01f;
        }

        private static bool CatalogueCollapsedAndSettled(DecorationCatalogueView catalogue)
        {
            if (catalogue == null || !catalogue.IsCatalogueVisible || !catalogue.IsCollapsed
                || catalogue.transform is not RectTransform rect)
            {
                return false;
            }

            var target = ReadPrivate<Vector2>(catalogue, "collapsedAnchoredPosition");
            return Vector2.SqrMagnitude(rect.anchoredPosition - target) <= 0.01f;
        }

        private IEnumerator ContinueThenDiscardExitByTouch(
            MainCafeContext context,
            Touchscreen touch,
            int firstId)
        {
            var previewBefore = ActivePreview(context.Controller);
            Assert.That(previewBefore, Is.Not.Null,
                "The Phase 7 Exit Modal contract applies only while a Preview is active.");
            yield return TapButton(touch, firstId, context.HudButton);
            var continueButton = FindNamed<Button>(context.Scene, "ContinueEditingButton");
            var discardButton = FindNamed<Button>(context.Scene, "DiscardChangesButton");
            yield return WaitUntil(() => continueButton.gameObject.activeInHierarchy
                    && IsTopButton(context.EventSystem, continueButton),
                2f, "Continue Editing did not become the real top Modal action.");
            Assert.That(context.Controller.IsOpen, Is.True);
            Assert.That(ActivePreview(context.Controller), Is.SameAs(previewBefore),
                "Opening the Exit Modal must not auto-confirm or silently discard.");

            yield return TapButton(touch, firstId + 1, continueButton);
            Assert.That(context.Controller.IsOpen, Is.True);
            Assert.That(ActivePreview(context.Controller), Is.SameAs(previewBefore),
                "Continue Editing must preserve the live Preview.");
            yield return WaitUntil(() => !continueButton.gameObject.activeInHierarchy,
                2f, "Continue Editing did not close the Exit Modal.");

            yield return TapButton(touch, firstId + 2, context.HudButton);
            yield return WaitUntil(() => discardButton.gameObject.activeInHierarchy
                    && IsTopButton(context.EventSystem, discardButton),
                2f, "Discard Changes did not become the real top Modal action.");
            Assert.That(ActivePreview(context.Controller), Is.SameAs(previewBefore));
            yield return TapButton(touch, firstId + 3, discardButton);
            yield return WaitUntil(() => !context.Controller.IsOpen
                    && context.Controller.State == DecorationSessionState.Closed
                    && ActivePreview(context.Controller) == null,
                2f, "Discard Changes did not rollback the Preview and exit Decoration Mode.");
        }

        private IEnumerator SelectTileByTouch(MainCafeContext context, Touchscreen touch, int id, int index)
        {
            var tiles = ActiveTiles(context.Catalogue);
            Assert.That(tiles, Has.Length.EqualTo(4));
            var button = tiles[index].GetComponent<Button>();
            yield return WaitUntil(() => IsTopButton(context.EventSystem, button), 2f,
                "Catalogue tile did not become the actual top actionable target before Touch.");
            yield return TapButton(touch, id, button);
            yield return WaitUntil(() => ActivePreview(context.Controller) != null, 2f,
                "Catalogue Touch did not create a Preview.");
            var rotate = FindNamed<Button>(context.Scene, "RotateButton");
            yield return WaitUntil(() => IsTopButton(context.EventSystem, rotate), 2f,
                "Preview action bar did not expose an actionable Rotate control.");
            yield return WaitUntil(() => CatalogueCollapsedAndSettled(context.Catalogue), 2f,
                "Catalogue did not finish its 0.16s collapse before the Scene Preview became draggable.");
        }

        private IEnumerator SelectInitialCounter(MainCafeContext context, Touchscreen touch, int id)
        {
            if (context.Catalogue.IsCatalogueVisible && !context.Catalogue.IsCollapsed)
            {
                yield return TapButton(touch, id - 1, FindNamed<Button>(context.Scene, "CollapseButton"));
                yield return WaitUntil(() => CatalogueCollapsedAndSettled(context.Catalogue), 2f,
                    "Catalogue did not collapse before the Scene furniture selection.");
            }

            var instance = context.Layout.FurnitureInstances.Single(item => item.InstanceId == InitialInstanceId);
            var point = FormalFurnitureScreenPoint(context, instance.InstanceId);
            yield return WaitUntil(() => TopGraphicAt(context.EventSystem, point) == null, 2f,
                "Collapsed Catalogue still covered the formal Scene furniture point.");
            AssertWorldPointIsUiFree(context.EventSystem, point);
            yield return BeginContact(touch, id, point);
            var router = ReadPrivate<DecorationTouchRouter>(context.Controller, "touchRouter");
            Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Furniture),
                "The real formal-Collider Began must latch Furniture ownership in its first processed frame.");
            yield return WaitUntil(
                () => ActivePreview(context.Controller)?.SourceInstanceId == InitialInstanceId,
                2f,
                "Touch did not select the formal initial Counter.");
            yield return Release(touch, id, point);
            var store = FindNamed<Button>(context.Scene, "StoreButton");
            yield return WaitUntil(() => IsTopButton(context.EventSystem, store), 2f,
                "Existing-furniture action bar did not expose an actionable Store control.");
        }

        private IEnumerator MovePreviewDirectlyThroughTouch(
            MainCafeContext context,
            Touchscreen touch,
            int id,
            GridPosition target)
        {
            var preview = ActivePreview(context.Controller);
            var offset = ReadPrivate<float>(context.Controller, "sanitizedFurnitureDragOffsetPixels");
            var end = GridCellScreenCenter(context, target) - Vector2.up * offset;
            var start = FindUiFreeScreenPointOnActivePreview(context, end);
            Assert.That(TopGraphicAt(context.EventSystem, end), Is.Null,
                $"Real Touch drag end {end} is covered by " +
                HierarchyPath(TopGraphicAt(context.EventSystem, end)?.transform));
            var projectedTarget = FindContainingGridCellAtScreen(
                context, end + Vector2.up * offset);
            Assert.That(projectedTarget, Is.EqualTo(target),
                $"The production drag offset must project {end} back to the requested target.");
            yield return BeginContact(touch, id, start);
            var router = ReadPrivate<DecorationTouchRouter>(context.Controller, "touchRouter");
            Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Furniture),
                $"The active Preview must own Touch Began at the UI-free point {start}.");
            yield return MoveContact(touch, id, end);
            Assert.That(router.IsDragging, Is.True,
                $"Furniture Touch did not cross the production drag threshold: " +
                $"start={start}, end={end}, distance={Vector2.Distance(start, end)}.");
            Assert.That(ActivePreview(context.Controller).ProposedPosition, Is.EqualTo(target),
                $"Furniture drag Move was consumed but did not settle on the requested cell. " +
                $"start={start}, end={end}, projected=({projectedTarget.X},{projectedTarget.Y}), " +
                $"owner={router.Owner}, dragging={router.IsDragging}, " +
                $"endTop={HierarchyPath(TopGraphicAt(context.EventSystem, end)?.transform)}.");
            yield return Release(touch, id, end);
        }

        private static Vector2 FindUiFreeScreenPointOnActivePreview(
            MainCafeContext context,
            Vector2 dragEnd)
        {
            var previewView = ReadPrivate<FurniturePreviewView>(context.Controller, "previewView");
            Assert.That(previewView.TryGetWorldBounds(out var bounds), Is.True,
                "The active Preview must expose Renderer bounds before a real Touch drag.");

            var min = bounds.min;
            var max = bounds.max;
            var worldCorners = new[]
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };
            var projected = worldCorners.Select(context.Camera.WorldToScreenPoint).ToArray();
            Assert.That(projected.All(point => point.z > 0f), Is.True,
                "The active Preview must remain in front of the production Camera.");

            var screenMin = projected
                .Select(point => new Vector2(point.x, point.y))
                .Aggregate(Vector2.Min);
            var screenMax = projected
                .Select(point => new Vector2(point.x, point.y))
                .Aggregate(Vector2.Max);
            var classifier = (IDecorationTouchHitClassifier)context.Controller;
            var samples = new[] { 0.15f, 0.3f, 0.5f, 0.7f, 0.85f };
            var candidates = new List<Vector2>();
            foreach (var y in samples)
            foreach (var x in samples)
            {
                candidates.Add(new Vector2(
                    Mathf.Lerp(screenMin.x, screenMax.x, x),
                    Mathf.Lerp(screenMin.y, screenMax.y, y)));
            }

            var preview = ActivePreview(context.Controller);
            candidates.AddRange(context.Layout
                .GetFurnitureFootprintCells(
                    preview.DefinitionId,
                    preview.ProposedPosition,
                    preview.ProposedRotation)
                .Select(cell => GridCellScreenCenter(context, cell)));

            var threshold = ReadPrivate<AnimalCafe.Camera.CameraSettings>(
                context.Controller, "cameraSettings").DragThresholdPixels;
            var chosen = candidates
                .Where(point => context.Camera.pixelRect.Contains(point))
                .Where(point => TopGraphicAt(context.EventSystem, point) == null)
                .Where(point => classifier.ClassifyBegan(-1, point).Kind
                    == DecorationTouchHitKind.Furniture)
                .Where(point => Vector2.Distance(point, dragEnd) > threshold)
                .OrderByDescending(point => Vector2.SqrMagnitude(point - dragEnd))
                .Cast<Vector2?>()
                .FirstOrDefault();
            Assert.That(chosen.HasValue, Is.True,
                $"No UI-free Touch point exists on active Preview screen bounds " +
                $"[{screenMin}..{screenMax}] farther than the {threshold}px drag threshold " +
                $"from {dragEnd}.");
            return chosen.GetValueOrDefault();
        }

        private IEnumerator Tap(Touchscreen device, int touchId, Vector2 position)
        {
            AssertTopGraphicExistsAt(EventSystem.current, position);
            Assert.That(TopGraphicAt(EventSystem.current, position), Is.Null,
                "World Tap must use a raycast-verified UI-free point.");
            yield return BeginContact(device, touchId, position);
            yield return Release(device, touchId, position);
        }

        private IEnumerator TapButton(Touchscreen device, int touchId, Button button)
        {
            Assert.That(button, Is.Not.Null);
            yield return WaitUntil(() => IsTopButton(EventSystem.current, button), 2f,
                button.name + " did not become the actual top actionable target before Touch.");
            var position = ButtonCenter(button);
            yield return BeginUiContact(device, touchId, position);
            yield return ReleaseUiContact(device, touchId, position);
            // Let production observe Ended, then flush the terminal slot before a fresh UI Touch.
            yield return null;
            InputSystem.Update();
            yield return null;
        }

        private IEnumerator BeginUiContact(Touchscreen device, int touchId, Vector2 position)
        {
            activeTouchIds.Add(touchId);
            activeTouchPositions[touchId] = position;
            yield return PumpFixtureTouch(
                device, touchId, InputTouchPhase.Began, position, Vector2.zero);
        }

        private IEnumerator ReleaseUiContact(Touchscreen device, int touchId, Vector2 position)
        {
            var delta = activeTouchPositions.TryGetValue(touchId, out var previous)
                ? position - previous
                : Vector2.zero;
            activeTouchIds.Remove(touchId);
            activeTouchPositions.Remove(touchId);
            yield return PumpFixtureTouch(
                device, touchId, InputTouchPhase.Ended, position, delta);
        }

        private IEnumerator Drag(Touchscreen device, int touchId, Vector2 start, params Vector2[] positions)
        {
            yield return BeginContact(device, touchId, start);
            foreach (var position in positions)
            {
                yield return MoveContact(device, touchId, position);
            }
            var end = positions.Length == 0 ? start : positions[positions.Length - 1];
            yield return Release(device, touchId, end);
        }

        private IEnumerator BeginContact(Touchscreen device, int touchId, Vector2 position)
        {
            activeTouchIds.Add(touchId);
            activeTouchPositions[touchId] = position;
            yield return PumpFixtureTouch(
                device, touchId, InputTouchPhase.Began, position, Vector2.zero);
        }

        private IEnumerator MoveContact(Touchscreen device, int touchId, Vector2 position)
        {
            Assert.That(activeTouchPositions.TryGetValue(touchId, out var previous), Is.True,
                "Moved Touch must already be active.");
            activeTouchPositions[touchId] = position;
            yield return PumpFixtureTouch(
                device, touchId, InputTouchPhase.Moved, position, position - previous);
        }

        private IEnumerator Release(Touchscreen device, int touchId, Vector2 position)
        {
            var delta = activeTouchPositions.TryGetValue(touchId, out var previous)
                ? position - previous
                : Vector2.zero;
            activeTouchIds.Remove(touchId);
            activeTouchPositions.Remove(touchId);
            yield return PumpFixtureTouch(
                device, touchId, InputTouchPhase.Ended, position, delta);
        }

        private IEnumerator Cancel(Touchscreen device, int touchId, Vector2 position)
        {
            var delta = activeTouchPositions.TryGetValue(touchId, out var previous)
                ? position - previous
                : Vector2.zero;
            activeTouchIds.Remove(touchId);
            activeTouchPositions.Remove(touchId);
            yield return PumpFixtureTouch(
                device, touchId, InputTouchPhase.Canceled, position, delta);
        }

        private IEnumerator TerminateContact(
            Touchscreen device,
            int touchId,
            Vector2 position,
            InputTouchPhase phase)
        {
            if (phase == InputTouchPhase.Ended)
            {
                yield return Release(device, touchId, position);
                yield break;
            }
            Assert.That(phase, Is.EqualTo(InputTouchPhase.Canceled));
            yield return Cancel(device, touchId, position);
        }

        private IEnumerator PumpFixtureTouch(
            Touchscreen device,
            int touchId,
            InputTouchPhase phase,
            Vector2 position,
            Vector2 delta)
        {
            var timestampDeadline = Time.realtimeSinceStartup + 2f;
            while (currentTime <= device.lastUpdateTime && Time.realtimeSinceStartup < timestampDeadline)
            {
                yield return null;
            }
            Assert.That(currentTime, Is.GreaterThan(device.lastUpdateTime),
                $"Touch {touchId} {phase} requires a fixture timestamp newer than the device state.");
            Assert.That(touchPump, Is.Not.Null, "The fixture-owned early Update Touch pump must exist.");
            var deviceTimeBeforeSemanticEvent = device.lastUpdateTime;
            var semanticEventTime = deviceTimeBeforeSemanticEvent + 0.000001d;
            var token = touchPump.Schedule(
                () =>
                {
                    ApplyFixtureTouch(device, touchId, phase, position, delta, semanticEventTime);
                },
                () => InputState.currentTime >= semanticEventTime);
            var deadline = Time.realtimeSinceStartup + 2f;
            while (!touchPump.IsComplete(token) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(touchPump.IsComplete(token), Is.True,
                $"Touch {touchId} {phase} was not consumed by the early Update pump within 2 realtime seconds.");
            touchPump.ThrowIfFailed(token);
            var processedDeadline = Time.realtimeSinceStartup + 2f;
            while (device.lastUpdateTime <= deviceTimeBeforeSemanticEvent
                   && Time.realtimeSinceStartup < processedDeadline)
            {
                yield return null;
            }
            Assert.That(device.lastUpdateTime, Is.GreaterThan(deviceTimeBeforeSemanticEvent),
                $"Touch {touchId} {phase} queued package event was not processed with a newer device timestamp.");
        }

        private void ApplyFixtureTouch(
            Touchscreen device,
            int touchId,
            InputTouchPhase phase,
            Vector2 position,
            Vector2 delta,
            double eventTime)
        {
            switch (phase)
            {
                case InputTouchPhase.Began:
                    BeginTouch(touchId, position, screen: device, time: eventTime);
                    break;
                case InputTouchPhase.Moved:
                    MoveTouch(touchId, position, delta, screen: device, time: eventTime);
                    break;
                case InputTouchPhase.Ended:
                    EndTouch(touchId, position, delta, screen: device, time: eventTime);
                    break;
                case InputTouchPhase.Canceled:
                    CancelTouch(touchId, position, delta, screen: device, time: eventTime);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(phase), phase, null);
            }
        }

        private Touchscreen AddTouchscreen() => InputSystem.AddDevice<Touchscreen>();

        private void CleanupTouchscreen(Touchscreen device)
        {
            if (device == null || !device.added)
            {
                return;
            }

            foreach (var id in activeTouchIds.ToArray())
            {
                var position = activeTouchPositions.TryGetValue(id, out var activePosition)
                    ? activePosition
                    : Vector2.zero;
                CancelTouch(id, position, Vector2.zero, queueEventOnly: true, screen: device);
            }
            activeTouchIds.Clear();
            activeTouchPositions.Clear();
            InputSystem.Update();
            foreach (var controller in FindAllInFixtureScenes<DecorationModeController>()) controller.enabled = false;
            foreach (var source in FindAllInFixtureScenes<InputSystemDecorationTouchSource>()) source.enabled = false;
            InputSystem.RemoveDevice(device);
        }

        private static Vector2 ButtonCenter(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.WorldToScreenPoint(camera, (corners[0] + corners[2]) * 0.5f);
        }

        private static Vector2 GridCellScreenCenter(MainCafeContext context, GridPosition cell)
        {
            var world = context.GridRoot.TransformPoint(context.GridSpace.GetCellCenterLocal(cell, 0.05f));
            return context.Camera.WorldToScreenPoint(world);
        }

        private static Vector2 FormalFurnitureScreenPoint(MainCafeContext context, string instanceId)
        {
            var registry = FindAll<FurnitureSceneRegistry>(context.Scene).Single();
            Assert.That(registry.TryGet(instanceId, out var representation), Is.True,
                "Formal furniture representation must exist before a real Touch selection.");
            var colliders = representation.GetComponentsInChildren<Collider>(false);
            Assert.That(colliders, Is.Not.Empty,
                "Formal furniture selection requires its real production Collider.");
            foreach (var collider in colliders)
            {
                var point = (Vector2)context.Camera.WorldToScreenPoint(collider.bounds.center);
                var hits = Physics.RaycastAll(context.Camera.ScreenPointToRay(point), Mathf.Infinity,
                    ~0, QueryTriggerInteraction.Collide);
                if (hits.Any(hit => hit.collider == collider))
                {
                    return point;
                }
            }

            Assert.Fail("No raycast-verified point reached the formal furniture Collider for " + instanceId);
            return default;
        }

        private static GameObject TopGraphicAt(EventSystem eventSystem, Vector2 position)
        {
            if (eventSystem == null) return null;
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, results);
            return results.FirstOrDefault().gameObject;
        }

        private static IEnumerator WaitUntil(
            Func<bool> condition,
            float realtimeTimeout,
            string failureMessage)
        {
            var deadline = Time.realtimeSinceStartup + realtimeTimeout;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(condition(), Is.True, failureMessage);
        }

        private static MainCafeContext CaptureMainCafe()
        {
            var scene = SceneManager.GetActiveScene();
            var controller = FindAll<DecorationModeController>(scene).Single();
            return new MainCafeContext(
                scene,
                controller,
                FindAll<CafeLayoutRuntime>(scene).Single().Layout,
                FindAll<UnityEngine.Camera>(scene).Single(item => item.CompareTag("MainCamera")),
                FindAll<EventSystem>(scene).Single(),
                FindAll<DecorationCatalogueView>(scene).Single(),
                FindAll<DecorationActionBarView>(scene).Single(),
                FindAll<DecorationStoreModalView>(scene).Single(),
                FindAll<GridHighlightView>(scene).Single(),
                ReadPrivate<Button>(controller, "decorationModeButton"),
                ReadPrivate<Transform>(controller, "gridRoot"),
                ReadPrivate<DecorationGridSpace>(controller, "gridSpace"),
                ReadPrivate<DecorationCatalogueAsset>(controller, "catalogueAsset"));
        }

        private static FurniturePlacementPreview ActivePreview(DecorationModeController controller) =>
            ReadPrivate<DecorationSession>(controller, "session")?.ActivePreview;

        private static string SnapshotLayout(CafeLayout layout) => string.Join("|", layout.FurnitureInstances
            .OrderBy(item => item.InstanceId, StringComparer.Ordinal)
            .Select(item => $"{item.InstanceId}:{item.DefinitionId}:{item.Position.X},{item.Position.Y}:{item.Rotation}"));

        private static DecorationCatalogueTileView[] ActiveTiles(DecorationCatalogueView view) =>
            view.GetComponentsInChildren<DecorationCatalogueTileView>(false)
                .Where(tile => tile.gameObject.activeInHierarchy && tile.Definition != null)
                .OrderBy(tile => tile.name, StringComparer.Ordinal)
                .ToArray();

        private static GridPosition FindValidFreeCell(
            CafeLayout layout,
            string definitionId,
            FurnitureRotation rotation)
        {
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                var cell = new GridPosition(x, y);
                if (layout.ValidateFurniturePlacement(definitionId, cell, rotation).Succeeded)
                    return cell;
            }
            Assert.Fail("No valid free cell for " + definitionId);
            return default;
        }

        private static GridPosition FindValidFreeCellDifferent(
            CafeLayout layout,
            string definitionId,
            FurnitureRotation rotation,
            GridPosition excluded)
        {
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 8; x++)
            {
                if (x == excluded.X && y == excluded.Y) continue;
                var cell = new GridPosition(x, y);
                if (layout.ValidateFurniturePlacement(definitionId, cell, rotation).Succeeded)
                    return cell;
            }
            Assert.Fail("No different valid free cell for " + definitionId);
            return default;
        }

        private static Vector2 UiFreeCell(MainCafeContext context, IEnumerable<GridPosition> candidates)
        {
            foreach (var cell in candidates)
            {
                var position = GridCellScreenCenter(context, cell);
                if (TopGraphicAt(context.EventSystem, position) == null)
                {
                    return position;
                }
            }
            Assert.Fail("No raycast-verified UI-free logical Floor point was available.");
            return default;
        }

        private static Vector2? FindUiFreeEdgePoint(MainCafeContext context, string edge)
        {
            var viewport = context.Camera.pixelRect;
            var usable = Rect.MinMaxRect(
                Mathf.Max(viewport.xMin, Screen.safeArea.xMin),
                Mathf.Max(viewport.yMin, Screen.safeArea.yMin),
                Mathf.Min(viewport.xMax, Screen.safeArea.xMax),
                Mathf.Min(viewport.yMax, Screen.safeArea.yMax));
            var flatForward = Vector3.ProjectOnPlane(
                context.Camera.transform.forward, Vector3.up).normalized;
            var flatRight = Vector3.ProjectOnPlane(
                context.Camera.transform.right, Vector3.up).normalized;
            var intent = edge switch
            {
                "Left" => new Vector2(1f, 0f),
                "Right" => new Vector2(-1f, 0f),
                "Bottom" => new Vector2(0f, 1f),
                "Top" => new Vector2(0f, -1f),
                _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, null)
            };
            var localPanDirection = context.GridRoot.InverseTransformDirection(
                -(flatRight * intent.x + flatForward * intent.y));
            Vector2? best = null;
            var bestDistance = float.PositiveInfinity;
            for (var step = 1; step <= 99; step++)
            {
                var t = step / 100f;
                Vector2 point;
                switch (edge)
                {
                    case "Left": point = new Vector2(usable.xMin + 2f, Mathf.Lerp(usable.yMin, usable.yMax, t)); break;
                    case "Right": point = new Vector2(usable.xMax - 2f, Mathf.Lerp(usable.yMin, usable.yMax, t)); break;
                    case "Bottom": point = new Vector2(Mathf.Lerp(usable.xMin, usable.xMax, t), usable.yMin + 2f); break;
                    case "Top": point = new Vector2(Mathf.Lerp(usable.xMin, usable.xMax, t), usable.yMax - 2f); break;
                    default: throw new ArgumentOutOfRangeException(nameof(edge), edge, null);
                }
                if (TopGraphicAt(context.EventSystem, point) != null) continue;
                var ray = context.Camera.ScreenPointToRay(point);
                var plane = new Plane(context.GridRoot.up, context.GridRoot.position);
                if (!plane.Raycast(ray, out var rayDistance)) continue;
                var local = context.GridRoot.InverseTransformPoint(ray.GetPoint(rayDistance));
                var cellSize = context.GridSpace.Settings.CellSize;
                var distanceX = DistanceToNextGridBoundary(
                    local.x / cellSize, localPanDirection.x);
                var distanceZ = DistanceToNextGridBoundary(
                    local.z / cellSize, localPanDirection.z);
                var distance = Mathf.Min(distanceX, distanceZ);
                if (!float.IsFinite(distance)) continue;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = point;
                }
            }
            return best;
        }

        private static float DistanceToNextGridBoundary(float coordinate, float direction)
        {
            const float epsilon = 0.00001f;
            var fraction = coordinate - Mathf.Floor(coordinate);
            if (direction > epsilon) return (1f - fraction) / direction;
            if (direction < -epsilon) return fraction / -direction;
            return float.PositiveInfinity;
        }

        private static void AssertWorldPointIsUiFree(EventSystem eventSystem, Vector2 position) =>
            Assert.That(TopGraphicAt(eventSystem, position), Is.Null,
                "World Touch point must be outside every active GraphicRaycaster result.");

        private static void PlaceFurnitureBehindScreenPoint(
            MainCafeContext context,
            GameObject representation,
            Vector2 screenPoint)
        {
            var ray = context.Camera.ScreenPointToRay(screenPoint);
            var plane = new Plane(context.GridRoot.up, context.GridRoot.position);
            Assert.That(plane.Raycast(ray, out var distance), Is.True,
                "UI pass-through fixture point must intersect the production Grid plane.");
            representation.transform.position = ray.GetPoint(distance);
            representation.SetActive(true);
            Physics.SyncTransforms();
        }

        private static void AssertFurnitureUnderScreenPoint(
            MainCafeContext context,
            Vector2 screenPoint,
            string expectedInstanceId)
        {
            var hits = Physics.RaycastAll(context.Camera.ScreenPointToRay(screenPoint), Mathf.Infinity)
                .OrderBy(hit => hit.distance)
                .ToArray();
            var matched = hits.Any(hit =>
                context.Registry.TryGetInstanceId(hit.collider, out var instanceId)
                && instanceId == expectedInstanceId);
            Assert.That(matched, Is.True,
                "The release point must have a registry-backed Furniture collider underneath the UI Button.");
        }

        private static void AssertTopButton(EventSystem eventSystem, Button button)
        {
            var center = ButtonCenter(button);
            var top = TopGraphicAt(eventSystem, center);
            Assert.That(top, Is.Not.Null);
            Assert.That(top == button.gameObject || top.transform.IsChildOf(button.transform), Is.True,
                $"{button.name} must own the top actionable raycast before Touch. " +
                $"center={center}, top={HierarchyPath(top?.transform)}");
        }

        private static string HierarchyPath(Transform transform)
        {
            if (transform == null) return "<none>";
            var names = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
            {
                names.Push(current.name);
            }
            return string.Join("/", names);
        }

        private static bool IsTopButton(EventSystem eventSystem, Button button)
        {
            if (button == null || !button.gameObject.activeInHierarchy)
            {
                return false;
            }

            var top = TopGraphicAt(eventSystem, ButtonCenter(button));
            return top != null && (top == button.gameObject || top.transform.IsChildOf(button.transform));
        }

        private static void AssertTopGraphicExistsAt(EventSystem eventSystem, Vector2 position)
        {
            // World gestures intentionally have no Graphic hit; UI gestures are checked by their callers.
            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(float.IsFinite(position.x) && float.IsFinite(position.y), Is.True);
        }

        private static void AssertRouterClean(DecorationTouchRouter router)
        {
            Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.None));
            Assert.That(router.PrimaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
            Assert.That(router.SecondaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
            Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.False);
        }

        private static void AssertPointerBoundaryClean(UiPointerBoundary boundary)
        {
            var ownership = ReadPrivate<Dictionary<int, UiPointerOwnership>>(
                boundary, "ownershipByPointer");
            Assert.That(ownership, Is.Empty, "No UI/Scene pointer ownership may survive a terminal path.");
            Assert.That(ReadPrivate<int>(boundary, "sceneBlockCount"), Is.Zero,
                "No modal Scene block may survive a terminal path.");
        }

        private static void AssertRecorderNamespace(
            PointerRecorder recorder,
            int expectedRawTouchId,
            int firstRecord = 0)
        {
            Assert.That(recorder.RawTouchIds.Skip(firstRecord), Is.Not.Empty);
            Assert.That(recorder.RawTouchIds.Skip(firstRecord), Is.All.EqualTo(expectedRawTouchId));
            Assert.That(recorder.CompositePointerIds.Skip(firstRecord).All(id => id != expectedRawTouchId),
                Is.True, "Composite UI pointer IDs must remain separate from raw Touch IDs.");
        }

        private static void AssertRecorderOrdering(PointerRecorder recorder, int firstRecord = 0)
        {
            var events = recorder.Events.Skip(firstRecord).ToArray();
            var down = Array.IndexOf(events, "Down");
            var up = Array.IndexOf(events, "Up");
            var click = Array.IndexOf(events, "Click");
            var beginDrag = Array.IndexOf(events, "BeginDrag");
            var drag = Array.IndexOf(events, "Drag");
            var endDrag = Array.IndexOf(events, "EndDrag");
            if (up >= 0) Assert.That(down, Is.GreaterThanOrEqualTo(0).And.LessThan(up));
            if (click >= 0) Assert.That(down, Is.GreaterThanOrEqualTo(0).And.LessThan(click));
            if (drag >= 0) Assert.That(beginDrag, Is.GreaterThanOrEqualTo(0).And.LessThanOrEqualTo(drag));
            if (endDrag >= 0) Assert.That(beginDrag, Is.GreaterThanOrEqualTo(0).And.LessThan(endDrag));
        }

        private static GridPosition FindUiReachableCellWithFailure(
            MainCafeContext context,
            string definitionId,
            FurnitureRotation rotation,
            PlacementFailureReason reason,
            GridPosition origin)
        {
            var dragOffset = ReadPrivate<float>(
                context.Controller, "sanitizedFurnitureDragOffsetPixels");
            var edgeZone = ReadPrivate<DecorationCameraDriver>(
                context.Controller, "cameraDriver").EdgeZonePixels;
            var safePixelRect = Rect.MinMaxRect(
                context.Camera.pixelRect.xMin + edgeZone,
                context.Camera.pixelRect.yMin + edgeZone,
                context.Camera.pixelRect.xMax - edgeZone,
                context.Camera.pixelRect.yMax - edgeZone);
            var originScreen = GridCellScreenCenter(context, origin) - Vector2.up * dragOffset;
            var candidates = new List<(GridPosition Cell, float Distance)>();
            for (var y = -2; y <= 9; y++)
            for (var x = -2; x <= 9; x++)
            {
                var cell = new GridPosition(x, y);
                var result = context.Layout.ValidateFurniturePlacement(definitionId, cell, rotation);
                if (result.Succeeded || result.FailureReason != reason) continue;
                var screen = GridCellScreenCenter(context, cell) - Vector2.up * dragOffset;
                if (!safePixelRect.Contains(screen)
                    || TopGraphicAt(context.EventSystem, screen) != null)
                {
                    continue;
                }

                candidates.Add((cell, Vector2.SqrMagnitude(screen - originScreen)));
            }

            if (candidates.Count > 0)
            {
                return candidates.OrderBy(item => item.Distance)
                    .ThenBy(item => item.Cell.Y)
                    .ThenBy(item => item.Cell.X)
                    .First().Cell;
            }

            Assert.Fail($"No UI-free on-screen cell produced {reason} for {definitionId}.");
            return default;
        }

        private static GridPosition FindContainingGridCellAtScreen(
            MainCafeContext context,
            Vector2 screenPosition)
        {
            var ray = context.Camera.ScreenPointToRay(screenPosition);
            var plane = new Plane(context.GridRoot.up, context.GridRoot.position);
            Assert.That(plane.Raycast(ray, out var distance), Is.True,
                "Camera centre must intersect the configured Grid plane.");
            var local = context.GridRoot.InverseTransformPoint(ray.GetPoint(distance));
            var cellSize = context.GridSpace.Settings.CellSize;
            return new GridPosition(
                context.GridSpace.Bounds.Origin.X + Mathf.FloorToInt(local.x / cellSize),
                context.GridSpace.Bounds.Origin.Y + Mathf.FloorToInt(local.z / cellSize));
        }

        private static int ActiveFootprintVisualCount(MainCafeContext context)
        {
            var root = ReadPrivate<Transform>(context.Grid, "visualRoot");
            return root.GetComponentsInChildren<Transform>(true)
                .Count(item => item.name.StartsWith("FootprintCell_", StringComparison.Ordinal)
                    && item.gameObject.activeInHierarchy);
        }

        private static int ActiveBaseGridVisualCount(MainCafeContext context)
        {
            var root = ReadPrivate<Transform>(context.Grid, "visualRoot");
            return root.GetComponentsInChildren<Transform>(true)
                .Count(item => item.name.StartsWith("BaseCell_", StringComparison.Ordinal)
                    && item.gameObject.activeInHierarchy);
        }

        private static int ActiveInvalidGeometryCount(MainCafeContext context)
        {
            var root = ReadPrivate<Transform>(context.Grid, "visualRoot");
            return root.GetComponentsInChildren<Transform>(true)
                .Count(item => item.name.StartsWith("InvalidBar", StringComparison.Ordinal)
                    && item.gameObject.activeInHierarchy);
        }

        private static bool IsTopTarget(EventSystem eventSystem, Component target, Vector2 screenPoint)
        {
            var top = TopGraphicAt(eventSystem, screenPoint);
            return top != null && (top == target.gameObject || top.transform.IsChildOf(target.transform));
        }

        private static int ActivePreviewObjectCount(MainCafeContext context) =>
            FindAll<Transform>(context.Scene).Count(item =>
                item.name.StartsWith("FurniturePreview_", StringComparison.Ordinal)
                && item.gameObject.activeInHierarchy);

        private static int CountActiveRaycastOwners(MainCafeContext context)
        {
            var groups = new[]
            {
                ReadPrivate<CanvasGroup>(context.Catalogue, "canvasGroup"),
                ReadPrivate<CanvasGroup>(context.ActionBar, "canvasGroup"),
                ReadPrivate<CanvasGroup>(context.StoreModal, "canvasGroup")
            };
            return groups.Count(group => group != null
                && group.gameObject.activeInHierarchy
                && group.enabled
                && group.interactable
                && group.blocksRaycasts);
        }

        private static void AssertTransitionCommandTiming(
            float priorCommandTime,
            float nextCommandTime,
            float duration,
            string label)
        {
            Assert.That(priorCommandTime, Is.GreaterThanOrEqualTo(0f), label + " prior callback");
            Assert.That(nextCommandTime, Is.GreaterThanOrEqualTo(priorCommandTime),
                label + " callback order");
            Assert.That(nextCommandTime - priorCommandTime, Is.LessThan(duration),
                label + " must occur inside the real unscaled transition window.");
        }

        private static void AssertNoCommerceContract(MainCafeContext context)
        {
            var forbidden = new[]
            {
                "price", "stock", "unlock", "owned", "inventory", "decrement", "depletion",
                "价格", "库存", "解锁", "拥有"
            };
            var visibleCopy = context.Catalogue.GetComponentsInChildren<TMP_Text>(true)
                .Concat(context.ActionBar.GetComponentsInChildren<TMP_Text>(true))
                .Select(label => label.text ?? string.Empty)
                .ToArray();
            foreach (var token in forbidden)
            {
                Assert.That(visibleCopy.Any(copy => copy.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.False, "Decoration UI must not expose commerce state: " + token);
            }

            var contractNames = context.Controller.GetType()
                .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(member => member.Name)
                .Concat(context.Catalogue.GetType()
                    .GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Select(member => member.Name))
                .ToArray();
            foreach (var token in forbidden.Take(7))
            {
                Assert.That(contractNames.Any(name => name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0),
                    Is.False, "Decoration controller/view contract must not track commerce state: " + token);
            }
        }

        private static void AssertEssentialButton(EventSystem eventSystem, Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var screen = corners.Select(corner => RectTransformUtility.WorldToScreenPoint(camera, corner)).ToArray();
            Assert.That(screen.All(point => point.x >= 0f && point.x <= Screen.width
                && point.y >= 0f && point.y <= Screen.height), Is.True,
                $"{button.name}; Screen={Screen.width}x{Screen.height}; " +
                $"corners={string.Join(",", screen.Select(point => point.ToString()))}");
            Assert.That(rect.rect.width, Is.GreaterThanOrEqualTo(48f), button.name + " authoring width");
            Assert.That(rect.rect.height, Is.GreaterThanOrEqualTo(48f), button.name + " authoring height");
            var canvasRect = (RectTransform)canvas.transform;
            var safeArea = Screen.safeArea;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, new Vector2(safeArea.xMin, safeArea.yMin), camera, out var safeMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, new Vector2(safeArea.xMax, safeArea.yMax), camera, out var safeMax);
            var logicalSafeArea = Rect.MinMaxRect(
                Mathf.Min(safeMin.x, safeMax.x), Mathf.Min(safeMin.y, safeMax.y),
                Mathf.Max(safeMin.x, safeMax.x), Mathf.Max(safeMin.y, safeMax.y));
            var logicalCorners = corners.Select(corner => (Vector2)canvasRect.InverseTransformPoint(corner)).ToArray();
            Assert.That(logicalCorners.All(point =>
                point.x >= logicalSafeArea.xMin - 0.1f && point.x <= logicalSafeArea.xMax + 0.1f
                && point.y >= logicalSafeArea.yMin - 0.1f && point.y <= logicalSafeArea.yMax + 0.1f),
                Is.True, button.name + " logical Safe Area containment");
            AssertTopButton(eventSystem, button);
        }

        private static void AssertGridPosition(GridPosition actual, GridPosition expected, string message)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X), message + " (X)");
            Assert.That(actual.Y, Is.EqualTo(expected.Y), message + " (Y)");
        }

        private static bool FindUiFreeLogicalArea(EventSystem eventSystem)
        {
            for (var y = 1; y < 10; y++)
            for (var x = 1; x < 10; x++)
            {
                var point = new Vector2(Screen.width * x / 10f, Screen.height * y / 10f);
                if (TopGraphicAt(eventSystem, point) == null) return true;
            }
            return false;
        }

        private static T ReadPrivate<T>(object owner, string fieldName)
        {
            var field = owner.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, owner.GetType().Name + "." + fieldName);
            return (T)field.GetValue(owner);
        }

        private static T FindNamed<T>(Scene scene, string name) where T : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => item.name == name)
                .Select(item => item.GetComponent<T>())
                .Where(item => item != null)
                .First();

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static T[] FindAllLoaded<T>() where T : Component => Enumerable.Range(0, SceneManager.sceneCount)
            .Select(SceneManager.GetSceneAt)
            .Where(scene => scene.IsValid() && scene.isLoaded)
            .SelectMany(FindAll<T>)
            .ToArray();

        private static Scene[] LoadedScenes() => Enumerable.Range(0, SceneManager.sceneCount)
            .Select(SceneManager.GetSceneAt)
            .Where(scene => scene.IsValid() && scene.isLoaded)
            .ToArray();

        private T[] FindAllInFixtureScenes<T>() where T : Component => LoadedScenes()
            .Where(scene => fixtureSceneHandles.Contains(scene.handle))
            .SelectMany(FindAll<T>)
            .ToArray();

        private void InstallTouchPumpPlayerLoop()
        {
            Assert.That(playerLoopPumpInstalled, Is.False);
            playerLoopBeforePump = PlayerLoop.GetCurrentPlayerLoop();
            var loopWithPump = playerLoopBeforePump;
            var pumpSystem = new PlayerLoopSystem
            {
                type = typeof(Task9TouchPump),
                updateDelegate = touchPump.ConsumePending
            };
            Assert.That(InsertBeforeScriptBehaviourUpdate(ref loopWithPump, pumpSystem), Is.True,
                "Task 9 fixture could not install its scoped pre-Behaviour Update Touch pump.");
            PlayerLoop.SetPlayerLoop(loopWithPump);
            playerLoopPumpInstalled = true;
        }

        private void RestoreTouchPumpPlayerLoop()
        {
            if (!playerLoopPumpInstalled)
            {
                return;
            }

            PlayerLoop.SetPlayerLoop(playerLoopBeforePump);
            playerLoopPumpInstalled = false;
        }

        private static bool InsertBeforeScriptBehaviourUpdate(
            ref PlayerLoopSystem parent,
            PlayerLoopSystem pumpSystem)
        {
            var children = parent.subSystemList;
            if (children == null)
            {
                return false;
            }

            for (var index = 0; index < children.Length; index++)
            {
                if (children[index].type == typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate))
                {
                    var expanded = new PlayerLoopSystem[children.Length + 1];
                    Array.Copy(children, 0, expanded, 0, index);
                    expanded[index] = pumpSystem;
                    Array.Copy(children, index, expanded, index + 1, children.Length - index);
                    parent.subSystemList = expanded;
                    return true;
                }

                var child = children[index];
                if (InsertBeforeScriptBehaviourUpdate(ref child, pumpSystem))
                {
                    children[index] = child;
                    parent.subSystemList = children;
                    return true;
                }
            }

            return false;
        }

        private sealed class Task9TouchPump : MonoBehaviour
        {
            private Action pending;
            private Func<bool> canConsumePending;
            private Exception failure;
            private int scheduledToken;
            private int completedToken;

            public int Schedule(Action semanticEvent, Func<bool> canConsume)
            {
                if (semanticEvent == null)
                {
                    throw new ArgumentNullException(nameof(semanticEvent));
                }
                if (pending != null)
                {
                    throw new InvalidOperationException(
                        "Task 9 Touch pump permits exactly one pending semantic event.");
                }

                failure = null;
                pending = semanticEvent;
                canConsumePending = canConsume ?? throw new ArgumentNullException(nameof(canConsume));
                return ++scheduledToken;
            }

            public bool IsComplete(int token) => completedToken >= token;

            public void ThrowIfFailed(int token)
            {
                Assert.That(IsComplete(token), Is.True);
                if (failure != null)
                {
                    throw new InvalidOperationException(
                        "Fixture-owned early Update Touch pump failed.", failure);
                }
            }

            public void ClearPending()
            {
                pending = null;
                canConsumePending = null;
                failure = null;
            }

            public void ConsumePending()
            {
                if (pending == null)
                {
                    return;
                }
                if (canConsumePending != null && !canConsumePending())
                {
                    return;
                }

                var semanticEvent = pending;
                pending = null;
                canConsumePending = null;
                try
                {
                    semanticEvent();
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    completedToken = scheduledToken;
                }
            }
        }

        private sealed class PointerRecorder : MonoBehaviour,
            IPointerDownHandler,
            IPointerUpHandler,
            IPointerClickHandler,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler
        {
            public readonly List<string> Events = new List<string>();
            public readonly List<int> RawTouchIds = new List<int>();
            public readonly List<int> CompositePointerIds = new List<int>();
            public int DownCount { get; private set; }
            public int UpCount { get; private set; }
            public int ClickCount { get; private set; }
            public int BeginDragCount { get; private set; }
            public int DragCount { get; private set; }
            public int EndDragCount { get; private set; }
            public UiPointerBoundary Boundary { get; set; }
            public readonly List<UiPointerOwnership> OwnershipOnDown =
                new List<UiPointerOwnership>();

            public void OnPointerDown(PointerEventData eventData)
            {
                DownCount++;
                Record("Down", eventData);
                OwnershipOnDown.Add(Boundary?.GetOwnership(eventData.pointerId) ?? UiPointerOwnership.None);
            }
            public void OnPointerUp(PointerEventData eventData) { UpCount++; Record("Up", eventData); }
            public void OnPointerClick(PointerEventData eventData) { ClickCount++; Record("Click", eventData); }
            public void OnBeginDrag(PointerEventData eventData)
            {
                BeginDragCount++;
                Record("BeginDrag", eventData);
            }
            public void OnDrag(PointerEventData eventData)
            {
                DragCount++;
                Record("Drag", eventData);
            }
            public void OnEndDrag(PointerEventData eventData)
            {
                EndDragCount++;
                Record("EndDrag", eventData);
            }

            private void Record(string name, PointerEventData eventData)
            {
                Assert.That(eventData, Is.TypeOf<ExtendedPointerEventData>());
                var extended = (ExtendedPointerEventData)eventData;
                Events.Add(name);
                RawTouchIds.Add(extended.touchId);
                CompositePointerIds.Add(eventData.pointerId);
            }
        }

        private readonly struct MainCafeContext
        {
            public MainCafeContext(
                Scene scene,
                DecorationModeController controller,
                CafeLayout layout,
                UnityEngine.Camera camera,
                EventSystem eventSystem,
                DecorationCatalogueView catalogue,
                DecorationActionBarView actionBar,
                DecorationStoreModalView storeModal,
                GridHighlightView grid,
                Button hudButton,
                Transform gridRoot,
                DecorationGridSpace gridSpace,
                DecorationCatalogueAsset catalogueAsset)
            {
                Scene = scene;
                Controller = controller;
                Layout = layout;
                Camera = camera;
                EventSystem = eventSystem;
                Catalogue = catalogue;
                ActionBar = actionBar;
                StoreModal = storeModal;
                Grid = grid;
                HudButton = hudButton;
                GridRoot = gridRoot;
                GridSpace = gridSpace;
                CatalogueAsset = catalogueAsset;
                SceneInteraction = ReadPrivate<SceneInteractionController>(controller, "sceneInteraction");
                Registry = ReadPrivate<FurnitureSceneRegistry>(controller, "sceneRegistry");
            }

            public Scene Scene { get; }
            public DecorationModeController Controller { get; }
            public CafeLayout Layout { get; }
            public UnityEngine.Camera Camera { get; }
            public EventSystem EventSystem { get; }
            public DecorationCatalogueView Catalogue { get; }
            public DecorationActionBarView ActionBar { get; }
            public DecorationStoreModalView StoreModal { get; }
            public GridHighlightView Grid { get; }
            public Button HudButton { get; }
            public Transform GridRoot { get; }
            public DecorationGridSpace GridSpace { get; }
            public DecorationCatalogueAsset CatalogueAsset { get; }
            public SceneInteractionController SceneInteraction { get; }
            public FurnitureSceneRegistry Registry { get; }
        }
    }
}
#endif

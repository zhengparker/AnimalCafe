using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using AnimalCafe.UI.Decoration;
using AnimalCafe.Camera;
using AnimalCafe.Core.Time;
using AnimalCafe.Interaction;
using AnimalCafe.UI.Foundation;
using AnimalCafe.UI;
using AnimalCafe.UI.Components;
using TMPro;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase7WallMountedTouchPlayModeTests
    {
        [TestCase(DecorationModeKind.Furniture, DecorationTouchHitKind.Furniture, true)]
        [TestCase(DecorationModeKind.Furniture, DecorationTouchHitKind.Scene, true)]
        [TestCase(DecorationModeKind.Furniture, DecorationTouchHitKind.FloorGrid, false)]
        [TestCase(DecorationModeKind.Floor, DecorationTouchHitKind.FloorGrid, true)]
        [TestCase(DecorationModeKind.Floor, DecorationTouchHitKind.WallSurface, false)]
        [TestCase(DecorationModeKind.Wall, DecorationTouchHitKind.WallSurface, true)]
        [TestCase(DecorationModeKind.Wall, DecorationTouchHitKind.WallSlot, false)]
        [TestCase(DecorationModeKind.WallDecor, DecorationTouchHitKind.WallSlot, true)]
        [TestCase(DecorationModeKind.WallDecor, DecorationTouchHitKind.WallMounted, true)]
        [TestCase(DecorationModeKind.WallDecor, DecorationTouchHitKind.Furniture, false)]
        public void Routing_AcceptsOnlyTheActiveModesDeclaredSceneHits(
            DecorationModeKind mode,
            DecorationTouchHitKind hit,
            bool expected)
        {
            Assert.That(DecorationModeController.AcceptsSceneHit(mode, hit), Is.EqualTo(expected));
        }

        [Test]
        public void ControllerClassifier_ProducesTypedFloorGridAndWallSurfaceHitsForTheActiveMode()
        {
            var root = new GameObject("Task8TypedClassifier");
            var cameraObject = new GameObject("Camera");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var controller = root.AddComponent<DecorationModeController>();
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.transform.position = new Vector3(0.5f, 10f, 0.5f);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                floor.transform.position = new Vector3(0.5f, 0f, 0.5f);
                floor.transform.localScale = new Vector3(8f, 0.1f, 8f);
                Set(controller, "targetCamera", camera);
                Set(controller, "floorCollider", floor.GetComponent<Collider>());
                Set(controller, "gridRoot", root.transform);
                Set(controller, "gridSpace", new DecorationGridSpace(
                    new GridSettings(1f),
                    new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8))));
                Physics.SyncTransforms();

                Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                var floorHit = ((IDecorationTouchHitClassifier)controller).ClassifyBegan(
                    1, camera.WorldToScreenPoint(new Vector3(2.5f, 0f, 3.5f)));
                Assert.That(floorHit.Kind, Is.EqualTo(DecorationTouchHitKind.FloorGrid));
                Assert.That(floorHit.FloorPosition, Is.EqualTo(new GridPosition(2, 3)));
                var blankFloorHit = ((IDecorationTouchHitClassifier)controller).ClassifyBegan(
                    9, camera.WorldToScreenPoint(new Vector3(20f, 0f, 20f)));
                Assert.That(blankFloorHit.Kind, Is.EqualTo(DecorationTouchHitKind.Scene),
                    "Blank Scene must still let Floor drag belong to Camera without becoming drag-to-paint.");

                wall.transform.position = new Vector3(0f, 1f, 0f);
                wall.transform.localScale = new Vector3(4f, 2f, 0.1f);
                var authoring = wall.AddComponent<WallSurfaceAuthoring>();
                Set(authoring, "surfaceId", "wall.back-left");
                camera.transform.position = new Vector3(0f, 1f, -10f);
                camera.transform.rotation = Quaternion.identity;
                Physics.SyncTransforms();
                Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
                var wallHit = ((IDecorationTouchHitClassifier)controller).ClassifyBegan(
                    2, camera.WorldToScreenPoint(wall.transform.position));
                Assert.That(wallHit.Kind, Is.EqualTo(DecorationTouchHitKind.WallSurface));
                Assert.That(wallHit.SurfaceId, Is.EqualTo("wall.back-left"));

                Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
                var blankWallDecorHit = ((IDecorationTouchHitClassifier)controller).ClassifyBegan(
                    3, camera.WorldToScreenPoint(wall.transform.position));
                Assert.That(blankWallDecorHit.Kind, Is.EqualTo(DecorationTouchHitKind.Scene),
                    "Without an active wall-mounted ghost, dragging a blank wall must belong to Camera pan.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(floor);
                UnityEngine.Object.DestroyImmediate(wall);
            }
        }

        [Test]
        public void ControllerClassifier_ProducesTypedWallMountedHitBeforeUnderlyingWallSlot()
        {
            var root = new GameObject("Task8MountedClassifier");
            var cameraObject = new GameObject("Camera");
            var mountedObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var controller = root.AddComponent<DecorationModeController>();
                var registry = root.AddComponent<WallMountedSceneRegistry>();
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.transform.position = new Vector3(0f, 0f, -10f);
                mountedObject.transform.position = Vector3.zero;
                registry.Register("decor.instance.1", mountedObject);
                Set(controller, "targetCamera", camera);
                Set(controller, "wallMountedSceneRegistry", registry);
                Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
                Physics.SyncTransforms();

                var hit = ((IDecorationTouchHitClassifier)controller).ClassifyBegan(
                    3, camera.WorldToScreenPoint(Vector3.zero));

                Assert.That(hit.Kind, Is.EqualTo(DecorationTouchHitKind.WallMounted));
                Assert.That(hit.TargetId, Is.EqualTo("decor.instance.1"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(mountedObject);
            }
        }

        [Test]
        public void Router_FloorShortTapAndDragHaveDistinctCommands()
        {
            var router = new DecorationTouchRouter(8f, 0f);
            var classifier = new FixedClassifier(new DecorationTouchHit(
                DecorationTouchHitKind.FloorGrid,
                targetId: "floor.2.3"));

            router.ProcessFrame(Frame(1, Point(1, 10, 10, InputTouchPhase.Began)), classifier);
            var tap = router.ProcessFrame(Frame(2, Point(1, 10, 10, InputTouchPhase.Ended)), classifier);
            Assert.That(tap.TapReleased, Is.True);
            Assert.That(tap.OriginHit.TargetId, Is.EqualTo("floor.2.3"));

            router.ProcessFrame(Frame(3, Point(2, 10, 10, InputTouchPhase.Began)), classifier);
            var drag = router.ProcessFrame(Frame(4, Point(2, 30, 10, InputTouchPhase.Moved, 20, 0)), classifier);
            Assert.That(drag.CameraPanRequested, Is.True);
            Assert.That(drag.SceneDragRequested, Is.False);
        }

        [Test]
        public void Router_WallTapSelectsWholeSurfaceAndWallMountedDragKeepsTargetUpdates()
        {
            var router = new DecorationTouchRouter(8f, 0f);
            var wall = new FixedClassifier(new DecorationTouchHit(
                DecorationTouchHitKind.WallSurface,
                targetId: "wall.back-left"));
            router.ProcessFrame(Frame(10, Point(3, 5, 5, InputTouchPhase.Began)), wall);
            var tap = router.ProcessFrame(Frame(11, Point(3, 5, 5, InputTouchPhase.Ended)), wall);
            Assert.That(tap.TapReleased, Is.True);
            Assert.That(tap.OriginHit.TargetId, Is.EqualTo("wall.back-left"));

            var mounted = new FixedClassifier(new DecorationTouchHit(
                DecorationTouchHitKind.WallMounted,
                targetId: "decor.clock"));
            router.ProcessFrame(Frame(12, Point(4, 20, 20, InputTouchPhase.Began)), mounted);
            var drag = router.ProcessFrame(Frame(13, Point(4, 50, 20, InputTouchPhase.Moved, 30, 0)), mounted);
            Assert.That(drag.SceneDragRequested, Is.True);
            Assert.That(drag.SceneDragScreenPosition, Is.EqualTo(new Vector2(50, 20)));
        }

        [Test]
        public void Router_WallMountedDragReclassifiesCurrentWallAcrossTheCorner()
        {
            var router = new DecorationTouchRouter(8f, 0f);
            var classifier = new CrossingClassifier();
            router.ProcessFrame(Frame(30, Point(12, 10, 10, InputTouchPhase.Began)), classifier);
            var across = router.ProcessFrame(
                Frame(31, Point(12, 60, 10, InputTouchPhase.Moved, 50, 0)),
                classifier);
            Assert.That(across.SceneDragRequested, Is.True);
            Assert.That(across.CurrentHit.Kind, Is.EqualTo(DecorationTouchHitKind.WallSlot));
            Assert.That(across.CurrentHit.TargetId, Is.EqualTo("wall.back-right:4:1"));
        }

        [Test]
        public void Router_WallMountedDragPreservesExplicitNoSlotAsInvalidCurrentHit()
        {
            var router = new DecorationTouchRouter(8f, 0f);
            var classifier = new CornerClassifier();
            router.ProcessFrame(Frame(40, Point(13, 10, 10, InputTouchPhase.Began)), classifier);
            var corner = router.ProcessFrame(
                Frame(41, Point(13, 60, 10, InputTouchPhase.Moved, 50, 0)),
                classifier);
            Assert.That(corner.SceneDragRequested, Is.True);
            Assert.That(corner.OriginHit.Kind, Is.EqualTo(DecorationTouchHitKind.WallSlot));
            Assert.That(corner.CurrentHit.Kind, Is.EqualTo(DecorationTouchHitKind.None));
        }

        [Test]
        public void Router_UiOwnsTheCompleteGestureAndEmitsNoSceneOrCameraCommand()
        {
            var router = new DecorationTouchRouter(8f, 0f);
            var ui = new FixedClassifier(new DecorationTouchHit(DecorationTouchHitKind.Ui));
            router.ProcessFrame(Frame(20, Point(9, 10, 10, InputTouchPhase.Began)), ui);
            var moved = router.ProcessFrame(Frame(21, Point(9, 50, 50, InputTouchPhase.Moved, 40, 40)), ui);
            var ended = router.ProcessFrame(Frame(22, Point(9, 50, 50, InputTouchPhase.Ended)), ui);
            Assert.That(moved.CameraPanRequested, Is.False);
            Assert.That(moved.SceneDragRequested, Is.False);
            Assert.That(ended.TapReleased, Is.False);
        }

        [Test]
        public void Controller_DefaultsFurnitureAndChangesModeThroughThePublicGate()
        {
            var go = new GameObject("Task8ControllerContract");
            try
            {
                var controller = go.AddComponent<DecorationModeController>();
                Assert.That(controller.ActiveMode, Is.EqualTo(DecorationModeKind.Furniture));
                Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
                Assert.That(controller.ActiveMode, Is.EqualTo(DecorationModeKind.Wall));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Controller_ActivePreviewRequestsDiscardExitAndNeverAutoConfirms()
        {
            var go = new GameObject("Task8DiscardExit");
            var owned = new List<UnityEngine.Object>();
            try
            {
                var controller = go.AddComponent<DecorationModeController>();
                controller.ConfigurePhase7Runtime(
                    CreateRoomLayout(),
                    new[]
                    {
                        CreateStyle("floor.cream", SurfaceStyleKind.Floor, false, owned),
                        CreateStyle("paint.cream", SurfaceStyleKind.Paint, false, owned),
                        CreateStyle("wains.none", SurfaceStyleKind.Wainscoting, true, owned)
                    },
                    new WallMountedLayout(new[]
                    {
                        new WallSurfaceLayout("wall.back-left", 8, 2),
                        new WallSurfaceLayout("wall.back-right", 8, 2)
                    }),
                    Array.Empty<WallMountedDefinitionAsset>());
                var requested = 0;
                controller.ExitDiscardConfirmationRequested += () => requested++;
                Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                Assert.That(controller.TrySelectFloorRange(SurfaceEditScope.SingleGridFloor), Is.True);
                Assert.That(controller.TryHandleSceneTap(new DecorationTouchHit(
                    DecorationTouchHitKind.FloorGrid,
                    floorPosition: new GridPosition(1, 1))), Is.True);
                Assert.That(controller.TryRequestExit(), Is.False);
                Assert.That(requested, Is.EqualTo(1));
                Assert.That(controller.ActiveSurfacePreview, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                foreach (var item in owned) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void Controller_Task8HandlersRemainFocusedAndPrivate()
        {
            var names = new[]
            {
                "HandleFurnitureFrame", "HandleFloorFrame", "HandleWallFrame", "HandleWallMountedFrame"
            };
            foreach (var name in names)
            {
                var method = typeof(DecorationModeController).GetMethod(
                    name,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(method, Is.Not.Null, name);
                Assert.That(method.IsPrivate, Is.True, name);
            }
        }

        [Test]
        public void Controller_RealSurfaceSessionOwnsGlobalGate_FloorTapBeginsPreview_AndUnchangedWallTargetCanRetarget()
        {
            var go = new GameObject("Task8RealSurfaceIntegration");
            var owned = new List<UnityEngine.Object>();
            try
            {
                var controller = go.AddComponent<DecorationModeController>();
                var room = CreateRoomLayout();
                var styles = new[]
                {
                    CreateStyle("floor.cream", SurfaceStyleKind.Floor, false, owned),
                    CreateStyle("paint.cream", SurfaceStyleKind.Paint, false, owned),
                    CreateStyle("wains.none", SurfaceStyleKind.Wainscoting, true, owned)
                };
                var mounted = new WallMountedLayout(new[]
                {
                    new WallSurfaceLayout("wall.back-left", 8, 2),
                    new WallSurfaceLayout("wall.back-right", 8, 2)
                });
                controller.ConfigurePhase7Runtime(room, styles, mounted, Array.Empty<WallMountedDefinitionAsset>());

                Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                Assert.That(controller.TrySelectFloorRange(SurfaceEditScope.SingleGridFloor), Is.True);
                Assert.That(controller.TryHandleSceneTap(new DecorationTouchHit(
                    DecorationTouchHitKind.FloorGrid,
                    floorPosition: new GridPosition(2, 3))), Is.True);
                Assert.That(controller.ActiveSurfacePreview, Is.Not.Null);
                Assert.That(controller.ActiveSurfacePreview.Scope, Is.EqualTo(SurfaceEditScope.SingleGridFloor));
                Assert.That(controller.ActiveSurfacePreview.SelectedFloorPosition, Is.EqualTo(new GridPosition(2, 3)));
                Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.False);
                var unsupportedRouter = new DecorationTouchRouter(8f, 0f);
                var unsupportedClassifier = new FixedClassifier(new DecorationTouchHit(
                    DecorationTouchHitKind.WallSurface,
                    surfaceId: "wall.back-right"));
                unsupportedRouter.ProcessFrame(
                    Frame(60, Point(21, 10, 10, InputTouchPhase.Began)),
                    unsupportedClassifier);
                var unsupportedTap = unsupportedRouter.ProcessFrame(
                    Frame(61, Point(21, 10, 10, InputTouchPhase.Ended)),
                    unsupportedClassifier);
                controller.RouteTouchResultForActiveMode(unsupportedTap);
                Assert.That(controller.ActiveSurfacePreview.SelectedFloorPosition, Is.EqualTo(new GridPosition(2, 3)));

                controller.CancelActivePhase7Preview();
                Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
                Assert.That(controller.TryBeginWallPreview(
                    "wall.back-left", SurfaceStyleKind.Paint, "paint.cream"), Is.True);
                // Coverage for Task 12: a Wall transaction with no changes may retarget.
                Assert.That(controller.TryHandleSceneTap(new DecorationTouchHit(
                    DecorationTouchHitKind.WallSurface,
                    surfaceId: "wall.back-right")), Is.True);
                Assert.That(controller.ActiveSurfacePreview.TargetWallSurfaceId,
                    Is.EqualTo("wall.back-right"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                foreach (var item in owned) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        public void Controller_WallMountedDragCrossesWalls_CornerIsInvalid_AndConfirmUsesCurrentValidSlot(
            int footprintHeight)
        {
            var go = new GameObject("Task8WallMountedIntegration");
            var sceneRoot = new GameObject("Task8WallMountedScene");
            var owned = new List<UnityEngine.Object>();
            try
            {
                var controller = go.AddComponent<DecorationModeController>();
                var mounted = new WallMountedLayout(new[]
                {
                    new WallSurfaceLayout("wall.back-left", 8, 2),
                    new WallSurfaceLayout("wall.back-right", 8, 2)
                });
                var definition = CreateWallMountedDefinition(
                    "decor.clock",
                    owned,
                    footprintHeight);
                controller.ConfigurePhase7Runtime(
                    CreateRoomLayout(),
                    new[]
                    {
                        CreateStyle("floor.cream", SurfaceStyleKind.Floor, false, owned),
                        CreateStyle("paint.cream", SurfaceStyleKind.Paint, false, owned),
                        CreateStyle("wains.none", SurfaceStyleKind.Wainscoting, true, owned)
                    },
                    mounted,
                    new[] { definition });
                var leftWall = CreateWallAuthoring("wall.back-left", sceneRoot.transform);
                var rightWall = CreateWallAuthoring("wall.back-right", sceneRoot.transform);
                rightWall.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                var projectionView = sceneRoot.AddComponent<WallMountedPreviewView>();
                var validMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                var invalidMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                owned.Add(validMaterial); owned.Add(invalidMaterial);
                projectionView.Configure(sceneRoot.transform, validMaterial, invalidMaterial);
                var mountedRegistry = sceneRoot.AddComponent<WallMountedSceneRegistry>();
                controller.ConfigurePhase7Scene(
                    new[] { leftWall, rightWall },
                    projectionView,
                    mountedSceneRegistry: mountedRegistry);
                Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
                Assert.That(controller.TryBeginWallMountedPreview(
                    "decor.clock", "wall.back-left", new WallSlotPosition(0, 0)), Is.True);
                Assert.That(projectionView.CurrentProjection, Is.Not.Null);
                Assert.That(projectionView.CurrentGhost, Is.Not.Null,
                    "The preview must instantiate the real wall-decor prefab, not only action buttons.");
                Assert.That(projectionView.CurrentGhost.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
                Assert.That(Vector3.Dot(projectionView.CurrentGhost.transform.forward, -leftWall.transform.forward),
                    Is.GreaterThan(.99f), "The real prefab ghost must face outward from the wall.");
                Assert.That(Vector3.Dot(projectionView.CurrentGhost.transform.up, Vector3.up),
                    Is.GreaterThan(.99f), "The real prefab ghost must stay upright.");
                Assert.That(Quaternion.Angle(
                    projectionView.CurrentGhost.transform.rotation,
                    leftWall.transform.rotation * Quaternion.Euler(0f, 180f, 0f)),
                    Is.LessThan(.01f));
                Assert.That(Vector3.Distance(
                    projectionView.CurrentGhost.transform.position,
                    projectionView.CurrentProjection.transform.position
                        - leftWall.transform.up * (definition.Footprint.Height * leftWall.SlotSize * .5f)),
                    Is.LessThan(.0001f));

                Assert.That(controller.TryHandleSceneDrag(new DecorationTouchHit(
                    DecorationTouchHitKind.WallSlot,
                    surfaceId: "wall.back-right",
                    wallSlotPosition: new WallSlotPosition(4, 0))), Is.True);
                Assert.That(controller.ActiveWallMountedPreview.SurfaceId, Is.EqualTo("wall.back-right"));
                Assert.That(controller.ActiveWallMountedPreview.IsValid, Is.True);
                Assert.That(projectionView.CurrentProjection.name, Does.Contain("ValidCheck"));
                Assert.That(Vector3.Dot(projectionView.CurrentGhost.transform.forward, -rightWall.transform.forward),
                    Is.GreaterThan(.99f));
                Assert.That(Vector3.Dot(projectionView.CurrentGhost.transform.up, Vector3.up),
                    Is.GreaterThan(.99f));
                Assert.That(Quaternion.Angle(
                    projectionView.CurrentGhost.transform.rotation,
                    rightWall.transform.rotation * Quaternion.Euler(0f, 180f, 0f)),
                    Is.LessThan(.01f),
                    "Cross-wall drag must rebuild pose from the current target Wall only.");
                Assert.That(Vector3.Distance(
                    projectionView.CurrentGhost.transform.position,
                    projectionView.CurrentProjection.transform.position
                        - rightWall.transform.up * (definition.Footprint.Height * rightWall.SlotSize * .5f)),
                    Is.LessThan(.0001f));

                Assert.That(controller.TryHandleSceneDrag(new DecorationTouchHit(
                    DecorationTouchHitKind.WallSlot,
                    surfaceId: "wall.back-right",
                    wallSlotPosition: new WallSlotPosition(8, 0))), Is.False);
                Assert.That(projectionView.CurrentProjection.name, Does.Contain("InvalidCross"));

                Assert.That(controller.TryHandleSceneDrag(default), Is.False);
                Assert.That(controller.ActiveWallMountedPreview.IsValid, Is.False);
                Assert.That(controller.ActiveWallMountedPreview.FailureReason,
                    Is.EqualTo(WallPlacementFailureReason.CrossCorner));
                Assert.That(projectionView.CurrentProjection, Is.Not.Null,
                    "Dragging between walls must retain the last footprint as an invalid red projection.");
                Assert.That(projectionView.CurrentProjection.name, Does.Contain("InvalidCross"));
                Assert.That(projectionView.CurrentGhost, Is.Not.Null,
                    "The real prefab ghost must remain visible while the placement is invalid.");
                Assert.That(controller.TryConfirmPhase7Preview(), Is.False);

                Assert.That(controller.TryHandleSceneDrag(new DecorationTouchHit(
                    DecorationTouchHitKind.WallSlot,
                    surfaceId: "wall.back-right",
                    wallSlotPosition: new WallSlotPosition(4, 0))), Is.True);
                var previewRootPosition = projectionView.CurrentGhost.transform.position;
                var previewRenderedCenter = projectionView.CurrentGhost
                    .GetComponentInChildren<Renderer>(true).bounds.center;
                Assert.That(controller.TryConfirmPhase7Preview(), Is.True);
                Assert.That(mounted.Surfaces["wall.back-right"].OccupiedSlotCount,
                    Is.EqualTo(footprintHeight));
                Assert.That(mounted.Surfaces["wall.back-left"].OccupiedSlotCount, Is.EqualTo(0));
                var confirmedInstances = mounted.CaptureSnapshot().Instances;
                Assert.That(confirmedInstances, Has.Count.EqualTo(1));
                var confirmedInstance = confirmedInstances[0];
                Assert.That(mountedRegistry.TryGet(
                    confirmedInstance.InstanceId,
                    out var confirmedRepresentation), Is.True);
                Assert.That(Mathf.Abs(Vector3.Dot(
                        previewRootPosition - confirmedRepresentation.transform.position,
                        rightWall.transform.up)),
                    Is.LessThan(.0001f),
                    "Confirm must not move the bottom-pivot root vertically.");
                Assert.That(Mathf.Abs(Vector3.Dot(
                        previewRenderedCenter - confirmedRepresentation
                            .GetComponentInChildren<Renderer>(true).bounds.center,
                        rightWall.transform.up)),
                    Is.LessThan(.0001f),
                    "Confirm must not make the rendered wall decor jump vertically.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(sceneRoot);
                foreach (var item in owned) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void Controller_WallMountedTapBeginsExistingRealPreviewWithoutRotatePath()
        {
            var go = new GameObject("Task8ExistingWallMounted");
            var owned = new List<UnityEngine.Object>();
            try
            {
                var mounted = new WallMountedLayout(new[]
                {
                    new WallSurfaceLayout("wall.back-left", 8, 2),
                    new WallSurfaceLayout("wall.back-right", 8, 2)
                });
                Assert.That(mounted.Place(new WallMountedInstance(
                    "decor.instance.1", "decor.clock", "wall.back-left",
                    new WallSlotPosition(1, 0), new WallFootprint(1, 1))).Succeeded, Is.True);
                var controller = go.AddComponent<DecorationModeController>();
                controller.ConfigurePhase7Runtime(
                    CreateRoomLayout(),
                    new[]
                    {
                        CreateStyle("floor.cream", SurfaceStyleKind.Floor, false, owned),
                        CreateStyle("paint.cream", SurfaceStyleKind.Paint, false, owned),
                        CreateStyle("wains.none", SurfaceStyleKind.Wainscoting, true, owned)
                    },
                    mounted,
                    new[] { CreateWallMountedDefinition("decor.clock", owned) });
                Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);

                Assert.That(controller.TryHandleSceneTap(new DecorationTouchHit(
                    DecorationTouchHitKind.WallMounted,
                    targetId: "decor.instance.1")), Is.True);
                Assert.That(controller.ActiveWallMountedPreview.IsExisting, Is.True);
                Assert.That(controller.ActiveWallMountedPreview.InstanceId,
                    Is.EqualTo("decor.instance.1"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                foreach (var item in owned) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void Controller_OptionalUiWiringBlocksTabsAndRangeDuringPreview_ContinuePreserves_DiscardCancels()
        {
            var controllerRoot = new GameObject("Task8UiController");
            var uiRoot = new GameObject("Task8Ui", typeof(RectTransform));
            var owned = new List<UnityEngine.Object>();
            try
            {
                uiRoot.SetActive(false);
                var tabs = uiRoot.AddComponent<DecorationModeTabsView>();
                var furniture = CreateButton("Furniture", uiRoot.transform);
                var floor = CreateButton("Floor", uiRoot.transform);
                var wall = CreateButton("Wall", uiRoot.transform);
                var decor = CreateButton("WallDecor", uiRoot.transform);
                Set(tabs, "furnitureButton", furniture); Set(tabs, "floorButton", floor);
                Set(tabs, "wallButton", wall); Set(tabs, "wallDecorButton", decor);
                var rangeRoot = new GameObject("Range", typeof(RectTransform));
                rangeRoot.transform.SetParent(uiRoot.transform, false);
                var range = rangeRoot.AddComponent<DecorationFloorRangeView>();
                var whole = CreateButton("Whole", rangeRoot.transform);
                var single = CreateButton("Single", rangeRoot.transform);
                range.Configure(whole, single);
                var exitRoot = new GameObject("ExitModal", typeof(RectTransform));
                exitRoot.transform.SetParent(uiRoot.transform, false);
                var exit = exitRoot.AddComponent<DecorationExitModalView>();
                var continueButton = CreateButton("Continue", exitRoot.transform);
                var discardButton = CreateButton("Discard", exitRoot.transform);
                Set(exit, "continueButton", continueButton); Set(exit, "discardButton", discardButton);
                uiRoot.SetActive(true);

                var controller = controllerRoot.AddComponent<DecorationModeController>();
                controller.ConfigurePhase7Runtime(
                    CreateRoomLayout(),
                    new[]
                    {
                        CreateStyle("floor.cream", SurfaceStyleKind.Floor, false, owned),
                        CreateStyle("paint.cream", SurfaceStyleKind.Paint, false, owned),
                        CreateStyle("wains.none", SurfaceStyleKind.Wainscoting, true, owned)
                    },
                    new WallMountedLayout(new[]
                    {
                        new WallSurfaceLayout("wall.back-left", 8, 2),
                        new WallSurfaceLayout("wall.back-right", 8, 2)
                    }),
                    Array.Empty<WallMountedDefinitionAsset>());
                controller.ConfigurePhase7Ui(tabs, range, exit);
                // This is an optional-UI wiring fixture without the legacy Phase 6
                // dependencies required by EnterDecorationMode. Model the open-state
                // precondition, then let the production mode change own range visibility.
                Set(controller, "isOpen", true);
                tabs.gameObject.SetActive(true);
                Assert.That(tabs.RequestMode(DecorationModeKind.Floor), Is.True);
                single.onClick.Invoke();
                Assert.That(controller.ActiveMode, Is.EqualTo(DecorationModeKind.Floor));
                Assert.That(controller.FloorRange, Is.EqualTo(SurfaceEditScope.SingleGridFloor));
                Assert.That(controller.TryHandleSceneTap(new DecorationTouchHit(
                    DecorationTouchHitKind.FloorGrid,
                    floorPosition: new GridPosition(1, 2))), Is.True);

                Assert.That(tabs.RequestMode(DecorationModeKind.Wall), Is.False);
                whole.onClick.Invoke();
                Assert.That(controller.ActiveMode, Is.EqualTo(DecorationModeKind.Floor));
                Assert.That(controller.FloorRange, Is.EqualTo(SurfaceEditScope.SingleGridFloor));
                Assert.That(controller.TryRequestExit(), Is.False);
                continueButton.onClick.Invoke();
                Assert.That(controller.ActiveSurfacePreview, Is.Not.Null);

                Assert.That(controller.TryRequestExit(), Is.False);
                discardButton.onClick.Invoke();
                Assert.That(controller.ActiveSurfacePreview, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controllerRoot);
                UnityEngine.Object.DestroyImmediate(uiRoot);
                foreach (var item in owned) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void Controller_GenericCatalogueItemsDelegateToTheActiveRealSession()
        {
            var go = new GameObject("Task8CatalogueRouting");
            var owned = new List<UnityEngine.Object>();
            try
            {
                var controller = go.AddComponent<DecorationModeController>();
                var floorStyle = CreateStyle("floor.cream", SurfaceStyleKind.Floor, false, owned);
                var paintStyle = CreateStyle("paint.cream", SurfaceStyleKind.Paint, false, owned);
                var noneStyle = CreateStyle("wains.none", SurfaceStyleKind.Wainscoting, true, owned);
                var wallDefinition = CreateWallMountedDefinition("decor.clock", owned);
                controller.ConfigurePhase7Runtime(
                    CreateRoomLayout(),
                    new[] { floorStyle, paintStyle, noneStyle },
                    new WallMountedLayout(new[]
                    {
                        new WallSurfaceLayout("wall.back-left", 8, 2),
                        new WallSurfaceLayout("wall.back-right", 8, 2)
                    }),
                    new[] { wallDefinition });

                Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                Assert.That(controller.TrySelectCatalogueItem(new DecorationCatalogueItemModel(
                    "floor.cream", "Floor", floorStyle.Thumbnail,
                    DecorationCatalogueItemKind.Floor, false)), Is.True);
                Assert.That(controller.ActiveSurfacePreview.Scope, Is.EqualTo(SurfaceEditScope.WholeRoomFloor));
                controller.CancelActivePhase7Preview();

                Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
                Assert.That(controller.TryHandleSceneTap(new DecorationTouchHit(
                    DecorationTouchHitKind.WallSurface,
                    surfaceId: "wall.back-left")), Is.True);
                Assert.That(controller.TrySelectCatalogueItem(new DecorationCatalogueItemModel(
                    "paint.cream", "Paint", paintStyle.Thumbnail,
                    DecorationCatalogueItemKind.WallSurface, false)), Is.True);
                Assert.That(controller.ActiveSurfacePreview.TargetWallSurfaceId,
                    Is.EqualTo("wall.back-left"));
                controller.CancelActivePhase7Preview();

                Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
                Assert.That(controller.TrySelectCatalogueItem(new DecorationCatalogueItemModel(
                    "decor.clock", "Clock", wallDefinition.Thumbnail,
                    DecorationCatalogueItemKind.WallMounted, false)), Is.True);
                Assert.That(controller.ActiveWallMountedPreview, Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                foreach (var item in owned) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void CafeLayoutRuntime_BuildsPhase7LayoutsFromCanonicalAuthoringIdsWithoutMutatingSources()
        {
            var runtimeObject = new GameObject("Task8Runtime");
            var leftObject = new GameObject("LeftWallAuthoring");
            var rightObject = new GameObject("RightWallAuthoring");
            try
            {
                var runtime = runtimeObject.AddComponent<CafeLayoutRuntime>();
                var left = leftObject.AddComponent<WallSurfaceAuthoring>();
                var right = rightObject.AddComponent<WallSurfaceAuthoring>();
                Set(left, "surfaceId", "wall.back-left");
                Set(left, "columns", 8);
                Set(left, "rows", 2);
                Set(right, "surfaceId", "wall.back-right");
                Set(right, "columns", 8);
                Set(right, "rows", 2);
                var leftState = (left.transform.position, left.gameObject.activeSelf, left.SurfaceId);
                var rightState = (right.transform.position, right.gameObject.activeSelf, right.SurfaceId);

                runtime.InitializePhase7Layouts(
                    "room.main",
                    new[] { left, right },
                    "paint.cream",
                    "floor.cream");

                Assert.That(runtime.RoomSurfaceLayout.Walls.Keys,
                    Is.EquivalentTo(new[] { "wall.back-left", "wall.back-right" }));
                Assert.That(runtime.RoomSurfaceLayout.FloorTiles.Count, Is.EqualTo(64));
                Assert.That(runtime.WallMountedLayout.Surfaces.Keys,
                    Is.EquivalentTo(new[] { "wall.back-left", "wall.back-right" }));
                Assert.That((left.transform.position, left.gameObject.activeSelf, left.SurfaceId), Is.EqualTo(leftState));
                Assert.That((right.transform.position, right.gameObject.activeSelf, right.SurfaceId), Is.EqualTo(rightState));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(runtimeObject);
                UnityEngine.Object.DestroyImmediate(leftObject);
                UnityEngine.Object.DestroyImmediate(rightObject);
            }
        }

        [Test]
        public void Controller_ProductionPhase7StartupConsumesSerializedCanonicalAuthoringAndCatalogues()
        {
            var root = new GameObject("Task8ProductionStartup");
            var leftObject = new GameObject("LeftWall");
            var rightObject = new GameObject("RightWall");
            var owned = new List<UnityEngine.Object>();
            try
            {
                var runtime = root.AddComponent<CafeLayoutRuntime>();
                var controller = root.AddComponent<DecorationModeController>();
                var left = leftObject.AddComponent<WallSurfaceAuthoring>();
                var right = rightObject.AddComponent<WallSurfaceAuthoring>();
                ConfigureWall(left, "wall.back-left");
                ConfigureWall(right, "wall.back-right");
                var floor = CreateStyle("floor.cream", SurfaceStyleKind.Floor, false, owned);
                var paint = CreateStyle("paint.cream", SurfaceStyleKind.Paint, false, owned);
                var none = CreateStyle("wains.none", SurfaceStyleKind.Wainscoting, true, owned);
                var clock = CreateWallMountedDefinition("decor.clock", owned);
                Set(controller, "layoutRuntime", runtime);
                Set(controller, "phase7WallAuthoring", new[] { left, right });
                Set(controller, "floorStyleCatalogue", CreateSurfaceCatalogue(
                    SurfaceStyleKind.Floor, new[] { floor }, owned));
                Set(controller, "paintStyleCatalogue", CreateSurfaceCatalogue(
                    SurfaceStyleKind.Paint, new[] { paint }, owned));
                Set(controller, "wallpaperStyleCatalogue", CreateSurfaceCatalogue(
                    SurfaceStyleKind.Wallpaper, Array.Empty<SurfaceStyleDefinitionAsset>(), owned));
                Set(controller, "wainscotingStyleCatalogue", CreateSurfaceCatalogue(
                    SurfaceStyleKind.Wainscoting, new[] { none }, owned));
                var wallDecorCatalogue = CreateWallCatalogue(
                    WallMountedCatalogueKind.WallDecor, owned);
                Set(wallDecorCatalogue, "entries", new List<WallMountedDefinitionAsset> { clock });
                Set(controller, "wallDecorCatalogue", wallDecorCatalogue);
                Set(controller, "windowCatalogue", CreateWallCatalogue(
                    WallMountedCatalogueKind.Windows, owned));
                var leftBefore = (left.transform.position, left.SurfaceId, left.Columns, left.Rows);
                var rightBefore = (right.transform.position, right.SurfaceId, right.Columns, right.Rows);
                var leftComponentsBefore = Array.ConvertAll(
                    left.GetComponents<Component>(), item => item.GetType().FullName);
                var rightComponentsBefore = Array.ConvertAll(
                    right.GetComponents<Component>(), item => item.GetType().FullName);

                Assert.That(controller.InitializePhase7RuntimeIfConfigured(), Is.True);

                Assert.That(runtime.RoomSurfaceLayout, Is.Not.Null);
                Assert.That(runtime.WallMountedLayout, Is.Not.Null);
                Assert.That((left.transform.position, left.SurfaceId, left.Columns, left.Rows),
                    Is.EqualTo(leftBefore));
                Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
                Assert.That(controller.TryBeginWallPreview(
                    "wall.back-left", SurfaceStyleKind.Paint, "paint.cream"), Is.True);
                AssertCanonicalSourcesUnchanged(left, right, leftBefore, rightBefore,
                    leftComponentsBefore, rightComponentsBefore);
                controller.CancelActivePhase7Preview();
                AssertCanonicalSourcesUnchanged(left, right, leftBefore, rightBefore,
                    leftComponentsBefore, rightComponentsBefore);

                Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
                Assert.That(controller.TryBeginWallMountedPreview(
                    "decor.clock", "wall.back-left", new WallSlotPosition(0, 0)), Is.True);
                AssertCanonicalSourcesUnchanged(left, right, leftBefore, rightBefore,
                    leftComponentsBefore, rightComponentsBefore);
                controller.CancelActivePhase7Preview();
                AssertCanonicalSourcesUnchanged(left, right, leftBefore, rightBefore,
                    leftComponentsBefore, rightComponentsBefore);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(leftObject);
                UnityEngine.Object.DestroyImmediate(rightObject);
                foreach (var item in owned) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        private static void AssertCanonicalSourcesUnchanged(
            WallSurfaceAuthoring left,
            WallSurfaceAuthoring right,
            (Vector3, string, int, int) leftBefore,
            (Vector3, string, int, int) rightBefore,
            string[] leftComponentsBefore,
            string[] rightComponentsBefore)
        {
            Assert.That((left.transform.position, left.SurfaceId, left.Columns, left.Rows),
                Is.EqualTo(leftBefore));
            Assert.That((right.transform.position, right.SurfaceId, right.Columns, right.Rows),
                Is.EqualTo(rightBefore));
            Assert.That(Array.ConvertAll(left.GetComponents<Component>(), item => item.GetType().FullName),
                Is.EqualTo(leftComponentsBefore));
            Assert.That(Array.ConvertAll(right.GetComponents<Component>(), item => item.GetType().FullName),
                Is.EqualTo(rightComponentsBefore));
        }

        [Test]
        public void Controller_Phase7ActionBarButtonsDriveLiveFloorSessionAndRestoreCatalogue()
        {
            var root = new GameObject("Task8Round2ActionController");
            var ui = new GameObject("Task8Round2ActionUi", typeof(RectTransform));
            var owned = new List<UnityEngine.Object>();
            try
            {
                var controller = root.AddComponent<DecorationModeController>();
                var floorCream = CreateStyle("floor.cream", SurfaceStyleKind.Floor, false, owned);
                var floorTerracotta = CreateStyle("floor.terracotta", SurfaceStyleKind.Floor, false, owned);
                var paint = CreateStyle("paint.cream", SurfaceStyleKind.Paint, false, owned);
                var none = CreateStyle("wains.none", SurfaceStyleKind.Wainscoting, true, owned);
                var layout = CreateRoomLayout();
                controller.ConfigurePhase7Runtime(layout,
                    new[] { floorCream, floorTerracotta, paint, none },
                    new WallMountedLayout(new[]
                    {
                        new WallSurfaceLayout("wall.back-left", 8, 2),
                        new WallSurfaceLayout("wall.back-right", 8, 2)
                    }), Array.Empty<WallMountedDefinitionAsset>());

                var action = CreateActionBar(ui.transform, out var actionButtons);
                var catalogueRoot = new GameObject("Catalogue", typeof(RectTransform));
                catalogueRoot.transform.SetParent(ui.transform, false);
                var catalogue = catalogueRoot.AddComponent<DecorationCatalogueView>();
                var rows = new GameObject("Rows", typeof(RectTransform));
                rows.transform.SetParent(catalogueRoot.transform, false);
                Set(catalogue, "categoryContent", rows.GetComponent<RectTransform>());
                Set(controller, "actionBarView", action);
                Set(controller, "catalogueView", catalogue);
                var storeModal = new GameObject("StoreModal", typeof(RectTransform));
                storeModal.transform.SetParent(ui.transform, false);
                Set(controller, "storeModalView", storeModal.AddComponent<DecorationStoreModalView>());
                Invoke(controller, "SubscribeViewEvents");
                controller.ConfigurePhase7Catalogue(catalogue, new[]
                {
                    new DecorationCategoryModel("floor", "Floor", new[]
                    {
                        new DecorationCatalogueItemModel("floor.terracotta", "Terracotta", null,
                            DecorationCatalogueItemKind.Floor, false)
                    }),
                    new DecorationCategoryModel("wallpaper", "Wallpaper", new[]
                    {
                        new DecorationCatalogueItemModel("paint.cream", "Cream", null,
                            DecorationCatalogueItemKind.WallSurface, false)
                    })
                });
                Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                Assert.That(controller.TrySelectFloorRange(SurfaceEditScope.SingleGridFloor), Is.True);
                Assert.That(controller.TrySelectFloorTarget(new GridPosition(2, 3)), Is.True);

                var tile = catalogueRoot.GetComponentInChildren<DecorationCatalogueTileView>(true);
                Assert.That(tile, Is.Not.Null);
                Set(tile, "button", tile.GetComponent<Button>());
                tile.GetComponent<Button>().onClick.Invoke();

                Assert.That(action.IsVisible, Is.True);
                Assert.That(action.VisibleActionLabels,
                    Is.EqualTo(new[] { "Undo Last", "Rotate", "Apply All", "Cancel", "Confirm" }));
                var rotationBefore = controller.ActiveSurfacePreview.ArmedRotation;
                actionButtons["Rotate"].onClick.Invoke();
                Assert.That(controller.ActiveSurfacePreview.ArmedRotation, Is.Not.EqualTo(rotationBefore));
                actionButtons["UndoLast"].onClick.Invoke();
                Assert.That(controller.ActiveSurfacePreview.ArmedRotation, Is.EqualTo(rotationBefore));
                var beforeApplyAll = JsonUtility.ToJson(
                    controller.ActiveSurfacePreview.ProposedSnapshot);
                actionButtons["ApplyAll"].onClick.Invoke();
                Assert.That(controller.ActiveSurfacePreview.CanUndo, Is.True);
                actionButtons["UndoLast"].onClick.Invoke();
                Assert.That(JsonUtility.ToJson(controller.ActiveSurfacePreview.ProposedSnapshot),
                    Is.EqualTo(beforeApplyAll));
                actionButtons["ApplyAll"].onClick.Invoke();
                actionButtons["Confirm"].onClick.Invoke();
                Assert.That(controller.ActiveSurfacePreview, Is.Null);
                Assert.That(catalogue.SheetState, Is.EqualTo(DecorationSheetState.Expanded));
                foreach (var floor in layout.FloorTiles.Values)
                    Assert.That(floor.StyleId, Is.EqualTo("floor.terracotta"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(ui);
                foreach (var item in owned) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void Controller_WallDecorRoutesBlankDragToCameraWithoutStealingMountedDrag()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);

            var cameraBeforePan = fixture.CameraController.transform.position;
            fixture.Controller.RouteTouchResultForActiveMode(CreateRoutingResult(
                DecorationGestureOwner.Camera,
                new DecorationTouchHit(DecorationTouchHitKind.Scene),
                cameraPanRequested: true,
                cameraPanDelta: new Vector2(24f, 0f)));
            Assert.That(fixture.CameraController.transform.position, Is.Not.EqualTo(cameraBeforePan),
                "A drag beginning on blank Scene in Wall Decor mode must pan the Camera.");

            Assert.That(fixture.Controller.TryBeginWallMountedPreview(
                "decor.clock", "wall.back-left", new WallSlotPosition(0, 0)), Is.True);
            var cameraBeforeMountedDrag = fixture.CameraController.transform.position;
            fixture.Controller.RouteTouchResultForActiveMode(CreateRoutingResult(
                DecorationGestureOwner.SceneDrag,
                new DecorationTouchHit(
                    DecorationTouchHitKind.WallSlot,
                    surfaceId: "wall.back-left",
                    wallSlotPosition: new WallSlotPosition(0, 0)),
                currentHit: new DecorationTouchHit(
                    DecorationTouchHitKind.WallSlot,
                    surfaceId: "wall.back-right",
                    wallSlotPosition: new WallSlotPosition(2, 1)),
                sceneDragRequested: true,
                currentHitClassified: true));

            Assert.That(fixture.CameraController.transform.position, Is.EqualTo(cameraBeforeMountedDrag),
                "A drag owned by the Wall-mounted ghost must not also pan the Camera.");
            Assert.That(fixture.Controller.ActiveWallMountedPreview.SurfaceId,
                Is.EqualTo("wall.back-right"));
            Assert.That(fixture.Controller.ActiveWallMountedPreview.Position,
                Is.EqualTo(new WallSlotPosition(2, 1)));
        }

        private static DecorationTouchRoutingResult CreateRoutingResult(
            DecorationGestureOwner owner,
            DecorationTouchHit originHit,
            DecorationTouchHit currentHit = default,
            bool sceneDragRequested = false,
            bool cameraPanRequested = false,
            Vector2 cameraPanDelta = default,
            bool currentHitClassified = false)
        {
            var constructor = typeof(DecorationTouchRoutingResult)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single();
            return (DecorationTouchRoutingResult)constructor.Invoke(new object[]
            {
                owner,
                originHit,
                currentHit,
                false,
                false,
                default(Vector2),
                sceneDragRequested,
                default(Vector2),
                cameraPanRequested,
                cameraPanDelta,
                false,
                0f,
                currentHitClassified
            });
        }

        [Test]
        public void Controller_WholeRoomFloorPreviewDisablesSingleGridUtilities()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            Assert.That(fixture.Controller.TrySelectCatalogueItem(fixture.FloorItem), Is.True);

            foreach (var name in new[] { "UndoLast", "Rotate", "ApplyAll" })
            {
                var button = fixture.Action.GetComponentsInChildren<Button>(true)
                    .Single(item => item.name == name);
                Assert.That(button.interactable, Is.False,
                    name + " is a Single Grid utility and must be greyed out in Whole Room.");
            }
        }

        [Test]
        public void Controller_SingleGridFloorPreviewEnablesSingleGridUtilities()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            Assert.That(fixture.Controller.TrySelectFloorRange(SurfaceEditScope.SingleGridFloor), Is.True);
            Assert.That(fixture.Controller.TrySelectFloorTarget(new GridPosition(2, 3)), Is.True);
            Assert.That(fixture.Controller.TrySelectCatalogueItem(fixture.FloorItem), Is.True);

            foreach (var name in new[] { "UndoLast", "Rotate", "ApplyAll" })
            {
                var button = fixture.Action.GetComponentsInChildren<Button>(true)
                    .Single(item => item.name == name);
                Assert.That(button.interactable, Is.True,
                    name + " must be available for a Single Grid transaction.");
            }
        }

        [Test]
        public void Controller_ModeSpecificCatalogueRebindsAndPreviewGatePreservesCurrentRows()
        {
            var root = new GameObject("Task8Round2CatalogueController");
            var catalogueRoot = new GameObject("Catalogue", typeof(RectTransform));
            var owned = new List<UnityEngine.Object>();
            try
            {
                var controller = root.AddComponent<DecorationModeController>();
                var floor = CreateStyle("floor.cream", SurfaceStyleKind.Floor, false, owned);
                var paint = CreateStyle("paint.cream", SurfaceStyleKind.Paint, false, owned);
                var none = CreateStyle("wains.none", SurfaceStyleKind.Wainscoting, true, owned);
                controller.ConfigurePhase7Runtime(CreateRoomLayout(), new[] { floor, paint, none },
                    new WallMountedLayout(new[]
                    {
                        new WallSurfaceLayout("wall.back-left", 8, 2),
                        new WallSurfaceLayout("wall.back-right", 8, 2)
                    }), Array.Empty<WallMountedDefinitionAsset>());
                var catalogue = catalogueRoot.AddComponent<DecorationCatalogueView>();
                var rows = new GameObject("Rows", typeof(RectTransform));
                rows.transform.SetParent(catalogueRoot.transform, false);
                Set(catalogue, "categoryContent", rows.GetComponent<RectTransform>());
                controller.ConfigurePhase7Catalogue(catalogue, new[]
                {
                    new DecorationCategoryModel("floor", "Floor", new[] { new DecorationCatalogueItemModel(
                        "floor.cream", "Floor", null, DecorationCatalogueItemKind.Floor, false) }),
                    new DecorationCategoryModel("paint", "Paint", new[] { new DecorationCatalogueItemModel(
                        "paint.cream", "Paint", null, DecorationCatalogueItemKind.WallSurface, false) })
                });

                Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                Assert.That(rows.transform.Find("CategoryRow_floor"), Is.Not.Null);
                Assert.That(rows.transform.Find("CategoryRow_paint"), Is.Null);
                Assert.That(controller.TrySelectCatalogueItem(new DecorationCatalogueItemModel(
                    "floor.cream", "Floor", null, DecorationCatalogueItemKind.Floor, false)), Is.True);
                Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.False);
                Assert.That(rows.transform.Find("CategoryRow_floor"), Is.Not.Null);
                Assert.That(rows.transform.Find("CategoryRow_paint"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(catalogueRoot);
                foreach (var item in owned) UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void Controller_FullEnterSurfaceExitModal_ContinuePreservesAndDiscardRestoresExactlyOnce()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.IsOpen, Is.True);
            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Assert.That(fixture.CameraController.enabled, Is.False);
            Assert.That(fixture.SceneSuppressionCount, Is.EqualTo(1));

            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            Assert.That(fixture.Controller.TrySelectCatalogueItem(fixture.FloorItem), Is.True);
            var previewBefore = fixture.Controller.ActiveSurfacePreview;
            var previewSnapshotBefore = JsonUtility.ToJson(previewBefore.ProposedSnapshot);
            var requestsBeforeContinue = fixture.Time.SetRequests;
            Assert.That(fixture.Controller.TryRequestExit(), Is.False);
            Assert.That(fixture.ExitModal.gameObject.activeSelf, Is.True);
            fixture.Continue.onClick.Invoke();

            Assert.That(fixture.Controller.IsOpen, Is.True);
            Assert.That(fixture.Controller.ActiveMode, Is.EqualTo(DecorationModeKind.Floor));
            Assert.That(fixture.Controller.ActiveSurfacePreview.Scope, Is.EqualTo(previewBefore.Scope));
            Assert.That(JsonUtility.ToJson(fixture.Controller.ActiveSurfacePreview.ProposedSnapshot),
                Is.EqualTo(previewSnapshotBefore));
            Assert.That(fixture.Time.SetRequests, Is.EqualTo(requestsBeforeContinue));
            Assert.That(fixture.CameraController.enabled, Is.False);
            Assert.That(fixture.SceneSuppressionCount, Is.EqualTo(1));

            Assert.That(fixture.Controller.TryRequestExit(), Is.False);
            fixture.Discard.onClick.Invoke();
            Assert.That(fixture.Controller.IsOpen, Is.False);
            Assert.That(fixture.Controller.ActiveSurfacePreview, Is.Null);
            Assert.That(fixture.Time.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
            Assert.That(fixture.Time.SetRequests, Is.EqualTo(requestsBeforeContinue + 1));
            Assert.That(fixture.CameraController.enabled, Is.True);
            Assert.That(fixture.SceneSuppressionCount, Is.Zero);

            fixture.Discard.onClick.Invoke();
            fixture.Controller.ExitDecorationMode();
            fixture.Controller.enabled = false;
            Assert.That(fixture.Time.SetRequests, Is.EqualTo(requestsBeforeContinue + 1));
            Assert.That(fixture.SceneSuppressionCount, Is.Zero);
        }

        [Test]
        public void Controller_FloorCatalogueSelection_RefreshesPreviewOutlineAndClearsItOnCancel()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            Assert.That(fixture.Controller.TrySelectCatalogueItem(fixture.FloorItem), Is.True);
            var tile = Array.Find(fixture.Catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true),
                item => item.ItemId == fixture.FloorItem.ItemId);
            Assert.That(tile.transform.Find("PreviewOutline").gameObject.activeSelf, Is.True);
            Assert.That(tile.transform.Find("UsingCheck").gameObject.activeSelf, Is.False,
                "WholeRoom Floor never represents one tile as the currently-used style.");
            Assert.That(fixture.Controller.TryConfirmPhase7Preview(), Is.True);
            Assert.That(tile.transform.Find("UsingCheck").gameObject.activeSelf, Is.False);
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            tile = Array.Find(fixture.Catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true),
                item => item.ItemId == fixture.FloorItem.ItemId);
            Assert.That(tile.transform.Find("UsingCheck").gameObject.activeSelf, Is.False);
            Assert.That(fixture.Controller.TrySelectCatalogueItem(fixture.FloorItem), Is.True);
            fixture.Controller.CancelActivePhase7Preview();
            Assert.That(tile.transform.Find("PreviewOutline").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Coverage_Task13_ControllerRerendersAndClearsSingleGridFeedbackAcrossPreviewLifecycle()
        {
            // Coverage only: Task 13 production routing already exists before this test.
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            Assert.That(fixture.Controller.TrySelectFloorRange(SurfaceEditScope.SingleGridFloor), Is.True);
            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.FloorGrid,
                floorPosition: new GridPosition(1, 1))), Is.True);
            var root = fixture.FloorFeedbackRoot.Find("FloorSelectionFeedback");
            Assert.That(root.Find("SelectedOutline_1_1"), Is.Not.Null);
            Assert.That(root.Find("PreviewCheck_1_1"), Is.Null);

            Assert.That(fixture.Controller.TrySelectCatalogueItem(fixture.FloorItem), Is.True);
            CollectionAssert.AreEqual(
                new[] { new GridPosition(1, 1) },
                fixture.Controller.ActiveSurfacePreview.PreviewedFloorPositions);
            root = fixture.FloorFeedbackRoot.Find("FloorSelectionFeedback");
            Assert.That(root.Find("PreviewCheck_1_1"), Is.Not.Null);
            Assert.That(fixture.Controller.TryConfirmPhase7Preview(), Is.True);
            Assert.That(fixture.FloorFeedbackRoot.Find("FloorSelectionFeedback").gameObject.activeSelf,
                Is.False);

            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.FloorGrid,
                floorPosition: new GridPosition(2, 1))), Is.True);
            fixture.Controller.CancelActivePhase7Preview();
            Assert.That(fixture.FloorFeedbackRoot.Find("FloorSelectionFeedback").gameObject.activeSelf,
                Is.False);

            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.FloorGrid,
                floorPosition: new GridPosition(3, 1))), Is.True);
            fixture.Controller.ExitDecorationMode();
            Assert.That(fixture.FloorFeedbackRoot.Find("FloorSelectionFeedback").gameObject.activeSelf,
                Is.False);
        }

        [Test]
        public void Controller_WallIndicatorsShowCurrentOnTarget_PreviewUntilConfirm_AndNeverLeakWallAIntoWallB()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.WallSurface, surfaceId: "wall.back-left")), Is.True);
            Assert.That(fixture.Controller.TrySelectCatalogueItem(fixture.PaintItem), Is.True);
            Assert.That(fixture.Controller.TryConfirmPhase7Preview(), Is.True);
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.WallSurface, surfaceId: "wall.back-right")), Is.True);
            var tiles = fixture.Catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true);
            var cream = Array.Find(tiles, tile => tile.ItemId == "paint.cream");
            var sage = Array.Find(tiles, tile => tile.ItemId == fixture.PaintItem.ItemId);
            Assert.That(cream.transform.Find("UsingCheck").gameObject.activeSelf, Is.True,
                "Selecting Wall B must immediately show Wall B's own confirmed base style.");
            Assert.That(sage.transform.Find("UsingCheck").gameObject.activeSelf, Is.False,
                "Wall A's confirmed style must never leak into Wall B.");
            Assert.That(fixture.Controller.TrySelectCatalogueItem(fixture.PaintItem), Is.True);
            tiles = fixture.Catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true);
            cream = Array.Find(tiles, tile => tile.ItemId == "paint.cream");
            sage = Array.Find(tiles, tile => tile.ItemId == fixture.PaintItem.ItemId);
            Assert.That(cream.transform.Find("UsingCheck").gameObject.activeSelf, Is.True,
                "Wall B still uses its own confirmed paint.cream style.");
            Assert.That(sage.transform.Find("UsingCheck").gameObject.activeSelf, Is.False);
            Assert.That(sage.transform.Find("PreviewOutline").gameObject.activeSelf, Is.True);

            fixture.Controller.CancelActivePhase7Preview();
            Assert.That(cream.transform.Find("UsingCheck").gameObject.activeSelf, Is.False,
                "Cancel clears the selected wall target, so target-specific current checks must also clear.");
            Assert.That(sage.transform.Find("PreviewOutline").gameObject.activeSelf, Is.False,
                "Cancel must clear the preview outline without leaving stale state.");

            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.WallSurface, surfaceId: "wall.back-right")), Is.True);
            Assert.That(fixture.Controller.TrySelectCatalogueItem(fixture.PaintItem), Is.True);
            Assert.That(fixture.Controller.TryConfirmPhase7Preview(), Is.True);
            Assert.That(sage.transform.Find("UsingCheck").gameObject.activeSelf, Is.True,
                "Confirm must move the current check to the newly confirmed style.");
            Assert.That(sage.transform.Find("PreviewOutline").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void Controller_WallMountedInvalidPreviewDisablesRealConfirm_ValidEnablesAndCancelRestoresSheet()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            Assert.That(fixture.Controller.TryBeginWallMountedPreview(
                "decor.clock", "wall.back-left", new WallSlotPosition(0, 0)), Is.True);
            Assert.That(fixture.Confirm.interactable, Is.True);

            Assert.That(fixture.Controller.TryHandleSceneDrag(default), Is.False);
            Assert.That(fixture.Controller.ActiveWallMountedPreview.IsValid, Is.False);
            Assert.That(fixture.Confirm.interactable, Is.False);
            fixture.Confirm.onClick.Invoke();
            Assert.That(fixture.Controller.ActiveWallMountedPreview, Is.Not.Null);

            Assert.That(fixture.Controller.TryHandleSceneDrag(new DecorationTouchHit(
                DecorationTouchHitKind.WallSlot,
                surfaceId: "wall.back-right",
                wallSlotPosition: new WallSlotPosition(2, 1))), Is.True);
            Assert.That(fixture.Confirm.interactable, Is.True);
            fixture.Cancel.onClick.Invoke();
            Assert.That(fixture.Controller.ActiveWallMountedPreview, Is.Null);
            Assert.That(fixture.Catalogue.SheetState, Is.EqualTo(DecorationSheetState.Expanded));
        }

        [Test]
        public void Controller_NewWallMountedConfirmKeepsCompactSheetAndCommittedItemCanBeSelectedImmediately()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            Assert.That(fixture.Controller.TryBeginWallMountedPreview(
                "decor.clock", "wall.back-left", new WallSlotPosition(0, 0)), Is.True);

            fixture.Confirm.onClick.Invoke();

            Assert.That(fixture.Controller.ActiveWallMountedPreview, Is.Null);
            Assert.That(fixture.Catalogue.SheetState, Is.EqualTo(DecorationSheetState.CompactPreview),
                "Wall Decor Confirm must keep the catalogue compact so the scene remains tappable.");
            var committed = fixture.WallLayout.CaptureSnapshot().Instances
                .Single(item => item.InstanceId != "decor.existing");
            Assert.That(fixture.WallRegistry.TryGet(committed.InstanceId, out _), Is.True);
            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.WallMounted,
                targetId: committed.InstanceId)), Is.True,
                "A newly confirmed wall item must be selectable on the very next tap.");
            Assert.That(fixture.Controller.ActiveWallMountedPreview.InstanceId,
                Is.EqualTo(committed.InstanceId));
        }

        [Test]
        public void Controller_WallMountedActionBarShowsEverySpecificInvalidReasonAndValidClearsIt()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            Assert.That(fixture.Controller.TryBeginWallMountedPreview(
                "decor.clock", "wall.back-left", new WallSlotPosition(0, 0)), Is.True);

            var cases = new[]
            {
                (new DecorationTouchHit(DecorationTouchHitKind.WallSlot, surfaceId: "wall.back-left", wallSlotPosition: new WallSlotPosition(4, 0)), "Wall space already occupied"),
                (new DecorationTouchHit(DecorationTouchHitKind.WallSlot, surfaceId: "wall.back-left", wallSlotPosition: new WallSlotPosition(8, 0)), "Outside wall area"),
                (default(DecorationTouchHit), "Place the item fully on one wall"),
                (new DecorationTouchHit(DecorationTouchHitKind.WallSlot, surfaceId: "wall.missing", wallSlotPosition: new WallSlotPosition(0, 0)), "Wall surface unavailable")
            };
            foreach (var item in cases)
            {
                Assert.That(fixture.Controller.TryHandleSceneDrag(item.Item1), Is.False);
                Assert.That(fixture.Confirm.interactable, Is.False);
                Assert.That(fixture.Feedback.text, Is.EqualTo(item.Item2));
            }

            Assert.That(fixture.Controller.TryHandleSceneDrag(new DecorationTouchHit(
                DecorationTouchHitKind.WallSlot, surfaceId: "wall.back-right",
                wallSlotPosition: new WallSlotPosition(2, 1))), Is.True);
            Assert.That(fixture.Confirm.interactable, Is.True);
            Assert.That(fixture.Feedback.text, Is.Empty);
        }

        [Test]
        public void Controller_NoPreviewDirectExitAndDestroyCleanupAreIdempotent()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            var requestsAfterEnter = fixture.Time.SetRequests;
            Assert.That(fixture.Controller.TryRequestExit(), Is.True);
            Assert.That(fixture.Controller.IsOpen, Is.False);
            Assert.That(fixture.ExitModal.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Time.SetRequests, Is.EqualTo(requestsAfterEnter + 1));
            UnityEngine.Object.DestroyImmediate(fixture.Controller);
            Assert.That(fixture.Time.SetRequests, Is.EqualTo(requestsAfterEnter + 1));
            Assert.That(fixture.SceneSuppressionCount, Is.Zero);
        }

        [Test]
        public void Controller_FullEnterWallMountedExitModal_ContinueThenDiscardNeverConfirms()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            Assert.That(fixture.Controller.TryBeginWallMountedPreview(
                "decor.clock", "wall.back-left", new WallSlotPosition(1, 0)), Is.True);
            var requestsAfterEnter = fixture.Time.SetRequests;
            Assert.That(fixture.Controller.TryRequestExit(), Is.False);
            fixture.Continue.onClick.Invoke();
            Assert.That(fixture.Controller.IsOpen, Is.True);
            Assert.That(fixture.Controller.ActiveWallMountedPreview, Is.Not.Null);
            Assert.That(fixture.Time.SetRequests, Is.EqualTo(requestsAfterEnter));
            Assert.That(fixture.SceneSuppressionCount, Is.EqualTo(1));

            Assert.That(fixture.Controller.TryRequestExit(), Is.False);
            fixture.Discard.onClick.Invoke();
            Assert.That(fixture.Controller.IsOpen, Is.False);
            Assert.That(fixture.Controller.ActiveWallMountedPreview, Is.Null);
            Assert.That(fixture.Time.SetRequests, Is.EqualTo(requestsAfterEnter + 1));
            Assert.That(fixture.SceneSuppressionCount, Is.Zero);
        }

        [Test]
        public void Controller_ExistingWallMountedTapShowsExactStoreMatrixAndNewPreviewHasNoStoreOrRotate()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.WallMounted, targetId: "decor.existing")), Is.True);
            Assert.That(fixture.Projection.CurrentProjection, Is.Not.Null);
            Assert.That(fixture.WallRegistry.TryGet("decor.existing", out _), Is.True);
            Assert.That(fixture.Controller.ActiveWallMountedPreview.IsExisting, Is.True);
            Assert.That(fixture.Store.gameObject.activeSelf, Is.True);
            Assert.That(fixture.Rotate.gameObject.activeSelf, Is.False);
            Assert.That(GetActionLabels(fixture), Is.EqualTo(new[] { "Store", "Cancel", "Confirm" }));

            fixture.Cancel.onClick.Invoke();
            Assert.That(fixture.Controller.TryBeginWallMountedPreview(
                "decor.clock", "wall.back-right", new WallSlotPosition(0, 0)), Is.True);
            Assert.That(fixture.Store.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Rotate.gameObject.activeSelf, Is.False);
            Assert.That(GetActionLabels(fixture), Is.EqualTo(new[] { "Cancel", "Confirm" }));
        }

        [Test]
        public void Controller_WallMountedStoreButtonsDismissWithoutMutationThenConfirmRemovesAndReleasesSlot()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.WallMounted, targetId: "decor.existing")), Is.True);

            fixture.Store.onClick.Invoke();
            Assert.That(fixture.Controller.ActiveWallMountedPreview.IsStoreConfirmationPending, Is.True);
            Assert.That(fixture.StoreModal.IsOpen, Is.True);
            Assert.That(fixture.Catalogue.SheetState, Is.EqualTo(DecorationSheetState.Hidden));
            fixture.StoreCancel.onClick.Invoke();
            Assert.That(fixture.Controller.ActiveWallMountedPreview.IsStoreConfirmationPending, Is.False);
            Assert.That(fixture.WallLayout.TryGetInstance("decor.existing", out _), Is.True);
            Assert.That(fixture.Catalogue.SheetState, Is.EqualTo(DecorationSheetState.CompactPreview));

            fixture.Store.onClick.Invoke();
            fixture.StoreConfirm.onClick.Invoke();
            Assert.That(fixture.Controller.ActiveWallMountedPreview, Is.Null);
            Assert.That(fixture.WallLayout.TryGetInstance("decor.existing", out _), Is.False);
            Assert.That(fixture.Projection.CurrentProjection, Is.Null);
            Assert.That(fixture.WallRegistry.TryGet("decor.existing", out _), Is.False);
            Assert.That(fixture.Catalogue.SheetState, Is.EqualTo(DecorationSheetState.Expanded));
            Assert.That(fixture.WallLayout.Place(new WallMountedInstance(
                "decor.reused", "decor.clock", "wall.back-left",
                new WallSlotPosition(4, 0), new WallFootprint(1, 1))).Succeeded, Is.True);
        }

        [Test]
        public void Controller_WallMountedStoreConfirmRestoresOcclusionFadeImmediately()
        {
            using var fixture = new EnterControllerFixture();
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var fadeObject = new GameObject("StoreConfirmFadeView");
            var sourceMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var fadeTemplate = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            try
            {
                var blockerRenderer = blocker.GetComponent<Renderer>();
                blockerRenderer.sharedMaterial = sourceMaterial;
                var sentinelId = Shader.PropertyToID("_Phase7StoreSentinel");
                var originalBlock = new MaterialPropertyBlock();
                originalBlock.SetFloat(sentinelId, 17f);
                blockerRenderer.SetPropertyBlock(originalBlock);

                var fadeView = fadeObject.AddComponent<WallOcclusionFadeView>();
                fadeView.Configure(
                    fixture.CameraController.GetComponent<UnityEngine.Camera>(),
                    fixture.LeftWall.GetComponent<Renderer>(),
                    0.35f,
                    fadeTemplate);
                Set(fixture.Controller, "wallOcclusionFadeView", fadeView);

                fixture.Controller.EnterDecorationMode();
                Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
                Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                    DecorationTouchHitKind.WallMounted,
                    targetId: "decor.existing")), Is.True);
                fadeView.FadeRepresentations(new[] { blocker.transform });
                Assert.That(blockerRenderer.sharedMaterial, Is.Not.SameAs(sourceMaterial),
                    "The regression fixture must start with a genuinely faded blocker.");

                fixture.Store.onClick.Invoke();
                fixture.StoreConfirm.onClick.Invoke();

                Assert.That(blockerRenderer.sharedMaterial, Is.SameAs(sourceMaterial),
                    "Store Confirm is terminal and must restore blockers without waiting for a mode switch.");
                var restoredBlock = new MaterialPropertyBlock();
                blockerRenderer.GetPropertyBlock(restoredBlock);
                Assert.That(restoredBlock.GetFloat(sentinelId), Is.EqualTo(17f),
                    "Restoring the fade must preserve the blocker's original MaterialPropertyBlock.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fadeObject);
                UnityEngine.Object.DestroyImmediate(blocker);
                UnityEngine.Object.DestroyImmediate(sourceMaterial);
                UnityEngine.Object.DestroyImmediate(fadeTemplate);
            }
        }

        [Test]
        public void Controller_ReenterResetsControllerTabsAndFloorRangeVisualsToDefaults()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            Assert.That(fixture.Controller.TrySelectFloorRange(SurfaceEditScope.SingleGridFloor), Is.True);
            Assert.That(fixture.Tabs.ActiveMode, Is.EqualTo(DecorationModeKind.Floor));
            Assert.That(fixture.Range.SelectedRange, Is.EqualTo(SurfaceEditScope.SingleGridFloor));
            Assert.That(fixture.Controller.TryRequestExit(), Is.True);

            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.ActiveMode, Is.EqualTo(DecorationModeKind.Furniture));
            Assert.That(fixture.Controller.FloorRange, Is.EqualTo(SurfaceEditScope.WholeRoomFloor));
            Assert.That(fixture.Tabs.ActiveMode, Is.EqualTo(DecorationModeKind.Furniture));
            Assert.That(fixture.Range.SelectedRange, Is.EqualTo(SurfaceEditScope.WholeRoomFloor));
        }

        [Test]
        public void Controller_ModeTabFromTabsOnlyReopensExpandedCatalogue()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            fixture.Catalogue.SetSheetState(
                DecorationSheetState.TabsOnly,
                hasActivePreview: false);
            Assert.That(fixture.Catalogue.SheetState, Is.EqualTo(DecorationSheetState.TabsOnly));

            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Wall), Is.True);

            Assert.That(fixture.Catalogue.SheetState, Is.EqualTo(DecorationSheetState.Expanded),
                "Selecting any mode tab must reopen its catalogue instead of leaving only the tabs visible.");
            Assert.That(fixture.Catalogue.AreCategoryRowsVisible, Is.True);
        }

        [Test]
        public void Controller_WallModeGuidesTargetSelectionBeforeMaterialSelection()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();

            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
            Assert.That(fixture.Feedback.text, Is.EqualTo("Select a wall to edit"));
            Assert.That(fixture.Controller.TrySelectCatalogueItem(fixture.PaintItem), Is.False,
                "A wall material must not silently start before the player selects a wall.");
            Assert.That(fixture.Feedback.text, Is.EqualTo("Select a wall to edit"));

            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.WallSurface,
                surfaceId: "wall.back-left")), Is.True);
            Assert.That(fixture.Feedback.text, Is.Empty,
                "The persistent target instruction must clear once the wall preview begins.");
        }

        [Test]
        public void Controller_WallCancelClearsTargetAndRestoresSelectionGuidance()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.WallSurface,
                surfaceId: "wall.back-left")), Is.True);
            Assert.That(fixture.Controller.TrySelectCatalogueItem(fixture.PaintItem), Is.True);
            Assert.That(fixture.Feedback.text, Is.Empty);

            fixture.Cancel.onClick.Invoke();

            Assert.That(fixture.Controller.ActiveSurfacePreview, Is.Null);
            Assert.That(fixture.Catalogue.SheetState, Is.EqualTo(DecorationSheetState.Expanded));
            Assert.That(fixture.Feedback.text, Is.EqualTo("Select a wall to edit"),
                "Cancel clears the selected wall, so the persistent target guidance must return immediately.");
        }

        [Test]
        public void Controller_SingleGridFloorGuidesTargetSelectionBeforeTileSelection()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Floor), Is.True);

            Assert.That(fixture.Controller.TrySelectFloorRange(SurfaceEditScope.SingleGridFloor), Is.True);
            Assert.That(fixture.Feedback.text, Is.EqualTo("Select a floor grid to edit"));
            Assert.That(fixture.Controller.TrySelectCatalogueItem(fixture.FloorItem), Is.False,
                "Single Grid material selection must wait for a highlighted floor target.");
            Assert.That(fixture.Feedback.text, Is.EqualTo("Select a floor grid to edit"));

            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.FloorGrid,
                floorPosition: new GridPosition(2, 3))), Is.True);
            Assert.That(fixture.Feedback.text, Is.Empty,
                "The persistent target instruction must clear once the floor preview begins.");
        }

        [Test]
        public void Controller_ReenterRebindsFurnitureCatalogueInsteadOfLeavingPriorModeItems()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
            Assert.That(fixture.Catalogue.transform.Find("Rows/CategoryRow_paint"), Is.Not.Null);
            Assert.That(fixture.Controller.TryRequestExit(), Is.True);

            fixture.Controller.EnterDecorationMode();

            Assert.That(fixture.Controller.ActiveMode, Is.EqualTo(DecorationModeKind.Furniture));
            Assert.That(fixture.Catalogue.transform.Find("Rows/CategoryRow_furniture"), Is.Not.Null,
                "Furniture tab and catalogue content must reset as one state on re-entry.");
            var stalePaintRow = fixture.Catalogue.transform.Find("Rows/CategoryRow_paint");
            Assert.That(stalePaintRow == null || !stalePaintRow.gameObject.activeSelf, Is.True,
                "Prior-mode rows must stop rendering and intercepting clicks immediately on re-entry.");
            var tile = fixture.Catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Single(item => item.ItemId == fixture.FurnitureItem.ItemId
                    && item.gameObject.activeInHierarchy);
            tile.GetComponent<Button>().onClick.Invoke();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Wall), Is.False,
                "The rebound Furniture tile must be clickable and start a real furniture preview.");
        }

        [Test]
        public void Controller_WallToFurniturePreviewRestoresCompactFurnitureActions()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.WallSurface, surfaceId: "wall.back-left")), Is.True);
            Assert.That(fixture.Controller.TrySelectCatalogueItem(fixture.PaintItem), Is.True);
            Assert.That(fixture.Cancel.GetComponent<RectTransform>().rect.width,
                Is.GreaterThanOrEqualTo(136f),
                "The fixture must first enter Wall's full-text footer presentation.");

            fixture.Controller.CancelActivePhase7Preview();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
            Assert.That(fixture.Controller.TrySelectCatalogueItem(fixture.FurnitureItem), Is.True);
            Canvas.ForceUpdateCanvases();

            Assert.That(fixture.Cancel.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("×"));
            Assert.That(fixture.Rotate.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("R"));
            Assert.That(fixture.Confirm.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("✓"));
            foreach (var button in new[] { fixture.Cancel, fixture.Rotate, fixture.Confirm })
            {
                var rect = button.GetComponent<RectTransform>().rect;
                Assert.That(rect.width, Is.EqualTo(48f).Within(0.01f));
                Assert.That(rect.height, Is.EqualTo(48f).Within(0.01f));
            }
        }

        [TestCase("wall.back-left", false)]
        [TestCase("wall.back-right", true)]
        public void Controller_PreviewAndConfirmMountWallDecorFlushToBaseSurfaceOnBothWalls(
            string surfaceId,
            bool rotateWall)
        {
            using var fixture = new EnterControllerFixture();
            var wall = string.Equals(surfaceId, "wall.back-left", StringComparison.Ordinal)
                ? fixture.LeftWall
                : fixture.RightWall;
            wall.transform.localScale = Vector3.one;
            wall.GetComponent<Renderer>().enabled = false;
            wall.transform.localPosition = rotateWall ? new Vector3(3f, 0f, 2f) : Vector3.zero;
            wall.transform.localRotation = rotateWall
                ? Quaternion.Euler(0f, 90f, 0f)
                : Quaternion.identity;
            var wallVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallVisual.name = "WallVisual";
            wallVisual.transform.SetParent(wall.transform, false);
            wallVisual.transform.localPosition = Vector3.up;
            wallVisual.transform.localScale = new Vector3(8f, 2f, .1f);
            UnityEngine.Object.DestroyImmediate(wallVisual.GetComponent<Collider>());
            var finish = GameObject.CreatePrimitive(PrimitiveType.Cube);
            finish.name = "Phase7_WallFinish";
            finish.transform.SetParent(wall.transform, false);
            finish.transform.localPosition = new Vector3(0f, 1f, -.08f);
            finish.transform.localScale = new Vector3(8f, 2f, .02f);
            UnityEngine.Object.DestroyImmediate(finish.GetComponent<Collider>());
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = "Phase7_WainscotingRailLip";
            rail.transform.SetParent(wall.transform, false);
            rail.transform.localPosition = new Vector3(0f, .65f, -.15f);
            rail.transform.localScale = new Vector3(8f, .05f, .04f);
            UnityEngine.Object.DestroyImmediate(rail.GetComponent<Collider>());

            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            Assert.That(fixture.Controller.TryBeginWallMountedPreview(
                "decor.clock", surfaceId, new WallSlotPosition(0, 0)), Is.True);
            var projectionLocalPosition = wall.transform.InverseTransformPoint(
                fixture.Projection.CurrentProjection.transform.position);
            Assert.That(projectionLocalPosition.z, Is.LessThan(-.171f),
                "The footprint must render outside the outer rail so its fixed green never appears faded or hidden.");
            var previewLocalPosition = wall.transform.InverseTransformPoint(
                fixture.Projection.CurrentGhost.transform.position);
            Assert.That(previewLocalPosition.z, Is.EqualTo(-.091f).Within(.0001f),
                "Preview must sit 1 mm outside the Base Wall Surface; a decorative rail must not float the whole item away from the wall.");
            Assert.That(fixture.Controller.TryConfirmPhase7Preview(), Is.True);

            var confirmed = wall.transform.Cast<Transform>()
                .Single(item => item.name.StartsWith("WallMounted_", StringComparison.Ordinal));
            var confirmedLocalPosition = wall.transform.InverseTransformPoint(confirmed.position);
            Assert.That(confirmedLocalPosition.z, Is.EqualTo(-.091f).Within(.0001f),
                "Confirmed wall decor must keep the same flush Base Wall contact plane as its preview ghost.");
        }

        [Test]
        public void Controller_ExistingWallMountedMoveHidesOriginalAndCancelRestoresIt()
        {
            using var fixture = new EnterControllerFixture();
            var original = fixture.ExistingRepresentation;
            var originalPosition = original.transform.position;
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);

            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.WallMounted, targetId: "decor.existing")), Is.True);
            Assert.That(original.activeSelf, Is.False,
                "The confirmed source must disappear while its real prefab ghost is being moved.");
            Assert.That(fixture.Controller.TryHandleSceneDrag(new DecorationTouchHit(
                DecorationTouchHitKind.WallSlot,
                surfaceId: "wall.back-right",
                wallSlotPosition: new WallSlotPosition(2, 1))), Is.True);
            Assert.That(original.transform.position, Is.EqualTo(originalPosition),
                "Dragging must not move or duplicate the confirmed source before Confirm.");

            fixture.Controller.CancelActivePhase7Preview();

            Assert.That(original.activeSelf, Is.True);
            Assert.That(original.transform.position, Is.EqualTo(originalPosition));
            Assert.That(fixture.WallLayout.TryGetInstance("decor.existing", out var restored), Is.True);
            Assert.That(restored.SurfaceId, Is.EqualTo("wall.back-left"));
            Assert.That(restored.Position, Is.EqualTo(new WallSlotPosition(4, 0)));
        }

        [Test]
        public void Controller_ExistingWallMountedConfirmReusesAndMovesHiddenRepresentation()
        {
            using var fixture = new EnterControllerFixture();
            var original = fixture.ExistingRepresentation;
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.WallMounted, targetId: "decor.existing")), Is.True);
            Assert.That(original.activeSelf, Is.False,
                "The original must remain hidden until Confirm resolves the move.");
            Assert.That(fixture.Controller.TryHandleSceneDrag(new DecorationTouchHit(
                DecorationTouchHitKind.WallSlot,
                surfaceId: "wall.back-right",
                wallSlotPosition: new WallSlotPosition(2, 1))), Is.True);

            Assert.That(fixture.Controller.TryConfirmPhase7Preview(), Is.True);

            Assert.That(fixture.WallRegistry.TryGet("decor.existing", out var confirmed), Is.True);
            Assert.That(confirmed, Is.SameAs(original),
                "Confirm must reactivate and move the registered representation, not leave a duplicate behind.");
            Assert.That(confirmed.activeSelf, Is.True);
            Assert.That(confirmed.transform.parent, Is.SameAs(fixture.RightWall.transform));
            Assert.That(fixture.WallLayout.TryGetInstance("decor.existing", out var moved), Is.True);
            Assert.That(moved.SurfaceId, Is.EqualTo("wall.back-right"));
            Assert.That(moved.Position, Is.EqualTo(new WallSlotPosition(2, 1)));
        }

        [Test]
        public void Controller_WallMountedActionPresentationTracksGhostBoundsAndNeverShowsRotate()
        {
            using var fixture = new EnterControllerFixture();
            fixture.Controller.EnterDecorationMode();
            Assert.That(fixture.Controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            Assert.That(fixture.Controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.WallMounted, targetId: "decor.existing")), Is.True);
            var camera = fixture.CameraController.GetComponent<UnityEngine.Camera>();
            var ghostBefore = camera.WorldToScreenPoint(fixture.Projection.CurrentGhost.transform.position);
            var preferredBefore = (Vector2)Invoke(
                fixture.Controller,
                "GetActionPresentationPreferredPoint",
                fixture.Projection.CurrentGhost.GetComponentInChildren<Renderer>().bounds);

            Assert.That(fixture.Controller.TryHandleSceneDrag(new DecorationTouchHit(
                DecorationTouchHitKind.WallSlot,
                surfaceId: "wall.back-left",
                wallSlotPosition: new WallSlotPosition(7, 1))), Is.True);
            var ghostAfter = camera.WorldToScreenPoint(fixture.Projection.CurrentGhost.transform.position);
            var preferredAfter = (Vector2)Invoke(
                fixture.Controller,
                "GetActionPresentationPreferredPoint",
                fixture.Projection.CurrentGhost.GetComponentInChildren<Renderer>().bounds);

            Assert.That(Vector2.Distance(ghostAfter, ghostBefore), Is.GreaterThan(1f));
            Assert.That(Vector2.Distance(preferredAfter, preferredBefore), Is.GreaterThan(1f),
                "The controller action presentation target must track the wall ghost bounds after a drag.");
            Assert.That(fixture.Action.IsVisible, Is.True);
            Assert.That(fixture.Rotate.gameObject.activeSelf, Is.False,
                "Wall Decor follows Furniture move controls but never exposes Rotate.");
        }

        private static string[] GetActionLabels(EnterControllerFixture fixture) =>
            ((DecorationActionBarView)fixture.Store.GetComponentInParent<DecorationActionBarView>())
                .VisibleActionLabels;

        private static DecorationTouchFrame Frame(int frame, params DecorationTouchPoint[] points) =>
            new DecorationTouchFrame(frame, points);

        private static DecorationTouchPoint Point(
            int id, float x, float y, InputTouchPhase phase, float dx = 0, float dy = 0) =>
            new DecorationTouchPoint(id, new Vector2(x, y), new Vector2(dx, dy), phase);

        private sealed class EnterControllerFixture : IDisposable
        {
            private readonly List<UnityEngine.Object> owned = new();
            private readonly SceneInteractionController sceneInteraction;
            private readonly GameObject infrastructureRoot;

            public EnterControllerFixture()
            {
                var runtimeObject = Own(new GameObject("Runtime"));
                infrastructureRoot = runtimeObject;
                var entranceObject = Own(new GameObject("Entrance"));
                var cameraObject = Own(new GameObject("Camera"));
                var gridRootObject = Own(new GameObject("GridRoot"));
                FloorFeedbackRoot = gridRootObject.transform;
                var formalRoot = Own(new GameObject("FormalRoot"));
                var previewRoot = Own(new GameObject("PreviewRoot"));
                var gridVisualRoot = Own(new GameObject("GridVisualRoot"));
                var floor = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
                floor.transform.SetParent(gridRootObject.transform, false);
                floor.transform.localPosition = new Vector3(4f, -0.1f, 4f);
                floor.transform.localScale = new Vector3(8f, 0.2f, 8f);

                var furniturePrefab = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
                var furniture = Own(ScriptableObject.CreateInstance<FurnitureDefinitionAsset>());
                Set(furniture, "definitionId", "furniture.counter.module.01");
                Set(furniture, "displayName", "Fixture");
                Set(furniture, "footprintWidth", 1);
                Set(furniture, "footprintDepth", 1);
                Set(furniture, "prefab", furniturePrefab);
                Set(furniture, "allowedPlacementSurfaces", PlacementSurfaceType.Floor);
                var content = Own(ScriptableObject.CreateInstance<FurnitureContentCatalog>());
                Set(content, "entries", new List<FurnitureDefinitionAsset> { furniture });
                var catalogueAsset = Own(ScriptableObject.CreateInstance<DecorationCatalogueAsset>());
                Set(catalogueAsset, "entries", new List<DecorationCatalogueEntry>());

                var entrance = entranceObject.AddComponent<EntrancePortalAuthoring>();
                Set(entrance, "entranceId", "entrance.main");
                Set(entrance, "originX", 3);
                Set(entrance, "originY", 0);
                var runtime = runtimeObject.AddComponent<CafeLayoutRuntime>();
                Set(runtime, "contentCatalog", content);
                Set(runtime, "entrancePortal", entrance);
                runtime.Initialize();

                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.orthographic = true;
                camera.transform.position = new Vector3(4f, 10f, 4f);
                camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var cameraSettings = Own(ScriptableObject.CreateInstance<CameraSettings>());
                cameraSettings.DragThresholdPixels = 6f;
                cameraSettings.PositionMin = new Vector2(-100f, -100f);
                cameraSettings.PositionMax = new Vector2(100f, 100f);
                cameraSettings.MinOrthographicSize = 1f;
                cameraSettings.MaxOrthographicSize = 20f;
                var cameraInput = cameraObject.AddComponent<QueuedCameraInput>();
                CameraController = cameraObject.AddComponent<CafeCameraController>();
                CameraController.Configure(camera, cameraSettings, cameraInput);
                sceneInteraction = runtimeObject.AddComponent<SceneInteractionController>();
                sceneInteraction.Configure(camera, cameraInput, new UiPointerBoundary());

                var gridSpace = new DecorationGridSpace(runtime.Layout.GridSettings,
                    new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8)));
                var theme = Own(ScriptableObject.CreateInstance<AnimalCafeUiTheme>());
                var material = Own(new Material(Shader.Find("Universal Render Pipeline/Unlit")));
                var registry = runtimeObject.AddComponent<FurnitureSceneRegistry>();
                registry.Configure(content, formalRoot.transform, gridSpace);
                registry.Rebuild(runtime.Layout.FurnitureInstances);
                var preview = runtimeObject.AddComponent<FurniturePreviewView>();
                preview.Configure(previewRoot.transform, gridSpace, theme);
                var grid = runtimeObject.AddComponent<GridHighlightView>();
                grid.Configure(gridVisualRoot.transform, gridSpace, material, theme);
                FloorFeedbackView = runtimeObject.AddComponent<FloorSurfaceGridView>();
                FloorFeedbackView.Configure(
                    gridRootObject.transform,
                    floor.GetComponent<Renderer>(),
                    1f);
                FloorFeedbackView.ConfigureSelectionFeedback(
                    Own(new Material(Shader.Find("Universal Render Pipeline/Unlit"))));
                var cameraDriver = runtimeObject.AddComponent<DecorationCameraDriver>();
                cameraDriver.Configure(CameraController);

                var ui = Own(new GameObject("UI", typeof(RectTransform)));
                Catalogue = ui.AddComponent<DecorationCatalogueView>();
                var rows = Own(new GameObject("Rows", typeof(RectTransform)));
                rows.transform.SetParent(ui.transform, false);
                Set(Catalogue, "categoryContent", rows.GetComponent<RectTransform>());
                Action = CreateActionBar(ui.transform, out var buttons);
                Feedback = Action.transform.Find("Feedback").GetComponent<TextMeshProUGUI>();
                Confirm = buttons["Confirm"];
                Cancel = buttons["Cancel"];
                Store = buttons["Store"];
                Rotate = buttons["Rotate"];
                var storeObject = Own(new GameObject("Store", typeof(RectTransform), typeof(CanvasGroup)));
                var sharedStoreModal = storeObject.AddComponent<AnimalCafeModalView>();
                StoreModal = storeObject.AddComponent<DecorationStoreModalView>();
                StoreConfirm = CreateButton("StoreConfirm", storeObject.transform);
                StoreCancel = CreateButton("StoreCancel", storeObject.transform);
                var storeBlocker = CreateButton("StoreBlocker", storeObject.transform);
                Set(StoreModal, "modalView", sharedStoreModal);
                Set(StoreModal, "confirmButton", StoreConfirm);
                Set(StoreModal, "cancelButton", StoreCancel);
                Set(StoreModal, "modalBlocker", storeBlocker);
                Set(StoreModal, "canvasGroup", storeObject.GetComponent<CanvasGroup>());
                Tabs = Own(new GameObject("Tabs", typeof(RectTransform)))
                    .AddComponent<DecorationModeTabsView>();
                Range = Own(new GameObject("Range", typeof(RectTransform)))
                    .AddComponent<DecorationFloorRangeView>();
                var modalObject = Own(new GameObject("ExitModal", typeof(RectTransform)));
                ExitModal = modalObject.AddComponent<DecorationExitModalView>();
                Continue = CreateButton("Continue", modalObject.transform);
                Discard = CreateButton("Discard", modalObject.transform);
                Set(ExitModal, "continueButton", Continue);
                Set(ExitModal, "discardButton", Discard);
                var hud = CreateButton("Hud", ui.transform);
                var hudLabel = Own(new GameObject("HudLabel", typeof(RectTransform)))
                    .AddComponent<TextMeshProUGUI>();
                var timePanel = Own(new GameObject("TimePanel")).AddComponent<TimeControlPanel>();

                var floorCream = CreateStyle("floor.cream", SurfaceStyleKind.Floor, false, owned);
                var floorTerracotta = CreateStyle("floor.terracotta", SurfaceStyleKind.Floor, false, owned);
                var paint = CreateStyle("paint.cream", SurfaceStyleKind.Paint, false, owned);
                var paintSage = CreateStyle("paint.sage", SurfaceStyleKind.Paint, false, owned);
                var none = CreateStyle("wains.none", SurfaceStyleKind.Wainscoting, true, owned);
                var wallDefinition = CreateWallMountedDefinition("decor.clock", owned);
                FloorItem = new DecorationCatalogueItemModel("floor.terracotta", "Terracotta", null,
                    DecorationCatalogueItemKind.Floor, false);
                PaintItem = new DecorationCatalogueItemModel("paint.sage", "Sage", null,
                    DecorationCatalogueItemKind.WallSurface, false);
                FurnitureItem = new DecorationCatalogueItemModel(
                    furniture.DefinitionId,
                    furniture.DisplayName,
                    null,
                    DecorationCatalogueItemKind.Furniture,
                    false,
                    furniture);

                Time = new FakeGameTimeService();
                var controllerObject = Own(new GameObject("Controller"));
                controllerObject.SetActive(false);
                Controller = controllerObject.AddComponent<DecorationModeController>();
                Set(Controller, "layoutRuntime", runtime); Set(Controller, "contentCatalog", content);
                Set(Controller, "catalogueAsset", catalogueAsset); Set(Controller, "targetCamera", camera);
                Set(Controller, "cameraSettings", cameraSettings); Set(Controller, "cameraController", CameraController);
                Set(Controller, "sceneInteraction", sceneInteraction); Set(Controller, "floorCollider", floor.GetComponent<Collider>());
                Set(Controller, "gridRoot", gridRootObject.transform); Set(Controller, "sceneRegistry", registry);
                Set(Controller, "previewView", preview); Set(Controller, "gridView", grid);
                Set(Controller, "floorSurfaceGridView", FloorFeedbackView);
                Set(Controller, "cameraDriver", cameraDriver); Set(Controller, "catalogueView", Catalogue);
                Set(Controller, "actionBarView", Action); Set(Controller, "storeModalView", StoreModal);
                Set(Controller, "decorationModeButton", hud); Set(Controller, "decorationModeButtonLabel", hudLabel);
                Set(Controller, "timeControlPanel", timePanel); Set(Controller, "gameTimeServiceOverride", Time);
                Set(Controller, "touchSourceOverride", new FakeTouchSource());
                Set(Controller, "mouseSourceOverride", new FakeMouseSource());
                Set(Controller, "gridSpace", gridSpace); Set(Controller, "runtimeBootstrapComplete", true);
                Set(Controller, "viewsConfigured", true);
                WallLayout = new WallMountedLayout(new[] { new WallSurfaceLayout("wall.back-left", 8, 2), new WallSurfaceLayout("wall.back-right", 8, 2) });
                Assert.That(WallLayout.Place(new WallMountedInstance(
                    "decor.existing", "decor.clock", "wall.back-left",
                    new WallSlotPosition(4, 0), new WallFootprint(1, 1))).Succeeded, Is.True);
                Controller.ConfigurePhase7Runtime(CreateRoomLayout(), new[] { floorCream, floorTerracotta, paint, paintSage, none },
                    WallLayout,
                    new[] { wallDefinition });
                LeftWall = CreateWallAuthoring("wall.back-left", gridRootObject.transform);
                RightWall = CreateWallAuthoring("wall.back-right", gridRootObject.transform);
                var projectionRoot = Own(new GameObject("ProjectionRoot"));
                Projection = projectionRoot.AddComponent<WallMountedPreviewView>();
                var validProjectionMaterial = Own(new Material(Shader.Find("Universal Render Pipeline/Unlit")));
                var invalidProjectionMaterial = Own(new Material(Shader.Find("Universal Render Pipeline/Unlit")));
                Projection.Configure(projectionRoot.transform, validProjectionMaterial, invalidProjectionMaterial);
                WallRegistry = runtimeObject.AddComponent<WallMountedSceneRegistry>();
                ExistingRepresentation = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
                WallRegistry.Register("decor.existing", ExistingRepresentation);
                Controller.ConfigurePhase7Scene(
                    new[] { LeftWall, RightWall }, Projection,
                    floorGridView: FloorFeedbackView,
                    mountedSceneRegistry: WallRegistry);
                var navigation = new UiNavigationCoordinator();
                var pause = new UiPauseCoordinator(Time);
                var boundary = new UiPointerBoundary();
                var transitions = new UiTransitionRunner(() => false);
                Set(Controller, "navigationCoordinator", navigation);
                Set(Controller, "pauseCoordinator", pause);
                Set(Controller, "pointerBoundary", boundary);
                Set(Controller, "transitionRunner", transitions);
                StoreModal.Configure(navigation, pause, boundary, transitions);
                Controller.ConfigurePhase7Ui(Tabs, Range, ExitModal);
                Controller.ConfigurePhase7Catalogue(Catalogue, new[]
                {
                    new DecorationCategoryModel("furniture", "Furniture", new[] { FurnitureItem }),
                    new DecorationCategoryModel("floor", "Floor", new[] { FloorItem }),
                    new DecorationCategoryModel("paint", "Paint", new[] {
                        new DecorationCatalogueItemModel("paint.cream", "Cream", null, DecorationCatalogueItemKind.WallSurface, false), PaintItem }),
                    new DecorationCategoryModel("decor", "Decor", new[] { new DecorationCatalogueItemModel(
                        "decor.clock", "Clock", null, DecorationCatalogueItemKind.WallMounted, false) })
                });
                controllerObject.SetActive(true);
            }

            public DecorationModeController Controller { get; }
            public FloorSurfaceGridView FloorFeedbackView { get; }
            public Transform FloorFeedbackRoot { get; }
            public DecorationCatalogueView Catalogue { get; }
            public DecorationExitModalView ExitModal { get; }
            public Button Continue { get; }
            public Button Discard { get; }
            public Button Confirm { get; }
            public Button Cancel { get; }
            public Button Store { get; }
            public Button Rotate { get; }
            public Button StoreConfirm { get; }
            public Button StoreCancel { get; }
            public DecorationStoreModalView StoreModal { get; }
            public DecorationActionBarView Action { get; }
            public DecorationModeTabsView Tabs { get; }
            public DecorationFloorRangeView Range { get; }
            public WallMountedLayout WallLayout { get; }
            public WallMountedPreviewView Projection { get; }
            public WallMountedSceneRegistry WallRegistry { get; }
            public WallSurfaceAuthoring LeftWall { get; }
            public WallSurfaceAuthoring RightWall { get; }
            public GameObject ExistingRepresentation { get; }
            public CafeCameraController CameraController { get; }
            public FakeGameTimeService Time { get; }
            public DecorationCatalogueItemModel FloorItem { get; }
            public DecorationCatalogueItemModel PaintItem { get; }
            public DecorationCatalogueItemModel FurnitureItem { get; }
            public TextMeshProUGUI Feedback { get; }
            public int SceneSuppressionCount
            {
                get
                {
                    var tokens = sceneInteraction.GetType()
                        .GetField("inputSuppressionTokens", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(sceneInteraction);
                    return (int)tokens.GetType().GetProperty("Count").GetValue(tokens);
                }
            }
            public void Dispose()
            {
                Controller?.ExitDecorationMode();
                if (infrastructureRoot != null) UnityEngine.Object.DestroyImmediate(infrastructureRoot);
                for (var i = owned.Count - 1; i >= 0; i--)
                    if (owned[i] != null) UnityEngine.Object.DestroyImmediate(owned[i]);
            }
            private T Own<T>(T value) where T : UnityEngine.Object { owned.Add(value); return value; }
        }

        private sealed class QueuedCameraInput : MonoBehaviour, AnimalCafe.Input.ICameraInputSource
        { public AnimalCafe.Input.CameraInputFrame ReadFrame() => default; }
        private sealed class FakeTouchSource : IDecorationTouchSource
        { public DecorationTouchFrame ReadFrame() => default; }
        private sealed class FakeMouseSource : IMouseDecorationInputSource
        {
            public bool HasActivePointer => false;
            public DecorationTouchFrame ReadFrame() => default;
            public float ReadScrollDelta() => 0f;
            public void Reset() { }
        }
        private sealed class FakeGameTimeService : IGameTimeService
        {
            public GameSpeed CurrentSpeed { get; private set; } = GameSpeed.Normal;
            public int SetRequests { get; private set; }
            public bool TrySetSpeed(GameSpeed speed) { SetRequests++; CurrentSpeed = speed; return true; }
        }

        private sealed class FixedClassifier : IDecorationTouchHitClassifier
        {
            private readonly DecorationTouchHit hit;
            public FixedClassifier(DecorationTouchHit hit) => this.hit = hit;
            public DecorationTouchHit ClassifyBegan(int touchId, Vector2 screenPosition) => hit;
        }


        private sealed class CrossingClassifier : IDecorationTouchHitClassifier
        {
            public DecorationTouchHit ClassifyBegan(int touchId, Vector2 screenPosition) =>
                new DecorationTouchHit(DecorationTouchHitKind.WallSlot, targetId: "wall.back-left:2:1");

            public DecorationTouchHit ClassifyCurrent(int touchId, Vector2 screenPosition) =>
                new DecorationTouchHit(DecorationTouchHitKind.WallSlot, targetId: "wall.back-right:4:1");
        }

        private sealed class CornerClassifier : IDecorationTouchHitClassifier
        {
            public DecorationTouchHit ClassifyBegan(int touchId, Vector2 screenPosition) =>
                new DecorationTouchHit(DecorationTouchHitKind.WallSlot,
                    surfaceId: "wall.back-left",
                    wallSlotPosition: new WallSlotPosition(2, 1));
            public DecorationTouchHit ClassifyCurrent(int touchId, Vector2 screenPosition) => default;
        }


        private static void Set(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var method = target.GetType().GetMethod(
                methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(target, arguments);
        }

        private static DecorationActionBarView CreateActionBar(
            Transform parent,
            out Dictionary<string, Button> buttons)
        {
            var root = new GameObject("ActionBar", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<DecorationActionBarView>();
            buttons = new Dictionary<string, Button>
            {
                ["UndoLast"] = CreateButton("UndoLast", root.transform),
                ["ApplyAll"] = CreateButton("ApplyAll", root.transform),
                ["Store"] = CreateButton("Store", root.transform),
                ["Rotate"] = CreateButton("Rotate", root.transform),
                ["Cancel"] = CreateButton("Cancel", root.transform),
                ["Confirm"] = CreateButton("Confirm", root.transform)
            };
            Set(view, "canvasGroup", root.GetComponent<CanvasGroup>());
            Set(view, "undoLastButton", buttons["UndoLast"]);
            Set(view, "applyAllButton", buttons["ApplyAll"]);
            Set(view, "storeButton", buttons["Store"]);
            Set(view, "rotateButton", buttons["Rotate"]);
            Set(view, "cancelButton", buttons["Cancel"]);
            Set(view, "confirmButton", buttons["Confirm"]);
            var feedback = new GameObject("Feedback", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            feedback.transform.SetParent(root.transform, false);
            Set(view, "feedbackLabel", feedback);
            return view;
        }

        private static RoomSurfaceLayout CreateRoomLayout()
        {
            var floors = new List<FloorTileAppearance>();
            for (var x = 0; x < 8; x++)
                for (var y = 0; y < 8; y++)
                    floors.Add(new FloorTileAppearance(
                        new GridPosition(x, y), "floor.cream", SurfaceRotation.Degrees0));
            return new RoomSurfaceLayout(
                "room.main",
                new[]
                {
                    new WallAppearance("wall.back-left", "paint.cream", null),
                    new WallAppearance("wall.back-right", "paint.cream", null)
                },
                floors);
        }

        private static SurfaceStyleDefinitionAsset CreateStyle(
            string id,
            SurfaceStyleKind kind,
            bool none,
            ICollection<UnityEngine.Object> owned)
        {
            var style = ScriptableObject.CreateInstance<SurfaceStyleDefinitionAsset>();
            var texture = new Texture2D(1, 1);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            owned.Add(style); owned.Add(sprite); owned.Add(texture);
            Set(style, "styleId", id);
            Set(style, "displayName", id);
            Set(style, "kind", kind);
            Set(style, "thumbnail", sprite);
            Set(style, "isNoneOption", none);
            if (!none)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                owned.Add(material);
                Set(style, "material", material);
            }
            return style;
        }

        private static WallMountedDefinitionAsset CreateWallMountedDefinition(
            string id,
            ICollection<UnityEngine.Object> owned,
            int footprintHeight = 1)
        {
            var definition = ScriptableObject.CreateInstance<WallMountedDefinitionAsset>();
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prefab.name = id + ".prefab";
            prefab.transform.localScale = new Vector3(.8f, .8f, .15f);
            var texture = new Texture2D(1, 1);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            owned.Add(definition); owned.Add(prefab); owned.Add(sprite); owned.Add(texture);
            Set(definition, "definitionId", id);
            Set(definition, "displayName", id);
            Set(definition, "footprintWidth", 1);
            Set(definition, "footprintHeight", footprintHeight);
            Set(definition, "prefab", prefab);
            Set(definition, "thumbnail", sprite);
            Set(definition, "maxVisualDepth", 0.1f);
            return definition;
        }

        private static Button CreateButton(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var button = go.AddComponent<Button>();
            var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(go.transform, false);
            return button;
        }

        private static WallSurfaceAuthoring CreateWallAuthoring(string id, Transform parent)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = id;
            wall.transform.SetParent(parent, false);
            wall.transform.localScale = new Vector3(8f, 2f, 0.1f);
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            Set(authoring, "surfaceId", id);
            Set(authoring, "columns", 8);
            Set(authoring, "rows", 2);
            Set(authoring, "slotSize", 1f);
            return authoring;
        }

        private static void ConfigureWall(WallSurfaceAuthoring authoring, string id)
        {
            Set(authoring, "surfaceId", id);
            Set(authoring, "columns", 8);
            Set(authoring, "rows", 2);
            Set(authoring, "slotSize", 1f);
        }

        private static SurfaceStyleCatalogueAsset CreateSurfaceCatalogue(
            SurfaceStyleKind kind,
            IEnumerable<SurfaceStyleDefinitionAsset> entries,
            ICollection<UnityEngine.Object> owned)
        {
            var catalogue = ScriptableObject.CreateInstance<SurfaceStyleCatalogueAsset>();
            owned.Add(catalogue);
            Set(catalogue, "kind", kind);
            Set(catalogue, "entries", new List<SurfaceStyleDefinitionAsset>(entries));
            return catalogue;
        }

        private static WallMountedCatalogueAsset CreateWallCatalogue(
            WallMountedCatalogueKind kind,
            ICollection<UnityEngine.Object> owned)
        {
            var catalogue = ScriptableObject.CreateInstance<WallMountedCatalogueAsset>();
            owned.Add(catalogue);
            Set(catalogue, "kind", kind);
            Set(catalogue, "entries", new List<WallMountedDefinitionAsset>());
            return catalogue;
        }
    }
}

#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    /// <summary>
    /// A visible Wall Decor ghost is the grab target on Began, while Current keeps
    /// tracking the physical wall behind the finger. 可见墙饰可直接抓取，拖动时仍按真实墙格移动。
    /// </summary>
    public sealed class P8RWallDecorDragAccessTests
    {
        private const string MainCafePath = "Assets/Scenes/MainCafe.unity";
        private const string MonitorDefinitionId = "wall-decor.monitor.01";
        private const string InitialSurfaceId = "wall.back-left";
        private static readonly WallSlotPosition InitialSlot = new WallSlotPosition(4, 0);
        private static readonly WallSlotPosition DragTargetSlot = new WallSlotPosition(3, 0);

        private Scene scene;
        private P8RReferenceLayoutTests.NativeScreenSize screen;
        private Vector2? previousLogicalViewport;
        private float previousTimeScale;
        private DecorationModeController controller;
        private CafeLayoutRuntime runtime;
        private WallMountedPreviewView previewView;
        private UnityEngine.Camera targetCamera;

        [SetUp]
        public void RememberEnvironment()
        {
            previousLogicalViewport = P8RMobileMetrics.EditorLogicalViewportOverride;
            previousTimeScale = Time.timeScale;
        }

        [UnityTearDown]
        public IEnumerator RestoreEnvironment()
        {
            if (screen != null)
            {
                screen.Dispose();
                screen = null;
                yield return null;
            }

            if (scene.IsValid() && scene.isLoaded)
            {
                var assets = Phase8SceneInputTestCleanup.CaptureAssets(scene);
                foreach (var item in Components<DecorationModeController>())
                    item.enabled = false;
                var cleanup = SceneManager.CreateScene("P8RWallDecorDragAccessCleanup");
                Assert.That(SceneManager.SetActiveScene(cleanup), Is.True);
                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone)
                    yield return null;
                Phase8SceneInputTestCleanup.DisposeReleasedAssets(assets);
            }

            scene = default;
            controller = null;
            runtime = null;
            previewView = null;
            targetCamera = null;
            P8RMobileMetrics.EditorLogicalViewportOverride = previousLogicalViewport;
            Time.timeScale = previousTimeScale;
        }

        [UnityTest]
        public IEnumerator MainCafeMonitorGhost_BeganOwnsVisibleEdge_ButCurrentTracksBackingWallSlot()
        {
            yield return LoadMonitorPreview();
            var classifier = (IDecorationTouchHitClassifier)controller;
            var ghostPoint = FindVisibleGhostPointWithoutItsOwnBackingSlot();

            var began = classifier.ClassifyBegan(501, ghostPoint);
            Assert.That(began.Kind, Is.EqualTo(DecorationTouchHitKind.WallSlot),
                "Began on the visible monitor must grab the Preview instead of the wall behind it.");
            Assert.That(began.SurfaceId, Is.EqualTo(InitialSurfaceId));
            Assert.That(began.WallSlotPosition, Is.EqualTo((WallSlotPosition?)InitialSlot));

            var currentAtGhost = classifier.ClassifyCurrent(501, ghostPoint);
            if (TryGetBackingWallSlot(ghostPoint, out var backingSurfaceId, out var backingSlot))
            {
                Assert.That(currentAtGhost.Kind, Is.EqualTo(DecorationTouchHitKind.WallSlot));
                Assert.That(currentAtGhost.SurfaceId, Is.EqualTo(backingSurfaceId));
                Assert.That(currentAtGhost.WallSlotPosition,
                    Is.EqualTo((WallSlotPosition?)backingSlot));
                Assert.That(backingSurfaceId != InitialSurfaceId || backingSlot != InitialSlot,
                    Is.True, "The witness must independently differ from the Preview Slot.");
            }
            else
            {
                Assert.That(currentAtGhost.Kind, Is.EqualTo(DecorationTouchHitKind.Scene),
                    "Current over a ghost pixel with no backing wall must remain Scene.");
            }

            var dragTarget = ScreenPointForWallSlot(InitialSurfaceId, DragTargetSlot);
            var current = classifier.ClassifyCurrent(501, dragTarget);
            Assert.That(current.Kind, Is.EqualTo(DecorationTouchHitKind.WallSlot));
            Assert.That(current.SurfaceId, Is.EqualTo(InitialSurfaceId));
            Assert.That(current.WallSlotPosition, Is.EqualTo((WallSlotPosition?)DragTargetSlot),
                "Current must read the physical wall Slot, not stick to the ghost's original Slot.");
        }

        [UnityTest]
        public IEnumerator MainCafeMonitorPreview_ActualActionButtonIsUi_AndUiFreeSkyIsScene()
        {
            yield return LoadMonitorPreview();
            var classifier = (IDecorationTouchHitClassifier)controller;
            var action = Find<DecorationActionBarView>();
            var cancel = action.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "CancelButton");
            Assert.That(cancel.gameObject.activeInHierarchy, Is.True);

            var cancelPoint = P8RWallDecorActionAvoidanceTests.UiBounds(
                (RectTransform)cancel.transform).center;
            Assert.That(classifier.ClassifyBegan(511, cancelPoint).Kind,
                Is.EqualTo(DecorationTouchHitKind.Ui),
                "The real Cancel hit root must retain UI ownership.");

            var blankPoint = FindUiFreePointWithoutWall();
            Assert.That(classifier.ClassifyBegan(512, blankPoint).Kind,
                Is.EqualTo(DecorationTouchHitKind.Scene),
                "A UI-free point with no physical wall behind it must retain Camera ownership.");
        }

        [UnityTest]
        public IEnumerator MainCafeMonitorGhost_RealRouterDragMovesPreviewWithoutCamera_AndCancelLeavesLayoutUntouched()
        {
            yield return LoadMonitorPreview();
            var classifier = (IDecorationTouchHitClassifier)controller;
            var start = FindVisibleGhostPointWithoutItsOwnBackingSlot();
            var dragTarget = ScreenPointForWallSlot(InitialSurfaceId, DragTargetSlot);
            var formalBefore = JsonUtility.ToJson(runtime.WallMountedLayout.CaptureSnapshot());
            var cameraPositionBefore = targetCamera.transform.position;
            var cameraRotationBefore = targetCamera.transform.rotation;
            var cameraSizeBefore = targetCamera.orthographicSize;
            var router = new DecorationTouchRouter(8f, 0f);

            var began = router.ProcessFrame(
                Frame(1, Point(521, start, Vector2.zero, InputTouchPhase.Began)), classifier);
            Assert.That(began.Owner, Is.EqualTo(DecorationGestureOwner.SceneDrag));
            Assert.That(began.OriginHit.SurfaceId, Is.EqualTo(InitialSurfaceId));
            Assert.That(began.OriginHit.WallSlotPosition, Is.EqualTo((WallSlotPosition?)InitialSlot));

            var moved = router.ProcessFrame(
                Frame(2, Point(521, dragTarget, dragTarget - start,
                    InputTouchPhase.Moved)), classifier);
            Assert.That(moved.SceneDragRequested, Is.True);
            Assert.That(moved.CameraPanRequested, Is.False);
            Assert.That(moved.CurrentHit.SurfaceId, Is.EqualTo(InitialSurfaceId));
            Assert.That(moved.CurrentHit.WallSlotPosition,
                Is.EqualTo((WallSlotPosition?)DragTargetSlot));
            controller.RouteTouchResultForActiveMode(moved);

            Assert.That(controller.ActiveWallMountedPreview.SurfaceId,
                Is.EqualTo(InitialSurfaceId));
            Assert.That(controller.ActiveWallMountedPreview.Position,
                Is.EqualTo(DragTargetSlot),
                "The real router gesture must move the Preview model to the backing wall Slot.");
            AssertCameraUnchanged(cameraPositionBefore, cameraRotationBefore, cameraSizeBefore);
            Assert.That(JsonUtility.ToJson(runtime.WallMountedLayout.CaptureSnapshot()),
                Is.EqualTo(formalBefore), "Dragging Preview must not mutate formal Layout.");

            controller.RouteTouchResultForActiveMode(router.ProcessFrame(
                Frame(3, Point(521, dragTarget, Vector2.zero, InputTouchPhase.Ended)),
                classifier));
            controller.CancelActivePhase7Preview();
            Assert.That(controller.ActiveWallMountedPreview, Is.Null);
            Assert.That(JsonUtility.ToJson(runtime.WallMountedLayout.CaptureSnapshot()),
                Is.EqualTo(formalBefore), "Cancel must leave formal WallMountedLayout unchanged.");
            AssertCameraUnchanged(cameraPositionBefore, cameraRotationBefore, cameraSizeBefore);
        }

        [UnityTest]
        public IEnumerator MainCafeNewWallDecor_DragAcrossUiPreservesPreviewAndResumesOnWall()
        {
            return AssertDragAcrossUiPreservesPreview(existing: false);
        }

        [UnityTest]
        public IEnumerator MainCafeExistingWallDecor_DragAcrossUiPreservesPreviewAndResumesOnWall()
        {
            return AssertDragAcrossUiPreservesPreview(existing: true);
        }

        private IEnumerator AssertDragAcrossUiPreservesPreview(bool existing)
        {
            yield return LoadMonitorPreview();
            GameObject source = null;
            if (existing)
            {
                Assert.That(controller.TryConfirmPhase7Preview(), Is.True);
                var instance = runtime.WallMountedLayout.CaptureSnapshot().Instances.Single(item =>
                    item.DefinitionId == MonitorDefinitionId && item.SurfaceId == InitialSurfaceId
                    && item.Column == InitialSlot.Column && item.Row == InitialSlot.Row);
                Assert.That(Find<WallMountedSceneRegistry>().TryGet(instance.InstanceId, out source), Is.True);
                Assert.That(controller.TryHandleSceneTap(new DecorationTouchHit(
                    DecorationTouchHitKind.WallMounted, targetId: instance.InstanceId)), Is.True);
                Assert.That(source.activeSelf, Is.False);
                Canvas.ForceUpdateCanvases();
            }

            var classifier = (IDecorationTouchHitClassifier)controller;
            var start = FindVisibleGhostPointWithoutItsOwnBackingSlot();
            var cancel = Find<DecorationActionBarView>().GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "CancelButton");
            var uiPoint = P8RWallDecorActionAvoidanceTests.UiBounds((RectTransform)cancel.transform).center;
            var before = controller.ActiveWallMountedPreview;
            var ghostPosition = previewView.CurrentGhost.transform.position;
            var ghostRotation = previewView.CurrentGhost.transform.rotation;
            var formalBefore = JsonUtility.ToJson(runtime.WallMountedLayout.CaptureSnapshot());
            var cameraPosition = targetCamera.transform.position;
            var cameraRotation = targetCamera.transform.rotation;
            var cameraSize = targetCamera.orthographicSize;
            Assert.That(before.CanConfirm, Is.True);
            Assert.That(before.IsExisting, Is.EqualTo(existing));

            var router = new DecorationTouchRouter(8f, 0f);
            Assert.That(router.ProcessFrame(
                Frame(1, Point(531, start, Vector2.zero, InputTouchPhase.Began)), classifier).Owner,
                Is.EqualTo(DecorationGestureOwner.SceneDrag));
            var overUi = router.ProcessFrame(
                Frame(2, Point(531, uiPoint, uiPoint - start, InputTouchPhase.Moved)), classifier);
            Assert.That(overUi.SceneDragRequested, Is.True);
            Assert.That(overUi.CurrentHit.Kind, Is.EqualTo(DecorationTouchHitKind.Ui));
            controller.RouteTouchResultForActiveMode(overUi);

            Assert.That(controller.ActiveWallMountedPreview.SurfaceId, Is.EqualTo(InitialSurfaceId),
                "An owned wall drag crossing UI must not discard its wall binding.");
            Assert.That(controller.ActiveWallMountedPreview.Position, Is.EqualTo(InitialSlot));
            Assert.That(controller.ActiveWallMountedPreview.CanConfirm, Is.True);
            Assert.That(previewView.CurrentGhost.transform.position, Is.EqualTo(ghostPosition));
            Assert.That(previewView.CurrentGhost.transform.rotation, Is.EqualTo(ghostRotation));
            Assert.That(JsonUtility.ToJson(runtime.WallMountedLayout.CaptureSnapshot()), Is.EqualTo(formalBefore));
            AssertCameraUnchanged(cameraPosition, cameraRotation, cameraSize);

            var wallPoint = ScreenPointForWallSlot(InitialSurfaceId, DragTargetSlot);
            var resumed = router.ProcessFrame(
                Frame(3, Point(531, wallPoint, wallPoint - uiPoint, InputTouchPhase.Moved)), classifier);
            Assert.That(resumed.CurrentHit.Kind, Is.EqualTo(DecorationTouchHitKind.WallSlot));
            controller.RouteTouchResultForActiveMode(resumed);
            Assert.That(controller.ActiveWallMountedPreview.Position, Is.EqualTo(DragTargetSlot));
            Assert.That(controller.ActiveWallMountedPreview.CanConfirm, Is.True);
            controller.RouteTouchResultForActiveMode(router.ProcessFrame(
                Frame(4, Point(531, wallPoint, Vector2.zero, InputTouchPhase.Ended)), classifier));
            Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.None));
            controller.CancelActivePhase7Preview();
            Assert.That(JsonUtility.ToJson(runtime.WallMountedLayout.CaptureSnapshot()), Is.EqualTo(formalBefore));
            AssertCameraUnchanged(cameraPosition, cameraRotation, cameraSize);
            if (existing)
                Assert.That(source.activeSelf, Is.True, "Cancel restores the original confirmed wall item.");
        }

        [UnityTest]
        public IEnumerator MainCafeWallDecor_DragOverConfirmedDecorReportsOverlapAndRecovers()
        {
            yield return LoadMonitorPreview();
            Assert.That(controller.TryHandleSceneDrag(new DecorationTouchHit(
                DecorationTouchHitKind.WallSlot, surfaceId: InitialSurfaceId,
                wallSlotPosition: DragTargetSlot)), Is.True);
            Assert.That(controller.TryConfirmPhase7Preview(), Is.True);
            var instance = runtime.WallMountedLayout.CaptureSnapshot().Instances.Single(item =>
                item.DefinitionId == MonitorDefinitionId && item.SurfaceId == InitialSurfaceId
                && item.Column == DragTargetSlot.Column && item.Row == DragTargetSlot.Row);
            var registry = Find<WallMountedSceneRegistry>();
            Assert.That(registry.TryGet(instance.InstanceId, out var source), Is.True);
            Assert.That(controller.TryBeginWallMountedPreview(
                MonitorDefinitionId, InitialSurfaceId, InitialSlot), Is.True);
            Canvas.ForceUpdateCanvases();
            Physics.SyncTransforms();

            var classifier = (IDecorationTouchHitClassifier)controller;
            var occupiedPoint = FindConfirmedDecorPoint(source, instance.InstanceId, DragTargetSlot);
            Assert.That(classifier.ClassifyBegan(541, occupiedPoint).Kind,
                Is.EqualTo(DecorationTouchHitKind.WallMounted),
                "Began still selects the confirmed item; only ongoing drag reads the backing wall.");
            var start = FindVisibleGhostPointWithoutItsOwnBackingSlot();
            var formalBefore = JsonUtility.ToJson(runtime.WallMountedLayout.CaptureSnapshot());
            var router = new DecorationTouchRouter(8f, 0f);
            Assert.That(router.ProcessFrame(
                Frame(1, Point(541, start, Vector2.zero, InputTouchPhase.Began)), classifier).Owner,
                Is.EqualTo(DecorationGestureOwner.SceneDrag));
            var moved = router.ProcessFrame(
                Frame(2, Point(541, occupiedPoint, occupiedPoint - start, InputTouchPhase.Moved)), classifier);
            Assert.That(moved.SceneDragRequested, Is.True);
            Assert.That(moved.CurrentHit.Kind, Is.EqualTo(DecorationTouchHitKind.WallSlot),
                "A confirmed wall decor collider must not hide the drag target wall Slot.");
            Assert.That(moved.CurrentHit.SurfaceId, Is.EqualTo(InitialSurfaceId));
            Assert.That(moved.CurrentHit.WallSlotPosition, Is.EqualTo((WallSlotPosition?)DragTargetSlot));
            controller.RouteTouchResultForActiveMode(moved);
            Assert.That(controller.ActiveWallMountedPreview.Position, Is.EqualTo(DragTargetSlot));
            Assert.That(controller.ActiveWallMountedPreview.FailureReason, Is.EqualTo(WallPlacementFailureReason.Overlap));
            Assert.That(controller.ActiveWallMountedPreview.CanConfirm, Is.False);
            Assert.That(controller.TryConfirmPhase7Preview(), Is.False);
            Assert.That(JsonUtility.ToJson(runtime.WallMountedLayout.CaptureSnapshot()), Is.EqualTo(formalBefore));

            // UI must preserve an invalid candidate too, never turn it valid or erase its reason.
            // 无效预览经过按钮也不能被重新验证成有效，或丢掉占位原因。
            Canvas.ForceUpdateCanvases();
            var cancel = Find<DecorationActionBarView>().GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "CancelButton");
            var uiPoint = P8RWallDecorActionAvoidanceTests.UiBounds((RectTransform)cancel.transform).center;
            var overUi = router.ProcessFrame(
                Frame(3, Point(541, uiPoint, uiPoint - occupiedPoint, InputTouchPhase.Moved)), classifier);
            Assert.That(overUi.CurrentHit.Kind, Is.EqualTo(DecorationTouchHitKind.Ui));
            controller.RouteTouchResultForActiveMode(overUi);
            Assert.That(controller.ActiveWallMountedPreview.Position, Is.EqualTo(DragTargetSlot));
            Assert.That(controller.ActiveWallMountedPreview.FailureReason, Is.EqualTo(WallPlacementFailureReason.Overlap));
            Assert.That(controller.ActiveWallMountedPreview.CanConfirm, Is.False);

            var freePoint = ScreenPointForWallSlot(InitialSurfaceId, InitialSlot);
            var recovered = router.ProcessFrame(
                Frame(4, Point(541, freePoint, freePoint - uiPoint, InputTouchPhase.Moved)), classifier);
            controller.RouteTouchResultForActiveMode(recovered);
            Assert.That(controller.ActiveWallMountedPreview.Position, Is.EqualTo(InitialSlot));
            Assert.That(controller.ActiveWallMountedPreview.CanConfirm, Is.True);
            controller.RouteTouchResultForActiveMode(router.ProcessFrame(
                Frame(5, Point(541, freePoint, Vector2.zero, InputTouchPhase.Ended)), classifier));
            Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.None));
            controller.CancelActivePhase7Preview();
            Assert.That(JsonUtility.ToJson(runtime.WallMountedLayout.CaptureSnapshot()), Is.EqualTo(formalBefore));
            Assert.That(source.activeSelf, Is.True);
        }

        private Vector2 FindConfirmedDecorPoint(GameObject source, string instanceId, WallSlotPosition slot)
        {
            var bounds = P8RWallDecorActionAvoidanceTests.ModelBounds(source, targetCamera);
            var registry = Find<WallMountedSceneRegistry>();
            for (var x = 1; x < 10; x++)
            for (var y = 1; y < 10; y++)
            {
                var point = new Vector2(Mathf.Lerp(bounds.xMin, bounds.xMax, x * .1f),
                    Mathf.Lerp(bounds.yMin, bounds.yMax, y * .1f));
                if (HasUiRaycast(point) || HitsActiveGhostRendererBounds(point)
                    || !TryGetBackingWallSlot(point, out var surfaceId, out var backingSlot)
                    || surfaceId != InitialSurfaceId || backingSlot != slot)
                    continue;
                if (Physics.RaycastAll(targetCamera.ScreenPointToRay(point), Mathf.Infinity,
                    ~0, QueryTriggerInteraction.Collide).Any(hit =>
                        registry.TryGetInstanceId(hit.collider, out var id) && id == instanceId))
                    return point;
            }
            Assert.Fail("The confirmed monitor must expose a UI-free collider pixel over its literal occupied wall Slot.");
            return default;
        }

        private IEnumerator LoadMonitorPreview()
        {
            screen = new P8RReferenceLayoutTests.NativeScreenSize();
            screen.Resize(new Vector2(1080f, 1920f));
            P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360f, 640f);
            scene = EditorSceneManager.LoadSceneInPlayMode(
                MainCafePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            yield return null;

            controller = Find<DecorationModeController>();
            runtime = Find<CafeLayoutRuntime>();
            previewView = Find<WallMountedPreviewView>();
            targetCamera = (UnityEngine.Camera)typeof(DecorationModeController)
                .GetField("targetCamera", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(controller);
            controller.EnterDecorationMode();
            Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            Assert.That(controller.TryBeginWallMountedPreview(
                MonitorDefinitionId, InitialSurfaceId, InitialSlot), Is.True);
            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();

            Assert.That(previewView.CurrentGhost, Is.Not.Null);
            Assert.That(previewView.CurrentGhost.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
            Assert.That(controller.ActiveWallMountedPreview.DefinitionId, Is.EqualTo(MonitorDefinitionId));
            Assert.That(controller.ActiveWallMountedPreview.SurfaceId, Is.EqualTo(InitialSurfaceId));
            Assert.That(controller.ActiveWallMountedPreview.Position, Is.EqualTo(InitialSlot));

            // Scoped input fixture: use the same close zoom as the reported mobile grab failure,
            // then center the real model so UI/screen edges cannot decide the classifier result.
            targetCamera.orthographicSize = 4f;
            var model = P8RWallDecorActionAvoidanceTests.ModelBounds(
                previewView.CurrentGhost, targetCamera);
            var target = new Vector2(Screen.width * .5f, Screen.height * .48f);
            var worldPerPixel = targetCamera.orthographicSize * 2f / targetCamera.pixelHeight;
            targetCamera.transform.position += targetCamera.transform.right
                    * ((model.center.x - target.x) * worldPerPixel)
                + targetCamera.transform.up
                    * ((model.center.y - target.y) * worldPerPixel);
            typeof(DecorationModeController)
                .GetMethod("UpdateActionPresentation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            Canvas.ForceUpdateCanvases();
        }

        private Vector2 FindVisibleGhostPointWithoutItsOwnBackingSlot()
        {
            var bounds = P8RWallDecorActionAvoidanceTests.ModelBounds(
                previewView.CurrentGhost, targetCamera);
            var fractions = Enumerable.Range(0, 51)
                .Select(index => .005f + index * .0198f)
                .OrderBy(value => Mathf.Min(value, 1f - value))
                .ToArray();
            foreach (var xFraction in fractions)
            foreach (var yFraction in fractions)
            {
                var point = new Vector2(
                    Mathf.Lerp(bounds.xMin, bounds.xMax, xFraction),
                    Mathf.Lerp(bounds.yMin, bounds.yMax, yFraction));
                if (!targetCamera.pixelRect.Contains(point)
                    || HasUiRaycast(point)
                    || !HitsActiveGhostRendererBounds(point))
                    continue;
                if (!TryGetBackingWallSlot(point, out var surfaceId, out var slot)
                    || !string.Equals(surfaceId, InitialSurfaceId)
                    || slot != InitialSlot)
                    return point;
            }

            Assert.Fail("The real 1x1 monitor needs a visible UI-free pixel whose backing wall misses or differs from wall.back-left (4,0).");
            return default;
        }

        private Vector2 ScreenPointForWallSlot(string surfaceId, WallSlotPosition slot)
        {
            var wall = Components<WallSurfaceAuthoring>()
                .Single(item => item.SurfaceId == surfaceId);
            // The floating action row may cover the center after the ghost moves.
            // 在同一个指定墙格内找未被按钮遮住的点，不禁用真实 UI 来让测试通过。
            foreach (var x in new[] { .5f, .2f, .8f })
            foreach (var y in new[] { .5f, .2f, .8f })
            {
                var localPoint = new Vector3(
                    -wall.Columns * wall.SlotSize * .5f + (slot.Column + x) * wall.SlotSize,
                    (slot.Row + y) * wall.SlotSize, 0f);
                var projected = targetCamera.WorldToScreenPoint(wall.GetWallMountedWorldPosition(localPoint));
                var point = new Vector2(projected.x, projected.y);
                if (projected.z > 0f && targetCamera.pixelRect.Contains(point) && !HasUiRaycast(point)
                    && TryGetBackingWallSlot(point, out var backingSurface, out var backingSlot)
                    && backingSurface == surfaceId && backingSlot == slot)
                    return point;
            }
            Assert.Fail("The literal drag target wall Slot must expose a UI-free point.");
            return default;
        }

        private bool HitsActiveGhostRendererBounds(Vector2 screenPoint)
        {
            var ray = targetCamera.ScreenPointToRay(screenPoint);
            return previewView.CurrentGhost.GetComponentsInChildren<Renderer>(true)
                .Any(renderer => renderer.enabled
                    && renderer.gameObject.activeInHierarchy
                    && renderer.bounds.IntersectRay(ray));
        }

        private Vector2 FindUiFreePointWithoutWall()
        {
            var pixelRect = targetCamera.pixelRect;
            foreach (var yFraction in new[] { .92f, .08f, .82f, .18f, .72f, .28f })
            foreach (var xFraction in new[] { .5f, .15f, .85f, .3f, .7f })
            {
                var point = new Vector2(
                    Mathf.Lerp(pixelRect.xMin, pixelRect.xMax, xFraction),
                    Mathf.Lerp(pixelRect.yMin, pixelRect.yMax, yFraction));
                if (!HasUiRaycast(point) && !TryGetBackingWallSlot(point, out _, out _))
                    return point;
            }

            Assert.Fail("MainCafe must expose one UI-free Scene pixel without a WallSurface behind it.");
            return default;
        }

        private bool TryGetBackingWallSlot(
            Vector2 screenPoint,
            out string surfaceId,
            out WallSlotPosition slot)
        {
            var hits = Physics.RaycastAll(
                    targetCamera.ScreenPointToRay(screenPoint),
                    Mathf.Infinity,
                    ~0,
                    QueryTriggerInteraction.Collide)
                .OrderBy(hit => hit.distance);
            foreach (var hit in hits)
            {
                // Confirmed items are parented to the wall but are not its mounting plane.
                // 已有墙饰继承墙面 parent，不能把它的外表面误当成真实墙格。
                if (Find<WallMountedSceneRegistry>().TryGetInstanceId(hit.collider, out _))
                    continue;
                var wall = hit.collider.GetComponentInParent<WallSurfaceAuthoring>();
                if (wall == null)
                    continue;
                var local = wall.transform.InverseTransformPoint(hit.point);
                var column = Mathf.FloorToInt(
                    (local.x + wall.Columns * wall.SlotSize * .5f) / wall.SlotSize);
                var row = Mathf.FloorToInt(local.y / wall.SlotSize);
                if (column < 0 || column >= wall.Columns || row < 0 || row >= wall.Rows)
                    continue;
                surfaceId = wall.SurfaceId;
                slot = new WallSlotPosition(column, row);
                return true;
            }

            surfaceId = null;
            slot = default;
            return false;
        }

        private static bool HasUiRaycast(Vector2 point)
        {
            var eventSystem = EventSystem.current;
            Assert.That(eventSystem, Is.Not.Null);
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = point }, hits);
            return hits.Any(hit => hit.module is GraphicRaycaster raycaster
                && raycaster.isActiveAndEnabled
                && raycaster.gameObject.activeInHierarchy);
        }

        private void AssertCameraUnchanged(
            Vector3 expectedPosition,
            Quaternion expectedRotation,
            float expectedOrthographicSize)
        {
            Assert.That(targetCamera.transform.position, Is.EqualTo(expectedPosition));
            Assert.That(targetCamera.transform.rotation, Is.EqualTo(expectedRotation));
            Assert.That(targetCamera.orthographicSize, Is.EqualTo(expectedOrthographicSize));
        }

        private T Find<T>() where T : Component => Components<T>().Single();

        private T[] Components<T>() where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static DecorationTouchFrame Frame(
            int frameNumber,
            params DecorationTouchPoint[] points) =>
            new DecorationTouchFrame(frameNumber, points);

        private static DecorationTouchPoint Point(
            int touchId,
            Vector2 position,
            Vector2 delta,
            InputTouchPhase phase) =>
            new DecorationTouchPoint(touchId, position, delta, phase);

    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    public sealed class Phase8CatalogueWheelInputPlayModeTests
    {
        private readonly InputTestFixture inputFixture = new InputTestFixture();
        private DecorationCatalogueView catalogue;

        [UnitySetUp]
        public IEnumerator OpenProductionCatalogue()
        {
            // UnitySetUp runs before inherited NUnit SetUp. Reset InputSystem before Scene Awake.
            // 显式控制 fixture 顺序，避免 Scene 的 EnhancedTouch 在启用后又被 reset。
            inputFixture.Setup();
            EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/MainCafe.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            yield return null;

            Object.FindFirstObjectByType<DecorationModeController>().EnterDecorationMode();
            yield return null;
            Assert.That(Object.FindFirstObjectByType<DecorationModeTabsView>()
                .RequestMode(DecorationModeKind.Wall), Is.True);
            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();
            catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            Assert.That(catalogue.CategoryRows, Is.Not.Empty);
        }

        [UnityTearDown]
        public IEnumerator RestoreCleanScene()
        {
            try
            {
                Time.timeScale = 1f;
                var active = SceneManager.GetActiveScene();
                var cleanup = SceneManager.CreateScene("Phase8CatalogueWheelCleanup");
                SceneManager.SetActiveScene(cleanup);
                if (active.IsValid() && active.isLoaded && active != cleanup)
                {
                    var unload = SceneManager.UnloadSceneAsync(active);
                    while (unload != null && !unload.isDone) yield return null;
                }
            }
            finally
            {
                inputFixture.TearDown();
            }
        }

        [Test]
        public void WheelOverItemRow_LeavesAllCatalogueContentStationary()
        {
            var tile = FirstTile();
            AssertWheelLeavesContentStationary(tile.gameObject);
        }

        [Test]
        public void WheelOverVerticalViewport_LeavesAllCatalogueContentStationary()
        {
            Assert.That(catalogue.VerticalScroll.content.rect.height,
                Is.GreaterThan(catalogue.VerticalScroll.viewport.rect.height),
                "The production Wall catalogue must have scrollable content for this regression.");
            AssertWheelLeavesContentStationary(catalogue.VerticalScroll.viewport.gameObject);
        }

        [TestCase(-1, true, false, TestName = "MousePointerDragAcrossRow_StillMovesHorizontalContent")]
        [TestCase(3, true, false, TestName = "TouchPointerDragAcrossRow_StillMovesHorizontalContent")]
        [TestCase(-1, false, false, TestName = "MousePointerDragUpRow_StillMovesVerticalContent")]
        [TestCase(3, false, false, TestName = "TouchPointerDragUpRow_StillMovesVerticalContent")]
        [TestCase(-1, true, true, TestName = "ReviewFix_MouseDiagonalHorizontalDragKeepsVerticalPosition")]
        [TestCase(3, true, true, TestName = "ReviewFix_TouchDiagonalHorizontalDragKeepsVerticalPosition")]
        [TestCase(-1, false, true, TestName = "ReviewFix_MouseDiagonalVerticalDragKeepsHorizontalPosition")]
        [TestCase(3, false, true, TestName = "ReviewFix_TouchDiagonalVerticalDragKeepsHorizontalPosition")]
        public void PointerDragAcrossItemRow_MovesOnlyTheChosenCatalogueAxis(
            int pointerId, bool horizontal, bool diagonal)
        {
            var row = catalogue.CategoryRows[0].HorizontalScroll;
            var vertical = catalogue.VerticalScroll;
            row.StopMovement();
            vertical.StopMovement();
            var rowBefore = row.content.anchoredPosition;
            var verticalBefore = vertical.content.anchoredPosition;
            var pointer = PointerAt(FirstTile().gameObject, pointerId);
            var dragTarget = ExecuteEvents.GetEventHandler<IDragHandler>(FirstTile().gameObject);
            Assert.That(dragTarget, Is.Not.Null);

            // Send the same pointer event sequence used after mouse/touch drag recognition.
            // 通过真实 row handlers 测试拖动，不直接调用 Catalogue 的路由 helper。
            ExecuteEvents.Execute(dragTarget, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(dragTarget, pointer, ExecuteEvents.beginDragHandler);
            for (var step = 0; step < 4; step++)
            {
                pointer.delta = horizontal
                    ? new Vector2(-20f, diagonal ? 5f : 0f)
                    : new Vector2(diagonal ? -5f : 0f, 20f);
                pointer.position += pointer.delta;
                ExecuteEvents.Execute(dragTarget, pointer, ExecuteEvents.dragHandler);
            }
            ExecuteEvents.Execute(dragTarget, pointer, ExecuteEvents.endDragHandler);

            if (horizontal)
            {
                Assert.That(Mathf.Abs(row.content.anchoredPosition.x - rowBefore.x),
                    Is.GreaterThan(8f), "Horizontal pointer dragging must still move the item row.");
                Assert.That(Vector2.Distance(vertical.content.anchoredPosition, verticalBefore),
                    Is.LessThan(.01f), "Horizontal dragging must not move the category list.");
            }
            else
            {
                Assert.That(Mathf.Abs(vertical.content.anchoredPosition.y - verticalBefore.y),
                    Is.GreaterThan(8f), "Vertical pointer dragging must still move the category list.");
                Assert.That(Vector2.Distance(row.content.anchoredPosition, rowBefore),
                    Is.LessThan(.01f), "Vertical dragging must not move the item row sideways.");
            }
            Assert.That(catalogue.IsSceneDragBlocked, Is.False,
                "Releasing the pointer must release nested drag ownership.");
            Assert.That(row.horizontal, Is.True,
                "A vertical gesture must not leave the next horizontal gesture locked.");
        }

        [TestCase("Hide")]
        [TestCase("Collapse")]
        [TestCase("Disable")]
        [TestCase("CompactPreview")]
        [TestCase("TabsOnly")]
        [TestCase("Hidden")]
        [TestCase("SheetDrag")]
        public void ReviewFix_InterruptedVerticalDragReleasesOwnership(string interruption)
        {
            var row = catalogue.CategoryRows[0].HorizontalScroll;
            var pointer = PointerAt(FirstTile().gameObject, -1);
            var target = ExecuteEvents.GetEventHandler<IDragHandler>(FirstTile().gameObject);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.beginDragHandler);
            pointer.delta = new Vector2(-5f, 20f);
            pointer.position += pointer.delta;
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.dragHandler);
            Assert.That(catalogue.IsSceneDragBlocked, Is.True);

            // Closing the sheet may prevent EndDrag; cleanup must not depend on pointer release.
            // 面板关闭可能不再收到 EndDrag，因此必须主动释放拖动状态。
            if (interruption == "Hide") catalogue.Hide();
            else if (interruption == "Collapse") catalogue.ShowCollapsedHandle();
            else if (interruption == "Disable") catalogue.enabled = false;
            else if (interruption == "SheetDrag") catalogue.ApplySheetDrag(-40f, false);
            else catalogue.SetSheetState(
                (DecorationSheetState)System.Enum.Parse(typeof(DecorationSheetState), interruption), false);

            Assert.That(catalogue.IsSceneDragBlocked, Is.False);
            Assert.That(catalogue.NestedDragOwner, Is.Null);
            Assert.That(row.horizontal, Is.True);
        }

        [UnityTest]
        public IEnumerator MouseWheelWithCatalogueOpen_StillZoomsProductionCamera()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            var camera = UnityEngine.Camera.main;
            Assert.That(camera, Is.Not.Null);
            var cameraSettings = (AnimalCafe.Camera.CameraSettings)new UnityEditor.SerializedObject(
                Object.FindFirstObjectByType<DecorationModeController>())
                .FindProperty("cameraSettings").objectReferenceValue;
            Assert.That(cameraSettings, Is.Not.Null);
            Assert.That(catalogue.State, Is.EqualTo(DecorationCatalogueState.Expanded));
            var position = PointerAt(FirstTile().gameObject, mouse.deviceId).position;
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
                { position = position }, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.transform.IsChildOf(catalogue.transform), Is.True,
                "The real mouse wheel must be positioned over Catalogue UI.");
            yield return QueueMouseStateAndWaitForConsumption(mouse,
                new MouseState { position = position });
            Assert.That(Mouse.current, Is.SameAs(mouse));
            var before = camera.orthographicSize;
            var sawWheel = false;
            var wheelUpdate = InputUpdateType.None;
            var wheelFrame = -1;
            System.Action observeWheel = () =>
            {
                if (!mouse.added || mouse.scroll.ReadValue().y <= 0f) return;
                sawWheel = true;
                wheelUpdate = InputState.currentUpdateType;
                wheelFrame = Time.frameCount;
            };
            InputSystem.onAfterUpdate += observeWheel;
            try
            {
                yield return QueueMouseStateAndWaitForConsumption(mouse, new MouseState
                {
                    position = position,
                    // Native wheel input is normalized to one step by the current Input System settings.
                    // 使用真实默认单位 1，而不是旧平台原始值 120；两者的方向相同，幅度不能混为 pinch 像素。
                    scroll = new Vector2(0f, 1f)
                });
                var deadline = Time.realtimeSinceStartup + 2f;
                while (camera.orthographicSize >= before && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.That(sawWheel, Is.True,
                    "A real positive wheel state must reach the virtual mouse before testing Camera.");
                Assert.That(camera.orthographicSize, Is.EqualTo(before - cameraSettings.ZoomSpeed).Within(.0001f),
                    "A normalized wheel step must retain the full ZoomSpeed with Catalogue open. "
                    + $"wheelUpdate={wheelUpdate}, wheelFrame={wheelFrame}, frame={Time.frameCount}, "
                    + $"deviceTime={mouse.lastUpdateTime}, currentMouse={Mouse.current?.deviceId}, "
                    + $"expectedMouse={mouse.deviceId}.");
            }
            finally
            {
                InputSystem.onAfterUpdate -= observeWheel;
                if (mouse.added) InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator MouseWheelOverBlockedChecklist_ZoomsScene()
        {
            var readiness = Object.FindFirstObjectByType<ValidationMessageView>();
            readiness.ShowReadiness(ScrollableReadinessReport());
            Canvas.ForceUpdateCanvases();
            yield return null;
            P8RCompleteFlowTests.AssertChecklist(readiness);
            var position = CenterOf((RectTransform)readiness.transform.Find("ChecklistRow1"));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.That(hits, Is.Empty, "The real checklist row must leave the scene wheel unobstructed.");
            yield return AssertWheelZoomsOneStepAt(position);
        }

        [UnityTest]
        public IEnumerator MouseWheelOverRepeatedChecklist_ZoomsScene()
        {
            var readiness = Object.FindFirstObjectByType<ValidationMessageView>();
            readiness.ShowReadiness(ScrollableReadinessReport());
            readiness.ShowReadiness(ScrollableReadinessReport());
            Canvas.ForceUpdateCanvases();
            yield return null;
            P8RCompleteFlowTests.AssertChecklist(readiness);
            var position = CenterOf((RectTransform)readiness.transform.Find("ChecklistRow1"));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.That(hits, Is.Empty, "The real checklist row must leave the scene wheel unobstructed.");
            yield return AssertWheelZoomsOneStepAt(position);
        }

        [UnityTest]
        public IEnumerator MouseWheelOverHealthyChecklist_ZoomsScene()
        {
            var readiness = Object.FindFirstObjectByType<ValidationMessageView>();
            readiness.ShowReadiness(P8RCompleteFlowTests.Report(true));
            Canvas.ForceUpdateCanvases();
            yield return null;
            P8RCompleteFlowTests.AssertChecklist(readiness);
            var position = CenterOf((RectTransform)readiness.transform.Find("ChecklistRow1"));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.That(hits, Is.Empty, "The real checklist row must leave the scene wheel unobstructed.");
            yield return AssertWheelZoomsOneStepAt(position);
        }

        [UnityTest]
        public IEnumerator MouseWheelOutsideReadinessAndUi_ZoomsSceneOneStep()
        {
            var readiness = Object.FindFirstObjectByType<ValidationMessageView>();
            readiness.ShowReadiness(ScrollableReadinessReport());
            Canvas.ForceUpdateCanvases();
            yield return null;

            yield return AssertWheelZoomsOneStepAt(FindScenePointOutsideUi());
        }

        [UnityTest]
        public IEnumerator MouseWheelWhileStoreModalOpen_DoesNotZoomCamera()
        {
            var modal = Object.FindFirstObjectByType<DecorationStoreModalView>();
            Assert.That(modal, Is.Not.Null);
            modal.ShowFunctionalSurface(DecorationCatalogueItemKind.CashRegister);
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(modal.IsOpen, Is.True);

            yield return AssertWheelDoesNotZoomAt(CenterOf(modal.ContentRect));
        }

        [UnityTest]
        public IEnumerator MouseWheelWhileExitModalOpen_DoesNotZoomCamera()
        {
            var modal = Object.FindFirstObjectByType<DecorationExitModalView>(FindObjectsInactive.Include);
            Assert.That(modal, Is.Not.Null);
            modal.Show();
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(modal.gameObject.activeInHierarchy, Is.True);

            yield return AssertWheelDoesNotZoomAt(CenterOf((RectTransform)modal.transform));
        }

        [UnityTest]
        public IEnumerator NormalMode_MouseWheelOverBlockedChecklist_ZoomsScene()
        {
            Object.FindFirstObjectByType<DecorationModeController>().ExitDecorationMode();
            var readiness = Object.FindFirstObjectByType<ValidationMessageView>();
            readiness.ShowReadiness(ScrollableReadinessReport());
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(readiness.IsVisible, Is.True);
            Assert.That(readiness.transform.Find("ChecklistRow0").gameObject.activeSelf, Is.False);
            var position = CenterOf((RectTransform)readiness.transform);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.That(hits, Is.Empty, "The real checklist row must leave the scene wheel unobstructed.");
            yield return AssertWheelZoomsOneStepAt(position);
        }

        [UnityTest]
        public IEnumerator NormalMode_MouseWheelOverRepeatedChecklist_ZoomsScene()
        {
            Object.FindFirstObjectByType<DecorationModeController>().ExitDecorationMode();
            var readiness = Object.FindFirstObjectByType<ValidationMessageView>();
            readiness.ShowReadiness(ScrollableReadinessReport());
            readiness.ShowReadiness(ScrollableReadinessReport());
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(readiness.IsVisible, Is.True);
            Assert.That(readiness.transform.Find("ChecklistRow0").gameObject.activeSelf, Is.False);
            var position = CenterOf((RectTransform)readiness.transform);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.That(hits, Is.Empty, "The real checklist row must leave the scene wheel unobstructed.");
            yield return AssertWheelZoomsOneStepAt(position);
        }

        [UnityTest]
        public IEnumerator NormalMode_MouseWheelOverHealthyChecklist_ZoomsScene()
        {
            Object.FindFirstObjectByType<DecorationModeController>().ExitDecorationMode();
            var readiness = Object.FindFirstObjectByType<ValidationMessageView>();
            readiness.ShowReadiness(P8RCompleteFlowTests.Report(true));
            Canvas.ForceUpdateCanvases();
            yield return null;
            Assert.That(readiness.IsVisible, Is.False);
            Assert.That(readiness.transform.Find("ChecklistRow0").gameObject.activeSelf, Is.False);
            var position = CenterOf((RectTransform)readiness.transform);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.That(hits, Is.Empty, "The real checklist row must leave the scene wheel unobstructed.");
            yield return AssertWheelZoomsOneStepAt(position);
        }

        [UnityTest]
        public IEnumerator NormalMode_MouseWheelOutsideReadinessAndUi_ZoomsSceneOneStep()
        {
            Object.FindFirstObjectByType<DecorationModeController>().ExitDecorationMode();
            var readiness = Object.FindFirstObjectByType<ValidationMessageView>();
            readiness.ShowReadiness(ScrollableReadinessReport());
            Canvas.ForceUpdateCanvases();
            yield return null;

            yield return AssertWheelZoomsOneStepAt(FindScenePointOutsideUi());
        }

        private static IEnumerator QueueMouseStateAndWaitForConsumption(Mouse mouse, MouseState state)
        {
            // Follow the existing Scene fixtures' device clock, outside Editor transition time windows.
            // 使用 device 递增时间，避免 fake runtime 时钟落入 Editor 切换时的丢弃窗口。
            var before = mouse.lastUpdateTime;
            var eventTime = before + .000001d;
            InputSystem.QueueStateEvent(mouse, state, eventTime);
            var deadline = Time.realtimeSinceStartup + 2f;
            while (mouse.lastUpdateTime <= before && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(mouse.lastUpdateTime, Is.GreaterThan(before),
                $"Mouse event was not consumed: eventTime={eventTime}, inputTime={InputState.currentTime}, "
                + $"update={InputState.currentUpdateType}, frame={Time.frameCount}.");
        }

        private static IEnumerator AssertWheelDoesNotZoomAt(Vector2 position)
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            try
            {
                yield return QueueMouseStateAndWaitForConsumption(mouse,
                    new MouseState { position = position });
                var camera = UnityEngine.Camera.main;
                Assert.That(camera, Is.Not.Null);
                var before = camera.orthographicSize;
                yield return QueueMouseStateAndWaitForConsumption(mouse, new MouseState
                {
                    position = position,
                    scroll = new Vector2(0f, -1f)
                });
                yield return null;

                Assert.That(camera.orthographicSize, Is.EqualTo(before).Within(.0001f));
            }
            finally
            {
                if (mouse.added) InputSystem.RemoveDevice(mouse);
            }
        }

        private static IEnumerator AssertWheelZoomsOneStepAt(Vector2 position)
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            try
            {
                yield return QueueMouseStateAndWaitForConsumption(mouse,
                    new MouseState { position = position });
                var camera = UnityEngine.Camera.main;
                var settings = (AnimalCafe.Camera.CameraSettings)new UnityEditor.SerializedObject(
                    Object.FindFirstObjectByType<DecorationModeController>())
                    .FindProperty("cameraSettings").objectReferenceValue;
                Assert.That(camera, Is.Not.Null);
                Assert.That(settings, Is.Not.Null);
                var before = camera.orthographicSize;
                yield return QueueMouseStateAndWaitForConsumption(mouse, new MouseState
                {
                    position = position,
                    scroll = new Vector2(0f, 1f)
                });
                var deadline = Time.realtimeSinceStartup + 2f;
                while (camera.orthographicSize >= before && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.That(camera.orthographicSize,
                    Is.EqualTo(before - settings.ZoomSpeed).Within(.0001f));
            }
            finally
            {
                if (mouse.added) InputSystem.RemoveDevice(mouse);
            }
        }

        private static LayoutReadinessReport ScrollableReadinessReport()
        {
            var failures = Enumerable.Range(0, 24).Select(index =>
                Construct<LayoutReadinessFailure>(
                    LayoutReadinessSeverity.Blocking,
                    LayoutReadinessFailureCode.AnchorUnreachable,
                    (LayoutStationType?)LayoutStationType.CashRegister,
                    "station.register." + index,
                    "support.counter." + index,
                    "slot." + index,
                    (InteractionRole?)InteractionRole.Employee,
                    (GridPosition?)new GridPosition(2, 3 + index),
                    "Detached diagnostic cause")).ToArray();
            var summary = Construct<LayoutReadinessSummary>(2, 1);
            return Construct<LayoutReadinessReport>(false, Array.Empty<StationReadiness>(), failures,
                summary, summary, summary);
        }

        [UnityTest]
        public IEnumerator NormalHudMouseDrag_StaysUiOwnedAndNextSceneDragWorks()
        {
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.ExitDecorationMode();
            yield return null;
            Assert.That(controller.IsOpen, Is.False);
            var cameraController = Object.FindFirstObjectByType<AnimalCafe.Camera.CafeCameraController>();
            Assert.That(cameraController.isActiveAndEnabled, Is.True);
            var readiness = Object.FindFirstObjectByType<ValidationMessageView>();
            Assert.That(readiness.IsVisible, Is.True);
            Assert.That(readiness.transform.Find("ChecklistRow0").gameObject.activeSelf, Is.False);
            var hud = Object.FindFirstObjectByType<AnimalCafe.UI.TimeControlPanel>();
            // Use the actual HUD input target; the passive checklist is no longer an input owner.
            // 保留真实 UI 按住拖出边界验证，改用现有 HUD 按钮。
            var button = (Button)typeof(AnimalCafe.UI.TimeControlPanel)
                .GetField("normalButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(hud);
            var position = CenterOf((RectTransform)button.transform);
            AssertTopUiHitBelongsTo(position, button.transform);
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            try
            {
                yield return QueueMouseStateAndWaitForConsumption(mouse, new MouseState { position = position });
                yield return null;
                var camera = UnityEngine.Camera.main;
                Assert.That(camera, Is.Not.Null);
                var cameraBefore = camera.transform.position;
                yield return QueueMouseStateAndWaitForConsumption(mouse,
                    new MouseState { position = position }.WithButton(MouseButton.Left));
                yield return null;
                for (var step = 0; step < 4; step++)
                {
                    var delta = Vector2.up * 12f;
                    position += delta;
                    yield return QueueMouseStateAndWaitForConsumption(mouse,
                        new MouseState { position = position, delta = delta }.WithButton(MouseButton.Left));
                    yield return null;
                }
                // Holding a UI-owned gesture outside the panel must not hand it to the scene.
                // 按住拖出 UI 后仍属于原来的 UI 手势。
                var outside = FindScenePointOutsideUi();
                yield return QueueMouseStateAndWaitForConsumption(mouse,
                    new MouseState { position = outside, delta = outside - position }.WithButton(MouseButton.Left));
                yield return null;
                position = outside;
                var cameraDistance = Vector3.Distance(cameraBefore, camera.transform.position);
                yield return QueueMouseStateAndWaitForConsumption(mouse, new MouseState { position = position });
                yield return null;
                Assert.That(cameraDistance, Is.LessThan(.0001f), "Dragging the real HUD target must not pan the Camera.");

                // Releasing UI ownership must allow a fresh scene gesture.
                // 松手后新的场景拖动仍应正常移动 Camera。
                position = FindScenePointOutsideUi();
                cameraBefore = camera.transform.position;
                yield return QueueMouseStateAndWaitForConsumption(mouse,
                    new MouseState { position = position }.WithButton(MouseButton.Left));
                yield return null;
                position += Vector2.right * 20f;
                yield return QueueMouseStateAndWaitForConsumption(mouse,
                    new MouseState { position = position, delta = Vector2.right * 20f }.WithButton(MouseButton.Left));
                yield return null;
                Assert.That(Vector3.Distance(cameraBefore, camera.transform.position), Is.GreaterThan(.001f));
                yield return QueueMouseStateAndWaitForConsumption(mouse, new MouseState { position = position });
            }
            finally
            {
                if (mouse.added) InputSystem.RemoveDevice(mouse);
            }
        }


        private static T Construct<T>(params object[] arguments) => (T)Activator.CreateInstance(
            typeof(T), BindingFlags.Instance | BindingFlags.NonPublic, null, arguments, null);

        private static Vector2 CenterOf(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(
            null, rect.TransformPoint(rect.rect.center));

        private static void AssertTopUiHitBelongsTo(Vector2 position, Transform expectedOwner)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.transform.IsChildOf(expectedOwner), Is.True,
                "The wheel fixture must hover the intended UI owner.");
        }

        private static Vector2 FindScenePointOutsideUi()
        {
            var candidates = new[]
            {
                new Vector2(.5f, .55f),
                new Vector2(.25f, .55f),
                new Vector2(.75f, .55f),
                new Vector2(.5f, .4f)
            };
            foreach (var normalized in candidates)
            {
                var position = new Vector2(Screen.width * normalized.x, Screen.height * normalized.y);
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
                if (hits.Count == 0) return position;
            }

            Assert.Fail("The production fixture needs one visible scene point outside readiness and other UI.");
            return default;
        }

        private void AssertWheelLeavesContentStationary(GameObject pointerTarget)
        {
            var scrolls = new[] { catalogue.VerticalScroll }
                .Concat(catalogue.CategoryRows.Select(row => row.HorizontalScroll)).ToArray();
            foreach (var scroll in scrolls) scroll.StopMovement();
            var before = scrolls.Select(scroll => scroll.content.anchoredPosition).ToArray();
            var pointer = PointerAt(pointerTarget, -1);
            pointer.scrollDelta = new Vector2(0f, -1f);

            // Match InputSystemUIInputModule's wheel dispatch through the real hierarchy.
            // 从实际命中对象向上寻找 handler，覆盖 row 与外层 viewport 两条路径。
            var handler = ExecuteEvents.GetEventHandler<IScrollHandler>(pointerTarget);
            Assert.That(handler, Is.Not.Null, "The fixture must deliver a real wheel event.");
            ExecuteEvents.ExecuteHierarchy(handler, pointer, ExecuteEvents.scrollHandler);

            for (var index = 0; index < scrolls.Length; index++)
            {
                Assert.That(Vector2.Distance(scrolls[index].content.anchoredPosition, before[index]),
                    Is.LessThan(.001f),
                    "Mouse wheel must not move Catalogue content: " + scrolls[index].name);
            }
        }

        private DecorationCatalogueTileView FirstTile() => catalogue.CategoryRows[0]
            .HorizontalScroll.content.GetComponentInChildren<DecorationCatalogueTileView>();

        private static PointerEventData PointerAt(GameObject target, int pointerId)
        {
            var rect = (RectTransform)target.transform;
            var position = RectTransformUtility.WorldToScreenPoint(
                null, rect.TransformPoint(rect.rect.center));
            return new PointerEventData(EventSystem.current)
            {
                pointerId = pointerId,
                button = PointerEventData.InputButton.Left,
                position = position,
                pressPosition = position,
                pointerEnter = target
            };
        }
    }
}
#endif

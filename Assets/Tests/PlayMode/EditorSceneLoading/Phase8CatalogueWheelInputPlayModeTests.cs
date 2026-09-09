#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using AnimalCafe.Decoration;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

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

        [TestCase(-1, true, TestName = "MousePointerDragAcrossRow_StillMovesHorizontalContent")]
        [TestCase(3, true, TestName = "TouchPointerDragAcrossRow_StillMovesHorizontalContent")]
        [TestCase(-1, false, TestName = "MousePointerDragUpRow_StillMovesVerticalContent")]
        [TestCase(3, false, TestName = "TouchPointerDragUpRow_StillMovesVerticalContent")]
        public void PointerDragAcrossItemRow_MovesOnlyTheChosenCatalogueAxis(
            int pointerId, bool horizontal)
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
                pointer.delta = horizontal ? new Vector2(-20f, 0f) : new Vector2(0f, 20f);
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
        }

        [UnityTest]
        public IEnumerator MouseWheelWithCatalogueOpen_StillZoomsProductionCamera()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            var camera = UnityEngine.Camera.main;
            Assert.That(camera, Is.Not.Null);
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
                    scroll = new Vector2(0f, 120f)
                });
                var deadline = Time.realtimeSinceStartup + 2f;
                while (camera.orthographicSize >= before && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.That(sawWheel, Is.True,
                    "A real positive wheel state must reach the virtual mouse before testing Camera.");
                Assert.That(camera.orthographicSize, Is.LessThan(before),
                    "The existing mouse wheel zoom must remain available with Catalogue open. "
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

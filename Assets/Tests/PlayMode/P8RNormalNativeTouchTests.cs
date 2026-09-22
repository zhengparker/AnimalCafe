using System.Collections;
using System.Collections.Generic;
using AnimalCafe.Camera;
using AnimalCafe.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Tests.PlayMode
{
    // Exercise native Input System events through the production Normal camera adapter.
    // 使用真实 Touch 事件测试 Normal adapter，不把 Touch 模拟为 Mouse。
    public sealed class P8RNormalNativeTouchTests : InputTestFixture
    {
        private GameObject root;
        private MouseCameraInput input;
        private CafeCameraController controller;
        private UnityEngine.Camera camera;
        private CameraSettings settings;
        private Touchscreen touch;
        private readonly List<Behaviour> disabledUi = new();

        public override void Setup()
        {
            base.Setup();
            foreach (var item in Object.FindObjectsByType<GraphicRaycaster>(FindObjectsSortMode.None))
                Disable(item);
            foreach (var item in Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None))
                Disable(item);
            touch = InputSystem.AddDevice<Touchscreen>();
            root = new GameObject("Normal native touch fixture");
            camera = root.AddComponent<UnityEngine.Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.transform.position = new Vector3(0f, 10f, 0f);
            camera.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            settings = ScriptableObject.CreateInstance<CameraSettings>();
            settings.PanSpeed = 0.02f;
            settings.ZoomSpeed = 1f;
            input = root.AddComponent<MouseCameraInput>();
            controller = root.AddComponent<CafeCameraController>();
            controller.Configure(camera, settings, input);
        }

        public override void TearDown()
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(settings);
            foreach (var item in disabledUi)
                if (item != null) item.enabled = true;
            disabledUi.Clear();
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator NativeTap_ReportsOnePressAndReleaseToSharedConsumers()
        {
            yield return Send(1, TouchPhase.Began, new Vector2(200f, 200f));
            Assert.That(input.ReadFrame().PointerPressed, Is.True);
            yield return Send(1, TouchPhase.Ended, new Vector2(202f, 200f));
            var frame = input.ReadFrame();
            Assert.That(frame.TapReleased, Is.True);
            Assert.That(frame.PointerReleased, Is.True);
            Assert.That(frame.PointerPosition, Is.EqualTo(new Vector2(202f, 200f)));
            Assert.That(input.ReadFrame().TapReleased, Is.True, "Same-frame consumers must share the tap.");
            Assert.That(camera.transform.position.x, Is.Zero);
            yield return null;
            Assert.That(input.ReadFrame().TapReleased, Is.False, "A release is not repeated next frame.");
        }

        [UnityTest]
        public IEnumerator NativeDrag_UsesSharedPanAndNeverBecomesTap()
        {
            yield return Send(1, TouchPhase.Began, new Vector2(200f, 200f));
            yield return Send(1, TouchPhase.Moved, new Vector2(220f, 200f));
            Assert.That(camera.transform.position.x, Is.EqualTo(-0.4f).Within(0.001f));
            yield return Send(1, TouchPhase.Moved, new Vector2(200f, 200f));
            yield return Send(1, TouchPhase.Ended, new Vector2(200f, 200f));
            Assert.That(input.ReadFrame().TapReleased, Is.False, "Returning to origin cannot turn a drag into a tap.");
        }

        [UnityTest]
        public IEnumerator NativePinch_PreservesFractionalDistanceAndCancelsTap()
        {
            yield return Send(1, TouchPhase.Began, new Vector2(200f, 200f));
            yield return Send(2, TouchPhase.Began, new Vector2(300f, 200f));
            yield return Send(2, TouchPhase.Moved, new Vector2(320f, 200f));
            Assert.That(camera.orthographicSize, Is.EqualTo(7.5f).Within(0.001f), "20 pixels is half the shared 40-pixel zoom step.");
            Assert.That(camera.transform.position.x, Is.Zero, "Pinch must not also pan.");
            yield return null;
            Assert.That(camera.orthographicSize, Is.EqualTo(7.5f).Within(0.001f), "Stationary pinch must not accumulate zoom.");
            yield return Send(2, TouchPhase.Ended, new Vector2(320f, 200f));
            yield return Send(1, TouchPhase.Ended, new Vector2(200f, 200f));
            Assert.That(input.ReadFrame().TapReleased, Is.False);
        }

        [UnityTest]
        public IEnumerator NativeUiOrigin_DraggingOffUiNeverMovesCameraOrTapsScene()
        {
            CreateUiTarget(new Vector2(200f, 200f));
            yield return null;
            yield return Send(1, TouchPhase.Began, new Vector2(200f, 200f));
            yield return Send(1, TouchPhase.Moved, new Vector2(350f, 200f));
            Assert.That(camera.transform.position.x, Is.Zero);
            yield return Send(1, TouchPhase.Ended, new Vector2(350f, 200f));
            Assert.That(input.ReadFrame().TapReleased, Is.False);
            yield return Send(2, TouchPhase.Began, new Vector2(400f, 200f));
            yield return Send(2, TouchPhase.Moved, new Vector2(420f, 200f));
            Assert.That(camera.transform.position.x, Is.EqualTo(-0.4f).Within(0.001f), "A fresh scene press remains usable.");
        }

        [UnityTest]
        public IEnumerator NativeUiSecondFinger_CannotStartPinchOrLeaveAccidentalTap()
        {
            CreateUiTarget(new Vector2(300f, 200f));
            yield return null;
            yield return Send(1, TouchPhase.Began, new Vector2(200f, 200f));
            yield return Send(2, TouchPhase.Began, new Vector2(300f, 200f));
            yield return Send(2, TouchPhase.Moved, new Vector2(400f, 200f));
            Assert.That(camera.orthographicSize, Is.EqualTo(8f));
            yield return Send(2, TouchPhase.Ended, new Vector2(400f, 200f));
            yield return Send(1, TouchPhase.Ended, new Vector2(200f, 200f));
            Assert.That(input.ReadFrame().TapReleased, Is.False);
            yield return Send(3, TouchPhase.Began, new Vector2(200f, 200f));
            yield return Send(3, TouchPhase.Ended, new Vector2(200f, 200f));
            Assert.That(input.ReadFrame().TapReleased, Is.True);
        }

        [UnityTest]
        public IEnumerator NativeCanceledContact_IsNotTapAndNextPressRecovers()
        {
            yield return Send(1, TouchPhase.Began, new Vector2(200f, 200f));
            var pressed = input.ReadFrame();
            Assert.That(pressed.PointerPressed, Is.True);
            var originalPointerId = pressed.PointerId;
            yield return Send(1, TouchPhase.Canceled, new Vector2(200f, 200f));
            var canceled = input.ReadFrame();
            Assert.That(canceled.PointerReleased, Is.True);
            Assert.That(canceled.PointerId, Is.EqualTo(originalPointerId), "Cancellation releases the original composite UI pointer identity.");
            Assert.That(canceled.TapReleased, Is.False);
            yield return null;
            Assert.That(input.ReadFrame().PointerReleased, Is.False, "Cancellation releases ownership only once.");
            yield return Send(2, TouchPhase.Began, new Vector2(200f, 200f));
            yield return Send(2, TouchPhase.Ended, new Vector2(200f, 200f));
            Assert.That(input.ReadFrame().TapReleased, Is.True);
        }

        [UnityTest]
        public IEnumerator NativeDeviceLoss_DuringDragAllowsFreshDeviceGesture()
        {
            yield return Send(1, TouchPhase.Began, new Vector2(200f, 200f));
            var pressed = input.ReadFrame();
            Assert.That(pressed.PointerPressed, Is.True);
            var originalPointerId = pressed.PointerId;
            yield return Send(1, TouchPhase.Moved, new Vector2(220f, 200f));
            InputSystem.RemoveDevice(touch);
            yield return null;
            var disconnected = input.ReadFrame();
            Assert.That(disconnected.PointerReleased, Is.True);
            Assert.That(disconnected.PointerId, Is.EqualTo(originalPointerId), "Device loss releases the removed device's composite UI pointer identity.");
            Assert.That(disconnected.TapReleased, Is.False);
            yield return null;
            Assert.That(input.ReadFrame().PointerReleased, Is.False, "Device loss releases ownership only once.");
            touch = InputSystem.AddDevice<Touchscreen>();
            yield return Send(7, TouchPhase.Began, new Vector2(200f, 200f));
            yield return Send(7, TouchPhase.Moved, new Vector2(220f, 200f));
            Assert.That(camera.transform.position.x, Is.EqualTo(-0.8f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator CameraModeHandover_HeldTouchCannotResumeUntilFreshPress()
        {
            yield return Send(1, TouchPhase.Began, new Vector2(200f, 200f));
            yield return Send(1, TouchPhase.Moved, new Vector2(220f, 200f));
            controller.enabled = false;
            yield return Send(1, TouchPhase.Moved, new Vector2(240f, 200f));
            controller.enabled = true;
            yield return Send(1, TouchPhase.Moved, new Vector2(260f, 200f));
            Assert.That(camera.transform.position.x, Is.EqualTo(-0.4f).Within(0.001f));
            yield return Send(1, TouchPhase.Ended, new Vector2(260f, 200f));
            Assert.That(input.ReadFrame().TapReleased, Is.False);
            yield return Send(2, TouchPhase.Began, new Vector2(200f, 200f));
            yield return Send(2, TouchPhase.Moved, new Vector2(220f, 200f));
            Assert.That(camera.transform.position.x, Is.EqualTo(-0.8f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator ActiveNativeTouch_ExcludesSimultaneousMouseWheel()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            yield return Send(1, TouchPhase.Began, new Vector2(200f, 200f));
            InputSystem.QueueStateEvent(mouse, new MouseState { scroll = new Vector2(0f, 120f) });
            yield return Send(1, TouchPhase.Moved, new Vector2(220f, 200f));
            Assert.That(camera.transform.position.x, Is.EqualTo(-0.4f).Within(0.001f));
            Assert.That(camera.orthographicSize, Is.EqualTo(8f), "Only the active touch gesture may drive the camera.");
        }

        [UnityTest]
        public IEnumerator IdleTouchscreen_PreservesMouseDragAndWholeWheelStep()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(200f, 200f) }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(220f, 200f), delta = new Vector2(20f, 0f), scroll = new Vector2(0f, 120f) }.WithButton(MouseButton.Left));
            yield return null;
            Assert.That(camera.transform.position.x, Is.EqualTo(-0.4f).Within(0.001f));
            Assert.That(camera.orthographicSize, Is.EqualTo(7f));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = new Vector2(220f, 200f) });
            yield return null;
            Assert.That(input.ReadFrame().PointerReleased, Is.True);
            Assert.That(input.ReadFrame().TapReleased, Is.False);
        }

        private IEnumerator Send(int id, TouchPhase phase, Vector2 position)
        {
            InputSystem.QueueStateEvent(touch, new TouchState
            {
                touchId = id, phase = phase, position = position,
                pressure = phase == TouchPhase.Ended || phase == TouchPhase.Canceled ? 0f : 1f
            });
            yield return null;
        }

        private void CreateUiTarget(Vector2 center)
        {
            var eventObject = new GameObject("Test EventSystem", typeof(EventSystem));
            eventObject.transform.SetParent(root.transform);
            var canvasObject = new GameObject("Test Canvas", typeof(Canvas), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(root.transform);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var target = new GameObject("UI raycast target", typeof(RectTransform), typeof(Image));
            target.transform.SetParent(canvasObject.transform, false);
            var rect = (RectTransform)target.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = center;
            rect.sizeDelta = new Vector2(60f, 60f);
            Canvas.ForceUpdateCanvases();
        }

        private void Disable(Behaviour item)
        {
            if (!item.enabled) return;
            item.enabled = false;
            disabledUi.Add(item);
        }
    }
}

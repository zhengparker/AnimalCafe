using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using AnimalCafe.Camera;
using AnimalCafe.Decoration.Input;
using AnimalCafe.UI.Feedback;
using UnityEngine.UI;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Input
{
    /// <summary>
    /// 将原生 Touch 与 Editor Mouse 转换为共享的 Camera / tap input。
    /// Keeps the existing serialized binding while routing native Touch and Editor Mouse.
    /// </summary>
    public sealed class MouseCameraInput : MonoBehaviour, ICameraInputSource,
        IDecorationTouchHitClassifier
    {
        [SerializeField, Min(0f)]
        private float dragThresholdPixels = 6f;

        [SerializeField]
        private CameraSettings settings;

        private Vector2 pressPosition;
        private bool isPointerDown;
        private bool mouseGestureStartedOnUi;
        private bool exceededDragThreshold;
        private int cachedFrameNumber = -1;
        private CameraInputFrame cachedFrame;
        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
        private EventSystem uiPointerEventSystem;
        private PointerEventData uiPointerEventData;
        private InputSystemDecorationTouchSource touchSource;
        private DecorationTouchRouter touchRouter;
        private bool ownsTouchSource;
        private int touchPointerId = -1;
        private Vector2 touchPointerPosition;
        private bool pendingTouchRelease;

        private void OnEnable()
        {
            touchSource = GetComponent<InputSystemDecorationTouchSource>();
            if (touchSource == null)
            {
                touchSource = gameObject.AddComponent<InputSystemDecorationTouchSource>();
                ownsTouchSource = true;
            }
            else if (ownsTouchSource)
            {
                touchSource.enabled = true;
            }

            cachedFrameNumber = -1;
        }

        private void OnDisable()
        {
            CancelTouchGesture();
            if (ownsTouchSource && touchSource != null)
            {
                touchSource.enabled = false;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CancelTouchGesture();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) CancelTouchGesture();
        }

        public void CancelTouchGesture()
        {
            touchRouter?.CancelGesture();
            pendingTouchRelease |= touchPointerId != -1;
            cachedFrameNumber = -1;
        }

        public float DragThresholdPixels
        {
            get => dragThresholdPixels;
            set => dragThresholdPixels = Mathf.Max(0f, value);
        }

        public static bool IsTapDistance(float dragDistance, float threshold)
        {
            return dragDistance <= Mathf.Max(0f, threshold);
        }

        public CameraInputFrame ReadFrame()
        {
            if (!isActiveAndEnabled)
            {
                return default;
            }

            // Camera 和 interaction 会在同一 frame 读取同一个 adapter。
            // Cache the frame so both consumers receive the same tap result.
            if (cachedFrameNumber == UnityEngine.Time.frameCount)
            {
                return cachedFrame;
            }

            cachedFrameNumber = UnityEngine.Time.frameCount;
            if (TryReadTouchFrame(out cachedFrame))
            {
                // One gesture has one source, even when a Mouse is also connected.
                // Touch 持有期间不叠加 Mouse，避免模拟鼠标导致双重移动。
                isPointerDown = false;
                return cachedFrame;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                cachedFrame = default;
                return cachedFrame;
            }

            var pointerPosition = mouse.position.ReadValue();
            var pointerPressed = mouse.leftButton.wasPressedThisFrame;
            var pointerReleased = mouse.leftButton.wasReleasedThisFrame;
            if (pointerPressed)
            {
                isPointerDown = true;
                exceededDragThreshold = false;
                pressPosition = pointerPosition;
                // Ownership follows the press origin until release, even outside the UI.
                // UI 起点持有整次鼠标手势，拖出面板也不能带动 Camera 或选中场景。
                mouseGestureStartedOnUi = IsUiAt(pointerPosition);
            }

            var activeThreshold = settings != null
                ? settings.DragThresholdPixels
                : dragThresholdPixels;

            if (isPointerDown
                && !IsTapDistance(
                    Vector2.Distance(pressPosition, pointerPosition),
                    activeThreshold))
            {
                exceededDragThreshold = true;
            }

            var panDelta = isPointerDown && exceededDragThreshold && !mouseGestureStartedOnUi
                ? mouse.delta.ReadValue()
                : Vector2.zero;

            var tapReleased = false;
            if (isPointerDown && pointerReleased)
            {
                tapReleased = !exceededDragThreshold && !mouseGestureStartedOnUi;
                isPointerDown = false;
                mouseGestureStartedOnUi = false;
            }

            var zoomDelta = mouse.scroll.ReadValue().y;
            if (!Mathf.Approximately(zoomDelta, 0f)
                && IsPointerOverVisibleReadiness(pointerPosition))
            {
                zoomDelta = 0f;
            }

            cachedFrame = new CameraInputFrame(
                panDelta,
                zoomDelta,
                tapReleased,
                pointerPosition,
                mouse.deviceId,
                pointerPressed,
                pointerReleased);
            return cachedFrame;
        }

        private bool TryReadTouchFrame(out CameraInputFrame inputFrame)
        {
            inputFrame = default;
            if (touchSource == null)
            {
                return false;
            }

            touchRouter ??= new DecorationTouchRouter(
                settings != null ? settings.DragThresholdPixels : dragThresholdPixels, 0f);
            var frame = touchSource.ReadFrame();
            if (frame.Touches.Length == 0
                && touchRouter.PrimaryTouchId == DecorationTouchRouter.NoTouchId
                && !touchRouter.IsSuppressingUntilAllTouchesUp
                && !pendingTouchRelease)
            {
                return false;
            }

            var previousPrimary = touchRouter.PrimaryTouchId;
            var primaryCandidate = previousPrimary;
            var joinedUi = false;
            for (var index = 0; index < frame.Touches.Length; index++)
            {
                var point = frame.Touches[index];
                if (point.TouchId == previousPrimary)
                {
                    touchPointerPosition = point.Position;
                }

                if (point.Phase != InputTouchPhase.Began) continue;
                if (primaryCandidate == DecorationTouchRouter.NoTouchId)
                {
                    primaryCandidate = point.TouchId;
                }
                else if (point.TouchId != primaryCandidate && IsUiAt(point.Position))
                {
                    joinedUi = true;
                }
            }

            var result = touchRouter.ProcessFrame(frame, this);
            if (joinedUi)
            {
                // A second UI contact must never become a pinch or release as a tap.
                // 第二指点在 UI 时取消场景手势，避免 UI 点击引发 pinch 或误选。
                touchRouter.CancelGesture();
                result = default;
            }

            var primary = touchRouter.PrimaryTouchId;
            var pressed = previousPrimary == DecorationTouchRouter.NoTouchId
                && primary != DecorationTouchRouter.NoTouchId;
            if (pressed)
            {
                for (var index = 0; index < frame.Touches.Length; index++)
                {
                    var point = frame.Touches[index];
                    if (point.TouchId != primary) continue;
                    touchPointerPosition = point.Position;
                    touchPointerId = GetTouchPointerId(primary);
                    break;
                }
            }

            var released = pendingTouchRelease
                || (previousPrimary != DecorationTouchRouter.NoTouchId
                    && primary == DecorationTouchRouter.NoTouchId);
            inputFrame = new CameraInputFrame(
                result.CameraPanRequested ? result.CameraPanDelta : Vector2.zero,
                0f,
                result.TapReleased && !pendingTouchRelease,
                touchPointerPosition,
                touchPointerId,
                pressed,
                released,
                result.PinchZoomRequested ? result.PinchDistanceDelta : 0f);
            pendingTouchRelease = false;
            if (released) touchPointerId = -1;
            return true;
        }

        private static int GetTouchPointerId(int touchId)
        {
            // Match InputSystemUIInputModule's composite identity.
            // 与 UI module 使用同一个 pointerId，SceneInteraction 才能正确释放 ownership。
            foreach (var touch in UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches)
            {
                if (touch.touchId == touchId)
                {
                    unchecked { return (touch.finger.screen.deviceId << 24) + touchId; }
                }
            }

            return touchId;
        }

        DecorationTouchHit IDecorationTouchHitClassifier.ClassifyBegan(
            int touchId, Vector2 screenPosition)
        {
            return new DecorationTouchHit(IsUiAt(screenPosition)
                ? DecorationTouchHitKind.Ui : DecorationTouchHitKind.Scene);
        }

        private bool IsUiAt(Vector2 screenPosition)
        {
            RaycastUi(screenPosition);
            foreach (var hit in uiRaycastResults)
            {
                if (hit.module is GraphicRaycaster raycaster
                    && raycaster.isActiveAndEnabled
                    && raycaster.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsPointerOverVisibleReadiness(Vector2 screenPosition)
        {
            RaycastUi(screenPosition);
            for (var index = 0; index < uiRaycastResults.Count; index++)
            {
                var readiness = uiRaycastResults[index].gameObject
                    .GetComponentInParent<ValidationMessageView>();
                if (readiness != null
                    && readiness.IsVisible
                    && readiness.gameObject.activeInHierarchy)
                {
                    return true;
                }
            }

            return false;
        }

        private void RaycastUi(Vector2 screenPosition)
        {
            uiRaycastResults.Clear();
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            if (!ReferenceEquals(uiPointerEventSystem, eventSystem)
                || uiPointerEventData == null)
            {
                uiPointerEventSystem = eventSystem;
                uiPointerEventData = new PointerEventData(eventSystem);
            }
            else
            {
                uiPointerEventData.Reset();
            }

            uiPointerEventData.position = screenPosition;
            eventSystem.RaycastAll(uiPointerEventData, uiRaycastResults);
        }
    }
}

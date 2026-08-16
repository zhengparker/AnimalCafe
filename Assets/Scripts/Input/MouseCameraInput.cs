using UnityEngine;
using UnityEngine.InputSystem;
using AnimalCafe.Camera;

namespace AnimalCafe.Input
{
    /// <summary>
    /// 将 mouse wheel、left drag 和 tap 转换为设备无关 input。
    /// Converts mouse wheel, left drag, and tap into device-independent input.
    /// </summary>
    public sealed class MouseCameraInput : MonoBehaviour, ICameraInputSource
    {
        [SerializeField, Min(0f)]
        private float dragThresholdPixels = 6f;

        [SerializeField]
        private CameraSettings settings;

        private Vector2 pressPosition;
        private bool isPointerDown;
        private bool exceededDragThreshold;
        private int cachedFrameNumber = -1;
        private CameraInputFrame cachedFrame;

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
            // Camera 和 interaction 会在同一 frame 读取同一个 adapter。
            // Cache the frame so both consumers receive the same tap result.
            if (cachedFrameNumber == UnityEngine.Time.frameCount)
            {
                return cachedFrame;
            }

            cachedFrameNumber = UnityEngine.Time.frameCount;
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

            var panDelta = isPointerDown && exceededDragThreshold
                ? mouse.delta.ReadValue()
                : Vector2.zero;

            var tapReleased = false;
            if (isPointerDown && pointerReleased)
            {
                tapReleased = !exceededDragThreshold;
                isPointerDown = false;
            }

            cachedFrame = new CameraInputFrame(
                panDelta,
                mouse.scroll.ReadValue().y,
                tapReleased,
                pointerPosition,
                mouse.deviceId,
                pointerPressed,
                pointerReleased);
            return cachedFrame;
        }
    }
}

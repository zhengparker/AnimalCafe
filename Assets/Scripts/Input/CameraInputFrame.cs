using UnityEngine;

namespace AnimalCafe.Input
{
    /// <summary>
    /// 与具体设备无关的单帧 Camera 和 pointer input。
    /// Device-independent camera and pointer input for one frame.
    /// </summary>
    public readonly struct CameraInputFrame
    {
        public CameraInputFrame(
            Vector2 panDelta,
            float zoomDelta,
            bool tapReleased,
            Vector2 pointerPosition,
            int pointerId = -1,
            bool pointerPressed = false,
            bool pointerReleased = false)
        {
            PanDelta = panDelta;
            ZoomDelta = zoomDelta;
            TapReleased = tapReleased;
            PointerPosition = pointerPosition;
            PointerId = pointerId;
            PointerPressed = pointerPressed;
            PointerReleased = pointerReleased || tapReleased;
        }

        public Vector2 PanDelta { get; }

        public float ZoomDelta { get; }

        public bool TapReleased { get; }

        public Vector2 PointerPosition { get; }

        /// <summary>
        /// Input System pointer identity for this frame. -1 is the legacy mouse default.
        /// è¯¥ frame çš„ Input System pointer identityï¼›-1 ä¿ç•™ä¸ºæ—§ mouse é»˜è®¤å€¼ã€‚
        /// </summary>
        public int PointerId { get; }

        public bool PointerPressed { get; }

        public bool PointerReleased { get; }
    }
}

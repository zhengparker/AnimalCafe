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
            Vector2 pointerPosition)
        {
            PanDelta = panDelta;
            ZoomDelta = zoomDelta;
            TapReleased = tapReleased;
            PointerPosition = pointerPosition;
        }

        public Vector2 PanDelta { get; }

        public float ZoomDelta { get; }

        public bool TapReleased { get; }

        public Vector2 PointerPosition { get; }
    }
}

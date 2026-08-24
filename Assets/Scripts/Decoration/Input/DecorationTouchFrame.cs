using System;
using UnityEngine;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Decoration.Input
{
    public enum DecorationGestureOwner
    {
        None,
        Ui,
        Furniture,
        Camera,
        Pinch
    }

    public enum DecorationTouchHitKind
    {
        None,
        Ui,
        Furniture,
        Scene
    }

    public readonly struct DecorationTouchPoint
    {
        public DecorationTouchPoint(
            int touchId,
            Vector2 position,
            Vector2 delta,
            InputTouchPhase phase)
        {
            TouchId = touchId;
            Position = position;
            Delta = delta;
            Phase = phase;
        }

        public int TouchId { get; }
        public Vector2 Position { get; }
        public Vector2 Delta { get; }
        public InputTouchPhase Phase { get; }

        public bool IsActive => Phase == InputTouchPhase.Began
            || Phase == InputTouchPhase.Moved
            || Phase == InputTouchPhase.Stationary;

        public bool IsTerminal => Phase == InputTouchPhase.Ended
            || Phase == InputTouchPhase.Canceled;
    }

    /// <summary>
    /// Source-owned current-frame view. Do not store it across a frame, yield or await.
    /// 由 source 持有的当前 frame 视图；不可跨 frame、yield 或 await 保存。
    /// </summary>
    public readonly ref struct DecorationTouchFrame
    {
        private readonly int activeTouchCount;

        public DecorationTouchFrame(
            int frameNumber,
            ReadOnlySpan<DecorationTouchPoint> touches)
        {
            FrameNumber = frameNumber;
            Touches = touches;
            var activeCount = 0;
            for (var index = 0; index < touches.Length; index++)
            {
                if (touches[index].IsActive)
                {
                    activeCount++;
                }
            }

            activeTouchCount = activeCount;
        }

        public int FrameNumber { get; }
        public ReadOnlySpan<DecorationTouchPoint> Touches { get; }
        public int ActiveTouchCount => activeTouchCount;
    }
}

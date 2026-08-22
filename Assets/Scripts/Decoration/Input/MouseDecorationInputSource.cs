using System;
using UnityEngine;
using UnityEngine.InputSystem;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Decoration.Input
{
    public interface IMouseDecorationInputSource : IDecorationTouchSource
    {
        bool HasActivePointer { get; }
        float ReadScrollDelta();
        void Reset();
    }

    /// <summary>
    /// Adapts one Mouse button gesture to the shared Decoration semantic frame.
    /// 将单个 Mouse 左键手势转换为 Decoration 共用语义帧。
    /// </summary>
    public sealed class MouseDecorationInputSource : MonoBehaviour, IMouseDecorationInputSource
    {
        public const int PointerId = -1001;

        private readonly DecorationTouchPoint[] buffer = new DecorationTouchPoint[1];
        private int count;
        private int cachedFrameNumber = -1;
        private int scrollConsumedFrame = -1;
        private bool hasActivePointer;
        private bool hasPendingCancellation;
        private bool suppressUntilRelease;
        private Vector2 previousPosition;
        private DecorationTouchPoint pendingCancellation;

        public bool HasActivePointer => hasActivePointer;

        private void OnEnable()
        {
            if (!hasPendingCancellation)
            {
                Reset();
            }
        }

        private void OnDisable()
        {
            CancelActivePointer();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                CancelActivePointer();
            }
        }

        public DecorationTouchFrame ReadFrame()
        {
            var frameNumber = Time.frameCount;
            if (cachedFrameNumber != frameNumber)
            {
                Refresh(frameNumber);
            }

            return new DecorationTouchFrame(
                cachedFrameNumber,
                new ReadOnlySpan<DecorationTouchPoint>(buffer, 0, count));
        }

        public float ReadScrollDelta()
        {
            var frameNumber = Time.frameCount;
            if (!isActiveAndEnabled || scrollConsumedFrame == frameNumber)
            {
                return 0f;
            }

            scrollConsumedFrame = frameNumber;
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return 0f;
            }

            var value = mouse.scroll.ReadValue().y;
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        public void Reset()
        {
            var mouse = Mouse.current;
            suppressUntilRelease = mouse != null && mouse.leftButton.isPressed;
            hasActivePointer = false;
            hasPendingCancellation = false;
            count = 0;
            cachedFrameNumber = -1;
            scrollConsumedFrame = -1;
            previousPosition = mouse?.position.ReadValue() ?? Vector2.zero;
        }

        private void Refresh(int frameNumber)
        {
            count = 0;
            cachedFrameNumber = frameNumber;
            if (hasPendingCancellation)
            {
                buffer[0] = pendingCancellation;
                count = 1;
                hasPendingCancellation = false;
                return;
            }

            if (!isActiveAndEnabled)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                hasActivePointer = false;
                return;
            }

            var position = mouse.position.ReadValue();
            if (suppressUntilRelease)
            {
                if (!mouse.leftButton.isPressed)
                {
                    suppressUntilRelease = false;
                }
                previousPosition = position;
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                hasActivePointer = true;
                previousPosition = position;
                buffer[0] = new DecorationTouchPoint(
                    PointerId, position, Vector2.zero, InputTouchPhase.Began);
                count = 1;
                return;
            }

            if (mouse.leftButton.wasReleasedThisFrame && hasActivePointer)
            {
                var delta = position - previousPosition;
                hasActivePointer = false;
                previousPosition = position;
                buffer[0] = new DecorationTouchPoint(
                    PointerId, position, delta, InputTouchPhase.Ended);
                count = 1;
                return;
            }

            if (!hasActivePointer || !mouse.leftButton.isPressed)
            {
                hasActivePointer = false;
                previousPosition = position;
                return;
            }

            var movement = position - previousPosition;
            previousPosition = position;
            buffer[0] = new DecorationTouchPoint(
                PointerId,
                position,
                movement,
                movement == Vector2.zero
                    ? InputTouchPhase.Stationary
                    : InputTouchPhase.Moved);
            count = 1;
        }

        private void CancelActivePointer()
        {
            var mouse = Mouse.current;
            var position = mouse?.position.ReadValue() ?? previousPosition;
            if (hasActivePointer && !hasPendingCancellation)
            {
                pendingCancellation = new DecorationTouchPoint(
                    PointerId,
                    position,
                    position - previousPosition,
                    InputTouchPhase.Canceled);
                hasPendingCancellation = true;
            }

            suppressUntilRelease = mouse != null && mouse.leftButton.isPressed;
            hasActivePointer = false;
            count = 0;
            cachedFrameNumber = -1;
            scrollConsumedFrame = -1;
            previousPosition = position;
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using EnhancedTouchPoint = UnityEngine.InputSystem.EnhancedTouch.Touch;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Decoration.Input
{
    /// <summary>
    /// Copies EnhancedTouch records once per Unity frame into one reusable buffer.
    /// 每个 Unity frame 只复制一次 EnhancedTouch records，并复用同一个 buffer。
    /// </summary>
    public sealed class InputSystemDecorationTouchSource : MonoBehaviour, IDecorationTouchSource
    {
        private const int InitialCapacity = 10;

        private DecorationTouchPoint[] buffer = new DecorationTouchPoint[InitialCapacity];
        private int count;
        private int cachedFrameNumber = -1;
        private bool ownsEnhancedTouchSupport;

        private void OnEnable()
        {
            if (!ownsEnhancedTouchSupport)
            {
                EnhancedTouchSupport.Enable();
                ownsEnhancedTouchSupport = true;
            }

            ClearCache();
        }

        private void OnDisable()
        {
            if (ownsEnhancedTouchSupport)
            {
                EnhancedTouchSupport.Disable();
                ownsEnhancedTouchSupport = false;
            }

            ClearCache();
        }

        public DecorationTouchFrame ReadFrame()
        {
            var frameNumber = Time.frameCount;
            if (!ownsEnhancedTouchSupport)
            {
                return new DecorationTouchFrame(
                    frameNumber,
                    ReadOnlySpan<DecorationTouchPoint>.Empty);
            }

            if (cachedFrameNumber != frameNumber)
            {
                Refresh(frameNumber);
            }

            return new DecorationTouchFrame(
                cachedFrameNumber,
                new ReadOnlySpan<DecorationTouchPoint>(buffer, 0, count));
        }

        private void Refresh(int frameNumber)
        {
            var activeTouches = EnhancedTouchPoint.activeTouches;
            EnsureCapacity(activeTouches.Count);
            count = 0;
            for (var index = 0; index < activeTouches.Count; index++)
            {
                var touch = activeTouches[index];
                if (touch.phase == InputTouchPhase.None)
                {
                    continue;
                }

                buffer[count++] = new DecorationTouchPoint(
                    touch.touchId,
                    touch.screenPosition,
                    touch.delta,
                    touch.phase);
            }

            cachedFrameNumber = frameNumber;
        }

        private void EnsureCapacity(int requiredCapacity)
        {
            if (buffer.Length >= requiredCapacity)
            {
                return;
            }

            var newCapacity = Math.Max(requiredCapacity, buffer.Length * 2);
            Array.Resize(ref buffer, newCapacity);
        }

        private void ClearCache()
        {
            count = 0;
            cachedFrameNumber = -1;
        }
    }
}

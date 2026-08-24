using UnityEngine;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Decoration.Input
{
    public readonly struct DecorationTouchHit
    {
        public DecorationTouchHit(
            DecorationTouchHitKind kind,
            string furnitureInstanceId = null)
        {
            Kind = kind;
            FurnitureInstanceId = furnitureInstanceId;
        }

        public DecorationTouchHitKind Kind { get; }
        public string FurnitureInstanceId { get; }
    }

    public interface IDecorationTouchHitClassifier
    {
        DecorationTouchHit ClassifyBegan(int touchId, Vector2 screenPosition);
    }

    public readonly struct DecorationTouchRoutingResult
    {
        internal DecorationTouchRoutingResult(
            DecorationGestureOwner owner,
            DecorationTouchHit originHit,
            bool tapReleased = false,
            bool furnitureDragRequested = false,
            Vector2 furnitureDragScreenPosition = default,
            bool cameraPanRequested = false,
            Vector2 cameraPanDelta = default,
            bool pinchZoomRequested = false,
            float pinchDistanceDelta = 0f)
        {
            Owner = owner;
            OriginHit = originHit;
            TapReleased = tapReleased;
            FurnitureDragRequested = furnitureDragRequested;
            FurnitureDragScreenPosition = furnitureDragScreenPosition;
            CameraPanRequested = cameraPanRequested;
            CameraPanDelta = cameraPanDelta;
            PinchZoomRequested = pinchZoomRequested;
            PinchDistanceDelta = pinchDistanceDelta;
        }

        public DecorationGestureOwner Owner { get; }
        public DecorationTouchHit OriginHit { get; }
        public bool TapReleased { get; }
        public bool FurnitureDragRequested { get; }
        public Vector2 FurnitureDragScreenPosition { get; }
        public bool CameraPanRequested { get; }
        public Vector2 CameraPanDelta { get; }
        public bool PinchZoomRequested { get; }
        public float PinchDistanceDelta { get; }
    }

    public sealed class DecorationTouchRouter
    {
        public const int NoTouchId = -1;

        private readonly float dragThresholdPixels;
        private readonly float furnitureDragOffsetPixels;

        private DecorationGestureOwner owner;
        private DecorationGestureOwner prePinchOwner;
        private int primaryTouchId = NoTouchId;
        private int secondaryTouchId = NoTouchId;
        private Vector2 primaryPressPosition;
        private DecorationTouchHit originHit;
        private float previousPinchDistance;
        private bool isDragging;
        private bool isSuppressingUntilAllTouchesUp;
        private int lastProcessedFrame = int.MinValue;

        public DecorationTouchRouter(
            float dragThresholdPixels,
            float furnitureDragOffsetPixels)
        {
            this.dragThresholdPixels = IsFinite(dragThresholdPixels)
                ? Mathf.Max(0f, dragThresholdPixels)
                : 0f;
            this.furnitureDragOffsetPixels = IsFinite(furnitureDragOffsetPixels)
                ? Mathf.Max(0f, furnitureDragOffsetPixels)
                : 0f;
        }

        public DecorationGestureOwner Owner => owner;
        public int PrimaryTouchId => primaryTouchId;
        public int SecondaryTouchId => secondaryTouchId;
        public bool IsDragging => isDragging;
        public bool IsSuppressingUntilAllTouchesUp => isSuppressingUntilAllTouchesUp;

        public DecorationTouchRoutingResult ProcessFrame(
            DecorationTouchFrame frame,
            IDecorationTouchHitClassifier hitClassifier)
        {
            if (frame.FrameNumber <= lastProcessedFrame)
            {
                return CurrentStateWithoutCommand();
            }

            lastProcessedFrame = frame.FrameNumber;
            if (isSuppressingUntilAllTouchesUp)
            {
                if (frame.ActiveTouchCount == 0)
                {
                    ClearGestureState();
                }

                return CurrentStateWithoutCommand();
            }

            var primaryTerminal = false;
            var primaryCanceled = false;
            var primaryTerminalTouch = default(DecorationTouchPoint);
            var secondaryTerminal = false;
            for (var index = 0; index < frame.Touches.Length; index++)
            {
                var touch = frame.Touches[index];
                if (!touch.IsTerminal)
                {
                    continue;
                }

                if (touch.TouchId == primaryTouchId)
                {
                    primaryTerminal = true;
                    primaryCanceled = touch.Phase == InputTouchPhase.Canceled;
                    primaryTerminalTouch = touch;
                }
                else if (touch.TouchId == secondaryTouchId)
                {
                    secondaryTerminal = true;
                }
            }

            // Primary terminal always wins over secondary replacement/new Began.
            // primary 结束优先，不能把剩余或新手指提升成新 gesture。
            if (primaryTerminal)
            {
                var releasedOrigin = originHit;
                // A short Began -> terminal can cross the drag threshold without a Moved frame.
                // 短 Began -> terminal 也必须用 terminal 位置锁定 drag threshold。
                if (!isDragging
                    && (owner == DecorationGestureOwner.Furniture
                        || owner == DecorationGestureOwner.Camera)
                    && Vector2.Distance(primaryPressPosition, primaryTerminalTouch.Position)
                        > dragThresholdPixels)
                {
                    isDragging = true;
                }

                var tapReleased = owner != DecorationGestureOwner.Pinch
                    && owner != DecorationGestureOwner.Ui
                    && owner != DecorationGestureOwner.None
                    && !isDragging
                    && !primaryCanceled
                    && frame.ActiveTouchCount == 0;

                if (frame.ActiveTouchCount > 0)
                {
                    ClearGestureState();
                    isSuppressingUntilAllTouchesUp = true;
                }
                else
                {
                    ClearGestureState();
                }

                return new DecorationTouchRoutingResult(
                    owner,
                    releasedOrigin,
                    tapReleased: tapReleased);
            }

            var skipSingleFingerCommand = false;
            if (owner == DecorationGestureOwner.Pinch && secondaryTerminal)
            {
                // Rebase the surviving primary so resuming drag cannot jump.
                // 以剩余 primary 重新建基线，下一 frame 恢复 drag 时不会跳动。
                owner = prePinchOwner;
                secondaryTouchId = NoTouchId;
                previousPinchDistance = 0f;
                if (TryFindActiveTouch(frame, primaryTouchId, out var primaryAfterPinch))
                {
                    primaryPressPosition = primaryAfterPinch.Position;
                }

                skipSingleFingerCommand = true;
            }

            var promotedToPinch = false;
            for (var index = 0; index < frame.Touches.Length; index++)
            {
                var touch = frame.Touches[index];
                if (touch.Phase != InputTouchPhase.Began)
                {
                    continue;
                }

                if (owner == DecorationGestureOwner.None)
                {
                    var hit = hitClassifier != null
                        ? hitClassifier.ClassifyBegan(touch.TouchId, touch.Position)
                        : default;
                    var classifiedOwner = OwnerFromHit(hit.Kind);
                    if (classifiedOwner == DecorationGestureOwner.None)
                    {
                        ClearGestureState();
                        isSuppressingUntilAllTouchesUp = frame.ActiveTouchCount > 0;
                        return CurrentStateWithoutCommand();
                    }

                    owner = classifiedOwner;
                    primaryTouchId = touch.TouchId;
                    primaryPressPosition = touch.Position;
                    originHit = hit;
                    continue;
                }

                if ((owner == DecorationGestureOwner.Furniture
                        || owner == DecorationGestureOwner.Camera)
                    && touch.TouchId != primaryTouchId
                    && secondaryTouchId == NoTouchId)
                {
                    prePinchOwner = owner;
                    owner = DecorationGestureOwner.Pinch;
                    secondaryTouchId = touch.TouchId;
                    isDragging = true;
                    promotedToPinch = true;
                    if (TryFindActiveTouch(frame, primaryTouchId, out var primary))
                    {
                        previousPinchDistance = Vector2.Distance(
                            primary.Position,
                            touch.Position);
                    }

                    continue;
                }

                // UI owns all joined touches; Pinch ignores any third touch.
                // UI 持有加入的所有 Touch；Pinch 忽略第三根手指。
            }

            if (owner == DecorationGestureOwner.Pinch)
            {
                if (!TryFindActiveTouch(frame, primaryTouchId, out var primary)
                    || !TryFindActiveTouch(frame, secondaryTouchId, out var secondary))
                {
                    return CurrentStateWithoutCommand();
                }

                var currentDistance = Vector2.Distance(primary.Position, secondary.Position);
                if (promotedToPinch)
                {
                    previousPinchDistance = currentDistance;
                    return CurrentStateWithoutCommand();
                }

                var distanceDelta = currentDistance - previousPinchDistance;
                previousPinchDistance = currentDistance;
                if (Mathf.Approximately(distanceDelta, 0f))
                {
                    return CurrentStateWithoutCommand();
                }

                return new DecorationTouchRoutingResult(
                    owner,
                    originHit,
                    pinchZoomRequested: true,
                    pinchDistanceDelta: distanceDelta);
            }

            if (skipSingleFingerCommand
                || owner == DecorationGestureOwner.None
                || owner == DecorationGestureOwner.Ui
                || !TryFindActiveTouch(frame, primaryTouchId, out var activePrimary))
            {
                return CurrentStateWithoutCommand();
            }

            if (!isDragging
                && Vector2.Distance(primaryPressPosition, activePrimary.Position) > dragThresholdPixels)
            {
                isDragging = true;
            }

            if (!isDragging)
            {
                return CurrentStateWithoutCommand();
            }

            if (owner == DecorationGestureOwner.Furniture)
            {
                return new DecorationTouchRoutingResult(
                    owner,
                    originHit,
                    furnitureDragRequested: true,
                    furnitureDragScreenPosition: activePrimary.Position
                        + Vector2.up * furnitureDragOffsetPixels);
            }

            if (owner == DecorationGestureOwner.Camera
                && activePrimary.Delta != Vector2.zero)
            {
                return new DecorationTouchRoutingResult(
                    owner,
                    originHit,
                    cameraPanRequested: true,
                    cameraPanDelta: activePrimary.Delta);
            }

            return CurrentStateWithoutCommand();
        }

        public void Reset()
        {
            ClearGestureState();
            lastProcessedFrame = int.MinValue;
        }

        private DecorationTouchRoutingResult CurrentStateWithoutCommand()
        {
            return new DecorationTouchRoutingResult(owner, originHit);
        }

        private void ClearGestureState()
        {
            owner = DecorationGestureOwner.None;
            prePinchOwner = DecorationGestureOwner.None;
            primaryTouchId = NoTouchId;
            secondaryTouchId = NoTouchId;
            primaryPressPosition = Vector2.zero;
            originHit = default;
            previousPinchDistance = 0f;
            isDragging = false;
            isSuppressingUntilAllTouchesUp = false;
        }

        private static DecorationGestureOwner OwnerFromHit(DecorationTouchHitKind hitKind)
        {
            return hitKind switch
            {
                DecorationTouchHitKind.Ui => DecorationGestureOwner.Ui,
                DecorationTouchHitKind.Furniture => DecorationGestureOwner.Furniture,
                DecorationTouchHitKind.Scene => DecorationGestureOwner.Camera,
                _ => DecorationGestureOwner.None
            };
        }

        private static bool TryFindActiveTouch(
            DecorationTouchFrame frame,
            int touchId,
            out DecorationTouchPoint touch)
        {
            for (var index = 0; index < frame.Touches.Length; index++)
            {
                var candidate = frame.Touches[index];
                if (candidate.TouchId == touchId && candidate.IsActive)
                {
                    touch = candidate;
                    return true;
                }
            }

            touch = default;
            return false;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

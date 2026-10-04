using System;
using UnityEngine;

namespace AnimalCafe.Navigation
{
    public enum MovementStatus { Arrived, Recovered, Failed, Cancelled, LayoutChanged }
    public enum NavigationFailure { None, InvalidArgument, UnknownActor, Busy, InvalidStart, InvalidTarget, IncompletePath, PathTimeout, MovementTimeout, RecoveryFailed, ActorUnavailable, LayoutUnavailable }
    public enum NavigationPathState { Pending, Complete, Invalid }

    public readonly struct NavigationTarget
    {
        public Vector3 Position { get; }
        public Vector3? Facing { get; }
        public Bounds? AllowedRegion { get; }

        public NavigationTarget(Vector3 position, Vector3? facing = null, Bounds? allowedRegion = null)
        {
            if (!Finite(position)) throw new ArgumentOutOfRangeException(nameof(position));
            if (facing.HasValue && (!Finite(facing.Value) || Math.Abs(facing.Value.y) > 0.001f ||
                                    new Vector2(facing.Value.x, facing.Value.z).sqrMagnitude < 0.000001f))
                throw new ArgumentOutOfRangeException(nameof(facing));
            if (allowedRegion.HasValue && (!Finite(allowedRegion.Value.center) ||
                                           !Finite(allowedRegion.Value.size) ||
                                           allowedRegion.Value.size.x <= 0 ||
                                           allowedRegion.Value.size.z <= 0 ||
                                           !allowedRegion.Value.Contains(position)))
                throw new ArgumentOutOfRangeException(nameof(allowedRegion));
            Position = position;
            Facing = facing;
            AllowedRegion = allowedRegion;
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    public readonly struct MoveStartResult
    {
        public bool Accepted { get; }
        public long RequestId { get; }
        public NavigationFailure Reason { get; }
        public MoveStartResult(bool accepted, long requestId, NavigationFailure reason)
        { Accepted = accepted; RequestId = requestId; Reason = reason; }
    }

    public readonly struct MovementResult
    {
        public long RequestId { get; }
        public string ActorId { get; }
        public MovementStatus Status { get; }
        public NavigationFailure Reason { get; }
        public NavigationFailure OriginalFailure { get; }
        public NavigationFailure RecoveryFailure { get; }
        public Vector3 FinalPosition { get; }
        public int RetryCount { get; }
        public MovementResult(long requestId, string actorId, MovementStatus status,
            NavigationFailure reason, NavigationFailure originalFailure,
            NavigationFailure recoveryFailure, Vector3 finalPosition, int retryCount)
        {
            RequestId = requestId; ActorId = actorId; Status = status; Reason = reason;
            OriginalFailure = originalFailure; RecoveryFailure = recoveryFailure;
            FinalPosition = finalPosition; RetryCount = retryCount;
        }
    }

    public readonly struct NavigationObservation
    {
        public int Generation { get; }
        public int LayoutRevision { get; }
        public NavigationPathState PathState { get; }
        public Vector3 Position { get; }
        public float PathLength { get; }
        public float RemainingDistance { get; }
        public float ActualSpeed { get; }
        public float FacingErrorDegrees { get; }
        public NavigationFailure Failure { get; }
        // Driver 验证后的实际站位；null 表示未采样，使用原 NavigationTarget.Position。
        public Vector3? ResolvedTargetPosition { get; }
        public NavigationObservation(int generation, int layoutRevision, NavigationPathState pathState,
            Vector3 position, float pathLength, float remainingDistance, float actualSpeed,
            float facingErrorDegrees = 0, NavigationFailure failure = NavigationFailure.None,
            Vector3? resolvedTargetPosition = null)
        {
            Generation = generation; LayoutRevision = layoutRevision; PathState = pathState;
            Position = position; PathLength = pathLength; RemainingDistance = remainingDistance;
            ActualSpeed = actualSpeed; FacingErrorDegrees = facingErrorDegrees; Failure = failure;
            ResolvedTargetPosition = resolvedTargetPosition;
        }
    }

    public interface INavigationDriver
    {
        Vector3 Position { get; }
        NavigationFailure BeginPath(NavigationTarget target, int generation, int layoutRevision);
        NavigationObservation Observe();
        void Stop();
    }
}

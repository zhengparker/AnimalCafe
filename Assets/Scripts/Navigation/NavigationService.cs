using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnimalCafe.Navigation
{
    // 请求只在此服务内改变状态；driver 仅提供路径和实际位置快照。
    public sealed class NavigationService
    {
        private sealed class Request
        {
            public long Id;
            public string ActorId;
            public INavigationDriver Driver;
            public NavigationTarget Original;
            public NavigationTarget? Recovery;
            public Action<MovementResult> Callback;
            public int Generation;
            public int LayoutRevision;
            public int RetryCount;
            public bool Recovering;
            public NavigationFailure OriginalFailure;
            public float PendingSeconds;
            public float SegmentSeconds;
            public float NoProgressSeconds;
            public float BestRemaining = float.PositiveInfinity;
            public float BestFacing = float.PositiveInfinity;
            public float SegmentLimit;
            public bool SegmentLimitSet;
        }

        private readonly NavigationSettings settings;
        private readonly Dictionary<string, INavigationDriver> actors = new Dictionary<string, INavigationDriver>();
        private readonly Dictionary<string, Request> byActor = new Dictionary<string, Request>();
        private readonly Dictionary<long, Request> byId = new Dictionary<long, Request>();
        private readonly HashSet<string> unavailableActors = new HashSet<string>();
        private long nextId = 1;
        private int layoutRevision;
        private bool shutDown;

        public NavigationService(NavigationSettings settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            settings.Validate();
        }

        public bool Register(string actorId, INavigationDriver driver)
        {
            if (shutDown || string.IsNullOrWhiteSpace(actorId) || driver == null || actors.ContainsKey(actorId))
                return false;
            actors.Add(actorId, driver);
            return true;
        }

        public void Unregister(string actorId)
        {
            if (string.IsNullOrWhiteSpace(actorId) || !actors.Remove(actorId)) return;
            unavailableActors.Remove(actorId);
            if (byActor.TryGetValue(actorId, out var request))
                Finish(request, MovementStatus.Failed, NavigationFailure.ActorUnavailable);
        }

        internal void SetActorAvailable(string actorId, bool available)
        {
            if (!actors.ContainsKey(actorId)) return;
            if (available) { unavailableActors.Remove(actorId); return; }
            // Gate before callback: a reentrant request cannot revive a disabled actor.
            unavailableActors.Add(actorId);
            if (byActor.TryGetValue(actorId, out var request))
                Finish(request, MovementStatus.Failed, NavigationFailure.ActorUnavailable);
        }

        public MoveStartResult MoveTo(string actorId, NavigationTarget target,
            Action<MovementResult> completed, NavigationTarget? recoveryTarget = null)
        {
            if (shutDown) return Rejected(NavigationFailure.ActorUnavailable);
            if (string.IsNullOrWhiteSpace(actorId) || completed == null || !ValidTarget(target) ||
                (recoveryTarget.HasValue && !ValidTarget(recoveryTarget.Value)))
                return Rejected(NavigationFailure.InvalidArgument);
            if (!actors.TryGetValue(actorId, out var driver)) return Rejected(NavigationFailure.UnknownActor);
            if (unavailableActors.Contains(actorId)) return Rejected(NavigationFailure.ActorUnavailable);
            if (byActor.ContainsKey(actorId)) return Rejected(NavigationFailure.Busy);
            if (nextId <= 0) return Rejected(NavigationFailure.InvalidArgument);

            var request = new Request
            {
                Id = nextId, ActorId = actorId, Driver = driver, Original = target,
                Recovery = recoveryTarget, Callback = completed, LayoutRevision = layoutRevision
            };
            nextId = nextId == long.MaxValue ? 0 : nextId + 1;
            byActor.Add(actorId, request);
            byId.Add(request.Id, request);
            Begin(request, target);
            return new MoveStartResult(true, request.Id, NavigationFailure.None);
        }

        public bool Cancel(long requestId)
        {
            if (!byId.TryGetValue(requestId, out var request)) return false;
            Finish(request, MovementStatus.Cancelled, NavigationFailure.None);
            return true;
        }

        public void Tick(float scaledDeltaTime)
        {
            if (float.IsNaN(scaledDeltaTime) || float.IsInfinity(scaledDeltaTime) || scaledDeltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(scaledDeltaTime));
            if (shutDown || scaledDeltaTime == 0) return;
            // Snapshot: callbacks may cancel, unregister, or submit another request.
            var requests = new List<Request>(byId.Values);
            foreach (var request in requests)
            {
                if (!byId.ContainsKey(request.Id)) continue;
                var observation = request.Driver.Observe();
                request.SegmentSeconds += scaledDeltaTime;
                if (observation.Generation != request.Generation ||
                    observation.LayoutRevision != request.LayoutRevision)
                {
                    // Stale path data cannot arrive, yet elapsed time must still count.
                    request.PendingSeconds += scaledDeltaTime;
                    if (request.PendingSeconds >= settings.PathPendingSeconds)
                        FailSegment(request, NavigationFailure.PathTimeout);
                    continue;
                }

                if (observation.PathState == NavigationPathState.Invalid ||
                    observation.Failure != NavigationFailure.None)
                {
                    FailSegment(request, observation.Failure == NavigationFailure.None
                        ? NavigationFailure.IncompletePath : observation.Failure);
                    continue;
                }

                if (observation.PathState == NavigationPathState.Pending)
                {
                    request.PendingSeconds += scaledDeltaTime;
                    if (request.PendingSeconds >= settings.PathPendingSeconds)
                        FailSegment(request, NavigationFailure.PathTimeout);
                    continue;
                }

                var target = request.Recovering ? request.Recovery.Value : request.Original;
                var destination = observation.ResolvedTargetPosition ?? target.Position;
                if (!ValidResolvedDestination(destination, target))
                {
                    Finish(request, MovementStatus.Failed, NavigationFailure.InvalidTarget);
                    continue;
                }
                if (Arrived(observation, target, destination))
                {
                    Finish(request, request.Recovering ? MovementStatus.Recovered : MovementStatus.Arrived,
                        NavigationFailure.None);
                    continue;
                }

                if (float.IsNaN(observation.RemainingDistance) || float.IsInfinity(observation.RemainingDistance) ||
                    observation.RemainingDistance < 0 || float.IsNaN(observation.PathLength) ||
                    float.IsInfinity(observation.PathLength) || observation.PathLength < 0)
                {
                    FailSegment(request, NavigationFailure.IncompletePath);
                    continue;
                }
                if (!request.SegmentLimitSet)
                {
                    request.SegmentLimit = Mathf.Max(settings.MinimumSegmentSeconds,
                        settings.SegmentLengthMultiplier * observation.PathLength / settings.MaxSpeed);
                    request.SegmentLimitSet = true;
                }
                // 远离目标时只有路径剩余距离减少才算进展；站在最终位置时允许原地转向。
                var distanceProgress = !float.IsInfinity(request.BestRemaining) &&
                    request.BestRemaining - observation.RemainingDistance >= settings.ProgressDistance;
                var atFinalPosition = Vector3.Distance(observation.Position, destination) <= settings.ArrivalDistance &&
                    observation.RemainingDistance <= settings.ArrivalDistance;
                var finalFacingProgress = atFinalPosition && target.Facing.HasValue &&
                    !float.IsInfinity(request.BestFacing) &&
                    request.BestFacing - observation.FacingErrorDegrees >= settings.FacingToleranceDegrees;
                if (distanceProgress || finalFacingProgress)
                {
                    request.BestRemaining = observation.RemainingDistance;
                    request.BestFacing = observation.FacingErrorDegrees;
                    request.NoProgressSeconds = 0;
                }
                else
                {
                    if (float.IsInfinity(request.BestRemaining)) request.BestRemaining = observation.RemainingDistance;
                    if (float.IsInfinity(request.BestFacing)) request.BestFacing = observation.FacingErrorDegrees;
                    request.NoProgressSeconds += scaledDeltaTime;
                }
                if (request.SegmentSeconds >= request.SegmentLimit ||
                    request.NoProgressSeconds >= settings.NoProgressSeconds)
                    FailSegment(request, NavigationFailure.MovementTimeout);
            }
        }

        public void InvalidateLayout(int revision)
        {
            if (revision == layoutRevision) return;
            SuspendLayout(revision);
        }
        internal void SuspendLayout(int revision)
        {
            layoutRevision = revision;
            foreach (var request in new List<Request>(byId.Values))
                if (byId.ContainsKey(request.Id))
                    Finish(request, MovementStatus.LayoutChanged, NavigationFailure.LayoutUnavailable);
        }

        public void Shutdown()
        {
            if (shutDown) return;
            shutDown = true;
            foreach (var request in new List<Request>(byId.Values))
                if (byId.ContainsKey(request.Id))
                    Finish(request, MovementStatus.Cancelled, NavigationFailure.ActorUnavailable);
            actors.Clear();
            unavailableActors.Clear();
        }

        private void Begin(Request request, NavigationTarget target)
        {
            request.Generation++;
            request.PendingSeconds = 0;
            request.SegmentSeconds = 0;
            request.NoProgressSeconds = 0;
            request.BestRemaining = float.PositiveInfinity;
            request.BestFacing = float.PositiveInfinity;
            request.SegmentLimit = settings.MinimumSegmentSeconds;
            request.SegmentLimitSet = false;
            var failure = request.Driver.BeginPath(target, request.Generation, request.LayoutRevision);
            if (failure != NavigationFailure.None) FailSegment(request, failure);
        }

        private void FailSegment(Request request, NavigationFailure failure)
        {
            if (!byId.ContainsKey(request.Id)) return;
            if (request.Recovering)
            {
                Finish(request, MovementStatus.Failed, NavigationFailure.RecoveryFailed, failure);
                return;
            }
            if (request.RetryCount == 0 && (failure == NavigationFailure.PathTimeout || failure == NavigationFailure.MovementTimeout))
            {
                request.RetryCount = 1;
                request.Driver.Stop();
                Begin(request, request.Original);
                return;
            }
            request.OriginalFailure = failure;
            if (request.Recovery.HasValue && failure != NavigationFailure.LayoutUnavailable && failure != NavigationFailure.ActorUnavailable && failure != NavigationFailure.InvalidStart &&
                failure != NavigationFailure.InvalidTarget && request.RetryCount == 1)
            {
                request.Recovering = true;
                request.Driver.Stop();
                Begin(request, request.Recovery.Value);
                return;
            }
            Finish(request, MovementStatus.Failed, failure);
        }

        private void Finish(Request request, MovementStatus status, NavigationFailure reason,
            NavigationFailure recoveryFailure = NavigationFailure.None)
        {
            // Remove first so reentrant callbacks can safely start a new request.
            byId.Remove(request.Id);
            byActor.Remove(request.ActorId);
            request.Driver.Stop();
            var finalPosition = request.Driver.Position;
            var result = new MovementResult(request.Id, request.ActorId, status, reason,
                request.OriginalFailure, recoveryFailure, finalPosition, request.RetryCount);
            try { request.Callback(result); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private bool Arrived(NavigationObservation observation, NavigationTarget target, Vector3 destination)
        {
            return Vector3.Distance(observation.Position, destination) <= settings.ArrivalDistance &&
                   observation.RemainingDistance <= settings.ArrivalDistance &&
                   observation.ActualSpeed <= settings.ArrivalSpeed &&
                   (!target.Facing.HasValue || observation.FacingErrorDegrees <= settings.FacingToleranceDegrees);
        }

        private bool ValidResolvedDestination(Vector3 destination, NavigationTarget target)
        {
            return Finite(destination) &&
                   Vector3.Distance(destination, target.Position) <= settings.TargetSampleDistance + settings.Epsilon &&
                   (!target.AllowedRegion.HasValue || target.AllowedRegion.Value.Contains(destination));
        }

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static bool ValidTarget(NavigationTarget target)
        {
            var p = target.Position;
            return !float.IsNaN(p.x) && !float.IsInfinity(p.x) &&
                   !float.IsNaN(p.y) && !float.IsInfinity(p.y) &&
                   !float.IsNaN(p.z) && !float.IsInfinity(p.z) &&
                   (!target.AllowedRegion.HasValue || target.AllowedRegion.Value.Contains(p));
        }

        private static MoveStartResult Rejected(NavigationFailure failure) =>
            new MoveStartResult(false, 0, failure);
    }
}

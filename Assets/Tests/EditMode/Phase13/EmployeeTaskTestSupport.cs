using System;
using System.Collections.Generic;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase13
{
    // 外部 Navigation / visual port；所有任务判断仍由真实 runner 完成。
    internal sealed class EmployeeTaskEnvironmentFake : IEmployeeTaskEnvironment
    {
        internal sealed class Move
        {
            internal long Id;
            internal NavigationTarget Target;
            internal Action<MovementResult> Callback;
        }
        internal readonly List<Move> Moves = new List<Move>();
        internal readonly List<long> Cancelled = new List<long>();
        internal readonly Queue<MovementStatus?> Immediate = new Queue<MovementStatus?>();
        internal string ActorId = "employee.test";
        internal bool Gate = true, ShowSucceeds = true, PlaceSucceeds = true, RejectMove;
        internal int Held, Placed, CleanupCount;
        internal SurfaceSlotAddress? PlacedAddress;
        internal EmployeeVisualMode Visual;
        internal Action OnCleanup, OnCancel, OnPlaceCup;
        public bool IsProgressAllowed => Gate;
        public MoveStartResult MoveTo(NavigationTarget target, Action<MovementResult> completed)
        {
            var move = new Move { Id = Moves.Count + 1, Target = target, Callback = completed };
            Moves.Add(move);
            if (RejectMove) return new MoveStartResult(false, 0, NavigationFailure.InvalidTarget);
            if (Immediate.Count > 0)
            {
                var status = Immediate.Dequeue();
                if (status.HasValue) Complete(Moves.Count - 1, status.Value);
            }
            return new MoveStartResult(true, move.Id, NavigationFailure.None);
        }
        internal void Complete(int index, MovementStatus status = MovementStatus.Arrived,
            long? requestId = null, string actorId = null)
        {
            var move = Moves[index];
            move.Callback(new MovementResult(requestId ?? move.Id, actorId ?? ActorId, status,
                status == MovementStatus.Arrived ? NavigationFailure.None : NavigationFailure.MovementTimeout,
                NavigationFailure.None, NavigationFailure.None, move.Target.Position, 0));
        }
        public bool CancelMove(long requestId) { Cancelled.Add(requestId); OnCancel?.Invoke(); return true; }
        public bool TryShowHeldCup(out string reason)
        { reason = ShowSucceeds ? "" : "held visual failed"; if (ShowSucceeds) Held = 1; return ShowSucceeds; }
        public bool TryPlaceCup(SurfaceSlotAddress address, out string reason)
        {
            reason = PlaceSucceeds ? "" : "place failed";
            if (PlaceSucceeds) { Held = 0; Placed++; PlacedAddress = address; }
            OnPlaceCup?.Invoke();
            return PlaceSucceeds;
        }
        public void SetVisualMode(EmployeeVisualMode mode) { Visual = mode; }
        public void CleanupTask(long handleId) { CleanupCount++; Held = 0; OnCleanup?.Invoke(); }
        internal static EmployeeStationBinding Binding(EmployeeRole role, int revision = 7) =>
            new EmployeeStationBinding(role, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1",
                role == EmployeeRole.Barista ? "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb1" : null,
                new NavigationTarget(Vector3.zero),
                role == EmployeeRole.Barista ? new NavigationTarget(new Vector3(5, 0, 0)) : (NavigationTarget?)null,
                role == EmployeeRole.Barista ? new SurfaceSlotAddress("ccccccccccccccccccccccccccccccc1", "slot.center") : (SurfaceSlotAddress?)null,
                revision);
        internal static EmployeeTaskRunner Ready(EmployeeRole role, out EmployeeTaskEnvironmentFake environment,
            float? duration = null)
        {
            environment = new EmployeeTaskEnvironmentFake();
            var runner = new EmployeeTaskRunner(environment.ActorId, role, environment, duration);
            runner.TryAssignBinding(Binding(role));
            environment.Immediate.Enqueue(MovementStatus.Arrived);
            runner.TryReturnToWork();
            return runner;
        }
    }
}


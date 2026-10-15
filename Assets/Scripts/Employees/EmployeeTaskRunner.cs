using System;
using AnimalCafe.Navigation;

namespace AnimalCafe.Employees
{
    /// <summary>单员工任务与 callback 所有权；只消费已缩放的游戏时间。</summary>
    public sealed class EmployeeTaskRunner : IDisposable
    {
        private sealed class MoveLeg
        {
            internal int Generation, LayoutRevision;
            internal long RequestId;
            internal bool Completed;
            internal EmployeeState State;
        }

        private readonly string actorId;
        private readonly EmployeeRole role;
        private readonly IEmployeeTaskEnvironment environment;
        private readonly float workDuration;
        private EmployeeStationBinding binding;
        private EmployeeState state = EmployeeState.Initializing;
        private EmployeeTaskOutcome outcome;
        private float remaining;
        private bool delivered, retiring, disposed, returnPending;
        private string failure = "";
        private long handleId, nextHandle;
        private int generation, layoutRevision = -1;
        private MoveLeg moveLeg;
        private MovementResult? pendingArrival;
        private EmployeeState? suspendedMove;
        private bool navigationResumeUsed;

        public EmployeeTaskRunner(string actorId, EmployeeRole role, IEmployeeTaskEnvironment environment,
            float? workDurationSeconds = null)
        {
            if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Employee ID 不能为空。", nameof(actorId));
            if (!Enum.IsDefined(typeof(EmployeeRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            float duration = workDurationSeconds ?? (role == EmployeeRole.Cashier ? 2f : 3f);
            if (!Finite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(workDurationSeconds));
            this.actorId = actorId;
            this.role = role;
            workDuration = duration;
        }

        public EmployeeTaskSnapshot Snapshot => new EmployeeTaskSnapshot(actorId, role, state, handleId,
            outcome, remaining, delivered, failure);

        public bool TryAssignBinding(EmployeeStationBinding value)
        {
            if (disposed || retiring || handleId != 0 || moveLeg != null || value == null ||
                value.Role != role || value.LayoutRevision < layoutRevision) return false;
            binding = value;
            layoutRevision = value.LayoutRevision;
            returnPending = false; navigationResumeUsed = false;
            // Binding 只证明目标有效；实际 Arrived 后才允许工作。
            state = EmployeeState.Initializing;
            if (outcome != EmployeeTaskOutcome.Failed) failure = "";
            return true;
        }

        public bool TryStart()
        {
            if (disposed || retiring || state != EmployeeState.Idle || handleId != 0 ||
                binding == null || !environment.IsProgressAllowed) return false;
            navigationResumeUsed = false;
            handleId = ++nextHandle;
            generation++;
            remaining = workDuration;
            outcome = EmployeeTaskOutcome.None;
            delivered = false;
            failure = "";
            state = EmployeeState.Working;
            environment.SetVisualMode(EmployeeVisualMode.Working);
            return true;
        }

        public void Tick(float scaledDeltaTime)
        {
            if (!Finite(scaledDeltaTime) || scaledDeltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(scaledDeltaTime));
            if (disposed || retiring || scaledDeltaTime == 0 || !environment.IsProgressAllowed) return;
            if (suspendedMove.HasValue)
            {
                var resumeState = suspendedMove.Value; suspendedMove = null;
                BeginMove(resumeState == EmployeeState.CarryingToPickUp ? binding.PickUpTarget.Value : binding.WorkTarget, resumeState);
                return;
            }
            if (pendingArrival.HasValue)
            {
                var result = pendingArrival.Value;
                pendingArrival = null;
                var leg = moveLeg;
                if (leg != null && IsLive(leg)) FinishMove(leg, result);
                return; // 不把到达前的帧时间算作新工作的时间。
            }
            if (returnPending) { TryReturnToWork(); return; }
            if (state != EmployeeState.Working || handleId == 0) return;
            remaining = Math.Max(0, remaining - scaledDeltaTime);
            if (remaining > 0) return;
            if (role == EmployeeRole.Cashier)
            {
                outcome = EmployeeTaskOutcome.Succeeded;
                RetireTask(EmployeeState.Idle, "", false);
                return;
            }
            long currentHandle = handleId;
            if (!environment.TryShowHeldCup(out var reason))
            {
                FailTask(reason, true);
                return;
            }
            if (handleId != currentHandle || disposed) return;
            BeginMove(binding.PickUpTarget.Value, EmployeeState.CarryingToPickUp);
        }

        public void Cancel(string reason)
        {
            if (disposed || retiring || handleId == 0) return;
            if (!delivered) outcome = EmployeeTaskOutcome.Cancelled;
            RetireTask(EmployeeState.Blocked, Reason(reason, "任务已取消 / Cancelled"), true);
        }

        public void InvalidateLayout(int revision)
        {
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            if (disposed || retiring || revision <= layoutRevision) return;
            layoutRevision = revision;
            binding = null;
            if (handleId != 0 && !delivered) outcome = EmployeeTaskOutcome.Cancelled;
            RetireTask(EmployeeState.Blocked, "布局已变化 / Layout changed", false);
        }

        internal void RejectBinding(string reason)
        {
            if (disposed || retiring) return;
            binding = null;
            RetireTask(EmployeeState.Blocked, Reason(reason, "工作站不可用 / Station unavailable"), false);
        }

        public void TryReturnToWork()
        {
            if (disposed || retiring || handleId != 0 || moveLeg != null) return;
            if (binding == null)
            {
                state = EmployeeState.Blocked;
                failure = "缺少工作站绑定 / Missing station binding";
                return;
            }
            if (!environment.IsProgressAllowed) { returnPending = true; return; }
            returnPending = false;
            BeginMove(binding.WorkTarget, EmployeeState.MovingToWork);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (handleId != 0 && !delivered) outcome = EmployeeTaskOutcome.Cancelled;
            RetireTask(EmployeeState.Disabled, "员工已停用 / Disabled", false);
        }

        private void BeginMove(NavigationTarget target, EmployeeState nextState)
        {
            // 必须先建立 leg；MoveTo 允许在返回 request ID 前同步完成。
            var leg = new MoveLeg { Generation = generation, LayoutRevision = layoutRevision, State = nextState };
            moveLeg = leg;
            state = nextState;
            environment.SetVisualMode(nextState == EmployeeState.CarryingToPickUp
                ? EmployeeVisualMode.Carrying : EmployeeVisualMode.Walking);
            if (!IsLive(leg)) return;
            var start = environment.MoveTo(target, result => OnMoveCompleted(leg, result));
            if (!IsLive(leg))
            {
                // 若外部在 MoveTo 内取消、尚未给出 ID，返回后仍停止这条 owned request。
                if (!leg.Completed && start.Accepted) environment.CancelMove(start.RequestId);
                return;
            }
            if (!start.Accepted)
            {
                moveLeg = null;
                OnMoveFailed(leg.State, start.Reason.ToString());
                return;
            }
            leg.RequestId = start.RequestId;
        }

        private void OnMoveCompleted(MoveLeg leg, MovementResult result)
        {
            if (!IsLive(leg) || leg.Completed || result.ActorId != actorId ||
                (leg.RequestId != 0 && result.RequestId != leg.RequestId)) return;
            leg.RequestId = result.RequestId;
            leg.Completed = true; // 重复 callback 不能重复交付，也不能替换已缓存的 Arrived。
            if (result.Status == MovementStatus.Arrived && !environment.IsProgressAllowed)
            {
                pendingArrival = result;
                return;
            }
            if (result.Status == MovementStatus.LayoutChanged && !environment.IsProgressAllowed)
            {
                // A same-revision NavMesh suspension is not a player Confirm. Resume once only.
                generation++; moveLeg = null; pendingArrival = null;
                if (navigationResumeUsed)
                    FailTask("导航重复失效，请修复后重试 / Navigation repeatedly unavailable", false);
                else { navigationResumeUsed = true; suspendedMove = leg.State; }
                return;
            }
            FinishMove(leg, result);
        }

        private void FinishMove(MoveLeg leg, MovementResult result)
        {
            if (!IsLive(leg)) return;
            moveLeg = null;
            if (result.Status != MovementStatus.Arrived)
            {
                OnMoveFailed(leg.State, result.Status + ": " + result.Reason);
                return;
            }
            if (leg.State == EmployeeState.CarryingToPickUp)
            {
                var placementBinding = binding;
                long placementHandle = handleId;
                bool placed = environment.TryPlaceCup(placementBinding.CupAddress.Value, out var reason);
                // Visual port 可重入取消、布局变更或停用；旧结果不能覆盖退休后的状态。
                if (disposed || retiring || handleId == 0 || handleId != placementHandle ||
                    generation != leg.Generation || layoutRevision != leg.LayoutRevision ||
                    !ReferenceEquals(binding, placementBinding) || state != EmployeeState.CarryingToPickUp) return;
                if (!placed)
                {
                    FailTask(reason, true);
                    return;
                }
                delivered = true;
                outcome = EmployeeTaskOutcome.Succeeded;
                BeginMove(placementBinding.WorkTarget, EmployeeState.Returning);
            }
            else
            {
                RetireTask(EmployeeState.Idle, "", false);
            }
        }

        private void OnMoveFailed(EmployeeState failedState, string reason)
        {
            // 回位失败是明确 Blocked；不在每帧或失败 callback 中无限重试。
            FailTask(reason, failedState == EmployeeState.CarryingToPickUp);
        }

        private void FailTask(string reason, bool tryReturn)
        {
            if (handleId != 0 && !delivered) outcome = EmployeeTaskOutcome.Failed;
            RetireTask(EmployeeState.Blocked, Reason(reason, "任务失败 / Task failed"), tryReturn);
        }

        private void RetireTask(EmployeeState finalState, string reason, bool tryReturn)
        {
            var oldLeg = moveLeg;
            long oldHandle = handleId;
            // 先退休上下文，再调用外部 cancel / cleanup；重入不能复活旧任务。
            generation++;
            moveLeg = null;
            pendingArrival = null;
            suspendedMove = null;
            returnPending = false;
            handleId = 0;
            remaining = 0;
            state = finalState;
            failure = outcome == EmployeeTaskOutcome.Failed && string.IsNullOrEmpty(reason) ? failure : reason;
            retiring = true;
            try
            {
                if (oldLeg != null && !oldLeg.Completed && oldLeg.RequestId != 0)
                    environment.CancelMove(oldLeg.RequestId);
                if (oldHandle != 0) environment.CleanupTask(oldHandle);
                environment.SetVisualMode(EmployeeVisualMode.Idle);
            }
            finally { retiring = false; }
            if (tryReturn && !disposed && binding != null) TryReturnToWork();
        }

        private bool IsLive(MoveLeg leg) => !disposed && !retiring && ReferenceEquals(moveLeg, leg) &&
            leg.Generation == generation && leg.LayoutRevision == layoutRevision;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static string Reason(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}


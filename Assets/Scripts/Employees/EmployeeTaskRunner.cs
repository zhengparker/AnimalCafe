using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Navigation;
using AnimalCafe.CafeLoop;

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
        private bool integrated, hasCheckpoint;
        private CafeTaskToken taskToken;
        private CafeWorkCheckpoint checkpoint;
        private CafeTaskEvent? deliveryArrival;
        private NavigationTarget? selectedDeliveryTarget;
        private NavigationTarget[] deliveryRoute;
        private int deliveryRouteIndex;
        private NavigationTarget[] returnRoute,pendingReturnRoute;
        private int returnRouteIndex;
        private Func<NavigationTarget,bool> pickupReturnTrafficWait;
        private NavigationTarget? pendingReturnTraffic;
        internal void SetPickupReturnTrafficWait(Func<NavigationTarget,bool> query) => pickupReturnTrafficWait=query;
        public bool HasSelectedDeliveryRoute => integrated && handleId!=0 && deliveryRoute!=null;
        internal bool HasSelectedReturnRoute => integrated && handleId!=0 && returnRoute!=null;
        internal IReadOnlyList<NavigationTarget> RemainingPickupReturnRoute => !HasSelectedReturnRoute?null:
            state==EmployeeState.Returning?returnRoute.Skip(returnRouteIndex).ToArray():
            state==EmployeeState.CarryingToPickUp || state==EmployeeState.WaitingToDeliver
                ?new[]{DeliveryTarget.Value}.Concat(returnRoute).ToArray():null;
        public NavigationTarget? DeliveryTarget => selectedDeliveryTarget ?? binding?.PickUpTarget;
        internal bool TrySelectPickupRoutes(NavigationTarget target,IReadOnlyList<NavigationTarget> outward,IReadOnlyList<NavigationTarget> home)
        {
            if(!ValidReturnRoute(home) || outward==null || outward.Count+home.Count>24) return false;
            if(!TrySelectDeliveryTarget(target,outward)) return false;
            if(returnRoute==null) { returnRoute=home.ToArray();returnRouteIndex=0; }
            return true;
        }
        internal bool ConfigurePickupReturnRoute(IReadOnlyList<NavigationTarget> home)
        {
            if(disposed || retiring || handleId!=0 || !ValidReturnRoute(home)) return false;
            pendingReturnRoute=home.ToArray();return true;
        }
        private bool ValidReturnRoute(IReadOnlyList<NavigationTarget> home) => binding!=null && role==EmployeeRole.Barista &&
            home!=null && home.Count>0 && home.Count<=24 && home[home.Count-1].Position==binding.WorkTarget.Position &&
            home[home.Count-1].Facing==binding.WorkTarget.Facing;
        private NavigationTarget CurrentReturnSegment => returnRoute!=null?returnRoute[returnRouteIndex]:binding.WorkTarget;
        public bool TrySelectDeliveryTarget(NavigationTarget target) => TrySelectDeliveryTarget(target,new[]{target});
        public bool TrySelectDeliveryTarget(NavigationTarget target,IReadOnlyList<NavigationTarget> route)
        {
            if(!integrated || disposed || retiring || handleId==0 || moveLeg!=null ||
                state!=EmployeeState.WaitingToDeliver || checkpoint.Stage!=CafeWorkStage.Prepared) return false;
            if(selectedDeliveryTarget.HasValue)
                return selectedDeliveryTarget.Value.Position==target.Position && selectedDeliveryTarget.Value.Facing==target.Facing;
            if(route==null || route.Count==0 || route.Count>24 || route[route.Count-1].Position!=target.Position ||
                route[route.Count-1].Facing!=target.Facing) return false;
            selectedDeliveryTarget=target; deliveryRoute=route.ToArray(); deliveryRouteIndex=0; return true;
        }

        public EmployeeTaskRunner(string actorId, EmployeeRole role, IEmployeeTaskEnvironment environment,
            float? workDurationSeconds = null, long previousHandleId = 0)
        {
            if (string.IsNullOrWhiteSpace(actorId)) throw new ArgumentException("Employee ID 不能为空。", nameof(actorId));
            if (!Enum.IsDefined(typeof(EmployeeRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
            float duration = workDurationSeconds ?? (role == EmployeeRole.Cashier ? 2f : 3f);
            if (!Finite(duration) || duration <= 0) throw new ArgumentOutOfRangeException(nameof(workDurationSeconds));
            if (previousHandleId < 0) throw new ArgumentOutOfRangeException(nameof(previousHandleId));
            this.actorId = actorId;
            this.role = role;
            workDuration = duration;
            nextHandle = previousHandleId;
        }

        public EmployeeTaskSnapshot Snapshot => new EmployeeTaskSnapshot(actorId, role, state, handleId,
            outcome, remaining, delivered, failure);

        public event Action<CafeTaskEvent> IntegratedTaskEvent;
        public CafeWorkCheckpoint CaptureCheckpoint() => hasCheckpoint ? checkpoint : default;
        public bool IsCurrentDeliveryArrival(CafeTaskEvent arrival) => deliveryArrival.HasValue &&
            !disposed && !retiring && environment.IsProgressAllowed && handleId == arrival.HandleId &&
            generation == arrival.Generation && layoutRevision == arrival.LayoutRevision &&
            deliveryArrival.Value.Equals(arrival);

        public bool TryStartIntegrated(CafeTaskToken token, CafeWorkCheckpoint saved)
        {
            if (disposed || retiring || handleId != 0 || moveLeg != null || binding == null ||
                !environment.IsProgressAllowed || !(environment is IIntegratedEmployeeTaskEnvironment) ||
                !ValidContext(token, saved)) return false;
            integrated = true; hasCheckpoint = true; taskToken = token; checkpoint = saved; selectedDeliveryTarget=null; deliveryRoute=null; deliveryRouteIndex=0;
            returnRoute=saved.Stage==CafeWorkStage.Returning || saved.Stage==CafeWorkStage.Delivered?pendingReturnRoute:null;
            pendingReturnRoute=null;returnRouteIndex=0;
            handleId = ++nextHandle; generation++; navigationResumeUsed = false;
            outcome = EmployeeTaskOutcome.None; failure = ""; remaining = saved.RemainingWorkSeconds;
            delivered = saved.Stage == CafeWorkStage.Delivered || saved.Stage == CafeWorkStage.Returning ||
                (role == EmployeeRole.Barista && saved.Stage == CafeWorkStage.Finished);
            state = saved.Stage == CafeWorkStage.Working ? EmployeeState.Working :
                saved.Stage == CafeWorkStage.Prepared ? EmployeeState.WaitingToDeliver : EmployeeState.Returning;
            long acceptedHandle = handleId;
            Publish(CafeTaskEventKind.Progress);
            if (!LiveTask(acceptedHandle)) return true;
            if (saved.Stage == CafeWorkStage.Finished)
            {
                FinishIntegrated(role == EmployeeRole.Cashier ? CafeTaskEventKind.ServiceFinished : CafeTaskEventKind.Returned);
            }
            else if (saved.Stage == CafeWorkStage.Prepared) StartPreparedDelivery();
            else if (saved.Stage == CafeWorkStage.Delivered || saved.Stage == CafeWorkStage.Returning)
            { SetStage(CafeWorkStage.Returning); BeginMove(CurrentReturnSegment, EmployeeState.Returning); }
            else environment.SetVisualMode(EmployeeVisualMode.Working);
            return true; // Accepted can synchronously finish or become Blocked; its event owns the result.
        }

        private bool ValidContext(CafeTaskToken token, CafeWorkCheckpoint saved)
        {
            if (string.IsNullOrWhiteSpace(token.SessionId) || token.SessionId != token.SessionId.Trim() ||
                token.Epoch <= 0 || token.Sequence <= 0 || token.Attempt <= 0 || saved.Role != role ||
                string.IsNullOrWhiteSpace(saved.VisitId) || saved.VisitId != saved.VisitId.Trim() ||
                !Enum.IsDefined(typeof(CafeWorkStage), saved.Stage) || !Finite(saved.RemainingWorkSeconds) ||
                saved.RemainingWorkSeconds < 0 || saved.RemainingWorkSeconds > workDuration ||
                (saved.Stage != CafeWorkStage.Working && saved.RemainingWorkSeconds != 0)) return false;
            if (role == EmployeeRole.Cashier)
            {
                if (saved.Order.HasValue || (saved.Stage != CafeWorkStage.Working && saved.Stage != CafeWorkStage.Finished)) return false;
            }
            else if (!saved.Order.HasValue || saved.Order.Value.OrderId <= 0 ||
                saved.Order.Value.SessionId != token.SessionId || saved.Order.Value.Epoch != token.Epoch) return false;
            if (!hasCheckpoint) return true;
            if (token.SessionId != taskToken.SessionId || token.Epoch < taskToken.Epoch) return false;
            if (token.Epoch > taskToken.Epoch) return true;
            if (token.Sequence < taskToken.Sequence) return false;
            if (token.Sequence > taskToken.Sequence) return checkpoint.Stage == CafeWorkStage.Finished;
            return token.Attempt > taskToken.Attempt && saved.VisitId == checkpoint.VisitId &&
                Nullable.Equals(saved.Order, checkpoint.Order) && (int)saved.Stage >= (int)checkpoint.Stage &&
                (saved.Stage != CafeWorkStage.Working || saved.RemainingWorkSeconds <= checkpoint.RemainingWorkSeconds);
        }

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
            if (integrated || disposed || retiring || state != EmployeeState.Idle || handleId != 0 ||
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
                BeginMove(resumeState == EmployeeState.CarryingToPickUp ? CurrentDeliverySegment : CurrentReturnSegment, resumeState);
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
            if(pendingReturnTraffic.HasValue)
            {var target=pendingReturnTraffic.Value;pendingReturnTraffic=null;BeginMove(target,EmployeeState.Returning);return;}
            if (integrated && state == EmployeeState.WaitingToDeliver && handleId != 0)
            { TryBeginDelivery(); return; }
            if (state != EmployeeState.Working || handleId == 0) return;
            remaining = Math.Max(0, remaining - scaledDeltaTime);
            if (integrated) { SetStage(CafeWorkStage.Working, remaining); Publish(CafeTaskEventKind.Progress); }
            if (disposed || retiring || handleId == 0 || state != EmployeeState.Working) return;
            if (remaining > 0) return;
            if (role == EmployeeRole.Cashier)
            {
                if (integrated) { SetStage(CafeWorkStage.Finished); FinishIntegrated(CafeTaskEventKind.ServiceFinished); return; }
                outcome = EmployeeTaskOutcome.Succeeded;
                RetireTask(EmployeeState.Idle, "", false);
                return;
            }
            if (integrated)
            {
                SetStage(CafeWorkStage.Prepared);
                long preparedHandle = handleId;
                Publish(CafeTaskEventKind.Prepared); // 已制作先成为事实，再创建可失败的视觉。
                if (LiveTask(preparedHandle)) StartPreparedDelivery();
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
            if (integrated && hasCheckpoint && checkpoint.Stage != CafeWorkStage.Finished) return;
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
            if(integrated && HasSelectedReturnRoute && nextState==EmployeeState.Returning && pickupReturnTrafficWait!=null)
            {
                var currentHandle=handleId;var currentGeneration=generation;var currentRevision=layoutRevision;
                var wait=pickupReturnTrafficWait(target);
                if(!LiveTask(currentHandle) || generation!=currentGeneration || layoutRevision!=currentRevision) return;
                if(wait)
                {state=EmployeeState.Returning;pendingReturnTraffic=target;return;}
            }
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
                if (integrated)
                {
                    if(deliveryRoute!=null && deliveryRouteIndex+1<deliveryRoute.Length)
                    {
                        // Intermediate native arrival is navigation only / 中间段不能交付或放杯。
                        deliveryRouteIndex++; BeginMove(CurrentDeliverySegment,EmployeeState.CarryingToPickUp); return;
                    }
                    FinishIntegratedDelivery(leg); return;
                }
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
                if(integrated && returnRoute!=null && returnRouteIndex+1<returnRoute.Length)
                { returnRouteIndex++;BeginMove(CurrentReturnSegment,EmployeeState.Returning);return; }
                if (integrated && handleId != 0)
                { SetStage(CafeWorkStage.Finished); FinishIntegrated(CafeTaskEventKind.Returned, leg.RequestId); return; }
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
            CafeTaskEvent? blocked = integrated && oldHandle != 0 &&
                (finalState == EmployeeState.Blocked || finalState == EmployeeState.Disabled)
                ? Event(CafeTaskEventKind.Blocked, oldLeg?.RequestId ?? 0, reason) : (CafeTaskEvent?)null;
            // 先退休上下文，再调用外部 cancel / cleanup；重入不能复活旧任务。
            generation++;
            moveLeg = null;
            pendingArrival = null;
            suspendedMove = null;
            pendingReturnTraffic=null;
            deliveryArrival = null;
            deliveryRoute=null; deliveryRouteIndex=0;
            returnRoute=pendingReturnRoute=null;returnRouteIndex=0;
            returnPending = false;
            handleId = 0;
            remaining = 0;
            state = finalState;
            failure = outcome == EmployeeTaskOutcome.Failed && string.IsNullOrEmpty(reason) ? failure : reason;
            retiring = true;
            try
            {
                // 身份已退休，但业务checkpoint还在；禁用/cleanup前允许同步保存进度。
                if (blocked.HasValue) IntegratedTaskEvent?.Invoke(blocked.Value);
                if (oldLeg != null && !oldLeg.Completed && oldLeg.RequestId != 0)
                    environment.CancelMove(oldLeg.RequestId);
                if (oldHandle != 0) environment.CleanupTask(oldHandle);
                environment.SetVisualMode(EmployeeVisualMode.Idle);
            }
            finally { retiring = false; }
            if (tryReturn && !integrated && !disposed && binding != null) TryReturnToWork();
        }

        private void StartPreparedDelivery()
        {
            long currentHandle = handleId;
            if (!environment.TryShowHeldCup(out var reason)) { FailTask(reason, false); return; }
            if (!LiveTask(currentHandle)) return;
            state = EmployeeState.WaitingToDeliver;
            environment.SetVisualMode(EmployeeVisualMode.Carrying);
            if (LiveTask(currentHandle)) TryBeginDelivery();
        }

        private NavigationTarget CurrentDeliverySegment => deliveryRoute!=null?deliveryRoute[deliveryRouteIndex]:DeliveryTarget.Value;

        private void TryBeginDelivery()
        {
            if (!environment.IsProgressAllowed || !(environment is IIntegratedEmployeeTaskEnvironment port)) return;
            long currentHandle = handleId;
            bool acquired = port.TryAcquireDelivery(actorId);
            if (acquired && LiveTask(currentHandle)) BeginMove(CurrentDeliverySegment, EmployeeState.CarryingToPickUp);
        }

        private void FinishIntegratedDelivery(MoveLeg leg)
        {
            var placementBinding = binding;
            var arrival = Event(CafeTaskEventKind.DeliveryArrived, leg.RequestId);
            deliveryArrival = arrival;
            bool committed = false; string reason = "业务交付入口缺失 / Delivery commit unavailable";
            try
            {
                IntegratedTaskEvent?.Invoke(arrival);
                if (!IsCurrentDeliveryArrival(arrival)) return;
                committed = ((IIntegratedEmployeeTaskEnvironment)environment).TryCommitDelivery(arrival,
                    placementBinding.CupAddress.Value, out reason);
            }
            finally { deliveryArrival = null; }
            // Commit可能已经成功并重入disable/Done；旧runner不能覆盖新生命周期。
            if (!LiveTask(arrival.HandleId) || generation != arrival.Generation ||
                layoutRevision != arrival.LayoutRevision || !ReferenceEquals(binding, placementBinding)) return;
            if (!committed) { FailTask(reason, false); return; }
            delivered = true; outcome = EmployeeTaskOutcome.Succeeded; SetStage(CafeWorkStage.Delivered);
            Publish(CafeTaskEventKind.Progress, leg.RequestId);
            if (!LiveTask(arrival.HandleId)) return;
            try { ((IIntegratedEmployeeTaskEnvironment)environment).HideHeldCup(); }
            catch (Exception error) { FailTask("交付已完成，手杯视觉清理失败 / " + error.Message, false); return; }
            if (!LiveTask(arrival.HandleId)) return;
            SetStage(CafeWorkStage.Returning); Publish(CafeTaskEventKind.Progress, leg.RequestId);
            if (LiveTask(arrival.HandleId)) BeginMove(CurrentReturnSegment, EmployeeState.Returning);
        }

        private void FinishIntegrated(CafeTaskEventKind kind, long requestId = 0)
        {
            var completed = Event(kind, requestId);
            outcome = EmployeeTaskOutcome.Succeeded;
            RetireTask(EmployeeState.Idle, "", false);
            IntegratedTaskEvent?.Invoke(completed);
        }
        private void SetStage(CafeWorkStage stage, float seconds = 0)
            => checkpoint = new CafeWorkCheckpoint(role, checkpoint.VisitId, checkpoint.Order, stage, seconds);
        private CafeTaskEvent Event(CafeTaskEventKind kind, long requestId = 0, string reason = "")
            => new CafeTaskEvent(taskToken, role, actorId, handleId, requestId, generation, layoutRevision,
                kind, checkpoint, reason);
        private void Publish(CafeTaskEventKind kind, long requestId = 0)
            => IntegratedTaskEvent?.Invoke(Event(kind, requestId));
        private bool LiveTask(long expectedHandle) => !disposed && !retiring && handleId != 0 && handleId == expectedHandle;

        private bool IsLive(MoveLeg leg) => !disposed && !retiring && ReferenceEquals(moveLeg, leg) &&
            leg.Generation == generation && leg.LayoutRevision == layoutRevision;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static string Reason(string value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}


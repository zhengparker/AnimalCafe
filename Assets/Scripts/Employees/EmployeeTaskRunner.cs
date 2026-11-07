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
            internal int NativeLegGeneration;
            internal NavigationTarget Target;
            internal bool AwaitingTraffic, TrafficIntermediate;
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
        private int nextNativeLegGeneration;
        // Identity of the actual unissued leg; pause/navigation availability stays with its consumer.
        // 只封存真实 MoveLeg；没有 checkpoint 的初始返岗不读取默认 task 字段。
        internal sealed class PreparedMovementStamp
        {
            private readonly EmployeeTaskRunner owner;
            private readonly MoveLeg leg;
            private readonly IEmployeeTaskEnvironment environment;
            private readonly EmployeeStationBinding binding;
            private readonly bool hasCheckpoint;
            private readonly bool integrated;
            private readonly CafeTaskToken token;
            private readonly CafeWorkCheckpoint checkpoint;
            private readonly CafeTaskToken? acceptedHistory;
            private readonly CafeWorkCheckpoint? savedHistory;
            private readonly int generation,revision,nativeGeneration;
            private readonly long handle,selection;
            private readonly EmployeeState state;
            private readonly NavigationTarget target;
            private PreparedMovementStamp(EmployeeTaskRunner owner,CafeTaskToken? accepted,CafeWorkCheckpoint? saved)
            {
                this.owner=owner;leg=owner.moveLeg;environment=owner.environment;binding=owner.binding;
                hasCheckpoint=owner.hasCheckpoint;integrated=owner.integrated;
                if(hasCheckpoint){token=owner.taskToken;checkpoint=owner.checkpoint;}
                else{acceptedHistory=accepted;savedHistory=saved;}
                generation=owner.generation;revision=owner.layoutRevision;nativeGeneration=leg.NativeLegGeneration;
                handle=owner.handleId;selection=owner.trafficSelectionVersion;state=owner.state;target=leg.Target;
            }
            internal NavigationTarget Target=>target;
            internal int NativeGeneration=>nativeGeneration;
            internal static bool TryCapture(EmployeeTaskRunner owner,CafeTaskToken? accepted,CafeWorkCheckpoint? saved,out PreparedMovementStamp stamp)
            {
                stamp=null;if(owner==null || !owner.UnissuedMovementAvailable)return false;
                // Retained Finished history is not a current task; seal its exact legitimate pair.
                // Finished 历史不冒充当前 checkpoint；只封存真实且匹配的 token/checkpoint。
                if(!owner.hasCheckpoint && (accepted.HasValue || saved.HasValue) &&
                    (!accepted.HasValue || !saved.HasValue || saved.Value.Stage!=CafeWorkStage.Finished ||
                        !owner.ValidContext(accepted.Value,saved.Value)))return false;
                stamp=new PreparedMovementStamp(owner,accepted,saved);return true;
            }
            internal bool BelongsTo(EmployeeTaskRunner runner,EmployeeStationBinding actualBinding,IEmployeeTaskEnvironment actualPort,
                EmployeeRole actualRole,CafeTaskToken? accepted,CafeWorkCheckpoint? saved)
                =>ReferenceEquals(owner,runner) && ReferenceEquals(binding,actualBinding) && ReferenceEquals(environment,actualPort) &&
                    owner.role==actualRole && binding.Role==actualRole && (hasCheckpoint
                        ?accepted.HasValue && saved.HasValue && token.Equals(accepted.Value) && checkpoint.Equals(saved.Value)
                        :Nullable.Equals(acceptedHistory,accepted) && Nullable.Equals(savedHistory,saved));
            internal bool IsCurrent
            {
                get
                {
                    try
                    {
                        return owner.UnissuedMovementAvailable && ReferenceEquals(owner.moveLeg,leg) &&
                            ReferenceEquals(owner.environment,environment) && ReferenceEquals(owner.binding,binding) &&
                            owner.hasCheckpoint==hasCheckpoint && owner.integrated==integrated && owner.generation==generation &&
                            owner.layoutRevision==revision && owner.handleId==handle && owner.trafficSelectionVersion==selection &&
                            owner.state==state && leg.NativeLegGeneration==nativeGeneration && leg.Target.Equals(target) &&
                            (!hasCheckpoint || owner.taskToken.Equals(token) && owner.checkpoint.Equals(checkpoint));
                    }
                    catch(Exception){return false;}
                }
            }
        }
        private bool UnissuedMovementAvailable=>binding!=null && binding.Role==role && binding.LayoutRevision==layoutRevision &&
            moveLeg!=null && IsLive(moveLeg) && moveLeg.State==state && moveLeg.AwaitingTraffic && !moveLeg.Completed &&
            moveLeg.RequestId==0 && moveLeg.NativeLegGeneration>0 && !suspendedMove.HasValue && !pendingArrival.HasValue &&
            !HasSelectedDeliveryRoute && !HasSelectedReturnRoute &&
            (hasCheckpoint ? integrated && checkpoint.Role==role : handleId==0 && state==EmployeeState.MovingToWork);
        internal bool TryCapturePreparedMovementStamp(CafeTaskToken? accepted,CafeWorkCheckpoint? saved,out PreparedMovementStamp stamp)
        {
            stamp=null;
            try{return PreparedMovementStamp.TryCapture(this,accepted,saved,out stamp);}
            catch(Exception){stamp=null;return false;}
        }

        internal int CurrentNativeLegGeneration => moveLeg?.NativeLegGeneration ?? 0;
        internal int CurrentTrafficRouteIndex => state==EmployeeState.Returning ? returnRouteIndex : deliveryRouteIndex;
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
        private long trafficSelectionVersion;
        // Sealed read-only provenance of this actual Prepared task, before route selection.
        // 只绑定真实 task 身份；不准备路线、不改 checkpoint、不产生移动权限。
        internal sealed class PreparedPickupStamp
        {
            private readonly EmployeeTaskRunner owner;
            private readonly EmployeeStationBinding binding;
            private readonly CafeTaskToken token;
            private readonly CafeWorkCheckpoint checkpoint;
            private readonly long handle,selection;
            private readonly int generation,revision;
            private readonly NavigationTarget work;
            private PreparedPickupStamp(EmployeeTaskRunner owner)
            {
                this.owner=owner;binding=owner.binding;token=owner.taskToken;checkpoint=owner.checkpoint;
                handle=owner.handleId;selection=owner.trafficSelectionVersion;generation=owner.generation;revision=owner.layoutRevision;
                work=binding.WorkTarget;Context=owner.PickupTrafficContext;
            }
            internal string Context { get; }
            internal static bool TryCapture(EmployeeTaskRunner owner,out PreparedPickupStamp stamp)
            { stamp=null;if(owner==null || !owner.PreparedPickupAvailable)return false;stamp=new PreparedPickupStamp(owner);return true; }
            internal bool BelongsTo(EmployeeTaskRunner runner,EmployeeStationBinding actualBinding,CafeTaskToken accepted,CafeWorkCheckpoint saved)
                =>ReferenceEquals(owner,runner) && ReferenceEquals(binding,actualBinding) && token.Equals(accepted) && checkpoint.Equals(saved);
            internal bool IsCurrent
            {
                get
                {
                    try
                    {
                        return owner.PreparedPickupAvailable && ReferenceEquals(owner.binding,binding) &&
                            owner.taskToken.Equals(token) && owner.checkpoint.Equals(checkpoint) && owner.handleId==handle &&
                            owner.generation==generation && owner.layoutRevision==revision && owner.trafficSelectionVersion==selection &&
                            binding.WorkTarget.Position==work.Position && binding.WorkTarget.Facing==work.Facing && owner.PickupTrafficContext==Context;
                    }
                    catch(Exception) { return false; }
                }
            }
        }
        private bool PreparedPickupAvailable => integrated && hasCheckpoint && !disposed && !retiring &&
            role==EmployeeRole.Barista && handleId>0 && binding!=null && binding.LayoutRevision==layoutRevision &&
            environment.IsProgressAllowed && state==EmployeeState.WaitingToDeliver && checkpoint.Stage==CafeWorkStage.Prepared &&
            // Validate the current accepted task; ValidContext admits a later retry attempt.
            // 当前 seal 校验真实身份；下一次 attempt 的 admission guard 保持原样。
            !string.IsNullOrWhiteSpace(taskToken.SessionId) && taskToken.SessionId==taskToken.SessionId.Trim() &&
            taskToken.Epoch>0 && taskToken.Sequence>0 && taskToken.Attempt>0 && checkpoint.Role==role &&
            !string.IsNullOrWhiteSpace(checkpoint.VisitId) && checkpoint.VisitId==checkpoint.VisitId.Trim() &&
            Finite(checkpoint.RemainingWorkSeconds) && checkpoint.RemainingWorkSeconds==0 && checkpoint.RemainingWorkSeconds<=workDuration &&
            checkpoint.Order.HasValue && checkpoint.Order.Value.OrderId>0 &&
            checkpoint.Order.Value.SessionId==taskToken.SessionId && checkpoint.Order.Value.Epoch==taskToken.Epoch &&
            moveLeg==null && !suspendedMove.HasValue && !pendingArrival.HasValue &&
            !selectedDeliveryTarget.HasValue && deliveryRoute==null && returnRoute==null;
        internal bool TryCapturePreparedPickupStamp(out PreparedPickupStamp stamp)
        {
            stamp=null;
            try { return PreparedPickupStamp.TryCapture(this,out stamp); }
            catch(Exception) { stamp=null;return false; }
        }

        internal string PickupTrafficContext => taskToken.SessionId+"/"+taskToken.Epoch+"/"+taskToken.Sequence+"/"+taskToken.Attempt+"/handle/"+handleId;
        internal bool HasIndependentNativeTraffic => HasCurrentPickupRoute && moveLeg!=null && !moveLeg.Completed && moveLeg.RequestId!=0;
        internal bool HasLivePickupJourney => HasCurrentPickupRoute && returnRoute!=null &&
            (state==EmployeeState.CarryingToPickUp || state==EmployeeState.WaitingToDeliver || state==EmployeeState.Returning);
        internal bool TryPreparePickupRoutes(NavigationTarget target,IReadOnlyList<NavigationTarget> outward,IReadOnlyList<NavigationTarget> home,
            out IPreparedTrafficMutation prepared)
        {
            prepared=null;
            if(!integrated || disposed || retiring || handleId==0 || moveLeg!=null || state!=EmployeeState.WaitingToDeliver ||
                checkpoint.Stage!=CafeWorkStage.Prepared || !ValidReturnRoute(home) || outward==null || outward.Count==0 ||
                outward.Count+home.Count>24 || outward[outward.Count-1].Position!=target.Position || outward[outward.Count-1].Facing!=target.Facing) return false;
            prepared=new PreparedPickupRoutes(this,target,outward.ToArray(),home.ToArray());return true;
        }
        private sealed class PreparedPickupRoutes : IPreparedTrafficMutation
        {
            private readonly EmployeeTaskRunner owner;
            private readonly NavigationTarget target;
            private readonly NavigationTarget[] outward,home;
            private readonly string context;
            private readonly long version;
            private readonly int revision,generation;
            private bool consumed;
            internal PreparedPickupRoutes(EmployeeTaskRunner owner,NavigationTarget target,NavigationTarget[] outward,NavigationTarget[] home)
            {this.owner=owner;this.target=target;this.outward=outward;this.home=home;context=owner.PickupTrafficContext;
                version=owner.trafficSelectionVersion;revision=owner.layoutRevision;generation=owner.generation;}
            public bool IsCurrent => !consumed && !owner.disposed && !owner.retiring && owner.PickupTrafficContext==context &&
                owner.trafficSelectionVersion==version && owner.layoutRevision==revision && owner.generation==generation &&
                owner.state==EmployeeState.WaitingToDeliver && owner.moveLeg==null;
            public bool TryApply(out ITrafficMutationReceipt receipt)
            {
                receipt=null;if(!IsCurrent)return false;consumed=true;
                var undo=new PickupRouteReceipt(owner);
                if(!owner.TrySelectPickupRoutes(target,outward,home))return false;
                undo.Applied();receipt=undo;return true;
            }
        }
        private sealed class PickupRouteReceipt : ITrafficMutationReceipt
        {
            private readonly EmployeeTaskRunner owner;
            private readonly string context;
            private readonly NavigationTarget? target;
            private readonly NavigationTarget[] outward,home;
            private readonly int outwardIndex,homeIndex;
            private long version;
            private bool retired;
            internal PickupRouteReceipt(EmployeeTaskRunner owner)
            {this.owner=owner;context=owner.PickupTrafficContext;target=owner.selectedDeliveryTarget;outward=owner.deliveryRoute;home=owner.returnRoute;
                outwardIndex=owner.deliveryRouteIndex;homeIndex=owner.returnRouteIndex;}
            internal void Applied(){version=owner.trafficSelectionVersion;}
            public bool IsCurrent => !retired && owner.PickupTrafficContext==context && owner.trafficSelectionVersion==version && owner.moveLeg==null;
            public void RollbackOwnMutation()
            {
                if(retired)return;if(!IsCurrent)throw new InvalidOperationException("Employee route selection is no longer owned.");
                retired=true;owner.selectedDeliveryTarget=target;owner.deliveryRoute=outward;owner.returnRoute=home;
                owner.deliveryRouteIndex=outwardIndex;owner.returnRouteIndex=homeIndex;owner.trafficSelectionVersion++;
            }
        }
        private Func<NavigationTarget,bool> pickupReturnTrafficWait;
        private NavigationTarget? pendingReturnTraffic;
        internal void SetPickupReturnTrafficWait(Func<NavigationTarget,bool> query) => pickupReturnTrafficWait=query;
        private Func<NavigationTarget,bool> pickupDeliveryTrafficWait;
        private NavigationTarget? pendingDeliveryTraffic;
        internal void SetPickupDeliveryTrafficWait(Func<NavigationTarget,bool> query) => pickupDeliveryTrafficWait=query;
        public bool HasSelectedDeliveryRoute => integrated && handleId!=0 && deliveryRoute!=null;
        internal bool HasSelectedReturnRoute => integrated && handleId!=0 && returnRoute!=null;
        // Read stored indices only: querying traffic must never advance a leg.
        // 只读当前task的真实suffix；取消、布局失效和旧generation均不再提供路线。
        private bool HasCurrentPickupRoute => integrated && !disposed && !retiring && handleId!=0 &&
            binding!=null && binding.LayoutRevision==layoutRevision && !suspendedMove.HasValue &&
            (moveLeg==null || moveLeg.Generation==generation && moveLeg.LayoutRevision==layoutRevision);
        internal IReadOnlyList<NavigationTarget> RemainingPickupDeliveryRoute => HasCurrentPickupRoute &&
            HasSelectedDeliveryRoute && (state==EmployeeState.CarryingToPickUp || state==EmployeeState.WaitingToDeliver) &&
            checkpoint.Stage==CafeWorkStage.Prepared?deliveryRoute.Skip(deliveryRouteIndex).ToArray():null;
        internal IReadOnlyList<NavigationTarget> RemainingPickupReturnRoute => !HasCurrentPickupRoute || !HasSelectedReturnRoute?null:
            state==EmployeeState.Returning?returnRoute.Skip(returnRouteIndex).ToArray():
            state==EmployeeState.CarryingToPickUp || state==EmployeeState.WaitingToDeliver
                ?new[]{DeliveryTarget.Value}.Concat(returnRoute).ToArray():null;
        public NavigationTarget? DeliveryTarget => selectedDeliveryTarget ?? binding?.PickUpTarget;
        internal bool TrySelectPickupRoutes(NavigationTarget target,IReadOnlyList<NavigationTarget> outward,IReadOnlyList<NavigationTarget> home)
        {
            if(!ValidReturnRoute(home) || outward==null || outward.Count+home.Count>24) return false;
            if(!TrySelectDeliveryTarget(target,outward)) return false;
            if(returnRoute==null) { returnRoute=home.ToArray();returnRouteIndex=0;trafficSelectionVersion++; }
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
            selectedDeliveryTarget=target; deliveryRoute=route.ToArray(); deliveryRouteIndex=0;trafficSelectionVersion++; return true;
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
            if (moveLeg!=null && moveLeg.AwaitingTraffic) { TryIssueMove(moveLeg); return; }
            if(pendingDeliveryTraffic.HasValue)
            {var target=pendingDeliveryTraffic.Value;pendingDeliveryTraffic=null;BeginMove(target,EmployeeState.CarryingToPickUp);return;}
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
            var coordinated=environment is IEmployeeTrafficEnvironment trafficPort && trafficPort.TrafficEnabled;
            if(!coordinated && integrated && HasSelectedDeliveryRoute && nextState==EmployeeState.CarryingToPickUp && pickupDeliveryTrafficWait!=null)
            {
                var currentHandle=handleId;var currentGeneration=generation;var currentRevision=layoutRevision;
                var wait=pickupDeliveryTrafficWait(target);
                if(!LiveTask(currentHandle) || generation!=currentGeneration || layoutRevision!=currentRevision) return;
                // Keep the same unissued leg/cup/lease; clearance still belongs to the original MoveTo guard.
                // 保留原任务，不重复制作/acquire，也不在traffic期间创建Navigation request。
                if(wait || !environment.IsProgressAllowed)
                {state=EmployeeState.CarryingToPickUp;pendingDeliveryTraffic=target;return;}
            }
            if(!coordinated && integrated && HasSelectedReturnRoute && nextState==EmployeeState.Returning && pickupReturnTrafficWait!=null)
            {
                var currentHandle=handleId;var currentGeneration=generation;var currentRevision=layoutRevision;
                var wait=pickupReturnTrafficWait(target);
                if(!LiveTask(currentHandle) || generation!=currentGeneration || layoutRevision!=currentRevision) return;
                if(wait)
                {state=EmployeeState.Returning;pendingReturnTraffic=target;return;}
            }
            // 必须先建立 leg；MoveTo 允许在返回 request ID 前同步完成。
            var leg = new MoveLeg { Generation = generation, LayoutRevision = layoutRevision, State = nextState,
                NativeLegGeneration = ++nextNativeLegGeneration, Target = target, AwaitingTraffic = coordinated };
            moveLeg = leg;
            state = nextState;
            environment.SetVisualMode(nextState == EmployeeState.CarryingToPickUp
                ? EmployeeVisualMode.Carrying : EmployeeVisualMode.Walking);
            if (!IsLive(leg)) return;
            TryIssueMove(leg);
        }
        private void TryIssueMove(MoveLeg leg)
        {
            if (!IsLive(leg) || leg.Completed || !environment.IsProgressAllowed) return;
            if (environment is IEmployeeTrafficEnvironment port && port.TrafficEnabled)
            {
                var decision=port.PrepareMove(leg.Target,leg.NativeLegGeneration);
                if (!IsLive(leg) || leg.Completed) return;
                if (decision.Kind==TrafficDecisionKind.DeferredByBudget || decision.Kind==TrafficDecisionKind.WaitingForTraffic)
                { leg.AwaitingTraffic=true; return; }
                if (decision.Kind!=TrafficDecisionKind.Granted)
                {
                    moveLeg=null;
                    // A traffic terminal keeps the real checkpoint, without inventing a recovery Native move.
                    // 普通等待不会走到这里；真正拒绝也不能自动发返岗请求掩盖错误。
                    FailTask(decision.Kind+": "+decision.Reason,false);return;
                }
                leg.TrafficIntermediate=decision.Route.Count==1 && decision.Route[0]!=leg.Target.Position;
            }
            if (!IsLive(leg) || !environment.IsProgressAllowed) { leg.AwaitingTraffic=true; return; }
            leg.AwaitingTraffic=false;
            var start = environment.MoveTo(leg.Target, result => OnMoveCompleted(leg, result));
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
                // Recovered means only the safe anchor was reached, not the service target.
                // 导航安全返回仍是未完成服务；不能显示成无错误的恢复成功。
                var reason = result.Status == MovementStatus.Recovered
                    ? "已返回安全位置，原服务目标未到达，请修复后重试 / Original service target not reached: " + result.OriginalFailure
                    : result.Status + ": " + result.Reason;
                OnMoveFailed(leg.State, reason);
                return;
            }
            if(leg.TrafficIntermediate)
            { BeginMove(leg.Target,leg.State);return; } // Real intermediate Arrived is never a business arrival.
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
            pendingDeliveryTraffic=null;
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


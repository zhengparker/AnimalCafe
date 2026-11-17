using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.CafeLoop;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using UnityEngine;

namespace AnimalCafe.Employees
{
    /// <summary>两名员工的 Scene 接线；移动只交给 NavigationWorld / Scene orchestration only.</summary>
    public sealed class EmployeeFlowController : MonoBehaviour
    {
        [SerializeField] private NavigationWorld world;
        [SerializeField] private NavigationLayoutAdapter adapter;
        [SerializeField] private CafeLayoutRuntime layout;
        [SerializeField] private DecorationModeController decoration;
        [SerializeField] private GameTimeService time;
        [SerializeField] private CustomerFlowController customers;
        [SerializeField] private Transform gridRoot;
        [SerializeField] private NavigationActor cashierPrefab, baristaPrefab;
        [SerializeField] private PickupCupDisplay display;
        // Task7 builder assigns the original Cup explicitly; missing configuration must fail.
        [SerializeField] private GameObject cupPrefab;

        private sealed class Employee
        {
            internal EmployeeRole Role;
            internal string Id, Failure = "";
            internal bool SpawnAttempted, NeedsBinding;
            internal NavigationActor Actor;
            internal EmployeeStationBinding Binding;
            internal EmployeeVisualPresenter Visual;
            internal EmployeeTaskRunner Runner;
            internal Environment Port;
            internal CafeWorkCheckpoint? Checkpoint;
            internal CafeTaskToken? AcceptedToken;
            internal long HighestHandle;
        }
        private readonly List<Employee> employees = new List<Employee>(2);
        private readonly EmployeeStationBindingResolver resolver = new EmployeeStationBindingResolver();
        private string session;
        private bool disposed;
        private int observedRevision;
        private long completedDecorVersion;
        [SerializeField] private bool integratedMode;
        private CafeDeliveryCommit deliveryCommit;
        private Func<string, bool> deliveryGate;

#if UNITY_EDITOR
        internal Action<string,string,string> MovementObserver;
#endif
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ObserveMovement(Employee employee,string operation,NavigationTarget target,long request=0,
            NavigationFailure reason=NavigationFailure.None,MovementStatus? status=null,bool? accepted=null)
        {
#if UNITY_EDITOR
            if(MovementObserver==null) return;
            var snapshot=employee.Runner?.Snapshot;
            MovementObserver(employee.Id,operation,"phase="+snapshot?.State+" handle="+snapshot?.HandleId+
                " layout="+employee.Binding.LayoutRevision+" request="+request+" reason="+reason+
                " status="+status+" accepted="+accepted+" target="+target.Position.ToString("F9"));
#endif
        }

        public event Action<CafeTaskEvent> IntegratedTaskEvent;
        public void SetIntegratedMode(bool enabled) => integratedMode = enabled;
        // Done丢弃旧订单context；保留身体/身份，由正常Step真实回新岗位。
        // Retire callbacks before cancelling, without reconstructing the employee session.
        public void DiscardIntegratedTasksForLayout()
        {
            if(!integratedMode) return;
            foreach(var employee in employees)
            {
                employee.AcceptedToken=null; employee.Checkpoint=null;
                if(employee.Runner!=null) employee.HighestHandle=Math.Max(employee.HighestHandle,employee.Runner.Snapshot.HandleId);
                employee.Runner?.Dispose(); employee.Port?.Dispose();
                employee.Runner=null; employee.Port=null; employee.NeedsBinding=employee.Actor!=null;
                employee.Visual?.HideHeldCup();
            }
        }
        public void ResumeIntegratedEmployees()
        {
            if(!integratedMode || !ProgressAllowed) return;
            foreach(var employee in employees)
            {
                if(employee.Checkpoint.HasValue && employee.Checkpoint.Value.Stage!=CafeWorkStage.Finished) continue;
                if(employee.Actor==null)
                { employee.SpawnAttempted=false; employee.Runner=null; employee.Port?.Dispose(); employee.Port=null; TryCreate(employee); continue; }
                if(!employee.Actor.isActiveAndEnabled) continue;
                if(employee.Runner==null || employee.Runner.Snapshot.State==EmployeeState.Disabled) StartRunner(employee);
                if(employee.NeedsBinding || employee.Runner.Snapshot.State==EmployeeState.Blocked ||
                    employee.Runner.Snapshot.State==EmployeeState.Initializing) Rebind(employee);
            }
        }
        public void SetDeliveryCommit(CafeDeliveryCommit commit) => deliveryCommit = commit;
        public void SetDeliveryGate(Func<string, bool> gate) => deliveryGate = gate;
        // This owner seals the real roster/actor/binding around the Runner's original task stamp.
        // 两层实际 owner 验证，不接受 ActorId/context 字符串作为身份凭证。
        // The real roster/port owns this seal; availability is checked by the movement consumer.
        // 原始员工身份只捕获一次，Pause/临时 Nav 不伪造新的 task 或 source。
        internal sealed class PreparedMovementContext
        {
            private readonly EmployeeFlowController owner;
            private readonly Employee employee;
            private readonly Environment port;
            private readonly EmployeeTaskRunner runner;
            private readonly NavigationActor actor;
            private readonly EmployeeStationBinding binding;
            private readonly CustomerFlowController.TrafficMovement movement;
            private readonly EmployeeTaskRunner.PreparedMovementStamp stamp;
            private readonly NavigationWorld world;
            private readonly NavigationLayoutAdapter adapter;
            private readonly CustomerFlowController customers;
            private readonly string session;
            private readonly EmployeeRole role;
            private readonly float skin,epsilon;
            private PreparedMovementContext(EmployeeFlowController owner,Employee employee,
                CustomerFlowController.TrafficMovement movement,EmployeeTaskRunner.PreparedMovementStamp stamp)
            {
                this.owner=owner;this.employee=employee;this.movement=movement;this.stamp=stamp;
                port=employee.Port;runner=employee.Runner;actor=employee.Actor;binding=employee.Binding;role=employee.Role;
                world=owner.world;adapter=owner.adapter;customers=owner.customers;session=owner.session;
                Key=movement.Intent;Goal=movement.Goal;Resource=movement.Resource;FirstFrame=Time.frameCount;
                Origin=actor.transform.position;Radius=actor.Settings.AgentRadius;skin=actor.Settings.CollisionSkin;epsilon=actor.Settings.Epsilon;
            }
            internal TrafficIntentKey Key { get; }
            internal NavigationTarget Goal { get; }
            internal string Resource { get; }
            internal int FirstFrame { get; }
            internal Vector3 Origin { get; }
            internal float Radius { get; }
            internal float SafetyMargin=>skin+epsilon;
            internal object ActorLife=>actor;
            internal bool BelongsTo(CustomerFlowController consumer,CustomerFlowController.TrafficMovement value)
                =>ReferenceEquals(customers,consumer) && ReferenceEquals(movement,value);
            internal static bool TryCapture(EmployeeFlowController owner,string actorId,CustomerFlowController.TrafficMovement movement,
                int nativeGeneration,out PreparedMovementContext context)
            {
                context=null;
                try
                {
                    var employee=owner?.employees.FirstOrDefault(e=>e.Id==actorId);
                    if(owner==null || !owner.integratedMode || owner.disposed || employee?.Runner==null || employee.Port==null ||
                        employee.Actor==null || employee.NeedsBinding || !owner.ValidBinding(employee.Binding) || movement==null ||
                        movement.Source!=null || movement.PendingSource!=null || movement.PreparationContext!=null ||
                        movement.Coordinator==null || movement.Coordinator.ContainsIntent(movement.Intent) ||
                        !employee.Port.MatchesMovement(movement) || !employee.Runner.TryCapturePreparedMovementStamp(employee.AcceptedToken,employee.Checkpoint,out var stamp) ||
                        stamp.NativeGeneration!=nativeGeneration || !stamp.Target.Equals(movement.Goal) ||
                        !stamp.BelongsTo(employee.Runner,employee.Binding,employee.Port,employee.Role,employee.AcceptedToken,employee.Checkpoint))return false;
                    var captured=new PreparedMovementContext(owner,employee,movement,stamp);
                    if(!captured.IsCurrent)return false;context=captured;return true;
                }
                catch(Exception){context=null;return false;}
            }
            internal bool IsCurrent
            {
                get
                {
                    try
                    {
                        return owner!=null && !owner.disposed && owner.integratedMode && owner.session==session &&
                            ReferenceEquals(owner.world,world) && ReferenceEquals(owner.adapter,adapter) && ReferenceEquals(owner.customers,customers) &&
                            owner.employees.Any(e=>ReferenceEquals(e,employee)) && employee.Role==role && employee.Id==Key.ActorId && !employee.NeedsBinding &&
                            ReferenceEquals(employee.Port,port) && port.MatchesMovement(movement) && ReferenceEquals(employee.Runner,runner) &&
                            ReferenceEquals(employee.Actor,actor) && actor!=null && actor.ActorId==Key.ActorId && actor.isActiveAndEnabled &&
                            world!=null && world.RegisteredActors.Any(a=>ReferenceEquals(a,actor)) &&
                            actor.transform.position==Origin && actor.Settings.AgentRadius==Radius && actor.Settings.CollisionSkin==skin && actor.Settings.Epsilon==epsilon &&
                            ReferenceEquals(employee.Binding,binding) && owner.ValidBinding(binding) && binding.Role==role &&
                            movement.Intent.Equals(Key) && movement.Goal.Equals(Goal) && movement.Resource==Resource &&
                            stamp.BelongsTo(runner,binding,port,role,employee.AcceptedToken,employee.Checkpoint) && stamp.IsCurrent;
                    }
                    catch(Exception){return false;}
                }
            }
        }

        internal void RefreshTrafficMovementPreparations(CustomerFlowController consumer,TrafficWorldSnapshot snapshot)
        {
            if(disposed || !integratedMode || !ReferenceEquals(customers,consumer) || snapshot==null)return;
            foreach(var employee in employees)employee.Port?.RefreshMovementPreparation(consumer,snapshot);
        }

        internal sealed class PreparedDeliveryContext
        {
            private readonly EmployeeFlowController owner;
            private readonly Employee employee;
            private readonly NavigationActor actor;
            private readonly EmployeeTaskRunner runner;
            private readonly EmployeeStationBinding binding;
            private readonly EmployeeTaskRunner.PreparedPickupStamp stamp;
            private readonly CafeTaskToken token;
            private readonly CafeWorkCheckpoint checkpoint;
            private readonly NavigationWorld world;
            private readonly NavigationLayoutAdapter adapter;
            private readonly CustomerFlowController customers;
            private readonly string session;
            private readonly Vector3 pose;
            private readonly float radius,skin,epsilon;
            private PreparedDeliveryContext(EmployeeFlowController owner,Employee employee,EmployeeTaskRunner.PreparedPickupStamp stamp)
            {
                this.owner=owner;this.employee=employee;this.stamp=stamp;actor=employee.Actor;runner=employee.Runner;binding=employee.Binding;
                token=employee.AcceptedToken.Value;checkpoint=employee.Checkpoint.Value;world=owner.world;adapter=owner.adapter;customers=owner.customers;
                session=owner.session;pose=actor.transform.position;radius=actor.Settings.AgentRadius;skin=actor.Settings.CollisionSkin;epsilon=actor.Settings.Epsilon;
                ActorId=employee.Id;Context=stamp.Context;WorkTarget=binding.WorkTarget;
            }
            internal string ActorId { get; }
            internal string Context { get; }
            internal object ActorLife => actor;
            internal NavigationTarget WorkTarget { get; }
            internal bool BelongsTo(CustomerFlowController flow)=>ReferenceEquals(customers,flow) && ReferenceEquals(owner.customers,flow);
            internal bool IsCurrent => owner!=null && owner.IsCurrentPreparedDeliveryContext(this);
            internal bool SameSource(PreparedDeliveryContext other)=>other!=null && ReferenceEquals(owner,other.owner) &&
                ReferenceEquals(employee,other.employee) && ReferenceEquals(actor,other.actor) && ReferenceEquals(runner,other.runner) &&
                ReferenceEquals(binding,other.binding) && token.Equals(other.token) && checkpoint.Equals(other.checkpoint) && Context==other.Context;
            internal static bool TryCapture(EmployeeFlowController owner,string actorId,out PreparedDeliveryContext context)
            {
                context=null;
                if(owner==null || !owner.integratedMode || !owner.ProgressAllowed)return false;
                var employee=owner.employees.FirstOrDefault(e=>e.Id==actorId && e.Role==EmployeeRole.Barista);
                if(employee?.Runner==null || employee.Actor==null || employee.NeedsBinding || !owner.ValidBinding(employee.Binding) ||
                    !employee.AcceptedToken.HasValue || !employee.Checkpoint.HasValue ||
                    !employee.Runner.TryCapturePreparedPickupStamp(out var stamp) ||
                    !stamp.BelongsTo(employee.Runner,employee.Binding,employee.AcceptedToken.Value,employee.Checkpoint.Value))return false;
                var captured=new PreparedDeliveryContext(owner,employee,stamp);
                if(!captured.IsCurrentFor(owner))return false;context=captured;return true;
            }
            internal bool IsCurrentFor(EmployeeFlowController candidate)
                =>ReferenceEquals(owner,candidate) && owner!=null && owner.integratedMode && owner.ProgressAllowed && owner.session==session &&
                    ReferenceEquals(owner.world,world) && ReferenceEquals(owner.adapter,adapter) && ReferenceEquals(owner.customers,customers) &&
                    owner.employees.Any(e=>ReferenceEquals(e,employee)) && employee.Id==ActorId && employee.Role==EmployeeRole.Barista &&
                    !employee.NeedsBinding && ReferenceEquals(employee.Actor,actor) && actor!=null && actor.isActiveAndEnabled &&
                    actor.Agent!=null && actor.Agent.enabled && actor.Agent.isOnNavMesh && world.RegisteredActors.Any(a=>ReferenceEquals(a,actor)) &&
                    actor.transform.position==pose && actor.Settings.AgentRadius==radius && actor.Settings.CollisionSkin==skin && actor.Settings.Epsilon==epsilon &&
                    ReferenceEquals(employee.Runner,runner) && ReferenceEquals(employee.Binding,binding) && owner.ValidBinding(binding) &&
                    employee.AcceptedToken.HasValue && employee.AcceptedToken.Value.Equals(token) &&
                    employee.Checkpoint.HasValue && employee.Checkpoint.Value.Equals(checkpoint) &&
                    stamp.BelongsTo(runner,binding,token,checkpoint) && stamp.IsCurrent && stamp.Context==Context;
        }
        internal bool TryCapturePreparedDeliveryContext(string actorId,out PreparedDeliveryContext context)
        {
            context=null;
            try { return PreparedDeliveryContext.TryCapture(this,actorId,out context); }
            catch(Exception) { context=null;return false; }
        }
        internal bool IsCurrentPreparedDeliveryContext(PreparedDeliveryContext context)
        {
            try { return context!=null && context.IsCurrentFor(this); }
            catch(Exception) { return false; }
        }

        internal string PickupTrafficContext(string actorId)
        {
            var employee=employees.FirstOrDefault(e=>e.Id==actorId);
            return employee?.Runner!=null && ValidBinding(employee.Binding) && employee.Runner.Snapshot.State==EmployeeState.WaitingToDeliver &&
                employee.Runner.Snapshot.HandleId!=0 && employee.Runner.CaptureCheckpoint().Stage==CafeWorkStage.Prepared?employee.Runner.PickupTrafficContext:null;
        }
        internal float IndependentTrafficSeconds(string actorId)
        {
            var employee=employees.FirstOrDefault(e=>e.Id==actorId);
            if(disposed || employee?.Actor==null || !employee.Actor.isActiveAndEnabled || employee.NeedsBinding || !ValidBinding(employee.Binding) || employee.Runner==null)return 0;
            if(employee.Runner.HasIndependentNativeTraffic)return employee.Actor.Settings.MinimumSegmentSeconds+employee.Actor.Settings.NoProgressSeconds;
            var snapshot=employee.Runner.Snapshot;
            return snapshot.State==EmployeeState.Working && snapshot.RemainingWorkSeconds>0?snapshot.RemainingWorkSeconds:0;
        }
        internal bool IsPickupJourneyCurrent(string actorId)
        {
            var employee=employees.FirstOrDefault(e=>e.Id==actorId);
            return !disposed && employee?.Actor!=null && employee.Actor.isActiveAndEnabled && !employee.NeedsBinding &&
                ValidBinding(employee.Binding) && employee.Runner!=null && employee.Runner.HasLivePickupJourney;
        }
        internal bool TryPreparePickupTrafficRoutes(string actorId,NavigationTarget target,IReadOnlyList<NavigationTarget> outward,
            IReadOnlyList<NavigationTarget> home,out IPreparedTrafficMutation prepared)
        {
            prepared=null;var employee=employees.FirstOrDefault(e=>e.Id==actorId && e.Role==EmployeeRole.Barista);
            return integratedMode && ProgressAllowed && employee?.Runner!=null && ValidBinding(employee.Binding) &&
                customers.GetPickupServiceTargets(employee.Binding.PickUpId).Any(t=>t.Position==target.Position && t.Facing==target.Facing) &&
                employee.Runner.TryPreparePickupRoutes(target,outward,home,out prepared);
        }
        internal IReadOnlyList<NavigationTarget> GetPickupReturnTraffic(string actorId)
        {
            var employee=employees.FirstOrDefault(e=>e.Id==actorId);var runner=employee?.Runner;
            if(!integratedMode || disposed || runner==null || employee.Actor==null || !employee.Actor.isActiveAndEnabled ||
                employee.NeedsBinding || !ValidBinding(employee.Binding)) return null;
            var targets=runner.RemainingPickupReturnRoute;if(targets==null)return null;
            var outward=runner.RemainingPickupDeliveryRoute;
            // The existing carrying return view begins at the delivery endpoint.
            // 当前outward已经包含该endpoint，故只拼接其后已选return，保留真实pose。
            if(outward!=null) targets=outward.Concat(targets.Skip(1)).ToArray();
            else if(runner.Snapshot.State!=EmployeeState.Returning)
            {
                var stage=runner.CaptureCheckpoint().Stage;
                if(runner.Snapshot.State!=EmployeeState.CarryingToPickUp ||
                    stage!=CafeWorkStage.Delivered && stage!=CafeWorkStage.Returning) return null;
                // Synchronous Delivered callbacks precede the Returning state update.
                // 最后一腿已Arrived，仍保留当前身体到已选return的所有权。
                targets=targets.Skip(1).ToArray();
            }
            return new[]{new NavigationTarget(employee.Actor.transform.position)}.Concat(targets).ToArray();
        }
        internal void BlockPreparedDelivery(string actorId,string reason)
        {
            var employee=employees.FirstOrDefault(e=>e.Id==actorId && e.Role==EmployeeRole.Barista);
            if(integratedMode && employee?.Runner!=null && employee.Runner.Snapshot.State==EmployeeState.WaitingToDeliver &&
                !string.IsNullOrEmpty(reason)) employee.Runner.Cancel(reason);
        }
        public bool TrySelectDeliveryTarget(string actorId,NavigationTarget target) => TrySelectDeliveryTarget(actorId,target,new[]{target});
        public bool TrySelectDeliveryTarget(string actorId,NavigationTarget target,IReadOnlyList<NavigationTarget> route)
        {
            var employee=employees.FirstOrDefault(e=>e.Id==actorId && e.Role==EmployeeRole.Barista);
            return integratedMode && ProgressAllowed && employee?.Runner!=null && ValidBinding(employee.Binding) &&
                customers.GetPickupServiceTargets(employee.Binding.PickUpId).Any(t=>t.Position==target.Position && t.Facing==target.Facing) &&
                employee.Runner.TrySelectDeliveryTarget(target,route);
        }
        internal bool TrySelectPickupRoutes(string actorId,NavigationTarget target,IReadOnlyList<NavigationTarget> outward,IReadOnlyList<NavigationTarget> home)
        {
            var employee=employees.FirstOrDefault(e=>e.Id==actorId && e.Role==EmployeeRole.Barista);
            var selected=integratedMode && ProgressAllowed && employee?.Runner!=null && ValidBinding(employee.Binding) &&
                customers.GetPickupServiceTargets(employee.Binding.PickUpId).Any(t=>t.Position==target.Position && t.Facing==target.Facing) &&
                employee.Runner.TrySelectPickupRoutes(target,outward,home);
            if(selected) ObserveMovement(employee,"delivery.route-selected",target);
            return selected;
        }
        public bool TryGetDeliveryTarget(out NavigationTarget target)
        {
            var value=employees.FirstOrDefault(e=>e.Role==EmployeeRole.Barista)?.Runner?.DeliveryTarget;
            target=value??default; return value.HasValue;
        }
        public bool TryStartOrderTask(CafeTaskToken token, CafeWorkCheckpoint checkpoint, out string reason)
        {
            reason = "员工或订单上下文不可用 / Employee task unavailable";
            if (!integratedMode || !ProgressAllowed) return false;
            var employee = employees.FirstOrDefault(e => e.Role == checkpoint.Role);
            if (employee?.Actor == null || !employee.Actor.isActiveAndEnabled || !ValidAttempt(employee, token, checkpoint)) return false;
            if (employee.Runner == null || employee.Runner.Snapshot.State == EmployeeState.Disabled)
            { StartRunner(employee); employee.NeedsBinding = true; }
            if (employee.NeedsBinding || !ValidBinding(employee.Binding))
            {
                if (!TryBind(employee, out reason)) return false;
            }
            var oldToken = employee.AcceptedToken;
            if(checkpoint.Role==EmployeeRole.Barista && (checkpoint.Stage==CafeWorkStage.Returning || checkpoint.Stage==CafeWorkStage.Delivered) &&
                customers.HasPickupDeliveryContext && !customers.HasTrafficCoordinator)
            {
                if(!customers.TryPlanPickupReturn(employee.Id,employee.Actor.transform.position,employee.Binding.WorkTarget,out var home) ||
                    !employee.Runner.ConfigurePickupReturnRoute(home))
                { reason="返程通道堵塞，请调整布局 / Return corridor blocked";return false; }
            }
            employee.AcceptedToken = token; // 同步Progress必须已经属于此次attempt。
            if (!employee.Runner.TryStartIntegrated(token, checkpoint))
            { employee.AcceptedToken = oldToken; reason = "员工任务拒绝 / Employee task rejected"; return false; }
            reason = ""; return true;
        }
        public bool TryCaptureOrderCheckpoint(EmployeeRole role, out CafeWorkCheckpoint checkpoint)
        {
            var employee = employees.FirstOrDefault(e => e.Role == role);
            checkpoint = employee?.Checkpoint ?? default; return employee?.Checkpoint != null;
        }
        internal bool IsCurrentCashierWorkingFor(string visitId,string sessionId,long epoch)
        {
            var employee=employees.FirstOrDefault(e=>e.Role==EmployeeRole.Cashier);
            if(!integratedMode || !ProgressAllowed || employee?.Runner==null || employee.Actor==null ||
                !employee.Actor.isActiveAndEnabled || !employee.AcceptedToken.HasValue || !employee.Checkpoint.HasValue) return false;
            var token=employee.AcceptedToken.Value;var checkpoint=employee.Checkpoint.Value;var current=employee.Runner.Snapshot;
            return token.SessionId==sessionId && token.Epoch==epoch && current.State==EmployeeState.Working &&
                current.HandleId!=0 && checkpoint.Stage==CafeWorkStage.Working && checkpoint.VisitId==visitId &&
                current.RemainingWorkSeconds>0 && !float.IsNaN(current.RemainingWorkSeconds) && !float.IsInfinity(current.RemainingWorkSeconds);
        }
        internal Func<bool> CaptureCashierTaskLife(string visitId,CafeTaskToken token,string actorId,long handle)
        {
            if(!IsCurrentCashierWorkingFor(visitId,token.SessionId,token.Epoch))return null;
            var employee=employees.FirstOrDefault(e=>e.Role==EmployeeRole.Cashier);
            if(employee==null || employee.Id!=actorId || !employee.AcceptedToken.Value.Equals(token) || employee.Runner.Snapshot.HandleId!=handle)return null;
            var actor=employee.Actor;var runner=employee.Runner;var binding=employee.Binding;var port=employee.Port;
            return ()=>
            {
                if(disposed || !isActiveAndEnabled || !integratedMode || !employees.Any(e=>ReferenceEquals(e,employee)) ||
                    !ReferenceEquals(employee.Actor,actor) || actor==null || !actor.isActiveAndEnabled ||
                    !ReferenceEquals(employee.Runner,runner) || !ReferenceEquals(employee.Binding,binding) || !ReferenceEquals(employee.Port,port) ||
                    !employee.AcceptedToken.HasValue || !employee.AcceptedToken.Value.Equals(token) || !employee.Checkpoint.HasValue ||
                    employee.Checkpoint.Value.VisitId!=visitId || employee.Checkpoint.Value.Role!=EmployeeRole.Cashier)return false;
                var current=runner.Snapshot;
                return current.State==EmployeeState.Working && current.HandleId==handle && current.RemainingWorkSeconds>0 &&
                        !float.IsNaN(current.RemainingWorkSeconds) && !float.IsInfinity(current.RemainingWorkSeconds) ||
                    current.State==EmployeeState.Idle && current.HandleId==0 && current.Outcome==EmployeeTaskOutcome.Succeeded &&
                        employee.Checkpoint.Value.Stage==CafeWorkStage.Finished;
            };
        }

        public void HoldIntegratedTasks()
        {
            if (!integratedMode) return;
            foreach (var employee in employees) employee.Runner?.Cancel("经营协调器已暂停任务 / Integrated task held");
        }
        public bool IsCurrentDeliveryArrival(CafeTaskEvent arrival, SurfaceSlotAddress address)
        {
            var employee = employees.FirstOrDefault(e => e.Id == arrival.ActorId && e.Role == arrival.Role);
            if (!integratedMode || !ProgressAllowed || employee?.Runner == null || employee.Actor == null ||
                !employee.Actor.isActiveAndEnabled || !ValidBinding(employee.Binding) ||
                !employee.Binding.CupAddress.HasValue || !employee.Binding.CupAddress.Value.Equals(address) ||
                !employee.Runner.IsCurrentDeliveryArrival(arrival)) return false;
            var target = employee.Runner.DeliveryTarget.Value;
            // Navigation的实际最终Arrived仍须匹配当前身体与正式anchor，查询无写入。
            return Vector3.Distance(employee.Actor.transform.position, target.Position) <= .08f &&
                (!target.Facing.HasValue || Vector3.Angle(employee.Actor.transform.forward, target.Facing.Value) <= 5f) &&
                employee.Actor.GetComponent<NavigationWalkPresenter>().ActualSpeed <= .05f;
        }
        private static bool ValidAttempt(Employee employee, CafeTaskToken token, CafeWorkCheckpoint saved)
        {
            if (string.IsNullOrWhiteSpace(token.SessionId) || token.Epoch <= 0 || token.Sequence <= 0 || token.Attempt <= 0) return false;
            if (!employee.AcceptedToken.HasValue) return true;
            var prior = employee.AcceptedToken.Value;
            if (token.SessionId != prior.SessionId || token.Epoch < prior.Epoch) return false;
            if (token.Epoch > prior.Epoch) return true;
            if (token.Sequence < prior.Sequence) return false;
            if (token.Sequence > prior.Sequence) return employee.Checkpoint?.Stage == CafeWorkStage.Finished;
            var previous = employee.Checkpoint;
            return token.Attempt > prior.Attempt && previous.HasValue && saved.VisitId == previous.Value.VisitId &&
                Nullable.Equals(saved.Order, previous.Value.Order) && (int)saved.Stage >= (int)previous.Value.Stage &&
                (saved.Stage != CafeWorkStage.Working || saved.RemainingWorkSeconds <= previous.Value.RemainingWorkSeconds);
        }

        public IReadOnlyList<EmployeeTaskSnapshot> Snapshot => employees.Select(employee => employee.Runner?.Snapshot ??
            new EmployeeTaskSnapshot(employee.Id, employee.Role,
                string.IsNullOrEmpty(employee.Failure) ? EmployeeState.Initializing : EmployeeState.Blocked,
                0, EmployeeTaskOutcome.None, 0, false, employee.Failure)).ToList().AsReadOnly();

        public void Configure(NavigationWorld world, NavigationLayoutAdapter adapter, CafeLayoutRuntime layout,
            DecorationModeController decoration, GameTimeService time, CustomerFlowController customers,
            Transform gridRoot, NavigationActor cashierPrefab, NavigationActor baristaPrefab, PickupCupDisplay display)
        {
            CleanupSession();
            this.world = world; this.adapter = adapter; this.layout = layout; this.decoration = decoration;
            this.time = time; this.customers = customers; this.gridRoot = gridRoot;
            this.cashierPrefab = cashierPrefab; this.baristaPrefab = baristaPrefab; this.display = display;
            BeginSession();
        }

        private void Awake() => BeginSession();
        private void BeginSession()
        {
            disposed = false; session = Guid.NewGuid().ToString("N"); employees.Clear();
            observedRevision = adapter == null ? -1 : adapter.Revision;
            completedDecorVersion = decoration == null ? 0 : decoration.CompletedLayoutChangeVersion;
            foreach (EmployeeRole role in Enum.GetValues(typeof(EmployeeRole)))
                employees.Add(new Employee { Role = role, Id = "employee-" + session + "-" + role });
            if (display != null) display.Configure(cupPrefab, ResolveConfirmedSlot);
        }
        private void Update() { if (!integratedMode) Step(Time.deltaTime); } // P14 owner is the only Step driver.

        private bool Ready => !disposed && isActiveAndEnabled && world != null && world.isActiveAndEnabled &&
            adapter != null && adapter.isActiveAndEnabled && adapter.World == world &&
            layout != null && layout.Layout != null && layout.FunctionalSurfaceLayout != null &&
            layout.CurrentReadiness != null && layout.CurrentReadiness.CanOpenForBusiness &&
            adapter.CurrentReadiness.CanResume && adapter.PublishedRevision == adapter.Revision &&
            world.LayoutAvailable && gridRoot != null && time != null && customers != null &&
            !string.IsNullOrEmpty(customers.SelectedRegisterId) && SelectionCurrent;
        // P12 may run later this frame. Wait until its selected CR matches the current formal ordinal choice.
        private bool SelectionCurrent => layout.CurrentReadiness.Stations
            .Where(station => station.IsValid && station.FunctionType == LayoutStationType.CashRegister)
            .OrderBy(station => station.InstanceId, StringComparer.Ordinal)
            .Select(station => station.InstanceId).FirstOrDefault() == customers.SelectedRegisterId;
        private bool ProgressAllowed => Ready && time.CurrentSpeed != GameSpeed.Paused &&
            !time.IsResumeBlocked && (decoration == null || !decoration.IsOpen);

        public void Step(float scaledDeltaTime)
        {
            if (float.IsNaN(scaledDeltaTime) || float.IsInfinity(scaledDeltaTime) || scaledDeltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(scaledDeltaTime));
            if (disposed || !isActiveAndEnabled) return;
            ObserveLayoutChanges();
            foreach (var employee in employees)
            {
                if (employee.SpawnAttempted && employee.Runner != null &&
                    (employee.Actor == null || !employee.Actor.isActiveAndEnabled))
                {
                    employee.Runner?.Dispose(); employee.Port?.Dispose(); employee.NeedsBinding = true;
                    continue; // Keep World registration and its body reservation.
                }
                if (!ProgressAllowed) continue;
                if(integratedMode && scaledDeltaTime==0) continue;
                if (employee.Actor != null && (employee.Runner == null || employee.Runner.Snapshot.State == EmployeeState.Disabled))
                { StartRunner(employee); employee.NeedsBinding = true; }
                if (employee.Runner == null && !employee.SpawnAttempted) TryCreate(employee);
                else if (employee.NeedsBinding && !(integratedMode && employee.Checkpoint.HasValue)) Rebind(employee);
                employee.Runner?.Tick(scaledDeltaTime);
            }
        }

        public bool TryStartTask(EmployeeRole role)
        {
            var employee = employees.FirstOrDefault(value => value.Role == role);
            return !integratedMode && ProgressAllowed && employee?.Actor != null && employee.Actor.isActiveAndEnabled && employee.Runner != null && ValidBinding(employee.Binding) && employee.Runner.TryStart();
        }
        // HUD reads the controller's actual immutable choice, never resolves a second binding.
        public bool TryGetBinding(EmployeeRole role, out EmployeeStationBinding binding)
        {
            binding = employees.FirstOrDefault(value => value.Role == role)?.Binding;
            return binding != null;
        }
        public bool AreEmployeesAtWorkStations(out string reason)
        {
            reason="员工owner或工作站配置不可用 / Employee stations unavailable";
            // 只读真实事实：Ready不含Pause；这里绝不Rebind或发出MoveTo。
            if(!Ready || time.IsResumeBlocked || decoration!=null && decoration.IsOpen || employees.Count!=2) return false;
            foreach(EmployeeRole role in Enum.GetValues(typeof(EmployeeRole)))
            {
                reason=role+" 员工或Binding不可用 / Employee or binding unavailable";
                var matches=employees.Where(employee=>employee.Role==role).ToArray();
                if(matches.Length!=1) return false;
                var employee=matches[0]; var actor=employee.Actor;
                if(actor==null || !actor.isActiveAndEnabled || employee.Runner==null || employee.NeedsBinding ||
                    !ValidBinding(employee.Binding) || employee.Binding.Role!=role ||
                    world.RegisteredActors.Count(value=>value!=null && value.ActorId==employee.Id)!=1 ||
                    !world.RegisteredActors.Contains(actor) || actor.ActorId!=employee.Id || actor.World!=world) return false;
                var state=employee.Runner.Snapshot;
                if(state.State!=EmployeeState.Idle || state.HandleId!=0)
                { reason=role+" 员工尚未返岗 / "+state.State+" "+state.Failure; return false; }
                var motion=actor.GetComponent<NavigationWalkPresenter>(); var target=employee.Binding.WorkTarget;
                var settings=actor.Settings;
                reason=role+" 真实位置、速度或朝向未到岗 / Work station arrival pending";
                if(settings==null || motion==null || !motion.isActiveAndEnabled || actor.Agent==null ||
                    !actor.Agent.enabled || !actor.Agent.isOnNavMesh ||
                    !(Vector3.Distance(actor.transform.position,target.Position)<=settings.ArrivalDistance) ||
                    !(motion.ActualSpeed<=settings.ArrivalSpeed) ||
                    target.Facing.HasValue && !(Vector3.Angle(actor.transform.forward,target.Facing.Value)<=settings.FacingToleranceDegrees)) return false;
            }
            reason=""; return true;
        }
        public bool TryCancelTask(EmployeeRole role)
        {
            var employee = employees.FirstOrDefault(value => value.Role == role);
            if (integratedMode || disposed || employee?.Runner == null || employee.Runner.Snapshot.HandleId == 0) return false;
            employee.Runner.Cancel("玩家取消任务 / Task cancelled"); return true;
        }
        public void RetryBlockedEmployees()
        {
            if (integratedMode || !ProgressAllowed) return;
            foreach (var employee in employees)
            {
                if (employee.SpawnAttempted && employee.Runner != null && (employee.Actor == null || !employee.Actor.isActiveAndEnabled)) continue;
                if (employee.Actor != null && (employee.Runner == null || employee.Runner.Snapshot.State == EmployeeState.Disabled))
                { StartRunner(employee); employee.NeedsBinding = true; }
                if (employee.NeedsBinding) { Rebind(employee); continue; }
                if (employee.Runner == null) { employee.SpawnAttempted = false; TryCreate(employee); }
                else if (employee.Runner.Snapshot.State == EmployeeState.Blocked)
                    Rebind(employee);
            }
        }

        private void ObserveLayoutChanges()
        {
            if (adapter == null) return;
            if (observedRevision != adapter.Revision)
            {
                observedRevision = adapter.Revision;
                foreach (var employee in employees)
                {
                    employee.Runner?.InvalidateLayout(observedRevision);
                    if (employee.Runner != null && layout != null && customers != null &&
                        !resolver.TryResolve(employee.Role, layout.CurrentReadiness, customers.SelectedRegisterId,
                            observedRevision, ToTarget, adapter.HasClearPath, out _, out var reason,
                            integratedMode?new Func<string,IReadOnlyList<NavigationTarget>>(customers.GetPickupServiceTargets):null))
                        employee.Runner.RejectBinding("工作站不可用 / Station unavailable: " + reason);
                    employee.NeedsBinding = employee.Actor != null;
                }
                display?.RefreshConfirmedSupports();
            }
            var version = decoration == null ? completedDecorVersion : decoration.CompletedLayoutChangeVersion;
            if (version != completedDecorVersion)
            {
                completedDecorVersion = version;
                foreach (var employee in employees) employee.NeedsBinding = employee.Actor != null;
                display?.RefreshConfirmedSupports();
            }
        }

        private void StartRunner(Employee employee)
        {
            employee.Port?.Dispose();
            employee.Port = new Environment(this, employee, session);
            employee.Runner = new EmployeeTaskRunner(employee.Id, employee.Role, employee.Port,
                workDurationSeconds:integratedMode && employee.Role==EmployeeRole.Cashier ? (float?)1f : null,
                previousHandleId: employee.HighestHandle);
            var runner = employee.Runner;
            runner.SetPickupDeliveryTrafficWait(target=>integratedMode && ReferenceEquals(employee.Runner,runner) &&
                customers!=null && customers.ShouldWaitForPickupDelivery(employee.Id,target));
            runner.SetPickupReturnTrafficWait(target=>integratedMode && ReferenceEquals(employee.Runner,runner) &&
                customers!=null && customers.ShouldWaitForPickupReturn(employee.Id,target));
            runner.IntegratedTaskEvent += value =>
            {
                if (!integratedMode || !ReferenceEquals(employee.Runner, runner) ||
                    !employee.AcceptedToken.HasValue || !employee.AcceptedToken.Value.Equals(value.Token)) return;
                employee.Checkpoint = value.Checkpoint;
                employee.HighestHandle = Math.Max(employee.HighestHandle, value.HandleId);
                IntegratedTaskEvent?.Invoke(value);
            };
        }

        private void Rebind(Employee employee)
        {
            if (!ProgressAllowed || employee.Actor == null || !employee.Actor.isActiveAndEnabled) return;
            if (TryBind(employee, out _)) employee.Runner.TryReturnToWork();
        }
        private bool TryBind(Employee employee, out string reason)
        {
            // Confirm can happen repeatedly while Decor remains open. Only its completed session may rebind.
            employee.NeedsBinding = false;
            if (!resolver.TryResolve(employee.Role, layout.CurrentReadiness, customers.SelectedRegisterId,
                adapter.Revision, ToTarget, adapter.HasClearPath, out var binding, out reason,
                integratedMode?new Func<string,IReadOnlyList<NavigationTarget>>(customers.GetPickupServiceTargets):null) || !ValidBinding(binding))
            { employee.Runner.RejectBinding(string.IsNullOrEmpty(reason) ? "正式工作站不可用 / Station unavailable" : reason); return false; }
            employee.Binding = binding;
            return employee.Runner.TryAssignBinding(binding);
        }

        private void TryCreate(Employee employee)
        {
            if(integratedMode) employee.SpawnAttempted=true; // 明确失败只由受控恢复重试。
            if (!resolver.TryResolve(employee.Role, layout.CurrentReadiness, customers.SelectedRegisterId,
                adapter.Revision, ToTarget, adapter.HasClearPath, out var binding, out var reason,
                integratedMode?new Func<string,IReadOnlyList<NavigationTarget>>(customers.GetPickupServiceTargets):null))
            { employee.Failure = reason; return; }
            employee.Binding = binding;
            if (!ValidBinding(binding)) { employee.Failure = "正式工作站或取餐 Slot 不可用 / Station unavailable"; return; }
            var prefab = employee.Role == EmployeeRole.Cashier ? cashierPrefab : baristaPrefab;
            if (prefab == null) { employee.Failure = "员工 Prefab 缺失 / Missing employee prefab"; return; }
            employee.SpawnAttempted = true;
            if (!adapter.IsPointWalkable(binding.WorkTarget.Position))
            { employee.Failure = "岗位不在当前 owned NavMesh 上 / Work anchor unavailable"; return; }

            var actor = Instantiate(prefab, binding.WorkTarget.Position,
                Quaternion.LookRotation(binding.WorkTarget.Facing.Value), transform);
            // Register is the final unchanged-body, solid and owned NavMesh test. Never project/teleport.
            if (!actor.TryInitializeRuntimeId(employee.Id) || !actor.TryEnableSteadyPathMotion() || !world.Register(actor))
            {
                world.Unregister(actor); actor.gameObject.SetActive(false); Destroy(actor.gameObject);
                employee.Failure = "岗位无法安全注册员工 / Unsafe employee spawn"; return;
            }
            employee.Actor = actor;
            employee.Visual = actor.GetComponent<EmployeeVisualPresenter>();
            if (employee.Visual != null) employee.Visual.Configure(actor, cupPrefab);
            StartRunner(employee);
            employee.Failure = "";
            employee.Runner.TryAssignBinding(binding);
            employee.Runner.TryReturnToWork(); // Even a safe initial pose needs an actual Arrived callback.
        }

        private NavigationTarget ToTarget(InteractionAnchor anchor)
        {
            var size = layout.Layout.GridSettings.CellSize;
            var point = gridRoot.TransformPoint(new Vector3((anchor.Position.X + .5f) * size, 0, (anchor.Position.Y + .5f) * size));
            var direction = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left }[(int)anchor.Facing];
            return new NavigationTarget(point, gridRoot.TransformDirection(direction));
        }

        private bool ValidBinding(EmployeeStationBinding binding)
        {
            if (binding == null || layout == null || layout.FunctionalSurfaceLayout == null ||
                adapter == null || binding.LayoutRevision != adapter.Revision) return false;
            var expected = binding.Role == EmployeeRole.Cashier ? LayoutStationType.CashRegister : LayoutStationType.CoffeeMachine;
            if (!layout.FunctionalSurfaceLayout.MountedInstances.Any(item => item.InstanceId == binding.WorkStationId) ||
                !layout.CurrentReadiness.Stations.Any(station => station.InstanceId == binding.WorkStationId && station.IsValid && station.FunctionType == expected))
                return false;
            if (binding.Role == EmployeeRole.Cashier) return customers != null && binding.WorkStationId == customers.SelectedRegisterId;
            return layout.FunctionalSurfaceLayout.PickUpPoints.Any(item => item.InstanceId == binding.PickUpId &&
                item.Address.Equals(binding.CupAddress.Value)) && ResolveConfirmedSlot(binding.CupAddress.Value) != null;
        }

        private Transform ResolveConfirmedSlot(SurfaceSlotAddress address)
        {
            // Scene objects can outlive domain removals. Check domain membership before the registry.
            if (disposed || layout == null || layout.Layout == null || layout.FunctionalSurfaceLayout == null ||
                !layout.Layout.TryGetFurnitureInstance(address.SupportFurnitureInstanceId, out var support) ||
                layout.SurfaceSlotCatalog == null || !layout.SurfaceSlotCatalog.TryGet(support.DefinitionId, address.SlotId, out _) ||
                !layout.FunctionalSurfaceLayout.PickUpPoints.Any(pickup => pickup.Address.Equals(address))) return null;
            var registry = decoration == null ? null : decoration.GetComponent<FurnitureSceneRegistry>();
            return FunctionalSurfaceViewPositioning.TryResolveSlot(address, registry, out _, out var slot) ? slot : null;
        }

        private void CleanupSession()
        {
            disposed = true; session = null; // Retire the session before external cancel/destroy callbacks.
            foreach (var employee in employees)
            {
                employee.Runner?.Dispose(); employee.Port?.Dispose();
                if (employee.Actor == null) continue;
                if (world != null) world.Unregister(employee.Actor);
                employee.Actor.gameObject.SetActive(false); Destroy(employee.Actor.gameObject);
            }
        }
        private void OnDisable()
        {
            // runner先发布checkpoint，再退休port；Pause不能阻止生命周期保存。
            foreach (var employee in employees) employee.Runner?.Dispose();
            disposed = true; session = null; // Retire callback ports before cancellation can reenter.
            foreach (var employee in employees) employee.Port?.Dispose();
        }
        private void OnEnable()
        {
            if (!disposed) return;
            disposed = false; session = Guid.NewGuid().ToString("N");
            foreach (var employee in employees)
            {
                if (employee.Actor == null) continue; // Destroyed bodies stay retired; failed initial spawns may still retry.
                employee.Runner = null; employee.Port = null; employee.NeedsBinding = true;
            }
        }
        private void OnDestroy() => CleanupSession();

        /// <summary>One employee's small runtime port; no shared task framework.</summary>
        private sealed class Environment : IIntegratedEmployeeTaskEnvironment, IEmployeeTrafficEnvironment, IDisposable
        {
            private readonly EmployeeFlowController owner;
            private readonly Employee employee;
            private readonly string session;
            // New real Port only; pending/Terminal polls retain the same movement scope.
            // 只有真实 Port 新建才产生 scope，同 Port 等待或 Terminal 不换 key。
            private readonly string movementLife = Guid.NewGuid().ToString("N");
            private readonly HashSet<long> requests = new HashSet<long>();
            private CustomerFlowController.TrafficMovement trafficMove;
            private EmployeeState trafficState;
            private int workMoveSequence;
            internal Environment(EmployeeFlowController owner, Employee employee, string session)
            { this.owner = owner; this.employee = employee; this.session = session; }
            private bool Live => owner != null && !owner.disposed && owner.session == session && ReferenceEquals(employee.Port, this) && employee.Actor != null;
            internal bool MatchesMovement(CustomerFlowController.TrafficMovement movement)
                =>Live && ReferenceEquals(trafficMove,movement) && requests.Count==0;
            internal void RefreshMovementPreparation(CustomerFlowController consumer,TrafficWorldSnapshot snapshot)
            {
                var movement=trafficMove;
                if(!ReferenceEquals(owner.customers,consumer) || movement?.PreparationContext==null || movement.Source!=null || movement.PendingSource==null)return;
                consumer.RefreshEmployeeMovementPreparation(employee.Actor,movement,snapshot);
            }
            public bool IsProgressAllowed => Live && employee.Actor.isActiveAndEnabled && owner.ProgressAllowed;
            public bool TrafficEnabled => owner.integratedMode && owner.customers!=null && owner.customers.HasTrafficCoordinator;
            public TrafficDecision PrepareMove(NavigationTarget target,int nativeLegGeneration)
            {
                if(!IsProgressAllowed)return new TrafficDecision(default,TrafficDecisionKind.StaleContext,"employee-lifecycle-unavailable");
                var state=employee.Runner.Snapshot.State;var context=employee.Runner.PickupTrafficContext;
                var selected=(state==EmployeeState.CarryingToPickUp && employee.Runner.HasSelectedDeliveryRoute) ||
                    (state==EmployeeState.Returning && employee.Runner.HasSelectedReturnRoute);
                if(selected)
                {
                    if(trafficMove==null || trafficMove.Intent.Context!=context)
                        trafficMove=owner.customers.CreateEmployeeMovement(employee.Actor,context,state,target);
                    if(trafficMove.Source==null)
                        return new TrafficDecision(trafficMove.Intent,TrafficDecisionKind.Cancelled,"selected-journey-source-unavailable");
                    var resource=state==EmployeeState.Returning?"selected-home":"pickup/"+employee.Id;
                    trafficMove.Resource=resource;trafficMove.Index=employee.Runner.CurrentTrafficRouteIndex;trafficMove.Goal=target;
                }
                else if(trafficMove==null || state!=trafficState || trafficMove.Proof!=null && trafficMove.Index>=trafficMove.Proof.Waypoints.Count)
                {
                    owner.customers.RetireUnissuedMovement(trafficMove);
                    trafficMove=owner.customers.CreateEmployeeMovement(employee.Actor,context+"/port/"+movementLife+"/"+state+"/move/"+(++workMoveSequence),state,target);
                }
                trafficState=state;
                if(!selected && trafficMove.Source==null)
                {
                    if(trafficMove.PreparationContext==null)
                    {
                        if(!PreparedMovementContext.TryCapture(owner,employee.Id,trafficMove,nativeLegGeneration,out var prepared))
                            return owner.customers.RefuseMovementPreparation(trafficMove,"employee-movement-source-unavailable");
                        trafficMove.PreparationContext=prepared;trafficMove.PreparationFrame=prepared.FirstFrame;trafficMove.PreparationOrigin=prepared.Origin;
                    }
                    else if(!trafficMove.PreparationContext.IsCurrent)
                        return owner.customers.RefuseMovementPreparation(trafficMove,"employee-movement-producer-changed");
                }
                var decision=owner.customers.PrepareMovement(employee.Actor,trafficMove,nativeLegGeneration);
                if(decision.Kind!=TrafficDecisionKind.Granted)return decision;
                if(owner.customers.CommitMovement(trafficMove,decision))return decision;
                var failed=trafficMove.Coordinator.GetDecision(trafficMove.Intent);
                return failed.Kind==TrafficDecisionKind.StaleContext && IsProgressAllowed
                    ?new TrafficDecision(trafficMove.Intent,TrafficDecisionKind.DeferredByBudget,"employee-candidate-revalidation") : failed;
            }

            public MoveStartResult MoveTo(NavigationTarget target, Action<MovementResult> completed)
            {
                owner.ObserveMovement(employee,"attempt",target);
                if (!IsProgressAllowed)
                { owner.ObserveMovement(employee,"precheck.progress-refused",target,reason:NavigationFailure.ActorUnavailable); return new MoveStartResult(false, 0, NavigationFailure.ActorUnavailable); }
                if(!TrafficEnabled && owner.integratedMode && employee.Runner.HasSelectedDeliveryRoute && employee.Runner.Snapshot.State==EmployeeState.CarryingToPickUp)
                {
                    var clear=owner.customers.IsPickupDeliverySegmentClear(employee.Id,target);
                    owner.ObserveMovement(employee,clear?"precheck.delivery-clear":"precheck.delivery-refused",target,
                        reason:clear?NavigationFailure.None:NavigationFailure.IncompletePath);
                    if(!clear) return new MoveStartResult(false,0,NavigationFailure.IncompletePath);
                }
                if(!TrafficEnabled && owner.integratedMode && employee.Runner.HasSelectedReturnRoute && employee.Runner.Snapshot.State==EmployeeState.Returning)
                {
                    var clear=owner.customers.IsPickupReturnSegmentClear(employee.Id,target);
                    owner.ObserveMovement(employee,clear?"precheck.return-clear":"precheck.return-refused",target,
                        reason:clear?NavigationFailure.None:NavigationFailure.IncompletePath);
                    if(!clear) return new MoveStartResult(false,0,NavigationFailure.IncompletePath);
                }
                var issuedTraffic=TrafficEnabled?trafficMove:null;
                var nativeGeneration=employee.Runner.CurrentNativeLegGeneration;
                if(TrafficEnabled)
                {
                    owner.customers.RefreshTrafficSnapshot();
                    if(issuedTraffic==null || issuedTraffic.NativeGeneration!=nativeGeneration || !owner.customers.CanIssueMovement(employee.Actor,issuedTraffic))
                        return new MoveStartResult(false,0,NavigationFailure.ActorUnavailable);
                    target=issuedTraffic.Target;
                }
                bool finished = false;long issuedRequest=0;var issuedActor=employee.Actor;
                var result = owner.world.Service.MoveTo(employee.Id, target, movement =>
                {
                    if(issuedTraffic!=null)
                    {
                        if(finished || !Live || !ReferenceEquals(employee.Actor,issuedActor) ||
                            employee.Runner.CurrentNativeLegGeneration!=nativeGeneration || movement.ActorId!=employee.Id ||
                            issuedRequest!=0 && issuedRequest!=movement.RequestId)return;
                        finished=true;
                    }
                    owner.ObserveMovement(employee,"callback",target,movement.RequestId,movement.Reason,movement.Status);
                    finished = true; requests.Remove(movement.RequestId);
                    if (!Live || issuedTraffic!=null && employee.Runner.CurrentNativeLegGeneration!=nativeGeneration) return;
                    // Revision advances before synchronous LayoutChanged callbacks and before Decor Done.
                    if (owner.adapter == null || employee.Binding.LayoutRevision != owner.adapter.Revision)
                    { owner.ObserveLayoutChanges(); return; }
                    if (!employee.Actor.isActiveAndEnabled)
                    { employee.Runner.Dispose(); employee.NeedsBinding = true; return; }
                    if(issuedTraffic!=null && movement.Status==MovementStatus.LayoutChanged && !IsProgressAllowed)
                    {
                        if(!owner.customers.SuspendMovement(issuedActor,issuedTraffic,movement,nativeGeneration,owner.adapter.Revision))
                        { owner.customers.RetireMovement(issuedTraffic);employee.Runner.Cancel("Native suspension is not eligible for another recovery");return; }
                        completed(movement);return; // Keep the original Runner's single navigationResumeUsed recovery.
                    }
                    if (movement.Status == MovementStatus.Arrived)
                    {
                        // Native has already stopped. Publish current stopped motion before business checks.
                        // 真实Native已停下；不能把本帧较早的平均走路速度当作现在仍在移动。
                        try { employee.Actor.SetMotion(0); }
                        catch (Exception error) { Debug.LogException(error, employee.Actor); }
                        // Motion listeners may disable/replace the current lifecycle.
                        // 可选展示事件异常不吞terminal callback；重入停用/装修仍须退休旧回调。
                        if (!Live || issuedTraffic!=null && employee.Runner.CurrentNativeLegGeneration!=nativeGeneration) return;
                        if (owner.adapter == null || employee.Binding.LayoutRevision != owner.adapter.Revision)
                        { owner.ObserveLayoutChanges(); return; }
                        if (!employee.Actor.isActiveAndEnabled)
                        { employee.Runner.Dispose(); employee.NeedsBinding = true; return; }
                        if(issuedTraffic!=null && !owner.customers.CompleteMovement(employee.Actor,issuedTraffic,movement,nativeGeneration))
                        { employee.Runner.Cancel("Actual Native arrival lost its committed traffic leg");return; }
                    }
                    else if(issuedTraffic!=null)owner.customers.RetireMovement(issuedTraffic);
                    completed(movement);
                }, employee.Binding.WorkTarget);
                if(result.Accepted && !finished)issuedRequest=result.RequestId;
                if(result.Accepted && !finished && !Live)
                { owner.world?.Service.Cancel(result.RequestId);return result; }
                if(!result.Accepted && issuedTraffic!=null && issuedTraffic.NativeGeneration==nativeGeneration)
                    owner.customers.RetireMovement(issuedTraffic);
                if(result.Accepted && !finished && Live && issuedTraffic!=null && issuedTraffic.NativeGeneration==nativeGeneration &&
                    !owner.customers.BindMovement(employee.Actor,issuedTraffic,result.RequestId))
                { owner.world.Service.Cancel(result.RequestId);return new MoveStartResult(false,0,NavigationFailure.ActorUnavailable); }
                if (result.Accepted && !finished) requests.Add(result.RequestId);
                owner.ObserveMovement(employee,"submitted",target,result.RequestId,result.Reason,accepted:result.Accepted);
                return result;
            }
            public bool CancelMove(long requestId)
            {
                if (!requests.Remove(requestId) || owner.world == null) return false;
                return owner.world.Service.Cancel(requestId);
            }
            public bool TryShowHeldCup(out string reason)
            {
                reason = "员工 Visual / Cup 配置不可用 / Missing employee visual or cup";
                return IsProgressAllowed && employee.Visual != null && owner.cupPrefab != null &&
                    employee.Visual.TryShowHeldCup(out reason);
            }
            public bool TryPlaceCup(SurfaceSlotAddress address, out string reason)
            {
                reason = "正式取餐 Slot / Cup 展示不可用 / Pickup display unavailable";
                if (!IsProgressAllowed || employee.Visual == null || owner.display == null ||
                    !owner.ValidBinding(employee.Binding) || !employee.Binding.CupAddress.Value.Equals(address)) return false;
                if (!owner.display.TryDeliver(address, out reason)) return false;
                // Only a committed display delivery transfers cup ownership away from the hand.
                employee.Visual.HideHeldCup(); return true;
            }
            public bool TryAcquireDelivery(string actorId) => IsProgressAllowed && actorId == employee.Id &&
                owner.integratedMode && owner.deliveryGate != null && owner.deliveryGate(actorId);
            public bool TryCommitDelivery(CafeTaskEvent arrival, SurfaceSlotAddress address, out string reason)
            {
                reason = "业务交付入口或实际到站不可用 / Delivery commit unavailable";
                return owner.deliveryCommit != null && owner.IsCurrentDeliveryArrival(arrival, address) &&
                    owner.deliveryCommit(arrival, address, out reason);
            }
            public void HideHeldCup() { if (employee.Visual != null) employee.Visual.HideHeldCup(); }
            public void SetVisualMode(EmployeeVisualMode mode)
            {
                // Only authenticated full initial arrival retires its complete permission, not the body.
                // initial 完整到站后释放整个意图；保留原 movement 身份，途中/Return 不走这里。
                if(Live && mode==EmployeeVisualMode.Idle && trafficState==EmployeeState.MovingToWork && employee.Runner!=null &&
                    employee.Runner.Snapshot.State==EmployeeState.Idle && employee.Runner.Snapshot.HandleId==0 &&
                    trafficMove?.Source!=null && trafficMove.Index>0 && trafficMove.NativeGeneration==0 && !trafficMove.Suspended &&
                    employee.Binding!=null && trafficMove.Goal.Equals(employee.Binding.WorkTarget))
                    owner.customers?.RetireMovement(trafficMove);
                if (Live && employee.Visual != null) employee.Visual.SetMode(mode);
            }
            public void CleanupTask(long handleId)
            { owner.customers?.RetireMovement(trafficMove);trafficMove=null;if (employee.Visual != null) employee.Visual.HideHeldCup(); }
            public void Dispose()
            {
                owner.customers?.RetireMovement(trafficMove);trafficMove=null;
                foreach (var request in requests.ToArray()) CancelMove(request);
                // Cancel stops the driver now; publish that stop before selecting Idle animation.
                if (employee.Actor != null) employee.Actor.SetMotion(0);
                if (employee.Visual != null) { employee.Visual.HideHeldCup(); employee.Visual.SetMode(EmployeeVisualMode.Idle); }
            }
        }
    }
}

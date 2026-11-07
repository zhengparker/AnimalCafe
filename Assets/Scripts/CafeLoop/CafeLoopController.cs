using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Core.Time;
using AnimalCafe.BusinessDay;
using AnimalCafe.Core.Events;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using AnimalCafe.Orders;
using UnityEngine;

namespace AnimalCafe.CafeLoop
{
    public sealed class CafeLoopController : MonoBehaviour, ICafeLoopEnvironment
    {
        [SerializeField] private CustomerFlowController customers;
        [SerializeField] private EmployeeFlowController employees;
        [SerializeField] private PickupCupDisplay cups;
        [SerializeField] private GameTimeService time;
        [SerializeField] private NavigationLayoutAdapter adapter;
        [SerializeField] private DecorationModeController decoration;
        private CafeLoopService service;
        private ServiceTrafficCoordinator traffic;
        private int trafficRevision=-1;
        private long trafficEpoch;
        private BusinessDayController businessDay;
        private BusinessDayController businessDayRecorder;
        private bool admissionEnabled=true;
        public void SetAdmissionEnabled(bool enabled)
        {
            admissionEnabled=enabled;
            customers?.SetIntegratedAdmissionAllowed(CanProgress && admissionEnabled);
        }
        internal void CancelUnbornAdmissionForClosing() => customers?.CancelUnbornAdmissionForClosing();
        public void RegisterBusinessDay(BusinessDayController owner)
        {
            if(owner==null) throw new ArgumentNullException(nameof(owner));
            if(businessDay!=null && businessDay!=owner && businessDay.isActiveAndEnabled)
                throw new InvalidOperationException("Cafe Loop已有活动Business Day owner。");
            customers?.RegisterAdmissionPolicy(owner,owner.CanAdmitNow);
            businessDay=owner;
            businessDayRecorder=owner;
            SetAdmissionEnabled(owner.CanAdmitNow());
        }
        public void UnregisterBusinessDay(BusinessDayController owner)
        {
            if(!ReferenceEquals(businessDay,owner)) return;
            customers?.UnregisterAdmissionPolicy(owner);
            businessDay=null; admissionEnabled=false;
        }
        internal void ReleaseBusinessDayRecorder(BusinessDayController owner)
        {
            // 旧owner的OnDestroy不能清掉新owner；disabled保留事实接收端，Destroy才释放。
            if(ReferenceEquals(businessDayRecorder,owner)) businessDayRecorder=null;
        }
        private bool stepping,held,destroying,comboReady;
        private string pickupId,leaseActor;
        private int pickupRevision=-1;
        private Vector3 leasePosition;
        private bool leaseArrived;
        private bool layoutPending,cleanupVerified,layoutFailed;
        private long preparingVersion;
        private IDisposable layoutBlock;
        private string lifecycleFailure="";
        private GameSpeed lastRunningSpeed=GameSpeed.Normal;
        private string readinessReason="等待完整服务组合 / Waiting for stations";
        private string pickupWaitReason="",pickupWaitVisit;
        public event Action<CafeLoopSnapshot> SnapshotChanged;
        // Presentation reads current owners; readiness/admission/traffic permissions stay separate.
        // 当前业务/员工/顾客的真实 Blocked 优先；不比较 diagnostic strings 猜状态。
        public CafeServicePresentation ReadServicePresentation()
        {
            var paused=time!=null && time.CurrentSpeed==GameSpeed.Paused;
            var available=!destroying && !held && isActiveAndEnabled && service!=null &&
                customers!=null && customers.isActiveAndEnabled && employees!=null && employees.isActiveAndEnabled &&
                cups!=null && cups.isActiveAndEnabled && time!=null && time.isActiveAndEnabled &&
                adapter!=null && adapter.isActiveAndEnabled && adapter.World!=null && adapter.World.isActiveAndEnabled &&
                decoration!=null && decoration.isActiveAndEnabled;
            var kind=CafeServicePresentationKind.None;
            if(service?.HasBlockedWork==true || employees!=null && employees.Snapshot.Any(e=>e.State==EmployeeState.Blocked) ||
                customers!=null && customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked))
                kind=CafeServicePresentationKind.Blocked;
            else
            {
                var pending=traffic?.CurrentWorkPresentation;
                if(pending==TrafficDecisionKind.WaitingForTraffic)kind=CafeServicePresentationKind.Waiting;
                else if(pending==TrafficDecisionKind.DeferredByBudget)kind=CafeServicePresentationKind.Planning;
            }
            return new CafeServicePresentation(kind,paused,available);
        }
        public bool HoldCollection { get; set; }
        private readonly List<CafeTaskEvent> observedEvents=new List<CafeTaskEvent>();
        private readonly Dictionary<EmployeeRole,CafeTaskToken> latestTokens=new Dictionary<EmployeeRole,CafeTaskToken>();
        public CafeLoopViewSnapshot ViewSnapshot => new CafeLoopViewSnapshot(Snapshot,customers?.CounterAssignments,
            customers?.PickupAssignments,employees?.Snapshot,customers?.Capacity?.GetCapacities(),cups?.VisibleOrders.Count ?? 0,ReceptionStatus());
        public IReadOnlyList<CafeTaskEvent> RetiredTaskEvents => observedEvents.Where(IsRetired).ToList().AsReadOnly();
        public bool TryReplayRetiredTask(CafeTaskEvent value,out string reason)
        {
            reason="仅允许已观察且已退休的callback / Unknown or current task";
            if(service==null || !observedEvents.Contains(value) || !IsRetired(value)) return false;
            service.ReportTask(value); reason="旧callback已重放，当前身份仍受保护 / Retired callback replayed"; return true;
        }
        private bool IsRetired(CafeTaskEvent value) => value.Token.SessionId==Snapshot.SessionId &&
            (value.Token.Epoch<Snapshot.Epoch || latestTokens.TryGetValue(value.Role,out var token) && !token.Equals(value.Token) ||
                !Snapshot.Employees.Any(w=>w.Role==value.Role && w.VisitId==value.Checkpoint.VisitId));
        private string ReceptionStatus()
        {
            if(decoration!=null && decoration.IsOpen) return "Decoration / 装修中";
            if(layoutPending || !string.IsNullOrEmpty(lifecycleFailure)) return Snapshot.Status+" "+lifecycleFailure;
            if(held || !isActiveAndEnabled) return "Held / 协调器暂停";
            if(time==null) return "GameTime unavailable";
            if(time.IsResumeBlocked) return "Blocked / "+time.ResumeBlockReason;
            if(time.CurrentSpeed==GameSpeed.Paused) return "Paused / 玩家暂停";
            if(!comboReady)
            {
                var employeeFailure=employees?.Snapshot.FirstOrDefault(e=>!string.IsNullOrEmpty(e.Failure)).Failure;
                return !string.IsNullOrEmpty(employeeFailure)?employeeFailure:string.IsNullOrEmpty(readinessReason)?"等待完整服务组合":readinessReason;
            }
            if(Snapshot.Status=="Blocked") return "Blocked / "+Snapshot.Failure;
            if(!string.IsNullOrEmpty(Snapshot.Failure)) return "视觉待刷新 / "+Snapshot.Failure;
            if(!string.IsNullOrEmpty(pickupWaitReason) && customers.CounterAssignments.Any(q=>q.VisitId==pickupWaitVisit) &&
                !Snapshot.PaidVisitIds.Contains(pickupWaitVisit)) return "Waiting / "+pickupWaitReason;
            return (customers!=null && !customers.AutoSpawn?"Arrivals held / ":"营业中 / ")+ (customers?.Status ?? "");
        }
        public CafeLoopSnapshot Snapshot
        {
            get
            {
                var value=service?.Snapshot ?? new CafeLoopSnapshot("unconfigured",1);
                return new CafeLoopSnapshot(value.SessionId,value.Epoch,value.Orders,value.PaidVisitIds,
                    value.CollectedExitVisitIds,value.Delivery,Visits,value.Employees,
                    layoutPending?"装修清场验证中 / Validating changed layout":held?"Held":comboReady?value.Status:readinessReason,
                    string.IsNullOrEmpty(lifecycleFailure)?value.Failure:lifecycleFailure);
            }
        }
        public void Configure(CustomerFlowController customers, EmployeeFlowController employees, PickupCupDisplay cups,
            GameTimeService time, NavigationLayoutAdapter adapter, DecorationModeController decoration)
        {
            if(service!=null && this.customers==customers && this.employees==employees && this.cups==cups &&
                this.time==time && this.adapter==adapter && this.decoration==decoration) { Wire(); return; }
            if(service!=null && (this.customers?.Snapshot.Count>0 || service.Snapshot.Orders.Count>0))
                throw new InvalidOperationException("Cannot replace an active cafe session.");
            Unwire();traffic?.Close();traffic=null; service?.Dispose();
            this.customers=customers; this.employees=employees; this.cups=cups; this.time=time;
            this.adapter=adapter; this.decoration=decoration; pickupId=null; pickupRevision=-1;
            leaseActor=null; comboReady=false; held=false;
            pickupWaitReason=""; pickupWaitVisit=null; observedEvents.Clear(); latestTokens.Clear();
            if(time!=null && time.CurrentSpeed!=GameSpeed.Paused) lastRunningSpeed=time.CurrentSpeed;
            service=new CafeLoopService(Guid.NewGuid().ToString("N"),this); Wire();
        }
        private Func<string,CafeOrderKey,GameObject> collectionRecoveryProvider;
        private GameObject ReadReadyCollectionCup(string visitId,CafeOrderKey key)
        {
            var snapshot=service?.Snapshot;
            if(destroying || layoutPending || !isActiveAndEnabled || snapshot==null || cups==null ||
                snapshot.SessionId!=key.SessionId || snapshot.Epoch!=key.Epoch || !snapshot.PaidVisitIds.Contains(visitId) ||
                !snapshot.Orders.TryGetValue(key,out var order) || order.CustomerId!=visitId || order.State!=OrderState.ReadyForPickup ||
                !snapshot.Delivery.Any(d=>d.Key.Equals(key) && d.VisitId==visitId))return null;
            return cups.VisibleOrders.TryGetValue(key,out var cup) && cup!=null && cup.activeInHierarchy?cup:null;
        }
        private void Wire()
        {
            if(decoration!=null) { decoration.ChangedLayoutExitPreparing-=OnChangedLayoutExitPreparing; decoration.ChangedLayoutExitPreparing+=OnChangedLayoutExitPreparing; }
            GameEventBus.GameSpeedChanged-=OnGameSpeedChanged; GameEventBus.GameSpeedChanged+=OnGameSpeedChanged;
            customers?.SetIntegratedMode(true); customers?.SetIntegratedAdmissionAllowed(false);
            if(employees!=null)
            {
                employees.SetIntegratedMode(true);
                employees.IntegratedTaskEvent-=OnEmployeeTask; employees.IntegratedTaskEvent+=OnEmployeeTask;
                employees.SetDeliveryGate(AcquireDeliveryLease); employees.SetDeliveryCommit(CommitDelivery);
                customers?.SetPickupReturnTraffic(employees.GetPickupReturnTraffic);
                customers?.SetExpectedCounterDeparture(IsExpectedCounterDeparture);
                customers?.SetCounterDepartureTaskCapture(CaptureCounterDepartureTask);
                customers?.SetTrafficDeliveryGate(AcquireDeliveryLease);
            }
            cups?.SetIntegratedMode(true);
            if(collectionRecoveryProvider==null)collectionRecoveryProvider=ReadReadyCollectionCup;
            customers?.SetCollectionRecoveryCupProvider(collectionRecoveryProvider);
            if(businessDay!=null && businessDay.isActiveAndEnabled) RegisterBusinessDay(businessDay);
        }
        private void Unwire()
        {
            customers?.ClearCollectionRecoveryCupProvider(collectionRecoveryProvider);
            if(decoration!=null) decoration.ChangedLayoutExitPreparing-=OnChangedLayoutExitPreparing;
            GameEventBus.GameSpeedChanged-=OnGameSpeedChanged;
            if(employees==null) return;
            employees.IntegratedTaskEvent-=OnEmployeeTask;
            employees.SetDeliveryGate(null); employees.SetDeliveryCommit(null);
            customers?.SetPickupReturnTraffic(null);
            customers?.SetExpectedCounterDeparture(null);
            customers?.SetCounterDepartureTaskCapture(null);
            customers?.SetTrafficDeliveryGate(null);
        }
        private void PrepareTrafficFrame(float scaledDeltaTime)
        {
            if(service==null || customers==null || employees==null || adapter==null || adapter.World==null)return;
            var state=service.Snapshot;
            if(traffic==null || trafficEpoch!=state.Epoch || trafficRevision!=adapter.Revision)
            {
                traffic?.Close();trafficEpoch=state.Epoch;trafficRevision=adapter.Revision;
                traffic=new ServiceTrafficCoordinator(state.SessionId,state.Epoch,adapter.Revision,new TrafficWorldSnapshot(0,0,null,null),adapter.GetCompletePath,
                    getNativePathContext:()=>adapter.NativePathQueryContext);
                customers.BindTraffic(traffic,state.SessionId,state.Epoch,employees.IndependentTrafficSeconds,
                    id=>IsExpectedCounterDeparture(id)?Math.Max(.001f,employees.Snapshot.First(e=>e.Role==EmployeeRole.Cashier).RemainingWorkSeconds)+1:0,
                    employees.IsPickupJourneyCurrent);
            }
            var snapshot=customers.RefreshTrafficSnapshot();
            traffic.Tick(scaledDeltaTime,!CanProgress);
            if(CanProgress)customers.RefreshTrafficProposals(snapshot,employees);
            if(CanProgress)traffic.ProcessReady(Time.frameCount);
        }
        private Func<CafeOrderKey?,bool> CaptureCounterDepartureTask(string visitId)
        {
            var original=service;var staff=employees;
            if(!CanProgress || original==null || staff==null ||
                !original.TryCaptureCounterWorkingTask(visitId,out var token,out var actor,out var handle,out var read))return null;
            var life=staff.CaptureCashierTaskLife(visitId,token,actor,handle);if(life==null)return null;
            return paid=>CanProgress && ReferenceEquals(service,original) && ReferenceEquals(employees,staff) &&
                read(paid) && (paid.HasValue || life());
        }
        private bool IsExpectedCounterDeparture(string visitId) => CanProgress && service!=null &&
            !service.Snapshot.PaidVisitIds.Contains(visitId) &&
            (employees.IsCurrentCashierWorkingFor(visitId,service.Snapshot.SessionId,service.Snapshot.Epoch) ||
             service.HasQueuedCashierCompletion(visitId));
        private void OnEnable()
        {
            if(service==null && customers!=null && employees!=null) Configure(customers,employees,cups,time,adapter,decoration);
            if(businessDay!=null && businessDay.isActiveAndEnabled) RegisterBusinessDay(businessDay);
        }
        private void Update() => Step(Time.deltaTime);
        public void Step(float scaledDeltaTime)
        {
            if(float.IsNaN(scaledDeltaTime) || float.IsInfinity(scaledDeltaTime) || scaledDeltaTime<0)
                throw new ArgumentOutOfRangeException(nameof(scaledDeltaTime));
            if(stepping || destroying || !isActiveAndEnabled || service==null) return;
            stepping=true;
            try
            {
                // 既有Cafe Loop是唯一driver，现实截止必须在任何业务推进之前观察。
                businessDay?.BeforeService();
                CompleteLayoutLifecycle();
                if(layoutPending) { service.Tick(0); customers?.AcknowledgeExitSnapshots(); SnapshotChanged?.Invoke(Snapshot); return; }
                ReleaseDepartedDeliveryLease();
                UpdateCombo(); customers?.SetIntegratedAdmissionAllowed(CanProgress && admissionEnabled);
                PrepareTrafficFrame(scaledDeltaTime);
                if(scaledDeltaTime>0 && CanProgress)customers?.ProgressPaidPickupTasks();
                customers?.Step(scaledDeltaTime);
                UpdateCombo();
                if(scaledDeltaTime>0 && CanProgress) MoveReadyHeadToService();
                service.Tick(scaledDeltaTime); // Exit facts are consumed even while business is paused.
                LatchCanonicalPaidPickups(); // Same real commit frame, before external projections/listeners or later polling.
                customers?.AcknowledgeExitSnapshots();
                // Initial station registration is independent of the business combo gate.
                // CFC may publish a newer layout after PrepareTrafficFrame; wait for the next normal bind.
                // 本帧traffic过期时，等待正常driver重新绑定，再推进员工初始移动。
                if(!held && traffic!=null && adapter!=null && trafficEpoch==service.Snapshot.Epoch &&
                    trafficRevision==adapter.Revision) employees?.Step(scaledDeltaTime);
                UpdateCombo();
                SnapshotChanged?.Invoke(Snapshot);
            }
            finally
            {
                try
                {
                    businessDay?.AfterService(GetDrainSnapshot());
                }
                finally { stepping=false; }
            }
        }
        public BusinessDayDrainResult GetDrainSnapshot()
        {
            // 不使用CanProgress：Pause只停止Scene推进，不会抹去已排空的真实证据。
            if(destroying || held || !isActiveAndEnabled || service==null)
                return new BusinessDayDrainResult(false,"经营owner不可用 / Cafe owner unavailable");
            if(layoutPending || layoutFailed || !string.IsNullOrEmpty(lifecycleFailure) ||
                decoration==null || !decoration.isActiveAndEnabled || decoration.IsOpen ||
                adapter==null || !adapter.isActiveAndEnabled || adapter.World==null || !adapter.World.isActiveAndEnabled ||
                !adapter.World.LayoutAvailable || !adapter.CurrentReadiness.CanResume || adapter.PublishedRevision!=adapter.Revision ||
                time==null || !time.isActiveAndEnabled || time.IsResumeBlocked)
                return new BusinessDayDrainResult(false,"装修或布局生命周期尚未就绪 / Layout lifecycle unavailable: "+lifecycleFailure);
            if(customers==null || !customers.isActiveAndEnabled || employees==null || !employees.isActiveAndEnabled ||
                cups==null || !cups.isActiveAndEnabled || !comboReady)
                return new BusinessDayDrainResult(false,"服务配置不可用 / Service configuration unavailable");
            if(customers.Capacity==null)
                return new BusinessDayDrainResult(false,"容量ledger缺失 / Capacity ledger unavailable");
            var snapshot=service.Snapshot;
            if(snapshot.Delivery.Count>0)
                return new BusinessDayDrainResult(false,"逻辑Delivery尚未领取 / Logical delivery pending");
            if(cups.VisibleOrders.Count>0)
                return new BusinessDayDrainResult(false,"可见桌杯尚未清空 / Visible cups pending");
            if(customers.Snapshot.Count>0)
                return new BusinessDayDrainResult(false,"顾客尚未实际离店，包括disabled或Blocked / Live customer visits");
            if(customers.CounterAssignments.Count>0 || customers.PickupAssignments.Count>0)
                return new BusinessDayDrainResult(false,"Counter或Pick-up队伍尚未排空 / Queue assignments pending");
            var capacities=customers.Capacity.GetCapacities();
            if(capacities.Count!=3 || capacities.Select(value=>value.Kind).Distinct().Count()!=3 || capacities.Any(value=>value.Used!=0))
                return new BusinessDayDrainResult(false,"容量尚未释放 / Capacity reservations pending");
            if(service.HasPendingBusinessWork || !string.IsNullOrEmpty(leaseActor) || traffic?.HasPendingWork==true)
                return new BusinessDayDrainResult(false,"业务任务、callback或恢复尚未完成 / Pending business work");
            if(!employees.AreEmployeesAtWorkStations(out var reason)) return new BusinessDayDrainResult(false,reason);
            return new BusinessDayDrainResult(true,"");
        }
        private bool BaseGate => !destroying && !held && !layoutPending && isActiveAndEnabled && time!=null &&
            time.CurrentSpeed!=GameSpeed.Paused && !time.IsResumeBlocked &&
            (decoration==null || !decoration.IsOpen) && adapter!=null && adapter.isActiveAndEnabled &&
            adapter.World!=null && adapter.World.isActiveAndEnabled && adapter.World.LayoutAvailable &&
            adapter.CurrentReadiness.CanResume && adapter.PublishedRevision==adapter.Revision;
        private void UpdateCombo()
        {
            comboReady=false;
            if(customers==null || employees==null || !customers.isActiveAndEnabled || !employees.isActiveAndEnabled ||
                adapter==null || !employees.TryGetBinding(EmployeeRole.Cashier,out var cashier) ||
                !employees.TryGetBinding(EmployeeRole.Barista,out var barista) || cashier.LayoutRevision!=adapter.Revision ||
                barista.LayoutRevision!=adapter.Revision || cashier.WorkStationId!=customers.SelectedRegisterId ||
                !barista.PickUpTarget.HasValue || !barista.CupAddress.HasValue ||
                Actor(EmployeeRole.Cashier)==null || Actor(EmployeeRole.Barista)==null) return;
            if(pickupId!=barista.PickUpId || pickupRevision!=barista.LayoutRevision)
            {
                if(!customers.TryConfigurePickupFromLayout(barista.PickUpId,out readinessReason)) return;
                pickupId=barista.PickUpId; pickupRevision=barista.LayoutRevision;
            }
            customers.ConfigurePickupDelivery(EmployeeId(EmployeeRole.Barista),barista.WorkTarget);
            comboReady=true; readinessReason="";
        }
        public void ResumeBlockedFlow()
        {
            if(layoutPending || destroying || !isActiveAndEnabled || time==null || time.CurrentSpeed==GameSpeed.Paused ||
                time.IsResumeBlocked || decoration!=null && decoration.IsOpen || adapter==null || !adapter.World.LayoutAvailable) return;
            held=false;
            customers?.ResumeBlockedVisits(); employees?.ResumeIntegratedEmployees(); UpdateCombo();
            if(customers!=null && service!=null)
                foreach(var id in service.Snapshot.PaidVisitIds.ToArray())
                    if(TryGetActualPaidOrder(id,out var key))
                    {
                        customers.TryResumePaidPickup(id,key,out _);
                        customers.TryResumeHeldDeliveryYield(id,key,out _);
                    }
            service?.ResumeBlocked();
            SnapshotChanged?.Invoke(Snapshot);
        }
        private void MoveReadyHeadToService()
        {
            if(HoldCollection || customers.PickupAssignments.Count==0) return;
            var head=customers.PickupAssignments[0].VisitId;
            var ready=service.Snapshot.Orders.Values.Any(o=>o.CustomerId==head && o.State==OrderState.ReadyForPickup);
            if(ready) customers.TryMoveToPickupService(head,out _);
        }
        private NavigationActor Actor(EmployeeRole role)
        {
            var id=EmployeeId(role);
            return adapter?.World?.RegisteredActors.FirstOrDefault(a=>a!=null && a.ActorId==id && a.isActiveAndEnabled);
        }
        public bool CanProgress => BaseGate && comboReady;
        public IReadOnlyList<CafeVisitSnapshot> Visits => customers?.CafeSnapshot ?? Array.Empty<CafeVisitSnapshot>();
        public bool IsEmployeeIdle(EmployeeRole role) => CanProgress && employees.Snapshot.Any(e=>e.Role==role && e.State==EmployeeState.Idle) && Actor(role)!=null;
        public string EmployeeId(EmployeeRole role) => employees?.Snapshot.FirstOrDefault(e=>e.Role==role).ActorId;
        public bool TryReservePickup(string visitId,out string reason)
        {
            reason="完整服务组合或进度门禁不可用"; if(!CanProgress) return false;
            var reserved=customers.TryReservePickup(visitId,out reason);
            pickupWaitReason=reserved?"":reason; pickupWaitVisit=reserved?null:visitId; return reserved;
        }
        public void ReleasePickupAssignment(string visitId) => customers?.ReleasePickupAssignment(visitId);
        public bool TryMoveToPickup(string visitId,out string reason)
        {
            reason="当前实际付款身份无效 / No current paid order";
            if(!TryGetActualPaidOrder(visitId,out var key))return false;
            if(!customers.HasTrafficCoordinator)return CanProgress && customers.TryMoveToPickup(visitId,out reason);
            // Latch the real business checkpoint before a reentrant Pause/progress gate can defer movement.
            if(!customers.LatchPaidPickupCheckpoint(visitId,key,Time.frameCount))return false;
            return customers.TryMovePaidToPickup(visitId,key,CanProgress,out reason);
        }
        private bool TryGetActualPaidOrder(string visitId,out CafeOrderKey key)
        {
            key=default;var actual=service?.Snapshot;
            if(actual==null || customers==null || !actual.PaidVisitIds.Contains(visitId))return false;
            var matches=actual.Orders.Where(p=>p.Value.CustomerId==visitId && p.Value.State!=OrderState.Completed &&
                p.Key.SessionId==actual.SessionId && p.Key.Epoch==actual.Epoch && p.Key.OrderId==p.Value.OrderId).ToArray();
            if(matches.Length!=1)return false;key=matches[0].Key;return true;
        }
        private void LatchCanonicalPaidPickups()
        {
            if(service==null || customers==null || !customers.HasTrafficCoordinator)return;
            foreach(var id in service.Snapshot.PaidVisitIds)
                if(TryGetActualPaidOrder(id,out var key))customers.LatchPaidPickupCheckpoint(id,key,Time.frameCount);
        }
        public bool TryMoveToExit(string visitId,bool hasCollected,out string reason)
        {
            if(hasCollected)LatchCanonicalCollections(); // Includes a same-frame coalesced projection, before movement/presentation.
            reason="P14暂停"; return CanProgress && customers.TryMoveToExit(visitId,hasCollected,out reason);
        }
        public bool TryStartWork(CafeTaskToken token,CafeWorkCheckpoint checkpoint,out string reason)
        { reason="P14暂停"; return CanProgress && employees.TryStartOrderTask(token,checkpoint,out reason); }
        public bool IsCurrentDeliveryArrival(CafeTaskEvent arrival,SurfaceSlotAddress address) => CanProgress &&
            leaseActor==arrival.ActorId && employees.IsCurrentDeliveryArrival(arrival,address);
        public bool CanCollect(string visitId,CafeOrderKey key)
        {
            if(!CanProgress || HoldCollection || !customers.CanCollectPickup(visitId))return false;
            var actual=service?.Snapshot;
            if(actual==null || !actual.Orders.TryGetValue(key,out var order) || order.CustomerId!=visitId || order.State!=OrderState.ReadyForPickup)return false;
            customers.RememberCollectionAttempt(visitId,key,Time.frameCount);return true;
        }
        private void LatchCanonicalCollections()
        {
            if(service==null || customers==null)return;
            var actual=service.Snapshot;
            foreach(var order in actual.Orders.Where(p=>p.Value.State==OrderState.Completed && actual.CollectedExitVisitIds.Contains(p.Value.CustomerId)))
                customers.LatchCanonicalCollection(order.Value.CustomerId,order.Key,Time.frameCount);
        }
        public void ProjectDeliveries(IReadOnlyList<CafeDeliverySnapshot> deliveries)
        {
            LatchCanonicalCollections(); // Internal canonical fact first; cup projection may synchronously reenter Pause/Done.
            if(cups==null) throw new InvalidOperationException("Pickup cup display unavailable");
            if(!cups.TryProject(deliveries,out var reason)) throw new InvalidOperationException(reason);
        }
        private void OnEmployeeTask(CafeTaskEvent value)
        {
            var trafficContext=value.Token.SessionId+"/"+value.Token.Epoch+"/"+value.Token.Sequence+"/"+value.Token.Attempt+"/handle/"+value.HandleId;
            if(value.Kind==CafeTaskEventKind.DeliveryArrived)customers?.CompleteEmployeeTraffic(value.ActorId,trafficContext,false);
            if(value.Kind==CafeTaskEventKind.Returned || value.Kind==CafeTaskEventKind.Blocked)customers?.CompleteEmployeeTraffic(value.ActorId,trafficContext,true);
            if(!latestTokens.TryGetValue(value.Role,out var prior) || !prior.Equals(value.Token)) latestTokens[value.Role]=value.Token;
            if((value.Kind!=CafeTaskEventKind.Progress || value.Checkpoint.Stage!=CafeWorkStage.Working) && !observedEvents.Contains(value))
            { observedEvents.Add(value); if(observedEvents.Count>64) observedEvents.RemoveAt(0); }
            if(value.Kind==CafeTaskEventKind.DeliveryArrived && value.ActorId==leaseActor &&
                employees.TryGetBinding(EmployeeRole.Barista,out var arrivedBinding) && arrivedBinding.CupAddress.HasValue &&
                IsCurrentDeliveryArrival(value,arrivedBinding.CupAddress.Value)) leaseArrived=true;
            service?.ReportTask(value);
        }
        private bool CommitDelivery(CafeTaskEvent arrival,SurfaceSlotAddress address,out string reason)
        { reason="Cafe session unavailable"; return service!=null && service.TryCommitDelivery(arrival,address,out reason); }
        private bool AcquireDeliveryLease(string actorId)
        {
            if(!CanProgress || !employees.TryGetBinding(EmployeeRole.Barista,out var binding)) return false;
            customers.SetPickupDeliveryAttempt(employees.Snapshot.First(e=>e.ActorId==actorId).HandleId);
            if(traffic!=null)
            {
                if(!employees.TryCapturePreparedDeliveryContext(actorId,out var context))return false;
                if(!customers.TryPrepareDeliveryTraffic(context,out var decision,out var queueMutation,
                    out var trafficTarget,out var outward,out var home))
                {employees.BlockPreparedDelivery(actorId,customers.PickupDeliveryFailure);return false;}
                if(!employees.IsCurrentPreparedDeliveryContext(context) ||
                    !employees.TryPreparePickupTrafficRoutes(actorId,trafficTarget,outward,home,out var employeeMutation) ||
                    !customers.CommitDeliveryTraffic(decision,queueMutation,employeeMutation,trafficTarget,outward,home))return false;
                if(leaseActor!=actorId){leaseActor=actorId;leasePosition=trafficTarget.Position;leaseArrived=false;}
                return true;
            }
            if(!customers.TryAcquirePickupDelivery(actorId,out var selected,out var route,out var returning))
            { employees.BlockPreparedDelivery(actorId,customers.PickupDeliveryFailure);return false; }
            if(!employees.TrySelectPickupRoutes(actorId,selected,route,returning))
            { customers.ReleasePickupServicePosition(actorId); return false; }
            if(leaseActor!=actorId) { leaseActor=actorId; leasePosition=selected.Position; leaseArrived=false; }
            return true;
        }
        private void ReleaseDepartedDeliveryLease()
        {
            if(string.IsNullOrEmpty(leaseActor) || adapter?.World==null) return;
            var actor=adapter.World.RegisteredActors.FirstOrDefault(a=>a!=null && a.ActorId==leaseActor);
            var state=employees?.Snapshot.FirstOrDefault(e=>e.ActorId==leaseActor) ?? default;
            var clear=actor==null || Vector3.Distance(actor.transform.position,leasePosition)>
                2*actor.Settings.AgentRadius+actor.Settings.CollisionSkin+actor.Settings.Epsilon+actor.Settings.ArrivalDistance;
            if(!clear || !leaseArrived && !(state.HandleId==0 && (state.State==EmployeeState.Blocked || state.State==EmployeeState.Disabled))) return;
            customers?.ReleasePickupServicePosition(leaseActor); leaseActor=null; leaseArrived=false;
        }
        private void OnDisable()
        {
            if(businessDay!=null)
            { customers?.UnregisterAdmissionPolicy(businessDay); admissionEnabled=false; }
            held=true; customers?.SetIntegratedAdmissionAllowed(false);
            customers?.HoldIntegratedVisits(); employees?.HoldIntegratedTasks();
            SnapshotChanged?.Invoke(Snapshot);
        }
        private void OnDestroy()
        {
            if(businessDay!=null) customers?.UnregisterAdmissionPolicy(businessDay);
            businessDay=null;
            businessDayRecorder=null;
            destroying=true; Unwire();traffic?.Close();traffic=null; service?.Dispose(); service=null;
            layoutBlock?.Dispose(); layoutBlock=null;
        }
        private void OnGameSpeedChanged(GameSpeedChangedEvent value)
        {
            if(time!=null && value.Current!=GameSpeed.Paused && time.CurrentSpeed==value.Current)
                lastRunningSpeed=value.Current;
        }
        private void OnChangedLayoutExitPreparing(long version)
        {
            if(destroying || service==null || version<=preparingVersion) return;
            LatchCanonicalCollections(); // Preserve any real coalesced collection before the old order epoch is cleared.
            // Acquire synchronously publishes availability; the local gate must exist first.
            layoutPending=true; cleanupVerified=false; layoutFailed=false; preparingVersion=version;
            traffic?.Close();
            try
            {
                // 必须先记账，再触发同步resume listener与旧epoch清场；disabled recorder仍属原Service。
                if(businessDayRecorder!=null) businessDayRecorder.RecordLayoutInterruption(version);
                else if(customers!=null && customers.RequiresAdmissionPolicy)
                    throw new InvalidOperationException("Business Day recorder不可用，无法确认装修中断。");
                if(time==null) throw new InvalidOperationException("Game time unavailable during cleanup");
                if(layoutBlock==null) layoutBlock=time.AcquireResumeBlock(this,"正在清理旧营业批次 / Cafe cleanup pending");
                var collected=service.Snapshot.CollectedExitVisitIds.ToArray();
                service.ClearOrdersForDecoration(version); // Retire epoch before any actor cleanup callback.
                // Old queue ownership cannot cross epochs, even while the same employee is carrying.
                customers?.ReleasePickupServicePosition(leaseActor);
                leaseActor=null; leaseArrived=false; leasePosition=default;
                if(customers==null || employees==null || cups==null) throw new InvalidOperationException("Cafe cleanup owner unavailable");
                employees.DiscardIntegratedTasksForLayout();
                customers.ClearUncollectedVisits(collected);
                pickupId=null; pickupRevision=-1; comboReady=false;
                pickupWaitReason=""; pickupWaitVisit=null;
                // All expected side effects must be verified before releasing our owned gate.
                if(service.Snapshot.Orders.Count!=0 || service.Snapshot.Delivery.Count!=0 ||
                    cups.VisibleOrders.Count!=0 || customers.CafeSnapshot.Any(v=>!v.ExitArrived && !collected.Contains(v.VisitId)) ||
                    employees.Snapshot.Any(e=>e.HandleId!=0) ||
                    employees.TryCaptureOrderCheckpoint(EmployeeRole.Cashier,out _) || employees.TryCaptureOrderCheckpoint(EmployeeRole.Barista,out _))
                    throw new InvalidOperationException("Cafe cleanup could not be verified");
                // MainCafe starts empty: install the existing validation policy before PrepareExit.
                // Configuration and validation may run while paused; ordinary movement remains gated.
                if(adapter==null) throw new InvalidOperationException("Navigation adapter unavailable during cleanup");
                adapter.RequireBusinessReadiness();
                var bridge=adapter.GetComponent<NavigationDecorationBridge>();
                if(bridge==null) throw new InvalidOperationException("Navigation bridge unavailable during cleanup");
                bridge.Configure(adapter); bridge.ConfigureTime(time);
                cleanupVerified=true; lifecycleFailure="";
            }
            catch(Exception exception)
            { layoutFailed=true; lifecycleFailure="装修清场失败 / Cafe cleanup failed: "+exception.Message; }
        }
        private void CompleteLayoutLifecycle()
        {
            if(!layoutPending || layoutFailed || !cleanupVerified || time==null || decoration==null || decoration.IsOpen ||
                decoration.CompletedLayoutChangeVersion<preparingVersion || adapter==null || !adapter.isActiveAndEnabled ||
                adapter.World==null || !adapter.World.isActiveAndEnabled || !adapter.World.LayoutAvailable ||
                !adapter.CurrentReadiness.CanResume || adapter.PublishedRevision!=adapter.Revision) return;
            // Keep the local gate during Dispose's synchronous listeners. Other owners remain untouched.
            var owned=layoutBlock; layoutBlock=null; owned?.Dispose();
            if(time.IsResumeBlocked) return;
            if(decoration.HasPendingSpeedRestore) decoration.TryRestorePendingSpeed();
            if(decoration.HasPendingSpeedRestore) return;
            if(time.LastExplicitSpeed!=GameSpeed.Paused && time.CurrentSpeed==GameSpeed.Paused &&
                !time.TrySetAutomaticSpeed(lastRunningSpeed)) return;
            layoutPending=false;
        }
    }
}

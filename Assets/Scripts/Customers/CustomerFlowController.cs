using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using AnimalCafe.Navigation;
using AnimalCafe.Core.Time;
using AnimalCafe.Capacity;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.CafeLoop;
namespace AnimalCafe.Customers
{
    public sealed class CustomerFlowController : MonoBehaviour
    {
        [SerializeField] private NavigationWorld world;
        [SerializeField] private NavigationLayoutAdapter adapter;
        [SerializeField] private GameTimeService time;
        [SerializeField] private NavigationActor[] prefabs;
        [SerializeField] private Transform spawn,exit,validationHead,gridRoot;
        [SerializeField] private CafeLayoutRuntime runtime;
        [SerializeField] private DecorationModeController decoration;
        [SerializeField] private bool autoSpawn=true;
        [SerializeField] private bool integratedMode;
        [SerializeField] private GameObject collectedCupPrefab;
        [SerializeField] private CustomerCarryPresenter.Appearance[] collectedCarryAppearances=Array.Empty<CustomerCarryPresenter.Appearance>();
        private sealed class Visit
        {
            public string Id; public NavigationActor Actor; public CustomerAdmission Admission;
            public CustomerVisitState State,ResumeState; public long RequestId;
            public int Generation; public QueueAssignment Assignment; public Vector3 CounterPosition,AdvanceStart;
            public bool QueueLeft;
            public bool CounterArrived,PickupArrived,PickupDeparted,HasCollected,PickupAssigned,PickupInTransit,ServiceArrived;
            public Vector3 PickupPosition;
            public PickupQueueService PickupOwner;
            public NavigationTarget FinalTarget;
            public NavigationTarget CurrentSegmentTarget;
            public Queue<Vector3> Route;
            public int Segments,Replans;
            public bool PendingExitSegment;
            public bool PendingPickupSegment;
            public int DeliveryYieldPhase; // 1 moving out, 2 held clear, 3 returning to the owned slot.
            public IReadOnlyList<Vector3>[] YieldReturnTraffic;
            public long ExitSequence;
            public string Failure;
        }
        private readonly Dictionary<string,Visit> visits=new Dictionary<string,Visit>();
        private CustomerSpawnClock clock;
        private CounterQueueService queue;
        private int revision=-1;
        private long decorationVersion;
        private bool suspended,disposed,recoveringQueue,admissionRejected,admissionRouteUnavailable;
        private string session;
        private long nextVisit;
        private long nextExitSequence;
        private Vector3 queueFacing;
        private Vector3[] counterWaitSlots=Array.Empty<Vector3>();
        private PickupQueueService pickupQueue;
        private readonly List<QueueForwardRejection> pickupPlanningRejections=new List<QueueForwardRejection>();
        internal IReadOnlyList<QueueForwardRejection> PickupPlanningRejections=>pickupPlanningRejections.AsReadOnly();
        private Vector3 pickupCustomer,pickupEmployee,pickupOutward;
        private string selectedPickupId;
        private bool pickupGeometryConfigured,integratedHeld,multiSidePickup;
        private NavigationTarget[] pickupSides=Array.Empty<NavigationTarget>();
        private string deliveryActorId;
        private NavigationTarget deliveryWorkTarget;
        private long deliveryAttempt;
        private int deliveryYieldCount;
        private bool deliveryAcquired,deliveryWasAway;
        private IReadOnlyList<Vector3>[] deliveryTrafficPaths;
        private Func<string,IReadOnlyList<NavigationTarget>> pickupReturnTraffic;
        internal void SetPickupReturnTraffic(Func<string,IReadOnlyList<NavigationTarget>> provider) => pickupReturnTraffic=provider;
        private Func<string,bool> expectedCounterDeparture;
        internal void SetExpectedCounterDeparture(Func<string,bool> provider) => expectedCounterDeparture=provider;
        internal string PickupDeliveryFailure { get; private set; }="";
        internal void ConfigurePickupDelivery(string actorId,NavigationTarget workTarget)
        { deliveryActorId=actorId; deliveryWorkTarget=workTarget; }
        internal void SetPickupDeliveryAttempt(long handle)
        {
            if(deliveryAttempt==handle) return;
            deliveryAttempt=handle;deliveryYieldCount=0;PickupDeliveryFailure="";deliveryTrafficPaths=null;
        }
        private readonly List<CafeVisitSnapshot> exitSnapshots=new List<CafeVisitSnapshot>();
        public bool AutoSpawn { get=>autoSpawn; set=>autoSpawn=value; }
        public CapacityService Capacity { get; private set; }
        public string Status { get; private set; }="等待营业布局";
        public string SelectedRegisterId { get; private set; }="";
        public float RemainingSeconds => clock?.RemainingSeconds ?? 0;
        public void SetIntegratedMode(bool enabled)
        {
            integratedMode=enabled;
            if(enabled && runtime!=null) { runtime.EnableMultiSidePickupReadiness(); adapter?.EnableMultiSidePickupReadiness(); }
        }
        public void ConfigureCollectedCarry(GameObject cupPrefab,CustomerCarryPresenter.Appearance[] appearances)
        {
            if(visits.Count!=0) throw new InvalidOperationException("Configure customer Carry before admitting visits.");
            collectedCupPrefab=cupPrefab;
            collectedCarryAppearances=appearances ?? Array.Empty<CustomerCarryPresenter.Appearance>();
        }
        private bool integratedAdmissionAllowed=true;
        public void SetIntegratedAdmissionAllowed(bool allowed) { integratedAdmissionAllowed=allowed; }
        public IReadOnlyList<CafeVisitSnapshot> CafeSnapshot => visits.Values.Select(v=>CafeVisit(v,false))
            .Concat(exitSnapshots).OrderBy(v=>v.VisitId,StringComparer.Ordinal).ToList().AsReadOnly();
        public IReadOnlyList<QueueAssignment> PickupAssignments => pickupQueue?.Snapshot ?? Array.Empty<QueueAssignment>();
        public IReadOnlyList<QueueAssignment> CounterAssignments => queue?.Snapshot.ToList().AsReadOnly() ?? (IReadOnlyList<QueueAssignment>)Array.Empty<QueueAssignment>();
        private CafeVisitSnapshot CafeVisit(Visit visit,bool exited) => new CafeVisitSnapshot(visit.Id,visit.State,
            visit.Actor==null?exit.position:visit.Actor.transform.position,visit.CounterArrived,visit.PickupArrived,
            visit.PickupDeparted,exited,!exited && visit.Actor!=null && visit.Actor.isActiveAndEnabled,adapter?.Revision ?? -1);
        private bool CanIntegratedProgress => integratedMode && !integratedHeld && isActiveAndEnabled && Configured &&
            world.isActiveAndEnabled && adapter.isActiveAndEnabled && world.LayoutAvailable &&
            time.CurrentSpeed!=GameSpeed.Paused && !time.IsResumeBlocked && (decoration==null || !decoration.IsOpen);

        // 正式选中ID和无business runtime的独立场景共用同一geometry入口。
        public void ConfigurePickupGeometry(string pickupId,Vector3 customerPosition,Vector3 employeePosition,Vector3 outward)
        {
            if(string.IsNullOrWhiteSpace(pickupId) || pickupId!=pickupId.Trim()) throw new ArgumentException(nameof(pickupId));
            if(!Finite(customerPosition) || !Finite(employeePosition) || !Finite(outward) ||
                Mathf.Abs(outward.y)>.001f || outward.sqrMagnitude<.01f) throw new ArgumentOutOfRangeException(nameof(outward));
            if(visits.Values.Any(v=>v.PickupAssigned && !v.HasCollected))
                throw new InvalidOperationException("Retire uncollected pickup assignments before changing geometry.");
            selectedPickupId=pickupId; pickupCustomer=customerPosition; pickupEmployee=employeePosition;
            pickupOutward=outward.normalized; pickupGeometryConfigured=true; pickupQueue=null;pickupPlanningRejections.Clear();
            multiSidePickup=false; pickupSides=new[]{new NavigationTarget(customerPosition,-pickupOutward)};
        }
        public bool TryConfigurePickupFromLayout(string pickupId,out string reason)
        {
            reason="Pick-up正式anchors不可用";
            if(runtime?.CurrentReadiness==null || gridRoot==null || runtime.Layout==null) return false;
            var station=runtime.CurrentReadiness.Stations.Where(s=>s.IsValid && s.FunctionType==LayoutStationType.PickUpPoint &&
                (string.IsNullOrEmpty(pickupId) || s.InstanceId==pickupId)).OrderBy(s=>s.InstanceId,StringComparer.Ordinal).FirstOrDefault();
            if(station==null || !station.Anchors.TryGetAnchor(InteractionRole.Customer,out var customer) ||
                !station.Anchors.TryGetAnchor(InteractionRole.Employee,out var employee)) return false;
            if(visits.Values.Any(v=>v.PickupAssigned && !v.HasCollected)) { reason="已有Pick-up分配"; return false; }
            var candidates=GetPickupServiceTargets(station.InstanceId);
            if(candidates.Count==0) return false;
            ConfigurePickupGeometry(station.InstanceId,WorldAnchor(customer),WorldAnchor(employee),-Direction(customer.Facing));
            // Keep the legacy customer side first, then all other exposed sides in cardinal order.
            pickupSides=candidates.OrderBy(t=>t.Position==pickupCustomer?0:1).ToArray(); multiSidePickup=true;
            reason=""; return true;
        }
        // Readonly formal geometry; both employee binding and customer routing use the same candidates.
        public IReadOnlyList<NavigationTarget> GetPickupServiceTargets(string pickupId)
        {
            if(runtime?.FunctionalSurfaceLayout==null || runtime.SurfaceSlotCatalog==null || gridRoot==null || adapter==null)
                return Array.Empty<NavigationTarget>();
            var pickup=runtime.FunctionalSurfaceLayout.PickUpPoints.FirstOrDefault(p=>p.InstanceId==pickupId);
            if(pickup==null) return Array.Empty<NavigationTarget>();
            var anchors=new InteractionAnchorResolver().ResolvePickUpCandidates(pickup,runtime.Layout,runtime.SurfaceSlotCatalog,
                p=>runtime.Layout.IsInsideUnlockedRegion(p) && !runtime.Layout.HasReservation(p,LayoutReservationType.Blocked) && !runtime.Layout.TryGetOccupant(p,out _));
            return anchors.Select(a=>new NavigationTarget(WorldAnchor(a),Direction(a.Facing)))
                .Where(t=>adapter.IsPointWalkable(t.Position)).ToArray();
        }
        private bool EnsurePickupQueue(Visit visit,out string reason)
        {
            reason="没有合法且完整可达的Pick-up等候位";
            if(!pickupGeometryConfigured && !TryConfigurePickupFromLayout(selectedPickupId,out reason)) return false;
            if(pickupQueue!=null) return true;
            var clearance=prefabs.Max(p=>2*p.Settings.AgentRadius+p.Settings.CollisionSkin+p.Settings.Epsilon+p.Settings.ArrivalDistance);
            var corridors=PickupDeliveryCorridors();
            if(corridors!=null && corridors.Count==0) { reason="送杯通道堵塞，请调整布局 / No complete delivery and return corridor"; return false; }
            foreach(var candidate in pickupSides)
            {
                var customer=candidate.Position; var outward=-candidate.Facing.Value;
                var shared=multiSidePickup || Vector3.Distance(customer,pickupEmployee)<clearance;
                var initialHead=shared?customer+outward*1.1f:customer;
                foreach(var head in PickupWaitHeads(initialHead,outward,corridors!=null))
                {
                pickupPlanningRejections.Clear();
                var planner=new CounterQueuePlanner(1.1f,preserveStraightPrefix:true);
                planner.RejectedForward=(point,previous,direction,prefix,failure)=>{
                    if(pickupPlanningRejections.Count<256)
                        pickupPlanningRejections.Add(new QueueForwardRejection(point,previous,direction,prefix,failure));
                };
                var slots=planner.Build(head,outward,Capacity.Limits.PickUp,(point,previous)=>
                    adapter.IsPointWalkable(point) && !InEntrance(point) && Vector3.Distance(point,exit.position)>=1.2f &&
                    (!shared || Vector3.Distance(point,customer)>=clearance) &&
                    (!multiSidePickup || pickupSides.All(side=>Vector3.Distance(point,side.Position)>=clearance)) &&
                    counterWaitSlots.All(p=>Vector3.Distance(p,point)>=clearance) &&
                    world.RegisteredActors.All(actor=>actor==null || Vector3.Distance(actor.transform.position,point)>=clearance) &&
                    (previous==null || adapter.HasClearPath(previous.Value,point)) &&
                    adapter.HasClearPath(point,customer) && adapter.HasClearPath(customer,exit.position) && (multiSidePickup?pickupSides.Any(side=>adapter.HasClearPath(side.Position,point)):adapter.HasClearPath(pickupEmployee,point)),
                    (point,prefix)=>(corridors==null || corridors.Any(path=>CustomerRoutePlanner.IsClear(path,prefix.Concat(new[]{point}).ToArray(),clearance))) &&
                        new CustomerRoutePlanner().Build(visit.Actor.transform.position,point,
                        RouteObstacles(visit.Id).Concat(prefix).ToArray(),RouteClearance(visit.Actor.Settings),adapter.GetCompletePath)!=null &&
                        PickupPlanCanExit(visit,head,prefix.Concat(new[]{point}).Skip(1).ToArray()),
                    (from,to)=>{var path=adapter.GetCompletePath(from,to); return path==null || path.Count==0?(Vector3?)null:path[path.Count-1];});
                if(slots.Count==0) continue;
                pickupCustomer=customer; pickupOutward=outward;
                pickupQueue=multiSidePickup?new PickupQueueService(slots,pickupSides.Select(t=>t.Position).ToArray()):new PickupQueueService(slots,customer,shared);
                reason=""; return true;
                }
            }
            return false;
        }
        private IEnumerable<Vector3> PickupWaitHeads(Vector3 head,Vector3 outward,bool reserveCorridor)
        {
            yield return head;
            if(!reserveCorridor) yield break;
            var lateral=new Vector3(-outward.z,0,outward.x);
            // A serviceable narrow aisle may need an offset head, never a hidden extra queue.
            // 有限偏移仍是同一条已计容量的队列，每个目标沿原全部安全门禁验证。
            foreach(var distance in new[]{1.1f,2.2f})
            { yield return head+lateral*distance; yield return head-lateral*distance; }
        }
        private IReadOnlyList<IReadOnlyList<Vector3>> PickupDeliveryCorridors()
        {
            if(string.IsNullOrEmpty(deliveryActorId)) return null; // Legacy/component ports keep their original policy.
            var actor=world.RegisteredActors.FirstOrDefault(a=>a!=null && a.ActorId==deliveryActorId);
            var result=new List<IReadOnlyList<Vector3>>(); if(actor==null) return result;
            var occupied=RouteObstacles(deliveryActorId); var clearance=RouteClearance(actor.Settings);
            foreach(var side in pickupSides)
            {
                var outward=new CustomerRoutePlanner().Build(deliveryWorkTarget.Position,side.Position,occupied,clearance,adapter.GetCompletePath);
                var returning=new CustomerRoutePlanner().Build(side.Position,deliveryWorkTarget.Position,occupied,clearance,adapter.GetCompletePath);
                if(outward==null || returning==null) continue;
                var outPath=ExpandRoute(deliveryWorkTarget.Position,outward); var backPath=ExpandRoute(side.Position,returning);
                if(CustomerRoutePlanner.IsClear(outPath,occupied,clearance) && CustomerRoutePlanner.IsClear(backPath,occupied,clearance))
                    result.Add(outPath.Concat(backPath).ToArray());
            }
            return result;
        }
        public bool TryReservePickup(string visitId,out string reason)
        {
            reason="顾客尚未实际到收银台队首或P14暂停";
            if(!CanIntegratedProgress || !visits.TryGetValue(visitId??"",out var visit) || visit.HasCollected ||
                visit.Actor==null || !visit.Actor.isActiveAndEnabled) return false;
            if(visit.State!=CustomerVisitState.Queued || queue==null || queue.Snapshot.Count==0 ||
                queue.Snapshot[0].VisitId!=visitId || !queue.Snapshot[0].Arrived || !visit.CounterArrived) return false;
            if(visit.PickupAssigned)
            {
                reason="预留站位已不可达";
                if(!visit.PickupOwner.TryReserve(visitId,out var current) || !adapter.IsPointWalkable(current.Position) ||
                    BuildPickupWaitRoute(visit.Actor.transform.position,current.Position,visitId,visit.Actor.Settings)==null) return false;
                reason=""; return true;
            }
            if(visit.State!=CustomerVisitState.Queued || queue.Snapshot.Count==0 || queue.Snapshot[0].VisitId!=visitId ||
                !queue.Snapshot[0].Arrived || !EnsurePickupQueue(visit,out reason)) return false;
            reason="没有可用的安全Pick-up分配";
            if(!pickupQueue.TryReserve(visitId,out var assignment)) return false;
            // 未付款的下一站不能落在前客仍拥有的剩余路径上；先普通等候再开工。
            // Existing bounded paths and unchanged body clearance remain authoritative.
            var clearance=RouteClearance(visit.Actor.Settings)+2*visit.Actor.Settings.MaxSpeed*
                Mathf.Min(Time.deltaTime,visit.Actor.Settings.MaxSubstepSeconds*visit.Actor.Settings.MaxSubstepsPerFrame);
            foreach(var earlier in visits.Values.Where(v=>v.Id!=visitId && v.RequestId!=0 && v.Actor!=null &&
                v.Actor.isActiveAndEnabled && IsPickupMovement(v.State)))
            {
                var remaining=ExpandRoute(earlier.Actor.transform.position,new[]{earlier.Actor.Agent.destination}
                    .Concat(earlier.Route??Enumerable.Empty<Vector3>()));
                if(remaining==null || !CustomerRoutePlanner.IsClear(remaining,new[]{assignment.Position},clearance))
                { pickupQueue.Remove(visitId); reason="等待前客实际通过预留位 / Earlier pickup remaining route"; return false; }
            }
            if(BuildPickupWaitRoute(visit.Actor.transform.position,assignment.Position,visitId,visit.Actor.Settings)==null)
            { pickupQueue.Remove(visitId); return false; }
            visit.PickupAssigned=true; visit.PickupOwner=pickupQueue;
            reason=""; return true;
        }
        public bool TryMoveToPickup(string visitId,out string reason)
        {
            reason="缺少有效的已预留Pick-up站位";
            if(!CanIntegratedProgress || !visits.TryGetValue(visitId??"",out var visit) || !visit.PickupAssigned || visit.HasCollected) return false;
            if(visit.PickupInTransit) { reason=""; return visit.State!=CustomerVisitState.Blocked; }
            if(visit.State!=CustomerVisitState.Queued || queue.Snapshot.Count==0 || queue.Snapshot[0].VisitId!=visitId ||
                !visit.PickupOwner.TryBeginTransfer(visitId,out var assignment)) return false;
            visit.CounterPosition=visit.Actor.transform.position; visit.PickupInTransit=true; visit.Assignment=assignment;
            Send(visit,CustomerVisitState.MovingToPickup,PickupWaitTarget(assignment,visit.PickupOwner));
            reason=""; return true;
        }
        public bool TryMoveToPickupService(string visitId,out string reason)
        {
            reason="尚非已到位队首，或共享服务点暂被占用";
            if(!CanIntegratedProgress || !visits.TryGetValue(visitId??"",out var visit) || !visit.PickupAssigned ||
                visit.HasCollected || visit.State==CustomerVisitState.Blocked || visit.DeliveryYieldPhase==1 || visit.DeliveryYieldPhase==3) return false;
            if(visit.State==CustomerVisitState.MovingToPickupService || visit.State==CustomerVisitState.PickupService)
            { reason=""; return true; }
            if(visit.State!=CustomerVisitState.PickupWaiting) return false;
            foreach(var candidate in pickupSides.OrderBy(t=>t.Position==pickupCustomer?0:1))
            {
                var route=BuildRoute(visit.Actor.transform.position,candidate.Position,visit.Id,visit.Actor.Settings);
                if(route==null || route.Count==0 || route.Count>24 ||
                    !PickupDeliveryPathClear(visit.Actor,ExpandRoute(visit.Actor.transform.position,route),RouteObstacles(visit.Id),candidate.Position) ||
                    !PickupCollectionClearsReturn(visit,ExpandRoute(visit.Actor.transform.position,route),candidate.Position)) continue;
                var acquired=visit.DeliveryYieldPhase==2?visit.PickupOwner.TryAcquireYieldedCollection(visitId,candidate.Position):
                    visit.PickupOwner.TryAcquireCollection(visitId,candidate.Position);
                if(!acquired) continue;
                visit.DeliveryYieldPhase=0; // Same paid owner/slot; generation retires the obsolete yield-return callback.
                visit.Assignment=visit.PickupOwner.Snapshot.First(a=>a.VisitId==visitId); visit.ServiceArrived=false;
                // Consume exactly the full owned route checked before taking this side lease.
                // 不抢其他活动顾客的中间waypoint；拒绝候选只是普通等待，不退休FIFO。
                SetRouteContext(visit,CustomerVisitState.MovingToPickupService,candidate);
                visit.Route=new Queue<Vector3>(route); SendRouteSegment(visit,CustomerVisitState.MovingToPickupService);
                reason=""; return true;
            }
            return false;
        }
        private bool PickupCollectionClearsReturn(Visit visit,IReadOnlyList<Vector3> path,Vector3 goal)
        {
            var targets=pickupReturnTraffic?.Invoke(deliveryActorId);
            if(targets==null || targets.Count==0) return true;
            if(targets.Count<2) return false; // A non-null corridor must include origin and an owned target.
            var actor=world.RegisteredActors.FirstOrDefault(a=>a!=null && a.ActorId==deliveryActorId);
            if(actor==null) return false;
            var remaining=ExpandRoute(targets[0].Position,targets.Skip(1).Select(t=>t.Position));
            var clearance=RouteClearance(visit.Actor.Settings)+2*visit.Actor.Settings.MaxSpeed*
                Mathf.Min(Time.deltaTime,visit.Actor.Settings.MaxSubstepSeconds*visit.Actor.Settings.MaxSubstepsPerFrame);
            return remaining!=null && !RoutesConflict(path,remaining,clearance) && CustomerRoutePlanner.IsClear(remaining,new[]{goal},clearance);
        }
        public bool CanCollectPickup(string visitId)
        {
            if(!CanIntegratedProgress || !visits.TryGetValue(visitId??"",out var visit) || visit.HasCollected || !visit.ServiceArrived ||
                visit.State!=CustomerVisitState.PickupService || visit.Actor==null || !visit.Actor.isActiveAndEnabled) return false;
            var assignments=visit.PickupOwner.Snapshot;
            return assignments.Count>0 && assignments[0].VisitId==visitId && assignments[0].Arrived &&
                assignments[0].Generation==visit.Assignment.Generation &&
                Vector3.Distance(visit.Actor.transform.position,visit.Assignment.Position)<=visit.Actor.Settings.ArrivalDistance+.001f;
        }
        public bool TryMoveToExit(string visitId,bool hasCollected,out string reason)
        {
            reason="实际领取尚未确认或P14暂停";
            if(!CanIntegratedProgress || !visits.TryGetValue(visitId??"",out var visit) || !hasCollected) return false;
            if(visit.HasCollected) { reason=""; return visit.State==CustomerVisitState.Exiting; }
            if(!CanCollectPickup(visitId)) return false;
            ConfirmCollectedExit(visit); visit.PickupPosition=visit.Actor.transform.position;
            Send(visit,CustomerVisitState.Exiting,new NavigationTarget(exit.position)); reason=""; return true;
        }
        public bool TryAcquirePickupDelivery(string employeeId) => TryAcquirePickupDelivery(employeeId,out _);
        public bool TryAcquirePickupDelivery(string employeeId,out NavigationTarget selected) => TryAcquirePickupDelivery(employeeId,out selected,out _);
        public bool TryAcquirePickupDelivery(string employeeId,out NavigationTarget selected,out IReadOnlyList<NavigationTarget> route)
            => TryAcquirePickupDelivery(employeeId,out selected,out route,out _);
        internal bool TryAcquirePickupDelivery(string employeeId,out NavigationTarget selected,out IReadOnlyList<NavigationTarget> route,out IReadOnlyList<NavigationTarget> returning)
        {
            selected=default; route=null;returning=null;
            if(!CanIntegratedProgress || pickupQueue==null || string.IsNullOrWhiteSpace(employeeId)) return false;
            if(!multiSidePickup && string.IsNullOrEmpty(deliveryActorId))
            {
                var radius=prefabs.Max(p=>p.Settings.AgentRadius+p.Settings.CollisionSkin+p.Settings.Epsilon);
                if(world.RegisteredActors.Any(a=>a!=null && a.ActorId!=employeeId && Vector3.Distance(a.transform.position,pickupEmployee)<a.Settings.AgentRadius+radius)) return false;
                selected=new NavigationTarget(pickupEmployee);
                route=new[]{selected}; return pickupQueue.TryAcquireDelivery(employeeId);
            }
            var actor=world.RegisteredActors.FirstOrDefault(a=>a!=null && a.ActorId==employeeId && a.isActiveAndEnabled);
            if(actor==null) return false;
            if(!string.IsNullOrEmpty(PickupDeliveryFailure) || visits.Values.Any(v=>v.DeliveryYieldPhase==1 || v.DeliveryYieldPhase==3)) return false;
            if(deliveryTrafficPaths!=null)
            {
                if(HasDeliveryTraffic(actor,deliveryTrafficPaths)) return false;
                deliveryTrafficPaths=null; // Traffic ended/cancelled: make one fresh full decision, not a per-frame A* retry.
            }
            var held=pickupQueue.TryGetServiceLease(employeeId,out var oldPosition);
            var sides=multiSidePickup?pickupSides:new[]{new NavigationTarget(pickupEmployee)};
            var attemptedPaths=new List<IReadOnlyList<Vector3>>();
            foreach(var candidate in sides.OrderBy(t=>t.Position==pickupEmployee?0:1))
            {
                if(held && candidate.Position!=oldPosition) continue;
                var occupied=RouteObstacles(employeeId);
                var points=new[]{candidate.Position};
                var path=adapter.GetCompletePath(actor.transform.position,candidate.Position);
                if(path!=null) attemptedPaths.Add(path);
                if(!CustomerRoutePlanner.IsClear(path,occupied,RouteClearance(actor.Settings)))
                {
                    var planned=new CustomerRoutePlanner().Build(actor.transform.position,candidate.Position,occupied,
                        RouteClearance(actor.Settings),adapter.GetCompletePath);
                    if(planned==null || planned.Count==0 || planned.Count>24) continue;
                    points=planned.ToArray(); path=ExpandRoute(actor.transform.position,points);
                }
                if(path!=null) attemptedPaths.Add(path);
                if(!PickupDeliveryPathClear(actor,path,occupied,candidate.Position)) continue;
                IReadOnlyList<NavigationTarget> home=null;
                if(!string.IsNullOrEmpty(deliveryActorId) && (!TryPlanPickupReturn(employeeId,candidate.Position,deliveryWorkTarget,out home) || points.Length+home.Count>24)) continue;
                if(!pickupQueue.TryAcquireDelivery(employeeId,candidate.Position)) continue;
                selected=candidate;
                route=points.Select((p,i)=>i==points.Length-1?candidate:new NavigationTarget(p)).ToArray();
                returning=home;deliveryAcquired=true;return true;
            }
            PrepareDeliveryYield(actor,sides,attemptedPaths);
            return false;
        }
        internal bool HasPickupDeliveryContext => !string.IsNullOrEmpty(deliveryActorId);
        internal bool TryPlanPickupReturn(string actorId,Vector3 from,NavigationTarget target,out IReadOnlyList<NavigationTarget> route)
        {
            route=null;
            var actor=world.RegisteredActors.FirstOrDefault(a=>a!=null && a.ActorId==actorId && a.isActiveAndEnabled);
            if(!CanIntegratedProgress || actor==null) return false;
            var occupied=RouteObstacles(actorId);var path=adapter.GetCompletePath(from,target.Position);
            IReadOnlyList<Vector3> points=new[]{target.Position};
            if(!CustomerRoutePlanner.IsClear(path,occupied,RouteClearance(actor.Settings)))
            {
                points=new CustomerRoutePlanner().Build(from,target.Position,occupied,RouteClearance(actor.Settings),adapter.GetCompletePath);
                if(points==null || points.Count==0 || points.Count>24) return false;
                path=ExpandRoute(from,points);
            }
            if(!PickupDeliveryPathClear(actor,path,occupied,target.Position)) return false;
            route=points.Select((p,i)=>i==points.Count-1?target:new NavigationTarget(p)).ToArray();return true;
        }
        internal bool IsPickupReturnSegmentClear(string actorId,NavigationTarget target)
        {
            var actor=world.RegisteredActors.FirstOrDefault(a=>a!=null && a.ActorId==actorId && a.isActiveAndEnabled);
            return CanIntegratedProgress && actor!=null && PickupDeliveryPathClear(actor,
                adapter.GetCompletePath(actor.transform.position,target.Position),RouteObstacles(actorId),target.Position);
        }
        internal bool ShouldWaitForPickupReturn(string actorId,NavigationTarget target)
        {
            var actor=world.RegisteredActors.FirstOrDefault(a=>a!=null && a.ActorId==actorId && a.isActiveAndEnabled);
            if(!CanIntegratedProgress || actor==null) return false;
            var path=adapter.GetCompletePath(actor.transform.position,target.Position);
            // Only an unissued owned return leg may wait for relevant real active traffic.
            // 不重算路线、不忽略身体；交通结束后原完整MoveTo guard仍可明确拒绝。
            return path!=null && !PickupDeliveryPathClear(actor,path,RouteObstacles(actorId),target.Position) &&
                HasDeliveryTraffic(actor,new[]{path});
        }

        private bool IsSettledPickupWaiter(Visit visit) => visit.Actor!=null && visit.Actor.isActiveAndEnabled &&
            visit.State==CustomerVisitState.PickupWaiting && visit.PickupAssigned && !visit.HasCollected &&
            visit.RequestId==0 && !visit.PendingPickupSegment && visit.DeliveryYieldPhase==0 &&
            visit.PickupOwner.Snapshot.Any(a=>a.VisitId==visit.Id && a.Arrived && a.Generation==visit.Assignment.Generation) && !visit.PickupOwner.TryGetServiceLease(visit.Id,out _) &&
            Vector3.Distance(visit.Actor.transform.position,visit.Assignment.Position)<=visit.Actor.Settings.ArrivalDistance+.001f;

        private void PrepareDeliveryYield(NavigationActor actor,IReadOnlyList<NavigationTarget> sides,IReadOnlyList<IReadOnlyList<Vector3>> attemptedPaths)
        {
            var occupied=RouteObstacles(actor.ActorId);var clearance=RouteClearance(actor.Settings);
            var working=visits.Values.Where(IsExpectedCounterDeparture).ToArray();
            if(working.Length!=0)
            {
                // Classification only: a current finite cashier task can release its real
                // head body shortly. Never dispatch using this counterfactual route.
                var withoutWorking=occupied.Where(p=>!working.Any(v=>p==v.Actor.transform.position)).ToArray();
                foreach(var side in sides)
                {
                    var outward=new CustomerRoutePlanner().Build(actor.transform.position,side.Position,withoutWorking,clearance,adapter.GetCompletePath);
                    var home=new CustomerRoutePlanner().Build(side.Position,deliveryWorkTarget.Position,withoutWorking,clearance,adapter.GetCompletePath);
                    if(outward==null || home==null) continue;
                    var paths=new[]{ExpandRoute(actor.transform.position,outward),ExpandRoute(side.Position,home)};
                    if(paths.Any(p=>p==null || !CustomerRoutePlanner.IsClear(p,withoutWorking,clearance)) ||
                        !working.Any(v=>paths.Any(p=>!CustomerRoutePlanner.IsClear(p,new[]{v.Actor.transform.position},clearance)))) continue;
                    deliveryTrafficPaths=paths;return;
                }
            }
            var waiters=visits.Values.Where(IsSettledPickupWaiter).OrderBy(v=>v.Assignment.SlotIndex).ToArray();
            // Counterfactual is selection only. Never dispatch an employee through ignored bodies.
            var withoutWaiters=occupied.Where(p=>!waiters.Any(w=>p==w.Actor.transform.position || p==w.Assignment.Position)).ToArray();
            foreach(var waiter in waiters)
            {
                foreach(var side in sides)
                {
                    var outward=new CustomerRoutePlanner().Build(actor.transform.position,side.Position,withoutWaiters,clearance,adapter.GetCompletePath);
                    var home=new CustomerRoutePlanner().Build(side.Position,deliveryWorkTarget.Position,withoutWaiters,clearance,adapter.GetCompletePath);
                    if(outward==null || home==null) continue;
                    var corridor=ExpandRoute(actor.transform.position,outward);
                    var back=ExpandRoute(side.Position,home);
                    if(!CustomerRoutePlanner.IsClear(corridor,withoutWaiters,clearance) || !CustomerRoutePlanner.IsClear(back,withoutWaiters,clearance) ||
                        CustomerRoutePlanner.IsClear(corridor,new[]{waiter.Actor.transform.position},clearance) &&
                        CustomerRoutePlanner.IsClear(back,new[]{waiter.Actor.transform.position},clearance)) continue;
                    if(deliveryYieldCount>=2) break; // Small per-handle budget; never search combinations or retry forever.
                    foreach(var distance in new[]{1.1f,2.2f,3.3f})
                    for(var direction=0;direction<8;direction++)
                    {
                        var angle=direction*Mathf.PI/4;
                        var target=waiter.Actor.transform.position+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*distance;
                        if(!adapter.IsPointWalkable(target) || InEntrance(target) || Vector3.Distance(target,spawn.position)<clearance ||
                            Vector3.Distance(target,exit.position)<clearance ||
                            !CustomerRoutePlanner.IsClear(corridor,new[]{target},clearance) || !CustomerRoutePlanner.IsClear(back,new[]{target},clearance)) continue;
                        var route=BuildRoute(waiter.Actor.transform.position,target,waiter.Id,waiter.Actor.Settings);
                        if(route==null || !CustomerRoutePlanner.IsClear(ExpandRoute(waiter.Actor.transform.position,route),RouteObstacles(waiter.Id),RouteClearance(waiter.Actor.Settings)) ||
                            !CanBeginPickupAdvance(new QueueAssignment(waiter.Id,waiter.Assignment.SlotIndex,waiter.Assignment.Generation+1,target,false))) continue;
                        if(!waiter.PickupOwner.TryBeginYield(waiter.Id,target,out var assignment)) continue;
                        deliveryYieldCount++;waiter.Assignment=assignment;waiter.DeliveryYieldPhase=1;
                        SetRouteContext(waiter,CustomerVisitState.PickupAdvancing,new NavigationTarget(target));
                        waiter.Route=new Queue<Vector3>(route);SendRouteSegment(waiter,CustomerVisitState.PickupAdvancing);return;
                    }
                }
            }
            var trafficPaths=attemptedPaths.ToList();
            foreach(var side in sides)
            {
                var path=adapter.GetCompletePath(actor.transform.position,side.Position);
                var back=adapter.GetCompletePath(side.Position,deliveryWorkTarget.Position);
                if(path!=null) trafficPaths.Add(path);if(back!=null) trafficPaths.Add(back);
            }
            if(HasDeliveryTraffic(actor,trafficPaths)) {deliveryTrafficPaths=trafficPaths.Select(p=>(IReadOnlyList<Vector3>)p.ToArray()).ToArray();return;}
            PickupDeliveryFailure="送杯通道堵塞，请调整布局 / Delivery corridor blocked";
        }

        private bool HasDeliveryTraffic(NavigationActor requester,IReadOnlyList<IReadOnlyList<Vector3>> paths)
        {
            var clearance=RouteClearance(requester.Settings)+2*requester.Settings.MaxSpeed*
                Mathf.Min(Time.deltaTime,requester.Settings.MaxSubstepSeconds*requester.Settings.MaxSubstepsPerFrame);
            if(visits.Values.Where(IsExpectedCounterDeparture).Any(v=>paths.Any(p=>p!=null &&
                !CustomerRoutePlanner.IsClear(p,new[]{v.Actor.transform.position},clearance)))) return true;
            foreach(var other in world.RegisteredActors.Where(a=>a!=null && a!=requester && a.isActiveAndEnabled))
            {
                var agent=other.Agent;
                if(agent==null || !agent.enabled || !agent.isOnNavMesh) continue;
                IReadOnlyList<Vector3> remaining;
                if(visits.TryGetValue(other.ActorId,out var moving))
                {
                    // SteadyPathMotion owns complete corners even when the native
                    // Agent has consumed its path. Only this live accepted leg is traffic.
                    // 只认尚未退休的真实request；pending0/静止标签不能无限等候。
                    if(moving.RequestId!=0 && (IsPickupMovement(moving.State) || moving.State==CustomerVisitState.Entering ||
                        moving.State==CustomerVisitState.Advancing || moving.State==CustomerVisitState.Exiting))
                    {
                        if(moving.State==CustomerVisitState.Exiting && moving.HasCollected && moving.PickupOwner!=null &&
                            moving.PickupOwner.TryGetServiceLease(moving.Id,out var lease) && paths.Any(path=>path!=null &&
                                !CustomerRoutePlanner.IsClear(path,new[]{lease},clearance))) return true;
                        remaining=ExpandRoute(other.transform.position,new[]{moving.CurrentSegmentTarget.Position}.Concat(moving.Route??Enumerable.Empty<Vector3>()));
                    }
                    else if(moving.RequestId==0 && moving.State==CustomerVisitState.Exiting && moving.PendingExitSegment &&
                        (CanWaitForDepartingExitBody(moving) || ExitRouteConflictsWithEarlierRequest(moving) || CanWaitForIndependentPickupExitTraffic(moving)))
                        // This exact monotonic exit dependency already has an earlier
                        // independently accepted request; no arbitrary pending0 wait.
                        remaining=ExpandRoute(other.transform.position,moving.Route??new Queue<Vector3>(new[]{moving.FinalTarget.Position}));
                    else continue;
                }
                else
                {
                    if(agent.isStopped || !(agent.pathPending || agent.hasPath && agent.pathStatus==UnityEngine.AI.NavMeshPathStatus.PathComplete)) continue;
                    remaining=adapter.GetCompletePath(other.transform.position,agent.destination);
                }
                if(remaining==null) continue; // Unknown/cancelled paths never authorize an unbounded wait.
                if(paths.Any(path=>path!=null && (!CustomerRoutePlanner.IsClear(path,new[]{other.transform.position},clearance) ||
                    RoutesConflict(path,remaining,clearance) || !CustomerRoutePlanner.IsClear(remaining,new[]{path[path.Count-1]},clearance)))) return true;
            }
            return false;
        }
        private bool IsExpectedCounterDeparture(Visit visit) => expectedCounterDeparture!=null &&
            visit.Actor!=null && visit.Actor.isActiveAndEnabled && visit.State==CustomerVisitState.Queued &&
            visit.CounterArrived && !visit.QueueLeft && visit.RequestId==0 && !visit.HasCollected &&
            visit.PickupAssigned && !visit.PickupInTransit && visit.PickupOwner!=null &&
            queue!=null && queue.Snapshot.Count>0 && queue.Snapshot[0].VisitId==visit.Id && queue.Snapshot[0].Arrived &&
            expectedCounterDeparture(visit.Id);

        private void RestoreDeliveryYielders()
        {
            var actor=world.RegisteredActors.FirstOrDefault(a=>a!=null && a.ActorId==deliveryActorId);
            if(actor==null || !deliveryAcquired) return;
            var home=Vector3.Distance(actor.transform.position,deliveryWorkTarget.Position)<=actor.Settings.ArrivalDistance+.001f;
            if(!home) deliveryWasAway=true;
            if(!deliveryWasAway || !home) return;
            if(visits.Values.Any(v=>v.DeliveryYieldPhase==1 || v.DeliveryYieldPhase==3)) return;
            var held=visits.Values.Where(v=>v.DeliveryYieldPhase==2).OrderBy(v=>v.Assignment.SlotIndex).ToArray();
            Visit blockedReturn=null;
            foreach(var visit in held)
            {
                if(!visit.PickupOwner.TryGetYieldReturn(visit.Id,out var candidate)) continue;
                if(visit.YieldReturnTraffic!=null && HasDeliveryTraffic(visit.Actor,visit.YieldReturnTraffic)) continue;
                visit.YieldReturnTraffic=null;
                if(!TryBuildPickupAdvance(candidate,out var path))
                {
                    path=path??adapter.GetCompletePath(visit.Actor.transform.position,candidate.Position);
                    if(path!=null && HasDeliveryTraffic(visit.Actor,new[]{path})) visit.YieldReturnTraffic=new[]{path};
                    else if(blockedReturn==null) blockedReturn=visit;
                    continue;
                }
                if(!visit.PickupOwner.TryReturnYielded(visit.Id,out var assignment)) continue;
                visit.Assignment=assignment;visit.DeliveryYieldPhase=3;
                Send(visit,CustomerVisitState.PickupAdvancing,PickupWaitTarget(assignment,visit.PickupOwner));
                return; // Restore one real body at a time, with normal remaining-route protection.
            }
            if(blockedReturn!=null && blockedReturn.PickupOwner.TryReturnYielded(blockedReturn.Id,out var blockedTarget))
            {
                blockedReturn.Assignment=blockedTarget;blockedReturn.DeliveryYieldPhase=3;
                SetRouteContext(blockedReturn,CustomerVisitState.PickupAdvancing,PickupWaitTarget(blockedTarget,blockedReturn.PickupOwner));
                BlockVisit(blockedReturn,CustomerVisitState.PickupAdvancing,"让位恢复通道堵塞，请调整布局");return;
            }
            if(held.Length!=0) return; // Pending real traffic must never abandon the still-owned held customers.
            deliveryAcquired=false;deliveryWasAway=false;
        }
        // P14 delivery only: every owned segment is rechecked from its current real body pose.
        public bool IsPickupDeliverySegmentClear(string employeeId,NavigationTarget target)
        {
            if(!CanIntegratedProgress || pickupQueue==null || !pickupQueue.TryGetServiceLease(employeeId,out _)) return false;
            var actor=world.RegisteredActors.FirstOrDefault(a=>a!=null && a.ActorId==employeeId && a.isActiveAndEnabled);
            return actor!=null && PickupDeliveryPathClear(actor,adapter.GetCompletePath(actor.transform.position,target.Position),RouteObstacles(employeeId),target.Position);
        }
        private bool PickupDeliveryPathClear(NavigationActor actor,IReadOnlyList<Vector3> path,Vector3[] occupied,Vector3 goal)
        {
            if(!CustomerRoutePlanner.IsClear(path,occupied,RouteClearance(actor.Settings))) return false;
            var clearance=RouteClearance(actor.Settings)+2*actor.Settings.MaxSpeed*
                Mathf.Min(Time.deltaTime,actor.Settings.MaxSubstepSeconds*actor.Settings.MaxSubstepsPerFrame);
            foreach(var moving in visits.Values.Where(v=>v.Id!=actor.ActorId && v.RequestId!=0 && v.Actor!=null && v.Actor.isActiveAndEnabled && IsPickupMovement(v.State)))
            {
                var remaining=ExpandRoute(moving.Actor.transform.position,new[]{moving.Actor.Agent.destination}.Concat(moving.Route??Enumerable.Empty<Vector3>()));
                if(remaining==null || RoutesConflict(path,remaining,clearance) || !CustomerRoutePlanner.IsClear(remaining,new[]{goal,path[path.Count-1]},clearance)) return false;
            }
            return true;
        }
        public void ReleasePickupServicePosition(string actorId) { pickupQueue?.ReleaseServicePosition(actorId); }
        public void ReleasePickupAssignment(string visitId)
        {
            if(!visits.TryGetValue(visitId??"",out var visit) || !visit.PickupAssigned) return;
            // 正常付费移动不走此取消入口；只用于未使用预留或明确的owned清理。
            if(visit.PickupInTransit || visit.HasCollected) return;
            visit.PickupOwner.Remove(visitId); visit.PickupAssigned=false;
        }
        public void ClearUncollectedVisits(IReadOnlyCollection<string> collectedVisitIds)
        {
            if(collectedVisitIds==null) throw new ArgumentNullException(nameof(collectedVisitIds));
            foreach(var visit in visits.Values.ToArray())
            {
                if(!collectedVisitIds.Contains(visit.Id)) { RemoveVisit(visit.Id); continue; }
                // Canonical service提交可能先于TryMoveToExit，且投影可重入Done。
                // Preserve that committed fact even before the navigation port is called.
                if(!visit.HasCollected)
                    visit.PickupPosition=visit.Actor.transform.position;
                ConfirmCollectedExit(visit);
                HoldVisit(visit); visit.ResumeState=CustomerVisitState.Exiting;
            }
            // 保留者仍持旧PickupOwner用于实际离位释放；新客必须解析新布局geometry。
            pickupQueue=null; pickupGeometryConfigured=false; selectedPickupId=null;pickupPlanningRejections.Clear();
            deliveryAcquired=false;deliveryWasAway=false;deliveryYieldCount=0;deliveryAttempt=0;PickupDeliveryFailure="";deliveryTrafficPaths=null;
            revision=-1; suspended=true; clock?.Tick(0,false,false);
        }
        public void HoldIntegratedVisits() { integratedHeld=true; HoldVisits(); clock?.Tick(0,false,false); }
        public void AcknowledgeExitSnapshots() { exitSnapshots.Clear(); }
        private void ConfirmCollectedExit(Visit visit)
        {
            var firstCollection=!visit.HasCollected;
            visit.HasCollected=true;
            if(visit.ExitSequence==0) visit.ExitSequence=++nextExitSequence;
            if(!firstCollection) return;
            // Presentation is optional; canonical collection and exit navigation remain committed.
            try { visit.Actor?.GetComponent<CustomerCarryPresenter>()?.ConfirmCollected(); }
            catch(Exception error) { Debug.LogWarning("Customer collected cup unavailable: "+error.Message+". Actual exit continues.",this); }
        }
        private static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) &&
            !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
        public IReadOnlyList<CustomerVisitSnapshot> Snapshot => visits.Values.OrderBy(v=>v.Id,StringComparer.Ordinal)
            .Select(v=>new CustomerVisitSnapshot(v.Id,v.State,v.Actor==null?Vector3.zero:v.Actor.transform.position)).ToList().AsReadOnly();
        public void Configure(NavigationWorld value,NavigationLayoutAdapter layout,GameTimeService gameTime,
            NavigationActor[] characters,Transform entrance,Transform departure,Transform head,
            CafeLayoutRuntime business=null,Transform grid=null,DecorationModeController decor=null)
        {
            if(visits.Count>0) throw new InvalidOperationException("Cannot reconfigure an active customer session");
            world=value; adapter=layout; time=gameTime; prefabs=characters; spawn=entrance; exit=departure;
            validationHead=head; runtime=business; gridRoot=grid; decoration=decor;
            decorationVersion=decoration==null?0:decoration.CompletedLayoutChangeVersion;
            revision=-1; Capacity=null; queue=null; admissionRejected=admissionRouteUnavailable=false;
            pickupPlanningRejections.Clear();
        }
        private void Awake()
        {
            session=Guid.NewGuid().ToString("N"); var random=new System.Random();
            clock=new CustomerSpawnClock(()=> (float)random.NextDouble());
        }
        private void Update() { if(!integratedMode) Step(Time.deltaTime); }
        private bool Configured => !disposed && world!=null && adapter!=null && time!=null && spawn!=null && exit!=null &&
            prefabs!=null && prefabs.Length>0 && prefabs.All(p=>p!=null);
        public void Step(float delta)
        {
            if(float.IsNaN(delta) || float.IsInfinity(delta) || delta<0) throw new ArgumentOutOfRangeException(nameof(delta));
            // The integrated owner calls Step directly / disabled组件也必须遵守生命周期门禁。
            if(integratedMode && !isActiveAndEnabled) return;
            if(integratedMode && integratedHeld) { Status="P14 owner暂停，保留顾客与容量"; return; }
            // P14只在正游戏时间推进普通移动；保留suspended给恢复后的合法Step。
            if(integratedMode && (delta==0 || time==null || time.CurrentSpeed==GameSpeed.Paused || time.IsResumeBlocked ||
                decoration!=null && decoration.IsOpen)) return;
            if(!disposed && decoration!=null && decorationVersion!=decoration.CompletedLayoutChangeVersion)
            {
                decorationVersion=decoration.CompletedLayoutChangeVersion;
                if(!integratedMode) ResetCustomersAfterDecoration(); // P14的同步选择性清场由owner负责。
            }
            if(!Configured || !world.isActiveAndEnabled || !adapter.isActiveAndEnabled)
            { Status="营业组件不可用"; HoldVisits(); clock?.Tick(0,false,false); return; }
            var decorating=decoration!=null && decoration.IsOpen;
            if(decorating) { HoldVisits(); clock.Tick(0,false,true); Status="装修中，暂停生成"; return; }
            if(runtime!=null && runtime.Layout==null) { Status="等待营业布局"; return; }
            if(runtime!=null && !adapter.EnforceBusinessReadiness)
            {
                if(runtime.CurrentReadiness==null || !runtime.CurrentReadiness.CanOpenForBusiness)
                { Status="请先布置可营业的服务布局"; clock.Tick(0,false,false); return; }
                adapter.RequireBusinessReadiness();
                var bridge=adapter.GetComponent<NavigationDecorationBridge>();
                if(bridge!=null) { bridge.Configure(adapter); bridge.ConfigureTime(time); }
            }
            if(Capacity==null) Capacity=new CapacityService(runtime==null?64:FloorCapacitySource.CountInteriorCells(runtime.Layout));
            if(!adapter.CurrentReadiness.CanResume || !world.LayoutAvailable)
            {
                HoldVisits(); clock.Tick(0,false,false);
                if(!adapter.RebuildAndValidate().CanResume || !world.LayoutAvailable)
                { if(adapter.EnforceBusinessReadiness) world.HoldUnavailableResumeBlock(time); Status="路径不可用，请修复布局"; return; }
                world.ReleaseValidatedResumeBlock();
            }
            if(revision!=adapter.Revision || queue==null || suspended)
            {
                // 即使adapter已同步rebuild，也必须退休上一revision的已终止请求。
                if(queue!=null) HoldVisits();
                if(runtime!=null) Capacity.UpdateFloorCellCount(FloorCapacitySource.CountInteriorCells(runtime.Layout));
                if(!BuildQueue()) { HoldVisits(); clock.Tick(0,false,false); return; }
                revision=adapter.Revision; suspended=false;
                ResumeBlockedVisits();
            }
            RestoreDeliveryYielders();
            foreach(var visit in visits.Values.ToArray())
            {
                if(visit.Actor==null) { RemoveVisit(visit.Id); continue; }
                if(visit.PendingPickupSegment && IsPickupMovement(visit.State))
                    SendRouteSegment(visit,visit.State); // Recheck the retained candidate; no A* while traffic is moving.
                if(visit.PendingExitSegment && visit.State==CustomerVisitState.Exiting && CanIntegratedProgress)
                {
                    if(visit.Route==null)
                    {
                        var resumed=integratedMode
                            ?PlanPickupExit(visit.Actor.transform.position,visit.FinalTarget.Position,RouteObstacles(visit.Id),visit.Actor.Settings)
                            :BuildRoute(visit.Actor.transform.position,visit.FinalTarget.Position,visit.Id,visit.Actor.Settings);
                        if(resumed==null)
                        { if(CanWaitForDepartingExitBody(visit) || CanWaitForIndependentPickupExitTraffic(visit)) continue; BlockVisit(visit,CustomerVisitState.Exiting,"没有安全通道"); continue; }
                        visit.Route=new Queue<Vector3>(resumed);
                    }
                    SendRouteSegment(visit,CustomerVisitState.Exiting);
                    if(!visits.ContainsKey(visit.Id)) continue; // Synchronous Arrived can retire this visit.
                }
                if((visit.ResumeState==CustomerVisitState.Exiting || integratedMode && visit.PickupInTransit) && !visit.QueueLeft &&
                    Vector3.Distance(visit.Actor.transform.position,visit.CounterPosition)>(integratedMode?DepartureDistance(visit):1.1f))
                { visit.QueueLeft=true; visit.Admission.LeaveQueue(); queue.Remove(visit.Id); }
                if(integratedMode && visit.HasCollected && !visit.PickupDeparted &&
                    Vector3.Distance(visit.Actor.transform.position,visit.PickupPosition)>DepartureDistance(visit))
                {
                    visit.PickupDeparted=true; visit.Admission.LeavePickup();
                    visit.PickupOwner?.Remove(visit.Id); visit.PickupOwner?.ReleaseServicePosition(visit.Id); visit.PickupAssigned=false;
                }
            }
            var blocked=visits.Values.Any(v=>v.State==CustomerVisitState.Blocked);
            if((!blocked || recoveringQueue) && queue.TryGetNextAdvance(out var assignment,CanBeginAdvance) && visits.TryGetValue(assignment.VisitId,out var next))
            { next.Assignment=assignment; Send(next,CustomerVisitState.Advancing,QueueTarget(assignment)); }
            if(integratedMode && pickupQueue!=null && !blocked && !visits.Values.Any(v=>v.DeliveryYieldPhase!=0) && pickupQueue.TryGetNextAdvance(out var pickupAdvance,CanBeginPickupAdvance) &&
                visits.TryGetValue(pickupAdvance.VisitId,out var pickupVisit))
            {
                pickupVisit.Assignment=pickupAdvance; pickupVisit.ServiceArrived=false;
                Send(pickupVisit,CustomerVisitState.PickupAdvancing,PickupWaitTarget(pickupAdvance,pickupVisit.PickupOwner));
            }
            blocked=visits.Values.Any(v=>v.State==CustomerVisitState.Blocked); // 同步提交/恢复可能已经改变状态。
            // 满员等营业条件取消本轮；临时入口/队尾/路径门禁由TrySpawn保留到期机会。
            var canWait=(!integratedMode || integratedAdmissionAllowed) && !blocked && Capacity.CanAdmit && queue.Snapshot.Count<queue.AvailableSlotCount;
            if(autoSpawn && clock.Tick(delta,canWait,false) && TrySpawn()) clock.CompleteAdmission();
            else if(!autoSpawn) clock.Tick(0,false,false);
            var failed=visits.Values.FirstOrDefault(v=>v.State==CustomerVisitState.Blocked && !string.IsNullOrEmpty(v.Failure));
            if(failed!=null) Status="顾客路径受阻（"+failed.Failure+"），请修复布局后重试";
            else if(queue.AvailableSlotCount<queue.Snapshot.Count)
                Status="队伍可用站位不足（"+queue.AvailableSlotCount+"/"+queue.Snapshot.Count+"），请调整布局";
            else if(blocked) Status="队伍正在恢复，等待前方顾客";
            else if(visits.Values.Any(v=>v.PendingExitSegment)) Status="等待前客实际离开出口 / Waiting for departing customer";
            else if(visits.Values.Any(v=>v.PendingPickupSegment)) Status="等待活动角色腾空取餐通道 / Waiting for moving traffic";
            else if(!Capacity.CanAdmit) Status="容量已满，暂停生成";
            else if(!queue.CanJoin) Status="队伍空间不足或正在前移";
            else if(!EntryClear()) Status="入口暂被占用";
            else if(!TailClear()) Status="队尾站位暂被占用";
            else if(admissionRouteUnavailable) Status="入店通道暂不可用，等待通道腾空后再尝试";
            else if(admissionRejected) Status="本次入店失败，等待入店重试";
            else Status="营业中";
        }
        private bool BuildQueue()
        {
            Vector3 head,outward;
            var excluded=new List<Vector3>();
            if(runtime!=null)
            {
                if(gridRoot==null || runtime.CurrentReadiness==null) { Status="服务布局未初始化"; return false; }
                var cash=runtime.CurrentReadiness.Stations.Where(s=>s.IsValid && s.FunctionType==LayoutStationType.CashRegister)
                    .OrderBy(s=>s.InstanceId,StringComparer.Ordinal).FirstOrDefault();
                if(cash==null || !cash.Anchors.TryGetAnchor(InteractionRole.Customer,out var customer))
                { Status="没有可用收银台"; return false; }
                SelectedRegisterId=cash.InstanceId;
                head=WorldAnchor(customer); queueFacing=Direction(customer.Facing); outward=-queueFacing;
                foreach(var station in runtime.CurrentReadiness.Stations)
                    foreach(var anchor in station.Anchors.Anchors)
                        // 空Employee anchor可排队；实际角色仍由World碰撞和路线避让保护。
                        if(station.FunctionType==LayoutStationType.PickUpPoint && anchor.Role==InteractionRole.Customer)
                            excluded.Add(WorldAnchor(anchor));
            }
            else
            {
                if(validationHead==null) { Status="缺少验证队首"; return false; }
                head=validationHead.position; outward=validationHead.forward; queueFacing=-outward; SelectedRegisterId="validation.head";
            }
            // Pick-up顾客位置按身体与到站误差留空，不把队伍间距当作禁站半径。
            var pickupClearance=prefabs.Max(p=>2*p.Settings.AgentRadius+p.Settings.CollisionSkin+
                p.Settings.Epsilon+p.Settings.ArrivalDistance);
            var queueClearance=prefabs.Max(p=>2*p.Settings.AgentRadius+p.Settings.CollisionSkin+p.Settings.Epsilon);
            // 出生点按身体保护；等待目标另计到站误差 / physical spawn clearance plus arrival allowance.
            var entryClearance=queueClearance+prefabs.Max(p=>p.Settings.ArrivalDistance);
            var slots=new CounterQueuePlanner().Build(head,outward,Capacity.Limits.CounterQueue,(p,previous)=>
            {
                if(!adapter.IsPointWalkable(p) || (previous!=null && !adapter.HasClearPath(previous.Value,p)) ||
                    !adapter.HasClearPath(spawn.position,p) || excluded.Any(e=>Vector3.Distance(e,p)<pickupClearance) ||
                    InEntrance(p) || Vector3.Distance(exit.position,p)<1.2f) return false;
                return true;
            },(p,prefix)=>
                // 只验证当前分支；回退后不保留已放弃的虚拟占位。
                // 前方slots都有人时，后客仍须从入口绕行抵达。
                (prefix.Count==0 || Vector3.Distance(p,spawn.position)>=entryClearance) &&
                    new CustomerRoutePlanner().Build(spawn.position,p,prefix,queueClearance,adapter.GetCompletePath)!=null,
                (from,requested)=>
                {
                    var path=adapter.GetCompletePath(from,requested);
                    return path==null || path.Count==0?(Vector3?)null:path[path.Count-1];
                });
            counterWaitSlots=slots.ToArray();
            if(queue==null) queue=new CounterQueueService(slots); else queue.UpdateSlots(slots);
            if(slots.Count==0) { Status="队伍没有可用站位"; return visits.Count>0; }
            return true;
        }
        private Vector3 WorldAnchor(InteractionAnchor a) => gridRoot.TransformPoint(new Vector3((a.Position.X+.5f)*runtime.Layout.GridSettings.CellSize,0,(a.Position.Y+.5f)*runtime.Layout.GridSettings.CellSize));
        private Vector3 Direction(CardinalDirection d) => gridRoot.TransformDirection(new[]{Vector3.forward,Vector3.right,Vector3.back,Vector3.left}[(int)d]);
        private bool InEntrance(Vector3 p)
        {
            if(runtime==null || gridRoot==null) return Vector3.Distance(p,spawn.position)<1.2f;
            var local=gridRoot.InverseTransformPoint(p); var size=runtime.Layout.GridSettings.CellSize;
            return runtime.Layout.Reservations.Any(r=>r.Type==LayoutReservationType.EntranceClearance &&
                local.x>=r.Origin.X*size-.46f && local.x<=(r.Origin.X+r.Size.Width)*size+.46f &&
                local.z>=r.Origin.Y*size-.46f && local.z<=(r.Origin.Y+r.Size.Height)*size+.46f);
        }
        private bool EntryClear()
        {
            if(!adapter.IsPointWalkable(spawn.position)) return false;
            var incomingRadius=prefabs.Max(p=>p.Settings.AgentRadius+p.Settings.CollisionSkin+p.Settings.Epsilon);
            return world.RegisteredActors.All(a=>a!=null &&
                Vector3.Distance(a.transform.position,spawn.position)>=a.Settings.AgentRadius+incomingRadius);
        }
        private bool TailClear()
        {
            if(!queue.NextAdmissionPosition.HasValue) return false;
            // 到站存在误差；用代理半径+碰撞余量+到站误差判断，不把slot间距当占用半径。
            var admissionRadius=prefabs.Max(p=>p.Settings.AgentRadius+p.Settings.CollisionSkin+
                p.Settings.Epsilon+p.Settings.ArrivalDistance);
            var position=queue.NextAdmissionPosition.Value;
            // 活动入店前客可预测腾位；TrySpawn仍须验证剩余路线，不移除实际身体。
            // A temporary entry occupant is provisional until the route preflight approves.
            return world.RegisteredActors.All(a=>a!=null &&
                (Vector3.Distance(a.transform.position,position)>=a.Settings.AgentRadius+admissionRadius ||
                 IsMovingEntryLeader(a) && Vector3.Distance(visits[a.ActorId].Assignment.Position,position)>=a.Settings.AgentRadius+admissionRadius));
        }
        public bool TrySpawn()
        {
            if(!isActiveAndEnabled || !Configured || !world.isActiveAndEnabled || !world.LayoutAvailable || queue==null ||
                integratedMode && (!integratedAdmissionAllowed || !CanIntegratedProgress || exitSnapshots.Count+visits.Count>=Capacity.Limits.TotalCustomers) ||
                (decoration!=null && decoration.IsOpen) || !queue.CanJoin || !Capacity.CanAdmit ||
                visits.Values.Any(v=>v.State==CustomerVisitState.Blocked) || !EntryClear() || !TailClear()) return false;
            // 在reserve/create前证明路线绕得开已有顾客，不发布一个已知会堵住的visit。
            admissionRouteUnavailable=!CanStartAdmission(queue.NextAdmissionPosition.Value,prefabs[(int)(nextVisit%prefabs.Length)].Settings);
            if(admissionRouteUnavailable) return false;
            var tail=queue.Snapshot.Count; var id=session+"-"+(++nextVisit).ToString("D8");
            admissionRejected=true; // 失败原因与到期机会保留到成功接纳。
            if(!CustomerAdmission.TryCreate(Capacity,id,out var lease)) return false;
            NavigationActor actor=null; Visit visit=null;
            try
            {
                var prefab=prefabs[(int)((nextVisit-1)%prefabs.Length)];
                actor=Instantiate(prefab,spawn.position,spawn.rotation,transform);
                if(!actor.TryInitializeRuntimeId(id) || !actor.TryEnableSteadyPathMotion() || !world.Register(actor) || !queue.TryJoin(id)) return false;
                var appearance=integratedMode?collectedCarryAppearances.FirstOrDefault(a=>a!=null && a.CustomerPrefab==prefab):null;
                if(integratedMode)
                    actor.gameObject.AddComponent<CustomerCarryPresenter>().Configure(actor,appearance,collectedCupPrefab);
                visit=new Visit{Id=id,Actor=actor,Admission=lease,Assignment=queue.Snapshot[tail]};
                visits.Add(id,visit);
                if(!lease.MarkInside()) { visits.Remove(id); queue.Remove(id); visit=null; return false; }
                admissionRejected=admissionRouteUnavailable=false;
                Send(visit,CustomerVisitState.Entering,QueueTarget(visit.Assignment));
                return true;
            }
            finally
            {
                if(visit==null)
                {
                    queue.Remove(id); if(actor!=null) { world.Unregister(actor); Destroy(actor.gameObject); }
                    lease.Dispose();
                }
            }
        }
        private NavigationTarget PickupWaitTarget(QueueAssignment assignment,PickupQueueService owner)
        {
            var facing=-pickupOutward;
            if(assignment.SlotIndex>0 && owner!=null)
            {
                // 等候链转弯后朝前一位；队首仍朝服务点，不改变实际领取侧。
                // Face the owned predecessor target, including during a real queue advance.
                var previous=owner.Snapshot.FirstOrDefault(a=>a.SlotIndex==assignment.SlotIndex-1);
                var towardPrevious=previous.Position-assignment.Position; towardPrevious.y=0;
                if(previous.VisitId!=null && towardPrevious.sqrMagnitude>.001f) facing=towardPrevious.normalized;
            }
            return new NavigationTarget(assignment.Position,facing);
        }
        private NavigationTarget QueueTarget(QueueAssignment assignment)
        {
            var facing=queueFacing;
            if(assignment.SlotIndex>0)
            {
                // 排队朝向沿slot链指向前一位；队首保留收银台方向。
                var previous=queue.Snapshot.FirstOrDefault(a=>a.SlotIndex==assignment.SlotIndex-1);
                var towardPrevious=previous.Position-assignment.Position; towardPrevious.y=0;
                if(previous.VisitId!=null && towardPrevious.sqrMagnitude>.001f) facing=towardPrevious.normalized;
            }
            return new NavigationTarget(assignment.Position,facing);
        }
        private void SetRouteContext(Visit visit,CustomerVisitState phase,NavigationTarget target)
        {
            if(phase==CustomerVisitState.Advancing) visit.AdvanceStart=visit.Actor.transform.position;
            visit.Generation++; visit.RequestId=0; visit.State=visit.ResumeState=phase;
            visit.FinalTarget=target; visit.Segments=visit.Replans=0; visit.Failure=null;
            visit.PendingExitSegment=visit.PendingPickupSegment=false;
            visit.YieldReturnTraffic=null;
        }
        private void Send(Visit visit,CustomerVisitState phase,NavigationTarget target)
        {
            SetRouteContext(visit,phase,target);
            var route=phase==CustomerVisitState.MovingToPickup || phase==CustomerVisitState.PickupAdvancing
                ?BuildPickupWaitRoute(visit.Actor.transform.position,target.Position,visit.Id,visit.Actor.Settings)
                :integratedMode && phase==CustomerVisitState.Exiting
                ?PlanPickupExit(visit.Actor.transform.position,target.Position,RouteObstacles(visit.Id),visit.Actor.Settings)
                :BuildRoute(visit.Actor.transform.position,target.Position,visit.Id,visit.Actor.Settings);
            if(route==null && phase==CustomerVisitState.Exiting && (CanWaitForDepartingExitBody(visit) || CanWaitForIndependentPickupExitTraffic(visit)))
            { visit.Route=null; visit.PendingExitSegment=true; return; }
            if(route==null || route.Count>24) { BlockVisit(visit,phase,"没有安全通道"); return; }
            visit.Route=new Queue<Vector3>(route); SendRouteSegment(visit,phase);
        }
        private IReadOnlyList<Vector3> BuildRoute(Vector3 from,Vector3 to,string ignore,NavigationSettings settings,int? advancingSlot=null,bool prospectiveAdmission=false)
        {
            var occupied=RouteObstacles(ignore,advancingSlot,prospectiveAdmission);
            return new CustomerRoutePlanner().Build(from,to,occupied,RouteClearance(settings),adapter.GetCompletePath);
        }
        private bool PickupPlanCanExit(Visit visit,Vector3 head,Vector3[] futureTail)
        {
            var occupied=RouteObstacles(visit.Id).Concat(futureTail).Distinct().ToArray();
            var clearance=RouteClearance(visit.Actor.Settings);
            // The collecting front vacates its head; all future paid followers remain obstacles.
            return pickupSides.Any(side=>new CustomerRoutePlanner().Build(head,side.Position,occupied,clearance,adapter.GetCompletePath)!=null &&
                PlanPickupExit(side.Position,exit.position,occupied,visit.Actor.Settings)!=null);
        }
        private IReadOnlyList<Vector3> PlanPickupExit(Vector3 from,Vector3 to,Vector3[] occupied,NavigationSettings settings)
        {
            var clearance=RouteClearance(settings);
            var original=new CustomerRoutePlanner().Build(from,to,occupied,clearance,adapter.GetCompletePath);
            if(original!=null || !integratedMode || runtime?.RoomSurfaceLayout==null || gridRoot==null) return original;
            var floor=runtime.RoomSurfaceLayout.FloorTiles.Keys.ToArray();if(floor.Length==0)return null;
            var cell=runtime.Layout.GridSettings.CellSize;
            foreach(var requested in PickupExitBoundaryCandidates(gridRoot,floor,cell,from,to,settings.AgentRadius))
            {
                var previous=from;var clear=true;var points=new List<Vector3>();
                for(var index=0;index<requested.Length;index++)
                {
                    var path=adapter.GetCompletePath(previous,requested[index]);
                    if(!CustomerRoutePlanner.IsClear(path,occupied,clearance)){clear=false;break;}
                    var endpoint=path[path.Count-1];endpoint.y=from.y;
                    // Cache owned horizontal endpoints, never a projected request.
                    points.Add(index==requested.Length-1?to:endpoint);previous=endpoint;
                }
                if(clear)return points.AsReadOnly();
            }
            return null;
        }
        internal static Vector3[][] PickupExitBoundaryCandidates(Transform root,IReadOnlyList<GridPosition> floor,float cell,Vector3 from,Vector3 to,float radius)
        {
            var inset=root.InverseTransformVector(Vector3.right*(radius+.02f)).magnitude;
            var start=root.InverseTransformPoint(from);var goal=root.InverseTransformPoint(to);
            // At most four boundary corridors, each three owned waypoints (within 24).
            // 不改旧P12环搜索；P14补原body-ring可能越出floor的有限边缘候选。
            var edges=new[]{
                new[]{new Vector3(floor.Min(p=>p.X)*cell+inset,start.y,start.z),new Vector3(floor.Min(p=>p.X)*cell+inset,start.y,goal.z)},
                new[]{new Vector3((floor.Max(p=>p.X)+1)*cell-inset,start.y,start.z),new Vector3((floor.Max(p=>p.X)+1)*cell-inset,start.y,goal.z)},
                new[]{new Vector3(start.x,start.y,floor.Min(p=>p.Y)*cell+inset),new Vector3(goal.x,start.y,floor.Min(p=>p.Y)*cell+inset)},
                new[]{new Vector3(start.x,start.y,(floor.Max(p=>p.Y)+1)*cell-inset),new Vector3(goal.x,start.y,(floor.Max(p=>p.Y)+1)*cell-inset)}};
            return edges.Select(edge=>edge.Select(root.TransformPoint).Concat(new[]{to}).ToArray()).ToArray();
        }
        private IReadOnlyList<Vector3> BuildPickupWaitRoute(Vector3 from,Vector3 to,string ignore,NavigationSettings settings)
        {
            var occupied=RouteObstacles(ignore);
            var clearance=RouteClearance(settings);
            var planned=new CustomerRoutePlanner().Build(from,to,occupied,clearance,adapter.GetCompletePath);
            if(planned!=null || !multiSidePickup) return planned;
            // A crowded queue can hide the safe approach behind a free exposed side from
            // the bounded body-ring search. Keep every body/owned target and the final goal.
            // 只复用已解析的合法侧；每段仍由原P11实际移动前复核，不忽略任何身体。
            foreach(var side in pickupSides)
            {
                if(Vector3.Distance(from,side.Position)<=settings.ArrivalDistance+settings.Epsilon || side.Position==to) continue;
                var approach=adapter.GetCompletePath(from,side.Position);
                if(!CustomerRoutePlanner.IsClear(approach,occupied,clearance)) continue;
                var departure=adapter.GetCompletePath(side.Position,to);
                if(CustomerRoutePlanner.IsClear(departure,occupied,clearance)) return new[]{side.Position,to};
            }
            return null;
        }
        private Vector3[] RouteObstacles(string ignore,int? advancingSlot=null,bool prospectiveAdmission=false)
            => world.RegisteredActors.Where(a=>a!=null && a.ActorId!=ignore && !IsMovingQueueLeader(a,ignore,advancingSlot) &&
                    !(prospectiveAdmission && IsMovingEntryLeader(a))).Select(a=>a.transform.position)
                .Concat(visits.Values.Where(v=>v.Id!=ignore && ReservesRouteTarget(v,ignore,advancingSlot))
                    .Select(v=>v.Assignment.Position))
                .Concat(!multiSidePickup || pickupQueue==null?Enumerable.Empty<Vector3>():pickupQueue.ServiceLeases.Where(lease=>lease.Key!=ignore).Select(lease=>lease.Value))
                .Distinct().ToArray();
        private bool IsMovingQueueLeader(NavigationActor actor,string followerId,int? advancingSlot)
        {
            // 同队前移或连续入店的活动前客可预测腾位，保留其目标slot。
            // 实际身体不移除：P11 World每一步照常检查全部角色并限制安全位移。
            if(followerId==null || !visits.TryGetValue(followerId,out var follower) || !visits.TryGetValue(actor.ActorId,out var leader) ||
                leader.RequestId==0 || !actor.isActiveAndEnabled || leader.Assignment.SlotIndex>=(advancingSlot??follower.Assignment.SlotIndex)) return false;
            return leader.State==CustomerVisitState.Advancing && (advancingSlot.HasValue || follower.State==CustomerVisitState.Advancing) ||
                follower.State==CustomerVisitState.Entering && leader.State==CustomerVisitState.Entering;
        }
        private bool IsMovingEntryLeader(NavigationActor actor)
            => actor.isActiveAndEnabled && visits.TryGetValue(actor.ActorId,out var leader) &&
                leader.State==CustomerVisitState.Entering && leader.RequestId!=0;
        private bool ReservesRouteTarget(Visit other,string requesterId,int? advancingSlot)
        {
            if(integratedMode && IsPickupMovement(other.State)) return true;
            if(other.State!=CustomerVisitState.Entering && other.State!=CustomerVisitState.Advancing) return false;
            if(requesterId==null || !visits.TryGetValue(requesterId,out var requester)) return true;
            // 后客尚未到达的目标不挡前客/离场路线；实际身体始终保留。
            if(requester.State==CustomerVisitState.Exiting) return false;
            return !advancingSlot.HasValue && requester.State!=CustomerVisitState.Advancing && requester.State!=CustomerVisitState.Entering ||
                other.Assignment.SlotIndex<(advancingSlot??requester.Assignment.SlotIndex);
        }
        private bool CanStartAdmission(Vector3 target,NavigationSettings settings)
        {
            var route=BuildRoute(spawn.position,target,null,settings,prospectiveAdmission:true);
            if(route==null || route.Count>24) return false;
            var incoming=ExpandRoute(spawn.position,route);
            if(incoming==null) return false;
            var clearance=RouteClearance(settings)+2*settings.MaxSpeed*
                Mathf.Min(Time.deltaTime,settings.MaxSubstepSeconds*settings.MaxSubstepsPerFrame);
            // 创建/预留前让已有任务先通过；新尾位不能堵住前客的剩余路线。
            // Validate remaining owned paths, then keep P11's continuous body guards.
            foreach(var earlier in visits.Values.Where(v=>v.RequestId!=0 && v.Actor!=null && v.Actor.isActiveAndEnabled))
            {
                var targets=new[]{earlier.Actor.Agent.destination}.Concat(earlier.Route??Enumerable.Empty<Vector3>());
                var remaining=ExpandRoute(earlier.Actor.transform.position,targets);
                if(remaining==null) return false;
                var targetClear=CustomerRoutePlanner.IsClear(remaining,new[]{target},clearance);
                var follows=earlier.State==CustomerVisitState.Entering &&
                    CanFollowEntryBend(incoming,remaining,clearance,settings.ArrivalDistance+settings.Epsilon) &&
                    CustomerRoutePlanner.IsClear(new[]{remaining[remaining.Count-1]},new[]{target},clearance) &&
                    (targetClear || FollowerReachesTailAfterLeader(incoming,remaining,target,clearance,
                        settings.MaxSpeed,earlier.Actor.Settings.MaxSpeed));
                // 同向跟随按先后经过尾位；前客最终目标仍须留空，实际身体由World逐步保护。
                if(!targetClear &&
                    !follows && !EntryVacatesTail(earlier,remaining,target,clearance)) return false;
                if(RoutesConflict(incoming,remaining,clearance) && !follows) return false;
            }
            return true;
        }
        private static bool FollowerReachesTailAfterLeader(IReadOnlyList<Vector3> follower,IReadOnlyList<Vector3> leader,
            Vector3 tail,float clearance,float followerSpeed,float leaderSpeed)
        {
            var followerDistance=0f; var leadingDistance=0f; var lastOccupiedDistance=0f;
            for(var i=1;i<follower.Count;i++) followerDistance+=Vector3.Distance(follower[i-1],follower[i]);
            for(var i=1;i<leader.Count;i++)
            {
                var delta=leader[i]-leader[i-1]; delta.y=0; var length=delta.magnitude;
                if(length>0 && !CustomerRoutePlanner.IsClear(new[]{leader[i-1],leader[i]},new[]{tail},clearance))
                {
                    var offset=tail-leader[i-1]; offset.y=0;
                    var closest=Mathf.Clamp01(Vector3.Dot(offset,delta)/delta.sqrMagnitude)*length;
                    // 最近点再走一个clearance必已腾位；跨段时后续段继续验证。
                    lastOccupiedDistance=leadingDistance+Mathf.Min(length,closest+clearance);
                }
                leadingDistance+=length;
            }
            // 仅用于有序同向跟随；留一段身体间距的时间余量，实际运动仍检查所有角色。
            return leaderSpeed>0 && followerSpeed>0 &&
                lastOccupiedDistance/leaderSpeed+clearance/Mathf.Min(leaderSpeed,followerSpeed)<=followerDistance/followerSpeed;
        }
        private static bool EntryVacatesTail(Visit earlier,IReadOnlyList<Vector3> path,Vector3 tail,float clearance)
        {
            if(earlier.State!=CustomerVisitState.Entering || path.Count<2 ||
                CustomerRoutePlanner.IsClear(new[]{path[0]},new[]{tail},clearance)) return false;
            // 只允许前客从临时占位向外腾空，退出后不得再穿回新队尾。
            // Ignore only the vacating entry prefix, not a future crossing or parked body.
            for(var i=1;i<path.Count;i++)
            {
                var offset=path[i-1]-tail; var step=path[i]-path[i-1]; offset.y=step.y=0;
                if(Vector3.Dot(offset,step)<-.000001f) return false;
                var end=path[i]-tail; end.y=0;
                if(end.magnitude>=clearance)
                    return CustomerRoutePlanner.IsClear(path.Skip(i).ToArray(),new[]{tail},clearance);
            }
            return false;
        }
        private bool CanBeginAdvance(QueueAssignment assignment)
        {
            if(!visits.TryGetValue(assignment.VisitId,out var visit) || visit.Actor==null) return false;
            // P14 customers can still cross the CR advance after releasing their queue token.
            // 只等待当前accepted取餐请求；旧Counter默认不增加此交通依赖。
            var leaders=visits.Values.Where(v=>v.RequestId!=0 && v.Actor!=null && v.Actor.isActiveAndEnabled &&
                (v.State==CustomerVisitState.Exiting || integratedMode && IsPickupMovement(v.State) || v.State==CustomerVisitState.Advancing &&
                    v.Assignment.SlotIndex<assignment.SlotIndex)).ToArray();
            if(leaders.Length==0) return true;
            var route=BuildRoute(visit.Actor.transform.position,assignment.Position,visit.Id,visit.Actor.Settings,assignment.SlotIndex);
            if(route==null) return false; // 活动前客腾位前先等待，不把临时占路立即判失败。
            var path=ExpandRoute(visit.Actor.transform.position,route);
            if(path==null) return false;
            var clearance=RouteClearance(visit.Actor.Settings)+2*visit.Actor.Settings.MaxSpeed*
                Mathf.Min(Time.deltaTime,visit.Actor.Settings.MaxSubstepSeconds*visit.Actor.Settings.MaxSubstepsPerFrame);
            foreach(var leader in leaders)
            {
                var targets=new[]{integratedMode?leader.CurrentSegmentTarget.Position:leader.Actor.Agent.destination}.Concat(leader.Route??Enumerable.Empty<Vector3>());
                var leadingPath=ExpandRoute(leader.Actor.transform.position,targets);
                if(leadingPath==null || RoutesConflict(path,leadingPath,clearance)) return false;
                // 同向逐格跟进可沿前客刚离开的站位走；交叉拐弯先等待安全空间。
                if(leader.State==CustomerVisitState.Advancing &&
                    Vector3.Distance(assignment.Position,leader.AdvanceStart)<=visit.Actor.Settings.ArrivalDistance+.001f) continue;
                // 后客不能先停在前客尚需经过的通道上，即使两条路径同向。
                if(!CustomerRoutePlanner.IsClear(leadingPath,new[]{assignment.Position},clearance)) return false;
            }
            return true;
        }
        private bool CanBeginPickupAdvance(QueueAssignment assignment) => TryBuildPickupAdvance(assignment,out _);
        private bool TryBuildPickupAdvance(QueueAssignment assignment,out IReadOnlyList<Vector3> path)
        {
            path=null;
            if(!CanIntegratedProgress || !visits.TryGetValue(assignment.VisitId,out var visit) || visit.Actor==null ||
                !visit.Actor.isActiveAndEnabled) return false;
            // 使用真实旧身体与各自owned target；不从World中忽略前客或穿过其旧位置。
            var route=BuildPickupWaitRoute(visit.Actor.transform.position,assignment.Position,visit.Id,visit.Actor.Settings);
            if(route==null) return false;
            path=ExpandRoute(visit.Actor.transform.position,route);
            if(path==null) return false;
            if(!PickupCollectionClearsReturn(visit,path,assignment.Position)) return false;
            foreach(var other in visits.Values.Where(v=>v.Id!=visit.Id && v.RequestId!=0 && v.Actor!=null))
            {
                var remaining=ExpandRoute(other.Actor.transform.position,new[]{other.Actor.Agent.destination}
                    .Concat(other.Route??Enumerable.Empty<Vector3>()));
                if(remaining==null || RoutesConflict(path,remaining,RouteClearance(visit.Actor.Settings))) return false;
                var clearance=RouteClearance(visit.Actor.Settings)+2*visit.Actor.Settings.MaxSpeed*
                    Mathf.Min(Time.deltaTime,visit.Actor.Settings.MaxSubstepSeconds*visit.Actor.Settings.MaxSubstepsPerFrame);
                if(!CustomerRoutePlanner.IsClear(remaining,new[]{assignment.Position},clearance)) return false;
            }
            return true;
        }
        private float DepartureDistance(Visit visit) => 2*visit.Actor.Settings.AgentRadius+visit.Actor.Settings.CollisionSkin+
            visit.Actor.Settings.Epsilon+visit.Actor.Settings.ArrivalDistance;
        private static bool IsPickupMovement(CustomerVisitState state) => state==CustomerVisitState.MovingToPickup ||
            state==CustomerVisitState.PickupAdvancing || state==CustomerVisitState.MovingToPickupService;
        private IReadOnlyList<Vector3> ExpandRoute(Vector3 start,IEnumerable<Vector3> targets)
        {
            var points=new List<Vector3>{start};
            foreach(var target in targets)
            {
                var path=adapter.GetCompletePath(points[points.Count-1],target);
                if(path==null || path.Count==0) return null;
                points.AddRange(path.Skip(1));
            }
            return points;
        }
        private static bool RoutesConflict(IReadOnlyList<Vector3> follower,IReadOnlyList<Vector3> leader,float clearance)
        {
            for(var i=1;i<follower.Count;i++) for(var j=1;j<leader.Count;j++)
            {
                var a=follower[i]-follower[i-1]; var b=leader[j]-leader[j-1]; a.y=b.y=0;
                if(a.sqrMagnitude<.000001f || b.sqrMagnitude<.000001f || Vector3.Dot(a.normalized,b.normalized)>=.5f) continue;
                var offset=leader[j-1]-follower[i-1]; var cross=a.x*b.z-a.z*b.x;
                if(Mathf.Abs(cross)>.000001f)
                {
                    var t=(offset.x*b.z-offset.z*b.x)/cross; var u=(offset.x*a.z-offset.z*a.x)/cross;
                    if(t>=0 && t<=1 && u>=0 && u<=1) return true;
                }
                if(!CustomerRoutePlanner.IsClear(new[]{follower[i-1],follower[i]},new[]{leader[j-1],leader[j]},clearance) ||
                    !CustomerRoutePlanner.IsClear(new[]{leader[j-1],leader[j]},new[]{follower[i-1],follower[i]},clearance)) return true;
            }
            return false;
        }
        private static bool CanFollowEntryBend(IReadOnlyList<Vector3> follower,IReadOnlyList<Vector3> leader,float clearance,float tolerance)
        {
            if(follower.Count<2 || leader.Count<2) return false;
            var segment=0; var progress=0f;
            // 同一弯道按顺序跟随：前客必须已在后客路线前方，不能借此放行横穿/对向。
            // Match the shared corridor in order; P11 still guards every actual body displacement.
            for(var i=1;i<follower.Count;i++)
            {
                var delta=follower[i]-follower[i-1]; delta.y=0;
                var ahead=leader[0]-follower[i-1]; ahead.y=0;
                var t=delta.sqrMagnitude<.000001f?0:Mathf.Clamp01(Vector3.Dot(ahead,delta)/delta.sqrMagnitude);
                if(ahead.magnitude>0 && (ahead-delta*t).magnitude<=tolerance &&
                    progress+delta.magnitude*t>=clearance && SameDirection(delta,leader[1]-leader[0]))
                { segment=i; break; }
                progress+=delta.magnitude;
            }
            if(segment==0) return false;
            var branch=leader.Count-1;
            for(var j=1;j<leader.Count-1;j++)
            {
                // Native paths may split a straight edge; never skip a follower bend.
                if(!MatchesEdge(follower,segment,leader[j-1],leader[j],tolerance))
                {
                    if(segment+1<follower.Count && MatchesEdge(follower,segment+1,leader[j-1],leader[j],tolerance)) segment++;
                    else { branch=j; break; }
                }
            }
            // 末端绕开前方站位时可能多一个拐点；只允许共同走廊后的两段有限分流。
            var fork=leader[branch-1];
            if(CustomerRoutePlanner.IsClear(new[]{follower[segment-1],follower[segment]},new[]{fork},tolerance)) return false;
            if(Vector3.Distance(fork,follower[segment])<=tolerance && segment<follower.Count-1) segment++;
            if(leader.Count-branch>2 || follower.Count-segment>2) return false;
            var forward=leader[branch]-fork; forward.y=0;
            if(forward.sqrMagnitude<.000001f) return false;
            for(var i=branch;i<leader.Count;i++)
            {
                var delta=leader[i]-leader[i-1]; delta.y=0;
                if(delta.sqrMagnitude<.000001f || Vector3.Dot(delta.normalized,forward.normalized)<.5f ||
                    Vector3.Dot(leader[i-1]-fork,delta)<-.000001f) return false;
            }
            for(var i=segment;i<follower.Count;i++)
            {
                var from=i==segment?fork:follower[i-1]; var delta=follower[i]-from; delta.y=0;
                if(delta.sqrMagnitude<.000001f || Vector3.Dot(delta.normalized,forward.normalized)<.5f ||
                    Vector3.Dot(from-fork,delta)<-.000001f) return false;
            }
            // A genuine interior crossing remains a conflict even if the paths later join.
            for(var i=1;i<follower.Count;i++) for(var j=1;j<leader.Count;j++)
            {
                var a=follower[i]-follower[i-1]; var b=leader[j]-leader[j-1];
                var offset=leader[j-1]-follower[i-1]; var cross=a.x*b.z-a.z*b.x;
                if(Mathf.Abs(cross)<.000001f) continue;
                var t=(offset.x*b.z-offset.z*b.x)/cross; var u=(offset.x*a.z-offset.z*a.x)/cross;
                if(t>.001f && t<.999f && u>.001f && u<.999f) return false;
            }
            return true;
        }
        private static bool MatchesEdge(IReadOnlyList<Vector3> path,int segment,Vector3 from,Vector3 to,float tolerance)
            => SameDirection(path[segment]-path[segment-1],to-from) &&
                !CustomerRoutePlanner.IsClear(new[]{path[segment-1],path[segment]},new[]{from},tolerance) &&
                !CustomerRoutePlanner.IsClear(new[]{path[segment-1],path[segment]},new[]{to},tolerance);
        private static bool SameDirection(Vector3 a,Vector3 b)
        { a.y=b.y=0; return a.sqrMagnitude>.000001f && b.sqrMagnitude>.000001f && Vector3.Dot(a.normalized,b.normalized)>=.9f; }
        private float RouteClearance(NavigationSettings settings)
            // 路径验证针对已知角色中心；ArrivalDistance不是身体半径。
            // 实际移动仍由P11连续碰撞约束，slot/admission另保留到站误差。
            => settings.AgentRadius+world.RegisteredActors.Where(a=>a!=null).Select(a=>a.Settings.AgentRadius)
                .DefaultIfEmpty(settings.AgentRadius).Max()+settings.CollisionSkin+settings.Epsilon;
        private void BlockVisit(Visit visit,CustomerVisitState phase,string reason)
        {
            visit.PendingExitSegment=visit.PendingPickupSegment=false; visit.RequestId=0; visit.State=CustomerVisitState.Blocked; recoveringQueue=false;
            visit.Failure=(phase==CustomerVisitState.Exiting?"离场":phase==CustomerVisitState.Advancing?"前移":"入店")+"："+reason;
            if(phase==CustomerVisitState.Advancing)
                foreach(var follower in visits.Values.Where(v=>v.State==CustomerVisitState.Advancing &&
                    v.Assignment.SlotIndex>visit.Assignment.SlotIndex).ToArray())
                    HoldVisit(follower); // 前客失败，退休并发后客请求，保留实际位置/容量。
        }
        private bool CanWaitForDepartingExitBody(Visit visit)
        {
            if(!integratedMode || !visit.HasCollected || visit.ExitSequence==0 || visit.ResumeState!=CustomerVisitState.Exiting || visit.Actor==null) return false;
            var staticPath=adapter.GetCompletePath(visit.Actor.transform.position,visit.FinalTarget.Position);
            if(staticPath==null || staticPath.Count==0) return false;
            var clearance=RouteClearance(visit.Actor.Settings);
            return visits.Values.Any(earlier=>earlier!=visit && earlier.HasCollected && earlier.ExitSequence>0 && earlier.ExitSequence<visit.ExitSequence && earlier.State==CustomerVisitState.Exiting &&
                earlier.RequestId!=0 && earlier.Actor!=null && earlier.Actor.isActiveAndEnabled &&
                Vector3.Distance(earlier.FinalTarget.Position,visit.FinalTarget.Position)<.001f &&
                Vector3.Distance(earlier.Actor.transform.position,visit.FinalTarget.Position)<clearance);
        }
        private bool CanWaitForIndependentPickupExitTraffic(Visit visit)
        {
            if(!CanIntegratedProgress || !visit.HasCollected || visit.ExitSequence==0 || visit.State!=CustomerVisitState.Exiting ||
                visit.Actor==null || !visit.Actor.isActiveAndEnabled) return false;
            var occupied=RouteObstacles(visit.Id);
            var retainsLeg=visit.Route!=null && visit.Route.Count>0;
            var nextPath=retainsLeg?adapter.GetCompletePath(visit.Actor.transform.position,visit.Route.Peek()):null;
            var clearance=RouteClearance(visit.Actor.Settings);
            if((retainsLeg && (nextPath==null || nextPath.Count==0)) || (!retainsLeg &&
                PlanPickupExit(visit.Actor.transform.position,visit.FinalTarget.Position,occupied,visit.Actor.Settings)!=null)) return false;
            foreach(var moving in visits.Values.Where(v=>v!=visit && v.RequestId!=0 && v.Actor!=null && v.Actor.isActiveAndEnabled &&
                (IsPickupMovement(v.State) || v.State==CustomerVisitState.Exiting && v.HasCollected && v.ExitSequence<visit.ExitSequence)))
            {
                var agent=moving.Actor.Agent;
                if(agent==null || !agent.enabled || !agent.isOnNavMesh ||
                    moving.PickupOwner!=null && moving.PickupOwner.TryGetServiceLease(moving.Id,out _)) continue;
                var remaining=ExpandRoute(moving.Actor.transform.position,
                    new[]{moving.CurrentSegmentTarget.Position}.Concat(moving.Route??Enumerable.Empty<Vector3>()));
                if(remaining==null || !CustomerRoutePlanner.IsClear(remaining,RouteObstacles(moving.Id),RouteClearance(moving.Actor.Settings))) continue;
                // Keep its owned future slot: a body that will park on the exit is not finite traffic.
                if(IsPickupMovement(moving.State) && Vector3.Distance(moving.Assignment.Position,moving.Actor.transform.position)<.001f) continue;
                var withoutBody=occupied.Where(p=>Vector3.Distance(p,moving.Actor.transform.position)>.001f).ToArray();
                // Relevance is specific to this failed, complete native next leg.
                // 只有此身体单独挡住未发下腿；无关moving body与静态无路均不授权等待。
                if(retainsLeg && (CustomerRoutePlanner.IsClear(nextPath,new[]{moving.Actor.transform.position},clearance) ||
                    !CustomerRoutePlanner.IsClear(nextPath,withoutBody,clearance))) continue;
                // This counterfactual only permits WAIT, never a navigation request.
                // 仅去掉一个独立accepted身体后原完整出口可行；静态无路仍明确Blocked。
                var afterRemoval=PlanPickupExit(visit.Actor.transform.position,visit.FinalTarget.Position,withoutBody,visit.Actor.Settings);
                if(afterRemoval==null) continue;
                if(retainsLeg) return true;
                var complete=ExpandRoute(visit.Actor.transform.position,afterRemoval);
                if(complete!=null && complete.Count>0 && !CustomerRoutePlanner.IsClear(complete,
                    new[]{moving.Actor.transform.position},clearance)) return true;
            }
            return false;
        }
        private bool ExitRouteConflictsWithEarlierRequest(Visit visit)
        {
            if(!integratedMode || !visit.HasCollected || visit.ExitSequence==0 || visit.Route==null) return false;
            var proposed=ExpandRoute(visit.Actor.transform.position,visit.Route);
            if(proposed==null) return false;
            foreach(var earlier in visits.Values.Where(v=>v!=visit && v.HasCollected && v.ExitSequence>0 && v.ExitSequence<visit.ExitSequence &&
                v.State==CustomerVisitState.Exiting && v.RequestId!=0 && v.Actor!=null && v.Actor.isActiveAndEnabled))
            {
                var remaining=ExpandRoute(earlier.Actor.transform.position,new[]{earlier.Actor.Agent.destination}.Concat(earlier.Route??Enumerable.Empty<Vector3>()));
                if(remaining!=null && RoutesConflict(proposed,remaining,RouteClearance(visit.Actor.Settings))) return true;
            }
            return false;
        }
        private void SendRouteSegment(Visit visit,CustomerVisitState phase)
        {
            visit.PendingPickupSegment=false;
            // 未发出下一段请求时，前客仍在共同出口完成真实Arrived可普通等候。
            // No segment/replan budget is consumed, and real issued-request failures remain unchanged.
            if(phase==CustomerVisitState.Exiting && (ExitRouteConflictsWithEarlierRequest(visit) || CanWaitForDepartingExitBody(visit) &&
                PlanPickupExit(visit.Actor.transform.position,visit.FinalTarget.Position,RouteObstacles(visit.Id),visit.Actor.Settings)==null))
            { visit.PendingExitSegment=true; return; }
            visit.PendingExitSegment=false;
            var point=visit.Route.Peek();
            // 其他顾客可能已移动；每段从实际位置验证，必要时有限重算整条剩余路线。
            var nextPath=adapter.GetCompletePath(visit.Actor.transform.position,point);
            if(!CustomerRoutePlanner.IsClear(nextPath,RouteObstacles(visit.Id),RouteClearance(visit.Actor.Settings)))
            {
                if(phase==CustomerVisitState.Exiting && CanWaitForIndependentPickupExitTraffic(visit))
                { visit.PendingExitSegment=true; return; } // Keep this unissued leg and its original budgets.
                if(integratedMode && IsPickupMovement(phase) && visit.RequestId==0 && CanWaitForMovingPickupBodies(visit,nextPath))
                { visit.PendingPickupSegment=true; return; }
                IReadOnlyList<Vector3> replanned=null;
                if(++visit.Replans<=3)
                    replanned=phase==CustomerVisitState.MovingToPickup || phase==CustomerVisitState.PickupAdvancing
                        ?BuildPickupWaitRoute(visit.Actor.transform.position,visit.FinalTarget.Position,visit.Id,visit.Actor.Settings)
                        :integratedMode && phase==CustomerVisitState.Exiting
                        ?PlanPickupExit(visit.Actor.transform.position,visit.FinalTarget.Position,RouteObstacles(visit.Id),visit.Actor.Settings)
                        :BuildRoute(visit.Actor.transform.position,visit.FinalTarget.Position,visit.Id,visit.Actor.Settings);
                if(replanned==null || replanned.Count>24) { BlockVisit(visit,phase,"通道被占用"); return; }
                visit.Route=new Queue<Vector3>(replanned);
            }
            // 有界重算后的最终candidate也必须先通过owned-route门禁；等待保留该candidate。
            if(phase==CustomerVisitState.Exiting && ExitRouteConflictsWithEarlierRequest(visit))
            { visit.PendingExitSegment=true; return; }
            if(++visit.Segments>32) { BlockVisit(visit,phase,"绕行次数已达上限"); return; }
            point=visit.Route.Dequeue(); var final=visit.Route.Count==0;
            var target=final?visit.FinalTarget:new NavigationTarget(point);
            var path=adapter.GetCompletePath(visit.Actor.transform.position,target.Position);
            if(path==null || path.Count==0) { BlockVisit(visit,phase,"路径不可达"); return; }
            var endpoint=path[path.Count-1];
            var generation=++visit.Generation; var layout=adapter.Revision;
            visit.RequestId=0; visit.State=visit.ResumeState=phase;
            visit.CurrentSegmentTarget=target;
            var start=world.Service.MoveTo(visit.Id,target,result=>
            {
                if(!visits.ContainsKey(visit.Id) || generation!=visit.Generation || layout!=adapter.Revision) return;
                visit.RequestId=0;
                if(result.Status!=MovementStatus.Arrived) { BlockVisit(visit,phase,result.Reason==NavigationFailure.MovementTimeout?"移动超时":result.Status==MovementStatus.Recovered?"原目标尚未到达":"路径不可达"); return; }
                // 临时waypoint沿用Service到站容差；完整skin可能使拐点无法精确抵达。
                // 下一段从实际pose重新查询/验证，不把临时Arrived当作Queued或瞬移到拐点。
                var remaining=result.FinalPosition-endpoint; remaining.y=0;
                if(!final && remaining.magnitude>visit.Actor.Settings.ArrivalDistance)
                { BlockVisit(visit,phase,"尚未抵达绕行拐点"); return; }
                if(!final) { SendRouteSegment(visit,phase); return; }
                if(phase==CustomerVisitState.Exiting)
                {
                    if(integratedMode)
                    {
                        visit.PickupDeparted=true; visit.Admission.LeavePickup();
                        visit.PickupOwner?.Remove(visit.Id); visit.PickupOwner?.ReleaseServicePosition(visit.Id);
                        exitSnapshots.Add(CafeVisit(visit,true));
                    }
                    RemoveVisit(visit.Id); return;
                }
                if(integratedMode && IsPickupMovement(phase))
                {
                    if(visit.DeliveryYieldPhase==1)
                    {
                        if(!visit.PickupOwner.MarkYieldArrived(visit.Id,visit.Assignment.Generation))
                        { BlockVisit(visit,phase,"让位站位提交失败");return; }
                        visit.DeliveryYieldPhase=2;visit.State=CustomerVisitState.PickupWaiting;return;
                    }
                    if(!visit.PickupOwner.MarkArrived(visit.Id,visit.Assignment.Generation) || !visit.Admission.MarkPickupWaiting())
                    { BlockVisit(visit,phase,"取餐站位提交失败"); return; }
                    visit.PickupArrived=true;
                    visit.DeliveryYieldPhase=0;
                    visit.ServiceArrived=phase==CustomerVisitState.MovingToPickupService;
                    visit.State=visit.ServiceArrived?CustomerVisitState.PickupService:CustomerVisitState.PickupWaiting;
                    return;
                }
                if(queue.Complete(visit.Id,visit.Assignment.Generation) && visit.Admission.MarkQueued())
                { visit.State=CustomerVisitState.Queued; visit.CounterArrived=true; visit.CounterPosition=visit.Actor.transform.position; }
                else BlockVisit(visit,phase,"站位提交失败");
            });
            // 同步callback可能已经将状态置为Blocked/Removed，不用返回值复活。
            if(visits.ContainsKey(visit.Id) && visit.Generation==generation && visit.State==phase)
            { if(start.Accepted) visit.RequestId=start.RequestId; else BlockVisit(visit,phase,"移动请求未接受"); }
        }
        private bool CanWaitForMovingPickupBodies(Visit visit,IReadOnlyList<Vector3> path)
        {
            if(path==null || path.Count==0) return false; // Static owned path must already be complete.
            var clearance=RouteClearance(visit.Actor.Settings);
            // A reserved target is not a moving body / 不把尚未腾空的正式目标当作活动交通。
            if(!CustomerRoutePlanner.IsClear(path,visits.Values.Where(v=>v.Id!=visit.Id && ReservesRouteTarget(v,visit.Id,null))
                .Select(v=>v.Assignment.Position).ToArray(),clearance)) return false;
            var found=false;
            foreach(var actor in world.RegisteredActors)
            {
                if(actor==null || actor==visit.Actor || CustomerRoutePlanner.IsClear(path,new[]{actor.transform.position},clearance)) continue;
                found=true;
                var agent=actor.Agent;
                // P11 Finish/Cancel/failure stops and resets the actual Agent path. No request is fabricated here.
                if(!actor.isActiveAndEnabled || agent==null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped ||
                    !agent.hasPath || agent.pathPending || agent.pathStatus!=UnityEngine.AI.NavMeshPathStatus.PathComplete ||
                    !CustomerRoutePlanner.IsClear(path,new[]{agent.destination},clearance)) return false;
            }
            return found; // Never turn a static/reservation-only obstruction into an unbounded wait.
        }
        public bool TryLetFrontLeave()
        {
            if(integratedMode || !isActiveAndEnabled || !Configured || !world.LayoutAvailable || queue==null || (decoration!=null && decoration.IsOpen) ||
                visits.Values.Any(v=>v.State==CustomerVisitState.Entering || v.State==CustomerVisitState.Advancing ||
                    v.State==CustomerVisitState.Exiting) || // 拦截活动移动；原位Blocked尾客仍允许安全队首离场。
                !queue.TryBeginFrontExit(out var id) || !visits.TryGetValue(id,out var visit)) return false;
            visit.CounterPosition=visit.Actor.transform.position; visit.Admission.CancelUnusedPickup();
            Send(visit,CustomerVisitState.Exiting,new NavigationTarget(exit.position)); return true;
        }
        public void RemoveVisit(string id)
        {
            if(!visits.TryGetValue(id,out var visit)) return;
            visits.Remove(id); visit.Generation++; visit.State=CustomerVisitState.Removed;
            visit.PendingPickupSegment=false;
            if(visit.RequestId!=0) world?.Service.Cancel(visit.RequestId);
            visit.PickupOwner?.Remove(id); visit.PickupOwner?.ReleaseServicePosition(id);
            queue?.Remove(id); if(visit.Actor!=null) { world?.Unregister(visit.Actor); Destroy(visit.Actor.gameObject); }
            visit.Admission.Dispose();
        }
        private void ResetCustomersAfterDecoration()
        {
            // 只退休本控制器拥有的顾客；员工/其他World角色独立保留 / customers only.
            foreach(var id in visits.Keys.ToArray()) RemoveVisit(id);
            queue=null; revision=-1; suspended=true; recoveringQueue=false;
            admissionRejected=admissionRouteUnavailable=false; SelectedRegisterId="";
            clock?.Tick(0,false,false);
            // 保留Capacity对象与递增visit ID；下一次有效营业重新抽取完整随机间隔。
        }
        private void HoldVisits()
        {
            deliveryTrafficPaths=null;
            suspended=true;
            foreach(var visit in visits.Values.ToArray())
            {
                visit.YieldReturnTraffic=null;
                if(visit.State==CustomerVisitState.Queued || visit.State==CustomerVisitState.Blocked ||
                    visit.State==CustomerVisitState.PickupWaiting || visit.State==CustomerVisitState.PickupService) continue;
                HoldVisit(visit);
            }
        }
        private void HoldVisit(Visit visit)
        {
            visit.PendingExitSegment=visit.PendingPickupSegment=false; visit.Generation++; var request=visit.RequestId; visit.RequestId=0; visit.State=CustomerVisitState.Blocked;
            if(request!=0) world?.Service.Cancel(request);
        }
        public void ResumeBlockedVisits()
        {
            if(!isActiveAndEnabled || !Configured || !world.LayoutAvailable || queue==null) return;
            if(integratedMode && (time.CurrentSpeed==GameSpeed.Paused || time.IsResumeBlocked ||
                decoration!=null && decoration.IsOpen)) return;
            integratedHeld=false;
            HoldVisits(); suspended=false; queue.RestartAssignments(); recoveringQueue=true;
            foreach(var visit in visits.Values.ToArray())
            {
                if(visit.Actor==null || !visit.Actor.isActiveAndEnabled) continue;
                if(visit.ResumeState==CustomerVisitState.Exiting)
                    Send(visit,CustomerVisitState.Exiting,new NavigationTarget(exit.position));
                else if(integratedMode && visit.State==CustomerVisitState.Blocked && IsPickupMovement(visit.ResumeState))
                    Send(visit,visit.ResumeState,visit.FinalTarget);
                else if(integratedMode && (visit.State==CustomerVisitState.PickupWaiting || visit.State==CustomerVisitState.PickupService)) continue;
                else if(visit.State!=CustomerVisitState.Queued) visit.State=CustomerVisitState.Blocked;
            }
        }
        private void OnDisable() { HoldVisits(); clock?.Tick(0,false,false); }
        private void OnEnable() { suspended=true; }
        private void OnDestroy()
        { disposed=true; foreach(var id in visits.Keys.ToArray()) RemoveVisit(id); Capacity=null; queue=null; pickupQueue=null;pickupPlanningRejections.Clear(); exitSnapshots.Clear(); }
    }
}


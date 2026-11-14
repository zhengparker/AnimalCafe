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
using AnimalCafe.Employees;
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
        [SerializeField] private bool requireAdmissionPolicy;
        private UnityEngine.Object admissionOwner;
        private Func<bool> canAdmit;
        private string admissionPolicyFailure="";
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
            // Only a real successful unpaid reservation owns this admission corridor.
            // 绑定取餐assignment，不能使用仍指向收银台的Assignment或逐段导航Generation。
            public QueueAssignment? ReservedPickup;
            public PickupQueueService ReservedPickupOwner;
            public int ReservedPickupRevision;
            public IReadOnlyList<Vector3> ReservedPickupPath;
            public IReadOnlyList<Vector3> ReservedPickupRoute;
            public IReadOnlyList<Vector3> OwnedRoutePath;
            public PickupQueueService OwnedRouteOwner;
            public Queue<Vector3> OwnedRouteSource;
            public QueueAssignment OwnedRouteAssignment;
            public CustomerVisitState OwnedRoutePhase;
            public int OwnedRouteRevision,OwnedRouteSegments;
            public List<int> OwnedRouteLegEnds;
            public FiniteRouteProof OwnedWaitProof;
            public long OwnedIssuedRequest;
            public int OwnedIssuedGeneration;
            public bool OwnedRouteWasClear;
            public long EmployeeExitWaitAttempt;
            public NavigationActor EmployeeExitWaitActor;
            public int EmployeeExitWaitGeneration,EmployeeExitWaitRevision;
            public Queue<Vector3> EmployeeExitWaitRoute;
            public PickupExitWait PickupPriorityWait;
            public Queue<Vector3> PickupPriorityCheckedRoute;
            public NavigationTarget FinalTarget;
            public NavigationTarget CurrentSegmentTarget;
            public Queue<Vector3> Route;
            public int Segments,Replans;
            public bool PendingExitSegment;
            public bool PendingPickupSegment;
            public int DeliveryYieldPhase; // 1 moving out, 2 held clear, 3 returning to the owned slot.
            public IReadOnlyList<Vector3>[] YieldReturnTraffic;
            public long ExitSequence;
            public CafeOrderKey? CollectionAttempt,CollectedOrder;
            public int CollectionAttemptFrame=-1,CollectionFrame=-1;
            public string Failure;
            public int TrafficPhaseSequence;
            public FailedCollectionRecovery FailedCollection;
            public TrafficIntentKey? TrafficJourney;
            public PendingCounterMove PendingCounter;
            public CounterAdvanceAnchor PendingCounterAdvance;
            public TrafficMovement TrafficMove;
            public TrafficSelection TrafficSelection;
            public TrafficPhaseReceipt IssuedTraffic,PendingTraffic;
            public readonly Dictionary<TrafficIntentKey,int> TrafficCandidateFrames=new Dictionary<TrafficIntentKey,int>();
            public bool TrafficMovePending;
            public TrafficDecision PickupTrafficPermit;
            public PickupParkingRight PickupParking;
            public PaidPickupTask PaidPickup;
            public PickupReservationAnchor PendingReservationAnchor;
            public PickupFamilyPreparation ReturnPreparation;
            public TrafficCandidateCursor YieldDeliverySource;
            public PickupDeliveryHistory ReturnHistory;
#if UNITY_EDITOR
            public long AdmissionObservation; // Diagnostic correlation only; never used by policy.
#endif
        }
        // Original producer identity only; capture does not refresh or grant traffic authority.
        // 身份归原 cursor/Visit；不复制 Request，也不以 capture 改变原规划/提交结果。
        // Inert M1 schema: only the real producer may later capture this original prerequisite.
        // 当前无构造/赋值/刷新调用；不能拿null Preview冒充已获Collection权限。
        private sealed class CollectionReleasePrerequisite
        {
            internal readonly CustomerFlowController Flow;
            internal readonly ServiceTrafficCoordinator Coordinator;
            internal readonly Visit Visit;
            internal readonly CustomerAdmission Admission;
            internal readonly CapacityToken Total;
            internal readonly CapacityService Capacity;
            internal readonly NavigationActor Actor,ReturnActor;
            internal readonly NavigationWorld World;
            internal readonly NavigationLayoutAdapter Adapter;
            internal readonly PickupQueueService Owner;
            internal readonly PickupQueueService.ReservationClaim Claim;
            internal readonly PickupQueueService.DeliveryLeaseClaim Lease;
            internal readonly PickupDeliveryHistory History;
            internal readonly TrafficCandidateCursor Cursor;
            internal readonly TrafficIntentKey Key,ReturnKey;
            internal readonly TrafficDecision ReturnSource;
            internal readonly TrafficRouteProof ReturnProof;
            internal readonly TrafficNativeStamp ReturnStamp;
            internal readonly QueueAssignment Assignment;
            internal readonly TrafficSelection Selection;
            internal readonly TrafficPhaseReceipt Issued,Pending;
            internal readonly TrafficMovement Movement;
            internal readonly long OwnerLife;
            internal readonly int FirstFrame,CandidateIndex,Generation,PhaseSequence;
            internal readonly Vector3 Start,Target;
            internal readonly float Radius,Skin,Epsilon;
            internal readonly IReadOnlyList<TrafficRouteRequirement> FixedRequirements;
            internal PickupQueueService.PreparedMutation CurrentPreview;
            internal CollectionReleasePrerequisite(CustomerFlowController flow,Visit visit,TrafficCandidateCursor cursor,int candidate,
                PickupQueueService.ReservationClaim claim,PickupQueueService.DeliveryLeaseClaim lease,PickupDeliveryHistory history,
                TrafficDecision source,CapacityToken total)
            {
                Flow=flow;Coordinator=flow.traffic;Visit=visit;Admission=visit.Admission;Total=total;Capacity=flow.Capacity;Actor=visit.Actor;
                World=flow.world;Adapter=flow.adapter;Owner=visit.PickupOwner;Claim=claim;Lease=lease;History=history;Cursor=cursor;
                Key=cursor.Key;ReturnKey=history.Key;ReturnActor=history.Actor;ReturnSource=source;ReturnProof=history.ReturnProof;ReturnStamp=history.ReturnStamp;
                Assignment=claim.Assignment;Selection=visit.TrafficSelection;Issued=visit.IssuedTraffic;Pending=visit.PendingTraffic;Movement=visit.TrafficMove;
                OwnerLife=flow.pickupPreparationLife;FirstFrame=flow.CustomerSourceFrame(visit,Key);CandidateIndex=candidate;
                Generation=visit.Generation;PhaseSequence=visit.TrafficPhaseSequence;Start=Actor.transform.position;Target=cursor.Sides[candidate].Position;
                Radius=Actor.Settings.AgentRadius;Skin=Actor.Settings.CollisionSkin;Epsilon=Actor.Settings.Epsilon;
                FixedRequirements=Array.AsReadOnly(new[]{new TrafficRouteRequirement("collection-exit-capability",Target,flow.exit.position,
                    flow.RouteClearance(Actor.Settings),avoidProposedParking:false)});
            }
        }
        // Private producer seal only: no route, lease or permission is manufactured by this checkpoint.
        private sealed class FailedCollectionRecovery
        {
            internal readonly PickupFamilyPreparation Original;
            internal readonly TrafficDecision Failure;
            internal readonly CafeOrderKey Order;
            internal readonly GameObject Cup;
            internal readonly Func<string,CafeOrderKey,GameObject> Provider;
            internal bool Resuming,Started;
            internal TrafficIntentKey ActiveKey;
            internal FailedCollectionRecovery(PickupFamilyPreparation original,TrafficDecision failure,CafeOrderKey order,
                GameObject cup,Func<string,CafeOrderKey,GameObject> provider)
            {Original=original;Failure=failure;Order=order;Cup=cup;Provider=provider;}
        }
        private Func<string,CafeOrderKey,GameObject> collectionRecoveryCup;
        internal void SetCollectionRecoveryCupProvider(Func<string,CafeOrderKey,GameObject> provider)=>collectionRecoveryCup=provider;
        internal void ClearCollectionRecoveryCupProvider(Func<string,CafeOrderKey,GameObject> provider)
        {if(collectionRecoveryCup==provider)collectionRecoveryCup=null;}
        private bool StopOrdinaryCollectionIfTerminal(PickupFamilyPreparation p,TrafficDecision failure,out string reason)
        {
            reason="";
            if(!CanIntegratedProgress || p==null || p.Operation!=PickupQueueService.MutationKind.CollectionLease ||
                p.Cursor==null || p.Cursor.CollectionRelease!=null || !PickupFamilyPreparationCurrent(p) || failure==null ||
                !failure.Intent.Equals(p.Key) || !ReferenceEquals(traffic.GetDecision(p.Key),failure) ||
                failure.FailureDisposition!=TrafficFailureDisposition.Terminal || failure.AcquiredSequence!=0 ||
                traffic.HasActiveGrant(p.Key) || traffic.TryGetCommittedJourney(p.Key,out _))return false;
            var visit=p.Visit;var provider=collectionRecoveryCup;var paid=visit.PaidPickup;
            // Keep a failure category even without a recovery credential: generic Resume must never bypass this gate.
            var saved=new FailedCollectionRecovery(p,failure,paid==null?default:paid.Order,null,provider);
            if(paid!=null && paid.Transferred && !paid.Failed && provider!=null)
            {
                GameObject cup=null;try{cup=provider(visit.Id,paid.Order);}catch(Exception){ /* Missing/unknown presentation cannot authorize recovery. */ }
                if(cup!=null && cup.activeInHierarchy && ReferenceEquals(provider,collectionRecoveryCup) &&
                    PickupFamilyPreparationCurrent(p) && ReferenceEquals(visit.PaidPickup,paid) &&
                    ReferenceEquals(traffic.GetDecision(p.Key),failure))saved=new FailedCollectionRecovery(p,failure,paid.Order,cup,provider);
            }
            // A missing Cup provider affects recovery only; an authentic Terminal still becomes visible Blocked.
            if(!CanIntegratedProgress || !PickupFamilyPreparationCurrent(p) || !ReferenceEquals(traffic.GetDecision(p.Key),failure))return false;
            reason="Collection traffic "+failure.Kind+": "+failure.Reason;
            trafficCandidates.Remove(p.Key);BlockVisit(visit,CustomerVisitState.MovingToPickupService,reason);
            if(saved!=null && visits.TryGetValue(visit.Id,out var live) && ReferenceEquals(live,visit) &&
                visit.State==CustomerVisitState.Blocked && visit.Generation==p.Generation && visit.Actor==p.Actor)visit.FailedCollection=saved;
            return true;
        }
        private bool FailedCollectionIdentityCurrent(Visit visit,FailedCollectionRecovery saved)
        {
            try
            {
                var p=saved?.Original;
                return CanIntegratedProgress && p!=null && !saved.Started && ReferenceEquals(visit.FailedCollection,saved) &&
                    visit.State==CustomerVisitState.Blocked && ReferenceEquals(p.Flow,this) && !disposed && p.OwnerLife==pickupPreparationLife &&
                    ReferenceEquals(traffic,p.Coordinator) && p.Key.Session==trafficSession && p.Key.Epoch==trafficEpoch &&
                    ReferenceEquals(adapter,p.Adapter) && adapter.Revision==p.Key.LayoutRevision && ReferenceEquals(world,p.World) &&
                    world.LayoutAvailable && ReferenceEquals(Capacity,p.Capacity) && ReferenceEquals(p.Owner,pickupQueue) &&
                    visits.TryGetValue(visit.Id,out var live) && ReferenceEquals(live,visit) && ReferenceEquals(p.Visit,visit) &&
                    ReferenceEquals(visit.Actor,p.Actor) && ReferenceEquals(visit.Admission,p.Admission) && TrafficActorReady(p.Actor) &&
                    p.Actor.transform.position.Equals(p.Start) && p.Actor.Settings.AgentRadius==p.Radius && p.Actor.Settings.CollisionSkin==p.Skin &&
                    p.Actor.Settings.Epsilon==p.Epsilon && visit.Generation==p.Generation && visit.TrafficPhaseSequence==p.PhaseSequence &&
                    visit.RequestId==0 && !visit.TrafficMovePending && visit.TrafficMove==null && visit.IssuedTraffic==null && visit.PendingTraffic==null &&
                    visit.DeliveryYieldPhase==0 && visit.PickupAssigned && !visit.PickupDeparted && !visit.HasCollected &&
                    ReferenceEquals(visit.PickupOwner,p.Owner) && p.Claim.IsCurrent && p.Owner.TryCaptureReservation(visit.Id,out var claim) &&
                    p.Claim.IsSameCurrentClaim(claim) && p.Owner.TryGetReservation(visit.Id,out var row) && row.Equals(p.Assignment) && row.Arrived &&
                    p.Preview!=null && !p.Preview.Consumed && p.Operation==PickupQueueService.MutationKind.CollectionLease &&
                    pickupSides.Any(s=>s.Position.Equals(p.Target)) && visit.PaidPickup!=null && visit.PaidPickup.Transferred && !visit.PaidPickup.Failed &&
                    visit.PaidPickup.Order.Equals(saved.Order) && saved.Cup!=null && saved.Cup.activeInHierarchy &&
                    ReferenceEquals(collectionRecoveryCup,saved.Provider) && ReferenceEquals(traffic.GetDecision(p.Key),saved.Failure) &&
                    saved.Failure.FailureDisposition==TrafficFailureDisposition.Terminal && saved.Failure.AcquiredSequence==0 &&
                    Capacity.GetReservations().Any(t=>ReferenceEquals(t.Token,p.Total) && t.OwnerId==visit.Id &&
                        t.Kind==CapacityKind.TotalCustomers && t.State==ReservationState.Occupied);
            }
            catch(Exception){return false;}
        }
        private bool IsActiveCollectionRecovery(Visit visit)
        {
            var saved=visit.FailedCollection;
            if(!CanIntegratedProgress || adapter==null || visit.Actor==null || !TrafficActorReady(visit.Actor) ||
                saved==null || !saved.Started || visit.HasCollected || visit.State==CustomerVisitState.Blocked ||
                visit.State!=CustomerVisitState.PickupWaiting && visit.State!=CustomerVisitState.MovingToPickupService && visit.State!=CustomerVisitState.PickupService ||
                !ReferenceEquals(visit.Actor,saved.Original.Actor) || !ReferenceEquals(traffic,saved.Original.Coordinator) ||
                saved.ActiveKey.Session!=trafficSession || saved.ActiveKey.Epoch!=trafficEpoch || saved.ActiveKey.LayoutRevision!=adapter.Revision)return false;
            var current=traffic.GetDecision(saved.ActiveKey);
            return current.FailureDisposition==TrafficFailureDisposition.None &&
                (current.Kind==TrafficDecisionKind.DeferredByBudget || current.Kind==TrafficDecisionKind.WaitingForTraffic || current.Kind==TrafficDecisionKind.Granted);
        }
        private bool TryResumeOrdinaryCollection(Visit visit)
        {
            var saved=visit.FailedCollection;if(saved==null || saved.Resuming || !FailedCollectionIdentityCurrent(visit,saved))return false;
            saved.Resuming=true;
            try
            {
                var p=saved.Original;GameObject cup=null;try{cup=saved.Provider(visit.Id,saved.Order);}catch(Exception){return false;}
                if(cup==null || !ReferenceEquals(cup,saved.Cup) || !FailedCollectionIdentityCurrent(visit,saved) ||
                    !p.Owner.TryPrepareMutation(PickupQueueService.MutationKind.CollectionLease,visit.Id,p.Owner.Version,out var preview,p.Target) ||
                    !PickupFamilyPreviewMatches(p,preview) || !FailedCollectionIdentityCurrent(visit,saved))return false;
                var logical=visit.Id+"/pickup-service/"+p.Key.Context;
                if(!trafficKeys.TryGetValue(logical,out var oldKey) || !oldKey.Equals(p.Key))return false;
                RefreshTrafficSnapshot();
                if(!FailedCollectionIdentityCurrent(visit,saved) || !preview.IsCurrent ||
                    !traffic.TryRetireFailedCanonicalPreparation(saved.Failure,p.Actor) || !FailedCollectionIdentityCurrent(visit,saved) || !preview.IsCurrent)return false;
                // Explicit Owner action creates a NEW finite attempt; old diagnostics/spent remain on the retired key.
                // 不是复活旧Terminal，不继承过期parent权限，也不重置旧budget。
                trafficKeys.Remove(logical);var key=TrafficKey(visit.Id,"pickup-service",p.Key.Context);
                var side=p.Cursor.Sides[p.CandidateIndex];var cursor=GetTrafficCursor(key,p.Actor,TrafficCandidateRole.Collection,new[]{side});
                saved.Started=true;saved.ActiveKey=key;visit.State=CustomerVisitState.PickupWaiting;visit.Failure=null;
                TryTrafficPickupMove(visit,PickupQueueService.MutationKind.CollectionLease,p.Target,"pickup-service",
                    CustomerVisitState.MovingToPickupService,"collection.published",out _,cursor:cursor,candidateIndex:0);
                if(visit.State==CustomerVisitState.PickupWaiting && cursor.Source==null)
                {BlockVisit(visit,CustomerVisitState.MovingToPickupService,"Explicit collection preparation unavailable");return false;}
                return true;
            }
            finally{saved.Resuming=false;}
        }
        private sealed class PickupFamilyPreparation
        {
            internal readonly CustomerFlowController Flow;
            internal readonly ServiceTrafficCoordinator Coordinator;
            internal readonly TrafficIntentKey Key;
            internal readonly Visit Visit;
            internal readonly NavigationActor Actor;
            internal readonly CustomerAdmission Admission;
            internal readonly CapacityToken Total;
            internal readonly CapacityService Capacity;
            internal readonly NavigationWorld World;
            internal readonly NavigationLayoutAdapter Adapter;
            internal readonly long OwnerLife;
            internal readonly PickupQueueService Owner;
            internal readonly PickupQueueService.ReservationClaim Claim;
            internal readonly QueueAssignment Assignment;
            internal readonly PickupQueueService.MutationKind Operation;
            internal readonly PickupQueueService.PreparedMutation Preview;
            internal PickupQueueService.PreparedMutation CurrentPreview;
            internal readonly CustomerVisitState Phase;
            internal readonly int Generation,PhaseSequence,YieldPhase,FirstFrame,CandidateIndex;
            internal readonly Vector3 Start,Target;
            internal readonly float Radius,Skin,Epsilon;
            internal readonly TrafficCandidateCursor Cursor,DeliveryCursor;
            internal readonly TrafficSelection Selection;
            internal readonly TrafficPhaseReceipt Issued,Pending;
            internal readonly TrafficMovement Movement;
            internal readonly IReadOnlyList<TrafficRouteRequirement> Requirements,FixedRequirements;
            internal readonly NavigationActor DeliveryActor;
            internal readonly NavigationTarget DeliveryWorkTarget;
            internal readonly EmployeeFlowController.PreparedDeliveryContext DeliveryContext;
            internal readonly PickupQueueService.DeliveryLeaseClaim DeliveryLease;
            internal readonly PickupDeliveryHistory History;
            internal readonly bool DeliveryAcquired,DeliveryWasAway;
            internal PickupFamilyPreparation(CustomerFlowController flow,Visit visit,TrafficIntentKey key,
                PickupQueueService.PreparedMutation preview,PickupQueueService.ReservationClaim claim,CapacityToken total,
                TrafficCandidateCursor cursor,int candidate,IEnumerable<TrafficRouteRequirement> requirements,TrafficCandidateCursor delivery,
                IEnumerable<TrafficRouteRequirement> fixedRequirements)
            {
                Flow=flow;Coordinator=flow.traffic;Key=key;Visit=visit;Actor=visit.Actor;Admission=visit.Admission;Total=total;
                Capacity=flow.Capacity;World=flow.world;Adapter=flow.adapter;OwnerLife=flow.pickupPreparationLife;
                Owner=visit.PickupOwner;Claim=claim;Assignment=claim.Assignment;Operation=preview.Kind;Preview=CurrentPreview=preview;
                Phase=visit.State;Generation=visit.Generation;PhaseSequence=visit.TrafficPhaseSequence;YieldPhase=visit.DeliveryYieldPhase;
                FirstFrame=visit.TrafficCandidateFrames.TryGetValue(key,out var frame)?frame:Time.frameCount;
                CandidateIndex=candidate;Start=Actor.transform.position;Target=preview.Assignment.Position;
                Radius=Actor.Settings.AgentRadius;Skin=Actor.Settings.CollisionSkin;Epsilon=Actor.Settings.Epsilon;
                Cursor=cursor;DeliveryCursor=delivery;Selection=visit.TrafficSelection;Issued=visit.IssuedTraffic;
                Pending=visit.PendingTraffic;Movement=visit.TrafficMove;Requirements=Array.AsReadOnly(requirements.ToArray());
                FixedRequirements=Array.AsReadOnly(fixedRequirements.ToArray());
                History=Operation==PickupQueueService.MutationKind.ReturnYield?visit.ReturnHistory:null;
                DeliveryContext=delivery?.DeliveryContext??History?.Context;
                DeliveryActor=DeliveryContext?.ActorLife as NavigationActor;DeliveryWorkTarget=flow.deliveryWorkTarget;
                DeliveryLease=History?.Lease;DeliveryAcquired=flow.deliveryAcquired;DeliveryWasAway=flow.deliveryWasAway;
            }
        }
        // One actual acquire/return obligation, retained after the delivery cursor retires.
        // lease 只能在真实 joint acquire 后捕获；自然释放后保留历史，不重新签发身份。
        private sealed class PickupDeliveryHistory
        {
            internal readonly ServiceTrafficCoordinator Coordinator;
            internal readonly TrafficIntentKey Key;
            internal readonly PickupQueueService Owner;
            internal readonly TrafficDecision Source;
            internal readonly PickupQueueService.DeliveryLeaseClaim Lease;
            internal readonly EmployeeFlowController.PreparedDeliveryContext Context;
            internal readonly NavigationActor Actor;
            internal readonly NavigationTarget Work;
            internal readonly NavigationWorld World;
            internal readonly NavigationLayoutAdapter Adapter;
            internal readonly long OwnerLife,Attempt;
            internal readonly float Radius,Skin,Epsilon;
            internal bool Delivered,WasAway,HomeObserved;
            internal TrafficMovement ReturnMovement;
            internal TrafficRouteProof ReturnProof;
            internal TrafficWorldSnapshot ReturnSnapshot;
            internal int ReturnGeneration;
            internal TrafficNativeStamp ReturnStamp;
            internal PickupDeliveryHistory(CustomerFlowController flow,TrafficCandidateCursor cursor,PickupQueueService.DeliveryLeaseClaim lease)
            {
                Coordinator=flow.traffic;Key=cursor.Key;Owner=flow.pickupQueue;Source=cursor.Source;Lease=lease;Context=cursor.DeliveryContext;
                Actor=cursor.ActorLife as NavigationActor;Work=flow.deliveryWorkTarget;World=flow.world;Adapter=flow.adapter;
                OwnerLife=flow.pickupPreparationLife;Attempt=flow.deliveryAttempt;
                Radius=Actor.Settings.AgentRadius;Skin=Actor.Settings.CollisionSkin;Epsilon=Actor.Settings.Epsilon;
            }
        }
        // Original release prerequisite owns no Queue permission; Core alone decides finite dependency waiting.
        // 原始前提不伪造 Preview；真实 lease 退休后才取得并提交 canonical mutation。
        private bool CollectionReleaseOwnerCurrent(CollectionReleasePrerequisite p)
        {
            try
            {
                if(p==null || disposed || !ReferenceEquals(p.Flow,this) || p.OwnerLife!=pickupPreparationLife ||
                    !ReferenceEquals(p.Coordinator,traffic) || p.Key.Session!=trafficSession || p.Key.Epoch!=trafficEpoch ||
                    !ReferenceEquals(p.World,world) || !ReferenceEquals(p.Adapter,adapter) || adapter==null || adapter.Revision!=p.Key.LayoutRevision ||
                    !ReferenceEquals(p.Capacity,Capacity) || !ReferenceEquals(p.Owner,pickupQueue) || !ReferenceEquals(p.Visit.PickupOwner,p.Owner) ||
                    !visits.TryGetValue(p.Visit.Id,out var visit) || !ReferenceEquals(visit,p.Visit) || !ReferenceEquals(visit.Admission,p.Admission) ||
                    !ReferenceEquals(visit.Actor,p.Actor) || p.Actor==null || !p.Actor.isActiveAndEnabled || !world.RegisteredActors.Contains(p.Actor) ||
                    visit.State!=CustomerVisitState.PickupWaiting || visit.DeliveryYieldPhase!=0 || visit.Generation!=p.Generation ||
                    visit.TrafficPhaseSequence!=p.PhaseSequence || p.Key.Context!=visit.Id+"/"+p.PhaseSequence || p.Key.ActorId!=visit.Id || p.Actor.ActorId!=p.Key.ActorId || !visit.PickupAssigned ||
                    visit.PickupDeparted || visit.HasCollected || !visit.PickupArrived || visit.RequestId!=0 || visit.PendingPickupSegment || visit.TrafficMovePending ||
                    !ReferenceEquals(visit.TrafficSelection,p.Selection) || !ReferenceEquals(visit.IssuedTraffic,p.Issued) ||
                    !ReferenceEquals(visit.PendingTraffic,p.Pending) || !ReferenceEquals(visit.TrafficMove,p.Movement) ||
                    p.Pending==null || !p.Pending.Marked || p.Pending.Handle==null || !ReferenceEquals(p.Pending,p.Issued) ||
                    !ReferenceEquals(p.Pending.Coordinator,traffic) || !ReferenceEquals(p.Pending.Handle.Coordinator,traffic) || !p.Pending.Handle.Predecessor.Equals(p.Pending.Intent) || !ReferenceEquals(p.Pending.Actor,p.Actor) ||
                    !ReferenceEquals(p.Pending.Total,p.Total) || !ReferenceEquals(p.Pending.Admission,p.Admission) ||
                    p.Actor.transform.position!=p.Start || p.Actor.Settings.AgentRadius!=p.Radius || p.Actor.Settings.CollisionSkin!=p.Skin || p.Actor.Settings.Epsilon!=p.Epsilon ||
                    !p.Claim.IsCurrent || !p.Owner.TryGetReservation(visit.Id,out var row) || !row.Equals(p.Assignment) || !row.Arrived ||
                    !p.Owner.TryCaptureReservation(visit.Id,out var currentClaim) || !p.Claim.IsSameReservation(currentClaim) ||
                    !Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,p.Total) && r.OwnerId==visit.Id && r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied) ||
                    !trafficCandidates.TryGetValue(p.Key,out var cursor) || !ReferenceEquals(cursor,p.Cursor) || !ReferenceEquals(cursor.CollectionRelease,p) ||
                    !ReferenceEquals(cursor.ActorLife,p.Actor) || cursor.Origin!=p.Start || cursor.Role!=TrafficCandidateRole.Collection ||
                    cursor.Source!=null && cursor.CurrentIndex!=p.CandidateIndex || !cursor.TryDescribe(p.Key,p.Actor,p.CandidateIndex,out _,out var target) || target!=p.Target)return false;
                return true;
            }
            catch(Exception){return false;}
        }
        private bool CollectionReleaseIdentityCurrent(CollectionReleasePrerequisite p)
        {
            try
            {
                if(!CollectionReleaseOwnerCurrent(p) ||
                    !ReferenceEquals(p.History,pickupDeliveryHistory) || p.History.OwnerLife!=p.OwnerLife || !ReferenceEquals(p.History.Owner,p.Owner) ||
                    !ReferenceEquals(p.History.Actor,p.ReturnActor) || !ReferenceEquals(p.History.World,world) || !ReferenceEquals(p.History.Adapter,adapter) ||
                    !p.History.Key.Equals(p.ReturnKey) || !p.History.Lease.IsSameLease(p.Lease) || p.CurrentPreview?.Consumed==true ||
                    p.ReturnActor==null || !p.ReturnActor.isActiveAndEnabled || !world.RegisteredActors.Contains(p.ReturnActor) ||
                    !p.History.Work.Equals(deliveryWorkTarget) || p.History.Attempt!=deliveryAttempt || p.ReturnActor.Settings.AgentRadius!=p.History.Radius ||
                    p.ReturnActor.Settings.CollisionSkin!=p.History.Skin || p.ReturnActor.Settings.Epsilon!=p.History.Epsilon ||
                    traffic.GetDecision(p.Pending.Intent).FailureDisposition!=TrafficFailureDisposition.None)return false;
                if(p.Owner.TryCaptureDeliveryLease(p.ReturnKey.ActorId,out var lease))return p.Lease.IsCurrent && p.Lease.IsSameLease(lease);
                return p.Lease.HasRetiredLifetime && p.History.Delivered;
            }
            catch(Exception){return false;}
        }
        private bool StopCollectionReleaseIfTerminal(CollectionReleasePrerequisite p,out string reason)
        {
            reason="";
            if(!CanIntegratedProgress || !CollectionReleaseOwnerCurrent(p))return false;
            var coordinator=p.Coordinator;var visit=p.Visit;var cursor=p.Cursor;var key=p.Key;
            // Missing/unknown is not Terminal. External return identity cannot hide an actual owned terminal source.
            // 只凭原顾客拥有关系读取已知Terminal；History/lease变化不授权释放或续期。
            var candidate=traffic.ContainsIntent(p.Key)?traffic.GetDecision(p.Key):null;
            var predecessor=traffic.ContainsIntent(p.Pending.Intent)?traffic.GetDecision(p.Pending.Intent):null;
            var stopped=candidate?.FailureDisposition==TrafficFailureDisposition.Terminal?candidate:
                predecessor?.FailureDisposition==TrafficFailureDisposition.Terminal?predecessor:null;
            if(stopped==null || candidate!=null && (candidate.AcquiredSequence!=0 || traffic.HasActiveGrant(p.Key) || traffic.TryGetCommittedJourney(p.Key,out _)))return false;
            reason="Collection prerequisite "+stopped.Kind+": "+stopped.Reason;
            if(candidate!=null && candidate.FailureDisposition!=TrafficFailureDisposition.Terminal)coordinator.Retire(key);
            if(!CanIntegratedProgress || !ReferenceEquals(traffic,coordinator) || !CollectionReleaseOwnerCurrent(p) ||
                !visits.TryGetValue(visit.Id,out var currentVisit) || !ReferenceEquals(currentVisit,visit) ||
                !trafficCandidates.TryGetValue(key,out var currentCursor) || !ReferenceEquals(currentCursor,cursor))return false;
            trafficCandidates.Remove(key);
            BlockVisit(visit,CustomerVisitState.MovingToPickupService,reason);return true;
        }
        private bool CollectionReleasePreviewMatches(CollectionReleasePrerequisite p,PickupQueueService.PreparedMutation preview)
            =>preview!=null && preview.IsCurrent && preview.BelongsTo(p.Owner) && preview.Kind==PickupQueueService.MutationKind.CollectionLease &&
                preview.Id==p.Visit.Id && preview.Assignment.VisitId==p.Visit.Id && preview.Assignment.Position==p.Target;
        private TrafficRequest CollectionReleaseRequest(CollectionReleasePrerequisite p,TrafficWorldSnapshot snapshot)
        {
            var proofs=PreservePickupGoals(p.Visit).ToList();
            var obstacles=snapshot.Parking.Where(f=>f.Owner.ActorId!=p.Key.ActorId).Select(f=>f.Position)
                .Concat(snapshot.Bodies.Where(b=>b.Owner.ActorId!=p.Key.ActorId && !b.IndependentlyProgressing).Select(b=>b.Position)).Distinct().ToArray();
            foreach(var proof in p.FixedRequirements)
                proofs.Add(new TrafficRouteRequirement(proof.ResourceId,proof.Start,proof.Goal,proof.Clearance,obstacles,
                    proof.SelectedJourney,proof.AvoidProposedParking));
            return new TrafficRequest(p.Key,p.FirstFrame,snapshot,p.Start,p.Target,p.Radius,"pickup/"+p.Visit.Id,
                requirements:proofs,safetyMargin:p.Skin+p.Epsilon,planPrimaryRoute:true);
        }
        private bool PrepareCollectionRelease(CollectionReleasePrerequisite p,TrafficWorldSnapshot snapshot,out TrafficDecision next)
        {
            next=null;
            if(StopCollectionReleaseIfTerminal(p,out _))return false;
            if(!CanIntegratedProgress || !ReferenceEquals(snapshot,trafficSnapshot) || !isActiveAndEnabled || !CollectionReleaseIdentityCurrent(p) ||
                !TrafficActorReady(p.Actor) || !traffic.ContainsIntent(p.Key))return false;
            var expected=traffic.GetDecision(p.Key);
            if(expected.FailureDisposition!=TrafficFailureDisposition.None || expected.AcquiredSequence!=0 || traffic.HasActiveGrant(p.Key) ||
                traffic.TryGetCommittedJourney(p.Key,out _) || expected.Kind!=TrafficDecisionKind.Granted &&
                expected.Kind!=TrafficDecisionKind.DeferredByBudget && expected.Kind!=TrafficDecisionKind.WaitingForTraffic)return false;
            if(p.Lease.IsCurrent)
            {
                if(!PickupDeliveryHistoryCurrent(p.History) || p.History.ReturnMovement==null ||
                    !ReferenceEquals(p.History.ReturnMovement.Source,p.History.Source) || p.History.ReturnMovement.Suspended ||
                    traffic.GetDecision(p.ReturnKey).FailureDisposition==TrafficFailureDisposition.Terminal)return false;
            }
            else
            {
                if(!p.Lease.HasRetiredLifetime)return false;
                if(!CollectionReleasePreviewMatches(p,p.CurrentPreview))
                {
                    if(!p.Owner.TryPrepareMutation(PickupQueueService.MutationKind.CollectionLease,p.Visit.Id,p.Owner.Version,out var preview,p.Target) ||
                        !CollectionReleasePreviewMatches(p,preview))return false;
                    p.CurrentPreview=preview;
                }
            }
            if(!traffic.TryReprepareCanonicalAnchor(expected,CollectionReleaseRequest(p,snapshot),out next))return false;
            p.Cursor.Source=next;return CollectionReleaseIdentityCurrent(p);
        }
        private bool TryCaptureCollectionRelease(Visit visit,TrafficCandidateCursor cursor,int candidate)
        {
            try
            {
                var h=pickupDeliveryHistory;var move=h?.ReturnMovement;
                if(!isActiveAndEnabled || cursor.CollectionRelease!=null || cursor.PickupPreparation!=null || cursor.Source!=null || traffic.ContainsIntent(cursor.Key) ||
                    visit.DeliveryYieldPhase!=0 || !PickupDeliveryHistoryCurrent(h) || !h.Delivered || !h.Lease.IsCurrent || move==null ||
                    !ReferenceEquals(move.Coordinator,traffic) || !ReferenceEquals(move.Source,h.Source) || !move.Intent.Equals(h.Key) ||
                    move.Resource!="selected-home" || move.Proof==null || move.NativeGeneration!=h.ReturnGeneration || move.NativeGeneration<=0 || move.Suspended ||
                    !ReferenceEquals(move.Proof,h.ReturnProof) || !h.Source.ProofRoutes.Any(p=>ReferenceEquals(p,move.Proof)) ||
                    move.Proof.Waypoints.Count==0 || move.Proof.Waypoints.Last()!=h.Work.Position || !move.Goal.Equals(h.Work) ||
                    h.ReturnSnapshot==null ||
                    !cursor.TryDescribe(cursor.Key,visit.Actor,candidate,out _,out var target) || target!=h.Lease.Position ||
                    !visit.PickupOwner.TryCaptureCollectionReleasePrerequisite(visit.Id,target,out var claim,out var lease) || !h.Lease.IsSameLease(lease))return false;
                var savedCurrent=h.ReturnSnapshot.Version==trafficSnapshot.Version && h.ReturnSnapshot.BodyVersion==trafficSnapshot.BodyVersion;
                if(!savedCurrent && (h.ReturnStamp.RequestId<=0 || h.ReturnStamp.LegGeneration!=h.ReturnGeneration ||
                    !traffic.TryGetCommittedLegProvenance(h.Key,h.Actor,h.ReturnGeneration,out var retained,out _) || !ReferenceEquals(retained,h.Source)))return false;
                var source=traffic.GetDecision(h.Key);
                // This actual original selected leg supplies provenance; Evaluate owns progress/path/finite-WAIT authority.
                if(source.Kind!=TrafficDecisionKind.Granted || source.FailureDisposition!=TrafficFailureDisposition.None ||
                    !traffic.TryGetCommittedJourney(h.Key,out var journey) || !ReferenceEquals(journey,h.Source) ||
                    source.ProofRoutes.Count!=1 || source.ProofRoutes[0].ResourceId!="selected-home" || source.ProofRoutes[0].ExpandedPath.Count==0 ||
                    source.Route.Count!=1 || source.Route[0]!=move.Target.Position)return false;
                var totals=Capacity.GetReservations().Where(r=>r.OwnerId==visit.Id && r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied).ToArray();
                if(totals.Length!=1)return false;
                var original=new CollectionReleasePrerequisite(this,visit,cursor,candidate,claim,lease,h,source,totals[0].Token);
                cursor.CollectionRelease=original;
                if(CollectionReleaseIdentityCurrent(original))return true;
                cursor.CollectionRelease=null;return false;
            }
            catch(Exception){return false;}
        }
        private void ObserveReturnPreparation(NavigationActor actor,TrafficMovement movement,TrafficWorldSnapshot snapshot,TrafficDecision result)
        {
            var h=pickupDeliveryHistory;
            if(!PickupDeliveryHistoryCurrent(h) || !ReferenceEquals(h.Actor,actor) || !h.Delivered || !h.Lease.IsCurrent ||
                !ReferenceEquals(movement.Coordinator,traffic) || !ReferenceEquals(movement.Source,h.Source) || !movement.Intent.Equals(h.Key) ||
                movement.Resource!="selected-home" || movement.Proof==null || !h.Source.ProofRoutes.Any(p=>ReferenceEquals(p,movement.Proof)) ||
                !movement.Goal.Equals(h.Work) || movement.NativeGeneration<=0 || result==null || !ReferenceEquals(traffic.GetDecision(h.Key),result) ||
                result.FailureDisposition!=TrafficFailureDisposition.None || result.Kind!=TrafficDecisionKind.Granted && result.Kind!=TrafficDecisionKind.DeferredByBudget)return;
            h.ReturnMovement=movement;h.ReturnProof=movement.Proof;h.ReturnGeneration=movement.NativeGeneration;h.ReturnSnapshot=snapshot;
            h.ReturnStamp=default;
        }
        private PickupFamilyPreparation FindPickupPreparation(Visit visit,TrafficIntentKey key)
        {
            if(visit==null)return null;
            if(trafficCandidates.TryGetValue(key,out var cursor) && ReferenceEquals(cursor.PickupPreparation?.Visit,visit) &&
                cursor.PickupPreparation.Key.Equals(key))
                return cursor.PickupPreparation;
            return visit.ReturnPreparation!=null && visit.ReturnPreparation.Key.Equals(key)?visit.ReturnPreparation:null;
        }
        private bool PickupFamilyPreparationCurrent(PickupFamilyPreparation p)
            =>isActiveAndEnabled && p!=null && ReferenceEquals(FindPickupPreparation(p.Visit,p.Key),p) && PickupFamilyIdentityCurrent(p);
        private bool PickupFamilyIdentityCurrent(PickupFamilyPreparation p)
        {
            try
            {
                if(p==null || disposed || !ReferenceEquals(p.Flow,this) || p.OwnerLife!=pickupPreparationLife ||
                    !ReferenceEquals(p.Coordinator,traffic) || p.Key.Session!=trafficSession || p.Key.Epoch!=trafficEpoch ||
                    !ReferenceEquals(p.World,world) || !ReferenceEquals(p.Adapter,adapter) || adapter==null || adapter.Revision!=p.Key.LayoutRevision ||
                    !ReferenceEquals(p.Capacity,Capacity) || !ReferenceEquals(p.Owner,pickupQueue) || !ReferenceEquals(p.Owner,p.Visit.PickupOwner) ||
                    !visits.TryGetValue(p.Visit.Id,out var live) || !ReferenceEquals(live,p.Visit) || !ReferenceEquals(p.Admission,p.Visit.Admission) ||
                    !ReferenceEquals(p.Actor,p.Visit.Actor) || p.Actor==null || !p.Actor.isActiveAndEnabled ||
                    !world.RegisteredActors.Any(a=>ReferenceEquals(a,p.Actor)) || p.Actor.ActorId!=p.Key.ActorId || p.Key.ActorId!=p.Visit.Id ||
                    p.Visit.State!=p.Phase || p.Phase!=CustomerVisitState.PickupWaiting || p.Visit.Generation!=p.Generation ||
                    p.Visit.TrafficPhaseSequence!=p.PhaseSequence || p.Visit.DeliveryYieldPhase!=p.YieldPhase ||
                    p.Key.Context!=p.Visit.Id+"/"+p.PhaseSequence || !p.Visit.PickupAssigned || p.Visit.PickupDeparted || p.Visit.HasCollected ||
                    p.Visit.RequestId!=0 || p.Visit.PendingPickupSegment || p.Visit.TrafficMovePending ||
                    !ReferenceEquals(p.Visit.TrafficSelection,p.Selection) || !ReferenceEquals(p.Visit.IssuedTraffic,p.Issued) ||
                    !ReferenceEquals(p.Visit.PendingTraffic,p.Pending) || !ReferenceEquals(p.Visit.TrafficMove,p.Movement) ||
                    p.Actor.transform.position!=p.Start || p.Actor.Settings.AgentRadius!=p.Radius || p.Actor.Settings.CollisionSkin!=p.Skin ||
                    p.Actor.Settings.Epsilon!=p.Epsilon || p.Claim==null || !p.Claim.IsCurrent ||
                    !p.Owner.TryGetReservation(p.Visit.Id,out var row) || !row.Equals(p.Assignment) || !row.Arrived ||
                    !p.Owner.TryCaptureReservation(p.Visit.Id,out var claim) || !p.Claim.IsSameReservation(claim) ||
                    p.Preview==null || p.Preview.Consumed || !p.Preview.BelongsTo(p.Owner) || p.Preview.Kind!=p.Operation ||
                    p.Preview.Id!=p.Visit.Id || p.Preview.Assignment.VisitId!=p.Visit.Id || p.Preview.Assignment.Position!=p.Target ||
                    p.Preview.ExpectedVersion>p.Owner.Version || p.Requirements.Any(r=>r.SelectedJourney) ||
                    !Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,p.Total) && r.OwnerId==p.Visit.Id &&
                        r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied))return false;
                if(p.Cursor!=null && (!trafficCandidates.TryGetValue(p.Key,out var cursor) || !ReferenceEquals(cursor,p.Cursor) ||
                    !ReferenceEquals(cursor.ActorLife,p.Actor) || cursor.Origin!=p.Start ||
                    cursor.Source!=null && cursor.CurrentIndex!=p.CandidateIndex ||
                    !cursor.TryDescribe(p.Key,p.Actor,p.CandidateIndex,out _,out var target) || target!=p.Target))return false;
                switch(p.Operation)
                {
                    case PickupQueueService.MutationKind.CollectionLease:
                        return p.YieldPhase==0 && p.Cursor?.Role==TrafficCandidateRole.Collection;
                    case PickupQueueService.MutationKind.YieldedCollectionLease:
                        return p.YieldPhase==2 && p.Cursor?.Role==TrafficCandidateRole.Collection;
                    case PickupQueueService.MutationKind.BeginYield:
                        return p.YieldPhase==0 && p.Cursor?.Role==TrafficCandidateRole.Yield && p.DeliveryContext!=null &&
                            p.DeliveryContext.IsCurrent && p.DeliveryContext.BelongsTo(this) && p.DeliveryCursor!=null &&
                            trafficCandidates.TryGetValue(p.DeliveryCursor.Key,out var delivery) && ReferenceEquals(delivery,p.DeliveryCursor) &&
                            ReferenceEquals(delivery.DeliveryContext,p.DeliveryContext) && ReferenceEquals(delivery.ActorLife,p.DeliveryActor) &&
                            p.DeliveryActor.ActorId==deliveryActorId && p.DeliveryWorkTarget.Equals(deliveryWorkTarget);
                    case PickupQueueService.MutationKind.ReturnYield:
                        return p.YieldPhase==2 && p.Cursor==null && ReferenceEquals(p.Visit.ReturnHistory,p.History) &&
                            PickupDeliveryHistoryCurrent(p.History) && p.History.Delivered && p.History.WasAway && p.History.HomeObserved &&
                            deliveryAcquired && deliveryWasAway && Vector3.Distance(p.History.Actor.transform.position,p.History.Work.Position)<=
                                p.History.Actor.Settings.ArrivalDistance+.001f;
                    default:return false;
                }
            }
            catch(Exception){return false;}
        }
        private void CapturePickupFamily(Visit visit,TrafficIntentKey key,PickupQueueService.PreparedMutation preview,
            TrafficCandidateCursor cursor,int candidate,IEnumerable<TrafficRouteRequirement> requirements,TrafficCandidateCursor delivery,
            IEnumerable<TrafficRouteRequirement> fixedRequirements)
        {
            try
            {
                if(!isActiveAndEnabled || preview==null || preview.Kind==PickupQueueService.MutationKind.Advance || traffic.ContainsIntent(key) ||
                    FindPickupPreparation(visit,key)!=null || !preview.IsCurrent || !preview.BelongsTo(visit.PickupOwner) ||
                    !visit.PickupOwner.TryCaptureReservation(visit.Id,out var claim))return;
                if(cursor==null)
                {
                    if(preview.Kind!=PickupQueueService.MutationKind.ReturnYield)return;
                    var old=visit.ReturnPreparation;
                    // A prior committed return may leave only a historical record, never a queued loser.
                    if(old!=null && (old.CurrentPreview?.Consumed!=true || old.PhaseSequence==visit.TrafficPhaseSequence ||
                        old.Coordinator.GetDecision(old.Key).AcquiredSequence==0))return;
                }
                else if(cursor.PickupPreparation!=null || cursor.Source!=null || !cursor.Key.Equals(key))return;
                var totals=Capacity.GetReservations().Where(r=>r.OwnerId==visit.Id && r.Kind==CapacityKind.TotalCustomers &&
                    r.State==ReservationState.Occupied).ToArray();
                if(totals.Length!=1)return;
                var preparation=new PickupFamilyPreparation(this,visit,key,preview,claim,totals[0].Token,cursor,candidate,requirements,delivery,fixedRequirements);
                if(!PickupFamilyIdentityCurrent(preparation))return;
                if(cursor!=null)cursor.PickupPreparation=preparation;else visit.ReturnPreparation=preparation;
            }
            catch(Exception){ /* Unknown original provenance grants no capture authority; original Evaluate still runs. */ }
        }
        private bool PickupFamilyPreviewMatches(PickupFamilyPreparation p,PickupQueueService.PreparedMutation preview)
            =>p!=null && preview!=null && preview.IsCurrent && preview.BelongsTo(p.Owner) && preview.Kind==p.Operation &&
                preview.Id==p.Visit.Id && preview.Assignment.VisitId==p.Visit.Id && preview.Assignment.Position==p.Target;
        private TrafficRequest PickupFamilyRequest(PickupFamilyPreparation p,TrafficWorldSnapshot snapshot)
        {
            // Other visitors are current external obligations: new roles/goals must not be omitted.
            // 原primary/exit/yield描述固定；其他顾客的当前安全proof重建，旧预算/key不变。
            var proofs=PreservePickupGoals(p.Visit).ToList();
            var excluded=p.Operation==PickupQueueService.MutationKind.BeginYield?p.DeliveryActor.ActorId:null;
            var obstacles=snapshot.Parking.Where(f=>f.Owner.ActorId!=p.Key.ActorId && f.Owner.ActorId!=excluded).Select(f=>f.Position)
                .Concat(snapshot.Bodies.Where(b=>b.Owner.ActorId!=p.Key.ActorId && b.Owner.ActorId!=excluded && !b.IndependentlyProgressing)
                    .Select(b=>b.Position)).Distinct().ToArray();
            foreach(var proof in p.FixedRequirements)
                proofs.Add(new TrafficRouteRequirement(proof.ResourceId,proof.Start,proof.Goal,proof.Clearance,obstacles,
                    proof.SelectedJourney,proof.AvoidProposedParking));
            return new TrafficRequest(p.Key,p.FirstFrame,snapshot,p.Start,p.Target,p.Radius,"pickup/"+p.Visit.Id,
                requirements:proofs,safetyMargin:p.Skin+p.Epsilon,planPrimaryRoute:true);
        }
        private bool PreparePickupFamily(PickupFamilyPreparation p,TrafficWorldSnapshot snapshot,out TrafficDecision next,
            PickupQueueService.PreparedMutation preview=null)
        {
            next=null;
            if(!CanIntegratedProgress || snapshot==null || !ReferenceEquals(snapshot,trafficSnapshot) ||
                !PickupFamilyPreparationCurrent(p) || !TrafficActorReady(p.Actor) || !traffic.ContainsIntent(p.Key))return false;
            var expected=traffic.GetDecision(p.Key);
            if(StopOrdinaryCollectionIfTerminal(p,expected,out _))return false;
            if(expected.FailureDisposition!=TrafficFailureDisposition.None || expected.AcquiredSequence!=0 ||
                traffic.HasActiveGrant(p.Key) || traffic.TryGetCommittedJourney(p.Key,out _) ||
                (expected.Kind!=TrafficDecisionKind.DeferredByBudget && expected.Kind!=TrafficDecisionKind.WaitingForTraffic &&
                 expected.Kind!=TrafficDecisionKind.Granted))return false;
            if(preview==null)
            {
                if(PickupFamilyPreviewMatches(p,p.CurrentPreview))preview=p.CurrentPreview;
                else if(!p.Owner.TryPrepareMutation(p.Operation,p.Visit.Id,p.Owner.Version,out preview,p.Target))return false;
            }
            if(!PickupFamilyPreviewMatches(p,preview))return false;
            if(!traffic.TryReprepareCanonicalAnchor(expected,PickupFamilyRequest(p,snapshot),out next))return false;
            // Original Preview remains sealed provenance; only accepted reproof installs the real current mutation.
            // forecast generation可随Queue变化，绝不替换held claim或补签已提交的missing seal。
            p.CurrentPreview=preview;if(p.Cursor!=null)p.Cursor.Source=next;
            return PickupFamilyPreparationCurrent(p) && PickupFamilyPreviewMatches(p,preview);
        }
        private bool PickupDeliveryHistoryCurrent(PickupDeliveryHistory h)
        {
            try
            {
                if(h==null || !ReferenceEquals(h,pickupDeliveryHistory) || h.OwnerLife!=pickupPreparationLife || disposed || !isActiveAndEnabled ||
                    !ReferenceEquals(h.Coordinator,traffic) || h.Key.Session!=trafficSession || h.Key.Epoch!=trafficEpoch ||
                    !ReferenceEquals(h.Owner,pickupQueue) || !ReferenceEquals(h.World,world) || !ReferenceEquals(h.Adapter,adapter) ||
                    adapter==null || adapter.Revision!=h.Key.LayoutRevision || h.Attempt!=deliveryAttempt || h.Actor==null ||
                    !h.Actor.isActiveAndEnabled || !world.RegisteredActors.Any(a=>ReferenceEquals(a,h.Actor)) || h.Actor.ActorId!=deliveryActorId ||
                    !h.Work.Equals(deliveryWorkTarget) || h.Actor.Settings.AgentRadius!=h.Radius || h.Actor.Settings.CollisionSkin!=h.Skin ||
                    h.Actor.Settings.Epsilon!=h.Epsilon || h.Lease==null)return false;
                return h.Owner.TryCaptureDeliveryLease(h.Actor.ActorId,out var current)?h.Lease.IsSameLease(current) && h.Lease.IsCurrent:
                    h.Delivered && h.Lease.HasRetiredLifetime;
            }
            catch(Exception){return false;}
        }
        private void CapturePickupDeliveryHistory(TrafficCandidateCursor cursor)
        {
            pickupDeliveryHistory=null;
            try
            {
                if(cursor?.DeliveryContext==null || !ReferenceEquals(cursor.DeliveryOwner,pickupQueue) ||
                    !pickupQueue.TryCaptureDeliveryLease(cursor.Key.ActorId,out var lease) || !lease.IsCurrent)return;
                // Context was authenticated before the joined mutation; its selection version legitimately changed during Apply.
                // 此处保存真实成功 acquire 的原来源，不能再以旧 Prepared.IsCurrent 否决已提交事实。
                var history=new PickupDeliveryHistory(this,cursor,lease);pickupDeliveryHistory=history;
                foreach(var visit in visits.Values)
                    if((visit.DeliveryYieldPhase==1 || visit.DeliveryYieldPhase==2) && ReferenceEquals(visit.YieldDeliverySource,cursor))
                        visit.ReturnHistory=history;
            }
            catch(Exception){pickupDeliveryHistory=null;} // Unknown metadata cannot interrupt the original successful publication.
        }

        private sealed class FiniteRouteProof
        {
            public Visit Moving;
            public NavigationActor Actor;
            public long Request;
            public int Generation,WaiterGeneration,Revision;
            public CustomerVisitState Phase;
            public QueueAssignment Assignment;
            public NavigationTarget Target;
            public Queue<Vector3> WaiterRoute;
            public Vector3[] Unissued,WaiterUnissued;
            public IReadOnlyList<Vector3> Path;
            public int FirstLegEnd;
        }
        private sealed class PendingCounterMove
        {
            internal ServiceTrafficCoordinator Coordinator;
            internal CounterQueueService Counter;
            internal CounterQueueService.AssignmentClaim Claim;
            internal TrafficIntentKey Intent;
            internal CustomerVisitState Phase;
            internal int Generation;
            // Original committed publication; these references never grant a new canonical commit.
            // 保留原已提交来源，只允许同一身体/预留继续原首腿。
            internal TrafficDecision Source;
            internal TrafficRouteProof Proof;
            internal NavigationActor Actor;
            internal CustomerAdmission Admission;
            internal CapacityService Capacity;
            internal CapacityToken Total;
            internal Vector3 Origin;
            internal float Radius,Skin,Epsilon;
        }
        // Producer provenance is separate from the mutable global traffic snapshot.
        // 只保存原Queue/实际life/业务cursor与轻量descriptor，不复制TrafficRequest registry。
        // Fixed phase descriptors select proof roles; the current sealed Queue preview owns its anchor.
        // 原side/初始yield坐标固定；cursor不保存TrafficRequest，也不授予canonical权限。
        private enum TrafficCandidateRole { Reservation,Collection,Yield,Delivery }
        private enum PlannedYieldResult { Pending,Yielded,Unavailable,Failed }
        // One optional strategy, fixed before the original staff Submit; not another service side.
        // 只绑定当时已安定的真实 paid waiter；等待 child 不创建新 parent key 或补预算。
        private sealed class DeliveryYieldPlan
        {
            internal readonly ServiceTrafficCoordinator Coordinator;
            internal readonly TrafficCandidateCursor Parent,Child;
            internal readonly EmployeeFlowController.PreparedDeliveryContext Context;
            internal readonly Visit Visit;
            internal readonly NavigationActor Actor;
            internal readonly CustomerAdmission Admission;
            internal readonly CapacityToken Total;
            internal readonly PickupQueueService Owner;
            internal readonly PickupQueueService.ReservationClaim Claim;
            internal readonly QueueAssignment Assignment;
            internal readonly NavigationTarget Side;
            internal readonly int Generation,PhaseSequence,FirstFrame;
            internal readonly float Radius,Skin,Epsilon;
            internal PickupQueueService.ReservationClaim YieldClaim;
            internal TrafficDecision ChildSource;
            internal QueueAssignment YieldAssignment;
            internal int YieldGeneration;
            internal DeliveryYieldContinuation Continuation;
            internal TrafficIntentKey ActiveChildKey=>Continuation?.Used==true?Continuation.Intent:Child.Key;
            internal TrafficDecision ActiveChildSource=>Continuation?.Used==true?Continuation.Source:ChildSource;
            internal DeliveryYieldPlan(CustomerFlowController flow,TrafficCandidateCursor parent,TrafficCandidateCursor child,
                Visit visit,PickupQueueService.ReservationClaim claim,CapacityToken total,NavigationTarget side)
            {
                Coordinator=flow.traffic;Parent=parent;Child=child;Context=parent.DeliveryContext;Visit=visit;Actor=visit.Actor;
                Admission=visit.Admission;Total=total;Owner=visit.PickupOwner;Claim=claim;Assignment=claim.Assignment;Side=side;
                Generation=visit.Generation;PhaseSequence=visit.TrafficPhaseSequence;FirstFrame=visit.TrafficCandidateFrames[child.Key];
                Radius=Actor.Settings.AgentRadius;Skin=Actor.Settings.CollisionSkin;Epsilon=Actor.Settings.Epsilon;
            }
        }
        // A logical same-owner checkpoint, never a second payment/PQS reservation or a Native result.
        private sealed class DeliveryYieldContinuation
        {
            internal readonly DeliveryYieldPlan Plan;
            internal readonly PaidPickupTask Paid;
            internal readonly CafeOrderKey Order;
            internal readonly PickupQueueService.ReservationClaim Claim;
            internal readonly QueueAssignment Assignment;
            internal readonly ReservationSnapshot[] Tokens;
            internal readonly Vector3 StopPose;
            internal readonly int Segments,Replans,PhaseSequence;
            internal ServiceTrafficCoordinator.MovementRecoveryReceipt Receipt;
            internal int HeldGeneration;
            internal bool CancellationConfirmed,Used,Pending,Failed;
            internal TrafficIntentKey Intent;
            internal TrafficDecision Source;
            internal DeliveryYieldContinuation(DeliveryYieldPlan plan,PickupQueueService.ReservationClaim claim,
                ReservationSnapshot[] tokens,ServiceTrafficCoordinator.MovementRecoveryReceipt receipt)
            {
                Plan=plan;Paid=plan.Visit.PaidPickup;Order=Paid.Order;Claim=claim;Assignment=claim.Assignment;
                Tokens=tokens;Receipt=receipt;StopPose=plan.Actor.transform.position;Segments=plan.Visit.Segments;
                Replans=plan.Visit.Replans;PhaseSequence=plan.Visit.TrafficPhaseSequence;
            }
        }
        private sealed class TrafficCandidateCursor
        {
            internal readonly TrafficIntentKey Key;
            internal readonly object ActorLife;
            internal readonly IReadOnlyList<NavigationTarget> Sides;
            internal readonly Vector3 Origin;
            internal readonly TrafficCandidateRole Role;
            internal readonly HashSet<int> AttemptedIndices=new HashSet<int>(),StaticInvalidIndices=new HashSet<int>();
            internal readonly HashSet<int> UnpaidEligibilityAttempts=new HashSet<int>();
            internal TrafficUnpaidParkingEligibility UnpaidEligibility;
            internal TrafficDecision UnpaidEligibilityRound;
            internal bool HasAttempted(int index)=>(UnpaidEligibility==null?AttemptedIndices:UnpaidEligibilityAttempts).Contains(index);
            internal int CurrentIndex;
            internal TrafficDecision Source;
            // Original producer seal and actual unconsumed Queue preview, never a full request cache.
            internal EmployeeFlowController.PreparedDeliveryContext DeliveryContext;
            internal PickupQueueService.PreparedMutation DeliveryPreview;
            internal PickupQueueService DeliveryOwner;
            internal PickupFamilyPreparation PickupPreparation;
            internal CollectionReleasePrerequisite CollectionRelease;
            internal DeliveryYieldPlan OriginalDeliveryPlan;
            internal bool DeliveryPlanDeclared;
#if UNITY_EDITOR
            internal TrafficDecision ObservedAdvanceSource;
            internal bool ObservedPreflight;
#endif
            internal int Count=>Sides.Count*(Role==TrafficCandidateRole.Yield?24:1)+(OriginalDeliveryPlan==null?0:1);
            internal TrafficCandidateCursor(TrafficIntentKey key,object actorLife,NavigationTarget[] sides,Vector3 origin,
                TrafficCandidateRole role,int currentIndex,TrafficDecision source)
            {
                Key=key;ActorLife=actorLife;Sides=Array.AsReadOnly(sides.ToArray());Origin=origin;Role=role;
                CurrentIndex=currentIndex;Source=source;if(source!=null && currentIndex>=0)AttemptedIndices.Add(currentIndex);
            }
            internal bool TrySelect(TrafficIntentKey key,object actorLife,TrafficDecision current,bool navigationReady,
                IReadOnlyList<int> availableIndices,out int index)
            {
                index=-1;
                if(!Key.Equals(key) || !ReferenceEquals(ActorLife,actorLife) || actorLife==null || !navigationReady)return false;
                if(current!=null)
                {
                    if(!current.Intent.Equals(Key) || current.FailureDisposition==TrafficFailureDisposition.Terminal || current.AcquiredSequence!=0)return false;
                    if(current.FailureDisposition==TrafficFailureDisposition.None)
                    {
                        if(CurrentIndex<0 || CurrentIndex>=Count || (current.Kind!=TrafficDecisionKind.Granted &&
                            current.Kind!=TrafficDecisionKind.DeferredByBudget && current.Kind!=TrafficDecisionKind.WaitingForTraffic))return false;
                        index=CurrentIndex;return true;
                    }
                    if(current.FailureDisposition!=TrafficFailureDisposition.AlternateAllowed)return false;
                }
                else if(Source!=null)return false;
                foreach(var candidate in availableIndices??Array.Empty<int>())
                    if(candidate>=0 && candidate<Count && !HasAttempted(candidate) && !StaticInvalidIndices.Contains(candidate))
                    {index=candidate;return true;}
                return false;
            }
            // Prefer a clear original Delivery/Collection side, preserving the first original busy fallback.
            // 只是提交前排序；全忙仍交 Core 判定有限等待/Terminal，不授予权限或烧候选。
            internal bool TrySelectPreferredBase(TrafficIntentKey key,object actorLife,TrafficDecision current,bool navigationReady,
                Func<int,NavigationTarget,Vector3,bool> available,Func<Vector3,bool> preferred,out int index)
            {
                index=-1;
                if((Role!=TrafficCandidateRole.Delivery && Role!=TrafficCandidateRole.Collection) || !Key.Equals(key) || !ReferenceEquals(ActorLife,actorLife) ||
                    actorLife==null || !navigationReady || available==null || preferred==null)return false;
                if(current!=null && current.FailureDisposition!=TrafficFailureDisposition.AlternateAllowed)
                    return TrySelect(key,actorLife,current,navigationReady,Array.Empty<int>(),out index);
                if(current==null && Source!=null || current!=null && (!current.Intent.Equals(Key) || current.AcquiredSequence!=0))return false;
                var fallback=-1;
                for(var candidate=0;candidate<Sides.Count;candidate++)
                {
                    if(HasAttempted(candidate) || StaticInvalidIndices.Contains(candidate) ||
                        !TryDescribe(key,actorLife,candidate,out var side,out var target) || !available(candidate,side,target))continue;
                    if(fallback<0)fallback=candidate;
                    if(preferred(target))return TrySelect(key,actorLife,current,navigationReady,new[]{candidate},out index);
                }
                // Reserved yield remains outside this base-side ordering and keeps its real child-arrival gate.
                return TrySelect(key,actorLife,current,navigationReady,fallback<0?Array.Empty<int>():new[]{fallback},out index);
            }
            internal bool TryPrepare(ServiceTrafficCoordinator coordinator,TrafficRequest request,object actorLife,int index,
                TrafficDecision expected,out TrafficDecision next)
            {
                next=null;
                if(coordinator==null || request==null || request.Start!=Origin ||
                    !TryDescribe(request.Intent,actorLife,index,out _,out var fixedTarget) ||
                    (Role!=TrafficCandidateRole.Reservation && request.Target!=fixedTarget))return false;
                if(Role==TrafficCandidateRole.Delivery && DeliveryContext!=null &&
                    (!DeliveryContext.IsCurrent || !ReferenceEquals(DeliveryContext.ActorLife,ActorLife) ||
                     DeliveryPreview==null || DeliveryPreview.Consumed || !DeliveryPreview.BelongsTo(DeliveryOwner)))return false;
                var body=request.Snapshot?.Bodies.FirstOrDefault(b=>b.Owner.ActorId==Key.ActorId);
                if(body==null || !ReferenceEquals(body.ActorLife,ActorLife) || !body.ActorActive || !body.Enabled || body.Position!=request.Start)return false;
                if(Source==null)
                {
                    if(expected!=null || CurrentIndex>=0 || AttemptedIndices.Count!=0 || StaticInvalidIndices.Contains(index))return false;
                    var initial=coordinator.EvaluateParking(request,index,Count);
                    if(UnpaidEligibility!=null && !coordinator.TrySetUnpaidParkingEligibility(initial,UnpaidEligibility))
                    {Source=initial;coordinator.Retire(Key);return false;}
                    if(initial.FailureDisposition!=TrafficFailureDisposition.None || initial.Kind!=TrafficDecisionKind.DeferredByBudget ||
                        !initial.Intent.Equals(Key) || !ReferenceEquals(coordinator.GetDecision(Key),initial))
                    {Source=initial;return false;}
                    next=initial;
                }
                else
                {
                    if(expected==null || expected.AcquiredSequence!=0 || coordinator.HasActiveGrant(Key) || !ReferenceEquals(coordinator.GetDecision(Key),expected) ||
                        !expected.Intent.Equals(Key) || expected.FailureDisposition==TrafficFailureDisposition.Terminal)return false;
                    if(expected.FailureDisposition==TrafficFailureDisposition.AlternateAllowed)
                    {
                        if(index==CurrentIndex || HasAttempted(index) || StaticInvalidIndices.Contains(index) ||
                            !coordinator.TryAdvanceCandidate(expected,index,request,out next))return false;
                    }
                    else
                    {
                        if(index!=CurrentIndex || expected.FailureDisposition!=TrafficFailureDisposition.None ||
                            (expected.Kind!=TrafficDecisionKind.Granted && expected.Kind!=TrafficDecisionKind.DeferredByBudget &&
                             expected.Kind!=TrafficDecisionKind.WaitingForTraffic))return false;
                        if(Role==TrafficCandidateRole.Reservation)
                        {if(!coordinator.TryReprepareCanonicalAnchor(expected,request,out next))return false;}
                        else if(Role==TrafficCandidateRole.Delivery && DeliveryContext!=null && expected.Kind!=TrafficDecisionKind.Granted)
                        {if(!coordinator.TryRefreshQueuedPreparation(expected,request,out next))return false;}
                        else next=coordinator.EvaluateParking(request,index,Count);
                    }
                }
                if(UnpaidEligibility!=null)
                {
                    if(next.Reason=="unpaid-eligibility/spatial-change" && !ReferenceEquals(next,UnpaidEligibilityRound))
                    {UnpaidEligibilityAttempts.Clear();UnpaidEligibilityRound=next;}
                    UnpaidEligibilityAttempts.Add(index);
                }
                CurrentIndex=index;AttemptedIndices.Add(index);Source=next;return true;
            }
            internal bool TryDescribe(TrafficIntentKey key,object actorLife,int index,out NavigationTarget side,out Vector3 target)
            {
                side=default;target=default;
                if(!Key.Equals(key) || actorLife==null || !ReferenceEquals(ActorLife,actorLife) || index<0 || index>=Count)return false;
                if(Role!=TrafficCandidateRole.Yield)
                {side=index==Sides.Count?OriginalDeliveryPlan.Side:Sides[index];target=side.Position;return true;}
                // Index decoding only; candidate selection never wraps or expands this original finite set.
                var sample=index/Sides.Count;side=Sides[index-sample*Sides.Count];
                var ring=sample/8;var angle=(sample-ring*8)*Mathf.PI/4;var distance=(ring+1)*1.1f;
                target=Origin+new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*distance;return true;
            }
        }
        private sealed class CounterAdmissionAnchor
        {
            internal ServiceTrafficCoordinator Coordinator;
            internal TrafficIntentKey Key;
            internal CounterQueueService Owner;
            internal CapacityService Capacity;
            internal NavigationWorld World;
            internal NavigationActor Prefab;
            internal Transform Spawn;
            internal string Session;
            internal long Before;
            internal int FirstFrame;
            internal Vector3 Position;
            internal Quaternion Rotation;
            // Compare original settings components; request Safety keeps its existing rounded sum.
            // 直接比较原始float分量；request的Safety仍使用原有存储值。
            internal float Radius,Safety,Skin,Epsilon;
            internal bool Submitted,Automatic;
            internal TrafficAdmissionReadiness Readiness;
            internal CounterQueueService.PreparedMutation Preview;
        }
        // One unborn canonical Advance, separate from the already selected PendingCounterMove.
        // 只绑定原真实 Queue claim/preview；旧的已提交 parking 仍由原 movement owner 管理。
        private sealed class CounterAdvanceAnchor
        {
            internal readonly CustomerFlowController Flow;
            internal readonly ServiceTrafficCoordinator Coordinator;
            internal readonly TrafficIntentKey Key;
            internal readonly Visit Visit;
            internal readonly NavigationActor Actor;
            internal readonly CustomerAdmission Admission;
            internal readonly CapacityToken Total;
            internal readonly CapacityService Capacity;
            internal readonly NavigationWorld World;
            internal readonly NavigationLayoutAdapter Adapter;
            internal readonly CounterQueueService Counter;
            internal readonly CounterQueueService.AssignmentClaim Claim;
            internal readonly QueueAssignment Assignment;
            internal QueueAssignment Target;
            internal readonly int Generation,FirstFrame,PhaseSequence;
            internal readonly CustomerVisitState Phase;
            internal readonly bool Recovery;
            internal readonly Vector3 Start;
            internal readonly float Radius,Skin,Epsilon;
            internal readonly TrafficSelection Selection;
            internal readonly TrafficPhaseReceipt Issued,Pending;
            internal readonly TrafficMovement Movement;
            internal CounterQueueService.PreparedMutation Preview;
            internal TrafficDecision Source;
            internal bool Retired;
            internal TrafficCounterAdvanceReadiness Readiness;
            internal CounterAdvanceWorkingPredecessor WorkingPredecessor;
            internal CounterAdvanceAnchor(CustomerFlowController flow,Visit visit,TrafficIntentKey key,CounterQueueService counter,
                CounterQueueService.AssignmentClaim claim,CounterQueueService.PreparedMutation preview,CapacityToken total,int firstFrame)
            {
                Flow=flow;Coordinator=flow.traffic;Key=key;Visit=visit;Actor=visit.Actor;Admission=visit.Admission;Total=total;Capacity=flow.Capacity;
                World=flow.world;Adapter=flow.adapter;Counter=counter;Claim=claim;Assignment=claim.Assignment;Target=preview.Assignment;
                Generation=visit.Generation;FirstFrame=firstFrame;PhaseSequence=visit.TrafficPhaseSequence;Start=Actor.transform.position;
                Phase=visit.State;Recovery=flow.recoveringQueue && Phase==CustomerVisitState.Blocked && !Assignment.Arrived;
                Radius=Actor.Settings.AgentRadius;Skin=Actor.Settings.CollisionSkin;Epsilon=Actor.Settings.Epsilon;
                Selection=visit.TrafficSelection;Issued=visit.IssuedTraffic;Pending=visit.PendingTraffic;Movement=visit.TrafficMove;Preview=preview;
            }
        }
        private sealed class CounterAdvanceWorkingPredecessor
        {
            internal Visit Visit;internal NavigationActor Actor;internal CustomerAdmission Admission;
            internal CounterQueueService.AssignmentClaim CounterClaim;internal QueueAssignment CounterAssignment;
            internal PickupParkingRight Parking;internal TrafficDecision ParkingSource;internal TrafficRouteProof ParkingProof;
            internal TrafficParkingFact ParkingFact;internal CapacityToken ParkingToken,Total;internal Func<CafeOrderKey?,bool> OriginalTask;
            internal Vector3 Origin;internal int Generation;internal float Radius,Skin,Epsilon;
            internal PaidPickupTask Paid;internal TrafficIntentKey PaidIntent;
        }
        // These capture/refresh calls bind metadata only, before the normal Core pump; Core callbacks stay readonly.
        private void RefreshCounterAdvanceWorkingPredecessor(CounterAdvanceAnchor a)
        {
            try{RefreshCounterAdvanceWorkingPredecessorCore(a);}
            catch(Exception){ /* Withhold no rights on failed readonly producer inspection. */ }
        }
        private void RefreshCounterAdvanceWorkingPredecessorCore(CounterAdvanceAnchor a)
        {
            if(a==null || a.Recovery || !CounterAdvanceAnchorCurrent(a))return;
            var work=a.WorkingPredecessor;
            if(work!=null)
            {
                var paid=work.Visit.PaidPickup;
                if(work.Paid==null && paid!=null && !paid.Failed && !paid.RecoveryUsed && !paid.RecoveryPending &&
                    ReferenceEquals(paid.Parking,work.Parking) && paid.ActiveIntent.Equals(paid.Intent) && work.OriginalTask(paid.Order))
                {work.Paid=paid;work.PaidIntent=paid.Intent;}
                return;
            }
            var rows=a.Counter.Snapshot;var index=-1;
            for(var i=0;i<rows.Count;i++)if(rows[i].VisitId==a.Visit.Id){index=i;break;}
            if(index<=0 || !visits.TryGetValue(rows[index-1].VisitId,out var predecessor) ||
                predecessor.State!=CustomerVisitState.Queued || !predecessor.CounterArrived || predecessor.QueueLeft || predecessor.HasCollected ||
                predecessor.RequestId!=0 || predecessor.PickupInTransit || predecessor.PaidPickup!=null || !TrafficActorReady(predecessor.Actor) ||
                !a.Counter.TryCaptureAssignment(predecessor.Id,out var claim) || !claim.IsCurrent || !claim.Assignment.Arrived ||
                !SameAssignment(predecessor.Assignment,claim.Assignment) || !PickupParkingCurrent(predecessor,trafficSnapshot) ||
                counterTrafficProgress==null || counterTrafficProgress(predecessor.Id)<=0 || counterDepartureTaskCapture==null)return;
            var read=counterDepartureTaskCapture(predecessor.Id);if(read==null || !read(null))return;
            var totals=Capacity.GetReservations().Where(r=>r.OwnerId==predecessor.Id && r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied).ToArray();
            if(totals.Length!=1)return;
            a.WorkingPredecessor=new CounterAdvanceWorkingPredecessor{Visit=predecessor,Actor=predecessor.Actor,Admission=predecessor.Admission,
                CounterClaim=claim,CounterAssignment=claim.Assignment,Parking=predecessor.PickupParking,
                ParkingSource=predecessor.PickupParking.Source,ParkingProof=predecessor.PickupParking.Proof,ParkingFact=predecessor.PickupParking.Fact,
                ParkingToken=predecessor.PickupParking.Token,Total=totals[0].Token,OriginalTask=read,
                Origin=predecessor.Actor.transform.position,Generation=predecessor.Generation,Radius=predecessor.Actor.Settings.AgentRadius,
                Skin=predecessor.Actor.Settings.CollisionSkin,Epsilon=predecessor.Actor.Settings.Epsilon};
        }
        private float CounterAdvanceWorkingPredecessorSeconds(CounterAdvanceAnchor a,TrafficIntentKey owner)
        {
            var work=a.WorkingPredecessor;var predecessor=work?.Visit;
            if(work==null || !CounterAdvanceAnchorCurrent(a) || predecessor==null || !visits.TryGetValue(predecessor.Id,out var live) ||
                !ReferenceEquals(live,predecessor) || !ReferenceEquals(predecessor.Actor,work.Actor) || !ReferenceEquals(predecessor.Admission,work.Admission) ||
                !TrafficActorReady(work.Actor) || work.Actor.Settings.AgentRadius!=work.Radius || work.Actor.Settings.CollisionSkin!=work.Skin ||
                work.Actor.Settings.Epsilon!=work.Epsilon || predecessor.State==CustomerVisitState.Blocked || predecessor.HasCollected ||
                !ReferenceEquals(predecessor.PickupParking,work.Parking) || !ReferenceEquals(work.Parking.Source,work.ParkingSource) ||
                !ReferenceEquals(work.Parking.Proof,work.ParkingProof) || !ReferenceEquals(work.Parking.Fact,work.ParkingFact) ||
                !ReferenceEquals(work.Parking.Token,work.ParkingToken) || owner.ActorId!=predecessor.Id ||
                !Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,work.Total) && r.OwnerId==predecessor.Id && r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied) ||
                !Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,work.ParkingToken) && r.OwnerId==predecessor.Id && r.Kind==CapacityKind.PickUp &&
                    (r.State==ReservationState.Reserved || r.State==ReservationState.Occupied)))return 0;
            var parkingOwner=owner.Equals(work.ParkingSource.Intent);var paid=predecessor.PaidPickup;
            if(paid==null)
            {
                if(!parkingOwner || predecessor.Generation!=work.Generation || predecessor.State!=CustomerVisitState.Queued || predecessor.RequestId!=0 ||
                    !predecessor.CounterArrived || predecessor.QueueLeft || predecessor.PickupInTransit || work.Actor.transform.position!=work.Origin ||
                    !work.CounterClaim.IsCurrent || !SameAssignment(predecessor.Assignment,work.CounterAssignment) ||
                    !PickupParkingIdentityCurrent(predecessor) || !work.OriginalTask(null))return 0;
                return 1; // Evidence of the same finite task, not a replacement/reset wait duration.
            }
            if(!ReferenceEquals(paid,work.Paid) || paid.Failed || paid.RecoveryUsed || paid.RecoveryPending || !paid.Accepted ||
                !ReferenceEquals(paid.Parking,work.Parking) || !paid.Intent.Equals(work.PaidIntent) || !paid.ActiveIntent.Equals(work.PaidIntent) ||
                !work.OriginalTask(paid.Order))return 0;
            var paidOwner=owner.Equals(work.PaidIntent);if(!parkingOwner && !paidOwner)return 0;
            if(!paid.Transferred)
            {
                if(predecessor.Generation!=work.Generation || predecessor.State!=CustomerVisitState.Queued || predecessor.RequestId!=0 ||
                    predecessor.QueueLeft || predecessor.PickupInTransit || work.Actor.transform.position!=work.Origin ||
                    !work.CounterClaim.IsCurrent || !SameAssignment(predecessor.Assignment,work.CounterAssignment) ||
                    !PickupParkingIdentityCurrent(predecessor))return 0;
                if(paidOwner && (paid.Preparation==null || !OwnsPaidPreparation(paid.Preparation) || !PaidPreparationCurrent(paid.Preparation) ||
                    !a.Coordinator.ContainsIntent(work.PaidIntent) || a.Coordinator.GetDecision(work.PaidIntent).FailureDisposition!=TrafficFailureDisposition.None))return 0;
                return 1;
            }
            var move=predecessor.TrafficMove;
            if(move==null || !move.Intent.Equals(work.PaidIntent) || !ReferenceEquals(move.Coordinator,a.Coordinator) ||
                move.Source==null || move.Source.AcquiredSequence<=0 || !a.Coordinator.TryGetCommittedJourney(work.PaidIntent,out var source) ||
                !ReferenceEquals(source,move.Source))return 0;
            // Release only after real first Native acceptance (or an already authenticated Arrived callback).
            if(move.Index>0 || HasOwnedActiveRequest(predecessor) && move.NativeGeneration==predecessor.Generation &&
                a.Coordinator.TryGetCommittedLegProvenance(move.Intent,work.Actor,move.NativeGeneration,out var issued,out _) && ReferenceEquals(issued,move.Source))return 0;
            if(predecessor.RequestId!=0 || predecessor.State!=CustomerVisitState.MovingToPickup || !predecessor.PickupInTransit || predecessor.QueueLeft ||
                work.Actor.transform.position!=work.Origin || !work.CounterClaim.IsCurrent ||
                !TrafficSelectionCurrent(predecessor,predecessor.TrafficSelection) ||
                a.Coordinator.GetDecision(work.PaidIntent).FailureDisposition!=TrafficFailureDisposition.None || parkingOwner && work.Parking.Retired)return 0;
            return 1;
        }
        // Read only the same actual Counter owner/Arrived receipt/current canonical successor.
        // 原captured dependency由Core核验；这里只认证真实FIFO前客、旧receipt和当前g100原source，不签新权限。
        private float CounterAdvanceHandoffPredecessorSeconds(CounterAdvanceAnchor a,TrafficIntentKey owner)
        {
            float Refused(string stage){a?.Coordinator?.ObserveCounterHandoffProducerRefusal(a.Key,stage);return 0;}
            try
            {
                if(a.Recovery)return Refused("cfc.1");
                if(!CounterAdvanceAnchorCurrent(a))return Refused("cfc.2");
                var rows=a.Counter.Snapshot;var index=-1;
                for(var i=0;i<rows.Count;i++)if(rows[i].VisitId==a.Visit.Id){index=i;break;}
                if(index<=0)return Refused("cfc.3");var row=rows[index-1];
                if(row.Arrived)return Refused("cfc.4");
                if(!visits.TryGetValue(row.VisitId,out var prior))return Refused("cfc.5");
                if(prior.State!=CustomerVisitState.Advancing)return Refused("cfc.6");
                if(prior.RequestId!=0)return Refused("cfc.7");
                if(prior.QueueLeft)return Refused("cfc.8");
                if(prior.HasCollected)return Refused("cfc.9");
                if(prior.PendingCounter!=null)return Refused("cfc.10");
                if(!prior.Assignment.Equals(row))return Refused("cfc.11");
                if(!TrafficActorReady(prior.Actor))return Refused("cfc.12");
                if(!a.Counter.TryCaptureAssignment(prior.Id,out var claim))return Refused("cfc.13");
                if(!claim.IsCurrent)return Refused("cfc.14");
                if(!claim.Assignment.Equals(row))return Refused("cfc.15");
                var selected=prior.TrafficSelection;var move=prior.TrafficMove;var pending=prior.PendingTraffic;
                if(!TrafficSelectionCurrent(prior,selected))return Refused("cfc.16");
                if(selected.Phase!=CustomerVisitState.Advancing)return Refused("cfc.17");
                if(selected.Source.AcquiredSequence<=0)return Refused("cfc.18");
                if(!ReferenceEquals(selected.Counter,a.Counter))return Refused("cfc.19");
                if(selected.CounterEntry==null)return Refused("cfc.20");
                if(!selected.CounterEntry.IsSameReservation(claim))return Refused("cfc.21");
                if(move==null)return Refused("cfc.22");
                if(!ReferenceEquals(move.Coordinator,a.Coordinator))return Refused("cfc.23");
                if(!ReferenceEquals(move.Source,selected.Source))return Refused("cfc.24");
                if(!move.Intent.Equals(selected.Intent))return Refused("cfc.25");
                if(move.Intent.IntentId!="counter-advance")return Refused("cfc.26");
                if(move.Index<0)return Refused("cfc.27");
                if(move.Suspended)return Refused("cfc.28");
                if(move.NativeGeneration<=0)return Refused("cfc.29");
                if(move.NativeGeneration!=prior.Generation)return Refused("cfc.30");
                if(!prior.TrafficMovePending)return Refused("cfc.31");
                if(move.Goal.Position!=row.Position)return Refused("cfc.32");
                if(prior.FinalTarget.Position!=row.Position)return Refused("cfc.33");
                if(!a.Coordinator.TryGetCommittedJourney(move.Intent,out var source))return Refused("cfc.34");
                if(!ReferenceEquals(source,move.Source))return Refused("cfc.35");
                if(move.Index>0)
                {
                    // Same authenticated FIFO predecessor, continuing its original committed route.
                    // 同一原路线换腿：只核对当前生产者和cursor，仍由Core原预算/全路权检查决定。
                    if(pending!=null || !owner.Equals(move.Intent) || move.Resource!="counter/"+prior.Id)return Refused("cfc.continuation-identity");
                    var proof=move.Proof;
                    if(proof==null || move.Index>=proof.Waypoints.Count || prior.Route==null || prior.Route.Count==0 ||
                        prior.Route.Peek()!=proof.Waypoints[move.Index])return Refused("cfc.continuation-cursor");
                    return 1;
                }
                if(pending==null)return Refused("cfc.36");
                if(!pending.Marked)return Refused("cfc.37");
                if(pending.Kind!=TrafficPhaseMilestoneKind.Arrived)return Refused("cfc.38");
                if(pending.Handle==null)return Refused("cfc.39");
                if(pending.Intent.IntentId!="counter-advance")return Refused("cfc.40");
                if(pending.Intent.Equals(move.Intent))return Refused("cfc.41");
                if(!ReferenceEquals(pending.Flow,this))return Refused("cfc.42");
                if(!ReferenceEquals(pending.Visit,prior))return Refused("cfc.43");
                if(!ReferenceEquals(pending.Actor,prior.Actor))return Refused("cfc.44");
                if(!ReferenceEquals(pending.Admission,prior.Admission))return Refused("cfc.45");
                if(!ReferenceEquals(pending.Coordinator,a.Coordinator))return Refused("cfc.46");
                if(!TrafficReceiptCurrent(pending))return Refused("cfc.47");
                if(!a.Coordinator.IsCurrentPhaseHandoff(pending.Handle,pending.Source,prior.Actor))return Refused("cfc.48");
                if(!owner.Equals(pending.Intent) && !owner.Equals(move.Intent))return Refused("cfc.49");
                foreach(var binding in pending.Claims??Array.Empty<TrafficClaimBinding>())
                    if(binding.Kind==TrafficClaimKind.Counter && ReferenceEquals(binding.Counter,a.Counter) && binding.CounterEntry!=null &&
                        binding.CounterEntry.IsSameReservation(claim) && binding.Fact.ResourceId=="counter/"+prior.Id &&
                        ReadTrafficClaim(pending,binding.Fact,out var actual)==TrafficPhaseClaimState.Held && ReferenceEquals(actual.CanonicalOwner,a.Counter) &&
                        actual.Position==row.Position && ReferenceEquals(actual.BusinessOwner,prior) && ReferenceEquals(actual.ReleaseObligation,prior.Admission))return 1;
            }
            catch(Exception error){return Refused("cfc.exception/"+error.GetType().Name);}
            return Refused("cfc.no-held-counter-claim");
        }
        private bool BindCounterAdvanceReadiness(CounterAdvanceAnchor a,TrafficDecision expected)
        {
            if(a.Recovery)return true;
            if(!CounterAdvanceAnchorCurrent(a) || expected==null || !ReferenceEquals(a.Coordinator.GetDecision(a.Key),expected))return false;
            if(a.Readiness==null)a.Readiness=new TrafficCounterAdvanceReadiness(a.Key,()=>CounterAdvanceAnchorCurrent(a),owner=>
            {
                var working=CounterAdvanceWorkingPredecessorSeconds(a,owner);
                return working>0?working:CounterAdvanceHandoffPredecessorSeconds(a,owner);
            });
            return a.Coordinator.TrySetCounterAdvanceReadiness(expected,a.Readiness);
        }
        private bool CounterAdvanceAnchorCurrent(CounterAdvanceAnchor a)
            =>CounterAdvanceIdentityCurrent(a,requireNavigation:true);
        private bool CounterAdvanceIdentityCurrent(CounterAdvanceAnchor a,bool requireNavigation)
        {
            try
            {
                if(a==null || a.Retired || disposed || !isActiveAndEnabled || integratedHeld || !Configured ||
                    !ReferenceEquals(a.Flow,this) || !ReferenceEquals(a.Visit.PendingCounterAdvance,a) ||
                    !ReferenceEquals(a.Capacity,Capacity) || !ReferenceEquals(a.World,world) || !ReferenceEquals(a.Adapter,adapter) ||
                    !ReferenceEquals(a.Counter,queue) || !ReferenceEquals(a.Actor,a.Visit.Actor) || !ReferenceEquals(a.Admission,a.Visit.Admission) ||
                    !ReferenceEquals(traffic,a.Coordinator) || a.Key.Session!=trafficSession || a.Key.Epoch!=trafficEpoch ||
                    world==null || !world.isActiveAndEnabled || adapter==null || !adapter.isActiveAndEnabled || adapter.Revision!=a.Key.LayoutRevision ||
                    a.Actor==null || !a.Actor.isActiveAndEnabled || !world.RegisteredActors.Any(actor=>ReferenceEquals(actor,a.Actor)) ||
                    (requireNavigation && (!CanIntegratedProgress || !TrafficActorReady(a.Actor))) ||
                    !visits.TryGetValue(a.Visit.Id,out var live) || !ReferenceEquals(live,a.Visit) || a.Visit.Generation!=a.Generation ||
                    a.Actor.ActorId!=a.Visit.Id || a.Visit.Id!=a.Key.ActorId || a.Visit.State!=a.Phase ||
                    (a.Recovery? !recoveringQueue || a.Phase!=CustomerVisitState.Blocked || a.Assignment.Arrived :
                        a.Phase!=CustomerVisitState.Queued || !a.Visit.CounterArrived || !a.Assignment.Arrived) ||
                    a.Visit.QueueLeft || a.Visit.HasCollected || a.Visit.RequestId!=0 || a.Visit.TrafficMovePending || a.Visit.PendingCounter!=null ||
                    a.Visit.TrafficPhaseSequence!=a.PhaseSequence || !ReferenceEquals(a.Visit.TrafficSelection,a.Selection) ||
                    !ReferenceEquals(a.Visit.IssuedTraffic,a.Issued) || !ReferenceEquals(a.Visit.PendingTraffic,a.Pending) || !ReferenceEquals(a.Visit.TrafficMove,a.Movement) ||
                    a.Actor.transform.position!=a.Start || a.Actor.Settings.AgentRadius!=a.Radius || a.Actor.Settings.CollisionSkin!=a.Skin ||
                    a.Actor.Settings.Epsilon!=a.Epsilon || !a.Claim.IsCurrent || !CounterAdvancePreviewMatches(a,a.Preview,false))return false;
                var rows=a.Counter.Snapshot.Where(row=>row.VisitId==a.Visit.Id).ToArray();
                return rows.Length==1 && rows[0].Arrived==a.Assignment.Arrived && rows[0].Generation==a.Assignment.Generation && rows[0].SlotIndex==a.Assignment.SlotIndex &&
                    rows[0].Position==a.Assignment.Position && a.Claim.Assignment.Equals(a.Assignment) &&
                    a.Key.Context==a.Visit.Id+"/assignment/"+a.Assignment.Generation &&
                    Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,a.Total) && r.OwnerId==a.Visit.Id && r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied);
            }
            catch(Exception){return false;}
        }
        private static bool CounterAdvancePreviewMatches(CounterAdvanceAnchor a,CounterQueueService.PreparedMutation preview,bool current)
            =>preview!=null && !preview.Consumed && preview.BelongsTo(a.Counter) && preview.Kind==CounterQueueService.MutationKind.Advance &&
                preview.Id==a.Visit.Id && preview.Assignment.Equals(a.Target) && preview.ExpectedVersion<=a.Counter.Version && (!current || preview.IsCurrent);
        // A fresh canonical preview may move this unissued goal earlier, never replace its physical assignment.
        // 只修订同原claim的更前FIFO目标；不改已到站row、年龄或容量账本。
        private static bool CounterAdvancePreviewCanReprepare(CounterAdvanceAnchor a,CounterQueueService.PreparedMutation preview)
            =>preview!=null && !preview.Consumed && preview.BelongsTo(a.Counter) && preview.Kind==CounterQueueService.MutationKind.Advance &&
                preview.Id==a.Visit.Id && preview.IsCurrent && preview.ExpectedVersion==a.Counter.Version &&
                preview.Assignment.VisitId==a.Assignment.VisitId && preview.Assignment.Generation==a.Assignment.Generation+1 &&
                !preview.Assignment.Arrived && preview.Assignment.SlotIndex>=0 && preview.Assignment.SlotIndex<=a.Assignment.SlotIndex &&
                (preview.Assignment.Equals(a.Target) || preview.Assignment.SlotIndex<a.Target.SlotIndex);
        private void RetireCounterAdvance(Visit visit,[System.Runtime.CompilerServices.CallerMemberName] string caller="",[System.Runtime.CompilerServices.CallerLineNumber] int callerLine=0)
        {
            var a=visit.PendingCounterAdvance;if(a==null)return;
#if UNITY_EDITOR
            if(CorridorObserver!=null)
                try{ObserveCorridor("traffic.counter-advance-retiring",visit,target:a.Target.Position,result:"caller="+caller+" line="+callerLine); }
                catch(Exception){ /* Readonly witness cannot alter the original retirement. */ }
#endif
            visit.PendingCounterAdvance=null;a.Retired=true;
            if(a.Source==null)return;
            var current=a.Coordinator.GetDecision(a.Key);
            if(current.FailureDisposition!=TrafficFailureDisposition.Terminal && current.AcquiredSequence==0 && !a.Coordinator.HasActiveGrant(a.Key))
                a.Coordinator.Retire(a.Key);
        }

        private sealed class PickupReservationAnchor
        {
            internal ServiceTrafficCoordinator Coordinator;
            internal TrafficIntentKey Key;
            internal PickupQueueService Owner;
            internal CounterQueueService Counter;
            internal CounterQueueService.AssignmentClaim CounterClaim;
            internal Visit Visit;
            internal NavigationActor Actor,Employee;
            internal CustomerAdmission Admission;
            internal CapacityService Capacity;
            internal NavigationWorld World;
            internal ReservationSnapshot[] Tokens;
            internal string EmployeeId;
            internal CustomerVisitState Phase;
            internal int Generation,Cursor,SideIndex,FirstFrame;
            internal NavigationTarget Side;
            internal Vector3 Start,Work,Exit;
            internal bool Submitted;
            internal TrafficUnpaidParkingEligibility Eligibility;
            internal PickupQueueService.PreparedMutation Preview;
        }
        // Keep the already committed right, not a cache of an uncommitted physical proposal.
        // 原canonical Entry/proof只读身份；其他身体移动不会重新申请这份未付款预留。
        private sealed class PickupParkingRight
        {
            internal ServiceTrafficCoordinator Coordinator;
            internal NavigationActor Actor;
            internal CustomerAdmission Admission;
            internal PickupQueueService Owner;
            internal PickupQueueService.ReservationClaim Claim;
            internal CapacityToken Token;
            internal TrafficDecision Source;
            internal TrafficRouteProof Proof;
            internal TrafficParkingFact Fact;
            internal int Revision;
            internal float CollisionSkin,Epsilon;
            internal bool Retired;
        }
        // One actual paid order owns one pending task, even before Transfer or Native acceptance.
        // 真实付款只创建一次phase；普通帧推进同一个task，不生成retry身份或重置预算。
        private sealed class PaidPickupTask
        {
            internal CafeOrderKey Order;
            internal int FirstFrame;
            internal TrafficIntentKey Intent,ActiveIntent;
            internal PickupParkingRight Parking;
            internal bool Accepted,Transferred,Failed,RecoveryUsed,RecoveryPending;
            internal ServiceTrafficCoordinator.MovementRecoveryReceipt RecoveryReceipt;
            internal PaidRecoveryCheckpoint Recovery;
            internal PaidPickupPreparation Preparation;
        }
        // Original producer identity only; no second queue/capacity ledger and no retroactive capture.
        // 首次Submit前只绑定真实身份；后续轮询不重签、不换key或重置预算。
        private sealed class PaidPickupPreparation
        {
            internal readonly CustomerFlowController Flow;
            internal readonly ServiceTrafficCoordinator Coordinator;
            internal readonly Visit Visit;
            internal readonly PaidPickupTask Task;
            internal readonly NavigationActor Actor;
            internal readonly CustomerAdmission Admission;
            internal readonly PickupParkingRight Parking;
            internal readonly TrafficDecision ParkingSource;
            internal readonly CounterQueueService Counter;
            internal readonly CounterQueueService.AssignmentClaim CounterClaim;
            internal readonly PickupQueueService.PreparedMutation Preview;
            internal readonly NavigationWorld World;
            internal readonly NavigationLayoutAdapter Adapter;
            internal readonly CapacityService Capacity;
            internal readonly CapacityToken Total;
            internal readonly CafeOrderKey Order;
            internal readonly TrafficIntentKey Intent;
            internal readonly int FirstFrame,Generation,PhaseSequence;
            internal readonly Vector3 Start,Target;
            internal readonly float Radius,Skin,Epsilon;
            internal readonly TrafficSelection Selection;
            internal readonly TrafficPhaseReceipt Pending,Issued;
            internal readonly TrafficMovement Movement;
            internal TrafficDecision Source;
            internal PaidPickupPreparation(CustomerFlowController flow,Visit visit,PaidPickupTask task,
                CounterQueueService.AssignmentClaim counterClaim,PickupQueueService.PreparedMutation preview,CapacityToken total)
            {
                Flow=flow;Coordinator=flow.traffic;Visit=visit;Task=task;Actor=visit.Actor;Admission=visit.Admission;
                Parking=task.Parking;ParkingSource=task.Parking.Source;Counter=flow.queue;CounterClaim=counterClaim;Preview=preview;
                World=flow.world;Adapter=flow.adapter;Capacity=flow.Capacity;Total=total;Order=task.Order;Intent=task.ActiveIntent;
                FirstFrame=task.FirstFrame;Generation=visit.Generation;PhaseSequence=visit.TrafficPhaseSequence;Start=Actor.transform.position;
                Target=preview.Assignment.Position;Radius=Actor.Settings.AgentRadius;Skin=Actor.Settings.CollisionSkin;Epsilon=Actor.Settings.Epsilon;
                Selection=visit.TrafficSelection;Pending=visit.PendingTraffic;Issued=visit.IssuedTraffic;Movement=visit.TrafficMove;
            }
        }
        private sealed class PaidRecoveryCheckpoint
        {
            internal bool BeforeTransfer;
            internal NavigationActor Actor;
            internal CustomerAdmission Admission;
            internal PickupQueueService Owner;
            internal PickupQueueService.ReservationClaim Claim;
            internal QueueAssignment Assignment;
            internal ReservationSnapshot[] Capacity;
            internal int Segments,Replans;
            internal CounterQueueService Counter;
            internal CounterQueueService.AssignmentClaim CounterClaim;
            internal Vector3 CounterPose;
        }
        // A consumer cursor over the coordinator's committed proof, not a second traffic ledger.
        internal sealed class TrafficMovement
        {
            internal ServiceTrafficCoordinator Coordinator;
            internal TrafficIntentKey Intent;
            internal TrafficDecision Source;
            // Original unissued preparation, distinct from the committed Source/strict-leg state.
            // 只保留原 producer seal/decision/出生事实，不缓存整份 Request 或重建 budget。
            internal TrafficDecision PendingSource;
            internal EmployeeFlowController.PreparedMovementContext PreparationContext;
            internal int PreparationFrame;
            internal Vector3 PreparationOrigin;
            internal string Resource;
            internal int Index,NativeGeneration;
            internal bool Suspended;
            internal NavigationTarget Goal,Target;
            internal TrafficRouteProof Proof;
        }
        private enum TrafficClaimKind { Counter,Pickup,Service }
        // References to the original canonical entries, never another reservation allocator.
        // 每项只保存原Entry/lease只读身份；不复制Queue业务账本。
        private sealed class TrafficClaimBinding
        {
            internal TrafficParkingFact Fact;
            internal TrafficClaimKind Kind;
            internal CounterQueueService Counter;
            internal CounterQueueService.AssignmentClaim CounterEntry;
            internal PickupQueueService Pickup;
            internal PickupQueueService.ReservationClaim PickupEntry;
            internal CapacityToken Token;
        }
        private sealed class TrafficSelection
        {
            internal TrafficIntentKey Intent;
            internal TrafficDecision Source;
            internal CustomerVisitState Phase;
            internal int FirstFrame,YieldPhase;
            internal QueueAssignment Assignment;
            internal CounterQueueService Counter;
            internal CounterQueueService.AssignmentClaim CounterEntry;
            internal PickupQueueService Pickup;
            internal PickupQueueService.ReservationClaim PickupEntry;
        }
        private sealed class TrafficPhaseReceipt : ITrafficPhaseMilestone
        {
            internal CustomerFlowController Flow;
            internal Visit Visit;
            internal CustomerAdmission Admission;
            internal NavigationActor Actor;
            internal ServiceTrafficCoordinator Coordinator;
            internal TrafficIntentKey Intent;
            internal TrafficDecision Source;
            internal TrafficWorldSnapshot SavedSnapshot;
            internal TrafficClaimBinding[] Claims;
            internal CapacityToken Total;
            internal int NativeGeneration;
            internal bool Marked;
            internal TrafficPhaseHandoff Handle;
            public TrafficDecision SourcePermit=>Source;
            public TrafficParkingFact OriginalClaim=>Claims.Length==0?null:Claims[0].Fact;
            public TrafficParkingFact CurrentClaim=>ReadClaim(OriginalClaim,out var current)==TrafficPhaseClaimState.Held?current:null;
            public object ActorLife=>Actor;
            public object BusinessOwner=>Visit;
            public object ReleaseObligation=>Admission;
            public TrafficPhaseMilestoneKind Kind=>Visit.HasCollected && Visit.CollectedOrder.HasValue?
                TrafficPhaseMilestoneKind.Collected:TrafficPhaseMilestoneKind.Arrived;
            public bool IsCurrent=>Marked && Flow.TrafficReceiptCurrent(this);
            public TrafficPhaseClaimState ReadClaim(TrafficParkingFact captured,out TrafficParkingFact current)
                =>Flow.ReadTrafficClaim(this,captured,out current);
            public bool AuthorizesSuccessor(TrafficIntentKey successor,TrafficDecision sourcePermit,int firstFrame)
                =>IsCurrent && Flow.TrafficSelectionCurrent(Visit,Visit.TrafficSelection) &&
                    Visit.TrafficSelection.Intent.Equals(successor) && ReferenceEquals(Visit.TrafficSelection.Source,sourcePermit) &&
                    Visit.TrafficSelection.FirstFrame==firstFrame && ReferenceEquals(Visit.TrafficMove?.Source,sourcePermit);
        }
        // Preparation selects a real canonical continuation, never a movement permit or early TrafficSelection.
        // 原receipt继续读取真实lease；初始Decision只认证一次，正常Wait/Ready/Native generation变化不洗身份。
        private sealed class CollectedExitPreparation:ITrafficPhaseMilestone
        {
            private readonly TrafficPhaseReceipt receipt;
            private readonly CustomerFlowController flow;private readonly Visit visit;
            private readonly NavigationActor actor;private readonly CustomerAdmission admission;
            private readonly PickupQueueService pickup;private readonly ServiceTrafficCoordinator coordinator;
            private readonly TrafficPhaseHandoff handle;private readonly TrafficDecision predecessor,initial;
            private readonly TrafficRequest request;private readonly CafeOrderKey order;
            private readonly CapacityToken total;private readonly int first;private readonly long sequence;
            internal CollectedExitPreparation(TrafficPhaseReceipt receipt,TrafficRequest request,TrafficDecision initial)
            {
                this.receipt=receipt;this.request=request;this.initial=initial;flow=receipt.Flow;visit=receipt.Visit;
                actor=visit.Actor;admission=visit.Admission;pickup=visit.PickupOwner;coordinator=receipt.Coordinator;
                handle=receipt.Handle;predecessor=receipt.Source;total=receipt.Total;
                order=visit.CollectedOrder.Value;first=visit.CollectionFrame;sequence=visit.ExitSequence;
            }
            public TrafficDecision SourcePermit=>predecessor;
            public TrafficParkingFact OriginalClaim=>receipt.OriginalClaim;
            public TrafficParkingFact CurrentClaim=>receipt.CurrentClaim;
            public object ActorLife=>actor;public object BusinessOwner=>visit;public object ReleaseObligation=>admission;
            public TrafficPhaseMilestoneKind Kind=>receipt.Kind;
            // Identity only: Pause/Nav progress gates belong to the caller and the original Core issue path.
            public bool IsCurrent=>receipt.IsCurrent && ReferenceEquals(receipt.Flow,flow) && ReferenceEquals(receipt.Visit,visit) &&
                ReferenceEquals(receipt.Actor,actor) && ReferenceEquals(receipt.Admission,admission) && ReferenceEquals(receipt.Total,total) &&
                ReferenceEquals(receipt.Coordinator,coordinator) && ReferenceEquals(receipt.Handle,handle) && ReferenceEquals(receipt.Source,predecessor) &&
                handle!=null && ReferenceEquals(flow.traffic,coordinator) && flow.visits.TryGetValue(visit.Id,out var current) && ReferenceEquals(current,visit) &&
                ReferenceEquals(visit.PendingTraffic,receipt) && ReferenceEquals(visit.Actor,actor) && ReferenceEquals(visit.Admission,admission) &&
                ReferenceEquals(visit.PickupOwner,pickup) && visit.ServiceArrived && visit.HasCollected && visit.State==CustomerVisitState.Exiting &&
                visit.CollectedOrder.HasValue && visit.CollectedOrder.Value.Equals(order) && visit.CollectionFrame==first &&
                sequence>0 && visit.ExitSequence==sequence && request.RequestedFrame==first &&
                request.Intent.ActorId==visit.Id && request.Intent.Session==flow.trafficSession && request.Intent.Epoch==flow.trafficEpoch &&
                flow.adapter!=null && request.Intent.LayoutRevision==flow.adapter.Revision && request.Intent.Context==CollectionTrafficContext(visit) &&
                flow.exit!=null && request.Target==flow.exit.position && visit.FinalTarget.Position==request.Target &&
                receipt.Claims!=null && receipt.Claims.Any(claim=>claim!=null && claim.Kind==TrafficClaimKind.Service && ReferenceEquals(claim.Pickup,pickup));
            public TrafficPhaseClaimState ReadClaim(TrafficParkingFact captured,out TrafficParkingFact current)
            {
                current=null;if(!IsCurrent)return TrafficPhaseClaimState.Invalid;
                return receipt.ReadClaim(captured,out current);
            }
            public bool AuthorizesSuccessor(TrafficIntentKey successor,TrafficDecision source,int firstFrame)
                =>IsCurrent && successor.Equals(request.Intent) && initial!=null && initial.Intent.Equals(successor) &&
                    ReferenceEquals(source,initial) && firstFrame==first;
        }

        private sealed class PickupExitClaim
        {
            public Visit Owner;
            public NavigationActor Actor;
            public PickupQueueService PickupOwner;
            public Queue<Vector3> Route;
            public CustomerVisitState Phase;
            public QueueAssignment Assignment;
        }
        private sealed class PickupExitWait
        {
            public int Generation,Revision;
            public Queue<Vector3> Route;
            public readonly List<PickupExitClaim> Claims=new List<PickupExitClaim>();
        }
        private readonly Dictionary<string,Visit> visits=new Dictionary<string,Visit>();
#if UNITY_EDITOR
        internal Action<string,string,PickupQueueService,QueueAssignment> PickupObserver;
        internal Action<TrafficRequest> AdmissionPreparationObserver; // Editor-only, default off; original request, no policy replay.
        internal Action<TrafficParkingFact> ParkingNotificationObserver; // Actual original Notify args, default null; no policy replay.
        internal Action<string> CollectedExitPreparationObserver; // Default off; original locals only, never policy replay.
        internal int CollectedExitPreparationObservationErrors; // Reader reports UNKNOWN on any enabled witness failure.
        internal Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string> CorridorObserver;
        // Cold-only witness / 只在原始segment、WAIT、replan均拒绝后调用。
        internal Action<string> SegmentFailureObserver;
        internal Action<string,NavigationActor,IReadOnlyList<Vector3>,Vector3[],float,bool?,bool?> DeliveryAcquisitionObserver;
        private long exitPlanObservation;
        private long admissionObservation;
#endif
        private enum PickupWaitRefusal { NotEvaluated,StaticPathNull,FormalTargetBlocked,NativeIneligible,
            CustomerRequestOrPhaseIneligible,IndependentRouteBlocked,ParkingFinalTargetBlocked,
            EmployeeNoPath,EmployeeCurrentSegmentDestinationBlocked,NoObstructingBody,Qualified }
        private struct PickupWaitFailure
        {
            public PickupWaitRefusal Reason;
            public NavigationActor Actor;
            public bool? Active,AgentExists,Enabled,OnNavMesh,Stopped,Pending,Complete,HasPath;
            public Vector3? Destination;
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ObservePickup(string operation,Visit visit,PickupQueueService owner,QueueAssignment assignment)
        {
#if UNITY_EDITOR
            PickupObserver?.Invoke(operation,visit.Id,owner,assignment);
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ObserveCorridor(string operation,Visit visit=null,Visit related=null,Vector3 target=default,
            IReadOnlyList<Vector3> path=null,Vector3[] obstacles=null,string result=null)
        {
#if UNITY_EDITOR
            CorridorObserver?.Invoke(operation,visit?.Id,related?.Id,target,path,obstacles,result);
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ObserveDeliveryAcquisition(string operation,NavigationActor actor,IReadOnlyList<Vector3> path,
            Vector3[] occupied,float clearance,bool? physical=null,bool? owned=null)
        {
#if UNITY_EDITOR
            DeliveryAcquisitionObserver?.Invoke(operation,actor,path,occupied,clearance,physical,owned);
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ObserveAdmissionDecision(string operation,Visit earlier=null,Vector3 target=default,IReadOnlyList<Vector3> path=null,
            Vector3? destination=null,float? clearance=null,bool? targetClear=null,bool? follows=null,bool? entryVacates=null,
            bool? conflict=null,bool? accepted=null,Visit incoming=null,string detail=null)
        {
#if UNITY_EDITOR
            if(CorridorObserver==null) return;
            string Flag(bool? value)=>value.HasValue?value.Value.ToString():"NOT_EVALUATED";
            string Reference(object value)=>value==null?"NULL":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value).ToString();
            var sequence=incoming==null?admissionObservation:incoming.AdmissionObservation;
            var proposed=incoming==null?session+"-"+(nextVisit+1).ToString("D8"):incoming.Id;
            // Only original values: never re-run an owned-binding predicate or navigation query.
            // nullable保留原short-circuit未评估项；published与native accepted是不同事件。
            var original=earlier==null?"earlier=NOT_EVALUATED":
                "earlier="+earlier.Id+" state="+earlier.State+" hasCollected="+earlier.HasCollected+" request="+earlier.RequestId+" generation="+earlier.Generation+
                " currentSegmentTarget="+earlier.CurrentSegmentTarget.Position.ToString("F9")+" route="+string.Join(";",(earlier.Route??Enumerable.Empty<Vector3>()).Select(p=>p.ToString("F9")))+
                " ownedPhase="+earlier.OwnedRoutePhase+" ownedRevision="+earlier.OwnedRouteRevision+" ownedSegments="+earlier.OwnedRouteSegments+
                " ownedIssuedRequest="+earlier.OwnedIssuedRequest+" ownedIssuedGeneration="+earlier.OwnedIssuedGeneration+" ownedWasClear="+earlier.OwnedRouteWasClear+
                " ownedOwnerReference="+Reference(earlier.OwnedRouteOwner)+" pickupOwnerReference="+Reference(earlier.PickupOwner)+
                " ownedSourceReference="+Reference(earlier.OwnedRouteSource)+" routeReference="+Reference(earlier.Route)+
                " ownedAssignment="+earlier.OwnedRouteAssignment.Position.ToString("F9")+" ownedAssignmentGeneration="+earlier.OwnedRouteAssignment.Generation+
                " ownedAssignmentVisit="+earlier.OwnedRouteAssignment.VisitId+" ownedAssignmentSlot="+earlier.OwnedRouteAssignment.SlotIndex+
                " ownedPath="+string.Join(";",(earlier.OwnedRoutePath??Array.Empty<Vector3>()).Select(p=>p.ToString("F9")));
            ObserveCorridor(operation,earlier,incoming,target,path,result:"sequence="+sequence+" nextVisitCounter="+nextVisit+" proposedVisit="+proposed+
                " nativeDestination="+(destination.HasValue?destination.Value.ToString("F9"):"NOT_EVALUATED")+
                " clearance="+(clearance.HasValue?clearance.Value.ToString("R"):"NOT_EVALUATED")+" targetClear="+Flag(targetClear)+
                " follows="+Flag(follows)+" entryVacates="+Flag(entryVacates)+" routesConflict="+Flag(conflict)+" accepted="+Flag(accepted)+
                " "+original+" "+detail);
#endif
        }
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
        // Only an observed real front failure can hold a later arrival.
        // 绑定真实队首、队列与布局；CanReserve恢复不等于已取得安全路线。
        private Visit refusedPickupFront;
        private CounterQueueService refusedCounterQueue;
        private PickupQueueService refusedPickupQueue;
        private int refusedPickupRevision;
        private Vector3[] pickupWaitSlots=Array.Empty<Vector3>();
        private struct PickupGoalIdentity
        {
            public Visit Visit;
            public PickupQueueService Owner;
            public QueueAssignment Assignment;
            public int Generation;
            public CustomerVisitState State;
            public bool InTransit,ActorActive;
        }
        private const int PickupGoalQueryBudget=4000;
        private int pickupGoalQueryFrame=-1,pickupGoalQueries;
        private string pickupGoalProofStatus="";
        // One refusal only; never cache permission across a moving actor's pose.
        // 只缓存当前候选的拒绝；真实预留/到位/布局改变后重新检查，不缓存移动许可。
        private Visit pickupGoalRefusalIncoming;
        private int pickupGoalRefusalGeneration,pickupGoalRefusalRevision;
        private Vector3 pickupGoalRefusalTarget;
        private PickupGoalIdentity[] pickupGoalRefusalOwners;
        private Vector3[] pickupGoalRefusalObstacles;
        private string pickupGoalRefusalStatus;
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
        private long pickupPreparationLife;
        private PickupDeliveryHistory pickupDeliveryHistory;
        private IReadOnlyList<Vector3>[] deliveryTrafficPaths;
        private Func<string,IReadOnlyList<NavigationTarget>> pickupReturnTraffic;
        internal void SetPickupReturnTraffic(Func<string,IReadOnlyList<NavigationTarget>> provider) => pickupReturnTraffic=provider;
        private Func<string,bool> expectedCounterDeparture;
        internal void SetExpectedCounterDeparture(Func<string,bool> provider) => expectedCounterDeparture=provider;
        private Func<string,Func<CafeOrderKey?,bool>> counterDepartureTaskCapture;
        internal void SetCounterDepartureTaskCapture(Func<string,Func<CafeOrderKey?,bool>> provider) => counterDepartureTaskCapture=provider;
        private ServiceTrafficCoordinator traffic;
        private string trafficSession;
        private long trafficEpoch,trafficCanonicalVersion,trafficBodyVersion;
        private Func<string,float> employeeTrafficProgress,counterTrafficProgress;
        private Func<string,bool> employeeJourneyCurrent;
        private Func<string,bool> trafficDeliveryGate;
        private NavigationTarget trafficDeliveryTarget;
        private IReadOnlyList<NavigationTarget> trafficDeliveryOutward,trafficDeliveryHome;
        private readonly Dictionary<string,TrafficIntentKey> trafficKeys=new Dictionary<string,TrafficIntentKey>();
        private readonly Dictionary<string,TrafficIntentKey> trafficActorKeys=new Dictionary<string,TrafficIntentKey>();
        private readonly Dictionary<TrafficIntentKey,TrafficCandidateCursor> trafficCandidates=new Dictionary<TrafficIntentKey,TrafficCandidateCursor>();
        private object[] trafficCanonicalFacts=Array.Empty<object>(),trafficBodyFacts=Array.Empty<object>();
        private TrafficWorldSnapshot trafficSnapshot;
        private int nextTrafficPhase;
        private bool admissionInProgress;
        private bool preparingTrafficProposals;
        private CounterAdmissionAnchor pendingAdmissionAnchor;
        internal void BindTraffic(ServiceTrafficCoordinator coordinator,string sessionId,long epoch,
            Func<string,float> employeeProgress,Func<string,float> counterProgress,Func<string,bool> currentJourney)
        {
            traffic=coordinator;trafficSession=sessionId;trafficEpoch=epoch;
            pickupPreparationLife++;pickupDeliveryHistory=null;
            pendingAdmissionAnchor=null;
            employeeTrafficProgress=employeeProgress;counterTrafficProgress=counterProgress;employeeJourneyCurrent=currentJourney;
            trafficKeys.Clear();trafficActorKeys.Clear();trafficCandidates.Clear();nextTrafficPhase=0;
            trafficCanonicalFacts=trafficBodyFacts=Array.Empty<object>();trafficCanonicalVersion=trafficBodyVersion=0;
            foreach(var visit in visits.Values)
            {
                RetireCounterAdvance(visit);
                DiscardPaidRecovery(visit);
                if(visit.PendingCounter!=null)visit.PendingCounter.Coordinator.Retire(visit.PendingCounter.Intent);
                visit.TrafficJourney=null;visit.PendingCounter=null;visit.PickupTrafficPermit=null;visit.PickupParking=null;visit.PaidPickup=null;
                visit.TrafficMove=null;visit.TrafficMovePending=false;visit.TrafficSelection=null;
                visit.IssuedTraffic=visit.PendingTraffic=null;visit.TrafficCandidateFrames.Clear();
                visit.PendingReservationAnchor=null;
                DiscardYieldContinuation(YieldContinuation(visit));
                visit.ReturnPreparation=null;visit.YieldDeliverySource=null;visit.ReturnHistory=null;
            }
        }
        private TrafficIntentKey TrafficKey(string actorId,string phase,string context)
        {
            var id=actorId+"/"+phase+"/"+context;
            if(!trafficKeys.TryGetValue(id,out var key))
            {key=new TrafficIntentKey(trafficSession,trafficEpoch,actorId,phase,++nextTrafficPhase,adapter.Revision,context);trafficKeys.Add(id,key);}
            return key;
        }
        private bool TrafficActorReady(NavigationActor actor)=>actor!=null && actor.isActiveAndEnabled && actor.Agent!=null &&
            actor.Agent.enabled && actor.Agent.isOnNavMesh && world!=null && world.RegisteredActors.Any(current=>ReferenceEquals(current,actor));
        private TrafficCandidateCursor GetTrafficCursor(TrafficIntentKey key,NavigationActor actor,TrafficCandidateRole role,
            NavigationTarget[] sides)
        {
            if(!trafficCandidates.TryGetValue(key,out var cursor))
            {
                // Retired Core keys retain the stop fact after their heavy producer cursor is released.
                if(traffic.GetDecision(key).FailureDisposition==TrafficFailureDisposition.Terminal)return null;
                cursor=new TrafficCandidateCursor(key,actor,sides,actor.transform.position,role,-1,null);
                trafficCandidates.Add(key,cursor);
            }
            return cursor;
        }
        private void RetireTrafficCandidate(TrafficIntentKey key)
        {traffic.Retire(key);trafficCandidates.Remove(key);}
        private bool SelectTrafficCandidate(TrafficCandidateCursor cursor,NavigationActor actor,
            Func<int,NavigationTarget,Vector3,bool> available,out int index,out TrafficDecision current)
        {
            index=-1;current=cursor.Source==null?null:traffic.GetDecision(cursor.Key);
            if(!AnchorContextCurrent(traffic,cursor.Key) || !TrafficActorReady(actor))return false;
            // A pending selected side is not reconsidered by canonical availability polling.
            // Waiting/Deferred仍属于原候选；只有首次提交或精确Alternate可检查其他原side。
            if(current!=null && current.FailureDisposition!=TrafficFailureDisposition.AlternateAllowed)
                return cursor.TrySelect(cursor.Key,actor,current,true,Array.Empty<int>(),out index);
            var candidates=new List<int>();
            for(var candidate=0;candidate<cursor.Count;candidate++)
                if(!cursor.HasAttempted(candidate) && !cursor.StaticInvalidIndices.Contains(candidate) &&
                    cursor.TryDescribe(cursor.Key,actor,candidate,out var side,out var target) && available(candidate,side,target))
                {candidates.Add(candidate);break;}
            return cursor.TrySelect(cursor.Key,actor,current,true,candidates,out index);
        }
        private bool PrepareTrafficCandidate(TrafficCandidateCursor cursor,TrafficRequest request,NavigationActor actor,int index,
            TrafficDecision expected,out TrafficDecision next)
        {
#if UNITY_EDITOR
            if(CorridorObserver!=null)
            {
                var alternate=expected!=null && expected.FailureDisposition==TrafficFailureDisposition.AlternateAllowed &&
                    ReferenceEquals(traffic.GetDecision(cursor.Key),expected) && index!=cursor.CurrentIndex &&
                    !cursor.HasAttempted(index) && !ReferenceEquals(cursor.ObservedAdvanceSource,expected);
                if(alternate || cursor.Source==null && !cursor.ObservedPreflight)
                {
                    if(alternate)cursor.ObservedAdvanceSource=expected;else cursor.ObservedPreflight=true;
                    ObserveCorridor(alternate?"traffic.candidate-before-advance":"traffic.candidate-preflight",target:request.Target,
                        result:"key="+cursor.Key.Session+"/e"+cursor.Key.Epoch+"/"+cursor.Key.ActorId+"/"+cursor.Key.IntentId+
                            "/g"+cursor.Key.Generation+"/r"+cursor.Key.LayoutRevision+"/"+cursor.Key.Context+" role="+cursor.Role+
                            " oldIndex="+cursor.CurrentIndex+" nextIndex="+index+" count="+cursor.Count+
                            " source="+expected?.Kind+"/"+expected?.FailureDisposition+" requestSequence="+expected?.RequestSequence+
                            " acquired="+expected?.AcquiredSequence+" queries="+traffic.QueriesConsumed(cursor.Key));
                }
            }
#endif
            return cursor.TryPrepare(traffic,request,actor,index,expected,out next);
        }
        // Authenticate an existing service lease; this marker never acquires or releases it.
        // 只读原collection Entry或delivery lifetime；无关Queue版本变化不能替代真实lease身份。
        private sealed class PickupServiceLeaseCheckpoint:ITrafficClaimCheckpoint
        {
            private readonly PickupQueueService owner;
            private readonly string actorId;
            private readonly Vector3 position;
            private readonly PickupQueueService.ReservationClaim collection;
            private readonly PickupQueueService.DeliveryLeaseClaim delivery;
            private PickupServiceLeaseCheckpoint(PickupQueueService owner,string actorId,Vector3 position,
                PickupQueueService.ReservationClaim collection,PickupQueueService.DeliveryLeaseClaim delivery)
            {
                this.owner=owner;this.actorId=actorId;this.position=position;
                this.collection=collection;this.delivery=delivery;CapturedOwnerVersion=owner.Version;
            }
            internal static PickupServiceLeaseCheckpoint Capture(PickupQueueService owner,string actorId,Vector3 position)
            {
                if(owner==null || !owner.TryGetServiceLease(actorId,out var actual) || !actual.Equals(position))return null;
                if(owner.TryCaptureReservation(actorId,out var collection) && collection.IsCurrentCollectionLease && collection.Assignment.Position.Equals(position))
                    return new PickupServiceLeaseCheckpoint(owner,actorId,position,collection,null);
                if(owner.TryCaptureDeliveryLease(actorId,out var delivery) && delivery.IsCurrent && delivery.Position.Equals(position))
                    return new PickupServiceLeaseCheckpoint(owner,actorId,position,null,delivery);
                return null;
            }
            public object CanonicalOwner=>owner;
            public string ResourceId=>"service/"+actorId;
            public long CapturedOwnerVersion{get;}
            public bool IsSameCurrentClaim(ITrafficClaimCheckpoint current)
            {
                if(!(current is PickupServiceLeaseCheckpoint other) || !ReferenceEquals(owner,other.owner) || actorId!=other.actorId || !position.Equals(other.position) ||
                    CapturedOwnerVersion>other.CapturedOwnerVersion || other.CapturedOwnerVersion!=owner.Version)return false;
                if(collection!=null)return other.collection!=null && collection.IsCurrentCollectionLease && other.collection.IsCurrentCollectionLease &&
                    collection.IsSameCurrentClaim(other.collection);
                return delivery!=null && other.delivery!=null && delivery.IsCurrent && other.delivery.IsCurrent && delivery.IsSameLease(other.delivery);
            }
        }
        private TrafficIntentKey TrafficBodyOwner(string actorId)
        {
            if(trafficActorKeys.TryGetValue(actorId,out var key))
            {
                var decision=traffic.GetDecision(key);
                // A committed owner survives proposal/revalidation; only actual retirement releases this identity.
                // 未提交候选不能改变canonical owner；真实退休后使用稳定body事实身份。
                if(decision.Kind!=TrafficDecisionKind.Cancelled)return key;
                trafficActorKeys.Remove(actorId);
            }
            return TrafficKey(actorId,"body",actorId);
        }
        internal TrafficWorldSnapshot RefreshTrafficSnapshot()
        {
            if(traffic==null) return null;
            var bodies=new List<TrafficBodyFact>();var parking=new List<TrafficParkingFact>();
            foreach(var actor in world.RegisteredActors.Where(a=>a!=null).OrderBy(a=>a.ActorId,StringComparer.Ordinal))
            {
                var enabled=actor.isActiveAndEnabled && actor.Agent!=null && actor.Agent.enabled && actor.Agent.isOnNavMesh;
                var remaining=0f;
                if(enabled && visits.TryGetValue(actor.ActorId,out var visit))
                {
                    if(visit.RequestId!=0 && visit.State!=CustomerVisitState.Blocked)
                        remaining=actor.Settings.MinimumSegmentSeconds+actor.Settings.NoProgressSeconds;
                    else if(visit.State==CustomerVisitState.Queued && visit.CounterArrived)
                        remaining=counterTrafficProgress?.Invoke(visit.Id)??0;
                }
                else if(enabled)
                {
                    remaining=employeeTrafficProgress?.Invoke(actor.ActorId)??0;
                    if(trafficActorKeys.TryGetValue(actor.ActorId,out var existing) && existing.IntentId=="employee-pickup-journey" &&
                        traffic.HasActiveGrant(existing) && employeeJourneyCurrent!=null && !employeeJourneyCurrent(actor.ActorId))RetireTrafficCandidate(existing);
                }
                bodies.Add(new TrafficBodyFact(TrafficBodyOwner(actor.ActorId),actor.transform.position,actor.Settings.AgentRadius,
                    enabled,remaining>0,remaining,actor.Settings.CollisionSkin+actor.Settings.Epsilon,actor,actorActive:actor.isActiveAndEnabled));
            }
            foreach(var visit in visits.Values.OrderBy(v=>v.Id,StringComparer.Ordinal))
            {
                if(visit.Actor==null) continue;
                var settings=visit.Actor.Settings;var key=TrafficBodyOwner(visit.Id);
                if(visit.PickupAssigned && visit.PickupOwner!=null && visit.PickupOwner.TryCaptureReservation(visit.Id,out var pickupClaim))
                    parking.Add(new TrafficParkingFact(key,visit.PickupOwner,visit.PickupOwner.Version,"pickup/"+visit.Id,pickupClaim.Assignment.Position,settings.AgentRadius,settings.CollisionSkin+settings.Epsilon,visit,visit.Admission,semanticCheckpoint:pickupClaim));
                if(queue!=null && !visit.QueueLeft && queue.TryCaptureAssignment(visit.Id,out var counterClaim))
                    parking.Add(new TrafficParkingFact(key,queue,queue.Version,"counter/"+visit.Id,counterClaim.Assignment.Position,settings.AgentRadius,settings.CollisionSkin+settings.Epsilon,visit,visit.Admission,semanticCheckpoint:counterClaim));
            }
            foreach(var owner in visits.Values.Select(v=>v.PickupOwner).Concat(new[]{pickupQueue}).Where(q=>q!=null).Distinct())
                foreach(var lease in owner.ServiceLeases.OrderBy(l=>l.Key,StringComparer.Ordinal))
                {
                    var actor=world.RegisteredActors.FirstOrDefault(a=>a!=null && a.ActorId==lease.Key);
                    visits.TryGetValue(lease.Key,out var visit);
                    if(actor!=null) parking.Add(new TrafficParkingFact(TrafficBodyOwner(lease.Key),owner,owner.Version,"service/"+lease.Key,
                        lease.Value,actor.Settings.AgentRadius,actor.Settings.CollisionSkin+actor.Settings.Epsilon,visit,visit?.Admission,
                        semanticCheckpoint:PickupServiceLeaseCheckpoint.Capture(owner,lease.Key,lease.Value)));
                }
            var canonical=parking.SelectMany(p=>new object[]{p.Owner,p.CanonicalOwner,p.OwnerVersion,p.ResourceId,p.Position,p.Radius,p.SafetyMargin,p.BusinessOwner,p.ReleaseObligation})
                .Concat(new object[]{pickupQueue,pickupQueue?.Version??0,queue,queue?.Version??0}).ToArray();
            var body=bodies.SelectMany(b=>new object[]{b.Owner,b.ActorLife,b.Position,b.Radius,b.Enabled,b.ActorActive,b.IndependentlyProgressing,b.SafetyMargin}).ToArray();
            if(!canonical.SequenceEqual(trafficCanonicalFacts)){trafficCanonicalVersion++;trafficCanonicalFacts=canonical;}
            if(!body.SequenceEqual(trafficBodyFacts))
            {
                trafficBodyVersion++;trafficBodyFacts=body;
            }
            var previous=trafficSnapshot;
            trafficSnapshot=new TrafficWorldSnapshot(trafficCanonicalVersion,trafficBodyVersion,bodies,parking);traffic.SetSnapshot(trafficSnapshot);
            if(previous!=null)
                foreach(var old in previous.Bodies)
                {
                    var current=bodies.FirstOrDefault(b=>b.Owner.ActorId==old.Owner.ActorId);
                    // Moving positions refresh final safety checks; only an actual progress/lifecycle checkpoint wakes a wait.
                    if(current==null || !ReferenceEquals(current.ActorLife,old.ActorLife) || !current.Owner.Equals(old.Owner) || current.Enabled!=old.Enabled || current.ActorActive!=old.ActorActive || current.IndependentlyProgressing!=old.IndependentlyProgressing)
                        traffic.NotifyChanged(old.Owner,"body");
                }
            if(previous!=null && previous.Version!=trafficSnapshot.Version)
                foreach(var old in previous.Parking)
                {
                    if(ParkingNotificationUnchanged(old,parking,previous.Bodies,bodies))continue;
                    traffic.NotifyChanged(old.Owner,old.ResourceId);
#if UNITY_EDITOR
                    try{ParkingNotificationObserver?.Invoke(old);}catch(Exception){ } // Observation cannot alter the original notification.
#endif
                }
            return trafficSnapshot;
        }
        // Equal known-owner stamps preserve lineage; a changed/unknown stamp always keeps conservative notification.
        // 只跳过已确证不变的原resource；不从缺失/未知推断释放，普通运动仍由body checkpoint路径处理。
        private static bool ParkingNotificationUnchanged(TrafficParkingFact old,IReadOnlyList<TrafficParkingFact> currentParking,
            IReadOnlyList<TrafficBodyFact> beforeBodies,IReadOnlyList<TrafficBodyFact> currentBodies)
        {
            if(old==null)return false;
            var matches=currentParking.Where(p=>p!=null && p.Owner.Equals(old.Owner) &&
                ReferenceEquals(p.CanonicalOwner,old.CanonicalOwner) && p.ResourceId==old.ResourceId).ToArray();
            if(matches.Length!=1)return false;
            var same=matches[0];
            if(same.OwnerVersion!=old.OwnerVersion || !same.Position.Equals(old.Position) || same.Radius!=old.Radius || same.SafetyMargin!=old.SafetyMargin ||
                !ReferenceEquals(same.BusinessOwner,old.BusinessOwner) || !ReferenceEquals(same.ReleaseObligation,old.ReleaseObligation) ||
                !TrafficClaimCheckpoints.AreSame(old,same))return false;
            if(old.CanonicalOwner is CounterQueueService counter){if(counter.Version!=same.OwnerVersion)return false;}
            else if(old.CanonicalOwner is PickupQueueService pickup){if(pickup.Version!=same.OwnerVersion)return false;}
            else return false;
            var before=beforeBodies.Where(b=>b!=null && b.Owner.Equals(old.Owner)).ToArray();
            var after=currentBodies.Where(b=>b!=null && b.Owner.Equals(old.Owner)).ToArray();
            return before.Length==1 && after.Length==1 && before[0].ActorLife!=null && ReferenceEquals(before[0].ActorLife,after[0].ActorLife) &&
                before[0].Enabled==after[0].Enabled && before[0].ActorActive==after[0].ActorActive;
        }
        internal bool HasTrafficCoordinator => integratedMode && traffic!=null;
        internal TrafficMovement CreateEmployeeMovement(NavigationActor actor,string context,EmployeeState state,NavigationTarget target)
        {
            TrafficDecision source=null;TrafficIntentKey key=default;
            var selected=(state==EmployeeState.CarryingToPickUp || state==EmployeeState.Returning) &&
                trafficActorKeys.TryGetValue(actor.ActorId,out key) && key.IntentId=="employee-pickup-journey" && key.Context==context &&
                traffic.TryGetCommittedJourney(key,out source);
            if(!selected)key=TrafficKey(actor.ActorId,"employee-work",context);
            return new TrafficMovement{Coordinator=traffic,Intent=key,Source=source,Goal=target,
                Resource=selected?(state==EmployeeState.Returning?"selected-home":"pickup/"+actor.ActorId):"movement/"+actor.ActorId};
        }
        internal TrafficDecision PrepareMovement(NavigationActor actor,TrafficMovement movement,int nativeGeneration)
        {
            var coordinator=movement.Coordinator;
            if(!CanIntegratedProgress || !ReferenceEquals(coordinator,traffic) || actor==null || !actor.isActiveAndEnabled ||
                movement.Intent.Epoch!=trafficEpoch || movement.Intent.LayoutRevision!=adapter.Revision)
                return new TrafficDecision(movement.Intent,TrafficDecisionKind.StaleContext,"movement-lifecycle-changed");
            var snapshot=RefreshTrafficSnapshot();
            if(movement.Source==null)
            {
                visits.TryGetValue(actor.ActorId,out var movingVisit);
                var typed=movement.PreparationContext!=null;
                if(typed && !MovementPreparationCurrent(actor,movement))
                    return RefuseMovementPreparation(movement,"movement-producer-changed");
                if(typed && movement.PendingSource==null && coordinator.ContainsIntent(movement.Intent))
                    return RefuseMovementPreparation(movement,"movement-original-source-missing");
                var sourceFrame=typed?movement.PreparationFrame:movingVisit!=null?CustomerSourceFrame(movingVisit,movement.Intent):Time.frameCount;
#if UNITY_EDITOR
                if(CorridorObserver!=null && !coordinator.ContainsIntent(movement.Intent))
                    CorridorObserver("traffic.movement-preflight",actor.ActorId,null,movement.Goal.Position,null,null,
                        "key="+movement.Intent.Session+"/e"+movement.Intent.Epoch+"/"+movement.Intent.ActorId+"/"+movement.Intent.IntentId+
                        "/g"+movement.Intent.Generation+"/r"+movement.Intent.LayoutRevision+"/"+movement.Intent.Context);
#endif
                // Poll the original typed source; metadata is refreshed only at the existing pre-pump checkpoint.
                // 已提交的原准备不再经普通 Submit 补签 snapshot；获权/strict 分支仍使用下方原合同。
                var source=typed && movement.PendingSource!=null?coordinator.GetDecision(movement.Intent):
                    coordinator.EvaluateMovement(typed?EmployeeMovementRequest(movement,snapshot):new TrafficRequest(movement.Intent,sourceFrame,snapshot,
                        actor.transform.position,movement.Goal.Position,actor.Settings.AgentRadius,movement.Resource,
                        safetyMargin:actor.Settings.CollisionSkin+actor.Settings.Epsilon,planPrimaryRoute:true));
                if(typed)movement.PendingSource=source;
                if(source.Kind!=TrafficDecisionKind.Granted)return source;
                if(typed && (!MovementPreparationCurrent(actor,movement) || source.AcquiredSequence!=0 ||
                    coordinator.HasActiveGrant(movement.Intent) || coordinator.TryGetCommittedJourney(movement.Intent,out _)))
                    return RefuseMovementPreparation(movement,"movement-primary-precommit-changed");
                if(typed && coordinator.TryRevokePhysicalPreparedPreparation(source,out var revoked))
                {
                    movement.PendingSource=revoked;
                    if(!coordinator.TryRefreshQueuedPreparation(revoked,EmployeeMovementRequest(movement,snapshot),out var refreshed))
                        return RefuseMovementPreparation(movement,"movement-revoked-refresh-rejected");
                    movement.PendingSource=refreshed;return refreshed;
                }
                if(!coordinator.TryCommit(source))return coordinator.GetDecision(movement.Intent);
                movement.Source=source;
                if(movingVisit!=null)
                {movingVisit.TrafficSelection=CaptureTrafficSelection(movingVisit,source,movingVisit.State);PublishCustomerOwner(movingVisit,movement.Intent);}
                else trafficActorKeys[actor.ActorId]=movement.Intent;
                snapshot=RefreshTrafficSnapshot();
            }
            movement.Proof=movement.Source.ProofRoutes.FirstOrDefault(p=>p.ResourceId==movement.Resource);
            if(movement.Proof==null || movement.Index<0 || movement.Index>=movement.Proof.Waypoints.Count)
                return new TrafficDecision(movement.Intent,TrafficDecisionKind.InvalidConfiguration,"missing-current-leg-proof");
            var point=movement.Proof.Waypoints[movement.Index];
            movement.Target=movement.Index==movement.Proof.Waypoints.Count-1 && point==movement.Goal.Position?movement.Goal:new NavigationTarget(point);
            movement.NativeGeneration=nativeGeneration;
            var actualStart=actor.transform.position;
            var requeue=coordinator.TryRequeuePreparedLeg(movement.Source,movement.Resource,movement.Index,actor,nativeGeneration,snapshot,actualStart,out var prepared);
            // Only NoOp delegates; a rejected authentic source must never fall through to the old body-only branch.
            // Requeued/Rejected 都返回真实 Core 结果，保留原 caller 的延后或失败处理。
            if(requeue==ServiceTrafficCoordinator.PreparedLegRequeueResult.NoOp)
                prepared=coordinator.PrepareLeg(movement.Source,movement.Resource,movement.Index,actor,nativeGeneration,snapshot,
                    actualStart,actor.Settings.ArrivalDistance,Time.frameCount);
            ObserveReturnPreparation(actor,movement,snapshot,prepared);return prepared;
        }
        internal bool CommitMovement(TrafficMovement movement,TrafficDecision decision)
        {
            if(!ReferenceEquals(movement.Coordinator,traffic) || !movement.Coordinator.TryCommit(decision))return false;
            movement.Suspended=false;return true;
        }
        internal bool CanIssueMovement(NavigationActor actor,TrafficMovement movement)
            => CanIntegratedProgress && ReferenceEquals(movement.Coordinator,traffic) && actor!=null && actor.isActiveAndEnabled &&
                movement.Coordinator.CanIssueLeg(movement.Intent,actor,movement.NativeGeneration);
        internal bool BindMovement(NavigationActor actor,TrafficMovement movement,long request)
        {
            if(!isActiveAndEnabled || disposed || !ReferenceEquals(movement.Coordinator,traffic) ||
                !movement.Coordinator.BindLeg(movement.Intent,actor,new TrafficNativeStamp(request,movement.NativeGeneration)))return false;
            var h=pickupDeliveryHistory;
            if(h!=null && ReferenceEquals(h.Actor,actor) && ReferenceEquals(h.ReturnMovement,movement) && h.ReturnGeneration==movement.NativeGeneration)
                h.ReturnStamp=new TrafficNativeStamp(request,movement.NativeGeneration);
            return true;
        }
        internal bool CompleteMovement(NavigationActor actor,TrafficMovement movement,MovementResult result,int nativeGeneration)
        {
            if(result.Status!=MovementStatus.Arrived || !ReferenceEquals(movement.Coordinator,traffic) || actor==null || result.ActorId!=actor.ActorId ||
                !movement.Coordinator.CompleteLeg(movement.Intent,actor,new TrafficNativeStamp(result.RequestId,nativeGeneration),result.FinalPosition))return false;
            movement.Index++;movement.NativeGeneration=0;return true;
        }
        internal bool SuspendMovement(NavigationActor actor,TrafficMovement movement,MovementResult result,int nativeGeneration,int actualRevision)
        {
            if(!ReferenceEquals(movement.Coordinator,traffic) || actor==null || result.ActorId!=actor.ActorId ||
                result.Status!=MovementStatus.LayoutChanged || result.Reason!=NavigationFailure.LayoutUnavailable ||
                movement.NativeGeneration!=nativeGeneration)return false;
            var stamp=new TrafficNativeStamp(result.RequestId,nativeGeneration);
            // A synchronous original callback can supply the real ID before MoveTo returns it.
            movement.Coordinator.BindLeg(movement.Intent,actor,stamp);
            if(!movement.Coordinator.SuspendLeg(movement.Intent,actor,stamp,result,actualRevision))return false;
            movement.Suspended=true;movement.NativeGeneration=0;return true;
        }
        internal void RetireMovement(TrafficMovement movement)
        { if(movement!=null)movement.Coordinator?.Retire(movement.Intent); }
        private bool MovementPreparationCurrent(NavigationActor actor,TrafficMovement movement)
        {
            var context=movement?.PreparationContext;
            return context!=null && context.BelongsTo(this,movement) && context.IsCurrent &&
                ReferenceEquals(context.ActorLife,actor) && ReferenceEquals(movement.Coordinator,traffic) &&
                movement.Intent.Session==trafficSession && movement.Intent.Epoch==trafficEpoch && adapter!=null &&
                movement.Intent.LayoutRevision==adapter.Revision && movement.Intent.Equals(context.Key) &&
                movement.PreparationFrame==context.FirstFrame && movement.PreparationOrigin==context.Origin;
        }
        private TrafficRequest EmployeeMovementRequest(TrafficMovement movement,TrafficWorldSnapshot snapshot)
        {
            var context=movement.PreparationContext;
            return new TrafficRequest(movement.Intent,movement.PreparationFrame,snapshot,context.Origin,context.Goal.Position,
                context.Radius,context.Resource,safetyMargin:context.SafetyMargin,planPrimaryRoute:true);
        }
        internal void RetireUnissuedMovement(TrafficMovement movement)
        {
            var coordinator=movement?.Coordinator;
            if(coordinator==null || movement.Source!=null || movement.PreparationContext==null || !coordinator.ContainsIntent(movement.Intent))return;
            var current=coordinator.GetDecision(movement.Intent);
            if(current.FailureDisposition==TrafficFailureDisposition.Terminal || current.AcquiredSequence!=0 ||
                coordinator.HasActiveGrant(movement.Intent) || coordinator.TryGetCommittedJourney(movement.Intent,out _))return;
            coordinator.Retire(movement.Intent);
        }
        internal TrafficDecision RefuseMovementPreparation(TrafficMovement movement,string reason)
        {
            RetireUnissuedMovement(movement);
            if(movement.Coordinator!=null && movement.Coordinator.ContainsIntent(movement.Intent))
            {
                var current=movement.Coordinator.GetDecision(movement.Intent);
                if(current.FailureDisposition==TrafficFailureDisposition.Terminal)return current;
            }
            return new TrafficDecision(movement.Intent,TrafficDecisionKind.StaleContext,reason,failureDisposition:TrafficFailureDisposition.Terminal);
        }
        internal void RefreshEmployeeMovementPreparation(NavigationActor actor,TrafficMovement movement,TrafficWorldSnapshot snapshot)
        {
            if(movement?.Source!=null || movement?.PendingSource==null || movement.PreparationContext==null ||
                !CanIntegratedProgress || snapshot==null || !ReferenceEquals(snapshot,trafficSnapshot) || !ReferenceEquals(movement.Coordinator,traffic))return;
            var expected=traffic.GetDecision(movement.Intent);
            if(expected.FailureDisposition!=TrafficFailureDisposition.None || expected.AcquiredSequence!=0 ||
                traffic.HasActiveGrant(movement.Intent) || traffic.TryGetCommittedJourney(movement.Intent,out _))return;
            if(!MovementPreparationCurrent(actor,movement)){RetireUnissuedMovement(movement);return;}
            if(!TrafficActorReady(actor) || expected.Kind!=TrafficDecisionKind.DeferredByBudget && expected.Kind!=TrafficDecisionKind.WaitingForTraffic)return;
            if(traffic.TryRefreshQueuedPreparation(expected,EmployeeMovementRequest(movement,snapshot),out var next))movement.PendingSource=next;
        }
        private int CustomerSourceFrame(Visit visit,TrafficIntentKey key)
        {
            if(visit.HasCollected && key.IntentId=="actual-exit")return visit.CollectionFrame;
            // Concurrent proposals keep their own original age for this Visit/context.
            // Counter前移与Pick-up候选交错轮询，不得互相重置phase起始帧。
            if(!visit.TrafficCandidateFrames.TryGetValue(key,out var frame))
            {frame=Time.frameCount;visit.TrafficCandidateFrames.Add(key,frame);}
            return frame;
        }
        private void PublishCustomerOwner(Visit visit,TrafficIntentKey key)
        {
            if(visit.PendingTraffic!=null)return;
            if(visit.TrafficJourney.HasValue && !visit.TrafficJourney.Value.Equals(key) &&
                !(visit.PaidPickup!=null && visit.PaidPickup.ActiveIntent.Equals(key) && !visit.PaidPickup.Parking.Retired &&
                    visit.TrafficJourney.Value.Equals(visit.PaidPickup.Parking.Source.Intent)))RetireTrafficCandidate(visit.TrafficJourney.Value);
            trafficActorKeys[visit.Id]=key;visit.TrafficJourney=key;
        }
        private TrafficSelection CaptureTrafficSelection(Visit visit,TrafficDecision source,CustomerVisitState phase)
        {
            var selection=new TrafficSelection{Intent=source.Intent,Source=source,Phase=phase,Assignment=visit.Assignment,
                YieldPhase=visit.DeliveryYieldPhase,FirstFrame=CustomerSourceFrame(visit,source.Intent),Counter=queue,Pickup=visit.PickupOwner};
            selection.Counter?.TryCaptureAssignment(visit.Id,out selection.CounterEntry);
            selection.Pickup?.TryCaptureReservation(visit.Id,out selection.PickupEntry);
            return selection;
        }
        private void SelectCustomerTraffic(Visit visit,TrafficDecision source,TrafficRouteProof proof,CustomerVisitState phase,NavigationTarget target)
        {
            visit.TrafficSelection=CaptureTrafficSelection(visit,source,phase);
            visit.TrafficMove=new TrafficMovement{Coordinator=traffic,Intent=source.Intent,Source=source,Resource=proof.ResourceId,Goal=target};
            PublishCustomerOwner(visit,source.Intent);
        }
        private static bool SameAssignment(QueueAssignment a,QueueAssignment b)=>a.VisitId==b.VisitId && a.Generation==b.Generation &&
            a.SlotIndex==b.SlotIndex && a.Position==b.Position;
        private bool TrafficSelectionCurrent(Visit visit,TrafficSelection selected)
        {
            if(selected==null || visit.Actor==null || !visit.Actor.isActiveAndEnabled || visit.State!=selected.Phase ||
                visit.TrafficMove==null || !ReferenceEquals(visit.TrafficMove.Coordinator,traffic) ||
                !visit.TrafficMove.Intent.Equals(selected.Intent) || !ReferenceEquals(visit.TrafficMove.Source,selected.Source) ||
                selected.Source==null || !selected.Source.Intent.Equals(selected.Intent) || selected.Intent.Session!=trafficSession ||
                selected.Intent.Epoch!=trafficEpoch || selected.Intent.LayoutRevision!=adapter.Revision)return false;
            if(selected.Phase==CustomerVisitState.Exiting)
                return visit.HasCollected && visit.CollectedOrder.HasValue && visit.CollectionFrame>=0 && visit.ExitSequence>0 &&
                    selected.FirstFrame==visit.CollectionFrame && selected.Intent.Context==CollectionTrafficContext(visit);
            if(!SameAssignment(visit.Assignment,selected.Assignment))return false;
            if(selected.Phase==CustomerVisitState.Entering || selected.Phase==CustomerVisitState.Advancing)
                return !visit.QueueLeft && ReferenceEquals(queue,selected.Counter) && selected.CounterEntry?.IsCurrent==true;
            if(selected.Phase==CustomerVisitState.MovingToPickup && (visit.PaidPickup==null || !visit.PaidPickup.Accepted ||
                !visit.PaidPickup.Transferred || visit.PaidPickup.Failed || !visit.PaidPickup.ActiveIntent.Equals(selected.Intent) ||
                visit.PaidPickup.FirstFrame!=selected.FirstFrame))return false;
            return IsPickupMovement(selected.Phase) && visit.PickupInTransit && visit.PickupAssigned &&
                ReferenceEquals(visit.PickupOwner,selected.Pickup) && selected.PickupEntry?.IsCurrent==true &&
                selected.Pickup.Snapshot.Any(a=>SameAssignment(a,selected.Assignment)) && visit.DeliveryYieldPhase==selected.YieldPhase &&
                (selected.Phase!=CustomerVisitState.MovingToPickupService || selected.PickupEntry.IsCurrentCollectionLease);
        }
        private bool TrafficReceiptCurrent(TrafficPhaseReceipt receipt)=>!disposed && isActiveAndEnabled &&
            ReferenceEquals(traffic,receipt.Coordinator) && receipt.Intent.Session==trafficSession && receipt.Intent.Epoch==trafficEpoch &&
            adapter!=null && receipt.Intent.LayoutRevision==adapter.Revision && receipt.Actor!=null && receipt.Actor.isActiveAndEnabled &&
            visits.TryGetValue(receipt.Visit.Id,out var current) && ReferenceEquals(current,receipt.Visit) &&
            ReferenceEquals(current.Actor,receipt.Actor) && ReferenceEquals(current.Admission,receipt.Admission) && Capacity!=null &&
            Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,receipt.Total) && r.OwnerId==current.Id && r.State==ReservationState.Occupied);
        private TrafficClaimBinding CaptureTrafficClaim(Visit visit,TrafficParkingFact fact,TrafficSelection selected=null)
        {
            if(fact==null || !ReferenceEquals(fact.BusinessOwner,visit) || !ReferenceEquals(fact.ReleaseObligation,visit.Admission))return null;
            var binding=new TrafficClaimBinding{Fact=fact};
            if(fact.ResourceId=="counter/"+visit.Id && fact.CanonicalOwner is CounterQueueService counter)
            {
                binding.Kind=TrafficClaimKind.Counter;binding.Counter=counter;
                if(selected!=null)
                {if(!ReferenceEquals(selected.Counter,counter))return null;binding.CounterEntry=selected.CounterEntry;}
                else if(!ReferenceEquals(queue,counter) || !counter.TryCaptureAssignment(visit.Id,out binding.CounterEntry))return null;
                if(binding.CounterEntry==null)return null;
            }
            else if((fact.ResourceId=="pickup/"+visit.Id || fact.ResourceId=="service/"+visit.Id) && fact.CanonicalOwner is PickupQueueService pickup)
            {
                binding.Kind=fact.ResourceId=="service/"+visit.Id?TrafficClaimKind.Service:TrafficClaimKind.Pickup;binding.Pickup=pickup;
                if(selected!=null)
                {if(!ReferenceEquals(selected.Pickup,pickup))return null;binding.PickupEntry=selected.PickupEntry;}
                else if(!ReferenceEquals(visit.PickupOwner,pickup) || !pickup.TryCaptureReservation(visit.Id,out binding.PickupEntry))return null;
                if(binding.PickupEntry==null || binding.Kind==TrafficClaimKind.Service && !binding.PickupEntry.IsCurrentCollectionLease)return null;
            }
            else return null;
            var kind=binding.Kind==TrafficClaimKind.Counter?CapacityKind.CounterQueue:CapacityKind.PickUp;
            binding.Token=Capacity?.GetReservations().SingleOrDefault(r=>r.OwnerId==visit.Id && r.Kind==kind)?.Token;
            return binding.Token==null?null:binding;
        }
        private TrafficPhaseClaimState ReadBoundTrafficClaim(Visit visit,TrafficClaimBinding binding,out TrafficParkingFact current)
        {
            current=null;if(binding==null)return TrafficPhaseClaimState.Invalid;
            var fact=binding.Fact;Vector3 position;long version;ITrafficClaimCheckpoint checkpoint=null;
            bool Released()=>Capacity!=null && Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,binding.Token) && r.State==ReservationState.Released);
            if(binding.Kind==TrafficClaimKind.Counter)
            {
                if(!binding.Counter.TryCaptureAssignment(visit.Id,out var claim))
                    return visit.QueueLeft && Released()?TrafficPhaseClaimState.Released:TrafficPhaseClaimState.Invalid;
                if(!binding.CounterEntry.IsSameReservation(claim) || !claim.IsCurrent)return TrafficPhaseClaimState.Invalid;
                position=claim.Assignment.Position;version=binding.Counter.Version;checkpoint=claim;
            }
            else
            {
                if(binding.Kind==TrafficClaimKind.Service)
                {
                    if(!binding.Pickup.TryGetServiceLease(visit.Id,out position))
                        return visit.PickupDeparted && Released()?TrafficPhaseClaimState.Released:TrafficPhaseClaimState.Invalid;
                    // Remove alone does not release a service body; an invalid old lease is not Released.
                    if(!binding.PickupEntry.IsCurrentCollectionLease)return TrafficPhaseClaimState.Invalid;
                }
                if(!binding.Pickup.TryCaptureReservation(visit.Id,out var claim))
                    return binding.Kind==TrafficClaimKind.Pickup && visit.PickupDeparted && Released()?TrafficPhaseClaimState.Released:TrafficPhaseClaimState.Invalid;
                if(!binding.PickupEntry.IsSameReservation(claim) || !claim.IsCurrent)return TrafficPhaseClaimState.Invalid;
                position=claim.Assignment.Position;version=binding.Pickup.Version;
                if(binding.Kind==TrafficClaimKind.Pickup)checkpoint=claim; // A reservation is not a service-lease checkpoint.
            }
            current=new TrafficParkingFact(fact.Owner,fact.CanonicalOwner,version,fact.ResourceId,position,fact.Radius,fact.SafetyMargin,visit,visit.Admission,semanticCheckpoint:checkpoint);
            return TrafficPhaseClaimState.Held;
        }
        private TrafficPhaseClaimState ReadTrafficClaim(TrafficPhaseReceipt receipt,TrafficParkingFact captured,out TrafficParkingFact current)
        {
            current=null;if(captured==null || !TrafficReceiptCurrent(receipt))return TrafficPhaseClaimState.Invalid;
            var binding=receipt.Claims.FirstOrDefault(c=>ReferenceEquals(c.Fact,captured));
            if(binding==null)
            {
                if(receipt.Claims.Any(c=>c.Fact.ResourceId==captured.ResourceId) || !TrafficSelectionCurrent(receipt.Visit,receipt.Visit.TrafficSelection))
                    return TrafficPhaseClaimState.Invalid;
                binding=CaptureTrafficClaim(receipt.Visit,captured,receipt.Visit.TrafficSelection);
            }
            return ReadBoundTrafficClaim(receipt.Visit,binding,out current);
        }
        private bool CaptureIssuedTraffic(Visit visit,TrafficMovement move)
        {
            if(!move.Coordinator.TryGetCommittedLegProvenance(move.Intent,visit.Actor,move.NativeGeneration,out var source,out var saved) ||
                !ReferenceEquals(source,move.Source))return false;
            var bindings=new List<TrafficClaimBinding>();
            foreach(var fact in saved.Parking.Where(p=>p.Owner.Equals(move.Intent)))
            {
                var binding=CaptureTrafficClaim(visit,fact);
                if(ReadBoundTrafficClaim(visit,binding,out var actual)!=TrafficPhaseClaimState.Held || actual.OwnerVersion!=fact.OwnerVersion ||
                    actual.Position!=fact.Position)return false;
                bindings.Add(binding);
            }
            visit.IssuedTraffic=new TrafficPhaseReceipt{Flow=this,Visit=visit,Admission=visit.Admission,Actor=visit.Actor,
                Coordinator=move.Coordinator,Intent=move.Intent,Source=source,SavedSnapshot=saved,Claims=bindings.ToArray(),
                NativeGeneration=move.NativeGeneration,Total=Capacity.GetReservations().Single(r=>r.OwnerId==visit.Id && r.Kind==CapacityKind.TotalCustomers).Token};
            return true;
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ObserveCustomerHandoffRefusal(Visit visit,TrafficMovement move,TrafficDecision decision,TrafficPhaseReceipt pending,string branch)
        {
#if UNITY_EDITOR
            if(CorridorObserver==null)return;
            try
            {
                string Ref(object value)=>value==null?"NULL":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value).ToString();
                CorridorObserver("traffic.customer-handoff-refused",visit.Id,decision.Intent.IntentId,move.Target.Position,null,null,
                    "branch="+branch+" originalGeneration="+decision.Intent.Generation+" requestSequence="+decision.RequestSequence+
                    " decisionRef="+Ref(decision)+" pendingRef="+Ref(pending)+" handleRef="+Ref(pending.Handle)+" moveRef="+Ref(move));
            }
            catch(Exception){ } // Diagnostics cannot change the already observed original false branch.
#endif
        }
        private bool CommitCustomerMovement(Visit visit,TrafficMovement move,TrafficDecision decision)
        {
            var pending=visit.PendingTraffic;
            if(pending==null)
            {
                if(!CommitMovement(move,decision))return false;
            }
            else
            {
                if(!ReferenceEquals(pending.Coordinator,traffic) || pending.Handle==null)
                {ObserveCustomerHandoffRefusal(visit,move,decision,pending,"header-guard");return false;}
                var handoffCommitted=traffic.TryCommitPhaseHandoff(pending.Handle,decision,pending,publish:()=>
                {
                    trafficCandidates.Remove(pending.Intent);
                    visit.PendingTraffic=null;trafficActorKeys[visit.Id]=move.Intent;visit.TrafficJourney=move.Intent;
                    RefreshTrafficSnapshot(); // Actual owner labels only; no query or canonical mutation here.
                });
                if(!handoffCommitted)
                {ObserveCustomerHandoffRefusal(visit,move,decision,pending,"TryCommitPhaseHandoff-false");return false;}
            }
            var paid=visit.PaidPickup;
            if(paid!=null && paid.Transferred && paid.ActiveIntent.Equals(move.Intent) && !paid.Parking.Retired)
            {
                // The replacement STRICT leg now protects the same canonical target; do not release its entry/token.
                RetireTrafficCandidate(paid.Parking.Source.Intent);paid.Parking.Retired=true;
            }
            move.Suspended=false;return true;
        }
        private bool MarkTrafficArrival(Visit visit,CustomerVisitState phase)
        {
            if(integratedMode && IsPickupMovement(phase))
            {
                if(visit.DeliveryYieldPhase==1)
                {
                    if(!visit.PickupOwner.MarkYieldArrived(visit.Id,visit.Assignment.Generation))return false;
                    visit.DeliveryYieldPhase=2;visit.State=CustomerVisitState.PickupWaiting;return true;
                }
                if(!visit.PickupOwner.MarkArrived(visit.Id,visit.Assignment.Generation) || !visit.Admission.MarkPickupWaiting())return false;
                visit.PickupArrived=true;visit.DeliveryYieldPhase=0;visit.ServiceArrived=phase==CustomerVisitState.MovingToPickupService;
                if(phase==CustomerVisitState.MovingToPickup && visit.PaidPickup!=null)DiscardPaidRecovery(visit);
                visit.State=visit.ServiceArrived?CustomerVisitState.PickupService:CustomerVisitState.PickupWaiting;return true;
            }
            if(queue==null || !queue.Complete(visit.Id,visit.Assignment.Generation) || !visit.Admission.MarkQueued())return false;
            visit.State=CustomerVisitState.Queued;visit.CounterArrived=true;visit.CounterPosition=visit.Actor.transform.position;return true;
        }
        private bool CompleteTrafficPhase(Visit visit,TrafficMovement move,TrafficPhaseReceipt receipt,CustomerVisitState phase,MovementResult result,int generation)
        {
            if(receipt==null || receipt.NativeGeneration!=generation || !ReferenceEquals(receipt.Source,move.Source) ||
                !receipt.Intent.Equals(move.Intent) || !TrafficReceiptCurrent(receipt))return false;
            var stamp=new TrafficNativeStamp(result.RequestId,generation);
            move.Coordinator.BindLeg(move.Intent,visit.Actor,stamp); // Original synchronous callback may precede MoveTo's returned ID.
            if(!move.Coordinator.CompleteLegAndBeginHandoff(move.Intent,visit.Actor,stamp,result,()=>
            {
                if(!MarkTrafficArrival(visit,phase))return null;
                receipt.Marked=true;return receipt;
            },out var handle))return false;
            receipt.Handle=handle;visit.PendingTraffic=handle==null?null:receipt;visit.RequestId=0;
            move.Index++;move.NativeGeneration=0;return true;
        }
        private void AbortCustomerHandoff(Visit visit,string reason)
        {
            var pending=visit.PendingTraffic;visit.PendingTraffic=null;
            if(pending?.Handle!=null)pending.Coordinator.AbortPhaseHandoff(pending.Handle,reason);
        }
        private Vector3[] TrafficObstacles(string ignore)
            => trafficSnapshot.Parking.Where(p=>p.Owner.ActorId!=ignore).Select(p=>p.Position)
                .Concat(trafficSnapshot.Bodies.Where(b=>b.Owner.ActorId!=ignore && !b.IndependentlyProgressing).Select(b=>b.Position)).Distinct().ToArray();

        // Birth has callbacks before the joint commit, never between canonical writes and traffic activation.
        // Register本身不发布managed事件；失败后的Unregister/Destroy留在TryCommit外处理。
        private sealed class CounterBirthMutation : IPreparedTrafficMutation,ITrafficMutationReceipt
        {
            private readonly CustomerFlowController flow;
            private readonly CounterQueueService.PreparedMutation prepared;
            private readonly NavigationWorld world;
            private readonly CapacityService capacity;
            private readonly NavigationActor actor;
            private readonly Func<bool> current;
            private readonly long next;
            private CustomerAdmission lease;
            private ITrafficMutationReceipt counterReceipt;
            private bool rolledBack;
            internal Visit AddedVisit;
            internal CounterBirthMutation(CustomerFlowController flow,CounterQueueService.PreparedMutation prepared,
                NavigationActor actor,Func<bool> current,long next)
            {this.flow=flow;this.prepared=prepared;this.actor=actor;this.current=current;this.next=next;world=flow.world;capacity=flow.Capacity;}
            public bool IsCurrent => !rolledBack && current() && prepared.IsCurrent;
            bool ITrafficMutationReceipt.IsCurrent => !rolledBack && (counterReceipt==null || counterReceipt.IsCurrent) &&
                (AddedVisit==null || flow.visits.TryGetValue(AddedVisit.Id,out var live) && ReferenceEquals(live,AddedVisit));
            public bool TryApply(out ITrafficMutationReceipt receipt)
            {
                receipt=this;if(!IsCurrent)return false;
                if(!world.Register(actor) || !CustomerAdmission.TryCreate(capacity,actor.ActorId,out lease) || !lease.MarkInside() ||
                    !prepared.TryApply(out counterReceipt))return false;
                AddedVisit=new Visit{Id=actor.ActorId,Actor=actor,Admission=lease,Assignment=prepared.Assignment,
                    State=CustomerVisitState.Entering,ResumeState=CustomerVisitState.Entering};
#if UNITY_EDITOR
                AddedVisit.AdmissionObservation=flow.admissionObservation;
#endif
                flow.visits.Add(AddedVisit.Id,AddedVisit);flow.nextVisit=next;return true;
            }
            public void RollbackOwnMutation()
            {
                if(rolledBack)return;
                if(!((ITrafficMutationReceipt)this).IsCurrent)throw new InvalidOperationException("Cannot roll back another admission.");
                rolledBack=true;
                if(AddedVisit!=null)flow.visits.Remove(AddedVisit.Id);
                counterReceipt?.RollbackOwnMutation();lease?.Dispose();
                // No external callbacks here, and no rewind of published visit/token identities.
            }
        }
        private bool TrafficCounterVisitCurrent(Visit visit,ServiceTrafficCoordinator coordinator,TrafficIntentKey key,int generation)
            => ReferenceEquals(traffic,coordinator) && trafficEpoch==key.Epoch && adapter!=null && adapter.Revision==key.LayoutRevision &&
                CanIntegratedProgress && visit.Actor!=null && visit.Actor.isActiveAndEnabled && visit.Generation==generation &&
                visits.TryGetValue(visit.Id,out var live) && ReferenceEquals(live,visit);
        private void PublishTrafficCounterRoute(Visit visit,TrafficDecision decision,TrafficRouteProof proof,CustomerVisitState phase,bool admission)
        {
            var coordinator=traffic;var key=decision.Intent;var generation=visit.Generation;
            var counter=queue;var counterVersion=counter.Version;var assignment=visit.Assignment;
            var actor=visit.Actor;var admissionOwner=visit.Admission;var capacity=Capacity;
            var origin=actor.transform.position;var radius=actor.Settings.AgentRadius;
            var skin=actor.Settings.CollisionSkin;var epsilon=actor.Settings.Epsilon;
            var total=capacity?.GetReservations().SingleOrDefault(r=>r.OwnerId==visit.Id &&
                r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied)?.Token;
            if(!counter.TryCaptureAssignment(visit.Id,out var claim) || claim.Assignment.VisitId!=assignment.VisitId ||
                claim.Assignment.Generation!=assignment.Generation || claim.Assignment.SlotIndex!=assignment.SlotIndex || claim.Assignment.Position!=assignment.Position)
            {coordinator.Retire(key);return;}
            PublishCustomerOwner(visit,key);
            bool Owned()=>TrafficCounterVisitCurrent(visit,coordinator,key,generation) && ReferenceEquals(queue,counter) && claim.IsCurrent;
            bool Current()=>Owned() && counter.Version==counterVersion;
            void RetireInvalid()
            {
                if(Owned())
                {
                    // The same committed assignment survives unrelated tail metadata; do not orphan its phase.
                    // 只保存尚未发腿的原任务，正常ready batch重验；不新建Intent或重置预算。
                    visit.PendingCounter=new PendingCounterMove{Coordinator=coordinator,Counter=counter,Claim=claim,Intent=key,Phase=phase,Generation=generation,
                        Source=decision,Proof=proof,Actor=actor,Admission=admissionOwner,Capacity=capacity,Total=total,
                        Origin=origin,Radius=radius,Skin=skin,Epsilon=epsilon};
                    visit.State=visit.ResumeState=phase;
                }
                else
                {
                    if(visit.PendingCounter!=null && visit.PendingCounter.Intent.Equals(key))visit.PendingCounter=null;
                    coordinator.Retire(key);
                }
            }
            if(admission)
            {
                ObserveCorridor("admission.published",visit,target:visit.Assignment.Position);
                if(!Current()){RetireInvalid();return;}
                ObserveAdmissionDecision("admission.original-published",target:visit.Assignment.Position,incoming:visit);
                if(!Current()){RetireInvalid();return;}
            }
            ClearOwnedRoute(visit);
            if(!Current()){RetireInvalid();return;}
            if(phase==CustomerVisitState.Advancing)visit.AdvanceStart=visit.Actor.transform.position;
            visit.Generation++;visit.RequestId=0;visit.State=visit.ResumeState=phase;
            visit.FinalTarget=QueueTarget(visit.Assignment);visit.Segments=visit.Replans=0;visit.Failure=null;
            visit.PendingExitSegment=visit.PendingPickupSegment=false;visit.PickupPriorityCheckedRoute=null;visit.YieldReturnTraffic=null;
            visit.Route=new Queue<Vector3>(proof.Waypoints);
            visit.TrafficMove=null;visit.TrafficMovePending=false;
            SelectCustomerTraffic(visit,decision,proof,phase,visit.FinalTarget);
            SendRouteSegment(visit,phase); // Existing Native endpoint/sweep checks remain; full movement migration is Task4C.
        }
        private void ResumeTrafficCounterMove(Visit visit)
        {
            var pending=visit.PendingCounter;if(pending==null)return;
            bool Current()
            {
                if(!ReferenceEquals(visit.PendingCounter,pending) || !ReferenceEquals(queue,pending.Counter) || pending.Claim==null || !pending.Claim.IsCurrent ||
                    !TrafficCounterVisitCurrent(visit,pending.Coordinator,pending.Intent,pending.Generation) ||
                    visit.State!=pending.Phase || visit.RequestId!=0 || !ReferenceEquals(visit.Actor,pending.Actor) ||
                    !ReferenceEquals(visit.Admission,pending.Admission) || !ReferenceEquals(Capacity,pending.Capacity) || pending.Total==null ||
                    pending.Actor.transform.position!=pending.Origin || pending.Actor.Settings.AgentRadius!=pending.Radius ||
                    pending.Actor.Settings.CollisionSkin!=pending.Skin || pending.Actor.Settings.Epsilon!=pending.Epsilon ||
                    !pending.Counter.TryCaptureAssignment(visit.Id,out var currentClaim) || !pending.Claim.IsSameCurrentClaim(currentClaim) ||
                    !SameAssignment(visit.Assignment,pending.Claim.Assignment) ||
                    !pending.Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,pending.Total) && r.OwnerId==visit.Id &&
                        r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied))return false;
                var source=pending.Source;var proof=pending.Proof;
                return source!=null && source.Kind==TrafficDecisionKind.Granted && source.AcquiredSequence>0 && source.Intent.Equals(pending.Intent) &&
                    pending.Coordinator.HasActiveGrant(pending.Intent) && ReferenceEquals(pending.Coordinator.GetDecision(pending.Intent),source) &&
                    source.FailureDisposition==TrafficFailureDisposition.None && pending.Coordinator.TryGetCommittedJourney(pending.Intent,out var retained) &&
                    ReferenceEquals(retained,source) && proof!=null && source.ProofRoutes.Any(p=>ReferenceEquals(p,proof)) &&
                    proof.ResourceId=="counter/"+visit.Id && proof.Waypoints.Count>0 && proof.Waypoints.Count<=24 &&
                    proof.Waypoints.Last()==pending.Claim.Assignment.Position;
            }
            if(!Current())
            {
                visit.PendingCounter=null;pending.Coordinator.Retire(pending.Intent);
                if(visits.TryGetValue(visit.Id,out var live) && ReferenceEquals(live,visit) && visit.Generation==pending.Generation &&
                    visit.State==pending.Phase && visit.RequestId==0 && CanIntegratedProgress && ReferenceEquals(traffic,pending.Coordinator) &&
                    trafficEpoch==pending.Intent.Epoch && adapter.Revision==pending.Intent.LayoutRevision)
                    BlockVisit(visit,pending.Phase,"Counter assignment ownership/context changed");
                return;
            }
            RefreshTrafficSnapshot();if(!Current())return;
            // The canonical Join/Advance already committed before publication reentry.
            // 不再Submit/TryCommit；原source经正常strict-leg和当前snapshot重新验证。
            visit.PendingCounter=null;
            PublishTrafficCounterRoute(visit,pending.Source,pending.Proof,pending.Phase,false);
        }
        // Typed binding is necessary, but caller life/cursor provenance remains a separate requirement.
        // sealed preview必须属于原Queue/visit并仍有效；目标相等本身不授予出生或移动权限。
        private static bool PrimaryMatchesCounterAdmissionPreview(CounterQueueService owner,string visitId,
            CounterQueueService.PreparedMutation preview,TrafficRouteProof primary)
            =>owner!=null && preview!=null && preview.Kind==CounterQueueService.MutationKind.Join && preview.Id==visitId &&
                preview.Assignment.VisitId==visitId && preview.BelongsTo(owner) && preview.IsCurrent && primary!=null &&
                primary.ResourceId=="counter/"+visitId && primary.Waypoints.Count>0 && primary.Waypoints.Count<=24 &&
                primary.Waypoints.Last()==preview.Assignment.Position;
        private static bool PrimaryMatchesPickupReservationPreview(PickupQueueService owner,string visitId,
            PickupQueueService.PreparedMutation preview,TrafficRouteProof primary)
            =>owner!=null && preview!=null && preview.Kind==PickupQueueService.MutationKind.Reserve && preview.Id==visitId &&
                preview.Assignment.VisitId==visitId && preview.BelongsTo(owner) && preview.IsCurrent && primary!=null &&
                primary.ResourceId=="pickup/"+visitId && primary.Waypoints.Count>0 && primary.Waypoints.Count<=24 &&
                primary.Waypoints.Last()==preview.Assignment.Position;
        private bool AnchorContextCurrent(ServiceTrafficCoordinator coordinator,TrafficIntentKey key)
            =>ReferenceEquals(traffic,coordinator) && key.Session==trafficSession && key.Epoch==trafficEpoch &&
                adapter!=null && key.LayoutRevision==adapter.Revision && CanIntegratedProgress;
        private bool AdmissionAnchorCurrent(CounterAdmissionAnchor a)
            =>a!=null && ReferenceEquals(pendingAdmissionAnchor,a) && AnchorContextCurrent(a.Coordinator,a.Key) &&
                session==a.Session && nextVisit==a.Before && ReferenceEquals(queue,a.Owner) && ReferenceEquals(Capacity,a.Capacity) &&
                ReferenceEquals(world,a.World) && ReferenceEquals(spawn,a.Spawn) && spawn.position==a.Position && spawn.rotation==a.Rotation &&
                (!a.Automatic || autoSpawn) && integratedAdmissionAllowed && a.Capacity.CanAdmit && prefabs.Length>0 &&
                ReferenceEquals(prefabs[(int)(a.Before%prefabs.Length)],a.Prefab) && a.Prefab.Settings.AgentRadius==a.Radius &&
                a.Prefab.Settings.CollisionSkin==a.Skin && a.Prefab.Settings.Epsilon==a.Epsilon && !visits.ContainsKey(a.Key.ActorId) &&
                !world.RegisteredActors.Any(actor=>actor!=null && actor.ActorId==a.Key.ActorId) &&
                !a.Capacity.GetReservations().Any(r=>r.OwnerId==a.Key.ActorId) &&
                (trafficSnapshot==null || !trafficSnapshot.Bodies.Any(b=>b.Owner.ActorId==a.Key.ActorId) &&
                    !trafficSnapshot.Parking.Any(p=>p.Owner.ActorId==a.Key.ActorId));
        private bool ReservationAnchorCurrent(PickupReservationAnchor a)
        {
            if(a==null || !AnchorContextCurrent(a.Coordinator,a.Key) || !ReferenceEquals(world,a.World) ||
                !ReferenceEquals(pickupQueue,a.Owner) || !ReferenceEquals(queue,a.Counter) || !ReferenceEquals(Capacity,a.Capacity) ||
                a.Actor==null || !a.Actor.isActiveAndEnabled || a.Actor.Agent==null || !a.Actor.Agent.enabled || !a.Actor.Agent.isOnNavMesh ||
                a.Actor.transform.position!=a.Start || !world.RegisteredActors.Any(actor=>ReferenceEquals(actor,a.Actor)) ||
                !visits.TryGetValue(a.Key.ActorId,out var visit) || !ReferenceEquals(visit,a.Visit) || !ReferenceEquals(visit.PendingReservationAnchor,a) ||
                !ReferenceEquals(visit.Actor,a.Actor) || !ReferenceEquals(visit.Admission,a.Admission) || visit.Generation!=a.Generation ||
                visit.TrafficPhaseSequence!=a.Cursor || visit.State!=a.Phase || visit.State!=CustomerVisitState.Queued || !visit.CounterArrived ||
                visit.PickupAssigned || visit.HasCollected || a.CounterClaim?.IsCurrent!=true || a.Counter.Snapshot.Count==0 ||
                a.Counter.Snapshot[0].VisitId!=visit.Id || a.Counter.Snapshot[0].SlotIndex!=0 || !a.Counter.Snapshot[0].Arrived ||
                a.SideIndex<0 || a.SideIndex>=pickupSides.Length || !pickupSides[a.SideIndex].Equals(a.Side) ||
                !trafficCandidates.TryGetValue(a.Key,out var cursor) || cursor.Role!=TrafficCandidateRole.Reservation ||
                !ReferenceEquals(cursor.ActorLife,a.Actor) || (cursor.CurrentIndex>=0 && cursor.CurrentIndex!=a.SideIndex) ||
                !cursor.Sides[a.SideIndex].Equals(a.Side) ||
                deliveryActorId!=a.EmployeeId || deliveryWorkTarget.Position!=a.Work || exit.position!=a.Exit)return false;
            var employee=world.RegisteredActors.FirstOrDefault(actor=>actor!=null && actor.ActorId==a.EmployeeId);
            if(!ReferenceEquals(employee,a.Employee))return false;
            var tokens=a.Capacity.GetReservations().Where(r=>r.OwnerId==visit.Id).ToArray();
            return a.Tokens.Length>0 && tokens.Length==a.Tokens.Length && a.Tokens.All(old=>old.State!=ReservationState.Released &&
                tokens.Any(current=>ReferenceEquals(current.Token,old.Token) && current.Kind==old.Kind && current.State==old.State));
        }
        // Identity only: Pause/navigation/admission-policy readiness never replaces the original opportunity.
        private bool AdmissionReadinessCurrent(CounterAdmissionAnchor a)
            =>a!=null && !disposed && isActiveAndEnabled && ReferenceEquals(pendingAdmissionAnchor,a) && ReferenceEquals(traffic,a.Coordinator) &&
                a.Key.Session==trafficSession && a.Key.Epoch==trafficEpoch && adapter!=null && a.Key.LayoutRevision==adapter.Revision &&
                session==a.Session && nextVisit==a.Before && ReferenceEquals(queue,a.Owner) && ReferenceEquals(Capacity,a.Capacity) &&
                ReferenceEquals(world,a.World) && ReferenceEquals(spawn,a.Spawn) && spawn.position==a.Position && spawn.rotation==a.Rotation &&
                prefabs.Length>0 && ReferenceEquals(prefabs[(int)(a.Before%prefabs.Length)],a.Prefab) &&
                a.Prefab.Settings.AgentRadius==a.Radius && a.Prefab.Settings.CollisionSkin==a.Skin && a.Prefab.Settings.Epsilon==a.Epsilon &&
                !visits.ContainsKey(a.Key.ActorId) && !a.Owner.Snapshot.Any(row=>row.VisitId==a.Key.ActorId) &&
                !world.RegisteredActors.Any(actor=>actor!=null && actor.ActorId==a.Key.ActorId) &&
                !a.Capacity.GetReservations().Any(row=>row.OwnerId==a.Key.ActorId);
        private IReadOnlyList<TrafficIntentKey> PendingCollectedExitKeys()
        {
            var keys=new List<TrafficIntentKey>();
            foreach(var visit in visits.Values)
            {
                if(!visit.HasCollected || visit.State!=CustomerVisitState.Exiting || !visit.CollectedOrder.HasValue || visit.CollectionFrame<0 ||
                    visit.Actor==null || !visit.Actor.isActiveAndEnabled || visit.Admission==null || exit==null || visit.FinalTarget.Position!=exit.position ||
                    !world.RegisteredActors.Any(actor=>ReferenceEquals(actor,visit.Actor)) ||
                    !Capacity.GetReservations().Any(row=>row.OwnerId==visit.Id && row.Kind==CapacityKind.TotalCustomers && row.State==ReservationState.Occupied))continue;
                var memo=visit.Id+"/actual-exit/"+CollectionTrafficContext(visit);
                if(trafficKeys.TryGetValue(memo,out var key) && traffic.ContainsIntent(key))keys.Add(key);
            }
            return keys;
        }
        private bool PrepareAdmissionAnchor(CounterAdmissionAnchor a,TrafficWorldSnapshot snapshot,out TrafficDecision decision)
        {
            decision=null;
            if(!AdmissionAnchorCurrent(a) || !a.Owner.TryPrepareMutation(CounterQueueService.MutationKind.Join,a.Key.ActorId,a.Owner.Version,out var preview) ||
                preview.Kind!=CounterQueueService.MutationKind.Join || preview.Id!=a.Key.ActorId || preview.Assignment.VisitId!=a.Key.ActorId ||
                !preview.BelongsTo(a.Owner) || !preview.IsCurrent)return false;
            var request=new TrafficRequest(a.Key,a.FirstFrame,snapshot,a.Position,preview.Assignment.Position,a.Radius,"counter/"+a.Key.ActorId,
                requirements:PreservePickupGoals(null),safetyMargin:a.Safety,planPrimaryRoute:true);
#if UNITY_EDITOR
            if(!a.Submitted){try{AdmissionPreparationObserver?.Invoke(request);}catch(Exception){ }}
#endif
            if(!AdmissionAnchorCurrent(a))return false;
            // Unborn admission has no paid owner or Core budget yet; preserve the same original opportunity while occupied.
            // 只延后尚未提交的原机会；已有任何 Core 来源（包括 Terminal）保持原合同。
            if(!a.Submitted && !a.Coordinator.ContainsIntent(a.Key) && !a.Coordinator.IsParkingEndpointAvailable(request))return false;
            if(!a.Submitted){decision=a.Coordinator.EvaluateParking(request);a.Submitted=true;}
            else if(!a.Coordinator.TryReprepareCanonicalAnchor(a.Coordinator.GetDecision(a.Key),request,out decision))return false;
            if(a.Readiness==null)a.Readiness=new TrafficAdmissionReadiness(a.Key,()=>AdmissionReadinessCurrent(a),PendingCollectedExitKeys);
            if(!a.Coordinator.TrySetAdmissionReadiness(decision,a.Readiness))return false;
            a.Preview=preview;return AdmissionAnchorCurrent(a) && preview.IsCurrent;
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ObserveReservationRefusal(string stage,Visit visit=null,PickupReservationAnchor anchor=null,
            TrafficCandidateCursor cursor=null,TrafficDecision decision=null,bool hasDecisionLocal=false,int sideIndex=-1,TrafficIntentKey? key=null)
        {
#if UNITY_EDITOR
            if(CorridorObserver==null)return;
            try
            {
                string Ref(object value)=>value==null?"NULL":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value).ToString();
                string Key(TrafficIntentKey? value)=>!value.HasValue?"NOT_CAPTURED":value.Value.Session+"/e"+value.Value.Epoch+"/"+
                    value.Value.ActorId+"/"+value.Value.IntentId+"/g"+value.Value.Generation+"/r"+value.Value.LayoutRevision+"/"+value.Value.Context;
                string Decision(TrafficDecision value,bool captured)=>!captured?"NOT_EVALUATED":value==null?"NULL":Ref(value)+"/"+value.Kind+"/"+value.Reason+"/"+value.FailureDisposition;
                var a=anchor??visit?.PendingReservationAnchor;var originalKey=key??a?.Key??cursor?.Key;var preview=a?.Preview;
                // Stored locals only; no guard replay, frame counter or new preview identity.
                // 只读旧字段；不调用GetDecision/IsCurrent，不影响原false。
                ObserveCorridor("reserve.original-refusal",visit,target:a==null?default:a.Side.Position,
                    result:"stage="+stage+" key="+Key(originalKey)+" sideIndex="+sideIndex+" anchor="+Ref(a)+
                    " anchorSubmitted="+(a==null?"NOT_CAPTURED":a.Submitted.ToString())+" anchorSide="+(a==null?"NOT_CAPTURED":a.SideIndex.ToString())+
                    " pendingAnchor="+Ref(visit?.PendingReservationAnchor)+" cursor="+Ref(cursor)+" cursorIndex="+(cursor==null?"NOT_CAPTURED":cursor.CurrentIndex.ToString())+
                    " sourceLocal="+Decision(cursor?.Source,cursor!=null)+" decisionLocal="+Decision(decision,hasDecisionLocal)+
                    " cachedPreviewKind="+(preview==null?"NOT_CAPTURED":preview.Kind.ToString())+" cachedPreviewId="+(preview==null?"NOT_CAPTURED":preview.Id)+
                    " cachedPreviewVersion="+(preview==null?"NOT_CAPTURED":preview.ExpectedVersion.ToString()));
            }
            catch(Exception) { }
#endif
        }
        private bool PrepareReservationAnchor(PickupReservationAnchor a,TrafficWorldSnapshot snapshot,out TrafficDecision decision,int candidateIndex=-1)
        {
            decision=null;
            if(!ReservationAnchorCurrent(a))
            {ObserveReservationRefusal("anchorCurrent",a?.Visit,a,sideIndex:candidateIndex);return false;}
            if(!trafficCandidates.TryGetValue(a.Key,out var cursor))
            {ObserveReservationRefusal("cursorMissing",a.Visit,a,sideIndex:candidateIndex);return false;}
            var index=candidateIndex<0?a.SideIndex:candidateIndex;
            var expected=a.Submitted?a.Coordinator.GetDecision(a.Key):null;
            if(!cursor.TryDescribe(a.Key,a.Actor,index,out var side,out _) ||
                (index!=a.SideIndex && (!a.Submitted || expected?.FailureDisposition!=TrafficFailureDisposition.AlternateAllowed)) ||
                (expected!=null && expected.FailureDisposition!=TrafficFailureDisposition.None &&
                 !(candidateIndex>=0 && expected.FailureDisposition==TrafficFailureDisposition.AlternateAllowed && index!=a.SideIndex)))
            {ObserveReservationRefusal("describe-or-alt",a.Visit,a,cursor,expected,true,index);return false;}
            if(!a.Owner.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,a.Key.ActorId,a.Owner.Version,out var preview))
            {
                if(a.Submitted && expected!=null)a.Coordinator.TryHoldCanonicalAnchorPreparation(expected);
                ObserveReservationRefusal("preview",a.Visit,a,cursor,expected,true,index);return false;
            }
            if(preview.Kind!=PickupQueueService.MutationKind.Reserve || preview.Id!=a.Key.ActorId || preview.Assignment.VisitId!=a.Key.ActorId ||
                !preview.BelongsTo(a.Owner) || !preview.IsCurrent)
            {ObserveReservationRefusal("preview",a.Visit,a,cursor,expected,true,index);return false;}
            var proofs=PreservePickupGoals(a.Visit,preserveExitCapability:true).ToList();
            if(a.Employee!=null)
            {
                var occupied=TrafficObstacles(a.Employee.ActorId);var clearance=RouteClearance(a.Employee.Settings);
                proofs.Add(new TrafficRouteRequirement("work-outward",a.Work,side.Position,clearance,occupied));
                proofs.Add(new TrafficRouteRequirement("work-home",side.Position,a.Work,clearance,occupied));
            }
            proofs.Add(new TrafficRouteRequirement("waiting-exit-capability",preview.Assignment.Position,a.Exit,
                RouteClearance(a.Actor.Settings),TrafficObstacles(a.Visit.Id),avoidProposedParking:false));
            var request=new TrafficRequest(a.Key,a.FirstFrame,snapshot,a.Start,preview.Assignment.Position,a.Actor.Settings.AgentRadius,
                "pickup/"+a.Visit.Id,requirements:proofs,safetyMargin:a.Actor.Settings.CollisionSkin+a.Actor.Settings.Epsilon,planPrimaryRoute:true);
            if(a.Eligibility==null)a.Eligibility=new TrafficUnpaidParkingEligibility(a.Key,a.Actor,a.Counter,a.Visit,a.Admission,
                ()=>CanIntegratedProgress && a.Visit.PaidPickup==null && ReservationAnchorCurrent(a));
            if(cursor.UnpaidEligibility==null && cursor.Source==null)cursor.UnpaidEligibility=a.Eligibility;
            if(!ReferenceEquals(cursor.UnpaidEligibility,a.Eligibility))return false;
            if(!PrepareTrafficCandidate(cursor,request,a.Actor,index,expected,out decision))
            {ObserveReservationRefusal("corePrepare",a.Visit,a,cursor,decision,true,index);return false;}
            // Only successful Core advancement can replace the submitted proof-role descriptor.
            // Queue的新tail仍来自preview；成功advance之后才更新原anchor的side。
            if(index!=a.SideIndex){a.SideIndex=index;a.Side=side;}
            a.Submitted=true;
            a.Preview=preview;
            if(!ReservationAnchorCurrent(a))
            {ObserveReservationRefusal("postCurrent.anchor",a.Visit,a,cursor,decision,true,index);return false;}
            if(!preview.IsCurrent)
            {ObserveReservationRefusal("postCurrent.preview",a.Visit,a,cursor,decision,true,index);return false;}
            return true;
        }
        // Called once by the existing driver after snapshot collection, before its normal ready pump.
        // 仅重建已存在proposal的当前preview/constraints；不建Queue、不调用policy/query、不写业务状态。
        internal void RefreshTrafficProposals(TrafficWorldSnapshot snapshot,EmployeeFlowController deliveryProducer=null)
        {
            CancelStoppedAutomaticAdmission();
            if(preparingTrafficProposals || admissionInProgress || traffic==null || !CanIntegratedProgress ||
                snapshot==null || !ReferenceEquals(snapshot,trafficSnapshot))return;
            preparingTrafficProposals=true;
            try
            {
                if(pendingAdmissionAnchor?.Submitted==true && (!pendingAdmissionAnchor.Automatic || autoSpawn))
                    PrepareAdmissionAnchor(pendingAdmissionAnchor,snapshot,out _);
                foreach(var visit in visits.Values)
                    if(visit.PendingReservationAnchor?.Submitted==true)PrepareReservationAnchor(visit.PendingReservationAnchor,snapshot,out _);
                foreach(var visit in visits.Values)
                    if(visit.PendingCounterAdvance?.Source!=null)PrepareCounterAdvanceAnchor(visit.PendingCounterAdvance,snapshot,out _);
                foreach(var visit in visits.Values.ToArray())
                    if(visit.PaidPickup?.Accepted==true && !visit.PaidPickup.Transferred && !visit.PaidPickup.Failed && !visit.PaidPickup.RecoveryUsed)
                        RefreshPaidPreparation(visit.PaidPickup.Preparation,snapshot);
                foreach(var cursor in trafficCandidates.Values.ToArray())
                    if(cursor.Source!=null && cursor.PickupPreparation!=null)PreparePickupFamily(cursor.PickupPreparation,snapshot,out _);
                foreach(var cursor in trafficCandidates.Values.ToArray())
                    if(cursor.Source!=null && cursor.CollectionRelease!=null)PrepareCollectionRelease(cursor.CollectionRelease,snapshot,out _);
                foreach(var visit in visits.Values)
                    if(visit.ReturnPreparation!=null)PreparePickupFamily(visit.ReturnPreparation,snapshot,out _);
                if(deliveryProducer!=null)
                {
                    deliveryProducer.RefreshTrafficMovementPreparations(this,snapshot);
                    foreach(var cursor in trafficCandidates.Values.Where(c=>c.Role==TrafficCandidateRole.Delivery &&
                        c.Source!=null && c.DeliveryContext!=null).ToArray())RefreshDeliveryPreparation(cursor,snapshot,deliveryProducer);
                }
            }
            finally{preparingTrafficProposals=false;CancelStoppedAutomaticAdmission();}
        }
        // Identity tap: the original guard runs once; default-null Editor receipt cannot change its result.
        private bool AdmissionRefused(string stage,bool refused)
        {
#if UNITY_EDITOR
            if(refused && CorridorObserver!=null)
                try{ObserveCorridor("admission.original-refusal",result:stage);}catch(Exception){}
#endif
            return refused;
        }
        private bool TryTrafficAdmission(bool automatic)
        {
            var coordinator=traffic;var counter=queue;var capacity=Capacity;var before=nextVisit;var currentSession=session;
            var id=currentSession+"-"+(before+1).ToString("D8");var prefab=prefabs[(int)(before%prefabs.Length)];
            var position=spawn.position;var rotation=spawn.rotation;
            var key=TrafficKey(id,"counter-admission",id);
            if(pendingAdmissionAnchor==null)pendingAdmissionAnchor=new CounterAdmissionAnchor{Coordinator=coordinator,Key=key,Owner=counter,
                Capacity=capacity,World=world,Prefab=prefab,Spawn=spawn,Session=currentSession,Before=before,FirstFrame=Time.frameCount,
                Automatic=automatic,Position=position,Rotation=rotation,Radius=prefab.Settings.AgentRadius,Safety=prefab.Settings.CollisionSkin+prefab.Settings.Epsilon,
                Skin=prefab.Settings.CollisionSkin,Epsilon=prefab.Settings.Epsilon};
            var anchor=pendingAdmissionAnchor;var snapshot=RefreshTrafficSnapshot();
            if(AdmissionRefused("anchor-key",!anchor.Key.Equals(key)) || AdmissionRefused("anchor-prepare",!PrepareAdmissionAnchor(anchor,snapshot,out var decision)))return false;
            var prepared=anchor.Preview;var target=prepared.Assignment.Position;
            if(AdmissionRefused("decision-not-granted",decision.Kind!=TrafficDecisionKind.Granted)){Status="等待入店通行 / "+decision.Kind+": "+decision.Reason;return false;}
            var proof=decision.ProofRoutes.SingleOrDefault(p=>p.ResourceId=="counter/"+id);
            bool ContextCurrent()=>AdmissionAnchorCurrent(anchor) && !UnreservedFrontHoldsAdmission() && ReferenceEquals(anchor.Preview,prepared) &&
                PrimaryMatchesCounterAdmissionPreview(counter,id,prepared,proof);
            if(AdmissionRefused("context-before-birth",!ContextCurrent()))return false;
            NavigationActor actor=null;var committed=false;admissionInProgress=true;
            try
            {
                if(AdmissionRefused("context-before-create",!ContextCurrent()))return false;
                actor=Instantiate(prefab,position,rotation,transform);
                if(AdmissionRefused("birth-actor-null",actor==null) || AdmissionRefused("birth-runtime-id",!actor.TryInitializeRuntimeId(id)) || AdmissionRefused("birth-native-enable",!actor.TryEnableSteadyPathMotion()))return false;
                var appearance=collectedCarryAppearances.FirstOrDefault(a=>a!=null && a.CustomerPrefab==prefab);
                actor.gameObject.AddComponent<CustomerCarryPresenter>().Configure(actor,appearance,collectedCupPrefab);
                ObserveCorridor("admission.birth-ready",target:target,result:id);
                if(AdmissionRefused("post-callback-policy",!AdmissionPolicyAllows()) || AdmissionRefused("post-callback-context",!ContextCurrent()) || AdmissionRefused("post-callback-inactive",!actor.isActiveAndEnabled) || AdmissionRefused("post-callback-id",actor.ActorId!=id) || AdmissionRefused("post-callback-pose",actor.transform.position!=position))return false;
                var actual=RefreshTrafficSnapshot();
                if(AdmissionRefused("birth-snapshot-version",actual.Version!=snapshot.Version) || AdmissionRefused("birth-body-version",actual.BodyVersion!=snapshot.BodyVersion) || AdmissionRefused("birth-final-context",!ContextCurrent()))return false;
                var birth=new CounterBirthMutation(this,prepared,actor,
                    ()=>ContextCurrent() && actor!=null && actor.isActiveAndEnabled && actor.ActorId==id && actor.transform.position==position,before+1);
                committed=coordinator.TryCommit(decision,birth,()=>
                {
                    pendingAdmissionAnchor=null;
                    admissionRejected=admissionRouteUnavailable=false;
                    PublishTrafficCounterRoute(birth.AddedVisit,decision,proof,CustomerVisitState.Entering,true);
                });
                AdmissionRefused("birth-joint-commit",!committed);
                return committed;
            }
            finally
            {
                if(!committed)
                {
                    // Preserve an already-pending source; cancellation cannot revive canonical failure or discard its accounting.
                    // 读取真实当前来源；仅撤销未出生许可，Deferred/Waiting 和真正失败保持原状态。
                    coordinator.TryCancelCanonicalAnchorPreparation(coordinator.GetDecision(key));
                    if(actor!=null)
                    {
                        if(world!=null && world.RegisteredActors.Contains(actor))world.Unregister(actor);
                        Destroy(actor.gameObject);
                    }
                }
                admissionInProgress=false;
                CancelStoppedAutomaticAdmission();
            }
        }
        private CounterAdvanceAnchor CaptureCounterAdvance(Visit visit,TrafficIntentKey key,CounterQueueService counter,CounterQueueService.PreparedMutation preview)
        {
            if(visit.PendingCounterAdvance!=null || !ReferenceEquals(counter,queue) || visit.Admission==null ||
                !TrafficActorReady(visit.Actor) || traffic.ContainsIntent(key) || preview==null || !preview.IsCurrent ||
                !preview.BelongsTo(counter) || preview.Kind!=CounterQueueService.MutationKind.Advance || preview.Id!=visit.Id ||
                !counter.TryCaptureAssignment(visit.Id,out var claim) || !claim.IsCurrent || preview.Assignment.VisitId!=visit.Id ||
                preview.Assignment.Generation!=claim.Assignment.Generation+1)return null;
            // The actual Queue preview, not a caller flag, proves existing explicit replan eligibility.
            // 正常 Queued 须真实 Arrived；已有 recoveringQueue/Blocked 仅沿用真实 owner 的原 Advance 入口。
            if(!(visit.State==CustomerVisitState.Queued && claim.Assignment.Arrived) &&
                !(recoveringQueue && visit.State==CustomerVisitState.Blocked && !claim.Assignment.Arrived))return null;
            var totals=Capacity.GetReservations().Where(r=>r.OwnerId==visit.Id && r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied).ToArray();
            if(totals.Length!=1)return null;
            var a=new CounterAdvanceAnchor(this,visit,key,counter,claim,preview,totals[0].Token,CustomerSourceFrame(visit,key));
            visit.PendingCounterAdvance=a;
            if(CounterAdvanceAnchorCurrent(a)){RefreshCounterAdvanceWorkingPredecessor(a);return a;}
            RetireCounterAdvance(visit);return null;
        }
        private TrafficRequest CounterAdvanceRequest(CounterAdvanceAnchor a,TrafficWorldSnapshot snapshot,QueueAssignment? plannedTarget=null)
            =>new TrafficRequest(a.Key,a.FirstFrame,snapshot,a.Start,(plannedTarget??a.Target).Position,a.Radius,"counter/"+a.Visit.Id,
                requirements:PreservePickupGoals(a.Visit),safetyMargin:a.Skin+a.Epsilon,planPrimaryRoute:true);
        private bool PrepareCounterAdvanceAnchor(CounterAdvanceAnchor a,TrafficWorldSnapshot snapshot,out TrafficDecision next,
            CounterQueueService.PreparedMutation preview=null)
        {
            next=null;
            if(a==null || a.Source==null || snapshot==null || !ReferenceEquals(snapshot,trafficSnapshot))return false;
            if(!CounterAdvanceAnchorCurrent(a))
            {if(ReferenceEquals(a.Visit.PendingCounterAdvance,a) && !CounterAdvanceIdentityCurrent(a,requireNavigation:false))RetireCounterAdvance(a.Visit);return false;}
            RefreshCounterAdvanceWorkingPredecessor(a);
            var expected=a.Coordinator.GetDecision(a.Key);
            if(expected.FailureDisposition==TrafficFailureDisposition.Terminal)
            {
                if(a.Coordinator.ContainsIntent(a.Key) && CounterAdvanceAnchorCurrent(a) && ReferenceEquals(a.Coordinator.GetDecision(a.Key),expected))
                    BlockVisit(a.Visit,CustomerVisitState.Advancing,"Counter traffic "+expected.Kind+": "+expected.Reason);
                return false;
            }
            if(expected.FailureDisposition!=TrafficFailureDisposition.None || expected.AcquiredSequence!=0 || a.Coordinator.HasActiveGrant(a.Key))return false;
            if(preview==null && !a.Counter.TryPrepareMutation(CounterQueueService.MutationKind.Advance,a.Visit.Id,a.Counter.Version,out preview))return false;
            if(!CounterAdvancePreviewCanReprepare(a,preview))return false;
            if(!a.Coordinator.TryReprepareCanonicalAnchor(expected,CounterAdvanceRequest(a,snapshot,preview.Assignment),out next))return false;
            // Publish only after the original Core accepts and the same producer/preview still owns this result.
            // Core拒绝或同步重入替换owner时只返回；不污染替代者，也不更新本地规划身份。
            if(!CounterAdvanceAnchorCurrent(a) || !CounterAdvancePreviewCanReprepare(a,preview) ||
                !ReferenceEquals(a.Coordinator.GetDecision(a.Key),next))return false;
            a.Target=preview.Assignment;a.Preview=preview;a.Source=next;return BindCounterAdvanceReadiness(a,next);
        }
        // Defer birth only across an authenticated, still-live canonical-to-Native handoff.
        // 不给未发腿finite权限；只让全新的FIFO后客等前客原有handoff结束，原Step其他工作继续。
        private bool CounterAdvanceTargetHeldByUnissuedPredecessor(Visit follower,CounterQueueService counter,
            QueueAssignment assignment,CounterQueueService.PreparedMutation preview)
        {
            try
            {
                if(!CanIntegratedProgress || !ReferenceEquals(counter,queue) || follower==null || follower.PendingCounterAdvance!=null ||
                    follower.State!=CustomerVisitState.Queued || !follower.CounterArrived || follower.QueueLeft || follower.HasCollected ||
                    follower.RequestId!=0 || !assignment.Arrived || !SameAssignment(follower.Assignment,assignment) || !TrafficActorReady(follower.Actor) ||
                    preview==null || !preview.IsCurrent || !preview.BelongsTo(counter) || preview.Kind!=CounterQueueService.MutationKind.Advance ||
                    preview.Id!=follower.Id || preview.Assignment.VisitId!=follower.Id || preview.Assignment.Generation!=assignment.Generation+1 ||
                    trafficKeys.ContainsKey(follower.Id+"/counter-advance/"+follower.Id+"/assignment/"+assignment.Generation))return false;
                var rows=counter.Snapshot;var index=-1;
                for(var i=0;i<rows.Count;i++)if(rows[i].VisitId==follower.Id){index=i;break;}
                if(index<=0 || !rows[index].Equals(assignment))return false;
                var previous=rows[index-1];
                if(previous.Arrived || previous.SlotIndex!=preview.Assignment.SlotIndex-1 ||
                    !visits.TryGetValue(previous.VisitId,out var predecessor) || predecessor.State!=CustomerVisitState.Advancing ||
                    predecessor.QueueLeft || predecessor.HasCollected || predecessor.RequestId!=0 || predecessor.PendingCounter!=null ||
                    !predecessor.Assignment.Equals(previous) || !TrafficActorReady(predecessor.Actor) ||
                    !counter.TryCaptureAssignment(previous.VisitId,out var claim) || !claim.IsCurrent || !claim.Assignment.Equals(previous))return false;
                var selected=predecessor.TrafficSelection;var move=predecessor.TrafficMove;var pending=predecessor.PendingTraffic;
                if(!TrafficSelectionCurrent(predecessor,selected) || selected.Phase!=CustomerVisitState.Advancing ||
                    selected.Source.AcquiredSequence<=0 || !ReferenceEquals(selected.Counter,counter) ||
                    selected.CounterEntry==null || !selected.CounterEntry.IsSameReservation(claim) ||
                    move==null || !ReferenceEquals(move.Coordinator,traffic) || !ReferenceEquals(move.Source,selected.Source) ||
                    !move.Intent.Equals(selected.Intent) || move.Intent.IntentId!="counter-advance" || move.Suspended ||
                    move.NativeGeneration<=0 || move.NativeGeneration!=predecessor.Generation || !predecessor.TrafficMovePending ||
                    !move.Goal.Position.Equals(previous.Position) || !predecessor.FinalTarget.Position.Equals(previous.Position) ||
                    pending==null || !pending.Marked || pending.Kind!=TrafficPhaseMilestoneKind.Arrived || pending.Handle==null ||
                    !ReferenceEquals(pending.Flow,this) || !ReferenceEquals(pending.Visit,predecessor) ||
                    !ReferenceEquals(pending.Actor,predecessor.Actor) || !ReferenceEquals(pending.Admission,predecessor.Admission) ||
                    !ReferenceEquals(pending.Coordinator,traffic) || !TrafficReceiptCurrent(pending) ||
                    !traffic.IsCurrentPhaseHandoff(pending.Handle,pending.Source,predecessor.Actor))return false;
                var current=traffic.GetDecision(move.Intent);
                if(current.FailureDisposition!=TrafficFailureDisposition.None || current.AcquiredSequence!=selected.Source.AcquiredSequence)return false;
                var held=false;
                foreach(var binding in pending.Claims??Array.Empty<TrafficClaimBinding>())
                    if(binding.Kind==TrafficClaimKind.Counter && ReferenceEquals(binding.Counter,counter) && binding.CounterEntry!=null &&
                        binding.CounterEntry.IsSameReservation(claim) && binding.Fact.ResourceId=="counter/"+predecessor.Id &&
                        ReadTrafficClaim(pending,binding.Fact,out var actual)==TrafficPhaseClaimState.Held &&
                        ReferenceEquals(actual.CanonicalOwner,counter) && actual.Position.Equals(previous.Position) &&
                        ReferenceEquals(actual.BusinessOwner,predecessor) && ReferenceEquals(actual.ReleaseObligation,predecessor.Admission))
                    {held=true;break;}
                if(!held)return false;
                var first=follower.Actor.Settings;var second=predecessor.Actor.Settings;
                var clearance=first.AgentRadius+second.AgentRadius+Mathf.Max(first.CollisionSkin+first.Epsilon,second.CollisionSkin+second.Epsilon);
                var occupied=predecessor.Actor.transform.position-preview.Assignment.Position;occupied.y=0;
                var vacated=previous.Position-preview.Assignment.Position;vacated.y=0;
                return occupied.magnitude<clearance && vacated.magnitude>=clearance && preview.IsCurrent;
            }
            catch(Exception){return false;} // Unknown/expired/ABA facts use the original Core safety policy, never a new hold.
        }
        private void AdvanceTrafficCounterQueue()
        {
            var coordinator=traffic;var counter=queue;
            foreach(var assignment in counter.Snapshot)
            {
                if(!visits.TryGetValue(assignment.VisitId,out var visit) || visit.Actor==null || !visit.Actor.isActiveAndEnabled ||
                    !counter.TryPrepareMutation(CounterQueueService.MutationKind.Advance,visit.Id,counter.Version,out var prepared))continue;
                var anchor=visit.PendingCounterAdvance;
                if(anchor==null && CounterAdvanceTargetHeldByUnissuedPredecessor(visit,counter,assignment,prepared))return;
                var generation=visit.Generation;var key=anchor?.Key??TrafficKey(visit.Id,"counter-advance",visit.Id+"/assignment/"+assignment.Generation);
                var snapshot=RefreshTrafficSnapshot();
                if(anchor==null)anchor=CaptureCounterAdvance(visit,key,counter,prepared);
                if(anchor==null)return;
                if(!CounterAdvanceAnchorCurrent(anchor))
                {if(ReferenceEquals(visit.PendingCounterAdvance,anchor) && !CounterAdvanceIdentityCurrent(anchor,requireNavigation:false))RetireCounterAdvance(visit);return;}
#if UNITY_EDITOR
                if(CorridorObserver!=null)ObserveCorridor("traffic.counter-advance-preflight",visit,target:prepared.Assignment.Position,
                    result:"key="+key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context);
#endif
                if(!CounterAdvanceAnchorCurrent(anchor))
                {if(ReferenceEquals(visit.PendingCounterAdvance,anchor) && !CounterAdvanceIdentityCurrent(anchor,requireNavigation:false))RetireCounterAdvance(visit);return;}
                TrafficDecision decision;
                if(anchor.Source==null)
                {
                    if(coordinator.ContainsIntent(key) || !CounterAdvancePreviewMatches(anchor,prepared,true))return;
                    decision=coordinator.EvaluateParking(CounterAdvanceRequest(anchor,snapshot));anchor.Source=decision;
                    if(!BindCounterAdvanceReadiness(anchor,decision))
                    {RetireCounterAdvance(visit);return;}
                }
                else if(!PrepareCounterAdvanceAnchor(anchor,snapshot,out decision,prepared))return;
                prepared=anchor.Preview;
                if(decision.Kind!=TrafficDecisionKind.Granted)return;
                if(!TrafficCounterVisitCurrent(visit,coordinator,key,generation) || !ReferenceEquals(queue,counter) ||
                    !CounterAdvanceAnchorCurrent(anchor) || !ReferenceEquals(anchor.Source,decision) ||
                    !ReferenceEquals(coordinator.GetDecision(key),decision) || !CounterAdvancePreviewMatches(anchor,prepared,true))return;
                var proof=decision.ProofRoutes.Single(p=>p.ResourceId=="counter/"+visit.Id);
                if(proof.Waypoints.Count==0 || proof.Waypoints.Count>24 || proof.Waypoints.Last()!=prepared.Assignment.Position)return;
                if(!CanCommitCustomerCanonicalSuccessor(visit,coordinator,decision,CustomerVisitState.Advancing,out _))return;
                coordinator.TryCommit(decision,prepared,()=>
                {
                    if(ReferenceEquals(visit.PendingCounterAdvance,anchor))visit.PendingCounterAdvance=null;
                    visit.Assignment=prepared.Assignment;
                    PublishTrafficCounterRoute(visit,decision,proof,CustomerVisitState.Advancing,false);
                });
                return;
            }
        }
        private TrafficDecision EvaluatePickupTraffic(NavigationActor actor,TrafficIntentKey key,Vector3 target,
            IEnumerable<TrafficRouteRequirement> requirements,bool parking=true,TrafficCandidateCursor cursor=null,int candidateIndex=0)
        {
            var snapshot=RefreshTrafficSnapshot();
            var requestedFrame=visits.TryGetValue(actor.ActorId,out var visit)?CustomerSourceFrame(visit,key):Time.frameCount;
            var request=new TrafficRequest(key,requestedFrame,snapshot,actor.transform.position,target,actor.Settings.AgentRadius,
                "pickup/"+actor.ActorId,requirements:requirements,safetyMargin:actor.Settings.CollisionSkin+actor.Settings.Epsilon,planPrimaryRoute:true);
            if(cursor==null)return parking?traffic.EvaluateParking(request):traffic.EvaluateMovement(request);
            var expected=cursor.Source==null?null:traffic.GetDecision(key);
            return PrepareTrafficCandidate(cursor,request,actor,candidateIndex,expected,out var decision)?decision:traffic.GetDecision(key);
        }
        private IEnumerable<TrafficRouteRequirement> PreservePickupGoals(Visit incoming,bool preserveExitCapability=false)
        {
            foreach(var owner in visits.Values.Where(v=>v!=incoming && v.Actor!=null && v.PickupAssigned &&
                (!v.HasCollected || preserveExitCapability && !v.PickupDeparted) && v.PickupOwner!=null))
                if(owner.PickupOwner.TryGetReservation(owner.Id,out var assignment))
                {
                    var clearance=RouteClearance(owner.Actor.Settings);var obstacles=TrafficObstacles(owner.Id);
                    // Moving owners already have a selected actual-to-goal route; preserve the original phase filter.
                    if(!owner.HasCollected && (!owner.PickupInTransit || owner.State==CustomerVisitState.PickupWaiting || owner.State==CustomerVisitState.PickupService))
                        yield return new TrafficRouteRequirement("existing-goal/"+owner.Id,owner.Actor.transform.position,assignment.Position,clearance,obstacles);
                    // Only new unpaid PQS Reserve parking preserves this capability until real canonical departure/release.
                    // 取餐不是实际离位；只对新PQS预留保留仍拥有目标的离店能力，默认actual-to-goal范围不变。
                    if(preserveExitCapability)
                        yield return new TrafficRouteRequirement("existing-goal/"+owner.Id+"/exit-capability",assignment.Position,exit.position,clearance,obstacles);
                }
        }
        private bool TryTrafficReservation(Visit visit,out string reason)
        {
            reason="等待Pick-up空间许可 / Preparing pickup traffic";
            var currentSnapshot=RefreshTrafficSnapshot();
            if(visit.PickupAssigned)
            {
                // Ownership is not a per-frame movement proposal. Check the actual entry and same-layout proof;
                // actual movement gets a new complete current-pose proof only after real payment.
                var held=PickupParkingCurrent(visit,currentSnapshot);
                reason=held?"":"原Pick-up预留身份已失效 / Existing pickup right is no longer current";return held;
            }
            if(!EnsurePickupQueue(visit,out reason))return RefuseUnreservedFront(visit);
            var owner=visit.PickupAssigned?visit.PickupOwner:pickupQueue;
            if(!owner.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,visit.Id,owner.Version,out var prepared))
            {
                var pending=visit.PendingReservationAnchor;
                if(pending?.Submitted==true && ReservationAnchorCurrent(pending))
                    pending.Coordinator.TryHoldCanonicalAnchorPreparation(pending.Coordinator.GetDecision(pending.Key));
                return false;
            }
            var target=prepared.Assignment.Position;
            // The diagnostic projection modifies only the canonical NEXT-slot inputs before this original decision seam.
            if(!visit.PickupAssigned) ObserveCorridor("reserve.candidate",visit,target:target);
            var key=TrafficKey(visit.Id,"pickup-wait",visit.Id+"/"+visit.TrafficPhaseSequence);
            currentSnapshot=RefreshTrafficSnapshot();
            if(!TrafficActorReady(visit.Actor))
            {ObserveReservationRefusal("actor-ready",visit,key:key);return false;}
            var cursor=GetTrafficCursor(key,visit.Actor,TrafficCandidateRole.Reservation,pickupSides);
            if(cursor==null){reason="Pick-up候选已停止 / Pickup candidate stopped";ObserveReservationRefusal("candidate-null",visit,key:key);return RefuseUnreservedFront(visit);}
            var employee=world.RegisteredActors.FirstOrDefault(a=>a!=null && a.ActorId==deliveryActorId);
            if(employee!=null && !TrafficActorReady(employee))
            {ObserveReservationRefusal("employee-ready",visit,cursor:cursor,key:key);return false;}
            if(!SelectTrafficCandidate(cursor,visit.Actor,(candidate,side,unused)=>employee==null ||
                owner.TryPrepareMutation(PickupQueueService.MutationKind.DeliveryLease,employee.ActorId,owner.Version,out _,side.Position),
                out var index,out var current))
            {if(current!=null)reason=current.Kind+": "+current.Reason;ObserveReservationRefusal("select-false",visit,cursor:cursor,decision:current,hasDecisionLocal:true,sideIndex:index,key:key);return RefuseUnreservedFront(visit,current);}
            if(visit.PendingReservationAnchor==null)
            {
                if(!queue.TryCaptureAssignment(visit.Id,out var counterClaim))
                {ObserveReservationRefusal("counter-claim",visit,cursor:cursor,decision:current,hasDecisionLocal:true,sideIndex:index,key:key);return false;}
                visit.PendingReservationAnchor=new PickupReservationAnchor{Coordinator=traffic,Key=key,Owner=owner,Counter=queue,CounterClaim=counterClaim,
                    Visit=visit,Actor=visit.Actor,Admission=visit.Admission,Capacity=Capacity,World=world,
                    Tokens=Capacity.GetReservations().Where(r=>r.OwnerId==visit.Id).ToArray(),EmployeeId=deliveryActorId,
                    Employee=employee,Phase=visit.State,
                    Generation=visit.Generation,Cursor=visit.TrafficPhaseSequence,SideIndex=index,Side=cursor.Sides[index],
                    Start=visit.Actor.transform.position,Work=deliveryWorkTarget.Position,Exit=exit.position,FirstFrame=CustomerSourceFrame(visit,key)};
            }
            var anchor=visit.PendingReservationAnchor;
            var anchorKeyMatches=anchor.Key.Equals(key);TrafficDecision decision=null;
            if(!anchorKeyMatches || !PrepareReservationAnchor(anchor,currentSnapshot,out decision,index))
            {
                ObserveReservationRefusal(anchorKeyMatches?"prepare-anchor-false":"anchor-key",visit,anchor,cursor,decision,anchorKeyMatches,index,key);
                if(!anchor.Submitted && cursor.Source==null && ReferenceEquals(visit.PendingReservationAnchor,anchor))visit.PendingReservationAnchor=null;
                return false;
            }
            prepared=anchor.Preview;target=prepared.Assignment.Position;
            if(decision.Kind!=TrafficDecisionKind.Granted)
            {
                reason=decision.Kind+": "+decision.Reason;
                ObserveCorridor("reserve.traffic-decision",visit,target:target,result:reason+" owners="+string.Join(";",decision.Blockers.Select(b=>b.Owner.ActorId+"/"+b.Proof)));
                if(decision.Blockers.Count>0) ObservePickup("reserve.deferred-service",visit,owner,prepared.Assignment);
                ObserveReservationRefusal("decision-not-granted",visit,anchor,cursor,decision,true,index,key);
                return RefuseUnreservedFront(visit,decision);
            }
            var primary=decision.ProofRoutes.FirstOrDefault(p=>p.ResourceId=="pickup/"+visit.Id);
            if(!ReservationAnchorCurrent(anchor) || !ReferenceEquals(anchor.Preview,prepared) ||
                !PrimaryMatchesPickupReservationPreview(owner,visit.Id,prepared,primary))
            {ObserveReservationRefusal("commit-preview-current",visit,anchor,cursor,decision,true,index,key);return false;}
            var originalAssigned=visit.PickupAssigned;
            if(!CanCommitCustomerCanonicalSuccessor(visit,traffic,decision,visit.State,out reason))
            {ObserveReservationRefusal("canonical-successor-can-commit",visit,anchor,cursor,decision,true,index,key);return false;}
            var committed=traffic.TryCommit(decision,prepared,()=>
            {
                visit.PendingReservationAnchor=null;
                visit.PickupAssigned=true;visit.PickupOwner=owner;visit.ReservedPickup=prepared.Assignment;visit.ReservedPickupOwner=owner;
                visit.ReservedPickupRevision=adapter.Revision;visit.ReservedPickupPath=primary.ExpandedPath;visit.ReservedPickupRoute=primary.Waypoints;
                PublishCustomerOwner(visit,key);visit.PickupTrafficPermit=decision;
                var committedSnapshot=RefreshTrafficSnapshot();owner.TryCaptureReservation(visit.Id,out var claim);
                visit.PickupParking=new PickupParkingRight{Coordinator=traffic,Actor=visit.Actor,Admission=visit.Admission,
                    Owner=owner,Claim=claim,Source=decision,Proof=primary,Revision=adapter.Revision,
                    CollisionSkin=visit.Actor.Settings.CollisionSkin,Epsilon=visit.Actor.Settings.Epsilon,
                    Fact=committedSnapshot.Parking.SingleOrDefault(p=>p.ResourceId=="pickup/"+visit.Id && ReferenceEquals(p.CanonicalOwner,owner) &&
                        ReferenceEquals(p.BusinessOwner,visit) && ReferenceEquals(p.ReleaseObligation,visit.Admission)),
                    Token=Capacity.GetReservations().Single(r=>r.OwnerId==visit.Id && r.Kind==CapacityKind.PickUp).Token};
                if(ReferenceEquals(refusedPickupFront,visit))refusedPickupFront=null;
                ObservePickup(originalAssigned?"reserve.revalidated":"reserve.published",visit,owner,prepared.Assignment);
                ObservePickup("reserve.accepted",visit,owner,prepared.Assignment);
            });
            reason=committed?"":"停车共同提交已失效 / Stale pickup commit";
            if(!committed)ObserveReservationRefusal("joint-commit-false",visit,anchor,cursor,decision,true,index,key);
            return committed;
        }
        private bool PickupParkingIdentityCurrent(Visit visit)
        {
            var held=visit.PickupParking;
            if(held==null || held.Retired || !visit.PickupAssigned || visit.PickupInTransit || visit.HasCollected ||
                held.Actor==null || !held.Actor.isActiveAndEnabled || !ReferenceEquals(held.Actor,visit.Actor) ||
                !ReferenceEquals(held.Admission,visit.Admission) || !ReferenceEquals(held.Coordinator,traffic) ||
                !ReferenceEquals(held.Owner,visit.PickupOwner) || held.Revision!=adapter.Revision ||
                held.Source==null || !ReferenceEquals(held.Source,visit.PickupTrafficPermit) ||
                held.Source.Intent.Session!=trafficSession || held.Source.Intent.Epoch!=trafficEpoch || held.Source.Intent.LayoutRevision!=adapter.Revision ||
                !traffic.HasActiveGrant(held.Source.Intent) || !traffic.TryGetCommittedJourney(held.Source.Intent,out var retained) ||
                !ReferenceEquals(retained,held.Source) || held.Claim?.IsCurrent!=true ||
                !held.Owner.TryCaptureReservation(visit.Id,out var current) || !held.Claim.IsSameReservation(current) || !current.IsCurrent ||
                !visit.ReservedPickup.HasValue || !SameAssignment(current.Assignment,visit.ReservedPickup.Value) ||
                !ReferenceEquals(visit.ReservedPickupOwner,held.Owner) || visit.ReservedPickupRevision!=held.Revision ||
                held.Proof==null || !held.Source.ProofRoutes.Contains(held.Proof) ||
                !ReferenceEquals(held.Proof.ExpandedPath,visit.ReservedPickupPath) || !ReferenceEquals(held.Proof.Waypoints,visit.ReservedPickupRoute) ||
                held.Proof.ResourceId!="pickup/"+visit.Id || held.Proof.Waypoints.Count==0 || held.Proof.Waypoints.Last()!=current.Assignment.Position ||
                held.Fact==null || !ReferenceEquals(held.Fact.CanonicalOwner,held.Owner) || !ReferenceEquals(held.Fact.BusinessOwner,visit) ||
                !ReferenceEquals(held.Fact.ReleaseObligation,visit.Admission) || held.Fact.Position!=current.Assignment.Position ||
                held.Fact.Radius!=visit.Actor.Settings.AgentRadius || held.CollisionSkin!=visit.Actor.Settings.CollisionSkin ||
                held.Epsilon!=visit.Actor.Settings.Epsilon)return false;
            return Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,held.Token) && r.OwnerId==visit.Id &&
                r.Kind==CapacityKind.PickUp && r.State==ReservationState.Reserved);
        }
        private bool PickupParkingCurrent(Visit visit,TrafficWorldSnapshot snapshot)
        {
            if(!PickupParkingIdentityCurrent(visit) || snapshot==null || !world.LayoutAvailable)return false;
            var held=visit.PickupParking;
            var body=snapshot.Bodies.SingleOrDefault(b=>b.Owner.ActorId==visit.Id);
            if(body==null || !body.Enabled || !body.ActorActive || !ReferenceEquals(body.ActorLife,held.Actor) || body.Position!=held.Actor.transform.position)return false;
            // Compare the stored nominal float margin here; identity checks its original inputs separately.
            // 避免把已存float与未落存的加法中间值比较；不改变skin/epsilon或加入近似容差。
            // Global versions may change; exact current own entry/fact and unchanged Nav revision keep the old proof meaningful.
            // 不把保存的路径当发腿许可；完整当前身体/目标冲突仍由paid primary及strict leg重新核验。
            return snapshot.Parking.Any(p=>p.ResourceId==held.Fact.ResourceId && ReferenceEquals(p.CanonicalOwner,held.Owner) &&
                ReferenceEquals(p.BusinessOwner,visit) && ReferenceEquals(p.ReleaseObligation,held.Admission) &&
                p.OwnerVersion==held.Owner.Version && p.Position==held.Fact.Position && p.Radius==held.Fact.Radius && p.SafetyMargin==held.Fact.SafetyMargin);
        }
        internal bool LatchPaidPickupCheckpoint(string visitId,CafeOrderKey actualPaidKey,int frame)
        {
            if(frame<0 || traffic==null || actualPaidKey.SessionId!=trafficSession || actualPaidKey.Epoch!=trafficEpoch ||
                actualPaidKey.OrderId<=0 || !visits.TryGetValue(visitId??"",out var visit) || visit.HasCollected)return false;
            if(visit.PaidPickup!=null)return visit.PaidPickup.Order.Equals(actualPaidKey);
            var held=visit.PickupParking;
            if(held==null || held.Source==null || held.Source.AcquiredSequence<=0 || !ReferenceEquals(held.Admission,visit.Admission) ||
                !ReferenceEquals(held.Coordinator,traffic))return false;
            var current=PickupParkingIdentityCurrent(visit) && visit.State!=CustomerVisitState.Blocked;
            var key=TrafficKey(visit.Id,"pickup-paid-movement",actualPaidKey.SessionId+"/"+actualPaidKey.Epoch+"/"+actualPaidKey.OrderId);
            // Even a now-invalid owner records this actual checkpoint once; a later poll cannot wash its age or failure.
            visit.PaidPickup=new PaidPickupTask{Order=actualPaidKey,FirstFrame=frame,Intent=key,ActiveIntent=key,Parking=held,Failed=!current};
            visit.TrafficCandidateFrames.Add(key,frame);return current;
        }
        internal bool TryMovePaidToPickup(string visitId,CafeOrderKey actualPaidKey,bool canAdvance,out string reason)
        {
            reason="付费移动任务身份已失效 / Invalid paid movement task";
            if(!visits.TryGetValue(visitId??"",out var visit) || visit.PaidPickup==null ||
                !visit.PaidPickup.Order.Equals(actualPaidKey) || visit.PaidPickup.Failed || visit.State==CustomerVisitState.Blocked ||
                visit.HasCollected || !ReferenceEquals(visit.PaidPickup.Parking.Coordinator,traffic))return false;
            if(visit.PaidPickup.Transferred){reason="";return visit.PickupInTransit;}
            if(!PickupParkingIdentityCurrent(visit))return false;
            visit.PaidPickup.Accepted=true;
            if(canAdvance && CanIntegratedProgress)TryTrafficTransfer(visit,out _);
            // Accepted means this CFC owns and steps the task. It does not claim Transfer, Native acceptance or arrival.
            // Service因此可移除pickupMoves；等待/预算延后仍由同一CFC任务推进，不依赖全局ResumeBlocked。
            reason="已接收付费移动任务 / Paid movement task owned";return true;
        }
        internal void ProgressPaidPickupTasks()
        {
            if(!CanIntegratedProgress)return;
            foreach(var visit in visits.Values.ToArray())
            {
                var yieldContinuation=YieldContinuation(visit);
                if(yieldContinuation?.Used==true && yieldContinuation.Pending && !yieldContinuation.Failed)
                {ProgressYieldContinuation(yieldContinuation);continue;}
                if(visit.PaidPickup?.RecoveryPending==true && !visit.PaidPickup.Failed)
                {
                    if(visit.PaidPickup.Recovery?.BeforeTransfer==true)TryTrafficTransfer(visit,out _);
                    else ProgressPaidPickupRecovery(visit,out _);
                    continue;
                }
                if(visit.PaidPickup!=null && visit.PaidPickup.Accepted && !visit.PaidPickup.Transferred &&
                    !visit.PaidPickup.Failed && visit.State!=CustomerVisitState.Blocked)TryTrafficTransfer(visit,out _);
            }
        }
        private void DiscardPaidRecovery(Visit visit)
        {
            var paid=visit.PaidPickup;if(paid==null)return;
            paid.Parking?.Coordinator?.DiscardRecovery(paid.RecoveryReceipt);
            paid.RecoveryReceipt=null;paid.Recovery=null;paid.RecoveryPending=false;
            if(paid.RecoveryUsed && !visit.PickupArrived && !visit.HasCollected)paid.Failed=true;
        }
        private bool CapturePaidRecovery(Visit visit,TrafficMovement movement,MovementResult failure,int generation)
        {
            var paid=visit.PaidPickup;
            if(paid==null || paid.RecoveryUsed || paid.Failed || !paid.Accepted || !paid.Transferred ||
                visit.State!=CustomerVisitState.MovingToPickup || visit.HasCollected || visit.PickupArrived ||
                !visit.PickupAssigned || !visit.PickupInTransit || visit.DeliveryYieldPhase!=0 || movement==null ||
                !paid.ActiveIntent.Equals(movement.Intent) || !TrafficSelectionCurrent(visit,visit.TrafficSelection) ||
                !ReferenceEquals(paid.Parking.Coordinator,traffic) || !ReferenceEquals(paid.Parking.Actor,visit.Actor) ||
                !ReferenceEquals(paid.Parking.Admission,visit.Admission) || visit.PendingTraffic!=null ||
                !visit.PickupOwner.TryCaptureReservation(visit.Id,out var claim) || !claim.IsCurrent || claim.Assignment.Arrived ||
                !SameAssignment(claim.Assignment,visit.Assignment))return false;
            var membership=visit.PickupOwner.Snapshot.Where(a=>a.VisitId==visit.Id).ToArray();
            var tokens=Capacity.GetReservations().Where(r=>r.OwnerId==visit.Id).ToArray();
            if(membership.Length!=1 || !SameAssignment(membership[0],claim.Assignment) || membership[0].Arrived ||
                tokens.Length!=3 || !tokens.Any(r=>r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied) ||
                !tokens.Any(r=>r.Kind==CapacityKind.PickUp && ReferenceEquals(r.Token,paid.Parking.Token) && r.State==ReservationState.Reserved))return false;
            RefreshTrafficSnapshot();
            if(!traffic.TryRetireFailedLeg(movement.Intent,visit.Actor,new TrafficNativeStamp(failure.RequestId,generation),failure,out var receipt))return false;
            paid.RecoveryReceipt=receipt;
            paid.Recovery=new PaidRecoveryCheckpoint{Actor=visit.Actor,Admission=visit.Admission,Owner=visit.PickupOwner,
                Claim=claim,Assignment=claim.Assignment,Capacity=tokens,Segments=visit.Segments,Replans=visit.Replans};
            return true;
        }
        private bool CapturePaidPreparationRecovery(Visit visit,TrafficDecision failure)
        {
            var paid=visit.PaidPickup;
            if(paid==null || paid.Failed || paid.RecoveryUsed || paid.RecoveryReceipt!=null || paid.Transferred ||
                !paid.Accepted || !paid.ActiveIntent.Equals(failure.Intent) || queue==null ||
                !queue.TryCaptureAssignment(visit.Id,out var counterClaim) ||
                !visit.PickupOwner.TryCaptureReservation(visit.Id,out var claim))return false;
            var saved=new PaidRecoveryCheckpoint{BeforeTransfer=true,Actor=visit.Actor,Admission=visit.Admission,
                Owner=visit.PickupOwner,Claim=claim,Assignment=claim.Assignment,Counter=queue,CounterClaim=counterClaim,
                CounterPose=visit.Actor.transform.position,Capacity=Capacity.GetReservations().Where(r=>r.OwnerId==visit.Id).ToArray(),
                Segments=visit.Segments,Replans=visit.Replans};
            if(!PaidPreparationIdentityCurrent(visit,saved) || !traffic.TryRetireFailedPreparation(failure,visit.Actor,out var receipt))return false;
            paid.Recovery=saved;paid.RecoveryReceipt=receipt;return true;
        }
        private bool PaidPreparationIdentityCurrent(Visit visit,PaidRecoveryCheckpoint saved,bool requireNavigation=true)
        {
            var paid=visit.PaidPickup;
            if(saved==null || !saved.BeforeTransfer || paid==null || !paid.Accepted || paid.Transferred ||
                paid.Order.SessionId!=trafficSession || paid.Order.Epoch!=trafficEpoch || paid.Intent.LayoutRevision!=adapter.Revision ||
                !ReferenceEquals(paid.Parking,visit.PickupParking) || !ReferenceEquals(paid.Parking.Coordinator,traffic) ||
                saved.Actor==null || !saved.Actor.isActiveAndEnabled || !ReferenceEquals(saved.Actor,visit.Actor) ||
                !ReferenceEquals(saved.Admission,visit.Admission) || !ReferenceEquals(saved.Owner,visit.PickupOwner) ||
                !ReferenceEquals(saved.Counter,queue) || visit.QueueLeft || !visit.CounterArrived || visit.RequestId!=0 ||
                visit.PickupInTransit || visit.PickupArrived || visit.HasCollected || visit.DeliveryYieldPhase!=0 ||
                saved.CounterClaim?.IsCurrent!=true || !queue.TryCaptureAssignment(visit.Id,out var counter) ||
                !counter.IsCurrent || !saved.CounterClaim.IsSameReservation(counter) ||
                !SameAssignment(saved.CounterClaim.Assignment,counter.Assignment) || !SameAssignment(counter.Assignment,visit.Assignment) ||
                counter.Assignment.SlotIndex!=0 || !counter.Assignment.Arrived || queue.Snapshot.Count==0 ||
                !SameAssignment(queue.Snapshot[0],counter.Assignment) || !queue.Snapshot[0].Arrived ||
                visit.Actor.transform.position!=saved.CounterPose ||
                Vector3.Distance(saved.CounterPose,counter.Assignment.Position)>visit.Actor.Settings.ArrivalDistance ||
                saved.Claim?.IsCurrent!=true || !saved.Owner.TryCaptureReservation(visit.Id,out var current) ||
                !current.IsCurrent || !saved.Claim.IsSameReservation(current) || current.Assignment.Arrived ||
                !SameAssignment(saved.Assignment,current.Assignment) || saved.Owner.Snapshot.Any(a=>a.VisitId==visit.Id) ||
                !(requireNavigation?PickupParkingCurrent(visit,RefreshTrafficSnapshot()):PickupParkingIdentityCurrent(visit)))return false;
            // RestartAssignments may change the global version; the original head Entry and assignment are the authority.
            // 只核对原 Counter/Pickup claim 与原三枚 token，不冻结 Queue 全局 Version。
            return saved.Capacity.Length==3 &&
                saved.Capacity.Any(r=>r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied) &&
                saved.Capacity.Any(r=>r.Kind==CapacityKind.CounterQueue && r.State==ReservationState.Occupied) &&
                saved.Capacity.Any(r=>r.Kind==CapacityKind.PickUp && ReferenceEquals(r.Token,paid.Parking.Token) && r.State==ReservationState.Reserved) &&
                PaidRecoveryCapacityCurrent(visit,saved);
        }
        private bool PaidRecoveryCapacityCurrent(Visit visit,PaidRecoveryCheckpoint saved)
        {
            var actual=Capacity.GetReservations().Where(r=>r.OwnerId==visit.Id).ToArray();
            return actual.Length==saved.Capacity.Length && saved.Capacity.All(old=>actual.Any(r=>ReferenceEquals(r.Token,old.Token) &&
                r.OwnerId==old.OwnerId && r.Kind==old.Kind && r.State==old.State));
        }
        private bool PaidRecoveryIdentityCurrent(Visit visit)
        {
            var paid=visit.PaidPickup;var saved=paid?.Recovery;
            if(saved?.BeforeTransfer==true)return PaidPreparationIdentityCurrent(visit,saved);
            if(saved==null || !paid.Accepted || !paid.Transferred || !ReferenceEquals(paid.Parking.Coordinator,traffic) ||
                paid.Order.SessionId!=trafficSession || paid.Order.Epoch!=trafficEpoch || paid.Intent.LayoutRevision!=adapter.Revision ||
                saved.Actor==null || !saved.Actor.isActiveAndEnabled || !ReferenceEquals(saved.Actor,visit.Actor) ||
                !ReferenceEquals(saved.Admission,visit.Admission) || !ReferenceEquals(saved.Owner,visit.PickupOwner) ||
                !visit.PickupAssigned || !visit.PickupInTransit || visit.PickupArrived || visit.HasCollected || visit.DeliveryYieldPhase!=0 ||
                saved.Claim?.IsCurrent!=true || !saved.Owner.TryCaptureReservation(visit.Id,out var current) ||
                !current.IsCurrent || !saved.Claim.IsSameReservation(current) || current.Assignment.Arrived ||
                !SameAssignment(saved.Assignment,current.Assignment) || !SameAssignment(saved.Assignment,visit.Assignment) ||
                visit.FinalTarget.Position!=saved.Assignment.Position)return false;
            // Queue owns paid FIFO; a predecessor's real departure may compress this list without changing our Entry.
            // 核对原 paid membership 和 assignment，不把自然变化的数组下标当恢复权限。
            var membership=saved.Owner.Snapshot.Where(a=>a.VisitId==visit.Id).ToArray();
            if(membership.Length!=1 || !SameAssignment(membership[0],saved.Assignment) || membership[0].Arrived)return false;
            return PaidRecoveryCapacityCurrent(visit,saved);
        }
        // Only CLC's explicit Owner Resume calls this; ordinary polling may step an already-authorized attempt.
        // 同一个付款 phase 只授权一次新 transport attempt；已 Transfer 不重做，未 Transfer 仍须原 joint commit。
        internal bool TryResumePaidPickup(string visitId,CafeOrderKey actualPaidKey,out string reason)
        {
            reason="原付费移动不能恢复 / Paid recovery unavailable";
            if(!CanIntegratedProgress || !visits.TryGetValue(visitId??"",out var visit) || visit.State!=CustomerVisitState.Blocked ||
                visit.PaidPickup==null || !visit.PaidPickup.Order.Equals(actualPaidKey) || !visit.PaidPickup.Failed ||
                visit.PaidPickup.RecoveryUsed || visit.PaidPickup.RecoveryReceipt==null || !PaidRecoveryIdentityCurrent(visit))return false;
            var paid=visit.PaidPickup;var saved=paid.Recovery;
            if(visit.Segments!=saved.Segments || visit.Replans!=saved.Replans || visit.Segments>=32 || visit.Replans>3)return false;
            var original=paid.Intent;
            var key=new TrafficIntentKey(original.Session,original.Epoch,original.ActorId,original.IntentId,Math.Max(nextTrafficPhase,original.Generation)+1,
                original.LayoutRevision,original.Context);
            var snapshot=RefreshTrafficSnapshot();
            var request=new TrafficRequest(key,paid.FirstFrame,snapshot,visit.Actor.transform.position,saved.Assignment.Position,
                visit.Actor.Settings.AgentRadius,"pickup/"+visit.Id,requirements:PreservePickupGoals(visit),
                safetyMargin:visit.Actor.Settings.CollisionSkin+visit.Actor.Settings.Epsilon,planPrimaryRoute:true);
            if(!traffic.TryBeginExplicitRecovery(paid.RecoveryReceipt,request,visit.Actor,out var ready))return false;
            nextTrafficPhase=key.Generation;
            paid.RecoveryReceipt=null;paid.RecoveryUsed=true;paid.RecoveryPending=true;paid.Failed=false;paid.ActiveIntent=key;
            trafficKeys.Add(visit.Id+"/paid-recovery/"+original.Generation,key);visit.TrafficCandidateFrames.Add(key,paid.FirstFrame);
            // Do not use Send/SetRouteContext: their ordinary new-phase setup resets Segments/Replans.
            visit.Generation++;visit.RequestId=0;visit.State=visit.ResumeState=CustomerVisitState.MovingToPickup;visit.Failure=null;
            visit.Route=null;visit.PendingExitSegment=visit.PendingPickupSegment=false;visit.TrafficMovePending=false;
            reason=ready.Reason;return true;
        }
        private bool ProgressPaidPickupRecovery(Visit visit,out string reason)
        {
            reason="原付费恢复身份已失效 / Stale paid recovery";var paid=visit.PaidPickup;
            if(paid==null || !paid.RecoveryPending || paid.Failed || !CanIntegratedProgress)return false;
            if(visit.State!=CustomerVisitState.MovingToPickup || !PaidRecoveryIdentityCurrent(visit))
            {BlockVisit(visit,CustomerVisitState.MovingToPickup,reason);return false;}
            var coordinator=traffic;var actor=visit.Actor;var admission=visit.Admission;var key=paid.ActiveIntent;var generation=visit.Generation;
            var decision=EvaluatePickupTraffic(actor,key,visit.Assignment.Position,PreservePickupGoals(visit),parking:false);
            if(!CanIntegratedProgress || !ReferenceEquals(traffic,coordinator) || !visits.TryGetValue(visit.Id,out var live) || !ReferenceEquals(live,visit) ||
                !ReferenceEquals(visit.PaidPickup,paid) || !ReferenceEquals(visit.Actor,actor) || !ReferenceEquals(visit.Admission,admission) ||
                !paid.ActiveIntent.Equals(key) || visit.Generation!=generation || !paid.RecoveryPending || paid.Failed ||
                !ReferenceEquals(coordinator.GetDecision(key),decision))return false;
            reason=decision.Kind+": "+decision.Reason;
            if(decision.FailureDisposition!=TrafficFailureDisposition.Terminal && (decision.Kind==TrafficDecisionKind.DeferredByBudget || decision.Kind==TrafficDecisionKind.WaitingForTraffic || decision.Kind==TrafficDecisionKind.StaleContext))return false;
            if(decision.FailureDisposition==TrafficFailureDisposition.Terminal || decision.Kind!=TrafficDecisionKind.Granted)
            {BlockVisit(visit,CustomerVisitState.MovingToPickup,reason);return false;}
            var primary=decision.ProofRoutes.SingleOrDefault(p=>p.ResourceId=="pickup/"+visit.Id);
            if(primary==null || primary.Waypoints.Count==0 || primary.Waypoints.Count>24)
            {BlockVisit(visit,CustomerVisitState.MovingToPickup,"Paid recovery proof invalid");return false;}
            if(!traffic.TryCommit(decision))return false;
            paid.RecoveryPending=false;
            SelectCustomerTraffic(visit,decision,primary,CustomerVisitState.MovingToPickup,visit.FinalTarget);
            visit.Route=new Queue<Vector3>(primary.Waypoints);RememberOwnedRoute(visit,CustomerVisitState.MovingToPickup,primary);
            SendRouteSegment(visit,CustomerVisitState.MovingToPickup);return true;
        }
        private bool OwnsPaidPreparation(PaidPickupPreparation a)
            =>a!=null && ReferenceEquals(a.Flow,this) && !disposed && ReferenceEquals(traffic,a.Coordinator) && adapter!=null &&
                a.Intent.Session==trafficSession && a.Intent.Epoch==trafficEpoch && a.Intent.LayoutRevision==adapter.Revision &&
                visits.TryGetValue(a.Visit.Id,out var live) && ReferenceEquals(live,a.Visit) && ReferenceEquals(a.Visit.PaidPickup,a.Task) &&
                ReferenceEquals(a.Visit.Actor,a.Actor) && ReferenceEquals(a.Visit.Admission,a.Admission) &&
                a.Task.Order.Equals(a.Order) && a.Task.FirstFrame==a.FirstFrame && a.Task.Intent.Equals(a.Intent) && a.Task.ActiveIntent.Equals(a.Intent);
        private bool PaidPreparationReferencesCurrent(PaidPickupPreparation a)
            =>OwnsPaidPreparation(a) && CanIntegratedProgress && a.Task.Accepted && !a.Task.Transferred && !a.Task.Failed && !a.Task.RecoveryUsed && !a.Task.RecoveryPending &&
                ReferenceEquals(world,a.World) && ReferenceEquals(adapter,a.Adapter) && ReferenceEquals(Capacity,a.Capacity) && ReferenceEquals(queue,a.Counter) &&
                ReferenceEquals(a.Task.Preparation,a) && ReferenceEquals(a.Task.Parking,a.Parking) && ReferenceEquals(a.Visit.PickupParking,a.Parking) &&
                ReferenceEquals(a.Parking.Source,a.ParkingSource) && a.Visit.State==CustomerVisitState.Queued && a.Visit.Generation==a.Generation &&
                a.Visit.TrafficPhaseSequence==a.PhaseSequence && ReferenceEquals(a.Visit.TrafficSelection,a.Selection) &&
                ReferenceEquals(a.Visit.PendingTraffic,a.Pending) && ReferenceEquals(a.Visit.IssuedTraffic,a.Issued) && ReferenceEquals(a.Visit.TrafficMove,a.Movement) &&
                a.Visit.CounterArrived && !a.Visit.QueueLeft && !a.Visit.PickupInTransit && !a.Visit.PickupArrived && !a.Visit.HasCollected &&
                a.Visit.RequestId==0 && !a.Visit.TrafficMovePending && a.Visit.PendingCounter==null && a.Visit.PendingCounterAdvance==null &&
                a.Actor!=null && TrafficActorReady(a.Actor) && a.Actor.transform.position==a.Start && a.Actor.Settings.AgentRadius==a.Radius &&
                a.Actor.Settings.CollisionSkin==a.Skin && a.Actor.Settings.Epsilon==a.Epsilon && !a.Preview.Consumed &&
                a.Preview.BelongsTo(a.Parking.Owner) && a.Preview.Kind==PickupQueueService.MutationKind.Transfer && a.Preview.Id==a.Visit.Id && a.Preview.Assignment.Position==a.Target;
        private bool PaidPreparationCurrent(PaidPickupPreparation a)
        {
            try
            {
                if(!PaidPreparationReferencesCurrent(a) || !PickupParkingIdentityCurrent(a.Visit) ||
                    !a.Counter.TryCaptureAssignment(a.Visit.Id,out var counter) || !((ITrafficClaimCheckpoint)a.CounterClaim).IsSameCurrentClaim(counter) ||
                    !SameAssignment(counter.Assignment,a.Visit.Assignment) || counter.Assignment.SlotIndex!=0 || !counter.Assignment.Arrived ||
                    a.Counter.Snapshot.Count==0 || a.Counter.Snapshot[0].VisitId!=a.Visit.Id ||
                    !a.Parking.Owner.TryCaptureReservation(a.Visit.Id,out var pickup) || !((ITrafficClaimCheckpoint)a.Parking.Claim).IsSameCurrentClaim(pickup) ||
                    pickup.Assignment.Position!=a.Target || !SameAssignment(pickup.Assignment,a.Parking.Claim.Assignment))return false;
                // Recheck references and the exact fresh captures after both interface calls.
                // 接口回调后复核同一次任务；实际Owner变化不能借原seal重新签名。
                return PaidPreparationReferencesCurrent(a) && counter.CapturedOwnerVersion==a.Counter.Version && pickup.CapturedOwnerVersion==a.Parking.Owner.Version &&
                    Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,a.Total) && r.OwnerId==a.Visit.Id && r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied);
            }
            catch(Exception){return false;}
        }
        private PaidPickupPreparation CapturePaidPreparation(Visit visit,PaidPickupTask paid,PickupQueueService.PreparedMutation preview)
        {
            var coordinator=traffic;var counter=queue;var actor=visit.Actor;var admission=visit.Admission;var parking=paid.Parking;
            var selection=visit.TrafficSelection;var generation=visit.Generation;var key=paid.ActiveIntent;var snapshot=trafficSnapshot;
            bool Current()=>CanIntegratedProgress && ReferenceEquals(traffic,coordinator) && ReferenceEquals(queue,counter) &&
                visits.TryGetValue(visit.Id,out var live) && ReferenceEquals(live,visit) && ReferenceEquals(visit.PaidPickup,paid) &&
                ReferenceEquals(visit.Actor,actor) && ReferenceEquals(visit.Admission,admission) && ReferenceEquals(paid.Parking,parking) &&
                ReferenceEquals(visit.PickupParking,parking) && ReferenceEquals(visit.TrafficSelection,selection) && visit.Generation==generation &&
                paid.ActiveIntent.Equals(key) && paid.Preparation==null && !paid.RecoveryUsed && !paid.RecoveryPending && !paid.Failed && !paid.Transferred &&
                ReferenceEquals(snapshot,trafficSnapshot) && !coordinator.ContainsIntent(key);
            if(!Current() || !paid.ActiveIntent.Equals(paid.Intent) || counter==null || !counter.TryCaptureAssignment(visit.Id,out var claim) || !claim.IsCurrent ||
                selection?.CounterEntry==null || !ReferenceEquals(selection.Counter,counter) || !selection.CounterEntry.IsSameReservation(claim) ||
                !SameAssignment(selection.CounterEntry.Assignment,claim.Assignment) || preview==null || !preview.IsCurrent ||
                !preview.BelongsTo(visit.PickupOwner) || preview.Kind!=PickupQueueService.MutationKind.Transfer || preview.Id!=visit.Id)return null;
            var totals=Capacity.GetReservations().Where(r=>r.OwnerId==visit.Id && r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied).ToArray();
            if(totals.Length!=1 || !Current() || !preview.IsCurrent)return null;
            var a=new PaidPickupPreparation(this,visit,paid,claim,preview,totals[0].Token);paid.Preparation=a;return a;
        }
        private TrafficRequest PaidPreparationRequest(PaidPickupPreparation a,TrafficWorldSnapshot snapshot)
            =>new TrafficRequest(a.Intent,a.FirstFrame,snapshot,a.Start,a.Target,a.Radius,"pickup/"+a.Visit.Id,
                requirements:PreservePickupGoals(a.Visit),safetyMargin:a.Skin+a.Epsilon,planPrimaryRoute:true);
        private void InvalidatePaidPreparation(PaidPickupPreparation a)
        {
            if(!OwnsPaidPreparation(a) || !CanIntegratedProgress || a.Task.Transferred || a.Task.Failed || a.Task.RecoveryUsed)return;
            if(a.Source!=null && a.Coordinator.ContainsIntent(a.Intent))
            {
                var decision=a.Coordinator.GetDecision(a.Intent);
                if(decision.FailureDisposition!=TrafficFailureDisposition.Terminal && decision.AcquiredSequence==0 && !a.Coordinator.HasActiveGrant(a.Intent))a.Coordinator.Retire(a.Intent);
            }
            if(!OwnsPaidPreparation(a) || !CanIntegratedProgress || a.Task.Transferred || a.Task.Failed || a.Task.RecoveryUsed || a.Task.RecoveryPending)return;
            a.Task.Failed=true;BlockVisit(a.Visit,CustomerVisitState.MovingToPickup,"原付费准备身份已失效 / Original paid preparation changed");
        }
        private void RefreshPaidPreparation(PaidPickupPreparation a,TrafficWorldSnapshot snapshot)
        {
            if(a?.Source==null || !OwnsPaidPreparation(a) || !CanIntegratedProgress || a.Task.Transferred || a.Task.Failed || a.Task.RecoveryUsed || a.Task.RecoveryPending ||
                !ReferenceEquals(snapshot,trafficSnapshot) || !a.Coordinator.ContainsIntent(a.Intent))return;
            var expected=a.Coordinator.GetDecision(a.Intent);
            // Terminal is consumed by the existing task step, preserving its explicit one-recovery path.
            if(expected.FailureDisposition!=TrafficFailureDisposition.None || expected.AcquiredSequence!=0 ||
                expected.Kind!=TrafficDecisionKind.DeferredByBudget && expected.Kind!=TrafficDecisionKind.WaitingForTraffic)return;
            if(!PaidPreparationCurrent(a)){InvalidatePaidPreparation(a);return;}
            var request=PaidPreparationRequest(a,snapshot);
            if(!PaidPreparationCurrent(a) || !ReferenceEquals(snapshot,trafficSnapshot) || !ReferenceEquals(a.Coordinator.GetDecision(a.Intent),expected))return;
            // Exact-shape refresh stays strict. A changed external goal set requires same-producer full reproof.
            // 只允许原paid primary/原claim；不新建intent，不重置年龄/累计query或finite等待。
            bool Current()=>PaidPreparationCurrent(a) && ReferenceEquals(snapshot,trafficSnapshot) &&
                ReferenceEquals(a.Coordinator.GetDecision(a.Intent),expected);
            var refreshed=a.Coordinator.TryRefreshQueuedPreparation(expected,request,out var next);
            if(!refreshed && Current())refreshed=a.Coordinator.TryResubmitQueuedPaidPickupPreparation(expected,request,Current,out next);
            if(refreshed && PaidPreparationCurrent(a) && ReferenceEquals(snapshot,trafficSnapshot) &&
                ReferenceEquals(a.Coordinator.GetDecision(a.Intent),next))a.Source=next;
        }
        private bool TryTrafficTransfer(Visit visit,out string reason)
        {
            reason="付款转移许可已失效 / Stale paid transfer";
            var paid=visit.PaidPickup;
            if(paid==null || !paid.Accepted || paid.Transferred || paid.Failed || visit.State==CustomerVisitState.Blocked || !CanIntegratedProgress)return false;
            var coordinator=traffic;var actor=visit.Actor;var admission=visit.Admission;var owner=visit.PickupOwner;
            var currentWorld=world;var currentAdapter=adapter;var counter=queue;var capacity=Capacity;
            var key=paid.ActiveIntent;var order=paid.Order;var first=paid.FirstFrame;var generation=visit.Generation;
            bool Current()=>CanIntegratedProgress && !disposed && ReferenceEquals(traffic,coordinator) && ReferenceEquals(world,currentWorld) &&
                ReferenceEquals(adapter,currentAdapter) && ReferenceEquals(queue,counter) && ReferenceEquals(Capacity,capacity) &&
                key.Session==trafficSession && key.Epoch==trafficEpoch && key.LayoutRevision==adapter.Revision &&
                visits.TryGetValue(visit.Id,out var live) && ReferenceEquals(live,visit) && ReferenceEquals(visit.PaidPickup,paid) &&
                ReferenceEquals(visit.Actor,actor) && ReferenceEquals(visit.Admission,admission) && ReferenceEquals(visit.PickupOwner,owner) &&
                paid.ActiveIntent.Equals(key) && paid.Order.Equals(order) && paid.FirstFrame==first && visit.Generation==generation && !paid.Failed;
            if(!Current())return false;
            if(paid.RecoveryPending && !PaidRecoveryIdentityCurrent(visit))
            {if(Current()){paid.Failed=true;BlockVisit(visit,CustomerVisitState.MovingToPickup,reason);}return false;}
            if(!Current())return false;
            var snapshot=RefreshTrafficSnapshot();
            if(!Current())return false;
            if(!PickupParkingCurrent(visit,snapshot) ||
                !owner.TryPrepareMutation(PickupQueueService.MutationKind.Transfer,visit.Id,owner.Version,out var prepared))
            {if(Current()){paid.Failed=true;BlockVisit(visit,CustomerVisitState.MovingToPickup,reason);}return false;}
            bool PreparedCurrent()=>Current() && !paid.Transferred && ReferenceEquals(snapshot,trafficSnapshot) && prepared.IsCurrent && prepared.BelongsTo(owner);
            if(!PreparedCurrent())return false;
            var prior=coordinator.GetDecision(key);
            if(coordinator.ContainsIntent(key) && (prior.FailureDisposition==TrafficFailureDisposition.Terminal ||
                prior.RequestSequence>0 && prior.Kind!=TrafficDecisionKind.Granted && prior.Kind!=TrafficDecisionKind.DeferredByBudget &&
                prior.Kind!=TrafficDecisionKind.WaitingForTraffic && prior.Kind!=TrafficDecisionKind.StaleContext))
            {
                reason=prior.Kind+": "+prior.Reason;
                var recoverable=CapturePaidPreparationRecovery(visit,prior);
                if(Current())BlockVisit(visit,CustomerVisitState.MovingToPickup,reason,preservePaidRecovery:recoverable);return false;
            }
            TrafficDecision decision;
            if(paid.RecoveryUsed)
            {
                decision=EvaluatePickupTraffic(actor,key,prepared.Assignment.Position,PreservePickupGoals(visit),parking:false);
                snapshot=trafficSnapshot; // Existing explicit recovery evaluates its own current snapshot; never capture a replacement seal.
            }
            else
            {
                var a=paid.Preparation??CapturePaidPreparation(visit,paid,prepared);
                if(!Current())return false;
                if(a==null)
                {paid.Failed=true;BlockVisit(visit,CustomerVisitState.MovingToPickup,"原付费准备未捕获 / Original paid preparation missing");return false;}
                if(!PaidPreparationCurrent(a) || !PreparedCurrent() || prepared.Assignment.Position!=a.Target)
                {InvalidatePaidPreparation(a);return false;}
                if(a.Source==null)
                {
                    if(coordinator.ContainsIntent(key)){InvalidatePaidPreparation(a);return false;} // Never backfill a registered source.
                    var request=PaidPreparationRequest(a,snapshot);
                    if(!PaidPreparationCurrent(a) || !PreparedCurrent())return false;
                    decision=coordinator.EvaluateMovement(request);
                    if(!PaidPreparationCurrent(a) || !PreparedCurrent() || !ReferenceEquals(coordinator.GetDecision(key),decision))return false;
                    a.Source=decision;
                }
                else
                {
                    if(!coordinator.ContainsIntent(key)){InvalidatePaidPreparation(a);return false;}
                    decision=coordinator.GetDecision(key);
                }
            }
            if(!Current() || !ReferenceEquals(coordinator.GetDecision(key),decision))return false;
            if(decision.FailureDisposition!=TrafficFailureDisposition.Terminal && (decision.Kind==TrafficDecisionKind.DeferredByBudget || decision.Kind==TrafficDecisionKind.WaitingForTraffic || decision.Kind==TrafficDecisionKind.StaleContext))
            {reason=decision.Kind+": "+decision.Reason;return false;}
            if(decision.FailureDisposition==TrafficFailureDisposition.Terminal || decision.Kind!=TrafficDecisionKind.Granted)
            {
                reason=decision.Kind+": "+decision.Reason;
                var recoverable=CapturePaidPreparationRecovery(visit,decision);
                if(Current())BlockVisit(visit,CustomerVisitState.MovingToPickup,reason,preservePaidRecovery:recoverable);return false;
            }
            var primary=decision.ProofRoutes.Single(p=>p.ResourceId=="pickup/"+visit.Id);
            if(primary.Waypoints.Count==0 || primary.Waypoints.Count>24)
            {paid.Failed=true;BlockVisit(visit,CustomerVisitState.MovingToPickup,"Paid movement proof invalid");return false;}
            if(!CanCommitCustomerCanonicalSuccessor(visit,coordinator,decision,CustomerVisitState.MovingToPickup,out reason) ||
                !PreparedCurrent() || !ReferenceEquals(coordinator.GetDecision(key),decision))return false;
            var committed=coordinator.TryCommit(decision,prepared,()=>
            {
                if(!Current())return;
                paid.Transferred=true;paid.RecoveryPending=false;
                visit.CounterPosition=actor.transform.position;visit.PickupInTransit=true;visit.Assignment=prepared.Assignment;
                ClearPickupCorridor(visit);ObservePickup("transfer.published",visit,owner,visit.Assignment);
                if(!Current())return;
                SetRouteContext(visit,CustomerVisitState.MovingToPickup,PickupWaitTarget(visit.Assignment,owner));
                SelectCustomerTraffic(visit,decision,primary,CustomerVisitState.MovingToPickup,visit.FinalTarget);
                visit.Route=new Queue<Vector3>(primary.Waypoints);RememberOwnedRoute(visit,CustomerVisitState.MovingToPickup,primary);
                SendRouteSegment(visit,CustomerVisitState.MovingToPickup);
            });
            reason=committed?"":reason;return committed;
        }
        // Check after all producer callbacks and immediately before the callback-free canonical Apply.
        // 只阻止当前原handoff失效后的业务写入；不缓存授权、不重建预算，也不阻塞替换后的Visit。
        private bool CanCommitCustomerCanonicalSuccessor(Visit visit,ServiceTrafficCoordinator coordinator,
            TrafficDecision successor,CustomerVisitState movementPhase,out string reason)
        {
            reason="";if(visit==null)return false;
            var pending=visit.PendingTraffic;if(pending==null)return true;
            var actor=visit.Actor;var admission=visit.Admission;
            bool CurrentVisit()=>CanIntegratedProgress && ReferenceEquals(traffic,coordinator) &&
                visits.TryGetValue(visit.Id,out var live) && ReferenceEquals(live,visit) &&
                ReferenceEquals(visit.PendingTraffic,pending) && ReferenceEquals(visit.Actor,actor) && ReferenceEquals(visit.Admission,admission);
            if(coordinator==null || successor==null || successor.Intent.ActorId!=visit.Id || !CurrentVisit() ||
                !ReferenceEquals(coordinator.GetDecision(successor.Intent),successor))return false;
            if(!pending.Marked || !ReferenceEquals(pending.Flow,this) || !ReferenceEquals(pending.Visit,visit) ||
                !ReferenceEquals(pending.Coordinator,coordinator) || !ReferenceEquals(pending.Actor,actor) ||
                !ReferenceEquals(pending.Admission,admission) || !TrafficReceiptCurrent(pending))return false;
            if(coordinator.IsCurrentPhaseHandoff(pending.Handle,pending.Source,actor))return true;
            reason="Original predecessor handoff is no longer valid / 原阶段交接已失效";
            var key=successor.Intent;
            if(!key.Equals(pending.Intent) && successor.AcquiredSequence==0 && !coordinator.HasActiveGrant(key) &&
                !coordinator.TryGetCommittedJourney(key,out _))
            {
                trafficCandidates.TryGetValue(key,out var cursor);
                var reservation=visit.PendingReservationAnchor;var advance=visit.PendingCounterAdvance;var returning=visit.ReturnPreparation;
                coordinator.Retire(key);
                if(!CurrentVisit())return false;
                if(cursor!=null && trafficCandidates.TryGetValue(key,out var current) && ReferenceEquals(current,cursor))trafficCandidates.Remove(key);
                if(reservation!=null && reservation.Key.Equals(key) && ReferenceEquals(visit.PendingReservationAnchor,reservation))visit.PendingReservationAnchor=null;
                if(advance!=null && advance.Key.Equals(key) && ReferenceEquals(visit.PendingCounterAdvance,advance))visit.PendingCounterAdvance=null;
                if(returning!=null && returning.Key.Equals(key) && ReferenceEquals(visit.ReturnPreparation,returning))visit.ReturnPreparation=null;
            }
            if(CurrentVisit())BlockVisit(visit,movementPhase,reason);
            return false;
        }
        private bool TryTrafficPickupMove(Visit visit,PickupQueueService.MutationKind operation,Vector3 target,
            string phaseName,CustomerVisitState movementPhase,string observation,out string reason,
            IEnumerable<TrafficRouteRequirement> extraProofs=null,TrafficCandidateCursor cursor=null,int candidateIndex=0,
            TrafficCandidateCursor deliverySource=null)
        {
            reason="等待Pick-up目标许可 / Preparing pickup target";
            var release=cursor?.CollectionRelease;
            PickupQueueService.PreparedMutation prepared;
            if(release!=null)
            {
                if(StopCollectionReleaseIfTerminal(release,out var stoppedReason)){reason=stoppedReason;return false;}
                if(operation!=PickupQueueService.MutationKind.CollectionLease || !ReferenceEquals(release.Visit,visit) ||
                    candidateIndex!=release.CandidateIndex || target!=release.Target || !release.Lease.HasRetiredLifetime ||
                    !isActiveAndEnabled || !CollectionReleaseIdentityCurrent(release))return false;
                prepared=release.CurrentPreview;
                if(!CollectionReleasePreviewMatches(release,prepared) &&
                    !visit.PickupOwner.TryPrepareMutation(operation,visit.Id,visit.PickupOwner.Version,out prepared,target))return false;
                if(!CollectionReleasePreviewMatches(release,prepared))return false;
                release.CurrentPreview=prepared;
            }
            else if(!visit.PickupOwner.TryPrepareMutation(operation,visit.Id,visit.PickupOwner.Version,out prepared,target))return false;
            target=prepared.Assignment.Position;
            var key=TrafficKey(visit.Id,phaseName,visit.Id+"/"+visit.TrafficPhaseSequence);
            RefreshTrafficSnapshot();var proofs=PreservePickupGoals(visit).ToList();
            var fixedProofs=(extraProofs??Enumerable.Empty<TrafficRouteRequirement>()).ToList();
            if(operation==PickupQueueService.MutationKind.CollectionLease || operation==PickupQueueService.MutationKind.YieldedCollectionLease)
                fixedProofs.Add(new TrafficRouteRequirement("collection-exit-capability",target,exit.position,RouteClearance(visit.Actor.Settings),
                    TrafficObstacles(visit.Id),avoidProposedParking:false));
            proofs.AddRange(fixedProofs);
            if(release==null)CapturePickupFamily(visit,key,prepared,cursor,candidateIndex,proofs,deliverySource,fixedProofs);
#if UNITY_EDITOR
            // Default off; only observe the original actual preview before its first Submit.
            // 只读原分支，不补签身份，也不查询、刷新、提交或唤醒。
            if(CorridorObserver!=null && operation!=PickupQueueService.MutationKind.Advance && !traffic.ContainsIntent(key))
                ObserveCorridor("traffic.pickup-family-preflight",visit,target:target,
                    result:"operation="+operation+";phase="+phaseName+";frame="+Time.frameCount+";candidate="+candidateIndex+";key="+key);
#endif
            var family=operation!=PickupQueueService.MutationKind.Advance;
            var preparation=family?FindPickupPreparation(visit,key):null;
            TrafficDecision decision;
            if(release!=null)
            {
                if(!release.Key.Equals(key) || !PrepareCollectionRelease(release,trafficSnapshot,out decision))return false;
                prepared=release.CurrentPreview;
            }
            else if(!family)decision=EvaluatePickupTraffic(visit.Actor,key,target,proofs,cursor:cursor,candidateIndex:candidateIndex);
            else
            {
                if(!PickupFamilyPreparationCurrent(preparation) || preparation.Operation!=operation ||
                    !ReferenceEquals(preparation.Cursor,cursor) || !ReferenceEquals(preparation.DeliveryCursor,deliverySource))return false;
                var submitted=traffic.ContainsIntent(key);var prior=submitted?traffic.GetDecision(key):null;
                var alternate=prior?.FailureDisposition==TrafficFailureDisposition.AlternateAllowed;
                if(submitted && !alternate)
                {
                    if(candidateIndex!=preparation.CandidateIndex || target!=preparation.Target ||
                        !PreparePickupFamily(preparation,trafficSnapshot,out decision,prepared))return false;
                }
                else
                {
                    if(alternate && (cursor==null || candidateIndex==preparation.CandidateIndex || prior.AcquiredSequence!=0))return false;
                    decision=EvaluatePickupTraffic(visit.Actor,key,target,proofs,cursor:cursor,candidateIndex:candidateIndex);
                    if(alternate)
                    {
                        // Derive only after the exact original Core Alternate consumed this declared candidate.
                        // 有原seal才可沿有限候选advance；不是提交后补签缺失来源或复活Terminal。
                        if(decision.FailureDisposition!=TrafficFailureDisposition.None || decision.Kind!=TrafficDecisionKind.DeferredByBudget ||
                            cursor.CurrentIndex!=candidateIndex || !ReferenceEquals(cursor.Source,decision) || ReferenceEquals(prior,decision))return false;
                        var derived=new PickupFamilyPreparation(this,visit,key,prepared,preparation.Claim,preparation.Total,
                            cursor,candidateIndex,proofs,deliverySource,fixedProofs);
                        if(!isActiveAndEnabled || !PickupFamilyIdentityCurrent(derived))return false;
                        cursor.PickupPreparation=preparation=derived;
                    }
                }
                prepared=preparation.CurrentPreview;
            }
            if(decision.Kind!=TrafficDecisionKind.Granted){reason=decision.Kind+": "+decision.Reason;return false;}
            if(release!=null)
            {
                if(!isActiveAndEnabled || !CollectionReleaseIdentityCurrent(release) || !release.Lease.HasRetiredLifetime ||
                    !CollectionReleasePreviewMatches(release,prepared) || !ReferenceEquals(traffic.GetDecision(key),decision))return false;
            }
            else if(family && (!PickupFamilyPreparationCurrent(preparation) || !PickupFamilyPreviewMatches(preparation,prepared) ||
                !ReferenceEquals(traffic.GetDecision(key),decision)))return false;
            var primary=decision.ProofRoutes.Single(p=>p.ResourceId=="pickup/"+visit.Id);
            if(primary.Waypoints.Count==0 || primary.Waypoints.Count>24)return false;
            if(family && primary.Waypoints.Last()!=(release==null?preparation.Target:release.Target))return false;
            if(cursor!=null && (!prepared.IsCurrent || !prepared.BelongsTo(visit.PickupOwner) || prepared.Kind!=operation ||
                prepared.Id!=visit.Id || prepared.Assignment.VisitId!=visit.Id || primary.Waypoints.Last()!=prepared.Assignment.Position))return false;
            if(!CanCommitCustomerCanonicalSuccessor(visit,traffic,decision,movementPhase,out reason))return false;
            var committed=traffic.TryCommit(decision,prepared,()=>
            {
                visit.TrafficPhaseSequence++;visit.Assignment=prepared.Assignment;visit.ServiceArrived=false;
                visit.DeliveryYieldPhase=operation==PickupQueueService.MutationKind.BeginYield?1:operation==PickupQueueService.MutationKind.ReturnYield?3:0;
                if(operation==PickupQueueService.MutationKind.BeginYield)
                    visit.YieldDeliverySource=FindPickupPreparation(visit,key)?.DeliveryCursor;
                ObservePickup(observation,visit,visit.PickupOwner,visit.Assignment);
                var destination=movementPhase==CustomerVisitState.MovingToPickupService
                    ?pickupSides.First(s=>s.Position==target):PickupWaitTarget(visit.Assignment,visit.PickupOwner);
                SetRouteContext(visit,movementPhase,destination);visit.Route=new Queue<Vector3>(primary.Waypoints);
                SelectCustomerTraffic(visit,decision,primary,movementPhase,destination);
                RememberOwnedRoute(visit,movementPhase,primary);
                if(movementPhase==CustomerVisitState.MovingToPickupService)ObservePickup("collection.route-selected",visit,visit.PickupOwner,visit.Assignment);
                SendRouteSegment(visit,movementPhase);
            });
            reason=committed?"":"目标提交已失效 / Stale target commit";return committed;
        }
        private bool TryTrafficCollection(Visit visit,out string reason)
        {
            reason="等待可用服务侧 / Waiting for service side";
            if(!TrafficActorReady(visit.Actor))return false;
            var phase=TrafficKey(visit.Id,"pickup-service",visit.Id+"/"+visit.TrafficPhaseSequence);
            var cursor=GetTrafficCursor(phase,visit.Actor,TrafficCandidateRole.Collection,pickupSides);
            if(cursor==null){reason="取杯候选已停止 / Collection candidate stopped";return false;}
            var operation=visit.DeliveryYieldPhase==2?PickupQueueService.MutationKind.YieldedCollectionLease:PickupQueueService.MutationKind.CollectionLease;
            var current=cursor.Source==null?null:traffic.GetDecision(phase);
            if(StopOrdinaryCollectionIfTerminal(cursor.PickupPreparation,current,out reason))return false;
            var candidateFrame=visit.TrafficCandidateFrames.TryGetValue(phase,out var originalFrame)?originalFrame:Time.frameCount;
            if(operation==PickupQueueService.MutationKind.CollectionLease && current==null && cursor.CollectionRelease==null && !traffic.ContainsIntent(phase))
            {
                var free=cursor.Sides.Any(side=>visit.PickupOwner.TryPrepareMutation(operation,visit.Id,visit.PickupOwner.Version,out _,side.Position) &&
                    traffic.IsParkingEndpointAvailable(new TrafficRequest(phase,candidateFrame,trafficSnapshot,cursor.Origin,side.Position,
                        visit.Actor.Settings.AgentRadius,"pickup/"+visit.Id,safetyMargin:visit.Actor.Settings.CollisionSkin+visit.Actor.Settings.Epsilon,planPrimaryRoute:true)));
                if(!free)
                    for(var candidate=0;candidate<cursor.Sides.Count;candidate++)
                        if(!cursor.AttemptedIndices.Contains(candidate) && !cursor.StaticInvalidIndices.Contains(candidate) &&
                            TryCaptureCollectionRelease(visit,cursor,candidate))break;
            }
#if UNITY_EDITOR
            // Default-off original cursor checkpoint, after original prerequisite capture and before first Submit.
            if(CorridorObserver!=null && operation==PickupQueueService.MutationKind.CollectionLease && !traffic.ContainsIntent(phase))
                ObserveCorridor("traffic.collection-prerequisite-preflight",visit,target:pickupDeliveryHistory?.Lease?.Position??cursor.Origin,
                    result:"frame="+candidateFrame+";count="+cursor.Count+";key="+phase);
#endif
            if(cursor.CollectionRelease!=null)
            {
                var release=cursor.CollectionRelease;
                if(StopCollectionReleaseIfTerminal(release,out var stoppedReason)){reason=stoppedReason;return false;}
                if(!CollectionReleaseIdentityCurrent(release))return false;
                var snapshot=RefreshTrafficSnapshot();
                if(cursor.Source==null)
                {
                    if(traffic.ContainsIntent(phase) || !PrepareTrafficCandidate(cursor,CollectionReleaseRequest(release,snapshot),visit.Actor,
                        release.CandidateIndex,null,out var initial))return false;
                    reason=initial.Kind+": "+initial.Reason;return false;
                }
                if(!PrepareCollectionRelease(release,snapshot,out var pending))return false;
                if(pending.Kind!=TrafficDecisionKind.Granted || !release.Lease.HasRetiredLifetime)
                {reason=pending.Kind+": "+pending.Reason;return false;}
                return TryTrafficPickupMove(visit,operation,release.Target,"pickup-service",CustomerVisitState.MovingToPickupService,
                    "collection.published",out reason,cursor:cursor,candidateIndex:release.CandidateIndex);
            }
            // Read the already published snapshot; the original pickup-move path refreshes and proves again.
            // hint 仅影响未提交候选偏好；全忙保留原fallback，绝不刷新/复活原Terminal。
            if(!cursor.TrySelectPreferredBase(phase,visit.Actor,current,AnchorContextCurrent(traffic,phase) && TrafficActorReady(visit.Actor),
                (candidate,side,target)=>visit.PickupOwner.TryPrepareMutation(operation,visit.Id,visit.PickupOwner.Version,out _,target),
                target=>traffic.IsParkingEndpointAvailable(new TrafficRequest(phase,candidateFrame,trafficSnapshot,cursor.Origin,target,
                    visit.Actor.Settings.AgentRadius,"pickup/"+visit.Id,
                    safetyMargin:visit.Actor.Settings.CollisionSkin+visit.Actor.Settings.Epsilon,planPrimaryRoute:true)),out var index))
            {if(current!=null)reason=current.Kind+": "+current.Reason;return false;}
            return TryTrafficPickupMove(visit,operation,cursor.Sides[index].Position,"pickup-service",CustomerVisitState.MovingToPickupService,
                "collection.published",out reason,cursor:cursor,candidateIndex:index);
        }
        private void AdvanceTrafficPickupQueue()
        {
            foreach(var assignment in pickupQueue.Snapshot)
            {
                if(!visits.TryGetValue(assignment.VisitId,out var visit) || visit.Actor==null || visit.State==CustomerVisitState.Blocked ||
                    !pickupQueue.TryPrepareMutation(PickupQueueService.MutationKind.Advance,visit.Id,pickupQueue.Version,out var prepared))continue;
                TryTrafficPickupMove(visit,PickupQueueService.MutationKind.Advance,prepared.Assignment.Position,"pickup-advance",
                    CustomerVisitState.PickupAdvancing,"advance.published",out _);return;
            }
        }
        private DeliveryYieldContinuation YieldContinuation(Visit visit)
        {
            var plan=visit?.YieldDeliverySource?.OriginalDeliveryPlan;
            return plan!=null && ReferenceEquals(plan.Visit,visit)?plan.Continuation:null;
        }
        private bool YieldPlanOwnerIdentityCurrent(DeliveryYieldPlan plan)
        {
            if(plan==null || disposed || !ReferenceEquals(plan.Coordinator,traffic) || adapter==null || world==null || Capacity==null ||
                plan.Parent.Key.Session!=trafficSession || plan.Parent.Key.Epoch!=trafficEpoch || plan.Parent.Key.LayoutRevision!=adapter.Revision ||
                !ReferenceEquals(plan.Parent.OriginalDeliveryPlan,plan) || !ReferenceEquals(plan.Parent.DeliveryContext,plan.Context) ||
                !visits.TryGetValue(plan.Visit.Id,out var visit) || !ReferenceEquals(visit,plan.Visit) ||
                !ReferenceEquals(visit.Actor,plan.Actor) || plan.Actor==null || !plan.Actor.isActiveAndEnabled ||
                !world.RegisteredActors.Any(actor=>ReferenceEquals(actor,plan.Actor)) || !ReferenceEquals(visit.Admission,plan.Admission) ||
                !ReferenceEquals(pickupQueue,plan.Owner) || !ReferenceEquals(visit.PickupOwner,plan.Owner) ||
                !ReferenceEquals(visit.YieldDeliverySource,plan.Parent) || !visit.PickupAssigned || visit.HasCollected || visit.PickupDeparted ||
                visit.DeliveryYieldPhase!=1 || visit.TrafficPhaseSequence!=plan.PhaseSequence+1 ||
                plan.Actor.Settings.AgentRadius!=plan.Radius || plan.Actor.Settings.CollisionSkin!=plan.Skin || plan.Actor.Settings.Epsilon!=plan.Epsilon)return false;
            var paid=visit.PaidPickup;
            return paid!=null && paid.Accepted && paid.Transferred && !paid.Failed && ReferenceEquals(paid.Parking.Actor,plan.Actor) &&
                ReferenceEquals(paid.Parking.Admission,plan.Admission) && ReferenceEquals(paid.Parking.Coordinator,traffic) &&
                Capacity.GetReservations().Any(token=>ReferenceEquals(token.Token,plan.Total) && token.OwnerId==visit.Id &&
                    token.Kind==CapacityKind.TotalCustomers && token.State==ReservationState.Occupied);
        }
        private bool YieldContinuationCheckpointCurrent(DeliveryYieldContinuation continuation)
        {
            if(continuation==null || continuation.Failed || continuation.Used && !continuation.Pending ||
                !YieldPlanOwnerIdentityCurrent(continuation.Plan))return false;
            var plan=continuation.Plan;var visit=plan.Visit;var parent=traffic.GetDecision(plan.Parent.Key);
            if(!CanIntegratedProgress || !plan.Context.IsCurrent || parent.FailureDisposition==TrafficFailureDisposition.Terminal ||
                parent.Kind==TrafficDecisionKind.Cancelled || !ReferenceEquals(visit.PaidPickup,continuation.Paid) ||
                !visit.PaidPickup.Order.Equals(continuation.Order) || plan.Actor.transform.position!=continuation.StopPose ||
                visit.State!=(continuation.Used?CustomerVisitState.PickupAdvancing:CustomerVisitState.Blocked) ||
                visit.Generation!=(continuation.Used?plan.YieldGeneration:continuation.HeldGeneration) ||
                visit.RequestId!=0 || visit.TrafficMove!=null || visit.IssuedTraffic!=null || visit.PendingTraffic!=null ||
                visit.TrafficPhaseSequence!=continuation.PhaseSequence || visit.Segments!=continuation.Segments || visit.Replans!=continuation.Replans ||
                visit.Segments>=32 || visit.Replans>3 || continuation.Claim?.IsCurrent!=true ||
                !plan.Owner.TryCaptureReservation(visit.Id,out var current) || !continuation.Claim.IsSameCurrentClaim(current) ||
                !SameAssignment(continuation.Assignment,current.Assignment) || current.Assignment.Arrived)return false;
            var actual=Capacity.GetReservations().Where(token=>token.OwnerId==visit.Id).ToArray();
            return actual.Length==continuation.Tokens.Length && continuation.Tokens.All(saved=>actual.Any(token=>
                ReferenceEquals(token.Token,saved.Token) && token.Kind==saved.Kind && token.State==saved.State && token.OwnerId==saved.OwnerId));
        }
        private DeliveryYieldContinuation CaptureYieldOwnerHold(Visit visit)
        {
            var plan=visit.YieldDeliverySource?.OriginalDeliveryPlan;var move=visit.TrafficMove;var issued=visit.IssuedTraffic;
            // OnDisable is already inactive and may be paused: identity-only capture gives no progress permission.
            if(isActiveAndEnabled || !integratedMode || !YieldPlanOwnerIdentityCurrent(plan) || plan.Continuation!=null ||
                visit.State!=CustomerVisitState.PickupAdvancing || visit.Generation!=plan.YieldGeneration || visit.RequestId<=0 ||
                move==null || issued==null || !ReferenceEquals(move.Coordinator,traffic) || !move.Intent.Equals(plan.Child.Key) ||
                !ReferenceEquals(move.Source,plan.ChildSource) || !ReferenceEquals(issued.Source,plan.ChildSource) ||
                !issued.Intent.Equals(plan.Child.Key) || issued.NativeGeneration!=visit.Generation || move.NativeGeneration!=visit.Generation ||
                visit.PendingTraffic!=null || plan.YieldClaim?.IsCurrent!=true || !plan.Owner.TryCaptureReservation(visit.Id,out var claim) ||
                !plan.YieldClaim.IsSameCurrentClaim(claim) || !SameAssignment(plan.YieldAssignment,claim.Assignment) || claim.Assignment.Arrived)return null;
            var tokens=Capacity.GetReservations().Where(token=>token.OwnerId==visit.Id).ToArray();
            if(tokens.Length!=3 || !tokens.Any(token=>token.Kind==CapacityKind.PickUp && ReferenceEquals(token.Token,visit.PaidPickup.Parking.Token) &&
                token.State==ReservationState.Occupied))return null;
            RefreshTrafficSnapshot();
            if(!traffic.TryRetireYieldForOwnerHold(traffic.GetDecision(plan.Parent.Key),plan.ChildSource,plan.Actor,
                new TrafficNativeStamp(visit.RequestId,visit.Generation),plan.Actor.transform.position,out var receipt))return null;
            var continuation=new DeliveryYieldContinuation(plan,claim,tokens,receipt);plan.Continuation=continuation;return continuation;
        }
        private void DiscardYieldContinuation(DeliveryYieldContinuation continuation)
        {
            if(continuation==null || continuation.Failed)return;
            continuation.Plan.Coordinator.DiscardRecovery(continuation.Receipt);continuation.Receipt=null;
            if(continuation.Used)continuation.Plan.Coordinator.Retire(continuation.Intent);
            continuation.Pending=false;continuation.Failed=true;
        }
        private void FailYieldContinuation(DeliveryYieldContinuation continuation,string reason)
        {
            DiscardYieldContinuation(continuation);var visit=continuation.Plan.Visit;
            if(visits.TryGetValue(visit.Id,out var current) && ReferenceEquals(current,visit) && visit.DeliveryYieldPhase==1)
            {HoldVisit(visit);visit.ResumeState=CustomerVisitState.PickupAdvancing;visit.Failure=reason;}
            PickupDeliveryFailure=reason;
        }
        // CLC's explicit Owner.Resume supplies the actual paid Order; ordinary queue rebuilding never calls this seam.
        internal bool TryResumeHeldDeliveryYield(string visitId,CafeOrderKey actualPaidKey,out string reason)
        {
            reason="原让路生命周期不能恢复 / Held yield continuation unavailable";
            if(!CanIntegratedProgress || !visits.TryGetValue(visitId??"",out var visit))return false;
            var continuation=YieldContinuation(visit);
            if(continuation==null || continuation.Used || continuation.Failed || !continuation.CancellationConfirmed ||
                !continuation.Order.Equals(actualPaidKey))return false;
            if(!YieldContinuationCheckpointCurrent(continuation)){FailYieldContinuation(continuation,reason);return false;}
            var plan=continuation.Plan;var old=plan.Child.Key;
            var key=new TrafficIntentKey(old.Session,old.Epoch,old.ActorId,old.IntentId,Math.Max(nextTrafficPhase,old.Generation)+1,old.LayoutRevision,old.Context);
            var snapshot=RefreshTrafficSnapshot();var request=YieldContinuationRequest(continuation,key,snapshot);
            if(!traffic.TryBeginExplicitRecovery(continuation.Receipt,request,visit.Actor,out var ready))
            {FailYieldContinuation(continuation,reason);return false;}
            nextTrafficPhase=key.Generation;continuation.Receipt=null;continuation.Used=continuation.Pending=true;continuation.Intent=key;
            trafficKeys.Add(visit.Id+"/yield-owner-continuation/"+old.Generation,key);visit.TrafficCandidateFrames.Add(key,plan.FirstFrame);
            visit.Generation++;plan.YieldGeneration=visit.Generation;visit.RequestId=0;visit.State=visit.ResumeState=CustomerVisitState.PickupAdvancing;
            visit.Failure=null;visit.Route=null;visit.TrafficJourney=null;visit.PendingExitSegment=visit.PendingPickupSegment=false;visit.TrafficMovePending=false;
            reason=ready.Reason;return true;
        }
        private TrafficRequest YieldContinuationRequest(DeliveryYieldContinuation continuation,TrafficIntentKey key,TrafficWorldSnapshot snapshot)
        {
            var plan=continuation.Plan;var actor=plan.Actor;var employee=plan.Context.ActorLife as NavigationActor;
            var occupied=snapshot.Parking.Where(fact=>fact.Owner.ActorId!=actor.ActorId && fact.Owner.ActorId!=employee.ActorId).Select(fact=>fact.Position)
                .Concat(snapshot.Bodies.Where(body=>body.Owner.ActorId!=actor.ActorId && body.Owner.ActorId!=employee.ActorId && !body.IndependentlyProgressing).Select(body=>body.Position)).Distinct().ToArray();
            var requirements=PreservePickupGoals(plan.Visit).ToList();
            requirements.Add(new TrafficRouteRequirement("yield-work-out",employee.transform.position,plan.Side.Position,RouteClearance(employee.Settings),occupied));
            requirements.Add(new TrafficRouteRequirement("yield-work-home",plan.Side.Position,deliveryWorkTarget.Position,RouteClearance(employee.Settings),occupied));
            return new TrafficRequest(key,plan.FirstFrame,snapshot,actor.transform.position,continuation.Assignment.Position,plan.Radius,"pickup/"+actor.ActorId,
                requirements:requirements,safetyMargin:plan.Skin+plan.Epsilon,planPrimaryRoute:true);
        }
        private bool ProgressYieldContinuation(DeliveryYieldContinuation continuation)
        {
            if(!CanIntegratedProgress || !continuation.Pending || continuation.Failed)return false;
            if(!YieldContinuationCheckpointCurrent(continuation))
            {FailYieldContinuation(continuation,"原让路恢复身份已失效 / Held yield identity changed");return false;}
            var plan=continuation.Plan;var visit=plan.Visit;var generation=visit.Generation;var snapshot=RefreshTrafficSnapshot();
            var decision=traffic.EvaluateMovement(YieldContinuationRequest(continuation,continuation.Intent,snapshot),plan.Child.CurrentIndex,plan.Child.Count);
            if(!YieldContinuationCheckpointCurrent(continuation) || visit.Generation!=generation || !ReferenceEquals(traffic.GetDecision(continuation.Intent),decision))return false;
            if(decision.FailureDisposition!=TrafficFailureDisposition.Terminal &&
                (decision.Kind==TrafficDecisionKind.DeferredByBudget || decision.Kind==TrafficDecisionKind.WaitingForTraffic || decision.Kind==TrafficDecisionKind.StaleContext))return false;
            if(decision.Kind!=TrafficDecisionKind.Granted || decision.FailureDisposition!=TrafficFailureDisposition.None)
            {FailYieldContinuation(continuation,"原让路恢复已停止 / Yield continuation stopped: "+decision.Kind+": "+decision.Reason);return false;}
            var primary=decision.ProofRoutes.SingleOrDefault(proof=>proof.ResourceId=="pickup/"+visit.Id);
            if(primary==null || primary.Waypoints.Count==0 || primary.Waypoints.Count>24 || primary.Waypoints.Last()!=continuation.Assignment.Position)
            {FailYieldContinuation(continuation,"原让路恢复路径无效 / Yield continuation proof invalid");return false;}
            if(!traffic.TryCommit(decision))return false;
            continuation.Pending=false;continuation.Source=decision;
            SelectCustomerTraffic(visit,decision,primary,CustomerVisitState.PickupAdvancing,visit.FinalTarget);
            visit.Route=new Queue<Vector3>(primary.Waypoints);RememberOwnedRoute(visit,CustomerVisitState.PickupAdvancing,primary);
            SendRouteSegment(visit,CustomerVisitState.PickupAdvancing);return true;
        }
        private void DeclareDeliveryYieldPlan(TrafficCandidateCursor parent)
        {
            if(parent.DeliveryPlanDeclared || parent.Source!=null || traffic.ContainsIntent(parent.Key) ||
                parent.CurrentIndex>=0 || parent.AttemptedIndices.Count!=0 || parent.Sides.Count==0 || parent.DeliveryContext==null)return;
            parent.DeliveryPlanDeclared=true;
            if(deliveryYieldCount>=2 || visits.Values.Any(v=>v.DeliveryYieldPhase!=0))return;
            foreach(var row in pickupQueue.Snapshot.Where(a=>a.Arrived).OrderBy(a=>a.SlotIndex))
            {
                if(!visits.TryGetValue(row.VisitId,out var visit) || !IsSettledPickupWaiter(visit) ||
                    !ReferenceEquals(visit.PickupOwner,pickupQueue) || visit.PaidPickup==null || !visit.PaidPickup.Transferred ||
                    visit.PaidPickup.Failed || !pickupQueue.TryCaptureReservation(visit.Id,out var claim) || !claim.IsCurrent)continue;
                var total=Capacity.GetReservations().FirstOrDefault(r=>r.OwnerId==visit.Id && r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied);
                if(total==null)return;
                var key=TrafficKey(visit.Id,"pickup-yield",visit.Id+"/"+visit.TrafficPhaseSequence);
                if(traffic.ContainsIntent(key) || trafficCandidates.ContainsKey(key))return;
                var child=GetTrafficCursor(key,visit.Actor,TrafficCandidateRole.Yield,new[]{parent.Sides[0]});
                if(child==null)return;
                CustomerSourceFrame(visit,key); // Original logical birth, even though the child has not submitted yet.
                parent.OriginalDeliveryPlan=new DeliveryYieldPlan(this,parent,child,visit,claim,total.Token,parent.Sides[0]);return;
            }
        }
        private bool DeliveryYieldIdentityCurrent(DeliveryYieldPlan plan)
        {
            if(plan==null || !ReferenceEquals(plan.Coordinator,traffic) || !AnchorContextCurrent(traffic,plan.Parent.Key) ||
                !AnchorContextCurrent(traffic,plan.ActiveChildKey) || !ReferenceEquals(plan.Parent.OriginalDeliveryPlan,plan) ||
                !ReferenceEquals(plan.Parent.DeliveryContext,plan.Context) || !plan.Context.IsCurrent ||
                !visits.TryGetValue(plan.Visit.Id,out var visit) || !ReferenceEquals(visit,plan.Visit) ||
                !ReferenceEquals(visit.Actor,plan.Actor) || plan.Actor==null || !plan.Actor.isActiveAndEnabled ||
                !world.RegisteredActors.Any(a=>ReferenceEquals(a,plan.Actor)) || !ReferenceEquals(visit.Admission,plan.Admission) ||
                !ReferenceEquals(pickupQueue,plan.Owner) || !ReferenceEquals(visit.PickupOwner,plan.Owner) ||
                !visit.PickupAssigned || visit.PickupDeparted || visit.HasCollected || visit.State==CustomerVisitState.Blocked ||
                plan.Actor.Settings.AgentRadius!=plan.Radius || plan.Actor.Settings.CollisionSkin!=plan.Skin || plan.Actor.Settings.Epsilon!=plan.Epsilon ||
                !visit.TrafficCandidateFrames.TryGetValue(plan.ActiveChildKey,out var first) || first!=plan.FirstFrame ||
                !Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,plan.Total) && r.OwnerId==visit.Id &&
                    r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied))return false;
            if(plan.ActiveChildSource==null)
                return visit.State==CustomerVisitState.PickupWaiting && visit.DeliveryYieldPhase==0 && visit.Generation==plan.Generation &&
                    visit.TrafficPhaseSequence==plan.PhaseSequence && visit.RequestId==0 && !visit.PendingPickupSegment &&
                    plan.Actor.transform.position==plan.Child.Origin && plan.Claim.IsCurrent &&
                    plan.Owner.TryCaptureReservation(visit.Id,out var current) && plan.Claim.IsSameReservation(current) &&
                    plan.Owner.TryGetReservation(visit.Id,out var original) && original.Equals(plan.Assignment) && original.Arrived &&
                    trafficCandidates.TryGetValue(plan.ActiveChildKey,out var child) && ReferenceEquals(child,plan.Child);
            return visit.Generation==plan.YieldGeneration && visit.TrafficPhaseSequence==plan.PhaseSequence+1 &&
                (visit.DeliveryYieldPhase==1 || visit.DeliveryYieldPhase==2) && ReferenceEquals(visit.YieldDeliverySource,plan.Parent) &&
                plan.YieldClaim!=null && plan.YieldClaim.IsCurrent && plan.Owner.TryCaptureReservation(visit.Id,out var yielded) &&
                plan.YieldClaim.IsSameReservation(yielded) && plan.Owner.TryGetReservation(visit.Id,out var row) &&
                row.Generation==plan.YieldAssignment.Generation && row.SlotIndex==plan.YieldAssignment.SlotIndex && row.Position==plan.YieldAssignment.Position &&
                traffic.TryGetCommittedJourney(plan.ActiveChildKey,out var source) && ReferenceEquals(source,plan.ActiveChildSource);
        }
        private bool PlannedYieldArrived(DeliveryYieldPlan plan)
        {
            if(!DeliveryYieldIdentityCurrent(plan) || plan.ActiveChildSource==null || plan.Visit.DeliveryYieldPhase!=2 ||
                plan.Visit.State!=CustomerVisitState.PickupWaiting || plan.Visit.RequestId!=0 || plan.Visit.PendingPickupSegment ||
                plan.Visit.TrafficMovePending || !plan.Owner.TryGetReservation(plan.Visit.Id,out var row) || !row.Arrived ||
                plan.Claim.IsCurrent || !plan.Claim.IsSameReservation(plan.YieldClaim) ||
                plan.YieldAssignment.Generation==plan.Assignment.Generation)return false;
            return Vector3.Distance(plan.Actor.transform.position,row.Position)<=plan.Actor.Settings.ArrivalDistance+.001f;
        }
        private bool StopPlannedDelivery(TrafficCandidateCursor parent,PlannedYieldResult result)
        {
            PickupDeliveryFailure="原让路策略已停止 / Original delivery yield stopped: "+result;
            var continuation=parent.OriginalDeliveryPlan?.Continuation;
            if(continuation!=null)FailYieldContinuation(continuation,PickupDeliveryFailure);
            var current=traffic.GetDecision(parent.Key);
            if(current.AcquiredSequence==0 && !traffic.HasActiveGrant(parent.Key) && !traffic.TryGetCommittedJourney(parent.Key,out _) &&
                current.FailureDisposition!=TrafficFailureDisposition.Terminal)RetireTrafficCandidate(parent.Key);
            var child=parent.OriginalDeliveryPlan?.Child;
            if(child!=null && traffic.ContainsIntent(child.Key))
            {
                var source=traffic.GetDecision(child.Key);
                if(source.AcquiredSequence==0 && !traffic.HasActiveGrant(child.Key) && !traffic.TryGetCommittedJourney(child.Key,out _) &&
                    source.FailureDisposition!=TrafficFailureDisposition.Terminal)RetireTrafficCandidate(child.Key);
            }
            return false;
        }
        private PlannedYieldResult PrepareTrafficYield(NavigationActor employee,TrafficCandidateCursor deliverySource)
        {
            var plan=deliverySource.OriginalDeliveryPlan;
            var continuation=plan?.Continuation;
            if(continuation!=null && (continuation.Failed || !continuation.Used || continuation.Pending))
                return continuation.CancellationConfirmed && YieldContinuationCheckpointCurrent(continuation)?PlannedYieldResult.Pending:PlannedYieldResult.Failed;
            if(!DeliveryYieldIdentityCurrent(plan))return PlannedYieldResult.Failed;
            if(plan.ActiveChildSource!=null)
            {
                if(PlannedYieldArrived(plan))return PlannedYieldResult.Yielded;
                // The existing selected child leg/task owns progress and its finite timeout; never issue a second child.
                // phase1 尚未真正到达，不能 fall through 到员工候选或把 bool commit 当作 Yielded。
                var move=plan.Visit.TrafficMove;
                return plan.Visit.DeliveryYieldPhase==1 && plan.Visit.State==CustomerVisitState.PickupAdvancing && move!=null &&
                    ReferenceEquals(move.Coordinator,traffic) && move.Intent.Equals(plan.ActiveChildKey) && ReferenceEquals(move.Source,plan.ActiveChildSource)
                    ?PlannedYieldResult.Pending:PlannedYieldResult.Failed;
            }
            if(deliveryYieldCount>=2 || !TrafficActorReady(plan.Actor) || !TrafficActorReady(employee))return PlannedYieldResult.Unavailable;
            var waiter=plan.Visit;var key=plan.Child.Key;var cursor=plan.Child;
            if(traffic.ContainsIntent(key) && traffic.GetDecision(key).FailureDisposition==TrafficFailureDisposition.Terminal)return PlannedYieldResult.Failed;
            RefreshTrafficSnapshot();
            if(!SelectTrafficCandidate(cursor,waiter.Actor,(candidate,unused,point)=>
            {
                if(InEntrance(point))
                {
                    // Only the actual same-revision layout reservation is permanent geometry evidence.
                    // Nav采样false/身体busy以及可移动marker距离都不烧掉候选。
                    if(runtime!=null && gridRoot!=null)cursor.StaticInvalidIndices.Add(candidate);
                    return false;
                }
                return Vector3.Distance(point,exit.position)>=1.2f && Vector3.Distance(point,spawn.position)>=1.2f &&
                    adapter.IsPointWalkable(point) && waiter.PickupOwner.TryPrepareMutation(PickupQueueService.MutationKind.BeginYield,
                        waiter.Id,waiter.PickupOwner.Version,out _,point) &&
                    traffic.IsParkingEndpointAvailable(new TrafficRequest(key,plan.FirstFrame,trafficSnapshot,cursor.Origin,point,
                        waiter.Actor.Settings.AgentRadius,"pickup/"+waiter.Id,
                        safetyMargin:waiter.Actor.Settings.CollisionSkin+waiter.Actor.Settings.Epsilon,planPrimaryRoute:true));
            },out var choice,out _))return PlannedYieldResult.Unavailable;
            if(!cursor.TryDescribe(key,waiter.Actor,choice,out var side,out var target))return PlannedYieldResult.Failed;
            var occupied=trafficSnapshot.Parking.Where(p=>p.Owner.ActorId!=waiter.Id && p.Owner.ActorId!=employee.ActorId).Select(p=>p.Position)
                .Concat(trafficSnapshot.Bodies.Where(b=>b.Owner.ActorId!=waiter.Id && b.Owner.ActorId!=employee.ActorId && !b.IndependentlyProgressing).Select(b=>b.Position)).Distinct().ToArray();
            var proofs=new[]{new TrafficRouteRequirement("yield-work-out",employee.transform.position,side.Position,RouteClearance(employee.Settings),occupied),
                new TrafficRouteRequirement("yield-work-home",side.Position,deliveryWorkTarget.Position,RouteClearance(employee.Settings),occupied)};
            if(TryTrafficPickupMove(waiter,PickupQueueService.MutationKind.BeginYield,target,"pickup-yield",CustomerVisitState.PickupAdvancing,
                "yield.published",out _,proofs,cursor,choice,deliverySource))
            {
                deliveryYieldCount++;
                if(!traffic.TryGetCommittedJourney(key,out var source) || !plan.Owner.TryCaptureReservation(waiter.Id,out var claim) ||
                    !plan.Owner.TryGetReservation(waiter.Id,out var assignment))return PlannedYieldResult.Failed;
                plan.ChildSource=source;plan.YieldClaim=claim;plan.YieldAssignment=assignment;plan.YieldGeneration=waiter.Generation;
                return PlannedYieldResult.Pending;
            }
            if(!traffic.ContainsIntent(key))return PlannedYieldResult.Unavailable;
            var pending=traffic.GetDecision(key);
            if(pending.FailureDisposition==TrafficFailureDisposition.Terminal)return PlannedYieldResult.Failed;
            // A real finite child proposal (or its remaining original candidates) is the only asynchronous prerequisite.
            return pending.AcquiredSequence==0 && (pending.Kind==TrafficDecisionKind.DeferredByBudget ||
                pending.Kind==TrafficDecisionKind.WaitingForTraffic || pending.FailureDisposition==TrafficFailureDisposition.AlternateAllowed)
                ?PlannedYieldResult.Pending:PlannedYieldResult.Failed;
        }
        internal void SetTrafficDeliveryGate(Func<string,bool> gate) => trafficDeliveryGate=gate;
        private bool DeliveryContextCurrent(TrafficCandidateCursor cursor,NavigationActor actor)
        {
            var context=cursor?.DeliveryContext;
            return context!=null && context.IsCurrent && context.BelongsTo(this) && cursor.Role==TrafficCandidateRole.Delivery &&
                AnchorContextCurrent(traffic,cursor.Key) && TrafficActorReady(actor) && ReferenceEquals(context.ActorLife,actor) &&
                ReferenceEquals(cursor.ActorLife,actor) && actor.transform.position==cursor.Origin &&
                context.ActorId==cursor.Key.ActorId && context.Context==cursor.Key.Context && deliveryActorId==context.ActorId &&
                context.WorkTarget.Position==deliveryWorkTarget.Position && context.WorkTarget.Facing==deliveryWorkTarget.Facing &&
                ReferenceEquals(cursor.DeliveryOwner,pickupQueue);
        }
        private bool DeliveryPreviewMatches(TrafficCandidateCursor cursor,PickupQueueService.PreparedMutation preview,int index,bool current)
            =>cursor!=null && cursor.TryDescribe(cursor.Key,cursor.ActorLife,index,out var side,out _) && preview!=null && !preview.Consumed &&
                ReferenceEquals(cursor.DeliveryOwner,pickupQueue) && preview.BelongsTo(cursor.DeliveryOwner) &&
                preview.Kind==PickupQueueService.MutationKind.DeliveryLease && preview.Id==cursor.Key.ActorId &&
                preview.Target.HasValue && preview.Target.Value==side.Position &&
                preview.Assignment.VisitId==cursor.Key.ActorId && preview.Assignment.SlotIndex==-1 &&
                preview.Assignment.Position==side.Position && preview.ExpectedVersion<=cursor.DeliveryOwner.Version &&
                (!current || preview.IsCurrent);
        private bool DeliveryReservedOptionCurrent(TrafficCandidateCursor cursor)
            =>cursor.OriginalDeliveryPlan==null || cursor.CurrentIndex!=cursor.Sides.Count || PlannedYieldArrived(cursor.OriginalDeliveryPlan);
        private bool RefreshAuthenticatedDeliveryProposal(TrafficCandidateCursor cursor,TrafficRequest request,
            PickupQueueService.PreparedMutation preview,int index,TrafficDecision expected,out TrafficDecision next)
        {
            next=null;var coordinator=traffic;var context=cursor.DeliveryContext;var source=cursor.Source;
            var oldPreview=cursor.DeliveryPreview;var plan=cursor.OriginalDeliveryPlan;var actor=cursor.ActorLife as NavigationActor;
            var owner=cursor.DeliveryOwner;var world=this.world;var layout=adapter;
            bool Current()=>ReferenceEquals(traffic,coordinator) && ReferenceEquals(this.world,world) && ReferenceEquals(adapter,layout) &&
                CanIntegratedProgress && ReferenceEquals(trafficSnapshot,request.Snapshot) &&
                trafficCandidates.TryGetValue(cursor.Key,out var actual) && ReferenceEquals(actual,cursor) &&
                ReferenceEquals(cursor.DeliveryContext,context) && ReferenceEquals(cursor.Source,source) &&
                ReferenceEquals(cursor.DeliveryPreview,oldPreview) && ReferenceEquals(cursor.OriginalDeliveryPlan,plan) &&
                ReferenceEquals(cursor.DeliveryOwner,owner) && cursor.CurrentIndex==index &&
                DeliveryContextCurrent(cursor,actor) && DeliveryPreviewMatches(cursor,preview,index,true) && DeliveryReservedOptionCurrent(cursor);
            if(expected==null || coordinator==null || context==null || !Current() ||
                !ReferenceEquals(coordinator.GetDecision(cursor.Key),expected))return false;
            if(!coordinator.TryRefreshQueuedPreparation(expected,request,out next) &&
                !coordinator.TryResubmitQueuedParkingPreparation(expected,request,Current,out next))return false;
            if(!Current() || !ReferenceEquals(coordinator.GetDecision(cursor.Key),next))
            {
                if(ReferenceEquals(traffic,coordinator) && next!=null && next.AcquiredSequence==0 &&
                    next.FailureDisposition==TrafficFailureDisposition.None && ReferenceEquals(coordinator.GetDecision(cursor.Key),next) &&
                    !coordinator.HasActiveGrant(cursor.Key) && trafficCandidates.TryGetValue(cursor.Key,out var actual) && ReferenceEquals(actual,cursor))
                    RetireTrafficCandidate(cursor.Key);
                next=null;return false;
            }
            cursor.Source=next;cursor.DeliveryPreview=preview;return true;
        }
        private void RefreshDeliveryPreparation(TrafficCandidateCursor cursor,TrafficWorldSnapshot snapshot,EmployeeFlowController producer)
        {
            var expected=traffic.GetDecision(cursor.Key);
            if(expected.FailureDisposition!=TrafficFailureDisposition.None || expected.AcquiredSequence!=0 || traffic.HasActiveGrant(cursor.Key))return;
            try
            {
                var actor=cursor.ActorLife as NavigationActor;
                if(!producer.IsCurrentPreparedDeliveryContext(cursor.DeliveryContext) || !DeliveryContextCurrent(cursor,actor) ||
                    !DeliveryPreviewMatches(cursor,cursor.DeliveryPreview,cursor.CurrentIndex,false) || !DeliveryReservedOptionCurrent(cursor))
                { RetireTrafficCandidate(cursor.Key);return; }
                if(expected.Kind!=TrafficDecisionKind.DeferredByBudget && expected.Kind!=TrafficDecisionKind.WaitingForTraffic)return;
                var preview=cursor.DeliveryPreview;
                if(!cursor.TryDescribe(cursor.Key,actor,cursor.CurrentIndex,out var target,out _))return;
                if(!preview.IsCurrent && !cursor.DeliveryOwner.TryPrepareMutation(PickupQueueService.MutationKind.DeliveryLease,
                    cursor.Key.ActorId,cursor.DeliveryOwner.Version,out preview,target.Position))return;
                if(!DeliveryPreviewMatches(cursor,preview,cursor.CurrentIndex,true))return;
                var requirements=PreservePickupGoals(null).ToList();
                requirements.Add(new TrafficRouteRequirement("selected-home",target.Position,cursor.DeliveryContext.WorkTarget.Position,
                    RouteClearance(actor.Settings),TrafficObstacles(cursor.Key.ActorId),selectedJourney:true,avoidProposedParking:false));
                var request=new TrafficRequest(cursor.Key,Time.frameCount,snapshot,cursor.Origin,target.Position,actor.Settings.AgentRadius,
                    "pickup/"+cursor.Key.ActorId,requirements:requirements,safetyMargin:actor.Settings.CollisionSkin+actor.Settings.Epsilon,planPrimaryRoute:true);
                RefreshAuthenticatedDeliveryProposal(cursor,request,preview,cursor.CurrentIndex,expected,out _);
            }
            catch(Exception)
            {
                // Unknown producer state cannot retain unissued authority; no acquired source enters here.
                // 仅退休尚未获权的原候选，不清除真实 canonical lease 或创建新预算。
                RetireTrafficCandidate(cursor.Key);
            }
        }
        // The typed call binds the actual producer; legacy string callers gain no refresh authority.
        // 原 cursor 的 seal 只保存一次，后续轮询不能以新 capture 替换旧身份。
        internal bool TryPrepareDeliveryTraffic(EmployeeFlowController.PreparedDeliveryContext context,out TrafficDecision decision,
            out PickupQueueService.PreparedMutation mutation,out NavigationTarget selected,
            out IReadOnlyList<NavigationTarget> outward,out IReadOnlyList<NavigationTarget> home)
        {
            decision=null;mutation=null;selected=default;outward=home=null;
            if(context==null || !context.IsCurrent || !context.BelongsTo(this))return false;
            return TryPrepareDeliveryTrafficCore(context.ActorId,context.Context,context,out decision,out mutation,out selected,out outward,out home);
        }

        internal bool TryPrepareDeliveryTraffic(string actorId,string context,out TrafficDecision decision,
            out PickupQueueService.PreparedMutation mutation,out NavigationTarget selected,
            out IReadOnlyList<NavigationTarget> outward,out IReadOnlyList<NavigationTarget> home)
            =>TryPrepareDeliveryTrafficCore(actorId,context,null,out decision,out mutation,out selected,out outward,out home);
        private bool TryPrepareDeliveryTrafficCore(string actorId,string context,EmployeeFlowController.PreparedDeliveryContext provenance,
            out TrafficDecision decision,out PickupQueueService.PreparedMutation mutation,out NavigationTarget selected,
            out IReadOnlyList<NavigationTarget> outward,out IReadOnlyList<NavigationTarget> home)
        {
            decision=null;mutation=null;selected=default;outward=home=null;
            if(traffic==null || !CanIntegratedProgress || pickupQueue==null || string.IsNullOrEmpty(context))return false;
            var actor=world.RegisteredActors.FirstOrDefault(a=>a!=null && a.ActorId==actorId && a.isActiveAndEnabled);
            if(!TrafficActorReady(actor))return false;
            var key=TrafficKey(actorId,"employee-pickup-journey",context);
            var cursor=GetTrafficCursor(key,actor,TrafficCandidateRole.Delivery,pickupSides.OrderBy(t=>t.Position==pickupEmployee?0:1).ToArray());
            if(cursor==null)
            {decision=traffic.GetDecision(key);PickupDeliveryFailure="送杯候选已停止 / Delivery candidate stopped: "+decision.Kind+": "+decision.Reason;return false;}
            if(visits.Values.Any(v=>(v.DeliveryYieldPhase==1 || v.DeliveryYieldPhase==3) &&
                !ReferenceEquals(v,cursor.OriginalDeliveryPlan?.Visit)))return false;
            if(provenance==null && cursor.DeliveryContext!=null)return false;
            if(provenance!=null)
            {
                if(!ReferenceEquals(provenance.ActorLife,actor))return false;
                if(cursor.Source!=null && cursor.DeliveryContext==null)return false; // Never retrofit a submitted legacy proposal.
                if(cursor.DeliveryContext!=null && (!cursor.DeliveryContext.SameSource(provenance) || !DeliveryContextCurrent(cursor,actor) ||
                    cursor.Source!=null && !DeliveryPreviewMatches(cursor,cursor.DeliveryPreview,cursor.CurrentIndex,false)))return false;
            }
            var availabilitySnapshot=RefreshTrafficSnapshot();
            var prior=cursor.Source==null?null:traffic.GetDecision(key);
#if UNITY_EDITOR
            if(CorridorObserver!=null && prior!=null && prior.Kind==TrafficDecisionKind.PlanningFailed)
            {
                try
                {
                    ObserveCorridor("traffic.delivery-yield-failure-witness",target:actor.transform.position,
                        result:"key="+key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context+
                            ";frame="+Time.frameCount+";priorRef="+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(prior)+
                            ";kind="+prior.Kind+";disposition="+prior.FailureDisposition+";reason="+prior.Reason+
                            ";permit="+prior.Permit+";requestSequence="+prior.RequestSequence+";acquired="+prior.AcquiredSequence+
                            ";count="+cursor.Count+";index="+cursor.CurrentIndex+
                            ";attempted="+string.Join(",",cursor.AttemptedIndices.OrderBy(value=>value))+
                            ";staticInvalid="+string.Join(",",cursor.StaticInvalidIndices.OrderBy(value=>value))+
                            ";sourceRef="+(cursor.Source==null?"NULL":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(cursor.Source).ToString())+
                            ";sourceKind="+cursor.Source?.Kind+";sourceDisposition="+cursor.Source?.FailureDisposition+
                            ";priorSameSource="+ReferenceEquals(prior,cursor.Source));
                }
                catch(Exception error)
                {
                    try{ObserveCorridor("traffic.delivery-yield-failure-witness-error",result:"UNKNOWN("+error.GetType().Name+":"+error.Message+")");}
                    catch(Exception){}
                }
            }
#endif
            decision=prior;
            if(!cursor.TrySelectPreferredBase(key,actor,prior,AnchorContextCurrent(traffic,key) && TrafficActorReady(actor),
                (candidate,side,target)=>candidate<cursor.Sides.Count && pickupQueue.TryPrepareMutation(PickupQueueService.MutationKind.DeliveryLease,
                    actorId,pickupQueue.Version,out _,target),
                target=>traffic.IsParkingEndpointAvailable(new TrafficRequest(key,Time.frameCount,availabilitySnapshot,cursor.Origin,target,
                    actor.Settings.AgentRadius,"pickup/"+actorId,safetyMargin:actor.Settings.CollisionSkin+actor.Settings.Epsilon,planPrimaryRoute:true)),out var index))
            {
                if(prior?.FailureDisposition==TrafficFailureDisposition.AlternateAllowed && cursor.OriginalDeliveryPlan!=null &&
                    !cursor.AttemptedIndices.Contains(cursor.Sides.Count))
                {
                    var result=PrepareTrafficYield(actor,cursor);
                    if(result==PlannedYieldResult.Pending)return false;
                    if(result!=PlannedYieldResult.Yielded)return StopPlannedDelivery(cursor,result);
                    if(!cursor.TrySelect(key,actor,prior,true,new[]{cursor.Sides.Count},out index))
                        return StopPlannedDelivery(cursor,PlannedYieldResult.Failed);
                }
                else
                {
                    if(decision?.FailureDisposition==TrafficFailureDisposition.Terminal)
                        PickupDeliveryFailure="送杯候选已停止 / Delivery candidate stopped: "+decision.Kind+": "+decision.Reason;
                    else if(provenance!=null && prior?.FailureDisposition==TrafficFailureDisposition.AlternateAllowed)
                        return StopPlannedDelivery(cursor,PlannedYieldResult.Unavailable);
                    return false;
                }
            }
            if(index==cursor.Sides.Count && !PlannedYieldArrived(cursor.OriginalDeliveryPlan))
                return StopPlannedDelivery(cursor,PlannedYieldResult.Failed);
            if(!cursor.TryDescribe(key,actor,index,out selected,out _))return false;
            if(!pickupQueue.TryPrepareMutation(PickupQueueService.MutationKind.DeliveryLease,actorId,pickupQueue.Version,out mutation,selected.Position))
                return false;
            if(provenance!=null && cursor.DeliveryContext==null)
            {
                if(cursor.Source!=null || cursor.CurrentIndex>=0 || cursor.AttemptedIndices.Count!=0)return false;
                cursor.DeliveryContext=provenance;cursor.DeliveryOwner=pickupQueue;cursor.DeliveryPreview=mutation;
            }
            if(provenance!=null && (!DeliveryContextCurrent(cursor,actor) || !DeliveryPreviewMatches(cursor,mutation,index,true)))return false;
            if(provenance!=null && cursor.Source==null)DeclareDeliveryYieldPlan(cursor);
            var requirements=PreservePickupGoals(null).ToList();
            requirements.Add(new TrafficRouteRequirement("selected-home",selected.Position,deliveryWorkTarget.Position,RouteClearance(actor.Settings),
                TrafficObstacles(actorId),selectedJourney:true,avoidProposedParking:false));
            if(provenance==null)decision=EvaluatePickupTraffic(actor,key,selected.Position,requirements,cursor:cursor,candidateIndex:index);
            else
            {
                var snapshot=RefreshTrafficSnapshot();
                var request=new TrafficRequest(key,Time.frameCount,snapshot,actor.transform.position,selected.Position,actor.Settings.AgentRadius,
                    "pickup/"+actorId,requirements:requirements,safetyMargin:actor.Settings.CollisionSkin+actor.Settings.Epsilon,planPrimaryRoute:true);
                var expected=cursor.Source==null?null:traffic.GetDecision(key);
                var prepared=expected!=null && (expected.Kind==TrafficDecisionKind.DeferredByBudget || expected.Kind==TrafficDecisionKind.WaitingForTraffic)
                    ? RefreshAuthenticatedDeliveryProposal(cursor,request,mutation,index,expected,out decision)
                    : PrepareTrafficCandidate(cursor,request,actor,index,expected,out decision);
                if(!prepared)
                {
                    decision=traffic.GetDecision(key);
                    if(decision.FailureDisposition==TrafficFailureDisposition.Terminal)
                        PickupDeliveryFailure="送杯候选已停止 / Delivery candidate stopped: "+decision.Kind+": "+decision.Reason;
                    return false;
                }
                if(decision.FailureDisposition==TrafficFailureDisposition.None && DeliveryContextCurrent(cursor,actor) &&
                    DeliveryPreviewMatches(cursor,mutation,index,true))cursor.DeliveryPreview=mutation;
            }
            if(decision.Kind!=TrafficDecisionKind.Granted)
            {
                if(decision.FailureDisposition==TrafficFailureDisposition.Terminal)
                    PickupDeliveryFailure="送杯候选已停止 / Delivery candidate stopped: "+decision.Kind+": "+decision.Reason;
                return false;
            }
            if(provenance!=null && (!DeliveryContextCurrent(cursor,actor) || !ReferenceEquals(cursor.DeliveryPreview,mutation) ||
                !DeliveryReservedOptionCurrent(cursor)))return false;
            var primary=decision.ProofRoutes.Single(p=>p.ResourceId=="pickup/"+actorId);
            var returning=decision.ProofRoutes.Single(p=>p.ResourceId=="selected-home");
            if(primary.Waypoints.Count==0 || primary.Waypoints.Count+returning.Waypoints.Count>24 || !mutation.IsCurrent ||
                !mutation.BelongsTo(pickupQueue) || mutation.Kind!=PickupQueueService.MutationKind.DeliveryLease ||
                mutation.Id!=actorId || mutation.Assignment.VisitId!=actorId || primary.Waypoints.Last()!=mutation.Assignment.Position)return false;
            var destination=selected;
            outward=primary.Waypoints.Select((p,i)=>i==primary.Waypoints.Count-1?destination:new NavigationTarget(p)).ToArray();
            home=returning.Waypoints.Select((p,i)=>i==returning.Waypoints.Count-1?deliveryWorkTarget:new NavigationTarget(p)).ToArray();return true;
        }
        internal bool CommitDeliveryTraffic(TrafficDecision decision,PickupQueueService.PreparedMutation queueMutation,
            IPreparedTrafficMutation employeeMutation,NavigationTarget selected,IReadOnlyList<NavigationTarget> outward,IReadOnlyList<NavigationTarget> home)
        {
            if(traffic==null || decision==null || queueMutation==null || !queueMutation.IsCurrent ||
                !queueMutation.BelongsTo(pickupQueue) || queueMutation.Kind!=PickupQueueService.MutationKind.DeliveryLease ||
                queueMutation.Id!=decision.Intent.ActorId || queueMutation.Assignment.VisitId!=decision.Intent.ActorId ||
                queueMutation.Assignment.Position!=selected.Position)return false;
            var primary=decision.ProofRoutes.SingleOrDefault(p=>p.ResourceId=="pickup/"+decision.Intent.ActorId);
            if(primary==null || primary.Waypoints.Count==0 || primary.Waypoints.Last()!=queueMutation.Assignment.Position)return false;
            if(trafficCandidates.TryGetValue(decision.Intent,out var cursor) && cursor.DeliveryContext!=null &&
                (!DeliveryContextCurrent(cursor,cursor.ActorLife as NavigationActor) || !ReferenceEquals(cursor.Source,decision) ||
                 !ReferenceEquals(cursor.DeliveryPreview,queueMutation) || !DeliveryPreviewMatches(cursor,queueMutation,cursor.CurrentIndex,true) ||
                 !DeliveryReservedOptionCurrent(cursor)))return false;
            return traffic.TryCommit(decision,new JoinedTrafficMutation(queueMutation,employeeMutation),()=>
            {
                CapturePickupDeliveryHistory(cursor);
                trafficActorKeys[decision.Intent.ActorId]=decision.Intent;
                deliveryAcquired=true;trafficDeliveryTarget=selected;trafficDeliveryOutward=outward;trafficDeliveryHome=home;
            });
        }
        private sealed class JoinedTrafficMutation : IPreparedTrafficMutation
        {
            private readonly IPreparedTrafficMutation first,second;
            internal JoinedTrafficMutation(IPreparedTrafficMutation first,IPreparedTrafficMutation second){this.first=first;this.second=second;}
            public bool IsCurrent=>first!=null && second!=null && first.IsCurrent && second.IsCurrent;
            public bool TryApply(out ITrafficMutationReceipt receipt)
            {
                receipt=null;if(!IsCurrent)return false;
                var applied=new JoinedTrafficReceipt();receipt=applied;
                if(!first.TryApply(out var a)){if(a!=null)applied.Items.Add(a);return false;}applied.Items.Add(a);
                if(!second.TryApply(out var b)){if(b!=null)applied.Items.Add(b);return false;}applied.Items.Add(b);return true;
            }
        }
        private sealed class JoinedTrafficReceipt : ITrafficMutationReceipt
        {
            internal readonly List<ITrafficMutationReceipt> Items=new List<ITrafficMutationReceipt>();private bool retired;
            public bool IsCurrent=>!retired && Items.Count>0 && Items.All(r=>r!=null && r.IsCurrent);
            public void RollbackOwnMutation()
            {
                if(retired)return;if(!IsCurrent)throw new InvalidOperationException("Joint canonical receipt is stale.");retired=true;
                for(var i=Items.Count-1;i>=0;i--)Items[i].RollbackOwnMutation();
            }
        }
        internal void CompleteEmployeeTraffic(string actorId,string context,bool returned)
        {
            if(traffic==null || !trafficActorKeys.TryGetValue(actorId,out var key) || key.Context!=context)return;
            var history=pickupDeliveryHistory;
            // The existing DeliveryArrived caller must also match the real acquired source and actual lease endpoint.
            // returned=true 也可能表示 Blocked，绝不把这个 bool 单独签成已回家证明。
            try
            {
                if(!returned && history!=null && history.Key.Equals(key) && history.OwnerLife==pickupPreparationLife &&
                    ReferenceEquals(history.Owner,pickupQueue) && history.Actor!=null &&
                    traffic.TryGetCommittedJourney(key,out var source) && ReferenceEquals(source,history.Source) &&
                    Vector3.Distance(history.Actor.transform.position,history.Lease.Position)<=history.Actor.Settings.ArrivalDistance+.001f)
                    history.Delivered=true;
            }
            catch(Exception){ /* No history authentication; preserve the original completion/retirement path. */ }
            if(returned){RetireTrafficCandidate(key);return;}
            traffic.RetainNamedJourney(key,"selected-home");
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ObserveCollectedExitPreparation(string stage,TrafficIntentKey? key,int first,long sequence,int generation,
            TrafficPhaseReceipt pending,NavigationActor actor,CustomerAdmission admission,bool collected,
            TrafficDecision decision=null,TrafficRequest request=null,bool? live=null,bool? bound=null,string guards="NOT_EVALUATED")
        {
#if UNITY_EDITOR
            var observer=CollectedExitPreparationObserver;if(observer==null)return;
            try
            {
                object Stored(object value,string member)
                {
                    if(value==null)return null;
                    var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
                    var field=value.GetType().GetField(member,flags)??value.GetType().GetField("<"+member+">k__BackingField",flags);
                    if(field==null)throw new MissingFieldException(value.GetType().FullName,member);
                    return field.GetValue(value);
                }
                string Scalar(object value,string member)=>value==null?"NOT_EVALUATED":Stored(value,member)?.ToString()??"NULL";
                string Ref(object value)=>value==null?"NULL":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
                object boxed=key.HasValue?(object)key.Value:null;var world=Stored(request,"Snapshot");
                var actualKey=boxed==null?"NOT_CREATED":Scalar(boxed,"Session")+"/e"+Scalar(boxed,"Epoch")+"/"+Scalar(boxed,"ActorId")+"/"+
                    Scalar(boxed,"IntentId")+"/g"+Scalar(boxed,"Generation")+"/r"+Scalar(boxed,"LayoutRevision")+"/"+Scalar(boxed,"Context");
                observer("COLLECTED_EXIT_PREPARATION_ORIGINAL key="+actualKey+" first="+first+" live="+(live.HasValue?live.Value.ToString():"NOT_EVALUATED")+
                    " bound="+(bound.HasValue?bound.Value.ToString():"NOT_EVALUATED")+" stage="+stage+" sequence="+sequence+" generation="+generation+" collected="+collected+
                    " pending="+Ref(pending)+" handle="+Ref(pending?.Handle)+" predecessorSource="+Ref(pending?.Source)+" marked="+pending?.Marked+
                    " actor="+Ref(actor)+" pendingActor="+Ref(pending?.Actor)+" admission="+Ref(admission)+" pendingAdmission="+Ref(pending?.Admission)+
                    " decision="+Ref(decision)+" kind="+Scalar(decision,"Kind")+" reason="+Scalar(decision,"Reason")+
                    " disposition="+Scalar(decision,"FailureDisposition")+" requestSeq="+Scalar(decision,"RequestSequence")+" acquiredSeq="+Scalar(decision,"AcquiredSequence")+
                    " request="+Ref(request)+" requestFirst="+Scalar(request,"RequestedFrame")+" snapshot="+Ref(world)+" version="+Scalar(world,"Version")+"/"+Scalar(world,"BodyVersion")+" guards="+guards);
            }
            catch(Exception){CollectedExitPreparationObservationErrors++;}
#endif
        }

        private bool TryPrepareTrafficExit(Visit visit)
        {
            var coordinator=traffic;var generation=visit.Generation;var actor=visit.Actor;var admission=visit.Admission;
            var pending=visit.PendingTraffic;var collected=visit.HasCollected;var order=visit.CollectedOrder;
            var first=visit.CollectionFrame;var sequence=visit.ExitSequence;var target=exit.position;
            if(collected && (!order.HasValue || first<0))
            {ObserveCollectedExitPreparation("collection-rejected",null,first,sequence,generation,pending,actor,admission,collected,guards:"collection=REJECTED;rest=NOT_EVALUATED");return false;}
            var key=TrafficKey(visit.Id,"actual-exit",CollectionTrafficContext(visit));
            ObserveCollectedExitPreparation("captured",key,first,sequence,generation,pending,actor,admission,collected,guards:"collection=PASSED;current=NOT_EVALUATED;live=NOT_EVALUATED;bind=NOT_EVALUATED");
            bool SameVisit()=>!disposed && coordinator!=null && ReferenceEquals(coordinator,traffic) && actor!=null &&
                key.Session==trafficSession && key.Epoch==trafficEpoch && adapter!=null && key.LayoutRevision==adapter.Revision &&
                visits.TryGetValue(visit.Id,out var current) && ReferenceEquals(current,visit) && ReferenceEquals(visit.Actor,actor) &&
                ReferenceEquals(visit.Admission,admission) && ReferenceEquals(visit.PendingTraffic,pending) &&
                visit.Generation==generation && visit.State==CustomerVisitState.Exiting && visit.HasCollected==collected &&
                Nullable.Equals(visit.CollectedOrder,order) && visit.CollectionFrame==first && visit.ExitSequence==sequence &&
                exit!=null && exit.position==target && visit.FinalTarget.Position==target;
            bool Current()=>SameVisit() && (pending==null || !collected || CanIntegratedProgress);
            if(!Current())
            {ObserveCollectedExitPreparation("pre-snapshot-rejected",key,first,sequence,generation,pending,actor,admission,collected,guards:"preSnapshot=REJECTED;rest=NOT_EVALUATED");return false;}
            var snapshot=RefreshTrafficSnapshot();if(!Current())
            {ObserveCollectedExitPreparation("post-snapshot-rejected",key,first,sequence,generation,pending,actor,admission,collected,guards:"preSnapshot=PASSED;postSnapshot=REJECTED;rest=NOT_EVALUATED");return false;}
            var request=new TrafficRequest(key,CustomerSourceFrame(visit,key),snapshot,actor.transform.position,target,actor.Settings.AgentRadius,
                "pickup/"+visit.Id,requirements:Array.Empty<TrafficRouteRequirement>(),
                safetyMargin:actor.Settings.CollisionSkin+actor.Settings.Epsilon,planPrimaryRoute:true);
            var decision=coordinator.EvaluateMovement(request);
            ObserveCollectedExitPreparation("submit",key,first,sequence,generation,pending,actor,admission,collected,decision,request,guards:"snapshotGuards=PASSED;postSubmit=NOT_EVALUATED;live=NOT_EVALUATED;bind=NOT_EVALUATED");
            // Wrong/reentrant Visit must not block its replacement. Genuine current terminal keeps the original failure path.
            if(!Current())
            {ObserveCollectedExitPreparation("post-submit-rejected",key,first,sequence,generation,pending,actor,admission,collected,decision,request,guards:"postSubmit=REJECTED;rest=NOT_EVALUATED");return false;}
            if(decision.Kind!=TrafficDecisionKind.Granted && decision.Kind!=TrafficDecisionKind.DeferredByBudget && decision.Kind!=TrafficDecisionKind.WaitingForTraffic)
            {ObserveCollectedExitPreparation("terminal",key,first,sequence,generation,pending,actor,admission,collected,decision,request,guards:"postSubmit=PASSED;decisionKind=REJECTED;rest=NOT_EVALUATED");BlockVisit(visit,CustomerVisitState.Exiting,"Exit traffic "+decision.Kind+": "+decision.Reason);return false;}
            if(!ReferenceEquals(coordinator.GetDecision(key),decision))
            {ObserveCollectedExitPreparation("source-rejected",key,first,sequence,generation,pending,actor,admission,collected,decision,request,guards:"postSubmit=PASSED;source=REJECTED;rest=NOT_EVALUATED");return false;}
            if(pending!=null && collected)
            {
                var live=coordinator.IsCurrentPhaseHandoff(pending.Handle,pending.Source,actor);
                ObserveCollectedExitPreparation("live-computed",key,first,sequence,generation,pending,actor,admission,collected,decision,request,live,guards:"source=PASSED;postLive=NOT_EVALUATED;nav=NOT_EVALUATED;bind=NOT_EVALUATED");
                if(!Current() || !ReferenceEquals(coordinator.GetDecision(key),decision))
                {ObserveCollectedExitPreparation("post-live-rejected",key,first,sequence,generation,pending,actor,admission,collected,decision,request,live,guards:"postLive=REJECTED;operand=UNKNOWN;nav=NOT_EVALUATED;bind=NOT_EVALUATED");return false;}
                // Temporary Nav absence neither proves release nor kills a live bounded completed phase.
                if(live && !TrafficActorReady(actor))
                {ObserveCollectedExitPreparation("nav-held",key,first,sequence,generation,pending,actor,admission,collected,decision,request,live,guards:"postLive=PASSED;nav=FALSE;bind=NOT_EVALUATED");return false;}
                var bound=live && coordinator.TryBindPhaseHandoffSuccessorPreparation(pending.Handle,decision,request,
                    new CollectedExitPreparation(pending,request,decision));
                ObserveCollectedExitPreparation("bind-computed",key,first,sequence,generation,pending,actor,admission,collected,decision,request,live,bound,
                    guards:live?"postLive=PASSED;nav=PASSED;bindCall=EVALUATED;postBind=NOT_EVALUATED":"postLive=PASSED;nav=NOT_EVALUATED;bindCall=NOT_EVALUATED;postBind=NOT_EVALUATED");
                if(!Current() || !ReferenceEquals(coordinator.GetDecision(key),decision))
                {ObserveCollectedExitPreparation("post-bind-rejected",key,first,sequence,generation,pending,actor,admission,collected,decision,request,live,bound,guards:"postBind=REJECTED;operand=UNKNOWN");return false;}
                if(!bound)
                {
                    if(decision.AcquiredSequence==0 && !coordinator.HasActiveGrant(key) && !coordinator.TryGetCommittedJourney(key,out _))
                    {coordinator.Retire(key);if(!Current())return false;}
                    if(Current())BlockVisit(visit,CustomerVisitState.Exiting,"Exit traffic original canonical preparation invalid / 原离场交接准备失效");
                    return false;
                }
            }
            else ObserveCollectedExitPreparation("binding-skipped",key,first,sequence,generation,pending,actor,admission,collected,decision,request,guards:"pendingAndCollected=FALSE;live=NOT_EVALUATED;bind=NOT_EVALUATED");
            if(decision.Kind!=TrafficDecisionKind.Granted)return false;
            var primary=decision.ProofRoutes.Single(p=>p.ResourceId=="pickup/"+visit.Id);
            if(primary.Waypoints.Count==0 || primary.Waypoints.Count>24)return false;
#if UNITY_EDITOR
            if(CorridorObserver!=null)
                try
                {
                    var original=decision.Intent;
                    CorridorObserver("traffic.exit-primary-precommit",visit.Id,original.Context,exit.position,null,null,
                        "session="+original.Session+" epoch="+original.Epoch+" actor="+original.ActorId+" layout="+original.LayoutRevision+" intent="+original.IntentId+" generation="+original.Generation+
                        " decisionRef="+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(decision)+" requestSequence="+decision.RequestSequence+" acquired="+decision.AcquiredSequence+" mutation=null");
                }
                catch(Exception){ } // Read-only diagnostics cannot change this original commit branch.
#endif
            if(!Current() || !ReferenceEquals(coordinator.GetDecision(key),decision))return false;
            return coordinator.TryCommit(decision,publish:()=>
            {
                if(!SameVisit() || !ReferenceEquals(coordinator.GetDecision(key),decision) || !coordinator.HasActiveGrant(key))return;
                visit.Route=new Queue<Vector3>(primary.Waypoints);visit.PendingExitSegment=false;
                SelectCustomerTraffic(visit,decision,primary,CustomerVisitState.Exiting,visit.FinalTarget);
                RememberOwnedRoute(visit,CustomerVisitState.Exiting,primary);
                if(!SameVisit() || !ReferenceEquals(coordinator.GetDecision(key),decision) || !coordinator.HasActiveGrant(key))return;
                visit.TrafficMovePending=true;SendRouteSegment(visit,CustomerVisitState.Exiting);
            });
        }
        internal string PickupDeliveryFailure { get; private set; }="";
        internal void ConfigurePickupDelivery(string actorId,NavigationTarget workTarget)
        { deliveryActorId=actorId; deliveryWorkTarget=workTarget; }
        internal void SetPickupDeliveryAttempt(long handle)
        {
            if(deliveryAttempt==handle) return;
            deliveryAttempt=handle;deliveryYieldCount=0;PickupDeliveryFailure="";deliveryTrafficPaths=null;
        }
        private readonly List<CafeVisitSnapshot> exitSnapshots=new List<CafeVisitSnapshot>();
        public bool AutoSpawn
        {
            get=>autoSpawn;
            set
            {
                autoSpawn=value;
                CancelStoppedAutomaticAdmission();
            }
        }
        private void CancelStoppedAutomaticAdmission()
        {
            // Explicitly held automatic arrivals release their unborn proposal. Manual requests keep their source.
            // 停止自动接客时释放未出生的准备；手动申请保留原身份与累计预算。
            if(!autoSpawn && pendingAdmissionAnchor?.Automatic==true)CancelUnbornAdmission();
        }
        public bool RequiresAdmissionPolicy => requireAdmissionPolicy;
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
        internal void CancelUnbornAdmissionForClosing() => CancelUnbornAdmission();
        // Closing and explicitly held automatic arrivals share the same unborn-only retirement checks.
        // 关店或明确停止自动来客共用未出生核验；Pause/临时门禁仍保留原机会。
        private void CancelUnbornAdmission()
        {
            var a=pendingAdmissionAnchor;
            if(a==null || disposed || !isActiveAndEnabled || admissionInProgress || preparingTrafficProposals ||
                traffic==null || !ReferenceEquals(traffic,a.Coordinator) || a.Key.Session!=trafficSession || a.Key.Epoch!=trafficEpoch ||
                adapter==null || a.Key.LayoutRevision!=adapter.Revision || session!=a.Session || nextVisit!=a.Before ||
                queue==null || !ReferenceEquals(queue,a.Owner) || Capacity==null || !ReferenceEquals(Capacity,a.Capacity) ||
                world==null || !ReferenceEquals(world,a.World) || spawn==null || !ReferenceEquals(spawn,a.Spawn) ||
                prefabs==null || prefabs.Length==0 || a.Prefab==null || !ReferenceEquals(prefabs[(int)(nextVisit%prefabs.Length)],a.Prefab) ||
                a.Key.IntentId!="counter-admission" || a.Key.Context!=a.Key.ActorId ||
                a.Key.ActorId!=session+"-"+(nextVisit+1).ToString("D8"))return;
            var memo=a.Key.ActorId+"/"+a.Key.IntentId+"/"+a.Key.Context;
            if(!trafficKeys.TryGetValue(memo,out var original) || !original.Equals(a.Key) || visits.ContainsKey(a.Key.ActorId) ||
                world.RegisteredActors.Any(actor=>actor!=null && actor.ActorId==a.Key.ActorId) ||
                GetComponentsInChildren<NavigationActor>(true).Any(actor=>actor!=null && actor.ActorId==a.Key.ActorId) ||
                queue.Snapshot.Any(row=>row.VisitId==a.Key.ActorId) || Capacity.GetReservations().Any(row=>row.OwnerId==a.Key.ActorId) ||
                trafficSnapshot!=null && (trafficSnapshot.Bodies.Any(row=>row.Owner.ActorId==a.Key.ActorId) ||
                    trafficSnapshot.Parking.Any(row=>row.Owner.ActorId==a.Key.ActorId)))return;
            var registered=traffic.ContainsIntent(a.Key);
            if(a.Submitted && !registered || registered &&
                (traffic.HasActiveGrant(a.Key) || traffic.GetDecision(a.Key).AcquiredSequence!=0))return;
            a.Coordinator.Retire(a.Key);
            if(!ReferenceEquals(pendingAdmissionAnchor,a) || !ReferenceEquals(traffic,a.Coordinator))return;
            pendingAdmissionAnchor=null;
            if(trafficKeys.TryGetValue(memo,out var current) && current.Equals(a.Key))trafficKeys.Remove(memo);
        }
        public void RegisterAdmissionPolicy(UnityEngine.Object owner,Func<bool> canAdmit)
        {
            if(owner==null) throw new ArgumentNullException(nameof(owner));
            if(canAdmit==null) throw new ArgumentNullException(nameof(canAdmit));
            if(ReferenceEquals(admissionOwner,owner)) return;
            if(AdmissionOwnerIsActive(admissionOwner))
                throw new InvalidOperationException("已有活动admission policy owner，不能竞争接待门禁。");
            admissionOwner=owner; this.canAdmit=canAdmit;
            // 一旦由营业周期接管，provider撤销后也继续fail closed。
            requireAdmissionPolicy=true;
        }
        public void UnregisterAdmissionPolicy(UnityEngine.Object owner)
        {
            if(!ReferenceEquals(admissionOwner,owner)) return;
            admissionOwner=null; canAdmit=null;
        }
        private static bool AdmissionOwnerIsActive(UnityEngine.Object owner) => owner!=null &&
            (!(owner is Behaviour behaviour) || behaviour.isActiveAndEnabled) &&
            (!(owner is GameObject host) || host.activeInHierarchy);
        private bool AdmissionPolicyAllows()
        {
            admissionPolicyFailure="";
            if(!requireAdmissionPolicy && canAdmit==null) return true;
            if(!AdmissionOwnerIsActive(admissionOwner) || canAdmit==null)
                admissionPolicyFailure="接待policy不可用 / Admission policy unavailable";
            else
            {
                try
                {
                    if(canAdmit()) return true;
                    admissionPolicyFailure="接待policy已关闭 / Admission policy closed";
                }
                catch(Exception exception)
                { admissionPolicyFailure="接待policy失败 / Admission policy failed: "+exception.Message; }
            }
            Status=admissionPolicyFailure; return false;
        }
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
            if(traffic!=null) return BuildTrafficPickupCandidates(visit,out reason);
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
                pickupWaitSlots=slots.ToArray();
                reason=""; return true;
                }
            }
            return false;
        }
        private bool BuildTrafficPickupCandidates(Visit visit,out string reason)
        {
            reason="没有合法Pick-up候选链 / No pickup candidate chain";
            var clearance=prefabs.Max(p=>2*p.Settings.AgentRadius+p.Settings.CollisionSkin+p.Settings.Epsilon+p.Settings.ArrivalDistance);
            // 空的未来收银尾位尚无owner；真实分配和已有路线仍由共同许可保护 / Unassigned tail slots own no space.
            var assignedCounter=queue?.Snapshot.Select(row=>row.Position).ToArray()??Array.Empty<Vector3>();
            // 初建候位先考虑靠近当前队首的一侧；完整route proof仍是唯一占位许可 / Prefer the nearer approach, prove before commit.
            foreach(var side in pickupSides.OrderBy(side=>(side.Position-visit.Actor.transform.position).sqrMagnitude))
            {
                var outward=-side.Facing.Value;var shared=multiSidePickup || Vector3.Distance(side.Position,pickupEmployee)<clearance;
                var first=shared?side.Position+outward*1.1f:side.Position;
                // 只排序既有有限候选，优先避开工作通道；最终仍需Core完整证明 / Preference only, never permission.
                var heads=PickupWaitHeads(first,outward,!string.IsNullOrEmpty(deliveryActorId));
                if(pickupSides.Length==1 && !string.IsNullOrEmpty(deliveryActorId))heads=heads.OrderBy(head=>CustomerRoutePlanner.IsClear(
                    new[]{deliveryWorkTarget.Position,side.Position},new[]{head},clearance)?0:1);
                foreach(var head in heads)
                {
                    // Geometry produces candidates only. Each actual parking prefix still needs the shared bounded capability proof.
                    // 候选链本身不授予空间；真正占位必须通过同一coordinator共同提交。
                    // Retain the same original forward-gate facts as the legacy branch; no diagnostic requery.
                    // 当前分支也保存原转弯拒绝结果，验收不能事后重算。
                    pickupPlanningRejections.Clear();
                    var planner=new CounterQueuePlanner(1.1f,preserveStraightPrefix:true);
                    planner.RejectedForward=(point,previous,direction,prefix,failure)=>{
                        if(pickupPlanningRejections.Count<256)
                            pickupPlanningRejections.Add(new QueueForwardRejection(point,previous,direction,prefix,failure));
                    };
                    var slots=planner.Build(head,outward,Capacity.Limits.PickUp,
                        (point,previous)=>adapter.IsPointArrivable(point,visit.Actor.Settings.ArrivalDistance) && !InEntrance(point) && Vector3.Distance(point,exit.position)>=1.2f &&
                            (!shared || Vector3.Distance(point,side.Position)>=clearance) &&
                            (!multiSidePickup || pickupSides.All(s=>Vector3.Distance(point,s.Position)>=clearance)) &&
                            assignedCounter.All(p=>Vector3.Distance(p,point)>=clearance) &&
                            world.RegisteredActors.All(actor=>actor==null || Vector3.Distance(actor.transform.position,point)>=clearance),
                        (point,prefix)=>true,(from,to)=>adapter.IsPointArrivable(to,visit.Actor.Settings.ArrivalDistance)?(Vector3?)to:null);
                    if(slots.Count==0)continue;
                    pickupCustomer=side.Position;pickupOutward=outward;pickupWaitSlots=slots.ToArray();
                    pickupQueue=multiSidePickup?new PickupQueueService(slots,pickupSides.Select(s=>s.Position).ToArray()):new PickupQueueService(slots,side.Position,shared);
                    reason="";return true;
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
            ObserveCorridor("reserve.attempt",visit);
            // Logical FIFO front can still occupy its old tail slot; service starts only at the actual Counter head.
            // 逻辑队首仍可能停在旧尾位，必须实际到slot0才开始预留/收银。
            if(visit.State!=CustomerVisitState.Queued || queue==null || queue.Snapshot.Count==0 ||
                queue.Snapshot[0].VisitId!=visitId || queue.Snapshot[0].SlotIndex!=0 || !queue.Snapshot[0].Arrived || !visit.CounterArrived)
            { ObserveCorridor("reserve.result",visit,result:"not-actual-front");return false; }
            if(traffic!=null) return TryTrafficReservation(visit,out reason);
            if(visit.PickupAssigned)
            {
                reason="预留站位已不可达";
                if(!visit.PickupOwner.TryReserve(visitId,out var current) || !adapter.IsPointWalkable(current.Position) ||
                    !TryCapturePickupCorridor(visit,visit.PickupOwner,current))
                { ObserveCorridor("reserve.result",visit,result:"existing-revalidation-failed");ClearPickupCorridor(visit); return false; }
                reason=""; ObserveCorridor("reserve.result",visit,result:"existing-accepted");return true;
            }
            if(!EnsurePickupQueue(visit,out reason))
            { ObserveCorridor("reserve.result",visit,result:"front-or-queue-construction-failed");return RefuseUnreservedFront(visit); }
            reason="没有可用的安全Pick-up分配";
            // CanReserve guarantees contiguous slots. Count actual accepted owners, including
            // unpaid reservations (the service Snapshot contains only paid FIFO entries).
            var nextSlot=visits.Values.Count(v=>v.PickupAssigned && ReferenceEquals(v.PickupOwner,pickupQueue));
            if(!pickupQueue.CanReserve || nextSlot>=pickupWaitSlots.Length)
            { ObserveCorridor("reserve.result",visit,result:"not-contiguous-or-count-outside-slots");return RefuseUnreservedFront(visit); }
            ObserveCorridor("reserve.candidate",visit,target:pickupWaitSlots[nextSlot]);
            if(!NewPickupTargetClear(visit,pickupWaitSlots[nextSlot]))
            {
                ObservePickup("reserve.deferred-service",visit,pickupQueue,new QueueAssignment(visitId,nextSlot,0,pickupWaitSlots[nextSlot],false));
                reason="等待已拥有的服务通道 / Owned service corridor";return RefuseUnreservedFront(visit);
            }
            if(!pickupQueue.TryReserve(visitId,out var assignment))
            { ObserveCorridor("reserve.result",visit,result:"service-refused");return RefuseUnreservedFront(visit); }
            ObservePickup("reserve.published",visit,pickupQueue,assignment);
            if(!TryCapturePickupCorridor(visit,pickupQueue,assignment))
            { ObserveCorridor("reserve.rollback-before",visit);pickupQueue.Remove(visitId);ObserveCorridor("reserve.result",visit,result:"new-certificate-failed-removed");return RefuseUnreservedFront(visit); }
            visit.PickupAssigned=true; visit.PickupOwner=pickupQueue;
            if(ReferenceEquals(refusedPickupFront,visit)) refusedPickupFront=null;
            ObservePickup("reserve.accepted",visit,pickupQueue,assignment);
            reason=""; return true;
        }
        private bool RefuseUnreservedFront(Visit visit,TrafficDecision originalSpatialRefusal=null)
        {
            if(traffic!=null)
            {
                // Existing business eligibility only. Core remains the sole spatial authority.
                // 只保存本次真实空间拒绝；Deferred/预算/身份故障不是空间失败，也不新增WAIT权利。
                var original=originalSpatialRefusal;var anchor=visit?.PendingReservationAnchor;
                var eligibility=original!=null && anchor?.Eligibility!=null &&
                    traffic.IsUnpaidParkingEligibilityHeld(original,anchor.Eligibility);
                var spatial=eligibility || original!=null && original.Blockers.Count>0 &&
                    (original.Kind==TrafficDecisionKind.WaitingForTraffic && original.FailureDisposition==TrafficFailureDisposition.None ||
                     original.Kind==TrafficDecisionKind.PlanningFailed && original.FailureDisposition==TrafficFailureDisposition.Terminal &&
                        original.Reason=="no-independent-finite-release-or-cycle");
                if(!spatial || anchor==null || !anchor.Submitted || !ReferenceEquals(anchor.Visit,visit) ||
                    !ReferenceEquals(anchor.Coordinator,traffic) || !anchor.Key.Equals(original.Intent) ||
                    !traffic.ContainsIntent(original.Intent) || !ReferenceEquals(traffic.GetDecision(original.Intent),original) ||
                    !ReservationAnchorCurrent(anchor))return false;
            }
            refusedPickupFront=visit;refusedCounterQueue=queue;refusedPickupQueue=pickupQueue;
            refusedPickupRevision=adapter.Revision;
            ObserveCorridor("admission.front-refusal-observed",visit);
            return false;
        }
        private bool UnreservedFrontHoldsAdmission()
        {
            var front=refusedPickupFront;
            if(front==null) return false;
            var head=queue?.Snapshot;
            if(!integratedMode || !ReferenceEquals(queue,refusedCounterQueue) || adapter.Revision!=refusedPickupRevision ||
                !ReferenceEquals(pickupQueue,refusedPickupQueue) || head==null || head.Count==0 ||
                head[0].VisitId!=front.Id ||
                !visits.TryGetValue(front.Id,out var current) || !ReferenceEquals(current,front) ||
                front.PickupAssigned || front.HasCollected ||
                front.Actor==null || !front.Actor.isActiveAndEnabled)
            { refusedPickupFront=null;return false; }
            // Trigger requires an arrived Queued head; retirement requires actual ownership loss.
            // 同一队首前移只改变assignment/Arrived，不解除已观察的空间拒绝。
            return true;
        }
        private Vector3[] CorridorObstacles(Visit visit) => CorridorObstaclesExceptBody(visit,null);
        private IEnumerable<Vector3> ReservedPickupTargets(string ignore)
        {
            // Path revalidation can retire the certificate without cancelling the slot.
            // 使用真实owner的预留目标，避免缓存失效后让其他路线穿过仍归顾客所有的站位。
            foreach(var visit in visits.Values)
                if(visit.Id!=ignore && visit.PickupAssigned && !visit.PickupInTransit && !visit.HasCollected &&
                    visit.PickupOwner!=null && visit.PickupOwner.TryGetReservation(visit.Id,out var assignment))
                    yield return assignment.Position;
        }
        private Vector3[] CorridorObstaclesExceptBody(Visit visit,NavigationActor finiteBody)
            => world.RegisteredActors.Where(a=>a!=null && a!=visit.Actor && a!=finiteBody).Select(a=>a.transform.position)
                .Concat(WaitTargetPositions(visit.Id))
                .Concat(ReservedPickupTargets(visit.Id))
                .Concat(visits.Values.Select(v=>v.PickupOwner).Concat(new[]{pickupQueue}).Where(q=>q!=null).Distinct()
                    .SelectMany(q=>q.ServiceLeases.Where(l=>l.Key!=visit.Id).Select(l=>l.Value))).Distinct().ToArray();
        private bool TryCapturePickupCorridor(Visit visit,PickupQueueService owner,QueueAssignment assignment)
        {
            var route=BuildPickupWaitRoute(visit.Actor.transform.position,assignment.Position,visit.Id,visit.Actor.Settings);
            ObserveCorridor("reserved.route",visit,target:assignment.Position,path:route);
            if(route==null || route.Count==0 || route.Count>24) return false;
            var path=route==null?null:ExpandRoute(visit.Actor.transform.position,route);
            if(path==null) return false;
            var obstacles=CorridorObstacles(visit);
            var clear=CustomerRoutePlanner.IsClear(path,obstacles,RouteClearance(visit.Actor.Settings));
            ObserveCorridor("reserved.capture-check",visit,target:assignment.Position,path:path,obstacles:obstacles,result:clear?"clear":"blocked");
            if(!clear) return false;
            visit.ReservedPickup=assignment;visit.ReservedPickupOwner=owner;
            visit.ReservedPickupRevision=adapter.Revision;visit.ReservedPickupPath=path;
            visit.ReservedPickupRoute=route;
            ObserveCorridor("reserved.captured",visit,target:assignment.Position,path:path);
            return true;
        }
        private void ClearPickupCorridor(Visit visit)
        { ObserveCorridor("reserved.retire",visit,path:visit.ReservedPickupPath);visit.ReservedPickup=null;visit.ReservedPickupOwner=null;visit.ReservedPickupPath=null;visit.ReservedPickupRoute=null; }
        private bool HasReservedPickupCorridor(Visit visit)
        { var valid=visit.PickupAssigned && !visit.PickupInTransit && !visit.HasCollected && visit.Actor!=null &&
                visit.Actor.isActiveAndEnabled && visit.State!=CustomerVisitState.Blocked &&
                ReferenceEquals(visit.ReservedPickupOwner,visit.PickupOwner) && visit.ReservedPickup.HasValue &&
                visit.ReservedPickup.Value.VisitId==visit.Id && visit.ReservedPickupRevision==adapter.Revision && visit.ReservedPickupPath!=null;
            ObserveCorridor("reserved.binding",visit,result:valid?"valid":"invalid");return valid; }
        private void RememberOwnedRoute(Visit visit,CustomerVisitState phase,TrafficRouteProof retained=null)
        {
            ObserveCorridor("owned.remember-begin",visit,result:visit.OwnedRouteSegments!=visit.Segments?"segment-progress":
                !ReferenceEquals(visit.OwnedRouteSource,visit.Route)?"route-source-changed":"acquisition-or-same-route");
            ClearOwnedRoute(visit,"before-recapture");
            if(!IsPickupMovement(phase) && phase!=CustomerVisitState.Exiting || visit.Route==null || visit.Route.Count==0)
            { ObserveCorridor("owned.capture-skipped",visit,result:"phase-or-empty-route");return; }
            var ends=new List<int>();
            var path=retained==null?ExpandRouteWithLegEnds(visit.Actor.transform.position,visit.Route,ends):retained.ExpandedPath;
            if(retained!=null)ends.AddRange(retained.LegEnds);
            if(path==null) { ObserveCorridor("owned.capture-check",visit,result:"null-path");return; }
            var obstacles=CorridorObstacles(visit);
            var clear=CustomerRoutePlanner.IsClear(path,obstacles,RouteClearance(visit.Actor.Settings));
            ObserveCorridor("owned.capture-check",visit,path:path,obstacles:obstacles,result:clear?"clear":"blocked");
            // Keep checked selected geometry for a later REAL accepted request. A blocked
            // unissued candidate gains no authority merely by being retained here.
            // 保留几何不等于获得优先权；RequestId零仍须原full-clear捕获资格。
            visit.OwnedRouteWasClear=clear;
            visit.OwnedRoutePath=path;visit.OwnedRouteLegEnds=ends;visit.OwnedRouteOwner=visit.PickupOwner;visit.OwnedRouteAssignment=visit.Assignment;
            visit.OwnedRoutePhase=phase;visit.OwnedRouteRevision=adapter.Revision;visit.OwnedRouteSegments=visit.Segments;visit.OwnedRouteSource=visit.Route;
            ObserveCorridor(clear?"owned.captured":"owned.candidate-unqualified",visit,path:path);
        }
        private void ClearOwnedRoute(Visit visit,string reason="phase-arrival-block-or-hold")
        { ObserveCorridor("owned.retire",visit,path:visit.OwnedRoutePath,result:reason);visit.OwnedRoutePath=null;visit.OwnedRouteOwner=null;visit.OwnedRouteSource=null;visit.OwnedRouteLegEnds=null;visit.OwnedWaitProof=null;visit.OwnedIssuedRequest=0;visit.OwnedRouteWasClear=false;ClearEmployeeExitWait(visit);visit.PickupPriorityWait=null; }
        private static bool HasOwnedActiveRequest(Visit visit) => visit.RequestId!=0 &&
            visit.RequestId==visit.OwnedIssuedRequest && visit.Generation==visit.OwnedIssuedGeneration;
        private bool HasOwnedRoute(Visit visit)
        { var valid=visit.OwnedRoutePath!=null && (visit.OwnedRouteWasClear || HasOwnedActiveRequest(visit)) && visit.Actor!=null && visit.Actor.isActiveAndEnabled &&
                visit.State==visit.OwnedRoutePhase && visit.OwnedRouteRevision==adapter.Revision &&
                ReferenceEquals(visit.OwnedRouteOwner,visit.PickupOwner) &&
                visit.OwnedRouteAssignment.Generation==visit.Assignment.Generation && visit.OwnedRouteAssignment.Position==visit.Assignment.Position &&
                (IsPickupMovement(visit.State) && visit.PickupAssigned || visit.State==CustomerVisitState.Exiting && visit.HasCollected) &&
                (visit.State!=CustomerVisitState.MovingToPickupService || visit.PickupOwner!=null &&
                    visit.PickupOwner.TryGetServiceLease(visit.Id,out var lease) && lease==visit.OwnedRouteAssignment.Position);
            ObserveCorridor("owned.binding",visit,result:valid?"valid":"invalid");return valid; }
        // Only the currently ISSUED leg may lose travelled geometry. Unissued legs
        // remain in full, even when a later leg crosses or approaches the actor.
        // 仅投影当前真实发出的一腿；不能跳到邻近的未来腿。
        private static IReadOnlyList<Vector3> TrimIssuedLeg(IReadOnlyList<Vector3> path,int end,Vector3 pose,float epsilon)
        {
            if(path==null || end<=0 || end>=path.Count) return path;
            for(var i=1;i<=end;i++)
            {
                var direction=path[i]-path[i-1];direction.y=0;
                if(direction.sqrMagnitude<=epsilon*epsilon) continue;
                var offset=pose-path[i-1];offset.y=0;
                var t=Vector3.Dot(offset,direction)/direction.sqrMagnitude;
                if(t<0 || t>1) continue;
                var projected=path[i-1]+direction*t;var error=pose-projected;error.y=0;
                if(error.sqrMagnitude>epsilon*epsilon) continue;
                // Earliest matching edge is conservative at a self-crossing.
                return new[]{pose}.Concat(path.Skip(i)).ToArray();
            }
            return path; // Ambiguous/off-path progress never erases owned geometry.
        }
        private IReadOnlyList<Vector3> RemainingOwnedRoute(Visit visit)
        {
            if(visit.RequestId==0 || visit.OwnedRouteLegEnds==null || visit.OwnedRouteLegEnds.Count==0 ||
                visit.Segments!=visit.OwnedRouteSegments+1) return visit.OwnedRoutePath;
            return TrimIssuedLeg(visit.OwnedRoutePath,visit.OwnedRouteLegEnds[0],visit.Actor.transform.position,visit.Actor.Settings.Epsilon);
        }
        private void RetireArrivedOwnedLeg(Visit visit)
        {
            if(!HasOwnedRoute(visit) || !ReferenceEquals(visit.OwnedRouteSource,visit.Route) ||
                visit.OwnedRouteLegEnds==null || visit.OwnedRouteLegEnds.Count<2 || visit.Segments!=visit.OwnedRouteSegments+1)
            { ClearOwnedRoute(visit,"arrived-binding-unavailable");return; }
            var end=visit.OwnedRouteLegEnds[0];
            visit.OwnedRoutePath=new[]{visit.Actor.transform.position}.Concat(visit.OwnedRoutePath.Skip(end+1)).ToArray();
            visit.OwnedRouteLegEnds=visit.OwnedRouteLegEnds.Skip(1).Select(i=>i-end).ToList();
            visit.OwnedRouteSegments=visit.Segments;visit.OwnedWaitProof=null;
            ObserveCorridor("owned.retained-after-arrival",visit,path:visit.OwnedRoutePath,result:"actual-Arrived-unissued-preserved");
        }
        private IReadOnlyList<Vector3> ProtectedOwnedRoute(Visit visit,string context,Visit incoming,Vector3 target,out IReadOnlyList<Vector3> finiteRoute)
        {
            var path=RemainingOwnedRoute(visit);var obstacles=CorridorObstacles(visit);
            var clear=CustomerRoutePlanner.IsClear(path,obstacles,RouteClearance(visit.Actor.Settings));
            var active=HasOwnedActiveRequest(visit);
            finiteRoute=clear||active?null:CurrentFiniteWaitRoute(visit,path);
            var finite=finiteRoute!=null;
            ObserveCorridor(context+".owned-validity",visit,incoming,target,path,obstacles,clear?"clear":active?"accepted-active-request":finite?"strict-finite-wait":"skip-current-obstacle");
            return clear||active||finite?path:null;
        }
        private IReadOnlyList<Vector3> CurrentFiniteWaitRoute(Visit visit,IReadOnlyList<Vector3> path)
        {
            var proof=visit.OwnedWaitProof;
            if(proof==null || visit.RequestId!=0 || !(visit.PendingPickupSegment||visit.PendingExitSegment) ||
                proof.WaiterGeneration!=visit.Generation || !ReferenceEquals(proof.WaiterRoute,visit.Route) ||
                !visit.Route.SequenceEqual(proof.WaiterUnissued) || proof.Revision!=adapter.Revision) return null;
            var moving=proof.Moving;var actor=moving.Actor;var agent=actor!=null?actor.Agent:null;
            if(!visits.TryGetValue(moving.Id,out var live) || live!=moving || actor==null || actor!=proof.Actor || !actor.isActiveAndEnabled ||
                moving.RequestId!=proof.Request || proof.Request==0 || moving.Generation!=proof.Generation || moving.State!=proof.Phase ||
                !moving.Assignment.Equals(proof.Assignment) || !moving.CurrentSegmentTarget.Equals(proof.Target) ||
                !(moving.Route??Enumerable.Empty<Vector3>()).SequenceEqual(proof.Unissued) ||
                agent==null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped || agent.pathPending ||
                agent.pathStatus!=UnityEngine.AI.NavMeshPathStatus.PathComplete) return null;
            var remaining=TrimIssuedLeg(proof.Path,proof.FirstLegEnd,actor.transform.position,actor.Settings.Epsilon);
            // Only this proven moving BODY may be discounted. Its formal parking target,
            // every other body/reservation and every lease still participates.
            // 仍保留全部正式目标和lease，不将无关活动角色当作通行证明。
            return CustomerRoutePlanner.IsClear(remaining,IndependentTrafficObstacles(moving,visit),RouteClearance(actor.Settings)) &&
                CustomerRoutePlanner.IsClear(path,CorridorObstaclesExceptBody(visit,actor),RouteClearance(visit.Actor.Settings)) &&
                !CustomerRoutePlanner.IsClear(path,new[]{actor.transform.position},RouteClearance(visit.Actor.Settings))?remaining:null;
        }
        private bool NewPickupTargetClear(Visit incoming,Vector3 target)
            => traffic!=null?TrafficTargetProbe(incoming,target):OwnedPickupTargetClear(incoming,target) && PreservesPickupGoalPaths(incoming,target);
        private bool TrafficTargetProbe(Visit incoming,Vector3 target)
        {
            if(incoming?.Actor==null)return false;
            var key=TrafficKey(incoming.Id,"parking-probe",incoming.Id);RefreshTrafficSnapshot();
            var request=new TrafficRequest(key,Time.frameCount,trafficSnapshot,incoming.Actor.transform.position,target,incoming.Actor.Settings.AgentRadius,
                "pickup-probe/"+incoming.Id,requirements:PreservePickupGoals(incoming),safetyMargin:incoming.Actor.Settings.CollisionSkin+incoming.Actor.Settings.Epsilon);
            var decision=traffic.EvaluateParking(request);var clear=decision.Kind==TrafficDecisionKind.Granted;
            if(clear)traffic.DiscardPrepared(key);return clear;
        }
        private bool OwnedPickupTargetClear(Visit incoming,Vector3 target)
        {
            if(traffic!=null)return TrafficTargetProbe(incoming,target);
            ObserveCorridor("target.before-check",incoming,target:target);
            foreach(var earlier in visits.Values.Where(v=>v!=incoming && HasOwnedRoute(v)))
            {
                var path=ProtectedOwnedRoute(earlier,"target",incoming,target,out var finiteRoute);
                if(path==null) continue;
                var clearance=RouteClearance(incoming.Actor.Settings)+2*incoming.Actor.Settings.MaxSpeed*
                    Mathf.Min(Time.deltaTime,incoming.Actor.Settings.MaxSubstepSeconds*incoming.Actor.Settings.MaxSubstepsPerFrame);
                var targetClear=CustomerRoutePlanner.IsClear(path,new[]{target},clearance) &&
                    (finiteRoute==null || CustomerRoutePlanner.IsClear(finiteRoute,new[]{target},clearance));
                ObserveCorridor("target.owned-decision",earlier,incoming,target,path,result:targetClear?"clear":"reject");
                if(!targetClear) return false;
            }
            return true;
        }
        private PickupGoalIdentity[] CurrentPickupGoalOwners()
        {
            var result=new List<PickupGoalIdentity>();
            foreach(var visit in visits.Values)
                if(visit.PickupAssigned && !visit.HasCollected && visit.PickupOwner!=null &&
                    visit.PickupOwner.TryGetReservation(visit.Id,out var assignment))
                    result.Add(new PickupGoalIdentity{Visit=visit,Owner=visit.PickupOwner,Assignment=assignment,
                        Generation=visit.Generation,State=visit.State,InTransit=visit.PickupInTransit,
                        ActorActive=visit.Actor!=null && visit.Actor.isActiveAndEnabled});
            return result.ToArray();
        }
        private Vector3[] PickupGoalObstacles(PickupGoalIdentity[] owners,string ignore)
            => owners.Where(o=>o.Visit.Id!=ignore).Select(o=>o.Assignment.Position)
                .Concat(visits.Values.Where(v=>v.Id!=ignore && ReservesRouteTarget(v,ignore,null)).Select(v=>v.Assignment.Position))
                .Concat(owners.Select(o=>o.Owner).Concat(new[]{pickupQueue}).Where(q=>q!=null).Distinct()
                    .SelectMany(q=>q.ServiceLeases.Where(l=>l.Key!=ignore).Select(l=>l.Value))).Distinct().ToArray();
        private bool PreservesPickupGoalPaths(Visit incoming,Vector3 target)
        {
            if(traffic!=null)return TrafficTargetProbe(incoming,target);
            var owners=CurrentPickupGoalOwners();
            var obstacles=PickupGoalObstacles(owners,null);
            if(ReferenceEquals(pickupGoalRefusalIncoming,incoming) && pickupGoalRefusalGeneration==incoming.Generation &&
                pickupGoalRefusalTarget.Equals(target) && pickupGoalRefusalRevision==adapter.Revision &&
                pickupGoalRefusalOwners!=null && owners.SequenceEqual(pickupGoalRefusalOwners) &&
                obstacles.SequenceEqual(pickupGoalRefusalObstacles))
            { pickupGoalProofStatus="cached-"+pickupGoalRefusalStatus;return false; }
            pickupGoalRefusalIncoming=null;pickupGoalRefusalOwners=null;pickupGoalRefusalObstacles=null;
            if(pickupGoalQueryFrame!=Time.frameCount) { pickupGoalQueryFrame=Time.frameCount;pickupGoalQueries=0; }
            var unknown=false;
            IReadOnlyList<Vector3> Query(Vector3 from,Vector3 to)
            {
                if(pickupGoalQueries>=PickupGoalQueryBudget) { unknown=true;return null; }
                pickupGoalQueries++;return adapter.GetCompletePath(from,to);
            }
            bool Refuse(string status,Visit earlier)
            {
                pickupGoalProofStatus=status;pickupGoalRefusalStatus=status;
                pickupGoalRefusalIncoming=incoming;pickupGoalRefusalGeneration=incoming.Generation;
                pickupGoalRefusalTarget=target;pickupGoalRefusalRevision=adapter.Revision;
                pickupGoalRefusalOwners=owners;pickupGoalRefusalObstacles=obstacles;
                ObserveCorridor("target.goal-proof",earlier,incoming,target,result:status+" queries="+pickupGoalQueries);
                return false;
            }
            foreach(var old in owners.Where(o=>o.Visit!=incoming && o.ActorActive && !o.Assignment.Arrived &&
                o.State!=CustomerVisitState.Blocked && (!o.InTransit || IsPickupMovement(o.State))))
            {
                var visit=old.Visit;var from=visit.Actor.transform.position;
                var clearance=RouteClearance(visit.Actor.Settings);
                var formal=PickupGoalObstacles(owners,visit.Id);
                IReadOnlyList<Vector3> original=null;var expandedOriginal=false;
                var waypoints=old.InTransit
                    ?(visit.RequestId!=0?new[]{visit.CurrentSegmentTarget.Position}.Concat(visit.Route??Enumerable.Empty<Vector3>()):visit.Route)
                    :visit.ReservedPickupRoute;
                if(waypoints!=null)
                {
                    var points=new List<Vector3>{from};var previous=from;var complete=true;
                    foreach(var point in waypoints)
                    {
                        var leg=Query(previous,point);
                        if(leg==null) { complete=false;break; }
                        points.AddRange(leg);previous=point;
                    }
                    if(complete && points.Count>1 && Vector3.Distance(previous,old.Assignment.Position)<.001f &&
                        CustomerRoutePlanner.IsClear(points,formal,clearance)) { original=points;expandedOriginal=true; }
                }
                if(unknown) return Refuse("unknown-budget",visit);
                // Reconnect the current pose; a stale polyline is never a path proof.
                // 动态身体仅在“拒绝新增站位”的比较中排除；实际发腿仍检查全部碰撞。
                if(original==null) original=new CustomerRoutePlanner().Build(from,old.Assignment.Position,formal,clearance,Query);
                if(unknown) return Refuse("unknown-budget",visit);
                if(original==null) continue; // No original path proof grants no permanent priority.
                // Planner returns waypoints; checking those chords could miss curved native legs.
                // 只有展开过的原路线可走快捷检查；其余比较仍由同一个Planner验证完整legs。
                if(expandedOriginal && CustomerRoutePlanner.IsClear(original,new[]{target},clearance)) continue;
                var after=new CustomerRoutePlanner().Build(from,old.Assignment.Position,formal.Concat(new[]{target}).ToArray(),clearance,Query);
                if(unknown) return Refuse("unknown-budget",visit);
                if(after==null) return Refuse("sealed-goal",visit);
            }
            pickupGoalProofStatus="preserved";return true;
        }
        public bool TryMoveToPickup(string visitId,out string reason)
        {
            reason="缺少有效的已预留Pick-up站位";
            if(!CanIntegratedProgress || !visits.TryGetValue(visitId??"",out var visit) || !visit.PickupAssigned || visit.HasCollected) return false;
            if(visit.PickupInTransit) { reason=""; return visit.State!=CustomerVisitState.Blocked; }
            if(traffic!=null) return TryTrafficTransfer(visit,out reason);
            // Revalidate all current bodies/targets/leases before the paid assignment is published.
            // 预留缓存只保护admission；转移必须再次检查真实障碍并接管刚核验的路线。
            if(!TryReservePickup(visitId,out reason)) return false;
            var route=visit.ReservedPickupRoute;
            if(visit.State!=CustomerVisitState.Queued || queue.Snapshot.Count==0 || queue.Snapshot[0].VisitId!=visitId ||
                !visit.PickupOwner.TryBeginTransfer(visitId,out var assignment)) return false;
            visit.CounterPosition=visit.Actor.transform.position; visit.PickupInTransit=true; visit.Assignment=assignment;
            ClearPickupCorridor(visit);
            ObservePickup("transfer.published",visit,visit.PickupOwner,assignment);
            SetRouteContext(visit,CustomerVisitState.MovingToPickup,PickupWaitTarget(assignment,visit.PickupOwner));
            visit.Route=new Queue<Vector3>(route);RememberOwnedRoute(visit,CustomerVisitState.MovingToPickup);SendRouteSegment(visit,CustomerVisitState.MovingToPickup);
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
            if(traffic!=null)return TryTrafficCollection(visit,out reason);
            foreach(var candidate in pickupSides.OrderBy(t=>t.Position==pickupCustomer?0:1))
            {
                var route=BuildRoute(visit.Actor.transform.position,candidate.Position,visit.Id,visit.Actor.Settings);
                ObserveCorridor("collection.candidate-route",visit,target:candidate.Position,path:route);
                if(route==null || route.Count==0 || route.Count>24 ||
                    !OwnedPickupTargetClear(visit,candidate.Position) ||
                    !CustomerRoutePlanner.IsClear(ExpandRoute(visit.Actor.transform.position,route),CorridorObstacles(visit),RouteClearance(visit.Actor.Settings)) ||
                    !PickupDeliveryPathClear(visit.Actor,ExpandRoute(visit.Actor.transform.position,route),RouteObstacles(visit.Id),candidate.Position) ||
                    !PickupCollectionClearsReturn(visit,ExpandRoute(visit.Actor.transform.position,route),candidate.Position) ||
                    !PickupCollectionCanExit(visit,candidate.Position))
                { ObserveCorridor("collection.candidate-rejected",visit,target:candidate.Position,path:route);continue; }
                ObserveCorridor("collection.before-acquire",visit,target:candidate.Position,path:route);
                var acquired=visit.DeliveryYieldPhase==2?visit.PickupOwner.TryAcquireYieldedCollection(visitId,candidate.Position):
                    visit.PickupOwner.TryAcquireCollection(visitId,candidate.Position);
                ObserveCorridor("collection.acquire-result",visit,target:candidate.Position,path:route,result:acquired?"accepted":"refused");
                if(!acquired) continue;
                visit.DeliveryYieldPhase=0; // Same paid owner/slot; generation retires the obsolete yield-return callback.
                visit.Assignment=visit.PickupOwner.Snapshot.First(a=>a.VisitId==visitId); visit.ServiceArrived=false;
                ObservePickup("collection.published",visit,visit.PickupOwner,visit.Assignment);
                // Consume exactly the full owned route checked before taking this side lease.
                // 不抢其他活动顾客的中间waypoint；拒绝候选只是普通等待，不退休FIFO。
                SetRouteContext(visit,CustomerVisitState.MovingToPickupService,candidate);
                visit.Route=new Queue<Vector3>(route);
                RememberOwnedRoute(visit,CustomerVisitState.MovingToPickupService);
                ObservePickup("collection.route-selected",visit,visit.PickupOwner,visit.Assignment);
                SendRouteSegment(visit,CustomerVisitState.MovingToPickupService);
                reason=""; return true;
            }
            return false;
        }
        private bool PickupCollectionCanExit(Visit visit,Vector3 side)
        {
            // Eligibility only: no future route/lease is published or retained.
            // 所选服务侧必须可离店；完整身体、目标、lease及既有traffic检查仍保留。
            var occupied=RouteObstacles(visit.Id).Concat(CorridorObstacles(visit)).Distinct().ToArray();
            PickupCounterEnvelope(visit,out var counterHead,out var envelope);
            var envelopeCenters=counterHead.HasValue?new[]{counterHead.Value}:Array.Empty<Vector3>();
            occupied=occupied.Concat(envelopeCenters).Distinct().ToArray();
            var query=CreatePickupEligibilityQuery(visit,counterHead,envelope);
            var route=PlanPickupExitUsingQuery(visit,"collection.eligibility",side,exit.position,occupied,visit.Actor.Settings,query,"filtered-eligibility");
            if(route==null || route.Count==0 || route.Count>24)
            {
#if UNITY_EDITOR
                if(CorridorObserver!=null) ObserveCorridor("collection.exit-eligibility-result",visit,target:side,obstacles:occupied,
                    result:"route="+(route==null?"NULL":route.Count.ToString())+" delivery=NOT_EVALUATED return=NOT_EVALUATED eligible=False");
#endif
                return false;
            }
            var path=ExpandRoute(side,route);
            // Re-expansion must preserve the same local eligibility envelope, without a new native query.
            // 最终展开仍须避开同一Counter包络；不发布未来离店路线或ownership。
            if(counterHead.HasValue && !CustomerRoutePlanner.IsClear(path,envelopeCenters,envelope))
            {
#if UNITY_EDITOR
                if(CorridorObserver!=null) ObserveCorridor("collection.exit-eligibility-result",visit,target:side,path:path,obstacles:occupied,
                    result:"route="+route.Count+" envelope=False delivery=NOT_EVALUATED return=NOT_EVALUATED eligible=False");
#endif
                return false;
            }
            var deliveryClear=PickupDeliveryPathClear(visit.Actor,path,occupied,exit.position);
            // Observe the original short circuit; diagnostics never repeat either guard.
            // 只记录原判定：delivery拒绝时不调用return guard。
            var returnClear=deliveryClear && PickupCollectionClearsReturn(visit,path,exit.position);
#if UNITY_EDITOR
            if(CorridorObserver!=null) ObserveCorridor("collection.exit-eligibility-result",visit,target:side,path:path,obstacles:occupied,
                result:"route="+route.Count+" delivery="+deliveryClear+" return="+(deliveryClear?returnClear.ToString():"NOT_EVALUATED")+" eligible="+(deliveryClear&&returnClear));
#endif
            return deliveryClear && returnClear;
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
        private static void ClearEmployeeExitWait(Visit visit)
        { visit.EmployeeExitWaitAttempt=0;visit.EmployeeExitWaitActor=null;visit.EmployeeExitWaitRoute=null; }
        private bool CanWaitForOwnedEmployeeExitTraffic(Visit visit)
        {
            if(!CanIntegratedProgress || visit.State!=CustomerVisitState.Exiting || !visit.HasCollected || visit.RequestId!=0 ||
                visit.Route==null || visit.Route.Count==0 || deliveryAttempt==0) return false;
            var actor=world.RegisteredActors.FirstOrDefault(a=>a!=null&&a.ActorId==deliveryActorId);
            if(actor==null || !actor.isActiveAndEnabled || visit.EmployeeExitWaitAttempt!=0 &&
                (visit.EmployeeExitWaitAttempt!=deliveryAttempt || visit.EmployeeExitWaitActor!=actor ||
                    visit.EmployeeExitWaitGeneration!=visit.Generation || visit.EmployeeExitWaitRevision!=adapter.Revision ||
                    !ReferenceEquals(visit.EmployeeExitWaitRoute,visit.Route))) return false;
            var agent=actor.Agent;
            // Finish/Cancel stops the real driver. A selected but idle/pending plan is insufficient.
            // 只允许当前真实运动提供有限依据；取消、停用、未发腿或静态堵塞不授权无限等候。
            if(agent==null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped || agent.pathPending ||
                agent.pathStatus!=UnityEngine.AI.NavMeshPathStatus.PathComplete || !actor.SteadyPathMotion&&!agent.hasPath) return false;
            var targets=pickupReturnTraffic?.Invoke(deliveryActorId);
            if(targets==null || targets.Count<2 || targets.Count>25) return false;
            var proposed=ExpandRoute(visit.Actor.transform.position,visit.Route);
            var remaining=ExpandRoute(targets[0].Position,targets.Skip(1).Select(t=>t.Position));
            if(proposed==null || remaining==null) return false;
            var clearance=RouteClearance(visit.Actor.Settings)+2*visit.Actor.Settings.MaxSpeed*
                Mathf.Min(Time.deltaTime,visit.Actor.Settings.MaxSubstepSeconds*visit.Actor.Settings.MaxSubstepsPerFrame);
            if(!RoutesConflict(proposed,remaining,clearance) &&
                CustomerRoutePlanner.IsClear(remaining,new[]{visit.FinalTarget.Position},clearance)) return false;
            var occupied=RouteObstacles(actor.ActorId).Concat(PickupGoalObstacles(CurrentPickupGoalOwners(),actor.ActorId)).Distinct().ToArray();
            return CustomerRoutePlanner.IsClear(remaining,occupied,RouteClearance(actor.Settings));
        }
        private bool HoldNewExitForOwnedEmployee(Visit visit)
        {
            if(!CanWaitForOwnedEmployeeExitTraffic(visit)) return false;
            // This later, unissued candidate must not block the earlier employee's next leg.
            // 保留选定Route及预算，不让后选的未发路线反过来抢占先有员工通道。
            ClearOwnedRoute(visit,"earlier-owned-employee-exit-wait");
            visit.EmployeeExitWaitAttempt=deliveryAttempt;visit.EmployeeExitWaitActor=world.RegisteredActors.Single(a=>a.ActorId==deliveryActorId);
            visit.EmployeeExitWaitGeneration=visit.Generation;visit.EmployeeExitWaitRevision=adapter.Revision;
            visit.EmployeeExitWaitRoute=visit.Route;visit.PendingExitSegment=true;return true;
        }
        private IReadOnlyList<Vector3> IndependentPickupClaimRoute(PickupExitClaim claim,Visit waiter)
        {
            var owner=claim.Owner;
            if(!visits.TryGetValue(owner.Id,out var live) || live!=owner || owner.Actor!=claim.Actor ||
                owner.State!=claim.Phase || !owner.Assignment.Equals(claim.Assignment) ||
                !ReferenceEquals(owner.PickupOwner,claim.PickupOwner) || !ReferenceEquals(owner.Route,claim.Route) ||
                !HasOwnedRoute(owner)) return null;
            IReadOnlyList<Vector3> remaining;
            if(owner.RequestId==0)
            {
                if(!owner.PendingPickupSegment) return null;
                remaining=RemainingOwnedRoute(owner);
            }
            else
            {
                var agent=owner.Actor.Agent;
                if(!HasOwnedActiveRequest(owner) || agent==null || !agent.enabled || !agent.isOnNavMesh ||
                    agent.isStopped || agent.pathPending || agent.pathStatus!=UnityEngine.AI.NavMeshPathStatus.PathComplete) return null;
                remaining=ExpandRoute(owner.Actor.transform.position,new[]{owner.CurrentSegmentTarget.Position}
                    .Concat(owner.Route??Enumerable.Empty<Vector3>()));
            }
            // A stored promise alone cannot authorize WAIT through a static obstruction.
            // 保留等待者真实身体、所有正式目标及lease；仅独立可完成的原路线保留优先权。
            var obstacles=IndependentTrafficObstacles(owner,waiter).Concat(ReservedPickupTargets(owner.Id)).Distinct().ToArray();
            return remaining!=null && CustomerRoutePlanner.IsClear(remaining,obstacles,
                RouteClearance(owner.Actor.Settings))?remaining:null;
        }
        private bool PickupClaimConflictsWithExit(Visit visit,PickupExitClaim claim,IReadOnlyList<Vector3> proposed)
        {
            var remaining=IndependentPickupClaimRoute(claim,visit);
            if(remaining==null) return false;
            var clearance=RouteClearance(visit.Actor.Settings);
            return RoutesConflict(proposed,remaining,clearance) ||
                !CustomerRoutePlanner.IsClear(remaining,new[]{visit.FinalTarget.Position},clearance);
        }
        private bool CanWaitForOwnedPickupExitTraffic(Visit visit)
        {
            var wait=visit.PickupPriorityWait;
            if(wait==null || !CanIntegratedProgress || visit.State!=CustomerVisitState.Exiting || !visit.HasCollected ||
                visit.RequestId!=0 || wait.Generation!=visit.Generation || wait.Revision!=adapter.Revision ||
                !ReferenceEquals(wait.Route,visit.Route) || visit.Route==null || visit.Route.Count==0) return false;
            var proposed=ExpandRoute(visit.Actor.transform.position,visit.Route);
            if(proposed==null) return false;
            wait.Claims.RemoveAll(claim=>!PickupClaimConflictsWithExit(visit,claim,proposed));
            return wait.Claims.Count>0;
        }
        private bool HoldNewExitForOwnedPickup(Visit visit)
        {
            if(!CanIntegratedProgress || visit.State!=CustomerVisitState.Exiting || !visit.HasCollected || visit.RequestId!=0 ||
                visit.Route==null || visit.Route.Count==0 || ReferenceEquals(visit.PickupPriorityCheckedRoute,visit.Route)) return false;
            visit.PickupPriorityCheckedRoute=visit.Route;
            var proposed=ExpandRoute(visit.Actor.transform.position,visit.Route);
            if(proposed==null) return false;
            var wait=new PickupExitWait{Generation=visit.Generation,Revision=adapter.Revision,Route=visit.Route};
            foreach(var owner in visits.Values.Where(v=>v!=visit && v.RequestId==0 && v.PendingPickupSegment &&
                IsPickupMovement(v.State) && HasOwnedRoute(v)))
            {
                var claim=new PickupExitClaim{Owner=owner,Actor=owner.Actor,PickupOwner=owner.PickupOwner,
                    Route=owner.Route,Phase=owner.State,Assignment=owner.Assignment};
                if(PickupClaimConflictsWithExit(visit,claim,proposed)) wait.Claims.Add(claim);
            }
            if(wait.Claims.Count==0) return false;
            // Only owners present at this acquisition can delay this candidate. Never
            // acquire younger claims on unchanged pending frames or publish the later exit as owner.
            // 绑定本次取得时的先有路线；不逐帧纳入后客，不让后来离店反过来挡住先客。
            ClearOwnedRoute(visit,"earlier-clear-unissued-pickup-exit-wait");
            visit.PickupPriorityWait=wait;visit.PendingExitSegment=true;return true;
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
            ObserveCorridor("exit.actual-dispatch",visit,target:exit.position);
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
            if(traffic!=null)
            {
                if(trafficDeliveryGate==null || !trafficDeliveryGate(employeeId))return false;
                selected=trafficDeliveryTarget;route=trafficDeliveryOutward;returning=trafficDeliveryHome;return true;
            }
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
                        ObservePickup("yield.published",waiter,waiter.PickupOwner,assignment);
                        SetRouteContext(waiter,CustomerVisitState.PickupAdvancing,new NavigationTarget(target));
                        waiter.Route=new Queue<Vector3>(route);RememberOwnedRoute(waiter,CustomerVisitState.PickupAdvancing);
                        SendRouteSegment(waiter,CustomerVisitState.PickupAdvancing);return;
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
            ObserveDeliveryAcquisition("static-refusal",actor,null,occupied,clearance);
        }

        private bool HasDeliveryTraffic(NavigationActor requester,IReadOnlyList<IReadOnlyList<Vector3>> paths)
        {
            // The acquisition guard and finite wait classification share one proof.
            // 同一真实pending ownership不能在拒绝acquire后被误判为静态Blocked。
            var physicalClearance=RouteClearance(requester.Settings);var occupied=RouteObstacles(requester.ActorId);
            // Owner progress does not prove that an incoming candidate can cross another static body.
            // 与acquisition guard一致：完整身体/目标/lease检查先通过，才分类新增ownership WAIT。
            ObserveDeliveryAcquisition("traffic.begin",requester,null,occupied,physicalClearance);
            if(paths.Any(path=>
            {
                // Record original results only; null means short-circuited / null表示原分支未计算。
                if(path==null || path.Count==0)
                { ObserveDeliveryAcquisition("traffic.candidate",requester,path,occupied,physicalClearance);return false; }
                var physical=CustomerRoutePlanner.IsClear(path,occupied,physicalClearance);
                if(!physical)
                {
                    ObserveDeliveryAcquisition("traffic.candidate",requester,path,occupied,physicalClearance,false);
                    return HasPendingServiceReservationTraffic(requester,path,physicalClearance);
                }
                var owned=OwnedServiceRouteConflicts(requester,path,path[path.Count-1]);
                ObserveDeliveryAcquisition("traffic.candidate",requester,path,occupied,physicalClearance,true,owned);
                return owned;
            })) return true;
            var clearance=physicalClearance+2*requester.Settings.MaxSpeed*
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
        private bool HasPendingServiceReservationTraffic(NavigationActor requester,IReadOnlyList<Vector3> path,float clearance)
        {
            foreach(var owner in visits.Values.Where(v=>v.RequestId==0 && v.State==CustomerVisitState.MovingToPickupService &&
                v.PendingPickupSegment && !v.ServiceArrived && !v.HasCollected && HasOwnedRoute(v)))
            {
                if(!ReferenceEquals(owner.PickupOwner,pickupQueue) ||
                    !owner.PickupOwner.TryGetServiceLease(owner.Id,out var lease) || lease!=owner.Assignment.Position ||
                    Vector3.Distance(owner.Actor.transform.position,lease)<=owner.Actor.Settings.ArrivalDistance+owner.Actor.Settings.Epsilon ||
                    CurrentFiniteWaitRoute(owner,RemainingOwnedRoute(owner))==null) continue;
                // Classification only: the live proof is rechecked on every call, not a promise of indefinite lease release.
                // 仅WAIT分类按身份略去这个未占用reservation；全部真实身体和外来同坐标来源仍保留。
                var waitingObstacles=RouteObstaclePositions(requester.ActorId,null,false,owner).Distinct().ToArray();
                if(CustomerRoutePlanner.IsClear(path,waitingObstacles,clearance)) return true;
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
            var history=pickupDeliveryHistory;
            if(PickupDeliveryHistoryCurrent(history) && ReferenceEquals(actor,history.Actor))
            {
                if(!home)history.WasAway=true;
                if(home && history.WasAway && history.Delivered)history.HomeObserved=true;
            }
            if(!deliveryWasAway || !home) return;
            if(visits.Values.Any(v=>v.DeliveryYieldPhase==1 || v.DeliveryYieldPhase==3)) return;
            var held=visits.Values.Where(v=>v.DeliveryYieldPhase==2).OrderBy(v=>v.Assignment.SlotIndex).ToArray();
            if(traffic!=null)
            {
                foreach(var waiting in held)
                    if(waiting.PickupOwner.TryGetYieldReturn(waiting.Id,out var returning))
                    {TryTrafficPickupMove(waiting,PickupQueueService.MutationKind.ReturnYield,returning.Position,"pickup-yield-return",
                        CustomerVisitState.PickupAdvancing,"yield-return.published",out _);return;}
                if(held.Length==0){deliveryAcquired=false;deliveryWasAway=false;}
                return;
            }
            Visit blockedReturn=null;
            foreach(var visit in held)
            {
                if(!visit.PickupOwner.TryGetYieldReturn(visit.Id,out var candidate)) continue;
                // A protected owned corridor is an ordinary deferred target, not a new
                // static failure. 不让下方Blocked fallback先发布被保护通道上的目标。
                if(!NewPickupTargetClear(visit,candidate.Position)) continue;
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
                ObservePickup("yield-return.published",visit,visit.PickupOwner,assignment);
                Send(visit,CustomerVisitState.PickupAdvancing,PickupWaitTarget(assignment,visit.PickupOwner));
                return; // Restore one real body at a time, with normal remaining-route protection.
            }
            if(blockedReturn!=null && blockedReturn.PickupOwner.TryReturnYielded(blockedReturn.Id,out var blockedTarget))
            {
                blockedReturn.Assignment=blockedTarget;blockedReturn.DeliveryYieldPhase=3;
                ObservePickup("yield-return-blocked.published",blockedReturn,blockedReturn.PickupOwner,blockedTarget);
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
        internal bool ShouldWaitForPickupDelivery(string actorId,NavigationTarget target)
        {
            if(!CanIntegratedProgress || !deliveryAcquired || deliveryActorId!=actorId || pickupQueue==null ||
                !pickupQueue.TryGetServiceLease(actorId,out _)) return false;
            var actor=world.RegisteredActors.FirstOrDefault(a=>a!=null && a.ActorId==actorId && a.isActiveAndEnabled);
            if(actor==null) return false;
            var path=adapter.GetCompletePath(actor.transform.position,target.Position);
            // Only selected, leased, unissued work may wait for relevant finite traffic.
            // 静止/未知body或缺失路径不会授权等待；清空后仍通过原完整segment guard。
            return path!=null && !PickupDeliveryPathClear(actor,path,RouteObstacles(actorId),target.Position) &&
                HasDeliveryTraffic(actor,new[]{path});
        }
        private bool PickupDeliveryPathClear(NavigationActor actor,IReadOnlyList<Vector3> path,Vector3[] occupied,Vector3 goal)
        {
            var pathClear=CustomerRoutePlanner.IsClear(path,occupied,RouteClearance(actor.Settings));
#if UNITY_EDITOR
            CorridorObserver?.Invoke("employee.path-check",actor.ActorId,null,goal,path,occupied,pathClear?"clear":"blocked");
#endif
            if(!pathClear) return false;
            if(OwnedServiceRouteConflicts(actor,path,goal)) return false;
            var clearance=RouteClearance(actor.Settings)+2*actor.Settings.MaxSpeed*
                Mathf.Min(Time.deltaTime,actor.Settings.MaxSubstepSeconds*actor.Settings.MaxSubstepsPerFrame);
            // Collection eligibility queries a possible future exit, not an owned exit.
            // Preserve the original customer pipeline; actual customer legs have their own
            // owned-route guard. Only later employee candidates gain this extra exit check.
            // 顾客未来出口不提前占权；员工新路线仍须避让已发出的真实离场路线。
            var employeeCandidate=!visits.ContainsKey(actor.ActorId);
            foreach(var moving in visits.Values.Where(v=>v.Id!=actor.ActorId && v.RequestId!=0 && v.Actor!=null && v.Actor.isActiveAndEnabled &&
                (IsPickupMovement(v.State) || employeeCandidate && v.State==CustomerVisitState.Exiting && v.HasCollected)))
            {
                var remaining=ExpandRoute(moving.Actor.transform.position,new[]{moving.Actor.Agent.destination}.Concat(moving.Route??Enumerable.Empty<Vector3>()));
                var refused=remaining==null || RoutesConflict(path,remaining,clearance) || !CustomerRoutePlanner.IsClear(remaining,new[]{goal,path[path.Count-1]},clearance);
#if UNITY_EDITOR
                CorridorObserver?.Invoke("employee.moving-conflict",actor.ActorId,moving.Id,goal,remaining,null,refused?"refused":"clear");
#endif
                if(refused) return false;
            }
            return true;
        }
        private bool OwnedServiceRouteConflicts(NavigationActor actor,IReadOnlyList<Vector3> path,Vector3 goal)
            => PendingOwnedRouteConflicts(actor.ActorId,actor.Settings,path,goal,"employee");
        private bool PendingOwnedRouteConflicts(string requesterId,NavigationSettings settings,IReadOnlyList<Vector3> path,Vector3 goal,string purpose)
        {
            var clearance=RouteClearance(settings)+2*settings.MaxSpeed*
                Mathf.Min(Time.deltaTime,settings.MaxSubstepSeconds*settings.MaxSubstepsPerFrame);
            // Issued legs retain the existing actual-pose guard above and traffic loop.
            // 这里只补未发腿；不拿保守缓存的已走prefix强化原active guard。
            foreach(var owned in visits.Values.Where(v=>v.Id!=requesterId && v.RequestId==0 && HasOwnedRoute(v)))
            {
                var remaining=ProtectedOwnedRoute(owned,purpose,null,goal,out var contributor);
                if(remaining==null) continue;
                // A later route/parking target must also leave the finite contributor independent.
                // 同时保护证明中真实腾位者，不能制造双向WAIT循环。
                if(RoutesConflict(path,remaining,clearance) || !CustomerRoutePlanner.IsClear(remaining,new[]{goal},clearance) ||
                    contributor!=null && (RoutesConflict(path,contributor,clearance) || !CustomerRoutePlanner.IsClear(contributor,new[]{goal},clearance))) return true;
            }
            return false;
        }
        public void ReleasePickupServicePosition(string actorId) { pickupQueue?.ReleaseServicePosition(actorId); }
        public void ReleasePickupAssignment(string visitId)
        {
            if(!visits.TryGetValue(visitId??"",out var visit) || !visit.PickupAssigned) return;
            // 正常付费移动不走此取消入口；只用于未使用预留或明确的owned清理。
            if(visit.PickupInTransit || visit.HasCollected || visit.PaidPickup!=null) return;
            if(visit.PendingTraffic!=null)
            {
                // Cancel only the unused new parking permit; the arrived Counter obligation still owns the body.
                if(visit.PickupTrafficPermit!=null && !visit.PickupTrafficPermit.Intent.Equals(visit.PendingTraffic.Intent))
                    traffic?.Retire(visit.PickupTrafficPermit.Intent);
            }
            else if(visit.TrafficJourney.HasValue){traffic?.Retire(visit.TrafficJourney.Value);visit.TrafficJourney=null;}
            visit.PickupOwner.Remove(visitId); visit.PickupAssigned=false;
            visit.TrafficPhaseSequence++;visit.PickupTrafficPermit=null;visit.PickupParking=null;
            ObserveCorridor("reservation.unused-removed",visit);
            ClearPickupCorridor(visit);
        }
        public void ClearUncollectedVisits(IReadOnlyCollection<string> collectedVisitIds)
        {
            ObserveCorridor("layout.retirement");
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
        internal void RememberCollectionAttempt(string visitId,CafeOrderKey key,int frame)
        {
            if(frame<0 || !visits.TryGetValue(visitId??"",out var visit) || visit.HasCollected || visit.CollectedOrder.HasValue)return;
            visit.CollectionAttempt=key;visit.CollectionAttemptFrame=frame;
        }
        internal bool LatchCanonicalCollection(string visitId,CafeOrderKey key,int observedFrame)
        {
            if(!visits.TryGetValue(visitId??"",out var visit) || visit.Actor==null || visit.Admission==null)return false;
            if(visit.CollectedOrder.HasValue)return visit.CollectedOrder.Value.Equals(key);
            // CanCollect and the service's successful Complete run synchronously in this original frame.
            // 只有原Ready检查与真实Completed/collected匹配才锁存；合并/迟投影不能重排年龄。
            if(!visit.CollectionAttempt.HasValue || !visit.CollectionAttempt.Value.Equals(key) ||
                visit.CollectionAttemptFrame<0 || visit.CollectionAttemptFrame>observedFrame)return false;
            visit.CollectedOrder=key;visit.CollectionFrame=visit.CollectionAttemptFrame;
            if(visit.ExitSequence==0)visit.ExitSequence=++nextExitSequence;
            return true; // No HasCollected, presentation, navigation or new business allocation.
        }
        private static string CollectionTrafficContext(Visit visit)
        {
            var context=visit.Id+"/collection/"+visit.ExitSequence;
            if(visit.CollectedOrder.HasValue)
            {var key=visit.CollectedOrder.Value;context+="/"+key.SessionId+"/"+key.Epoch+"/"+key.OrderId;}
            return context;
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
                var preserveSuspendedTraffic=traffic!=null && revision==adapter.Revision && suspended;
                // 即使adapter已同步rebuild，也必须退休上一revision的已终止请求。
                if(queue!=null && !preserveSuspendedTraffic) HoldVisits();
                if(runtime!=null) Capacity.UpdateFloorCellCount(FloorCapacitySource.CountInteriorCells(runtime.Layout));
                if(!BuildQueue()) { HoldVisits(); clock.Tick(0,false,false); return; }
                revision=adapter.Revision; suspended=false;
                if(!preserveSuspendedTraffic) ResumeBlockedVisits();
            }
            RestoreDeliveryYielders();
            foreach(var visit in visits.Values.ToArray())
            {
                if(visit.Actor==null) { RemoveVisit(visit.Id); continue; }
                if(visit.PendingCounter!=null)
                {
                    ResumeTrafficCounterMove(visit);
                    if(!visits.ContainsKey(visit.Id))continue;
                }
                if(traffic!=null && visit.PendingExitSegment && visit.Route==null && visit.TrafficMove==null && visit.State==CustomerVisitState.Exiting)
                { TryPrepareTrafficExit(visit);if(!visits.ContainsKey(visit.Id))continue; }
                if(traffic!=null && visit.TrafficMovePending)
                { SendRouteSegment(visit,visit.State);if(!visits.ContainsKey(visit.Id))continue; }
                if(traffic==null && visit.PendingPickupSegment && IsPickupMovement(visit.State))
                    SendRouteSegment(visit,visit.State); // Recheck the retained candidate; no A* while traffic is moving.
                if(traffic==null && visit.PendingExitSegment && visit.State==CustomerVisitState.Exiting && CanIntegratedProgress)
                {
                    if(visit.PickupPriorityWait!=null)
                    {
                        if(CanWaitForOwnedPickupExitTraffic(visit)) continue;
                        visit.PickupPriorityWait=null;
                        if(visit.Route!=null&&visit.Route.Count>0)RememberOwnedRoute(visit,CustomerVisitState.Exiting);
                    }
                    if(visit.EmployeeExitWaitAttempt!=0)
                    {
                        if(CanWaitForOwnedEmployeeExitTraffic(visit)) continue;
                        ClearEmployeeExitWait(visit);
                        if(visit.Route!=null&&visit.Route.Count>0)RememberOwnedRoute(visit,CustomerVisitState.Exiting);
                    }
                    if(visit.Route==null)
                    {
                        var resumed=integratedMode
                            ?PlanPickupExitForCaller(visit,"pending.actual",visit.Actor.transform.position,visit.FinalTarget.Position,RouteObstacles(visit.Id),visit.Actor.Settings)
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
                    ObserveCorridor("lease.departure-before-remove",visit);
                    visit.PickupOwner?.Remove(visit.Id); visit.PickupOwner?.ReleaseServicePosition(visit.Id); visit.PickupAssigned=false;
                    ObserveCorridor("lease.departure-removed",visit);
                }
            }
            var blocked=visits.Values.Any(v=>v.State==CustomerVisitState.Blocked);
            if(traffic!=null)AdvanceTrafficCounterQueue();
            if((!blocked || recoveringQueue) && traffic==null && queue.TryGetNextAdvance(out var assignment,CanBeginAdvance) && visits.TryGetValue(assignment.VisitId,out var next))
            { next.Assignment=assignment; Send(next,CustomerVisitState.Advancing,QueueTarget(assignment)); }
            if(integratedMode && pickupQueue!=null && !visits.Values.Any(v=>v.DeliveryYieldPhase!=0) && traffic!=null)
                AdvanceTrafficPickupQueue();
            if(integratedMode && pickupQueue!=null && !blocked && !visits.Values.Any(v=>v.DeliveryYieldPhase!=0) && traffic==null && pickupQueue.TryGetNextAdvance(out var pickupAdvance,CanBeginPickupAdvance) &&
                visits.TryGetValue(pickupAdvance.VisitId,out var pickupVisit))
            {
                pickupVisit.Assignment=pickupAdvance; pickupVisit.ServiceArrived=false;
                ObservePickup("advance.published",pickupVisit,pickupVisit.PickupOwner,pickupAdvance);
                Send(pickupVisit,CustomerVisitState.PickupAdvancing,PickupWaitTarget(pickupAdvance,pickupVisit.PickupOwner));
            }
            blocked=visits.Values.Any(v=>v.State==CustomerVisitState.Blocked); // 同步提交/恢复可能已经改变状态。
            // 满员等营业条件取消本轮；临时入口/队尾/路径门禁由TrySpawn保留到期机会。
            var policyAllows=AdmissionPolicyAllows();
            var canWait=policyAllows && (!integratedMode || integratedAdmissionAllowed) && !blocked && Capacity.CanAdmit && queue.Snapshot.Count<queue.AvailableSlotCount;
            if(autoSpawn && clock.Tick(delta,canWait,false) && TrySpawn(true)) clock.CompleteAdmission();
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
            else if(!policyAllows) Status=admissionPolicyFailure;
            else if(UnreservedFrontHoldsAdmission()) Status="等待安全取餐站位";
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
        public bool TrySpawn() => TrySpawn(false);
        private bool TrySpawn(bool automatic)
        {
            if(AdmissionRefused("spawn-in-progress",admissionInProgress))return false;
            // 所有入口先读实时policy；任何capacity reserve或Actor生成都在此之后。
            if(AdmissionRefused("spawn-policy",!AdmissionPolicyAllows())) return false;
            if(AdmissionRefused("spawn-inactive",!isActiveAndEnabled) || AdmissionRefused("spawn-unconfigured",!Configured) || AdmissionRefused("spawn-world-inactive",!world.isActiveAndEnabled) || AdmissionRefused("spawn-layout",!world.LayoutAvailable) || AdmissionRefused("spawn-no-queue",queue==null) ||
                integratedMode && (AdmissionRefused("spawn-admission-disabled",!integratedAdmissionAllowed) || AdmissionRefused("spawn-integrated-progress",!CanIntegratedProgress) || AdmissionRefused("spawn-total-opportunities",exitSnapshots.Count+visits.Count>=Capacity.Limits.TotalCustomers)) ||
                AdmissionRefused("spawn-decoration",decoration!=null && decoration.IsOpen) || AdmissionRefused("spawn-queue-canjoin",!queue.CanJoin) || AdmissionRefused("spawn-capacity",!Capacity.CanAdmit) ||
                AdmissionRefused("spawn-blocked-visit",visits.Values.Any(v=>v.State==CustomerVisitState.Blocked)) || AdmissionRefused("spawn-entry-clear",!EntryClear()) || AdmissionRefused("spawn-tail-clear",!TailClear())) return false;
            // Pure business eligibility precedes both integrated and legacy spatial admission.
            // 不创建/重置原unborn source；正常clock仅在真实成功出生后消费这一个Due机会。
            if(UnreservedFrontHoldsAdmission())
            {
                Status="等待安全取餐站位";
                ObserveCorridor("admission.unreserved-front-held",refusedPickupFront);
                return false;
            }
            if(traffic!=null)return TryTrafficAdmission(automatic);
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
#if UNITY_EDITOR
                visit.AdmissionObservation=admissionObservation;
#endif
                visits.Add(id,visit);
                ObserveCorridor("admission.published",visit,target:visit.Assignment.Position);
                ObserveAdmissionDecision("admission.original-published",target:visit.Assignment.Position,incoming:visit);
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
            ClearOwnedRoute(visit);
            visit.TrafficMove=null;visit.TrafficMovePending=false;visit.TrafficSelection=null;visit.IssuedTraffic=null;
            if(phase==CustomerVisitState.Advancing) visit.AdvanceStart=visit.Actor.transform.position;
            visit.Generation++; visit.RequestId=0; visit.State=visit.ResumeState=phase;
            visit.FinalTarget=target; visit.Segments=visit.Replans=0; visit.Failure=null;
            visit.PendingExitSegment=visit.PendingPickupSegment=false;
            visit.PickupPriorityCheckedRoute=null;
            visit.YieldReturnTraffic=null;
        }
        private void Send(Visit visit,CustomerVisitState phase,NavigationTarget target)
        {
            SetRouteContext(visit,phase,target);
            if(traffic!=null && phase==CustomerVisitState.Exiting)
            {visit.Route=null;visit.PendingExitSegment=true;TryPrepareTrafficExit(visit);return;}
            if(traffic!=null)
            { visit.Route=null;visit.TrafficMovePending=true;SendRouteSegment(visit,phase);return; }
            var route=phase==CustomerVisitState.MovingToPickup || phase==CustomerVisitState.PickupAdvancing
                ?BuildPickupWaitRoute(visit.Actor.transform.position,target.Position,visit.Id,visit.Actor.Settings)
                :integratedMode && phase==CustomerVisitState.Exiting
                ?PlanPickupExitForCaller(visit,"send.actual",visit.Actor.transform.position,target.Position,RouteObstacles(visit.Id),visit.Actor.Settings)
                :BuildRoute(visit.Actor.transform.position,target.Position,visit.Id,visit.Actor.Settings);
            ObserveCorridor("route.actual-plan",visit,target:target.Position,path:route);
            if(route==null && phase==CustomerVisitState.Exiting)
            {
                if(TryPlanIndependentExitWait(visit,out var waitingRoute))
                { visit.Route=new Queue<Vector3>(waitingRoute); visit.PendingExitSegment=true; return; }
                if(CanWaitForDepartingExitBody(visit))
                { visit.Route=null; visit.PendingExitSegment=true; return; }
            }
            if(route==null || route.Count>24) { BlockVisit(visit,phase,"没有安全通道"); return; }
            visit.Route=new Queue<Vector3>(route);
            if(phase==CustomerVisitState.Exiting&&(HoldNewExitForOwnedEmployee(visit)||HoldNewExitForOwnedPickup(visit)))return;
            RememberOwnedRoute(visit,phase); SendRouteSegment(visit,phase);
            ObservePickup("route.selected",visit,visit.PickupOwner,visit.Assignment);
        }
        private IReadOnlyList<Vector3> BuildRoute(Vector3 from,Vector3 to,string ignore,NavigationSettings settings,int? advancingSlot=null,bool prospectiveAdmission=false)
        {
            var occupied=RouteObstacles(ignore,advancingSlot,prospectiveAdmission);
            var planned=new CustomerRoutePlanner().Build(from,to,occupied,RouteClearance(settings),adapter.GetCompletePath);
            ObserveCorridor("plan.build",visits.TryGetValue(ignore??"",out var subject)?subject:null,target:to,path:planned,obstacles:occupied,result:planned==null?"NULL":"route");
            return planned;
        }
        private bool PickupPlanCanExit(Visit visit,Vector3 head,Vector3[] futureTail)
        {
            var occupied=RouteObstacles(visit.Id).Concat(futureTail).Distinct().ToArray();
            var clearance=RouteClearance(visit.Actor.Settings);
            PickupCounterEnvelope(visit,out var counterHead,out var envelope);
            // Keep hypothetical occupancy local to eligibility; the actual caller can leave Counter.
            var envelopeCenters=counterHead.HasValue?new[]{counterHead.Value}:Array.Empty<Vector3>();
            occupied=occupied.Concat(envelopeCenters).Distinct().ToArray();
            var query=CreatePickupEligibilityQuery(visit,counterHead,envelope);
            // The collecting front vacates its head; all future paid followers remain obstacles.
            return pickupSides.Any(side=>new CustomerRoutePlanner().Build(head,side.Position,occupied,clearance,query)!=null &&
                PlanPickupExitUsingQuery(visit,"pickup.eligibility",side.Position,exit.position,occupied,visit.Actor.Settings,query,"filtered-eligibility")!=null);
        }
        private void PickupCounterEnvelope(Visit visit,out Vector3? counterHead,out float envelope)
        {
            counterHead=integratedMode && counterWaitSlots.Length>0 &&
                runtime?.CurrentReadiness?.Stations.Any(s=>s.IsValid && s.FunctionType==LayoutStationType.CashRegister &&
                    s.InstanceId==SelectedRegisterId)==true ? counterWaitSlots[0] : (Vector3?)null;
            // A real Counter arrival is measured from its sampled destination, not the raw slot.
            // 两个eligibility门共用连续包络：future body + sample allowance + arrival allowance。
            envelope=counterHead.HasValue ? visit.Actor.Settings.AgentRadius+visit.Actor.Settings.CollisionSkin+visit.Actor.Settings.Epsilon+
                prefabs.Max(p=>p.Settings.AgentRadius+p.Settings.TargetSampleDistance+p.Settings.Epsilon+p.Settings.ArrivalDistance) : 0;
        }
        private Func<Vector3,Vector3,IReadOnlyList<Vector3>> CreatePickupEligibilityQuery(Visit visit,Vector3? counterHead,float envelope)
        {
            var envelopeCenters=counterHead.HasValue?new[]{counterHead.Value}:Array.Empty<Vector3>();
            IReadOnlyList<Vector3> Query(Vector3 from,Vector3 to)
            {
                var raw=adapter.GetCompletePath(from,to);
                var accepted=!counterHead.HasValue || CustomerRoutePlanner.IsClear(raw,envelopeCenters,envelope);
                ObserveCorridor("pickup.eligibility-native-query",visit,target:to,path:raw,obstacles:envelopeCenters,
                    result:"rawNative="+(raw==null?"NULL":"route")+" filteredEligibility="+(accepted && raw!=null?"route":"NULL")+" envelope="+envelope);
                return accepted?raw:null;
            }
            return Query;
        }
        // Retain the original four-argument entrypoint for existing readonly test probes.
        private IReadOnlyList<Vector3> PlanPickupExit(Vector3 from,Vector3 to,Vector3[] occupied,NavigationSettings settings)
            => PlanPickupExitForCaller(null,"unattributed-readonly-probe",from,to,occupied,settings);
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ObserveExitPlan(string operation,Visit caller,string purpose,long invocation,Vector3 from,Vector3 target,
            IReadOnlyList<Vector3> path,Vector3[] obstacles,string result,int candidate=-1,int leg=-1)
        {
#if UNITY_EDITOR
            if(CorridorObserver==null) return;
            ObserveCorridor(operation,caller,target:target,path:path,obstacles:obstacles,
                result:"invocation="+invocation+" purpose="+purpose+" from="+from.ToString("F9")+
                    " candidate="+candidate+" leg="+leg+" outcome="+result);
#endif
        }
        private IReadOnlyList<Vector3> PlanPickupExitForCaller(Visit caller,string purpose,Vector3 from,Vector3 to,
            Vector3[] occupied,NavigationSettings settings)
            => PlanPickupExitUsingQuery(caller,purpose,from,to,occupied,settings,adapter.GetCompletePath,"raw-native");
        private IReadOnlyList<Vector3> PlanPickupExitUsingQuery(Visit caller,string purpose,Vector3 from,Vector3 to,
            Vector3[] occupied,NavigationSettings settings,Func<Vector3,Vector3,IReadOnlyList<Vector3>> query,string queryKind)
        {
            long invocation=0;
#if UNITY_EDITOR
            if(CorridorObserver!=null) invocation=++exitPlanObservation;
#endif
            var clearance=RouteClearance(settings);
            var queries=0;
            IReadOnlyList<Vector3> Query(Vector3 start,Vector3 goal) { queries++;return query(start,goal); }
            var original=new CustomerRoutePlanner().Build(from,to,occupied,clearance,Query);
            ObserveExitPlan("plan.exit-direct",caller,purpose,invocation,from,to,original,occupied,
                (original==null?"NULL":"route")+" queryKind="+queryKind+" queries="+queries);
            if(original!=null || !integratedMode || runtime?.RoomSurfaceLayout==null || gridRoot==null) return original;
            var floor=runtime.RoomSurfaceLayout.FloorTiles.Keys.ToArray();if(floor.Length==0)return null;
            var cell=runtime.Layout.GridSettings.CellSize;
            var candidate=0;
            foreach(var requested in PickupExitBoundaryCandidates(gridRoot,floor,cell,from,to,settings.AgentRadius))
            {
                var candidateIndex=candidate++;
                var previous=from;var clear=true;var points=new List<Vector3>();
                for(var index=0;index<requested.Length;index++)
                {
                    var path=Query(previous,requested[index]);
                    var legClear=CustomerRoutePlanner.IsClear(path,occupied,clearance);
                    ObserveExitPlan("plan.exit-boundary-leg",caller,purpose,invocation,previous,requested[index],path,occupied,
                        (legClear?"clear":"blocked")+" queryKind="+queryKind+" queries="+queries,candidateIndex,index);
                    if(!legClear)
                    {
                        // These are the actual sampled anchors of the same Native query/filter.
                        // 锚点本身被占住时，换中间路径也不可能安全，避免无效的整图搜索。
                        if(path==null || path.Count==0 ||
                            !CustomerRoutePlanner.IsClear(new[]{path[0]},occupied,clearance) ||
                            !CustomerRoutePlanner.IsClear(new[]{path[path.Count-1]},occupied,clearance))
                        { clear=false;break; }
                        // A boundary corridor may be safe while its shortest connector meets a body.
                        // 边缘通道可用时，连接段也须绕开全部障碍；不把一条最短线失败当作无路。
                        var connector=new CustomerRoutePlanner().Build(previous,requested[index],occupied,clearance,Query);
                        if(connector==null || connector.Count>24-points.Count){clear=false;break;}
                        for(var hop=0;hop<connector.Count;hop++)
                        {
                            var connection=Query(previous,connector[hop]);
                            var connectionClear=CustomerRoutePlanner.IsClear(connection,occupied,clearance);
                            ObserveExitPlan("plan.exit-boundary-detour-leg",caller,purpose,invocation,previous,connector[hop],connection,occupied,
                                (connectionClear?"clear":"blocked")+" queryKind="+queryKind+" queries="+queries,candidateIndex,index);
                            if(!connectionClear){clear=false;break;}
                            var connected=connection[connection.Count-1];connected.y=from.y;
                            points.Add(index==requested.Length-1&&hop==connector.Count-1?to:connected);previous=connected;
                        }
                        if(!clear)break;
                        continue;
                    }
                    if(points.Count==24){clear=false;break;}
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
            // At most four boundary corridors; validated connectors share the 24-waypoint limit.
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
            ObserveCorridor("plan.pickup",visits.TryGetValue(ignore??"",out var subject)?subject:null,target:to,path:planned,obstacles:occupied,result:planned==null?"NULL":"route");
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
            => RouteObstaclePositions(ignore,advancingSlot,prospectiveAdmission,null).Distinct().ToArray();
        private IEnumerable<Vector3> RouteObstaclePositions(string ignore,int? advancingSlot,bool prospectiveAdmission,Visit unoccupiedServiceOwner)
            => world.RegisteredActors.Where(a=>a!=null && a.ActorId!=ignore && !IsMovingQueueLeader(a,ignore,advancingSlot) &&
                    !(prospectiveAdmission && IsMovingEntryLeader(a))).Select(a=>a.transform.position)
                .Concat(visits.Values.Where(v=>v.Id!=ignore && v!=unoccupiedServiceOwner && ReservesRouteTarget(v,ignore,advancingSlot))
                    .Select(v=>v.Assignment.Position))
                .Concat(ReservedPickupTargets(ignore))
                .Concat(!multiSidePickup || pickupQueue==null?Enumerable.Empty<Vector3>():pickupQueue.ServiceLeases
                    .Where(lease=>lease.Key!=ignore && lease.Key!=unoccupiedServiceOwner?.Id).Select(lease=>lease.Value));
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
#if UNITY_EDITOR
            if(CorridorObserver!=null) admissionObservation++;
#endif
            ObserveAdmissionDecision("admission.original-before",target:target);
            ObserveCorridor("admission.before-check",target:target);
            var accepted=CanStartAdmissionCore(target,settings);
            ObserveCorridor("admission.result",target:target,result:accepted?"accepted":"refused");
            ObserveAdmissionDecision("admission.original-result",target:target,accepted:accepted);
            return accepted;
        }
        private bool CanStartAdmissionCore(Vector3 target,NavigationSettings settings)
        {
            // A parked tail may seal a real unpaid front even while its RequestId is zero.
            // 保守保护已成功预留的完整通道；拒绝只保留原到店机会，不授权任何移动。
            foreach(var reserved in visits.Values.Where(HasReservedPickupCorridor))
            {
                var clear=CustomerRoutePlanner.IsClear(reserved.ReservedPickupPath,new[]{target},RouteClearance(settings));
                ObserveCorridor("admission.reserved-decision",reserved,target:target,path:reserved.ReservedPickupPath,result:clear?"clear":"reject");
                if(!clear) return false;
            }
            foreach(var owned in visits.Values.Where(HasOwnedRoute))
            {
                var path=ProtectedOwnedRoute(owned,"admission",null,target,out var finiteRoute);
                if(path!=null)
                {
                    var targetClear=CustomerRoutePlanner.IsClear(path,new[]{target},RouteClearance(settings)) &&
                        (finiteRoute==null || CustomerRoutePlanner.IsClear(finiteRoute,new[]{target},RouteClearance(settings)));
                    ObserveCorridor("admission.owned-decision",owned,target:target,path:path,result:targetClear?"clear":"reject");
                    ObserveAdmissionDecision("admission.original-owned",owned,target,path,targetClear:targetClear,accepted:targetClear);
                    if(!targetClear) return false;
                }
            }
            var route=BuildRoute(spawn.position,target,null,settings,prospectiveAdmission:true);
            if(route==null || route.Count>24) return false;
            var incoming=ExpandRoute(spawn.position,route);
            ObserveAdmissionDecision("admission.original-incoming",target:target,path:incoming);
            if(incoming==null) return false;
            if(PendingOwnedRouteConflicts(null,settings,incoming,target,"admission.incoming")) return false;
            var clearance=RouteClearance(settings)+2*settings.MaxSpeed*
                Mathf.Min(Time.deltaTime,settings.MaxSubstepSeconds*settings.MaxSubstepsPerFrame);
            // 创建/预留前让已有任务先通过；新尾位不能堵住前客的剩余路线。
            // Validate remaining owned paths, then keep P11's continuous body guards.
            foreach(var earlier in visits.Values.Where(v=>v.RequestId!=0 && v.Actor!=null && v.Actor.isActiveAndEnabled))
            {
                var destination=earlier.Actor.Agent.destination;
                var targets=new[]{destination}.Concat(earlier.Route??Enumerable.Empty<Vector3>());
                var remaining=ExpandRoute(earlier.Actor.transform.position,targets);
                if(remaining==null)
                {
                    ObserveAdmissionDecision("admission.original-active",earlier,target,remaining,destination,clearance,accepted:false);
                    return false;
                }
                var targetClear=CustomerRoutePlanner.IsClear(remaining,new[]{target},clearance);
                var follows=earlier.State==CustomerVisitState.Entering &&
                    CanFollowEntryBend(incoming,remaining,clearance,settings.ArrivalDistance+settings.Epsilon) &&
                    CustomerRoutePlanner.IsClear(new[]{remaining[remaining.Count-1]},new[]{target},clearance) &&
                    (targetClear || FollowerReachesTailAfterLeader(incoming,remaining,target,clearance,
                        settings.MaxSpeed,earlier.Actor.Settings.MaxSpeed));
                // 同向跟随按先后经过尾位；前客最终目标仍须留空，实际身体由World逐步保护。
                bool? vacates=null;
                if(!targetClear && !follows)
                {
                    vacates=EntryVacatesTail(earlier,remaining,target,clearance);
                    if(!vacates.Value)
                    {
                        ObserveAdmissionDecision("admission.original-active",earlier,target,remaining,destination,clearance,targetClear,follows,vacates,accepted:false);
                        return false;
                    }
                }
                var conflict=RoutesConflict(incoming,remaining,clearance);
                ObserveAdmissionDecision("admission.original-active",earlier,target,remaining,destination,clearance,targetClear,follows,vacates,conflict,!conflict||follows);
                if(conflict && !follows) return false;
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
            ObservePickup("advance.candidate",visit,visit.PickupOwner,assignment);
            if(!NewPickupTargetClear(visit,assignment.Position)) return false;
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
            => ExpandRouteWithLegEnds(start,targets,null);
        private IReadOnlyList<Vector3> ExpandRouteWithLegEnds(Vector3 start,IEnumerable<Vector3> targets,List<int> ends)
        {
            var points=new List<Vector3>{start};
            foreach(var target in targets)
            {
                var path=adapter.GetCompletePath(points[points.Count-1],target);
                if(path==null || path.Count==0) return null;
                points.AddRange(path.Skip(1));
                ends?.Add(points.Count-1);
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
        private void BlockVisit(Visit visit,CustomerVisitState phase,string reason,bool preservePaidRecovery=false)
        {
            visit.FailedCollection=null;
            AbortCustomerHandoff(visit,"customer-phase-blocked");
            if(visit.PaidPickup!=null && phase==CustomerVisitState.MovingToPickup)
            {
                if(!preservePaidRecovery)DiscardPaidRecovery(visit);
                visit.PaidPickup.Failed=true;visit.PaidPickup.RecoveryPending=false;
            }
            visit.TrafficMovePending=false;
            RetireMovement(visit.TrafficMove);visit.TrafficMove=null;visit.TrafficSelection=null;visit.IssuedTraffic=null;
            visit.PendingCounter=null;
            if(visit.TrafficJourney.HasValue)
            {
                if(visit.PaidPickup==null || visit.PaidPickup.Parking.Retired ||
                    !visit.TrafficJourney.Value.Equals(visit.PaidPickup.Parking.Source.Intent))traffic?.Retire(visit.TrafficJourney.Value);
                visit.TrafficJourney=null;
            }
            ObserveCorridor("route.blocked",visit,result:reason);
            ClearOwnedRoute(visit);
            visit.PendingExitSegment=visit.PendingPickupSegment=false; visit.RequestId=0; visit.State=CustomerVisitState.Blocked; recoveringQueue=false;
            visit.Failure=(phase==CustomerVisitState.Exiting?"离场":phase==CustomerVisitState.Advancing?"前移":"入店")+"："+reason;
            if(phase==CustomerVisitState.Advancing)
                foreach(var follower in visits.Values.Where(v=>v.State==CustomerVisitState.Advancing &&
                    v.Assignment.SlotIndex>visit.Assignment.SlotIndex).ToArray())
                    HoldVisit(follower); // 前客失败，退休并发后客请求，保留实际位置/容量。
        }
        private bool CanWaitForDepartingExitBody(Visit visit)
        {
            if(!integratedMode || !visit.HasCollected || visit.ExitSequence==0 || visit.ResumeState!=CustomerVisitState.Exiting || visit.Actor==null) { ObserveCorridor("wait.departing",visit,result:"identity-ineligible; staticPath=NOT_EVALUATED"); return false; }
            var staticPath=adapter.GetCompletePath(visit.Actor.transform.position,visit.FinalTarget.Position);
            if(staticPath==null || staticPath.Count==0) { ObserveCorridor("wait.departing",visit,path:staticPath,result:"no-static-path; earlier=NOT_EVALUATED"); return false; }
            var clearance=RouteClearance(visit.Actor.Settings);
            var departing=visits.Values.Any(earlier=>earlier!=visit && earlier.HasCollected && earlier.ExitSequence>0 && earlier.ExitSequence<visit.ExitSequence && earlier.State==CustomerVisitState.Exiting &&
                earlier.RequestId!=0 && earlier.Actor!=null && earlier.Actor.isActiveAndEnabled &&
                Vector3.Distance(earlier.FinalTarget.Position,visit.FinalTarget.Position)<.001f &&
                Vector3.Distance(earlier.Actor.transform.position,visit.FinalTarget.Position)<clearance);
            ObserveCorridor("wait.departing",visit,path:staticPath,result:departing?"qualified":"no-earlier-active-exit-near-goal");
            return departing;
        }
        private bool CanWaitForIndependentPickupExitTraffic(Visit visit)
            => TryPlanIndependentExitWait(visit,out _);
        private bool TryPlanIndependentExitWait(Visit visit,out IReadOnlyList<Vector3> waitingRoute)
        {
            waitingRoute=null;
            if(!CanIntegratedProgress || !visit.HasCollected || visit.ExitSequence==0 || visit.State!=CustomerVisitState.Exiting ||
                visit.Actor==null || !visit.Actor.isActiveAndEnabled) { ObserveCorridor("wait.independent",visit,result:"identity-ineligible"); return false; }
            var occupied=RouteObstacles(visit.Id);
            var retainsLeg=visit.Route!=null && visit.Route.Count>0;
            var complete=retainsLeg?ExpandRoute(visit.Actor.transform.position,visit.Route):null;
            var clearance=RouteClearance(visit.Actor.Settings);
            if(retainsLeg && (complete==null || CustomerRoutePlanner.IsClear(complete,occupied,clearance))) { ObserveCorridor("wait.independent",visit,path:complete,obstacles:occupied,result:"retained-leg-null-or-already-clear"); return false; }
            var candidates=visits.Values.Where(v=>v!=visit && v.RequestId!=0 && v.Actor!=null && v.Actor.isActiveAndEnabled &&
                (IsPickupMovement(v.State) || v.State==CustomerVisitState.Entering || v.State==CustomerVisitState.Advancing ||
                    v.State==CustomerVisitState.Exiting && v.HasCollected && v.ExitSequence<visit.ExitSequence)).ToArray();
            var finite=new HashSet<Visit>();
            Vector3[] RetainedObstacles(string ignore) => world.RegisteredActors
                .Where(a=>a!=null && a.ActorId!=ignore && !finite.Any(v=>v.Actor==a)).Select(a=>a.transform.position)
                .Concat(WaitTargetPositions(ignore))
                .Concat(!multiSidePickup || pickupQueue==null?Enumerable.Empty<Vector3>():pickupQueue.ServiceLeases.Where(l=>l.Key!=ignore).Select(l=>l.Value))
                .Distinct().ToArray();
            // Bounded dependency order: every accepted request can vacate without the
            // waiting collector, possibly after an already proven finite predecessor.
            // 不允许相互等待的循环；真实身体、未来目标和service lease均保留。
            for(var pass=0;pass<candidates.Length;pass++)
            {
                var added=false;
                foreach(var moving in candidates)
                {
                    var agent=moving.Actor.Agent;
                    if(finite.Contains(moving) || agent==null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped ||
                        agent.pathPending || agent.pathStatus!=UnityEngine.AI.NavMeshPathStatus.PathComplete ||
                        moving.PickupOwner!=null && moving.PickupOwner.TryGetServiceLease(moving.Id,out _) ||
                        Vector3.Distance(moving.FinalTarget.Position,moving.Actor.transform.position)<.001f) { ObserveCorridor("wait.candidate",visit,moving,result:"compound-candidate-skip; evaluated-child=UNKNOWN; remaining=NOT_EVALUATED"); continue; }
                    var remaining=ExpandRoute(moving.Actor.transform.position,
                        new[]{moving.CurrentSegmentTarget.Position}.Concat(moving.Route??Enumerable.Empty<Vector3>()));
                    if(remaining==null) { ObserveCorridor("wait.candidate",visit,moving,result:"remaining-NULL; obstacles=NOT_EVALUATED"); continue; }
                    var retained=RetainedObstacles(moving.Id);
                    var independent=CustomerRoutePlanner.IsClear(remaining,retained,RouteClearance(moving.Actor.Settings));
                    ObserveCorridor("wait.candidate",visit,moving,path:remaining,obstacles:retained,result:independent?"finite":"remaining-blocked");
                    if(!independent) continue;
                    finite.Add(moving);added=true;
                }
                if(!added) break;
            }
            if(finite.Count==0) { ObserveCorridor("wait.independent",visit,result:"no-finite-candidate; counterfactual=NOT_EVALUATED"); return false; }
            var withoutBodies=RetainedObstacles(visit.Id);
            waitingRoute=retainsLeg?visit.Route.ToArray():PlanPickupExitForCaller(visit,"wait.counterfactual",visit.Actor.transform.position,visit.FinalTarget.Position,withoutBodies,visit.Actor.Settings);
            if(waitingRoute==null) { ObserveCorridor("wait.independent",visit,obstacles:withoutBodies,result:"counterfactual-route-NULL"); return false; }
            complete=retainsLeg?complete:ExpandRoute(visit.Actor.transform.position,waitingRoute);
            // Counterfactual proves WAIT only. Dispatch below still verifies the full
            // owned route against every real body; no planner graph is rebuilt per wait frame.
            var withoutClear=CustomerRoutePlanner.IsClear(complete,withoutBodies,clearance);
            ObserveCorridor("wait.without-finite-bodies",visit,path:complete,obstacles:withoutBodies,result:withoutClear?"clear":"blocked");
            if(!withoutClear) return false;
            var finiteBodies=finite.Select(v=>v.Actor.transform.position).ToArray();
            var obstructed=!CustomerRoutePlanner.IsClear(complete,finiteBodies,clearance);
            ObserveCorridor("wait.independent",visit,path:complete,obstacles:finiteBodies,result:obstructed?"qualified":"finite-bodies-not-obstructing");
            return obstructed;
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
        private bool CanWaitForConflictingCustomerRequest(Visit visit)
            => TryProveConflictingCustomerRequest(visit,out _);
        private bool RememberConflictingCustomerWait(Visit visit)
        {
            var qualified=TryProveConflictingCustomerRequest(visit,out var proof);
            visit.OwnedWaitProof=proof;
            ObserveCorridor("owned.finite-wait-proof",visit,result:qualified?"strict-qualified":"not-qualified");
            return qualified;
        }
        private bool TryProveConflictingCustomerRequest(Visit visit,out FiniteRouteProof proof)
        {
            proof=null;
            if(!CanIntegratedProgress || visit.RequestId!=0 || visit.Actor==null || !visit.Actor.isActiveAndEnabled ||
                visit.Route==null || visit.Route.Count==0) return false;
            var proposed=ExpandRoute(visit.Actor.transform.position,visit.Route);
            if(proposed==null) return false;
            var clearance=RouteClearance(visit.Actor.Settings);
            foreach(var moving in visits.Values.Where(v=>v!=visit && v.RequestId!=0 && v.Actor!=null && v.Actor.isActiveAndEnabled &&
                (IsPickupMovement(v.State) || v.State==CustomerVisitState.Entering || v.State==CustomerVisitState.Advancing ||
                    v.State==CustomerVisitState.Exiting && v.HasCollected &&
                    (visit.State!=CustomerVisitState.Exiting || v.ExitSequence<visit.ExitSequence))))
            {
                var agent=moving.Actor.Agent;
                if(agent==null || !agent.enabled || !agent.isOnNavMesh || agent.isStopped || agent.pathPending ||
                    agent.pathStatus!=UnityEngine.AI.NavMeshPathStatus.PathComplete) continue;
                var ends=new List<int>();
                var remaining=ExpandRouteWithLegEnds(moving.Actor.transform.position,new[]{moving.CurrentSegmentTarget.Position}
                    .Concat(moving.Route??Enumerable.Empty<Vector3>()),ends);
                // An issued route already obstructed by this collector is not an
                // independent exit certificate. Prevent the interlock BEFORE entering it.
                // 保护已接受的完整路线；已互锁的身体不能反过来授权无限等待。
                var independentObstacles=IndependentTrafficObstacles(moving,visit);
                var independent=CustomerRoutePlanner.IsClear(remaining,independentObstacles,RouteClearance(moving.Actor.Settings));
                ObserveCorridor("owned.finite-independent",visit,moving,path:remaining,obstacles:independentObstacles,result:independent?"clear":"blocked");
                if(!independent) continue;
                if(moving.State!=CustomerVisitState.Exiting &&
                    !CustomerRoutePlanner.IsClear(proposed,new[]{moving.Assignment.Position},clearance)) continue;
                if(RoutesConflict(proposed,remaining,clearance) ||
                    !CustomerRoutePlanner.IsClear(remaining,new[]{visit.Route.Peek()},clearance))
                {
                    proof=new FiniteRouteProof{Moving=moving,Actor=moving.Actor,Request=moving.RequestId,Generation=moving.Generation,
                        WaiterGeneration=visit.Generation,Revision=adapter.Revision,Phase=moving.State,Assignment=moving.Assignment,
                        Target=moving.CurrentSegmentTarget,WaiterRoute=visit.Route,WaiterUnissued=visit.Route.ToArray(),Unissued=moving.Route?.ToArray()??Array.Empty<Vector3>(),
                        Path=remaining,FirstLegEnd=ends[0]};
                    return true;
                }
            }
            return false;
        }
        private Vector3[] IndependentTrafficObstacles(Visit moving,Visit waiter)
        {
            // WAIT promises the unissued pickup leg stays at its real pose. Its own
            // still-empty future slot is not a body on an already accepted leader route.
            // 仅按Visit归属处理等待者未占用的未来slot；真实身体/其它目标/全部lease保留，身份不释放。
            var emptyWaiterSlot=waiter.RequestId==0 && IsPickupMovement(waiter.State) && waiter.Actor!=null &&
                Vector3.Distance(waiter.Actor.transform.position,waiter.Assignment.Position)>waiter.Actor.Settings.ArrivalDistance+waiter.Actor.Settings.Epsilon;
            return world.RegisteredActors.Where(a=>a!=null && a!=moving.Actor).Select(a=>a.transform.position)
                .Concat(WaitTargetPositions(moving.Id,emptyWaiterSlot?waiter:null))
                .Concat(visits.Values.Select(v=>v.PickupOwner).Concat(new[]{pickupQueue}).Where(q=>q!=null).Distinct()
                    .SelectMany(q=>q.ServiceLeases.Where(l=>l.Key!=moving.Id).Select(l=>l.Value)))
                .Distinct().ToArray();
        }
        private IEnumerable<Vector3> WaitTargetPositions(string ignore,Visit emptyWaiter=null)
            // WAIT owns every outstanding target, independent of the older phase-relative
            // route-planning filters. Only the named waiter's still-empty slot is exempt.
            // 按Visit归属保留所有未退休目标；不改变旧RouteObstacles的phase规则。
            => visits.Values.Where(v=>v.Id!=ignore && v!=emptyWaiter &&
                (IsPickupMovement(v.State) || v.State==CustomerVisitState.Entering || v.State==CustomerVisitState.Advancing))
                .Select(v=>v.Assignment.Position);
        private void SendRouteSegment(Visit visit,CustomerVisitState phase)
            => SendRouteSegmentCore(visit,phase,null,0);
        private void SendTrafficRouteSegment(Visit visit,CustomerVisitState phase)
        {
            if(!CanIntegratedProgress || visit.Actor==null || !visit.Actor.isActiveAndEnabled || visit.RequestId!=0 ||
                !visits.TryGetValue(visit.Id,out var live) || !ReferenceEquals(live,visit) || visit.State!=phase)return;
            if(visit.TrafficMove==null)
            {
                // A parked predecessor keeps its owner label until the selected successor's strict leg commits.
                // pending期间不能把旧owner当作新phase的source。
                var key=visit.PendingTraffic==null && visit.TrafficJourney.HasValue?visit.TrafficJourney.Value:
                    TrafficKey(visit.Id,"movement-"+phase,visit.Id+"/route/"+visit.Generation);
                traffic.TryGetCommittedJourney(key,out var source);
                var proof=source?.ProofRoutes.FirstOrDefault(p=>p.ResourceId=="pickup/"+visit.Id || p.ResourceId=="counter/"+visit.Id);
                visit.TrafficMove=new TrafficMovement{Coordinator=traffic,Intent=key,Source=source,Goal=visit.FinalTarget,
                    Resource=proof?.ResourceId??"movement/"+visit.Id};
                if(visit.PendingTraffic==null)visit.TrafficJourney=key;
            }
            var move=visit.TrafficMove;
            if(move.NativeGeneration==0)
            {
                var continuation=YieldContinuation(visit);
                var sameYield=phase==CustomerVisitState.PickupAdvancing && visit.DeliveryYieldPhase==1 &&
                    continuation?.Used==true && !continuation.Pending && !continuation.Failed &&
                    ReferenceEquals(continuation.Plan.Visit,visit) && ReferenceEquals(continuation.Plan.Actor,visit.Actor) &&
                    ReferenceEquals(move.Coordinator,continuation.Plan.Coordinator) && move.Intent.Equals(continuation.Intent) &&
                    ReferenceEquals(move.Source,continuation.Source) && visit.Generation==continuation.Plan.YieldGeneration;
                move.NativeGeneration=++visit.Generation;
                // The same authorized yield follows each actual strict-leg generation, including deferred preparation.
                // 同一已授权source同步真实strict generation；不增加额外generation或重置任何budget。
                if(sameYield)continuation.Plan.YieldGeneration=move.NativeGeneration;
            }
            var generation=move.NativeGeneration;
            visit.TrafficMovePending=true;visit.PendingExitSegment=phase==CustomerVisitState.Exiting;
            visit.PendingPickupSegment=IsPickupMovement(phase);
            var hadSource=move.Source!=null;
            var decision=PrepareMovement(visit.Actor,move,generation);
            if(!ReferenceEquals(visit.TrafficMove,move) || visit.Generation!=generation || visit.State!=phase)return;
            if(!hadSource && move.Source!=null)
            {
                visit.Route=new Queue<Vector3>(move.Proof.Waypoints.Skip(move.Index));
                RememberOwnedRoute(visit,phase,move.Proof);
            }
            if(decision.Kind==TrafficDecisionKind.DeferredByBudget || decision.Kind==TrafficDecisionKind.WaitingForTraffic)return;
            if(decision.Kind==TrafficDecisionKind.StaleContext)
            {
                // Only the kernel's explicit stale candidate path may reprepare; the phase and query budget stay put.
                // 普通元数据失效保留本次候选；生命周期失效不能改写其它visit。
                if(!ReferenceEquals(move.Coordinator,traffic) || move.Intent.Epoch!=trafficEpoch || move.Intent.LayoutRevision!=adapter.Revision)
                    BlockVisit(visit,phase,"Movement traffic context retired");
                return;
            }
            if(decision.Kind!=TrafficDecisionKind.Granted)
            {
                if(decision.Reason=="actual-leg-needs-route-replacement" && ++visit.Replans<=3)
                { move.Source=null;move.Index=0;visit.Route=null;return; }
                BlockVisit(visit,phase,"Movement traffic "+decision.Kind+": "+decision.Reason);return;
            }
            var path=decision.ProofRoutes.Single().ExpandedPath;
            ObserveCorridor("segment.issue",visit,target:move.Target.Position,path:path);
            if(!visits.TryGetValue(visit.Id,out live) || !ReferenceEquals(live,visit) || visit.Generation!=generation ||
                visit.State!=phase || !visit.Actor.isActiveAndEnabled || !ReferenceEquals(move.Coordinator,traffic))return;
            RefreshTrafficSnapshot();
            if(!CommitCustomerMovement(visit,move,decision))return;
            if(!CanIssueMovement(visit.Actor,move))return;
            if(!CaptureIssuedTraffic(visit,move))
            {BlockVisit(visit,phase,"Committed traffic provenance no longer matches the canonical reservation");return;}
            if(visit.Route==null)visit.Route=new Queue<Vector3>(move.Proof.Waypoints.Skip(move.Index));
            if(visit.Route.Count==0 || visit.Route.Peek()!=move.Target.Position)
            {BlockVisit(visit,phase,"Retained leg cursor does not match the real selected route");return;}
            if(++visit.Segments>32){BlockVisit(visit,phase,"绕行次数已达上限");return;}
            visit.Route.Dequeue();visit.TrafficMovePending=false;visit.PendingExitSegment=visit.PendingPickupSegment=false;
            IssueRouteSegment(visit,phase,move.Target,path,visit.Route.Count==0,move);
        }
        private void SendRouteSegmentCore(Visit visit,CustomerVisitState phase,MovementResult? precedingCallback,int callbackGeneration)
        {
            if(traffic!=null) { SendTrafficRouteSegment(visit,phase);return; }
            // Deferred first candidates need the same check; issued suffixes keep their precedence.
            // 延后才得到的首腿同样终验；不让后到员工抢占已发过首腿的原路线。
            if(phase==CustomerVisitState.Exiting && visit.Segments==0 && visit.RequestId==0 &&
                (HoldNewExitForOwnedEmployee(visit)||HoldNewExitForOwnedPickup(visit)))return;
            if(visit.OwnedRoutePath!=null && (visit.OwnedRouteSegments!=visit.Segments || !ReferenceEquals(visit.OwnedRouteSource,visit.Route)))
                RememberOwnedRoute(visit,phase); // Actual segment arrival/replacement only, not unchanged pending frames.
            visit.PendingPickupSegment=false;
            if(integratedMode && (phase==CustomerVisitState.Exiting || IsPickupMovement(phase)) &&
                RememberConflictingCustomerWait(visit))
            {
                if(phase==CustomerVisitState.Exiting) visit.PendingExitSegment=true;
                else visit.PendingPickupSegment=true;
                return; // Retain selected route and finite budgets; no new planner graph.
            }
            // 未发出下一段请求时，前客仍在共同出口完成真实Arrived可普通等候。
            // No segment/replan budget is consumed, and real issued-request failures remain unchanged.
            if(phase==CustomerVisitState.Exiting && (ExitRouteConflictsWithEarlierRequest(visit) || CanWaitForDepartingExitBody(visit) &&
                PlanPickupExitForCaller(visit,"segment.wait-eligibility",visit.Actor.transform.position,visit.FinalTarget.Position,RouteObstacles(visit.Id),visit.Actor.Settings)==null))
            { visit.PendingExitSegment=true; return; }
            visit.PendingExitSegment=false;
            var point=visit.Route.Peek();
            // 其他顾客可能已移动；每段从实际位置验证，必要时有限重算整条剩余路线。
            var from=visit.Actor.transform.position;
            var nextPath=adapter.GetCompletePath(from,point);
            var nextObstacles=RouteObstacles(visit.Id);
            var nextClearance=RouteClearance(visit.Actor.Settings);
            var nextClear=CustomerRoutePlanner.IsClear(nextPath,nextObstacles,nextClearance);
            ObserveCorridor("segment.next-check",visit,target:point,path:nextPath,obstacles:nextObstacles,result:nextClear?"clear":"blocked");
            var fullClear=true;
            IReadOnlyList<Vector3> fullPath=null; Vector3[] fullObstacles=null;
            if(nextClear && phase==CustomerVisitState.Exiting)
            {
                fullPath=ExpandRoute(visit.Actor.transform.position,visit.Route);
                fullObstacles=RouteObstacles(visit.Id);
                fullClear=CustomerRoutePlanner.IsClear(fullPath,fullObstacles,RouteClearance(visit.Actor.Settings));
                ObserveCorridor("segment.full-check",visit,path:fullPath,obstacles:fullObstacles,result:fullClear?"clear":"blocked");
            }
            if(!nextClear || !fullClear)
            {
                if(phase==CustomerVisitState.Exiting && CanWaitForIndependentPickupExitTraffic(visit))
                { visit.PendingExitSegment=true; return; } // Keep this unissued leg and its original budgets.
                var waitFailure=default(PickupWaitFailure);
                if(integratedMode && IsPickupMovement(phase) && visit.RequestId==0 && CanWaitForMovingPickupBodiesWithFailure(visit,nextPath,out waitFailure))
                { visit.PendingPickupSegment=true; return; }
                IReadOnlyList<Vector3> replanned=null;
                if(++visit.Replans<=3)
                    replanned=phase==CustomerVisitState.MovingToPickup || phase==CustomerVisitState.PickupAdvancing
                        ?BuildPickupWaitRoute(visit.Actor.transform.position,visit.FinalTarget.Position,visit.Id,visit.Actor.Settings)
                        :integratedMode && phase==CustomerVisitState.Exiting
                        ?PlanPickupExitForCaller(visit,"segment.replan.actual",visit.Actor.transform.position,visit.FinalTarget.Position,RouteObstacles(visit.Id),visit.Actor.Settings)
                        :BuildRoute(visit.Actor.transform.position,visit.FinalTarget.Position,visit.Id,visit.Actor.Settings);
                ObserveCorridor("segment.replan-result",visit,path:replanned,result:visit.Replans>3?"budget-exhausted":replanned==null?"NULL":"route");
                if(replanned==null || replanned.Count>24)
                {
#if UNITY_EDITOR
                    if(SegmentFailureObserver!=null)
                        RecordSegmentFailure(visit,phase,from,point,nextPath,nextObstacles,nextClearance,nextClear,
                            fullPath,fullObstacles,fullClear,waitFailure,replanned,precedingCallback,callbackGeneration);
#endif
                    BlockVisit(visit,phase,"通道被占用"); return;
                }
                visit.Route=new Queue<Vector3>(replanned);
                ObserveCorridor("segment.replaced",visit,path:replanned);
                if(phase==CustomerVisitState.Exiting&&(HoldNewExitForOwnedEmployee(visit)||HoldNewExitForOwnedPickup(visit)))return;
                RememberOwnedRoute(visit,phase);
                // A replacement is a new owned candidate. Apply the same independent
                // accepted-request precedence before consuming/dequeuing any segment.
                // 重算后的Pickup/Exit共用终验；等待保留新candidate及原segment预算。
                if(integratedMode && (phase==CustomerVisitState.Exiting || IsPickupMovement(phase)) &&
                    RememberConflictingCustomerWait(visit))
                {
                    if(phase==CustomerVisitState.Exiting) visit.PendingExitSegment=true;
                    else visit.PendingPickupSegment=true;
                    return;
                }
            }
            // 有界重算后的最终candidate也必须先通过owned-route门禁；等待保留该candidate。
            if(phase==CustomerVisitState.Exiting && ExitRouteConflictsWithEarlierRequest(visit))
            { visit.PendingExitSegment=true; return; }
            if(visit.OwnedRoutePath==null) RememberOwnedRoute(visit,phase);
            if(++visit.Segments>32) { BlockVisit(visit,phase,"绕行次数已达上限"); return; }
            point=visit.Route.Dequeue(); var final=visit.Route.Count==0;
            var target=final?visit.FinalTarget:new NavigationTarget(point);
            var path=adapter.GetCompletePath(visit.Actor.transform.position,target.Position);
            IssueRouteSegment(visit,phase,target,path,final,null);
        }
        private void IssueRouteSegment(Visit visit,CustomerVisitState phase,NavigationTarget target,IReadOnlyList<Vector3> path,bool final,TrafficMovement strict)
        {
            if(strict==null)ObserveCorridor("segment.issue",visit,target:target.Position,path:path);
            if(path==null || path.Count==0) { BlockVisit(visit,phase,"路径不可达"); return; }
            var endpoint=path[path.Count-1];
            var generation=strict==null?++visit.Generation:strict.NativeGeneration; var layout=adapter.Revision;
            var issuedActor=visit.Actor;var issuedTraffic=strict==null?null:visit.IssuedTraffic;var strictFinished=false;
            visit.RequestId=0; visit.State=visit.ResumeState=phase;
            visit.CurrentSegmentTarget=target;
            var start=world.Service.MoveTo(visit.Id,target,result=>
            {
                if(strict!=null)
                {
                    if(strictFinished || !visits.TryGetValue(visit.Id,out var current) || !ReferenceEquals(current,visit) ||
                        !ReferenceEquals(visit.Actor,issuedActor) || !ReferenceEquals(visit.TrafficMove,strict) ||
                        generation!=visit.Generation || result.ActorId!=visit.Id ||
                        visit.RequestId!=0 && visit.RequestId!=result.RequestId)return;
                    strictFinished=true;
                }
                var canonicalArrival=strict!=null && final && phase!=CustomerVisitState.Exiting && result.Status==MovementStatus.Arrived;
#if UNITY_EDITOR
                if(!canonicalArrival && CorridorObserver!=null) ObserveCorridor("segment.callback-result",visit,target:result.FinalPosition,
                    result:"request="+result.RequestId+" status="+result.Status+" reason="+result.Reason+" originalFailure="+result.OriginalFailure+
                    " recoveryFailure="+result.RecoveryFailure+" retry="+result.RetryCount+" issuedGeneration="+generation);
#endif
                if(!visits.ContainsKey(visit.Id) || generation!=visit.Generation || layout!=adapter.Revision) return;
                if(strict!=null && result.Status==MovementStatus.LayoutChanged && !world.LayoutAvailable &&
                    SuspendMovement(issuedActor,strict,result,generation,adapter.Revision))
                {
                    visit.RequestId=0;visit.TrafficMovePending=true;
                    visit.Route=new Queue<Vector3>(strict.Proof.Waypoints.Skip(strict.Index));
                    visit.PendingExitSegment=phase==CustomerVisitState.Exiting;visit.PendingPickupSegment=IsPickupMovement(phase);
                    return; // The cancelled leg was not Arrived; its exact uncompleted cursor is retained.
                }
                if(strict!=null && result.Status==MovementStatus.Arrived &&
                    (!ReferenceEquals(visit.TrafficMove,strict) || !(canonicalArrival?
                        CompleteTrafficPhase(visit,strict,issuedTraffic,phase,result,generation):
                        CompleteMovement(visit.Actor,strict,result,generation))))
                {BlockVisit(visit,phase,"Actual arrival did not match the committed traffic leg");return;}
#if UNITY_EDITOR
                // The actual final Arrived and its canonical mark/pending are complete before any observer can reenter.
                if(canonicalArrival && CorridorObserver!=null) ObserveCorridor("segment.callback-result",visit,target:result.FinalPosition,
                    result:"request="+result.RequestId+" status="+result.Status+" reason="+result.Reason+" originalFailure="+result.OriginalFailure+
                    " recoveryFailure="+result.RecoveryFailure+" retry="+result.RetryCount+" issuedGeneration="+generation);
#endif
                ObserveCorridor("segment.callback",visit,target:result.FinalPosition,result:result.Status==MovementStatus.Arrived?"Arrived":"not-Arrived");
                if(strict!=null && (!visits.TryGetValue(visit.Id,out var afterPublication) || !ReferenceEquals(afterPublication,visit) ||
                    generation!=visit.Generation || !ReferenceEquals(visit.Actor,issuedActor) || !ReferenceEquals(visit.TrafficMove,strict) ||
                    !isActiveAndEnabled || (!issuedActor.isActiveAndEnabled &&
                    !(result.Status==MovementStatus.Failed && result.Reason==NavigationFailure.ActorUnavailable &&
                    visit.RequestId>0 && visit.RequestId==result.RequestId && strict.NativeGeneration==generation)) || layout!=adapter.Revision))return;
                visit.RequestId=0;
                if(result.Status!=MovementStatus.Arrived)
                {
                    var recovery=phase==CustomerVisitState.MovingToPickup && CapturePaidRecovery(visit,strict,result,generation);
                    BlockVisit(visit,phase,result.Reason==NavigationFailure.MovementTimeout?"移动超时":result.Status==MovementStatus.Recovered?"原目标尚未到达":"路径不可达",recovery);return;
                }
                if(canonicalArrival)
                {
                    ObserveCorridor("arrival.canonical-committed",visit,target:result.FinalPosition,
                        result:"phase="+phase+" request="+result.RequestId+" issuedGeneration="+generation);
                    if(visits.TryGetValue(visit.Id,out var parked) && ReferenceEquals(parked,visit) &&
                        generation==visit.Generation && ReferenceEquals(visit.TrafficMove,strict))ClearOwnedRoute(visit);
                    return; // The old owner and all still-owned claims remain in the one bounded handoff.
                }
                // 临时waypoint沿用Service到站容差；完整skin可能使拐点无法精确抵达。
                // 下一段从实际pose重新查询/验证，不把临时Arrived当作Queued或瞬移到拐点。
                var remaining=result.FinalPosition-endpoint; remaining.y=0;
                if(!final && remaining.magnitude>visit.Actor.Settings.ArrivalDistance)
                { BlockVisit(visit,phase,"尚未抵达绕行拐点"); return; }
                if(!final) { RetireArrivedOwnedLeg(visit);SendRouteSegmentCore(visit,phase,result,generation); return; }
                if(visit.TrafficJourney.HasValue){traffic?.Retire(visit.TrafficJourney.Value);visit.TrafficJourney=null;}
                ClearOwnedRoute(visit);
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
                    if(strict!=null)ObserveCorridor("arrival.canonical-committed",visit,target:result.FinalPosition,
                        result:"phase="+phase+" request="+result.RequestId+" issuedGeneration="+generation);
                    return;
                }
                if(queue.Complete(visit.Id,visit.Assignment.Generation) && visit.Admission.MarkQueued())
                {
                    visit.State=CustomerVisitState.Queued; visit.CounterArrived=true; visit.CounterPosition=visit.Actor.transform.position;
                    if(strict!=null)ObserveCorridor("arrival.canonical-committed",visit,target:result.FinalPosition,
                        result:"phase="+phase+" request="+result.RequestId+" issuedGeneration="+generation);
                }
                else BlockVisit(visit,phase,"站位提交失败");
            });
            if(strict!=null && !strictFinished && start.Accepted && visits.TryGetValue(visit.Id,out var live) && ReferenceEquals(live,visit) &&
                visit.Generation==generation && visit.State==phase && strict.NativeGeneration==generation)
            {
                if(!BindMovement(visit.Actor,strict,start.RequestId))
                { world.Service.Cancel(start.RequestId);BlockVisit(visit,phase,"Native request binding lost its traffic leg");return; }
            }
            else if(strict!=null && !strictFinished && start.Accepted && strict.NativeGeneration==generation)
                world?.Service.Cancel(start.RequestId); // Only this retired attempt's returned ID, never a callback's next leg.
#if UNITY_EDITOR
            if(CorridorObserver!=null) ObserveCorridor("segment.submitted",visit,target:target.Position,
                result:"request="+start.RequestId+" accepted="+start.Accepted+" reason="+start.Reason+" issuedGeneration="+generation);
            if(phase==CustomerVisitState.Entering && CorridorObserver!=null)
                ObserveAdmissionDecision("admission.original-native-result",target:visit.Assignment.Position,incoming:visit,accepted:start.Accepted,
                    detail:"nativeRequest="+start.RequestId+" issuedGeneration="+generation+" nativeTarget="+target.Position.ToString("F9")+" segment="+visit.Segments);
#endif
            // 同步callback可能已经将状态置为Blocked/Removed，不用返回值复活。
            if(visits.ContainsKey(visit.Id) && visit.Generation==generation && visit.State==phase && (strict==null || !strictFinished))
            { if(start.Accepted)
                { visit.RequestId=start.RequestId;visit.OwnedIssuedRequest=start.RequestId;visit.OwnedIssuedGeneration=generation; }
                else BlockVisit(visit,phase,"移动请求未接受"); }
        }
        private bool CanWaitForMovingPickupBodies(Visit visit,IReadOnlyList<Vector3> path)
            => CanWaitForMovingPickupBodiesWithFailure(visit,path,out _);
        private bool CanWaitForMovingPickupBodiesWithFailure(Visit visit,IReadOnlyList<Vector3> path,out PickupWaitFailure failure)
        {
            failure=default;
            if(path==null || path.Count==0) { failure.Reason=PickupWaitRefusal.StaticPathNull; ObserveCorridor("wait.pickup-body",visit,path:path,result:"static-path-NULL"); return false; } // Static owned path must already be complete.
            var clearance=RouteClearance(visit.Actor.Settings);
            // A reserved target is not a moving body / 不把尚未腾空的正式目标当作活动交通。
            if(!CustomerRoutePlanner.IsClear(path,visits.Values.Where(v=>v.Id!=visit.Id && ReservesRouteTarget(v,visit.Id,null))
                .Select(v=>v.Assignment.Position).ToArray(),clearance)) { failure.Reason=PickupWaitRefusal.FormalTargetBlocked; ObserveCorridor("wait.pickup-body",visit,path:path,result:"formal-target-blocked; bodies=NOT_EVALUATED"); return false; }
            var found=false;
            foreach(var actor in world.RegisteredActors)
            {
                if(actor==null || actor==visit.Actor || CustomerRoutePlanner.IsClear(path,new[]{actor.transform.position},clearance)) continue;
                found=true;
                var agent=actor.Agent;
                failure=new PickupWaitFailure{Actor=actor};
                // P11 Finish/Cancel/failure stops and resets the actual Agent path. No request is fabricated here.
                // Same original short circuit; nullable values mark checks that never ran.
                // 保存原判断已求值的flags，后续flags保持NOT_EVALUATED，不补查native状态。
                if(!(failure.Active=actor.isActiveAndEnabled).Value || !(failure.AgentExists=agent!=null).Value ||
                    !(failure.Enabled=agent.enabled).Value || !(failure.OnNavMesh=agent.isOnNavMesh).Value ||
                    (failure.Stopped=agent.isStopped).Value || (failure.Pending=agent.pathPending).Value ||
                    !(failure.Complete=agent.pathStatus==UnityEngine.AI.NavMeshPathStatus.PathComplete).Value)
                {
                    failure.Reason=PickupWaitRefusal.NativeIneligible;
#if UNITY_EDITOR
                    CorridorObserver?.Invoke("wait.pickup-body",visit.Id,actor.ActorId,actor.transform.position,path,null,"obstructing-body-native-ineligible");
#endif
                    return false;
                }
                if(visits.TryGetValue(actor.ActorId,out var moving))
                {
                    if(moving.RequestId==0 || !(IsPickupMovement(moving.State) || moving.State==CustomerVisitState.Entering ||
                        moving.State==CustomerVisitState.Advancing || moving.State==CustomerVisitState.Exiting)) { failure.Reason=PickupWaitRefusal.CustomerRequestOrPhaseIneligible; ObserveCorridor("wait.pickup-body",visit,moving,path:path,result:"obstructing-body-request-or-phase-ineligible"); return false; }
                    var remaining=ExpandRoute(actor.transform.position,new[]{moving.CurrentSegmentTarget.Position}
                        .Concat(moving.Route??Enumerable.Empty<Vector3>()));
                    var independentObstacles=IndependentTrafficObstacles(moving,visit);
                    var independentClear=CustomerRoutePlanner.IsClear(remaining,independentObstacles,RouteClearance(actor.Settings));
                    ObserveCorridor("wait.pickup-independent",visit,moving,path:remaining,obstacles:independentObstacles,result:independentClear?"clear":"blocked");
                    if(!independentClear) { failure.Reason=PickupWaitRefusal.IndependentRouteBlocked; return false; }
                    // A collected exit body disappears only after actual Arrived. Its
                    // destination can occupy this leg temporarily; a parking target cannot.
                    // 只等待真实离店移除，不提前释放body/lease/token；steady请求不依赖native hasPath。
                    if(moving.State==CustomerVisitState.Exiting && moving.HasCollected && moving.ExitSequence>0) continue;
                    if(!CustomerRoutePlanner.IsClear(path,new[]{moving.FinalTarget.Position},clearance)) { failure.Reason=PickupWaitRefusal.ParkingFinalTargetBlocked; ObserveCorridor("wait.pickup-body",visit,moving,path:path,result:"parking-final-target-blocked"); return false; }
                }
                else
                {
                    failure.HasPath=agent.hasPath;
                    if(!failure.HasPath.Value) failure.Reason=PickupWaitRefusal.EmployeeNoPath;
                    else
                    {
                        failure.Destination=agent.destination;
                        if(!CustomerRoutePlanner.IsClear(path,new[]{failure.Destination.Value},clearance))
                            failure.Reason=PickupWaitRefusal.EmployeeCurrentSegmentDestinationBlocked;
                    }
                    if(failure.Reason==PickupWaitRefusal.NotEvaluated) continue;
#if UNITY_EDITOR
                    CorridorObserver?.Invoke("wait.pickup-body",visit.Id,actor.ActorId,actor.transform.position,path,null,"employee-no-path-or-destination-blocked");
#endif
                    return false;
                }
            }
            failure.Reason=found?PickupWaitRefusal.Qualified:PickupWaitRefusal.NoObstructingBody;
            ObserveCorridor("wait.pickup-body",visit,path:path,result:found?"qualified":"no-obstructing-body");
            return found; // Never turn a static/reservation-only obstruction into an unbounded wait.
        }
#if UNITY_EDITOR
        private void RecordSegmentFailure(Visit visit,CustomerVisitState phase,Vector3 from,Vector3 next,
            IReadOnlyList<Vector3> nextPath,Vector3[] nextObstacles,float clearance,bool nextClear,
            IReadOnlyList<Vector3> fullPath,Vector3[] fullObstacles,bool fullClear,PickupWaitFailure wait,
            IReadOnlyList<Vector3> replanned,MovementResult? callback,int callbackGeneration)
        {
            // All formatting and enumeration are cold and behind the dedicated null check.
            // 原始query结果只格式化一次；不重新调用adapter/planner/native getters。
            string P(Vector3 p)=>p.ToString("F9");
            string Path(IEnumerable<Vector3> p)=>p==null?"NULL":string.Join(";",p.Select(P));
            string Flag(bool? value)=>value?.ToString()??"NOT_EVALUATED";
            var reason=wait.Reason==PickupWaitRefusal.StaticPathNull?"static-path-NULL":wait.Reason.ToString();
            var preceding=callback.HasValue?"request="+callback.Value.RequestId+" status="+callback.Value.Status+
                " reason="+callback.Value.Reason+" originalFailure="+callback.Value.OriginalFailure+" recoveryFailure="+callback.Value.RecoveryFailure+
                " retry="+callback.Value.RetryCount+" final="+P(callback.Value.FinalPosition)+" issuedGeneration="+callbackGeneration:"NONE";
            var owners=visits.Values.Select(v=>v.PickupOwner).Concat(new[]{pickupQueue}).Where(q=>q!=null).Distinct().ToArray();
            SegmentFailureObserver("SEGMENT_FAILURE frame="+Time.frameCount+" actor="+visit.Id+" phase="+phase+" state="+visit.State+
                " request="+visit.RequestId+" generation="+visit.Generation+" layout="+adapter.Revision+" segments="+visit.Segments+" replans="+visit.Replans+
                " from="+P(from)+" next="+P(next)+" final="+P(visit.FinalTarget.Position)+" remaining="+Path(visit.Route)+
                " nextPath="+Path(nextPath)+" nextObstacles="+Path(nextObstacles)+" clearance="+clearance.ToString("R")+" nextClear="+nextClear+
                " fullPath="+(!nextClear || phase!=CustomerVisitState.Exiting?"NOT_EVALUATED":Path(fullPath))+" fullObstacles="+Path(fullObstacles)+
                " fullClear="+(!nextClear || phase!=CustomerVisitState.Exiting?"NOT_EVALUATED":fullClear.ToString())+
                " wait="+reason+" waitActor="+wait.Actor?.ActorId+" native.active="+Flag(wait.Active)+" native.agentExists="+Flag(wait.AgentExists)+
                " native.enabled="+Flag(wait.Enabled)+" native.onNavMesh="+Flag(wait.OnNavMesh)+" native.stopped="+Flag(wait.Stopped)+
                " native.pending="+Flag(wait.Pending)+" native.complete="+Flag(wait.Complete)+" native.hasPath="+Flag(wait.HasPath)+
                " originalEmployeeSegmentDestination="+(wait.Destination.HasValue?P(wait.Destination.Value):"NOT_EVALUATED")+
                " replan="+(replanned==null?"NULL":"count="+replanned.Count+" path="+Path(replanned))+" replanBudget="+(visit.Replans>3?"exhausted":"evaluated")+
                " callback="+preceding+"\nactors="+string.Join(";",world.RegisteredActors.Where(a=>a!=null).Select(a=>a.ActorId+"@"+P(a.transform.position)))+
                "\nvisits="+string.Join(";",visits.Values.Select(v=>v.Id+" state="+v.State+" request="+v.RequestId+" gen="+v.Generation+
                    " assignment="+v.Assignment.Generation+"@"+P(v.Assignment.Position)+" reserved="+(v.ReservedPickup.HasValue?P(v.ReservedPickup.Value.Position):"NONE")+
                    " segment="+P(v.CurrentSegmentTarget.Position)+" final="+P(v.FinalTarget.Position)+" route="+Path(v.Route)))+
                "\nleases="+string.Join(";",owners.SelectMany((q,i)=>q.ServiceLeases.Select(l=>"owner="+i+" "+l.Key+"@"+P(l.Value)))));
        }
#endif
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
            DiscardYieldContinuation(YieldContinuation(visit));
            RetireCounterAdvance(visit);
            visit.PendingReservationAnchor=null;
            DiscardPaidRecovery(visit);
            AbortCustomerHandoff(visit,"customer-removed");
            visit.TrafficMovePending=false;RetireMovement(visit.TrafficMove);visit.TrafficMove=null;visit.TrafficSelection=null;visit.IssuedTraffic=null;
            visit.PendingCounter=null;
            if(traffic!=null)
                foreach(var pair in trafficKeys.Where(p=>p.Value.ActorId==id).ToArray())
                {RetireTrafficCandidate(pair.Value);trafficKeys.Remove(pair.Key);}
            trafficActorKeys.Remove(id);
            if(ReferenceEquals(refusedPickupFront,visit)) refusedPickupFront=null;
            ObserveCorridor("visit.before-remove",visit);
            visits.Remove(id); visit.Generation++; visit.State=CustomerVisitState.Removed;
            ClearPickupCorridor(visit);
            ClearOwnedRoute(visit);
            visit.PendingPickupSegment=false;
            if(visit.RequestId!=0) world?.Service.Cancel(visit.RequestId);
            visit.PickupOwner?.Remove(id); visit.PickupOwner?.ReleaseServicePosition(id);
            queue?.Remove(id); if(visit.Actor!=null) { world?.Unregister(visit.Actor); Destroy(visit.Actor.gameObject); }
            visit.Admission.Dispose();
            ObserveCorridor("visit.removed",visit);
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
        private bool IsActivePaidRecovery(Visit visit)=>visit.PaidPickup?.RecoveryUsed==true && !visit.PaidPickup.Failed &&
            visit.State==CustomerVisitState.MovingToPickup && !visit.PickupArrived && !visit.HasCollected &&
            (visit.PaidPickup.RecoveryPending || visit.TrafficMove!=null && visit.TrafficMove.Intent.Equals(visit.PaidPickup.ActiveIntent));
        // Availability suspends authorization, not this already-authenticated stationary source.
        // 只保留原普通Collection的真实handoff；移动/Return/Blocked及身份失效仍走原Abort。
        private bool PreservesHeldCollectionPreparation(Visit visit)
        {
            try
            {
                var pending=visit.PendingTraffic;
                if(traffic==null || pending?.Handle==null || !pending.Marked || !ReferenceEquals(pending,visit.IssuedTraffic) ||
                    !ReferenceEquals(pending.Flow,this) || !ReferenceEquals(pending.Visit,visit) || !ReferenceEquals(pending.Coordinator,traffic) ||
                    !ReferenceEquals(pending.Handle.Coordinator,traffic) || !pending.Handle.Predecessor.Equals(pending.Intent) ||
                    visit.State!=CustomerVisitState.PickupWaiting || visit.DeliveryYieldPhase!=0 || !visit.PickupArrived ||
                    visit.RequestId!=0 || visit.PendingPickupSegment || visit.TrafficMovePending ||
                    visit.TrafficMove==null || visit.TrafficMove.NativeGeneration!=0)return false;
                var predecessor=traffic.GetDecision(pending.Intent);
                if(predecessor.Kind!=TrafficDecisionKind.DeferredByBudget || predecessor.FailureDisposition!=TrafficFailureDisposition.None ||
                    predecessor.AcquiredSequence<=0)return false;
                foreach(var key in visit.TrafficCandidateFrames.Keys)
                {
                    var original=FindPickupPreparation(visit,key);
                    var release=trafficCandidates.TryGetValue(key,out var held)?held.CollectionRelease:null;
                    if(release!=null && ReferenceEquals(release.Pending,pending) && ReferenceEquals(release.Total,pending.Total) &&
                        ReferenceEquals(release.Actor,pending.Actor) && ReferenceEquals(release.Admission,pending.Admission) &&
                        release.Movement!=null && ReferenceEquals(release.Movement.Source,pending.Source) && release.Movement.Intent.Equals(pending.Intent) &&
                        CollectionReleaseIdentityCurrent(release))
                    {
                        var heldSource=traffic.GetDecision(key);
                        if(heldSource.FailureDisposition==TrafficFailureDisposition.None && heldSource.AcquiredSequence==0 &&
                            !traffic.HasActiveGrant(key) && !traffic.TryGetCommittedJourney(key,out _) &&
                            (heldSource.Kind==TrafficDecisionKind.DeferredByBudget || heldSource.Kind==TrafficDecisionKind.WaitingForTraffic || heldSource.Kind==TrafficDecisionKind.Granted))return true;
                    }
                    if(original==null || original.Operation!=PickupQueueService.MutationKind.CollectionLease ||
                        !ReferenceEquals(original.Pending,pending) || !ReferenceEquals(original.Total,pending.Total) ||
                        !ReferenceEquals(original.Actor,pending.Actor) || !ReferenceEquals(original.Admission,pending.Admission) ||
                        original.Movement==null || !ReferenceEquals(original.Movement.Source,pending.Source) || !original.Movement.Intent.Equals(pending.Intent) ||
                        !PickupFamilyIdentityCurrent(original))continue;
                    var source=traffic.GetDecision(key);
                    if(source.FailureDisposition==TrafficFailureDisposition.None && source.AcquiredSequence==0 &&
                        !traffic.HasActiveGrant(key) && !traffic.TryGetCommittedJourney(key,out _) &&
                        (source.Kind==TrafficDecisionKind.DeferredByBudget || source.Kind==TrafficDecisionKind.WaitingForTraffic ||
                         source.Kind==TrafficDecisionKind.Granted))return true;
                }
            }
            catch(Exception){ /* Unknown original identity preserves no hold exception. */ }
            return false;
        }
        private void HoldVisits(bool preservePaidRecovery=false,bool captureYieldContinuation=false)
        {
            deliveryTrafficPaths=null;
            suspended=true;
            foreach(var visit in visits.Values.ToArray())
            {
                if(visit.PendingCounterAdvance!=null && !CounterAdvanceIdentityCurrent(visit.PendingCounterAdvance,requireNavigation:false))
                    RetireCounterAdvance(visit);
                if(PreservesSameRevisionPaidPreparation(visit))continue;
                if(preservePaidRecovery && (IsActivePaidRecovery(visit) || IsActiveCollectionRecovery(visit)))continue;
                if(!preservePaidRecovery && !PreservesSameRevisionTraffic(visit))DiscardPaidRecovery(visit);
                visit.YieldReturnTraffic=null;
                if((!isActiveAndEnabled || integratedHeld || visit.Actor==null || !visit.Actor.isActiveAndEnabled) &&
                    !PreservesHeldCollectionPreparation(visit))
                    AbortCustomerHandoff(visit,"customer-owner-or-actor-inactive");
                if(visit.State==CustomerVisitState.Queued || visit.State==CustomerVisitState.Blocked ||
                    visit.State==CustomerVisitState.PickupWaiting || visit.State==CustomerVisitState.PickupService) continue;
                HoldVisit(visit,captureYieldContinuation);
            }
        }
        private bool PreservesSameRevisionTraffic(Visit visit)
            => traffic!=null && isActiveAndEnabled && !integratedHeld && Configured && !world.LayoutAvailable &&
                adapter.Revision==revision && visit.Actor!=null && visit.Actor.isActiveAndEnabled && visit.RequestId==0 &&
                (visit.PendingTraffic!=null && ReferenceEquals(visit.PendingTraffic.Coordinator,traffic) ||
                 visit.TrafficMove!=null && ReferenceEquals(visit.TrafficMove.Coordinator,traffic) &&
                 (visit.TrafficMove.Suspended || visit.TrafficMovePending));
        private bool PreservesSameRevisionPaidPreparation(Visit visit)
        {
            var paid=visit.PaidPickup;
            if(traffic==null || !isActiveAndEnabled || integratedHeld || !Configured || !adapter.isActiveAndEnabled ||
                !world.isActiveAndEnabled || world.LayoutAvailable || adapter.Revision!=revision ||
                visit.Actor==null || !visit.Actor.isActiveAndEnabled || !world.RegisteredActors.Contains(visit.Actor) ||
                visit.RequestId!=0 || visit.TrafficMove!=null || visit.PendingTraffic!=null || paid?.Recovery?.BeforeTransfer!=true ||
                paid.ActiveIntent.Session!=trafficSession || paid.ActiveIntent.Epoch!=trafficEpoch || paid.ActiveIntent.LayoutRevision!=adapter.Revision)return false;
            var waiting=visit.State==CustomerVisitState.Blocked && paid.Failed && !paid.RecoveryUsed && !paid.RecoveryPending &&
                paid.RecoveryReceipt!=null && paid.ActiveIntent.Equals(paid.Intent);
            var pending=visit.State==CustomerVisitState.MovingToPickup && !paid.Failed && paid.RecoveryUsed && paid.RecoveryPending &&
                paid.RecoveryReceipt==null && !paid.ActiveIntent.Equals(paid.Intent);
            if(pending)
            {
                var kind=traffic.GetDecision(paid.ActiveIntent).Kind;
                pending=kind==TrafficDecisionKind.DeferredByBudget || kind==TrafficDecisionKind.WaitingForTraffic || kind==TrafficDecisionKind.Granted;
            }
            // Retain the logical checkpoint only; this identity-only path cannot authorize Resume or issue a leg.
            // 暂时无 Nav 时仍须原 Counter/Pickup/三枚 token；恢复授权与移动继续使用完整 Nav 检查。
            return (waiting || pending) && PaidPreparationIdentityCurrent(visit,paid.Recovery,requireNavigation:false);
        }
        private void HoldVisit(Visit visit,bool captureYieldContinuation=false)
        {
            if(visit.PendingCounterAdvance!=null && !CounterAdvanceIdentityCurrent(visit.PendingCounterAdvance,requireNavigation:false))
                RetireCounterAdvance(visit);
            if(PreservesSameRevisionTraffic(visit) || PreservesSameRevisionPaidPreparation(visit))return;
            RetireCounterAdvance(visit);
            var captured=captureYieldContinuation?CaptureYieldOwnerHold(visit):null;
            if(captured==null)DiscardYieldContinuation(YieldContinuation(visit));
            DiscardPaidRecovery(visit); // Actual cancellation also revokes any remaining explicit recovery checkpoint.
            AbortCustomerHandoff(visit,"customer-held-or-context-retired");
            visit.TrafficMovePending=false;RetireMovement(visit.TrafficMove);visit.TrafficMove=null;visit.TrafficSelection=null;visit.IssuedTraffic=null;
            if(visit.PendingCounter!=null)visit.PendingCounter.Coordinator.Retire(visit.PendingCounter.Intent);
            visit.PendingCounter=null;
            ClearOwnedRoute(visit);
            visit.PendingExitSegment=visit.PendingPickupSegment=false; visit.Generation++; var request=visit.RequestId; visit.RequestId=0; visit.State=CustomerVisitState.Blocked;
            if(captured!=null)captured.HeldGeneration=visit.Generation;
            try
            {
                var cancelled=request!=0 && world!=null && world.Service.Cancel(request);
                if(captured!=null)
                {
                    captured.CancellationConfirmed=captured.Plan.Coordinator.ConfirmYieldOwnerHoldCancellation(captured.Receipt,cancelled);
                    if(!captured.CancellationConfirmed)DiscardYieldContinuation(captured);
                }
            }
            catch{DiscardYieldContinuation(captured);throw;}
        }
        public void ResumeBlockedVisits()
        {
            if(!isActiveAndEnabled || !Configured || !world.LayoutAvailable || queue==null) return;
            if(integratedMode && (time.CurrentSpeed==GameSpeed.Paused || time.IsResumeBlocked ||
                decoration!=null && decoration.IsOpen)) return;
            integratedHeld=false;
            if(traffic==null)
            {
                // P12/component flow keeps its original whole-queue cancellation/reflow contract.
                HoldVisits(preservePaidRecovery:true);queue.RestartAssignments();
            }
            else
            {
                // A failed employee/other visit does not retire a healthy owned movement or pending handoff.
                // 真实Blocked已在失败/hold阶段退休；这里只恢复它的Counter重排，不清健康付费路线/claims。
                var counterRetries=visits.Values.Where(visit=>visit.State==CustomerVisitState.Blocked && visit.Actor!=null && visit.Actor.isActiveAndEnabled &&
                    !visit.PickupInTransit && !visit.HasCollected && (visit.ResumeState==CustomerVisitState.Entering ||
                    visit.ResumeState==CustomerVisitState.Advancing || visit.ResumeState==CustomerVisitState.Queued)).Select(visit=>visit.Id).ToArray();
                queue.RestartAssignments(counterRetries);
            }
            suspended=false;recoveringQueue=true;
            // Explicit collected Exit recovery rebinds before any new request is sent.
            // 仅显式恢复已收杯离场；先核验原站位，不移动角色或放宽交通权限。
            var exitRebinds=visits.Values.Where(v=>v.HasCollected && v.ResumeState==CustomerVisitState.Exiting &&
                v.Actor!=null && v.Actor.isActiveAndEnabled && !TrafficActorReady(v.Actor)).ToArray();
            var exitRebindFailure="";
            var exitRebound=exitRebinds.Length==0 || world.ValidateAndRebindActors(out exitRebindFailure);
            foreach(var visit in visits.Values.ToArray())
            {
                if(traffic!=null && visit.State!=CustomerVisitState.Blocked)continue;
                if(visit.Actor==null || !visit.Actor.isActiveAndEnabled) continue;
                if(IsActivePaidRecovery(visit) || IsActiveCollectionRecovery(visit))continue; // Repeated Resume cannot cancel its already-authorized live request.
                if(visit.DeliveryYieldPhase==1 && YieldContinuation(visit)!=null)continue; // Only CLC actual-paid Owner.Resume may consume this checkpoint.
                if(visit.FailedCollection!=null && !visit.FailedCollection.Started){TryResumeOrdinaryCollection(visit);continue;}
                // Only the separate explicit paid-recovery seam may consume an authenticated Native failure.
                if(visit.PaidPickup?.Failed==true && !visit.HasCollected)continue;
                if(visit.ResumeState==CustomerVisitState.Exiting)
                {
                    if(exitRebinds.Contains(visit) && (!exitRebound || !TrafficActorReady(visit.Actor)))
                    {
                        BlockVisit(visit,CustomerVisitState.Exiting,exitRebound?
                            "Exit actor remains unavailable after safe rebind":"Exit resume rebind failed: "+exitRebindFailure);
                        continue;
                    }
                    // Explicit recovery is a new bounded traffic attempt, not a new collection.
                    // 只有明确恢复才退休失败key；修复后的路线仍须经过正常ready batch核验。
                    if(traffic!=null && visit.State==CustomerVisitState.Blocked)
                        foreach(var pair in trafficKeys.Where(p=>p.Value.ActorId==visit.Id && p.Value.IntentId=="actual-exit").ToArray())
                        {
                            var result=traffic.GetDecision(pair.Value);
                            if(result.Kind==TrafficDecisionKind.Granted || result.Kind==TrafficDecisionKind.DeferredByBudget ||
                                result.Kind==TrafficDecisionKind.WaitingForTraffic)continue;
                            RetireTrafficCandidate(pair.Value);trafficKeys.Remove(pair.Key);
                        }
                    Send(visit,CustomerVisitState.Exiting,new NavigationTarget(exit.position));
                }
                else if(integratedMode && visit.State==CustomerVisitState.Blocked && IsPickupMovement(visit.ResumeState))
                    Send(visit,visit.ResumeState,visit.FinalTarget);
                else if(integratedMode && (visit.State==CustomerVisitState.PickupWaiting || visit.State==CustomerVisitState.PickupService)) continue;
                else if(visit.State!=CustomerVisitState.Queued) visit.State=CustomerVisitState.Blocked;
            }
        }
        private void OnDisable() { HoldVisits(captureYieldContinuation:true); clock?.Tick(0,false,false); }
        private void OnEnable() { suspended=true; }
        private void OnDestroy()
        { pickupPreparationLife++;disposed=true; foreach(var id in visits.Keys.ToArray()) RemoveVisit(id); Capacity=null; queue=null; pickupQueue=null;pickupPlanningRejections.Clear(); exitSnapshots.Clear(); }
    }
}


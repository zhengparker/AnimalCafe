using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AnimalCafe.Navigation
{
    // One policy owner; Native movement and canonical business ledgers remain external.
    // 纯C#协调器只持有空间许可，不分配Queue/FIFO/容量。
    public sealed class ServiceTrafficCoordinator
    {
        private sealed class Entry
        {
            // Stable identity survives release of snapshot/ActorLife-bearing payloads.
            // 真实退休后只留轻量身份，不再从可释放的 Request 取 key。
            internal readonly TrafficIntentKey Key;
            internal readonly long BirthOrder;
            internal Entry(TrafficIntentKey key,long birthOrder) { Key=key;BirthOrder=birthOrder; }
            internal TrafficRequest Request;
            internal TrafficBodyFact PreparationBody;
            internal TrafficParkingFact[] PreparationClaims;
            internal bool PreparationIdentityCaptured;
            internal TrafficDecision Decision;
            internal bool Parking, Queued, Prepared, Active, Retired, Discarded, ExplicitRecoveryUsed;
            internal bool PreparationFailed, EverCanonicalAttempt, ContinuationAttempted;
            // One exact original parent deadline; it is never a movement/WAIT permission.
            internal bool YieldOwnerWaitStarted,YieldOwnerWaitActive;
            internal TrafficIntentKey? YieldOwnerWaitChild,YieldOwnerParent,YieldOwnerOriginalChild;
            internal bool LegacyLegCompleted;
            internal bool ProducerPreviewHeld;
            internal TrafficUnpaidParkingEligibility UnpaidEligibility;
            internal bool UnpaidEligibilityHeld;
            internal bool DeliveryAlternateDebtGap;
            internal Dictionary<NativeChunkKey,IReadOnlyList<Vector3>> NativeChunks;
            internal object NativeChunkContext;internal bool NativeChunkReadAllowed;
            // A provider fault is not a proof that physical space is unavailable.
            // 原query异常/畸形结果只记本typed来源；不能转换成可恢复业务空间等待。
            internal bool UnpaidEligibilityQueryFault;
            internal TrafficDecision UnpaidEligibilitySpatialRefusal;
            internal readonly HashSet<string> UnpaidEligibilitySpatialActors=new HashSet<string>(StringComparer.Ordinal);
            internal UnpaidNativeActivity[] UnpaidEligibilityNativeActivity=Array.Empty<UnpaidNativeActivity>();
            internal TrafficRequest UnpaidEligibilityFacts;
            internal UnpaidEligibilityRight[] UnpaidEligibilityRights=Array.Empty<UnpaidEligibilityRight>();
            internal readonly HashSet<int> UnpaidEligibilityAttempts=new HashSet<int>();
            internal TrafficAdmissionReadiness AdmissionReadiness;
            internal TrafficCounterAdvanceReadiness CounterAdvanceReadiness;
            internal bool CounterAdvanceReadinessWaitStarted;
#if UNITY_EDITOR
            internal string CounterHandoffReadinessRefusal,CounterHandoffProducerRefusal;
#endif
            internal bool AdmissionHeld,AdmissionWaitStarted;
            internal Vector3[] AdmissionFirstLeg=Array.Empty<Vector3>();
            internal long RequestSequence, AcquiredSequence;
            internal int FirstFrame, Queries, JobIndex, Suspensions;
            internal int CandidateCount, CurrentIndex;
            internal readonly HashSet<int> AttemptedIndices = new HashSet<int>();
            internal float FiniteLimit;
            internal readonly List<RouteJob> Jobs = new List<RouteJob>();
            internal Vector3[] CurrentPath = Array.Empty<Vector3>(), Journey = Array.Empty<Vector3>();
            internal HashSet<TrafficIntentKey> FrozenOwners;
            internal Dictionary<TrafficIntentKey,HashSet<string>> FrozenResources;
            internal IReadOnlyDictionary<TrafficIntentKey,OriginalExitSelectedRoute> FrozenSelectedRouteEligibility;
            // Unresolved actual blockers survive a budget-deferred recheck; frozen eligibility outlives them.
            // 当前阻塞边与历史资格分开；普通 budget 延后不能解除等待，Granted 则已解除。
            internal IReadOnlyList<TrafficBlocker> WaitingBlockers=Array.Empty<TrafficBlocker>();
            internal float WaitRemaining;
            internal TrafficDecision JourneyPermit;
            internal TrafficRequest JourneyRequest;
            internal bool JourneyParking;
            internal LegState Leg;
            internal HandoffState PendingHandoff;
        }
        // One first-freeze capability. Actual WAIT still requires a current strict leg and normal progress checks.
        // 首次冻结仅保存原路线资格；真实等待边仍须当前已发腿、无环与原有限进展核验。
        private sealed class OriginalExitSelectedRoute
        {
            internal readonly Entry Owner;
            internal readonly TrafficDecision Source;
            internal readonly TrafficRequest Journey;
            internal readonly TrafficRouteProof Proof;
            internal readonly object ActorLife;
            internal readonly string Resource;
            internal readonly long AcquiredSequence;
            internal readonly int Index,Generation;
            internal readonly float FiniteLimit;
            internal readonly Vector3 ExitStart,ExitTarget;
            internal readonly Vector3[] SelectedRemainder;
            internal OriginalExitSelectedRoute(Entry exit,Entry owner,Vector3[] selectedRemainder)
            {
                Owner=owner;Source=owner.JourneyPermit;Journey=owner.JourneyRequest;Proof=owner.Leg.Proof;
                ActorLife=owner.Leg.ActorLife;Resource=owner.Request.ResourceId;AcquiredSequence=owner.AcquiredSequence;
                Index=owner.Leg.Index;Generation=owner.Leg.Generation;FiniteLimit=owner.FiniteLimit;
                ExitStart=exit.Request.Start;ExitTarget=exit.Request.Target;SelectedRemainder=selectedRemainder;
            }
        }
        // Scheduling provenance only. This record grants no WAIT, release promise, route or parking right.
        // 只观察原空间失败相关actor的真实已发Native阶段；不把body hint当Independent证明。
        private sealed class UnpaidNativeActivity
        {
            internal readonly Entry Owner;
            internal readonly LegState Leg;
            internal readonly TrafficDecision Source;
            internal readonly object ActorLife;
            internal readonly TrafficNativeStamp Stamp;
            internal readonly int Index,Generation;
            internal readonly Vector3 Target;
            internal readonly Vector3[] Path;
            internal readonly bool RequiresStableSpace;
            internal UnpaidNativeActivity(Entry owner,bool requiresStableSpace=false)
            {RequiresStableSpace=requiresStableSpace;Owner=owner;Leg=owner.Leg;Source=owner.JourneyPermit;ActorLife=Leg.ActorLife;Stamp=Leg.Stamp;
                Index=Leg.Index;Generation=Leg.Generation;Target=owner.Request.Target;Path=Leg.Path;}
            internal bool Same(UnpaidNativeActivity current)=>ReferenceEquals(Owner,current.Owner) && ReferenceEquals(Leg,current.Leg) &&
                ReferenceEquals(Source,current.Source) && ReferenceEquals(ActorLife,current.ActorLife) && Stamp.Equals(current.Stamp) &&
                Index==current.Index && Generation==current.Generation && Target==current.Target && ReferenceEquals(Path,current.Path);
        }
        private sealed class UnpaidEligibilityRight
        {
            internal readonly TrafficIntentKey Key;
            internal readonly Vector3 Target;
            internal readonly Vector3[] Current,Journey;
            internal UnpaidEligibilityRight(Entry entry)
            {Key=entry.Key;Target=entry.Request.Target;Current=entry.CurrentPath.ToArray();Journey=entry.Journey.ToArray();}
            internal bool Same(Entry entry)=>Key.Equals(entry.Key) && Target==entry.Request.Target &&
                Current.SequenceEqual(entry.CurrentPath) && Journey.SequenceEqual(entry.Journey);
        }
        private sealed class HandoffState
        {
            internal TrafficPhaseHandoff Handle;
            internal TrafficParkingFact OriginalClaim;
            internal TrafficParkingFact[] OriginalClaims;
            internal TrafficDecision Source;
            internal object ActorLife;
            internal float Remaining;
            internal TrafficPhaseMilestoneKind? CheckpointKind;
            internal Entry PreparedSuccessor;
            internal TrafficRequest PreparationRequest;
            internal TrafficDecision PreparationSource;
            internal ITrafficPhaseMilestone PreparationMilestone;
            internal bool InspectingPreparation,CommittingPreparedSuccessor;
            internal readonly Dictionary<Entry,HashSet<string>> Dependencies=new Dictionary<Entry,HashSet<string>>();
            // An actual completion checkpoint may schedule each original transient waiter only once.
            // This records scheduling, never claim release, permission, age or a new finite budget.
            internal readonly HashSet<Entry> CompletedBodyWakeups=new HashSet<Entry>();
        }
        private sealed class LegState
        {
            internal TrafficRouteProof Proof;
            internal object ActorLife;
            internal int Index, Generation;
            internal float ArrivalTolerance;
            internal bool NeedsQuery, Committed, Bound, Completed, ReprepareAllowed, Suspended;
            internal TrafficNativeStamp Stamp;
            internal Vector3[] Path, Suffix;
        }
        private readonly Dictionary<TrafficIntentKey, Entry> entries = new Dictionary<TrafficIntentKey, Entry>();
        private readonly HashSet<Entry> workEntries = new HashSet<Entry>();
        private readonly Dictionary<MovementRecoveryReceipt,RecoveryRecord> recoveryReceipts=new Dictionary<MovementRecoveryReceipt,RecoveryRecord>();
        private readonly HashSet<TrafficIntentKey> wakeups = new HashSet<TrafficIntentKey>();
        private readonly Func<Vector3, Vector3, IReadOnlyList<Vector3>> getCompletePath;
        // Opt-in contract: identity must change/null whenever Native geometry, filter or availability changes.
        // token认证真实owned NavMesh；默认null保留原query合同。
        private readonly Func<object> getNativePathContext;
        private readonly string session;
        private readonly int queriesPerFrame, queriesPerIntent;
        private readonly float waitSeconds;
        private long epoch, requestSequence, acquiredSequence, permit, entryBirthSequence;
        private int layoutRevision, queryFrame = -1;
        private TrafficWorldSnapshot snapshot;
        private bool closed, paused, committing;
        // The synchronous milestone callback cannot release edges before its claim result is authenticated.
        // 回调期间只保护当前 predecessor 的边；结束后按真实 Released/ Held 结果通知。
        private Entry completingHandoff;
        private TrafficIntentKey completingHandoffBody;
#if UNITY_EDITOR
        // Default-off original-decision witness; never re-run a query or progress predicate.
        internal Action<string> WaitFailureObserver;
        internal Action<string> CanonicalOrderingObserver;
        internal int CanonicalOrderingObservationErrors;
        internal Action<string> PrepareLegRejectionObserver;
        internal Action<TrafficRequest,TrafficWorldSnapshot> SnapshotChangedObserver;
        internal Action<TrafficIntentKey,string,TrafficRequest,TrafficRequest> UnpaidPreparationObserver;
        // Default-off Editor counter: actual source entries, before readiness filtering.
        // 只统计真正遍历的源 Entry；不代表 ready 数量、查询次数或 CPU 耗时。
        internal bool CountProcessReadySourceVisits { get; set; }
        internal int ProcessReadySourceVisits { get; private set; }
#endif

        public ServiceTrafficCoordinator(string session, long epoch, int layoutRevision,
            TrafficWorldSnapshot snapshot, Func<Vector3, Vector3, IReadOnlyList<Vector3>> getCompletePath,
            int queriesPerFrame = 128, int queriesPerIntent = 16384, float waitSeconds = 30, Func<object> getNativePathContext=null)
        {
            if (string.IsNullOrWhiteSpace(session)) throw new ArgumentException(nameof(session));
            if (queriesPerFrame < 1 || queriesPerFrame > 4000 || queriesPerIntent < 1 || queriesPerIntent > 16384 || !Positive(waitSeconds))
                throw new ArgumentOutOfRangeException(nameof(queriesPerFrame));
            this.session = session; this.epoch = epoch; this.layoutRevision = layoutRevision;
            this.snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            this.getCompletePath = getCompletePath ?? throw new ArgumentNullException(nameof(getCompletePath));
            this.getNativePathContext=getNativePathContext;
            this.queriesPerFrame = queriesPerFrame; this.queriesPerIntent = queriesPerIntent; this.waitSeconds = waitSeconds;
        }
        // Reuse the guarded milestone window; unfinished strict point legs still need real completion.
        // 复用已有核验窗口；单点腿被暂停或允许重新准备时，也不能提前结束 drain。
        public bool HasPendingWork => workEntries.Count!=0;
        // Read current stored work only; presentation cannot query, wake, or grant permission.
        // 只读当前事实；显示汇总不重查、不唤醒，也不授予权限。
        public TrafficDecisionKind? CurrentWorkPresentation
        {
            get
            {
                if(closed)return null;
                TrafficDecisionKind? result=null;
                foreach(var entry in workEntries)
                {
                    if(!LiveEntry(entry) || !Current(entry.Key) || entry.Decision==null ||
                        entry.Decision.FailureDisposition!=TrafficFailureDisposition.None)continue;
                    if(entry.Decision.Kind==TrafficDecisionKind.WaitingForTraffic)return TrafficDecisionKind.WaitingForTraffic;
                    if(entry.Decision.Kind==TrafficDecisionKind.DeferredByBudget)result=TrafficDecisionKind.DeferredByBudget;
                }
                return result;
            }
        }
        public int QueriesThisFrame { get; private set; }
        internal int ProofStepsThisFrame { get; private set; }
        internal long NativeChunkHits { get; private set; }
        public Exception LastPublicationException { get; private set; }
        public int QueriesConsumed(TrafficIntentKey intent) => entries.TryGetValue(intent, out var e) ? e.Queries : 0;
        public void SetSnapshot(TrafficWorldSnapshot value)
        {
            if (committing) throw new InvalidOperationException("Snapshot change inside joint commit.");
            snapshot = value ?? throw new ArgumentNullException(nameof(value));
            foreach(var pair in recoveryReceipts.ToArray())
            {
                var body=snapshot.Bodies.FirstOrDefault(b=>b.Owner.ActorId==pair.Value.Original.ActorId);
                if(!Current(pair.Value.Original) || !pair.Value.Actor.TryGetTarget(out var actor) || body==null ||
                    !Current(body.Owner) || !body.ActorActive || !ReferenceEquals(body.ActorLife,actor))recoveryReceipts.Remove(pair.Key);
                // Nav availability is temporary; an authenticated receipt survives only the same active actor/context.
                // 保留已认证 nonce 不授予移动权；capture/Begin/issue 仍各自要求 Nav Enabled。
            }
            foreach(var entry in OrderedWorkEntries.Where(e=>!e.Retired && e.PendingHandoff!=null).ToArray())
                if(!HandoffBodyCurrent(entry,entry.PendingHandoff))AbortHandoff(entry,"actor-lifecycle-changed");
            InspectSuccessorPreparations();
            foreach(var entry in OrderedWorkEntries.Where(e=>e.Queued && !e.ProducerPreviewHeld && !e.Retired && !e.Prepared && !e.Active).ToArray())
            {
                if(!LiveEntry(entry) || !entry.Queued || entry.Prepared || entry.Active)continue;
                var request=entry.Request;var own=snapshot.Bodies.FirstOrDefault(b=>b.Owner.ActorId==request.Intent.ActorId);
                if(entry.Leg!=null && entry.Leg.NeedsQuery)
                {
                    // This original unissued leg still owes a current-world query; own identity must survive first.
                    // 原 strict leg 尚待正常查询；身份不符直接跳过，不能落入旧 body-only 无条件复制。
                    if(!CanRefreshQueuedQueryLeg(entry))continue;
                }
                else if(request.Snapshot.Version!=snapshot.Version)
                {
                    if(!CanRefreshProspectiveParking(entry))continue;
                    // Only unchanged physical/owner facts retain a bounded frontier across canonical metadata changes.
                    // 未出生 parking 可先更新安全事实；真实几何/身份变化只清旧几何，不重置累计预算。
                    if(!SameProspectiveFacts(request.Snapshot,snapshot))
                    {entry.Jobs.Clear();entry.JobIndex=0;entry.CurrentPath=Array.Empty<Vector3>();}
                }
                if(own!=null && own.Position!=request.Start)continue;
                if(entry.Leg==null && HasStartedSelectedDetour(entry,request) && !PlanningJobsCompatible(entry,request))
                {entry.Jobs.Clear();entry.JobIndex=0;entry.CurrentPath=Array.Empty<Vector3>();}
                // A ready batch sees fresh other-body facts without depending on a caller polling first.
                entry.Request=new TrafficRequest(request.Intent,request.RequestedFrame,snapshot,request.Start,request.Target,request.Radius,
                    request.ResourceId,request.SelectedJourney,request.Requirements,request.NativeStamp,request.IndependentlyProgressing,
                    request.FiniteSeconds,request.ProvenFailure,request.SafetyMargin,request.PlanPrimaryRoute);
            }
        }
        private bool CanRefreshQueuedQueryLeg(Entry entry)
        {
            var request=entry.Request;var leg=entry.Leg;
            if(!LiveEntry(entry) || !Current(request.Intent) || !entry.Queued || entry.Prepared || entry.Active ||
                entry.Decision==null || entry.Decision.Kind!=TrafficDecisionKind.DeferredByBudget ||
                entry.Decision.FailureDisposition!=TrafficFailureDisposition.None || entry.PendingHandoff!=null ||
                leg==null || !leg.NeedsQuery || leg.Committed || leg.Bound || leg.Completed || leg.Suspended || leg.ReprepareAllowed ||
                leg.Stamp.RequestId!=0 || leg.Stamp.LegGeneration!=0 || request.NativeStamp.RequestId!=0 || request.NativeStamp.LegGeneration!=leg.Generation ||
                entry.JourneyPermit==null || entry.JourneyRequest==null || leg.Proof==null ||
                !SelectedProofs(entry).Any(proof=>ReferenceEquals(proof,leg.Proof)) || leg.Index<0 || leg.Index>=leg.Proof.Waypoints.Count ||
                request.ResourceId!=leg.Proof.ResourceId || request.Target!=leg.Proof.Waypoints[leg.Index] || !ValidSnapshot())return false;
            return StrictLegOwnFactsCurrent(entry);
        }
        // Identity only: the queued and prepared callers retain their own state/permission guards.
        // 共用原 strict snapshot 的自有身份核验，不把外部事实或版本相等当作授权。
        private Func<bool> ClaimInspectionGuard(Entry entry)
        {
            var request=entry.Request;var decision=entry.Decision;var world=snapshot;var leg=entry.Leg;
            var permit=entry.JourneyPermit;var journey=entry.JourneyRequest;var handoff=entry.PendingHandoff;
            var wasPaused=paused;var queued=entry.Queued;var prepared=entry.Prepared;var active=entry.Active;
            var parking=entry.Parking;var discarded=entry.Discarded;var acquired=entry.AcquiredSequence;var producerHeld=entry.ProducerPreviewHeld;
            var attempted=entry.EverCanonicalAttempt;var stamp=leg==null?default(TrafficNativeStamp):leg.Stamp;
            var committed=leg!=null && leg.Committed;var bound=leg!=null && leg.Bound;var completed=leg!=null && leg.Completed;
            var suspended=leg!=null && leg.Suspended;var reprepare=leg!=null && leg.ReprepareAllowed;var needsQuery=leg!=null && leg.NeedsQuery;
            return ()=>!committing && paused==wasPaused && LiveEntry(entry) && Current(entry.Key) &&
                ReferenceEquals(entry.Request,request) && ReferenceEquals(entry.Decision,decision) && ReferenceEquals(snapshot,world) &&
                ReferenceEquals(entry.Leg,leg) && ReferenceEquals(entry.JourneyPermit,permit) && ReferenceEquals(entry.JourneyRequest,journey) &&
                ReferenceEquals(entry.PendingHandoff,handoff) && entry.Queued==queued && entry.Prepared==prepared && entry.Active==active &&
                entry.Parking==parking && entry.Discarded==discarded && entry.AcquiredSequence==acquired && entry.EverCanonicalAttempt==attempted && entry.ProducerPreviewHeld==producerHeld &&
                (leg==null || leg.Stamp.Equals(stamp) && leg.Committed==committed && leg.Bound==bound && leg.Completed==completed &&
                    leg.Suspended==suspended && leg.ReprepareAllowed==reprepare && leg.NeedsQuery==needsQuery);
        }
        private bool StrictLegOwnFactsCurrent(Entry entry)
        {
            var request=entry.Request;var leg=entry.Leg;var inspect=ClaimInspectionGuard(entry);
            var original=request.Snapshot;var world=snapshot;var actor=request.Intent.ActorId;
            var before=original.Bodies.Where(body=>body!=null && body.Owner.ActorId==actor).ToArray();
            var current=world.Bodies.Where(body=>body.Owner.ActorId==actor).ToArray();
            if(before.Length!=1 || current.Length!=1)return false;
            var oldBody=before[0];var body=current[0];
            if(!Current(body.Owner) || !oldBody.Owner.Equals(body.Owner) || oldBody.ActorLife==null ||
                !ReferenceEquals(oldBody.ActorLife,leg.ActorLife) || !ReferenceEquals(oldBody.ActorLife,body.ActorLife) ||
                !oldBody.Enabled || !oldBody.ActorActive || !body.Enabled || !body.ActorActive ||
                oldBody.Position!=request.Start || body.Position!=request.Start || oldBody.Radius!=body.Radius || oldBody.SafetyMargin!=body.SafetyMargin ||
                oldBody.IndependentlyProgressing!=body.IndependentlyProgressing || oldBody.RemainingSeconds!=body.RemainingSeconds)return false;
            // Authenticate all claims for this ActorId, even when the physical owner is a prior intent.
            // 原 strict snapshot 是身份依据；外部新身体/占位留给原 EvaluateRetainedLeg 完整碰撞检查。
            if(original.Parking.Any(claim=>claim==null))return false;
            var oldClaims=original.Parking.Where(claim=>claim.Owner.ActorId==actor).ToArray();
            var claims=world.Parking.Where(claim=>claim.Owner.ActorId==actor).ToArray();
            bool Same(TrafficParkingFact old,TrafficParkingFact now)
            {
                if(!inspect() || !SameOwnedClaim(old,now) || !old.Position.Equals(now.Position) ||
                    old.Radius!=now.Radius || old.SafetyMargin!=now.SafetyMargin)return false;
                // Only a typed current claim can authenticate unrelated canonical bookkeeping.
                // null/opaque仍保留原OwnerVersion与checkpoint实例的严格匹配。
                var typed=old.SemanticCheckpoint is ITrafficClaimCheckpoint || now.SemanticCheckpoint is ITrafficClaimCheckpoint;
                if(!typed && !SameClaimFacts(old,now))return false;
                var same=TrafficClaimCheckpoints.AreSame(old,now);return inspect() && same;
            }
            return oldClaims.Length==claims.Length && oldClaims.All(old=>claims.Count(now=>Same(old,now))==1) &&
                claims.All(now=>oldClaims.Count(old=>Same(old,now))==1) && inspect();
        }
        private bool CanRefreshProspectiveParking(Entry entry)
        {
            if(!LiveEntry(entry))return false;
            var request=entry.Request;
            if(!Current(request.Intent) || !entry.Parking || !entry.Queued || entry.Retired || entry.Prepared || entry.Active ||
                entry.Decision==null || entry.Decision.Kind!=TrafficDecisionKind.DeferredByBudget ||
                entry.Decision.FailureDisposition!=TrafficFailureDisposition.None || entry.AcquiredSequence!=0 ||
                entry.Leg!=null || entry.JourneyPermit!=null || entry.JourneyRequest!=null || entry.Journey.Length!=0 ||
                entry.PendingHandoff!=null || entry.EverCanonicalAttempt || entry.ExplicitRecoveryUsed || entry.LegacyLegCompleted ||
                request.NativeStamp.RequestId!=0 || request.SelectedJourney.Count!=0 || request.Requirements.Any(r=>r.SelectedJourney))return false;
            var actor=request.Intent.ActorId;
            // No existing physical actor or canonical claim may borrow unborn proposal refresh authority.
            // 两份快照都检查 ActorId，防止用同一身体的不同 intent key 绕过已有所有权。
            return !request.Snapshot.Bodies.Any(b=>b.Owner.ActorId==actor) && !snapshot.Bodies.Any(b=>b.Owner.ActorId==actor) &&
                !request.Snapshot.Parking.Any(p=>p.Owner.ActorId==actor) && !snapshot.Parking.Any(p=>p.Owner.ActorId==actor);
        }
        private static bool SameProspectiveFacts(TrafficWorldSnapshot old,TrafficWorldSnapshot current)
        {
            if(old.Bodies.Count!=current.Bodies.Count || old.Parking.Count!=current.Parking.Count)return false;
            for(var i=0;i<old.Bodies.Count;i++)
            {
                var a=old.Bodies[i];var b=current.Bodies[i];
                if(!a.Owner.Equals(b.Owner) || !ReferenceEquals(a.ActorLife,b.ActorLife) || a.Position!=b.Position ||
                    a.Radius!=b.Radius || a.SafetyMargin!=b.SafetyMargin || a.Enabled!=b.Enabled || a.ActorActive!=b.ActorActive ||
                    a.IndependentlyProgressing!=b.IndependentlyProgressing || a.RemainingSeconds!=b.RemainingSeconds)return false;
            }
            for(var i=0;i<old.Parking.Count;i++)
            {
                var a=old.Parking[i];var b=current.Parking[i];
                if(!a.Owner.Equals(b.Owner) || !ReferenceEquals(a.CanonicalOwner,b.CanonicalOwner) || a.ResourceId!=b.ResourceId ||
                    a.Position!=b.Position || a.Radius!=b.Radius || a.SafetyMargin!=b.SafetyMargin ||
                    !ReferenceEquals(a.BusinessOwner,b.BusinessOwner) || !ReferenceEquals(a.ReleaseObligation,b.ReleaseObligation))return false;
                // OwnerVersion alone can describe unrelated queue bookkeeping; it never changes physical clearance.
            }
            return true;
        }
        public TrafficDecision EvaluateParking(TrafficRequest request, int candidateIndex=0, int candidateCount=1)
            => Submit(request, true, candidateIndex, candidateCount);
        public TrafficDecision EvaluateMovement(TrafficRequest request, int candidateIndex=0, int candidateCount=1)
            => Submit(request, false, candidateIndex, candidateCount);
        // Revoke only an unissued unborn parking permission; actual canonical attempts cannot enter here.
        // 仅撤销未出生、未提交的准备许可；保留原意图全部预算/依赖，由正常 ready batch 重新证明。
        internal bool TryCancelCanonicalAnchorPreparation(TrafficDecision expected)
        {
            if(closed || committing || expected==null || !Current(expected.Intent) ||
                !entries.TryGetValue(expected.Intent,out var entry) || !LiveEntry(entry) || !ReferenceEquals(entry.Decision,expected) ||
                expected.Kind!=TrafficDecisionKind.Granted || expected.FailureDisposition!=TrafficFailureDisposition.None ||
                !entry.Parking || !entry.Prepared || entry.Queued || entry.Retired || entry.Discarded || entry.Active || entry.AcquiredSequence!=0 ||
                entry.Leg!=null || entry.JourneyPermit!=null || entry.JourneyRequest!=null || entry.Journey.Length!=0 ||
                entry.PendingHandoff!=null || entry.EverCanonicalAttempt || entry.ExplicitRecoveryUsed || entry.LegacyLegCompleted ||
                !entry.PreparationIdentityCaptured || entry.PreparationBody!=null || entry.PreparationClaims.Length!=0 ||
                !ValidSnapshot() || !PreparationIdentityCurrent(entry))return false;
            var request=entry.Request;
            if(request.SelectedJourney.Count!=0 || request.Requirements.Any(r=>r.SelectedJourney) ||
                !request.NativeStamp.Equals(default(TrafficNativeStamp)) || request.ProvenFailure.HasValue)return false;
            // An expired snapshot may revoke authority; only the subsequent normal reproof can grant again.
            // 撤销无需旧 snapshot 仍 current；不刷新事实、不查询路径、不触发 canonical 回调。
            entry.Prepared=false;entry.Queued=true;entry.Jobs.Clear();entry.JobIndex=0;entry.CurrentPath=Array.Empty<Vector3>();
            Decide(entry,TrafficDecisionKind.DeferredByBudget,"canonical-anchor-preparation-cancelled");return true;
        }
        // Revoke only stale, unissued physical authority; the original queued port owns metadata reproof.
        // 只撤销过期的 physical 准备许可；保留原请求、预算、依赖与几何，fresh 许可不反复撤销。
        internal bool TryRevokePhysicalPreparedPreparation(TrafficDecision expected,out TrafficDecision next)
        {
            next=null;
            if(closed || paused || committing || expected==null || !Current(expected.Intent) ||
                !entries.TryGetValue(expected.Intent,out var entry) || !LiveEntry(entry) || !ReferenceEquals(entry.Decision,expected) ||
                expected.Kind!=TrafficDecisionKind.Granted || expected.FailureDisposition!=TrafficFailureDisposition.None ||
                !entry.Prepared || entry.Queued || entry.Active || entry.Retired || entry.Discarded || entry.AcquiredSequence!=0 ||
                entry.Leg!=null || entry.JourneyPermit!=null || entry.JourneyRequest!=null || entry.Journey.Length!=0 ||
                entry.PendingHandoff!=null || entry.EverCanonicalAttempt || entry.ExplicitRecoveryUsed || entry.LegacyLegCompleted ||
                !Valid(entry.Request) || !ValidSnapshot() || SnapshotCurrent(entry.Request))return false;
            var request=entry.Request;var captured=entry.PreparationBody;
            if(request.SelectedJourney.Count!=0 || request.Requirements.Any(r=>r.SelectedJourney) ||
                !request.NativeStamp.Equals(default(TrafficNativeStamp)) || request.ProvenFailure.HasValue ||
                captured==null || !captured.Enabled || !captured.ActorActive || !Current(captured.Owner) ||
                !Finite(captured.RemainingSeconds) || captured.RemainingSeconds<0 || !PreparationIdentityCurrent(entry))return false;
            var body=snapshot.Bodies.Single(b=>b.Owner.ActorId==request.Intent.ActorId);
            if(body.IndependentlyProgressing!=captured.IndependentlyProgressing || body.RemainingSeconds!=captured.RemainingSeconds ||
                entry.PreparationClaims.Any(old=>!Current(old.Owner) || !snapshot.Parking.Any(current=>SameClaimFacts(old,current))))return false;
            entry.Prepared=false;entry.Queued=true;
            next=Decide(entry,TrafficDecisionKind.DeferredByBudget,"physical-preparation-revoked");return true;
        }

        // The producer authenticates its original task/preview before refreshing this pending proposal.
        // 仅刷新同一未获权候选；真实 task/preview 仍由原 producer 验证，不产生新许可或预算。
        internal bool TryRefreshQueuedPreparation(TrafficDecision expected,TrafficRequest refreshed,out TrafficDecision next)
        {
            next=null;
            if(closed || paused || committing || expected==null || refreshed==null || !Current(expected.Intent) ||
                !refreshed.Intent.Equals(expected.Intent) || !entries.TryGetValue(expected.Intent,out var entry) ||
                !LiveEntry(entry) || !ReferenceEquals(entry.Decision,expected) || expected.FailureDisposition!=TrafficFailureDisposition.None ||
                (expected.Kind!=TrafficDecisionKind.DeferredByBudget && expected.Kind!=TrafficDecisionKind.WaitingForTraffic) ||
                !entry.Queued || entry.Prepared || entry.Active || entry.Retired || entry.Discarded ||
                entry.AcquiredSequence!=0 || entry.Leg!=null || entry.JourneyPermit!=null || entry.JourneyRequest!=null ||
                entry.Journey.Length!=0 || entry.PendingHandoff!=null || entry.EverCanonicalAttempt ||
                entry.ExplicitRecoveryUsed || entry.LegacyLegCompleted || !Valid(refreshed) ||
                !ReferenceEquals(refreshed.Snapshot,snapshot) || !SnapshotCurrent(refreshed) || !ValidSnapshot())return false;
            var original=entry.Request;
            if(!Valid(original) || original.SelectedJourney.Count!=0 || refreshed.SelectedJourney.Count!=0 ||
                !original.NativeStamp.Equals(default(TrafficNativeStamp)) || !refreshed.NativeStamp.Equals(original.NativeStamp) ||
                original.ProvenFailure.HasValue || refreshed.ProvenFailure.HasValue ||
                refreshed.Start!=original.Start || refreshed.Target!=original.Target || refreshed.ResourceId!=original.ResourceId ||
                refreshed.Radius!=original.Radius || refreshed.SafetyMargin!=original.SafetyMargin ||
                refreshed.FiniteSeconds!=entry.FiniteLimit || refreshed.FiniteSeconds!=original.FiniteSeconds ||
                refreshed.IndependentlyProgressing!=original.IndependentlyProgressing ||
                refreshed.PlanPrimaryRoute!=original.PlanPrimaryRoute || refreshed.Requirements.Count!=original.Requirements.Count)return false;
            // Prospective selected-home is a proof role, not an acquired selected-journey source.
            // proof role/端点不可替换；只允许外部 obstacle 数据随实际 snapshot 更新。
            for(var index=0;index<original.Requirements.Count;index++)
            {
                var old=original.Requirements[index];var current=refreshed.Requirements[index];
                if(old.ResourceId!=current.ResourceId || old.Start!=current.Start || old.Goal!=current.Goal ||
                    old.Clearance!=current.Clearance || old.SelectedJourney!=current.SelectedJourney ||
                    old.AvoidProposedParking!=current.AvoidProposedParking)return false;
            }
            var capturedBody=entry.PreparationBody;
            if(capturedBody==null || !capturedBody.Enabled || !capturedBody.ActorActive ||
                !Current(capturedBody.Owner) || !Finite(capturedBody.RemainingSeconds) || capturedBody.RemainingSeconds<0 ||
                !PreparationIdentityCurrent(entry))return false;
            var body=snapshot.Bodies.Single(b=>b.Owner.ActorId==original.Intent.ActorId);
            if(body.IndependentlyProgressing!=capturedBody.IndependentlyProgressing || body.RemainingSeconds!=capturedBody.RemainingSeconds)return false;
            var inspect=ClaimInspectionGuard(entry);
            if(!QueuedPreparationClaimsCurrent(entry,inspect) || !inspect())return false;
            var compatible=SameRequirements(original.Requirements,refreshed.Requirements) &&
                (!HasPrimary(entry) || entry.Jobs.Count==0 || PlanningJobsCompatible(entry,refreshed));
            if(compatible && ReferenceEquals(original.Snapshot,snapshot)) { next=expected;return true; }
            entry.Request=WithPrimaryFrame(refreshed,entry.FirstFrame);
            if(!compatible) { entry.Jobs.Clear();entry.JobIndex=0;entry.CurrentPath=Array.Empty<Vector3>(); }
            // Already queued: keep the exact decision/current wait edges until the ordinary ready pump.
            // 不 Notify/Decide，不改变 frozen/current edges、候选集合、query/wait 累计值或年龄。
            next=expected;return true;
        }

        // The actual producer may replace nonselected safety roles only before any parking authority exists.
        // 原exact-shape port不变；本入口认证原own facts后走原Submit全量重证，不授予新权限。
        internal bool TryResubmitQueuedParkingPreparation(TrafficDecision expected,TrafficRequest refreshed,
            Func<bool> producerCurrent,out TrafficDecision next)
            =>TryResubmitQueuedRolePreparation(expected,refreshed,producerCurrent,true,out next);
        // Only an original unpaid-to-pickup movement primary with both actual typed queue claims may change external roles.
        // 原paid primary尚未获权；动态安全role由原producer完整重建，再用原Entry/预算走正常batch重证。
        internal bool TryResubmitQueuedPaidPickupPreparation(TrafficDecision expected,TrafficRequest refreshed,
            Func<bool> producerCurrent,out TrafficDecision next)
        {
            next=null;
            if(expected==null || refreshed==null || expected.Intent.IntentId!="pickup-paid-movement" ||
                !entries.TryGetValue(expected.Intent,out var entry) || entry.Request==null || !entry.Request.PlanPrimaryRoute || !refreshed.PlanPrimaryRoute ||
                entry.Request.ResourceId!="pickup/"+expected.Intent.ActorId || refreshed.ResourceId!=entry.Request.ResourceId ||
                entry.PreparationClaims==null || entry.PreparationClaims.Length!=2 ||
                !entry.PreparationClaims.Any(p=>p!=null && p.BusinessOwner!=null && p.ReleaseObligation!=null && p.ResourceId=="counter/"+expected.Intent.ActorId &&
                    p.CanonicalOwner is AnimalCafe.Customers.CounterQueueService && p.SemanticCheckpoint is AnimalCafe.Customers.CounterQueueService.AssignmentClaim counter && ReferenceEquals(p.CanonicalOwner,counter.CanonicalOwner) && counter.Assignment.Arrived && counter.Assignment.SlotIndex==0) ||
                !entry.PreparationClaims.Any(p=>p!=null && p.BusinessOwner!=null && p.ReleaseObligation!=null && p.ResourceId=="pickup/"+expected.Intent.ActorId &&
                    p.CanonicalOwner is AnimalCafe.Customers.PickupQueueService && p.SemanticCheckpoint is AnimalCafe.Customers.PickupQueueService.ReservationClaim pickup && ReferenceEquals(p.CanonicalOwner,pickup.CanonicalOwner) && pickup.Assignment.Position==entry.Request.Target) ||
                !entry.Request.Requirements.Concat(refreshed.Requirements).All(role=>role!=null && role.ResourceId!=null && !role.SelectedJourney && role.AvoidProposedParking &&
                    role.ResourceId.StartsWith("existing-goal/",StringComparison.Ordinal) && role.ResourceId!="existing-goal/"+expected.Intent.ActorId))return false;
            return TryResubmitQueuedRolePreparation(expected,refreshed,producerCurrent,false,out next);
        }
        private bool TryResubmitQueuedRolePreparation(TrafficDecision expected,TrafficRequest refreshed,
            Func<bool> producerCurrent,bool parking,out TrafficDecision next)
        {
            next=null;
            if(closed || paused || committing || producerCurrent==null || expected==null || refreshed==null || !Current(expected.Intent) ||
                !refreshed.Intent.Equals(expected.Intent) || !entries.TryGetValue(expected.Intent,out var entry) || !LiveEntry(entry) ||
                !ReferenceEquals(entry.Decision,expected) || expected.FailureDisposition!=TrafficFailureDisposition.None ||
                (expected.Kind!=TrafficDecisionKind.DeferredByBudget && expected.Kind!=TrafficDecisionKind.WaitingForTraffic) ||
                entry.Parking!=parking || !entry.Queued || entry.Prepared || entry.Active || entry.Discarded || entry.AcquiredSequence!=0 ||
                entry.Leg!=null || entry.JourneyPermit!=null || entry.JourneyRequest!=null || entry.Journey.Length!=0 || entry.PendingHandoff!=null ||
                entry.EverCanonicalAttempt || entry.ExplicitRecoveryUsed || entry.LegacyLegCompleted || !Valid(refreshed) ||
                !ReferenceEquals(refreshed.Snapshot,snapshot) || !SnapshotCurrent(refreshed) || !ValidSnapshot())return false;
            var original=entry.Request;
            if(!Valid(original) || original.SelectedJourney.Count!=0 || refreshed.SelectedJourney.Count!=0 ||
                !original.NativeStamp.Equals(default(TrafficNativeStamp)) || !refreshed.NativeStamp.Equals(original.NativeStamp) ||
                original.ProvenFailure.HasValue || refreshed.ProvenFailure.HasValue || !refreshed.Start.Equals(original.Start) ||
                !refreshed.Target.Equals(original.Target) || refreshed.ResourceId!=original.ResourceId || refreshed.Radius!=original.Radius ||
                refreshed.SafetyMargin!=original.SafetyMargin || refreshed.FiniteSeconds!=entry.FiniteLimit ||
                refreshed.FiniteSeconds!=original.FiniteSeconds || refreshed.IndependentlyProgressing!=original.IndependentlyProgressing ||
                refreshed.PlanPrimaryRoute!=original.PlanPrimaryRoute ||
                original.Requirements.Select(r=>r.ResourceId).Distinct(StringComparer.Ordinal).Count()!=original.Requirements.Count ||
                refreshed.Requirements.Select(r=>r.ResourceId).Distinct(StringComparer.Ordinal).Count()!=refreshed.Requirements.Count ||
                !SamePreparationRoleSchema(original.Requirements.Where(r=>r.SelectedJourney),refreshed.Requirements.Where(r=>r.SelectedJourney),ordered:true) ||
                SamePreparationRoleSchema(original.Requirements.Where(r=>!r.SelectedJourney),refreshed.Requirements.Where(r=>!r.SelectedJourney)))return false;
            var body=entry.PreparationBody;
            if(body==null || !body.Enabled || !body.ActorActive || !Current(body.Owner) || !Finite(body.RemainingSeconds) ||
                body.RemainingSeconds<0 || !PreparationIdentityCurrent(entry))return false;
            var actual=snapshot.Bodies.Single(b=>b.Owner.ActorId==original.Intent.ActorId);
            if(actual.IndependentlyProgressing!=body.IndependentlyProgressing || actual.RemainingSeconds!=body.RemainingSeconds)return false;
            var inspect=ClaimInspectionGuard(entry);var wait=entry.WaitRemaining;var queries=entry.Queries;var first=entry.FirstFrame;
            var sequence=entry.RequestSequence;var count=entry.CandidateCount;var index=entry.CurrentIndex;
            var frozen=entry.FrozenOwners;var resources=entry.FrozenResources;var edges=entry.WaitingBlockers;
            var attempted=entry.AttemptedIndices.ToArray();
            bool CurrentPreparation()=>inspect() && entry.WaitRemaining==wait && entry.Queries==queries && entry.FirstFrame==first &&
                entry.RequestSequence==sequence && entry.CandidateCount==count && entry.CurrentIndex==index &&
                ReferenceEquals(entry.FrozenOwners,frozen) && ReferenceEquals(entry.FrozenResources,resources) &&
                ReferenceEquals(entry.WaitingBlockers,edges) && entry.AttemptedIndices.SetEquals(attempted);
            try
            {
                bool Authenticated()=>CurrentPreparation() && producerCurrent() && CurrentPreparation();
                if(!Authenticated() || !QueuedPreparationClaimsCurrent(entry,Authenticated) || !Authenticated())return false;
                // Submit has no external authentication callback between this final guard and its unowned reset.
                // 同一Entry/候选/年龄/累计预算保留；完整current roles只能由正常batch重新查询。
                next=Submit(refreshed,parking,index,count);
                return LiveEntry(entry) && ReferenceEquals(entry.Decision,next) && next.FailureDisposition==TrafficFailureDisposition.None &&
                    next.Kind==TrafficDecisionKind.DeferredByBudget && entry.Queued && entry.FirstFrame==first;
            }
            catch{return false;}
        }
        private static bool SamePreparationRoleSchema(IEnumerable<TrafficRouteRequirement> first,IEnumerable<TrafficRouteRequirement> second,bool ordered=false)
        {
            var a=first.ToArray();var b=second.ToArray();
            bool Same(TrafficRouteRequirement old,TrafficRouteRequirement now)=>old.ResourceId==now.ResourceId && old.Start.Equals(now.Start) &&
                old.Goal.Equals(now.Goal) && old.Clearance==now.Clearance && old.SelectedJourney==now.SelectedJourney &&
                old.AvoidProposedParking==now.AvoidProposedParking;
            return a.Length==b.Length && (ordered?a.Select((old,index)=>Same(old,b[index])).All(value=>value):a.All(old=>b.Count(now=>Same(old,now))==1));
        }

        private bool QueuedPreparationClaimsCurrent(Entry entry,Func<bool> inspect)
        {
            var originals=entry.PreparationClaims;var actor=entry.Request.Intent.ActorId;
            var claims=snapshot.Parking.Where(claim=>claim.Owner.ActorId==actor).ToArray();
            bool Same(TrafficParkingFact old,TrafficParkingFact current)
            {
                if(!inspect() || !Current(old.Owner) || !SameOwnedClaim(old,current) || old.Position!=current.Position ||
                    old.Radius!=current.Radius || old.SafetyMargin!=current.SafetyMargin)return false;
                // Unknown markers retain exact old facts; either typed marker requires full authentication even at equal versions.
                var same=old.SemanticCheckpoint is ITrafficClaimCheckpoint || current.SemanticCheckpoint is ITrafficClaimCheckpoint
                    ? TrafficClaimCheckpoints.AreSame(old,current):SameClaimFacts(old,current);
                return inspect() && same;
            }
            return originals.Length==claims.Length && originals.All(old=>claims.Count(current=>Same(old,current))==1) &&
                claims.All(current=>originals.Count(old=>Same(old,current))==1) && inspect();
        }

        private bool CounterAdvancePreparationCurrent(Entry entry)
        {
            if(!LiveEntry(entry) || !Current(entry.Key) || entry.Key.IntentId!="counter-advance" || !entry.Parking || !HasPrimary(entry) ||
                entry.Active || entry.Discarded || entry.AcquiredSequence!=0 || entry.Leg!=null || entry.PendingHandoff!=null ||
                entry.JourneyPermit!=null || entry.JourneyRequest!=null || entry.Journey.Length!=0 || entry.EverCanonicalAttempt ||
                entry.ExplicitRecoveryUsed || entry.LegacyLegCompleted || entry.Decision.FailureDisposition!=TrafficFailureDisposition.None ||
                entry.Request.SelectedJourney.Count!=0 || entry.Request.Requirements.Any(r=>r.SelectedJourney) ||
                !entry.Request.NativeStamp.Equals(default(TrafficNativeStamp)) || entry.PreparationBody?.ActorLife==null ||
                !entry.PreparationBody.Enabled || !entry.PreparationBody.ActorActive || entry.PreparationClaims==null ||
                !entry.PreparationClaims.Any(p=>p.ResourceId==entry.Request.ResourceId && p.ResourceId=="counter/"+entry.Key.ActorId &&
                    p.CanonicalOwner is AnimalCafe.Customers.CounterQueueService && p.SemanticCheckpoint is ITrafficClaimCheckpoint &&
                    p.BusinessOwner!=null && p.ReleaseObligation!=null) || !PreparationIdentityCurrent(entry))return false;
            var inspect=ClaimInspectionGuard(entry);return QueuedPreparationClaimsCurrent(entry,inspect) && inspect();
        }
        internal bool TrySetCounterAdvanceReadiness(TrafficDecision expected,TrafficCounterAdvanceReadiness readiness)
        {
            if(closed || committing || expected==null || readiness==null || !entries.TryGetValue(expected.Intent,out var entry) ||
                !ReferenceEquals(entry.Decision,expected) || !readiness.Intent.Equals(entry.Key) || !CounterAdvancePreparationCurrent(entry) ||
                entry.CounterAdvanceReadiness!=null && !ReferenceEquals(entry.CounterAdvanceReadiness,readiness))return false;
            var original=entry.CounterAdvanceReadiness;var inspect=ClaimInspectionGuard(entry);bool current;
            try{current=readiness.IsCurrent();}catch(Exception){return false;}
            if(!current || !inspect() || !ReferenceEquals(entry.CounterAdvanceReadiness,original))return false;
            entry.CounterAdvanceReadiness=readiness;return true;
        }
        private bool CheckCounterAdvanceReadiness(Entry entry,List<TrafficBlocker> originalBlockers=null)
        {
            var readiness=entry.CounterAdvanceReadiness;
            if(readiness==null || entry.AcquiredSequence!=0 || entry.Leg!=null)return true;
            if(!CounterAdvancePreparationCurrent(entry))
            {Fail(entry,TrafficDecisionKind.StaleContext,"counter-advance-readiness-identity-changed");return false;}
            var inspect=ClaimInspectionGuard(entry);bool current;
            try{current=readiness.IsCurrent();}
            catch(Exception){if(inspect())Fail(entry,TrafficDecisionKind.StaleContext,"counter-advance-readiness-read-failed");return false;}
            if(!inspect() || !ReferenceEquals(entry.CounterAdvanceReadiness,readiness))return false;
            if(!current){Fail(entry,TrafficDecisionKind.StaleContext,"counter-advance-readiness-source-changed");return false;}
            var blockers=originalBlockers;
            if(blockers==null){blockers=EndpointBlockers(entry);AddParkingEndpointRouteBlockers(entry.Request,blockers);}
            if(blockers.Count==0)return true;
            if(!CheckCounterAdvanceHandoffReadiness(entry,readiness,blockers,out var handoffHeld))return false;
            if(handoffHeld)return HoldCounterAdvanceReadiness(entry,"counter-advance-handoff-readiness-held");
            if(!CheckCounterAdvanceContinuationReadiness(entry,readiness,blockers,out var continuationHeld))return false;
            if(continuationHeld)return HoldCounterAdvanceReadiness(entry,"counter-advance-continuation-readiness-held");
            foreach(var blocker in blockers)
            {
                // Only the observed earlier selected route, authenticated by its own producer.
                // 不把body/未知停车或未认证新phase包装成业务暂缓。
                if(blocker.Proof!="selected-journey-parking-protection" || blocker.Owner.ActorId==entry.Key.ActorId ||
                    (blocker.Owner.IntentId!="pickup-wait" && blocker.Owner.IntentId!="pickup-paid-movement") ||
                    !entries.TryGetValue(blocker.Owner,out var owner) || !LiveEntry(owner) ||
                    owner.Decision.FailureDisposition!=TrafficFailureDisposition.None ||
                    !(owner.Active || owner.Prepared || owner.Journey.Length>1))return true;
                float seconds;
                try{seconds=readiness.FiniteWorkingPredecessorSeconds(blocker.Owner);}
                catch(Exception){if(inspect())Fail(entry,TrafficDecisionKind.StaleContext,"counter-advance-work-read-failed");return false;}
                if(!inspect() || !ReferenceEquals(entry.CounterAdvanceReadiness,readiness))return false;
                if(float.IsNaN(seconds) || float.IsInfinity(seconds))
                {Fail(entry,TrafficDecisionKind.StaleContext,"counter-advance-work-invalid");return false;}
                if(seconds<=0)return true; // No actual finite task proof: keep the original policy.
            }
            return HoldCounterAdvanceReadiness(entry,"counter-advance-working-readiness-held");
        }
        // An authenticated same-journey next leg may be queued behind its existing follower's recheck.
        // 只保留已开始的原有限等待；不把未发腿算成独立运动、不添加新依赖或许可。
        private bool CheckCounterAdvanceContinuationReadiness(Entry entry,TrafficCounterAdvanceReadiness readiness,
            IReadOnlyList<TrafficBlocker> blockers,out bool held)
        {
            held=false;
            if(!entry.CounterAdvanceReadinessWaitStarted || !Positive(entry.WaitRemaining) || entry.FrozenOwners?.Count!=1 ||
                entry.FrozenResources==null)return true;
            var key=entry.FrozenOwners.Single();var resource="counter/"+key.ActorId;
            bool IsBody(TrafficBlocker b)=>b.Owner.Equals(key) && b.ResourceId=="body" && b.Proof=="live-body-checkpoint";
            bool IsRoute(TrafficBlocker b)=>b.Owner.Equals(key) && b.ResourceId==resource && b.Proof=="selected-journey-parking-protection";
            if(key.IntentId!="counter-advance" || key.ActorId==entry.Key.ActorId || !entry.FrozenResources.TryGetValue(key,out var resources) ||
                !resources.Contains("body") || !resources.Contains(resource) || !entry.WaitingBlockers.Any(IsRoute) ||
                !blockers.Any(IsBody) || !blockers.Any(IsRoute) || blockers.Any(b=>!IsBody(b) && !IsRoute(b)) ||
                !entries.TryGetValue(key,out var owner) || !LiveEntry(owner) || !Current(key))return true;
            var leg=owner.Leg;var request=owner.Request;var journey=owner.JourneyRequest;var permit=owner.JourneyPermit;
            if(!owner.Queued || owner.Prepared || owner.Active || owner.Parking || owner.Discarded || owner.PendingHandoff!=null ||
                owner.AcquiredSequence<=0 || owner.ExplicitRecoveryUsed || owner.Suspensions!=0 || owner.LegacyLegCompleted ||
                owner.Decision.Kind!=TrafficDecisionKind.DeferredByBudget || owner.Decision.FailureDisposition!=TrafficFailureDisposition.None ||
                (owner.Decision.Reason!="retained-leg-ready-batch-pending" && owner.Decision.Reason!="native-query-frame-budget") ||
                owner.WaitingBlockers.Count!=0 || owner.Journey.Length<2 || journey==null || permit==null ||
                !permit.Intent.Equals(key) || permit.AcquiredSequence!=owner.AcquiredSequence || leg==null ||
                leg.Index<=0 || leg.Committed || leg.Bound || leg.Completed || leg.Suspended || leg.ReprepareAllowed ||
                !leg.Stamp.Equals(default(TrafficNativeStamp)) || !request.NativeStamp.Equals(new TrafficNativeStamp(0,leg.Generation)) ||
                !request.IndependentlyProgressing || !Positive(request.FiniteSeconds) || !SnapshotCurrent(request) ||
                request.ResourceId!=resource || journey.ResourceId!=resource || leg.Proof==null || leg.Proof.ResourceId!=resource ||
                leg.Index>=leg.Proof.Waypoints.Count || leg.Proof.Waypoints[leg.Index]!=request.Target ||
                !permit.ProofRoutes.Any(proof=>ReferenceEquals(proof,leg.Proof)))return true;
            var bodies=snapshot.Bodies.Where(body=>body.Owner.ActorId==key.ActorId).ToArray();
            var claims=snapshot.Parking.Where(claim=>claim.Owner.ActorId==key.ActorId).ToArray();
            if(bodies.Length!=1 || !bodies[0].Owner.Equals(key) || bodies[0].IndependentlyProgressing || bodies[0].RemainingSeconds!=0 ||
                claims.Length!=1 || !claims[0].Owner.Equals(key) || claims[0].ResourceId!=resource ||
                !(claims[0].CanonicalOwner is AnimalCafe.Customers.CounterQueueService) ||
                !(claims[0].SemanticCheckpoint is AnimalCafe.Customers.CounterQueueService.AssignmentClaim claim) ||
                !ReferenceEquals(claims[0].CanonicalOwner,claim.CanonicalOwner) || !claim.IsCurrent || claim.Assignment.Arrived ||
                claim.Assignment.Position!=journey.Target || claims[0].BusinessOwner==null || claims[0].ReleaseObligation==null)return true;
            var inspect=ClaimInspectionGuard(entry);var inspectOwner=ClaimInspectionGuard(owner);var debt=entry.WaitRemaining;
            bool SamePair()=>inspect() && inspectOwner() && entry.WaitRemaining==debt && ReferenceEquals(entry.CounterAdvanceReadiness,readiness);
            var ownFacts=StrictLegOwnFactsCurrent(owner);
            if(!SamePair())return false;
            if(!ownFacts)return true;
#if UNITY_EDITOR
            if(WaitFailureObserver!=null)entry.CounterHandoffReadinessRefusal="core.continuation-producer";
#endif
            float seconds;
            try{seconds=readiness.FiniteWorkingPredecessorSeconds(key);}
            catch(Exception){if(SamePair())Fail(entry,TrafficDecisionKind.StaleContext,"counter-advance-continuation-read-failed");return false;}
            if(!SamePair())return false;
            if(float.IsNaN(seconds) || float.IsInfinity(seconds))
            {Fail(entry,TrafficDecisionKind.StaleContext,"counter-advance-continuation-invalid");return false;}
            held=seconds>0;return true;
        }
        private bool HoldCounterAdvanceReadiness(Entry entry,string reason)
        {
            if(!entry.CounterAdvanceReadinessWaitStarted)
            {
                var limit=Mathf.Min(waitSeconds,entry.FiniteLimit);
                entry.WaitRemaining=entry.FrozenOwners!=null || entry.AdmissionWaitStarted || entry.YieldOwnerWaitStarted?Mathf.Min(entry.WaitRemaining,limit):limit;
                entry.CounterAdvanceReadinessWaitStarted=true;
            }
            if(entry.WaitRemaining<=0){Fail(entry,TrafficDecisionKind.PlanningFailed,"wait-budget-exhausted",preparation:true);return false;}
            entry.Queued=true;entry.Prepared=false;entry.Active=false;entry.CurrentPath=entry.Journey=Array.Empty<Vector3>();
            if(entry.Decision.Kind!=TrafficDecisionKind.DeferredByBudget || entry.Decision.Reason!=reason)
                Decide(entry,TrafficDecisionKind.DeferredByBudget,reason);
            else SyncWork(entry);
            return false;
        }

        // Only the original captured Counter obligation may bridge its owner's next unissued first strict leg.
        // 仅暂缓未获权follower；不把旧body变Independent、不新增Frozen/body边，原strict handoff仍唯一接续。
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        internal void ObserveCounterHandoffProducerRefusal(TrafficIntentKey key,string stage)
        {
#if UNITY_EDITOR
            if(WaitFailureObserver!=null && entries.TryGetValue(key,out var entry))entry.CounterHandoffProducerRefusal=stage;
#endif
        }
        private bool CheckCounterAdvanceHandoffReadiness(Entry entry,TrafficCounterAdvanceReadiness readiness,
            List<TrafficBlocker> blockers,out bool held)
        {
            held=false;
            bool Skip(string stage)
            {
#if UNITY_EDITOR
                if(WaitFailureObserver!=null)entry.CounterHandoffReadinessRefusal=stage;
#endif
                return true;
            }
#if UNITY_EDITOR
            if(WaitFailureObserver!=null){entry.CounterHandoffReadinessRefusal="entered";entry.CounterHandoffProducerRefusal=null;}
#endif
            var bodyBlocks=blockers.Where(b=>b.ResourceId=="body" && b.Proof=="live-body-checkpoint" && b.Owner.IntentId=="counter-advance").ToArray();
            // A completed original body may already be clear of this target while its successor route is not.
            // 身体已不挡目标时，只从原冻结池寻找同一已捕获前客；后续全部handoff/claim认证仍执行。
            var routeOnly=bodyBlocks.Length==0;
            var previousKeys=bodyBlocks.Select(b=>b.Owner).ToArray();
            if(routeOnly && entry.FrozenOwners!=null && entry.FrozenResources!=null)
                previousKeys=entry.FrozenOwners.Where(key=>key.IntentId=="counter-advance" &&
                    entry.FrozenResources.TryGetValue(key,out var original) && original.Contains("body") && original.Contains("counter/"+key.ActorId) &&
                    blockers.Any(b=>b.Owner.ActorId==key.ActorId && !b.Owner.Equals(key) && b.Owner.IntentId=="counter-advance" &&
                        b.ResourceId=="counter/"+key.ActorId && b.Proof=="selected-journey-parking-protection")).ToArray();
            if(previousKeys.Length!=1)return Skip("core.1");
            if(!entries.TryGetValue(previousKeys[0],out var previous))return Skip("core.2");
            if(!LiveEntry(previous))return Skip("core.3");
            var state=previous.PendingHandoff;var resource="counter/"+previous.Key.ActorId;
            if(state==null)return Skip("core.4");
            if(state.CheckpointKind!=TrafficPhaseMilestoneKind.Arrived)return Skip("core.5");
            if(!Positive(state.Remaining))return Skip("core.6");
            if(!HandoffBodyCurrent(previous,state))return Skip("core.7");
            if(previous.AcquiredSequence<=0)return Skip("core.8");
            if(previous.Leg==null)return Skip("core.9");
            if(!previous.Leg.Committed)return Skip("core.10");
            if(!previous.Leg.Bound)return Skip("core.11");
            if(!previous.Leg.Completed)return Skip("core.12");
            if(previous.Leg.Suspended)return Skip("core.13");
            if(previous.Decision.FailureDisposition!=TrafficFailureDisposition.None)return Skip("core.14");
            if(!ReferenceEquals(previous.JourneyPermit,state.Source))return Skip("core.15");
            if(!state.Dependencies.TryGetValue(entry,out var captured))return Skip("core.16");
            if(!captured.Contains(resource))return Skip("core.17");
            if(routeOnly && !captured.Contains("body"))return Skip("core.route-only-body-not-captured");
            // Body may cross this gap only when the SAME original body resource was really captured and already frozen.
            // 不创造body许可；只接续原Native等待已捕获的同身体义务，仍由真实strict handoff唯一迁移。
            if(captured.Any(value=>value!=resource && value!="body"))return Skip("core.18");
            if(!state.CompletedBodyWakeups.Contains(entry))return Skip("core.19");
            if(entry.FrozenOwners==null)return Skip("core.20");
            if(entry.FrozenOwners.Count!=1)return Skip("core.21");
            if(!entry.FrozenOwners.Contains(previous.Key))return Skip("core.22");
            if(entry.FrozenResources==null)return Skip("core.23");
            if(!entry.FrozenResources.TryGetValue(previous.Key,out var frozen))return Skip("core.24");
            if(!frozen.Contains(resource))return Skip("core.25");
            if(captured.Any(value=>!frozen.Contains(value)))return Skip("core.captured-resource-not-originally-frozen");
            // Captured dependency + original Frozen above are durable; prepared Grant legitimately clears the transient blocker cache.
            // 原资源池已被真实handoff捕获；未commit的Grant后重排不会重建WaitingBlockers，不能把空cache当丢失身份。
            var nextKeys=blockers.Where(b=>!b.Owner.Equals(previous.Key)).Select(b=>b.Owner).Distinct().ToArray();
            if(nextKeys.Length!=1)return Skip("core.27");
            if(nextKeys[0].ActorId!=previous.Key.ActorId)return Skip("core.28");
            if(nextKeys[0].IntentId!="counter-advance")return Skip("core.29");
            if(!entries.TryGetValue(nextKeys[0],out var next))return Skip("core.30");
            if(!LiveEntry(next))return Skip("core.31");
            if(next.PendingHandoff!=null)return Skip("core.32");
            if(next.JourneyRequest==null)return Skip("core.33");
            if(next.JourneyPermit==null)return Skip("core.34");
            if(next.AcquiredSequence<=previous.AcquiredSequence)return Skip("core.35");
            if(next.Decision.FailureDisposition!=TrafficFailureDisposition.None)return Skip("core.36");
            if(next.Active)return Skip("core.37");
            if(next.Retired)return Skip("core.38");
            if(next.Discarded)return Skip("core.39");
            if(!(next.Queued || next.Prepared))return Skip("core.40");
            if(next.Leg==null)return Skip("core.41");
            if(next.Leg.Index!=0)return Skip("core.42");
            if(next.Leg.Committed)return Skip("core.43");
            if(next.Leg.Bound)return Skip("core.44");
            if(next.Leg.Completed)return Skip("core.45");
            if(next.Leg.Suspended)return Skip("core.46");
            if(next.Leg.ReprepareAllowed)return Skip("core.47");
            if(!ReferenceEquals(next.Leg.ActorLife,state.ActorLife))return Skip("core.48");
            if(next.Leg.Generation<=0)return Skip("core.49");
            if(next.Request.NativeStamp.RequestId!=0)return Skip("core.50");
            if(next.Request.NativeStamp.LegGeneration!=next.Leg.Generation)return Skip("core.51");
            if(next.Request.ResourceId!=resource)return Skip("core.52");
            if(next.JourneyRequest.ResourceId!=resource)return Skip("core.53");
            if(next.Leg.Proof.ResourceId!=resource)return Skip("core.54");
            if(!next.JourneyPermit.ProofRoutes.Any(proof=>ReferenceEquals(proof,next.Leg.Proof)))return Skip("core.55");
            if(!Positive(next.FiniteLimit))return Skip("core.56");
            var bodies=snapshot.Bodies.Where(body=>body.Owner.Equals(previous.Key)).ToArray();
            var claims=snapshot.Parking.Where(claim=>claim.Owner.Equals(previous.Key) && claim.ResourceId==resource).ToArray();
            if(bodies.Length!=1)return Skip("core.57");
            if(!bodies[0].Enabled)return Skip("core.58");
            if(!bodies[0].ActorActive)return Skip("core.59");
            if(bodies[0].IndependentlyProgressing)return Skip("core.60");
            if(bodies[0].RemainingSeconds!=0)return Skip("core.61");
            if(!ReferenceEquals(bodies[0].ActorLife,state.ActorLife))return Skip("core.62");
            if(bodies[0].Position!=next.Request.Start)return Skip("core.63");
            if(Vector3.Distance(bodies[0].Position,previous.Request.Target)>previous.Leg.ArrivalTolerance)return Skip("core.64");
            if(claims.Length!=1)return Skip("core.65");
            if(!(claims[0].CanonicalOwner is AnimalCafe.Customers.CounterQueueService))return Skip("core.66");
            if(!(claims[0].SemanticCheckpoint is AnimalCafe.Customers.CounterQueueService.AssignmentClaim claim))return Skip("core.67");
            if(!claim.IsCurrent)return Skip("core.68");
            if(claim.Assignment.Arrived)return Skip("core.69");
            if(!ReferenceEquals(claim.CanonicalOwner,claims[0].CanonicalOwner))return Skip("core.70");
            if(claim.Assignment.Position!=next.JourneyRequest.Target)return Skip("core.71");
            if(next.Leg.Proof.Waypoints.Last()!=claim.Assignment.Position)return Skip("core.72");
            if(claims[0].BusinessOwner==null)return Skip("core.73");
            if(claims[0].ReleaseObligation==null)return Skip("core.74");
            if(blockers.Any(blocker=>blocker.Owner.Equals(previous.Key)
                    ? !(blocker.ResourceId=="body" && blocker.Proof=="live-body-checkpoint" || blocker.ResourceId==resource && blocker.Proof=="selected-journey-parking-protection")
                    : !blocker.Owner.Equals(next.Key) || blocker.ResourceId!=resource || blocker.Proof!="selected-journey-parking-protection"))return Skip("core.75");
            var inspect=ClaimInspectionGuard(entry);var inspectPrevious=ClaimInspectionGuard(previous);var inspectNext=ClaimInspectionGuard(next);
            bool Current()=>inspect() && inspectPrevious() && inspectNext() && ReferenceEquals(previous.PendingHandoff,state) &&
                ReferenceEquals(entry.CounterAdvanceReadiness,readiness) && state.Dependencies.TryGetValue(entry,out var actual) && ReferenceEquals(actual,captured);
            foreach(var owner in new[]{previous.Key,next.Key})
            {
                float seconds;
                try{seconds=readiness.FiniteWorkingPredecessorSeconds(owner);}
                catch(Exception){if(Current())Fail(entry,TrafficDecisionKind.StaleContext,"counter-advance-handoff-read-failed");return false;}
                if(!Current())return false;
                if(float.IsNaN(seconds) || float.IsInfinity(seconds))
                {Fail(entry,TrafficDecisionKind.StaleContext,"counter-advance-handoff-invalid");return false;}
                if(seconds<=0)return Skip("core.76"); // No authentic original producer/receipt: keep the existing rejection policy.
            }
            held=true;return true;
        }

        // One original unborn producer view; this can only withhold a preparation, never grant rights.
        internal bool TrySetAdmissionReadiness(TrafficDecision expected,TrafficAdmissionReadiness readiness)
        {
            if(closed || committing || expected==null || readiness==null || !entries.TryGetValue(expected.Intent,out var entry) ||
                !ReferenceEquals(entry.Decision,expected) || !UnbornAdmissionCurrent(entry) || !readiness.Intent.Equals(entry.Key) ||
                entry.AdmissionReadiness!=null && !ReferenceEquals(entry.AdmissionReadiness,readiness))return false;
            var originalReadiness=entry.AdmissionReadiness;var inspect=ClaimInspectionGuard(entry);bool current;
            try{current=readiness.IsCurrent();}catch(Exception){return false;}
            if(!current || !inspect() || !ReferenceEquals(entry.AdmissionReadiness,originalReadiness))return false;
            entry.AdmissionReadiness=readiness;return true;
        }
        private bool UnbornAdmissionCurrent(Entry entry)
            =>LiveEntry(entry) && Current(entry.Key) && entry.Key.IntentId=="counter-admission" && entry.Parking && HasPrimary(entry) &&
                !entry.Active && !entry.Discarded && !entry.ProducerPreviewHeld && entry.AcquiredSequence==0 && entry.Leg==null &&
                entry.JourneyPermit==null && entry.JourneyRequest==null && entry.Journey.Length==0 && entry.PendingHandoff==null &&
                !entry.EverCanonicalAttempt && !entry.LegacyLegCompleted && !entry.ExplicitRecoveryUsed &&
                entry.Decision.FailureDisposition==TrafficFailureDisposition.None && entry.PreparationBody==null &&
                entry.Request.SelectedJourney.Count==0 && !entry.Request.Requirements.Any(r=>r.SelectedJourney) &&
                entry.Request.NativeStamp.Equals(default(TrafficNativeStamp)) && PreparationIdentityCurrent(entry);
        private Vector3[] AdmissionFirstLeg(Entry entry)
        {
            if(entry.AdmissionHeld)return entry.AdmissionFirstLeg;
            if(entry.Jobs.Count==0)return Array.Empty<Vector3>();
            var job=entry.Jobs[0];
            if(job.LegEnds.Length==0 || job.Waypoints.Length==0 || job.LegEnds[0]<0 || job.LegEnds[0]>=job.ExpandedPath.Length)
                return Array.Empty<Vector3>();
            return GuardCurrentPath(entry.Request.Start,job.ExpandedPath.Take(job.LegEnds[0]+1).ToArray(),job.Waypoints[0]);
        }
        // Stored exact first leg + real current exit Entry only; no planner, refresh, grant or new WAIT edge.
        // 只暂缓原未出生准备；已Bound的离店仍使用原JourneyRequest固定出口，不能变成当前拐点。
        private bool CheckAdmissionReadiness(Entry entry)
        {
            var readiness=entry.AdmissionReadiness;
            if(readiness==null || entry.AcquiredSequence!=0 || entry.Leg!=null)return true;
            if(!UnbornAdmissionCurrent(entry)){Fail(entry,TrafficDecisionKind.StaleContext,"admission-readiness-identity-changed");return false;}
            var path=AdmissionFirstLeg(entry);if(path.Length==0)return true; // Original planner has not produced a candidate yet.
            var inspect=ClaimInspectionGuard(entry);IReadOnlyList<TrafficIntentKey> exits;bool current;
            try
            {
                current=readiness.IsCurrent();if(!inspect() || !ReferenceEquals(entry.AdmissionReadiness,readiness))return false;
                exits=current?readiness.PendingExits()?.ToArray():null;
            }
            catch(Exception){if(inspect())Fail(entry,TrafficDecisionKind.StaleContext,"admission-readiness-read-failed");return false;}
            if(!inspect() || !ReferenceEquals(entry.AdmissionReadiness,readiness))return false;
            if(!current || exits==null){Fail(entry,TrafficDecisionKind.StaleContext,"admission-readiness-source-changed");return false;}
            var conflict=false;
            foreach(var key in exits.Distinct())
            {
                if(!Current(key) || key.IntentId!="actual-exit" || !entries.TryGetValue(key,out var leaving) || !LiveEntry(leaving) ||
                    leaving.Decision.FailureDisposition!=TrafficFailureDisposition.None)continue;
                var original=leaving.JourneyRequest??leaving.Request;
                var bodies=snapshot.Bodies.Where(b=>b.Owner.ActorId==key.ActorId).ToArray();
                var body=bodies.Length==1?bodies[0]:null;
                if(original==null || !original.Intent.Equals(key) || body==null || !body.ActorActive ||
                    leaving.PreparationBody?.ActorLife==null || !ReferenceEquals(body.ActorLife,leaving.PreparationBody.ActorLife))continue;
                if(NearPath(original.Target,path,PairClearance(entry.Request,original))){conflict=true;break;}
            }
            if(!conflict)return true;
            if(!entry.AdmissionWaitStarted)
            {
                if(entry.FrozenOwners==null)entry.WaitRemaining=entry.YieldOwnerWaitStarted?Mathf.Min(entry.WaitRemaining,Mathf.Min(waitSeconds,entry.FiniteLimit)):Mathf.Min(waitSeconds,entry.FiniteLimit);
                entry.AdmissionWaitStarted=true;
            }
            if(entry.WaitRemaining<=0){Fail(entry,TrafficDecisionKind.PlanningFailed,"wait-budget-exhausted",preparation:true);return false;}
            entry.AdmissionFirstLeg=path;entry.AdmissionHeld=true;entry.Queued=false;entry.Prepared=false;entry.Active=false;
            entry.CurrentPath=entry.Journey=Array.Empty<Vector3>();wakeups.Remove(entry.Key);
            if(entry.Decision.Reason!="admission-exit-readiness-held")Decide(entry,TrafficDecisionKind.DeferredByBudget,"admission-exit-readiness-held");
            else SyncWork(entry);
            return false;
        }

        // The typed producer authenticates its sealed Queue preview; this port preserves the original proposal.
        // 同一canonical anchor准备只更新安全事实/几何，不产生canonical写入、Native发腿或新预算。
        // Missing canonical preview only withholds scheduling; it grants no route, parking or WAIT authority.
        // preview 暂缺只暂停原准备；未付款 typed head 与已有 Frozen WAIT 各自保留原身份和预算。
        internal bool TryHoldCanonicalAnchorPreparation(TrafficDecision expected)
        {
            if(closed || paused || committing || expected==null || !Current(expected.Intent) ||
                !entries.TryGetValue(expected.Intent,out var entry) || !LiveEntry(entry) || !ReferenceEquals(entry.Decision,expected) ||
                expected.FailureDisposition!=TrafficFailureDisposition.None ||
                !entry.Parking || entry.Prepared || entry.Active || entry.Discarded || entry.AcquiredSequence!=0 || entry.Leg!=null ||
                entry.JourneyPermit!=null || entry.JourneyRequest!=null || entry.Journey.Length!=0 || entry.PendingHandoff!=null ||
                entry.EverCanonicalAttempt || entry.ExplicitRecoveryUsed || entry.LegacyLegCompleted ||
                !Valid(entry.Request) || !ValidSnapshot() || entry.Request.SelectedJourney.Count!=0 ||
                entry.Request.Requirements.Any(requirement=>requirement.SelectedJourney) || entry.Request.ProvenFailure.HasValue ||
                !entry.Request.NativeStamp.Equals(default(TrafficNativeStamp)))return false;
            var unpaidDeferred=expected.Kind==TrafficDecisionKind.DeferredByBudget && !entry.UnpaidEligibilityQueryFault &&
                UnpaidEligibilityCurrent(entry);
            if(!unpaidDeferred && (expected.Kind!=TrafficDecisionKind.WaitingForTraffic || !Positive(entry.WaitRemaining) ||
                entry.FrozenOwners==null || entry.FrozenOwners.Count==0 || entry.FrozenResources==null || entry.WaitingBlockers.Count==0 ||
                entry.FrozenOwners.Any(owner=>!entry.FrozenResources.TryGetValue(owner,out var resources) || resources.Count==0)))return false;
            var inspect=ClaimInspectionGuard(entry);
            if(!ProducerHoldIdentityCurrent(entry,inspect))return false;
            entry.ProducerPreviewHeld=true;entry.Queued=false;wakeups.Remove(entry.Key);SyncWork(entry);return true;
        }
        private bool ProducerHoldIdentityCurrent(Entry entry,Func<bool> inspect)
        {
            var captured=entry.PreparationBody;
            if(!entry.PreparationIdentityCaptured || captured==null || captured.ActorLife==null || !captured.Enabled || !captured.ActorActive ||
                !Current(captured.Owner) || !captured.Position.Equals(entry.Request.Start) || entry.PreparationClaims==null || entry.PreparationClaims.Length==0 ||
                entry.PreparationClaims.Any(claim=>claim==null || !(claim.SemanticCheckpoint is ITrafficClaimCheckpoint) ||
                    claim.CanonicalOwner==null || claim.BusinessOwner==null || claim.ReleaseObligation==null))return false;
            return PreparationIdentityCurrent(entry) && inspect() && QueuedPreparationClaimsCurrent(entry,inspect) && inspect();
        }
        internal bool TryReprepareCanonicalAnchor(TrafficDecision expected,TrafficRequest refreshedRequest,out TrafficDecision next)
        {
            if(!TryReprepareCanonicalAnchorCore(expected,refreshedRequest,out next))return false;
            if(!entries.TryGetValue(expected.Intent,out var entry) || !LiveEntry(entry) || !ReferenceEquals(entry.Decision,next))return false;
            if(entry.CounterAdvanceReadiness!=null && !CheckCounterAdvanceReadiness(entry))next=entry.Decision;
            return LiveEntry(entry) && ReferenceEquals(entry.Decision,next);
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ObserveUnpaidPreparation(Entry entry,string stage,TrafficRequest request,TrafficRequest previous=null)
        {
#if UNITY_EDITOR
            if(entry?.UnpaidEligibility==null || UnpaidPreparationObserver==null)return;
            try { UnpaidPreparationObserver(entry.Key,stage,request,previous??entry.Request); } catch { /* Observation cannot change the original decision. */ }
#endif
        }
        private bool TryReprepareCanonicalAnchorCore(TrafficDecision expected,TrafficRequest refreshedRequest,out TrafficDecision next)
        {
            next=null;
            if(closed || committing || expected==null || refreshedRequest==null || !Current(expected.Intent) ||
                !refreshedRequest.Intent.Equals(expected.Intent) || !entries.TryGetValue(expected.Intent,out var entry) ||
                !LiveEntry(entry) || !ReferenceEquals(entry.Decision,expected) || expected.FailureDisposition!=TrafficFailureDisposition.None ||
                !entry.Parking || entry.Retired || entry.Discarded || entry.Active || entry.AcquiredSequence!=0 || entry.ProducerPreviewHeld && paused ||
                entry.Leg!=null || entry.JourneyPermit!=null || entry.JourneyRequest!=null || entry.Journey.Length!=0 ||
                entry.PendingHandoff!=null || entry.EverCanonicalAttempt || entry.ExplicitRecoveryUsed || entry.LegacyLegCompleted ||
                !Valid(refreshedRequest) || !SnapshotCurrent(refreshedRequest) || !ValidSnapshot())return false;
            if(entry.UnpaidEligibility!=null && !UnpaidEligibilityCurrent(entry))return false;
            if(entry.UnpaidEligibilityHeld && entry.UnpaidEligibility==null && !PreparationEligibilityCurrent(entry))return false;
            if(entry.AdmissionReadiness!=null && entry.Prepared && !CheckAdmissionReadiness(entry)){next=entry.Decision;return true;}
            var original=entry.Request;var resumeProducer=entry.ProducerPreviewHeld;var inspectProducer=ClaimInspectionGuard(entry);
            if(original.SelectedJourney.Count!=0 || original.Requirements.Any(r=>r.SelectedJourney) || original.NativeStamp.RequestId!=0 ||
                refreshedRequest.SelectedJourney.Count!=0 || refreshedRequest.Requirements.Any(r=>r.SelectedJourney) ||
                refreshedRequest.ProvenFailure.HasValue || original.ProvenFailure.HasValue ||
                !refreshedRequest.NativeStamp.Equals(original.NativeStamp) || refreshedRequest.Start!=original.Start ||
                refreshedRequest.ResourceId!=original.ResourceId || refreshedRequest.Radius!=original.Radius ||
                refreshedRequest.SafetyMargin!=original.SafetyMargin || refreshedRequest.FiniteSeconds!=entry.FiniteLimit ||
                refreshedRequest.IndependentlyProgressing!=original.IndependentlyProgressing ||
                refreshedRequest.PlanPrimaryRoute!=original.PlanPrimaryRoute || !PreparationIdentityCurrent(entry))return false;
            if(!inspectProducer())return false;
            var nativeContextChanged=false;
            if(entry.UnpaidEligibilityHeld && entry.UnpaidEligibility!=null && getNativePathContext!=null && entry.NativeChunkContext!=null)
            {
                var storedContext=entry.NativeChunkContext;
                if(!ReadNativeChunkContext(entry,inspectProducer,out var currentContext))
                {
                    if(!inspectProducer())return false;
                    Fail(entry,TrafficDecisionKind.PlanningFailed,"native-query-context-read-failed");next=entry.Decision;return true;
                }
                if(!ReferenceEquals(storedContext,currentContext))
                {
                    if(paused)return false;
                    ClearNativeChunks(entry);entry.UnpaidEligibilitySpatialRefusal=null;entry.UnpaidEligibilitySpatialActors.Clear();
                    entry.UnpaidEligibilityNativeActivity=Array.Empty<UnpaidNativeActivity>();nativeContextChanged=true;
                }
            }
            if(resumeProducer && entry.UnpaidEligibility!=null)
            {
                if(expected.Kind!=TrafficDecisionKind.DeferredByBudget || entry.UnpaidEligibilityQueryFault ||
                    !ProducerHoldIdentityCurrent(entry,inspectProducer))return false;
                // A restored real producer preview requires fresh complete proof, before any motion coalescing.
                // 原 Entry/age/query/candidate 历史不变；不把 preview 恢复当成 Grant 或空间已安全。
                ObserveUnpaidPreparation(entry,"producer-preview-resumed",refreshedRequest);
                entry.Request=WithPrimaryFrame(refreshedRequest,entry.FirstFrame);
                entry.ProducerPreviewHeld=entry.UnpaidEligibilityHeld=false;
                entry.UnpaidEligibilityFacts=null;entry.UnpaidEligibilityRights=Array.Empty<UnpaidEligibilityRight>();
                entry.UnpaidEligibilitySpatialRefusal=null;entry.UnpaidEligibilitySpatialActors.Clear();
                entry.UnpaidEligibilityNativeActivity=Array.Empty<UnpaidNativeActivity>();
                entry.Jobs.Clear();entry.JobIndex=0;entry.CurrentPath=Array.Empty<Vector3>();entry.Prepared=false;entry.Queued=true;
                next=Decide(entry,TrafficDecisionKind.DeferredByBudget,"unpaid-producer-preview-ready-batch-pending");return true;
            }
            var nativeCheckpoint=false;
            if(!nativeContextChanged && TryCoalesceUnpaidNativeActivity(entry,refreshedRequest,out next,out nativeCheckpoint))return true;
            if(entry.UnpaidEligibilityHeld)
            {
                // Only actual external geometry/ownership/declared roles wake this original business proposal.
                // countdown、snapshot序号和无关Queue版本不续预算，也不触发路径查询。
                if(!nativeCheckpoint && !nativeContextChanged && SameUnpaidEligibilitySpace(entry,refreshedRequest)){entry.Request=WithPrimaryFrame(refreshedRequest,entry.FirstFrame);next=expected;return true;}
                if(paused)return false;
                ObserveUnpaidPreparation(entry,nativeCheckpoint?"held-native-checkpoint":"held-spatial-change",refreshedRequest);
                entry.Request=WithPrimaryFrame(refreshedRequest,entry.FirstFrame);entry.UnpaidEligibilityHeld=false;
                entry.UnpaidEligibilityFacts=null;entry.UnpaidEligibilityRights=Array.Empty<UnpaidEligibilityRight>();
                entry.UnpaidEligibilityAttempts.Clear();entry.UnpaidEligibilityAttempts.Add(entry.CurrentIndex);
                entry.Jobs.Clear();entry.JobIndex=0;entry.CurrentPath=Array.Empty<Vector3>();entry.Prepared=false;entry.Queued=true;
                next=Decide(entry,TrafficDecisionKind.DeferredByBudget,entry.UnpaidEligibility==null?"admission-eligibility/spatial-change":"unpaid-eligibility/spatial-change");return true;
            }
            if(resumeProducer && (expected.Kind!=TrafficDecisionKind.WaitingForTraffic || !Positive(entry.WaitRemaining) ||
                entry.FrozenOwners==null || entry.FrozenOwners.Count==0 || !ProducerHoldIdentityCurrent(entry,inspectProducer)))return false;
            var moved=original.Target!=refreshedRequest.Target;
            var sameRequirements=!moved && SameRequirements(original.Requirements,refreshedRequest.Requirements);
            // Walking changes require current full-proof validation, not automatic loss of an otherwise safe prepared source.
            // 仅刷新已完整复核的原许可；不产生新Grant、budget、age或释放通知。
            if(sameRequirements && entry.Prepared && !entry.Queued && expected.Kind==TrafficDecisionKind.Granted && HasPrimary(entry) &&
                original.Snapshot.Version==snapshot.Version && original.Snapshot.BodyVersion!=snapshot.BodyVersion)
            {
                var inspect=ClaimInspectionGuard(entry);var sameParking=SameCanonicalParkingFacts(original.Snapshot,snapshot,inspect);
                if(!inspect())return false;
                if(sameParking && PreparedParkingProofsStillCompatible(entry,refreshedRequest))
                {entry.Request=WithPrimaryFrame(refreshedRequest,entry.FirstFrame);next=expected;return true;}
            }
            var compatible=sameRequirements && (!HasPrimary(entry) || entry.Jobs.Count==0 ||
                PlanningJobsCompatible(entry,refreshedRequest));
            if(!compatible && sameRequirements && CanReusePreparedAnchorProofs(entry,refreshedRequest))compatible=true;
            var retainedJobs=!compatible && !nativeCheckpoint?RevalidateQueuedUnpaidJobs(entry,original,refreshedRequest):null;
            if(!inspectProducer())return false;
            var changed=moved || !compatible || original.Snapshot.Version!=snapshot.Version || original.Snapshot.BodyVersion!=snapshot.BodyVersion;
            entry.Request=WithPrimaryFrame(refreshedRequest,entry.FirstFrame);
            if(!compatible)
            {
                ObserveUnpaidPreparation(entry,retainedJobs!=null?"retained-current-safe-jobs":moved?"reset-target":!sameRequirements?"reset-roles":"reset-geometry",refreshedRequest,original);
                entry.Jobs.Clear();if(retainedJobs!=null)entry.Jobs.AddRange(retainedJobs);
                entry.JobIndex=0;entry.CurrentPath=Array.Empty<Vector3>();
            }
            if(resumeProducer)
            {
                entry.ProducerPreviewHeld=false;entry.Prepared=false;entry.Queued=true;
                next=Decide(entry,TrafficDecisionKind.DeferredByBudget,"canonical-anchor-ready-batch-pending");return true;
            }
            // Metadata cannot wake a fully covered bounded handoff; an already queued real Notify still wins.
            // 未移动照旧保留；目标移动时只保留全部原依赖仍由真实有限 handoff 覆盖的睡眠，不新增许可或重置预算。
            if(!entry.Queued && expected.Kind==TrafficDecisionKind.WaitingForTraffic &&
                (!moved || entry.WaitingBlockers.Count>0 && entry.WaitingBlockers.All(blocker=>
                    HoldsHandoffDependency(entry,blocker.Owner,blocker.ResourceId))))
            {next=expected;return true;}
            if(!changed){next=expected;return true;}
            entry.Prepared=false;entry.Queued=true;
            next=Decide(entry,TrafficDecisionKind.DeferredByBudget,"canonical-anchor-ready-batch-pending");return true;
        }
        // A changed future role does not invalidate every completed route of this same unacquired proposal.
        // 仅普通Queued付款前准备；Held/preview/checkpoint恢复仍走原完整证明，不续任何账本。
        private List<RouteJob> RevalidateQueuedUnpaidJobs(Entry entry,TrafficRequest original,TrafficRequest request)
        {
            if((entry.UnpaidEligibility==null && entry.AdmissionReadiness==null) || entry.UnpaidEligibilityHeld || entry.ProducerPreviewHeld || entry.UnpaidEligibilityQueryFault ||
                !entry.Queued || entry.Prepared || entry.Active || entry.Decision.Kind!=TrafficDecisionKind.DeferredByBudget ||
                entry.Decision.FailureDisposition!=TrafficFailureDisposition.None || entry.Jobs.Count==0)return null;
            var offset=HasPrimary(entry)?1:0;
            var oldIds=(offset==1?new[]{original.ResourceId}:Array.Empty<string>()).Concat(original.Requirements.Select(r=>r.ResourceId)).ToArray();
            var ids=(offset==1?new[]{request.ResourceId}:Array.Empty<string>()).Concat(request.Requirements.Select(r=>r.ResourceId)).ToArray();
            if(entry.Jobs.Count!=oldIds.Length || oldIds.Distinct(StringComparer.Ordinal).Count()!=oldIds.Length ||
                ids.Distinct(StringComparer.Ordinal).Count()!=ids.Length)return null;
            var rights=Rights(request).ToArray();var jobs=new List<RouteJob>();var retained=0;
            for(var index=0;index<ids.Length;index++)
            {
                var primary=index<offset;var role=primary?null:request.Requirements[index-offset];
                var start=primary?request.Start:role.Start;var goal=primary?request.Target:role.Goal;
                var obstacles=JobBaseObstacles(entry,request,index);var oldIndex=Array.IndexOf(oldIds,ids[index]);RouteJob kept=null;
                if(oldIndex>=0 && (oldIndex<offset)==primary)
                {
                    var oldRole=primary?null:original.Requirements[oldIndex-offset];
                    var same=primary?original.Start==start && original.Target==goal:
                        oldRole.Start==start && oldRole.Goal==goal && oldRole.Clearance==role.Clearance &&
                        oldRole.SelectedJourney==role.SelectedJourney && oldRole.AvoidProposedParking==role.AvoidProposedParking;
                    var old=entry.Jobs[oldIndex];
                    if(same && oldIndex<entry.JobIndex)
                    {
                        kept=old.RevalidateCompleted(start,goal,obstacles);
                        if(kept!=null)
                        {
                            var path=kept.CompletedGuardPath();
                            foreach(var other in rights)
                            {
                                var clearance=primary?PairClearance(request,other.Request):role.Clearance;
                                if(other.Parking?NearPath(other.Request.Target,path,clearance):PathsConflict(path,other.CurrentPath,clearance))
                                {kept=null;break;}
                            }
                        }
                    }
                    else if(same && old.HasUnfinishedBase && !old.HasPreferredBodies && old.HasSameObstacles(obstacles))kept=old;
                }
                if(kept!=null)retained++;
                jobs.Add(kept??new RouteJob(start,goal,obstacles));
            }
            return retained>0?jobs:null;
        }
        private static bool SameCanonicalParkingFacts(TrafficWorldSnapshot before,TrafficWorldSnapshot current,Func<bool> inspect)
        {
            if(before==null || current==null || before.Parking.Count!=current.Parking.Count)return false;
            for(var i=0;i<before.Parking.Count;i++)
            {
                if(!inspect() || !SameClaimFacts(before.Parking[i],current.Parking[i]))return false;
                var same=TrafficClaimCheckpoints.AreSame(before.Parking[i],current.Parking[i]);
                if(!inspect() || !same)return false;
            }
            return inspect();
        }
        // A changed prepared peer set need not discard routes which remain wholly safe now.
        // 只复用相同事实/descriptor的完整已证路线；未知或任一路线冲突仍走原完整重证。
        private bool CanReusePreparedAnchorProofs(Entry entry,TrafficRequest request)
        {
            if(!entry.Prepared || entry.Decision.Kind!=TrafficDecisionKind.Granted || !HasPrimary(entry) ||
                !SnapshotCurrent(entry.Request) || !SameProspectiveFacts(entry.Request.Snapshot,snapshot))return false;
            return PreparedParkingProofsStillCompatible(entry,request);
        }
        // Shared current-world geometry only; callers retain their distinct eligibility/authentication contracts.
        private bool PreparedParkingProofsStillCompatible(Entry entry,TrafficRequest request)
        {
            if(entry.Decision.ProofRoutes.Count!=request.Requirements.Count+1 || !IndependentPathClear(entry))return false;
            var blockers=EndpointBlockers(request);AddParkingEndpointRouteBlockers(request,blockers);
            if(blockers.Count!=0)return false;
            var rights=Rights(request).ToArray();var primaryObstacles=MovementObstacles(request);
            for(var i=0;i<entry.Decision.ProofRoutes.Count;i++)
            {
                var proof=entry.Decision.ProofRoutes[i];var requirement=i==0?null:request.Requirements[i-1];
                if(proof==null || proof.ExpandedPath.Count==0 ||
                    proof.ResourceId!=(requirement==null?request.ResourceId:requirement.ResourceId))return false;
                var path=GuardCurrentPath(requirement==null?request.Start:requirement.Start,proof.ExpandedPath,
                    requirement==null?request.Target:requirement.Goal);
                if(requirement==null)
                {
                    if(primaryObstacles.Any(o=>NearPath(o.Position,path,o.Clearance)))return false;
                }
                else if(requirement.Obstacles.Any(p=>NearPath(p,path,requirement.Clearance)) ||
                    requirement.AvoidProposedParking && NearPath(request.Target,path,requirement.Clearance))return false;
                foreach(var other in rights)
                {
                    var clearance=requirement==null?PairClearance(request,other.Request):requirement.Clearance;
                    if(other.Parking?NearPath(other.Request.Target,path,clearance):PathsConflict(path,other.CurrentPath,clearance))return false;
                }
            }
            return true;
        }
        private bool PreparationIdentityCurrent(Entry entry)
        {
            if(!LiveEntry(entry) || !entry.PreparationIdentityCaptured || entry.PreparationClaims==null)return false;
            var actor=entry.Request.Intent.ActorId;var bodies=snapshot.Bodies.Where(b=>b.Owner.ActorId==actor).ToArray();
            var claims=snapshot.Parking.Where(p=>p.Owner.ActorId==actor).ToArray();var original=entry.PreparationBody;
            if(original==null)return bodies.Length==0 && entry.PreparationClaims.Length==0 && claims.Length==0;
            if(bodies.Length!=1 || original.ActorLife==null || !ReferenceEquals(bodies[0].ActorLife,original.ActorLife) ||
                !bodies[0].Owner.Equals(original.Owner) || !bodies[0].Enabled || !bodies[0].ActorActive ||
                bodies[0].Position!=original.Position || bodies[0].Position!=entry.Request.Start ||
                bodies[0].Radius!=original.Radius || bodies[0].SafetyMargin!=original.SafetyMargin ||
                claims.Length!=entry.PreparationClaims.Length)return false;
            return entry.PreparationClaims.All(old=>claims.Count(current=>SameOwnedClaim(old,current) &&
                old.Position==current.Position && old.Radius==current.Radius && old.SafetyMargin==current.SafetyMargin)==1);
        }
        // Only the exact failed uncommitted candidate may consume another original index once.
        // 原侧列表由 producer 固定；这里只验证原数量、已尝试集合及当前失败权限。
        public bool TryAdvanceCandidate(TrafficDecision expected, int nextIndex, TrafficRequest refreshedRequest, out TrafficDecision next)
        {
            next=null;
            if(closed || paused || committing || expected==null || refreshedRequest==null ||
                !Current(expected.Intent) || !refreshedRequest.Intent.Equals(expected.Intent) ||
                !entries.TryGetValue(expected.Intent,out var entry) || !LiveEntry(entry) || !ReferenceEquals(entry.Decision,expected) ||
                expected.FailureDisposition!=TrafficFailureDisposition.AlternateAllowed || !CanAlternate(entry) ||
                nextIndex<0 || nextIndex>=entry.CandidateCount || CandidateAttempts(entry).Contains(nextIndex) ||
                !Valid(refreshedRequest) || !SnapshotCurrent(refreshedRequest))return false;
            var original=entry.Request;
            if(refreshedRequest.ResourceId!=original.ResourceId || refreshedRequest.Radius!=original.Radius ||
                refreshedRequest.SafetyMargin!=original.SafetyMargin || refreshedRequest.FiniteSeconds!=entry.FiniteLimit ||
                refreshedRequest.IndependentlyProgressing!=original.IndependentlyProgressing ||
                refreshedRequest.PlanPrimaryRoute!=original.PlanPrimaryRoute || refreshedRequest.ProvenFailure.HasValue ||
                !refreshedRequest.NativeStamp.Equals(original.NativeStamp))return false;
            entry.DeliveryAlternateDebtGap=false;
            entry.CurrentIndex=nextIndex;entry.AttemptedIndices.Add(nextIndex);if(entry.UnpaidEligibility!=null)entry.UnpaidEligibilityAttempts.Add(nextIndex);
            entry.Request=WithPrimaryFrame(refreshedRequest,entry.FirstFrame);entry.Discarded=false;entry.Queued=true;
            entry.Jobs.Clear();entry.JobIndex=0;entry.CurrentPath=Array.Empty<Vector3>();
            next=Decide(entry,TrafficDecisionKind.DeferredByBudget,"alternate-candidate-ready-batch-pending");return true;
        }
        private static bool CanAlternate(Entry entry) => entry!=null && !entry.Retired && entry.Request!=null && !entry.Discarded && !entry.Active && !entry.Prepared &&
            !entry.Queued && entry.Leg==null && entry.PendingHandoff==null && entry.JourneyPermit==null && entry.JourneyRequest==null &&
            entry.AcquiredSequence==0 && !entry.EverCanonicalAttempt && entry.Request.NativeStamp.RequestId==0 &&
            CandidateAttempts(entry).Count<entry.CandidateCount;
        private static HashSet<int> CandidateAttempts(Entry entry)=>entry.UnpaidEligibility==null?entry.AttemptedIndices:entry.UnpaidEligibilityAttempts;
        private TrafficDecision Submit(TrafficRequest request, bool parking, int candidateIndex, int candidateCount)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (closed) return new TrafficDecision(request.Intent, TrafficDecisionKind.Cancelled, "context-closed",failureDisposition:TrafficFailureDisposition.Terminal);
            if (!Current(request.Intent)) return new TrafficDecision(request.Intent, TrafficDecisionKind.StaleContext, "context-changed",failureDisposition:TrafficFailureDisposition.Terminal);
            if (committing) throw new InvalidOperationException("Submit inside joint commit.");
            if (!entries.TryGetValue(request.Intent, out var entry))
            {
                if(candidateCount<1 || candidateIndex<0 || candidateIndex>=candidateCount)
                    return new TrafficDecision(request.Intent,TrafficDecisionKind.InvalidConfiguration,"invalid-candidate-range",failureDisposition:TrafficFailureDisposition.Terminal);
                entry = new Entry(request.Intent,++entryBirthSequence) { FirstFrame = request.RequestedFrame, Request = request, Parking = parking,
                    CandidateCount=candidateCount,CurrentIndex=candidateIndex,FiniteLimit=request.FiniteSeconds };
                // Capture once for either preparation kind; only the explicit typed refresh port may consume it.
                // 原始 body/claims 仅首次提交捕获；不自动采纳后续 snapshot，也不改变 Parking/Movement 类型。
                if(request.Snapshot!=null)
                {
                    var bodies=request.Snapshot.Bodies.Where(b=>b.Owner.ActorId==request.Intent.ActorId).ToArray();
                    entry.PreparationIdentityCaptured=bodies.Length<=1;entry.PreparationBody=bodies.FirstOrDefault();
                    entry.PreparationClaims=request.Snapshot.Parking.Where(p=>p.Owner.ActorId==request.Intent.ActorId).ToArray();
                }
                entry.AttemptedIndices.Add(candidateIndex);
                entries.Add(request.Intent, entry);
            }
            else
            {
                // Check authority before any mutable metadata or retained proof is cleared.
                // genuine terminal 与已提交来源不能通过 Submit/新 snapshot 复活或被擦除。
                if (entry.Retired || entry.ProducerPreviewHeld || entry.UnpaidEligibilityHeld || entry.PendingHandoff!=null || entry.Decision.FailureDisposition!=TrafficFailureDisposition.None)
                    return entry.Decision;
                if(candidateCount!=entry.CandidateCount || candidateIndex!=entry.CurrentIndex ||
                    request.FiniteSeconds!=entry.FiniteLimit || entry.Parking!=parking)return entry.Decision;
                // An explicit recovery carries historical acquisition age without a new committed source yet.
                // 继承的 AcquiredSequence 是历史优先级；真正的当前 source/strict leg 才禁止 metadata 刷新。
                if(entry.Leg!=null || entry.JourneyPermit!=null && !entry.LegacyLegCompleted)
                    return entry.Decision;
                // Legacy completed legs and discarded standalone proposals retain their existing narrow contracts.
                // 有限原侧不可被同 index 的另一 target 偷换；已完成旧式分腿仍保留原累计预算。
                if(!entry.LegacyLegCompleted && (!entry.Discarded || entry.CandidateCount>1 || entry.ExplicitRecoveryUsed) &&
                    (request.Target!=entry.Request.Target || request.ResourceId!=entry.Request.ResourceId))return entry.Decision;
                request=WithPrimaryFrame(request,entry.FirstFrame);
                if(!entry.Parking && !entry.Queued && entry.Decision.Kind==TrafficDecisionKind.WaitingForTraffic)
                {
                    var inspect=ClaimInspectionGuard(entry);var sleeping=SleepingMovementMetadataPollCurrent(entry,request,inspect);
                    if(!inspect())return new TrafficDecision(request.Intent,TrafficDecisionKind.StaleContext,"sleeping-poll-inspection-source-changed",
                        failureDisposition:TrafficFailureDisposition.Terminal);
                    if(sleeping)return entry.Decision;
                }
                // Polling the same snapshot cannot restart a cursor or frozen wait.
                if (!entry.Discarded && SameRequest(entry.Request, request) && entry.Parking == parking)
                {
                    var changedBodies=entry.Request.Snapshot.BodyVersion!=request.Snapshot.BodyVersion;
                    entry.Request=request;
                    // Refresh moving bodies without discarding the bounded Native frontier.
                    // 等待只由相关事件唤醒；prepared许可仍须复核当前真实身体。
                    if(changedBodies && (entry.Active || entry.Prepared))
                    {
                        entry.Active=false;entry.Prepared=false;
                        if(CanPreferUnacquired(entry,request) && HasStartedSelectedDetour(entry,request) && !PlanningJobsCompatible(entry,request))
                        {entry.Jobs.Clear();entry.JobIndex=0;entry.CurrentPath=entry.Journey=Array.Empty<Vector3>();}
                        entry.Queued=true;return Decide(entry,TrafficDecisionKind.DeferredByBudget,"current-body-revalidation");
                    }
                    return entry.Decision;
                }
                // Revoking an uncommitted computed parking proof also retires its cached selected-home geometry.
                // 仅清无真实owned authority的计算cache；legacy余程与caller静态声明保持原合同。
                if(entry.Parking && entry.AcquiredSequence==0 && entry.Leg==null && entry.JourneyPermit==null && entry.JourneyRequest==null &&
                    !entry.LegacyLegCompleted && !entry.ExplicitRecoveryUsed && !entry.EverCanonicalAttempt &&
                    entry.Request.NativeStamp.Equals(default(TrafficNativeStamp)) && entry.Request.SelectedJourney.Count==0)
                    entry.Journey=Array.Empty<Vector3>();
                entry.Prepared = false; entry.Active = false; entry.CurrentPath = Array.Empty<Vector3>();
                entry.Jobs.Clear(); entry.JobIndex = 0; entry.Request = request; entry.Parking = parking;
                entry.Leg = null; entry.JourneyPermit = null; entry.JourneyRequest = null;
            }
            entry.Discarded = false; entry.LegacyLegCompleted=false; entry.Queued = true;
            return Decide(entry, TrafficDecisionKind.DeferredByBudget, "ready-batch-pending");
        }
        // A sleeping unissued movement may authenticate metadata without acquiring a new dependency or permission.
        private bool SleepingMovementMetadataPollCurrent(Entry entry,TrafficRequest request,Func<bool> inspect)
        {
            var original=entry.Request;var body=entry.PreparationBody;
            if(!LiveEntry(entry) || entry.Queued || entry.Prepared || entry.Active || entry.Parking || entry.Discarded ||
                entry.AcquiredSequence!=0 || entry.Leg!=null || entry.JourneyPermit!=null || entry.JourneyRequest!=null || entry.Journey.Length!=0 ||
                entry.PendingHandoff!=null || entry.EverCanonicalAttempt || entry.ExplicitRecoveryUsed || entry.LegacyLegCompleted ||
                entry.FrozenOwners==null || entry.FrozenResources==null || entry.WaitingBlockers.Count==0 || !Positive(entry.WaitRemaining) ||
                !Valid(original) || !Valid(request) || !ReferenceEquals(request.Snapshot,snapshot) || !ValidSnapshot() ||
                original.Snapshot.Version==snapshot.Version || !SameProspectiveFacts(original.Snapshot,snapshot) || !original.NativeStamp.Equals(default(TrafficNativeStamp)) ||
                !request.NativeStamp.Equals(original.NativeStamp) || original.SelectedJourney.Count!=0 || request.SelectedJourney.Count!=0 ||
                original.ProvenFailure.HasValue || request.ProvenFailure.HasValue || original.Start!=request.Start || original.Target!=request.Target ||
                original.ResourceId!=request.ResourceId || original.Radius!=request.Radius || original.SafetyMargin!=request.SafetyMargin ||
                original.FiniteSeconds!=request.FiniteSeconds || original.IndependentlyProgressing!=request.IndependentlyProgressing ||
                original.PlanPrimaryRoute!=request.PlanPrimaryRoute || !SameRequirements(original.Requirements,request.Requirements) ||
                body==null || !body.Enabled || !body.ActorActive || !Current(body.Owner) || !Finite(body.RemainingSeconds) || body.RemainingSeconds<0 ||
                !PreparationIdentityCurrent(entry))return false;
            var current=snapshot.Bodies.Single(b=>b.Owner.ActorId==request.Intent.ActorId);
            if(current.IndependentlyProgressing!=body.IndependentlyProgressing || current.RemainingSeconds!=body.RemainingSeconds)return false;
            return inspect() && QueuedPreparationClaimsCurrent(entry,inspect) && inspect();
        }

        // Primary preparation keeps phase birth; PrepareLeg still schedules at the actual current frame.
        // 候选与 metadata 不重置 primary 年龄；严格发腿继续使用自己的当前 scheduling frame。
        private static TrafficRequest WithPrimaryFrame(TrafficRequest request,int frame) => request.RequestedFrame==frame?request:
            new TrafficRequest(request.Intent,frame,request.Snapshot,request.Start,request.Target,request.Radius,request.ResourceId,
                request.SelectedJourney,request.Requirements,request.NativeStamp,request.IndependentlyProgressing,
                request.FiniteSeconds,request.ProvenFailure,request.SafetyMargin,request.PlanPrimaryRoute);
        public void ProcessReady(int frame)
        {
#if UNITY_EDITOR
            var observer=NavigationCostObservation.ProcessReadyElapsed;
            if(observer!=null)
            {
                var start=System.Diagnostics.Stopwatch.GetTimestamp();
                try{ProcessReadyCore(frame);}
                finally
                {
                    var elapsed=System.Diagnostics.Stopwatch.GetTimestamp()-start;
                    try{observer(frame,elapsed);}catch(Exception){NavigationCostObservation.ObserverErrors++;}
                }
                return;
            }
#endif
            ProcessReadyCore(frame);
        }
        // Refresh only an already authenticated, unacquired canonical successor before ordinary batching.
        // 只更新原bound preparation的snapshot；原milestone、预算、年龄与strict提交权限不变。
        private bool RefreshBoundQueuedSuccessorSnapshots(int frame)
        {
            foreach(var previous in OrderedWorkEntries.Where(e=>e.PendingHandoff?.PreparedSuccessor!=null).ToArray())
            {
                var state=previous.PendingHandoff;var next=state?.PreparedSuccessor;
                if(state==null || state.InspectingPreparation || !InitialBoundPreparationQueued(next) || next.Request.RequestedFrame>frame || SnapshotCurrent(next.Request))continue;
                var world=snapshot;var original=next.Request;var decision=next.Decision;
                var descriptor=state.PreparationRequest;var source=state.PreparationSource;var milestone=state.PreparationMilestone;
                var remaining=state.Remaining;var inspectPrevious=ClaimInspectionGuard(previous);var inspectNext=ClaimInspectionGuard(next);
                bool BoundCurrent()=>inspectPrevious() && ReferenceEquals(previous.PendingHandoff,state) &&
                    ReferenceEquals(state.PreparedSuccessor,next) && ReferenceEquals(state.PreparationRequest,descriptor) &&
                    ReferenceEquals(state.PreparationSource,source) && ReferenceEquals(state.PreparationMilestone,milestone) &&
                    state.Remaining==remaining && Positive(remaining);
                var authenticated=ReadBoundSuccessor(previous,state,out _);
                if(!BoundCurrent() || !inspectNext())return false;
                if(!authenticated)continue;
                var refreshed=new TrafficRequest(original.Intent,original.RequestedFrame,world,original.Start,original.Target,
                    original.Radius,original.ResourceId,original.SelectedJourney,original.Requirements,original.NativeStamp,
                    original.IndependentlyProgressing,original.FiniteSeconds,original.ProvenFailure,original.SafetyMargin,original.PlanPrimaryRoute);
                state.InspectingPreparation=true;
                try
                {
                    var accepted=TryRefreshQueuedPreparation(decision,refreshed,out var current);
                    // The port may replace Request/Decision; the original binding and live world must not change with it.
                    // 容许port正常替换request对象，但不容许回调换掉原source、handoff或剩余预算。
                    if(!BoundCurrent() || !InitialBoundPreparationQueued(next) || !ReferenceEquals(snapshot,world))return false;
                    if(!accepted){if(!inspectNext())return false;continue;}
                    if(!ReferenceEquals(next.Decision,current) || !ReferenceEquals(next.Request.Snapshot,world) ||
                        !SameSuccessorDescriptor(original,next.Request))return false;
                }
                finally{state.InspectingPreparation=false;}
            }
            return true;
        }
        private bool InitialBoundPreparationQueued(Entry next)=>next!=null && LiveEntry(next) && Current(next.Key) && next.Decision!=null &&
            next.Decision.FailureDisposition==TrafficFailureDisposition.None &&
            (next.Decision.Kind==TrafficDecisionKind.DeferredByBudget || next.Decision.Kind==TrafficDecisionKind.WaitingForTraffic) &&
            next.Queued && !next.Prepared && !next.Active && !next.Parking && !next.Discarded && next.AcquiredSequence==0 &&
            next.Leg==null && next.JourneyPermit==null && next.JourneyRequest==null && next.Journey.Length==0 &&
            next.PendingHandoff==null && !next.EverCanonicalAttempt && !next.ExplicitRecoveryUsed && !next.LegacyLegCompleted &&
            next.Request.SelectedJourney.Count==0 && next.Request.Requirements.Count==0;
        // Original synchronous body, including early returns/exceptions; no extra evaluation.
        // 原同步过程只执行一次；elapsed包含其原查询/回调，不冒充纯CPU。
        private void ProcessReadyCore(int frame)
        {
#if UNITY_EDITOR
            if(CountProcessReadySourceVisits)ProcessReadySourceVisits=0;
#endif
            if (closed || paused || committing) return;
            if (frame < queryFrame) throw new ArgumentOutOfRangeException(nameof(frame));
            if (frame != queryFrame) { queryFrame = frame; QueriesThisFrame = ProofStepsThisFrame = 0; }
            if(!RefreshBoundQueuedSuccessorSnapshots(frame))return;
            IEnumerable<Entry> source=workEntries;
#if UNITY_EDITOR
            if(CountProcessReadySourceVisits)
                source=source.Select(e=>{ProcessReadySourceVisits++;return e;});
#endif
            foreach(var held in OrderedWorkEntries.Where(e=>e.AdmissionHeld).ToArray())
            {
                if(!CheckAdmissionReadiness(held))continue;
                held.AdmissionHeld=false;QueueChangedWaiter(held);
            }
            var batch = source.Where(e => e.Queued && !e.ProducerPreviewHeld && !e.AdmissionHeld && !e.Retired && e.Request!=null && e.Request.RequestedFrame <= frame)
                .OrderBy(e => e.FirstFrame).ThenBy(e => e.Key.ActorId, StringComparer.Ordinal)
                .ThenBy(e => e.Key.IntentId, StringComparer.Ordinal).ThenBy(e => e.Key.Generation).ThenBy(e => e.BirthOrder).ToArray();
            foreach (var entry in batch)
            {
                if(!LiveEntry(entry) || !entry.Queued || entry.ProducerPreviewHeld)continue;
                if (entry.RequestSequence == 0) entry.RequestSequence = ++requestSequence;
                Evaluate(entry);
            }
        }
        // Bind only before the first ordinary ready batch. Existing or spent authority cannot opt in later.
        internal bool TrySetUnpaidParkingEligibility(TrafficDecision expected,TrafficUnpaidParkingEligibility descriptor)
        {
            if(closed || paused || committing || expected==null || descriptor==null || !Current(expected.Intent) ||
                !descriptor.Intent.Equals(expected.Intent) || !entries.TryGetValue(expected.Intent,out var entry) ||
                !LiveEntry(entry) || !Valid(entry.Request) || !SnapshotCurrent(entry.Request) || !ValidSnapshot() ||
                !ReferenceEquals(entry.Decision,expected) || expected.Kind!=TrafficDecisionKind.DeferredByBudget ||
                expected.FailureDisposition!=TrafficFailureDisposition.None || entry.RequestSequence!=0 || entry.Queries!=0 ||
                entry.UnpaidEligibility!=null && !ReferenceEquals(entry.UnpaidEligibility,descriptor) ||
                entry.AdmissionReadiness!=null || entry.CounterAdvanceReadiness!=null)return false;
            var previous=entry.UnpaidEligibility;entry.UnpaidEligibility=descriptor;
            if(!UnpaidEligibilityCurrent(entry)){entry.UnpaidEligibility=previous;return false;}
            entry.UnpaidEligibilityAttempts.Add(entry.CurrentIndex);return true;
        }
        internal bool IsUnpaidParkingEligibilityHeld(TrafficDecision expected,TrafficUnpaidParkingEligibility descriptor)
            =>expected!=null && descriptor!=null && entries.TryGetValue(expected.Intent,out var entry) && LiveEntry(entry) &&
                ReferenceEquals(entry.Decision,expected) && ReferenceEquals(entry.UnpaidEligibility,descriptor) && entry.UnpaidEligibilityHeld &&
                expected.Kind==TrafficDecisionKind.DeferredByBudget && expected.FailureDisposition==TrafficFailureDisposition.None &&
                expected.AcquiredSequence==0 && !entry.Active && !entry.Prepared && !entry.Queued;
        private bool UnpaidEligibilityCurrent(Entry entry)
        {
            var typed=entry?.UnpaidEligibility;var request=entry?.Request;
            if(typed==null || !LiveEntry(entry) || !Current(entry.Key) || !typed.Intent.Equals(entry.Key) ||
                entry.Key.IntentId!="pickup-wait" || request.ResourceId!="pickup/"+entry.Key.ActorId || !entry.Parking || entry.Active || entry.Discarded ||
                entry.Decision==null || entry.Decision.FailureDisposition!=TrafficFailureDisposition.None || entry.AcquiredSequence!=0 ||
                entry.EverCanonicalAttempt || entry.ExplicitRecoveryUsed || entry.LegacyLegCompleted || entry.ContinuationAttempted ||
                entry.Leg!=null || entry.JourneyPermit!=null || entry.JourneyRequest!=null || entry.PendingHandoff!=null || entry.FrozenOwners!=null ||
                !request.PlanPrimaryRoute || !request.NativeStamp.Equals(default(TrafficNativeStamp)) || request.ProvenFailure.HasValue ||
                request.SelectedJourney.Count!=0 || request.Requirements.Any(role=>role.SelectedJourney) ||
                !PreparationIdentityCurrent(entry) || typed.ActorLife==null || typed.CounterOwner==null || typed.BusinessOwner==null || typed.Admission==null)return false;
            var bodies=snapshot.Bodies.Where(body=>body.Owner.ActorId==entry.Key.ActorId).ToArray();
            var claims=snapshot.Parking.Where(claim=>claim.Owner.ActorId==entry.Key.ActorId).ToArray();
            if(bodies.Length!=1 || !ReferenceEquals(bodies[0].ActorLife,typed.ActorLife) || bodies[0].Position!=request.Start ||
                !bodies[0].Enabled || !bodies[0].ActorActive || claims.Length!=1 ||
                claims[0].ResourceId!="counter/"+entry.Key.ActorId || !ReferenceEquals(claims[0].CanonicalOwner,typed.CounterOwner) ||
                !ReferenceEquals(claims[0].BusinessOwner,typed.BusinessOwner) || !ReferenceEquals(claims[0].ReleaseObligation,typed.Admission) ||
                !(typed.CounterOwner is AnimalCafe.Customers.CounterQueueService counter) ||
                !(claims[0].SemanticCheckpoint is AnimalCafe.Customers.CounterQueueService.AssignmentClaim claim) ||
                !ReferenceEquals(claim.CanonicalOwner,counter) || !claim.IsCurrent || !claim.Assignment.Arrived ||
                claim.Assignment.SlotIndex!=0 || claim.Assignment.VisitId!=entry.Key.ActorId)return false;
            var inspect=ClaimInspectionGuard(entry);var rows=counter.Snapshot;
            if(rows.Count==0 || !rows[0].Equals(claim.Assignment) || !rows[0].Arrived || rows[0].SlotIndex!=0 ||
                entry.PreparationClaims.Length!=1 || !TrafficClaimCheckpoints.AreSame(entry.PreparationClaims[0],claims[0]) || !inspect())return false;
            bool current;
            try{current=typed.IsCurrent();}catch{current=false;}
            return current && inspect() && ReferenceEquals(entry.UnpaidEligibility,typed);
        }
        private bool SameUnpaidEligibilitySpace(Entry entry,TrafficRequest current,ISet<string> movingActors=null)
        {
            var before=entry.UnpaidEligibilityFacts;
            if(before==null || before.Start!=current.Start || before.Target!=current.Target ||
                !SameRequirements(before.Requirements,current.Requirements))return false;
            var old=before.Snapshot;var now=current.Snapshot;
            if(old.Bodies.Count!=now.Bodies.Count || old.Parking.Count!=now.Parking.Count)return false;
            bool SameBody(TrafficBodyFact a,TrafficBodyFact b)=>a.Owner.Equals(b.Owner) && ReferenceEquals(a.ActorLife,b.ActorLife) &&
                (a.Position==b.Position || movingActors!=null && movingActors.Contains(a.Owner.ActorId)) &&
                a.Radius==b.Radius && a.SafetyMargin==b.SafetyMargin && a.Enabled==b.Enabled && a.ActorActive==b.ActorActive;
            bool SameParking(TrafficParkingFact a,TrafficParkingFact b)=>a.Owner.Equals(b.Owner) && a.ResourceId==b.ResourceId &&
                ReferenceEquals(a.CanonicalOwner,b.CanonicalOwner) && ReferenceEquals(a.BusinessOwner,b.BusinessOwner) &&
                ReferenceEquals(a.ReleaseObligation,b.ReleaseObligation) && a.Position==b.Position && a.Radius==b.Radius && a.SafetyMargin==b.SafetyMargin;
            if(old.Bodies.Any(a=>now.Bodies.Count(b=>SameBody(a,b))!=1) || old.Parking.Any(a=>now.Parking.Count(b=>SameParking(a,b))!=1))return false;
            var rights=Rights(current).ToArray();
            return rights.Length==entry.UnpaidEligibilityRights.Length && entry.UnpaidEligibilityRights.All(oldRight=>rights.Count(oldRight.Same)==1);
        }
        // Both typed business opportunities are still unacquired: an arrived unpaid head, or a genuinely unborn
        // admission. Neither can borrow finite WAIT/Handoff authority from an already real moving actor.
        // 复用同一资格等待账本；未出生来源必须有原producer认证，已有真实WAIT债务仍按原期限执行。
        private bool PreparationEligibilityCurrent(Entry entry)
        {
            if(entry?.UnpaidEligibility!=null)return UnpaidEligibilityCurrent(entry);
            var readiness=entry?.AdmissionReadiness;
            if(readiness==null || !readiness.Intent.Equals(entry.Key) || !UnbornAdmissionCurrent(entry))return false;
            var inspect=ClaimInspectionGuard(entry);bool current;
            try{current=readiness.IsCurrent();}catch{return false;}
            return current && inspect() && ReferenceEquals(entry.AdmissionReadiness,readiness);
        }
        private bool HoldUnpaidEligibility(Entry entry,string reason,IEnumerable<TrafficBlocker> blockers=null,string failedRole=null)
        {
            if(!PreparationEligibilityCurrent(entry))return false;
            if(entry.UnpaidEligibility==null)
            {
                if(entry.FrozenOwners!=null || entry.AdmissionWaitStarted || entry.CounterAdvanceReadinessWaitStarted || entry.YieldOwnerWaitStarted)return false;
                reason=reason.Replace("unpaid-eligibility/","admission-eligibility/");
            }
            var actualBlockers=(blockers??Array.Empty<TrafficBlocker>()).ToArray();
            CaptureUnpaidSpatialActors(entry,actualBlockers,failedRole);
            if(entry.UnpaidEligibility!=null)entry.NativeChunkReadAllowed=true;
            entry.UnpaidEligibilityNativeActivity=Array.Empty<UnpaidNativeActivity>();
            entry.UnpaidEligibilityHeld=true;entry.UnpaidEligibilityFacts=entry.Request;
            entry.UnpaidEligibilityRights=Rights(entry.Request).Select(right=>new UnpaidEligibilityRight(right)).ToArray();
            ObserveUnpaidPreparation(entry,"actual-spatial-refusal",entry.Request);
            entry.Queued=entry.Prepared=entry.Active=false;entry.CurrentPath=entry.Journey=Array.Empty<Vector3>();
            entry.Jobs.Clear();entry.JobIndex=0;
            entry.UnpaidEligibilitySpatialRefusal=Decide(entry,TrafficDecisionKind.DeferredByBudget,reason,actualBlockers);return true;
        }
        private void CaptureUnpaidSpatialActors(Entry entry,TrafficBlocker[] blockers,string failedRole)
        {
            var actors=entry.UnpaidEligibilitySpatialActors;actors.Clear();
            foreach(var blocker in blockers)if(blocker.Owner.ActorId!=entry.Key.ActorId)actors.Add(blocker.Owner.ActorId);
            if(failedRole==null)return;
            var request=entry.Request;var world=request.Snapshot;
            foreach(var id in world.Bodies.Select(body=>body.Owner.ActorId).Concat(world.Parking.Select(claim=>claim.Owner.ActorId)).Distinct())
                if(id!=entry.Key.ActorId && (failedRole=="existing-goal/"+id || failedRole=="existing-goal/"+id+"/exit-capability"))actors.Add(id);
            // All coincident original body identities remain candidates; no owner is erased by coordinate projection.
            foreach(var role in request.Requirements.Where(role=>role.ResourceId==failedRole))
                foreach(var body in world.Bodies.Where(body=>body.Owner.ActorId!=entry.Key.ActorId && role.Obstacles.Any(point=>point.Equals(body.Position))))
                    actors.Add(body.Owner.ActorId);
        }
        private bool TryReadUnpaidNativeActivity(string actorId,out UnpaidNativeActivity activity)
        {
            activity=null;var bodies=snapshot.Bodies.Where(body=>body.Owner.ActorId==actorId).ToArray();
            if(bodies.Length!=1)return false;var body=bodies[0];
            if(!body.Enabled || !body.ActorActive || body.ActorLife==null || !Current(body.Owner) ||
                !entries.TryGetValue(body.Owner,out var moving) || !LiveEntry(moving) || !moving.Active || moving.Parking ||
                moving.Decision?.Kind!=TrafficDecisionKind.Granted || moving.Decision.FailureDisposition!=TrafficFailureDisposition.None ||
                moving.PendingHandoff!=null || moving.JourneyPermit==null || moving.JourneyRequest==null || moving.AcquiredSequence<=0 ||
                !moving.JourneyPermit.Intent.Equals(moving.Key) || !Positive(moving.FiniteLimit) || !Positive(moving.Request.FiniteSeconds))return false;
            var leg=moving.Leg;
            if(leg==null || !leg.Committed || !leg.Bound || leg.Completed || leg.Suspended || leg.ReprepareAllowed ||
                leg.Generation<1 || leg.Stamp.RequestId<=0 || leg.Stamp.LegGeneration!=leg.Generation ||
                moving.Request.NativeStamp.LegGeneration!=leg.Generation || !ReferenceEquals(leg.ActorLife,body.ActorLife) ||
                body.Radius!=moving.Request.Radius || body.SafetyMargin!=moving.Request.SafetyMargin ||
                leg.Proof==null || !SelectedProofs(moving).Any(proof=>ReferenceEquals(proof,leg.Proof)) ||
                leg.Index<0 || leg.Index>=leg.Proof.Waypoints.Count || moving.Request.ResourceId!=leg.Proof.ResourceId ||
                moving.Request.Target!=leg.Proof.Waypoints[leg.Index] || leg.Path==null || leg.Path.Length<2 ||
                !LegBodyCurrent(moving,body.ActorLife) || !NearPath(body.Position,leg.Path,body.Radius+leg.ArrivalTolerance))return false;
            // JourneyParking may be true: a real pickup-service family becomes movement at strict PrepareLeg.
            // 只拒无真实Bound腿的parking grant；不排除真实PQS family的原停车许可来源。
            activity=new UnpaidNativeActivity(moving);return true;
        }
        private bool TryCoalesceUnpaidNativeActivity(Entry entry,TrafficRequest refreshed,out TrafficDecision next,out bool checkpoint)
        {
            next=null;checkpoint=false;
            if(paused || (entry.UnpaidEligibility==null && entry.AdmissionReadiness==null) || entry.UnpaidEligibilitySpatialRefusal==null || entry.Prepared ||
                entry.UnpaidEligibilityQueryFault)return false;
            var prior=entry.UnpaidEligibilityNativeActivity;
            var observed=new List<UnpaidNativeActivity>();
            foreach(var actor in entry.UnpaidEligibilitySpatialActors.Concat(prior.Select(activity=>activity.Owner.Key.ActorId)).Distinct())
                if(TryReadUnpaidNativeActivity(actor,out var activity))observed.Add(activity);
            if(entry.UnpaidEligibilityHeld && prior.Length==0 && entry.UnpaidEligibilityFacts!=null)
            {
                // A body outside the failed role can still wake the full-space comparison. Discover only its
                // actual changed, authenticated Native leg; this is scheduling, never new movement permission.
                // 非拒绝角色的身体位移也会唤醒重算；只识别真实Bound腿，不授予任何新权利。
                var before=entry.UnpaidEligibilityFacts.Snapshot;
                foreach(var body in snapshot.Bodies)
                {
                    var previous=before.Bodies.Where(old=>old.Owner.Equals(body.Owner)).ToArray();
                    if(previous.Length!=1 || !ReferenceEquals(previous[0].ActorLife,body.ActorLife) ||
                        previous[0].Position==body.Position || previous[0].Radius!=body.Radius || previous[0].SafetyMargin!=body.SafetyMargin ||
                        observed.Any(activity=>activity.Owner.Key.ActorId==body.Owner.ActorId))continue;
                    if(TryReadUnpaidNativeActivity(body.Owner.ActorId,out var activity))
                        observed.Add(new UnpaidNativeActivity(activity.Owner,requiresStableSpace:true));
                }
            }
            if(prior.Length!=0 && (prior.Length!=observed.Count || prior.Any(old=>observed.Count(old.Same)!=1)))
            {
                ObserveUnpaidPreparation(entry,"native-checkpoint",refreshed);
                entry.UnpaidEligibilityNativeActivity=Array.Empty<UnpaidNativeActivity>();
                if(prior.Any(activity=>activity.RequiresStableSpace))
                {entry.UnpaidEligibilitySpatialRefusal=null;entry.UnpaidEligibilitySpatialActors.Clear();}
                checkpoint=true;return false; // The new full proof cannot borrow this old refusal again.
            }
            var stableSpace=prior.Any(activity=>activity.RequiresStableSpace) || observed.Any(activity=>activity.RequiresStableSpace);
            if(stableSpace && !SameUnpaidEligibilitySpace(entry,refreshed,new HashSet<string>(observed.Select(activity=>activity.Owner.Key.ActorId))))
            {
                // Only position samples may be coalesced. Any other world/role/claim change needs fresh proof.
                // 静态身体、站位、角色或所有权变化不能被运动等待吞掉，也不能重新采用旧拒绝因。
                ObserveUnpaidPreparation(entry,"motion-space-changed",refreshed);
                entry.UnpaidEligibilityNativeActivity=Array.Empty<UnpaidNativeActivity>();
                entry.UnpaidEligibilitySpatialRefusal=null;entry.UnpaidEligibilitySpatialActors.Clear();return false;
            }
            if(observed.Count==0){ObserveUnpaidPreparation(entry,"no-bound-native",refreshed);return false;}
            entry.Request=WithPrimaryFrame(refreshed,entry.FirstFrame);
            if(prior.Length==0)
            {
                ObserveUnpaidPreparation(entry,"native-coalesced",refreshed);
                entry.UnpaidEligibilityNativeActivity=observed.ToArray();
                entry.UnpaidEligibilityHeld=true;entry.Queued=entry.Prepared=entry.Active=false;
                entry.Jobs.Clear();entry.JobIndex=0;entry.CurrentPath=Array.Empty<Vector3>();
                var refusal=entry.UnpaidEligibilitySpatialRefusal;
                next=Decide(entry,TrafficDecisionKind.DeferredByBudget,refusal.Reason,refusal.Blockers);return true;
            }
            // Position samples of the same actual stamped leg change metadata only, never query/age/attempt/WAIT budgets.
            // checkpoint后仍由最新完整world proof决定Grant；当前观察本身不授予任何权利。
            next=entry.Decision;return true;
        }
        internal bool ContainsIntent(TrafficIntentKey intent)=>entries.ContainsKey(intent);
        public TrafficDecision GetDecision(TrafficIntentKey intent) => entries.TryGetValue(intent, out var entry)
            ? entry.Decision : new TrafficDecision(intent, TrafficDecisionKind.Cancelled, "missing-intent");

        private void Evaluate(Entry entry)
        {
            if(!LiveEntry(entry) || entry.ProducerPreviewHeld)return;
            if (entry.Leg != null) { EvaluateRetainedLeg(entry); return; }
            var request = entry.Request;
            if (!Current(request.Intent) || !SnapshotCurrent(request))
            {
#if UNITY_EDITOR
                // Default-off witness of this original branch; diagnostics cannot suppress the failure.
                // 只传递原始只读事实；observer 异常也必须继续原 Fail。
                try { SnapshotChangedObserver?.Invoke(request,snapshot); }
                catch(Exception) { }
#endif
                Fail(entry, TrafficDecisionKind.StaleContext, "snapshot-changed"); return;
            }
            if (!Valid(request) || !ValidSnapshot()) { Fail(entry, TrafficDecisionKind.InvalidConfiguration, "invalid-traffic-input"); return; }
            var ownBody = snapshot.Bodies.FirstOrDefault(b => b.Owner.ActorId == request.Intent.ActorId);
            if (ownBody != null && ownBody.Position != request.Start) { Fail(entry, TrafficDecisionKind.InvalidStart, "actual-start-changed"); return; }
            if (request.ProvenFailure.HasValue) { Fail(entry, request.ProvenFailure.Value, "explicit-geometry-proof"); return; }
            if(entry.UnpaidEligibility!=null && !UnpaidEligibilityCurrent(entry))
            {Fail(entry,TrafficDecisionKind.StaleContext,"unpaid-producer-identity-changed");return;}
            var blockers = EndpointBlockers(entry);
            if (entry.Parking) AddParkingEndpointRouteBlockers(request, blockers);
            if(blockers.Count>0 && HoldUnpaidEligibility(entry,"unpaid-eligibility/endpoint-occupied",blockers))return;
            if(!CheckCounterAdvanceReadiness(entry,blockers))return;
            if (blockers.Count > 0) { WaitOrFail(entry, blockers,preparation:true); return; }
            if (entry.Jobs.Count == 0)
            {
                if (HasPrimary(entry)) entry.Jobs.Add(new RouteJob(request.Start, request.Target, MovementObstacles(request)));
                foreach (var proof in request.Requirements)
                {
                    var obstacles = proof.Obstacles.Select(p => new Obstacle(p, proof.Clearance)).ToList();
                    if (entry.Parking && proof.AvoidProposedParking) obstacles.Add(new Obstacle(request.Target, proof.Clearance));
                    entry.Jobs.Add(new RouteJob(proof.Start, proof.Goal, obstacles));
                }
            }
            while (entry.JobIndex < entry.Jobs.Count)
            {
                var job = entry.Jobs[entry.JobIndex];
                var status = job.Advance((Vector3 from, Vector3 to, out IReadOnlyList<Vector3> path) => Query(entry, from, to, out path));
                if(!LiveEntry(entry) || !entry.Queued || !ReferenceEquals(entry.Request,request) || !SnapshotCurrent(request) ||
                    entry.JobIndex>=entry.Jobs.Count || !ReferenceEquals(entry.Jobs[entry.JobIndex],job))return;
                if (status == JobStatus.Deferred) { Decide(entry, TrafficDecisionKind.DeferredByBudget, "native-query-frame-budget"); return; }
                if (status == JobStatus.Failed) { Fail(entry, TrafficDecisionKind.PlanningFailed, "bounded-route-proof-failed",preparation:true,alternate:true); return; }
                if (status == JobStatus.Exhausted) { Fail(entry, TrafficDecisionKind.PlanningFailed, "intent-query-budget-exhausted",preparation:true); return; }
                entry.JobIndex++;
            }
            if(!CheckAdmissionReadiness(entry))return;
            if (HasPrimary(entry))
            {
                entry.CurrentPath = entry.Jobs[0].ExpandedPath;
                blockers=SelectedPathBlockers(entry,entry.CurrentPath);
                if (blockers.Count > 0)
                {
                    if(WaitForPreparedCanonicalSuccessor(entry,blockers))return;
                    // Only the original body-only wait may try one bounded physical detour, after all rights checks.
                    // 已有canonical/路线优先检查不变；不提交、不提前转移原权利。
                    if(TryStartSelectedDetour(entry,request,0,blockers)) { Evaluate(entry);return; }
                    WaitOrFail(entry, blockers,preparation:true);return;
                }
            }
            if(WaitForPreparedCanonicalSuccessor(entry))return;
            if(CanPreferUnacquired(entry,request))
            {
                var previous=HasPrimary(entry)?0:-1;var selectedOffset=HasPrimary(entry)?1:0;
                for(var index=0;index<request.Requirements.Count;index++)
                {
                    var requirement=request.Requirements[index];if(!requirement.SelectedJourney)continue;
                    var selected=index+selectedOffset;var joined=new List<Vector3>();
                    if(previous>=0 && entry.Jobs[previous].ExpandedPath.Length!=0)joined.Add(entry.Jobs[previous].ExpandedPath.Last());
                    joined.Add(requirement.Start);joined.AddRange(entry.Jobs[selected].ExpandedPath);joined.Add(requirement.Goal);
                    var selectedBlockers=SelectedPathBlockers(entry,joined,requirement.Clearance);
                    if(selectedBlockers.Count!=0)
                    {
                        if(TryStartSelectedDetour(entry,request,selected,selectedBlockers)) { Evaluate(entry);return; }
                        WaitOrFail(entry,selectedBlockers,preparation:true);return;
                    }
                    previous=selected;
                }
            }
            if(!CheckCounterAdvanceReadiness(entry))return;
            entry.Queued = false; entry.Prepared = true;
            if (request.SelectedJourney.Count > 1) entry.Journey = request.SelectedJourney.ToArray();
            var proofs=new List<TrafficRouteProof>();var offset=HasPrimary(entry)?1:0;
            if(offset!=0) proofs.Add(new TrafficRouteProof(request.ResourceId,entry.Jobs[0].Waypoints,entry.Jobs[0].ExpandedPath,entry.Jobs[0].LegEnds));
            for(var index=0;index<request.Requirements.Count;index++)
            {
                var requirement=request.Requirements[index];var job=entry.Jobs[index+offset];
                proofs.Add(new TrafficRouteProof(requirement.ResourceId,job.Waypoints,job.ExpandedPath,job.LegEnds));
            }
            if(request.Requirements.Any(r=>r.SelectedJourney))
                entry.Journey=entry.CurrentPath.Concat(request.Requirements.Select((r,i)=>new{r,i})
                    .Where(x=>x.r.SelectedJourney).SelectMany(x=>entry.Jobs[x.i+offset].ExpandedPath)).ToArray();
            var route = HasPrimary(entry) ? entry.Jobs[0].Waypoints : Array.Empty<Vector3>();
            entry.Decision = new TrafficDecision(request.Intent, TrafficDecisionKind.Granted, "prepared", ++permit,
                entry.RequestSequence, entry.AcquiredSequence, route: route,proofRoutes:proofs);
            entry.UnpaidEligibilitySpatialRefusal=null;entry.UnpaidEligibilitySpatialActors.Clear();
            entry.UnpaidEligibilityNativeActivity=Array.Empty<UnpaidNativeActivity>();
            entry.WaitingBlockers=Array.Empty<TrafficBlocker>();SyncWork(entry);
        }
        private static bool HasPrimary(Entry entry) => !entry.Parking || entry.Request.PlanPrimaryRoute;

        // Availability only: no intent, query, permit or candidate history is created here.
        // 只排除当前占位目标；true 不是许可，真正 Submit/Evaluate 仍重新验证。
        internal bool IsParkingEndpointAvailable(TrafficRequest candidate)
        {
            if (closed || !Valid(candidate) || !Current(candidate.Intent) || !SnapshotCurrent(candidate) || !ValidSnapshot()) return false;
            var blockers = EndpointBlockers(candidate);
            AddParkingEndpointRouteBlockers(candidate, blockers);
            return blockers.Count == 0;
        }
        private void AddParkingEndpointRouteBlockers(TrafficRequest request, List<TrafficBlocker> blockers)
        {
            foreach (var other in Rights(request))
                if (NearPath(request.Target, other.Journey, PairClearance(request, other.Request)) ||
                    NearPath(request.Target, other.CurrentPath, PairClearance(request, other.Request)))
                    blockers.Add(new TrafficBlocker(other.Request.Intent, other.Request.ResourceId, "selected-journey-parking-protection"));
        }
        private List<TrafficBlocker> EndpointBlockers(Entry entry) => EndpointBlockers(entry.Request);
        private List<TrafficBlocker> EndpointBlockers(TrafficRequest request)
        {
            var result = new List<TrafficBlocker>();
            foreach (var body in snapshot.Bodies.Where(b => b.Owner.ActorId != request.Intent.ActorId))
                if (Distance(request.Target, body.Position) < request.Radius + body.Radius + Mathf.Max(request.SafetyMargin, body.SafetyMargin))
                    result.Add(new TrafficBlocker(body.Owner, "body", body.Enabled ? "live-body-checkpoint" : "disabled-body-remains"));
            foreach (var parking in snapshot.Parking.Where(p => p.Owner.ActorId != request.Intent.ActorId))
                if (Distance(request.Target, parking.Position) < request.Radius + parking.Radius + Mathf.Max(request.SafetyMargin, parking.SafetyMargin))
                    result.Add(new TrafficBlocker(parking.Owner, parking.ResourceId, "canonical-parking-version:" + parking.OwnerVersion));
            foreach (var other in Rights(request).Where(e => e.Parking))
                if (Distance(request.Target, other.Request.Target) < PairClearance(request, other.Request))
                    result.Add(new TrafficBlocker(other.Request.Intent, other.Request.ResourceId, "prepared-or-committed-parking"));
            return result;
        }
        private List<Obstacle> MovementObstacles(TrafficRequest request)
        {
            var result = snapshot.Parking.Where(p => p.Owner.ActorId != request.Intent.ActorId)
                .Select(p => new Obstacle(p.Position, request.Radius + p.Radius + Mathf.Max(request.SafetyMargin, p.SafetyMargin))).ToList();
            foreach (var body in snapshot.Bodies.Where(b => b.Owner.ActorId != request.Intent.ActorId))
                if (!CanProgress(body.Owner, out _)) result.Add(new Obstacle(body.Position, request.Radius + body.Radius + Mathf.Max(request.SafetyMargin, body.SafetyMargin)));
            foreach (var other in OrderedWorkEntries.Where(e => !e.Retired && e.Parking && (e.Active || e.Prepared) && e.Request.Intent.ActorId != request.Intent.ActorId))
                result.Add(new Obstacle(other.Request.Target, PairClearance(request, other.Request)));
            return result;
        }
        // A business countdown can justify finite waiting, but is not a selected independent physical route.
        // 只为尚未获权的移动proposal捕获身体几何；计时变化不使同一cursor失效。
        private List<TrafficBlocker> SelectedPathBlockers(Entry entry,IReadOnlyList<Vector3> path,float minimum=0)
        {
            var request=entry.Request;var blockers=new List<TrafficBlocker>();
            foreach (var body in snapshot.Bodies.Where(b => b.Owner.ActorId != request.Intent.ActorId))
                if (NearPath(body.Position, path, Mathf.Max(minimum,request.Radius + body.Radius + Mathf.Max(request.SafetyMargin, body.SafetyMargin))))
                    blockers.Add(new TrafficBlocker(body.Owner, "body", "live-body-checkpoint"));
            // A coalesced cursor retains geometry, never permission to cross a newly observed canonical claim.
            foreach(var parking in snapshot.Parking.Where(p=>p.Owner.ActorId!=request.Intent.ActorId))
                if(NearPath(parking.Position,path,Mathf.Max(minimum,request.Radius+parking.Radius+Mathf.Max(request.SafetyMargin,parking.SafetyMargin))))
                    blockers.Add(new TrafficBlocker(parking.Owner,parking.ResourceId,"current-canonical-parking-on-planned-route"));
            foreach (var other in Rights(entry))
            {
                // A budget-deferred job may predate a new parking permit in this same snapshot.
                // 最终检查当前停车许可；旧cursor的障碍快照不能授权穿过后来提交的停车点。
                if (other.Parking && NearPath(other.Request.Target, path, Mathf.Max(minimum,PairClearance(request, other.Request))))
                    blockers.Add(new TrafficBlocker(other.Request.Intent, other.Request.ResourceId, "current-parking-on-planned-route"));
                else if (!other.Parking && PathsConflict(path, other.CurrentPath, Mathf.Max(minimum,PairClearance(request, other.Request))))
                    blockers.Add(new TrafficBlocker(other.Request.Intent, other.Request.ResourceId, "current-conflict-segment"));
            }
            return blockers;
        }
        private bool CanPreferUnacquired(Entry entry,TrafficRequest request)
            =>(!entry.Parking || request.PlanPrimaryRoute) && entry.Leg==null && !entry.Active && !entry.Prepared && entry.AcquiredSequence==0 &&
                entry.JourneyPermit==null && entry.JourneyRequest==null && request.SelectedJourney.Count==0 &&
                request.NativeStamp.Equals(default(TrafficNativeStamp));
        private PreferredBody[] PreferredBodiesForJob(Entry entry,TrafficRequest request,int jobIndex)
        {
            if(!CanPreferUnacquired(entry,request))return Array.Empty<PreferredBody>();
            var minimum=jobIndex==0 && HasPrimary(entry)?0:request.Requirements[jobIndex-(HasPrimary(entry)?1:0)].Clearance;
            return snapshot.Bodies.Where(b=>b.Owner.ActorId!=request.Intent.ActorId && !entries.ContainsKey(b.Owner) &&
                CanProgress(b.Owner,out _)).Select(b=>new PreferredBody(b.Owner,b.ActorLife,
                    new Obstacle(b.Position,Mathf.Max(minimum,request.Radius+b.Radius+Mathf.Max(request.SafetyMargin,b.SafetyMargin))))).ToArray();
        }
        private IEnumerable<int> SelectedJobIndices(Entry entry,TrafficRequest request)
        {
            var offset=HasPrimary(entry)?1:0;if(offset!=0 && entry.Jobs.Count!=0)yield return 0;
            for(var index=0;index<request.Requirements.Count;index++)
                if(request.Requirements[index].SelectedJourney && index+offset<entry.Jobs.Count)yield return index+offset;
        }
        private bool HasStartedSelectedDetour(Entry entry,TrafficRequest request)
            =>SelectedJobIndices(entry,request).Any(index=>entry.Jobs[index].HasPreferredBodies);
        private List<Obstacle> JobBaseObstacles(Entry entry,TrafficRequest request,int jobIndex)
        {
            if(HasPrimary(entry) && jobIndex==0)return MovementObstacles(request);
            var requirement=request.Requirements[jobIndex-(HasPrimary(entry)?1:0)];
            var result=requirement.Obstacles.Select(p=>new Obstacle(p,requirement.Clearance)).ToList();
            if(entry.Parking && requirement.AvoidProposedParking)result.Add(new Obstacle(request.Target,requirement.Clearance));
            return result;
        }
        private List<Obstacle> SelectedPhysicalKeepouts(Entry entry,TrafficRequest request,int jobIndex)
        {
            var minimum=jobIndex==0 && HasPrimary(entry)?0:request.Requirements[jobIndex-(HasPrimary(entry)?1:0)].Clearance;
            return MovementObstacles(request).Select(o=>new Obstacle(o.Position,Mathf.Max(o.Clearance,minimum))).ToList();
        }
        private bool PlanningJobsCompatible(Entry entry,TrafficRequest request)
        {
            foreach(var index in SelectedJobIndices(entry,request))
            {
                var job=entry.Jobs[index];
                if(index!=0 && !job.HasPreferredBodies)continue; // Unstarted future capabilities keep their original metadata contract.
                if(!job.HasSameObstacles(JobBaseObstacles(entry,request,index),PreferredBodiesForJob(entry,request,index),
                    SelectedPhysicalKeepouts(entry,request,index)))return false;
            }
            return true;
        }
        private bool TryStartSelectedDetour(Entry entry,TrafficRequest request,int jobIndex,IReadOnlyList<TrafficBlocker> blockers)
        {
            if(!LiveEntry(entry) || !entry.Queued || !ReferenceEquals(entry.Request,request) || !SnapshotCurrent(request) ||
                !CanPreferUnacquired(entry,request))return false;
            if(!entry.Jobs[jobIndex].TryStartPreferred(blockers,PreferredBodiesForJob(entry,request,jobIndex),
                SelectedPhysicalKeepouts(entry,request,jobIndex)))return false;
            entry.JobIndex=jobIndex;return true;
        }
        private readonly struct PreferredBody
        {
            internal readonly TrafficIntentKey Owner;
            internal readonly object ActorLife;
            internal readonly Obstacle Geometry;
            internal PreferredBody(TrafficIntentKey owner,object actorLife,Obstacle geometry)
            { Owner=owner;ActorLife=actorLife;Geometry=geometry; }
            internal bool Same(PreferredBody other)=>Owner.Equals(other.Owner) && ReferenceEquals(ActorLife,other.ActorLife) &&
                Geometry.Position.Equals(other.Geometry.Position) && Geometry.Clearance.Equals(other.Geometry.Clearance);
        }
        private IEnumerable<Entry> Rights(Entry incoming) => Rights(incoming.Request);
        private IEnumerable<Entry> Rights(TrafficRequest incoming) => OrderedWorkEntries.Where(e => !e.Retired &&
            e.Request.Intent.ActorId != incoming.Intent.ActorId && (e.Active || e.Prepared || e.Journey.Length > 1));
        private IReadOnlyDictionary<TrafficIntentKey,OriginalExitSelectedRoute> CaptureOriginalExitSelectedRoutes(Entry exit)
        {
            var captured=new Dictionary<TrafficIntentKey,OriginalExitSelectedRoute>();
            bool OriginalExit()=>LiveEntry(exit) && Current(exit.Key) && exit.Key.IntentId=="actual-exit" &&
                !exit.Parking && exit.Queued && !exit.Prepared && !exit.Active && !exit.Discarded && exit.AcquiredSequence==0 &&
                exit.Leg==null && exit.JourneyPermit==null && exit.JourneyRequest==null && exit.PendingHandoff==null &&
                !exit.EverCanonicalAttempt && !exit.ExplicitRecoveryUsed && !exit.LegacyLegCompleted && !exit.ContinuationAttempted &&
                exit.Request.ResourceId=="pickup/"+exit.Key.ActorId && exit.Request.NativeStamp.Equals(default(TrafficNativeStamp)) &&
                exit.FrozenOwners==null && exit.Jobs.Count>0 && exit.JobIndex==exit.Jobs.Count &&
                ReferenceEquals(exit.CurrentPath,exit.Jobs[0].ExpandedPath) && exit.CurrentPath.Length>1 &&
                SnapshotCurrent(exit.Request) && PreparationIdentityCurrent(exit);
            if(OriginalExit())
            {
                foreach(var owner in Rights(exit))
                {
                    var leg=owner.Leg;var source=owner.JourneyPermit;var journey=owner.JourneyRequest;
                    if(!LiveEntry(owner) || !Current(owner.Key) || owner.Key.IntentId!="pickup-paid-movement" || owner.Parking ||
                        owner.Queued || owner.Discarded || owner.PendingHandoff!=null || owner.ExplicitRecoveryUsed || owner.Suspensions!=0 ||
                        owner.LegacyLegCompleted || owner.AcquiredSequence<=0 || source==null || journey==null ||
                        source.Kind!=TrafficDecisionKind.Granted || !source.Intent.Equals(owner.Key) || !journey.Intent.Equals(owner.Key) ||
                        source.AcquiredSequence!=owner.AcquiredSequence || owner.Decision.Kind!=TrafficDecisionKind.Granted ||
                        owner.Decision.FailureDisposition!=TrafficFailureDisposition.None || !Positive(owner.FiniteLimit) ||
                        leg==null || leg.Completed || leg.Suspended || leg.ReprepareAllowed || leg.Generation<=0 ||
                        leg.ActorLife==null || leg.Proof==null || owner.CurrentPath.Length<2 || owner.Journey.Length<2 ||
                        owner.Request.ResourceId!="pickup/"+owner.Key.ActorId || journey.ResourceId!=owner.Request.ResourceId ||
                        leg.Proof.ResourceId!=owner.Request.ResourceId || !ReferenceEquals(SelectedProofs(owner).FirstOrDefault(),leg.Proof) ||
                        leg.Index<0 || leg.Index>=leg.Proof.Waypoints.Count || leg.Proof.LegEnds.Count!=leg.Proof.Waypoints.Count ||
                        !owner.Request.Target.Equals(leg.Proof.Waypoints[leg.Index]) ||
                        !journey.Target.Equals(leg.Proof.Waypoints.Last()) || !LegBodyCurrent(owner,leg.ActorLife))continue;
                    var prepared=owner.Prepared && !owner.Active && !leg.Committed && !leg.Bound &&
                        leg.Stamp.Equals(default(TrafficNativeStamp)) && owner.Request.NativeStamp.Equals(new TrafficNativeStamp(0,leg.Generation)) &&
                        SnapshotCurrent(owner.Request) && StrictLegOwnFactsCurrent(owner);
                    var bound=owner.Active && !owner.Prepared && TryReadUnpaidNativeActivity(owner.Key.ActorId,out var activity) &&
                        ReferenceEquals(activity.Owner,owner);
                    if(!prepared && !bound)continue;
                    var first=leg.Index==0?0:leg.Proof.LegEnds[leg.Index-1];
                    var remainder=GuardProofRemainder(leg.Proof,first).ToArray();
                    if(remainder.Length>1 && PathsConflict(exit.CurrentPath,remainder,PairClearance(exit.Request,journey)))
                        captured.Add(owner.Key,new OriginalExitSelectedRoute(exit,owner,remainder));
                }
            }
            return new System.Collections.ObjectModel.ReadOnlyDictionary<TrafficIntentKey,OriginalExitSelectedRoute>(captured);
        }
        private bool HasOriginalExitSelectedRouteEligibility(Entry exit,TrafficBlocker blocker)
        {
            if(exit.Key.IntentId!="actual-exit" || exit.AcquiredSequence!=0 || exit.Leg!=null || exit.JourneyPermit!=null ||
                exit.JourneyRequest!=null || exit.PendingHandoff!=null || exit.ExplicitRecoveryUsed || exit.Discarded ||
                !Positive(exit.WaitRemaining) || exit.FrozenSelectedRouteEligibility==null ||
                !exit.FrozenSelectedRouteEligibility.TryGetValue(blocker.Owner,out var original) ||
                !exit.Request.Start.Equals(original.ExitStart) || !exit.Request.Target.Equals(original.ExitTarget) ||
                !TryReadUnpaidNativeActivity(blocker.Owner.ActorId,out var activity))return false;
            var route=blocker.ResourceId==original.Resource && blocker.Proof=="current-conflict-segment";
            var bodyProjection=blocker.ResourceId=="body" && blocker.Proof=="live-body-checkpoint";
            if(!route && !bodyProjection)return false;
            var owner=activity.Owner;var leg=activity.Leg;
            var sameSource=ReferenceEquals(owner,original.Owner) && owner.Key.Equals(blocker.Owner) &&
                ReferenceEquals(activity.Source,original.Source) && ReferenceEquals(owner.JourneyRequest,original.Journey) &&
                ReferenceEquals(activity.ActorLife,original.ActorLife) && ReferenceEquals(leg.Proof,original.Proof) &&
                owner.AcquiredSequence==original.AcquiredSequence && owner.FiniteLimit==original.FiniteLimit &&
                !owner.ExplicitRecoveryUsed && owner.Suspensions==0 && !owner.LegacyLegCompleted &&
                owner.Request.ResourceId==original.Resource && leg.Index>=original.Index && leg.Generation>=original.Generation &&
                PathsConflict(exit.CurrentPath,original.SelectedRemainder,PairClearance(exit.Request,original.Journey));
            if(!sameSource)return false;
            if(route)return true;
            // Only this original Bound source's finite moving body projects its captured selected route.
            // 仅接续原已发路线的真实有限移动身体；停留身体或同Actor别名仍走原拒绝策略。
            var bodies=snapshot.Bodies.Where(body=>body.Owner.Equals(blocker.Owner)).ToArray();
            if(bodies.Length!=1)return false;
            var body=bodies[0];
            return body.Enabled && body.ActorActive && ReferenceEquals(body.ActorLife,original.ActorLife) &&
                body.IndependentlyProgressing && Positive(body.RemainingSeconds) &&
                NearPath(body.Position,original.SelectedRemainder,body.Radius+leg.ArrivalTolerance) &&
                NearPath(body.Position,exit.CurrentPath,exit.Request.Radius+body.Radius+Mathf.Max(exit.Request.SafetyMargin,body.SafetyMargin));
        }
        private void WaitOrFail(Entry entry, IEnumerable<TrafficBlocker> source,bool preparation=false)
        {
            var blockers = source.GroupBy(b => new { b.Owner, b.ResourceId }).Select(g => g.First()).ToArray();
            if(preparation && HoldUnpaidEligibility(entry,"unpaid-eligibility/route-capability-unavailable",blockers))return;
            var remaining = Mathf.Min(waitSeconds, entry.Request.FiniteSeconds);
            var handoffGap=false;
            foreach (var blocker in blockers)
            {
                if(HoldsHandoffDependency(entry,blocker.Owner,blocker.ResourceId))
                {handoffGap=true;continue;} // Existing bounded edge only; this is not independent Native motion.
                var ownerSeconds=0f;
                // Preserve the original left-to-right short circuit exactly; record only the first failed condition.
                var refusal=entry.FrozenOwners!=null && !entry.FrozenOwners.Contains(blocker.Owner) &&
                    !HasOriginalExitSelectedRouteEligibility(entry,blocker)?"new-frozen-owner":
                    blocker.Owner.Equals(entry.Request.Intent)?"self-dependency":
                    DependsOn(blocker.Owner,entry.Request.Intent,new HashSet<TrafficIntentKey>())?"dependency-cycle":
                    !CanProgress(blocker.Owner,out ownerSeconds)?"owner-not-independent":null;
                if(refusal!=null)
                {
                    var inspect=ClaimInspectionGuard(entry);
                    if(preparation && refusal=="new-frozen-owner" && DeferPaidPreparationAfterReleasedExit(entry,blockers))return;
                    if(!inspect())return;
                    var alternate=DeliveryFrozenBlockerMayTryOriginalAlternate(entry,blocker,refusal,preparation,blockers,out var alternateRefusal);
                    if(!inspect())return;
                    if(!alternate)StoreWaitFailure(entry,blocker,refusal,blockers,alternateRefusal);
                    ObserveWaitFailure(entry,blocker,refusal);if(alternate && !inspect())return;
                    Fail(entry,TrafficDecisionKind.PlanningFailed,"no-independent-finite-release-or-cycle",blockers,
                        preparation:preparation,alternate:alternate,deliveryAlternateDebt:alternate);return;
                }
                remaining = Mathf.Min(remaining, ownerSeconds);
            }
            if (entry.FrozenOwners == null)
            {
                if(entry.Key.IntentId=="actual-exit")
                {
                    var inspectCapture=ClaimInspectionGuard(entry);
                    var eligibility=CaptureOriginalExitSelectedRoutes(entry);
                    if(!inspectCapture())return;
                    entry.FrozenSelectedRouteEligibility=eligibility;
                }
                entry.FrozenOwners = new HashSet<TrafficIntentKey>(blockers.Select(b => b.Owner));
                entry.WaitRemaining = entry.AdmissionWaitStarted || entry.CounterAdvanceReadinessWaitStarted || entry.YieldOwnerWaitStarted?Mathf.Min(entry.WaitRemaining,remaining):remaining;
                entry.FrozenResources=blockers.GroupBy(b=>b.Owner).ToDictionary(g=>g.Key,g=>new HashSet<string>(g.Select(b=>b.ResourceId)));
#if UNITY_EDITOR
                if(entry.Key.IntentId=="actual-exit" && WaitFailureObserver!=null)
                {
                    try
                    {
                            WaitFailureObserver("EXIT_INITIAL_FREEZE seq="+entry.RequestSequence+" snapshot="+snapshot.Version+"/"+snapshot.BodyVersion+
                                " target="+entry.Request.Target+" path=["+string.Join(";",entry.Jobs.FirstOrDefault()?.ExpandedPath??Array.Empty<Vector3>())+"]"+
                                " peers=["+string.Join(";",entries.Values.Where(p=>!p.Retired && p.Key.IntentId=="pickup-paid-movement").Select(p=>
                                    p.Key.ActorId+"/"+p.Key.Generation+"/seq"+p.RequestSequence+"/acq"+p.AcquiredSequence+"/active"+p.Active+
                                    "/prepared"+p.Prepared+"/leg"+p.Leg?.Index+"/bound"+p.Leg?.Bound+"/request"+p.Leg?.Stamp.RequestId+
                                    "/current=["+string.Join(";",p.CurrentPath)+"]/journey=["+string.Join(";",p.Journey)+"]"))+"]");
                    }
                    catch(Exception) { } // Diagnostic failure leaves the original WAIT transition to run.
                }
#endif

            }
            if (entry.WaitRemaining <= 0) { Fail(entry, TrafficDecisionKind.PlanningFailed, "wait-budget-exhausted", blockers,preparation:preparation); return; }
            ObserveOwnerInvalidation(entry,TrafficDecisionKind.WaitingForTraffic,handoffGap?"bounded-phase-handoff":"finite-owner-release",blockers);
            entry.Queued = false; entry.Prepared = false; entry.Active = false; entry.CurrentPath = Array.Empty<Vector3>();
            Decide(entry, TrafficDecisionKind.WaitingForTraffic, handoffGap?"bounded-phase-handoff":"finite-owner-release", blockers);
            // Existing consumers still cannot rely on a waiting owner's finite motion.
            // Only a genuinely unborn parking preparation may reprove around the actual static body, on its original budgets.
            InvalidateDependentWaiters(entry,reproveUnbornParking:!handoffGap);
        }
        // An already-paid, unissued fixed movement may finish its original reproof after a real exit.
        // 原离店已真实释放后，仅在原债务内等待真实已发腿结束；不新增WAIT边、Frozen成员或权限。
        private bool DeferPaidPreparationAfterReleasedExit(Entry entry,IReadOnlyList<TrafficBlocker> blockers)
        {
            if(!LiveEntry(entry) || !Current(entry.Key) || entry.Key.IntentId!="pickup-paid-movement" || entry.Parking ||
                !entry.Request.PlanPrimaryRoute || entry.Request.ResourceId!="pickup/"+entry.Key.ActorId ||
                !entry.Queued || entry.Prepared || entry.Active || entry.Discarded || entry.AcquiredSequence!=0 ||
                entry.Leg!=null || entry.JourneyPermit!=null || entry.JourneyRequest!=null || entry.Journey.Length!=0 ||
                entry.PendingHandoff!=null || entry.EverCanonicalAttempt || entry.ExplicitRecoveryUsed || entry.LegacyLegCompleted ||
                entry.Decision.FailureDisposition!=TrafficFailureDisposition.None || entry.Request.SelectedJourney.Count!=0 ||
                !entry.Request.NativeStamp.Equals(default(TrafficNativeStamp)) || entry.Request.ProvenFailure.HasValue ||
                entry.FrozenOwners==null || entry.FrozenOwners.Count==0 || entry.FrozenResources==null ||
                !Positive(entry.WaitRemaining) || entry.WaitRemaining>entry.FiniteLimit || !PreparationIdentityCurrent(entry) ||
                entry.PreparationClaims==null || entry.PreparationClaims.Length!=2 ||
                !entry.PreparationClaims.Any(p=>p!=null && p.BusinessOwner!=null && p.ReleaseObligation!=null && p.ResourceId=="counter/"+entry.Key.ActorId &&
                    p.CanonicalOwner is AnimalCafe.Customers.CounterQueueService && p.SemanticCheckpoint is AnimalCafe.Customers.CounterQueueService.AssignmentClaim counter &&
                    ReferenceEquals(p.CanonicalOwner,counter.CanonicalOwner) && counter.Assignment.Arrived && counter.Assignment.SlotIndex==0) ||
                !entry.PreparationClaims.Any(p=>p!=null && p.BusinessOwner!=null && p.ReleaseObligation!=null && p.ResourceId=="pickup/"+entry.Key.ActorId &&
                    p.CanonicalOwner is AnimalCafe.Customers.PickupQueueService && p.SemanticCheckpoint is AnimalCafe.Customers.PickupQueueService.ReservationClaim pickup &&
                    ReferenceEquals(p.CanonicalOwner,pickup.CanonicalOwner) && pickup.Assignment.Position==entry.Request.Target) ||
                entry.Request.Requirements.Any(r=>r==null || r.ResourceId==null || r.SelectedJourney || !r.AvoidProposedParking ||
                    !r.ResourceId.StartsWith("existing-goal/",StringComparison.Ordinal) || r.ResourceId=="existing-goal/"+entry.Key.ActorId))return false;
            var inspect=ClaimInspectionGuard(entry);
            foreach(var old in entry.FrozenOwners)
                if(old.IntentId!="actual-exit" || !entries.TryGetValue(old,out var released) || !released.Retired ||
                    snapshot.Bodies.Any(b=>b.Owner.ActorId==old.ActorId) || snapshot.Parking.Any(p=>p.Owner.ActorId==old.ActorId) ||
                    Rights(entry).Any(right=>right.Key.ActorId==old.ActorId))return false;
            if(!QueuedPreparationClaimsCurrent(entry,inspect) || !inspect())return false;
            foreach(var key in blockers.Select(b=>b.Owner).Distinct())
            {
                if(entry.FrozenOwners.Contains(key) || key.Equals(entry.Key) || DependsOn(key,entry.Key,new HashSet<TrafficIntentKey>()) ||
                    !entries.TryGetValue(key,out var moving))return false;
                var checkMoving=ClaimInspectionGuard(moving);
                if(!TryReadUnpaidNativeActivity(key.ActorId,out var actual) || !ReferenceEquals(actual.Owner,moving) ||
                    !moving.Request.IndependentlyProgressing || !IndependentPathClear(moving) || !checkMoving())return false;
                var own=blockers.Where(b=>b.Owner.Equals(key)).ToArray();
                if(!own.Any(b=>b.ResourceId=="body" && b.Proof=="live-body-checkpoint") || own.Any(b=>
                    !(b.ResourceId=="body" && b.Proof=="live-body-checkpoint") &&
                    !(b.ResourceId==moving.Request.ResourceId && b.Proof=="current-conflict-segment")))return false;
                if(!inspect())return false;
            }
            // Keep completed jobs, exact target and every lifetime account. The normal batch checks again.
            // 当前Deferred不建立新依赖；Queued保留原Tick扣时，实际claim变化仍须完整重证。
            entry.Queued=true;entry.Prepared=entry.Active=false;
            Decide(entry,TrafficDecisionKind.DeferredByBudget,"paid-preparation-issued-route-revalidation",blockers);return true;
        }
        // A new issued body or an authenticated prepared collection point may invalidate this one side.
        // Only an already-declared delivery side may be tried; no new WAIT edge, time or permission.
        // 原delivery侧失败可换未试原侧；真实有限债务在选择空档继续扣减。
        private bool DeliveryFrozenBlockerMayTryOriginalAlternate(Entry entry,TrafficBlocker first,string refusal,bool preparation,
            IReadOnlyList<TrafficBlocker> blockers,out string denied)
        {
            denied="not-applicable";
            var preparedCollection=first.Owner.IntentId=="pickup-service" && first.ResourceId=="pickup/"+first.Owner.ActorId &&
                first.Proof=="prepared-or-committed-parking";
            if(!preparation || refusal!="new-frozen-owner" ||
                (!preparedCollection && (first.ResourceId!="body" || first.Proof!="live-body-checkpoint")) ||
                entry.Key.IntentId!="employee-pickup-journey" || entry.Request.ResourceId!="pickup/"+entry.Key.ActorId || !entry.Request.PlanPrimaryRoute ||
                entry.Request.SelectedJourney.Count!=0 || entry.Request.Requirements.Count(r=>r.SelectedJourney)!=1 ||
                !entry.Request.Requirements.Any(r=>r.ResourceId=="selected-home" && r.SelectedJourney && r.Start.Equals(entry.Request.Target) && !r.AvoidProposedParking) ||
                entry.FrozenOwners==null || entry.FrozenOwners.Count==0 || entry.FrozenResources==null || !Positive(entry.WaitRemaining) ||
                entry.WaitRemaining>entry.FiniteLimit || entry.AcquiredSequence!=0 || entry.Active || entry.Prepared || entry.Discarded ||
                entry.Leg!=null || entry.JourneyPermit!=null || entry.JourneyRequest!=null || entry.Journey.Length!=0 || entry.PendingHandoff!=null ||
                entry.EverCanonicalAttempt || entry.ExplicitRecoveryUsed || entry.LegacyLegCompleted || entry.ContinuationAttempted ||
                !entry.Request.NativeStamp.Equals(default(TrafficNativeStamp)) || !PreparationIdentityCurrent(entry))return false;
            denied="original-candidates-exhausted";if(CandidateAttempts(entry).Count>=entry.CandidateCount)return false;
            var inspect=ClaimInspectionGuard(entry);var frozen=entry.FrozenOwners.ToArray();
            if(preparedCollection)
            {
                denied="not-current-prepared-collection-endpoint";
                // Authenticate each prepared right at this guest's single endpoint; the next side still avoids every right.
                // 同一顾客的同一取餐位可有多份准备证明；逐份核验，不撤销占位或添加等待依赖。
                if(!entry.Parking || frozen.Length!=1 || frozen[0].IntentId!="actual-exit" ||
                    frozen[0].ActorId==first.Owner.ActorId || first.Owner.ActorId==entry.Key.ActorId ||
                    !entry.FrozenResources.TryGetValue(frozen[0],out var original) ||
                    !original.Contains("pickup/"+frozen[0].ActorId) ||
                    !entry.WaitingBlockers.Any(b=>b.Owner.Equals(frozen[0]) && b.ResourceId=="pickup/"+frozen[0].ActorId && b.Proof=="current-conflict-segment") ||
                    !entries.TryGetValue(first.Owner,out var endpoint))return false;
                var target=endpoint.Request.Target;
                foreach(var blocker in blockers)
                {
                    denied="not-current-prepared-collection-endpoint";
                    if(blocker.Owner.ActorId!=first.Owner.ActorId || blocker.Owner.IntentId!="pickup-service" ||
                        blocker.ResourceId!=first.ResourceId || blocker.Proof!="prepared-or-committed-parking" ||
                        entry.FrozenOwners.Contains(blocker.Owner) ||
                        DependsOn(blocker.Owner,entry.Key,new HashSet<TrafficIntentKey>()))return false;
                    if(!PreparedCollectionEndpointCurrent(blocker.Owner,out denied))return false;
                    denied="prepared-side-target-or-entry-changed";
                    if(!entries.TryGetValue(blocker.Owner,out var candidate) || !candidate.Request.Target.Equals(target) || !inspect())return false;
                }
                denied="eligible-original-alternate";return true;
            }
            foreach(var key in blockers.Select(b=>b.Owner).Distinct())
            {
                denied="self-or-cycle:"+key.ActorId;
                if(key.Equals(entry.Key) || DependsOn(key,entry.Key,new HashSet<TrafficIntentKey>()))return false;
                if(entry.FrozenOwners.Contains(key))
                {
                    denied="original-owner-not-independent:"+key.ActorId;
                    if(!blockers.Where(b=>b.Owner.Equals(key)).All(b=>HoldsHandoffDependency(entry,key,b.ResourceId)) &&
                        (!CanProgress(key,out var seconds) || !Positive(seconds)))return false;
                }
                else
                {
                    denied="new-owner-not-current-bound-independent:"+key.ActorId;
                    if(!entries.TryGetValue(key,out var moving))return false;var inspectMoving=ClaimInspectionGuard(moving);
                    if(!TryReadUnpaidNativeActivity(key.ActorId,out var activity) || !ReferenceEquals(activity.Owner,moving) || !activity.Owner.Key.Equals(key) ||
                        !moving.Request.IndependentlyProgressing || moving.CurrentPath.Length<2 || !IndependentPathClear(moving) || !inspectMoving())return false;
                    var actual=blockers.Where(b=>b.Owner.Equals(key)).ToArray();denied="new-owner-non-body-protection:"+key.ActorId;
                    if(!actual.Any(b=>b.ResourceId=="body" && b.Proof=="live-body-checkpoint") || actual.Any(b=>
                        !(b.ResourceId=="body" && b.Proof=="live-body-checkpoint") &&
                        !(b.ResourceId==moving.Request.ResourceId && b.Proof=="current-conflict-segment")))return false;
                }
                denied="preparation-reentered";if(!inspect())return false;
            }
            denied="frozen-history-changed";
            if(entry.FrozenOwners==null || frozen.Length!=entry.FrozenOwners.Count || frozen.Any(k=>!entry.FrozenOwners.Contains(k)) || !inspect())return false;
            denied="eligible-original-alternate";return true;
        }
        private bool PreparedCollectionEndpointCurrent(TrafficIntentKey key,out string denied)
        {
            denied="prepared-side-owner";
            if(!entries.TryGetValue(key,out var owner) || !LiveEntry(owner) || !Current(key) || !owner.Parking ||
                !owner.Prepared || owner.Active || owner.Queued || owner.Discarded || owner.AcquiredSequence!=0 ||
                owner.Decision.Kind!=TrafficDecisionKind.Granted || owner.Decision.FailureDisposition!=TrafficFailureDisposition.None ||
                owner.Leg!=null || owner.JourneyPermit!=null || owner.JourneyRequest!=null || owner.PendingHandoff!=null ||
                owner.EverCanonicalAttempt || owner.ExplicitRecoveryUsed || owner.LegacyLegCompleted)return false;
            denied="prepared-side-request";
            if(owner.Request.ResourceId!="pickup/"+key.ActorId || !owner.Request.PlanPrimaryRoute)return false;
            denied="prepared-side-snapshot";if(!SnapshotCurrent(owner.Request))return false;
            denied="prepared-side-original-life";
            if(!owner.Request.NativeStamp.Equals(default(TrafficNativeStamp)) || !owner.PreparationIdentityCaptured ||
                owner.PreparationBody?.ActorLife==null || owner.PreparationClaims==null)return false;
            // A later prepared proof may follow a real queue advance of the same reservation/body.
            // 当前已准备证明可跨同一顾客的真实补位；认证同一身体/预留，不要求第一次提交时的阶段键或位置。
            var currentBodies=snapshot.Bodies.Where(b=>b.Owner.ActorId==key.ActorId).ToArray();
            var preparedBodies=owner.Request.Snapshot.Bodies.Where(b=>b.Owner.ActorId==key.ActorId).ToArray();
            denied="prepared-side-current-body";
            if(currentBodies.Length!=1 || preparedBodies.Length!=1)return false;
            var body=currentBodies[0];var preparedBody=preparedBodies[0];
            if(!Current(body.Owner) || !body.Owner.Equals(preparedBody.Owner) || !body.Enabled || !body.ActorActive ||
                !preparedBody.Enabled || !preparedBody.ActorActive || body.IndependentlyProgressing || body.RemainingSeconds!=0 ||
                !ReferenceEquals(body.ActorLife,owner.PreparationBody.ActorLife) || !ReferenceEquals(body.ActorLife,preparedBody.ActorLife) ||
                body.Position!=owner.Request.Start || preparedBody.Position!=body.Position ||
                body.Radius!=preparedBody.Radius || body.SafetyMargin!=preparedBody.SafetyMargin)return false;
            var current=snapshot.Parking.Where(f=>f.Owner.ActorId==key.ActorId).ToArray();
            var prepared=owner.Request.Snapshot.Parking.Where(f=>f.Owner.ActorId==key.ActorId).ToArray();
            denied="prepared-side-current-claim";
            if(current.Length!=1 || prepared.Length!=1)return false;
            var fact=current[0];var inspect=ClaimInspectionGuard(owner);
            if(!(fact.ResourceId==owner.Request.ResourceId && fact.BusinessOwner!=null && fact.ReleaseObligation!=null &&
                fact.CanonicalOwner is AnimalCafe.Customers.PickupQueueService &&
                fact.SemanticCheckpoint is AnimalCafe.Customers.PickupQueueService.ReservationClaim claim &&
                ReferenceEquals(fact.CanonicalOwner,claim.CanonicalOwner) && claim.Assignment.VisitId==key.ActorId &&
                claim.Assignment.Arrived && claim.IsCurrent && !claim.IsCurrentCollectionLease && claim.Assignment.Position==fact.Position &&
                SameClaimFacts(prepared[0],fact) && TrafficClaimCheckpoints.AreSame(prepared[0],fact) && inspect()))return false;
            denied="prepared-side-original-reservation";
            return owner.PreparationClaims.Any(first=>first!=null && first.ResourceId==fact.ResourceId &&
                ReferenceEquals(first.CanonicalOwner,fact.CanonicalOwner) && ReferenceEquals(first.BusinessOwner,fact.BusinessOwner) &&
                ReferenceEquals(first.ReleaseObligation,fact.ReleaseObligation) &&
                first.SemanticCheckpoint is AnimalCafe.Customers.PickupQueueService.ReservationClaim original && original.IsSameReservation(claim)) && inspect();
        }
#if UNITY_EDITOR
        internal string WaitFailureDiagnosticIntent;
        internal string WaitFailureDiagnostic;
        internal Dictionary<TrafficIntentKey,string> WaitFailureDiagnostics;
        internal int WaitFailureDiagnosticDropped;
#endif
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void StoreWaitFailure(Entry entry,TrafficBlocker blocker,string condition,IEnumerable<TrafficBlocker> allBlockers=null,string alternateRefusal=null)
        {
#if UNITY_EDITOR
            // No observer or per-frame work. Store immutable/raw facts only after the original refusal is decided.
            // 仅原失败分支缓存一次，不重跑CanProgress/路径/claim规则，不影响原Fail。
            var all=WaitFailureDiagnosticIntent=="*";
            if(all && WaitFailureDiagnostics!=null && WaitFailureDiagnostics.ContainsKey(entry.Key))return;
            if(all && WaitFailureDiagnostics!=null && WaitFailureDiagnostics.Count>=64){WaitFailureDiagnosticDropped++;return;}
            if(!all && (WaitFailureDiagnostic!=null || WaitFailureDiagnosticIntent!=entry.Key.IntentId))return;
            string record;
            try
            {
                entries.TryGetValue(blocker.Owner,out var owner);var job=entry.JobIndex<entry.Jobs.Count?entry.Jobs[entry.JobIndex]:null;
                var gap=owner?.PendingHandoff;HashSet<string> captured=null;if(gap!=null)gap.Dependencies.TryGetValue(entry,out captured);
                string Key(TrafficIntentKey k)=>k.ActorId+"/"+k.IntentId+"/g"+k.Generation+"/"+k.Context;
                string Point(Vector3 p)=>p.ToString("F9");string Path(IEnumerable<Vector3> p)=>string.Join(";",p??Array.Empty<Vector3>());
                record="WAIT_STORED condition="+condition+" intent="+Key(entry.Key)+" owner="+Key(blocker.Owner)+" resource="+blocker.ResourceId+" proof="+blocker.Proof+
                    " previous="+entry.Decision?.Kind+" priorWaiting=["+string.Join(";",entry.WaitingBlockers.Select(b=>Key(b.Owner)+"/"+b.ResourceId+"/"+b.Proof))+"]"+
                    " handoffCaptured=["+string.Join(";",captured??Enumerable.Empty<string>())+"] handoffRemaining="+gap?.Remaining+" handoffSourceMatchesJourneyPermit="+(gap!=null && ReferenceEquals(gap.Source,owner.JourneyPermit))+
                    " alternate="+alternateRefusal+" allBlockers=["+string.Join(";",(allBlockers??Array.Empty<TrafficBlocker>()).Select(b=>Key(b.Owner)+"/"+b.ResourceId+"/"+b.Proof))+"]"+
                    " first="+entry.FirstFrame+" queries="+entry.Queries+" sequence="+entry.RequestSequence+" acquired="+entry.AcquiredSequence+
                    " queued="+entry.Queued+" prepared="+entry.Prepared+" active="+entry.Active+" frozen="+(entry.FrozenOwners==null?"NONE":string.Join(";",entry.FrozenOwners.Select(Key)))+
                    " job="+entry.JobIndex+"/"+entry.Jobs.Count+" start="+Point(entry.Request.Start)+" target="+Point(entry.Request.Target)+" path=["+Path(job?.ExpandedPath)+"]"+
                    " ownerState="+owner?.Decision?.Kind+"/"+owner?.Decision?.Reason+" ownerAcquired="+owner?.AcquiredSequence+" ownerActive="+owner?.Active+" ownerPrepared="+owner?.Prepared+
                    " ownerRetired="+owner?.Retired+" ownerParking="+owner?.Parking+" ownerHandoff="+(owner?.PendingHandoff!=null)+
                    " ownerNative="+owner?.Leg?.Committed+"/"+owner?.Leg?.Bound+"/"+owner?.Leg?.Completed+
                    " roles=["+string.Join(";",entry.Request.Requirements.Select(r=>r.ResourceId+":"+Point(r.Start)+"->"+Point(r.Goal)+" selected="+r.SelectedJourney))+"]"+
                    " bodies=["+string.Join(";",snapshot.Bodies.Select(body=>Key(body.Owner)+"@"+Point(body.Position)+" enabled="+body.Enabled+" active="+body.ActorActive+" independent="+body.IndependentlyProgressing+" finite="+body.RemainingSeconds))+"]"+
                    " parking=["+string.Join(";",snapshot.Parking.Select(p=>Key(p.Owner)+"/"+p.ResourceId+"@"+Point(p.Position)))+"]";
            }
            catch(Exception error){record="UNKNOWN_WAIT_STORE "+error.GetType().Name;}
            if(all)
            {
                if(WaitFailureDiagnostics==null)WaitFailureDiagnostics=new Dictionary<TrafficIntentKey,string>();
                var k=entry.Key;WaitFailureDiagnostics.Add(k,"fullIntent="+k.Session+"/e"+k.Epoch+"/"+k.ActorId+"/"+k.IntentId+"/g"+k.Generation+"/r"+k.LayoutRevision+"/"+k.Context+" "+record);
                WaitFailureDiagnostic=string.Join("\n",WaitFailureDiagnostics.Values);
            }
            else WaitFailureDiagnostic=record;
#endif
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ObserveWaitFailure(Entry entry,TrafficBlocker blocker,string condition)
        {
#if UNITY_EDITOR
            if(WaitFailureObserver==null)return;
            try
            {
                string Key(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
                entries.TryGetValue(blocker.Owner,out var owner);
                var body=snapshot.Bodies.FirstOrDefault(b=>b.Owner.Equals(blocker.Owner));
                WaitFailureObserver("WAIT_ORIGINAL condition="+condition+" intent="+Key(entry.Request.Intent)+" previous="+entry.Decision?.Kind+
                    " owner="+Key(blocker.Owner)+" proof="+blocker.Proof+" resource="+blocker.ResourceId+
                    " ownerDecision="+owner?.Decision?.Kind+" ownerReason="+owner?.Decision?.Reason+" ownerAcquired="+owner?.AcquiredSequence+
                    " ownerQueued="+owner?.Queued+" ownerPrepared="+owner?.Prepared+" ownerActive="+owner?.Active+" ownerRetired="+owner?.Retired+
                    " ownerLeg="+owner?.Leg?.Index+"/"+owner?.Leg?.Generation+"/"+owner?.Leg?.Committed+"/"+owner?.Leg?.Bound+"/"+owner?.Leg?.Completed+
                    " bodyEnabled="+body?.Enabled+" bodyFinite="+body?.IndependentlyProgressing+" bodyRemaining="+body?.RemainingSeconds+
                    " frozen="+string.Join(";",(entry.FrozenOwners??Enumerable.Empty<TrafficIntentKey>()).Select(Key))+
                    " requestSequence="+entry.RequestSequence+" queries="+entry.Queries+" waitRemaining="+entry.WaitRemaining+
                    " counterHandoffStage="+entry.CounterHandoffReadinessRefusal+" producerStage="+entry.CounterHandoffProducerRefusal+
                    " storedEdges=["+string.Join(";",entry.WaitingBlockers.Select(b=>Key(b.Owner)+"/"+b.ResourceId+"/"+b.Proof))+"]"+
                    " frozenResources=["+(entry.FrozenResources==null?"NULL":string.Join(";",entry.FrozenResources.Select(pair=>Key(pair.Key)+"={"+string.Join(",",pair.Value)+"}")))+"]"+
                    " snapshot="+snapshot.Version+"/"+snapshot.BodyVersion);
            }
            catch(Exception) { } // Observer formatting/callback cannot replace the original refusal.

#endif
        }
        // Default-off original transition, before rights/path loss and dependent fanout.
        // Only stored facts; witness failure cannot alter the original branch.
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ObserveOwnerInvalidation(Entry owner,TrafficDecisionKind next,string reason,
            IEnumerable<TrafficBlocker> blockers,bool? bodyCouldProgress=null)
        {
#if UNITY_EDITOR
            var observer=WaitFailureObserver;
            if(observer==null || owner==null || owner.Decision==null || owner.Key.IntentId!="actual-exit")return;
            try
            {
                var work=OrderedWorkEntries.ToArray();
                var dependents=work.Where(e=>e!=owner && !e.Retired && e.Decision!=null &&
                    e.Decision.Kind==TrafficDecisionKind.WaitingForTraffic && e.Decision.Blockers.Any(b=>b.Owner.Equals(owner.Key))).ToArray();
                if(dependents.Length==0 && (next!=TrafficDecisionKind.PlanningFailed || reason!="wait-budget-exhausted"))return;
                string Key(TrafficIntentKey k)=>k.Session+"/e"+k.Epoch+"/"+k.ActorId+"/"+k.IntentId+"/g"+k.Generation+"/r"+k.LayoutRevision+"/"+k.Context;
                string Identity(object value)=>value==null?"null":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
                string Path(IEnumerable<Vector3> values)=>string.Join(";",(values??Enumerable.Empty<Vector3>()).Select(v=>v.ToString("F9")));
                string Block(TrafficBlocker b)=>Key(b.Owner)+"/resource="+b.ResourceId+"/proof="+b.Proof;
                string Proof(TrafficRouteProof p)=>p==null?"null":Identity(p)+"/resource="+p.ResourceId+"/waypoints=["+Path(p.Waypoints)+"]/expanded=["+Path(p.ExpandedPath)+"]/ends=["+string.Join(",",p.LegEnds)+"]";
                string Decision(TrafficDecision d)=>d==null?"null":Identity(d)+"/key="+Key(d.Intent)+"/kind="+d.Kind+"/reason="+d.Reason+"/disposition="+d.FailureDisposition+
                    "/permit="+d.Permit+"/seq="+d.RequestSequence+"/acq="+d.AcquiredSequence+"/blockers=["+string.Join(";",d.Blockers.Select(Block))+"]/route=["+Path(d.Route)+"]/proofs=["+string.Join(";",d.ProofRoutes.Select(Proof))+"]";
                string Request(TrafficRequest r)=>r==null?"NOT_RETAINED":Identity(r)+"/key="+Key(r.Intent)+"/first="+r.RequestedFrame+"/snapshot="+r.Snapshot.Version+"/body="+r.Snapshot.BodyVersion+
                    "/resource="+r.ResourceId+"/start="+r.Start.ToString("F9")+"/target="+r.Target.ToString("F9")+"/radius="+r.Radius+"/margin="+r.SafetyMargin+
                    "/independent="+r.IndependentlyProgressing+"/finite="+r.FiniteSeconds+"/native="+r.NativeStamp.RequestId+"/"+r.NativeStamp.LegGeneration+
                    "/selected=["+Path(r.SelectedJourney)+"]/requirements=["+string.Join(";",r.Requirements.Select(q=>q.ResourceId+"/"+q.Start.ToString("F9")+"->"+q.Goal.ToString("F9")+
                        "/clearance="+q.Clearance+"/selected="+q.SelectedJourney+"/avoidParking="+q.AvoidProposedParking+"/obstacles=["+Path(q.Obstacles)+"]"))+"]";
                string Body(TrafficBodyFact b)=>b==null?"null":Key(b.Owner)+"/pose="+b.Position.ToString("F9")+"/radius="+b.Radius+"/margin="+b.SafetyMargin+
                    "/enabled="+b.Enabled+"/active="+b.ActorActive+"/independent="+b.IndependentlyProgressing+"/remaining="+b.RemainingSeconds+"/life="+Identity(b.ActorLife);
                string Parking(TrafficParkingFact p)=>Key(p.Owner)+"/resource="+p.ResourceId+"/pose="+p.Position.ToString("F9")+"/radius="+p.Radius+"/margin="+p.SafetyMargin+
                    "/version="+p.OwnerVersion+"/canonical="+Identity(p.CanonicalOwner)+"/business="+Identity(p.BusinessOwner)+"/release="+Identity(p.ReleaseObligation);
                string EntryText(Entry e)=>"key="+Key(e.Key)+" decision="+Decision(e.Decision)+" first="+e.FirstFrame+" seq="+e.RequestSequence+" acq="+e.AcquiredSequence+" queries="+e.Queries+
                    " queued="+e.Queued+" prepared="+e.Prepared+" active="+e.Active+" retired="+e.Retired+" parking="+e.Parking+" wait="+e.WaitRemaining+" finite="+e.FiniteLimit+
                    " current=["+Path(e.CurrentPath)+"] journey=["+Path(e.Journey)+"] frozenOwners=["+string.Join(";",(e.FrozenOwners??Enumerable.Empty<TrafficIntentKey>()).Select(Key))+
                    "] frozenResources=["+(e.FrozenResources==null?"null":string.Join(";",e.FrozenResources.Select(p=>Key(p.Key)+"={"+string.Join(",",p.Value)+"}")))+"] waiting=["+string.Join(";",e.WaitingBlockers.Select(Block))+"]"+
                    "\nREQUEST "+Request(e.Request)+"\nJOURNEY_REQUEST "+Request(e.JourneyRequest)+" permit="+Decision(e.JourneyPermit)+
                    "\nLEG "+(e.Leg==null?"null":Identity(e.Leg)+"/life="+Identity(e.Leg.ActorLife)+"/index="+e.Leg.Index+"/generation="+e.Leg.Generation+"/stamp="+e.Leg.Stamp.RequestId+"/"+e.Leg.Stamp.LegGeneration+
                        "/committed="+e.Leg.Committed+"/bound="+e.Leg.Bound+"/completed="+e.Leg.Completed+"/suspended="+e.Leg.Suspended+"/reprepare="+e.Leg.ReprepareAllowed+"/needsQuery="+e.Leg.NeedsQuery+
                        "/path=["+Path(e.Leg.Path)+"]/suffix=["+Path(e.Leg.Suffix)+"]/proof="+Proof(e.Leg.Proof))+
                    "\nPREPARATION body="+Body(e.PreparationBody)+" claims=["+string.Join(";",(e.PreparationClaims??Array.Empty<TrafficParkingFact>()).Select(Parking))+"] pendingHandoff="+Identity(e.PendingHandoff);
                var records=new List<string>{"OWNER_INVALIDATION ownerIntent="+owner.Key.IntentId+" owner="+Key(owner.Key)+" previous="+owner.Decision.Kind+" next="+next+" reason="+reason+
                    " snapshot="+snapshot.Version+"/"+snapshot.BodyVersion+" computedBodyCouldProgress="+(bodyCouldProgress.HasValue?bodyCouldProgress.Value.ToString():"NOT_COMPUTED")+
                    " causeBlockers=["+string.Join(";",(blockers??Enumerable.Empty<TrafficBlocker>()).Select(Block))+"]", "OWNER "+EntryText(owner)};
                records.AddRange(dependents.Select(e=>"DEPENDENT "+EntryText(e)));
                records.Add("SNAPSHOT_BODIES ["+string.Join(";",snapshot.Bodies.Select(Body))+"]");
                records.Add("SNAPSHOT_PARKING ["+string.Join(";",snapshot.Parking.Select(Parking))+"]");
                records.AddRange(work.Where(e=>e!=owner && !dependents.Contains(e) && (e.Active || e.Prepared || e.Journey.Length>1)).Select(e=>"PEER "+EntryText(e)));
                observer(string.Join("\n",records));
            }
            catch(Exception){} // Keep the original transition/fanout; missing receipt remains UNKNOWN.
#endif
        }
        private bool CanProgress(TrafficIntentKey owner, out float seconds)
        {
            seconds = 0;
            var body = snapshot.Bodies.FirstOrDefault(b => b.Owner.Equals(owner));
            if (body != null && (!body.Enabled || !body.ActorActive)) return false;
            if (entries.TryGetValue(owner, out var entry))
            {
                if(entry.PendingHandoff!=null)return false;
                if (entry.Retired || entry.Decision?.Kind == TrafficDecisionKind.WaitingForTraffic || entry.Decision?.Kind == TrafficDecisionKind.PlanningFailed) return false;
                if ((entry.Active || entry.Prepared) && entry.Request.IndependentlyProgressing && Positive(entry.Request.FiniteSeconds) &&
                    !entry.Parking && entry.CurrentPath.Length > 1 && IndependentPathClear(entry))
                { seconds = entry.Request.FiniteSeconds; return true; }
            }
            if (body == null || !body.Enabled || !body.ActorActive || !body.IndependentlyProgressing || !Positive(body.RemainingSeconds)) return false;
            seconds = body.RemainingSeconds; return true;
        }
        private bool DependsOn(TrafficIntentKey owner, TrafficIntentKey target, HashSet<TrafficIntentKey> visited)
        {
            if (owner.Equals(target)) return true;
            if (!visited.Add(owner) || !entries.TryGetValue(owner, out var entry) || entry.Decision == null || entry.Decision.Kind != TrafficDecisionKind.WaitingForTraffic) return false;
            return entry.Decision.Blockers.Any(b => DependsOn(b.Owner, target, visited));
        }

        public bool TryCommit(TrafficDecision decision, IPreparedTrafficMutation mutation = null, Action publish = null)
        {
            if (committing || closed || decision == null || !entries.TryGetValue(decision.Intent, out var entry) ||
                !LiveEntry(entry) || !entry.Prepared || entry.Decision.FailureDisposition==TrafficFailureDisposition.Terminal ||
                !ReferenceEquals(entry.Decision, decision)) return false;
            if(entry.UnpaidEligibility!=null && (entry.UnpaidEligibilityHeld || !UnpaidEligibilityCurrent(entry)))return false;
            if(!CheckAdmissionReadiness(entry) || !CheckCounterAdvanceReadiness(entry))return false;
            var request=entry.Request;var resource=request.ResourceId;
            var actualBody = snapshot.Bodies.FirstOrDefault(b => b.Owner.ActorId == entry.Request.Intent.ActorId);
            if (!Current(decision.Intent) || !SnapshotCurrent(entry.Request) ||
                (actualBody != null && (!actualBody.Enabled || !actualBody.ActorActive || actualBody.Position != entry.Request.Start)) ||
                (entry.Leg != null && !LegBodyCurrent(entry, entry.Leg.ActorLife)) || (mutation != null && !mutation.IsCurrent))
            { Fail(entry, TrafficDecisionKind.StaleContext, "commit-version-changed", releasedProvisional: true); return false; }
            if(!LiveEntry(entry) || !entry.Prepared || !ReferenceEquals(entry.Request,request) || !ReferenceEquals(entry.Decision,decision) || !SnapshotCurrent(request))return false;
            if(!CanCommitSuccessorPreparation(entry) || WaitForPreparedCanonicalSuccessor(entry))return false;
            if(!LiveEntry(entry) || !entry.Prepared || !ReferenceEquals(entry.Request,request) || !ReferenceEquals(entry.Decision,decision) || !SnapshotCurrent(request))return false;
            ITrafficMutationReceipt receipt = null; var rollbackAttempted = false; committing = true;
            void RollbackOnce()
            {
                if (receipt == null || rollbackAttempted) return;
                rollbackAttempted = true; receipt.RollbackOwnMutation();
            }
            try
            {
                // Actual canonical invocation permanently disqualifies preparation-only recovery, even if it returns false.
                // Submit/Discard/后续规划失败不清除此标记；无 mutation 的空间提交不属于 canonical 尝试。
                if(mutation!=null)entry.EverCanonicalAttempt=true;
                if (mutation != null && (!mutation.TryApply(out receipt) || receipt == null || !receipt.IsCurrent))
                {
                    RollbackOnce(); Fail(entry, TrafficDecisionKind.PlanningFailed, "canonical-apply-failed", releasedProvisional: true); return false;
                }
                if(!LiveEntry(entry) || !entry.Prepared || !ReferenceEquals(entry.Request,request) || !ReferenceEquals(entry.Decision,decision) || !SnapshotCurrent(request))
                { RollbackOnce();return false; }
                // No external events between canonical write and traffic write.
                entry.Prepared = false; entry.Active = true;
                if (entry.AcquiredSequence == 0) entry.AcquiredSequence = ++acquiredSequence;
                ClearNativeChunks(entry);
                decision.AcquiredSequence = entry.AcquiredSequence;
                if (entry.Leg == null)
                { entry.JourneyPermit = decision; entry.JourneyRequest = entry.Request; entry.JourneyParking = entry.Parking; }
                else { entry.Leg.Committed = true; entry.Leg.ReprepareAllowed = false; }
                SyncWork(entry);
            }
            catch
            {
                if (!rollbackAttempted && receipt != null && receipt.IsCurrent)
                { try { RollbackOnce(); } catch { /* Receipt owns rollback failure; never invoke it twice. */ } }
                Fail(entry, TrafficDecisionKind.PlanningFailed, "canonical-apply-exception", releasedProvisional: true); return false;
            }
            finally { committing = false; }
            // Only a successful joint winner supersedes its unselected provisional alternatives.
            // 共同提交成功后只废弃候选空间权利；同phase仍可按原年龄和预算重新准备。
            foreach(var sibling in OrderedWorkEntries.Where(other=>other!=entry && !other.Retired && other.AcquiredSequence==0 &&
                (other.Prepared || other.Journey.Length>1) && Current(other.Request.Intent) &&
                other.Key.ActorId==entry.Key.ActorId && other.Request.ResourceId==resource).ToArray())
                DiscardPrepared(sibling.Key);
            LastPublicationException = null;
            try { publish?.Invoke(); }
            catch (Exception error) { LastPublicationException = error; } // Committed business survives presentation failures.
            return true;
        }
        public bool HasActiveGrant(TrafficIntentKey intent) => entries.TryGetValue(intent, out var entry) && !entry.Retired && entry.Active;
        // Read-only provenance/issue checks for consumers; these never prepare, wake, query or commit.
        public bool TryGetCommittedJourney(TrafficIntentKey intent,out TrafficDecision source)
        {
            source=null;
            if(closed || !Current(intent) || !entries.TryGetValue(intent,out var entry) || entry.Retired || entry.JourneyPermit==null)return false;
            source=entry.JourneyPermit;return true;
        }
        public bool TryGetCommittedLegProvenance(TrafficIntentKey intent,object actualActorLife,int nativeGeneration,
            out TrafficDecision source,out TrafficWorldSnapshot savedSnapshot)
        {
            source=null;savedSnapshot=null;
            if(closed || committing || !Current(intent) || !entries.TryGetValue(intent,out var entry) || entry.Retired ||
                !entry.Active || entry.Leg==null || !entry.Leg.Committed || entry.Leg.Completed || entry.Leg.Suspended ||
                entry.Leg.Generation!=nativeGeneration || entry.JourneyPermit==null || !entry.JourneyPermit.Intent.Equals(intent) ||
                !LegBodyCurrent(entry,actualActorLife))return false;
            // Bound Native motion may change body facts; it cannot rewrite the committed provenance or grant another issue.
            // 已bound保留原始strict快照；未bound仍须拥有当前可发腿权限。
            if(entry.Leg.Bound)
            { if(entry.Leg.Stamp.RequestId<=0 || entry.Leg.Stamp.LegGeneration!=nativeGeneration)return false; }
            else if(!CanIssueLeg(intent,actualActorLife,nativeGeneration))return false;
            source=entry.JourneyPermit;savedSnapshot=entry.Request.Snapshot;return true;
        }
        public bool CanIssueLeg(TrafficIntentKey intent,object actorLife,int nativeLegGeneration)
        {
            if(closed || committing || !Current(intent) || !entries.TryGetValue(intent,out var entry) || entry.Retired ||
                !entry.Active || entry.Decision.FailureDisposition==TrafficFailureDisposition.Terminal ||
                entry.Leg==null || !entry.Leg.Committed || entry.Leg.Bound || entry.Leg.Completed ||
                entry.Leg.Generation!=nativeLegGeneration || !SnapshotCurrent(entry.Request) || !LegBodyCurrent(entry,actorLife))return false;
            return snapshot.Bodies.First(b=>b.Owner.ActorId==intent.ActorId).Position==entry.Request.Start;
        }
        public bool RetainNamedJourney(TrafficIntentKey intent,string resourceId)
        {
            if(!entries.TryGetValue(intent,out var entry) || entry.Leg != null || entry.Retired || !entry.Active ||
                entry.Decision.FailureDisposition==TrafficFailureDisposition.Terminal)return false;
            var proof=entry.Decision.ProofRoutes.FirstOrDefault(p=>p.ResourceId==resourceId);
            if(proof==null)return false;
            entry.CurrentPath=Array.Empty<Vector3>();entry.Journey=proof.ExpandedPath.ToArray();SyncWork(entry);
            NotifyChanged(intent,entry.Request.ResourceId);return true;
        }
        // Transfer paid bookkeeping under an existing parking right; never acquire a new target or movement leg.
        // 仅消费已有许可；失败保留原parking、age与预算，真实发腿仍由movement入口核验。
        public bool TryContinueCanonical(TrafficDecision permission,TrafficRequest current,object canonicalOwner,long ownerVersion,
            IPreparedTrafficMutation mutation,Action publish=null)
        {
            if(committing || closed || permission==null || current==null || mutation==null ||
                !entries.TryGetValue(permission.Intent,out var entry) || entry.Retired || !entry.Active || !entry.Parking ||
                entry.ContinuationAttempted || entry.Decision.FailureDisposition==TrafficFailureDisposition.Terminal ||
                !ReferenceEquals(permission,entry.Decision) || !current.Intent.Equals(permission.Intent) || !Current(permission.Intent) ||
                !SnapshotCurrent(current) || current.Start!=entry.Request.Start || current.Target!=entry.Request.Target ||
                current.ResourceId!=entry.Request.ResourceId || !mutation.IsCurrent) return false;
            if(!LiveEntry(entry) || !entry.Active || !ReferenceEquals(entry.Decision,permission) || !SnapshotCurrent(current))return false;
            var body=snapshot.Bodies.FirstOrDefault(b=>b.Owner.ActorId==permission.Intent.ActorId);
            if(body!=null && (!body.Enabled || !body.ActorActive || body.Position!=current.Start)) return false;
            if(!snapshot.Parking.Any(p=>p.Owner.ActorId==permission.Intent.ActorId && ReferenceEquals(p.CanonicalOwner,canonicalOwner) &&
                p.OwnerVersion==ownerVersion && p.Position==current.Target && p.ResourceId==current.ResourceId)) return false;
            ITrafficMutationReceipt receipt=null;var rolledBack=false;var applied=false;committing=true;
            // Consume only an actual invocation, including uncertainty and successful publication failure.
            // Preflight 拒绝不消耗；实际尝试后旧 continuation 永不重试。
            entry.ContinuationAttempted=true;entry.EverCanonicalAttempt=true;
            void Undo(){if(receipt==null || rolledBack)return;rolledBack=true;receipt.RollbackOwnMutation();}
            try
            {
                if(!mutation.TryApply(out receipt) || receipt==null || !receipt.IsCurrent){Undo();return false;}
                if(!LiveEntry(entry) || !entry.Active || !ReferenceEquals(entry.Decision,permission)){Undo();return false;}
                applied=true;
            }
            catch{if(!rolledBack && receipt!=null && receipt.IsCurrent){try{Undo();}catch{}}return false;}
            finally
            {
                committing=false;
                if(!applied && LiveEntry(entry))
                {
                    // Keep the physical parking guard even when rollback is unknown; revoke mutation/issue authority.
                    // 实际 parking 保护仍在，terminal 不代表释放或成功运动。
                    entry.Prepared=entry.Queued=false;wakeups.Remove(permission.Intent);
                    Decide(entry,TrafficDecisionKind.PlanningFailed,"canonical-continuation-failed",failureDisposition:TrafficFailureDisposition.Terminal);
                }
            }
            if(!applied)return false;
            LastPublicationException=null;try{publish?.Invoke();}catch(Exception error){LastPublicationException=error;}
            return true;
        }
        // Abandon a candidate without losing phase age or consumed budget.
        public bool DiscardPrepared(TrafficIntentKey intent)
        {
            if (!entries.TryGetValue(intent, out var entry) || entry.Retired || entry.Active ||
                entry.Decision.FailureDisposition!=TrafficFailureDisposition.None ||
                entry.Leg==null && entry.JourneyPermit!=null) return false;
            if (entry.Leg != null)
            {
                // A committed business journey can have a still-uncommitted movement candidate.
                // 只撤销尚未发腿的候选；保留原已选余程，不能借 discard 复活真实失败或已绑定腿。
                if (entry.Leg.Committed || entry.Leg.Bound || entry.Leg.Completed ||
                    !(entry.Queued || entry.Prepared || entry.Leg.ReprepareAllowed)) return false;
                entry.Leg.ReprepareAllowed = true;
            }
            AbortPreparationsUsing(entry,"successor-discarded");
            if(!LiveEntry(entry) || entry.Decision.FailureDisposition!=TrafficFailureDisposition.None)return false;
            entry.ProducerPreviewHeld=false;entry.Queued = false; entry.Prepared = false; entry.Discarded = true; entry.Jobs.Clear(); entry.JobIndex = 0;
            entry.FrozenSelectedRouteEligibility=null;
            entry.CurrentPath = Array.Empty<Vector3>();
            if (entry.Leg == null) entry.Journey = Array.Empty<Vector3>();
            Decide(entry, TrafficDecisionKind.Cancelled, "proposal-discarded"); NotifyChanged(intent, entry.Request.ResourceId); return true;
        }
        public bool CompleteLeg(TrafficIntentKey intent, TrafficNativeStamp stamp)
        {
            if (!entries.TryGetValue(intent, out var entry) || entry.Leg != null || entry.Retired || !entry.Active ||
                entry.Decision.FailureDisposition==TrafficFailureDisposition.Terminal || !entry.Request.NativeStamp.Equals(stamp)) return false;
            entry.Active = false; entry.CurrentPath = Array.Empty<Vector3>();
            entry.LegacyLegCompleted=true;SyncWork(entry);
            // Preserve the selected journey until caller supplies its verified suffix.
            // 到站只退休当前腿；不猜全局最近点，避免回调间隙丢失下一腿保护。
            NotifyChanged(intent, entry.Request.ResourceId);
            // Legacy stamp authority can only identify this exact registered owner, never an ActorId alias.
            if(!entry.Parking && snapshot.Bodies.Any(b=>b.Owner.Equals(intent)))NotifyChanged(intent,"body");
            return true;
        }
        // A retained proof supplies geometry, never a second phase age or an immediate issue permission.
        // 消费原始 proof 的确定腿索引；当前腿和未来受保护余程是两份不同的几何事实。
        // One typed outcome: only NoOp may fall through to legacy PrepareLeg.
        // 同源身份失效明确 Rejected，不能借旧 body-only 分支重新取得许可。
        internal enum PreparedLegRequeueResult { NoOp, Requeued, Rejected }
        internal PreparedLegRequeueResult TryRequeuePreparedLeg(TrafficDecision retainedPermit,string proofResource,int legIndex,
            object actorLife,int legGeneration,TrafficWorldSnapshot currentSnapshot,Vector3 actualStart,out TrafficDecision next)
        {
            next=null;
            if(closed || paused || committing || retainedPermit==null || !Current(retainedPermit.Intent) ||
                !ReferenceEquals(currentSnapshot,snapshot) || !entries.TryGetValue(retainedPermit.Intent,out var entry) || !LiveEntry(entry) ||
                !ReferenceEquals(entry.JourneyPermit,retainedPermit) || entry.JourneyRequest==null || entry.Decision==null ||
                entry.Decision.Kind!=TrafficDecisionKind.Granted || entry.Decision.FailureDisposition!=TrafficFailureDisposition.None ||
                !entry.Prepared || entry.Queued || entry.Active || entry.Parking || entry.Discarded || entry.PendingHandoff!=null ||
                !Valid(entry.Request) || SnapshotCurrent(entry.Request))return PreparedLegRequeueResult.NoOp;
            var request=entry.Request;var leg=entry.Leg;
            if(leg==null || leg.Committed || leg.Bound || leg.Completed || leg.Suspended || leg.ReprepareAllowed || actorLife==null ||
                !ReferenceEquals(leg.ActorLife,actorLife) || leg.Generation!=legGeneration || legGeneration<1 || leg.Index!=legIndex ||
                leg.Proof==null || leg.Proof.ResourceId!=proofResource || !SelectedProofs(entry).Any(proof=>ReferenceEquals(proof,leg.Proof)) ||
                legIndex<0 || legIndex>=leg.Proof.Waypoints.Count || request.ResourceId!=proofResource || request.Target!=leg.Proof.Waypoints[legIndex] ||
                request.Start!=actualStart || leg.Stamp.RequestId!=0 || leg.Stamp.LegGeneration!=0 ||
                request.NativeStamp.RequestId!=0 || request.NativeStamp.LegGeneration!=legGeneration)return PreparedLegRequeueResult.NoOp;
            var inspect=ClaimInspectionGuard(entry);var ownCurrent=ValidSnapshot() && StrictLegOwnFactsCurrent(entry);
            if(!inspect())
            {next=new TrafficDecision(retainedPermit.Intent,TrafficDecisionKind.StaleContext,"claim-inspection-source-changed",failureDisposition:TrafficFailureDisposition.Terminal);return PreparedLegRequeueResult.Rejected;}
            if(!ownCurrent)
            {
                // This exact unissued candidate has lost its own identity; withdraw its real permission.
                // 只撤销已认证的本候选；原 Fail 保留 strict Leg/余程/账本，不伤 foreign 或已发权利。
                Fail(entry,TrafficDecisionKind.StaleContext,"strict-preparation-owner-changed");
                next=entry.Decision;return PreparedLegRequeueResult.Rejected;
            }
            entry.Request=new TrafficRequest(request.Intent,request.RequestedFrame,snapshot,request.Start,request.Target,request.Radius,
                request.ResourceId,request.SelectedJourney,request.Requirements,request.NativeStamp,request.IndependentlyProgressing,
                request.FiniteSeconds,request.ProvenFailure,request.SafetyMargin,request.PlanPrimaryRoute);
            // The next normal query uses the original limit, and later world drift uses the guarded queued path.
            // NeedsQuery 承接下一帧变化；只消耗原 query 额度，不重建腿、不增预算。
            leg.NeedsQuery=true;entry.Prepared=false;entry.Queued=true;entry.CurrentPath=Array.Empty<Vector3>();
            next=Decide(entry,TrafficDecisionKind.DeferredByBudget,"strict-preparation-requeued");return PreparedLegRequeueResult.Requeued;
        }

        public TrafficDecision PrepareLeg(TrafficDecision retainedPermit, string proofResource, int legIndex,
            object actorLife, int legGeneration, TrafficWorldSnapshot currentSnapshot, Vector3 actualStart,
            float arrivalTolerance, int requestedFrame)
        {
            var intent = retainedPermit == null ? default : retainedPermit.Intent;
            TrafficDecision Reject(TrafficDecisionKind kind, string reason) => new TrafficDecision(intent, kind, reason);
            if (closed) return Reject(TrafficDecisionKind.Cancelled, "context-closed");
            if (!Current(intent)) return Reject(TrafficDecisionKind.StaleContext, "context-changed");
            if (committing || retainedPermit == null || !entries.TryGetValue(intent, out var entry))
                return Reject(TrafficDecisionKind.Cancelled, "missing-retained-permit");
            if (entry.Retired) return entry.Decision;
            if(entry.Decision.FailureDisposition==TrafficFailureDisposition.Terminal) return entry.Decision;
            if (!ReferenceEquals(entry.JourneyPermit, retainedPermit) || entry.JourneyRequest == null)
                return Reject(TrafficDecisionKind.StaleContext, "foreign-retained-proof");
            var body = snapshot.Bodies.FirstOrDefault(b => b.Owner.ActorId == intent.ActorId);
            var sourceBody = entry.JourneyRequest.Snapshot.Bodies.FirstOrDefault(b => b.Owner.ActorId == intent.ActorId);
            if (!ReferenceEquals(currentSnapshot, snapshot) || actorLife == null || body == null || !body.Enabled || !body.ActorActive || !Current(body.Owner) ||
                !ReferenceEquals(body.ActorLife, actorLife) || sourceBody != null && !ReferenceEquals(sourceBody.ActorLife, actorLife) ||
                body.Position != actualStart)
                return Reject(TrafficDecisionKind.StaleContext, "actual-actor-life-or-pose-changed");
            var selected = SelectedProofs(entry).ToArray();
            var proofIndex = Array.FindIndex(selected, p => p.ResourceId == proofResource);
            if (proofIndex < 0 || legGeneration < 1 || !Positive(arrivalTolerance) || !Finite(actualStart))
                return Reject(TrafficDecisionKind.InvalidConfiguration, "invalid-retained-leg");
            var proof = selected[proofIndex];
            if (legIndex < 0 || legIndex >= proof.Waypoints.Count || proof.LegEnds.Count != proof.Waypoints.Count)
                return Reject(TrafficDecisionKind.InvalidConfiguration, "invalid-retained-leg-index");
            var first = legIndex == 0 ? 0 : proof.LegEnds[legIndex - 1]; var last = proof.LegEnds[legIndex];
            if (first < 0 || last < first || last >= proof.ExpandedPath.Count)
                return Reject(TrafficDecisionKind.InvalidConfiguration, "invalid-retained-leg-ends");
            if (first == last && !CompatibleNativeEndpoints(new[] { proof.ExpandedPath[first] }, actualStart,
                proof.Waypoints[legIndex], arrivalTolerance))
                return Reject(TrafficDecisionKind.InvalidConfiguration, "invalid-single-point-leg");
            var restoringSuspendedLeg=false;
            if (entry.Leg != null)
            {
                var prior = entry.Leg;
                if (!ReferenceEquals(prior.ActorLife, actorLife)) return Reject(TrafficDecisionKind.StaleContext, "leg-actor-life-changed");
                if (!prior.Completed)
                {
                    restoringSuspendedLeg=prior.Suspended && prior.Proof==proof && prior.Index==legIndex && legGeneration>prior.Generation;
                    if(prior.Suspended && !restoringSuspendedLeg)
                        return Reject(TrafficDecisionKind.Cancelled,"suspended-leg-needs-new-native-generation");
                    var retryCandidate = prior.ReprepareAllowed && !prior.Committed && !prior.Bound &&
                        prior.Proof == proof && prior.Index == legIndex && legGeneration >= prior.Generation;
                    if (!retryCandidate && !restoringSuspendedLeg)
                    {
                        // Preserve the original short-circuit checks; null means the later check was not evaluated.
                        // 保留原检查顺序；null 明确表示前一条件失败后没有执行后续检查。
                        bool? sameProof=null,sameIndex=null,sameGeneration=null,sameStart=null,sameSnapshot=null;
                        if ((sameProof=prior.Proof == proof).Value && (sameIndex=prior.Index == legIndex).Value &&
                            (sameGeneration=prior.Generation == legGeneration).Value &&
                            (sameStart=entry.Request.Start == actualStart).Value &&
                            (sameSnapshot=SnapshotCurrent(entry.Request)).Value) return entry.Decision;
                        var inspect=ClaimInspectionGuard(entry);
                        // Polling a sleeping/queued original leg grants nothing and must not replace its finite wait.
                        // Same unissued identity only; real Notify/ready-batch evaluation remains the wakeup authority.
                        var retainPending=sameProof==true && sameIndex==true && sameGeneration==true && sameStart==true &&
                            !entry.Prepared && !entry.Active && !entry.Parking && !entry.Discarded && entry.PendingHandoff==null &&
                            entry.Decision.FailureDisposition==TrafficFailureDisposition.None &&
                            (entry.Decision.Kind==TrafficDecisionKind.WaitingForTraffic ||
                                entry.Queued && entry.Decision.Kind==TrafficDecisionKind.DeferredByBudget) &&
                            !prior.Committed && !prior.Bound && !prior.Completed && !prior.Suspended && !prior.ReprepareAllowed &&
                            prior.Stamp.RequestId==0 && prior.Stamp.LegGeneration==0 &&
                            entry.Request.NativeStamp.RequestId==0 && entry.Request.NativeStamp.LegGeneration==prior.Generation &&
                            entry.Request.ResourceId==proofResource && entry.Request.Target==proof.Waypoints[legIndex] &&
                            Valid(entry.Request) && ValidSnapshot() && StrictLegOwnFactsCurrent(entry);
                        if(!inspect())return Reject(TrafficDecisionKind.StaleContext,"claim-inspection-source-changed");
                        if(retainPending)return entry.Decision;
                        var revalidate=sameProof==true && sameIndex==true && sameGeneration==true && sameStart==true &&
                            entry.Prepared && !entry.Active && !prior.Committed && !prior.Bound && !prior.Completed && !prior.Suspended &&
                            (entry.Request.Snapshot.Version==snapshot.Version && entry.Request.Snapshot.BodyVersion!=snapshot.BodyVersion ||
                                StrictLegSnapshotAllowsExternalRemoval(entry));
                        if(!inspect())return Reject(TrafficDecisionKind.StaleContext,"claim-inspection-source-changed");
                        if(revalidate)
                        {
                            // Revoke only this unissued permit; the same leg/path must pass the ordinary current-snapshot batch.
                            // 旧 body-only 分支不变；兼容的外部占位减少也只撤销未发许可，不直接授权或重置原腿账本。
                            var request=entry.Request;
                            var reason=request.Snapshot.Version==snapshot.Version?"current-body-revalidation":"current-canonical-revalidation";
                            entry.Request=new TrafficRequest(request.Intent,request.RequestedFrame,snapshot,request.Start,request.Target,request.Radius,
                                request.ResourceId,request.SelectedJourney,request.Requirements,request.NativeStamp,request.IndependentlyProgressing,
                                request.FiniteSeconds,request.ProvenFailure,request.SafetyMargin,request.PlanPrimaryRoute);
                            entry.Prepared=false;entry.Queued=true;entry.CurrentPath=Array.Empty<Vector3>();
                            return Decide(entry,TrafficDecisionKind.DeferredByBudget,reason);
                        }
                        ObservePrepareLegRejection(entry,proof,legIndex,legGeneration,actualStart,requestedFrame,
                            sameProof,sameIndex,sameGeneration,sameStart,sameSnapshot);
                        return Reject(TrafficDecisionKind.Cancelled, "previous-leg-not-completed");
                    }
                }
                else
                {
                    var nextInProof = prior.Proof == proof && legIndex == prior.Index + 1;
                    var nextProof = prior.Index == prior.Proof.Waypoints.Count - 1 && legIndex == 0 &&
                        proofIndex > 0 && selected[proofIndex - 1] == prior.Proof;
                    if (legGeneration <= prior.Generation || !(nextInProof || nextProof))
                        return Reject(TrafficDecisionKind.Cancelled, "completed-leg-cannot-replay-or-skip");
                }
            }
            else if (proofIndex != 0 || legIndex != 0)
                return Reject(TrafficDecisionKind.Cancelled, "selected-journey-cannot-skip");
            var path = proof.ExpandedPath.Skip(first).Take(last - first + 1).ToArray();
            var following = selected.Skip(proofIndex + 1).SelectMany(p => GuardProofRemainder(p, 0)).ToArray();
            entry.Leg = new LegState { Proof = proof, Index = legIndex, Generation = legGeneration,
                ActorLife = actorLife, ArrivalTolerance = arrivalTolerance, Path = path,
                Suffix = GuardProofRemainder(proof, last).Concat(following).ToArray(), NeedsQuery = restoringSuspendedLeg || path[0] != actualStart };
            var source = entry.JourneyRequest;
            entry.Request = new TrafficRequest(intent, requestedFrame, currentSnapshot, actualStart, proof.Waypoints[legIndex],
                source.Radius, proofResource, nativeStamp: new TrafficNativeStamp(0, legGeneration),
                independentlyProgressing: source.IndependentlyProgressing, finiteSeconds: source.FiniteSeconds, safetyMargin: source.SafetyMargin);
            entry.Journey = GuardProofRemainder(proof, first).Concat(following).ToArray();
            entry.CurrentPath = Array.Empty<Vector3>(); entry.Active = false; entry.Prepared = false;
            entry.Parking = false; entry.Queued = true; entry.Discarded = false; entry.Jobs.Clear(); entry.JobIndex = 0;
            return Decide(entry, TrafficDecisionKind.DeferredByBudget, "retained-leg-ready-batch-pending");
        }
        // Only canonical metadata drift with unchanged bodies/own claims and an external-claim subset is compatible.
        // 自有 claim 按 ActorId 全部保护（不只当前 intent）；外部只许消失或共享版本变化，不能新增/搬移/替换。
        private bool StrictLegSnapshotAllowsExternalRemoval(Entry entry)
        {
            var original=entry.Request.Snapshot;var actor=entry.Request.Intent.ActorId;var world=snapshot;
            var inspect=ClaimInspectionGuard(entry);
            if(original.Version==snapshot.Version || !ValidSnapshot() || entry.Request.NativeStamp.RequestId!=0 ||
                entry.Leg.Stamp.RequestId!=0 || entry.Leg.Stamp.LegGeneration!=0 ||
                original.Bodies.Count!=snapshot.Bodies.Count || original.Parking.Count<snapshot.Parking.Count ||
                original.Bodies.Count(b=>b!=null && b.Owner.ActorId==actor)!=1)return false;
            for(var i=0;i<original.Bodies.Count;i++)
            {
                var old=original.Bodies[i];var current=snapshot.Bodies[i];
                if(old==null || !old.Owner.Equals(current.Owner) || !ReferenceEquals(old.ActorLife,current.ActorLife) ||
                    old.Position!=current.Position || old.Radius!=current.Radius || old.SafetyMargin!=current.SafetyMargin ||
                    old.Enabled!=current.Enabled || old.ActorActive!=current.ActorActive ||
                    old.IndependentlyProgressing!=current.IndependentlyProgressing || old.RemainingSeconds!=current.RemainingSeconds)return false;
            }
            if(original.Parking.Any(p=>p==null) ||
                original.Parking.Count(p=>p.Owner.ActorId==actor)!=snapshot.Parking.Count(p=>p.Owner.ActorId==actor))return false;
            var matched=new HashSet<int>();
            foreach(var current in world.Parking)
            {
                var match=-1;
                for(var i=0;i<original.Parking.Count;i++)
                {
                    if(!inspect())return false;
                    var old=original.Parking[i];
                    if(!SameOwnedClaim(old,current) || old.Position!=current.Position || old.Radius!=current.Radius ||
                        old.SafetyMargin!=current.SafetyMargin)continue;
                    var same=TrafficClaimCheckpoints.AreSame(old,current);
                    if(!inspect())return false;
                    if(!same)continue;
                    if(match>=0)return false; // An ambiguous old identity cannot authenticate a current claim.
                    match=i;
                }
                if(match<0 || !matched.Add(match))return false; // No old claim may authorize two current entries.
                if(!inspect() || current.Owner.ActorId==actor && !SameClaimFacts(original.Parking[match],current))return false;
            }
            return inspect();
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ObservePrepareLegRejection(Entry entry,TrafficRouteProof proof,int index,int generation,Vector3 actualStart,int frame,
            bool? sameProof,bool? sameIndex,bool? sameGeneration,bool? sameStart,bool? sameSnapshot)
        {
#if UNITY_EDITOR
            if(PrepareLegRejectionObserver==null)return;
            string Checked(bool? value)=>value.HasValue?value.Value.ToString():"NOT_EVALUATED";
            var prior=entry.Leg;var request=entry.Request;var intent=request.Intent;
            PrepareLegRejectionObserver("PREPARE_LEG_ORIGINAL reason=previous-leg-not-completed actor="+intent.ActorId+
                " intent="+intent.IntentId+" phaseGen="+intent.Generation+" context="+intent.Context+" epoch="+intent.Epoch+
                " revision="+intent.LayoutRevision+" frame="+frame+" firstFrame="+entry.FirstFrame+
                " proof="+proof.ResourceId+" priorProof="+prior.Proof.ResourceId+" index="+index+" priorIndex="+prior.Index+
                " nativeGen="+generation+" priorNativeGen="+prior.Generation+" stamp="+prior.Stamp.RequestId+"/"+prior.Stamp.LegGeneration+
                " sameProof="+Checked(sameProof)+" sameIndex="+Checked(sameIndex)+" sameGeneration="+Checked(sameGeneration)+
                " sameStart="+Checked(sameStart)+" sameSnapshot="+Checked(sameSnapshot)+
                " start="+request.Start.ToString("F9")+" actualStart="+actualStart.ToString("F9")+
                " requestVersion="+request.Snapshot.Version+" currentVersion="+snapshot.Version+
                " requestBodyVersion="+request.Snapshot.BodyVersion+" currentBodyVersion="+snapshot.BodyVersion+
                " queued="+entry.Queued+" prepared="+entry.Prepared+" active="+entry.Active+" decision="+entry.Decision.Kind+
                " committed="+prior.Committed+" bound="+prior.Bound+" completed="+prior.Completed+" suspended="+prior.Suspended+
                " reprepare="+prior.ReprepareAllowed+" suspensions="+entry.Suspensions+" queries="+entry.Queries);
#endif
        }
        private IEnumerable<TrafficRouteProof> SelectedProofs(Entry entry) => entry.JourneyPermit.ProofRoutes.Where(p =>
            ((!entry.JourneyParking || entry.JourneyRequest.PlanPrimaryRoute) && p.ResourceId == entry.JourneyRequest.ResourceId) ||
            entry.JourneyRequest.Requirements.Any(r => r.SelectedJourney && r.ResourceId == p.ResourceId));
        private bool LegBodyCurrent(Entry entry, object actorLife, bool requireNavigation = true)
        {
            if(!LiveEntry(entry) || entry.Leg==null)return false;
            var body = snapshot.Bodies.FirstOrDefault(b => b.Owner.ActorId == entry.Request.Intent.ActorId);
            // Only an already-completed pending phase may outlive temporary Native unavailability.
            // 已完成pending只依赖真实actor生命周期；所有新发腿仍要求Nav可用。
            return body != null && body.ActorActive && (!requireNavigation || body.Enabled) && Current(body.Owner) &&
                actorLife != null && ReferenceEquals(body.ActorLife, actorLife) &&
                ReferenceEquals(entry.Leg.ActorLife, actorLife);
        }
        private static bool CompatibleNativeEndpoints(IReadOnlyList<Vector3> path,Vector3 start,Vector3 target,float tolerance)
            => path!=null && path.Count>0 && path.All(Finite) &&
                Vector3.Distance(path[0],start)<=tolerance && Vector3.Distance(path[path.Count-1],target)<=tolerance &&
                (path.Count!=1 || Vector3.Distance(start,target)<=tolerance);

        // Raw Native corners remain in Leg.Path/proofs; connectors are safety geometry only.
        // 投影连接段只用于碰撞和未来停车保护，不伪装成 Native 路径或改写 LegEnds。
        private static Vector3[] GuardCurrentPath(Vector3 start,IReadOnlyList<Vector3> path,Vector3 target)
        {
            var guard=new List<Vector3>();
            if(start!=path[0])guard.Add(start);
            guard.AddRange(path);
            if(target!=path[path.Count-1])guard.Add(target);
            return guard.ToArray();
        }
        private static IEnumerable<Vector3> GuardProofRemainder(TrafficRouteProof proof,int first)
        {
            for(var index=first;index<proof.ExpandedPath.Count;index++)
            {
                var corner=proof.ExpandedPath[index];yield return corner;
                for(var leg=0;leg<proof.LegEnds.Count;leg++)
                    if(proof.LegEnds[leg]==index && proof.Waypoints[leg]!=corner)
                    {
                        yield return proof.Waypoints[leg];
                        // Retain the original next Native segment as well as its business endpoint connector.
                        if(index+1<proof.ExpandedPath.Count)yield return corner;
                    }
            }
        }
        private void EvaluateRetainedLeg(Entry entry)
        {
            if(!LiveEntry(entry) || entry.Leg==null)return;
            var leg = entry.Leg; var request = entry.Request;
            if (!Current(request.Intent) || !SnapshotCurrent(request) || !LegBodyCurrent(entry, leg.ActorLife))
            { Fail(entry, TrafficDecisionKind.StaleContext, "leg-context-changed"); return; }
            if (!Valid(request) || !ValidSnapshot()) { Fail(entry, TrafficDecisionKind.InvalidConfiguration, "invalid-leg-input"); return; }
            var body = snapshot.Bodies.First(b => b.Owner.ActorId == request.Intent.ActorId);
            if (body.Position != request.Start) { Fail(entry, TrafficDecisionKind.InvalidStart, "actual-leg-start-changed"); return; }
            if (leg.NeedsQuery)
            {
                var status = Query(entry, request.Start, request.Target, out var path);
                if(!LiveEntry(entry) || !entry.Queued || !ReferenceEquals(entry.Request,request) || !ReferenceEquals(entry.Leg,leg))return;
                if (status == JobStatus.Deferred) { Decide(entry, TrafficDecisionKind.DeferredByBudget, "native-query-frame-budget"); return; }
                if (status != JobStatus.Complete || !CompatibleNativeEndpoints(path,request.Start,request.Target,leg.ArrivalTolerance))
                { Fail(entry, TrafficDecisionKind.PlanningFailed, status == JobStatus.Exhausted ? "intent-query-budget-exhausted" : "actual-leg-proof-failed"); return; }
                leg.Path = path.ToArray(); leg.NeedsQuery = false;
            }
            if(!CompatibleNativeEndpoints(leg.Path,request.Start,request.Target,leg.ArrivalTolerance))
            { Fail(entry,TrafficDecisionKind.PlanningFailed,"actual-leg-proof-failed");return; }
            var guardPath=GuardCurrentPath(request.Start,leg.Path,request.Target);
            // Keep the verified current geometry plus the exact still-selected suffix; no new LegEnds.
            // 实际起点重查可改变当前路径，停车保护必须同步更新，不能继续只看旧路径。
            entry.Journey = guardPath.Concat(leg.Suffix.Skip(1)).ToArray();
            var requirement = entry.JourneyRequest.Requirements.FirstOrDefault(r => r.ResourceId == leg.Proof.ResourceId);
            if (requirement != null && requirement.Obstacles.Any(p => NearPath(p, guardPath, requirement.Clearance)))
            { Fail(entry, TrafficDecisionKind.PlanningFailed, "actual-leg-needs-route-replacement"); return; }
            var blockers = EndpointBlockers(entry);
            foreach (var otherBody in snapshot.Bodies.Where(b => b.Owner.ActorId != request.Intent.ActorId))
                if (NearPath(otherBody.Position, guardPath, request.Radius + otherBody.Radius + Mathf.Max(request.SafetyMargin, otherBody.SafetyMargin)))
                    blockers.Add(new TrafficBlocker(otherBody.Owner, "body", "current-leg-body"));
            foreach (var parking in snapshot.Parking.Where(p => p.Owner.ActorId != request.Intent.ActorId))
                if (NearPath(parking.Position, guardPath, request.Radius + parking.Radius + Mathf.Max(request.SafetyMargin, parking.SafetyMargin)))
                    blockers.Add(new TrafficBlocker(parking.Owner, parking.ResourceId, "canonical-parking-on-current-leg"));
            foreach (var other in Rights(entry))
                if (other.Parking && NearPath(other.Request.Target, guardPath, PairClearance(request, other.Request)) ||
                    !other.Parking && PathsConflict(guardPath, other.CurrentPath, PairClearance(request, other.Request)))
                    blockers.Add(new TrafficBlocker(other.Request.Intent, other.Request.ResourceId, "current-conflict-segment"));
            if (blockers.Count > 0) { WaitOrFail(entry, blockers); return; }
            entry.CurrentPath = guardPath; entry.Prepared = true; entry.Queued = false;
            entry.Decision = new TrafficDecision(request.Intent, TrafficDecisionKind.Granted, "retained-leg-prepared", ++permit,
                entry.RequestSequence, entry.AcquiredSequence, route: new[] { request.Target },
                proofRoutes: new[] { new TrafficRouteProof(request.ResourceId, new[] { request.Target }, leg.Path, new[] { leg.Path.Length - 1 }) });
            entry.WaitingBlockers=Array.Empty<TrafficBlocker>();SyncWork(entry);
        }
        public bool BindLeg(TrafficIntentKey intent, object actorLife, TrafficNativeStamp stamp)
        {
            if (!LiveLeg(intent, actorLife, stamp, out var entry) || entry.Leg.Bound) return false;
            entry.Leg.Stamp = stamp; entry.Leg.Bound = true;SyncWork(entry);return true;
        }
        // A handle alone grants nothing: only this coordinator's registered one-use record authorizes recovery.
        // receipt 不保留已退休 Source/Native leg/snapshot；仅转移原 accounting，不复活旧移动权利。
        internal sealed class MovementRecoveryReceipt
        {
            internal MovementRecoveryReceipt() { }
        }
        private sealed class RecoveryRecord
        {
            internal TrafficIntentKey Original;
            internal WeakReference<object> Actor;
            internal string Resource;
            internal Vector3 Goal;
            internal float Radius,SafetyMargin,FiniteSeconds,WaitRemaining;
            internal bool IndependentlyProgressing,EverCanonicalAttempt;
            internal bool PendingOwnerYieldCancellation,YieldOwnerWaitStarted;
            internal TrafficIntentKey? YieldOwnerParent,YieldOwnerOriginalChild;
            internal int FirstFrame,Queries,Suspensions,CandidateCount,CurrentIndex;
            internal HashSet<int> AttemptedIndices;
            internal long RequestSequence,AcquiredSequence;
            internal HashSet<TrafficIntentKey> FrozenOwners;
            internal Dictionary<TrafficIntentKey,HashSet<string>> FrozenResources;
        }
        // Authenticated initial planning failure only; canonical uncertainty and Native legs have separate authority.
        // 来源由原失败点记录，不能用 Reason 字符串或 consumer 声明代替。
        // Explicit producer recovery retires no acquired right and announces no physical/resource release.
        // 只退休原未提交Collection失败；新的Owner尝试仍须从真实预留完整重证。
        internal bool TryRetireFailedCanonicalPreparation(TrafficDecision expected,object actorLife)
        {
            if(closed || paused || committing || expected==null || actorLife==null || !Current(expected.Intent) ||
                expected.Intent.IntentId!="pickup-service" || !entries.TryGetValue(expected.Intent,out var entry) || !LiveEntry(entry) ||
                !ReferenceEquals(entry.Decision,expected) || expected.Kind!=TrafficDecisionKind.PlanningFailed ||
                expected.FailureDisposition!=TrafficFailureDisposition.Terminal || !entry.PreparationFailed || !entry.Parking ||
                entry.EverCanonicalAttempt || entry.ExplicitRecoveryUsed || entry.LegacyLegCompleted || entry.Discarded || entry.ProducerPreviewHeld ||
                entry.Active || entry.Prepared || entry.Queued || entry.AcquiredSequence!=0 || entry.Leg!=null ||
                entry.PendingHandoff!=null || entry.JourneyPermit!=null || entry.JourneyRequest!=null || entry.Journey.Length!=0 ||
                !entry.PreparationIdentityCaptured || entry.PreparationBody==null || entry.PreparationClaims.Length==0 ||
                !entry.Request.PlanPrimaryRoute || entry.Request.ResourceId!="pickup/"+expected.Intent.ActorId ||
                !entry.Request.NativeStamp.Equals(default(TrafficNativeStamp)) || entry.Request.ProvenFailure.HasValue ||
                entry.Request.SelectedJourney.Count!=0 || entry.Request.Requirements.Any(requirement=>requirement.SelectedJourney) ||
                !Valid(entry.Request) || !ValidSnapshot())return false;
            var inspect=ClaimInspectionGuard(entry);var original=entry.PreparationBody;var request=entry.Request;
            var bodies=snapshot.Bodies.Where(body=>body.Owner.ActorId==expected.Intent.ActorId).ToArray();
            if(bodies.Length!=1 || !ReferenceEquals(original.ActorLife,actorLife) || !ReferenceEquals(bodies[0].ActorLife,actorLife) ||
                !original.Enabled || !original.ActorActive || !bodies[0].Enabled || !bodies[0].ActorActive ||
                !Current(original.Owner) || !Current(bodies[0].Owner) || !original.Position.Equals(request.Start) ||
                !bodies[0].Position.Equals(request.Start) || original.Radius!=bodies[0].Radius || original.SafetyMargin!=bodies[0].SafetyMargin)return false;
            var claims=snapshot.Parking.Where(claim=>claim.Owner.ActorId==expected.Intent.ActorId).ToArray();
            if(claims.Length!=entry.PreparationClaims.Length)return false;
            bool Same(TrafficParkingFact old,TrafficParkingFact current)
            {
                // The old parent may have really retired; only the same typed business claim survives that phase label.
                // 不授予新owner权限；实际canonical Entry/lease身份、geometry与业务义务必须全部相同。
                try
                {
                if(!inspect() || old==null || current==null || !(old.SemanticCheckpoint is ITrafficClaimCheckpoint oldMarker) ||
                    !(current.SemanticCheckpoint is ITrafficClaimCheckpoint nowMarker) || !Current(old.Owner) || !Current(current.Owner) ||
                    old.BusinessOwner==null || old.ReleaseObligation==null || !ReferenceEquals(old.CanonicalOwner,current.CanonicalOwner) ||
                    !ReferenceEquals(old.BusinessOwner,current.BusinessOwner) || !ReferenceEquals(old.ReleaseObligation,current.ReleaseObligation) ||
                    old.ResourceId!="pickup/"+expected.Intent.ActorId || current.ResourceId!=old.ResourceId ||
                    !old.Position.Equals(current.Position) || old.Radius!=current.Radius || old.SafetyMargin!=current.SafetyMargin ||
                    !ReferenceEquals(oldMarker.CanonicalOwner,old.CanonicalOwner) || !ReferenceEquals(nowMarker.CanonicalOwner,current.CanonicalOwner) ||
                    oldMarker.ResourceId!=old.ResourceId || nowMarker.ResourceId!=current.ResourceId ||
                    oldMarker.CapturedOwnerVersion!=old.OwnerVersion || nowMarker.CapturedOwnerVersion!=current.OwnerVersion ||
                    old.OwnerVersion>current.OwnerVersion)return false;
                return oldMarker.IsSameCurrentClaim(nowMarker) && inspect();
                }
                catch(Exception){return false;}
            }
            if(!entry.PreparationClaims.All(old=>claims.Count(current=>Same(old,current))==1) || !inspect())return false;
            RetireEntry(entry,false,expected);
            return entry.Retired && ReferenceEquals(entry.Decision,expected);
        }

        internal bool TryRetireFailedPreparation(TrafficDecision expected, object actorLife, out MovementRecoveryReceipt receipt)
        {
            receipt=null;
            if(closed || paused || committing || expected==null || actorLife==null || !Current(expected.Intent) ||
                !entries.TryGetValue(expected.Intent,out var entry) || !LiveEntry(entry) || !ReferenceEquals(entry.Decision,expected) ||
                expected.Kind!=TrafficDecisionKind.PlanningFailed || !entry.PreparationFailed || entry.EverCanonicalAttempt ||
                entry.Parking || entry.Retired || entry.Discarded || entry.ExplicitRecoveryUsed || entry.Active || entry.Prepared || entry.Queued ||
                entry.Leg!=null || entry.PendingHandoff!=null || entry.JourneyPermit!=null || entry.JourneyRequest!=null || entry.AcquiredSequence!=0 ||
                entry.Request.NativeStamp.RequestId!=0 || entry.Request.ProvenFailure.HasValue || !Valid(entry.Request))return false;
            var original=entry.Request;
            var sourceBodies=original.Snapshot.Bodies.Where(b=>b!=null && b.Owner.ActorId==expected.Intent.ActorId).ToArray();
            var bodies=snapshot.Bodies.Where(b=>b!=null && b.Owner.ActorId==expected.Intent.ActorId).ToArray();
            if(sourceBodies.Length!=1 || bodies.Length!=1)return false;
            var sourceBody=sourceBodies[0];var body=bodies[0];
            if(!sourceBody.Enabled || !sourceBody.ActorActive || !Current(sourceBody.Owner) || !ReferenceEquals(sourceBody.ActorLife,actorLife) ||
                !body.Enabled || !body.ActorActive || !Current(body.Owner) || !ReferenceEquals(body.ActorLife,actorLife) ||
                sourceBody.Position!=original.Start || body.Position!=original.Start)return false;
            CaptureRecovery(entry,original,actorLife,out receipt,notifyRetirement:false);
            // Retired is the authority boundary; retain the exact terminal diagnosis for blocked ordinary polling.
            // 旧失败记录不复活 grant，receipt 仍只保存轻量 accounting。
            entry.Decision=expected;return true;
        }

        internal bool TryRetireFailedLeg(TrafficIntentKey intent, object actorLife, TrafficNativeStamp stamp,
            MovementResult originalFailure, out MovementRecoveryReceipt receipt)
        {
            receipt=null;
            var failed=originalFailure.Status==MovementStatus.Failed && originalFailure.Reason!=NavigationFailure.None;
            var recovered=originalFailure.Status==MovementStatus.Recovered && originalFailure.OriginalFailure!=NavigationFailure.None;
            if((!failed && !recovered) || originalFailure.ActorId!=intent.ActorId || originalFailure.RequestId!=stamp.RequestId ||
                !Finite(originalFailure.FinalPosition) || !LiveLeg(intent,actorLife,stamp,out var entry) ||
                !entry.Leg.Bound || !entry.Leg.Stamp.Equals(stamp) || entry.PendingHandoff!=null || entry.ExplicitRecoveryUsed ||
                entry.JourneyRequest==null || entry.JourneyPermit==null || entry.JourneyParking)return false;
            var body=snapshot.Bodies.FirstOrDefault(b=>b.Owner.ActorId==intent.ActorId);
            if(body==null || body.Position!=originalFailure.FinalPosition)return false;
            CaptureRecovery(entry,entry.JourneyRequest,actorLife,out receipt);
            NotifyChanged(body.Owner,"body");return true;
        }
        // Owner Disable retires this exact issued yield, but only its real successful Cancel seals a continuation.
        // 仅原pickup-yield的bound source；捕获不授权移动，未确认Cancel的receipt不能消费。
        internal bool TryRetireYieldForOwnerHold(TrafficDecision parentSource,TrafficDecision originalSource,object actorLife,TrafficNativeStamp stamp,
            Vector3 actualStopPose,out MovementRecoveryReceipt receipt)
        {
            receipt=null;
            if(originalSource==null || originalSource.Kind!=TrafficDecisionKind.Granted || originalSource.AcquiredSequence<=0 ||
                originalSource.Intent.IntentId!="pickup-yield" || !Finite(actualStopPose) ||
                !LiveLeg(originalSource.Intent,actorLife,stamp,out var entry) || !entry.Leg.Bound || !entry.Leg.Stamp.Equals(stamp) ||
                entry.Leg.Suspended || entry.PendingHandoff!=null || entry.ExplicitRecoveryUsed || !entry.JourneyParking ||
                !ReferenceEquals(entry.JourneyPermit,originalSource) || entry.JourneyRequest==null ||
                entry.JourneyRequest.ResourceId!="pickup/"+originalSource.Intent.ActorId ||
                entry.JourneyRequest.NativeStamp.RequestId!=0)return false;
            var bodies=snapshot.Bodies.Where(body=>body.Owner.ActorId==originalSource.Intent.ActorId).ToArray();
            if(bodies.Length!=1 || !bodies[0].Enabled || !bodies[0].ActorActive || !bodies[0].Owner.Equals(originalSource.Intent) ||
                !ReferenceEquals(bodies[0].ActorLife,actorLife) || bodies[0].Position!=actualStopPose)return false;
            if(parentSource==null || parentSource.Intent.IntentId!="employee-pickup-journey" || !Current(parentSource.Intent) ||
                !entries.TryGetValue(parentSource.Intent,out var parent) || !LiveEntry(parent) || !ReferenceEquals(parent.Decision,parentSource) ||
                parentSource.Kind!=TrafficDecisionKind.PlanningFailed || parentSource.FailureDisposition!=TrafficFailureDisposition.AlternateAllowed ||
                parentSource.AcquiredSequence!=0 || parent.Active || parent.Prepared || parent.Queued || parent.Retired || parent.Discarded ||
                parent.Leg!=null || parent.PendingHandoff!=null || parent.ExplicitRecoveryUsed || parent.YieldOwnerWaitStarted ||
                parent.Request.ResourceId!="pickup/"+parent.Key.ActorId || !Positive(parent.FiniteLimit))return false;
            var started=parent.FrozenOwners!=null || parent.AdmissionWaitStarted || parent.CounterAdvanceReadinessWaitStarted;
            // These flags are monotonic on a live parent. Zero with any history is spent, never a fresh allowance.
            if(started?(!Positive(parent.WaitRemaining) || parent.WaitRemaining>parent.FiniteLimit):parent.WaitRemaining!=0)return false;
            var remaining=started?parent.WaitRemaining:Mathf.Min(waitSeconds,parent.FiniteLimit);
            if(!Positive(remaining))return false;
            parent.YieldOwnerWaitStarted=parent.YieldOwnerWaitActive=true;parent.YieldOwnerWaitChild=entry.Key;
            parent.WaitRemaining=remaining;SyncWork(parent);
            entry.YieldOwnerParent=parent.Key;entry.YieldOwnerOriginalChild=entry.Key;
            CaptureRecovery(entry,entry.JourneyRequest,actorLife,out receipt);
            recoveryReceipts[receipt].PendingOwnerYieldCancellation=true;
            return true;
        }
        internal bool ConfirmYieldOwnerHoldCancellation(MovementRecoveryReceipt receipt,bool cancelled)
        {
            if(receipt==null || !recoveryReceipts.TryGetValue(receipt,out var saved) || !saved.PendingOwnerYieldCancellation)return false;
            if(!cancelled || closed || committing || !Current(saved.Original) || !YieldOwnerParentCurrent(saved,out _) ||
                !entries.TryGetValue(saved.Original,out var original) || !original.Retired)
            {recoveryReceipts.Remove(receipt);return false;}
            saved.PendingOwnerYieldCancellation=false;return true;
        }
        private bool YieldOwnerParentCurrent(RecoveryRecord saved,out Entry parent)
        {
            parent=null;
            return saved.YieldOwnerParent.HasValue && saved.YieldOwnerOriginalChild.HasValue &&
                entries.TryGetValue(saved.YieldOwnerParent.Value,out parent) && LiveEntry(parent) && parent.YieldOwnerWaitActive &&
                parent.YieldOwnerWaitStarted && parent.YieldOwnerWaitChild.HasValue &&
                parent.YieldOwnerWaitChild.Value.Equals(saved.YieldOwnerOriginalChild.Value) && Positive(parent.WaitRemaining) &&
                parent.Decision.FailureDisposition!=TrafficFailureDisposition.Terminal;
        }
        private void StopYieldOwnerWait(Entry parent)
        {
            if(!parent.YieldOwnerWaitActive)return;
            parent.YieldOwnerWaitActive=false;
            foreach(var receipt in recoveryReceipts.Where(pair=>pair.Value.YieldOwnerParent.HasValue &&
                pair.Value.YieldOwnerParent.Value.Equals(parent.Key)).Select(pair=>pair.Key).ToArray())recoveryReceipts.Remove(receipt);
            foreach(var child in entries.Values.Where(value=>!value.Retired && value.YieldOwnerParent.HasValue &&
                value.YieldOwnerParent.Value.Equals(parent.Key)).ToArray())Retire(child.Key);
        }
        private void CompleteYieldOwnerWait(Entry child)
        {
            if(!child.YieldOwnerParent.HasValue || !child.YieldOwnerOriginalChild.HasValue || child.PendingHandoff==null ||
                child.PendingHandoff.CheckpointKind!=TrafficPhaseMilestoneKind.Arrived || child.Leg==null || !child.Leg.Completed ||
                !entries.TryGetValue(child.YieldOwnerParent.Value,out var parent) || !LiveEntry(parent) || !parent.YieldOwnerWaitActive ||
                !parent.YieldOwnerWaitChild.HasValue || !parent.YieldOwnerWaitChild.Value.Equals(child.YieldOwnerOriginalChild.Value) ||
                parent.Decision.FailureDisposition==TrafficFailureDisposition.Terminal)return;
            parent.YieldOwnerWaitActive=false;SyncWork(parent);
        }
        private void CaptureRecovery(Entry entry,TrafficRequest original,object actorLife,out MovementRecoveryReceipt receipt,bool notifyRetirement=true)
        {
            var intent=original.Intent;
            var record=new RecoveryRecord{Original=intent,Actor=new WeakReference<object>(actorLife),Resource=original.ResourceId,
                Goal=original.Target,Radius=original.Radius,SafetyMargin=original.SafetyMargin,FiniteSeconds=original.FiniteSeconds,
                IndependentlyProgressing=original.IndependentlyProgressing,FirstFrame=entry.FirstFrame,Queries=entry.Queries,
                YieldOwnerParent=entry.YieldOwnerParent,YieldOwnerOriginalChild=entry.YieldOwnerOriginalChild,YieldOwnerWaitStarted=entry.YieldOwnerWaitStarted,
                RequestSequence=entry.RequestSequence,AcquiredSequence=entry.AcquiredSequence,Suspensions=entry.Suspensions,
                CandidateCount=entry.CandidateCount,CurrentIndex=entry.CurrentIndex,AttemptedIndices=new HashSet<int>(entry.AttemptedIndices),
                EverCanonicalAttempt=entry.EverCanonicalAttempt,
                WaitRemaining=entry.WaitRemaining,FrozenOwners=entry.FrozenOwners==null?null:new HashSet<TrafficIntentKey>(entry.FrozenOwners),
                FrozenResources=entry.FrozenResources?.ToDictionary(pair=>pair.Key,pair=>new HashSet<string>(pair.Value))};
            // An unacquired preparation held no movement right: retirement cannot announce physical/resource release.
            // 从未取得权利的 preparation 不广播释放；真实 Native 的既有退休语义不变。
            if(notifyRetirement)Retire(intent);else RetireEntry(entry,false);
            receipt=new MovementRecoveryReceipt();recoveryReceipts.Add(receipt,record);
        }
        internal bool TryBeginExplicitRecovery(MovementRecoveryReceipt receipt, TrafficRequest replacement,
            object actorLife, out TrafficDecision decision)
        {
            decision=null;
            if(closed || paused || committing || receipt==null || replacement==null || !recoveryReceipts.TryGetValue(receipt,out var saved) ||
                saved.PendingOwnerYieldCancellation || !Current(saved.Original) || !Current(replacement.Intent) || !saved.Actor.TryGetTarget(out var originalActor) ||
                actorLife==null || !ReferenceEquals(actorLife,originalActor))return false;
            if(saved.YieldOwnerParent.HasValue && !YieldOwnerParentCurrent(saved,out _)){recoveryReceipts.Remove(receipt);return false;}
            var key=replacement.Intent;var old=saved.Original;
            if(key.ActorId!=old.ActorId || key.IntentId!=old.IntentId || key.Context!=old.Context || key.Generation<=old.Generation ||
                entries.ContainsKey(key) || replacement.NativeStamp.RequestId!=0 || !ReferenceEquals(replacement.Snapshot,snapshot) ||
                replacement.ResourceId!=saved.Resource || replacement.Target!=saved.Goal || replacement.Radius!=saved.Radius ||
                replacement.SafetyMargin!=saved.SafetyMargin || replacement.FiniteSeconds!=saved.FiniteSeconds ||
                replacement.IndependentlyProgressing!=saved.IndependentlyProgressing || !Valid(replacement))return false;
            var body=snapshot.Bodies.FirstOrDefault(b=>b.Owner.ActorId==key.ActorId);
            if(body==null || !body.Enabled || !body.ActorActive || !Current(body.Owner) || !ReferenceEquals(body.ActorLife,actorLife) ||
                body.Position!=replacement.Start || saved.Queries>=queriesPerIntent || saved.FrozenOwners!=null && saved.WaitRemaining<=0)return false;
            // Preserve frozen eligibility/history, but re-evaluate CURRENT blockers from the new actual snapshot.
            // 老阻挡已释放不再阻路；剩余预算与可等待owner集合不因新 transport generation 重置。
            var next=new Entry(key,++entryBirthSequence){Request=WithPrimaryFrame(replacement,saved.FirstFrame),FirstFrame=saved.FirstFrame,Queries=saved.Queries,RequestSequence=saved.RequestSequence,
                AcquiredSequence=saved.AcquiredSequence,Suspensions=saved.Suspensions,WaitRemaining=saved.WaitRemaining,
                CandidateCount=saved.CandidateCount,CurrentIndex=saved.CurrentIndex,FiniteLimit=saved.FiniteSeconds,
                EverCanonicalAttempt=saved.EverCanonicalAttempt,YieldOwnerParent=saved.YieldOwnerParent,YieldOwnerOriginalChild=saved.YieldOwnerOriginalChild,YieldOwnerWaitStarted=saved.YieldOwnerWaitStarted,
                FrozenOwners=saved.FrozenOwners,FrozenResources=saved.FrozenResources,Queued=true,ExplicitRecoveryUsed=true};
            next.AttemptedIndices.UnionWith(saved.AttemptedIndices);
            entries.Add(key,next);recoveryReceipts.Remove(receipt);
            decision=Decide(next,TrafficDecisionKind.DeferredByBudget,"explicit-recovery-ready-batch-pending");return true;
        }
        // Actual issued stop without creating a paid recovery nonce; the registered body remains in the snapshot.
        // 只消费绑定的原始结果与 ActorLife，不把退休当作身体消失。
        internal bool TryRetireStoppedLeg(TrafficIntentKey intent,object actorLife,TrafficNativeStamp stamp,MovementResult originalResult)
        {
            var stopped=originalResult.Status==MovementStatus.Failed && originalResult.Reason!=NavigationFailure.None ||
                originalResult.Status==MovementStatus.Recovered && originalResult.OriginalFailure!=NavigationFailure.None ||
                originalResult.Status==MovementStatus.Cancelled;
            if(!stopped || originalResult.ActorId!=intent.ActorId || originalResult.RequestId!=stamp.RequestId ||
                !Finite(originalResult.FinalPosition) || !LiveLeg(intent,actorLife,stamp,out var entry) ||
                !entry.Leg.Bound || !entry.Leg.Stamp.Equals(stamp) || entry.PendingHandoff!=null)return false;
            var body=snapshot.Bodies.First(b=>b.Owner.ActorId==intent.ActorId);
            if(body.Position!=originalResult.FinalPosition)return false;
            RetireEntry(entry,true);NotifyChanged(body.Owner,"body");return true;
        }
        internal void DiscardRecovery(MovementRecoveryReceipt receipt)
        { if(receipt!=null)recoveryReceipts.Remove(receipt); }
        // Only an actual same-revision Native cancellation may suspend a bound leg once in this phase.
        // 不消费 Arrived/LegEnds；原结果、实例和 epoch/revision 必须保持同一所有权。
        public bool SuspendLeg(TrafficIntentKey intent,object actorLife,TrafficNativeStamp stamp,
            MovementResult originalResult,int observedLayoutRevision)
        {
            if(closed || committing || !Current(intent) || observedLayoutRevision!=layoutRevision ||
                originalResult.Status!=MovementStatus.LayoutChanged || originalResult.Reason!=NavigationFailure.LayoutUnavailable ||
                originalResult.OriginalFailure!=NavigationFailure.None || originalResult.RecoveryFailure!=NavigationFailure.None || originalResult.RetryCount!=0 ||
                originalResult.ActorId!=intent.ActorId || originalResult.RequestId!=stamp.RequestId || stamp.RequestId<=0 ||
                !entries.TryGetValue(intent,out var entry) || entry.Retired || !entry.Active || entry.Suspensions!=0 ||
                entry.Leg==null || !entry.Leg.Committed || !entry.Leg.Bound || entry.Leg.Completed || entry.Leg.Suspended ||
                !entry.Leg.Stamp.Equals(stamp) || entry.Leg.Generation!=stamp.LegGeneration ||
                actorLife==null || !ReferenceEquals(entry.Leg.ActorLife,actorLife))return false;
            var body=snapshot.Bodies.FirstOrDefault(b=>b.Owner.ActorId==intent.ActorId);
            // NavMesh suspension may make this same physical body unavailable, never a different actor life.
            if(body==null || !Current(body.Owner) || !ReferenceEquals(body.ActorLife,actorLife))return false;
            entry.Suspensions++;entry.Leg.Suspended=true;
            entry.Active=entry.Prepared=entry.Queued=false;entry.CurrentPath=Array.Empty<Vector3>();
            Decide(entry,TrafficDecisionKind.DeferredByBudget,"native-leg-suspended");
            NotifyChanged(intent,entry.Request.ResourceId);return true;
        }
        private bool LiveLeg(TrafficIntentKey intent, object actorLife, TrafficNativeStamp stamp, out Entry entry)
        {
            entry = null;
            return !closed && !committing && Current(intent) && entries.TryGetValue(intent, out entry) &&
                !entry.Retired && entry.Active && entry.Decision.FailureDisposition!=TrafficFailureDisposition.Terminal &&
                entry.Leg != null && entry.Leg.Committed && !entry.Leg.Completed && stamp.RequestId > 0 &&
                entry.Leg.Generation == stamp.LegGeneration && LegBodyCurrent(entry, actorLife) &&
                (!entry.Leg.Bound || entry.Leg.Stamp.Equals(stamp));
        }
        public bool CompleteLeg(TrafficIntentKey intent, object actorLife, TrafficNativeStamp stamp,
            Vector3 actualPosition)
        {
            if (!LiveLeg(intent, actorLife, stamp, out var entry) || !Finite(actualPosition) ||
                Vector3.Distance(actualPosition, entry.Request.Target) > entry.Leg.ArrivalTolerance) return false;
            entry.Leg.Stamp = stamp; entry.Leg.Bound = true; entry.Leg.Completed = true;
            entry.Active = false; entry.CurrentPath = Array.Empty<Vector3>(); entry.Journey = entry.Leg.Suffix.ToArray();SyncWork(entry);
            var body=snapshot.Bodies.First(b=>b.Owner.ActorId==intent.ActorId);
            NotifyChanged(intent, entry.Request.ResourceId);NotifyChanged(body.Owner,"body");return true;
        }
        public bool CompleteLegAndBeginHandoff(TrafficIntentKey predecessor,object actorLife,TrafficNativeStamp stamp,
            MovementResult originalArrival,Func<ITrafficPhaseMilestone> confirmCanonicalMilestone,out TrafficPhaseHandoff pending)
        {
            pending=null;
            if(confirmCanonicalMilestone==null || originalArrival.Status!=MovementStatus.Arrived ||
                originalArrival.Reason!=NavigationFailure.None || originalArrival.ActorId!=predecessor.ActorId ||
                originalArrival.RequestId!=stamp.RequestId || !Finite(originalArrival.FinalPosition) ||
                !LiveLeg(predecessor,actorLife,stamp,out var entry) || !entry.Leg.Bound || entry.PendingHandoff!=null ||
                entry.Leg.Index!=entry.Leg.Proof.Waypoints.Count-1 || SelectedProofs(entry).LastOrDefault()!=entry.Leg.Proof ||
                Vector3.Distance(originalArrival.FinalPosition,entry.Request.Target)>entry.Leg.ArrivalTolerance)return false;
            // Actual Native completion is irreversible, even if the following canonical milestone fails.
            // 原 stamped Arrived 先消费，但不 Notify；业务失败不能伪造 undo 或抹去实际身体。
            entry.Leg.Completed=true;entry.Active=false;entry.CurrentPath=Array.Empty<Vector3>();entry.Journey=entry.Leg.Suffix.ToArray();
            ITrafficPhaseMilestone milestone=null;var failure="canonical-phase-milestone-invalid";
            TrafficParkingFact[] originals=null,currentClaims=null;TrafficPhaseMilestoneKind? checkpointKind=null;
            var completedBody=snapshot.Bodies.First(b=>b.Owner.ActorId==predecessor.ActorId).Owner;
            completingHandoff=entry;completingHandoffBody=completedBody;committing=true;SyncWork(entry);
            try
            {
                milestone=confirmCanonicalMilestone();
                if(!ReadHandoffMilestone(entry,milestone,null,out originals,out currentClaims))milestone=null;
                else checkpointKind=milestone.Kind; // Capture the authenticated original checkpoint inside the guarded window.
            }
            catch(Exception error){failure="canonical-phase-milestone-exception:"+error.GetType().Name;milestone=null;}
            finally{committing=false;completingHandoff=null;SyncWork(entry);}
            if(!LiveEntry(entry))return false;
            if(milestone==null){AbortCompletedPhase(entry,failure);return false;}
            var eligible=HandoffResourceScope(entry,originals,currentClaims);
            if(eligible.Count==0)
            {
                // Accepted completion with no retained obligation is ordinary completion, not a failed gap.
                // 全部真实释放：保留已完成事实，正常通知相关旧等待者；不创建handle、不伪造失败。
                foreach(var claim in originals)NotifyChanged(claim.Owner,claim.ResourceId);
                NotifyChanged(predecessor,entry.Request.ResourceId);NotifyChanged(completedBody,"body");return true;
            }
            var originalClaim=milestone.OriginalClaim;
            if(!LiveEntry(entry))return false;
            var state=new HandoffState{Handle=new TrafficPhaseHandoff(this,predecessor),OriginalClaim=originalClaim,
                OriginalClaims=originals,Source=entry.JourneyPermit,ActorLife=actorLife,Remaining=entry.Request.FiniteSeconds,CheckpointKind=checkpointKind};
            foreach(var waiter in OrderedWorkEntries.Where(HandoffWaiterCurrent).ToArray())
            {
                var owned=new HashSet<string>(CurrentHandoffResources(waiter,predecessor).Where(eligible.Contains));
                if(owned.Count==0)continue;
                state.Dependencies.Add(waiter,owned);
                if(waiter.WaitingBlockers.All(b=>b.Owner.Equals(predecessor) && owned.Contains(b.ResourceId)))
                {
                    waiter.Queued=false;wakeups.Remove(waiter.Request.Intent);
                    // The still-blocked deferred recheck now waits on this gap; its original Tick budget keeps running.
                    if(waiter.Decision.Kind==TrafficDecisionKind.DeferredByBudget)
                        Decide(waiter,TrafficDecisionKind.WaitingForTraffic,"bounded-phase-handoff",waiter.WaitingBlockers);
                    SyncWork(waiter);
                }
            }
            if(state.Dependencies.Count>0)state.Remaining=Mathf.Min(state.Remaining,state.Dependencies.Keys.Max(e=>e.WaitRemaining));
            entry.PendingHandoff=state;entry.Prepared=entry.Queued=false;
            Decide(entry,TrafficDecisionKind.DeferredByBudget,"canonical-phase-handoff-pending");
            if(!Positive(state.Remaining)){AbortHandoff(entry,"original-budget-exhausted");return false;}
            var released=new HashSet<string>(originals.Where((claim,index)=>currentClaims[index]==null).Select(claim=>claim.ResourceId));
            foreach(var waiter in OrderedWorkEntries.Where(w=>CurrentHandoffResources(w,predecessor).Any(released.Contains)).ToArray())
                QueueChangedWaiter(waiter); // Only observed release edges; no new handoff eligibility or global wake policy.
            pending=state.Handle;CompleteYieldOwnerWait(entry);return true;
        }
        // Bind ordering to a real completed canonical phase, not to a generic sleeping route.
        // 初次认证exact source；之后用原Entry/descriptor继续，不要求早期Deferred对象永远current。
        internal bool TryBindPhaseHandoffSuccessorPreparation(TrafficPhaseHandoff pending,TrafficDecision successorSource,
            TrafficRequest successorRequest,ITrafficPhaseMilestone collectedMilestone)
        {
            if(committing || successorSource==null || !Valid(successorRequest) ||
                !TryGetHandoff(pending,out var previous,out var state) || state.InspectingPreparation ||
                !entries.TryGetValue(successorSource.Intent,out var next) || !LiveEntry(next) ||
                !ReferenceEquals(next.Decision,successorSource) || next.AcquiredSequence!=0 || next.Leg!=null ||
                next.JourneyPermit!=null || next.JourneyRequest!=null || next.Discarded || next.Parking ||
                next.Decision.FailureDisposition!=TrafficFailureDisposition.None || next.PendingHandoff!=null ||
                successorRequest.NativeStamp.RequestId!=0 || successorRequest.NativeStamp.LegGeneration!=0 ||
                !ReferenceEquals(successorRequest.Snapshot,snapshot) || !SnapshotCurrent(next.Request) ||
                !SameSuccessorDescriptor(next.Request,successorRequest) ||
                state.PreparedSuccessor!=null && !ReferenceEquals(state.PreparedSuccessor,next))return false;
            if(!ReadSuccessorPreparation(previous,state,next,successorRequest,successorSource,collectedMilestone,out _))return false;
            state.PreparedSuccessor=next;state.PreparationRequest=successorRequest;
            state.PreparationSource=successorSource;state.PreparationMilestone=collectedMilestone;return true;
        }
        private static bool SameSuccessorDescriptor(TrafficRequest a,TrafficRequest b)=>a!=null && b!=null &&
            a.Intent.Equals(b.Intent) && a.RequestedFrame==b.RequestedFrame && a.Start==b.Start && a.Target==b.Target &&
            a.ResourceId==b.ResourceId && a.Radius==b.Radius && a.SafetyMargin==b.SafetyMargin &&
            a.NativeStamp.Equals(b.NativeStamp) && a.FiniteSeconds==b.FiniteSeconds && a.IndependentlyProgressing==b.IndependentlyProgressing &&
            a.ProvenFailure==b.ProvenFailure && a.PlanPrimaryRoute==b.PlanPrimaryRoute &&
            a.SelectedJourney.SequenceEqual(b.SelectedJourney) && SameRequirements(a.Requirements,b.Requirements);
        private bool ReadSuccessorPreparation(Entry previous,HandoffState state,Entry next,TrafficRequest descriptor,
            TrafficDecision initialSource,ITrafficPhaseMilestone milestone,out TrafficParkingFact held,bool requireNavigation=true)
        {
            held=null;
            if(committing || state==null || state.InspectingPreparation || !LiveEntry(previous) || !LiveEntry(next) ||
                !ReferenceEquals(previous.PendingHandoff,state) || !Positive(state.Remaining) || !HandoffBodyCurrent(previous,state) ||
                previous.Leg==null || !previous.Leg.Bound || !previous.Leg.Completed || next.Key.Equals(previous.Key) ||
                next.Key.ActorId!=previous.Key.ActorId || !Current(next.Key) || next.Parking || next.Discarded ||
                next.Decision.FailureDisposition!=TrafficFailureDisposition.None || descriptor==null || initialSource==null || milestone==null ||
                descriptor.RequestedFrame!=next.FirstFrame || descriptor.RequestedFrame<previous.FirstFrame || !descriptor.Intent.Equals(next.Key) ||
                !SameSuccessorDescriptor(descriptor,next.JourneyRequest??next.Request) || !ValidSnapshot())return false;
            var body=snapshot.Bodies.Where(b=>b.Owner.ActorId==next.Key.ActorId).ToArray();
            if(body.Length!=1 || !body[0].Owner.Equals(previous.Key) || !body[0].ActorActive ||
                !ReferenceEquals(body[0].ActorLife,state.ActorLife) || body[0].Position!=descriptor.Start ||
                body[0].Radius!=descriptor.Radius || body[0].SafetyMargin!=descriptor.SafetyMargin ||
                Vector3.Distance(body[0].Position,previous.Request.Target)>previous.Leg.ArrivalTolerance)return false;
            var checkPrevious=ClaimInspectionGuard(previous);var checkNext=ClaimInspectionGuard(next);
            var oldEntry=state.PreparedSuccessor;var oldRequest=state.PreparationRequest;
            var oldSource=state.PreparationSource;var oldMilestone=state.PreparationMilestone;
            bool CurrentRead()=>checkPrevious() && checkNext() && ReferenceEquals(previous.PendingHandoff,state) &&
                ReferenceEquals(state.PreparedSuccessor,oldEntry) && ReferenceEquals(state.PreparationRequest,oldRequest) &&
                ReferenceEquals(state.PreparationSource,oldSource) && ReferenceEquals(state.PreparationMilestone,oldMilestone) && Positive(state.Remaining);
            state.InspectingPreparation=true;
            try
            {
                var collected=milestone.Kind==TrafficPhaseMilestoneKind.Collected;if(!CurrentRead() || !collected)return false;
                var authorized=milestone.AuthorizesSuccessor(next.Key,initialSource,next.FirstFrame);
                if(!CurrentRead() || !authorized || !ReadHandoffMilestone(previous,milestone,state,out var originals,out var claims,requireNavigation:requireNavigation) || !CurrentRead())return false;
                var index=Array.FindIndex(originals,claim=>ReferenceEquals(claim,state.OriginalClaim));
                if(index<0 || claims[index]==null ||
                    claims.Where((claim,i)=>claim!=null && (claim.Position!=originals[i].Position || claim.Radius!=originals[i].Radius ||
                        claim.SafetyMargin!=originals[i].SafetyMargin)).Any())return false;
                var actual=snapshot.Parking.Where(p=>p.Owner.ActorId==next.Key.ActorId).ToArray();var retained=claims.Where(p=>p!=null).ToArray();
                if(actual.Length!=retained.Length || retained.Any(claim=>actual.Count(p=>SameClaimFacts(p,claim))!=1) ||
                    actual.Any(claim=>retained.Count(p=>SameClaimFacts(p,claim))!=1) || !CurrentRead())return false;
                held=claims[index];return true;
            }
            catch{return false;}
            finally{state.InspectingPreparation=false;}
        }
        private bool ReadBoundSuccessor(Entry previous,HandoffState state,out TrafficParkingFact held)
            =>ReadSuccessorPreparation(previous,state,state.PreparedSuccessor,state.PreparationRequest,state.PreparationSource,state.PreparationMilestone,out held,requireNavigation:false);
        private static void ClearSuccessorPreparation(HandoffState state)
        {state.PreparedSuccessor=null;state.PreparationRequest=null;state.PreparationSource=null;state.PreparationMilestone=null;}
        private void AbortPreparationsUsing(Entry successor,string reason)
        {
            foreach(var previous in OrderedWorkEntries.Where(e=>e.PendingHandoff!=null && ReferenceEquals(e.PendingHandoff.PreparedSuccessor,successor)).ToArray())
                if(LiveEntry(previous) && previous.PendingHandoff!=null && ReferenceEquals(previous.PendingHandoff.PreparedSuccessor,successor))AbortHandoff(previous,reason);
        }
        private void InspectSuccessorPreparations()
        {
            foreach(var previous in OrderedWorkEntries.Where(e=>e.PendingHandoff?.PreparedSuccessor!=null && !e.PendingHandoff.InspectingPreparation).ToArray())
            {
                var state=previous.PendingHandoff;
                if(state!=null && !ReadBoundSuccessor(previous,state,out _) && LiveEntry(previous) && ReferenceEquals(previous.PendingHandoff,state))
                    AbortHandoff(previous,"successor-preparation-invalid");
            }
        }
        private bool CanCommitSuccessorPreparation(Entry next)
        {
            var inspect=ClaimInspectionGuard(next);
            foreach(var previous in OrderedWorkEntries.Where(e=>e.PendingHandoff!=null && ReferenceEquals(e.PendingHandoff.PreparedSuccessor,next)).ToArray())
            {
                var state=previous.PendingHandoff;
                if(state==null)return false;
                if(!ReadBoundSuccessor(previous,state,out _))
                {if(LiveEntry(previous) && ReferenceEquals(previous.PendingHandoff,state))AbortHandoff(previous,"successor-preparation-invalid");return false;}
                if(!inspect())return false;
                if(next.Leg!=null && !state.CommittingPreparedSuccessor)return false; // Only the original strict handoff may connect it.
            }
            return inspect();
        }
        private static bool EarlierPreparation(Entry earlier,Entry later)
        {
            var order=earlier.FirstFrame.CompareTo(later.FirstFrame);if(order!=0)return order<0;
            order=StringComparer.Ordinal.Compare(earlier.Key.ActorId,later.Key.ActorId);if(order!=0)return order<0;
            order=StringComparer.Ordinal.Compare(earlier.Key.IntentId,later.Key.IntentId);if(order!=0)return order<0;
            order=earlier.Key.Generation.CompareTo(later.Key.Generation);return order!=0?order<0:earlier.BirthOrder<later.BirthOrder;
        }
        // A real canonical predecessor still holds this ordering until the first strict joint commit.
        // Primary取得后不丢原proof；strict未发腿沿已选current+suffix，绝不重新分配路径或Native许可。
        // An original declared capability is ordering evidence only while the real canonical phase is still held.
        // 仅保同一前驱已查询的nonselected证据；绝不把capability写入后继Journey/Leg或授予Native运动权。
        private IReadOnlyList<Vector3> CanonicalPredecessorCapabilityPath(Entry previous,HandoffState state,Entry next)
        {
            if(previous==null || state==null || next==null || !ReferenceEquals(previous.PendingHandoff,state) ||
                !ReferenceEquals(state.PreparedSuccessor,next) || next.Leg!=null || next.JourneyPermit!=null || next.JourneyRequest!=null)return null;
            var source=state.Source;var original=previous.JourneyRequest;var descriptor=state.PreparationRequest;var completed=previous.Leg;
            if(source==null || original==null || descriptor==null || previous.Request==null || completed==null ||
                !ReferenceEquals(source,previous.JourneyPermit) || source.Kind!=TrafficDecisionKind.Granted ||
                source.AcquiredSequence<=0 || source.AcquiredSequence!=previous.AcquiredSequence ||
                !source.Intent.Equals(previous.Key) || !original.Intent.Equals(previous.Key) || !descriptor.Intent.Equals(next.Key) ||
                source.Intent.Session!=next.Key.Session || source.Intent.Epoch!=next.Key.Epoch ||
                source.Intent.ActorId!=next.Key.ActorId || source.Intent.LayoutRevision!=next.Key.LayoutRevision ||
                !completed.Bound || !completed.Completed || !Positive(completed.ArrivalTolerance) ||
                !original.Target.Equals(previous.Request.Target) || !descriptor.NativeStamp.Equals(default(TrafficNativeStamp)))return null;
            var declarations=original.Requirements.Where(requirement=>!requirement.SelectedJourney &&
                requirement.Start.Equals(original.Target) && requirement.Goal.Equals(descriptor.Target)).ToArray();
            if(declarations.Length!=1)return null; // Ambiguous/unknown roles cannot manufacture priority.
            var declared=declarations[0];var members=source.ProofRoutes.Where(proof=>proof.ResourceId==declared.ResourceId).ToArray();
            if(members.Length!=1)return null;
            var proof=members[0];var tolerance=completed.ArrivalTolerance;
            if(proof.Waypoints.Count==0 || !proof.Waypoints[proof.Waypoints.Count-1].Equals(declared.Goal) ||
                Vector3.Distance(descriptor.Start,declared.Start)>tolerance ||
                !CompatibleNativeEndpoints(proof.ExpandedPath,declared.Start,declared.Goal,tolerance))return null;
            // This geometry is consumed solely by the existing conflict test; ordinary new-path proof is still mandatory.
            return GuardCurrentPath(descriptor.Start,proof.ExpandedPath,descriptor.Target);
        }
        private IReadOnlyList<Vector3> CanonicalSuccessorOrderingPath(Entry previous,HandoffState state,Entry next)
        {
            if(next.AcquiredSequence==0)
            {
                if(next.Leg!=null || next.JourneyPermit!=null)return null;
                var path=next.Jobs.Count>0?next.Jobs[0].ExpandedPath:null;
                return path!=null && path.Length>=2?path:CanonicalPredecessorCapabilityPath(previous,state,next);
            }
            var permit=next.JourneyPermit;var source=next.JourneyRequest;
            if(permit==null || source==null || next.JourneyParking || permit.Kind!=TrafficDecisionKind.Granted ||
                permit.AcquiredSequence!=next.AcquiredSequence || !permit.Intent.Equals(next.Key) ||
                !source.NativeStamp.Equals(default(TrafficNativeStamp)))return null;
            var primary=SelectedProofs(next).FirstOrDefault();
            if(primary==null || primary.ResourceId!=source.ResourceId)return null;
            var leg=next.Leg;
            if(leg==null)return primary.ExpandedPath;
            if(!ReferenceEquals(leg.Proof,primary) || leg.Index!=0 || leg.Generation<1 || leg.ActorLife==null ||
                leg.Committed || leg.Bound || leg.Completed || leg.Suspended || leg.ReprepareAllowed ||
                !leg.Stamp.Equals(default(TrafficNativeStamp)) || next.Request.NativeStamp.RequestId!=0 ||
                next.Request.NativeStamp.LegGeneration!=leg.Generation || next.Request.ResourceId!=primary.ResourceId)return null;
            return next.Journey;
        }
        private bool WaitForPreparedCanonicalSuccessor(Entry later,IReadOnlyList<TrafficBlocker> existingBlockers=null)
        {
            var outer=!LiveEntry(later)?"not-live":later.Parking?"parking":later.Discarded?"discarded":later.AcquiredSequence!=0?"already-acquired":
                later.Leg!=null?"has-leg":later.JourneyPermit!=null?"has-journey-permit":later.FrozenOwners!=null?"already-frozen":later.CurrentPath.Length<2?"no-current-path":null;
            ObserveCanonicalOrdering(later,null,null,null,"entry",outer??"PASS");
            if(outer!=null)return false;
            var inspect=ClaimInspectionGuard(later);
            var predecessors=OrderedWorkEntries.Where(e=>e.PendingHandoff?.PreparedSuccessor!=null).ToArray();
            if(predecessors.Length==0)ObserveCanonicalOrdering(later,null,null,null,"bindings","NONE");
            foreach(var previous in predecessors)
            {
                var state=previous.PendingHandoff;var next=state?.PreparedSuccessor;
                var nextReject=next==null?"missing-next":ReferenceEquals(next,later)?"same-entry":!LiveEntry(next)?"next-not-live":!EarlierPreparation(next,later)?"not-earlier":null;
                ObserveCanonicalOrdering(later,previous,state,next,"state",nextReject??"PASS");
                if(nextReject!=null)continue;
                // Preserve ordinary rejection/waiting for every other live body, canonical target or route.
                // 仅替换对这个真实已绑定后继的route依赖；不能借canonical等待掩盖其他实际blocker。
                var unrelated=existingBlockers!=null && existingBlockers.Any(blocker=>!blocker.Owner.Equals(next.Key) || blocker.Proof!="current-conflict-segment");
                if(unrelated){ObserveCanonicalOrdering(later,previous,state,next,"existing-blockers","not-exclusive-successor-route");continue;}
                var ownedPath=CanonicalSuccessorOrderingPath(previous,state,next);
                if(ownedPath==null || ownedPath.Count<2)
                {ObserveCanonicalOrdering(later,previous,state,next,"owned-path",ownedPath==null?"NULL":"too-short",true,ownedPath);continue;}
                var conflict=PathsConflict(ownedPath,later.CurrentPath,PairClearance(next.Request,later.Request));
                ObserveCanonicalOrdering(later,previous,state,next,"conflict",conflict?"PASS":"no-conflict",true,ownedPath,conflict);
                if(!conflict)continue;
                var bound=ReadBoundSuccessor(previous,state,out var held);
                ObserveCanonicalOrdering(later,previous,state,next,"read-bound",bound?"PASS":"invalid",true,ownedPath,conflict,bound);
                if(!bound)
                {
                    if(LiveEntry(previous) && ReferenceEquals(previous.PendingHandoff,state))AbortHandoff(previous,"successor-preparation-invalid");
                    var stillCurrent=inspect();ObserveCanonicalOrdering(later,previous,state,next,"after-abort",stillCurrent?"PASS":"later-source-changed",true,ownedPath,conflict,bound);
                    if(!stillCurrent)return true;continue;
                }
                var sourceCurrent=inspect();ObserveCanonicalOrdering(later,previous,state,next,"after-read",sourceCurrent?"PASS":"later-source-changed",true,ownedPath,conflict,bound);
                if(!sourceCurrent)return true;
                var blockers=new[]{new TrafficBlocker(previous.Key,held.ResourceId,"bounded-canonical-successor-order")};
                var cycle=DependsOn(next.Key,later.Key,new HashSet<TrafficIntentKey>());
                if(cycle)
                {ObserveCanonicalOrdering(later,previous,state,next,"cycle","dependency-cycle",true,ownedPath,conflict,bound);Fail(later,TrafficDecisionKind.PlanningFailed,"successor-preparation-cycle",blockers,preparation:true);return true;}
                var remaining=Mathf.Min(waitSeconds,Mathf.Min(later.FiniteLimit,state.Remaining));
                if(later.YieldOwnerWaitStarted)remaining=Mathf.Min(later.WaitRemaining,remaining);
                var budget=Positive(remaining);
                if(!budget){ObserveCanonicalOrdering(later,previous,state,next,"budget","wait-budget-exhausted",true,ownedPath,conflict,bound);Fail(later,TrafficDecisionKind.PlanningFailed,"wait-budget-exhausted",blockers,preparation:true);return true;}
                ObserveCanonicalOrdering(later,previous,state,next,"install","bounded-phase-handoff",true,ownedPath,conflict,bound);
                // Atomically install this NEW bounded edge before any dependent callback; never append to an existing Frozen set.
                later.FrozenOwners=new HashSet<TrafficIntentKey>{previous.Key};
                later.FrozenResources=new Dictionary<TrafficIntentKey,HashSet<string>>{{previous.Key,new HashSet<string>{held.ResourceId}}};
                later.WaitRemaining=remaining;later.WaitingBlockers=blockers;
                state.Dependencies.Add(later,new HashSet<string>{held.ResourceId});
                later.Prepared=later.Active=later.Queued=false;later.CurrentPath=Array.Empty<Vector3>();
                Decide(later,TrafficDecisionKind.WaitingForTraffic,"bounded-phase-handoff",blockers);InvalidateDependentWaiters(later);return true;
            }
            var current=inspect();ObserveCanonicalOrdering(later,null,null,null,"exit",current?"no-matching-bound-order":"later-source-changed");return !current;
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void ObserveCanonicalOrdering(Entry later,Entry previous,HandoffState state,Entry next,string stage,string guard,
            bool pathEvaluated=false,IReadOnlyList<Vector3> ownedPath=null,bool? conflict=null,bool? bound=null)
        {
#if UNITY_EDITOR
            var observer=CanonicalOrderingObserver;
            if(observer==null || later==null || later.Key.IntentId!="pickup-paid-movement")return;
            try
            {
                string Identity(object value)=>value==null?"NULL":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
                string Key(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
                string Path(IEnumerable<Vector3> values)=>values==null?"NULL":"["+string.Join(";",values.Select(point=>point.ToString("F9")))+"]";
                string Original(bool? value)=>value.HasValue?value.Value.ToString():"NOT_EVALUATED";
                var request=next?.Request;var leg=next?.Leg;
                observer("CANONICAL_ORDER_ORIGINAL laterKey="+Key(later.Key)+" stage="+stage+" guard="+guard+
                    " later="+Identity(later)+" first="+later.FirstFrame+" birth="+later.BirthOrder+" seq="+later.RequestSequence+" acq="+later.AcquiredSequence+
                    " laterDecision="+Identity(later.Decision)+" laterKind="+later.Decision?.Kind+" laterQueued="+later.Queued+" laterFrozen="+(later.FrozenOwners==null?"NULL":string.Join(";",later.FrozenOwners.Select(Key)))+
                    " laterPath="+Path(later.CurrentPath)+" previous="+Identity(previous)+" previousKey="+(previous==null?"NULL":Key(previous.Key))+
                    " state="+Identity(state)+" actualPending="+Identity(previous?.PendingHandoff)+" remaining="+state?.Remaining+" handle="+Identity(state?.Handle)+
                    " preparedSuccessor="+Identity(state?.PreparedSuccessor)+" preparationRequest="+Identity(state?.PreparationRequest)+
                    " preparationSource="+Identity(state?.PreparationSource)+" preparationMilestone="+Identity(state?.PreparationMilestone)+
                    " next="+Identity(next)+" nextKey="+(next==null?"NULL":Key(next.Key))+" nextFirst="+next?.FirstFrame+" nextBirth="+next?.BirthOrder+" nextSeq="+next?.RequestSequence+" nextAcq="+next?.AcquiredSequence+
                    " nextKind="+next?.Decision?.Kind+" nextQueued="+next?.Queued+" jobs="+next?.Jobs.Count+" jobIndex="+next?.JobIndex+" nextQueries="+next?.Queries+
                    " nextRequest="+Identity(request)+" nextSnapshot="+Identity(request?.Snapshot)+" requestVersion="+request?.Snapshot?.Version+" requestBodyVersion="+request?.Snapshot?.BodyVersion+
                    " nextStart="+(request==null?"NULL":request.Start.ToString("F9"))+" nextTarget="+(request==null?"NULL":request.Target.ToString("F9"))+
                    " journeyPermit="+Identity(next?.JourneyPermit)+" journeyRequest="+Identity(next?.JourneyRequest)+" leg="+Identity(leg)+
                    " legCommitted="+leg?.Committed+" legBound="+leg?.Bound+" legCompleted="+leg?.Completed+" legNeedsQuery="+leg?.NeedsQuery+
                    " ownedPath="+(pathEvaluated?Path(ownedPath):"NOT_EVALUATED")+" originalConflict="+Original(conflict)+" originalReadBound="+Original(bound)+
                    " currentSnapshot="+Identity(snapshot)+" version="+snapshot?.Version+" bodyVersion="+snapshot?.BodyVersion);
            }
            catch{CanonicalOrderingObservationErrors++;}
#endif
        }

        // Reads exact existing handoff validity without consuming or renewing its lifetime.
        // 只读原handle/source/ActorLife/剩余期；true不授予后继写入或运动权限。
        internal bool IsCurrentPhaseHandoff(TrafficPhaseHandoff pending,TrafficDecision predecessorSource,object actorLife)
            =>predecessorSource!=null && actorLife!=null && TryGetHandoff(pending,out var entry,out var state) &&
                ReferenceEquals(state.Source,predecessorSource) && ReferenceEquals(state.ActorLife,actorLife) &&
                predecessorSource.Intent.Equals(pending.Predecessor) && Positive(state.Remaining) && HandoffBodyCurrent(entry,state);
        public bool TryCommitPhaseHandoff(TrafficPhaseHandoff pending,TrafficDecision successor,
            ITrafficPhaseMilestone currentMilestone,IPreparedTrafficMutation mutation=null,Action publish=null)
        {
            if(committing || !TryGetHandoff(pending,out var previous,out var state) || successor==null ||
                !HandoffBodyCurrent(previous,state) || !Positive(state.Remaining) || !Current(successor.Intent) ||
                successor.Intent.Equals(pending.Predecessor) || successor.Intent.ActorId!=pending.Predecessor.ActorId ||
                !entries.TryGetValue(successor.Intent,out var next) || next.Retired || !next.Prepared || next.Active ||
                !ReferenceEquals(next.Decision,successor) || successor.Kind!=TrafficDecisionKind.Granted ||
                next.Leg==null || next.Leg.Committed || next.Leg.Bound || next.Leg.Completed || next.Leg.Suspended ||
                !ReferenceEquals(next.Leg.ActorLife,state.ActorLife) || next.Leg.Generation<=0 ||
                next.Parking || !next.Request.IndependentlyProgressing || !Positive(next.Request.FiniteSeconds) ||
                next.CurrentPath.Length<2 || !SnapshotCurrent(next.Request) || !LegBodyCurrent(next,state.ActorLife))return false;
            var body=snapshot.Bodies.First(b=>b.Owner.ActorId==successor.Intent.ActorId);
            if(body.Position!=next.Request.Start || !IndependentPathClear(next) || Rights(next).Any(other=>
                other.Parking && NearPath(other.Request.Target,next.CurrentPath,PairClearance(next.Request,other.Request)) ||
                !other.Parking && PathsConflict(next.CurrentPath,other.CurrentPath,PairClearance(next.Request,other.Request))))return false;
            TrafficParkingFact[] originals,currentClaims,authenticatedClaims;
            committing=true;
            try
            {
                if(!ReadHandoffMilestone(previous,currentMilestone,state,out originals,out currentClaims) || next.JourneyPermit==null ||
                    !next.JourneyPermit.Intent.Equals(successor.Intent) ||
                    !currentMilestone.AuthorizesSuccessor(successor.Intent,next.JourneyPermit,next.FirstFrame) ||
                    !LiveEntry(previous) || !LiveEntry(next) ||
                    !ReadHandoffSuccessorClaims(next.Request,previous.Key,currentMilestone,originals,currentClaims,out authenticatedClaims))return false;
            }
            catch{return false;}
            finally{committing=false;}
            if(!LiveEntry(previous) || !LiveEntry(next) || !ReferenceEquals(previous.PendingHandoff,state) ||
                !next.Prepared || !ReferenceEquals(next.Decision,successor))return false;
            var eligible=HandoffResourceScope(previous,originals,currentClaims);
            var dependencyResources=state.Dependencies.ToDictionary(pair=>pair.Key,pair=>new HashSet<string>(pair.Value));
            var waiters=dependencyResources.Keys.Where(w=>
                CurrentHandoffResources(w,pending.Predecessor).Any(dependencyResources[w].Contains)).ToArray();
            if(waiters.Any(w=>w.Key.Equals(successor.Intent) ||
                DependsOn(successor.Intent,w.Key,new HashSet<TrafficIntentKey>())))return false;
            // The strict current leg is committed before any dependency or external publication changes.
            // source/parking 的整程许可不够；只有可立即发出的当前腿才能接续原释放义务。
            state.CommittingPreparedSuccessor=true;
            try{if(!TryCommit(successor,mutation))return false;}
            finally{state.CommittingPreparedSuccessor=false;}
            if(!LiveEntry(previous) || !LiveEntry(next) || !ReferenceEquals(previous.PendingHandoff,state))return false;
            foreach(var waiter in waiters)
            {
                var resources=new HashSet<string>(CurrentHandoffResources(waiter,pending.Predecessor)
                    .Where(r=>dependencyResources[waiter].Contains(r) && eligible.Contains(r)));
                if(resources.Count==0)continue;
                var oldResources=waiter.FrozenResources[pending.Predecessor];
                oldResources.ExceptWith(resources);
                if(oldResources.Count==0){waiter.FrozenResources.Remove(pending.Predecessor);waiter.FrozenOwners.Remove(pending.Predecessor);}
                if(!waiter.FrozenResources.TryGetValue(successor.Intent,out var nextResources))
                    waiter.FrozenResources.Add(successor.Intent,nextResources=new HashSet<string>());
                nextResources.UnionWith(resources);waiter.FrozenOwners.Add(successor.Intent);
                waiter.WaitingBlockers=waiter.WaitingBlockers.Select(b=>b.Owner.Equals(pending.Predecessor) && resources.Contains(b.ResourceId)
                    ?new TrafficBlocker(successor.Intent,b.ResourceId,b.Proof):b).ToArray();
                var decision=waiter.Decision;
                waiter.Decision=new TrafficDecision(decision.Intent,decision.Kind,decision.Reason,decision.Permit,decision.RequestSequence,
                    decision.AcquiredSequence,decision.Blockers.Select(b=>b.Owner.Equals(pending.Predecessor) && resources.Contains(b.ResourceId)
                        ?new TrafficBlocker(successor.Intent,b.ResourceId,b.Proof):b),decision.Route,decision.ProofRoutes);
                SyncWork(waiter);
            }
            // 原完成checkpoint已放行的当前腿仍保留原冻结池；仅把已认证交接资源的owner接续，不改Native权限。
            // Carry only captured eligibility after its one completed-body wake; never wake or re-grant a moving waiter.
            foreach(var waiter in dependencyResources.Keys.Where(waiter=>state.CompletedBodyWakeups.Contains(waiter) &&
                LiveEntry(waiter) && Current(waiter.Key) && !waiter.Discarded && waiter.WaitRemaining>0 &&
                waiter.FrozenOwners!=null && waiter.FrozenOwners.Contains(pending.Predecessor) &&
                waiter.FrozenResources!=null && waiter.FrozenResources.ContainsKey(pending.Predecessor) &&
                (waiter.Active && !waiter.Prepared && waiter.Decision.Kind==TrafficDecisionKind.Granted && waiter.AcquiredSequence>0 &&
                    waiter.JourneyPermit!=null && waiter.JourneyPermit.Intent.Equals(waiter.Key) &&
                    waiter.Leg!=null && waiter.Leg.Committed && waiter.Leg.Bound && !waiter.Leg.Completed && !waiter.Leg.Suspended &&
                    LegBodyCurrent(waiter,waiter.Leg.ActorLife,requireNavigation:false) ||
                 HeldCapturedCounterReadinessCurrent(waiter,previous,state))).ToArray())
            {
                var resources=dependencyResources[waiter].Where(eligible.Contains)
                    .Where(waiter.FrozenResources[pending.Predecessor].Contains).ToArray();
                if(resources.Length==0)continue;
                var oldResources=waiter.FrozenResources[pending.Predecessor];oldResources.ExceptWith(resources);
                if(oldResources.Count==0){waiter.FrozenResources.Remove(pending.Predecessor);waiter.FrozenOwners.Remove(pending.Predecessor);}
                if(!waiter.FrozenResources.TryGetValue(successor.Intent,out var nextResources))
                    waiter.FrozenResources.Add(successor.Intent,nextResources=new HashSet<string>());
                nextResources.UnionWith(resources);waiter.FrozenOwners.Add(successor.Intent);SyncWork(waiter);
            }
            ClearSuccessorPreparation(state);previous.PendingHandoff=null;RetireEntry(previous,false);
            var committedRequest=next.Request;var committedLeg=next.Leg;
            LastPublicationException=null;try{publish?.Invoke();}catch(Exception error){LastPublicationException=error;}
            if(LastPublicationException==null && ReferenceEquals(next.Decision,successor))
                RebindPublishedHandoffSnapshot(next,committedRequest,committedLeg,pending.Predecessor,authenticatedClaims);
            if(LiveEntry(next) && Current(successor.Intent))
                foreach(var waiter in waiters.Where(w=>CurrentHandoffResources(w,successor.Intent).Any(dependencyResources[w].Contains) ||
                    CurrentHandoffResources(w,pending.Predecessor).Any(r=>dependencyResources[w].Contains(r) && !eligible.Contains(r))))
                    QueueChangedWaiter(waiter);
            return true;
        }
        // An original unacquired Counter candidate may have cleared its transient blocker cache at a prior prepared Grant.
        // 仅在本次真实strict handoff接续已经捕获且仍在原Frozen中的资源；不建WAIT、不加预算、不唤醒候选。
        private bool HeldCapturedCounterReadinessCurrent(Entry waiter,Entry previous,HandoffState state)
        {
            if(state.CheckpointKind!=TrafficPhaseMilestoneKind.Arrived || !ReferenceEquals(previous.PendingHandoff,state) ||
                !waiter.Queued || waiter.Prepared || waiter.Active || waiter.WaitingBlockers.Count!=0 ||
                waiter.Decision.Kind!=TrafficDecisionKind.DeferredByBudget || waiter.Decision.Reason!="counter-advance-handoff-readiness-held" ||
                waiter.Decision.FailureDisposition!=TrafficFailureDisposition.None || !waiter.CounterAdvanceReadinessWaitStarted ||
                waiter.CounterAdvanceReadiness==null || !state.CompletedBodyWakeups.Contains(waiter) ||
                !state.Dependencies.TryGetValue(waiter,out var captured) || captured.Any(value=>value!="counter/"+previous.Key.ActorId && value!="body") ||
                !captured.Contains("counter/"+previous.Key.ActorId) || !waiter.FrozenResources.TryGetValue(previous.Key,out var frozen) ||
                !frozen.Contains("counter/"+previous.Key.ActorId) || captured.Any(value=>!frozen.Contains(value)) || !CounterAdvancePreparationCurrent(waiter))return false;
            var readiness=waiter.CounterAdvanceReadiness;var inspect=ClaimInspectionGuard(waiter);
            try
            {
                var current=readiness.IsCurrent();
                return current && inspect() && ReferenceEquals(waiter.CounterAdvanceReadiness,readiness) &&
                    CounterAdvancePreparationCurrent(waiter) && inspect() && ReferenceEquals(previous.PendingHandoff,state) &&
                    state.Dependencies.TryGetValue(waiter,out var actual) && ReferenceEquals(actual,captured);
            }
            catch(Exception){return false;}
        }
        private void RebindPublishedHandoffSnapshot(Entry entry,TrafficRequest request,LegState leg,
            TrafficIntentKey predecessor,IReadOnlyList<TrafficParkingFact> authenticatedClaims)
        {
            var published=snapshot;var before=request.Snapshot;var successor=request.Intent;
            if(closed || !Current(predecessor) || !Current(successor) || entry.Retired || !entry.Active ||
                !entries.TryGetValue(successor,out var registered) || !ReferenceEquals(registered,entry) ||
                !ReferenceEquals(entry.Request,request) || !ReferenceEquals(entry.Leg,leg) || !leg.Committed ||
                leg.Bound || leg.Completed || leg.Suspended || ReferenceEquals(before,published) ||
                published.Version<before.Version || published.BodyVersion<before.BodyVersion ||
                published.Version==before.Version && published.BodyVersion==before.BodyVersion ||
                before.Bodies.Count!=published.Bodies.Count || before.Parking.Count!=published.Parking.Count ||
                published.Bodies.Any(b=>b==null) || published.Parking.Any(p=>p==null) ||
                before.Bodies.Count(b=>b.Owner.ActorId==predecessor.ActorId)!=1 ||
                published.Bodies.Count(b=>b.Owner.ActorId==successor.ActorId)!=1)return;
            var own=before.Bodies.First(b=>b.Owner.ActorId==predecessor.ActorId);
            if(!own.Owner.Equals(predecessor) || !own.Enabled || !own.ActorActive || !ReferenceEquals(own.ActorLife,leg.ActorLife) ||
                authenticatedClaims.Count!=before.Parking.Count(p=>p.Owner.Equals(predecessor)))return;
            // Only the publisher's actual owner-tag transition may update saved completion provenance.
            // 逐项比较真实快照；位置、业务版本或其他身体变化都不能借换名刷新发腿授权。
            var bodies=published.Bodies.ToList();
            foreach(var oldBody in before.Bodies)
            {
                var owner=oldBody.Owner.Equals(predecessor)?successor:oldBody.Owner;
                var match=bodies.FindIndex(body=>body.Owner.Equals(owner) && ReferenceEquals(body.ActorLife,oldBody.ActorLife) &&
                    body.Position.Equals(oldBody.Position) && body.Radius==oldBody.Radius && body.SafetyMargin==oldBody.SafetyMargin &&
                    body.Enabled==oldBody.Enabled && body.ActorActive==oldBody.ActorActive && body.IndependentlyProgressing==oldBody.IndependentlyProgressing &&
                    body.RemainingSeconds==oldBody.RemainingSeconds);
                if(match<0)return;bodies.RemoveAt(match);
            }
            var parking=published.Parking.ToList();
            foreach(var oldClaim in before.Parking)
            {
                var owned=oldClaim.Owner.Equals(predecessor);
                if(owned && !authenticatedClaims.Any(claim=>ReferenceEquals(claim,oldClaim)))return;
                var owner=owned?successor:oldClaim.Owner;
                var match=parking.FindIndex(claim=>claim.Owner.Equals(owner) && ReferenceEquals(claim.CanonicalOwner,oldClaim.CanonicalOwner) &&
                    ReferenceEquals(claim.BusinessOwner,oldClaim.BusinessOwner) && ReferenceEquals(claim.ReleaseObligation,oldClaim.ReleaseObligation) &&
                    claim.OwnerVersion==oldClaim.OwnerVersion && claim.ResourceId==oldClaim.ResourceId &&
                    claim.Position.Equals(oldClaim.Position) && claim.Radius==oldClaim.Radius && claim.SafetyMargin==oldClaim.SafetyMargin);
                if(match<0)return;parking.RemoveAt(match);
            }
            entry.Request=new TrafficRequest(request.Intent,request.RequestedFrame,published,request.Start,request.Target,request.Radius,
                request.ResourceId,request.SelectedJourney,request.Requirements,request.NativeStamp,request.IndependentlyProgressing,
                request.FiniteSeconds,request.ProvenFailure,request.SafetyMargin,request.PlanPrimaryRoute);
        }
        public void AbortPhaseHandoff(TrafficPhaseHandoff pending,string reason)
        {if(!committing && TryGetHandoff(pending,out var entry,out _))AbortHandoff(entry,reason??"cancelled");}
        private bool TryGetHandoff(TrafficPhaseHandoff handle,out Entry entry,out HandoffState state)
        {
            entry=null;state=null;
            if(closed || handle==null || !ReferenceEquals(handle.Coordinator,this) || !Current(handle.Predecessor) ||
                !entries.TryGetValue(handle.Predecessor,out entry) || entry.Retired || entry.PendingHandoff==null ||
                !ReferenceEquals(entry.PendingHandoff.Handle,handle))return false;
            state=entry.PendingHandoff;return true;
        }
        private static bool HandoffWaiterCurrent(Entry entry)=>entry!=null && !entry.Retired && entry.Request!=null && !entry.Active && !entry.Prepared &&
            entry.FrozenOwners!=null && entry.FrozenResources!=null && entry.WaitRemaining>0 && entry.WaitingBlockers.Count>0 &&
            (entry.Decision.Kind==TrafficDecisionKind.WaitingForTraffic || entry.Decision.Kind==TrafficDecisionKind.DeferredByBudget) &&
            (entry.Leg==null || !entry.Leg.Committed && !entry.Leg.Bound && !entry.Leg.Completed && !entry.Leg.Suspended);
        private static IEnumerable<string> CurrentHandoffResources(Entry waiter,TrafficIntentKey owner)
        {
            if(!HandoffWaiterCurrent(waiter) || !waiter.FrozenOwners.Contains(owner) ||
                !waiter.FrozenResources.TryGetValue(owner,out var eligible))yield break;
            foreach(var blocker in waiter.WaitingBlockers)
                if(blocker.Owner.Equals(owner) && eligible.Contains(blocker.ResourceId))yield return blocker.ResourceId;
        }
        private bool HandoffBodyCurrent(Entry entry,HandoffState state)=>LiveEntry(entry) && state!=null && Current(entry.Key) &&
            entry.Leg!=null && entry.Leg.Completed && ReferenceEquals(entry.JourneyPermit,state.Source) && LegBodyCurrent(entry,state.ActorLife,requireNavigation:false);
        private static bool SameOwnedClaim(TrafficParkingFact a,TrafficParkingFact b)=>a!=null && b!=null && a.Owner.Equals(b.Owner) &&
            ReferenceEquals(a.CanonicalOwner,b.CanonicalOwner) && ReferenceEquals(a.BusinessOwner,b.BusinessOwner) &&
            ReferenceEquals(a.ReleaseObligation,b.ReleaseObligation) && a.ResourceId==b.ResourceId;
        private static bool SameClaimFacts(TrafficParkingFact a,TrafficParkingFact b)=>SameOwnedClaim(a,b) &&
            a.OwnerVersion==b.OwnerVersion && a.Position.Equals(b.Position) && a.Radius==b.Radius && a.SafetyMargin==b.SafetyMargin;
        private static bool ValidCurrentClaim(TrafficParkingFact original,TrafficParkingFact current)=>SameOwnedClaim(original,current) &&
            current.OwnerVersion>=original.OwnerVersion && Finite(current.Position) && Positive(current.Radius) &&
            Finite(current.SafetyMargin) && current.SafetyMargin>=0;
        private bool ReadHandoffMilestone(Entry entry,ITrafficPhaseMilestone milestone,HandoffState state,
            out TrafficParkingFact[] originals,out TrafficParkingFact[] currentClaims,bool requireNavigation=true)
        {
            originals=null;currentClaims=null;
            if(!LiveEntry(entry) || entry.Leg==null || milestone==null)return false;
            var request=entry.Request;var leg=entry.Leg;var source=entry.JourneyPermit;
            if(!Current(entry.Key) || !milestone.IsCurrent || !Enum.IsDefined(typeof(TrafficPhaseMilestoneKind),milestone.Kind) ||
                !ReferenceEquals(milestone.SourcePermit,source) || milestone.OriginalClaim==null ||
                !ReferenceEquals(milestone.ActorLife,leg.ActorLife) || !LegBodyCurrent(entry,milestone.ActorLife,requireNavigation:requireNavigation) ||
                !request.Snapshot.Bodies.Any(b=>b.Owner.ActorId==entry.Key.ActorId && ReferenceEquals(b.ActorLife,milestone.ActorLife)) ||
                state!=null && (!ReferenceEquals(state.OriginalClaim,milestone.OriginalClaim) || !ReferenceEquals(state.Source,milestone.SourcePermit)))return false;
            if(!LiveEntry(entry) || !ReferenceEquals(entry.Request,request) || !ReferenceEquals(entry.Leg,leg))return false;
            // The saved strict snapshot defines the complete fixed set; a caller cannot omit a secondary claim.
            // 原strict快照决定集合；null仅代表已验证的Released，Invalid永远不能混作释放。
            var saved=request.Snapshot.Parking.Where(p=>p.Owner.Equals(entry.Key)).ToArray();
            originals=state==null?saved:state.OriginalClaims;
            if(originals==null || originals.Length==0 || originals.Length!=saved.Length ||
                originals.Any(original=>!saved.Any(p=>ReferenceEquals(p,original))))return false;
            currentClaims=new TrafficParkingFact[originals.Length];var resources=new HashSet<string>();var primary=-1;
            for(var index=0;index<originals.Length;index++)
            {
                var original=originals[index];
                if(original.CanonicalOwner==null || original.BusinessOwner==null || original.ReleaseObligation==null ||
                    string.IsNullOrEmpty(original.ResourceId) || !resources.Add(original.ResourceId) ||
                    !ReferenceEquals(original.BusinessOwner,milestone.BusinessOwner) || !ReferenceEquals(original.ReleaseObligation,milestone.ReleaseObligation))return false;
                if(ReferenceEquals(original,milestone.OriginalClaim))primary=index;
                var status=milestone.ReadClaim(original,out var current);
                if(!LiveEntry(entry) || !ReferenceEquals(entry.Request,request) || !ReferenceEquals(entry.Leg,leg))return false;
                if(status==TrafficPhaseClaimState.Released){if(current!=null)return false;}
                else if(status==TrafficPhaseClaimState.Held)
                { if(!ValidCurrentClaim(original,current))return false;currentClaims[index]=current; }
                else return false;
            }
            var valid=primary>=0 && (currentClaims[primary]==null?milestone.CurrentClaim==null:SameClaimFacts(currentClaims[primary],milestone.CurrentClaim));
            return valid && LiveEntry(entry) && ReferenceEquals(entry.Request,request) && ReferenceEquals(entry.Leg,leg);
        }
        private static HashSet<string> HandoffResourceScope(Entry entry,TrafficParkingFact[] originals,TrafficParkingFact[] currentClaims)
        {
            var resources=new HashSet<string>(currentClaims.Where(claim=>claim!=null).Select(claim=>claim.ResourceId));
            if(resources.Count==0)return resources;
            // Existing body/current-leg edges attach once to actual retained obligations, never to a new claim.
            // 同名原canonical资源若已释放，不能借movement别名把它重新算成Held。
            foreach(var resource in new[]{"body",entry.Request.ResourceId})
                if(!originals.Any(claim=>claim.ResourceId==resource) || resources.Contains(resource))resources.Add(resource);
            return resources;
        }
        private bool ReadHandoffSuccessorClaims(TrafficRequest request,TrafficIntentKey predecessor,ITrafficPhaseMilestone milestone,
            TrafficParkingFact[] originals,TrafficParkingFact[] currentClaims,out TrafficParkingFact[] authenticated)
        {
            authenticated=null;
            if(request==null || !entries.TryGetValue(request.Intent,out var entry) || !LiveEntry(entry) || !ReferenceEquals(entry.Request,request))return false;
            authenticated=request.Snapshot.Parking.Where(p=>p.Owner.Equals(predecessor)).ToArray();
            if(request.Snapshot.Parking.Any(p=>p.Owner.ActorId==predecessor.ActorId && !p.Owner.Equals(predecessor)))return false;
            foreach(var current in currentClaims.Where(claim=>claim!=null))
                if(!authenticated.Any(claim=>SameClaimFacts(claim,current)))return false;
            var resources=new HashSet<string>();
            foreach(var captured in authenticated)
            {
                if(captured.CanonicalOwner==null || string.IsNullOrEmpty(captured.ResourceId) || !resources.Add(captured.ResourceId) ||
                    !ReferenceEquals(captured.BusinessOwner,milestone.BusinessOwner) || !ReferenceEquals(captured.ReleaseObligation,milestone.ReleaseObligation))return false;
                var index=Array.FindIndex(originals,original=>original.ResourceId==captured.ResourceId);
                if(index>=0)
                { if(currentClaims[index]==null || !SameClaimFacts(captured,currentClaims[index]))return false; }
                else if(milestone.ReadClaim(captured,out var current)!=TrafficPhaseClaimState.Held ||
                    !ValidCurrentClaim(captured,current) || !SameClaimFacts(captured,current))return false;
                if(!LiveEntry(entry) || !ReferenceEquals(entry.Request,request))return false;
            }
            return LiveEntry(entry) && ReferenceEquals(entry.Request,request);
        }
        private bool HoldsHandoffDependency(Entry waiter,TrafficIntentKey owner,string resource)
        {
            if(!CurrentHandoffResources(waiter,owner).Contains(resource) ||
                !entries.TryGetValue(owner,out var entry) || entry.PendingHandoff==null)return false;
            var state=entry.PendingHandoff;
            return HandoffBodyCurrent(entry,state) && state.Remaining>0 && state.Dependencies.TryGetValue(waiter,out var resources) && resources.Contains(resource);
        }
        private void AbortCompletedPhase(Entry entry,string reason)
        {
            if(!LiveEntry(entry))return;
            var dependents=OrderedWorkEntries.Where(e=>CurrentHandoffResources(e,entry.Key).Any()).ToArray();
            var successor=entry.PendingHandoff?.PreparedSuccessor;
            if(entry.PendingHandoff!=null)ClearSuccessorPreparation(entry.PendingHandoff);
            entry.PendingHandoff=null;Fail(entry,TrafficDecisionKind.PlanningFailed,reason);
            foreach(var waiter in dependents)
                if(HandoffWaiterCurrent(waiter)){wakeups.Remove(waiter.Request.Intent);Fail(waiter,TrafficDecisionKind.PlanningFailed,reason,waiter.Decision.Blockers);}
            if(LiveEntry(successor) && successor.Decision.FailureDisposition==TrafficFailureDisposition.None &&
                (successor.Leg==null || !successor.Leg.Bound))Fail(successor,TrafficDecisionKind.PlanningFailed,reason);
        }
        private void AbortHandoff(Entry entry,string reason)
        {if(entry.PendingHandoff!=null)AbortCompletedPhase(entry,"phase-handoff-aborted:"+reason);}

        public void Retire(TrafficIntentKey intent)
        {
            if (!entries.TryGetValue(intent, out var entry) || entry.Retired) return;
            if(entry.PendingHandoff!=null)AbortHandoff(entry,"predecessor-retired");
            RetireEntry(entry,true);
        }
        private void RetireEntry(Entry entry,bool notify,TrafficDecision retainedFailure=null)
        {
            if(!LiveEntry(entry))return;
            var intent=entry.Key;var resource=entry.Request.ResourceId;
            StopYieldOwnerWait(entry);
            AbortPreparationsUsing(entry,"successor-retired");if(!LiveEntry(entry))return;
            // Only a live movement owner's loss changes that same registered body's finite-progress proof.
            // provisional / parking 退休不能凭 ActorId 推导另一个完整 body owner 的事件。
            var bodyProofChanged=entry.Active && !entry.Parking && snapshot.Bodies.Any(b=>b.Owner.Equals(intent));
            entry.ProducerPreviewHeld=false;entry.DeliveryAlternateDebtGap=false;entry.Retired = true; entry.Active = false; entry.Prepared = false; entry.Queued = false;SyncWork(entry);
            entry.CurrentPath = entry.Journey = Array.Empty<Vector3>(); entry.Jobs.Clear();ClearNativeChunks(entry); wakeups.Remove(intent);
            foreach(var owner in OrderedWorkEntries.Where(e=>!e.Retired && e.PendingHandoff!=null).ToArray())
            {
                owner.PendingHandoff.Dependencies.Remove(entry);
                owner.PendingHandoff.CompletedBodyWakeups.Remove(entry);
            }
            entry.Decision=retainedFailure??new TrafficDecision(intent,TrafficDecisionKind.Cancelled,"intent-retired",
                requestSequence:entry.RequestSequence,acquiredSequence:entry.AcquiredSequence,failureDisposition:TrafficFailureDisposition.Terminal);
            entry.WaitingBlockers=Array.Empty<TrafficBlocker>();
            // Release coordinator ownership before notification/publication; physical snapshot facts stay intact.
            // 只释放真正 Retired 的重引用；Terminal/Discarded 和真实身体/停车位不会在这里被推断为释放。
            entry.Request=null;entry.PreparationBody=null;entry.PreparationClaims=Array.Empty<TrafficParkingFact>();
            entry.Leg=null;entry.JourneyRequest=null;entry.JourneyPermit=null;entry.PendingHandoff=null;
            entry.FrozenSelectedRouteEligibility=null;
            entry.UnpaidEligibility=null;entry.UnpaidEligibilityHeld=false;entry.UnpaidEligibilityFacts=null;
            entry.UnpaidEligibilityRights=Array.Empty<UnpaidEligibilityRight>();entry.UnpaidEligibilityAttempts.Clear();
            entry.UnpaidEligibilitySpatialRefusal=null;entry.UnpaidEligibilitySpatialActors.Clear();
            entry.UnpaidEligibilityNativeActivity=Array.Empty<UnpaidNativeActivity>();
            entry.AdmissionReadiness=null;entry.AdmissionHeld=false;entry.AdmissionFirstLeg=Array.Empty<Vector3>();
            entry.CounterAdvanceReadiness=null;entry.CounterAdvanceReadinessWaitStarted=false;
            if(notify)
            {
                NotifyChanged(intent, resource);
                if(bodyProofChanged)NotifyChanged(intent,"body");
            }
        }
        // Only the post-completion body checkpoint can recheck a transient physical conflict.
        // Canonical proof with the same resource name is not a release; the ordinary full batch still decides safety.
        private bool TryConsumeCompletedTransientBodyCheckpoint(Entry waiter,TrafficIntentKey owner)
        {
            if(!LiveEntry(waiter) || !Current(waiter.Key) || !HandoffWaiterCurrent(waiter) || waiter.Discarded ||
                waiter.Decision.FailureDisposition!=TrafficFailureDisposition.None || waiter.Request.NativeStamp.RequestId!=0 ||
                waiter.Leg!=null && (waiter.Leg.ReprepareAllowed || waiter.Leg.Stamp.RequestId!=0 || waiter.Leg.Stamp.LegGeneration!=0 ||
                    waiter.Request.NativeStamp.LegGeneration!=waiter.Leg.Generation) ||
                !entries.TryGetValue(owner,out var completed) || !LiveEntry(completed))return false;
            // An unacquired parking proposal may still declare its selected-home proof role; it owns no journey yet.
            // 只新增未获权停车候选；保留合法往返proof声明，不把已取得journey/issued腿借名重证。
            if(waiter.Parking && (!waiter.Request.PlanPrimaryRoute || waiter.Request.SelectedJourney.Count!=0 ||
                waiter.AcquiredSequence!=0 || waiter.Leg!=null || waiter.JourneyPermit!=null || waiter.JourneyRequest!=null || waiter.Journey.Length!=0 ||
                waiter.PendingHandoff!=null || waiter.EverCanonicalAttempt || waiter.ExplicitRecoveryUsed || waiter.LegacyLegCompleted ||
                waiter.ContinuationAttempted || waiter.Request.NativeStamp.LegGeneration!=0 || !PreparationIdentityCurrent(waiter)))return false;
            var state=completed.PendingHandoff;var leg=completed.Leg;var world=snapshot;
            if(state==null || state.CompletedBodyWakeups.Contains(waiter) || !Positive(state.Remaining) ||
                completed.Decision.FailureDisposition!=TrafficFailureDisposition.None || !HandoffBodyCurrent(completed,state) ||
                leg==null || !leg.Committed || !leg.Bound || !leg.Completed || leg.Suspended || leg.Stamp.RequestId<=0 ||
                leg.Stamp.LegGeneration!=leg.Generation || !ReferenceEquals(leg.ActorLife,state.ActorLife) ||
                !state.Dependencies.TryGetValue(waiter,out var eligible) || !waiter.FrozenOwners.Contains(owner) ||
                !waiter.FrozenResources.TryGetValue(owner,out var frozen))return false;
            var checkCompleted=ClaimInspectionGuard(completed);var checkWaiter=ClaimInspectionGuard(waiter);
            var body=world.Bodies.FirstOrDefault(value=>value.Owner.Equals(owner));
            if(body==null || !body.Enabled || !body.ActorActive || !ReferenceEquals(body.ActorLife,state.ActorLife) ||
                Vector3.Distance(body.Position,completed.Request.Target)>leg.ArrivalTolerance)return false;
            // Preserve generic canonical parking gaps and the existing selected-parking checkpoint.
            // 原普通停车gap不变；完整outward+selected continuation仅多一次全量重证，不释放Held claim。
            var transient=waiter.Parking
                ?waiter.WaitingBlockers.Any(block=>block.Owner.Equals(owner) && eligible.Contains(block.ResourceId) && frozen.Contains(block.ResourceId) &&
                    block.ResourceId==completed.Request.ResourceId && block.Proof=="selected-journey-parking-protection")
                :waiter.WaitingBlockers.Any(block=>block.Owner.Equals(owner) && eligible.Contains(block.ResourceId) && frozen.Contains(block.ResourceId) &&
                (block.ResourceId=="body" && (block.Proof=="live-body-checkpoint" || block.Proof=="current-leg-body") ||
                    block.ResourceId==completed.Request.ResourceId && block.Proof=="current-conflict-segment"));
            // JobIndex reaches Count only after every original primary/requirement query completed.
            // 未获权proposal的真实选定后续已算完；这里只排队，当前停车/身体仍由正常batch重验。
            if(!transient && waiter.Parking && waiter.Request.Requirements.Any(requirement=>requirement.SelectedJourney) &&
                waiter.Jobs.Count==waiter.Request.Requirements.Count+1 && waiter.JobIndex==waiter.Jobs.Count &&
                waiter.Jobs.All(job=>job.ExpandedPath.Length>=1 && job.Waypoints.Length>0 &&
                    job.LegEnds.Length==job.Waypoints.Length && job.LegEnds[job.LegEnds.Length-1]==job.ExpandedPath.Length-1))
                transient=waiter.WaitingBlockers.Any(block=>block.Owner.Equals(owner) && eligible.Contains(block.ResourceId) && frozen.Contains(block.ResourceId) &&
                    (block.ResourceId=="body" && (block.Proof=="live-body-checkpoint" || block.Proof=="current-leg-body") ||
                        block.ResourceId==completed.Request.ResourceId && block.Proof=="current-conflict-segment"));
            if(!transient || !checkCompleted() || !checkWaiter() || !ReferenceEquals(snapshot,world) ||
                !ReferenceEquals(completed.PendingHandoff,state))return false;
            return state.CompletedBodyWakeups.Add(waiter);
        }
        public void NotifyChanged(TrafficIntentKey owner, string resourceId)
        {
            if (closed) return;
            if(completingHandoff!=null && (owner.Equals(completingHandoff.Key) ||
                owner.Equals(completingHandoffBody) && resourceId=="body"))return;
            var bodyHandoff=resourceId=="body" && entries.TryGetValue(owner,out var completedOwner)?completedOwner.PendingHandoff:null;
            foreach (var entry in OrderedWorkEntries.Where(e => !e.Retired && !e.ProducerPreviewHeld && e.Decision != null &&
                (e.Decision.Kind == TrafficDecisionKind.WaitingForTraffic || e.Decision.Kind==TrafficDecisionKind.DeferredByBudget) &&
                (e.WaitingBlockers.Any(b => b.Owner.Equals(owner) && b.ResourceId == resourceId) ||
                    bodyHandoff!=null && bodyHandoff.Dependencies.ContainsKey(e))).ToArray())
            {
                var completedBodyCheckpoint=resourceId=="body" && TryConsumeCompletedTransientBodyCheckpoint(entry,owner);
                if(!completedBodyCheckpoint && HoldsHandoffDependency(entry,owner,resourceId))continue;
                // A body checkpoint also covers its old current-leg alias, never unrelated canonical resources.
                if(!completedBodyCheckpoint && !entry.WaitingBlockers.Any(block=>block.Owner.Equals(owner) && block.ResourceId==resourceId))continue;
                QueueChangedWaiter(entry);
            }
        }
        private void QueueChangedWaiter(Entry entry)
        {
            if(!LiveEntry(entry) || entry.ProducerPreviewHeld || entry.AdmissionHeld)return;
            var r=entry.Request;var own=snapshot.Bodies.FirstOrDefault(b=>b.Owner.ActorId==r.Intent.ActorId);
            var keepCursor=entry.Queued && (own==null || own.Position==r.Start);
            if(keepCursor && HasPrimary(entry) && entry.Jobs.Count!=0)
                keepCursor=PlanningJobsCompatible(entry,r);
            entry.Request=new TrafficRequest(r.Intent,r.RequestedFrame,snapshot,own?.Position??r.Start,r.Target,r.Radius,r.ResourceId,
                r.SelectedJourney,r.Requirements,r.NativeStamp,r.IndependentlyProgressing,r.FiniteSeconds,r.ProvenFailure,r.SafetyMargin,r.PlanPrimaryRoute);
            // Reuse only compatible captured geometry; fresh facts still reach endpoint/final safety checks.
            // 起点或实际规划障碍变化时只清旧搜索；累计 query/等待预算和 caller 静态约束不变。
            entry.Queued=true;
            if(!keepCursor){entry.Jobs.Clear();entry.JobIndex=0;entry.CurrentPath=Array.Empty<Vector3>();}
            SyncWork(entry);wakeups.Add(r.Intent);
        }
        public IReadOnlyList<TrafficIntentKey> DrainReady()
        {
            var ready = wakeups.OrderBy(k => k.ActorId, StringComparer.Ordinal).ThenBy(k => k.IntentId, StringComparer.Ordinal).ToArray();
            wakeups.Clear(); return Array.AsReadOnly(ready);
        }
        // Completed Arrived parking is storage, not an active release transaction.
        // 仅暂停真正无工作交接的原停车 checkpoint；不续期、不新增依赖、不绕过 claim 认证。
        private bool IdleArrivedParkingCheckpoint(Entry entry,HandoffState state)
        {
            if(state.CheckpointKind!=TrafficPhaseMilestoneKind.Arrived || state.Dependencies.Count!=0 ||
                state.PreparedSuccessor!=null || state.PreparationRequest!=null ||
                state.InspectingPreparation || state.CommittingPreparedSuccessor ||
                entry.Active || entry.Prepared || entry.Queued || !HandoffBodyCurrent(entry,state) || !entry.Leg.Bound)return false;
            var bodies=snapshot.Bodies.Where(body=>body.Owner.ActorId==entry.Key.ActorId).ToArray();
            if(bodies.Length!=1 || !bodies[0].Owner.Equals(entry.Key) || bodies[0].IndependentlyProgressing ||
                !(Vector3.Distance(bodies[0].Position,entry.Request.Target)<=entry.Leg.ArrivalTolerance) ||
                state.OriginalClaims==null || !snapshot.Parking.Any(claim=>claim.Owner.Equals(entry.Key) &&
                    state.OriginalClaims.Any(original=>SameOwnedClaim(original,claim))))return false;
            // Unacquired typed business eligibility holds no transaction work. Data only: no policy call in Tick.
            // 真实Queued/Prepared successor仍结束idle；无权业务资格等待不消耗原已完成Counter交接预算。
            return !OrderedWorkEntries.Any(other=>other!=entry && LiveEntry(other) && Current(other.Key) &&
                other.Key.ActorId==entry.Key.ActorId && !other.UnpaidEligibilityHeld);
        }
        public void Tick(float sceneDeltaSeconds, bool isPaused)
        {
            if (!Finite(sceneDeltaSeconds) || sceneDeltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(sceneDeltaSeconds));
            paused = isPaused; if (paused || closed) return;
            foreach (var entry in OrderedWorkEntries.Where(e => !e.Retired && (e.YieldOwnerWaitActive ||
                (e.FrozenOwners != null || e.AdmissionWaitStarted || e.CounterAdvanceReadinessWaitStarted) &&
                (e.AdmissionHeld || e.Queued || e.DeliveryAlternateDebtGap || e.Decision.Kind == TrafficDecisionKind.WaitingForTraffic))).ToArray())
            {
                entry.WaitRemaining -= sceneDeltaSeconds;
                if (entry.WaitRemaining <= 0) Fail(entry, TrafficDecisionKind.PlanningFailed, "wait-budget-exhausted", entry.Decision.Blockers,
                    preparation:entry.Leg==null);
            }
            foreach(var entry in OrderedWorkEntries.Where(e=>!e.Retired && e.PendingHandoff!=null).ToArray())
            {
                if(IdleArrivedParkingCheckpoint(entry,entry.PendingHandoff))continue;
                entry.PendingHandoff.Remaining-=sceneDeltaSeconds;
                if(entry.PendingHandoff.Remaining<=0)AbortHandoff(entry,"original-budget-exhausted");
            }
        }
        public void ResetLayout(long newEpoch, int newLayoutRevision, TrafficWorldSnapshot world)
        {
            if (committing) throw new InvalidOperationException("Reset inside joint commit.");
            epoch = newEpoch; layoutRevision = newLayoutRevision; snapshot = world ?? throw new ArgumentNullException(nameof(world));
            entries.Clear();workEntries.Clear(); wakeups.Clear();recoveryReceipts.Clear();
        }
        public void Close() { closed = true; entries.Clear();workEntries.Clear(); wakeups.Clear();recoveryReceipts.Clear(); }

        private TrafficDecision Decide(Entry entry, TrafficDecisionKind kind, string reason, IEnumerable<TrafficBlocker> blockers = null,
            TrafficFailureDisposition failureDisposition=TrafficFailureDisposition.None)
        {
            if(!LiveEntry(entry))return entry?.Decision;
            entry.PreparationFailed=false;
            entry.Decision = new TrafficDecision(entry.Key, kind, reason, requestSequence: entry.RequestSequence,
                acquiredSequence: entry.AcquiredSequence, blockers: blockers,failureDisposition:failureDisposition);
            if(kind==TrafficDecisionKind.WaitingForTraffic)entry.WaitingBlockers=entry.Decision.Blockers;
            else if(kind!=TrafficDecisionKind.DeferredByBudget && failureDisposition!=TrafficFailureDisposition.AlternateAllowed)
                entry.WaitingBlockers=Array.Empty<TrafficBlocker>();
            SyncWork(entry);return entry.Decision;
        }
        private void Fail(Entry entry, TrafficDecisionKind kind, string reason, IEnumerable<TrafficBlocker> blockers = null,
            bool releasedProvisional = false,bool preparation=false,bool alternate=false,bool deliveryAlternateDebt=false)
        {
            if(!LiveEntry(entry))return;
            // Only completed real spatial proof failure is business ineligibility. Budget/config/identity remain terminal.
            // Generic/Paid/Native/已调用canonical mutation的来源永远不能进入这个付款前窄分支。
            if(preparation && kind==TrafficDecisionKind.PlanningFailed && reason=="bounded-route-proof-failed" && !entry.UnpaidEligibilityQueryFault && PreparationEligibilityCurrent(entry))
            {
                var roleIndex=entry.JobIndex-(HasPrimary(entry)?1:0);
                var role=roleIndex>=0 && roleIndex<entry.Request.Requirements.Count?entry.Request.Requirements[roleIndex].ResourceId:null;
                var candidateIndependent=roleIndex<0 || role=="waiting-exit-capability" ||
                    role!=null && role.StartsWith("existing-goal/",StringComparison.Ordinal);
                if((candidateIndependent || CandidateAttempts(entry).Count>=entry.CandidateCount) &&
                    HoldUnpaidEligibility(entry,"unpaid-eligibility/route-capability-unavailable",blockers,role))return;
            }
            entry.UnpaidEligibilityHeld=false;
            var intent=entry.Key;var resource=entry.Request.ResourceId;
            var bodyCouldProgress=snapshot.Bodies.Any(b=>b.Owner.Equals(intent)) && CanProgress(intent,out _);
            var staleCandidate = kind == TrafficDecisionKind.StaleContext && entry.Leg != null &&
                !entry.Leg.Committed && !entry.Leg.Bound && !entry.Leg.Completed;
            var deliveryInspect=deliveryAlternateDebt?ClaimInspectionGuard(entry):null;
            if(!releasedProvisional)ObserveOwnerInvalidation(entry,kind,reason,blockers,bodyCouldProgress);
            if(deliveryInspect!=null && !deliveryInspect())return;
            if (entry.Leg != null) entry.Leg.ReprepareAllowed = staleCandidate;
            entry.AdmissionHeld=false;entry.ProducerPreviewHeld=false;entry.Queued = false; entry.Prepared = false; entry.Active = false;
            entry.CurrentPath = Array.Empty<Vector3>();
            if (!staleCandidate) entry.Journey = Array.Empty<Vector3>();
            entry.Jobs.Clear();
            var disposition=staleCandidate?TrafficFailureDisposition.None:
                alternate && CanAlternate(entry)?TrafficFailureDisposition.AlternateAllowed:TrafficFailureDisposition.Terminal;
            entry.DeliveryAlternateDebtGap=deliveryAlternateDebt && disposition==TrafficFailureDisposition.AlternateAllowed;
            if(disposition==TrafficFailureDisposition.Terminal)
            {
                entry.FrozenSelectedRouteEligibility=null;
                entry.DeliveryAlternateDebtGap=false;ClearNativeChunks(entry);StopYieldOwnerWait(entry);entry.CounterAdvanceReadiness=null;entry.UnpaidEligibilitySpatialRefusal=null;
                entry.UnpaidEligibilitySpatialActors.Clear();entry.UnpaidEligibilityNativeActivity=Array.Empty<UnpaidNativeActivity>();
            }
            Decide(entry, kind, reason, blockers,disposition);
            entry.PreparationFailed=preparation && kind==TrafficDecisionKind.PlanningFailed;
            if(disposition==TrafficFailureDisposition.Terminal)AbortPreparationsUsing(entry,"successor-terminal");
            // Losing an uncommitted proposal frees its space; genuine owner failure does not prove release.
            // 唤醒后仍检查真实身体和canonical预留，不能把取消许可当作身体消失。
            if (releasedProvisional) NotifyChanged(intent, resource);
            else InvalidateDependentWaiters(entry);
            // Losing this exact body's finite owner proof is a checkpoint, not movement or disappearance.
            // 临时 reprepare/alternate 不广播 proof 丢失；另一个完整 body owner 也不受此推导。
            if(disposition==TrafficFailureDisposition.Terminal && bodyCouldProgress && !CanProgress(intent,out _))
                NotifyChanged(intent,"body");
        }
        // Called only from an actual validated finite, acyclic WaitOrFail transition; never from Fail or Terminal polling.
        // No new release/dependency/permit is supplied. The ordinary batch rechecks the original candidate's full current geometry.
        private bool TryRequeueUnbornParkingAfterTemporaryOwnerWait(Entry entry,Entry owner)
        {
            if(closed || paused || committing || !LiveEntry(entry) || !LiveEntry(owner) || !Current(entry.Key) || !Current(owner.Key))return false;
            var request=entry.Request;var leg=owner.Leg;
            if(owner.Decision.Kind!=TrafficDecisionKind.WaitingForTraffic || owner.Decision.Reason!="finite-owner-release" ||
                owner.Decision.FailureDisposition!=TrafficFailureDisposition.None || owner.PendingHandoff!=null || !Positive(owner.WaitRemaining) ||
                owner.FrozenOwners==null || owner.FrozenResources==null || owner.WaitingBlockers.Count==0 ||
                owner.JourneyPermit==null || owner.JourneyPermit.Kind!=TrafficDecisionKind.Granted || owner.JourneyRequest==null || owner.AcquiredSequence<=0 ||
                leg==null || leg.Committed || leg.Bound || leg.Completed || leg.Suspended || leg.ReprepareAllowed ||
                leg.Stamp.RequestId!=0 || leg.Stamp.LegGeneration!=0 || owner.Request.NativeStamp.RequestId!=0 ||
                owner.Request.NativeStamp.LegGeneration!=leg.Generation || !LegBodyCurrent(owner,leg.ActorLife))return false;
            var body=snapshot.Bodies.FirstOrDefault(value=>value.Owner.ActorId==owner.Key.ActorId);
            if(body==null || body.IndependentlyProgressing || body.Position!=owner.Request.Start)return false;
            if(!entry.Parking || entry.Prepared || entry.Active || entry.Discarded || entry.Decision.Kind!=TrafficDecisionKind.WaitingForTraffic ||
                entry.Decision.FailureDisposition!=TrafficFailureDisposition.None || entry.AcquiredSequence!=0 || entry.EverCanonicalAttempt ||
                entry.Leg!=null || entry.JourneyPermit!=null || entry.JourneyRequest!=null || entry.Journey.Length!=0 || entry.PendingHandoff!=null ||
                entry.ExplicitRecoveryUsed || entry.LegacyLegCompleted || entry.ContinuationAttempted ||
                !request.PlanPrimaryRoute || !request.NativeStamp.Equals(default(TrafficNativeStamp)) || request.SelectedJourney.Count!=0 ||
                request.Requirements.Any(requirement=>requirement.SelectedJourney) || request.ProvenFailure.HasValue ||
                !Positive(entry.WaitRemaining) || entry.FrozenOwners==null || entry.FrozenResources==null || !entry.FrozenOwners.Contains(owner.Key) ||
                !entry.FrozenResources.TryGetValue(owner.Key,out var resources) ||
                !entry.WaitingBlockers.Any(block=>block.Owner.Equals(owner.Key) && resources.Contains(block.ResourceId)))return false;
            var inspectEntry=ClaimInspectionGuard(entry);var inspectOwner=ClaimInspectionGuard(owner);
            var current=Valid(request) && ValidSnapshot() && PreparationIdentityCurrent(entry);
            if(!inspectEntry() || !inspectOwner() || !current)return false;
            // Repeated owner checks do not restart an already queued frontier or renew its finite wait.
            if(!entry.Queued)QueueChangedWaiter(entry);
            return true;
        }
        private void InvalidateDependentWaiters(Entry owner,bool reproveUnbornParking=false)
        {
            if(!RegisteredEntry(owner))return;
            foreach (var dependent in OrderedWorkEntries.Where(e => e != owner && !e.Retired && e.Decision != null &&
                e.Decision.Kind == TrafficDecisionKind.WaitingForTraffic && e.Decision.Blockers.Any(b => b.Owner.Equals(owner.Key))).ToArray())
            {
                if(reproveUnbornParking && TryRequeueUnbornParkingAfterTemporaryOwnerWait(dependent,owner))continue;
                Fail(dependent, TrafficDecisionKind.PlanningFailed, "owner-no-longer-independent", dependent.Decision.Blockers);
            }
        }
        // Work is derived from the same live flags; physical snapshot facts remain independent.
        // 只维护派生工作集合；同 key 的旧 callback 不得把已移除的 Entry 加回来。
        private IEnumerable<Entry> OrderedWorkEntries => workEntries.OrderBy(e=>e.BirthOrder);
        private void SyncWork(Entry entry)
        {
            if(entry==null)return;
            if(LiveEntry(entry) &&
                (ReferenceEquals(entry,completingHandoff) ||
                entry.PendingHandoff!=null || entry.UnpaidEligibilityHeld || entry.AdmissionHeld || entry.YieldOwnerWaitActive || entry.DeliveryAlternateDebtGap || entry.Queued || entry.Prepared || entry.Active || entry.Decision.Kind == TrafficDecisionKind.WaitingForTraffic || entry.Journey.Length > 1 ||
                entry.Leg!=null && !entry.Leg.Completed && entry.Decision.FailureDisposition!=TrafficFailureDisposition.Terminal &&
                (entry.Leg.Suspended || entry.Leg.ReprepareAllowed)))workEntries.Add(entry);
            else workEntries.Remove(entry);
        }
        private bool RegisteredEntry(Entry entry)=>entry!=null && entries.TryGetValue(entry.Key,out var current) && ReferenceEquals(current,entry);
        private bool LiveEntry(Entry entry)=>!closed && RegisteredEntry(entry) && !entry.Retired && entry.Request!=null;
        private bool Current(TrafficIntentKey key) => key.Session == session && key.Epoch == epoch && key.LayoutRevision == layoutRevision;
        private bool SnapshotCurrent(TrafficRequest request) => request!=null && request.Snapshot != null && request.Snapshot.Version == snapshot.Version &&
            request.Snapshot.BodyVersion == snapshot.BodyVersion;
        private static bool SameRequest(TrafficRequest a, TrafficRequest b) =>
            a.Start == b.Start && a.Target == b.Target && a.ResourceId == b.ResourceId && a.Snapshot != null && b.Snapshot != null &&
            a.Snapshot.Version == b.Snapshot.Version &&
            a.SelectedJourney.SequenceEqual(b.SelectedJourney) && SameRequirements(a.Requirements,b.Requirements) && a.PlanPrimaryRoute==b.PlanPrimaryRoute &&
            a.Radius == b.Radius && a.SafetyMargin == b.SafetyMargin && a.ProvenFailure == b.ProvenFailure &&
            a.IndependentlyProgressing == b.IndependentlyProgressing && a.FiniteSeconds == b.FiniteSeconds;
        private static bool SameRequirements(IReadOnlyList<TrafficRouteRequirement> a,IReadOnlyList<TrafficRouteRequirement> b)
            => a.Count==b.Count && a.Select((value,index)=>new{value,index}).All(x=>x.value!=null && b[x.index]!=null &&
                x.value.ResourceId==b[x.index].ResourceId && x.value.Start==b[x.index].Start && x.value.Goal==b[x.index].Goal &&
                x.value.Clearance==b[x.index].Clearance && x.value.SelectedJourney==b[x.index].SelectedJourney &&
                x.value.AvoidProposedParking==b[x.index].AvoidProposedParking && x.value.Obstacles.SequenceEqual(b[x.index].Obstacles));
        private static bool Valid(TrafficRequest request) => request!=null && !string.IsNullOrWhiteSpace(request.Intent.ActorId) &&
            !string.IsNullOrWhiteSpace(request.Intent.IntentId) && !string.IsNullOrWhiteSpace(request.Intent.Context) &&
            !string.IsNullOrWhiteSpace(request.ResourceId) && Positive(request.Radius) && Finite(request.SafetyMargin) &&
            request.SafetyMargin >= 0 && Finite(request.Start) && Finite(request.Target) && Positive(request.FiniteSeconds) &&
            request.SelectedJourney.All(Finite) && request.Requirements.All(r => r != null && Positive(r.Clearance) &&
                Finite(r.Start) && Finite(r.Goal) && r.Obstacles.All(Finite)) &&
            (!request.ProvenFailure.HasValue || request.ProvenFailure == TrafficDecisionKind.InvalidStart ||
                request.ProvenFailure == TrafficDecisionKind.Unreachable || request.ProvenFailure == TrafficDecisionKind.InvalidConfiguration);
        private static float PairClearance(TrafficRequest a, TrafficRequest b) => a.Radius + b.Radius + Mathf.Max(a.SafetyMargin, b.SafetyMargin);
        private bool ValidSnapshot() => snapshot.Bodies.All(b => b != null && Positive(b.Radius) && Finite(b.Position) &&
            Finite(b.SafetyMargin) && b.SafetyMargin >= 0) && snapshot.Parking.All(p => p != null && p.CanonicalOwner != null &&
            Positive(p.Radius) && Finite(p.Position) && Finite(p.SafetyMargin) && p.SafetyMargin >= 0);
        private bool IndependentPathClear(Entry entry)
        {
            if(!LiveEntry(entry))return false;
            var r = entry.Request;
            return snapshot.Bodies.Where(b => b.Owner.ActorId != r.Intent.ActorId).All(b =>
                !NearPath(b.Position, entry.CurrentPath, r.Radius + b.Radius + Mathf.Max(r.SafetyMargin, b.SafetyMargin))) &&
                snapshot.Parking.Where(p => p.Owner.ActorId != r.Intent.ActorId).All(p =>
                !NearPath(p.Position, entry.CurrentPath, r.Radius + p.Radius + Mathf.Max(r.SafetyMargin, p.SafetyMargin)));
        }

        private enum JobStatus { Complete, Deferred, Exhausted, Failed }
        private delegate JobStatus PathQuery(Vector3 from, Vector3 to, out IReadOnlyList<Vector3> path);
        private readonly struct NativeChunkKey:IEquatable<NativeChunkKey>
        {
            internal readonly Vector3 From,To;
            internal NativeChunkKey(Vector3 from,Vector3 to){From=from;To=to;}
            public bool Equals(NativeChunkKey other)=>From.Equals(other.From) && To.Equals(other.To);
            public override bool Equals(object other)=>other is NativeChunkKey key && Equals(key);
            public override int GetHashCode()=>unchecked(From.GetHashCode()*397^To.GetHashCode());
        }
        private static void ClearNativeChunks(Entry entry)
        {entry.NativeChunks=null;entry.NativeChunkContext=null;entry.NativeChunkReadAllowed=false;}
        private bool NativeChunkScope(Entry entry)=>getNativePathContext!=null && entry.UnpaidEligibility!=null &&
            entry.AcquiredSequence==0 && !entry.Active && !entry.Prepared && !entry.Discarded && entry.Leg==null &&
            entry.JourneyPermit==null && entry.JourneyRequest==null && entry.PendingHandoff==null && !entry.EverCanonicalAttempt &&
            !entry.ExplicitRecoveryUsed && !entry.LegacyLegCompleted && !entry.UnpaidEligibilityQueryFault;
        private bool ReadNativeChunkContext(Entry entry,Func<bool> inspect,out object context)
        {
            context=null;try{context=getNativePathContext();}catch{if(inspect()){entry.UnpaidEligibilityQueryFault=true;ClearNativeChunks(entry);}return false;}
            return inspect();
        }
        private JobStatus Query(Entry entry, Vector3 from, Vector3 to, out IReadOnlyList<Vector3> path)
        {
            path = null;
            if(!LiveEntry(entry) || !entry.Queued)return JobStatus.Failed;
            var request=entry.Request;var memo=NativeChunkScope(entry);var inspect=memo?ClaimInspectionGuard(entry):null;
            var key=new NativeChunkKey(from,to);object context=null;
            if(memo)
            {
                if(!ReadNativeChunkContext(entry,inspect,out context))return JobStatus.Failed;
                if(!ReferenceEquals(context,entry.NativeChunkContext))
                {
                    var known=entry.NativeChunkContext!=null;entry.NativeChunks=null;entry.NativeChunkContext=context;
                    if(known || context==null)entry.NativeChunkReadAllowed=false;
                }
                if(context!=null && entry.NativeChunkReadAllowed && entry.NativeChunks!=null && entry.NativeChunks.TryGetValue(key,out var cached))
                {
                    var currentProducer=UnpaidEligibilityCurrent(entry);if(!inspect())return JobStatus.Failed;
                    if(!currentProducer){entry.UnpaidEligibilityQueryFault=true;ClearNativeChunks(entry);return JobStatus.Failed;}
                    if(!ReadNativeChunkContext(entry,inspect,out var current))return JobStatus.Failed;
                    if(!ReferenceEquals(context,current)){entry.UnpaidEligibilityQueryFault=true;ClearNativeChunks(entry);return JobStatus.Failed;}
                    if(ProofStepsThisFrame>=queriesPerFrame)return JobStatus.Deferred;
                    ProofStepsThisFrame++;NativeChunkHits++;path=cached;return JobStatus.Complete;
                }
            }
            if (entry.Queries >= queriesPerIntent) {ObserveUnpaidPreparation(entry,"query-budget-exhausted",entry.Request);return JobStatus.Exhausted;}
            if (QueriesThisFrame >= queriesPerFrame || getNativePathContext!=null && ProofStepsThisFrame>=queriesPerFrame) return JobStatus.Deferred;
            entry.Queries++; QueriesThisFrame++;ProofStepsThisFrame++;
            try { path = getCompletePath(from, to); }
            catch { if(entry.UnpaidEligibility!=null || entry.AdmissionReadiness!=null)entry.UnpaidEligibilityQueryFault=true;return JobStatus.Failed; }
            if(!LiveEntry(entry) || !entry.Queued || !ReferenceEquals(entry.Request,request)){path=null;return JobStatus.Failed;}
            if (path != null && (path.Count == 0 || path.Any(p => !Finite(p))))
            {if(entry.UnpaidEligibility!=null || entry.AdmissionReadiness!=null)entry.UnpaidEligibilityQueryFault=true;return JobStatus.Failed;}
            if(memo)
            {
                var frozen=path?.ToArray();
                if(!ReadNativeChunkContext(entry,inspect,out var current)){path=null;return JobStatus.Failed;}
                if(!ReferenceEquals(context,current)){path=null;entry.UnpaidEligibilityQueryFault=true;ClearNativeChunks(entry);return JobStatus.Failed;}
                if(context!=null && frozen!=null)
                {
                    if(entry.NativeChunks==null)entry.NativeChunks=new Dictionary<NativeChunkKey,IReadOnlyList<Vector3>>();
                    path=entry.NativeChunks[key]=Array.AsReadOnly(frozen);
                }
            }
            return JobStatus.Complete;
        }
        private readonly struct Obstacle
        {
            internal readonly Vector3 Position;
            internal readonly float Clearance;
            internal Obstacle(Vector3 position, float clearance) { Position = position; Clearance = clearance; }
        }

        // Existing bounded candidate geometry with cursor/frontier retained between frames.
        // 不抛弃A*进度；缓存实际Native legs，不用null假装预算用尽。
        private sealed class RouteJob
        {
            private readonly Vector3 start, goal;
            private readonly List<Obstacle> obstacles;
            private PreferredBody[] preferredBodies=Array.Empty<PreferredBody>();
            private RouteJob preferredJob;
            private bool preferredFinished;
            private readonly List<Vector3> nodes = new List<Vector3>();
            private readonly Dictionary<long, IReadOnlyList<Vector3>> paths = new Dictionary<long, IReadOnlyList<Vector3>>();
            private float[] costs;
            private int[] parents;
            private bool[] visited;
            private int current = -1, next;
            private bool started, complete;
            internal bool HasPreferredBodies=>preferredBodies.Length!=0;
            internal bool HasUnfinishedBase=>!complete;
            internal Vector3[] ExpandedPath { get; private set; } = Array.Empty<Vector3>();
            internal Vector3[] Waypoints { get; private set; } = Array.Empty<Vector3>();
            internal int[] LegEnds { get; private set; } = Array.Empty<int>();
            internal RouteJob(Vector3 start, Vector3 goal, List<Obstacle> obstacles)
            { this.start = start; this.goal = goal; this.obstacles = obstacles; }

            // Include every Native segment, its virtual waypoint, and the declared first/last connector.
            // 复核完整接缝，不把弯曲Native path替换成穿柜台的waypoint直线。
            internal Vector3[] CompletedGuardPath()
            {
                if(!complete || HasPreferredBodies || ExpandedPath.Length==0 || Waypoints.Length==0 || LegEnds.Length!=Waypoints.Length ||
                    LegEnds.Last()!=ExpandedPath.Length-1 || Waypoints.Last()!=goal)return Array.Empty<Vector3>();
                var joined=new List<Vector3>{start};var previous=-1;
                for(var leg=0;leg<LegEnds.Length;leg++)
                {
                    var end=LegEnds[leg];if(end<=previous || end>=ExpandedPath.Length)return Array.Empty<Vector3>();
                    joined.AddRange(ExpandedPath.Skip(previous+1).Take(end-previous));joined.Add(Waypoints[leg]);previous=end;
                }
                joined.Add(goal);return joined.ToArray();
            }
            internal RouteJob RevalidateCompleted(Vector3 currentStart,Vector3 currentGoal,List<Obstacle> currentObstacles)
            {
                if(start!=currentStart || goal!=currentGoal)return null;
                var path=CompletedGuardPath();if(path.Length==0 || currentObstacles.Any(o=>NearPath(o.Position,path,o.Clearance)))return null;
                return new RouteJob(start,goal,currentObstacles){started=true,complete=true,
                    ExpandedPath=ExpandedPath.ToArray(),Waypoints=Waypoints.ToArray(),LegEnds=LegEnds.ToArray()};
            }

            // Compare exact Position/Clearance values, ignoring duplicate facts and list ordering.
            // 比较捕获的真实几何；独立身体未进入规划障碍时仍复用 cursor。
            internal bool HasSameObstacles(IEnumerable<Obstacle> current,IEnumerable<PreferredBody> preferred=null,IEnumerable<Obstacle> keepouts=null)
            {
                var currentObstacles=current.ToArray();var bodies=(preferred??Array.Empty<PreferredBody>()).ToArray();
                return new HashSet<Obstacle>(obstacles).SetEquals(currentObstacles) && (!HasPreferredBodies ||
                    bodies.Length==preferredBodies.Length && preferredBodies.All(body=>bodies.Any(body.Same)) &&
                    preferredJob.HasSameObstacles(currentObstacles.Concat(bodies.Select(b=>b.Geometry)).Concat(keepouts??Array.Empty<Obstacle>())));
            }
            internal bool TryStartPreferred(IReadOnlyList<TrafficBlocker> blockers,PreferredBody[] currentBodies,IEnumerable<Obstacle> keepouts)
            {
                if(!complete || preferredJob!=null || preferredFinished || currentBodies.Length==0 || blockers.Count==0 ||
                    blockers.Any(b=>b.ResourceId!="body" || b.Proof!="live-body-checkpoint" || !currentBodies.Any(p=>p.Owner.Equals(b.Owner))))return false;
                // Capture only when an actual child starts; unrelated potential bodies never invalidate the base cursor.
                // 先保原base frontier；首次真实绕行才锁当下owner/life/位置/间距，随后严格复核。
                preferredBodies=currentBodies.ToArray();
                var full=obstacles.Concat(preferredBodies.Select(b=>b.Geometry)).Concat(keepouts).ToList();
                preferredJob=new RouteJob(start,goal,full);return true;
            }
            internal JobStatus Advance(PathQuery query)
            {
                var original=AdvanceOriginal(query);
                if(original!=JobStatus.Complete || preferredJob==null || preferredFinished)return original;
                var queryFailed=false;
                var preferred=preferredJob.Advance((Vector3 from,Vector3 to,out IReadOnlyList<Vector3> path)=>
                {
                    var status=query(from,to,out path);
                    if(status==JobStatus.Failed)queryFailed=true;
                    return status;
                });
                if(queryFailed)return JobStatus.Failed;
                if(preferred==JobStatus.Deferred || preferred==JobStatus.Exhausted)return preferred;
                preferredFinished=true;
                // Failed means a completed no-detour search; keep the original path and original finite wait.
                // 预算未知/耗尽不能冒充无路；整条连接线也必须满足原尺寸和间距。
                if(preferred==JobStatus.Complete && preferredJob.Waypoints.Length<=24 &&
                    preferredJob.Clear(new[]{start}.Concat(preferredJob.ExpandedPath).Concat(new[]{goal}).ToArray()))
                {
                    ExpandedPath=preferredJob.ExpandedPath;Waypoints=preferredJob.Waypoints;LegEnds=preferredJob.LegEnds;
                }
                return JobStatus.Complete;
            }
            private JobStatus AdvanceOriginal(PathQuery query)
            {
                if (complete) return JobStatus.Complete;
                if (!started)
                {
                    var status = query(start, goal, out var direct);
                    if (status != JobStatus.Complete) return status;
                    started = true;
                    if (direct == null) return JobStatus.Failed;
                    if (Clear(direct)) { ExpandedPath = direct.ToArray(); Waypoints = new[] { goal };LegEnds=new[]{ExpandedPath.Length-1}; complete = true; return JobStatus.Complete; }
                    BuildNodes(); paths[Edge(0, 1)] = direct;
                }
                while (true)
                {
                    if (current < 0)
                    {
                        var best = float.PositiveInfinity;
                        for (var i = 0; i < nodes.Count; i++)
                        {
                            var score = costs[i] + Distance(nodes[i], goal);
                            if (!visited[i] && score < best) { current = i; best = score; }
                        }
                        if (current < 0) return JobStatus.Failed;
                        if (current == 1) return Finish()?JobStatus.Complete:JobStatus.Failed;
                        visited[current] = true; next = 1;
                    }
                    while (next < nodes.Count)
                    {
                        if (visited[next] || next == current || costs[current] + Distance(nodes[current], nodes[next]) >= costs[next])
                        { next++; continue; }
                        var edge = Edge(current, next);
                        if (!paths.TryGetValue(edge, out var path))
                        {
                            var status = query(nodes[current], nodes[next], out path);
                            if (status != JobStatus.Complete) return status;
                            paths[edge] = path;
                        }
                        if (path != null && Clear(path))
                        {
                            var length = 0f;
                            for (var p = 1; p < path.Count; p++) length += Distance(path[p - 1], path[p]);
                            if (costs[current] + length < costs[next])
                            { costs[next] = costs[current] + length; parents[next] = current; }
                        }
                        next++;
                    }
                    current = -1;
                }
            }
            private void BuildNodes()
            {
                nodes.Add(start); nodes.Add(goal);
                foreach (var obstacle in obstacles.OrderBy(o => PointSegmentDistance(o.Position, start, goal)).Take(8))
                {
                    var radius = obstacle.Clearance / Mathf.Cos(Mathf.PI / 8) + .06f;
                    foreach (var ring in new[] { radius, radius * 2 })
                        for (var i = 0; i < 8; i++)
                        {
                            var angle = i * Mathf.PI / 4;
                            var point = obstacle.Position + new Vector3(Mathf.Cos(angle) * ring, 0, Mathf.Sin(angle) * ring);
                            point.y = start.y;
                            if (obstacles.All(o => Distance(point, o.Position) >= o.Clearance) && !nodes.Contains(point)) nodes.Add(point);
                        }
                }
                costs = Enumerable.Repeat(float.PositiveInfinity, nodes.Count).ToArray(); costs[0] = 0;
                parents = Enumerable.Repeat(-1, nodes.Count).ToArray(); visited = new bool[nodes.Count];
            }
            private bool Clear(IReadOnlyList<Vector3> path) => obstacles.All(o => !NearPath(o.Position, path, o.Clearance));
            private bool Finish()
            {
                var indices = new List<int>();
                for (var i = 1; i != 0; i = parents[i]) indices.Add(i);
                indices.Reverse();var waypoints=indices.Select(i=>nodes[i]).ToArray();
                var expanded = new List<Vector3> { start }; var previous = 0;var ends=new List<int>();
                for(var leg=0;leg<indices.Count;leg++)
                {
                    var index=indices[leg];var native=paths[Edge(previous,index)];
                    expanded.AddRange(native);ends.Add(expanded.Count-1);previous=index;
                    if(leg<indices.Count-1)
                    {
                        // Only virtual turns follow their already-proven Native endpoint; the declared goal stays exact.
                        // 中间绕行点对齐真实NavMesh端点，避免采样误差与到达误差叠加；最终站位不变。
                        var endpoint=native[native.Count-1];endpoint.y=start.y;waypoints[leg]=endpoint;
                    }
                }
                // Individual edge proofs do not authenticate a gap between different sampled endpoints.
                // 封存前核验整段接缝及原始首尾连接；不增加查询，不跳过碰撞或刷新预算。
                if(!Clear(GuardCurrentPath(start,expanded,goal)))return false;
                Waypoints=waypoints;ExpandedPath=expanded.ToArray();LegEnds=ends.ToArray();complete=true;return true;
            }
            private static long Edge(int from, int to) => ((long)from << 32) | (uint)to;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Positive(float value) => Finite(value) && value > 0;
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static float Distance(Vector3 a, Vector3 b) { a.y = b.y = 0; return Vector3.Distance(a, b); }
        private static float PointSegmentDistance(Vector3 point, Vector3 a, Vector3 b)
        {
            point.y = a.y = b.y = 0; var delta = b - a;
            var t = delta.sqrMagnitude < 1e-10f ? 0 : Mathf.Clamp01(Vector3.Dot(point - a, delta) / delta.sqrMagnitude);
            return Vector3.Distance(point, a + delta * t);
        }
        private static bool NearPath(Vector3 point, IReadOnlyList<Vector3> path, float clearance)
        {
            if (path == null || path.Count == 0) return false;
            if (path.Count == 1) return Distance(point, path[0]) < clearance;
            for (var i = 1; i < path.Count; i++) if (PointSegmentDistance(point, path[i - 1], path[i]) < clearance) return true;
            return false;
        }
        private static bool PathsConflict(IReadOnlyList<Vector3> a, IReadOnlyList<Vector3> b, float clearance)
        {
            for (var i = 1; i < a.Count; i++)
                for (var j = 1; j < b.Count; j++)
                {
                    if (PointSegmentDistance(a[i - 1], b[j - 1], b[j]) < clearance || PointSegmentDistance(a[i], b[j - 1], b[j]) < clearance ||
                        PointSegmentDistance(b[j - 1], a[i - 1], a[i]) < clearance || PointSegmentDistance(b[j], a[i - 1], a[i]) < clearance) return true;
                    var u = a[i] - a[i - 1]; var v = b[j] - b[j - 1]; var w = b[j - 1] - a[i - 1];
                    var cross = u.x * v.z - u.z * v.x;
                    if (Mathf.Abs(cross) < 1e-10f) continue;
                    var t = (w.x * v.z - w.z * v.x) / cross; var s = (w.x * u.z - w.z * u.x) / cross;
                    if (t >= 0 && t <= 1 && s >= 0 && s <= 1) return true;
                }
            return false;
        }
    }
}

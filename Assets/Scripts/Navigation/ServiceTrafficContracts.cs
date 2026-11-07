using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AnimalCafe.Navigation
{
    // Internal producer identity/readiness only; this object cannot grant any traffic right.
    internal sealed class TrafficAdmissionReadiness
    {
        internal readonly TrafficIntentKey Intent;
        internal readonly Func<bool> IsCurrent;
        internal readonly Func<IReadOnlyList<TrafficIntentKey>> PendingExits;
        internal TrafficAdmissionReadiness(TrafficIntentKey intent,Func<bool> isCurrent,Func<IReadOnlyList<TrafficIntentKey>> pendingExits)
        { Intent=intent;IsCurrent=isCurrent??throw new ArgumentNullException(nameof(isCurrent));PendingExits=pendingExits??throw new ArgumentNullException(nameof(pendingExits)); }
    }

    // Unacquired queued/unpaid Counter proposal only; this descriptor grants no traffic WAIT or movement right.
    // 只认证付款前真实Counter身份；空间资格等待沿用原Entry/age/query，不复活Terminal。
    internal sealed class TrafficUnpaidParkingEligibility
    {
        internal readonly TrafficIntentKey Intent;
        internal readonly object ActorLife,CounterOwner,BusinessOwner,Admission;
        internal readonly Func<bool> IsCurrent;
        internal TrafficUnpaidParkingEligibility(TrafficIntentKey intent,object actorLife,object counterOwner,
            object businessOwner,object admission,Func<bool> isCurrent)
        {Intent=intent;ActorLife=actorLife;CounterOwner=counterOwner;BusinessOwner=businessOwner;Admission=admission;
            IsCurrent=isCurrent??throw new ArgumentNullException(nameof(isCurrent));}
    }

    // Existing Counter producer only. Withholds preparation; never grants a finite WAIT/Native right.
    // 已有顾客的原Counter准备，与未出生admission身份严格区分。
    internal sealed class TrafficCounterAdvanceReadiness
    {
        internal readonly TrafficIntentKey Intent;
        internal readonly Func<bool> IsCurrent;
        internal readonly Func<TrafficIntentKey,float> FiniteWorkingPredecessorSeconds;
        internal TrafficCounterAdvanceReadiness(TrafficIntentKey intent,Func<bool> isCurrent,Func<TrafficIntentKey,float> finiteWorkingPredecessorSeconds)
        { Intent=intent;IsCurrent=isCurrent??throw new ArgumentNullException(nameof(isCurrent));
            FiniteWorkingPredecessorSeconds=finiteWorkingPredecessorSeconds??throw new ArgumentNullException(nameof(finiteWorkingPredecessorSeconds)); }
    }

    public enum TrafficDecisionKind
    {
        Granted, WaitingForTraffic, DeferredByBudget, Unreachable, InvalidConfiguration,
        PlanningFailed, InvalidStart, Cancelled, StaleContext
    }

    // Failure authority is distinct from the displayed outcome. / 失败后的权限独立于显示结果。
    public enum TrafficFailureDisposition { None, AlternateAllowed, Terminal }

    // A business phase owns age; a Native leg does not. / 分腿不能重置意图年龄。
    public readonly struct TrafficIntentKey : IEquatable<TrafficIntentKey>
    {
        public string Session { get; }
        public long Epoch { get; }
        public string ActorId { get; }
        public string IntentId { get; }
        public int Generation { get; }
        public int LayoutRevision { get; }
        public string Context { get; }
        public TrafficIntentKey(string session, long epoch, string actorId, string intentId,
            int generation, int layoutRevision, string context)
        {
            Session = session; Epoch = epoch; ActorId = actorId; IntentId = intentId;
            Generation = generation; LayoutRevision = layoutRevision; Context = context;
        }
        public bool Equals(TrafficIntentKey other) => Session == other.Session && Epoch == other.Epoch &&
            ActorId == other.ActorId && IntentId == other.IntentId && Generation == other.Generation &&
            LayoutRevision == other.LayoutRevision && Context == other.Context;
        public override bool Equals(object obj) => obj is TrafficIntentKey other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Session?.GetHashCode() ?? 0;
                hash = hash * 397 ^ Epoch.GetHashCode(); hash = hash * 397 ^ (ActorId?.GetHashCode() ?? 0);
                hash = hash * 397 ^ (IntentId?.GetHashCode() ?? 0); hash = hash * 397 ^ Generation;
                return (hash * 397 ^ LayoutRevision) * 397 ^ (Context?.GetHashCode() ?? 0);
            }
        }
    }

    public readonly struct TrafficNativeStamp : IEquatable<TrafficNativeStamp>
    {
        public long RequestId { get; }
        public int LegGeneration { get; }
        public TrafficNativeStamp(long requestId, int legGeneration)
        { RequestId = requestId; LegGeneration = legGeneration; }
        public bool Equals(TrafficNativeStamp other) => RequestId == other.RequestId && LegGeneration == other.LegGeneration;
        public override bool Equals(object obj) => obj is TrafficNativeStamp other && Equals(other);
        public override int GetHashCode() => RequestId.GetHashCode() ^ LegGeneration;
    }

    public sealed class TrafficBodyFact
    {
        public TrafficIntentKey Owner { get; }
        public Vector3 Position { get; }
        public float Radius { get; }
        public float SafetyMargin { get; }
        public bool Enabled { get; }
        // Component lifecycle is independent of temporary Native navigation availability.
        // 旧调用默认沿用Enabled；生产者可单独报告真实actor组件仍active。
        public bool ActorActive { get; }
        public bool IndependentlyProgressing { get; }
        public float RemainingSeconds { get; }
        // Runtime identity only; the same ActorId does not identify a replacement actor instance.
        // 只读实际实例身份，不序列化，也不分配第二份身体 ownership。
        public object ActorLife { get; }
        public TrafficBodyFact(TrafficIntentKey owner, Vector3 position, float radius, bool enabled = true,
            bool independentlyProgressing = false, float remainingSeconds = 0, float safetyMargin = 0, object actorLife = null,
            bool? actorActive = null)
        {
            Owner = owner; Position = position; Radius = radius; Enabled = enabled;
            ActorActive = actorActive ?? enabled;
            IndependentlyProgressing = independentlyProgressing; RemainingSeconds = remainingSeconds;
            SafetyMargin = safetyMargin;
            ActorLife = actorLife;
        }
    }

    // Exact claim compatibility only; this evidence never grants a reservation or lease.
    public interface ITrafficClaimCheckpoint
    {
        object CanonicalOwner { get; }
        string ResourceId { get; }
        long CapturedOwnerVersion { get; }
        bool IsSameCurrentClaim(ITrafficClaimCheckpoint current);
    }
    public static class TrafficClaimCheckpoints
    {
        public static bool AreSame(TrafficParkingFact captured,TrafficParkingFact current)
        {
            if(captured==null || current==null)return false;
            var old=captured.SemanticCheckpoint as ITrafficClaimCheckpoint;
            var now=current.SemanticCheckpoint as ITrafficClaimCheckpoint;
            if(old==null && now==null)return ReferenceEquals(captured.SemanticCheckpoint,current.SemanticCheckpoint);
            if(old==null || now==null)return false; // A typed marker never falls back, even when it is the same object.
            try
            {
                bool Bound()=>captured.CanonicalOwner!=null && ReferenceEquals(captured.CanonicalOwner,current.CanonicalOwner) &&
                    captured.Owner.Equals(current.Owner) && captured.ResourceId!=null && captured.ResourceId==current.ResourceId &&
                    ReferenceEquals(old.CanonicalOwner,captured.CanonicalOwner) && ReferenceEquals(now.CanonicalOwner,current.CanonicalOwner) &&
                    old.ResourceId==captured.ResourceId && now.ResourceId==current.ResourceId &&
                    old.CapturedOwnerVersion==captured.OwnerVersion && now.CapturedOwnerVersion==current.OwnerVersion &&
                    old.CapturedOwnerVersion<=now.CapturedOwnerVersion;
                return Bound() && old.IsSameCurrentClaim(now) && Bound();
            }
            catch { return false; }
        }
    }

    // Read-only canonical reference, never a second reservation allocator. / 原owner仍拥有业务预留。
    public sealed class TrafficParkingFact
    {
        public TrafficIntentKey Owner { get; }
        public object CanonicalOwner { get; }
        public long OwnerVersion { get; }
        public string ResourceId { get; }
        public Vector3 Position { get; }
        public float Radius { get; }
        public float SafetyMargin { get; }
        // Opaque references to the existing business and release owners, never copied business state.
        // 仅保存原业务与释放义务的实例身份；不建立第二份订单或容量账本。
        public object BusinessOwner { get; }
        public object ReleaseObligation { get; }
        public object SemanticCheckpoint { get; } // Optional inert value; unknown/null proves no release.
        public TrafficParkingFact(TrafficIntentKey owner, object canonicalOwner, long ownerVersion,
            string resourceId, Vector3 position, float radius, float safetyMargin = 0,
            object businessOwner = null, object releaseObligation = null, object semanticCheckpoint = null)
        {
            Owner = owner; CanonicalOwner = canonicalOwner; OwnerVersion = ownerVersion;
            ResourceId = resourceId; Position = position; Radius = radius;
            SafetyMargin = safetyMargin;
            BusinessOwner = businessOwner; ReleaseObligation = releaseObligation; SemanticCheckpoint = semanticCheckpoint;
        }
    }

    public sealed class TrafficWorldSnapshot
    {
        public long Version { get; }
        public long BodyVersion { get; }
        public IReadOnlyList<TrafficBodyFact> Bodies { get; }
        public IReadOnlyList<TrafficParkingFact> Parking { get; }
        public TrafficWorldSnapshot(long version, long bodyVersion,
            IEnumerable<TrafficBodyFact> bodies = null, IEnumerable<TrafficParkingFact> parking = null)
        {
            Version = version; BodyVersion = bodyVersion;
            Bodies = Array.AsReadOnly((bodies ?? Enumerable.Empty<TrafficBodyFact>()).ToArray());
            Parking = Array.AsReadOnly((parking ?? Enumerable.Empty<TrafficParkingFact>()).ToArray());
        }
    }

    // Each requirement is a real route proof, e.g. delivery+return or existing customer goal.
    // 能力检查不登记未选定的未来路线；所有query共用同一意图预算。
    public sealed class TrafficRouteRequirement
    {
        public string ResourceId { get; }
        public Vector3 Start { get; }
        public Vector3 Goal { get; }
        public float Clearance { get; }
        public IReadOnlyList<Vector3> Obstacles { get; }
        public bool SelectedJourney { get; }
        public bool AvoidProposedParking { get; }
        public TrafficRouteRequirement(string resourceId, Vector3 start, Vector3 goal, float clearance,
            IEnumerable<Vector3> obstacles = null,bool selectedJourney=false,bool avoidProposedParking=true)
        {
            ResourceId = resourceId; Start = start; Goal = goal; Clearance = clearance;
            Obstacles = Array.AsReadOnly((obstacles ?? Enumerable.Empty<Vector3>()).ToArray());
            SelectedJourney=selectedJourney;
            AvoidProposedParking=avoidProposedParking;
        }
    }

    public sealed class TrafficRequest
    {
        public TrafficIntentKey Intent { get; }
        public TrafficNativeStamp NativeStamp { get; }
        public int RequestedFrame { get; }
        public TrafficWorldSnapshot Snapshot { get; }
        public Vector3 Start { get; }
        public Vector3 Target { get; }
        public float Radius { get; }
        public float SafetyMargin { get; }
        public string ResourceId { get; }
        public IReadOnlyList<Vector3> SelectedJourney { get; }
        public IReadOnlyList<TrafficRouteRequirement> Requirements { get; }
        public bool IndependentlyProgressing { get; }
        public float FiniteSeconds { get; }
        public TrafficDecisionKind? ProvenFailure { get; }
        public bool PlanPrimaryRoute { get; }
        public TrafficRequest(TrafficIntentKey intent, int requestedFrame, TrafficWorldSnapshot snapshot,
            Vector3 start, Vector3 target, float radius, string resourceId,
            IEnumerable<Vector3> selectedJourney = null, IEnumerable<TrafficRouteRequirement> requirements = null,
            TrafficNativeStamp nativeStamp = default, bool independentlyProgressing = true,
            float finiteSeconds = 30, TrafficDecisionKind? provenFailure = null, float safetyMargin = 0,bool planPrimaryRoute=false)
        {
            Intent = intent; RequestedFrame = requestedFrame; Snapshot = snapshot; Start = start;
            Target = target; Radius = radius; ResourceId = resourceId; NativeStamp = nativeStamp;
            SelectedJourney = Array.AsReadOnly((selectedJourney ?? Enumerable.Empty<Vector3>()).ToArray());
            Requirements = Array.AsReadOnly((requirements ?? Enumerable.Empty<TrafficRouteRequirement>()).ToArray());
            IndependentlyProgressing = independentlyProgressing; FiniteSeconds = finiteSeconds;
            ProvenFailure = provenFailure;
            SafetyMargin = safetyMargin;
            PlanPrimaryRoute=planPrimaryRoute;
        }
    }

    public readonly struct TrafficBlocker
    {
        public TrafficIntentKey Owner { get; }
        public string ResourceId { get; }
        public string Proof { get; }
        public TrafficBlocker(TrafficIntentKey owner, string resourceId, string proof)
        { Owner = owner; ResourceId = resourceId; Proof = proof; }
    }

    // Retained Native proof, not permission to occupy an unselected future exit.
    // waypoint用于发腿，ExpandedPath用于几何核验；消费时不得再次调用Native planner。
    public sealed class TrafficRouteProof
    {
        public string ResourceId { get; }
        public IReadOnlyList<Vector3> Waypoints { get; }
        public IReadOnlyList<Vector3> ExpandedPath { get; }
        public IReadOnlyList<int> LegEnds { get; }
        public TrafficRouteProof(string resourceId,IEnumerable<Vector3> waypoints,IEnumerable<Vector3> expandedPath,IEnumerable<int> legEnds=null)
        {
            ResourceId=resourceId;
            Waypoints=Array.AsReadOnly((waypoints??Enumerable.Empty<Vector3>()).ToArray());
            ExpandedPath=Array.AsReadOnly((expandedPath??Enumerable.Empty<Vector3>()).ToArray());
            LegEnds=Array.AsReadOnly((legEnds??Enumerable.Empty<int>()).ToArray());
        }
    }

    public sealed class TrafficDecision
    {
        public TrafficIntentKey Intent { get; }
        public TrafficDecisionKind Kind { get; }
        public string Reason { get; }
        public TrafficFailureDisposition FailureDisposition { get; }
        public long Permit { get; }
        public long RequestSequence { get; }
        public long AcquiredSequence { get; internal set; }
        public IReadOnlyList<TrafficBlocker> Blockers { get; }
        public IReadOnlyList<Vector3> Route { get; }
        public IReadOnlyList<TrafficRouteProof> ProofRoutes { get; }
        public TrafficDecision(TrafficIntentKey intent, TrafficDecisionKind kind, string reason,
            long permit = 0, long requestSequence = 0, long acquiredSequence = 0,
            IEnumerable<TrafficBlocker> blockers = null, IEnumerable<Vector3> route = null,IEnumerable<TrafficRouteProof> proofRoutes=null,
            TrafficFailureDisposition failureDisposition=TrafficFailureDisposition.None)
        {
            Intent = intent; Kind = kind; Reason = reason; Permit = permit;
            FailureDisposition = failureDisposition;
            RequestSequence = requestSequence; AcquiredSequence = acquiredSequence;
            Blockers = Array.AsReadOnly((blockers ?? Enumerable.Empty<TrafficBlocker>()).ToArray());
            Route = Array.AsReadOnly((route ?? Enumerable.Empty<Vector3>()).ToArray());
            ProofRoutes=Array.AsReadOnly((proofRoutes??Enumerable.Empty<TrafficRouteProof>()).ToArray());
        }
    }

    public enum TrafficPhaseMilestoneKind { Arrived, Collected }
    public enum TrafficPhaseClaimState { Invalid, Held, Released }

    // Producer reads the original canonical owner; source claims authenticate these opaque references.
    // 生产者只读真实原 owner；两次传入相同任意 object 不能替代 source claim 的身份验证。
    public interface ITrafficPhaseMilestone
    {
        TrafficDecision SourcePermit { get; }
        TrafficParkingFact OriginalClaim { get; }
        TrafficParkingFact CurrentClaim { get; }
        object ActorLife { get; }
        object BusinessOwner { get; }
        object ReleaseObligation { get; }
        TrafficPhaseMilestoneKind Kind { get; }
        bool IsCurrent { get; }
        // Read each exact fact from the saved strict snapshot against its original canonical Entry/lease.
        // Invalid不是Released；只有真实释放可返回Released/null，仍owned必须返回经原owner验证的当前fact。
        TrafficPhaseClaimState ReadClaim(TrafficParkingFact captured,out TrafficParkingFact current);
        // Read the actual canonical continuation, including its exact retained source and phase birth.
        // 必须读取原业务/释放义务当前准许的接续；不能用传入的候选自己盖章，也不能补洗提前创建的年龄。
        bool AuthorizesSuccessor(TrafficIntentKey successor,TrafficDecision sourcePermit,int firstFrame);
    }

    // Only the coordinator may mint a pending handoff; callers cannot construct a permission DTO.
    public sealed class TrafficPhaseHandoff
    {
        internal object Coordinator { get; }
        internal TrafficIntentKey Predecessor { get; }
        internal TrafficPhaseHandoff(object coordinator,TrafficIntentKey predecessor)
        { Coordinator=coordinator;Predecessor=predecessor; }
    }

    // Apply/Rollback must be synchronous and must not publish events. / 共同提交后才通知外部。
    public interface IPreparedTrafficMutation
    {
        bool IsCurrent { get; }
        bool TryApply(out ITrafficMutationReceipt receipt);
    }
    public interface ITrafficMutationReceipt
    {
        bool IsCurrent { get; }
        void RollbackOwnMutation();
    }
}

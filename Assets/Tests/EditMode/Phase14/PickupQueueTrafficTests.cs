using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Customers;
using AnimalCafe.Navigation;
using NUnit.Framework;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase14
{
    public sealed class PickupQueueTrafficTests
    {
        // CFC producer + analytic narrow-map Core proof. This is not Native movement acceptance.
        // 用真实PQS reservation与实际组件位置测试能力声明；不构造或授予任何Core permission。
        private sealed class PickupGoalExitFixture : IDisposable
        {
            internal readonly GameObject Root=new GameObject("existing pickup exit capability");
            internal readonly CustomerFlowController Flow;
            internal readonly NavigationActor Earlier,Incoming;
            internal readonly PickupQueueService Pickup=new PickupQueueService(new[]{P(0),P(0,2)},new[]{P(3)});
            internal readonly CounterQueueService Counter=new CounterQueueService(new[]{P(8,8)});
            internal readonly TrafficIntentKey EarlierKey=new TrafficIntentKey("exit-capability",1,"earlier","pickup-paid-movement",1,7,"original");
            internal readonly TrafficIntentKey IncomingKey=new TrafficIntentKey("exit-capability",1,"candidate","pickup-wait",2,7,"original");
            internal readonly TrafficIntentKey CounterKey=new TrafficIntentKey("exit-capability",1,"counter-owner","counter",1,7,"original");
            internal readonly object EarlierVisit;internal readonly System.Collections.IDictionary Visits;
            internal readonly TrafficWorldSnapshot Snapshot;internal int QueryCalls;
            private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            internal PickupGoalExitFixture(bool moving=false)
            {
                Flow=Root.AddComponent<CustomerFlowController>();
                typeof(CustomerFlowController).GetField("world",Fields).SetValue(Flow,Root.AddComponent<NavigationWorld>());
                var e=new GameObject("earlier actual body");e.transform.SetParent(Root.transform);Earlier=e.AddComponent<NavigationActor>();e.transform.position=moving?P(-4):P(0);
                var c=new GameObject("incoming actual body");c.transform.SetParent(Root.transform);Incoming=c.AddComponent<NavigationActor>();c.transform.position=P(4,2);
                Earlier.Configure(EarlierKey.ActorId,null,null,null,e.transform);Incoming.Configure(IncomingKey.ActorId,null,null,null,c.transform);
                var exit=new GameObject("actual exit");exit.transform.SetParent(Root.transform);exit.transform.position=P(0,4);
                typeof(CustomerFlowController).GetField("exit",Fields).SetValue(Flow,exit.transform);
                if(moving)
                {
                    Assert.That(Pickup.TryReserve(EarlierKey.ActorId,out _),Is.True);
                    Assert.That(Pickup.TryBeginTransfer(EarlierKey.ActorId,out _),Is.True);
                    Assert.That(Pickup.Snapshot.Single().Arrived,Is.False,"Moving fixture has never called MarkArrived");
                }
                else PaidArrived(Pickup,EarlierKey.ActorId);
                Assert.That(Pickup.TryCaptureReservation(EarlierKey.ActorId,out var held),Is.True);
                Assert.That(Counter.TryJoin(CounterKey.ActorId),Is.True);Assert.That(Counter.Complete(CounterKey.ActorId,1),Is.True);
                Assert.That(Counter.TryCaptureAssignment(CounterKey.ActorId,out var counter),Is.True);
                var visitType=typeof(CustomerFlowController).GetNestedType("Visit",BindingFlags.NonPublic);
                EarlierVisit=Activator.CreateInstance(visitType,true);
                void Set(string field,object value)=>visitType.GetField(field,Fields).SetValue(EarlierVisit,value);
                Set("Id",EarlierKey.ActorId);Set("Actor",Earlier);Set("PickupAssigned",true);Set("PickupInTransit",true);
                Set("State",moving?CustomerVisitState.MovingToPickup:CustomerVisitState.PickupWaiting);Set("PickupOwner",Pickup);
                Visits=(System.Collections.IDictionary)typeof(CustomerFlowController).GetField("visits",Fields).GetValue(Flow);Visits.Add(EarlierKey.ActorId,EarlierVisit);
                Snapshot=new TrafficWorldSnapshot(1,1,new[]{new TrafficBodyFact(EarlierKey,Earlier.transform.position,.45f,actorLife:Earlier),
                    new TrafficBodyFact(IncomingKey,Incoming.transform.position,.45f,actorLife:Incoming),new TrafficBodyFact(CounterKey,P(8,8),.45f,actorLife:Counter)},
                    new[]{new TrafficParkingFact(EarlierKey,Pickup,Pickup.Version,"pickup/"+EarlierKey.ActorId,held.Assignment.Position,.45f,.011f,EarlierVisit,held,held),
                    new TrafficParkingFact(CounterKey,Counter,Counter.Version,"counter/"+CounterKey.ActorId,counter.Assignment.Position,.45f,.011f,Counter,counter,counter)});
                typeof(CustomerFlowController).GetField("trafficSnapshot",Fields).SetValue(Flow,Snapshot);
            }
            internal TrafficRouteRequirement[] Roles(object incoming=null,bool preserveExitCapability=true)=>((IEnumerable<TrafficRouteRequirement>)typeof(CustomerFlowController)
                .GetMethod("PreservePickupGoals",Fields).Invoke(Flow,new object[]{incoming,preserveExitCapability})).ToArray();
            internal TrafficRouteRequirement[] RolesWithProductionDefault()=>((IEnumerable<TrafficRouteRequirement>)typeof(CustomerFlowController)
                .GetMethod("PreservePickupGoals",Fields).Invoke(Flow,new object[]{null,Type.Missing})).ToArray();
            internal void SetOwner(string field,object value)=>EarlierVisit.GetType().GetField(field,Fields).SetValue(EarlierVisit,value);
            internal ServiceTrafficCoordinator Core()=>new ServiceTrafficCoordinator("exit-capability",1,7,Snapshot,NarrowMap,128,16384,10);
            private IReadOnlyList<Vector3> NarrowMap(Vector3 from,Vector3 to)
            {
                QueryCalls++;
                bool Vertical(Vector3 p)=>Mathf.Abs(p.x)<.0001f && p.z>=0 && p.z<=4;
                bool Horizontal(Vector3 p)=>Mathf.Abs(p.z-2)<.0001f && p.x>=0 && p.x<=4;
                bool Approach(Vector3 p)=>Mathf.Abs(p.z)<.0001f && p.x>=-4 && p.x<=0;
                return Vertical(from) && Vertical(to) || Horizontal(from) && Horizontal(to) || Approach(from) && Approach(to)?new[]{from,to}:null;
            }
            public void Dispose(){Visits.Clear();UnityEngine.Object.DestroyImmediate(Root);}
        }
        [TestCase(false)][TestCase(true)]
        public void CfcOtherPhasesKeepDefaultActualGoalScopeWithoutFutureExitCapability(bool moving)
        {
            using(var f=new PickupGoalExitFixture(moving))
            {
                var version=f.Pickup.Version;Assert.That(f.Pickup.TryCaptureReservation(f.EarlierKey.ActorId,out var held),Is.True);
                // Type.Missing invokes the real optional default used by Counter/Admission/Paid/Employee callers.
                // 验证原调用的默认范围，不把新PQS parking能力扩到其它相位。
                var roles=f.RolesWithProductionDefault();
                Assert.That(roles.Any(role=>role.ResourceId.EndsWith("/exit-capability",StringComparison.Ordinal)),Is.False);
                if(moving)Assert.That(roles,Is.Empty,"Keep the original moving-owner actual-to-goal phase filter");
                else
                {
                    Assert.That(roles.Select(role=>role.ResourceId),Is.EqualTo(new[]{"existing-goal/earlier"}));
                    Assert.That(roles.Single().Start,Is.EqualTo(f.Earlier.transform.position));
                    Assert.That(roles.Single().Goal,Is.EqualTo(held.Assignment.Position));
                    Assert.That(roles.Single().SelectedJourney,Is.False);Assert.That(roles.Single().AvoidProposedParking,Is.True);
                }
                Assert.That(held.IsCurrent,Is.True);Assert.That(f.Pickup.Version,Is.EqualTo(version));Assert.That(f.QueryCalls,Is.Zero);
            }
        }
        [Test]
        public void CfcOwnedArrivedPickupPublishesNonexclusiveExitCapabilityWithOriginalObstacles()
        {
            using(var f=new PickupGoalExitFixture())
            {
                var version=f.Pickup.Version;Assert.That(f.Pickup.TryCaptureReservation(f.EarlierKey.ActorId,out var original),Is.True);
                var roles=f.Roles();
                Assert.That(roles.Select(r=>r.ResourceId),Is.EqualTo(new[]{"existing-goal/earlier","existing-goal/earlier/exit-capability"}),
                    "FEATURE: arrival at an owned target does not prove that target can still reach Exit after the new parking point");
                var exit=roles.Single(r=>r.ResourceId.EndsWith("/exit-capability",StringComparison.Ordinal));
                Assert.That(exit.Start,Is.EqualTo(original.Assignment.Position));Assert.That(exit.Goal,Is.EqualTo(P(0,4)));
                Assert.That(exit.SelectedJourney,Is.False,"Capability is no selected/exclusive future route");Assert.That(exit.AvoidProposedParking,Is.True);
                Assert.That(exit.Clearance,Is.EqualTo(roles[0].Clearance));CollectionAssert.AreEquivalent(roles[0].Obstacles,exit.Obstacles);
                CollectionAssert.AreEquivalent(new[]{f.Incoming.transform.position,P(8,8)},exit.Obstacles,"Original actual body and real Counter claim remain obstacles");
                Assert.That(f.Pickup.Version,Is.EqualTo(version));Assert.That(original.IsCurrent,Is.True);Assert.That(f.QueryCalls,Is.Zero);
            }
        }
        [TestCase("incoming")][TestCase("removed")]
        public void CfcExitCapabilityRequiresAnotherCurrentOwnedPickup(string reason)
        {
            using(var f=new PickupGoalExitFixture())
            {
                if(reason=="removed")Assert.That(f.Pickup.Remove(f.EarlierKey.ActorId),Is.True);
                Assert.That(f.Roles(reason=="incoming"?f.EarlierVisit:null),Is.Empty);
                Assert.That(f.QueryCalls,Is.Zero);
            }
        }
        [TestCase(false)][TestCase(true)]
        public void CfcCollectedRetainedPickupCannotBeSealedByNewParking(bool blocksExit)
        {
            using(var f=new PickupGoalExitFixture())
            {
                // Collection does not release the actual canonical PQS reservation or move the body.
                // HasCollected只是业务事实；真实离位/Remove之前仍保留原拥有目标的离店能力。
                f.SetOwner("HasCollected",true);f.SetOwner("State",CustomerVisitState.Exiting);
                Assert.That(f.Pickup.TryCaptureReservation(f.EarlierKey.ActorId,out var held),Is.True);
                Assert.That(held.Assignment.Arrived,Is.True);Assert.That(held.Assignment.Position,Is.EqualTo(f.Earlier.transform.position));
                var version=f.Pickup.Version;var roles=f.Roles();var core=f.Core();
                Assert.That(f.RolesWithProductionDefault(),Is.Empty,"Other phase callers retain the original collected-owner actual-to-goal exclusion");
                var request=new TrafficRequest(f.IncomingKey,11,f.Snapshot,f.Incoming.transform.position,blocksExit?P(0,2):P(3,2),.45f,
                    "pickup/candidate",requirements:roles,safetyMargin:.011f,planPrimaryRoute:true);
                core.EvaluateParking(request,0,2);
                for(var frame=11;frame<211 && core.GetDecision(f.IncomingKey).Kind==TrafficDecisionKind.DeferredByBudget;frame++)core.ProcessReady(frame);
                var result=core.GetDecision(f.IncomingKey);
                Assert.That(result.Kind,Is.EqualTo(blocksExit?TrafficDecisionKind.PlanningFailed:TrafficDecisionKind.Granted),
                    "FEATURE: collected owner still holds its real pickup checkpoint until actual departure; later parking cannot seal its Exit");
                Assert.That(roles.Select(role=>role.ResourceId),Is.EqualTo(new[]{"existing-goal/earlier/exit-capability"}),
                    "Keep only the nonselected capability; do not restore a collected actor-to-pickup movement role");
                var capability=roles.Single();Assert.That(capability.Start,Is.EqualTo(held.Assignment.Position));Assert.That(capability.Goal,Is.EqualTo(P(0,4)));
                Assert.That(capability.SelectedJourney,Is.False);Assert.That(capability.AvoidProposedParking,Is.True);
                if(blocksExit)
                {
                    Assert.That(result.Reason,Is.EqualTo("bounded-route-proof-failed"));Assert.That(result.AcquiredSequence,Is.Zero);
                    Assert.That(core.TryCommit(result),Is.False);Assert.That(core.HasActiveGrant(f.IncomingKey),Is.False);
                }
                else Assert.That(result.ProofRoutes.Select(proof=>proof.ResourceId),Is.EquivalentTo(new[]{request.ResourceId,capability.ResourceId}));
                Assert.That(f.Pickup.Version,Is.EqualTo(version));Assert.That(held.IsCurrent,Is.True);
                Assert.That(f.Pickup.TryGetReservation(f.IncomingKey.ActorId,out _),Is.False);
                Assert.That(f.Pickup.Snapshot.Single().VisitId,Is.EqualTo(f.EarlierKey.ActorId));Assert.That(f.Earlier.transform.position,Is.EqualTo(P(0)));
                Assert.That(core.QueriesConsumed(f.IncomingKey),Is.EqualTo(f.QueryCalls));
            }
        }
        [Test]
        public void CfcCollectedRemovedPickupDoesNotRetainUnownedExitCapability()
        {
            using(var f=new PickupGoalExitFixture())
            {
                f.SetOwner("HasCollected",true);f.SetOwner("State",CustomerVisitState.Exiting);
                Assert.That(f.Pickup.TryCaptureReservation(f.EarlierKey.ActorId,out var held),Is.True);
                Assert.That(f.Pickup.Remove(f.EarlierKey.ActorId),Is.True,"Use actual canonical release, not a guessed HasCollected shortcut");
                Assert.That(held.IsCurrent,Is.False);var version=f.Pickup.Version;
                Assert.That(f.Roles(),Is.Empty);Assert.That(f.RolesWithProductionDefault(),Is.Empty);
                Assert.That(f.QueryCalls,Is.Zero);Assert.That(f.Pickup.Version,Is.EqualTo(version));
            }
        }

        [TestCase(false)][TestCase(true)]
        public void CfcNextParkingCannotSealEarlierArrivedGoalExitAndSafeParkingStillProves(bool blocksExit)
        {
            using(var f=new PickupGoalExitFixture())
            {
                var core=f.Core();var roles=f.Roles();var version=f.Pickup.Version;
                Assert.That(f.Pickup.TryCaptureReservation(f.EarlierKey.ActorId,out var held),Is.True);
                var target=blocksExit?P(0,2):P(3,2);
                var request=new TrafficRequest(f.IncomingKey,11,f.Snapshot,f.Incoming.transform.position,target,.45f,"pickup/candidate",
                    requirements:roles,safetyMargin:.011f,planPrimaryRoute:true);
                var pending=core.EvaluateParking(request,0,2);
                for(var frame=11;frame<211 && core.GetDecision(f.IncomingKey).Kind==TrafficDecisionKind.DeferredByBudget;frame++)core.ProcessReady(frame);
                var result=core.GetDecision(f.IncomingKey);
                Assert.That(result.Kind,Is.EqualTo(blocksExit?TrafficDecisionKind.PlanningFailed:TrafficDecisionKind.Granted),
                    "FEATURE: the old zero-length actual-to-goal role alone accepted a later parking point that seals the real earlier Exit capability");
                if(blocksExit)
                {
                    Assert.That(result.Reason,Is.EqualTo("bounded-route-proof-failed"));Assert.That(result.AcquiredSequence,Is.Zero);
                    Assert.That(core.TryCommit(result),Is.False);Assert.That(core.HasActiveGrant(f.IncomingKey),Is.False);
                }
                else Assert.That(result.ProofRoutes.Select(p=>p.ResourceId),Is.EquivalentTo(new[]{request.ResourceId,"existing-goal/earlier","existing-goal/earlier/exit-capability"}));
                Assert.That(f.Pickup.Version,Is.EqualTo(version));Assert.That(held.IsCurrent,Is.True);
                Assert.That(f.Pickup.Snapshot.Single().VisitId,Is.EqualTo(f.EarlierKey.ActorId));Assert.That(f.Pickup.TryGetReservation(f.IncomingKey.ActorId,out _),Is.False);
                Assert.That(f.QueryCalls,Is.GreaterThan(0));Assert.That(core.QueriesConsumed(f.IncomingKey),Is.EqualTo(f.QueryCalls));
            }
        }

        [TestCase(false)][TestCase(true)]
        public void CfcMovingOwnedPickupKeepsFutureExitCapabilityWithoutClaimingItsPath(bool blocksExit)
        {
            using(var f=new PickupGoalExitFixture(moving:true))
            {
                Assert.That(f.Pickup.TryCaptureReservation(f.EarlierKey.ActorId,out var held),Is.True);
                Assert.That(held.Assignment.Arrived,Is.False);Assert.That(held.Assignment.Position,Is.EqualTo(P(0)));
                Assert.That(f.Earlier.transform.position,Is.EqualTo(P(-4)));
                var core=f.Core();var earlierRequest=new TrafficRequest(f.EarlierKey,1,f.Snapshot,f.Earlier.transform.position,held.Assignment.Position,.45f,
                    "pickup/earlier",safetyMargin:.011f,planPrimaryRoute:true);
                core.EvaluateMovement(earlierRequest);core.ProcessReady(1);var earlier=core.GetDecision(f.EarlierKey);
                Assert.That(earlier.Kind,Is.EqualTo(TrafficDecisionKind.Granted));Assert.That(core.TryCommit(earlier),Is.True);
                Assert.That(core.HasActiveGrant(f.EarlierKey),Is.True,"Actual original Core owns the approach route; no fabricated Native stamp");
                var target=blocksExit?P(0,2):P(3,2);
                Assert.That(CustomerRoutePlanner.IsClear(earlier.Route,new[]{target},.991f),Is.True,"Later point does not block the existing selected approach");
                var roles=f.Roles();Assert.That(roles.Select(role=>role.ResourceId),Is.EqualTo(new[]{"existing-goal/earlier/exit-capability"}),
                    "FEATURE: moving owner still owns its real PQS goal; only the old actual-to-goal role stays phase-filtered");
                var exit=roles.Single();Assert.That(exit.Start,Is.EqualTo(P(0)));Assert.That(exit.Goal,Is.EqualTo(P(0,4)));
                Assert.That(exit.SelectedJourney,Is.False);Assert.That(exit.AvoidProposedParking,Is.True);
                var before=f.QueryCalls;var version=f.Pickup.Version;
                var request=new TrafficRequest(f.IncomingKey,11,f.Snapshot,f.Incoming.transform.position,target,.45f,"pickup/candidate",
                    requirements:roles,safetyMargin:.011f,planPrimaryRoute:true);
                core.EvaluateParking(request,0,2);
                for(var frame=11;frame<211 && core.GetDecision(f.IncomingKey).Kind==TrafficDecisionKind.DeferredByBudget;frame++)core.ProcessReady(frame);
                var result=core.GetDecision(f.IncomingKey);
                Assert.That(result.Kind,Is.EqualTo(blocksExit?TrafficDecisionKind.PlanningFailed:TrafficDecisionKind.Granted));
                if(blocksExit){Assert.That(result.Reason,Is.EqualTo("bounded-route-proof-failed"));Assert.That(core.TryCommit(result),Is.False);}
                else Assert.That(result.ProofRoutes.Select(proof=>proof.ResourceId),Is.EquivalentTo(new[]{request.ResourceId,exit.ResourceId}));
                Assert.That(core.HasActiveGrant(f.EarlierKey),Is.True);Assert.That(core.TryGetCommittedJourney(f.EarlierKey,out var retained),Is.True);
                Assert.That(retained,Is.SameAs(earlier));Assert.That(held.IsCurrent,Is.True);Assert.That(f.Pickup.Version,Is.EqualTo(version));
                Assert.That(f.Pickup.Snapshot.Single().Arrived,Is.False);Assert.That(f.Pickup.TryGetReservation(f.IncomingKey.ActorId,out _),Is.False);
                Assert.That(core.QueriesConsumed(f.IncomingKey),Is.EqualTo(f.QueryCalls-before));
                Assert.That(f.Earlier.transform.position,Is.EqualTo(P(-4)),"Contract proof cannot teleport or advance the older body");
            }
        }

        private static Vector3 P(float x,float z=0) => new Vector3(x,0,z);
        private static PickupQueueService Queue() => new PickupQueueService(new[]{P(0),P(2),P(4)},new[]{P(0,4),P(2,4)});
        private static bool Call(PickupQueueService queue,string method,params object[] args)
            => (bool)typeof(PickupQueueService).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(queue,args);
        private static void PaidArrived(PickupQueueService queue,string id)
        {
            Assert.That(queue.TryReserve(id,out _),Is.True);
            Assert.That(queue.TryBeginTransfer(id,out var assignment),Is.True);
            Assert.That(queue.MarkArrived(id,assignment.Generation),Is.True);
        }
        private static void Yield(PickupQueueService queue,string id,bool arrived)
        {
            var args=new object[]{id,P(6),default(QueueAssignment)};
            Assert.That(Call(queue,"TryBeginYield",args),Is.True);
            if(arrived) Assert.That(Call(queue,"MarkYieldArrived",id,((QueueAssignment)args[2]).Generation),Is.True);
        }

        private static bool CaptureCollectionRelease(PickupQueueService queue,string id,Vector3 target,
            out PickupQueueService.ReservationClaim reservation,out PickupQueueService.DeliveryLeaseClaim delivery)
        {
            var args=new object[]{id,target,null,null};
            var captured=Call(queue,"TryCaptureCollectionReleasePrerequisite",args);
            reservation=(PickupQueueService.ReservationClaim)args[2];
            delivery=(PickupQueueService.DeliveryLeaseClaim)args[3];return captured;
        }

        [Test]
        public void CollectionReleasePrerequisiteCapturesOnlyActualHeldLeaseAndPreservesCanonicalState()
        {
            var queue=Queue();PaidArrived(queue,"head");
            Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);
            Assert.That(queue.TryCaptureReservation("head",out var originalHead),Is.True);
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var originalLease),Is.True);
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.CollectionLease,"head",queue.Version,out var blocked,P(0,4)),Is.False);
            Assert.That(blocked,Is.Null,"Real owner rejects the requested collection while the delivery lease is held");
            var version=queue.Version;var rows=queue.Snapshot.ToArray();var leases=queue.ServiceLeases.ToArray();
            var captured=CaptureCollectionRelease(queue,"head",P(0,4),out var head,out var delivery);
            Assert.That(queue.Version,Is.EqualTo(version));CollectionAssert.AreEqual(rows,queue.Snapshot.ToArray());
            CollectionAssert.AreEqual(leases,queue.ServiceLeases.ToArray());
            Assert.That(captured,Is.True,"FEATURE: capture actual sole delivery blocker; deeper controls NOT_PROVEN until capture passes");
            Assert.That(head.IsCurrent,Is.True);Assert.That(head.IsSameReservation(originalHead),Is.True);
            Assert.That(head.Assignment,Is.EqualTo(originalHead.Assignment));Assert.That(head.IsCurrentCollectionLease,Is.False);
            Assert.That(delivery.IsCurrent,Is.True);Assert.That(delivery.IsSameLease(originalLease),Is.True);
            Assert.That(delivery.ActorId,Is.EqualTo("employee"));Assert.That(delivery.Position,Is.EqualTo(P(0,4)));
            Assert.That(CaptureCollectionRelease(queue,"head",P(0,4),out var repeatedHead,out var repeatedLease),Is.True);
            Assert.That(head.IsSameReservation(repeatedHead),Is.True);Assert.That(delivery.IsSameLease(repeatedLease),Is.True);
            Assert.That(queue.Version,Is.EqualTo(version));
            Assert.That(queue.TryReserve("unrelated",out _),Is.True);var unrelatedVersion=queue.Version;
            Assert.That(unrelatedVersion,Is.GreaterThan(version));Assert.That(head.IsCurrent,Is.True);Assert.That(delivery.IsCurrent,Is.True);
            Assert.That(CaptureCollectionRelease(queue,"head",P(0,4),out var afterHead,out var afterLease),Is.True);
            Assert.That(head.IsSameReservation(afterHead),Is.True);Assert.That(delivery.IsSameLease(afterLease),Is.True);
            Assert.That(queue.Version,Is.EqualTo(unrelatedVersion));
            queue.ReleaseServicePosition("employee");var releasedVersion=queue.Version;
            Assert.That(delivery.IsCurrent,Is.False);Assert.That(delivery.HasRetiredLifetime,Is.True);Assert.That(head.IsCurrent,Is.True);
            Assert.That(CaptureCollectionRelease(queue,"head",P(0,4),out var absentHead,out var absentLease),Is.False);
            Assert.That(absentHead,Is.Null);Assert.That(absentLease,Is.Null);Assert.That(queue.Version,Is.EqualTo(releasedVersion));
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.CollectionLease,"head",releasedVersion,out var actual,P(0,4)),Is.True);
            Assert.That(queue.Version,Is.EqualTo(releasedVersion),"Real preview remains uncommitted until explicit apply");
            Assert.That(actual.TryApply(out var receipt),Is.True);Assert.That(receipt.IsCurrent,Is.True);
            Assert.That(actual.TryApply(out _),Is.False);Assert.That(head.IsCurrent,Is.False);
            Assert.That(queue.TryGetServiceLease("head",out var position),Is.True);Assert.That(position,Is.EqualTo(P(0,4)));
        }

        [Test]
        public void CollectionReleasePrerequisiteCannotConfuseActualLeaseOrReservationReplacement()
        {
            var queue=Queue();PaidArrived(queue,"head");Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.CollectionLease,"head",queue.Version,out _,P(0,4)),Is.False);
            Assert.That(CaptureCollectionRelease(queue,"head",P(0,4),out var head,out var delivery),Is.True,
                "FEATURE: actual original pair is required before ABA/replacement controls are proven");
            queue.ReleaseServicePosition("employee");Assert.That(delivery.HasRetiredLifetime,Is.True);
            Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);
            Assert.That(CaptureCollectionRelease(queue,"head",P(0,4),out var sameHead,out var replacementLease),Is.True);
            Assert.That(head.IsSameReservation(sameHead),Is.True);Assert.That(head.IsCurrent,Is.True);
            Assert.That(delivery.IsSameLease(replacementLease),Is.False);Assert.That(delivery.IsCurrent,Is.False);
            Assert.That(replacementLease.IsCurrent,Is.True);
            Assert.That(queue.Remove("head"),Is.True);PaidArrived(queue,"head");
            Assert.That(CaptureCollectionRelease(queue,"head",P(0,4),out var replacementHead,out var sameLease),Is.True);
            Assert.That(head.IsCurrent,Is.False);Assert.That(head.IsSameReservation(replacementHead),Is.False);
            Assert.That(replacementHead.IsCurrent,Is.True);Assert.That(replacementLease.IsSameLease(sameLease),Is.True);
            var foreign=Queue();PaidArrived(foreign,"head");Assert.That(foreign.TryAcquireDelivery("employee",P(0,4)),Is.True);
            Assert.That(CaptureCollectionRelease(foreign,"head",P(0,4),out var foreignHead,out var foreignLease),Is.True);
            Assert.That(replacementHead.IsSameReservation(foreignHead),Is.False);Assert.That(sameLease.IsSameLease(foreignLease),Is.False);
        }

        [TestCase("no-lease")]
        [TestCase("free-side")]
        [TestCase("wrong-head")]
        [TestCase("unarrived")]
        [TestCase("collection-owned")]
        [TestCase("yielded")]
        public void CollectionReleasePrerequisiteRejectsMissingOrNonSoleDeliveryBlocker(string premise)
        {
            var queue=Queue();
            if(premise=="unarrived")
            {Assert.That(queue.TryReserve("head",out _),Is.True);Assert.That(queue.TryBeginTransfer("head",out _),Is.True);}
            else PaidArrived(queue,"head");
            if(premise=="wrong-head")PaidArrived(queue,"tail");
            if(premise=="collection-owned")Assert.That(queue.TryAcquireCollection("head",P(2,4)),Is.True);
            if(premise=="yielded")
            {
                Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.BeginYield,"head",queue.Version,out var yielding,P(6)),Is.True);
                Assert.That(yielding.TryApply(out _),Is.True);
                Assert.That(queue.MarkArrived("head",yielding.Assignment.Generation),Is.True);
            }
            if(premise!="no-lease")Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);
            var id=premise=="wrong-head"?"tail":"head";var target=premise=="free-side"?P(2,4):P(0,4);
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.CollectionLease,id,queue.Version,out _,target),
                Is.EqualTo(premise=="no-lease" || premise=="free-side"),"Actual owner eligibility establishes the case, without injected Entry facts");
            var version=queue.Version;var rows=queue.Snapshot.ToArray();var leases=queue.ServiceLeases.ToArray();
            Assert.That(CaptureCollectionRelease(queue,id,target,out var head,out var delivery),Is.False);
            Assert.That(head,Is.Null);Assert.That(delivery,Is.Null);Assert.That(queue.Version,Is.EqualTo(version));
            CollectionAssert.AreEqual(rows,queue.Snapshot.ToArray());CollectionAssert.AreEqual(leases,queue.ServiceLeases.ToArray());
        }

        // M1 prospective lease evidence: deeper guards are NOT_PROVEN until capture is implemented.
        // 先经真实public API证明lease存在；FEATURE失败之后的断言不可冒充已验证。
        [Test]
        public void DeliveryLeaseClaimRejectsAbsentAndWrongActorWithoutCanonicalWrites()
        {
            var queue=Queue();var emptyVersion=queue.Version;
            foreach(var id in new string[]{null,""," employee","missing"})
            {Assert.That(queue.TryCaptureDeliveryLease(id,out var absent),Is.False);Assert.That(absent,Is.Null);}
            Assert.That(queue.Version,Is.EqualTo(emptyVersion));Assert.That(queue.ServiceLeases,Is.Empty);
            Assert.That(queue.Snapshot,Is.Empty);
            Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);
            Assert.That(queue.TryGetServiceLease("employee",out var actual),Is.True);Assert.That(actual,Is.EqualTo(P(0,4)));
            var version=queue.Version;
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var held),Is.True,
                "FEATURE: capture must observe the actual acquired lease; wrong-actor guard below is NOT_PROVEN before this passes");
            Assert.That(held,Is.Not.Null);
            foreach(var id in new string[]{null,""," employee","stranger"})
            {Assert.That(queue.TryCaptureDeliveryLease(id,out var wrong),Is.False);Assert.That(wrong,Is.Null);}
            queue.ReleaseServicePosition("stranger");
            Assert.That(held.IsCurrent,Is.True);Assert.That(held.HasRetiredLifetime,Is.False);
            Assert.That(queue.Version,Is.EqualTo(version));Assert.That(queue.ServiceLeases.Count,Is.EqualTo(1));
        }

        [Test]
        public void DeliveryLeaseClaimReadsActualStoredLegacyLeaseWithoutCanonicalWrites()
        {
            var queue=new PickupQueueService(new[]{P(0),P(2),P(4)},P(0,4),false);
            Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);var version=queue.Version;
            // Legacy ServiceSides==null accepts an idempotent request at another position; it keeps the stored lease.
            // 保留现有legacy语义；claim必须报告真实stored position，不能偷改请求规则。
            Assert.That(queue.TryAcquireDelivery("employee",P(2,4)),Is.True);
            Assert.That(queue.TryGetServiceLease("employee",out var actual),Is.True);Assert.That(actual,Is.EqualTo(P(0,4)));
            Assert.That(queue.Version,Is.EqualTo(version));var leases=queue.ServiceLeases.ToArray();
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var first),Is.True,
                "FEATURE: the real stored legacy lease must be capturable");
            Assert.That(first,Is.Not.Null);Assert.That(first.ActorId,Is.EqualTo("employee"));Assert.That(first.Position,Is.EqualTo(P(0,4)));
            Assert.That(first.IsCurrent,Is.True);Assert.That(first.HasRetiredLifetime,Is.False);
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var second),Is.True);
            Assert.That(first.IsSameLease(second),Is.True);Assert.That(second.IsSameLease(first),Is.True);
            Assert.That(first.IsSameLease(null),Is.False);
            CollectionAssert.AreEqual(leases,queue.ServiceLeases.ToArray());Assert.That(queue.Snapshot,Is.Empty);
            Assert.That(queue.Version,Is.EqualTo(version),"Capture and read-only evidence cannot write canonical state");
        }

        [Test]
        public void DeliveryLeaseClaimIdempotentAcquireAndUnrelatedVersionKeepSameLifetime()
        {
            var queue=Queue();Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);
            Assert.That(queue.TryGetServiceLease("employee",out var actual),Is.True);Assert.That(actual,Is.EqualTo(P(0,4)));
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var original),Is.True,
                "FEATURE: establish real original lease evidence before idempotence/version guards");
            var version=queue.Version;
            Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);Assert.That(queue.Version,Is.EqualTo(version));
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var repeated),Is.True);
            Assert.That(original.IsSameLease(repeated),Is.True);Assert.That(repeated.IsSameLease(original),Is.True);
            Assert.That(queue.TryReserve("unrelated-customer",out _),Is.True);Assert.That(queue.Version,Is.GreaterThan(version));
            var changedVersion=queue.Version;
            Assert.That(original.IsCurrent,Is.True,"A different customer's Queue.Version change cannot retire a held staff lease");
            Assert.That(original.HasRetiredLifetime,Is.False);
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var afterOtherMutation),Is.True);
            Assert.That(original.IsSameLease(afterOtherMutation),Is.True);Assert.That(afterOtherMutation.Position,Is.EqualTo(P(0,4)));
            Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);Assert.That(queue.Version,Is.EqualTo(changedVersion));
        }

        [Test]
        public void DeliveryLeaseClaimActualReleaseAndSamePositionReacquireRetireOldLifetime()
        {
            var queue=Queue();Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);
            Assert.That(queue.TryGetServiceLease("employee",out var actual),Is.True);Assert.That(actual,Is.EqualTo(P(0,4)));
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var original),Is.True,
                "FEATURE: acquire/capture premise must pass before release/reacquire ABA is proven");
            var acquiredVersion=queue.Version;queue.ReleaseServicePosition("employee");
            Assert.That(queue.TryGetServiceLease("employee",out _),Is.False);Assert.That(queue.Version,Is.GreaterThan(acquiredVersion));
            Assert.That(original.IsCurrent,Is.False);Assert.That(original.HasRetiredLifetime,Is.True);
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var absent),Is.False);Assert.That(absent,Is.Null);
            var releasedVersion=queue.Version;queue.ReleaseServicePosition("employee");Assert.That(queue.Version,Is.EqualTo(releasedVersion));
            Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);
            Assert.That(queue.TryGetServiceLease("employee",out var reacquired),Is.True);Assert.That(reacquired,Is.EqualTo(P(0,4)));
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var current),Is.True);
            Assert.That(current.IsCurrent,Is.True);Assert.That(current.HasRetiredLifetime,Is.False);
            Assert.That(original.IsSameLease(current),Is.False);Assert.That(current.IsSameLease(original),Is.False);
            Assert.That(original.IsCurrent,Is.False);Assert.That(original.HasRetiredLifetime,Is.True,"Identical actor/position cannot resurrect retired evidence");
        }

        [Test]
        public void DeliveryLeaseClaimForeignQueueCannotShareOrRetireCanonicalLifetime()
        {
            var queue=Queue();var foreign=Queue();
            Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);
            Assert.That(foreign.TryAcquireDelivery("employee",P(0,4)),Is.True);
            Assert.That(queue.TryGetServiceLease("employee",out var actual),Is.True);Assert.That(actual,Is.EqualTo(P(0,4)));
            Assert.That(foreign.TryGetServiceLease("employee",out var foreignActual),Is.True);Assert.That(foreignActual,Is.EqualTo(P(0,4)));
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var canonical),Is.True,
                "FEATURE: actual canonical lease capture precedes foreign-owner guard");
            Assert.That(foreign.TryCaptureDeliveryLease("employee",out var other),Is.True);
            Assert.That(canonical.IsSameLease(other),Is.False);Assert.That(other.IsSameLease(canonical),Is.False);
            var version=queue.Version;foreign.ReleaseServicePosition("employee");
            Assert.That(other.IsCurrent,Is.False);Assert.That(other.HasRetiredLifetime,Is.True);
            Assert.That(canonical.IsCurrent,Is.True);Assert.That(canonical.HasRetiredLifetime,Is.False);
            Assert.That(queue.TryGetServiceLease("employee",out var preserved),Is.True);Assert.That(preserved,Is.EqualTo(P(0,4)));
            Assert.That(queue.Version,Is.EqualTo(version));
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var stillCanonical),Is.True);
            Assert.That(canonical.IsSameLease(stillCanonical),Is.True);
        }

        [Test]
        public void DeliveryLeaseClaimAppliedAcquisitionRollbackRetiresOnlyItsActualLifetime()
        {
            var queue=Queue();Assert.That(queue.TryReserve("customer",out var customer),Is.True);var before=queue.Version;
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.DeliveryLease,"employee",before,out var prepared,P(0,4)),Is.True);
            Assert.That(prepared.TryApply(out var receipt),Is.True);Assert.That(receipt,Is.Not.Null);Assert.That(receipt.IsCurrent,Is.True);
            Assert.That(queue.Version,Is.GreaterThan(before));
            Assert.That(queue.TryGetServiceLease("employee",out var actual),Is.True);Assert.That(actual,Is.EqualTo(P(0,4)));
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var applied),Is.True,
                "FEATURE: receipt really acquired the lease before rollback-retirement evidence is tested");
            var appliedVersion=queue.Version;receipt.RollbackOwnMutation();
            Assert.That(queue.Version,Is.GreaterThan(appliedVersion));Assert.That(receipt.IsCurrent,Is.False);
            Assert.That(queue.TryGetServiceLease("employee",out _),Is.False);
            Assert.That(applied.IsCurrent,Is.False);Assert.That(applied.HasRetiredLifetime,Is.True);
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var absent),Is.False);Assert.That(absent,Is.Null);
            Assert.That(queue.TryGetReservation("customer",out var retained),Is.True);Assert.That(retained.Generation,Is.EqualTo(customer.Generation));
            Assert.That(retained.Position,Is.EqualTo(customer.Position));
            var rolledBack=queue.Version;Assert.DoesNotThrow(()=>receipt.RollbackOwnMutation());Assert.That(queue.Version,Is.EqualTo(rolledBack));
            Assert.That(queue.TryReserve("later-customer",out var later),Is.True);Assert.That(later.Generation,Is.GreaterThan(customer.Generation));
            Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var reacquired),Is.True);
            Assert.That(applied.IsSameLease(reacquired),Is.False);Assert.That(reacquired.IsCurrent,Is.True);
            Assert.That(applied.HasRetiredLifetime,Is.True,"Rollback cannot rewind a retired lifetime into a later acquisition");
        }

        [Test]
        public void DeliveryLeaseClaimIdempotentMutationRollbackPreservesExistingLifetime()
        {
            var queue=Queue();Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);
            Assert.That(queue.TryGetServiceLease("employee",out var actual),Is.True);Assert.That(actual,Is.EqualTo(P(0,4)));
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var original),Is.True,
                "FEATURE: held original lease must be captured before no-op rollback preservation is proven");
            var version=queue.Version;
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.DeliveryLease,"employee",version,out var prepared,P(0,4)),Is.True);
            Assert.That(prepared.TryApply(out var receipt),Is.True);Assert.That(receipt,Is.Not.Null);Assert.That(receipt.IsCurrent,Is.True);
            Assert.That(queue.Version,Is.EqualTo(version),"An idempotent actual acquire is a no-op");
            receipt.RollbackOwnMutation();Assert.That(receipt.IsCurrent,Is.False);
            Assert.That(queue.Version,Is.EqualTo(version));Assert.That(original.IsCurrent,Is.True);Assert.That(original.HasRetiredLifetime,Is.False);
            Assert.That(queue.TryGetServiceLease("employee",out var preserved),Is.True);Assert.That(preserved,Is.EqualTo(P(0,4)));
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var current),Is.True);
            Assert.That(original.IsSameLease(current),Is.True);Assert.That(current.IsSameLease(original),Is.True);
            Assert.DoesNotThrow(()=>receipt.RollbackOwnMutation());Assert.That(queue.Version,Is.EqualTo(version));Assert.That(original.IsCurrent,Is.True);
        }

        [Test]
        public void DeliveryLeaseClaimDetachedCopyReleaseAndPreviewCannotRetireCanonicalLifetime()
        {
            var queue=Queue();Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);
            Assert.That(queue.TryGetServiceLease("employee",out var actual),Is.True);Assert.That(actual,Is.EqualTo(P(0,4)));
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var canonical),Is.True,
                "FEATURE: capture real canonical lifetime before detached-copy/preview guard");
            var version=queue.Version;
            // Invoke the actual production copy constructor, then the public release; no fabricated private fields.
            // 通过真实copy constructor生成副本，再真实Release；不伪造Entry、pose或lease字段。
            var copyConstructor=typeof(PickupQueueService).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,
                new[]{typeof(PickupQueueService)},null);
            Assert.That(copyConstructor,Is.Not.Null,"FIXTURE: invoke the real private Queue copy constructor");
            var copy=(PickupQueueService)copyConstructor.Invoke(new object[]{queue});
            Assert.That(copy.TryGetServiceLease("employee",out var copiedPosition),Is.True);Assert.That(copiedPosition,Is.EqualTo(P(0,4)));
            Assert.That(copy.TryCaptureDeliveryLease("employee",out var copied),Is.True);
            Assert.That(canonical.IsSameLease(copied),Is.False);Assert.That(copied.IsSameLease(canonical),Is.False);
            var copyVersion=copy.Version;copy.ReleaseServicePosition("employee");
            Assert.That(copy.TryGetServiceLease("employee",out _),Is.False);Assert.That(copy.Version,Is.GreaterThan(copyVersion));
            Assert.That(copied.IsCurrent,Is.False);Assert.That(copied.HasRetiredLifetime,Is.True);
            Assert.That(canonical.IsCurrent,Is.True);Assert.That(canonical.HasRetiredLifetime,Is.False);
            Assert.That(queue.TryGetServiceLease("employee",out var preserved),Is.True);Assert.That(preserved,Is.EqualTo(P(0,4)));
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.DeliveryLease,"employee",version,out var preview,P(0,4)),Is.True);
            Assert.That(preview.IsCurrent,Is.True);Assert.That(queue.Version,Is.EqualTo(version));
            Assert.That(queue.TryCaptureDeliveryLease("employee",out var afterPreview),Is.True);
            Assert.That(canonical.IsSameLease(afterPreview),Is.True);Assert.That(canonical.IsCurrent,Is.True);
            Assert.That(canonical.HasRetiredLifetime,Is.False);Assert.That(queue.Snapshot,Is.Empty);
        }

        private static CounterQueueService CfcCounterWithTwoCompletedOwners()
        {
            var queue=new CounterQueueService(new[]{P(0),P(2),P(4)});
            foreach(var id in new[]{"head","tail"})
            {Assert.That(queue.TryJoin(id),Is.True);Assert.That(queue.Complete(id,queue.Snapshot.Single(a=>a.VisitId==id).Generation),Is.True);}
            return queue;
        }
        private static void CfcAdvanceCounterAfterHeadRemoval(CounterQueueService queue)
        {
            Assert.That(queue.Remove("head"),Is.True);Assert.That(queue.TryGetNextAdvance(out var moved),Is.True);
            Assert.That(moved.VisitId,Is.EqualTo("tail"));Assert.That(queue.Complete("tail",moved.Generation),Is.True);Assert.That(queue.CanJoin,Is.True);
        }
        private static void CfcAdvancePickupAfterHeadRemoval(PickupQueueService queue)
        {
            Assert.That(queue.Remove("head"),Is.True);Assert.That(queue.TryGetNextAdvance(out var moved),Is.True);
            Assert.That(moved.VisitId,Is.EqualTo("tail"));Assert.That(queue.MarkArrived("tail",moved.Generation),Is.True);Assert.That(queue.CanReserve,Is.True);
        }
        private static QueueAssignment[] CfcPickupAssignments(PickupQueueService queue)
            =>new[]{"head","tail","candidate","other","transient"}.Where(id=>queue.TryGetReservation(id,out _))
                .Select(id=>{queue.TryGetReservation(id,out var assignment);return assignment;}).ToArray();
        private static TrafficDecision CfcPrepareActualPrimary(ref ServiceTrafficCoordinator coordinator,object owner,long version,
            string prefix,QueueAssignment[] assignments,Vector3 target,string resource,int frame=11)
        {
            var key=new TrafficIntentKey("preview-model",1,"candidate","canonical-preview",1,7,"candidate");
            var facts=assignments.Select(a=>new TrafficParkingFact(new TrafficIntentKey("preview-model",1,a.VisitId,"body",1,7,a.VisitId),
                owner,version,prefix+a.VisitId,a.Position,.4f)).ToArray();
            var snapshot=new TrafficWorldSnapshot(version,1,null,facts);
            if(coordinator==null)coordinator=new ServiceTrafficCoordinator("preview-model",1,7,snapshot,(from,to)=>new[]{from,to});
            else coordinator.SetSnapshot(snapshot);
            coordinator.EvaluateParking(new TrafficRequest(key,11,snapshot,P(-2,4),target,.4f,resource,planPrimaryRoute:true));
            coordinator.ProcessReady(frame);var decision=coordinator.GetDecision(key);
            Assert.That(decision.Kind,Is.EqualTo(TrafficDecisionKind.Granted),"MODEL FIXTURE: actual public Core proof uses the current canonical snapshot: "+decision.Reason);
            Assert.That(decision.AcquiredSequence,Is.Zero);Assert.That(coordinator.HasActiveGrant(key),Is.False);return decision;
        }
        private static bool CfcPreviewMatches(string method,object owner,object preview,TrafficRouteProof primary)
        {
            var entry=typeof(CustomerFlowController).GetMethod(method,BindingFlags.Static|BindingFlags.NonPublic);
            Assert.That(entry,Is.Not.Null,"MODEL FIXTURE: inert private typed helper exists before behavior RED");
            return (bool)entry.Invoke(null,new[]{owner,"candidate",preview,primary});
        }

        // Cursor model sources come from actual public Core/Queue operations; no private Entry writes.
        // 这些是consumer模型，不证明Native运动；仅copied-source负例构造被拒绝的decision副本。
        private sealed class CursorModel
        {
            internal readonly TrafficIntentKey Key;
            internal readonly object Life=new object();
            internal readonly NavigationTarget[] Sides={new NavigationTarget(P(0,4)),new NavigationTarget(P(2,4))};
            internal readonly Vector3 Start=P(-2,4);
            internal readonly ServiceTrafficCoordinator Core;
            internal TrafficWorldSnapshot World;
            internal CursorModel(string mode="clear",int firstIndex=0,string actorId="candidate")
            {
                Key=new TrafficIntentKey("cursor-model",1,actorId,"pickup-phase",7,3,"original");
                var bodies=new List<TrafficBodyFact>{new TrafficBodyFact(Key,Start,.4f,actorLife:Life)};
                if(mode=="waiting" || mode=="terminal")bodies.Add(new TrafficBodyFact(
                    new TrafficIntentKey("cursor-model",1,"blocker","body",1,3,"body"),Sides[firstIndex].Position,.4f,
                    true,mode=="waiting",mode=="waiting"?8:0,actorLife:new object()));
                World=new TrafficWorldSnapshot(1,1,bodies,null);
                Core=new ServiceTrafficCoordinator("cursor-model",1,3,World,(from,to)=>
                    mode=="exhausted" || mode=="alternate" && to==Sides[firstIndex].Position?null:
                    mode=="detour"?new[]{from,P(-2,8),P(2,8),to}:new[]{from,to});
            }
            internal TrafficRequest Request(Vector3 target,int frame=11)=>new TrafficRequest(Key,frame,World,Start,target,.4f,
                "pickup/"+Key.ActorId,planPrimaryRoute:true);
            internal TrafficDecision Submit(int index=0,bool process=true)
            {
                Core.EvaluateParking(Request(Sides[index].Position),index,2);
                if(process)Core.ProcessReady(11);return Core.GetDecision(Key);
            }
            internal void QueueSnapshot(PickupQueueService owner)
            {
                var parking=CfcPickupAssignments(owner).Select(a=>new TrafficParkingFact(
                    new TrafficIntentKey("cursor-model",1,a.VisitId,"queue",1,3,a.VisitId),owner,owner.Version,
                    "pickup/"+a.VisitId,a.Position,.4f));
                World=new TrafficWorldSnapshot(owner.Version,World.BodyVersion,World.Bodies,parking);Core.SetSnapshot(World);
            }
        }
        private static object Cursor(CursorModel model,TrafficDecision source=null,int index=-1,bool yielding=false,string role="Collection")
        {
            var type=typeof(CustomerFlowController).GetNestedType("TrafficCandidateCursor",BindingFlags.NonPublic);
            Assert.That(type,Is.Not.Null,"MODEL FIXTURE: inert cursor declaration exists");
            var roleType=typeof(CustomerFlowController).GetNestedType("TrafficCandidateRole",BindingFlags.NonPublic);
            return Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,
                new object[]{model.Key,model.Life,model.Sides,model.Start,Enum.Parse(roleType,yielding?"Yield":role),index,source},null);
        }
        private static object CursorRead(object cursor,string name)=>cursor.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(cursor);
        private static bool CursorCall(object cursor,string method,object[] args)
            =>(bool)cursor.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(cursor,args);
        private static bool CursorSelect(object cursor,CursorModel model,TrafficDecision source,bool ready,int[] available,out int index)
        {
            var args=new object[]{model.Key,model.Life,source,ready,available,-1};var result=CursorCall(cursor,"TrySelect",args);
            index=(int)args[5];return result;
        }
        private static bool CursorPrepare(object cursor,CursorModel model,TrafficRequest request,int index,TrafficDecision source,out TrafficDecision next)
        {
            var args=new object[]{model.Core,request,model.Life,index,source,null};var result=CursorCall(cursor,"TryPrepare",args);
            next=(TrafficDecision)args[5];return result;
        }
        private static bool CursorDescribe(object cursor,TrafficIntentKey key,object life,int index,out NavigationTarget side,out Vector3 target)
        {
            var args=new object[]{key,life,index,default(NavigationTarget),default(Vector3)};var result=CursorCall(cursor,"TryDescribe",args);
            side=(NavigationTarget)args[3];target=(Vector3)args[4];return result;
        }

        private static bool DeliveryPreferredSelect(object cursor,CursorModel model,TrafficDecision source,bool ready,
            Func<int,NavigationTarget,Vector3,bool> available,Func<Vector3,bool> preferred,out int index)
        {
            var args=new object[]{model.Key,model.Life,source,ready,available,preferred,-1};
            var result=CursorCall(cursor,"TrySelectPreferredBase",args);index=(int)args[6];return result;
        }
        private static bool DeliveryEndpointAvailable(CursorModel model,Vector3 target)
            =>(bool)typeof(ServiceTrafficCoordinator).GetMethod("IsParkingEndpointAvailable",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(model.Core,new object[]{model.Request(target)});
        private static object DeliveryEntryRead(CursorModel model,string name)
        {
            var entries=(System.Collections.IDictionary)CursorRead(model.Core,"entries");
            return CursorRead(entries[model.Key],name);
        }

        [TestCase("clear")]
        [TestCase("first-static-body")]
        [TestCase("first-real-collection-lease")]
        public void CfcDeliveryPreferenceChoosesFreeOriginalSideWithoutSubmittingOrBurning(string scenario)
        {
            var model=new CursorModel(actorId:"employee");var queue=Queue();
            if(scenario=="first-static-body")
            {
                var blocker=new TrafficIntentKey("cursor-model",1,"static-body","body",1,3,"actual");
                model.World=new TrafficWorldSnapshot(1,2,model.World.Bodies.Concat(new[]{new TrafficBodyFact(blocker,model.Sides[0].Position,.4f)}),null);
                model.Core.SetSnapshot(model.World);
            }
            if(scenario=="first-real-collection-lease")
            {
                PaidArrived(queue,"collector");Assert.That(queue.TryAcquireCollection("collector",model.Sides[0].Position),Is.True);
                model.QueueSnapshot(queue);
            }
            var cursor=Cursor(model,role:"Delivery");var version=queue.Version;var availabilityReads=new List<int>();var hintReads=new List<Vector3>();
            bool Available(int index,NavigationTarget side,Vector3 target)
            {availabilityReads.Add(index);return queue.TryPrepareMutation(PickupQueueService.MutationKind.DeliveryLease,model.Key.ActorId,queue.Version,out _,target);}
            bool Preferred(Vector3 target){hintReads.Add(target);return DeliveryEndpointAvailable(model,target);}

            Assert.That(DeliveryPreferredSelect(cursor,model,null,true,Available,Preferred,out var selected),Is.True);

            Assert.That(selected,Is.EqualTo(scenario=="clear"?0:1));Assert.That(CursorRead(cursor,"CurrentIndex"),Is.EqualTo(-1));
            Assert.That((HashSet<int>)CursorRead(cursor,"AttemptedIndices"),Is.Empty);Assert.That((HashSet<int>)CursorRead(cursor,"StaticInvalidIndices"),Is.Empty);
            Assert.That(((System.Collections.IDictionary)CursorRead(model.Core,"entries")).Contains(model.Key),Is.False);
            Assert.That(model.Core.QueriesThisFrame,Is.Zero);Assert.That(queue.Version,Is.EqualTo(version));
            Assert.That(queue.TryGetServiceLease(model.Key.ActorId,out _),Is.False,"Selection has no canonical lease authority");
            Assert.That(DeliveryPreferredSelect(cursor,model,null,true,Available,Preferred,out var repeated),Is.True);Assert.That(repeated,Is.EqualTo(selected));
            Assert.That(availabilityReads.All(index=>index>=0 && index<model.Sides.Length),Is.True,"The reserved strategy is not an ordinary base side");
            Assert.That(hintReads,Does.Contain(model.Sides[selected].Position));
            Assert.That(CursorPrepare(cursor,model,model.Request(model.Sides[selected].Position),selected,null,out var queued),Is.True);
            Assert.That(queued.Kind,Is.EqualTo(TrafficDecisionKind.DeferredByBudget));
            CollectionAssert.AreEquivalent(new[]{selected},(HashSet<int>)CursorRead(cursor,"AttemptedIndices"));
            Assert.That(DeliveryEntryRead(model,"FirstFrame"),Is.EqualTo(11));Assert.That(DeliveryEntryRead(model,"CandidateCount"),Is.EqualTo(2));
            Assert.That(model.Core.QueriesConsumed(model.Key),Is.Zero);Assert.That(queue.Version,Is.EqualTo(version));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CfcDeliveryPreferenceAllBusyKeepsOriginalFallbackForCoreTerminalOrFiniteWait(bool finite)
        {
            var model=new CursorModel(actorId:"employee");var queue=Queue();
            var blockers=model.Sides.Select((side,index)=>new TrafficBodyFact(
                new TrafficIntentKey("cursor-model",1,"body-"+index,"body",1,3,"actual"),side.Position,.4f,true,finite,finite?8:0));
            model.World=new TrafficWorldSnapshot(1,2,model.World.Bodies.Concat(blockers),null);model.Core.SetSnapshot(model.World);
            var cursor=Cursor(model,role:"Delivery");var version=queue.Version;
            bool Available(int index,NavigationTarget side,Vector3 target)=>queue.TryPrepareMutation(PickupQueueService.MutationKind.DeliveryLease,
                model.Key.ActorId,queue.Version,out _,target);

            Assert.That(DeliveryPreferredSelect(cursor,model,null,true,Available,target=>DeliveryEndpointAvailable(model,target),out var selected),Is.True);

            Assert.That(selected,Is.EqualTo(0),"All busy keeps the original fallback; no unsubmitted infinite wait or blanket Unavailable");
            Assert.That((HashSet<int>)CursorRead(cursor,"AttemptedIndices"),Is.Empty);
            Assert.That(CursorPrepare(cursor,model,model.Request(model.Sides[selected].Position),selected,null,out _),Is.True);
            model.Core.ProcessReady(11);var actual=model.Core.GetDecision(model.Key);
            Assert.That(actual.Kind,Is.EqualTo(finite?TrafficDecisionKind.WaitingForTraffic:TrafficDecisionKind.PlanningFailed));
            Assert.That(actual.FailureDisposition,Is.EqualTo(finite?TrafficFailureDisposition.None:TrafficFailureDisposition.Terminal));
            Assert.That(model.Core.QueriesConsumed(model.Key),Is.Zero);Assert.That(actual.AcquiredSequence,Is.Zero);Assert.That(queue.Version,Is.EqualTo(version));
            Assert.That(DeliveryEntryRead(model,"FirstFrame"),Is.EqualTo(11));Assert.That(DeliveryEntryRead(model,"CandidateCount"),Is.EqualTo(2));
            Assert.That(DeliveryEntryRead(model,"EverCanonicalAttempt"),Is.EqualTo(false));
            if(finite)Assert.That(DeliveryEntryRead(model,"WaitRemaining"),Is.GreaterThan(0f));
            else Assert.That(DeliveryEntryRead(model,"WaitRemaining"),Is.EqualTo(0f));
        }

        [TestCase("deferred")]
        [TestCase("waiting")]
        [TestCase("granted")]
        [TestCase("acquired")]
        [TestCase("terminal")]
        public void CfcDeliveryPreferenceDoesNotReorderHeldOrForbiddenSources(string state)
        {
            var mode=state=="waiting"?"waiting":state=="terminal"?"terminal":"clear";
            var model=new CursorModel(mode,actorId:"employee");var source=model.Submit(process:state!="deferred");
            if(state=="acquired"){Assert.That(model.Core.TryCommit(source),Is.True);source=model.Core.GetDecision(model.Key);}
            Assert.That(source.Kind,Is.EqualTo(state=="deferred"?TrafficDecisionKind.DeferredByBudget:
                state=="waiting"?TrafficDecisionKind.WaitingForTraffic:state=="terminal"?TrafficDecisionKind.PlanningFailed:TrafficDecisionKind.Granted));
            var cursor=Cursor(model,source,0,role:"Delivery");var queries=model.Core.QueriesConsumed(model.Key);
            var originalWait=DeliveryEntryRead(model,"WaitRemaining");var originalFirst=DeliveryEntryRead(model,"FirstFrame");var callbacks=0;

            var accepted=DeliveryPreferredSelect(cursor,model,source,true,(index,side,target)=>{callbacks++;return true;},
                target=>{callbacks++;return true;},out var selected);

            Assert.That(accepted,Is.EqualTo(state!="terminal" && state!="acquired"));
            Assert.That(selected,Is.EqualTo(accepted?0:-1));Assert.That(callbacks,Is.Zero,"Opaque held or forbidden source must not poll different sides");
            Assert.That(model.Core.GetDecision(model.Key),Is.SameAs(source));Assert.That(model.Core.QueriesConsumed(model.Key),Is.EqualTo(queries));
            Assert.That(DeliveryEntryRead(model,"FirstFrame"),Is.EqualTo(originalFirst));Assert.That(DeliveryEntryRead(model,"WaitRemaining"),Is.EqualTo(originalWait));
            CollectionAssert.AreEquivalent(new[]{0},(HashSet<int>)CursorRead(cursor,"AttemptedIndices"));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void CfcDeliveryPreferenceConsumesOnlyExactOriginalAlternateWithRetainedAccounting(int first)
        {
            var model=new CursorModel("alternate",first,"employee");var source=model.Submit(first);
            Assert.That(source.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.AlternateAllowed));
            var cursor=Cursor(model,source,first,role:"Delivery");var queue=Queue();var version=queue.Version;var spent=model.Core.QueriesConsumed(model.Key);
            var originalWait=DeliveryEntryRead(model,"WaitRemaining");var sequence=source.RequestSequence;
            bool Available(int index,NavigationTarget side,Vector3 target)=>queue.TryPrepareMutation(PickupQueueService.MutationKind.DeliveryLease,
                model.Key.ActorId,queue.Version,out _,target);

            Assert.That(DeliveryPreferredSelect(cursor,model,source,true,Available,target=>DeliveryEndpointAvailable(model,target),out var nextIndex),Is.True);

            Assert.That(nextIndex,Is.EqualTo(1-first));CollectionAssert.AreEquivalent(new[]{first},(HashSet<int>)CursorRead(cursor,"AttemptedIndices"));
            Assert.That(model.Core.GetDecision(model.Key),Is.SameAs(source));Assert.That(model.Core.QueriesConsumed(model.Key),Is.EqualTo(spent));
            Assert.That(queue.Version,Is.EqualTo(version));Assert.That(CursorPrepare(cursor,model,model.Request(model.Sides[nextIndex].Position,99),nextIndex,source,out var next),Is.True);
            Assert.That(next.RequestSequence,Is.EqualTo(sequence));Assert.That(DeliveryEntryRead(model,"FirstFrame"),Is.EqualTo(11));
            Assert.That(((TrafficRequest)DeliveryEntryRead(model,"Request")).RequestedFrame,Is.EqualTo(11));
            Assert.That(DeliveryEntryRead(model,"CandidateCount"),Is.EqualTo(2));Assert.That(DeliveryEntryRead(model,"WaitRemaining"),Is.EqualTo(originalWait));
            Assert.That(model.Core.QueriesConsumed(model.Key),Is.EqualTo(spent));
            CollectionAssert.AreEquivalent(new[]{0,1},(HashSet<int>)CursorRead(cursor,"AttemptedIndices"));
            model.Core.ProcessReady(100);var granted=model.Core.GetDecision(model.Key);Assert.That(granted.Kind,Is.EqualTo(TrafficDecisionKind.Granted));
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.DeliveryLease,model.Key.ActorId,queue.Version,out var preview,model.Sides[nextIndex].Position),Is.True);
            Assert.That(model.Core.TryCommit(granted,preview),Is.True);Assert.That(queue.Version,Is.EqualTo(version+1));
            Assert.That(model.Core.TryCommit(granted,preview),Is.False);Assert.That(queue.Version,Is.EqualTo(version+1));
            Assert.That(queue.TryGetServiceLease(model.Key.ActorId,out var actual),Is.True);Assert.That(actual,Is.EqualTo(model.Sides[nextIndex].Position));
        }

        [TestCase("clear")]
        [TestCase("first-static-body")]
        [TestCase("first-real-delivery-lease")]
        public void CfcCollectionPreferenceChoosesFreeOriginalSideWithoutSubmittingOrBurning(string scenario)
        {
            var model=new CursorModel(actorId:"candidate");var queue=Queue();PaidArrived(queue,model.Key.ActorId);
            if(scenario=="first-static-body")
            {
                var blocker=new TrafficIntentKey("cursor-model",1,"static-body","body",1,3,"actual");
                model.World=new TrafficWorldSnapshot(1,2,model.World.Bodies.Concat(new[]{new TrafficBodyFact(blocker,model.Sides[0].Position,.4f)}),null);
                model.Core.SetSnapshot(model.World);
            }
            if(scenario=="first-real-delivery-lease")
            {
                Assert.That(queue.TryAcquireDelivery("employee",model.Sides[0].Position),Is.True);
                model.QueueSnapshot(queue);
            }
            model.QueueSnapshot(queue);var cursor=Cursor(model,role:"Collection");var version=queue.Version;var availabilityReads=new List<int>();var hintReads=new List<Vector3>();
            bool Available(int index,NavigationTarget side,Vector3 target)
            {availabilityReads.Add(index);return queue.TryPrepareMutation(PickupQueueService.MutationKind.CollectionLease,model.Key.ActorId,queue.Version,out _,target);}
            bool Preferred(Vector3 target){hintReads.Add(target);return DeliveryEndpointAvailable(model,target);}

            Assert.That(DeliveryPreferredSelect(cursor,model,null,true,Available,Preferred,out var selected),Is.True);

            Assert.That(selected,Is.EqualTo(scenario=="clear"?0:1));Assert.That(CursorRead(cursor,"CurrentIndex"),Is.EqualTo(-1));
            Assert.That((HashSet<int>)CursorRead(cursor,"AttemptedIndices"),Is.Empty);Assert.That((HashSet<int>)CursorRead(cursor,"StaticInvalidIndices"),Is.Empty);
            Assert.That(((System.Collections.IDictionary)CursorRead(model.Core,"entries")).Contains(model.Key),Is.False);
            Assert.That(model.Core.QueriesThisFrame,Is.Zero);Assert.That(queue.Version,Is.EqualTo(version));
            Assert.That(queue.TryGetServiceLease(model.Key.ActorId,out _),Is.False,"Selection has no canonical lease authority");
            Assert.That(DeliveryPreferredSelect(cursor,model,null,true,Available,Preferred,out var repeated),Is.True);Assert.That(repeated,Is.EqualTo(selected));
            Assert.That(availabilityReads.All(index=>index>=0 && index<model.Sides.Length),Is.True,"Collection stays within its original base-side count");
            Assert.That(hintReads,Does.Contain(model.Sides[selected].Position));
            Assert.That(CursorPrepare(cursor,model,model.Request(model.Sides[selected].Position),selected,null,out var queued),Is.True);
            Assert.That(queued.Kind,Is.EqualTo(TrafficDecisionKind.DeferredByBudget));
            CollectionAssert.AreEquivalent(new[]{selected},(HashSet<int>)CursorRead(cursor,"AttemptedIndices"));
            Assert.That(DeliveryEntryRead(model,"FirstFrame"),Is.EqualTo(11));Assert.That(DeliveryEntryRead(model,"CandidateCount"),Is.EqualTo(2));
            Assert.That(model.Core.QueriesConsumed(model.Key),Is.Zero);Assert.That(queue.Version,Is.EqualTo(version));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CfcCollectionPreferenceAllBusyKeepsOriginalFallbackForCoreTerminalOrFiniteWait(bool finite)
        {
            var model=new CursorModel(actorId:"candidate");var queue=Queue();PaidArrived(queue,model.Key.ActorId);
            var blockers=model.Sides.Select((side,index)=>new TrafficBodyFact(
                new TrafficIntentKey("cursor-model",1,"body-"+index,"body",1,3,"actual"),side.Position,.4f,true,finite,finite?8:0));
            model.World=new TrafficWorldSnapshot(1,2,model.World.Bodies.Concat(blockers),null);model.Core.SetSnapshot(model.World);
            model.QueueSnapshot(queue);var cursor=Cursor(model,role:"Collection");var version=queue.Version;
            bool Available(int index,NavigationTarget side,Vector3 target)=>queue.TryPrepareMutation(PickupQueueService.MutationKind.CollectionLease,
                model.Key.ActorId,queue.Version,out _,target);

            Assert.That(DeliveryPreferredSelect(cursor,model,null,true,Available,target=>DeliveryEndpointAvailable(model,target),out var selected),Is.True);

            Assert.That(selected,Is.EqualTo(0),"All busy keeps the original fallback; no unsubmitted infinite wait or blanket Unavailable");
            Assert.That((HashSet<int>)CursorRead(cursor,"AttemptedIndices"),Is.Empty);
            Assert.That(CursorPrepare(cursor,model,model.Request(model.Sides[selected].Position),selected,null,out _),Is.True);
            model.Core.ProcessReady(11);var actual=model.Core.GetDecision(model.Key);
            Assert.That(actual.Kind,Is.EqualTo(finite?TrafficDecisionKind.WaitingForTraffic:TrafficDecisionKind.PlanningFailed));
            Assert.That(actual.FailureDisposition,Is.EqualTo(finite?TrafficFailureDisposition.None:TrafficFailureDisposition.Terminal));
            Assert.That(model.Core.QueriesConsumed(model.Key),Is.Zero);Assert.That(actual.AcquiredSequence,Is.Zero);Assert.That(queue.Version,Is.EqualTo(version));
            Assert.That(DeliveryEntryRead(model,"FirstFrame"),Is.EqualTo(11));Assert.That(DeliveryEntryRead(model,"CandidateCount"),Is.EqualTo(2));
            Assert.That(DeliveryEntryRead(model,"EverCanonicalAttempt"),Is.EqualTo(false));
            if(finite)Assert.That(DeliveryEntryRead(model,"WaitRemaining"),Is.GreaterThan(0f));
            else Assert.That(DeliveryEntryRead(model,"WaitRemaining"),Is.EqualTo(0f));
        }

        [TestCase("deferred")]
        [TestCase("waiting")]
        [TestCase("granted")]
        [TestCase("acquired")]
        [TestCase("terminal")]
        public void CfcCollectionPreferenceDoesNotReorderHeldOrForbiddenSources(string state)
        {
            var mode=state=="waiting"?"waiting":state=="terminal"?"terminal":"clear";
            var model=new CursorModel(mode,actorId:"candidate");var source=model.Submit(process:state!="deferred");
            if(state=="acquired"){Assert.That(model.Core.TryCommit(source),Is.True);source=model.Core.GetDecision(model.Key);}
            Assert.That(source.Kind,Is.EqualTo(state=="deferred"?TrafficDecisionKind.DeferredByBudget:
                state=="waiting"?TrafficDecisionKind.WaitingForTraffic:state=="terminal"?TrafficDecisionKind.PlanningFailed:TrafficDecisionKind.Granted));
            var cursor=Cursor(model,source,0,role:"Collection");var queries=model.Core.QueriesConsumed(model.Key);
            var originalWait=DeliveryEntryRead(model,"WaitRemaining");var originalFirst=DeliveryEntryRead(model,"FirstFrame");var callbacks=0;

            var accepted=DeliveryPreferredSelect(cursor,model,source,true,(index,side,target)=>{callbacks++;return true;},
                target=>{callbacks++;return true;},out var selected);

            Assert.That(accepted,Is.EqualTo(state!="terminal" && state!="acquired"));
            Assert.That(selected,Is.EqualTo(accepted?0:-1));Assert.That(callbacks,Is.Zero,"Opaque held or forbidden source must not poll different sides");
            Assert.That(model.Core.GetDecision(model.Key),Is.SameAs(source));Assert.That(model.Core.QueriesConsumed(model.Key),Is.EqualTo(queries));
            Assert.That(DeliveryEntryRead(model,"FirstFrame"),Is.EqualTo(originalFirst));Assert.That(DeliveryEntryRead(model,"WaitRemaining"),Is.EqualTo(originalWait));
            CollectionAssert.AreEquivalent(new[]{0},(HashSet<int>)CursorRead(cursor,"AttemptedIndices"));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void CfcCollectionPreferenceConsumesOnlyExactOriginalAlternateWithRetainedAccounting(int first)
        {
            var model=new CursorModel("alternate",first,"candidate");var source=model.Submit(first);
            Assert.That(source.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.AlternateAllowed));
            var cursor=Cursor(model,source,first,role:"Collection");var queue=Queue();PaidArrived(queue,model.Key.ActorId);var version=queue.Version;var spent=model.Core.QueriesConsumed(model.Key);
            var originalWait=DeliveryEntryRead(model,"WaitRemaining");var sequence=source.RequestSequence;
            bool Available(int index,NavigationTarget side,Vector3 target)=>queue.TryPrepareMutation(PickupQueueService.MutationKind.CollectionLease,
                model.Key.ActorId,queue.Version,out _,target);

            Assert.That(DeliveryPreferredSelect(cursor,model,source,true,Available,target=>DeliveryEndpointAvailable(model,target),out var nextIndex),Is.True);

            Assert.That(nextIndex,Is.EqualTo(1-first));CollectionAssert.AreEquivalent(new[]{first},(HashSet<int>)CursorRead(cursor,"AttemptedIndices"));
            Assert.That(model.Core.GetDecision(model.Key),Is.SameAs(source));Assert.That(model.Core.QueriesConsumed(model.Key),Is.EqualTo(spent));
            Assert.That(queue.Version,Is.EqualTo(version));Assert.That(CursorPrepare(cursor,model,model.Request(model.Sides[nextIndex].Position,99),nextIndex,source,out var next),Is.True);
            Assert.That(next.RequestSequence,Is.EqualTo(sequence));Assert.That(DeliveryEntryRead(model,"FirstFrame"),Is.EqualTo(11));
            Assert.That(((TrafficRequest)DeliveryEntryRead(model,"Request")).RequestedFrame,Is.EqualTo(11));
            Assert.That(DeliveryEntryRead(model,"CandidateCount"),Is.EqualTo(2));Assert.That(DeliveryEntryRead(model,"WaitRemaining"),Is.EqualTo(originalWait));
            Assert.That(model.Core.QueriesConsumed(model.Key),Is.EqualTo(spent));
            CollectionAssert.AreEquivalent(new[]{0,1},(HashSet<int>)CursorRead(cursor,"AttemptedIndices"));
            model.Core.ProcessReady(100);var granted=model.Core.GetDecision(model.Key);Assert.That(granted.Kind,Is.EqualTo(TrafficDecisionKind.Granted));
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.CollectionLease,model.Key.ActorId,queue.Version,out var preview,model.Sides[nextIndex].Position),Is.True);
            Assert.That(model.Core.TryCommit(granted,preview),Is.True);Assert.That(queue.Version,Is.EqualTo(version+1));
            Assert.That(model.Core.TryCommit(granted,preview),Is.False);Assert.That(queue.Version,Is.EqualTo(version+1));
            Assert.That(queue.TryGetServiceLease(model.Key.ActorId,out var actual),Is.True);Assert.That(actual,Is.EqualTo(model.Sides[nextIndex].Position));
        }

        [TestCase("busy-first")] [TestCase("all-busy")] [TestCase("nav-unavailable")] [TestCase("disabled")]
        [TestCase("unknown")] [TestCase("waiting")] [TestCase("deferred")] [TestCase("exhausted")]
        public void CfcFiniteCursorSelectionPreservesUnburnedAndPendingCandidates(string scenario)
        {
            var mode=scenario=="unknown"?"alternate":scenario;var model=new CursorModel(mode);
            TrafficDecision source=null;var initial=-1;
            if(scenario=="unknown" || scenario=="waiting" || scenario=="deferred" || scenario=="exhausted")
            {
                source=model.Submit(process:scenario!="deferred");initial=0;
                if(scenario=="waiting")Assert.That(source.Kind,Is.EqualTo(TrafficDecisionKind.WaitingForTraffic));
                if(scenario=="deferred")Assert.That(source.Kind,Is.EqualTo(TrafficDecisionKind.DeferredByBudget));
                if(scenario=="unknown" || scenario=="exhausted")Assert.That(source.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.AlternateAllowed));
                if(scenario=="exhausted")
                {
                    Assert.That(model.Core.TryAdvanceCandidate(source,1,model.Request(model.Sides[1].Position,12),out _),Is.True);
                    model.Core.ProcessReady(12);source=model.Core.GetDecision(model.Key);initial=1;
                    Assert.That(source.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.Terminal));
                }
            }
            var cursor=Cursor(model,source,initial);var attempted=((HashSet<int>)CursorRead(cursor,"AttemptedIndices")).ToArray();
            var queries=model.Core.QueriesConsumed(model.Key);var available=scenario=="all-busy"?Array.Empty<int>():new[]{1};
            var ready=scenario!="nav-unavailable" && scenario!="disabled";
            var positive=scenario=="busy-first" || scenario=="unknown" || scenario=="waiting" || scenario=="deferred";
            Assert.That(CursorSelect(cursor,model,source,ready,available,out var selected),Is.EqualTo(positive),"PRODUCT: bounded selection state");
            if(positive)Assert.That(selected,Is.EqualTo(scenario=="waiting" || scenario=="deferred"?0:1));
            Assert.That((HashSet<int>)CursorRead(cursor,"AttemptedIndices"),Is.EquivalentTo(attempted),"Selection is not submission");
            Assert.That((HashSet<int>)CursorRead(cursor,"StaticInvalidIndices"),Is.Empty,"Busy, disabled and null query are not static proof");
            Assert.That(model.Core.QueriesConsumed(model.Key),Is.EqualTo(queries));
            if(source!=null)Assert.That(model.Core.GetDecision(model.Key),Is.SameAs(source));
            else Assert.That(model.Core.HasPendingWork,Is.False,"Preflight never creates a Core entry");
        }

        [TestCase("forward")] [TestCase("lower-index")] [TestCase("copied")] [TestCase("stale")]
        [TestCase("repeated")] [TestCase("out-of-range")] [TestCase("terminal")] [TestCase("canonical-attempt")]
        public void CfcFiniteCursorAdvanceUsesExactPublicCoreSource(string scenario)
        {
            var first=scenario=="lower-index"?1:0;var model=new CursorModel(scenario=="terminal"?"terminal":scenario=="canonical-attempt"?"clear":"alternate",first);
            var source=model.Submit(first);var cursor=Cursor(model,source,first);var nextIndex=1-first;
            if(scenario=="canonical-attempt")
            {
                Assert.That(source.Kind,Is.EqualTo(TrafficDecisionKind.Granted));var owner=Queue();
                Assert.That(owner.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"candidate",owner.Version,out var prepared),Is.True);
                Assert.That(model.Core.TryCommit(source,new FailAfterQueueApply(prepared)),Is.False);
                Assert.That(owner.TryGetReservation("candidate",out _),Is.False);source=model.Core.GetDecision(model.Key);
                Assert.That(source.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.Terminal));
            }
            else if(scenario=="terminal")Assert.That(source.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.Terminal));
            else Assert.That(source.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.AlternateAllowed));
            var expected=source;
            if(scenario=="copied")expected=new TrafficDecision(source.Intent,source.Kind,source.Reason,failureDisposition:source.FailureDisposition);
            if(scenario=="stale")
                Assert.That(model.Core.TryAdvanceCandidate(source,nextIndex,model.Request(model.Sides[nextIndex].Position,12),out _),Is.True);
            if(scenario=="repeated")nextIndex=first;
            if(scenario=="out-of-range")nextIndex=2;
            var before=model.Core.GetDecision(model.Key);var queries=model.Core.QueriesConsumed(model.Key);
            var request=model.Request(model.Sides[Math.Min(nextIndex,1)].Position,99);
            var positive=scenario=="forward" || scenario=="lower-index";
            Assert.That(CursorPrepare(cursor,model,request,nextIndex,expected,out var next),Is.EqualTo(positive),"PRODUCT: exact original alternate source only");
            Assert.That(model.Core.QueriesConsumed(model.Key),Is.EqualTo(queries),"Advance only queues normal work");
            if(!positive){Assert.That(next,Is.Null);Assert.That(model.Core.GetDecision(model.Key),Is.SameAs(before));return;}
            Assert.That(next.Kind,Is.EqualTo(TrafficDecisionKind.DeferredByBudget));Assert.That(next.RequestSequence,Is.EqualTo(source.RequestSequence));
            Assert.That((HashSet<int>)CursorRead(cursor,"AttemptedIndices"),Is.EquivalentTo(new[]{first,nextIndex}));
            model.Core.ProcessReady(100);var granted=model.Core.GetDecision(model.Key);
            Assert.That(granted.Kind,Is.EqualTo(TrafficDecisionKind.Granted));Assert.That(granted.RequestSequence,Is.EqualTo(source.RequestSequence));
            Assert.That(model.Core.QueriesConsumed(model.Key),Is.EqualTo(queries+1));
            Assert.That(model.Core.TryCommit(granted),Is.True);Assert.That(model.Core.TryCommit(granted),Is.False);
            Assert.That(CursorPrepare(cursor,model,request,first,expected,out _),Is.False,"Consumed source and attempted index cannot replay");
        }

        [TestCase("immutable-sides")] [TestCase("yield-origin")] [TestCase("changed-context")] [TestCase("changed-life")]
        public void CfcFiniteCursorDescriptorBindsOriginalGeometryAndLife(string scenario)
        {
            var model=new CursorModel();var cursor=Cursor(model,yielding:scenario=="yield-origin");
            var original=model.Sides[0];model.Sides[0]=new NavigationTarget(P(90,90));
            var key=scenario=="changed-context"?new TrafficIntentKey("cursor-model",2,"candidate","pickup-phase",7,4,"original"):model.Key;
            var life=scenario=="changed-life"?new object():model.Life;
            var positive=scenario=="immutable-sides" || scenario=="yield-origin";
            Assert.That(CursorDescribe(cursor,key,life,0,out var side,out var target),Is.EqualTo(positive),"PRODUCT: original descriptor context");
            if(!positive)return;
            Assert.That(side,Is.EqualTo(original));
            if(scenario=="immutable-sides")
            {
                Assert.That(target,Is.EqualTo(original.Position));Assert.That(CursorDescribe(cursor,key,life,2,out _,out _),Is.False,"Original count cannot expand");
            }
            else
            {
                Assert.That(target,Is.EqualTo(model.Start+Vector3.forward*1.1f));
                Assert.That(CursorDescribe(cursor,key,life,47,out var lastSide,out var lastTarget),Is.True);
                Assert.That(lastSide,Is.EqualTo(model.Sides[1]));Assert.That(Vector3.Distance(lastTarget,model.Start),Is.EqualTo(3.3f).Within(.00001f));
                model.World=new TrafficWorldSnapshot(2,2,new[]{new TrafficBodyFact(model.Key,P(50),.4f,actorLife:model.Life)},null);
                model.Core.SetSnapshot(model.World);
                Assert.That(CursorDescribe(cursor,key,life,0,out _,out var retained),Is.True);Assert.That(retained,Is.EqualTo(target),"Later actual body pose cannot recenter original samples");
                Assert.That(CursorDescribe(cursor,key,life,48,out _,out _),Is.False);
            }
        }

        [TestCase("reserve-tail")] [TestCase("collection-busy")] [TestCase("delivery-busy")] [TestCase("yield-fixed")]
        public void CfcFiniteCursorKeepsFixedSideSeparateFromRealQueueAnchor(string scenario)
        {
            var model=scenario=="delivery-busy"?new CursorModel("detour",actorId:"employee"):new CursorModel();
            var owner=Queue();object cursor;TrafficDecision source=null;int index=0;
            PickupQueueService.PreparedMutation prepared;Vector3 target;PickupQueueService.MutationKind operation;
            if(scenario=="reserve-tail")
            {
                PaidArrived(owner,"head");PaidArrived(owner,"tail");
                Assert.That(owner.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"candidate",owner.Version,out prepared),Is.True);
                Assert.That(prepared.Assignment.SlotIndex,Is.EqualTo(2));model.QueueSnapshot(owner);
                model.Core.EvaluateParking(model.Request(prepared.Assignment.Position),0,2);model.Core.ProcessReady(11);source=model.Core.GetDecision(model.Key);
                Assert.That(source.Kind,Is.EqualTo(TrafficDecisionKind.Granted));cursor=Cursor(model,source,0,role:"Reservation");
                CfcAdvancePickupAfterHeadRemoval(owner);model.QueueSnapshot(owner);operation=PickupQueueService.MutationKind.Reserve;
                Assert.That(owner.TryPrepareMutation(operation,"candidate",owner.Version,out prepared),Is.True);
                Assert.That(prepared.Assignment.SlotIndex,Is.EqualTo(1));target=prepared.Assignment.Position;
                Assert.That(target,Is.Not.EqualTo(model.Sides[0].Position),"Queue tail is not the work-side descriptor");
            }
            else
            {
                PaidArrived(owner,"candidate");cursor=Cursor(model,yielding:scenario=="yield-fixed",role:scenario=="delivery-busy"?"Delivery":"Collection");
                if(scenario=="yield-fixed")
                {
                    Assert.That(CursorDescribe(cursor,model.Key,model.Life,0,out _,out target),Is.True,"PRODUCT: fixed absolute yield target");
                    operation=PickupQueueService.MutationKind.BeginYield;
                }
                else
                {
                    operation=scenario=="collection-busy"?PickupQueueService.MutationKind.CollectionLease:PickupQueueService.MutationKind.DeliveryLease;
                    if(scenario=="collection-busy")Assert.That(owner.TryAcquireDelivery("occupied",model.Sides[0].Position),Is.True);
                    else Assert.That(owner.TryAcquireCollection("candidate",model.Sides[0].Position),Is.True);
                    Assert.That(owner.TryPrepareMutation(operation,model.Key.ActorId,owner.Version,out _,model.Sides[0].Position),Is.False);
                    Assert.That(CursorSelect(cursor,model,null,true,new[]{1},out index),Is.True,"PRODUCT: real canonical busy side is skipped without submission");
                    Assert.That((HashSet<int>)CursorRead(cursor,"AttemptedIndices"),Is.Empty);target=model.Sides[index].Position;
                }
                Assert.That(owner.TryPrepareMutation(operation,model.Key.ActorId,owner.Version,out prepared,target),Is.True);model.QueueSnapshot(owner);
            }
            var version=owner.Version;var before=model.Core.QueriesConsumed(model.Key);
            Assert.That(CursorPrepare(cursor,model,model.Request(target,99),index,source,out _),Is.True,"PRODUCT: current sealed target with original finite descriptor");
            Assert.That(owner.Version,Is.EqualTo(version),"Preparation grants no canonical mutation");
            model.Core.ProcessReady(100);var granted=model.Core.GetDecision(model.Key);
            Assert.That(granted.Kind,Is.EqualTo(TrafficDecisionKind.Granted));Assert.That(model.Core.QueriesConsumed(model.Key),Is.GreaterThan(before));
            Assert.That(granted.ProofRoutes.Single(p=>p.ResourceId=="pickup/"+model.Key.ActorId).Waypoints.Last(),Is.EqualTo(prepared.Assignment.Position));
            if(scenario=="reserve-tail")Assert.That(CfcPreviewMatches("PrimaryMatchesPickupReservationPreview",owner,prepared,granted.ProofRoutes.Single()),Is.True);
            Assert.That(model.Core.TryCommit(granted,prepared),Is.True);Assert.That(owner.Version,Is.EqualTo(version+1));
            Assert.That(model.Core.TryCommit(granted,prepared),Is.False);Assert.That(owner.Version,Is.EqualTo(version+1),"Actual canonical apply once");
        }

        // These are Queue model transitions, not evidence of Native movement or birth authority.
        // helper只检查preview与primary；真实Actor/session/cursor权限仍属于caller记录与Core。
        [TestCase("tail-moved")] [TestCase("wrong-queue")] [TestCase("wrong-id")] [TestCase("stale")]
        [TestCase("consumed")] [TestCase("wrong-kind")] [TestCase("wrong-resource")] [TestCase("fresh-preflight")]
        public void CfcCounterAdmissionPreviewGuardsActualCanonicalApply(string scenario)
        {
            var owner=CfcCounterWithTwoCompletedOwners();CounterQueueService foreign=null;
            Assert.That(owner.TryPrepareMutation(CounterQueueService.MutationKind.Join,"candidate",owner.Version,out var original),Is.True);
            Assert.That(original.Assignment.SlotIndex,Is.EqualTo(2));Assert.That(original.Assignment.Position,Is.EqualTo(P(4)));
            ServiceTrafficCoordinator coordinator=null;
            if(scenario=="tail-moved")CfcPrepareActualPrimary(ref coordinator,owner,owner.Version,"counter/",owner.Snapshot.ToArray(),P(4),"counter/candidate");
            CfcAdvanceCounterAfterHeadRemoval(owner);
            Assert.That(owner.TryPrepareMutation(CounterQueueService.MutationKind.Join,"candidate",owner.Version,out var preview),Is.True);
            Assert.That(preview.Assignment.SlotIndex,Is.EqualTo(1));Assert.That(preview.Assignment.Position,Is.EqualTo(P(2)));Assert.That(original.IsCurrent,Is.False);
            if(scenario=="wrong-queue")
            {
                foreign=CfcCounterWithTwoCompletedOwners();CfcAdvanceCounterAfterHeadRemoval(foreign);Assert.That(foreign.Version,Is.EqualTo(owner.Version));
                Assert.That(foreign.TryPrepareMutation(CounterQueueService.MutationKind.Join,"candidate",foreign.Version,out preview),Is.True);
            }
            else if(scenario=="wrong-id")Assert.That(owner.TryPrepareMutation(CounterQueueService.MutationKind.Join,"other",owner.Version,out preview),Is.True);
            else if(scenario=="stale")owner.RestartAssignments();
            else if(scenario=="consumed")Assert.That(preview.TryApply(out _),Is.True);
            else if(scenario=="wrong-kind")
            {
                Assert.That(preview.TryApply(out _),Is.True);Assert.That(owner.Complete("candidate",owner.Snapshot.Single(a=>a.VisitId=="candidate").Generation),Is.True);
                Assert.That(owner.Remove("tail"),Is.True);Assert.That(owner.TryPrepareMutation(CounterQueueService.MutationKind.Advance,"candidate",owner.Version,out preview),Is.True);
            }
            var resource=scenario=="wrong-resource"?"other/candidate":"counter/candidate";
            var decision=CfcPrepareActualPrimary(ref coordinator,owner,owner.Version,"counter/",owner.Snapshot.ToArray(),
                scenario=="tail-moved"?P(4):preview.Assignment.Position,resource,12);
            var primary=decision.ProofRoutes.Single(p=>p.ResourceId==resource);
            Assert.That(primary.Waypoints.Last(),Is.EqualTo(scenario=="tail-moved"?P(4):preview.Assignment.Position));
            if(scenario=="tail-moved")Assert.That(primary.Waypoints.Last(),Is.Not.EqualTo(preview.Assignment.Position),"FIXTURE: current Core permission still proves the old anchor, so stale context cannot hide this mismatch");
            var before=owner.Version;var assignments=owner.Snapshot.ToArray();var foreignBefore=foreign?.Version;var foreignAssignments=foreign?.Snapshot.ToArray();
            var accepted=CfcPreviewMatches("PrimaryMatchesCounterAdmissionPreview",owner,preview,primary);
            var committed=accepted && coordinator.TryCommit(decision,preview);
            if(scenario=="fresh-preflight")
            {
                Assert.That(accepted,Is.True,"PRODUCT: the real current Join preview and current matching Core primary must pass the typed binding check");
                Assert.That(committed,Is.True);Assert.That(owner.Version,Is.EqualTo(before+1));Assert.That(owner.Snapshot.Count(a=>a.VisitId=="candidate"),Is.EqualTo(1));
                Assert.That(owner.Snapshot.Single(a=>a.VisitId=="candidate").Position,Is.EqualTo(P(2)));Assert.That(coordinator.HasActiveGrant(decision.Intent),Is.True);
                Assert.That(preview.IsCurrent,Is.False);Assert.That(CfcPreviewMatches("PrimaryMatchesCounterAdmissionPreview",owner,preview,primary),Is.False);
                Assert.That(coordinator.TryCommit(decision,preview),Is.False);Assert.That(owner.Version,Is.EqualTo(before+1));return;
            }
            Assert.That(accepted,Is.False,"PRODUCT: an invalid typed preview cannot authorize a canonical Queue write");Assert.That(committed,Is.False);
            Assert.That(owner.Version,Is.EqualTo(before));Assert.That(owner.Snapshot,Is.EqualTo(assignments));Assert.That(coordinator.HasActiveGrant(decision.Intent),Is.False);
            if(foreign!=null){Assert.That(foreign.Version,Is.EqualTo(foreignBefore.Value));Assert.That(foreign.Snapshot,Is.EqualTo(foreignAssignments));}
        }

        [TestCase("tail-moved")] [TestCase("wrong-queue")] [TestCase("wrong-id")] [TestCase("stale")]
        [TestCase("consumed")] [TestCase("wrong-kind")] [TestCase("wrong-resource")] [TestCase("fresh-preflight")]
        public void CfcPickupReservationPreviewGuardsActualCanonicalApply(string scenario)
        {
            var owner=Queue();PaidArrived(owner,"head");PaidArrived(owner,"tail");PickupQueueService foreign=null;
            Assert.That(owner.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"candidate",owner.Version,out var original),Is.True);
            Assert.That(original.Assignment.SlotIndex,Is.EqualTo(2));Assert.That(original.Assignment.Position,Is.EqualTo(P(4)));
            ServiceTrafficCoordinator coordinator=null;
            if(scenario=="tail-moved")CfcPrepareActualPrimary(ref coordinator,owner,owner.Version,"pickup/",CfcPickupAssignments(owner),P(4),"pickup/candidate");
            CfcAdvancePickupAfterHeadRemoval(owner);
            Assert.That(owner.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"candidate",owner.Version,out var preview),Is.True);
            Assert.That(preview.Assignment.SlotIndex,Is.EqualTo(1));Assert.That(preview.Assignment.Position,Is.EqualTo(P(2)));Assert.That(original.IsCurrent,Is.False);
            if(scenario=="wrong-queue")
            {
                foreign=Queue();PaidArrived(foreign,"head");PaidArrived(foreign,"tail");CfcAdvancePickupAfterHeadRemoval(foreign);Assert.That(foreign.Version,Is.EqualTo(owner.Version));
                Assert.That(foreign.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"candidate",foreign.Version,out preview),Is.True);
            }
            else if(scenario=="wrong-id")Assert.That(owner.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"other",owner.Version,out preview),Is.True);
            else if(scenario=="stale")
            {Assert.That(owner.TryReserve("transient",out _),Is.True);Assert.That(owner.Remove("transient"),Is.True);}
            else if(scenario=="consumed")Assert.That(preview.TryApply(out _),Is.True);
            else if(scenario=="wrong-kind")
            {Assert.That(preview.TryApply(out _),Is.True);Assert.That(owner.TryPrepareMutation(PickupQueueService.MutationKind.Transfer,"candidate",owner.Version,out preview),Is.True);}
            var resource=scenario=="wrong-resource"?"other/candidate":"pickup/candidate";
            var decision=CfcPrepareActualPrimary(ref coordinator,owner,owner.Version,"pickup/",CfcPickupAssignments(owner),
                scenario=="tail-moved"?P(4):preview.Assignment.Position,resource,12);
            var primary=decision.ProofRoutes.Single(p=>p.ResourceId==resource);
            Assert.That(primary.Waypoints.Last(),Is.EqualTo(scenario=="tail-moved"?P(4):preview.Assignment.Position));
            if(scenario=="tail-moved")Assert.That(primary.Waypoints.Last(),Is.Not.EqualTo(preview.Assignment.Position),"FIXTURE: current Core permission still proves the old anchor, so stale context cannot hide this mismatch");
            var before=owner.Version;var assignments=CfcPickupAssignments(owner);var paid=owner.Snapshot.ToArray();
            var foreignBefore=foreign?.Version;var foreignAssignments=foreign==null?null:CfcPickupAssignments(foreign);var foreignPaid=foreign?.Snapshot.ToArray();
            var accepted=CfcPreviewMatches("PrimaryMatchesPickupReservationPreview",owner,preview,primary);
            var committed=accepted && coordinator.TryCommit(decision,preview);
            if(scenario=="fresh-preflight")
            {
                Assert.That(accepted,Is.True,"PRODUCT: the real current Reserve preview and current matching Core primary must pass the typed binding check");
                Assert.That(committed,Is.True);Assert.That(owner.Version,Is.EqualTo(before+1));Assert.That(owner.TryGetReservation("candidate",out var actual),Is.True);
                Assert.That(actual.Position,Is.EqualTo(P(2)));Assert.That(CfcPickupAssignments(owner).Count(a=>a.VisitId=="candidate"),Is.EqualTo(1));
                Assert.That(owner.Snapshot,Is.EqualTo(paid),"An unpaid Reserve cannot create payment or paid FIFO membership");Assert.That(coordinator.HasActiveGrant(decision.Intent),Is.True);
                Assert.That(preview.IsCurrent,Is.False);Assert.That(CfcPreviewMatches("PrimaryMatchesPickupReservationPreview",owner,preview,primary),Is.False);
                Assert.That(coordinator.TryCommit(decision,preview),Is.False);Assert.That(owner.Version,Is.EqualTo(before+1));return;
            }
            Assert.That(accepted,Is.False,"PRODUCT: an invalid typed preview cannot authorize a canonical Queue write");Assert.That(committed,Is.False);
            Assert.That(owner.Version,Is.EqualTo(before));Assert.That(CfcPickupAssignments(owner),Is.EqualTo(assignments));Assert.That(owner.Snapshot,Is.EqualTo(paid));
            Assert.That(coordinator.HasActiveGrant(decision.Intent),Is.False);
            if(foreign!=null){Assert.That(foreign.Version,Is.EqualTo(foreignBefore.Value));Assert.That(CfcPickupAssignments(foreign),Is.EqualTo(foreignAssignments));Assert.That(foreign.Snapshot,Is.EqualTo(foreignPaid));}
        }

        [TestCase("reserve")] [TestCase("transfer")] [TestCase("advance")]
        [TestCase("yield")] [TestCase("return-yield")] [TestCase("collection")] [TestCase("yielded-collection")]
        public void PickupReservationClaimCapturesRealCanonicalStateWithoutWrites(string phase)
        {
            var queue=Queue();var id="a";
            if(phase=="reserve")Assert.That(queue.TryReserve(id,out _),Is.True);
            else if(phase=="transfer")
            { Assert.That(queue.TryReserve(id,out _),Is.True);Assert.That(queue.TryBeginTransfer(id,out _),Is.True); }
            else if(phase=="advance")
            {
                PaidArrived(queue,"head");PaidArrived(queue,id);Assert.That(queue.Remove("head"),Is.True);
                Assert.That(queue.TryGetNextAdvance(out var advanced),Is.True);Assert.That(advanced.VisitId,Is.EqualTo(id));
                Assert.That(queue.MarkArrived(id,advanced.Generation),Is.True);
            }
            else
            {
                PaidArrived(queue,id);
                if(phase=="yield" || phase=="return-yield" || phase=="yielded-collection")Yield(queue,id,true);
                if(phase=="return-yield")
                {
                    var args=new object[]{id,default(QueueAssignment)};
                    Assert.That(Call(queue,"TryReturnYielded",args),Is.True);
                    Assert.That(queue.MarkArrived(id,((QueueAssignment)args[1]).Generation),Is.True);
                }
                if(phase=="collection" || phase=="yielded-collection")
                {
                    Assert.That(phase=="collection"?queue.TryAcquireCollection(id,P(0,4)):
                        Call(queue,"TryAcquireYieldedCollection",id,P(0,4)),Is.True);
                    Assert.That(queue.TryGetReservation(id,out var service),Is.True);
                    Assert.That(queue.MarkArrived(id,service.Generation),Is.True);
                    Assert.That(queue.TryGetServiceLease(id,out var leased),Is.True);Assert.That(leased,Is.EqualTo(P(0,4)));
                }
            }
            Assert.That(queue.TryGetReservation(id,out var actual),Is.True,"FIXTURE: existing canonical API committed the requested phase");
            Assert.That(actual.Generation,Is.GreaterThan(0));Assert.That(actual.VisitId,Is.EqualTo(id));
            var before=queue.Snapshot.ToArray();var leases=queue.ServiceLeases.ToArray();var version=queue.Version;
            Assert.That(queue.TryCaptureReservation(id,out var claim),Is.True,"PRODUCT: capture the real canonical Entry without mutation");
            Assert.That(claim,Is.Not.Null);Assert.That(claim.Assignment,Is.EqualTo(actual));Assert.That(claim.IsCurrent,Is.True);
            Assert.That(queue.TryCaptureReservation(id,out var second),Is.True);
            Assert.That(claim.IsSameReservation(second),Is.True);Assert.That(second.IsSameReservation(claim),Is.True);
            Assert.That(claim.IsSameReservation(null),Is.False);
            Assert.That(claim.IsCurrentCollectionLease,Is.EqualTo(phase=="collection" || phase=="yielded-collection"));
            CollectionAssert.AreEqual(before,queue.Snapshot);CollectionAssert.AreEqual(leases,queue.ServiceLeases.ToArray());
            Assert.That(queue.TryGetReservation(id,out var unchanged),Is.True);Assert.That(unchanged,Is.EqualTo(actual));
            Assert.That(queue.Version,Is.EqualTo(version));
        }

        [Test] public void PickupReservationLineageSurvivesActualPhaseChanges()
        {
            var queue=Queue();Assert.That(queue.TryReserve("a",out var reserved),Is.True);
            Assert.That(queue.TryCaptureReservation("a",out var original),Is.True);
            Assert.That(queue.TryBeginTransfer("a",out var transferred),Is.True);
            Assert.That(queue.MarkArrived("a",transferred.Generation),Is.True);
            Assert.That(queue.TryCaptureReservation("a",out var waiting),Is.True);
            Yield(queue,"a",true);Assert.That(queue.TryCaptureReservation("a",out var yielded),Is.True);
            var args=new object[]{"a",default(QueueAssignment)};Assert.That(Call(queue,"TryReturnYielded",args),Is.True);
            Assert.That(queue.MarkArrived("a",((QueueAssignment)args[1]).Generation),Is.True);
            Assert.That(queue.TryAcquireCollection("a",P(0,4)),Is.True);Assert.That(queue.TryGetReservation("a",out var service),Is.True);
            Assert.That(queue.MarkArrived("a",service.Generation),Is.True);Assert.That(queue.TryCaptureReservation("a",out var current),Is.True);
            var before=queue.Snapshot.ToArray();var version=queue.Version;
            foreach(var older in new[]{original,waiting,yielded})
            {
                Assert.That(older.IsCurrent,Is.False,"Changed assignment generation expires the exact target claim");
                Assert.That(older.IsSameReservation(current),Is.True,"Real phase changes retain the same reservation Entry");
                Assert.That(current.IsSameReservation(older),Is.True);
            }
            Assert.That(original.Assignment,Is.EqualTo(reserved));Assert.That(current.IsCurrent,Is.True);
            Assert.That(current.IsCurrentCollectionLease,Is.True);
            CollectionAssert.AreEqual(before,queue.Snapshot);Assert.That(queue.Version,Is.EqualTo(version));
        }

        [TestCase("remove-rejoin")] [TestCase("rollback")] [TestCase("foreign")]
        public void PickupReservationLineageRejectsReplacedOrForeignEntry(string replacement)
        {
            var queue=Queue();Assert.That(queue.TryReserve("same",out var originalAssignment),Is.True);
            Assert.That(queue.TryCaptureReservation("same",out var original),Is.True);var target=queue;
            if(replacement=="remove-rejoin")
            { Assert.That(queue.Remove("same"),Is.True);Assert.That(queue.TryReserve("same",out _),Is.True); }
            else if(replacement=="rollback")
            {
                Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Transfer,"same",queue.Version,out var prepared),Is.True);
                Assert.That(prepared.TryApply(out var receipt),Is.True);Assert.That(queue.Snapshot.Single().VisitId,Is.EqualTo("same"));
                receipt.RollbackOwnMutation();Assert.That(queue.Snapshot,Is.Empty);
                Assert.That(queue.TryGetReservation("same",out var restored),Is.True);Assert.That(restored,Is.EqualTo(originalAssignment));
            }
            else { target=Queue();Assert.That(target.TryReserve("same",out var foreign),Is.True);Assert.That(foreign,Is.EqualTo(originalAssignment)); }
            Assert.That(target.TryGetReservation("same",out _),Is.True,"FIXTURE: replacement/foreign reservation really exists");
            Assert.That(target.TryCaptureReservation("same",out var current),Is.True);Assert.That(current.IsCurrent,Is.True);
            if(replacement!="foreign")Assert.That(original.IsCurrent,Is.False);
            var version=queue.Version;var targetVersion=target.Version;
            Assert.That(original.IsSameReservation(current),Is.False);Assert.That(current.IsSameReservation(original),Is.False);
            Assert.That(current.IsSameReservation(current),Is.True);
            Assert.That(queue.Version,Is.EqualTo(version));Assert.That(target.Version,Is.EqualTo(targetVersion));
        }

        [Test] public void PickupCollectionLeaseClaimExpiresOnReleaseAndCannotReviveOnReacquire()
        {
            var queue=Queue();PaidArrived(queue,"a");Assert.That(queue.TryAcquireCollection("a",P(0,4)),Is.True);
            Assert.That(queue.TryGetReservation("a",out var service),Is.True);Assert.That(queue.MarkArrived("a",service.Generation),Is.True);
            Assert.That(queue.TryGetServiceLease("a",out var position),Is.True);Assert.That(position,Is.EqualTo(P(0,4)));
            Assert.That(queue.TryCaptureReservation("a",out var original),Is.True);Assert.That(original.IsCurrentCollectionLease,Is.True);
            queue.ReleaseServicePosition("a");Assert.That(queue.TryGetServiceLease("a",out _),Is.False);
            Assert.That(queue.TryGetReservation("a",out _),Is.True,"Reservation survives actual lease release");
            Assert.That(original.IsCurrentCollectionLease,Is.False);
            Assert.That(queue.TryAcquireCollection("a",P(0,4)),Is.True);Assert.That(queue.TryGetReservation("a",out var reacquired),Is.True);
            Assert.That(reacquired.Generation,Is.GreaterThan(service.Generation),"FIXTURE: real reacquisition has a later canonical generation");
            Assert.That(queue.MarkArrived("a",reacquired.Generation),Is.True);
            Assert.That(queue.TryCaptureReservation("a",out var current),Is.True);
            Assert.That(original.IsCurrentCollectionLease,Is.False,"Same actor and position cannot revive a released lease claim");
            Assert.That(current.IsCurrentCollectionLease,Is.True);Assert.That(original.IsSameReservation(current),Is.True);
        }

        [Test] public void MissingPickupReservationCannotProduceClaimOrChangeCanonicalState()
        {
            var queue=Queue();var version=queue.Version;
            Assert.That(queue.TryCaptureReservation("missing",out var claim),Is.False);Assert.That(claim,Is.Null);
            Assert.That(queue.Snapshot,Is.Empty);Assert.That(queue.ServiceLeases,Is.Empty);Assert.That(queue.Version,Is.EqualTo(version));
        }

        [TestCase("reserve")]
        [TestCase("transfer")]
        [TestCase("arrival")]
        [TestCase("advance")]
        [TestCase("begin-yield")]
        [TestCase("yield-arrival")]
        [TestCase("return-yield")]
        [TestCase("delivery-lease")]
        [TestCase("collection-lease")]
        [TestCase("yielded-collection-lease")]
        [TestCase("release-lease")]
        [TestCase("remove")]
        public void EveryCanonicalMutationAdvancesVersion(string operation)
        {
            var queue=Queue();Func<bool> mutate;
            switch(operation)
            {
                case "reserve": mutate=()=>queue.TryReserve("a",out _);break;
                case "transfer":
                    Assert.That(queue.TryReserve("a",out _),Is.True);mutate=()=>queue.TryBeginTransfer("a",out _);break;
                case "arrival":
                    Assert.That(queue.TryReserve("a",out _),Is.True);Assert.That(queue.TryBeginTransfer("a",out var moving),Is.True);
                    mutate=()=>queue.MarkArrived("a",moving.Generation);break;
                case "advance":
                    PaidArrived(queue,"a");PaidArrived(queue,"b");Assert.That(queue.Remove("a"),Is.True);
                    mutate=()=>queue.TryGetNextAdvance(out _);break;
                case "begin-yield":
                    PaidArrived(queue,"a");mutate=()=>Call(queue,"TryBeginYield","a",P(6),default(QueueAssignment));break;
                case "yield-arrival":
                    PaidArrived(queue,"a");Yield(queue,"a",false);Assert.That(queue.TryGetReservation("a",out var yielding),Is.True);
                    mutate=()=>Call(queue,"MarkYieldArrived","a",yielding.Generation);break;
                case "return-yield":
                    PaidArrived(queue,"a");Yield(queue,"a",true);mutate=()=>Call(queue,"TryReturnYielded","a",default(QueueAssignment));break;
                case "delivery-lease": mutate=()=>queue.TryAcquireDelivery("employee",P(0,4));break;
                case "collection-lease": PaidArrived(queue,"a");mutate=()=>queue.TryAcquireCollection("a",P(0,4));break;
                case "yielded-collection-lease":
                    PaidArrived(queue,"a");Yield(queue,"a",true);mutate=()=>Call(queue,"TryAcquireYieldedCollection","a",P(0,4));break;
                case "release-lease":
                    Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);mutate=()=>{queue.ReleaseServicePosition("employee");return true;};break;
                case "remove": Assert.That(queue.TryReserve("a",out _),Is.True);mutate=()=>queue.Remove("a");break;
                default: throw new ArgumentOutOfRangeException(nameof(operation));
            }
            var before=queue.Version;
            Assert.That(mutate(),Is.True,"real Queue operation must succeed");
            Assert.That(queue.Version,Is.GreaterThan(before),"Every actual canonical mutation invalidates old prepared facts: "+operation);
        }

        [Test]
        public void IdempotentReservationArrivalAndLeaseCallsDoNotInvalidatePreparedFacts()
        {
            var queue=Queue();PaidArrived(queue,"a");Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);
            var version=queue.Version;Assert.That(version,Is.GreaterThan(0));
            Assert.That(queue.TryReserve("a",out var assignment),Is.True);
            Assert.That(queue.TryBeginTransfer("a",out _),Is.True);
            Assert.That(queue.MarkArrived("a",assignment.Generation),Is.True);
            Assert.That(queue.TryAcquireDelivery("employee",P(0,4)),Is.True);
            queue.ReleaseServicePosition("stranger");Assert.That(queue.Remove("stranger"),Is.False);
            Assert.That(queue.Version,Is.EqualTo(version));
        }

        [Test]
        public void PrepareIsReadOnlyAndApplyPublishesExactlyItsOwnReservation()
        {
            var queue=Queue();var version=queue.Version;
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"a",version,out var prepared),Is.True);
            Assert.That(prepared,Is.Not.Null);Assert.That(prepared.IsCurrent,Is.True);
            Assert.That(queue.TryGetReservation("a",out _),Is.False);Assert.That(queue.Version,Is.EqualTo(version));
            Assert.That(prepared.Assignment.VisitId,Is.EqualTo("a"));Assert.That(prepared.Assignment.Position,Is.EqualTo(P(0)));
            Assert.That(prepared.TryApply(out var receipt),Is.True);Assert.That(receipt,Is.Not.Null);
            Assert.That(receipt.IsCurrent,Is.True);Assert.That(prepared.IsCurrent,Is.False);
            Assert.That(queue.TryGetReservation("a",out var actual),Is.True);
            Assert.That(actual.Position,Is.EqualTo(prepared.Assignment.Position));Assert.That(queue.Snapshot,Is.Empty,"reservation is not payment");
            Assert.That(queue.Version,Is.GreaterThan(version));
        }

        [Test]
        public void StaleExpectedVersionCannotPrepareOrApply()
        {
            var queue=Queue();var version=queue.Version;
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"a",version,out var prepared),Is.True);
            Assert.That(queue.TryReserve("other",out _),Is.True);var changed=queue.Version;
            Assert.That(prepared.IsCurrent,Is.False,"another owner changed the canonical Queue");
            Assert.That(prepared.TryApply(out _),Is.False);
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"a",version,out _),Is.False);
            Assert.That(queue.TryGetReservation("a",out _),Is.False);Assert.That(queue.Version,Is.EqualTo(changed));
        }

        [Test]
        public void ForeignQueueCannotConsumePreparedMutationEvenAtSameVersion()
        {
            var owner=Queue();var foreign=Queue();
            Assert.That(owner.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"a",owner.Version,out var prepared),Is.True);
            Assert.That(foreign.Version,Is.EqualTo(owner.Version));
            Assert.That(foreign.TryApplyPrepared(prepared,out _),Is.False);
            Assert.That(foreign.TryGetReservation("a",out _),Is.False);Assert.That(owner.TryGetReservation("a",out _),Is.False);
            Assert.That(prepared.IsCurrent,Is.True,"foreign rejection must not consume owner's preparation");
            Assert.That(prepared.TryApply(out _),Is.True);Assert.That(owner.TryGetReservation("a",out _),Is.True);
            Assert.That(foreign.TryGetReservation("a",out _),Is.False);
        }

        [Test]
        public void ReceiptRollbackIsOnceOnlyAndCannotRestoreAnOldVersion()
        {
            var queue=Queue();PaidArrived(queue,"earlier");var earlier=queue.Snapshot.Single();var version=queue.Version;
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"a",version,out var prepared),Is.True);
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"b",version,out var abandoned),Is.True);
            Assert.That(prepared.TryApply(out var receipt),Is.True);Assert.That(receipt,Is.Not.Null);var applied=queue.Version;
            Assert.That(prepared.TryApply(out _),Is.False,"one preparation cannot publish twice");
            receipt.RollbackOwnMutation();var rolledBack=queue.Version;
            Assert.That(rolledBack,Is.GreaterThan(applied));Assert.That(applied,Is.GreaterThan(version));
            Assert.That(receipt.IsCurrent,Is.False);Assert.That(abandoned.IsCurrent,Is.False);
            Assert.That(abandoned.TryApply(out _),Is.False,"rollback must not revive another pre-apply proposal (ABA)");
            Assert.That(queue.TryGetReservation("a",out _),Is.False);Assert.That(queue.TryGetReservation("b",out _),Is.False);
            Assert.That(queue.Snapshot.Single().VisitId,Is.EqualTo(earlier.VisitId));Assert.That(queue.Snapshot.Single().Generation,Is.EqualTo(earlier.Generation));
            Assert.DoesNotThrow(()=>receipt.RollbackOwnMutation());Assert.That(queue.Version,Is.EqualTo(rolledBack));
        }

        [Test]
        public void StaleReceiptCannotUndoAnotherOwnersLaterMutation()
        {
            var queue=Queue();
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"a",queue.Version,out var prepared),Is.True);
            Assert.That(prepared.TryApply(out var receipt),Is.True);Assert.That(receipt,Is.Not.Null);
            Assert.That(queue.TryReserve("b",out var other),Is.True);var later=queue.Version;
            Assert.That(receipt.IsCurrent,Is.False);
            Assert.Throws<InvalidOperationException>(()=>receipt.RollbackOwnMutation());
            Assert.That(queue.Version,Is.EqualTo(later));Assert.That(queue.TryGetReservation("a",out _),Is.True);
            Assert.That(queue.TryGetReservation("b",out var preserved),Is.True);Assert.That(preserved.Generation,Is.EqualTo(other.Generation));
        }

        [TestCase(PickupQueueService.MutationKind.Transfer)]
        [TestCase(PickupQueueService.MutationKind.Advance)]
        [TestCase(PickupQueueService.MutationKind.BeginYield)]
        [TestCase(PickupQueueService.MutationKind.ReturnYield)]
        [TestCase(PickupQueueService.MutationKind.DeliveryLease)]
        [TestCase(PickupQueueService.MutationKind.CollectionLease)]
        [TestCase(PickupQueueService.MutationKind.YieldedCollectionLease)]
        public void PreparedSpatialMutationKeepsExistingOwnerAndCanRollbackOwnChange(PickupQueueService.MutationKind kind)
        {
            var queue=Queue();var id="a";var target=kind==PickupQueueService.MutationKind.BeginYield?P(6):P(0,4);
            if(kind==PickupQueueService.MutationKind.Transfer) Assert.That(queue.TryReserve(id,out _),Is.True);
            else if(kind!=PickupQueueService.MutationKind.DeliveryLease) PaidArrived(queue,id);
            if(kind==PickupQueueService.MutationKind.Advance)
            { PaidArrived(queue,"b");Assert.That(queue.Remove(id),Is.True);id="b"; }
            if(kind==PickupQueueService.MutationKind.ReturnYield || kind==PickupQueueService.MutationKind.YieldedCollectionLease) Yield(queue,id,true);
            var hadReservation=queue.TryGetReservation(id,out var original);var paid=queue.Snapshot.Select(a=>a.VisitId).ToArray();var version=queue.Version;
            Assert.That(queue.TryPrepareMutation(kind,id,version,out var prepared,target),Is.True);
            Assert.That(queue.Version,Is.EqualTo(version));Assert.That(prepared.TryApply(out var receipt),Is.True);Assert.That(receipt,Is.Not.Null);
            Assert.That(queue.Version,Is.GreaterThan(version));
            if(kind==PickupQueueService.MutationKind.DeliveryLease || kind==PickupQueueService.MutationKind.CollectionLease || kind==PickupQueueService.MutationKind.YieldedCollectionLease)
            { Assert.That(queue.TryGetServiceLease(id,out var lease),Is.True);Assert.That(lease,Is.EqualTo(target)); }
            else if(kind==PickupQueueService.MutationKind.Transfer) Assert.That(queue.Snapshot.Select(a=>a.VisitId),Does.Contain(id));
            else
            { Assert.That(queue.TryGetReservation(id,out var changed),Is.True);Assert.That(changed.Position,Is.EqualTo(kind==PickupQueueService.MutationKind.BeginYield?P(6):P(0))); }
            receipt.RollbackOwnMutation();Assert.That(queue.TryGetReservation(id,out var restored),Is.EqualTo(hadReservation));
            if(hadReservation) { Assert.That(restored.Position,Is.EqualTo(original.Position));Assert.That(restored.Arrived,Is.EqualTo(original.Arrived)); }
            CollectionAssert.AreEqual(paid,queue.Snapshot.Select(a=>a.VisitId));Assert.That(queue.ServiceLeases,Is.Empty);
        }

        [Test]
        public void AdvanceCallbackCannotCommitAfterReentrantOwnerRemoval()
        {
            var queue=Queue();PaidArrived(queue,"a");PaidArrived(queue,"b");Assert.That(queue.Remove("a"),Is.True);
            var removed=false;
            var advanced=queue.TryGetNextAdvance(out _,candidate=>{removed=queue.Remove(candidate.VisitId);return true;});
            Assert.That(removed,Is.True);Assert.That(advanced,Is.False,"callback invalidated the canonical owner before apply");
            Assert.That(queue.TryGetReservation("b",out _),Is.False);Assert.That(queue.Snapshot,Is.Empty);
        }

        [Test]
        public void JointCommitPublishesBothFactsAndPublicationReentryCannotApplyTwice()
        {
            var queue=Queue();var key=new TrafficIntentKey("scene",1,"a","parking",1,7,"visit.a");
            var world=new TrafficWorldSnapshot(1,1,null,null);
            var coordinator=new ServiceTrafficCoordinator("scene",1,7,world,(a,b)=>new[]{a,b});
            coordinator.EvaluateParking(new TrafficRequest(key,1,world,P(-2),P(0),.4f,"slot.a"));coordinator.ProcessReady(1);
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"a",queue.Version,out var prepared),Is.True);
            var published=0;var sawBoth=false;var reentered=false;var decision=coordinator.GetDecision(key);
            Assert.That(coordinator.TryCommit(decision,prepared,()=>
            {
                published++;sawBoth=queue.TryGetReservation("a",out _) && coordinator.HasActiveGrant(key);
                reentered=prepared.TryApply(out _) || coordinator.TryCommit(decision,prepared);
            }),Is.True);
            Assert.That(published,Is.EqualTo(1));Assert.That(sawBoth,Is.True);Assert.That(reentered,Is.False);
            Assert.That(queue.TryGetReservation("a",out _),Is.True);Assert.That(coordinator.HasActiveGrant(key),Is.True);
        }

        private static TrafficRequest ReservedPermit(out PickupQueueService queue,out ServiceTrafficCoordinator coordinator,out TrafficDecision permission)
        {
            queue=Queue();var key=new TrafficIntentKey("scene",1,"a","parking",1,7,"visit.a");
            var initial=new TrafficWorldSnapshot(1,1,new[]{new TrafficBodyFact(key,P(-2),.4f)},null);
            coordinator=new ServiceTrafficCoordinator("scene",1,7,initial,(a,b)=>new[]{a,b});
            coordinator.EvaluateParking(new TrafficRequest(key,1,initial,P(-2),P(0),.4f,"pickup/a",planPrimaryRoute:true));coordinator.ProcessReady(1);
            permission=coordinator.GetDecision(key);Assert.That(permission.Kind,Is.EqualTo(TrafficDecisionKind.Granted));
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"a",queue.Version,out var prepared),Is.True);
            Assert.That(coordinator.TryCommit(permission,prepared),Is.True);
            var current=new TrafficWorldSnapshot(2,1,new[]{new TrafficBodyFact(key,P(-2),.4f)},
                new[]{new TrafficParkingFact(key,queue,queue.Version,"pickup/a",P(0),.4f)});
            coordinator.SetSnapshot(current);
            return new TrafficRequest(key,2,current,P(-2),P(0),.4f,"pickup/a",planPrimaryRoute:true);
        }

        [Test]
        public void PaidTransferConsumesExistingPermitWithoutNewQueriesOrReentrantPayment()
        {
            var current=ReservedPermit(out var queue,out var coordinator,out var permission);
            var sequence=permission.AcquiredSequence;var queries=coordinator.QueriesConsumed(permission.Intent);
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Transfer,"a",queue.Version,out var prepared),Is.True);
            var version=queue.Version;var published=0;var sawPaidAndGrant=false;var reentered=false;
            Assert.That(coordinator.TryContinueCanonical(permission,current,queue,version,prepared,()=>
            {
                published++;sawPaidAndGrant=queue.Snapshot.Single().VisitId=="a" && coordinator.HasActiveGrant(permission.Intent);
                reentered=coordinator.TryContinueCanonical(permission,current,queue,version,prepared);
            }),Is.True);
            Assert.That(published,Is.EqualTo(1));Assert.That(sawPaidAndGrant,Is.True);Assert.That(reentered,Is.False);
            Assert.That(permission.AcquiredSequence,Is.EqualTo(sequence));Assert.That(coordinator.QueriesConsumed(permission.Intent),Is.EqualTo(queries));
            Assert.That(queue.Snapshot.Count,Is.EqualTo(1));
        }

        [TestCase("actual-start")]
        [TestCase("actual-body")]
        [TestCase("context")]
        [TestCase("target")]
        [TestCase("resource")]
        [TestCase("foreign-owner")]
        [TestCase("owner-version")]
        [TestCase("queue-version")]
        public void InvalidCanonicalContinuationKeepsOriginalParkingAndGrant(string mismatch)
        {
            var current=ReservedPermit(out var queue,out var coordinator,out var permission);
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Transfer,"a",queue.Version,out var prepared),Is.True);
            var ownerVersion=queue.Version;
            if(mismatch=="queue-version")Assert.That(queue.TryReserve("b",out _),Is.True);
            var snapshot=current.Snapshot;
            if(mismatch=="actual-body")
            {snapshot=new TrafficWorldSnapshot(snapshot.Version,2,new[]{new TrafficBodyFact(current.Intent,P(-1),.4f)},snapshot.Parking);coordinator.SetSnapshot(snapshot);}
            var key=mismatch=="context"?new TrafficIntentKey("scene",1,"a","parking",2,7,"another-visit"):current.Intent;
            var changed=new TrafficRequest(key,2,snapshot,mismatch=="actual-start"?P(-1):current.Start,
                mismatch=="target"?P(1):current.Target,.4f,mismatch=="resource"?"other-resource":current.ResourceId,planPrimaryRoute:true);
            var published=0;
            Assert.That(coordinator.TryContinueCanonical(permission,changed,mismatch=="foreign-owner"?Queue():queue,
                mismatch=="owner-version"?ownerVersion+1:ownerVersion,prepared,()=>published++),Is.False);
            Assert.That(queue.TryGetReservation("a",out var original),Is.True);Assert.That(original.Position,Is.EqualTo(P(0)));
            Assert.That(queue.Snapshot,Is.Empty);Assert.That(published,Is.Zero);Assert.That(coordinator.HasActiveGrant(permission.Intent),Is.True);
        }

        [Test]
        public void FailedPaidTransferRollsBackOwnWriteAndRetainsExistingParkingPermit()
        {
            var current=ReservedPermit(out var queue,out var coordinator,out var permission);
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Transfer,"a",queue.Version,out var prepared),Is.True);
            var version=queue.Version;var published=0;
            Assert.That(coordinator.TryContinueCanonical(permission,current,queue,version,new FailAfterQueueApply(prepared),()=>published++),Is.False);
            Assert.That(queue.Snapshot,Is.Empty);Assert.That(queue.TryGetReservation("a",out _),Is.True);
            Assert.That(queue.Version,Is.GreaterThan(version));Assert.That(coordinator.HasActiveGrant(permission.Intent),Is.True);Assert.That(published,Is.Zero);
        }
        [TestCase("false")]
        [TestCase("throw")]
        [TestCase("null")]
        [TestCase("stale")]
        [TestCase("rollback-unknown")]
        public void Fix3AttemptedCanonicalContinuationFailureClosesMutationButKeepsPhysicalParking(string mode)
        {
            var current=ReservedPermit(out var queue,out var coordinator,out var permission);
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Transfer,"a",queue.Version,out var prepared),Is.True);
            var attempted=new ContinuationMutation(prepared,mode);var published=0;var queries=coordinator.QueriesConsumed(permission.Intent);
            Assert.That(coordinator.TryContinueCanonical(permission,current,queue,queue.Version,attempted,()=>published++),Is.False);
            Assert.That(attempted.Applies,Is.EqualTo(1));Assert.That(attempted.ActualApplied,Is.True,"FIXTURE: real Queue Transfer occurred before the modeled failure");
            var rollbackCalls=attempted.Rollbacks;
            var terminal=coordinator.GetDecision(permission.Intent);
            Assert.That(terminal.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.Terminal),"PRODUCT: actual canonical uncertainty closes continuation authority");
            Assert.That(queue.TryGetReservation("a",out var held),Is.True);Assert.That(coordinator.HasActiveGrant(permission.Intent),Is.True,
                "Existing physical parking protection survives business mutation failure");
            var version=queue.Version;var paid=queue.Snapshot.Select(a=>a.VisitId).ToArray();
            var snapshot=new TrafficWorldSnapshot(3,2,current.Snapshot.Bodies,
                new[]{new TrafficParkingFact(permission.Intent,queue,version,"pickup/a",held.Position,.4f)});
            coordinator.SetSnapshot(snapshot);
            var refreshed=new TrafficRequest(permission.Intent,3,snapshot,current.Start,current.Target,.4f,current.ResourceId,planPrimaryRoute:true);
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,"b",version,out var nextPrepared),Is.True,
                "FIXTURE: a fresh genuine canonical mutation exists, so refusal is not a stale receipt shortcut");
            var next=new ContinuationMutation(nextPrepared,"success");
            Assert.That(coordinator.EvaluateParking(refreshed),Is.SameAs(terminal));
            Assert.That(coordinator.TryContinueCanonical(permission,refreshed,queue,version,next,()=>published++),Is.False);
            Assert.That(coordinator.TryContinueCanonical(terminal,refreshed,queue,version,next,()=>published++),Is.False);
            Assert.That(coordinator.TryCommit(terminal,next,()=>published++),Is.False);
            Assert.That(coordinator.CompleteLeg(permission.Intent,default),Is.False,"A retained parking guard is not successful legacy movement");
            Assert.That(coordinator.RetainNamedJourney(permission.Intent,current.ResourceId),Is.False,"Terminal protection cannot grant new movement authority");
            Assert.That(coordinator.DiscardPrepared(permission.Intent),Is.False);
            Assert.That(next.Applies,Is.Zero);Assert.That(next.Rollbacks,Is.Zero);Assert.That(published,Is.Zero);
            Assert.That(attempted.Applies,Is.EqualTo(1));Assert.That(attempted.Rollbacks,Is.EqualTo(rollbackCalls));
            Assert.That(rollbackCalls,Is.LessThanOrEqualTo(1));Assert.That(queue.Version,Is.EqualTo(version));
            Assert.That(queue.Snapshot.Select(a=>a.VisitId),Is.EqualTo(paid));Assert.That(queue.TryGetReservation("b",out _),Is.False);
            Assert.That(coordinator.HasActiveGrant(permission.Intent),Is.True);Assert.That(coordinator.QueriesConsumed(permission.Intent),Is.EqualTo(queries));
            var intruder=new TrafficIntentKey("scene",1,"intruder","parking",1,7,"other");
            coordinator.EvaluateParking(new TrafficRequest(intruder,4,snapshot,P(-4,2),held.Position,.4f,"other"));coordinator.ProcessReady(4);
            Assert.That(coordinator.GetDecision(intruder).Kind,Is.Not.EqualTo(TrafficDecisionKind.Granted));
            Assert.That(snapshot.Parking.Single().CanonicalOwner,Is.SameAs(queue));Assert.That(snapshot.Bodies,Is.EquivalentTo(current.Snapshot.Bodies));
        }

        [Test] public void Fix3CanonicalContinuationPreflightRejectionDoesNotConsumeValidAuthority()
        {
            var current=ReservedPermit(out var queue,out var coordinator,out var permission);
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Transfer,"a",queue.Version,out var prepared),Is.True);
            var mutation=new ContinuationMutation(prepared,"success");var published=0;
            Assert.That(coordinator.TryContinueCanonical(permission,current,queue,queue.Version+1,mutation,()=>published++),Is.False);
            Assert.That(mutation.Applies,Is.Zero);Assert.That(mutation.Rollbacks,Is.Zero);Assert.That(coordinator.GetDecision(permission.Intent),Is.SameAs(permission));
            Assert.That(permission.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None));
            Assert.That(coordinator.TryContinueCanonical(permission,current,queue,queue.Version,mutation,()=>published++),Is.True);
            Assert.That(mutation.Applies,Is.EqualTo(1));Assert.That(published,Is.EqualTo(1));Assert.That(coordinator.HasActiveGrant(permission.Intent),Is.True);
        }

        [Test] public void Fix3SuccessfulCanonicalContinuationSurvivesPublicationExceptionWithoutReapplying()
        {
            var current=ReservedPermit(out var queue,out var coordinator,out var permission);
            Assert.That(queue.TryPrepareMutation(PickupQueueService.MutationKind.Transfer,"a",queue.Version,out var prepared),Is.True);
            var mutation=new ContinuationMutation(prepared,"success");var published=0;var version=queue.Version;
            Assert.That(coordinator.TryContinueCanonical(permission,current,queue,version,mutation,()=>{published++;throw new InvalidOperationException("publication");}),Is.True);
            Assert.That(coordinator.LastPublicationException,Is.Not.Null);Assert.That(queue.Snapshot.Single().VisitId,Is.EqualTo("a"));
            Assert.That(coordinator.TryContinueCanonical(permission,current,queue,version,mutation,()=>published++),Is.False);
            Assert.That(mutation.Applies,Is.EqualTo(1));Assert.That(mutation.Rollbacks,Is.Zero);Assert.That(published,Is.EqualTo(1));
            Assert.That(coordinator.HasActiveGrant(permission.Intent),Is.True);Assert.That(coordinator.GetDecision(permission.Intent),Is.SameAs(permission));
        }

        // The underlying mutation really writes the canonical Queue; only its returned failure contract varies.
        // 不伪造 Queue 状态；包装实际 apply/receipt 来证明异常后不能再次写入。
        private sealed class ContinuationMutation : IPreparedTrafficMutation,ITrafficMutationReceipt
        {
            private readonly IPreparedTrafficMutation inner;private readonly string mode;private ITrafficMutationReceipt actual;
            internal int Applies,Rollbacks;internal bool ActualApplied;
            internal ContinuationMutation(IPreparedTrafficMutation value,string failure){inner=value;mode=failure;}
            public bool IsCurrent=>actual==null?inner.IsCurrent:mode!="stale" && actual.IsCurrent;
            public bool TryApply(out ITrafficMutationReceipt receipt)
            {
                Applies++;ActualApplied=inner.TryApply(out actual);if(!ActualApplied){receipt=null;return false;}
                receipt=mode=="null"?null:this;if(mode=="throw")throw new InvalidOperationException("actual apply then throw");
                return mode=="success" || mode=="null" || mode=="stale";
            }
            public void RollbackOwnMutation()
            {Rollbacks++;if(mode=="rollback-unknown")throw new InvalidOperationException("rollback UNKNOWN");actual.RollbackOwnMutation();}
        }

        private sealed class FailAfterQueueApply : IPreparedTrafficMutation
        {
            private readonly IPreparedTrafficMutation inner;
            internal FailAfterQueueApply(IPreparedTrafficMutation inner){this.inner=inner;}
            public bool IsCurrent=>inner.IsCurrent;
            public bool TryApply(out ITrafficMutationReceipt receipt){inner.TryApply(out receipt);return false;}
        }
    }
}

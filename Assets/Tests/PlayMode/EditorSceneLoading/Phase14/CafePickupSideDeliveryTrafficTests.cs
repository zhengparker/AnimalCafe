#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.CafeLoop;
using AnimalCafe.Capacity;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.Employees;
using AnimalCafe.Navigation;
using AnimalCafe.Orders;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode.Phase14
{
    public sealed partial class CafePickupSideTests
    {
        // Real selected route + body + accepted native request; no safety setting is changed.
        [UnityTest] public IEnumerator SingleSideSelectedUnissuedDeliverySegmentRecognizesFiniteMovingBodyAndKeepsOriginalWork()
        {
            // One fixed formal Decor input before any visit/Source; no coordinate or side search.
            // 只把原CM台面向西移两格，给原midpoint body留出真实绕行空间；不改Pickup单侧。
            var preparationStartFrame=Time.frameCount;
            int PreparationRemaining()=>6000-(Time.frameCount-preparationStartFrame);
            void EmptyBusiness()
            {
                Assert.That(customers.Snapshot,Is.Empty);Assert.That(owner.Snapshot.Orders,Is.Empty);
                Assert.That(owner.Snapshot.PaidVisitIds,Is.Empty);Assert.That(customers.CounterAssignments,Is.Empty);
                Assert.That(customers.PickupAssignments,Is.Empty);
                Assert.That(customers.Capacity==null || !customers.Capacity.GetReservations().Any(),Is.True);
                Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.Zero);
                Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(customers,"trafficCandidates").Count,Is.Zero);
                Assert.That(CafeLoopRuntimeProof.Field<object>(customers,"pendingAdmissionAnchor"),Is.Null);
            }
            EmptyBusiness();var runtime=Find<CafeLayoutRuntime>();var decor=Find<DecorationModeController>();
            var machine=runtime.FunctionalSurfaceLayout.MountedInstances.Single(item=>item.DefinitionId=="equipment.coffee-machine.01");
            var support=runtime.Layout.FurnitureInstances.Single(item=>item.InstanceId==machine.Address.SupportFurnitureInstanceId);
            var originalPickup=runtime.FunctionalSurfaceLayout.PickUpPoints.Single();var originalPickupAddress=originalPickup.Address;
            Assert.That(support.Position,Is.EqualTo(new GridPosition(6,3)),"Fixed SharedPickup recipe requires the original machine support");
            decor.EnterDecorationMode();yield return null;EmptyBusiness();
            var session=CafeLoopRuntimeProof.Field<DecorationSession>(decor,"session");
            Assert.That(session.BeginExisting(support.InstanceId).Succeeded,Is.True);
            Assert.That(session.MovePreview(new GridPosition(4,3)).Succeeded,Is.True,"One fixed legal support move; no fallback position");
            yield return MatrixConfirmFurniture();decor.ExitDecorationMode();
            while((!owner.CanProgress || employees.Snapshot.Any(item=>item.State!=EmployeeState.Idle)) && PreparationRemaining()>0)
            {EmptyBusiness();yield return null;}
            EmptyBusiness();Assert.That(PreparationRemaining(),Is.GreaterThan(0),"Fixed Decor preparation consumes the original first 6000-frame window");
            Assert.That(owner.CanProgress && employees.Snapshot.All(item=>item.State==EmployeeState.Idle),Is.True,"Formal layout must really resume before any proposal");
            Assert.That(runtime.Layout.FurnitureInstances.Single(item=>item.InstanceId==support.InstanceId).Position,Is.EqualTo(new GridPosition(4,3)));
            Assert.That(runtime.FunctionalSurfaceLayout.MountedInstances.Single(item=>item.InstanceId==machine.InstanceId).Address,Is.EqualTo(machine.Address));
            Assert.That(runtime.FunctionalSurfaceLayout.PickUpPoints.Single().InstanceId,Is.EqualTo(originalPickup.InstanceId));
            Assert.That(runtime.FunctionalSurfaceLayout.PickUpPoints.Single().Address,Is.EqualTo(originalPickupAddress));
            var captureOriginal=Environment.GetCommandLineArgs().Contains("-singleSideDeliveryOriginalTrace");
            using var originalWaitTrace=captureOriginal?new TrafficWaitFailureTrace(
                CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic")):null;
            InitializeLegacySingleSideHeldReadyInput();
            Assert.That(employees.TryGetBinding(EmployeeRole.Barista,out var initialBinding),Is.True);
            Assert.That(Vector3.Distance(initialBinding.WorkTarget.Position,new Vector3(.5f,0,.5f)),Is.LessThan(.001f));
            Assert.That(initialBinding.PickUpTarget.HasValue,Is.True);
            Assert.That(Vector3.Distance(initialBinding.PickUpTarget.Value.Position,new Vector3(2.5f,0,2.5f)),Is.LessThan(.001f));
            var actualPickupId=runtime.FunctionalSurfaceLayout.PickUpPoints.Single().InstanceId;
            Assert.That(customers.GetPickupServiceTargets(actualPickupId).Count,Is.EqualTo(1));
            TestContext.WriteLine("SINGLE_SIDE_FIXED_WORK_SUPPORT support="+support.InstanceId+" grid=(4,3) work="+initialBinding.WorkTarget.Position.ToString("F6")+
                " service="+initialBinding.PickUpTarget.Value.Position.ToString("F6")+" sourceBeforeFirstProposal=True remainingPreparationFrames="+PreparationRemaining());
            var shaping=Blocker((initialBinding.WorkTarget.Position+initialBinding.PickUpTarget.Value.Position)/2,
                "delivery-original-route-shaping-body");
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,PreparationRemaining());
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            var visit=customers.Snapshot.Single().VisitId;
            EmployeeTaskRunner runner=null;NavigationActor barista=null;NavigationTarget[] route=null;var index=0;
            var bodyPosition=Vector3.zero;var routeLogged=false;
            var bodySettings=CafeLoopRuntimeProof.Field<NavigationActor>(tools,"blockerPrefab").Settings;
            var staffSettings=world.RegisteredActors.Single(actor=>employees.Snapshot.Any(row=>row.Role==EmployeeRole.Barista && row.ActorId==actor.ActorId)).Settings;
            var pairClearance=bodySettings.AgentRadius+staffSettings.AgentRadius+Mathf.Max(bodySettings.CollisionSkin+bodySettings.Epsilon,staffSettings.CollisionSkin+staffSettings.Epsilon);
            float FlatDistance(Vector3 a,Vector3 b)=>new Vector2(a.x-b.x,a.z-b.z).magnitude;
            bool AtPriorArrival()
            {
                Assert.That(PreparationRemaining(),Is.GreaterThan(0),"NOT_PROVEN: original 6000-frame preparation/debt window exhausted");
                runner=CafeLoopRuntimeProof.Field<IEnumerable>(employees,"employees").Cast<object>()
                    .Select(e=>CafeLoopRuntimeProof.Field<EmployeeTaskRunner>(e,"Runner"))
                    .Single(r=>r!=null && r.Snapshot.Role==EmployeeRole.Barista);
                if(runner.Snapshot.State!=EmployeeState.CarryingToPickUp)return false;
                route=CafeLoopRuntimeProof.Field<NavigationTarget[]>(runner,"deliveryRoute");
                index=CafeLoopRuntimeProof.Field<int>(runner,"deliveryRouteIndex");
                if(route==null || route.Length<3 || index!=0)return false;
                barista=world.RegisteredActors.Single(a=>a.ActorId==runner.Snapshot.ActorId);
                if(!routeLogged){routeLogged=true;TestContext.WriteLine("P14_SELECTED_ROUTE "+string.Join(";",route.Select(t=>t.Position.ToString("F6"))));}
                // Fixed ordinal0/right offset at the real temporary bend; not a new canonical parking target.
                // 只用本次真实首弯的固定右侧offset；真正service lease仍由原Parking.All严格保护。
                var direction=route[1].Position-route[0].Position;direction.y=0;if(direction.sqrMagnitude==0)return false;
                bodyPosition=route[1].Position+Vector3.Cross(Vector3.up,direction.normalized)*.65f;
                return Vector3.Distance(barista.transform.position,route[index].Position)<.16f &&
                    Vector3.Distance(barista.transform.position,route[index].Position)>barista.Settings.ArrivalDistance+barista.Settings.Epsilon &&
                    adapter.IsPointWalkable(bodyPosition) && world.RegisteredActors.All(a=>Vector3.Distance(a.transform.position,bodyPosition)>.92f) &&
                    FlatDistance(bodyPosition,route[1].Position)<.85f &&
                    CafeLoopRuntimeProof.Field<TrafficWorldSnapshot>(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic"),"snapshot").Parking.All(claim=>
                        FlatDistance(bodyPosition,claim.Position)>=bodySettings.AgentRadius+claim.Radius+Mathf.Max(bodySettings.CollisionSkin+bodySettings.Epsilon,claim.SafetyMargin));
            }
            yield return Until(AtPriorArrival);
            var target=route[index+1];var handle=runner.Snapshot.HandleId;var saved=runner.CaptureCheckpoint();
            var queue=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue");
            Assert.That(queue.TryGetServiceLease(barista.ActorId,out var lease),Is.True);
            var core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic");
            var entries=CafeLoopRuntimeProof.Field<IDictionary>(core,"entries");
            var employee=CafeLoopRuntimeProof.Field<IEnumerable>(employees,"employees").Cast<object>()
                .Single(item=>ReferenceEquals(MovementRead(item,"Runner"),runner));
            var movement=MovementRead(MovementRead(employee,"Port"),"trafficMove");Assert.That(movement,Is.Not.Null);
            var originalSource=MovementRead(movement,"Source") as TrafficDecision;Assert.That(originalSource,Is.Not.Null);
            var originalIntent=(TrafficIntentKey)MovementRead(movement,"Intent");Assert.That(entries.Contains(originalIntent),Is.True);
            var originalEntry=entries[originalIntent];var priorCoreLeg=MovementRead(originalEntry,"Leg");Assert.That(priorCoreLeg,Is.Not.Null);
            var priorStamp=(TrafficNativeStamp)MovementRead(priorCoreLeg,"Stamp");
            var first=MovementRead(originalEntry,"FirstFrame");var sequence=MovementRead(originalEntry,"RequestSequence");
            var acquisition=MovementRead(originalEntry,"AcquiredSequence");var finite=MovementRead(originalEntry,"FiniteLimit");
            var previousQueries=(int)MovementRead(originalEntry,"Queries");var debtBefore=0f;
            Assert.That(MovementRead(originalEntry,"JourneyPermit"),Is.SameAs(originalSource));
            Assert.That(originalSource.AcquiredSequence,Is.GreaterThan(0));
            Assert.That(MovementRead(originalEntry,"FrozenOwners"),Is.Null,"NOT_PROVEN: this selected source already owes an earlier finite dependency");
            var heldCup=barista.GetComponentsInChildren<Transform>(true).Single(item=>item.name=="EmployeeHeldCup");
            var originalTokens=customers.Capacity.GetReservations().Where(item=>item.OwnerId==visit).Select(item=>item.Token).ToArray();
            var heldTokenStates=customers.Capacity.GetReservations().Where(item=>item.OwnerId==visit).Select(item=>(item.Token,item.Kind,item.State)).ToArray();
            var observedTokenStates=heldTokenStates.ToDictionary(item=>item.Token,item=>item.State);
            Assert.That(queue.TryGetReservation(visit,out var originalPickupRow),Is.True);
            var heldOrders=owner.Snapshot.Orders.ToDictionary(item=>item.Key,item=>item.Value.State);var heldPaid=owner.Snapshot.PaidVisitIds.ToArray();
            void SameWork()
            {
                Assert.That(runner.Snapshot.State,Is.EqualTo(EmployeeState.CarryingToPickUp));
                Assert.That(runner.Snapshot.HandleId,Is.EqualTo(handle));Assert.That(runner.CaptureCheckpoint(),Is.EqualTo(saved));
                Assert.That(queue.TryGetServiceLease(barista.ActorId,out var currentLease),Is.True);Assert.That(currentLease,Is.EqualTo(lease));
                Assert.That(heldCup!=null && heldCup.IsChildOf(barista.transform),Is.True);
                CollectionAssert.AreEquivalent(originalTokens,customers.Capacity.GetReservations().Where(item=>item.OwnerId==visit).Select(item=>item.Token));
                foreach(var token in heldTokenStates)
                {
                    var current=customers.Capacity.GetReservations().Single(item=>ReferenceEquals(item.Token,token.Token));
                    Assert.That(current.OwnerId,Is.EqualTo(visit));Assert.That(current.Kind,Is.EqualTo(token.Kind));
                    var previous=observedTokenStates[token.Token];
                    if(current.State!=previous)
                    {
                        // Same real Pickup token may become Occupied only after its actual canonical arrival.
                        // 仅允许原Pickup token真实到位后的单向Reserved→Occupied；不允许release/回退/换token。
                        Assert.That(token.Kind,Is.EqualTo(CapacityKind.PickUp));Assert.That(previous,Is.EqualTo(ReservationState.Reserved));
                        Assert.That(current.State,Is.EqualTo(ReservationState.Occupied));
                        Assert.That(queue.TryGetReservation(visit,out var arrivedRow),Is.True);Assert.That(arrivedRow.Arrived,Is.True);
                        Assert.That(arrivedRow.Generation,Is.EqualTo(originalPickupRow.Generation));Assert.That(arrivedRow.Position,Is.EqualTo(originalPickupRow.Position));
                        var arrival=customers.CafeSnapshot.Single(item=>item.VisitId==visit);
                        Assert.That(arrival.IsEnabled && arrival.PickupArrived && !arrival.PickupDeparted && !arrival.ExitArrived,Is.True);
                        Assert.That(arrival.NavigationState,Is.EqualTo(CustomerVisitState.PickupWaiting));
                        var customer=world.RegisteredActors.Single(actor=>actor.ActorId==visit);
                        Assert.That(Vector3.Distance(customer.transform.position,arrivedRow.Position),Is.LessThanOrEqualTo(customer.Settings.ArrivalDistance+customer.Settings.Epsilon));
                        observedTokenStates[token.Token]=current.State;
                    }
                }
                CollectionAssert.AreEquivalent(heldPaid,owner.Snapshot.PaidVisitIds);Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(heldOrders.Count));
                foreach(var order in heldOrders)Assert.That(owner.Snapshot.Orders[order.Key].State,Is.EqualTo(order.Value));
                Assert.That(CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue"),Is.SameAs(queue));
                Assert.That(MovementRead(MovementRead(employee,"Port"),"trafficMove"),Is.SameAs(movement));
                Assert.That(entries[originalIntent],Is.SameAs(originalEntry));Assert.That(MovementRead(movement,"Source"),Is.SameAs(originalSource));
                Assert.That(MovementRead(originalEntry,"JourneyPermit"),Is.SameAs(originalSource));
                Assert.That(MovementRead(originalEntry,"FirstFrame"),Is.EqualTo(first));Assert.That(MovementRead(originalEntry,"RequestSequence"),Is.EqualTo(sequence));
                Assert.That(MovementRead(originalEntry,"AcquiredSequence"),Is.EqualTo(acquisition));Assert.That(MovementRead(originalEntry,"FiniteLimit"),Is.EqualTo(finite));
                var q=(int)MovementRead(originalEntry,"Queries");Assert.That(q,Is.GreaterThanOrEqualTo(previousQueries));previousQueries=q;
            }
            Assert.That(MovementRead(priorCoreLeg,"Committed"),Is.EqualTo(true));Assert.That(MovementRead(priorCoreLeg,"Bound"),Is.EqualTo(true));
            Assert.That(MovementRead(priorCoreLeg,"Completed"),Is.EqualTo(false));Assert.That(priorStamp.RequestId,Is.GreaterThan(0));
            var natives=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor");
            void PriorLegStillCurrent()
            {
                Assert.That(PreparationRemaining(),Is.GreaterThan(0));SameWork();Assert.That(MovementRead(originalEntry,"Leg"),Is.SameAs(priorCoreLeg));
                Assert.That(MovementRead(priorCoreLeg,"ActorLife"),Is.SameAs(barista));
                Assert.That(MovementRead(priorCoreLeg,"Committed"),Is.EqualTo(true));Assert.That(MovementRead(priorCoreLeg,"Bound"),Is.EqualTo(true));
                Assert.That(MovementRead(priorCoreLeg,"Completed"),Is.EqualTo(false),"NOT_PROVEN: the real prior Native leg completed during the bounded time transition");
                Assert.That(MovementRead(priorCoreLeg,"Stamp"),Is.EqualTo(priorStamp));Assert.That(natives.Contains(barista.ActorId),Is.True);
                Assert.That(MovementRead(natives[barista.ActorId],"Id"),Is.EqualTo(priorStamp.RequestId));
                var nativeLeg=MovementRead(runner,"moveLeg");Assert.That(nativeLeg,Is.Not.Null);
                Assert.That(MovementRead(nativeLeg,"RequestId"),Is.EqualTo(priorStamp.RequestId));
                Assert.That(MovementRead(nativeLeg,"NativeLegGeneration"),Is.EqualTo(priorStamp.LegGeneration));
            }
            object bodyEntry=null;NavigationActor body=null;TrafficIntentKey bodyKey=default;TrafficDecision bodySource=null;TrafficNativeStamp bodyStamp=default;
            MovementResult? completed=null;var bodyCoreCompleted=false;var readingBody=true;
            var savedCapture=Time.captureDeltaTime;var savedScale=Time.timeScale;var savedStepping=CafeLoopRuntimeProof.Field<bool>(owner,"stepping");
            Assert.That(savedStepping,Is.False,"Fixture must start from the original normal driver");
            try
            {
                // Only real zero-delta driver frames prepare the new source; the selected old Native leg stays unchanged.
                // 冻结物理时间但不手动pump Core；原source/杯/lease/订单/预算全部保留。
                CafeLoopRuntimeProof.Set(owner,"stepping",true);Time.captureDeltaTime=0;Time.timeScale=0;
                // A residual positive frame may move other actors; authenticate only the original owned task/leg here.
                // 切换到真实delta0之前允许正常残余移动；绝不回写pose或把已完成腿重新变成pending。
                for(var transition=0;transition<3 && Time.deltaTime!=0;transition++){PriorLegStillCurrent();yield return null;PriorLegStillCurrent();}
                Assert.That(Time.deltaTime,Is.Zero,"NOT_PROVEN: actual zero-delta transition was not reached");
                PriorLegStillCurrent();debtBefore=(float)MovementRead(originalEntry,"WaitRemaining");
                // Capture all frozen physical/native/canonical baselines only after the actual zero-delta boundary.
                // 真正delta0后才捕获冻结基线；之前的合法残余tick不属于这个基线。
                var poses=world.RegisteredActors.ToDictionary(actor=>actor,actor=>actor.transform.position);var expectedActorCount=poses.Count;
                var originalNative=natives.Keys.Cast<string>().ToDictionary(id=>id,id=>natives[id]);
                var originalOrders=owner.Snapshot.Orders.ToDictionary(item=>item.Key,item=>item.Value.State);
                var originalPaid=owner.Snapshot.PaidVisitIds.ToArray();var version=queue.Version;
                var tokens=customers.Capacity.GetReservations().Select(item=>(item.Token,item.State)).ToArray();
                void FrozenFacts()
                {
                    Assert.That(Time.deltaTime,Is.Zero);PriorLegStillCurrent();
                    Assert.That((float)MovementRead(originalEntry,"WaitRemaining"),Is.EqualTo(debtBefore));
                    Assert.That(world.RegisteredActors.Count,Is.EqualTo(expectedActorCount));
                    foreach(var pose in poses)Assert.That(pose.Key.transform.position,Is.EqualTo(pose.Value),"Zero-time preparation cannot move an existing body");
                    Assert.That(natives.Count,Is.EqualTo(originalNative.Count));foreach(var request in originalNative)Assert.That(natives[request.Key],Is.SameAs(request.Value));
                    Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(originalOrders.Count));foreach(var order in originalOrders)Assert.That(owner.Snapshot.Orders[order.Key].State,Is.EqualTo(order.Value));
                    CollectionAssert.AreEquivalent(originalPaid,owner.Snapshot.PaidVisitIds);Assert.That(queue.Version,Is.EqualTo(version));
                    CollectionAssert.AreEquivalent(tokens.Select(item=>item.Token),customers.Capacity.GetReservations().Select(item=>item.Token));
                    foreach(var token in tokens)Assert.That(customers.Capacity.GetReservations().Single(item=>ReferenceEquals(item.Token,token.Token)).State,Is.EqualTo(token.State));
                }
                FrozenFacts();
                body=Blocker(bodyPosition,"delivery-segment-moving-body");expectedActorCount++;
                Assert.That(FlatDistance(body.transform.position,lease),Is.GreaterThanOrEqualTo(pairClearance),"Body starts outside the actual foreign service lease; the temporary waypoint grants no parking claim");
                var bodyPose=body.transform.position;CafeLoopRuntimeProof.Set(owner,"stepping",false);
                yield return null;FrozenFacts();Assert.That(Time.deltaTime,Is.Zero);
                var worldFacts=CafeLoopRuntimeProof.Field<TrafficWorldSnapshot>(core,"snapshot");
                var bodyFact=worldFacts.Bodies.Single(item=>ReferenceEquals(item.ActorLife,body));bodyKey=bodyFact.Owner;
                Assert.That(bodyKey.ActorId,Is.EqualTo(body.ActorId));Assert.That(bodyFact.Position,Is.EqualTo(bodyPose));Assert.That(bodyFact.Enabled && bodyFact.ActorActive,Is.True);
                Assert.That(bodyFact.IndependentlyProgressing,Is.False);Assert.That(bodyFact.RemainingSeconds,Is.Zero);
                Assert.That(entries.Contains(bodyKey),Is.False,"Only the actual unowned snapshot body key may receive this public movement request");
                Assert.That(worldFacts.Parking.All(claim=>FlatDistance(bodyPose,claim.Position)>=body.Settings.AgentRadius+claim.Radius+
                    Mathf.Max(body.Settings.CollisionSkin+body.Settings.Epsilon,claim.SafetyMargin)),Is.True,"NOT_PROVEN: real body start intersects a foreign canonical claim");
                var destination=new Vector3(0,0,-2.5f); // One fixed floor destination, never scan offsets or choose a passing sample.
                // 固定离开目标；原Core全部body/claim/currentMOVE检查决定是否真的独立。
                Assert.That(adapter.IsPointWalkable(destination),Is.True,"NOT_PROVEN: fixed independent destination is not on actual owned NavMesh");
                Assert.That(CustomerRoutePlanner.IsClear((Vector3[])MovementRead(priorCoreLeg,"Suffix"),new[]{destination},pairClearance),Is.True,
                    "NOT_PROVEN: independent destination must not park on any original unissued delivery/home suffix");
                var request=new TrafficRequest(bodyKey,Time.frameCount,worldFacts,bodyPose,destination,body.Settings.AgentRadius,"body",
                    finiteSeconds:body.Settings.MinimumSegmentSeconds+body.Settings.NoProgressSeconds,
                    safetyMargin:body.Settings.CollisionSkin+body.Settings.Epsilon,planPrimaryRoute:true);
                core.EvaluateMovement(request);bodyEntry=entries[bodyKey];var bodyFirst=MovementRead(bodyEntry,"FirstFrame");var bodyQueries=0;
                while(core.GetDecision(bodyKey).Kind!=TrafficDecisionKind.Granted)
                {
                    FrozenFacts();Assert.That(body.transform.position,Is.EqualTo(bodyPose));Assert.That(Time.deltaTime,Is.Zero);Assert.That(PreparationRemaining(),Is.GreaterThan(0));
                    var decision=core.GetDecision(bodyKey);Assert.That(decision.Kind,Is.EqualTo(TrafficDecisionKind.DeferredByBudget),
                        "NOT_PROVEN: fixed external source is not independently preparable: "+decision.Kind+"/"+decision.Reason);
                    Assert.That(entries[bodyKey],Is.SameAs(bodyEntry));var q=(int)MovementRead(bodyEntry,"Queries");Assert.That(q,Is.GreaterThanOrEqualTo(bodyQueries));bodyQueries=q;
                    yield return null;
                }
                bodySource=core.GetDecision(bodyKey);var bodyProof=bodySource.ProofRoutes.Single(item=>item.ResourceId=="body");
                TestContext.WriteLine("SINGLE_SIDE_REAL_BODY_PROOF key="+bodyKey+" first="+bodyFirst+" queries="+MovementRead(bodyEntry,"Queries")+
                    " waypoints="+bodyProof.Waypoints.Count+" start="+bodyPose.ToString("F9")+" goal="+destination.ToString("F9")+
                    " expanded=["+string.Join(";",bodyProof.ExpandedPath.Select(point=>point.ToString("F9")))+"]");
                Assert.That(bodyProof.Waypoints.Count,Is.EqualTo(1),"NOT_PROVEN: this original single-MoveTo contributor requires one real Core leg; no handmade next-leg driver");
                Assert.That(bodyProof.Waypoints.Single(),Is.EqualTo(destination));Assert.That(core.TryCommit(bodySource),Is.True);
                core.PrepareLeg(bodySource,"body",0,body,1,CafeLoopRuntimeProof.Field<TrafficWorldSnapshot>(core,"snapshot"),bodyPose,
                    body.Settings.ArrivalDistance+body.Settings.Epsilon,Time.frameCount);
                while(core.GetDecision(bodyKey).Kind!=TrafficDecisionKind.Granted)
                {
                    FrozenFacts();Assert.That(Time.deltaTime,Is.Zero);Assert.That(body.transform.position,Is.EqualTo(bodyPose));Assert.That(PreparationRemaining(),Is.GreaterThan(0));
                    var decision=core.GetDecision(bodyKey);Assert.That(decision.Kind,Is.EqualTo(TrafficDecisionKind.DeferredByBudget),"NOT_PROVEN: original external strict leg: "+decision.Kind+"/"+decision.Reason);
                    Assert.That(entries[bodyKey],Is.SameAs(bodyEntry));Assert.That(MovementRead(bodyEntry,"FirstFrame"),Is.EqualTo(bodyFirst));
                    var q=(int)MovementRead(bodyEntry,"Queries");Assert.That(q,Is.GreaterThanOrEqualTo(bodyQueries));bodyQueries=q;yield return null;
                }
                FrozenFacts();Assert.That(core.TryCommit(core.GetDecision(bodyKey)),Is.True);Assert.That(core.CanIssueLeg(bodyKey,body,1),Is.True);
                var moving=world.Service.MoveTo(body.ActorId,new NavigationTarget(destination),result=>
                {
                    if(!readingBody)return;completed=result;
                    if(result.ActorId==body.ActorId && result.RequestId==bodyStamp.RequestId && result.Status==MovementStatus.Arrived)
                        bodyCoreCompleted=core.CompleteLeg(bodyKey,body,bodyStamp,result.FinalPosition);
                });
                Assert.That(moving.Accepted,Is.True);Assert.That(completed.HasValue,Is.False);
                bodyStamp=new TrafficNativeStamp(moving.RequestId,1);Assert.That(core.BindLeg(bodyKey,body,bodyStamp),Is.True);
                Assert.That(core.TryGetCommittedJourney(bodyKey,out var actualSource),Is.True);Assert.That(actualSource,Is.SameAs(bodySource));
                Assert.That(body.Agent.hasPath,Is.True);Assert.That(body.Agent.isStopped,Is.False);
                Assert.That(natives.Count,Is.EqualTo(originalNative.Count+1));foreach(var old in originalNative)Assert.That(natives[old.Key],Is.SameAs(old.Value));
                TestContext.WriteLine("P14_OUTBOUND_TRAFFIC typedBody=True source="+bodyKey+" acq="+bodySource.AcquiredSequence+" request="+moving.RequestId+
                    " handle="+handle+" index="+index+" target="+target.Position.ToString("F6")+" lease="+lease.ToString("F6")+" body="+bodyPose.ToString("F6")+" destination="+destination.ToString("F6"));
            }
            finally {CafeLoopRuntimeProof.Set(owner,"stepping",savedStepping);Time.captureDeltaTime=savedCapture;Time.timeScale=savedScale;}
            var actualExitCount=0;var observingExit=true;var exit=CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position;
            var customerActor=world.RegisteredActors.Single(actor=>actor.ActorId==visit);
            var exitObserver=customerActor.gameObject.AddComponent<CafeActualExitObserver>();exitObserver.VisitId=visit;
            exitObserver.Receipt=(id,position)=>{if(!observingExit)return;Assert.That(id,Is.EqualTo(visit));
                Assert.That(Vector3.Distance(position,exit),Is.LessThanOrEqualTo(.081f));actualExitCount++;};
            var waited=false;object frozen=null,resources=null;Dictionary<TrafficIntentKey,string[]> resourceFacts=null;var originalWaitRemaining=debtBefore;
            var waitRemaining=originalWaitRemaining>0?originalWaitRemaining:Convert.ToSingle(finite);
            try
            {
                for(var frame=0;frame<600 && !completed.HasValue;frame++)
                {
                    Assert.That(PreparationRemaining(),Is.GreaterThan(0));Assert.That(runner.Snapshot.State,Is.Not.EqualTo(EmployeeState.Blocked),"Transient traffic preserves the selected Prepared attempt");
                    var pending=MovementRead(runner,"moveLeg");
                    if(CafeLoopRuntimeProof.Field<int>(runner,"deliveryRouteIndex")==index+1 && pending!=null && (bool)MovementRead(pending,"AwaitingTraffic") &&
                        core.GetDecision(originalIntent).Kind==TrafficDecisionKind.WaitingForTraffic)
                    {
                        SameWork();Assert.That(MovementRead(pending,"RequestId"),Is.EqualTo(0L));
                        var decision=core.GetDecision(originalIntent);Assert.That(decision.Kind,Is.EqualTo(TrafficDecisionKind.WaitingForTraffic));
                        Assert.That(decision.Blockers.Count,Is.GreaterThan(0));Assert.That(decision.Blockers.All(block=>block.Owner.Equals(bodyKey) && block.ResourceId=="body"),Is.True);
                        var nextLeg=MovementRead(originalEntry,"Leg");Assert.That(nextLeg,Is.Not.Null);Assert.That(nextLeg,Is.Not.SameAs(priorCoreLeg));
                        Assert.That(MovementRead(nextLeg,"ActorLife"),Is.SameAs(barista));Assert.That(MovementRead(nextLeg,"Proof"),Is.SameAs(MovementRead(priorCoreLeg,"Proof")));
                        Assert.That(MovementRead(nextLeg,"Generation"),Is.EqualTo(MovementRead(pending,"NativeLegGeneration")));
                        Assert.That(MovementRead(priorCoreLeg,"Completed"),Is.EqualTo(true));Assert.That(MovementRead(nextLeg,"Committed"),Is.EqualTo(false));Assert.That(MovementRead(nextLeg,"Bound"),Is.EqualTo(false));
                        var currentFrozen=MovementRead(originalEntry,"FrozenOwners");var currentResources=MovementRead(originalEntry,"FrozenResources");
                        Assert.That(currentFrozen,Is.Not.Null);Assert.That(currentResources,Is.Not.Null);
                        if(!waited){frozen=currentFrozen;resources=currentResources;var refs=(IDictionary)resources;resourceFacts=refs.Keys.Cast<TrafficIntentKey>().ToDictionary(key=>key,key=>((IEnumerable<string>)refs[key]).ToArray());}
                        else {Assert.That(currentFrozen,Is.SameAs(frozen));Assert.That(currentResources,Is.SameAs(resources));}
                        CollectionAssert.AreEquivalent(new[]{bodyKey},((IEnumerable)currentFrozen).Cast<TrafficIntentKey>());
                        var liveResources=(IDictionary)currentResources;CollectionAssert.AreEquivalent(resourceFacts.Keys,liveResources.Keys.Cast<TrafficIntentKey>());
                        foreach(var resource in resourceFacts)CollectionAssert.AreEquivalent(resource.Value,(IEnumerable<string>)liveResources[resource.Key]);
                        Assert.That(resourceFacts[bodyKey],Does.Contain("body"));Assert.That(MovementRead(bodyEntry,"JourneyPermit"),Is.SameAs(bodySource));
                        var movingLeg=MovementRead(bodyEntry,"Leg");Assert.That(movingLeg,Is.Not.Null);Assert.That(MovementRead(movingLeg,"ActorLife"),Is.SameAs(body));Assert.That(MovementRead(movingLeg,"Bound"),Is.EqualTo(true));
                        Assert.That(MovementRead(movingLeg,"Committed"),Is.EqualTo(true));Assert.That(MovementRead(movingLeg,"Completed"),Is.EqualTo(false));
                        Assert.That(MovementRead(movingLeg,"Stamp"),Is.EqualTo(bodyStamp));Assert.That(natives.Contains(body.ActorId),Is.True);
                        Assert.That(MovementRead(natives[body.ActorId],"Id"),Is.EqualTo(bodyStamp.RequestId));Assert.That(MovementRead(natives[body.ActorId],"Generation"),Is.EqualTo(1));
                        var remaining=(float)MovementRead(originalEntry,"WaitRemaining");Assert.That(remaining,Is.GreaterThan(0).And.LessThanOrEqualTo(waitRemaining));waitRemaining=remaining;waited=true;
                    }
                    yield return null;
                }
                Assert.That(waited,Is.True,"Actual prior Native arrival must produce a same-source next-leg finite WAIT");
                Assert.That(completed.HasValue,Is.True);Assert.That(completed.Value.Status,Is.EqualTo(MovementStatus.Arrived));
                Assert.That(bodyCoreCompleted,Is.True,"Only the exact real Arrived callback may complete the contributor's Core leg");
                Assert.That(completed.Value.RequestId,Is.EqualTo(bodyStamp.RequestId));Assert.That(completed.Value.RetryCount,Is.Zero);
                core.Retire(bodyKey);world.Unregister(body);body.gameObject.SetActive(false);UnityEngine.Object.Destroy(body.gameObject);
                world.Unregister(shaping);shaping.gameObject.SetActive(false);UnityEngine.Object.Destroy(shaping.gameObject);
                tools.ResumeCollection();
                yield return Until(()=>{Assert.That(PreparationRemaining(),Is.GreaterThan(0));return owner.Snapshot.Orders.Values.Single().State==OrderState.Completed &&
                    customers.Capacity.GetCapacities().All(item=>item.Used==0) && employees.Snapshot.All(item=>item.State==EmployeeState.Idle) && actualExitCount==1;});
                Assert.That(owner.Snapshot.PaidVisitIds.Count,Is.EqualTo(1));Assert.That(owner.Snapshot.Orders.Values.Single().CustomerId,Is.EqualTo(visit));
                Assert.That(actualExitCount,Is.EqualTo(1));
                Assert.That(Vector3.Distance(barista.transform.position,initialBinding.WorkTarget.Position),Is.LessThanOrEqualTo(barista.Settings.ArrivalDistance+barista.Settings.Epsilon));
                CollectionAssert.AreEquivalent(originalTokens,customers.Capacity.GetReservations().Where(item=>item.OwnerId==visit).Select(item=>item.Token));
                Assert.That(customers.Capacity.GetReservations().Where(item=>item.OwnerId==visit).All(item=>item.State==ReservationState.Released),Is.True);
            }
            finally {readingBody=false;observingExit=false;}
        }
    }
}
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.CafeLoop;
using AnimalCafe.Capacity;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode.Phase14
{
    public sealed class CafeCustomerTestDriver : MonoBehaviour
    {
        public CustomerFlowController Flow;
        private void Update() { if (Flow != null) Flow.Step(Time.deltaTime); }
    }

    public class CafeCustomerFlowTests
    {
        private GameObject root;
        private NavigationWorld world;
        private NavigationLayoutAdapter adapter;
        private CustomerFlowController flow;
        private Transform exit;
        private float savedScale, savedCapture;

        [SetUp] public void Setup()
        {
            savedScale = Time.timeScale; savedCapture = Time.captureDeltaTime; Time.captureDeltaTime = 1f / 60;
            root = new GameObject("P14 customer fixture");
            world = root.AddComponent<NavigationWorld>();
            adapter = root.AddComponent<NavigationLayoutAdapter>();
            var time = root.AddComponent<GameTimeService>();
            var floorGo = new GameObject("floor"); floorGo.transform.SetParent(root.transform);
            floorGo.transform.position = Vector3.down * .1f;
            var floor = floorGo.AddComponent<BoxCollider>(); floor.size = new Vector3(18, .2f, 18);
            adapter.Configure(null, null, null, floor, new Collider[0], world, false);
            Assert.That(adapter.RebuildAndValidate().CanResume, Is.True);
            var spawn = Marker("spawn", new Vector3(-4, 0, 4));
            exit = Marker("exit", new Vector3(-4, 0, -4));
            var head = Marker("head", Vector3.zero);
            var prefabs = new[] { "Shiba", "Westie" }.Select(species => AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Phase11/Characters/" + species + "/PF_" + species + "_Navigation.prefab")
                .GetComponent<NavigationActor>()).ToArray();
            flow = root.AddComponent<CustomerFlowController>();
            flow.Configure(world, adapter, time, prefabs, spawn, exit, head); flow.AutoSpawn = false;
            flow.Step(0);
            flow.ConfigurePickupGeometry("pickup", new Vector3(4, 0, 0), new Vector3(4, 0, 0), Vector3.forward);
            flow.SetIntegratedMode(true);
            root.AddComponent<CafeCustomerTestDriver>().Flow = flow;
        }

        private Transform Marker(string name, Vector3 point)
        { var go = new GameObject(name); go.transform.SetParent(root.transform); go.transform.position = point; return go.transform; }
        [TearDown] public void Cleanup()
        { if (root != null) Object.DestroyImmediate(root); Time.captureDeltaTime = savedCapture; Time.timeScale = savedScale; }

        private IEnumerator SpawnQueued()
        {
            Assert.That(flow.TrySpawn(), Is.True, flow.Status);
            yield return Until(() => flow.Snapshot.All(v => v.State == CustomerVisitState.Queued));
        }
        private CafeVisitSnapshot Visit(string id) => flow.CafeSnapshot.Single(v => v.VisitId == id);
        private IEnumerator Until(Func<bool> done)
        {
            for (var frame = 0; frame < 2400 && !done(); frame++)
            {
                var actors = world.RegisteredActors.ToArray(); var before = actors.Select(a => a.transform.position).ToArray();
                yield return null;
                for (var i = 0; i < actors.Length; i++)
                {
                    if (actors[i] == null) continue;
                    var delta = actors[i].transform.position - before[i];
                    Assert.That(delta.magnitude, Is.LessThanOrEqualTo(actors[i].Settings.MaxSpeed * Time.deltaTime + .003f));
                    Assert.That(adapter.IsPointWalkable(actors[i].transform.position), Is.True);
                    for (var j = 0; j < i; j++)
                    {
                        if (actors[j] == null) continue;
                        var relative = before[i] - before[j];
                        var change = delta - (actors[j].transform.position - before[j]);
                        var t = change.sqrMagnitude < 1e-12f ? 0 : Mathf.Clamp01(-Vector3.Dot(relative, change) / change.sqrMagnitude);
                        Assert.That((relative + change * t).magnitude, Is.GreaterThanOrEqualTo(.899f), "Continuous real body separation");
                    }
                }
            }
            Assert.That(done(), Is.True, flow.Status);
        }

        [UnityTest] public IEnumerator RealCounterPickupServiceExitUsesOriginalTokensAndActualMilestones()
        {
            yield return SpawnQueued(); var id = flow.Snapshot.Single().VisitId;
            var tokens = flow.Capacity.GetReservations().Select(r => r.Token).ToArray();
            Assert.That(flow.TryLetFrontLeave(), Is.False, "P14 owns business departure");
            Assert.That(flow.TryReservePickup(id, out var reason), Is.True, reason);
            Assert.That(flow.TryMoveToPickup(id, out reason), Is.True, reason);
            Assert.That(flow.Capacity.GetCapacities()[1].Occupied, Is.EqualTo(1));
            Assert.That(flow.Capacity.GetCapacities()[2].Reserved, Is.EqualTo(1));
            yield return Until(() => Visit(id).PickupArrived);
            Assert.That(flow.Capacity.GetCapacities()[1].Used, Is.Zero);
            Assert.That(flow.Capacity.GetCapacities()[2].Occupied, Is.EqualTo(1));
            Assert.That(flow.CanCollectPickup(id), Is.False, "Wait arrival is not service arrival");
            var wait = flow.PickupAssignments.Single();
            Assert.That(flow.TryAcquirePickupDelivery("barista"), Is.True);
            Assert.That(flow.TryMoveToPickupService(id, out _), Is.False);
            Assert.That(Visit(id).NavigationState, Is.EqualTo(CustomerVisitState.PickupWaiting));
            flow.ReleasePickupServicePosition("barista");
            Assert.That(flow.TryMoveToPickupService(id, out reason), Is.True, reason);
            var service = flow.PickupAssignments.Single();
            Assert.That(service.Generation, Is.GreaterThan(wait.Generation));
            Assert.That(service.Arrived, Is.False);
            Assert.That(flow.CanCollectPickup(id), Is.False);
            yield return Until(() => flow.CanCollectPickup(id));
            Assert.That(Vector3.Distance(Visit(id).Position, new Vector3(4, 0, 0)), Is.LessThanOrEqualTo(.081f));
            Assert.That(flow.TryMoveToExit(id, true, out reason), Is.True, reason);
            Assert.That(flow.Capacity.GetCapacities()[2].Used, Is.EqualTo(1));
            Assert.That(flow.Capacity.GetCapacities()[0].Occupied, Is.EqualTo(1));
            yield return Until(() => Visit(id).PickupDeparted);
            Assert.That(flow.Capacity.GetCapacities()[2].Used, Is.Zero);
            Assert.That(flow.Capacity.GetCapacities()[0].Occupied, Is.EqualTo(1));
            yield return Until(() => Visit(id).ExitArrived);
            Assert.That(flow.Snapshot, Is.Empty);
            Assert.That(world.RegisteredActors, Is.Empty);
            Assert.That(flow.Capacity.GetCapacities().All(c => c.Used == 0), Is.True);
            CollectionAssert.AreEqual(tokens, flow.Capacity.GetReservations().Select(r => r.Token));
            flow.Step(0); Assert.That(Visit(id).ExitArrived, Is.True, "Fact persists until explicit consumption");
            flow.AcknowledgeExitSnapshots(); Assert.That(flow.CafeSnapshot, Is.Empty);
        }

        [UnityTest] public IEnumerator NoPickupSlotKeepsCustomerAtCounter()
        {
            yield return SpawnQueued(); var id = flow.Snapshot.Single().VisitId; var before = flow.Snapshot.Single().Position;
            flow.ConfigurePickupGeometry("unreachable", Vector3.one * 1000, Vector3.one * 1000, Vector3.forward);
            Assert.That(flow.TryReservePickup(id, out var reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(flow.TryMoveToPickup(id, out _), Is.False);
            yield return null;
            Assert.That(flow.Snapshot.Single().Position, Is.EqualTo(before));
            Assert.That(flow.Capacity.GetCapacities()[1].Occupied, Is.EqualTo(1));
            Assert.That(flow.Capacity.GetCapacities()[2].Reserved, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator PaidFifoMovesRealFollowersAndRejectsNonHeadCollection()
        {
            yield return SpawnQueued(); yield return SpawnQueued(); yield return SpawnQueued();
            var ids = flow.Snapshot.Select(v => v.VisitId).ToArray();
            Assert.That(flow.TryReservePickup(ids[1],out _),Is.False,"An actually arrived Counter follower cannot reserve or be charged ahead of the head");
            Assert.That(flow.TryMoveToPickup(ids[1],out _),Is.False);
            foreach (var id in ids)
            {
                yield return Until(() => flow.Snapshot.Single(v => v.VisitId == id).State == CustomerVisitState.Queued);
                Assert.That(flow.TryReservePickup(id, out var reason), Is.True, reason);
                Assert.That(flow.TryMoveToPickup(id, out reason), Is.True, reason);
                yield return Until(() => Visit(id).PickupArrived);
            }
            var old = flow.PickupAssignments.ToArray();
            Assert.That(flow.TryMoveToPickupService(ids[1], out _), Is.False);
            Assert.That(flow.TryMoveToPickupService("wrong-visit", out _), Is.False);
            Assert.That(flow.TryMoveToPickupService(ids[0], out var failure), Is.True, failure);
            yield return Until(() => flow.CanCollectPickup(ids[0]));
            Assert.That(flow.TryMoveToExit(ids[0], true, out failure), Is.True, failure);
            yield return Until(() => Visit(ids[0]).PickupDeparted &&
                flow.PickupAssignments.Count == 2 && flow.PickupAssignments.All(a => a.Arrived));
            CollectionAssert.AreEqual(ids.Skip(1), flow.PickupAssignments.Select(a => a.VisitId));
            Assert.That(flow.PickupAssignments[0].SlotIndex, Is.EqualTo(0));
            Assert.That(flow.PickupAssignments[1].SlotIndex, Is.EqualTo(1));
            Assert.That(flow.PickupAssignments[0].Generation, Is.GreaterThan(old[1].Generation));
            Assert.That(Vector3.Distance(Visit(ids[1]).Position, old[0].Position), Is.LessThanOrEqualTo(.081f));
            Assert.That(flow.CanCollectPickup(ids[1]), Is.False);
        }

        [UnityTest] public IEnumerator PickupBendFacesPreviousCustomerAndUpdatesAfterActualAdvance()
            => PickupBendFacing(0);

        [UnityTest] public IEnumerator RotatedPickupBendFacesPreviousCustomerAndUpdatesAfterActualAdvance()
            => PickupBendFacing(90);

        private IEnumerator PickupBendFacing(float yaw)
        {
            // 靠近真实floor边缘使等候链合法转弯；不注入slots/Route或移动Actor。
            // The original planner and native movement own the bend and every arrival.
            var rotation=Quaternion.Euler(0,yaw,0);
            var service=rotation*new Vector3(4,0,7.1f);
            var outward=rotation*Vector3.forward;
            flow.ConfigurePickupGeometry("bent-pickup",service,service,outward);
            yield return SpawnQueued(); yield return SpawnQueued(); yield return SpawnQueued();
            var ids=flow.Snapshot.Select(v=>v.VisitId).ToArray();
            foreach(var id in ids)
            {
                yield return Until(()=>Visit(id).NavigationState==CustomerVisitState.Queued);
                Assert.That(flow.TryReservePickup(id,out var reason),Is.True,reason);
                Assert.That(flow.TryMoveToPickup(id,out reason),Is.True,reason);
                yield return Until(()=>Visit(id).PickupArrived);
            }
            var original=flow.PickupAssignments.ToArray();
            Assert.That(Mathf.Abs(Vector3.Dot((original[1].Position-original[0].Position).normalized,outward)),
                Is.LessThan(.1f),"Real safe floor boundary must establish a bent waiting chain");
            AssertPickupFacing(outward);
            Assert.That(flow.TryMoveToPickupService(ids[0],out var failure),Is.True,failure);
            yield return Until(()=>flow.CanCollectPickup(ids[0]));
            Assert.That(flow.TryMoveToExit(ids[0],true,out failure),Is.True,failure);
            yield return Until(()=>Visit(ids[0]).PickupDeparted && flow.PickupAssignments.Count==2 &&
                flow.PickupAssignments.All(a=>a.Arrived));
            CollectionAssert.AreEqual(ids.Skip(1),flow.PickupAssignments.Select(a=>a.VisitId));
            Assert.That(flow.PickupAssignments[0].SlotIndex,Is.Zero);
            Assert.That(flow.PickupAssignments[1].SlotIndex,Is.EqualTo(1));
            for(var i=0;i<2;i++)
            {
                Assert.That(flow.PickupAssignments[i].Generation,Is.GreaterThan(original[i+1].Generation));
                Assert.That(Vector3.Distance(Visit(ids[i+1]).Position,original[i].Position),Is.LessThanOrEqualTo(.081f));
            }
            AssertPickupFacing(outward); // 新队首朝服务侧，后客前移后仍朝前一位。
        }

        private void AssertPickupFacing(Vector3 outward)
        {
            var assignments=flow.PickupAssignments.OrderBy(a=>a.SlotIndex).ToArray();
            for(var i=0;i<assignments.Length;i++)
            {
                var assignment=assignments[i];
                Assert.That(assignment.Arrived,Is.True);
                var actor=world.RegisteredActors.Single(a=>a.ActorId==assignment.VisitId);
                Assert.That(Visit(assignment.VisitId).NavigationState,Is.EqualTo(CustomerVisitState.PickupWaiting));
                var expected=i==0?-outward:assignments[i-1].Position-assignment.Position;
                expected.y=0;
                Assert.That(Vector3.Dot(actor.transform.forward,expected.normalized),Is.GreaterThan(.99f),
                    "Settled actual Pickup slot "+assignment.SlotIndex+" must face its predecessor; head faces service");
            }
        }

        [UnityTest] public IEnumerator ExistingPickupWaiterActuallyYieldsAndReturnsOnlyAfterActualBaristaReturn()
        { yield return ExistingWaiterDeliveryCorridor(false); }

        [UnityTest] public IEnumerator ExistingPickupWaiterWithoutSafeYieldKeepsOriginalSlotAndCapacity()
        { yield return ExistingWaiterDeliveryCorridor(true); }
        [UnityTest] public IEnumerator TwoStationaryWaitersYieldWithUnrelatedCounterQueuePresent()
        { yield return ExistingWaiterDeliveryCorridor(false,true); }
        [UnityTest] public IEnumerator TwoHeldWaitersRestoreAfterActualMovingTrafficVacatesOriginalSlots()
        { yield return ExistingWaiterDeliveryCorridor(false,true,true); }
        [UnityTest] public IEnumerator FuturePickupCrossingWaitsWhileRealRequestIsOutsideDeliveryCorridor()
        { yield return FuturePickupDeliveryCrossing(false); }
        [UnityTest] public IEnumerator CancelledFuturePickupCrossingDoesNotKeepDeliveryWaitingForever()
        { yield return FuturePickupDeliveryCrossing(true); }
        private IEnumerator FuturePickupDeliveryCrossing(bool cancel)
        {
            yield return SpawnQueued();var id=flow.Snapshot.Single().VisitId;
            var body=world.RegisteredActors.Single(a=>a.ActorId==id);
            var barista=AddTrafficActor("future-crossing-barista",new Vector3(4,0,5));
            Assert.That(flow.TryReservePickup(id,out var reason),Is.True,reason);Assert.That(flow.TryMoveToPickup(id,out reason),Is.True,reason);
            yield return null;
            var visit=PrivateVisit(id);var request=Read<long>(visit,"RequestId");
            Assert.That(request,Is.GreaterThan(0));Assert.That(body.Agent.pathStatus,Is.EqualTo(UnityEngine.AI.NavMeshPathStatus.PathComplete));
            Assert.That(body.Agent.isStopped,Is.False);Assert.That(body.Agent.hasPath || body.Agent.pathPending,Is.True);
            typeof(CustomerFlowController).GetMethod("ConfigurePickupDelivery",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(flow,new object[]{barista.ActorId,new NavigationTarget(barista.transform.position)});
            var points=(IReadOnlyList<Vector3>)typeof(CustomerFlowController).GetMethod("BuildRoute",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(flow,new object[]{barista.transform.position,new Vector3(4,0,0),barista.ActorId,barista.Settings,null,false});
            Assert.That(points,Is.Not.Null,"A full owned-safe delivery detour must exist before classifying future traffic");
            var direct=(IReadOnlyList<Vector3>)typeof(CustomerFlowController).GetMethod("ExpandRoute",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(flow,new object[]{barista.transform.position,points});
            Assert.That(CustomerRoutePlanner.IsClear(direct,new[]{body.transform.position},.911f),Is.True,"Actual body has not entered the delivery corridor yet");
            var future=(IReadOnlyList<Vector3>)typeof(CustomerFlowController).GetMethod("ExpandRoute",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(flow,new object[]{body.transform.position,new[]{body.Agent.destination}.Concat(Read<Queue<Vector3>>(visit,"Route")??Enumerable.Empty<Vector3>())});
            TestContext.WriteLine("FUTURE_CROSSING actual="+body.transform.position.ToString("F6")+" delivery="+string.Join(";",direct.Select(p=>p.ToString("F6")))+" ownedRemaining="+string.Join(";",future.Select(p=>p.ToString("F6"))));
            Assert.That((bool)typeof(CustomerFlowController).GetMethod("RoutesConflict",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{direct,future,.911f}),Is.True,"The real active pickup request will cross the complete delivery corridor");
            Assert.That(flow.TryAcquirePickupDelivery(barista.ActorId,out _,out _),Is.False);
            var failure=typeof(CustomerFlowController).GetProperty("PickupDeliveryFailure",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That((string)failure.GetValue(flow),Is.Empty,"Related future crossing is transient, not a permanent Prepared failure");
            if(cancel)
            {
                Assert.That(world.Service.Cancel(request),Is.True);yield return null;
                Assert.That(Read<long>(visit,"RequestId"),Is.Zero);
            }
            else yield return Until(()=>Visit(id).PickupArrived);
            yield return Until(()=>flow.TryAcquirePickupDelivery(barista.ActorId,out _,out _));
            Assert.That((string)failure.GetValue(flow),Is.Empty);
            Assert.That(flow.Capacity.GetCapacities()[0].Used,Is.EqualTo(1));Assert.That(flow.Capacity.GetCapacities()[2].Used,Is.EqualTo(1));
        }

        private IEnumerator ExistingWaiterDeliveryCorridor(bool noSpace,bool twoWaiters=false,bool restoreTraffic=false)
        {
            var floor=root.GetComponentsInChildren<BoxCollider>().Single(b=>b.name=="floor");
            var walls=new List<Collider>();
            foreach(var x in new[]{3f,5f})
            {
                var go=new GameObject("actual corridor wall");go.transform.SetParent(root.transform);
                go.transform.position=new Vector3(x,.5f,1.5f);var wall=go.AddComponent<BoxCollider>();wall.size=new Vector3(1,1,4);walls.Add(wall);
            }
            adapter.Configure(null,null,null,floor,walls.ToArray(),world,false);
            Assert.That(adapter.RebuildAndValidate().CanResume,Is.True);yield return null;
            var barista=AddTrafficActor("corridor-barista",new Vector3(4,0,5));
            yield return SpawnQueued();var id=flow.Snapshot.Single().VisitId;
            Assert.That(flow.TryReservePickup(id,out var reason),Is.True,reason);Assert.That(flow.TryMoveToPickup(id,out reason),Is.True,reason);
            yield return Until(()=>Visit(id).PickupArrived);
            var original=flow.PickupAssignments.Single();var body=world.RegisteredActors.Single(a=>a.ActorId==id);var visit=PrivateVisit(id);
            if(twoWaiters)
            {
                Assert.That(flow.TrySpawn(),Is.True,flow.Status);var second=flow.Snapshot.Single(v=>v.VisitId!=id).VisitId;
                yield return Until(()=>flow.Snapshot.Single(v=>v.VisitId==second).State==CustomerVisitState.Queued);
                Assert.That(flow.TryReservePickup(second,out reason),Is.True,reason);Assert.That(flow.TryMoveToPickup(second,out reason),Is.True,reason);
                yield return Until(()=>Visit(second).PickupArrived);
                // A real but unrelated Counter queue must not globally disable delivery recovery.
                Assert.That(flow.TrySpawn(),Is.True,flow.Status);var counter=flow.Snapshot.Single(v=>v.VisitId!=id && v.VisitId!=second).VisitId;
                yield return Until(()=>flow.Snapshot.Single(v=>v.VisitId==counter).State==CustomerVisitState.Queued);
                Assert.That(flow.PickupAssignments.Single(a=>a.VisitId==second).Position,Is.EqualTo(new Vector3(4,0,2.2f)),"Actual second slot must enter the second real alcove");
                var right=(BoxCollider)walls[1];right.transform.position=new Vector3(5,.5f,-4.2f);right.size=new Vector3(1,1,9.6f);
                foreach(var interval in new[]{new Vector2(1.6f,1.7f),new Vector2(2.7f,9f)})
                {
                    var go=new GameObject("right alcove separator");go.transform.SetParent(root.transform);
                    go.transform.position=new Vector3(5,.5f,(interval.x+interval.y)/2);
                    var wall=go.AddComponent<BoxCollider>();wall.size=new Vector3(1,1,interval.y-interval.x);walls.Add(wall);
                }
                Assert.That(flow.PickupAssignments.Count,Is.EqualTo(2));
            }
            // Close the end-around detour only after normal admission/arrival. No actor is repositioned.
            if(twoWaiters) ((BoxCollider)walls[0]).size=new Vector3(1,1,18);
            else foreach(var wall in walls.Cast<BoxCollider>()) wall.size=new Vector3(1,1,18);
            adapter.Configure(null,null,null,floor,walls.ToArray(),world,false);
            Assert.That(adapter.RebuildAndValidate().CanResume,Is.True);yield return null;
            var tokens=flow.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray();
            Assert.That(original.Arrived,Is.True);Assert.That(Read<long>(visit,"RequestId"),Is.Zero);
            Assert.That(Vector3.Distance(body.transform.position,original.Position),Is.LessThanOrEqualTo(.081f));
            var direct=adapter.GetCompletePath(barista.transform.position,new Vector3(4,0,0));
            Assert.That(direct,Is.Not.Null);Assert.That(CustomerRoutePlanner.IsClear(direct,new[]{body.transform.position},.911f),Is.False);
            if(noSpace) AddTrafficActor("no-yield-space",new Vector3(4,0,-1.4f));
            // Late production configuration models an already-owned queue; it does not inject a route/slot/state.
            typeof(CustomerFlowController).GetMethod("ConfigurePickupDelivery",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(flow,new object[]{barista.ActorId,new NavigationTarget(new Vector3(4,0,5))});
            var paidBefore=flow.PickupAssignments.ToArray();
            var allTokens=flow.Capacity.GetReservations().Select(r=>r.Token).ToArray();
            if(twoWaiters)
            {
                var unrelated=AddTrafficActor("unrelated-moving-traffic",new Vector3(-6,0,5));
                Assert.That(world.Service.MoveTo(unrelated.ActorId,new NavigationTarget(new Vector3(-6,0,-5)),_=>{}).Accepted,Is.True);
            }
            Assert.That(flow.TryAcquirePickupDelivery(barista.ActorId,out _,out _),Is.False,"Do not grant delivery through a genuinely arrived owned waiting body");
            if(noSpace)
            {
                for(var frame=0;frame<12;frame++){flow.TryAcquirePickupDelivery(barista.ActorId,out _,out _);yield return null;}
                var property=typeof(CustomerFlowController).GetProperty("PickupDeliveryFailure",BindingFlags.Instance|BindingFlags.NonPublic);
                Assert.That(property,Is.Not.Null,"Production delivery gate exposes its bounded failure to the coordinator");
                Assert.That((string)property.GetValue(flow),Does.Contain("送杯通道堵塞"));
                Assert.That(body.transform.position,Is.EqualTo(original.Position));Assert.That(flow.PickupAssignments.Single().SlotIndex,Is.EqualTo(original.SlotIndex));
                Assert.That(flow.Capacity.GetCapacities()[0].Used,Is.EqualTo(1));
                Assert.That(flow.Capacity.GetCapacities()[2].Used,Is.EqualTo(1));yield break;
            }
            var queue=Read<PickupQueueService>(flow,"pickupQueue");
            Assert.That(queue.TryReserve("new-tail-cannot-steal-original-slot",out _),Is.False);
            var gameTime=root.GetComponent<GameTimeService>();gameTime.SetPaused();var before=body.transform.position;
            var generation=Read<int>(visit,"Generation");for(var frame=0;frame<8;frame++)yield return null;
            Assert.That(body.transform.position,Is.EqualTo(before));Assert.That(Read<int>(visit,"Generation"),Is.EqualTo(generation));gameTime.SetNormal();
            NavigationTarget target=default;IReadOnlyList<NavigationTarget> route=null;
            yield return Until(()=>flow.TryAcquirePickupDelivery(barista.ActorId,out target,out route));
            Assert.That(Vector3.Distance(body.transform.position,original.Position),Is.GreaterThan(.9f),"Owner must actually yield, not only change reservation metadata");
            foreach(var beforeWait in paidBefore)
            {
                var actual=world.RegisteredActors.Single(a=>a.ActorId==beforeWait.VisitId);
                Assert.That(Vector3.Distance(actual.transform.position,beforeWait.Position),Is.GreaterThan(.9f),"Every claimed yielding participant must really leave its original slot");
                TestContext.WriteLine("ACTUAL_YIELD "+beforeWait.VisitId+" original="+beforeWait.Position.ToString("F6")+" actual="+actual.transform.position.ToString("F6"));
            }
            CollectionAssert.AreEqual(paidBefore.Select(a=>a.VisitId),flow.PickupAssignments.Select(a=>a.VisitId));
            var yielded=body.transform.position;Assert.That(flow.PickupAssignments.Single(a=>a.VisitId==id).SlotIndex,Is.EqualTo(original.SlotIndex));
            foreach(var leg in route)yield return Travel(barista,leg);
            Assert.That(Vector3.Distance(barista.transform.position,target.Position),Is.LessThanOrEqualTo(.081f));
            for(var frame=0;frame<8;frame++)yield return null;
            Assert.That(body.transform.position,Is.EqualTo(yielded),"Do not restore waiter when delivery arrives; keep the return corridor open");
            if(restoreTraffic)
            {
                var homeDone=false;MovementResult homeResult=default;
                Assert.That(world.Service.MoveTo(barista.ActorId,new NavigationTarget(new Vector3(4,0,5)),r=>{homeResult=r;homeDone=true;}).Accepted,Is.True);
                yield return Until(()=>barista.transform.position.z>4.9f);
                var moving=AddTrafficActor("temporary-original-slot-traffic",new Vector3(4,0,1.65f));
                Assert.That(world.Service.MoveTo(moving.ActorId,new NavigationTarget(new Vector3(4,0,-3)),_=>{}).Accepted,Is.True);
                yield return Until(()=>homeDone);Assert.That(homeResult.Status,Is.EqualTo(MovementStatus.Arrived));
                foreach(var waiting in paidBefore)
                {
                    var held=PrivateVisit(waiting.VisitId);Assert.That(Read<int>(held,"DeliveryYieldPhase"),Is.EqualTo(2));
                    Assert.That(Read<long>(held,"RequestId"),Is.Zero);
                    Assert.That((bool)typeof(CustomerFlowController).GetMethod("CanBeginPickupAdvance",BindingFlags.Instance|BindingFlags.NonPublic)
                        .Invoke(flow,new object[]{waiting}),Is.False,"Both original return slots must actually be obstructed in this CM-return frame");
                }
                TestContext.WriteLine("RESTORE_TRAFFIC actual="+moving.transform.position.ToString("F6")+" destination="+moving.Agent.destination.ToString("F6"));
            }
            else yield return Travel(barista,new NavigationTarget(new Vector3(4,0,5)));
            yield return Until(()=>flow.PickupAssignments.All(a=>a.Arrived) && Vector3.Distance(body.transform.position,original.Position)<=.081f);
            Assert.That(flow.PickupAssignments.Single(a=>a.VisitId==id).Generation,Is.GreaterThan(original.Generation));
            foreach(var beforeWait in paidBefore)
            {
                var actual=world.RegisteredActors.Single(a=>a.ActorId==beforeWait.VisitId);
                Assert.That(Vector3.Distance(actual.transform.position,beforeWait.Position),Is.LessThanOrEqualTo(.081f));
                Assert.That(flow.PickupAssignments.Single(a=>a.VisitId==beforeWait.VisitId).SlotIndex,Is.EqualTo(beforeWait.SlotIndex));
            }
            CollectionAssert.AreEqual(paidBefore.Select(a=>a.VisitId),flow.PickupAssignments.Select(a=>a.VisitId));
            CollectionAssert.AreEqual(allTokens,flow.Capacity.GetReservations().Select(r=>r.Token));
            CollectionAssert.AreEqual(tokens,flow.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
            Assert.That(flow.Capacity.GetCapacities()[2].Occupied,Is.EqualTo(twoWaiters?2:1));
        }

        private IEnumerator Travel(NavigationActor actor,NavigationTarget target)
        {
            var done=false;MovementResult receipt=default;
            var move=world.Service.MoveTo(actor.ActorId,target,result=>{receipt=result;done=true;});Assert.That(move.Accepted,Is.True);
            yield return Until(()=>done);Assert.That(receipt.Status,Is.EqualTo(MovementStatus.Arrived),receipt.Reason.ToString());
        }

        [UnityTest] public IEnumerator HeldMovementKeepsActualBodyAndCapacityThenResumes()
        {
            yield return SpawnQueued(); var id = flow.Snapshot.Single().VisitId;
            Assert.That(flow.TryReservePickup(id, out _), Is.True);
            Assert.That(flow.TryMoveToPickup(id, out _), Is.True); yield return null;
            flow.HoldIntegratedVisits(); var before = Visit(id).Position;
            var driver = root.GetComponent<CafeCustomerTestDriver>(); driver.enabled = false;
            for (var frame = 0; frame < 8; frame++) { flow.Step(Time.deltaTime); yield return null; }
            Assert.That(Visit(id).Position, Is.EqualTo(before));
            Assert.That(flow.Capacity.GetCapacities()[0].Occupied, Is.EqualTo(1));
            Assert.That(flow.TryMoveToPickupService(id, out _), Is.False);
            flow.ResumeBlockedVisits(); driver.enabled = true;
            yield return Until(() => Visit(id).PickupArrived);
        }

        [UnityTest] public IEnumerator CollectedBlockedExitSurvivesDecorationClearWithoutOrderState()
        {
            yield return SpawnQueued(); var id = flow.Snapshot.Single().VisitId;
            Assert.That(flow.TryReservePickup(id, out _), Is.True);
            Assert.That(flow.TryMoveToPickup(id, out _), Is.True);
            yield return Until(() => Visit(id).PickupArrived);
            Assert.That(flow.TryMoveToPickupService(id, out _), Is.True);
            yield return Until(() => flow.CanCollectPickup(id));
            var actor = world.RegisteredActors.Single();
            exit.position = Vector3.one * 1000;
            Assert.That(flow.TryMoveToExit(id, true, out _), Is.True);
            yield return Until(() => Visit(id).NavigationState == CustomerVisitState.Blocked);
            var before = actor.transform.position; var capacity = flow.Capacity;
            flow.ClearUncollectedVisits(new[] { id });
            flow.ClearUncollectedVisits(new[] { id });
            Assert.That(world.RegisteredActors.Single(), Is.SameAs(actor));
            Assert.That(actor.transform.position, Is.EqualTo(before));
            Assert.That(flow.Capacity, Is.SameAs(capacity));
            Assert.That(flow.Capacity.GetCapacities()[0].Occupied, Is.EqualTo(1));
            exit.position = new Vector3(-4, 0, -4); flow.ResumeBlockedVisits();
            yield return Until(() => Visit(id).ExitArrived);
            Assert.That(flow.Capacity.GetCapacities().All(c => c.Used == 0), Is.True);
        }

        [UnityTest] public IEnumerator CanonicalCollectedBeforeExitRequestSurvivesReentrantClear()
        {
            yield return SpawnQueued(); var id = flow.Snapshot.Single().VisitId;
            Assert.That(flow.TryReservePickup(id, out _), Is.True);
            Assert.That(flow.TryMoveToPickup(id, out _), Is.True);
            yield return Until(() => Visit(id).PickupArrived);
            Assert.That(flow.TryMoveToPickupService(id, out _), Is.True);
            yield return Until(() => flow.CanCollectPickup(id));
            var actor = world.RegisteredActors.Single(); var before = actor.transform.position;
            var capacity = flow.Capacity; var tokens = capacity.GetReservations().Select(r => r.Token).ToArray();
            // Service already committed Completed + canonical collected IDs, but an
            // external projection reentered Done before TryMoveToExit was called.
            flow.ClearUncollectedVisits(new[] { id });
            Assert.That(world.RegisteredActors.Count, Is.EqualTo(1));
            Assert.That(world.RegisteredActors.Single(), Is.SameAs(actor));
            Assert.That(actor.transform.position, Is.EqualTo(before));
            Assert.That(flow.Capacity, Is.SameAs(capacity));
            Assert.That(capacity.GetCapacities()[0].Occupied, Is.EqualTo(1));
            Assert.That(capacity.GetCapacities()[2].Occupied, Is.EqualTo(1));
            CollectionAssert.AreEqual(tokens, capacity.GetReservations().Select(r => r.Token));
            flow.ResumeBlockedVisits();
            yield return Until(() => Visit(id).ExitArrived);
            Assert.That(capacity.GetCapacities().All(c => c.Used == 0), Is.True);
        }

        [UnityTest] public IEnumerator FutureCounterQueueSlotsCannotBecomePickupWaitSlots()
        {
            yield return SpawnQueued(); var id = flow.Snapshot.Single().VisitId;
            // The old Counter planner already owns the forward row, including
            // currently unused slots at z=4.4/5.5. Do not sell that same space twice.
            flow.ConfigurePickupGeometry("overlap-counter", new Vector3(0, 0, 4.4f),
                new Vector3(0, 0, 4.4f), Vector3.forward);
            Assert.That(flow.TryReservePickup(id, out var reason), Is.False);
            Assert.That(reason, Is.Not.Empty);
            Assert.That(flow.Snapshot.Single().State, Is.EqualTo(CustomerVisitState.Queued));
            Assert.That(flow.Capacity.GetCapacities()[1].Occupied, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator PickupWaitsAtActualWaypointForMovingBodyWithoutSpendingReplans()
        { yield return PickupWaypointTraffic(false); }

        [UnityTest] public IEnumerator PickupDoesNotWaitForeverAfterBlockingBodyRequestIsCancelled()
        { yield return PickupWaypointTraffic(true); }

        private IEnumerator PickupWaypointTraffic(bool cancelBlocker)
        {
            yield return SpawnQueued();
            var id = flow.Snapshot.Single().VisitId;
            var customer = world.RegisteredActors.Single();
            // A real stationary body makes the initial route use a temporary waypoint.
            var obstacle = AddTrafficActor("route-obstacle", new Vector3(2, 0, .55f));
            Assert.That(flow.TryReservePickup(id, out var reason), Is.True, reason);
            Assert.That(flow.TryMoveToPickup(id, out reason), Is.True, reason);
            var visit = PrivateVisit(id);
            var firstRequest = Read<long>(visit, "RequestId");
            var first = customer.Agent.destination; first.y = 0;
            var remaining = Read<Queue<Vector3>>(visit, "Route").ToArray();
            Debug.Log("P14_WAYPOINT_PLAN issued=" + first.ToString("F6") + " remaining=" +
                string.Join(";", remaining.Select(p => p.ToString("F6"))));
            // Let short native segments finish normally until a safely separated next candidate exists.
            while (remaining.Length > 0 && Vector3.Distance(first, remaining[0]) <= 1.2f)
            {
                yield return Until(() => Read<long>(visit, "RequestId") != firstRequest);
                firstRequest = Read<long>(visit, "RequestId");
                first = customer.Agent.destination; first.y = 0;
                remaining = Read<Queue<Vector3>>(visit, "Route").ToArray();
            }
            Assert.That(remaining.Length, Is.GreaterThan(0), "The real planner must have issued a non-final segment");
            var next = remaining[0];
            Assert.That(Vector3.Distance(first, next), Is.GreaterThan(1.2f), "Safe initial body placement away from the approaching customer");
            yield return Until(() => Vector3.Distance(customer.transform.position, first) < .24f);
            Assert.That(Read<long>(visit, "RequestId"), Is.EqualTo(firstRequest));
            // A new actor starts at a legal pose; no running Actor is repositioned.
            var traffic = AddTrafficActor("departing-traffic", next);
            var along = (next - first).normalized;
            var target = next + new Vector3(-along.z, 0, along.x) * 3;
            MovementResult trafficResult = default;
            var trafficFinished = false;
            var move = world.Service.MoveTo(traffic.ActorId, new NavigationTarget(target), result =>
            { trafficResult = result; trafficFinished = true; });
            Assert.That(move.Accepted, Is.True);
            Assert.That(traffic.Agent.hasPath && !traffic.Agent.isStopped, Is.True);
            var segmentBudget = Read<int>(visit, "Segments");
            var replans = Read<int>(visit, "Replans");
            yield return Until(() => Read<long>(visit, "RequestId") != firstRequest);
            Debug.Log("P14_WAYPOINT actual=" + customer.transform.position.ToString("F6") +
                " next=" + next.ToString("F6") + " traffic=" + traffic.transform.position.ToString("F6") +
                " state=" + Visit(id).NavigationState + " failure=" + Read<string>(visit, "Failure") +
                " segments=" + Read<int>(visit, "Segments") + " replans=" + Read<int>(visit, "Replans"));
            Assert.That(Vector3.Distance(customer.transform.position, first), Is.LessThanOrEqualTo(.081f), "Actual first-waypoint arrival");
            Assert.That(CustomerRoutePlanner.IsClear(adapter.GetCompletePath(customer.transform.position, next),
                new[] { traffic.transform.position }, .911f), Is.False, "The real active body still blocks the unissued segment");
            Assert.That(Visit(id).NavigationState, Is.EqualTo(CustomerVisitState.MovingToPickup),
                "A still-moving body must cause ordinary waiting, not immediate Blocked");
            Assert.That(Read<long>(visit, "RequestId"), Is.Zero, "Do not issue an unsafe next request");
            Assert.That(Read<int>(visit, "Segments"), Is.EqualTo(segmentBudget));
            Assert.That(Read<int>(visit, "Replans"), Is.EqualTo(replans));
            CollectionAssert.AreEqual(remaining, Read<Queue<Vector3>>(visit, "Route"));
            var time = root.GetComponent<GameTimeService>();
            time.SetPaused(); var before = customer.transform.position; var trafficBefore = traffic.transform.position;
            for (var i = 0; i < 8; i++) { flow.Step(0); yield return null; }
            Assert.That(customer.transform.position, Is.EqualTo(before));
            Assert.That(traffic.transform.position, Is.EqualTo(trafficBefore));
            Assert.That(Read<int>(visit, "Replans"), Is.EqualTo(replans));
            if (cancelBlocker)
            {
                Assert.That(world.Service.Cancel(move.RequestId), Is.True);
                Assert.That(trafficFinished, Is.True);
                Assert.That(trafficResult.Status, Is.EqualTo(MovementStatus.Cancelled));
            }
            time.SetNormal();
            if (cancelBlocker)
            {
                yield return Until(() => Visit(id).NavigationState == CustomerVisitState.Blocked);
                Assert.That(Read<bool>(visit, "PendingPickupSegment"), Is.False, "A cancelled request cannot keep ordinary pending traffic alive");
                Assert.That(Read<string>(visit, "Failure"), Does.Contain("通道被占用"));
                Assert.That(Read<int>(visit, "Replans"), Is.LessThanOrEqualTo(3));
                Assert.That(flow.Capacity.GetCapacities()[0].Occupied, Is.EqualTo(1));
                Assert.That(flow.Capacity.GetCapacities()[2].Reserved, Is.EqualTo(1));
            }
            else
            {
                yield return Until(() => Visit(id).PickupArrived || Visit(id).NavigationState == CustomerVisitState.Blocked);
                Assert.That(Visit(id).PickupArrived, Is.True, Read<string>(visit, "Failure"));
                yield return Until(() => trafficFinished);
                Assert.That(trafficResult.Status, Is.EqualTo(MovementStatus.Arrived));
                Assert.That(Vector3.Distance(traffic.transform.position, target), Is.LessThanOrEqualTo(.081f));
                Assert.That(flow.Capacity.GetCapacities()[1].Used, Is.Zero);
                Assert.That(flow.Capacity.GetCapacities()[2].Occupied, Is.EqualTo(1));
            }
        }

        [UnityTest] public IEnumerator AcceptedExitKeepsOwnedServiceLeaseAsFiniteTrafficUntilVacatedOrCancelled()
        {
            Time.captureDeltaTime=1f/240;
            yield return SpawnQueued();var customer=world.RegisteredActors.Single();var id=customer.ActorId;
            var requester=AddTrafficActor("lease-requester",new Vector3(6,0,4));
            Assert.That(flow.TryReservePickup(id,out var reason),Is.True,reason);Assert.That(flow.TryMoveToPickup(id,out reason),Is.True,reason);
            yield return Until(()=>Visit(id).PickupArrived);
            Assert.That(flow.TryMoveToPickupService(id,out reason),Is.True,reason);yield return Until(()=>flow.CanCollectPickup(id));
            var queue=Read<PickupQueueService>(flow,"pickupQueue");Assert.That(queue.TryGetServiceLease(id,out var lease),Is.True);
            Assert.That(flow.TryMoveToExit(id,true,out reason),Is.True,reason);
            yield return Until(()=>Vector3.Distance(customer.transform.position,lease)>.98f);
            var request=Read<long>(PrivateVisit(id),"RequestId");Assert.That(request,Is.GreaterThan(0));
            Assert.That(queue.TryGetServiceLease(id,out var stillOwned),Is.True);Assert.That(stillOwned,Is.EqualTo(lease));
            var paths=new List<IReadOnlyList<Vector3>>{new[]{lease+Vector3.forward*.01f,lease-Vector3.forward*.01f}};
            var clearance=customer.Settings.AgentRadius*2+customer.Settings.CollisionSkin+customer.Settings.Epsilon+2*customer.Settings.MaxSpeed*Time.deltaTime;
            Assert.That(CustomerRoutePlanner.IsClear(paths[0],new[]{customer.transform.position},clearance),Is.True,"The current body already cleared this test path; only its real owned lease still blocks it");
            bool IsTraffic()=>(bool)typeof(CustomerFlowController).GetMethod("HasDeliveryTraffic",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(flow,new object[]{requester,paths});
            Assert.That(IsTraffic(),Is.True,"An accepted exiting owner's service lease will be released by its actual finite movement");
            Assert.That(world.Service.Cancel(request),Is.True);
            Assert.That(queue.TryGetServiceLease(id,out _),Is.True,"Cancel does not fabricate actual departure or free ownership");
            Assert.That(IsTraffic(),Is.False,"Cancelled exit cannot authorize any temporary lease wait");
        }

        [UnityTest] public IEnumerator AcceptedSteadyPickupRequestRemainsFiniteTrafficWithoutNativeHasPath()
        {
            yield return SpawnQueued();var customer=world.RegisteredActors.Single();var id=customer.ActorId;
            var requester=AddTrafficActor("traffic-requester",new Vector3(-3,0,0));
            Assert.That(flow.TryReservePickup(id,out var reason),Is.True,reason);
            Assert.That(flow.TryMoveToPickup(id,out reason),Is.True,reason);
            var visit=PrivateVisit(id);var requestId=Read<long>(visit,"RequestId");
            var generation=Read<int>(visit,"Generation");
            Assert.That(requestId,Is.GreaterThan(0));
            var accepted=(System.Collections.IDictionary)typeof(NavigationService).GetField("byId",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(world.Service);
            Assert.That(accepted.Contains(requestId),Is.True,"Real current accepted Nav request, not a state label");
            var request=accepted[requestId];var driver=(INavigationDriver)request.GetType().GetField("Driver").GetValue(request);
            customer.Agent.ResetPath(); // Native presentation disappears; the real steady driver still owns its complete corners.
            Assert.That(customer.Agent.hasPath,Is.False);
            Assert.That(driver.Observe().PathState,Is.EqualTo(NavigationPathState.Complete));
            var before=customer.transform.position;
            yield return Until(()=>Vector3.Distance(customer.transform.position,before)>.1f);
            Assert.That(Read<long>(visit,"RequestId"),Is.EqualTo(requestId));
            Assert.That(Read<int>(visit,"Generation"),Is.EqualTo(generation));
            Assert.That(accepted[requestId],Is.SameAs(request),"Actual displacement uses the same original accepted segment");
            var paths=new System.Collections.Generic.List<System.Collections.Generic.IReadOnlyList<Vector3>>{
                new[]{requester.transform.position,new Vector3(3,0,0)}};
            bool IsTraffic()=>(bool)typeof(CustomerFlowController).GetMethod("HasDeliveryTraffic",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(flow,new object[]{requester,paths});
            Assert.That(IsTraffic(),Is.True,"Owned steady request is bounded by the real NavigationService even when hasPath is false");
            yield return Until(()=>Visit(id).PickupArrived);
            Assert.That(accepted.Contains(requestId),Is.False);
            Assert.That(Vector3.Distance(customer.transform.position,new Vector3(4,0,1.1f)),Is.LessThanOrEqualTo(.081f));
            Assert.That(IsTraffic(),Is.False,"Normally completed body is no longer moving traffic");
            Assert.That(flow.TrySpawn(),Is.True);var second=flow.Snapshot.Single(v=>v.VisitId!=id).VisitId;
            yield return Until(()=>flow.Snapshot.Any(v=>v.VisitId==second && v.State==CustomerVisitState.Queued));
            Assert.That(flow.TryReservePickup(second,out reason),Is.True,reason);Assert.That(flow.TryMoveToPickup(second,out reason),Is.True,reason);
            var secondVisit=PrivateVisit(second);var secondRequest=Read<long>(secondVisit,"RequestId");
            Assert.That(world.Service.Cancel(secondRequest),Is.True);
            Assert.That(Read<long>(secondVisit,"RequestId"),Is.Zero);
            Assert.That(accepted.Contains(secondRequest),Is.False);
            Assert.That(IsTraffic(),Is.False,"The same parked body after Cancel cannot authorize temporary traffic");
        }

        private NavigationActor AddTrafficActor(string id, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase11/Characters/Shiba/PF_Shiba_Navigation.prefab");
            var actor = Object.Instantiate(prefab, position, Quaternion.identity, root.transform).GetComponent<NavigationActor>();
            Assert.That(actor.TryInitializeRuntimeId(id), Is.True);
            Assert.That(world.Register(actor), Is.True, "Traffic body must bind at its real initial pose");
            return actor;
        }
        private object PrivateVisit(string id) => ((System.Collections.IDictionary)typeof(CustomerFlowController)
            .GetField("visits", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(flow))[id];
        private static T Read<T>(object target, string field) => (T)target.GetType()
            .GetField(field, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }
}

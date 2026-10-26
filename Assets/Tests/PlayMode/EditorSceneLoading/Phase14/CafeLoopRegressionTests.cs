#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.CafeLoop;
using AnimalCafe.Capacity;
using AnimalCafe.Content;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using AnimalCafe.Orders;
using AnimalCafe.Tests.PlayMode.EditorSceneLoading;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode.Phase14
{
    // Test-only observer: actual body destruction, pose and original released token must agree.
    public sealed class CafeActualExitObserver:MonoBehaviour
    {
        public string VisitId;
        public Action<string,Vector3> Receipt;
        private void OnDestroy() => Receipt?.Invoke(VisitId,transform.position);
    }
    internal static class CafeLoopRuntimeProof
    {
        internal const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        internal static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Flags).GetValue(owner);
        internal static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Flags).SetValue(owner,value);
        internal static IEnumerator Twenty(Scene scene,CafeLoopController owner,CustomerFlowController customers,EmployeeFlowController employees,
            PickupCupDisplay cups,GameTimeService time,NavigationWorld world,NavigationLayoutAdapter adapter,GameSpeed speed)
        {
            var seen=new HashSet<string>(); var receipts=new Dictionary<string,Vector3>(); var observers=new List<CafeActualExitObserver>();
            var cash=new Dictionary<string,int>(); var prepared=new Dictionary<string,int>(); var arrivals=new Dictionary<string,int>();
            var delivered=new Dictionary<string,CafeOrderKey>(); var completed=new Dictionary<string,CafeOrderKey>(); var species=new HashSet<string>();
            var tracking=true; var pausedWork=false; var pausedCarry=false; var workingAnimation=false; var carryAnimation=false; var emptyReturn=false;
            var exit=Field<Transform>(customers,"exit"); var tokens=new Dictionary<string,CapacityToken>();
            var maxPaidPickupWaiters=0;var maxCRQueue=0;
            Action<CafeTaskEvent> onTask=value=>
            {
                var id=value.Checkpoint.VisitId;
                if(value.Kind==CafeTaskEventKind.ServiceFinished) Increment(cash,id);
                if(value.Kind==CafeTaskEventKind.Prepared) Increment(prepared,id);
                if(value.Kind==CafeTaskEventKind.DeliveryArrived) Increment(arrivals,id);
            };
            Action<CafeLoopSnapshot> onSnapshot=value=>
            {
                maxPaidPickupWaiters=Math.Max(maxPaidPickupWaiters,customers.Snapshot.Count(v=>
                    v.State==CustomerVisitState.PickupWaiting && value.PaidVisitIds.Contains(v.VisitId)));
                maxCRQueue=Math.Max(maxCRQueue,customers.Snapshot.Count(v=>v.State==CustomerVisitState.Queued ||
                    v.State==CustomerVisitState.Entering || v.State==CustomerVisitState.Advancing));
                foreach(var visit in customers.Snapshot)
                {
                    if(!seen.Add(visit.VisitId)) continue;
                    var body=world.RegisteredActors.Single(a=>a.ActorId==visit.VisitId);
                    species.Add(body.ModelAnimator.runtimeAnimatorController.name);
                    tokens.Add(visit.VisitId,customers.Capacity.GetReservations().Single(r=>r.OwnerId==visit.VisitId && r.Kind==CapacityKind.TotalCustomers).Token);
                    var observer=body.gameObject.AddComponent<CafeActualExitObserver>(); observers.Add(observer); observer.VisitId=visit.VisitId;
                    observer.Receipt=(id,pose)=>
                    {
                        if(!tracking) return;
                        Assert.That(completed.ContainsKey(id),Is.True,"Actual exit must belong to a committed collection");
                        Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,tokens[id])).State,Is.EqualTo(ReservationState.Released));
                        Assert.That(Vector3.Distance(pose,exit.position),Is.LessThanOrEqualTo(.081f),"Destruction away from the real exit is not success");
                        Assert.That(receipts.ContainsKey(id),Is.False,"One actual body exit per Visit"); receipts.Add(id,pose);
                    };
                }
                if(seen.Count>=20) customers.AutoSpawn=false;
                foreach(var delivery in value.Delivery)
                { if(delivered.TryGetValue(delivery.VisitId,out var prior)) Assert.That(prior,Is.EqualTo(delivery.Key)); else delivered.Add(delivery.VisitId,delivery.Key); }
                foreach(var order in value.Orders.Where(p=>p.Value.State==OrderState.Completed))
                { Assert.That(order.Value.CustomerId,Is.Not.Null); if(!completed.ContainsKey(order.Value.CustomerId)) completed.Add(order.Value.CustomerId,order.Key); }
            };
            employees.IntegratedTaskEvent+=onTask; owner.SnapshotChanged+=onSnapshot;
            Assert.That(time.TrySetSpeed(speed),Is.True); customers.AutoSpawn=true;
            try
            {
                for(var frame=0;frame<50000 && (receipts.Count<20 || customers.Snapshot.Count>0 || employees.Snapshot.Any(e=>e.State!=EmployeeState.Idle));frame++)
                {
                    Assert.That(owner.Snapshot.Failure,Is.Empty,owner.Snapshot.Status+" / "+customers.Status);
                    Assert.That(customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked),Is.False,customers.Status);
                    var actors=world.RegisteredActors.ToArray(); var before=actors.Select(a=>a.transform.position).ToArray();
                    yield return null;
                    CheckBodies(actors,before,adapter);
                    foreach(var state in employees.Snapshot)
                    {
                        var body=world.RegisteredActors.Single(a=>a.ActorId==state.ActorId); var animator=body.ModelAnimator;
                        if(state.State==EmployeeState.Working && animator.GetCurrentAnimatorStateInfo(0).IsName("Working")) workingAnimation=true;
                        if(state.State==EmployeeState.CarryingToPickUp && animator.GetCurrentAnimatorStateInfo(0).IsName("Walk") && animator.GetLayerWeight(1)>.99f) carryAnimation=true;
                        if(state.State==EmployeeState.Returning && animator.GetLayerWeight(1)<.01f && !body.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="EmployeeHeldCup")) emptyReturn=true;
                    }
                    var work=employees.Snapshot.Any(e=>e.State==EmployeeState.Working); var carry=employees.Snapshot.Any(e=>e.State==EmployeeState.CarryingToPickUp);
                    if(!pausedWork && work || !pausedCarry && carry)
                    {
                        pausedWork|=work; pausedCarry|=carry; time.SetPaused(); yield return null;
                        var held=world.RegisteredActors.ToArray(); var poses=held.Select(a=>a.transform.position).ToArray();
                        var animation=held.Select(a=>a.ModelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime).ToArray();
                        var remaining=employees.Snapshot.Select(e=>e.RemainingWorkSeconds).ToArray();
                        for(var pause=0;pause<4;pause++)
                        { yield return null; CollectionAssert.AreEqual(poses,held.Select(a=>a.transform.position));
                            CollectionAssert.AreEqual(remaining,employees.Snapshot.Select(e=>e.RemainingWorkSeconds));
                            for(var i=0;i<held.Length;i++) Assert.That(held[i].ModelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime,Is.EqualTo(animation[i]).Within(.00001f)); }
                        Assert.That(time.TrySetSpeed(speed),Is.True);
                    }
                }
                Assert.That(seen.Count,Is.EqualTo(20)); Assert.That(receipts.Count,Is.EqualTo(20),"Actual pose/token exit receipts, not missing Visits");
                Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(20)); Assert.That(owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed),Is.True);
                Assert.That(owner.Snapshot.Orders.Values.GroupBy(o=>o.CustomerId).All(g=>g.Count()==1),Is.True);
                CollectionAssert.AreEquivalent(seen,owner.Snapshot.PaidVisitIds);
                foreach(var id in seen)
                {
                    Assert.That(cash[id],Is.EqualTo(1)); Assert.That(prepared[id],Is.EqualTo(1)); Assert.That(arrivals[id],Is.EqualTo(1));
                    Assert.That(delivered[id],Is.EqualTo(completed[id])); Assert.That(owner.Snapshot.Orders[completed[id]].CustomerId,Is.EqualTo(id));
                }
                Assert.That(species.Count,Is.GreaterThanOrEqualTo(2)); Assert.That(pausedWork && pausedCarry && workingAnimation && carryAnimation && emptyReturn,Is.True);
                Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True); Assert.That(owner.Snapshot.Delivery,Is.Empty);
                Assert.That(cups.VisibleOrders,Is.Empty); Assert.That(world.RegisteredActors.Count,Is.EqualTo(2));
                Assert.That(employees.Snapshot.All(e=>e.State==EmployeeState.Idle && e.HandleId==0),Is.True);
                Assert.That(((IDictionary)Field<object>(world.Service,"byId")).Count,Is.Zero);
                Assert.That(scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Any(t=>t.name=="EmployeeHeldCup" || t.name.StartsWith("PickupCup_")),Is.False);
                Directory.CreateDirectory("outputs/phase14"); File.WriteAllLines("outputs/phase14/task9-real20-"+speed+".tsv",
                    new[]{"Visit\tOrderKey\tCashierFinished\tPrepared\tFinalDeliveryArrival\tActualExit\tOriginalTotalToken"}.Concat(seen.OrderBy(id=>id).Select(id=>
                        id+"\t"+completed[id].SessionId+"/"+completed[id].Epoch+"/"+completed[id].OrderId+"\t"+cash[id]+"\t"+prepared[id]+"\t"+arrivals[id]+"\t"+receipts[id].ToString("F4")+"\t"+tokens[id].Id)));
                File.WriteAllText("outputs/phase14/single-file/natural-rhythm-"+speed+".txt",
                    "maxPaidPickupWaiters="+maxPaidPickupWaiters+" maxCRQueue="+maxCRQueue);
                TestContext.WriteLine("P14_REAL20 speed="+speed+" distinct="+seen.Count+" actualExit="+receipts.Count+" species="+species.Count+" maxPaidPickupWaiters="+maxPaidPickupWaiters+" maxCRQueue="+maxCRQueue+" working/carry/emptyReturn=True allCapacity/cups/handles/requests=0");
            }
            finally
            { tracking=false; customers.AutoSpawn=false; employees.IntegratedTaskEvent-=onTask; owner.SnapshotChanged-=onSnapshot; foreach(var observer in observers) if(observer!=null) observer.Receipt=null; }
        }
        private static void Increment(Dictionary<string,int> values,string id) { values.TryGetValue(id,out var count); values[id]=count+1; }
        internal static void CheckBodies(NavigationActor[] actors,Vector3[] before,NavigationLayoutAdapter adapter)
        {
            for(var i=0;i<actors.Length;i++)
            {
                if(actors[i]==null) continue; var delta=actors[i].transform.position-before[i];
                Assert.That(delta.magnitude,Is.LessThanOrEqualTo(actors[i].Settings.MaxSpeed*Time.deltaTime+.003f));
                Assert.That(adapter.IsPointWalkable(actors[i].transform.position),Is.True);
                for(var j=0;j<i;j++)
                { if(actors[j]==null) continue; var relative=before[i]-before[j]; var change=delta-(actors[j].transform.position-before[j]);
                    var t=change.sqrMagnitude<1e-12f?0:Mathf.Clamp01(-Vector3.Dot(relative,change)/change.sqrMagnitude);
                    Assert.That((relative+change*t).magnitude,Is.GreaterThanOrEqualTo(actors[i].Settings.AgentRadius+actors[j].Settings.AgentRadius-.002f)); }
            }
        }
    }
    public sealed class CafeLoopRegressionTests
    {
        private Scene scene; private CafeLoopController owner; private CafeLoopValidationController tools;
        private CustomerFlowController customers; private EmployeeFlowController employees; private PickupCupDisplay cups;
        private GameTimeService time; private NavigationWorld world; private NavigationLayoutAdapter adapter;
        private float capture,scale; private UnityEngine.InputSystem.InputActionAsset[] inputs;
        [UnitySetUp] public IEnumerator Setup()
        {
            capture=Time.captureDeltaTime; scale=Time.timeScale; Time.captureDeltaTime=1f/60;
            scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase14CafeLoop.unity",new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
            owner=Find<CafeLoopController>(); tools=Find<CafeLoopValidationController>(); customers=Find<CustomerFlowController>(); employees=Find<EmployeeFlowController>();
            cups=Find<PickupCupDisplay>(); time=Find<GameTimeService>(); world=Find<NavigationWorld>(); adapter=Find<NavigationLayoutAdapter>();
            tools.HoldArrivals(); inputs=Phase8SceneInputTestCleanup.CaptureAssets(scene);
            yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(scene.IsValid() && scene.isLoaded) { SceneManager.SetActiveScene(SceneManager.CreateScene("Task9 cleanup")); yield return SceneManager.UnloadSceneAsync(scene); }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputs ?? Array.Empty<UnityEngine.InputSystem.InputActionAsset>());
            Time.captureDeltaTime=capture; Time.timeScale=scale;
        }
        [UnityTest] public IEnumerator TotalFullResetsClockThenDrawsOneCompleteRandomInterval() => CapacityFull(CapacityKind.TotalCustomers);
        [UnityTest] public IEnumerator CounterFullResetsClockThenDrawsOneCompleteRandomInterval() => CapacityFull(CapacityKind.CounterQueue);
        [UnityTest] public IEnumerator PickupFullResetsClockThenDrawsOneCompleteRandomInterval() => CapacityFull(CapacityKind.PickUp);
        private IEnumerator CapacityFull(CapacityKind kind)
        {
            var capacity=customers.Capacity; var tokens=new List<CapacityToken>();
            while(capacity.GetCapacities().Single(c=>c.Kind==kind).Available>0)
            { var reserved=capacity.TryReserve("test-capacity-lock-"+tokens.Count,new[]{kind}); Assert.That(reserved.Succeeded,Is.True); tokens.Add(reserved.Reservations.Single().Token); }
            tools.ResumeArrivals(); owner.Step(.1f); owner.Step(100);
            Assert.That(customers.Snapshot,Is.Empty); Assert.That(customers.RemainingSeconds,Is.Zero); Assert.That(owner.Snapshot.Orders,Is.Empty);
            for(var i=0;i<tokens.Count;i++) Assert.That(capacity.Release(tokens[i],"test-capacity-lock-"+i).Succeeded,Is.True);
            owner.Step(.01f); Assert.That(customers.RemainingSeconds,Is.InRange(.989f,4.991f)); Assert.That(customers.Snapshot,Is.Empty);
            var remaining=customers.RemainingSeconds; var delta=Math.Min(.1f,remaining/2); owner.Step(delta);
            Assert.That(customers.RemainingSeconds,Is.EqualTo(remaining-delta).Within(.00001f),"Customer consumes the supplied delta exactly once");
            owner.Step(remaining+20); tools.HoldArrivals(); Assert.That(customers.Snapshot.Count,Is.EqualTo(1),"No accumulated admission backlog in one large tick");
            Assert.That(customers.Capacity,Is.SameAs(capacity)); yield return Until(()=>customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(capacity.GetCapacities().All(c=>c.Used==0),Is.True);
        }
        [UnityTest] public IEnumerator RealVisualRefusalAndCompleteRejectionPreserveBusinessThenRepairWhileReturnBlocked()
        {
            tools.HoldCollection(); tools.ResumeArrivals(); yield return Until(()=>customers.Snapshot.Count==1); tools.HoldArrivals();
            var prefab=CafeLoopRuntimeProof.Field<GameObject>(cups,"cupPrefab"); CafeLoopRuntimeProof.Set(cups,"cupPrefab",null);
            yield return Until(()=>owner.Snapshot.Delivery.Count==1 && employees.Snapshot.Any(e=>e.State==EmployeeState.Returning));
            Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.ReadyForPickup)); Assert.That(cups.VisibleOrders,Is.Empty);
            Assert.That(owner.Snapshot.Failure,Does.Contain("Cup prefab")); tools.BlockCurrentReturnRoute(); Assert.That(tools.Feedback,Does.StartWith("Registered owned"));
            CafeLoopRuntimeProof.Set(cups,"cupPrefab",prefab); yield return Until(()=>cups.VisibleOrders.Count==1);
            yield return Until(()=>employees.Snapshot.Any(e=>e.Role==EmployeeRole.Barista && e.State==EmployeeState.Blocked),6000);
            var service=CafeLoopRuntimeProof.Field<CafeLoopService>(owner,"service"); var operation=CafeLoopRuntimeProof.Field<Delegate>(service,"completeOrder"); var rejected=0;
            CafeLoopRuntimeProof.Set(service,"completeOrder",new Func<OrderService,long,string,OrderResult>((orders,id,visit)=>{rejected++; return orders.Complete(id,"wrong-real-owner");}));
            var id=customers.Snapshot.Single().VisitId; var tokenStates=customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.State).ToArray();
            tools.ResumeCollection(); yield return Until(()=>rejected==1);
            Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.ReadyForPickup)); Assert.That(owner.Snapshot.Delivery.Count,Is.EqualTo(1));
            Assert.That(owner.Snapshot.CollectedExitVisitIds,Is.Empty); Assert.That(cups.VisibleOrders.Count,Is.EqualTo(1));
            CollectionAssert.AreEqual(tokenStates,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.State));
            CafeLoopRuntimeProof.Set(service,"completeOrder",operation); CafeLoopRuntimeProof.Set(cups,"projecting",true); owner.ResumeBlockedFlow();
            yield return Until(()=>owner.Snapshot.Orders.Values.Single().State==OrderState.Completed);
            Assert.That(owner.Snapshot.Delivery,Is.Empty); Assert.That(cups.VisibleOrders.Count,Is.EqualTo(1),"Actual display refusal after Complete cannot roll business back");
            Assert.That(owner.Snapshot.Failure,Is.Not.Empty); CafeLoopRuntimeProof.Set(cups,"projecting",false); yield return Until(()=>cups.VisibleOrders.Count==0);
            yield return Until(()=>customers.Snapshot.Count==0); Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.Completed));
            tools.RemoveTestBlockers(); tools.ResumeBlockedFlow(); yield return Until(()=>employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1)); Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
        }
        [UnityTest] public IEnumerator RotatedGridMultiStationAndNonFirstSlotUseActualOrdinalBindingAndCupModule()
            => MultiStationOrdinalAndCupModule(false);
        [UnityTest] public IEnumerator RecordedGuidSecondRegisterAndMachineHaveSafeQueueAndMiddleSlotDelivery()
            => MultiStationOrdinalAndCupModule(true);
        [UnityTest] public IEnumerator FirstRegisterSecondMachineHaveSafeQueueAndMiddleSlotDelivery()
            => MultiStationOrdinalAndCupModule(true,true,false);
        [UnityTest] public IEnumerator SecondRegisterFirstMachineHaveSafeQueueAndMiddleSlotDelivery()
            => MultiStationOrdinalAndCupModule(true,false,true);
        private IEnumerator MultiStationOrdinalAndCupModule(bool recordedGuid,bool firstRegister=false,bool firstMachine=false)
        {
            var oldInputs=inputs;
            var oldScene=scene; SceneManager.SetActiveScene(SceneManager.CreateScene("Task9 multi station switch"));
            yield return SceneManager.UnloadSceneAsync(oldScene); Phase8SceneInputTestCleanup.DisposeReleasedAssets(oldInputs); inputs=null;
            scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",new LoadSceneParameters(LoadSceneMode.Single));
            var pivot=new GameObject("Task9 rigid grid fixture"); SceneManager.MoveGameObjectToScene(pivot,scene);
            foreach(var root in scene.GetRootGameObjects().Where(r=>r!=pivot).ToArray()) root.transform.SetParent(pivot.transform,true);
            if(!recordedGuid) pivot.transform.SetPositionAndRotation(new Vector3(13,0,-7),Quaternion.Euler(0,37,0));
            yield return null; inputs=Phase8SceneInputTestCleanup.CaptureAssets(scene);
            owner=Find<CafeLoopController>(); customers=Find<CustomerFlowController>(); employees=Find<EmployeeFlowController>(); cups=Find<PickupCupDisplay>();
            time=Find<GameTimeService>(); tools=Find<CafeLoopValidationController>(); world=Find<NavigationWorld>(); adapter=Find<NavigationLayoutAdapter>(); tools.HoldArrivals();
            var layout=Find<CafeLayoutRuntime>(); var decor=Find<DecorationModeController>(); var initial=layout.Layout.FurnitureInstances.Single();
            Assert.That(world.RegisteredActors,Is.Empty,"Rigid transform and formal authoring precede every actor birth"); decor.EnterDecorationMode();
            Assert.That(layout.Layout.MoveFurniture(initial.InstanceId,new GridPosition(1,5)).Succeeded,Is.True);
            var catalog=(FurnitureContentCatalog)new SerializedObject(decor).FindProperty("contentCatalog").objectReferenceValue;
            var slot=catalog.BuildSurfaceSlotCatalog(layout.Layout.GridSettings).GetForSupport(initial.DefinitionId).First().SlotId;
            // Both possible CR Customer anchors must stay outside the formal entrance reservation.
            var positions=new[]{new GridPosition(4,6),new GridPosition(0,6),new GridPosition(3,3)};
            var supports=recordedGuid?new[]{"9241c0c539aa4795b537b87c087f94dd","6b2536ca27dc480b8150d9a734c1807f","717ec02d7d9d472bb93f499aea8cd0ef"}:
                new[]{"19000000000000000000000000000002","19000000000000000000000000000003","19000000000000000000000000000004"};
            for(var i=0;i<positions.Length;i++) Assert.That(layout.Layout.PlaceFurniture(FurnitureInstance.Restore(supports[i],initial.DefinitionId,positions[i],FurnitureRotation.Degrees0)).Succeeded,Is.True);
            var multi=recordedGuid?"8ccd8ca5298f418589083bb0fd140c46":"19000000000000000000000000000009";
            var cash=recordedGuid?(firstRegister?"0c6a7ab4320d4ea9a7f8144d6ed04cfe":"ac6a7ab4320d4ea9a7f8144d6ed04cfe"):"19000000000000000000000000000010";
            var machine=recordedGuid?(firstMachine?"0a267a1b81fb4f849f3cf202f6edf215":"3a267a1b81fb4f849f3cf202f6edf215"):"19000000000000000000000000000011";
            var extraCash=recordedGuid?"374b1e501d7c4ae89a5ae30ddb5d24e7":"19000000000000000000000000000020";
            var extraMachine=recordedGuid?"1946afedddef4a26aef6adce1e91ba33":"19000000000000000000000000000021";
            var pickup="19000000000000000000000000000012";
            Assert.That(layout.Layout.PlaceFurniture(FurnitureInstance.Restore(multi,"counter.preset.1x3",new GridPosition(5,2),FurnitureRotation.Degrees90)).Succeeded,Is.True);
            Assert.That(layout.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance(cash,"equipment.cash-register.01",new SurfaceSlotAddress(initial.InstanceId,slot),FurnitureRotation.Degrees0)).Succeeded,Is.True);
            Assert.That(layout.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance(machine,"equipment.coffee-machine.01",new SurfaceSlotAddress(supports[0],slot),FurnitureRotation.Degrees0)).Succeeded,Is.True);
            Assert.That(layout.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance(extraCash,"equipment.cash-register.01",new SurfaceSlotAddress(supports[2],slot),FurnitureRotation.Degrees0)).Succeeded,Is.True);
            Assert.That(layout.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance(extraMachine,"equipment.coffee-machine.01",new SurfaceSlotAddress(supports[1],slot),FurnitureRotation.Degrees0)).Succeeded,Is.True);
            var address=new SurfaceSlotAddress(multi,"slot.1"); Assert.That(layout.FunctionalSurfaceLayout.PlacePickUp(new PickUpPointInstance(pickup,address)).Succeeded,Is.True);
            decor.RefreshConfirmedNavigationPresentation();
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,machine),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True); Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True); decor.ExitDecorationMode();
            yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            var selectedCash=recordedGuid && !firstRegister?extraCash:cash; var selectedMachine=recordedGuid && !firstMachine?extraMachine:machine;
            Assert.That(customers.SelectedRegisterId,Is.EqualTo(selectedCash)); employees.TryGetBinding(EmployeeRole.Cashier,out var cashier); employees.TryGetBinding(EmployeeRole.Barista,out var barista);
            Assert.That(cashier.WorkStationId,Is.EqualTo(selectedCash)); Assert.That(barista.WorkStationId,Is.EqualTo(selectedMachine)); Assert.That(barista.CupAddress.Value,Is.EqualTo(address));
            var recipeVariant=!recordedGuid?0:firstRegister?2:firstMachine?3:1;
            CafePickupSideTests.ExportLayoutRecipe(scene,"L-10",recipeVariant,"BUILT_ORDINAL_CONTROLLED_IDS");
            var slots=CafeLoopRuntimeProof.Field<Vector3[]>(customers,"counterWaitSlots");
            TestContext.WriteLine("P14_M017_GUID recorded="+recordedGuid+" selectedCR="+cashier.WorkStationId+" work="+cashier.WorkTarget.Position.ToString("F6")+
                " selectedCM="+barista.WorkStationId+" queueSlots="+slots.Length+" reception="+owner.ViewSnapshot.Reception+" customerStatus="+customers.Status);
            Assert.That(slots.Length,Is.GreaterThan(0),"Published multi-station recipe must admit a real guest for the actual ordinal-selected CR");
            tools.HoldCollection(); tools.ResumeArrivals(); yield return Until(()=>customers.Snapshot.Count==1); tools.HoldArrivals(); yield return Until(()=>owner.Snapshot.Delivery.Count==1);
            var registry=Find<FurnitureSceneRegistry>(); Assert.That(registry.TryGet(multi,out var support),Is.True);
            var module=support.GetComponentsInChildren<SurfaceSlotMarker>().Single(s=>s.SlotId=="slot.1").transform;
            var cup=cups.VisibleOrders.Single().Value; Assert.That(cup.transform.parent,Is.SameAs(module)); Assert.That(cup.transform.localPosition,Is.EqualTo(new Vector3(-.12f,0,-.08f)));
            Assert.That(Vector3.Distance(cup.transform.position,module.TransformPoint(new Vector3(-.12f,0,-.08f))),Is.LessThan(.0001f));
            TestContext.WriteLine("P14_M017_NORMAL_GRID CR(1,5) rot0 id="+cash+"; CM(4,6) equipmentRot90 id="+machine+"; extraCR(3,3) rot0 id="+extraCash+"; extraCM(0,6) rot0 id="+extraMachine+"; Pickup counter.preset.1x3 (5,2) furnitureRot90 slot.1 id="+pickup+"; baseSlot="+slot+"; selectedOrdinal="+selectedCash+"/"+selectedMachine+"; rigidTest="+!recordedGuid);
            tools.ResumeCollection(); yield return Until(()=>customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
            CafePickupSideTests.ExportLayoutRecipe(scene,"L-10",recipeVariant,"PASS_ORDINAL_CONTROLLED_IDS");
        }
        [UnityTest] public IEnumerator WholeOwnerPreparedAndReturningDisableResumeSameBodyWithoutRepreparingOrRedelivery()
        {
            var prepared=0; var delivered=0;
            employees.IntegratedTaskEvent+=e=>{if(e.Kind==CafeTaskEventKind.Prepared) prepared++; if(e.Kind==CafeTaskEventKind.DeliveryArrived) delivered++;};
            tools.HoldCollection(); tools.ResumeArrivals(); yield return Until(()=>customers.Snapshot.Count==1); tools.HoldArrivals();
            yield return Until(()=>employees.Snapshot.Any(e=>e.State==EmployeeState.CarryingToPickUp));
            var bodies=world.RegisteredActors.ToArray(); owner.enabled=false; var positions=bodies.Select(a=>a.transform.position).ToArray();
            for(var f=0;f<4;f++) { yield return null; CollectionAssert.AreEqual(positions,bodies.Select(a=>a.transform.position)); }
            Assert.That(owner.Snapshot.Employees.Single(e=>e.Role==EmployeeRole.Barista).Stage,Is.EqualTo(CafeWorkStage.Prepared));
            owner.enabled=true; owner.ResumeBlockedFlow(); yield return Until(()=>owner.Snapshot.Delivery.Count==1 && employees.Snapshot.Any(e=>e.State==EmployeeState.Returning));
            owner.enabled=false; var returning=bodies.Select(a=>a.transform.position).ToArray();
            for(var f=0;f<4;f++) { yield return null; CollectionAssert.AreEqual(returning,bodies.Select(a=>a.transform.position)); }
            Assert.That(owner.Snapshot.Employees.Single(e=>e.Role==EmployeeRole.Barista).Stage,Is.GreaterThanOrEqualTo(CafeWorkStage.Delivered));
            owner.enabled=true; owner.ResumeBlockedFlow(); tools.ResumeCollection();
            yield return Until(()=>customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(prepared,Is.EqualTo(1)); Assert.That(delivered,Is.EqualTo(1)); Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.Completed));
            Assert.That(world.RegisteredActors.All(a=>bodies.Contains(a)),Is.True); Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
        }
        [UnityTest] public IEnumerator RealCreateRejectionKeepsFinishedAcrossOwnerDisableUntilExplicitRetryWithoutTwoMoreSeconds()
        {
            var service=CafeLoopRuntimeProof.Field<CafeLoopService>(owner,"service"); var operation=CafeLoopRuntimeProof.Field<Delegate>(service,"createOrder");
            var rejected=0; CafeLoopRuntimeProof.Set(service,"createOrder",new Func<OrderService,string,string,OrderResult>((orders,visit,product)=>{rejected++; return orders.Create("",product);}));
            tools.ResumeArrivals(); yield return Until(()=>customers.Snapshot.Count==1); tools.HoldArrivals(); yield return Until(()=>rejected==1);
            Assert.That(owner.Snapshot.Orders,Is.Empty); Assert.That(owner.Snapshot.PaidVisitIds,Is.Empty);
            Assert.That(owner.Snapshot.Employees.Single().Stage,Is.EqualTo(CafeWorkStage.Finished));
            var bodies=world.RegisteredActors.ToArray(); owner.enabled=false; var positions=bodies.Select(a=>a.transform.position).ToArray();
            for(var f=0;f<4;f++) { yield return null; CollectionAssert.AreEqual(positions,bodies.Select(a=>a.transform.position)); }
            CafeLoopRuntimeProof.Set(service,"createOrder",operation); owner.enabled=true;
            for(var f=0;f<4;f++) { yield return null; Assert.That(owner.Snapshot.Orders,Is.Empty); }
            var begin=Time.time; owner.ResumeBlockedFlow(); yield return Until(()=>owner.Snapshot.Orders.Count==1);
            Assert.That(Time.time-begin,Is.LessThan(.25f),"Finished only retries the explicit business submission, without another 2-second work cycle");
            yield return Until(()=>customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1)); Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
        }
        [UnityTest] public IEnumerator CancelledPreviewWithTwoTableCupsAndCarriedCupPreservesActualObjectsAndEpoch()
        {
            tools.HoldCollection(); tools.ResumeArrivals(); yield return Until(()=>customers.Snapshot.Count==3); tools.HoldArrivals();
            yield return Until(()=>owner.Snapshot.Delivery.Count==2 && employees.Snapshot.Any(e=>e.State==EmployeeState.CarryingToPickUp));
            var epoch=owner.Snapshot.Epoch; var tables=cups.VisibleOrders.ToArray(); var hand=employees.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="EmployeeHeldCup");
            var before=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista); var decor=Find<DecorationModeController>();
            decor.EnterDecorationMode(); var machine=employees.TryGetBinding(EmployeeRole.Barista,out var binding)?binding.WorkStationId:null;
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,machine),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True); decor.CancelFunctionalSurfacePreview(); decor.ExitDecorationMode();
            Assert.That(owner.Snapshot.Epoch,Is.EqualTo(epoch)); Assert.That(employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).HandleId,Is.EqualTo(before.HandleId));
            foreach(var pair in tables) Assert.That(cups.VisibleOrders[pair.Key],Is.SameAs(pair.Value));
            Assert.That(employees.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="EmployeeHeldCup"),Is.SameAs(hand));
            yield return Until(()=>owner.Snapshot.Delivery.Count==3); tools.ResumeCollection();
            yield return Until(()=>customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
        }
        [UnityTest] public IEnumerator DisabledCustomerCannotInitializeNavigationThroughIntegratedOwner()
        {
            var oldInputs=inputs; SceneManager.SetActiveScene(SceneManager.CreateScene("Disabled customer boundary"));
            yield return SceneManager.UnloadSceneAsync(scene); Phase8SceneInputTestCleanup.DisposeReleasedAssets(oldInputs); inputs=null;
            scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; inputs=Phase8SceneInputTestCleanup.CaptureAssets(scene);
            owner=Find<CafeLoopController>(); customers=Find<CustomerFlowController>(); employees=Find<EmployeeFlowController>();
            time=Find<GameTimeService>(); adapter=Find<NavigationLayoutAdapter>(); world=Find<NavigationWorld>(); tools=Find<CafeLoopValidationController>();
            Assert.That(adapter.EnforceBusinessReadiness,Is.False);
            P8RTwoButtonTimeControlTests.PrepareBusinessLayout(scene); yield return null;
            Assert.That(customers.isActiveAndEnabled,Is.False); Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True);
            var remaining=customers.RemainingSeconds; var requests=((IDictionary)CafeLoopRuntimeProof.Field<object>(world.Service,"byId")).Count;
            owner.Step(.25f); customers.Step(.25f);
            Assert.That(adapter.EnforceBusinessReadiness,Is.False,"A disabled customer cannot acquire navigation policy through the real integrated owner");
            Assert.That(adapter.GetComponent<NavigationDecorationBridge>().EnforceBusinessReadiness,Is.False);
            Assert.That(customers.RemainingSeconds,Is.EqualTo(remaining)); Assert.That(customers.Snapshot,Is.Empty);
            Assert.That(((IDictionary)CafeLoopRuntimeProof.Field<object>(world.Service,"byId")).Count,Is.EqualTo(requests));
        }
        [UnityTest] public IEnumerator DisabledCustomerKeepsActualVisitBodyAndCapacityUntilExplicitResume()
        {
            tools.ResumeArrivals(); yield return Until(()=>customers.Snapshot.Count==1); tools.HoldArrivals(); time.SetPaused();
            var visit=customers.Snapshot.Single(); var body=world.RegisteredActors.Single(a=>a.ActorId==visit.VisitId);
            customers.enabled=false; var held=customers.Snapshot.Single(); var pose=body.transform.position;
            var capacity=customers.Capacity; var tokens=capacity.GetReservations().Where(r=>r.OwnerId==visit.VisitId).ToArray();
            var states=tokens.Select(r=>r.State).ToArray(); var remaining=customers.RemainingSeconds;
            Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True);
            for(var f=0;f<4;f++) { owner.Step(.1f); yield return null; Assert.That(body.transform.position,Is.EqualTo(pose)); }
            Assert.That(customers.Snapshot.Single().VisitId,Is.EqualTo(held.VisitId));
            Assert.That(customers.Snapshot.Single().State,Is.EqualTo(held.State));
            Assert.That(customers.RemainingSeconds,Is.EqualTo(remaining));
            Assert.That(customers.Capacity,Is.SameAs(capacity));
            var actual=capacity.GetReservations().Where(r=>r.OwnerId==visit.VisitId).ToArray();
            CollectionAssert.AreEqual(tokens.Select(r=>r.Token),actual.Select(r=>r.Token));
            CollectionAssert.AreEqual(states,actual.Select(r=>r.State)); Assert.That(body,Is.Not.Null);
            customers.enabled=true; owner.ResumeBlockedFlow();
            yield return Until(()=>customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.Completed));
            Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
        }
        [UnityTest] public IEnumerator ToolBlockRegistrationAndRejectionPublishVisibleConsoleResults()
        {
            LogAssert.Expect(LogType.Log,"[P14 tool] Barista is not on the selected route");
            tools.BlockCurrentDeliveryRoute();
            Assert.That(tools.Feedback,Is.EqualTo("Barista is not on the selected route"));
            tools.HoldCollection(); tools.ResumeArrivals(); yield return Until(()=>customers.Snapshot.Count==1); tools.HoldArrivals();
            yield return Until(()=>employees.Snapshot.Any(e=>e.State==EmployeeState.CarryingToPickUp));
            LogAssert.Expect(LogType.Log,new System.Text.RegularExpressions.Regex("^\\[P14 tool\\] Registered owned stationary blocker p14-blocker-[a-f0-9]+$"));
            tools.BlockCurrentDeliveryRoute();
            Assert.That(tools.Feedback,Does.StartWith("Registered owned stationary blocker "));
            var actorId=tools.Feedback.Substring("Registered owned stationary blocker ".Length);
            Assert.That(world.RegisteredActors.Any(a=>a.ActorId==actorId),Is.True,"Visible success must refer to an actually registered owned body");
        }
        [UnityTest] public IEnumerator PrintSnapshotDoesNotOverwriteLatestToolFeedback()
        {
            LogAssert.Expect(LogType.Log,"[P14 tool] Barista is not on the selected route");
            tools.BlockCurrentDeliveryRoute(); var feedback=tools.Feedback;
            LogAssert.Expect(LogType.Log,new System.Text.RegularExpressions.Regex("^P14 .* epoch="));
            tools.PrintSnapshot();
            Assert.That(tools.Feedback,Is.EqualTo(feedback),"Printing readonly facts must retain the most recent mutation result");
            yield return null;
        }
        [UnityTest] public IEnumerator ReplayMenuPrefersObservedOldEpochAfterNewEpochOrderExists()
        {
            tools.HoldCollection(); tools.ResumeArrivals(); yield return Until(()=>customers.Snapshot.Count==1); tools.HoldArrivals();
            yield return Until(()=>owner.Snapshot.Orders.Count==1); var oldEpoch=owner.Snapshot.Epoch;
            var decor=Find<DecorationModeController>(); employees.TryGetBinding(EmployeeRole.Barista,out var binding);
            decor.EnterDecorationMode(); Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,binding.WorkStationId),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True); Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True); decor.ExitDecorationMode();
            yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            tools.ResumeArrivals(); yield return Until(()=>customers.Snapshot.Count==1); tools.HoldArrivals(); yield return Until(()=>owner.Snapshot.Orders.Count==1);
            Assert.That(owner.Snapshot.Epoch,Is.GreaterThan(oldEpoch)); Assert.That(owner.RetiredTaskEvents.Any(e=>e.Token.Epoch==oldEpoch),Is.True);
            var before=owner.Snapshot.Orders.Select(p=>p.Key.OrderId+":"+p.Value.State).ToArray();
            LogAssert.Expect(LogType.Log,new System.Text.RegularExpressions.Regex("^\\[P14 tool\\] .* source=old-epoch token="+
                System.Text.RegularExpressions.Regex.Escape(owner.Snapshot.SessionId)+"/"+oldEpoch+"/\\d+/\\d+ actor=\\S+ handle=\\d+ request=\\d+ generation=\\d+ revision=\\d+ kind=\\S+$"));
            tools.ReplayLastBusinessCallback();
            Assert.That(tools.Feedback,Does.Contain("source=old-epoch")); Assert.That(tools.Feedback,Does.Contain("token="+owner.Snapshot.SessionId+"/"+oldEpoch+"/"));
            CollectionAssert.AreEqual(before,owner.Snapshot.Orders.Select(p=>p.Key.OrderId+":"+p.Value.State));
        }
        private T Find<T>() where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
        private IEnumerator Until(Func<bool> done,int max=6000)
        { for(var f=0;f<max && !done();f++) yield return null; Assert.That(done(),Is.True,owner.Snapshot.Status+" / "+owner.Snapshot.Failure+" / "+customers.Status); }
    }
}
#endif

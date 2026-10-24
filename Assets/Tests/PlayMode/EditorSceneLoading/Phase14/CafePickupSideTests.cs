#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using AnimalCafe.CafeLoop;
using AnimalCafe.Capacity;
using AnimalCafe.Decoration;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using UnityEditor;
using UnityEngine.UI;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Employees;
using AnimalCafe.Navigation;
using AnimalCafe.Orders;
using AnimalCafe.Tests.PlayMode.EditorSceneLoading;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace AnimalCafe.Tests.PlayMode.Phase14
{
    public sealed partial class CafePickupSideTests
    {
        Scene scene; CafeLoopController owner; CustomerFlowController customers; EmployeeFlowController employees;
        CafeLoopValidationController tools; NavigationWorld world; NavigationLayoutAdapter adapter; GameTimeService time;
        readonly System.Collections.Generic.HashSet<long> deliveryStarts=new System.Collections.Generic.HashSet<long>();
        float capture,scale; UnityEngine.InputSystem.InputActionAsset[] inputs;
        [UnitySetUp] public IEnumerator Setup()
        {
            deliveryStarts.Clear(); capture=Time.captureDeltaTime; scale=Time.timeScale; Time.captureDeltaTime=1f/60;
            scene=EditorSceneManager.LoadSceneInPlayMode(TestContext.CurrentContext.Test.Name.StartsWith("SingleSide")?"Assets/Scenes/Validation/Phase14SharedPickup.unity":"Assets/Scenes/Validation/Phase14CafeLoop.unity",new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
            owner=Find<CafeLoopController>(); customers=Find<CustomerFlowController>(); employees=Find<EmployeeFlowController>(); tools=Find<CafeLoopValidationController>();
            world=Find<NavigationWorld>(); adapter=Find<NavigationLayoutAdapter>(); time=Find<GameTimeService>(); inputs=Phase8SceneInputTestCleanup.CaptureAssets(scene);
            employees.IntegratedTaskEvent+=e=>{if(e.Kind!=CafeTaskEventKind.Progress) TestContext.WriteLine("SIDE_EVENT "+e.Kind+" role="+e.Role+" failure="+e.Failure+" stage="+e.Checkpoint.Stage);};
            tools.HoldArrivals(); tools.HoldCollection(); yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
        }
        T Find<T>() where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
        IEnumerator Until(Func<bool> done,bool employeeMayBlock=false)
        {
            for(var i=0;i<6000 && !done();i++)
            {
                Assert.That(customers.Snapshot.All(v=>v.State!=CustomerVisitState.Blocked),Is.True,owner.ViewSnapshot.Reception+" / "+owner.Snapshot.Failure);
                if(!employeeMayBlock && employees.Snapshot.Any(e=>e.State==EmployeeState.Blocked))
                    Assert.Fail("Unexpected employee Blocked: "+string.Join(";",employees.Snapshot.Select(e=>e.Role+"/"+e.State+"/"+e.Failure))+" bodies="+
                        string.Join(";",world.RegisteredActors.Select(a=>a.ActorId+"@"+a.transform.position.ToString("F6")))+" leases="+
                        string.Join(";",CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue")?.ServiceLeases.Select(l=>l.Key+"@"+l.Value.ToString("F6"))??Array.Empty<string>()));
                ObserveDeliveryStart(); yield return null;
            }
            Assert.That(done(),Is.True,owner.ViewSnapshot.Reception+" / "+owner.Snapshot.Failure);
        }
        void ObserveDeliveryStart()
        {
            var staff=employees.Snapshot.FirstOrDefault(e=>e.Role==EmployeeRole.Barista);
            if(staff.State!=EmployeeState.CarryingToPickUp || !deliveryStarts.Add(staff.HandleId)) return;
            var actor=world.RegisteredActors.Single(a=>a.ActorId==staff.ActorId);
            Assert.That(employees.TryGetDeliveryTarget(out var target),Is.True);
            var native=adapter.GetCompletePath(actor.transform.position,target.Position);
            var entries=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits");
            foreach(var visit in customers.Snapshot.Where(v=>v.State==CustomerVisitState.MovingToPickup || v.State==CustomerVisitState.PickupAdvancing))
            {
                var entry=entries[visit.VisitId]; var request=(long)entry.GetType().GetField("RequestId").GetValue(entry); if(request==0) continue;
                var customer=world.RegisteredActors.Single(a=>a.ActorId==visit.VisitId);
                var queue=(System.Collections.Generic.Queue<Vector3>)entry.GetType().GetField("Route").GetValue(entry);
                var targets=new[]{customer.Agent.destination}.Concat(queue??Enumerable.Empty<Vector3>());
                var remaining=new System.Collections.Generic.List<Vector3>{customer.transform.position};
                foreach(var point in targets) { var path=adapter.GetCompletePath(remaining.Last(),point); Assert.That(path,Is.Not.Null); remaining.AddRange(path.Skip(1)); }
                TestContext.WriteLine("SIDE_DISPATCH native="+string.Join(";",native.Select(v=>v.ToString("F6")))+" customerRemaining="+string.Join(";",remaining.Select(v=>v.ToString("F6")))+" request="+request);
                Assert.That(CustomerRoutePlanner.IsClear(remaining,new[]{target.Position},2*actor.Settings.AgentRadius+actor.Settings.CollisionSkin+actor.Settings.Epsilon),Is.True,
                    "Do not reserve a new delivery side on an already-issued customer's real remaining route");
            }
        }
        NavigationActor Blocker(Vector3 position,string name)
        {
            Assert.That(adapter.IsPointWalkable(position),Is.True,"Fixture blocker must be on real owned NavMesh");
            Assert.That(world.RegisteredActors.All(a=>Vector3.Distance(a.transform.position,position)>.92f),Is.True,"Never overlap an existing body when creating the blocker");
            var prefab=CafeLoopRuntimeProof.Field<NavigationActor>(tools,"blockerPrefab");
            var body=UnityEngine.Object.Instantiate(prefab,position,Quaternion.identity,tools.transform);
            Assert.That(body.TryInitializeRuntimeId(name),Is.True); Assert.That(body.TryEnableSteadyPathMotion(),Is.True); Assert.That(world.Register(body),Is.True); return body;
        }
        [UnityTest] public IEnumerator BlockedPreferredSideUsesAlternativeWaitHeadAndCollectsBesideOccupiedDeliverySide()
        {
            var oldCustomer=CafeLoopRuntimeProof.Field<Vector3>(customers,"pickupCustomer");
            var obstruction=Blocker(oldCustomer,"side-original-customer-blocker");
            Assert.That(customers.TrySpawn(),Is.True,customers.Status); var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==id).CounterArrived);
            Assert.That(customers.TryReservePickup(id,out var reason),Is.True,"A body on the default side must not prevent safe alternative waiting geometry: "+reason);
            yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==id && o.State==OrderState.ReadyForPickup));
            var barista=world.RegisteredActors.Single(a=>a.ActorId==employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).ActorId);
            employees.TryGetBinding(EmployeeRole.Barista,out var binding); var delivery=CafeLoopRuntimeProof.Field<Vector3>(owner,"leasePosition");
            var path=adapter.GetCompletePath(barista.transform.position,binding.WorkTarget.Position);
            Assert.That(path,Is.Not.Null); var next=path.First(p=>Vector3.Distance(p,barista.transform.position)>.1f);
            var stop=Blocker(barista.transform.position+(next-barista.transform.position).normalized*1.02f,"side-return-blocker");
            var total=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token;
            tools.ResumeCollection(); yield return Until(()=>owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==id).State==OrderState.Completed,true);
            var ownedQueue=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue");
            Assert.That(ownedQueue.TryGetServiceLease(barista.ActorId,out var actualLease),Is.True); Assert.That(actualLease,Is.EqualTo(delivery));
            Assert.That(CafeLoopRuntimeProof.Field<string>(owner,"leaseActor"),Is.EqualTo(barista.ActorId));
            TestContext.WriteLine("SIDE_STILL_OWNED actualDistance="+Vector3.Distance(barista.transform.position,delivery)+" lease="+actualLease);
            Assert.That(Vector3.Distance(barista.transform.position,delivery),Is.LessThan(.95f),"Barista has not physically vacated the owned delivery side");
            var body=world.RegisteredActors.Single(a=>a.ActorId==id); var cup=body.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="CustomerHeldCup");
            Assert.That(Vector3.Distance(body.transform.position,delivery),Is.GreaterThan(.92f));
            Assert.That(Vector3.Distance(body.transform.position,oldCustomer),Is.GreaterThan(.92f),"Actual third-side collection, not the blocked original side");
            Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1));
            time.SetPaused(); yield return null; var paused=body.transform.position; for(var f=0;f<8;f++) yield return null;
            Assert.That(body.transform.position,Is.EqualTo(paused)); Assert.That(cup!=null,Is.True);
            var exit=CafeLoopRuntimeProof.Field<Transform>(customers,"exit"); var actualExit=false; var observing=true;
            var observer=body.gameObject.AddComponent<CafeActualExitObserver>(); observer.VisitId=id;
            observer.Receipt=(leaving,position)=>{if(!observing)return; Assert.That(Vector3.Distance(position,exit.position),Is.LessThanOrEqualTo(.081f)); Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,total)).State,Is.EqualTo(ReservationState.Released));actualExit=true;};
            try
            {
                world.Unregister(obstruction); obstruction.gameObject.SetActive(false); UnityEngine.Object.Destroy(obstruction.gameObject);
                world.Unregister(stop); stop.gameObject.SetActive(false); UnityEngine.Object.Destroy(stop.gameObject);
                time.SetNormal(); tools.ResumeBlockedFlow();
                yield return Until(()=>actualExit && employees.Snapshot.All(e=>e.State==EmployeeState.Idle)); yield return null;
                Assert.That(cup==null,Is.True); Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                TestContext.WriteLine("P14_PICKUP_SIDES actual alternative wait/third-side collect while delivery occupied/Pause/originalTotal/ownedCup/actualExit=True delivery="+delivery+" oldCustomer="+oldCustomer);
            }
            finally {observing=false;}
        }
        IEnumerator AddCounters(params GridPosition[] positions)
        {
            var decor=Find<DecorationModeController>(); var runtime=Find<CafeLayoutRuntime>(); decor.EnterDecorationMode(); yield return null;
            var asset=AssetDatabase.FindAssets("t:FurnitureDefinitionAsset").Select(g=>AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .Single(a=>a.DefinitionId=="furniture.counter.module.01");
            var session=CafeLoopRuntimeProof.Field<DecorationSession>(decor,"session");
            foreach(var position in positions)
            {
                Assert.That(decor.TrySelectCatalogueItem(new DecorationCatalogueItemModel(asset.DefinitionId,asset.DisplayName,null,DecorationCatalogueItemKind.Furniture,false,asset)),Is.True);
                session.MovePreview(position);
                var bar=CafeLoopRuntimeProof.Field<DecorationActionBarView>(decor,"actionBarView"); var button=CafeLoopRuntimeProof.Field<Button>(bar,"confirmButton");
                Assert.That(button.interactable,Is.True); button.onClick.Invoke();
                Assert.That(session.ActivePreview,Is.Null,"Formal Confirm must not bypass navigation/body guards"); yield return null;
            }
            runtime.RecalculateReadiness();
        }
        [UnityTest] public IEnumerator IsolatedDefaultNorthUsesConnectedEastSouthForActualFullOrder()
        {
            yield return AddCounters(new GridPosition(5,7),new GridPosition(7,7));
            var runtime=Find<CafeLayoutRuntime>(); var decor=Find<DecorationModeController>();
            Assert.That(runtime.CurrentReadiness.CanOpenForBusiness,Is.True,"An isolated old Employee North anchor must not veto connected alternative sides");
            decor.ExitDecorationMode(); yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            var pickup=runtime.FunctionalSurfaceLayout.PickUpPoints.Single(); employees.TryGetBinding(EmployeeRole.Barista,out var binding);
            var all=customers.GetPickupServiceTargets(pickup.InstanceId);
            var grid=CafeLoopRuntimeProof.Field<Transform>(customers,"gridRoot");
            var north=grid.TransformPoint(new Vector3(6.5f,0,7.5f));
            Assert.That(runtime.Layout.TryGetOccupant(new GridPosition(6,7),out _),Is.False,"The old default side is still exposed");
            Assert.That(adapter.HasClearPath(binding.WorkTarget.Position,north),Is.False);
            Assert.That(all.Any(t=>adapter.HasClearPath(binding.WorkTarget.Position,t.Position) && adapter.HasClearPath(t.Position,binding.WorkTarget.Position)),Is.True);
            TestContext.WriteLine("SIDE_STATIC_ISLAND north="+north+" sampled="+adapter.IsPointWalkable(north)+" selected="+binding.PickUpTarget.Value.Position+" canOpen="+runtime.CurrentReadiness.CanOpenForBusiness+" nav="+adapter.CurrentReadiness.CanResume);
            yield return Until(()=>customers.Snapshot.Count>0 || customers.TrySpawn()); tools.ResumeCollection();
            yield return Until(()=>owner.Snapshot.Orders.Count==1 && owner.Snapshot.Orders.Values.Single().State==OrderState.Completed && customers.Capacity.GetCapacities().All(c=>c.Used==0) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
        }
        [UnityTest] public IEnumerator TwoStaticSidesWithOneOccupiedKeepWaiterOffTheOnlyDeliverySide()
        {
            yield return AddCounters(new GridPosition(6,7),new GridPosition(7,6)); var decor=Find<DecorationModeController>(); decor.ExitDecorationMode();
            yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            var pickup=Find<CafeLayoutRuntime>().FunctionalSurfaceLayout.PickUpPoints.Single(); var sides=customers.GetPickupServiceTargets(pickup.InstanceId);
            Assert.That(sides.Count,Is.EqualTo(2)); var south=sides.Single(t=>t.Facing.HasValue && Vector3.Dot(t.Facing.Value,Vector3.forward)>.9f);
            var block=Blocker(south.Position,"two-side-south-blocker"); yield return null; yield return Until(()=>customers.Snapshot.Count>0 || customers.TrySpawn());
            var id=customers.Snapshot.Single().VisitId; yield return Until(()=>owner.Snapshot.Orders.Count==1 && owner.Snapshot.Orders.Values.Single().State==OrderState.ReadyForPickup);
            var wait=customers.PickupAssignments.Single(); Assert.That(sides.All(t=>Vector3.Distance(t.Position,wait.Position)>.99f),Is.True,"Preparing waiter cannot preoccupy the sole remaining delivery side");
            tools.ResumeCollection(); yield return Until(()=>owner.Snapshot.Orders.Values.Single().State==OrderState.Completed);
            world.Unregister(block); block.gameObject.SetActive(false); UnityEngine.Object.Destroy(block.gameObject);
            yield return Until(()=>customers.Capacity.GetCapacities().All(c=>c.Used==0) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
        }
        [UnityTest] public IEnumerator SingleSideQueuePreservesActualDeliveryAndReturnCorridor()
        {
            // 真实装修建单入口aisle；顾客可从开放左厅进入，唯一送杯腿经过旧wait head。
            // No injected routes or repositioned actors: both staff and customer use native movement.
            yield return AddCounters(new GridPosition(1,6),new GridPosition(4,5),new GridPosition(5,5),
                new GridPosition(6,5),new GridPosition(4,7),new GridPosition(5,7),new GridPosition(6,7));
            var runtime=Find<CafeLayoutRuntime>(); var decor=Find<DecorationModeController>();
            var cm=runtime.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
            var support=runtime.Layout.FurnitureInstances.Single(f=>f.Position.Equals(new GridPosition(1,6)));
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,cm.InstanceId),Is.True);
            Assert.That(decor.TryMoveFunctionalSurfacePreview(new SurfaceSlotAddress(support.InstanceId,cm.Address.SlotId)),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True);
            Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);
            decor.ExitDecorationMode();
            yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            employees.TryGetBinding(EmployeeRole.Barista,out var binding);
            var pickup=runtime.FunctionalSurfaceLayout.PickUpPoints.Single(); var sides=customers.GetPickupServiceTargets(pickup.InstanceId);
            Assert.That(sides.Count,Is.EqualTo(1));
            var outgoing=adapter.GetCompletePath(binding.WorkTarget.Position,sides[0].Position);
            var returning=adapter.GetCompletePath(sides[0].Position,binding.WorkTarget.Position);
            Assert.That(outgoing,Is.Not.Null); Assert.That(returning,Is.Not.Null);
            TestContext.WriteLine("CORRIDOR_EMPTY work="+binding.WorkTarget.Position.ToString("F6")+" side="+sides[0].Position.ToString("F6")+
                " native="+string.Join(";",outgoing.Select(p=>p.ToString("F6"))));
            WriteMatrixRecipe("L-06",0,"BUILT_FORMAL_CORRIDOR");
            Assert.That(customers.TrySpawn(),Is.True,customers.Status); var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>owner.Snapshot.Orders.Count==1);
            var original=customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray();
            var cashierFinished=0; var prepared=0;
            Action<CafeTaskEvent> count=e=>
            {
                if(e.Checkpoint.VisitId!=id)return;
                if(e.Role==EmployeeRole.Cashier && e.Kind==CafeTaskEventKind.ServiceFinished)cashierFinished++;
                if(e.Role==EmployeeRole.Barista && e.Kind==CafeTaskEventKind.Prepared)prepared++;
            };
            employees.IntegratedTaskEvent+=count;
            try
            {
                for(var frame=0;frame<1800 && !(owner.Snapshot.Orders.Values.Single().State==OrderState.ReadyForPickup &&
                    employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State==EmployeeState.Idle);frame++) yield return null;
                var staff=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);
                var waits=customers.PickupAssignments.Select(a=>a.Position).ToArray();
                TestContext.WriteLine("CORRIDOR_ACTUAL order="+owner.Snapshot.Orders.Values.Single().State+" employee="+staff.State+" failure="+staff.Failure+
                    " queue="+string.Join(";",waits.Select(p=>p.ToString("F6")))+" originalNativeClear="+CustomerRoutePlanner.IsClear(outgoing,waits,.911f));
                Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.ReadyForPickup),
                    "A serviceable layout must choose safe waiting space and actually deliver; not silently wait forever behind its owned waiting customer");
                Assert.That(staff.State,Is.EqualTo(EmployeeState.Idle),"The same validated corridor must also actually return to CM");
                var barista=world.RegisteredActors.Single(a=>a.ActorId==staff.ActorId);
                Assert.That(Vector3.Distance(barista.transform.position,binding.WorkTarget.Position),Is.LessThanOrEqualTo(.081f));
                var wait=customers.PickupAssignments.Single(); var customer=world.RegisteredActors.Single(a=>a.ActorId==id);
                Assert.That(wait.Arrived,Is.True); Assert.That(Vector3.Distance(customer.transform.position,wait.Position),Is.LessThanOrEqualTo(.081f));
                var visit=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits")[id];
                Assert.That((long)visit.GetType().GetField("RequestId").GetValue(visit),Is.Zero);
                Assert.That(prepared,Is.EqualTo(1));
                tools.ResumeCollection();
                yield return Until(()=>owner.Snapshot.Orders.Values.Single().State==OrderState.Completed &&
                    customers.Capacity.GetCapacities().All(c=>c.Used==0) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                CollectionAssert.AreEqual(original,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
                Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1));
                WriteMatrixRecipe("L-06",0,"PASS");
            }
            finally { employees.IntegratedTaskEvent-=count; }
        }

        [UnityTest] public IEnumerator SingleSidePaidOwnerActuallyYieldsThroughDeliveryReturnThenCollectsWithOriginalTokens()
        { yield return ActualPaidYield(false); }
        [UnityTest] public IEnumerator SingleSideHeldReadyStartsSafeCollectionBeforeActualMachineReturn()
        { yield return ActualPaidYield(true); }
        [UnityTest] public IEnumerator SingleSideActualYieldPauseDisableResumeKeepsPaidBodyAndTokens()
        { yield return ActualPaidYield(false,1); }
        [UnityTest] public IEnumerator SingleSideChangedDoneDuringActualYieldRetiresOriginalPaidVisit()
        { yield return ActualPaidYield(false,2); }
        [UnityTest] public IEnumerator SingleSideRelatedCounterWorkingIsTemporaryDeliveryTraffic()
        {yield return CounterWorkingTraffic(false);}
        [UnityTest] public IEnumerator SingleSideDenseStationaryCorridorBlocksPreparedThenExplicitlyRecovers()
        {yield return CounterWorkingTraffic(true);}
        private IEnumerator CounterWorkingTraffic(bool keepStationaryWall)
        {
            var fees=0;var preparations=0;var deliveries=0;
            NavigationTarget[] selectedReturn=null;
            employees.IntegratedTaskEvent+=e=>{if(e.Kind==CafeTaskEventKind.ServiceFinished)fees++;if(e.Kind==CafeTaskEventKind.Prepared)preparations++;if(e.Kind==CafeTaskEventKind.DeliveryArrived)
                {deliveries++;var runner=CafeLoopRuntimeProof.Field<System.Collections.IEnumerable>(employees,"employees").Cast<object>().Select(v=>CafeLoopRuntimeProof.Field<EmployeeTaskRunner>(v,"Runner")).Single(r=>r!=null && r.Snapshot.Role==EmployeeRole.Barista);
                    selectedReturn=CafeLoopRuntimeProof.Field<NavigationTarget[]>(runner,"returnRoute")?.ToArray();}
                if(e.Kind==CafeTaskEventKind.Blocked && e.Checkpoint.Stage==CafeWorkStage.Returning && selectedReturn!=null)
                {
                    var actor=world.RegisteredActors.Single(a=>a.ActorId==e.ActorId);var closest=Enumerable.Range(0,selectedReturn.Length).OrderBy(i=>Vector3.Distance(actor.transform.position,selectedReturn[i].Position)).First();
                    var next=selectedReturn[Math.Min(closest+1,selectedReturn.Length-1)];var native=adapter.GetCompletePath(actor.transform.position,next.Position);
                    TestContext.WriteLine("RETURN_REFUSED actual="+actor.transform.position.ToString("F6")+" route="+string.Join(";",selectedReturn.Select(t=>t.Position.ToString("F6")))+" next="+next.Position.ToString("F6")+" native="+string.Join(";",native??Array.Empty<Vector3>()));
                    foreach(var other in world.RegisteredActors.Where(a=>a!=actor))TestContext.WriteLine("RETURN_BODY "+other.ActorId+" actual="+other.transform.position.ToString("F6")+" clear="+CustomerRoutePlanner.IsClear(native,new[]{other.transform.position},.911f)+" moving="+(other.Agent.hasPath && !other.Agent.isStopped)+" target="+other.Agent.destination.ToString("F6"));
                }};
            var decor=Find<DecorationModeController>();var runtime=Find<CafeLayoutRuntime>();decor.EnterDecorationMode();yield return null;
            var machine=runtime.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
            var support=runtime.Layout.FurnitureInstances.Single(f=>f.InstanceId==machine.Address.SupportFurnitureInstanceId);
            var session=CafeLoopRuntimeProof.Field<DecorationSession>(decor,"session");
            Assert.That(session.BeginExisting(support.InstanceId).Succeeded,Is.True);Assert.That(session.MovePreview(new GridPosition(6,0)).Succeeded,Is.True);
            yield return MatrixConfirmFurniture();
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,machine.InstanceId),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True);Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);decor.ExitDecorationMode();
            yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));yield return null;
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);var first=customers.Snapshot.Single().VisitId;
            yield return Until(()=>owner.Snapshot.Orders.Count==1);employees.enabled=false;
            yield return Until(()=>customers.PickupAssignments.Single().Arrived,true);
            employees.enabled=true;tools.ResumeBlockedFlow();yield return Until(()=>owner.CanProgress && employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State==EmployeeState.Working,true);yield return null;
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);var second=customers.Snapshot.Single(v=>v.VisitId!=first).VisitId;
            var originalTokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();
            yield return Until(()=>employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).RemainingWorkSeconds<.5f);
            Assert.That(customers.CafeSnapshot.Single(v=>v.VisitId==second).CounterArrived,Is.False,"Second guest must still be physically approaching before the controlled component hold");
            employees.enabled=false;yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==second).CounterArrived,true);
            employees.enabled=true;tools.ResumeBlockedFlow();
            yield return Until(()=>employees.Snapshot.All(e=>e.State==EmployeeState.Working),true);
            var barriers=new System.Collections.Generic.List<NavigationActor>();
            foreach(var x in new[]{-.5f,.5f,1.5f,2.5f,3.5f})barriers.Add(Blocker(new Vector3(x,0,-1.5f),"counter-working-corridor-"+x));
            var firstKey=owner.Snapshot.Orders.Single().Key;var waitingObserved=false;
            for(var frame=0;frame<300 && employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).State==EmployeeState.Working;frame++)
            {
                var staff=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);
                Assert.That(staff.State,Is.Not.EqualTo(EmployeeState.Blocked),"Related real Cashier Working has finite progress and reserved pickup; must not become permanent Prepared failure: "+staff.Failure);
                if(staff.State==EmployeeState.WaitingToDeliver)
                {
                    waitingObserved=true;var body=world.RegisteredActors.Single(a=>a.ActorId==second);
                    Assert.That(customers.CounterAssignments.Single(a=>a.VisitId==second).Arrived,Is.True);
                    Assert.That(owner.Snapshot.PaidVisitIds.Contains(second),Is.False);
                    var visit=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits")[second];
                    Assert.That((bool)visit.GetType().GetField("PickupAssigned").GetValue(visit),Is.True);
                    Assert.That((bool)visit.GetType().GetField("PickupInTransit").GetValue(visit),Is.False);
                    Assert.That(customers.Capacity.GetReservations().Single(r=>r.OwnerId==second && r.Kind==CapacityKind.PickUp).State,Is.EqualTo(ReservationState.Reserved));
                    if(frame%60==0)TestContext.WriteLine("COUNTER_WORKING_TRAFFIC actual="+body.transform.position+" cashierRemaining="+employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).RemainingWorkSeconds+" barista="+staff.State);
                }
                yield return null;
            }
            Assert.That(waitingObserved,Is.True,"The finite static Working window must really obstruct a Prepared delivery");
            var departed=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits")[second];var departing=world.RegisteredActors.Single(a=>a.ActorId==second);
            TestContext.WriteLine("COUNTER_FINISHED_TRAFFIC state="+customers.Snapshot.Single(v=>v.VisitId==second).State+" request="+departed.GetType().GetField("RequestId").GetValue(departed)+
                " pending="+departed.GetType().GetField("PendingPickupSegment").GetValue(departed)+" agentStopped="+departing.Agent.isStopped+" pendingPath="+departing.Agent.pathPending+" hasPath="+departing.Agent.hasPath+" target="+departing.Agent.destination+
                " route="+string.Join(";",(System.Collections.Generic.Queue<Vector3>)departed.GetType().GetField("Route").GetValue(departed)??new System.Collections.Generic.Queue<Vector3>()));
            // These registered bodies create only the controlled CR traffic window.
            // Retire them after that same paid body really vacates the Counter region;
            // a permanent artificial wall is a separate bounded-routing geometry.
            yield return Until(()=>customers.CounterAssignments.All(a=>a.VisitId!=second));
            Assert.That(owner.Snapshot.PaidVisitIds.Contains(second),Is.True);Assert.That(Vector3.Distance(departing.transform.position,new Vector3(-3.5f,0,-1.5f)),Is.GreaterThan(.97f));
            if(keepStationaryWall)
            {
                yield return Until(()=>employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State==EmployeeState.Blocked,true);
                Assert.That(owner.Snapshot.Orders[firstKey].State,Is.EqualTo(OrderState.Preparing));Assert.That(owner.Snapshot.Delivery,Is.Empty);
                Assert.That(owner.Snapshot.Employees.Single(e=>e.Role==EmployeeRole.Barista).Stage,Is.EqualTo(CafeWorkStage.Prepared));
                Assert.That(employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).Failure,Does.Contain("送杯通道堵塞"));
                WriteMatrixRecipe("L-14",1,"EXPECTED_BOUNDED_BLOCKED_WITH_STATIONARY_WALL");
            }
            foreach(var blocker in barriers){world.Unregister(blocker);blocker.gameObject.SetActive(false);UnityEngine.Object.Destroy(blocker.gameObject);}
            if(keepStationaryWall)tools.ResumeBlockedFlow();
            yield return Until(()=>owner.Snapshot.Orders[firstKey].State==OrderState.ReadyForPickup && employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State==EmployeeState.Idle);
            tools.ResumeCollection();yield return Until(()=>owner.Snapshot.Orders.Count==2 && owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed) && customers.Capacity.GetCapacities().All(c=>c.Used==0) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            WriteMatrixRecipe(keepStationaryWall?"L-14":"L-12",keepStationaryWall?1:0,"PASS_WITH_CONTROLLED_REGISTERED_CORRIDOR");
            Assert.That(fees,Is.EqualTo(2));Assert.That(preparations,Is.EqualTo(2));Assert.That(deliveries,Is.EqualTo(2));
            CollectionAssert.AreEqual(originalTokens,customers.Capacity.GetReservations().Select(r=>r.Token));
        }
        IEnumerator ActualPaidYield(bool collectBeforeMachineReturn,int lifecycle=0)
        {
            var paid=0;var prepared=0;
            employees.IntegratedTaskEvent+=e=>{if(e.Kind==CafeTaskEventKind.ServiceFinished)paid++;if(e.Kind==CafeTaskEventKind.Prepared)prepared++;};
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>owner.Snapshot.Orders.Count==1);
            var key=owner.Snapshot.Orders.Single().Key;
            var tokens=customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray();
            // Hold only the real employee component while the already-paid customer finishes its real route.
            employees.enabled=false;yield return Until(()=>customers.PickupAssignments.Single().Arrived,true);
            var body=world.RegisteredActors.Single(a=>a.ActorId==id);var original=customers.PickupAssignments.Single();
            Assert.That(Vector3.Distance(body.transform.position,new Vector3(1.4f,0,2.5f)),Is.LessThanOrEqualTo(.081f));
            WriteMatrixRecipe(lifecycle==0?"L-13":"L-15",lifecycle,"CONTROLLED_REGISTERED_BODIES");
            // Controlled physical fixture: only real registered bodies; no structural revision or slot injection.
            var controlled=new System.Collections.Generic.List<NavigationActor>();
            foreach(var z in new[]{-3.5f,-2.5f,-1.5f,-.5f,.5f,1.5f,3.5f})
                controlled.Add(Blocker(new Vector3(.35f,0,z),"yield-column-"+z));
            foreach(var x in new[]{1.35f,2.35f})
            {
                controlled.Add(Blocker(new Vector3(x,0,3.5f),"yield-upper-"+x));
                if(x<2) controlled.Add(Blocker(new Vector3(x,0,-.5f),"yield-lower-"+x)); // CM's real support already closes the remaining lower edge.
            }
            controlled.Add(Blocker(new Vector3(2.5f,0,1.52f),"actual-delivery-detour-obstruction"));
            Action clearControlled=()=>{foreach(var external in controlled){world.Unregister(external);external.gameObject.SetActive(false);UnityEngine.Object.Destroy(external.gameObject);}controlled.Clear();};
            employees.enabled=true;yield return Until(()=>owner.CanProgress,true);tools.ResumeBlockedFlow();
            yield return Until(()=>employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State==EmployeeState.Working,true);
            TestContext.WriteLine("OWNER_YIELD_STARTED canProgress="+owner.CanProgress+" revision="+adapter.Revision+" published="+adapter.PublishedRevision+
                " speed="+time.CurrentSpeed+" block="+time.ResumeBlockReason+" staff="+employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State);
            var actualYield=false;var actualReturn=false;var issuedBeforeReturn=false;var lifecycleObserved=false;
            for(var frame=0;frame<3000 && !actualReturn;frame++)
            {
                var staff=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);
                Assert.That(staff.State,Is.Not.EqualTo(EmployeeState.Blocked),staff.Failure);
                Assert.That(customers.Snapshot.Single(v=>v.VisitId==id).State,Is.Not.EqualTo(CustomerVisitState.Blocked),customers.Status);
                var yielding=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits")[id];
                if(lifecycle!=0 && !lifecycleObserved && (int)yielding.GetType().GetField("DeliveryYieldPhase").GetValue(yielding)==1 &&
                    (long)yielding.GetType().GetField("RequestId").GetValue(yielding)>0 && Vector3.Distance(body.transform.position,original.Position)>.1f)
                {
                    lifecycleObserved=true;var request=(long)yielding.GetType().GetField("RequestId").GetValue(yielding);
                    var generation=(int)yielding.GetType().GetField("Generation").GetValue(yielding);
                    time.SetPaused();var paused=body.transform.position;for(var f=0;f<8;f++)yield return null;
                    Assert.That(body.transform.position,Is.EqualTo(paused));Assert.That((int)yielding.GetType().GetField("Generation").GetValue(yielding),Is.EqualTo(generation));
                    if(lifecycle==1)
                    {
                        customers.enabled=false;yield return null;
                        Assert.That(world.Service.Cancel(request),Is.False,"Disable must already retire the owned live yield request");
                        Assert.That(world.RegisteredActors.Single(a=>a.ActorId==id),Is.SameAs(body));Assert.That(body.transform.position,Is.EqualTo(paused));
                        CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
                        customers.enabled=true;time.SetNormal();tools.ResumeBlockedFlow();
                    }
                    else
                    {
                        var beforeEpoch=owner.Snapshot.Epoch;var decor=Find<DecorationModeController>();decor.EnterDecorationMode();
                        var cm=Find<CafeLayoutRuntime>().FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
                        Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,cm.InstanceId),Is.True);
                        Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True);Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);
                        clearControlled();decor.ExitDecorationMode();yield return null;
                        Assert.That(owner.Snapshot.Epoch,Is.EqualTo(beforeEpoch+1));Assert.That(owner.Snapshot.Orders,Is.Empty);
                        Assert.That(customers.Snapshot,Is.Empty);Assert.That(world.Service.Cancel(request),Is.False);
                        Assert.That(world.RegisteredActors.Any(a=>a.ActorId==id),Is.False);Assert.That(body==null,Is.True);
                        Assert.That(customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).All(r=>r.State==ReservationState.Released),Is.True);
                        Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));time.SetNormal();tools.ResumeBlockedFlow();
                        yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                        Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);Assert.That(owner.Snapshot.Orders,Is.Empty);
                        TestContext.WriteLine("YIELD_LIFECYCLE changedDone retired actual request="+request+" generation="+generation+" oldVisit="+id);yield break;
                    }
                }
                if(Vector3.Distance(body.transform.position,original.Position)>.9f) actualYield=true;
                if(collectBeforeMachineReturn && !issuedBeforeReturn && staff.State==EmployeeState.Returning && owner.Snapshot.Orders[key].State==OrderState.ReadyForPickup)
                {
                    var queue=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue");
                    var returning=world.RegisteredActors.Single(a=>a.ActorId==staff.ActorId);
                    var service=customers.GetPickupServiceTargets(Find<CafeLayoutRuntime>().FunctionalSurfaceLayout.PickUpPoints.Single().InstanceId).Single();
                    var path=adapter.GetCompletePath(body.transform.position,service.Position);
                    var clearance=2*body.Settings.AgentRadius+body.Settings.CollisionSkin+body.Settings.Epsilon+2*body.Settings.MaxSpeed*Time.deltaTime;
                    if(!queue.TryGetServiceLease(staff.ActorId,out _) && CustomerRoutePlanner.IsClear(path,world.RegisteredActors.Where(a=>a!=body).Select(a=>a.transform.position).ToArray(),clearance))
                    {
                        var runner=CafeLoopRuntimeProof.Field<System.Collections.IEnumerable>(employees,"employees").Cast<object>()
                            .Select(e=>CafeLoopRuntimeProof.Field<EmployeeTaskRunner>(e,"Runner")).Single(r=>r!=null && r.Snapshot.Role==EmployeeRole.Barista);
                        var route=CafeLoopRuntimeProof.Field<NavigationTarget[]>(runner,"returnRoute");
                        var index=CafeLoopRuntimeProof.Field<int>(runner,"returnRouteIndex");
                        var remaining=new System.Collections.Generic.List<Vector3>{returning.transform.position};
                        foreach(var point in new[]{returning.Agent.destination}.Concat(route.Skip(index+1).Select(t=>t.Position)))
                        { var native=adapter.GetCompletePath(remaining.Last(),point);Assert.That(native,Is.Not.Null);remaining.AddRange(native.Skip(1)); }
                        var conflict=(bool)typeof(CustomerFlowController).GetMethod("RoutesConflict",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)
                            .Invoke(null,new object[]{path,remaining,clearance});
                        if(!conflict)
                        {
                            var privateVisit=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits")[id];
                            Assert.That((int)privateVisit.GetType().GetField("DeliveryYieldPhase").GetValue(privateVisit),Is.EqualTo(2));
                            employees.TryGetBinding(EmployeeRole.Barista,out var beforeReturnBinding);
                            Assert.That(Vector3.Distance(returning.transform.position,beforeReturnBinding.WorkTarget.Position),Is.GreaterThan(.081f));
                            Assert.That(customers.TryMoveToPickupService(id,out var why),Is.True,"A held Ready head with a fully safe collection route must not wait for CM: "+why);
                            issuedBeforeReturn=true;clearControlled();tools.ResumeCollection();
                        }
                    }
                }
                actualReturn=(owner.Snapshot.Orders[key].State==OrderState.ReadyForPickup || owner.Snapshot.Orders[key].State==OrderState.Completed) && staff.State==EmployeeState.Idle;
                yield return null;
            }
            Assert.That(actualYield,Is.True,"The actual paid visit must really yield; returned="+actualReturn+" order="+owner.Snapshot.Orders[key].State+" staff="+employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State+" failure="+owner.Snapshot.Failure);
            Assert.That(actualReturn,Is.True,owner.Snapshot.Failure);
            Assert.That(lifecycle==0 || lifecycleObserved,Is.True,"Lifecycle must occur during a real owned yield request");
            Assert.That(!collectBeforeMachineReturn || issuedBeforeReturn,Is.True,"Real safe heldReady/Returning window must be established");
            employees.TryGetBinding(EmployeeRole.Barista,out var binding);var barista=world.RegisteredActors.Single(a=>a.ActorId==employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).ActorId);
            Assert.That(Vector3.Distance(barista.transform.position,binding.WorkTarget.Position),Is.LessThanOrEqualTo(.081f));
            if(!collectBeforeMachineReturn)
            {
                yield return Until(()=>customers.PickupAssignments.Single().Arrived && Vector3.Distance(body.transform.position,original.Position)<=.081f);
                Assert.That(customers.PickupAssignments.Single().SlotIndex,Is.EqualTo(original.SlotIndex));
            }
            Assert.That(paid,Is.EqualTo(1));Assert.That(prepared,Is.EqualTo(1));
            TestContext.WriteLine("OWNER_ACTUAL_YIELD true actualReturn=true earlyCollection="+issuedBeforeReturn+" original="+original.Position.ToString("F6")+" returned="+body.transform.position.ToString("F6"));
            clearControlled();
            tools.ResumeCollection();yield return Until(()=>owner.Snapshot.Orders[key].State==OrderState.Completed && customers.Capacity.GetCapacities().All(c=>c.Used==0));
            CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
            Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator SingleSideStationaryObstructionReportsPreparedBlockedAndExplicitlyRecovers()
        {
            var paid=0; var prepared=0;
            Action<CafeTaskEvent> count=e=>{if(e.Kind==CafeTaskEventKind.ServiceFinished)paid++;if(e.Kind==CafeTaskEventKind.Prepared)prepared++;};
            employees.IntegratedTaskEvent+=count;
            try
            {
                Assert.That(customers.TrySpawn(),Is.True,customers.Status); var id=customers.Snapshot.Single().VisitId;
                yield return Until(()=>owner.Snapshot.Orders.Count==1);
                var tokens=customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray();
                var side=customers.GetPickupServiceTargets(Find<CafeLayoutRuntime>().FunctionalSurfaceLayout.PickUpPoints.Single().InstanceId).Single();
                var obstruction=Blocker(side.Position,"stationary-delivery-obstruction");
                yield return Until(()=>employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State==EmployeeState.WaitingToDeliver ||
                    employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State==EmployeeState.Blocked,true);
                for(var frame=0;frame<180 && employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State!=EmployeeState.Blocked;frame++)yield return null;
                var staff=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);
                Assert.That(staff.State,Is.EqualTo(EmployeeState.Blocked),"Stationary no-space cannot remain silently WaitingToDeliver forever");
                Assert.That(staff.Failure,Does.Contain("送杯通道堵塞")); Assert.That(owner.Snapshot.Failure,Does.Contain("送杯通道堵塞"));
                Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.Preparing)); Assert.That(owner.Snapshot.Delivery,Is.Empty);
                Assert.That(employees.TryCaptureOrderCheckpoint(EmployeeRole.Barista,out var checkpoint),Is.True);
                Assert.That(checkpoint.Stage,Is.EqualTo(CafeWorkStage.Prepared)); Assert.That(checkpoint.RemainingWorkSeconds,Is.Zero);
                Assert.That(customers.Capacity.GetCapacities()[0].Used,Is.EqualTo(1));
                Assert.That(customers.Capacity.GetCapacities()[2].Used,Is.EqualTo(1));
                for(var frame=0;frame<20;frame++)yield return null;
                Assert.That(employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State,Is.EqualTo(EmployeeState.Blocked));
                world.Unregister(obstruction); obstruction.gameObject.SetActive(false); UnityEngine.Object.Destroy(obstruction.gameObject);
                tools.ResumeBlockedFlow(); tools.ResumeCollection();
                yield return Until(()=>owner.Snapshot.Orders.Values.Single().State==OrderState.Completed && customers.Capacity.GetCapacities().All(c=>c.Used==0) &&
                    employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                Assert.That(paid,Is.EqualTo(1)); Assert.That(prepared,Is.EqualTo(1)); Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1));
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
            }
            finally {employees.IntegratedTaskEvent-=count;}
        }

        [UnityTest] public IEnumerator SingleSideWaitsUntilTheEmployeeActuallyVacatesThenCollects()
        {
            var pickup=Find<CafeLayoutRuntime>().FunctionalSurfaceLayout.PickUpPoints.Single(); Assert.That(customers.GetPickupServiceTargets(pickup.InstanceId).Count,Is.EqualTo(1));
            Assert.That(customers.TrySpawn(),Is.True,customers.Status); var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>owner.Snapshot.Orders.Count==1 && owner.Snapshot.Orders.Values.Single().State==OrderState.ReadyForPickup);
            var barista=world.RegisteredActors.Single(a=>a.ActorId==employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).ActorId);
            var delivery=CafeLoopRuntimeProof.Field<Vector3>(owner,"leasePosition"); employees.TryGetBinding(EmployeeRole.Barista,out var binding);
            var path=adapter.GetCompletePath(barista.transform.position,binding.WorkTarget.Position); var next=path.First(p=>Vector3.Distance(p,barista.transform.position)>.1f);
            var block=Blocker(barista.transform.position+(next-barista.transform.position).normalized*1.02f,"one-side-return-blocker"); tools.ResumeCollection();
            yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==id).NavigationState==CustomerVisitState.PickupWaiting,true);
            for(var i=0;i<30;i++) yield return null;
            Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.ReadyForPickup));
            Assert.That(customers.CafeSnapshot.Single(v=>v.VisitId==id).NavigationState,Is.EqualTo(CustomerVisitState.PickupWaiting));
            var queue=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue"); Assert.That(queue.TryGetServiceLease(barista.ActorId,out var held),Is.True); Assert.That(held,Is.EqualTo(delivery));
            world.Unregister(block); block.gameObject.SetActive(false); UnityEngine.Object.Destroy(block.gameObject);
            if(employees.Snapshot.Any(e=>e.State==EmployeeState.Blocked)) tools.ResumeBlockedFlow();
            yield return Until(()=>owner.Snapshot.Orders.Values.Single().State==OrderState.Completed && customers.Capacity.GetCapacities().All(c=>c.Used==0) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            matrixObserving=false;
            if(scene.IsValid() && scene.isLoaded) { SceneManager.SetActiveScene(SceneManager.CreateScene("P14 sides cleanup")); yield return SceneManager.UnloadSceneAsync(scene); }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputs??Array.Empty<UnityEngine.InputSystem.InputActionAsset>()); Time.captureDeltaTime=capture; Time.timeScale=scale;
        }
    }
}
#endif

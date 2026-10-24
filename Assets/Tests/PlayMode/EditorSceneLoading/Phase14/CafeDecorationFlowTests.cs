#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.CafeLoop;
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
    public sealed class CafeDecorationFlowTests
    {
        private Scene scene;
        private CustomerFlowController customers;
        private EmployeeFlowController employees;
        private CafeLoopController owner;
        private PickupCupDisplay cups;
        private GameTimeService time;
        private NavigationWorld world;
        private NavigationLayoutAdapter adapter;
        private DecorationModeController decor;
        private CafeLayoutRuntime layout;
        private UnityEngine.InputSystem.InputActionAsset[] inputs;
        private float capture,scale;
        private const string Machine="12000000000000000000000000000005";
        [UnitySetUp] public IEnumerator Setup()
        {
            capture=Time.captureDeltaTime; scale=Time.timeScale; Time.captureDeltaTime=1f/60;
            var initial=TestContext.CurrentContext.Test.Name.StartsWith("Initial");
            scene=EditorSceneManager.LoadSceneInPlayMode(initial?"Assets/Scenes/MainCafe.unity":"Assets/Scenes/Validation/Phase12CustomerQueue.unity",
                new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
            customers=Find<CustomerFlowController>(); customers.AutoSpawn=false; world=Find<NavigationWorld>();
            adapter=Find<NavigationLayoutAdapter>(); time=Find<GameTimeService>(); layout=Find<CafeLayoutRuntime>(); decor=Find<DecorationModeController>();
            inputs=Phase8SceneInputTestCleanup.CaptureAssets(scene);
            if(!initial) for(var f=0;f<120 && string.IsNullOrEmpty(customers.SelectedRegisterId);f++) yield return null;
            GameObject host=null;
            if(!initial) { host=new GameObject("Task7 connected owner fixture"); SceneManager.MoveGameObjectToScene(host,scene); }
            employees=All<EmployeeFlowController>().SingleOrDefault(); cups=All<PickupCupDisplay>().SingleOrDefault();
            if(employees==null)
            {
                cups=host.AddComponent<PickupCupDisplay>(); employees=host.AddComponent<EmployeeFlowController>();
                var so=new SerializedObject(employees); so.FindProperty("cupPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase4/Prefabs/PF_Item_CeramicCup_01.prefab"); so.ApplyModifiedPropertiesWithoutUndo();
                employees.Configure(world,adapter,layout,decor,time,customers,Ref<Transform>(decor,"gridRoot"),Prefab("Shiba"),Prefab("Westie"),cups);
            }
            if(initial) { Assert.That(All<CafeLoopController>().Length,Is.EqualTo(1)); owner=Find<CafeLoopController>(); }
            else { owner=host.AddComponent<CafeLoopController>(); owner.Configure(customers,employees,cups,time,adapter,decor); }
            if(initial) { yield return null; yield return null; }
            else { Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True,time.ResumeBlockReason); yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle)); }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(owner!=null) Object.Destroy(owner.gameObject); yield return null;
            if(scene.IsValid() && scene.isLoaded)
            { SceneManager.SetActiveScene(SceneManager.CreateScene("Task7 cleanup")); yield return SceneManager.UnloadSceneAsync(scene); }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputs ?? Array.Empty<UnityEngine.InputSystem.InputActionAsset>());
            Time.captureDeltaTime=capture; Time.timeScale=scale;
        }
        [UnityTest] public IEnumerator InitialMainCafeAutomaticSetupPauseValidDoneStartsWithoutPlay()
        {
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); Assert.That(time.ExplicitChoiceVersion,Is.Zero);
            Assert.That(layout.CurrentReadiness.CanOpenForBusiness,Is.False);
            decor.EnterDecorationMode(); Seed(); decor.RefreshConfirmedNavigationPresentation(); Rotate(); decor.ExitDecorationMode();
            for(var f=0;f<180;f++) yield return null;
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Normal),"Valid changed Done must leave initial automatic setup Pause without pressing Play | "+owner.Snapshot.Status+" | "+owner.Snapshot.Failure+" | "+time.ResumeBlockReason+" | nav="+adapter.CurrentReadiness.Reason+" published="+adapter.PublishedRevision+"/"+adapter.Revision);
            Assert.That(time.ExplicitChoiceVersion,Is.Zero); Assert.That(time.IsResumeBlocked,Is.False,time.ResumeBlockReason);
            Assert.That(owner.CanProgress,Is.True); customers.AutoSpawn=true;
            yield return Until(()=>customers.Snapshot.Count>0); customers.AutoSpawn=false;
        }
        [UnityTest] public IEnumerator InitialMainCafeExplicitPauseAtAlreadyPausedSetupSurvivesDone()
        {
            Assert.That(time.TrySetSpeed(GameSpeed.Paused),Is.True);
            decor.EnterDecorationMode(); Seed(); decor.RefreshConfirmedNavigationPresentation(); Rotate(); decor.ExitDecorationMode();
            for(var f=0;f<12;f++) yield return null;
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); Assert.That(owner.CanProgress,Is.False);
            Assert.That(time.LastExplicitSpeed,Is.EqualTo(GameSpeed.Paused));
        }
        [UnityTest] public IEnumerator PreparingGateRetiresOldCallbacksBeforeNavigationAndRelease()
        {
            CafeTaskEvent progress=default; employees.IntegratedTaskEvent+=e=>{if(e.Role==EmployeeRole.Cashier) progress=e;};
            Assert.That(customers.TrySpawn(),Is.True); yield return null;
            var requests=(System.Collections.IDictionary)Field<object>(world.Service,"byActor");
            var oldId=customers.Snapshot.Single().VisitId; var request=requests[oldId]; Assert.That(request,Is.Not.Null);
            var callback=(Action<MovementResult>)request.GetType().GetField("Callback").GetValue(request);
            var target=(NavigationTarget)request.GetType().GetField("Original").GetValue(request);
            var requestId=(long)request.GetType().GetField("Id").GetValue(request);
            var arrived=new MovementResult(requestId,oldId,MovementStatus.Arrived,NavigationFailure.None,NavigationFailure.None,NavigationFailure.None,target.Position,0);
            yield return Until(()=>employees.Snapshot.Any(e=>e.Role==EmployeeRole.Cashier && e.State==EmployeeState.Working)); yield return null;
            var replay=new CafeTaskEvent(progress.Token,progress.Role,progress.ActorId,progress.HandleId,progress.RequestId,
                progress.Generation,progress.LayoutRevision,CafeTaskEventKind.ServiceFinished,
                new CafeWorkCheckpoint(EmployeeRole.Cashier,progress.Checkpoint.VisitId,progress.Checkpoint.Order,CafeWorkStage.Finished,0));
            var before=owner.Snapshot; var service=Field<CafeLoopService>(owner,"service");
            decor.EnterDecorationMode(); Rotate(); var calls=0;
            time.ResumeAvailabilityChanged+=Observe;
            decor.ChangedLayoutExitPreparing+=v=>
            {
                calls++; Assert.That(decor.CompletedLayoutChangeVersion,Is.EqualTo(v-1));
                Assert.That(owner.CanProgress,Is.False); Assert.That(time.IsResumeBlocked,Is.True);
                Assert.That(owner.Snapshot.Epoch,Is.EqualTo(before.Epoch+1)); Assert.That(owner.Snapshot.Orders,Is.Empty);
                var next=(long)Field<object>(world.Service,"nextId");
                service.ReportTask(replay); callback(arrived); owner.Step(.5f); Assert.That(owner.Snapshot.Orders,Is.Empty);
                Assert.That((long)Field<object>(world.Service,"nextId"),Is.EqualTo(next),"pre-publication callback cannot issue movement");
                Assert.That(owner.Snapshot.Visits,Is.Empty,"paused snapshot must already reflect actual cleared customers");
                InvokePreparing(v); Assert.That(owner.Snapshot.Epoch,Is.EqualTo(before.Epoch+1),"same target version must be idempotent");
            };
            decor.ExitDecorationMode(); time.ResumeAvailabilityChanged-=Observe;
            Assert.That(calls,Is.EqualTo(1)); Assert.That(owner.Snapshot.Epoch,Is.EqualTo(before.Epoch+1));
            yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            void Observe() { owner.Step(.5f); if(decor.CompletedLayoutChangeVersion==0) Assert.That(owner.CanProgress,Is.False); }
        }
        [UnityTest] public IEnumerator CleanupFailureKeepsResumeBlockAndDiagnostic()
        {
            decor.EnterDecorationMode(); Rotate(); var original=Field<CustomerFlowController>(owner,"customers"); Set(owner,"customers",null);
            decor.ExitDecorationMode(); Set(owner,"customers",original);
            for(var f=0;f<8;f++) yield return null; owner.ResumeBlockedFlow();
            Assert.That(time.IsResumeBlocked,Is.True); Assert.That(owner.CanProgress,Is.False);
            Assert.That(owner.Snapshot.Failure,Is.Not.Empty,"Unknown cleanup must retain a diagnostic and cannot be blindly resumed");
        }
        [UnityTest] public IEnumerator NoChangeAndCancelledPreviewPreserveOrderAndCheckpoint()
        {
            owner.HoldCollection=true; customers.AutoSpawn=true;
            yield return Until(()=>owner.Snapshot.Orders.Count>0); customers.AutoSpawn=false;
            var before=owner.Snapshot; var revision=adapter.Revision; decor.EnterDecorationMode();
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,Machine),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True); decor.CancelFunctionalSurfacePreview(); decor.ExitDecorationMode();
            Assert.That(adapter.Revision,Is.EqualTo(revision),"Cancellation cannot first publish a delayed P14 readiness policy after business started");
            Assert.That(owner.Snapshot.Epoch,Is.EqualTo(before.Epoch)); Assert.That(owner.Snapshot.Orders.Keys,Is.EqualTo(before.Orders.Keys));
            yield return Until(()=>owner.Snapshot.Delivery.Count==1);
        }
        [UnityTest] public IEnumerator CollectedDisabledVisitAndOriginalCapacitySurviveRepeatedDoneAndExitOnce()
        {
            owner.HoldCollection=true; Assert.That(customers.TrySpawn(),Is.True);
            yield return Until(()=>owner.Snapshot.Delivery.Count==1 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(customers.TrySpawn(),Is.True,customers.Status); yield return Until(()=>customers.Snapshot.Count==2 && customers.Snapshot.Any(v=>v.State==CustomerVisitState.Queued));
            owner.HoldCollection=false; yield return Until(()=>owner.Snapshot.CollectedExitVisitIds.Count==1);
            time.SetPaused(); var id=owner.Snapshot.CollectedExitVisitIds.Single(); var body=world.RegisteredActors.Single(a=>a.ActorId==id);
            var pose=body.transform.position; body.enabled=false; var capacity=customers.Capacity;
            var employeeBodies=world.RegisteredActors.Where(a=>a.ActorId!=id && employees.Snapshot.Any(e=>e.ActorId==a.ActorId)).ToArray();
            var epoch=owner.Snapshot.Epoch;
            for(var round=0;round<2;round++)
            {
                decor.EnterDecorationMode(); Rotate(); decor.ExitDecorationMode(); yield return null;
                Assert.That(owner.Snapshot.Epoch,Is.EqualTo(++epoch)); Assert.That(owner.Snapshot.Orders,Is.Empty);
                Assert.That(customers.Snapshot.Select(v=>v.VisitId),Is.EqualTo(new[]{id}));
                Assert.That(body.transform.position,Is.EqualTo(pose)); Assert.That(customers.Capacity,Is.SameAs(capacity));
                Assert.That(capacity.GetCapacities()[0].Used,Is.EqualTo(1));
                foreach(var employee in employeeBodies) Assert.That(world.RegisteredActors.Contains(employee),Is.True);
                Assert.That(employees.TryCaptureOrderCheckpoint(EmployeeRole.Barista,out _),Is.False,"Old order checkpoint must be discarded");
                Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
            }
            body.enabled=true; Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True,time.ResumeBlockReason);
            yield return Until(()=>customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(capacity.GetCapacities().All(c=>c.Used==0),Is.True);
            Assert.That(owner.Snapshot.CollectedExitVisitIds,Is.Empty); Assert.That(world.RegisteredActors.Count,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator ChangedDoneRestoresFastAndReturnsSameEmployeesToNewStations()
        {
            time.TrySetSpeed(GameSpeed.Fast); var bodies=world.RegisteredActors.ToArray();
            decor.EnterDecorationMode(); var pose=bodies.Select(a=>a.transform.position).ToArray(); Rotate(); decor.ExitDecorationMode();
            CollectionAssert.AreEquivalent(bodies,world.RegisteredActors); CollectionAssert.AreEqual(pose,bodies.Select(a=>a.transform.position));
            yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Fast));
            foreach(var e in employees.Snapshot)
            { employees.TryGetBinding(e.Role,out var b); var a=world.RegisteredActors.Single(x=>x.ActorId==e.ActorId);
                Assert.That(Vector3.Distance(a.transform.position,b.WorkTarget.Position),Is.LessThan(a.Settings.ArrivalDistance+.001f)); }
        }
        [UnityTest] public IEnumerator BlockedCollectedExitKeepsOverCapacityTokensThenRepairAndNewEpochRejectOldSuccess()
        {
            CafeTaskEvent oldSuccess=default; employees.IntegratedTaskEvent+=e=>{if(e.Kind==CafeTaskEventKind.ServiceFinished && oldSuccess.HandleId==0) oldSuccess=e;};
            owner.HoldCollection=true; Assert.That(customers.TrySpawn(),Is.True);
            yield return Until(()=>owner.Snapshot.Delivery.Count==1 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            var departure=Ref<Transform>(customers,"exit"); var validExit=departure.position; departure.position=Vector3.one*1000;
            owner.HoldCollection=false; yield return Until(()=>owner.Snapshot.CollectedExitVisitIds.Count==1 && customers.Snapshot.Single().State==CustomerVisitState.Blocked);
            var id=owner.Snapshot.CollectedExitVisitIds.Single(); var body=world.RegisteredActors.Single(a=>a.ActorId==id);
            var pose=body.transform.position; var capacity=customers.Capacity; var token=capacity.GetReservations().Where(r=>r.OwnerId==id && r.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers).Single().Token;
            time.SetPaused(); decor.EnterDecorationMode(); Rotate(); decor.ExitDecorationMode();
            Assert.That(owner.Snapshot.Visits.Select(v=>v.VisitId),Is.EqualTo(new[]{id}));
            Assert.That(body.transform.position,Is.EqualTo(pose)); Assert.That(customers.Capacity,Is.SameAs(capacity));
            owner.Step(0); Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True,time.ResumeBlockReason);
            yield return Until(()=>employees.Snapshot.All(e=>e.State==EmployeeState.Idle) && customers.Snapshot.Single().State==CustomerVisitState.Blocked);
            capacity.UpdateFloorCellCount(0);
            Assert.That(capacity.GetCapacities()[0].Used,Is.EqualTo(1)); Assert.That(capacity.GetCapacities()[0].OverCapacity,Is.EqualTo(1));
            Assert.That(capacity.CanAdmit,Is.False); Assert.That(customers.TrySpawn(),Is.False);
            Assert.That(capacity.GetReservations().Single(r=>r.Token.Equals(token)).State,Is.EqualTo(AnimalCafe.Capacity.ReservationState.Occupied));
            departure.position=validExit; owner.ResumeBlockedFlow();
            Assert.That(owner.CanProgress,Is.True); Assert.That(customers.Snapshot.Single().State,Is.EqualTo(CustomerVisitState.Exiting));
            Assert.That(customers.TrySpawn(),Is.False,"Running cafe still cannot admit while the surviving Total lease exceeds the new limit");
            // Restore the legitimate floor input; no character position is assigned.
            capacity.UpdateFloorCellCount(AnimalCafe.Capacity.FloorCapacitySource.CountInteriorCells(layout.Layout));
            yield return Until(()=>customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(capacity.GetCapacities().All(c=>c.Used==0),Is.True); var epoch=owner.Snapshot.Epoch;
            Assert.That(customers.TrySpawn(),Is.True,customers.Status); yield return Until(()=>owner.Snapshot.Orders.Count==1);
            var key=owner.Snapshot.Orders.Keys.Single(); Assert.That(key.OrderId,Is.EqualTo(1)); Assert.That(key.Epoch,Is.EqualTo(epoch));
            Assert.That(oldSuccess.Token.Epoch,Is.LessThan(key.Epoch),"Replay must remain the old successful event after numeric ID reuse");
            Field<CafeLoopService>(owner,"service").ReportTask(oldSuccess); owner.Step(.1f);
            Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1)); Assert.That(owner.Snapshot.PaidVisitIds,Does.Not.Contain(id));
            Assert.That(world.RegisteredActors.Contains(body),Is.False);
        }
        [UnityTest] public IEnumerator DoneDuringActualCarryRetiresDeliveryLeaseBeforeNextEpochDelivery()
        {
            owner.HoldCollection=true; Assert.That(customers.TrySpawn(),Is.True);
            yield return Until(()=>employees.Snapshot.Any(e=>e.Role==EmployeeRole.Barista && e.State==EmployeeState.CarryingToPickUp) && Field<string>(owner,"leaseActor")!=null);
            var old=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista); var bodies=world.RegisteredActors.Where(a=>employees.Snapshot.Any(e=>e.ActorId==a.ActorId)).ToArray();
            time.SetPaused(); decor.EnterDecorationMode(); Rotate(); decor.ExitDecorationMode();
            Assert.That(Field<string>(owner,"leaseActor"),Is.Null,"Done must retire the previous queue's delivery ownership even before actual arrival");
            Assert.That(Field<bool>(owner,"leaseArrived"),Is.False);
            owner.Step(0); Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True,time.ResumeBlockReason);
            yield return Until(()=>employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            CollectionAssert.AreEquivalent(bodies,world.RegisteredActors);
            Assert.That(customers.TrySpawn(),Is.True,customers.Status); yield return Until(()=>owner.Snapshot.Delivery.Count==1);
            Assert.That(employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).HandleId,Is.GreaterThan(old.HandleId));
            owner.HoldCollection=false; yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.State==OrderState.Completed));
            yield return Until(()=>customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
        }
        [UnityTest] public IEnumerator InvalidChangedDoneStaysBlockedUntilAnotherValidChangedDone()
        {
            decor.EnterDecorationMode();
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,Machine),Is.True);
            Assert.That(decor.TryStoreFunctionalSurfacePreview(),Is.True); decor.ExitDecorationMode();
            for(var f=0;f<5;f++) yield return null;
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused)); Assert.That(time.IsResumeBlocked,Is.True);
            Assert.That(owner.CanProgress,Is.False); owner.ResumeBlockedFlow(); Assert.That(time.IsResumeBlocked,Is.True);
            decor.EnterDecorationMode(); var catalog=Ref<FurnitureContentCatalog>(decor,"contentCatalog");
            var support=layout.Layout.FurnitureInstances.Single(f=>f.InstanceId=="12000000000000000000000000000002");
            var slot=catalog.BuildSurfaceSlotCatalog(layout.Layout.GridSettings).GetForSupport(support.DefinitionId).First().SlotId;
            Assert.That(layout.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance(Machine,"equipment.coffee-machine.01",new SurfaceSlotAddress(support.InstanceId,slot),FurnitureRotation.Degrees0)).Succeeded,Is.True);
            decor.RefreshConfirmedNavigationPresentation(); Rotate(); decor.ExitDecorationMode();
            yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Normal)); Assert.That(time.IsResumeBlocked,Is.False);
        }
        private void Rotate()
        {
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,Machine),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True); Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);
        }
        private void Seed()
        {
            var support=layout.Layout.FurnitureInstances.Single(); var catalog=Ref<FurnitureContentCatalog>(decor,"contentCatalog");
            var slot=catalog.BuildSurfaceSlotCatalog(layout.Layout.GridSettings).GetForSupport(support.DefinitionId).First().SlotId;
            Assert.That(layout.Layout.PlaceFurniture(FurnitureInstance.Restore("12000000000000000000000000000002",support.DefinitionId,new GridPosition(4,3),FurnitureRotation.Degrees0)).Succeeded,Is.True);
            Assert.That(layout.Layout.PlaceFurniture(FurnitureInstance.Restore("12000000000000000000000000000003",support.DefinitionId,new GridPosition(6,3),FurnitureRotation.Degrees0)).Succeeded,Is.True);
            Assert.That(layout.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance("12000000000000000000000000000004","equipment.cash-register.01",new SurfaceSlotAddress(support.InstanceId,slot),FurnitureRotation.Degrees0)).Succeeded,Is.True);
            Assert.That(layout.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance(Machine,"equipment.coffee-machine.01",new SurfaceSlotAddress("12000000000000000000000000000002",slot),FurnitureRotation.Degrees0)).Succeeded,Is.True);
            Assert.That(layout.FunctionalSurfaceLayout.PlacePickUp(new PickUpPointInstance("12000000000000000000000000000006",new SurfaceSlotAddress("12000000000000000000000000000003",slot))).Succeeded,Is.True);
        }
        private void InvokePreparing(long version) => typeof(CafeLoopController).GetMethod("OnChangedLayoutExitPreparing",BindingFlags.Instance|BindingFlags.NonPublic)?.Invoke(owner,new object[]{version});
        private static void Set(object o,string name,object value)=>o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,value);
        private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
        private T[] All<T>() where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        private T Find<T>() where T:Component=>All<T>().Single();
        private static T Ref<T>(Object o,string field) where T:Object=>(T)new SerializedObject(o).FindProperty(field).objectReferenceValue;
        private static NavigationActor Prefab(string species)=>AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/Phase13/Employees/{species}/PF_{species}_Employee.prefab").GetComponent<NavigationActor>();
        private IEnumerator Until(Func<bool> done,int max=4000)
        { for(var f=0;f<max && !done();f++) yield return null; Assert.That(done(),Is.True,customers.Status+" | "+owner.Snapshot.Status+" "+owner.Snapshot.Failure+" | "+string.Join(" / ",employees.Snapshot.Select(e=>e.Role+" "+e.State+" "+e.Failure))); }
    }
}
#endif

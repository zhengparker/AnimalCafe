#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.CafeLoop;
using AnimalCafe.Capacity;
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

namespace AnimalCafe.Tests.PlayMode.Phase14
{
    public sealed class CafeCustomerCarryTests
    {
        private Scene scene;
        private CafeLoopController owner;
        private CustomerFlowController customers;
        private EmployeeFlowController employees;
        private CafeLoopValidationController tools;
        private NavigationWorld world;
        private GameTimeService time;
        private DecorationModeController decor;
        private float capture,scale;
        private UnityEngine.InputSystem.InputActionAsset[] inputs;
        private Action carryFaultObserverCleanup;
        private Action carryActualExitObserverCleanup;
        [UnitySetUp] public IEnumerator Setup()
        {
            capture=Time.captureDeltaTime; scale=Time.timeScale; Time.captureDeltaTime=1f/60;
            var shared=TestContext.CurrentContext.Test.Name==nameof(SharedFirstPaidCustomerReleasesCounterWhileBaristaWorksWithoutNavigationRetry);
            scene=EditorSceneManager.LoadSceneInPlayMode(shared?"Assets/Scenes/Validation/Phase14SharedPickup.unity":"Assets/Scenes/Validation/Phase14CafeLoop.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            owner=Find<CafeLoopController>(); customers=Find<CustomerFlowController>(); employees=Find<EmployeeFlowController>();
            tools=Find<CafeLoopValidationController>(); world=Find<NavigationWorld>(); time=Find<GameTimeService>(); decor=Find<DecorationModeController>();
            inputs=Phase8SceneInputTestCleanup.CaptureAssets(scene); if(!shared) tools.HoldArrivals(); tools.HoldCollection();
            yield return Until(()=>owner.CanProgress && (shared || employees.Snapshot.All(e=>e.State==EmployeeState.Idle)));
        }
        private T Find<T>() where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
        private IEnumerator Until(Func<bool> condition,bool allowBlocked=false)
        {
            for(var frame=0;frame<6000 && !condition();frame++)
            {
                if(!allowBlocked) Assert.That(customers.Snapshot.All(v=>v.State!=CustomerVisitState.Blocked) && employees.Snapshot.All(e=>e.State!=EmployeeState.Blocked),Is.True,owner.Snapshot.Failure);
                yield return null;
            }
            Assert.That(condition(),Is.True,owner.ViewSnapshot.Reception+" / "+owner.Snapshot.Failure);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var exitCleanup=carryActualExitObserverCleanup;carryActualExitObserverCleanup=null;
            exitCleanup?.Invoke(); // Disarm actual-exit assertions before Scene unload can destroy survivors.
            var observerCleanup=carryFaultObserverCleanup;carryFaultObserverCleanup=null;
            observerCleanup?.Invoke(); // Fallback even if Unity abandons the nested test iterator.
            if(scene.IsValid() && scene.isLoaded)
            { SceneManager.SetActiveScene(SceneManager.CreateScene("P14 carry cleanup")); yield return SceneManager.UnloadSceneAsync(scene); }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputs ?? Array.Empty<UnityEngine.InputSystem.InputActionAsset>());
            Time.captureDeltaTime=capture; Time.timeScale=scale;
        }

        [UnityTest] public IEnumerator SuccessfulCreateMovesActualCustomerBeforeBaristaFinishesWorking()
        {
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==id).CounterArrived);
            var counter=customers.Snapshot.Single().Position;
            yield return Until(()=>owner.Snapshot.Orders.Count==1);
            // Payment accepts the original bounded task; normal ready batches issue its movement later.
            // 付款当下证明原任务已接管；不再把 Accepted 错当成同一 business step 的 Native 移动。
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            object Read(object value,string name)=>value.GetType().GetField(name,flags).GetValue(value);
            var visits=(IDictionary)Read(customers,"visits");var visit=visits[id];var paid=Read(visit,"PaidPickup");
            Assert.That(paid,Is.Not.Null,"Successful Create owns an actual paid movement task at payment");
            var actor=Read(visit,"Actor");var admission=Read(visit,"Admission");
            var total=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token;
            var order=owner.Snapshot.Orders.Keys.Single();var intent=(TrafficIntentKey)Read(paid,"Intent");
            var firstFrame=(int)Read(paid,"FirstFrame");var core=Read(customers,"traffic");
            var entries=(IDictionary)Read(core,"entries");Assert.That(entries.Contains(intent),Is.True);
            var entry=entries[intent];var initial=(TrafficDecision)Read(entry,"Decision");
            Assert.That(Read(paid,"Accepted"),Is.EqualTo(true));Assert.That(Read(paid,"Transferred"),Is.EqualTo(false));
            Assert.That(Read(entry,"Queued"),Is.EqualTo(true));Assert.That(Read(entry,"Prepared"),Is.EqualTo(false));
            Assert.That(initial.Kind==TrafficDecisionKind.DeferredByBudget || initial.Kind==TrafficDecisionKind.WaitingForTraffic,Is.True);
            Assert.That(initial.AcquiredSequence,Is.Zero);Assert.That((long)Read(visit,"RequestId"),Is.Zero);
            Assert.That(customers.CounterAssignments.Any(a=>a.VisitId==id),Is.True);
            Assert.That(((NavigationActor)actor).transform.position,Is.EqualTo(counter));
            var count=(int)Read(entry,"CandidateCount");var index=(int)Read(entry,"CurrentIndex");
            var finiteLimit=(float)Read(entry,"FiniteLimit");var sequence=(long)Read(entry,"RequestSequence");
            var acquired=0L;var queries=(int)Read(entry,"Queries");object native=null;var nativeId=0L;
            var requests=(IDictionary)Read(world.Service,"byActor");
            bool OriginalProgress()
            {
                Assert.That(visits[id],Is.SameAs(visit));Assert.That(Read(visit,"PaidPickup"),Is.SameAs(paid));
                Assert.That(Read(visit,"Actor"),Is.SameAs(actor));Assert.That(Read(visit,"Admission"),Is.SameAs(admission));
                Assert.That(world.RegisteredActors.Single(a=>a.ActorId==id),Is.SameAs(actor));
                Assert.That(owner.Snapshot.Orders.Keys.Single(),Is.EqualTo(order));Assert.That(Read(paid,"Order"),Is.EqualTo(order));
                Assert.That(Read(paid,"Intent"),Is.EqualTo(intent));Assert.That(Read(paid,"ActiveIntent"),Is.EqualTo(intent));
                Assert.That(Read(paid,"FirstFrame"),Is.EqualTo(firstFrame));Assert.That(Read(paid,"Accepted"),Is.EqualTo(true));
                Assert.That(Read(paid,"Failed"),Is.EqualTo(false));Assert.That(Read(paid,"RecoveryUsed"),Is.EqualTo(false));
                Assert.That(Read(paid,"RecoveryPending"),Is.EqualTo(false));Assert.That(entries[intent],Is.SameAs(entry));
                Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(firstFrame));Assert.That(Read(entry,"Retired"),Is.EqualTo(false));
                Assert.That(Read(entry,"CandidateCount"),Is.EqualTo(count));Assert.That(Read(entry,"CurrentIndex"),Is.EqualTo(index));
                Assert.That(Read(entry,"FiniteLimit"),Is.EqualTo(finiteLimit));
                var nextSequence=(long)Read(entry,"RequestSequence");
                if(sequence==0)sequence=nextSequence;else Assert.That(nextSequence,Is.EqualTo(sequence),"One original positive request sequence; no retry/rebudget");
                var nextAcquired=(long)Read(entry,"AcquiredSequence");
                if(acquired==0)acquired=nextAcquired;else Assert.That(nextAcquired,Is.EqualTo(acquired));
                var nextQueries=(int)Read(entry,"Queries");Assert.That(nextQueries,Is.GreaterThanOrEqualTo(queries));queries=nextQueries;
                Assert.That(queries,Is.LessThanOrEqualTo((int)Read(core,"queriesPerIntent")));
                Assert.That(customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token,Is.SameAs(total));
                if(requests.Contains(id))
                {
                    var current=requests[id];if(native==null){native=current;nativeId=(long)Read(native,"Id");}
                    Assert.That(current,Is.SameAs(native));Assert.That((long)Read(current,"Id"),Is.EqualTo(nativeId));
                    Assert.That(Read(visit,"RequestId"),Is.EqualTo(nativeId));Assert.That((int)Read(current,"RetryCount"),Is.Zero);
                }
                if(native!=null)Assert.That(requests.Contains(id),Is.True,"Original Native request remains live through early-motion evidence");
                return true;
            }
            OriginalProgress();
            TestContext.WriteLine("P14_PAID_OWNERSHIP key="+intent+" first="+firstFrame+" frame="+Time.frameCount+
                " sequence="+sequence+" acquired="+acquired+" queries="+queries+" accepted=True transferred=False pending="+initial.Kind);
            yield return Until(()=>OriginalProgress() && native!=null && customers.Snapshot.Single().State==CustomerVisitState.MovingToPickup);
            Assert.That(Read(paid,"Transferred"),Is.EqualTo(true));Assert.That(sequence,Is.GreaterThan(0));Assert.That(acquired,Is.GreaterThan(0));
            Assert.That(customers.Snapshot.Single().State,Is.EqualTo(CustomerVisitState.MovingToPickup),"Original accepted paid task enters actual movement through normal frames");
            yield return Until(()=>OriginalProgress() && employees.Snapshot.Any(e=>e.Role==EmployeeRole.Barista && e.State==EmployeeState.Working) &&
                Vector3.Distance(customers.Snapshot.Single().Position,counter)>.1f);
            Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.Preparing));
            Assert.That(employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).RemainingWorkSeconds,Is.GreaterThan(0));
            Assert.That(customers.Snapshot.Single().State,Is.EqualTo(CustomerVisitState.MovingToPickup));
            TestContext.WriteLine("P14_EARLY_PICKUP counter="+counter.ToString("F6")+" actual="+customers.Snapshot.Single().Position.ToString("F6")+
                " order=Preparing baristaRemaining="+employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).RemainingWorkSeconds);
        }

        [UnityTest] public IEnumerator SharedFirstPaidCustomerReleasesCounterWhileBaristaWorksWithoutNavigationRetry()
        {
            // Natural reception, then isolate the first original body; no fake orders or movement.
            // 正式自动接待首客；出生后停止后续到店，不手动推进身体/订单。
            yield return Until(()=>customers.Snapshot.Count>0); tools.HoldArrivals();
            var id=customers.Snapshot.Single().VisitId;
            var actor=world.RegisteredActors.Single(a=>a.ActorId==id);
            yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==id).CounterArrived);
            var counter=actor.transform.position;
            yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==id));
            var paidAt=Time.time; var requests=(IDictionary)typeof(NavigationService).GetField("byActor",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(world.Service);
            // Keep the original payment clock/window; only the original paid task may issue the first real Native request.
            // 同一 paid source 正常准备；不重置 paidAt、年龄、预算或换 key。
            var proofVisits=(IDictionary)CarryRead(customers,"visits");var proofVisit=proofVisits[id];var paid=CarryRead(proofVisit,"PaidPickup");
            Assert.That(paid,Is.Not.Null,"SHARED_PAID_PREPARATION_NOT_PROVEN: actual paid task required");
            var admission=CarryRead(proofVisit,"Admission");var order=owner.Snapshot.Orders.Single(o=>o.Value.CustomerId==id).Key;
            var total=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token;
            var core=(ServiceTrafficCoordinator)CarryRead(customers,"traffic");var intent=(TrafficIntentKey)CarryRead(paid,"ActiveIntent");
            var entries=(IDictionary)CarryRead(core,"entries");Assert.That(entries.Contains(intent),Is.True,"SHARED_PAID_PREPARATION_NOT_PROVEN: original Entry must exist before first Native");
            var entry=entries[intent];var first=(int)CarryRead(entry,"FirstFrame");var sequence=(long)CarryRead(entry,"RequestSequence");
            var acquisition=(long)CarryRead(entry,"AcquiredSequence");var queries=(int)CarryRead(entry,"Queries");
            var count=(int)CarryRead(entry,"CandidateCount");var finite=(float)CarryRead(entry,"FiniteLimit");var sharedWorkingObserved=false;
            void OriginalPaidSource()
            {
                Assert.That(proofVisits[id],Is.SameAs(proofVisit));Assert.That(CarryRead(proofVisit,"PaidPickup"),Is.SameAs(paid));
                Assert.That(CarryRead(proofVisit,"Actor"),Is.SameAs(actor));Assert.That(CarryRead(proofVisit,"Admission"),Is.SameAs(admission));
                Assert.That(CarryRead(paid,"Order"),Is.EqualTo(order));Assert.That(CarryRead(paid,"Intent"),Is.EqualTo(intent));Assert.That(CarryRead(paid,"ActiveIntent"),Is.EqualTo(intent));
                Assert.That(CarryRead(paid,"Accepted"),Is.EqualTo(true));Assert.That(CarryRead(paid,"Failed"),Is.EqualTo(false));Assert.That(CarryRead(paid,"RecoveryUsed"),Is.EqualTo(false));
                Assert.That(CarryRead(paid,"RecoveryPending"),Is.EqualTo(false));Assert.That(CarryRead(paid,"FirstFrame"),Is.EqualTo(first));
                Assert.That(CarryRead(customers,"traffic"),Is.SameAs(core));Assert.That(entries[intent],Is.SameAs(entry));Assert.That(CarryRead(entry,"FirstFrame"),Is.EqualTo(first));
                Assert.That(CarryRead(entry,"Retired"),Is.EqualTo(false));Assert.That(((TrafficDecision)CarryRead(entry,"Decision")).FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None));
                Assert.That(CarryRead(entry,"CandidateCount"),Is.EqualTo(count));Assert.That(CarryRead(entry,"FiniteLimit"),Is.EqualTo(finite));
                var nextSequence=(long)CarryRead(entry,"RequestSequence");if(sequence==0)sequence=nextSequence;Assert.That(nextSequence,Is.EqualTo(sequence));
                var nextAcquisition=(long)CarryRead(entry,"AcquiredSequence");if(acquisition==0)acquisition=nextAcquisition;Assert.That(nextAcquisition,Is.EqualTo(acquisition));
                var nextQueries=(int)CarryRead(entry,"Queries");Assert.That(nextQueries,Is.GreaterThanOrEqualTo(queries));queries=nextQueries;
                Assert.That(queries,Is.LessThanOrEqualTo((int)CarryRead(core,"queriesPerIntent")));
                Assert.That(customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token,Is.SameAs(total));
                var originalOrder=owner.Snapshot.Orders[order];var preparingBarista=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);
                Assert.That(originalOrder.State,Is.Not.EqualTo(OrderState.ReadyForPickup).And.Not.EqualTo(OrderState.Completed),"Do not wait beyond the original early-departure work window");
                Assert.That(preparingBarista.State==EmployeeState.Idle || preparingBarista.State==EmployeeState.Working,Is.True,"SHARED_PAID_PREPARATION_NOT_PROVEN: original Barista work must not finish while awaiting first Native");
                if(preparingBarista.State==EmployeeState.Working){sharedWorkingObserved=true;Assert.That(preparingBarista.RemainingWorkSeconds,Is.GreaterThan(0));}
                else Assert.That(sharedWorkingObserved,Is.False,"The observed original Working window cannot end and be replaced while waiting");
            }
            var preparationFrames=0;
            while(preparationFrames<1200 && !requests.Contains(id))
            { OriginalPaidSource();yield return null;preparationFrames++; }
            OriginalPaidSource();
            TestContext.WriteLine("SHARED_PAID_FIRST_NATIVE source="+CarryKey(intent)+" first="+first+" prepFrames="+preparationFrames+" sequence="+sequence+" acquired="+acquisition+" queries="+queries+" actualRequest="+requests.Contains(id));
            var request=requests[id]; Assert.That(request,Is.Not.Null,"Successful payment sends the real Pickup movement immediately");
            var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
            object Read(string name)=>request.GetType().GetField(name,flags).GetValue(request);
            var requestId=(long)Read("Id"); var observedCertificate=false;
            for(var frame=preparationFrames;frame<1200 && customers.CounterAssignments.Any(a=>a.VisitId==id);frame++)
            {
                OriginalPaidSource();
                Assert.That(requests.Contains(id),Is.True,"The same original movement remains active until actual Counter departure");
                Assert.That(requests[id],Is.SameAs(request),"No replacement movement may hide the original transfer's progress");
                Assert.That((long)Read("Id"),Is.EqualTo(requestId));
                if(!observedCertificate && (float)Read("NoProgressSeconds")>.5f)
                { observedCertificate=true; LogSharedCornerCertificate(actor,Read("Driver")); }
                yield return null;
            }
            var barista=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);
            TestContext.WriteLine("P14_SHARED_COUNTER_RELEASE elapsed="+(Time.time-paidAt)+" actual="+actor.transform.position.ToString("F9")+
                " distance="+Vector3.Distance(actor.transform.position,counter)+" request="+requestId+" retry="+Read("RetryCount")+" generation="+Read("Generation")+" Barista="+barista.State+" remaining="+barista.RemainingWorkSeconds);
            Assert.That(customers.CounterAssignments.Any(a=>a.VisitId==id),Is.False,"Counter releases only after the original body really departs");
            Assert.That((int)Read("RetryCount"),Is.Zero,"A safe first-customer transfer must not stall until the three-second native retry");
            Assert.That(barista.State,Is.EqualTo(EmployeeState.Working));
            Assert.That(barista.RemainingWorkSeconds,Is.GreaterThan(0));
            Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.Preparing));
            yield return Until(()=>customers.Snapshot.Single(v=>v.VisitId==id).State==CustomerVisitState.PickupWaiting);
            Assert.That(customers.PickupAssignments.Single(a=>a.VisitId==id).Arrived,Is.True);
            Assert.That(customers.Snapshot.All(v=>v.State!=CustomerVisitState.Blocked),Is.True);
        }

        private void LogSharedCornerCertificate(NavigationActor actor,object driver)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            object D(string n)=>driver.GetType().GetField(n,flags).GetValue(driver);
            var corner=(int)D("corner"); var raw=(Vector3[])D("rawCorners"); var corners=(Vector3[])D("corners");
            var arguments=new object[]{raw[corner],Vector3.zero};
            var candidateExists=(bool)driver.GetType().GetMethod("TrySkinSafeCorner",flags).Invoke(driver,arguments);
            var candidate=(Vector3)arguments[1];
            var guard=(NavigationCollisionGuard)typeof(NavigationWorld).GetField("guard",flags).GetValue(world);
            var solids=(System.Collections.Generic.IReadOnlyList<Collider>)typeof(NavigationWorld).GetField("solids",flags).GetValue(world);
            var filter=new UnityEngine.AI.NavMeshQueryFilter{agentTypeID=actor.Agent.agentTypeID,areaMask=actor.Agent.areaMask};
            foreach(var target in new[]{corners[Math.Min(corner+1,corners.Length-1)],candidate})
            {
                var delta=target-actor.transform.position; delta.y=0;
                var meshBlocked=UnityEngine.AI.NavMesh.Raycast(actor.transform.position,actor.transform.position+delta,out _,filter);
                var approved=guard.ClampDisplacement(actor,delta,world.RegisteredActors,solids);
                TestContext.WriteLine("P14_SHARED_CORNER pose="+actor.transform.position.ToString("F9")+" index="+corner+" raw="+raw[corner].ToString("F9")+
                    " adjusted="+((bool[])D("adjustedCorners"))[corner]+" candidateExists="+candidateExists+" candidate="+candidate.ToString("F9")+
                    " queryTarget="+target.ToString("F9")+" meshBlocked="+meshBlocked+" requestedDelta="+delta.ToString("F9")+" fullGuardApproved="+approved.ToString("F9"));
            }
        }

        [UnityTest] public IEnumerator SeparatePickupCollectsBeforeBaristaReturnsToMachine()
            => CollectBeforeBaristaReturns();
        private IEnumerator CollectBeforeBaristaReturns()
        {
            // Fixed formal Decor fixture: give real collection a longer physical return window.
            // 首客出生前只移动同一 CM support；不暂停员工任务、不写 pose/speed/预算或 Ready。
            Assert.That(customers.Snapshot,Is.Empty);Assert.That(owner.Snapshot.Orders,Is.Empty);
            var returnLayout=Find<CafeLayoutRuntime>();var returnAdapter=Find<NavigationLayoutAdapter>();
            var machine=returnLayout.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
            var machineAddress=machine.Address;var machineRotation=machine.Rotation;
            var machineSupport=returnLayout.Layout.FurnitureInstances.Single(f=>f.InstanceId==machineAddress.SupportFurnitureInstanceId);
            Assert.That(machineSupport.Position,Is.EqualTo(new GridPosition(4,6)),"FIXTURE: original separate CM support");
            var pickup=returnLayout.FunctionalSurfaceLayout.PickUpPoints.Single();var pickupAddress=pickup.Address;
            var register=returnLayout.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.cash-register.01");
            var registerAddress=register.Address;var registerPosition=returnLayout.Layout.FurnitureInstances.Single(f=>f.InstanceId==registerAddress.SupportFurnitureInstanceId).Position;
            Assert.That(employees.TryGetBinding(EmployeeRole.Barista,out var beforeLayoutBinding),Is.True);
            var oldHome=beforeLayoutBinding.WorkTarget.Position;
            var expectedHome=oldHome+CafeLoopRuntimeProof.Field<Transform>(returnAdapter,"gridRoot").TransformVector(
                new Vector3(-3*returnLayout.Layout.GridSettings.CellSize,0,-6*returnLayout.Layout.GridSettings.CellSize));
            decor.EnterDecorationMode();yield return null;
            var returnSession=CafeLoopRuntimeProof.Field<DecorationSession>(decor,"session");
            Assert.That(returnSession.BeginExisting(machineSupport.InstanceId).Succeeded,Is.True);
            Assert.That(returnSession.MovePreview(new GridPosition(1,0)).Succeeded,Is.True,"FIXTURE: one fixed legal CM move, never a geometry sweep");
            yield return null;
            // Same Controller confirm entry as the existing Matrix API fixture; all placement/Nav guards still run.
            var confirm=typeof(DecorationModeController).GetMethod("HandleConfirmRequested",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(confirm,Is.Not.Null);confirm.Invoke(decor,null);
            Assert.That(returnSession.ActivePreview,Is.Null,"Controller must accept the actual furniture/Nav preview");
            decor.ExitDecorationMode();yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(customers.Snapshot,Is.Empty);Assert.That(owner.Snapshot.Orders,Is.Empty);
            Assert.That(returnLayout.Layout.FurnitureInstances.Single(f=>f.InstanceId==machineSupport.InstanceId).Position,Is.EqualTo(new GridPosition(1,0)));
            var movedMachine=returnLayout.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.InstanceId==machine.InstanceId);
            Assert.That(movedMachine.Address,Is.EqualTo(machineAddress));Assert.That(movedMachine.Rotation,Is.EqualTo(machineRotation));
            Assert.That(returnLayout.FunctionalSurfaceLayout.PickUpPoints.Single().Address,Is.EqualTo(pickupAddress));
            Assert.That(returnLayout.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.InstanceId==register.InstanceId).Address,Is.EqualTo(registerAddress));
            Assert.That(returnLayout.Layout.FurnitureInstances.Single(f=>f.InstanceId==registerAddress.SupportFurnitureInstanceId).Position,Is.EqualTo(registerPosition));
            Assert.That(employees.TryGetBinding(EmployeeRole.Barista,out var movedLayoutBinding),Is.True);
            Assert.That(movedLayoutBinding.WorkStationId,Is.EqualTo(beforeLayoutBinding.WorkStationId));
            Assert.That(movedLayoutBinding.WorkTarget.Position,Is.EqualTo(expectedHome),"Same anchor direction translated by the real fixed support move");
            var returnSides=customers.GetPickupServiceTargets(pickup.InstanceId);Assert.That(returnSides,Is.Not.Empty);
            var minReturnDistance=returnSides.Min(side=>Vector3.Distance(side.Position,movedLayoutBinding.WorkTarget.Position));
            Assert.That(minReturnDistance,Is.GreaterThan(6f),"FIXTURE: all original real PU sides have a materially longer actual CM return");
            TestContext.WriteLine("P14_CARRY_RETURN_LAYOUT support="+machineSupport.InstanceId+" grid=(4,6)->(1,0) oldHome="+oldHome.ToString("F6")+
                " newHome="+movedLayoutBinding.WorkTarget.Position.ToString("F6")+" sides="+string.Join(";",returnSides.Select(side=>side.Position.ToString("F6")))+
                " minReturnDistance="+minReturnDistance+" originalPickup=True originalRegister=True beforeFirstCustomer=True");
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            var id=customers.Snapshot.Single().VisitId;
            tools.ResumeCollection();
            yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==id && o.State==OrderState.ReadyForPickup));
            employees.TryGetBinding(EmployeeRole.Barista,out var binding);
            var barista=world.RegisteredActors.Single(a=>a.ActorId==employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).ActorId);
            var ready=Time.frameCount;
            var previous=customers.Snapshot.Single().State;
            TestContext.WriteLine("P14_COLLECT_READY separate frame="+ready+" customer="+previous+" barista="+employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State+
                " pose="+barista.transform.position+" home="+binding.WorkTarget.Position+" lease="+CafeLoopRuntimeProof.Field<string>(owner,"leaseActor"));
            for(var frame=0;frame<6000 && !owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==id && o.State==OrderState.Completed);frame++)
            {
                yield return null;
                Assert.That(customers.Snapshot.All(v=>v.State!=CustomerVisitState.Blocked),Is.True,owner.Snapshot.Failure);
                var current=customers.Snapshot.Single().State;
                if(current!=previous)
                {
                    TestContext.WriteLine("P14_COLLECT_TRANSITION separate frames="+(Time.frameCount-ready)+" customer="+current+
                        " barista="+employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State+" pose="+barista.transform.position+
                        " lease="+CafeLoopRuntimeProof.Field<string>(owner,"leaseActor"));
                    previous=current;
                }
            }
            Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==id).State,Is.EqualTo(OrderState.Completed));
            Assert.That(employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State,Is.EqualTo(EmployeeState.Returning),
                "A ready customer should collect while the Barista is still returning, rather than after CM arrival");
            Assert.That(Vector3.Distance(barista.transform.position,binding.WorkTarget.Position),Is.GreaterThan(barista.Settings.ArrivalDistance));
            Assert.That(HeldCups(world.RegisteredActors.Single(a=>a.ActorId==id)).Length,Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator ShibaCollectedCupSurvivesPauseBlockedDisableAndChangedDoneUntilActualExit()
            => CarryLifecycle("Shiba",false);
        [UnityTest] public IEnumerator WestieCollectedCupSurvivesPauseBlockedDisableAndChangedDoneUntilActualExit()
            => CarryLifecycle("Westie",true);
        [UnityTest] public IEnumerator MissingCollectedCupPrefabReportsOnceWithoutBlockingActualExit()
            => MissingPresentation(false);
        [UnityTest] public IEnumerator MissingCollectedAppearanceReportsOnceWithoutBlockingActualExit()
            => MissingPresentation(true);
        private IEnumerator MissingPresentation(bool missingAppearance)
        {
            if(missingAppearance) customers.ConfigureCollectedCarry(CafeLoopRuntimeProof.Field<GameObject>(customers,"collectedCupPrefab"),Array.Empty<CustomerCarryPresenter.Appearance>());
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True); var id=customers.Snapshot.Single().VisitId; var body=world.RegisteredActors.Single(a=>a.ActorId==id);
            if(!missingAppearance) CafeLoopRuntimeProof.Set(body.GetComponent<CustomerCarryPresenter>(),"cupPrefab",null);
            var token=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token;
            var exit=CafeLoopRuntimeProof.Field<Transform>(customers,"exit"); var exited=false; var observing=true; var warnings=0;
            Application.LogCallback log=(message,stack,type)=>{if(type==LogType.Warning && message.StartsWith("Customer collected cup unavailable:",StringComparison.Ordinal)) warnings++;};
            var observer=body.gameObject.AddComponent<CafeActualExitObserver>(); observer.VisitId=id;
            observer.Receipt=(visit,position)=>
            {
                if(!observing) return;
                Assert.That(Vector3.Distance(position,exit.position),Is.LessThanOrEqualTo(.081f));
                Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==visit).State,Is.EqualTo(OrderState.Completed));
                Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,token)).State,Is.EqualTo(ReservationState.Released)); exited=true;
            };
            Application.logMessageReceived+=log;
            try
            {
                tools.ResumeCollection(); yield return Until(()=>exited && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                Assert.That(warnings,Is.EqualTo(1),"A missing collected visual must be visible once while canonical facts and actual exit still succeed");
                Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
            }
            finally { observing=false; Application.logMessageReceived-=log; }
        }
        private IEnumerator CarryLifecycle(string species,bool skipFirst)
        {
            if(skipFirst)
            {
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                Assert.That(customers.TrySpawn(),Is.True); tools.ResumeCollection();
                yield return Until(()=>owner.Snapshot.Orders.Count==1 && customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                tools.HoldCollection();
            }
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            var id=customers.Snapshot.Single().VisitId; var body=world.RegisteredActors.Single(a=>a.ActorId==id);
            Assert.That(body.gameObject.name,Does.Contain(species));
            var token=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token;
            yield return Until(()=>owner.Snapshot.Delivery.Any(d=>owner.Snapshot.Orders[d.Key].CustomerId==id) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(HeldCups(body),Is.Empty,"Uncollected waiting customers have no hand cup");
            Assert.That(body.ModelAnimator.GetLayerWeight(1),Is.Zero);
            Assert.That(body.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Working"),Is.False);
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            var uncollected=customers.Snapshot.Single(v=>v.VisitId!=id).VisitId;
            yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==uncollected).CounterArrived);
            tools.ResumeCollection(); yield return Until(()=>owner.Snapshot.CollectedExitVisitIds.Contains(id)); tools.HoldCollection();
            var cup=HeldCups(body).SingleOrDefault();
            Assert.That(cup,Is.Not.Null,"Canonical collection must create a Visit-owned customer hand cup");
            var animator=body.ModelAnimator;
            yield return Until(()=>body.GetComponent<NavigationWalkPresenter>().ActualSpeed>.1f && animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"));
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Working"),Is.False);
            Assert.That(animator.GetLayerWeight(1),Is.EqualTo(1)); Assert.That(animator.GetCurrentAnimatorStateInfo(1).IsName("Carry"),Is.True);
            Assert.That(animator.speed,Is.EqualTo(1)); Assert.That(animator.updateMode,Is.EqualTo(AnimatorUpdateMode.Normal));
            var reference=UnityEngine.Object.Instantiate(animator.gameObject); reference.GetComponent<Animator>().enabled=false;
            try
            {
                animator.runtimeAnimatorController.animationClips.Single(c=>c.name=="Carry").SampleAnimation(reference,0);
                foreach(var boneName in new[]{"chest","head","upper_arm.L","upper_arm.R","forearm.L","forearm.R"})
                {
                    var expected=reference.GetComponentsInChildren<Transform>().Single(t=>t.name==boneName);
                    var actualBone=animator.GetComponentsInChildren<Transform>().Single(t=>t.name==boneName);
                    Assert.That(Quaternion.Angle(actualBone.localRotation,expected.localRotation),Is.LessThan(.1f),species+" full source Carry "+boneName);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(reference); }
            var bones=animator.GetComponentsInChildren<Transform>(); var hand=bones.Single(t=>t.name=="hand.R"); var leg=bones.Single(t=>t.name=="thigh.L");
            Assert.That(cup.parent.name,Is.EqualTo("hand_socket.R"));
            Assert.That(Vector3.Dot(cup.up,Vector3.up),Is.GreaterThan(.999f));
            Assert.That(Vector3.Distance(cup.lossyScale,Vector3.one),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(cup.position,hand.position),Is.InRange(.02f,.3f));
            Assert.That(cup.GetComponentsInChildren<Collider>(true).All(c=>!c.enabled),Is.True,"Presentation cannot add a physical obstacle");
            var upper=hand.localRotation; var lower=leg.localRotation; var actual=body.transform.position;
            for(var f=0;f<8;f++) yield return null;
            Assert.That(Vector3.Distance(body.transform.position,actual),Is.GreaterThan(.01f),"Real World movement, no manual Animator or Actor advance");
            Assert.That(Quaternion.Angle(upper,hand.localRotation),Is.LessThan(.1f));
            Assert.That(Quaternion.Angle(lower,leg.localRotation),Is.GreaterThan(1));
            // Find a real unfinished exit leg before the original Pause/fault sequence; never fabricate a callback.
            // 只在原 Pause 前准备真实 exit Native 腿；后续1+8暂停帧和原fault原样保留。
            var faultVisits=(IDictionary)CarryRead(customers,"visits");var faultVisit=faultVisits[id];var faultAdmission=CarryRead(faultVisit,"Admission");
            var faultMove=CarryRead(faultVisit,"TrafficMove");
            TestContext.WriteLine("CARRY_EXIT_FAULT_SOURCE phase="+CarryRead(faultVisit,"State")+" collected="+CarryRead(faultVisit,"HasCollected")+" actualMove="+(faultMove!=null));
            Assert.That(faultMove,Is.Not.Null,"EXIT_FAULT_PREPARATION_NOT_PROVEN: actual original exit movement required");
            var faultCore=(ServiceTrafficCoordinator)CarryRead(faultMove,"Coordinator");var faultKey=(TrafficIntentKey)CarryRead(faultMove,"Intent");
            var faultEntries=(IDictionary)CarryRead(faultCore,"entries");Assert.That(faultEntries.Contains(faultKey),Is.True,"EXIT_FAULT_PREPARATION_NOT_PROVEN: actual original exit Entry required");
            var faultEntry=faultEntries[faultKey];var faultFirst=(int)CarryRead(faultEntry,"FirstFrame");var faultQueries=(int)CarryRead(faultEntry,"Queries");
            var faultSequence=(long)CarryRead(faultEntry,"RequestSequence");var faultAcquisition=(long)CarryRead(faultEntry,"AcquiredSequence");
            var faultLimit=(float)CarryRead(faultEntry,"FiniteLimit");var faultCount=(int)CarryRead(faultEntry,"CandidateCount");
            var faultRequests=(IDictionary)CarryRead(world.Service,"byActor");object faultRequest=null,faultLeg=null,faultSource=null;TrafficNativeStamp faultStamp=default;
            void OriginalExitSource()
            {
                Assert.That(faultVisits.Contains(id) && ReferenceEquals(faultVisits[id],faultVisit),Is.True,"EXIT_FAULT_PREPARATION_NOT_PROVEN: original visit exited or changed");
                Assert.That(CarryRead(faultVisit,"Actor"),Is.SameAs(body));Assert.That(CarryRead(faultVisit,"Admission"),Is.SameAs(faultAdmission));
                Assert.That(CarryRead(faultVisit,"State"),Is.EqualTo(CustomerVisitState.Exiting));Assert.That(CarryRead(faultVisit,"HasCollected"),Is.EqualTo(true));
                Assert.That(CarryRead(faultVisit,"TrafficMove"),Is.SameAs(faultMove));Assert.That(CarryRead(faultMove,"Coordinator"),Is.SameAs(faultCore));
                Assert.That(CarryRead(customers,"traffic"),Is.SameAs(faultCore));Assert.That(CarryRead(faultMove,"Intent"),Is.EqualTo(faultKey));Assert.That(faultEntries[faultKey],Is.SameAs(faultEntry));
                Assert.That(CarryRead(faultEntry,"Retired"),Is.EqualTo(false));Assert.That(CarryRead(faultEntry,"FirstFrame"),Is.EqualTo(faultFirst));
                Assert.That(((TrafficDecision)CarryRead(faultEntry,"Decision")).FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None));
                Assert.That(CarryRead(faultEntry,"FiniteLimit"),Is.EqualTo(faultLimit));Assert.That(CarryRead(faultEntry,"CandidateCount"),Is.EqualTo(faultCount));
                var nextSequence=(long)CarryRead(faultEntry,"RequestSequence");if(faultSequence==0)faultSequence=nextSequence;Assert.That(nextSequence,Is.EqualTo(faultSequence));
                var nextAcquisition=(long)CarryRead(faultEntry,"AcquiredSequence");if(faultAcquisition==0)faultAcquisition=nextAcquisition;Assert.That(nextAcquisition,Is.EqualTo(faultAcquisition));
                var nextQueries=(int)CarryRead(faultEntry,"Queries");Assert.That(nextQueries,Is.GreaterThanOrEqualTo(faultQueries));faultQueries=nextQueries;
                Assert.That(faultQueries,Is.LessThanOrEqualTo((int)CarryRead(faultCore,"queriesPerIntent")));
                Assert.That(HeldCups(body).Single(),Is.SameAs(cup));Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,token)).State,Is.Not.EqualTo(ReservationState.Released));
            }
            bool LatchCurrentExitLeg()
            {
                if(!faultRequests.Contains(id))return false;
                var candidate=faultRequests[id];var candidateLeg=CarryRead(faultEntry,"Leg");if(candidateLeg==null)return false;
                var native=(TrafficNativeStamp)CarryRead(candidateLeg,"Stamp");var requestId=(long)CarryRead(candidate,"Id");
                if(!(bool)CarryRead(candidateLeg,"Committed") || !(bool)CarryRead(candidateLeg,"Bound") || (bool)CarryRead(candidateLeg,"Completed") ||
                    !ReferenceEquals(CarryRead(candidateLeg,"ActorLife"),body) || native.RequestId!=requestId ||
                    native.LegGeneration!=(int)CarryRead(faultMove,"NativeGeneration") || (long)CarryRead(faultVisit,"RequestId")!=requestId ||
                    (int)CarryRead(faultVisit,"Generation")!=native.LegGeneration || CarryRead(faultMove,"Source")==null)return false;
                faultRequest=candidate;faultLeg=candidateLeg;faultStamp=native;faultSource=CarryRead(faultMove,"Source");return true;
            }
            var faultPreparationFrames=0;OriginalExitSource();
            while(faultPreparationFrames<6000 && !LatchCurrentExitLeg())
            {
                Assert.That(owner.CanProgress && body.isActiveAndEnabled && time.CurrentSpeed!=GameSpeed.Paused,Is.True,"EXIT_FAULT_PREPARATION_NOT_PROVEN: no legitimate normal frames");
                OriginalExitSource();yield return null;faultPreparationFrames++;
            }
            TestContext.WriteLine("CARRY_EXIT_FAULT_ELIGIBILITY source="+CarryKey(faultKey)+" prepFrames="+faultPreparationFrames+" actualBoundUnfinishedNative="+(faultRequest!=null)+
                " phase="+CarryRead(faultVisit,"State")+" requestPresent="+faultRequests.Contains(id)+" first="+faultFirst+" queries="+faultQueries);
            Assert.That(faultRequest,Is.Not.Null,"EXIT_FAULT_PREPARATION_NOT_PROVEN: actual same-source bound unfinished Native leg was never available");
            void SameFaultLeg()
            {
                OriginalExitSource();Assert.That(faultRequests.Contains(id),Is.True,"EXIT_FAULT_PREPARATION_NOT_PROVEN: original Native leg finished before fault");
                Assert.That(faultRequests[id],Is.SameAs(faultRequest));Assert.That(CarryRead(faultEntry,"Leg"),Is.SameAs(faultLeg));Assert.That(CarryRead(faultMove,"Source"),Is.SameAs(faultSource));
                Assert.That((TrafficNativeStamp)CarryRead(faultLeg,"Stamp"),Is.EqualTo(faultStamp));Assert.That(CarryRead(faultLeg,"Completed"),Is.EqualTo(false));
                Assert.That(CarryRead(faultLeg,"Committed"),Is.EqualTo(true));Assert.That(CarryRead(faultLeg,"Bound"),Is.EqualTo(true));
                Assert.That(CarryRead(faultVisit,"RequestId"),Is.EqualTo(faultStamp.RequestId));Assert.That(CarryRead(faultMove,"NativeGeneration"),Is.EqualTo(faultStamp.LegGeneration));
            }
            SameFaultLeg();
            TestContext.WriteLine("CARRY_EXIT_FAULT_PREP source="+CarryKey(faultKey)+" first="+faultFirst+" prepFrames="+faultPreparationFrames+
                " request="+faultStamp.RequestId+" generation="+faultStamp.LegGeneration+" queries="+faultQueries+" actorLifeSame=True boundUnfinished=True");
            time.SetPaused(); yield return null;
            var paused=body.transform.position; var phase=animator.GetCurrentAnimatorStateInfo(0).normalizedTime; var carryPhase=animator.GetCurrentAnimatorStateInfo(1).normalizedTime;
            for(var f=0;f<8;f++) yield return null;
            Assert.That(body.transform.position,Is.EqualTo(paused)); Assert.That(Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime-phase),Is.LessThan(.001f));
            Assert.That(Mathf.Abs(animator.GetCurrentAnimatorStateInfo(1).normalizedTime-carryPhase),Is.LessThan(.001f));
            SameFaultLeg(); // After the exact original Pause frames, before the real lifecycle fault.
            var callbackField=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
            Assert.That(callbackField,Is.Not.Null,"EXIT_FAULT_PREPARATION_NOT_PROVEN: existing readonly original callback observer required");
            var priorCallback=callbackField.GetValue(customers);var callbackErrors=new System.Collections.Generic.List<string>();
            var callbackCount=0;var callbackOwned=false;var callbackInactive=false;var callbackFailure=false;var callbackRaw="NOT_OBSERVED";
            TrafficNativeStamp callbackStamp=default;object callbackActor=null;var callbackPhase="UNKNOWN";
            void OriginalFaultCallback(string operation,string actorId,string related,Vector3 target,System.Collections.Generic.IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                if(operation!="segment.callback-result" || actorId!=id)return;
                try
                {
                    string Part(string name)=>result.Split(' ').Single(p=>p.StartsWith(name+"=",StringComparison.Ordinal)).Substring(name.Length+1);
                    var originalRequest=long.Parse(Part("request"),System.Globalization.CultureInfo.InvariantCulture);
                    if(originalRequest!=faultStamp.RequestId){callbackErrors.Add("Original actor callback belonged to another request: "+result);return;}
                    callbackCount++;callbackRaw=result;callbackActor=CarryRead(faultVisit,"Actor");callbackPhase=CarryRead(faultVisit,"State").ToString();
                    callbackStamp=(TrafficNativeStamp)CarryRead(faultLeg,"Stamp");callbackInactive=!body.isActiveAndEnabled;
                    callbackFailure=Part("status")==MovementStatus.Failed.ToString() && Part("reason")==NavigationFailure.ActorUnavailable.ToString();
                    callbackOwned=faultVisits.Contains(id) && ReferenceEquals(faultVisits[id],faultVisit) && ReferenceEquals(callbackActor,body) &&
                        ReferenceEquals(CarryRead(faultVisit,"TrafficMove"),faultMove) && ReferenceEquals(faultEntries[faultKey],faultEntry) &&
                        ReferenceEquals(CarryRead(faultEntry,"Leg"),faultLeg) && ReferenceEquals(CarryRead(faultMove,"Source"),faultSource) &&
                        ReferenceEquals(CarryRead(faultLeg,"ActorLife"),body) && callbackStamp.Equals(faultStamp) &&
                        (int)CarryRead(faultVisit,"Generation")==faultStamp.LegGeneration && (long)CarryRead(faultVisit,"RequestId")==faultStamp.RequestId &&
                        int.Parse(Part("issuedGeneration"),System.Globalization.CultureInfo.InvariantCulture)==faultStamp.LegGeneration;
                }
                catch(Exception error){if(callbackErrors.Count<4)callbackErrors.Add("UNKNOWN: "+error.GetType().Name+" "+error.Message);}
            }
            var combinedCallback=Delegate.Combine(priorCallback as Delegate,(Action<string,string,string,Vector3,System.Collections.Generic.IReadOnlyList<Vector3>,Vector3[],string>)OriginalFaultCallback);
            var observerRestored=false;Action restoreFaultObserver=null;
            restoreFaultObserver=()=>
            {
                if(observerRestored)return;observerRestored=true;
                try
                {
                    if(ReferenceEquals(callbackField.GetValue(customers),combinedCallback))callbackField.SetValue(customers,priorCallback);
                    else TestContext.WriteLine("CARRY_EXIT_FAULT_OBSERVER_CLEANUP status=UNKNOWN reason=observer-replaced");
                }
                catch(Exception error){TestContext.WriteLine("CARRY_EXIT_FAULT_OBSERVER_CLEANUP status=UNKNOWN error="+error.GetType().Name+" "+error.Message);}
                finally {if(ReferenceEquals(carryFaultObserverCleanup,restoreFaultObserver))carryFaultObserverCleanup=null;}
            };
            carryFaultObserverCleanup=restoreFaultObserver;
            try
            {
                // Only observer registration changes; the callback copies facts and never asserts or mutates production state.
                callbackField.SetValue(customers,combinedCallback);
                body.enabled=false; yield return null;
                TestContext.WriteLine("CARRY_EXIT_FAULT_CALLBACK source="+CarryKey(faultKey)+" expectedRequest="+faultStamp.RequestId+
                    " expectedGeneration="+faultStamp.LegGeneration+" observed="+callbackCount+" owned="+callbackOwned+" actorLifeSame="+ReferenceEquals(callbackActor,body)+
                    " actorInactive="+callbackInactive+" actualStamp="+callbackStamp.RequestId+"/"+callbackStamp.LegGeneration+" originalPhase="+callbackPhase+
                    " actualAfter="+CarryRead(faultVisit,"State")+" result="+callbackRaw+" errors="+string.Join(" | ",callbackErrors));
                Assert.That(callbackErrors,Is.Empty,"EXIT_FAULT_PREPARATION_NOT_PROVEN: original read-only callback capture errors");
                Assert.That(callbackCount,Is.EqualTo(1),"EXIT_FAULT_PREPARATION_NOT_PROVEN: actual original Native terminal callback required; absence is not product proof");
                Assert.That(callbackOwned && callbackInactive && callbackFailure,Is.True,"EXIT_FAULT_PREPARATION_NOT_PROVEN: actual same-source Failed/ActorUnavailable and inactive original actor required");
                Assert.That(customers.Snapshot.Single(v=>v.VisitId==id).State,Is.EqualTo(CustomerVisitState.Blocked));
            }
            finally {restoreFaultObserver();}
            Assert.That(HeldCups(body).Single(),Is.SameAs(cup));
            body.gameObject.SetActive(false); yield return null; body.gameObject.SetActive(true); yield return null;
            Assert.That(HeldCups(body).Single(),Is.SameAs(cup),"Disable/re-enable must retain the same cup, not replace it");
            time.SetNormal(); yield return Until(()=>employees.Snapshot.All(e=>e.State==EmployeeState.Idle),true); time.SetPaused();
            employees.TryGetBinding(EmployeeRole.Barista,out var binding); var epoch=owner.Snapshot.Epoch;
            decor.EnterDecorationMode();
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,binding.WorkStationId),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True); Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True); decor.ExitDecorationMode();
            yield return null;
            Assert.That(owner.Snapshot.Epoch,Is.EqualTo(epoch+1)); Assert.That(owner.Snapshot.Orders,Is.Empty);
            Assert.That(customers.Snapshot.Select(v=>v.VisitId),Is.EqualTo(new[]{id}),"Uncollected guest clears; collected body and hand cup survive Done");
            Assert.That(world.RegisteredActors.Contains(body),Is.True); Assert.That(body.transform.position,Is.EqualTo(paused)); Assert.That(HeldCups(body).Single(),Is.SameAs(cup));
            Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,token)).State,Is.Not.EqualTo(ReservationState.Released));
            var exit=CafeLoopRuntimeProof.Field<Transform>(customers,"exit"); var exited=false; var observing=true;
            var exitObservationStopped=false;Action stopActualExitObservation=null;
            stopActualExitObservation=()=>
            {
                if(exitObservationStopped)return;exitObservationStopped=true;observing=false;
                if(ReferenceEquals(carryActualExitObserverCleanup,stopActualExitObservation))carryActualExitObserverCleanup=null;
            };
            carryActualExitObserverCleanup=stopActualExitObservation;
            var recoveryCheckpointLogged=false;var recoveryStartFrame=0;
            var recoveryTraceEnabled=TestContext.CurrentContext.Test.Name==nameof(ShibaCollectedCupSurvivesPauseBlockedDisableAndChangedDoneUntilActualExit);
            var recoveryTraceArmed=false;var recoveryTraceObserved=false;var recoveryTraceMissingReported=false;var recoveryTraceInstalled=false;
            Action restoreRecoveryCommitTrace=null;
            void ReportMissingRecoveryCommitTrace()
            {
                if(!recoveryTraceEnabled || !recoveryTraceArmed || recoveryTraceObserved || recoveryTraceMissingReported)return;
                recoveryTraceMissingReported=true;
                TestContext.WriteLine("CARRY_RESUME_PRIMARY_PRECOMMIT status="+(recoveryTraceInstalled?"NOT_OBSERVED":"UNKNOWN(observer-not-installed)")+" frame="+Time.frameCount);
            }
            void RecoveryCommitOperands(string operation,string actorId,string context,Vector3 target,System.Collections.Generic.IReadOnlyList<Vector3> path,Vector3[] obstacles,string original)
            {
                if(!recoveryTraceEnabled || !recoveryTraceArmed || recoveryTraceObserved || operation!="traffic.exit-primary-precommit" || actorId!=id)return;
                recoveryTraceObserved=true; // Cap1: original actual resumed candidate, never a later attempt.
                try
                {
                    string Part(string name)=>original.Split(' ').Single(part=>part.StartsWith(name+"=",StringComparison.Ordinal)).Substring(name.Length+1);
                    object R(object value,string name)
                    {
                        if(value==null)return null;
                        var type=value.GetType();var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
                        var field=type.GetField(name,flags);return field!=null?field.GetValue(value):type.GetProperty(name,flags)?.GetValue(value)??"UNKNOWN:"+name;
                    }
                    string Ref(object value)=>value==null?"NULL":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
                    string Fields(object value,params string[] names)=>string.Join(" ",names.Select(name=>name+"="+(R(value,name)??"NULL")));
                    string Body(TrafficBodyFact value)=>value==null?"NULL":"owner="+CarryKey(value.Owner)+" life="+Ref(value.ActorLife)+" pos="+value.Position.ToString("F9")+" enabled="+value.Enabled+" active="+value.ActorActive;
                    var key=new TrafficIntentKey(Part("session"),long.Parse(Part("epoch"),System.Globalization.CultureInfo.InvariantCulture),Part("actor"),Part("intent"),
                        int.Parse(Part("generation"),System.Globalization.CultureInfo.InvariantCulture),int.Parse(Part("layout"),System.Globalization.CultureInfo.InvariantCulture),context);
                    var core=(ServiceTrafficCoordinator)R(customers,"traffic");var table=(IDictionary)R(core,"entries");var entry=table.Contains(key)?table[key]:null;
                    var request=R(entry,"Request") as TrafficRequest;var decision=R(entry,"Decision") as TrafficDecision;
                    var current=R(core,"snapshot") as TrafficWorldSnapshot;var retained=request?.Snapshot;
                    var currentBody=current?.Bodies.FirstOrDefault(value=>value.Owner.ActorId==key.ActorId);var retainedBody=retained?.Bodies.FirstOrDefault(value=>value.Owner.ActorId==key.ActorId);
                    var currentVisit=((IDictionary)R(customers,"visits"))[id];var actualActor=R(currentVisit,"Actor") as NavigationActor;var agent=actualActor==null?null:actualActor.Agent;
                    var scope=key.Session==(string)R(core,"session") && key.Epoch==(long)R(core,"epoch") && key.LayoutRevision==(int)R(core,"layoutRevision");
                    var snapshotMatches=request!=null && retained!=null && current!=null && retained.Version==current.Version && retained.BodyVersion==current.BodyVersion;
                    var leg=R(entry,"Leg");var legLife=R(leg,"ActorLife");
                    TestContext.WriteLine("CARRY_RESUME_PRIMARY_PRECOMMIT status=OBSERVED frame="+Time.frameCount+" key="+CarryKey(key)+" original={"+original+"}"+
                        " core="+Ref(core)+" entry="+Ref(entry)+" decision="+Ref(decision)+" request="+Ref(request)+" prepared="+R(entry,"Prepared")+
                        " header={"+Fields(core,"closed","committing")+" "+Fields(entry,"Retired","Queued","Active","AcquiredSequence")+"}"+
                        " decisionFacts={"+Fields(decision,"Kind","FailureDisposition","RequestSequence","AcquiredSequence")+"}"+
                        " requestKey="+(request==null?"NULL":CarryKey(request.Intent))+" actualActor="+Ref(actualActor)+" originalBodySame="+ReferenceEquals(actualActor,body)+
                        " enabled="+(actualActor!=null && actualActor.enabled)+" active="+(actualActor!=null && actualActor.isActiveAndEnabled)+
                        " registered="+(actualActor!=null && world.RegisteredActors.Contains(actualActor))+" agentEnabled="+(agent!=null && agent.enabled)+" agentOnMesh="+(agent!=null && agent.enabled && agent.isOnNavMesh));
                    TestContext.WriteLine("CARRY_RESUME_PRIMARY_OPERANDS frame="+Time.frameCount+" rawScopeCurrent="+scope+" rawSnapshotCurrent="+snapshotMatches+
                        " retainedSnapshot="+Ref(retained)+"/"+(retained==null?"NULL":retained.Version+"/"+retained.BodyVersion)+
                        " currentSnapshot="+Ref(current)+"/"+(current==null?"NULL":current.Version+"/"+current.BodyVersion)+
                        " requestStart="+(request==null?"NULL":request.Start.ToString("F9"))+" requestTarget="+(request==null?"NULL":request.Target.ToString("F9"))+
                        " requestFrame="+(request==null?"NULL":request.RequestedFrame.ToString())+" retainedOwn={"+Body(retainedBody)+"} currentOwn={"+Body(currentBody)+"}"+
                        " rawOwnBodyDisabled="+(currentBody!=null && !currentBody.Enabled)+" rawOwnActorInactive="+(currentBody!=null && !currentBody.ActorActive)+
                        " rawStartMismatch="+(currentBody!=null && request!=null && currentBody.Position!=request.Start)+" leg="+Ref(leg)+" legLife="+Ref(legLife)+
                        " bodyLifeMatchesLeg="+(currentBody!=null && ReferenceEquals(currentBody.ActorLife,legLife))+" mutationArgument=NULL"+
                        " budget={"+Fields(entry,"FirstFrame","Queries","RequestSequence","AcquiredSequence","CandidateCount","CurrentIndex","FiniteLimit","WaitRemaining")+"}");
                    TestContext.WriteLine("CARRY_RESUME_PRIMARY_SNAPSHOT_BODIES retained=["+(retained==null?"NULL":string.Join(";",retained.Bodies.Select(Body)))+
                        "] current=["+(current==null?"NULL":string.Join(";",current.Bodies.Select(Body)))+"]");
                }
                catch(Exception error){TestContext.WriteLine("CARRY_RESUME_PRIMARY_PRECOMMIT status=UNKNOWN frame="+Time.frameCount+" original={"+original+"} error="+error.GetType().Name+" "+error.Message);}
            }
            void RecoveryContext(string stage)
            {
                if(stage=="resumeUntil-preCleanup")ReportMissingRecoveryCommitTrace();
                object R(object value,string name)
                {try{if(value==null)return null;var type=value.GetType();var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;var field=type.GetField(name,flags);return field!=null?field.GetValue(value):type.GetProperty(name,flags)?.GetValue(value)??"UNKNOWN:"+name;}catch(Exception error){return "UNKNOWN:"+name+"/"+error.GetType().Name;}}
                string F(object value,params string[] names)=>string.Join(" ",names.Select(name=>name+"="+(R(value,name)??"NULL")));
                try
                {
                    var currentVisits=R(customers,"visits") as IDictionary;var actualVisit=currentVisits!=null && currentVisits.Contains(id)?currentVisits[id]:null;
                    var move=R(actualVisit,"TrafficMove");var actualCore=R(move,"Coordinator") as ServiceTrafficCoordinator ?? R(customers,"traffic") as ServiceTrafficCoordinator;
                    var keyValue=R(move,"Intent");if(!(keyValue is TrafficIntentKey))keyValue=R(actualVisit,"TrafficJourney");
                    var actualEntries=R(actualCore,"entries") as IDictionary;
                    var actualEntry=keyValue is TrafficIntentKey currentKey && actualEntries!=null && actualEntries.Contains(currentKey)?actualEntries[currentKey]:null;
                    var route=R(actualVisit,"Route") as System.Collections.Generic.IEnumerable<Vector3>;
                    var routeText=route==null?"NULL/UNKNOWN":string.Join(";",route.Select(point=>point.ToString("F6")));
                    var agent=body==null?null:body.Agent;var agentReady=agent!=null && agent.enabled && agent.isOnNavMesh;
                    var nativeTable=R(world.Service,"byActor") as IDictionary;var native=nativeTable!=null && nativeTable.Contains(id)?nativeTable[id]:null;
                    TestContext.WriteLine("CARRY_EXIT_RECOVERY_CONTEXT stage="+stage+" frame="+Time.frameCount+" exited="+exited+
                        " visit={"+F(actualVisit,"State","ResumeState","Failure","HasCollected","RequestId","Generation","TrafficMovePending","PendingExitSegment")+"}"+
                        " actorEnabled="+(body!=null && body.enabled)+" actorActive="+(body!=null && body.isActiveAndEnabled)+
                        " registered="+(body!=null && world.RegisteredActors.Contains(body))+" agentEnabled="+(agent!=null && agent.enabled)+" navAgentReady="+agentReady+
                        " route=["+routeText+"] move={"+F(move,"Index","NativeGeneration","Suspended","PreparationFrame","Goal","Target")+"}"+
                        " key="+(keyValue is TrafficIntentKey actualKey?CarryKey(actualKey):"NULL/UNKNOWN")+
                        " core={"+F(actualCore,"closed","paused","session","epoch","layoutRevision")+"}"+
                        " decision={"+F(R(actualEntry,"Decision"),"Kind","FailureDisposition","Reason","RequestSequence","AcquiredSequence")+"}"+
                        " budget={"+F(actualEntry,"FirstFrame","Queries","RequestSequence","AcquiredSequence","CandidateCount","CurrentIndex","FiniteLimit","WaitRemaining","Retired","Queued","Prepared","Active")+"}"+
                        " leg={"+F(R(actualEntry,"Leg"),"Committed","Bound","Completed","Suspended","Generation")+"}"+
                        " native={"+F(native,"Id","Generation","RetryCount","PendingSeconds","SegmentSeconds","NoProgressSeconds")+"}"+
                        " loopCanProgress="+owner.CanProgress+" flowActive="+customers.isActiveAndEnabled+" flowHeld="+R(customers,"integratedHeld")+
                        " timeSpeed="+time.CurrentSpeed+" timeResumeBlocked="+time.IsResumeBlocked+" resumeReason="+time.ResumeBlockReason+
                        " staff="+string.Join(";",employees.Snapshot.Select(e=>e.Role+"/"+e.State+"/"+e.Failure)));
                }
                catch(Exception error){TestContext.WriteLine("CARRY_EXIT_RECOVERY_CONTEXT stage="+stage+" frame="+Time.frameCount+" status=UNKNOWN error="+error.GetType().Name+" "+error.Message);}
            }
            bool RecoveryDone()
            {
                var done=exited && employees.Snapshot.All(e=>e.State==EmployeeState.Idle);
                if(!done && !recoveryCheckpointLogged && (customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked) ||
                    employees.Snapshot.Any(e=>e.State==EmployeeState.Blocked) || Time.frameCount-recoveryStartFrame>=6000))
                {recoveryCheckpointLogged=true;RecoveryContext("resumeUntil-preCleanup");}
                return done;
            }
            var observer=body.gameObject.AddComponent<CafeActualExitObserver>(); observer.VisitId=id;
            observer.Receipt=(visit,position)=>
            {
                if(!observing) return;
                Assert.That(visit,Is.EqualTo(id)); Assert.That(Vector3.Distance(position,exit.position),Is.LessThanOrEqualTo(.081f));
                Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,token)).State,Is.EqualTo(ReservationState.Released)); exited=true;
            };
            if(recoveryTraceEnabled)
            {
                var recoveryHook=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
                if(recoveryHook==null)TestContext.WriteLine("CARRY_RESUME_PRIMARY_PRECOMMIT status=UNKNOWN reason=missing-existing-observer");
                else
                {
                    var previousRecoveryHook=recoveryHook.GetValue(customers);
                    var combinedRecoveryHook=Delegate.Combine(previousRecoveryHook as Delegate,(Action<string,string,string,Vector3,System.Collections.Generic.IReadOnlyList<Vector3>,Vector3[],string>)RecoveryCommitOperands);
                    var recoveryHookRestored=false;
                    restoreRecoveryCommitTrace=()=>
                    {
                        if(recoveryHookRestored)return;recoveryHookRestored=true;ReportMissingRecoveryCommitTrace();recoveryTraceArmed=false;
                        try
                        {
                            if(ReferenceEquals(recoveryHook.GetValue(customers),combinedRecoveryHook))recoveryHook.SetValue(customers,previousRecoveryHook);
                            else TestContext.WriteLine("CARRY_RESUME_PRIMARY_CLEANUP status=UNKNOWN reason=observer-replaced");
                        }
                        catch(Exception error){TestContext.WriteLine("CARRY_RESUME_PRIMARY_CLEANUP status=UNKNOWN error="+error.GetType().Name);}
                        finally {if(ReferenceEquals(carryFaultObserverCleanup,restoreRecoveryCommitTrace))carryFaultObserverCleanup=null;}
                    };
                    carryFaultObserverCleanup=restoreRecoveryCommitTrace; // Reuse existing TearDown fallback after old fault hook restored.
                    recoveryHook.SetValue(customers,combinedRecoveryHook);recoveryTraceInstalled=true;
                }
            }
            try
            {
                recoveryStartFrame=Time.frameCount;RecoveryContext("beforeResume");
                recoveryTraceArmed=recoveryTraceEnabled;
                body.enabled=true; time.SetNormal(); tools.ResumeBlockedFlow();
                RecoveryContext("syncAfterResume");
                yield return Until(RecoveryDone); yield return null;
                if(!recoveryCheckpointLogged){recoveryCheckpointLogged=true;RecoveryContext("resumeUntil-preCleanup");}
                Assert.That(cup==null,Is.True,"Same actual actor exit destroys its owned cup"); Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                TestContext.WriteLine("P14_CUSTOMER_CARRY "+species+" actualWalk/Pause/Blocked/disabled/Done/sameCup/actualExit=True");
            }
            finally { stopActualExitObservation();restoreRecoveryCommitTrace?.Invoke(); }
        }
        private static object CarryRead(object value,string name)=>value==null?null:value.GetType().GetField(name,
            BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(value);
        private static string CarryKey(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+
            "/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
        private static Transform[] HeldCups(NavigationActor actor) => actor.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="CustomerHeldCup").ToArray();
    }
}
#endif

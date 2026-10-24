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
            if(scene.IsValid() && scene.isLoaded)
            { SceneManager.SetActiveScene(SceneManager.CreateScene("P14 carry cleanup")); yield return SceneManager.UnloadSceneAsync(scene); }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputs ?? Array.Empty<UnityEngine.InputSystem.InputActionAsset>());
            Time.captureDeltaTime=capture; Time.timeScale=scale;
        }

        [UnityTest] public IEnumerator SuccessfulCreateMovesActualCustomerBeforeBaristaFinishesWorking()
        {
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==id).CounterArrived);
            var counter=customers.Snapshot.Single().Position;
            yield return Until(()=>owner.Snapshot.Orders.Count==1);
            Assert.That(customers.Snapshot.Single().State,Is.EqualTo(CustomerVisitState.MovingToPickup),"Successful Create requests Pickup in the same business step");
            yield return Until(()=>employees.Snapshot.Any(e=>e.Role==EmployeeRole.Barista && e.State==EmployeeState.Working) &&
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
            var request=requests[id]; Assert.That(request,Is.Not.Null,"Successful payment sends the real Pickup movement immediately");
            var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
            object Read(string name)=>request.GetType().GetField(name,flags).GetValue(request);
            var requestId=(long)Read("Id"); var observedCertificate=false;
            for(var frame=0;frame<1200 && customers.CounterAssignments.Any(a=>a.VisitId==id);frame++)
            {
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
                Assert.That(customers.TrySpawn(),Is.True); tools.ResumeCollection();
                yield return Until(()=>owner.Snapshot.Orders.Count==1 && customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                tools.HoldCollection();
            }
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            var id=customers.Snapshot.Single().VisitId; var body=world.RegisteredActors.Single(a=>a.ActorId==id);
            Assert.That(body.gameObject.name,Does.Contain(species));
            var token=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token;
            yield return Until(()=>owner.Snapshot.Delivery.Any(d=>owner.Snapshot.Orders[d.Key].CustomerId==id) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(HeldCups(body),Is.Empty,"Uncollected waiting customers have no hand cup");
            Assert.That(body.ModelAnimator.GetLayerWeight(1),Is.Zero);
            Assert.That(body.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Working"),Is.False);
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
            time.SetPaused(); yield return null;
            var paused=body.transform.position; var phase=animator.GetCurrentAnimatorStateInfo(0).normalizedTime; var carryPhase=animator.GetCurrentAnimatorStateInfo(1).normalizedTime;
            for(var f=0;f<8;f++) yield return null;
            Assert.That(body.transform.position,Is.EqualTo(paused)); Assert.That(Mathf.Abs(animator.GetCurrentAnimatorStateInfo(0).normalizedTime-phase),Is.LessThan(.001f));
            Assert.That(Mathf.Abs(animator.GetCurrentAnimatorStateInfo(1).normalizedTime-carryPhase),Is.LessThan(.001f));
            body.enabled=false; yield return null;
            Assert.That(customers.Snapshot.Single(v=>v.VisitId==id).State,Is.EqualTo(CustomerVisitState.Blocked));
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
            var observer=body.gameObject.AddComponent<CafeActualExitObserver>(); observer.VisitId=id;
            observer.Receipt=(visit,position)=>
            {
                if(!observing) return;
                Assert.That(visit,Is.EqualTo(id)); Assert.That(Vector3.Distance(position,exit.position),Is.LessThanOrEqualTo(.081f));
                Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,token)).State,Is.EqualTo(ReservationState.Released)); exited=true;
            };
            try
            {
                body.enabled=true; time.SetNormal(); tools.ResumeBlockedFlow();
                yield return Until(()=>exited && employees.Snapshot.All(e=>e.State==EmployeeState.Idle)); yield return null;
                Assert.That(cup==null,Is.True,"Same actual actor exit destroys its owned cup"); Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                TestContext.WriteLine("P14_CUSTOMER_CARRY "+species+" actualWalk/Pause/Blocked/disabled/Done/sameCup/actualExit=True");
            }
            finally { observing=false; }
        }
        private static Transform[] HeldCups(NavigationActor actor) => actor.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="CustomerHeldCup").ToArray();
    }
}
#endif

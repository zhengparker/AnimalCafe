#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.CafeLoop;
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
    public sealed class CafeLoopIntegrationTests
    {
        private Scene scene;
        private CustomerFlowController customers;
        private EmployeeFlowController employees;
        private CafeLoopController owner;
        private PickupCupDisplay cups;
        private GameTimeService time;
        private NavigationWorld world;
        private NavigationLayoutAdapter adapter;
        private UnityEngine.InputSystem.InputActionAsset[] inputAssets;
        private float capture,scale;
        [UnitySetUp] public IEnumerator Setup()
        {
            capture=Time.captureDeltaTime; scale=Time.timeScale; Time.captureDeltaTime=1f/60;
            var longRun=TestContext.CurrentContext.Test.Name.StartsWith("CompletesTwenty");
            scene=EditorSceneManager.LoadSceneInPlayMode(longRun?"Assets/Scenes/Validation/Phase14CafeLoop.unity":"Assets/Scenes/Validation/Phase12CustomerQueue.unity",
                new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
            customers=Find<CustomerFlowController>(); customers.AutoSpawn=false; world=Find<NavigationWorld>();
            adapter=Find<NavigationLayoutAdapter>(); time=Find<GameTimeService>(); var layout=Find<CafeLayoutRuntime>();
            var decoration=Find<DecorationModeController>(); inputAssets=Phase8SceneInputTestCleanup.CaptureAssets(scene);
            if(longRun)
            {
                owner=Find<CafeLoopController>(); employees=Find<EmployeeFlowController>(); cups=Find<PickupCupDisplay>();
                yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle),600);
                yield break;
            }
            for(var f=0;f<120 && string.IsNullOrEmpty(customers.SelectedRegisterId);f++) yield return null;
            Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True,time.ResumeBlockReason);
            var host=new GameObject("P14 connected loop fixture"); SceneManager.MoveGameObjectToScene(host,scene);
            cups=host.AddComponent<PickupCupDisplay>(); employees=host.AddComponent<EmployeeFlowController>();
            var serialized=new SerializedObject(employees); serialized.FindProperty("cupPrefab").objectReferenceValue=
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase4/Prefabs/PF_Item_CeramicCup_01.prefab");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var grid=(Transform)new SerializedObject(decoration).FindProperty("gridRoot").objectReferenceValue;
            employees.Configure(world,adapter,layout,decoration,time,customers,grid,Prefab("Shiba"),Prefab("Westie"),cups);
            owner=host.AddComponent<CafeLoopController>(); owner.Configure(customers,employees,cups,time,adapter,decoration);
            yield return Until(()=>employees.Snapshot.All(e=>e.State==EmployeeState.Idle),600);
        }
        [UnityTest] public IEnumerator CompletesTwentyRealOrdersAndDrainsAllCapacityAtOneX()
            => CafeLoopRuntimeProof.Twenty(scene,owner,customers,employees,cups,time,world,adapter,GameSpeed.Normal);
        [UnityTest] public IEnumerator CompletesTwentyRealOrdersAndDrainsAllCapacityAtTwoX()
            => CafeLoopRuntimeProof.Twenty(scene,owner,customers,employees,cups,time,world,adapter,GameSpeed.Fast);
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(owner!=null) Object.Destroy(owner.gameObject); yield return null;
            if(scene.IsValid() && scene.isLoaded)
            { var empty=SceneManager.CreateScene("P14 loop cleanup"); SceneManager.SetActiveScene(empty); yield return SceneManager.UnloadSceneAsync(scene); }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets ?? Array.Empty<UnityEngine.InputSystem.InputActionAsset>());
            Time.captureDeltaTime=capture; Time.timeScale=scale;
        }
        [UnityTest] public IEnumerator RealCashierCoffeeCollectionAndActualExitDrainCapacity()
        {
            Assert.That(owner.CanProgress,Is.True);
            owner.HoldCollection=true; customers.AutoSpawn=true;
            yield return Until(()=>customers.Snapshot.Count>0); customers.AutoSpawn=false;
            yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.State==OrderState.ReadyForPickup));
            yield return Until(()=>customers.Snapshot.Any(v=>v.State==CustomerVisitState.PickupWaiting));
            Assert.That(customers.Snapshot.Any(v=>v.State==CustomerVisitState.PickupService || v.State==CustomerVisitState.MovingToPickupService),Is.False);
            yield return Until(()=>employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(owner.Snapshot.Delivery.Count,Is.EqualTo(1)); owner.HoldCollection=false;
            yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.State==OrderState.Completed));
            yield return Until(()=>customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1));
            Assert.That(owner.Snapshot.Delivery,Is.Empty); Assert.That(owner.Snapshot.CollectedExitVisitIds,Is.Empty);
            Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
            Assert.That(cups.VisibleOrders,Is.Empty);
            Assert.That(world.RegisteredActors.Count,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator OneSecondCashierOwnerPauseFastAndRecreatedRunnerKeepOnePayment()
        {
            owner.HoldCollection=true;
            var initial=-1f; var finished=0;
            Action<CafeTaskEvent> observe=e=>{
                if(e.Role!=EmployeeRole.Cashier)return;
                if(e.Kind==CafeTaskEventKind.Progress && initial<0) initial=e.Checkpoint.RemainingWorkSeconds;
                if(e.Kind==CafeTaskEventKind.ServiceFinished)finished++;
            };
            employees.IntegratedTaskEvent+=observe;
            try
            {
                Assert.That(customers.TrySpawn(),Is.True,customers.Status);
                yield return Until(()=>employees.Snapshot.Any(e=>e.Role==EmployeeRole.Cashier && e.State==EmployeeState.Working));
                Assert.That(initial,Is.EqualTo(1f),"Owner dispatch must really enter the one-second runner");
                var remaining=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).RemainingWorkSeconds;
                Assert.That(owner.Snapshot.Orders,Is.Empty);
                time.SetPaused();for(var i=0;i<5;i++){owner.Step(1);yield return null;}
                Assert.That(employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).RemainingWorkSeconds,Is.EqualTo(remaining));
                Assert.That(finished,Is.Zero);
                time.SetFast();
                // Owner input is already game time; Fast must not multiply it again.
                owner.Step(remaining-.25f);
                Assert.That(employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).RemainingWorkSeconds,Is.EqualTo(.25f).Within(.00001f));
                owner.enabled=false;employees.enabled=false;
                for(var i=0;i<4;i++)yield return null;
                employees.enabled=true;owner.enabled=true;owner.ResumeBlockedFlow();
                Assert.That(employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).RemainingWorkSeconds,Is.EqualTo(.25f).Within(.00001f));
                owner.Step(.125f);owner.Step(.115f);
                Assert.That(finished,Is.Zero,"At .99 total game seconds payment must not finish");
                Assert.That(owner.Snapshot.Orders,Is.Empty);
                owner.Step(.01f);
                Assert.That(finished,Is.EqualTo(1));
                owner.Step(.1f);owner.Step(.1f);
                Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1));
                Assert.That(owner.Snapshot.PaidVisitIds.Count,Is.EqualTo(1));
                Assert.That(finished,Is.EqualTo(1),"Pause/disable/recreation must not charge twice");
            }
            finally {employees.IntegratedTaskEvent-=observe;}
        }

        [UnityTest] public IEnumerator SingleDriverPauseAndDoubleSpeedKeepOneWorkingTimer()
        {
            Assert.That(owner.CanProgress,Is.True); customers.AutoSpawn=true;
            yield return Until(()=>employees.Snapshot.Any(e=>e.State==EmployeeState.Working)); customers.AutoSpawn=false;
            var before=employees.Snapshot.Single(e=>e.State==EmployeeState.Working);
            var ticks=0; owner.SnapshotChanged+=_=>{ ticks++; owner.Step(.5f); };
            owner.Step(.25f);
            var after=employees.Snapshot.Single(e=>e.ActorId==before.ActorId);
            Assert.That(after.RemainingWorkSeconds,Is.EqualTo(before.RemainingWorkSeconds-.25f).Within(.0001f));
            Assert.That(ticks,Is.EqualTo(1)); time.SetPaused(); var paused=after.RemainingWorkSeconds;
            for(var f=0;f<6;f++) { owner.Step(0); yield return null; }
            Assert.That(employees.Snapshot.Single(e=>e.ActorId==before.ActorId).RemainingWorkSeconds,Is.EqualTo(paused));
            Assert.That(time.TrySetSpeed(GameSpeed.Fast),Is.True);
            owner.Step(.1f);
            Assert.That(employees.Snapshot.Single(e=>e.ActorId==before.ActorId).RemainingWorkSeconds,
                Is.EqualTo(paused-.1f).Within(.0001f),"Fast input delta is already scaled once");
            yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.State==OrderState.Completed));
        }
        [UnityTest] public IEnumerator OwnerDisableRetainsModeAndResumesSameBusinessSession()
        {
            Assert.That(owner.CanProgress,Is.True); customers.AutoSpawn=true;
            yield return Until(()=>employees.Snapshot.Any(e=>e.State==EmployeeState.Working)); customers.AutoSpawn=false;
            var session=owner.Snapshot.SessionId; var ids=employees.Snapshot.Select(e=>e.ActorId).ToArray();
            owner.enabled=false; var remaining=owner.Snapshot.Employees.Select(e=>e.RemainingWorkSeconds).ToArray();
            for(var f=0;f<6;f++) yield return null;
            Assert.That(owner.Snapshot.Employees.Select(e=>e.RemainingWorkSeconds),Is.EqualTo(remaining));
            Assert.That(employees.TryStartTask(EmployeeRole.Cashier),Is.False); Assert.That(customers.TryLetFrontLeave(),Is.False);
            owner.enabled=true; owner.ResumeBlockedFlow();
            CollectionAssert.AreEqual(remaining,owner.Snapshot.Employees.Select(e=>e.RemainingWorkSeconds),"Real owner restore must preserve the remaining Working checkpoint before the next frame");
            Assert.That(owner.Snapshot.SessionId,Is.EqualTo(session));
            yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.State==OrderState.Completed));
            CollectionAssert.AreEqual(ids,employees.Snapshot.Select(e=>e.ActorId));
        }
        [UnityTest] public IEnumerator CustomerZeroDeltaCannotSubmitReplacementMovement()
        {
            customers.SetIntegratedMode(true); Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            yield return null; customers.enabled=false; customers.enabled=true;
            var before=NextRequestId(); customers.Step(0);
            Assert.That(NextRequestId(),Is.EqualTo(before),"Zero game time cannot submit a new ordinary movement");
        }
        [UnityTest] public IEnumerator PausedResumeCannotReleaseCustomerHoldOrSubmitMovement()
        {
            customers.SetIntegratedMode(true); Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            yield return null; customers.HoldIntegratedVisits(); time.SetPaused();
            var before=NextRequestId(); customers.ResumeBlockedVisits(); owner.ResumeBlockedFlow();
            Assert.That(NextRequestId(),Is.EqualTo(before),"Paused resume must keep owned movement retired");
        }
        [UnityTest] public IEnumerator InitialEmployeeFailureRetriesOnlyOnExplicitIntegratedResume()
        {
            var layout=Find<CafeLayoutRuntime>(); var decoration=Find<DecorationModeController>();
            var grid=(Transform)new SerializedObject(decoration).FindProperty("gridRoot").objectReferenceValue;
            employees.Configure(world,adapter,layout,decoration,time,customers,grid,null,Prefab("Westie"),cups);
            yield return null; yield return null;
            Assert.That(employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).State,Is.EqualTo(EmployeeState.Blocked));
            var serialized=new SerializedObject(employees); serialized.FindProperty("cashierPrefab").objectReferenceValue=Prefab("Shiba");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            for(var f=0;f<10;f++) yield return null;
            Assert.That(world.RegisteredActors.Any(a=>a.ActorId==owner.EmployeeId(EmployeeRole.Cashier)),Is.False,
                "Repair alone must not retry an explicitly failed initial spawn each frame");
            Assert.That(owner.CanProgress,Is.False); Assert.That(customers.AutoSpawn,Is.False);
            owner.ResumeBlockedFlow();
            yield return Until(()=>employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(owner.CanProgress,Is.True); Assert.That(world.RegisteredActors.Count,Is.EqualTo(2));
        }
        private long NextRequestId() => (long)typeof(NavigationService).GetField("nextId",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(world.Service);
        private T Find<T>() where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
        private static NavigationActor Prefab(string species) => AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Art/Phase13/Employees/{species}/PF_{species}_Employee.prefab").GetComponent<NavigationActor>();
        private IEnumerator Until(Func<bool> done,int max=3000)
        {
            for(var f=0;f<max && !done();f++) yield return null;
            Assert.That(done(),Is.True,customers.Status+" | "+owner.Snapshot.Failure+" | "+
                string.Join(" / ",employees.Snapshot.Select(e=>e.Role+" "+e.State+" "+e.Failure)));
        }
    }
}
#endif

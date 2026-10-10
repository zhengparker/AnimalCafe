using System.Linq;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using AnimalCafe.Customers;
using AnimalCafe.Navigation;

namespace AnimalCafe.Tests.PlayMode.Phase12Integration
{
    public class Phase12CustomerQueueSceneTests : InputTestFixture
    {
        private AsyncOperation pendingUnload;
        private float? nativeCaptureToRestore;
        private bool requireSteadyMotion;
        private bool requireCornerContinuity;
        private int concurrentAdvanceFrames;
        private int concurrentEntryFrames;
        [UnityTest] public IEnumerator NativeDefaultUnattendedOneSecondArrivalsDoNotResetFailedCountdown()
        { yield return NativeDefaultUnattendedArrivals(0); }
        [UnityTest] public IEnumerator NativeDefaultUnattendedTwoSecondArrivalsDoNotResetFailedCountdown()
        { yield return NativeDefaultUnattendedArrivals(.25f); }
        private IEnumerator NativeDefaultUnattendedArrivals(float intervalRandom)
        {
            nativeCaptureToRestore=Time.captureDeltaTime; Time.captureDeltaTime=1f/60;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            try
            {
                var roots=scene.GetRootGameObjects();
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CustomerFlowController>(true)).Single();
                var world=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationWorld>(true)).Single();
                flow.AutoSpawn=false;
                yield return WaitNative(flow,world,()=>!string.IsNullOrEmpty(flow.SelectedRegisterId));
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                // 固定允许范围内的间隔以便复现；不手动接纳、不挪动布局。
                typeof(CustomerFlowController).GetField("clock",flags).SetValue(flow,new CustomerSpawnClock(()=>intervalRandom));
                flow.AutoSpawn=true;
                var previousCount=0; var previousTimer=0f; var earlyFollowers=0;
                concurrentEntryFrames=0;
                yield return WaitNative(flow,world,()=>
                {
                    var count=flow.Snapshot.Count; var timer=flow.RemainingSeconds;
                    if(count==previousCount && count>=5)
                        Assert.That(timer,Is.LessThanOrEqualTo(previousTimer+.001f),"Failed admission must retain its due arrival: "+NativeState(flow,world));
                    if(count>previousCount && count>=7)
                    {
                        var queue=(CounterQueueService)typeof(CustomerFlowController).GetField("queue",flags).GetValue(flow);
                        var predecessor=flow.Snapshot[count-2];
                        Assert.That(predecessor.State,Is.EqualTo(CustomerVisitState.Entering),"The next arrival must not depend on predecessor Arrived");
                        Assert.That(Vector3.Distance(predecessor.Position,queue.Snapshot[count-2].Position),Is.GreaterThan(2),
                            "Safe following must admit before the predecessor's final approach: "+NativeState(flow,world));
                        earlyFollowers++;
                    }
                    previousCount=count; previousTimer=timer;
                    return count==8 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued);
                },8);
                Assert.That(earlyFollowers,Is.EqualTo(2));
                Assert.That(concurrentEntryFrames,Is.GreaterThanOrEqualTo(12));
                var ids=flow.Snapshot.Select(v=>v.VisitId).ToArray();
                for(var count=8;count>0;count--)
                {
                    Assert.That(flow.Snapshot[0].VisitId,Is.EqualTo(ids[8-count]));
                    Assert.That(flow.TryLetFrontLeave(),Is.True);
                    yield return WaitNative(flow,world,()=>flow.Snapshot.Count==count-1 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                }
                foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
            }
            finally { if(scene.IsValid() && scene.isLoaded) pendingUnload=SceneManager.UnloadSceneAsync(scene); }
        }
        [UnityTest] public IEnumerator NativeDefaultEighthArrivesWhileSeventhTemporarilyPassesTail()
        {
            nativeCaptureToRestore=Time.captureDeltaTime; Time.captureDeltaTime=1f/60;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            try
            {
                var roots=scene.GetRootGameObjects();
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CustomerFlowController>(true)).Single();
                var world=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationWorld>(true)).Single();
                flow.AutoSpawn=false;
                yield return WaitNative(flow,world,()=>!string.IsNullOrEmpty(flow.SelectedRegisterId));
                for(var count=0;count<6;count++)
                { Assert.That(flow.TrySpawn(),Is.True); yield return WaitNative(flow,world,()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued)); }
                Assert.That(flow.TrySpawn(),Is.True);
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var queue=(CounterQueueService)typeof(CustomerFlowController).GetField("queue",flags).GetValue(flow);
                var tail=queue.NextAdmissionPosition.Value; var previousDistance=float.PositiveInfinity;
                yield return WaitNative(flow,world,()=>
                {
                    var distance=Vector3.Distance(flow.Snapshot[6].Position,tail);
                    var passing=flow.Snapshot[6].State==CustomerVisitState.Entering && distance<.98f && distance>previousDistance+.0001f;
                    previousDistance=distance; return passing;
                });
                typeof(CustomerFlowController).GetField("clock",flags).SetValue(flow,new CustomerSpawnClock(()=>0));
                flow.AutoSpawn=true; flow.Step(1.01f); flow.AutoSpawn=false;
                Assert.That(flow.Snapshot.Count,Is.EqualTo(8),"A moving predecessor passing the future tail must not pause safe admission: "+NativeState(flow,world));
                concurrentEntryFrames=0;
                yield return WaitNative(flow,world,()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                Assert.That(concurrentEntryFrames,Is.GreaterThanOrEqualTo(12));
                var ids=flow.Snapshot.Select(v=>v.VisitId).ToArray();
                for(var count=8;count>0;count--)
                {
                    Assert.That(flow.Snapshot[0].VisitId,Is.EqualTo(ids[8-count]));
                    Assert.That(flow.TryLetFrontLeave(),Is.True);
                    yield return WaitNative(flow,world,()=>flow.Snapshot.Count==count-1 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                }
                foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
            }
            finally { if(scene.IsValid() && scene.isLoaded) pendingUnload=SceneManager.UnloadSceneAsync(scene); }
        }
        [UnityTest] public IEnumerator NativeDefaultFifthArrivalFollowsFourthAroundCounterBeforeItQueues()
        {
            nativeCaptureToRestore=Time.captureDeltaTime; Time.captureDeltaTime=1f/60;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            try
            {
                var roots=scene.GetRootGameObjects();
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CustomerFlowController>(true)).Single();
                var world=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationWorld>(true)).Single();
                flow.AutoSpawn=false;
                yield return WaitNative(flow,world,()=>!string.IsNullOrEmpty(flow.SelectedRegisterId));
                for(var count=0;count<3;count++)
                { Assert.That(flow.TrySpawn(),Is.True); yield return WaitNative(flow,world,()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued)); }
                Assert.That(flow.TrySpawn(),Is.True);
                yield return WaitNative(flow,world,()=>flow.Snapshot[3].Position.x<-1.8f && flow.Snapshot[3].Position.z>.35f);
                Assert.That(flow.Snapshot[3].State,Is.EqualTo(CustomerVisitState.Entering));
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                typeof(CustomerFlowController).GetField("clock",flags).SetValue(flow,new CustomerSpawnClock(()=>0));
                flow.AutoSpawn=true; flow.Step(1.01f); flow.AutoSpawn=false;
                Assert.That(flow.Snapshot.Count,Is.EqualTo(5),"Expired arrival must admit a safe follower before the fourth queues: "+NativeState(flow,world));
                concurrentEntryFrames=0;
                yield return WaitNative(flow,world,()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                Assert.That(concurrentEntryFrames,Is.GreaterThanOrEqualTo(12),"Both entering bodies must actually move together");
                var ids=flow.Snapshot.Select(v=>v.VisitId).ToArray();
                for(var count=5;count>0;count--)
                {
                    Assert.That(flow.Snapshot[0].VisitId,Is.EqualTo(ids[5-count]));
                    Assert.That(flow.TryLetFrontLeave(),Is.True);
                    yield return WaitNative(flow,world,()=>flow.Snapshot.Count==count-1 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                }
                foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
            }
            finally { if(scene.IsValid() && scene.isLoaded) pendingUnload=SceneManager.UnloadSceneAsync(scene); }
        }
        [UnityTest] public IEnumerator NativeDefaultCornerArrivalsContinueAtFifteenFps()
        { yield return NativeDefaultCornerArrivals(1f/15); }
        [UnityTest] public IEnumerator NativeDefaultCornerArrivalsContinueAtSixtyFps()
        { yield return NativeDefaultCornerArrivals(1f/60); }
        [UnityTest] public IEnumerator NativeDefaultCornerArrivalsContinueAtTwoFortyFps()
        { yield return NativeDefaultCornerArrivals(1f/240); }
        [UnityTest] public IEnumerator NativeDefaultCornerArrivalsContinueAtThousandFps()
        { yield return NativeDefaultCornerArrivals(1f/1000); }
        [UnityTest] public IEnumerator NativeDefaultCornerArrivalsContinueWithRealEditorTiming()
        { yield return NativeDefaultCornerArrivals(0); }
        private IEnumerator NativeDefaultCornerArrivals(float frameTime)
        {
            nativeCaptureToRestore=Time.captureDeltaTime; Time.captureDeltaTime=frameTime;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            try
            {
                var roots=scene.GetRootGameObjects();
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CustomerFlowController>(true)).Single();
                var world=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationWorld>(true)).Single();
                flow.AutoSpawn=false;
                yield return WaitNative(flow,world,()=>!string.IsNullOrEmpty(flow.SelectedRegisterId));
                typeof(CustomerFlowController).GetField("clock",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(flow,new CustomerSpawnClock(()=>0));
                requireCornerContinuity=true; flow.AutoSpawn=true;
                yield return WaitNative(flow,world,()=>flow.Snapshot.Count==5 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued),5);
                var ids=flow.Snapshot.Select(v=>v.VisitId).ToArray();
                for(var count=5;count>0;count--)
                {
                    Assert.That(flow.Snapshot[0].VisitId,Is.EqualTo(ids[5-count]));
                    Assert.That(flow.TryLetFrontLeave(),Is.True,NativeState(flow,world));
                    yield return WaitNative(flow,world,()=>flow.Snapshot.Count==count-1 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                }
                foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
            }
            finally { requireCornerContinuity=false; if(scene.IsValid() && scene.isLoaded) pendingUnload=SceneManager.UnloadSceneAsync(scene); }
        }
        [UnityTearDown] public IEnumerator WaitForSceneInputOwnersToUnload()
        {
            // Shared UI Actions必须在InputTestFixture恢复前退出自己的scene。
            var scene=SceneManager.GetSceneByPath("Assets/Scenes/Validation/Phase12CustomerQueue.unity");
            if(pendingUnload==null && scene.IsValid() && scene.isLoaded) pendingUnload=SceneManager.UnloadSceneAsync(scene);
            while(pendingUnload!=null && !pendingUnload.isDone) yield return null;
            pendingUnload=null;
            if(nativeCaptureToRestore.HasValue) { Time.captureDeltaTime=nativeCaptureToRestore.Value; nativeCaptureToRestore=null; }
            Assert.That(SceneManager.GetSceneByPath("Assets/Scenes/Validation/Phase12CustomerQueue.unity").isLoaded,Is.False);
        }
        [Test] public void SceneBuilderExistsAndSavedSceneHasSingleNavigationOwner()
        {
            var editorAssembly=System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a=>a.GetType("AnimalCafe.EditorTools.Phase12.Phase12CustomerQueueSceneSetup")!=null);
            Assert.That(editorAssembly,Is.Not.Null,"P12 needs its own idempotent builder");
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Validation/Phase12CustomerQueue.unity"),Is.Not.Null);
        }
        [UnityTest] public IEnumerator SavedValidationSceneSeedsRealBusinessAndSpawnsWithoutDuplicateOwners()
        {
            var savedCapture=Time.captureDeltaTime; Time.captureDeltaTime=1f/60;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null; // LoadSceneInPlayMode publishes roots on the next frame.
            try
            {
                var roots=scene.GetRootGameObjects();
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CustomerFlowController>(true)).Single();
                Assert.That(roots.SelectMany(r=>r.GetComponentsInChildren<NavigationWorld>(true)).Count(),Is.EqualTo(1));
                Assert.That(roots.SelectMany(r=>r.GetComponentsInChildren<NavigationLayoutAdapter>(true)).Count(),Is.EqualTo(1));
                Assert.That(roots.SelectMany(r=>r.GetComponentsInChildren<NavigationDecorationBridge>(true)).Count(),Is.EqualTo(1));
                for(var i=0;i<900 && flow.Snapshot.Count==0;i++) yield return null;
                Assert.That(flow.Snapshot.Count,Is.GreaterThan(0),flow.Status);
                Assert.That(flow.SelectedRegisterId,Is.EqualTo("12000000000000000000000000000004"));
                for(var i=0;i<900 && !flow.Snapshot.Any(v=>v.State==CustomerVisitState.Queued);i++) yield return null;
                Assert.That(flow.Snapshot.Any(v=>v.State==CustomerVisitState.Queued),Is.True,flow.Status);
                flow.AutoSpawn=false;
            }
            finally { Time.captureDeltaTime=savedCapture; if(scene.IsValid() && scene.isLoaded) pendingUnload=SceneManager.UnloadSceneAsync(scene); }
            yield return null;
        }
        [UnityTest] public IEnumerator NativeDefaultThirdQueuesBehindWestieThenEightCustomersDrain()
        {
            var capture=Time.captureDeltaTime; Time.captureDeltaTime=1f/60;
            nativeCaptureToRestore=capture;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            try
            {
                var roots=scene.GetRootGameObjects();
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CustomerFlowController>(true)).Single();
                var world=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationWorld>(true)).Single();
                flow.AutoSpawn=false;
                yield return WaitNative(flow,world,()=>!string.IsNullOrEmpty(flow.SelectedRegisterId));
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var grid=(Transform)typeof(CustomerFlowController).GetField("gridRoot",flags).GetValue(flow);
                for(var count=1;count<=8;count++)
                {
                    Assert.That(flow.TrySpawn(),Is.True,NativeState(flow,world));
                    yield return WaitNative(flow,world,()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                    if(count==3)
                    {
                        Assert.That(Vector3.Distance(grid.InverseTransformPoint(flow.Snapshot[1].Position),new Vector3(2.5f,0,1.5f)),Is.LessThanOrEqualTo(.081f));
                        Assert.That(Vector3.Distance(grid.InverseTransformPoint(flow.Snapshot[2].Position),new Vector3(2.5f,0,.5f)),Is.LessThanOrEqualTo(.081f),"Third customer must actually stand behind Westie, before the queue turns");
                    }
                }
                var ids=flow.Snapshot.Select(v=>v.VisitId).ToArray();
                for(var count=8;count>0;count--)
                {
                    Assert.That(flow.Snapshot[0].VisitId,Is.EqualTo(ids[8-count]));
                    Assert.That(flow.TryLetFrontLeave(),Is.True,NativeState(flow,world));
                    yield return WaitNative(flow,world,()=>flow.Snapshot.Count==count-1 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                }
                foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
            }
            finally { Time.captureDeltaTime=capture; if(scene.IsValid() && scene.isLoaded) pendingUnload=SceneManager.UnloadSceneAsync(scene); }
            yield return null;
        }
        [UnityTest] public IEnumerator NativeDecorResetCustomersOnlyAfterConfirmedDoneAndRestartsClock()
        {
            var capture=Time.captureDeltaTime; Time.captureDeltaTime=1f/60; nativeCaptureToRestore=capture;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            try
            {
                var roots=scene.GetRootGameObjects();
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CustomerFlowController>(true)).Single();
                var world=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationWorld>(true)).Single();
                var decor=roots.SelectMany(r=>r.GetComponentsInChildren<AnimalCafe.Decoration.DecorationModeController>(true)).Single();
                var runtime=roots.SelectMany(r=>r.GetComponentsInChildren<AnimalCafe.Decoration.CafeLayoutRuntime>(true)).Single();
                flow.AutoSpawn=false;
                yield return WaitNative(flow,world,()=>!string.IsNullOrEmpty(flow.SelectedRegisterId));
                for(var i=0;i<3;i++)
                { Assert.That(flow.TrySpawn(),Is.True,NativeState(flow,world)); yield return WaitNative(flow,world,()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued)); }
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var grid=(Transform)typeof(CustomerFlowController).GetField("gridRoot",flags).GetValue(flow);
                // 非顾客真实actor，即使挂在flow下面也不属于顾客visit / employee lifecycle stays separate.
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase11/Characters/Shiba/PF_Shiba_Navigation.prefab");
                var worker=Object.Instantiate(prefab,grid.TransformPoint(new Vector3(6.5f,0,6.5f)),Quaternion.identity,flow.transform).GetComponent<NavigationActor>();
                Assert.That(worker.TryInitializeRuntimeId("preserved-worker"),Is.True); Assert.That(world.Register(worker),Is.True);
                var workerPosition=worker.transform.position;
                var ids=flow.Snapshot.Select(v=>v.VisitId).ToArray();
                decor.EnterDecorationMode(); yield return null;
                Assert.That(decor.TryRequestExit(),Is.True); flow.Step(0);
                yield return WaitNative(flow,world,()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                CollectionAssert.AreEqual(ids,flow.Snapshot.Select(v=>v.VisitId).ToArray(),"Open/Done without a commit must preserve customers");
                var cash=runtime.Layout.FurnitureInstances.Single(f=>!f.InstanceId.StartsWith("1200000000000000000000000000000"));
                decor.EnterDecorationMode(); yield return null;
                var edit=(AnimalCafe.Decoration.DecorationSession)typeof(AnimalCafe.Decoration.DecorationModeController).GetField("session",flags).GetValue(decor);
                Assert.That(edit.BeginExisting(cash.InstanceId).Succeeded,Is.True);
                Assert.That(edit.MovePreview(new AnimalCafe.Layout.GridPosition(1,5)).Succeeded,Is.True);
                Assert.That(decor.TryRequestExit(),Is.False,"An unfinished preview is not completed decoration");
                flow.Step(0); CollectionAssert.AreEqual(ids,flow.Snapshot.Select(v=>v.VisitId).ToArray());
                decor.CancelActivePreview(); Assert.That(decor.TryRequestExit(),Is.True); flow.Step(0);
                yield return WaitNative(flow,world,()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                CollectionAssert.AreEqual(ids,flow.Snapshot.Select(v=>v.VisitId).ToArray(),"Cancelled preview must not reset customers");
                var capacity=flow.Capacity;
                typeof(CustomerFlowController).GetField("clock",flags).SetValue(flow,new CustomerSpawnClock(()=>.25f));
                for(var cycle=0;cycle<2;cycle++)
                {
                    ids=flow.Snapshot.Select(v=>v.VisitId).ToArray();
                    decor.EnterDecorationMode(); yield return null;
                    ConfirmCashPreview(decor,cash.InstanceId,cycle==0?new AnimalCafe.Layout.GridPosition(1,5):new AnimalCafe.Layout.GridPosition(2,3));
                    flow.Step(0); CollectionAssert.AreEqual(ids,flow.Snapshot.Select(v=>v.VisitId).ToArray(),"Confirm while Decor is open must retain paused customers");
                    Assert.That(decor.TryRequestExit(),Is.True); flow.Step(0);
                    Assert.That(flow.Snapshot.Count,Is.Zero,"Done must retire the old customer session, rather than reflow its bodies");
                    Assert.That(flow.Capacity,Is.SameAs(capacity));
                    foreach(var ledger in capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
                    yield return null;
                    Assert.That(world.RegisteredActors.Count,Is.EqualTo(1)); Assert.That(world.RegisteredActors.Single(),Is.SameAs(worker));
                    Assert.That(worker.transform.position,Is.EqualTo(workerPosition));
                    flow.AutoSpawn=true; flow.Step(.5f); Assert.That(flow.Snapshot.Count,Is.Zero);
                    Assert.That(flow.RemainingSeconds,Is.EqualTo(1.5f).Within(.001f),"Fresh two-second injected interval, no immediate refill");
                    flow.Step(1.4f); Assert.That(flow.Snapshot.Count,Is.Zero); flow.Step(.11f);
                    Assert.That(flow.Snapshot.Count,Is.EqualTo(1)); Assert.That(ids.Contains(flow.Snapshot.Single().VisitId),Is.False);
                    flow.AutoSpawn=false; yield return WaitNative(flow,world,()=>flow.Snapshot.Single().State==CustomerVisitState.Queued);
                    var newId=flow.Snapshot.Single().VisitId;
                    decor.ExitDecorationMode(); flow.Step(0); flow.Step(0);
                    Assert.That(flow.Snapshot.Single().VisitId,Is.EqualTo(newId),"Duplicate Done and Step cannot reset the new visit again");
                }
                Assert.That(flow.TryLetFrontLeave(),Is.True); yield return WaitNative(flow,world,()=>flow.Snapshot.Count==0);
                foreach(var ledger in capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
                Assert.That(world.RegisteredActors.Single(),Is.SameAs(worker));
            }
            finally { Time.captureDeltaTime=capture; if(scene.IsValid() && scene.isLoaded) pendingUnload=SceneManager.UnloadSceneAsync(scene); }
            yield return null;
        }
        private static void ConfirmCashPreview(AnimalCafe.Decoration.DecorationModeController decor,string id,AnimalCafe.Layout.GridPosition position)
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var edit=(AnimalCafe.Decoration.DecorationSession)typeof(AnimalCafe.Decoration.DecorationModeController).GetField("session",flags).GetValue(decor);
            Assert.That(edit.BeginExisting(id).Succeeded,Is.True); Assert.That(edit.MovePreview(position).Succeeded,Is.True);
            typeof(AnimalCafe.Decoration.DecorationModeController).GetMethod("SyncActivePreviewPresentation",flags).Invoke(decor,null);
            typeof(AnimalCafe.Decoration.DecorationModeController).GetMethod("ShowActionForActivePreview",flags).Invoke(decor,new object[]{false});
            typeof(AnimalCafe.Decoration.DecorationModeController).GetMethod("HandleConfirmRequested",flags).Invoke(decor,null);
            Assert.That(edit.ActivePreview,Is.Null,"Real Confirm must publish the placement");
        }
        [UnityTest] public IEnumerator NativeDecorResetWithInvalidBusinessKeepsAdmissionStopped()
        {
            var capture=Time.captureDeltaTime; Time.captureDeltaTime=1f/60; nativeCaptureToRestore=capture;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            try
            {
                var roots=scene.GetRootGameObjects();
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CustomerFlowController>(true)).Single();
                var world=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationWorld>(true)).Single();
                var decor=roots.SelectMany(r=>r.GetComponentsInChildren<AnimalCafe.Decoration.DecorationModeController>(true)).Single();
                var runtime=roots.SelectMany(r=>r.GetComponentsInChildren<AnimalCafe.Decoration.CafeLayoutRuntime>(true)).Single();
                flow.AutoSpawn=false; yield return WaitNative(flow,world,()=>!string.IsNullOrEmpty(flow.SelectedRegisterId));
                Assert.That(flow.TrySpawn(),Is.True); yield return WaitNative(flow,world,()=>flow.Snapshot.Single().State==CustomerVisitState.Queued);
                decor.EnterDecorationMode(); yield return null;
                Assert.That(runtime.FunctionalSurfaceLayout.RemoveMounted("12000000000000000000000000000005").Succeeded,Is.True);
                decor.RefreshConfirmedNavigationPresentation(); Assert.That(decor.TryRequestExit(),Is.True); flow.Step(0);
                Assert.That(flow.Snapshot.Count,Is.Zero);
                foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
                flow.AutoSpawn=true; flow.Step(5); Assert.That(flow.TrySpawn(),Is.False);
                Assert.That(flow.Snapshot.Count,Is.Zero); Assert.That(flow.RemainingSeconds,Is.Zero); Assert.That(world.LayoutAvailable,Is.False);
            }
            finally { Time.captureDeltaTime=capture; if(scene.IsValid() && scene.isLoaded) pendingUnload=SceneManager.UnloadSceneAsync(scene); }
            yield return null;
        }
        [UnityTest] public IEnumerator NativeDenseQueueFrontExitAdvancesEveryRemainingCustomer()
        { yield return NativeQueueExitAndAdvance(false,4); }

        [UnityTest] public IEnumerator NativeSpreadQueueFrontExitAdvancesEveryRemainingCustomer()
        { yield return NativeQueueExitAndAdvance(true,4); }

        [UnityTest] public IEnumerator NativeSpreadFiveCustomersTurnAndDrainEveryRemainingCustomer()
        { yield return NativeQueueExitAndAdvance(true,5); }

        [UnityTest] public IEnumerator NativeDenseFiveCustomersTurnAndDrainEveryRemainingCustomer()
        { yield return NativeQueueExitAndAdvance(false,5); }

        [UnityTest] public IEnumerator NativeDenseFiveFollowersMoveTogetherThroughCornersAndDrain()
        {
            concurrentAdvanceFrames=0;
            yield return NativeQueueExitAndAdvance(false,5);
            Assert.That(concurrentAdvanceFrames,Is.GreaterThanOrEqualTo(12),"Native curved queue must have overlapping actual advances");
        }

        [UnityTest] public IEnumerator NativeCashMovedOnlyFiveCustomersDrainAtThirtyFps()
        { yield return NativeQueueExitAndAdvance(true,5,false,false,30,true,true); }

        [UnityTest] public IEnumerator NativeCashMovedOnlyFiveCustomersDrainAtFifteenFps()
        { yield return NativeQueueExitAndAdvance(true,5,false,false,15,true,true); }

        [UnityTest] public IEnumerator NativeCashMovedOnlyFiveCustomersDrainAtTwentyFps()
        { yield return NativeQueueExitAndAdvance(true,5,false,false,20,true,true); }

        [UnityTest] public IEnumerator NativeCashMovedOnlyFiveCustomersDrainAtOneTwentyFps()
        { yield return NativeQueueExitAndAdvance(true,5,false,false,120,true,true); }

        [UnityTest] public IEnumerator NativeAutoSpawnCashOnlyFiveCustomersDrainWithRealEditorTiming()
        { yield return NativeAutoSpawnAndBusinessMove(false); }

        [UnityTest] public IEnumerator NativeAutoSpawnCashMovedDuringBusinessResetsCustomersAndNewQueueDrains()
        { yield return NativeAutoSpawnAndBusinessMove(true); }

        [UnityTest] public IEnumerator NativeFullQueueCashMovedDuringBusinessResetsAndRefillsNewQueue()
        { yield return NativeAutoSpawnAndBusinessMove(true,8); }

        [UnityTest] public IEnumerator NativeBoundaryDecorResetFindsEightAccessibleSlotsAndDrains()
        { yield return NativeAutoSpawnAndBusinessMove(true,8,null,false,true); }

        [UnityTest] public IEnumerator NativeEdgeAlignedRowReachesCornerCellAndDrains()
        { yield return NativeAutoSpawnAndBusinessMove(false,5,new AnimalCafe.Layout.GridPosition(0,5),false,false,true); }

        [UnityTest] public IEnumerator NativeEdgeAlignedThreeCustomerDecorResetThenFiveArrivalsDrain()
        { yield return NativeAutoSpawnAndBusinessMove(true,5,new AnimalCafe.Layout.GridPosition(0,5),false,false,true,3); }

        [UnityTest] public IEnumerator NativeFiveCustomerWallBarrierDecorResetReopensAndDrains()
        { yield return NativeAutoSpawnAndBusinessMove(true,5,new AnimalCafe.Layout.GridPosition(0,5),false,false,true); }

        [UnityTest] public IEnumerator NativeFiveCashMovedBehindCoffeeFindsDetourAndDrains()
        { yield return NativeAutoSpawnAndBusinessMove(true,5,new AnimalCafe.Layout.GridPosition(4,5)); }

        [UnityTest] public IEnumerator NativeFiveCashMovedBehindPickupFindsDetourAndDrains()
        { yield return NativeAutoSpawnAndBusinessMove(true,5,new AnimalCafe.Layout.GridPosition(6,5)); }

        [UnityTest] public IEnumerator NativeFiveCashMovedBesideFarWallFindsDetourAndDrains()
        { yield return NativeAutoSpawnAndBusinessMove(true,5,new AnimalCafe.Layout.GridPosition(7,6)); }

        [UnityTest] public IEnumerator NativeCashBehindCoffeeMovedDuringEntryFindsDetourAndDrains()
        { yield return NativeAutoSpawnAndBusinessMove(true,5,new AnimalCafe.Layout.GridPosition(4,5),true); }

        [UnityTest] public IEnumerator NativeCashBehindPickupMovedDuringEntryFindsDetourAndDrains()
        { yield return NativeAutoSpawnAndBusinessMove(true,5,new AnimalCafe.Layout.GridPosition(6,5),true); }

        [UnityTest] public IEnumerator NativeCashBesideFarWallMovedDuringEntryFindsDetourAndDrains()
        { yield return NativeAutoSpawnAndBusinessMove(true,5,new AnimalCafe.Layout.GridPosition(7,6),true); }

        [UnityTest] public IEnumerator NativeFurnitureEdgeWaypointContinuesFromActualPoseToQueueSlot()
        {
            var capture=Time.captureDeltaTime; Time.captureDeltaTime=1f/60; nativeCaptureToRestore=capture;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            try
            {
                var roots=scene.GetRootGameObjects();
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CustomerFlowController>(true)).Single(); flow.AutoSpawn=false;
                var world=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationWorld>(true)).Single();
                var adapter=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationLayoutAdapter>(true)).Single();
                yield return WaitNative(flow,world,()=>!string.IsNullOrEmpty(flow.SelectedRegisterId));
                var grid=(Transform)new SerializedObject(flow).FindProperty("gridRoot").objectReferenceValue;
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var solids=(System.Collections.Generic.IReadOnlyList<Collider>)typeof(NavigationWorld).GetField("solids",flags).GetValue(world);
                Assert.That(flow.TrySpawn(),Is.True);
                var actor=world.RegisteredActors.Single();
                Vector3? edge=null;
                var corners=new System.Collections.Generic.List<string>();
                foreach(var local in new[]{new Vector3(1.5f,0,4.5f),new Vector3(1.5f,0,5.5f),new Vector3(3.5f,0,4.5f),new Vector3(4.5f,0,5.5f),new Vector3(5.5f,0,4.5f),new Vector3(6.5f,0,6.5f)})
                {
                    var path=adapter.GetCompletePath(actor.transform.position,grid.TransformPoint(local));
                    if(path==null) continue;
                    foreach(var point in path)
                    {
                        var body=point+Vector3.up*.65f;
                        var clearance=solids.Min(s=>Vector3.Distance(body,s.ClosestPoint(body)));
                        corners.Add(point.ToString("F3")+" clearance="+clearance.ToString("F5"));
                        if(clearance>=actor.Settings.AgentRadius && clearance<actor.Settings.AgentRadius+actor.Settings.CollisionSkin)
                        { edge=point; break; }
                    }
                    if(edge.HasValue) break;
                }
                Assert.That(edge.HasValue,Is.True,"Fixture needs a real accepted NavMesh corner inside full collision skin: "+string.Join(" | ",corners));
                // 使用真实driver/Service到达，不模拟Arrived，也不移动Transform。
                // 只指定两段route，独立验证Controller对临时waypoint的处理。
                flow.enabled=false;
                var visits=(System.Collections.IDictionary)typeof(CustomerFlowController).GetField("visits",flags).GetValue(flow);
                var visit=visits.Values.Cast<object>().Single(); var type=visit.GetType();
                var final=(NavigationTarget)type.GetField("FinalTarget").GetValue(visit);
                type.GetField("Route").SetValue(visit,new System.Collections.Generic.Queue<Vector3>(new[]{edge.Value,final.Position}));
                typeof(CustomerFlowController).GetMethod("SendRouteSegment",flags).Invoke(flow,new object[]{visit,CustomerVisitState.Entering});
                yield return WaitNative(flow,world,()=>flow.Snapshot.Single().State==CustomerVisitState.Queued);
                Assert.That(Vector3.Distance(actor.transform.position,final.Position),Is.LessThanOrEqualTo(.081f));
                Assert.That(flow.Capacity.GetCapacities()[0].Occupied,Is.EqualTo(1),"Intermediate arrival must retain Total");
            }
            finally { Time.captureDeltaTime=capture; if(scene.IsValid() && scene.isLoaded) pendingUnload=SceneManager.UnloadSceneAsync(scene); }
            yield return null;
        }

        [UnityTest] public IEnumerator NativeScreenshotInspiredFiveCustomersRepeatedlyAdvanceAtSixtyFps()
        { yield return NativeQueueExitAndAdvance(true,5,false,false,60,false,true,true); }

        [UnityTest] public IEnumerator NativeClearQueueRouteDoesNotCrawlBeforeCornerOrDestination()
        {
            requireSteadyMotion=true;
            try { yield return NativeQueueExitAndAdvance(true,5,false,false,60,false,false,true); }
            finally { requireSteadyMotion=false; }
        }

        private IEnumerator NativeAutoSpawnAndBusinessMove(bool moveDuringBusiness,int customers=5,AnimalCafe.Layout.GridPosition? destination=null,bool interruptLast=false,bool requireFullSlots=false,bool requireCorner=false,int? initialCustomers=null)
        {
            var capture=Time.captureDeltaTime; Time.captureDeltaTime=moveDuringBusiness?1f/60:0;
            nativeCaptureToRestore=capture;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            try
            {
                var roots=scene.GetRootGameObjects();
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CustomerFlowController>(true)).Single(); flow.AutoSpawn=false;
                var world=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationWorld>(true)).Single();
                var adapter=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationLayoutAdapter>(true)).Single();
                var decor=roots.SelectMany(r=>r.GetComponentsInChildren<AnimalCafe.Decoration.DecorationModeController>(true)).Single();
                var runtime=roots.SelectMany(r=>r.GetComponentsInChildren<AnimalCafe.Decoration.CafeLayoutRuntime>(true)).Single();
                yield return WaitNative(flow,world,()=>!string.IsNullOrEmpty(flow.SelectedRegisterId));
                var cash=runtime.Layout.FurnitureInstances.Single(f=>!f.InstanceId.StartsWith("1200000000000000000000000000000"));
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                if(customers==8)
                {
                    // 满队先朝后墙排队，再移动至店内；使用真实Confirm/Done提交。
                    decor.EnterDecorationMode(); yield return null;
                    var initialEdit=(AnimalCafe.Decoration.DecorationSession)typeof(AnimalCafe.Decoration.DecorationModeController).GetField("session",flags).GetValue(decor);
                    Assert.That(initialEdit.BeginExisting(cash.InstanceId).Succeeded,Is.True);
                    Assert.That(initialEdit.RotatePreview().Succeeded,Is.True);
                    Assert.That(initialEdit.RotatePreview().Succeeded,Is.True);
                    typeof(AnimalCafe.Decoration.DecorationModeController).GetMethod("SyncActivePreviewPresentation",flags).Invoke(decor,null);
                    typeof(AnimalCafe.Decoration.DecorationModeController).GetMethod("ShowActionForActivePreview",flags).Invoke(decor,new object[]{false});
                    typeof(AnimalCafe.Decoration.DecorationModeController).GetMethod("HandleConfirmRequested",flags).Invoke(decor,null);
                    decor.ExitDecorationMode();
                    yield return WaitNative(flow,world,()=>world.LayoutAvailable);
                }
                if(moveDuringBusiness)
                {
                    for(var i=0;i<(initialCustomers??customers);i++)
                    {
                        Assert.That(flow.TrySpawn(),Is.True,NativeState(flow,world));
                        if(interruptLast && i==customers-1)
                        {
                            var last=world.RegisteredActors.Single(a=>a.ActorId==flow.Snapshot.Last().VisitId); var initial=last.transform.position;
                            yield return WaitNative(flow,world,()=>Vector3.Distance(last.transform.position,initial)>1.5f);
                            Assert.That(flow.Snapshot.Last().State,Is.EqualTo(CustomerVisitState.Entering),"Decor must interrupt a real entry");
                        }
                        else yield return WaitNative(flow,world,()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                    }
                }
                var oldIds=flow.Snapshot.Select(v=>v.VisitId).ToArray();
                // 实际Decoration/Preview/Confirm/Done，避免只测直接修改domain再手动rebuild。
                decor.EnterDecorationMode(); yield return null;
                var edit=(AnimalCafe.Decoration.DecorationSession)typeof(AnimalCafe.Decoration.DecorationModeController).GetField("session",flags).GetValue(decor);
                Assert.That(edit.BeginExisting(cash.InstanceId).Succeeded,Is.True);
                var movedPosition=destination??(customers==8?new AnimalCafe.Layout.GridPosition(4,6):moveDuringBusiness?new AnimalCafe.Layout.GridPosition(2,6):new AnimalCafe.Layout.GridPosition(1,5));
                Assert.That(edit.MovePreview(movedPosition).Succeeded,Is.True);
                if(customers==8)
                {
                    Assert.That(edit.RotatePreview().Succeeded,Is.True);
                    Assert.That(edit.RotatePreview().Succeeded,Is.True);
                }
                typeof(AnimalCafe.Decoration.DecorationModeController).GetMethod("SyncActivePreviewPresentation",flags).Invoke(decor,null);
                typeof(AnimalCafe.Decoration.DecorationModeController).GetMethod("ShowActionForActivePreview",flags).Invoke(decor,new object[]{false});
                typeof(AnimalCafe.Decoration.DecorationModeController).GetMethod("HandleConfirmRequested",flags).Invoke(decor,null);
                Assert.That(edit.ActivePreview,Is.Null,"Real Confirm must commit the move: "+
                    typeof(AnimalCafe.Decoration.DecorationModeController).GetField("currentEditingMessage",flags).GetValue(decor)+" / "+NativeState(flow,world));
                Assert.That(runtime.Layout.FurnitureInstances.Single(f=>f.InstanceId==cash.InstanceId).Position,Is.EqualTo(movedPosition));
                var rebuildTime=System.Diagnostics.Stopwatch.StartNew();
                decor.ExitDecorationMode();
                flow.Step(0); // 消费已确认Done，结束旧顾客批次 / retire the old visits.
                rebuildTime.Stop();
                if(requireFullSlots) Debug.Log("P12_BOUNDARY Done and queue rebuild seconds="+rebuildTime.Elapsed.TotalSeconds.ToString("F3"));
                var confirmedQueue=(CounterQueueService)typeof(CustomerFlowController).GetField("queue",flags).GetValue(flow);
                var newSlots=(Vector3[])typeof(CounterQueueService).GetField("slots",flags).GetValue(confirmedQueue);
                if(requireFullSlots) Assert.That(newSlots.Length,Is.EqualTo(8),"Boundary reflow should retry alternate turns instead of stopping at the first enclosed tail");
                if(requireCorner)
                {
                    var grid=(Transform)typeof(CustomerFlowController).GetField("gridRoot",flags).GetValue(flow);
                    var corner=grid.TransformPoint(new Vector3(.5f,0,.5f));
                    Assert.That(newSlots.Any(p=>Vector3.Distance(p,corner)<.04f),Is.True,
                        "Safe corner cell should participate in the row: "+string.Join(" | ",newSlots.Select(p=>grid.InverseTransformPoint(p).ToString("F3"))));
                    for(var i=1;i<newSlots.Length;i++) for(var j=0;j<i;j++)
                        Assert.That(Vector3.Distance(newSlots[i],newSlots[j]),Is.GreaterThanOrEqualTo(.999f));
                }
                Assert.That(newSlots.Length,Is.GreaterThanOrEqualTo(customers),"New opening needs enough accessible waiting slots");
                Assert.That(flow.Snapshot.Count,Is.Zero,"Confirmed Done must clear all old customer visits");
                foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
                Assert.That(world.RegisteredActors.Any(a=>oldIds.Contains(a.ActorId)),Is.False,"Old movement owners must be unregistered");
                yield return null; // Destroy retires old proxy objects at frame end before any new admission.
                typeof(CustomerFlowController).GetField("clock",flags).SetValue(flow,new CustomerSpawnClock(()=>.25f));
                var replenish=customers<8 && (!destination.HasValue || requireCorner);
                // 新批次包含逐次倒计时与入店；每位分别保持原60秒移动上限。
                // Keep the same arrival deadline per customer instead of timing the whole reopening.
                for(var arrived=1;arrived<=customers;arrived++)
                {
                    var expected=arrived;
                    flow.AutoSpawn=true;
                    yield return WaitNative(flow,world,()=>flow.Snapshot.Count==expected && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued),expected);
                }
                Assert.That(flow.Snapshot.Any(v=>oldIds.Contains(v.VisitId)),Is.False,"Reopening must publish new unique visits");
                flow.AutoSpawn=replenish;
                if(requireCorner)
                {
                    foreach(var assignment in confirmedQueue.Snapshot)
                        Assert.That(Vector3.Distance(flow.Snapshot.Single(v=>v.VisitId==assignment.VisitId).Position,newSlots[assignment.SlotIndex]),Is.LessThanOrEqualTo(.081f),"Corner customers must actually reach the new slots");
                    var grid=(Transform)typeof(CustomerFlowController).GetField("gridRoot",flags).GetValue(flow);
                    Assert.That(flow.Snapshot.Any(v=>Vector3.Distance(v.Position,grid.TransformPoint(new Vector3(.5f,0,.5f)))<.121f),Is.True,"A real customer must occupy the safe corner cell");
                }
                var hud=roots.SelectMany(r=>r.GetComponentsInChildren<CustomerQueueValidationController>(true)).Single();
                var leave=(UnityEngine.UI.Button)new SerializedObject(hud).FindProperty("leaveButton").objectReferenceValue;
                for(var cycle=0;cycle<(replenish && !requireCorner?5:0);cycle++)
                {
                    flow.AutoSpawn=true;
                    var departing=flow.Snapshot[0].VisitId; leave.onClick.Invoke();
                    yield return WaitNative(flow,world,()=>flow.Snapshot.Count==customers && flow.Snapshot[0].VisitId!=departing && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued),customers);
                }
                flow.AutoSpawn=false;
                while(flow.Snapshot.Count>0)
                {
                    var count=flow.Snapshot.Count; leave.onClick.Invoke();
                    yield return WaitNative(flow,world,()=>flow.Snapshot.Count==count-1 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                }
                foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
            }
            finally { Time.captureDeltaTime=capture; if(scene.IsValid() && scene.isLoaded) pendingUnload=SceneManager.UnloadSceneAsync(scene); }
            yield return null;
        }

        [UnityTest] public IEnumerator NativeDenseTwoQueuedCustomersExitWithoutRepositioning()
        { yield return NativeQueueExitAndAdvance(false,2); }

        [UnityTest] public IEnumerator NativeDetourIntermediateArrivalKeepsTokensAndDisableRebuildResumes()
        { yield return NativeQueueExitAndAdvance(false,4,true); }

        [UnityTest] public IEnumerator NativeCounterBesideEntranceStillAllowsQueueAndFrontDeparture()
        { yield return NativeQueueExitAndAdvance(false,3,false,true); }

        [UnityTest] public IEnumerator EntranceClearanceAllFourCellsRemainWalkableRoutes()
        {
            var capture=Time.captureDeltaTime; Time.captureDeltaTime=1f/60; nativeCaptureToRestore=capture;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            try
            {
                var roots=scene.GetRootGameObjects();
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CustomerFlowController>(true)).Single(); flow.AutoSpawn=false;
                var world=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationWorld>(true)).Single();
                var adapter=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationLayoutAdapter>(true)).Single();
                yield return WaitNative(flow,world,()=>!string.IsNullOrEmpty(flow.SelectedRegisterId));
                var grid=(Transform)new SerializedObject(flow).FindProperty("gridRoot").objectReferenceValue;
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase11/Characters/Shiba/PF_Shiba_Navigation.prefab");
                var actor=Object.Instantiate(prefab,grid.TransformPoint(new Vector3(3.5f,0,.5f)),Quaternion.identity,flow.transform).GetComponent<NavigationActor>();
                Assert.That(actor.TryInitializeRuntimeId("entrance-walk-proof"),Is.True); Assert.That(world.Register(actor),Is.True);
                foreach(var local in new[]{new Vector3(4.5f,0,.5f),new Vector3(4.5f,0,1.5f),new Vector3(3.5f,0,1.5f),new Vector3(3.5f,0,.5f)})
                {
                    var target=grid.TransformPoint(local);
                    Assert.That(adapter.IsPointWalkable(target),Is.True,"Entrance cell "+local);
                    Assert.That(adapter.GetCompletePath(actor.transform.position,target),Is.Not.Null);
                    var arrived=false;
                    Assert.That(world.Service.MoveTo(actor.ActorId,new NavigationTarget(target),r=>arrived=r.Status==MovementStatus.Arrived).Accepted,Is.True);
                    yield return WaitNative(flow,world,()=>arrived);
                    Assert.That(Vector3.Distance(actor.transform.position,target),Is.LessThanOrEqualTo(.081f));
                }
            }
            finally { Time.captureDeltaTime=capture; if(scene.IsValid() && scene.isLoaded) pendingUnload=SceneManager.UnloadSceneAsync(scene); }
            yield return null;
        }

        private IEnumerator NativeQueueExitAndAdvance(bool spread,int customers=3,bool interruptThird=false,bool entranceNeighbor=false,
            int fps=60,bool cashOnly=false,bool replenish=false,bool screenshotLayout=false)
        {
            var capture=Time.captureDeltaTime; Time.captureDeltaTime=1f/fps;
            nativeCaptureToRestore=capture;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            try
            {
                var roots=scene.GetRootGameObjects();
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CustomerFlowController>(true)).Single();
                var world=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationWorld>(true)).Single();
                var adapter=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationLayoutAdapter>(true)).Single();
                flow.AutoSpawn=false;
                yield return WaitNative(flow,world,()=>!string.IsNullOrEmpty(flow.SelectedRegisterId));
                if(entranceNeighbor)
                {
                    var runtime=roots.SelectMany(r=>r.GetComponentsInChildren<AnimalCafe.Decoration.CafeLayoutRuntime>(true)).Single();
                    var support=runtime.Layout.FurnitureInstances.First();
                    Assert.That(runtime.Layout.PlaceFurniture(AnimalCafe.Layout.FurnitureInstance.Restore("12000000000000000000000000000009",support.DefinitionId,new AnimalCafe.Layout.GridPosition(2,0),AnimalCafe.Layout.FurnitureRotation.Degrees0)).Succeeded,Is.True);
                    var decor=roots.SelectMany(r=>r.GetComponentsInChildren<AnimalCafe.Decoration.DecorationModeController>(true)).Single();
                    decor.RefreshConfirmedNavigationPresentation(); adapter.RefreshConfirmedLayout();
                    Assert.That(adapter.RebuildAndValidate().CanResume,Is.True); flow.Step(0);
                }
                if(spread)
                {
                    var runtime=roots.SelectMany(r=>r.GetComponentsInChildren<AnimalCafe.Decoration.CafeLayoutRuntime>(true)).Single();
                    var original=runtime.Layout.FurnitureInstances.Single(f=>!f.InstanceId.StartsWith("1200000000000000000000000000000"));
                    Assert.That(runtime.Layout.MoveFurniture(original.InstanceId,screenshotLayout?new AnimalCafe.Layout.GridPosition(2,6):new AnimalCafe.Layout.GridPosition(1,5)).Succeeded,Is.True);
                    if(!cashOnly) Assert.That(runtime.Layout.MoveFurniture("12000000000000000000000000000002",screenshotLayout?new AnimalCafe.Layout.GridPosition(5,4):new AnimalCafe.Layout.GridPosition(5,5)).Succeeded,Is.True);
                    if(screenshotLayout) Assert.That(runtime.Layout.MoveFurniture("12000000000000000000000000000003",new AnimalCafe.Layout.GridPosition(6,6)).Succeeded,Is.True);
                    var decor=roots.SelectMany(r=>r.GetComponentsInChildren<AnimalCafe.Decoration.DecorationModeController>(true)).Single();
                    decor.RefreshConfirmedNavigationPresentation(); adapter.RefreshConfirmedLayout();
                    Assert.That(adapter.RebuildAndValidate().CanResume,Is.True); flow.Step(0);
                }
                Debug.Log("P12_NATIVE capacity "+string.Join(" | ",flow.Capacity.GetCapacities().Select(c=>c.Kind+" "+c.Used+"/"+c.Limit)));
                for(var i=0;i<customers;i++)
                {
                    Assert.That(flow.TrySpawn(),Is.True,NativeState(flow,world));
                    if(interruptThird && i==3)
                    {
                        var third=world.RegisteredActors.Single(a=>a.ActorId==flow.Snapshot[3].VisitId);
                        var intermediate=third.Agent.destination;
                        Assert.That(Vector3.Distance(intermediate,new Vector3(-2.6f,0,-2.6f)),Is.GreaterThan(.2f),"Must exercise an intermediate detour, not direct entry");
                        yield return WaitNative(flow,world,()=>Vector3.Distance(third.Agent.destination,intermediate)>.2f);
                        Assert.That(flow.Snapshot[3].State,Is.EqualTo(CustomerVisitState.Entering),"Intermediate Arrived must not commit Queued");
                        Assert.That(flow.Capacity.GetCapacities()[0].Occupied,Is.EqualTo(4));
                        Assert.That(flow.Capacity.GetCapacities()[1].Used,Is.EqualTo(4));
                        var positions=world.RegisteredActors.Select(a=>a.transform.position).ToArray();
                        flow.enabled=false;
                        for(var frame=0;frame<10;frame++) yield return null;
                        CollectionAssert.AreEqual(positions,world.RegisteredActors.Select(a=>a.transform.position).ToArray());
                        Assert.That(flow.Snapshot[3].State,Is.EqualTo(CustomerVisitState.Blocked));
                        Assert.That(flow.Capacity.GetCapacities()[0].Occupied,Is.EqualTo(4));
                        adapter.ReleaseOwnedNavigation(); Assert.That(adapter.RebuildAndValidate().CanResume,Is.True);
                        flow.enabled=true; flow.Step(0);
                        CollectionAssert.AreEqual(positions,world.RegisteredActors.Select(a=>a.transform.position).ToArray(),"Rebuild resumes from actual poses");
                    }
                    yield return WaitNative(flow,world,()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                }
                Debug.Log("P12_NATIVE full positions "+NativeState(flow,world));
                if(replenish)
                    for(var cycle=0;cycle<5;cycle++)
                    {
                        Assert.That(flow.TryLetFrontLeave(),Is.True,NativeState(flow,world));
                        yield return WaitNative(flow,world,()=>flow.Snapshot.Count==customers-1 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                        Assert.That(flow.TrySpawn(),Is.True,NativeState(flow,world));
                        yield return WaitNative(flow,world,()=>flow.Snapshot.Count==customers && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                    }
                var ids=flow.Snapshot.Select(v=>v.VisitId).ToArray();
                for(var i=0;i<customers;i++)
                {
                    Assert.That(flow.Snapshot[0].VisitId,Is.EqualTo(ids[i]));
                    Assert.That(flow.TryLetFrontLeave(),Is.True,NativeState(flow,world));
                    var count=customers-1-i;
                    yield return WaitNative(flow,world,()=>flow.Snapshot.Count==count && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                }
                foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
            }
            finally { Time.captureDeltaTime=capture; if(scene.IsValid() && scene.isLoaded) pendingUnload=SceneManager.UnloadSceneAsync(scene); }
            yield return null;
        }
        private IEnumerator WaitNative(CustomerFlowController flow,NavigationWorld world,System.Func<bool> done,int? admissionLimit=null)
        {
            var solids=(System.Collections.Generic.IReadOnlyList<Collider>)typeof(NavigationWorld).GetField("solids",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(world);
            var settledFrames=0;
            var elapsed=0f; var deadline=Time.realtimeSinceStartup+90;
            var trace=new System.Collections.Generic.Queue<string>(); var nextSample=0f;
            var slow=new System.Collections.Generic.Dictionary<NavigationActor,float>();
            var recoveryFlag=typeof(CustomerFlowController).GetField("recoveringQueue",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            for(var frame=0;frame<1000000 && elapsed<60 && Time.realtimeSinceStartup<deadline;frame++)
            {
                // Fixture只接纳本轮目标人数，离场者仍计Total；不借用生产全局移动锁。
                if(admissionLimit.HasValue && flow.Snapshot.Count(v=>v.State!=CustomerVisitState.Exiting)>=admissionLimit.Value)
                    flow.AutoSpawn=false;
                // Arrived回调与下一位前移之间有一帧Queued间隙，不能当作整队已经完成。
                if(done()) { if(++settledFrames>=2) yield break; } else settledFrames=0;
                var actors=world.RegisteredActors.ToArray(); var old=actors.Select(a=>a.transform.position).ToArray();
                if(flow.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked) && !(bool)recoveryFlag.GetValue(flow)) break;
                yield return null;
                elapsed+=Time.deltaTime;
                if(actors.Where((a,i)=>a!=null && Vector3.Distance(old[i],a.transform.position)>.001f &&
                    flow.Snapshot.Any(v=>v.VisitId==a.ActorId && v.State==CustomerVisitState.Advancing)).Count()>=2)
                    concurrentAdvanceFrames++;
                if(actors.Where((a,i)=>a!=null && Vector3.Distance(old[i],a.transform.position)>.001f &&
                    flow.Snapshot.Any(v=>v.VisitId==a.ActorId && v.State==CustomerVisitState.Entering)).Count()>=2)
                    concurrentEntryFrames++;
                if(elapsed>=nextSample)
                {
                    nextSample=elapsed+.25f;
                    foreach(var moving in actors.Where(a=>a!=null && a.Agent.isOnNavMesh && !a.Agent.isStopped))
                    {
                        var index=System.Array.IndexOf(actors,moving);
                        var drivers=(System.Collections.IDictionary)typeof(NavigationWorld).GetField("drivers",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(world);
                        var driver=drivers[moving]; var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                        var owned=(Vector3[])driver.GetType().GetField("corners",flags).GetValue(driver);
                        var cursor=(int)driver.GetType().GetField("corner",flags).GetValue(driver);
                        trace.Enqueue(elapsed.ToString("F2")+" "+moving.ActorId.Substring(moving.ActorId.Length-8)+" pos="+moving.transform.position.ToString("F3")+
                            " wish="+moving.Agent.desiredVelocity.ToString("F3")+" steering="+moving.Agent.steeringTarget.ToString("F3")+
                            " remain="+moving.Agent.remainingDistance.ToString("F3")+" actual="+(Vector3.Distance(old[index],moving.transform.position)/Time.deltaTime).ToString("F3")+
                            " owned="+cursor+":"+string.Join(",",owned.Select(p=>p.ToString("F3"))));
                        while(trace.Count>40) trace.Dequeue();
                    }
                }
                for(var i=0;i<actors.Length;i++)
                {
                    if(actors[i]==null) continue;
                    var current=actors[i].transform.position;
                    Assert.That(Vector3.Distance(current,old[i]),Is.LessThanOrEqualTo(actors[i].Settings.MaxSpeed*Time.deltaTime+.002f),"No teleport during native detour");
                    if((requireSteadyMotion || requireCornerContinuity) && Time.deltaTime>0 && elapsed>.2f)
                    {
                        var active=flow.Snapshot.Any(v=>v.VisitId==actors[i].ActorId && v.State!=CustomerVisitState.Queued && v.State!=CustomerVisitState.Blocked);
                        var clear=active && Vector3.Distance(old[i],actors[i].Agent.destination)>.25f &&
                            actors.All(a=>a==null || a==actors[i] || Vector3.Distance(old[i],a.transform.position)>1.03f) &&
                            (requireCornerContinuity || solids.All(s=>s==null || Vector3.Distance(old[i]+Vector3.up*.65f,s.ClosestPoint(old[i]+Vector3.up*.65f))>.55f));
                        slow.TryGetValue(actors[i],out var duration);
                        var minimumSpeed=actors[i].Settings.MaxSpeed*(requireCornerContinuity?.25f:.9f);
                        duration=clear && Vector3.Distance(current,old[i])/Time.deltaTime<minimumSpeed?duration+Time.deltaTime:0;
                        slow[actors[i]]=duration;
                        Assert.That(duration,Is.LessThanOrEqualTo(.2f),"Clear route must not crawl before a corner/destination: "+NativeState(flow,world));
                    }
                    foreach(var solid in solids)
                        for(var sample=0;sample<=8;sample++)
                        {
                            var point=Vector3.Lerp(old[i],current,sample/8f)+Vector3.up*.65f;
                            Assert.That(Vector3.Distance(point,solid.ClosestPoint(point)),Is.GreaterThanOrEqualTo(actors[i].Settings.AgentRadius+actors[i].Settings.CollisionSkin-.001f),"Native detour must retain furniture skin");
                        }
                    for(var j=0;j<i;j++)
                    {
                        if(actors[j]==null) continue;
                        var relative=old[i]-old[j]; var delta=(current-old[i])-(actors[j].transform.position-old[j]);
                        var t=delta.sqrMagnitude<1e-12f?0:Mathf.Clamp01(-Vector3.Dot(relative,delta)/delta.sqrMagnitude);
                        if((relative+delta*t).magnitude<.909f)
                            Debug.Log("P12_SEPARATION dt="+Time.deltaTime+" t="+t+" pair="+actors[i].ActorId+"/"+actors[j].ActorId+
                                " a="+old[i].ToString("F5")+"->"+current.ToString("F5")+" b="+old[j].ToString("F5")+"->"+actors[j].transform.position.ToString("F5")+" / "+NativeState(flow,world));
                        Assert.That((relative+delta*t).magnitude,Is.GreaterThanOrEqualTo(.909f),"Continuous native actor separation including skin");
                    }
                }
            }
            Debug.Log("P12_NATIVE failed trace\n"+string.Join("\n",trace));
            var visits=(System.Collections.IDictionary)typeof(CustomerFlowController).GetField("visits",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(flow);
            foreach(var visit in visits.Values)
            {
                var type=visit.GetType();
                Debug.Log("P12_VISIT "+type.GetField("Id").GetValue(visit)+" failure="+type.GetField("Failure").GetValue(visit)+
                    " segments="+type.GetField("Segments").GetValue(visit)+" replans="+type.GetField("Replans").GetValue(visit));
            }
            Assert.That(settledFrames,Is.GreaterThanOrEqualTo(2),NativeState(flow,world));
        }
        private static string NativeState(CustomerFlowController flow,NavigationWorld world)
            => flow.Status+" / "+string.Join(" | ",flow.Snapshot.Select(v=>v.VisitId.Substring(v.VisitId.Length-8)+" "+v.State+" "+v.Position.ToString("F3")))+
                " paths="+string.Join(" | ",world.RegisteredActors.Select(a=>a.ActorId.Substring(a.ActorId.Length-8)+":"+string.Join(",",a.Agent.path.corners.Select(p=>p.ToString("F3")))));

        [UnityTest] public IEnumerator ValidationHudHidesDuringDecorationAndRestoresClickableLeaveButton()
        {
            var capture=Time.captureDeltaTime; Time.captureDeltaTime=1f/60;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            try
            {
                var roots=scene.GetRootGameObjects();
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CustomerFlowController>(true)).Single();
                for(var i=0;i<900 && string.IsNullOrEmpty(flow.SelectedRegisterId);i++) yield return null;
                flow.AutoSpawn=false;
                var decor=roots.SelectMany(r=>r.GetComponentsInChildren<AnimalCafe.Decoration.DecorationModeController>(true)).Single();
                var hud=flow.transform.Find("P12 Validation HUD").gameObject;
                var raycaster=hud.GetComponent<UnityEngine.UI.GraphicRaycaster>();
                var events=roots.SelectMany(r=>r.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true)).Single();
                decor.EnterDecorationMode(); yield return null;
                Assert.That(decor.IsOpen,Is.True); Assert.That(hud.activeInHierarchy,Is.False,"P12 panel must not cover Decoration tools");
                var data=new UnityEngine.EventSystems.PointerEventData(events){position=new Vector2(Screen.width*.5f,20)};
                var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                raycaster.Raycast(data,hits); Assert.That(hits.Count,Is.Zero);
                decor.ExitDecorationMode(); yield return null;
                Assert.That(hud.activeInHierarchy,Is.True);
                Canvas.ForceUpdateCanvases();
                var button=hud.transform.Find("Panel/LetFrontLeave").GetComponent<UnityEngine.UI.Button>();
                // Overlay重启后的native draw depth在后续render frame发布。
                for(var frame=0;frame<30 && button.image.depth<0;frame++) { Canvas.ForceUpdateCanvases(); yield return null; }
                var rect=button.GetComponent<RectTransform>();
                data.position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                hits.Clear(); raycaster.Raycast(data,hits);
                Assert.That(hits.Any(h=>h.gameObject.transform.IsChildOf(button.transform)),Is.True,
                    "screen="+Screen.width+"x"+Screen.height+" point="+data.position+" rect="+rect.rect+" depth="+button.image.depth+" target="+button.image.raycastTarget+" enabled="+raycaster.isActiveAndEnabled+" inside="+RectTransformUtility.RectangleContainsScreenPoint(rect,data.position)+" hits="+string.Join(",",hits.Select(h=>h.gameObject.name)));
            }
            finally { Time.captureDeltaTime=capture; if(scene.IsValid() && scene.isLoaded) pendingUnload=SceneManager.UnloadSceneAsync(scene); }
            yield return null;
        }
        [UnityTest] public IEnumerator StableRegisterSelectionAndPickupAnchorExclusionUseRealLayout()
        {
            var capture=Time.captureDeltaTime; Time.captureDeltaTime=1f/60;
            var scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",new LoadSceneParameters(LoadSceneMode.Additive));
            yield return null;
            try
            {
                var roots=scene.GetRootGameObjects();
                var flow=roots.SelectMany(r=>r.GetComponentsInChildren<CustomerFlowController>(true)).Single(); flow.AutoSpawn=false;
                for(var i=0;i<900 && string.IsNullOrEmpty(flow.SelectedRegisterId);i++) yield return null;
                var runtime=roots.SelectMany(r=>r.GetComponentsInChildren<AnimalCafe.Decoration.CafeLayoutRuntime>(true)).Single();
                var decor=roots.SelectMany(r=>r.GetComponentsInChildren<AnimalCafe.Decoration.DecorationModeController>(true)).Single();
                var catalog=(AnimalCafe.Content.FurnitureContentCatalog)new SerializedObject(decor).FindProperty("contentCatalog").objectReferenceValue;
                var support=runtime.Layout.FurnitureInstances.First();
                var slot=catalog.BuildSurfaceSlotCatalog(runtime.Layout.GridSettings).GetForSupport(support.DefinitionId).First().SlotId;
                Assert.That(runtime.Layout.PlaceFurniture(AnimalCafe.Layout.FurnitureInstance.Restore("00000000000000000000000000000002",support.DefinitionId,new AnimalCafe.Layout.GridPosition(0,5),AnimalCafe.Layout.FurnitureRotation.Degrees0)).Succeeded,Is.True);
                Assert.That(runtime.FunctionalSurfaceLayout.PlaceMounted(new AnimalCafe.Layout.SurfaceMountedInstance("00000000000000000000000000000003","equipment.cash-register.01",new AnimalCafe.Layout.SurfaceSlotAddress("00000000000000000000000000000002",slot),AnimalCafe.Layout.FurnitureRotation.Degrees0)).Succeeded,Is.True);
                decor.RefreshConfirmedNavigationPresentation();
                var adapter=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationLayoutAdapter>(true)).Single();
                var readiness=adapter.RebuildAndValidate(); Assert.That(readiness.CanResume,Is.True,readiness.Reason+" / "+string.Join(",",readiness.StationFailures));
                flow.Step(0);
                Assert.That(flow.SelectedRegisterId,Is.EqualTo("00000000000000000000000000000003"),"Lower stable ID inserted last must still win");
                var grid=(Transform)new SerializedObject(flow).FindProperty("gridRoot").objectReferenceValue;
                var queue=(CounterQueueService)typeof(CustomerFlowController).GetField("queue",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(flow);
                var slots=(Vector3[])typeof(CounterQueueService).GetField("slots",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(queue);
                Assert.That(slots.Length,Is.GreaterThan(0),flow.Status);
                foreach(var station in runtime.CurrentReadiness.Stations) foreach(var anchor in station.Anchors.Anchors)
                    if(anchor.Role==AnimalCafe.Layout.InteractionRole.Customer && station.FunctionType==AnimalCafe.Layout.LayoutStationType.PickUpPoint)
                    {
                        var point=grid.TransformPoint(new Vector3((anchor.Position.X+.5f)*runtime.Layout.GridSettings.CellSize,0,(anchor.Position.Y+.5f)*runtime.Layout.GridSettings.CellSize));
                        foreach(var position in slots) Assert.That(Vector3.Distance(position,point),Is.GreaterThanOrEqualTo(.99f));
                    }
                // 空工作位允许排队，不豁免实际角色：目标有人时不占用容量。
                var world=roots.SelectMany(r=>r.GetComponentsInChildren<NavigationWorld>(true)).Single();
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase11/Characters/Shiba/PF_Shiba_Navigation.prefab");
                var occupant=Object.Instantiate(prefab,slots[0],Quaternion.identity,flow.transform).GetComponent<NavigationActor>();
                Assert.That(occupant.TryInitializeRuntimeId("real-worker-occupant"),Is.True); Assert.That(world.Register(occupant),Is.True);
                Assert.That(flow.TrySpawn(),Is.False,"A real body at the head must still prevent admission");
                foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
                world.Unregister(occupant); Object.Destroy(occupant.gameObject); yield return null;
                Assert.That(flow.TrySpawn(),Is.True,NativeState(flow,world));
                yield return WaitNative(flow,world,()=>flow.Snapshot.Single().State==CustomerVisitState.Queued);
            }
            finally { Time.captureDeltaTime=capture; if(scene.IsValid() && scene.isLoaded) pendingUnload=SceneManager.UnloadSceneAsync(scene); }
            yield return null;
        }
    }
}






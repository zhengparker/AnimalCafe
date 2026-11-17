using System;
using System.Collections;
using System.Linq;
using AnimalCafe.CafeLoop;
using AnimalCafe.Customers;
using AnimalCafe.Employees;
using AnimalCafe.Navigation;
using AnimalCafe.Capacity;
using AnimalCafe.Orders;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace AnimalCafe.Tests.EditMode.Phase14
{
    public sealed class CafeLoopPlaySessionTests
    {
        [Serializable] private sealed class SavedScene { public string path; public bool loaded,active; }
        [Serializable] private sealed class SavedScenes { public SavedScene[] scenes; }
        [Serializable] private sealed class SavedCashierEvent
        {
            public string session,actor,visit; public long epoch,sequence,handle,request; public int attempt,generation,revision;
            public CafeTaskEvent Restore()=>new CafeTaskEvent(new CafeTaskToken(session,epoch,sequence,attempt),EmployeeRole.Cashier,actor,
                handle,request,generation,revision,CafeTaskEventKind.ServiceFinished,new CafeWorkCheckpoint(EmployeeRole.Cashier,visit,null,CafeWorkStage.Finished,0));
        }
        private const string Key="AnimalCafe.P14.Task9.Reload.";
        private const string ScenePath="Assets/Scenes/Validation/Phase14CafeLoop.unity";
        private static CustomerFlowController EditSceneCustomers()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ScenePath);
            return !scene.IsValid() || !scene.isLoaded?null:scene.GetRootGameObjects()
                .SelectMany(root=>root.GetComponentsInChildren<CustomerFlowController>(true)).Single();
        }
        private static void RestoreStartupArrivals()
        {
            if(EditorApplication.isPlaying)return;
            var customers=EditSceneCustomers();
            if(customers!=null)customers.AutoSpawn=SessionState.GetBool(Key+"startupAutoSpawn",true);
        }
        private static CafeLoopController receiptOwner;
        private static CustomerFlowController receiptCustomers;
        private static CapacityToken receiptToken;
        private static Transform receiptExit;
        private static string receiptVisit;
        private static Component receiptObserver;
        private static void BeginReceipt(CafeLoopController owner,CustomerFlowController customers,Transform exit)
        { receiptOwner=owner; receiptCustomers=customers; receiptExit=exit; receiptVisit=null; receiptObserver=null; SessionState.SetBool(Key+"actualExit",false); }
        private static void ObserveFirstBody()
        {
            receiptVisit=receiptCustomers.Snapshot.Single().VisitId; receiptCustomers.AutoSpawn=false;
            receiptToken=receiptCustomers.Capacity.GetReservations().Single(r=>r.OwnerId==receiptVisit && r.Kind==CapacityKind.TotalCustomers).Token;
            var body=UnityEngine.Object.FindAnyObjectByType<NavigationWorld>().RegisteredActors.Single(a=>a.ActorId==receiptVisit);
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("AnimalCafe.Tests.PlayMode.Phase14.CafeActualExitObserver")).First(t=>t!=null);
            receiptObserver=body.gameObject.AddComponent(type); type.GetField("VisitId").SetValue(receiptObserver,receiptVisit);
            type.GetField("Receipt").SetValue(receiptObserver,new Action<string,Vector3>(OnActualExit));
        }
        private static void OnActualExit(string id,Vector3 pose)
        {
            Assert.That(id,Is.EqualTo(receiptVisit)); Assert.That(Vector3.Distance(pose,receiptExit.position),Is.LessThanOrEqualTo(.081f));
            Assert.That(receiptCustomers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,receiptToken)).State,Is.EqualTo(ReservationState.Released));
            Assert.That(receiptOwner.Snapshot.Orders.Values.Single(o=>o.CustomerId==id).State,Is.EqualTo(OrderState.Completed)); SessionState.SetBool(Key+"actualExit",true);
        }
        [UnityTest] public IEnumerator RealP14RepeatedPlayWithDomainReload() => Sessions(true);
        [UnityTest] public IEnumerator RealP14RepeatedPlayWithoutDomainReload() => Sessions(false);
        // Read the original startup state without changing admission, time, navigation or actor ownership.
        // 只在五个原边界同步记录；诊断失败标为 UNKNOWN，不覆盖原测试结果。
        private static void ObserveStartup(string boundary,CafeLoopController owner=null,EmployeeFlowController staff=null)
        {
            try
            {
                TestContext.WriteLine("P14_STARTUP_META boundary="+boundary+" frame="+Time.frameCount+" delta="+Time.deltaTime+
                    " unscaled="+Time.unscaledDeltaTime+" capture="+Time.captureDeltaTime+" timeScale="+Time.timeScale+
                    " reload="+SessionState.GetBool(Key+"reload",true)+" run="+SessionState.GetInt(Key+"run",0)+
                    " savedSession="+SessionState.GetString(Key+"session",""));
                if(owner==null)owner=UnityEngine.Object.FindAnyObjectByType<CafeLoopController>();
                if(staff==null)staff=UnityEngine.Object.FindAnyObjectByType<EmployeeFlowController>();
                var customers=UnityEngine.Object.FindAnyObjectByType<CustomerFlowController>();
                var world=UnityEngine.Object.FindAnyObjectByType<NavigationWorld>();
                var tools=UnityEngine.Object.FindAnyObjectByType<CafeLoopValidationController>();
                const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var clock=customers==null?null:typeof(CustomerFlowController).GetField("clock",flags)?.GetValue(customers) as CustomerSpawnClock;
                var heldCustomers=tools==null?null:typeof(CafeLoopValidationController).GetField("customers",flags)?.GetValue(tools) as CustomerFlowController;
                var actors=UnityEngine.Object.FindObjectsByType<NavigationActor>(); // Same active-object scope as the original assertion.
                TestContext.WriteLine("P14_STARTUP_COUNTS boundary="+boundary+" runtimeSession="+(owner==null?"UNKNOWN":owner.Snapshot.SessionId)+
                    " staff="+(staff==null?"UNKNOWN":staff.Snapshot.Count.ToString())+" globalActiveNav="+actors.Length+
                    " customers="+(customers==null?"UNKNOWN":customers.Snapshot.Count.ToString())+
                    " orders="+(owner==null?"UNKNOWN":owner.Snapshot.Orders.Count.ToString())+
                    " paid="+(owner==null?"UNKNOWN":owner.Snapshot.PaidVisitIds.Count.ToString())+
                    " autoSpawn="+(customers==null?"UNKNOWN":customers.AutoSpawn.ToString())+
                    " clockRemaining="+(clock==null?"UNKNOWN":clock.RemainingSeconds.ToString("R"))+
                    " customerEntity="+(customers==null?"UNKNOWN":customers.GetEntityId().ToString())+
                    " holdTargetEntity="+(heldCustomers==null?"UNKNOWN":heldCustomers.GetEntityId().ToString()));
                TestContext.WriteLine("P14_STARTUP_STAFF boundary="+boundary+" rows=["+(staff==null?"UNKNOWN":
                    string.Join(";",staff.Snapshot.Select(e=>e.ActorId+"/"+e.Role+"/"+e.State+"/handle="+e.HandleId)))+"] customers=["+
                    (customers==null?"UNKNOWN":string.Join(";",customers.Snapshot.Select(v=>v.VisitId+"/"+v.State)))+"]");
                foreach(var actor in actors)
                    TestContext.WriteLine("P14_STARTUP_ACTOR boundary="+boundary+" name="+actor.name+" entity="+actor.GetEntityId()+
                        " runtimeActorId="+actor.ActorId+" sceneName="+actor.gameObject.scene.name+" scenePath="+actor.gameObject.scene.path+
                        " sceneLoaded="+actor.gameObject.scene.isLoaded+" activeHierarchy="+actor.gameObject.activeInHierarchy+
                        " componentEnabled="+actor.enabled+" registered="+(world==null?"UNKNOWN":world.RegisteredActors.Contains(actor).ToString()));
            }
            catch(Exception error)
            {
                try{TestContext.WriteLine("P14_STARTUP_DIAGNOSTIC_UNKNOWN boundary="+boundary+" error="+error);}
                catch { /* A diagnostic sink must never replace the original Play assertion. */ }
            }
        }
        private IEnumerator Sessions(bool reload)
        {
            SessionState.SetBool(Key+"enabled",EditorSettings.enterPlayModeOptionsEnabled); SessionState.SetInt(Key+"options",(int)EditorSettings.enterPlayModeOptions);
            SessionState.SetFloat(Key+"capture",Time.captureDeltaTime); SessionState.SetBool(Key+"reload",reload);
            SessionState.SetString(Key+"scenes",JsonUtility.ToJson(new SavedScenes{scenes=EditorSceneManager.GetSceneManagerSetup().Select(s=>new SavedScene{path=s.path,loaded=s.isLoaded,active=s.isActive}).ToArray()}));
            SessionState.SetInt(Key+"run",0); SessionState.SetString(Key+"session",""); SessionState.SetString(Key+"actor","");
            SessionState.SetString(Key+"event","");
            EditorSettings.enterPlayModeOptionsEnabled=true; EditorSettings.enterPlayModeOptions=reload?EnterPlayModeOptions.None:EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            SessionState.SetBool(Key+"startupAutoSpawn",EditSceneCustomers().AutoSpawn);
            try
            {
                while(SessionState.GetInt(Key+"run",0)<2)
                {
                    // Serialize this test-owned startup gate before Play can run its first Update.
                    // 先暂停自动来客，再进入真实Play；保留原两名员工检查，不清理已出生顾客来掩盖竞态。
                    EditSceneCustomers().AutoSpawn=false;
                    yield return new EnterPlayMode(SessionState.GetBool(Key+"reload",true));
                    SessionState.SetBool(Key+"startupHeldAtEnter",!UnityEngine.Object.FindAnyObjectByType<CustomerFlowController>().AutoSpawn);
                    ObserveStartup("enter-return-before-capture");Time.captureDeltaTime=1f/60;
                    CafeLoopController owner=null; EmployeeFlowController staff=null; Exception failure=null;
                    for(var f=0;f<600;f++)
                    {
                        owner=UnityEngine.Object.FindAnyObjectByType<CafeLoopController>(); staff=UnityEngine.Object.FindAnyObjectByType<EmployeeFlowController>();
                        if(f==0)ObserveStartup("before-first-hold",owner,staff);
                        UnityEngine.Object.FindAnyObjectByType<CafeLoopValidationController>()?.HoldArrivals();
                        if(f==0)ObserveStartup("after-first-hold",owner,staff);
                        if(owner!=null && owner.CanProgress && staff.Snapshot.All(e=>e.State==EmployeeState.Idle)) break; yield return null;
                    }
                    try
                    {
                        ObserveStartup("before-ready-assertions",owner,staff);
                        Assert.That(SessionState.GetBool(Key+"startupHeldAtEnter",false),Is.True,"The startup gate must survive actual Play entry before the test coroutine can hold arrivals");
                        Assert.That(owner,Is.Not.Null); Assert.That(owner.CanProgress,Is.True);
                        Assert.That(UnityEngine.Object.FindObjectsByType<CafeLoopController>().Length,Is.EqualTo(1));
                        Assert.That(UnityEngine.Object.FindObjectsByType<CafeLoopStatusView>().Length,Is.EqualTo(1));
                        Assert.That(staff.Snapshot.Count,Is.EqualTo(2),"Original staff snapshot count must be two");
                        Assert.That(UnityEngine.Object.FindObjectsByType<NavigationActor>().Length,Is.EqualTo(2),"Original global active NavigationActor count must be two");
                        Assert.That(owner.Snapshot.Orders,Is.Empty); Assert.That(owner.Snapshot.SessionId,Is.Not.EqualTo(SessionState.GetString(Key+"session","")));
                        Assert.That(owner.Snapshot.Delivery,Is.Empty); Assert.That(UnityEngine.Object.FindAnyObjectByType<PickupCupDisplay>().VisibleOrders,Is.Empty);
                        Assert.That(UnityEngine.Object.FindObjectsByType<Transform>().Any(t=>t.name=="EmployeeHeldCup" || t.name.StartsWith("PickupCup_")),Is.False);
                        var priorEvent=SessionState.GetString(Key+"event","");
                        if(priorEvent.Length>0)
                        {
                            var old=JsonUtility.FromJson<SavedCashierEvent>(priorEvent).Restore();
                            var service=(CafeLoopService)typeof(CafeLoopController).GetField("service",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(owner);
                            service.ReportTask(old); owner.Step(.1f); Assert.That(owner.Snapshot.Orders,Is.Empty,"Old real success cannot cross actual Play sessions");
                        }
                        SessionState.SetString(Key+"session",owner.Snapshot.SessionId); SessionState.SetString(Key+"actor",staff.Snapshot[0].ActorId);
                        staff.IntegratedTaskEvent+=value=>
                        {
                            if(value.Kind==CafeTaskEventKind.ServiceFinished) SessionState.SetString(Key+"event",JsonUtility.ToJson(new SavedCashierEvent
                            {session=value.Token.SessionId,epoch=value.Token.Epoch,sequence=value.Token.Sequence,attempt=value.Token.Attempt,
                                actor=value.ActorId,handle=value.HandleId,request=value.RequestId,generation=value.Generation,revision=value.LayoutRevision,visit=value.Checkpoint.VisitId}));
                        };
                        UnityEngine.Object.FindAnyObjectByType<CafeLoopValidationController>().ResumeArrivals();
                    }
                    catch(Exception error) { ObserveStartup("ready-assertion-catch-before-exit",owner,staff);failure=error; }
                    if(failure==null)
                    {
                        var customers=UnityEngine.Object.FindAnyObjectByType<CustomerFlowController>();
                        var world=UnityEngine.Object.FindAnyObjectByType<NavigationWorld>();
                        var exit=(Transform)typeof(CustomerFlowController).GetField("exit",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(customers);
                        BeginReceipt(owner,customers,exit);
                        for(var f=0;f<6000;f++)
                        {
                            if(receiptVisit==null && customers.Snapshot.Count>0) ObserveFirstBody();
                            if(SessionState.GetBool(Key+"actualExit",false) && customers.Snapshot.Count==0 && staff.Snapshot.All(e=>e.State==EmployeeState.Idle)) break;
                            yield return null;
                        }
                        try
                        {
                            Assert.That(SessionState.GetBool(Key+"actualExit",false),Is.True,"Each real Play must complete a real collection and pose/token exit");
                            Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1)); Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.Completed));
                            Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                            Assert.That(owner.Snapshot.Delivery,Is.Empty); Assert.That(UnityEngine.Object.FindAnyObjectByType<PickupCupDisplay>().VisibleOrders,Is.Empty);
                            Assert.That(staff.Snapshot.All(e=>e.State==EmployeeState.Idle && e.HandleId==0),Is.True);
                            Assert.That(SessionState.GetString(Key+"event",""),Is.Not.Empty);
                        }
                        catch(Exception error) { failure=error; }
                        if(receiptObserver!=null) receiptObserver.GetType().GetField("Receipt").SetValue(receiptObserver,null);
                    }
                    SessionState.SetInt(Key+"run",SessionState.GetInt(Key+"run",0)+1);
                    SessionState.SetString(Key+"failure",failure?.ToString()??"");
                    TestContext.WriteLine("P14_REAL_PLAY reload="+SessionState.GetBool(Key+"reload",true)+" run="+SessionState.GetInt(Key+"run",0)+" session="+SessionState.GetString(Key+"session",""));
                    yield return new ExitPlayMode();
                    Assert.That(Resources.FindObjectsOfTypeAll<NavigationActor>().Any(a=>a.ActorId==SessionState.GetString(Key+"actor","")),Is.False);
                    Assert.That(SessionState.GetString(Key+"failure",""),Is.Empty,"Play assertions must survive true Domain Reload");
                }
            }
            finally
            {
                Time.captureDeltaTime=SessionState.GetFloat(Key+"capture",0);
                EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Key+"options",0); EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Key+"enabled",false);
                if(!EditorApplication.isPlaying) { RestoreStartupArrivals();RestoreScenes(); }
                Assert.That(EditorSettings.enterPlayModeOptions,Is.EqualTo((EnterPlayModeOptions)SessionState.GetInt(Key+"options",0)));
                Assert.That(EditorSettings.enterPlayModeOptionsEnabled,Is.EqualTo(SessionState.GetBool(Key+"enabled",false)));
            }
        }
        [UnityTearDown] public IEnumerator RestoreAfterFailure()
        {
            if(receiptObserver!=null) receiptObserver.GetType().GetField("Receipt").SetValue(receiptObserver,null);
            if(EditorApplication.isPlaying) yield return new ExitPlayMode();
            Time.captureDeltaTime=SessionState.GetFloat(Key+"capture",0);
            EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Key+"options",0); EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Key+"enabled",false);
            RestoreStartupArrivals();RestoreScenes();
        }
        private static void RestoreScenes()
        {
            var saved=JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key+"scenes","{}"));
            if(saved?.scenes!=null && saved.scenes.Length>0) EditorSceneManager.RestoreSceneManagerSetup(saved.scenes.Select(s=>new SceneSetup{path=s.path,isLoaded=s.loaded,isActive=s.active}).ToArray());
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }
}

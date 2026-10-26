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
        private IEnumerator Sessions(bool reload)
        {
            SessionState.SetBool(Key+"enabled",EditorSettings.enterPlayModeOptionsEnabled); SessionState.SetInt(Key+"options",(int)EditorSettings.enterPlayModeOptions);
            SessionState.SetFloat(Key+"capture",Time.captureDeltaTime); SessionState.SetBool(Key+"reload",reload);
            SessionState.SetString(Key+"scenes",JsonUtility.ToJson(new SavedScenes{scenes=EditorSceneManager.GetSceneManagerSetup().Select(s=>new SavedScene{path=s.path,loaded=s.isLoaded,active=s.isActive}).ToArray()}));
            SessionState.SetInt(Key+"run",0); SessionState.SetString(Key+"session",""); SessionState.SetString(Key+"actor","");
            SessionState.SetString(Key+"event","");
            EditorSettings.enterPlayModeOptionsEnabled=true; EditorSettings.enterPlayModeOptions=reload?EnterPlayModeOptions.None:EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.OpenScene("Assets/Scenes/Validation/Phase14CafeLoop.unity",OpenSceneMode.Single);
            try
            {
                while(SessionState.GetInt(Key+"run",0)<2)
                {
                    yield return new EnterPlayMode(SessionState.GetBool(Key+"reload",true)); Time.captureDeltaTime=1f/60;
                    CafeLoopController owner=null; EmployeeFlowController staff=null; Exception failure=null;
                    for(var f=0;f<600;f++)
                    {
                        owner=UnityEngine.Object.FindAnyObjectByType<CafeLoopController>(); staff=UnityEngine.Object.FindAnyObjectByType<EmployeeFlowController>();
                        UnityEngine.Object.FindAnyObjectByType<CafeLoopValidationController>()?.HoldArrivals();
                        if(owner!=null && owner.CanProgress && staff.Snapshot.All(e=>e.State==EmployeeState.Idle)) break; yield return null;
                    }
                    try
                    {
                        Assert.That(owner,Is.Not.Null); Assert.That(owner.CanProgress,Is.True);
                        Assert.That(UnityEngine.Object.FindObjectsByType<CafeLoopController>().Length,Is.EqualTo(1));
                        Assert.That(UnityEngine.Object.FindObjectsByType<CafeLoopStatusView>().Length,Is.EqualTo(1));
                        Assert.That(staff.Snapshot.Count,Is.EqualTo(2)); Assert.That(UnityEngine.Object.FindObjectsByType<NavigationActor>().Length,Is.EqualTo(2));
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
                    catch(Exception error) { failure=error; }
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
                if(!EditorApplication.isPlaying) RestoreScenes();
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
            RestoreScenes();
        }
        private static void RestoreScenes()
        {
            var saved=JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key+"scenes","{}"));
            if(saved?.scenes!=null && saved.scenes.Length>0) EditorSceneManager.RestoreSceneManagerSetup(saved.scenes.Select(s=>new SceneSetup{path=s.path,isLoaded=s.loaded,isActive=s.active}).ToArray());
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        }
    }
}

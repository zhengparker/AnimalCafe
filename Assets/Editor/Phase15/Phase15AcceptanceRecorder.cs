using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AnimalCafe.BusinessDay;
using AnimalCafe.CafeLoop;
using AnimalCafe.Capacity;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Navigation;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.Phase15
{
    // Explicit Editor menu only. No scene component, automatic startup, gameplay command or clock write.
    // 只读验收记录器：原Native到达、同一Actor与Total token实际释放共同确认离店。
    public static class Phase15AcceptanceRecorder
    {
        const string Menu="Tools/AnimalCafe/Phase 15/Acceptance/";
        const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static CafeLoopController loop;static CustomerFlowController flow;static BusinessDayController day;
        static GameTimeService clock;static DecorationModeController decor;static StreamWriter writer;
        static FieldInfo hook;static Delegate prior,installed;static double started,nextSample;static int errors;
        static readonly Dictionary<string,ExitProof> arrivals=new Dictionary<string,ExitProof>();
        static readonly HashSet<string> exits=new HashSet<string>();
        sealed class ExitProof { internal object Life;internal int Generation;internal long Request;internal Vector3 Pose;internal CapacityToken Total; }
        [Serializable] sealed class Row
        {
            public string utc,kind,scene,session,phase,speed,visit,detail;
            public double elapsed,remaining;public long epoch,day,transition,nativeRequest;
            public int frame,orders,completed,inside,actualExits,totalUsed,observerErrors;
            public bool paused,editorPaused,decorOpen,staffIdle,blocked;public float captureDelta,x,y,z;
        }
        static object Read(object value,string name)=>value?.GetType().GetField(name,Flags)?.GetValue(value);
        static T Find<T>() where T:Component=>UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude,FindObjectsSortMode.None).SingleOrDefault();
        [MenuItem(Menu+"Start Passive Recording")]
        public static void StartRecording()
        {
            if(!EditorApplication.isPlaying || writer!=null)return;
            try
            {
            loop=Find<CafeLoopController>();flow=Find<CustomerFlowController>();day=Find<BusinessDayController>();
            clock=Find<GameTimeService>();decor=Find<DecorationModeController>();
            if(loop==null || flow==null || day==null){Debug.LogError("[P15 recorder] One active P15 cafe is required.");return;}
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../outputs/phase15/p15-closeout-20261104/gui/"+
                DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8)));
            Directory.CreateDirectory(folder);writer=new StreamWriter(new FileStream(Path.Combine(folder,"events.jsonl"),FileMode.CreateNew,FileAccess.Write,FileShare.Read));
            arrivals.Clear();exits.Clear();errors=0;started=EditorApplication.timeSinceStartup;nextSample=started;
            hook=typeof(CustomerFlowController).GetField("CorridorObserver",Flags);prior=hook.GetValue(flow) as Delegate;
            Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string> observe=Observe;
            installed=Delegate.Combine(prior,observe);hook.SetValue(flow,installed);
            EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=OnPlayMode;
            Sample("start",folder);Debug.Log("[P15 recorder] Passive capture: "+folder);
            }
            catch(Exception error)
            {
                Failure("start/"+error.GetType().Name);Stop("setup-failed");
                try{Debug.LogError("[P15 recorder] Could not start passive capture: "+error.GetType().Name);}catch(Exception){}
            }
        }
        [MenuItem(Menu+"Start Passive Recording",true)] static bool CanStart()=>EditorApplication.isPlaying && writer==null;
        [MenuItem(Menu+"Snapshot Now")] public static void SnapshotNow(){if(writer!=null)Sample("manual-snapshot","");}
        [MenuItem(Menu+"Stop Passive Recording")] public static void StopRecording()=>Stop("manual-stop");
        static void Tick()
        {
            if(writer==null)return;
            if(!EditorApplication.isPlaying || loop==null || flow==null){Stop("play-or-scene-ended");return;}
            if(EditorApplication.timeSinceStartup-started>=3600){Stop("one-hour-recording-budget");return;}
            if(EditorApplication.timeSinceStartup<nextSample)return;
            nextSample=EditorApplication.timeSinceStartup+1;Sample("sample","");
        }
        static void OnPlayMode(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingPlayMode)Stop("exiting-play");}
        static void Observe(string operation,string id,string other,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
        {
            if(writer==null || string.IsNullOrEmpty(id) ||
                (operation!="segment.callback-result" && operation!="visit.before-remove" && operation!="visit.removed"))return;
            try
            {
                var visits=Read(flow,"visits") as IDictionary;var visit=visits!=null && visits.Contains(id)?visits[id]:null;
                var actor=Read(visit,"Actor") as NavigationActor;var departure=Read(flow,"exit") as Transform;
                if(operation=="segment.callback-result" && visit!=null && actor!=null && departure!=null &&
                    (CustomerVisitState)Read(visit,"State")==CustomerVisitState.Exiting && result!=null && result.Contains("status=Arrived ") &&
                    Vector3.Distance(target,departure.position)<=actor.Settings.ArrivalDistance+actor.Settings.Epsilon)
                {
                    var match=Regex.Match(result,@"request=(\d+)");long request;
                    if(match.Success && long.TryParse(match.Groups[1].Value,out request) && request>0)
                        arrivals[id]=new ExitProof{Life=actor,Generation=(int)Read(visit,"Generation"),Request=request,Pose=target};
                }
                if(operation=="visit.before-remove" && flow.CafeSnapshot.Any(v=>v.VisitId==id && v.ExitArrived))
                {
                    ExitProof proof;
                    if(actor==null || departure==null || !arrivals.TryGetValue(id,out proof) || !ReferenceEquals(proof.Life,actor) ||
                        proof.Generation!=(int)Read(visit,"Generation") || Vector3.Distance(actor.transform.position,departure.position)>actor.Settings.ArrivalDistance+actor.Settings.Epsilon)
                    {Failure("unverified-exit-arrival/"+id);return;}
                    var totals=flow.Capacity.GetReservations().Where(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied).ToArray();
                    if(totals.Length!=1){Failure("unverified-original-total/"+id);return;}proof.Total=totals[0].Token;
                }
                if(operation=="visit.removed")
                {
                    ExitProof proof;if(!arrivals.TryGetValue(id,out proof) || proof.Total==null)return;
                    var released=flow.Capacity.GetReservations().Any(r=>r.OwnerId==id && ReferenceEquals(r.Token,proof.Total) && r.State==ReservationState.Released);
                    if(!released || !exits.Add(id)){Failure("unverified-or-duplicate-release/"+id);return;}
                    var row=Capture("actual-exit","");row.visit=id;row.nativeRequest=proof.Request;row.x=proof.Pose.x;row.y=proof.Pose.y;row.z=proof.Pose.z;Write(row);arrivals.Remove(id);
                }
            }
            catch(Exception error){Failure("callback/"+error.GetType().Name);}
        }
        static Row Capture(string kind,string detail)
        {
            var business=loop.Snapshot;var cycle=day==null?null:day.Snapshot;var staff=loop.ViewSnapshot.Employees;
            return new Row{utc=DateTime.UtcNow.ToString("o"),kind=kind,detail=detail,elapsed=EditorApplication.timeSinceStartup-started,
                scene=loop.gameObject.scene.path,session=business.SessionId,epoch=business.Epoch,day=cycle?.DayNumber??0,
                phase=cycle?.Phase.ToString()??"UNKNOWN",remaining=cycle?.RemainingSeconds??0,transition=cycle?.TransitionSequence??0,
                speed=clock==null?"UNKNOWN":clock.CurrentSpeed.ToString(),paused=clock!=null && clock.CurrentSpeed==GameSpeed.Paused,
                editorPaused=EditorApplication.isPaused,decorOpen=decor!=null && decor.IsOpen,frame=Time.frameCount,captureDelta=Time.captureDeltaTime,
                orders=business.Orders.Count,completed=business.Orders.Values.Count(o=>o.State==AnimalCafe.Orders.OrderState.Completed),inside=flow.Snapshot.Count,
                actualExits=exits.Count,totalUsed=flow.Capacity==null?-1:flow.Capacity.GetCapacities().Single(c=>c.Kind==CapacityKind.TotalCustomers).Used,
                staffIdle=staff.All(s=>s.State==EmployeeState.Idle),blocked=staff.Any(s=>s.State==EmployeeState.Blocked) || flow.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked),observerErrors=errors};
        }
        static void Sample(string kind,string detail){try{Write(Capture(kind,detail));}catch(Exception error){Failure("snapshot/"+error.GetType().Name);}}
        static void Write(Row row){writer.WriteLine(JsonUtility.ToJson(row));writer.Flush();}
        static void Failure(string detail)
        {
            errors++;try{Write(new Row{utc=DateTime.UtcNow.ToString("o"),kind="OBSERVER_UNKNOWN",detail=detail,observerErrors=errors});}catch(Exception){}
            if(errors>=32)Stop("observer-error-budget");
        }
        // Cleanup must never escape through the product's synchronous observer callback.
        // 验收I/O失败只能使证据失效，不得打断原游戏回调。
        static void Stop(string reason)
        {
            var ownedWriter=writer;writer=null;
            var count=exits.Count;var elapsed=EditorApplication.timeSinceStartup-started;
            try
            {
                try{EditorApplication.update-=Tick;}catch(Exception){errors++;}
                try{EditorApplication.playModeStateChanged-=OnPlayMode;}catch(Exception){errors++;}
                try
                {
                    if(flow!=null && hook!=null && installed!=null)
                    {if(ReferenceEquals(hook.GetValue(flow),installed))hook.SetValue(flow,prior);else{errors++;reason+="/UNKNOWN-hook-ownership";}}
                }
                catch(Exception){errors++;reason+="/UNKNOWN-hook-cleanup";}
                if(ownedWriter!=null)
                {
                    try
                    {
                        ownedWriter.WriteLine(JsonUtility.ToJson(new Row{utc=DateTime.UtcNow.ToString("o"),kind="stop",detail=reason,
                            elapsed=elapsed,actualExits=count,observerErrors=errors}));ownedWriter.Flush();
                    }
                    catch(Exception){errors++;reason+="/UNKNOWN-stop-write";}
                    try{ownedWriter.Dispose();}catch(Exception){errors++;reason+="/UNKNOWN-dispose";}
                }
            }
            finally
            {
                loop=null;flow=null;day=null;clock=null;decor=null;hook=null;prior=installed=null;arrivals.Clear();
                try{Debug.Log("[P15 recorder] Stopped: "+reason+"; actual exits="+count+"; observer errors="+errors);}catch(Exception){}
            }
        }
    }
}

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
    // Opt-in bounded original-step history; no planners, Observe or DesiredVelocity getters.
    // 只复制原始运算和已存字段；数组立即复制，诊断结束后才格式化，不重新执行移动判断。
    internal sealed class TrafficWaitFailureTrace:IDisposable
    {
        private readonly ServiceTrafficCoordinator coordinator;private readonly FieldInfo hook;private readonly object previous;
        private readonly List<string> records=new List<string>();private bool overflow;
        internal TrafficWaitFailureTrace(ServiceTrafficCoordinator value)
        {
            coordinator=value;hook=typeof(ServiceTrafficCoordinator).GetField("WaitFailureObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            previous=hook.GetValue(coordinator);hook.SetValue(coordinator,(Action<string>)Record);
        }
        private void Record(string text)
        {
            if(records.Count>=256){overflow=true;hook.SetValue(coordinator,null);return;}
            records.Add("frame="+Time.frameCount+" sceneTime="+Time.time+" "+text);
        }
        public void Dispose()
        {
            hook.SetValue(coordinator,previous);
            TestContext.WriteLine("WAIT_ORIGINAL_TRACE records="+records.Count+" overflow="+overflow);
            foreach(var record in records)TestContext.WriteLine(record);
        }
    }

    // Focused optional witness; original Twenty recipe remains the default path.
    internal sealed class AdmissionFirstEndpointWitness:IDisposable
    {
        private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private readonly CustomerFlowController customers;private readonly NavigationWorld world;private readonly ServiceTrafficCoordinator core;
        private readonly FieldInfo hook;private readonly object previous;private readonly Action<TrafficRequest> installed;
        private readonly MethodInfo hint;private readonly IDictionary entries;private readonly HashSet<TrafficIntentKey> freeFirst=new HashSet<TrafficIntentKey>();
        private readonly List<string> records=new List<string>(),errors=new List<string>();private int flushed;private bool disposed;
        private TrafficRequest original,clearRequest;private object anchor;private CounterQueueService queue;private bool natural,clear;private int birth;
        private static object Read(object value,string field)=>value==null?null:value.GetType().GetField(field,Flags).GetValue(value);
        private static string Key(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
        internal AdmissionFirstEndpointWitness(CafeLoopController owner,CustomerFlowController flow,NavigationWorld navigation)
        {
            customers=flow;world=navigation;core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");entries=(IDictionary)Read(core,"entries");
            hook=typeof(CustomerFlowController).GetField("AdmissionPreparationObserver",Flags);previous=hook.GetValue(customers);
            hint=typeof(ServiceTrafficCoordinator).GetMethod("IsParkingEndpointAvailable",Flags);installed=(Action<TrafficRequest>)Delegate.Combine(previous as Delegate,(Action<TrafficRequest>)Observe);hook.SetValue(customers,installed);
        }
        private bool Unborn(TrafficIntentKey key)=>!customers.Snapshot.Any(v=>v.VisitId==key.ActorId) &&
            !world.RegisteredActors.Any(a=>a!=null && a.ActorId==key.ActorId) && !customers.Capacity.GetReservations().Any(r=>r.OwnerId==key.ActorId) &&
            (queue==null || !queue.Snapshot.Any(row=>row.VisitId==key.ActorId));
        private void Observe(TrafficRequest request)
        {
            if(disposed || clear || original!=null && !request.Intent.Equals(original.Intent) || freeFirst.Contains(request.Intent))return;
            try
            {
                // Exactly one readonly hint for this original request invocation; never Evaluate/Prepare/Refresh/pump.
                var available=(bool)hint.Invoke(core,new object[]{request});
                if(original==null && available){freeFirst.Add(request.Intent);return;}
                var current=CafeLoopRuntimeProof.Field<object>(customers,"pendingAdmissionAnchor");
                if(original==null)
                {
                    original=request;anchor=current;queue=Read(anchor,"Owner") as CounterQueueService;birth=request.RequestedFrame;
                    natural=anchor!=null && queue!=null && !(bool)Read(anchor,"Submitted") && !entries.Contains(request.Intent) && Unborn(request.Intent) &&
                        ((TrafficIntentKey)Read(anchor,"Key")).Equals(request.Intent) && (int)Read(anchor,"FirstFrame")==birth &&
                        ReferenceEquals(request.Snapshot,Read(core,"snapshot")) && request.Intent.Session==(string)Read(core,"session") &&
                        request.Intent.Epoch==(long)Read(core,"epoch") && request.Intent.LayoutRevision==(int)Read(core,"layoutRevision") && !(bool)Read(core,"closed");
                    records.Add("ADMISSION_FIRST_ENDPOINT busy=True natural="+natural+" frame="+Time.frameCount+" first="+birth+" key="+Key(request.Intent)+
                        " snapshot="+request.Snapshot.Version+"/"+request.Snapshot.BodyVersion+" start="+request.Start+" target="+request.Target+
                        " entry="+entries.Contains(request.Intent)+" unborn="+Unborn(request.Intent)+" queueVersion="+queue?.Version);
                    records.Add("ADMISSION_FIRST_ENDPOINT_BODIES "+string.Join(";",request.Snapshot.Bodies.Select(b=>Key(b.Owner)+"/"+b.Position+"/enabled="+b.Enabled+"/active="+b.ActorActive+"/progress="+b.IndependentlyProgressing)));
                    records.Add("ADMISSION_FIRST_ENDPOINT_PARKING "+string.Join(";",request.Snapshot.Parking.Select(f=>Key(f.Owner)+"/"+f.ResourceId+"/"+f.Position)));
                    records.Add("ADMISSION_FIRST_ENDPOINT_RIGHTS "+string.Join(";",entries.Values.Cast<object>().Where(e=>Read(e,"Request")!=null &&
                        ((bool)Read(e,"Active") || (bool)Read(e,"Prepared") || ((Vector3[])Read(e,"Journey")).Length>1)).Select(e=>
                        Key(((TrafficRequest)Read(e,"Request")).Intent)+"/prepared="+Read(e,"Prepared")+"/active="+Read(e,"Active")+
                        "/current="+string.Join(",",((Vector3[])Read(e,"CurrentPath")).Select(v=>v.ToString("R")))+"/journey="+string.Join(",",((Vector3[])Read(e,"Journey")).Select(v=>v.ToString("R"))))));
                }
                else if(available)
                {
                    clear=true;clearRequest=request;
                    if(!ReferenceEquals(current,anchor) || request.RequestedFrame!=birth || (bool)Read(anchor,"Submitted") || entries.Contains(request.Intent) || !Unborn(request.Intent))
                        errors.Add("Natural clear checkpoint lost original unborn anchor/key/frame before Submit");
                    records.Add("ADMISSION_FIRST_ENDPOINT_CLEAR frame="+Time.frameCount+" first="+request.RequestedFrame+" key="+Key(request.Intent)+" target="+request.Target);
                }
            }
            catch(Exception error){errors.Add(error.ToString());}
        }
        private void Flush(){while(flushed<records.Count)TestContext.WriteLine(records[flushed++]);foreach(var error in errors)TestContext.WriteLine("ADMISSION_FIRST_ENDPOINT_ERROR "+error);}
        internal bool Verify()
        {
            Flush();Assert.That(errors,Is.Empty);
            if(original==null)
            {
                foreach(var key in freeFirst)
                    if(entries.Contains(key) && ((TrafficDecision)Read(entries[key],"Decision")).FailureDisposition==TrafficFailureDisposition.Terminal &&
                        (long)Read(entries[key],"AcquiredSequence")==0 && Unborn(key))
                        Assert.Ignore("NOT_PROVEN: first endpoint hint was free; actual unborn source stopped after Submit. Original later-batch failure remains separate.");
                return false;
            }
            if(!natural)Assert.Ignore("NOT_PROVEN: busy hint lacked actual same-scope unborn first-submission identity");
            if(!clear)
            {
                Assert.That(entries.Contains(original.Intent),Is.False,"FEATURE: genuinely busy original admission endpoint must remain unsubmitted, not become a Terminal unborn intent");
                Assert.That(CafeLoopRuntimeProof.Field<object>(customers,"pendingAdmissionAnchor"),Is.SameAs(anchor));
                Assert.That(Read(anchor,"FirstFrame"),Is.EqualTo(birth));Assert.That(Read(anchor,"Key"),Is.EqualTo(original.Intent));
                Assert.That(Read(anchor,"Submitted"),Is.EqualTo(false));Assert.That(Unborn(original.Intent),Is.True);return false;
            }
            if(!entries.Contains(original.Intent))return false;
            var entry=entries[original.Intent];Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(birth));
            var decision=(TrafficDecision)Read(entry,"Decision");Assert.That(decision.FailureDisposition,Is.Not.EqualTo(TrafficFailureDisposition.Terminal),"Natural clear is not permission; actual Core failure must remain a failure");
            var primary=(TrafficRequest)(Read(entry,"JourneyRequest")??Read(entry,"Request"));Assert.That(primary.RequestedFrame,Is.EqualTo(birth));
            Assert.That(clearRequest.Intent,Is.EqualTo(original.Intent));Assert.That(Read(anchor,"Submitted"),Is.EqualTo(true));
            if(!customers.Snapshot.Any(v=>v.VisitId==original.Intent.ActorId))return false;
            Assert.That((long)Read(entry,"RequestSequence"),Is.GreaterThan(0));Assert.That((long)Read(entry,"AcquiredSequence"),Is.GreaterThan(0));
            Assert.That(world.RegisteredActors.Count(a=>a!=null && a.ActorId==original.Intent.ActorId),Is.EqualTo(1));
            Assert.That(queue.Snapshot.Count(row=>row.VisitId==original.Intent.ActorId),Is.EqualTo(1));
            Assert.That(customers.Capacity.GetReservations().Count(r=>r.OwnerId==original.Intent.ActorId && r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied),Is.EqualTo(1));
            records.Add("ADMISSION_FIRST_ENDPOINT_BIRTH sameKey=True first="+birth+" seq="+Read(entry,"RequestSequence")+" acq="+Read(entry,"AcquiredSequence"));Flush();return true;
        }
        internal void Exhausted(){Flush();Assert.That(errors,Is.Empty);Assert.Ignore("NOT_PROVEN: original Twenty bounded run did not establish busy-first then natural-clear birth");}
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            try{if(ReferenceEquals(hook.GetValue(customers),installed))hook.SetValue(customers,previous);else errors.Add("Admission observer changed externally");}
            catch(Exception error){errors.Add(error.ToString());}
            Flush();TestContext.WriteLine("ADMISSION_FIRST_ENDPOINT_STATUS observed="+(original!=null)+" natural="+natural+" clear="+clear+" errors="+errors.Count);
        }
    }
    internal sealed class TwentyTrafficFailureTrace
    {
        private const BindingFlags All=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
        private struct Step
        {
            public int Frame;
            public Vector3 Start,Velocity,Corner,Dynamic,Static,Nav,Final;
            public float Delta;
        }
        private readonly NavigationWorld world;
        private readonly CustomerFlowController customers;
        private readonly EmployeeFlowController employees;
        private readonly FieldInfo hook;
        private readonly Dictionary<string,Step> last=new Dictionary<string,Step>();
        private readonly Queue<Dictionary<string,object>> frames=new Queue<Dictionary<string,object>>();
        internal TwentyTrafficFailureTrace(NavigationWorld w,CustomerFlowController c,EmployeeFlowController e)
        {
            world=w;customers=c;employees=e;hook=typeof(NavigationWorld).GetField("StepObserver",All);
            Assert.That(hook.GetValue(world),Is.Null);
            hook.SetValue(world,(Action<NavigationActor,Vector3,Vector3,Vector3,Vector3,Vector3,Vector3,Vector3,float>)Record);
        }
        private void Record(NavigationActor actor,Vector3 start,Vector3 velocity,Vector3 corner,Vector3 dynamic,Vector3 solid,Vector3 nav,Vector3 final,float delta)
        { last[actor.ActorId]=new Step{Frame=Time.frameCount,Start=start,Velocity=velocity,Corner=corner,Dynamic=dynamic,Static=solid,Nav=nav,Final=final,Delta=delta}; }
        private static object Read(object value,string name)
        {
            if(value==null)return "NO_OBJECT";
            var field=value.GetType().GetField(name,All);
            if(field==null)return "FIELD_UNAVAILABLE";
            var data=field.GetValue(value);
            if(data is IEnumerable<Vector3> path)return path.ToArray();
            if(data is IEnumerable<int> indices)return indices.ToArray();
            if(data is bool[] flags)return flags.ToArray();
            return data;
        }
        private static Dictionary<string,object> Capture(NavigationWorld world,CustomerFlowController customers,EmployeeFlowController employees,
            Dictionary<string,Step> steps=null)
        {
            var visits=(IDictionary)Read(customers,"visits");var requests=(IDictionary)Read(world.Service,"byActor");
            var drivers=(IDictionary)Read(world,"drivers");var rows=new List<Dictionary<string,object>>();
            var staff=employees.Snapshot;
            foreach(var actor in world.RegisteredActors.Where(a=>a!=null))
            {
                var visit=visits.Contains(actor.ActorId)?visits[actor.ActorId]:null;
                var request=requests.Contains(actor.ActorId)?requests[actor.ActorId]:null;
                var driver=drivers.Contains(actor)?drivers[actor]:null;
                var row=new Dictionary<string,object>{{"id",actor.ActorId},{"pose",actor.transform.position},
                    {"radius",actor.Settings.AgentRadius},{"epsilon",actor.Settings.Epsilon},
                    {"staff",staff.Where(e=>e.ActorId==actor.ActorId).Select(e=>e.State.ToString()+"/h"+e.HandleId).ToArray()}};
                if(visit!=null)foreach(var name in new[]{"State","ResumeState","Failure","Generation","RequestId","CurrentSegmentTarget","FinalTarget","Route",
                    "Assignment","PendingPickupSegment","PendingExitSegment","HasCollected","PickupDeparted","PickupPosition","PickupAssigned","PickupInTransit",
                    "OwnedRoutePath","OwnedRouteAssignment","OwnedRouteSegments","OwnedIssuedRequest","OwnedIssuedGeneration","OwnedRouteWasClear","OwnedRoutePhase","OwnedRouteRevision","Segments","Replans",
                    "CounterArrived","CounterPosition","QueueLeft"})row["visit."+name]=Read(visit,name);
                foreach(var name in new[]{"Id","Generation","LayoutRevision","Original","Recovery","NoProgressSeconds","BestRemaining","BestFacing","SegmentSeconds","SegmentLimit","RetryCount","Recovering","OriginalFailure"})row["request."+name]=Read(request,name);
                foreach(var name in new[]{"moving","destination","target","rawCorners","corners","adjustedCorners","corner","pathLength","actualSpeed","failure","ownedAgentType","generation","revision"})row["driver."+name]=Read(driver,name);
                if(steps!=null&&steps.TryGetValue(actor.ActorId,out var step))
                {
                    row["step.frame"]=step.Frame;row["step.dt"]=step.Delta;
                    row["step.start"]=step.Start;row["step.velocity"]=step.Velocity;row["step.corner"]=step.Corner;
                    row["step.dynamic"]=step.Dynamic;row["step.static"]=step.Static;row["step.nav"]=step.Nav;row["step.final"]=step.Final;
                }
                else row["step"]="NOT_OBSERVED";
                rows.Add(row);
            }
            var queue=Read(customers,"pickupQueue") as PickupQueueService;
            var counter=Read(customers,"queue") as CounterQueueService;
            var counterEntries=counter==null?null:((IEnumerable)Read(counter,"entries")).Cast<object>().Select(entry=>new Dictionary<string,object>
            {{"id",Read(entry,"Id")},{"generation",Read(entry,"Generation")},{"slot",Read(entry,"Slot")},{"position",Read(entry,"Position")},
                {"arrived",Read(entry,"Arrived")},{"moving",Read(entry,"Moving")},{"needsReplan",Read(entry,"NeedsReplan")}}).ToArray();
            var coordinator=Read(customers,"traffic") as ServiceTrafficCoordinator;
            var snapshot=Read(customers,"trafficSnapshot") as TrafficWorldSnapshot;
            var trafficRows=new List<Dictionary<string,object>>();
            if(coordinator!=null)
                foreach(DictionaryEntry pair in (IDictionary)Read(coordinator,"entries"))
                {
                    var request=(TrafficRequest)Read(pair.Value,"Request");var decision=(TrafficDecision)Read(pair.Value,"Decision");
                    var missingRequest=(bool)Read(pair.Value,"Retired")?"NOT_RETAINED":"UNKNOWN_MISSING_REQUEST";
                    var row=new Dictionary<string,object>{{"key",pair.Key},{"kind",decision.Kind},{"reason",decision.Reason},
                        {"requestSequence",decision.RequestSequence},{"acquiredSequence",decision.AcquiredSequence},
                        {"queries",coordinator.QueriesConsumed((TrafficIntentKey)pair.Key)},
                        {"requestSnapshotVersion",request==null?(object)missingRequest:request.Snapshot.Version},{"requestBodyVersion",request==null?(object)missingRequest:request.Snapshot.BodyVersion},
                        {"start",request==null?(object)missingRequest:request.Start},{"target",request==null?(object)missingRequest:request.Target},
                        {"blockers",decision.Blockers.Select(b=>Format(b.Owner)+"/"+b.ResourceId+"/"+b.Proof).ToArray()}};
                    foreach(var name in new[]{"Queued","Prepared","Active","Retired","Discarded","JobIndex","WaitRemaining"})row[name]=Read(pair.Value,name);
                    trafficRows.Add(row);
                }
            var owner=world.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CafeLoopController>(true)).SingleOrDefault();
            return new Dictionary<string,object>{{"frame",Time.frameCount},{"time",Time.time},{"worldRevision",Read(world,"currentRevision")},
                {"agentType",Read(world,"ownedAgentType")},{"actors",rows},{"pickupVersion",queue?.Version},
                {"counterVersion",counter?.Version},{"counterAssignments",counter?.Snapshot.ToArray()},{"counterEntries",counterEntries},
                {"pickupAssignments",queue?.Snapshot.ToArray()},{"pickupLeases",queue?.ServiceLeases.ToDictionary(p=>p.Key,p=>p.Value)},
                {"orders",owner?.Snapshot.Orders.Select(p=>p.Key.SessionId+"/"+p.Key.Epoch+"/"+p.Key.OrderId+"/"+p.Value.CustomerId+"/"+p.Value.State).ToArray()},
                {"trafficVersion",snapshot?.Version},{"trafficBodyVersion",snapshot?.BodyVersion},
                {"trafficActorKeys",((Dictionary<string,TrafficIntentKey>)Read(customers,"trafficActorKeys")).ToDictionary(p=>p.Key,p=>p.Value)},
                {"trafficEntries",trafficRows}};
        }
        private static string Format(object value)
        {
            if(value==null)return "NULL";
            if(value is Vector3 vector)return vector.ToString("F9");
            if(value is NavigationTarget target)return target.Position.ToString("F9")+" facing="+target.Facing;
            if(value is QueueAssignment assignment)return assignment.VisitId+"/g"+assignment.Generation+"/slot"+assignment.SlotIndex+"@"+assignment.Position.ToString("F9")+" arrived="+assignment.Arrived;
            if(value is TrafficIntentKey key)return key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
            if(value is IDictionary map)return string.Join(" ",map.Keys.Cast<object>().Select(key=>key+"="+Format(map[key])));
            if(value is IEnumerable sequence && !(value is string))return "["+string.Join(";\n",sequence.Cast<object>().Select(Format))+"]";
            return value.ToString();
        }
        internal void Sample() { frames.Enqueue(Capture(world,customers,employees,last));while(frames.Count>64)frames.Dequeue(); }
        // Only the original final 1x failure calls this once; stored facts cannot advance or repair the queue.
        // 原最终失败处同步复制字段；不 Prepare/Query/Refresh/DrainReady，不改变原等待或断言。
        internal static string CounterGapCurrent(NavigationWorld world,CustomerFlowController customers,bool storedOnly=false)
        {
            try
            {
                string Ref(object value)=>value==null?"NULL":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
                object Stored(object value,string name)
                {
                    if(value==null)return null;
                    var field=value.GetType().GetField(name,All)??value.GetType().GetField("<"+name+">k__BackingField",All);
                    return field==null?(object)("UNKNOWN_FIELD_"+name):field.GetValue(value);
                }
                object Value(object value)=>value==null || value is string || value.GetType().IsValueType || value is IEnumerable?value:(object)Ref(value);
                Dictionary<string,object> Facts(object value,params string[] names)
                {
                    var facts=new Dictionary<string,object>{{"ref",Ref(value)}};
                    foreach(var name in names)facts[name]=Value(Stored(value,name));return facts;
                }
                object Rows(object source,params string[] names)
                {
                    if(source==null || source is string)return source;
                    if(source is IDictionary map)return map.Keys.Cast<object>().Select(key=>
                    {var row=Facts(map[key],names);row["mapKey"]=Value(key);return row;}).ToArray();
                    return source is IEnumerable sequence?(object)sequence.Cast<object>().Select(value=>Facts(value,names)).ToArray():"UNKNOWN_NOT_COLLECTION";
                }
                var counter=Stored(customers,"queue") as CounterQueueService;var core=Stored(customers,"traffic") as ServiceTrafficCoordinator;
                var visits=Stored(customers,"visits") as IDictionary;var entries=Stored(core,"entries") as IDictionary;
                var owner=world.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CafeLoopController>(true)).SingleOrDefault();
                var data=new Dictionary<string,object>{{"frame",Time.frameCount},{"ownerCanProgress",storedOnly?(object)"NOT_EVALUATED_STORED_ONLY":owner==null?(object)"UNKNOWN_OWNER":owner.CanProgress},
                    {"flow",Facts(customers,"integratedMode","integratedHeld","suspended","disposed","recoveringQueue","trafficSession","trafficEpoch","revision")},
                    {"queue",Facts(counter,"exiting","slots","preparationRevision")},{"queueVersion",counter==null?(object)"UNKNOWN_QUEUE":counter.Version},
                    {"queueRows",counter?.Snapshot.ToArray()},{"queueEntries",counter==null?null:((IEnumerable)Stored(counter,"entries")).Cast<object>().Select(e=>Facts(e,"Id","Generation","Slot","Position","Arrived","Moving","NeedsReplan")).ToArray()},
                    {"core",Facts(core,"closed","paused","session","epoch","layoutRevision","queryFrame","QueriesThisFrame")},
                    {"coreSnapshot",Facts(Stored(core,"snapshot"),"Version","BodyVersion")},
                    {"wakeups",Stored(core,"wakeups")},{"workKeys",((IEnumerable)Stored(core,"workEntries")).Cast<object>().Select(e=>Stored(e,"Key")).ToArray()},
                    {"trafficKeys",Stored(customers,"trafficKeys")},{"trafficActorKeys",Stored(customers,"trafficActorKeys")}};
                if(storedOnly)
                {
                    data["context"]="EXACT_TWOX_FINAL_STORED_ONLY";
                    var service=Stored(owner,"service");var cashier=Stored(service,"cashier");var barista=Stored(service,"barista");
                    data["CLCStored"]=Facts(owner,"held","stepping","comboReady","layoutPending","layoutFailed","lifecycleFailure","pickupWaitVisit","pickupWaitReason");
                    data["pickupWaitOriginalFrame"]="UNKNOWN_NOT_STORED";
                    data["serviceStored"]=Facts(service,"sessionId","epoch","sequence","permit","ticking","disposed","resuming","failure","visualFailure","projectionDirty","cashier","barista","pickupMoves","exitMoves","paidFifo","collected","events");
                    data["paidOrderKeys"]=Rows(Stored(service,"paid"),"SessionId","Epoch","OrderId");
                    data["actualOrderStore"]=Rows(Stored(Stored(service,"orders"),"orders"),"OrderId","CustomerId","ProductId","ClaimantId","State","TerminationReason");
                    data["serviceVisitCache"]=Rows(Stored(service,"visits"),"VisitId","NavigationState","CounterArrived","PickupArrived","ExitArrived","IsEnabled");
                    data["serviceEventQueue"]=Rows(Stored(service,"events"),"Token","Role","ActorId","HandleId","RequestId","Generation","LayoutRevision","Kind","Checkpoint","Failure");
                    data["cashierWork"]=Facts(cashier,"Token","Checkpoint","ActorId","Handle","Generation","LayoutRevision","Starting","Accepted","Blocked","Observed","SubmissionPending","Arrival");
                    data["cashierTicket"]=Facts(Stored(cashier,"Token"),"SessionId","Epoch","Sequence","Attempt");
                    data["cashierCheckpoint"]=Facts(Stored(cashier,"Checkpoint"),"Role","VisitId","Order","Stage","RemainingWorkSeconds");
                    data["baristaWork"]=Facts(barista,"Token","Checkpoint","ActorId","Handle","Generation","LayoutRevision","Starting","Accepted","Blocked","Observed","SubmissionPending","Arrival");
                    data["baristaTicket"]=Facts(Stored(barista,"Token"),"SessionId","Epoch","Sequence","Attempt");
                    var pickup=Stored(customers,"pickupQueue");data["pickupOwnerStored"]=Facts(pickup,"Version","generation","waitSlots","deliveryOwner","deliveryLifetime","collectionOwner","deliveryPosition","collectionPosition");
                    data["pickupReservationsAll"]=Rows(Stored(pickup,"reservations"),"Id","Slot","Generation","Target","Paid","Arrived","Advancing","Yielding");
                    data["pickupTransferredPaidQueue"]=Rows(Stored(pickup,"paidQueue"),"Id","Slot","Generation","Target","Paid","Arrived","Advancing","Yielding");
                    var families=Stored(customers,"trafficCandidates") as IDictionary;
                    data["FamilySources"]=families==null?(object)"UNKNOWN_NO_CURSOR_MAP":families.Keys.Cast<object>().Select(key=>
                    {
                        var cursor=families[key];var preparation=Stored(cursor,"PickupPreparation");
                        var row=Facts(cursor,"Key","ActorLife","Role","Origin","CurrentIndex","AttemptedIndices","Source","PickupPreparation","CollectionRelease","DeliveryContext","DeliveryPreview","DeliveryOwner","OriginalDeliveryPlan");
                        row["source"]=Facts(Stored(cursor,"Source"),"Intent","Kind","Reason","FailureDisposition","RequestSequence","AcquiredSequence");
                        row["preparation"]=Facts(preparation,"Key","OwnerLife","Owner","Claim","Assignment","Operation","Phase","Generation","PhaseSequence","FirstFrame","CandidateIndex","Start","Target","Selection","Issued","Pending","Movement","Preview","CurrentPreview");
                        row["currentPreview"]=Facts(Stored(preparation,"CurrentPreview"),"Kind","Id","ExpectedVersion","Consumed","Target","Assignment");
                        row["collectionRelease"]=Facts(Stored(cursor,"CollectionRelease"),"Key","ReturnKey","ReturnSource","Owner","Claim","Lease","Pending","Movement","FirstFrame");return row;
                    }).ToArray();
                    var roster=Stored(Stored(owner,"employees"),"employees") as IEnumerable;
                    data["supplierMovesStored"]=roster==null?(object)"UNKNOWN_NO_ROSTER":roster.Cast<object>().Select(employee=>new Dictionary<string,object>
                    {
                        {"employee",Facts(employee,"Role","Id","Failure","Actor","AcceptedToken","Checkpoint","HighestHandle","Runner","Port")},
                        {"acceptedTicket",Facts(Stored(employee,"AcceptedToken"),"SessionId","Epoch","Sequence","Attempt")},
                        {"acceptedCheckpoint",Facts(Stored(employee,"Checkpoint"),"Role","VisitId","Order","Stage","RemainingWorkSeconds")},
                        {"runner",Facts(Stored(employee,"Runner"),"state","generation","layoutRevision","hasCheckpoint","handleId","taskToken","checkpoint","moveLeg","pendingArrival","suspendedMove","failure")},
                        {"runnerMoveLeg",Facts(Stored(Stored(employee,"Runner"),"moveLeg"),"Generation","LayoutRevision","NativeLegGeneration","Target","AwaitingTraffic","TrafficIntermediate","RequestId","Completed","State")},
                        {"port",Facts(Stored(employee,"Port"),"session","movementLife","requests","trafficMove","trafficState","workMoveSequence","supplierHasCurrentMove")},
                        {"portTrafficMove",Facts(Stored(Stored(employee,"Port"),"trafficMove"),"Intent","Source","PendingSource","PreparationContext","PreparationFrame","PreparationOrigin","Index","NativeGeneration","Suspended","Goal","Target")}
                    }).ToArray();
                }
                var visitRows=new List<Dictionary<string,object>>();
                foreach(DictionaryEntry pair in visits)
                {
                    var visit=pair.Value;var id=(string)pair.Key;var anchor=Stored(visit,"PendingCounterAdvance");var actor=Stored(visit,"Actor") as NavigationActor;
                    var row=Facts(visit,"Id","State","ResumeState","Generation","RequestId","Failure","CounterArrived","CounterPosition","QueueLeft","HasCollected","TrafficMovePending","TrafficPhaseSequence","TrafficCandidateFrames");
                    row["actorRef"]=Ref(actor);row["actorEnabled"]=actor!=null && actor.isActiveAndEnabled;row["agentEnabled"]=actor!=null && actor.Agent!=null && actor.Agent.enabled;
                    row["actorPose"]=actor==null?(object)"UNKNOWN_ACTOR":actor.transform.position;row["actorSettings"]=actor==null?(object)"UNKNOWN_ACTOR":new[]{actor.Settings.AgentRadius,actor.Settings.CollisionSkin,actor.Settings.Epsilon};
                    row["totalReservations"]=customers.Capacity.GetReservations().Where(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Select(r=>new Dictionary<string,object>{{"tokenRef",Ref(r.Token)},{"state",r.State}}).ToArray();
                    row["agentOnMesh"]=actor!=null && actor.Agent!=null && actor.Agent.enabled && actor.Agent.isOnNavMesh;row["actorRegistered"]=actor!=null && world.RegisteredActors.Any(a=>ReferenceEquals(a,actor));
                    row["anchor"]=Facts(anchor,"Key","Generation","FirstFrame","PhaseSequence","Phase","Recovery","Assignment","Target","Start","Radius","Skin","Epsilon","Retired");
                    row["anchorRefMatches"]=anchor==null?(object)"UNKNOWN_NO_ANCHOR":new Dictionary<string,object>{{"flow",ReferenceEquals(Stored(anchor,"Flow"),customers)},{"visit",ReferenceEquals(Stored(anchor,"Visit"),visit)},
                        {"actor",ReferenceEquals(Stored(anchor,"Actor"),actor)},{"counter",ReferenceEquals(Stored(anchor,"Counter"),counter)},{"core",ReferenceEquals(Stored(anchor,"Coordinator"),core)},
                        {"world",ReferenceEquals(Stored(anchor,"World"),world)},{"adapter",ReferenceEquals(Stored(anchor,"Adapter"),Stored(customers,"adapter"))},{"capacity",ReferenceEquals(Stored(anchor,"Capacity"),customers.Capacity)},
                        {"admission",ReferenceEquals(Stored(anchor,"Admission"),Stored(visit,"Admission"))},{"selection",ReferenceEquals(Stored(anchor,"Selection"),Stored(visit,"TrafficSelection"))},
                        {"issued",ReferenceEquals(Stored(anchor,"Issued"),Stored(visit,"IssuedTraffic"))},{"pending",ReferenceEquals(Stored(anchor,"Pending"),Stored(visit,"PendingTraffic"))},{"movement",ReferenceEquals(Stored(anchor,"Movement"),Stored(visit,"TrafficMove"))}};
                    var claim=Stored(anchor,"Claim") as CounterQueueService.AssignmentClaim;var preview=Stored(anchor,"Preview") as CounterQueueService.PreparedMutation;
                    row["claimRef"]=Ref(claim);row["claimIsCurrent"]=storedOnly?(object)"NOT_EVALUATED_STORED_ONLY":claim==null?(object)"UNKNOWN_NO_CLAIM":claim.IsCurrent;row["claimAssignment"]=claim==null?(object)"UNKNOWN_NO_CLAIM":claim.Assignment;
                    row["preview"]=Facts(preview,"Kind","Id","ExpectedVersion","Consumed");row["previewIsCurrent"]=storedOnly?(object)"NOT_EVALUATED_STORED_ONLY":preview==null?(object)"UNKNOWN_NO_PREVIEW":preview.IsCurrent;row["previewAssignment"]=preview==null?(object)"UNKNOWN_NO_PREVIEW":preview.Assignment;
                    row["anchorSource"]=Facts(Stored(anchor,"Source"),"Kind","Reason","FailureDisposition","RequestSequence","AcquiredSequence");
                    foreach(var name in new[]{"PendingCounter","PendingTraffic","IssuedTraffic","TrafficSelection","TrafficMove"})
                        row[name]=Facts(Stored(visit,name),"Key","Intent","Generation","FirstFrame","Phase","Marked","Source","Handle","Coordinator","Claim","Counter","Actor","Admission");
                    if(storedOnly)
                    {
                        var reservation=Stored(visit,"PendingReservationAnchor");var originalCounter=Stored(reservation,"CounterClaim");
                        var candidates=Stored(customers,"trafficCandidates") as IDictionary;var reservationKey=Stored(reservation,"Key");
                        var unpaidCursor=reservationKey is TrafficIntentKey rk && candidates!=null && candidates.Contains(rk)?candidates[rk]:null;
                        row["unpaidAnchor"]=Facts(reservation,"Key","Owner","Counter","CounterClaim","Visit","Actor","Admission","Capacity","World","Phase","Generation","Cursor","SideIndex","Side","Start","Work","Exit","FirstFrame","Submitted","Eligibility");
                        row["unpaidClaim"]=Facts(originalCounter,"owner","entry","arrived","moving","needsReplan","ownExiting","preparationRevision","CapturedOwnerVersion","Assignment");
                        row["unpaidOriginalCounterEntry"]=Facts(Stored(originalCounter,"entry"),"Id","Slot","Generation","Position","Arrived","Moving","NeedsReplan");
                        row["unpaidPreview"]=Facts(Stored(reservation,"Preview"),"Kind","Id","ExpectedVersion","Consumed","Assignment");
                        row["unpaidCursor"]=Facts(unpaidCursor,"Key","ActorLife","Origin","Role","CurrentIndex","AttemptedIndices","StaticInvalidIndices","Source","UnpaidEligibility","UnpaidEligibilityAttempts","UnpaidEligibilityRound");
                        var paid=Stored(visit,"PaidPickup");var parking=Stored(visit,"PickupParking");var pickupOwner=Stored(visit,"PickupOwner");
                        row["PaidPickup"]=Facts(paid,"Order","FirstFrame","Intent","ActiveIntent","Accepted","Transferred","Failed","RecoveryUsed","RecoveryPending","Parking","RecoveryReceipt","Recovery","Failure","Source","Callback","Selection");
                        row["PaidOrderIdentity"]=Facts(Stored(paid,"Order"),"SessionId","Epoch","OrderId");
                        row["paidFirstFrameAge"]=Stored(paid,"FirstFrame") is int first?(object)(Time.frameCount-first):"UNKNOWN_NO_PAID_FRAME";
                        row["parkingRight"]=Facts(parking,"Coordinator","Actor","Admission","Owner","Claim","Token","Source","Proof","Fact","Revision","CollisionSkin","Epsilon","Retired");
                        row["parkingSource"]=Facts(Stored(parking,"Source"),"Intent","Kind","Reason","FailureDisposition","RequestSequence","AcquiredSequence");
                        row["parkingClaim"]=Facts(Stored(parking,"Claim"),"owner","entry","collectionLease","Assignment");
                        row["parkingFact"]=Facts(Stored(parking,"Fact"),"Owner","CanonicalOwner","OwnerVersion","ResourceId","Position","Radius","SafetyMargin","BusinessOwner","ReleaseObligation","SemanticCheckpoint");
                        row["pickupOwnerRef"]=Ref(pickupOwner);row["pickupOwnerVersion"]=Stored(pickupOwner,"Version");
                        row["actualOwnerReservations"]=Rows(Stored(pickupOwner,"reservations"),"Id","Slot","Generation","Target","Paid","Arrived","Advancing","Yielding");
                        row["actualOwnerPaidQueue"]=Rows(Stored(pickupOwner,"paidQueue"),"Id","Slot","Generation","Target","Paid","Arrived","Advancing","Yielding");
                        row["pickupIdentityStored"]=Facts(visit,"PickupOwner","ReservedPickup","ReservedPickupOwner","ReservedPickupRevision","PickupTrafficPermit","PickupParking","PaidPickup","ReturnPreparation");
                        row["movementDetails"]=Facts(Stored(visit,"TrafficMove"),"Intent","Source","PendingSource","PreparationContext","PreparationFrame","PreparationOrigin","Resource","Index","NativeGeneration","Suspended","Goal","Target");
                        row["selectionDetails"]=Facts(Stored(visit,"TrafficSelection"),"Intent","Source","Phase","FirstFrame","Assignment","Counter","CounterEntry","Pickup","PickupEntry");
                        var native=Stored(Stored(world,"service"),"byActor") as IDictionary;
                        row["actualNativeRequest"]=Facts(native!=null && native.Contains(id)?native[id]:null,"Id","ActorId","Generation","LayoutRevision","Original","Recovery","Callback","RetryCount","Recovering","OriginalFailure","PendingSeconds","SegmentSeconds","NoProgressSeconds");
                    }
                    var coreRows=new List<Dictionary<string,object>>();
                    foreach(DictionaryEntry entryPair in entries)
                    {
                        if(((TrafficIntentKey)entryPair.Key).ActorId!=id)continue;var entry=entryPair.Value;var decision=Stored(entry,"Decision") as TrafficDecision;
                        var er=Facts(entry,"Key","Queued","Prepared","Active","Retired","Discarded","RequestSequence","AcquiredSequence","FirstFrame","Queries","FiniteLimit","WaitRemaining","FrozenOwners","FrozenResources","WaitingBlockers","CurrentPath","Journey","JourneyPermit","JourneyRequest","PendingHandoff");
                        er["decision"]=decision==null?(object)"UNKNOWN_NO_DECISION":decision.Kind+"/"+decision.FailureDisposition+"/"+decision.Reason;
                        er["sameAnchorSource"]=anchor==null?(object)"UNKNOWN_NO_ANCHOR":ReferenceEquals(Stored(anchor,"Source"),decision);er["request"]=Facts(Stored(entry,"Request"),"Intent","RequestedFrame","Start","Target","Radius","SafetyMargin","NativeStamp");
                        er["leg"]=Facts(Stored(entry,"Leg"),"Index","Generation","NeedsQuery","Committed","Bound","Completed","Suspended","ReprepareAllowed","Stamp","ActorLife","Path","Suffix");
                        er["handoff"]=Facts(Stored(entry,"PendingHandoff"),"Handle","Source","OriginalClaim","OriginalClaims","ActorLife","Remaining","Dependencies");
                        if(storedOnly)
                        {
                            var paid=Stored(visit,"PaidPickup");var paidIntent=Stored(paid,"Intent");var activeIntent=Stored(paid,"ActiveIntent");
                            er["matchesPaidIntent"]=paidIntent is TrafficIntentKey pi?(object)((TrafficIntentKey)entryPair.Key).Equals(pi):"UNKNOWN_NO_PAID_INTENT";
                            er["matchesPaidActiveIntent"]=activeIntent is TrafficIntentKey ai?(object)((TrafficIntentKey)entryPair.Key).Equals(ai):"UNKNOWN_NO_ACTIVE_PAID_INTENT";
                            er["actualDecisionRef"]=Ref(decision);er["requestSnapshot"]=Facts(Stored(Stored(entry,"Request"),"Snapshot"),"Version","BodyVersion");
                            er["preparationBody"]=Facts(Stored(entry,"PreparationBody"),"Owner","Position","Radius","Enabled","ActorActive","IndependentlyProgressing","RemainingSeconds","SafetyMargin","ActorLife");
                            er["preparationClaims"]=Rows(Stored(entry,"PreparationClaims"),"Owner","CanonicalOwner","OwnerVersion","ResourceId","Position","Radius","SafetyMargin","BusinessOwner","ReleaseObligation","SemanticCheckpoint");
                            er["preparationIdentityCaptured"]=Stored(entry,"PreparationIdentityCaptured");er["nativeStamp"]=Facts(Stored(Stored(entry,"Leg"),"Stamp"),"RequestId","LegGeneration");
                            er["unpaidStored"]=Facts(entry,"UnpaidEligibility","UnpaidEligibilityHeld","UnpaidEligibilityQueryFault","UnpaidEligibilitySpatialRefusal","UnpaidEligibilitySpatialActors","UnpaidEligibilityFacts");
                            er["unpaidActivities"]=Rows(Stored(entry,"UnpaidEligibilityNativeActivity"),"Owner","Leg","Source","ActorLife","Stamp","Index","Generation","Target","Path");
                            var currentWorld=Stored(core,"snapshot") as TrafficWorldSnapshot;
                            er["currentOwnClaims"]=Rows(currentWorld?.Parking.Where(p=>p.Owner.ActorId==id),"Owner","CanonicalOwner","OwnerVersion","ResourceId","Position","Radius","SafetyMargin","BusinessOwner","ReleaseObligation","SemanticCheckpoint");
                            er["currentOwnClaimCheckpoints"]=Rows(currentWorld?.Parking.Where(p=>p.Owner.ActorId==id).Select(p=>p.SemanticCheckpoint),"owner","entry","arrived","moving","needsReplan","ownExiting","preparationRevision","CapturedOwnerVersion","Assignment");
                            er["originalClaimCheckpoints"]=Rows((Stored(entry,"PreparationClaims") as IEnumerable)?.Cast<object>().Select(p=>Stored(p,"SemanticCheckpoint")),"owner","entry","arrived","moving","needsReplan","ownExiting","preparationRevision","CapturedOwnerVersion","Assignment");
                            er["requestRoles"]=Rows(Stored(Stored(entry,"Request"),"Requirements"),"ResourceId","Start","Goal","Clearance","SelectedJourney","AvoidProposedParking","Obstacles");
                            er["originalJourneyRoles"]=Rows(Stored(Stored(entry,"JourneyRequest"),"Requirements"),"ResourceId","Start","Goal","Clearance","SelectedJourney","AvoidProposedParking","Obstacles");
                            er["originalJourneyProofs"]=Rows(Stored(Stored(entry,"JourneyPermit"),"ProofRoutes"),"ResourceId","Waypoints","ExpandedPath","LegEnds");
                            er["producerHold"]=Stored(entry,"ProducerPreviewHeld");
                        }
                        coreRows.Add(er);
                    }
                    row["coreEntries"]=coreRows;visitRows.Add(row);
                }
                data["visits"]=visitRows;return "TWENTY_COUNTER_GAP cap=1 "+Format(data);
            }
            catch(Exception error){return "TWENTY_COUNTER_GAP cap=1 status=UNKNOWN error="+error.GetType().Name+"/"+error.Message;}
        }
        internal static string Current(NavigationWorld world,CustomerFlowController customers,EmployeeFlowController employees)
            => "TWENTY_COLD "+Format(Capture(world,customers,employees));
        internal void Finish()
        {
            hook.SetValue(world,null);Sample();
            var path=Path.Combine("outputs","phase15","twenty-traffic-witness",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path,TestContext.CurrentContext.Test.Name+".txt"),"Stored fields and original step results only; missing data stays explicit.\n"+
                string.Join("\n\n",frames.Select(Format)));
            TestContext.WriteLine("TWENTY_TRACE "+path+" frames="+frames.Count);
        }
    }
    // Default-off original refusal recorder. It never evaluates a guard, refreshes policy, or queries a path.
    internal sealed class UnpaidPreparationOriginalStageReceipt:IDisposable
    {
        private const BindingFlags All=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        private readonly NavigationWorld world;private readonly CustomerFlowController flow;private readonly FieldInfo hook;
        private readonly Delegate prior,installed;private readonly Dictionary<object,string> last=new Dictionary<object,string>();
        private readonly HashSet<object> captured=new HashSet<object>();private readonly List<string> first=new List<string>();
        private readonly List<string> errors=new List<string>();private bool disposed;
        private static object Read(object value,string name)=>value==null?null:(value.GetType().GetField(name,All)??value.GetType().GetField("<"+name+">k__BackingField",All))?.GetValue(value);
        internal UnpaidPreparationOriginalStageReceipt(NavigationWorld world,CustomerFlowController flow)
        {
            this.world=world;this.flow=flow;hook=typeof(CustomerFlowController).GetField("CorridorObserver",All);
            prior=hook.GetValue(flow) as Delegate;
            installed=Delegate.Combine(prior,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)Observe);hook.SetValue(flow,installed);
        }
        private void Observe(string operation,string id,string other,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
        {
            if(disposed || operation!="reserve.original-refusal" || id==null)return;
            try
            {
                var visits=Read(flow,"visits") as IDictionary;if(visits==null || !visits.Contains(id))return;
                var anchor=Read(visits[id],"PendingReservationAnchor");if(!(Read(anchor,"Key") is TrafficIntentKey key))return;
                var core=Read(flow,"traffic");var entries=Read(core,"entries") as IDictionary;if(entries==null || !entries.Contains(key))return;
                var entry=entries[key];last.TryGetValue(entry,out var previous);
                var text="frame="+Time.frameCount+" actor="+id+" entry="+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(entry)+" original="+result;
                if(last.Count<32 || last.ContainsKey(entry))last[entry]=text;
                var decision=Read(entry,"Decision") as TrafficDecision;
                var stage=(result??"").Split(' ').FirstOrDefault(part=>part.StartsWith("stage=",StringComparison.Ordinal))??"UNKNOWN_STAGE";
                var originalGuardFalse=stage=="stage=preview" || stage=="stage=corePrepare" || stage=="stage=anchorCurrent" || stage=="stage=actor-ready" ||
                    stage=="stage=employee-ready" || stage=="stage=describe-or-alt" || stage.StartsWith("stage=postCurrent",StringComparison.Ordinal) ||
                    stage=="stage=select-false";
                if(originalGuardFalse && decision!=null && decision.FailureDisposition==TrafficFailureDisposition.None &&
                    decision.AcquiredSequence==0 && captured.Count<32 && captured.Add(entry))
                {
                    // Keep each entry's first original refusal; retain the most recent four entries.
                    if(first.Count==4)first.RemoveAt(0);
                    first.Add("UNPAID_FIRST_NONTERMINAL_FALSE previous="+previous+"\n"+text+"\n"+TwentyTrafficFailureTrace.CounterGapCurrent(world,flow,storedOnly:true));
                }
            }
            catch(Exception error){if(errors.Count<4)errors.Add(error.GetType().Name+":"+error.Message);}
        }
        internal void Write()
        {
            foreach(var value in first)TestContext.WriteLine(value);
            foreach(var value in last.Values)TestContext.WriteLine("UNPAID_LAST_ORIGINAL_REFUSAL "+value);
            TestContext.WriteLine("UNPAID_ORIGINAL_REFUSAL_RECEIPT firstNonterminal="+first.Count+" entries="+last.Count+" errors="+string.Join(";",errors));
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            try{if(ReferenceEquals(hook.GetValue(flow),installed))hook.SetValue(flow,prior);else TestContext.WriteLine("UNPAID_ORIGINAL_REFUSAL cleanup=UNKNOWN_HOOK_OWNERSHIP");}
            catch(Exception error){TestContext.WriteLine("UNPAID_ORIGINAL_REFUSAL cleanup=UNKNOWN "+error.GetType().Name);}
        }
    }

    internal static class CafeLoopRuntimeProof
    {
        internal const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
        internal static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Flags).GetValue(owner);
        internal static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Flags).SetValue(owner,value);
        // Prepare only this actual unborn proposal; older customers keep normal progress and release.
        // 只等待当前 unborn id 的真实通行准备；不冻结旧顾客、全局账本或其他 Native requests。
        internal static IEnumerator PreparePositiveAdmission(CafeLoopController owner,CustomerFlowController customers,
            NavigationWorld world,int maxFrames)
        {
            const string premise="ADMISSION_PREPARATION_NOT_PROVEN: ";
            Assert.That(maxFrames,Is.InRange(1,6000),premise+"use the caller's existing bounded frame window");
            Assert.That(owner,Is.Not.Null);Assert.That(customers,Is.Not.Null);Assert.That(world,Is.Not.Null);
            var coordinator=Field<ServiceTrafficCoordinator>(owner,"traffic");
            Assert.That(coordinator,Is.Not.Null,premise+"the actual owner must already bind shared traffic");
            void CurrentDriver()
            {
                Assert.That(owner.isActiveAndEnabled && customers.isActiveAndEnabled && world.isActiveAndEnabled && world.LayoutAvailable,Is.True,premise+"live owner/body world required");
                Assert.That(customers.AutoSpawn,Is.False,premise+"manual canonical admission cannot race AutoSpawn; do not disable it here");
                Assert.That(Field<bool>(owner,"stepping"),Is.False,premise+"normal driver is frozen; no helper pump or gate repair is allowed");
                Assert.That(owner.CanProgress,Is.True,premise+"original lifecycle/readiness gate must allow normal frames");
                Assert.That(Field<CustomerFlowController>(owner,"customers"),Is.SameAs(customers));
                Assert.That(Field<NavigationWorld>(customers,"world"),Is.SameAs(world));
                Assert.That(Field<ServiceTrafficCoordinator>(owner,"traffic"),Is.SameAs(coordinator));
                Assert.That(Field<ServiceTrafficCoordinator>(customers,"traffic"),Is.SameAs(coordinator));
                Assert.That(Field<bool>(coordinator,"closed"),Is.False,premise+"original Core was closed");
            }
            CurrentDriver();
            var before=Field<long>(customers,"nextVisit");var anchor=Field<object>(customers,"pendingAdmissionAnchor");
            var fresh=anchor==null;
            if(fresh)
            {
                Assert.That(customers.TrySpawn(),Is.False,premise+"one real prime must leave canonical birth to the original positive assertion");
                anchor=Field<object>(customers,"pendingAdmissionAnchor");
            }
            Assert.That(anchor,Is.Not.Null,premise+"actual prime/pending source has no anchor; do not retry or manufacture one");
            var key=Field<TrafficIntentKey>(anchor,"Key");var id=key.ActorId;var firstFrame=Field<int>(anchor,"FirstFrame");
            var counter=Field<CounterQueueService>(anchor,"Owner");var capacity=Field<CapacityService>(anchor,"Capacity");
            var entries=Field<IDictionary>(coordinator,"entries");
            Assert.That(entries.Contains(key),Is.True,premise+"the actual submitted source must retain an Entry");
            var entry=entries[key];var previousQueries=Field<int>(entry,"Queries");var initialSequence=Field<long>(entry,"RequestSequence");
            if(fresh)Assert.That(initialSequence,Is.Zero,premise+"new original source must precede the normal ready-batch sequence");
            var positiveSequence=initialSequence;var sequenceTransitions=0;
            for(var waited=0;waited<=maxFrames;waited++)
            {
                CurrentDriver();
                Assert.That(Field<object>(customers,"pendingAdmissionAnchor"),Is.SameAs(anchor),premise+"pending producer replaced");
                Assert.That(Field<ServiceTrafficCoordinator>(anchor,"Coordinator"),Is.SameAs(coordinator));
                Assert.That(Field<TrafficIntentKey>(anchor,"Key"),Is.EqualTo(key));Assert.That(Field<int>(anchor,"FirstFrame"),Is.EqualTo(firstFrame));
                Assert.That(Field<long>(anchor,"Before"),Is.EqualTo(before));Assert.That(Field<long>(customers,"nextVisit"),Is.EqualTo(before));
                Assert.That(Field<string>(anchor,"Session"),Is.EqualTo(Field<string>(customers,"session")));
                Assert.That(Field<CounterQueueService>(anchor,"Owner"),Is.SameAs(counter));Assert.That(Field<CounterQueueService>(customers,"queue"),Is.SameAs(counter));
                Assert.That(Field<CapacityService>(anchor,"Capacity"),Is.SameAs(capacity));Assert.That(customers.Capacity,Is.SameAs(capacity));
                Assert.That(Field<NavigationWorld>(anchor,"World"),Is.SameAs(world));Assert.That(Field<bool>(anchor,"Submitted"),Is.True);
                Assert.That(key.Session,Is.EqualTo(Field<string>(coordinator,"session")));Assert.That(key.Epoch,Is.EqualTo(Field<long>(coordinator,"epoch")));
                Assert.That(key.LayoutRevision,Is.EqualTo(Field<int>(coordinator,"layoutRevision")));
                Assert.That(key.Session,Is.EqualTo(Field<string>(customers,"trafficSession")));Assert.That(key.Epoch,Is.EqualTo(Field<long>(customers,"trafficEpoch")));
                Assert.That(key.LayoutRevision,Is.EqualTo(Field<NavigationLayoutAdapter>(customers,"adapter").Revision));
                Assert.That(entries.Contains(key) && ReferenceEquals(entries[key],entry),Is.True,premise+"original Entry replaced/removed");
                Assert.That(Field<TrafficIntentKey>(entry,"Key"),Is.EqualTo(key));Assert.That(Field<int>(entry,"FirstFrame"),Is.EqualTo(firstFrame));
                var request=Field<TrafficRequest>(entry,"Request");var decision=Field<TrafficDecision>(entry,"Decision");
                Assert.That(request,Is.Not.Null,premise+"original request payload was retired");Assert.That(decision,Is.Not.Null);
                Assert.That(request.Intent,Is.EqualTo(key));Assert.That(request.RequestedFrame,Is.EqualTo(firstFrame));
                Assert.That(request.ResourceId,Is.EqualTo("counter/"+id));Assert.That(decision.Intent,Is.EqualTo(key));
                Assert.That(decision.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None),premise+decision.Kind+": "+decision.Reason);
                Assert.That(Field<bool>(entry,"Retired") || Field<bool>(entry,"Discarded"),Is.False,premise+"terminal source is not eligible for helper retry");
                Assert.That(Field<bool>(entry,"Parking"),Is.True);Assert.That(Field<bool>(entry,"Active"),Is.False);
                Assert.That(Field<long>(entry,"AcquiredSequence"),Is.Zero);Assert.That(decision.AcquiredSequence,Is.Zero);
                Assert.That(Field<object>(entry,"Leg"),Is.Null);Assert.That(Field<object>(entry,"JourneyPermit"),Is.Null);
                Assert.That(request.NativeStamp.RequestId,Is.Zero);Assert.That(request.NativeStamp.LegGeneration,Is.Zero);
                var queries=Field<int>(entry,"Queries");var sequence=Field<long>(entry,"RequestSequence");
                Assert.That(queries,Is.GreaterThanOrEqualTo(previousQueries),premise+"query accounting reset");previousQueries=queries;
                Assert.That(sequence,Is.GreaterThanOrEqualTo(0));
                if(sequence>0 && positiveSequence==0){positiveSequence=sequence;sequenceTransitions++;}
                Assert.That(sequence,Is.EqualTo(positiveSequence),premise+"first request sequence changed or reset");
                // All absence checks are scoped to the unborn id; no global count or Native nextId freeze.
                Assert.That(Field<IDictionary>(customers,"visits").Contains(id),Is.False);
                Assert.That(customers.Snapshot.Any(v=>v.VisitId==id) || customers.CafeSnapshot.Any(v=>v.VisitId==id),Is.False);
                Assert.That(world.RegisteredActors.Any(a=>a!=null && a.ActorId==id),Is.False);
                Assert.That(customers.GetComponentsInChildren<NavigationActor>(true).Any(a=>a.ActorId==id),Is.False,premise+"unborn source already has a staged actor");
                Assert.That(customers.CounterAssignments.Any(a=>a.VisitId==id) || customers.PickupAssignments.Any(a=>a.VisitId==id),Is.False);
                Assert.That(capacity.GetReservations().Any(r=>r.OwnerId==id),Is.False);
                var business=owner.Snapshot;
                Assert.That(business.Orders.Values.Any(o=>o.CustomerId==id) || business.PaidVisitIds.Contains(id) || business.Delivery.Any(d=>d.VisitId==id),Is.False);
                Assert.That(Field<IDictionary>(world.Service,"byActor").Contains(id),Is.False,premise+"unborn id already owns an actual Native request");
                var snapshots=new[]{Field<TrafficWorldSnapshot>(customers,"trafficSnapshot"),Field<TrafficWorldSnapshot>(coordinator,"snapshot")};
                foreach(var snapshot in snapshots)
                    if(snapshot!=null)Assert.That(snapshot.Bodies.Any(b=>b.Owner.ActorId==id) || snapshot.Parking.Any(p=>p.Owner.ActorId==id),Is.False);
                if(decision.Kind==TrafficDecisionKind.Granted)
                {
                    Assert.That(positiveSequence,Is.GreaterThan(0));Assert.That(decision.RequestSequence,Is.EqualTo(positiveSequence));
                    Assert.That(sequenceTransitions,Is.LessThanOrEqualTo(1));
                    TestContext.WriteLine("NATIVE_ADMISSION_PREP sameAnchor=True sameEntry=True fullKey="+key.Session+"/e"+key.Epoch+"/"+id+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context+
                        " firstFrame="+firstFrame+" waited="+waited+" initialSequence="+initialSequence+" firstPositiveSequence="+positiveSequence+
                        " sequenceTransitions="+sequenceTransitions+" queries="+queries+" acquired=0 unbornCanonicalAndNativeAbsent=True olderProgressAllowed=True");
                    yield break;
                }
                Assert.That(decision.Kind==TrafficDecisionKind.DeferredByBudget || decision.Kind==TrafficDecisionKind.WaitingForTraffic,Is.True,premise+decision.Kind+": "+decision.Reason);
                Assert.That(waited,Is.LessThan(maxFrames),premise+"normal-frame preparation exceeded the original caller bound");
                yield return null;
            }
        }

        internal static IEnumerator Twenty(Scene scene,CafeLoopController owner,CustomerFlowController customers,EmployeeFlowController employees,
            PickupCupDisplay cups,GameTimeService time,NavigationWorld world,NavigationLayoutAdapter adapter,GameSpeed speed,
            AdmissionFirstEndpointWitness admissionWitness=null)
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
            var unpaidOriginal=Environment.GetCommandLineArgs().Contains("-unpaidPreparationOriginalReceipt")?new UnpaidPreparationOriginalStageReceipt(world,customers):null;
            var trafficTrace=Environment.GetCommandLineArgs().Contains("-twentyFailureTrace")?new TwentyTrafficFailureTrace(world,customers,employees):null;
            var waitTrace=Environment.GetCommandLineArgs().Contains("-serviceTrafficWaitTrace")?new TrafficWaitFailureTrace(Field<ServiceTrafficCoordinator>(owner,"traffic")):null;
            var timeoutClock=Environment.GetCommandLineArgs().Contains("-twentyTimeoutSnapshot")?System.Diagnostics.Stopwatch.StartNew():null;
            var timeoutSnapshotSeconds=new[]{30,60,120,165};var timeoutSnapshotIndex=0;
            Assert.That(time.TrySetSpeed(speed),Is.True); customers.AutoSpawn=true;
            try
            {
                for(var frame=0;frame<50000 && (receipts.Count<20 || customers.Snapshot.Count>0 || employees.Snapshot.Any(e=>e.State!=EmployeeState.Idle));frame++)
                {
                    if(timeoutClock!=null && timeoutSnapshotIndex<timeoutSnapshotSeconds.Length && timeoutClock.Elapsed.TotalSeconds>=timeoutSnapshotSeconds[timeoutSnapshotIndex])
                    {
                        // At most four read-only snapshots before the original NUnit timeout; no planner/refresh calls.
                        // 默认关闭；只复制当前原始facts，不增加Native查询或修改原50000帧/180秒界限。
                        timeoutSnapshotIndex++;
                        TestContext.WriteLine("TWENTY_TIMEOUT_SNAPSHOT index="+timeoutSnapshotIndex+" wall="+timeoutClock.Elapsed.TotalSeconds.ToString("F3")+
                            " frame="+Time.frameCount+" loopFrame="+frame+" speed="+speed+" admitted="+seen.Count+" actualExit="+receipts.Count+
                            " paid="+owner.Snapshot.PaidVisitIds.Count+" completed="+completed.Count+" status="+customers.Status+" failure="+owner.Snapshot.Failure);
                        TestContext.WriteLine(TwentyTrafficFailureTrace.Current(world,customers,employees));
                    }
                    if(frame%30==0)trafficTrace?.Sample();
                    Assert.That(owner.Snapshot.Failure,Is.Empty,owner.Snapshot.Status+" / "+customers.Status);
                    if(customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked))
                    {
                        TestContext.WriteLine(TwentyTrafficFailureTrace.Current(world,customers,employees));
                        var visits=Field<System.Collections.IDictionary>(customers,"visits");
                        foreach(var blocked in customers.Snapshot.Where(v=>v.State==CustomerVisitState.Blocked))
                        {
                            var entry=visits[blocked.VisitId];
                            string Read(string name)=>entry.GetType().GetField(name).GetValue(entry)?.ToString();
                            TestContext.WriteLine("BLOCKED_VISIT "+blocked.VisitId+" failure="+Read("Failure")+
                                " resume="+Read("ResumeState")+" target="+Read("CurrentSegmentTarget")+" route="+Read("Route"));
                        }
                    }
                    if(unpaidOriginal!=null && customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked))
                    {unpaidOriginal.Write();TestContext.WriteLine(TwentyTrafficFailureTrace.CounterGapCurrent(world,customers,storedOnly:true));}
                    Assert.That(customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked),Is.False,customers.Status);
                    var actors=world.RegisteredActors.ToArray(); var before=actors.Select(a=>a.transform.position).ToArray();
                    yield return null;
                    if(admissionWitness!=null && admissionWitness.Verify())yield break;
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
                admissionWitness?.Exhausted();
                if(seen.Count!=20 || receipts.Count!=20)
                {
                    if(unpaidOriginal!=null){unpaidOriginal.Write();TestContext.WriteLine(TwentyTrafficFailureTrace.CounterGapCurrent(world,customers,storedOnly:true));}
                    TestContext.WriteLine("TWENTY_INCOMPLETE speed="+speed+" admitted="+seen.Count+" actualExit="+receipts.Count+" failure="+owner.Snapshot.Failure+" status="+customers.Status);
                    TestContext.WriteLine(TwentyTrafficFailureTrace.Current(world,customers,employees));
                    if(TestContext.CurrentContext.Test.Name=="CompletesTwentyRealOrdersAndDrainsAllCapacityAtOneX")
                        TestContext.WriteLine(TwentyTrafficFailureTrace.CounterGapCurrent(world,customers));
                    if(TestContext.CurrentContext.Test.Name=="CompletesTwentyRealOrdersAndDrainsAllCapacityAtTwoX")
                        TestContext.WriteLine(TwentyTrafficFailureTrace.CounterGapCurrent(world,customers,storedOnly:true));
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
            {
                try { unpaidOriginal?.Dispose();trafficTrace?.Finish();waitTrace?.Dispose(); }
                finally { tracking=false; customers.AutoSpawn=false; employees.IntegratedTaskEvent-=onTask; owner.SnapshotChanged-=onSnapshot; foreach(var observer in observers) if(observer!=null) observer.Receipt=null; }
            }
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
            var originalClock=CafeLoopRuntimeProof.Field<CustomerSpawnClock>(customers,"clock");
            var originalAuto=customers.AutoSpawn;var draws=0;
            var controlledClock=new CustomerSpawnClock(()=>{draws++;return .5f;}); // 0.5 of 0-10 = 5 game seconds.
            CafeLoopRuntimeProof.Set(customers,"clock",controlledClock);
            try
            {
                var capacity=customers.Capacity; var tokens=new List<CapacityToken>();
                while(capacity.GetCapacities().Single(c=>c.Kind==kind).Available>0)
                { var reserved=capacity.TryReserve("test-capacity-lock-"+tokens.Count,new[]{kind}); Assert.That(reserved.Succeeded,Is.True); tokens.Add(reserved.Reservations.Single().Token); }
                tools.ResumeArrivals(); owner.Step(.1f); owner.Step(100);
                Assert.That(customers.Snapshot,Is.Empty); Assert.That(customers.RemainingSeconds,Is.Zero); Assert.That(owner.Snapshot.Orders,Is.Empty);
                Assert.That(draws,Is.Zero,"Full capacity must not draw or accumulate an admission interval");
                for(var i=0;i<tokens.Count;i++) Assert.That(capacity.Release(tokens[i],"test-capacity-lock-"+i).Succeeded,Is.True);
                owner.Step(.01f); Assert.That(customers.RemainingSeconds,Is.EqualTo(4.99f).Within(.00001f)); Assert.That(customers.Snapshot,Is.Empty);
                Assert.That(draws,Is.EqualTo(1),"Capacity release draws one complete current 0-10 interval");
                var remaining=customers.RemainingSeconds; var delta=Math.Min(.1f,remaining/2); owner.Step(delta);
                Assert.That(customers.RemainingSeconds,Is.EqualTo(remaining-delta).Within(.00001f),"Customer consumes the supplied delta exactly once");
                owner.Step(remaining+20);
                Assert.That(draws,Is.EqualTo(1),"Deferred traffic does not redraw the original due opportunity");
                Assert.That(customers.Snapshot,Is.Empty,"The original due opportunity awaits normal shared-traffic preparation");
                var due=CafeLoopRuntimeProof.Field<object>(customers,"pendingAdmissionAnchor");
                var before=CafeLoopRuntimeProof.Field<long>(customers,"nextVisit");
                Assert.That(due,Is.Not.Null,"The large tick must submit one real due admission");
                // Keep AutoSpawn and the normal owner driver active: no manual TrySpawn or Core pump.
                // 同一到期机会等正常driver完成；不手动出生、不重抽签、不增加等待预算。
                yield return Until(()=>
                {
                    if(customers.Snapshot.Count!=0)return true;
                    Assert.That(customers.AutoSpawn,Is.True);
                    Assert.That(CafeLoopRuntimeProof.Field<object>(customers,"pendingAdmissionAnchor"),Is.SameAs(due));
                    Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.EqualTo(before));
                    Assert.That(CafeLoopRuntimeProof.Field<CustomerSpawnClock>(customers,"clock"),Is.SameAs(controlledClock));
                    Assert.That(customers.RemainingSeconds,Is.Zero);Assert.That(draws,Is.EqualTo(1));
                    return false;
                });
                tools.HoldArrivals(); Assert.That(customers.Snapshot.Count,Is.EqualTo(1),"No accumulated admission backlog in one large tick");
                Assert.That(draws,Is.EqualTo(2),"Only actual admission starts exactly one next interval");
                Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.EqualTo(before+1));
                Assert.That(customers.Capacity,Is.SameAs(capacity)); yield return Until(()=>customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                Assert.That(capacity.GetCapacities().All(c=>c.Used==0),Is.True);
            }
            finally
            {
                customers.AutoSpawn=originalAuto;
                CafeLoopRuntimeProof.Set(customers,"clock",originalClock);
            }
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

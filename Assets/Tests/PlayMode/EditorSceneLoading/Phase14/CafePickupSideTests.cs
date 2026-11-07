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
        Action parkingEndpointWitnessCleanup;
        Action parkingEndpointStoredEnd;
        [UnitySetUp] public IEnumerator Setup()
        {
            deliveryStarts.Clear(); capture=Time.captureDeltaTime; scale=Time.timeScale; Time.captureDeltaTime=1f/60;
            scene=EditorSceneManager.LoadSceneInPlayMode(TestContext.CurrentContext.Test.Name.StartsWith("SingleSide")?"Assets/Scenes/Validation/Phase14SharedPickup.unity":"Assets/Scenes/Validation/Phase14CafeLoop.unity",new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
            owner=Find<CafeLoopController>(); customers=Find<CustomerFlowController>(); employees=Find<EmployeeFlowController>(); tools=Find<CafeLoopValidationController>();
            world=Find<NavigationWorld>(); adapter=Find<NavigationLayoutAdapter>(); time=Find<GameTimeService>(); inputs=Phase8SceneInputTestCleanup.CaptureAssets(scene);
            employees.IntegratedTaskEvent+=e=>{if(e.Kind!=CafeTaskEventKind.Progress) TestContext.WriteLine("SIDE_EVENT "+e.Kind+" role="+e.Role+" failure="+e.Failure+" stage="+e.Checkpoint.Stage+
                " frame="+Time.frameCount+" sceneTime="+Time.timeSinceLevelLoadAsDouble.ToString("R")+" visit="+e.Checkpoint.VisitId+
                " remaining="+e.Checkpoint.RemainingWorkSeconds.ToString("R")+" handle="+e.HandleId);};
            tools.HoldArrivals(); tools.HoldCollection(); yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
        }
        T Find<T>() where T:Component=>scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
        IEnumerator Until(Func<bool> done,bool employeeMayBlock=false)
        {
            for(var i=0;i<6000 && !done();i++)
            {
                var customersUnblocked=customers.Snapshot.All(v=>v.State!=CustomerVisitState.Blocked);
                var blockedFacts="";
                if(!customersUnblocked)
                {
                    // Read only the current failed Visit; Status may already have been overwritten.
                    // 仅原断言已为false时读保存的失败，不重放guard、不查询Native或Core。
                    try
                    {
                        var facts=new System.Collections.Generic.List<string>();
                        foreach(DictionaryEntry row in (IDictionary)MovementRead(customers,"visits"))
                        {
                            var visit=row.Value;var state=(CustomerVisitState)MovementRead(visit,"State");
                            if(state!=CustomerVisitState.Blocked)continue;
                            var journey=MovementRead(visit,"TrafficJourney");
                            var key=journey is TrafficIntentKey k?k.Session+"/e"+k.Epoch+"/"+k.ActorId+"/"+k.IntentId+"/g"+k.Generation+"/r"+k.LayoutRevision+"/"+k.Context:"null";
                            facts.Add("visit="+row.Key+" State="+state+" ResumeState="+MovementRead(visit,"ResumeState")+
                                " Failure="+MovementRead(visit,"Failure")+" TrafficJourney="+key+" RequestId="+MovementRead(visit,"RequestId"));
                        }
                        blockedFacts=" BLOCKED_VISIT ["+string.Join(";",facts)+"]";
                    }
                    catch(Exception error){blockedFacts=" BLOCKED_VISIT UNKNOWN "+error.GetType().Name+": "+error.Message;}
                }
                Assert.That(customersUnblocked,Is.True,owner.ViewSnapshot.Reception+" / "+owner.Snapshot.Failure+" / "+customers.Status+blockedFacts);
                if(!employeeMayBlock && employees.Snapshot.Any(e=>e.State==EmployeeState.Blocked))
                {
                    ObserveInitialTrafficProof();
                    Assert.Fail("Unexpected employee Blocked: "+string.Join(";",employees.Snapshot.Select(e=>e.Role+"/"+e.State+"/"+e.Failure))+" bodies="+
                        string.Join(";",world.RegisteredActors.Select(a=>a.ActorId+"@"+a.transform.position.ToString("F6")))+" leases="+
                        string.Join(";",CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue")?.ServiceLeases.Select(l=>l.Key+"@"+l.Value.ToString("F6"))??Array.Empty<string>()));
                }
                ObserveDeliveryStart(); yield return null;
            }
            var completed=done();if(!completed)ObserveOldTestFailure();
            Assert.That(completed,Is.True,owner.ViewSnapshot.Reception+" / "+owner.Snapshot.Failure);
        }
        private void ObserveOldTestFailure()
        {
            if(!Environment.GetCommandLineArgs().Contains("-p15OldTestTrace"))return;
            var coordinator=MovementRead(customers,"traffic") as ServiceTrafficCoordinator;
            var entries=coordinator==null?null:MovementRead(coordinator,"entries") as IDictionary;
            if(entries==null)return;
            foreach(DictionaryEntry row in entries)
            {
                var request=MovementRead(row.Value,"Request") as TrafficRequest;if(request==null)continue;
                var decision=MovementRead(row.Value,"Decision") as TrafficDecision;
                TestContext.WriteLine("OLD_FAILURE_ENTRY actor="+request.Intent.ActorId+" phase="+request.Intent.IntentId+" status="+decision?.Kind+" reason="+decision?.Reason+
                    " jobIndex="+MovementRead(row.Value,"JobIndex")+" queries="+MovementRead(row.Value,"Queries")+" candidate="+MovementRead(row.Value,"CurrentIndex")+
                    " start="+request.Start.ToString("F5")+" target="+request.Target.ToString("F5")+
                    " requirements="+string.Join(";",request.Requirements.Select(q=>q.ResourceId+":"+q.Start.ToString("F5")+"->"+q.Goal.ToString("F5")+" clearance="+q.Clearance+" selected="+q.SelectedJourney+" obstacles="+string.Join(",",q.Obstacles))));
            }
            TestContext.WriteLine("OLD_FAILURE_ACTORS "+string.Join(";",world.RegisteredActors.Select(a=>a.ActorId+"@"+a.transform.position.ToString("F5"))));
            TestContext.WriteLine("OLD_FAILURE_QUEUE counter="+string.Join(";",customers.CounterAssignments.Select(a=>a.VisitId+"@"+a.Position.ToString("F5")))+
                " pickup="+string.Join(";",customers.PickupAssignments.Select(a=>a.VisitId+"@"+a.Position.ToString("F5")))+
                " candidateSlots="+string.Join(";",(Vector3[])MovementRead(customers,"pickupWaitSlots")));
        }
        // Default off: inspect already-retained facts immediately before the original assertion.
        // 只读已存证明，不查询 Native、不刷新 snapshot，也不改变等待窗口。
        void ObserveInitialTrafficProof()
        {
            if(!Environment.GetCommandLineArgs().Contains("-trafficInitialProofSnapshot"))return;
            try
            {
                var coordinator=MovementRead(customers,"traffic") as ServiceTrafficCoordinator;
                var entries=coordinator==null?null:MovementRead(coordinator,"entries") as IDictionary;
                var snapshot=coordinator==null?null:MovementRead(coordinator,"snapshot") as TrafficWorldSnapshot;
                foreach(var row in ((IEnumerable)MovementRead(employees,"employees")).Cast<object>())
                {
                    var id=(string)MovementRead(row,"Id");var actor=MovementRead(row,"Actor") as NavigationActor;
                    var binding=MovementRead(row,"Binding") as EmployeeStationBinding;
                    var body=snapshot?.Bodies.FirstOrDefault(b=>b.Owner.ActorId==id);
                    TestContext.WriteLine("TRAFFIC_INITIAL_PROOF actor="+id+" pose="+(actor==null?"UNKNOWN":actor.transform.position.ToString("F9"))+
                        " workTarget="+(binding==null?"UNKNOWN":binding.WorkTarget.Position.ToString("F9"))+
                        " arrivalTolerance="+(actor==null?"UNKNOWN":actor.Settings.ArrivalDistance.ToString("R"))+
                        " bodyKey="+(body==null?"UNKNOWN":body.Owner.ToString())+" bodyLifeMatches="+(body!=null && ReferenceEquals(body.ActorLife,actor)));
                    var found=false;
                    if(entries!=null)foreach(DictionaryEntry pair in entries)
                    {
                        var key=(TrafficIntentKey)pair.Key;if(key.ActorId!=id)continue;
                        var source=MovementRead(pair.Value,"JourneyPermit") as TrafficDecision;
                        var request=MovementRead(pair.Value,"JourneyRequest") as TrafficRequest;
                        var sourceBody=request?.Snapshot.Bodies.FirstOrDefault(b=>b.Owner.ActorId==id);
                        TestContext.WriteLine("TRAFFIC_INITIAL_PROOF key="+key+" retired="+MovementRead(pair.Value,"Retired")+
                            " source="+(source==null?"UNKNOWN":source.Kind+"/acquired="+source.AcquiredSequence)+
                            " sourceStart="+(request==null?"UNKNOWN":request.Start.ToString("F9"))+
                            " sourceTarget="+(request==null?"UNKNOWN":request.Target.ToString("F9"))+
                            " sourceLifeMatches="+(sourceBody!=null && ReferenceEquals(sourceBody.ActorLife,actor)));
                        if(source==null)continue;found=true;
                        foreach(var proof in source.ProofRoutes)
                            TestContext.WriteLine("TRAFFIC_INITIAL_PROOF resource="+proof.ResourceId+
                                " waypoints="+string.Join(";",proof.Waypoints.Select(p=>p.ToString("F9")))+
                                " expanded="+string.Join(";",proof.ExpandedPath.Select(p=>p.ToString("F9")))+
                                " legEnds="+string.Join(",",proof.LegEnds));
                    }
                    if(!found)TestContext.WriteLine("TRAFFIC_INITIAL_PROOF actor="+id+" retainedSource=UNKNOWN");
                }
            }
            catch(Exception error){TestContext.WriteLine("TRAFFIC_INITIAL_PROOF UNKNOWN "+error.GetType().Name+": "+error.Message);}
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
        // Default-off original WaitOrFail witness; never call a planner, hint, preview or progress API.
        // callback 内捕获所有观察异常，确保原 Fail 继续；finally 恢复 hook 后才输出和断言。
        Action BeginParkingEndpointFailureWitness(bool collection)
        {
            if(!Environment.GetCommandLineArgs().Contains("-trafficParkingEndpointFailureWitness"))return null;
            TestContext.WriteLine("PARKING_ENDPOINT_WITNESS_GATE enabled=True caller="+TestContext.CurrentContext.Test.Name+
                " actualContext="+TestContext.CurrentContext.Test.FullName); // Context is evidence only; the two explicit callers define scope.
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            object Read(object value,string name)=>value==null?null:value.GetType().GetField(name,flags)?.GetValue(value)??value.GetType().GetProperty(name,flags)?.GetValue(value);
            string Ref(object value)=>value==null?"NULL":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value).ToString();
            string Key(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
            string Fields(object value,params string[] names)=>value==null?"UNKNOWN(no-object)":string.Join(";",names.Select(name=>name+"="+(Read(value,name)??"NULL_OR_UNAVAILABLE")));
            string Points(object value)=>value is System.Collections.Generic.IEnumerable<Vector3> points?string.Join(";",points.Select(v=>v.ToString("F9"))):"UNKNOWN";
            string Items(object value)=>value is IEnumerable items?string.Join(",",items.Cast<object>()):"UNKNOWN";
            string Owners(object value)=>value is System.Collections.Generic.IEnumerable<TrafficIntentKey> keys?string.Join(";",keys.Select(Key)):"UNKNOWN";
            string Resources(object value)=>value is IDictionary pairs?string.Join(";",pairs.Cast<DictionaryEntry>().Select(pair=>Key((TrafficIntentKey)pair.Key)+"/["+Items(pair.Value)+"]")):"UNKNOWN";
            string Edges(object value)=>value is System.Collections.Generic.IEnumerable<TrafficBlocker> edges?string.Join(";",edges.Select(edge=>Key(edge.Owner)+"/"+edge.ResourceId+"/"+edge.Proof)):"UNKNOWN";
            var core=(ServiceTrafficCoordinator)Read(customers,"traffic");
            var hook=typeof(ServiceTrafficCoordinator).GetField("WaitFailureObserver",flags);var previous=hook.GetValue(core);
            var records=new System.Collections.Generic.List<string>();var errors=new System.Collections.Generic.List<string>();
            var observed=new System.Collections.Generic.HashSet<string>();
            void Snapshot(string tag,TrafficWorldSnapshot snapshot)
            {
                if(snapshot==null){records.Add(tag+"=UNKNOWN");return;}
                records.Add(tag+" ref="+Ref(snapshot)+" versions="+snapshot.Version+"/"+snapshot.BodyVersion+
                    " bodies=["+string.Join(";",snapshot.Bodies.Select(b=>Key(b.Owner)+"/life="+Ref(b.ActorLife)+"/pose="+b.Position.ToString("F9")+
                        "/r="+b.Radius+"/s="+b.SafetyMargin+"/enabled="+b.Enabled+"/active="+b.ActorActive+"/progress="+b.IndependentlyProgressing+"/remaining="+b.RemainingSeconds))+"]"+
                    " parking=["+string.Join(";",snapshot.Parking.Select(c=>Key(c.Owner)+"/owner="+Ref(c.CanonicalOwner)+"/version="+c.OwnerVersion+
                        "/resource="+c.ResourceId+"/pose="+c.Position.ToString("F9")+"/r="+c.Radius+"/s="+c.SafetyMargin+
                        "/business="+Ref(c.BusinessOwner)+"/release="+Ref(c.ReleaseObligation)))+"]");
            }
            void Observe(string original)
            {
                try
                {
                    if(errors.Count!=0 || observed.Count>=(collection?2:1))return;
                    var entries=(IDictionary)Read(core,"entries");
                    foreach(DictionaryEntry pair in entries)
                    {
                        var entry=pair.Value;var request=Read(entry,"Request") as TrafficRequest;if(request==null)continue;
                        var key=request.Intent;var leg=Read(entry,"Leg");var proof=Read(leg,"Proof") as TrafficRouteProof;
                        var role=!collection && key.IntentId=="employee-pickup-journey" && leg==null?"delivery":
                            collection && key.IntentId=="pickup-service" && leg==null?"collection":
                            collection && key.IntentId=="employee-pickup-journey" && proof?.ResourceId=="selected-home"?"return":null;
                        if(role==null || observed.Contains(role) || !original.Contains(" intent="+Key(key)+" previous=") ||
                            !original.Contains(" requestSequence="+Read(entry,"RequestSequence")+" queries="))continue;
                        observed.Add(role); // Cap before copying; an observation error cannot cause repeated captures.
                        records.Add("PARKING_ENDPOINT_ORIGINAL status=OBSERVED role="+role+" cap=1 frame="+Time.frameCount+" key="+Key(key)+
                            " coreStillCurrent="+ReferenceEquals(Read(customers,"traffic"),core)+" entry="+Ref(entry)+" originalNativeResult=UNKNOWN "+original);
                        records.Add("ENDPOINT_REQUEST frame="+request.RequestedFrame+" start="+request.Start.ToString("F9")+" target="+request.Target.ToString("F9")+
                            " r="+request.Radius+" s="+request.SafetyMargin+" resource="+request.ResourceId+" native="+request.NativeStamp.RequestId+" primary="+request.PlanPrimaryRoute+
                            " selected=["+Points(request.SelectedJourney)+"] requirements=["+string.Join(";",request.Requirements.Select(r=>r.ResourceId+"/"+r.Start.ToString("F9")+"->"+r.Goal.ToString("F9")+
                                "/clearance="+r.Clearance+"/selected="+r.SelectedJourney+"/avoidParking="+r.AvoidProposedParking+"/obstacles="+Points(r.Obstacles)))+"]");
                        records.Add("ENDPOINT_ENTRY "+Fields(entry,"FirstFrame","RequestSequence","AcquiredSequence","Queries","CandidateCount","CurrentIndex","FiniteLimit","WaitRemaining","JobIndex","Parking","Queued","Prepared","Active","Retired","EverCanonicalAttempt")+
                            " attempted=["+Items(Read(entry,"AttemptedIndices"))+"] frozen=["+Owners(Read(entry,"FrozenOwners"))+"] resources=["+Resources(Read(entry,"FrozenResources"))+"]"+
                            " edges=["+Edges(Read(entry,"WaitingBlockers"))+"] currentPath=["+Points(Read(entry,"CurrentPath"))+"] journey=["+Points(Read(entry,"Journey"))+"]"+
                            " leg="+Ref(leg)+" "+Fields(leg,"Index","Generation","NeedsQuery","Committed","Bound","Completed","Suspended")+" proof="+proof?.ResourceId);
                        var jobs=Read(entry,"Jobs") as IList;
                        records.Add("ENDPOINT_JOBS count="+jobs?.Count+" data=["+(jobs==null?"UNKNOWN":string.Join(";",jobs.Cast<object>().Select(job=>
                            Fields(job,"start","goal")+"/nodes="+(Read(job,"nodes") as IList)?.Count+"/cachedPaths="+(Read(job,"paths") as IDictionary)?.Count)))+"]");
                        Snapshot("ENDPOINT_REQUEST_SNAPSHOT",request.Snapshot);Snapshot("ENDPOINT_CURRENT_SNAPSHOT",Read(core,"snapshot") as TrafficWorldSnapshot);
                        var cursors=(IDictionary)Read(customers,"trafficCandidates");var cursor=cursors.Contains(key)?cursors[key]:null;
                        records.Add("ENDPOINT_CURSOR "+Fields(cursor,"Key","Role","Origin","CurrentIndex","Count")+" source="+Ref(Read(cursor,"Source"))+
                            " attempted=["+Items(Read(cursor,"AttemptedIndices"))+"] staticInvalid=["+Items(Read(cursor,"StaticInvalidIndices"))+"] sides=["+
                            (Read(cursor,"Sides") is IEnumerable sides?string.Join(";",sides.Cast<NavigationTarget>().Select(side=>side.Position.ToString("F9"))):"UNKNOWN")+"]");
                        foreach(DictionaryEntry right in entries)
                        {
                            var other=right.Value;var otherRequest=Read(other,"Request") as TrafficRequest;
                            if(otherRequest==null || (bool)Read(other,"Retired"))continue;
                            var journey=Read(other,"Journey") as Vector3[];
                            if(!(bool)Read(other,"Active") && !(bool)Read(other,"Prepared") && (journey==null || journey.Length<2))continue;
                            records.Add("ENDPOINT_RIGHT key="+Key(otherRequest.Intent)+" target="+otherRequest.Target.ToString("F9")+" r="+otherRequest.Radius+" s="+otherRequest.SafetyMargin+" "+
                                Fields(other,"Parking","Prepared","Active","AcquiredSequence")+" current=["+Points(Read(other,"CurrentPath"))+"] retained=["+Points(journey)+"]");
                        }
                        var queue=Read(customers,"pickupQueue") as PickupQueueService;
                        records.Add("ENDPOINT_QUEUE version="+queue?.Version+" rows=["+(queue==null?"UNKNOWN":string.Join(";",queue.Snapshot.Select(row=>
                            row.VisitId+"/g"+row.Generation+"/slot"+row.SlotIndex+"/arrived="+row.Arrived+"/"+row.Position.ToString("F9"))))+"] leases=["+
                            (queue==null?"UNKNOWN":string.Join(";",queue.ServiceLeases.Select(lease=>lease.Key+"/"+lease.Value.ToString("F9"))))+"]");
                        break;
                    }
                }
                catch(Exception error){errors.Add(error.ToString());} // Never let this subscriber interrupt Core's original Fail.
            }
            // Optional end-state capture for these two explicit cases; no gate/policy getter is replayed.
            // 在原scene仍存活时复制已有字段；这是end事实，不冒充原Fail分支证据。
            var endCaptured=false;
            if(Environment.GetCommandLineArgs().Contains("-trafficCollectionStoredEnd"))parkingEndpointStoredEnd=()=>
            {
                try
                {
                    if(endCaptured)return;endCaptured=true;
                    records.Add("COLLECTION_STORED_END status=OBSERVED cap=1 frame="+Time.frameCount+
                        " timing=before-scene-cleanup originalFailureStage=UNKNOWN NOT_ORIGINAL_BRANCH coreSame="+ReferenceEquals(Read(customers,"traffic"),core));
                    records.Add("COLLECTION_END_OWNER "+Fields(owner,"held","destroying","comboReady","layoutPending","layoutFailed","trafficEpoch","trafficRevision","leaseActor","leasePosition","leaseArrived","<HoldCollection>k__BackingField")+
                        " CFC="+Fields(customers,"integratedMode","integratedHeld","integratedAdmissionAllowed","suspended","disposed","revision","selectedRegisterId")+
                        " time="+Read(time,"<CurrentSpeed>k__BackingField")+" resumeBlocks="+(Read(time,"resumeBlocks") as IList)?.Count+
                        " readiness="+Ref(Read(adapter,"<CurrentReadiness>k__BackingField"))+" publishedRevision="+Read(adapter,"<PublishedRevision>k__BackingField")+
                        " predicatesNOT_REEVALUATED=CanProgress,CanIntegratedProgress,TrafficActorReady,CanCollect,CanCollectPickup");
                    var service=Read(owner,"service");var storedOrders=Read(Read(service,"orders"),"orders") as IDictionary;
                    records.Add("COLLECTION_END_SERVICE "+Fields(service,"disposed","ticking","permit","epoch","failure","projectionDirty")+
                        " paidFIFO=["+Items(Read(service,"paidFifo"))+"] collected=["+Items(Read(service,"collected"))+"] blockedCollections=["+Items(Read(service,"blockedCollections"))+"] pickupMoves=["+Items(Read(service,"pickupMoves"))+"] exitMoves=["+Items(Read(service,"exitMoves"))+"]"+
                        " barista="+Fields(Read(service,"barista"),"Accepted","Blocked","Observed","SubmissionPending")+
                        " orders=["+(storedOrders==null?"UNKNOWN":string.Join(";",storedOrders.Keys.Cast<object>().Select(key=>Fields(storedOrders[key],"OrderId","CustomerId","State","ClaimantId"))))+"]");
                    var visits=(IDictionary)Read(customers,"visits");var keys=(IDictionary)Read(customers,"trafficKeys");
                    var cursors=(IDictionary)Read(customers,"trafficCandidates");var entries=(IDictionary)Read(core,"entries");
                    foreach(DictionaryEntry pair in visits)
                    {
                        var visit=pair.Value;var id=(string)Read(visit,"Id");
                        records.Add("COLLECTION_END_VISIT "+Fields(visit,"Id","State","ResumeState","PickupAssigned","PickupArrived","PickupDeparted","ServiceArrived","HasCollected","DeliveryYieldPhase","TrafficPhaseSequence","Generation","RequestId","PendingPickupSegment","TrafficMovePending","Failure")+
                            " assignment="+Fields(Read(visit,"Assignment"),"VisitId","Generation","SlotIndex","Arrived","Position")+" owner="+Ref(Read(visit,"PickupOwner"))+
                            " actor="+Ref(Read(visit,"Actor"))+" move="+Ref(Read(visit,"TrafficMove"))+" selection="+Ref(Read(visit,"TrafficSelection")));
                        var serviceKeys=keys.Values.Cast<TrafficIntentKey>().Where(key=>key.ActorId==id && key.IntentId=="pickup-service").ToArray();
                        records.Add("COLLECTION_END_KEYS visit="+id+" count="+serviceKeys.Length+" keys=["+string.Join(";",serviceKeys.Select(Key))+"]");
                        foreach(var key in serviceKeys)
                        {
                            var cursor=cursors.Contains(key)?cursors[key]:null;var entry=entries.Contains(key)?entries[key]:null;
                            var request=Read(entry,"Request") as TrafficRequest;var decision=Read(entry,"Decision") as TrafficDecision;
                            records.Add("COLLECTION_END_SOURCE key="+Key(key)+" cursor="+Ref(cursor)+" entry="+Ref(entry)+" "+
                                Fields(cursor,"Role","CurrentIndex","Count")+" attempted=["+Items(Read(cursor,"AttemptedIndices"))+"] staticInvalid=["+Items(Read(cursor,"StaticInvalidIndices"))+"]"+
                                " decision="+Fields(decision,"Kind","FailureDisposition","Reason","RequestSequence","AcquiredSequence")+" "+
                                Fields(entry,"FirstFrame","CandidateCount","CurrentIndex","Queries","WaitRemaining","Queued","Prepared","Active","Retired","JobIndex")+
                                " jobs="+(Read(entry,"Jobs") as IList)?.Count+" requestFrame="+request?.RequestedFrame+" target="+request?.Target.ToString("F9")+
                                " start="+request?.Start.ToString("F9")+" versions="+request?.Snapshot?.Version+"/"+request?.Snapshot?.BodyVersion);
                        }
                    }
                    var queue=Read(customers,"pickupQueue") as PickupQueueService;
                    records.Add("COLLECTION_END_QUEUE version="+queue?.Version+" rows=["+(queue==null?"UNKNOWN":string.Join(";",queue.Snapshot.Select(row=>
                        row.VisitId+"/g"+row.Generation+"/slot"+row.SlotIndex+"/arrived="+row.Arrived+"/"+row.Position.ToString("F9"))))+"] leases=["+
                        (queue==null?"UNKNOWN":string.Join(";",queue.ServiceLeases.Select(lease=>lease.Key+"/"+lease.Value.ToString("F9"))))+"]");
                    Snapshot("COLLECTION_END_CURRENT_WORLD",Read(core,"snapshot") as TrafficWorldSnapshot);
                }
                catch(Exception error){errors.Add("COLLECTION_STORED_END UNKNOWN: "+error);}
            };
            Action<string> observer=Observe;hook.SetValue(core,observer);
            var finished=false;
            parkingEndpointWitnessCleanup=()=>
            {
                if(finished)return;finished=true;
                parkingEndpointStoredEnd?.Invoke();parkingEndpointStoredEnd=null;parkingEndpointWitnessCleanup=null;
                if(ReferenceEquals(hook.GetValue(core),observer))hook.SetValue(core,previous);
                else errors.Add("Endpoint witness hook changed externally; previous hook not restored over its replacement");
                foreach(var role in collection?new[]{"collection","return"}:new[]{"delivery"})
                    TestContext.WriteLine("PARKING_ENDPOINT_WITNESS role="+role+" status="+(observed.Contains(role)?"OBSERVED":"NOT_OBSERVED")+" cap=1 errors="+errors.Count);
                foreach(var record in records)TestContext.WriteLine(record);
                Assert.That(errors,Is.Empty,"Read-only endpoint witness UNKNOWN: observer errors, not product evidence");
            };
            return parkingEndpointWitnessCleanup;
        }
        [UnityTest] public IEnumerator BlockedPreferredSideUsesAlternativeWaitHeadAndCollectsBesideOccupiedDeliverySide()
        {
            var endEndpointWitness=BeginParkingEndpointFailureWitness(true);
            try
            {
            var oldCustomer=CafeLoopRuntimeProof.Field<Vector3>(customers,"pickupCustomer");
            var obstruction=Blocker(oldCustomer,"side-original-customer-blocker");
            yield return AwaitTrafficAdmission(); var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==id).CounterArrived);
            var reserved=customers.TryReservePickup(id,out var reason);
            // A positive batch result may be consumed by the normal Cafe Loop before this coroutine polls.
            // 等待真实预留落账；不绕过ready batch，也不把Deferred当成成功。
            yield return Until(()=>reserved || (reserved=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue")?.TryGetReservation(id,out _)==true));
            Assert.That(reserved,Is.True,"A body on the default side must not prevent safe alternative waiting geometry: "+reason);
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
            finally {endEndpointWitness?.Invoke();}
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
            var endEndpointWitness=BeginParkingEndpointFailureWitness(true);
            try
            {
            var pickup=Find<CafeLayoutRuntime>().FunctionalSurfaceLayout.PickUpPoints.Single(); var sides=customers.GetPickupServiceTargets(pickup.InstanceId);
            Assert.That(sides.Count,Is.EqualTo(2)); var south=sides.Single(t=>t.Facing.HasValue && Vector3.Dot(t.Facing.Value,Vector3.forward)>.9f);
            var block=Blocker(south.Position,"two-side-south-blocker"); yield return null; yield return Until(()=>customers.Snapshot.Count>0 || customers.TrySpawn());
            var id=customers.Snapshot.Single().VisitId; yield return Until(()=>owner.Snapshot.Orders.Count==1 && owner.Snapshot.Orders.Values.Single().State==OrderState.ReadyForPickup);
            var wait=customers.PickupAssignments.Single(); Assert.That(sides.All(t=>Vector3.Distance(t.Position,wait.Position)>.99f),Is.True,"Preparing waiter cannot preoccupy the sole remaining delivery side");
            tools.ResumeCollection(); yield return Until(()=>owner.Snapshot.Orders.Values.Single().State==OrderState.Completed);
            world.Unregister(block); block.gameObject.SetActive(false); UnityEngine.Object.Destroy(block.gameObject);
            yield return Until(()=>customers.Capacity.GetCapacities().All(c=>c.Used==0) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            }
            finally {endEndpointWitness?.Invoke();}
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
            yield return PrimePickupFamilyAdmission();
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
        // Default-off observer of original retained facts; no query, guard replay or state edits.
        // 只复制原时序和原拒绝点；不改变任务、布局、时间或原等待断言。
        private Action BeginCounterWorkingWindowTrace(string first,string second,out Action poll)
        {
            poll=()=>{};
            if(!Environment.GetCommandLineArgs().Contains("-counterWorkingWindowTrace"))return ()=>{};
            object Read(object value,string name)
            {
                if(value==null)return null;
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
                var field=value.GetType().GetField(name,flags);if(field!=null)return field.GetValue(value);
                return value.GetType().GetProperty(name,flags)?.GetValue(value);
            }
            string Fields(object value,params string[] names)=>value==null?"NULL":string.Join(",",names.Select(n=>n+"="+Read(value,n)));
            string Key(TrafficIntentKey key)=>key.ActorId+"/"+key.IntentId+"/g"+key.Generation;
            var records=new System.Collections.Generic.List<string>();var errors=new System.Collections.Generic.List<string>();
            var stages=new System.Collections.Generic.HashSet<string>();var snapshots=0;var next=0d;var ended=false;
            void Sample(string stage,string originalResult="")
            {
                if(snapshots>=48)return;snapshots++;
                try
                {
                    var visits=Read(customers,"visits") as IDictionary;var core=Read(customers,"traffic");var entries=Read(core,"entries") as IDictionary;
                    var queue=Read(customers,"pickupQueue");var reservations=Read(queue,"reservations") as IDictionary;
                    records.Add("WORKING_WINDOW stage="+stage+" frame="+Time.frameCount+" sceneTime="+Time.timeSinceLevelLoadAsDouble.ToString("R")+
                        " originalResult="+originalResult+" owner="+Fields(owner,"held","comboReady","layoutPending","layoutFailed","pickupWaitVisit","pickupWaitReason")+
                        " employeesEnabled="+employees.isActiveAndEnabled+" holdCollection="+owner.HoldCollection+
                        " staff=["+string.Join(";",employees.Snapshot.Select(e=>e.Role+"/"+e.State+"/h"+e.HandleId+"/remaining="+e.RemainingWorkSeconds.ToString("R")))+"]");
                    foreach(var id in new[]{first,second})
                    {
                        var visit=visits!=null && visits.Contains(id)?visits[id]:null;var actor=Read(visit,"Actor") as NavigationActor;
                        records.Add("WORKING_WINDOW_VISIT "+Fields(visit,"Id","State","CounterArrived","PickupAssigned","PickupInTransit","PickupArrived","HasCollected","Generation","RequestId","TrafficMovePending","PendingPickupSegment")+
                            " pose="+actor?.transform.position.ToString("F6")+" assignment="+Fields(Read(visit,"Assignment"),"SlotIndex","Generation","Arrived","Position")+
                            " reservationAnchor="+Fields(Read(visit,"PendingReservationAnchor"),"Submitted","FirstFrame","Start","SideIndex"));
                    }
                    records.Add("WORKING_WINDOW_QUEUE "+Fields(queue,"Version","deliveryOwner","deliveryPosition","collectionOwner")+
                        " slotCount="+(Read(queue,"waitSlots") as Vector3[])?.Length+" reservationCount="+reservations?.Count+
                        " reservations=["+(reservations==null?"NULL":string.Join(";",reservations.Keys.Cast<object>().Select(k=>k+"/"+Fields(reservations[k],"Slot","Advancing","Arrived","Target"))))+"]");
                    if(entries!=null)foreach(var keyObject in entries.Keys.Cast<object>().ToArray())
                    {
                        var entry=entries[keyObject];if(!(keyObject is TrafficIntentKey key) || Equals(Read(entry,"Retired"),true))continue;
                        var decision=Read(entry,"Decision") as TrafficDecision;var request=Read(entry,"Request") as TrafficRequest;
                        records.Add("WORKING_WINDOW_CORE key="+Key(key)+" "+Fields(entry,"Queued","Prepared","Active","FirstFrame","Queries","JobIndex","WaitRemaining")+
                            " decision="+Fields(decision,"Kind","Reason","RequestSequence","AcquiredSequence")+
                            " blockers=["+(decision==null?"NULL":string.Join(";",decision.Blockers.Select(b=>Key(b.Owner)+"/"+b.ResourceId+"/"+b.Proof)))+"]"+
                            " leg="+Fields(Read(entry,"Leg"),"Index","NeedsQuery","Committed","Bound","Completed")+
                            " geometry="+request?.Start.ToString("F6")+"->"+request?.Target.ToString("F6"));
                    }
                }
                catch(Exception error){errors.Add(stage+":"+error.GetType().Name+":"+error.Message);}
            }
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var previous=(Action<string,string,string,Vector3,System.Collections.Generic.IReadOnlyList<Vector3>,Vector3[],string>)hook.GetValue(customers);
            Action<string,string,string,Vector3,System.Collections.Generic.IReadOnlyList<Vector3>,Vector3[],string> observer=(operation,id,related,target,path,obstacles,result)=>
            {
                previous?.Invoke(operation,id,related,target,path,obstacles,result);
                if(id!=second || operation!="reserve.original-refusal")return;
                var stage=result==null?"NULL":result.Split(' ')[0];
                if(stages.Count<8 && stages.Add(stage))Sample("original-reserve-refusal/"+stage,result);
            };
            hook.SetValue(customers,observer);
            void OnTask(CafeTaskEvent e)
            {
                if((e.Checkpoint.VisitId==first || e.Checkpoint.VisitId==second) && e.Kind!=CafeTaskEventKind.Progress)
                    Sample("task/"+e.Kind+"/"+e.Role+"/"+e.Checkpoint.VisitId);
            }
            employees.IntegratedTaskEvent+=OnTask;
            poll=()=>{if(Time.timeSinceLevelLoadAsDouble<next)return;next=Time.timeSinceLevelLoadAsDouble+1;Sample("normal-frame");};
            var previousCleanup=parkingEndpointWitnessCleanup;var previousStoredEnd=parkingEndpointStoredEnd;Action finish=null;
            finish=()=>
            {
                if(ended)return;ended=true;Sample("end");employees.IntegratedTaskEvent-=OnTask;
                if(ReferenceEquals(hook.GetValue(customers),observer))hook.SetValue(customers,previous);
                else errors.Add("CorridorObserver replaced externally; replacement preserved");
                if(ReferenceEquals(parkingEndpointWitnessCleanup,finish))parkingEndpointWitnessCleanup=previousCleanup;
                if(ReferenceEquals(parkingEndpointStoredEnd,finish))parkingEndpointStoredEnd=previousStoredEnd;
                foreach(var record in records)TestContext.WriteLine(record);
                TestContext.WriteLine("WORKING_WINDOW_END status="+(errors.Count==0?"OBSERVED":"UNKNOWN")+" snapshots="+snapshots+" cap=48 errors=["+string.Join(";",errors)+"]");
                previousCleanup?.Invoke();
            };
            parkingEndpointWitnessCleanup=finish;parkingEndpointStoredEnd=finish;Sample("second-born");return finish;
        }
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
            Assert.That(customers.Snapshot,Is.Empty);Assert.That(owner.Snapshot.Orders,Is.Empty);
            var machine=runtime.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
            var register=runtime.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.cash-register.01");
            var pickup=runtime.FunctionalSurfaceLayout.PickUpPoints.Single();var pickupAddress=pickup.Address;
            var lower=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(7,5));
            var upper=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(7,7));
            var session=CafeLoopRuntimeProof.Field<DecorationSession>(decor,"session");
            // One fixed formal recipe; no timing/actor/state edits or alternative-layout search.
            // 只搬现有柜体并走正式Confirm；首客路径更短，第二客仍靠原1秒收银/3秒制作形成真实重叠。
            IEnumerator MoveWorkingSupport(string id,GridPosition position)
            {
                Assert.That(session.BeginExisting(id).Succeeded,Is.True);
                Assert.That(session.MovePreview(position).Succeeded,Is.True);
                yield return MatrixConfirmFurniture();
            }
            Assert.That(register.Rotation,Is.EqualTo(FurnitureRotation.Degrees90));
            yield return MoveWorkingSupport(register.Address.SupportFurnitureInstanceId,new GridPosition(1,1));
            yield return MoveWorkingSupport(machine.Address.SupportFurnitureInstanceId,new GridPosition(6,0));
            yield return MoveWorkingSupport(pickup.Address.SupportFurnitureInstanceId,new GridPosition(0,4));
            yield return MoveWorkingSupport(lower.InstanceId,new GridPosition(0,3));
            yield return MoveWorkingSupport(upper.InstanceId,new GridPosition(0,5));
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,machine.InstanceId),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True);Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);decor.ExitDecorationMode();
            yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));yield return null;
            Assert.That(runtime.FunctionalSurfaceLayout.PickUpPoints.Single().InstanceId,Is.EqualTo(pickup.InstanceId));
            Assert.That(runtime.FunctionalSurfaceLayout.PickUpPoints.Single().Address,Is.EqualTo(pickupAddress));
            var sides=customers.GetPickupServiceTargets(pickup.InstanceId);Assert.That(sides.Count,Is.EqualTo(1));
            Assert.That(Vector3.Distance(sides.Single().Position,new Vector3(-2.5f,0,.5f)),Is.LessThan(.01f));
            Assert.That(Vector3.Distance(CafeLoopRuntimeProof.Field<Vector3[]>(customers,"counterWaitSlots")[0],new Vector3(-3.5f,0,-2.5f)),Is.LessThan(.01f));
            Assert.That(employees.TryGetBinding(EmployeeRole.Cashier,out var cashierBinding),Is.True);
            Assert.That(employees.TryGetBinding(EmployeeRole.Barista,out var baristaBinding),Is.True);
            Assert.That(Vector3.Distance(cashierBinding.WorkTarget.Position,new Vector3(-1.5f,0,-2.5f)),Is.LessThan(.01f));
            Assert.That(Vector3.Distance(baristaBinding.WorkTarget.Position,new Vector3(3.5f,0,-3.5f)),Is.LessThan(.01f));
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);var first=customers.Snapshot.Single().VisitId;
            bool CashierWorkingFor(string id)=>employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).State==EmployeeState.Working &&
                employees.TryCaptureOrderCheckpoint(EmployeeRole.Cashier,out var work) && work.VisitId==id &&
                work.Stage==CafeWorkStage.Working && work.RemainingWorkSeconds>0;
            yield return Until(()=>CashierWorkingFor(first));
            var firstCashier=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier);
            Assert.That(firstCashier.RemainingWorkSeconds,Is.GreaterThan(0).And.LessThanOrEqualTo(1f),"Original integrated Cashier duration remains one second");
            var cashierRow=CafeLoopRuntimeProof.Field<System.Collections.IEnumerable>(employees,"employees").Cast<object>()
                .Single(row=>(EmployeeRole)MovementRead(row,"Role")==EmployeeRole.Cashier);
            var firstCashierToken=MovementRead(cashierRow,"AcceptedToken");Assert.That(firstCashierToken,Is.Not.Null);
            // Admit the second original proposal during the first real Cashier task; all owners keep stepping.
            // 同一首收银任务尚未结束才出生第二客，不停组件、不冻结Core、不延长任何工作时间。
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(CashierWorkingFor(first),Is.True,"FIXTURE: the second proposal must fit the same first Cashier Working window");
            Assert.That(employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).HandleId,Is.EqualTo(firstCashier.HandleId));
            Assert.That(MovementRead(cashierRow,"AcceptedToken"),Is.EqualTo(firstCashierToken));
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);var second=customers.Snapshot.Single(v=>v.VisitId!=first).VisitId;
            var endWorkingTrace=BeginCounterWorkingWindowTrace(first,second,out var pollWorkingTrace);
            try
            {
            var originalTokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();
            Assert.That(customers.CafeSnapshot.Single(v=>v.VisitId==second).CounterArrived,Is.False,"Second guest must still physically approach through the normal driver");
            yield return Until(()=>{pollWorkingTrace();return employees.Snapshot.All(e=>e.State==EmployeeState.Working) && CashierWorkingFor(second);});
            Assert.That(owner.CanProgress && employees.isActiveAndEnabled,Is.True);
            Assert.That(employees.TryCaptureOrderCheckpoint(EmployeeRole.Barista,out var firstWork),Is.True);
            Assert.That(firstWork.VisitId,Is.EqualTo(first));Assert.That(firstWork.Stage,Is.EqualTo(CafeWorkStage.Working));
            Assert.That(firstWork.RemainingWorkSeconds,Is.GreaterThan(0).And.LessThanOrEqualTo(3f),"Original Barista duration remains three seconds");
            var secondCashier=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier);
            var secondCashierToken=MovementRead(cashierRow,"AcceptedToken");Assert.That(secondCashierToken,Is.Not.Null);
            Assert.That(owner.Snapshot.PaidVisitIds.Contains(second),Is.False);
            Assert.That(owner.Snapshot.Orders.Values.Any(order=>order.CustomerId==second),Is.False);
            TestContext.WriteLine("COUNTER_WORKING_OVERLAP first="+first+" second="+second+" frame="+Time.frameCount+
                " cashierHandle="+secondCashier.HandleId+" cashierRemaining="+secondCashier.RemainingWorkSeconds+" baristaRemaining="+firstWork.RemainingWorkSeconds);
            var barriers=new System.Collections.Generic.List<NavigationActor>();
            foreach(var x in new[]{-.5f,.5f,1.5f,2.5f,3.5f})barriers.Add(Blocker(new Vector3(x,0,-2.5f),"counter-working-corridor-"+x));
            var firstKey=owner.Snapshot.Orders.Single().Key;var waitingObserved=false;
            for(var frame=0;frame<300 && employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).State==EmployeeState.Working;frame++)
            {
                var staff=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);
                Assert.That(staff.State,Is.Not.EqualTo(EmployeeState.Blocked),"Related real Cashier Working has finite progress and reserved pickup; must not become permanent Prepared failure: "+staff.Failure);
                // A still-moving first paid guest must not masquerade as the second Working owner.
                // 首客已真实到PU后才算本例的Working等待；当前任务/handle/token与原预留都必须仍在。
                if(staff.State==EmployeeState.WaitingToDeliver && customers.PickupAssignments.Any(row=>row.VisitId==first && row.Arrived))
                {
                    Assert.That(CashierWorkingFor(second),Is.True);
                    Assert.That(employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).HandleId,Is.EqualTo(secondCashier.HandleId));
                    Assert.That(MovementRead(cashierRow,"AcceptedToken"),Is.EqualTo(secondCashierToken));
                    Assert.That(customers.PickupAssignments.Single(row=>row.VisitId==first).VisitId,Is.EqualTo(first));
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
            Assert.That(waitingObserved,Is.True,"The finite static Working window must really obstruct a Prepared delivery; firstPickupArrived="+customers.PickupAssignments.Any(row=>row.VisitId==first && row.Arrived)+" customer="+customers.Status);
            var departed=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits")[second];var departing=world.RegisteredActors.Single(a=>a.ActorId==second);
            TestContext.WriteLine("COUNTER_FINISHED_TRAFFIC state="+customers.Snapshot.Single(v=>v.VisitId==second).State+" request="+departed.GetType().GetField("RequestId").GetValue(departed)+
                " pending="+departed.GetType().GetField("PendingPickupSegment").GetValue(departed)+" agentStopped="+departing.Agent.isStopped+" pendingPath="+departing.Agent.pathPending+" hasPath="+departing.Agent.hasPath+" target="+departing.Agent.destination+
                " route="+string.Join(";",(System.Collections.Generic.Queue<Vector3>)departed.GetType().GetField("Route").GetValue(departed)??new System.Collections.Generic.Queue<Vector3>()));
            // These registered bodies create only the controlled CR traffic window.
            // Retire them after that same paid body really vacates the Counter region;
            // a permanent artificial wall is a separate bounded-routing geometry.
            yield return Until(()=>customers.CounterAssignments.All(a=>a.VisitId!=second));
            Assert.That(owner.Snapshot.PaidVisitIds.Contains(second),Is.True);Assert.That(Vector3.Distance(departing.transform.position,new Vector3(-3.5f,0,-2.5f)),Is.GreaterThan(.97f));
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
            finally {endWorkingTrace();}
        }
        // One real unborn proposal; preserve the following original TrySpawn/assert as first canonical birth.
        // 只用普通 frames 等同一个实际 anchor/key；不额外 pump 或重复提交。
        private IEnumerator PrimePickupFamilyAdmission()
        {
            var beforeNext=MovementRead(customers,"nextVisit");
            Assert.That(customers.Snapshot.Count,Is.Zero,"Admission prime requires the original empty scene premise");
            var primed=customers.TrySpawn();var anchor=MovementRead(customers,"pendingAdmissionAnchor");
            Assert.That(primed,Is.False,"Prime must not perform canonical birth");
            Assert.That(anchor,Is.Not.Null,"Prime must retain the actual unborn admission");
            var coordinator=(ServiceTrafficCoordinator)MovementRead(anchor,"Coordinator");
            var key=(TrafficIntentKey)MovementRead(anchor,"Key");
            bool Same()=>ReferenceEquals(MovementRead(customers,"pendingAdmissionAnchor"),anchor) &&
                ReferenceEquals(MovementRead(customers,"traffic"),coordinator) && ((TrafficIntentKey)MovementRead(anchor,"Key")).Equals(key);
            for(var frame=0;frame<600;frame++)
            {
                if(!Same())break;var decision=coordinator.GetDecision(key);
                if(decision.Kind==TrafficDecisionKind.Granted)break;
                if(decision.Kind!=TrafficDecisionKind.DeferredByBudget && decision.Kind!=TrafficDecisionKind.WaitingForTraffic)break;
                yield return null;
            }
            TestContext.WriteLine("PICKUP_FAMILY_ADMISSION_PRIME same="+Same()+" key="+key+
                " decision="+coordinator.GetDecision(key).Kind+" canonicalVisits="+customers.Snapshot.Count);
            Assert.That(Same(),Is.True,"Preparation must retain the original actual coordinator/anchor/key");
            Assert.That(coordinator.GetDecision(key).Kind,Is.EqualTo(TrafficDecisionKind.Granted),"Ordinary frames must prepare the same original source");
            Assert.That(customers.Snapshot.Count,Is.Zero,"Only the original next TrySpawn may perform first canonical birth");
            Assert.That(MovementRead(customers,"nextVisit"),Is.EqualTo(beforeNext));
        }

        IEnumerator ActualPaidYield(bool collectBeforeMachineReturn,int lifecycle=0,Func<bool> preparationObserved=null)
        {
            var paid=0;var prepared=0;
            employees.IntegratedTaskEvent+=e=>{if(e.Kind==CafeTaskEventKind.ServiceFinished)paid++;if(e.Kind==CafeTaskEventKind.Prepared)prepared++;};
            yield return PrimePickupFamilyAdmission();
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>owner.Snapshot.Orders.Count==1);
            var key=owner.Snapshot.Orders.Single().Key;
            var tokens=customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray();
            // Wait for the real paid Transfer and its accepted/bound/live leg before disabling EFC.
            // 仅普通 frames + 实际 owner/Native 读取；不让 disable 把尚未 issued 的 Transfer 暂停在半途。
            bool ActualPickupTransferIssuedOrArrived()
            {
                var rows=customers.PickupAssignments.Where(a=>a.VisitId==id).ToArray();
                if(rows.Length!=1)return false;
                var visit=((IDictionary)MovementRead(customers,"visits"))[id];
                var paidTask=MovementRead(visit,"PaidPickup");
                if(paidTask==null || !(bool)MovementRead(paidTask,"Transferred"))return false;
                if(rows[0].Arrived)return true;
                var move=MovementRead(visit,"TrafficMove");var request=(long)MovementRead(visit,"RequestId");
                var live=(IDictionary)MovementRead(world.Service,"byActor");
                if(move==null || request<=0 || !live.Contains(id) || (long)MovementRead(live[id],"Id")!=request)return false;
                var coordinator=(ServiceTrafficCoordinator)MovementRead(move,"Coordinator");
                var intent=(TrafficIntentKey)MovementRead(move,"Intent");
                var source=(TrafficDecision)MovementRead(move,"Source");
                var entries=(IDictionary)MovementRead(coordinator,"entries");
                if(source==null || !intent.Equals((TrafficIntentKey)MovementRead(paidTask,"ActiveIntent")) || !entries.Contains(intent))return false;
                var leg=MovementRead(entries[intent],"Leg");
                return leg!=null && (bool)MovementRead(leg,"Committed") && (bool)MovementRead(leg,"Bound") &&
                    !(bool)MovementRead(leg,"Completed") && ReferenceEquals(MovementRead(leg,"ActorLife"),MovementRead(visit,"Actor")) &&
                    ((TrafficNativeStamp)MovementRead(leg,"Stamp")).RequestId==request;
            }
            yield return Until(ActualPickupTransferIssuedOrArrived);
            TestContext.WriteLine("PICKUP_FAMILY_TRANSFER_READY visit="+id+" rowArrived="+customers.PickupAssignments.Single(a=>a.VisitId==id).Arrived+
                " request="+MovementRead(((IDictionary)MovementRead(customers,"visits"))[id],"RequestId"));
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
            var continuationNativeObserved=false;ServiceTrafficCoordinator continuationCore=null;
            object originalYieldEntry=null,originalYieldParent=null,originalYieldPlan=null,originalParentEntry=null,originalParentContext=null;
            TrafficIntentKey originalYieldKey=default,continuationKey=default,originalParentKey=default;
            int originalYieldQueries=0,originalYieldFrame=0,originalYieldCandidate=0,originalYieldCount=0;
            long originalYieldSequence=0,originalYieldAcquired=0,originalYieldRequest=0,originalParentHandle=0;
            PickupQueueService.ReservationClaim originalYieldClaim=null;

            var observeYieldLifecycle=lifecycle==1 && Environment.GetCommandLineArgs().Contains("-yieldLifecycleSource");
            var lifecycleSourceFailureObserved=false;string lifecycleSourceCaptureError=null;
            object savedYieldVisit=null,savedYieldParent=null,savedYieldPlan=null,savedYieldChild=null,savedYieldCore=null,savedYieldEntry=null,savedYieldKey=null;
            object savedVisitGeneration=null,savedYieldGeneration=null,savedPhaseSequence=null,savedChildSource=null,savedChildQueries=null;
            const System.Reflection.BindingFlags rawFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            object ReadYield(object value,string name)=>value?.GetType().GetField(name,rawFlags)?.GetValue(value);
            string RefYield(object value)=>value==null?"NULL":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            string KeyYield(object value)=>value is TrafficIntentKey k?k.Session+"/e"+k.Epoch+"/"+k.ActorId+"/"+k.IntentId+"/g"+k.Generation+"/r"+k.LayoutRevision+"/"+k.Context:"NULL";
            for(var frame=0;frame<3000 && !actualReturn;frame++)
            {
                if(preparationObserved?.Invoke()==true){clearControlled();yield break;}
                var staff=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);
                if(observeYieldLifecycle && !lifecycleSourceFailureObserved && staff.State==EmployeeState.Blocked)
                {
                    lifecycleSourceFailureObserved=true;
                    // Read the original captured objects after the original failure; no policy replay or navigation query.
                    try
                    {
                        var liveVisits=ReadYield(customers,"visits") as System.Collections.IDictionary;
                        var currentVisit=liveVisits!=null && liveVisits.Contains(id)?liveVisits[id]:null;
                        var currentParent=ReadYield(currentVisit,"YieldDeliverySource");var currentPlan=ReadYield(currentParent,"OriginalDeliveryPlan");
                        var currentCore=ReadYield(customers,"traffic");var entries=ReadYield(currentCore,"entries") as System.Collections.IDictionary;
                        var currentEntry=entries!=null && savedYieldKey!=null && entries.Contains(savedYieldKey)?entries[savedYieldKey]:null;
                        var decision=ReadYield(savedYieldEntry,"Decision") as TrafficDecision;var source=savedChildSource as TrafficDecision;
                        TestContext.WriteLine("YIELD_LIFECYCLE_ORIGINAL_FAILURE status="+(lifecycleSourceCaptureError==null && savedYieldEntry!=null?"OBSERVED":"UNKNOWN")+
                            " cap=1 frame="+Time.frameCount+" visit="+id+" captureError="+lifecycleSourceCaptureError+
                            " sameVisit="+ReferenceEquals(savedYieldVisit,currentVisit)+" sameParent="+ReferenceEquals(savedYieldParent,currentParent)+
                            " samePlan="+ReferenceEquals(savedYieldPlan,currentPlan)+" sameChild="+ReferenceEquals(savedYieldChild,ReadYield(currentPlan,"Child"))+
                            " sameCore="+ReferenceEquals(savedYieldCore,currentCore)+" sameEntry="+ReferenceEquals(savedYieldEntry,currentEntry)+
                            " state="+ReadYield(savedYieldVisit,"State")+" resume="+ReadYield(savedYieldVisit,"ResumeState")+" failure="+ReadYield(savedYieldVisit,"Failure")+
                            " beforeVisitGeneration="+savedVisitGeneration+" visitGeneration="+ReadYield(savedYieldVisit,"Generation")+
                            " beforeYieldGeneration="+savedYieldGeneration+" yieldGeneration="+ReadYield(savedYieldPlan,"YieldGeneration")+
                            " beforePhaseSequence="+savedPhaseSequence+" phaseSequence="+ReadYield(savedYieldVisit,"TrafficPhaseSequence")+
                            " planPhaseSequence="+ReadYield(savedYieldPlan,"PhaseSequence")+" yieldPhase="+ReadYield(savedYieldVisit,"DeliveryYieldPhase")+
                            " request="+ReadYield(savedYieldVisit,"RequestId")+" journey="+KeyYield(ReadYield(savedYieldVisit,"TrafficJourney"))+
                            " parent="+RefYield(savedYieldParent)+" plan="+RefYield(savedYieldPlan)+" child="+RefYield(savedYieldChild)+" key="+KeyYield(savedYieldKey)+
                            " planContext="+RefYield(ReadYield(savedYieldPlan,"Context"))+" claim="+RefYield(ReadYield(savedYieldPlan,"Claim"))+
                            " yieldClaim="+RefYield(ReadYield(savedYieldPlan,"YieldClaim"))+" childSource="+RefYield(source)+
                            " samePlanSource="+ReferenceEquals(source,ReadYield(savedYieldPlan,"ChildSource"))+" sourceKind="+source?.Kind+" sourceAcquired="+source?.AcquiredSequence+
                            " childEntry="+RefYield(savedYieldEntry)+" retired="+ReadYield(savedYieldEntry,"Retired")+
                            " decision="+decision?.Kind+"/"+decision?.Reason+"/"+decision?.FailureDisposition+
                            " journeyPermit="+RefYield(ReadYield(savedYieldEntry,"JourneyPermit"))+" sameOriginalSource="+ReferenceEquals(source,ReadYield(savedYieldEntry,"JourneyPermit"))+
                            " originalFirst="+ReadYield(savedYieldEntry,"FirstFrame")+" beforeSpentQueries="+savedChildQueries+" spentQueries="+ReadYield(savedYieldEntry,"Queries")+
                            " observationAddedQueries=0 policyReplay=0");
                    }
                    catch(Exception error){TestContext.WriteLine("YIELD_LIFECYCLE_ORIGINAL_FAILURE status=UNKNOWN "+error.GetType().Name+":"+error.Message);}
                }
                Assert.That(staff.State,Is.Not.EqualTo(EmployeeState.Blocked),staff.Failure);
                Assert.That(customers.Snapshot.Single(v=>v.VisitId==id).State,Is.Not.EqualTo(CustomerVisitState.Blocked),customers.Status);
                var yielding=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits")[id];
                if(lifecycle==1 && lifecycleObserved && !continuationNativeObserved && (long)ReadYield(yielding,"RequestId")>0)
                {
                    var continuation=ReadYield(originalYieldPlan,"Continuation");var move=ReadYield(yielding,"TrafficMove");
                    var entries=(System.Collections.IDictionary)ReadYield(continuationCore,"entries");var fresh=entries[continuationKey];var leg=ReadYield(fresh,"Leg");
                    Assert.That((TrafficIntentKey)ReadYield(move,"Intent"),Is.EqualTo(continuationKey));Assert.That(ReadYield(continuation,"Source"),Is.SameAs(ReadYield(move,"Source")));
                    Assert.That(ReadYield(leg,"Bound"),Is.EqualTo(true));var stamp=(TrafficNativeStamp)ReadYield(leg,"Stamp");
                    Assert.That(stamp.RequestId,Is.EqualTo((long)ReadYield(yielding,"RequestId")));Assert.That(stamp.RequestId,Is.Not.EqualTo(originalYieldRequest));
                    Assert.That((int)ReadYield(originalYieldPlan,"YieldGeneration"),Is.EqualTo((int)ReadYield(yielding,"Generation")),"The original parent plan must follow the actual strict generation");
                    Assert.That(stamp.LegGeneration,Is.EqualTo((int)ReadYield(yielding,"Generation")),"Actual Bound Native stamp, Visit and plan must agree exactly");
                    Assert.That(ReadYield(yielding,"YieldDeliverySource"),Is.SameAs(originalYieldParent));Assert.That(ReadYield(originalYieldParent,"OriginalDeliveryPlan"),Is.SameAs(originalYieldPlan));
                    Assert.That(ReadYield(originalYieldPlan,"Context"),Is.SameAs(originalParentContext));Assert.That(entries[originalParentKey],Is.SameAs(originalParentEntry));
                    Assert.That(staff.HandleId,Is.EqualTo(originalParentHandle));Assert.That(ReadYield(originalYieldEntry,"Retired"),Is.EqualTo(true));
                    Assert.That(ReadYield(originalYieldEntry,"Queries"),Is.EqualTo(originalYieldQueries));Assert.That(ReadYield(fresh,"FirstFrame"),Is.EqualTo(originalYieldFrame));
                    Assert.That((int)ReadYield(fresh,"Queries"),Is.InRange(originalYieldQueries,(int)ReadYield(continuationCore,"queriesPerIntent")));
                    Assert.That(ReadYield(fresh,"CandidateCount"),Is.EqualTo(originalYieldCount));Assert.That(ReadYield(fresh,"CurrentIndex"),Is.EqualTo(originalYieldCandidate));
                    var pickupOwner=(PickupQueueService)ReadYield(yielding,"PickupOwner");Assert.That(pickupOwner.TryCaptureReservation(id,out var samePickup),Is.True);
                    Assert.That(originalYieldClaim.IsSameReservation(samePickup),Is.True);Assert.That(samePickup.Assignment.Generation,Is.EqualTo(originalYieldClaim.Assignment.Generation));
                    Assert.That(samePickup.Assignment.Position,Is.EqualTo(originalYieldClaim.Assignment.Position));
                    CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Where(token=>token.OwnerId==id).Select(token=>token.Token));
                    continuationNativeObserved=true;
                    TestContext.WriteLine("YIELD_OWNER_CONTINUATION_NATIVE sameParent=True samePaidPqsAndTokens=True oldRetired=True oldQueries="+originalYieldQueries+
                        " cumulativeQueries="+ReadYield(fresh,"Queries")+" originalFrame="+originalYieldFrame+" newActualRequest="+stamp.RequestId+" explicitOnce=True");
                }

                if(lifecycle!=0 && !lifecycleObserved && (int)yielding.GetType().GetField("DeliveryYieldPhase").GetValue(yielding)==1 &&
                    (long)yielding.GetType().GetField("RequestId").GetValue(yielding)>0 && Vector3.Distance(body.transform.position,original.Position)>.1f)
                {
                    lifecycleObserved=true;var request=(long)yielding.GetType().GetField("RequestId").GetValue(yielding);
                    var generation=(int)yielding.GetType().GetField("Generation").GetValue(yielding);
                    if(observeYieldLifecycle)
                    {
                        // Capture only at the real moving phase/request before the existing Pause/Disable actions.
                        try
                        {
                            savedYieldVisit=yielding;savedYieldParent=ReadYield(yielding,"YieldDeliverySource");
                            savedYieldPlan=ReadYield(savedYieldParent,"OriginalDeliveryPlan");savedYieldChild=ReadYield(savedYieldPlan,"Child");
                            savedYieldKey=ReadYield(savedYieldChild,"Key");savedYieldCore=ReadYield(savedYieldPlan,"Coordinator");
                            var entries=ReadYield(savedYieldCore,"entries") as System.Collections.IDictionary;
                            savedYieldEntry=entries!=null && savedYieldKey!=null && entries.Contains(savedYieldKey)?entries[savedYieldKey]:null;
                            savedVisitGeneration=ReadYield(yielding,"Generation");savedYieldGeneration=ReadYield(savedYieldPlan,"YieldGeneration");
                            savedPhaseSequence=ReadYield(yielding,"TrafficPhaseSequence");savedChildSource=ReadYield(savedYieldPlan,"ChildSource");
                            savedChildQueries=ReadYield(savedYieldEntry,"Queries");
                            if(savedYieldParent==null || savedYieldPlan==null || savedYieldChild==null || savedYieldCore==null || savedYieldEntry==null)
                                lifecycleSourceCaptureError="missing-original-yield-reference";
                        }
                        catch(Exception error){lifecycleSourceCaptureError=error.GetType().Name+":"+error.Message;}
                    }
                    if(lifecycle==1)
                    {
                        continuationCore=(ServiceTrafficCoordinator)ReadYield(customers,"traffic");originalYieldParent=ReadYield(yielding,"YieldDeliverySource");
                        originalYieldPlan=ReadYield(originalYieldParent,"OriginalDeliveryPlan");var originalChild=ReadYield(originalYieldPlan,"Child");
                        originalYieldKey=(TrafficIntentKey)ReadYield(originalChild,"Key");originalParentKey=(TrafficIntentKey)ReadYield(originalYieldParent,"Key");
                        var entries=(System.Collections.IDictionary)ReadYield(continuationCore,"entries");originalYieldEntry=entries[originalYieldKey];originalParentEntry=entries[originalParentKey];
                        originalYieldQueries=(int)ReadYield(originalYieldEntry,"Queries");originalYieldFrame=(int)ReadYield(originalYieldEntry,"FirstFrame");
                        originalYieldCandidate=(int)ReadYield(originalYieldEntry,"CurrentIndex");originalYieldCount=(int)ReadYield(originalYieldEntry,"CandidateCount");
                        originalYieldSequence=(long)ReadYield(originalYieldEntry,"RequestSequence");originalYieldAcquired=(long)ReadYield(originalYieldEntry,"AcquiredSequence");
                        originalYieldRequest=request;originalParentHandle=staff.HandleId;originalParentContext=ReadYield(originalYieldPlan,"Context");
                        var pickupOwner=(PickupQueueService)ReadYield(yielding,"PickupOwner");Assert.That(pickupOwner.TryCaptureReservation(id,out originalYieldClaim),Is.True);
                        Assert.That(originalYieldClaim.Assignment.Arrived,Is.False);Assert.That(originalYieldCount,Is.EqualTo(24));
                    }
                    time.SetPaused();var paused=body.transform.position;for(var f=0;f<8;f++)yield return null;
                    Assert.That(body.transform.position,Is.EqualTo(paused));Assert.That((int)yielding.GetType().GetField("Generation").GetValue(yielding),Is.EqualTo(generation));
                    if(lifecycle==1)
                    {
                        customers.enabled=false;yield return null;
                        Assert.That(world.Service.Cancel(request),Is.False,"Disable must already retire the owned live yield request");
                        Assert.That(world.RegisteredActors.Single(a=>a.ActorId==id),Is.SameAs(body));Assert.That(body.transform.position,Is.EqualTo(paused));
                        CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
                        var continuation=ReadYield(originalYieldPlan,"Continuation");Assert.That(continuation,Is.Not.Null,"Actual owner Disable must capture the same real issued yield before retirement");
                        Assert.That(ReadYield(continuation,"CancellationConfirmed"),Is.EqualTo(true));Assert.That(ReadYield(continuation,"Used"),Is.EqualTo(false));
                        var receipt=ReadYield(continuation,"Receipt");Assert.That(receipt,Is.Not.Null);Assert.That(ReadYield(originalYieldEntry,"Retired"),Is.EqualTo(true));
                        var parentRemaining=(float)ReadYield(originalParentEntry,"WaitRemaining");Assert.That(parentRemaining,Is.GreaterThan(0));
                        customers.enabled=true;time.SetNormal();
                        for(var pending=0;pending<4;pending++)
                        {
                            yield return null;
                            Assert.That(ReadYield(continuation,"Used"),Is.EqualTo(false),"Re-enable and automatic queue Resume cannot authorize a continuation");
                            Assert.That(ReadYield(continuation,"Receipt"),Is.SameAs(receipt));Assert.That(ReadYield(yielding,"RequestId"),Is.EqualTo(0L));
                            Assert.That(body.transform.position,Is.EqualTo(paused));Assert.That(ReadYield(originalYieldEntry,"Retired"),Is.EqualTo(true));
                        }
                        Assert.That((float)ReadYield(originalParentEntry,"WaitRemaining"),Is.LessThan(parentRemaining),"The unpaused held father deadline must actually spend time");
                        tools.ResumeBlockedFlow();Assert.That(ReadYield(continuation,"Used"),Is.EqualTo(true));
                        continuationKey=(TrafficIntentKey)ReadYield(continuation,"Intent");Assert.That(continuationKey.Equals(originalYieldKey),Is.False);
                        var entries=(System.Collections.IDictionary)ReadYield(continuationCore,"entries");var fresh=entries[continuationKey];
                        Assert.That(ReadYield(fresh,"FirstFrame"),Is.EqualTo(originalYieldFrame));Assert.That(ReadYield(fresh,"Queries"),Is.EqualTo(originalYieldQueries));
                        Assert.That(ReadYield(fresh,"RequestSequence"),Is.EqualTo(originalYieldSequence));Assert.That(ReadYield(fresh,"AcquiredSequence"),Is.EqualTo(originalYieldAcquired));
                        var beforeRepeat=(int)ReadYield(fresh,"Queries");tools.ResumeBlockedFlow();
                        Assert.That((TrafficIntentKey)ReadYield(continuation,"Intent"),Is.EqualTo(continuationKey));Assert.That(entries[continuationKey],Is.SameAs(fresh));
                        Assert.That(ReadYield(fresh,"Queries"),Is.EqualTo(beforeRepeat));Assert.That(ReadYield(continuation,"Receipt"),Is.Null);

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
                    clearControlled(); // The delivery/yield setup is complete; retire its synthetic exit blockers before qualifying collection.
                    employees.TryGetBinding(EmployeeRole.Barista,out var earlyReturnBinding);
                    if(Vector3.Distance(returning.transform.position,earlyReturnBinding.WorkTarget.Position)<=.081f){yield return null;continue;}
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
                            // The delivery-yield blockers include one .212 from the actual exit; retire this completed setup first.
                            // 只移除controlled fixture blockers；真实staff/body、exit及原完整collection安全门仍执行。
                            clearControlled();
                            // Select a real safe early-return window; the public acquire still rechecks every production guard.
                            // 仅资格检查；未满足时沿原3000-frame窗口推进，不把到达CM/Idle算提前成功。
                            var exitQualified=(bool)typeof(CustomerFlowController).GetMethod("PickupCollectionCanExit",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                                .Invoke(customers,new object[]{privateVisit,service.Position});
                            if(!exitQualified){yield return null;continue;}
                            if(preparationObserved!=null)
                            {
                                // New capture recipe observes the same real public attempt, before asynchronous commit.
                                customers.TryMoveToPickupService(id,out _);
                                if(preparationObserved()){clearControlled();yield break;}
                                yield return null;continue;
                            }
                            // Same public request progresses through the normal driver within the original Returning window.
                            // Asynchronous preparation is not a movement grant; require the actual original Native request.
                            var accepted=customers.TryMoveToPickupService(id,out var why);
                            if(!accepted){yield return null;continue;}
                            Assert.That(accepted,Is.True,"A held Ready head with a fully safe collection route must not wait for CM: "+why);
                            var earlyRequest=(long)privateVisit.GetType().GetField("RequestId").GetValue(privateVisit);
                            var earlyNative=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world.Service,"byId");
                            Assert.That(earlyRequest,Is.GreaterThan(0),"Early collection requires a real submitted Native leg, not a prepared proposal");
                            Assert.That(earlyNative.Contains(earlyRequest),Is.True);
                            Assert.That((string)MovementRead(earlyNative[earlyRequest],"ActorId"),Is.EqualTo(id));
                            TestContext.WriteLine("HELD_READY_SAFE_ACQUIRE frame="+frame+" staff="+staff.State+" staffPose="+returning.transform.position.ToString("F9")+
                                " workTarget="+beforeReturnBinding.WorkTarget.Position.ToString("F9")+" distance="+Vector3.Distance(returning.transform.position,beforeReturnBinding.WorkTarget.Position).ToString("R")+
                                " exitQualified=True request="+privateVisit.GetType().GetField("RequestId").GetValue(privateVisit));
                            issuedBeforeReturn=true;tools.ResumeCollection();
                        }
                    }
                }
                actualReturn=(owner.Snapshot.Orders[key].State==OrderState.ReadyForPickup || owner.Snapshot.Orders[key].State==OrderState.Completed) && staff.State==EmployeeState.Idle;
                yield return null;
            }
            // Capture-only recipe cannot certify a missing ReturnYield window by completing another phase.
            // 没到真实 seam 就交给外层明确报告 fixture NOT_PROVEN；不另加 retry loop。
            if(preparationObserved!=null){clearControlled();yield break;}
            Assert.That(actualYield,Is.True,"The actual paid visit must really yield; returned="+actualReturn+" order="+owner.Snapshot.Orders[key].State+" staff="+employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State+" failure="+owner.Snapshot.Failure);
            Assert.That(actualReturn,Is.True,owner.Snapshot.Failure);
            Assert.That(lifecycle==0 || lifecycleObserved,Is.True,"Lifecycle must occur during a real owned yield request");
            Assert.That(lifecycle!=1 || continuationNativeObserved,Is.True,"The same held paid owner must issue and bind exactly its authorized actual continuation");
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
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
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
                Assert.That(staff.Failure,Does.Contain("PlanningFailed")); Assert.That(staff.Failure,Does.Contain("no-independent-finite-release-or-cycle"));
                Assert.That(owner.Snapshot.Failure,Does.Contain(staff.Failure));
                Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.Preparing)); Assert.That(owner.Snapshot.Delivery,Is.Empty);
                Assert.That(employees.TryCaptureOrderCheckpoint(EmployeeRole.Barista,out var checkpoint),Is.True);
                Assert.That(checkpoint.Stage,Is.EqualTo(CafeWorkStage.Prepared)); Assert.That(checkpoint.RemainingWorkSeconds,Is.Zero);
                Assert.That(customers.Capacity.GetCapacities()[0].Used,Is.EqualTo(1));
                Assert.That(customers.Capacity.GetCapacities()[2].Used,Is.EqualTo(1));
                for(var frame=0;frame<20;frame++)yield return null;
                Assert.That(employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State,Is.EqualTo(EmployeeState.Blocked));
                world.Unregister(obstruction); obstruction.gameObject.SetActive(false); UnityEngine.Object.Destroy(obstruction.gameObject);
                // The failed worker's Resume must leave this healthy original paid route intact.
                // 同次同步调用前后只读原source；不查询路径、不重放guard，也不造新的顾客请求。
                var originals=(System.Collections.IDictionary)MovementRead(customers,"visits");var originalVisit=originals[id];
                var originalActor=(NavigationActor)MovementRead(originalVisit,"Actor");var originalMove=MovementRead(originalVisit,"TrafficMove");
                Assert.That(MovementRead(originalVisit,"State"),Is.EqualTo(CustomerVisitState.MovingToPickup),"FIXTURE: real first paid customer is still moving when the unrelated Barista is repaired");
                Assert.That(originalMove,Is.Not.Null);var originalCore=(ServiceTrafficCoordinator)MovementRead(originalMove,"Coordinator");
                var originalKey=(TrafficIntentKey)MovementRead(originalMove,"Intent");var originalSource=(TrafficDecision)MovementRead(originalMove,"Source");
                var originalEntry=((System.Collections.IDictionary)MovementRead(originalCore,"entries"))[originalKey];
                var originalNative=(long)MovementRead(originalVisit,"RequestId");var originalGeneration=(int)MovementRead(originalVisit,"Generation");
                var originalQueries=originalCore.QueriesConsumed(originalKey);
                var originalPose=originalActor.transform.position;var originalPaid=MovementRead(originalVisit,"PaidPickup");
                Assert.That(originalSource.Kind,Is.EqualTo(TrafficDecisionKind.Granted));Assert.That(originalSource.AcquiredSequence,Is.GreaterThan(0));
                Assert.That(MovementRead(originalEntry,"Retired"),Is.EqualTo(false));
                Assert.That(((TrafficDecision)MovementRead(originalEntry,"Decision")).FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None));
                Assert.That(MovementRead(originalPaid,"Accepted"),Is.EqualTo(true));Assert.That(MovementRead(originalPaid,"Transferred"),Is.EqualTo(true));
                Assert.That(MovementRead(originalPaid,"Failed"),Is.EqualTo(false));Assert.That(MovementRead(originalPaid,"RecoveryUsed"),Is.EqualTo(false));
                TestContext.WriteLine("STATIONARY_RESUME_ORIGINAL_BEFORE frame="+Time.frameCount+" key="+originalKey.Session+"/e"+originalKey.Epoch+"/"+originalKey.ActorId+"/"+originalKey.IntentId+
                    " request="+originalNative+" generation="+originalGeneration+" retired="+MovementRead(originalEntry,"Retired"));
                tools.ResumeBlockedFlow(); tools.ResumeCollection();
                Assert.That(originals[id],Is.SameAs(originalVisit));Assert.That(MovementRead(originalVisit,"Actor"),Is.SameAs(originalActor));
                Assert.That(MovementRead(originalVisit,"State"),Is.EqualTo(CustomerVisitState.MovingToPickup),"FEATURE: repairing a different employee must not retire/restart the healthy paid customer");
                Assert.That(MovementRead(originalVisit,"TrafficMove"),Is.SameAs(originalMove));
                Assert.That(MovementRead(originalMove,"Source"),Is.SameAs(originalSource));Assert.That(MovementRead(originalVisit,"TrafficJourney"),Is.EqualTo(originalKey));
                Assert.That(((System.Collections.IDictionary)MovementRead(originalCore,"entries"))[originalKey],Is.SameAs(originalEntry));
                Assert.That(MovementRead(originalEntry,"Retired"),Is.EqualTo(false));Assert.That(MovementRead(originalVisit,"RequestId"),Is.EqualTo(originalNative));
                Assert.That(MovementRead(originalVisit,"Generation"),Is.EqualTo(originalGeneration));Assert.That(originalActor.transform.position,Is.EqualTo(originalPose));
                if(originalNative>0)Assert.That(((System.Collections.IDictionary)MovementRead(world.Service,"byId")).Contains(originalNative),Is.True,"The same original Native request remains owned");
                Assert.That(MovementRead(originalVisit,"PaidPickup"),Is.SameAs(originalPaid));Assert.That(MovementRead(originalPaid,"Failed"),Is.EqualTo(false));
                Assert.That(MovementRead(originalPaid,"RecoveryUsed"),Is.EqualTo(false));
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
                Assert.That(originalCore.QueriesConsumed(originalKey),Is.EqualTo(originalQueries),"Resume must not query/replan the healthy original phase");
                TestContext.WriteLine("STATIONARY_RESUME_ORIGINAL_AFTER frame="+Time.frameCount+" sameVisitEntrySourceAndNative=True generation="+originalGeneration+" originalTokens=True originalSpentQueries="+originalQueries+" addedQueries=0");
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
            yield return PrimePickupFamilyAdmission();
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
            parkingEndpointStoredEnd?.Invoke(); // Copy before unload; caught errors are flushed after mandatory cleanup.
            exitTrafficObserving=centerTrafficObserving=false; // Stop receipt checks before timeout-driven scene teardown.
            matrixObserving=false;
            controlledHandoffCleanup?.Invoke("UnityTearDown");
            if(scene.IsValid() && scene.isLoaded) { SceneManager.SetActiveScene(SceneManager.CreateScene("P14 sides cleanup")); yield return SceneManager.UnloadSceneAsync(scene); }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputs??Array.Empty<UnityEngine.InputSystem.InputActionAsset>()); Time.captureDeltaTime=capture; Time.timeScale=scale;
            parkingEndpointWitnessCleanup?.Invoke(); // Nested IEnumerator assertion may skip the outer test's finally.
        }
    }
}
#endif

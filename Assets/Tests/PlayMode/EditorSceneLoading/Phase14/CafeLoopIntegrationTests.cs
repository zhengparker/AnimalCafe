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
        private Action initialSnapshotObservation;
        private Action admissionEndpointCleanup;
        private Action oneXPrepareLegCleanup;
        private Action oneXCounterAdvanceRetirementCleanup;
        private Action twoXOwnerInvalidationCleanup;
        private Action twoXPrepareLegCleanup;
        private Action fixedSpawnClockCleanup;
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
        {
            var retireFlush=Environment.GetCommandLineArgs().Contains("-trafficOneXCounterAdvanceRetirementWitness")?ObserveOneXCounterAdvanceRetirement():null;
            oneXCounterAdvanceRetirementCleanup=retireFlush;
            try
            {
            if(!Environment.GetCommandLineArgs().Contains("-trafficOneXPrepareLegWitness"))
            {yield return CafeLoopRuntimeProof.Twenty(scene,owner,customers,employees,cups,time,world,adapter,GameSpeed.Normal);yield break;}
            var flush=ObserveOneXPrepareLegRejection();oneXPrepareLegCleanup=flush;
            try{yield return CafeLoopRuntimeProof.Twenty(scene,owner,customers,employees,cups,time,world,adapter,GameSpeed.Normal);}
            finally{flush();oneXPrepareLegCleanup=null;}
            }
            finally{retireFlush?.Invoke();oneXCounterAdvanceRetirementCleanup=null;}
        }
        [UnityTest] public IEnumerator CompletesTwentyRealOrdersAndDrainsAllCapacityAtTwoX()
        {
            var paidSnapshotFlush=Environment.GetCommandLineArgs().Contains("-trafficTwoXPaidPickupSnapshotWitness")?ObserveInitialEmployeeSnapshotFailure("pickup-paid-movement"):null;
            if(paidSnapshotFlush!=null)initialSnapshotObservation=paidSnapshotFlush;
            var legFlush=Environment.GetCommandLineArgs().Contains("-trafficTwoXPrepareLegWitness")?ObserveOneXPrepareLegRejection("TWOX"):null;
            twoXPrepareLegCleanup=legFlush;
            try
            {
                if(!Environment.GetCommandLineArgs().Contains("-trafficTwoXOwnerInvalidationWitness"))
                {yield return CafeLoopRuntimeProof.Twenty(scene,owner,customers,employees,cups,time,world,adapter,GameSpeed.Fast);yield break;}
                var flush=ObserveTwoXOwnerInvalidation();twoXOwnerInvalidationCleanup=flush;
                try{yield return CafeLoopRuntimeProof.Twenty(scene,owner,customers,employees,cups,time,world,adapter,GameSpeed.Fast);}
                finally{flush();twoXOwnerInvalidationCleanup=null;}
            }
            finally{legFlush?.Invoke();twoXPrepareLegCleanup=null;if(paidSnapshotFlush!=null)FlushInitialEmployeeSnapshotObservation();}
        }
        [UnityTest] public IEnumerator CompletesTwentyRealOrdersAndDrainsAllCapacityAtTwoXWithOneSecondSpawnClock()
            => CompletesTwentyWithRealSpawnClock(()=>.1f);
        [UnityTest] public IEnumerator CompletesTwentyRealOrdersAndDrainsAllCapacityAtTwoXWithThreeSecondSpawnClock()
            => CompletesTwentyWithRealSpawnClock(()=>.3f);
        // Fix only real clock draws; the original driver still owns every admission and assertion.
        // 只固定真实Clock节奏；不直接出生顾客或修改业务/站位事实。
        private IEnumerator CompletesTwentyWithRealSpawnClock(Func<float> draw)
        {
            var originalClock=CafeLoopRuntimeProof.Field<CustomerSpawnClock>(customers,"clock");
            var clock=new CustomerSpawnClock(draw);var restored=false;
            Action restore=()=>{if(restored)return;if(customers!=null)CafeLoopRuntimeProof.Set(customers,"clock",originalClock);restored=true;};
            fixedSpawnClockCleanup=restore;
            try
            {
                CafeLoopRuntimeProof.Set(customers,"clock",clock);
                initialSnapshotObservation=ObserveInitialEmployeeSnapshotFailure("pickup-paid-movement");
                yield return CafeLoopRuntimeProof.Twenty(scene,owner,customers,employees,cups,time,world,adapter,GameSpeed.Fast);
            }
            finally
            {
                try{restore();}
                finally{if(ReferenceEquals(fixedSpawnClockCleanup,restore))fixedSpawnClockCleanup=null;FlushInitialEmployeeSnapshotObservation();}
            }
        }
        [UnityTest] public IEnumerator CompletesTwentyRecipeKeepsBusyFirstAdmissionUnsubmittedUntilNaturalClear()
        {
            var witness=new AdmissionFirstEndpointWitness(owner,customers,world);admissionEndpointCleanup=witness.Dispose;
            try{yield return CafeLoopRuntimeProof.Twenty(scene,owner,customers,employees,cups,time,world,adapter,GameSpeed.Normal,witness);}
            finally{witness.Dispose();admissionEndpointCleanup=null;}
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            var restoreClock=fixedSpawnClockCleanup;fixedSpawnClockCleanup=null;restoreClock?.Invoke();
            var retireFlush=oneXCounterAdvanceRetirementCleanup;oneXCounterAdvanceRetirementCleanup=null;retireFlush?.Invoke();
            var ownerFlush=twoXOwnerInvalidationCleanup;twoXOwnerInvalidationCleanup=null;ownerFlush?.Invoke();
            FlushInitialEmployeeSnapshotObservation();
            if(owner!=null) Object.Destroy(owner.gameObject); yield return null;
            if(scene.IsValid() && scene.isLoaded)
            { var empty=SceneManager.CreateScene("P14 loop cleanup"); SceneManager.SetActiveScene(empty); yield return SceneManager.UnloadSceneAsync(scene); }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets ?? Array.Empty<UnityEngine.InputSystem.InputActionAsset>());
            Time.captureDeltaTime=capture; Time.timeScale=scale;
            admissionEndpointCleanup?.Invoke();admissionEndpointCleanup=null; // Flush even if Unity did not dispose a failed parent iterator.
            var legFlush=oneXPrepareLegCleanup;oneXPrepareLegCleanup=null;legFlush?.Invoke();
            var twoXLegFlush=twoXPrepareLegCleanup;twoXPrepareLegCleanup=null;twoXLegFlush?.Invoke();
        }
        // Default-off: first original unacquired, still-owned Queued counter retirement, before its fields are cleared.
        // 只读现场字段与原 claim；不生成 Preview/Query/Refresh，不在 callback 内断言或修复。
        private Action ObserveOneXCounterAdvanceRetirement()
        {
            try
            {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);var previous=hook.GetValue(customers) as Delegate;
            var errors=new System.Collections.Generic.List<string>();var sampled=false;var disposed=false;var events=0;string firstFilter=null;
            void Log(string text){try{TestContext.WriteLine(text);}catch(Exception error){errors.Add("OUTPUT_MISSING:"+error.GetType().Name);}}
            bool SameRow(QueueAssignment a,QueueAssignment b)=>a.VisitId==b.VisitId && a.Generation==b.Generation && a.SlotIndex==b.SlotIndex && a.Position==b.Position;
            Action<string,string,string,Vector3,System.Collections.Generic.IReadOnlyList<Vector3>,Vector3[],string> observe=(operation,id,related,target,path,obstacles,origin)=>
            {
                if(sampled || disposed || operation!="traffic.counter-advance-retiring")return;
                events++;
                try
                {
                    var visits=StartupRead(customers,"visits") as IDictionary;
                    if(visits==null || id==null || !visits.Contains(id))throw new InvalidOperationException("UNKNOWN actual Visit missing");
                    var visit=visits[id];var anchor=StartupRead(visit,"PendingCounterAdvance");var source=StartupRead(anchor,"Source") as TrafficDecision;
                    if(source==null){if(firstFilter==null)firstFilter="sourceAbsent=True origin="+origin+" visit="+id;return;}
                    var counter=StartupRead(anchor,"Counter") as CounterQueueService;var claim=StartupRead(anchor,"Claim") as CounterQueueService.AssignmentClaim;
                    var core=StartupRead(anchor,"Coordinator") as ServiceTrafficCoordinator;var entries=StartupRead(core,"entries") as IDictionary;
                    var keyValue=StartupRead(anchor,"Key");var assignmentValue=StartupRead(anchor,"Assignment");
                    if(!(keyValue is TrafficIntentKey key) || !(assignmentValue is QueueAssignment assignment) || counter==null || claim==null || entries==null || !entries.Contains(key))
                        throw new InvalidOperationException("UNKNOWN original anchor/claim/Core Entry missing");
                    var entry=entries[key];var decision=StartupRead(entry,"Decision") as TrafficDecision;var acquired=StartupRead(entry,"AcquiredSequence");
                    if(!(acquired is long acquiredSequence) || decision==null)throw new InvalidOperationException("UNKNOWN actual Core ledger");
                    var rows=counter.Snapshot.Where(row=>row.VisitId==id).ToArray();var queued=Equals(StartupRead(visit,"State"),CustomerVisitState.Queued);
                    var claimCurrent=claim.IsCurrent;var sameAssignment=rows.Length==1 && SameRow(assignment,rows[0]) && SameRow(claim.Assignment,rows[0]);
                    var unacquired=acquiredSequence==0 && decision.AcquiredSequence==0;
                    var filter="origin="+origin+" queued="+queued+" unacquired="+unacquired+" claimCurrent="+claimCurrent+" sameAssignment="+sameAssignment+" rows="+rows.Length+
                        " key="+StartupValue(key)+" anchorAssignment="+assignment+" claimAssignment="+claim.Assignment+" currentRows="+string.Join(";",rows.Select(row=>row.VisitId+"/g"+row.Generation+"/slot"+row.SlotIndex+"/"+row.Position.ToString("F9")+"/arrived="+row.Arrived));
                    if(firstFilter==null)firstFilter=filter;
                    if(!queued || !unacquired || !claimCurrent || !sameAssignment)return;
                    sampled=true;
                    var preview=StartupRead(anchor,"Preview");var anchorFirst=StartupRead(anchor,"FirstFrame");var entryFirst=StartupRead(entry,"FirstFrame");
                    var queueEntries=StartupRead(counter,"entries") as IEnumerable;object currentEntry=null;
                    if(queueEntries!=null)currentEntry=queueEntries.Cast<object>().FirstOrDefault(row=>Equals(StartupRead(row,"Id"),id));
                    Log("COUNTER_ADVANCE_RETIREMENT cap=1 frame="+Time.frameCount+" "+filter+
                        " anchorRef="+StartupIdentity(anchor)+" sourceRef="+StartupIdentity(source)+" source="+StartupValue(source)+" actualDecisionRef="+StartupIdentity(decision)+" actualDecision="+StartupValue(decision)+
                        " anchorFirst="+StartupValue(anchorFirst)+" anchorAgeFrames="+(anchorFirst is int af?(Time.frameCount-af).ToString():"UNKNOWN")+
                        " entryFirst="+StartupValue(entryFirst)+" entryAgeFrames="+(entryFirst is int ef?(Time.frameCount-ef).ToString():"UNKNOWN")+
                        " capturedPreviewVersion="+StartupValue(StartupRead(preview,"ExpectedVersion"))+" capturedCounterCurrentVersion="+counter.Version+" flowCounterCurrentVersion="+StartupValue(StartupRead(StartupRead(customers,"queue"),"Version"))+
                        " originalClaimEntry="+StartupIdentity(StartupRead(claim,"entry"))+" currentQueueEntry="+StartupIdentity(currentEntry)+
                        " actualLedger="+StartupMembers(entry,"Queued","Prepared","Active","Retired","Discarded","Queries","WaitRemaining","FiniteLimit","FirstFrame","RequestSequence","AcquiredSequence"));
                    Log(TwentyTrafficFailureTrace.CounterGapCurrent(world,customers));
                }
                catch(Exception error)
                {
                    sampled=true;errors.Add(error.GetType().Name+":"+error.Message);
                    Log("COUNTER_ADVANCE_RETIREMENT cap=1 status=UNKNOWN frame="+Time.frameCount+" origin="+origin+" visit="+id+" error="+error.GetType().Name+"/"+error.Message);
                }
            };
            var installed=(Action<string,string,string,Vector3,System.Collections.Generic.IReadOnlyList<Vector3>,Vector3[],string>)Delegate.Combine(previous,observe);
            hook.SetValue(customers,installed);
            return ()=>
            {
                if(disposed)return;disposed=true;
                try{if(ReferenceEquals(hook.GetValue(customers),installed))hook.SetValue(customers,previous);else errors.Add("UNKNOWN observer replaced externally");}
                catch(Exception error){errors.Add("UNKNOWN cleanup:"+error.GetType().Name);}
                Log("COUNTER_ADVANCE_RETIREMENT_STATUS sampled="+sampled+" callbacks="+events+" firstFilter="+(firstFilter??"NONE_OBSERVED")+" errors="+string.Join(";",errors));
            };
            }
            catch(Exception error)
            {
                try{TestContext.WriteLine("COUNTER_ADVANCE_RETIREMENT_STATUS status=UNKNOWN registration="+error.GetType().Name+"/"+error.Message);}catch(Exception){}
                return ()=>{};
            }
        }
        // Default-off cap1: actual-exit owner with a real Waiting dependent; preserve its actual old kind.
        // Store immutable original strings only; report callback errors outside the policy stack.
        private Action ObserveTwoXOwnerInvalidation()
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var hook=typeof(ServiceTrafficCoordinator).GetField("WaitFailureObserver",flags);
            var previous=(Action<string>)hook.GetValue(core);var sampled=false;var finished=false;var detached=false;string record=null;
            var errors=new System.Collections.Generic.List<string>();
            Action<string> observe=original=>
            {
                if(sampled || original==null || !original.StartsWith("OWNER_INVALIDATION ownerIntent=actual-exit ",StringComparison.Ordinal))return;
                sampled=true;
                try{record="TWOX_OWNER_INVALIDATION frame="+Time.frameCount+" sceneTime="+Time.time+"\n"+original;hook.SetValue(core,previous);detached=true;}
                catch(Exception error){errors.Add(error.ToString());}
            };
            // Preserve the exact old subscriber and its original WAIT_ORIGINAL behavior.
            var installIdentity="UNKNOWN";try{var installCore=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");installIdentity="captured="+StartupIdentity(core)+" current="+StartupIdentity(installCore)+" same="+ReferenceEquals(core,installCore);}catch(Exception error){errors.Add("identity install: "+error);}
            var installed=(Action<string>)Delegate.Combine(observe,previous);hook.SetValue(core,installed);
            return ()=>
            {
                if(finished)return;finished=true;
                try
                {
                    if(!ReferenceEquals(hook.GetValue(core),detached?(object)previous:installed))errors.Add("WaitFailureObserver changed externally before cleanup");
                    hook.SetValue(core,previous);
                }
                catch(Exception error){errors.Add("restore: "+error);}
                try
                {
                    var flushIdentity="UNKNOWN";try{var flushCore=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");flushIdentity="captured="+StartupIdentity(core)+" current="+StartupIdentity(flushCore)+" same="+ReferenceEquals(core,flushCore);}catch(Exception error){errors.Add("identity flush: "+error);}TestContext.WriteLine("TWOX_OWNER_INVALIDATION_CORE install=["+installIdentity+"] flush=["+flushIdentity+"]");
                    TestContext.WriteLine("TWOX_OWNER_INVALIDATION_WITNESS status="+(errors.Count>0?"UNKNOWN":sampled?"OBSERVED":"NOT_PROVEN")+" cap=1 errors="+errors.Count);
                    if(record!=null)TestContext.WriteLine(record);
                    foreach(var error in errors)TestContext.WriteLine("TWOX_OWNER_INVALIDATION_ERROR "+error);
                }
                catch(Exception){} // Do not replace the original business assertion with a logger failure.
            };
        }
        // Default-off, first original refusal only; do not replay PrepareLeg or any progress/query operation.
        // 只复制原拒绝现场；callback 捕获错误，finally/Cleanup 在强制清理后可靠输出。
        private Action ObserveOneXPrepareLegRejection(string witnessTag="ONEX")
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var hook=typeof(ServiceTrafficCoordinator).GetField("PrepareLegRejectionObserver",flags);
            var previous=hook.GetValue(core);Assert.That(previous,Is.Null,witnessTag+" witness must not replace another active subscriber");
            var records=new System.Collections.Generic.List<string>();var errors=new System.Collections.Generic.List<string>();var sampled=false;var finished=false;
            Action<string> observe=original=>
            {
                if(sampled)return;sampled=true;
                try
                {
                    records.Add(original);
                    string Word(string name){var prefix=" "+name+"=";var start=original.IndexOf(prefix,StringComparison.Ordinal);if(start<0)return "UNKNOWN";start+=prefix.Length;var end=original.IndexOf(' ',start);return end<0?original.Substring(start):original.Substring(start,end-start);}
                    string Ref(object value)=>StartupIdentity(value);
                    string Members(object value,params string[] names)=>StartupMembers(value,names);
                    var actorId=Word("actor");var entries=(IDictionary)StartupRead(core,"entries");var matches=0;
                    records.Add("ONEX_PREPARE_LEG_GATE frame="+Time.frameCount+" core="+Ref(core)+" "+Members(core,"paused","closed","committing","epoch","layoutRevision")+
                        " ownerCanProgress="+owner.CanProgress+" comboReady="+StartupValue(StartupRead(owner,"comboReady"))+" ownerHeld="+StartupValue(StartupRead(owner,"held"))+
                        " time="+time.CurrentSpeed+" resumeBlocked="+time.IsResumeBlocked+" snapshot="+Members(StartupRead(core,"snapshot"),"Version","BodyVersion"));
                    foreach(DictionaryEntry pair in entries)
                    {
                        var key=(TrafficIntentKey)pair.Key;
                        // String fields only locate diagnostic data; they never authorize or alter production behavior.
                        if(key.ActorId!=actorId || key.IntentId!=Word("intent") || key.Generation.ToString()!=Word("phaseGen") || key.Context!=Word("context") || key.Epoch.ToString()!=Word("epoch") || key.LayoutRevision.ToString()!=Word("revision"))continue;
                        matches++;var entry=pair.Value;var leg=StartupRead(entry,"Leg");var request=StartupRead(entry,"Request");var journey=StartupRead(entry,"JourneyRequest");
                        records.Add("ONEX_PREPARE_LEG_ENTRY key="+StartupValue(key)+" ref="+Ref(entry)+" "+Members(entry,"FirstFrame","RequestSequence","AcquiredSequence","Queries","Queued","Prepared","Active","Retired","Parking","WaitRemaining","FiniteLimit")+
                            " decision="+StartupValue(StartupRead(entry,"Decision"))+" JourneyPermit="+Ref(StartupRead(entry,"JourneyPermit")));
                        records.Add("ONEX_PREPARE_LEG_CURRENT ref="+Ref(leg)+" "+Members(leg,"Index","Generation","Bound","Committed","Completed","NeedsQuery","Suspended","ReprepareAllowed")+
                            " stamp="+Members(StartupRead(leg,"Stamp"),"RequestId","LegGeneration")+" ActorLife="+Ref(StartupRead(leg,"ActorLife"))+" proof="+Ref(StartupRead(leg,"Proof"))+" resource="+StartupValue(StartupRead(StartupRead(leg,"Proof"),"ResourceId")));
                        foreach(var item in new[]{request,journey})records.Add("ONEX_PREPARE_LEG_REQUEST ref="+Ref(item)+" isJourney="+ReferenceEquals(item,journey)+" "+Members(item,"Intent","RequestedFrame","ResourceId","Start","Target")+
                            " stamp="+Members(StartupRead(item,"NativeStamp"),"RequestId","LegGeneration")+" snapshot="+Ref(StartupRead(item,"Snapshot"))+" "+Members(StartupRead(item,"Snapshot"),"Version","BodyVersion"));
                        foreach(var phase in new[]{"originalStrict","currentCore"})
                        {
                            var snapshot=phase=="originalStrict"?(request as TrafficRequest)?.Snapshot:StartupRead(core,"snapshot") as TrafficWorldSnapshot;
                            records.Add("ONEX_PREPARE_LEG_SNAPSHOT phase="+phase+" ref="+Ref(snapshot)+" "+Members(snapshot,"Version","BodyVersion")+" bodies="+snapshot?.Bodies.Count+" parking="+snapshot?.Parking.Count);
                            if(snapshot==null)continue;
                            foreach(var body in snapshot.Bodies)
                                records.Add("ONEX_PREPARE_LEG_BODY phase="+phase+" fact="+Ref(body)+" actorLife="+Ref(body.ActorLife)+" "+Members(body,"Owner","Position","Radius","SafetyMargin","Enabled","ActorActive","IndependentlyProgressing","RemainingSeconds"));
                            foreach(var parking in snapshot.Parking)
                                records.Add("ONEX_PREPARE_LEG_PARKING phase="+phase+" fact="+Ref(parking)+" canonical="+Ref(parking.CanonicalOwner)+" business="+Ref(parking.BusinessOwner)+" release="+Ref(parking.ReleaseObligation)+
                                    " semantic="+Ref(parking.SemanticCheckpoint)+" "+Members(parking,"Owner","OwnerVersion","ResourceId","Position","Radius","SafetyMargin"));
                        }
                    }
                    records.Add("ONEX_PREPARE_LEG_MATCHES count="+matches);var rowCount=0;
                    foreach(var row in (IEnumerable)StartupRead(employees,"employees"))
                    {
                        if(!Equals(StartupRead(row,"Id"),actorId))continue;
                        rowCount++;var runner=StartupRead(row,"Runner");var port=StartupRead(row,"Port");var move=StartupRead(port,"trafficMove");var nativeLeg=StartupRead(runner,"moveLeg");var actor=StartupRead(row,"Actor") as NavigationActor;
                        records.Add("ONEX_PREPARE_LEG_EMPLOYEE ref="+Ref(row)+" "+Members(row,"Id","Role","Failure","NeedsBinding","SpawnAttempted")+" binding="+Ref(StartupRead(row,"Binding"))+" "+Members(StartupRead(row,"Binding"),"LayoutRevision","WorkStationId","PickUpId")+
                            " actor="+Ref(actor)+" active="+actor?.isActiveAndEnabled+" agentEnabled="+actor?.Agent?.enabled+" pose="+(actor==null?"UNKNOWN":actor.transform.position.ToString("F9")));
                        records.Add("ONEX_PREPARE_LEG_RUNNER ref="+Ref(runner)+" "+Members(runner,"state","generation","layoutRevision","handleId","disposed","retiring","PickupTrafficContext","deliveryRouteIndex","returnRouteIndex","trafficSelectionVersion")+
                            " pendingArrival="+StartupValue(StartupRead(runner,"pendingArrival"))+" pendingArrivalFacts="+Members(StartupRead(runner,"pendingArrival"),"RequestId","ActorId","Status","Reason","FinalPosition")+
                            " moveLeg="+Ref(nativeLeg)+" "+Members(nativeLeg,"State","Generation","LayoutRevision","NativeLegGeneration","RequestId","AwaitingTraffic","Completed","TrafficIntermediate"));
                        records.Add("ONEX_PREPARE_LEG_PORT ref="+Ref(port)+" "+Members(port,"trafficState","movementLife","workMoveSequence")+" movement="+Ref(move)+" "+Members(move,"Intent","Resource","Index","NativeGeneration","Suspended")+
                            " Source="+Ref(StartupRead(move,"Source"))+" sourceFacts="+Members(StartupRead(move,"Source"),"Intent","Kind","Reason","RequestSequence","AcquiredSequence")+
                            " PendingSource="+Ref(StartupRead(move,"PendingSource"))+" Proof="+Ref(StartupRead(move,"Proof"))+" proofResource="+StartupValue(StartupRead(StartupRead(move,"Proof"),"ResourceId"))+" preparationContext="+Ref(StartupRead(move,"PreparationContext")));
                    }
                    records.Add("ONEX_PREPARE_LEG_EMPLOYEE_MATCHES count="+rowCount);
                }
                catch(Exception error){errors.Add(error.ToString());}
            };
            hook.SetValue(core,observe);
            return ()=>
            {
                if(finished)return;finished=true;
                try{hook.SetValue(core,previous);}catch(Exception error){errors.Add("restore: "+error);}
                TestContext.WriteLine(witnessTag+"_PREPARE_LEG_WITNESS status="+(sampled?"OBSERVED":"NOT_OBSERVED")+" cap=1 errors="+errors.Count);
                foreach(var record in records)TestContext.WriteLine(record.StartsWith("ONEX_PREPARE_LEG_",StringComparison.Ordinal)?witnessTag+record.Substring(4):record);
                foreach(var error in errors)TestContext.WriteLine(witnessTag+"_PREPARE_LEG_ERROR "+error);
                Assert.That(errors,Is.Empty,"Original rejection witness errors; no policy callback assertion");
            };
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
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,3000);
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
            customers.SetIntegratedMode(true);
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,3000);
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            yield return null; customers.enabled=false; customers.enabled=true;
            var before=NextRequestId(); customers.Step(0);
            Assert.That(NextRequestId(),Is.EqualTo(before),"Zero game time cannot submit a new ordinary movement");
        }
        [UnityTest] public IEnumerator PausedResumeCannotReleaseCustomerHoldOrSubmitMovement()
        {
            yield return PreparePausedResumeAdmission();
            customers.SetIntegratedMode(true); Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            yield return null; customers.HoldIntegratedVisits(); time.SetPaused();
            var before=NextRequestId(); customers.ResumeBlockedVisits(); owner.ResumeBlockedFlow();
            Assert.That(NextRequestId(),Is.EqualTo(before),"Paused resume must keep owned movement retired");
        }
        [UnityTest] public IEnumerator InitialEmployeeFailureRetriesOnlyOnExplicitIntegratedResume()
        {
            var restoreObservation=ObserveInitialEmployeeSnapshotFailure();
            initialSnapshotObservation=restoreObservation;
            try
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
            LogInitialMovementResumeCheckpoint("before");
            owner.ResumeBlockedFlow();
            LogInitialMovementResumeCheckpoint("after");
            yield return Until(()=>employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(owner.CanProgress,Is.True); Assert.That(world.RegisteredActors.Count,Is.EqualTo(2));
            }
            finally { restoreObservation(); }
        }
        private long NextRequestId() => (long)typeof(NavigationService).GetField("nextId",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(world.Service);
        private T Find<T>() where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
        private static NavigationActor Prefab(string species) => AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Art/Phase13/Employees/{species}/PF_{species}_Employee.prefab").GetComponent<NavigationActor>();
        // Prime once, then let only the ordinary owner frames prepare this exact admission.
        // 原 TrySpawn assertion 保留首次 canonical admission，不在准备阶段循环提交。
        private IEnumerator PreparePausedResumeAdmission()
        {
            var beforeVisit=StartupRead(customers,"nextVisit");var beforeCount=customers.Snapshot.Count;
            var primed=customers.TrySpawn();
            var anchor=StartupRead(customers,"pendingAdmissionAnchor");
            var coordinator=StartupRead(anchor,"Coordinator") as ServiceTrafficCoordinator;
            var intent=StartupRead(anchor,"Key");
            Assert.That(primed,Is.False,"Preparation must leave the original admission uncommitted");
            Assert.That(anchor,Is.Not.Null,"Preparation must retain the actual pending admission anchor");
            Assert.That(coordinator,Is.Not.Null,"Preparation requires the anchor's actual Core");
            Assert.That(intent,Is.TypeOf<TrafficIntentKey>());
            var key=(TrafficIntentKey)intent;
            bool SameProposal()=>ReferenceEquals(StartupRead(customers,"pendingAdmissionAnchor"),anchor) &&
                ReferenceEquals(StartupRead(customers,"traffic"),coordinator) &&
                StartupRead(anchor,"Key") is TrafficIntentKey current && current.Equals(key);
            for(var frame=0;frame<600;frame++)
            {
                if(!SameProposal())break;
                var decision=coordinator.GetDecision(key);
                if(decision.Kind==TrafficDecisionKind.Granted)break;
                if(decision.Kind!=TrafficDecisionKind.DeferredByBudget && decision.Kind!=TrafficDecisionKind.WaitingForTraffic)break;
                yield return null;
            }
            var actual=coordinator.GetDecision(key);
            Debug.Log("[PAUSED_RESUME_ADMISSION_PREPARATION] sameProposal="+SameProposal()+
                " anchor="+StartupIdentity(anchor)+" core="+StartupIdentity(coordinator)+" key="+StartupValue(key)+
                " decision="+StartupValue(actual)+" canonicalVisits="+customers.Snapshot.Count);
            Assert.That(SameProposal(),Is.True,"The original admission context changed; preparation must not replace its key");
            Assert.That(actual.Kind,Is.EqualTo(TrafficDecisionKind.Granted),"Normal frames must grant the original pending proposal within 600 frames");
            Assert.That(customers.Snapshot.Count,Is.EqualTo(beforeCount),"Only the original TrySpawn assertion may commit admission");
            Assert.That(StartupRead(customers,"nextVisit"),Is.EqualTo(beforeVisit));
        }

        private void LogInitialMovementResumeCheckpoint(string label)
        {
            if(TestContext.CurrentContext.Test.Name!="InitialEmployeeFailureRetriesOnlyOnExplicitIntegratedResume" ||
                label!="before" && label!="after")return;
            var log=new System.Text.StringBuilder("[INITIAL_MOVEMENT_RESUME_CHECKPOINT] label="+label+" frame="+Time.frameCount);
            AppendInitialMovementResumeFacts(log,null,null,null,null);
            try{TestContext.WriteLine(log.ToString());}catch(Exception){}
        }
        // Read the already-retained production seal and pure getters; never capture or refresh a replacement.
        // 读取本次实际保存的 ctor 身份；不追补旧 E80 未保存的 producer seal。
        private void AppendInitialMovementResumeFacts(System.Text.StringBuilder log,string actorId,object observedCore,
            TrafficRequest observedRequest,TrafficWorldSnapshot suppliedSnapshot)
        {
            try
            {
                var core=observedCore??StartupRead(customers,"traffic");var ownerCore=StartupRead(owner,"traffic");
                var cfcSnapshot=StartupRead(customers,"trafficSnapshot");var coreSnapshot=StartupRead(core,"snapshot") as TrafficWorldSnapshot;
                log.AppendLine().Append("ResumeGate.Owner ref="+StartupIdentity(owner)+" "+StartupMembers(owner,"CanProgress","held","comboReady","trafficEpoch","trafficRevision")+
                    " ownerCore="+StartupIdentity(ownerCore)+" observedCore="+StartupCore(core)+
                    " coreSameOwner="+ReferenceEquals(core,ownerCore)+" coreSameCFC="+ReferenceEquals(core,StartupRead(customers,"traffic")));
                log.AppendLine().Append("ResumeGate.CFC "+StartupMembers(customers,"integratedMode","integratedHeld","Configured","CanIntegratedProgress",
                    "admissionInProgress","preparingTrafficProposals","disposed","trafficSession","trafficEpoch")+
                    " queueRef="+StartupIdentity(StartupRead(customers,"queue"))+" snapshotRef="+StartupIdentity(cfcSnapshot)+
                    " snapshot="+StartupMembers(cfcSnapshot,"Version","BodyVersion")+" snapshotSameCore="+ReferenceEquals(cfcSnapshot,coreSnapshot)+
                    " suppliedSnapshotRef="+StartupIdentity(suppliedSnapshot));
                log.AppendLine().Append("ResumeGate.Availability world="+StartupMembers(world,"isActiveAndEnabled","LayoutAvailable","currentRevision")+
                    " adapter="+StartupMembers(adapter,"isActiveAndEnabled","Revision","PublishedRevision")+
                    " time="+StartupMembers(time,"CurrentSpeed","IsResumeBlocked","ResumeBlockReason")+
                    " decoration="+StartupMembers(StartupRead(customers,"decoration"),"IsOpen")+
                    " EFC="+StartupMembers(employees,"session","integratedMode","disposed","isActiveAndEnabled","observedRevision"));
                AppendInitialTrafficSnapshot(log,"ResumeGate.currentCoreSnapshot",coreSnapshot);
                var roster=StartupRead(employees,"employees") as IEnumerable;object row=null;
                if(roster!=null)foreach(var candidate in roster)
                {
                    if(actorId==null?StartupRead(candidate,"Role") is EmployeeRole role && role==EmployeeRole.Barista:
                        StartupRead(candidate,"Id") is string id && id==actorId){row=candidate;break;}
                }
                var port=StartupRead(row,"Port");var runner=StartupRead(row,"Runner");var movement=StartupRead(port,"trafficMove");
                var binding=StartupRead(row,"Binding");var leg=StartupRead(runner,"moveLeg");var actor=StartupRead(row,"Actor") as NavigationActor;
                var context=StartupRead(movement,"PreparationContext");var stamp=StartupRead(context,"stamp");
                var pending=StartupRead(movement,"PendingSource");var intent=StartupRead(movement,"Intent");
                log.AppendLine().Append("ResumeGate.Row ref="+StartupIdentity(row)+" "+StartupMembers(row,"Id","Role","NeedsBinding","SpawnAttempted","Failure")+
                    " portRef="+StartupIdentity(port)+" runnerRef="+StartupIdentity(runner)+" bindingRef="+StartupIdentity(binding)+" legRef="+StartupIdentity(leg));
                log.AppendLine().Append("ResumeGate.Movement ref="+StartupIdentity(movement)+" "+StartupMembers(movement,"Intent","PreparationFrame","PreparationOrigin","Resource","Index","NativeGeneration","Suspended")+
                    " coordinatorRef="+StartupIdentity(StartupRead(movement,"Coordinator"))+" source="+StartupValue(StartupRead(movement,"Source"))+
                    " pendingRef="+StartupIdentity(pending)+" pending="+StartupValue(pending));
                log.AppendLine().Append("ResumeGate.RetainedProductionSeal ref="+StartupIdentity(context)+
                    " captured="+(context==null || context is string?"UNKNOWN(no-retained-context)":"ACTUAL_PRODUCTION_CONTEXT")+
                    " "+StartupMembers(context,"IsCurrent","Key","FirstFrame","Origin","Radius","SafetyMargin","Resource")+
                    " goal="+StartupMembers(StartupRead(context,"Goal"),"Position","Facing"));
                log.AppendLine().Append("ResumeGate.ContextCtor owner="+StartupIdentity(StartupRead(context,"owner"))+
                    " row="+StartupIdentity(StartupRead(context,"employee"))+" port="+StartupIdentity(StartupRead(context,"port"))+
                    " runner="+StartupIdentity(StartupRead(context,"runner"))+" actor="+StartupIdentity(StartupRead(context,"actor"))+
                    " binding="+StartupIdentity(StartupRead(context,"binding"))+" movement="+StartupIdentity(StartupRead(context,"movement"))+
                    " world="+StartupIdentity(StartupRead(context,"world"))+" adapter="+StartupIdentity(StartupRead(context,"adapter"))+
                    " customers="+StartupIdentity(StartupRead(context,"customers"))+" "+StartupMembers(context,"session","role","skin","epsilon"));
                if(context!=null && !(context is string))log.AppendLine().Append("ResumeGate.ContextRefs rowSame="+ReferenceEquals(StartupRead(context,"employee"),row)+
                    " ownerSame="+ReferenceEquals(StartupRead(context,"owner"),employees)+" portSame="+ReferenceEquals(StartupRead(context,"port"),port)+
                    " runnerSame="+ReferenceEquals(StartupRead(context,"runner"),runner)+" actorSame="+ReferenceEquals(StartupRead(context,"actor"),actor)+
                    " bindingSame="+ReferenceEquals(StartupRead(context,"binding"),binding)+" movementSame="+ReferenceEquals(StartupRead(context,"movement"),movement)+
                    " worldSame="+ReferenceEquals(StartupRead(context,"world"),world)+" adapterSame="+ReferenceEquals(StartupRead(context,"adapter"),adapter));
                else log.AppendLine().Append("ResumeGate.ContextRefs=UNKNOWN(no-retained-context)");
                log.AppendLine().Append("ResumeGate.Runner "+StartupMembers(runner,"state","failure","generation","layoutRevision","handleId","trafficSelectionVersion",
                    "hasCheckpoint","integrated","disposed","retiring","suspendedMove","pendingArrival")+
                    " bindingRef="+StartupIdentity(StartupRead(runner,"binding"))+" environmentRef="+StartupIdentity(StartupRead(runner,"environment"))+
                    " leg="+StartupMembers(leg,"Generation","LayoutRevision","NativeLegGeneration","RequestId","AwaitingTraffic","Completed","State")+
                    " binding="+StartupMembers(binding,"Role","LayoutRevision","WorkStationId","PickUpId"));
                log.AppendLine().Append("ResumeGate.RunnerStamp ref="+StartupIdentity(stamp)+" "+StartupMembers(stamp,"IsCurrent","hasCheckpoint","integrated","generation",
                    "revision","nativeGeneration","handle","selection","state")+
                    " ownerRef="+StartupIdentity(StartupRead(stamp,"owner"))+" bindingRef="+StartupIdentity(StartupRead(stamp,"binding"))+
                    " environmentRef="+StartupIdentity(StartupRead(stamp,"environment"))+" legRef="+StartupIdentity(StartupRead(stamp,"leg"))+
                    " legSame="+(stamp==null || stamp is string?"UNKNOWN":ReferenceEquals(StartupRead(stamp,"leg"),leg).ToString())+
                    " bindingSame="+(stamp==null || stamp is string?"UNKNOWN":ReferenceEquals(StartupRead(stamp,"binding"),binding).ToString())+
                    " environmentSame="+(stamp==null || stamp is string?"UNKNOWN":ReferenceEquals(StartupRead(stamp,"environment"),port).ToString())+
                    " target="+StartupMembers(StartupRead(stamp,"target"),"Position","Facing"));
                log.AppendLine().Append("ResumeGate.Actor life="+StartupIdentity(actor)+" "+StartupMembers(actor,"ActorId","isActiveAndEnabled")+
                    " pose="+(actor==null?"UNKNOWN":actor.transform.position.ToString("F9"))+
                    " registered="+(actor==null || world==null?"UNKNOWN":world.RegisteredActors.Any(a=>ReferenceEquals(a,actor)).ToString())+
                    " settings="+StartupMembers(StartupRead(actor,"Settings"),"AgentRadius","CollisionSkin","Epsilon"));
                var entries=StartupRead(core,"entries") as IDictionary;
                var entryIntent=observedRequest==null?intent:(object)observedRequest.Intent;
                var entry=entries!=null && entryIntent is TrafficIntentKey key && entries.Contains(key)?entries[key]:null;
                var request=StartupRead(entry,"Request") as TrafficRequest;var requestSnapshot=request?.Snapshot;
                log.AppendLine().Append("ResumeGate.Entry ref="+StartupIdentity(entry)+" "+StartupMembers(entry,"FirstFrame","Queries","RequestSequence","AcquiredSequence",
                    "PreparationIdentityCaptured","Queued","Prepared","Active","Retired","Discarded","EverCanonicalAttempt","ExplicitRecoveryUsed")+
                    " decisionRef="+StartupIdentity(StartupRead(entry,"Decision"))+" decision="+StartupValue(StartupRead(entry,"Decision"))+
                    " requestRef="+StartupIdentity(request)+" observedRequestRef="+StartupIdentity(observedRequest)+
                    " requestSnapshotRef="+StartupIdentity(requestSnapshot)+" requestSnapshot="+StartupMembers(requestSnapshot,"Version","BodyVersion")+
                    " requestVersionsMatchCore="+(requestSnapshot==null || coreSnapshot==null?"UNKNOWN":
                        (requestSnapshot.Version==coreSnapshot.Version && requestSnapshot.BodyVersion==coreSnapshot.BodyVersion).ToString()));
                var body=StartupRead(entry,"PreparationBody");
                log.AppendLine().Append("ResumeGate.PreparationBody ref="+StartupIdentity(body)+" owner="+StartupValue(StartupRead(body,"Owner"))+
                    " life="+StartupIdentity(StartupRead(body,"ActorLife"))+" "+StartupMembers(body,"Position","Radius","SafetyMargin","Enabled","ActorActive","IndependentlyProgressing","RemainingSeconds"));
                var claims=StartupRead(entry,"PreparationClaims") as IEnumerable;var claimCount=0;
                if(claims!=null)foreach(var claim in claims)
                {
                    claimCount++;log.AppendLine().Append("ResumeGate.PreparationClaim ref="+StartupIdentity(claim)+" owner="+StartupValue(StartupRead(claim,"Owner"))+
                        " canonical="+StartupIdentity(StartupRead(claim,"CanonicalOwner"))+" business="+StartupIdentity(StartupRead(claim,"BusinessOwner"))+
                        " release="+StartupIdentity(StartupRead(claim,"ReleaseObligation"))+" "+StartupMembers(claim,"OwnerVersion","ResourceId","Position","Radius","SafetyMargin"));
                }
                log.AppendLine().Append("ResumeGate.PreparationClaims count="+(claims==null?"UNKNOWN":claimCount.ToString()));
            }
            catch(Exception error){log.AppendLine().Append("ResumeGate=UNKNOWN("+error.GetType().Name+":"+error.Message+")");}
        }
        private void FlushInitialEmployeeSnapshotObservation()
        {
            var flush=initialSnapshotObservation;initialSnapshotObservation=null;
            try{flush?.Invoke();}
            catch(Exception error)
            {
                try{TestContext.WriteLine("[INITIAL_EMPLOYEE_SNAPSHOT_CHANGED] status=UNKNOWN flush=UNKNOWN("+error.GetType().Name+":"+error.Message+")");}
                catch(Exception){}
            }
        }
        // Subscribe only to the existing original failure seam; restoring it cannot replace a test failure.
        // 只捕获第一次 employee-work 原 Fail 前事实，不刷新、不准备、不改变任何生产上下文。
        private Action ObserveInitialEmployeeSnapshotFailure(string intentKind="employee-work")
        {
            var coordinator=StartupRead(customers,"traffic") as ServiceTrafficCoordinator;
            var counterWaitFlush=Environment.GetCommandLineArgs().Contains("-trafficCounterAdvanceWaitSnapshot")?ObserveOriginalCounterAdvanceWait(coordinator):null;
            var hook=typeof(ServiceTrafficCoordinator).GetField("SnapshotChangedObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var tag=intentKind=="employee-work"?"[INITIAL_EMPLOYEE_SNAPSHOT_CHANGED]":"[PAID_PICKUP_SNAPSHOT_CHANGED]";
            object previous=null;var installed=false;var observed=false;var errors="";string capture=null;
            var restored=false;var emitted=false;
            try
            {
                if(coordinator==null || hook==null)throw new InvalidOperationException("original Core/observer unavailable");
                previous=hook.GetValue(coordinator);
                var prior=previous as Action<TrafficRequest,TrafficWorldSnapshot>;
                Action<TrafficRequest,TrafficWorldSnapshot> witness=(request,current)=>
                {
                    if(!observed && request!=null && request.Intent.IntentId==intentKind)
                    {
                        observed=true;
                        try{capture=CaptureInitialEmployeeSnapshotFailure(coordinator,request,current);}
                        catch(Exception error){errors+=" capture=UNKNOWN("+error.GetType().Name+":"+error.Message+")";}
                    }
                    try{prior?.Invoke(request,current);}
                    catch(Exception error){errors+=" previousObserver=UNKNOWN("+error.GetType().Name+")";}
                };
                hook.SetValue(coordinator,witness);installed=true;
            }
            catch(Exception error){errors+=" install=UNKNOWN("+error.GetType().Name+":"+error.Message+")";}
            return ()=>
            {
                if(emitted)return;emitted=true;
                counterWaitFlush?.Invoke();
                if(!restored)
                {
                    restored=true;
                    try{if(installed)hook.SetValue(coordinator,previous);}
                    catch(Exception error){errors+=" restore=UNKNOWN("+error.GetType().Name+")";}
                }
                try{TestContext.WriteLine(tag+" status="+(observed?"OBSERVED":installed?"NOT_OBSERVED":"UNKNOWN")+
                    " observerCore="+StartupIdentity(coordinator)+" coreStillCurrent="+ReferenceEquals(StartupRead(customers,"traffic"),coordinator)+
                    " savedOriginalProducerSeal=UNKNOWN(no-original-producer-capture)"+errors+"\n"+capture);}
                catch(Exception error)
                {
                    try{Debug.Log(tag+" status=UNKNOWN write=UNKNOWN("+error.GetType().Name+")");}
                    catch(Exception){}
                }
            };
        }
        // Default-off cap1, inside ORIGINAL WaitOrFail refusal, before Fail destroys the live owner/handoff facts.
        // 只读保存原Entry/leg/receipt/queue；不查询、不重跑guard、不notify或推进，原拒绝照常执行。
        private Action ObserveOriginalCounterAdvanceWait(ServiceTrafficCoordinator coordinator)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var hook=typeof(ServiceTrafficCoordinator).GetField("WaitFailureObserver",flags);
            var prior=hook?.GetValue(coordinator) as Action<string>;var observed=false;var emitted=false;string captured=null;string error=null;
            string Key(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
            string Points(object value)=>value is System.Collections.Generic.IEnumerable<Vector3> points?string.Join(";",points.Select(p=>p.ToString("F9"))):StartupIdentity(value);
            Action<string> observer=original=>
            {
                if(observed || original==null || !original.StartsWith("WAIT_ORIGINAL condition=owner-not-independent ",StringComparison.Ordinal))return;
                try
                {
                    var table=StartupRead(coordinator,"entries") as IDictionary;if(table==null)throw new InvalidOperationException("original entries unavailable");
                    var rows=table.Keys.Cast<object>().Select(key=>new DictionaryEntry(key,table[key])).ToArray();
                    var follower=rows.FirstOrDefault(row=>original.Contains(" intent="+Key((TrafficIntentKey)row.Key)+" "));
                    var predecessor=rows.FirstOrDefault(row=>original.Contains(" owner="+Key((TrafficIntentKey)row.Key)+" "));
                    if(!(follower.Key is TrafficIntentKey incoming) || !(predecessor.Key is TrafficIntentKey ownerKey) ||
                        incoming.IntentId!="counter-advance" || ownerKey.IntentId!="counter-advance")return;
                    observed=true;
                    var log=new System.Text.StringBuilder("COUNTER_ADVANCE_WAIT_ORIGINAL cap=1 frame="+Time.frameCount+" sceneTime="+Time.time+" original="+original);
                    log.AppendLine().Append("FOLLOWER ref="+StartupIdentity(follower.Value)+" "+StartupMembers(follower.Value,"FirstFrame","Queries","RequestSequence","AcquiredSequence","Queued","Prepared","Active","Retired","WaitRemaining","FiniteLimit")+
                        " request="+StartupMembers(StartupRead(follower.Value,"Request"),"Start","Target","ResourceId","RequestedFrame")+
                        " frozenResources="+StartupIdentity(StartupRead(follower.Value,"FrozenResources"))+" decision="+StartupValue(StartupRead(follower.Value,"Decision")));
                    foreach(var row in rows.Where(row=>((TrafficIntentKey)row.Key).ActorId==ownerKey.ActorId && ((TrafficIntentKey)row.Key).IntentId=="counter-advance"))
                    {
                        var entry=row.Value;var leg=StartupRead(entry,"Leg");var handoff=StartupRead(entry,"PendingHandoff");var dependencies=StartupRead(handoff,"Dependencies") as IDictionary;
                        var wakeups=StartupRead(handoff,"CompletedBodyWakeups") as IEnumerable;
                        log.AppendLine().Append("OWNER_CHAIN key="+Key((TrafficIntentKey)row.Key)+" ref="+StartupIdentity(entry)+" originalBlocker="+Equals(row.Key,ownerKey)+" "+StartupMembers(entry,"FirstFrame","Queries","RequestSequence","AcquiredSequence","Queued","Prepared","Active","Retired","WaitRemaining","FiniteLimit")+
                            " decision="+StartupValue(StartupRead(entry,"Decision"))+" journeyPermit="+StartupValue(StartupRead(entry,"JourneyPermit"))+
                            " request="+StartupMembers(StartupRead(entry,"Request"),"Start","Target","ResourceId","RequestedFrame")+
                            " current=["+Points(StartupRead(entry,"CurrentPath"))+"] journey=["+Points(StartupRead(entry,"Journey"))+"]");
                        log.AppendLine().Append("OWNER_LEG ref="+StartupIdentity(leg)+" "+StartupMembers(leg,"Index","Generation","Committed","Bound","Completed","Suspended","NeedsQuery","ReprepareAllowed","Stamp")+
                            " actorLife="+StartupIdentity(StartupRead(leg,"ActorLife"))+" path=["+Points(StartupRead(leg,"Path"))+"] suffix=["+Points(StartupRead(leg,"Suffix"))+"]");
                        log.AppendLine().Append("OWNER_HANDOFF ref="+StartupIdentity(handoff)+" "+StartupMembers(handoff,"Remaining","CheckpointKind","PreparedSuccessor","InspectingPreparation","CommittingPreparedSuccessor")+
                            " source="+StartupValue(StartupRead(handoff,"Source"))+" actorLife="+StartupIdentity(StartupRead(handoff,"ActorLife"))+
                            " dependenciesCount="+(dependencies==null?"UNKNOWN":dependencies.Count.ToString())+" containsOriginalFollower="+(dependencies==null?"UNKNOWN":dependencies.Contains(follower.Value).ToString())+
                            " followerResources="+(dependencies!=null && dependencies.Contains(follower.Value)?string.Join(";",((IEnumerable)dependencies[follower.Value]).Cast<object>()):"NONE")+
                            " completedBodyWakeupContainsFollower="+(wakeups==null?"UNKNOWN":wakeups.Cast<object>().Any(value=>ReferenceEquals(value,follower.Value)).ToString()));
                    }
                    var visits=StartupRead(customers,"visits") as IDictionary;var visit=visits!=null && visits.Contains(ownerKey.ActorId)?visits[ownerKey.ActorId]:null;
                    var move=StartupRead(visit,"TrafficMove");var pending=StartupRead(visit,"PendingTraffic");var actor=StartupRead(visit,"Actor") as NavigationActor;
                    log.AppendLine().Append("CFC_PRIOR "+StartupMembers(visit,"State","Generation","CounterArrived","QueueLeft","HasCollected","RequestId","OwnedIssuedRequest","OwnedIssuedGeneration","TrafficMovePending","PendingCounter")+
                        " actorLife="+StartupIdentity(actor)+" pose="+(actor==null?"UNKNOWN":actor.transform.position.ToString("F9"))+
                        " currentCounterRows="+string.Join(";",customers.CounterAssignments.Where(row=>row.VisitId==ownerKey.ActorId).Select(row=>row.VisitId+"/g"+row.Generation+"/slot"+row.SlotIndex+"/arrived="+row.Arrived+"/"+row.Position.ToString("F9"))));
                    log.AppendLine().Append("CFC_PRIOR_MOVE ref="+StartupIdentity(move)+" "+StartupMembers(move,"Intent","Index","NativeGeneration","Suspended")+" source="+StartupValue(StartupRead(move,"Source"))+
                        " pendingRef="+StartupIdentity(pending)+" "+StartupMembers(pending,"Intent","NativeGeneration","Marked","Kind","Handle")+" pendingSource="+StartupValue(StartupRead(pending,"Source")));
                    var counterRows=StartupRead(StartupRead(customers,"queue"),"entries") as IEnumerable;
                    var actualRow=counterRows?.Cast<object>().FirstOrDefault(row=>Equals(StartupRead(row,"Id"),ownerKey.ActorId));
                    log.AppendLine().Append("QUEUE_PRIOR_ENTRY "+StartupMembers(actualRow,"Id","Generation","Slot","Position","Arrived","Moving","NeedsReplan"));
                    var byActor=StartupRead(world.Service,"byActor") as IDictionary;var native=byActor!=null && byActor.Contains(ownerKey.ActorId)?byActor[ownerKey.ActorId]:null;
                    log.AppendLine().Append("ACTUAL_PRIOR_NATIVE ref="+StartupIdentity(native)+" "+StartupMembers(native,"Id","ActorId","Generation","LayoutRevision","NoProgressSeconds","SegmentSeconds","SegmentLimit","RetryCount","Recovering"));
                    captured=log.ToString();
                }
                catch(Exception failure){observed=true;error=failure.ToString();}
            };
            var installed=(Action<string>)Delegate.Combine(observer,prior);if(hook!=null && coordinator!=null)hook.SetValue(coordinator,installed);
            return ()=>
            {
                if(emitted)return;emitted=true;
                try{if(hook!=null && coordinator!=null && ReferenceEquals(hook.GetValue(coordinator),installed))hook.SetValue(coordinator,prior);else error+=" UNKNOWN observer lifecycle/replacement";}
                catch(Exception failure){error+=" restore="+failure;}
                TestContext.WriteLine("COUNTER_ADVANCE_WAIT_SNAPSHOT status="+(observed?error==null?"OBSERVED":"UNKNOWN":"NOT_OBSERVED")+" originalGuardReevaluated=False queryAdded=0 "+error+"\n"+captured);
            };
        }
        private string CaptureInitialEmployeeSnapshotFailure(ServiceTrafficCoordinator coordinator,TrafficRequest request,TrafficWorldSnapshot current)
        {
            var entries=StartupRead(coordinator,"entries") as IDictionary;
            var entry=entries!=null && entries.Contains(request.Intent)?entries[request.Intent]:null;
            var first=StartupRead(entry,"FirstFrame");
            var log=new System.Text.StringBuilder("originalBranch=Evaluate.before-Fail(snapshot-changed) frame="+Time.frameCount+
                " core="+StartupCore(coordinator)+" requestRef="+StartupIdentity(request)+" entryRef="+StartupIdentity(entry)+
                " requestSameEntry="+ReferenceEquals(StartupRead(entry,"Request"),request)+
                " ageFrames="+(first is int firstFrame?(Time.frameCount-firstFrame).ToString():"UNKNOWN"));
            log.AppendLine().Append("Request "+StartupMembers(request,"Intent","RequestedFrame","Start","Target","Radius","SafetyMargin",
                "ResourceId","FiniteSeconds","IndependentlyProgressing","ProvenFailure","PlanPrimaryRoute")+
                " native="+StartupMembers(request.NativeStamp,"RequestId","LegGeneration")+
                " selectedJourneyCount="+request.SelectedJourney.Count+" requirementsCount="+request.Requirements.Count);
            log.AppendLine().Append("Entry "+StartupMembers(entry,"FirstFrame","Queries","RequestSequence","AcquiredSequence","CandidateCount",
                "CurrentIndex","JobIndex","WaitRemaining","FiniteLimit","Parking","Queued","Prepared","Active","Retired","Discarded")+
                " decisionBeforeFail="+StartupValue(StartupRead(entry,"Decision"))+
                " attemptedIndices="+StartupMembers(StartupRead(entry,"AttemptedIndices"),"Count")+
                " legRef="+StartupIdentity(StartupRead(entry,"Leg"))+" leg="+
                StartupMembers(StartupRead(entry,"Leg"),"Index","Generation","NeedsQuery","Committed","Bound","Completed","Suspended"));
            AppendInitialTrafficSnapshot(log,"old.requestSnapshot",request.Snapshot);
            AppendInitialTrafficSnapshot(log,"new.actualSnapshot",current);
            var oldBody=request.Snapshot?.Bodies.FirstOrDefault(b=>b.Owner.ActorId==request.Intent.ActorId);
            var newBody=current?.Bodies.FirstOrDefault(b=>b.Owner.ActorId==request.Intent.ActorId);
            log.AppendLine().Append("OwnBody oldLife="+StartupIdentity(oldBody?.ActorLife)+" newLife="+StartupIdentity(newBody?.ActorLife)+
                " sameLife="+(oldBody!=null && newBody!=null?ReferenceEquals(oldBody.ActorLife,newBody.ActorLife).ToString():"UNKNOWN")+
                " sameOwner="+(oldBody!=null && newBody!=null?oldBody.Owner.Equals(newBody.Owner).ToString():"UNKNOWN")+
                " samePose="+(oldBody!=null && newBody!=null?oldBody.Position.Equals(newBody.Position).ToString():"UNKNOWN"));
            if(request.Snapshot!=null && current!=null)
                foreach(var body in current.Bodies.Where(b=>b.Owner.ActorId!=request.Intent.ActorId &&
                    !request.Snapshot.Bodies.Any(old=>old.Owner.ActorId==b.Owner.ActorId && ReferenceEquals(old.ActorLife,b.ActorLife))))
                    log.AppendLine().Append("OtherActorAdded owner="+StartupValue(body.Owner)+" life="+StartupIdentity(body.ActorLife)+" pose="+body.Position.ToString("F9"));
            if(request.Intent.IntentId=="pickup-paid-movement")
            {
                var visits=StartupRead(customers,"visits") as IDictionary;var visit=visits!=null && visits.Contains(request.Intent.ActorId)?visits[request.Intent.ActorId]:null;
                var paid=StartupRead(visit,"PaidPickup");var active=StartupRead(paid,"ActiveIntent");
                log.AppendLine().Append("ActualPaidTask "+StartupMembers(paid,"FirstFrame","Intent","ActiveIntent","Accepted","Transferred","Failed","RecoveryUsed","RecoveryPending")+" activeIntentMatchesOriginalRequest="+(active is TrafficIntentKey key?key.Equals(request.Intent).ToString():"UNKNOWN"));
                if(Environment.GetCommandLineArgs().Contains("-trafficPaidPreparationSnapshotDetails"))
                    AppendPaidPreparationSnapshotDetails(log,request,entry,visit,paid,current);
            }
            var roster=StartupRead(employees,"employees") as IEnumerable;var matched=false;
            if(roster!=null)foreach(var employee in roster)
            {
                if(!(StartupRead(employee,"Id") is string id) || id!=request.Intent.ActorId)continue;
                matched=true;var runner=StartupRead(employee,"Runner");var port=StartupRead(employee,"Port");
                var move=StartupRead(port,"trafficMove");var actor=StartupRead(employee,"Actor") as NavigationActor;
                log.AppendLine().Append("ActualEmployee ref="+StartupIdentity(employee)+" "+StartupMembers(employee,"Id","Role","Failure","NeedsBinding","SpawnAttempted")+
                    " EFC="+StartupMembers(employees,"session","observedRevision","integratedMode")+
                    " binding="+StartupMembers(StartupRead(employee,"Binding"),"LayoutRevision","WorkStationId","PickUpId"));
                log.AppendLine().Append("ActualRunner ref="+StartupIdentity(runner)+" "+StartupMembers(runner,"state","failure","handleId","generation",
                    "layoutRevision","hasCheckpoint","disposed","remaining")+
                    " task="+StartupMembers(StartupRead(runner,"taskToken"),"SessionId","Epoch","Sequence","Attempt")+
                    " checkpoint="+StartupMembers(StartupRead(runner,"checkpoint"),"Role","VisitId","Stage","RemainingWorkSeconds")+
                    " acceptedToken="+StartupMembers(StartupRead(employee,"AcceptedToken"),"SessionId","Epoch","Sequence","Attempt"));
                log.AppendLine().Append("ActualPort ref="+StartupIdentity(port)+" "+StartupMembers(port,"session","trafficState","workMoveSequence")+
                    " moveRef="+StartupIdentity(move)+" "+StartupMembers(move,"Intent","Index","NativeGeneration","Suspended")+
                    " moveCore="+StartupIdentity(StartupRead(move,"Coordinator"))+" source="+StartupValue(StartupRead(move,"Source"))+
                    " runnerLeg="+StartupMembers(StartupRead(runner,"moveLeg"),"Generation","LayoutRevision","NativeLegGeneration","RequestId","AwaitingTraffic","Completed","State"));
                log.AppendLine().Append("ActualActor life="+StartupIdentity(actor)+" "+StartupMembers(actor,"ActorId","isActiveAndEnabled")+
                    " pose="+(actor==null?"UNKNOWN":actor.transform.position.ToString("F9"))+
                    " registeredSameLife="+(actor==null || world==null?"UNKNOWN":world.RegisteredActors.Any(a=>ReferenceEquals(a,actor)).ToString()));
            }
            if(!matched)log.AppendLine().Append("ActualEmployee=UNKNOWN(no-current-roster-match)");
            AppendInitialMovementResumeFacts(log,request.Intent.ActorId,coordinator,request,current);
            return log.ToString();
        }
        // Default-off read-only metadata at the ORIGINAL snapshot failure; never refresh, submit, query, or re-run a guard.
        // 原snapshot-changed分支只复制生产者seal与当前role描述；诊断不修复候选、不改原断言/预算。
        private void AppendPaidPreparationSnapshotDetails(System.Text.StringBuilder log,TrafficRequest request,object entry,object visit,object paid,TrafficWorldSnapshot current)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            try
            {
                var preparation=StartupRead(paid,"Preparation");var source=StartupRead(preparation,"Source");var preview=StartupRead(preparation,"Preview");
                var parking=StartupRead(preparation,"Parking");var actor=StartupRead(preparation,"Actor") as NavigationActor;
                log.AppendLine().Append("PAID_PREPARATION_SNAPSHOT_DETAILS originalBranchOnly=True guardsReevaluated=False queriesAdded=0 frame="+Time.frameCount+
                    " preparationRef="+StartupIdentity(preparation)+" sourceRef="+StartupIdentity(source)+" sourceSameActualDecision="+ReferenceEquals(source,StartupRead(entry,"Decision"))+
                    " source="+StartupValue(source)+" previewRef="+StartupIdentity(preview)+" preview="+StartupMembers(preview,"Consumed","Kind","Id","ExpectedVersion","IsCurrent","Assignment"));
                log.AppendLine().Append("PaidProducerSeal "+StartupMembers(preparation,"FirstFrame","Generation","PhaseSequence","Order","Intent","Start","Target","Radius","Skin","Epsilon")+
                    " visit="+StartupMembers(visit,"State","Generation","TrafficPhaseSequence","CounterArrived","QueueLeft","PickupInTransit","PickupArrived","HasCollected","RequestId","TrafficMovePending")+
                    " actorPose="+(actor==null?"UNKNOWN":actor.transform.position.ToString("F9")));
                log.AppendLine().Append("PaidProducerRefs sameVisit="+ReferenceEquals(StartupRead(preparation,"Visit"),visit)+" sameTask="+ReferenceEquals(StartupRead(preparation,"Task"),paid)+
                    " sameActor="+ReferenceEquals(actor,StartupRead(visit,"Actor"))+" sameAdmission="+ReferenceEquals(StartupRead(preparation,"Admission"),StartupRead(visit,"Admission"))+
                    " sameParking="+ReferenceEquals(parking,StartupRead(visit,"PickupParking"))+" sameParkingSource="+ReferenceEquals(StartupRead(preparation,"ParkingSource"),StartupRead(parking,"Source"))+
                    " sameSelection="+ReferenceEquals(StartupRead(preparation,"Selection"),StartupRead(visit,"TrafficSelection"))+
                    " samePending="+ReferenceEquals(StartupRead(preparation,"Pending"),StartupRead(visit,"PendingTraffic"))+
                    " sameIssued="+ReferenceEquals(StartupRead(preparation,"Issued"),StartupRead(visit,"IssuedTraffic"))+
                    " sameMovement="+ReferenceEquals(StartupRead(preparation,"Movement"),StartupRead(visit,"TrafficMove"))+
                    " counterClaim="+StartupMembers(StartupRead(preparation,"CounterClaim"),"IsCurrent","CapturedOwnerVersion","Assignment")+
                    " pickupClaim="+StartupMembers(StartupRead(parking,"Claim"),"IsCurrent","CapturedOwnerVersion","Assignment")+
                    " parking="+StartupMembers(parking,"Retired","Revision","Token")+
                    " sameSuppliedSnapshot="+ReferenceEquals(current,StartupRead(customers,"trafficSnapshot")));
                // PreservePickupGoals only reads visits/assignments/current snapshot to project requirements (no TrafficKey, writes or Native calls).
                var project=typeof(CustomerFlowController).GetMethod("PreservePickupGoals",flags);
                var projected=project?.Invoke(customers,new[]{visit}) as System.Collections.Generic.IEnumerable<TrafficRouteRequirement>;
                if(projected==null){log.AppendLine().Append("PaidRoleShape=UNKNOWN(no-readonly-projection)");return;}
                var actual=projected.ToArray();var original=request.Requirements;
                bool Same(TrafficRouteRequirement a,TrafficRouteRequirement b)=>a.ResourceId==b.ResourceId && a.Start==b.Start && a.Goal==b.Goal && a.Clearance==b.Clearance &&
                    a.SelectedJourney==b.SelectedJourney && a.AvoidProposedParking==b.AvoidProposedParking;
                var shape=original.Count==actual.Length && original.Select((old,index)=>Same(old,actual[index])).All(value=>value);
                log.AppendLine().Append("PaidRoleShape oldCount="+original.Count+" currentCount="+actual.Length+" sameExactRefreshSchema="+shape+
                    " sameShapeRequiredByExistingTryRefreshQueuedPreparation=True");
                foreach(var role in original)log.AppendLine().Append("PaidRole.original "+StartupMembers(role,"ResourceId","Start","Goal","Clearance","SelectedJourney","AvoidProposedParking")+" obstacles="+role.Obstacles.Count);
                foreach(var role in actual)log.AppendLine().Append("PaidRole.current "+StartupMembers(role,"ResourceId","Start","Goal","Clearance","SelectedJourney","AvoidProposedParking")+" obstacles="+role.Obstacles.Count);
            }
            catch(Exception error){log.AppendLine().Append("PAID_PREPARATION_SNAPSHOT_DETAILS status=UNKNOWN error="+error.GetType().Name+":"+error.Message);}
        }
        private static void AppendInitialTrafficSnapshot(System.Text.StringBuilder log,string label,TrafficWorldSnapshot snapshot)
        {
            log.AppendLine().Append(label+" ref="+StartupIdentity(snapshot)+" "+StartupMembers(snapshot,"Version","BodyVersion"));
            if(snapshot==null){log.Append(" Bodies=UNKNOWN Parking=UNKNOWN");return;}
            log.Append(" Bodies="+snapshot.Bodies.Count+" Parking="+snapshot.Parking.Count);
            foreach(var body in snapshot.Bodies)
                log.AppendLine().Append(label+".Body owner="+StartupValue(body.Owner)+" life="+StartupIdentity(body.ActorLife)+
                    " "+StartupMembers(body,"Position","Radius","SafetyMargin","Enabled","ActorActive","IndependentlyProgressing","RemainingSeconds"));
            foreach(var claim in snapshot.Parking)
                log.AppendLine().Append(label+".Parking owner="+StartupValue(claim.Owner)+" canonicalOwner="+StartupIdentity(claim.CanonicalOwner)+
                    " businessOwner="+StartupIdentity(claim.BusinessOwner)+" release="+StartupIdentity(claim.ReleaseObligation)+
                    " "+StartupMembers(claim,"OwnerVersion","ResourceId","Position","Radius","SafetyMargin"));
        }
        // 仅在原 Until 失败后读取现场；不重放早期原因、不授予恢复或移动权限。
        // One late observation before the original assertion terminates either selected test.
        private void LogStartupContextOnFailure()
        {
            var test=TestContext.CurrentContext.Test.Name;
            if(test!="OwnerDisableRetainsModeAndResumesSameBusinessSession" &&
                test!="PausedResumeCannotReleaseCustomerHoldOrSubmitMovement")return;
            var log=new System.Text.StringBuilder("[INTEGRATION_STARTUP_CONTEXT] test="+test+
                " frame="+Time.frameCount+" observation=late-failed-until originalStaleCause=UNKNOWN");
            try
            {
                var ownerTraffic=StartupRead(owner,"traffic");var customerTraffic=StartupRead(customers,"traffic");
                var service=StartupRead(owner,"service");var serviceState=StartupRead(service,"Snapshot");
                log.AppendLine().Append("CLC ref="+StartupIdentity(owner)+" service="+StartupIdentity(service)+
                    " "+StartupMembers(serviceState,"SessionId","Epoch","Status","Failure")+
                    " "+StartupMembers(owner,"trafficEpoch","trafficRevision","held","comboReady","lifecycleFailure"));
                log.AppendLine().Append("CLC.core "+StartupCore(ownerTraffic));
                log.AppendLine().Append("CFC ref="+StartupIdentity(customers)+" "+
                    StartupMembers(customers,"trafficSession","trafficEpoch","revision","integratedMode","integratedHeld","disposed")+
                    " coreSameCLC="+ReferenceEquals(customerTraffic,ownerTraffic));
                log.AppendLine().Append("CFC.core "+StartupCore(customerTraffic));
                log.AppendLine().Append("World ref="+StartupIdentity(world)+" "+
                    StartupMembers(world,"LayoutAvailable","currentRevision","isActiveAndEnabled")+
                    " adapter="+StartupIdentity(adapter)+" "+StartupMembers(adapter,"Revision","PublishedRevision","isActiveAndEnabled")+
                    " time="+StartupMembers(time,"CurrentSpeed","IsResumeBlocked","ResumeBlockReason"));
                log.AppendLine().Append("ReferenceBindings CLC.worldViaAdapter="+StartupIdentity(StartupRead(StartupRead(owner,"adapter"),"World"))+
                    " CFC.world="+StartupIdentity(StartupRead(customers,"world"))+
                    " CFC.adapter="+StartupIdentity(StartupRead(customers,"adapter"))+
                    " CLC.adapter="+StartupIdentity(StartupRead(owner,"adapter"))+
                    " CFC.worldSameFixture="+ReferenceEquals(StartupRead(customers,"world"),world)+
                    " CFC.adapterSameCLC="+ReferenceEquals(StartupRead(customers,"adapter"),StartupRead(owner,"adapter")));
                var sceneOwners=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CafeLoopController>(true)).ToArray();
                log.AppendLine().Append("Scene.CLC.count="+sceneOwners.Length);
                foreach(var sceneOwner in sceneOwners)
                    log.AppendLine().Append("Scene.CLC ref="+StartupIdentity(sceneOwner)+" manualOwner="+ReferenceEquals(sceneOwner,owner)+
                        " "+StartupMembers(sceneOwner,"isActiveAndEnabled","trafficEpoch","trafficRevision")+
                        " service="+StartupMembers(StartupRead(StartupRead(sceneOwner,"service"),"Snapshot"),"SessionId","Epoch")+
                        " core="+StartupCore(StartupRead(sceneOwner,"traffic"))+
                        " customersSameFixture="+ReferenceEquals(StartupRead(sceneOwner,"customers"),customers));
                StartupKeys(log,"CFC.trafficKeys",StartupRead(customers,"trafficKeys"),customerTraffic);
                StartupKeys(log,"CFC.trafficActorKeys",StartupRead(customers,"trafficActorKeys"),customerTraffic);
                var roster=StartupRead(employees,"employees") as IEnumerable;
                log.AppendLine().Append("EFC ref="+StartupIdentity(employees)+" "+
                    StartupMembers(employees,"session","disposed","observedRevision","integratedMode")+
                    " roster="+(roster==null?"UNKNOWN":"actual-private-employees"));
                if(roster!=null)foreach(var employee in roster)
                {
                    var runner=StartupRead(employee,"Runner");var port=StartupRead(employee,"Port");
                    var move=StartupRead(port,"trafficMove");var moveCore=StartupRead(move,"Coordinator");
                    var intent=StartupRead(move,"Intent");var actor=StartupRead(employee,"Actor") as NavigationActor;
                    log.AppendLine().Append("Employee "+StartupMembers(employee,"Role","Id","Failure","SpawnAttempted","NeedsBinding")+
                        " runner="+StartupIdentity(runner)+" "+StartupMembers(runner,"state","failure","generation","layoutRevision","hasCheckpoint","disposed")+
                        " snapshot="+StartupMembers(StartupRead(runner,"Snapshot"),"State","Failure","HandleId","RemainingWorkSeconds")+
                        " checkpoint="+StartupMembers(StartupRead(employee,"Checkpoint"),"Role","VisitId","Stage","RemainingWorkSeconds")+
                        " runnerCheckpoint="+StartupMembers(StartupRead(runner,"checkpoint"),"Role","VisitId","Stage","RemainingWorkSeconds")+
                        " binding="+StartupMembers(StartupRead(employee,"Binding"),"LayoutRevision","WorkStationId","PickUpId"));
                    log.AppendLine().Append("Employee.Port ref="+StartupIdentity(port)+" "+StartupMembers(port,"session","trafficState","workMoveSequence")+
                        " move="+StartupIdentity(move)+" createdFrame=UNKNOWN(no-retained-creation-field)"+
                        " "+StartupMembers(move,"Intent","Resource","Index","NativeGeneration","Suspended")+
                        " goal="+StartupMembers(StartupRead(move,"Goal"),"Position","Facing")+
                        " sourceDecision="+StartupValue(StartupRead(move,"Source"))+
                        " coreSameCFC="+ReferenceEquals(moveCore,customerTraffic)+" coreSameCLC="+ReferenceEquals(moveCore,ownerTraffic));
                    log.AppendLine().Append("Employee.moveCore "+StartupCore(moveCore));
                    StartupDecision(log,"Employee.move.originalCore",moveCore,intent);
                    StartupDecision(log,"Employee.move.currentCFC",customerTraffic,intent);
                    StartupDecision(log,"Employee.move.currentCLC",ownerTraffic,intent);
                    if(actor==null)log.AppendLine().Append("Employee.actor=UNKNOWN(null-or-unavailable)");
                    else log.AppendLine().Append("Employee.actor life="+StartupIdentity(actor)+" "+StartupMembers(actor,"ActorId","isActiveAndEnabled")+
                        " registeredSameLife="+(world==null?"UNKNOWN":world.RegisteredActors.Any(a=>ReferenceEquals(a,actor)).ToString())+
                        " worldSameFixture="+ReferenceEquals(StartupRead(actor,"World"),world)+
                        " pose="+actor.transform.position.ToString("F9")+
                        " agent="+StartupMembers(actor.Agent,"enabled","isOnNavMesh"));
                }
            }
            catch(Exception error){log.AppendLine().Append("diagnostic=UNKNOWN("+error.GetType().Name+":"+error.Message+")");}
            // A diagnostic read or logger failure must never replace the original NUnit assertion.
            try{Debug.Log(log.ToString());}catch(Exception){}
        }
        private static object StartupRead(object source,string name)
        {
            if(source==null)return "UNKNOWN(null-owner:"+name+")";
            try
            {
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
                var field=source.GetType().GetField(name,flags);if(field!=null)return field.GetValue(source);
                var property=source.GetType().GetProperty(name,flags);
                return property!=null && property.GetIndexParameters().Length==0?property.GetValue(source):"UNKNOWN(missing:"+name+")";
            }
            catch(Exception error){return "UNKNOWN("+name+":"+error.GetType().Name+")";}
        }
        private static string StartupIdentity(object value)=>value==null?"null":value is string text?text:
            value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        private static string StartupValue(object value)
        {
            if(value==null)return "null";
            if(value is TrafficIntentKey key)return "{session="+key.Session+",epoch="+key.Epoch+",actor="+key.ActorId+
                ",intent="+key.IntentId+",generation="+key.Generation+",layout="+key.LayoutRevision+",context="+key.Context+"}";
            if(value is TrafficDecision decision)return "{intent="+StartupValue(decision.Intent)+",kind="+decision.Kind+
                ",reason="+decision.Reason+",disposition="+decision.FailureDisposition+",permit="+decision.Permit+
                ",requestSequence="+decision.RequestSequence+",acquiredSequence="+decision.AcquiredSequence+"}";
            if(value is Vector3 position)return position.ToString("F9");
            return value.ToString();
        }
        private static string StartupMembers(object source,params string[] names)=>string.Join(" ",
            names.Select(name=>name+"="+StartupValue(StartupRead(source,name))));
        private static string StartupCore(object core)=>"ref="+StartupIdentity(core)+" "+
            StartupMembers(core,"session","epoch","layoutRevision","closed","paused")+
            " snapshot="+StartupMembers(StartupRead(core,"snapshot"),"Version","BodyVersion");
        private static void StartupKeys(System.Text.StringBuilder log,string label,object value,object core)
        {
            var keys=value as IDictionary;if(keys==null){log.AppendLine().Append(label+"=UNKNOWN");return;}
            log.AppendLine().Append(label+".count="+keys.Count);
            foreach(DictionaryEntry pair in keys)
            {
                log.AppendLine().Append(label+"["+StartupValue(pair.Key)+"]="+StartupValue(pair.Value));
                StartupDecision(log,label+".currentCore",core,pair.Value);
            }
        }
        private static void StartupDecision(System.Text.StringBuilder log,string label,object core,object intent)
        {
            try
            {
                if(!(core is ServiceTrafficCoordinator coordinator) || !(intent is TrafficIntentKey key))
                {log.AppendLine().Append(label+"=UNKNOWN(no-coordinator-or-intent)");return;}
                log.AppendLine().Append(label+" observationOnly="+StartupValue(coordinator.GetDecision(key)));
                var entries=StartupRead(core,"entries") as IDictionary;
                var entry=entries!=null && entries.Contains(key)?entries[key]:null;
                log.AppendLine().Append(label+".retainedEntry="+(entry==null?"UNKNOWN(no-retained-entry)":
                    StartupMembers(entry,"FirstFrame","Retired","Discarded","Prepared","Active","Queued"))+
                    " request="+StartupMembers(StartupRead(entry,"Request"),"Intent","RequestedFrame"));
            }
            catch(Exception error){log.AppendLine().Append(label+"=UNKNOWN("+error.GetType().Name+")");}
        }
        private IEnumerator Until(Func<bool> done,int max=3000)
        {
            for(var f=0;f<max && !done();f++) yield return null;
            if(!done())LogStartupContextOnFailure();
            if(!done() && TestContext.CurrentContext.Test.Name=="InitialEmployeeFailureRetriesOnlyOnExplicitIntegratedResume")
                FlushInitialEmployeeSnapshotObservation();
            Assert.That(done(),Is.True,customers.Status+" | "+owner.Snapshot.Failure+" | "+
                string.Join(" / ",employees.Snapshot.Select(e=>e.Role+" "+e.State+" "+e.Failure)));
        }
    }
}
#endif

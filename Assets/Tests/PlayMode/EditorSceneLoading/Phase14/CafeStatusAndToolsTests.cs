#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using AnimalCafe.CafeLoop;
using AnimalCafe.Capacity;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
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
using Object=UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode.Phase14
{
    public sealed class CafeStatusAndToolsTests
    {
        private Scene scene; private CafeLoopController owner; private CafeLoopValidationController tools;
        private CustomerFlowController customers; private EmployeeFlowController employees; private GameTimeService time;
        private float capture,scale; private UnityEngine.InputSystem.InputActionAsset[] inputs;
        private Action highLoadTraceCleanup;
        private Action<string,string> cashierDiagnosticSample;
        private Action cashierDiagnosticCleanup;
        private bool originalUnpaidFailureSnapshotWritten;
        private Action originalUnpaidFailureObserverCleanup,originalUnpaidFailureFinalSnapshot;
        private Action<string> fiveCachedQueueSnapshot;
        private void StopOriginalUnpaidFailureObserver()
        {var cleanup=originalUnpaidFailureObserverCleanup;originalUnpaidFailureObserverCleanup=null;originalUnpaidFailureFinalSnapshot=null;cleanup?.Invoke();}
        private void StopCashierOriginalDecisionTrace()
        {
            var stop=cashierDiagnosticCleanup; cashierDiagnosticCleanup=null;
            stop?.Invoke();
        }
        private void StopHighLoadTrace()
        {
            var cleanup=highLoadTraceCleanup; highLoadTraceCleanup=null;
            cleanup?.Invoke();
        }
        private const string ValidationPath="Assets/Scenes/Validation/Phase14CafeLoop.unity";
        [UnitySetUp] public IEnumerator Setup()
        {
            fiveCachedQueueSnapshot=null;StopOriginalUnpaidFailureObserver();originalUnpaidFailureSnapshotWritten=false;
            capture=Time.captureDeltaTime; scale=Time.timeScale;
            Time.captureDeltaTime=TestContext.CurrentContext.Test.Name==nameof(ContinuedArrivalsFillThenResumeCollectionAndDrain)?0:1f/60;
            Assert.That(File.Exists(ValidationPath),Is.True,"Build actual P14 validation Scene before Play tests");
            scene=EditorSceneManager.LoadSceneInPlayMode(ValidationPath,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; owner=Find<CafeLoopController>(); tools=Find<CafeLoopValidationController>();
            customers=Find<CustomerFlowController>(); employees=Find<EmployeeFlowController>(); time=Find<GameTimeService>();
            inputs=Phase8SceneInputTestCleanup.CaptureAssets(scene);
            if(TestContext.CurrentContext.Test.Name==nameof(ContinuedArrivalsFillThenResumeCollectionAndDrain) &&
                Environment.GetEnvironmentVariable("ANIMALCAFE_P14_HIGH_LOAD_AUTOSTART")=="true")
            { TestContext.WriteLine("P14_HIGH_STARTUP ordinary AutoSpawn retained; no initial Hold or staff-Idle wait; native="+!Application.isBatchMode); yield break; }
            tools.HoldArrivals();
            yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            // Timeout may bypass the nested iterator finally; disarm before any Scene unload.
            // timeout可能跳过内部finally；先注销观察者，再卸载Scene。
            fiveCachedQueueSnapshot=null;
            StopHighLoadTrace();
            StopCashierOriginalDecisionTrace();
            StopOriginalUnpaidFailureObserver();
            // Scene teardown is not an actual customer Exit, including timeout paths that skip nested finally.
            if(scene.IsValid() && scene.isLoaded)
                foreach(var observer in scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<CafeActualExitObserver>(true)))observer.Receipt=null;
            if(scene.IsValid() && scene.isLoaded) { var empty=SceneManager.CreateScene("P14 tools cleanup"); SceneManager.SetActiveScene(empty); yield return SceneManager.UnloadSceneAsync(scene); }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputs ?? Array.Empty<UnityEngine.InputSystem.InputActionAsset>());
            Time.captureDeltaTime=capture; Time.timeScale=scale;
        }
        [UnityTest] public IEnumerator ContinuedArrivalsFillThenResumeCollectionAndDrain()
        {
            // The optional ordinary-autostart mode keeps its original layout and startup ownership.
            if(Environment.GetEnvironmentVariable("ANIMALCAFE_P14_HIGH_LOAD_AUTOSTART")!="true")
                yield return OpenPickupLayoutForProjectionCapacity();
            // Catches unsafe shared-corridor dispatch after the fifth customer, not just five bounded arrivals.
            Time.captureDeltaTime=0;
            var world=Find<NavigationWorld>(); var ring=new System.Collections.Generic.Queue<string>();
            var observedCupDisplay=Find<PickupCupDisplay>();
            var nextSample=0d; var captured=false; var projectionCorrect=true; var maximumDelivery=0;
            var totals=new System.Collections.Generic.Dictionary<string,CapacityToken>(); var exits=new System.Collections.Generic.HashSet<string>();
            var completed=new System.Collections.Generic.HashSet<string>(); var cashierDone=new System.Collections.Generic.Dictionary<string,int>(); var prepared=new System.Collections.Generic.Dictionary<string,int>();
            var evidenceActive=true; var exitCorrect=true; var fifoCorrect=true; var capacityCorrect=true;
            var exit=CafeLoopRuntimeProof.Field<Transform>(customers,"exit");
            bool Blocked()=>employees.Snapshot.Any(e=>e.State==EmployeeState.Blocked) || customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked);
            bool Drained()=>customers.Snapshot.Count==0 && owner.Snapshot.Delivery.Count==0 && customers.Capacity.GetCapacities().All(c=>c.Used==0) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle && e.HandleId==0);
            Action<CafeLoopSnapshot> trace=_=>
            {
                if(!evidenceActive) return;
                Assert.That(observedCupDisplay!=null,Is.True,"Live projection evidence requires the original cup display");
                foreach(var visit in customers.Snapshot)
                {
                    if(totals.ContainsKey(visit.VisitId)) continue;
                    var id=visit.VisitId; var actor=world.RegisteredActors.Single(a=>a.ActorId==id);
                    totals.Add(id,customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token);
                    var observer=actor.gameObject.AddComponent<CafeActualExitObserver>(); observer.VisitId=id;
                    observer.Receipt=(leaving,pose)=>
                    {
                        if(!evidenceActive) return;
                        var released=customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,totals[leaving])).State==ReservationState.Released;
                        exitCorrect&=released && owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==leaving && o.State==OrderState.Completed) &&
                            Vector3.Distance(pose,exit.position)<=actor.Settings.ArrivalDistance+actor.Settings.Epsilon && !world.RegisteredActors.Contains(actor);
                        exits.Add(leaving); TestContext.WriteLine("P14_HIGH_EXIT visit="+leaving+" pose="+pose.ToString("F6")+" originalTotal="+totals[leaving].Id+" released="+released);
                    };
                }
                foreach(var body in world.RegisteredActors.Where(a=>totals.ContainsKey(a.ActorId) && Vector3.Distance(a.transform.position,exit.position)>a.Settings.ArrivalDistance+a.Settings.Epsilon))
                    capacityCorrect&=customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,totals[body.ActorId])).State!=ReservationState.Released;
                foreach(var order in owner.Snapshot.Orders.Values.Where(o=>o.State==OrderState.Completed).OrderBy(o=>o.OrderId))
                {
                    if(completed.Contains(order.CustomerId)) continue;
                    fifoCorrect&=owner.Snapshot.PaidVisitIds.FirstOrDefault(id=>!completed.Contains(id))==order.CustomerId; completed.Add(order.CustomerId);
                }
                maximumDelivery=Math.Max(maximumDelivery,owner.Snapshot.Delivery.Count);
                projectionCorrect&=owner.Snapshot.Delivery.OrderBy(d=>d.DeliverySequence).Take(3).Select(d=>d.Key).OrderBy(k=>k.OrderId)
                    .SequenceEqual(observedCupDisplay.VisibleOrders.Keys.OrderBy(k=>k.OrderId));
                if(Time.timeAsDouble>=nextSample)
                { nextSample=Time.timeAsDouble+.2; ring.Enqueue(ActualTraffic(world)); while(ring.Count>12) ring.Dequeue(); }
                if(!captured && Blocked())
                {
                    captured=true; TestContext.WriteLine("P14_FIRST_BLOCK frame="+Time.frameCount+" game="+Time.timeAsDouble);
                    foreach(var sample in ring) TestContext.WriteLine(sample);
                    TestContext.WriteLine(ActualTraffic(world)); tools.PrintSnapshot(); DumpRoutes();
                    if(!Application.isBatchMode) ScreenCapture.CaptureScreenshot(Path.GetFullPath("outputs/phase14/high-load-native-first-block.png"));
                }
            };
            Action<CafeTaskEvent> task=value=>
            {
                if(!evidenceActive) return;
                var counts=value.Role==EmployeeRole.Cashier && value.Kind==CafeTaskEventKind.ServiceFinished?cashierDone:value.Role==EmployeeRole.Barista && value.Kind==CafeTaskEventKind.Prepared?prepared:null;
                if(counts!=null) { counts.TryGetValue(value.Checkpoint.VisitId,out var prior); counts[value.Checkpoint.VisitId]=prior+1; }
                if(value.Kind==CafeTaskEventKind.Blocked) TestContext.WriteLine("P14_TASK_FAILURE frame="+Time.frameCount+" actor="+value.ActorId+" handle="+value.HandleId+" request="+value.RequestId+" gen="+value.Generation+" stage="+value.Checkpoint.Stage+" reason="+value.Failure);
            };
            owner.SnapshotChanged+=trace; employees.IntegratedTaskEvent+=task;
            highLoadTraceCleanup=()=>
            {
                evidenceActive=false;
                if(owner!=null) owner.SnapshotChanged-=trace;
                if(employees!=null) employees.IntegratedTaskEvent-=task;
            };
            try
            {
                TestContext.WriteLine("P14_HIGH_INITIAL capture="+Time.captureDeltaTime+" counter="+string.Join(";",CafeLoopRuntimeProof.Field<Vector3[]>(customers,"counterWaitSlots").Select(p=>p.ToString("F6"))));
                TestContext.WriteLine(ActualTraffic(world));
                tools.ResumeArrivals(); var deadline=Time.realtimeSinceStartup+40;
                while(customers.Snapshot.Count==0 && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(customers.Snapshot.Count,Is.GreaterThan(0)); tools.HoldArrivals();
                deadline=Time.realtimeSinceStartup+80; while(!Drained() && !Blocked() && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(Drained(),Is.True,"Ordinary prefix must complete a real order and exit before the held wave");
                var originalClock=CafeLoopRuntimeProof.Field<CustomerSpawnClock>(customers,"clock");
                CafeLoopRuntimeProof.Set(customers,"clock",new CustomerSpawnClock(()=>.1f));
                TestContext.WriteLine("P14_HIGH_LOAD input=one-second fixed valid random interval, capture="+Time.captureDeltaTime);
                maximumDelivery=0; captured=false; ring.Clear(); tools.HoldCollection(); tools.ResumeArrivals();
                var full=false; string priorGatedHead=null,gatedHead=null; var loggedDepartingHead=false;
                deadline=Time.realtimeSinceStartup+150;
                while(Time.realtimeSinceStartup<deadline)
                {
                    var head=customers.CounterAssignments.FirstOrDefault();
                    var visit=owner.Snapshot.Visits.FirstOrDefault(v=>v.VisitId==head.VisitId);
                    var waitVisit=CafeLoopRuntimeProof.Field<string>(owner,"pickupWaitVisit");
                    var waitReason=CafeLoopRuntimeProof.Field<string>(owner,"pickupWaitReason");
                    // A paid departing body can still own Counter[0]; sample the real gate, not generic Waiting.
                    // 已付款顾客尚未离开CR时仍可能占队首；未付款/无Order保留为下面的独立断言。
                    var gated=owner.Snapshot.Delivery.Count>=5 && owner.CanProgress && !string.IsNullOrEmpty(head.VisitId) && head.Arrived &&
                        visit.IsEnabled && visit.NavigationState==CustomerVisitState.Queued && visit.CounterArrived &&
                        employees.Snapshot.Any(e=>e.Role==EmployeeRole.Cashier && e.State==EmployeeState.Idle && e.HandleId==0) &&
                        waitVisit==head.VisitId && !string.IsNullOrWhiteSpace(waitReason);
                    if(!loggedDepartingHead && owner.Snapshot.Delivery.Count>=5 && visit.NavigationState==CustomerVisitState.MovingToPickup)
                    {
                        loggedDepartingHead=true;
                        TestContext.WriteLine("P14_GATE_REJECT_DEPARTING_HEAD visit="+head.VisitId+" state="+visit.NavigationState+
                            " paid="+owner.Snapshot.PaidVisitIds.Contains(head.VisitId)+" arrived="+head.Arrived+" waitVisit="+waitVisit);
                    }
                    full=gated && priorGatedHead==head.VisitId;
                    priorGatedHead=gated?head.VisitId:null;
                    if(full)
                    {
                        gatedHead=head.VisitId;
                        TestContext.WriteLine("P14_GATE_CONFIRMED_TWO_FRAMES visit="+gatedHead+" state="+visit.NavigationState+
                            " counterArrived="+visit.CounterArrived+" assignmentArrived="+head.Arrived+" reason="+waitReason);
                    }
                    if(full || Blocked()) break;
                    yield return null;
                }
                tools.HoldArrivals();
                TestContext.WriteLine("P14_FILL_END actualVisits="+customers.Snapshot.Count+" paid="+owner.Snapshot.PaidVisitIds.Count+" delivery="+owner.Snapshot.Delivery.Count+" full="+full+" blocked="+Blocked()+" reception="+owner.ViewSnapshot.Reception);
                DumpRoutes();
                if(full)
                {
                    var head=customers.CounterAssignments.First().VisitId;
                    Assert.That(head,Is.EqualTo(gatedHead),"The same actual Counter[0] owns the observed reservation gate");
                    Assert.That(owner.Snapshot.PaidVisitIds.Contains(head),Is.False,"The actual gated head remains unpaid without a cashier handle");
                    Assert.That(owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==head),Is.False,"The gated head has no committed Order");
                    Assert.That(Find<PickupCupDisplay>().VisibleOrders.Count,Is.EqualTo(3));
                    Assert.That(scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Count(t=>t.name.StartsWith("PickupCup_")),Is.EqualTo(3),"Exactly three real table cup objects, no hidden extras");
                    if(!Application.isBatchMode)
                    { ScreenCapture.CaptureScreenshot(Path.GetFullPath("outputs/phase14/high-load-native-fill.png")); yield return null; yield return null; }
                }
                // Always execute the real release/resume operation; the old Until helper stops at Blocked.
                var neededResume=Blocked(); tools.ResumeCollection(); if(neededResume) tools.ResumeBlockedFlow();
                yield return null;
                deadline=Time.realtimeSinceStartup+120;
                while(!Drained() && !Blocked() && Time.realtimeSinceStartup<deadline) yield return null;
                if(!Drained()) { tools.PrintSnapshot(); DumpRoutes(); TestContext.WriteLine(ActualTraffic(world)); }
                Assert.That(Drained(),Is.True,"All admitted bodies must actually exit after normal collection and at most one explicit recovery; "+owner.Snapshot.Failure);
                Assert.That(captured,Is.False,"Normal continuous arrivals must not cause an unexpected Blocked; explicit Resume is diagnostic only");
                Assert.That(maximumDelivery,Is.GreaterThanOrEqualTo(5),"Real held wave must demonstrate five delivered orders / only three visible cups");
                Assert.That(full,Is.True,"Continuous arrivals must reach a genuine effective-space or logical-capacity gate");
                Assert.That(projectionCorrect,Is.True,"Visible keys replenish in delivery order, at most three");
                CafeLoopRuntimeProof.Set(customers,"clock",originalClock);
                var before=customers.Snapshot.Count; var started=Time.timeAsDouble; tools.ResumeArrivals(); deadline=Time.realtimeSinceStartup+12;
                while(customers.Snapshot.Count==before && Time.realtimeSinceStartup<deadline) yield return null;
                tools.HoldArrivals(); Assert.That(customers.Snapshot.Count,Is.EqualTo(1),"No admission backlog burst");
                Assert.That(Time.timeAsDouble-started,Is.InRange(0,10.3),"Restored admission uses a fresh complete random game interval");
                deadline=Time.realtimeSinceStartup+80; while(!Drained() && !Blocked() && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(Drained(),Is.True,"Recovery guest must also exit and release all original capacity");
                foreach(var pending in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<NavigationActor>(true)).Where(a=>totals.ContainsKey(a.ActorId) && !exits.Contains(a.ActorId)))
                    TestContext.WriteLine("P14_DESTROY_PENDING visit="+pending.ActorId+" pose="+pending.transform.position.ToString("F6")+" registered="+world.RegisteredActors.Contains(pending)+
                        " released="+(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,totals[pending.ActorId])).State==ReservationState.Released));
                yield return null; // Unity Destroy callbacks run at frame end, after logical exit/release.
                Assert.That(owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed),Is.True);
                Assert.That(fifoCorrect && capacityCorrect && exitCorrect,Is.True,"Actual FIFO, original in-route Total and same-body exit proof");
                Assert.That(exits.Count,Is.EqualTo(totals.Count)); Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(totals.Count));
                Assert.That(owner.Snapshot.Orders.Values.Select(o=>o.CustomerId).Distinct().Count(),Is.EqualTo(totals.Count));
                Assert.That(totals.Keys.All(id=>cashierDone.TryGetValue(id,out var count) && count==1 && prepared.TryGetValue(id,out var workCount) && workCount==1),Is.True,"Exactly one real cashier completion and preparation per original visit");
                CafePickupSideTests.ExportLayoutRecipe(scene,"L-11",0,"PASS_CONTINUOUS_FIVE_DELIVERIES_FIFO_ACTUAL_EXITS");
                TestContext.WriteLine("P14_HIGH_LOAD complete5/full/resume/projection/drain/freshInterval=True explicitResume="+neededResume);
            }
            finally { StopHighLoadTrace(); }
        }
        private string ActualTraffic(NavigationWorld world)
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var requests=(System.Collections.IDictionary)typeof(NavigationService).GetField("byActor",flags).GetValue(world.Service);
            var text=new System.Text.StringBuilder("TRAFFIC frame="+Time.frameCount+" game="+Time.timeAsDouble+" delta="+Time.deltaTime);
            foreach(var actor in world.RegisteredActors)
            {
                text.Append("\n actor=").Append(actor.ActorId).Append(" pose=").Append(actor.transform.position.ToString("F6"));
                if(!requests.Contains(actor.ActorId)) continue;
                var request=requests[actor.ActorId]; var type=request.GetType(); object Read(string name)=>type.GetField(name).GetValue(request);
                var driver=Read("Driver"); var destination=driver.GetType().GetField("destination",flags)?.GetValue(driver);
                text.Append(" request=").Append(Read("Id")).Append(" gen=").Append(Read("Generation")).Append(" original=").Append(((NavigationTarget)Read("Original")).Position.ToString("F6"))
                    .Append(" resolved=").Append(destination is Vector3 resolved?resolved.ToString("F6"):"unknown").Append(" retry=").Append(Read("RetryCount")).Append(" failure=").Append(Read("OriginalFailure"))
                    .Append(" segment=").Append(Read("SegmentSeconds")).Append('/').Append(Read("SegmentLimit")).Append(" noProgress=").Append(Read("NoProgressSeconds"));
                var stored=(Vector3[])driver.GetType().GetField("corners",flags).GetValue(driver);
                var index=(int)driver.GetType().GetField("corner",flags).GetValue(driver);
                text.Append(" driverCorner=").Append(index).Append(" driverRemaining=").Append(string.Join(";",stored.Skip(index).Select(p=>p.ToString("F6"))));
                if(actor.Agent!=null && actor.Agent.enabled && actor.Agent.isOnNavMesh)
                    text.Append(" pending=").Append(actor.Agent.pathPending).Append(" nativeGoal=").Append(actor.Agent.destination.ToString("F6")).Append(" corners=").Append(string.Join(";",actor.Agent.path.corners.Select(p=>p.ToString("F6"))));
            }
            return text.ToString();
        }
        [UnityTest] public IEnumerator HeldCollectionAllowsFiveRealDeliveriesAndFifoDrain()
            => FiveDeliveriesAndDrain();
        [UnityTest] public IEnumerator SharedServiceAllowsFiveRealDeliveriesAndFifoDrain()
        {
            var empty=SceneManager.CreateScene("P14 shared switch"); SceneManager.SetActiveScene(empty);
            yield return SceneManager.UnloadSceneAsync(scene);
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputs);
            scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase14SharedPickup.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; owner=Find<CafeLoopController>(); tools=Find<CafeLoopValidationController>();
            customers=Find<CustomerFlowController>(); employees=Find<EmployeeFlowController>(); time=Find<GameTimeService>();
            inputs=Phase8SceneInputTestCleanup.CaptureAssets(scene); tools.HoldArrivals();
            yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(employees.TryGetBinding(EmployeeRole.Barista,out var binding),Is.True);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var customerAnchor=(Vector3)typeof(CustomerFlowController).GetField("pickupCustomer",flags).GetValue(customers);
            Assert.That(Vector3.Distance(customerAnchor,binding.PickUpTarget.Value.Position),Is.LessThan(.01f),"Actual formal shared anchor");
            yield return FiveDeliveriesAndDrain();
        }
        [UnityTest] public IEnumerator CachedPickupNextForRealCounterFrontDefersOnlyConflictingUnbornJoinAndThreeGuestsActuallyExit()
        {
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            object Read(object value,string name)=>value==null?null:(value.GetType().GetField(name,flags)??value.GetType().GetField("<"+name+">k__BackingField",flags))?.GetValue(value);
            var savedScale=Time.timeScale;var savedCapture=Time.captureDeltaTime;var savedStepping=false;
            var remaining=12000;var exits=new System.Collections.Generic.HashSet<string>();var observers=new System.Collections.Generic.List<CafeActualExitObserver>();
            var errors=new System.Collections.Generic.List<string>();var observing=true;var firstStandingSeen=false;var watchStanding=false;
            Action<CafeLoopSnapshot> standingObserver=null;System.Reflection.FieldInfo preparationHook=null;Delegate preparationPrior=null,preparationInstalled=null;
            var preparationCalls=0;
            IEnumerator Run(IEnumerator operation)
            {
                try{while(operation.MoveNext()){Assert.That(remaining--,Is.GreaterThan(0),"One original12000-frame business window, never replenished");
                    Assert.That(operation.Current,Is.Null,"The bounded fixture forwards only original normal frames");yield return null;}}
                finally{(operation as IDisposable)?.Dispose();}
            }
            void Track(string id,NavigationWorld world)
            {
                var actor=world.RegisteredActors.Single(body=>body.ActorId==id);
                var total=customers.Capacity.GetReservations().Single(row=>row.OwnerId==id&&row.Kind==CapacityKind.TotalCustomers).Token;
                var tolerance=actor.Settings.ArrivalDistance+actor.Settings.Epsilon;var exitGoal=CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position;
                var observer=actor.gameObject.AddComponent<CafeActualExitObserver>();observer.VisitId=id;
                observer.Receipt=(leaving,pose)=>
                {
                    if(!observing)return;try{Assert.That(leaving,Is.EqualTo(id));Assert.That(Vector3.Distance(pose,exitGoal),Is.LessThanOrEqualTo(tolerance));
                        Assert.That(owner.Snapshot.Orders.Values.Single(order=>order.CustomerId==id).State,Is.EqualTo(OrderState.Completed));
                        Assert.That(customers.Capacity.GetReservations().Single(row=>ReferenceEquals(row.Token,total)).State,Is.EqualTo(ReservationState.Released));
                        Assert.That(world.RegisteredActors.Any(body=>body.ActorId==id),Is.False);Assert.That(exits.Add(id),Is.True);}
                    catch(Exception error){if(errors.Count<4)errors.Add(error.ToString());}
                };observers.Add(observer);
            }
            try
            {
                yield return OpenPickupLayoutForProjectionCapacity(); // Same formal4,4; initialize legacy cache before any real visit/source.
                InitializeLegacyCachedPickupInput(); // After publication, the original cached owner/slots are never rewritten.
                savedStepping=CafeLoopRuntimeProof.Field<bool>(owner,"stepping");var world=Find<NavigationWorld>();tools.ResumeCollection();
                Assert.That(customers.AutoSpawn,Is.False,"Three manual real opportunities only; no automatic anchor is cancelled/replaced");
                yield return Run(CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,Math.Min(6000,remaining),requireCurrentSnapshot:true));
                Assert.That(customers.TrySpawn(),Is.True,customers.Status);var prefix=customers.Snapshot.Single().VisitId;Track(prefix,world);
                yield return Run(Until(()=>customers.Snapshot.Count==0&&exits.Contains(prefix)&&customers.Capacity.GetCapacities().All(row=>row.Used==0)&&
                    employees.Snapshot.All(row=>row.State==EmployeeState.Idle),Math.Min(6000,remaining)));
                Assert.That(errors,Is.Empty,string.Join("\n",errors));
                var cached=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue");Assert.That(cached,Is.Not.Null);
                var cachedRows=CafeLoopRuntimeProof.Field<IDictionary>(cached,"reservations");Assert.That(cachedRows.Count,Is.Zero);Assert.That(cached.ServiceLeases,Is.Empty);
                var cachedSlots=CafeLoopRuntimeProof.Field<Vector3[]>(customers,"pickupWaitSlots").ToArray();
                yield return Run(CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,Math.Min(6000,remaining),requireCurrentSnapshot:true));
                Assert.That(customers.TrySpawn(),Is.True,customers.Status);var front=customers.Snapshot.Single().VisitId;Track(front,world);
                var core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");var entries=CafeLoopRuntimeProof.Field<IDictionary>(core,"entries");
                var visits=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");var counter=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue");
                var live=visits[front];var actor=(NavigationActor)Read(live,"Actor");var life=actor;var admission=Read(live,"Admission");
                var native=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byId");var byActor=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor");
                var spawn=CafeLoopRuntimeProof.Field<Transform>(customers,"spawn").position;var prefabs=CafeLoopRuntimeProof.Field<NavigationActor[]>(customers,"prefabs");
                var incomingRadius=prefabs.Max(body=>body.Settings.AgentRadius+body.Settings.CollisionSkin+body.Settings.Epsilon+body.Settings.ArrivalDistance);
                bool LastRealEnteringLeg()
                {
                    var move=Read(live,"TrafficMove");if(move==null||(CustomerVisitState)Read(live,"State")!=CustomerVisitState.Entering)return false;
                    var key=(TrafficIntentKey)Read(move,"Intent");if(!entries.Contains(key))return false;var leg=Read(entries[key],"Leg");var proof=Read(leg,"Proof") as TrafficRouteProof;
                    var stamp=Read(leg,"Stamp") is TrafficNativeStamp current?current:default;
                    return leg!=null&&proof!=null&&(bool)Read(leg,"Committed")&&(bool)Read(leg,"Bound")&&!(bool)Read(leg,"Completed")&&
                        (int)Read(leg,"Index")==proof.Waypoints.Count-1&&proof.Waypoints.Last()==counter.Snapshot[0].Position&&
                        stamp.RequestId>0&&(long)Read(live,"RequestId")==stamp.RequestId&&native.Contains(stamp.RequestId)&&byActor.Contains(front)&&
                        ReferenceEquals(native[stamp.RequestId],byActor[front])&&ReferenceEquals(Read(leg,"ActorLife"),actor)&&
                        Vector3.Distance(actor.transform.position,spawn)>actor.Settings.AgentRadius+incomingRadius;
                }
                yield return Run(Until(LastRealEnteringLeg,Math.Min(6000,remaining)));
                Assert.That(counter.Snapshot[0].VisitId,Is.EqualTo(front));Assert.That(counter.Snapshot[0].SlotIndex,Is.Zero);
                Assert.That(counter.TryCaptureAssignment(front,out var enteringClaim)&&enteringClaim.IsCurrent,Is.True);
                Assert.That(cached.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,front,cached.Version,out var cachedNext),Is.True);
                Assert.That(cachedRows.Count,Is.Zero);Assert.That(Read(live,"PickupAssigned"),Is.EqualTo(false));
                CafeLoopRuntimeProof.Set(owner,"stepping",true); // Native World finishes its real last leg; business cannot pre-accept pickup before the RED seam.
                var before=CafeLoopRuntimeProof.Field<long>(customers,"nextVisit");
                Assert.That(customers.TrySpawn(),Is.False,"One original next opportunity must remain unborn");
                var anchor=Read(customers,"pendingAdmissionAnchor");Assert.That(anchor,Is.Not.Null,"PremiseNotProven: real Entering-front opportunity created no anchor");
                var unborn=(TrafficIntentKey)Read(anchor,"Key");var firstFrame=Read(anchor,"FirstFrame");
                Assert.That((bool)Read(anchor,"Automatic"),Is.False);Assert.That(Read(anchor,"Owner"),Is.SameAs(counter));
                Assert.That(counter.TryPrepareMutation(CounterQueueService.MutationKind.Join,unborn.ActorId,counter.Version,out var join),Is.True);
                var oldJoin=join.Assignment;var plannedPickup=cachedNext.Assignment;
                var envelope=incomingRadius+actor.Settings.AgentRadius;
                Assert.That(Vector3.Distance(oldJoin.Position,plannedPickup.Position),Is.LessThan(envelope),
                    "PremiseNotProven: actual cached NEXT Reserve and actual next Join must conflict, not a hypothetical lane point");
                var nextTokens=customers.Capacity.GetReservations().ToArray();var roster=world.RegisteredActors.ToArray();
                var negativeOrderCount=owner.Snapshot.Orders.Count;var negativePaid=owner.Snapshot.PaidVisitIds.ToArray();
                void SameUnborn(bool noCore)
                {
                    Assert.That(errors,Is.Empty,string.Join("\n",errors));Assert.That(Read(customers,"pendingAdmissionAnchor"),Is.SameAs(anchor));
                    Assert.That(Read(anchor,"Key"),Is.EqualTo(unborn));Assert.That(Read(anchor,"FirstFrame"),Is.EqualTo(firstFrame));Assert.That(Read(anchor,"Before"),Is.EqualTo(before));
                    Assert.That(Read(anchor,"Coordinator"),Is.SameAs(core));Assert.That(Read(anchor,"Owner"),Is.SameAs(counter));
                    Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic"),Is.SameAs(core));Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic"),Is.SameAs(core));
                    Assert.That(CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue"),Is.SameAs(cached));
                    CollectionAssert.AreEqual(cachedSlots,CafeLoopRuntimeProof.Field<Vector3[]>(customers,"pickupWaitSlots"));
                    Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.EqualTo(before));Assert.That(visits.Contains(unborn.ActorId),Is.False);
                    Assert.That(counter.Snapshot.Any(row=>row.VisitId==unborn.ActorId),Is.False);Assert.That(world.RegisteredActors.Any(body=>body.ActorId==unborn.ActorId),Is.False);
                    Assert.That(byActor.Contains(unborn.ActorId),Is.False);Assert.That(customers.Capacity.GetReservations().Any(row=>row.OwnerId==unborn.ActorId),Is.False);
                    Assert.That(counter.TryCaptureAssignment(front,out var currentFront)&&currentFront.IsCurrent&&enteringClaim.IsSameReservation(currentFront),Is.True);
                    Assert.That(counter.TryPrepareMutation(CounterQueueService.MutationKind.Join,unborn.ActorId,counter.Version,out var currentJoin),Is.True);
                    Assert.That(currentJoin.Assignment.Position,Is.EqualTo(oldJoin.Position));
                    Assert.That(cached.TryPrepareMutation(PickupQueueService.MutationKind.Reserve,front,cached.Version,out var currentNext),Is.True);
                    Assert.That(currentNext.Assignment.Position,Is.EqualTo(plannedPickup.Position));Assert.That(cachedRows.Count,Is.Zero);Assert.That(Read(live,"PickupAssigned"),Is.EqualTo(false));
                    Assert.That(Vector3.Distance(currentJoin.Assignment.Position,currentNext.Assignment.Position),Is.LessThan(envelope));
                    Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(negativeOrderCount));CollectionAssert.AreEquivalent(negativePaid,owner.Snapshot.PaidVisitIds);
                    CollectionAssert.AreEquivalent(roster,world.RegisteredActors);Assert.That(customers.Capacity.GetCapacities().Single(row=>row.Kind==CapacityKind.TotalCustomers).Used,Is.EqualTo(1));
                    var tokens=customers.Capacity.GetReservations().ToArray();Assert.That(tokens.Length,Is.EqualTo(nextTokens.Length));
                    foreach(var token in nextTokens){var actual=tokens.Single(row=>ReferenceEquals(row.Token,token.Token));Assert.That(actual.Kind,Is.EqualTo(token.Kind));
                        Assert.That(actual.OwnerId,Is.EqualTo(token.OwnerId));Assert.That(actual.State,Is.EqualTo(token.State));}
                    var snapshot=Read(core,"snapshot") as TrafficWorldSnapshot;Assert.That(snapshot,Is.Not.Null);
                    Assert.That(snapshot.Bodies.Any(body=>body.Owner.ActorId==unborn.ActorId)||snapshot.Parking.Any(claim=>claim.Owner.ActorId==unborn.ActorId),Is.False,"No pseudo unborn body/parking fact");
                    if(noCore){Assert.That(Read(anchor,"Submitted"),Is.EqualTo(false),"FEATURE RED: cached real-front NEXT must hold the SAME opportunity before Submit");
                        Assert.That(entries.Contains(unborn),Is.False,"FEATURE RED: readiness delay cannot mint a Core intent/WAIT/permission for this unborn opportunity");}
                }
                // This actual last Native leg may complete while the real owner is frozen; no pose/Arrived/claim is written.
                yield return Run(Until(()=>(CustomerVisitState)Read(live,"State")==CustomerVisitState.Queued&&counter.Snapshot[0].Arrived,Math.Min(6000,remaining)));
                Assert.That(counter.TryCaptureAssignment(front,out var arrivedClaim)&&arrivedClaim.IsCurrent&&enteringClaim.IsSameReservation(arrivedClaim),Is.True);
                Assert.That(Read(live,"Actor"),Is.SameAs(life));Assert.That(Read(live,"Admission"),Is.SameAs(admission));Assert.That(Read(live,"PickupAssigned"),Is.EqualTo(false));Assert.That(cachedRows.Count,Is.Zero);
                // Real Counter arrival occupies its original token; lock the negative baseline only after verifying this one transition.
                // 真实到站仅允许原CounterQueue token Reserved -> Occupied，之后负向阶段仍要求全部状态固定。
                var arrivedTokens=customers.Capacity.GetReservations().ToArray();Assert.That(arrivedTokens.Length,Is.EqualTo(nextTokens.Length));var counterTransitions=0;
                foreach(var token in nextTokens)
                {
                    var actual=arrivedTokens.Single(row=>ReferenceEquals(row.Token,token.Token));Assert.That(actual.Kind,Is.EqualTo(token.Kind));Assert.That(actual.OwnerId,Is.EqualTo(token.OwnerId));
                    if(token.OwnerId==front&&token.Kind==CapacityKind.CounterQueue)
                    {Assert.That(token.State,Is.EqualTo(ReservationState.Reserved));Assert.That(actual.State,Is.EqualTo(ReservationState.Occupied));counterTransitions++;}
                    else Assert.That(actual.State,Is.EqualTo(token.State));
                }
                Assert.That(counterTransitions,Is.EqualTo(1));nextTokens=arrivedTokens;
                var bodyClearance=(float)Read(anchor,"Radius")+actor.Settings.AgentRadius+Mathf.Max((float)Read(anchor,"Safety"),actor.Settings.CollisionSkin+actor.Settings.Epsilon);
                Assert.That(Vector3.Distance(actor.transform.position,oldJoin.Position),Is.GreaterThanOrEqualTo(bodyClearance),"Actual body must clear the exact Core endpoint pair; cached candidate spacing separately includes ArrivalDistance");
                Assert.That(customers.TrySpawn(),Is.False);SameUnborn(true); // A8 RED: actual head/route are now clear but the cached NEXT is still unowned.
                var fixedPoses=roster.Select(body=>(Actor:body,Pose:body.transform.position)).ToArray();
                Time.captureDeltaTime=0;Time.timeScale=0;
                for(var transition=0;transition<3&&Time.deltaTime!=0;transition++){Assert.That(remaining--,Is.GreaterThan(0));yield return null;}
                Assert.That(Time.deltaTime,Is.Zero);CafeLoopRuntimeProof.Set(owner,"stepping",false);
                for(var frame=0;frame<3;frame++)
                {
                    Assert.That(customers.TrySpawn(),Is.False);SameUnborn(true);Assert.That(cachedRows.Count,Is.Zero);
                    foreach(var pose in fixedPoses)Assert.That(pose.Actor.transform.position,Is.EqualTo(pose.Pose));
                    Assert.That(remaining--,Is.GreaterThan(0));yield return null;
                }
                preparationHook=typeof(CustomerFlowController).GetField("AdmissionPreparationObserver",flags);preparationPrior=preparationHook.GetValue(customers) as Delegate;
                Action<TrafficRequest> attempted=request=>{try{if(request.Intent.Equals(unborn))preparationCalls++;}catch(Exception error){if(errors.Count<4)errors.Add(error.ToString());}};
                preparationInstalled=Delegate.Combine(preparationPrior,attempted);preparationHook.SetValue(customers,preparationInstalled);
                QueueAssignment standing=default;
                standingObserver=ignored=>
                {
                    if(!observing||!watchStanding||firstStandingSeen)return;
                    try{if(cached.TryGetReservation(front,out var actual)&& (bool)Read(live,"PickupAssigned"))
                        {standing=actual;firstStandingSeen=true;Time.captureDeltaTime=0;Time.timeScale=0;}}
                    catch(Exception error){if(errors.Count<4)errors.Add(error.ToString());}
                };
                owner.SnapshotChanged+=standingObserver;watchStanding=true;Time.captureDeltaTime=savedCapture;Time.timeScale=savedScale;
                yield return Run(Until(()=>firstStandingSeen,Math.Min(6000,remaining)));
                Assert.That(errors,Is.Empty,string.Join("\n",errors));Assert.That(standing.Position,Is.EqualTo(plannedPickup.Position));
                for(var transition=0;transition<3&&Time.deltaTime!=0;transition++){Assert.That(remaining--,Is.GreaterThan(0));yield return null;}
                Assert.That(Time.deltaTime,Is.Zero);Assert.That(counter.TryPrepareMutation(CounterQueueService.MutationKind.Join,unborn.ActorId,counter.Version,out var occupiedJoin),Is.True);
                var snapshotOwned=Read(core,"snapshot") as TrafficWorldSnapshot;Assert.That(snapshotOwned,Is.Not.Null);
                var fact=snapshotOwned.Parking.Single(claim=>claim.ResourceId=="pickup/"+front&&ReferenceEquals(claim.CanonicalOwner,cached)&&ReferenceEquals(claim.BusinessOwner,live));
                var pair=(float)Read(anchor,"Radius")+fact.Radius+Mathf.Max((float)Read(anchor,"Safety"),fact.SafetyMargin);
                Assert.That(Vector3.Distance(occupiedJoin.Assignment.Position,fact.Position),Is.LessThan(pair),"PremiseNotProven: exact current Join must still collide with the REAL canonical pickup claim");
                var attempts=preparationCalls;Assert.That(customers.TrySpawn(),Is.False);Assert.That(preparationCalls,Is.EqualTo(attempts+1),"Original public caller must reach the existing endpoint preparation, not an unrelated top-level false");
                // Canonical pickup now exists: the original Core endpoint protection, not a fabricated future-body rule, keeps it unborn.
                Assert.That(Read(anchor,"Submitted"),Is.EqualTo(false));Assert.That(entries.Contains(unborn),Is.False);
                Assert.That(Read(customers,"pendingAdmissionAnchor"),Is.SameAs(anchor));Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.EqualTo(before));
                watchStanding=false;Time.captureDeltaTime=savedCapture;Time.timeScale=savedScale;
                yield return Run(Until(()=>counter.Snapshot.Count==0,Math.Min(6000,remaining))); // Real paid departure changes Join slot1 -> current empty slot0.
                Assert.That(counter.TryPrepareMutation(CounterQueueService.MutationKind.Join,unborn.ActorId,counter.Version,out var clearJoin),Is.True);
                Assert.That(clearJoin.Assignment.SlotIndex,Is.Zero);Assert.That(clearJoin.Assignment.Position,Is.Not.EqualTo(oldJoin.Position));
                Assert.That(Read(customers,"pendingAdmissionAnchor"),Is.SameAs(anchor));Assert.That(Read(anchor,"FirstFrame"),Is.EqualTo(firstFrame));
                var entryBorn=false;object entry=null;var queries=0;var sequence=0L;var wait=0f;var waited=false;var finite=0f;var born=false;
                var birthWindow=Math.Min(6000,remaining);
                for(var frame=0;frame<birthWindow&&!born;frame++)
                {
                    Assert.That(errors,Is.Empty,string.Join("\n",errors));Assert.That(Read(customers,"pendingAdmissionAnchor"),Is.SameAs(anchor));
                    Assert.That(Read(anchor,"Key"),Is.EqualTo(unborn));Assert.That(Read(anchor,"FirstFrame"),Is.EqualTo(firstFrame));
                    born=customers.TrySpawn(); // Same opportunity; false is only preparation, never declared a new spatial refusal.
                    if(entries.Contains(unborn))
                    {
                        if(!entryBorn){entryBorn=true;entry=entries[unborn];finite=(float)Read(entry,"FiniteLimit");}
                        Assert.That(entries[unborn],Is.SameAs(entry));Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(firstFrame));Assert.That(Read(entry,"FiniteLimit"),Is.EqualTo(finite));
                        var q=(int)Read(entry,"Queries");Assert.That(q,Is.InRange(queries,16384));queries=q;Assert.That(core.QueriesThisFrame,Is.LessThanOrEqualTo(128));
                        var seq=(long)Read(entry,"RequestSequence");if(sequence==0&&seq>0)sequence=seq;Assert.That(seq,Is.EqualTo(sequence));
                        var debt=(float)Read(entry,"WaitRemaining");if(waited)Assert.That(debt,Is.LessThanOrEqualTo(wait));if(debt>0)waited=true;wait=debt;
                        var decision=Read(entry,"Decision") as TrafficDecision;Assert.That(decision.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None),decision.Reason);
                    }
                    if(!born){Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.EqualTo(before));Assert.That(world.RegisteredActors.Any(body=>body.ActorId==unborn.ActorId),Is.False);
                        Assert.That(customers.Capacity.GetReservations().Any(row=>row.OwnerId==unborn.ActorId),Is.False);Assert.That(remaining--,Is.GreaterThan(0));yield return null;}
                }
                Assert.That(born,Is.True,"Same original opportunity must prepare/commit when the ACTUAL Join becomes safe; no unconditional wait for front scene Exit");
                var second=customers.Snapshot.Single(row=>row.VisitId==unborn.ActorId).VisitId;Track(second,world);
                Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.EqualTo(before+1));Assert.That(entryBorn,Is.True);
                Assert.That(core.TryGetCommittedJourney(unborn,out var birthSource),Is.True);Assert.That(birthSource.AcquiredSequence,Is.GreaterThan(0));
                var birthBeforeFrontExit=!exits.Contains(front);
                yield return Run(Until(()=>customers.Snapshot.Count==0&&exits.Count==3&&customers.Capacity.GetCapacities().All(row=>row.Used==0)&&
                    employees.Snapshot.All(row=>row.State==EmployeeState.Idle),Math.Min(6000,remaining)));
                Assert.That(errors,Is.Empty,string.Join("\n",errors));CollectionAssert.AreEquivalent(new[]{prefix,front,second},exits);
                Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(3));Assert.That(owner.Snapshot.Orders.Values.All(order=>order.State==OrderState.Completed),Is.True);
                Assert.That(CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue"),Is.SameAs(cached));
                TestContext.WriteLine("CACHED_REAL_FRONT_ADMISSION prefixActualExit=True sameCachedQueue=True originalConflict="+Vector3.Distance(oldJoin.Position,plannedPickup.Position).ToString("R")+
                    " cachedCandidateClearance="+envelope.ToString("R")+" liveBodyEndpointClearance="+bodyClearance.ToString("R")+
                    " sameAnchor=True noCoreBeforeFrontReserve=True realCanonicalEndpointHeld=True actualJoinChangedToSlot0=True birthBeforeFrontExit="+birthBeforeFrontExit+
                    " actualOrders=3 actualExits=3 totals=0 framesUsed="+(12000-remaining));
            }
            finally
            {
                observing=false;if(standingObserver!=null)owner.SnapshotChanged-=standingObserver;foreach(var observer in observers)if(observer!=null)observer.Receipt=null;
                if(preparationInstalled!=null){var current=preparationHook.GetValue(customers);if(ReferenceEquals(current,preparationInstalled))preparationHook.SetValue(customers,preparationPrior);
                    else if(!ReferenceEquals(current,preparationPrior))errors.Add("UNKNOWN: AdmissionPreparationObserver replacement preserved");}
                if(owner!=null)CafeLoopRuntimeProof.Set(owner,"stepping",savedStepping);Time.captureDeltaTime=savedCapture;Time.timeScale=savedScale;
            }
            Assert.That(errors,Is.Empty,string.Join("\n",errors));
        }

        [UnityTest] public IEnumerator ReusedPickupCandidatesKeepOriginalGeometryAndTwoRealGuestsDrain()
        {
            yield return OpenPickupLayoutForProjectionCapacity();
            InitializeLegacyCachedPickupInput(); // Explicit old cache input; every later source/row/lease/exit remains real.
            var world=Find<NavigationWorld>();tools.ResumeCollection();
            var observers=new System.Collections.Generic.List<CafeActualExitObserver>();
            var exits=new System.Collections.Generic.HashSet<string>();var observing=true;
            var savedClock=CafeLoopRuntimeProof.Field<CustomerSpawnClock>(customers,"clock");
            void Track(string id)
            {
                var actor=world.RegisteredActors.Single(body=>body.ActorId==id);
                var total=customers.Capacity.GetReservations().Single(row=>row.OwnerId==id && row.Kind==CapacityKind.TotalCustomers).Token;
                var observer=actor.gameObject.AddComponent<CafeActualExitObserver>();observer.VisitId=id;
                observer.Receipt=(leaving,pose)=>
                {
                    if(!observing)return;
                    Assert.That(leaving,Is.EqualTo(id));
                    Assert.That(Vector3.Distance(pose,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position),Is.LessThanOrEqualTo(.081f));
                    Assert.That(customers.Capacity.GetReservations().Single(row=>ReferenceEquals(row.Token,total)).State,Is.EqualTo(ReservationState.Released));
                    Assert.That(owner.Snapshot.Orders.Values.Single(order=>order.CustomerId==id).State,Is.EqualTo(OrderState.Completed));
                    Assert.That(exits.Add(id),Is.True);
                };observers.Add(observer);
            }
            try
            {
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                Assert.That(customers.TrySpawn(),Is.True,customers.Status);var prefix=customers.Snapshot.Single().VisitId;Track(prefix);
                yield return Until(()=>customers.Snapshot.Count==0 && exits.Contains(prefix) && employees.Snapshot.All(row=>row.State==EmployeeState.Idle),6000);
                Assert.That(exits,Has.Member(prefix));
                var retainedQueue=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue");Assert.That(retainedQueue,Is.Not.Null);
                var slots=CafeLoopRuntimeProof.Field<Vector3[]>(customers,"pickupWaitSlots");
                var counter=CafeLoopRuntimeProof.Field<Vector3[]>(customers,"counterWaitSlots");
                var prefabs=CafeLoopRuntimeProof.Field<NavigationActor[]>(customers,"prefabs");
                var clearance=prefabs.Max(body=>2*body.Settings.AgentRadius+body.Settings.CollisionSkin+body.Settings.Epsilon+body.Settings.ArrivalDistance);
                Assert.That(customers.CounterAssignments,Is.Empty,"No future Counter tail has an owner at this geometry checkpoint");
                Assert.That(slots,Is.Not.Empty);Assert.That(counter.Length,Is.GreaterThan(1));
                Assert.That(slots.Any(slot=>counter.Any(point=>Vector3.Distance(point,slot)<clearance)),Is.True,
                    "Original cached geometry retains a hypothetical future-lane overlap; actual admissions must remain safe at runtime");
                // One-second valid arrival input; all movement/work/payment stays on the normal driver.
                // 只固定合法到店输入，不改时间、角色、站位、工作或Core预算。
                CafeLoopRuntimeProof.Set(customers,"clock",new CustomerSpawnClock(()=>.1f));tools.HoldCollection();tools.ResumeArrivals();
                yield return Until(()=>customers.Snapshot.Count==2,6000);tools.HoldArrivals();
                var wave=customers.Snapshot.Select(row=>row.VisitId).ToArray();foreach(var id in wave)Track(id);
                Assert.That(CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue"),Is.SameAs(retainedQueue));
                yield return Until(()=>owner.Snapshot.Delivery.Count==2 && employees.Snapshot.All(row=>row.State==EmployeeState.Idle),6000);
                Assert.That(wave.All(id=>owner.Snapshot.PaidVisitIds.Contains(id)),Is.True);
                Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(3));tools.ResumeCollection();
                yield return Until(()=>customers.Snapshot.Count==0 && exits.Count==3 && customers.Capacity.GetCapacities().All(row=>row.Used==0) && employees.Snapshot.All(row=>row.State==EmployeeState.Idle),6000);
                CollectionAssert.AreEquivalent(wave.Append(prefix),exits);
                Assert.That(owner.Snapshot.Orders.Values.All(row=>row.State==OrderState.Completed),Is.True);
                TestContext.WriteLine("REUSED_PICKUP_ORIGINAL_GEOMETRY actualPrefixExit=True cachedQueueReused=True originalGeometryRetained=True hypotheticalOverlap=True runtimeAdmissionProtection=True wavePaid=2 waveActualExits=2 originalTotalsReleased=True");
            }
            finally
            {
                observing=false;foreach(var observer in observers)if(observer!=null)observer.Receipt=null;
                CafeLoopRuntimeProof.Set(customers,"clock",savedClock);tools.HoldArrivals();
            }
        }
        // Compatibility input for these two cached-owner regressions, before any actual business publication.
        // 仅初始化旧格式空缓存；不是initial-blueprint算法测试，发布后绝不改slots/owner/actor/source。
        private void InitializeLegacyCachedPickupInput()
        {
            Assert.That(customers.Snapshot,Is.Empty);Assert.That(owner.Snapshot.Orders,Is.Empty);Assert.That(customers.CounterAssignments,Is.Empty);
            Assert.That(customers.Capacity.GetReservations(),Is.Empty);Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.Zero);
            Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits").Count,Is.Zero);
            Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(customers,"trafficCandidates").Count,Is.Zero);
            Assert.That(CafeLoopRuntimeProof.Field<object>(customers,"pendingAdmissionAnchor"),Is.Null);
            Assert.That(CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue"),Is.Null);
            var runtime=Find<CafeLayoutRuntime>();var world=Find<NavigationWorld>();var adapter=Find<NavigationLayoutAdapter>();
            var pickupId=runtime.FunctionalSurfaceLayout.PickUpPoints.Single().InstanceId;
            Assert.That(customers.TryConfigurePickupFromLayout(pickupId,out var reason),Is.True,reason);
            Assert.That(CafeLoopRuntimeProof.Field<object>(customers,"pickupBlueprint"),Is.Null);
            var sides=customers.GetPickupServiceTargets(pickupId).ToArray();Assert.That(sides.Length,Is.EqualTo(4));
            var side=sides.Single(target=>Vector3.Distance(target.Position,new Vector3(-.5f,0,.5f))<.001f);
            Assert.That(side.Facing.HasValue,Is.True);var outward=-side.Facing.Value;var lateral=new Vector3(-outward.z,0,outward.x);
            var head=side.Position+outward*1.1f+lateral*1.1f;
            Assert.That(Vector3.Distance(head,new Vector3(-1.6f,0,-.6f)),Is.LessThan(.001f),"Original formal legacy head, derived from its real side");
            var prefabs=CafeLoopRuntimeProof.Field<NavigationActor[]>(customers,"prefabs");
            var clearance=prefabs.Max(body=>2*body.Settings.AgentRadius+body.Settings.CollisionSkin+body.Settings.Epsilon+body.Settings.ArrivalDistance);
            var arrival=prefabs.Min(body=>body.Settings.ArrivalDistance+body.Settings.Epsilon);
            var grid=CafeLoopRuntimeProof.Field<Transform>(customers,"gridRoot");var size=runtime.Layout.GridSettings.CellSize;
            bool InEntrance(Vector3 point)
            {
                var local=grid.InverseTransformPoint(point);
                return runtime.Layout.Reservations.Any(row=>row.Type==LayoutReservationType.EntranceClearance &&
                    local.x>=row.Origin.X*size-.46f && local.x<=(row.Origin.X+row.Size.Width)*size+.46f &&
                    local.z>=row.Origin.Y*size-.46f && local.z<=(row.Origin.Y+row.Size.Height)*size+.46f);
            }
            var exit=CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position;
            var slots=new CounterQueuePlanner(1.1f,preserveStraightPrefix:true).Build(head,outward,customers.Capacity.Limits.PickUp,
                (point,previous)=>adapter.IsPointArrivable(point,arrival) && !InEntrance(point) && Vector3.Distance(point,exit)>=1.2f &&
                    sides.All(target=>Vector3.Distance(point,target.Position)>=clearance) &&
                    world.RegisteredActors.All(actor=>actor==null || Vector3.Distance(actor.transform.position,point)>=clearance) &&
                    (previous==null || adapter.HasClearPath(previous.Value,point)) && adapter.HasClearPath(point,side.Position),
                sampleEdge:(from,to)=>{var path=adapter.GetCompletePath(from,to);return path==null || path.Count==0?(Vector3?)null:path[path.Count-1];}).ToArray();
            Assert.That(slots.Length,Is.GreaterThanOrEqualTo(2),"Legacy cache must support the unchanged two-paid-guest proof");
            Assert.That(slots[0],Is.EqualTo(head));var cached=new PickupQueueService(slots,sides.Select(target=>target.Position).ToArray());
            Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(cached,"reservations").Count,Is.Zero);Assert.That(cached.ServiceLeases,Is.Empty);
            Assert.That(cached.Snapshot,Is.Empty);Assert.That(cached.Version,Is.Zero);
            CafeLoopRuntimeProof.Set(customers,"pickupWaitSlots",slots);CafeLoopRuntimeProof.Set(customers,"pickupQueue",cached);
            Assert.That(CafeLoopRuntimeProof.Field<object>(customers,"pickupBlueprint"),Is.Null);
            Assert.That(customers.Snapshot,Is.Empty);Assert.That(owner.Snapshot.Orders,Is.Empty);Assert.That(customers.Capacity.GetReservations(),Is.Empty);
            TestContext.WriteLine("LEGACY_CACHED_PICKUP_INPUT formalPickup="+pickupId+" originalHead="+head.ToString("F6")+
                " slotCount="+slots.Length+" beforeFirstVisit=True rows=0 leases=0 blueprint=NULL noPublishedOwnerRewrite=True");
        }
        // Fixed formal layout already proved by MatrixOpen: this fixture tests five-order projection/FIFO.
        // Move the same pickup support before business; no body, order, payment, speed or budget mutation.
        private IEnumerator OpenPickupLayoutForProjectionCapacity()
        {
            Assert.That(customers.Snapshot,Is.Empty);Assert.That(owner.Snapshot.Orders,Is.Empty);
            var runtime=Find<CafeLayoutRuntime>();var decor=Find<DecorationModeController>();
            var point=runtime.FunctionalSurfaceLayout.PickUpPoints.Single();var address=point.Address;
            var support=address.SupportFurnitureInstanceId;
            decor.EnterDecorationMode();yield return null;
            var session=CafeLoopRuntimeProof.Field<DecorationSession>(decor,"session");
            Assert.That(session.BeginExisting(support).Succeeded,Is.True);
            Assert.That(session.MovePreview(new GridPosition(4,4)).Succeeded,Is.True);
            yield return null;
            typeof(DecorationModeController).GetMethod("HandleConfirmRequested",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(decor,null);
            Assert.That(session.ActivePreview,Is.Null,"Actual Controller placement/Nav guards must accept the fixed open layout");
            decor.ExitDecorationMode();yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(runtime.FunctionalSurfaceLayout.PickUpPoints.Single().InstanceId,Is.EqualTo(point.InstanceId));
            Assert.That(runtime.FunctionalSurfaceLayout.PickUpPoints.Single().Address,Is.EqualTo(address));
            Assert.That(customers.GetPickupServiceTargets(point.InstanceId).Count,Is.EqualTo(4),"Actual four-sided projection fixture");
            Assert.That(runtime.Layout.FurnitureInstances.Single(f=>f.InstanceId==support).Position,Is.EqualTo(new GridPosition(4,4)));
            Assert.That(customers.Snapshot,Is.Empty);Assert.That(owner.Snapshot.Orders,Is.Empty);
            TestContext.WriteLine("STATUS_PROJECTION_LAYOUT fixedPickupGrid=4,4 sameMount=True beforeFirstVisit=True originalFiveOrderAndFifoAssertions=True");
        }
        private IEnumerator FiveDeliveriesAndDrain()
        {
            if(scene.path==ValidationPath)yield return OpenPickupLayoutForProjectionCapacity();
            InstallOriginalUnpaidFailureObserver();
            var traced=false;
            var simultaneousExits=0;
            owner.SnapshotChanged+=_=>
            {
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var visits=(System.Collections.IDictionary)typeof(CustomerFlowController).GetField("visits",flags).GetValue(customers);
                simultaneousExits=Math.Max(simultaneousExits,visits.Values.Cast<object>().Count(v=>
                    (CustomerVisitState)v.GetType().GetField("State").GetValue(v)==CustomerVisitState.Exiting && (long)v.GetType().GetField("RequestId").GetValue(v)!=0));
            };
            owner.SnapshotChanged+=value=> { if(!traced && (value.Status=="Blocked" || customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked))) { traced=true; tools.PrintSnapshot(); DumpRoutes(); } };
            tools.HoldCollection(); tools.ResumeArrivals();
            yield return Until(()=>customers.Snapshot.Count>=5,12000); tools.HoldArrivals();
            InstallFiveCachedQueueSnapshot();fiveCachedQueueSnapshot?.Invoke("five-visits-held");
            var admitted=customers.Snapshot.Select(v=>v.VisitId).ToArray();
            yield return Until(()=>owner.Snapshot.Delivery.Count>=5,12000);
            Assert.That(owner.Snapshot.Delivery.Count,Is.EqualTo(5));
            tools.PrintSnapshot();
            foreach(var slot in customers.PickupAssignments) TestContext.WriteLine("ACTUAL_WAIT "+scene.path+" "+slot.VisitId+" "+slot.Position.ToString("F3"));
            foreach(var role in new[]{EmployeeRole.Cashier,EmployeeRole.Barista})
            {
                Assert.That(employees.TryGetBinding(role,out var actualBinding),Is.True);
                TestContext.WriteLine("ACTUAL_BINDING "+scene.path+" "+role+" work="+actualBinding.WorkTarget.Position.ToString("F3")+
                    " pickup="+(actualBinding.PickUpTarget.HasValue?actualBinding.PickUpTarget.Value.Position.ToString("F3"):"none"));
            }
            var grid=(Transform)new SerializedObject(employees).FindProperty("gridRoot").objectReferenceValue;
            TestContext.WriteLine("ACTUAL_GRID "+grid.position.ToString("F3")+" "+grid.rotation.eulerAngles.ToString("F3"));
            Assert.That(Find<PickupCupDisplay>().VisibleOrders.Count,Is.EqualTo(3));
            Assert.That(customers.PickupAssignments.Count,Is.EqualTo(5));
            Assert.That(customers.Snapshot.All(v=>v.State!=CustomerVisitState.PickupService && v.State!=CustomerVisitState.MovingToPickupService),Is.True);
            var ids=owner.Snapshot.PaidVisitIds.ToArray(); CollectionAssert.AreEquivalent(admitted,ids);
            var display=Find<PickupCupDisplay>();
            CollectionAssert.AreEquivalent(owner.Snapshot.Delivery.OrderBy(d=>d.DeliverySequence).Take(3).Select(d=>d.Key),display.VisibleOrders.Keys);
            Assert.That(scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Count(t=>t.name.StartsWith("PickupCup_")),Is.EqualTo(3));
            if(Environment.GetEnvironmentVariable("ANIMALCAFE_P14_CAPTURE")=="true" && scene.path==ValidationPath)
                yield return CaptureStatusScreens();
            var completed=new System.Collections.Generic.List<string>();
            owner.SnapshotChanged+=value=>
            {
                foreach(var order in value.Orders.Values.Where(o=>o.State==OrderState.Completed)) if(!completed.Contains(order.CustomerId)) completed.Add(order.CustomerId);
                CollectionAssert.AreEquivalent(value.Delivery.OrderBy(d=>d.DeliverySequence).Take(3).Select(d=>d.Key),display.VisibleOrders.Keys);
            };
            tools.ResumeCollection();
            yield return Until(()=>owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed) && customers.Snapshot.Count==0,12000);
            CollectionAssert.AreEqual(ids,completed,"Actual Completed milestones preserve paid FIFO");
            TestContext.WriteLine("P14_MAX_CONCURRENT_EXIT_REQUESTS "+scene.path+" "+simultaneousExits);
            Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True); Assert.That(owner.Snapshot.Delivery,Is.Empty);
        }
        [UnityTest] public IEnumerator RealOwnedBlockersPauseDeliveryReturnAndExitWithoutLosingBusiness()
        {
            var world=Find<NavigationWorld>(); var staff=world.RegisteredActors.ToArray(); tools.HoldCollection();
            tools.ResumeArrivals(); yield return Until(()=>customers.Snapshot.Count==1); tools.HoldArrivals();
            yield return Until(()=>employees.Snapshot.Any(e=>e.State==EmployeeState.CarryingToPickUp));
            tools.BlockCurrentDeliveryRoute(); Assert.That(tools.Feedback,Does.StartWith("Registered owned"));
            Assert.That(world.RegisteredActors.Count,Is.EqualTo(4));
            yield return Until(()=>employees.Snapshot.Any(e=>e.Role==EmployeeRole.Barista && e.State==EmployeeState.Blocked),6000);
            Assert.That(owner.Snapshot.Delivery,Is.Empty);
            Assert.That(owner.Snapshot.Employees.Single(e=>e.Role==EmployeeRole.Barista).Stage,Is.EqualTo(CafeWorkStage.Prepared));
            tools.RemoveTestBlockers(); tools.ResumeBlockedFlow();
            yield return Until(()=>owner.Snapshot.Delivery.Count==1 && employees.Snapshot.Any(e=>e.State==EmployeeState.Returning));
            tools.BlockCurrentReturnRoute(); Assert.That(tools.Feedback,Does.StartWith("Registered owned"));
            yield return Until(()=>employees.Snapshot.Any(e=>e.Role==EmployeeRole.Barista && e.State==EmployeeState.Blocked),6000);
            Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1)); Assert.That(owner.Snapshot.Delivery.Count,Is.EqualTo(1));
            tools.RemoveTestBlockers(); tools.ResumeBlockedFlow();
            yield return Until(()=>employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            tools.ResumeCollection(); yield return Until(()=>owner.Snapshot.CollectedExitVisitIds.Count==1);
            tools.BlockCurrentExitRoute(); Assert.That(tools.Feedback,Does.StartWith("Registered owned"));
            yield return Until(()=>customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked),6000);
            Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.Completed));
            tools.RemoveTestBlockers(); tools.ResumeBlockedFlow();
            yield return Until(()=>customers.Snapshot.Count==0);
            CollectionAssert.AreEquivalent(staff,world.RegisteredActors);
            Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1)); Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
        }
        private IEnumerator CaptureStatusScreens()
        {
            Assert.That(Application.isBatchMode,Is.False,"Capture requires the actual graphics GameView");
            time.SetPaused(); yield return null;
            using(var screen=new P8RReferenceLayoutTests.RealGameViewSize())
            foreach(var size in new[]{new Vector2(1080,1920),new Vector2(1600,720)})
            {
                screen.Resize(size); yield return null; yield return null; Canvas.ForceUpdateCanvases();
                Assert.That(new Vector2(Screen.width,Screen.height),Is.EqualTo(size));
                Assert.That(UnityEngine.Camera.main.targetTexture,Is.Null);
                var deadline=Time.realtimeSinceStartup+45;
                while(ShaderUtil.anythingCompiling && Time.realtimeSinceStartup<deadline) yield return null;
                Assert.That(ShaderUtil.anythingCompiling,Is.False);
                var text=(UnityEngine.UI.Text)new SerializedObject(Find<CafeLoopStatusView>()).FindProperty("status").objectReferenceValue;
                Assert.That(text.cachedTextGenerator.lineCount,Is.GreaterThanOrEqualTo(9));
                foreach(var button in Find<AnimalCafe.UI.TimeControlPanel>().GetComponentsInChildren<UnityEngine.UI.Button>().Where(b=>b.isActiveAndEnabled))
                {
                    var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                        {position=RectTransformUtility.WorldToScreenPoint(null,((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center))};
                    var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
                    Assert.That(hits.First().gameObject.GetComponentInParent<UnityEngine.UI.Button>(),Is.SameAs(button),button.name+" remains unobscured");
                }
                var directory=Environment.GetEnvironmentVariable("ANIMALCAFE_P14_CAPTURE_DIRECTORY")??"outputs/phase14/task8-screens";
                Directory.CreateDirectory(directory);
                var path=Path.GetFullPath(directory+"/status-"+(int)size.x+"x"+(int)size.y+".png");
                Assert.That(File.Exists(path),Is.False); ScreenCapture.CaptureScreenshot(path);
                deadline=Time.realtimeSinceStartup+10; while(!File.Exists(path) && Time.realtimeSinceStartup<deadline) yield return null;
                var pixels=new Texture2D(2,2);
                try { Assert.That(pixels.LoadImage(File.ReadAllBytes(path)),Is.True); Assert.That(new Vector2(pixels.width,pixels.height),Is.EqualTo(size)); }
                finally { Object.Destroy(pixels); }
                TestContext.WriteLine("P14_NATIVE_SCREEN "+path+" "+size);
            }
            Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True);
        }
        [UnityTest] public IEnumerator StatusIsReadonlyAndReportsActualPauseAndDiagnosticFields()
        {
            var view=Find<CafeLoopStatusView>(); view.Bind(owner); view.Bind(owner);
            var epoch=owner.Snapshot.Epoch; var counts=customers.Capacity.GetCapacities().Select(c=>c.Used).ToArray();
            time.SetPaused(); yield return null;
            Assert.That(view.CurrentText,Does.Contain("Paused")); Assert.That(view.CurrentText,Does.Contain("Counter"));
            Assert.That(view.CurrentText,Does.Contain("Pickup")); Assert.That(view.CurrentText,Does.Contain("Cashier"));
            Assert.That(view.CurrentText,Does.Contain("Barista")); Assert.That(view.CurrentText,Does.Contain("Total"));
            Assert.That(owner.Snapshot.Epoch,Is.EqualTo(epoch)); Assert.That(customers.Capacity.GetCapacities().Select(c=>c.Used),Is.EqualTo(counts));
            owner.enabled=false; Assert.That(view.CurrentText,Does.Contain("Held"),"Owner disable must publish its final readonly hold fact");
            yield return null; Assert.That(view.CurrentText,Does.Contain("Held"));
            owner.enabled=true; Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True); owner.ResumeBlockedFlow();
            Assert.That(view.CurrentText,Does.Not.Contain("Held"),"Explicit resume refreshes the readonly reception immediately");
        }
        [UnityTest] public IEnumerator PendingExitSegmentFreezesOnPauseAndRetiresEachCollectedVisitOnce()
        {
            tools.HoldCollection(); tools.ResumeArrivals(); yield return Until(()=>customers.Snapshot.Count==2); tools.HoldArrivals();
            yield return Until(()=>owner.Snapshot.Delivery.Count==2 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle) && customers.PickupAssignments.All(q=>q.Arrived));
            var paid=owner.Snapshot.PaidVisitIds.ToArray(); var first=paid[0]; var second=paid[1];
            tools.ResumeCollection(); yield return Until(()=>owner.Snapshot.CollectedExitVisitIds.Contains(first)); tools.HoldCollection();
            Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==second).State,Is.EqualTo(OrderState.ReadyForPickup));
            yield return Until(()=>customers.PickupAssignments.Count>0 && customers.PickupAssignments.OrderBy(q=>q.SlotIndex).First().VisitId==second);
            // HoldCollection prevents owner dispatch; use the same public guarded port to stage
            // the real next head at its service side before the earlier body reaches the exit.
            yield return Until(()=>customers.TryMoveToPickupService(second,out _));

            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var visitField=typeof(CustomerFlowController).GetField("visits",flags);
            var exit=(Transform)typeof(CustomerFlowController).GetField("exit",flags).GetValue(customers);
            var pausedNearGoal=false; var pausedPending=false;
            owner.SnapshotChanged+=_=>
            {
                var visits=(System.Collections.IDictionary)visitField.GetValue(customers);
                if(!pausedNearGoal && visits.Contains(first) && customers.CanCollectPickup(second))
                {
                    var prior=visits[first]; var type=prior.GetType(); var actor=(NavigationActor)type.GetField("Actor").GetValue(prior);
                    if((long)type.GetField("RequestId").GetValue(prior)!=0 && Vector3.Distance(actor.transform.position,exit.position)<.7f)
                    { pausedNearGoal=true; time.SetPaused(); }
                }
                if(pausedNearGoal && !pausedPending && visits.Contains(second) &&
                    (bool)visits[second].GetType().GetField("PendingExitSegment").GetValue(visits[second]))
                { pausedPending=true; time.SetPaused(); }
            };
            yield return Until(()=>pausedNearGoal);
            tools.ResumeCollection(); Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True);
            yield return Until(()=>pausedPending);
            var current=((System.Collections.IDictionary)visitField.GetValue(customers))[second]; var currentType=current.GetType();
            var generation=(int)currentType.GetField("Generation").GetValue(current);
            var segments=(int)currentType.GetField("Segments").GetValue(current); var replans=(int)currentType.GetField("Replans").GetValue(current);
            var position=((NavigationActor)currentType.GetField("Actor").GetValue(current)).transform.position;
            for(var frame=0;frame<6;frame++)
            {
                yield return null; Assert.That((long)currentType.GetField("RequestId").GetValue(current),Is.Zero);
                Assert.That(currentType.GetField("State").GetValue(current),Is.EqualTo(CustomerVisitState.Exiting));
                Assert.That((int)currentType.GetField("Generation").GetValue(current),Is.EqualTo(generation));
                Assert.That((int)currentType.GetField("Segments").GetValue(current),Is.EqualTo(segments));
                Assert.That((int)currentType.GetField("Replans").GetValue(current),Is.EqualTo(replans));
                Assert.That(((NavigationActor)currentType.GetField("Actor").GetValue(current)).transform.position,Is.EqualTo(position));
            }
            Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(2)); Assert.That(owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed),Is.True);
            TestContext.WriteLine("P14_PENDING_EXIT_PAUSE request0 generation="+generation+" segments="+segments+" replans="+replans);
            Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True); yield return Until(()=>customers.Snapshot.Count==0);
            for(var frame=0;frame<6;frame++) yield return null;
            Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(2)); Assert.That(owner.Snapshot.CollectedExitVisitIds,Is.Empty);
            Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
        }
        [UnityTest] public IEnumerator InspectorReplayRejectsCurrentOrUnknownEvent()
        {
            var currentRejected=false;
            employees.IntegratedTaskEvent+=value=>
            {
                if(value.Checkpoint.Stage!=CafeWorkStage.Working) return;
                Assert.That(owner.TryReplayRetiredTask(value,out _),Is.False,"Actual current Working event must not be replayable"); currentRejected=true;
            };
            tools.ResumeArrivals(); yield return Until(()=>owner.Snapshot.Orders.Count>0); tools.HoldArrivals();
            Assert.That(currentRejected,Is.True);
            yield return Until(()=>owner.RetiredTaskEvents.Count>0);
            var before=owner.Snapshot.Orders.Select(p=>p.Key.OrderId+":"+p.Value.State).ToArray();
            Assert.That(owner.TryReplayRetiredTask(default,out _),Is.False);
            Assert.That(owner.TryReplayRetiredTask(owner.RetiredTaskEvents.Last(),out var reason),Is.True,reason);
            Assert.That(owner.Snapshot.Orders.Select(p=>p.Key.OrderId+":"+p.Value.State),Is.EqualTo(before));
        }
        [UnityTest] public IEnumerator CashierNeverStartsWithSlotOnEarlierPickupRemainingPath()
        {
            // A real west-facing straight tail establishes the crossing under the new
            // prefix-preserving planner; Preview/Confirm/Done retain every placement guard.
            // 不写角色或路线；Confirm仍执行正式完整身体/导航guard。
            var decor=Find<DecorationModeController>(); var layout=Find<CafeLayoutRuntime>();
            decor.EnterDecorationMode(); yield return null;
            var session=CafeLoopRuntimeProof.Field<DecorationSession>(decor,"session");
            var support=layout.FunctionalSurfaceLayout.PickUpPoints.Single().Address.SupportFurnitureInstanceId;
            Assert.That(session.BeginExisting(support).Succeeded,Is.True);
            var proposed=session.MovePreview(new GridPosition(6,5)); Assert.That(proposed.Succeeded,Is.True);
            var previewFlags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(DecorationModeController).GetMethod("SyncActivePreviewPresentation",previewFlags).Invoke(decor,null);
            typeof(DecorationModeController).GetMethod("ShowActionForResult",previewFlags).Invoke(decor,new object[]{proposed});
            var actions=CafeLoopRuntimeProof.Field<DecorationActionBarView>(decor,"actionBarView");
            var confirm=CafeLoopRuntimeProof.Field<UnityEngine.UI.Button>(actions,"confirmButton");
            Assert.That(confirm.interactable,Is.True,"Actual Preview must pass the ordinary full-body guard"); confirm.onClick.Invoke();
            Assert.That(session.ActivePreview,Is.Null); Assert.That(layout.Layout.FurnitureInstances.Single(f=>f.InstanceId==support).Position,Is.EqualTo(new GridPosition(6,5)));
            // Force a real west-facing approach: its future straight tail crosses the
            // earlier customer's incoming route, rather than relying on old lateral backtracking.
            foreach(var neighbour in new[]{new GridPosition(6,4),new GridPosition(6,6),new GridPosition(7,5)})
            {
                session.BeginNew("furniture.counter.module.01",neighbour);var legal=session.MovePreview(neighbour);Assert.That(legal.Succeeded,Is.True);
                typeof(DecorationModeController).GetMethod("SyncActivePreviewPresentation",previewFlags).Invoke(decor,null);
                typeof(DecorationModeController).GetMethod("ShowActionForResult",previewFlags).Invoke(decor,new object[]{legal});yield return null;
                Assert.That(confirm.interactable,Is.True);confirm.onClick.Invoke();Assert.That(session.ActivePreview,Is.Null);yield return null;
            }
            decor.ExitDecorationMode(); yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            // Use a fixed one-second interval from the real clock's approved range to establish a repeatable real queue.
            // 仅控制合法到店输入；不写Actor/Route，不伪造业务或Nav回执。
            typeof(CustomerFlowController).GetField("clock",flags).SetValue(customers,new CustomerSpawnClock(()=>.1f));
            T ReadTraffic<T>(object target,string field)=>(T)target.GetType().GetField(field,flags|System.Reflection.BindingFlags.Public).GetValue(target);
            var starts=new System.Collections.Generic.HashSet<long>(); var unsafeStart=false; var explainedWait=false;
            var provedRouteConflict=false; var resumedWaitingHead=false; string waitingHead=null;
            cashierDiagnosticCleanup=BeginCashierOriginalDecisionTrace();
            var originalWaitTrace=Environment.GetCommandLineArgs().Contains("-serviceTrafficWaitTrace")?
                new TrafficWaitFailureTrace(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic")):null;
            var originalDiagnosticCleanup=cashierDiagnosticCleanup;
            cashierDiagnosticCleanup=()=>{try{originalWaitTrace?.Dispose();}finally{originalDiagnosticCleanup?.Invoke();}};
            var beforePublicationCleanup=cashierDiagnosticCleanup;
            Action publicationCleanup=null;
            try{publicationCleanup=BeginCashierPublicationTrace();}
            catch{StopCashierOriginalDecisionTrace();throw;}
            cashierDiagnosticCleanup=()=>{try{publicationCleanup?.Invoke();}finally{beforePublicationCleanup?.Invoke();}};
            owner.SnapshotChanged+=value=>
            {
                cashierDiagnosticSample?.Invoke("snapshot",null);
                if(provedRouteConflict || customers.CounterAssignments.Count==0) return;
                // Bind the actual unpaid front and its original coordinator proposal, not a HUD phrase.
                // 只认当前未付款队首的原proposal；预算排队、未知等待和无关容量等待不算路线冲突。
                var front=customers.CounterAssignments[0]; var head=front.VisitId;
                if(!front.Arrived || value.PaidVisitIds.Contains(head) || value.Orders.Values.Any(order=>order.CustomerId==head) ||
                    CafeLoopRuntimeProof.Field<string>(owner,"pickupWaitVisit")!=head) return;
                var visits=(System.Collections.IDictionary)typeof(CustomerFlowController).GetField("visits",flags).GetValue(customers);
                if(!visits.Contains(head)) return;
                var pendingVisit=visits[head];
                if(ReadTraffic<CustomerVisitState>(pendingVisit,"State")!=CustomerVisitState.Queued ||
                    !ReadTraffic<bool>(pendingVisit,"CounterArrived")) return;
                var anchor=ReadTraffic<object>(pendingVisit,"PendingReservationAnchor");
                if(anchor==null || !CafeLoopRuntimeProof.Field<bool>(anchor,"Submitted")) return;
                var core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic");
                if(core==null || !ReferenceEquals(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(anchor,"Coordinator"),core) ||
                    !ReferenceEquals(CafeLoopRuntimeProof.Field<object>(anchor,"Visit"),pendingVisit)) return;
                var key=CafeLoopRuntimeProof.Field<TrafficIntentKey>(anchor,"Key");
                if(key.ActorId!=head || key.IntentId!="pickup-wait") return;
                var decision=core.GetDecision(key); // Lookup only: does not Submit, Evaluate or pump.
                var spatialWait=decision.Kind==TrafficDecisionKind.WaitingForTraffic &&
                    (decision.Reason=="finite-owner-release" || decision.Reason=="bounded-phase-handoff") ||
                    decision.Kind==TrafficDecisionKind.DeferredByBudget &&
                    (decision.Reason=="unpaid-eligibility/endpoint-occupied" || decision.Reason=="unpaid-eligibility/route-capability-unavailable");
                if(!spatialWait || decision.FailureDisposition!=TrafficFailureDisposition.None || decision.Blockers.Count==0 ||
                    CafeLoopRuntimeProof.Field<string>(owner,"pickupWaitReason")!=decision.Kind+": "+decision.Reason) return;
                var preview=CafeLoopRuntimeProof.Field<PickupQueueService.PreparedMutation>(anchor,"Preview");
                if(preview==null || !preview.IsCurrent) return;
                var ownerKeys=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"trafficActorKeys");
                // The original actual remaining-route geometry must still prove the same current blocker.
                // 原有真实余程几何仍须成立，且必须对应当前decision里的同一owner与路线proof。
                var pickup=(PickupQueueService)typeof(CustomerFlowController).GetField("pickupQueue",flags).GetValue(customers);
                var entries=(System.Collections.IDictionary)typeof(PickupQueueService).GetField("reservations",flags).GetValue(pickup);
                var slots=(Vector3[])typeof(PickupQueueService).GetField("waitSlots",flags).GetValue(pickup);
                Assert.That(owner.HoldCollection,Is.True);
                Assert.That(pickup.CanReserve,Is.True,"Owned slots remain contiguous with no advance; the rolled-back tail index is reservation Count");
                var candidate=slots[entries.Count]; // TryReserve chooses reservations.Count before adding.
                Assert.That(preview.Assignment.Position,Is.EqualTo(candidate),"Observe the exact current unpaid candidate, not another slot");
                Assert.That(customers.CounterAssignments[0].Arrived,Is.True);
                var headActor=Find<NavigationWorld>().RegisteredActors.Single(a=>a.ActorId==head);
                var clearance=(float)typeof(CustomerFlowController).GetMethod("RouteClearance",flags).Invoke(customers,new object[]{headActor.Settings})+
                    2*headActor.Settings.MaxSpeed*Mathf.Min(Time.deltaTime,headActor.Settings.MaxSubstepSeconds*headActor.Settings.MaxSubstepsPerFrame);
                var conflict=false; var decisionBoundConflict=false;
                foreach(System.Collections.DictionaryEntry entry in visits)
                {
                    var item=entry.Value; var type=item.GetType(); var state=(CustomerVisitState)type.GetField("State").GetValue(item);
                    var request=(long)type.GetField("RequestId").GetValue(item);
                    if(request==0 || state!=CustomerVisitState.MovingToPickup && state!=CustomerVisitState.PickupAdvancing && state!=CustomerVisitState.MovingToPickupService) continue;
                    var actor=(NavigationActor)type.GetField("Actor").GetValue(item); if(actor==null || !actor.isActiveAndEnabled) continue;
                    var route=(System.Collections.Generic.Queue<Vector3>)type.GetField("Route").GetValue(item);
                    var path=(System.Collections.Generic.IReadOnlyList<Vector3>)typeof(CustomerFlowController).GetMethod("ExpandRoute",flags).Invoke(customers,
                        new object[]{actor.transform.position,new[]{actor.Agent.destination}.Concat(route??Enumerable.Empty<Vector3>())});
                    if(path!=null && !CustomerRoutePlanner.IsClear(path,new[]{candidate},clearance))
                    {
                        conflict=true;
                        cashierDiagnosticSample?.Invoke("remaining-route-conflict","earlier="+actor.ActorId+" request="+request+
                            " candidate="+candidate.ToString("F6")+" clearance="+clearance.ToString("R")+
                            " originalExpandedPath=["+string.Join(";",path.Select(p=>p.ToString("F6")))+"]");
                        var sameOwner=ownerKeys.Contains(actor.ActorId) && decision.Blockers.Any(blocker=>
                            blocker.Owner.Equals((TrafficIntentKey)ownerKeys[actor.ActorId]) &&
                            (blocker.Proof=="selected-journey-parking-protection" || blocker.Proof=="current-conflict-segment"));
                        if(sameOwner)
                        {
                            decisionBoundConflict=true;
                            TestContext.WriteLine("PREPAY_ROUTE_CONFLICT head="+head+" earlier="+actor.ActorId+" state="+state+" request="+request+
                                " candidate="+candidate.ToString("F4")+" clearance="+clearance.ToString("R")+" decision="+decision.Kind+": "+decision.Reason);
                        }
                    }
                }
                // Other spatial waits remain unclassified; the final evidence assertions still require this seam.
                cashierDiagnosticSample?.Invoke("remaining-route-observed","conflict="+conflict+" decisionBoundConflict="+decisionBoundConflict);
                if(!conflict || !decisionBoundConflict) return;
                Assert.That(conflict,Is.True,"The explained wait must name an actual active remaining-route conflict");
                Assert.That(value.Status,Is.Not.EqualTo("Blocked"));
                Assert.That(value.PaidVisitIds,Does.Not.Contain(head));
                Assert.That(value.Orders.Values.Any(order=>order.CustomerId==head),Is.False);
                Assert.That(entries.Contains(head),Is.False,"Rejected prepayment reserve must roll back, without charging");
                waitingHead=waitingHead??head; provedRouteConflict=explainedWait=true;
            };
            employees.IntegratedTaskEvent+=value=>
            {
                if(cashierDiagnosticSample!=null && value.Role==EmployeeRole.Cashier && value.Checkpoint.Stage==CafeWorkStage.Working)
                    cashierDiagnosticSample("cashier-working",value.Checkpoint.VisitId);
                if(value.Role!=EmployeeRole.Cashier || value.Checkpoint.Stage!=CafeWorkStage.Working || !starts.Add(value.HandleId)) return;
                if(value.Checkpoint.VisitId==waitingHead) resumedWaitingHead=true;
                var pickup=typeof(CustomerFlowController).GetField("pickupQueue",flags).GetValue(customers);
                var entries=(System.Collections.IDictionary)typeof(PickupQueueService).GetField("reservations",flags).GetValue(pickup);
                var target=(Vector3)entries[value.Checkpoint.VisitId].GetType().GetField("Target").GetValue(entries[value.Checkpoint.VisitId]);
                var visits=(System.Collections.IDictionary)typeof(CustomerFlowController).GetField("visits",flags).GetValue(customers);
                foreach(System.Collections.DictionaryEntry entry in visits)
                {
                    var item=entry.Value; var type=item.GetType(); var state=(CustomerVisitState)type.GetField("State").GetValue(item);
                    if(state!=CustomerVisitState.MovingToPickup && state!=CustomerVisitState.PickupAdvancing && state!=CustomerVisitState.MovingToPickupService) continue;
                    var actor=(NavigationActor)type.GetField("Actor").GetValue(item);
                    var route=(System.Collections.Generic.Queue<Vector3>)type.GetField("Route").GetValue(item);
                    var path=(System.Collections.Generic.IReadOnlyList<Vector3>)typeof(CustomerFlowController).GetMethod("ExpandRoute",flags).Invoke(customers,
                        new object[]{actor.transform.position,new[]{actor.Agent.destination}.Concat(route??Enumerable.Empty<Vector3>())});
                    if(!CustomerRoutePlanner.IsClear(path,new[]{target},.911f)) { unsafeStart=true; TestContext.WriteLine("UNSAFE_CASHIER_START reserved="+target+" earlier="+actor.ActorId); }
                }
            };
            try
            {
                tools.HoldCollection(); tools.ResumeArrivals();
                yield return Until(()=>unsafeStart || starts.Count>=4 && explainedWait && provedRouteConflict && resumedWaitingHead,12000); tools.HoldArrivals();
                Assert.That(unsafeStart,Is.False,"A still-owned earlier pickup path cannot cross the newly sold slot");
                Assert.That(explainedWait,Is.True,"Actual prepayment route wait must explain its reason without becoming Blocked");
                Assert.That(provedRouteConflict,Is.True);
                Assert.That(resumedWaitingHead,Is.True,"The same waiting head resumes through the formal service after the earlier customer actually passes");
            }
            finally { StopCashierOriginalDecisionTrace(); }
        }
        private Action BeginCashierPublicationTrace()
        {
            // Default-off identity witness; no producer guard, planner, Refresh or mutation is replayed.
            // 默认关闭；只在原回调读取身份，不重跑业务guard或路径查询。
            if(!Environment.GetCommandLineArgs().Contains("-cashierPublicationTrace"))return ()=>{};
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            object Read(object value,string name)=>value==null?null:value.GetType().GetField(name,flags)?.GetValue(value);
            string Ref(object value)=>value==null?"NULL":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value).ToString();
            string Key(TrafficIntentKey value)=>value.Session+"/e"+value.Epoch+"/"+value.ActorId+"/"+value.IntentId+
                "/g"+value.Generation+"/r"+value.LayoutRevision+"/"+value.Context;
            var flow=customers;var staff=employees;var loop=owner;
            var core=(ServiceTrafficCoordinator)Read(loop,"traffic");
            var hook=typeof(ServiceTrafficCoordinator).GetField("WaitFailureObserver",flags);
            var previous=(Action<string>)hook.GetValue(core); // Installed AFTER TrafficWaitFailureTrace.
            var rows=(IDictionary)Read(core,"entries");
            var visits=(IDictionary)Read(flow,"visits");
            var records=new System.Collections.Generic.List<string>();var errors=new System.Collections.Generic.List<string>();
            var active=true;var captured=false;var published=false;var ordinal=0;
            object savedEntry=null,savedVisit=null,savedAnchor=null,savedCursor=null,savedEligibility=null;
            TrafficDecision savedDecision=null;TrafficIntentKey savedKey=default;long beforeAcquired=-1;
            void Error(Exception error){if(errors.Count<4)errors.Add(error.GetType().Name+":"+error.Message);}
            string OwnerMap(string id)
            {
                var map=(IDictionary)Read(flow,"trafficActorKeys");
                return map!=null && map.Contains(id) && map[id] is TrafficIntentKey value?Key(value):"MISSING";
            }
            Action<string> installed=null;
            Action<CafeTaskEvent> working=value=>
            {
                if(!active || !captured || published || value.Role!=EmployeeRole.Cashier ||
                    value.Checkpoint.Stage!=CafeWorkStage.Working || value.Checkpoint.VisitId!=savedKey.ActorId)return;
                published=true;
                try
                {
                    var visit=visits.Contains(savedKey.ActorId)?visits[savedKey.ActorId]:null;
                    var entry=rows.Contains(savedKey)?rows[savedKey]:null;
                    var parking=Read(visit,"PickupParking");var source=Read(parking,"Source") as TrafficDecision;
                    var claim=Read(parking,"Claim") as PickupQueueService.ReservationClaim;
                    var token=Read(parking,"Token") as CapacityToken;
                    var tokenRows=flow.Capacity.GetReservations().Where(row=>ReferenceEquals(row.Token,token)).ToArray();
                    var currentDecision=Read(entry,"Decision") as TrafficDecision;
                    var sameSource=source!=null && ReferenceEquals(source,savedDecision);
                    records.Add("PUBLICATION_TAP ordinal="+(++ordinal)+" stage=cashier-working frame="+Time.frameCount+
                        " key="+Key(savedKey)+" handle="+value.HandleId+" core="+Ref(core)+
                        " sameLoopCore="+ReferenceEquals(Read(loop,"traffic"),core)+" sameFlowCore="+ReferenceEquals(Read(flow,"traffic"),core)+
                        " sameVisit="+ReferenceEquals(visit,savedVisit)+" sameEntry="+ReferenceEquals(entry,savedEntry)+
                        " samePreparedDecision="+sameSource+" sameEntryDecision="+ReferenceEquals(currentDecision,savedDecision)+
                        " beforeAcquired="+beforeAcquired+" sourceAcquired="+source?.AcquiredSequence+
                        " entryAcquired="+Read(entry,"AcquiredSequence")+" sourceKeyMatches="+(source!=null && source.Intent.Equals(savedKey))+
                        " originalDecisionNowAcquired="+savedDecision?.AcquiredSequence+" savedAnchor="+Ref(savedAnchor)+
                        " savedCursor="+Ref(savedCursor)+" savedEligibility="+Ref(savedEligibility)+
                        " pendingAnchor="+Ref(Read(visit,"PendingReservationAnchor"))+" pickupAssigned="+Read(visit,"PickupAssigned")+
                        " phaseSeq="+Read(visit,"TrafficPhaseSequence")+" actorKey="+OwnerMap(savedKey.ActorId)+
                        " parking="+Ref(parking)+" parkingCoreMatches="+ReferenceEquals(Read(parking,"Coordinator"),core)+
                        " visitPermitMatches="+ReferenceEquals(Read(visit,"PickupTrafficPermit"),savedDecision)+
                        " claim="+Ref(claim)+" claimCurrent="+(claim==null?"NULL":claim.IsCurrent.ToString())+
                        " claimOwnerMatches="+(claim!=null && ReferenceEquals(claim.CanonicalOwner,Read(parking,"Owner")))+
                        " claimResource="+claim?.ResourceId+" token="+Ref(token)+" tokenRows="+tokenRows.Length+
                        " tokenFacts=["+string.Join(";",tokenRows.Select(row=>row.OwnerId+"/"+row.Kind+"/"+row.State))+"]");
                }
                catch(Exception error){Error(error);}
            };
            installed=text=>
            {
                try
                {
                    if(!active || captured || text==null || !text.StartsWith("WAIT_ORIGINAL condition=owner-not-independent ",StringComparison.Ordinal) ||
                        !text.Contains("/employee-pickup-journey/") || !text.Contains(" proof=selected-journey-parking-protection "))return;
                    var begin=text.IndexOf(" owner=",StringComparison.Ordinal);var end=text.IndexOf(" proof=",StringComparison.Ordinal);
                    if(begin<0 || end<=begin)return;
                    var fullKey=text.Substring(begin+7,end-(begin+7));
                    var matches=rows.Keys.Cast<TrafficIntentKey>().Where(key=>Key(key)==fullKey).ToArray();
                    if(matches.Length!=1){captured=true;records.Add("PUBLICATION_TAP ordinal="+(++ordinal)+" stage=wait-unmatched frame="+Time.frameCount+" key="+fullKey+" matches="+matches.Length);return;}
                    savedKey=matches[0];savedEntry=rows[savedKey];
                    savedVisit=visits.Contains(savedKey.ActorId)?visits[savedKey.ActorId]:null;
                    savedAnchor=Read(savedVisit,"PendingReservationAnchor");
                    var candidates=(IDictionary)Read(flow,"trafficCandidates");
                    savedCursor=candidates.Contains(savedKey)?candidates[savedKey]:null;
                    object Fact(object value,string name)=>value==null?null:value.GetType().GetField(name,flags)?.GetValue(value) ?? value.GetType().GetProperty(name,flags)?.GetValue(value);
                    foreach(var deliveryKey in candidates.Keys.Cast<TrafficIntentKey>().Where(k=>k.IntentId=="employee-pickup-journey" && text.Contains("intent="+Key(k)+" previous=")))
                    {
                        var cursor=candidates[deliveryKey];var sides=(IEnumerable)Fact(cursor,"Sides");
                        string Values(string name)=>Fact(cursor,name) is IEnumerable values?string.Join(",",values.Cast<object>()):"NULL";
                        records.Add("PUBLICATION_DECLARED_CANDIDATES frame="+Time.frameCount+" key="+Key(deliveryKey)+
                            " index="+Fact(cursor,"CurrentIndex")+" count="+Fact(cursor,"Count")+" origin="+Fact(cursor,"Origin")+
                            " sides=["+(sides==null?"NULL":string.Join(";",sides.Cast<object>().Select(side=>Fact(side,"Position"))))+"]"+
                            " attempted=["+Values("AttemptedIndices")+"] staticInvalid=["+Values("StaticInvalidIndices")+"]"+
                            " context="+Ref(Fact(cursor,"DeliveryContext"))+" preview="+Ref(Fact(cursor,"DeliveryPreview"))+
                            " originalPlan="+Ref(Fact(cursor,"OriginalDeliveryPlan"))+
                            " deliveryReadiness="+Ref(Fact(cursor,"DeliveryReadiness"))+" deliveryWorking="+Ref(Fact(cursor,"DeliveryWorking"))+
                            " coreReadiness="+Ref(Fact(rows[deliveryKey],"DeliveryPreparationReadiness"))+
                            " preparationStarted="+Fact(rows[deliveryKey],"DeliveryPreparationStarted")+
                            " publicationSealed="+Fact(rows[deliveryKey],"DeliveryPreparationPublicationSealed")+" addedQueries=0");
                    }
                    savedEligibility=Read(savedEntry,"UnpaidEligibility");savedDecision=Read(savedEntry,"Decision") as TrafficDecision;
                    beforeAcquired=savedDecision?.AcquiredSequence??-1;captured=true;
                    var head=flow.CounterAssignments.Count==0?null:flow.CounterAssignments[0].VisitId;
                    var anchorKey=Read(savedAnchor,"Key");var request=Read(savedEntry,"Request") as TrafficRequest;
                    records.Add("PUBLICATION_TAP ordinal="+(++ordinal)+" stage=wait frame="+Time.frameCount+
                        " key="+fullKey+" core="+Ref(core)+" sameLoopCore="+ReferenceEquals(Read(loop,"traffic"),core)+
                        " sameFlowCore="+ReferenceEquals(Read(flow,"traffic"),core)+" headMatches="+(head==savedKey.ActorId)+
                        " entry="+Ref(savedEntry)+" requestKeyMatches="+(request!=null && request.Intent.Equals(savedKey))+
                        " anchor="+Ref(savedAnchor)+" anchorKeyMatches="+(anchorKey is TrafficIntentKey keyValue && keyValue.Equals(savedKey))+
                        " anchorCoreMatches="+ReferenceEquals(Read(savedAnchor,"Coordinator"),core)+
                        " anchorVisitMatches="+ReferenceEquals(Read(savedAnchor,"Visit"),savedVisit)+" anchorSubmitted="+Read(savedAnchor,"Submitted")+
                        " cursor="+Ref(savedCursor)+" cursorSourceMatches="+ReferenceEquals(Read(savedCursor,"Source"),savedDecision)+
                        " cursorSource="+Ref(Read(savedCursor,"Source"))+" preparedDecision="+Ref(savedDecision)+
                        " kind="+savedDecision?.Kind+" reason="+savedDecision?.Reason+" acquired="+beforeAcquired+
                        " entryAcquired="+Read(savedEntry,"AcquiredSequence")+" prepared="+Read(savedEntry,"Prepared")+
                        " active="+Read(savedEntry,"Active")+" queued="+Read(savedEntry,"Queued")+
                        " canonicalAttempt="+Read(savedEntry,"EverCanonicalAttempt")+" leg="+Ref(Read(savedEntry,"Leg"))+
                        " native="+request?.NativeStamp.RequestId+"/"+request?.NativeStamp.LegGeneration+
                        " eligibility="+Ref(savedEligibility)+" anchorEligibilityMatches="+ReferenceEquals(Read(savedAnchor,"Eligibility"),savedEligibility)+
                        " visit="+Ref(savedVisit)+" pickupAssigned="+Read(savedVisit,"PickupAssigned")+
                        " phaseSeq="+Read(savedVisit,"TrafficPhaseSequence")+" actorKey="+OwnerMap(savedKey.ActorId)+
                        " parking="+Ref(Read(savedVisit,"PickupParking"))+" parkingSource="+Ref(Read(Read(savedVisit,"PickupParking"),"Source")));
                }
                catch(Exception error){Error(error);}
                finally{previous?.Invoke(text);} // Preserve every original WAIT trace record, including nonmatching events.
            };
            // Restore the wrapper before disposing TrafficWaitFailureTrace; fallback teardown calls the same idempotent closure.
            try{staff.IntegratedTaskEvent+=working;hook.SetValue(core,installed);}
            catch{staff.IntegratedTaskEvent-=working;throw;}
            return ()=>
            {
                if(!active)return;active=false;
                try{staff.IntegratedTaskEvent-=working;}
                finally
                {
                    if(ReferenceEquals(hook.GetValue(core),installed))hook.SetValue(core,previous);
                    else if(errors.Count<4)errors.Add("WaitFailureObserver changed externally before cleanup");
                    foreach(var record in records)TestContext.WriteLine(record);
                    TestContext.WriteLine("PUBLICATION_TAP_END payloads="+records.Count+" captured="+captured+" working="+published+
                        " errors=["+string.Join(";",errors)+"]");
                }
            };
        }

        private Action BeginCashierOriginalDecisionTrace()
        {
            var enabled=Environment.GetCommandLineArgs().Contains("-cashierOriginalDecisionTrace");
            if(!enabled)return ()=>{};
            const System.Reflection.BindingFlags rf=System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            object R(object obj,string name)=>obj==null?null:obj.GetType().GetField(name,rf)?.GetValue(obj);
            string Key(object obj)=>obj is TrafficIntentKey k?
                k.Session+"/e"+k.Epoch+"/"+k.ActorId+"/"+k.IntentId+"/g"+k.Generation+"/r"+k.LayoutRevision+"/"+k.Context:"NULL";
            string Ref(object obj)=>obj==null?"NULL":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj).ToString();
            string Points(object obj)=>obj is System.Collections.Generic.IEnumerable<Vector3> points?
                "["+string.Join(";",points.Select(p=>p.ToString("F6")))+"]":"NULL";
            string Fields(object obj,params string[] names)=>obj==null?"NULL":string.Join(",",names.Select(n=>n+"="+R(obj,n)));
            var records=new System.Collections.Generic.List<string>();
            var errors=new System.Collections.Generic.List<string>();
            var seen=new System.Collections.Generic.HashSet<string>();
            var active=true;var samples=0;var calls=0;var dropped=0;
            var cachedWorld=Find<NavigationWorld>(); // cache while scene is alive, never Find during teardown
            void Sample(string stage,string originalEvent=null)
            {
                if(!active)return;
                calls++;
                if(samples>=96){dropped++;return;}
                try
                {
                    var snapshot=owner.Snapshot;
                    var assignments=customers.CounterAssignments;
                    var head=assignments.Count==0?null:assignments[0].VisitId;
                    var visits=R(customers,"visits") as System.Collections.IDictionary;
                    var visit=head!=null && visits!=null && visits.Contains(head)?visits[head]:null;
                    var anchor=R(visit,"PendingReservationAnchor");
                    var core=R(customers,"traffic") as ServiceTrafficCoordinator;
                    var entries=R(core,"entries") as System.Collections.IDictionary;
                    var keyObject=R(anchor,"Key");
                    var entry=keyObject!=null && entries!=null && entries.Contains(keyObject)?entries[keyObject]:null;
                    var decision=R(entry,"Decision") as TrafficDecision;
                    var headRequest=R(entry,"Request") as TrafficRequest;
                    var blockers=decision==null?"NULL":string.Join(";",decision.Blockers.Select(b=>Key(b.Owner)+"/"+b.ResourceId+"/"+b.Proof));
                    var why=R(owner,"pickupWaitReason") as string;
                    var fingerprint=stage+"/"+head+"/"+R(visit,"State")+"/"+R(visit,"CounterArrived")+"/"+
                        Ref(anchor)+"/"+R(anchor,"Submitted")+"/"+decision?.Kind+"/"+decision?.Reason+"/"+blockers+"/"+why;
                    // First transition, plus at most one original-snapshot sample per 30 frames for a retained wait.
                    // bounded copy only; unchanged business/frame budgets, no per-frame planner queries.
                    if(!seen.Add(fingerprint) && (stage!="snapshot" || decision==null || decision.Blockers.Count==0 || Time.frameCount%30!=0))return;
                    samples++;
                    var preview=R(anchor,"Preview") as PickupQueueService.PreparedMutation;
                    var pickup=R(customers,"pickupQueue");
                    var reservations=R(pickup,"reservations") as System.Collections.IDictionary;
                    var slots=R(pickup,"waitSlots") as Vector3[];
                    var nextIndex=reservations==null?-1:reservations.Count;
                    var candidate=slots!=null && nextIndex>=0 && nextIndex<slots.Length?slots[nextIndex].ToString("F6"):"UNAVAILABLE";
                    var ownerKeys=R(customers,"trafficActorKeys") as System.Collections.IDictionary;
                    records.Add("CASHIER_ORIGINAL stage="+stage+" frame="+Time.frameCount+" time="+Time.timeSinceLevelLoadAsDouble.ToString("R")+
                        " head="+head+" arrived="+(head!=null && assignments[0].Arrived)+" paid="+(head!=null && snapshot.PaidVisitIds.Contains(head))+
                        " ordered="+(head!=null && snapshot.Orders.Values.Any(o=>o.CustomerId==head))+" waitVisit="+R(owner,"pickupWaitVisit")+" why="+why+
                        " originalEvent="+originalEvent+" core="+Ref(core)+" anchor="+Ref(anchor)+" anchorCore="+Ref(R(anchor,"Coordinator"))+
                        " sameVisit="+ReferenceEquals(R(anchor,"Visit"),visit)+" submitted="+R(anchor,"Submitted")+" key="+Key(keyObject)+
                        " visit="+Fields(visit,"State","CounterArrived","PickupAssigned","RequestId","Generation","TrafficPhaseSequence")+
                        " decision="+decision?.Kind+"/"+decision?.Reason+"/failure="+decision?.FailureDisposition+
                        " requestTarget="+headRequest?.Target.ToString("F6")+" radius="+headRequest?.Radius+" safety="+headRequest?.SafetyMargin+
                        " seq="+decision?.RequestSequence+" acquired="+decision?.AcquiredSequence+" blockers=["+blockers+"]"+
                        " preview="+Ref(preview)+" current="+(preview!=null && preview.IsCurrent)+" assignment="+(preview==null?"NULL":preview.Assignment.Position.ToString("F6"))+
                        " candidate="+candidate+" slot="+nextIndex+" count="+slots?.Length+" holdCollection="+owner.HoldCollection);
                    if(decision!=null)foreach(var blocker in decision.Blockers)
                    {
                        var source=entries!=null && entries.Contains(blocker.Owner)?entries[blocker.Owner]:null;
                        var request=R(source,"Request") as TrafficRequest;
                        var sourceVisit=visits!=null && visits.Contains(blocker.Owner.ActorId)?visits[blocker.Owner.ActorId]:null;
                        var actor=R(sourceVisit,"Actor") as NavigationActor;
                        var stampObject=R(R(source,"Leg"),"Stamp");var stamp=stampObject is TrafficNativeStamp native?native:default;
                        var currentKey=ownerKeys!=null && ownerKeys.Contains(blocker.Owner.ActorId)?ownerKeys[blocker.Owner.ActorId]:null;
                        records.Add("CASHIER_BLOCKER key="+Key(blocker.Owner)+" currentActorKey="+Key(currentKey)+" sameKey="+Equals(currentKey,blocker.Owner)+
                            " proof="+blocker.Proof+" resource="+blocker.ResourceId+" source="+Ref(source)+
                            " entry="+Fields(source,"Retired","Parking","Active","Prepared","RequestSequence","AcquiredSequence","WaitRemaining","FirstFrame","Queries")+
                            " requestTarget="+request?.Target.ToString("F6")+" radius="+request?.Radius+" safety="+request?.SafetyMargin+
                            " journey="+Points(R(source,"Journey"))+" currentPath="+Points(R(source,"CurrentPath"))+
                            " leg="+Fields(R(source,"Leg"),"Index","Generation","Bound","Committed","Completed")+
                            " nativeStamp="+stamp.RequestId+"/"+stamp.LegGeneration+" actorLife="+Ref(R(R(source,"Leg"),"ActorLife"))+
                            " visit="+Fields(sourceVisit,"State","RequestId","Generation","CurrentSegmentTarget","PendingPickupSegment")+
                            " body="+(actor==null?"NONE":actor.transform.position.ToString("F6"))+
                            " destination="+(actor==null?"NONE":actor.Agent.destination.ToString("F6"))+
                            " selectedWaypoints="+Points(R(sourceVisit,"Route"))+
                            " registered="+(actor!=null && cachedWorld.RegisteredActors.Contains(actor)));
                    }
                }
                catch(Exception e){if(errors.Count<8)errors.Add(stage+":"+e.GetType().Name+":"+e.Message);}
            }
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",rf);
            var prior=(Action<string,string,string,Vector3,System.Collections.Generic.IReadOnlyList<Vector3>,Vector3[],string>)hook.GetValue(customers);
            Action<string,string,string,Vector3,System.Collections.Generic.IReadOnlyList<Vector3>,Vector3[],string> observer=(op,id,related,target,path,bodies,result)=>
            {
                prior?.Invoke(op,id,related,target,path,bodies,result);
                if(op=="reserve.original-refusal" || op=="admission.front-refusal-observed")
                    Sample(op,"actor="+id+" target="+target.ToString("F6")+" originalPath="+Points(path)+" result="+result);
            };
            hook.SetValue(customers,observer); // only the existing editor observation delegate
            cashierDiagnosticSample=Sample;
            return ()=>
            {
                if(!active)return;active=false;cashierDiagnosticSample=null;
                if(customers!=null && ReferenceEquals(hook.GetValue(customers),observer))hook.SetValue(customers,prior);
                else errors.Add("CorridorObserver cleanup ownership UNKNOWN; external replacement preserved");
                foreach(var line in records)TestContext.WriteLine(line);
                TestContext.WriteLine("CASHIER_ORIGINAL_END samples="+samples+" calls="+calls+" dropped="+dropped+" errors=["+string.Join(";",errors)+"]");
            };
        }
        // Two bounded stored-state receipts only; no Reserve/IsCurrent/planner or additional business step.
        // 仅记录真实五客窗口与原失败分支；未知字段明确保留 UNKNOWN。
        private void InstallFiveCachedQueueSnapshot()
        {
            if(!Environment.GetCommandLineArgs().Contains("-p15FiveCachedQueueSnapshot") ||
                TestContext.CurrentContext.Test.Name!=nameof(HeldCollectionAllowsFiveRealDeliveriesAndFifoDrain))return;
            var initial=false;var failed=false;var ordinal=0;
            fiveCachedQueueSnapshot=stage=>
            {
                if(stage=="five-visits-held"){if(initial)return;initial=true;}else{if(failed)return;failed=true;}
                var text=new System.Text.StringBuilder("P15_FIVE_CACHED_QUEUE ordinal="+(++ordinal)+" stage="+stage+" frame="+Time.frameCount);
                try
                {
                    if(customers==null || owner==null){TestContext.WriteLine(text+" UNKNOWN destroyed scene dependency");return;}
                    const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
                    object Read(object v,string n)=>v==null?null:(v.GetType().GetField(n,flags)??v.GetType().GetField("<"+n+">k__BackingField",flags))?.GetValue(v);
                    string Ref(object v)=>v==null?"NULL":v.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(v);
                    string Points(object v)=>v is IEnumerable points?string.Join(";",points.Cast<Vector3>().Select(point=>point.ToString("F9"))):"UNKNOWN";
                    string Fields(object v,params string[] names)=>v==null?"NULL":Ref(v)+"{"+string.Join(";",names.Select(n=>n+"="+(Read(v,n)??"NULL")))+"}";
                    string Key(TrafficIntentKey k)=>k.Session+"/e"+k.Epoch+"/"+k.ActorId+"/"+k.IntentId+"/g"+k.Generation+"/r"+k.LayoutRevision+"/"+k.Context;
                    var pickup=Read(customers,"pickupQueue");var slots=Read(pickup,"waitSlots") as Vector3[];
                    var reservations=Read(pickup,"reservations") as IDictionary;var paid=Read(pickup,"paidQueue") as IList;
                    var counter=Read(customers,"queue") as CounterQueueService;var rows=counter?.Snapshot;
                    var visits=Read(customers,"visits") as IDictionary;var core=Read(customers,"traffic");var entries=Read(core,"entries") as IDictionary;
                    text.Append(" visits=").Append(customers.Snapshot.Count).Append(" actualDelivery=").Append(owner.Snapshot.Delivery.Count)
                        .Append(" completed=").Append(owner.Snapshot.Orders.Values.Count(order=>order.State==OrderState.Completed))
                        .Append(" queue=").Append(Ref(pickup)).Append(" version=").Append(Read(pickup,"Version"))
                        .Append(" slotCount=").Append(slots==null?"UNKNOWN":slots.Length.ToString()).Append(" reservationCount=").Append(reservations==null?"UNKNOWN":reservations.Count.ToString())
                        .Append(" paidCount=").Append(paid==null?"UNKNOWN":paid.Count.ToString()).Append(" countFullDerived=")
                        .Append(slots==null||reservations==null?"UNKNOWN":(reservations.Count>=slots.Length).ToString())
                        .Append(" slots=[").Append(Points(slots)).Append("] storedControllerSlots=[").Append(Points(Read(customers,"pickupWaitSlots"))).Append("]")
                        .Append(" selectedSide=").Append(Read(customers,"pickupCustomer")).Append(" outward=").Append(Read(customers,"pickupOutward"))
                        .Append(" pickupId=").Append(Read(customers,"selectedPickupId")).Append(" core=").Append(Ref(core));
                    if(reservations!=null)foreach(var value in reservations.Values.Cast<object>().Take(32))text.Append("\nreservation=").Append(Fields(value,"Id","Slot","Generation","Target","Paid","Arrived","Advancing","Yielding"));
                    if(paid!=null)foreach(var value in paid.Cast<object>().Take(32))text.Append("\npaidQueue=").Append(Fields(value,"Id","Slot","Generation","Target","Paid","Arrived","Advancing","Yielding"));
                    if(reservations!=null && reservations.Count>32 || paid!=null && paid.Count>32)text.Append("\nrowsTruncated=True limit=32");
                    if(rows!=null && rows.Count>0)
                    {
                        var head=rows[0];var visit=visits!=null && visits.Contains(head.VisitId)?visits[head.VisitId]:null;var anchor=Read(visit,"PendingReservationAnchor");
                        text.Append("\nhead=").Append(head.VisitId).Append("/slot=").Append(head.SlotIndex).Append("/gen=").Append(head.Generation).Append("/arrived=").Append(head.Arrived)
                            .Append("/position=").Append(head.Position.ToString("F9")).Append(" visit=").Append(Fields(visit,"State","CounterArrived","PickupAssigned","HasCollected"))
                            .Append(" pendingAnchor=").Append(Fields(anchor,"Submitted","Retired","FirstFrame","SideIndex")).Append(" anchorOwnerSameCached=").Append(anchor==null?"UNKNOWN":ReferenceEquals(Read(anchor,"Owner"),pickup).ToString());
                        var key=Read(anchor,"Key");if(key is TrafficIntentKey typed)text.Append(" anchorKey=").Append(Key(typed));
                        var entry=key!=null && entries!=null && entries.Contains(key)?entries[key]:null;
                        text.Append(" entry=").Append(Fields(entry,"RequestSequence","AcquiredSequence","Queries","JobIndex","Queued","Prepared","Retired"));
                        if(Read(entry,"Decision") is TrafficDecision decision)text.Append(" decision=").Append(decision.Kind).Append("/").Append(decision.Reason);
                        if(entries!=null)
                        {
                            var keys=entries.Keys.Cast<TrafficIntentKey>().Where(k=>k.ActorId==head.VisitId && k.IntentId=="pickup-wait").ToArray();
                            text.Append(" headStoredPickupIntentCount=").Append(keys.Length);
                            foreach(var stored in keys.Take(8))text.Append("\nheadStoredIntent=").Append(Key(stored)).Append(" ").Append(Fields(entries[stored],"Retired","Queries","RequestSequence","AcquiredSequence"));
                            if(keys.Length>8)text.Append("\nheadStoredIntentsTruncated=True limit=8");
                        }
                    }
                    else text.Append("\nhead=UNKNOWN_OR_EMPTY");
                    var rejected=Read(customers,"pickupPlanningRejections") as IList;text.Append("\nrejectedForwardCount=").Append(rejected==null?"UNKNOWN":rejected.Count.ToString());
                    if(rejected!=null)foreach(var value in rejected.Cast<object>().Take(32))text.Append("\nrejectedForward=").Append(Fields(value,"Point","Previous","Direction","Reason")).Append(" prefix=[").Append(Points(Read(value,"Prefix"))).Append("]");
                    if(rejected!=null && rejected.Count>32)text.Append("\nrejectionsTruncated=True limit=32");
                    text.Append("\noriginalReserveResult=NOT_REPLAYED plannerQueriesAdded=0 businessStepsAdded=0");
                }
                catch(Exception error){text.Append("\nUNKNOWN_CAPTURE_ERROR ").Append(error.GetType().FullName).Append(":").Append(error.Message);}
                TestContext.WriteLine(text.ToString());
            };
        }
        private T Find<T>() where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
        private IEnumerator Until(Func<bool> done,int max=2400)
        { for(var f=0;f<max && !done();f++) { if(owner!=null && (owner.Snapshot.Status=="Blocked" && !string.IsNullOrEmpty(owner.Snapshot.Failure) || customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked))) break; yield return null; } if(!done() && tools!=null) { fiveCachedQueueSnapshot?.Invoke("original-until-failed"); if(Environment.GetCommandLineArgs().Contains("-p15OriginalUnpaidFailureSnapshot")) { if(originalUnpaidFailureFinalSnapshot!=null)originalUnpaidFailureFinalSnapshot();else DumpOriginalUnpaidFailureSnapshot(); } tools.PrintSnapshot(); DumpRoutes(); }
          Assert.That(done(),Is.True,(owner==null?"No owner":owner.Snapshot.Status+" / "+owner.Snapshot.Failure)+" / "+(customers==null?"":customers.Status)); }
        // Capture the original stored Job before the existing Core observer's caller clears it.
        // Default-off; forward the existing observer chain and never replay a producer/guard/query.
        private void InstallOriginalUnpaidFailureObserver()
        {
            if(!Environment.GetCommandLineArgs().Contains("-p15OriginalUnpaidFailureSnapshot"))return;
            StopOriginalUnpaidFailureObserver();
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            object Read(object value,string name)=>value==null?null:(value.GetType().GetField(name,flags)??value.GetType().GetField("<"+name+">k__BackingField",flags))?.GetValue(value);
            string Ref(object value)=>value==null?"NULL":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            var core=Read(customers,"traffic");System.Reflection.FieldInfo field=null;Delegate prior=null,installed=null;var active=true;
            var last=new System.Collections.Generic.Dictionary<TrafficIntentKey,(object Entry,TrafficRequest Request,int Frame,int Queries,string Payload)>();
            var skippedCompletedGeometry=0;var droppedAtCapacity=0;string issue=null;
            void Unknown(string detail){if(issue==null)issue=detail;}
            string Summary()=>" failureFrame="+Time.frameCount+" cacheCount="+last.Count+" cacheCap=16 captureMode=LAST_VALID_JOB_INDEX_PER_FULL_KEY"+
                " ignoredCompletedGeometryBusinessHolds="+skippedCompletedGeometry+" droppedEventsAtCapacity="+droppedAtCapacity+" issue="+(issue??"NONE");
            originalUnpaidFailureFinalSnapshot=()=>
            {
                if(originalUnpaidFailureSnapshotWritten)return;
                try
                {
                    var queue=Read(customers,"queue") as CounterQueueService;var rows=queue?.Snapshot;var visits=Read(customers,"visits") as IDictionary;
                    var visit=rows!=null&&rows.Count>0&&visits!=null&&visits.Contains(rows[0].VisitId)?visits[rows[0].VisitId]:null;
                    var anchor=Read(visit,"PendingReservationAnchor");var entries=Read(core,"entries") as IDictionary;
                    var currentBinding=active&&ReferenceEquals(core,Read(customers,"traffic"))&&ReferenceEquals(core,Read(owner,"traffic"))&&
                        anchor!=null&&ReferenceEquals(Read(anchor,"Coordinator"),core)&&ReferenceEquals(Read(anchor,"Visit"),visit);
                    if(currentBinding && Read(anchor,"Key") is TrafficIntentKey key && entries!=null&&entries.Contains(key) && last.TryGetValue(key,out var record)&&
                        ReferenceEquals(entries[key],record.Entry))
                    {
                        var entry=entries[key];var currentRequest=Read(entry,"Request") as TrafficRequest;var currentQueries=Read(entry,"Queries");
                        originalUnpaidFailureSnapshotWritten=true;
                        try{TestContext.WriteLine("P15_ORIGINAL_UNPAID_FAILURE_LAST status=LAST_KNOWN_NOT_CURRENT_PROOF"+Summary()+
                            " actor="+key.ActorId+" intent="+key.IntentId+" generation="+key.Generation+" epoch="+key.Epoch+
                            " eventFrame="+record.Frame+" lastQueries="+record.Queries+" currentQueries="+currentQueries+
                            " entryRefStillCurrent=True eventRequest="+Ref(record.Request)+" currentRequest="+Ref(currentRequest)+
                            " requestRefStillCurrent="+ReferenceEquals(record.Request,currentRequest)+" historicalOnly=True noNewCurrentRoleEvidence=True\n"+record.Payload);}catch(Exception){}
                        return;
                    }
                    DumpOriginalUnpaidFailureSnapshot("until-failed-no-matching-valid-job-event",null,
                        payload=>TestContext.WriteLine("P15_ORIGINAL_UNPAID_FAILURE_LAST status=UNKNOWN_NO_MATCHING_VALID_JOB_EVENT"+Summary()+"\n"+payload));
                }
                catch(Exception error)
                {
                    Unknown("final-snapshot-error="+error.GetType().FullName+":"+error.Message);
                    DumpOriginalUnpaidFailureSnapshot("until-failed-cache-error",null,
                        payload=>TestContext.WriteLine("P15_ORIGINAL_UNPAID_FAILURE_LAST status=UNKNOWN"+Summary()+"\n"+payload));
                }
            };
            try
            {
                if(core==null||!ReferenceEquals(core,Read(owner,"traffic"))){Unknown("install=current-core-unavailable");return;}
                field=core.GetType().GetField("UnpaidPreparationObserver",flags);if(field==null){Unknown("install=existing-hook-unavailable");return;}
                prior=field.GetValue(core) as Delegate;
                Action<TrafficIntentKey,string,TrafficRequest,TrafficRequest> observe=(key,stage,request,previous)=>
                {
                    if(!active||stage!="actual-spatial-refusal"&&stage!="query-budget-exhausted")return;
                    try
                    {
                        if(!ReferenceEquals(core,Read(customers,"traffic"))||!ReferenceEquals(core,Read(owner,"traffic")))return;
                        var queue=Read(customers,"queue") as CounterQueueService;var rows=queue?.Snapshot;
                        if(rows==null||rows.Count==0||rows[0].VisitId!=key.ActorId)return;
                        var visits=Read(customers,"visits") as IDictionary;if(visits==null||!visits.Contains(key.ActorId))return;
                        var visit=visits[key.ActorId];var anchor=Read(visit,"PendingReservationAnchor");
                        if(anchor==null||!ReferenceEquals(Read(anchor,"Coordinator"),core)||!ReferenceEquals(Read(anchor,"Visit"),visit)||
                            !(Read(anchor,"Key") is TrafficIntentKey anchorKey)||!anchorKey.Equals(key))return;
                        var entries=Read(core,"entries") as IDictionary;if(entries==null||!entries.Contains(key))return;
                        var entry=entries[key];if(!(Read(entry,"Key") is TrafficIntentKey entryKey)||!entryKey.Equals(key)||
                            request==null||!ReferenceEquals(Read(entry,"Request"),request)||!request.Intent.Equals(key))return;
                        var jobs=Read(entry,"Jobs") as IList;var index=(int)Read(entry,"JobIndex");
                        if(jobs==null||index<0||index>=jobs.Count)
                        {if(jobs!=null&&index==jobs.Count)skippedCompletedGeometry++;return;} // Completed geometry business hold is not a failing path role.
                        if(!last.ContainsKey(key)&&last.Count>=16){droppedAtCapacity++;return;}
                        string payload=null;
                        DumpOriginalUnpaidFailureSnapshot(stage,request,value=>payload=value,recordOnly:true);
                        if(payload==null||payload.Contains("status=ERROR")){Unknown("callback=raw-formatter-error");return;}
                        last[key]=(entry,request,Time.frameCount,(int)Read(entry,"Queries"),payload); // Same full key overwrites; no output/final latch here.
                    }
                    catch(Exception error){Unknown("callback-error="+error.GetType().FullName+":"+error.Message);}
                };
                installed=Delegate.Combine(prior,observe);
                originalUnpaidFailureObserverCleanup=()=>
                {
                    active=false;
                    try
                    {var current=field.GetValue(core);if(ReferenceEquals(current,installed))field.SetValue(core,prior);
                        else if(!ReferenceEquals(current,prior))try{TestContext.WriteLine("P15_ORIGINAL_UNPAID_OBSERVER status=UNKNOWN cleanup=holder-replaced-preserved");}catch(Exception){}}
                    catch(Exception error){try{TestContext.WriteLine("P15_ORIGINAL_UNPAID_OBSERVER status=UNKNOWN cleanup-error="+error.GetType().FullName+":"+error.Message);}catch(Exception){}}
                    finally{last.Clear();}
                };
                field.SetValue(core,installed);
            }
            catch(Exception error)
            {Unknown("install-error="+error.GetType().FullName+":"+error.Message);var cleanup=originalUnpaidFailureObserverCleanup;originalUnpaidFailureObserverCleanup=null;cleanup?.Invoke();}
        }

        // One shared formatter: recordOnly caches original events; only failed Until emits the final payload/latch.
        // No decision/refresh/planner/guard/producer API is invoked; errors never replace original behavior.
        private void DumpOriginalUnpaidFailureSnapshot(string stage="until-failed",TrafficRequest observerRequest=null,Action<string> sink=null,bool recordOnly=false)
        {
            if(!recordOnly){if(originalUnpaidFailureSnapshotWritten)return;originalUnpaidFailureSnapshotWritten=true;}
            void Emit(string payload){if(sink!=null)sink(payload);else TestContext.WriteLine(payload);}
            try
            {
                const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
                object Read(object value,string name)=>value==null?null:(value.GetType().GetField(name,flags)??value.GetType().GetField("<"+name+">k__BackingField",flags))?.GetValue(value);
                string Ref(object value)=>value==null?"NULL":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
                string P(Vector3 value)=>value.ToString("F9");
                string Key(TrafficIntentKey value)=>value.Session+"/e"+value.Epoch+"/"+value.ActorId+"/"+value.IntentId+"/g"+value.Generation+"/r"+value.LayoutRevision+"/"+value.Context;
                var text=new System.Text.StringBuilder("P15_ORIGINAL_UNPAID_FAILURE_SNAPSHOT frame="+Time.frameCount+" stage="+stage);
                var core=Read(customers,"traffic");var queue=Read(customers,"queue") as CounterQueueService;
                var visits=Read(customers,"visits") as IDictionary;var rows=queue?.Snapshot;
                if(core==null||!ReferenceEquals(core,Read(owner,"traffic"))||rows==null||rows.Count==0||visits==null)
                {Emit(text+" status=UNKNOWN reason=current-core-or-counter-head-unavailable core="+Ref(core)+" ownerCore="+Ref(Read(owner,"traffic")));return;}
                var head=rows[0];var visit=visits.Contains(head.VisitId)?visits[head.VisitId]:null;var anchor=Read(visit,"PendingReservationAnchor");
                text.Append(" head=").Append(head.VisitId).Append("/slot=").Append(head.SlotIndex).Append("/generation=").Append(head.Generation).Append("/arrived=").Append(head.Arrived)
                    .Append(" visit=").Append(Ref(visit)).Append(" visitState=").Append(Read(visit,"State")).Append(" anchor=").Append(Ref(anchor));
                var entries=Read(core,"entries") as IDictionary;var rawKey=Read(anchor,"Key");
                if(anchor==null||!ReferenceEquals(Read(anchor,"Coordinator"),core)||!(rawKey is TrafficIntentKey)||entries==null||!entries.Contains(rawKey))
                {Emit(text+" status=UNKNOWN reason=original-head-anchor-entry-unavailable anchorCore="+Ref(Read(anchor,"Coordinator")));return;}
                var key=(TrafficIntentKey)rawKey;var entry=entries[key];var request=Read(entry,"Request") as TrafficRequest;var decision=Read(entry,"Decision") as TrafficDecision;
                var snapshot=Read(core,"snapshot") as TrafficWorldSnapshot;var jobs=Read(entry,"Jobs") as IList;var jobIndex=(int)Read(entry,"JobIndex");var parking=(bool)Read(entry,"Parking");
                var hasPrimary=request==null?(bool?)null:!parking||request.PlanPrimaryRoute;var offset=hasPrimary==true?1:0;
                var role=request==null?"UNKNOWN_NO_ORIGINAL_REQUEST":jobs==null?"UNKNOWN_JOBS_UNAVAILABLE":jobs.Count==0?"UNKNOWN_JOBS_CLEARED":
                    jobIndex<0||jobIndex>=jobs.Count?"UNKNOWN_JOBINDEX_OUT_OF_RANGE":hasPrimary==true&&jobIndex==0?"PRIMARY/"+request.ResourceId:
                    jobIndex-offset>=0&&jobIndex-offset<request.Requirements.Count?request.Requirements[jobIndex-offset].ResourceId:"UNKNOWN_ROLE_INDEX";
                var preview=Read(anchor,"Preview");var assignment=Read(preview,"Assignment");
                text.Append(" status=").Append(request==null||jobs==null||jobs.Count==0?"UNKNOWN":"OBSERVED_RAW_FIELDS").Append(" key=").Append(Key(key))
                    .Append(" entry=").Append(Ref(entry)).Append(" entryKey=").Append(Read(entry,"Key") is TrafficIntentKey entryKey?Key(entryKey):"UNKNOWN")
                    .Append(" decision=").Append(Ref(decision)).Append(" decisionKind=").Append(decision?.Kind).Append(" reason=").Append(decision?.Reason)
                    .Append(" disposition=").Append(decision?.FailureDisposition).Append(" acquired=").Append(Read(entry,"AcquiredSequence"))
                    .Append(" request=").Append(Ref(request)).Append(" observerRequest=").Append(Ref(observerRequest))
                    .Append(" currentRequestRefMatches=").Append(observerRequest==null?"NOT_SUPPLIED":ReferenceEquals(request,observerRequest).ToString())
                    .Append(" JobIndex=").Append(jobIndex).Append(" Jobs.Count=").Append(jobs==null?"UNKNOWN":jobs.Count.ToString())
                    .Append(" Parking=").Append(parking).Append(" HasPrimaryDerived=").Append(hasPrimary?.ToString()??"UNKNOWN").Append(" primaryOffsetDerived=").Append(request==null?"UNKNOWN":offset.ToString())
                    .Append(" actualFailedRole=").Append(role).Append(" FirstFrame=").Append(Read(entry,"FirstFrame")).Append(" sequence=").Append(Read(entry,"RequestSequence"))
                    .Append(" queries=").Append(Read(entry,"Queries")).Append(" WaitRemaining=").Append(Read(entry,"WaitRemaining")).Append(" FiniteLimit=").Append(Read(entry,"FiniteLimit"))
                    .Append(" CurrentIndex=").Append(Read(entry,"CurrentIndex")).Append(" CandidateCount=").Append(Read(entry,"CandidateCount"))
                    .Append(" UnpaidEligibilityHeld=").Append(Read(entry,"UnpaidEligibilityHeld")).Append(" queryFault=").Append(Read(entry,"UnpaidEligibilityQueryFault"))
                    .Append(" queued=").Append(Read(entry,"Queued")).Append(" prepared=").Append(Read(entry,"Prepared")).Append(" retired=").Append(Read(entry,"Retired"))
                    .Append(" currentSnapshot=").Append(snapshot==null?"UNKNOWN":snapshot.Version+"/"+snapshot.BodyVersion)
                    .Append(" requestSnapshot=").Append(request?.Snapshot==null?"UNKNOWN":request.Snapshot.Version+"/"+request.Snapshot.BodyVersion)
                    .Append(" preview=").Append(Ref(preview)).Append(" previewOwner=").Append(Ref(Read(preview,"owner"))).Append(" anchorOwner=").Append(Ref(Read(anchor,"Owner")))
                    .Append(" previewOwnerSameAnchor=").Append(ReferenceEquals(Read(preview,"owner"),Read(anchor,"Owner")))
                    .Append(" previewVersion=").Append(Read(preview,"ExpectedVersion")).Append(" previewConsumed=").Append(Read(preview,"Consumed"))
                    .Append(" counterOwner=").Append(Ref(Read(anchor,"Counter"))).Append(" counterClaim=").Append(Ref(Read(anchor,"CounterClaim")));
                if(assignment is QueueAssignment prepared)text.Append(" previewAssignment=").Append(prepared.VisitId).Append("/slot=").Append(prepared.SlotIndex)
                    .Append("/generation=").Append(prepared.Generation).Append("/target=").Append(P(prepared.Position)).Append("/arrived=").Append(prepared.Arrived);
                else text.Append(" previewAssignment=UNKNOWN");
                if(request!=null)
                {
                    text.Append(" originalRequestStart=").Append(P(request.Start)).Append(" target=").Append(P(request.Target)).Append(" radius=").Append(request.Radius.ToString("R"))
                        .Append(" safety=").Append(request.SafetyMargin.ToString("R")).Append(" resource=").Append(request.ResourceId).Append(" PlanPrimaryRoute=").Append(request.PlanPrimaryRoute);
                    for(var index=0;index<request.Requirements.Count;index++)
                    {var proof=request.Requirements[index];text.Append("\nROLE index=").Append(index).Append(" jobIndex=").Append(index+offset).Append(" resource=").Append(proof.ResourceId)
                        .Append(" start=").Append(P(proof.Start)).Append(" goal=").Append(P(proof.Goal)).Append(" clearance=").Append(proof.Clearance.ToString("R"))
                        .Append(" selected=").Append(proof.SelectedJourney).Append(" avoidProposedParking=").Append(proof.AvoidProposedParking)
                        .Append(" obstacles=[").Append(string.Join(";",proof.Obstacles.Select(P))).Append("]");}
                }
                if(jobs!=null)for(var index=0;index<jobs.Count;index++)
                {var job=jobs[index];var nodes=Read(job,"nodes") as IList;text.Append("\nJOB index=").Append(index).Append(" ref=").Append(Ref(job))
                    .Append(" nodesCount=").Append(nodes==null?"UNKNOWN":nodes.Count.ToString()).Append(" current=").Append(Read(job,"current")).Append(" next=").Append(Read(job,"next"))
                    .Append(" started=").Append(Read(job,"started")).Append(" complete=").Append(Read(job,"complete"));}
                Emit(text.ToString());
            }
            catch(Exception error)
            {try{Emit("P15_ORIGINAL_UNPAID_FAILURE_SNAPSHOT status=ERROR "+error.GetType().FullName+": "+error.Message);}catch(Exception){}}
        }

        private void DumpRoutes()
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var world=Find<NavigationWorld>(); var adapter=Find<NavigationLayoutAdapter>();
            foreach(var actor in world.RegisteredActors) TestContext.WriteLine("BODY "+actor.ActorId+" "+actor.transform.position.ToString("F3"));
            var allVisits=(System.Collections.IDictionary)typeof(CustomerFlowController).GetField("visits",flags).GetValue(customers);
            foreach(System.Collections.DictionaryEntry pair in allVisits)
            {
                var type=pair.Value.GetType();
                TestContext.WriteLine("VISIT "+pair.Key+" state="+type.GetField("State").GetValue(pair.Value)+" resume="+type.GetField("ResumeState").GetValue(pair.Value)+
                    " request="+type.GetField("RequestId").GetValue(pair.Value)+" failure="+type.GetField("Failure").GetValue(pair.Value)+
                    " final="+((NavigationTarget)type.GetField("FinalTarget").GetValue(pair.Value)).Position.ToString("F3"));
                var actor=(NavigationActor)type.GetField("Actor").GetValue(pair.Value);
                var route=(System.Collections.Generic.Queue<Vector3>)type.GetField("Route").GetValue(pair.Value);
                if(actor!=null) TestContext.WriteLine("REMAINING "+pair.Key+" agent="+actor.Agent.destination.ToString("F3")+" route="+string.Join(" / ",(route??Enumerable.Empty<Vector3>()).Select(p=>p.ToString("F3"))));
            }
            var pickup=typeof(CustomerFlowController).GetField("pickupQueue",flags).GetValue(customers);
            if(pickup==null) return;
            var counter=(Vector3[])typeof(CustomerFlowController).GetField("counterWaitSlots",flags).GetValue(customers);
            var slots=(Vector3[])typeof(PickupQueueService).GetField("waitSlots",flags).GetValue(pickup);
            TestContext.WriteLine("COUNTER PLANNED "+string.Join(" / ",counter.Select(p=>p.ToString("F3"))));
            TestContext.WriteLine("PICKUP PLANNED "+string.Join(" / ",slots.Select(p=>p.ToString("F3"))));
            var outward=(Vector3)typeof(CustomerFlowController).GetField("pickupOutward",flags).GetValue(customers);
            if(slots.Length<3) return;
            var forward=slots[2]+outward*1.1f;
            TestContext.WriteLine("STRAIGHT FOURTH "+forward.ToString("F3")+" walkable="+adapter.IsPointWalkable(forward)+
                " entrance="+typeof(CustomerFlowController).GetMethod("InEntrance",flags).Invoke(customers,new object[]{forward})+
                " counterNearest="+counter.Min(p=>Vector3.Distance(p,forward)));
            var exit=(Transform)typeof(CustomerFlowController).GetField("exit",flags).GetValue(customers);
            foreach(var body in world.RegisteredActors.Where(a=>customers.Snapshot.Any(v=>v.VisitId==a.ActorId)))
            {
                var obstacles=(Vector3[])typeof(CustomerFlowController).GetMethod("RouteObstacles",flags).Invoke(customers,new object[]{body.ActorId,null,false});
                var raw=adapter.GetCompletePath(body.transform.position,exit.position);
                var planned=typeof(CustomerFlowController).GetMethod("BuildRoute",flags).Invoke(customers,new object[]{body.transform.position,exit.position,body.ActorId,body.Settings,null,false});
                TestContext.WriteLine("EXIT "+body.ActorId+" goal="+exit.position.ToString("F3")+" static="+(raw!=null)+" bodyRoute="+(planned!=null)+" direct="+CustomerRoutePlanner.IsClear(raw,obstacles,.911f));
            }
            var reservations=(System.Collections.IDictionary)typeof(PickupQueueService).GetField("reservations",flags).GetValue(pickup);
            foreach(System.Collections.DictionaryEntry pair in reservations)
            {
                var target=(Vector3)pair.Value.GetType().GetField("Target").GetValue(pair.Value);
                var actor=world.RegisteredActors.FirstOrDefault(a=>a.ActorId==(string)pair.Key); if(actor==null) continue;
                var obstacles=(Vector3[])typeof(CustomerFlowController).GetMethod("RouteObstacles",flags).Invoke(customers,new object[]{pair.Key,null,false});
                var raw=adapter.GetCompletePath(actor.transform.position,target);
                var planned=typeof(CustomerFlowController).GetMethod("BuildRoute",flags).Invoke(customers,new object[]{actor.transform.position,target,pair.Key,actor.Settings,null,false});
                TestContext.WriteLine("RESERVED "+pair.Key+" slot="+pair.Value.GetType().GetField("Slot").GetValue(pair.Value)+" goal="+target.ToString("F3")+
                    " staticPath="+(raw!=null)+" routeNow="+(planned!=null)+" directClear="+CustomerRoutePlanner.IsClear(raw,obstacles,.911f)+" goalNearest="+(obstacles.Length==0?999:obstacles.Min(p=>Vector3.Distance(p,target)))+
                    " startNearest="+(obstacles.Length==0?999:obstacles.Min(p=>Vector3.Distance(p,actor.transform.position))));
                TestContext.WriteLine("OBSTACLES "+string.Join(" / ",obstacles.Select(p=>p.ToString("F3"))));
            }
        }
    }
}
#endif

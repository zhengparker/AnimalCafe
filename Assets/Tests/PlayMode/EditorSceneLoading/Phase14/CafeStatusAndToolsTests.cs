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
        private const string ValidationPath="Assets/Scenes/Validation/Phase14CafeLoop.unity";
        [UnitySetUp] public IEnumerator Setup()
        {
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
            if(scene.IsValid() && scene.isLoaded) { var empty=SceneManager.CreateScene("P14 tools cleanup"); SceneManager.SetActiveScene(empty); yield return SceneManager.UnloadSceneAsync(scene); }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputs ?? Array.Empty<UnityEngine.InputSystem.InputActionAsset>());
            Time.captureDeltaTime=capture; Time.timeScale=scale;
        }
        [UnityTest] public IEnumerator ContinuedArrivalsFillThenResumeCollectionAndDrain()
        {
            // Catches unsafe shared-corridor dispatch after the fifth customer, not just five bounded arrivals.
            Time.captureDeltaTime=0;
            var world=Find<NavigationWorld>(); var ring=new System.Collections.Generic.Queue<string>();
            var nextSample=0d; var captured=false; var projectionCorrect=true; var maximumDelivery=0;
            var totals=new System.Collections.Generic.Dictionary<string,CapacityToken>(); var exits=new System.Collections.Generic.HashSet<string>();
            var completed=new System.Collections.Generic.HashSet<string>(); var cashierDone=new System.Collections.Generic.Dictionary<string,int>(); var prepared=new System.Collections.Generic.Dictionary<string,int>();
            var evidenceActive=true; var exitCorrect=true; var fifoCorrect=true; var capacityCorrect=true;
            var exit=CafeLoopRuntimeProof.Field<Transform>(customers,"exit");
            bool Blocked()=>employees.Snapshot.Any(e=>e.State==EmployeeState.Blocked) || customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked);
            bool Drained()=>customers.Snapshot.Count==0 && owner.Snapshot.Delivery.Count==0 && customers.Capacity.GetCapacities().All(c=>c.Used==0) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle && e.HandleId==0);
            Action<CafeLoopSnapshot> trace=_=>
            {
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
                    .SequenceEqual(Find<PickupCupDisplay>().VisibleOrders.Keys.OrderBy(k=>k.OrderId));
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
                var counts=value.Role==EmployeeRole.Cashier && value.Kind==CafeTaskEventKind.ServiceFinished?cashierDone:value.Role==EmployeeRole.Barista && value.Kind==CafeTaskEventKind.Prepared?prepared:null;
                if(counts!=null) { counts.TryGetValue(value.Checkpoint.VisitId,out var prior); counts[value.Checkpoint.VisitId]=prior+1; }
                if(value.Kind==CafeTaskEventKind.Blocked) TestContext.WriteLine("P14_TASK_FAILURE frame="+Time.frameCount+" actor="+value.ActorId+" handle="+value.HandleId+" request="+value.RequestId+" gen="+value.Generation+" stage="+value.Checkpoint.Stage+" reason="+value.Failure);
            };
            owner.SnapshotChanged+=trace; employees.IntegratedTaskEvent+=task;
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
                CafeLoopRuntimeProof.Set(customers,"clock",new CustomerSpawnClock(()=>0));
                TestContext.WriteLine("P14_HIGH_LOAD input=one-second legitimate minimum random interval, capture="+Time.captureDeltaTime);
                maximumDelivery=0; captured=false; ring.Clear(); tools.HoldCollection(); tools.ResumeArrivals();
                var full=false; deadline=Time.realtimeSinceStartup+150;
                while(Time.realtimeSinceStartup<deadline)
                {
                    full=owner.Snapshot.Delivery.Count>=5 && owner.ViewSnapshot.Counter.Count>0 &&
                        (owner.ViewSnapshot.Reception.StartsWith("Waiting /",StringComparison.Ordinal) || customers.Capacity.GetCapacities().Any(c=>c.Used==c.Limit)) &&
                        employees.Snapshot.Where(e=>e.Role==EmployeeRole.Cashier).All(e=>e.State==EmployeeState.Idle && e.HandleId==0);
                    if(full || Blocked()) break;
                    yield return null;
                }
                tools.HoldArrivals();
                TestContext.WriteLine("P14_FILL_END actualVisits="+customers.Snapshot.Count+" paid="+owner.Snapshot.PaidVisitIds.Count+" delivery="+owner.Snapshot.Delivery.Count+" full="+full+" blocked="+Blocked()+" reception="+owner.ViewSnapshot.Reception);
                DumpRoutes();
                if(full)
                {
                    var head=owner.ViewSnapshot.Counter.First().VisitId;
                    Assert.That(owner.Snapshot.PaidVisitIds.Contains(head),Is.False,"The actual gated head remains unpaid without a cashier handle");
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
                Assert.That(Time.timeAsDouble-started,Is.InRange(.95,5.3),"Restored admission uses a fresh complete random game interval");
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
            finally { evidenceActive=false; owner.SnapshotChanged-=trace; employees.IntegratedTaskEvent-=task; }
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
        private IEnumerator FiveDeliveriesAndDrain()
        {
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
            // Use the real clock's minimum valid interval to establish a repeatable real queue.
            // 仅控制合法到店输入；不写Actor/Route，不伪造业务或Nav回执。
            typeof(CustomerFlowController).GetField("clock",flags).SetValue(customers,new CustomerSpawnClock(()=>0));
            var starts=new System.Collections.Generic.HashSet<long>(); var unsafeStart=false; var explainedWait=false;
            var provedRouteConflict=false; var resumedWaitingHead=false; string waitingHead=null;
            owner.SnapshotChanged+=value=>
            {
                if(!owner.ViewSnapshot.Reception.Contains("Earlier pickup remaining route")) return;
                // This synchronous snapshot follows rejected reserve, before the next World.Step.
                // reserve已回滚；同一同步窗口读取真实body/destination/Route及刚拒绝的候选位。
                var pickup=(PickupQueueService)typeof(CustomerFlowController).GetField("pickupQueue",flags).GetValue(customers);
                var entries=(System.Collections.IDictionary)typeof(PickupQueueService).GetField("reservations",flags).GetValue(pickup);
                var slots=(Vector3[])typeof(PickupQueueService).GetField("waitSlots",flags).GetValue(pickup);
                Assert.That(owner.HoldCollection,Is.True);
                Assert.That(pickup.CanReserve,Is.True,"Owned slots remain contiguous with no advance; the rolled-back tail index is reservation Count");
                var candidate=slots[entries.Count]; // TryReserve chooses reservations.Count before adding.
                var head=customers.CounterAssignments[0].VisitId;
                Assert.That(customers.CounterAssignments[0].Arrived,Is.True);
                var headActor=Find<NavigationWorld>().RegisteredActors.Single(a=>a.ActorId==head);
                var clearance=(float)typeof(CustomerFlowController).GetMethod("RouteClearance",flags).Invoke(customers,new object[]{headActor.Settings})+
                    2*headActor.Settings.MaxSpeed*Mathf.Min(Time.deltaTime,headActor.Settings.MaxSubstepSeconds*headActor.Settings.MaxSubstepsPerFrame);
                var visits=(System.Collections.IDictionary)typeof(CustomerFlowController).GetField("visits",flags).GetValue(customers);
                var conflict=false;
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
                    { conflict=true; if(!provedRouteConflict) TestContext.WriteLine("PREPAY_ROUTE_CONFLICT head="+head+" earlier="+actor.ActorId+" state="+state+" request="+request+" candidate="+candidate.ToString("F4")+" clearance="+clearance.ToString("R")); }
                }
                Assert.That(conflict,Is.True,"The explained wait must name an actual active remaining-route conflict");
                Assert.That(value.Status,Is.Not.EqualTo("Blocked"));
                Assert.That(value.PaidVisitIds,Does.Not.Contain(head));
                Assert.That(value.Orders.Values.Any(order=>order.CustomerId==head),Is.False);
                Assert.That(entries.Contains(head),Is.False,"Rejected prepayment reserve must roll back, without charging");
                waitingHead=waitingHead??head; provedRouteConflict=explainedWait=true;
            };
            employees.IntegratedTaskEvent+=value=>
            {
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
            tools.HoldCollection(); tools.ResumeArrivals();
            yield return Until(()=>unsafeStart || starts.Count>=4,12000); tools.HoldArrivals();
            Assert.That(unsafeStart,Is.False,"A still-owned earlier pickup path cannot cross the newly sold slot");
            Assert.That(explainedWait,Is.True,"Actual prepayment route wait must explain its reason without becoming Blocked");
            Assert.That(provedRouteConflict,Is.True);
            Assert.That(resumedWaitingHead,Is.True,"The same waiting head resumes through the formal service after the earlier customer actually passes");
        }
        private T Find<T>() where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).Single();
        private IEnumerator Until(Func<bool> done,int max=2400)
        { for(var f=0;f<max && !done();f++) { if(owner!=null && (owner.Snapshot.Status=="Blocked" && !string.IsNullOrEmpty(owner.Snapshot.Failure) || customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked))) break; yield return null; } if(!done() && tools!=null) { tools.PrintSnapshot(); DumpRoutes(); }
          Assert.That(done(),Is.True,(owner==null?"No owner":owner.Snapshot.Status+" / "+owner.Snapshot.Failure)+" / "+(customers==null?"":customers.Status)); }
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

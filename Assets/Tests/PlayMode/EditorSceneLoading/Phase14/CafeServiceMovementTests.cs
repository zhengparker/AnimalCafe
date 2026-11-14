#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Capacity;
using AnimalCafe.CafeLoop;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Navigation;
using AnimalCafe.Orders;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode.Phase14
{
    // Armed only on an existing real delivered cup; never replay the callback during cleanup.
    // 真实杯子OnDisable只观察一次，先撤销arm，再执行可能重入Pause/Done的测试动作。
    public sealed class CafeCollectionProjectionObserver:MonoBehaviour
    {
        internal Action Callback;
        internal Exception Error;
        internal int Calls;
        internal bool Armed;
        internal void Disarm(){Armed=false;Callback=null;}
        private void OnDisable()
        {
            if(!Armed)return;
            var callback=Callback;Disarm();Calls++;
            try{callback?.Invoke();}catch(Exception error){Error=error;}
        }
    }

    public sealed partial class CafePickupSideTests
    {
        private static object MovementRead(object instance,string name)
            => instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(instance);

        private IEnumerator AwaitTrafficAdmission(string reason=null)
        {
            var accepted=false;
            yield return Until(()=>accepted || (accepted=customers.TrySpawn()));
            Assert.That(accepted,Is.True,reason??customers.Status);
        }

        // Same-call canonical facts only: no geometry queries or runtime-policy refresh.
        // 同次调用前后只读原账本；不运行ready batch，也不修改任何业务事实。
        private string MovementAdmissionFacts()
            => "next="+MovementRead(customers,"nextVisit")+" visits="+string.Join(";",customers.Snapshot.Select(v=>v.VisitId+"/"+v.State))+
                " actors="+string.Join(";",world.RegisteredActors.Select(a=>a.ActorId).OrderBy(id=>id,StringComparer.Ordinal))+
                " counter="+string.Join(";",customers.CounterAssignments.Select(a=>a.VisitId+"/"+a.Generation+"/"+a.Position.ToString("F9")))+
                " tokens="+string.Join(";",customers.Capacity.GetReservations().Select(r=>
                    System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(r.Token)+"/"+r.OwnerId+"/"+r.Kind+"/"+r.State))+
                " orders="+string.Join(";",owner.Snapshot.Orders.Select(p=>p.Key+"/"+p.Value.State))+
                " paid="+string.Join(";",owner.Snapshot.PaidVisitIds);

        [UnityTest, Timeout(180000)] public IEnumerator PhotoParallelBenchesExposeActualPickupWaitReason()
            => PhotoParallelBenchesBusinessAndAdmissionReadiness(false);

        [UnityTest, Timeout(180000)] public IEnumerator PhotoParallelBenchesConfiguredConflictHoldsAndResumesOriginalAdmission()
            => PhotoParallelBenchesBusinessAndAdmissionReadiness(true);

        private IEnumerator PhotoParallelBenchesBusinessAndAdmissionReadiness(bool requireConflict)
        {
            // 截图估计布局，不能冒充Owner精确存档 / Photo-derived estimate, not an exact saved layout.
            yield return MatrixStart();
            var configuredSpawn=CafeLoopRuntimeProof.Field<Transform>(customers,"spawn");
            var originalSpawn=configuredSpawn.position;
            var originalClock=CafeLoopRuntimeProof.Field<CustomerSpawnClock>(customers,"clock");
            try
            {
            var runtime=Find<AnimalCafe.Decoration.CafeLayoutRuntime>();var decor=Find<DecorationModeController>();
            var edit=CafeLoopRuntimeProof.Field<AnimalCafe.Decoration.DecorationSession>(decor,"session");
            var register=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new AnimalCafe.Layout.GridPosition(1,5));
            Assert.That(edit.BeginExisting(register.InstanceId).Succeeded,Is.True);
            Assert.That(edit.MovePreview(new AnimalCafe.Layout.GridPosition(3,5)).Succeeded,Is.True);yield return MatrixConfirmFurniture();
            var machineSupport=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new AnimalCafe.Layout.GridPosition(4,6));
            Assert.That(edit.BeginExisting(machineSupport.InstanceId).Succeeded,Is.True);
            Assert.That(edit.MovePreview(new AnimalCafe.Layout.GridPosition(5,7)).Succeeded,Is.True);yield return MatrixConfirmFurniture();
            var machine=runtime.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(AnimalCafe.Decoration.FunctionalSurfacePreviewKind.MountedEquipment,machine.InstanceId),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True);Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);yield return null;
            foreach(var pos in new[]{new AnimalCafe.Layout.GridPosition(4,7),new AnimalCafe.Layout.GridPosition(6,7),
                new AnimalCafe.Layout.GridPosition(5,5),new AnimalCafe.Layout.GridPosition(6,5),new AnimalCafe.Layout.GridPosition(7,5)})
                yield return MatrixFurniture("furniture.counter.module.01",pos,0);
            var pickupSupport=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new AnimalCafe.Layout.GridPosition(6,5));
            var catalog=CafeLoopRuntimeProof.Field<AnimalCafe.Content.FurnitureContentCatalog>(decor,"contentCatalog");
            var slot=catalog.BuildSurfaceSlotCatalog(runtime.Layout.GridSettings).GetForSupport(pickupSupport.DefinitionId).First().SlotId;
            Assert.That(decor.TryBeginFunctionalSurfacePreview(AnimalCafe.UI.Decoration.DecorationCatalogueItemKind.PickUpPoint,
                "pickup.point",new AnimalCafe.Layout.SurfaceSlotAddress(pickupSupport.InstanceId,slot)),Is.True);
            Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);yield return null;
            // This variant changes its declared entry configuration before any real customer is born.
            // 独立场景明确配置入口，原截图布局场景保持原入口；真实身体和通行权由正常流程建立。
            if(requireConflict)configuredSpawn.position=new Vector3(1.5f,0,-3.35f);
            decor.ExitDecorationMode();yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            yield return null; // Let the normal owner publish the final layout binding before the input-order control.
            if(Environment.GetCommandLineArgs().Contains("-photoPreferFront"))
            {
                Assert.That(CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue"),Is.Null);
                Assert.That(owner.Snapshot.Orders,Is.Empty);
                var sides=CafeLoopRuntimeProof.Field<NavigationTarget[]>(customers,"pickupSides");
                var preferred=sides.OrderBy(t=>t.Position.z).ToArray();
                CollectionAssert.AreEquivalent(sides.Select(t=>t.Position),preferred.Select(t=>t.Position));
                CafeLoopRuntimeProof.Set(customers,"pickupSides",preferred);
                TestContext.WriteLine("PHOTO_CONTROL only full legal side order changed; front-first; no removed side or bypass");
            }
            CafeLoopRuntimeProof.Set(customers,"clock",new CustomerSpawnClock(()=>.1f)); // One-second valid input, normal admission only.
            if(!requireConflict){tools.ResumeCollection();tools.ResumeArrivals();}
            TestContext.WriteLine("PHOTO_ESTIMATE CR=(3,5)/0 CM=(5,7)/180 pickup=(6,5) ownerConfirmedMiddle=True fixedArrival=1gameSecond rows=(4..6,7),(5..7,5)");
            Assert.That(employees.TryGetBinding(EmployeeRole.Barista,out var binding),Is.True);
            TestContext.WriteLine("PHOTO_BINDING machine="+binding.WorkStationId+" work="+binding.WorkTarget.Position+
                " pickup="+binding.PickUpId+" delivery="+binding.PickUpTarget+" sides="+
                string.Join(";",customers.GetPickupServiceTargets(binding.PickUpId).Select(t=>t.Position)));
            var photoHook=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var previousPhoto=photoHook.GetValue(customers) as Delegate;var photoEvents=new List<string>();
            var photoCore=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic");
            var photoStates=new Dictionary<TrafficIntentKey,string>();
            var departure=CafeLoopRuntimeProof.Field<Transform>(customers,"exit");
            var photoAdmitted=new Dictionary<string,CapacityToken>();var actualPhotoExits=new HashSet<string>();
            var exitErrors=new List<string>();var exitObservers=new List<CafeActualExitObserver>();var trackExits=true;
            void WatchPhoto(CafeLoopSnapshot snapshot)
            {
                try
                {
                    foreach(var visit in customers.Snapshot)
                    {
                        if(photoAdmitted.ContainsKey(visit.VisitId))continue;
                        var id=visit.VisitId;
                        photoAdmitted.Add(id,customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token);
                        var actor=world.RegisteredActors.Single(a=>a.ActorId==id);
                        var exitTolerance=actor.Settings.ArrivalDistance+actor.Settings.Epsilon;
                        var observer=actor.gameObject.AddComponent<CafeActualExitObserver>();exitObservers.Add(observer);observer.VisitId=id;
                        observer.Receipt=(leaving,pose)=>
                        {
                            if(!trackExits)return;
                            try
                            {
                                if(Vector3.Distance(pose,departure.position)>exitTolerance ||
                                    customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,photoAdmitted[leaving])).State!=ReservationState.Released ||
                                    !owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==leaving && o.State==OrderState.Completed) || !actualPhotoExits.Add(leaving))
                                    exitErrors.Add("Invalid or repeated actual exit: "+leaving);
                            }
                            catch(Exception error){exitErrors.Add(error.ToString());}
                        };
                    }
                    if(photoAdmitted.Count>=12)customers.AutoSpawn=false;
                }
                catch(Exception error){exitErrors.Add(error.ToString());}
            }
            string PhotoPath(IEnumerable<Vector3> points)=>string.Join(";",points.Select(v=>v.ToString("F4")));
            void PhotoEntries(string cause,bool force=false)
            {
                var live=CafeLoopRuntimeProof.Field<IDictionary>(photoCore,"entries");
                foreach(DictionaryEntry row in live)
                {
                    var key=(TrafficIntentKey)row.Key;
                    if(key.IntentId!="actual-exit" && key.IntentId!="counter-admission" && key.IntentId!="counter-advance")continue;
                    var e=row.Value;var d=(TrafficDecision)MovementRead(e,"Decision");var leg=MovementRead(e,"Leg");
                    var frozen=(IEnumerable)MovementRead(e,"FrozenOwners");
                    var signature=d?.Kind+"/"+d?.Reason+"/"+MovementRead(e,"AcquiredSequence")+"/"+
                        (leg==null?"NULL":MovementRead(leg,"Index")+"/"+MovementRead(leg,"Stamp")+"/"+MovementRead(leg,"Bound"))+"/"+
                        (frozen==null?"NULL":string.Join(";",frozen.Cast<TrafficIntentKey>()));
                    if(!force && photoStates.TryGetValue(key,out var old) && old==signature)continue;
                    photoStates[key]=signature;
                    if(photoEvents.Count>=1024)continue;
                    var jobs=((IEnumerable)MovementRead(e,"Jobs")).Cast<object>().ToArray();
                    photoEvents.Add("PHOTO_TIMELINE frame="+Time.frameCount+" cause="+cause+" actor="+key.ActorId+" intent="+key.IntentId+
                        " decision="+d?.Kind+" reason="+d?.Reason+" seq="+MovementRead(e,"RequestSequence")+" acquired="+MovementRead(e,"AcquiredSequence")+
                        " frozen="+(frozen==null?"NULL":string.Join(";",frozen.Cast<TrafficIntentKey>().Select(k=>k.ActorId+"/"+k.IntentId)))+
                        " blockers="+string.Join(";",d?.Blockers.Select(x=>x.Owner.ActorId+"/"+x.Owner.IntentId+"/"+x.ResourceId+"/"+x.Proof)??Array.Empty<string>())+
                        " current=["+PhotoPath((Vector3[])MovementRead(e,"CurrentPath"))+"]"+
                        " jobs=["+string.Join("|",jobs.Select(j=>"preferred="+j.GetType().GetProperty("HasPreferredBodies",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(j)+
                            "/path="+PhotoPath((Vector3[])j.GetType().GetProperty("ExpandedPath",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).GetValue(j))))+"]"+
                        " leg="+(leg==null?"NULL":MovementRead(leg,"Index")+"/stamp="+MovementRead(leg,"Stamp")+"/bound="+MovementRead(leg,"Bound")+
                            "/path=["+PhotoPath((Vector3[])MovementRead(leg,"Path"))+"]"));
                }
            }
            Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string> photoObserver=(operation,id,other,target,path,obstacles,result)=>
            {
                if(operation=="segment.submitted")PhotoEntries("native-bind");
                if(photoEvents.Count<1024 && (operation=="route.blocked" || operation=="segment.callback-result" || operation=="segment.submitted"))
                    photoEvents.Add("PHOTO_ORIGINAL frame="+Time.frameCount+" op="+operation+" id="+id+" target="+target+" result="+result);
                if(operation=="segment.submitted" && photoEvents.Count<1024)
                {
                    var originalEntries=CafeLoopRuntimeProof.Field<IDictionary>(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic"),"entries");
                    foreach(DictionaryEntry ownerRow in originalEntries)
                    {
                        var pendingState=MovementRead(ownerRow.Value,"PendingHandoff");if(pendingState==null)continue;
                        var dependencies=(IDictionary)MovementRead(pendingState,"Dependencies");
                        foreach(DictionaryEntry dep in dependencies)
                        {
                            var originalRequest=(TrafficRequest)MovementRead(dep.Key,"Request");if(originalRequest.Intent.ActorId!=id)continue;
                            var originalLeg=MovementRead(dep.Key,"Leg");
                            photoEvents.Add("PHOTO_CAPTURED_DEPENDENCY frame="+Time.frameCount+" predecessor="+((TrafficIntentKey)ownerRow.Key).IntentId+
                                " waiter="+originalRequest.Intent.ActorId+"/"+originalRequest.Intent.IntentId+" sameRegisteredEntry="+ReferenceEquals(originalEntries[originalRequest.Intent],dep.Key)+
                                " resources="+string.Join(";",((IEnumerable)dep.Value).Cast<string>())+" completedCheckpointWake="+((IEnumerable)MovementRead(pendingState,"CompletedBodyWakeups")).Cast<object>().Any(w=>ReferenceEquals(w,dep.Key))+" active="+MovementRead(dep.Key,"Active")+
                                " bound="+(originalLeg==null?"NULL":MovementRead(originalLeg,"Bound").ToString())+" completed="+(originalLeg==null?"NULL":MovementRead(originalLeg,"Completed").ToString()));
                        }
                    }
                }
            };
            photoHook.SetValue(customers,Delegate.Combine(previousPhoto,photoObserver));
            var photoWaitTrace=new TrafficWaitFailureTrace(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic"));
            var failureHook=typeof(ServiceTrafficCoordinator).GetField("WaitFailureObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var previousFailure=failureHook.GetValue(photoCore) as Delegate;
            Action<string> failureCapture=text=>{if(text.StartsWith("EXIT_INITIAL_FREEZE"))TestContext.WriteLine(text);if(text.StartsWith("WAIT_ORIGINAL")){TestContext.WriteLine("PHOTO_ORIGINAL_WAIT "+text);PhotoEntries("original-refusal",true);}};
            failureHook.SetValue(photoCore,Delegate.Combine(previousFailure,failureCapture));
            owner.SnapshotChanged+=WatchPhoto;
            try
            {
            TrafficIntentKey? controlledAdmission=null;TrafficRequest controlledExit=null;var controlledBorn=false;
            if(requireConflict)
            {
                var prefabs=CafeLoopRuntimeProof.Field<NavigationActor[]>(customers,"prefabs");
                var next=CafeLoopRuntimeProof.Field<long>(customers,"nextVisit");
                var priorSettings=prefabs[(int)(next%prefabs.Length)].Settings;
                var incomingSettings=prefabs[(int)((next+1)%prefabs.Length)].Settings;
                var clearance=priorSettings.AgentRadius+incomingSettings.AgentRadius+
                    Mathf.Max(priorSettings.CollisionSkin+priorSettings.Epsilon,incomingSettings.CollisionSkin+incomingSettings.Epsilon);
                var counter=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue");
                Assert.That(counter.NextAdmissionPosition.HasValue,Is.True,"FIXTURE: real Counter has a legal first endpoint");
                var endpoint=counter.NextAdmissionPosition.Value;
                Assert.That(adapter.IsPointWalkable(configuredSpawn.position),Is.True,"FIXTURE: configured entry is on the real NavMesh");
                Assert.That(adapter.IsPointWalkable(departure.position),Is.True);
                var approach=adapter.GetCompletePath(configuredSpawn.position,endpoint);
                Assert.That(approach,Is.Not.Null.And.Not.Empty,"FIXTURE: actual Native entry approach is complete");
                Assert.That(CustomerRoutePlanner.IsClear(approach,new[]{departure.position},clearance),Is.False,
                    "FIXTURE: actual Native approach must cross the fixed exit endpoint clearance");
                Assert.That(Vector3.Distance(configuredSpawn.position,departure.position),Is.GreaterThan(clearance));
                Assert.That(Vector3.Distance(endpoint,departure.position),Is.GreaterThan(clearance));
                TestContext.WriteLine("PHOTO_CONFIGURED_CONFLICT spawn="+configuredSpawn.position+" exit="+departure.position+
                    " endpoint="+endpoint+" clearance="+clearance+" native=["+PhotoPath(approach)+"]");
                yield return AwaitTrafficAdmission();
                var predecessor=customers.Snapshot.Single().VisitId;
                yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==predecessor && o.State==OrderState.ReadyForPickup) &&
                    customers.Snapshot.Single(v=>v.VisitId==predecessor).State==CustomerVisitState.PickupWaiting &&
                    employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                tools.ResumeCollection();
                yield return Until(()=>customers.Snapshot.Any(v=>v.VisitId==predecessor && v.State==CustomerVisitState.Exiting) &&
                    (long)MovementRead(CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[predecessor],"RequestId")>0);
                var priorVisit=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[predecessor];
                var priorMove=MovementRead(priorVisit,"TrafficMove");var priorKey=(TrafficIntentKey)MovementRead(priorMove,"Intent");
                Assert.That(priorKey.IntentId,Is.EqualTo("actual-exit"));
                var priorEntry=CafeLoopRuntimeProof.Field<IDictionary>(photoCore,"entries")[priorKey];
                var priorLeg=MovementRead(priorEntry,"Leg");
                Assert.That(MovementRead(priorLeg,"Bound"),Is.EqualTo(true),"FIXTURE: original real Native exit has actually been accepted");
                controlledExit=(TrafficRequest)MovementRead(priorEntry,"JourneyRequest");
                Assert.That(controlledExit.Target.Equals(departure.position),Is.True);
                Assert.That(customers.TrySpawn(),Is.False,"FIXTURE: the new original manual admission first prepares normally");
                var anchor=CafeLoopRuntimeProof.Field<object>(customers,"pendingAdmissionAnchor");
                Assert.That(anchor,Is.Not.Null);controlledAdmission=(TrafficIntentKey)MovementRead(anchor,"Key");
            }
            var first="";var reached=false;var last="";var records=0;var pickupVisits=new HashSet<string>();
            var heldAdmissions=new HashSet<string>();var resumedAdmissions=new HashSet<string>();
            var heldSources=new Dictionary<TrafficIntentKey,(object Entry,int FirstFrame,int Queries,float Wait)>();
            var resumedSources=new HashSet<TrafficIntentKey>();
            for(var frame=0;frame<7200 && actualPhotoExits.Count<12;frame++)
            {
                foreach(DictionaryEntry row in CafeLoopRuntimeProof.Field<IDictionary>(photoCore,"entries"))
                {
                    var key=(TrafficIntentKey)row.Key;
                    if(key.IntentId!="counter-admission")continue;
                    var heldEntry=row.Value;var heldDecision=(TrafficDecision)MovementRead(heldEntry,"Decision");
                    var actualReadinessHold=(bool)MovementRead(heldEntry,"AdmissionHeld") &&
                        heldDecision.Kind==TrafficDecisionKind.DeferredByBudget && heldDecision.Reason=="admission-exit-readiness-held" &&
                        heldDecision.FailureDisposition==TrafficFailureDisposition.None;
                    if(!heldSources.TryGetValue(key,out var saved))
                    {
                        // Only the current exact readiness decision establishes the first hold receipt.
                        // AdmissionHeld 可跨正常重证保留；普通 budget 状态不能首次充当 readiness 证据。
                        if(!actualReadinessHold)continue;
                        heldAdmissions.Add(key.ActorId);
                        saved=(heldEntry,(int)MovementRead(heldEntry,"FirstFrame"),(int)MovementRead(heldEntry,"Queries"),(float)MovementRead(heldEntry,"WaitRemaining"));
                        heldSources.Add(key,saved);
                    }
                    if(resumedSources.Contains(key) || customers.Snapshot.Any(v=>v.VisitId==key.ActorId))continue;
                    Assert.That(heldEntry,Is.SameAs(saved.Entry));
                    Assert.That(MovementRead(heldEntry,"FirstFrame"),Is.EqualTo(saved.FirstFrame));
                    Assert.That((int)MovementRead(heldEntry,"Queries"),Is.GreaterThanOrEqualTo(saved.Queries),"The original query accounting cannot reset across a held source");
                    Assert.That((float)MovementRead(heldEntry,"WaitRemaining"),Is.LessThanOrEqualTo(saved.Wait));
                    Assert.That(heldDecision.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None));
                    Assert.That(heldDecision.Kind,Is.EqualTo(TrafficDecisionKind.DeferredByBudget).Or.EqualTo(TrafficDecisionKind.Granted),
                        "An observed original hold can pass through normal bounded reproof and uncommitted preparation before birth");
                    Assert.That(heldDecision.AcquiredSequence,Is.Zero);
                    Assert.That(MovementRead(heldEntry,"Active"),Is.EqualTo(false));
                    if(heldDecision.Kind==TrafficDecisionKind.DeferredByBudget)
                        Assert.That(MovementRead(heldEntry,"Prepared"),Is.EqualTo(false));
                    if(actualReadinessHold)Assert.That(heldDecision.Reason,Is.EqualTo("admission-exit-readiness-held"));
                    Assert.That(MovementRead(heldEntry,"FrozenOwners"),Is.Null);Assert.That(MovementRead(heldEntry,"FrozenResources"),Is.Null);
                    if(actualReadinessHold && requireConflict && controlledAdmission.HasValue && key.Equals(controlledAdmission.Value))
                    {
                        var heldRequest=(TrafficRequest)MovementRead(heldEntry,"Request");
                        var pairClearance=heldRequest.Radius+controlledExit.Radius+Mathf.Max(heldRequest.SafetyMargin,controlledExit.SafetyMargin);
                        var heldPath=(Vector3[])MovementRead(heldEntry,"AdmissionFirstLeg");
                        Assert.That(heldPath,Is.Not.Empty,"FIXTURE: inspect the actual stored first leg used by readiness");
                        Assert.That(CustomerRoutePlanner.IsClear(heldPath,new[]{controlledExit.Target},pairClearance),Is.False);
                    }
                    Assert.That(customers.Snapshot.Any(v=>v.VisitId==key.ActorId),Is.False,"Held admission must remain unborn");
                    Assert.That(world.RegisteredActors.Any(v=>v.ActorId==key.ActorId),Is.False);
                    Assert.That(customers.CounterAssignments.Any(v=>v.VisitId==key.ActorId),Is.False);
                    Assert.That(customers.Capacity.GetReservations().Any(v=>v.OwnerId==key.ActorId),Is.False);
                }
                foreach(var v in customers.Snapshot.Where(v=>heldAdmissions.Contains(v.VisitId)))
                {
                    resumedAdmissions.Add(v.VisitId);
                    foreach(var saved in heldSources.Where(p=>p.Key.ActorId==v.VisitId && !resumedSources.Contains(p.Key)))
                    {
                        var resumed=CafeLoopRuntimeProof.Field<IDictionary>(photoCore,"entries")[saved.Key];
                        Assert.That(resumed,Is.SameAs(saved.Value.Entry));
                        Assert.That(MovementRead(resumed,"FirstFrame"),Is.EqualTo(saved.Value.FirstFrame));
                        var primary=(TrafficRequest)MovementRead(resumed,"JourneyRequest");
                        Assert.That(primary,Is.Not.Null);Assert.That(primary.Intent,Is.EqualTo(saved.Key));
                        Assert.That(primary.RequestedFrame,Is.EqualTo(saved.Value.FirstFrame));
                        Assert.That((float)MovementRead(resumed,"WaitRemaining"),Is.LessThanOrEqualTo(saved.Value.Wait));
                        resumedSources.Add(saved.Key);
                    }
                }
                if(requireConflict && !controlledBorn)
                {
                    controlledBorn=customers.TrySpawn();
                    if(controlledBorn)tools.ResumeArrivals();
                }
                PhotoEntries("frame");
                if(first.Length==0 && customers.Snapshot.Count>0)first=customers.Snapshot[0].VisitId;
                foreach(var visit in customers.CafeSnapshot.Where(v=>v.PickupArrived))pickupVisits.Add(visit.VisitId);
                foreach(var order in owner.Snapshot.Orders.Values.Where(o=>o.State==OrderState.Completed))pickupVisits.Add(order.CustomerId);
                reached=first.Length>0 && pickupVisits.Contains(first);
                var state=string.Join(";",customers.Snapshot.Select(v=>v.VisitId+"/"+v.State))+" sideOrder="+string.Join(";",CafeLoopRuntimeProof.Field<NavigationTarget[]>(customers,"pickupSides").Select(t=>t.Position))+" orders="+
                    string.Join(";",owner.Snapshot.Orders.Values.Select(o=>o.CustomerId+"/"+o.State))+" failure="+owner.Snapshot.Failure;
                if(state!=last){if(records<32)TestContext.WriteLine("PHOTO_STATE frame="+frame+" "+state);records++;last=state;}
                if(customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked))
                {
                    var actualVisits=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");
                    foreach(var v in customers.Snapshot.Where(v=>v.State==CustomerVisitState.Blocked))
                        TestContext.WriteLine("PHOTO_FIRST_BLOCKED frame="+frame+" id="+v.VisitId+" reason="+MovementRead(actualVisits[v.VisitId],"Failure"));
                    break;
                }
                yield return null;
            }
            tools.HoldArrivals();tools.PrintSnapshot();
            var plannedCounter=CafeLoopRuntimeProof.Field<Vector3[]>(customers,"counterWaitSlots");
            var customerPrefabs=CafeLoopRuntimeProof.Field<NavigationActor[]>(customers,"prefabs");
            var geometryClearance=customerPrefabs.Max(p=>2*p.Settings.AgentRadius+p.Settings.CollisionSkin+p.Settings.Epsilon+p.Settings.ArrivalDistance);
            TestContext.WriteLine("PHOTO_PLANNED_COUNTER clearance="+geometryClearance+" allSlots="+string.Join(";",plannedCounter));
            var headsMethod=typeof(CustomerFlowController).GetMethod("PickupWaitHeads",BindingFlags.Instance|BindingFlags.NonPublic);
            var entranceMethod=typeof(CustomerFlowController).GetMethod("InEntrance",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(var side in CafeLoopRuntimeProof.Field<NavigationTarget[]>(customers,"pickupSides").OrderBy(t=>t.Position.z).Take(1))
            {
                var outward=-side.Facing.Value;
                var heads=(IEnumerable<Vector3>)headsMethod.Invoke(customers,new object[]{side.Position+outward*1.1f,outward,true});
                foreach(var point in heads.Take(16))
                    TestContext.WriteLine("PHOTO_GEOMETRY_PROBE independent final-world head="+point+" walkable="+adapter.IsPointWalkable(point)+
                        " entrance="+entranceMethod.Invoke(customers,new object[]{point})+" counterMin="+plannedCounter.Min(v=>Vector3.Distance(v,point))+
                        " actorMin="+world.RegisteredActors.Min(actor=>Vector3.Distance(actor.transform.position,point)));
            }
            var core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic");
            var entries=CafeLoopRuntimeProof.Field<IDictionary>(core,"entries");
            foreach(DictionaryEntry row in entries)
            {
                var entry=row.Value;
                var key=(TrafficIntentKey)row.Key;var decision=(TrafficDecision)MovementRead(entry,"Decision");
                var request=(TrafficRequest)MovementRead(entry,"Request");
                TestContext.WriteLine("PHOTO_ENTRY actor="+key.ActorId+" intent="+key.IntentId+" kind="+decision?.Kind+" reason="+decision?.Reason+
                    " seq="+MovementRead(entry,"RequestSequence")+" acquired="+MovementRead(entry,"AcquiredSequence")+
                    " queries="+MovementRead(entry,"Queries")+" wait="+MovementRead(entry,"WaitRemaining")+
                    " start="+request?.Start+" target="+request?.Target+" blockers="+
                    string.Join(";",decision?.Blockers.Select(b=>b.Owner.ActorId+"/"+b.ResourceId+"/"+b.Proof)??Array.Empty<string>()));
            }
            TestContext.WriteLine("PHOTO_RESULT first="+first+" actualPaid="+owner.Snapshot.PaidVisitIds.Contains(first)+" pickupArrived="+reached+" distinctPickupVisits="+pickupVisits.Count+" completed="+owner.Snapshot.Orders.Values.Count(o=>o.State==OrderState.Completed));
            // Render only the already-observed layout; no game/traffic state mutation or scene save.
            var camera=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<UnityEngine.Camera>(true)).First(c=>c.enabled && c.cameraType==CameraType.Game);
            var render=new RenderTexture(664,855,24);var image=new Texture2D(664,855,TextureFormat.RGB24,false);
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var oldAspect=camera.aspect;
            try
            {
                camera.targetTexture=render;camera.aspect=664f/855;camera.Render();RenderTexture.active=render;
                image.ReadPixels(new Rect(0,0,664,855),0,0);image.Apply();
                var screenshotPath=System.IO.Path.Combine(Application.dataPath,requireConflict
                    ? "../outputs/phase15/photo-pickup-wait-20261103/configured-conflict-layout.png"
                    : "../outputs/phase15/photo-pickup-wait-20261103/layout.png");
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(screenshotPath));
                System.IO.File.WriteAllBytes(screenshotPath,image.EncodeToPNG());
            }
            finally{camera.targetTexture=oldTarget;camera.aspect=oldAspect;RenderTexture.active=oldActive;UnityEngine.Object.Destroy(render);UnityEngine.Object.Destroy(image);}
            TestContext.WriteLine("PHOTO_READINESS held="+string.Join(";",heldAdmissions)+" resumed="+string.Join(";",resumedAdmissions));
            if(requireConflict)
            {
                Assert.That(controlledAdmission.HasValue,Is.True);
                Assert.That(heldAdmissions,Is.Not.Empty,"Require actual configured-layout unborn readiness hold");
                Assert.That(resumedAdmissions,Is.Not.Empty,"Held original admission must later resume naturally");
                Assert.That(heldSources.ContainsKey(controlledAdmission.Value),Is.True,"The specifically submitted original source must actually hold");
                Assert.That(resumedSources.Contains(controlledAdmission.Value),Is.True,"The exact held key must actually resume and give birth");
            }
            Assert.That(pickupVisits.Count,Is.GreaterThanOrEqualTo(4),"Photo-derived multi-guest reproduction: inspect actual payments and blocker facts");
            yield return Until(()=>customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True,"真实排空后原容量归零 / Real departure releases capacity");
            Assert.That(owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed),Is.True);
            Assert.That(exitErrors,Is.Empty);
            Assert.That(photoAdmitted.Count,Is.EqualTo(12),"Require the same bounded twelve-customer business cohort");
            CollectionAssert.AreEquivalent(photoAdmitted.Keys,actualPhotoExits,"Every admitted body must reach the fixed exit and release its original capacity token exactly once");
            Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(12));
            Assert.That(owner.Snapshot.PaidVisitIds.Count,Is.EqualTo(12));
            Assert.That(world.RegisteredActors.Count,Is.EqualTo(2),"Both staff remain after every customer actually departs");
            TestContext.WriteLine("PHOTO_FINAL_DRAIN orders="+owner.Snapshot.Orders.Count+" completed="+owner.Snapshot.Orders.Values.Count(o=>o.State==OrderState.Completed)+
                " visits="+customers.Snapshot.Count+" staffIdle="+employees.Snapshot.All(e=>e.State==EmployeeState.Idle)+" capacityZero="+customers.Capacity.GetCapacities().All(c=>c.Used==0));
            }
            finally
            {
                trackExits=false;owner.SnapshotChanged-=WatchPhoto;customers.AutoSpawn=false;
                foreach(var observer in exitObservers)if(observer!=null)observer.Receipt=null;
                photoHook.SetValue(customers,previousPhoto);
                failureHook.SetValue(photoCore,previousFailure);
                photoWaitTrace.Dispose();
                foreach(var photoEvent in photoEvents)TestContext.WriteLine(photoEvent);
            }
            }
            finally
            {
                customers.AutoSpawn=false;
                configuredSpawn.position=originalSpawn;
                CafeLoopRuntimeProof.Set(customers,"clock",originalClock);
            }
        }

        [UnityTest] public IEnumerator LaterPaidGuestStartsPickupBeforeReadyAndQueuesWhileEarlierGuestRemainsInside()
        {
            // 保留真实付款、制作与身体；只暂停取杯以保留前客 / Hold collection, preserve real service.
            yield return AwaitTrafficAdmission();
            var first=customers.Snapshot.Single().VisitId;
            yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==first && o.State==OrderState.ReadyForPickup) &&
                customers.Snapshot.Single(v=>v.VisitId==first).State==CustomerVisitState.PickupWaiting &&
                employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            var firstBody=world.RegisteredActors.Single(a=>a.ActorId==first);
            yield return AwaitTrafficAdmission();
            var second=customers.Snapshot.Single(v=>v.VisitId!=first).VisitId;
            var submittedBeforeReady=false;var observedState=default(OrderState);
            for(var frame=0;frame<6000 && !submittedBeforeReady;frame++)
            {
                Assert.That(customers.Snapshot.All(v=>v.State!=CustomerVisitState.Blocked),Is.True,customers.Status);
                Assert.That(employees.Snapshot.All(e=>e.State!=EmployeeState.Blocked),Is.True,"Unexpected employee Blocked");
                var order=owner.Snapshot.Orders.Values.SingleOrDefault(o=>o.CustomerId==second);
                var visit=customers.Snapshot.Single(v=>v.VisitId==second);
                var live=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor");
                if(order!=null && visit.State==CustomerVisitState.MovingToPickup && live.Contains(second))
                {
                    observedState=order.State;
                    Assert.That(order.State!=OrderState.ReadyForPickup && order.State!=OrderState.Completed,Is.True,
                        "Paid pickup movement should start before coffee is Ready in this fixture");
                    submittedBeforeReady=true;
                }
                if(!submittedBeforeReady)yield return null;
            }
            Assert.That(submittedBeforeReady,Is.True,"Require real second-customer pickup Native movement before Ready");
            yield return Until(()=>customers.PickupAssignments.Any(a=>a.VisitId==second && a.Arrived) &&
                customers.Snapshot.Single(v=>v.VisitId==second).State==CustomerVisitState.PickupWaiting);
            Assert.That(world.RegisteredActors.Contains(firstBody),Is.True,"Earlier guest remains physically inside");
            Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==first).State,Is.EqualTo(OrderState.ReadyForPickup));
            Assert.That(owner.Snapshot.CollectedExitVisitIds,Is.Empty);
            CollectionAssert.AreEqual(new[]{first,second},customers.PickupAssignments.Select(a=>a.VisitId));
            TestContext.WriteLine("PICKUP_BEFORE_READY second="+second+" stateAtNativeIssue="+observedState+
                " actualWaitArrival=True earlierBodyInside=True earlierUncollected=True");
            tools.ResumeCollection();
            yield return Until(()=>customers.Snapshot.Count==0 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed),Is.True);
            Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
        }

        [UnityTest] public IEnumerator RealRapidPaidPickupPrefersAvailableDetourAroundWorkingBody()
        {
            // Same R17 east-only recipe and real rapid pair; preserve the existing finite Working contract.
            // 只验证真实几何选择，不把业务Working提示改成false，也不提交探针的移动许可。
            var recipe=new R17Witness{Seed=1501707,Tracking=false};
            yield return MatrixStart();yield return R17BuildPickup(recipe);
            Find<DecorationModeController>().ExitDecorationMode();
            yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True);tools.HoldCollection();
            yield return AwaitTrafficAdmission();var first=R17CaptureBirth(recipe);
            yield return Until(()=>owner.Snapshot.PaidVisitIds.Contains(first));yield return null;
            yield return R17AwaitOriginalRapidAdmission(recipe,first);var second=R17CaptureBirth(recipe);
            var actualCore=(ServiceTrafficCoordinator)MovementRead(customers,"traffic");
            var entries=(IDictionary)MovementRead(actualCore,"entries");
            TrafficRequest source=null;TrafficDecision observedOriginal=null;NavigationActor workingActor=null;
            for(var frame=0;frame<6000 && source==null;frame++)
            {
                var barista=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);
                if(barista.State==EmployeeState.Working && barista.RemainingWorkSeconds>.25f &&
                    !((IDictionary)MovementRead(world.Service,"byActor")).Contains(barista.ActorId))
                    foreach(DictionaryEntry pair in entries)
                    {
                        var key=(TrafficIntentKey)pair.Key;if(key.ActorId!=second || key.IntentId!="pickup-paid-movement")continue;
                        var decision=(TrafficDecision)MovementRead(pair.Value,"Decision");
                        if(decision.Kind!=TrafficDecisionKind.WaitingForTraffic && decision.Kind!=TrafficDecisionKind.DeferredByBudget)continue;
                        if(decision.FailureDisposition!=TrafficFailureDisposition.None || (bool)MovementRead(pair.Value,"Retired") ||
                            (bool)MovementRead(pair.Value,"Active") || (bool)MovementRead(pair.Value,"Prepared") ||
                            (long)MovementRead(pair.Value,"AcquiredSequence")!=0 || MovementRead(pair.Value,"Leg")!=null ||
                            MovementRead(pair.Value,"JourneyPermit")!=null || MovementRead(pair.Value,"JourneyRequest")!=null ||
                            ((IDictionary)MovementRead(world.Service,"byActor")).Contains(second))continue;
                        if(decision.Kind==TrafficDecisionKind.WaitingForTraffic && (decision.Blockers.Count==0 ||
                            !decision.Blockers.All(b=>b.ResourceId=="body" && b.Owner.ActorId==barista.ActorId)))continue;
                        if(decision.Kind==TrafficDecisionKind.DeferredByBudget && !(bool)MovementRead(pair.Value,"Queued"))continue;
                        var candidate=(TrafficRequest)MovementRead(pair.Value,"Request");
                        if(candidate==null || !candidate.NativeStamp.Equals(default(TrafficNativeStamp)) || candidate.SelectedJourney.Count!=0)continue;
                        source=candidate;observedOriginal=decision;
                        workingActor=world.RegisteredActors.Single(a=>a.ActorId==barista.ActorId);break;
                    }
                if(source==null)yield return null;
            }
            Assert.That(source,Is.Not.Null,"PREMISE_FAIL: no real unacquired/unissued second-customer paid-route Deferred or body-only Waiting on a Working Barista with Native0 was observed");
            var worker=employees.Snapshot.Single(e=>e.ActorId==workingActor.ActorId);
            Assert.That(worker.State,Is.EqualTo(EmployeeState.Working));
            Assert.That(employees.TryGetBinding(EmployeeRole.Barista,out var binding),Is.True);
            Assert.That(Vector3.Distance(workingActor.transform.position,binding.WorkTarget.Position),Is.LessThanOrEqualTo(workingActor.Settings.ArrivalDistance));
            var actualEmployee=((IEnumerable)MovementRead(employees,"employees")).Cast<object>().Single(e=>(string)MovementRead(e,"Id")==worker.ActorId);
            var runner=(EmployeeTaskRunner)MovementRead(actualEmployee,"Runner");var leg=MovementRead(runner,"moveLeg");
            Assert.That(leg==null || (bool)MovementRead(leg,"Completed") || (long)MovementRead(leg,"RequestId")==0,Is.True);
            Assert.That(((IDictionary)MovementRead(world.Service,"byActor")).Contains(worker.ActorId),Is.False);
            Assert.That(owner.Snapshot.PaidVisitIds.Contains(second),Is.True,"PREMISE_FAIL: second payment must be genuine");
            var facts=CurrentTrafficFacts();var workerFact=facts.Bodies.Single(b=>b.Owner.ActorId==worker.ActorId);
            Assert.That(workerFact.ActorLife,Is.SameAs(workingActor));
            Assert.That(workerFact.Enabled && workerFact.ActorActive && workerFact.IndependentlyProgressing && workerFact.RemainingSeconds>0,Is.True,
                "PREMISE_FAIL: preserve the original finite Working business fact");
            var movingActor=world.RegisteredActors.Single(a=>a.ActorId==second);
            Assert.That(source.Start.Equals(movingActor.transform.position),Is.True,"PREMISE_FAIL: original unpaid-to-paid movement has not physically started");
            var bodies=facts.Bodies.Where(b=>b.Owner.ActorId!=second).ToArray();
            var parking=facts.Parking.Where(p=>p.Owner.ActorId!=second).ToArray();
            var occupied=bodies.Select(b=>b.Position).Concat(parking.Select(p=>p.Position)).Distinct().ToArray();
            var clearance=bodies.Select(b=>source.Radius+b.Radius+Mathf.Max(source.SafetyMargin,b.SafetyMargin))
                .Concat(parking.Select(p=>source.Radius+p.Radius+Mathf.Max(source.SafetyMargin,p.SafetyMargin))).Max();
            var witnessQueries=0;
            IReadOnlyList<Vector3> Native(Vector3 from,Vector3 to)
            {
                Assert.That(witnessQueries++,Is.LessThan(16384),"PREMISE_FAIL: bounded independent Native geometry witness exhausted");
                return adapter.GetCompletePath(from,to);
            }
            var detour=new CustomerRoutePlanner().Build(source.Start,source.Target,occupied,clearance,Native);
            Assert.That(detour,Is.Not.Null,"PREMISE_FAIL: no independent complete body-aware detour was proven; do not change Working policy on this result");
            Assert.That(detour.Count,Is.LessThanOrEqualTo(24),"PREMISE_FAIL: witness exceeds existing owned-route waypoint limit");
            var expanded=new List<Vector3>{source.Start};var previous=source.Start;
            foreach(var target in detour)
            {
                var path=Native(previous,target);Assert.That(path,Is.Not.Null.And.Not.Empty);
                Assert.That(Vector3.Distance(path[0],previous),Is.LessThanOrEqualTo(movingActor.Settings.ArrivalDistance));
                Assert.That(Vector3.Distance(path[path.Count-1],target),Is.LessThanOrEqualTo(movingActor.Settings.ArrivalDistance));
                Assert.That(CustomerRoutePlanner.IsClear(path,occupied,clearance),Is.True,"PREMISE_FAIL: all current physical bodies and canonical parking remain obstacles");
                expanded.AddRange(path);previous=path[path.Count-1];
            }
            Assert.That(Vector3.Distance(previous,source.Target),Is.LessThanOrEqualTo(movingActor.Settings.ArrivalDistance));
            expanded.Add(source.Target);
            Assert.That(CustomerRoutePlanner.IsClear(expanded,occupied,clearance),Is.True,
                "PREMISE_FAIL: the full assembled path includes every sampled connector and the exact original target");
            // Read-only controlled geometry comparison: same real descriptor/world/requirements, no commit or Native movement.
            var probe=new ServiceTrafficCoordinator(source.Intent.Session,source.Intent.Epoch,source.Intent.LayoutRevision,facts,adapter.GetCompletePath);
            var request=new TrafficRequest(source.Intent,source.RequestedFrame,facts,source.Start,source.Target,source.Radius,source.ResourceId,
                source.SelectedJourney,source.Requirements,source.NativeStamp,source.IndependentlyProgressing,source.FiniteSeconds,
                source.ProvenFailure,source.SafetyMargin,source.PlanPrimaryRoute);
            probe.EvaluateMovement(request);TrafficDecision result=null;
            for(var round=0;round<64;round++)
            {
                probe.ProcessReady(Time.frameCount);result=probe.GetDecision(source.Intent);
                if(result.Kind!=TrafficDecisionKind.DeferredByBudget)break;
                yield return null;
            }
            TestContext.WriteLine("REAL_WORKING_DETOUR observedOriginal="+observedOriginal.Kind+"/"+observedOriginal.Reason+
                " sourceWorld="+source.Snapshot.Version+" capturedWorld="+facts.Version+" worker="+worker.ActorId+
                " workRemaining="+worker.RemainingWorkSeconds+" native0=True businessFinite="+workerFact.IndependentlyProgressing+
                " witnessQueries="+witnessQueries+" clearance="+clearance+" from="+source.Start.ToString("F9")+" goal="+source.Target.ToString("F9")+
                " safeDetour="+string.Join(";",expanded.Select(p=>p.ToString("F9")))+" probe="+result.Kind+"/"+result.Reason);
            Assert.That(result.Kind==TrafficDecisionKind.WaitingForTraffic || result.Kind==TrafficDecisionKind.Granted,Is.True,
                "PREMISE_FAIL: controlled comparison must isolate Waiting versus a safe primary route");
            if(result.Kind==TrafficDecisionKind.WaitingForTraffic)
                Assert.That(result.Blockers.Count>0 && result.Blockers.All(b=>b.ResourceId=="body" && b.Owner.ActorId==worker.ActorId),Is.True,
                    "PREMISE_FAIL: another owner or canonical dependency cannot be bypassed");
            Assert.That(result.Kind,Is.EqualTo(TrafficDecisionKind.Granted),
                "FEATURE: an independently proven body-aware detour should be preferred to waiting through a stationary Working body");
        }

        [UnityTest] public IEnumerator CustomerPaidPickupNativeIssueHasBoundStrictCurrentLeg() => NativeIssueHasStrictLeg(false);
        [UnityTest] public IEnumerator EmployeePreparedDeliveryNativeIssueHasBoundStrictCurrentLeg() => NativeIssueHasStrictLeg(true);

        [UnityTest] public IEnumerator CounterFinalNativeArrivalCreatesPendingCanonicalHandoff() => FinalNativeArrivalCreatesPending(false);
        [UnityTest] public IEnumerator PaidPickupFinalNativeArrivalCreatesPendingCanonicalHandoff() => FinalNativeArrivalCreatesPending(true);

        [UnityTest] public IEnumerator RealCollectionProjectionPausePreservesOriginalExitRequestFrame()
            => RealCollectionProjectionPreservesExitAge(false);
        [UnityTest] public IEnumerator RealCollectionProjectionChangedDonePreservesOriginalExitAgeAndBody()
            => RealCollectionProjectionPreservesExitAge(true);

        private IEnumerator RealCollectionProjectionPreservesExitAge(bool changedDone)
        {
            var cups=Find<PickupCupDisplay>();var decor=Find<DecorationModeController>();
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var previous=hook.GetValue(customers);var errors=new List<string>();var observing=true;
            CafeCollectionProjectionObserver projection=null;CafeActualExitObserver exitObserver=null;
            string id=null;object visit=null,admission=null,exitEntry=null;NavigationActor actor=null;
            TrafficIntentKey exitIntent=default;TrafficRequest primaryRequest=null,strictRequest=null;
            TrafficDecision exitSource=null,moveSource=null;TrafficNativeStamp stamp=default;
            var collectionFrame=0;var firstFrame=0;var issueFrame=0;var serviceArrivalFrame=0;var exitFrame=0;
            var exits=0;var acceptedRequest=0L;var exitSequence=0L;var projectionSequence=0L;
            var bound=false;var completedAtProjection=false;var collectedAtProjection=false;var nativeWasLive=false;
            var collectionPose=Vector3.zero;var exitPose=Vector3.zero;var releasedAtExit=false;
            Transform heldCup=null;CafeOrderKey originalKey=default;CafeLoopSnapshot collectedSnapshot=null;
            var capacity=customers.Capacity;CapacityToken total=null;
            long Number(string details,string prefix)
            {
                var field=(details??"").Split(' ').FirstOrDefault(p=>p.StartsWith(prefix,StringComparison.Ordinal));
                return field!=null && long.TryParse(field.Substring(prefix.Length),out var value)?value:0;
            }
            void ReadFirstExitEntry()
            {
                if(id==null || exitEntry!=null)return;
                var coordinator=MovementRead(customers,"traffic") as ServiceTrafficCoordinator;if(coordinator==null)return;
                foreach(DictionaryEntry pair in (IDictionary)MovementRead(coordinator,"entries"))
                {
                    var key=(TrafficIntentKey)pair.Key;if(key.ActorId!=id || key.IntentId!="actual-exit")continue;
                    exitEntry=pair.Value;exitIntent=key;firstFrame=(int)MovementRead(exitEntry,"FirstFrame");return;
                }
            }
            void Observe(string operation,string observedId,string other,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string details)
            {
                if(!observing || observedId!=id)return;
                try
                {
                    if(operation=="arrival.canonical-committed" && (details??"").Contains("phase="+CustomerVisitState.MovingToPickupService))
                        serviceArrivalFrame=Time.frameCount;
                    ReadFirstExitEntry();
                    if(operation!="segment.submitted" || issueFrame!=0 || !(details??"").Contains("accepted=True"))return;
                    var visits=(IDictionary)MovementRead(customers,"visits");if(!visits.Contains(id))return;
                    var current=visits[id];if((CustomerVisitState)MovementRead(current,"State")!=CustomerVisitState.Exiting)return;
                    Assert.That(current,Is.SameAs(visit));Assert.That(MovementRead(current,"Actor"),Is.SameAs(actor));
                    Assert.That(exitEntry,Is.Not.Null,"FIXTURE: real exit issue retains its original phase entry");
                    var move=MovementRead(current,"TrafficMove");Assert.That(move,Is.Not.Null);
                    Assert.That((TrafficIntentKey)MovementRead(move,"Intent"),Is.EqualTo(exitIntent));
                    primaryRequest=MovementRead(exitEntry,"JourneyRequest") as TrafficRequest;
                    strictRequest=MovementRead(exitEntry,"Request") as TrafficRequest;
                    exitSource=MovementRead(exitEntry,"JourneyPermit") as TrafficDecision;
                    moveSource=MovementRead(move,"Source") as TrafficDecision;
                    var leg=MovementRead(exitEntry,"Leg");Assert.That(leg,Is.Not.Null);
                    bound=(bool)MovementRead(leg,"Committed") && (bool)MovementRead(leg,"Bound");
                    stamp=(TrafficNativeStamp)MovementRead(leg,"Stamp");acceptedRequest=Number(details,"request=");
                    var live=(IDictionary)MovementRead(world.Service,"byActor");
                    nativeWasLive=live.Contains(id) && (long)MovementRead(live[id],"Id")==acceptedRequest;
                    issueFrame=Time.frameCount;
                }
                catch(Exception error){errors.Add(error.GetType().Name+": "+error.Message);}
            }
            void AssertObserverErrors()
            {
                Assert.That(errors,Is.Empty,"Read-only original callbacks must not change runtime control flow");
                if(!ReferenceEquals(projection,null))Assert.That(projection.Error,Is.Null,"FIXTURE: authentic cup projection callback failed");
            }
            IEnumerator WaitFor(Func<bool> condition,string premise,bool layoutHold=false)
            {
                // Same existing bounded window, without Until's optional geometry probes or manual processing.
                for(var frame=0;frame<6000 && !condition();frame++)
                {
                    AssertObserverErrors();ReadFirstExitEntry();
                    if(!layoutHold)
                    {
                        Assert.That(customers.Snapshot.All(v=>v.State!=CustomerVisitState.Blocked),Is.True,"FIXTURE: "+premise+" / "+owner.Snapshot.Failure);
                        Assert.That(employees.Snapshot.All(e=>e.State!=EmployeeState.Blocked),Is.True,"FIXTURE: "+premise+" / employee Blocked");
                    }
                    yield return null;
                }
                AssertObserverErrors();Assert.That(condition(),Is.True,"FIXTURE: "+premise);
            }
            void AssertSameSurvivor()
            {
                var visits=(IDictionary)MovementRead(customers,"visits");Assert.That(visits.Contains(id),Is.True);
                Assert.That(visits[id],Is.SameAs(visit));Assert.That(MovementRead(visits[id],"Admission"),Is.SameAs(admission));
                Assert.That(MovementRead(visits[id],"Actor"),Is.SameAs(actor));Assert.That(world.RegisteredActors.Contains(actor),Is.True);
                Assert.That(customers.Capacity,Is.SameAs(capacity));
                Assert.That(capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,total)).State,Is.EqualTo(ReservationState.Occupied));
                Assert.That(owner.Snapshot.CollectedExitVisitIds.Count(v=>v==id),Is.EqualTo(1));
            }
            try
            {
                customers.AutoSpawn=false;var admitted=false;
                yield return WaitFor(()=>admitted || (admitted=customers.TrySpawn()),"real formal admission");
                id=customers.Snapshot.Single().VisitId;actor=world.RegisteredActors.Single(a=>a.ActorId==id);
                visit=((IDictionary)MovementRead(customers,"visits"))[id];admission=MovementRead(visit,"Admission");
                total=capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token;
                yield return WaitFor(()=>owner.Snapshot.Orders.Any(p=>p.Value.CustomerId==id && p.Value.State==OrderState.ReadyForPickup) &&
                    owner.Snapshot.Delivery.Any(d=>d.VisitId==id) && cups.VisibleOrders.Any(p=>owner.Snapshot.Orders.TryGetValue(p.Key,out var order) && order.CustomerId==id),
                    "real paid Ready order, logical delivery and existing table cup");
                originalKey=owner.Snapshot.Orders.Single(p=>p.Value.CustomerId==id).Key;
                Assert.That(owner.Snapshot.Orders[originalKey].State,Is.EqualTo(OrderState.ReadyForPickup));
                Assert.That(owner.Snapshot.PaidVisitIds.Count(v=>v==id),Is.EqualTo(1));
                Assert.That(owner.Snapshot.Delivery.Single(d=>d.VisitId==id).Key,Is.EqualTo(originalKey));
                var tableCup=cups.VisibleOrders[originalKey];Assert.That(tableCup,Is.Not.Null);Assert.That(tableCup.activeInHierarchy,Is.True);
                Assert.That(actor.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="CustomerHeldCup"),Is.False);
                Assert.That(employees.TryGetBinding(EmployeeRole.Barista,out var binding),Is.True);
                var oldEpoch=owner.Snapshot.Epoch;var oldRevision=adapter.Revision;var oldDone=decor.CompletedLayoutChangeVersion;
                hook.SetValue(customers,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)Observe);
                exitObserver=actor.gameObject.AddComponent<CafeActualExitObserver>();exitObserver.VisitId=id;
                exitObserver.Receipt=(leaving,position)=>
                {
                    if(!observing)return;
                    try
                    {
                        Assert.That(leaving,Is.EqualTo(id));exits++;exitFrame=Time.frameCount;exitPose=position;
                        releasedAtExit=capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,total)).State==ReservationState.Released;
                    }
                    catch(Exception error){errors.Add(error.GetType().Name+": "+error.Message);}
                };
                projection=tableCup.AddComponent<CafeCollectionProjectionObserver>();
                projection.Callback=()=>
                {
                    collectedSnapshot=owner.Snapshot;collectionFrame=Time.frameCount;collectionPose=actor.transform.position;
                    completedAtProjection=collectedSnapshot.Orders.TryGetValue(originalKey,out var order) && order.CustomerId==id && order.State==OrderState.Completed;
                    collectedAtProjection=collectedSnapshot.CollectedExitVisitIds.Contains(id);
                    Assert.That(completedAtProjection && collectedAtProjection,Is.True,"FIXTURE: original service canonical collection precedes projection");
                    Assert.That(collectedSnapshot.SessionId,Is.EqualTo(originalKey.SessionId));Assert.That(collectedSnapshot.Epoch,Is.EqualTo(oldEpoch));
                    Assert.That(collectedSnapshot.Delivery.Any(d=>d.Key.Equals(originalKey)),Is.False);Assert.That(cups.VisibleOrders.ContainsKey(originalKey),Is.False);
                    Assert.That(serviceArrivalFrame,Is.GreaterThan(0));Assert.That(serviceArrivalFrame,Is.LessThanOrEqualTo(collectionFrame));
                    Assert.That(MovementRead(visit,"ServiceArrived"),Is.True);Assert.That(MovementRead(visit,"HasCollected"),Is.False,
                        "The earlier collection latch must not change the existing later movement/presentation fact");
                    var pickup=(PickupQueueService)MovementRead(visit,"PickupOwner");
                    Assert.That(pickup.TryGetReservation(id,out var assignment),Is.True);Assert.That(assignment.Arrived,Is.True);
                    Assert.That(assignment.Generation,Is.EqualTo(((QueueAssignment)MovementRead(visit,"Assignment")).Generation));
                    Assert.That(Vector3.Distance(collectionPose,assignment.Position),Is.LessThanOrEqualTo(actor.Settings.ArrivalDistance+.001f));
                    Assert.That(actor.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="CustomerHeldCup"),Is.False);
                    ReadFirstExitEntry();Assert.That(exitEntry,Is.Null,"FIXTURE: projection reentry precedes the later actual-exit port");
                    projectionSequence=(long)MovementRead(visit,"ExitSequence");AssertSameSurvivor();
                    time.SetPaused();Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
                    if(!changedDone)return;
                    decor.EnterDecorationMode();
                    Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,binding.WorkStationId),Is.True);
                    Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True);Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);
                    decor.ExitDecorationMode();
                    Assert.That(decor.CompletedLayoutChangeVersion,Is.GreaterThan(oldDone));Assert.That(owner.Snapshot.Epoch,Is.EqualTo(oldEpoch+1));
                    Assert.That(owner.Snapshot.Orders,Is.Empty);Assert.That(owner.Snapshot.Delivery,Is.Empty);
                    AssertSameSurvivor();Assert.That(actor.transform.position,Is.EqualTo(collectionPose));
                    Assert.That(MovementRead(visit,"HasCollected"),Is.True);Assert.That(MovementRead(visit,"ResumeState"),Is.EqualTo(CustomerVisitState.Exiting));
                    heldCup=actor.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="CustomerHeldCup");
                    exitSequence=(long)MovementRead(visit,"ExitSequence");Assert.That(exitSequence,Is.GreaterThan(0));
                    if(projectionSequence>0)Assert.That(exitSequence,Is.EqualTo(projectionSequence));
                };
                projection.Armed=true;tools.ResumeCollection();
                yield return WaitFor(()=>projection.Calls==1,"one real collected table-cup OnDisable");
                Assert.That(completedAtProjection && collectedAtProjection,Is.True);Assert.That(collectionFrame,Is.GreaterThan(0));
                for(var frame=0;frame<5;frame++)
                {
                    yield return null;AssertObserverErrors();ReadFirstExitEntry();AssertSameSurvivor();
                    Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));Assert.That(actor.transform.position,Is.EqualTo(collectionPose));
                    Assert.That(exitEntry,Is.Null);Assert.That(issueFrame,Is.Zero);Assert.That(exits,Is.Zero);
                    Assert.That(owner.Snapshot.Epoch,Is.EqualTo(changedDone?oldEpoch+1:oldEpoch));
                    if(changedDone)
                    {
                        Assert.That(owner.Snapshot.Orders,Is.Empty);Assert.That(owner.Snapshot.PaidVisitIds,Is.Empty);
                        Assert.That(actor.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="CustomerHeldCup"),Is.SameAs(heldCup));
                        Assert.That(MovementRead(visit,"ExitSequence"),Is.EqualTo(exitSequence));
                    }
                    else Assert.That(owner.Snapshot.Orders[originalKey].State,Is.EqualTo(OrderState.Completed));
                }
                Assert.That(Time.frameCount,Is.GreaterThanOrEqualTo(collectionFrame+5));Assert.That(projection.Calls,Is.EqualTo(1));
                Assert.That(tableCup==null,Is.True,"The collected table cup is removed; the survivor owns a separate hand cup");
                yield return WaitFor(()=>!time.IsResumeBlocked && world.LayoutAvailable && !decor.IsOpen,"confirmed layout may resume",layoutHold:changedDone);
                if(changedDone)Assert.That(adapter.Revision,Is.GreaterThan(oldRevision));
                AssertSameSurvivor();Assert.That(actor.transform.position,Is.EqualTo(collectionPose));
                time.SetNormal();Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Normal));owner.ResumeBlockedFlow(); // Existing explicit recovery, once.
                yield return WaitFor(()=>issueFrame>0,"same real customer accepted and bound actual-exit Native request");
                Assert.That(bound && nativeWasLive,Is.True);Assert.That(acceptedRequest,Is.GreaterThan(0));Assert.That(stamp.RequestId,Is.EqualTo(acceptedRequest));
                Assert.That(exitSource,Is.Not.Null);Assert.That(exitSource,Is.SameAs(moveSource));Assert.That(exitSource.AcquiredSequence,Is.GreaterThan(0));
                Assert.That(primaryRequest,Is.Not.Null);Assert.That(strictRequest,Is.Not.Null);Assert.That(primaryRequest,Is.Not.SameAs(strictRequest));
                Assert.That(primaryRequest.Intent,Is.EqualTo(exitIntent));Assert.That(strictRequest.Intent,Is.EqualTo(exitIntent));
                Assert.That(exitIntent.Session,Is.EqualTo(originalKey.SessionId));Assert.That(exitIntent.Epoch,Is.EqualTo(owner.Snapshot.Epoch));
                Assert.That(exitIntent.LayoutRevision,Is.EqualTo(adapter.Revision));AssertSameSurvivor();
                var currentCup=actor.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="CustomerHeldCup");
                if(changedDone)Assert.That(currentCup,Is.SameAs(heldCup));else heldCup=currentCup;
                var actualSequence=(long)MovementRead(visit,"ExitSequence");Assert.That(actualSequence,Is.GreaterThan(0));
                if(exitSequence>0)Assert.That(actualSequence,Is.EqualTo(exitSequence));else exitSequence=actualSequence;
                if(projectionSequence>0)Assert.That(actualSequence,Is.EqualTo(projectionSequence));
                Assert.That(collectedSnapshot.Orders[originalKey].State,Is.EqualTo(OrderState.Completed));
                TestContext.WriteLine("C3_COLLECTION_AGE done="+changedDone+" actor="+id+" originalEpoch="+oldEpoch+" currentEpoch="+exitIntent.Epoch+
                    " collectionFrame="+collectionFrame+" firstFrame="+firstFrame+" primaryRequestedFrame="+primaryRequest.RequestedFrame+
                    " strictRequestedFrame="+strictRequest.RequestedFrame+" issueFrame="+issueFrame+" request="+acceptedRequest+
                    " realCompleted=True sameVisit=True sameActor=True originalTotalOccupied=True");
                Assert.That(issueFrame,Is.GreaterThan(collectionFrame),"FIXTURE: genuine Pause/Done reentry separated collection from issue");
                Assert.That(firstFrame,Is.EqualTo(collectionFrame),"PRODUCT: actual-exit phase age belongs to the first real canonical collection");
                Assert.That(primaryRequest.RequestedFrame,Is.EqualTo(collectionFrame),"PRODUCT: original primary source uses the real collection latch");
                Assert.That(strictRequest.RequestedFrame,Is.GreaterThanOrEqualTo(collectionFrame),"Strict leg keeps its ordinary later scheduling frame");
                Assert.That(issueFrame,Is.GreaterThanOrEqualTo(strictRequest.RequestedFrame));
                var exitTarget=CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position;
                yield return WaitFor(()=>exits==1 && customers.Snapshot.All(v=>v.VisitId!=id) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle),"actual endpoint Exit and original token release");
                yield return null;AssertObserverErrors();Assert.That(exits,Is.EqualTo(1));Assert.That(exitFrame,Is.GreaterThanOrEqualTo(issueFrame));
                Assert.That(Vector3.Distance(exitPose,exitTarget),Is.LessThanOrEqualTo(.081f));Assert.That(releasedAtExit,Is.True);
                Assert.That(capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,total)).State,Is.EqualTo(ReservationState.Released));
                Assert.That(capacity.GetCapacities().All(c=>c.Used==0),Is.True);Assert.That(actor==null && heldCup==null,Is.True);
                Assert.That(owner.Snapshot.CollectedExitVisitIds.Contains(id),Is.False);Assert.That(projection.Calls,Is.EqualTo(1));
                Assert.That(owner.Snapshot.Epoch,Is.EqualTo(changedDone?oldEpoch+1:oldEpoch));
                if(changedDone){Assert.That(owner.Snapshot.Orders,Is.Empty);Assert.That(owner.Snapshot.PaidVisitIds,Is.Empty);}
                else{Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1));Assert.That(owner.Snapshot.Orders[originalKey].State,Is.EqualTo(OrderState.Completed));}
            }
            finally
            {
                if(!ReferenceEquals(projection,null))projection.Disarm();
                observing=false;if(exitObserver!=null)exitObserver.Receipt=null;
                hook.SetValue(customers,previous);customers.AutoSpawn=false;
            }
        }

        private sealed class CanonicalArrivalWitness
        {
            internal string Id,Callback,CanonicalDetails;
            internal CustomerVisitState Phase,CanonicalState;
            internal NavigationActor Actor;
            internal object Visit,Admission,Entry,Leg,LegActor,BodyLife,CanonicalOwner,Pending;
            internal TrafficIntentKey Intent;
            internal TrafficRequest Request;
            internal TrafficDecision Source,MoveSource;
            internal TrafficRouteProof Proof;
            internal TrafficParkingFact OriginalClaim;
            internal TrafficNativeStamp Stamp;
            internal QueueAssignment Assignment,CanonicalAssignment;
            internal ReservationSnapshot[] BeforeTokens,AfterTokens;
            internal int Generation,LegGeneration,Index,RouteCount,CallbackGeneration,CanonicalGeneration;
            internal long NativeRequest,CanonicalRequest;
            internal float ArrivalTolerance;
            internal Vector3 Endpoint,CallbackPosition,CanonicalPosition;
            internal bool Registered,Committed,Bound,Active,Paid,Order,CallbackArrived,CanonicalObserved;
            internal bool SameVisit,SameAdmission,SameActor,SameLeg,Completed,Retired,CanonicalFlag;
        }

        // Only observe the original submitted/callback/canonical-Mark boundaries.
        // 只读原 Native 与业务事实；不补跑 ready batch、geometry probe 或模拟 callback。
        private IEnumerator FinalNativeArrivalCreatesPending(bool pickup)
        {
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var previous=hook.GetValue(customers);var errors=new List<string>();CanonicalArrivalWitness witness=null;
            object admittedVisit=null,admittedAdmission=null;NavigationActor admittedActor=null;ReservationSnapshot[] admittedTokens=null;
            string admittedId=null;var expectedPhase=pickup?CustomerVisitState.MovingToPickup:CustomerVisitState.Entering;
            long Number(string details,string prefix)
            {
                var field=(details??"").Split(' ').FirstOrDefault(p=>p.StartsWith(prefix,StringComparison.Ordinal));
                return field!=null && long.TryParse(field.Substring(prefix.Length),out var value)?value:0;
            }
            ReservationSnapshot[] Tokens(string id)=>customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).OrderBy(r=>r.Kind).ToArray();
            void Observe(string operation,string id,string other,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string details)
            {
                try
                {
                    var visits=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");
                    if(operation=="admission.published" && admittedVisit==null && visits.Contains(id))
                    {
                        admittedId=id;admittedVisit=visits[id];admittedAdmission=MovementRead(admittedVisit,"Admission");
                        admittedActor=(NavigationActor)MovementRead(admittedVisit,"Actor");admittedTokens=Tokens(id);return;
                    }
                    if(witness==null && operation=="segment.submitted" && (details??"").Contains("accepted=True") && id==admittedId && visits.Contains(id))
                    {
                        var visit=visits[id];if((CustomerVisitState)MovementRead(visit,"State")!=expectedPhase)return;
                        var move=MovementRead(visit,"TrafficMove");if(move==null)return;
                        var proof=(TrafficRouteProof)MovementRead(move,"Proof");var index=(int)MovementRead(move,"Index");
                        var route=(Queue<Vector3>)MovementRead(visit,"Route");
                        if(proof==null || index!=proof.Waypoints.Count-1 || route==null || route.Count!=0)return;
                        var native=Number(details,"request=");var live=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor");
                        if(native<=0 || !live.Contains(id) || (long)MovementRead(live[id],"Id")!=native)return;
                        var intent=(TrafficIntentKey)MovementRead(move,"Intent");
                        var entry=CafeLoopRuntimeProof.Field<IDictionary>(coordinator,"entries")[intent];
                        var request=(TrafficRequest)MovementRead(entry,"Request");var leg=MovementRead(entry,"Leg");
                        var actor=(NavigationActor)MovementRead(visit,"Actor");
                        var canonicalOwner=pickup?MovementRead(visit,"PickupOwner"):MovementRead(customers,"queue");
                        var resource=(pickup?"pickup/":"counter/")+id;
                        witness=new CanonicalArrivalWitness { Id=id,Phase=expectedPhase,Visit=visit,Admission=MovementRead(visit,"Admission"),
                            Actor=actor,Entry=entry,Leg=leg,Intent=intent,Request=request,Proof=proof,Index=index,RouteCount=route.Count,
                            Source=(TrafficDecision)MovementRead(entry,"JourneyPermit"),MoveSource=(TrafficDecision)MovementRead(move,"Source"),
                            NativeRequest=native,Generation=(int)MovementRead(visit,"Generation"),CanonicalOwner=canonicalOwner,
                            Assignment=(QueueAssignment)MovementRead(visit,"Assignment"),BeforeTokens=Tokens(id),Endpoint=target,
                            Registered=actor.isActiveAndEnabled && world.RegisteredActors.Contains(actor),
                            Active=(bool)MovementRead(entry,"Active"),Paid=owner.Snapshot.PaidVisitIds.Contains(id),
                            Order=owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==id),
                            OriginalClaim=request.Snapshot.Parking.SingleOrDefault(p=>p.Owner.Equals(intent) && p.ResourceId==resource && ReferenceEquals(p.CanonicalOwner,canonicalOwner)),
                            BodyLife=request.Snapshot.Bodies.SingleOrDefault(b=>b.Owner.Equals(intent))?.ActorLife,
                            LegActor=leg==null?null:MovementRead(leg,"ActorLife"),
                            LegGeneration=leg==null?0:(int)MovementRead(leg,"Generation"),
                            Committed=leg!=null && (bool)MovementRead(leg,"Committed"),Bound=leg!=null && (bool)MovementRead(leg,"Bound"),
                            Stamp=leg==null?default:(TrafficNativeStamp)MovementRead(leg,"Stamp"),
                            ArrivalTolerance=leg==null?0:(float)MovementRead(leg,"ArrivalTolerance") };
                        return;
                    }
                    if(witness==null || witness.Id!=id)return;
                    if(operation=="segment.callback-result" && Number(details,"request=")==witness.NativeRequest)
                    {
                        witness.Callback=details;witness.CallbackGeneration=(int)Number(details,"issuedGeneration=");
                        witness.CallbackArrived=(details??"").Contains("status=Arrived");witness.CallbackPosition=target;return;
                    }
                    if(operation!="arrival.canonical-committed" || Number(details,"request=")!=witness.NativeRequest)return;
                    witness.CanonicalObserved=true;witness.CanonicalDetails=details;witness.CanonicalPosition=target;
                    witness.CanonicalRequest=Number(details,"request=");witness.CanonicalGeneration=(int)Number(details,"issuedGeneration=");
                    var current=visits.Contains(id)?visits[id]:null;
                    witness.SameVisit=ReferenceEquals(current,witness.Visit);
                    witness.SameAdmission=current!=null && ReferenceEquals(MovementRead(current,"Admission"),witness.Admission);
                    witness.SameActor=current!=null && ReferenceEquals(MovementRead(current,"Actor"),witness.Actor);
                    witness.CanonicalState=(CustomerVisitState)MovementRead(current,"State");
                    witness.CanonicalFlag=(bool)MovementRead(current,pickup?"PickupArrived":"CounterArrived");
                    if(pickup)((PickupQueueService)witness.CanonicalOwner).TryGetReservation(id,out witness.CanonicalAssignment);
                    else witness.CanonicalAssignment=((CounterQueueService)witness.CanonicalOwner).Snapshot.Single(a=>a.VisitId==id);
                    witness.AfterTokens=Tokens(id);
                    witness.SameLeg=ReferenceEquals(MovementRead(witness.Entry,"Leg"),witness.Leg);
                    witness.Completed=witness.Leg!=null && (bool)MovementRead(witness.Leg,"Completed");
                    witness.Pending=MovementRead(witness.Entry,"PendingHandoff");witness.Retired=(bool)MovementRead(witness.Entry,"Retired");
                }
                catch(Exception error){errors.Add(error.GetType().Name+": "+error.Message);}
            }
            try
            {
                customers.AutoSpawn=false;
                hook.SetValue(customers,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)Observe);
                var admitted=false;
                for(var frame=0;frame<6000 && (witness==null || !witness.CanonicalObserved) && errors.Count==0;frame++)
                {
                    if(!admitted)admitted=customers.TrySpawn();
                    Assert.That(customers.Snapshot.All(v=>v.State!=CustomerVisitState.Blocked),Is.True,"FIXTURE: customer blocked before original final arrival");
                    Assert.That(employees.Snapshot.All(e=>e.State!=EmployeeState.Blocked),Is.True,"FIXTURE: employee blocked before original final arrival");
                    yield return null;
                }
                Assert.That(errors,Is.Empty,"Read-only observer must not alter production callback control flow");
                Assert.That(admitted,Is.True);Assert.That(admittedVisit,Is.Not.Null,"FIXTURE: original formal admission publication");
                Assert.That(admittedAdmission,Is.Not.Null);Assert.That(admittedTokens,Has.Length.EqualTo(3));
                Assert.That(admittedTokens.Select(r=>r.Kind),Is.EquivalentTo(new[]{CapacityKind.TotalCustomers,CapacityKind.CounterQueue,CapacityKind.PickUp}));
                Assert.That(admittedTokens.Single(r=>r.Kind==CapacityKind.TotalCustomers).State,Is.EqualTo(ReservationState.Occupied));
                Assert.That(witness,Is.Not.Null,"FIXTURE: original accepted live Native final strict leg is required");
                Assert.That(witness.Visit,Is.SameAs(admittedVisit));Assert.That(witness.Admission,Is.SameAs(admittedAdmission));
                Assert.That(witness.Actor,Is.SameAs(admittedActor));Assert.That(witness.Registered,Is.True);
                if(pickup){Assert.That(witness.Paid,Is.True,"FIXTURE: real payment precedes pickup movement");Assert.That(witness.Order,Is.True,"FIXTURE: real order belongs to this visit");}
                Assert.That(witness.NativeRequest,Is.GreaterThan(0));Assert.That(witness.Generation,Is.GreaterThan(0));
                Assert.That(witness.Source,Is.Not.Null);Assert.That(witness.Source,Is.SameAs(witness.MoveSource));
                Assert.That(witness.Source.AcquiredSequence,Is.GreaterThan(0));Assert.That(witness.Source.ProofRoutes.Contains(witness.Proof),Is.True);
                Assert.That(witness.Leg,Is.Not.Null);Assert.That(witness.Committed,Is.True);Assert.That(witness.Active,Is.True);Assert.That(witness.Bound,Is.True);
                Assert.That(witness.LegActor,Is.SameAs(witness.Actor));Assert.That(witness.BodyLife,Is.SameAs(witness.Actor));
                Assert.That(witness.LegGeneration,Is.EqualTo(witness.Generation));Assert.That(witness.Stamp,Is.EqualTo(new TrafficNativeStamp(witness.NativeRequest,witness.Generation)));
                Assert.That(witness.Index,Is.EqualTo(witness.Proof.Waypoints.Count-1));Assert.That(witness.RouteCount,Is.Zero);
                Assert.That(witness.OriginalClaim,Is.Not.Null,"FIXTURE: original saved strict-leg snapshot contains the actual canonical parking claim");
                Assert.That(witness.Request.Snapshot.Parking.Any(p=>ReferenceEquals(p,witness.OriginalClaim)),Is.True);
                Assert.That(witness.OriginalClaim.CanonicalOwner,Is.SameAs(witness.CanonicalOwner));
                Assert.That(witness.Callback,Is.Not.Null,"FIXTURE: original callback for the issued real Native request");
                Assert.That(witness.CallbackArrived,Is.True,witness.Callback);Assert.That(witness.CallbackGeneration,Is.EqualTo(witness.Generation));
                var delta=witness.CallbackPosition-witness.Endpoint;delta.y=0;
                Assert.That(delta.magnitude,Is.LessThanOrEqualTo(witness.ArrivalTolerance),"FIXTURE: actual final endpoint uses the existing strict leg arrival tolerance");
                Assert.That(witness.CanonicalObserved,Is.True,"FIXTURE: successful canonical Mark after that actual final Arrived");
                Assert.That(witness.CanonicalDetails,Does.Contain("phase="+expectedPhase));
                Assert.That(witness.CanonicalRequest,Is.EqualTo(witness.NativeRequest));Assert.That(witness.CanonicalGeneration,Is.EqualTo(witness.Generation));
                Assert.That(witness.CanonicalPosition,Is.EqualTo(witness.CallbackPosition));
                Assert.That(witness.SameVisit && witness.SameAdmission && witness.SameActor,Is.True);Assert.That(witness.SameLeg,Is.True);Assert.That(witness.Completed,Is.True);
                Assert.That(witness.CanonicalState,Is.EqualTo(pickup?CustomerVisitState.PickupWaiting:CustomerVisitState.Queued));Assert.That(witness.CanonicalFlag,Is.True);
                Assert.That(witness.CanonicalAssignment.VisitId,Is.EqualTo(witness.Id));Assert.That(witness.CanonicalAssignment.Arrived,Is.True);
                Assert.That(witness.CanonicalAssignment.Generation,Is.EqualTo(witness.Assignment.Generation));
                Assert.That(witness.CanonicalAssignment.Position,Is.EqualTo(witness.Assignment.Position));
                Assert.That(witness.BeforeTokens,Has.Length.EqualTo(3));Assert.That(witness.AfterTokens,Has.Length.EqualTo(3));
                foreach(var original in admittedTokens)
                {
                    Assert.That(witness.BeforeTokens.Single(r=>r.Kind==original.Kind).Token,Is.SameAs(original.Token));
                    Assert.That(witness.AfterTokens.Single(r=>r.Kind==original.Kind).Token,Is.SameAs(original.Token));
                }
                var occupiedKind=pickup?CapacityKind.PickUp:CapacityKind.CounterQueue;
                Assert.That(witness.BeforeTokens.Single(r=>r.Kind==occupiedKind).State,Is.EqualTo(ReservationState.Reserved));
                Assert.That(witness.AfterTokens.Single(r=>r.Kind==occupiedKind).State,Is.EqualTo(ReservationState.Occupied));
                Assert.That(witness.AfterTokens.Single(r=>r.Kind==CapacityKind.TotalCustomers).State,Is.EqualTo(ReservationState.Occupied));
                TestContext.WriteLine("C3_CANONICAL_NATIVE pickup="+pickup+" actor="+witness.Id+" phase="+witness.Phase+
                    " request="+witness.NativeRequest+" generation="+witness.Generation+" sourceAcquired="+witness.Source.AcquiredSequence+
                    " savedSnapshot="+witness.Request.Snapshot.Version+" actualArrived="+witness.CallbackArrived+
                    " canonicalArrived="+witness.CanonicalAssignment.Arrived+" originalTokenOccupied=True pending="+(witness.Pending!=null)+" retired="+witness.Retired);
                Assert.That(witness.Pending,Is.Not.Null,"PRODUCT: actual final Arrived plus canonical Mark must begin the bounded pending handoff");
                Assert.That(witness.Retired,Is.False,"PRODUCT: canonical arrival cannot prematurely retire the predecessor owner");
                Assert.That(MovementRead(witness.Pending,"Source"),Is.SameAs(witness.Source));
                Assert.That(MovementRead(witness.Pending,"ActorLife"),Is.SameAs(witness.Actor));
                Assert.That(MovementRead(witness.Pending,"OriginalClaim"),Is.SameAs(witness.OriginalClaim));
                Assert.That(witness.OriginalClaim.BusinessOwner,Is.SameAs(witness.Visit));
                Assert.That(witness.OriginalClaim.ReleaseObligation,Is.SameAs(witness.Admission));
            }
            finally{hook.SetValue(customers,previous);customers.AutoSpawn=false;}
        }

        [UnityTest] public IEnumerator RealPaidPickupNativeFailureRequiresExplicitResumeAndKeepsOrderThroughExit()
            => PaidPickupRecoveryWitness();
        [UnityTest] public IEnumerator FailedPaidPickupResumesAfterEarlierGuestNaturallyLeavesCanonicalFifo()
            => PaidPickupRecoveryWitness(earlierLeaves:true);
        [UnityTest] public IEnumerator AuthorizedPaidRecoverySurvivesSameRevisionUnavailableFrameAndRepeatedResume()
            => PaidPickupRecoveryWitness(suspendRecovery:true);

        private IEnumerator PaidPickupRecoveryWitness(bool earlierLeaves=false,bool suspendRecovery=false)
        {
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var corridorHook=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var pickupHook=typeof(CustomerFlowController).GetField("PickupObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var previousCorridor=corridorHook.GetValue(customers);var previousPickup=pickupHook.GetValue(customers);
            var errors=new List<string>();var records=new List<string>();var productErrors=new List<string>();var printedRecords=0;
            NavigationActor actor=null,blocker=null;PickupQueueService pickup=null;
            object visit=null,paid=null,admission=null,reservationEntry=null,originalTrafficEntry=null;
            PickupQueueService.ReservationClaim claim=null;QueueAssignment assignment=default;
            TrafficDecision source=null;TrafficIntentKey phase=default;TrafficNativeStamp originalStamp=default;
            CafeOrderKey orderKey=default;CapacityToken[] tokens=null;CapacityToken total=null;
            string id=null,originalCallback=null;string[] fifo=null;Vector3 exitPose=default;
            var originalRequest=0L;var firstFrame=-1;var transfers=0;var originalCallbacks=0;var acceptedRequests=new HashSet<long>();
            var resumed=false;var canonicalArrival=false;var nativePickupArrival=false;var realExit=false;var removalSeen=false;
            var ownerResumeCalls=0;var repeatedResumeAttempted=false;var repeatedResumePreserved=false;
            var orderCompletedObservations=0;var completedSeen=false;
            string earlierId=null,laterId=null;CafeOrderKey earlierOrder=default;NavigationActor earlierActor=null;
            CapacityToken earlierTotal=null;var earlierRemoved=false;var earlierExit=false;var earlierVisitRemoved=false;
            var originalFifoIndex=-1;
            var recoveryRequest=0L;var suspensionReleased=false;var suspensionRestored=false;string suspensionCallback=null;
            TrafficIntentKey recoveryIntent=default;TrafficDecision recoverySource=null;object recoveryEntry=null,recoveryLeg=null;
            var recoveryGeneration=0;var recoveryQueries=0;var recoveryLegIndex=0;var suspensionFailedFlag=false;
            IDictionary Visits()=>CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");
            IDictionary Entries()=>CafeLoopRuntimeProof.Field<IDictionary>(coordinator,"entries");
            int CustomerIntentCount()=>Entries().Keys.Cast<TrafficIntentKey>().Count(key=>key.ActorId==id);
            string Field(string value,string name)=>(value??"").Split(' ').FirstOrDefault(p=>p.StartsWith(name+"=",StringComparison.Ordinal))?.Substring(name.Length+1);
            long Request(string value)=>long.TryParse(Field(value,"request"),out var number)?number:0;
            void Safe(Action action){try{action();}catch(Exception error){if(errors.Count<8)errors.Add(error.ToString());}}
            void ProductCheck(Action action){try{action();}catch(AssertionException error){if(productErrors.Count<16)productErrors.Add(error.ToString());}}
            void Record(string value){if(records.Count<24)records.Add("frame="+Time.frameCount+" "+value);}
            void FlushRecords()
            {foreach(var record in records.Skip(printedRecords))TestContext.WriteLine("PAID_RECOVERY_WITNESS "+record);printedRecords=records.Count;}
            void AssertErrors()=>Assert.That(errors,Is.Empty,"FIXTURE: original-boundary observation/injection must succeed");
            void AssertBusiness(bool beforeArrival)
            {
                Assert.That(Visits().Contains(id),Is.True,"Same paid Visit remains until true Exit");
                Assert.That(Visits()[id],Is.SameAs(visit));Assert.That(MovementRead(visit,"Actor"),Is.SameAs(actor));
                Assert.That(actor!=null && actor.isActiveAndEnabled && world.RegisteredActors.Contains(actor),Is.True);
                Assert.That(MovementRead(visit,"Admission"),Is.SameAs(admission));Assert.That(MovementRead(visit,"PaidPickup"),Is.SameAs(paid));
                Assert.That((CafeOrderKey)MovementRead(paid,"Order"),Is.EqualTo(orderKey));
                Assert.That((TrafficIntentKey)MovementRead(paid,"Intent"),Is.EqualTo(phase));
                Assert.That((int)MovementRead(paid,"FirstFrame"),Is.EqualTo(firstFrame));
                if(earlierLeaves)
                {
                    Assert.That(owner.Snapshot.Orders.Keys,Is.EquivalentTo(new[]{earlierOrder,orderKey}));
                    Assert.That(owner.Snapshot.Orders.Values.Count(o=>o.CustomerId==id),Is.EqualTo(1));
                    Assert.That(owner.Snapshot.Orders[earlierOrder].CustomerId,Is.EqualTo(earlierId));
                    Assert.That(owner.Snapshot.PaidVisitIds,Is.EquivalentTo(new[]{earlierId,id}));
                }
                else
                {
                    Assert.That(owner.Snapshot.Orders.Keys,Is.EquivalentTo(new[]{orderKey}));
                    Assert.That(owner.Snapshot.PaidVisitIds,Is.EquivalentTo(new[]{id}));
                }
                Assert.That(owner.Snapshot.Orders[orderKey].CustomerId,Is.EqualTo(id));
                CollectionAssert.AreEquivalent(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
                Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,total)).State,Is.EqualTo(ReservationState.Occupied));
                if(!beforeArrival)return;
                Assert.That(MovementRead(visit,"PickupInTransit"),Is.EqualTo(true));Assert.That(MovementRead(paid,"Transferred"),Is.EqualTo(true));
                Assert.That(MovementRead(visit,"PickupArrived"),Is.EqualTo(false));Assert.That(MovementRead(visit,"HasCollected"),Is.EqualTo(false));
                Assert.That(MovementRead(visit,"PickupOwner"),Is.SameAs(pickup));
                Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations")[id],Is.SameAs(reservationEntry));
                Assert.That(pickup.TryCaptureReservation(id,out var current),Is.True);Assert.That(claim.IsSameReservation(current),Is.True);
                Assert.That(claim.IsCurrent && current.IsCurrent,Is.True);Assert.That(current.Assignment.Generation,Is.EqualTo(assignment.Generation));
                Assert.That(current.Assignment.SlotIndex,Is.EqualTo(assignment.SlotIndex));
                Assert.That(current.Assignment.Position,Is.EqualTo(assignment.Position));Assert.That(current.Assignment.Arrived,Is.False);
                if(earlierLeaves)
                    CollectionAssert.AreEqual(fifo.Where(v=>!earlierRemoved || v!=earlierId),pickup.Snapshot.Select(a=>a.VisitId),
                        "Natural predecessor removal preserves canonical paid membership and surviving relative order");
                else CollectionAssert.AreEqual(fifo,pickup.Snapshot.Select(a=>a.VisitId));
                Assert.That(customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.PickUp).State,Is.EqualTo(ReservationState.Reserved));
                Assert.That(transfers,Is.EqualTo(1),"Recovery never repeats canonical Transfer/FIFO publication");
            }
            void ObservePickup(string operation,string incoming,PickupQueueService queue,QueueAssignment current)
            {
                if(operation=="transfer.published" && (!earlierLeaves || incoming==laterId))transfers++;
            }
            void ObserveCorridor(string operation,string incoming,string other,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string details)=>Safe(()=>
            {
                if(earlierLeaves && incoming==earlierId)
                {
                    if(operation=="lease.departure-removed")
                    {
                        Assert.That(owner.Snapshot.Orders[earlierOrder].State,Is.EqualTo(OrderState.Completed));
                        Assert.That(pickup.TryGetReservation(earlierId,out _),Is.False);earlierRemoved=true;
                        Record("EARLIER_CANONICAL_REMOVED actor="+earlierId+" order="+earlierOrder.OrderId+" survivingIndex="+
                            Array.FindIndex(pickup.Snapshot.ToArray(),a=>a.VisitId==id));
                    }
                    if(operation=="segment.callback-result" && Field(details,"status")=="Arrived" &&
                        Visits().Contains(earlierId) && (CustomerVisitState)MovementRead(Visits()[earlierId],"State")==CustomerVisitState.Exiting &&
                        Vector3.Distance(target,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position)<=earlierActor.Settings.ArrivalDistance)
                    {earlierExit=true;Record("EARLIER_ACTUAL_EXIT "+details+" pose="+target.ToString("F9"));}
                    if(operation=="visit.before-remove")
                    {Assert.That(earlierExit,Is.True,"FIXTURE: earlier Visit removal follows its actual endpoint Arrived");earlierVisitRemoved=true;}
                    return;
                }
                if(earlierLeaves && incoming!=laterId)return;
                if(id!=null && incoming!=id)return;
                if(operation=="segment.submitted" && Field(details,"accepted")=="True")
                {
                    if(!Visits().Contains(incoming))return;
                    var current=Visits()[incoming];var state=(CustomerVisitState)MovementRead(current,"State");
                    if(id==null)
                    {
                        if(state!=CustomerVisitState.MovingToPickup)return;
                        // Inject only after a real accepted/bound leg, at its exact current target.
                        // 目标不适合安全创建真实 blocker 时继续等待另一腿，不改身体位置或半径。
                        if(world.RegisteredActors.Any(a=>Vector3.Distance(a.transform.position,target)<=.92f))return;
                        var live=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor");
                        Assert.That(live.Contains(incoming),Is.True,"FIXTURE: submitted request is still actually live");
                        originalRequest=Request(details);Assert.That((long)MovementRead(live[incoming],"Id"),Is.EqualTo(originalRequest));
                        visit=current;id=incoming;actor=(NavigationActor)MovementRead(visit,"Actor");paid=MovementRead(visit,"PaidPickup");
                        admission=MovementRead(visit,"Admission");pickup=(PickupQueueService)MovementRead(visit,"PickupOwner");
                        Assert.That(paid,Is.Not.Null);Assert.That(MovementRead(paid,"Accepted"),Is.EqualTo(true));
                        phase=(TrafficIntentKey)MovementRead(paid,"Intent");firstFrame=(int)MovementRead(paid,"FirstFrame");
                        orderKey=(CafeOrderKey)MovementRead(paid,"Order");originalTrafficEntry=Entries()[phase];
                        source=(TrafficDecision)MovementRead(originalTrafficEntry,"JourneyPermit");var leg=MovementRead(originalTrafficEntry,"Leg");
                        Assert.That(source,Is.Not.Null);Assert.That(source.AcquiredSequence,Is.GreaterThan(0));Assert.That(leg,Is.Not.Null);
                        Assert.That(MovementRead(leg,"Committed"),Is.EqualTo(true));Assert.That(MovementRead(leg,"Bound"),Is.EqualTo(true));
                        Assert.That(MovementRead(leg,"Completed"),Is.EqualTo(false));Assert.That(MovementRead(leg,"ActorLife"),Is.SameAs(actor));
                        originalStamp=(TrafficNativeStamp)MovementRead(leg,"Stamp");Assert.That(originalStamp.RequestId,Is.EqualTo(originalRequest));
                        Assert.That(originalStamp.LegGeneration,Is.EqualTo((int)MovementRead(visit,"Generation")));
                        Assert.That(pickup.TryCaptureReservation(id,out claim),Is.True);assignment=claim.Assignment;
                        reservationEntry=CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations")[id];fifo=pickup.Snapshot.Select(a=>a.VisitId).ToArray();
                        originalFifoIndex=Array.IndexOf(fifo,id);
                        if(earlierLeaves)
                        {Assert.That(originalFifoIndex,Is.EqualTo(1),"FIXTURE: later paid guest has an outstanding predecessor");CollectionAssert.AreEqual(new[]{earlierId,id},fifo);}
                        tokens=customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray();
                        total=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token;
                        AssertBusiness(true);
                        Record("BOUND actor="+id+" order="+orderKey.OrderId+" request="+originalRequest+" generation="+originalStamp.LegGeneration+
                            " phase="+phase.IntentId+" phaseGeneration="+phase.Generation+" firstFrame="+firstFrame+
                            " requestSequence="+source.RequestSequence+" acquired="+source.AcquiredSequence+" queries="+coordinator.QueriesConsumed(phase)+
                            " assignmentGeneration="+assignment.Generation+" target="+target.ToString("F9")+" body="+actor.transform.position.ToString("F9"));
                        Record("IDENTITY source="+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(source)+
                            " visit="+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(visit)+" actorInstance="+actor.GetEntityId()+
                            " canonicalEntry="+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(reservationEntry)+
                            " claim="+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(claim)+" tokens="+
                            string.Join(";",tokens.Select(t=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(t).ToString())));
                        blocker=Blocker(target,"paid-native-recovery-obstacle");
                        Assert.That(blocker.transform.position,Is.EqualTo(target));Assert.That(world.RegisteredActors.Contains(blocker),Is.True);
                    }
                    acceptedRequests.Add(Request(details));
                    if(resumed && state==CustomerVisitState.MovingToPickup)
                    {
                        Assert.That(Request(details),Is.Not.EqualTo(originalRequest));
                        Assert.That((int)MovementRead(visit,"Generation"),Is.GreaterThan(originalStamp.LegGeneration));
                        AssertBusiness(true);
                        var active=CafeLoopRuntimeProof.Field<Dictionary<string,TrafficIntentKey>>(customers,"trafficActorKeys")[id];
                        var next=Entries()[active];var nextSource=(TrafficDecision)MovementRead(next,"JourneyPermit");
                        Assert.That(active,Is.Not.EqualTo(phase));Assert.That(nextSource,Is.Not.SameAs(source));
                        Assert.That(MovementRead(originalTrafficEntry,"Retired"),Is.EqualTo(true));
                        Assert.That((int)MovementRead(next,"FirstFrame"),Is.EqualTo(firstFrame));
                        Assert.That(nextSource.RequestSequence,Is.EqualTo(source.RequestSequence));
                        Assert.That(nextSource.AcquiredSequence,Is.EqualTo(source.AcquiredSequence));
                        Assert.That(coordinator.QueriesConsumed(active),Is.GreaterThanOrEqualTo(coordinator.QueriesConsumed(phase)));
                        var nextLeg=MovementRead(next,"Leg");Assert.That(MovementRead(nextLeg,"Bound"),Is.EqualTo(true));
                        Assert.That(((TrafficNativeStamp)MovementRead(nextLeg,"Stamp")).RequestId,Is.EqualTo(Request(details)));
                        Record("RECOVERY_BOUND request="+Request(details)+" firstFrame="+MovementRead(next,"FirstFrame")+
                            " requestSequence="+nextSource.RequestSequence+" acquired="+nextSource.AcquiredSequence+" queries="+coordinator.QueriesConsumed(active));
                        if(recoveryRequest==0)
                        {
                            recoveryRequest=Request(details);recoveryIntent=active;recoverySource=nextSource;recoveryEntry=next;recoveryLeg=nextLeg;
                            recoveryGeneration=(int)MovementRead(visit,"Generation");recoveryQueries=coordinator.QueriesConsumed(active);
                            recoveryLegIndex=(int)MovementRead(nextLeg,"Index");
                        }
                        else if(suspendRecovery && suspensionReleased && Request(details)!=recoveryRequest && !suspensionRestored)
                        {
                            Assert.That(active,Is.EqualTo(recoveryIntent));Assert.That(nextSource,Is.SameAs(recoverySource));
                            Assert.That((int)MovementRead(visit,"Generation"),Is.EqualTo(recoveryGeneration+1));
                            Assert.That((int)MovementRead(nextLeg,"Index"),Is.EqualTo(recoveryLegIndex));
                            Assert.That(coordinator.QueriesConsumed(active),Is.GreaterThanOrEqualTo(recoveryQueries));
                            Assert.That(coordinator.QueriesConsumed(active),Is.LessThanOrEqualTo(16384));
                            Assert.That((int)MovementRead(next,"Suspensions"),Is.EqualTo(1));suspensionRestored=true;
                            Record("RECOVERY_SUSPENSION_RESTORED oldRequest="+recoveryRequest+" newRequest="+Request(details)+
                                " sameSource=True sameIntent=True generation="+MovementRead(visit,"Generation")+" queries="+coordinator.QueriesConsumed(active));
                        }
                        if(!repeatedResumeAttempted && (!suspendRecovery || suspensionRestored))
                        {
                            repeatedResumeAttempted=true;
                            var live=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor");
                            Assert.That(live.Contains(id),Is.True);var native=live[id];var moving=MovementRead(visit,"TrafficMove");
                            var generation=(int)MovementRead(visit,"Generation");var intents=CustomerIntentCount();
                            ownerResumeCalls++;owner.ResumeBlockedFlow();
                            var present=live.Contains(id);var actualNative=present?live[id]:null;
                            var actualRequest=actualNative==null?0:(long)MovementRead(actualNative,"Id");
                            var actualMove=MovementRead(visit,"TrafficMove");var actualGeneration=(int)MovementRead(visit,"Generation");
                            var actualIntents=CustomerIntentCount();var actualState=(CustomerVisitState)MovementRead(visit,"State");
                            var hasOwner=CafeLoopRuntimeProof.Field<Dictionary<string,TrafficIntentKey>>(customers,"trafficActorKeys").TryGetValue(id,out var actualOwner);
                            var actualSource=MovementRead(next,"JourneyPermit");var previousProductErrors=productErrors.Count;
                            Record("REPEATED_OWNER_RESUME calls="+ownerResumeCalls+" expectedNative="+Request(details)+" actualNative="+actualRequest+
                                " sameNativeObject="+ReferenceEquals(actualNative,native)+" sameMove="+ReferenceEquals(actualMove,moving)+
                                " generation="+generation+"->"+actualGeneration+" intentCount="+intents+"->"+actualIntents+
                                " state="+actualState+" sameOwner="+(hasOwner && actualOwner.Equals(active))+" sameSource="+ReferenceEquals(actualSource,nextSource));
                            // These are product postconditions, not failures of the original-boundary observer.
                            // 单独捕获 NUnit 产品断言，所有检查保留；其它 observer 异常仍归 fixture。
                            ProductCheck(()=>Assert.That(present,Is.True,"PRODUCT: repeated Owner Resume retains the live Native request"));
                            ProductCheck(()=>Assert.That(actualNative,Is.SameAs(native)));ProductCheck(()=>Assert.That(actualRequest,Is.EqualTo(Request(details))));
                            ProductCheck(()=>Assert.That(actualMove,Is.SameAs(moving)));ProductCheck(()=>Assert.That(actualGeneration,Is.EqualTo(generation)));
                            ProductCheck(()=>Assert.That(actualIntents,Is.EqualTo(intents)));ProductCheck(()=>Assert.That(actualState,Is.EqualTo(CustomerVisitState.MovingToPickup)));
                            ProductCheck(()=>Assert.That(hasOwner && actualOwner.Equals(active),Is.True));ProductCheck(()=>Assert.That(actualSource,Is.SameAs(nextSource)));
                            ProductCheck(()=>AssertBusiness(true));repeatedResumePreserved=productErrors.Count==previousProductErrors;
                        }
                    }
                }
                if(id==null || incoming!=id)return;
                if(operation=="segment.callback-result")
                {
                    var request=Request(details);var state=(CustomerVisitState)MovementRead(visit,"State");
                    if(request==originalRequest){originalCallbacks++;originalCallback=details;Record("ORIGINAL_RESULT "+details+" finalPose="+target.ToString("F9"));}
                    if(suspendRecovery && suspensionReleased && request==recoveryRequest)
                    {Assert.That(suspensionCallback,Is.Null,"Only one original suspension callback");suspensionCallback=details;Record("RECOVERY_SUSPEND_RESULT "+details);}
                    if(resumed && request!=originalRequest && Field(details,"status")=="Arrived")
                    {
                        if(state==CustomerVisitState.PickupWaiting || state==CustomerVisitState.PickupService || state==CustomerVisitState.MovingToPickup)
                            nativePickupArrival=true;
                        if(state==CustomerVisitState.Exiting && Vector3.Distance(target,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position)<=actor.Settings.ArrivalDistance)
                        {realExit=true;exitPose=target;AssertBusiness(false);Record("ACTUAL_EXIT request="+request+" pose="+target.ToString("F9"));}
                    }
                }
                if(operation=="arrival.canonical-committed" && details.Contains("phase=MovingToPickup "))
                {
                    Assert.That(resumed,Is.True);Assert.That(pickup.TryCaptureReservation(id,out var current),Is.True);
                    Assert.That(claim.IsSameReservation(current),Is.True);Assert.That(current.Assignment.Generation,Is.EqualTo(assignment.Generation));
                    Assert.That(current.Assignment.Arrived,Is.True);canonicalArrival=true;Record("CANONICAL_ARRIVED "+details);
                }
                if(operation=="visit.before-remove")
                {Assert.That(realExit,Is.True,"PRODUCT: removal must follow actual endpoint Arrived");removalSeen=true;}
            });
            try
            {
                customers.AutoSpawn=false;
                corridorHook.SetValue(customers,Delegate.Combine(previousCorridor as Delegate,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)ObserveCorridor));
                pickupHook.SetValue(customers,Delegate.Combine(previousPickup as Delegate,(Action<string,string,PickupQueueService,QueueAssignment>)ObservePickup));
                if(earlierLeaves)
                {
                    var firstAdmitted=false;
                    for(var frame=0;frame<6000 && !firstAdmitted && errors.Count==0;frame++)
                    {firstAdmitted=customers.TrySpawn();yield return null;}
                    AssertErrors();Assert.That(firstAdmitted,Is.True,"FIXTURE: first real admission");
                    earlierId=customers.Snapshot.Single().VisitId;earlierActor=world.RegisteredActors.Single(a=>a.ActorId==earlierId);
                    bool EarlierReady()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==earlierId && o.State==OrderState.ReadyForPickup) &&
                        customers.CafeSnapshot.Any(v=>v.VisitId==earlierId && v.PickupArrived) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle);
                    for(var frame=0;frame<6000 && !EarlierReady() && errors.Count==0;frame++)yield return null;
                    AssertErrors();Assert.That(EarlierReady(),Is.True,"FIXTURE: first paid order genuinely delivered and guest arrived; collection remains held");
                    earlierOrder=owner.Snapshot.Orders.Single(p=>p.Value.CustomerId==earlierId).Key;
                    earlierTotal=customers.Capacity.GetReservations().Single(r=>r.OwnerId==earlierId && r.Kind==CapacityKind.TotalCustomers).Token;
                    Assert.That(owner.HoldCollection,Is.True);Assert.That(owner.Snapshot.PaidVisitIds,Is.EquivalentTo(new[]{earlierId}));
                    Record("EARLIER_READY actor="+earlierId+" order="+earlierOrder.OrderId+" realPickupArrived=True realDelivered=True");
                }
                var admitted=false;
                for(var frame=0;frame<6000 && blocker==null && errors.Count==0;frame++)
                {
                    if(!admitted)admitted=customers.TrySpawn();
                    if(earlierLeaves && admitted && laterId==null)laterId=customers.Snapshot.Single(v=>v.VisitId!=earlierId).VisitId;
                    yield return null;
                }
                AssertErrors();Assert.That(admitted && blocker!=null,Is.True,"FIXTURE: real once-paid accepted/bound Native leg with safe blocker position required");
                Assert.That(acceptedRequests.Contains(originalRequest),Is.True);AssertBusiness(true);
                for(var frame=0;frame<6000 && originalCallback==null && errors.Count==0;frame++)yield return null;
                AssertErrors();Assert.That(originalCallback,Is.Not.Null,"FIXTURE: actual Native terminal failure required, not coordinator refusal or test timeout");
                var failed=Field(originalCallback,"status")=="Failed";
                var recovered=Field(originalCallback,"status")=="Recovered" && Field(originalCallback,"originalFailure")!="None" && Field(originalCallback,"originalFailure")!=null;
                Assert.That(failed || recovered,Is.True,"FIXTURE: authentic Failed or failed-target Recovered required: "+originalCallback);
                Assert.That(originalCallbacks,Is.EqualTo(1));Assert.That((CustomerVisitState)MovementRead(visit,"State"),Is.EqualTo(CustomerVisitState.Blocked));
                Assert.That(MovementRead(paid,"Failed"),Is.EqualTo(true));Assert.That(MovementRead(originalTrafficEntry,"Retired"),Is.EqualTo(true));AssertBusiness(true);
                var failedQueries=coordinator.QueriesConsumed(phase);var failedCount=CustomerIntentCount();var failedRequests=acceptedRequests.Count;
                var failedPose=actor.transform.position;
                for(var repaired=0;repaired<2;repaired++)
                {
                    if(repaired==1)
                    {
                        if(earlierLeaves)
                        {
                            // Release only the existing collection gate: the earlier real guest owns all subsequent business/movement.
                            // 后客真实 Blocked 时，前客自然领取、移除其 reservation 并真实 Exit；不调用 Queue.Remove 或假完成。
                            tools.ResumeCollection();
                            for(var frame=0;frame<6000 && !earlierVisitRemoved && errors.Count==0;frame++)
                            {
                                yield return null;AssertErrors();AssertBusiness(true);
                                Assert.That((CustomerVisitState)MovementRead(visit,"State"),Is.EqualTo(CustomerVisitState.Blocked));
                                Assert.That(acceptedRequests.Count,Is.EqualTo(failedRequests));Assert.That(coordinator.QueriesConsumed(phase),Is.EqualTo(failedQueries));
                            }
                            AssertErrors();Assert.That(earlierRemoved && earlierExit && earlierVisitRemoved,Is.True,
                                "FIXTURE: first guest must naturally collect, remove only its own canonical entry, and actually Exit while later guest remains Blocked");
                            Assert.That(owner.Snapshot.Orders[earlierOrder].State,Is.EqualTo(OrderState.Completed));
                            Assert.That(Visits().Contains(earlierId),Is.False);
                            Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,earlierTotal)).State,Is.EqualTo(ReservationState.Released));
                            Assert.That(originalFifoIndex,Is.EqualTo(1));Assert.That(Array.FindIndex(pickup.Snapshot.ToArray(),a=>a.VisitId==id),Is.Zero);
                            AssertBusiness(true);Record("SURVIVOR_FIFO_COMPRESSED oldIndex=1 newIndex=0 sameEntry=True sameClaim=True sameGeneration=True sameSlotTarget=True");
                        }
                        // Remove only the test's owned obstruction. No pose edits, layout rebuild or Resume.
                        world.Unregister(blocker);blocker.gameObject.SetActive(false);UnityEngine.Object.Destroy(blocker.gameObject);
                        yield return null;Assert.That(blocker==null,Is.True);Assert.That(world.RegisteredActors.Any(a=>a.ActorId=="paid-native-recovery-obstacle"),Is.False);
                    }
                    for(var frame=0;frame<120;frame++)
                    {
                        yield return null;AssertErrors();AssertBusiness(true);
                        Assert.That((CustomerVisitState)MovementRead(visit,"State"),Is.EqualTo(CustomerVisitState.Blocked));
                        Assert.That(MovementRead(paid,"Failed"),Is.EqualTo(true));Assert.That(actor.transform.position,Is.EqualTo(failedPose));
                        Assert.That(acceptedRequests.Count,Is.EqualTo(failedRequests));Assert.That(CustomerIntentCount(),Is.EqualTo(failedCount));
                        Assert.That(coordinator.QueriesConsumed(phase),Is.EqualTo(failedQueries));
                        Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor").Contains(id),Is.False);
                    }
                    Record("NO_RETRY repaired="+(repaired==1)+" frames=120 requests="+failedRequests+" queries="+failedQueries+" sameClaim=True samePaid=True");
                }
                FlushRecords();
                // This is the first recovery authorization; a fixture failure above is never product RED.
                tools.ResumeCollection();resumed=true;ownerResumeCalls++;owner.ResumeBlockedFlow();Record("OWNER_RESUME calls="+ownerResumeCalls);
                if(suspendRecovery)
                {
                    for(var frame=0;frame<6000 && recoveryRequest==0 && errors.Count==0;frame++)yield return null;
                    AssertErrors();Assert.That(recoveryRequest,Is.GreaterThan(0),"FIXTURE: first explicit recovery must actually accept and bind a Native request before suspension");
                    var live=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor");
                    Assert.That(live.Contains(id),Is.True,"FIXTURE: recovery request still live outside the submitted callback");
                    Assert.That((long)MovementRead(live[id],"Id"),Is.EqualTo(recoveryRequest));AssertBusiness(true);
                    var revision=adapter.Revision;var epoch=owner.Snapshot.Epoch;var session=owner.Snapshot.SessionId;
                    var wait=MovementRead(recoveryEntry,"WaitRemaining");var journey=((Vector3[])MovementRead(recoveryEntry,"Journey")).ToArray();
                    Record("RECOVERY_SUSPEND_PRE request="+recoveryRequest+" generation="+recoveryGeneration+" revision="+revision+
                        " firstFrame="+MovementRead(recoveryEntry,"FirstFrame")+" queries="+recoveryQueries);
                    // Exact original M09 lifecycle: release, one ordinary unavailable frame, same-revision rebuild.
                    // 不手动调用 Step/ProcessReady；不修改 pose、Arrived、Failed 或 suspension accounting。
                    suspensionReleased=true;adapter.ReleaseOwnedNavigation();
                    Assert.That(world.LayoutAvailable,Is.False);Assert.That(adapter.Revision,Is.EqualTo(revision));
                    AssertErrors();Assert.That(suspensionCallback,Is.Not.Null,"FIXTURE: original Native LayoutChanged cancellation required");
                    Assert.That(Field(suspensionCallback,"status"),Is.EqualTo("LayoutChanged"));
                    Assert.That(Field(suspensionCallback,"reason"),Is.EqualTo("LayoutUnavailable"));
                    Assert.That(Field(suspensionCallback,"originalFailure"),Is.EqualTo("None"));
                    Assert.That(Field(suspensionCallback,"recoveryFailure"),Is.EqualTo("None"));Assert.That(Field(suspensionCallback,"retry"),Is.EqualTo("0"));
                    Assert.That(live.Contains(id),Is.False);Assert.That(MovementRead(recoveryLeg,"Suspended"),Is.EqualTo(true));
                    Assert.That(MovementRead(recoveryLeg,"Completed"),Is.EqualTo(false));
                    Assert.That((int)MovementRead(recoveryLeg,"Index"),Is.EqualTo(recoveryLegIndex));
                    CollectionAssert.AreEqual(journey,(Vector3[])MovementRead(recoveryEntry,"Journey"));
                    Assert.That(coordinator.QueriesConsumed(recoveryIntent),Is.EqualTo(recoveryQueries));
                    yield return null;
                    AssertErrors();AssertBusiness(true);Assert.That(owner.Snapshot.Epoch,Is.EqualTo(epoch));Assert.That(owner.Snapshot.SessionId,Is.EqualTo(session));
                    Assert.That(adapter.Revision,Is.EqualTo(revision));Assert.That(live.Contains(id),Is.False);
                    Assert.That(MovementRead(recoveryEntry,"JourneyPermit"),Is.SameAs(recoverySource));
                    Assert.That(MovementRead(recoveryEntry,"Retired"),Is.EqualTo(false));
                    Assert.That(MovementRead(recoveryEntry,"WaitRemaining"),Is.EqualTo(wait));
                    Assert.That(coordinator.QueriesConsumed(recoveryIntent),Is.EqualTo(recoveryQueries));
                    suspensionFailedFlag=(bool)MovementRead(paid,"Failed");
                    Record("ONE_UNAVAILABLE_FRAME sameSource=True sameIntent=True sameClaim=True failedFlag="+suspensionFailedFlag);
                    Assert.That(adapter.RebuildAndValidate().CanResume,Is.True,"FIXTURE: original same-revision rebuild must succeed");time.SetNormal();
                    Assert.That(adapter.Revision,Is.EqualTo(revision));Assert.That(owner.Snapshot.Epoch,Is.EqualTo(epoch));
                    for(var frame=0;frame<6000 && !suspensionRestored && errors.Count==0 &&
                        (CustomerVisitState)MovementRead(visit,"State")!=CustomerVisitState.Blocked;frame++)yield return null;
                    AssertErrors();
                    if(!repeatedResumeAttempted)
                    {
                        // The defective source may block before it can issue a restored Native request.
                        // 仍执行一次真实 Owner Resume；缺失恢复 Native 是产品结果，不改装成 fixture 失败。
                        repeatedResumeAttempted=true;ownerResumeCalls++;owner.ResumeBlockedFlow();
                        Record("POST_REBUILD_OWNER_RESUME calls="+ownerResumeCalls+" restoredNative="+suspensionRestored+
                            " failedFlag="+MovementRead(paid,"Failed")+" state="+MovementRead(visit,"State"));
                    }
                }
                for(var frame=0;frame<12000 && !removalSeen && errors.Count==0;frame++)
                {
                    yield return null;
                    if(Visits().Contains(id))AssertBusiness(!canonicalArrival);
                    if(owner.Snapshot.Orders.TryGetValue(orderKey,out var order) && order.State==OrderState.Completed && !completedSeen)
                    {completedSeen=true;orderCompletedObservations++;}
                }
                AssertErrors();
                FlushRecords();
                TestContext.WriteLine("PAID_RECOVERY_RESULT originalRequest="+originalRequest+" originalResult=["+originalCallback+"] ownerResumeCalls="+ownerResumeCalls+
                    " newRequests="+(acceptedRequests.Count-failedRequests)+" nativePickupArrived="+nativePickupArrival+" canonicalArrived="+canonicalArrival+
                    " actualExit="+realExit+" removal="+removalSeen+" finalState="+(Visits().Contains(id)?MovementRead(visit,"State").ToString():"Removed")+
                    " earlierFifoRemoval="+earlierRemoved+" suspensionFailedFlag="+suspensionFailedFlag+" suspensionRestored="+suspensionRestored);
                Assert.That(productErrors,Is.Empty,"PRODUCT: repeated Owner Resume must preserve every bound recovery postcondition");
                if(suspendRecovery)Assert.That(!suspensionFailedFlag && suspensionRestored && repeatedResumePreserved,Is.True,
                    "PRODUCT: one ordinary same-revision unavailable frame must not poison or cancel the already-authorized paid recovery attempt");
                Assert.That(nativePickupArrival && canonicalArrival && realExit && removalSeen,Is.True,
                    "PRODUCT: repaired real Native failure plus explicit Owner Resume must continue the same paid order through actual Pickup Arrived and Exit");
                Assert.That(ownerResumeCalls,Is.EqualTo(2));Assert.That(repeatedResumePreserved,Is.True,"Repeated Owner Resume must preserve the already bound recovery attempt");
                Assert.That(originalCallbacks,Is.EqualTo(1),"The old request cannot arrive or callback again");
                Assert.That(orderCompletedObservations,Is.EqualTo(1));
                Assert.That(owner.Snapshot.Orders.Keys,Is.EquivalentTo(earlierLeaves?new[]{earlierOrder,orderKey}:new[]{orderKey}));
                Assert.That(owner.Snapshot.Orders[orderKey].State,Is.EqualTo(OrderState.Completed));Assert.That(Visits().Contains(id),Is.False);
                Assert.That(Vector3.Distance(exitPose,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position),Is.LessThanOrEqualTo(.081f));
                Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,total)).State,Is.EqualTo(ReservationState.Released));
                Assert.That(transfers,Is.EqualTo(1));Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
            }
            finally
            {
                FlushRecords();
                corridorHook.SetValue(customers,previousCorridor);pickupHook.SetValue(customers,previousPickup);customers.AutoSpawn=false;
                if(blocker!=null){world.Unregister(blocker);blocker.gameObject.SetActive(false);UnityEngine.Object.Destroy(blocker.gameObject);}
            }
        }

        private sealed class StrictLegWitness
        {
            internal string ActorId,Phase;
            internal NavigationActor Actor;
            internal object Leg,LegActor,BodyLife;
            internal bool Paid,Order,Prepared,Registered,Bound;
            internal long NativeRequest;
            internal int ConsumerGeneration,LegGeneration,TaskGeneration,RunnerTaskGeneration;
            internal TrafficNativeStamp Stamp;
            internal TrafficDecision Source;
            internal TrafficIntentKey Intent;
        }

        private IEnumerator NativeIssueHasStrictLeg(bool employee)
        {
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var customerHook=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var employeeHook=typeof(EmployeeFlowController).GetField("MovementObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var previousCustomer=customerHook.GetValue(customers);var previousEmployee=employeeHook.GetValue(employees);
            StrictLegWitness witness=null;var errors=new List<string>();var arrived=false;var callbackSeen=false;string callback="";
            long RequestId(string text)
            {
                var field=(text??"").Split(' ').FirstOrDefault(p=>p.StartsWith("request=",StringComparison.Ordinal));
                return field!=null && long.TryParse(field.Substring(8),out var id)?id:0;
            }
            void Observe(string actorId,string operation,string details)
            {
                try
                {
                    if(witness!=null)
                    {
                        if(actorId==witness.ActorId && operation=="callback" && RequestId(details)==witness.NativeRequest)
                        { callbackSeen=true;callback=details;arrived=details.Contains("status=Arrived"); }
                        return;
                    }
                    if(operation!="submitted" || !details.Contains("accepted=True"))return;
                    object runner=null;object consumerLeg=null;string phase;int generation;var taskGeneration=0;var runnerTaskGeneration=0;
                    if(employee)
                    {
                        var row=((IEnumerable)MovementRead(employees,"employees")).Cast<object>()
                            .SingleOrDefault(e=>(string)MovementRead(e,"Id")==actorId);
                        if(row==null)return;
                        runner=MovementRead(row,"Runner");
                        if(((EmployeeTaskRunner)runner).Snapshot.State!=EmployeeState.CarryingToPickUp)return;
                        consumerLeg=MovementRead(runner,"moveLeg");if(consumerLeg==null)return;
                        phase="Prepared delivery";generation=(int)MovementRead(consumerLeg,"NativeLegGeneration");
                        taskGeneration=(int)MovementRead(consumerLeg,"Generation");runnerTaskGeneration=(int)MovementRead(runner,"generation");
                    }
                    else
                    {
                        var visits=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");
                        if(!visits.Contains(actorId))return;
                        var visit=visits[actorId];var state=(CustomerVisitState)MovementRead(visit,"State");
                        if(state!=CustomerVisitState.MovingToPickup && state!=CustomerVisitState.PickupAdvancing)return;
                        phase=state.ToString();generation=(int)MovementRead(visit,"Generation");
                    }
                    var live=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor");
                    if(!live.Contains(actorId))return; // This fixture proves the real returned-ID/live-request branch.
                    var native=(long)MovementRead(live[actorId],"Id");if(native<=0 || native!=RequestId(details))return;
                    var keys=CafeLoopRuntimeProof.Field<Dictionary<string,TrafficIntentKey>>(customers,"trafficActorKeys");
                    if(!keys.TryGetValue(actorId,out var intent))throw new InvalidOperationException("Issued actor has no traffic phase key");
                    var entries=CafeLoopRuntimeProof.Field<IDictionary>(coordinator,"entries");var entry=entries[intent];
                    var body=world.RegisteredActors.Single(a=>a.ActorId==actorId);
                    var leg=MovementRead(entry,"Leg");var snapshot=(TrafficWorldSnapshot)MovementRead(coordinator,"snapshot");
                    var fact=snapshot.Bodies.SingleOrDefault(b=>b.Owner.ActorId==actorId);
                    var order=owner.Snapshot.Orders.Values.SingleOrDefault();
                    witness=new StrictLegWitness { ActorId=actorId,Phase=phase,Actor=body,Leg=leg,Intent=intent,
                        NativeRequest=native,ConsumerGeneration=generation,TaskGeneration=taskGeneration,RunnerTaskGeneration=runnerTaskGeneration,
                        Registered=body.isActiveAndEnabled && world.RegisteredActors.Contains(body),
                        Paid=owner.Snapshot.PaidVisitIds.Count==1,Order=order!=null,
                        Prepared=employee && ((CafeWorkCheckpoint)MovementRead(runner,"checkpoint")).Stage==CafeWorkStage.Prepared,
                        Source=(TrafficDecision)MovementRead(entry,"JourneyPermit"),BodyLife=fact?.ActorLife,
                        LegActor=leg==null?null:MovementRead(leg,"ActorLife"),
                        LegGeneration=leg==null?0:(int)MovementRead(leg,"Generation"),
                        Bound=leg!=null && (bool)MovementRead(leg,"Bound"),
                        Stamp=leg==null?default:(TrafficNativeStamp)MovementRead(leg,"Stamp") };
                }
                catch(Exception error) { errors.Add(error.GetType().Name+": "+error.Message); }
            }
            try
            {
                customers.AutoSpawn=false;
                if(employee)employeeHook.SetValue(employees,(Action<string,string,string>)Observe);
                else customerHook.SetValue(customers,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)
                    ((operation,id,other,target,path,obstacles,result)=>
                    { if(operation=="segment.submitted")Observe(id,"submitted",result);else if(operation=="segment.callback-result")Observe(id,"callback",result); }));
                var admitted=false;
                // No Until/ObserveDeliveryStart: this witness reads original facts and makes no extra Native query.
                for(var frame=0;frame<6000 && witness==null && errors.Count==0;frame++)
                {
                    if(!admitted)admitted=customers.TrySpawn();
                    Assert.That(customers.Snapshot.All(v=>v.State!=CustomerVisitState.Blocked),Is.True,"Unexpected customer Blocked before real Native issue");
                    Assert.That(employees.Snapshot.All(e=>e.State!=EmployeeState.Blocked),Is.True,"Unexpected employee Blocked before real Native issue");
                    yield return null;
                }
                Assert.That(errors,Is.Empty,"Read-only original-boundary observer must not fail or change production control flow");
                Assert.That(admitted,Is.True,"FIXTURE: real formal admission required");
                Assert.That(witness,Is.Not.Null,"FIXTURE: require a real live Native request at the original submitted boundary");
                TestContext.WriteLine("STRICT_LEG_NATIVE employee="+employee+" actor="+witness.ActorId+" phase="+witness.Phase+
                    " nativeRequest="+witness.NativeRequest+" consumerGeneration="+witness.ConsumerGeneration+
                    " paid="+witness.Paid+" order="+witness.Order+" prepared="+witness.Prepared+" registered="+witness.Registered+
                    " intent="+witness.Intent.IntentId+" sourceAcquired="+witness.Source?.AcquiredSequence+
                    " sourceProofs="+witness.Source?.ProofRoutes.Count+" strictLeg="+(witness.Leg!=null)+
                    " actorLifeMatches="+ReferenceEquals(witness.LegActor,witness.Actor)+" bodyLifeMatches="+ReferenceEquals(witness.BodyLife,witness.Actor)+
                    " bound="+witness.Bound+" boundRequest="+witness.Stamp.RequestId+" legGeneration="+witness.LegGeneration);
                Assert.That(witness.Paid,Is.True,"FIXTURE: real paid visit precedes this Native issue");
                Assert.That(witness.Order,Is.True,"FIXTURE: real order precedes this Native issue");
                if(employee)Assert.That(witness.Prepared,Is.True,"FIXTURE: real completed preparation checkpoint precedes delivery Native issue");
                if(employee) { Assert.That(witness.TaskGeneration,Is.GreaterThan(0));Assert.That(witness.TaskGeneration,Is.EqualTo(witness.RunnerTaskGeneration),"Business task identity remains independent of Native leg generation"); }
                Assert.That(witness.Registered,Is.True,"FIXTURE: issuing actor is the actual registered enabled body");
                Assert.That(witness.NativeRequest,Is.GreaterThan(0));Assert.That(witness.ConsumerGeneration,Is.GreaterThan(0));
                Assert.That(witness.Source,Is.Not.Null,"FIXTURE: real committed retained source proof required");
                Assert.That(witness.Source.AcquiredSequence,Is.GreaterThan(0));Assert.That(witness.Source.ProofRoutes.Count,Is.GreaterThan(0));
                Assert.That(witness.Leg,Is.Not.Null,"PRODUCT: actual Native issue must consume a strict current leg, not only the full journey grant");
                Assert.That(witness.LegActor,Is.SameAs(witness.Actor));Assert.That(witness.BodyLife,Is.SameAs(witness.Actor));
                Assert.That(witness.LegGeneration,Is.EqualTo(witness.ConsumerGeneration));
                Assert.That(witness.Bound,Is.True,"PRODUCT: returned real request ID must already be bound before publication");
                Assert.That(witness.Stamp.RequestId,Is.EqualTo(witness.NativeRequest));
                Assert.That(witness.Stamp.LegGeneration,Is.EqualTo(witness.ConsumerGeneration));
                for(var frame=0;frame<6000 && !callbackSeen;frame++)yield return null;
                Assert.That(errors,Is.Empty);Assert.That(callbackSeen,Is.True,"Original Native callback required for the same real request");
                Assert.That(arrived,Is.True,"Same real Native request must actually arrive: "+callback);
            }
            finally { customerHook.SetValue(customers,previousCustomer);employeeHook.SetValue(employees,previousEmployee);customers.AutoSpawn=false; }
        }

        [UnityTest] public IEnumerator CustomerPaidPickupSameRevisionSuspensionResumesBoundLeg() => RealTrafficSuspension(false);
        [UnityTest] public IEnumerator EmployeePreparedDeliverySameRevisionSuspensionResumesBoundLeg() => RealTrafficSuspension(true);

        private IEnumerator RealTrafficSuspension(bool employee)
        {
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var prepareHook=typeof(ServiceTrafficCoordinator).GetField("PrepareLegRejectionObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var previousPrepare=prepareHook.GetValue(coordinator);
            var capturePrepare=System.Environment.GetCommandLineArgs().Contains("-trafficPrepareLegSnapshot");
            var prepareRecords=new List<string>();var prepareOverflow=0;
            var customerHook=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var employeeHook=typeof(EmployeeFlowController).GetField("MovementObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var previousCustomer=customerHook.GetValue(customers);var previousEmployee=employeeHook.GetValue(employees);
            var errors=new List<string>();var callbacks=new Dictionary<long,List<string>>();
            StrictLegWitness first=null,resumed=null;object row=null,entry=null;EmployeeTaskRunner runner=null;
            object resumedCup=null;long resumedHandle=0;
            var released=false;
            long RequestId(string text)
            {
                var field=(text??"").Split(' ').FirstOrDefault(p=>p.StartsWith("request=",StringComparison.Ordinal));
                return field!=null && long.TryParse(field.Substring(8),out var id)?id:0;
            }
            object Entry(TrafficIntentKey key)=>CafeLoopRuntimeProof.Field<IDictionary>(coordinator,"entries")[key];
            void Observe(string actorId,string operation,string details)
            {
                try
                {
                    var requestId=RequestId(details);
                    if(first!=null && actorId==first.ActorId && operation=="callback")
                    {
                        if(!callbacks.TryGetValue(requestId,out var values))callbacks.Add(requestId,values=new List<string>());
                        values.Add(details);return;
                    }
                    if(operation!="submitted" || !details.Contains("accepted=True") ||
                        first!=null && (!released || actorId!=first.ActorId || requestId==first.NativeRequest || resumed!=null))return;
                    var live=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor");
                    if(!live.Contains(actorId) || (long)MovementRead(live[actorId],"Id")!=requestId)return;
                    int generation;var prepared=false;var taskGeneration=0;var runnerGeneration=0;
                    if(employee)
                    {
                        var candidate=((IEnumerable)MovementRead(employees,"employees")).Cast<object>().SingleOrDefault(e=>(string)MovementRead(e,"Id")==actorId);
                        if(candidate==null)return;
                        var candidateRunner=(EmployeeTaskRunner)MovementRead(candidate,"Runner");
                        if(candidateRunner.Snapshot.State!=EmployeeState.CarryingToPickUp)return;
                        var move=MovementRead(candidateRunner,"moveLeg");if(move==null)return;
                        generation=(int)MovementRead(move,"NativeLegGeneration");taskGeneration=(int)MovementRead(move,"Generation");
                        runnerGeneration=(int)MovementRead(candidateRunner,"generation");
                        prepared=((CafeWorkCheckpoint)MovementRead(candidateRunner,"checkpoint")).Stage==CafeWorkStage.Prepared;
                        if(first==null){row=candidate;runner=candidateRunner;}
                    }
                    else
                    {
                        var visits=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");if(!visits.Contains(actorId))return;
                        var visit=visits[actorId];var state=(CustomerVisitState)MovementRead(visit,"State");
                        if(state!=CustomerVisitState.MovingToPickup && state!=CustomerVisitState.PickupAdvancing)return;
                        generation=(int)MovementRead(visit,"Generation");
                    }
                    var keys=CafeLoopRuntimeProof.Field<Dictionary<string,TrafficIntentKey>>(customers,"trafficActorKeys");
                    if(!keys.TryGetValue(actorId,out var intent))throw new InvalidOperationException("Live Native actor has no traffic phase");
                    var current=Entry(intent);var leg=MovementRead(current,"Leg");var actor=world.RegisteredActors.Single(a=>a.ActorId==actorId);
                    var body=((TrafficWorldSnapshot)MovementRead(coordinator,"snapshot")).Bodies.Single(b=>b.Owner.ActorId==actorId);
                    var witness=new StrictLegWitness{ActorId=actorId,Actor=actor,Intent=intent,Leg=leg,NativeRequest=requestId,
                        ConsumerGeneration=generation,TaskGeneration=taskGeneration,RunnerTaskGeneration=runnerGeneration,
                        Source=(TrafficDecision)MovementRead(current,"JourneyPermit"),Paid=owner.Snapshot.PaidVisitIds.Count==1,
                        Order=owner.Snapshot.Orders.Count==1,Prepared=prepared,Registered=actor.isActiveAndEnabled,
                        BodyLife=body.ActorLife,LegActor=leg==null?null:MovementRead(leg,"ActorLife"),
                        Bound=leg!=null && (bool)MovementRead(leg,"Bound"),
                        LegGeneration=leg==null?0:(int)MovementRead(leg,"Generation"),
                        Stamp=leg==null?default:(TrafficNativeStamp)MovementRead(leg,"Stamp")};
                    if(first==null){first=witness;entry=current;}
                    else
                    {
                        resumed=witness;
                        if(employee){resumedHandle=runner.Snapshot.HandleId;resumedCup=MovementRead(MovementRead(row,"Visual"),"heldCup");}
                    }
                }
                catch(Exception error){errors.Add(error.GetType().Name+": "+error.Message);}
            }
            void AssertWitness(StrictLegWitness witness)
            {
                Assert.That(witness,Is.Not.Null,"FIXTURE: actual paid/Prepared live Native request required");
                Assert.That(witness.Paid && witness.Order && witness.Registered,Is.True);
                if(employee)
                {
                    Assert.That(witness.Prepared,Is.True);Assert.That(witness.TaskGeneration,Is.GreaterThan(0));
                    Assert.That(witness.TaskGeneration,Is.EqualTo(witness.RunnerTaskGeneration));
                }
                Assert.That(witness.Source,Is.Not.Null);Assert.That(witness.Source.AcquiredSequence,Is.GreaterThan(0));
                Assert.That(witness.Source.ProofRoutes.Count,Is.GreaterThan(0));Assert.That(witness.Leg,Is.Not.Null);
                Assert.That(witness.BodyLife,Is.SameAs(witness.Actor));Assert.That(witness.LegActor,Is.SameAs(witness.Actor));
                Assert.That(witness.Bound,Is.True);Assert.That(witness.NativeRequest,Is.GreaterThan(0));
                Assert.That(witness.LegGeneration,Is.EqualTo(witness.ConsumerGeneration));
                Assert.That(witness.Stamp.RequestId,Is.EqualTo(witness.NativeRequest));
                Assert.That(witness.Stamp.LegGeneration,Is.EqualTo(witness.ConsumerGeneration));
            }
            try
            {
                customers.AutoSpawn=false;
                if(capturePrepare)prepareHook.SetValue(coordinator,(Action<string>)(record=>
                {if(prepareRecords.Count<32)prepareRecords.Add(record);else prepareOverflow++;}));
                if(employee)employeeHook.SetValue(employees,(Action<string,string,string>)Observe);
                else customerHook.SetValue(customers,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)
                    ((operation,id,other,target,path,obstacles,result)=>
                    {if(operation=="segment.submitted")Observe(id,"submitted",result);else if(operation=="segment.callback-result")Observe(id,"callback",result);}));
                var admitted=false;
                // Original observations only: no Until helper's geometry queries, Refresh, or manual Step.
                for(var frame=0;frame<6000 && first==null && errors.Count==0;frame++)
                {
                    if(!admitted)admitted=customers.TrySpawn();
                    Assert.That(customers.Snapshot.All(v=>v.State!=CustomerVisitState.Blocked),Is.True,"Customer blocked before suspension premise");
                    Assert.That(employees.Snapshot.All(e=>e.State!=EmployeeState.Blocked),Is.True,"Employee blocked before suspension premise");
                    yield return null;
                }
                Assert.That(errors,Is.Empty);Assert.That(admitted,Is.True);AssertWitness(first);
                var native=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor");
                Assert.That(native.Contains(first.ActorId),Is.True,"FIXTURE: original request must still be live outside its issue callback stack");
                Assert.That((long)MovementRead(native[first.ActorId],"Id"),Is.EqualTo(first.NativeRequest));
                var revision=adapter.Revision;var epoch=owner.Snapshot.Epoch;var session=owner.Snapshot.SessionId;
                var visitId=owner.Snapshot.PaidVisitIds.Single();var orders=owner.Snapshot.Orders.Keys.ToArray();
                var orderStates=owner.Snapshot.Orders.Values.Select(o=>o.State).ToArray();
                var tokens=customers.Capacity.GetReservations().Where(r=>r.OwnerId==visitId).Select(r=>r.Token).ToArray();
                var tokenStates=customers.Capacity.GetReservations().Where(r=>r.OwnerId==visitId).Select(r=>r.State).ToArray();
                var firstFrame=MovementRead(entry,"FirstFrame");var sequence=MovementRead(entry,"RequestSequence");
                var acquired=MovementRead(entry,"AcquiredSequence");var queries=coordinator.QueriesConsumed(first.Intent);
                var index=MovementRead(first.Leg,"Index");var journey=((Vector3[])MovementRead(entry,"Journey")).ToArray();
                var handle=employee?runner.Snapshot.HandleId:0;
                var visual=employee?MovementRead(row,"Visual"):null;
                var cup=employee?(GameObject)MovementRead(visual,"heldCup"):null;
                if(employee){Assert.That(handle,Is.GreaterThan(0));Assert.That(cup,Is.Not.Null);}
                void AssertHeldFacts()
                {
                    Assert.That(owner.Snapshot.Epoch,Is.EqualTo(epoch));Assert.That(owner.Snapshot.SessionId,Is.EqualTo(session));
                    Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic"),Is.SameAs(coordinator));
                    CollectionAssert.AreEqual(new[]{visitId},owner.Snapshot.PaidVisitIds);
                    CollectionAssert.AreEqual(orders,owner.Snapshot.Orders.Keys);
                    CollectionAssert.AreEqual(orderStates,owner.Snapshot.Orders.Values.Select(o=>o.State));
                    CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==visitId).Select(r=>r.Token));
                    CollectionAssert.AreEqual(tokenStates,customers.Capacity.GetReservations().Where(r=>r.OwnerId==visitId).Select(r=>r.State));
                    Assert.That(world.RegisteredActors.Contains(first.Actor),Is.True);
                    Assert.That(MovementRead(entry,"JourneyPermit"),Is.SameAs(first.Source));
                    Assert.That(MovementRead(entry,"Retired"),Is.False);
                    Assert.That(MovementRead(entry,"FirstFrame"),Is.EqualTo(firstFrame));
                    Assert.That(MovementRead(entry,"RequestSequence"),Is.EqualTo(sequence));
                    Assert.That(MovementRead(entry,"AcquiredSequence"),Is.EqualTo(acquired));
                    Assert.That(coordinator.QueriesConsumed(first.Intent),Is.EqualTo(queries));
                    if(employee)
                    {
                        Assert.That(runner.Snapshot.HandleId,Is.EqualTo(handle));
                        Assert.That(((CafeWorkCheckpoint)MovementRead(runner,"checkpoint")).Stage,Is.EqualTo(CafeWorkStage.Prepared));
                        Assert.That(MovementRead(visual,"heldCup"),Is.SameAs(cup));
                    }
                }
                TestContext.WriteLine("STRICT_SUSPEND_PRE actor="+first.ActorId+" employee="+employee+" request="+first.NativeRequest+
                    " nativeGen="+first.ConsumerGeneration+" acquired="+acquired+" firstFrame="+firstFrame+" queries="+queries+" revision="+revision);
                released=true;adapter.ReleaseOwnedNavigation();
                Assert.That(world.LayoutAvailable,Is.False);Assert.That(adapter.Revision,Is.EqualTo(revision));
                Assert.That(callbacks.ContainsKey(first.NativeRequest),Is.True,"Original Native cancellation callback must be observed");
                Assert.That(callbacks[first.NativeRequest].Count,Is.EqualTo(1));
                Assert.That(callbacks[first.NativeRequest][0],Does.Contain("status=LayoutChanged"));
                Assert.That(callbacks[first.NativeRequest][0],Does.Contain("reason=LayoutUnavailable"));
                Assert.That(native.Contains(first.ActorId),Is.False,"Cancelled real request must be removed");
                Assert.That(MovementRead(first.Leg,"Suspended"),Is.True);
                Assert.That(MovementRead(first.Leg,"Completed"),Is.False);Assert.That(MovementRead(first.Leg,"Index"),Is.EqualTo(index));
                Assert.That((Vector3[])MovementRead(entry,"CurrentPath"),Is.Empty);
                CollectionAssert.AreEqual(journey,(Vector3[])MovementRead(entry,"Journey"));
                Assert.That(MovementRead(entry,"Suspensions"),Is.EqualTo(1));AssertHeldFacts();
                yield return null; // Exercise the normal Loop's unavailable/hold path before restoring navigation.
                AssertHeldFacts();Assert.That(resumed,Is.Null);Assert.That(native.Contains(first.ActorId),Is.False);
                Assert.That(adapter.RebuildAndValidate().CanResume,Is.True);time.SetNormal();
                Assert.That(adapter.Revision,Is.EqualTo(revision));
                for(var frame=0;frame<6000 && resumed==null && errors.Count==0;frame++)yield return null;
                Assert.That(errors,Is.Empty);AssertWitness(resumed);
                Assert.That(resumed.Intent,Is.EqualTo(first.Intent));Assert.That(resumed.Source,Is.SameAs(first.Source));
                Assert.That(resumed.Actor,Is.SameAs(first.Actor));Assert.That(resumed.NativeRequest,Is.Not.EqualTo(first.NativeRequest));
                Assert.That(resumed.ConsumerGeneration,Is.EqualTo(first.ConsumerGeneration+1));
                Assert.That(MovementRead(resumed.Leg,"Index"),Is.EqualTo(index));
                Assert.That(MovementRead(entry,"FirstFrame"),Is.EqualTo(firstFrame));
                Assert.That(MovementRead(entry,"RequestSequence"),Is.EqualTo(sequence));
                Assert.That(MovementRead(entry,"AcquiredSequence"),Is.EqualTo(acquired));
                Assert.That(coordinator.QueriesConsumed(first.Intent),Is.GreaterThan(queries));
                Assert.That(coordinator.QueriesConsumed(first.Intent),Is.LessThanOrEqualTo(16384));
                Assert.That(coordinator.QueriesThisFrame,Is.LessThanOrEqualTo(128));
                Assert.That(MovementRead(entry,"Suspensions"),Is.EqualTo(1));
                if(employee)
                {
                    Assert.That(resumedHandle,Is.EqualTo(handle));Assert.That(resumedCup,Is.SameAs(cup));
                    Assert.That(MovementRead(runner,"navigationResumeUsed"),Is.True);
                }
                for(var frame=0;frame<6000 && !callbacks.ContainsKey(resumed.NativeRequest) && errors.Count==0;frame++)yield return null;
                Assert.That(errors,Is.Empty);Assert.That(callbacks.ContainsKey(resumed.NativeRequest),Is.True);
                Assert.That(callbacks[resumed.NativeRequest].Count,Is.EqualTo(1));
                Assert.That(callbacks[resumed.NativeRequest][0],Does.Contain("status=Arrived"),"The resumed real Native leg must arrive");
                Assert.That(MovementRead(resumed.Leg,"Completed"),Is.True);
                Assert.That(callbacks[first.NativeRequest].Count,Is.EqualTo(1),"Old request never reappears or arrives after cancellation");
                Assert.That(owner.Snapshot.Epoch,Is.EqualTo(epoch));
                CollectionAssert.AreEqual(new[]{visitId},owner.Snapshot.PaidVisitIds);
                CollectionAssert.AreEqual(orders,owner.Snapshot.Orders.Keys);
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==visitId).Select(r=>r.Token));
                TestContext.WriteLine("STRICT_SUSPEND_ARRIVED actor="+first.ActorId+" old="+first.NativeRequest+" resumed="+resumed.NativeRequest+
                    " nativeGen="+resumed.ConsumerGeneration+" queries="+coordinator.QueriesConsumed(first.Intent)+" revision="+adapter.Revision);
            }
            finally
            {
                prepareHook.SetValue(coordinator,previousPrepare);
                if(capturePrepare)
                {
                    foreach(var record in prepareRecords)TestContext.WriteLine(record);
                    TestContext.WriteLine("PREPARE_LEG_CAPTURE records="+prepareRecords.Count+" overflow="+prepareOverflow);
                }
                customerHook.SetValue(customers,previousCustomer);employeeHook.SetValue(employees,previousEmployee);customers.AutoSpawn=false;
            }
        }

        [UnityTest] public IEnumerator AdmissionBirthBoundaryDisablePublishesNoCanonicalFacts() => AdmissionBirthReentry("pre-disable");
        [UnityTest] public IEnumerator AdmissionBirthBoundaryQueueChangeRejectsStalePermission() => AdmissionBirthReentry("pre-version");
        [UnityTest] public IEnumerator AdmissionPublishedRemovalCannotIssueNativeMovement() => AdmissionBirthReentry("post-remove");
        [UnityTest] public IEnumerator AdmissionPublishedDisableRetainsBodyWithoutIssuingMovement() => AdmissionBirthReentry("post-disable");
        [UnityTest] public IEnumerator AdmissionPublishedQueueVersionChangeRevalidatesSamePhaseAndActuallyArrives() => CounterPublicationVersionChange(false);
        [UnityTest] public IEnumerator CounterAdvancePublishedQueueVersionChangeRevalidatesSamePhaseAndActuallyArrives() => CounterPublicationVersionChange(true);

        private IEnumerator CounterPublicationVersionChange(bool advance)
        {
            var queue=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue");
            var originalSlots=((Vector3[])MovementRead(queue,"slots")).ToArray();
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var previous=hook.GetValue(customers);var witnessed=false;var immediateIssue=false;var actualArrival=false;
            var callbackFrame=-1;string id=null;NavigationActor body=null;QueueAssignment assignment=default;TrafficIntentKey intent=default;
            CapacityToken[] tokens=null;long nextVisit=0,sequence=0,acquired=0;var queries=0;var seen=new HashSet<string>();
            try
            {
                hook.SetValue(customers,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)((operation,actorId,other,target,path,obstacles,result)=>
                {
                    if(witnessed)
                    {
                        if(actorId==id && operation=="segment.issue" && Time.frameCount==callbackFrame)immediateIssue=true;
                        if(actorId==id && operation=="segment.callback" && result=="Arrived")actualArrival=true;
                        return;
                    }
                    if(operation!=(advance?"owned.retire":"admission.published") || actorId==null)return;
                    var keys=CafeLoopRuntimeProof.Field<Dictionary<string,TrafficIntentKey>>(customers,"trafficActorKeys");
                    if(!keys.TryGetValue(actorId,out var key) || key.IntentId!=(advance?"counter-advance":"counter-admission") ||
                        !coordinator.HasActiveGrant(key))return;
                    var owned=queue.Snapshot.SingleOrDefault(a=>a.VisitId==actorId);
                    if(owned.VisitId==null || owned.Arrived || queue.Snapshot.Count>=originalSlots.Length)return;
                    var visit=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[actorId];
                    if((long)MovementRead(visit,"RequestId")!=0)return;
                    var decision=coordinator.GetDecision(key);if(decision.AcquiredSequence<=0)return;
                    witnessed=true;id=actorId;intent=key;assignment=owned;callbackFrame=Time.frameCount;
                    body=world.RegisteredActors.Single(a=>a.ActorId==id);
                    tokens=customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray();
                    nextVisit=(long)MovementRead(customers,"nextVisit");sequence=decision.RequestSequence;acquired=decision.AcquiredSequence;queries=coordinator.QueriesConsumed(key);
                    customers.AutoSpawn=false;
                    // Only unallocated tail metadata changes; this owner's exact assignment remains canonical.
                    // 不改身体/已有assignment/路线/请求；回调只让旧全局Version失效。
                    var changed=((Vector3[])MovementRead(queue,"slots")).ToArray();changed[changed.Length-1]+=Vector3.forward*.05f;
                    queue.UpdateSlots(changed);
                }));
                customers.AutoSpawn=false;
                if(advance)
                {
                    // Approved fallback after the natural two-visitor run never produced an advance.
                    // 已真实到站的同一顾客，换合法候选目标后仍由正常Step/Native执行前移。
                    yield return AwaitTrafficAdmission();
                    var existing=customers.Snapshot.Single().VisitId;
                    yield return Until(()=>queue.Snapshot.Any(a=>a.VisitId==existing && a.Arrived));
                    var actualBody=world.RegisteredActors.Single(a=>a.ActorId==existing);
                    Assert.That(originalSlots.Length,Is.GreaterThan(2),"FIXTURE: need a distinct next target plus an unused tail");
                    var target=originalSlots[1];var native=adapter.GetCompletePath(actualBody.transform.position,target);
                    var others=world.RegisteredActors.Where(a=>a!=actualBody).ToArray();
                    var clearance=actualBody.Settings.AgentRadius+others.Max(a=>a.Settings.AgentRadius)+actualBody.Settings.CollisionSkin+actualBody.Settings.Epsilon;
                    Assert.That(adapter.IsPointWalkable(target),Is.True);Assert.That(native,Is.Not.Null,"FIXTURE: controlled advance uses real complete Native geometry");
                    Assert.That(CustomerRoutePlanner.IsClear(native,others.Select(a=>a.transform.position).ToArray(),clearance),Is.True,"FIXTURE: actual other bodies clear the new leg");
                    var shifted=originalSlots.ToArray();shifted[0]=target;shifted[1]=originalSlots[0];
                    queue.UpdateSlots(shifted);
                    TestContext.WriteLine("CONTROLLED_COUNTER_ADVANCE notNaturalCohort=True actor="+existing+" actualArrivedPose="+actualBody.transform.position.ToString("F9")+
                        " candidate="+target.ToString("F9")+" path=["+string.Join(";",native.Select(p=>p.ToString("F9")))+"]");
                }
                for(var frame=0;frame<6000 && !witnessed;frame++)
                {
                    if(!advance)customers.TrySpawn();
                    foreach(var live in customers.Snapshot)seen.Add(live.VisitId);
                    if(seen.Count>=2)customers.AutoSpawn=false;
                    yield return null;
                }
                customers.AutoSpawn=false;
                TestContext.WriteLine("COUNTER_PUBLICATION_VERSION advance="+advance+" witnessed="+witnessed+" seen="+seen.Count+
                    " id="+id+" phase="+intent.IntentId+" requestSequence="+sequence+" acquired="+acquired+" queries="+queries+" immediateIssue="+immediateIssue);
                Assert.That(witnessed,Is.True,"FIXTURE: real joint Counter publication with an unused tail must reach the original callback");
                Assert.That(immediateIssue,Is.False,"PRODUCT: stale publication must not immediately issue Native movement");
                Assert.That(world.RegisteredActors.Single(a=>a.ActorId==id),Is.SameAs(body));
                Assert.That((long)MovementRead(customers,"nextVisit"),Is.EqualTo(nextVisit));
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
                Assert.That(tokens.Length,Is.EqualTo(3));
                var current=queue.Snapshot.Single(a=>a.VisitId==id);
                Assert.That(current.Generation,Is.EqualTo(assignment.Generation));Assert.That(current.Position,Is.EqualTo(assignment.Position));
                Assert.That(coordinator.GetDecision(intent).Kind,Is.Not.EqualTo(TrafficDecisionKind.Cancelled),
                    "PRODUCT: unchanged owned assignment needs bounded normal revalidation, not an orphaned cancelled phase");
                bool Arrived()=>actualArrival && queue.Snapshot.Any(a=>a.VisitId==id && a.Generation==assignment.Generation && a.Arrived) &&
                    body!=null && Vector3.Distance(body.transform.position,assignment.Position)<=.081f;
                for(var frame=0;frame<6000 && !Arrived();frame++)yield return null;
                Assert.That(Arrived(),Is.True,"PRODUCT: same owned phase must reach real Counter arrival without Resume or new admission");
                var final=coordinator.GetDecision(intent);
                Assert.That(final.RequestSequence,Is.EqualTo(sequence));Assert.That(final.AcquiredSequence,Is.EqualTo(acquired));
                Assert.That(coordinator.QueriesConsumed(intent),Is.GreaterThanOrEqualTo(queries),"Revalidation cannot reset the intent query budget");
                var phaseKeys=CafeLoopRuntimeProof.Field<Dictionary<string,TrafficIntentKey>>(customers,"trafficKeys").Values.Where(k=>k.ActorId==id && k.IntentId==intent.IntentId).ToArray();
                CollectionAssert.AreEqual(new[]{intent},phaseKeys,"No replacement phase may reset age or the finite budget");
            }
            finally {customers.AutoSpawn=false;hook.SetValue(customers,previous);queue.UpdateSlots(originalSlots);}
        }

        private IEnumerator AdmissionBirthReentry(string mode)
        {
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var previous=hook.GetValue(customers);var enabled=customers.enabled;
            var queue=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue");
            var originalActors=world.RegisteredActors.Select(a=>a.ActorId).ToArray();var originalNext=(long)MovementRead(customers,"nextVisit");
            var witnessed=false;string bornId=null;var nativeIssued=false;
            try
            {
                hook.SetValue(customers,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)((operation,id,other,target,path,obstacles,result)=>
                {
                    if(operation=="segment.issue" && id==bornId)nativeIssued=true;
                    if(witnessed || operation!=(mode.StartsWith("pre-",StringComparison.Ordinal)?"admission.birth-ready":"admission.published"))return;
                    witnessed=true;bornId=id;
                    // birth-ready is a controlled callback boundary, not an injected Unity OnEnable.
                    if(mode=="pre-version")queue.RestartAssignments();
                    else if(mode=="post-remove")customers.RemoveVisit(id);
                    else customers.enabled=false;
                }));
                for(var frame=0;frame<600 && !witnessed;frame++) { customers.TrySpawn();yield return null; }
                Assert.That(witnessed,Is.True,"FIXTURE: expected original birth/publication boundary must execute");
                Assert.That(nativeIssued,Is.False,"PRODUCT: invalidated publication cannot continue into Native issue");
                Assert.That(owner.Snapshot.Orders,Is.Empty);Assert.That(owner.Snapshot.PaidVisitIds,Is.Empty);
                if(mode.StartsWith("pre-",StringComparison.Ordinal))
                {
                    Assert.That(customers.Snapshot,Is.Empty);Assert.That(queue.Snapshot,Is.Empty);
                    Assert.That(customers.Capacity.GetReservations(),Is.Empty);
                    Assert.That((long)MovementRead(customers,"nextVisit"),Is.EqualTo(originalNext));
                    CollectionAssert.AreEquivalent(originalActors,world.RegisteredActors.Select(a=>a.ActorId));
                }
                else
                {
                    Assert.That((long)MovementRead(customers,"nextVisit"),Is.EqualTo(originalNext+1),"Published identities never rewind");
                    var tokens=customers.Capacity.GetReservations().Where(r=>r.OwnerId==bornId).ToArray();
                    Assert.That(tokens.Length,Is.EqualTo(3),"Published token history remains truthful");
                    var key=CafeLoopRuntimeProof.Field<Dictionary<string,TrafficIntentKey>>(customers,"trafficActorKeys");
                    var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
                    Assert.That(!key.TryGetValue(bornId,out var intent) || !coordinator.HasActiveGrant(intent),Is.True);
                    if(mode=="post-remove")
                    {
                        Assert.That(tokens.All(r=>r.State==ReservationState.Released),Is.True);
                        Assert.That(customers.Snapshot,Is.Empty);Assert.That(queue.Snapshot,Is.Empty);
                        CollectionAssert.AreEquivalent(originalActors,world.RegisteredActors.Select(a=>a.ActorId));
                    }
                    else
                    {
                        Assert.That(tokens.All(r=>r.State!=ReservationState.Released),Is.True);
                        Assert.That(customers.Snapshot.Single().VisitId,Is.EqualTo(bornId));
                        Assert.That(queue.Snapshot.Single().VisitId,Is.EqualTo(bornId));
                        Assert.That(world.RegisteredActors.Any(a=>a.ActorId==bornId && a.isActiveAndEnabled),Is.True,"Disabled controller cannot erase a remaining physical body");
                    }
                }
            }
            finally { hook.SetValue(customers,previous);customers.enabled=enabled; }
        }

        [UnityTest] public IEnumerator AdmissionDeferredCreatesNoActorVisitOrCapacityTokens()
        {
            var queue=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue");
            Assert.That(owner.CanProgress && queue.CanJoin && customers.Capacity.CanAdmit,Is.True,"FIXTURE: configured empty integrated admission");
            Assert.That(customers.Snapshot,Is.Empty);Assert.That(customers.CounterAssignments,Is.Empty);
            Assert.That(owner.Snapshot.Orders,Is.Empty);Assert.That(customers.Capacity.GetReservations(),Is.Empty);
            var spawn=CafeLoopRuntimeProof.Field<Transform>(customers,"spawn");
            Assert.That(adapter.GetCompletePath(spawn.position,queue.NextAdmissionPosition.Value),Is.Not.Null,"FIXTURE: real owned Native approach exists");
            var before=MovementAdmissionFacts();var version=queue.Version;
            var accepted=customers.TrySpawn();var after=MovementAdmissionFacts();
            TestContext.WriteLine("ADMISSION_FIRST_BATCH accepted="+accepted+" before="+before+" after="+after);
            Assert.That(accepted,Is.False,"PRODUCT: integrated admission must first enter the shared ready batch");
            Assert.That(after,Is.EqualTo(before),"PRODUCT: Deferred allocates no visit, actor, queue entry, order, payment or capacity token");
            Assert.That(queue.Version,Is.EqualTo(version),"PRODUCT: no canonical Counter write before traffic permission");
            // A passing rejection must still make real progress through ordinary frames.
            yield return Until(()=>customers.Snapshot.Count>0 || customers.TrySpawn());
            Assert.That(customers.Snapshot.Count,Is.EqualTo(1));var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>customers.CafeSnapshot.Any(v=>v.VisitId==id && v.CounterArrived));
            Assert.That(customers.Capacity.GetReservations().Count(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers),Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator CounterTailCannotCommitAcrossEarlierPendingExitSuffix()
        {
            // Controlled target projection, separate from the original natural Main regression.
            // 只改变尚未分配的下一Counter候选；实际Exit来自正常付款、领取和Native移动。
            var queue=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue");
            var originalSlots=((Vector3[])MovementRead(queue,"slots")).ToArray();
            var retained=CafeLoopRuntimeProof.Field<Vector3[]>(customers,"counterWaitSlots");var originalRetained=retained.ToArray();
            var visits=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var spawn=CafeLoopRuntimeProof.Field<Transform>(customers,"spawn");var departure=CafeLoopRuntimeProof.Field<Transform>(customers,"exit");
            var prefab=CafeLoopRuntimeProof.Field<NavigationActor[]>(customers,"prefabs").First();
            var seen=new HashSet<string>();var errors=new List<string>();
            object earlier=null;NavigationActor earlierBody=null;TrafficIntentKey earlierKey=default;
            Vector3 projected=default;Vector3[] selectedSuffix=null;var slot=-1;var projectedFixture=false;
            var actualExit=false;var unsafeCommit=false;var accepted=false;var observing=true;CafeActualExitObserver exitObserver=null;
            var pendingObserved=0;var selectedObserved=0;var samplesChecked=0;
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var priorHook=hook.GetValue(customers);var originalAdmissionDecision="NOT_EVALUATED";
            var bodyClearance=prefab.Settings.AgentRadius+world.RegisteredActors.Max(a=>a.Settings.AgentRadius)+prefab.Settings.CollisionSkin+prefab.Settings.Epsilon;
            var tailClearance=bodyClearance+prefab.Settings.ArrivalDistance;
            try
            {
                tools.ResumeCollection();customers.AutoSpawn=true;
                // Exactly two natural visitors; retain the first genuine pending selected Exit we can witness.
                for(var frame=0;frame<6000 && !projectedFixture;frame++)
                {
                    foreach(var live in customers.Snapshot)seen.Add(live.VisitId);
                    if(seen.Count>=2)customers.AutoSpawn=false;
                    if(seen.Count==2 && customers.Snapshot.Count==0)break;
                    if(seen.Count==2 && queue.CanJoin && queue.Snapshot.Count==0 && customers.Capacity.CanAdmit && owner.CanProgress &&
                        customers.Snapshot.All(v=>v.State!=CustomerVisitState.Blocked))
                    foreach(var live in customers.Snapshot.Where(v=>v.State==CustomerVisitState.Exiting))
                    {
                        var entry=visits[live.VisitId];var route=(Queue<Vector3>)MovementRead(entry,"Route");
                        if(!(bool)MovementRead(entry,"HasCollected") || !(bool)MovementRead(entry,"PendingExitSegment") ||
                            (long)MovementRead(entry,"RequestId")!=0 || route==null || route.Count==0)continue;
                        pendingObserved++;
                        var selected=(IReadOnlyList<Vector3>)MovementRead(entry,"OwnedRoutePath");
                        var intent=(TrafficIntentKey?)MovementRead(entry,"TrafficJourney");
                        if(Environment.GetCommandLineArgs().Contains("-p15OldTestTrace") && pendingObserved<=4)
                        {
                            var move=MovementRead(entry,"TrafficMove");var selection=MovementRead(entry,"TrafficSelection");
                            var moveSource=move==null?null:MovementRead(move,"Source") as TrafficDecision;
                            var moveIntent=move==null?(TrafficIntentKey?)null:(TrafficIntentKey)MovementRead(move,"Intent");
                            TestContext.WriteLine("OLD_COUNTER_PENDING frame="+Time.frameCount+" id="+live.VisitId+" selectedCount="+selected?.Count+
                                " journey="+(intent.HasValue?intent.Value.IntentId+"/g"+intent.Value.Generation+"/active="+coordinator.HasActiveGrant(intent.Value)+"/acq="+coordinator.GetDecision(intent.Value).AcquiredSequence:"NULL")+
                                " move="+(moveIntent.HasValue?moveIntent.Value.IntentId+"/g"+moveIntent.Value.Generation+"/active="+coordinator.HasActiveGrant(moveIntent.Value):"NULL")+
                                " moveSource="+moveSource?.Kind+"/acq="+moveSource?.AcquiredSequence+" pending="+(MovementRead(entry,"PendingTraffic")!=null)+
                                " selection="+(selection==null?"NULL":((TrafficIntentKey)MovementRead(selection,"Intent")).IntentId)+" routeCount="+route.Count);
                        }
                        // pending strict腿尚未Active；从原committed Exit Source验真实Journey / Authenticate the selected journey.
                        var selectedMove=MovementRead(entry,"TrafficMove");
                        var selectedSource=selectedMove==null?null:MovementRead(selectedMove,"Source") as TrafficDecision;
                        intent=selectedSource?.Intent;
                        if(selected==null || selected.Count<2 || !intent.HasValue || selectedMove==null ||
                            !ReferenceEquals(MovementRead(selectedMove,"Coordinator"),coordinator) ||
                            !((TrafficIntentKey)MovementRead(selectedMove,"Intent")).Equals(intent.Value) ||
                            intent.Value.ActorId!=live.VisitId || intent.Value.IntentId!="actual-exit" || selectedSource.AcquiredSequence<=0 ||
                            !coordinator.TryGetCommittedJourney(intent.Value,out var committed) || !ReferenceEquals(committed,selectedSource))continue;
                        var currentDecision=coordinator.GetDecision(intent.Value);
                        if(currentDecision.AcquiredSequence!=selectedSource.AcquiredSequence || currentDecision.FailureDisposition!=TrafficFailureDisposition.None ||
                            currentDecision.Kind!=TrafficDecisionKind.DeferredByBudget && currentDecision.Kind!=TrafficDecisionKind.WaitingForTraffic &&
                            currentDecision.Kind!=TrafficDecisionKind.Granted)continue;
                        selectedObserved++;
                        var body=world.RegisteredActors.Single(a=>a.ActorId==live.VisitId);
                        if(!owner.Snapshot.PaidVisitIds.Contains(live.VisitId) ||
                            !owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==live.VisitId && o.State==OrderState.Completed))continue;
                        var next=queue.Snapshot.Count;
                        if(next>=originalSlots.Length || next>=retained.Length)continue;
                        var occupied=world.RegisteredActors.Select(a=>a.transform.position).ToArray();
                        if(occupied.Any(p=>Vector3.Distance(p,spawn.position)<tailClearance))continue;
                        var samples=new List<Vector3>();
                        for(var index=1;index<selected.Count;index++)
                            for(var part=1;part<=8;part++)samples.Add(Vector3.Lerp(selected[index-1],selected[index],part/8f));
                        foreach(var point in samples.OrderByDescending(p=>Vector3.Distance(p,body.transform.position)))
                        {
                            samplesChecked++;
                            if(!adapter.IsPointWalkable(point) || occupied.Any(p=>Vector3.Distance(p,point)<=tailClearance) ||
                                queue.Snapshot.Any(a=>Vector3.Distance(a.Position,point)<=tailClearance))continue;
                            var approach=adapter.GetCompletePath(spawn.position,point);
                            if(approach==null || !CustomerRoutePlanner.IsClear(approach,occupied,bodyClearance))continue;
                            earlier=entry;earlierBody=body;earlierKey=intent.Value;selectedSuffix=selected.ToArray();projected=point;slot=next;
                            var projectedSlots=originalSlots.ToArray();projectedSlots[slot]=point;
                            queue.UpdateSlots(projectedSlots);retained[slot]=point;projectedFixture=true;break;
                        }
                        if(projectedFixture)break;
                    }
                    if(!projectedFixture)yield return null;
                }
                customers.AutoSpawn=false;
                TestContext.WriteLine("COUNTER_PROJECTION_PREMISES seen="+seen.Count+" pending="+pendingObserved+" selected="+selectedObserved+" samples="+samplesChecked+
                    " projected="+projectedFixture+" counterCount="+queue.Snapshot.Count+" visits="+string.Join(";",customers.Snapshot.Select(v=>v.VisitId+"/"+v.State)));
                Assert.That(projectedFixture,Is.True,"FIXTURE: require a real acquired pending Exit plus an otherwise body-clear Native admission approach; no injected route/request/pose");
                var earlierId=(string)MovementRead(earlier,"Id");var token=customers.Capacity.GetReservations().Single(r=>r.OwnerId==earlierId && r.Kind==CapacityKind.TotalCustomers).Token;
                Assert.That(CustomerRoutePlanner.IsClear(selectedSuffix,new[]{projected},bodyClearance),Is.False,"FIXTURE: new Counter endpoint intersects the real complete pending suffix");
                Assert.That(queue.NextAdmissionPosition.Value,Is.EqualTo(projected));
                Assert.That(queue.Snapshot,Is.Empty,"FIXTURE: no unrelated unpaid Counter head may hold admission");
                foreach(var guard in new[]{"EntryClear","TailClear"})
                    Assert.That((bool)typeof(CustomerFlowController).GetMethod(guard,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(customers,null),Is.True,"FIXTURE: "+guard);
                Assert.That((bool)MovementRead(customers,"integratedAdmissionAllowed"),Is.True,"FIXTURE: admission remains enabled");
                Assert.That((bool)MovementRead(earlier,"PendingExitSegment"),Is.True);Assert.That((long)MovementRead(earlier,"RequestId"),Is.Zero);
                exitObserver=earlierBody.gameObject.AddComponent<CafeActualExitObserver>();exitObserver.VisitId=earlierId;
                exitObserver.Receipt=(id,pose)=>
                {
                    if(!observing)return;actualExit=true;
                    if(id!=earlierId || Vector3.Distance(pose,departure.position)>.081f ||
                        customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,token)).State!=ReservationState.Released)errors.Add("Invalid actual Exit receipt");
                };
                hook.SetValue(customers,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)((operation,id,other,target,path,obstacles,result)=>
                {
                    if(operation=="admission.original-result")originalAdmissionDecision=result;
                }));
                var before=MovementAdmissionFacts();accepted=customers.TrySpawn();var after=MovementAdmissionFacts();
                TestContext.WriteLine("CONTROLLED_COUNTER_PENDING_EXIT notNaturalMain=True owner="+earlierId+" intent="+earlierKey.IntentId+"/g"+earlierKey.Generation+
                    " acquired="+coordinator.GetDecision(earlierKey).AcquiredSequence+" target="+projected.ToString("F9")+" slot="+slot+
                    " suffix=["+string.Join(";",selectedSuffix.Select(p=>p.ToString("F9")))+"] accepted="+accepted+" originalDecision="+originalAdmissionDecision);
                Assert.That(accepted,Is.False,"PRODUCT: later Counter parking must not commit across the earlier selected pending Exit suffix");
                Assert.That(after,Is.EqualTo(before),"PRODUCT: refusal must not allocate canonical admission facts");
                // Initial Deferred alone is not success: watch subsequent normal batch attempts until real owner Exit.
                for(var frame=0;frame<6000 && !actualExit && !unsafeCommit;frame++)
                {
                    yield return null;
                    if(actualExit || accepted || earlierBody==null)continue;
                    var remaining=(IReadOnlyList<Vector3>)typeof(CustomerFlowController).GetMethod("RemainingOwnedRoute",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(customers,new[]{earlier});
                    var stillConflicts=remaining!=null && !CustomerRoutePlanner.IsClear(remaining,new[]{projected},bodyClearance);
                    accepted=customers.TrySpawn();unsafeCommit=accepted && stillConflicts;
                }
                TestContext.WriteLine("CONTROLLED_COUNTER_FINAL actualExit="+actualExit+" unsafeCommit="+unsafeCommit+" accepted="+accepted+" errors="+string.Join(";",errors));
                Assert.That(errors,Is.Empty);Assert.That(unsafeCommit,Is.False,"PRODUCT: no late unsafe parking publication after an initial Deferred");
                Assert.That(actualExit,Is.True,"PRODUCT: protected owner must actually leave within the bounded window");
                Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==earlierId).State,Is.EqualTo(OrderState.Completed));
            }
            finally
            {
                observing=false;customers.AutoSpawn=false;if(exitObserver!=null)exitObserver.Receipt=null;
                hook.SetValue(customers,priorHook);
                if(projectedFixture)queue.UpdateSlots(originalSlots);
                Array.Copy(originalRetained,retained,retained.Length);
            }
        }
        [UnityTest] public IEnumerator SingleSideOrdinaryCollectionTerminalBecomesBlockedWithoutConsumingReadyOrder()
        {
            // Real paid/Ready customer first; obstruct only after the ordinary family was born.
            // 先正常付款/出杯，再堵唯一服务侧；不写State、订单或Core decision。
            yield return PrimePickupFamilyAdmission();
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>owner.Snapshot.Orders.Count==1 && owner.Snapshot.Orders.Values.Single().State==OrderState.ReadyForPickup &&
                customers.Snapshot.Single().State==CustomerVisitState.PickupWaiting && customers.PickupAssignments.Single().Arrived &&
                employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(owner.HoldCollection,Is.True);
            var order=owner.Snapshot.Orders.Single();var delivery=owner.Snapshot.Delivery.Single();
            Assert.That(order.Value.CustomerId,Is.EqualTo(id));Assert.That(owner.Snapshot.PaidVisitIds.Contains(id),Is.True);
            var display=Find<PickupCupDisplay>();var cup=display.VisibleOrders[order.Key];
            var reservations=customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).ToArray();
            var tokens=reservations.Select(r=>r.Token).ToArray();var states=reservations.Select(r=>r.State).ToArray();
            Assert.That(reservations.Single(r=>r.Kind==CapacityKind.TotalCustomers).State,Is.EqualTo(ReservationState.Occupied));
            var originalAssignment=customers.PickupAssignments.Single();
            var visit=((IDictionary)MovementRead(customers,"visits"))[id];
            var actor=(NavigationActor)MovementRead(visit,"Actor");var originalPose=actor.transform.position;
            var side=customers.GetPickupServiceTargets(Find<CafeLayoutRuntime>().FunctionalSurfaceLayout.PickUpPoints.Single().InstanceId).Single();
            var core=(ServiceTrafficCoordinator)MovementRead(customers,"traffic");
            var cursors=(IDictionary)MovementRead(customers,"trafficCandidates");
            // The free-side first Submit must create the ordinary family, never the release-prerequisite fallback.
            Assert.That(customers.TryMoveToPickupService(id,out _),Is.False);
            var matching=cursors.Keys.Cast<TrafficIntentKey>().Where(k=>k.ActorId==id && k.IntentId=="pickup-service").ToArray();
            Assert.That(matching.Length,Is.EqualTo(1));var key=matching.Single();var cursor=cursors[key];
            Assert.That(MovementRead(cursor,"CollectionRelease"),Is.Null,"PREMISE: ordinary family only");
            Assert.That(MovementRead(cursor,"PickupPreparation"),Is.Not.Null);
            var submitted=core.GetDecision(key);
            Assert.That(submitted.Kind,Is.EqualTo(TrafficDecisionKind.DeferredByBudget));
            Assert.That(submitted.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None));
            Assert.That(submitted.AcquiredSequence,Is.Zero);Assert.That(core.HasActiveGrant(key),Is.False);
            NavigationActor obstruction=null;
            try
            {
                obstruction=Blocker(side.Position,"ordinary-collection-terminal-static-body");
                TrafficDecision terminal=null;
                for(var frame=0;frame<6000;frame++)
                {
                    var decision=core.GetDecision(key);
                    if(decision.FailureDisposition==TrafficFailureDisposition.Terminal){terminal=decision;break;}
                    Assert.That(employees.Snapshot.All(e=>e.State==EmployeeState.Idle),Is.True,"PREMISE: no employee failure or moving provider");
                    yield return null;
                }
                Assert.That(terminal,Is.Not.Null,"PREMISE: the original actual family must reach its real finite Terminal");
                Assert.That(terminal.Intent,Is.EqualTo(key));Assert.That(terminal.AcquiredSequence,Is.Zero);
                Assert.That(terminal.Reason,Is.Not.Empty);
                // Exercise the normal consumer as well as the per-frame family refresh; neither may revive Terminal.
                Assert.That(customers.TryMoveToPickupService(id,out _),Is.False);
                for(var frame=0;frame<4;frame++)yield return null;
                Assert.That(customers.Snapshot.Single().State,Is.EqualTo(CustomerVisitState.Blocked),
                    "FEATURE RED: an ordinary collection Terminal must report Blocked instead of polling silently in PickupWaiting");
                Assert.That((string)MovementRead(visit,"Failure"),Does.Contain(terminal.Reason));
                Assert.That(owner.Snapshot.Orders.Single().Key,Is.EqualTo(order.Key));
                Assert.That(owner.Snapshot.Orders[order.Key].State,Is.EqualTo(OrderState.ReadyForPickup));
                Assert.That(owner.Snapshot.Delivery.Single(),Is.EqualTo(delivery));
                Assert.That(display.VisibleOrders[order.Key],Is.SameAs(cup));Assert.That(cup.activeInHierarchy,Is.True);
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
                CollectionAssert.AreEqual(states,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.State));
                Assert.That(customers.PickupAssignments.Single(),Is.EqualTo(originalAssignment));
                Assert.That(MovementRead(visit,"HasCollected"),Is.False);Assert.That((long)MovementRead(visit,"RequestId"),Is.Zero);
                Assert.That(Vector3.Distance(actor.transform.position,originalPose),Is.LessThanOrEqualTo(.001f));
                for(var frame=0;frame<30;frame++)yield return null;
                Assert.That(core.GetDecision(key),Is.SameAs(terminal),"No automatic Terminal replacement or retry");
                Assert.That(customers.Snapshot.Single().State,Is.EqualTo(CustomerVisitState.Blocked));
                Assert.That(display.VisibleOrders[order.Key],Is.SameAs(cup));
                // Remove only the test's body. Physical repair alone cannot restart the old Terminal.
                world.Unregister(obstruction);obstruction.gameObject.SetActive(false);UnityEngine.Object.Destroy(obstruction.gameObject);obstruction=null;
                for(var frame=0;frame<30;frame++)yield return null;
                Assert.That(customers.Snapshot.Single().State,Is.EqualTo(CustomerVisitState.Blocked));
                Assert.That(core.GetDecision(key),Is.SameAs(terminal));
                Assert.That(owner.Snapshot.Orders[order.Key].State,Is.EqualTo(OrderState.ReadyForPickup));
                Assert.That(display.VisibleOrders[order.Key],Is.SameAs(cup));
                Assert.That((long)MovementRead(visit,"RequestId"),Is.Zero);
                var actualExit=false;var exitPose=Vector3.zero;
                var departure=CafeLoopRuntimeProof.Field<Transform>(customers,"exit");
                var receipt=actor.gameObject.AddComponent<CafeActualExitObserver>();receipt.VisitId=id;
                // Existing base UnityTearDown disarms exitTrafficObserving before forced scene unload.
                exitTrafficObserving=true;
                receipt.Receipt=(visitId,pose)=>{if(!exitTrafficObserving)return;actualExit=visitId==id;exitPose=pose;};
                tools.ResumeCollection();tools.ResumeBlockedFlow(); // First explicit Owner recovery starts one new bounded attempt.
                var recovery=MovementRead(visit,"FailedCollection");Assert.That(recovery,Is.Not.Null);
                Assert.That(MovementRead(recovery,"Started"),Is.EqualTo(true),"The first explicit Resume must have created a real current recovery attempt");
                var activeKey=(TrafficIntentKey)MovementRead(recovery,"ActiveKey");Assert.That(activeKey,Is.Not.EqualTo(key));
                var activeEntries=(IDictionary)MovementRead(core,"entries");Assert.That(activeEntries.Contains(activeKey),Is.True);
                var activeEntry=activeEntries[activeKey];var activeSource=core.GetDecision(activeKey);
                Assert.That(activeSource.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None));
                Assert.That(activeSource.Kind==TrafficDecisionKind.DeferredByBudget || activeSource.Kind==TrafficDecisionKind.WaitingForTraffic ||
                    activeSource.Kind==TrafficDecisionKind.Granted,Is.True,"Observe the actual asynchronous stage; do not require an artificial issued leg");
                var activeGeneration=MovementRead(visit,"Generation");var activeRequest=(long)MovementRead(visit,"RequestId");
                if(activeRequest!=0)Assert.That(((IDictionary)MovementRead(world.Service,"byId")).Contains(activeRequest),Is.True,"Any nonzero request must be a real accepted Native request");
                var spent=core.QueriesConsumed(activeKey);var wait=MovementRead(activeEntry,"WaitRemaining");var limit=MovementRead(activeEntry,"FiniteLimit");
                var born=MovementRead(activeEntry,"FirstFrame");var sequence=MovementRead(activeEntry,"RequestSequence");
                tools.ResumeBlockedFlow(); // Real repeat while this same attempt is still Deferred/Waiting/issued; no artificial pause or Core batch.
                Assert.That(MovementRead(visit,"FailedCollection"),Is.SameAs(recovery));
                Assert.That((TrafficIntentKey)MovementRead(recovery,"ActiveKey"),Is.EqualTo(activeKey));Assert.That(activeEntries[activeKey],Is.SameAs(activeEntry));
                Assert.That(MovementRead(visit,"Generation"),Is.EqualTo(activeGeneration));Assert.That((long)MovementRead(visit,"RequestId"),Is.EqualTo(activeRequest));
                Assert.That(core.QueriesConsumed(activeKey),Is.EqualTo(spent));Assert.That(MovementRead(activeEntry,"WaitRemaining"),Is.EqualTo(wait));
                Assert.That(MovementRead(activeEntry,"FiniteLimit"),Is.EqualTo(limit));Assert.That(MovementRead(activeEntry,"FirstFrame"),Is.EqualTo(born));
                Assert.That(MovementRead(activeEntry,"RequestSequence"),Is.EqualTo(sequence));
                for(var frame=0;frame<6000 && !(actualExit && customers.Capacity.GetCapacities().All(c=>c.Used==0) &&
                    employees.Snapshot.All(e=>e.State==EmployeeState.Idle));frame++)yield return null;
                exitTrafficObserving=false;
                Assert.That(actualExit,Is.True,"FEATURE: explicit recovery must continue this original Ready customer through real collection and Exit");
                Assert.That(Vector3.Distance(exitPose,departure.position),Is.LessThanOrEqualTo(.081f));
                Assert.That(customers.Snapshot,Is.Empty);Assert.That(world.RegisteredActors.Any(a=>a.ActorId==id),Is.False);
                Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1));Assert.That(owner.Snapshot.Orders.ContainsKey(order.Key),Is.True);
                Assert.That(owner.Snapshot.Orders[order.Key].State,Is.EqualTo(OrderState.Completed));
                Assert.That(display.VisibleOrders.ContainsKey(order.Key),Is.False);
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
                Assert.That(customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).All(r=>r.State==ReservationState.Released),Is.True);
                Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                Assert.That(employees.Snapshot.All(e=>e.State==EmployeeState.Idle),Is.True);
                Assert.That(core.GetDecision(key).AcquiredSequence,Is.Zero,"The original failed source never acquires a movement right");
                Assert.That(core.GetDecision(key).Kind,Is.Not.EqualTo(TrafficDecisionKind.Granted));
                TestContext.WriteLine("ORDINARY_COLLECTION_TERMINAL originalKey="+key+" reason="+terminal.Reason+
                    " ReadyOrder/Cup/Assignment/TokensRetained=True noAutomaticRetry=True explicitResumeSameOrderActualExit=True");
            }
            finally
            {
                if(obstruction!=null){world.Unregister(obstruction);obstruction.gameObject.SetActive(false);UnityEngine.Object.Destroy(obstruction.gameObject);}
            }
        }
        [UnityTest] public IEnumerator SingleSideMissingReadyCupProviderCannotFallBackToGenericResume()
            => ObserveOrdinaryCollectionProviderFailure("missing");
        [UnityTest] public IEnumerator SingleSideThrowingReadyCupProviderCannotFallBackToGenericResume()
            => ObserveOrdinaryCollectionProviderFailure("throw");
        [UnityTest] public IEnumerator SingleSideInactiveReadyCupCannotFallBackToGenericResume()
            => ObserveOrdinaryCollectionProviderFailure("inactive");
        private IEnumerator ObserveOrdinaryCollectionProviderFailure(string fault)
        {
            yield return PrimePickupFamilyAdmission();Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>owner.Snapshot.Orders.Count==1 && owner.Snapshot.Orders.Values.Single().State==OrderState.ReadyForPickup &&
                customers.Snapshot.Single().State==CustomerVisitState.PickupWaiting && customers.PickupAssignments.Single().Arrived &&
                employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            var order=owner.Snapshot.Orders.Single();var delivery=owner.Snapshot.Delivery.Single();
            var display=Find<PickupCupDisplay>();var cup=display.VisibleOrders[order.Key];Assert.That(cup.activeInHierarchy,Is.True);
            var visit=((IDictionary)MovementRead(customers,"visits"))[id];var actor=(NavigationActor)MovementRead(visit,"Actor");
            var core=(ServiceTrafficCoordinator)MovementRead(customers,"traffic");var cursors=(IDictionary)MovementRead(customers,"trafficCandidates");
            Assert.That(customers.TryMoveToPickupService(id,out _),Is.False);
            var key=cursors.Keys.Cast<TrafficIntentKey>().Single(k=>k.ActorId==id && k.IntentId=="pickup-service");
            Assert.That(MovementRead(cursors[key],"CollectionRelease"),Is.Null);Assert.That(MovementRead(cursors[key],"PickupPreparation"),Is.Not.Null);
            Assert.That(core.GetDecision(key).Kind,Is.EqualTo(TrafficDecisionKind.DeferredByBudget));
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var setter=typeof(CustomerFlowController).GetMethod("SetCollectionRecoveryCupProvider",flags);
            var field=typeof(CustomerFlowController).GetField("collectionRecoveryCup",flags);
            Assert.That(setter,Is.Not.Null);Assert.That(field,Is.Not.Null);
            var original=(Func<string,CafeOrderKey,GameObject>)field.GetValue(customers);
            Assert.That(original,Is.Not.Null,"PREMISE: the healthy runtime provider must be installed before the test faults it");
            Assert.That(original(id,order.Key),Is.SameAs(cup),"PREMISE: the real Ready order must resolve to this active cup");
            var throwCalls=0;
            Func<string,CafeOrderKey,GameObject> installed=fault=="throw"?(Func<string,CafeOrderKey,GameObject>)((_,__)=>
                {throwCalls++;throw new InvalidOperationException("test-ready-cup-provider");}):null;
            NavigationActor obstruction=null;
            try
            {
                // Only the real provider or the real passive cup is faulted; no visit/Core/receipt is edited.
                if(fault=="inactive")cup.SetActive(false);else setter.Invoke(customers,new object[]{installed});
                var side=customers.GetPickupServiceTargets(Find<CafeLayoutRuntime>().FunctionalSurfaceLayout.PickUpPoints.Single().InstanceId).Single();
                obstruction=Blocker(side.Position,"ordinary-collection-provider-"+fault);
                for(var frame=0;frame<6000 && customers.Snapshot.Single().State!=CustomerVisitState.Blocked;frame++)yield return null;
                Assert.That(customers.Snapshot.Single().State,Is.EqualTo(CustomerVisitState.Blocked),"Missing recovery evidence cannot hide the actual collection failure");
                var terminal=core.GetDecision(key);Assert.That(terminal.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.Terminal));
                if(fault=="throw")Assert.That(throwCalls,Is.GreaterThanOrEqualTo(1),"The original Terminal handling must actually invoke the throwing provider");
                Assert.That(MovementRead(visit,"FailedCollection"),Is.Not.Null,"A nonrecoverable failure marker must prevent generic Resume fallback");
                world.Unregister(obstruction);obstruction.gameObject.SetActive(false);UnityEngine.Object.Destroy(obstruction.gameObject);obstruction=null;
                for(var frame=0;frame<30;frame++)yield return null;
                var generation=MovementRead(visit,"Generation");var phase=MovementRead(visit,"TrafficPhaseSequence");var pose=actor.transform.position;
                var nextNative=MovementRead(world.Service,"nextId");var assignment=customers.PickupAssignments.Single();
                var records=customers.Capacity.GetReservations().Where(t=>t.OwnerId==id).ToArray();var tokens=records.Select(t=>t.Token).ToArray();var states=records.Select(t=>t.State).ToArray();
                TrafficIntentKey[] Keys()=>((IDictionary)MovementRead(customers,"trafficKeys")).Values.Cast<TrafficIntentKey>().Where(k=>k.ActorId==id).ToArray();
                var keys=Keys();tools.ResumeCollection();
                for(var attempt=0;attempt<2;attempt++)
                {
                    tools.ResumeBlockedFlow();for(var frame=0;frame<30;frame++)yield return null;
                    Assert.That(customers.Snapshot.Single().State,Is.EqualTo(CustomerVisitState.Blocked));
                    Assert.That(MovementRead(visit,"Generation"),Is.EqualTo(generation));Assert.That(MovementRead(visit,"TrafficPhaseSequence"),Is.EqualTo(phase));
                    Assert.That(MovementRead(visit,"RequestId"),Is.EqualTo(0L));Assert.That(MovementRead(world.Service,"nextId"),Is.EqualTo(nextNative));
                    Assert.That(actor.transform.position,Is.EqualTo(pose));CollectionAssert.AreEquivalent(keys,Keys());Assert.That(core.GetDecision(key),Is.SameAs(terminal));
                    Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1));Assert.That(owner.Snapshot.Orders[order.Key].State,Is.EqualTo(OrderState.ReadyForPickup));
                    Assert.That(owner.Snapshot.Delivery.Single(),Is.EqualTo(delivery));Assert.That(display.VisibleOrders[order.Key],Is.SameAs(cup));
                    Assert.That(cup.activeInHierarchy,Is.EqualTo(fault!="inactive"));Assert.That(customers.PickupAssignments.Single(),Is.EqualTo(assignment));
                    CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Where(t=>t.OwnerId==id).Select(t=>t.Token));
                    CollectionAssert.AreEqual(states,customers.Capacity.GetReservations().Where(t=>t.OwnerId==id).Select(t=>t.State));
                }
                TestContext.WriteLine("ORDINARY_COLLECTION_PROVIDER fault="+fault+" actualTerminal=True Blocked=True explicitResumeNoPhase/Key/Native/Ledger/CupChange=True");
            }
            finally
            {
                if(obstruction!=null){world.Unregister(obstruction);obstruction.gameObject.SetActive(false);UnityEngine.Object.Destroy(obstruction.gameObject);}
                if(customers!=null && fault!="inactive" && ReferenceEquals(field.GetValue(customers),installed))setter.Invoke(customers,new object[]{original});
                if(cup!=null && fault=="inactive")cup.SetActive(true);
            }
        }
    }
}
#endif

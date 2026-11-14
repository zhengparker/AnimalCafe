#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using AnimalCafe.BusinessDay;
using AnimalCafe.CafeLoop;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using AnimalCafe.Orders;
using AnimalCafe.Tests.PlayMode.EditorSceneLoading;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace AnimalCafe.Tests.PlayMode.Phase14
{
    public sealed partial class CafePickupSideTests
    {
        [UnityTest] public IEnumerator LaterCollectedExitPreservesEarlierClearUnissuedPickupRoute()
            => ObserveClearUnissuedPickupBeforeLaterExit(0);
        [UnityTest] public IEnumerator DisabledEarlierUnissuedPickupDoesNotKeepExitAuthority()
            => ObserveClearUnissuedPickupBeforeLaterExit(1);
        [UnityTest] public IEnumerator BlockedEarlierUnissuedPickupDoesNotKeepExitWaiting()
            => ObserveClearUnissuedPickupBeforeLaterExit(2);
        [UnityTest] public IEnumerator EarlierPickupDisabledAfterExitWaitRetiresPriority()
            => ObserveClearUnissuedPickupBeforeLaterExit(3);
        [UnityTest] public IEnumerator EarlierPickupObstructedAfterExitWaitRetiresPriority()
            => ObserveClearUnissuedPickupBeforeLaterExit(4);
        [UnityTest] public IEnumerator CanonicalUntransferredTargetCannotAuthorizePickupExitWait()
            => ObserveClearUnissuedPickupBeforeLaterExit(5);

        [UnityTest] public IEnumerator NativeEmployeeArrivalCallbackPublishesStoppedMotion()
            => ObserveEmployeeStoppedCompletion(0);
        [UnityTest] public IEnumerator NativeEmployeeStoppedMotionDisableDoesNotReviveCompletion()
            => ObserveEmployeeStoppedCompletion(1);
        [UnityTest] public IEnumerator NativeEmployeeStoppedMotionSubscriberFailureRetainsCompletion()
            => ObserveEmployeeStoppedCompletion(2);

        private IEnumerator ObserveEmployeeStoppedCompletion(int scenario)
        {
            // Real shared-side route returns straight toward CM facing; no invented leg/pose/token.
            // 使用既有共享侧布局的真实订单与返程；准备阶段正常driver照常运行。
            yield return SwitchPortObservationFixture(false);
            var rows=CafeLoopRuntimeProof.Field<System.Collections.IEnumerable>(employees,"employees").Cast<object>();
            var row=rows.Single(e=>CafeLoopRuntimeProof.Field<EmployeeRole>(e,"Role")==EmployeeRole.Barista);
            var actor=CafeLoopRuntimeProof.Field<NavigationActor>(row,"Actor");
            var runner=CafeLoopRuntimeProof.Field<EmployeeTaskRunner>(row,"Runner");
            var port=CafeLoopRuntimeProof.Field<object>(row,"Port");
            var presenter=actor.GetComponent<NavigationWalkPresenter>();
            var movementHook=typeof(EmployeeFlowController).GetField("MovementObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var prior=movementHook.GetValue(employees);Assert.That(prior,Is.Null,"Own only this test's observer");
            var submitted=new Dictionary<long,string>();long selectedRequest=0;var rawCallbacks=0;var returnedCallbacks=0;
            var rawArrived=false;var callbackSpeed=-1f;var beforeStopSpeed=-1f;CafeTaskEvent? returned=null;
            Action<float> motion=null;CafeTaskToken? expectedToken=null;
            Action<string,string,string> movement=(id,operation,data)=>
            {
                if(id!=actor.ActorId)return;
                var match=System.Text.RegularExpressions.Regex.Match(data,@"request=(\d+)");long request;
                if(!match.Success || !long.TryParse(match.Groups[1].Value,out request))return;
                if(operation=="submitted" && data.Contains("accepted=True "))submitted[request]=data;
                if(operation=="callback" && request==selectedRequest)
                {rawCallbacks++;rawArrived=data.Contains("status=Arrived ");beforeStopSpeed=presenter.ActualSpeed;}
            };
            Action<CafeTaskEvent> completed=value=>
            {
                if(value.Kind!=CafeTaskEventKind.Returned || value.ActorId!=actor.ActorId || value.RequestId!=selectedRequest ||
                    !expectedToken.HasValue || !value.Token.Equals(expectedToken.Value))return;
                returnedCallbacks++;returned=value;callbackSpeed=presenter.ActualSpeed;
            };
            movementHook.SetValue(employees,movement);employees.IntegratedTaskEvent+=completed;
            var oldStepping=CafeLoopRuntimeProof.Field<bool>(owner,"stepping");
            try
            {
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                Assert.That(customers.TrySpawn(),Is.True);var visitId=customers.Snapshot.Single().VisitId;
                var customer=world.RegisteredActors.Single(a=>a.ActorId==visitId);object leg=null;EmployeeStationBinding binding=null;
                Vector3 start=default;var generation=0;var handle=0L;
                var native=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world.Service,"byId");
                for(var frame=0;frame<6000;frame++)
                {
                    Assert.That(customers.Snapshot.All(v=>v.State!=CustomerVisitState.Blocked),Is.True,customers.Status);
                    Assert.That(employees.Snapshot.All(e=>e.State!=EmployeeState.Blocked),Is.True,owner.Snapshot.Failure);
                    var current=CafeLoopRuntimeProof.Field<object>(runner,"moveLeg");
                    if(runner.Snapshot.State==EmployeeState.Returning && current!=null && (bool)typeof(EmployeeTaskRunner).GetProperty("HasSelectedReturnRoute",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(runner) &&
                        employees.TryGetBinding(EmployeeRole.Barista,out binding))
                    {
                        var request=CafeLoopRuntimeProof.Field<long>(current,"RequestId");
                        var target=CafeLoopRuntimeProof.Field<NavigationTarget>(current,"Target");
                        var distance=Vector3.Distance(actor.transform.position,binding.WorkTarget.Position);
                        var direction=binding.WorkTarget.Position-actor.transform.position;direction.y=0;
                        var aligned=!target.Facing.HasValue || Vector3.Dot(direction.normalized,target.Facing.Value.normalized)>.98f;
                        if(request>0 && native.Contains(request) && submitted.ContainsKey(request) && distance>=.14f && distance<=.18f && aligned &&
                            Vector3.Distance(target.Position,binding.WorkTarget.Position)<=actor.Settings.Epsilon &&
                            !CafeLoopRuntimeProof.Field<bool>(current,"Completed"))
                        {
                            var move=CafeLoopRuntimeProof.Field<object>(port,"trafficMove");
                            var source=CafeLoopRuntimeProof.Field<TrafficDecision>(move,"Source");
                            Assert.That(source,Is.Not.Null);Assert.That(source.AcquiredSequence,Is.GreaterThan(0));
                            Assert.That(CafeLoopRuntimeProof.Field<int>(move,"NativeGeneration"),Is.EqualTo(CafeLoopRuntimeProof.Field<int>(current,"NativeLegGeneration")));
                            Assert.That((string)MovementRead(native[request],"ActorId"),Is.EqualTo(actor.ActorId));
                            selectedRequest=request;leg=current;start=actor.transform.position;generation=CafeLoopRuntimeProof.Field<int>(runner,"generation");
                            handle=runner.Snapshot.HandleId;expectedToken=CafeLoopRuntimeProof.Field<CafeTaskToken?>(row,"AcceptedToken");break;
                        }
                    }
                    yield return null;
                }
                Assert.That(leg,Is.Not.Null,"FIXTURE_NOT_PROVEN: require the original bound final Returning leg .14-.18m from CM, aligned with its real facing");
                Assert.That(expectedToken.HasValue,Is.True);Assert.That(handle,Is.GreaterThan(0));
                Assert.That(presenter.ActualSpeed,Is.GreaterThan(actor.Settings.ArrivalSpeed),"Real original leg is moving before the controlled final step");
                Assert.That(owner.HoldCollection,Is.True);
                var before=owner.Snapshot;var orders=before.Orders.ToDictionary(p=>p.Key,p=>p.Value);
                var paid=before.PaidVisitIds.ToArray();var tokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();
                var total=customers.Capacity.GetReservations().Single(r=>r.OwnerId==visitId && r.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers).Token;
                Assert.That(orders.Count,Is.EqualTo(1));Assert.That(orders.Values.Single().State,Is.EqualTo(OrderState.ReadyForPickup));
                var armed=true;
                if(scenario!=0)
                {
                    motion=speed=>
                    {
                        if(!armed||speed>actor.Settings.ArrivalSpeed)return;
                        armed=false;
                        if(scenario==1)actor.enabled=false;
                        else throw new InvalidOperationException("motion-terminal-observer-fault");
                    };
                    presenter.MotionChanged+=motion;
                    if(scenario==2)LogAssert.Expect(LogType.Exception,new System.Text.RegularExpressions.Regex("motion-terminal-observer-fault"));
                }
                // Only freeze after the original Native request is genuinely bound. One finite World step.
                CafeLoopRuntimeProof.Set(owner,"stepping",true);
                Assert.That(actor.Settings.MaxSubstepSeconds*actor.Settings.MaxSubstepsPerFrame,Is.GreaterThanOrEqualTo(.25f));
                world.Step(.25f);
                Assert.That(Vector3.Distance(start,actor.transform.position),Is.GreaterThan(.05f),"FIXTURE: the real final step actually moved");
                Assert.That(rawCallbacks,Is.EqualTo(1));Assert.That(rawArrived,Is.True,"Original Native callback is Arrived");
                Assert.That(beforeStopSpeed,Is.GreaterThan(actor.Settings.ArrivalSpeed),"Mutation guard: this Native completion still sees the frame's positive displacement before the port's SetMotion(0)");
                if(scenario==1)
                {
                    Assert.That(armed,Is.False);Assert.That(actor.isActiveAndEnabled,Is.False);
                    Assert.That(world.RegisteredActors.Contains(actor),Is.True,"Keep the disabled actor's real body");
                    Assert.That(returned.HasValue,Is.False,"Stopped-motion disable cannot revive port/runner completion");
                    Assert.That(returnedCallbacks,Is.Zero);Assert.That(CafeLoopRuntimeProof.Field<bool>(leg,"Completed"),Is.False);
                }
                else
                {
                    Assert.That(returned.HasValue,Is.True,"Actual original port callback must complete its original runner task");
                    Assert.That(returnedCallbacks,Is.EqualTo(1));Assert.That(returned.Value.RequestId,Is.EqualTo(selectedRequest));
                    Assert.That(returned.Value.HandleId,Is.EqualTo(handle));Assert.That(returned.Value.Generation,Is.EqualTo(generation));
                    Assert.That(callbackSpeed,Is.LessThanOrEqualTo(actor.Settings.ArrivalSpeed),"Stopped presentation is published before the real business Returned callback");
                    Assert.That(CafeLoopRuntimeProof.Field<bool>(leg,"Completed"),Is.True);
                    if(scenario==2)Assert.That(armed,Is.False,"The original stopped-motion subscriber fault really occurred");
                }
                Assert.That(native.Contains(selectedRequest),Is.False);
                var after=owner.Snapshot;CollectionAssert.AreEquivalent(orders.Keys,after.Orders.Keys,"Motion creates no new business order");
                foreach(var pair in orders)
                {Assert.That(after.Orders[pair.Key].CustomerId,Is.EqualTo(pair.Value.CustomerId));Assert.That(after.Orders[pair.Key].State,Is.EqualTo(pair.Value.State));}
                CollectionAssert.AreEqual(paid,after.PaidVisitIds,"Motion creates no payment or duplicate payment");
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(r=>r.Token));
                Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,total)).State,Is.EqualTo(AnimalCafe.Capacity.ReservationState.Occupied));
                Assert.That(world.RegisteredActors.Contains(customer),Is.True,"Employee arrival cannot fake customer Exit");
                TestContext.WriteLine("NATIVE_EMPLOYEE_STOP integrated=True request="+selectedRequest+" nativeCallbacks="+rawCallbacks+" returned="+returnedCallbacks+" beforePortStop="+beforeStopSpeed+" businessSpeed="+callbackSpeed);
            }
            finally
            {
                if(presenter!=null&&motion!=null)presenter.MotionChanged-=motion;
                employees.IntegratedTaskEvent-=completed;
                if(ReferenceEquals(movementHook.GetValue(employees),movement))movementHook.SetValue(employees,prior);
                CafeLoopRuntimeProof.Set(owner,"stepping",oldStepping);
            }
            yield return null;
        }

        private IEnumerator ObserveClearUnissuedPickupBeforeLaterExit(int invalidate)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
            object Read(object v,string name)=>v.GetType().GetField(name).GetValue(v);
            object Call(string name,params object[] args)=>typeof(CustomerFlowController).GetMethod(name,flags).Invoke(customers,args);
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);
            var original=hook.GetValue(customers);NavigationActor blocker=null;
            var tracking=true;var exits=new HashSet<string>();var observers=new List<CafeActualExitObserver>();
            try
            {
                yield return AddCounters(new GridPosition(4,4));Find<DecorationModeController>().ExitDecorationMode();
                yield return Until(()=>owner.CanProgress&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                var firstSpawned=false;yield return Until(()=>firstSpawned||(firstSpawned=customers.TrySpawn()));
                var collector=customers.Snapshot.Single().VisitId;
                yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==collector&&o.State==OrderState.ReadyForPickup)&&
                    employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                var collectorServiceAccepted=false;string reason=null;
                yield return Until(()=>collectorServiceAccepted || (collectorServiceAccepted=customers.TryMoveToPickupService(collector,out reason)));
                Assert.That(collectorServiceAccepted,Is.True,reason);
                var visits=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits");var collected=visits[collector];
                yield return Until(()=>(bool)Read(collected,"ServiceArrived"));
                Assert.That(owner.HoldCollection,Is.True);
                var secondSpawned=false;yield return Until(()=>secondSpawned||(secondSpawned=customers.TrySpawn()));
                var incoming=customers.Snapshot.Single(v=>v.VisitId!=collector).VisitId;
                if(invalidate==0 && Environment.GetCommandLineArgs().Contains("-unissuedPickupCoreFixtureWitness"))
                {
                    yield return ObserveUnissuedPickupCoreFixtureWindow(collector,incoming);
                    Assert.Fail("DIAGNOSTIC_ONLY: stored actual Core/Native geometry is a fixture witness, not acceptance of the six migrated contracts");
                }
                yield return Until(()=>owner.Snapshot.PaidVisitIds.Contains(incoming)&&
                    customers.Snapshot.Single(v=>v.VisitId==incoming).State==CustomerVisitState.MovingToPickup);
                if(invalidate==5)yield return Until(()=>(bool)Read(visits[incoming],"QueueLeft"));
                CafeLoopRuntimeProof.Set(owner,"stepping",true);
                var waiter=visits[incoming];var actor=(NavigationActor)Read(waiter,"Actor");
                var target=(NavigationTarget)Read(waiter,"FinalTarget");
                // Existing controlled unissued-leg seam. Real payment, owner, goal and pose stay intact.
                // 只重新选择未发waypoint；不写proof、HasCollected、Native请求、订单或位置。
                Call("HoldVisit",waiter,false);Call("SetRouteContext",waiter,CustomerVisitState.MovingToPickup,target);
                string reservedId=null;PickupQueueService reservedOwner=null;QueueAssignment reserved=default;
                if(invalidate==5)
                {
                    var spawned=false;yield return Until(()=>spawned||(spawned=customers.TrySpawn()));
                    reservedId=customers.Snapshot.Single(v=>v.VisitId!=collector&&v.VisitId!=incoming).VisitId;
                    yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==reservedId).CounterArrived);
                    Assert.That(customers.TryReservePickup(reservedId,out reason),Is.True,reason);
                    reservedOwner=(PickupQueueService)Read(visits[reservedId],"PickupOwner");
                    Assert.That(reservedOwner.TryGetReservation(reservedId,out reserved),Is.True);
                    Assert.That(owner.Snapshot.PaidVisitIds.Contains(reservedId),Is.False);
                }
                var selected=new Queue<Vector3>(new[]{new Vector3(.4078815f,0,-.9016669f),target.Position});
                waiter.GetType().GetField("Route").SetValue(waiter,selected);
                var path=(IReadOnlyList<Vector3>)Call("ExpandRoute",actor.transform.position,selected);
                var clearance=(float)Call("RouteClearance",actor.Settings);
                Assert.That(CustomerRoutePlanner.IsClear(path,(Vector3[])Call("CorridorObstacles",waiter),clearance),Is.True,
                    "FIXTURE: original full native route is clear before acquiring the unissued promise");
                Call("RememberOwnedRoute",waiter,CustomerVisitState.MovingToPickup,null);
                waiter.GetType().GetField("PendingPickupSegment").SetValue(waiter,true);
                Assert.That((bool)Call("HasOwnedRoute",waiter),Is.True);
                Assert.That(Read(waiter,"RequestId"),Is.EqualTo(0L));
                Assert.That(Read(waiter,"OwnedWaitProof"),Is.Null,"This is a clear selected route, not a fabricated finite contributor");
                if(invalidate==5)
                {
                    // Controlled renewal through the real owner API, outside the new-target gate.
                    // 构造既有静态预留；不声称正常CFC会允许这个后来目标，也不伪造Entry/付款/位置。
                    Assert.That(reservedOwner.Remove(reservedId),Is.True);
                    var slots=CafeLoopRuntimeProof.Field<Vector3[]>(reservedOwner,"waitSlots");
                    slots[reserved.SlotIndex]=new Vector3(.4078815f,0,-.9016669f);
                    CafeLoopRuntimeProof.Field<Vector3[]>(customers,"pickupWaitSlots")[reserved.SlotIndex]=slots[reserved.SlotIndex];
                    Assert.That(reservedOwner.TryReserve(reservedId,out var renewed),Is.True);
                    blocker=Blocker(renewed.Position,"canonical-reservation-certificate-invalidation");
                    Assert.That(customers.TryReservePickup(reservedId,out _),Is.False);
                    Assert.That(Read(visits[reservedId],"ReservedPickupPath"),Is.Null);
                    world.Unregister(blocker);UnityEngine.Object.DestroyImmediate(blocker.gameObject);blocker=null;
                    Assert.That(reservedOwner.TryGetReservation(reservedId,out var retained),Is.True);
                    Assert.That(retained.Generation,Is.EqualTo(renewed.Generation));
                    Assert.That((bool)Call("HasOwnedRoute",waiter),Is.True,"The older clear capture remains bound");
                    Assert.That(CustomerRoutePlanner.IsClear(path,(Vector3[])Call("CorridorObstacles",waiter),clearance),Is.False);
                    var claimType=typeof(CustomerFlowController).GetNestedType("PickupExitClaim",BindingFlags.NonPublic);
                    var claim=Activator.CreateInstance(claimType,true);
                    foreach(var name in new[]{"Owner","Actor","PickupOwner","Route","Phase","Assignment"})
                        claimType.GetField(name).SetValue(claim,name=="Owner"?waiter:name=="Phase"?Read(waiter,"State"):Read(waiter,name));
                    Assert.That(Call("IndependentPickupClaimRoute",claim,collected),Is.Null,
                        "A real unpaid reserved target must retire WAIT authority even after its disposable route cache is gone");
                    Assert.That(owner.Snapshot.PaidVisitIds.Contains(reservedId),Is.False);
                    yield break;
                }
                var tokens=new Dictionary<string,AnimalCafe.Capacity.CapacityToken>();
                foreach(var id in new[]{collector,incoming})
                {
                    tokens[id]=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id&&r.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers).Token;
                    var body=world.RegisteredActors.Single(a=>a.ActorId==id);var observer=body.gameObject.AddComponent<CafeActualExitObserver>();
                    observer.VisitId=id;observer.Receipt=(visit,pose)=>
                    {
                        if(!tracking)return;
                        Assert.That(Vector3.Distance(pose,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position),Is.LessThanOrEqualTo(.081f));
                        Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,tokens[visit])).State,
                            Is.EqualTo(AnimalCafe.Capacity.ReservationState.Released));
                        Assert.That(exits.Add(visit),Is.True);
                    };observers.Add(observer);
                }
                if(invalidate==1)actor.gameObject.SetActive(false);
                if(invalidate==2)blocker=Blocker(new Vector3(-1.25f,0,-.2f),"unissued-owner-static-obstruction");
                var qualified=(bool)Call("HasOwnedRoute",waiter)&&CustomerRoutePlanner.IsClear(path,(Vector3[])Call("CorridorObstacles",waiter),clearance);
                Assert.That(qualified,Is.EqualTo(invalidate!=1&&invalidate!=2));
                var candidateSeen=false;var ownerCross=false;var oldGoal=target.Position;
                hook.SetValue(customers,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)
                    ((operation,id,related,goal,route,obstacles,result)=>
                    {
                        if(operation!="route.actual-plan"||id!=collector||route==null)return;
                        candidateSeen=true;
                        var actual=(IReadOnlyList<Vector3>)Call("ExpandRoute",((NavigationActor)Read(collected,"Actor")).transform.position,route);
                        ownerCross=(bool)Call("RoutesConflict",actual,path,clearance);
                        TestContext.WriteLine("UNISSUED_EXIT_BOUNDARY qualified="+qualified+" ownerCross="+ownerCross+
                            " contributor=NULL; contributorGoal=NOT_APPLICABLE ownerGoal="+oldGoal.ToString("F9")+
                            " candidate="+string.Join(";",actual.Select(p=>p.ToString("F9")))+" protected="+string.Join(";",path.Select(p=>p.ToString("F9"))));
                    }));
                var service=CafeLoopRuntimeProof.Field<CafeLoopService>(owner,"service");
                owner.HoldCollection=false;
                try{Assert.That(service.TryCollect(collector,out reason),Is.True,reason);}
                finally{owner.HoldCollection=true;}
                Assert.That(candidateSeen,Is.True);
                Assert.That(ownerCross,Is.True,"FIXTURE: actual new exit crosses the earlier clear selected pickup route");
                Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==collector).State,Is.EqualTo(OrderState.Completed));
                if(invalidate==1||invalidate==2)
                {
                    Assert.That((long)Read(collected,"RequestId"),Is.GreaterThan(0),"Retired/obstructed promises cannot retain future route authority");
                    Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,tokens[incoming])).State,
                        Is.Not.EqualTo(AnimalCafe.Capacity.ReservationState.Released));
                    Assert.That(exits,Is.Empty);yield break;
                }
                Assert.That(Read(collected,"RequestId"),Is.EqualTo(0L),
                    "A later collected exit must preserve the earlier qualified unissued pickup route before native dispatch");
                Assert.That(Read(collected,"PendingExitSegment"),Is.EqualTo(true));
                if(invalidate==3||invalidate==4)
                {
                    Assert.That((bool)Call("CanWaitForOwnedPickupExitTraffic",collected),Is.True);
                    var body=((NavigationActor)Read(collected,"Actor")).transform.position;
                    var capacity=customers.Capacity.GetCapacities().Select(c=>c.Used).ToArray();
                    if(invalidate==3)actor.gameObject.SetActive(false);
                    else blocker=Blocker(new Vector3(-1.25f,0,-.2f),"captured-unissued-owner-static-obstruction");
                    Assert.That((bool)Call("CanWaitForOwnedPickupExitTraffic",collected),Is.False,
                        "A captured clear promise must lose WAIT authority after disable/static obstruction");
                    Assert.That(((NavigationActor)Read(collected,"Actor")).transform.position,Is.EqualTo(body));
                    CollectionAssert.AreEqual(capacity,customers.Capacity.GetCapacities().Select(c=>c.Used));
                    Assert.That(exits,Is.Empty,"Retiring priority cannot fabricate a departure or release capacity");
                    yield break;
                }
                hook.SetValue(customers,original);CafeLoopRuntimeProof.Set(owner,"stepping",false);tools.ResumeCollection();
                yield return Until(()=>exits.Count==2&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                Assert.That(owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed),Is.True);
                Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
            }
            finally
            {
                tracking=false;foreach(var observer in observers)if(observer!=null)observer.Receipt=null;
                if(blocker!=null){world.Unregister(blocker);UnityEngine.Object.DestroyImmediate(blocker.gameObject);}
                if(customers!=null)hook.SetValue(customers,original);
                if(owner!=null)CafeLoopRuntimeProof.Set(owner,"stepping",false);
            }
        }

        // One bounded real-business fixture witness. It intentionally cannot mark the legacy contract PASS.
        // 不写Route/State/pose/permit/request，不暂停正常driver；只在真实Bind发布后收集一次真实Ready队首。
        private IEnumerator ObserveUnissuedPickupCoreFixtureWindow(string collector,string incoming)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            object Read(object value,string name)=>value==null?null:MovementRead(value,name);
            var core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic");
            var entries=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(core,"entries");
            var visits=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits");
            var native=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world.Service,"byId");
            var service=CafeLoopRuntimeProof.Field<CafeLoopService>(owner,"service");
            var conflict=typeof(ServiceTrafficCoordinator).GetMethod("PathsConflict",BindingFlags.Static|BindingFlags.NonPublic);
            var errors=new List<string>();var records=new List<string>();var seen=new HashSet<string>();
            var bound=false;var collected=false;var attempted=false;var waiting=false;var strictExit=false;var active=true;var captureFrame=-1;
            TrafficIntentKey earlierKey=default;TrafficDecision originalSource=null;object earlierEntry=null;NavigationActor earlierBody=null;
            var savedSuffix=Array.Empty<Vector3>();var savedPath=Array.Empty<Vector3>();var savedPose=default(Vector3);
            string Points(IEnumerable<Vector3> points)=>points==null?"NULL":string.Join(";",points.Select(p=>p.ToString("F6")));
            bool LiveBound()
            {
                if(!bound || earlierEntry==null || !entries.Contains(earlierKey) || !ReferenceEquals(entries[earlierKey],earlierEntry) ||
                    !ReferenceEquals(Read(earlierEntry,"JourneyPermit"),originalSource) || (bool)Read(earlierEntry,"Retired"))return false;
                var leg=Read(earlierEntry,"Leg");if(leg==null || !(bool)Read(leg,"Committed") || !(bool)Read(leg,"Bound") || (bool)Read(leg,"Completed"))return false;
                var stamp=(TrafficNativeStamp)Read(leg,"Stamp");return native.Contains(stamp.RequestId) && ReferenceEquals(Read(leg,"ActorLife"),earlierBody);
            }
            void Observe(CafeLoopSnapshot ignored)
            {
                if(!active)return;
                try
                {
                    if(!bound && visits.Contains(incoming))
                    {
                        var visit=visits[incoming];var move=Read(visit,"TrafficMove");var paid=Read(visit,"PaidPickup");
                        if(move==null || paid==null || !(bool)Read(paid,"Accepted") || !(bool)Read(paid,"Transferred") || (bool)Read(paid,"Failed"))return;
                        var key=(TrafficIntentKey)Read(move,"Intent");if(key.IntentId!="pickup-paid-movement" || !entries.Contains(key))return;
                        var entry=entries[key];var leg=Read(entry,"Leg");var source=Read(move,"Source") as TrafficDecision;
                        if(leg==null || source==null || source.AcquiredSequence<=0 || !ReferenceEquals(Read(entry,"JourneyPermit"),source) ||
                            !(bool)Read(leg,"Committed") || !(bool)Read(leg,"Bound") || (bool)Read(leg,"Completed"))return;
                        var stamp=(TrafficNativeStamp)Read(leg,"Stamp");var body=Read(visit,"Actor") as NavigationActor;
                        if(body==null || !body.isActiveAndEnabled || !native.Contains(stamp.RequestId) || (long)Read(visit,"RequestId")!=stamp.RequestId ||
                            (long)Read(visit,"OwnedIssuedRequest")!=stamp.RequestId || !ReferenceEquals(Read(leg,"ActorLife"),body))return;
                        bound=true;captureFrame=Time.frameCount;earlierKey=key;earlierEntry=entry;originalSource=source;earlierBody=body;savedPose=body.transform.position;
                        savedPath=((Vector3[])Read(entry,"CurrentPath")).ToArray();savedSuffix=((Vector3[])Read(leg,"Suffix")).ToArray();
                        var proof=Read(leg,"Proof") as TrafficRouteProof;
                        records.Add("UNISSUED_CORE_BOUND frame="+captureFrame+" key="+key+" request="+stamp.RequestId+" generation="+stamp.LegGeneration+
                            " samePaid=True realNativeLive=True bound=True acquired="+source.AcquiredSequence+" first="+Read(entry,"FirstFrame")+" queries="+Read(entry,"Queries")+
                            " physicalPose="+savedPose.ToString("F6")+" proofWaypoints="+Points(proof?.Waypoints)+" legIndex="+Read(leg,"Index")+
                            " current=["+Points(savedPath)+"] suffix=["+Points(savedSuffix)+"] distinctFutureGeometry="+savedSuffix.Any(p=>Vector3.Distance(p,savedSuffix[0])>body.Settings.ArrivalDistance));
                        // Public business operation, once; normal owner/World/Core updates continue unchanged.
                        attempted=true;var held=owner.HoldCollection;owner.HoldCollection=false;
                        try{collected=service.TryCollect(collector,out var reason);records.Add("UNISSUED_CORE_COLLECT accepted="+collected+" reason="+reason+" originalBoundStillLive="+LiveBound());}
                        finally{owner.HoldCollection=held;}
                    }
                    if(!bound)return;
                    foreach(System.Collections.DictionaryEntry row in entries)
                    {
                        var key=(TrafficIntentKey)row.Key;if(key.ActorId!=collector || key.IntentId!="actual-exit")continue;
                        var entry=row.Value;var decision=core.GetDecision(key);var leg=Read(entry,"Leg");
                        var onEarlier=decision.Blockers.Any(b=>b.Owner.Equals(earlierKey));if(decision.Kind==TrafficDecisionKind.WaitingForTraffic && onEarlier)waiting=true;
                        var current=Read(entry,"CurrentPath") as Vector3[];var exitBound=leg!=null && (bool)Read(leg,"Bound");if(exitBound)strictExit=true;
                        var fingerprint=decision.Kind+"/"+decision.Reason+"/"+decision.AcquiredSequence+"/"+onEarlier+"/"+exitBound+"/"+LiveBound();
                        if(seen.Count>=8 || !seen.Add(fingerprint))continue;
                        var earlierRight=Read(earlierEntry,"Journey") as Vector3[];var clearance=earlierBody.Settings.AgentRadius+(Read(visits[collector],"Actor") as NavigationActor).Settings.AgentRadius+
                            Math.Max(earlierBody.Settings.CollisionSkin+earlierBody.Settings.Epsilon,(Read(visits[collector],"Actor") as NavigationActor).Settings.CollisionSkin+(Read(visits[collector],"Actor") as NavigationActor).Settings.Epsilon);
                        bool? cross=current!=null && current.Length>1 && earlierRight!=null && earlierRight.Length>1?(bool?)conflict.Invoke(null,new object[]{current,earlierRight,clearance}):null;
                        records.Add("UNISSUED_CORE_EXIT frame="+Time.frameCount+" key="+key+" kind="+decision.Kind+" reason="+decision.Reason+" acquired="+decision.AcquiredSequence+
                            " first="+Read(entry,"FirstFrame")+" queries="+Read(entry,"Queries")+" finite="+Read(entry,"FiniteLimit")+" remaining="+Read(entry,"WaitRemaining")+
                            " waitOnOriginal="+onEarlier+" earlierCurrentBound="+LiveBound()+" strictBound="+exitBound+" currentCrossLiveEarlier="+(cross.HasValue?cross.Value.ToString():"NOT_EVALUATED")+
                            " blockers=["+string.Join(";",decision.Blockers.Select(b=>b.Owner+"/"+b.ResourceId+"/"+b.Proof))+"] path=["+Points(current)+"] earlierCurrentRight=["+Points(earlierRight)+"]");
                    }
                }
                catch(Exception error){if(errors.Count<4)errors.Add(error.ToString());}
            }
            owner.SnapshotChanged+=Observe;
            try
            {
                for(var frame=0;frame<6000 && !bound && errors.Count==0;frame++)yield return null;
                for(var frame=0;frame<180 && bound && errors.Count==0;frame++)yield return null;
            }
            finally
            {
                active=false;owner.SnapshotChanged-=Observe;
                TestContext.WriteLine("UNISSUED_CORE_FIXTURE_WITNESS bound="+bound+" actualCollectAttempted="+attempted+" actualCollected="+collected+" waitOnOriginal="+waiting+
                    " actualExitBound="+strictExit+" suffixPoints="+savedSuffix.Length+" captureFrame="+captureFrame+" errors="+errors.Count+" status=DIAGNOSTIC_ONLY_NOT_ACCEPTANCE");
                foreach(var record in records)TestContext.WriteLine(record);foreach(var error in errors)TestContext.WriteLine("UNISSUED_CORE_FIXTURE_UNKNOWN "+error);
            }
        }

        // Local diagnostic, intentionally separate from fast synthetic-clock regression.
        // 使用实际P15 Scene与现实时间，记录自然停滞；未复现不能冒充修复通过。
        [UnityTest, Timeout(900000)] public IEnumerator MixedClockCentralCounterCompletesSixtyRealExitsAndDrainsAfterDecoration()
        {
            if(!Environment.GetCommandLineArgs().Contains("-mixedClockBusinessAcceptance"))
                Assert.Ignore("Opt-in actual-clock acceptance requires -mixedClockBusinessAcceptance and -mixedClockStallWitness.");
            yield return MixedClockCentralCounterNativeStallWitness();
        }

        [UnityTest, Timeout(900000)] public IEnumerator MixedClockCentralCounterNativeStallWitness()
        {
            if(!Environment.GetCommandLineArgs().Contains("-mixedClockStallWitness"))
                Assert.Ignore("Opt-in real-clock manual-stall investigation; requires -mixedClockStallWitness.");
            Time.captureDeltaTime=0;
            var oldInputs=inputs;
            SceneManager.SetActiveScene(SceneManager.CreateScene("Mixed clock previous fixture cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(oldInputs);
            scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase15BusinessDay.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            owner=Find<CafeLoopController>();customers=Find<CustomerFlowController>();employees=Find<EmployeeFlowController>();
            tools=Find<CafeLoopValidationController>();world=Find<NavigationWorld>();adapter=Find<NavigationLayoutAdapter>();time=Find<GameTimeService>();
            inputs=Phase8SceneInputTestCleanup.CaptureAssets(scene);
            var day=Find<BusinessDayController>();var decor=Find<DecorationModeController>();
            var directory=Path.Combine("outputs","phase15","mixed-clock-stall",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);
            var compact=Environment.GetCommandLineArgs().Contains("-compactCorridorWitness");
            var acceptance=Environment.GetCommandLineArgs().Contains("-mixedClockBusinessAcceptance");
            var verbose=!compact||Environment.GetCommandLineArgs().Contains("-retainVerboseTrace");
            var history=new Queue<string>();var decisions=verbose?new CorridorTrace(this,true):null;
            var compactDecisions=compact?new CompactDecisionWitness(this):null;
            var receiptTracking=false;string receiptError=null;
            var seenReceipts=new HashSet<string>();var actualExits=new HashSet<string>();
            var cohortExcludedVisits=new HashSet<string>();var admittedCohort=new HashSet<string>();
            var cohortEpoch=-1L;var cohortArrivalsHeld=false;
            var receiptObservers=new List<CafeActualExitObserver>();var exitReceipts=new List<string>();
            Action<CafeLoopSnapshot> observePaid=snapshot=>
            {
                if(!receiptTracking||!centerTrafficObserving)return;
                if(snapshot.Epoch!=cohortEpoch){receiptError="Acceptance admission cohort changed epoch";return;}
                if(!cohortArrivalsHeld)
                {
                    // Count committed admissions; pending proposals are outside this bounded workload.
                    // 只收真实Visit/身体/Occupied Total，不把未出生的请求算作入店。
                    foreach(var visit in customers.Snapshot)
                    {
                        if(cohortExcludedVisits.Contains(visit.VisitId)||admittedCohort.Contains(visit.VisitId))continue;
                        if(!world.RegisteredActors.Any(actor=>actor!=null&&actor.ActorId==visit.VisitId)||
                            !customers.Capacity.GetReservations().Any(reservation=>reservation.OwnerId==visit.VisitId&&
                                reservation.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers&&
                                reservation.State==AnimalCafe.Capacity.ReservationState.Occupied))continue;
                        admittedCohort.Add(visit.VisitId);
                    }
                    if(admittedCohort.Count>=60)
                    {
                        cohortArrivalsHeld=true;tools.HoldArrivals();
                        TestContext.WriteLine("MIXED_COHORT_ADMISSIONS_HELD admitted="+admittedCohort.Count+" excludedExisting="+
                            cohortExcludedVisits.Count+" epoch="+cohortEpoch+" frame="+Time.frameCount+" realtime="+Time.realtimeSinceStartupAsDouble);
                    }
                }
                foreach(var id in snapshot.PaidVisitIds.Where(id=>!seenReceipts.Contains(id)))
                {
                    var actor=world.RegisteredActors.SingleOrDefault(a=>a.ActorId==id);
                    if(actor==null)continue;
                    var token=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id&&r.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers).Token;
                    seenReceipts.Add(id);
                    var observer=actor.gameObject.AddComponent<CafeActualExitObserver>();observer.VisitId=id;
                    observer.Receipt=(visit,pose)=>
                    {
                        if(!receiptTracking||!centerTrafficObserving)return;
                        var atExit=Vector3.Distance(pose,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position)<=.081f;
                        var released=customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,token)).State==AnimalCafe.Capacity.ReservationState.Released;
                        var completed=owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==visit&&o.State==OrderState.Completed);
                        var unique=actualExits.Add(visit);
                        exitReceipts.Add("visit="+visit+" pose="+pose.ToString("F9")+" atExit="+atExit+" originalTotalReleased="+released+" completed="+completed+" unique="+unique);
                        if(!atExit||!released||!completed||!unique)receiptError="Actual departure proof failed for "+visit;
                    };receiptObservers.Add(observer);
                }
            };
            var priorCleanup=controlledHandoffCleanup;Action<string> cleanup=null;
            cleanup=reason=>
            {
                receiptTracking=false;centerTrafficObserving=false;
                if(owner!=null)owner.SnapshotChanged-=observePaid;
                foreach(var observer in receiptObservers)if(observer!=null)observer.Receipt=null;
                if(ReferenceEquals(controlledHandoffCleanup,cleanup))controlledHandoffCleanup=priorCleanup;
                priorCleanup?.Invoke(reason);
            };
            if(acceptance){owner.SnapshotChanged+=observePaid;controlledHandoffCleanup=cleanup;}
            var arrivals=new List<string>();
            void ObserveDeliveryArrival(CafeTaskEvent value)
            {
                if(value.Kind!=CafeTaskEventKind.DeliveryArrived)return;
                var actor=world.RegisteredActors.Single(a=>a.ActorId==value.ActorId);
                var row=CafeLoopRuntimeProof.Field<System.Collections.IEnumerable>(employees,"employees").Cast<object>()
                    .Single(e=>CafeLoopRuntimeProof.Field<string>(e,"Id")==value.ActorId);
                var runner=CafeLoopRuntimeProof.Field<EmployeeTaskRunner>(row,"Runner");
                var binding=CafeLoopRuntimeProof.Field<EmployeeStationBinding>(row,"Binding");
                var goal=runner.DeliveryTarget.Value;
                var drivers=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world,"drivers");
                var driver=drivers[actor];
                var arrivalFlags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
                var ready=typeof(EmployeeFlowController).GetProperty("Ready",arrivalFlags)?.GetValue(employees);
                var progress=typeof(EmployeeFlowController).GetProperty("ProgressAllowed",arrivalFlags)?.GetValue(employees);
                var validBinding=typeof(EmployeeFlowController).GetMethod("ValidBinding",arrivalFlags)?.Invoke(employees,new object[]{binding});
                arrivals.Add("frame="+Time.frameCount+" realtime="+Time.realtimeSinceStartupAsDouble+" request="+value.RequestId+
                    " actor="+value.ActorId+" token="+value.Token+" stage="+value.Checkpoint.Stage+
                    " runnerCurrent="+runner.IsCurrentDeliveryArrival(value)+" portPrecheck="+employees.IsCurrentDeliveryArrival(value,binding.CupAddress.Value)+
                    " ready="+ready+" progressAllowed="+progress+" bindingCorroboration="+validBinding+
                    " deliveryDelegate="+(typeof(EmployeeFlowController).GetField("deliveryCommit",arrivalFlags)?.GetValue(employees)!=null)+
                    " target="+goal.Position.ToString("F9")+" pose="+actor.transform.position.ToString("F9")+
                    " distance="+Vector3.Distance(actor.transform.position,goal.Position).ToString("R")+
                    " facingError="+(goal.Facing.HasValue?Vector3.Angle(actor.transform.forward,goal.Facing.Value):0).ToString("R")+
                    " presenterSpeed="+actor.GetComponent<NavigationWalkPresenter>().ActualSpeed.ToString("R")+
                    " driverSpeed="+CafeLoopRuntimeProof.Field<float>(driver,"actualSpeed").ToString("R")+
                    " delta="+Time.deltaTime.ToString("R"));
            }
            employees.IntegratedTaskEvent+=ObserveDeliveryArrival;
            var start=Time.realtimeSinceStartupAsDouble;var lastSample=start;var changedAt=start;var lastSignature="";
            var duration=Environment.GetCommandLineArgs().Contains("-deliveryArrivalWitnessOnly")?120:780;
            var stage=0;var decoratedAt=0d;var pausedAt=0d;
            try
            {
                while(Time.realtimeSinceStartupAsDouble-start<duration)
                {
                    var now=Time.realtimeSinceStartupAsDouble;var business=day.Snapshot;
                    if(stage==0 && business!=null && business.DayNumber==2 && business.Phase==BusinessDayPhase.Open &&
                        business.RemainingSeconds<=32 && customers.Snapshot.Count>0)
                    {time.SetPaused();stage=1;}
                    if(stage==1 && business.Phase==BusinessDayPhase.Closing)
                    {
                        var epoch=owner.Snapshot.Epoch;
                        // Capture old visitors before Decor; a birth during later yields must stay in the cohort.
                        // 提前排除旧访客；Confirm后刚出生的新客不能被tracking启用时的快照误排除。
                        if(acceptance)foreach(var visit in customers.Snapshot)cohortExcludedVisits.Add(visit.VisitId);
                        decor.EnterDecorationMode();yield return null;decor.ExitDecorationMode();yield return null;
                        Assert.That(owner.Snapshot.Epoch,Is.EqualTo(epoch));
                        yield return AddCounters(new GridPosition(4,4));decor.ExitDecorationMode();yield return null;
                        Assert.That(owner.Snapshot.Epoch,Is.EqualTo(epoch+1));
                        if(acceptance){cohortEpoch=owner.Snapshot.Epoch;receiptTracking=true;centerTrafficObserving=true;observePaid(owner.Snapshot);}
                        time.SetNormal();decoratedAt=Time.realtimeSinceStartupAsDouble;stage=2;changedAt=decoratedAt;
                    }
                    else if(stage==2 && now-decoratedAt>=30){time.SetFast();stage=3;}
                    else if(stage==3 && business.DayNumber>=3 && business.Phase==BusinessDayPhase.Closing)
                    {time.SetPaused();pausedAt=now;stage=4;}
                    else if(stage==4 && now-pausedAt>=8){time.SetNormal();stage=5;changedAt=now;}
                    else if(stage==5 && business.DayNumber>=4 && business.Phase==BusinessDayPhase.Closing)
                    {time.SetFast();stage=6;}

                    if(now-lastSample>=1)
                    {
                        lastSample=now;
                        var snapshot=MixedClockStoredState(stage,day);
                        if(receiptError!=null)Assert.Fail(receiptError);
                        history.Enqueue(snapshot);while(history.Count>40)history.Dequeue();
                        var signature=owner.Snapshot.Orders.Count+"/"+owner.Snapshot.Orders.Values.Count(o=>o.State==OrderState.Completed)+"/"+
                            string.Join(";",world.RegisteredActors.OrderBy(a=>a.ActorId).Select(a=>a.ActorId+"@"+a.transform.position.ToString("F3")))+"/"+
                            string.Join(";",employees.Snapshot.Select(e=>e.State+"/"+e.RemainingWorkSeconds.ToString("F1")));
                        if(signature!=lastSignature){lastSignature=signature;changedAt=now;}
                        if(stage>=2 && stage!=4 && time.CurrentSpeed!=GameSpeed.Paused && customers.Snapshot.Count>0 && now-changedAt>=15)
                        {
                            File.WriteAllText(Path.Combine(directory,"first-stationary.txt"),snapshot);
                            Assert.Fail("Natural real-clock service stationary for 15s; inspect owned Entry/Visit/native receipts: "+directory);
                        }
                        if(customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked)||employees.Snapshot.Any(e=>e.State==EmployeeState.Blocked))
                        {File.WriteAllText(Path.Combine(directory,"first-blocked.txt"),snapshot);Assert.Fail("Real-clock native Blocked: "+directory);}
                        if((!compact||acceptance) && stage>=2 && owner.Snapshot.Orders.Values.Count(o=>o.State==OrderState.Completed)>=60 &&
                            customers.Snapshot.Count==0 && business.Phase==BusinessDayPhase.Rest)
                        {
                            if(!acceptance)Assert.Inconclusive("Target manual stall not reproduced before sixty completed and drained: "+directory);
                            if(actualExits.Count>=60)
                            {
                                Assert.That(receiptError,Is.Null);
                                Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                                Assert.That(employees.Snapshot.All(e=>e.State==EmployeeState.Idle),Is.True);
                                Assert.That(CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world.Service,"byId").Count,Is.EqualTo(0));
                                Assert.That(world.RegisteredActors.All(a=>employees.Snapshot.Any(e=>e.ActorId==a.ActorId)),Is.True);
                                TestContext.WriteLine("REAL_CLOCK_ACCEPTANCE actualExits="+actualExits.Count+" day="+business.DayNumber+" phase="+business.Phase+
                                    " epoch="+owner.Snapshot.Epoch+" stage="+stage+" capacity=0 nativeRequests=0 employees=Idle");
                                yield break;
                            }
                        }
                    }
                    yield return null;
                }
                if(acceptance)Assert.Fail("Bounded acceptance did not reach sixty actual exits and a true drain; actualExits="+actualExits.Count+" evidence="+directory);
                Assert.Inconclusive("Bounded real-clock investigation ended without reproducing the target stall: "+directory);
            }
            finally
            {
                if(acceptance)cleanup("Mixed clock finally");
                File.WriteAllLines(Path.Combine(directory,"actual-exit-receipts.txt"),exitReceipts);
                employees.IntegratedTaskEvent-=ObserveDeliveryArrival;
                File.WriteAllLines(Path.Combine(directory,"delivery-arrival-prechecks.txt"),arrivals);
                try{File.WriteAllText(Path.Combine(directory,"recent-states.txt"),string.Join("\n\n",history));}
                finally{compactDecisions?.Finish(directory);decisions?.Finish();}
                TestContext.WriteLine("MIXED_CLOCK_STALL "+directory);
            }
        }

        // Keep the original decision beside the later issue. No repeated policy/path query.
        // 只保留原判定输入和后续发请求边界；环形保留最新记录，不把早期容量满当成晚期无事件。
        private sealed class CompactDecisionWitness
        {
            private readonly CafePickupSideTests test;
            private readonly FieldInfo hook;
            private readonly object original;
            private readonly Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string> originalObserver;
            private readonly Queue<string> records=new Queue<string>();
            private readonly Dictionary<string,(string Stamp,string Record)> latest=new Dictionary<string,(string,string)>();
            private readonly Dictionary<string,string> decisions=new Dictionary<string,string>();
            private readonly Queue<string> keys=new Queue<string>();
            private int evicted,errors;
            private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            private static object Read(object value,string name)=>value?.GetType().GetField(name,Flags)?.GetValue(value);
            private static string Points(IReadOnlyList<Vector3> path)=>path==null?"NULL":string.Join(";",path.Select(p=>p.ToString("F9")));
            private static string Stamp(object visit)=>Read(visit,"Id")+"/g="+Read(visit,"Generation")+"/phase="+Read(visit,"State")+
                "/source="+(Read(visit,"Route") is object route?System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(route):0);
            public CompactDecisionWitness(CafePickupSideTests value)
            {
                test=value;hook=typeof(CustomerFlowController).GetField("CorridorObserver",Flags);original=hook.GetValue(test.customers);
                originalObserver=original as Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>;
                Assert.That(original==null||originalObserver!=null,Is.True,"Preserve the original observer exactly once");
                hook.SetValue(test.customers,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)Record);
            }
            private void Add(string value){records.Enqueue(value);if(records.Count>4096){records.Dequeue();evicted++;}}
            private string Context()
            {
                var visits=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(test.customers,"visits");
                var native=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(test.world.Service,"byId");
                return string.Join("\n",visits.Values.Cast<object>().Select(v=>
                {
                    var actor=Read(v,"Actor") as NavigationActor;var agent=actor!=null?actor.Agent:null;
                    var bound=agent!=null&&agent.enabled&&agent.isOnNavMesh;
                    var request=(long)Read(v,"RequestId");
                    var assignment=(QueueAssignment)Read(v,"Assignment");
                    var current=(NavigationTarget)Read(v,"CurrentSegmentTarget");var final=(NavigationTarget)Read(v,"FinalTarget");
                    var live=request!=0&&native.Contains(request)?native[request]:null;
                    return "VISIT "+Stamp(v)+" req="+request+" nativeLive="+native.Contains(request)+" exitSequence="+Read(v,"ExitSequence")+
                        " pose="+(actor!=null?actor.transform.position.ToString("F9"):"NULL")+
                        " collected="+Read(v,"HasCollected")+" pendingPickup="+Read(v,"PendingPickupSegment")+" pendingExit="+Read(v,"PendingExitSegment")+
                        " assignment="+assignment.Generation+"/slot="+assignment.SlotIndex+"@"+assignment.Position.ToString("F9")+
                        " current="+current.Position.ToString("F9")+" final="+final.Position.ToString("F9")+
                        " nativeGeneration="+Read(live,"Generation")+" nativeRevision="+Read(live,"LayoutRevision")+
                        " recovering="+Read(live,"Recovering")+" originalFailure="+Read(live,"OriginalFailure")+
                        " queue="+Points((Read(v,"Route") as Queue<Vector3>)?.ToArray())+
                        " owned="+Points(Read(v,"OwnedRoutePath") as IReadOnlyList<Vector3>)+" wasClear="+Read(v,"OwnedRouteWasClear")+
                        " issued="+Read(v,"OwnedIssuedRequest")+"/g="+Read(v,"OwnedIssuedGeneration")+
                        " agent="+(bound?"pending="+agent.pathPending+" stopped="+agent.isStopped+" status="+agent.pathStatus:"UNAVAILABLE");
                }));
            }
            private void Record(string operation,string id,string related,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                originalObserver?.Invoke(operation,id,related,target,path,obstacles,result);
                if(operation!="owned.finite-independent"&&operation!="segment.issue"&&operation!="segment.submitted"&&
                    operation!="route.actual-plan"&&operation!="owned.captured")return;
                try
                {
                    var visits=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(test.customers,"visits");
                    var subject=id!=null&&visits.Contains(id)?visits[id]:null;
                    var moving=related!=null&&visits.Contains(related)?visits[related]:null;
                    var stamp=Stamp(subject);
                    var record="frame="+Time.frameCount+" realtime="+Time.realtimeSinceStartupAsDouble+" op="+operation+
                        " id="+id+" related="+related+" stamp="+stamp+" result="+result+" target="+target.ToString("F9")+
                        "\nORIGINAL path="+Points(path)+" obstacles="+Points(obstacles)+"\n"+Context();
                    if(operation=="owned.finite-independent")
                    {
                        var key=id+"/"+related;
                        if(!latest.ContainsKey(key))
                        {keys.Enqueue(key);if(keys.Count>4096){var old=keys.Dequeue();latest.Remove(old);decisions.Remove(old);}}
                        latest[key]=(stamp,record);
                        var decision=stamp+"/provider="+Stamp(moving)+"/req="+Read(moving,"RequestId")+"/"+result;
                        if(!decisions.TryGetValue(key,out var prior)||prior!=decision){Add(record);decisions[key]=decision;}
                        return;
                    }
                    if(operation=="segment.issue")
                        foreach(var previous in latest.Values.Where(v=>v.Stamp==stamp))
                            Add("LAST_OBSERVED_ORIGINAL_BEFORE_ISSUE issueFrame="+Time.frameCount+"\n"+previous.Record);
                    Add(record);
                }
                catch(Exception error){errors++;Add("OBSERVATION_ERROR "+error);}
            }
            public void Finish(string directory)
            {
                hook.SetValue(test.customers,original);
                File.WriteAllText(Path.Combine(directory,"compact-original-decisions.txt"),
                    "BOUNDED_LATEST_RECORDS capacity=4096 evicted="+evicted+" observationErrors="+errors+
                    "\nCached records are marked LAST_OBSERVED; use their original frame/stamp, never infer an unevaluated guard.\n\n"+
                    string.Join("\n\n",records));
            }
        }

        private string MixedClockStoredState(int stage,BusinessDayController day)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            string Fields(object value)=>value==null?"NULL":string.Join(" ",value.GetType().GetFields(flags)
                .Where(f=>f.FieldType.IsPrimitive||f.FieldType.IsEnum||f.FieldType==typeof(string)||f.FieldType==typeof(Vector3))
                .Select(f=>f.Name+"="+(f.GetValue(value) is Vector3 point?point.ToString("F9"):f.GetValue(value)?.ToString()??"NULL")));
            object Read(object value,string name)=>value?.GetType().GetField(name,flags)?.GetValue(value);
            var visits=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits").Values.Cast<object>().ToArray();
            var byId=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world.Service,"byId");
            var proofs=visits.Select(v=>
            {
                var proof=Read(v,"OwnedWaitProof");var moving=Read(proof,"Moving");var request=Read(proof,"Request");
                return "visit="+Read(v,"Id")+" "+Fields(v)+" proof={"+Fields(proof)+"} contributor="+Read(moving,"Id")+
                    " contributorCurrentRequest="+Read(moving,"RequestId")+" contributorCurrentGeneration="+Read(moving,"Generation")+
                    " proofRequestInById="+(request is long requestId&&byId.Contains(requestId))+
                    " sameWaiterQueue="+ReferenceEquals(Read(proof,"WaiterRoute"),Read(v,"Route"))+
                    " employeeWaitActor="+(Read(v,"EmployeeExitWaitActor") as NavigationActor)?.ActorId;
            });
            var queues=visits
                .Select(v=>v.GetType().GetField("PickupOwner").GetValue(v) as PickupQueueService)
                .Concat(new[]{CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue")}).Where(q=>q!=null).Distinct();
            var queueRows=queues.Select(q=>"owner="+q.GetHashCode()+" canReserve="+q.CanReserve+" "+Fields(q)+" entries="+
                string.Join(";",CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(q,"reservations").Values.Cast<object>().Select(Fields))+
                " leases="+string.Join(";",q.ServiceLeases.Select(l=>l.Key+"@"+l.Value.ToString("F9"))));
            return "stage="+stage+" realtime="+Time.realtimeSinceStartupAsDouble+" day="+day.Snapshot.DayNumber+"/"+day.Snapshot.Phase+
                " speed="+time.CurrentSpeed+" resumeBlocked="+time.IsResumeBlocked+" canProgress="+owner.CanProgress+
                " worldEnabled="+world.isActiveAndEnabled+" layoutAvailable="+world.LayoutAvailable+
                " adapterEnabled="+adapter.isActiveAndEnabled+" publishedRevision="+adapter.PublishedRevision+
                " readiness="+adapter.CurrentReadiness?.CanResume+"\nCFC "+Fields(customers)+
                "\n"+UserCounterDiagnostic()+"\n"+string.Join("\n",proofs)+"\n"+string.Join("\n",queueRows)+
                "\n"+TwentyTrafficFailureTrace.Current(world,customers,employees);
        }
        // Optional stored-state witness, with no additional path or policy evaluation.
        // 只读原判定及字段；不重查路径，也不改变原候位/付款/移动。
        private sealed class PickupTargetDecisionTrace
        {
            private readonly CafePickupSideTests test;
            private readonly FieldInfo hook;
            private readonly FieldInfo pickupHook;
            private readonly List<string> records = new List<string>();
            private readonly HashSet<string> seen = new HashSet<string>();
            public string FirstUnsafeBodyWaitPublication { get; private set; }
            private int dropped;
            private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            private static object Read(object value, string name) => value?.GetType().GetField(name, Fields)?.GetValue(value);
            private static string Points(object value) => value is IEnumerable<Vector3> points
                ? string.Join(";", points.Select(p => p.ToString("F9"))) : value?.ToString() ?? "NULL";
            public PickupTargetDecisionTrace(CafePickupSideTests value)
            {
                test = value; hook = typeof(CustomerFlowController).GetField("CorridorObserver", Fields);
                pickupHook = typeof(CustomerFlowController).GetField("PickupObserver", Fields);
                Assert.That(hook.GetValue(test.customers), Is.Null);
                Assert.That(pickupHook.GetValue(test.customers), Is.Null);
                if (!Environment.GetCommandLineArgs().Contains("-pickupPublicationOnly"))
                    hook.SetValue(test.customers, (Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)Record);
                pickupHook.SetValue(test.customers, (Action<string,string,PickupQueueService,QueueAssignment>)
                    ((operation,id,owner,assignment) =>
                    {
                        if(operation=="reserve.published" || operation=="reserve.accepted" || operation=="transfer.published")
                            Record(operation,id,null,assignment.Position,null,null,"assignment="+assignment.Generation);
                    }));
            }
            private void Record(string operation, string id, string related, Vector3 target,
                IReadOnlyList<Vector3> path, Vector3[] obstacles, string result)
            {
                // Publication boundaries only: avoid formatting every movement observation.
                if (!(operation=="reserve.candidate" || operation=="reserve.published" || operation=="reserve.accepted" ||
                    operation=="transfer.published" || operation=="target.owned-validity" || operation=="target.owned-decision" ||
                    operation=="route.blocked")) return;
                var visits = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(test.customers, "visits");
                var subject = id != null && visits.Contains(id) ? visits[id] : null;
                var key = operation + "|" + id + "|" + related + "|" + target + "|" + result + "|" +
                    Read(subject, "State") + "|" + Read(subject, "Generation") + "|" + Read(subject, "RequestId");
                if (!seen.Add(key)) return;
                if (records.Count == 8192) { dropped++; return; }
                var lines = new List<string>{"frame=" + Time.frameCount + " op=" + operation + " id=" + id +
                    " related=" + related + " target=" + target.ToString("F9") + " result=" + result +
                    "\nOBSERVED path=" + Points(path) + " obstacles=" + Points(obstacles)};
                var requests = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(test.world.Service, "byId");
                foreach (System.Collections.DictionaryEntry entry in visits)
                {
                    var visit = entry.Value; var actor = (NavigationActor)Read(visit, "Actor");
                    var assignment = (QueueAssignment)Read(visit, "Assignment");
                    var owned = (QueueAssignment)Read(visit, "OwnedRouteAssignment");
                    var request = (long)Read(visit, "RequestId");
                    lines.Add("VISIT " + entry.Key + " state=" + Read(visit, "State") + " gen=" + Read(visit, "Generation") +
                        " request=" + request + " live=" + requests.Contains(request) + " pose=" + actor.transform.position.ToString("F9") +
                        " assignment=" + assignment.Generation + "@" + assignment.Position.ToString("F9") +
                        " ownedAssignment=" + owned.Generation + "@" + owned.Position.ToString("F9") +
                        " pickupAssigned=" + Read(visit, "PickupAssigned") + " inTransit=" + Read(visit, "PickupInTransit") +
                        " reserved=" + Read(visit, "ReservedPickup") + " wasClear=" + Read(visit, "OwnedRouteWasClear") +
                        " pending=" + Read(visit,"PendingPickupSegment") + " finiteProof=" + (Read(visit,"OwnedWaitProof")!=null) +
                        " ownerSame=" + ReferenceEquals(Read(visit,"PickupOwner"),Read(visit,"OwnedRouteOwner")) +
                        " revision=" + Read(visit,"OwnedRouteRevision") + "/" + test.adapter.Revision +
                        " ownedPhase=" + Read(visit, "OwnedRoutePhase") + " issued=" + Read(visit, "OwnedIssuedRequest") +
                        "/" + Read(visit, "OwnedIssuedGeneration") + " segments=" + Read(visit, "Segments") + "/" + Read(visit, "OwnedRouteSegments") +
                        " sameSource=" + ReferenceEquals(Read(visit, "Route"), Read(visit, "OwnedRouteSource")) +
                        "\n ROUTE " + Points(Read(visit, "Route")) + "\n OWNED " + Points(Read(visit, "OwnedRoutePath")));
                }
                var byActor = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(test.world.Service,"byActor");
                foreach(var employee in test.employees.Snapshot)
                {
                    var request = byActor.Contains(employee.ActorId) ? byActor[employee.ActorId] : null;
                    var original = Read(request,"Original");
                    lines.Add("STAFF "+employee.ActorId+" state="+employee.State+" handle="+employee.HandleId+
                        " request="+Read(request,"Id")+" generation="+Read(request,"Generation")+" layout="+Read(request,"LayoutRevision")+
                        " target="+(original is NavigationTarget goal ? goal.Position.ToString("F9") : "NONE"));
                }
                if (operation == "reserve.published" && Environment.GetCommandLineArgs().Contains("-pickupBodyWaitProof"))
                {
                    // Post-decision corroboration, after the original publication was copied.
                    // 此处重查仅用于测试取证，不能冒充原检查的求值结果。
                    object Call(string name, params object[] args) => typeof(CustomerFlowController).GetMethod(name, Fields).Invoke(test.customers, args);
                    foreach (System.Collections.DictionaryEntry entry in visits)
                    {
                        var visit = entry.Value;
                        if (Equals(entry.Key, id) || !(bool)Read(visit, "PendingPickupSegment") ||
                            !(bool)Read(visit, "OwnedRouteWasClear") || Read(visit, "OwnedRoutePath") == null) continue;
                        var actor = (NavigationActor)Read(visit, "Actor");
                        var remaining = (IReadOnlyList<Vector3>)Call("RemainingOwnedRoute", visit);
                        var clearance = (float)Call("RouteClearance", actor.Settings);
                        if (CustomerRoutePlanner.IsClear(remaining, new[] { target }, clearance)) continue;
                        var bodyWait = (bool)Call("CanWaitForMovingPickupBodies", visit, remaining);
                        var finite = Call("CurrentFiniteWaitRoute", visit, remaining) != null;
                        var proof = "POST_DECISION pending=" + entry.Key + " bodyWait=" + bodyWait + " retainedFinite=" + finite +
                            " newTarget=" + target.ToString("F9") + " remaining=" + Points(remaining);
                        lines.Add(proof);
                        if (bodyWait && !finite && FirstUnsafeBodyWaitPublication == null) FirstUnsafeBodyWaitPublication = proof;
                    }
                }
                records.Add(string.Join("\n", lines));
            }
            public void Finish()
            {
                hook.SetValue(test.customers, null);
                pickupHook.SetValue(test.customers, null);
                var directory = System.IO.Path.Combine("outputs", "phase15", "pickup-target-witness", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                System.IO.Directory.CreateDirectory(directory);
                var path = System.IO.Path.Combine(directory, TestContext.CurrentContext.Test.Name + ".txt");
                System.IO.File.WriteAllText(path, "records=" + records.Count + " dropped=" + dropped + "\n" + string.Join("\n\n", records));
                TestContext.WriteLine("PICKUP_TARGET_WITNESS " + path + " records=" + records.Count + " dropped=" + dropped);
            }
        }
        private bool exitTrafficObserving;
        private bool centerTrafficObserving;
        private IEnumerator PreparePhotoSevenReservationFixture()
        {
            yield return MatrixStart();
            var runtime = Find<CafeLayoutRuntime>(); var decor = Find<DecorationModeController>();
            var cash = runtime.Layout.FurnitureInstances.Single(f => f.Position == new GridPosition(1, 5));
            var machine = runtime.Layout.FurnitureInstances.Single(f => f.Position == new GridPosition(4, 6));
            yield return UserMoveCounterSupport(cash.InstanceId, new GridPosition(7, 4));
            yield return UserMoveCounterSupport(machine.InstanceId, new GridPosition(1, 7));
            var equipment = runtime.FunctionalSurfaceLayout.MountedInstances.Single(m => m.DefinitionId == "equipment.coffee-machine.01");
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment, equipment.InstanceId), Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(), Is.True);
            Assert.That(decor.TryConfirmFunctionalSurfacePreview(), Is.True);
            for (var x = 1; x < 7; x++) yield return MatrixFurniture("furniture.counter.module.01", new GridPosition(x, 4), 0);
            var support = runtime.Layout.FurnitureInstances.Single(f => f.Position == new GridPosition(1, 4));
            Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint, "pickup.point", new SurfaceSlotAddress(support.InstanceId, "slot.0")), Is.True);
            Assert.That(decor.TryConfirmFunctionalSurfacePreview(), Is.True);
            decor.ExitDecorationMode();
            yield return Until(() => owner.CanProgress && employees.Snapshot.All(e => e.State == EmployeeState.Idle));
            yield return null;
        }

        [UnityTest] public IEnumerator PhotoSevenNewParkingTargetRemovesExistingBoundedPathProof()
        {
            yield return PreparePhotoSevenReservationFixture();
            // Recorded geometry on the real authored NavMesh; this experiment does not claim live Visit ownership.
            // 复用失败现场坐标验证规划前后差异；不伪造顾客、预留、付款或移动请求。
            var from = new Vector3(1.416260f, 0, -2.310070f);
            var goal = new Vector3(-3.6f, 0, -2.7f);
            var candidate = new Vector3(-3.6f, 0, -1.6f);
            var parking = new[] { new Vector3(-2.5f, 0, -1.6f), new Vector3(-2.5f, 0, -2.7f) };
            var settings = CafeLoopRuntimeProof.Field<NavigationActor[]>(customers, "prefabs")[0].Settings;
            var clearance = (float)typeof(CustomerFlowController).GetMethod("RouteClearance", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(customers, new object[] { settings });
            var queries = 0;
            IReadOnlyList<Vector3> Query(Vector3 start, Vector3 end) { queries++; return adapter.GetCompletePath(start, end); }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var before = new CustomerRoutePlanner().Build(from, goal, parking, clearance, Query);
            var beforeQueries = queries; var beforeMs = watch.Elapsed.TotalMilliseconds;
            var after = new CustomerRoutePlanner().Build(from, goal, parking.Concat(new[] { candidate }).ToArray(), clearance, Query);
            var afterQueries = queries - beforeQueries; var afterMs = watch.Elapsed.TotalMilliseconds - beforeMs;
            IReadOnlyList<Vector3> Expand(IReadOnlyList<Vector3> route)
            {
                if (route == null) return null;
                var path = new List<Vector3> { from }; var previous = from;
                foreach (var point in route)
                {
                    var leg = adapter.GetCompletePath(previous, point);
                    if (leg == null) return null;
                    path.AddRange(leg); previous = point;
                }
                return path;
            }
            var expanded = Expand(before);
            var directory = System.IO.Path.Combine("outputs", "phase15", "reservation-path-proof", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            System.IO.Directory.CreateDirectory(directory);
            System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "geometry.txt"),
                "Photo7 formal furniture, recorded positions; no fabricated reservation.\nclearance=" + clearance.ToString("R") +
                " beforeQueries=" + beforeQueries + " afterQueries=" + afterQueries + " beforeMs=" + beforeMs + " afterMs=" + afterMs +
                "\nbefore=" + (expanded == null ? "NULL" : string.Join(";", expanded)) +
                "\nafter=" + (after == null ? "NULL" : string.Join(";", after)));
            Assert.That(before, Is.Not.Null, "The existing bounded planner must first prove a complete route without the candidate");
            Assert.That(CustomerRoutePlanner.IsClear(expanded, parking, clearance), Is.True);
            Assert.That(after, Is.Null, "Adding the candidate must remove this bounded path proof; NULL is not a mathematical no-path theorem");
            TestContext.WriteLine("PARKING_PATH_PROOF " + directory + " nativeQueries=" + beforeQueries + "/" + afterQueries);
        }

        private IEnumerator PrepareRealCanonicalParkingGoal(Action<string,string,PickupQueueService> ready)
        {
            yield return PreparePhotoSevenReservationFixture();
            CafeLoopRuntimeProof.Set(owner, "stepping", true);
            Assert.That(customers.TrySpawn(), Is.True);
            var first = customers.Snapshot.Single().VisitId;
            yield return Until(() => customers.CafeSnapshot.Single().CounterArrived);
            Assert.That(customers.TryReservePickup(first, out var reason), Is.True, reason);
            var visits = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers, "visits");
            var pickup = (PickupQueueService)visits[first].GetType().GetField("PickupOwner").GetValue(visits[first]);
            customers.ReleasePickupAssignment(first);
            var slots = CafeLoopRuntimeProof.Field<Vector3[]>(pickup, "waitSlots");
            var retained = CafeLoopRuntimeProof.Field<Vector3[]>(customers, "pickupWaitSlots");
            var projected = new[] { new Vector3(-2.5f,0,-1.6f), new Vector3(-2.5f,0,-2.7f),
                new Vector3(-3.6f,0,-2.7f), new Vector3(-3.6f,0,-1.6f) };
            // Project legal candidate slots, then obtain every reservation/payment/arrival through real flow.
            // 只投影候选站位；真实owner、generation、付款、移动和到位均由原流程取得。
            for (var i = 0; i < projected.Length; i++) { slots[i] = projected[i]; retained[i] = projected[i]; }
            CafeLoopRuntimeProof.Set(owner, "stepping", false);
            yield return Until(() => customers.PickupAssignments.Any(a => a.VisitId == first && a.Arrived) &&
                owner.Snapshot.Orders.Values.Any(o => o.CustomerId == first && o.State == OrderState.ReadyForPickup) &&
                employees.Snapshot.All(e => e.State == EmployeeState.Idle));
            Assert.That(customers.TrySpawn(), Is.True);
            var second = customers.Snapshot.Single(v => v.VisitId != first).VisitId;
            yield return Until(() => customers.PickupAssignments.Any(a => a.VisitId == second && a.Arrived) &&
                owner.Snapshot.Orders.Values.Any(o => o.CustomerId == second && o.State == OrderState.ReadyForPickup) &&
                employees.Snapshot.All(e => e.State == EmployeeState.Idle));
            CafeLoopRuntimeProof.Set(owner, "stepping", true);
            Assert.That(customers.TrySpawn(), Is.True);
            var earlier = customers.Snapshot.Single(v => v.VisitId != first && v.VisitId != second).VisitId;
            yield return Until(() => customers.CafeSnapshot.Single(v => v.VisitId == earlier).CounterArrived);
            Assert.That(customers.TryReservePickup(earlier, out reason), Is.True, reason);
            Assert.That(pickup.TryGetReservation(earlier, out var assignment), Is.True);
            Assert.That(assignment.Position, Is.EqualTo(projected[2]));
            Assert.That(assignment.Arrived, Is.False);
            Assert.That(customers.TrySpawn(), Is.True, customers.Status);
            var incoming = customers.Snapshot.Single(v => v.VisitId != first && v.VisitId != second && v.VisitId != earlier).VisitId;
            ready(earlier, incoming, pickup);
        }

        [UnityTest] public IEnumerator RealReservedPickupGoalRejectsSealingNewParkingTargetUntilRelease()
        {
            string earlier = null, incoming = null; PickupQueueService pickup = null;
            try
            {
                yield return PrepareRealCanonicalParkingGoal((a,b,q) => { earlier=a; incoming=b; pickup=q; });
                var visits = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers, "visits");
                bool Clear(Vector3 target) => (bool)typeof(CustomerFlowController).GetMethod("NewPickupTargetClear", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(customers, new object[] { visits[incoming], target });
                var tokens = customers.Capacity.GetReservations().Select(r => r.Token).ToArray();
                var paid = owner.Snapshot.PaidVisitIds.ToArray();
                Assert.That(Clear(new Vector3(2.5f,0,-2.7f)), Is.True, "An unrelated new parking candidate remains available");
                Assert.That(Clear(new Vector3(-3.6f,0,-1.6f)), Is.False,
                    "A later parking target must preserve the older genuine reserved goal, even before an owned movement request exists");
                Assert.That(pickup.TryGetReservation(incoming, out _), Is.False);
                CollectionAssert.AreEqual(tokens, customers.Capacity.GetReservations().Select(r => r.Token));
                CollectionAssert.AreEqual(paid, owner.Snapshot.PaidVisitIds);
                customers.ReleasePickupAssignment(earlier);
                Assert.That(Clear(new Vector3(-3.6f,0,-1.6f)), Is.True, "Real release retires the old movement commitment");
            }
            finally { if (owner != null) CafeLoopRuntimeProof.Set(owner, "stepping", false); }
        }

        private bool CanonicalParkingTargetClear(string incoming,Vector3 target)
        {
            var visits=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits");
            return (bool)typeof(CustomerFlowController).GetMethod("NewPickupTargetClear",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(customers,new object[]{visits[incoming],target});
        }
        [UnityTest] public IEnumerator CanonicalPickupGoalProtectionRetiresAfterRealArrival()
        {
            string earlier=null,incoming=null;PickupQueueService pickup=null;
            try
            {
                yield return PrepareRealCanonicalParkingGoal((a,b,q)=>{earlier=a;incoming=b;pickup=q;});
                var target=new Vector3(-3.6f,0,-1.6f);
                Assert.That(CanonicalParkingTargetClear(incoming,target),Is.False);
                var queries=CafeLoopRuntimeProof.Field<int>(customers,"pickupGoalQueries");
                Assert.That(CanonicalParkingTargetClear(incoming,target),Is.False);
                Assert.That(CafeLoopRuntimeProof.Field<int>(customers,"pickupGoalQueries"),Is.EqualTo(queries),"Repeated refusal performs no more native queries");
                CafeLoopRuntimeProof.Set(owner,"stepping",false);
                yield return Until(()=>pickup.TryGetReservation(earlier,out var a)&&a.Arrived);
                Assert.That(customers.Snapshot.Single(v=>v.VisitId==earlier).State,Is.EqualTo(CustomerVisitState.PickupWaiting));
                Assert.That(CanonicalParkingTargetClear(incoming,target),Is.True,"Actual arrival retires only the movement protection");
                Assert.That(pickup.TryGetReservation(earlier,out var stillOwned)&&stillOwned.Arrived,Is.True);
                Assert.That(customers.Capacity.GetCapacities().Single(c=>c.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers).Used,Is.EqualTo(4),
                    "Arrival never pretends that the four uncollected customers departed");
            }
            finally {if(owner!=null)CafeLoopRuntimeProof.Set(owner,"stepping",false);}
        }
        [UnityTest] public IEnumerator CanonicalPickupGoalBudgetUnknownCreatesNoCommitAndRealOwnerRemovalClearsIt()
        {
            string earlier=null,incoming=null;PickupQueueService pickup=null;
            try
            {
                yield return PrepareRealCanonicalParkingGoal((a,b,q)=>{earlier=a;incoming=b;pickup=q;});
                var tokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();
                var paid=owner.Snapshot.PaidVisitIds.ToArray();
                // Controlled shared-budget seam, not fabricated movement or ownership.
                // 模拟同帧预算已被前面的目标用完；所有预留与顾客仍是真实流程取得。
                CafeLoopRuntimeProof.Set(customers,"pickupGoalQueryFrame",Time.frameCount);
                CafeLoopRuntimeProof.Set(customers,"pickupGoalQueries",4000);
                var target=new Vector3(-3.6f,0,-1.6f);
                Assert.That(CanonicalParkingTargetClear(incoming,target),Is.False);
                Assert.That(CafeLoopRuntimeProof.Field<string>(customers,"pickupGoalProofStatus"),Is.EqualTo("unknown-budget"));
                Assert.That(CanonicalParkingTargetClear(incoming,target),Is.False);
                Assert.That(CafeLoopRuntimeProof.Field<int>(customers,"pickupGoalQueries"),Is.EqualTo(4000));
                Assert.That(pickup.TryGetReservation(incoming,out _),Is.False);
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(r=>r.Token));
                CollectionAssert.AreEqual(paid,owner.Snapshot.PaidVisitIds);
                Assert.That(pickup.Remove(earlier),Is.True);
                Assert.That(CanonicalParkingTargetClear(incoming,target),Is.True,
                    "The real owner wins over a stale Visit PickupAssigned flag and UNKNOWN cache");
                Assert.That(pickup.TryGetReservation(earlier,out _),Is.False,"Readonly validation cannot recreate removed ownership");
                customers.ReleasePickupAssignment(earlier);
            }
            finally {if(owner!=null)CafeLoopRuntimeProof.Set(owner,"stepping",false);}
        }
        [UnityTest] public IEnumerator CanonicalPickupGoalWithoutOriginalPathProofDoesNotKeepPriority()
        {
            string earlier=null,incoming=null;PickupQueueService pickup=null;
            try
            {
                yield return PrepareRealCanonicalParkingGoal((a,b,q)=>{earlier=a;incoming=b;pickup=q;});
                Assert.That(CanonicalParkingTargetClear(incoming,new Vector3(-3.6f,0,-1.6f)),Is.False);
                var head=pickup.Snapshot.First();
                // A controlled preexisting owner promise, deliberately outside the new CFC gate.
                // 通过真实owner API构造既有问题，不声称新产品流程会允许这次让位。
                var args=new object[]{head.VisitId,new Vector3(-3.6f,0,-1.6f),null};
                Assert.That((bool)typeof(PickupQueueService).GetMethod("TryBeginYield",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(pickup,args),Is.True);
                var actor=world.RegisteredActors.Single(a=>a.ActorId==earlier);
                var clearance=(float)typeof(CustomerFlowController).GetMethod("RouteClearance",BindingFlags.Instance|BindingFlags.NonPublic)
                    .Invoke(customers,new object[]{actor.Settings});
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var identities=typeof(CustomerFlowController).GetMethod("CurrentPickupGoalOwners",flags).Invoke(customers,null);
                var parked=(Vector3[])typeof(CustomerFlowController).GetMethod("PickupGoalObstacles",flags)
                    .Invoke(customers,new object[]{identities,earlier});
                Assert.That(new CustomerRoutePlanner().Build(actor.transform.position,new Vector3(-3.6f,0,-2.7f),parked,clearance,adapter.GetCompletePath),Is.Null,
                    "This counterexample must actually lack an original bounded path proof");
                Assert.That(CanonicalParkingTargetClear(incoming,new Vector3(-3.6f,0,-.5f)),Is.True,
                    "No original proof grants no permanent stale priority");
            }
            finally
            {
                // Keep this deliberately inconsistent prior promise held until scene teardown.
                if(owner!=null)CafeLoopRuntimeProof.Set(owner,"stepping",true);
            }
        }

        [UnityTest] public IEnumerator LaterActualExitYieldsToEarlierEmployeeDeliveryAndReleasesOriginalTokens()
            => ObserveEarlierEmployeeExitTraffic(false);
        [UnityTest] public IEnumerator CancelledEarlierEmployeeCannotKeepLaterActualExitWaiting()
            => ObserveEarlierEmployeeExitTraffic(true);
        [UnityTest] public IEnumerator DeferredActualExitReplanYieldsToEarlierEmployee()
            => ObserveEarlierEmployeeExitTraffic(false,1);
        [UnityTest] public IEnumerator DeferredSelectedExitFirstLegYieldsToEarlierEmployee()
            => ObserveEarlierEmployeeExitTraffic(false,2);
        [UnityTest] public IEnumerator NativeDeliveryArrivalPassesStrictCheckAfterMultiSubstepFrame()
            => ObserveEarlierEmployeeExitTraffic(false,0,true);

        [UnityTest] public IEnumerator CurrentReturningBodyFailureRetiresOriginalActualExitWait()
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            object Read(object value,string name)=>value==null?null:MovementRead(value,name);
            void Need(bool ok,string reason){if(!ok)Assert.Inconclusive("NOT_PROVEN: "+reason);}
            Action<string> prepSample=null,prepFailureReport=null;Action bWaitSample=null;
            IEnumerator Await(Func<bool> done,string reason)
            {for(var frame=0;frame<6000 && !done() && customers.Snapshot.All(v=>v.State!=CustomerVisitState.Blocked) && employees.Snapshot.All(e=>e.State!=EmployeeState.Blocked);frame++){prepSample?.Invoke("normal-Await-frame");bWaitSample?.Invoke();yield return null;}var reached=done();if(!reached){ObserveOldTestFailure();prepFailureReport?.Invoke(reason);}Need(reached,reason);}
            // Ordinary CR initially parks Cashier at (1,6), the proposed CM support. Move the formal job first.
            // 只旋转真实CR并等待合法rebind；不移动actor，也不把回岗方式当作负例的Native证明。
            var prepDecor=Find<DecorationModeController>();var prepRuntime=Find<CafeLayoutRuntime>();
            prepDecor.EnterDecorationMode();yield return null;
            var register=prepRuntime.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.cash-register.01");
            // 用正式装修让A从南侧接近，保留本负例原来的南侧候位/西侧领取流程 / Preserve the original approach via real layout.
            yield return UserMoveCounterSupport(register.Address.SupportFurnitureInstanceId,new GridPosition(2,2));
            Need(prepDecor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,register.InstanceId),"formal CR preview before freeing its occupied North anchor");
            for(var turn=0;turn<3;turn++)Need(prepDecor.TryRotateFunctionalSurfacePreview(),"formal CR rotation to270");
            Need(prepDecor.TryConfirmFunctionalSurfacePreview(),"formal CR Confirm retains actual body/Nav guards");prepDecor.ExitDecorationMode();
            yield return Await(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle),"real staff layout rebind after CR rotation");
            Need(employees.TryGetBinding(EmployeeRole.Cashier,out var rebound),"actual rebound Cashier binding");
            var prepGrid=(DecorationGridSpace)Read(prepDecor,"gridSpace");var prepRoot=CafeLoopRuntimeProof.Field<Transform>(prepDecor,"gridRoot");
            var westWork=prepRoot.TransformPoint(prepGrid.GetCellCenterLocal(new GridPosition(1,2)));
            Need(rebound.WorkTarget.Position.Equals(westWork),"rotation270 must create the actual West WorkTarget atcell1,2");
            var prepCashier=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier);var cashBody=world.RegisteredActors.SingleOrDefault(a=>a.ActorId==prepCashier.ActorId);
            Need(prepCashier.State==EmployeeState.Idle && cashBody!=null && cashBody.isActiveAndEnabled &&
                Vector3.Distance(cashBody.transform.position,rebound.WorkTarget.Position)<=cashBody.Settings.ArrivalDistance,"real registered Idle Cashier must occupy the new job before adding a counter atcell1,6");
            yield return AddCounters(new GridPosition(1,6),new GridPosition(6,7),new GridPosition(7,6),new GridPosition(5,7),new GridPosition(4,4),new GridPosition(5,4));
            var runtime=Find<CafeLayoutRuntime>();var decor=Find<DecorationModeController>();
            var cm=runtime.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
            var support=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(1,6));
            Need(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,cm.InstanceId),"original CM preview");
            Need(decor.TryMoveFunctionalSurfacePreview(new SurfaceSlotAddress(support.InstanceId,cm.Address.SlotId)),"original CM move");
            Need(decor.TryRotateFunctionalSurfacePreview() && decor.TryConfirmFunctionalSurfacePreview(),"one CM rotation/real Confirm");decor.ExitDecorationMode();
            yield return Await(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle),"two-side layout must really reopen");
            tools.HoldArrivals();tools.HoldCollection();time.SetNormal();
            var sides=customers.GetPickupServiceTargets(runtime.FunctionalSurfaceLayout.PickUpPoints.Single().InstanceId).ToArray();Need(sides.Length==2,"exactly two actual sides");
            var west=sides.Single(s=>s.Facing.HasValue && Vector3.Dot(s.Facing.Value,Vector3.right)>.9f);var south=sides.Single(s=>s.Facing.HasValue && Vector3.Dot(s.Facing.Value,Vector3.forward)>.9f);
            var sideField=typeof(CustomerFlowController).GetField("pickupSides",flags);var fullSides=(NavigationTarget[])sideField.GetValue(customers);
            var core=(ServiceTrafficCoordinator)Read(customers,"traffic");var entries=(System.Collections.IDictionary)Read(core,"entries");var visits=(System.Collections.IDictionary)Read(customers,"visits");
            var live=(System.Collections.IDictionary)Read(world.Service,"byActor");var byId=(System.Collections.IDictionary)Read(world.Service,"byId");
            // Test-only stored-state witness: no policy/path/progress getters, queries or extra frames.
            // 只采原字段；首WAIT与失败冷尾分开标记，缺记录不能解释成没有阻挡。
            var prepRecords=new List<string>();var prepSeen=new HashSet<string>();var prepErrors=new List<string>();
            var prepEntries=new List<(TrafficIntentKey Key,object Entry)>();var prepOwners=new HashSet<TrafficIntentKey>();var prepDropped=0;var prepReported=false;var prepStopped=false;
            string PrepKey(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
            string PrepRef(object value)=>value==null?"NULL":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value).ToString();
            string PrepPoints(object value)=>value is IEnumerable<Vector3> points?string.Join(";",points.Select(p=>p.ToString("F9"))):value==null?"NULL":"NOT_CAPTURED";
            string PrepBlock(TrafficBlocker b)=>PrepKey(b.Owner)+" resource="+b.ResourceId+" proof="+b.Proof;
            string PrepEntry(TrafficIntentKey key,object entry)
            {
                if(entry==null)return "ENTRY key="+PrepKey(key)+" NO_CORE_ENTRY";
                var request=Read(entry,"Request") as TrafficRequest;var decision=Read(entry,"Decision") as TrafficDecision;var leg=Read(entry,"Leg");var handoff=Read(entry,"PendingHandoff");
                var jobs=Read(entry,"Jobs") as System.Collections.IEnumerable;var rows=jobs==null?Array.Empty<object>():jobs.Cast<object>().ToArray();
                var edges=Read(entry,"WaitingBlockers") as IReadOnlyList<TrafficBlocker>;
                return "ENTRY key="+PrepKey(key)+" ref="+PrepRef(entry)+" registrySame="+(entries.Contains(key)&&ReferenceEquals(entries[key],entry))+
                    " decision="+decision?.Kind+" reason="+decision?.Reason+" disposition="+decision?.FailureDisposition+" requestRef="+PrepRef(request)+
                    " queued="+Read(entry,"Queued")+" prepared="+Read(entry,"Prepared")+" active="+Read(entry,"Active")+" retired="+Read(entry,"Retired")+
                    " first="+Read(entry,"FirstFrame")+" seq="+Read(entry,"RequestSequence")+" acq="+Read(entry,"AcquiredSequence")+" query="+Read(entry,"Queries")+" wait="+Read(entry,"WaitRemaining")+
                    " start="+(request==null?"NULL":request.Start.ToString("F9"))+" target="+(request==null?"NULL":request.Target.ToString("F9"))+
                    " current=["+PrepPoints(Read(entry,"CurrentPath"))+"] journey=["+PrepPoints(Read(entry,"Journey"))+"] journeyPermit="+PrepRef(Read(entry,"JourneyPermit"))+" journeyRequest="+PrepRef(Read(entry,"JourneyRequest"))+
                    " waiting=["+string.Join(";",(edges??Array.Empty<TrafficBlocker>()).Select(PrepBlock))+"] frozen=["+string.Join(";",(Read(entry,"FrozenOwners") as IEnumerable<TrafficIntentKey>??Array.Empty<TrafficIntentKey>()).Select(PrepKey))+"]"+
                    " legRef="+PrepRef(leg)+" stamp="+(Read(leg,"Stamp") is TrafficNativeStamp stamp?stamp.RequestId+"/"+stamp.LegGeneration:"NULL")+" bound="+Read(leg,"Bound")+" completed="+Read(leg,"Completed")+" legPath=["+PrepPoints(Read(leg,"Path"))+"] suffix=["+PrepPoints(Read(leg,"Suffix"))+"]"+
                    " handoff="+PrepRef(handoff)+" source="+PrepRef(Read(handoff,"Source"))+" remaining="+Read(handoff,"Remaining")+" jobIndex="+Read(entry,"JobIndex")+
                    string.Concat(rows.Select((job,index)=>"\nJOB "+index+" start="+Read(job,"start")+" goal="+Read(job,"goal")+" expanded=["+PrepPoints(Read(job,"<ExpandedPath>k__BackingField"))+"] waypoints=["+PrepPoints(Read(job,"<Waypoints>k__BackingField"))+"]"));
            }
            string PrepWorld(IEnumerable<TrafficIntentKey> keys)
            {
                var selected=keys.ToArray();var snapshot=Read(core,"snapshot") as TrafficWorldSnapshot;
                if(snapshot==null)return "WORLD NULL_OR_NOT_CAPTURED";
                var ids=new HashSet<string>(selected.Select(k=>k.ActorId));
                var result="WORLD stored="+snapshot.Version+"/"+snapshot.BodyVersion+" snapshotRef="+PrepRef(snapshot);
                foreach(var body in snapshot.Bodies.Where(b=>ids.Contains(b.Owner.ActorId)))result+="\nBODY owner="+PrepKey(body.Owner)+" exactSelectedOwner="+selected.Contains(body.Owner)+" lifeRef="+PrepRef(body.ActorLife)+" pose="+body.Position.ToString("F9")+" enabled="+body.Enabled+" active="+body.ActorActive+" finite="+body.IndependentlyProgressing+" remaining="+body.RemainingSeconds+" radius="+body.Radius+" margin="+body.SafetyMargin;
                foreach(var parking in snapshot.Parking.Where(p=>ids.Contains(p.Owner.ActorId)))result+="\nPARKING owner="+PrepKey(parking.Owner)+" resource="+parking.ResourceId+" pose="+parking.Position.ToString("F9")+" canonicalRef="+PrepRef(parking.CanonicalOwner)+" version="+parking.OwnerVersion+" businessRef="+PrepRef(parking.BusinessOwner)+" obligationRef="+PrepRef(parking.ReleaseObligation)+" checkpointRef="+PrepRef(parking.SemanticCheckpoint);
                foreach(var id in ids)
                {
                    var native=live.Contains(id)?live[id]:null;var runnerRow=((System.Collections.IEnumerable)Read(employees,"employees")).Cast<object>().FirstOrDefault(e=>(string)Read(e,"Id")==id);var runner=Read(runnerRow,"Runner");var move=Read(runner,"moveLeg");
                    result+="\nNATIVE actor="+id+" rawRef="+PrepRef(native)+" request="+Read(native,"Id")+" generation="+Read(native,"Generation")+" originalTarget="+(Read(native,"Original") is NavigationTarget original?original.Position.ToString("F9"):"NULL")+" noProgress="+Read(native,"NoProgressSeconds")+" retry="+Read(native,"RetryCount")+" recovering="+Read(native,"Recovering")+
                        " employeeFailure="+Read(runnerRow,"Failure")+" runnerState="+Read(runner,"state")+" runnerFailure="+Read(runner,"failure")+" handle="+Read(runner,"handleId")+" moveRef="+PrepRef(move)+" moveRequest="+Read(move,"RequestId")+" awaitingTraffic="+Read(move,"AwaitingTraffic")+" moveTarget="+(Read(move,"Target") is NavigationTarget moveTarget?moveTarget.Position.ToString("F9"):"NULL");
                }
                return result;
            }
            // Two stored checkpoints only; they neither replay a predicate nor notify/requeue its owner.
            // 两条独立one-shot保留当时字段；不把失败冷尾当作中间operand，也不扩大首WAIT cap8。
            var prepArrivalSeen=false;var prepStoppedBodySeen=false;var prepArrivalFrame=-1;
            TrafficIntentKey prepArrivalKey=default;object prepArrivalEntry=null;
            (TrafficIntentKey Key,object Entry)[] prepCheckpointWaiters=Array.Empty<(TrafficIntentKey Key,object Entry)>();
            string PrepStoredKey(object checkpointEntry)=>Read(checkpointEntry,"Key") is TrafficIntentKey checkpointKey?PrepKey(checkpointKey):"UNKNOWN_KEY";
            string PrepCheckpointFacts()
            {
                var checkpointHandoff=Read(prepArrivalEntry,"PendingHandoff");var checkpointDependencies=Read(checkpointHandoff,"Dependencies") as System.Collections.IDictionary;
                var checkpointWakeups=Read(checkpointHandoff,"CompletedBodyWakeups") as System.Collections.IEnumerable;
                var checkpointWoken=checkpointWakeups==null?null:checkpointWakeups.Cast<object>().ToArray();
                var checkpointFacts="OWNER "+PrepEntry(prepArrivalKey,prepArrivalEntry)+" legLifeRef="+PrepRef(Read(Read(prepArrivalEntry,"Leg"),"ActorLife"))+
                    "\nHANDOFF ref="+PrepRef(checkpointHandoff)+" remaining="+(checkpointHandoff==null?"UNKNOWN":Read(checkpointHandoff,"Remaining"))+
                    " dependencyCount="+(checkpointDependencies==null?"UNKNOWN":checkpointDependencies.Count.ToString())+" completedBodyWakeupCount="+(checkpointWoken==null?"UNKNOWN":checkpointWoken.Length.ToString());
                if(checkpointDependencies!=null)foreach(var checkpointDependent in checkpointDependencies.Keys.Cast<object>())checkpointFacts+="\nDEPENDENCY entry="+PrepRef(checkpointDependent)+" key="+PrepStoredKey(checkpointDependent)+" resources=["+string.Join(";",(IEnumerable<string>)checkpointDependencies[checkpointDependent])+"]";
                if(checkpointWoken!=null)foreach(var checkpointWokenEntry in checkpointWoken)checkpointFacts+="\nCOMPLETED_BODY_WAKEUP entry="+PrepRef(checkpointWokenEntry)+" key="+PrepStoredKey(checkpointWokenEntry);
                foreach(var checkpointWaiter in prepCheckpointWaiters)checkpointFacts+="\nORIGINAL_WAITER dependencyContainsSameEntry="+(checkpointDependencies==null?"UNKNOWN":checkpointDependencies.Contains(checkpointWaiter.Entry).ToString())+
                    " wakeupContainsSameEntry="+(checkpointWoken==null?"UNKNOWN":checkpointWoken.Any(w=>ReferenceEquals(w,checkpointWaiter.Entry)).ToString())+"\n"+PrepEntry(checkpointWaiter.Key,checkpointWaiter.Entry);
                return checkpointFacts+"\n"+PrepWorld(prepCheckpointWaiters.Select(p=>p.Key).Append(prepArrivalKey));
            }
            void PrepArrivalCheckpoint(string checkpointActor)
            {
                if(prepStopped || prepArrivalSeen)return;try
                {
                    var checkpointVisit=visits.Contains(checkpointActor)?visits[checkpointActor]:null;var checkpointMove=Read(checkpointVisit,"TrafficMove");
                    if(!(Read(checkpointMove,"Intent") is TrafficIntentKey checkpointKey) || !prepOwners.Contains(checkpointKey) || !entries.Contains(checkpointKey))return;
                    var checkpointEntry=entries[checkpointKey];var checkpointSource=Read(checkpointMove,"Source");
                    if(checkpointKey.ActorId!=checkpointActor || !(Read(checkpointEntry,"Key") is TrafficIntentKey checkpointStoredKey) || !checkpointStoredKey.Equals(checkpointKey) ||
                        checkpointSource==null || !ReferenceEquals(checkpointSource,Read(checkpointEntry,"JourneyPermit")))return;
                    prepArrivalSeen=true;prepArrivalFrame=Time.frameCount;prepArrivalKey=checkpointKey;prepArrivalEntry=checkpointEntry;prepCheckpointWaiters=prepEntries.ToArray();
                    TestContext.WriteLine("STORED_CHECKPOINT_1_ARRIVAL status=OBSERVED_CANONICAL_HOOK frame="+Time.frameCount+" operation=arrival.canonical-committed moveRef="+PrepRef(checkpointMove)+" moveSourceRef="+PrepRef(checkpointSource)+" moveNativeGeneration="+Read(checkpointMove,"NativeGeneration")+"\n"+PrepCheckpointFacts());
                }catch(Exception checkpointError){prepArrivalSeen=true;if(prepErrors.Count<8)prepErrors.Add("UNKNOWN arrival checkpoint "+checkpointError);TestContext.WriteLine("STORED_CHECKPOINT_1_ARRIVAL status=UNKNOWN_OBSERVATION_ERROR "+checkpointError);}
            }
            void PrepStoppedBodyCheckpoint()
            {
                if(!prepArrivalSeen || prepStoppedBodySeen || prepArrivalEntry==null || Time.frameCount<=prepArrivalFrame)return;try
                {
                    var checkpointSnapshot=Read(core,"snapshot") as TrafficWorldSnapshot;var checkpointBody=checkpointSnapshot?.Bodies.SingleOrDefault(b=>b.Owner.Equals(prepArrivalKey));
                    var checkpointLeg=Read(prepArrivalEntry,"Leg");var checkpointRegistered=entries.Contains(prepArrivalKey)&&ReferenceEquals(entries[prepArrivalKey],prepArrivalEntry);
                    var checkpointLiveCompleted=checkpointRegistered && Read(core,"closed") is bool checkpointClosed && !checkpointClosed && Read(prepArrivalEntry,"Request")!=null && Read(prepArrivalEntry,"Retired") is bool checkpointRetired && !checkpointRetired && Read(checkpointLeg,"Completed") is bool checkpointCompleted && checkpointCompleted;
                    if(checkpointLiveCompleted && checkpointBody!=null && ReferenceEquals(checkpointBody.ActorLife,Read(checkpointLeg,"ActorLife")) && checkpointBody.IndependentlyProgressing)return;
                    prepStoppedBodySeen=true;
                    TestContext.WriteLine("STORED_CHECKPOINT_2_NORMAL_AWAIT status="+(checkpointLiveCompleted && checkpointBody!=null && ReferenceEquals(checkpointBody.ActorLife,Read(checkpointLeg,"ActorLife"))?"OBSERVED_SAME_COMPLETED_OWNER_NOT_PROGRESSING":"UNKNOWN_MISSING_EXPIRED_OR_REPLACED_OWNER_BODY")+
                        " frame="+Time.frameCount+" arrivalFrame="+prepArrivalFrame+" sameRegisteredEntry="+checkpointRegistered+" liveCompletedRaw="+checkpointLiveCompleted+" bodyPresent="+(checkpointBody!=null)+"\n"+PrepCheckpointFacts());
                }catch(Exception checkpointError){prepStoppedBodySeen=true;if(prepErrors.Count<8)prepErrors.Add("UNKNOWN stopped-body checkpoint "+checkpointError);TestContext.WriteLine("STORED_CHECKPOINT_2_NORMAL_AWAIT status=UNKNOWN_OBSERVATION_ERROR "+checkpointError);}
            }
            prepSample=stage=>
            {
                if(prepStopped)return;try
                {
                    if(stage=="normal-Await-frame")PrepStoppedBodyCheckpoint();
                    var workerIds=new HashSet<string>(((System.Collections.IEnumerable)Read(employees,"employees")).Cast<object>().Select(e=>(string)Read(e,"Id")));
                    foreach(var key in entries.Keys.Cast<TrafficIntentKey>().Where(k=>workerIds.Contains(k.ActorId)).ToArray())
                    {
                        var entry=entries[key];var decision=Read(entry,"Decision") as TrafficDecision;
                        if(decision?.Kind!=TrafficDecisionKind.WaitingForTraffic || (bool)Read(entry,"Retired"))continue;
                        var blockers=decision.Blockers.ToArray();var signature=PrepKey(key)+"/entry="+PrepRef(entry)+"/reason="+decision.Reason+"/"+string.Join(";",blockers.Select(PrepBlock));
                        if(prepSeen.Contains(signature))continue;if(prepRecords.Count==8){prepDropped++;continue;}prepSeen.Add(signature);prepEntries.Add((key,entry));foreach(var blocker in blockers)prepOwners.Add(blocker.Owner);
                        var related=blockers.Select(b=>b.Owner).Append(key).Distinct().ToArray();
                        prepRecords.Add("FIRST_DISTINCT_STORED_WAIT frame="+Time.frameCount+" stage="+stage+"\n"+PrepEntry(key,entry)+"\n"+string.Join("\n",blockers.Select(b=>PrepEntry(b.Owner,entries.Contains(b.Owner)?entries[b.Owner]:null)))+"\n"+PrepWorld(related));
                    }
                }catch(Exception error){if(prepErrors.Count<8)prepErrors.Add("UNKNOWN sample "+error);}
            };
            prepFailureReport=reason=>
            {
                if(prepReported)return;prepReported=true;try
                {
                    TestContext.WriteLine("CURRENT_RETURN_A_PREPARATION_WITNESS status="+(prepErrors.Count>0?"UNKNOWN_OBSERVATION_ERRORS":prepDropped>0?"UNKNOWN_CAPPED_WITH_POSITIVE_RECORDS":prepRecords.Count==0?"NOT_OBSERVED":"OBSERVED_STORED_WAIT")+" reason="+reason+" samples="+prepRecords.Count+" cap=8 droppedObservationCount="+prepDropped+" errors="+string.Join(";",prepErrors)+"\n"+string.Join("\n\n",prepRecords));
                    TestContext.WriteLine("CAPTURED_AT_AWAIT_FAILURE frame="+Time.frameCount+" (current stored facts, not original predicate replay)\n"+string.Join("\n",prepEntries.Select(p=>PrepEntry(p.Key,p.Entry)))+"\n"+string.Join("\n",prepOwners.Select(k=>PrepEntry(k,entries.Contains(k)?entries[k]:null)))+"\n"+PrepWorld(prepEntries.Select(p=>p.Key).Concat(prepOwners)));
                }catch(Exception error){TestContext.WriteLine("CURRENT_RETURN_A_PREPARATION UNKNOWN report "+error);}
            };
            var prepEmployeeHook=typeof(EmployeeFlowController).GetField("MovementObserver",flags);var prepPriorEmployee=prepEmployeeHook.GetValue(employees) as Action<string,string,string>;
            var prepCorridorHook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);var prepPriorCorridor=prepCorridorHook.GetValue(customers) as Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>;
            Action<string,string,string> prepEmployee=(id,operation,message)=>{prepPriorEmployee?.Invoke(id,operation,message);prepSample("EFC."+operation);};
            Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string> prepCorridor=(operation,id,other,target,path,occupied,result)=>{prepPriorCorridor?.Invoke(operation,id,other,target,path,occupied,result);if(operation=="arrival.canonical-committed")PrepArrivalCheckpoint(id);prepSample("CFC."+operation);};
            var prepPriorCleanup=controlledHandoffCleanup;Action<string> prepCleanup=null;
            prepCleanup=reason=>{if(prepStopped)return;prepStopped=true;
                try{if(ReferenceEquals(prepEmployeeHook.GetValue(employees),prepEmployee))prepEmployeeHook.SetValue(employees,prepPriorEmployee);else prepErrors.Add("UNKNOWN EFC hook replaced");if(ReferenceEquals(prepCorridorHook.GetValue(customers),prepCorridor))prepCorridorHook.SetValue(customers,prepPriorCorridor);else prepErrors.Add("UNKNOWN CFC hook replaced");}catch(Exception error){prepErrors.Add("UNKNOWN restore "+error);}
                if(ReferenceEquals(controlledHandoffCleanup,prepCleanup))controlledHandoffCleanup=prepPriorCleanup;prepSample=null;prepFailureReport=null;
                if(!prepArrivalSeen)TestContext.WriteLine("STORED_CHECKPOINT_1_ARRIVAL status=UNKNOWN_NOT_OBSERVED reason="+reason);if(!prepStoppedBodySeen)TestContext.WriteLine("STORED_CHECKPOINT_2_NORMAL_AWAIT status=UNKNOWN_NOT_OBSERVED reason="+reason);
                TestContext.WriteLine("CURRENT_RETURN_A_PREPARATION_CLEANUP reason="+reason+" observationErrors="+string.Join(";",prepErrors));prepPriorCleanup?.Invoke(reason);};
            controlledHandoffCleanup=prepCleanup;prepEmployeeHook.SetValue(employees,prepEmployee);prepCorridorHook.SetValue(customers,prepCorridor);
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);Need(customers.TrySpawn(),"A actual admission");var first=customers.Snapshot.Single().VisitId;
            var aCounter=customers.CounterAssignments.Single().Position;
            Need((aCounter-south.Position).sqrMagnitude<(aCounter-west.Position).sqrMagnitude,"A must genuinely approach closer to South, with no distance tie");
            TestContext.WriteLine("CURRENT_RETURN_A_APPROACH counter="+aCounter+" southDistance="+Vector3.Distance(aCounter,south.Position)+" westDistance="+Vector3.Distance(aCounter,west.Position));
            yield return Await(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==first && o.State==OrderState.ReadyForPickup) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle) && customers.PickupAssignments.Single().Arrived,"A original Ready/arrived/Idle");
            prepCleanup("A-preparation-complete");
            var queue=Read(customers,"pickupQueue") as PickupQueueService;var actualSlots=Read(queue,"waitSlots") as Vector3[];
            Need(queue!=null && actualSlots!=null && actualSlots.Length>=2,"A real paid/Ready flow must have built the actual two-slot pickup queue");var waitSlots=actualSlots.ToArray();
            for(var frame=0;frame<6000 && !customers.CanCollectPickup(first);frame++)
            {try{sideField.SetValue(customers,new[]{west});customers.TryMoveToPickupService(first,out _);}finally{sideField.SetValue(customers,fullSides);}yield return null;}
            Need(customers.CanCollectPickup(first) && (bool)Read(visits[first],"ServiceArrived"),"A genuine west ServiceArrived/CanCollect");
            Need(queue.TryGetServiceLease(first,out var lease) && lease.Equals(west.Position) && queue.TryCaptureReservation(first,out var aClaim) && aClaim.IsCurrentCollectionLease,"A real west collection lease");
            var firstVisit=visits[first];var firstBody=(NavigationActor)Read(firstVisit,"Actor");var firstAdmission=Read(firstVisit,"Admission");var display=Find<PickupCupDisplay>();
            var firstDelivery=owner.Snapshot.Delivery.Single(d=>d.VisitId==first);var firstOrder=firstDelivery.Key;var firstCup=display.VisibleOrders[firstOrder];
            var firstTotal=customers.Capacity.GetReservations().Single(t=>t.OwnerId==first && t.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers).Token;
            var serviceMove=Read(firstVisit,"TrafficMove");Need(serviceMove!=null,"A original service movement exists");
            var serviceKey=(TrafficIntentKey)Read(serviceMove,"Intent");var serviceLeg=entries.Contains(serviceKey)?Read(entries[serviceKey],"Leg"):null;
            Need(serviceKey.IntentId=="pickup-service" && serviceLeg!=null && (bool)Read(serviceLeg,"Bound") && (bool)Read(serviceLeg,"Completed") &&
                ((TrafficNativeStamp)Read(serviceLeg,"Stamp")).RequestId>0 && ReferenceEquals(Read(serviceLeg,"ActorLife"),firstBody) &&
                Vector3.Distance(firstBody.transform.position,west.Position)<=firstBody.Settings.ArrivalDistance,"A real same-life Bound service Native actually arrived");
            var errors=new List<string>();var exiting=new HashSet<string>();var observers=new List<CafeActualExitObserver>();var observing=true;NavigationActor obstruction=null;
            string second=null,employeeId=null;object secondVisit=null,secondAdmission=null,workerEntry=null,exitEntry=null;NavigationActor secondBody=null,employee=null;
            CafeOrderKey secondOrder=default;GameObject secondCup=null;AnimalCafe.Capacity.CapacityToken secondTotal=null;
            TrafficIntentKey employeeKey=default,exitKey=default;TrafficDecision employeeSource=null;TrafficNativeStamp workerStamp=default;long handle=0;
            Vector3[] currentPath=null;bool deliveredSouth=false,failed=false,noProgress=false,actualExitArrival=false,accountingCaptured=false;TrafficDecision terminal=null;GameObject heldCup=null;
            var employeeHook=typeof(EmployeeFlowController).GetField("MovementObserver",flags);var oldEmployee=employeeHook.GetValue(employees) as Action<string,string,string>;
            var preparationHook=typeof(CustomerFlowController).GetField("CollectedExitPreparationObserver",flags);var oldPreparation=preparationHook.GetValue(customers) as Action<string>;
            var corridorHook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);var oldCorridor=corridorHook.GetValue(customers) as Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>;
            int firstFrame=0,lastQueries=0;long sequence=0;float finite=0,lastWait=0;object frozen=null,resources=null;
            var bWaitCaptured=false;
            bWaitSample=()=>{if(bWaitCaptured)return;try
            {
                var bWaitStaff=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);
                var bWaitingKeys=entries.Keys.Cast<TrafficIntentKey>().Where(k=>k.ActorId==bWaitStaff.ActorId && k.IntentId=="employee-pickup-journey" &&
                    !(bool)Read(entries[k],"Retired") && (Read(entries[k],"Decision") as TrafficDecision)?.Kind==TrafficDecisionKind.WaitingForTraffic).ToArray();
                if(bWaitingKeys.Length==1)
                {bWaitCaptured=true;var bWaitKey=bWaitingKeys[0];var bWaitEntry=entries[bWaitKey];var bWaitDecision=(TrafficDecision)Read(bWaitEntry,"Decision");var bWaitOwners=bWaitDecision.Blockers.Select(b=>b.Owner).Distinct().ToArray();
                    TestContext.WriteLine("CURRENT_RETURN_B_FIRST_STORED_WAIT frame="+Time.frameCount+" currentVisit="+second+" employeeHandle="+bWaitStaff.HandleId+" (normal Await stored facts, no original predicate replay)\n"+PrepEntry(bWaitKey,bWaitEntry)+"\n"+string.Join("\n",bWaitOwners.Select(k=>PrepEntry(k,entries.Contains(k)?entries[k]:null)))+"\n"+PrepWorld(bWaitOwners.Append(bWaitKey)));}
                else if(bWaitingKeys.Length>1){bWaitCaptured=true;TestContext.WriteLine("CURRENT_RETURN_B_WAIT UNKNOWN_MULTIPLE_CURRENT_ENTRIES keys="+string.Join(";",bWaitingKeys.Select(PrepKey)));}
            }catch(Exception bWaitError){bWaitCaptured=true;TestContext.WriteLine("CURRENT_RETURN_B_WAIT UNKNOWN_OBSERVATION_ERROR "+bWaitError);}};
            var returningPathMismatchLogged=false;
            bool ReturningBound()
            {
                var staff=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);if(staff.State!=EmployeeState.Returning || !staff.Delivered || !live.Contains(staff.ActorId))return false;
                var row=((System.Collections.IEnumerable)Read(employees,"employees")).Cast<object>().Single(e=>(string)Read(e,"Id")==staff.ActorId);var runner=(EmployeeTaskRunner)Read(row,"Runner");
                if(runner.CaptureCheckpoint().VisitId!=second)return false;var movement=Read(Read(row,"Port"),"trafficMove");if(movement==null || (string)Read(movement,"Resource")!="selected-home")return false;
                var key=(TrafficIntentKey)Read(movement,"Intent");if(key.IntentId!="employee-pickup-journey" || !entries.Contains(key))return false;
                var entry=entries[key];var leg=Read(entry,"Leg");var source=Read(entry,"JourneyPermit") as TrafficDecision;var proof=Read(leg,"Proof") as TrafficRouteProof;
                if((bool)Read(entry,"Retired") || source==null || source.AcquiredSequence<=0 || !ReferenceEquals(Read(movement,"Source"),source) || proof?.ResourceId!="selected-home" ||
                    !source.ProofRoutes.Any(p=>ReferenceEquals(p,proof)) || !(bool)Read(leg,"Committed") || !(bool)Read(leg,"Bound") || (bool)Read(leg,"Completed"))return false;
                var body=(NavigationActor)Read(row,"Actor");var stamp=(TrafficNativeStamp)Read(leg,"Stamp");var native=Read(runner,"moveLeg");
                if(!ReferenceEquals(Read(leg,"ActorLife"),body) || stamp.RequestId<=0 || stamp.LegGeneration!=(int)Read(movement,"NativeGeneration") ||
                    !byId.Contains(stamp.RequestId) || (long)Read(live[staff.ActorId],"Id")!=stamp.RequestId || native==null ||
                    (long)Read(native,"RequestId")!=stamp.RequestId || (int)Read(native,"NativeLegGeneration")!=stamp.LegGeneration)return false;
                var request=Read(entry,"Request") as TrafficRequest;var path=Read(leg,"Path") as Vector3[];
                if(Environment.GetCommandLineArgs().Contains("-p15ReturningTrace") && !returningPathMismatchLogged && request?.ResourceId=="selected-home" && path!=null && path.Length>=2 &&
                    !path.SequenceEqual((Vector3[])Read(entry,"CurrentPath")))
                {returningPathMismatchLogged=true;TestContext.WriteLine("RETURNING_PATH_PREMISE sameJob=True committed=True bound=True liveStamp="+stamp.RequestId+" raw=["+PrepPoints(path)+"] current=["+PrepPoints(Read(entry,"CurrentPath"))+"] requestStart="+request.Start.ToString("F9")+" target="+request.Target.ToString("F9"));}
                if(request?.ResourceId!="selected-home" || path==null || path.Length<2)return false;
                // Native投影corners与真实端点连接段必须一起核对 / Verify complete guarded geometry, retain raw corners.
                var guarded=new List<Vector3>();if(request.Start!=path[0])guarded.Add(request.Start);guarded.AddRange(path);
                if(request.Target!=path[path.Length-1])guarded.Add(request.Target);
                if(!guarded.SequenceEqual((Vector3[])Read(entry,"CurrentPath")))return false;
                if(employeeSource!=null)return key.Equals(employeeKey) && ReferenceEquals(source,employeeSource) && stamp.Equals(workerStamp) && staff.HandleId==handle && ReferenceEquals(body,employee);
                employeeId=staff.ActorId;employee=body;employeeKey=key;employeeSource=source;workerEntry=entry;workerStamp=stamp;handle=staff.HandleId;currentPath=path.ToArray();return true;
            }
            void ExitFacts()
            {
                if(exitEntry==null || !accountingCaptured)return;var keys=entries.Keys.Cast<TrafficIntentKey>().Where(k=>k.ActorId==first && k.IntentId=="actual-exit").ToArray();
                Assert.That(keys,Is.EqualTo(new[]{exitKey}));Assert.That(entries[exitKey],Is.SameAs(exitEntry));Assert.That(Read(exitEntry,"FirstFrame"),Is.EqualTo(firstFrame));
                Assert.That(Read(exitEntry,"RequestSequence"),Is.EqualTo(sequence));Assert.That((int)Read(exitEntry,"Queries"),Is.GreaterThanOrEqualTo(lastQueries));lastQueries=(int)Read(exitEntry,"Queries");
                Assert.That(Read(exitEntry,"FiniteLimit"),Is.EqualTo(finite));Assert.That(Read(exitEntry,"FrozenOwners"),Is.SameAs(frozen));Assert.That(Read(exitEntry,"FrozenResources"),Is.SameAs(resources));
                var remaining=(float)Read(exitEntry,"WaitRemaining");Assert.That(remaining,Is.LessThanOrEqualTo(lastWait));lastWait=remaining;
                var decision=core.GetDecision(exitKey);if(decision.FailureDisposition==TrafficFailureDisposition.Terminal && terminal==null)terminal=decision;
                if(terminal!=null)Assert.That(decision.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.Terminal),"ordinary polling cannot revive the original Terminal");
            }
            void Business()
            {
                Assert.That(owner.Snapshot.Orders[firstOrder].State,Is.EqualTo(OrderState.Completed));Assert.That(owner.Snapshot.Orders[secondOrder].State,Is.EqualTo(OrderState.ReadyForPickup));
                Assert.That(display.VisibleOrders[secondOrder],Is.SameAs(secondCup));Assert.That(secondCup!=null && secondCup.activeInHierarchy,Is.True);
                Assert.That(visits[second],Is.SameAs(secondVisit));Assert.That(Read(secondVisit,"Actor"),Is.SameAs(secondBody));Assert.That(Read(secondVisit,"Admission"),Is.SameAs(secondAdmission));
                Assert.That(owner.Snapshot.PaidVisitIds.Contains(second),Is.True);Assert.That(owner.Snapshot.Delivery.Any(d=>d.Key.Equals(secondOrder)),Is.True);
                var removed=!visits.Contains(first);if(removed)Assert.That(actualExitArrival,Is.True,"Only the real Native Exit may precede OnDestroy's receipt");
                foreach(var pair in new[]{(Id:first,Token:firstTotal),(Id:second,Token:secondTotal)})
                    Assert.That(customers.Capacity.GetReservations().Single(t=>ReferenceEquals(t.Token,pair.Token) && t.OwnerId==pair.Id).State,
                        Is.EqualTo(pair.Id==first && (removed || exiting.Contains(first))?AnimalCafe.Capacity.ReservationState.Released:AnimalCafe.Capacity.ReservationState.Occupied));
                if(!removed){Assert.That(visits[first],Is.SameAs(firstVisit));Assert.That(Read(firstVisit,"Admission"),Is.SameAs(firstAdmission));Assert.That(Read(firstVisit,"Actor"),Is.SameAs(firstBody));
                    Assert.That(firstBody.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="CustomerHeldCup").gameObject,Is.SameAs(heldCup));}
            }
            Action<string,string,string> observeEmployee=(id,operation,message)=>{oldEmployee?.Invoke(id,operation,message);if(observing && id==employeeId && operation=="callback" && message.Contains("phase=Returning ") && message.Contains("request="+workerStamp.RequestId+" ") && message.Contains("handle="+handle+" ") && message.Contains("status=Failed")){failed=true;TestContext.WriteLine("CURRENT_RETURN_NATIVE_FAILURE "+message);}};
            Action<string> observePreparation=message=>{oldPreparation?.Invoke(message);if(observing && exitEntry!=null)try{ExitFacts();}catch(Exception error){errors.Add("exit preparation: "+error);}};
            Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string> observeCorridor=(operation,id,other,target,path,occupied,result)=>
            {oldCorridor?.Invoke(operation,id,other,target,path,occupied,result);if(observing && id==first && operation=="segment.callback-result" && result.Contains("status=Arrived") && Vector3.Distance(target,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position)<=firstBody.Settings.ArrivalDistance)actualExitArrival=true;};
            Action<CafeTaskEvent> observeDelivery=value=>{if(value.Kind!=CafeTaskEventKind.DeliveryArrived || value.Checkpoint.VisitId!=second)return;try{var row=((System.Collections.IEnumerable)Read(employees,"employees")).Cast<object>().Single(e=>(string)Read(e,"Id")==value.ActorId);var runner=(EmployeeTaskRunner)Read(row,"Runner");deliveredSouth=runner.DeliveryTarget.HasValue && runner.DeliveryTarget.Value.Position.Equals(south.Position) && Vector3.Distance(((NavigationActor)Read(row,"Actor")).transform.position,south.Position)<=((NavigationActor)Read(row,"Actor")).Settings.ArrivalDistance;}catch(Exception error){errors.Add("delivery: "+error);}};
            var priorCleanup=controlledHandoffCleanup;Action<string> cleanup=null;var cleaned=false;
            cleanup=reason=>{if(cleaned)return;cleaned=true;observing=false;foreach(var o in observers)if(o!=null)o.Receipt=null;employees.IntegratedTaskEvent-=observeDelivery;
                if(ReferenceEquals(employeeHook.GetValue(employees),observeEmployee))employeeHook.SetValue(employees,oldEmployee);else errors.Add("UNKNOWN movement hook ownership");
                if(ReferenceEquals(preparationHook.GetValue(customers),observePreparation))preparationHook.SetValue(customers,oldPreparation);else errors.Add("UNKNOWN preparation hook ownership");
                if(ReferenceEquals(corridorHook.GetValue(customers),observeCorridor))corridorHook.SetValue(customers,oldCorridor);else errors.Add("UNKNOWN corridor hook ownership");
                if(obstruction!=null){world.Unregister(obstruction);obstruction.gameObject.SetActive(false);UnityEngine.Object.Destroy(obstruction.gameObject);obstruction=null;}
                if(ReferenceEquals(controlledHandoffCleanup,cleanup))controlledHandoffCleanup=priorCleanup;priorCleanup?.Invoke(reason);};
            employeeHook.SetValue(employees,observeEmployee);preparationHook.SetValue(customers,observePreparation);corridorHook.SetValue(customers,observeCorridor);employees.IntegratedTaskEvent+=observeDelivery;controlledHandoffCleanup=cleanup;
            try
            {
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);Need(customers.TrySpawn(),"B normal full-side admission");second=customers.Snapshot.Single(v=>v.VisitId!=first).VisitId;
                secondVisit=visits[second];secondBody=(NavigationActor)Read(secondVisit,"Actor");secondAdmission=Read(secondVisit,"Admission");secondTotal=customers.Capacity.GetReservations().Single(t=>t.OwnerId==second && t.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers).Token;
                foreach(var id in new[]{first,second})
                {var body=(NavigationActor)Read(visits[id],"Actor");var token=id==first?firstTotal:secondTotal;var o=body.gameObject.AddComponent<CafeActualExitObserver>();o.VisitId=id;o.Receipt=(departed,pose)=>{if(!observing)return;try{Assert.That(departed,Is.EqualTo(first),"B must keep its original Ready cup");Assert.That(actualExitArrival,Is.True);Assert.That(Vector3.Distance(pose,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position),Is.LessThanOrEqualTo(body.Settings.ArrivalDistance));Assert.That(customers.Capacity.GetReservations().Single(t=>ReferenceEquals(t.Token,token)).State,Is.EqualTo(AnimalCafe.Capacity.ReservationState.Released));Assert.That(exiting.Add(departed),Is.True);}catch(Exception error){errors.Add("actual Exit: "+error);}};observers.Add(o);}
                yield return Await(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==second && o.State==OrderState.ReadyForPickup) && ReturningBound(),"B same-job current Returning selected-home Bound/live");
                Need(deliveredSouth,"B actual South delivery arrival");Need(queue.TryCaptureReservation(second,out var bClaim) && bClaim.Assignment.SlotIndex==1 && bClaim.Assignment.Position.Equals(waitSlots[1]),"B actual immutable waitSlots[1], not a guessed cell");
                secondOrder=owner.Snapshot.Orders.Single(p=>p.Value.CustomerId==second).Key;secondCup=display.VisibleOrders[secondOrder];
                tools.ResumeCollection();yield return Await(()=>(bool)Read(firstVisit,"HasCollected"),"A genuine collection");tools.HoldCollection();
                var exitKeys=entries.Keys.Cast<TrafficIntentKey>().Where(k=>k.ActorId==first && k.IntentId=="actual-exit").ToArray();Need(exitKeys.Length==1,"one original actual-exit source");exitKey=exitKeys[0];exitEntry=entries[exitKey];
                var decision=core.GetDecision(exitKey);for(var frame=0;frame<6000 && decision.Kind==TrafficDecisionKind.DeferredByBudget && ReturningBound();frame++){yield return null;decision=core.GetDecision(exitKey);}
                Need(decision.Kind==TrafficDecisionKind.WaitingForTraffic && decision.Reason=="finite-owner-release" && ReturningBound(),"A must really WAIT on that CURRENT Returning leg; a safe Grant/detour is not this negative");
                Need(decision.Blockers.Any(b=>b.Owner.Equals(employeeKey) && b.ResourceId=="selected-home"),"original current-home resource edge");
                frozen=Read(exitEntry,"FrozenOwners");resources=Read(exitEntry,"FrozenResources");Need(frozen is HashSet<TrafficIntentKey> owners && owners.Contains(employeeKey) && resources is Dictionary<TrafficIntentKey,HashSet<string>> scope && scope.TryGetValue(employeeKey,out var owned) && owned.Contains("selected-home"),"real frozen current-resource proof");
                firstFrame=(int)Read(exitEntry,"FirstFrame");sequence=(long)Read(exitEntry,"RequestSequence");lastQueries=(int)Read(exitEntry,"Queries");finite=(float)Read(exitEntry,"FiniteLimit");lastWait=(float)Read(exitEntry,"WaitRemaining");Need(sequence>0 && lastWait>0,"original finite accounting");accountingCaptured=true;
                heldCup=firstBody.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="CustomerHeldCup")?.gameObject;Need(heldCup!=null && heldCup.activeInHierarchy,"A actual collected carry cup");
                Need((CustomerVisitState)Read(secondVisit,"State")==CustomerVisitState.PickupWaiting && bClaim.IsCurrent,"B has not begun another collection or changed its original wait assignment");
                Assert.That(sequence,Is.GreaterThan((long)Read(workerEntry,"RequestSequence")));Business();
                var grid=(DecorationGridSpace)Read(decor,"gridSpace");var root=CafeLoopRuntimeProof.Field<Transform>(decor,"gridRoot");var center=grid.GetCellCenterLocal(new GridPosition(4,5));var candidate=root.TransformPoint(center);
                (float Along,float Distance,Vector3 Point) Project(Vector3 point)
                {var best=float.PositiveInfinity;var along=0f;var selected=0f;var closest=Vector3.zero;for(var i=1;i<currentPath.Length;i++){var a=currentPath[i-1];var b=currentPath[i];a.y=b.y=point.y=0;var delta=b-a;var t=delta.sqrMagnitude==0?0:Mathf.Clamp01(Vector3.Dot(point-a,delta)/delta.sqrMagnitude);var p=a+delta*t;var distance=Vector3.Distance(point,p);if(distance<best){best=distance;selected=along+delta.magnitude*t;closest=p;}along+=delta.magnitude;}return(selected,best,closest);}
                var projected=Project(candidate);var local=root.InverseTransformPoint(projected.Point);var bodyProgress=Project(employee.transform.position);var half=runtime.Layout.GridSettings.CellSize*.5f;
                Need(Mathf.Abs(local.x-center.x)<half && Mathf.Abs(local.z-center.z)<half,"the actual CURRENT Native leg must enter interior cell4,5");
                var prefab=CafeLoopRuntimeProof.Field<NavigationActor>(tools,"blockerPrefab");
                var pair=employee.Settings.AgentRadius+prefab.Settings.AgentRadius+Mathf.Max(employee.Settings.CollisionSkin+employee.Settings.Epsilon,prefab.Settings.CollisionSkin+prefab.Settings.Epsilon);
                Need(projected.Along-bodyProgress.Along>pair && Vector3.Distance(projected.Point,((TrafficRequest)Read(workerEntry,"Request")).Target)>pair,"fault strictly ahead on CURRENT leg, away from its endpoint");
                Need(world.RegisteredActors.All(a=>Vector3.Distance(a.transform.position,projected.Point)>a.Settings.AgentRadius+prefab.Settings.AgentRadius+Mathf.Max(a.Settings.CollisionSkin+a.Settings.Epsilon,prefab.Settings.CollisionSkin+prefab.Settings.Epsilon)),"fault cannot overlap any actual body");
                Need(ReturningBound() && (int)Read(byId[workerStamp.RequestId],"RetryCount")==0 && !(bool)Read(byId[workerStamp.RequestId],"Recovering"),"same original unretried current-leg Native request must still be live immediately before fault");var revision=adapter.Revision;obstruction=Blocker(projected.Point,"current-return-original-leg-fault");Need(byId.Contains(workerStamp.RequestId) && adapter.Revision==revision,"fault leaves the original active request/revision intact");
                for(var frame=0;frame<6000 && !failed;frame++)
                {if(byId.Contains(workerStamp.RequestId))noProgress|=(float)Read(byId[workerStamp.RequestId],"NoProgressSeconds")>0;ExitFacts();Business();Assert.That(errors,Is.Empty);yield return null;}
                Need(failed && noProgress,"the same original CURRENT request must genuinely fail after positive normal no-progress; recovery success is NOT_PROVEN for this negative");
                Assert.That(byId.Contains(workerStamp.RequestId),Is.False);Assert.That(Read(workerEntry,"Retired"),Is.EqualTo(true));Assert.That(Read(workerEntry,"Leg"),Is.Null);Assert.That(core.HasActiveGrant(employeeKey),Is.False);
                Assert.That(employee.isActiveAndEnabled && world.RegisteredActors.Contains(employee),Is.True);Assert.That(employees.Snapshot.Single(e=>e.ActorId==employeeId).State,Is.EqualTo(EmployeeState.Blocked));
                for(var frame=0;frame<60;frame++){ExitFacts();Business();Assert.That(errors,Is.Empty);yield return null;}
                var final=core.GetDecision(exitKey);Assert.That(final.Kind==TrafficDecisionKind.WaitingForTraffic && final.Blockers.Any(b=>b.Owner.Equals(employeeKey)),Is.False,"failed current owner supplies no continuing finite authority");
                Need(terminal!=null || exiting.Contains(first),"A requires original Terminal or genuine safe Native Exit, not merely Grant");
                world.Unregister(obstruction);obstruction.gameObject.SetActive(false);UnityEngine.Object.Destroy(obstruction.gameObject);obstruction=null;
                for(var frame=0;frame<30;frame++){ExitFacts();Business();Assert.That(errors,Is.Empty);yield return null;}
                CollectionAssert.AreEqual(waitSlots,(Vector3[])Read(queue,"waitSlots"));Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(2));Assert.That(exiting.Contains(second),Is.False);
                TestContext.WriteLine("CURRENT_RETURN_NEGATIVE originalNative="+workerStamp.RequestId+" key="+employeeKey+" sameCurrentWait=True noProgress=True originalFailed=True retired=True originalWaitAccounting=True actualAExit="+exiting.Contains(first)+" BReadyCup/Body/TotalRetained=True");
            }
            finally{cleanup("test-finally");}
        }

        private IEnumerator ObserveEarlierEmployeeExitTraffic(bool cancelEmployee,int deferredCandidate=0,bool multiSubstep=false)
        {
            // Approved Task4 contract: current Core rights, actual Native legs and original business identities.
            // 保留五个业务case；不写Route/State，不调用旧role等待批准者，也不冻结正常driver。
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            object Read(object value,string name)=>value==null?null:MovementRead(value,name);
            var core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic");
            var entries=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(core,"entries");
            var visits=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits");
            var live=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world.Service,"byActor");
            var byId=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world.Service,"byId");
            var corridorHook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);
            var preparationHook=typeof(CustomerFlowController).GetField("CollectedExitPreparationObserver",flags);
            var employeeHook=typeof(EmployeeFlowController).GetField("MovementObserver",flags);
            var previousEmployee=employeeHook.GetValue(employees) as Action<string,string,string>;Action<string,string,string> installedEmployee=null;
            var previousCorridor=corridorHook.GetValue(customers) as Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>;
            var previousPreparation=preparationHook.GetValue(customers) as Action<string>;
            Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string> installedCorridor=null;
            Action<string> installedPreparation=null;
            var conflict=typeof(ServiceTrafficCoordinator).GetMethod("PathsConflict",BindingFlags.Static|BindingFlags.NonPublic);
            var errors=new List<string>();var exited=new HashSet<string>();var observers=new List<CafeActualExitObserver>();
            var waitWitnesses=new HashSet<string>(); // At most 8 distinct read-only receipts; never a new wait authority.
            string Key(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
            var priorCapture=Time.captureDeltaTime;var observing=true;var sampleArrival=false;bool? arrivalValid=null;var arrivalDetail="";
            string first=null,second=null,employeeId=null;NavigationActor employee=null;
            object firstVisit=null,firstAdmission=null,exitEntry=null;NavigationActor firstBody=null;
            AnimalCafe.Capacity.CapacityToken firstTotal=null;
            CafeOrderKey firstOrder=default,secondOrder=default;CafeDeliverySnapshot firstDelivery=default;GameObject firstCup=null;
            var display=Find<PickupCupDisplay>();var prepared=0;var customerNative=0;
            TrafficIntentKey employeeKey=default,exitKey=default;TrafficDecision employeeSource=null;
            var earlierWindow=false;var haveExit=false;var primaryObserved=false;var selectedObserved=false;var waitingObserved=false;var firstNativeGoalArrived=false;
            long windowRequest=0,sequence=0,acquisition=0;int firstFrame=0,lastQueries=0;float? lastWait=null,finiteLimit=null;object frozenOwners=null,frozenResources=null;
            EmployeeTaskRunner employeeRunner=null;CafeTaskToken employeeToken=default;long employeeHandle=0;CafeTaskEvent? returnedReceipt=null;var employeeCancelled=false;
            TrafficNativeStamp cancelledNative=default;var cancelledFrame=-1;
            var validatedWaits=new HashSet<TrafficDecision>();
            var ownerEntries=new Dictionary<TrafficIntentKey,(object Entry,int First,long Sequence,int Queries,float Limit)>();
            var ownerStamps=new Dictionary<TrafficIntentKey,TrafficNativeStamp>();
            var actualArrivals=new Dictionary<TrafficIntentKey,(TrafficNativeStamp Stamp,int Frame)>();

            bool EmployeeBound(out long request)
            {
                request=0;if(employee==null || !employee.isActiveAndEnabled || !world.RegisteredActors.Contains(employee) || !live.Contains(employeeId))return false;
                var row=employees.Snapshot.Single(value=>value.ActorId==employeeId);
                if(row.State!=EmployeeState.CarryingToPickUp && row.State!=EmployeeState.Returning)return false;
                var found=entries.Keys.Cast<TrafficIntentKey>().Where(key=>key.ActorId==employeeId && key.IntentId=="employee-pickup-journey" &&
                    !(bool)Read(entries[key],"Retired") && Read(entries[key],"Leg")!=null).ToArray();
                if(found.Length!=1)return false;
                var key=found[0];var entry=entries[key];var source=Read(entry,"JourneyPermit") as TrafficDecision;var leg=Read(entry,"Leg");
                if(source==null || source.Kind!=TrafficDecisionKind.Granted || source.AcquiredSequence<=0 ||
                    !(bool)Read(leg,"Committed") || !(bool)Read(leg,"Bound") || (bool)Read(leg,"Completed") ||
                    !ReferenceEquals(Read(leg,"ActorLife"),employee))return false;
                var stamp=(TrafficNativeStamp)Read(leg,"Stamp");request=stamp.RequestId;
                if(request<=0 || !byId.Contains(request) || (long)Read(live[employeeId],"Id")!=request)return false;
                var actual=CafeLoopRuntimeProof.Field<System.Collections.IEnumerable>(employees,"employees").Cast<object>().Single(value=>(string)Read(value,"Id")==employeeId);
                var runner=(EmployeeTaskRunner)Read(actual,"Runner");var native=Read(runner,"moveLeg");
                if(native==null || (long)Read(native,"RequestId")!=request || (int)Read(native,"NativeLegGeneration")!=stamp.LegGeneration)return false;
                if(employeeSource==null)
                {employeeKey=key;employeeSource=source;employeeRunner=runner;employeeToken=(CafeTaskToken)Read(runner,"taskToken");employeeHandle=row.HandleId;}
                return key.Equals(employeeKey) && ReferenceEquals(source,employeeSource) && ReferenceEquals(runner,employeeRunner) &&
                    row.HandleId==employeeHandle && employeeHandle>0 && employeeToken.Equals((CafeTaskToken)Read(runner,"taskToken"));
            }
            void NativeArrived(TrafficIntentKey key,string result)
            {
                if(!result.Contains("status=Arrived") || !entries.Contains(key))return;
                var leg=Read(entries[key],"Leg");if(leg==null)return;var stamp=(TrafficNativeStamp)Read(leg,"Stamp");
                if(stamp.RequestId<=0 || !(" "+result+" ").Contains(" request="+stamp.RequestId+" "))return;
                Assert.That(Read(leg,"Committed"),Is.EqualTo(true));Assert.That(Read(leg,"Bound"),Is.EqualTo(true));
                Assert.That(byId.Contains(stamp.RequestId),Is.False,"A real Native callback follows removal of that exact request");
                actualArrivals[key]=(stamp,Time.frameCount);
            }
            void EmployeeMovement(string id,string operation,string result)
            {
                if(!observing || multiSubstep || employeeSource==null || id!=employeeId || operation!="callback")return;
                try
                {Assert.That(employeeToken.Equals((CafeTaskToken)Read(employeeRunner,"taskToken")),Is.True);NativeArrived(employeeKey,result);}
                catch(Exception error){errors.Add("employee original Native receipt: "+error);}
            }
            void ValidateFiniteOwner(TrafficBlocker blocker,TrafficDecision wait)
            {
                Assert.That(!string.IsNullOrEmpty(blocker.ResourceId) && !string.IsNullOrEmpty(blocker.Proof),Is.True);
                var eligible=(Dictionary<TrafficIntentKey,HashSet<string>>)Read(exitEntry,"FrozenResources");
                Assert.That(eligible.TryGetValue(blocker.Owner,out var resources) && resources.Contains(blocker.ResourceId),Is.True);
                Assert.That(entries.Contains(blocker.Owner),Is.True,"NOT_PROVEN: this fixture requires an actual Native/handoff owner entry");
                var entry=entries[blocker.Owner];Assert.That(((TrafficIntentKey)Read(entry,"Key")).Equals(blocker.Owner),Is.True);
                var now=((int)Read(entry,"FirstFrame"),(long)Read(entry,"RequestSequence"),(int)Read(entry,"Queries"),(float)Read(entry,"FiniteLimit"));
                if(ownerEntries.TryGetValue(blocker.Owner,out var old))
                {Assert.That(entry,Is.SameAs(old.Entry));Assert.That(now.Item1,Is.EqualTo(old.First));Assert.That(now.Item2,Is.EqualTo(old.Sequence));
                    Assert.That(now.Item3,Is.GreaterThanOrEqualTo(old.Queries));Assert.That(now.Item4,Is.EqualTo(old.Limit));}
                ownerEntries[blocker.Owner]=(entry,now.Item1,now.Item2,now.Item3,now.Item4);
                var snapshot=(TrafficWorldSnapshot)Read(core,"snapshot");var body=snapshot.Bodies.FirstOrDefault(value=>value.Owner.Equals(blocker.Owner));
                var leg=Read(entry,"Leg");var source=Read(entry,"JourneyPermit") as TrafficDecision;var request=Read(entry,"Request") as TrafficRequest;
                var path=Read(entry,"CurrentPath") as Vector3[];var handoff=Read(entry,"PendingHandoff");
                var stamp=Read(leg,"Stamp") is TrafficNativeStamp native?native:default;
                var actor=body?.ActorLife as NavigationActor;
                var bound=!(bool)Read(entry,"Retired") && (bool)Read(entry,"Active") && source!=null && source.AcquiredSequence>0 &&
                    leg!=null && (bool)Read(leg,"Committed") && (bool)Read(leg,"Bound") && !(bool)Read(leg,"Completed") &&
                    actor!=null && actor.isActiveAndEnabled && body.Enabled && body.ActorActive && ReferenceEquals(Read(leg,"ActorLife"),actor) &&
                    stamp.RequestId>0 && byId.Contains(stamp.RequestId) && live.Contains(blocker.Owner.ActorId) &&
                    (long)Read(live[blocker.Owner.ActorId],"Id")==stamp.RequestId;
                if(bound)
                {
                    Assert.That(request!=null && request.Intent.Equals(blocker.Owner) && request.IndependentlyProgressing && request.FiniteSeconds>0,Is.True);
                    Assert.That(path!=null && path.Length>1 && body.IndependentlyProgressing && body.RemainingSeconds>0,Is.True);
                    Assert.That(core.GetDecision(blocker.Owner).FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None));
                    if(visits.Contains(blocker.Owner.ActorId))
                    {var move=Read(visits[blocker.Owner.ActorId],"TrafficMove");Assert.That(Read(move,"Source"),Is.SameAs(source));
                        Assert.That(((TrafficIntentKey)Read(move,"Intent")).Equals(blocker.Owner),Is.True);Assert.That(Read(move,"NativeGeneration"),Is.EqualTo(stamp.LegGeneration));}
                    foreach(var other in snapshot.Bodies.Where(value=>value.Owner.ActorId!=blocker.Owner.ActorId))
                        Assert.That(CustomerRoutePlanner.IsClear(path,new[]{other.Position},request.Radius+other.Radius+Math.Max(request.SafetyMargin,other.SafetyMargin)),Is.True);
                    foreach(var other in snapshot.Parking.Where(value=>value.Owner.ActorId!=blocker.Owner.ActorId))
                        Assert.That(CustomerRoutePlanner.IsClear(path,new[]{other.Position},request.Radius+other.Radius+Math.Max(request.SafetyMargin,other.SafetyMargin)),Is.True);
                    ownerStamps[blocker.Owner]=stamp;return;
                }
                if(handoff!=null && !(bool)Read(entry,"Retired") && leg!=null && (bool)Read(leg,"Completed"))
                {
                    // Read the existing bounded dependency; do not replay Core's progress policy.
                    var dependencies=(System.Collections.IDictionary)Read(handoff,"Dependencies");
                    Assert.That(dependencies.Contains(exitEntry) && ((HashSet<string>)dependencies[exitEntry]).Contains(blocker.ResourceId),Is.True);
                    Assert.That((float)Read(handoff,"Remaining"),Is.GreaterThan(0));Assert.That(Read(handoff,"Source"),Is.SameAs(source));
                    Assert.That(body!=null && body.ActorActive && ReferenceEquals(body.ActorLife,Read(handoff,"ActorLife")) && ReferenceEquals(Read(leg,"ActorLife"),body.ActorLife),Is.True);return;
                }
                // A previously proved Wait can be queued for ordinary recheck immediately after its real owner Arrived.
                // 只接同原Native完成回执后的下一正常帧；不让已退休owner成为新的无限等待依据。
                Assert.That(validatedWaits.Contains(wait) && (bool)Read(exitEntry,"Queued") && ownerStamps.TryGetValue(blocker.Owner,out var prior) &&
                    !byId.Contains(prior.RequestId) &&
                    (actualArrivals.TryGetValue(blocker.Owner,out var arrival) && arrival.Stamp.LegGeneration>=prior.LegGeneration && Time.frameCount-arrival.Frame<=1 ||
                     employeeCancelled && blocker.Owner.Equals(employeeKey) && prior.Equals(cancelledNative) && Time.frameCount-cancelledFrame<=1),
                    Is.True,"NOT_PROVEN: original finite Native owner/handoff or its immediate related actual completion/cancel recheck is missing");
            }
            Vector3[] CurrentEmployeeRight()
            {
                if(EmployeeBound(out _))return (Vector3[])Read(entries[employeeKey],"CurrentPath");
                var entry=entries[employeeKey];var row=employees.Snapshot.Single(value=>value.ActorId==employeeId);
                if(employeeCancelled || returnedReceipt.HasValue)
                {Assert.That(Read(entry,"Retired"),Is.EqualTo(true));Assert.That(Read(entry,"Leg"),Is.Null);Assert.That(live.Contains(employeeId),Is.False);
                    if(returnedReceipt.HasValue)Assert.That(row.State,Is.EqualTo(EmployeeState.Idle));return Array.Empty<Vector3>();}
                Assert.That(Read(entry,"Retired"),Is.EqualTo(false));Assert.That(Read(entry,"JourneyPermit"),Is.SameAs(employeeSource));
                Assert.That(row.HandleId,Is.EqualTo(employeeHandle));Assert.That(employeeToken.Equals((CafeTaskToken)Read(employeeRunner,"taskToken")),Is.True);
                Assert.That(actualArrivals.ContainsKey(employeeKey),Is.True,"NOT_PROVEN: missing original Native Arrived receipt for same-job leg handoff");
                var leg=Read(entry,"Leg");Assert.That(leg==null || !(bool)Read(leg,"Bound") || (bool)Read(leg,"Completed"),Is.True);
                Assert.That(core.GetDecision(employeeKey).FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None));
                return (Vector3[])Read(entry,"CurrentPath");
            }
            void BusinessIdentity()
            {
                Assert.That(owner.Snapshot.Orders.Count,Is.LessThanOrEqualTo(2));
                if(first==null || exited.Contains(first))return;
                if(!visits.Contains(first))
                {
                    Assert.That(firstNativeGoalArrived,Is.True,"Removal still requires the original Native goal-arrival callback; OnDestroy receipt remains mandatory");
                    Assert.That(customers.Capacity.GetReservations().Single(value=>ReferenceEquals(value.Token,firstTotal)).State,Is.EqualTo(AnimalCafe.Capacity.ReservationState.Released));
                    return;
                }
                Assert.That(visits[first],Is.SameAs(firstVisit));Assert.That(Read(firstVisit,"Actor"),Is.SameAs(firstBody));
                Assert.That(Read(firstVisit,"Admission"),Is.SameAs(firstAdmission));
                var total=customers.Capacity.GetReservations().Single(value=>value.OwnerId==first && value.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers);
                Assert.That(total.Token,Is.SameAs(firstTotal));Assert.That(total.State,Is.EqualTo(AnimalCafe.Capacity.ReservationState.Occupied));
                Assert.That(owner.Snapshot.Orders[firstOrder].CustomerId,Is.EqualTo(first));
                if(!(bool)Read(firstVisit,"HasCollected"))
                {
                    Assert.That(owner.Snapshot.Orders[firstOrder].State,Is.EqualTo(OrderState.ReadyForPickup));
                    Assert.That(owner.Snapshot.Delivery.Single(value=>value.Key.Equals(firstOrder)).Equals(firstDelivery),Is.True);
                    Assert.That(display.VisibleOrders[firstOrder],Is.SameAs(firstCup));
                }
                else
                {
                    Assert.That(owner.Snapshot.Orders[firstOrder].State,Is.EqualTo(OrderState.Completed));
                    Assert.That((CafeOrderKey?)Read(firstVisit,"CollectedOrder"),Is.EqualTo((CafeOrderKey?)firstOrder));
                    Assert.That(firstBody.GetComponentsInChildren<Transform>(true).Any(value=>value.name=="CustomerHeldCup"),Is.True);
                }
            }
            void ObserveExitSource()
            {
                if(!observing || first==null)return;
                var keys=entries.Keys.Cast<TrafficIntentKey>().Where(key=>key.ActorId==first && key.IntentId=="actual-exit").ToArray();
                if(keys.Length==0)return;Assert.That(keys.Length,Is.EqualTo(1),"Original actual-exit identity must not be replaced");
                var key=keys[0];var entry=entries[key];
                if(!haveExit){exitKey=key;exitEntry=entry;firstFrame=(int)Read(entry,"FirstFrame");haveExit=true;}
                Assert.That(key.Equals(exitKey),Is.True);Assert.That(entry,Is.SameAs(exitEntry));Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(firstFrame));
                var currentSequence=(long)Read(entry,"RequestSequence");if(sequence==0 && currentSequence>0)sequence=currentSequence;
                if(sequence>0)Assert.That(currentSequence,Is.EqualTo(sequence));
                var queries=(int)Read(entry,"Queries");Assert.That(queries,Is.GreaterThanOrEqualTo(lastQueries));lastQueries=queries;
                var limit=(float)Read(entry,"FiniteLimit");if(finiteLimit.HasValue)Assert.That(limit,Is.EqualTo(finiteLimit.Value));else finiteLimit=limit;
                var owners=Read(entry,"FrozenOwners");
                if(owners!=null)
                {
                    if(frozenOwners==null)frozenOwners=owners;else Assert.That(owners,Is.SameAs(frozenOwners));
                    var resources=Read(entry,"FrozenResources");if(frozenResources==null)frozenResources=resources;else Assert.That(resources,Is.SameAs(frozenResources));
                    var remaining=(float)Read(entry,"WaitRemaining");if(lastWait.HasValue)Assert.That(remaining,Is.LessThanOrEqualTo(lastWait.Value));lastWait=remaining;
                }
                var decision=core.GetDecision(key);var leg=Read(entry,"Leg");
                if(decision.Kind==TrafficDecisionKind.DeferredByBudget && leg==null && decision.AcquiredSequence==0 && Read(entry,"JourneyPermit")==null)primaryObserved=true;
                if(decision.Kind==TrafficDecisionKind.WaitingForTraffic)
                {
                    // Original decision facts only. No progress predicate, query, refresh, wakeup or mutation.
                    // 先记录实际owner/reason；未知等待不能被猜成员工等待，也不改变原R2断言。
                    var edge=string.Join(";",decision.Blockers.Select(value=>Key(value.Owner)+"/"+value.ResourceId+"/"+value.Proof));
                    if(waitWitnesses.Count<8 && waitWitnesses.Add(Key(key)+"/"+decision.Reason+"/"+edge))
                    {
                        var snapshot=(TrafficWorldSnapshot)Read(core,"snapshot");
                        var detail=decision.Blockers.Select(value=>
                        {
                            var owner=entries.Contains(value.Owner)?entries[value.Owner]:null;var ownerLeg=Read(owner,"Leg");var handoff=Read(owner,"PendingHandoff");
                            var body=snapshot.Bodies.FirstOrDefault(item=>item.Owner.Equals(value.Owner));
                            var stamp=Read(ownerLeg,"Stamp") is TrafficNativeStamp native?native:default;
                            var dependencies=Read(handoff,"Dependencies") as System.Collections.IDictionary;
                            return Key(value.Owner)+" retired="+Read(owner,"Retired")+" kind="+(Read(owner,"Decision") as TrafficDecision)?.Kind+
                                " active="+Read(owner,"Active")+" prepared="+Read(owner,"Prepared")+" acquired="+Read(owner,"AcquiredSequence")+
                                " path="+((Read(owner,"CurrentPath") as Vector3[])?.Length??0)+" finite="+(Read(owner,"Request") as TrafficRequest)?.FiniteSeconds+
                                " bound="+Read(ownerLeg,"Bound")+" committed="+Read(ownerLeg,"Committed")+" completed="+Read(ownerLeg,"Completed")+
                                " native="+stamp.RequestId+"/g"+stamp.LegGeneration+" nativeLive="+byId.Contains(stamp.RequestId)+
                                " body="+(body==null?"NONE":body.Enabled+"/active="+body.ActorActive+"/progress="+body.IndependentlyProgressing+"/remaining="+body.RemainingSeconds)+
                                " handoffRemaining="+Read(handoff,"Remaining")+" handoffDependsOnWaiter="+(dependencies!=null && dependencies.Keys.Cast<object>().Any(item=>ReferenceEquals(item,entry)));
                        });
                        TestContext.WriteLine("CORE_EXIT_WAIT_ORIGINAL frame="+Time.frameCount+" key="+Key(key)+" reason="+decision.Reason+" blockers=["+edge+"]"+
                            " first="+firstFrame+" sequence="+currentSequence+" queries="+queries+" finite="+limit+" remaining="+Read(entry,"WaitRemaining")+
                            " employee="+Key(employeeKey)+" employeeState="+employees.Snapshot.FirstOrDefault(value=>value.ActorId==employeeId).State+" windowNative="+windowRequest+
                            " owners=["+string.Join(";",detail)+"]");
                    }
                    Assert.That(decision.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None));
                    Assert.That(decision.Blockers,Is.Not.Empty);Assert.That(lastWait.HasValue && lastWait.Value>0,Is.True);
                    foreach(var blocker in decision.Blockers)ValidateFiniteOwner(blocker,decision);
                    validatedWaits.Add(decision);
                    if(decision.Blockers.Any(value=>value.Owner.Equals(employeeKey)))waitingObserved=true;
                }
            }
            void CheckFirstStrictLeg()
            {
                ObserveExitSource();var move=Read(firstVisit,"TrafficMove");var source=Read(move,"Source") as TrafficDecision;
                Assert.That(haveExit && source!=null,Is.True);Assert.That(((TrafficIntentKey)Read(move,"Intent")).Equals(exitKey),Is.True);
                Assert.That(source.Kind,Is.EqualTo(TrafficDecisionKind.Granted));Assert.That(source.AcquiredSequence,Is.GreaterThan(employeeSource.AcquiredSequence));
                if(acquisition==0)acquisition=source.AcquiredSequence;else Assert.That(source.AcquiredSequence,Is.EqualTo(acquisition));
                Assert.That(Read(exitEntry,"JourneyPermit"),Is.SameAs(source));
                var journey=(TrafficRequest)Read(exitEntry,"JourneyRequest");Assert.That(journey.Intent.Equals(exitKey),Is.True);Assert.That(journey.RequestedFrame,Is.EqualTo(firstFrame));
                var primary=source.ProofRoutes.Single(value=>value.ResourceId=="pickup/"+first);
                Assert.That(primary.ExpandedPath,Is.Not.Empty);Assert.That(primary.Waypoints.Last(),Is.EqualTo(CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position));
                var leg=Read(exitEntry,"Leg");Assert.That(leg,Is.Not.Null);Assert.That(Read(leg,"ActorLife"),Is.SameAs(firstBody));
                Assert.That(Read(leg,"Bound"),Is.EqualTo(false));Assert.That(Read(leg,"Committed"),Is.EqualTo(false));
                var request=(TrafficRequest)Read(exitEntry,"Request");var snapshot=(TrafficWorldSnapshot)Read(core,"snapshot");
                Assert.That(core.GetDecision(exitKey).Kind,Is.EqualTo(TrafficDecisionKind.Granted));
                // Core SnapshotCurrent compares facts' versions; a fresh equal-version snapshot need not be the same object.
                // 不把合法的同版本原Prepared腿误判成旧世界；仍核当前真实actor/life/pose和完整通路。
                Assert.That(request.Snapshot.Version,Is.EqualTo(snapshot.Version));Assert.That(request.Snapshot.BodyVersion,Is.EqualTo(snapshot.BodyVersion));
                Assert.That(snapshot,Is.SameAs(Read(customers,"trafficSnapshot")));
                var own=snapshot.Bodies.Single(value=>value.Owner.ActorId==first);Assert.That(own.ActorLife,Is.SameAs(firstBody));
                Assert.That(own.ActorActive && own.Enabled,Is.True);Assert.That(own.Position,Is.EqualTo(firstBody.transform.position));
                Assert.That(request.Start,Is.EqualTo(firstBody.transform.position));
                var path=(Vector3[])Read(exitEntry,"CurrentPath");Assert.That(path.Length,Is.GreaterThan(1));
                foreach(var body in world.RegisteredActors.Where(value=>value!=firstBody))
                    Assert.That(CustomerRoutePlanner.IsClear(path,new[]{body.transform.position},firstBody.Settings.AgentRadius+body.Settings.AgentRadius+
                        Math.Max(firstBody.Settings.CollisionSkin+firstBody.Settings.Epsilon,body.Settings.CollisionSkin+body.Settings.Epsilon)),Is.True,
                        "The actual current Granted leg must remain body-clear");
                foreach(var parking in snapshot.Parking.Where(value=>value.Owner.ActorId!=first))
                    Assert.That(CustomerRoutePlanner.IsClear(path,new[]{parking.Position},firstBody.Settings.AgentRadius+parking.Radius+
                        Math.Max(firstBody.Settings.CollisionSkin+firstBody.Settings.Epsilon,parking.SafetyMargin)),Is.True);
                var earlier=CurrentEmployeeRight();
                if(earlier.Length>1)
                {
                    Assert.That((bool)conflict.Invoke(null,new object[]{path,earlier,firstBody.Settings.AgentRadius+employee.Settings.AgentRadius+
                        Math.Max(firstBody.Settings.CollisionSkin+firstBody.Settings.Epsilon,employee.Settings.CollisionSkin+employee.Settings.Epsilon)}),Is.False,
                        "A safe Grant/detour must not cross the still-live earlier current Native right");
                }
                if((int)Read(move,"Index")==0)selectedObserved=true;
            }
            void Corridor(string operation,string id,string related,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                if(!observing)return;
                try
                {
                    if(!multiSubstep && operation=="segment.callback-result" && visits.Contains(id))
                    {var move=Read(visits[id],"TrafficMove");if(move!=null)NativeArrived((TrafficIntentKey)Read(move,"Intent"),result);}
                    if(id!=first)return;
                    if(operation=="exit.actual-dispatch")
                    {
                        Assert.That(EmployeeBound(out windowRequest),Is.True,"NOT_PROVEN: original acquired employee must still own a Bound/live leg at actual collection");
                        Assert.That(employeeSource.AcquiredSequence,Is.GreaterThan(0));earlierWindow=true;
                    }
                    if(operation=="segment.issue" && (CustomerVisitState)Read(firstVisit,"State")==CustomerVisitState.Exiting)CheckFirstStrictLeg();
                    if(operation=="segment.submitted" && (CustomerVisitState)Read(firstVisit,"State")==CustomerVisitState.Exiting)
                    {
                        var move=Read(firstVisit,"TrafficMove");Assert.That(((TrafficIntentKey)Read(move,"Intent")).Equals(exitKey),Is.True);
                        var leg=Read(exitEntry,"Leg");var stamp=(TrafficNativeStamp)Read(leg,"Stamp");
                        Assert.That(Read(leg,"Committed"),Is.EqualTo(true));Assert.That(Read(leg,"Bound"),Is.EqualTo(true));
                        Assert.That(live.Contains(first) && byId.Contains(stamp.RequestId),Is.True);Assert.That(Read(live[first],"Id"),Is.EqualTo(stamp.RequestId));
                        Assert.That(Read(leg,"ActorLife"),Is.SameAs(firstBody));Assert.That(stamp.LegGeneration,Is.EqualTo((int)Read(move,"NativeGeneration")));customerNative++;
                    }
                    if(operation=="segment.callback-result" && result.Contains("status=Arrived") &&
                        Vector3.Distance(target,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position)<=.081f)firstNativeGoalArrived=true;
                    ObserveExitSource();
                }
                catch(Exception error){errors.Add(operation+": "+error);}
            }
            void Preparation(string ignored)
            {if(!observing)return;try{ObserveExitSource();}catch(Exception error){errors.Add("actual preparation: "+error);}}
            void ObserveArrival(CafeTaskEvent value)
            {
                if(value.Checkpoint.VisitId!=second)return;
                if(value.Kind==CafeTaskEventKind.Prepared)prepared++;
                if(!multiSubstep && value.Kind==CafeTaskEventKind.Returned)
                {
                    try
                    {
                        Assert.That(value.ActorId,Is.EqualTo(employeeId));Assert.That(value.HandleId,Is.EqualTo(employeeHandle));Assert.That(value.Token.Equals(employeeToken),Is.True);
                        Assert.That(value.Checkpoint.Stage,Is.EqualTo(CafeWorkStage.Finished));Assert.That(value.RequestId,Is.GreaterThan(0));
                        Assert.That(actualArrivals.TryGetValue(employeeKey,out var receipt) && receipt.Stamp.RequestId==value.RequestId,Is.True);
                        Assert.That(byId.Contains(value.RequestId),Is.False);Assert.That(Read(entries[employeeKey],"Retired"),Is.EqualTo(true));
                        Assert.That(Vector3.Distance(employee.transform.position,employeeSource.ProofRoutes.Single(p=>p.ResourceId=="selected-home").Waypoints.Last()),Is.LessThanOrEqualTo(.081f));
                        returnedReceipt=value;
                    }
                    catch(Exception error){errors.Add("original employee Returned receipt: "+error);}
                }
                if(!sampleArrival || value.Kind!=CafeTaskEventKind.DeliveryArrived)return;
                try
                {
                    Assert.That(employees.TryGetBinding(value.Role,out var binding),Is.True);
                    var row=CafeLoopRuntimeProof.Field<System.Collections.IEnumerable>(employees,"employees").Cast<object>().Single(e=>(string)Read(e,"Id")==value.ActorId);
                    var runner=(EmployeeTaskRunner)Read(row,"Runner");var actor=world.RegisteredActors.Single(a=>a.ActorId==value.ActorId);var target=runner.DeliveryTarget.Value;
                    var driver=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world,"drivers")[actor];
                    arrivalValid=employees.IsCurrentDeliveryArrival(value,binding.CupAddress.Value);
                    arrivalDetail="request="+value.RequestId+" runnerCurrent="+runner.IsCurrentDeliveryArrival(value)+" distance="+Vector3.Distance(actor.transform.position,target.Position).ToString("R")+
                        " driverSpeed="+Read(driver,"actualSpeed")+" presenterSpeed="+actor.GetComponent<NavigationWalkPresenter>().ActualSpeed+" delta="+Time.deltaTime.ToString("R");
                }
                catch(Exception error){errors.Add("actual delivery arrival: "+error);}
            }
            exitTrafficObserving=true;employees.IntegratedTaskEvent+=ObserveArrival;
            try
            {
                Assert.That(time.TrySetSpeed(GameSpeed.Fast),Is.True);
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                Assert.That(customers.TrySpawn(),Is.True);first=customers.Snapshot.Single().VisitId;
                yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==first&&o.State==OrderState.ReadyForPickup)&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                firstVisit=visits[first];firstBody=(NavigationActor)Read(firstVisit,"Actor");firstAdmission=Read(firstVisit,"Admission");
                firstTotal=customers.Capacity.GetReservations().Single(r=>r.OwnerId==first&&r.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers).Token;
                firstDelivery=owner.Snapshot.Delivery.Single(value=>value.VisitId==first);firstOrder=firstDelivery.Key;
                Assert.That(display.VisibleOrders.TryGetValue(firstOrder,out firstCup),Is.True);
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                Assert.That(customers.TrySpawn(),Is.True);second=customers.Snapshot.Single(v=>v.VisitId!=first).VisitId;
                foreach(var id in new[]{first,second})
                {
                    var token=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id&&r.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers).Token;
                    var body=world.RegisteredActors.Single(a=>a.ActorId==id);var observer=body.gameObject.AddComponent<CafeActualExitObserver>();observer.VisitId=id;
                    observer.Receipt=(visit,pose)=>
                    {
                        if(!observing || !exitTrafficObserving)return;
                        try{Assert.That(Vector3.Distance(pose,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position),Is.LessThanOrEqualTo(.081f));
                            Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,token)).State,Is.EqualTo(AnimalCafe.Capacity.ReservationState.Released));Assert.That(exited.Add(visit),Is.True);}
                        catch(Exception error){errors.Add("actual Exit: "+error);}
                    };observers.Add(observer);
                }
                employeeId=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).ActorId;employee=world.RegisteredActors.Single(value=>value.ActorId==employeeId);
                installedCorridor=previousCorridor+Corridor;installedPreparation=previousPreparation+Preparation;
                corridorHook.SetValue(customers,installedCorridor);preparationHook.SetValue(customers,installedPreparation);
                installedEmployee=previousEmployee+EmployeeMovement;employeeHook.SetValue(employees,installedEmployee);
                yield return Until(()=>{Assert.That(errors,Is.Empty);BusinessIdentity();return EmployeeBound(out _);});
                secondOrder=owner.Snapshot.Orders.Single(value=>value.Value.CustomerId==second).Key;
                Assert.That(prepared,Is.EqualTo(1));Assert.That(employeeSource.ProofRoutes.Any(value=>value.ResourceId=="selected-home"),Is.True);
                if(multiSubstep)
                {
                    Assert.That(employees.Snapshot.Single(value=>value.ActorId==employeeId).State,Is.EqualTo(EmployeeState.CarryingToPickUp),"NOT_PROVEN: delivery arrival already passed the original Bound window");
                    time.SetNormal();Time.captureDeltaTime=.25f;sampleArrival=true;
                    yield return Until(()=>{Assert.That(errors,Is.Empty);BusinessIdentity();return arrivalValid.HasValue;});
                    TestContext.WriteLine("MULTI_SUBSTEP_ARRIVAL "+arrivalDetail);
                    Assert.That(arrivalValid.Value,Is.True,"A real final native Arrived must pass the original strict service check: "+arrivalDetail);
                    Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==second).State,Is.EqualTo(OrderState.ReadyForPickup));
                    Assert.That(owner.Snapshot.Delivery.Any(value=>value.Key.Equals(secondOrder)),Is.True);yield break;
                }
                tools.ResumeCollection();
                yield return Until(()=>{Assert.That(errors,Is.Empty);BusinessIdentity();return customers.Snapshot.Single(value=>value.VisitId==first).State==CustomerVisitState.Exiting;});
                Assert.That(earlierWindow,Is.True,"NOT_PROVEN: no actual earlier valid Native commitment overlapped this real Exit declaration");
                Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==first).State,Is.EqualTo(OrderState.Completed));
                if(cancelEmployee)
                {
                    var capacityBefore=customers.Capacity.GetCapacities().Select(c=>c.Used).ToArray();var positionBefore=employee.transform.position;
                    var paidBefore=owner.Snapshot.PaidVisitIds.ToArray();var stateBefore=owner.Snapshot.Orders[secondOrder].State;var deliveryBefore=owner.Snapshot.Delivery.ToArray();
                    Assert.That(EmployeeBound(out var cancelRequest),Is.True,"NOT_PROVEN: the original employee job has no current Bound/live leg; a completed job cannot be cancelled");
                    Assert.That(byId.Contains(cancelRequest),Is.True);
                    cancelledNative=(TrafficNativeStamp)Read(Read(entries[employeeKey],"Leg"),"Stamp");cancelledFrame=Time.frameCount;employeeCancelled=true;
                    Assert.That(world.Service.Cancel(cancelRequest),Is.True,"Cancel the current real Native leg of that same original job, retaining its physical body");
                    Assert.That(byId.Contains(cancelRequest),Is.False);Assert.That(employee.isActiveAndEnabled,Is.True);Assert.That(world.RegisteredActors.Contains(employee),Is.True);
                    Assert.That(employee.transform.position,Is.EqualTo(positionBefore));CollectionAssert.AreEqual(capacityBefore,customers.Capacity.GetCapacities().Select(c=>c.Used));
                    CollectionAssert.AreEqual(paidBefore,owner.Snapshot.PaidVisitIds);CollectionAssert.AreEqual(deliveryBefore,owner.Snapshot.Delivery);
                    Assert.That(owner.Snapshot.Orders[secondOrder].State,Is.EqualTo(stateBefore));Assert.That(prepared,Is.EqualTo(1));
                    Assert.That(exited,Is.Empty,"Cancelling staff movement cannot manufacture a customer departure");
                    Assert.That(Read(entries[employeeKey],"Retired"),Is.EqualTo(true));Assert.That(Read(entries[employeeKey],"Leg"),Is.Null);
                    yield return null; // Only the ordinary driver handles the related retirement notification.
                    Assert.That(errors,Is.Empty);BusinessIdentity();
                    if(haveExit && !exited.Contains(first))
                    {
                        var decision=core.GetDecision(exitKey);
                        Assert.That(decision.Kind==TrafficDecisionKind.WaitingForTraffic && decision.Blockers.Any(value=>value.Owner.Equals(employeeKey)),Is.False,
                            "A retired employee source cannot remain valid finite-release authority after ordinary processing");
                    }
                    CollectionAssert.AreEqual(capacityBefore,customers.Capacity.GetCapacities().Select(c=>c.Used));Assert.That(exited,Is.Empty);yield break;
                }
                yield return Until(()=>{Assert.That(errors,Is.Empty);ObserveExitSource();BusinessIdentity();return exited.Count==2&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle);});
                Assert.That(errors,Is.Empty);Assert.That(haveExit && customerNative>0,Is.True);
                if(deferredCandidate==1)Assert.That(primaryObserved,Is.True,"NOT_PROVEN: original unacquired actual-exit planning stage was not observed");
                if(deferredCandidate==2)Assert.That(selectedObserved,Is.True,"NOT_PROVEN: original acquired first strict leg was not observed before Native binding");
                Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(2));Assert.That(owner.Snapshot.Orders.ContainsKey(firstOrder)&&owner.Snapshot.Orders.ContainsKey(secondOrder),Is.True);
                Assert.That(owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed),Is.True);Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                Assert.That(returnedReceipt.HasValue,Is.True,"The original earlier employee job must actually return, not merely lose its Native ID");
                TestContext.WriteLine("CORE_EARLIER_EMPLOYEE earlierAcquired="+employeeSource.AcquiredSequence+" exitAcquired="+acquisition+" employeeWaitObserved="+waitingObserved+
                    " primary="+primaryObserved+" selectedUnbound="+selectedObserved+" realExit="+exited.Count);
            }
            finally
            {
                observing=false;exitTrafficObserving=false;employees.IntegratedTaskEvent-=ObserveArrival;Time.captureDeltaTime=priorCapture;
                foreach(var observer in observers)if(observer!=null)observer.Receipt=null;
                if(installedCorridor!=null && customers!=null && ReferenceEquals(corridorHook.GetValue(customers),installedCorridor))corridorHook.SetValue(customers,previousCorridor);
                else if(installedCorridor!=null)TestContext.WriteLine("CORE_EARLIER_EMPLOYEE cleanup UNKNOWN corridor ownership changed");
                if(installedPreparation!=null && customers!=null && ReferenceEquals(preparationHook.GetValue(customers),installedPreparation))preparationHook.SetValue(customers,previousPreparation);
                else if(installedPreparation!=null)TestContext.WriteLine("CORE_EARLIER_EMPLOYEE cleanup UNKNOWN preparation ownership changed");
                if(installedEmployee!=null && employees!=null && ReferenceEquals(employeeHook.GetValue(employees),installedEmployee))employeeHook.SetValue(employees,previousEmployee);
                else if(installedEmployee!=null)TestContext.WriteLine("CORE_EARLIER_EMPLOYEE cleanup UNKNOWN employee observer ownership changed");
            }
        }

        [UnityTest] public IEnumerator UntransferredPickupTargetSurvivesFailedPathRevalidationUntilRealRelease()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object Call(string name, params object[] args) => typeof(CustomerFlowController).GetMethod(name, flags).Invoke(customers, args);
            NavigationActor obstruction = null;
            CafeLoopRuntimeProof.Set(owner, "stepping", true); // Hold payment, keep real customer movement.
            try
            {
                Assert.That(customers.TrySpawn(), Is.True, customers.Status);
                var id = customers.Snapshot.Single().VisitId;
                yield return Until(() => customers.CafeSnapshot.Single().CounterArrived);
                Assert.That(customers.TryReservePickup(id, out var reason), Is.True, reason);
                var visit = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers, "visits")[id];
                var pickup = (PickupQueueService)visit.GetType().GetField("PickupOwner").GetValue(visit);
                var live = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(pickup, "reservations")[id];
                var target = (Vector3)live.GetType().GetField("Target").GetValue(live);
                var generation = live.GetType().GetField("Generation").GetValue(live);
                var tokens = customers.Capacity.GetReservations().Select(r => r.Token).ToArray();
                Assert.That(owner.Snapshot.PaidVisitIds, Is.Empty);
                obstruction = Blocker(target, "reserved-target-revalidation-obstruction");
                Assert.That(customers.TryReservePickup(id, out _), Is.False, "Real obstruction invalidates the route certificate");
                Assert.That(visit.GetType().GetField("ReservedPickupPath").GetValue(visit), Is.Null);
                world.Unregister(obstruction);
                UnityEngine.Object.DestroyImmediate(obstruction.gameObject); obstruction = null;

                // The route cache retired, but the actual unpaid parking reservation did not.
                // 路径缓存失效不等于取消真实站位；其他路线仍必须避让。
                Assert.That(CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(pickup, "reservations")[id], Is.SameAs(live));
                Assert.That(live.GetType().GetField("Generation").GetValue(live), Is.EqualTo(generation));
                Assert.That(live.GetType().GetField("Paid").GetValue(live), Is.False);
                var obstacles = (Vector3[])Call("RouteObstacles", "unrelated-route-probe", null, false);
                CollectionAssert.Contains(obstacles, target, "An actual reserved target remains a route obstacle without a cached path");
                CollectionAssert.DoesNotContain((Vector3[])Call("RouteObstacles", id, null, false), target, "The owner keeps access to its own target");
                CollectionAssert.AreEqual(tokens, customers.Capacity.GetReservations().Select(r => r.Token));
                Assert.That(owner.Snapshot.PaidVisitIds, Is.Empty);
                customers.ReleasePickupAssignment(id);
                Assert.That(CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(pickup, "reservations").Contains(id), Is.False);
                CollectionAssert.DoesNotContain((Vector3[])Call("RouteObstacles", "unrelated-route-probe", null, false), target, "Real release retires the obstacle");
            }
            finally
            {
                if (obstruction != null) { world.Unregister(obstruction); UnityEngine.Object.DestroyImmediate(obstruction.gameObject); }
                CafeLoopRuntimeProof.Set(owner, "stepping", false);
            }
        }
        [UnityTest] public IEnumerator ActiveCollectedExitProtectsItsFutureEndpointUntilActualDeparture()
            => ObserveActiveCollectedExit(false);
        [UnityTest] public IEnumerator CancelledCollectedExitRetiresRouteWithoutFakingDeparture()
            => ObserveActiveCollectedExit(true);

        private IEnumerator ObserveActiveCollectedExit(bool cancel)
        {
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(), Is.True, customers.Status);
            var id = customers.Snapshot.Single().VisitId;
            yield return Until(() => owner.Snapshot.Orders.Values.Any(o => o.State == OrderState.ReadyForPickup) &&
                employees.Snapshot.All(e => e.State == EmployeeState.Idle));
            tools.ResumeCollection();
            var boundExitVisits = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers, "visits");
            var boundExitCore = CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers, "traffic");
            var boundExitEntries = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(boundExitCore, "entries");
            var boundExitNative = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world.Service, "byId");
            // Exiting declares a journey; retain the original wait until its actual Native leg is bound/live.
            // 原6000窗等同一visit的实际Bound请求，不把声明阶段当作已发出移动。
            yield return Until(() =>
            {
                if (!boundExitVisits.Contains(id)) return false;
                var current = boundExitVisits[id]; var requestId = (long)MovementRead(current, "RequestId");
                var move = MovementRead(current, "TrafficMove");
                if ((CustomerVisitState)MovementRead(current, "State") != CustomerVisitState.Exiting || requestId <= 0 ||
                    move == null || !boundExitNative.Contains(requestId)) return false;
                var key = (TrafficIntentKey)MovementRead(move, "Intent");
                var leg = boundExitEntries.Contains(key) ? MovementRead(boundExitEntries[key], "Leg") : null;
                if (key.IntentId != "actual-exit" || leg == null) return false;
                var stamp = (TrafficNativeStamp)MovementRead(leg, "Stamp");
                return (bool)MovementRead(leg, "Committed") && (bool)MovementRead(leg, "Bound") &&
                    ReferenceEquals(MovementRead(leg, "ActorLife"), MovementRead(current, "Actor")) &&
                    stamp.RequestId == requestId && stamp.LegGeneration == (int)MovementRead(move, "NativeGeneration");
            });
            var customer = world.RegisteredActors.Single(a => a.ActorId == id);
            var barista = world.RegisteredActors.Single(a => a.ActorId == employees.Snapshot.Single(e => e.Role == EmployeeRole.Barista).ActorId);
            var departure = CafeLoopRuntimeProof.Field<Transform>(customers, "exit");
            var target = new NavigationTarget(departure.position);
            var path = adapter.GetCompletePath(barista.transform.position, target.Position);
            bool ReturnClear() => (bool)typeof(CustomerFlowController).GetMethod("IsPickupReturnSegmentClear",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(customers, new object[] { barista.ActorId, target });
            var visit = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers, "visits")[id];
            var request = (long)visit.GetType().GetField("RequestId").GetValue(visit);
            Assert.That(request, Is.GreaterThan(0));
            Assert.That(CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world.Service, "byId").Contains(request), Is.True);
            Assert.That(customer.Agent.hasPath && !customer.Agent.isStopped, Is.True, "A real collected customer owns an accepted exit request");
            Assert.That(owner.Snapshot.Orders.Values.Single().State, Is.EqualTo(OrderState.Completed));
            var occupied = world.RegisteredActors.Where(a => a != barista).Select(a => a.transform.position).ToArray();
            Assert.That(CustomerRoutePlanner.IsClear(path, occupied, .911f), Is.True,
                "The current bodies do not explain this refusal; the future exit endpoint must be protected");
            Assert.That(ReturnClear(), Is.False,
                "A later employee route must not occupy the endpoint of an active collected exit");
            var unrelated = new NavigationTarget(barista.transform.position + Vector3.left * .5f);
            Assert.That((bool)typeof(CustomerFlowController).GetMethod("IsPickupReturnSegmentClear",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(customers, new object[] { barista.ActorId, unrelated }), Is.True,
                "An independent nonconflicting real route remains available");
            if (cancel)
            {
                Assert.That(world.Service.Cancel(request), Is.True);
                Assert.That(customers.Snapshot.Single().State, Is.EqualTo(CustomerVisitState.Blocked));
                Assert.That(ReturnClear(), Is.True, "A cancelled request must not retain stale future route authority");
                Assert.That(customers.Capacity.GetCapacities().Single(c => c.Kind == AnimalCafe.Capacity.CapacityKind.TotalCustomers).Used, Is.EqualTo(1),
                    "Cancellation must not pretend that a collected customer actually departed");
                yield break;
            }
            var actualExit = false; exitTrafficObserving = true;
            var observer = customer.gameObject.AddComponent<CafeActualExitObserver>(); observer.VisitId = id;
            observer.Receipt = (_, position) =>
            { if (!exitTrafficObserving) return; Assert.That(Vector3.Distance(position, departure.position), Is.LessThanOrEqualTo(.081f)); actualExit = true; };
            try { yield return Until(() => customers.Snapshot.Count == 0); yield return null; }
            finally { exitTrafficObserving = false; }
            Assert.That(actualExit, Is.True);
            Assert.That(customers.Capacity.GetCapacities().All(c => c.Used == 0), Is.True);
            Assert.That(ReturnClear(), Is.True,
                "Actual departure must retire the route, rather than permanently denying later movement");
        }

        [UnityTest, Timeout(600000)] public IEnumerator CenterCounterContinuousBusinessNormal() => ObserveCenterCounterBusiness(GameSpeed.Normal);
        [UnityTest, Timeout(600000)] public IEnumerator CenterCounterContinuousBusinessFast() => ObserveCenterCounterBusiness(GameSpeed.Fast);

        private IEnumerator ObserveCenterCounterBusiness(GameSpeed speed)
        {
            // The same real Confirm and grid position as the delegated UI failure.
            // 复用正式装修确认；不移动角色、不缩小身体、不自动恢复 Blocked。
            yield return AddCounters(new GridPosition(4, 4));
            Find<DecorationModeController>().ExitDecorationMode();
            yield return Until(() => owner.CanProgress && employees.Snapshot.All(e => e.State == EmployeeState.Idle));
            double now = 0;
            var day = owner.gameObject.AddComponent<BusinessDayController>();
            day.Initialize(owner, new BusinessDaySettings(60, 15), () => now, true);
            var seen = new HashSet<string>(); var exited = new HashSet<string>();
            var departure = CafeLoopRuntimeProof.Field<Transform>(customers, "exit");
            centerTrafficObserving = true; var paused = false; var done = false;
            var waitCore=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var waitTrace=Environment.GetCommandLineArgs().Contains("-serviceTrafficWaitTrace")?new TrafficWaitFailureTrace(waitCore):null;
            var priorWaitIntent=CafeLoopRuntimeProof.Field<string>(waitCore,"WaitFailureDiagnosticIntent");
            if(waitTrace!=null)CafeLoopRuntimeProof.Set(waitCore,"WaitFailureDiagnosticIntent","*");
            var history = new Queue<string>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var customerHook = typeof(CustomerFlowController).GetField("CorridorObserver", flags);
            var employeeHook = typeof(EmployeeFlowController).GetField("MovementObserver", flags);
            var witness = Environment.GetCommandLineArgs().Contains("-manualExitWitness");
            void Record(string value)
            {
                if (history.Count == 300) history.Dequeue();
                history.Enqueue("frame=" + Time.frameCount + " clock=" + now.ToString("F3") + " " + value + " bodies=" +
                    string.Join(";", world.RegisteredActors.Select(a => a.ActorId + "@" + a.transform.position.ToString("F6"))));
            }
            try
            {
                if (witness)
                {
                    customerHook.SetValue(customers, (Action<string, string, string, Vector3, IReadOnlyList<Vector3>, Vector3[], string>)
                        ((operation, id, related, target, path, obstacles, result) =>
                        {
                            if (operation == "segment.issue" || operation == "segment.callback-result" || operation == "employee.moving-conflict")
                                Record("CUSTOMER " + operation + " id=" + id + " related=" + related + " target=" + target.ToString("F6") +
                                    " path=" + (path == null ? "NULL" : string.Join(";", path.Select(p => p.ToString("F6")))) + " result=" + result);
                        }));
                    employeeHook.SetValue(employees, (Action<string, string, string>)((id, operation, result) =>
                    { if (operation == "submitted" || operation == "callback") Record("EMPLOYEE " + id + " " + operation + " " + result); }));
                }
                tools.ResumeCollection(); tools.ResumeArrivals();
                if (speed == GameSpeed.Fast) time.SetFast(); else time.SetNormal();
                // Normal observed about 8-10 departures per 100-120 wall seconds.
                // 普通速度保留60人目标，给真实Closing/Rest留足有限测试时间。
                var frameLimit = speed == GameSpeed.Normal ? 60000 : 40000;
                for (var frame = 0; frame < frameLimit; frame++)
                {
                    now += 1d / 60; // Deterministic wall-clock source, independent of work speed.
                    foreach (var visit in customers.Snapshot)
                    {
                        if (!seen.Add(visit.VisitId)) continue;
                        var body = world.RegisteredActors.Single(a => a.ActorId == visit.VisitId);
                        var observer = body.gameObject.AddComponent<CafeActualExitObserver>();
                        observer.VisitId = visit.VisitId;
                        observer.Receipt = (id, position) =>
                        {
                            if (!centerTrafficObserving) return;
                            Assert.That(Vector3.Distance(position, departure.position), Is.LessThanOrEqualTo(.081f));
                            Assert.That(exited.Add(id), Is.True, "Each original body departs once");
                        };
                    }
                    Assert.That(customers.Snapshot.All(v => v.State != CustomerVisitState.Blocked), Is.True,
                        "Natural customer failure: " + customers.Status);
                    Assert.That(employees.Snapshot.All(e => e.State != EmployeeState.Blocked), Is.True,
                        "Natural employee failure: " + string.Join(";", employees.Snapshot.Select(e => e.Failure)));
                    if (frame % 1200 == 0)
                        Debug.Log("CENTER_PROGRESS " + speed + " frame=" + frame + " wall=" + now.ToString("F1") +
                            " seen=" + seen.Count + " actualExits=" + exited.Count + " day=" + day.Snapshot.DayNumber + "/" + day.Snapshot.Phase +
                            " visits=" + customers.Snapshot.Count + " staff=" + string.Join(";", employees.Snapshot.Select(e => e.State)));
                    if (!paused && seen.Count >= 8)
                    {
                        paused = true; time.SetPaused(); var poses = world.RegisteredActors.Select(a => a.transform.position).ToArray();
                        for (var p = 0; p < 12; p++) { now += 1d / 60; yield return null; }
                        CollectionAssert.AreEqual(poses, world.RegisteredActors.Select(a => a.transform.position));
                        if (speed == GameSpeed.Fast) time.SetFast(); else time.SetNormal();
                    }
                    if (seen.Count >= 60) customers.AutoSpawn = false;
                    if (seen.Count >= 60 && exited.Count == seen.Count && customers.Snapshot.Count == 0 &&
                        employees.Snapshot.All(e => e.State == EmployeeState.Idle) && day.Snapshot.DayNumber >= 4 &&
                        day.Snapshot.Phase == BusinessDayPhase.Rest) { done = true; break; }
                    yield return null;
                }
                Assert.That(done, Is.True, "Sixty real departures and automatic drained rest across at least three days");
                Assert.That(owner.Snapshot.Orders.Count, Is.EqualTo(seen.Count));
                Assert.That(owner.Snapshot.Orders.Values.All(o => o.State == OrderState.Completed), Is.True);
                Assert.That(customers.Capacity.GetCapacities().All(c => c.Used == 0), Is.True);
                TestContext.WriteLine("CENTER_COUNTER " + speed + " actualExits=" + exited.Count + " day=" + day.Snapshot.DayNumber + " clock=" + now);
            }
            finally
            {
                if(waitTrace!=null)
                {
                    TestContext.WriteLine("CENTER_ORIGINAL_WAIT "+CafeLoopRuntimeProof.Field<string>(waitCore,"WaitFailureDiagnostic"));
                    CafeLoopRuntimeProof.Set(waitCore,"WaitFailureDiagnosticIntent",priorWaitIntent);waitTrace.Dispose();
                }
                centerTrafficObserving = false;
                customerHook.SetValue(customers, null); employeeHook.SetValue(employees, null);
                if (witness) TestContext.WriteLine(string.Join("\n", history));
            }
        }
        [UnityTest, Timeout(600000)]
        public IEnumerator ExistingCounterAdvancesIndependentlyOfRealCancelledPickupService()
        {
            // Fault A's first real bound service leg before B's paid proposal exists.
            // A未取杯且保留原lease/Ready杯/身体；B真实转队后C独立前移，不伪造gap。
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            object Read(object value, string name) => value == null ? null : MovementRead(value, name);
            var core = CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers, "traffic");
            var visits = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers, "visits");
            var entries = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(core, "entries");
            var counter = CafeLoopRuntimeProof.Field<CounterQueueService>(customers, "queue");
            var hook = typeof(CustomerFlowController).GetField("CorridorObserver", flags);
            var previous = (Action<string, string, string, Vector3, IReadOnlyList<Vector3>, Vector3[], string>)hook.GetValue(customers);
            var currentMethod = typeof(CustomerFlowController).GetMethod("CounterAdvanceAnchorCurrent", flags);
            Action<string, string, string, Vector3, IReadOnlyList<Vector3>, Vector3[], string> installed = null;
            var errors = new List<string>(); var observing = true; var attempted = false; var faulted = false;
            string a = null, b = null, c = null; object aVisit = null, bVisit = null, cVisit = null, aAdmission = null, cAdmission = null, aEntry = null, cEntry = null, anchor = null;
            NavigationActor aBody = null, cBody = null; CounterQueueService.AssignmentClaim originalClaim = null;
            PickupQueueService pickup = null; PickupQueueService.ReservationClaim aClaim = null; QueueAssignment aAssignment = default;
            var display = Find<PickupCupDisplay>(); CafeDeliverySnapshot aDelivery = default; GameObject aCup = null;
            AnimalCafe.Capacity.CapacityToken aPickup = null; var gap = false; var serviceSubmissions = 0;
            AnimalCafe.Capacity.CapacityToken aTotal = null, cTotal = null;
            TrafficIntentKey aKey = default, cKey = default; QueueAssignment originalC = default, nextC = default;
            TrafficDecision aStopped = null, safeGrant = null; long faultRequest = 0;
            int cancelledCallbacks = 0, issued = 0, arrived = 0, aGeneration = 0, aFirst = 0, aQueries = 0, cFirst = 0;
            Vector3 aPose = default; var stage = "ordinary natural Counter preflight";
            var timelineIds = new List<string>(); var timelineSeen = new HashSet<string>(StringComparer.Ordinal);
            // Fingerprint excludes continuous time/pose and exact Native IDs; only changed discrete facts print.
            // 最多64条不同状态；仅该case读原Visit/Counter字段，不query/refresh/pump或flush。
            void Timeline(string operation, string preflightActor = null)
            {
                if (timelineIds.Count == 0 || timelineSeen.Count >= 64) return;
                var rows = counter.Snapshot.ToArray(); var fingerprints = new List<string>(); var details = new List<string>();
                foreach (var id in timelineIds)
                {
                    var value = visits.Contains(id) ? visits[id] : null; var body = Read(value, "Actor") as NavigationActor;
                    var request = value == null ? 0L : (long)Read(value, "RequestId"); var move = Read(value, "TrafficMove");
                    var state = value == null ? "ABSENT" : Read(value, "State").ToString();
                    var collected = value != null && (bool)Read(value, "HasCollected"); var departed = value != null && (bool)Read(value, "PickupDeparted");
                    var intent = move == null ? "NONE" : ((TrafficIntentKey)Read(move, "Intent")).IntentId;
                    var row = rows.FirstOrDefault(item => item.VisitId == id);
                    var assignment = row.VisitId == null ? "NONE" : row.SlotIndex + "/g" + row.Generation + "/arrived=" + row.Arrived;
                    var discrete = id + " state=" + state + " collected=" + collected + " pickupDeparted=" + departed +
                        " hasRequest=" + (request > 0) + " intent=" + intent + " counter=" + assignment;
                    fingerprints.Add(discrete);
                    details.Add(discrete + " request=" + request + " pose=" + (body == null ? "UNKNOWN" : body.transform.position.ToString("F6")));
                }
                var fingerprint = operation + "/" + preflightActor + "/" + string.Join(";", fingerprints);
                if (!timelineSeen.Add(fingerprint)) return;
                TestContext.WriteLine("I02_TIMELINE n=" + timelineSeen.Count + " frame=" + Time.frameCount +
                    " sceneTime=" + Time.timeSinceLevelLoad.ToString("R") + " op=" + operation + " preflightActor=" + preflightActor + " " + string.Join(" | ", details));
            }
            bool Ready(NavigationActor body) => body != null && body.isActiveAndEnabled && body.Agent != null &&
                body.Agent.enabled && body.Agent.isOnNavMesh && world.RegisteredActors.Contains(body);
            bool Current() => anchor != null && (bool)currentMethod.Invoke(customers, new[] { anchor });
            AnimalCafe.Capacity.ReservationSnapshot Total(string id) => customers.Capacity.GetReservations().Single(row =>
                row.OwnerId == id && row.Kind == AnimalCafe.Capacity.CapacityKind.TotalCustomers);
            void Healthy()
            {
                Assert.That(owner.CanProgress && customers.isActiveAndEnabled && world.LayoutAvailable && world.isActiveAndEnabled && adapter.isActiveAndEnabled, Is.True,
                    "NOT_PROVEN: shared readiness unavailable / " + stage);
                Assert.That(employees.Snapshot.All(row => row.State != EmployeeState.Blocked), Is.True, "NOT_PROVEN: employee failure");
                Assert.That(customers.Snapshot.All(row => row.VisitId == a || row.State != CustomerVisitState.Blocked), Is.True, "NOT_PROVEN: another customer failed");
                Assert.That(world.RegisteredActors.All(Ready), Is.True, "NOT_PROVEN: unavailable body would test a global hold");
            }
            void PreserveFault()
            {
                Healthy(); Assert.That(visits[a], Is.SameAs(aVisit)); Assert.That(visits[c], Is.SameAs(cVisit));
                Assert.That(Read(aVisit, "Actor"), Is.SameAs(aBody)); Assert.That(Read(cVisit, "Actor"), Is.SameAs(cBody));
                Assert.That(Read(aVisit, "Admission"), Is.SameAs(aAdmission)); Assert.That(Read(cVisit, "Admission"), Is.SameAs(cAdmission));
                Assert.That(customers.Snapshot.Single(row => row.VisitId == a).State, Is.EqualTo(CustomerVisitState.Blocked));
                Assert.That(aBody.transform.position, Is.EqualTo(aPose)); Assert.That(Read(aVisit, "Generation"), Is.EqualTo(aGeneration));
                Assert.That(Read(aVisit, "HasCollected"), Is.EqualTo(false)); Assert.That(Read(aVisit, "PickupDeparted"), Is.EqualTo(false));
                Assert.That(Read(aVisit, "ServiceArrived"), Is.EqualTo(false)); Assert.That(Read(aVisit, "PickupAssigned"), Is.EqualTo(true));
                Assert.That(entries[aKey], Is.SameAs(aEntry)); Assert.That(core.GetDecision(aKey), Is.SameAs(aStopped));
                Assert.That(Read(aEntry, "FirstFrame"), Is.EqualTo(aFirst)); Assert.That(core.QueriesConsumed(aKey), Is.EqualTo(aQueries));
                Assert.That(Total(a).Token, Is.SameAs(aTotal)); Assert.That(Total(a).State, Is.EqualTo(AnimalCafe.Capacity.ReservationState.Occupied));
                Assert.That(Total(c).Token, Is.SameAs(cTotal)); Assert.That(Total(c).State, Is.EqualTo(AnimalCafe.Capacity.ReservationState.Occupied));
                Assert.That(counter.TryCaptureAssignment(c, out var claim), Is.True); Assert.That(originalClaim.IsSameReservation(claim), Is.True);
                Assert.That(pickup.TryCaptureReservation(a, out var currentA), Is.True); Assert.That(aClaim.IsSameCurrentClaim(currentA), Is.True);
                Assert.That(currentA.IsCurrentCollectionLease, Is.True); Assert.That(currentA.Assignment.Equals(aAssignment), Is.True);
                Assert.That(pickup.ServiceLeases.TryGetValue(a, out var lease), Is.True); Assert.That(lease, Is.EqualTo(aAssignment.Position));
                var held = customers.Capacity.GetReservations().Single(row => row.OwnerId == a && row.Kind == AnimalCafe.Capacity.CapacityKind.PickUp);
                Assert.That(held.Token, Is.SameAs(aPickup)); Assert.That(held.State, Is.EqualTo(AnimalCafe.Capacity.ReservationState.Occupied));
                Assert.That(owner.Snapshot.Orders[aDelivery.Key].State, Is.EqualTo(OrderState.ReadyForPickup));
                Assert.That(owner.Snapshot.Delivery.Single(row => row.Key.Equals(aDelivery.Key)).Equals(aDelivery), Is.True);
                Assert.That(aCup != null && display.VisibleOrders.TryGetValue(aDelivery.Key, out var cup) && ReferenceEquals(cup, aCup), Is.True);
                Assert.That(customers.AutoSpawn, Is.False); Assert.That(owner.HoldCollection, Is.True);
            }
            void CaptureNaturalGap()
            {
                if (gap || !faulted || !(bool)Read(bVisit, "QueueLeft") || counter.Snapshot.Any(row => row.VisitId == b)) return;
                var row = counter.Snapshot.Single(value => value.VisitId == c);
                if (!row.Arrived || (CustomerVisitState)Read(cVisit, "State") != CustomerVisitState.Queued) return;
                Assert.That(owner.Snapshot.PaidVisitIds.Contains(b), Is.True);
                Assert.That(Read(Read(bVisit, "PaidPickup"), "Transferred"), Is.EqualTo(true)); Assert.That(Read(bVisit, "PickupInTransit"), Is.EqualTo(true));
                Assert.That(row.SlotIndex, Is.EqualTo(1)); Assert.That(row.Generation, Is.EqualTo(originalC.Generation));
                Assert.That(counter.TryCaptureAssignment(c, out var actual), Is.True); Assert.That(actual.IsCurrent && originalClaim.IsSameReservation(actual), Is.True);
                Assert.That(counter.TryPrepareMutation(CounterQueueService.MutationKind.Advance, c, counter.Version, out var preview), Is.True);
                Assert.That(preview.IsCurrent, Is.True); Assert.That(preview.Assignment.SlotIndex, Is.Zero); Assert.That(preview.Assignment.Generation, Is.EqualTo(row.Generation + 1));
                var others = world.RegisteredActors.Where(body => body != cBody).ToArray();
                var occupied = others.Select(body => body.transform.position).ToArray();
                var clearance = cBody.Settings.AgentRadius + others.Max(body => body.Settings.AgentRadius) +
                    Math.Max(cBody.Settings.CollisionSkin + cBody.Settings.Epsilon, others.Max(body => body.Settings.CollisionSkin + body.Settings.Epsilon));
                var route = new CustomerRoutePlanner().Build(cBody.transform.position, preview.Assignment.Position, occupied, clearance, adapter.GetCompletePath);
                Assert.That(route, Is.Not.Null, "NOT_PROVEN: C requires a complete current Native route with every real body including stopped A");
                var expanded = new List<Vector3> { cBody.transform.position };
                foreach (var target in route)
                {
                    var segment = adapter.GetCompletePath(expanded.Last(), target);
                    Assert.That(segment, Is.Not.Null, "NOT_PROVEN: every witness segment must be a current complete owned Native path");
                    expanded.AddRange(segment.Skip(1));
                }
                expanded.Add(preview.Assignment.Position);
                Assert.That(CustomerRoutePlanner.IsClear(expanded, occupied, clearance), Is.True, "NOT_PROVEN: current full C route must be clear of retained actual bodies");
                originalC = row; originalClaim = actual; nextC = preview.Assignment; gap = true;
                TestContext.WriteLine("I02_NATURAL_COUNTER_GAP BActualPaidTransfer=True C=" + c + " canonicalPreview=True currentNativeBodyClear=True");
            }
            // The ordinary helper may refresh then commit between preflight and Native submission.
            // 真实jointcommit后以Consumed preview/当前typed selection认证原来源，不要求已清除的pending仍Current。
            void CaptureCommittedGrant(object move, TrafficDecision source)
            {
                var entry = entries[cKey]; var request = Read(entry, "JourneyRequest") as TrafficRequest;
                Assert.That(request, Is.Not.Null); Assert.That(Read(entry, "JourneyPermit"), Is.SameAs(source));
                Assert.That(source.Kind, Is.EqualTo(TrafficDecisionKind.Granted)); Assert.That(source.Intent.Equals(cKey), Is.True);
                Assert.That(source.AcquiredSequence, Is.GreaterThan(0)); Assert.That(Read(anchor, "Source"), Is.SameAs(source));
                Assert.That(((TrafficIntentKey)Read(anchor, "Key")).Equals(cKey), Is.True);
                Assert.That(Read(anchor, "Actor"), Is.SameAs(cBody)); Assert.That(Read(anchor, "Admission"), Is.SameAs(cAdmission));
                Assert.That(Read(anchor, "Total"), Is.SameAs(cTotal)); Assert.That(Read(anchor, "Counter"), Is.SameAs(counter));
                var preview = (CounterQueueService.PreparedMutation)Read(anchor, "Preview");
                Assert.That(Read(preview, "Consumed"), Is.EqualTo(true)); Assert.That(Read(preview, "owner"), Is.SameAs(counter));
                Assert.That(Read(preview, "Kind").ToString(), Is.EqualTo("Advance")); Assert.That(Read(preview, "Id"), Is.EqualTo(c));
                var selection = Read(cVisit, "TrafficSelection"); var current = counter.Snapshot.Single(row => row.VisitId == c);
                var claim = (CounterQueueService.AssignmentClaim)Read(selection, "CounterEntry");
                Assert.That(Read(selection, "Source"), Is.SameAs(source)); Assert.That(Read(selection, "Counter"), Is.SameAs(counter));
                Assert.That(((TrafficIntentKey)Read(selection, "Intent")).Equals(cKey), Is.True);
                Assert.That(claim.IsCurrent && originalClaim.IsSameReservation(claim), Is.True);
                Assert.That(Read(selection, "Assignment"), Is.EqualTo(current)); Assert.That(current.Equals(preview.Assignment), Is.True);
                Assert.That(current.Generation, Is.EqualTo(originalC.Generation + 1)); Assert.That(current.SlotIndex, Is.Zero);
                Assert.That(request.Intent.Equals(cKey), Is.True); Assert.That(request.RequestedFrame, Is.EqualTo(cFirst));
                Assert.That(request.Start, Is.EqualTo((Vector3)Read(anchor, "Start"))); Assert.That(request.Start, Is.EqualTo(cBody.transform.position));
                Assert.That(request.Target, Is.EqualTo(current.Position)); Assert.That(cKey.LayoutRevision, Is.EqualTo(adapter.Revision));
                var aFact = request.Snapshot.Bodies.Single(row => row.Owner.ActorId == a);
                Assert.That(aFact.ActorLife, Is.SameAs(aBody)); Assert.That(aFact.Position, Is.EqualTo(aPose)); Assert.That(aFact.Enabled && aFact.ActorActive, Is.True);
                var proof = source.ProofRoutes.Single(row => row.ResourceId == "counter/" + c);
                Assert.That(proof.ExpandedPath, Is.Not.Empty); Assert.That(proof.Waypoints.Last(), Is.EqualTo(request.Target));
                var currentPath = new[] { request.Start }.Concat(proof.ExpandedPath).Concat(new[] { request.Target }).ToArray();
                foreach (var body in world.RegisteredActors.Where(body => body != cBody))
                    Assert.That(CustomerRoutePlanner.IsClear(currentPath, new[] { body.transform.position },
                        cBody.Settings.AgentRadius + body.Settings.AgentRadius + Math.Max(cBody.Settings.CollisionSkin + cBody.Settings.Epsilon, body.Settings.CollisionSkin + body.Settings.Epsilon)), Is.True,
                        "NOT_PROVEN: authentic committed Core primary must remain clear of every actual body, including stopped A");
                Assert.That(gap, Is.True); Assert.That(current.Equals(nextC), Is.True);
                cEntry = entry; nextC = current; safeGrant = source;
                TestContext.WriteLine("I02_COMMITTED_SAFE_GRANT C=" + c + " first=" + cFirst + " acquired=" + source.AcquiredSequence + " currentTypedClaim=True currentBodyClear=True");
            }
            void Observe(string operation, string id, string related, Vector3 target, IReadOnlyList<Vector3> path, Vector3[] obstacles, string result)
            {
                if (!observing) return;
                try
                {
                    if (operation == "traffic.counter-advance-preflight") Timeline("preflight", id);
                    if (id == a && faultRequest > 0 && operation == "segment.callback-result" && result.Contains("request=" + faultRequest + " ") && result.Contains("status=Cancelled"))
                    { Assert.That(Ready(aBody), Is.True); cancelledCallbacks++; }
                    if (aVisit != null && bVisit != null && cVisit != null && id == a && operation == "segment.submitted" && (CustomerVisitState)Read(aVisit, "State") == CustomerVisitState.MovingToPickupService && !attempted)
                    {
                        attempted = true; serviceSubmissions++; stage = "first real service Bound before B paid proposal birth"; Healthy();
                        var paid = Read(bVisit, "PaidPickup"); var bMove = Read(bVisit, "TrafficMove");
                        var paidKey = paid == null ? default(TrafficIntentKey) : (TrafficIntentKey)Read(paid, "ActiveIntent");
                        Assert.That(paid == null || (!entries.Contains(paidKey) && !(bool)Read(paid, "Transferred") &&
                            (bMove == null || !((TrafficIntentKey)Read(bMove, "Intent")).Equals(paidKey))), Is.True,
                            "NOT_PROVEN: B must not already own a paid source/movement with frozen dependencies");
                        var move = Read(aVisit, "TrafficMove"); aKey = (TrafficIntentKey)Read(move, "Intent");
                        Assert.That(aKey.IntentId, Is.EqualTo("pickup-service")); var source = (TrafficDecision)Read(move, "Source");
                        Assert.That(source.Kind, Is.EqualTo(TrafficDecisionKind.Granted)); Assert.That(source.AcquiredSequence, Is.GreaterThan(0));
                        aEntry = entries[aKey]; Assert.That(Read(aEntry, "JourneyPermit"), Is.SameAs(source));
                        var selection = Read(aVisit, "TrafficSelection"); var selected = (PickupQueueService.ReservationClaim)Read(selection, "PickupEntry");
                        Assert.That(Read(selection, "Source"), Is.SameAs(source)); Assert.That(Read(selection, "Pickup"), Is.SameAs(pickup));
                        Assert.That(selected.IsCurrentCollectionLease, Is.True); Assert.That(pickup.TryCaptureReservation(a, out aClaim), Is.True);
                        Assert.That(selected.IsSameCurrentClaim(aClaim), Is.True); aAssignment = aClaim.Assignment;
                        Assert.That(pickup.ServiceLeases.TryGetValue(a, out var lease), Is.True); Assert.That(lease, Is.EqualTo(aAssignment.Position));
                        Assert.That(source.ProofRoutes.Single(value => value.ResourceId == "pickup/" + a).Waypoints.Last(), Is.EqualTo(aAssignment.Position));
                        Assert.That(Read(aVisit, "HasCollected"), Is.EqualTo(false)); Assert.That(Read(aVisit, "ServiceArrived"), Is.EqualTo(false));
                        Assert.That(owner.Snapshot.Orders[aDelivery.Key].State, Is.EqualTo(OrderState.ReadyForPickup));
                        var leg = Read(aEntry, "Leg"); Assert.That(Read(leg, "Committed"), Is.EqualTo(true)); Assert.That(Read(leg, "Bound"), Is.EqualTo(true)); Assert.That(Read(leg, "ActorLife"), Is.SameAs(aBody));
                        faultRequest = ((TrafficNativeStamp)Read(leg, "Stamp")).RequestId; Assert.That(faultRequest, Is.GreaterThan(0));
                        var live = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world.Service, "byActor");
                        Assert.That(live.Contains(a), Is.True); Assert.That(Read(live[a], "Id"), Is.EqualTo(faultRequest));
                        Assert.That(world.Service.Cancel(faultRequest), Is.True); Assert.That(cancelledCallbacks, Is.EqualTo(1)); Assert.That(errors, Is.Empty);
                        Assert.That(customers.Snapshot.Single(row => row.VisitId == a).State, Is.EqualTo(CustomerVisitState.Blocked));
                        aPose = aBody.transform.position; aGeneration = (int)Read(aVisit, "Generation"); aFirst = (int)Read(aEntry, "FirstFrame");
                        aQueries = core.QueriesConsumed(aKey); aStopped = core.GetDecision(aKey); faulted = true;
                        TestContext.WriteLine("I02_SERVICE_FAULT A=" + a + " B=" + b + " C=" + c + " request=" + faultRequest + " BOriginalPaidSourceAbsent=True");
                    }
                    if (!faulted || id != c) return;
                    if (operation == "traffic.counter-advance-preflight")
                    {
                        CaptureNaturalGap(); var pending = Read(cVisit, "PendingCounterAdvance");
                        Assert.That(pending, Is.Not.Null); if (anchor == null) { anchor = pending; cKey = (TrafficIntentKey)Read(anchor, "Key"); cFirst = (int)Read(anchor, "FirstFrame"); }
                        Assert.That(pending, Is.SameAs(anchor)); Assert.That(((TrafficIntentKey)Read(pending, "Key")).Equals(cKey), Is.True);
                        Assert.That(cKey.ActorId, Is.EqualTo(c)); Assert.That(cKey.IntentId, Is.EqualTo("counter-advance")); Assert.That(Current(), Is.True);
                    }
                    if (operation == "segment.submitted" && (CustomerVisitState)Read(cVisit, "State") == CustomerVisitState.Advancing)
                    {
                        var move = Read(cVisit, "TrafficMove"); Assert.That(((TrafficIntentKey)Read(move, "Intent")).Equals(cKey), Is.True);
                        var source = (TrafficDecision)Read(move, "Source"); if (safeGrant == null) CaptureCommittedGrant(move, source);
                        Assert.That(safeGrant, Is.Not.Null); Assert.That(source, Is.SameAs(safeGrant));
                        Assert.That(source.AcquiredSequence, Is.GreaterThan(0)); Assert.That(entries[cKey], Is.SameAs(cEntry));
                        Assert.That(Read(cEntry, "JourneyPermit"), Is.SameAs(source)); Assert.That(counter.Snapshot.Single(row => row.VisitId == c).Generation, Is.EqualTo(nextC.Generation));
                        var leg = Read(cEntry, "Leg"); Assert.That(Read(leg, "Committed"), Is.EqualTo(true)); Assert.That(Read(leg, "Bound"), Is.EqualTo(true)); Assert.That(Read(leg, "ActorLife"), Is.SameAs(cBody));
                        var stamp = (TrafficNativeStamp)Read(leg, "Stamp"); var live = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world.Service, "byActor");
                        Assert.That(live.Contains(c), Is.True); Assert.That(Read(live[c], "Id"), Is.EqualTo(stamp.RequestId)); Assert.That(stamp.RequestId, Is.GreaterThan(0)); issued++;
                    }
                    if (operation == "segment.callback-result" && result.Contains("status=Arrived") &&
                        counter.Snapshot.Any(row => row.VisitId == c && row.SlotIndex == 0 && row.Generation == nextC.Generation && row.Arrived)) arrived++;
                }
                catch (Exception error) { errors.Add(stage + " / " + operation + ": " + error); }
            }
            try
            {
                Assert.That(currentMethod, Is.Not.Null); installed = previous + Observe; hook.SetValue(customers, installed);
                tools.HoldArrivals(); tools.HoldCollection();
                stage = "A admission then real Ready Pickup arrival"; TestContext.WriteLine("I02_PREP " + stage);
                yield return AwaitTrafficAdmission(); var prepA = customers.Snapshot.Single().VisitId; a = prepA;
                timelineIds.Add(prepA);
                yield return Until(() => customers.PickupAssignments.Any(row => row.VisitId == prepA && row.Arrived) &&
                    owner.Snapshot.Orders.Values.Any(row => row.CustomerId == prepA && row.State == OrderState.ReadyForPickup) &&
                    employees.Snapshot.All(row => row.State == EmployeeState.Idle));
                aVisit = visits[a]; aBody = (NavigationActor)Read(aVisit, "Actor"); aAdmission = Read(aVisit, "Admission"); aTotal = Total(a).Token;
                pickup = CafeLoopRuntimeProof.Field<PickupQueueService>(customers, "pickupQueue"); aDelivery = owner.Snapshot.Delivery.Single(row => row.VisitId == a);
                Assert.That(display.VisibleOrders.TryGetValue(aDelivery.Key, out aCup), Is.True);
                aPickup = customers.Capacity.GetReservations().Single(row => row.OwnerId == a && row.Kind == AnimalCafe.Capacity.CapacityKind.PickUp).Token;
                stage = "consecutive original B/C admissions"; TestContext.WriteLine("I02_PREP " + stage);
                yield return AwaitTrafficAdmission(); var prepB = customers.Snapshot.Single(row => row.VisitId != prepA).VisitId;
                timelineIds.Add(prepB);
                yield return AwaitTrafficAdmission(); var prepC = customers.Snapshot.Single(row => row.VisitId != prepA && row.VisitId != prepB).VisitId;
                timelineIds.Add(prepC);
                b = prepB; c = prepC; bVisit = visits[b]; cVisit = visits[c]; cBody = (NavigationActor)Read(cVisit, "Actor"); cAdmission = Read(cVisit, "Admission"); cTotal = Total(c).Token;
                originalC = counter.Snapshot.Single(row => row.VisitId == c); Assert.That(counter.TryCaptureAssignment(c, out originalClaim), Is.True);
                Assert.That(counter.Snapshot.Single(row => row.VisitId == prepC).SlotIndex, Is.EqualTo(1), "NOT_PROVEN: C did not obtain the original occupied Counter slot1");
                Assert.That(counter.Snapshot.Any(row => row.VisitId == prepB && row.SlotIndex == 0), Is.True,
                    "NOT_PROVEN: B no longer owns its original Counter slot0 at C's actual admission");
                TestContext.WriteLine("I02_PREP_REAL_ADMISSION A=" + prepA + " B=" + prepB + " C=" + prepC +
                    " BState=" + customers.Snapshot.Single(row => row.VisitId == prepB).State + " CState=" + customers.Snapshot.Single(row => row.VisitId == prepC).State);
                tools.ResumeCollection(); stage = "first authentic A service Bound";
                for (var frame = 0; frame < 6000 && !faulted; frame++) { Timeline("frame"); Assert.That(errors, Is.Empty); Healthy(); yield return null; }
                Assert.That(errors, Is.Empty); Assert.That(faulted, Is.True, "NOT_PROVEN: no first authentic A service Bound before B paid-source birth"); Assert.That(serviceSubmissions, Is.EqualTo(1));
                tools.HoldArrivals(); tools.HoldCollection(); PreserveFault();
                Assert.That(owner.Snapshot.Orders[aDelivery.Key].State, Is.EqualTo(OrderState.ReadyForPickup));
                Assert.That(display.VisibleOrders[aDelivery.Key], Is.SameAs(aCup));
                var beforeSpawn = MovementAdmissionFacts(); Assert.That(customers.TrySpawn(), Is.False, "Conservative new-admission Blocked gate remains closed");
                Assert.That(MovementAdmissionFacts(), Is.EqualTo(beforeSpawn)); stage = "B actual PaidTransfer and C natural body-clear canonical gap";
                for (var frame = 0; frame < 1200 && !gap; frame++) { PreserveFault(); Assert.That(errors, Is.Empty); CaptureNaturalGap(); if (!gap) yield return null; }
                Assert.That(errors, Is.Empty); Assert.That(gap, Is.True, "NOT_PROVEN: require B actual transfer/departure and C original Arrived canonical preview with complete current Native body-clear route");
                stage = "existing healthy Counter obtains and consumes its original Core grant";
                for (var frame = 0; frame < 600 && arrived == 0; frame++) { PreserveFault(); Assert.That(errors, Is.Empty); yield return null; }
                TestContext.WriteLine("I02_EXISTING_COUNTER naturalGap=" + gap + " CoreSource=" + (safeGrant != null) + " bound=" + issued + " arrived=" + arrived + " AStillBlocked=True ReadyCupRetained=True");
                Assert.That(entries.Keys.Cast<TrafficIntentKey>().Any(key => key.ActorId == c && key.IntentId == "counter-advance"), Is.True, "I-02 RED: unrelated Blocked service A must not suppress C existing canonical Counter advance submission");
                Assert.That(issued, Is.GreaterThan(0), "C must obtain its own current Core-approved primary and real bound request");
                Assert.That(arrived, Is.GreaterThan(0), "C must really Arrive; current proof/Granted alone is insufficient");
                Assert.That(errors, Is.Empty); PreserveFault(); var final = counter.Snapshot.Single(row => row.VisitId == c);
                Assert.That(final.SlotIndex, Is.Zero); Assert.That(final.Generation, Is.EqualTo(nextC.Generation)); Assert.That(final.Arrived, Is.True);
                Assert.That(Vector3.Distance(cBody.transform.position, nextC.Position), Is.LessThanOrEqualTo(cBody.Settings.ArrivalDistance + cBody.Settings.Epsilon));
            }
            finally
            {
                observing = false;
                if (installed != null && customers != null && ReferenceEquals(hook.GetValue(customers), installed)) hook.SetValue(customers, previous);
                else if (installed != null) TestContext.WriteLine("I02_CLEANUP UNKNOWN observer ownership changed; replacement preserved");
                TestContext.WriteLine("I02_LAST_STAGE " + stage + " faulted=" + faulted + " safeGrant=" + (safeGrant != null));
            }
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

namespace AnimalCafe.Tests.PlayMode.Phase14
{
    public sealed partial class CafePickupSideTests
    {
        private Action<string> controlledHandoffCleanup;

        private sealed class PickupEligibilityQueryCosts : IDisposable
        {
            private readonly CustomerFlowController controller;
            private readonly System.Reflection.FieldInfo hook;
            private readonly object prior;
            private readonly Dictionary<string,int> calls=new Dictionary<string,int>();
            private readonly Dictionary<string,int> queries=new Dictionary<string,int>();
            public PickupEligibilityQueryCosts(CustomerFlowController controller)
            {
                this.controller=controller;
                hook=typeof(CustomerFlowController).GetField("CorridorObserver",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                prior=hook.GetValue(controller);Assert.That(prior,Is.Null);
                hook.SetValue(controller,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)Observe);
            }
            private void Observe(string operation,string caller,string related,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                if(operation=="pickup.eligibility-native-query" || operation=="collection.candidate-route" || operation=="collection.before-acquire")
                    calls[operation]=calls.TryGetValue(operation,out var count)?count+1:1;
                if((operation!="plan.exit-direct"&&operation!="plan.exit-boundary-leg") || result==null)return;
                var parts=result.Split(' ');var invocation=parts.First(p=>p.StartsWith("invocation="));
                var purpose=parts.First(p=>p.StartsWith("purpose="));var cost=parts.FirstOrDefault(p=>p.StartsWith("queries="));
                if(cost!=null)queries[purpose+" "+invocation]=int.Parse(cost.Substring("queries=".Length));
            }
            public void Dispose()
            {
                hook.SetValue(controller,prior);
                TestContext.WriteLine("ELIGIBILITY_COST observedCalls="+string.Join(";",calls.Select(p=>p.Key+"="+p.Value))+
                    " exitInvocations="+queries.Count+" exitNativeQueries="+queries.Values.Sum()+" maxPerExit="+(queries.Count==0?0:queries.Values.Max())+
                    " grouped="+string.Join(";",queries.GroupBy(p=>p.Key.Split(' ')[0]).Select(g=>g.Key+":"+g.Count()+"/"+g.Sum(p=>p.Value)))+
                    " hookRestored="+ReferenceEquals(hook.GetValue(controller),prior));
            }
        }

        [UnityTest] public IEnumerator PickupCollectionPreservesFutureCounterEnvelopeAndSafeSideCompletes()
        {
            var random=UnityEngine.Random.state;
            System.Reflection.FieldInfo hook=null;object prior=null;
            CafeActualExitObserver exitObserver=null;
            try
            {
                UnityEngine.Random.InitState(150027);yield return MatrixStart();
                var runtime=Find<CafeLayoutRuntime>();var decor=Find<DecorationModeController>();
                // Same real Long7 recipe as the geometry positive; no artificial route or actor pose.
                // 保留真实付款、Ready和到站流程；只用既有Hold控制准备一个顾客。
                for(var x=2;x<=7;x++)yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(x,5),0);
                var support=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(7,5));
                Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint,"pickup.point",new SurfaceSlotAddress(support.InstanceId,"slot.0")),Is.True);
                Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);decor.ExitDecorationMode();
                yield return Until(()=>owner.CanProgress&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True);
                // Prepare the original unborn traffic intent; the canonical-birth assertion below remains unchanged.
                // 仅等待同一次真实路线准备，不手动pump，不提前出生或重置预算。
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                Assert.That(customers.TrySpawn(),Is.True);var id=customers.Snapshot.Single().VisitId;
                yield return Until(()=>owner.Snapshot.PaidVisitIds.Contains(id)&&
                    owner.Snapshot.Orders.Values.Any(order=>order.CustomerId==id&&order.State==OrderState.ReadyForPickup)&&
                    customers.CafeSnapshot.Single(v=>v.VisitId==id).PickupArrived&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                CafeLoopRuntimeProof.Set(owner,"stepping",true);
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
                object Call(string name,params object[] args)=>typeof(CustomerFlowController).GetMethod(name,flags).Invoke(customers,args);
                var visit=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[id];
                object Read(string name)=>visit.GetType().GetField(name).GetValue(visit);
                var actor=(NavigationActor)Read("Actor");var queue=(PickupQueueService)Read("PickupOwner");
                var counter=CafeLoopRuntimeProof.Field<Vector3[]>(customers,"counterWaitSlots")[0];
                var prefabs=CafeLoopRuntimeProof.Field<NavigationActor[]>(customers,"prefabs");
                var envelope=actor.Settings.AgentRadius+actor.Settings.CollisionSkin+actor.Settings.Epsilon+
                    prefabs.Max(p=>p.Settings.AgentRadius+p.Settings.TargetSampleDistance+p.Settings.Epsilon+p.Settings.ArrivalDistance);
                var upper=new Vector3(3.5f,0,2.5f);var exit=CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position;
                var sides=CafeLoopRuntimeProof.Field<NavigationTarget[]>(customers,"pickupSides");
                var occupied=((Vector3[])Call("RouteObstacles",id,null,false)).Concat((Vector3[])Call("CorridorObstacles",visit)).Distinct().ToArray();
                var clearance=(float)Call("RouteClearance",actor.Settings);
                var rawRoute=(IReadOnlyList<Vector3>)Call("PlanPickupExit",upper,exit,occupied,actor.Settings);
                var rawPath=rawRoute==null?null:(IReadOnlyList<Vector3>)Call("ExpandRoute",upper,rawRoute);
                var bodyClear=CustomerRoutePlanner.IsClear(rawPath,occupied,clearance);
                var envelopeClear=CustomerRoutePlanner.IsClear(rawPath,new[]{counter},envelope);
                var deliveryClear=rawPath!=null&&(bool)Call("PickupDeliveryPathClear",actor,rawPath,occupied,exit);
                var returnClear=rawPath!=null&&(bool)Call("PickupCollectionClearsReturn",visit,rawPath,exit);
                string PathText(IReadOnlyList<Vector3> path)=>path==null?"NULL":"["+string.Join(";",path.Select(p=>p.ToString("F9")))+"]";
                // Save computed native facts before fixture assertions, including an unexpected path.
                // 不假定native直线；前提失败也必须留下原路径和坐标。
                TestContext.WriteLine("SELECTED_SIDE_FIXTURE id="+id+" state="+Read("State")+" paid="+owner.Snapshot.PaidVisitIds.Contains(id)+
                    " order="+owner.Snapshot.Orders.Values.Single(order=>order.CustomerId==id).State+" pickupArrived="+customers.CafeSnapshot.Single(v=>v.VisitId==id).PickupArrived+
                    " staffIdle="+employees.Snapshot.All(e=>e.State==EmployeeState.Idle)+" pose="+actor.transform.position.ToString("F9")+
                    " counter="+counter.ToString("F9")+" upper="+upper.ToString("F9")+" exit="+exit.ToString("F9")+" sides="+PathText(sides.Select(s=>s.Position).ToArray())+
                    " envelope="+envelope+" clearance="+clearance+" rawRoute="+PathText(rawRoute)+" rawExpanded="+PathText(rawPath)+" occupied="+PathText(occupied)+
                    " bodyClear="+bodyClear+" envelopeClear="+envelopeClear+" deliveryClear="+deliveryClear+" returnClear="+returnClear);
                Assert.That(Read("State"),Is.EqualTo(CustomerVisitState.PickupWaiting),"FIXTURE: real arrived paid Ready caller");
                Assert.That(sides.Any(s=>s.Position==upper),Is.True,"FIXTURE: actual Long7 upper service side");
                Assert.That(rawRoute,Is.Not.Null,"FIXTURE: original raw bounded exit route exists");
                Assert.That(rawRoute.Count,Is.InRange(1,24),"FIXTURE: existing route budget");
                Assert.That(bodyClear,Is.True,"FIXTURE: original expanded route clears all current obstacles");
                Assert.That(deliveryClear,Is.True,"FIXTURE: original delivery guard accepts");
                Assert.That(returnClear,Is.True,"FIXTURE: original return guard accepts");
                Assert.That(envelopeClear,Is.False,"FIXTURE: actual raw expanded path crosses future Counter envelope");
                var assignment=Read("Assignment");var state=Read("State");var request=Read("RequestId");var generation=Read("Generation");
                var queueGeneration=CafeLoopRuntimeProof.Field<int>(queue,"generation");
                var tokens=customers.Capacity.GetReservations().Select(row=>row.Token).ToArray();
                var tokenStates=customers.Capacity.GetReservations().Select(row=>row.State).ToArray();var paid=owner.Snapshot.PaidVisitIds.ToArray();
                var decisions=new List<(Vector3 Side,Vector3[] Path,string Result)>();
                hook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);prior=hook.GetValue(customers);
                Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string> observe=(operation,caller,related,target,path,obstacles,result)=>
                {
                    if(operation=="collection.exit-eligibility-result"&&caller==id)decisions.Add((target,path?.ToArray(),result));
                };
                hook.SetValue(customers,Delegate.Combine(prior as Delegate,observe));
                var accepted=(bool)Call("PickupCollectionCanExit",visit,upper);
                Assert.That(decisions.Count,Is.EqualTo(1),"Actual helper must emit its original result once");
                var decision=decisions[0];
                TestContext.WriteLine("SELECTED_SIDE_ORIGINAL_HELPER accepted="+accepted+" side="+decision.Side.ToString("F9")+
                    " result="+decision.Result+" originalExpanded="+PathText(decision.Path)+" counter="+counter.ToString("F9")+" envelope="+envelope);
                Assert.That(decision.Side,Is.EqualTo(upper));
                // RED on the old accepted crossing; GREEN permits rejection OR a safe bounded alternate.
                // 不把old accepted留成GREEN前提，不强制拒绝能找到安全路线的同一侧。
                if(accepted)
                {
                    Assert.That(decision.Path,Is.Not.Null,"Accepted helper must expose its actual expanded path");
                    Assert.That(CustomerRoutePlanner.IsClear(decision.Path,new[]{counter},envelope),Is.True,
                        "Final selected-side eligibility must preserve the future Counter envelope before lease acquisition");
                }
                Assert.That(queue.TryGetServiceLease(id,out _),Is.False,"Readonly eligibility cannot publish a lease, including refusals");
                Assert.That(Read("Assignment"),Is.EqualTo(assignment));Assert.That(Read("State"),Is.EqualTo(state));
                Assert.That(Read("RequestId"),Is.EqualTo(request));Assert.That(Read("Generation"),Is.EqualTo(generation));
                Assert.That(Read("PickupOwner"),Is.SameAs(queue));Assert.That(CafeLoopRuntimeProof.Field<int>(queue,"generation"),Is.EqualTo(queueGeneration));
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(row=>row.Token));
                CollectionAssert.AreEqual(tokenStates,customers.Capacity.GetReservations().Select(row=>row.State));CollectionAssert.AreEqual(paid,owner.Snapshot.PaidVisitIds);
                Assert.That(owner.Snapshot.Orders.Values.Single(order=>order.CustomerId==id).State,Is.EqualTo(OrderState.ReadyForPickup));
                decisions.Clear();
                Assert.That(customers.TryMoveToPickupService(id,out var reason),Is.True,"A genuinely safe side must permit actual collection: "+reason);
                var selected=decisions.LastOrDefault(d=>d.Result.EndsWith(" eligible=True"));
                Assert.That(selected.Path,Is.Not.Null,"Public acquisition must pass the original final-side helper");
                Assert.That(CustomerRoutePlanner.IsClear(selected.Path,new[]{counter},envelope),Is.True);
                Assert.That(queue.TryGetServiceLease(id,out var lease),Is.True);Assert.That(lease,Is.EqualTo(selected.Side));
                Assert.That((long)Read("RequestId"),Is.GreaterThan(0),"Real service approach request");
                var exits=0;var exitPose=Vector3.zero;
                exitObserver=actor.gameObject.AddComponent<CafeActualExitObserver>();exitObserver.VisitId=id;
                exitObserver.Receipt=(actual,pose)=>{if(actual==id){exits++;exitPose=pose;}};
                CafeLoopRuntimeProof.Set(owner,"stepping",false);tools.ResumeCollection();
                yield return Until(()=>owner.Snapshot.Orders.Values.Single(order=>order.CustomerId==id).State==OrderState.Completed&&customers.Snapshot.Count==0&&exits==1);
                Assert.That(exits,Is.EqualTo(1));Assert.That(Vector3.Distance(exitPose,exit),Is.LessThanOrEqualTo(.081f));
                Assert.That(queue.ServiceLeases,Is.Empty);CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(row=>row.Token));
                Assert.That(customers.Capacity.GetReservations().All(row=>row.State==AnimalCafe.Capacity.ReservationState.Released),Is.True);
                Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);CollectionAssert.AreEqual(paid,owner.Snapshot.PaidVisitIds);
                TestContext.WriteLine("SELECTED_SIDE_SAFE_PROGRESS side="+selected.Side.ToString("F9")+" actualExits="+exits+" exitPose="+exitPose.ToString("F9")+" originalTokensReleased=True");
            }
            finally
            {
                if(exitObserver!=null)exitObserver.Receipt=null;
                if(hook!=null&&customers!=null)hook.SetValue(customers,prior);
                if(owner!=null)CafeLoopRuntimeProof.Set(owner,"stepping",false);
                UnityEngine.Random.state=random;
            }
        }

        [UnityTest] public IEnumerator PickupGeometryPreservesContinuousFutureCounterHead()
        {
            var random=UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.InitState(150027);yield return MatrixStart();
                var runtime=Find<CafeLayoutRuntime>();var decor=Find<DecorationModeController>();
                for(var x=2;x<=7;x++)yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(x,5),0);
                var support=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(7,5));
                Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint,"pickup.point",new SurfaceSlotAddress(support.InstanceId,"slot.0")),Is.True);
                Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);decor.ExitDecorationMode();
                yield return Until(()=>owner.CanProgress&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                CafeLoopRuntimeProof.Set(owner,"stepping",true);
                Assert.That(customers.TrySpawn(),Is.True);var id=customers.Snapshot.Single().VisitId;
                yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==id).CounterArrived);
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                object Call(string method,params object[] args)=>typeof(CustomerFlowController).GetMethod(method,flags).Invoke(customers,args);
                var visit=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[id];
                var actor=world.RegisteredActors.Single(a=>a.ActorId==id);
                var counter=CafeLoopRuntimeProof.Field<Vector3[]>(customers,"counterWaitSlots")[0];
                Assert.That(customers.CounterAssignments.Single().Arrived,Is.True,"FIXTURE: real Counter-front arrival");
                Assert.That(CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue"),Is.Null);
                using var costs=new PickupEligibilityQueryCosts(customers);
                Assert.That(customers.TryReservePickup(id,out var reason),Is.True,"Legal nonconflicting queue control: "+reason);
                var tokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();
                var queue=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue");
                var slots=CafeLoopRuntimeProof.Field<Vector3[]>(customers,"pickupWaitSlots");
                var prefabs=CafeLoopRuntimeProof.Field<NavigationActor[]>(customers,"prefabs");
                // Triangle inequality covers the continuous arrival disk, not a few sampled poses.
                // 原始目标到sampled destination再到Arrived pose，两段合法偏移均计入。
                var envelope=actor.Settings.AgentRadius+actor.Settings.CollisionSkin+actor.Settings.Epsilon+
                    prefabs.Max(p=>p.Settings.AgentRadius+p.Settings.TargetSampleDistance+p.Settings.Epsilon+p.Settings.ArrivalDistance);
                var occupied=((Vector3[])Call("RouteObstacles",id,null,false)).Concat(slots.Skip(1)).Append(counter).Distinct().ToArray();
                var clearance=(float)Call("RouteClearance",actor.Settings);
                IReadOnlyList<Vector3> Query(Vector3 from,Vector3 to)
                {
                    var raw=adapter.GetCompletePath(from,to);
                    return CustomerRoutePlanner.IsClear(raw,new[]{counter},envelope)?raw:null;
                }
                var exit=CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position;
                var root=CafeLoopRuntimeProof.Field<Transform>(customers,"gridRoot");
                var boundary=typeof(CustomerFlowController).GetMethod("PickupExitBoundaryCandidates",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                var sides=CafeLoopRuntimeProof.Field<NavigationTarget[]>(customers,"pickupSides");
                bool Safe(Vector3 side)
                {
                    if(new CustomerRoutePlanner().Build(slots[0],side,occupied,clearance,Query)==null)return false;
                    var route=new CustomerRoutePlanner().Build(side,exit,occupied,clearance,Query);
                    if(route!=null)return true;
                    var candidates=(Vector3[][])boundary.Invoke(null,new object[]{root,runtime.RoomSurfaceLayout.FloorTiles.Keys.ToArray(),runtime.Layout.GridSettings.CellSize,side,exit,actor.Settings.AgentRadius});
                    foreach(var candidate in candidates)
                    {
                        var previous=side;var clear=true;
                        foreach(var point in candidate)
                        {
                            var path=Query(previous,point);
                            if(!CustomerRoutePlanner.IsClear(path,occupied,clearance)){clear=false;break;}
                            previous=path[path.Count-1];previous.y=side.y;
                        }
                        if(clear)return true;
                    }
                    return false;
                }
                TestContext.WriteLine("COUNTER_ENVELOPE actualSlots="+slots.Length+" radius="+envelope+" counter="+counter+" actualCaller="+actor.transform.position);
                Assert.That(sides.Any(s=>Safe(s.Position)),Is.True,
                    "Produced queue must retain a complete pickup-to-exit route outside the continuous future Counter-head envelope");
                CafeLoopRuntimeProof.Set(owner,"stepping",false);tools.ResumeCollection();
                yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==id&&o.State==OrderState.Completed)&&customers.Snapshot.Count==0);
                Assert.That(owner.Snapshot.PaidVisitIds,Does.Contain(id));Assert.That(queue.ServiceLeases,Is.Empty);
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(r=>r.Token));
                Assert.That(customers.Capacity.GetReservations().All(r=>r.State==AnimalCafe.Capacity.ReservationState.Released),Is.True);
                Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                Assert.That(customers.TrySpawn(),Is.True,"Original Counter caller departed; later actual admission remains available");
                var later=customers.Snapshot.Single().VisitId;Assert.That(later,Is.Not.EqualTo(id));
                yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==later).CounterArrived);
                TestContext.WriteLine("COUNTER_ENVELOPE originalCompletedAndExited=True laterActualCounterArrived=True");
            }
            finally {if(owner!=null)CafeLoopRuntimeProof.Set(owner,"stepping",false);UnityEngine.Random.state=random;}
        }

        [UnityTest] public IEnumerator PickupCollectionRejectsNoExitBeforeLeaseAndResumesAfterPhysicalClear()
        {
            yield return PrepareControlledPhotoSix();
            using var costs=new PickupEligibilityQueryCosts(customers);
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True);var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==id&&o.State==OrderState.ReadyForPickup)&&
                customers.CafeSnapshot.Single(v=>v.VisitId==id).PickupArrived&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
            object Call(string method,params object[] args)=>typeof(CustomerFlowController).GetMethod(method,flags).Invoke(customers,args);
            var visit=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[id];
            object Read(string field)=>visit.GetType().GetField(field).GetValue(visit);
            var actor=(NavigationActor)Read("Actor");var queue=(PickupQueueService)Read("PickupOwner");
            var tokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();var paid=owner.Snapshot.PaidVisitIds.ToArray();
            var assignment=Read("Assignment");var generation=CafeLoopRuntimeProof.Field<int>(queue,"generation");
            var published=0;var hook=typeof(CustomerFlowController).GetField("PickupObserver",flags);var prior=hook.GetValue(customers);
            NavigationActor obstruction=null;
            CafeLoopRuntimeProof.Set(owner,"stepping",true);
            try
            {
                obstruction=Blocker(CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position,"pickup-exit-eligibility-static-body");
                var sides=CafeLoopRuntimeProof.Field<NavigationTarget[]>(customers,"pickupSides");
                var preferred=CafeLoopRuntimeProof.Field<Vector3>(customers,"pickupCustomer");
                var candidate=sides.OrderBy(t=>t.Position==preferred?0:1).First();
                var route=(IReadOnlyList<Vector3>)Call("BuildRoute",actor.transform.position,candidate.Position,id,actor.Settings,null,false);
                Assert.That(route,Is.Not.Null,"FIXTURE: original preferred approach must be complete");
                var expanded=(IReadOnlyList<Vector3>)Call("ExpandRoute",actor.transform.position,route);
                var occupied=(Vector3[])Call("RouteObstacles",id,null,false);var clearance=(float)Call("RouteClearance",actor.Settings);
                Assert.That((bool)Call("NewPickupTargetClear",visit,candidate.Position),Is.True,"FIXTURE: target eligible");
                Assert.That(CustomerRoutePlanner.IsClear(expanded,(Vector3[])Call("CorridorObstacles",visit),clearance),Is.True,"FIXTURE: owned corridor eligible");
                Assert.That((bool)Call("PickupDeliveryPathClear",actor,expanded,occupied,candidate.Position),Is.True,"FIXTURE: delivery eligible");
                Assert.That((bool)Call("PickupCollectionClearsReturn",visit,expanded,candidate.Position),Is.True,"FIXTURE: return eligible");
                foreach(var side in sides)Assert.That(Call("PlanPickupExit",side.Position,obstruction.transform.position,occupied,actor.Settings),Is.Null,"FIXTURE: actual body denies exit from every side");
                hook.SetValue(customers,(Action<string,string,PickupQueueService,QueueAssignment>)((operation,who,q,a)=>{if(operation=="collection.published"&&who==id)published++;}));
                Assert.That(customers.TryMoveToPickupService(id,out _),Is.False,"Unsafe side must refuse BEFORE service lease publication");
                Assert.That(published,Is.Zero);Assert.That(queue.TryGetServiceLease(id,out _),Is.False);
                Assert.That(Read("Assignment"),Is.EqualTo(assignment));Assert.That(Read("PickupOwner"),Is.SameAs(queue));
                Assert.That(CafeLoopRuntimeProof.Field<int>(queue,"generation"),Is.EqualTo(generation));
                Assert.That(Read("State"),Is.EqualTo(CustomerVisitState.PickupWaiting));Assert.That(Read("RequestId"),Is.EqualTo(0L));
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(r=>r.Token));CollectionAssert.AreEqual(paid,owner.Snapshot.PaidVisitIds);
                Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==id).State,Is.EqualTo(OrderState.ReadyForPickup));
                world.Unregister(obstruction);obstruction.gameObject.SetActive(false);UnityEngine.Object.Destroy(obstruction.gameObject);obstruction=null;
                Assert.That(customers.TryMoveToPickupService(id,out var reason),Is.True,"Explicit physical clearance permits actual service: "+reason);
                Assert.That(published,Is.EqualTo(1));Assert.That(queue.TryGetServiceLease(id,out _),Is.True);Assert.That((long)Read("RequestId"),Is.GreaterThan(0));
                CafeLoopRuntimeProof.Set(owner,"stepping",false);tools.ResumeCollection();
                yield return Until(()=>owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==id).State==OrderState.Completed&&customers.Snapshot.Count==0);
                Assert.That(queue.ServiceLeases,Is.Empty);
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(r=>r.Token));
                Assert.That(customers.Capacity.GetReservations().All(r=>r.State==AnimalCafe.Capacity.ReservationState.Released),Is.True);
                Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                CollectionAssert.AreEqual(paid,owner.Snapshot.PaidVisitIds);
            }
            finally
            {
                if(obstruction!=null){world.Unregister(obstruction);obstruction.gameObject.SetActive(false);UnityEngine.Object.Destroy(obstruction.gameObject);}
                hook.SetValue(customers,prior);CafeLoopRuntimeProof.Set(owner,"stepping",false);
            }
        }

        [UnityTest] public IEnumerator SegmentFailureWitnessDefaultsOffPreservesRejectedRoute()
            => ObserveSegmentFailureWitness(false);
        [UnityTest] public IEnumerator SegmentFailureWitnessCapturesOriginalRejectedRouteWithoutChangingBudgets()
            => ObserveSegmentFailureWitness(true);

        private IEnumerator ObserveSegmentFailureWitness(bool enabled)
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var hook=typeof(CustomerFlowController).GetField("SegmentFailureObserver",flags);
            Assert.That(hook,Is.Not.Null,"Dedicated cold failure hook must exist");
            Assert.That(hook.GetValue(customers),Is.Null,"Cold failure witness defaults OFF");
            var originalStepping=CafeLoopRuntimeProof.Field<bool>(owner,"stepping");
            SegmentFailureTrace trace=null;
            IReadOnlyList<string> records=Array.Empty<string>();
            if(enabled) { trace=new SegmentFailureTrace(this); records=trace.Records; }
            try
            {
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                Assert.That(customers.TrySpawn(),Is.True);
                var id=customers.Snapshot.Single().VisitId;
                yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==id).CounterArrived);
                CafeLoopRuntimeProof.Set(owner,"stepping",true);
                var visit=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[id];
                object Read(string name)=>visit.GetType().GetField(name).GetValue(visit);
                void Write(string name,object value)=>visit.GetType().GetField(name).SetValue(visit,value);
                var actor=(NavigationActor)Read("Actor");
                var pose=actor.transform.position;
                // Stage only an unissued impossible waypoint; actual adapter, WAIT and planner decide.
                // 仅暂存真实Visit的未发waypoint，不伪造planner结果或native flags。
                var unreachable=pose+Vector3.right*1000;
                Write("State",CustomerVisitState.MovingToPickupService); Write("ResumeState",CustomerVisitState.MovingToPickupService);
                Write("FinalTarget",new NavigationTarget(unreachable)); Write("Route",new Queue<Vector3>(new[]{unreachable}));
                Write("Segments",4); Write("Replans",0);
                var route=Read("Route");
                var tokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();
                var reservationStates=customers.Capacity.GetReservations().Select(r=>r.State).ToArray();
                var generation=Read("Generation");
                var obstacles=(Vector3[])typeof(CustomerFlowController).GetMethod("RouteObstacles",flags).Invoke(customers,new object[]{id,null,false});
                Assert.That(adapter.GetCompletePath(pose,unreachable),Is.Null,"Fixture must really reject original nextPath");
                typeof(CustomerFlowController).GetMethod("SendRouteSegment",flags).Invoke(customers,new[]{visit,(object)CustomerVisitState.MovingToPickupService});
                Assert.That(Read("State"),Is.EqualTo(CustomerVisitState.Blocked));
                Assert.That(Read("Failure").ToString(),Does.Contain("通道被占用"));
                Assert.That(Read("Segments"),Is.EqualTo(4)); Assert.That(Read("Replans"),Is.EqualTo(1));
                Assert.That(Read("Generation"),Is.EqualTo(generation)); Assert.That(Read("RequestId"),Is.EqualTo(0L));
                Assert.That(actor.transform.position,Is.EqualTo(pose));
                Assert.That(Read("Route"),Is.SameAs(route));
                CollectionAssert.AreEqual(new[]{unreachable},(Queue<Vector3>)Read("Route"));
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(r=>r.Token));
                CollectionAssert.AreEqual(reservationStates,customers.Capacity.GetReservations().Select(r=>r.State));
                Assert.That(records.Count,Is.EqualTo(enabled?1:0));
                if(enabled)
                {
                    var record=records.Single();
                    Assert.That(record,Does.Contain("actor="+id).And.Contain("nextPath=NULL").And.Contain("wait=static-path-NULL"));
                    Assert.That(record,Does.Contain("replan=NULL").And.Contain("segments=4").And.Contain("replans=1"));
                    Assert.That(record,Does.Contain("native.hasPath=NOT_EVALUATED").And.Contain("callback=NONE"));
                    Assert.That(record,Does.Contain("from="+pose.ToString("F9")).And.Contain("next="+unreachable.ToString("F9")));
                    foreach(var obstacle in obstacles) Assert.That(record,Does.Contain(obstacle.ToString("F9")));
                    // Actual sink must retain real stored runner context, including explicit idle NULLs.
                    // 真实fixture字段证据；不写入合成route/leg，也不宣称这是native路径证据。
                    foreach(var staff in CafeLoopRuntimeProof.Field<IList>(employees,"employees"))
                    {
                        object Stored(object value,string field)=>value.GetType().GetField(field,flags|System.Reflection.BindingFlags.Public).GetValue(value);
                        var runner=Stored(staff,"Runner"); Assert.That(runner,Is.Not.Null);
                        var leg=Stored(runner,"moveLeg");
                        var runnerRecord=record.Split('\n').Single(line=>line.StartsWith("runnerActor="+Stored(staff,"Id")+" "));
                        Assert.That(runnerRecord,Does.Contain("runnerActor="+Stored(staff,"Id"))
                            .And.Contain("taskGeneration="+Stored(runner,"generation"))
                            .And.Contain("deliveryRouteIndex="+Stored(runner,"deliveryRouteIndex")));
                        var delivery=(NavigationTarget[])Stored(runner,"deliveryRoute");
                        var selected=(NavigationTarget?)Stored(runner,"selectedDeliveryTarget");
                        Assert.That(runnerRecord,Does.Contain("deliveryRoute="+(delivery==null?"NULL":"["+string.Join(";",delivery.Select(t=>t.Position.ToString("F9")))+"]"))
                            .And.Contain("selectedDeliveryTarget="+(selected.HasValue?selected.Value.Position.ToString("F9"):"NULL"))
                            .And.Contain("moveLeg="+(leg==null?"NULL":"PRESENT")));
                        Assert.That(runnerRecord,Does.Contain("moveLegRequest="+(leg==null?"UNAVAILABLE_OWNER_NULL":Stored(leg,"RequestId").ToString())));
                    }
                    TestContext.WriteLine(record);
                }
            }
            finally
            {
                hook.SetValue(customers,null);
                CafeLoopRuntimeProof.Set(owner,"stepping",originalStepping);
                trace?.Finish();
            }
            Assert.That(hook.GetValue(customers),Is.Null,"Always detach after test");
            Assert.That(CafeLoopRuntimeProof.Field<bool>(owner,"stepping"),Is.EqualTo(originalStepping));
        }

        [UnityTest] public IEnumerator StallObservationDefaultsOffPreservesRealSubmission()
            => ObserveStallSubmission(false);
        [UnityTest] public IEnumerator StallObservationCapturesRealSubmissionAndSynchronousFailure()
            => ObserveStallSubmission(true);
        [UnityTest] public IEnumerator StallObservationUnifiedTraceIncludesIdleEmployeeAndCustomer()
        {
            var trace=new CorridorTrace(this,true);
            try
            {
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                Assert.That(customers.TrySpawn(),Is.True);
                var id=customers.Snapshot.Single().VisitId;
                yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==id).CounterArrived);
                Assert.That(trace.StallCustomerSeen,Is.True,"Unified records include real customer decisions");
                Assert.That(trace.StallDriverSeen,Is.True,"Unified records join original native path decisions");
                Assert.That(trace.StallIdleEmployeeSeen,Is.True,"Requestless zero-desired employee is retained");
                Assert.That(trace.StallDropped,Is.EqualTo(0),"Small real integration has no capacity loss");
            }
            finally { trace.Finish(); }
        }

        [UnityTest] public IEnumerator StallObservationRetainsTwoRealPlannerCallersAndPurposes()
        {
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True);
            var first=customers.Snapshot.Single().VisitId;
            yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==first).CounterArrived);
            // Query attribution needs two real callers; wait for the first actual PQS arrival and idle staff before the second admission.
            // 正常owner推进付款/分配；保原Until上限，不造state/guard，不改变下方四次query-only输入与断言。
            yield return Until(()=>customers.PickupAssignments.Any(assignment=>assignment.VisitId==first && assignment.Arrived) &&
                employees.Snapshot.All(employee=>employee.State==EmployeeState.Idle));
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True);
            var second=customers.Snapshot.Single(v=>v.VisitId!=first).VisitId;
            var visits=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");
            var from=world.RegisteredActors.Single(a=>a.ActorId==first).transform.position;
            var settings=world.RegisteredActors.Single(a=>a.ActorId==first).Settings;
            var goal=CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position;
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var attributed=typeof(CustomerFlowController).GetMethod("PlanPickupExitForCaller",flags);
            var original=typeof(CustomerFlowController).GetMethod("PlanPickupExit",flags);
            var trace=new CorridorTrace(this,true);
            try
            {
                // Two real visit identities, original planner, identical query inputs.
                // Query-only acceptance purposes; these calls do not authorize movement.
                foreach(var id in new[]{first,second})
                foreach(var purpose in new[]{"acceptance.actual-query","acceptance.counterfactual-query"})
                {
                    if(attributed==null) original.Invoke(customers,new object[]{from,goal,Array.Empty<Vector3>(),settings});
                    else attributed.Invoke(customers,new object[]{visits[id],purpose,from,goal,Array.Empty<Vector3>(),settings});
                }
                Assert.That(trace.PlannerReceiptCount(first),Is.EqualTo(2),"First real caller retains both independently invoked planner purposes");
                Assert.That(trace.PlannerReceiptCount(second),Is.EqualTo(2),"Second real caller with identical target/decision must not be suppressed");
                Assert.That(trace.PlannerPurposesSeen,Is.True,"Actual-query and counterfactual-query purpose tags remain separate");
            }
            finally { trace.Finish(); }
        }

        [UnityTest] public IEnumerator StallObservationSyntheticMetadataRetainsGenerationsAndTerminalIdentity()
        {
            // Collector-only metadata, explicitly NOT a native retry/recovery experiment.
            var trace=new CorridorTrace(this,true);
            try
            {
                trace.SyntheticSample(1,4,7,"nav");
                trace.SyntheticCallback(1,4,7,"Cancelled");
                for(var i=0;i<20;i++) trace.SyntheticSample(2,4,7,"nav");
                trace.SyntheticSample(2,5,7,"nav");
                trace.SyntheticSample(2,5,8,"nav");
                Assert.That(trace.SyntheticRingCount,Is.EqualTo(4),"Same request retains independent path generation, layout and task generation rings");
                Assert.That(trace.SyntheticLayerTransitions,Is.EqualTo(4),"Same layer in a new identity is a new semantic transition");
                Assert.That(trace.SyntheticFrozenCount,Is.EqualTo(0),"Harmless loss and old Cancelled do not freeze later samples");
                trace.SyntheticCallback(2,5,8,"Failed");
                trace.SyntheticSample(3,5,8,"nav");
                Assert.That(trace.SyntheticRingCount,Is.EqualTo(5),"A terminal identity does not suppress a later generation");
                Assert.That(trace.SyntheticFrozenCount,Is.EqualTo(1));
            }
            finally { trace.Finish(); }
            yield return null;
        }

        // Switch genuine saved fixtures; never toggle an integrated Core/port into standalone mode.
        // 卸载旧场景与其inputs；新模式来自既有正式场景，不修改Core或角色位置。
        private IEnumerator SwitchPortObservationFixture(bool standalone)
        {
            var old=scene;var oldInputs=inputs;inputs=null;
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(UnityEngine.SceneManagement.SceneManager.CreateScene("port observation switch"));
            if(old.IsValid() && old.isLoaded)yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(old);
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(oldInputs??Array.Empty<UnityEngine.InputSystem.InputActionAsset>());
            var path=standalone?"Assets/Scenes/Validation/Phase13EmployeeTasks.unity":"Assets/Scenes/Validation/Phase14SharedPickup.unity";
            scene=UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path,
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return null;
            world=Find<NavigationWorld>();employees=Find<EmployeeFlowController>();adapter=Find<NavigationLayoutAdapter>();
            time=Find<GameTimeService>();customers=Find<CustomerFlowController>();
            inputs=Phase8SceneInputTestCleanup.CaptureAssets(scene);deliveryStarts.Clear();
            var roots=scene.GetRootGameObjects();
            owner=roots.SelectMany(r=>r.GetComponentsInChildren<CafeLoopController>(true)).SingleOrDefault();
            tools=roots.SelectMany(r=>r.GetComponentsInChildren<CafeLoopValidationController>(true)).SingleOrDefault();
            if(standalone)
            {
                Assert.That(owner,Is.Null,"P13 is a genuine standalone fixture, not a disabled integrated owner");
                Assert.That(CafeLoopRuntimeProof.Field<object>(customers,"traffic"),Is.Null,"Standalone fixture has no Core");
                Assert.That(customers.AutoSpawn,Is.False,"Do not enable the P13 optional customer scenario");
            }
            else
            {
                Assert.That(owner,Is.Not.Null);Assert.That(tools,Is.Not.Null);
                tools.HoldArrivals();tools.HoldCollection();
            }
            for(var frame=0;frame<6000 && !(employees.Snapshot.Count==2 && employees.Snapshot.All(e=>e.State==EmployeeState.Idle) &&
                (standalone || owner.CanProgress));frame++)yield return null;
            Assert.That(employees.Snapshot.Count,Is.EqualTo(2));Assert.That(employees.Snapshot.All(e=>e.State==EmployeeState.Idle),Is.True);
            Assert.That(customers.AutoSpawn,Is.False);Assert.That(world.RegisteredActors.Count,Is.EqualTo(2));
            Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True,time.ResumeBlockReason);
            if(!standalone)Assert.That(owner.CanProgress,Is.True);
        }

        // Same real Environment.MoveTo + native driver. Core rejects the raw integrated call;
        // the valid/invalid Native-target observer boundary is exercised in genuine standalone P13.
        private IEnumerator ObserveStallSubmission(bool enabled)
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static|
                System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            var originalStaff=CafeLoopRuntimeProof.Field<IList>(employees,"employees")[0];
            var originalPort=(IEmployeeTaskEnvironment)originalStaff.GetType().GetField("Port",flags).GetValue(originalStaff);
            var originalActor=(NavigationActor)originalStaff.GetType().GetField("Actor",flags).GetValue(originalStaff);
            Assert.That(((IEmployeeTrafficEnvironment)originalPort).TrafficEnabled,Is.True);
            var beforeNative=CafeLoopRuntimeProof.Field<long>(world.Service,"nextId");var unsafeCallback=false;
            var unprepared=originalPort.MoveTo(new NavigationTarget(originalActor.transform.position),r=>unsafeCallback=true);
            Assert.That(unprepared.Accepted,Is.False,"An unprepared integrated port cannot bypass Core");
            Assert.That(unsafeCallback,Is.False);Assert.That(CafeLoopRuntimeProof.Field<long>(world.Service,"nextId"),Is.EqualTo(beforeNative));
            yield return SwitchPortObservationFixture(true);
            var driverHook=typeof(NavMeshMovementDriver).GetField("PathObserver",flags);
            var employeeHook=typeof(EmployeeFlowController).GetField("MovementObserver",flags);
            Assert.That(driverHook?.GetValue(null),Is.Null,"Driver observer starts disabled");
            Assert.That(employeeHook?.GetValue(employees),Is.Null,"Employee observer starts disabled");
            var records=new List<string>();
            if(enabled)
            {
                driverHook?.SetValue(null,new Action<NavigationActor,string,string>((a,op,data)=>records.Add("driver/"+op+" actor="+a.ActorId+" "+data)));
                employeeHook?.SetValue(employees,new Action<string,string,string>((a,op,data)=>records.Add("employee/"+op+" actor="+a+" "+data)));
            }
            var staff=CafeLoopRuntimeProof.Field<IList>(employees,"employees")[0];
            var port=(IEmployeeTaskEnvironment)staff.GetType().GetField("Port",flags).GetValue(staff);
            var actor=(NavigationActor)staff.GetType().GetField("Actor",flags).GetValue(staff);
            Assert.That(((IEmployeeTrafficEnvironment)port).TrafficEnabled,Is.False,"Saved P13 standalone mode is the Native-target boundary");
            var pose=actor.transform.position;
            try
            {
                MovementResult? arrived=null;
                var start=port.MoveTo(new NavigationTarget(pose),r=>arrived=r);
                Assert.That(start.Accepted,Is.True,"Real same-position request is accepted");
                for(var frame=0;frame<6000 && !arrived.HasValue;frame++)yield return null;
                Assert.That(arrived.HasValue,Is.True,"Real standalone completion within the original finite window");
                Assert.That(arrived.Value.Status,Is.EqualTo(MovementStatus.Arrived));
                Assert.That(arrived.Value.RequestId,Is.EqualTo(start.RequestId));
                Assert.That(Vector3.Distance(actor.transform.position,pose),Is.LessThanOrEqualTo(actor.Settings.Epsilon));
                MovementResult? refused=null;
                var bad=port.MoveTo(new NavigationTarget(new Vector3(1000,0,1000)),r=>refused=r);
                Assert.That(bad.Accepted,Is.True,"Service accepts a request before the native target refusal");
                Assert.That(refused.HasValue,Is.True,"BeginPath failure is synchronous");
                Assert.That(refused.Value.Reason,Is.EqualTo(NavigationFailure.InvalidTarget));
                Assert.That(refused.Value.RequestId,Is.EqualTo(bad.RequestId));
                if(enabled)
                {
                    Assert.That(records.Any(r=>r.StartsWith("driver/begin.accepted")),Is.True,"Required original successful native decision is observed");
                    Assert.That(records.Any(r=>r.StartsWith("driver/target.sample")&&r.Contains("outcome=False")),Is.True,"Required original failed sample is observed");
                    Assert.That(records.Any(r=>r.StartsWith("employee/callback")&&r.Contains("actor="+actor.ActorId+" ")&&r.Contains("request="+bad.RequestId+" ")),Is.True,"Synchronous callback retains real identity");
                    Assert.That(records.Any(r=>r.StartsWith("employee/submitted")&&r.Contains("actor="+actor.ActorId+" ")&&r.Contains("request="+bad.RequestId+" ")),Is.True,"Later submission joins the same real request");
                }
                else Assert.That(records,Is.Empty);
                TestContext.WriteLine("STALL_OBSERVATION standaloneP13=True integratedRawGuard=True enabled="+enabled+" events="+records.Count+" arrived="+start.RequestId+" synchronousFailure="+bad.RequestId+"\n"+string.Join("\n",records));
            }
            finally { driverHook?.SetValue(null,null); employeeHook?.SetValue(employees,null); }
        }

        // Screenshot approximation, not an exact recovered player layout.
        // 连续长柜台两端分别收银和取餐；不修改正式Scene，不注入订单或角色位置。
        [UnityTest] public IEnumerator UserLongCounterSixNormal() => Environment.GetCommandLineArgs().Contains("-exitDecisionWitness")
            ?ObserveNormalDecisionWitnesses(6):ObserveUserLongCounter(6, GameSpeed.Normal);
        [UnityTest] public IEnumerator UserLongCounterSixFast() => ObserveUserLongCounter(6, GameSpeed.Fast);
        [UnityTest] public IEnumerator UserLongCounterSevenNormal() => Environment.GetCommandLineArgs().Any(a=>a=="-deliveryAcquisitionWitness"||a=="-exitDecisionWitness")
            ?ObserveNormalDecisionWitnesses(7):ObserveUserLongCounter(7, GameSpeed.Normal);
        [UnityTest] public IEnumerator UserLongCounterSevenFast() => ObserveUserLongCounter(7, GameSpeed.Fast);
        [UnityTest] public IEnumerator UserPhotoCounterSixNormal() => ObserveUserLongCounter(6, GameSpeed.Normal, true);
        [UnityTest, Timeout(600000)] public IEnumerator UserPhotoCounterSixOriginalExitDecisions()
        {
            if(!Environment.GetCommandLineArgs().Contains("-photoSixOriginalExitDecisions"))
                Assert.Ignore("Opt-in original exit decisions; requires -photoSixOriginalExitDecisions.");
            yield return ObserveUserLongCounter(6,GameSpeed.Normal,true,false,false,true);
        }
        [UnityTest] public IEnumerator UserPhotoCounterSixFast() => ObserveUserLongCounter(6, GameSpeed.Fast, true,
            originalExitDecisions:Environment.GetCommandLineArgs().Any(arg=>arg=="-photoExitFirstFailureWitness" || arg=="-photoCheckoutFirstRefusalWitness"));
        [UnityTest] public IEnumerator UserPhotoCounterSevenNormal() => ObserveUserLongCounter(7, GameSpeed.Normal, true);
        [UnityTest] public IEnumerator UserPhotoCounterSevenFast() => ObserveUserLongCounter(7, GameSpeed.Fast, true);
        [UnityTest, Timeout(600000)] public IEnumerator PhotoSevenFastExitConnectorTimings()
        {
            if(!Environment.GetCommandLineArgs().Contains("-exitConnectorTimings"))Assert.Ignore("Opt-in connector timing; requires -exitConnectorTimings.");
            yield return ObserveUserLongCounter(7,GameSpeed.Fast,true,false,false,false,true);
        }
        [UnityTest] public IEnumerator PhotoSixExitPlannerFindsExistingSafeNativeDetour()
        {
            yield return MatrixStart();
            var runtime=Find<CafeLayoutRuntime>();var decor=Find<DecorationModeController>();
            var cash=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(1,5));
            var machine=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(4,6));
            yield return UserMoveCounterSupport(cash.InstanceId,new GridPosition(6,4));
            yield return UserMoveCounterSupport(machine.InstanceId,new GridPosition(1,7));
            var equipment=runtime.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,equipment.InstanceId),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True);
            Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);
            for(var column=1;column<6;column++)yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(column,4),0);
            var support=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(1,4));
            Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint,"pickup.point",new SurfaceSlotAddress(support.InstanceId,"slot.0")),Is.True);
            Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);decor.ExitDecorationMode();
            yield return Until(()=>owner.CanProgress&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));yield return null;
            // Fixed quantized query input, derived from the retained failure snapshot.
            // Exiting excludes a future Counter arrival target; every actual body and Pickup target stays.
            // 这是可审查的空间反例，不声称量化快照就是历史原Send输入。
            var occupied=new[]{new Vector3(-2.5f,0,-2.7f),new Vector3(-3.525f,0,-2.699999f),
                new Vector3(.158701f,0,-.927458f),new Vector3(2.5f,0,-.500001f),new Vector3(1.712891f,0,-2.723013f),
                new Vector3(-2.5f,0,2.5f),new Vector3(2.5f,0,1.5f),new Vector3(-3.6f,0,-1.6f),new Vector3(-3.6f,0,-.5f)};
            var start=new Vector3(-2.5f,0,1.5f);var goal=new Vector3(.5f,0,-3.35f);
            var prefabs=CafeLoopRuntimeProof.Field<NavigationActor[]>(customers,"prefabs");var settings=prefabs[2%prefabs.Length].Settings;
            const float clearance=.911f;
            var stops=new[]{new Vector3(2.5f,0,2.55f),new Vector3(3.5f,0,2.55f),
                new Vector3(3.5f,0,-1.65f),new Vector3(1f,0,-1.8f),goal};
            var native=new List<Vector3>{start};var previous=start;var evidence=new List<string>();
            foreach(var stop in stops)
            {
                var leg=adapter.GetCompletePath(previous,stop);
                evidence.Add("NATIVE_LEG from="+previous.ToString("F9")+" to="+stop.ToString("F9")+
                    " path="+(leg==null?"NULL":string.Join(";",leg.Select(p=>p.ToString("F9"))))+
                    " allOriginalObstaclesClear="+CustomerRoutePlanner.IsClear(leg,occupied,clearance));
                Assert.That(leg,Is.Not.Null,"Independent complete Native leg must exist.");
                native.AddRange(leg.Skip(1));previous=stop;
            }
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var planned=(IReadOnlyList<Vector3>)typeof(CustomerFlowController).GetMethod("PlanPickupExit",flags)
                .Invoke(customers,new object[]{start,goal,occupied,settings});
            evidence.Add("FIXED_QUERY input="+string.Join(";",occupied.Select(p=>p.ToString("F9")))+
                " manualCompletePathClear="+CustomerRoutePlanner.IsClear(native,occupied,clearance)+
                " planner="+(planned==null?"NULL":string.Join(";",planned.Select(p=>p.ToString("F9")))));
            var directory=Path.Combine("outputs","phase15","photo-six-detour-query",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);File.WriteAllLines(Path.Combine(directory,"native-query.txt"),evidence);
            TestContext.WriteLine("PHOTO_SIX_DETOUR_QUERY "+directory);
            Assert.That(CustomerRoutePlanner.IsClear(native,occupied,clearance),Is.True,"Hand-chosen complete Native witness must retain all fixed input obstacles.");
            Assert.That(planned,Is.Not.Null,"A complete, unobstructed Native detour exists; exit planning must find a route.");
            Assert.That(planned.Count,Is.LessThanOrEqualTo(24));Assert.That(planned.Last(),Is.EqualTo(goal));
            previous=start;
            foreach(var waypoint in planned)
            {
                var actualLeg=adapter.GetCompletePath(previous,waypoint);
                Assert.That(CustomerRoutePlanner.IsClear(actualLeg,occupied,clearance),Is.True,"Every returned leg must still clear every original obstacle.");
                previous=actualLeg[actualLeg.Count-1];previous.y=0;
            }
            foreach(var blocked in new[]{start,goal})
            {
                var blockedPlan=typeof(CustomerFlowController).GetMethod("PlanPickupExit",flags)
                    .Invoke(customers,new object[]{start,goal,occupied.Concat(new[]{blocked}).ToArray(),settings});
                Assert.That(blockedPlan,Is.Null,"A body at the real start/exit still forbids a route.");
            }
            var root=CafeLoopRuntimeProof.Field<Transform>(customers,"gridRoot");
            var boundary=(Vector3[][])typeof(CustomerFlowController).GetMethod("PickupExitBoundaryCandidates",
                System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,
                new object[]{root,runtime.RoomSurfaceLayout.FloorTiles.Keys.ToArray(),runtime.Layout.GridSettings.CellSize,start,goal,settings.AgentRadius});
            var corner=boundary[0][0];
            var coveredInput=occupied.Concat(new[]{corner}).ToArray();var coveredTargetQueries=0;
            IReadOnlyList<Vector3> CountRealNativeQuery(Vector3 from,Vector3 to)
            {
                if(to==corner)coveredTargetQueries++;
                return adapter.GetCompletePath(from,to);
            }
            typeof(CustomerFlowController).GetMethod("PlanPickupExitUsingQuery",flags).Invoke(customers,
                new object[]{null,"covered-endpoint-native-query",start,goal,coveredInput,settings,
                    (Func<Vector3,Vector3,IReadOnlyList<Vector3>>)CountRealNativeQuery,"real-native-counted"});
            File.AppendAllText(Path.Combine(directory,"native-query.txt"),"\nCOVERED_ENDPOINT target="+corner.ToString("F9")+" nativeQueries="+coveredTargetQueries+"\n");
            TestContext.WriteLine("COVERED_ENDPOINT_NATIVE_QUERIES="+coveredTargetQueries);
            Assert.That(coveredTargetQueries,Is.EqualTo(1),"After its real Native endpoint is covered, this connector must not launch an impossible graph search.");
        }
        [UnityTest] public IEnumerator QuantizedLongSevenExitSnapshotCounterFrontRemovalQueries()
        {
            if(!Environment.GetCommandLineArgs().Contains("-postFailureSnapshotReplay"))
                Assert.Ignore("Local-only snapshot diagnostic; requires -postFailureSnapshotReplay and frozen local artifacts.");
            // Two fixed planning inputs only; no actors/orders/requests/proofs are fabricated.
            // 仅重放量化后的空间输入，不冒充历史原决策，也不搜索新的反事实。
            const string label="POST_FAILURE_QUANTIZED_SNAPSHOT_REPLAY";
            var directory=Path.Combine("outputs","phase15","owned-service-route-repair","quantized-exit-query");
            var authority=Path.Combine("outputs","phase15","owned-service-route-repair","post-guard-regression","cases","UserLongCounterSevenNormal","natural.txt");
            var records=new List<string>{label,"Historical original decision remains UNKNOWN; directPlannerResult is not an internal native/ring receipt."};
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
            object Call(string name,params object[] args)=>typeof(CustomerFlowController).GetMethod(name,flags).Invoke(customers,args);
            string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");}
            string Points(IEnumerable<Vector3> points)=>points==null?"NULL":"["+string.Join(";",points.Select(p=>p.ToString("F9")))+"]";
            Vector3[] ReadInput(string path)=>File.ReadAllLines(path).Where(l=>!string.IsNullOrWhiteSpace(l)).Select(line=>
            {
                var p=line.Split(',').Select(v=>float.Parse(v,System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                return new Vector3(p[0],p[1],p[2]);
            }).ToArray();
            var random=UnityEngine.Random.state;System.Reflection.FieldInfo hook=null;object prior=null;var hookRestored=false;var scenario="SETUP";var observerErrors=0;
            try
            {
                Assert.That(Hash(authority),Is.EqualTo("0304F36A12BD2F283B0EFA5CE7B4B7A6E999C6F4ADC0EDA8C90058D68AFF3688"));
                var fullFile=Path.Combine(directory,"input-full12.txt");var withoutFile=Path.Combine(directory,"input-without-c12-11.txt");
                Assert.That(Hash(fullFile),Is.EqualTo("B57E75B688D7C9BF3D984B5212D86C8AA38177BA5ECCF379E5ACA72F1536F5E3"));
                Assert.That(Hash(withoutFile),Is.EqualTo("1D230D792DD906385228FDE60E4EC240D43A54253C5FE7BEF1B8811E3D4837F2"));
                var full=ReadInput(fullFile);var without=ReadInput(withoutFile);var namedBody=new Vector3(-2.5f,0,.5f);
                Assert.That(full.Length,Is.EqualTo(12));Assert.That(without.Length,Is.EqualTo(11));Assert.That(full[8],Is.EqualTo(namedBody));
                Assert.That(full.Count(p=>p==namedBody),Is.EqualTo(1));CollectionAssert.AreEqual(full.Where((p,i)=>i!=8),without);
                records.Add("authority="+authority+" SHA256="+Hash(authority)+" lines=284-287 sourceIdentities=281-282,289-290");
                var identities=new[]{"C4.body","C5.body","C6.body","C7.body","C8.body","C9.body","C10.body","C11.body","C12.body","C13.body","Barista.body","Cashier.body"};
                records.Add("QUANTIZED_SOURCES "+string.Join(";",full.Select((p,i)=>i+":"+identities[i]+"@"+p.ToString("F6"))));
                records.Add("C12 CounterAssignments is same-owner Queued context, not ReservesRouteTarget; saved snapshot has no foreign coincident source. C3 own lease is excluded by original caller. No actual actor is removed.");
                UnityEngine.Random.InitState(150027);yield return MatrixStart();
                var runtime=Find<CafeLayoutRuntime>();var decor=Find<DecorationModeController>();
                for(var x=2;x<=7;x++)yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(x,5),0);
                var support=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(7,5));
                Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint,"pickup.point",new SurfaceSlotAddress(support.InstanceId,"slot.0")),Is.True);
                Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);decor.ExitDecorationMode();
                yield return Until(()=>owner.CanProgress&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));yield return null;
                Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True);
                Assert.That(customers.Snapshot,Is.Empty);Assert.That(owner.Snapshot.Orders,Is.Empty);
                var prefabs=CafeLoopRuntimeProof.Field<NavigationActor[]>(customers,"prefabs");var settings=prefabs[2%prefabs.Length].Settings;
                var clearance=(float)Call("RouteClearance",settings);Assert.That(clearance,Is.EqualTo(.911f).Within(.000001f));
                var filter=(UnityEngine.AI.NavMeshQueryFilter)typeof(NavigationLayoutAdapter).GetProperty("OwnedFilter",flags).GetValue(adapter);
                var from=new Vector3(3.5f,0,.499998f);var goal=new Vector3(.5f,0,-3.35f);
                records.Add("REBUILT_FIXTURE Long7 originalDecorAPI from="+from.ToString("F6")+" goal="+goal.ToString("F6")+
                    " currentOwnedAgentType="+filter.agentTypeID+" areaMask="+filter.areaMask+" revision="+adapter.Revision+
                    " historicalFilterIdentity=NOT_CLAIMED sameAdapterMethodAndBuildSettings=True");
                records.Add("CURRENT_SETTINGS radius="+settings.AgentRadius+" skin="+settings.CollisionSkin+" epsilon="+settings.Epsilon+
                    " arrival="+settings.ArrivalDistance+" derivedRouteClearance="+clearance+" savedClearance=.911");
                hook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);prior=hook.GetValue(customers);
                Assert.That(prior,Is.Null,"Fixed query fixture has no unrelated trace consumer");
                hook.SetValue(customers,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)((operation,caller,related,target,path,occupied,result)=>
                {
                    if(operation!="plan.exit-direct"&&operation!="plan.exit-boundary-leg")return;
                    try{records.Add("ORIGINAL_REPLAY_EVENT input="+scenario+" operation="+operation+" caller="+(caller??"NULL_QUERY_ONLY")+
                        " target="+target.ToString("F9")+" path="+Points(path)+" occupied="+Points(occupied)+" result="+result+
                        (operation=="plan.exit-direct"?" pathMeaning=originalDirectPlannerResult internalNativeRing=NOT_OBSERVED":" pathMeaning=originalAdapterNativeResult"));}
                    catch(Exception error){observerErrors++;records.Add("OBSERVER_ERROR "+error.GetType().Name+" "+error.Message);}
                }));
                var actual=(IReadOnlyList<Vector3>)null;var counterfactual=(IReadOnlyList<Vector3>)null;
                foreach(var input in new[]{full,without})
                {
                    scenario=ReferenceEquals(input,full)?"A_FULL12":"B_WITHOUT_NAMED_C12_BODY_11";
                    records.Add("PLANNER_CALL input="+scenario+" occupied="+Points(input));
                    var route=(IReadOnlyList<Vector3>)Call("PlanPickupExit",from,goal,input,settings);
                    records.Add("PLANNER_RESULT input="+scenario+" waypoints="+Points(route));
                    if(ReferenceEquals(input,full))actual=route;else counterfactual=route;
                    if(route==null)continue;
                    // Additional queries validate only this returned route; they are not original planner decisions.
                    // 仅验证原返回路线，不增加输入/候选；与上面的原planner事件分层。
                    records.Add("VERIFICATION_QUERIES_BEGIN input="+scenario+" method=originalExpandRoute adapterNativeLegQueries internalCount=NOT_OBSERVED");
                    var expanded=(IReadOnlyList<Vector3>)Call("ExpandRoute",from,route);
                    var clear=CustomerRoutePlanner.IsClear(expanded,input,clearance);
                    var fullClear=CustomerRoutePlanner.IsClear(expanded,full,clearance);
                    var c12Clear=CustomerRoutePlanner.IsClear(expanded,new[]{namedBody},clearance);
                    records.Add("VERIFICATION_RESULT input="+scenario+" expanded="+Points(expanded)+" sameInputFullClear="+clear+" full12Clear="+fullClear+" namedC12BodyClear="+c12Clear);
                    Assert.That(expanded,Is.Not.Null,"Returned route must have complete native legs");Assert.That(clear,Is.True,"Returned route must clear its complete fixed input");
                    Assert.That(route.Last(),Is.EqualTo(goal),"Returned route retains the exact exit goal");
                    if(!ReferenceEquals(input,full))records.Add("C12_REMOVAL_GEOMETRY="+(!fullClear&&!c12Clear?"SUPPORTED_FOR_THIS_RETURNED_ROUTE":"UNRESOLVED"));
                }
                records.Add("DIAGNOSTIC_SUMMARY full12="+(actual==null?"NULL_MATCHES_SAVED_SNAPSHOT":"FIXTURE_NOT_REPRODUCED")+
                    " withoutC12="+(counterfactual==null?"NULL_HYPOTHESIS_UNRESOLVED":"ROUTE_SEE_SEPARATE_VERIFICATION")+" productRED=False historicalDecision=UNKNOWN");
                Assert.That(observerErrors,Is.Zero,"Query event evidence must be complete");
            }
            finally
            {
                if(hook!=null&&customers!=null){hook.SetValue(customers,prior);hookRestored=ReferenceEquals(hook.GetValue(customers),prior);}
                UnityEngine.Random.state=random;
                Directory.CreateDirectory(directory);records.Add("CLEANUP originalHookRestored="+hookRestored+" observerErrors="+observerErrors);
                File.WriteAllText(Path.Combine(directory,"query-evidence.txt"),string.Join("\n",records)+"\n");
                TestContext.WriteLine(label+" evidence="+Path.Combine(directory,"query-evidence.txt")+" hookRestored="+hookRestored);
            }
        }
        [UnityTest] public IEnumerator ReservedPickupPublicationDefersConflictingRealServiceCorridor()
            => ObserveReservedPublicationCandidate();
        [UnityTest] public IEnumerator ControlledCollectedExitWaitsForRealEntryTraffic() => ObserveControlledEntryExitTraffic(0);
        [UnityTest] public IEnumerator ControlledCollectedExitCancelledEntryCannotKeepWaiting() => ObserveControlledEntryExitTraffic(1);
        [UnityTest] public IEnumerator ControlledCollectedExitDisabledEntryCannotKeepWaiting() => ObserveControlledEntryExitTraffic(2);
        [UnityTest] public IEnumerator Fix1SelectedPickupRetainsCandidateUntilAcceptedTrafficClears() => ObserveFix1ReplannedPrecedence(false);
        [UnityTest] public IEnumerator Fix1ReplannedExitRetainsCandidateUntilAcceptedTrafficClears() => ObserveFix1ReplannedPrecedence(true);

        [UnityTest] public IEnumerator OwnedPickupRemainingSuffixRejectsFutureTargetAfterRealProgress()
            => ObserveReservedPublicationCandidate(true);
        [UnityTest] public IEnumerator OwnedPendingStrictQualificationObservation()
            => ObserveFix1ReplannedPrecedence(false,true);

        [UnityTest] public IEnumerator OwnedPendingCancelledContributorRetiresProof() => ObserveFix1ReplannedPrecedence(false,true,1);
        [UnityTest] public IEnumerator OwnedPendingDisabledContributorRetiresProof() => ObserveFix1ReplannedPrecedence(false,true,2);
        [UnityTest] public IEnumerator OwnedPendingDeliveryProtectsWaiterAndFiniteContributor()
            => ObserveFix1ReplannedPrecedence(false,true,0,true);
        [UnityTest] public IEnumerator OwnedPendingServiceLeaseWaitReevaluatesFiniteProof()
            => ObservePendingServiceLeaseGuard(false);
        [UnityTest] public IEnumerator OwnedPendingAdmissionProtectsFullIncomingRoute()
            => ObservePendingServiceLeaseGuard(true);
        [UnityTest] public IEnumerator OwnedStaffDeliverySuffixRetiresAfterRealArrivalAndCompletion() => ObserveOwnedStaffSuffix(0);
        [UnityTest] public IEnumerator OwnedStaffDeliverySuffixRetiresOnCancel() => ObserveOwnedStaffSuffix(1);
        [UnityTest] public IEnumerator OwnedStaffDeliverySuffixRetiresOnLayoutChange() => ObserveOwnedStaffSuffix(2);
        [UnityTest] public IEnumerator DeliveryAcquisitionWitnessDefaultsOff() => ValidateDeliveryAcquisitionWitness(false);
        [UnityTest] public IEnumerator DeliveryAcquisitionWitnessColdCapturePreservesOriginalRefusal() => ValidateDeliveryAcquisitionWitness(true);
        private IEnumerator ValidateDeliveryAcquisitionWitness(bool enabled)
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var hook=typeof(CustomerFlowController).GetField("DeliveryAcquisitionObserver",flags);
            Assert.That(hook?.GetValue(customers),Is.Null,"Acquisition witness defaults off");
            var trace=enabled?new DeliveryAcquisitionTrace(this):null;
            var staff=world.RegisteredActors.Single(a=>employees.Snapshot.Any(e=>e.ActorId==a.ActorId&&e.Role==EmployeeRole.Barista));
            var requests=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byId").Count;
            var tokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();
            try
            {
                // Cold sink contract only: empty side candidates test short-circuit observation.
                // 不伪造request/finite proof，也不将此可控拒绝称为自然营业故障。
                var paths=new IReadOnlyList<Vector3>[]{null,Array.Empty<Vector3>()};
                typeof(CustomerFlowController).GetMethod("PrepareDeliveryYield",flags)
                    .Invoke(customers,new object[]{staff,Array.Empty<NavigationTarget>(),paths});
                Assert.That(typeof(CustomerFlowController).GetProperty("PickupDeliveryFailure",flags).GetValue(customers),Does.Contain("Delivery corridor blocked"));
                Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byId").Count,Is.EqualTo(requests));
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(r=>r.Token));
                if(enabled)
                {
                    Assert.That(trace.Captured,Is.True,"Actual static refusal must reach the cold hook before any task cancellation");
                    Assert.That(trace.Original,Does.Contain("physical=NOT_EVALUATED").And.Contain("owned=NOT_EVALUATED"));
                    Assert.That(trace.Original,Does.Contain("HANDLE_CONTEXT").And.Contain("BODIES").And.Contain("LEASES"));
                    Assert.That(trace.PostQueries,Is.Zero,"No real pending owner means no qualification query");
                }
            }
            finally{trace?.Finish();}
            Assert.That(hook?.GetValue(customers),Is.Null);
            yield return null;
        }

        private IEnumerator ObserveNormalDecisionWitnesses(int length)
        {
            var args=Environment.GetCommandLineArgs();
            var oldController=customers;var oldId=oldController==null?"NULL":oldController.GetEntityId().ToString();
            var inner=ObserveUserLongCounter(length,GameSpeed.Normal);
            DeliveryAcquisitionTrace acquisition=null;ExitDecisionTrace exit=null;
            var priorCleanup=controlledHandoffCleanup;
            try
            {
                // Its first yield is the nested MatrixStart; install only after scene replacement.
                // 原natural方法不变：完成首个setup后再绑定新controller，之后原样驱动inner。
                Assert.That(inner.MoveNext(),Is.True);Assert.That(inner.Current,Is.InstanceOf<IEnumerator>());
                yield return inner.Current;
                Assert.That(ReferenceEquals(oldController,customers),Is.False,"MatrixStart must replace the controller before witness attachment");
                acquisition=args.Contains("-deliveryAcquisitionWitness")?new DeliveryAcquisitionTrace(this):null;
                exit=args.Contains("-exitDecisionWitness")?new ExitDecisionTrace(this,oldId):null;
                controlledHandoffCleanup=reason=>{exit?.Finish();acquisition?.Finish();priorCleanup?.Invoke(reason);};
                while(inner.MoveNext())yield return inner.Current;
            }
            finally
            {
                try{(inner as IDisposable)?.Dispose();}
                finally{exit?.Finish();acquisition?.Finish();controlledHandoffCleanup=priorCleanup;}
            }
        }

        // Existing event consumer only: no navigation queries, Tick, or frame observer.
        // 复制原eligibility/acquire/exit事件；首个collected-exit拒绝后才做纯几何比较。
        private sealed class ExitDecisionTrace
        {
            private sealed class Source
            {
                internal string Kind,Owner;internal Vector3 Position;internal object Context,Queue;
            }
            private sealed class EventRecord
            {
                internal string Operation,Caller,Related,Result;internal int Frame,Revision;
                internal Vector3 Target;internal Vector3[] Path,Occupied;internal Source[] Sources;
                internal object[] CallerValues;internal float[] Settings;
                internal object Actor,PickupOwner;internal Vector3 Pose,Exit;
                internal object[] BindingValues;internal bool RetainedAcquisition;
                internal long AdmissionSequence;internal bool RetainedAdmission;
            }
            private const System.Reflection.BindingFlags Flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            private static readonly string[] CallerFields={"State","ResumeState","RequestId","Generation","HasCollected","ExitSequence","Segments","Replans",
                "PendingExitSegment","Assignment","CurrentSegmentTarget","FinalTarget","Route","ServiceArrived","DeliveryYieldPhase",
                "OwnedRoutePhase","OwnedRouteRevision","OwnedRouteSegments"};
            private static readonly string[] BindingFields={"selectedPickupId","SelectedRegisterId","deliveryActorId","revision"};
            private readonly CafePickupSideTests test;
            private readonly CustomerFlowController attached;
            private readonly string oldControllerId,attachedControllerId;
            private readonly List<EventRecord> events=new List<EventRecord>(256);
            private readonly List<string> errors=new List<string>();
            private readonly HashSet<string> dispatched=new HashSet<string>();
            private System.Reflection.FieldInfo hook;private object prior,installed;
            private int overflow,errorOverflow;private bool frozen,finished,hookRestored;private string receipt,failedCaller;
            private int callbacks,activeCallbacks,acceptedEligibility,acceptedCollection,dispatches;
            private bool activeInstanceMismatch;
            private int admissionRejectedDropped;
            private static object Raw(object value,string name)=>value?.GetType().GetField(name,Flags)?.GetValue(value);
            private void Error(Exception error)
            {if(errors.Count<16)errors.Add(error.GetType().Name+": "+error.Message);else errorOverflow++;}
            internal ExitDecisionTrace(CafePickupSideTests value,string previousControllerId)
            {
                test=value;attached=value.customers;oldControllerId=previousControllerId;
                attachedControllerId=attached==null?"NULL":attached.GetEntityId().ToString();
                try
                {
                    hook=typeof(CustomerFlowController).GetField("CorridorObserver",Flags);
                    if(hook==null||attached==null)throw new InvalidOperationException("Exit witness hook unavailable");
                    prior=hook.GetValue(attached);
                    installed=Delegate.Combine(prior as Delegate,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)Record);
                    hook.SetValue(attached,installed);
                }
                catch(Exception error){Error(error);}
            }
            private void RestoreHook()
            {
                try
                {
                    if(hook==null||ReferenceEquals(attached,null))return;
                    var current=hook.GetValue(attached);
                    if(ReferenceEquals(current,installed)){hook.SetValue(attached,prior);hookRestored=true;}
                    else if(ReferenceEquals(current,prior))hookRestored=true;
                    else throw new InvalidOperationException("Exit hook replaced externally; original restoration is UNKNOWN");
                }
                catch(Exception error){Error(error);}
            }
            private void Record(string operation,string caller,string related,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] occupied,string result)
            {
                if(frozen||finished)return;
                try
                {
                    callbacks++;
                    if(!ReferenceEquals(test.customers,attached)){activeInstanceMismatch=true;return;}
                    activeCallbacks++;
                    if(operation=="collection.exit-eligibility-result"&&result!=null&&result.EndsWith(" eligible=True"))acceptedEligibility++;
                    if(operation=="collection.acquire-result"&&result=="accepted")acceptedCollection++;
                    if(operation=="exit.actual-dispatch")dispatches++;
                    if(operation.StartsWith("admission.original-",StringComparison.Ordinal))
                    {
                        var visits=Raw(attached,"visits") as IDictionary;
                        if(visits==null)return;
                        foreach(DictionaryEntry pair in visits)
                        {
                            var visit=pair.Value;
                            if(caller!=null&&!Equals(pair.Key,caller))continue;
                            if(!(Raw(visit,"HasCollected") is bool collected&&collected)||
                                !(Raw(visit,"State") is CustomerVisitState state&&state==CustomerVisitState.Exiting))continue;
                            // Admission globals are attributed only to currently real collected Exit owners.
                            // incoming id/sequence来自原event；不调用binding/guard/导航来补证。
                            RecordForCaller(operation,(string)pair.Key,related,target,path,occupied,result);
                        }
                        return;
                    }
                    RecordForCaller(operation,caller,related,target,path,occupied,result);
                }
                catch(Exception error){Error(error);}
            }
            private void RecordForCaller(string operation,string caller,string related,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] occupied,string result)
            {
                if(frozen||finished||caller==null)return;
                var admission=operation.StartsWith("admission.original-",StringComparison.Ordinal);
                var planner=operation=="plan.exit-direct"||operation=="plan.exit-boundary-leg";
                var collection=operation=="collection.exit-eligibility-result"||operation=="collection.acquire-result"||
                    planner&&result!=null&&result.Contains(" purpose=collection.eligibility ");
                var segment=operation=="segment.next-check"||operation=="segment.full-check"||operation=="segment.replan-result"||operation=="segment.callback-result";
                if(!admission&&!collection&&!segment&&!planner&&operation!="exit.actual-dispatch"&&operation!="route.actual-plan"&&operation!="route.blocked"&&
                    operation!="wait.independent"&&operation!="wait.candidate"&&operation!="wait.departing"&&operation!="wait.without-finite-bodies")return;
                // Keep original collection decisions; exclude geometry enumeration/readonly probes.
                if(planner&&(result==null||!new[]{"collection.eligibility","send.actual","pending.actual","wait.counterfactual","segment.wait-eligibility","segment.replan.actual"}
                    .Any(p=>result.Contains(" purpose="+p+" "))))return;
                try
                {
                    var visits=Raw(attached,"visits") as IDictionary;
                    if(visits==null||!visits.Contains(caller))return;
                    var visit=visits[caller];
                    var arrival=operation=="segment.callback-result"&&Raw(visit,"State") is CustomerVisitState arriving&&arriving==CustomerVisitState.MovingToPickupService;
                    if(!collection&&!arrival&&!(Raw(visit,"HasCollected") is bool collected&&collected))return;
                    if(operation=="exit.actual-dispatch")dispatched.Add(caller);
                    if(!collection&&!arrival&&!dispatched.Contains(caller))return;
                    if(!collection&&!arrival&&operation!="exit.actual-dispatch"&&!(Raw(visit,"State") is CustomerVisitState state&&state==CustomerVisitState.Exiting))return;
                    var actor=Raw(visit,"Actor") as NavigationActor;
                    var actors=test.world==null?Array.Empty<NavigationActor>():test.world.RegisteredActors.Where(a=>a!=null).ToArray();
                    var sources=actors.Select(a=>new Source{Kind="body",Owner=a.ActorId,Position=a.transform.position,Context=a.isActiveAndEnabled}).ToList();
                    var entries=visits.Values.Cast<object>().ToArray();
                    foreach(var entry in entries)
                    {
                        var id=Raw(entry,"Id") as string;
                        if(Raw(entry,"Assignment") is QueueAssignment assignment)sources.Add(new Source{Kind="formal-target",Owner=id,Position=assignment.Position,Context=assignment});
                        if(Raw(entry,"ReservedPickup") is QueueAssignment reserved)sources.Add(new Source{Kind="reserved-target",Owner=id,Position=reserved.Position,Context=reserved,Queue=Raw(entry,"ReservedPickupOwner")});
                    }
                    var queues=entries.Select(v=>Raw(v,"PickupOwner") as PickupQueueService).Concat(new[]{Raw(attached,"pickupQueue") as PickupQueueService}).Where(q=>q!=null).Distinct();
                    foreach(var queue in queues)foreach(var lease in queue.ServiceLeases)
                        sources.Add(new Source{Kind="service-lease",Owner=lease.Key,Position=lease.Value,Queue=queue});
                    var settings=actor?.Settings;
                    var item=new EventRecord{Operation=operation,Caller=caller,Related=related,Target=target,Result=result,Frame=Time.frameCount,
                        Revision=test.adapter==null?-1:test.adapter.Revision,Path=path?.ToArray(),Occupied=occupied?.ToArray(),Sources=sources.ToArray(),
                        Actor=actor,PickupOwner=Raw(visit,"PickupOwner"),Pose=actor==null?default:actor.transform.position,
                        Exit=(Raw(test.customers,"exit") as Transform)?.position??default,
                        BindingValues=BindingFields.Select(f=>f=="SelectedRegisterId"?(object)test.customers.SelectedRegisterId:Raw(test.customers,f)).ToArray(),
                        CallerValues=CallerFields.Select(f=>Raw(visit,f) is IEnumerable<Vector3> points?(object)points.ToArray():Raw(visit,f)).ToArray(),
                        Settings=settings==null?null:new[]{settings.AgentRadius,settings.CollisionSkin,settings.Epsilon,settings.ArrivalDistance,settings.MaxSpeed,
                            settings.MaxSubstepSeconds,(float)settings.MaxSubstepsPerFrame,actors.Where(a=>a.Settings!=null).Select(a=>a.Settings.AgentRadius).DefaultIfEmpty(settings.AgentRadius).Max()}};
                    if(operation=="collection.acquire-result"&&result=="accepted")
                    {
                        // Retain the accepted helper's expanded path inside the same 256-event bound.
                        // 只保留已接受原结果；后续纯几何比较不重新查询Nav或guard。
                        var eligibility=events.LastOrDefault(e=>e.Caller==caller&&e.Operation=="collection.exit-eligibility-result"&&e.Target==target);
                        if(eligibility!=null&&eligibility.Result.EndsWith(" eligible=True"))eligibility.RetainedAcquisition=true;
                        item.RetainedAcquisition=true;
                    }
                    if(admission)
                    {
                        var sequence=result?.Split(' ').FirstOrDefault(p=>p.StartsWith("sequence=",StringComparison.Ordinal));
                        if(sequence==null||!long.TryParse(sequence.Substring("sequence=".Length),out item.AdmissionSequence)||item.AdmissionSequence<=0)
                            Error(new InvalidOperationException("Original admission sequence missing; correlation UNKNOWN"));
                        var accepted=operation=="admission.original-result"&&result!=null&&result.Contains(" accepted=True ");
                        if(accepted||operation=="admission.original-published"||operation=="admission.original-native-result")
                        {
                            foreach(var priorEvent in events.Where(e=>e.Caller==caller&&e.AdmissionSequence==item.AdmissionSequence&&e.AdmissionSequence>0))
                                priorEvent.RetainedAdmission=true;
                            item.RetainedAdmission=true;
                        }
                        if(operation=="admission.original-result"&&!accepted&&item.AdmissionSequence>0)
                        {
                            // Keep the latest rejected chain, never silently imply complete retry history.
                            // accepted/published/native链仍共用原256池；丢弃拒绝历史单独计数。
                            admissionRejectedDropped+=events.RemoveAll(e=>e.Caller==caller&&e.AdmissionSequence>0&&
                                e.AdmissionSequence!=item.AdmissionSequence&&!e.RetainedAdmission);
                        }
                    }
                    if(events.Count==256)
                    {
                        var discard=events.FindIndex(e=>!e.RetainedAcquisition&&!e.RetainedAdmission);events.RemoveAt(discard<0?0:discard);overflow++;
                    }
                    events.Add(item);
                    if(operation!="route.blocked")return;
                    frozen=true;failedCaller=caller;RestoreHook();receipt=Format();
                }
                catch(Exception error){Error(error);} // Diagnostic failure must never change gameplay.
            }
            private static string Text(object value)
            {
                if(value==null)return "NULL";
                if(value is Vector3 point)return point.ToString("F9");
                if(value is NavigationTarget target)return Text(target.Position);
                if(value is QueueAssignment assignment)return assignment.VisitId+"/slot="+assignment.SlotIndex+"/generation="+assignment.Generation+"/position="+Text(assignment.Position)+"/arrived="+assignment.Arrived;
                if(value is IEnumerable<Vector3> path)return "["+string.Join(";",path.Select(p=>Text(p)))+"]";
                return value.ToString();
            }
            private static string Identity(Source source)=>source.Kind+" owner="+source.Owner+" pose="+Text(source.Position)+" context="+Text(source.Context)+
                " queueReferenceHash="+(source.Queue==null?"NULL":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(source.Queue).ToString());
            private static string ReferenceIdentity(object value)=>value==null?"NULL":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value).ToString();
            private string Format()
            {
                var selected=events.Where(e=>e.Caller==failedCaller).ToArray();
                var lines=new List<string>{"FIRST_COLLECTED_EXIT_BLOCK caller="+failedCaller+" originalEvents="+selected.Length,
                    "plan.exit-direct=originalDirectPlannerResult; internalNativeDecision=NOT_OBSERVED; boundary-leg=original adapter native result",
                    "occupied arrays are original inputs; coincident identities list ALL snapshot sources, not a rerun of source eligibility predicates",
                    "WAIT event present=EVALUATED; raw NOT_EVALUATED children retain their original short-circuit labels; absent event=NOT_OBSERVED (skipped/uninstrumented/overflow not inferred)"};
                lines.Add("ACTIVE_ATTACHMENT oldControllerId="+oldControllerId+" newControllerId="+attachedControllerId+" attachedControllerId="+attachedControllerId+
                    " referenceEqualsCurrent="+ReferenceEquals(attached,test.customers)+" callbacks="+callbacks+" activeCallbacks="+activeCallbacks+
                    " acceptedEligibility="+acceptedEligibility+" acceptedCollection="+acceptedCollection+" dispatches="+dispatches+" mismatch="+activeInstanceMismatch);
                foreach(var item in selected)
                {
                    lines.Add("EVENT operation="+item.Operation+" frame="+item.Frame+" caller="+item.Caller+" related="+item.Related+" revision="+item.Revision+
                        " target="+Text(item.Target)+" originalResult="+item.Result+" path="+Text(item.Path)+" occupied="+Text(item.Occupied));
                    lines.Add("CALLER "+string.Join(" ",CallerFields.Select((f,i)=>f+"="+Text(item.CallerValues[i]))));
                    lines.Add("IDENTITY actorReference="+ReferenceIdentity(item.Actor)+" pickupOwnerReference="+ReferenceIdentity(item.PickupOwner)+
                        " actualPose="+Text(item.Pose)+" exit="+Text(item.Exit)+" pathStart="+(item.Path==null||item.Path.Length==0?"NOT_OBSERVED":Text(item.Path[0]))+
                        " pathEnd="+(item.Path==null||item.Path.Length==0?"NOT_OBSERVED":Text(item.Path[item.Path.Length-1]))+
                        " binding="+string.Join(" ",BindingFields.Select((f,i)=>f+"="+Text(item.BindingValues[i]))));
                    var s=item.Settings;
                    lines.Add(s==null?"SETTINGS UNKNOWN":"SETTINGS radius="+s[0]+" skin="+s[1]+" epsilon="+s[2]+" arrival="+s[3]+" speed="+s[4]+" substep="+s[5]+" maxSubsteps="+s[6]+
                        " maxRegisteredRadius="+s[7]+" derivedRouteClearance="+(s[0]+s[7]+s[1]+s[2])+" clearanceSource=DERIVED_FROM_SNAPSHOT_NOT_ORIGINAL_ARGUMENT");
                    lines.Add("SOURCES "+string.Join(";",item.Sources.Select(Identity)));
                    if(item.Occupied!=null)foreach(var point in item.Occupied)
                        lines.Add("OCCUPIED_POINT "+Text(point)+" coincidentIdentities="+string.Join(";",item.Sources.Where(source=>source.Position.Equals(point)).Select(Identity)));
                }
                foreach(var operation in new[]{"wait.independent","wait.candidate","wait.departing","wait.without-finite-bodies"})
                    lines.Add("WAIT_COVERAGE "+operation+"="+(selected.Any(e=>e.Operation==operation)?"EVALUATED":"NOT_OBSERVED"));
                lines.Add("ADMISSION_HISTORY rejectedEventsDropped="+admissionRejectedDropped+" allChronology="+
                    (admissionRejectedDropped>0||overflow>0?"UNKNOWN_TRUNCATED":"CAPTURED_EVENTS_ONLY")+" poolLimit=256");
                foreach(var chain in selected.Where(e=>e.AdmissionSequence>0).GroupBy(e=>e.AdmissionSequence).OrderBy(g=>g.Key))
                {
                    var before=chain.Any(e=>e.Operation=="admission.original-before");
                    var incoming=chain.Any(e=>e.Operation=="admission.original-incoming"&&e.Path!=null);
                    var accepted=chain.Any(e=>e.Operation=="admission.original-result"&&e.Result.Contains(" accepted=True "));
                    var published=chain.Any(e=>e.Operation=="admission.original-published");
                    var native=chain.Any(e=>e.Operation=="admission.original-native-result"&&e.Result.Contains(" accepted=True "));
                    lines.Add("ADMISSION_CHAIN sequence="+chain.Key+" originalBefore="+before+" originalIncoming="+incoming+
                        " activeOwnerComparisons="+chain.Count(e=>e.Operation=="admission.original-active")+" preflightAccepted="+accepted+
                        " published="+published+" nativeAccepted="+native+" links="+(before&&incoming&&accepted&&published&&native?"CAPTURED":"UNKNOWN_MISSING_OR_NOT_ACCEPTED")+
                        " acceptedIsNotPublished=True");
                }
                var acquired=selected.LastOrDefault(e=>e.Operation=="collection.acquire-result"&&e.Result=="accepted");
                var eligible=acquired==null?null:selected.LastOrDefault(e=>e.Operation=="collection.exit-eligibility-result"&&e.RetainedAcquisition&&
                    e.Frame<=acquired.Frame&&e.Target==acquired.Target);
                var dispatch=selected.FirstOrDefault(e=>e.Operation=="exit.actual-dispatch");
                var actualInputs=selected.Where(e=>dispatch!=null&&e.Frame>=dispatch.Frame&&e.Occupied!=null&&
                    (e.Operation=="segment.next-check"||e.Operation=="segment.full-check"||
                    (e.Operation=="plan.exit-direct"||e.Operation=="plan.exit-boundary-leg")&&e.Result!=null&&
                    new[]{"send.actual","pending.actual","segment.replan.actual"}.Any(p=>e.Result.Contains(" purpose="+p+" ")))).ToArray();
                lines.Add("ELIGIBILITY_CHAIN accepted="+(acquired!=null)+" originalExpandedPath="+(eligible?.Path!=null)+" actualDispatch="+(dispatch!=null)+
                    " originalActualInputs="+actualInputs.Length+" status="+(acquired==null||eligible?.Path==null||dispatch==null||actualInputs.Length==0?"UNKNOWN_MISSING_LINK":"LINKS_CAPTURED")+
                    " traceOverflow="+overflow+" (overflow prevents a complete chronology claim)");
                if(eligible?.Path!=null&&acquired!=null&&dispatch!=null)
                {
                    lines.Add("CHAIN_IDENTITY sameActor="+ReferenceEquals(eligible.Actor,dispatch.Actor)+" samePickupOwner="+ReferenceEquals(eligible.PickupOwner,dispatch.PickupOwner)+
                        " sameRevision="+(eligible.Revision==dispatch.Revision)+" side="+Text(acquired.Target)+" actualExitStart="+Text(dispatch.Pose)+
                        " sideToActualStartDistance="+Vector3.Distance(acquired.Target,dispatch.Pose)+" sameExit="+(eligible.Exit==dispatch.Exit));
                    foreach(var input in actualInputs.Take(1).Concat(actualInputs.Length>1?actualInputs.Skip(actualInputs.Length-1):Enumerable.Empty<EventRecord>()))
                    {
                        var s=input.Settings;var clearance=s==null?float.NaN:s[0]+s[7]+s[1]+s[2];
                        lines.Add("POST_DECISION_GEOMETRY originalEligibilityPath_vs_originalActualOccupied operation="+input.Operation+" frame="+input.Frame+
                            " isClear="+(s==null?"UNKNOWN":CustomerRoutePlanner.IsClear(eligible.Path,input.Occupied,clearance).ToString())+
                            " clearance="+clearance+" clearanceSource=DERIVED_FROM_ORIGINAL_SETTINGS_SNAPSHOT nativeCompleteness=NOT_REQUERIED futureOwnership=NOT_PROVEN"+
                            " addedPositions="+Text(input.Occupied.Except(eligible.Occupied??Array.Empty<Vector3>()).ToArray())+
                            " removedPositions="+Text((eligible.Occupied??Array.Empty<Vector3>()).Except(input.Occupied).ToArray()));
                    }
                }
                return string.Join("\n",lines);
            }
            internal void Finish()
            {
                if(finished)return;finished=true;RestoreHook();
                try
                {
                    var directory=Path.Combine("outputs","phase15","owned-service-route-repair","exit-decision-witness",
                        "capture-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory,TestContext.CurrentContext.Test.Name+"-exit-decision.txt"),
                        "captured="+frozen+" overflow="+overflow+" errors="+errors.Count+" errorOverflow="+errorOverflow+" originalHookRestored="+hookRestored+
                        " status="+(activeCallbacks==0||activeInstanceMismatch||!ReferenceEquals(attached,test.customers)?"UNKNOWN_NOT_ATTACHED":
                        errors.Count>0||overflow>0||admissionRejectedDropped>0||!hookRestored?"UNKNOWN_INCOMPLETE":frozen?"ORIGINAL_EVENTS_CAPTURED":"NOT_REPRODUCED")+"\n"+
                        "oldControllerId="+oldControllerId+" newControllerId="+attachedControllerId+" attachedControllerId="+attachedControllerId+
                        " referenceEqualsCurrent="+ReferenceEquals(attached,test.customers)+" callbacks="+callbacks+" activeCallbacks="+activeCallbacks+
                        " acceptedEligibility="+acceptedEligibility+" acceptedCollection="+acceptedCollection+" dispatches="+dispatches+
                        " admissionRejectedDropped="+admissionRejectedDropped+"\n"+
                        (receipt??"Original exit receipt unavailable")+"\nERRORS\n"+string.Join("\n",errors));
                    TestContext.WriteLine("EXIT_DECISION_WITNESS "+directory+" captured="+frozen+" overflow="+overflow+" errors="+errors.Count);
                }
                catch(Exception error)
                {
                    Error(error); // File/cleanup errors also must not alter the original case result.
                    try{TestContext.WriteLine("EXIT_DECISION_WITNESS status=UNKNOWN artifact-write-error="+error.GetType().Name+": "+error.Message);}catch(Exception){}
                }
            }
        }

        // Keep only the latest actual classification call; format once at the first refusal.
        // 只保留最后一次原始分支的小buffer，首次拒绝后冻结，禁止每帧格式化大trace。
        private sealed class DeliveryAcquisitionTrace
        {
            private sealed class Candidate
            {
                internal IReadOnlyList<Vector3> Path;internal Vector3[] Occupied;
                internal float Clearance;internal bool? Physical,Owned;
            }
            private readonly CafePickupSideTests test;
            private readonly System.Reflection.FieldInfo hook;
            private readonly List<Candidate> candidates=new List<Candidate>();
            private bool finished;private int omitted;
            internal bool Captured {get;private set;}
            internal string Original {get;private set;}="NOT_REPRODUCED";
            internal int PostQueries {get;private set;}
            private string corroboration="POST_DECISION_QUERY NOT_RUN";
            private const System.Reflection.BindingFlags Flags=System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            private static object Raw(object value,string field)=>value?.GetType().GetField(field,Flags)?.GetValue(value);
            private static string Text(object value)
            {
                if(value==null)return "NULL";
                if(value is Vector3 point)return point.ToString("F6");
                if(value is NavigationTarget target)return target.Position.ToString("F6");
                if(value is QueueAssignment assignment)return assignment.VisitId+"/slot="+assignment.SlotIndex+"/generation="+assignment.Generation+"/position="+Text(assignment.Position)+"/arrived="+assignment.Arrived;
                if(value is CafeWorkCheckpoint checkpoint)return checkpoint.Role+"/visit="+checkpoint.VisitId+"/order="+checkpoint.Order+"/stage="+checkpoint.Stage+"/remaining="+checkpoint.RemainingWorkSeconds;
                if(value is CafeTaskToken token)return token.SessionId+"/epoch="+token.Epoch+"/sequence="+token.Sequence+"/attempt="+token.Attempt;
                if(value is IEnumerable<Vector3> path)return "["+string.Join(";",path.Select(p=>p.ToString("F6")))+"]";
                if(value is IEnumerable<NavigationTarget> targets)return "["+string.Join(";",targets.Select(p=>p.Position.ToString("F6")))+"]";
                return value.ToString();
            }
            private static string Fields(object value,params string[] fields)=>string.Join(" ",fields.Select(f=>f+"="+Text(Raw(value,f))));
            private static string Flag(bool? value)=>value.HasValue?value.Value.ToString():"NOT_EVALUATED";
            private static string Native(NavigationActor actor)
            {
                var agent=actor?.Agent;if(agent==null)return "NULL";
                var enabled=agent.enabled;var onMesh=enabled&&agent.isOnNavMesh;
                return "enabled="+enabled+" onMesh="+onMesh+(!onMesh?" rest=NOT_EVALUATED":
                    " stopped="+agent.isStopped+" pending="+agent.pathPending+" hasPath="+agent.hasPath+" status="+agent.pathStatus);
            }
            internal DeliveryAcquisitionTrace(CafePickupSideTests value)
            {
                test=value;hook=typeof(CustomerFlowController).GetField("DeliveryAcquisitionObserver",Flags);
                if(hook==null)return; // RED still executes the original decision before the assertion.
                Assert.That(hook.GetValue(test.customers),Is.Null);
                hook.SetValue(test.customers,(Action<string,NavigationActor,IReadOnlyList<Vector3>,Vector3[],float,bool?,bool?>)Record);
            }
            private void Record(string operation,NavigationActor requester,IReadOnlyList<Vector3> path,Vector3[] occupied,float clearance,bool? physical,bool? owned)
            {
                if(Captured||finished)return;
                if(operation=="traffic.begin"){candidates.Clear();omitted=0;return;}
                if(operation=="traffic.candidate")
                {
                    if(candidates.Count<32)candidates.Add(new Candidate{Path=path,Occupied=occupied,Clearance=clearance,Physical=physical,Owned=owned});
                    else omitted++;
                    return;
                }
                if(operation!="static-refusal")return;
                Captured=true;hook.SetValue(test.customers,null); // Freeze/detach BEFORE optional post-decision corroboration.
                var entries=CafeLoopRuntimeProof.Field<IDictionary>(test.customers,"visits").Values.Cast<object>().ToArray();
                var staff=CafeLoopRuntimeProof.Field<IList>(test.employees,"employees").Cast<object>().SingleOrDefault(e=>(string)Raw(e,"Id")==requester.ActorId);
                var runner=Raw(staff,"Runner");var lines=new List<string>{"ORIGINAL_STATIC_REFUSAL frame="+Time.frameCount+" actor="+requester.ActorId+
                    " pose="+Text(requester.transform.position)+" failure="+typeof(CustomerFlowController).GetProperty("PickupDeliveryFailure",Flags).GetValue(test.customers)+" candidateOmitted="+omitted,
                    "HANDLE_CONTEXT "+Fields(runner,"state","handleId","generation","layoutRevision","taskToken","checkpoint","deliveryRoute","deliveryRouteIndex","pendingDeliveryTraffic","pendingReturnTraffic","moveLeg")};
                for(var i=0;i<candidates.Count;i++)
                {
                    var item=candidates[i];lines.Add("CANDIDATE "+i+" physical="+Flag(item.Physical)+" owned="+Flag(item.Owned)+" clearance="+item.Clearance+
                        " path="+Text(item.Path)+" exactOccupied="+Text(item.Occupied));
                }
                lines.Add("BODIES "+string.Join(";",test.world.RegisteredActors.Where(a=>a!=null).Select(a=>a.ActorId+"@"+Text(a.transform.position))));
                lines.Add("TARGETS "+string.Join(";",entries.Select(v=>Fields(v,"Id","State","Assignment","CurrentSegmentTarget","FinalTarget"))));
                var queues=entries.Select(v=>Raw(v,"PickupOwner") as PickupQueueService)
                    .Concat(new[]{CafeLoopRuntimeProof.Field<PickupQueueService>(test.customers,"pickupQueue")}).Where(q=>q!=null).Distinct();
                lines.Add("LEASES "+string.Join(";",queues.SelectMany(q=>q.ServiceLeases.Select(p=>p.Key+"@"+Text(p.Value)))));
                lines.Add("CAPACITY "+string.Join(";",test.customers.Capacity.GetReservations().Select(r=>r.OwnerId+"/"+r.Kind+"/"+r.Token+"/"+r.State)));
                foreach(var visit in entries)
                {
                    lines.Add("VISIT "+Fields(visit,"Id","State","RequestId","Generation","PendingPickupSegment","PendingExitSegment","Segments","OwnedRouteWasClear",
                        "OwnedRoutePhase","OwnedRouteRevision","OwnedRouteSegments","OwnedRouteAssignment","OwnedIssuedRequest","OwnedIssuedGeneration","OwnedRoutePath","Route")+
                        " sourceSame="+ReferenceEquals(Raw(visit,"OwnedRouteSource"),Raw(visit,"Route"))+
                        " ownerSame="+ReferenceEquals(Raw(visit,"OwnedRouteOwner"),Raw(visit,"PickupOwner"))+" adapterRevision="+test.adapter.Revision);
                    var proof=Raw(visit,"OwnedWaitProof");if(proof==null){lines.Add("PROOF NULL");continue;}
                    var moving=Raw(proof,"Moving");var actor=Raw(moving,"Actor") as NavigationActor;
                    lines.Add("PROOF "+Fields(proof,"Request","Generation","WaiterGeneration","Revision","Phase","Assignment","Target","Path","FirstLegEnd","Unissued","WaiterUnissued")+
                        " waiterRouteSame="+ReferenceEquals(Raw(proof,"WaiterRoute"),Raw(visit,"Route"))+
                        " actorSame="+ReferenceEquals(Raw(proof,"Actor"),actor));
                    lines.Add("CONTRIBUTOR "+Fields(moving,"Id","State","RequestId","Generation","Assignment","CurrentSegmentTarget","Route")+
                        " pose="+Text(actor==null?(object)null:actor.transform.position)+" native="+Native(actor)+
                        " serviceRequestPresent="+(Raw(moving,"RequestId") is long request&&CafeLoopRuntimeProof.Field<IDictionary>(test.world.Service,"byId").Contains(request)));
                }
                Original=string.Join("\n",lines); // Immutable original receipt; post-query result never enters production decisions.
                var subject=entries.FirstOrDefault(v=>(Raw(v,"PendingPickupSegment") is bool pickup&&pickup || Raw(v,"PendingExitSegment") is bool exit&&exit)&&Raw(v,"OwnedRoutePath")!=null);
                if(subject==null)return;
                PostQueries=1;
                try
                {
                    var hasOwned=(bool)typeof(CustomerFlowController).GetMethod("HasOwnedRoute",Flags).Invoke(test.customers,new[]{subject});
                    var remaining=typeof(CustomerFlowController).GetMethod("RemainingOwnedRoute",Flags).Invoke(test.customers,new[]{subject});
                    var finite=typeof(CustomerFlowController).GetMethod("CurrentFiniteWaitRoute",Flags).Invoke(test.customers,new[]{subject,remaining});
                    corroboration="POST_DECISION_QUERY subject="+Text(Raw(subject,"Id"))+" HasOwnedRoute="+hasOwned+
                        " currentOwnedPath="+Text(remaining)+" strictFinite="+(finite!=null)+" finiteContributor="+Text(finite)+
                        " OriginalShortCircuitStill=NOT_EVALUATED; no planner/driver/world advance; not used to drive behavior";
                }
                catch(Exception error){corroboration="POST_DECISION_QUERY ERROR "+error.GetType().Name+" "+error.Message;}
            }
            internal void Finish()
            {
                if(finished)return;finished=true;hook?.SetValue(test.customers,null);
                var directory=Path.Combine("outputs","phase15","owned-service-route-repair","acquire-diagnostic");Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory,TestContext.CurrentContext.Test.Name+"-acquisition-witness.txt"),
                    "captured="+Captured+" postQueries="+PostQueries+"\n"+Original+"\n"+corroboration+"\n");
            }
        }
        private IEnumerator ObserveOwnedStaffSuffix(int retirement)
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
            object Read(object value,string name)=>value.GetType().GetField(name,flags).GetValue(value);
            var staff=CafeLoopRuntimeProof.Field<IList>(employees,"employees").Cast<object>()
                .Single(e=>(EmployeeRole)Read(e,"Role")==EmployeeRole.Barista);
            var runner=(EmployeeTaskRunner)Read(staff,"Runner");var actor=(NavigationActor)Read(staff,"Actor");
            var provider=typeof(EmployeeFlowController).GetMethod("GetPickupReturnTraffic",flags);
            IReadOnlyList<NavigationTarget> Traffic()=>(IReadOnlyList<NavigationTarget>)provider.Invoke(employees,new object[]{actor.ActorId});
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True);
            var id=customers.Snapshot.Single().VisitId;
            var port=Read(staff,"Port");var core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var coreEntries=CafeLoopRuntimeProof.Field<IDictionary>(core,"entries");
            var actualById=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byId");
            var actualByActor=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor");
            object originalEntry=null;TrafficDecision originalSource=null;TrafficNativeStamp originalStamp=default;TrafficIntentKey originalKey=default;
            bool OriginalOutwardNativeBound()
            {
                Assert.That(CafeLoopRuntimeProof.Field<bool>(owner,"stepping"),Is.False,"Use the normal driver for actual employee Native preparation");
                if(runner.Snapshot.State!=EmployeeState.CarryingToPickUp || !runner.HasSelectedDeliveryRoute)return false;
                var native=Read(runner,"moveLeg");var move=Read(port,"trafficMove");if(native==null || move==null)return false;
                var request=(long)Read(native,"RequestId");var source=Read(move,"Source") as TrafficDecision;
                if(request<=0 || source==null || source.AcquiredSequence<=0 || !actualById.Contains(request) || !actualByActor.Contains(actor.ActorId))return false;
                var key=(TrafficIntentKey)Read(move,"Intent");if(key.ActorId!=actor.ActorId || !coreEntries.Contains(key))return false;
                var entry=coreEntries[key];var strict=Read(entry,"Leg");if(strict==null)return false;
                var stamp=(TrafficNativeStamp)Read(strict,"Stamp");
                if((bool)Read(entry,"Retired") || !(bool)Read(entry,"Active") || !(bool)Read(strict,"Committed") ||
                    !(bool)Read(strict,"Bound") || (bool)Read(strict,"Completed") || !ReferenceEquals(Read(strict,"ActorLife"),actor) ||
                    !ReferenceEquals(Read(entry,"JourneyPermit"),source) || stamp.RequestId!=request ||
                    stamp.LegGeneration!=(int)Read(native,"NativeLegGeneration") || stamp.LegGeneration!=(int)Read(move,"NativeGeneration") ||
                    (long)Read(actualByActor[actor.ActorId],"Id")!=request || (string)Read(actualById[request],"ActorId")!=actor.ActorId)return false;
                originalEntry=entry;originalSource=source;originalStamp=stamp;originalKey=key;return true;
            }
            yield return Until(OriginalOutwardNativeBound);
            var originalJourney=(TrafficRequest)Read(originalEntry,"JourneyRequest");
            Assert.That(originalJourney.Requirements.Any(r=>r.ResourceId=="selected-home" && r.SelectedJourney),Is.True,
                "Actual acquired employee journey must select its home suffix; a capability alone is not ownership");
            Assert.That(originalSource.ProofRoutes.Any(proof=>proof.ResourceId=="selected-home"),Is.True);
            TestContext.WriteLine("OWNED_STAFF_CURRENT_SOURCE key="+originalKey+" sourceAcquired="+originalSource.AcquiredSequence+
                " first="+Read(originalEntry,"FirstFrame")+" native="+originalStamp.RequestId+" generation="+originalStamp.LegGeneration+
                " selectedHome=True normalDriver=True canonicalFactsUnchanged=True");
            var selected=(NavigationTarget[])Read(runner,"deliveryRoute");var index=(int)Read(runner,"deliveryRouteIndex");
            var home=(NavigationTarget[])Read(runner,"returnRoute");var leg=Read(runner,"moveLeg");
            Assert.That(leg,Is.Not.Null,"Original real selected route has an accepted native leg");
            var request=(long)Read(leg,"RequestId");
            Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byId").Contains(request),Is.True);
            var published=Traffic();
            Assert.That(published,Is.Not.Null);
            Assert.That(published[0].Position,Is.EqualTo(actor.transform.position),"Carrying provider must begin at the real pose, not the future delivery endpoint");
            CollectionAssert.AreEqual(selected.Skip(index).Concat(home).Select(t=>t.Position),published.Skip(1).Select(t=>t.Position),
                "Real outward unissued suffix and existing home route are both protected");
            var taskGeneration=(int)Read(runner,"generation");
            try
            {
                // Guard-only stale generation snapshot; original request stays real and is restored without a frame.
                typeof(EmployeeTaskRunner).GetField("generation",flags).SetValue(runner,taskGeneration+1);
                Assert.That(Traffic(),Is.Null,"A leg from another task generation cannot protect a current corridor");
            }
            finally{typeof(EmployeeTaskRunner).GetField("generation",flags).SetValue(runner,taskGeneration);}
            var visit=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[id];
            var customer=(NavigationActor)Read(visit,"Actor");var goal=selected[index].Position;
            var candidate=adapter.GetCompletePath(customer.transform.position,goal);
            Assert.That(candidate,Is.Not.Null,"Actual native complete customer query");
            Assert.That((bool)typeof(CustomerFlowController).GetMethod("PickupCollectionClearsReturn",flags)
                .Invoke(customers,new object[]{visit,candidate,goal}),Is.False,"Later customer cannot publish on actual outward route");
            if(retirement!=0)
            {
                if(retirement==1)runner.Cancel("owned-route lifecycle negative control");
                else runner.InvalidateLayout(adapter.Revision+1);
                Assert.That(Traffic(),Is.Null,"Retired task must not keep a stale corridor");
                yield break;
            }
            IReadOnlyList<NavigationTarget> atDelivery=null;
            Action<CafeTaskEvent> deliveredProgress=value=>
            {if(value.Kind==CafeTaskEventKind.Progress&&value.Role==EmployeeRole.Barista&&value.Checkpoint.Stage==CafeWorkStage.Delivered)atDelivery=Traffic();};
            employees.IntegratedTaskEvent+=deliveredProgress;
            try{yield return Until(()=>runner.Snapshot.State==EmployeeState.Returning);}
            finally{employees.IntegratedTaskEvent-=deliveredProgress;}
            Assert.That(atDelivery,Is.Not.Null,"Real Delivered callback before Returning retains selected home protection");
            CollectionAssert.AreEqual(home.Select(t=>t.Position),atDelivery.Skip(1).Select(t=>t.Position),"Final real Arrived retires outward even inside Delivered callback");
            var returning=Traffic();Assert.That(returning,Is.Not.Null);
            CollectionAssert.AreEqual(((NavigationTarget[])Read(runner,"returnRoute")).Skip((int)Read(runner,"returnRouteIndex")).Select(t=>t.Position),
                returning.Skip(1).Select(t=>t.Position),"Real Arrived retires outward legs; only remaining return persists");
            yield return Until(()=>runner.Snapshot.State==EmployeeState.Idle);
            Assert.That(Traffic(),Is.Null,"Completed handle no longer protects any selected route");
            tools.ResumeCollection();yield return Until(()=>customers.Snapshot.Count==0);
        }
        [UnityTest] public IEnumerator ActualUnreservedHeadSpatialRefusalHoldsOneAdmissionWithoutAllocation()
            => ObserveUnreservedBusinessGateOriginalResult(true);
        [UnityTest] public IEnumerator DeferredReservationPreparationDoesNotInstallUnreservedHeadBusinessHold()
            => ObserveUnreservedBusinessGateOriginalResult(false);

        // One actual public reservation operation; never a private route/State or a synthetic gate record.
        // 首次真实候选上只放一个真实身体；原Deferred和空间结果分开，正常driver保持原预算。
        private IEnumerator ObserveUnreservedBusinessGateOriginalResult(bool spatial,bool recoverOneDue=false)
        {
            const string premise="UNRESERVED_GATE_PREPARATION_NOT_PROVEN: ";
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            object Read(object value,string name)=>value==null?null:value.GetType().GetField(name,flags).GetValue(value);
            var core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var visits=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");var entries=CafeLoopRuntimeProof.Field<IDictionary>(core,"entries");
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);var controller=customers;var prior=hook.GetValue(controller) as Delegate;
            var cleanupPrior=parkingEndpointWitnessCleanup;Action installedCleanup=null;Delegate installed=null;var cleaned=false;
            var errors=new List<string>();NavigationActor obstruction=null;string first=null;object visit=null,actor=null,admission=null;
            TrafficDecision captured=null,lastOriginal=null;object originalAnchor=null,originalEntry=null;TrafficIntentKey key=default;
            var deferredSeen=false;var deferredInstalled=false;var admissionHoldObserved=false;var capturedFrame=-1;
            var obstructionReleased=false;CustomerSpawnClock dueClock=null,priorClock=null;var dueDraws=0;var clockInstalled=false;
            var priorAutoSpawn=customers.AutoSpawn;
            Action cleanup=()=>
            {
                if(cleaned)return;cleaned=true;
                if(clockInstalled)
                {
                    try
                    {
                        controller.AutoSpawn=priorAutoSpawn;
                        if(ReferenceEquals(CafeLoopRuntimeProof.Field<CustomerSpawnClock>(controller,"clock"),dueClock))
                            CafeLoopRuntimeProof.Set(controller,"clock",priorClock);
                        else errors.Add("Due clock ownership changed");
                    }
                    catch(Exception error){errors.Add(error.ToString());}
                }
                try{if(ReferenceEquals(hook.GetValue(controller),installed))hook.SetValue(controller,prior);else errors.Add("Observer ownership changed");}catch(Exception error){errors.Add(error.ToString());}
                try{if(obstruction!=null){world.Unregister(obstruction);obstruction.gameObject.SetActive(false);UnityEngine.Object.Destroy(obstruction.gameObject);obstruction=null;}}catch(Exception error){errors.Add(error.ToString());}
                if(ReferenceEquals(parkingEndpointWitnessCleanup,installedCleanup))parkingEndpointWitnessCleanup=cleanupPrior;
            };
            Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string> observe=(operation,id,other,target,path,obstacles,result)=>
            {
                if(cleaned || first==null || id!=first)return;
                try
                {
                    if(spatial && !obstructionReleased && operation=="reserve.candidate" && obstruction==null)
                        obstruction=Blocker(target,"actual-unreserved-reservation-target-body");
                    if(operation=="admission.unreserved-front-held")admissionHoldObserved=true;
                    if(operation=="reserve.original-refusal")
                    {
                        var anchor=Read(visit,"PendingReservationAnchor");if(anchor==null)return;
                        var originalKey=(TrafficIntentKey)Read(anchor,"Key");if(!entries.Contains(originalKey))return;
                        var decision=core.GetDecision(originalKey);lastOriginal=decision;
                        if(decision.Kind==TrafficDecisionKind.DeferredByBudget && decision.FailureDisposition==TrafficFailureDisposition.None)
                        {
                            deferredSeen=true;
                            if(!spatial && captured==null){captured=decision;key=originalKey;originalAnchor=anchor;originalEntry=entries[key];capturedFrame=Time.frameCount;}
                        }
                        if(spatial && captured==null && obstruction!=null && decision.Kind==TrafficDecisionKind.DeferredByBudget &&
                            decision.Reason=="unpaid-eligibility/endpoint-occupied" && decision.FailureDisposition==TrafficFailureDisposition.None &&
                            Read(entries[originalKey],"UnpaidEligibilityHeld").Equals(true) && decision.AcquiredSequence==0 &&
                            decision.Blockers.Any(b=>b.Owner.ActorId==obstruction.ActorId && b.ResourceId=="body"))
                        {captured=decision;key=originalKey;originalAnchor=anchor;originalEntry=entries[key];capturedFrame=Time.frameCount;}
                    }
                    if(operation=="admission.front-refusal-observed" && lastOriginal?.Kind==TrafficDecisionKind.DeferredByBudget &&
                        lastOriginal.Reason!="unpaid-eligibility/endpoint-occupied" && lastOriginal.Reason!="unpaid-eligibility/route-capability-unavailable" &&
                        ReferenceEquals(Read(controller,"refusedPickupFront"),visit))deferredInstalled=true;
                }
                catch(Exception error){if(errors.Count<8)errors.Add(error.ToString());}
            };
            installed=Delegate.Combine(prior,observe);hook.SetValue(controller,installed);
            installedCleanup=(Action)Delegate.Combine(cleanup,cleanupPrior);parkingEndpointWitnessCleanup=installedCleanup;
            try
            {
                Assert.That(CafeLoopRuntimeProof.Field<bool>(owner,"stepping"),Is.False);Assert.That(customers.AutoSpawn,Is.False);
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                Assert.That(customers.TrySpawn(),Is.True);first=customers.Snapshot.Single().VisitId;visit=visits[first];
                actor=Read(visit,"Actor");admission=Read(visit,"Admission");
                yield return Until(()=>{Assert.That(errors,Is.Empty,premise+"observer");return captured!=null;});
                Assert.That(entries[key],Is.SameAs(originalEntry));Assert.That(Read(originalAnchor,"Visit"),Is.SameAs(visit));
                Assert.That(Read(visit,"Actor"),Is.SameAs(actor));Assert.That(Read(visit,"Admission"),Is.SameAs(admission));
                Assert.That(captured.Intent,Is.EqualTo(key));Assert.That((int)Read(originalEntry,"FirstFrame"),Is.LessThanOrEqualTo(capturedFrame));
                TestContext.WriteLine("UNRESERVED_ORIGINAL_RESULT spatial="+spatial+" id="+first+" frame="+capturedFrame+
                    " key="+key+" kind="+captured.Kind+" reason="+captured.Reason+" blockers="+string.Join(";",captured.Blockers.Select(b=>b.Owner.ActorId+"/"+b.ResourceId+"/"+b.Proof)));
                if(!spatial)
                {
                    Assert.That(deferredSeen,Is.True);Assert.That(captured.AcquiredSequence,Is.Zero);
                    Assert.That(deferredInstalled,Is.False,"RED: DeferredByBudget is unfinished preparation, not an observed spatial refusal business hold");
                    yield break;
                }
                Assert.That(captured.Kind,Is.EqualTo(TrafficDecisionKind.DeferredByBudget));
                Assert.That(captured.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None));
                Assert.That(captured.Reason,Is.EqualTo("unpaid-eligibility/endpoint-occupied"));
                Assert.That(Read(originalEntry,"UnpaidEligibilityHeld"),Is.EqualTo(true));
                Assert.That(captured.AcquiredSequence,Is.Zero);Assert.That(Read(originalEntry,"FrozenOwners"),Is.Null);
                Assert.That(customers.CafeSnapshot.Single(v=>v.VisitId==first).CounterArrived,Is.True);
                Assert.That(Read(visit,"PickupAssigned"),Is.EqualTo(false));Assert.That(owner.Snapshot.PaidVisitIds.Contains(first),Is.False);
                Assert.That(owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==first),Is.False);
                Assert.That((bool)typeof(CustomerFlowController).GetMethod("EntryClear",flags).Invoke(customers,null),Is.True,premise+"real entrance clear");
                Assert.That((bool)typeof(CustomerFlowController).GetMethod("TailClear",flags).Invoke(customers,null),Is.True,premise+"real tail clear");
                var before=CafeLoopRuntimeProof.Field<long>(customers,"nextVisit");var bodies=world.RegisteredActors.ToArray();
                var tokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();var assignments=customers.CounterAssignments.ToArray();
                if(recoverOneDue)
                {
                    // First draw is Due on the next normal frame; no manual Step/Tick or Core pump.
                    // 只使用既有clock的random测试输入，Core预算与原source保持不变。
                    priorClock=CafeLoopRuntimeProof.Field<CustomerSpawnClock>(customers,"clock");
                    dueClock=new CustomerSpawnClock(()=>{dueDraws++;return dueDraws==1?0f:1f;});
                    CafeLoopRuntimeProof.Set(customers,"clock",dueClock);clockInstalled=true;customers.AutoSpawn=true;
                    yield return Until(()=>dueDraws==1 && dueClock.RemainingSeconds==0 && admissionHoldObserved);
                    for(var heldFrame=0;heldFrame<3;heldFrame++)yield return null;
                    Assert.That(dueDraws,Is.EqualTo(1));Assert.That(dueClock.RemainingSeconds,Is.Zero);
                }
                Assert.That(customers.TrySpawn(),Is.False,"Original real prime cannot publish a new customer immediately");
                var pending=Read(customers,"pendingAdmissionAnchor");
                if(pending!=null)
                {
                    // The old bypass creates a real unborn source; await that exact source so Deferred cannot fake a rejection PASS.
                    var admissionKey=(TrafficIntentKey)Read(pending,"Key");Assert.That(entries.Contains(admissionKey),Is.True);
                    var admissionEntry=entries[admissionKey];var firstFrame=Read(admissionEntry,"FirstFrame");var finite=Read(admissionEntry,"FiniteLimit");
                    yield return Until(()=>
                    {
                        Assert.That(Read(customers,"pendingAdmissionAnchor"),Is.SameAs(pending));Assert.That(entries[admissionKey],Is.SameAs(admissionEntry));
                        Assert.That(Read(admissionEntry,"FirstFrame"),Is.EqualTo(firstFrame));Assert.That(Read(admissionEntry,"FiniteLimit"),Is.EqualTo(finite));
                        var decision=core.GetDecision(admissionKey);Assert.That(decision.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None),premise+decision.Reason);
                        return decision.Kind==TrafficDecisionKind.Granted;
                    });
                    Assert.That(customers.TrySpawn(),Is.False,"RED: genuine head spatial refusal must still refuse canonical birth after the original admission source becomes Granted");
                }
                else
                    Assert.That(admissionHoldObserved,Is.True,premise+"early rejection must be the actual observed-refusal eligibility gate, not an unrelated failure");
                Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.EqualTo(before));
                CollectionAssert.AreEqual(bodies,world.RegisteredActors);CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(r=>r.Token));
                CollectionAssert.AreEqual(assignments,customers.CounterAssignments);Assert.That(owner.Snapshot.Orders,Is.Empty);
                Assert.That(Read(visit,"Actor"),Is.SameAs(actor));Assert.That(Read(visit,"Admission"),Is.SameAs(admission));
                Assert.That(errors,Is.Empty,premise+"observer");
                if(recoverOneDue)
                {
                    var originalFirst=Read(originalEntry,"FirstFrame");var originalFinite=Read(originalEntry,"FiniteLimit");
                    var originalCursor=CafeLoopRuntimeProof.Field<IDictionary>(customers,"trafficCandidates")[key];
                    var originalQueries=core.QueriesConsumed(key);var counterClaim=Read(originalAnchor,"CounterClaim");
                    var phaseSequence=Read(visit,"TrafficPhaseSequence");var generation=Read(visit,"Generation");
                    Assert.That(dueDraws,Is.EqualTo(1));Assert.That(dueClock.RemainingSeconds,Is.Zero);
                    Assert.That(Read(controller,"refusedPickupFront"),Is.SameAs(visit));
                    Assert.That(owner.Snapshot.PaidVisitIds,Is.Empty,"A real failed unpaid reservation cannot dispatch payment");
                    obstructionReleased=true;world.Unregister(obstruction);obstruction.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(obstruction.gameObject);obstruction=null;
                    yield return null; // One ordinary frame after the real physical obstruction was removed.
                    var afterClear=core.GetDecision(key);
                    TestContext.WriteLine("UNRESERVED_CLEAR_RECOVERY id="+first+" originalEntrySame="+ReferenceEquals(entries[key],originalEntry)+
                        " anchorSame="+ReferenceEquals(Read(visit,"PendingReservationAnchor"),originalAnchor)+
                        " cursorSame="+ReferenceEquals(CafeLoopRuntimeProof.Field<IDictionary>(customers,"trafficCandidates")[key],originalCursor)+
                        " eligibilityHeld="+Read(originalEntry,"UnpaidEligibilityHeld")+
                        " decision="+afterClear.Kind+"/"+afterClear.FailureDisposition+" reason="+afterClear.Reason+
                        " first="+Read(originalEntry,"FirstFrame")+" finite="+Read(originalEntry,"FiniteLimit")+
                        " originalQueries="+originalQueries+" nowQueries="+core.QueriesConsumed(key)+
                        " due="+dueClock.RemainingSeconds+" draws="+dueDraws+" normalDriver="+!CafeLoopRuntimeProof.Field<bool>(owner,"stepping"));
                    // Retain recovery obligation; never turn a Terminal result into retry permission.
                    // 若清障后仍Terminal，明确暴露gap，不改key/预算，不伪造成功。
                    Assert.That(afterClear.FailureDisposition,Is.Not.EqualTo(TrafficFailureDisposition.Terminal),
                        "U1_REAL_RECOVERY_GAP: real blocker removal cannot recover the original unpaid reservation; "+afterClear.Reason);
                    yield return Until(()=>
                    {
                        Assert.That(entries[key],Is.SameAs(originalEntry));
                        Assert.That(Read(originalEntry,"FirstFrame"),Is.EqualTo(originalFirst));Assert.That(Read(originalEntry,"FiniteLimit"),Is.EqualTo(originalFinite));
                        Assert.That(Read(visit,"Actor"),Is.SameAs(actor));Assert.That(Read(visit,"Admission"),Is.SameAs(admission));
                        Assert.That(Read(visit,"TrafficPhaseSequence"),Is.EqualTo(phaseSequence));Assert.That(Read(visit,"Generation"),Is.EqualTo(generation));
                        Assert.That(core.GetDecision(key).FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None));
                        if(!(bool)Read(visit,"PickupAssigned"))
                        {Assert.That(Read(visit,"PendingReservationAnchor"),Is.SameAs(originalAnchor));Assert.That(Read(originalAnchor,"CounterClaim"),Is.SameAs(counterClaim));}
                        return (bool)Read(visit,"PickupAssigned");
                    });
                    Assert.That(customers.TryReservePickup(first,out var accepted),Is.True,accepted);
                    Assert.That(Read(controller,"refusedPickupFront"),Is.Null,"Only actual pickup ownership clears the hold");
                    yield return Until(()=>CafeLoopRuntimeProof.Field<long>(customers,"nextVisit")==before+1);
                    Assert.That(customers.Snapshot.Count,Is.EqualTo(2));Assert.That(dueDraws,Is.EqualTo(2));
                    Assert.That(dueClock.RemainingSeconds,Is.EqualTo(10f),"One successful admission starts one new interval");
                    yield return null;
                    Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.EqualTo(before+1));
                    Assert.That(customers.Snapshot.Count,Is.EqualTo(2),"One Due opportunity only; no backlog burst");
                    Assert.That(dueDraws,Is.EqualTo(2));Assert.That(errors,Is.Empty,premise+"observer");
                }
            }
            finally{cleanup();}
        }

        [UnityTest] public IEnumerator UnreservedActualFrontRefusalPreservesDueUntilReservationSucceeds()
            => ObserveUnreservedBusinessGateOriginalResult(true,true);

        [UnityTest] public IEnumerator UnreservedNoActualAttemptAndNonFrontDoNotHoldAdmission()
        {
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True);var id=customers.Snapshot.Single().VisitId;
            Assert.That(customers.TryReservePickup(id,out _),Is.False,"Not arrived is not an actual spatial refusal");
            yield return Until(()=>customers.CafeSnapshot.Single().CounterArrived);
            Assert.That(customers.TryReservePickup("missing-visit",out _),Is.False);
            Assert.That(customers.TrySpawn(),Is.True,"No actual front spatial attempt: next arrival stays eligible");
            var second=customers.Snapshot.Single(v=>v.VisitId!=id).VisitId;
            Assert.That(customers.TryReservePickup(second,out _),Is.False,"A real nonfront request cannot install the gate");
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            Assert.That((bool)typeof(CustomerFlowController).GetMethod("UnreservedFrontHoldsAdmission",flags).Invoke(customers,null),Is.False);
        }
        [UnityTest] public IEnumerator UnreservedPausedHeldDisabledAttemptsDoNotHoldAdmission()
        {
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True);var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>customers.CafeSnapshot.Single().CounterArrived);
            time.SetPaused();Assert.That(customers.TryReservePickup(id,out _),Is.False);
            Assert.That(customers.TrySpawn(),Is.False,"Pause still wins");time.SetNormal();owner.Step(0);
            customers.enabled=false;Assert.That(customers.TryReservePickup(id,out _),Is.False);
            Assert.That(customers.TrySpawn(),Is.False);customers.enabled=true;
            customers.HoldIntegratedVisits();Assert.That(customers.TryReservePickup(id,out _),Is.False);
            Assert.That(customers.TrySpawn(),Is.False);customers.ResumeBlockedVisits();
            Assert.That(customers.TrySpawn(),Is.True,"Invalid lifecycle requests must not leave a stale spatial gate");
        }
        [UnityTest] public IEnumerator UnreservedAssignedFrontRevalidationFailureDoesNotHoldAdmission()
        {
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True);var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>customers.CafeSnapshot.Single().CounterArrived);
            Assert.That(customers.TryReservePickup(id,out var reason),Is.True,reason);
            var pickup=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue");
            var reservation=CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations")[id];
            var target=(Vector3)reservation.GetType().GetField("Target").GetValue(reservation);
            var body=Blocker(target,"assigned-front-revalidation-body");
            try {Assert.That(customers.TryReservePickup(id,out _),Is.False,"Actual assigned reservation fails revalidation");}
            finally {world.Unregister(body);body.gameObject.SetActive(false);UnityEngine.Object.Destroy(body.gameObject);}
            Assert.That(customers.TrySpawn(),Is.True,"Existing assigned owner cannot be classified as an unreserved front");
        }
        [UnityTest] public IEnumerator UnreservedActualFrontRetirementReleasesAdmission() => ObserveUnreservedRetirement(false);
        [UnityTest] public IEnumerator UnreservedLayoutRetirementReleasesAdmission() => ObserveUnreservedRetirement(true);
        [UnityTest] public IEnumerator UnreservedSameHeadRealAdvanceRetainsObservedRefusal()
        {
            // Two real customers arrive; retire the predecessor through the real API.
            // No fabricated request, assignment, pose or Arrived state.
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True);var first=customers.Snapshot.Single().VisitId;
            yield return Until(()=>customers.CafeSnapshot.Single().CounterArrived);
            CafeLoopRuntimeProof.Set(owner,"stepping",true);
            try
            {
                Assert.That(customers.TrySpawn(),Is.True);
                var id=customers.Snapshot.Single(v=>v.VisitId!=first).VisitId;
                yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==id).CounterArrived);
                customers.RemoveVisit(first);
                var queue=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue");
                Assert.That(queue.Snapshot[0].VisitId,Is.EqualTo(id));Assert.That(queue.Snapshot[0].SlotIndex,Is.EqualTo(1));
                Assert.That(customers.TryReservePickup(id,out var reason),Is.True,reason);customers.ReleasePickupAssignment(id);
                var pickup=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue");
                var slots=CafeLoopRuntimeProof.Field<Vector3[]>(pickup,"waitSlots");
                var retained=CafeLoopRuntimeProof.Field<Vector3[]>(customers,"pickupWaitSlots");
                var original=slots[0];var oldRetained=retained[0];
                try
                {
                    slots[0]=retained[0]=world.RegisteredActors.First(a=>a.ActorId!=id).transform.position;
                    Assert.That(pickup.CanReserve,Is.True);Assert.That(customers.TryReservePickup(id,out _),Is.False);
                }
                finally {slots[0]=original;retained[0]=oldRetained;}
                var beforeGeneration=queue.Snapshot[0].Generation;var revision=adapter.Revision;
                customers.Step(1f/60); // Actual counter advance owns a real request.
                var entry=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[id];
                object Read(string name)=>entry.GetType().GetField(name).GetValue(entry);
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                bool TailClear()=>(bool)typeof(CustomerFlowController).GetMethod("TailClear",flags).Invoke(customers,null);
                for(var f=0;f<300 && Read("State").Equals(CustomerVisitState.Advancing) && !TailClear();f++) yield return null;
                Assert.That(Read("State"),Is.EqualTo(CustomerVisitState.Advancing),"Fixture must retain real advancing request at admission seam");
                var request=(long)Read("RequestId");Assert.That(request,Is.GreaterThan(0));
                Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byId").Contains(request),Is.True);
                Assert.That(TailClear(),Is.True,"Natural tail must be physically clear before testing this gate");
                Assert.That(queue.Snapshot[0].VisitId,Is.EqualTo(id));Assert.That(queue.Snapshot[0].Generation,Is.GreaterThan(beforeGeneration));
                Assert.That(queue.Snapshot[0].Arrived,Is.False);Assert.That(adapter.Revision,Is.EqualTo(revision));
                Assert.That(Read("PickupAssigned"),Is.False);Assert.That(CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue"),Is.SameAs(pickup));
                var next=CafeLoopRuntimeProof.Field<long>(customers,"nextVisit");var tokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();
                var actors=world.RegisteredActors.ToArray();var assignments=customers.CounterAssignments.ToArray();
                TestContext.WriteLine("SAME_HEAD_ADVANCE actualRequest="+request+" generation="+Read("Generation")+" assignment="+queue.Snapshot[0].Generation+" head="+id+" layout="+revision+" canReserve="+pickup.CanReserve);
                Assert.That(customers.TrySpawn(),Is.False,"Same unpaid Counter head advancing must retain its observed refusal before ID/token/Actor creation");
                Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.EqualTo(next));
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(r=>r.Token));CollectionAssert.AreEqual(actors,world.RegisteredActors);
                CollectionAssert.AreEqual(assignments,customers.CounterAssignments);
                yield return Until(()=>Read("State").Equals(CustomerVisitState.Queued));
                Assert.That(customers.TrySpawn(),Is.False,"Same head arrival alone does not acquire pickup space");
                Assert.That(customers.TryReservePickup(id,out var accepted),Is.True,accepted);
                Assert.That(customers.TrySpawn(),Is.True,"Only real pickup acceptance releases this same-head hold");
            }
            finally {CafeLoopRuntimeProof.Set(owner,"stepping",false);}
        }

        private IEnumerator ObserveUnreservedRetirement(bool layout)
        {
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True);var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>customers.CafeSnapshot.Single().CounterArrived);
            Assert.That(customers.TryReservePickup(id,out var reason),Is.True,reason);customers.ReleasePickupAssignment(id);
            var pickup=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue");
            var slots=CafeLoopRuntimeProof.Field<Vector3[]>(pickup,"waitSlots");
            var retained=CafeLoopRuntimeProof.Field<Vector3[]>(customers,"pickupWaitSlots");
            var original=slots[0];var oldRetained=retained[0];
            try
            {
                slots[0]=retained[0]=world.RegisteredActors.First(a=>a.ActorId!=id).transform.position;
                Assert.That(pickup.CanReserve,Is.True);Assert.That(customers.TryReservePickup(id,out _),Is.False);
            }
            finally {slots[0]=original;retained[0]=oldRetained;}
            Assert.That(customers.TrySpawn(),Is.False);
            if(layout)
            {
                var revision=adapter.Revision;var decor=Find<DecorationModeController>();decor.EnterDecorationMode();
                var machine=Find<CafeLayoutRuntime>().FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
                Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,machine.InstanceId),Is.True);
                Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True);Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);
                decor.ExitDecorationMode();yield return null;
                Assert.That(adapter.Revision,Is.GreaterThan(revision));Assert.That(customers.Snapshot,Is.Empty);
                time.SetNormal();tools.ResumeBlockedFlow();
                yield return Until(()=>owner.CanProgress&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            }
            else {customers.RemoveVisit(id);yield return null;}
            Assert.That(customers.TrySpawn(),Is.True,"Retired actual head/layout must release the later arrival");
            Assert.That(customers.Snapshot.Single().VisitId,Is.Not.EqualTo(id));
        }

        [Test] public void OwnedCurrentLegProjectionKeepsSelfNearUnissuedGeometry()
        {
            // Literal geometry: actor lies exactly on a later leg as well as current leg.
            // The later leg remains owned; a nearest-global-path algorithm would erase it.
            var path=new[]{new Vector3(0,0,0),new Vector3(4,0,0),new Vector3(4,0,3),new Vector3(2,0,0),new Vector3(2,0,-3)};
            var method=typeof(CustomerFlowController).GetMethod("TrimIssuedLeg",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            var remaining=(IReadOnlyList<Vector3>)method.Invoke(null,new object[]{path,1,new Vector3(2,0,0),.001f});
            CollectionAssert.AreEqual(new[]{new Vector3(2,0,0),new Vector3(4,0,0),new Vector3(4,0,3),new Vector3(2,0,0),new Vector3(2,0,-3)},remaining);
            Assert.That(CustomerRoutePlanner.IsClear(remaining,new[]{new Vector3(3,0,0)},.1f),Is.False);
            Assert.That(CustomerRoutePlanner.IsClear(remaining,new[]{new Vector3(.5f,0,0)},.1f),Is.True);
            var off=(IReadOnlyList<Vector3>)method.Invoke(null,new object[]{path,1,new Vector3(2,0,2),.001f});
            CollectionAssert.AreEqual(path,off,"Unproven off-leg progress keeps all geometry");
        }

        private IEnumerator ObserveReservedPublicationCandidate(bool consumedPrefix=false)
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            object Read(object v,string name)=>v.GetType().GetField(name,flags).GetValue(v);
            object Call(string name,params object[] args)=>typeof(CustomerFlowController).GetMethod(name,flags).Invoke(customers,args);
            yield return PrepareControlledPhotoSix();
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True);var first=customers.Snapshot.Single().VisitId;
            yield return Until(()=>owner.Snapshot.Delivery.Count==1&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True);var second=customers.Snapshot.Single(v=>v.VisitId!=first).VisitId;
            yield return Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==second).CounterArrived);
            var entries=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");var leader=entries[first];
            var pickup=(PickupQueueService)Read(leader,"PickupOwner");var body=(NavigationActor)Read(leader,"Actor");
            var tokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();
            var paid=owner.Snapshot.PaidVisitIds.ToArray();
            var sides=CafeLoopRuntimeProof.Field<NavigationTarget[]>(customers,"pickupSides");
            var slots=CafeLoopRuntimeProof.Field<Vector3[]>(pickup,"waitSlots");var originalSlots=(Vector3[])slots.Clone();
            var retainedField=typeof(CustomerFlowController).GetField("pickupWaitSlots",flags);
            var retained=retainedField?.GetValue(customers) as Vector3[];
            var originalRetained=retained==null?null:(Vector3[])retained.Clone();
            var selected=(IReadOnlyList<Vector3>)null;var published=0;var unissuedProtected=false;NavigationActor prefixBody=null;
            Vector3 Candidate(IReadOnlyList<Vector3> path)
            {
                var candidates=new List<Vector3>();
                for(var i=1;i<path.Count;i++) for(var j=1;j<=8;j++) candidates.Add(Vector3.Lerp(path[i-1],path[i],j/8f));
                var point=candidates.FirstOrDefault(p=>world.RegisteredActors.All(a=>Vector3.Distance(a.transform.position,p)>1.05f)&&
                    pickup.ServiceLeases.Values.All(pose=>Vector3.Distance(pose,p)>1.05f)&&adapter.IsPointWalkable(p));
                Assert.That(candidates.Contains(point),Is.True,"Projected NEXT-slot candidate must be clear of real bodies and actual owned leases; not a natural planner-slot claim");
                return point;
            }
            var observerField=typeof(CustomerFlowController).GetField("PickupObserver",flags);
            Action<string,string,PickupQueueService,QueueAssignment> observer=(operation,id,actualOwner,assignment)=>
            {
                if(operation=="reserve.published"&&id==second) published++;
                if(operation=="collection.route-selected"&&id==first)
                {
                    Assert.That((long)Read(leader,"RequestId"),Is.Zero);
                    Assert.That(pickup.TryGetServiceLease(first,out _),Is.True,"Actual lease already exists at unissued acquisition seam");
                    selected=(IReadOnlyList<Vector3>)Call("ExpandRoute",body.transform.position,(Queue<Vector3>)Read(leader,"Route"));
                    // Read-only projection at the real acquisition seam; no fake RequestId/lease.
                    Assert.That((bool)Call("NewPickupTargetClear",entries[second],Candidate(selected)),Is.False);
                    unissuedProtected=true;
                }
            };
            CafeLoopRuntimeProof.Set(owner,"stepping",true); // Freeze canonical work only; native World still owns actual movement.
            try
            {
                customers.ReleasePickupAssignment(second); // Only unused spatial reservation; capacity tokens stay owned.
                Assert.That((bool)Read(entries[second],"PickupAssigned"),Is.False);
                observerField.SetValue(customers,observer);
                // Select an existing legal side through the real acquisition path, not a fake lease.
                CafeLoopRuntimeProof.Set(customers,"pickupSides",new[]{sides.OrderByDescending(s=>Vector3.Distance(s.Position,body.transform.position)).First()});
                Assert.That(customers.TryMoveToPickupService(first,out var reason),Is.True,reason);
                CafeLoopRuntimeProof.Set(customers,"pickupSides",sides);
                Assert.That(selected,Is.Not.Null);Assert.That((long)Read(leader,"RequestId"),Is.GreaterThan(0));
                Assert.That(unissuedProtected,Is.True);
                if(consumedPrefix)
                {
                    var origin=selected[0];var issued=Read(leader,"RequestId");
                    for(var frame=0;frame<300&&Vector3.Distance(body.transform.position,origin)<1.3f;frame++) yield return null;
                    Assert.That(Read(leader,"RequestId"),Is.EqualTo(issued),"Observe partial progress on the SAME real issued leg");
                    Assert.That(Vector3.Distance(body.transform.position,origin),Is.GreaterThanOrEqualTo(1.3f));
                    prefixBody=Blocker(origin,"owned-consumed-prefix-real-body");
                    TestContext.WriteLine("OWNED_PREFIX_GEOMETRY selected="+string.Join(";",selected)+" cache="+string.Join(";",(IReadOnlyList<Vector3>)Read(leader,"OwnedRoutePath"))+
                        " remaining="+string.Join(";",(IReadOnlyList<Vector3>)Call("RemainingOwnedRoute",leader))+" pose="+body.transform.position.ToString("F7")+
                        " ends="+string.Join(",",(List<int>)Read(leader,"OwnedRouteLegEnds"))+" segment="+Read(leader,"Segments")+" cached="+Read(leader,"OwnedRouteSegments"));
                    // This curved native service leg may be off the cached polyline.
                    // Conservative old-prefix retention is allowed when projection is unproven;
                    // exact on-leg past-only behavior is covered by the literal projection case.
                    TestContext.WriteLine("OWNED_PREFIX_PAST_CONTROL eligible="+Call("NewPickupTargetClear",entries[second],origin)+" (off-leg may remain conservative)");
                    TestContext.WriteLine("OWNED_PREFIX realRequest="+issued+" origin="+origin+" actualPose="+body.transform.position);
                }
                var remaining=consumedPrefix?new[]{body.transform.position}.Concat(selected.Skip(1)).ToArray():selected;
                var candidate=Candidate(remaining);
                Assert.That((bool)Call("NewPickupTargetClear",entries[second],candidate),Is.False,
                    "A body on consumed prefix must not discard FUTURE suffix target protection");
                if(consumedPrefix)
                {
                    // Guard-only snapshots, restored without a frame. The request is real.
                    var cacheFlag=Read(leader,"OwnedRouteWasClear");var actualRequest=Read(leader,"RequestId");var actualState=Read(leader,"State");
                    try
                    {
                        leader.GetType().GetField("OwnedRouteWasClear").SetValue(leader,false);
                        Assert.That((bool)Call("HasOwnedRoute",leader),Is.True,"Real accepted request retains baseline denial authority even when candidate body check was blocked");
                        Assert.That((bool)Call("NewPickupTargetClear",entries[second],candidate),Is.False);
                        leader.GetType().GetField("RequestId").SetValue(leader,0L);
                        Assert.That((bool)Call("HasOwnedRoute",leader),Is.False,"Unqualified RequestId0 candidate cannot inherit active authority");
                        leader.GetType().GetField("RequestId").SetValue(leader,actualRequest);
                        leader.GetType().GetField("State").SetValue(leader,CustomerVisitState.Blocked);
                        Assert.That((bool)Call("HasOwnedRoute",leader),Is.False,"Genuine Blocked phase cannot be revived by old request binding");
                    }
                    finally
                    {
                        leader.GetType().GetField("OwnedRouteWasClear").SetValue(leader,cacheFlag);
                        leader.GetType().GetField("RequestId").SetValue(leader,actualRequest);
                        leader.GetType().GetField("State").SetValue(leader,actualState);
                    }
                }
                TestContext.WriteLine("PUBLICATION_FIXTURE selected="+string.Join(";",selected)+" sides="+string.Join(";",sides.Select(s=>s.Position)));
                var count=CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations").Count;
                var generation=CafeLoopRuntimeProof.Field<int>(pickup,"generation");
                var request=Read(leader,"RequestId");Assert.That(pickup.TryGetServiceLease(first,out var lease),Is.True);
                try
                {
                    // No yield while projected NEXT unreserved slot differs. Existing entries/leases are untouched.
                    slots[count]=candidate;if(retained!=null) retained[count]=candidate;
                    Assert.That(customers.TryReservePickup(second,out _),Is.False);
                    Assert.That(published,Is.Zero,"Reject BEFORE publication, not reserve/remove rollback");
                    Assert.That(CafeLoopRuntimeProof.Field<int>(pickup,"generation"),Is.EqualTo(generation));
                    Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations").Count,Is.EqualTo(count));
                    Assert.That(Read(leader,"RequestId"),Is.EqualTo(request));Assert.That(pickup.TryGetServiceLease(first,out var stillOwned)&&stillOwned==lease,Is.True);
                    CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(r=>r.Token));CollectionAssert.AreEqual(paid,owner.Snapshot.PaidVisitIds);
                }
                finally { Array.Copy(originalSlots,slots,slots.Length);if(retained!=null) Array.Copy(originalRetained,retained,retained.Length); }
                yield return Until(()=>Read(leader,"State").Equals(CustomerVisitState.PickupService));
                try
                {
                    slots[count]=candidate;if(retained!=null) retained[count]=candidate;
                    Assert.That(customers.TryReservePickup(second,out var retiredReason),Is.True,"Earlier route actually retired: "+retiredReason);
                    Assert.That(CafeLoopRuntimeProof.Field<int>(pickup,"generation"),Is.GreaterThan(generation));
                    customers.ReleasePickupAssignment(second);
                }
                finally { Array.Copy(originalSlots,slots,slots.Length);if(retained!=null) Array.Copy(originalRetained,retained,retained.Length); }
                Assert.That(customers.TryReservePickup(second,out _),Is.True,"Normal candidate restored through real reserve lifecycle");
                TestContext.WriteLine("CONTROLLED_PUBLICATION candidate="+candidate.ToString("F6")+" earlierRequest="+request+" retiredByRealArrived=True");
            }
            finally
            {
                if(prefixBody!=null){world.Unregister(prefixBody);prefixBody.gameObject.SetActive(false);UnityEngine.Object.Destroy(prefixBody.gameObject);}
                Array.Copy(originalSlots,slots,slots.Length);if(retained!=null) Array.Copy(originalRetained,retained,retained.Length);
                CafeLoopRuntimeProof.Set(customers,"pickupSides",sides);observerField.SetValue(customers,null);CafeLoopRuntimeProof.Set(owner,"stepping",false);
            }
        }

        private IEnumerator PrepareControlledPhotoSix()
        {
            yield return MatrixStart();var runtime=Find<CafeLayoutRuntime>();var decor=Find<DecorationModeController>();
            var cash=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(1,5));
            var machine=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(4,6));
            yield return UserMoveCounterSupport(cash.InstanceId,new GridPosition(6,4));
            yield return UserMoveCounterSupport(machine.InstanceId,new GridPosition(1,7));
            var equipment=runtime.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,equipment.InstanceId),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True);Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);
            for(var x=1;x<6;x++) yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(x,4),0);
            var support=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(1,4));
            Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint,"pickup.point",new SurfaceSlotAddress(support.InstanceId,"slot.0")),Is.True);
            Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);decor.ExitDecorationMode();
            yield return Until(()=>owner.CanProgress&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));yield return null;
        }

        [UnityTest] public IEnumerator BoundaryTangentRetainsNativeEdgeHoleAndBodyGuards()
        {
            yield return MatrixStart();var runtime=Find<CafeLayoutRuntime>();var decor=Find<DecorationModeController>();
            for(var x=2;x<=7;x++) yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(x,5),0);
            var support=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(7,5));
            Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint,"pickup.point",new SurfaceSlotAddress(support.InstanceId,"slot.0")),Is.True);
            Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);decor.ExitDecorationMode();
            yield return Until(()=>owner.CanProgress&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            var actor=Blocker(new Vector3(-3.525f,0,-.5000002f),"boundary-guard-native");NavigationActor obstruction=null;
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var driver=CafeLoopRuntimeProof.Field<IDictionary>(world,"drivers")[actor];
            var helper=typeof(NavMeshMovementDriver).GetMethod("CanKeepBoundaryTangent",flags);
            var clamp=typeof(NavMeshMovementDriver).GetMethod("ClampToNavMesh",flags);
            var filter=new UnityEngine.AI.NavMeshQueryFilter{agentTypeID=actor.Agent.agentTypeID,areaMask=actor.Agent.areaMask};
            MovementResult? result=null;
            var start=world.Service.MoveTo(actor.ActorId,new NavigationTarget(new Vector3(-3.546059f,0,2.5f)),r=>result=r);
            Assert.That(start.Accepted,Is.True);
            try
            {
                for(var f=0;f<100&&Mathf.Abs(actor.transform.position.z)>actor.Settings.Epsilon;f++) yield return null;
                Assert.That(Mathf.Abs(actor.transform.position.z),Is.LessThanOrEqualTo(actor.Settings.Epsilon),"Observe real native arrival at the boundary seam");
                var pose=actor.transform.position;var forward=new Vector3(0,0,.02f);
                Assert.That(UnityEngine.AI.NavMesh.Raycast(pose,pose+forward,out var first,filter),Is.True,"Real degenerate edge query fixture");
                Assert.That(UnityEngine.AI.NavMesh.Raycast(pose,pose+forward,out var second,filter),Is.True);
                Assert.That((bool)helper.Invoke(driver,new object[]{forward,first,second}),Is.True);
                var outward=new Vector3(-.02f,0,0);
                UnityEngine.AI.NavMesh.Raycast(pose,pose+outward,out var outside,filter);
                Assert.That((bool)helper.Invoke(driver,new object[]{outward,outside,outside}),Is.False,"Real outward edge never receives tangent permission");
                Assert.That(((Vector3)clamp.Invoke(driver,new object[]{outward})).magnitude,Is.LessThan(.0011f));
                var hole=new Vector3(-1.5f,0,1.5f)-pose;
                Assert.That(UnityEngine.AI.NavMesh.Raycast(pose,pose+hole,out var holeHit,filter),Is.True);
                Assert.That((bool)helper.Invoke(driver,new object[]{hole,holeHit,holeHit}),Is.False,"Furniture/navmesh hole cannot use a different owned direction");
                var beyond=new Vector3(0,0,20);
                var partial=new UnityEngine.AI.NavMeshPath();UnityEngine.AI.NavMesh.CalculatePath(pose,pose+beyond,filter,partial);
                Assert.That(partial.status!=UnityEngine.AI.NavMeshPathStatus.PathComplete||partial.corners.Length==0||
                    Vector3.Distance(partial.corners.Last(),pose+beyond)>.05f,Is.True,"Native unavailable endpoint/partial proof remains ineligible");
                Assert.That((bool)helper.Invoke(driver,new object[]{beyond,first,second}),Is.False);
                var detourEnd=new Vector3(1.5f,0,2.5f);var detour=new UnityEngine.AI.NavMeshPath();
                Assert.That(UnityEngine.AI.NavMesh.CalculatePath(pose,detourEnd,filter,detour),Is.True);
                Assert.That(detour.corners.Length,Is.GreaterThan(2),"Actual counter requires a multi-corner detour");
                Assert.That((bool)helper.Invoke(driver,new object[]{detourEnd-pose,first,second}),Is.False);
                obstruction=Blocker(new Vector3(-3.525f,0,1),"boundary-dynamic-blocker");
                for(var f=0;f<20;f++) yield return null;
                Assert.That(Vector3.Distance(actor.transform.position,obstruction.transform.position),Is.GreaterThanOrEqualTo(.92f),"Original full-body sweep still blocks real motion");
                Assert.That(result.HasValue,Is.False,"Blocked body is never a fake arrival");
                world.Unregister(obstruction);UnityEngine.Object.Destroy(obstruction.gameObject);obstruction=null;
                for(var f=0;f<400&&!result.HasValue;f++) yield return null;
                Assert.That(result.HasValue,Is.True);Assert.That(result.Value.Status,Is.EqualTo(MovementStatus.Arrived),result.Value.Reason.ToString());
                Assert.That((bool)helper.Invoke(driver,new object[]{forward,first,second}),Is.False,"Actual request retirement removes boundary permission");
                TestContext.WriteLine("BOUNDARY_NATIVE_GUARDS actualRequest="+start.RequestId+" nativeArrived=True edge/hole/unavailableEndpoint/detour/body/unowned rejected");
            }
            finally
            { if(obstruction!=null){world.Unregister(obstruction);UnityEngine.Object.Destroy(obstruction.gameObject);}world.Unregister(actor);UnityEngine.Object.Destroy(actor.gameObject); }
        }

        [UnityTest] public IEnumerator ReservedPickupCorridorRejectsTailBeforeCreationAndRetainsDueOpportunity()
        {
            // Controlled proposed-tail seam: real unpaid reservation, unchanged bodies.
            // 只暂存下一队尾候选；不伪造付款、lease、request或Arrived。
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            object Call(string name,params object[] args)=>typeof(CustomerFlowController).GetMethod(name,flags).Invoke(customers,args);
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True);
            var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>customers.CafeSnapshot.Single().CounterArrived);
            Assert.That(customers.TryReservePickup(id,out var reason),Is.True,reason);
            var entries=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");
            var visit=entries[id];
            var actor=world.RegisteredActors.Single(a=>a.ActorId==id);
            var pickup=(PickupQueueService)visit.GetType().GetField("PickupOwner").GetValue(visit);
            var reservation=CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations")[id];
            Assert.That((bool)reservation.GetType().GetField("Paid").GetValue(reservation),Is.False,"Needs actual unpaid reservation window");
            var target=(Vector3)reservation.GetType().GetField("Target").GetValue(reservation);
            var route=(IReadOnlyList<Vector3>)Call("BuildPickupWaitRoute",actor.transform.position,target,id,actor.Settings);
            var expanded=(IReadOnlyList<Vector3>)Call("ExpandRoute",actor.transform.position,route);
            Assert.That(expanded,Is.Not.Null);
            var queue=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue");
            var originalSlots=CafeLoopRuntimeProof.Field<Vector3[]>(queue,"slots");
            var prefab=CafeLoopRuntimeProof.Field<NavigationActor[]>(customers,"prefabs")[1%CafeLoopRuntimeProof.Field<NavigationActor[]>(customers,"prefabs").Length];
            var originalClock=CafeLoopRuntimeProof.Field<CustomerSpawnClock>(customers,"clock");
            var clock=new CustomerSpawnClock(()=>.1f); // Deterministic real clock; same due instance across refusal/release.
            var nextVisit=CafeLoopRuntimeProof.Field<long>(customers,"nextVisit");
            var tokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();
            var paid=owner.Snapshot.PaidVisitIds.ToArray();
            var originalAuto=customers.AutoSpawn;
            var candidates=new List<Vector3>();
            for(var i=1;i<expanded.Count;i++)
                for(var j=1;j<=4;j++) candidates.Add(Vector3.Lerp(expanded[i-1],expanded[i],j/4f));
            var tail=candidates.FirstOrDefault(p=>world.RegisteredActors.All(a=>Vector3.Distance(a.transform.position,p)>1.15f)&&
                adapter.IsPointWalkable(p)&&Call("BuildRoute",CafeLoopRuntimeProof.Field<Transform>(customers,"spawn").position,
                    p,null,prefab.Settings,null,true)!=null);
            Assert.That(candidates.Contains(tail),Is.True,"Fixture needs a body-clear tail on real reserved path");
            // Read-only projection controls on real identities; restore before any yield.
            var ownerField=visit.GetType().GetField("PickupOwner");
            try
            {
                ownerField.SetValue(visit,null);
                Assert.That((bool)Call("CanStartAdmission",tail,prefab.Settings),Is.True,"Changed pickup owner retires old corridor evidence");
            }
            finally { ownerField.SetValue(visit,pickup); }
            var ordinaryTail=queue.NextAdmissionPosition.Value;
            Assert.That((bool)Call("CanStartAdmission",ordinaryTail,prefab.Settings),Is.True,"Nonintersecting natural tail stays eligible");
            try
            {
                queue.UpdateSlots(new[]{originalSlots[0],tail});
                typeof(CustomerFlowController).GetField("clock",flags).SetValue(customers,clock);
                Assert.That(clock.Tick(5,true,false),Is.True);
                customers.AutoSpawn=true;
                customers.Step(1f/60);
                Assert.That(customers.Snapshot.Count,Is.EqualTo(1),"Reserved pickup corridor must refuse before actor creation");
                Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.EqualTo(nextVisit));
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(r=>r.Token));
                CollectionAssert.AreEqual(paid,owner.Snapshot.PaidVisitIds);
                Assert.That(clock.RemainingSeconds,Is.Zero,"Keep this same due opportunity");
                var before=actor.transform.position;time.SetPaused();customers.Step(1f/60);yield return null;
                Assert.That(actor.transform.position,Is.EqualTo(before));Assert.That(clock.RemainingSeconds,Is.Zero);
                time.SetNormal();
                owner.Step(0); // Publish the ordinary resumed admission gate before testing the customer seam.
                var originalGeneration=(int)reservation.GetType().GetField("Generation").GetValue(reservation);
                customers.ReleasePickupAssignment(id);
                Assert.That(visit.GetType().GetField("ReservedPickupPath").GetValue(visit),Is.Null);
                Assert.That((bool)Call("CanStartAdmission",tail,prefab.Settings),Is.True);
                Assert.That(customers.TryReservePickup(id,out var refreshedReason),Is.True,refreshedReason);
                var refreshed=CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations")[id];
                var renewed=(QueueAssignment?)visit.GetType().GetField("ReservedPickup").GetValue(visit);
                Assert.That(renewed.Value.Generation,Is.EqualTo((int)refreshed.GetType().GetField("Generation").GetValue(refreshed)));
                Assert.That(renewed.Value.Generation,Is.GreaterThan(originalGeneration),"Genuine cancel/re-reserve changes reservation generation");
                Assert.That((bool)Call("CanStartAdmission",tail,prefab.Settings),Is.False,"New accepted generation owns its freshly checked corridor");
                customers.ReleasePickupAssignment(id);
                TestContext.WriteLine("CORRIDOR_RELEASE assigned="+visit.GetType().GetField("PickupAssigned").GetValue(visit)+
                    " transit="+visit.GetType().GetField("PickupInTransit").GetValue(visit)+" state="+visit.GetType().GetField("State").GetValue(visit)+
                    " canStart="+Call("CanStartAdmission",tail,prefab.Settings)+" due="+clock.RemainingSeconds+
                    " speed="+time.CurrentSpeed+" resumeBlocked="+time.IsResumeBlocked+" status="+customers.Status+" tail="+tail);
                customers.Step(1f/60);
                Assert.That(customers.Snapshot.Count,Is.EqualTo(2),"Actual cancellation releases the same due arrival");
                Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.EqualTo(nextVisit+1));
                Assert.That(clock.RemainingSeconds,Is.GreaterThan(0));
                CollectionAssert.AreEqual(paid,owner.Snapshot.PaidVisitIds,"Admission does not pay or regenerate an order");
                TestContext.WriteLine("CORRIDOR_ADMISSION tail="+tail.ToString("F6")+" reservedTarget="+target.ToString("F6"));
            }
            finally
            {
                queue.UpdateSlots(originalSlots);
                typeof(CustomerFlowController).GetField("clock",flags).SetValue(customers,originalClock);
                customers.AutoSpawn=originalAuto;time.SetNormal();
            }
        }

        [UnityTest] public IEnumerator ReservedPickupCorridorRetiresOnRealDecorationRebuild()
        {
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True);
            var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>customers.CafeSnapshot.Single().CounterArrived);
            // Positive setup only: normal cashier driver owns the first asynchronous reservation.
            // 等真实原准备完成后才进入Decor被测边界；不pump、不freeze、不代造Source/claim。
            var originalVisit=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[id];
            var originalActor=MovementRead(originalVisit,"Actor");var originalAdmission=MovementRead(originalVisit,"Admission");
            var originalPhase=MovementRead(originalVisit,"TrafficPhaseSequence");
            var originalTokens=customers.Capacity.GetReservations().Where(row=>row.OwnerId==id).Select(row=>row.Token).ToArray();
            var counterOwner=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue");
            Assert.That(counterOwner.TryCaptureAssignment(id,out var originalCounterClaim),Is.True);
            Assert.That(originalCounterClaim.Assignment.SlotIndex,Is.Zero);Assert.That(originalCounterClaim.Assignment.Arrived,Is.True);
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var trafficEntries=CafeLoopRuntimeProof.Field<IDictionary>(coordinator,"entries");
            object preparedAnchor=null,preparedEntry=null,firstFrame=null,finiteLimit=null;TrafficIntentKey preparedKey=default;
            yield return Until(()=>
            {
                Assert.That(CafeLoopRuntimeProof.Field<bool>(owner,"stepping"),Is.False);
                Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[id],Is.SameAs(originalVisit));
                Assert.That(MovementRead(originalVisit,"Actor"),Is.SameAs(originalActor));Assert.That(MovementRead(originalVisit,"Admission"),Is.SameAs(originalAdmission));
                Assert.That(MovementRead(originalVisit,"TrafficPhaseSequence"),Is.EqualTo(originalPhase));Assert.That(originalCounterClaim.IsCurrent,Is.True);
                CollectionAssert.AreEqual(originalTokens,customers.Capacity.GetReservations().Where(row=>row.OwnerId==id).Select(row=>row.Token));
                var assigned=(bool)MovementRead(originalVisit,"PickupAssigned");
                var pending=MovementRead(originalVisit,"PendingReservationAnchor");
                if(preparedAnchor==null && pending!=null)
                {
                    var key=(TrafficIntentKey)MovementRead(pending,"Key");
                    if(trafficEntries.Contains(key))
                    {preparedAnchor=pending;preparedKey=key;preparedEntry=trafficEntries[key];firstFrame=MovementRead(preparedEntry,"FirstFrame");finiteLimit=MovementRead(preparedEntry,"FiniteLimit");}
                }
                if(preparedEntry!=null)
                {
                    Assert.That(trafficEntries[preparedKey],Is.SameAs(preparedEntry));Assert.That(MovementRead(preparedEntry,"FirstFrame"),Is.EqualTo(firstFrame));
                    Assert.That(MovementRead(preparedEntry,"FiniteLimit"),Is.EqualTo(finiteLimit));
                    var source=coordinator.GetDecision(preparedKey);Assert.That(source.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None),source.Reason);
                    if(!assigned)Assert.That(pending,Is.SameAs(preparedAnchor));
                }
                return assigned;
            });
            Assert.That(customers.TryReservePickup(id,out var reason),Is.True,reason);
            var visit=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[id];
            var pickup=(PickupQueueService)visit.GetType().GetField("PickupOwner").GetValue(visit);
            var revision=adapter.Revision;
            Assert.That(visit.GetType().GetField("ReservedPickupPath").GetValue(visit),Is.Not.Null);
            var decor=Find<DecorationModeController>();decor.EnterDecorationMode();
            var machine=Find<CafeLayoutRuntime>().FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,machine.InstanceId),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True);Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);
            decor.ExitDecorationMode();yield return null;
            Assert.That(adapter.Revision,Is.GreaterThan(revision));Assert.That(customers.Snapshot,Is.Empty);
            Assert.That(visit.GetType().GetField("ReservedPickupPath").GetValue(visit),Is.Null);
            Assert.That(visit.GetType().GetField("OwnedRoutePath").GetValue(visit),Is.Null);
            Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations").Contains(id),Is.False);
            Assert.That(world.RegisteredActors.Any(a=>a.ActorId==id),Is.False);
            time.SetNormal();tools.ResumeBlockedFlow();
            yield return Until(()=>owner.CanProgress&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True,"Retired layout evidence cannot suppress the next real admission");
            Assert.That(customers.Snapshot.Single().VisitId,Is.Not.EqualTo(id));
        }

        [UnityTest] public IEnumerator ReservedPickupTransferRechecksActualBodyBeforePublishingPaidAssignment()
        {
            // A real reservation is evidence for admission only, never permission through a new body.
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True);var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>customers.CafeSnapshot.Single().CounterArrived);
            Assert.That(customers.TryReservePickup(id,out var reason),Is.True,reason);
            var entries=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");var visit=entries[id];
            var pickup=(PickupQueueService)visit.GetType().GetField("PickupOwner").GetValue(visit);
            var reservations=CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations");var reservation=reservations[id];
            var target=(Vector3)reservation.GetType().GetField("Target").GetValue(reservation);
            var generation=(int)reservation.GetType().GetField("Generation").GetValue(reservation);
            var counter=(QueueAssignment)visit.GetType().GetField("Assignment").GetValue(visit);
            Assert.That((bool)reservation.GetType().GetField("Paid").GetValue(reservation),Is.False);
            var tokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();
            var obstruction=Blocker(target,"reserved-transfer-blocker");
            try
            {
                Assert.That(customers.TryMoveToPickup(id,out _),Is.False,"New actual body must reject transfer before paid FIFO/target publication");
                Assert.That((bool)reservation.GetType().GetField("Paid").GetValue(reservation),Is.False);
                Assert.That((int)reservation.GetType().GetField("Generation").GetValue(reservation),Is.EqualTo(generation));
                Assert.That((QueueAssignment)visit.GetType().GetField("Assignment").GetValue(visit),Is.EqualTo(counter));
                Assert.That((long)visit.GetType().GetField("RequestId").GetValue(visit),Is.Zero);
                Assert.That((bool)visit.GetType().GetField("PickupInTransit").GetValue(visit),Is.False);
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(r=>r.Token));
            }
            finally
            { world.Unregister(obstruction);obstruction.gameObject.SetActive(false);UnityEngine.Object.Destroy(obstruction.gameObject); }
        }

        private IEnumerator ObservePendingServiceLeaseGuard(bool admission)
        {
            // One fixed native fixture; no search/fake request/proof/Ready state.
            // 只stage未发出的waypoints；付款、到位、Ready、lease与contributor均来自原流程。
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            object Read(object value,string name)=>value.GetType().GetField(name,flags).GetValue(value);
            void Write(object value,string name,object data)=>value.GetType().GetField(name,flags).SetValue(value,data);
            object Call(string name,params object[] args)=>typeof(CustomerFlowController).GetMethod(name,flags).Invoke(customers,args);
            var hook=typeof(CustomerFlowController).GetField("PickupObserver",flags);
            var diagnostic=admission&&Environment.GetCommandLineArgs().Contains("-pickupExitFixtureDiagnostic");
            var corridorHook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);var priorCorridor=corridorHook.GetValue(customers);
            var originalExitEvents=new Queue<string>();
            if(diagnostic)corridorHook.SetValue(customers,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)((operation,id,related,target,path,obstacles,result)=>
            {
                if(operation!="plan.exit-direct"&&operation!="plan.exit-boundary-leg"&&operation!="collection.before-acquire")return;
                if(originalExitEvents.Count==24)originalExitEvents.Dequeue();
                originalExitEvents.Enqueue(operation+" actor="+id+" target="+target.ToString("F9")+" path="+(path==null?"NULL":string.Join(";",path.Select(p=>p.ToString("F9"))))+" result="+result);
            }));
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True);var first=customers.Snapshot.Single().VisitId;
            yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==first&&o.State==OrderState.ReadyForPickup)&&
                customers.Snapshot.Any(v=>v.VisitId==first&&v.State==CustomerVisitState.PickupWaiting)&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            var entries=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");var waiter=entries[first];
            var actor=(NavigationActor)Read(waiter,"Actor");var pickup=(PickupQueueService)Read(waiter,"PickupOwner");
            Assert.That(owner.Snapshot.PaidVisitIds.Contains(first),Is.True,"FIXTURE: real canonical paid owner");
            Assert.That(pickup.Snapshot.Single(a=>a.VisitId==first).Arrived,Is.True,"FIXTURE: actual wait arrival");
            var ownerTokens=customers.Capacity.GetReservations().Where(r=>r.OwnerId==first).Select(r=>r.Token).ToArray();
            var captured=false;var clearance=(float)Call("RouteClearance",actor.Settings);
            if(!admission)CafeLoopRuntimeProof.Set(owner,"stepping",true);
            try
            {
                object heldAssignment=null;Vector3 heldLease=default;var heldRevision=adapter.Revision;
                if(admission)
                {
                    // Acquire against real exit eligibility before the later contributor exists.
                    // 返回后、任何yield之前Hold；只准备人工未发腿seam，不是产品自动恢复。
                    yield return PreparePositiveLongService(first);
                    Assert.That(customers.TryMoveToPickupService(first,out var earlyReason),Is.True,"FIXTURE: early real safe acquisition: "+earlyReason);
                    Assert.That(pickup.TryGetServiceLease(first,out heldLease),Is.True);
                    heldAssignment=Read(waiter,"Assignment");Call("HoldVisit",waiter);Write(waiter,"State",CustomerVisitState.MovingToPickupService);
                    Assert.That(Read(waiter,"ServiceArrived"),Is.EqualTo(false));Assert.That(Read(waiter,"RequestId"),Is.EqualTo(0L));
                    Assert.That(Read(waiter,"PendingPickupSegment"),Is.EqualTo(false));Assert.That(Read(waiter,"PendingExitSegment"),Is.EqualTo(false));
                    Assert.That(Read(waiter,"PickupOwner"),Is.SameAs(pickup));Assert.That(Read(waiter,"Assignment"),Is.EqualTo(heldAssignment));
                    Assert.That(pickup.TryGetServiceLease(first,out var retained)&&retained==heldLease,Is.True);
                }
                Assert.That(customers.TrySpawn(),Is.True,"FIXTURE: real second admission");
                var second=customers.Snapshot.Single(v=>v.VisitId!=first).VisitId;var leader=entries[second];
                if(admission)
                {
                    yield return Until(()=>owner.Snapshot.PaidVisitIds.Contains(second)&&
                        (CustomerVisitState)Read(leader,"State")==CustomerVisitState.MovingToPickup&&(bool)Read(leader,"QueueLeft"));
                    CafeLoopRuntimeProof.Set(owner,"stepping",true);
                    Assert.That(CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue").Snapshot.Count,Is.Zero,
                        "FIXTURE: real paid contributor left Counter; actual NextAdmissionPosition is the original head");
                    Assert.That(adapter.Revision,Is.EqualTo(heldRevision));Assert.That(Read(waiter,"State"),Is.EqualTo(CustomerVisitState.MovingToPickupService));
                    Assert.That(Read(waiter,"ServiceArrived"),Is.EqualTo(false));Assert.That(Read(waiter,"RequestId"),Is.EqualTo(0L));
                    Assert.That(Read(waiter,"PendingPickupSegment"),Is.EqualTo(false));Assert.That(Read(waiter,"PendingExitSegment"),Is.EqualTo(false));
                    Assert.That(Read(waiter,"PickupOwner"),Is.SameAs(pickup));Assert.That(Read(waiter,"Assignment"),Is.EqualTo(heldAssignment));
                    Assert.That(pickup.TryGetServiceLease(first,out var retained)&&retained==heldLease,Is.True);
                    CollectionAssert.AreEqual(ownerTokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==first).Select(r=>r.Token));
                }
                var moving=(NavigationActor)Read(leader,"Actor");var request=(long)Read(leader,"RequestId");
                Assert.That(request,Is.GreaterThan(0));Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byId").Contains(request),Is.True);
                yield return null; // Native request initialization; canonical service remains frozen.
                void StageOwnedRoute(PickupQueueService actualOwner)
                {
                    Assert.That(actualOwner,Is.SameAs(pickup));Assert.That(pickup.TryGetServiceLease(first,out var lease),Is.True);
                    Assert.That(Read(waiter,"State"),Is.EqualTo(CustomerVisitState.MovingToPickupService));
                    Assert.That(Read(waiter,"RequestId"),Is.EqualTo(0L));
                    if(!admission){Call("HoldVisit",waiter);Write(waiter,"State",CustomerVisitState.MovingToPickupService);}
                    var route=new Queue<Vector3>(new[]{new Vector3(-3.5f,0,-3.5f),lease});Write(waiter,"Route",route);
                    var full=(IReadOnlyList<Vector3>)Call("ExpandRoute",actor.transform.position,route);
                    Assert.That(full,Is.Not.Null,"FIXTURE: fixed two-leg native service route");
                    var obstacles=(Vector3[])Call("CorridorObstacles",waiter);
                    var fullClear=CustomerRoutePlanner.IsClear(full,obstacles,clearance);
                    if(diagnostic)
                    {
                        // Stored values and original query output only; no additional native/planner calls.
                        string P(Vector3 p)=>p.ToString("F9");
                        var lines=new List<string>{"FIXTURE_EXISTING_QUERY_SNAPSHOT fullClear="+fullClear+" clearance="+clearance+
                            " originalContributorRequest="+request+" currentContributorRequest="+Read(leader,"RequestId")+
                            " lease="+P(lease)+" full="+string.Join(";",full.Select(P))+" obstacles="+string.Join(";",obstacles.Select(P))};
                        lines.AddRange(originalExitEvents);
                        foreach(var body in world.RegisteredActors.Where(a=>a!=null))lines.Add("BODY id="+body.ActorId+" pose="+P(body.transform.position));
                        foreach(var entry in entries.Values.Cast<object>())lines.Add("VISIT id="+Read(entry,"Id")+" state="+Read(entry,"State")+
                            " request="+Read(entry,"RequestId")+" assignment="+P(((QueueAssignment)Read(entry,"Assignment")).Position)+
                            " currentSegment="+P(((NavigationTarget)Read(entry,"CurrentSegmentTarget")).Position)+" final="+P(((NavigationTarget)Read(entry,"FinalTarget")).Position)+
                            " storedRemaining="+string.Join(";",((Queue<Vector3>)Read(entry,"Route")??new Queue<Vector3>()).Select(P)));
                        foreach(var q in entries.Values.Cast<object>().Select(v=>Read(v,"PickupOwner") as PickupQueueService).Append(pickup).Where(q=>q!=null).Distinct())
                            foreach(var pair in q.ServiceLeases)lines.Add("LEASE id="+pair.Key+" position="+P(pair.Value));
                        TestContext.WriteLine(string.Join("\n",lines));
                    }
                    Assert.That(fullClear,Is.True,
                        "FIXTURE: fixed unissued route must be originally full-clear; no fixture geometry sweep");
                    Call("RememberOwnedRoute",waiter,CustomerVisitState.MovingToPickupService);captured=true;
                }
                if(admission)
                {
                    Assert.That((long)Read(leader,"RequestId"),Is.EqualTo(request));
                    StageOwnedRoute((PickupQueueService)Read(waiter,"PickupOwner"));
                    Call("SendRouteSegment",waiter,CustomerVisitState.MovingToPickupService);
                    TestContext.WriteLine("PENDING_GUARD_ACQUISITION earlyRealLease=True sameAssignment=True sameRevision=True sameContributor=True");
                }
                else
                {
                    hook.SetValue(customers,(Action<string,string,PickupQueueService,QueueAssignment>)((operation,id,actualOwner,assignment)=>
                    {if(operation=="collection.route-selected"&&id==first)StageOwnedRoute(actualOwner);}));
                    tools.ResumeCollection();
                    Assert.That(customers.TryMoveToPickupService(first,out var reason),Is.True,"FIXTURE: real acquisition: "+reason);
                }
                hook.SetValue(customers,null);Assert.That(captured,Is.True);
                IReadOnlyList<Vector3> owned=null,finite=null;
                for(var frame=0;frame<300;frame++)
                {
                    owned=(IReadOnlyList<Vector3>)Call("RemainingOwnedRoute",waiter);
                    finite=(IReadOnlyList<Vector3>)Call("CurrentFiniteWaitRoute",waiter,owned);
                    if(finite!=null)break;
                    if((long)Read(leader,"RequestId")!=request)break;
                    yield return null;
                }
                TestContext.WriteLine("PENDING_GUARD_FIXED mode="+(admission?"admission":"lease")+" request="+request+" liveRequest="+Read(leader,"RequestId")+
                    " phase="+Read(leader,"State")+" owned="+(owned==null?"NULL":string.Join(";",owned))+" finite="+(finite==null?"NULL":string.Join(";",finite)));
                Assert.That(finite,Is.Not.Null,"FIXTURE: same accepted contributor must physically obstruct the fixed route with original strict proof");
                Assert.That((bool)Call("HasOwnedRoute",waiter),Is.True);Assert.That(Read(waiter,"RequestId"),Is.EqualTo(0L));
                Assert.That(Read(waiter,"PendingPickupSegment"),Is.EqualTo(true));
                Assert.That(pickup.TryGetServiceLease(first,out var ownedLease),Is.True);
                var staff=world.RegisteredActors.Single(a=>employees.Snapshot.Any(e=>e.ActorId==a.ActorId&&e.Role==EmployeeRole.Barista));
                var conflict=typeof(CustomerFlowController).GetMethod("RoutesConflict",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                var trafficClearance=clearance+2*staff.Settings.MaxSpeed*Mathf.Min(Time.deltaTime,staff.Settings.MaxSubstepSeconds*staff.Settings.MaxSubstepsPerFrame);
                bool Cross(IReadOnlyList<Vector3> a,IReadOnlyList<Vector3> b)=>(bool)conflict.Invoke(null,new object[]{a,b,trafficClearance});
                if(admission)
                {
                    var queue=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue");
                    Assert.That(queue.NextAdmissionPosition.HasValue,Is.True);var tail=queue.NextAdmissionPosition.Value;
                    var settings=CafeLoopRuntimeProof.Field<NavigationActor[]>(customers,"prefabs")[(int)(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit")%CafeLoopRuntimeProof.Field<NavigationActor[]>(customers,"prefabs").Length)].Settings;
                    var spawn=CafeLoopRuntimeProof.Field<Transform>(customers,"spawn").position;
                    var route=(IReadOnlyList<Vector3>)Call("BuildRoute",spawn,tail,null,settings,null,true);
                    Assert.That(route,Is.Not.Null,"FIXTURE: original prospective admission plan");
                    var incoming=(IReadOnlyList<Vector3>)Call("ExpandRoute",spawn,route);
                    TestContext.WriteLine("PENDING_ADMISSION_PREMISES tail="+tail.ToString("F6")+" spawn="+spawn.ToString("F6")+" incoming="+(incoming==null?"NULL":string.Join(";",incoming))+
                        " owned="+string.Join(";",owned)+" finite="+string.Join(";",finite)+" tailOwnedClear="+CustomerRoutePlanner.IsClear(owned,new[]{tail},clearance)+
                        " tailFiniteClear="+CustomerRoutePlanner.IsClear(finite,new[]{tail},clearance)+" incomingOwnedConflict="+Cross(incoming,owned));
                    Assert.That(CustomerRoutePlanner.IsClear(owned,new[]{tail},clearance)&&CustomerRoutePlanner.IsClear(finite,new[]{tail},clearance),Is.True,"FIXTURE: actual tail itself is safe");
                    Assert.That(Cross(incoming,owned),Is.True,"FIXTURE: full incoming route crosses pending owner");
                    var activeRemaining=(IReadOnlyList<Vector3>)Call("ExpandRoute",moving.transform.position,
                        new[]{moving.Agent.destination}.Concat((Queue<Vector3>)Read(leader,"Route")??Enumerable.Empty<Vector3>()));
                    var admissionClearance=(float)Call("RouteClearance",settings)+2*settings.MaxSpeed*Mathf.Min(Time.deltaTime,settings.MaxSubstepSeconds*settings.MaxSubstepsPerFrame);
                    object Static(string name,params object[] args)=>typeof(CustomerFlowController).GetMethod(name,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,args);
                    var targetClear=CustomerRoutePlanner.IsClear(activeRemaining,new[]{tail},admissionClearance);
                    var follows=(CustomerVisitState)Read(leader,"State")==CustomerVisitState.Entering&&
                        (bool)Static("CanFollowEntryBend",incoming,activeRemaining,admissionClearance,settings.ArrivalDistance+settings.Epsilon)&&
                        CustomerRoutePlanner.IsClear(new[]{activeRemaining[activeRemaining.Count-1]},new[]{tail},admissionClearance)&&
                        (targetClear||(bool)Static("FollowerReachesTailAfterLeader",incoming,activeRemaining,tail,admissionClearance,settings.MaxSpeed,moving.Settings.MaxSpeed));
                    TestContext.WriteLine("PENDING_ADMISSION_ACTIVE targetClear="+targetClear+" follows="+follows+" incomingConflict="+Static("RoutesConflict",incoming,activeRemaining,admissionClearance)+
                        " activeRemaining="+string.Join(";",activeRemaining));
                    Assert.That((targetClear||follows||(bool)Static("EntryVacatesTail",leader,activeRemaining,tail,admissionClearance))&&
                        (!(bool)Static("RoutesConflict",incoming,activeRemaining,admissionClearance)||follows),Is.True,
                        "FIXTURE: original active-contributor guard already permits this incoming route; isolate pending-owner hole");
                    var count=customers.Snapshot.Count;var actors=world.RegisteredActors.ToArray();var next=CafeLoopRuntimeProof.Field<long>(customers,"nextVisit");
                    var tokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();var assignments=queue.Snapshot.ToArray();
                    var canStart=(bool)Call("CanStartAdmission",tail,settings);var admitted=customers.TrySpawn();
                    TestContext.WriteLine("PENDING_ADMISSION originalTail="+tail+" incoming="+string.Join(";",incoming)+" owned="+string.Join(";",owned)+
                        " contributor="+request+" CanStart="+canStart+" TrySpawn="+admitted+" visits="+count+"->"+customers.Snapshot.Count+" nextId="+next+"->"+CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"));
                    Assert.That(canStart,Is.False,"Full incoming route must protect valid pending ownership before publication");
                    Assert.That(admitted,Is.False);Assert.That(customers.Snapshot.Count,Is.EqualTo(count));
                    Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.EqualTo(next));
                    CollectionAssert.AreEqual(actors,world.RegisteredActors);CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(r=>r.Token));
                    CollectionAssert.AreEqual(assignments,queue.Snapshot);
                    yield break;
                }
                var serviceProof=Read(waiter,"OwnedWaitProof");
                Assert.That(CafeLoopRuntimeProof.Field<NavigationTarget[]>(customers,"pickupSides").Any(side=>side.Position==ownedLease),Is.True,
                    "FIXTURE: actual collection lease is an original PrepareDeliveryYield side candidate");
                var candidate=adapter.GetCompletePath(staff.transform.position,ownedLease);
                Assert.That(candidate,Is.Not.Null,"FIXTURE: actual staff-origin candidate to owner lease");
                Assert.That(!Cross(candidate,finite)&&CustomerRoutePlanner.IsClear(candidate,new[]{moving.transform.position},trafficClearance),Is.True,
                    "FIXTURE: existing active contributor branch must not mask the pending-owner classification");
                var occupied=(Vector3[])Call("RouteObstacles",staff.ActorId,null,false);
                var others=entries.Values.Cast<object>().Where(v=>(string)Read(v,"Id")!=first).ToArray();
                // Provenance check BEFORE coordinate projection: no actual body/foreign source may be removed.
                // 先按身份分开body/target/lease，再证明唯一被排除来源是当前owner的未占用reservation。
                var bodies=world.RegisteredActors.Where(a=>a!=staff).Select(a=>a.transform.position).ToArray();
                var foreignTargets=others.Select(v=>((QueueAssignment)Read(v,"Assignment")).Position).ToArray();
                var foreignLeases=pickup.ServiceLeases.Where(l=>l.Key!=first&&l.Key!=staff.ActorId).Select(l=>l.Value).ToArray();
                Assert.That(bodies.Concat(foreignTargets).Concat(foreignLeases).All(p=>p!=ownedLease),Is.True,"FIXTURE: no other source shares owner reservation coordinate");
                Assert.That(CustomerRoutePlanner.IsClear(candidate,bodies.Concat(foreignTargets).Concat(foreignLeases).ToArray(),clearance),Is.True,"FIXTURE: all actual bodies and foreign sources remain clear");
                Assert.That(CustomerRoutePlanner.IsClear(candidate,occupied,clearance),Is.False);
                Assert.That(CustomerRoutePlanner.IsClear(candidate,occupied.Where(p=>p!=ownedLease).ToArray(),clearance),Is.True,"FIXTURE: only identity-qualified owner reservation blocks candidate");
                var beforeActors=world.RegisteredActors.ToArray();var beforeTokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();
                Assert.That((bool)Call("PickupDeliveryPathClear",staff,candidate,occupied,ownedLease),Is.False,"Admission still rejects the occupied reservation");
                var traffic=(bool)Call("HasDeliveryTraffic",staff,new[]{candidate});
                CollectionAssert.AreEqual(beforeActors,world.RegisteredActors);CollectionAssert.AreEqual(beforeTokens,customers.Capacity.GetReservations().Select(r=>r.Token));
                var waitObstacles=((IEnumerable<Vector3>)Call("RouteObstaclePositions",staff.ActorId,null,false,waiter)).ToArray();
                foreach(var point in bodies.Concat(foreignTargets).Concat(foreignLeases))
                    CollectionAssert.Contains(waitObstacles,point,"WAIT projection preserves every real body and foreign source, including owner body");
                try
                {
                    Write(waiter,"OwnedWaitProof",null);
                    Assert.That((bool)Call("HasDeliveryTraffic",staff,new[]{candidate}),Is.False,"Missing original proof cannot keep the reservation WAIT alive");
                }
                finally{Write(waiter,"OwnedWaitProof",serviceProof);}
                var phase=Read(waiter,"State");
                try
                {
                    Write(waiter,"State",CustomerVisitState.PickupService);
                    Assert.That((bool)Call("HasDeliveryTraffic",staff,new[]{candidate}),Is.False,"Changed phase cannot reuse pending service classification");
                }
                finally{Write(waiter,"State",phase);}
                var staticBody=Blocker(new Vector3(1.48f,0,3.05f),"service-wait-independent-static-body");
                try
                {
                    Assert.That(Call("CurrentFiniteWaitRoute",waiter,owned),Is.Not.Null,"Independent candidate-leg body leaves the owner's strict proof valid");
                    Assert.That((bool)Call("HasDeliveryTraffic",staff,new[]{candidate}),Is.False,"Another actual body must reject WAIT even with a valid owner proof");
                }
                finally{world.Unregister(staticBody);staticBody.gameObject.SetActive(false);UnityEngine.Object.Destroy(staticBody.gameObject);}
                var leaseBody=Blocker(ownedLease,"service-wait-foreign-body-at-owner-lease");
                try
                {
                    CollectionAssert.Contains(((IEnumerable<Vector3>)Call("RouteObstaclePositions",staff.ActorId,null,false,waiter)).ToArray(),ownedLease,
                        "Foreign BODY at the owner's lease coordinate must survive identity filtering");
                    Assert.That((bool)Call("HasDeliveryTraffic",staff,new[]{candidate}),Is.False,"Co-located foreign body is never an unoccupied reservation");
                }
                finally{world.Unregister(leaseBody);leaseBody.gameObject.SetActive(false);UnityEngine.Object.Destroy(leaseBody.gameObject);}
                TestContext.WriteLine("PENDING_SERVICE_LEASE physical=False strict=True HasTraffic="+traffic+" lease="+ownedLease+" owner="+first+" contributor="+request+
                    " originalSide=True originalPrepareNativeCandidate=True candidate="+string.Join(";",candidate)+" owned="+string.Join(";",owned)+" finite="+string.Join(";",finite));
                CafeLoopRuntimeProof.Set(owner,"stepping",false);
                yield return Until(()=>!pickup.TryGetServiceLease(first,out _)&&owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==first).State==OrderState.Completed);
                Assert.That(ReferenceEquals(Read(waiter,"OwnedWaitProof"),serviceProof),Is.False,"Original service proof retires; a new Exiting route may be owned");
                Assert.That((bool)Call("HasOwnedRoute",waiter)&&(CustomerVisitState)Read(waiter,"OwnedRoutePhase")==CustomerVisitState.MovingToPickupService,Is.False,
                    "Retired service binding cannot qualify the old pending WAIT");
                CollectionAssert.AreEqual(ownerTokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==first).Select(r=>r.Token));
                TestContext.WriteLine("PENDING_SERVICE_LEASE actualCompleted=True actualLeaseRetired=True originalTokens=True; future classification must re-evaluate changed owner/proof");
                Assert.That(traffic,Is.True,"Live finite pending proof must classify the sole unoccupied owner reservation as re-evaluated WAIT, without admitting the candidate");
            }
            finally{hook.SetValue(customers,null);if(diagnostic)corridorHook.SetValue(customers,priorCorridor);CafeLoopRuntimeProof.Set(owner,"stepping",false);}
        }

        // Setup only: prime the real public collection proposal, then let the normal driver prepare it.
        // 只准备同一原 source；保留调用处真正 acquire 的原 call/assert，不在 freeze 下等待或手动 pump。
        private IEnumerator PreparePositiveLongService(string id)
        {
            const string premise="SERVICE_PREPARATION_NOT_PROVEN: ";
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
            object Read(object value,string name)=>value.GetType().GetField(name,flags).GetValue(value);
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var visits=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");var visit=visits[id];
            var actor=Read(visit,"Actor");var admission=Read(visit,"Admission");var pickup=(PickupQueueService)Read(visit,"PickupOwner");
            var assignment=Read(visit,"Assignment");var generation=Read(visit,"Generation");var phase=Read(visit,"TrafficPhaseSequence");
            var pending=Read(visit,"PendingTraffic");var tokens=customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray();
            void SameCaller()
            {
                Assert.That(owner.isActiveAndEnabled && customers.isActiveAndEnabled && world.isActiveAndEnabled && world.LayoutAvailable && owner.CanProgress,Is.True,premise+"live normal driver required");
                Assert.That(owner.HoldCollection,Is.True,premise+"the original manual acquisition boundary must stay held");
                Assert.That(CafeLoopRuntimeProof.Field<bool>(owner,"stepping"),Is.False,premise+"frozen setup is not eligible; never pump or unfreeze it here");
                Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic"),Is.SameAs(coordinator));
                Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic"),Is.SameAs(coordinator));
                Assert.That(visits[id],Is.SameAs(visit));Assert.That(Read(visit,"Actor"),Is.SameAs(actor));Assert.That(Read(visit,"Admission"),Is.SameAs(admission));
                Assert.That(Read(visit,"PickupOwner"),Is.SameAs(pickup));Assert.That(Read(visit,"Assignment"),Is.EqualTo(assignment));
                Assert.That(Read(visit,"Generation"),Is.EqualTo(generation));Assert.That(Read(visit,"TrafficPhaseSequence"),Is.EqualTo(phase));
                Assert.That(Read(visit,"PendingTraffic"),Is.SameAs(pending));Assert.That(Read(visit,"State"),Is.EqualTo(CustomerVisitState.PickupWaiting));
                Assert.That(Read(visit,"RequestId"),Is.EqualTo(0L));Assert.That(pickup.TryGetServiceLease(id,out _),Is.False);
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
            }
            SameCaller();Assert.That(coordinator,Is.Not.Null);
            Assert.That(customers.TryMoveToPickupService(id,out var reason),Is.False,premise+"one original prime must leave acquisition to the unchanged caller assertion: "+reason);
            var candidates=CafeLoopRuntimeProof.Field<IDictionary>(customers,"trafficCandidates");
            var keys=candidates.Keys.Cast<TrafficIntentKey>().Where(k=>k.ActorId==id && k.IntentId=="pickup-service").ToArray();
            Assert.That(keys.Length,Is.EqualTo(1),premise+"prime must retain one actual collection cursor");
            var key=keys[0];var cursor=candidates[key];var seal=Read(cursor,"PickupPreparation")??Read(cursor,"CollectionRelease");
            var entries=CafeLoopRuntimeProof.Field<IDictionary>(coordinator,"entries");
            Assert.That(entries.Contains(key),Is.True,premise+"original submitted Entry missing");var entry=entries[key];
            var first=Read(entry,"FirstFrame");var finite=Read(entry,"FiniteLimit");var claim=seal==null?null:Read(seal,"Claim");
            Assert.That(seal,Is.Not.Null,premise+"original producer prerequisite missing");Assert.That(claim,Is.Not.Null);
            yield return Until(()=>
            {
                SameCaller();Assert.That(candidates[key],Is.SameAs(cursor));Assert.That(entries[key],Is.SameAs(entry));
                Assert.That(Read(cursor,"PickupPreparation")??Read(cursor,"CollectionRelease"),Is.SameAs(seal));Assert.That(Read(seal,"Claim"),Is.SameAs(claim));
                Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(first));Assert.That(Read(entry,"FiniteLimit"),Is.EqualTo(finite));
                Assert.That(Read(entry,"Retired"),Is.EqualTo(false));Assert.That(Read(entry,"AcquiredSequence"),Is.EqualTo(0L));
                Assert.That(Read(entry,"Leg"),Is.Null);var request=(TrafficRequest)Read(entry,"Request");
                Assert.That(request.Intent,Is.EqualTo(key));Assert.That(request.RequestedFrame,Is.EqualTo(first));
                var decision=coordinator.GetDecision(key);Assert.That(decision.Intent,Is.EqualTo(key));
                Assert.That(decision.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None),premise+decision.Kind+": "+decision.Reason);
                return decision.Kind==TrafficDecisionKind.Granted;
            }); // Reuse this fixture's original 6000-frame window and Blocked checks; no new timeout.
            TestContext.WriteLine("LONG_SERVICE_PREPARED id="+id+" first="+first+" finite="+finite+" sameCursor=True sameEntry=True sameClaim=True sameTokens=True canonicalAcquisition=False");
        }

        private IEnumerator ObserveFix1ReplannedPrecedence(bool exiting,bool observePending=false,int retireContributor=0,bool deliveryProtection=false)
        {
            // Controlled unissued-route seam. Orders, tokens, leader request and all motion
            // are real; only the waiter's initial owned waypoint queue is staged.
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            object Read(object v, string name) => v.GetType().GetField(name).GetValue(v);
            void Write(object v, string name, object value) => v.GetType().GetField(name).SetValue(v, value);
            object Call(string name, params object[] args) => typeof(CustomerFlowController).GetMethod(name, flags).Invoke(customers, args);
            var tracking = true; NavigationActor obstruction = null;
            var exits = new HashSet<string>();
            var tokens = new Dictionary<string, AnimalCafe.Capacity.CapacityToken[]>();
            void Track(string id)
            {
                tokens.Add(id, customers.Capacity.GetReservations().Where(r => r.OwnerId == id).Select(r => r.Token).ToArray());
                var actor = world.RegisteredActors.Single(a => a.ActorId == id);
                var observer = actor.gameObject.AddComponent<CafeActualExitObserver>(); observer.VisitId = id;
                observer.Receipt = (visit, pose) =>
                {
                    if (!tracking) return;
                    Assert.That(Vector3.Distance(pose, CafeLoopRuntimeProof.Field<Transform>(customers, "exit").position), Is.LessThanOrEqualTo(.081f));
                    Assert.That(exits.Add(visit), Is.True); Assert.That(owner.Snapshot.Orders.Values.Single(o => o.CustomerId == visit).State, Is.EqualTo(OrderState.Completed));
                };
            }
            try
            {
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                Assert.That(customers.TrySpawn(), Is.True); var first = customers.Snapshot.Single().VisitId; Track(first);
                var entries = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers, "visits");
                string second;
                var pickupHeld = false;
                if (exiting)
                {
                    yield return Until(() => owner.Snapshot.Delivery.Count == 1 && employees.Snapshot.All(e => e.State == EmployeeState.Idle));
                    yield return Until(() => owner.Snapshot.PaidVisitIds.Contains(first) &&
                        owner.Snapshot.Orders.Values.Any(order => order.CustomerId == first && order.State == OrderState.ReadyForPickup) &&
                        customers.CafeSnapshot.Single(v => v.VisitId == first).PickupArrived);
                    Assert.That(owner.HoldCollection, Is.True);
                    // Acquire and reach service before C2 starts its real pickup request.
                    // 只调整真实准备顺序；不写Ready/ServiceArrived/request/pose或放宽任何guard。
                    yield return PreparePositiveLongService(first);
                    var acquired = customers.TryMoveToPickupService(first, out var acquireReason);
                    TestContext.WriteLine("FIX1_EXIT_PREP acquired=" + acquired + " reason=" + acquireReason +
                        " state=" + Read(entries[first], "State") + " request=" + Read(entries[first], "RequestId") +
                        " serviceArrived=" + Read(entries[first], "ServiceArrived") + " holdCollection=" + owner.HoldCollection);
                    Assert.That(acquired, Is.True, "FIXTURE: C1 must pass the real collection gate before C2 admission: " + acquireReason);
                    yield return Until(() => (bool)Read(entries[first], "ServiceArrived"));
                    Assert.That(owner.HoldCollection, Is.True);
                    Assert.That((bool)Read(entries[first], "HasCollected"), Is.False);
                    Assert.That(owner.Snapshot.Orders.Values.Single(order => order.CustomerId == first).State, Is.EqualTo(OrderState.ReadyForPickup));
                    yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                    Assert.That(customers.TrySpawn(), Is.True); second = customers.Snapshot.Single(v => v.VisitId != first).VisitId; Track(second);
                    yield return Until(() => owner.Snapshot.PaidVisitIds.Contains(second) && customers.Snapshot.Any(v => v.VisitId == second && v.State == CustomerVisitState.MovingToPickup));
                    tools.ResumeCollection();
                    yield return Until(() => customers.Snapshot.Any(v => v.VisitId == first && v.State == CustomerVisitState.Exiting));
                }
                else
                {
                    yield return Until(() => Read(entries[first], "State").Equals(CustomerVisitState.MovingToPickup) && (bool)Read(entries[first], "QueueLeft"));
                    CafeLoopRuntimeProof.Set(owner, "stepping", true);
                    Call("HoldVisit", entries[first]); Write(entries[first], "State", CustomerVisitState.MovingToPickup);
                    Write(entries[first], "PendingPickupSegment", true); pickupHeld = true;
                    Assert.That(customers.TrySpawn(), Is.True, "Public admission must accept a real Entry while the controlled unissued pickup leg stays at its actual pose");
                    second = customers.Snapshot.Single(v => v.VisitId != first).VisitId; Track(second); tools.ResumeCollection();
                }
                CafeLoopRuntimeProof.Set(owner, "stepping", true);
                var waiter = entries[first]; var leader = entries[second];
                var phase = (CustomerVisitState)Read(waiter, "State");
                var actor = (NavigationActor)Read(waiter, "Actor"); var moving = (NavigationActor)Read(leader, "Actor");
                var request = (long)Read(leader, "RequestId");
                var accepted = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world.Service, "byId");
                Assert.That(request, Is.GreaterThan(0)); Assert.That(accepted.Contains(request), Is.True);
                if (!pickupHeld) Call("HoldVisit", waiter);
                Write(waiter, "State", phase);
                Write(waiter, exiting ? "PendingExitSegment" : "PendingPickupSegment", true);
                var goal = ((NavigationTarget)Read(waiter, "FinalTarget")).Position;
                var occupied = (Vector3[])Call("RouteObstacles", actor.ActorId, null, false);
                var clearance = (float)Call("RouteClearance", actor.Settings);
                var leading = (IReadOnlyList<Vector3>)Call("ExpandRoute", moving.transform.position,
                    new[] { ((NavigationTarget)Read(leader, "CurrentSegmentTarget")).Position }.Concat((Queue<Vector3>)Read(leader, "Route") ?? Enumerable.Empty<Vector3>()));
                Assert.That(CustomerRoutePlanner.IsClear(leading, (Vector3[])Call("IndependentTrafficObstacles", leader, waiter), clearance), Is.True);
                var root = CafeLoopRuntimeProof.Field<Transform>(customers, "gridRoot"); var runtime = Find<CafeLayoutRuntime>();
                var cell = runtime.Layout.GridSettings.CellSize;
                var points = runtime.RoomSurfaceLayout.FloorTiles.Keys.Select(p => root.TransformPoint(new Vector3((p.X + .5f) * cell, 0, (p.Y + .5f) * cell)))
                    .Select(p => new Vector3(p.x, actor.transform.position.y, p.z)).Where(adapter.IsPointWalkable).Take(64).ToArray();
                IReadOnlyList<Vector3> Plan(Vector3[] obstacles)
                {
                    if (exiting) return (IReadOnlyList<Vector3>)Call("PlanPickupExit", actor.transform.position, goal, obstacles, actor.Settings);
                    var plan = new CustomerRoutePlanner().Build(actor.transform.position, goal, obstacles, clearance, adapter.GetCompletePath);
                    if (plan != null || !CafeLoopRuntimeProof.Field<bool>(customers, "multiSidePickup")) return plan;
                    foreach (var side in CafeLoopRuntimeProof.Field<NavigationTarget[]>(customers, "pickupSides"))
                    {
                        if (Vector3.Distance(actor.transform.position, side.Position) <= actor.Settings.ArrivalDistance + actor.Settings.Epsilon || side.Position == goal) continue;
                        if (CustomerRoutePlanner.IsClear(adapter.GetCompletePath(actor.transform.position, side.Position), obstacles, clearance) &&
                            CustomerRoutePlanner.IsClear(adapter.GetCompletePath(side.Position, goal), obstacles, clearance)) return new[] { side.Position, goal };
                    }
                    return null;
                }
                Queue<Vector3> initial = null; IReadOnlyList<Vector3> expected = null; Vector3 blockPoint = default;
                // One bounded fixture query, not a per-frame or product floor search.
                if (exiting) foreach (var blocked in points.Where(p => world.RegisteredActors.All(a => Vector3.Distance(a.transform.position, p) > .94f) &&
                    CustomerRoutePlanner.IsClear(leading, new[] { p }, clearance)))
                {
                    var candidate = Plan(occupied.Concat(new[] { blocked }).ToArray());
                    if (candidate == null || candidate.Count > 24) continue;
                    Write(waiter, "Route", new Queue<Vector3>(candidate));
                    if (!(bool)Call("CanWaitForConflictingCustomerRequest", waiter)) continue;
                    foreach (var via in points)
                    {
                        var original = new Queue<Vector3>(new[] { blocked, via, goal });
                        var full = (IReadOnlyList<Vector3>)Call("ExpandRoute", actor.transform.position, original);
                        if (!CustomerRoutePlanner.IsClear(full, occupied, clearance)) continue;
                        Write(waiter, "Route", original);
                        if ((bool)Call("CanWaitForConflictingCustomerRequest", waiter)) continue;
                        initial = original; expected = candidate; blockPoint = blocked; break;
                    }
                    if (initial != null) break;
                }
                else foreach (var via in points)
                {
                    var candidate = new Queue<Vector3>(new[] { via, goal });
                    if (!CustomerRoutePlanner.IsClear((IReadOnlyList<Vector3>)Call("ExpandRoute", actor.transform.position, candidate), occupied, clearance)) continue;
                    Write(waiter, "Route", candidate);
                    if (!(bool)Call("CanWaitForConflictingCustomerRequest", waiter)) continue;
                    initial = candidate; expected = candidate.ToArray(); break;
                }
                if (exiting) TestContext.WriteLine("FIX1_EXIT_CANDIDATE waiterPose=" + actor.transform.position.ToString("F9") +
                    " waiterRequest=" + Read(waiter, "RequestId") + " waiterTarget=" + ((NavigationTarget)Read(waiter, "CurrentSegmentTarget")).Position.ToString("F9") +
                    " leaderPose=" + moving.transform.position.ToString("F9") + " originalLeaderRequest=" + request + " currentLeaderRequest=" + Read(leader, "RequestId") +
                    " leaderTarget=" + ((NavigationTarget)Read(leader, "CurrentSegmentTarget")).Position.ToString("F9") +
                    " originalExpandedLeader=" + (leading == null ? "NULL" : string.Join(";", leading.Select(p => p.ToString("F9")))) +
                    " leaderUnissuedRoute=" + string.Join(";", ((Queue<Vector3>)Read(leader, "Route") ?? new Queue<Vector3>()).Select(p => p.ToString("F9"))) +
                    " initial=" + (initial == null ? "NULL" : string.Join(";", initial.Select(p => p.ToString("F9")))) +
                    " expected=" + (expected == null ? "NULL" : string.Join(";", expected.Select(p => p.ToString("F9")))) + " blockPoint=" + blockPoint.ToString("F9"));
                Assert.That(initial, Is.Not.Null, exiting ? "Fixture must expose nonconflicting original -> blocked -> conflicting replanned candidate" :
                    "Selected Pickup liveness control must conflict with the real accepted Entry corridor; not a post-replan branch claim");
                if (exiting) obstruction = Blocker(blockPoint, "fix1-replan-obstruction-" + phase);
                Write(waiter, "Route", initial);
                Assert.That((bool)Call("CanWaitForConflictingCustomerRequest", waiter), Is.EqualTo(!exiting));
                if (exiting)
                {
                    var next = adapter.GetCompletePath(actor.transform.position, initial.Peek());
                    Assert.That(CustomerRoutePlanner.IsClear(next, (Vector3[])Call("RouteObstacles", actor.ActorId, null, false), clearance), Is.False);
                    Assert.That((bool)Call("CanWaitForIndependentPickupExitTraffic", waiter), Is.False, "Static obstruction must force real bounded replan");
                }
                var segments = (int)Read(waiter, "Segments"); var replans = (int)Read(waiter, "Replans");
                var generation = Read(waiter, "Generation"); var pose = actor.transform.position;
                var marker = exiting ? "FIX1_REPLAN" : "FIX1_SELECTED_PICKUP_NOT_POST_REPLAN";
                TestContext.WriteLine(marker + "_BEFORE phase=" + phase + " acceptedLeader=" + request + " original=" + string.Join(";", initial) + " replacement=" + string.Join(";", expected));
                if(observePending)
                {
                    Call("RememberOwnedRoute",waiter,phase);
                    Assert.That(Read(waiter,"OwnedRoutePath"),Is.Not.Null,"Original full-clear acquisition before actual strict WAIT");
                }
                Call("SendRouteSegment", waiter, phase);
                TestContext.WriteLine(marker + "_AFTER request=" + Read(waiter, "RequestId") + " segments=" + Read(waiter, "Segments") + " replans=" + Read(waiter, "Replans"));
                Assert.That(Read(waiter, "RequestId"), Is.EqualTo(0L), "Replacement candidate must pass the same accepted-route precedence gate");
                Assert.That(Read(waiter, "Segments"), Is.EqualTo(segments)); Assert.That(Read(waiter, "Generation"), Is.EqualTo(generation));
                var expectedReplans = replans + (exiting ? 1 : 0);
                Assert.That(Read(waiter, "Replans"), Is.EqualTo(expectedReplans), "Exactly the real bounded replan, no wait retries");
                Assert.That(Read(waiter, exiting ? "PendingExitSegment" : "PendingPickupSegment"), Is.True);
                var retained = (Queue<Vector3>)Read(waiter, "Route"); CollectionAssert.AreEqual(expected, retained);
                Assert.That(actor.transform.position, Is.EqualTo(pose));
                Call("SendRouteSegment", waiter, phase);
                Assert.That(Read(waiter, "Route"), Is.SameAs(retained)); Assert.That(Read(waiter, "Segments"), Is.EqualTo(segments));
                Assert.That(Read(waiter, "Replans"), Is.EqualTo(expectedReplans));
                CollectionAssert.AreEqual(tokens[actor.ActorId], customers.Capacity.GetReservations().Where(r => r.OwnerId == actor.ActorId).Select(r => r.Token));
                Assert.That(customers.Capacity.GetReservations().Single(r => r.OwnerId == actor.ActorId && r.Kind == AnimalCafe.Capacity.CapacityKind.TotalCustomers).State, Is.EqualTo(AnimalCafe.Capacity.ReservationState.Occupied));
                if(observePending)
                {
                    // Acquire through the original full-clear capture; never invent ownership.
                    Assert.That(Read(waiter,"OwnedRoutePath"),Is.Not.Null,"Real selected pending route retains original full-clear capture");
                    var qualified=false;
                    for(var frame=0;frame<300;frame++)
                    {
                        var owned=(IReadOnlyList<Vector3>)Read(waiter,"OwnedRoutePath");
                        if(owned==null || (long)Read(waiter,"RequestId")!=0) break;
                        var blocked=!CustomerRoutePlanner.IsClear(owned,(Vector3[])Call("CorridorObstacles",waiter),clearance);
                        var strict=(bool)Call("CanWaitForConflictingCustomerRequest",waiter);
                        if(blocked)
                        {
                            TestContext.WriteLine("PENDING_STRICT_OBSERVATION blocked=True strict="+strict+" waiter="+actor.ActorId+
                                " leader="+moving.ActorId+" acceptedRequest="+Read(leader,"RequestId")+" body="+moving.transform.position+
                                " selected="+string.Join(";",owned));
                            Assert.That(strict,Is.True,"Existing strict predicate must certify this exact real pending/body fixture before behavior change");
                            qualified=true;
                            var path=(IReadOnlyList<Vector3>)Read(waiter,"OwnedRoutePath");
                            Assert.That(Call("CurrentFiniteWaitRoute",waiter,path),Is.Not.Null,"Captured exact original strict proof remains valid against ALL other obstacles");
                            if(deliveryProtection)
                            {
                                var staff=world.RegisteredActors.Single(a=>employees.Snapshot.Any(e=>e.ActorId==a.ActorId&&e.Role==EmployeeRole.Barista));
                                var obstacles=(Vector3[])Call("RouteObstacles",staff.ActorId,null,false);
                                var finite=(IReadOnlyList<Vector3>)Call("CurrentFiniteWaitRoute",waiter,path);
                                var conflict=typeof(CustomerFlowController).GetMethod("RoutesConflict",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                                var trafficClearance=clearance+2*staff.Settings.MaxSpeed*Mathf.Min(Time.deltaTime,staff.Settings.MaxSubstepSeconds*staff.Settings.MaxSubstepsPerFrame);
                                bool Cross(IReadOnlyList<Vector3> a,IReadOnlyList<Vector3> b)=>(bool)conflict.Invoke(null,new object[]{a,b,trafficClearance});
                                var sampleGoals=points.SelectMany(p=>new[]{p,p+Vector3.right*.3f,p-Vector3.right*.3f,p+Vector3.forward*.3f,p-Vector3.forward*.3f})
                                    .Concat(finite.Skip(1).SelectMany((p,i)=>new[]{Vector3.Lerp(finite[i],p,.25f),Vector3.Lerp(finite[i],p,.5f),Vector3.Lerp(finite[i],p,.75f)}));
                                var queries=sampleGoals.Select(p=>new {Goal=p,Path=adapter.GetCompletePath(staff.transform.position,p)})
                                    .Where(q=>CustomerRoutePlanner.IsClear(q.Path,obstacles,clearance)).ToArray();
                                var waiterQuery=queries.FirstOrDefault(q=>Cross(q.Path,path)&&!Cross(q.Path,finite));
                                var contributorQuery=queries.FirstOrDefault(q=>!Cross(q.Path,path)&&Cross(q.Path,finite));
                                // The narrow upper-left contributor corridor needs a real two-leg
                                // approach; the native shortest line crosses the waiter's triangle.
                                // 有界fixture查询，完整native legs；不伪造成功或移动角色。
                                if(contributorQuery==null)
                                foreach(var via in points)
                                {
                                    var firstLeg=adapter.GetCompletePath(staff.transform.position,via);
                                    if(!CustomerRoutePlanner.IsClear(firstLeg,obstacles,clearance)||Cross(firstLeg,path))continue;
                                    foreach(var goalPoint in sampleGoals)
                                    {
                                        var nextLeg=adapter.GetCompletePath(via,goalPoint);
                                        if(!CustomerRoutePlanner.IsClear(nextLeg,obstacles,clearance)||Cross(nextLeg,path)||!Cross(nextLeg,finite))continue;
                                        contributorQuery=new {Goal=goalPoint,Path=(IReadOnlyList<Vector3>)firstLeg.Concat(nextLeg.Skip(1)).ToArray()};break;
                                    }
                                    if(contributorQuery!=null)break;
                                }
                                var clearQuery=queries.FirstOrDefault(q=>!Cross(q.Path,path)&&!Cross(q.Path,finite));
                                Assert.That(waiterQuery,Is.Not.Null,"Actual staff-origin native path crosses owned pending suffix while clearing bodies");
                                TestContext.WriteLine("OWNED_DELIVERY_QUERY waiter="+string.Join(";",waiterQuery.Path)+" contributor="+
                                    (contributorQuery==null?"NONE":string.Join(";",contributorQuery.Path))+" finite="+string.Join(";",finite));
                                Assert.That((bool)Call("PickupDeliveryPathClear",staff,waiterQuery.Path,obstacles,waiterQuery.Goal),Is.False,
                                    "Later staff candidate must protect actual owned pending customer suffix");
                                Assert.That(contributorQuery,Is.Not.Null,"Separate actual staff query crosses only finite contributor");
                                Assert.That(clearQuery,Is.Not.Null,"Nonconflicting real query exists");
                                foreach(var query in new[]{waiterQuery,contributorQuery})
                                {
                                    Assert.That((bool)Call("PickupDeliveryPathClear",staff,query.Path,obstacles,query.Goal),Is.False,
                                        "Later staff candidate must not seal waiter or its independent finite contributor");
                                    Assert.That((bool)Call("HasDeliveryTraffic",staff,new[]{query.Path}),Is.True,
                                        "Same finite proof must classify rejection as traffic, not static Blocked");
                                }
                                Assert.That((bool)Call("PickupDeliveryPathClear",staff,clearQuery.Path,obstacles,clearQuery.Goal),Is.True);
                                var proof=Read(waiter,"OwnedWaitProof");
                                try
                                {
                                    Write(waiter,"OwnedWaitProof",null);
                                    Assert.That(Call("CurrentFiniteWaitRoute",waiter,path),Is.Null,"pending0 without original finite proof cannot authorize waiting");
                                    Assert.That((bool)Call("PickupDeliveryPathClear",staff,waiterQuery.Path,obstacles,waiterQuery.Goal),Is.True,
                                        "Unproven blocked pending route has no denial authority");
                                    Assert.That((bool)Call("HasDeliveryTraffic",staff,new[]{waiterQuery.Path}),Is.False,
                                        "pending0 without the original finite proof cannot create an indefinite delivery WAIT");
                                }
                                finally{Write(waiter,"OwnedWaitProof",proof);}
                                // Real unrelated body on the incoming route only: owner proof remains valid.
                                // owner能腾位不代表员工候选能穿过另一堵静态墙。
                                var staticBody=Blocker(new Vector3(-.5f,0,1.5f),"owned-delivery-independent-static-body");
                                try
                                {
                                    Assert.That(Call("CurrentFiniteWaitRoute",waiter,path),Is.Not.Null,"Independent static body must not invalidate the original owner/contributor proof");
                                    Assert.That(CustomerRoutePlanner.IsClear(waiterQuery.Path,(Vector3[])Call("RouteObstacles",staff.ActorId,null,false),clearance),Is.False);
                                    Assert.That((bool)Call("HasDeliveryTraffic",staff,new[]{waiterQuery.Path}),Is.False,
                                        "Owned-route conflict cannot classify a separately static-blocked candidate as finite traffic");
                                }
                                finally{world.Unregister(staticBody);staticBody.gameObject.SetActive(false);UnityEngine.Object.Destroy(staticBody.gameObject);}
                            }
                            var candidate=Vector3.Lerp(path[0],path[1],.25f);
                            Assert.That((bool)Call("NewPickupTargetClear",leader,candidate),Is.False,"Real pending authority rejects new target before publication");
                            var contributorOnly=((NavigationTarget)Read(leader,"FinalTarget")).Position;
                            Assert.That(CustomerRoutePlanner.IsClear(path,new[]{contributorOnly},clearance),Is.True,"Contributor-only future target is outside waiter suffix");
                            Assert.That((bool)Call("NewPickupTargetClear",leader,contributorOnly),Is.False,"New target cannot destroy the proof's real vacating contributor route");
                            var priorState=Read(leader,"State");
                            try{Write(leader,"State",CustomerVisitState.Blocked);Assert.That(Call("CurrentFiniteWaitRoute",waiter,path),Is.Null);}
                            finally{Write(leader,"State",priorState);}
                            var priorRequest=Read(leader,"RequestId");
                            try{Write(leader,"RequestId",0L);Assert.That(Call("CurrentFiniteWaitRoute",waiter,path),Is.Null);}
                            finally{Write(leader,"RequestId",priorRequest);}
                            var priorAssignment=(QueueAssignment)Read(leader,"Assignment");
                            try{Write(leader,"Assignment",new QueueAssignment(moving.ActorId,priorAssignment.SlotIndex,priorAssignment.Generation+1,priorAssignment.Position,false));
                                Assert.That(Call("CurrentFiniteWaitRoute",waiter,path),Is.Null);}
                            finally{Write(leader,"Assignment",priorAssignment);}
                            // A separate real parked body must not be discounted by this single-contributor proof.
                            var parked=Blocker(candidate,"pending-other-parked-body");
                            try{Assert.That(Call("CurrentFiniteWaitRoute",waiter,path),Is.Null);}
                            finally{world.Unregister(parked);parked.gameObject.SetActive(false);UnityEngine.Object.Destroy(parked.gameObject);}
                            var queue=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue");
                            var originalSlots=CafeLoopRuntimeProof.Field<Vector3[]>(queue,"slots");
                            var staged=(Vector3[])originalSlots.Clone();staged[queue.Snapshot.Count]=candidate;
                            var allTokens=customers.Capacity.GetReservations().Select(r=>r.Token).ToArray();
                            var allPaid=owner.Snapshot.PaidVisitIds.ToArray();var next=CafeLoopRuntimeProof.Field<long>(customers,"nextVisit");var count=customers.Snapshot.Count;
                            try
                            {
                                queue.UpdateSlots(staged);
                                Assert.That(customers.TrySpawn(),Is.False,"Reject before real Actor/capacity/queue publication");
                                Assert.That(customers.Snapshot.Count,Is.EqualTo(count));Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.EqualTo(next));
                                CollectionAssert.AreEqual(allTokens,customers.Capacity.GetReservations().Select(r=>r.Token));CollectionAssert.AreEqual(allPaid,owner.Snapshot.PaidVisitIds);
                            }
                            finally{queue.UpdateSlots(originalSlots);}
                            break;
                        }
                        yield return null;
                    }
                    Assert.That(qualified,Is.True,"Bounded observation must expose actual pending/body obstruction; otherwise report missing premise");
                    if(retireContributor!=0)
                    {
                        if(retireContributor==1) Assert.That(world.Service.Cancel((long)Read(leader,"RequestId")),Is.True);
                        else moving.enabled=false;
                        Assert.That(Call("CurrentFiniteWaitRoute",waiter,(IReadOnlyList<Vector3>)Read(waiter,"OwnedRoutePath")),Is.Null,
                            "Real cancelled/disabled contributor cannot keep old denial proof alive");
                        yield break; // Negative control; no invented request restoration or liveness claim.
                    }
                }
                var leaderPose = moving.transform.position;
                if (obstruction != null) { world.Unregister(obstruction); UnityEngine.Object.Destroy(obstruction.gameObject); obstruction = null; }
                CafeLoopRuntimeProof.Set(owner, "stepping", false);
                var displaced = 0f;
                for (var frame = 0; frame < 120 && displaced < .1f && moving != null; frame++)
                { yield return null; if (moving != null) displaced = Vector3.Distance(moving.transform.position, leaderPose); }
                Assert.That(displaced, Is.GreaterThanOrEqualTo(.1f), "Original accepted leader genuinely advances before clearance");
                yield return Until(() => customers.Snapshot.Count == 0 && exits.Count == 2 && employees.Snapshot.All(e => e.State == EmployeeState.Idle));
                Assert.That(owner.Snapshot.Orders.Count, Is.EqualTo(2)); Assert.That(owner.Snapshot.Orders.Values.All(o => o.State == OrderState.Completed), Is.True);
                foreach (var pair in tokens)
                {
                    var records = customers.Capacity.GetReservations().Where(r => r.OwnerId == pair.Key).ToArray();
                    CollectionAssert.AreEqual(pair.Value, records.Select(r => r.Token)); Assert.That(records.All(r => r.State == AnimalCafe.Capacity.ReservationState.Released), Is.True);
                }
            }
            finally
            {
                tracking = false; CafeLoopRuntimeProof.Set(owner, "stepping", false);
                if (obstruction != null) { world.Unregister(obstruction); UnityEngine.Object.Destroy(obstruction.gameObject); }
            }
        }
        [UnityTest] public IEnumerator ControlledIndependentTrafficKeepsBodiesOtherTargetsAndServiceLeases()
        {
            // Projection guard only. All visits/tokens are real; temporary assignment/
            // lease snapshots below are NOT real route requests and never yield a frame.
            for (var i = 0; i < 3; i++)
            {
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                Assert.That(customers.TrySpawn(), Is.True);
                yield return Until(() => customers.Snapshot.Count == i + 1 && customers.CafeSnapshot.All(v => v.PickupArrived) &&
                    employees.Snapshot.All(e => e.State == EmployeeState.Idle));
            }
            var entries = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers, "visits");
            var ordered = customers.PickupAssignments.Select(a => entries[a.VisitId]).ToArray();
            var waiter = ordered[0]; var moving = ordered[1]; var other = ordered[2];
            object Read(object v, string name) => v.GetType().GetField(name).GetValue(v);
            void Write(object v, string name, object value) => v.GetType().GetField(name).SetValue(v, value);
            var originalWaiterState = Read(waiter, "State"); var originalWaiterAssignment = Read(waiter, "Assignment");
            var originalOtherState = Read(other, "State"); var originalOtherAssignment = Read(other, "Assignment");
            var originalMovingState = Read(moving, "State");
            var queue = (PickupQueueService)Read(waiter, "PickupOwner");
            var leaseOwner = CafeLoopRuntimeProof.Field<string>(queue, "collectionOwner");
            var leasePosition = CafeLoopRuntimeProof.Field<Vector3>(queue, "collectionPosition");
            var tokens = customers.Capacity.GetReservations().Select(r => r.Token).ToArray();
            var point = new Vector3(100, 0, 100); // Identity projection only, not a navigation position.
            var waiterBody = (NavigationActor)Read(waiter, "Actor");
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            Vector3[] Obstacles() => (Vector3[])typeof(CustomerFlowController).GetMethod("IndependentTrafficObstacles", flags).Invoke(customers, new[] { moving, waiter });
            try
            {
                CafeLoopRuntimeProof.Set(owner, "stepping", true);
                Assert.That(Read(waiter, "RequestId"), Is.EqualTo(0L));
                var assignment = (QueueAssignment)originalWaiterAssignment;
                Write(waiter, "State", CustomerVisitState.MovingToPickup);
                Write(waiter, "Assignment", new QueueAssignment((string)Read(waiter, "Id"), assignment.SlotIndex, assignment.Generation, point, false));
                CollectionAssert.DoesNotContain(Obstacles(), point, "Only the waiter's own empty future slot may be omitted in WAIT proof");
                Assert.That(Obstacles(), Does.Contain(waiterBody.transform.position), "Waiter's actual body must remain");
                var otherAssignment = (QueueAssignment)originalOtherAssignment;
                Write(other, "State", CustomerVisitState.MovingToPickup);
                Write(other, "Assignment", new QueueAssignment((string)Read(other, "Id"), otherAssignment.SlotIndex, otherAssignment.Generation, point, false));
                Assert.That(Obstacles(), Does.Contain(point), "Another visit's co-located future target must remain by identity");
                // WAIT target ownership is phase-independent; these are projection snapshots,
                // not fabricated Entry/Advance requests or real navigation receipts.
                foreach (var requester in new[] { CustomerVisitState.Exiting, CustomerVisitState.Entering, CustomerVisitState.Advancing })
                foreach (var targetOwner in new[] { CustomerVisitState.Entering, CustomerVisitState.Advancing })
                {
                    Write(moving, "State", requester); Write(other, "State", targetOwner);
                    Assert.That(Obstacles(), Does.Contain(point), "Retain " + targetOwner + " target for " + requester + " independent-request proof");
                }
                Write(other, "State", CustomerVisitState.Blocked);
                CollectionAssert.DoesNotContain(Obstacles(), point, "Retired target ownership must not become an indefinite wait obstacle");
                Write(moving, "State", originalMovingState);
                Write(other, "State", originalOtherState); Write(other, "Assignment", originalOtherAssignment);
                CafeLoopRuntimeProof.Set(queue, "collectionOwner", (string)Read(waiter, "Id"));
                CafeLoopRuntimeProof.Set(queue, "collectionPosition", point);
                Assert.That(Obstacles(), Does.Contain(point), "Even waiter's co-located service lease must remain");
                CafeLoopRuntimeProof.Set(queue, "collectionOwner", leaseOwner); CafeLoopRuntimeProof.Set(queue, "collectionPosition", leasePosition);
                Write(waiter, "Assignment", new QueueAssignment((string)Read(waiter, "Id"), assignment.SlotIndex, assignment.Generation, waiterBody.transform.position, false));
                Assert.That(Obstacles(), Does.Contain(waiterBody.transform.position), "Occupied slot never hides its real body");
                CollectionAssert.AreEqual(tokens, customers.Capacity.GetReservations().Select(r => r.Token));
            }
            finally
            {
                Write(waiter, "State", originalWaiterState); Write(waiter, "Assignment", originalWaiterAssignment);
                Write(other, "State", originalOtherState); Write(other, "Assignment", originalOtherAssignment);
                Write(moving, "State", originalMovingState);
                CafeLoopRuntimeProof.Set(queue, "collectionOwner", leaseOwner); CafeLoopRuntimeProof.Set(queue, "collectionPosition", leasePosition);
                CafeLoopRuntimeProof.Set(owner, "stepping", false);
            }
        }
        private IEnumerator ObserveControlledEntryExitTraffic(int retire)
        {
            // Controlled dispatch seam only: the entry request and its motion are real.
            // 只暂存已领取者的未发路线；不伪造request、订单、付款或capacity token。
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            object Read(object v, string name) => v.GetType().GetField(name).GetValue(v);
            void Write(object v, string name, object value) => v.GetType().GetField(name).SetValue(v, value);
            object Call(string name, params object[] args) => typeof(CustomerFlowController).GetMethod(name, flags).Invoke(customers, args);
            var tracking = true;
            var actualExits = new HashSet<string>();
            try
            {
                yield return MatrixStart();
                var runtime = Find<CafeLayoutRuntime>(); var decor = Find<DecorationModeController>();
                var cash = runtime.Layout.FurnitureInstances.Single(f => f.Position == new GridPosition(1, 5));
                var machine = runtime.Layout.FurnitureInstances.Single(f => f.Position == new GridPosition(4, 6));
                yield return UserMoveCounterSupport(cash.InstanceId, new GridPosition(6, 4));
                yield return UserMoveCounterSupport(machine.InstanceId, new GridPosition(1, 7));
                var equipment = runtime.FunctionalSurfaceLayout.MountedInstances.Single(m => m.DefinitionId == "equipment.coffee-machine.01");
                Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment, equipment.InstanceId), Is.True);
                Assert.That(decor.TryRotateFunctionalSurfacePreview(), Is.True); Assert.That(decor.TryConfirmFunctionalSurfacePreview(), Is.True);
                for (var x = 1; x < 6; x++) yield return MatrixFurniture("furniture.counter.module.01", new GridPosition(x, 4), 0);
                var support = runtime.Layout.FurnitureInstances.Single(f => f.Position == new GridPosition(1, 4));
                Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint, "pickup.point", new SurfaceSlotAddress(support.InstanceId, "slot.0")), Is.True);
                Assert.That(decor.TryConfirmFunctionalSurfacePreview(), Is.True); decor.ExitDecorationMode();
                yield return Until(() => owner.CanProgress && employees.Snapshot.All(e => e.State == EmployeeState.Idle));
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
                Assert.That(customers.TrySpawn(), Is.True);
                var id = customers.Snapshot.Single().VisitId;
                var actor = world.RegisteredActors.Single(a => a.ActorId == id);
                var token = customers.Capacity.GetReservations().Single(r => r.OwnerId == id && r.Kind == AnimalCafe.Capacity.CapacityKind.TotalCustomers).Token;
                var observer = actor.gameObject.AddComponent<CafeActualExitObserver>();
                observer.VisitId = id; observer.Receipt = (visit, pose) =>
                {
                    if (!tracking) return;
                    Assert.That(Vector3.Distance(pose, CafeLoopRuntimeProof.Field<Transform>(customers, "exit").position), Is.LessThanOrEqualTo(.081f));
                    Assert.That(actualExits.Add(visit), Is.True);
                };
                yield return Until(() => owner.Snapshot.Delivery.Count == 1 && employees.Snapshot.All(e => e.State == EmployeeState.Idle));
                tools.ResumeCollection();
                yield return Until(() => customers.Snapshot.Any(v => v.VisitId == id && v.State == CustomerVisitState.Exiting));
                CafeLoopRuntimeProof.Set(owner, "stepping", true);
                var entries = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers, "visits");
                var front = entries[id];
                Call("HoldVisit", front);
                Write(front, "State", CustomerVisitState.Exiting);
                Write(front, "PendingExitSegment", true);
                Write(front, "Route", null);
                Assert.That(customers.TrySpawn(), Is.True, customers.Status);
                var entryId = customers.Snapshot.Single(v => v.VisitId != id).VisitId;
                var entry = entries[entryId];
                var incoming = world.RegisteredActors.Single(a => a.ActorId == entryId);
                var request = (long)Read(entry, "RequestId");
                var accepted = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world.Service, "byId");
                Assert.That(request, Is.GreaterThan(0)); Assert.That(accepted.Contains(request), Is.True);
                Assert.That(Read(entry, "State"), Is.EqualTo(CustomerVisitState.Entering));
                var goal = ((NavigationTarget)Read(front, "FinalTarget")).Position;
                var occupied = (Vector3[])Call("RouteObstacles", id, null, false);
                var without = occupied.Where(p => Vector3.Distance(p, incoming.transform.position) > .001f).ToArray();
                var route = (IReadOnlyList<Vector3>)Call("PlanPickupExit", actor.transform.position, goal, without, actor.Settings);
                Assert.That(route, Is.Not.Null, "Retaining all static bodies, entry-body removal must provide a complete owned exit route");
                var selected = new Queue<Vector3>(route);
                Write(front, "Route", selected);
                var clearance = (float)Call("RouteClearance", actor.Settings);
                var complete = (IReadOnlyList<Vector3>)Call("ExpandRoute", actor.transform.position, selected);
                Assert.That(CustomerRoutePlanner.IsClear(complete, occupied, clearance), Is.False,
                    "Real entry body must obstruct this full owned exit route");
                bool Wait() => (bool)Call("CanWaitForIndependentPickupExitTraffic", front);
                bool PriorityWait() => (bool)Call("CanWaitForConflictingCustomerRequest", front);
                TestContext.WriteLine("CONTROLLED_ENTRY_EXIT realAcceptedRequest=" + request + " pose=" + incoming.transform.position + " selected=" + string.Join(";", selected));
                Assert.That(Wait(), Is.True, "Relevant accepted entry may vacate the shared exit without this collected customer moving");
                Assert.That(PriorityWait(), Is.True, "Protect the real accepted remaining route before this next leg enters it");
                // Guard-only snapshots restore original fields in the same frame.
                var state = Read(entry, "State");
                try { Write(entry, "State", CustomerVisitState.Blocked); Assert.That(Wait(), Is.False); Assert.That(PriorityWait(), Is.False); }
                finally { Write(entry, "State", state); }
                try { Write(entry, "RequestId", 0L); Assert.That(Wait(), Is.False); Assert.That(PriorityWait(), Is.False); }
                finally { Write(entry, "RequestId", request); }
                var assignment = Read(entry, "Assignment");
                try
                {
                    var original = (QueueAssignment)assignment;
                    Write(entry, "Assignment", new QueueAssignment(entryId, original.SlotIndex, original.Generation, goal, false));
                    Assert.That(Wait(), Is.False, "Owned future parking target on the exit is not finite corridor relief");
                    Assert.That(PriorityWait(), Is.False);
                }
                finally { Write(entry, "Assignment", assignment); }
                var safeUnrelated = new Queue<Vector3>(new[] { actor.transform.position });
                try { Write(front, "Route", safeUnrelated); Assert.That(Wait(), Is.False, "Unrelated moving entry cannot delay a clear selected leg"); Assert.That(PriorityWait(), Is.False); }
                finally { Write(front, "Route", selected); }
                var currentTarget = Read(entry, "CurrentSegmentTarget");
                try
                {
                    // Guard-only invalid remaining-route snapshot; this is NOT a new accepted request.
                    Write(entry, "CurrentSegmentTarget", new NavigationTarget(actor.transform.position));
                    Assert.That(PriorityWait(), Is.False, "A remaining route blocked by the waiter's own body cannot independently finish");
                }
                finally { Write(entry, "CurrentSegmentTarget", currentTarget); }
                var segments = Read(front, "Segments"); var replans = Read(front, "Replans");
                Call("SendRouteSegment", front, CustomerVisitState.Exiting);
                Assert.That(Read(front, "PendingExitSegment"), Is.True); Assert.That(Read(front, "RequestId"), Is.EqualTo(0L));
                Assert.That(Read(front, "Route"), Is.SameAs(selected));
                Assert.That(Read(front, "Segments"), Is.EqualTo(segments)); Assert.That(Read(front, "Replans"), Is.EqualTo(replans));
                // Let the actual entry advance into the exit's clearance before retiring it.
                // 真实移动到出口净空内；停止后的身体必定堵住goal，不能误把安全替代路线判失败。
                for (var i = 0; i < 600 && Vector3.Distance(incoming.transform.position, goal) >= .8f; i++) yield return null;
                Assert.That(accepted.Contains(request), Is.True);
                Assert.That(Vector3.Distance(incoming.transform.position, goal), Is.LessThan(.8f));
                time.SetPaused(); var before = incoming.transform.position;
                for (var i = 0; i < 8; i++) { customers.Step(0); yield return null; }
                Assert.That(incoming.transform.position, Is.EqualTo(before)); Assert.That(Read(front, "Route"), Is.SameAs(selected));
                Assert.That(customers.Capacity.GetReservations().Single(r => ReferenceEquals(r.Token, token)).State,
                    Is.EqualTo(AnimalCafe.Capacity.ReservationState.Occupied));
                if (retire != 0)
                {
                    if (retire == 1) Assert.That(world.Service.Cancel(request), Is.True);
                    else incoming.enabled = false;
                    Assert.That(accepted.Contains(request), Is.False);
                    Assert.That(time.TrySetSpeed(GameSpeed.Normal), Is.True);
                    Assert.That(Wait(), Is.False, "Cancelled/disabled now-stationary entry cannot keep waiting alive");
                    Assert.That(PriorityWait(), Is.False);
                    Assert.That(Call("PlanPickupExit", actor.transform.position, goal, Call("RouteObstacles", id, null, false), actor.Settings), Is.Null,
                        "Stationary real body occupies final exit clearance; no safe alternate route exists");
                    customers.Step(1f / 60);
                    Assert.That(Read(front, "State"), Is.EqualTo(CustomerVisitState.Blocked));
                    Assert.That(Read(front, "PendingExitSegment"), Is.False); Assert.That(actualExits, Is.Empty);
                }
                else
                {
                    incoming.Agent.ResetPath(); // Steady driver retains its owned request and corners.
                    Assert.That(time.TrySetSpeed(GameSpeed.Normal), Is.True);
                    Assert.That(incoming.Agent.hasPath, Is.False); Assert.That(Wait(), Is.True);
                    for (var i = 0; i < 600 && Read(entry, "State").Equals(CustomerVisitState.Entering); i++) yield return null;
                    Assert.That(Vector3.Distance(incoming.transform.position, before), Is.GreaterThan(.1f));
                    Assert.That(accepted.Contains(request), Is.False, "Real entry request finishes before rechecking dispatch");
                    CafeLoopRuntimeProof.Set(owner, "stepping", false);
                    yield return Until(() => !entries.Contains(id) && actualExits.Count == 1);
                    Assert.That(owner.Snapshot.Orders.Values.Single(o => o.CustomerId == id).State, Is.EqualTo(OrderState.Completed));
                    Assert.That(customers.Capacity.GetReservations().Single(r => ReferenceEquals(r.Token, token)).State,
                        Is.EqualTo(AnimalCafe.Capacity.ReservationState.Released));
                }
            }
            finally { tracking = false; CafeLoopRuntimeProof.Set(owner, "stepping", false); time.SetNormal(); }
        }
        [UnityTest, Timeout(300000)] public IEnumerator UserPhotoCounterSixNativeInputProbe()
        {
            if (Application.isBatchMode || !Environment.GetCommandLineArgs().Contains("-longCounterNativeHold"))
                Assert.Ignore("Opt-in normal Editor input investigation; requires -longCounterNativeHold.");
            return ObserveUserLongCounter(6, GameSpeed.Normal, true, true);
        }

        [UnityTest] public IEnumerator ControlledLongSevenStaffAcquiresBeforeReadyCustomer() => ObserveControlledLongSeven(true);
        [UnityTest] public IEnumerator ControlledLongSevenReadyCustomerAcquiresBeforeStaff() => ObserveControlledLongSeven(false);

        // Only gates order real owner Updates; no fabricated route/request or manual actor Tick.
        // 仅控制真实gate的先后；几何查询是事后corroboration，不是原始决策回执。
        private IEnumerator ObserveControlledLongSeven(bool staffFirst)
        {
            var directory=Path.Combine("outputs","phase15","owned-service-route-repair","controlled",TestContext.CurrentContext.Test.Name);
            Directory.CreateDirectory(directory);
            var records=new List<string>(); var ids=new List<string>(); var exits=new HashSet<string>();
            var routeEvents=new List<string>();var boundaryStates=new Dictionary<string,string>();
            var exitObservers=new List<CafeActualExitObserver>();var cleanupDone=false;var completionAssertionsReached=false;
            var tokens=new Dictionary<string,AnimalCafe.Capacity.CapacityToken[]>();
            var delivered=new Dictionary<CafeOrderKey,long>(); var arrivals=new Dictionary<CafeOrderKey,int>();
            var collected=new HashSet<string>(); var random=UnityEngine.Random.state;
            var fields=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            object Raw(object value,string name)=>value?.GetType().GetField(name,fields)?.GetValue(value);
            string Stored(object value,string name)=>Raw(value,name) is NavigationTarget target?target.Position.ToString("F6"):Raw(value,name)?.ToString()??"NULL";
            object Visit(string id)=>id!=null && CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits").Contains(id)
                ?CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[id]:null;
            object Staff()=>CafeLoopRuntimeProof.Field<IList>(employees,"employees").Cast<object>()
                .SingleOrDefault(e=>(EmployeeRole)Raw(e,"Role")==EmployeeRole.Barista);
            object Request(string id)=>id!=null && CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor").Contains(id)
                ?CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor")[id]:null;
            string Points(IEnumerable<Vector3> route)=>route==null?"NULL":"["+string.Join(";",route.Select(p=>p.ToString("F6")))+"]";
            string TargetRoute(object runner,string name)=>Points((Raw(runner,name) as NavigationTarget[])?.Select(t=>t.Position));
            var third=(string)null; var second=(string)null; var armed=false; var allowed=false; var tracking=true;
            var firstCompleted=false;var lastFirstBody=false;bool? lastFirstLease=null;var firstLifecycleSeen=false;
            var firstBodyAtCustomerAcquire=false;var firstBodyAtStaffAcquire=false;var customerAlternateSide=false;var customerLongLeftUpper=false;
            Transform controlledGrid=null;float counterLeft=0,counterUpper=0;
            var simultaneous=false; var crossing=false; var geometryUnavailable=false;var geometrySamples=0;
            var heldOpportunities=0; var originalOpportunities=0; var refusedOpportunities=0;
            long heldHandle=0,firstCustomerRequest=0,firstStaffRequest=0,lastCustomerRequest=-1,lastStaffRequest=-1;
            CafeTaskToken? heldToken=null;
            long epoch=0; var stage="setup"; var qualification="PRECONDITION_NOT_REPRODUCED";
            Func<string,bool> originalGate=null; var originalCollection=false; var gateInstalled=false;
            SegmentFailureTrace cold=null; Action<CafeLoopSnapshot> observe=null; Action<CafeTaskEvent> task=null;
            var pickupHook=typeof(CustomerFlowController).GetField("PickupObserver",fields);
            var movementHook=typeof(EmployeeFlowController).GetField("MovementObserver",fields);
            var priorPickup=pickupHook.GetValue(customers);var priorMovement=movementHook.GetValue(employees);
            void RouteEvent(string operation,string id)
            {
                if(routeEvents.Count>=256)return;
                var visit=Visit(id);var runner=Raw(Staff(),"Runner");
                routeEvents.Add("sequence="+(routeEvents.Count+1)+" frame="+Time.frameCount+" event="+operation+" actor="+id+
                    " customerState="+Stored(visit,"State")+" customerRequest="+Stored(visit,"RequestId")+
                    " customerGeneration="+Stored(visit,"Generation")+" pendingPickup="+Stored(visit,"PendingPickupSegment")+
                    " pendingExit="+Stored(visit,"PendingExitSegment")+" owned="+Points(Raw(visit,"OwnedRoutePath") as IEnumerable<Vector3>)+
                    " staffVisit="+((Raw(runner,"checkpoint") is CafeWorkCheckpoint cp)?cp.VisitId:"NULL")+
                    " handle="+Stored(runner,"handleId")+" generation="+Stored(runner,"generation")+" state="+Stored(runner,"state")+
                    " selected="+Stored(runner,"selectedDeliveryTarget")+" index="+Stored(runner,"deliveryRouteIndex")+
                    " outward="+TargetRoute(runner,"deliveryRoute")+" pendingDelivery="+Stored(runner,"pendingDeliveryTraffic")+
                    " pendingReturn="+Stored(runner,"pendingReturnTraffic")+" moveLeg="+(Raw(runner,"moveLeg")==null?"NULL":"PRESENT"));
            }
            void Capture(string reason)
            {
                if(records.Count>=32) return;
                var visit=Visit(second); var staff=Staff(); var runner=Raw(staff,"Runner"); var leg=Raw(runner,"moveLeg");
                var staffId=(string)Raw(staff,"Id");var customerRequest=Request(second);var staffRequest=Request(staffId);
                var actor=Raw(staff,"Actor") as NavigationActor;
                var checkpoint=Raw(runner,"checkpoint") is CafeWorkCheckpoint saved?(CafeWorkCheckpoint?)saved:null;
                var first=ids.Count==0?null:Visit(ids[0]);var firstActor=Raw(first,"Actor") as NavigationActor;
                var pickup=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue");
                records.Add("frame="+Time.frameCount+" stage="+stage+" reason="+reason+" epoch="+owner.Snapshot.Epoch+
                    " holdCollection="+owner.HoldCollection+" allowThird="+allowed+" firstExited="+(ids.Count>0&&exits.Contains(ids[0]))+
                    "\nfirst="+(ids.Count==0?"NULL":ids[0])+" state="+Stored(first,"State")+" request="+Stored(first,"RequestId")+
                    " hasCollected="+Stored(first,"HasCollected")+" pickupDeparted="+Stored(first,"PickupDeparted")+
                    " pose="+(firstActor==null?"NULL":firstActor.transform.position.ToString("F6"))+
                    "\ncustomer="+second+" state="+Stored(visit,"State")+" request="+Stored(visit,"RequestId")+
                    " generation="+Stored(visit,"Generation")+" pose="+((Raw(visit,"Actor") as NavigationActor)?.transform.position.ToString("F6")??"NULL")+
                    " assignment="+Stored(visit,"Assignment")+" target="+Stored(visit,"CurrentSegmentTarget")+" finalAcquiredSide="+Stored(visit,"FinalTarget")+
                    " ownedPhase="+Stored(visit,"OwnedRoutePhase")+" ownedPath="+Points(Raw(visit,"OwnedRoutePath") as IEnumerable<Vector3>)+
                    " remaining="+Points(Raw(visit,"Route") as IEnumerable<Vector3>)+" serviceId="+Stored(customerRequest,"Id")+
                    " original="+Stored(customerRequest,"Original")+" serviceGeneration="+Stored(customerRequest,"Generation")+
                    "\nstaff="+(staffId??"UNKNOWN")+" checkpointVisit="+(checkpoint?.VisitId??"UNKNOWN")+" checkpointOrder="+(checkpoint.HasValue?checkpoint.Value.Order.ToString():"UNKNOWN")+
                    " checkpointStage="+(checkpoint.HasValue?checkpoint.Value.Stage.ToString():"UNKNOWN")+" state="+Stored(runner,"state")+
                    " handle="+Stored(runner,"handleId")+" taskGeneration="+Stored(runner,"generation")+
                    " pose="+(actor==null?"NULL":actor.transform.position.ToString("F6"))+
                    " selected="+Stored(runner,"selectedDeliveryTarget")+" deliveryIndex="+Stored(runner,"deliveryRouteIndex")+
                    " pendingDelivery="+Stored(runner,"pendingDeliveryTraffic")+" pendingReturn="+Stored(runner,"pendingReturnTraffic")+
                    " delivery="+TargetRoute(runner,"deliveryRoute")+" returnIndex="+Stored(runner,"returnRouteIndex")+
                    " return="+TargetRoute(runner,"returnRoute")+" moveLeg="+(leg==null?"NULL":"PRESENT")+
                    " legState="+Stored(leg,"State")+" legRequest="+Stored(leg,"RequestId")+" legGeneration="+Stored(leg,"Generation")+
                    " serviceId="+Stored(staffRequest,"Id")+" original="+Stored(staffRequest,"Original")+
                    " serviceGeneration="+Stored(staffRequest,"Generation")+" serviceLayout="+Stored(staffRequest,"LayoutRevision")+
                    "\notherRealGuests="+string.Join(";",customers.Snapshot.Where(v=>v.VisitId!=second&&(ids.Count==0||v.VisitId!=ids[0]))
                        .Select(v=>v.VisitId+"/"+v.State+"@"+v.Position.ToString("F6")+" request="+Stored(Visit(v.VisitId),"RequestId")))+
                    "\nleases="+(pickup==null?"UNINITIALIZED":string.Join(";",pickup.ServiceLeases.Select(p=>p.Key+"@"+p.Value.ToString("F6")))));
            }
            bool Blocked()=>customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked)||employees.Snapshot.Any(e=>e.State==EmployeeState.Blocked);
            IEnumerator WaitFor(Func<bool> predicate,string name)
            {
                var elapsed=0f;
                while(!predicate()&&!Blocked()&&elapsed<45f){elapsed+=Time.deltaTime;yield return null;}
                Capture(name+" satisfied="+predicate()+" blocked="+Blocked());
                if(Blocked()||!predicate()&&name=="drain-original-updates")qualification="CONTROLLED_FAILURE";
                Assert.That(Blocked(),Is.False,"CONTROLLED_FAILURE at "+name+"; inspect bounded snapshots/cold witness");
                Assert.That(predicate(),Is.True,"PRECONDITION_NOT_REPRODUCED at "+name+"; no fixture mutation/retry");
            }
            void Track(string id)
            {
                ids.Add(id);if(ids.Count==2)second=id;if(ids.Count==3)third=id;
                tokens.Add(id,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray());
                var actor=world.RegisteredActors.Single(a=>a.ActorId==id);var exit=actor.gameObject.AddComponent<CafeActualExitObserver>();
                exitObservers.Add(exit);
                exit.VisitId=id;exit.Receipt=(actual,pose)=>
                {
                    if(!tracking)return;
                    Assert.That(Vector3.Distance(pose,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position),Is.LessThanOrEqualTo(.081f));
                    Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==actual).State,Is.EqualTo(OrderState.Completed));
                    Assert.That(exits.Add(actual),Is.True);
                    Capture("actual-exit-receipt "+actual+" pose="+pose.ToString("F6"));
                };
            }
            controlledHandoffCleanup=CleanupControlledHandoff;
            try
            {
                UnityEngine.Random.InitState(150027);yield return MatrixStart();
                originalCollection=owner.HoldCollection;
                var runtime=Find<CafeLayoutRuntime>();var decor=Find<DecorationModeController>();
                for(var x=2;x<=7;x++)yield return MatrixFurniture("furniture.counter.module.01",new GridPosition(x,5),0);
                var support=runtime.Layout.FurnitureInstances.Single(f=>f.Position==new GridPosition(7,5));
                Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint,"pickup.point",new SurfaceSlotAddress(support.InstanceId,"slot.0")),Is.True);
                Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);decor.ExitDecorationMode();
                yield return Until(()=>owner.CanProgress&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));yield return null;
                Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True);epoch=owner.Snapshot.Epoch;
                controlledGrid=CafeLoopRuntimeProof.Field<Transform>(customers,"gridRoot");
                var counter=runtime.Layout.FurnitureInstances.Where(f=>f.Position.Y==5).ToArray();var cell=runtime.Layout.GridSettings.CellSize;
                counterLeft=counter.Min(f=>f.Position.X)*cell;counterUpper=(counter.Max(f=>f.Position.Y)+1)*cell;
                cold=new SegmentFailureTrace(this);
                Assert.That(priorPickup,Is.Null);Assert.That(priorMovement,Is.Null);
                pickupHook.SetValue(customers,(Action<string,string,PickupQueueService,QueueAssignment>)((operation,id,q,a)=>
                {if(operation=="collection.route-selected"||operation=="transfer.published")RouteEvent(operation,id);}));
                movementHook.SetValue(employees,(Action<string,string,string>)((id,operation,data)=>
                {if(operation=="delivery.route-selected"||operation=="callback")RouteEvent(operation+" "+data,id);}));
                originalGate=(Func<string,bool>)Raw(employees,"deliveryGate");Assert.That(originalGate,Is.Not.Null);
                employees.SetDeliveryGate(actor=>
                {
                    var runner=Raw(Staff(),"Runner");var checkpoint=(CafeWorkCheckpoint)Raw(runner,"checkpoint");
                    if(third==null||checkpoint.VisitId!=third)return originalGate(actor);
                    var handle=(long)Raw(runner,"handleId");
                    if(heldHandle==0){heldHandle=handle;heldToken=(CafeTaskToken)Raw(runner,"taskToken");Capture("third-original-prepared-gate-opportunity");}
                    Assert.That(handle,Is.EqualTo(heldHandle),"Same real prepared third handle");
                    Assert.That((CafeTaskToken)Raw(runner,"taskToken"),Is.EqualTo(heldToken.Value),"Same real third attempt token");
                    if(!allowed){heldOpportunities++;return false;}
                    originalOpportunities++;var acquired=originalGate(actor);if(!acquired)refusedOpportunities++;return acquired;
                });gateInstalled=true;
                task=value=>
                {
                    if(value.Role!=EmployeeRole.Barista||value.Kind!=CafeTaskEventKind.DeliveryArrived)return;
                    var key=value.Checkpoint.Order.Value;arrivals[key]=arrivals.TryGetValue(key,out var count)?count+1:1;
                };
                employees.IntegratedTaskEvent+=task;
                observe=snapshot=>
                {
                    Assert.That(snapshot.Epoch,Is.EqualTo(epoch));collected.UnionWith(snapshot.CollectedExitVisitIds);
                    foreach(var customer in customers.Snapshot)if(!ids.Contains(customer.VisitId))Track(customer.VisitId);
                    // All real C3/C4 handoffs, not only the initially armed C2/C3 pair.
                    foreach(var id in ids.Skip(2))
                    {
                        var actual=Visit(id);var actualRunner=Raw(Staff(),"Runner");
                        var signature=Stored(actual,"State")+"/"+Stored(actual,"RequestId")+"/"+Stored(actual,"PendingPickupSegment")+
                            "/"+Stored(actual,"PendingExitSegment")+"/"+Stored(actualRunner,"handleId")+"/"+Stored(actualRunner,"deliveryRouteIndex")+
                            "/"+Stored(actualRunner,"pendingDeliveryTraffic")+"/"+Stored(actualRunner,"pendingReturnTraffic");
                        if(!boundaryStates.TryGetValue(id,out var previous)||signature!=previous)
                        {boundaryStates[id]=signature;RouteEvent("real-customer/staff-handoff-boundary",id);}
                    }
                    if(ids.Count>=4)customers.AutoSpawn=false;
                    Assert.That(ids.Count,Is.LessThanOrEqualTo(4),"Original arrivals capped at four real visits");
                    if(ids.Count>0)
                    {
                        var first=Visit(ids[0]);var firstBody=first!=null;
                        var pickup=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue");
                        var firstLease=pickup==null?(bool?)null:pickup.ServiceLeases.ContainsKey(ids[0]);
                        if(!firstLifecycleSeen||firstBody!=lastFirstBody||firstLease!=lastFirstLease)
                        {firstLifecycleSeen=true;lastFirstBody=firstBody;lastFirstLease=firstLease;Capture("first-real-body/service-lease-transition");}
                        if(!firstCompleted&&snapshot.Orders.Values.Any(o=>o.CustomerId==ids[0]&&o.State==OrderState.Completed))
                        {
                            firstCompleted=true;owner.HoldCollection=true;
                            Capture("first-canonical-completed; hold-only-later-collection-next-owner-update");
                        }
                    }
                    foreach(var delivery in snapshot.Delivery)
                    {
                        if(delivered.TryGetValue(delivery.Key,out var sequence))Assert.That(delivery.DeliverySequence,Is.EqualTo(sequence));
                        else delivered.Add(delivery.Key,delivery.DeliverySequence);
                    }
                    if(!armed)return;
                    var visit=Visit(second);var runner=Raw(Staff(),"Runner");var leg=Raw(runner,"moveLeg");
                    var customerRequest=Request(second);var staffRequest=Request((string)Raw(Staff(),"Id"));
                    var customerLive=visit!=null&&(CustomerVisitState)Raw(visit,"State")==CustomerVisitState.MovingToPickupService&&
                        customerRequest!=null&&(long)Raw(visit,"RequestId")== (long)Raw(customerRequest,"Id");
                    var staffLive=Raw(runner,"deliveryRoute")!=null&&leg!=null&&staffRequest!=null&&
                        ((CafeWorkCheckpoint)Raw(runner,"checkpoint")).VisitId==third&&
                        (EmployeeState)Raw(leg,"State")==EmployeeState.CarryingToPickUp&&
                        (long)Raw(leg,"RequestId")== (long)Raw(staffRequest,"Id");
                    var accepted=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byId");
                    if(customerLive)Assert.That(accepted[(long)Raw(customerRequest,"Id")],Is.SameAs(customerRequest));
                    if(staffLive)Assert.That(accepted[(long)Raw(staffRequest,"Id")],Is.SameAs(staffRequest));
                    if(customerLive&&firstCustomerRequest==0)
                    {
                        firstCustomerRequest=(long)Raw(customerRequest,"Id");firstBodyAtCustomerAcquire=Visit(ids[0])!=null;
                        customerAlternateSide=((NavigationTarget)Raw(visit,"FinalTarget")).Position!=CafeLoopRuntimeProof.Field<Vector3>(customers,"pickupCustomer");
                        var owned=(Raw(visit,"OwnedRoutePath") as IEnumerable<Vector3>)?.Select(p=>controlledGrid.InverseTransformPoint(p)).ToArray();
                        customerLongLeftUpper=customerAlternateSide&&owned!=null&&owned.Any(p=>p.x<counterLeft)&&owned.Any(p=>p.z>counterUpper)&&
                            controlledGrid.InverseTransformPoint(((NavigationTarget)Raw(visit,"FinalTarget")).Position).z>counterUpper;
                    }
                    if(staffLive&&firstStaffRequest==0)
                    {firstStaffRequest=(long)Raw(staffRequest,"Id");firstBodyAtStaffAcquire=Visit(ids[0])!=null;}
                    var customerId=customerLive?(long)Raw(customerRequest,"Id"):0;var staffId=staffLive?(long)Raw(staffRequest,"Id"):0;
                    var changed=customerId!=lastCustomerRequest||staffId!=lastStaffRequest;
                    if(changed)
                    {lastCustomerRequest=customerId;lastStaffRequest=staffId;Capture("accepted-request/segment-handoff");}
                    if(!customerLive||!staffLive||crossing||geometrySamples>=8||!changed)return;
                    simultaneous=true;Capture("simultaneous-real-accepted-ownership");
                    geometrySamples++;
                    // Post-decision query of actual stored selected waypoints, never used to drive motion.
                    IReadOnlyList<Vector3> Expand(Vector3 start,IEnumerable<Vector3> targets)
                    {
                        var path=new List<Vector3>{start};foreach(var target in targets)
                        {var part=adapter.GetCompletePath(path.Last(),target);if(part==null)return null;path.AddRange(part.Skip(1));}return path;
                    }
                    var customerActor=(NavigationActor)Raw(visit,"Actor");var staffActor=(NavigationActor)Raw(Staff(),"Actor");
                    var customerPath=Expand(customerActor.transform.position,new[]{((NavigationTarget)Raw(visit,"CurrentSegmentTarget")).Position}.Concat((IEnumerable<Vector3>)Raw(visit,"Route")));
                    var staffPath=Expand(staffActor.transform.position,((NavigationTarget[])Raw(runner,"deliveryRoute")).Skip((int)Raw(runner,"deliveryRouteIndex")).Select(t=>t.Position));
                    geometryUnavailable=customerPath==null||staffPath==null;
                    if(!geometryUnavailable)
                    {
                        // Clip both real remaining paths to the left of this actual counter footprint.
                        // 只判定同一左侧通道内的相向/交叉，别处的冲突不算目标形成。
                        List<Vector3[]> LeftSegments(IReadOnlyList<Vector3> path)
                        {
                            var result=new List<Vector3[]>();
                            for(var i=1;i<path.Count;i++)
                            {
                                var a=controlledGrid.InverseTransformPoint(path[i-1]);var b=controlledGrid.InverseTransformPoint(path[i]);
                                if(a.x>=counterLeft&&b.x>=counterLeft)continue;
                                if(a.x>counterLeft)a=Vector3.Lerp(a,b,(counterLeft-a.x)/(b.x-a.x));
                                if(b.x>counterLeft)b=Vector3.Lerp(a,b,(counterLeft-a.x)/(b.x-a.x));
                                result.Add(new[]{a,b});
                            }
                            return result;
                        }
                        var conflict=typeof(CustomerFlowController).GetMethod("RoutesConflict",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                        foreach(var customerSegment in LeftSegments(customerPath))
                        {
                            foreach(var staffSegment in LeftSegments(staffPath))
                            {
                                var a=customerSegment[1]-customerSegment[0];var b=staffSegment[1]-staffSegment[0];
                                var opposed=Vector3.Dot(a,b)<0;var angled=Vector3.Cross(a,b).sqrMagnitude>1e-8f;
                                if(!(opposed||angled)||!(bool)conflict.Invoke(null,new object[]{customerSegment,staffSegment,
                                    2*customerActor.Settings.AgentRadius+customerActor.Settings.CollisionSkin+customerActor.Settings.Epsilon}))continue;
                                crossing=true;
                                records.Add("POST_DECISION_LEFT_CORRIDOR_LOCAL opposed="+opposed+" angled="+angled+
                                    " customer="+Points(customerSegment)+" outwardStaff="+Points(staffSegment));break;
                            }
                            if(crossing)break;
                        }
                    }
                    records.Add("POST_DECISION_CORROBORATION crossing="+crossing+" unavailable="+geometryUnavailable+" customer="+Points(customerPath)+" staff="+Points(staffPath));
                };
                owner.SnapshotChanged+=observe;
                stage="concurrent-original-arrivals-second-ready";tools.ResumeCollection();tools.ResumeArrivals();
                yield return WaitFor(()=>ids.Count>=3&&firstCompleted&&owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==second&&o.State==OrderState.ReadyForPickup)&&
                    customers.Snapshot.Any(v=>v.VisitId==second&&v.State==CustomerVisitState.PickupWaiting),stage);
                Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==second).State,Is.EqualTo(OrderState.ReadyForPickup));
                Assert.That(customers.Snapshot.Single(v=>v.VisitId==second).State,Is.EqualTo(CustomerVisitState.PickupWaiting));
                armed=true;stage=staffFirst?"staff-first-acquire":"customer-first-acquire";Capture("second-ready; third-gate-installed; first-exit-unmodified");
                if(staffFirst)
                {
                    allowed=true;yield return WaitFor(()=>firstStaffRequest>0,stage);
                    Assert.That(firstCustomerRequest,Is.Zero);Capture("release-collection-next-owner-update");tools.ResumeCollection();
                }
                else
                {
                    tools.ResumeCollection();yield return WaitFor(()=>firstCustomerRequest>0,stage);
                    Assert.That(firstStaffRequest,Is.Zero);Capture("release-delivery-gate-next-owner-update");allowed=true;
                }
                stage="drain-original-updates";
                var drainElapsed=0f;var noCompletion=0f;var completed=owner.Snapshot.Orders.Values.Count(o=>o.State==OrderState.Completed);
                while((exits.Count<4||employees.Snapshot.Any(e=>e.State!=EmployeeState.Idle))&&!Blocked()&&drainElapsed<180f&&noCompletion<45f)
                {
                    yield return null;drainElapsed+=Time.deltaTime;
                    var next=owner.Snapshot.Orders.Values.Count(o=>o.State==OrderState.Completed);
                    noCompletion=next>completed?0:noCompletion+Time.deltaTime;completed=next;
                }
                Capture("drain-end elapsed="+drainElapsed+" noCompletion="+noCompletion+" actualExits="+exits.Count+" realGuests="+ids.Count);
                if(Blocked())qualification="CONTROLLED_FAILURE_PRODUCT_BLOCKED";
                else if(exits.Count!=4)qualification="PRECONDITION_NOT_REPRODUCED_FIXTURE_DRAIN_BUDGET_EXHAUSTED";
                Assert.That(Blocked(),Is.False,"CONTROLLED_FAILURE_PRODUCT_BLOCKED; inspect original cold witness");
                Assert.That(exits.Count,Is.EqualTo(4),"FIXTURE_DRAIN_BUDGET_EXHAUSTED; original arrivals/admission were not forced");
                Assert.That(employees.Snapshot.All(e=>e.State==EmployeeState.Idle),Is.True);
                Assert.That(firstStaffRequest,Is.GreaterThan(0));Assert.That(firstCustomerRequest,Is.GreaterThan(0));
                Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(4));Assert.That(owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed),Is.True);
                CollectionAssert.AreEqual(ids,owner.Snapshot.PaidVisitIds);CollectionAssert.AreEquivalent(ids,collected);
                Assert.That(delivered.Count,Is.EqualTo(4));Assert.That(arrivals.Count,Is.EqualTo(4));Assert.That(arrivals.Values.All(n=>n==1),Is.True);
                Assert.That(owner.Snapshot.Delivery,Is.Empty);Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                foreach(var pair in tokens)
                {
                    var reservations=customers.Capacity.GetReservations().Where(r=>r.OwnerId==pair.Key).ToArray();
                    CollectionAssert.AreEqual(pair.Value,reservations.Select(r=>r.Token));
                    Assert.That(reservations.All(r=>r.State==AnimalCafe.Capacity.ReservationState.Released),Is.True);
                }
                qualification=simultaneous&&crossing&&customerLongLeftUpper
                    ?"CONTROLLED_CROSSING_DRAINED":"PRECONDITION_NOT_REPRODUCED";
                completionAssertionsReached=true;
                Capture("drained; original-guard-deferrals="+refusedOpportunities);
            }
            finally { CleanupControlledHandoff("normalfinally"); }

            void CleanupControlledHandoff(string reason)
            {
                if(cleanupDone)return;
                cleanupDone=true;tracking=false;controlledHandoffCleanup=null;
                pickupHook.SetValue(customers,priorPickup);movementHook.SetValue(employees,priorMovement);
                if(observe!=null)owner.SnapshotChanged-=observe;if(task!=null)employees.IntegratedTaskEvent-=task;
                foreach(var observer in exitObservers)if(observer!=null)observer.Receipt=null;
                if(gateInstalled)employees.SetDeliveryGate(originalGate);
                if(owner!=null)owner.HoldCollection=originalCollection;if(customers!=null)customers.AutoSpawn=false;
                UnityEngine.Random.state=random;cold?.Finish();
                bool Attached(object source,string name,Delegate handler)=>(Raw(source,name) as Delegate)?.GetInvocationList().Contains(handler)==true;
                File.WriteAllText(Path.Combine(directory,"cleanup-receipt.txt"),"reason="+reason+" tracking="+tracking+
                    " actionCleared="+(controlledHandoffCleanup==null)+" snapshotDetached="+!Attached(owner,"SnapshotChanged",observe)+
                    " taskDetached="+!Attached(employees,"IntegratedTaskEvent",task)+
                    " exitReceiptsDetached="+exitObservers.All(o=>o==null||o.Receipt==null)+
                    " gateRestored="+(!gateInstalled||ReferenceEquals(Raw(employees,"deliveryGate"),originalGate))+
                    " collectionRestored="+(owner!=null&&owner.HoldCollection==originalCollection)+
                    " coldHookDetached="+(cold==null||Raw(customers,"SegmentFailureObserver")==null)+"\n");
                File.WriteAllText(Path.Combine(directory,"route-events.txt"),"Actual event-boundary ordering; historical acquisition order remains UNKNOWN.\n"+string.Join("\n",routeEvents));
                File.WriteAllText(Path.Combine(directory,"handoff.txt"),"qualification="+qualification+" stage="+stage+
                    " cleanupReason="+reason+" completionAssertionsReached="+completionAssertionsReached+
                    " staffFirst="+staffFirst+" simultaneous="+simultaneous+" crossing="+crossing+" geometryUnavailable="+geometryUnavailable+
                    " firstBodyAtCustomerAcquire="+firstBodyAtCustomerAcquire+" firstBodyAtStaffAcquire="+firstBodyAtStaffAcquire+
                    " customerAlternateSide="+customerAlternateSide+" customerLongLeftUpper="+customerLongLeftUpper+
                    " localCounterLeft="+counterLeft+" localCounterUpper="+counterUpper+
                    " firstCustomerRequest="+firstCustomerRequest+" firstStaffRequest="+firstStaffRequest+" heldHandle="+heldHandle+
                    " heldOpportunities="+heldOpportunities+" originalOpportunities="+originalOpportunities+" refusedOpportunities="+refusedOpportunities+
                    " actualExits="+exits.Count+" coldRecords="+(cold?.Records.Count??0)+"\nHistorical defect/root cause remains UNKNOWN.\n"+string.Join("\n\n",records));
                TestContext.WriteLine("CONTROLLED_HANDOFF "+qualification+" stage="+stage+" evidence="+directory);
            }
        }

        private IEnumerator ObserveUserLongCounter(int length, GameSpeed speed, bool photoDirection = false, bool nativeInput = false,bool publicationProbe=false,bool originalExitDecisions=false,bool exitConnectorTimings=false)
        {
            originalExitDecisions=originalExitDecisions || Environment.GetCommandLineArgs().Contains("-photoEmptyAdmissionAnchorWitness");
            CorridorTrace corridorTrace = null;
            SegmentFailureTrace segmentFailureTrace = null;
            PickupTargetDecisionTrace targetDecisionTrace = null;
            OriginalExitDecisionTrace exitDecisions=null;
            ExitConnectorTimingTrace connectorTimings=null;
            var receiptTrackers=new List<CafeActualExitObserver>();
            var priorCleanup=controlledHandoffCleanup;
            Action<string> diagnosticCleanup=null;
            var directory = Path.Combine("outputs", "phase15", "long-counter-stall", "live-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Directory.CreateDirectory(directory);
            var output = Path.Combine(directory, TestContext.CurrentContext.Test.Name + ".txt");
            var random = UnityEngine.Random.state;
            UnityEngine.Random.InitState(150027);
            var history = new Queue<string>();
            var seen = new HashSet<string>();
            var exited = new HashSet<string>();
            var collectedSeen = new HashSet<string>();
            var originalTokens = new Dictionary<string, AnimalCafe.Capacity.CapacityToken[]>();
            var watching = true;
            var frameClock = System.Diagnostics.Stopwatch.StartNew();
            var previousWallMs = 0d;
            var maxFrameMs = 0d;
            var observedFrames = 0;
            var over500Ms = 0;
            void ObserveFrame()
            {
                var now = frameClock.Elapsed.TotalMilliseconds;
                var interval = now - previousWallMs;
                previousWallMs = now;
                maxFrameMs = Math.Max(maxFrameMs, interval);
                observedFrames++;
                if (interval > 500) over500Ms++;
            }
            Action<CafeLoopSnapshot> observe = null;
            try
            {
                yield return MatrixStart();
                var runtime = Find<CafeLayoutRuntime>();
                var decor = Find<DecorationModeController>();
                // Second recipe also matches the photograph's fixed-camera direction:
                // far-right CR, near-left pickup, CM behind that end of the counter.
                // 第二组按相机方向调换两端，咖啡机放取餐端后方，仍是近似格子。
                if (photoDirection)
                {
                    var cash = runtime.Layout.FurnitureInstances.Single(f => f.Position == new GridPosition(1, 5));
                    var machine = runtime.Layout.FurnitureInstances.Single(f => f.Position == new GridPosition(4, 6));
                    yield return UserMoveCounterSupport(cash.InstanceId, new GridPosition(length, 4));
                    yield return UserMoveCounterSupport(machine.InstanceId, new GridPosition(1, 7));
                    var equipment = runtime.FunctionalSurfaceLayout.MountedInstances.Single(m => m.DefinitionId == "equipment.coffee-machine.01");
                    Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment, equipment.InstanceId), Is.True);
                    Assert.That(decor.TryRotateFunctionalSurfacePreview(), Is.True);
                    Assert.That(decor.TryConfirmFunctionalSurfacePreview(), Is.True);
                    for (var x = 1; x < length; x++)
                        yield return MatrixFurniture("furniture.counter.module.01", new GridPosition(x, 4), 0);
                }
                else
                {
                    for (var x = 2; x <= length; x++)
                        yield return MatrixFurniture("furniture.counter.module.01", new GridPosition(x, 5), 0);
                }
                var pickupPosition = photoDirection ? new GridPosition(1, 4) : new GridPosition(length, 5);
                var support = runtime.Layout.FurnitureInstances.Single(f => f.Position == pickupPosition);
                Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint,
                    "pickup.point", new SurfaceSlotAddress(support.InstanceId, "slot.0")), Is.True);
                Assert.That(decor.TryConfirmFunctionalSurfacePreview(), Is.True);
                decor.ExitDecorationMode();
                yield return Until(() => owner.CanProgress && employees.Snapshot.All(e => e.State == EmployeeState.Idle));
                yield return null;
                File.WriteAllText(output, "APPROXIMATE screenshot layout; length=" + length + " speed=" + speed +
                    (photoDirection ? " CR=(" + length + ",4) CM=(1,7) rotated180 pickup=(1,4)" :
                    " CR=(1,5) CM=(4,6) pickup=(" + length + ",5)") + " modules contiguous\n");
                tools.ResumeCollection();
                Assert.That(time.TrySetSpeed(speed), Is.True);
                if(publicationProbe)
                    typeof(CustomerFlowController).GetField("clock",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                        .SetValue(customers,new CustomerSpawnClock(()=>.1f)); // Fixed real one-second arrival opportunities; no injected visits.
                if (publicationProbe || Environment.GetCommandLineArgs().Contains("-corridorTrace") || Environment.GetCommandLineArgs().Contains("-stallObservationTrace")) corridorTrace = new CorridorTrace(this);
                if(Environment.GetCommandLineArgs().Contains("-segmentFailureWitness")) segmentFailureTrace=new SegmentFailureTrace(this);
                if(Environment.GetCommandLineArgs().Contains("-pickupTargetWitness")) targetDecisionTrace=new PickupTargetDecisionTrace(this);
                if(originalExitDecisions||exitConnectorTimings)
                {
                    if(originalExitDecisions)exitDecisions=new OriginalExitDecisionTrace(this,directory);
                    if(exitConnectorTimings)connectorTimings=new ExitConnectorTimingTrace(this,directory);
                    var cleaned=false;
                    diagnosticCleanup=reason=>
                    {
                        if(cleaned)return;cleaned=true;
                        // Timeout teardown must save evidence and retire receipts BEFORE unloading actors.
                        // 超时清场不能算真实离店；先解除回执，再保存已有原判定。
                        watching=false;
                        if(observe!=null&&owner!=null)owner.SnapshotChanged-=observe;
                        foreach(var tracker in receiptTrackers)if(tracker!=null)tracker.Receipt=null;
                        if(ReferenceEquals(controlledHandoffCleanup,diagnosticCleanup))controlledHandoffCleanup=priorCleanup;
                        exitDecisions?.Finish(reason);connectorTimings?.Finish(reason);
                        priorCleanup?.Invoke(reason);
                    };
                    controlledHandoffCleanup=diagnosticCleanup;
                }
                var lastCompleted = 0;
                var idleSeconds = 0f;
                var elapsed = 0f;
                var nextSample = 0f;
                var firstBlocked = false;
                observe = snapshot =>
                {
                    // Collected ownership retires after exit; the payment FIFO remains cumulative.
                    // 领取集合记录在途历史；付款FIFO保留终点账本断言。
                    collectedSeen.UnionWith(snapshot.CollectedExitVisitIds);
                    foreach (var visit in customers.Snapshot)
                    {
                        if (!seen.Add(visit.VisitId)) continue;
                        var body = world.RegisteredActors.Single(a => a.ActorId == visit.VisitId);
                        originalTokens.Add(visit.VisitId, customers.Capacity.GetReservations().Where(r => r.OwnerId == visit.VisitId).Select(r => r.Token).ToArray());
                        var total = customers.Capacity.GetReservations().Single(r => r.OwnerId == visit.VisitId && r.Kind == AnimalCafe.Capacity.CapacityKind.TotalCustomers).Token;
                        var tracker = body.gameObject.AddComponent<CafeActualExitObserver>();
                        if(originalExitDecisions||exitConnectorTimings)receiptTrackers.Add(tracker);
                        tracker.VisitId = visit.VisitId;
                        tracker.Receipt = (id, pose) =>
                        {
                            if (!watching) return;
                            Assert.That(Vector3.Distance(pose, CafeLoopRuntimeProof.Field<Transform>(customers, "exit").position),
                                Is.LessThanOrEqualTo(.081f), "Only actual near-exit destruction counts");
                            Assert.That(customers.Capacity.GetReservations().Single(r => ReferenceEquals(r.Token, total)).State,
                                Is.EqualTo(AnimalCafe.Capacity.ReservationState.Released));
                            Assert.That(owner.Snapshot.Orders.Values.Single(o => o.CustomerId == id).State, Is.EqualTo(OrderState.Completed));
                            Assert.That(exited.Add(id), Is.True, "One actual receipt for the original body");
                        };
                    }
                    if (seen.Count >= 20) customers.AutoSpawn = false;
                };
                owner.SnapshotChanged += observe;
                tools.ResumeArrivals();
                previousWallMs = frameClock.Elapsed.TotalMilliseconds;
                while (elapsed < 360f)
                {
                    yield return null;
                    ObserveFrame();
                    if(Environment.GetCommandLineArgs().Contains("-pickupBodyWaitProof"))
                        Assert.That(targetDecisionTrace?.FirstUnsafeBodyWaitPublication,Is.Null,
                            "A genuinely qualified moving-body wait must not lose its selected suffix to a later parking target");
                    if(publicationProbe) Assert.That(corridorTrace.FirstReservationConflict,Is.Null,
                        "Reject the later pickup target BEFORE real reservation publication; preserve the earlier service owner");
                    foreach (var active in customers.Snapshot)
                    {
                        var records = customers.Capacity.GetReservations().Where(r => r.OwnerId == active.VisitId).ToArray();
                        CollectionAssert.AreEqual(originalTokens[active.VisitId], records.Select(r => r.Token), "No replacement capacity tokens while in route");
                        Assert.That(records.Single(r => r.Kind == AnimalCafe.Capacity.CapacityKind.TotalCustomers).State,
                            Is.EqualTo(AnimalCafe.Capacity.ReservationState.Occupied), "Original total token stays owned until actual exit");
                    }
                    elapsed += Time.deltaTime;
                    var completed = owner.Snapshot.Orders.Values.Count(o => o.State == OrderState.Completed);
                    if (completed != lastCompleted) { lastCompleted = completed; idleSeconds = 0; }
                    else if (customers.Snapshot.Count > 0) idleSeconds += Time.deltaTime;
                    else idleSeconds = 0;
                    var blocked = customers.Snapshot.Any(v => v.State == CustomerVisitState.Blocked) ||
                        employees.Snapshot.Any(e => e.State == EmployeeState.Blocked) || owner.Snapshot.Status == "Blocked";
                    if (elapsed >= nextSample || blocked && !firstBlocked)
                    {
                        var sample = "t=" + elapsed.ToString("F2") + " idle=" + idleSeconds.ToString("F2") +
                            " seen=" + seen.Count + " actualExit=" + exited.Count + "\n" + UserCounterDiagnostic();
                        history.Enqueue(sample); while (history.Count > 10) history.Dequeue();
                        File.AppendAllText(output, sample + "\n\n");
                        nextSample = elapsed + 5;
                    }
                    if (blocked && !firstBlocked)
                    {
                        firstBlocked = true;
                        TestContext.WriteLine("USER_LONG_COUNTER_FIRST_BLOCK " + UserCounterDiagnostic());
                        File.AppendAllText(output, UserExitPathQueries("FIRST_BLOCK") + "\n");
                        File.AppendAllText(output, UserTrafficPathQueries("FIRST_BLOCK") + "\n");
                    }
                    if (idleSeconds >= 45f)
                    {
                        File.AppendAllText(output, UserExitPathQueries("STALLED") + "\n");
                        File.AppendAllText(output, UserTrafficPathQueries("STALLED") + "\n");
                        TestContext.WriteLine("USER_LONG_COUNTER_STALL\n" + string.Join("\n", history));
                        if (nativeInput)
                        {
                            watching = false; // Manual inspection/runner timeout must not count scene cleanup as a real customer exit.
                            // Keep the normal simulation alive for actual mouse input checks.
                            // 保留现场，继续真实Update；不暂停Editor或注入按钮调用。
                            var holdUntil = Time.realtimeSinceStartupAsDouble + 180;
                            var heartbeatAt = 0d;
                            File.WriteAllText(Path.Combine(directory, "native-input-ready.txt"), output);
                            while (Time.realtimeSinceStartupAsDouble < holdUntil)
                            {
                                yield return null;
                                ObserveFrame();
                                if (Time.realtimeSinceStartupAsDouble < heartbeatAt) continue;
                                heartbeatAt = Time.realtimeSinceStartupAsDouble + 2;
                                File.AppendAllText(Path.Combine(directory, "native-input-heartbeat.txt"),
                                    DateTime.UtcNow.ToString("O") + " frame=" + Time.frameCount + " speed=" + time.CurrentSpeed +
                                    " decor=" + decor.IsOpen + " completed=" + owner.Snapshot.Orders.Values.Count(o => o.State == OrderState.Completed) + "\n");
                                File.WriteAllText(Path.Combine(directory, "frame-intervals-live.txt"),
                                    "ObservedFrames=" + observedFrames + " MaxWallFrameMilliseconds=" + maxFrameMs.ToString("F3") +
                                    " Over500ms=" + over500Ms + "\nPartial live frame intervals; includes Editor/render/test IO.\n");
                            }
                        }
                        Assert.Fail("No completed order for 45 game seconds with live customers. " + UserCounterDiagnostic());
                    }
                    // Destroy callbacks arrive at end of frame, after the visit leaves its live store.
                    // 等待真实出口回执，不把“从活跃列表移除”提前当成实际销毁完成。
                    if (seen.Count >= 20 && exited.Count == 20 && customers.Snapshot.Count == 0 &&
                        employees.Snapshot.All(e => e.State == EmployeeState.Idle)) break;
                }
                TestContext.WriteLine("USER_LONG_COUNTER_END " + UserCounterDiagnostic());
                Assert.That(seen.Count, Is.EqualTo(20), "Natural arrivals must reach twenty distinct customers");
                Assert.That(exited.Count, Is.EqualTo(20), "All original customer bodies must actually exit");
                Assert.That(owner.Snapshot.Orders.Count, Is.EqualTo(20));
                Assert.That(owner.Snapshot.Orders.Values.All(o => o.State == OrderState.Completed), Is.True);
                CollectionAssert.AreEquivalent(seen, owner.Snapshot.Orders.Values.Select(o => o.CustomerId));
                CollectionAssert.AreEquivalent(seen, owner.Snapshot.PaidVisitIds);
                CollectionAssert.AreEquivalent(seen, collectedSeen);
                foreach (var pair in originalTokens)
                {
                    var records = customers.Capacity.GetReservations().Where(r => r.OwnerId == pair.Key).ToArray();
                    CollectionAssert.AreEqual(pair.Value, records.Select(r => r.Token));
                    Assert.That(records.All(r => r.State == AnimalCafe.Capacity.ReservationState.Released), Is.True);
                }
                Assert.That(employees.Snapshot.All(e => e.State == EmployeeState.Idle), Is.True);
                Assert.That(customers.Capacity.GetCapacities().All(c => c.Used == 0), Is.True);
                Assert.That(owner.Snapshot.Delivery, Is.Empty);
                if(publicationProbe) Assert.That(corridorTrace.ServiceDeferrals,Is.GreaterThan(0),"This controlled chronology must actually encounter a protected service corridor");
                File.AppendAllText(output, "RESULT twenty actual exits, all orders completed, capacities zero\n");
            }
            finally
            {
                diagnosticCleanup?.Invoke("finally");
                segmentFailureTrace?.Finish();
                targetDecisionTrace?.Finish();
                corridorTrace?.Finish();
                File.WriteAllText(Path.Combine(directory, "frame-intervals.txt"), "ObservedFrames=" + observedFrames +
                    " MaxWallFrameMilliseconds=" + maxFrameMs.ToString("F3") + " Over500ms=" + over500Ms +
                    "\nWhole frame intervals include Editor/render/test IO; not exclusive gameplay CPU timing.\n");
                watching = false;
                if (observe != null) owner.SnapshotChanged -= observe;
                if (customers != null) customers.AutoSpawn = false;
                UnityEngine.Random.state = random;
            }
        }

        // Synchronous timing around existing callbacks only; no extra Native/policy queries or hot file writes.
        // 只量原blocked连接段到同次调用下一条原事件；跨帧/跨调用间隔不当作规划耗时。
        private sealed class ExitConnectorTimingTrace
        {
            private readonly CafePickupSideTests test;
            private readonly string output;
            private readonly System.Reflection.FieldInfo hook;
            private readonly object prior;
            private readonly Dictionary<string,(long Invocation,long Ticks,int Frame,int Queries,bool Covered,string Context)> pending=
                new Dictionary<string,(long,long,int,int,bool,string)>();
            private readonly List<(double Ms,int Queries,bool Covered,string Context)> samples=new List<(double,int,bool,string)>();
            private int unclosed,dropped;
            private bool finished;
            private static long Number(string text,string key)
            {
                var start=text.IndexOf(key,StringComparison.Ordinal);if(start<0)return -1;start+=key.Length;
                var end=text.IndexOf(' ',start);return long.Parse(end<0?text.Substring(start):text.Substring(start,end-start),System.Globalization.CultureInfo.InvariantCulture);
            }
            public ExitConnectorTimingTrace(CafePickupSideTests value,string directory)
            {
                test=value;output=Path.Combine(directory,"connector-timings.txt");
                hook=typeof(CustomerFlowController).GetField("CorridorObserver",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                prior=hook.GetValue(test.customers);Assert.That(prior,Is.Null,"Timing excludes general diagnostic observers.");
                hook.SetValue(test.customers,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)Observe);
                TestContext.WriteLine("EXIT_CONNECTOR_TIMINGS "+output);
            }
            private void Observe(string operation,string caller,string related,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] occupied,string result)
            {
                if(finished||caller==null||result==null||operation!="plan.exit-direct"&&operation!="plan.exit-boundary-leg"&&operation!="plan.exit-boundary-detour-leg")return;
                var now=System.Diagnostics.Stopwatch.GetTimestamp();var invocation=Number(result,"invocation=");var queries=(int)Number(result,"queries=");
                if(pending.TryGetValue(caller,out var previous))
                {
                    pending.Remove(caller);
                    if(previous.Invocation==invocation&&previous.Frame==Time.frameCount)
                    {
                        var ms=(now-previous.Ticks)*1000d/System.Diagnostics.Stopwatch.Frequency;
                        if(samples.Count<4096)samples.Add((ms,queries-previous.Queries,previous.Covered,previous.Context+" next="+operation));else dropped++;
                    }
                    else unclosed++;
                }
                if(operation!="plan.exit-boundary-leg"||!result.Contains("outcome=blocked"))return;
                var end=path==null||path.Count==0?(Vector3?)null:path[path.Count-1];
                var covered=end.HasValue&&occupied!=null&&occupied.Any(p=>
                {var delta=p-end.Value;delta.y=0;return delta.sqrMagnitude<.911f*.911f;});
                pending[caller]=(invocation,now,Time.frameCount,queries,covered,"caller="+caller+" "+result+" target="+target.ToString("F9"));
            }
            public void Finish(string reason)
            {
                if(finished)return;finished=true;hook.SetValue(test.customers,prior);
                File.WriteAllText(output,"Original synchronous blocked-connector to next same-invocation callback; includes next original query/observer overhead.\n"+
                    "reason="+reason+" samples="+samples.Count+" dropped="+dropped+" unclosed="+(unclosed+pending.Count)+
                    " totalMs="+samples.Sum(s=>s.Ms).ToString("F3")+" coveredEndpointMs="+samples.Where(s=>s.Covered).Sum(s=>s.Ms).ToString("F3")+
                    " queryDelta="+samples.Sum(s=>s.Queries)+"\n"+string.Join("\n",samples.OrderByDescending(s=>s.Ms)
                        .Select(s=>"ms="+s.Ms.ToString("F3")+" queries="+s.Queries+" originalNativeEndpointCovered="+s.Covered+" "+s.Context)));
            }
        }

        // Only original calls, bounded per caller. No second planner, native getters or policy checks.
        // 按顾客保留最近原调用；首次Blocked立即落盘，不读取事后状态来重算原因。
        private sealed class OriginalExitDecisionTrace
        {
            private readonly CafePickupSideTests test;
            private readonly string output;
            private readonly System.Reflection.FieldInfo hook;
            private readonly Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string> prior;
            private readonly Dictionary<string,Queue<string>> records=new Dictionary<string,Queue<string>>();
            private long sequence,evicted;
            private int errors;
            private bool frozen,finished;
            private readonly bool firstFailureEnabled=Environment.GetCommandLineArgs().Contains("-photoExitFirstFailureWitness");
            private readonly bool checkoutEnabled=Environment.GetCommandLineArgs().Contains("-photoCheckoutFirstRefusalWitness");
            private readonly bool emptyAdmissionEnabled=Environment.GetCommandLineArgs().Contains("-photoEmptyAdmissionAnchorWitness");
            private bool emptyAdmissionAttempted,admissionWaitAttempted,admissionWaitHookInstalled;
            private string emptyAdmissionRecord,admissionWaitRecord;
            private int emptyAdmissionErrors;
            private readonly List<string> emptyAdmissionErrorDetails=new List<string>();
            private readonly Queue<string> checkoutStates=new Queue<string>();
            private readonly Dictionary<TrafficIntentKey,string> checkoutTransitions=new Dictionary<TrafficIntentKey,string>();
            private readonly Dictionary<TrafficIntentKey,string> checkoutWaitFailures=new Dictionary<TrafficIntentKey,string>();
            private readonly List<string> checkoutErrors=new List<string>();
            private string checkoutLastState,checkoutTerminal;private long checkoutChanges,checkoutDropped;
            private void CheckoutError(Exception error){if(checkoutErrors.Count<4)checkoutErrors.Add(error.GetType().Name+":"+error.Message);}
            private readonly List<(string Key,string Text,bool Pinned)> canonicalHistory=new List<(string,string,bool)>();
            private readonly HashSet<string> canonicalKeys=new HashSet<string>(),canonicalBoundKeys=new HashSet<string>();
            private readonly List<string> canonicalErrors=new List<string>();
            private long canonicalSequence,canonicalDropped,canonicalErrorCount;
            private int canonicalCoreBaseline=-1,canonicalCfcBaseline=-1,canonicalCoreFreeze=-1,canonicalCfcFreeze=-1,canonicalFreezeFrame=-1;
            private string canonicalFreezeKind;
            private readonly List<Action> firstFailureRestore=new List<Action>();
            private readonly List<string> firstFailureErrors=new List<string>();
            private ServiceTrafficCoordinator firstFailureCore;
            private bool firstFailureSampled;
            private string firstFailureRecord;
            private int firstFailureInstalled;
            private static string Identity(object value)=>value==null?"NULL":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            private static string Key(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
            private static string Value(object value)
            {
                if(value==null)return "NULL";
                if(value is Vector3 point)return point.ToString("F9");
                if(value is TrafficIntentKey key)return Key(key);
                if(value is TrafficNativeStamp stamp)return stamp.RequestId+"/"+stamp.LegGeneration;
                if(value is NavigationTarget target)return target.Position.ToString("F9");
                if(value is QueueAssignment assignment)return assignment.VisitId+"/slot"+assignment.SlotIndex+"/g"+assignment.Generation+"/arrived="+assignment.Arrived+"@"+assignment.Position.ToString("F9");
                if(value is IEnumerable<Vector3> path)return Points(path);
                if(value is float number)return number.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
                return value is string || value.GetType().IsValueType?value.ToString():Identity(value);
            }
            private static string Members(object value,params string[] names)=>value==null?"OWNER_NULL":string.Join(" ",names.Select(name=>
            {
                const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
                var field=value.GetType().GetField(name,flags)??value.GetType().GetField("<"+name+">k__BackingField",flags);
                return name+"="+(field==null?"UNKNOWN_FIELD":Value(field.GetValue(value)));
            }));
            private static string WorldFacts(TrafficWorldSnapshot world)=>world==null?"NULL":Identity(world)+" version="+world.Version+" bodyVersion="+world.BodyVersion+
                "\nBODIES\n"+string.Join("\n",world.Bodies.Select(b=>Key(b.Owner)+" life="+Identity(b.ActorLife)+" pose="+b.Position.ToString("F9")+
                    " radius="+Value(b.Radius)+" safety="+Value(b.SafetyMargin)+" enabled="+b.Enabled+" actorActive="+b.ActorActive+" progressing="+b.IndependentlyProgressing+" seconds="+Value(b.RemainingSeconds)))+
                "\nPARKING\n"+string.Join("\n",world.Parking.Select(p=>Key(p.Owner)+" resource="+p.ResourceId+" canonical="+Identity(p.CanonicalOwner)+" ownerVersion="+p.OwnerVersion+
                    " business="+Identity(p.BusinessOwner)+" release="+Identity(p.ReleaseObligation)+" pose="+p.Position.ToString("F9")+" radius="+Value(p.Radius)+" safety="+Value(p.SafetyMargin)+
                    " checkpoint="+Identity(p.SemanticCheckpoint)+" captured=["+Members(p.SemanticCheckpoint,"CapturedOwnerVersion","Assignment","arrived","moving","needsReplan","ownExiting","preparationRevision","paid","advancing","yielding","collectionLease","hasCollectionLease","ownCollectionPosition")+
                    "] claimEntryNow=["+Members(Raw(p.SemanticCheckpoint,"entry"),"Id","Slot","Generation","Position","Target","Paid","Arrived","Moving","NeedsReplan","Advancing","Yielding")+
                    "] ownerNow=["+Members(p.CanonicalOwner,"Version","preparationRevision","exiting","collectionOwner","collectionPosition","generation","deliveryOwner","deliveryPosition")+"]"));
            private static string DecisionFacts(TrafficDecision decision)=>decision==null?"NULL":Identity(decision)+" "+Members(decision,"Intent","Kind","Reason","FailureDisposition","Permit","RequestSequence","AcquiredSequence")+
                " route="+Points(decision.Route)+" blockers=["+string.Join(";",decision.Blockers.Select(b=>Key(b.Owner)+"/"+b.ResourceId+"/"+b.Proof))+"]\nproofs="+
                string.Join("\n",decision.ProofRoutes.Select(p=>Identity(p)+" resource="+p.ResourceId+" waypoints="+Points(p.Waypoints)+" expanded="+Points(p.ExpandedPath)+" legEnds="+string.Join(",",p.LegEnds)));
            private void FirstFailureError(Exception error){if(firstFailureErrors.Count<4)firstFailureErrors.Add(error.GetType().Name+":"+error.Message);}
            private void CanonicalError(Exception error)
            {canonicalErrorCount++;if(canonicalErrors.Count<4)canonicalErrors.Add(error.GetType().Name+":"+error.Message);}
            private int CanonicalCounter(object owner,string name)
            {
                if(Raw(owner,name) is int value)return value;
                CanonicalError(new InvalidOperationException("UNKNOWN counter "+name));return -1;
            }
            private int CanonicalDelta(object owner,string name,int baseline)
            {
                var current=CanonicalCounter(owner,name);
                if(baseline<0 || current<baseline){CanonicalError(new InvalidOperationException("UNKNOWN counter delta "+name));return -1;}
                return current-baseline;
            }
            private void CaptureCanonicalOriginal(string message)
            {
                if(!firstFailureEnabled || firstFailureSampled || finished)return;
                try
                {
                    if(message==null)throw new InvalidOperationException("NULL canonical payload");
                    var marker=message.StartsWith("CANONICAL_ORDER_ORIGINAL ",StringComparison.Ordinal)?"laterKey=":
                        message.StartsWith("COLLECTED_EXIT_PREPARATION_ORIGINAL ",StringComparison.Ordinal)?"key=":null;
                    if(marker==null)throw new InvalidOperationException("UNKNOWN canonical payload prefix");
                    var start=message.IndexOf(marker,StringComparison.Ordinal);if(start<0)throw new InvalidOperationException("UNKNOWN canonical key");
                    start+=marker.Length;var end=message.IndexOf(' ',start);var key=end<0?message.Substring(start):message.Substring(start,end-start);
                    canonicalSequence++;
                    if(!canonicalKeys.Contains(key) && canonicalKeys.Count>=32){canonicalDropped++;return;}
                    var first=canonicalKeys.Add(key);
                    var firstBind=message.Contains(" stage=bind-computed") && message.Contains(" bound=True") && canonicalBoundKeys.Add(key);
                    if(canonicalHistory.Count(row=>row.Key==key)>=16)
                    {
                        var index=canonicalHistory.FindIndex(row=>row.Key==key&&!row.Pinned);
                        if(index<0){canonicalDropped++;return;}canonicalHistory.RemoveAt(index);canonicalDropped++;
                    }
                    if(canonicalHistory.Count>=256)
                    {
                        var index=canonicalHistory.FindIndex(row=>!row.Pinned);
                        if(index<0){canonicalDropped++;return;}canonicalHistory.RemoveAt(index);canonicalDropped++;
                    }
                    canonicalHistory.Add((key,"obs="+canonicalSequence+" frame="+Time.frameCount+" "+message,first||firstBind));
                }
                catch(Exception error){CanonicalError(error);}
            }
            private string CanonicalOriginalFacts()
            {
                var core=CanonicalDelta(firstFailureCore,"CanonicalOrderingObservationErrors",canonicalCoreBaseline);
                var cfc=CanonicalDelta(test.customers,"CollectedExitPreparationObservationErrors",canonicalCfcBaseline);
                var status=canonicalErrorCount!=0 || core!=0 || cfc!=0?"UNKNOWN_OBSERVER_ERROR":canonicalDropped!=0?"UNKNOWN_EVICTED_HISTORY":firstFailureSampled?"FROZEN_ORIGINAL_RECORDS":"NOT_PROVEN";
                return "status="+status+" observed="+canonicalSequence+" retained="+canonicalHistory.Count+" cap=256 perKeyCap=16 keyCap=32 keys="+canonicalKeys.Count+
                    " dropped="+canonicalDropped+" errors="+canonicalErrorCount+" errorDetails="+string.Join(";",canonicalErrors)+
                    " coreErrorsAtFreeze="+canonicalCoreFreeze+" cfcErrorsAtFreeze="+canonicalCfcFreeze+" coreErrorsFinal="+core+" cfcErrorsFinal="+cfc+
                    " freezeFrame="+canonicalFreezeFrame+" freezeKind="+canonicalFreezeKind+" firstAndFirstSuccessfulBindPinned=True unexecuted=NOT_EVALUATED\n"+
                    string.Join("\n",canonicalHistory.Select(row=>row.Text));
            }
            private static object AdmissionStored(object owner,string name)
            {
                const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly;
                for(var type=owner?.GetType();type!=null;type=type.BaseType)
                {
                    var field=type.GetField(name,flags)??type.GetField("<"+name+">k__BackingField",flags);
                    if(field!=null)return field.GetValue(owner);
                }
                return null;
            }
            private void EmptyAdmissionError(Exception error)
            {emptyAdmissionErrors++;if(emptyAdmissionErrorDetails.Count<4)emptyAdmissionErrorDetails.Add(error.GetType().Name+":"+error.Message);}
            private string EmptyAdmissionFacts(string label)
            {
                var flow=test.customers;var anchor=Raw(flow,"pendingAdmissionAnchor");var core=Raw(flow,"traffic");
                var key=AdmissionStored(anchor,"Key");var id=key is TrafficIntentKey actual?actual.ActorId:null;
                var entries=Raw(core,"entries") as IDictionary;var entry=key!=null && entries!=null && entries.Contains(key)?entries[key]:null;
                var request=AdmissionStored(entry,"Request");var snapshot=AdmissionStored(request,"Snapshot");
                var decision=AdmissionStored(entry,"Decision") as TrafficDecision;var preview=AdmissionStored(anchor,"Preview");
                var counter=Raw(flow,"queue");var capacity=AdmissionStored(flow,"Capacity");var clock=Raw(flow,"clock");
                var rows=Raw(counter,"entries") as IEnumerable;var ledger=Raw(capacity,"_records") as IEnumerable;
                var jobs=Raw(entry,"Jobs") as IList;var firstJob=jobs!=null && jobs.Count>0?jobs[0]:null;
                string Items(object value)=>value is IEnumerable items?"["+string.Join(";",items.Cast<object>().Select(Value))+"]":Value(value);
                string RowFields(object value)=>Identity(value)+" "+Members(value,"OwnerId","Kind","State","Token")+" token=["+Members(AdmissionStored(value,"Token"),"Id","Issuer")+"]";
                var matching=ledger==null?"UNKNOWN_LEDGER":string.Join(";",ledger.Cast<object>().Where(row=>id!=null && Equals(AdmissionStored(row,"OwnerId"),id)).Select(RowFields));
                var aliases=new List<string>();var keys=Raw(flow,"trafficKeys") as IDictionary;
                if(keys!=null)foreach(DictionaryEntry pair in keys)
                    if(pair.Value is TrafficIntentKey alias && alias.ActorId==id)aliases.Add(Value(pair.Key)+"="+Key(alias));
                var requirements=AdmissionStored(request,"Requirements") as IEnumerable;
                var prefabs=Raw(flow,"prefabs") as IEnumerable;
                var actors=Raw(test.world,"actors") as IEnumerable;
                var visits=Raw(flow,"visits") as IDictionary;var exits=Raw(flow,"exitSnapshots") as IList;
                return label+" frame="+Time.frameCount+" originalCompoundGuard=NOT_REPLAYED freshJoinPreviewLocal=NOT_CAPTURED freshReprepareRequestLocal=NOT_CAPTURED unityAndTransformGetters=NOT_CAPTURED\n"+
                    "CURRENT_FLOW "+Identity(flow)+" "+Members(flow,"session","nextVisit","trafficSession","trafficEpoch","autoSpawn","integratedMode","integratedAdmissionAllowed","admissionInProgress","disposed","admissionRejected","admissionRouteUnavailable","Status")+
                    " visits="+(visits==null?"UNKNOWN":visits.Count.ToString())+" exitSnapshots="+(exits==null?"UNKNOWN":exits.Count.ToString())+
                    "\nCURRENT_REFS "+Members(flow,"pendingAdmissionAnchor","traffic","queue","world","spawn","prefabs","adapter","Capacity")+
                    " adapterStored=["+Members(AdmissionStored(flow,"adapter"),"revision","Revision")+"] clock=["+Members(clock,"armed","RemainingSeconds")+"]"+
                    "\nANCHOR "+Identity(anchor)+" "+Members(anchor,"Coordinator","Key","Owner","Capacity","World","Prefab","Spawn","Session","Before","FirstFrame","Position","Rotation","Radius","Safety","Skin","Epsilon","Submitted","Preview")+
                    "\nANCHOR_PREFAB_SETTINGS "+Members(AdmissionStored(AdmissionStored(anchor,"Prefab"),"settings"),"AgentRadius","CollisionSkin","Epsilon")+
                    "\nCURRENT_PREFABS "+(prefabs==null?"UNKNOWN":string.Join(";",prefabs.Cast<object>().Select(prefab=>Identity(prefab)+" settings=["+Members(Raw(prefab,"settings"),"AgentRadius","CollisionSkin","Epsilon")+"]")))+
                    "\nSTORED_PREVIEW "+Identity(preview)+" "+Members(preview,"owner","Kind","Id","ExpectedVersion","Consumed","Assignment")+
                    "\nCORE "+Identity(core)+" "+Members(core,"session","epoch","layoutRevision","paused","closed","committing","queryFrame","QueriesThisFrame","queriesPerFrame","queriesPerIntent")+
                    "\nEXACT_ENTRY "+Identity(entry)+" "+Members(entry,"Key","BirthOrder","FirstFrame","RequestSequence","AcquiredSequence","Queries","WaitRemaining","FiniteLimit","Parking","Prepared","Queued","Active","Retired","Discarded","EverCanonicalAttempt","CurrentIndex","CandidateCount","JourneyPermit","JourneyRequest","PendingHandoff")+
                    "\nACTUAL_DECISION "+DecisionFacts(decision)+"\nSTORED_REQUEST "+Identity(request)+" "+Members(request,"Intent","RequestedFrame","Start","Target","ResourceId","Radius","SafetyMargin","NativeStamp","FiniteSeconds","PlanPrimaryRoute")+
                    " snapshot="+Identity(snapshot)+" "+Members(snapshot,"Version","BodyVersion")+
                    "\nSTORED_REQUIREMENTS "+(requirements==null?"UNKNOWN":string.Join(";",requirements.Cast<object>().Select(requirement=>Identity(requirement)+" "+Members(requirement,"ResourceId","Start","Goal","Clearance","SelectedJourney","AvoidProposedParking","Obstacles"))))+
                    "\nSTORED_WAIT owners="+Items(Raw(entry,"FrozenOwners"))+" blockers="+Items(Raw(entry,"WaitingBlockers"))+
                    " jobsCount="+(jobs==null?"UNKNOWN":jobs.Count.ToString())+" jobIndex="+Value(Raw(entry,"JobIndex"))+" firstJob="+Identity(firstJob)+" "+Members(firstJob,"start","goal","started","complete","ExpandedPath")+
                    "\nCOUNTER "+Identity(counter)+" "+Members(counter,"Version","preparationRevision","exiting","slots")+" entries=["+(rows==null?"UNKNOWN":string.Join(";",rows.Cast<object>().Select(row=>Members(row,"Id","Slot","Generation","Position","Arrived","NeedsReplan","Moving"))))+"]"+
                    "\nCAPACITY "+Identity(capacity)+" "+Members(capacity,"Limits","_nextTokenId")+" actualActorId="+(id??"UNKNOWN")+" records=["+matching+"]"+
                    "\nTRAFFIC_KEY_MEMOS "+(keys==null?"UNKNOWN":string.Join(";",aliases))+
                    "\nREGISTERED_ACTOR_FIELDS "+(actors==null?"UNKNOWN":string.Join(";",actors.Cast<object>().Select(actor=>Identity(actor)+" "+Members(actor,"actorId","runtimeId"))))+
                    "\nCURRENT_PUBLISHED_WORLD "+WorldFacts(Raw(flow,"trafficSnapshot") as TrafficWorldSnapshot);
            }
            private void CaptureEmptyAdmissionRefusal(string operation,string result)
            {
                if(!emptyAdmissionEnabled || emptyAdmissionAttempted || finished || operation!="admission.original-refusal" || result!="anchor-prepare")return;
                try
                {
                    var visits=Raw(test.customers,"visits") as IDictionary;var before=Raw(test.customers,"nextVisit");
                    if(visits==null || !(before is long count))throw new InvalidOperationException("UNKNOWN empty-admission qualifier fields");
                    if(visits.Count!=0 || count<=0)return;
                    emptyAdmissionAttempted=true;
                    emptyAdmissionRecord=EmptyAdmissionFacts("CAPTURED_AT_ORIGINAL_REFUSAL operation="+operation+" stage="+result);
                }
                catch(Exception error){emptyAdmissionAttempted=true;EmptyAdmissionError(error);}
            }
            private void CaptureAdmissionWaitFailure(string message)
            {
                if(!emptyAdmissionEnabled || admissionWaitAttempted || finished || message==null || !message.StartsWith("WAIT_ORIGINAL ",StringComparison.Ordinal))return;
                try
                {
                    var anchor=Raw(test.customers,"pendingAdmissionAnchor");
                    if(!(AdmissionStored(anchor,"Key") is TrafficIntentKey expected))return;
                    var start=message.IndexOf(" intent=",StringComparison.Ordinal);var stop=start<0?-1:message.IndexOf(" previous=",start,StringComparison.Ordinal);
                    if(start<0 || stop<0 || !string.Equals(message.Substring(start+8,stop-start-8),Key(expected),StringComparison.Ordinal))return;
                    admissionWaitAttempted=true;
                    if(firstFailureCore==null || !ReferenceEquals(firstFailureCore,Raw(test.customers,"traffic")) || !ReferenceEquals(firstFailureCore,Raw(test.owner,"traffic")))
                        throw new InvalidOperationException("Original admission shared Core changed");
                    var entries=Raw(firstFailureCore,"entries") as IDictionary;
                    if(entries==null || !entries.Contains(expected))throw new InvalidOperationException("Original admission entry missing");
                    var entry=entries[expected];var request=Raw(entry,"Request") as TrafficRequest;
                    if(request==null || !request.Intent.Equals(expected))throw new InvalidOperationException("Original admission request identity changed");
                    var ownerStart=message.IndexOf(" owner=",stop,StringComparison.Ordinal);
                    var ownerEnd=ownerStart<0?-1:message.IndexOf(" proof=",ownerStart,StringComparison.Ordinal);
                    var ownerText=ownerStart>=0 && ownerEnd>ownerStart?message.Substring(ownerStart+7,ownerEnd-ownerStart-7):"NOT_CAPTURED";
                    var ownerMatches=new List<string>();
                    foreach(DictionaryEntry pair in entries)
                        if(pair.Key is TrafficIntentKey owner && string.Equals(Key(owner),ownerText,StringComparison.Ordinal))ownerMatches.Add(Identity(pair.Value)+" key="+Key(owner));
                    admissionWaitRecord="CAPTURED_AT_ORIGINAL_WAIT_FAILURE beforeOriginalFail=True frame="+Time.frameCount+" key="+Key(expected)+
                        " currentBlockersLocal=NOT_CAPTURED policyQueries=NONE\nORIGINAL "+message+
                        "\nFIRST_FAILED_OWNER exact="+ownerText+" registeredMatches="+ownerMatches.Count+" ["+string.Join(";",ownerMatches)+"]"+
                        "\n"+EmptyAdmissionFacts("PRE_FAIL_PENDING_ANCHOR_CURRENT_FACTS")+
                        "\nPRE_FAIL_ACTUAL_CORE "+StoredCoreHandoffFacts(entry,null)+
                        "\nCORE_CURRENT_WORLD "+WorldFacts(Raw(firstFailureCore,"snapshot") as TrafficWorldSnapshot);
                }
                catch(Exception error){admissionWaitAttempted=true;EmptyAdmissionError(error);}
            }
            private void FinishEmptyAdmission(string reason)
            {
                if(!emptyAdmissionEnabled)return;
                string final;
                try{final=EmptyAdmissionFacts("CAPTURED_AT_FINAL currentFactsOnly=True");}
                catch(Exception error){EmptyAdmissionError(error);final="CAPTURED_AT_FINAL UNKNOWN_CAPTURE_ERROR";}
                try
                {
                    if(!ReferenceEquals(hook.GetValue(test.customers),prior))EmptyAdmissionError(new InvalidOperationException("Original CorridorObserver restore mismatch"));
                    var waitField=typeof(ServiceTrafficCoordinator).GetField("WaitFailureObserver",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                    var coreRestored=firstFailureCore!=null && waitField!=null && ReferenceEquals(waitField.GetValue(firstFailureCore),null);
                    if(!admissionWaitHookInstalled || firstFailureRestore.Count!=0 || firstFailureErrors.Count!=0 || !coreRestored)
                        EmptyAdmissionError(new InvalidOperationException("Original Core observer setup/restore UNKNOWN: installed="+admissionWaitHookInstalled+" remaining="+firstFailureRestore.Count+" restored="+coreRestored+" errors="+string.Join(";",firstFailureErrors)));
                    var status=emptyAdmissionErrors!=0?"UNKNOWN_OBSERVER_ERROR":emptyAdmissionRecord==null?"NOT_PROVEN":"OBSERVED_ORIGINAL_EMPTY_REFUSAL";
                    var path=Path.Combine(Path.GetDirectoryName(output),"empty-admission-anchor-witness.txt");
                    File.WriteAllText(path,"status="+status+" reason="+reason+" cap=1 attempted="+emptyAdmissionAttempted+" errors="+emptyAdmissionErrors+
                        " errorDetails="+string.Join(";",emptyAdmissionErrorDetails)+" observerRestored="+ReferenceEquals(hook.GetValue(test.customers),prior)+" gameAssertionsUnchanged=True\n"+
                        (emptyAdmissionRecord??"NO_QUALIFIED_ORIGINAL_REFUSAL")+
                        "\n\nADMISSION_ORIGINAL_WAIT status="+(emptyAdmissionErrors!=0?"UNKNOWN_OBSERVER_ERROR":admissionWaitRecord==null?"NOT_PROVEN":"OBSERVED_ORIGINAL_QUALIFIER")+
                        " attempted="+admissionWaitAttempted+" coreHookInstalled="+admissionWaitHookInstalled+" coreInstalledCount="+firstFailureInstalled+
                        " coreRestoreRemaining="+firstFailureRestore.Count+" coreRestored="+coreRestored+" coreSetupRestoreErrors="+string.Join(";",firstFailureErrors)+
                        "\n"+(admissionWaitRecord??"NO_MATCHING_ORIGINAL_ADMISSION_WAIT_FAILURE")+"\n\n"+final+"\n");
                    TestContext.WriteLine("EMPTY_ADMISSION_ANCHOR status="+status+" file="+path);
                }
                catch(Exception error){EmptyAdmissionError(error);TestContext.WriteLine("EMPTY_ADMISSION_ANCHOR status=UNKNOWN_FLUSH_ERROR "+error.GetType().Name+":"+error.Message);}
            }
            private void AttachFirstFailure()
            {
                try
                {
                    firstFailureCore=Raw(test.customers,"traffic") as ServiceTrafficCoordinator;
                    if(firstFailureCore==null || !ReferenceEquals(firstFailureCore,Raw(test.owner,"traffic")))throw new InvalidOperationException("Original shared Core unavailable");
                    void Attach(string name,Delegate callback)
                    {
                        var field=typeof(ServiceTrafficCoordinator).GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                        if(field==null)throw new InvalidOperationException("Missing original "+name);
                        var previous=field.GetValue(firstFailureCore);
                        if(previous!=null)throw new InvalidOperationException("Original "+name+" already has a subscriber");
                        firstFailureRestore.Add(()=>{field.SetValue(firstFailureCore,previous);if(!ReferenceEquals(field.GetValue(firstFailureCore),previous))throw new InvalidOperationException("Restore "+name);});
                        field.SetValue(firstFailureCore,callback);firstFailureInstalled++;
                    }
                    if(firstFailureEnabled)
                    {
                        Attach("CanonicalOrderingObserver",(Action<string>)CaptureCanonicalOriginal);
                        var field=typeof(CustomerFlowController).GetField("CollectedExitPreparationObserver",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                        if(field==null)throw new InvalidOperationException("Missing CollectedExitPreparationObserver");
                        var previous=field.GetValue(test.customers);if(previous!=null)throw new InvalidOperationException("CollectedExitPreparationObserver already subscribed");
                        firstFailureRestore.Add(()=>{field.SetValue(test.customers,previous);if(!ReferenceEquals(field.GetValue(test.customers),previous))throw new InvalidOperationException("Restore CollectedExitPreparationObserver");});
                        field.SetValue(test.customers,(Action<string>)CaptureCanonicalOriginal);firstFailureInstalled++;
                        canonicalCoreBaseline=CanonicalCounter(firstFailureCore,"CanonicalOrderingObservationErrors");
                        canonicalCfcBaseline=CanonicalCounter(test.customers,"CollectedExitPreparationObservationErrors");
                    }
                    if(firstFailureEnabled)Attach("PrepareLegRejectionObserver",(Action<string>)(message=>CaptureFirstFailure("prepare",message,null,null)));
                    Attach("WaitFailureObserver",(Action<string>)(message=>
                    {
                        if(message==null)return;
                        if(emptyAdmissionEnabled)CaptureAdmissionWaitFailure(message);
                        if(checkoutEnabled)CaptureCheckoutOriginal(message);
                        if(!firstFailureEnabled)return;
                        if(message.StartsWith("WAIT_ORIGINAL ",StringComparison.Ordinal))CaptureFirstFailure("wait",message,null,null);
                        // Original Fail observer runs before decision/path/dependent invalidation; no second policy call.
                        // Capture only the actual finite budget failure, not the later owner-no-longer-independent fanout.
                        else if(message.StartsWith("OWNER_INVALIDATION ownerIntent=actual-exit ",StringComparison.Ordinal) &&
                            message.Contains(" next=PlanningFailed reason=wait-budget-exhausted "))
                            CaptureFirstFailure("owner-budget",message,null,null);
                    }));
                    if(emptyAdmissionEnabled)admissionWaitHookInstalled=true;
                    if(firstFailureEnabled)Attach("SnapshotChangedObserver",(Action<TrafficRequest,TrafficWorldSnapshot>)((request,current)=>CaptureFirstFailure("snapshot","SNAPSHOT_CHANGED original branch",request,current)));
                }
                catch(Exception error){FirstFailureError(error);RestoreFirstFailure();}
            }
            private void CaptureFirstFailure(string kind,string original,TrafficRequest supplied,TrafficWorldSnapshot suppliedCurrent)
            {
                if(firstFailureSampled || finished)return;
                try
                {
                    if(!ReferenceEquals(firstFailureCore,Raw(test.customers,"traffic")) || !ReferenceEquals(firstFailureCore,Raw(test.owner,"traffic")))return;
                    var entries=(IDictionary)Raw(firstFailureCore,"entries");var visits=(IDictionary)Raw(test.customers,"visits");
                    object selectedEntry=null,selectedVisit=null;var selectedKey=default(TrafficIntentKey);var matches=0;
                    foreach(DictionaryEntry pair in entries)
                    {
                        var key=(TrafficIntentKey)pair.Key;var request=Raw(pair.Value,"Request") as TrafficRequest;
                        if(request==null || !visits.Contains(key.ActorId))continue;
                        var visit=visits[key.ActorId];
                        if(!Equals(Raw(visit,"HasCollected"),true) || !Equals(Raw(visit,"State"),CustomerVisitState.Exiting))continue;
                        if(kind=="snapshot"?!ReferenceEquals(request,supplied):kind=="wait"?!original.Contains(" intent="+Key(key)+" previous="):
                            kind=="owner-budget"?!original.StartsWith("OWNER_INVALIDATION ownerIntent=actual-exit owner="+Key(key)+" previous=",StringComparison.Ordinal):
                            !original.Contains(" actor="+key.ActorId+" intent="+key.IntentId+" phaseGen="+key.Generation+" context="+key.Context+" epoch="+key.Epoch+" revision="+key.LayoutRevision+" "))continue;
                        if(kind=="owner-budget" && (!Equals(Raw(pair.Value,"Key"),key) ||
                            !Equals(Raw(pair.Value,"Retired"),false) ||
                            !(Raw(firstFailureCore,"workEntries") is IEnumerable work) || !work.Cast<object>().Any(item=>ReferenceEquals(item,pair.Value))))continue;
                        selectedEntry=pair.Value;selectedVisit=visit;selectedKey=key;matches++;
                    }
                    if(matches!=1)return;
                    firstFailureSampled=true; // One attempt only, including a capture error; no continuous formatting.
                    canonicalFreezeFrame=Time.frameCount;canonicalFreezeKind=kind;
                    canonicalCoreFreeze=CanonicalDelta(firstFailureCore,"CanonicalOrderingObservationErrors",canonicalCoreBaseline);
                    canonicalCfcFreeze=CanonicalDelta(test.customers,"CollectedExitPreparationObservationErrors",canonicalCfcBaseline);
                    var oldRequest=(TrafficRequest)Raw(selectedEntry,"Request");var current=suppliedCurrent??(TrafficWorldSnapshot)Raw(firstFailureCore,"snapshot");
                    var move=Raw(selectedVisit,"TrafficMove");var leg=Raw(selectedEntry,"Leg");var journey=Raw(selectedEntry,"JourneyRequest") as TrafficRequest;
                    var service=Raw(test.world,"service");var nativeMap=Raw(service,"byActor") as IDictionary;
                    var native=nativeMap!=null && nativeMap.Contains(selectedKey.ActorId)?nativeMap[selectedKey.ActorId]:null;
                    var actor=Raw(selectedVisit,"Actor");var drivers=Raw(test.world,"drivers") as IDictionary;
                    var driver=actor!=null && drivers!=null && drivers.Contains(actor)?drivers[actor]:null;
                    firstFailureRecord="PHOTO_EXIT_FIRST_FAILURE kind="+kind+" frame="+Time.frameCount+" core="+Identity(firstFailureCore)+" key="+Key(selectedKey)+"\nORIGINAL "+original+
                        "\nENTRY "+Identity(selectedEntry)+" "+Members(selectedEntry,"FirstFrame","RequestSequence","AcquiredSequence","Queries","WaitRemaining","FiniteLimit","Parking","Prepared","Queued","Active","Retired","Discarded","CurrentPath","Journey")+
                        "\nREQUEST "+Identity(oldRequest)+" "+Members(oldRequest,"Intent","RequestedFrame","Start","Target","ResourceId","Radius","SafetyMargin","NativeStamp","FiniteSeconds","PlanPrimaryRoute")+
                        "\nDECISION "+DecisionFacts(Raw(selectedEntry,"Decision") as TrafficDecision)+"\nPERMIT "+DecisionFacts(Raw(selectedEntry,"JourneyPermit") as TrafficDecision)+
                        "\nLEG "+Identity(leg)+" "+Members(leg,"ActorLife","Proof","Index","Generation","Stamp","Committed","Bound","Completed","Suspended","ReprepareAllowed","NeedsQuery","Path","Suffix")+
                        "\nVISIT "+Identity(selectedVisit)+" "+Members(selectedVisit,"Id","Actor","State","ResumeState","HasCollected","RequestId","Generation","ExitSequence","TrafficMovePending","PendingTraffic","IssuedTraffic","PendingExitSegment","CurrentSegmentTarget","FinalTarget")+
                        "\nMOVE "+Identity(move)+" "+Members(move,"Coordinator","Intent","Resource","Index","NativeGeneration","Source","Proof")+"\nMOVE_SOURCE "+DecisionFacts(Raw(move,"Source") as TrafficDecision)+
                        "\nNATIVE_SERVICE "+Identity(service)+" request="+Identity(native)+" "+Members(native,"Id","ActorId","Generation","LayoutRevision","Original","Recovery","Recovering","RetryCount","PendingSeconds","SegmentSeconds","NoProgressSeconds","SegmentLimit","SegmentLimitSet","Driver")+
                        "\nDRIVER "+Identity(driver)+" "+Members(driver,"moving","corner","corners","rawCorners")+
                        "\nOLD_WORLD "+WorldFacts(oldRequest.Snapshot)+"\nCURRENT_WORLD "+WorldFacts(current)+"\nJOURNEY_WORLD "+WorldFacts(journey?.Snapshot);
                    if(kind=="owner-budget" || kind=="wait")firstFailureRecord+="\nPRE_FAIL_STORED_CORE "+StoredCoreHandoffFacts(selectedEntry,selectedVisit);
                }
                catch(Exception error){FirstFailureError(error);}
            }
            // One bounded pre-Fail dump. Reflection reads fields/backing fields only, never milestone/claim getters.
            // Snapshot/DTO getters below are immutable facts; no IsCurrent, ReadClaim, Nav, planning or policy query.
            private string StoredCoreHandoffFacts(object selectedEntry,object selectedVisit)
            {
                string Blocks(object value)=>value is IEnumerable values?string.Join(";",values.Cast<TrafficBlocker>().Select(block=>
                    Key(block.Owner)+"/resource="+block.ResourceId+"/proof="+block.Proof)):"NULL";
                string Owners(object value)=>value is IEnumerable values?string.Join(";",values.Cast<TrafficIntentKey>().Select(Key)):"NULL";
                string Resources(object value)
                {
                    if(!(value is IDictionary map))return "NULL";
                    var parts=new List<string>();foreach(var key in map.Keys)
                        parts.Add(Value(key)+"={"+string.Join(";",((IEnumerable)map[key]).Cast<object>().Select(Value))+"}");
                    return string.Join(";",parts);
                }
                string Claims(object value)=>value is IEnumerable values?string.Join("\n",values.Cast<object>().Select(claim=>Identity(claim)+" "+
                    Members(claim,"Owner","CanonicalOwner","OwnerVersion","ResourceId","Position","Radius","SafetyMargin","BusinessOwner","ReleaseObligation","SemanticCheckpoint"))):"NULL";
                string Proof(object value)=>value is TrafficRouteProof proof?Identity(proof)+" resource="+proof.ResourceId+" waypoints="+Points(proof.Waypoints)+
                    " expanded="+Points(proof.ExpandedPath)+" ends="+string.Join(",",proof.LegEnds):"NULL";
                string Handoff(object value)
                {
                    if(value==null)return "NULL";
                    var dependencies=Raw(value,"Dependencies") as IDictionary;var parts=new List<string>();
                    if(dependencies!=null)foreach(var entry in dependencies.Keys)
                        parts.Add("waiter="+Identity(entry)+" key="+Value(Raw(entry,"Key"))+" isSelectedEntry="+ReferenceEquals(entry,selectedEntry)+" resources={"+
                            string.Join(";",((IEnumerable)dependencies[entry]).Cast<object>().Select(Value))+"}");
                    return Identity(value)+" "+Members(value,"ActorLife","Remaining","Handle","PreparedSuccessor","PreparationRequest","PreparationSource","PreparationMilestone","InspectingPreparation","CommittingPreparedSuccessor")+" source="+DecisionFacts(Raw(value,"Source") as TrafficDecision)+
                        " preparationDescriptor=["+Members(Raw(value,"PreparationRequest"),"Intent","RequestedFrame","Start","Target","ResourceId","NativeStamp","Snapshot")+"]"+
                        " originalClaim=["+Claims(new[]{Raw(value,"OriginalClaim")})+"] originalClaims=["+Claims(Raw(value,"OriginalClaims"))+"] dependencies=["+string.Join(";",parts)+"]"+
                        " completedBodyWakeups=["+(Raw(value,"CompletedBodyWakeups") is IEnumerable wakeups?string.Join(";",wakeups.Cast<object>().Select(entry=>Identity(entry)+" key="+Value(Raw(entry,"Key"))+" isSelectedEntry="+ReferenceEquals(entry,selectedEntry))):"UNKNOWN")+"]";
                }
                string Entry(object value)
                {
                    var leg=Raw(value,"Leg");var request=Raw(value,"Request") as TrafficRequest;
                    var jobs=Raw(value,"Jobs") as IList;var firstJob=jobs!=null && jobs.Count>0?jobs[0]:null;
                    return "ENTRY "+Identity(value)+" "+Members(value,"Key","BirthOrder","FirstFrame","RequestSequence","AcquiredSequence","Queries","WaitRemaining","FiniteLimit",
                        "Parking","Prepared","Queued","Active","Retired","Discarded","CurrentPath","Journey")+
                        "\nDECISION "+DecisionFacts(Raw(value,"Decision") as TrafficDecision)+"\nPERMIT "+DecisionFacts(Raw(value,"JourneyPermit") as TrafficDecision)+
                        "\nREQUEST "+Identity(request)+" "+Members(request,"Intent","RequestedFrame","Start","Target","ResourceId","Radius","SafetyMargin","NativeStamp","FiniteSeconds")+
                        " snapshot="+Identity(request?.Snapshot)+" "+Members(request?.Snapshot,"Version","BodyVersion")+
                        "\nWAITING ["+Blocks(Raw(value,"WaitingBlockers"))+"] FROZEN_OWNERS ["+Owners(Raw(value,"FrozenOwners"))+"] FROZEN_RESOURCES ["+Resources(Raw(value,"FrozenResources"))+"]"+
                        "\nLEG "+Identity(leg)+" "+Members(leg,"ActorLife","Index","Generation","Stamp","Committed","Bound","Completed","Suspended","ReprepareAllowed","NeedsQuery","Path","Suffix")+
                        " proof="+Proof(Raw(leg,"Proof"))+"\nJOBS count="+(jobs==null?"UNKNOWN":jobs.Count.ToString())+" index="+Value(Raw(value,"JobIndex"))+" first="+Identity(firstJob)+
                        " "+Members(firstJob,"start","goal","started","complete","current","next","ExpandedPath","Waypoints","LegEnds")+
                        "\nPENDING_HANDOFF "+Handoff(Raw(value,"PendingHandoff"));
                }
                string Receipt(object value)
                {
                    if(value==null)return "NULL";
                    var bindings=Raw(value,"Claims") as IEnumerable;
                    return Identity(value)+" "+Members(value,"Flow","Visit","Admission","Actor","Coordinator","Intent","SavedSnapshot","Total","NativeGeneration","Marked","Handle")+
                        " source="+DecisionFacts(Raw(value,"Source") as TrafficDecision)+"\nBINDINGS ["+(bindings==null?"NULL":string.Join("\n",bindings.Cast<object>().Select(binding=>
                            Identity(binding)+" "+Members(binding,"Kind","Counter","CounterEntry","Pickup","PickupEntry","Token")+" fact=["+Claims(new[]{Raw(binding,"Fact")})+"]")))+"]";
                }
                var entries=(IDictionary)Raw(firstFailureCore,"entries");var liveWork=Raw(firstFailureCore,"workEntries") as IEnumerable;
                var workObjects=liveWork==null?Array.Empty<object>():liveWork.Cast<object>().ToArray();var lines=new List<string>{
                    "selectedEntry="+Identity(selectedEntry)+" registered="+(entries.Contains(Raw(selectedEntry,"Key")) && ReferenceEquals(entries[Raw(selectedEntry,"Key")],selectedEntry))+
                    " inActualWork="+workObjects.Any(value=>ReferenceEquals(value,selectedEntry))+" actualWorkCount="+workObjects.Length+
                    " milestoneRuntimeGetters=NOT_EVALUATED policyQueries=NONE phaseStateIsStoredOnly=True"};
                foreach(var key in entries.Keys)
                {
                    var entry=entries[key];lines.Add(Entry(entry));
                    var visits=Raw(test.customers,"visits") as IDictionary;var actorId=((TrafficIntentKey)key).ActorId;
                    if(visits!=null && visits.Contains(actorId))
                    {
                        var visit=visits[actorId];lines.Add("BUSINESS_VISIT "+Identity(visit)+" "+Members(visit,"Id","State","ResumeState","HasCollected","RequestId","Generation","CounterArrived","PickupAssigned","PickupArrived","ServiceArrived","TrafficJourney","TrafficMovePending","PendingExitSegment")+
                            "\nSTORED_PENDING_RECEIPT "+Receipt(Raw(visit,"PendingTraffic"))+"\nSTORED_ISSUED_RECEIPT "+Receipt(Raw(visit,"IssuedTraffic")));
                    }
                }
                return string.Join("\n",lines);
            }

            // Stored actual unpaid FIFO head only. No IsCurrent, ReadClaim, business call, Nav getter or policy query.
            private bool ReadCheckoutHead(string expected,out object visit,out object anchor,out object cursor,out object entry,out TrafficIntentKey key)
            {
                visit=anchor=cursor=entry=null;key=default;
                if(firstFailureCore==null || !ReferenceEquals(firstFailureCore,Raw(test.customers,"traffic")) ||
                    !ReferenceEquals(firstFailureCore,Raw(test.owner,"traffic")))return false;
                var rows=Raw(Raw(test.customers,"queue"),"entries") as IList;
                if(rows==null || rows.Count==0)return false;
                var row=rows[0];var id=Raw(row,"Id") as string;
                if(id==null || expected!=null && id!=expected || !Equals(Raw(row,"Slot"),0) || !Equals(Raw(row,"Arrived"),true))return false;
                var visits=Raw(test.customers,"visits") as IDictionary;
                var service=Raw(test.owner,"service");var paid=Raw(service,"paid") as IDictionary;
                if(visits==null || !visits.Contains(id) || paid==null || paid.Contains(id))return false;
                visit=visits[id];
                if(!Equals(Raw(visit,"State"),CustomerVisitState.Queued) || !Equals(Raw(visit,"CounterArrived"),true) ||
                    !Equals(Raw(visit,"HasCollected"),false))return false;
                anchor=Raw(visit,"PendingReservationAnchor");var candidates=Raw(test.customers,"trafficCandidates") as IDictionary;
                var boxed=Raw(anchor,"Key");
                if(boxed is TrafficIntentKey captured)key=captured;
                else if(candidates!=null)
                {
                    var found=new List<object>();foreach(var candidate in candidates.Keys)
                        if(candidate is TrafficIntentKey candidateKey && candidateKey.ActorId==id &&
                            Convert.ToString(Raw(candidates[candidate],"Role"))=="Reservation")found.Add(candidate);
                    if(found.Count==1)key=(TrafficIntentKey)found[0];
                    else if(found.Count>1)throw new InvalidOperationException("Ambiguous stored reservation cursor for actual unpaid head");
                }
                if(candidates!=null && candidates.Contains(key))cursor=candidates[key];
                var entries=Raw(firstFailureCore,"entries") as IDictionary;
                if(entries!=null && entries.Contains(key))entry=entries[key];
                return true;
            }
            private void CaptureCheckoutOriginal(string message)
            {
                if(checkoutTerminal!=null)return;
                try
                {
                    if(!ReadCheckoutHead(null,out _,out _,out _,out var entry,out var key) || entry==null || !Equals(Raw(entry,"Key"),key))return;
                    Dictionary<TrafficIntentKey,string> sink=null;
                    if(message.StartsWith("WAIT_ORIGINAL ",StringComparison.Ordinal) && message.Contains(" intent="+Key(key)+" previous="))sink=checkoutWaitFailures;
                    else if(message.StartsWith("OWNER_INVALIDATION ",StringComparison.Ordinal) && message.Contains("DEPENDENT key="+Key(key)+" decision="))sink=checkoutTransitions;
                    if(sink==null || sink.ContainsKey(key))return;
                    if(sink.Count==16){checkoutDropped++;return;}
                    sink.Add(key,"ORIGINAL_BEFORE_CHANGE frame="+Time.frameCount+" reservationKey="+Key(key)+" entry="+Identity(entry)+"\n"+message);
                }
                catch(Exception error){CheckoutError(error);}
            }
            private void CaptureCheckoutState(string operation,string caller,string result)
            {
                if(checkoutTerminal!=null)return;
                try
                {
                    if(!ReadCheckoutHead(caller,out var visit,out var anchor,out var cursor,out var entry,out var key))return;
                    var decision=Raw(entry,"Decision") as TrafficDecision;var pending=Raw(visit,"PendingTraffic");
                    var predecessor=Raw(pending,"Intent");var entries=Raw(firstFailureCore,"entries") as IDictionary;
                    var previous=predecessor is TrafficIntentKey predecessorKey && entries!=null && entries.Contains(predecessorKey)?entries[predecessorKey]:null;
                    var handoff=Raw(previous,"PendingHandoff");
                    var signature=Key(key)+"/"+Identity(anchor)+"/"+Identity(cursor)+"/"+Identity(decision)+"/"+
                        Members(entry,"Queued","Prepared","Active","Retired")+"/"+Identity(handoff)+"/"+
                        Identity(Raw(previous,"Decision"))+"/"+Members(Raw(previous,"Decision"),"Kind","Reason","FailureDisposition");
                    if(signature!=checkoutLastState)
                    {
                        checkoutLastState=signature;checkoutChanges++;
                        if(checkoutStates.Count==32){checkoutStates.Dequeue();checkoutDropped++;}
                        checkoutStates.Enqueue("HEAD_STATE frame="+Time.frameCount+" operation="+operation+" originalResult="+result+" key="+Key(key)+
                            "\nVISIT "+Identity(visit)+" "+Members(visit,"Id","State","CounterArrived","PickupAssigned","HasCollected","RequestId","Generation","Assignment","TrafficPhaseSequence")+
                            "\nANCHOR "+Identity(anchor)+" "+Members(anchor,"Key","FirstFrame","Submitted","Generation","Cursor","Phase","Start","SideIndex","Counter","CounterClaim","Owner","Preview")+
                            "\nCURSOR "+Identity(cursor)+" "+Members(cursor,"Key","Role","ActorLife","Origin","CurrentIndex","AttemptedIndices","StaticInvalidIndices")+
                            "\nCURSOR_SOURCE "+DecisionFacts(Raw(cursor,"Source") as TrafficDecision)+"\nCORE_SOURCE "+DecisionFacts(decision)+
                            "\nENTRY "+Identity(entry)+" "+Members(entry,"Key","FirstFrame","Queries","WaitRemaining","FiniteLimit","RequestSequence","AcquiredSequence","EverCanonicalAttempt","Parking","Queued","Prepared","Active","Retired","Leg","Journey")+
                            "\nPENDING_RECEIPT "+Identity(pending)+" "+Members(pending,"Intent","Marked","Handle","Actor","Admission","Total","Source")+
                            "\nPREDECESSOR "+Identity(previous)+" "+DecisionFacts(Raw(previous,"Decision") as TrafficDecision)+" handoff="+Identity(handoff)+" "+Members(handoff,"ActorLife","Remaining","Handle"));
                    }
                    if(decision==null || decision.FailureDisposition!=TrafficFailureDisposition.Terminal || entry==null || !Equals(Raw(entry,"Key"),key))return;
                    checkoutTerminal="FIRST_TERMINAL_RESERVATION_OBSERVED frame="+Time.frameCount+" key="+Key(key)+" entry="+Identity(entry)+
                        "\nNOTE source read at original reserve call; causal callback below is independent original-before-change evidence, never inferred from global HUD.\n"+
                        (checkoutWaitFailures.TryGetValue(key,out var wait)?wait:"ORIGINAL_WAIT_FAILURE NOT_OBSERVED")+"\n"+
                        (checkoutTransitions.TryGetValue(key,out var transition)?transition:"MATCHING_OWNER_TRANSITION NOT_OBSERVED")+
                        "\nTERMINAL_STORED_CORE "+StoredCoreHandoffFacts(entry,visit)+"\nCURRENT_WORLD "+WorldFacts((TrafficWorldSnapshot)Raw(firstFailureCore,"snapshot"));
                }
                catch(Exception error){CheckoutError(error);}
            }
            private void FinishCheckout(string reason)
            {
                if(!checkoutEnabled)return;
                try
                {
                    if(!ReferenceEquals(hook.GetValue(test.customers),prior) || firstFailureRestore.Count!=0 || firstFailureErrors.Count!=0)
                        throw new InvalidOperationException("Original observer restore was not verified");
                    var status=checkoutErrors.Count!=0?"UNKNOWN_OBSERVER_ERROR":checkoutTerminal==null?"NOT_PROVEN":"OBSERVED_TERMINAL_SOURCE";
                    var path=Path.Combine(Path.GetDirectoryName(output),"checkout-first-refusal.txt");
                    File.WriteAllText(path,"status="+status+" reason="+reason+" stateChanges="+checkoutChanges+" dropped="+checkoutDropped+
                        " errors="+string.Join(";",checkoutErrors)+" wrappersAndGameAssertionsUnchanged=True\n"+string.Join("\n\n",checkoutStates)+"\n"+(checkoutTerminal??"NO_QUALIFIED_TERMINAL_RESERVATION_RECEIPT")+"\n");
                    TestContext.WriteLine("CHECKOUT_FIRST_REFUSAL status="+status+" file="+path);
                }
                catch(Exception error){CheckoutError(error);TestContext.WriteLine("CHECKOUT_FIRST_REFUSAL status=UNKNOWN_FLUSH_ERROR "+error.GetType().Name+":"+error.Message);}
            }

            private void RestoreFirstFailure()
            {
                for(var i=firstFailureRestore.Count-1;i>=0;i--)try{firstFailureRestore[i]();}catch(Exception error){FirstFailureError(error);}
                firstFailureRestore.Clear();
            }
            private void FinishFirstFailure(string reason)
            {
                RestoreFirstFailure();
                if(!firstFailureEnabled)return;
                try {if(!ReferenceEquals(hook.GetValue(test.customers),prior))throw new InvalidOperationException("Corridor restore mismatch");}
                catch(Exception error){FirstFailureError(error);}
                var status=firstFailureErrors.Count!=0?"UNKNOWN_OBSERVER_ERROR":firstFailureSampled?"OBSERVED":"NOT_PROVEN";
                try
                {
                    var path=Path.Combine(Path.GetDirectoryName(output),"photo-exit-first-failure.txt");
                    File.WriteAllText(path,"status="+status+" reason="+reason+" core="+Identity(firstFailureCore)+" customerCoreNow="+Identity(Raw(test.customers,"traffic"))+" ownerCoreNow="+Identity(Raw(test.owner,"traffic"))+
                        " installed="+firstFailureInstalled+" remainingRestores="+firstFailureRestore.Count+
                        " errors="+string.Join(";",firstFailureErrors)+"\n"+(firstFailureRecord??"NO_QUALIFIED_RECEIPT")+
                        "\nCANONICAL_ORDERING_HISTORY "+CanonicalOriginalFacts()+"\n");
                    TestContext.WriteLine("PHOTO_EXIT_FIRST_FAILURE status="+status+" file="+path+" errors="+firstFailureErrors.Count);
                }
                catch(Exception error){FirstFailureError(error);TestContext.WriteLine("PHOTO_EXIT_FIRST_FAILURE status=UNKNOWN_FLUSH_ERROR "+error.GetType().Name+":"+error.Message);}
            }
            private static object Raw(object owner,string name)=>owner?.GetType().GetField(name,
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public)?.GetValue(owner);
            private static string Points(IEnumerable<Vector3> values)=>values==null?"NULL":"["+string.Join(";",values.Select(p=>p.ToString("F9")))+"]";
            private string StoredVisits()
            {
                var visits=(IDictionary)Raw(test.customers,"visits");
                return string.Join("\n",visits.Values.Cast<object>().Select(v=>
                {
                    var actor=(NavigationActor)Raw(v,"Actor");var assignment=(QueueAssignment)Raw(v,"Assignment");
                    return "visit="+Raw(v,"Id")+" state="+Raw(v,"State")+" req="+Raw(v,"RequestId")+" gen="+Raw(v,"Generation")+
                        " collected="+Raw(v,"HasCollected")+" exitSeq="+Raw(v,"ExitSequence")+
                        " position="+(actor==null?"NULL":actor.transform.position.ToString("F9"))+
                        " assignment="+assignment.Position.ToString("F9")+" slot="+assignment.SlotIndex+
                        " pickupAssigned="+Raw(v,"PickupAssigned")+" pickupInTransit="+Raw(v,"PickupInTransit")+
                        " pendingExit="+Raw(v,"PendingExitSegment")+" pendingPickup="+Raw(v,"PendingPickupSegment")+
                        " current="+((NavigationTarget)Raw(v,"CurrentSegmentTarget")).Position.ToString("F9")+
                        " final="+((NavigationTarget)Raw(v,"FinalTarget")).Position.ToString("F9")+
                        " route="+Points(Raw(v,"Route") as IEnumerable<Vector3>);
                }))+"\nbodies="+string.Join(";",test.world.RegisteredActors.Where(a=>a!=null)
                    .Select(a=>a.ActorId+"@"+a.transform.position.ToString("F9")));
            }
            public OriginalExitDecisionTrace(CafePickupSideTests value,string directory)
            {
                test=value;output=Path.Combine(directory,"original-exit-decisions.txt");
                hook=typeof(CustomerFlowController).GetField("CorridorObserver",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                prior=(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)hook.GetValue(test.customers);
                Assert.That(prior,Is.Null,"Dedicated minimal observer must not carry a general trace.");
                hook.SetValue(test.customers,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)Observe);
                try
                {
                    if(firstFailureEnabled || checkoutEnabled || emptyAdmissionEnabled)AttachFirstFailure();
                    Flush("attached");
                    TestContext.WriteLine("ORIGINAL_EXIT_DECISIONS "+output);
                }
                catch { RestoreFirstFailure();hook.SetValue(test.customers,prior);throw; }
            }
            private void Observe(string operation,string caller,string related,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] occupied,string result)
            {
                prior?.Invoke(operation,caller,related,target,path,occupied,result);
                CaptureEmptyAdmissionRefusal(operation,result);
                if(emptyAdmissionEnabled && !firstFailureEnabled && !checkoutEnabled)return;
                if(checkoutEnabled && !finished && (operation=="reserve.attempt" || operation=="reserve.traffic-decision"))
                    CaptureCheckoutState(operation,caller,result);
                if(checkoutEnabled && !firstFailureEnabled)return;
                if(frozen||finished||caller==null)return;
                var planning=operation=="plan.exit-direct"||operation=="plan.exit-boundary-leg"||operation=="plan.exit-boundary-detour-leg";
                if(!planning&&operation!="exit.actual-dispatch"&&operation!="route.actual-plan"&&operation!="route.blocked"&&
                    operation!="collection.before-acquire"&&operation!="collection.acquire-result"&&
                    operation!="wait.candidate"&&operation!="wait.independent"&&operation!="wait.departing"&&operation!="wait.without-finite-bodies")return;
                if(planning&&(result==null||!new[]{"send.actual","pending.actual","wait.counterfactual"}
                    .Any(purpose=>result.Contains("purpose="+purpose+" "))))return;
                try
                {
                    if(!records.TryGetValue(caller,out var ring))records[caller]=ring=new Queue<string>();
                    if(ring.Count==256){ring.Dequeue();evicted++;}
                    var snapshot=operation=="exit.actual-dispatch"||operation=="route.blocked"||operation=="collection.before-acquire"||
                        operation=="route.actual-plan"&&path==null;
                    ring.Enqueue("sequence="+(++sequence)+" frame="+Time.frameCount+" gameTime="+Time.time.ToString("R")+
                        " caller="+caller+" operation="+operation+" related="+related+" target="+target.ToString("F9")+
                        " result="+result+" path="+Points(path)+" obstacles="+Points(occupied)+(snapshot?"\n"+StoredVisits():""));
                    if(operation=="route.blocked"){frozen=true;Flush("first-route-blocked");}
                }
                catch(Exception error){errors++;File.AppendAllText(output,"OBSERVER_ERROR "+error+"\n");}
            }
            private void Flush(string reason)=>File.WriteAllText(output,"Original calls only; no re-query. reason="+reason+
                " observations="+sequence+" evicted="+evicted+" errors="+errors+" frozen="+frozen+"\n"+
                string.Join("\n\n",records.Select(pair=>"CALLER "+pair.Key+"\n"+string.Join("\n",pair.Value)))+"\n");
            public void Finish(string reason)
            {
                if(finished)return;finished=true;
                try {hook.SetValue(test.customers,prior);}finally {FinishFirstFailure(reason);FinishCheckout(reason);FinishEmptyAdmission(reason);}
                Flush(reason);
            }
        }

        // Bounded failure-only sink: no general trace hook, native getters or planner queries.
        // 只保存拒绝当时的cold witness；finally先detach，再写文件。
        private sealed class SegmentFailureTrace
        {
            private readonly CafePickupSideTests test;
            private readonly System.Reflection.FieldInfo hook;
            private readonly List<string> records=new List<string>();
            public IReadOnlyList<string> Records=>records;
            private int dropped;
            private const System.Reflection.BindingFlags StoredFields=System.Reflection.BindingFlags.Instance|
                System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic;
            private static object Raw(object value,string name)=>value?.GetType().GetField(name,StoredFields)?.GetValue(value);
            private static string Stored(object value,string name)
            {
                if(value==null) return "UNAVAILABLE_OWNER_NULL";
                var field=value.GetType().GetField(name,StoredFields);
                if(field==null) return "UNAVAILABLE_FIELD";
                var item=field.GetValue(value);
                if(item==null) return "NULL";
                if(item is NavigationTarget target) return target.Position.ToString("F9");
                if(item is NavigationTarget[] route) return "["+string.Join(";",route.Select(t=>t.Position.ToString("F9")))+"]";
                return item.ToString();
            }
            private string RunnerContext(object employee)
            {
                var runner=Raw(employee,"Runner"); var leg=Raw(runner,"moveLeg");
                var id=(string)Raw(employee,"Id");
                var requests=CafeLoopRuntimeProof.Field<IDictionary>(test.world.Service,"byActor");
                var request=id!=null && requests.Contains(id)?requests[id]:null;
                return "runnerActor="+id+" runner="+(runner==null?"NULL":"PRESENT")+
                    " taskGeneration="+Stored(runner,"generation")+" taskLayout="+Stored(runner,"layoutRevision")+
                    " deliveryRoute="+Stored(runner,"deliveryRoute")+" deliveryRouteIndex="+Stored(runner,"deliveryRouteIndex")+
                    " pendingDeliveryTraffic="+Stored(runner,"pendingDeliveryTraffic")+" pendingReturnTraffic="+Stored(runner,"pendingReturnTraffic")+
                    " selectedDeliveryTarget="+Stored(runner,"selectedDeliveryTarget")+" moveLeg="+(leg==null?"NULL":"PRESENT")+
                    " moveLegRequest="+Stored(leg,"RequestId")+" moveLegGeneration="+Stored(leg,"Generation")+
                    " moveLegLayout="+Stored(leg,"LayoutRevision")+" moveLegState="+Stored(leg,"State")+" moveLegCompleted="+Stored(leg,"Completed")+
                    " serviceRequest="+(request==null?"NULL":Stored(request,"Id"))+" serviceOriginalTarget="+Stored(request,"Original")+
                    " serviceGeneration="+Stored(request,"Generation")+" serviceLayout="+Stored(request,"LayoutRevision");
            }
            public SegmentFailureTrace(CafePickupSideTests value)
            {
                test=value;
                hook=typeof(CustomerFlowController).GetField("SegmentFailureObserver",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                Assert.That(hook.GetValue(test.customers),Is.Null,"Dedicated witness has one owner");
                hook.SetValue(test.customers,(Action<string>)Record);
            }
            private void Record(string record)
            {
                if(records.Count>=64) { dropped++; return; }
                records.Add(record+"\nstaff="+string.Join(";",test.employees.Snapshot.Select(e=>e.ActorId+" role="+e.Role+" state="+e.State+
                    " handle="+e.HandleId+" failure="+e.Failure))+"\nrunners=\n"+string.Join("\n",CafeLoopRuntimeProof.Field<IList>(test.employees,"employees").Cast<object>().Select(RunnerContext))+
                    "\ncapacity="+string.Join(";",test.customers.Capacity.GetReservations()
                    .Select(r=>r.OwnerId+" kind="+r.Kind+" token="+r.Token+" state="+r.State)));
            }
            public void Finish()
            {
                hook.SetValue(test.customers,null);
                var directory=Path.Combine("outputs","phase15","segment-failure-witness","capture-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                Directory.CreateDirectory(directory);
                var path=Path.Combine(directory,TestContext.CurrentContext.Test.Name+"-failure-witness.txt");
                File.WriteAllText(path,"Failure-only original-decision witness; final snapshots are not prior callback chronology.\nrecords="+records.Count+
                    " dropped="+dropped+"\n"+string.Join("\n\n",records));
                TestContext.WriteLine("SEGMENT_FAILURE_WITNESS "+path+" records="+records.Count+" dropped="+dropped);
            }
        }

        // Bounded read-only trace. No DesiredVelocity/Observe/AdvanceCorner calls.
        // 读取真实字段和原Step结果，不制造request、lease或完成回执。
        private sealed class CorridorTrace
        {
            private readonly CafePickupSideTests test;
            private readonly List<string> events = new List<string>();
            private readonly List<string> motion = new List<string>();
            private readonly Dictionary<PickupQueueService,int> owners = new Dictionary<PickupQueueService,int>();
            private readonly Dictionary<string,string> lastDiagnostic=new Dictionary<string,string>();
            private readonly Dictionary<string,string> lastAssignment=new Dictionary<string,string>();
            private readonly bool frontTrace=Environment.GetCommandLineArgs().Contains("-unreservedFrontTrace");
            private readonly bool stallTrace=Environment.GetCommandLineArgs().Contains("-stallObservationTrace");
            private readonly Dictionary<string,Queue<string>> preFailure=new Dictionary<string,Queue<string>>();
            private readonly HashSet<string> frozenIdentities=new HashSet<string>();
            private readonly Dictionary<string,string> semanticState=new Dictionary<string,string>();
            private readonly Dictionary<string,string> observedRequestIdentities=new Dictionary<string,string>();
            private long ringEvicted, ringObserved, semanticSuppressed, semanticObserved;
            private int ringDropped;
            public bool StallCustomerSeen { get; private set; }
            public bool StallDriverSeen { get; private set; }
            public bool StallIdleEmployeeSeen { get; private set; }
            public int StallDropped => ringDropped+eventOverflow;

            private readonly HashSet<string> firstFrontDecisions=new HashSet<string>();
            private readonly HashSet<string> firstMovementLosses=new HashSet<string>();
            private long observationSequence;
            private int repeatedObservations;
            private long selectedRequest;
            private string selectedActor;
            private int eventOverflow, motionOverflow;
            public string FirstReservationConflict { get; private set; }
            public int ServiceDeferrals { get; private set; }
            private readonly string directory;
            private static readonly System.Reflection.BindingFlags Fields = System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            private static object Read(object value,string field) => value.GetType().GetField(field,Fields).GetValue(value);
            private static string P(Vector3 value) => value.ToString("F9");
            private static string PathText(IEnumerable<Vector3> value) => value==null?"NULL":string.Join(";",value.Select(P));
            private int Owner(PickupQueueService value)
            {
                if(value==null) return 0;
                if(!owners.TryGetValue(value,out var id)) owners.Add(value,id=owners.Count+1);
                return id;
            }
            public CorridorTrace(CafePickupSideTests value,bool forceStall=false)
            {
                test=value;
                if(forceStall) stallTrace=true;
                directory=Path.Combine("outputs","phase15",stallTrace?"stall-observation":frontTrace?"unreserved-front-admission":"corridor-navigation","trace-"+
                    DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+TestContext.CurrentContext.Test.Name);
                if(stallTrace)
                {
                    typeof(NavMeshMovementDriver).GetField("PathObserver",Fields|System.Reflection.BindingFlags.Static).SetValue(null,
                        new Action<NavigationActor,string,string>(DriverDecision));
                    typeof(EmployeeFlowController).GetField("MovementObserver",Fields).SetValue(test.employees,
                        new Action<string,string,string>(EmployeeDecision));
                }
                typeof(NavigationWorld).GetField("StepObserver",Fields).SetValue(test.world,
                    (Action<NavigationActor,Vector3,Vector3,Vector3,Vector3,Vector3,Vector3,Vector3,float>)Step);
                typeof(CustomerFlowController).GetField("PickupObserver",Fields).SetValue(test.customers,
                    (Action<string,string,PickupQueueService,QueueAssignment>)Pickup);
                typeof(CustomerFlowController).GetField("CorridorObserver",Fields).SetValue(test.customers,
                    (Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)Corridor);
            }
            private void Pickup(string operation,string id,PickupQueueService owner,QueueAssignment assignment)
                => Record(operation,id,owner,assignment,null,assignment.Position,null,null,null);
            private void Corridor(string operation,string id,string related,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                var entries=(IDictionary)Read(test.customers,"visits");var visit=id!=null&&entries.Contains(id)?entries[id]:null;
                Record(operation,id,visit==null?null:(PickupQueueService)Read(visit,"PickupOwner"),
                    visit==null?default:(QueueAssignment)Read(visit,"Assignment"),related,target,path,obstacles,result);
            }
            private void Record(string operation,string id,PickupQueueService owner,QueueAssignment assignment,string related,
                Vector3 target,IReadOnlyList<Vector3> observedPath,Vector3[] observedObstacles,string decision)
            {
                if(stallTrace)
                {
                    StallDecision("customer",operation,id,"related="+related+" owner="+Owner(owner)+" assignment="+assignment.Generation+
                        " target="+P(target)+" decision="+decision+" path="+PathText(observedPath)+" obstacles="+PathText(observedObstacles)+
                        " obstacleOwners="+(observedObstacles==null?"NOT_EVALUATED":string.Join(";",observedObstacles.Select(v=>P(v)+":"+PointOwners(v)))),
                        operation+"/"+related+"/"+Owner(owner)+"/"+assignment.Generation+"/"+P(target)+"/"+decision,
                        operation=="route.blocked"||operation=="segment.callback-result"&&
                            (decision.Contains("status=Failed")||decision.Contains("status=Recovered")));
                    return;
                }
                var sequence=++observationSequence;
                if(operation=="reserve.deferred-service") ServiceDeferrals++;
                // Bounded first-decision receipts per real visit, not a full chronology.
                // Keep all real admissions and each first refusal/acceptance even in long waits.
                if(frontTrace)
                {
                    var relevant=operation=="admission.published" || operation=="admission.front-refusal-observed" ||
                        operation=="admission.unreserved-front-held" || operation=="reserve.result" ||
                        operation=="reserve.deferred-service" || operation=="reserved.route" ||
                        operation=="reserve.accepted" || operation=="visit.removed" || operation=="route.blocked" ||
                        operation=="segment.callback" && decision!="Arrived";
                    if(!relevant || !firstFrontDecisions.Add(operation+"/"+id+"/"+decision)) return;
                }
                if(events.Count>=12000) { eventOverflow++;return; }
                var entries=(IDictionary)Read(test.customers,"visits");
                var subject=id!=null&&entries.Contains(id)?entries[id]:null;
                var geometry=Geometry(subject,observedPath,observedObstacles,target);
                var state=string.Join(";",entries.Values.Cast<object>().Select(v=>Read(v,"Id")+":"+Read(v,"State")+":"+Read(v,"Generation")+":"+
                    Read(v,"RequestId")+":"+Read(v,"PickupAssigned")+":"+Read(v,"PickupInTransit")+":"+Read(v,"OwnedRouteRevision")+":"+Read(v,"ReservedPickupRevision")));
                var repeatKey=operation+"/"+id+"/"+related;
                var signature=decision+"/"+P(target)+"/"+state+"/"+geometry.Split('|')[0]+"/"+PathText(observedPath);
                var repeatable=operation.EndsWith(".binding")||operation=="reserve.attempt"||operation=="reserve.result"||
                    operation=="target.before-check"||operation.StartsWith("target.owned-")||operation.StartsWith("admission.")&&operation!="admission.published";
                if(repeatable&&lastDiagnostic.TryGetValue(repeatKey,out var previous)&&previous==signature){repeatedObservations++;return;}
                lastDiagnostic[repeatKey]=signature;
                var lines=new List<string>{"event="+events.Count+" sequence="+sequence+" frame="+Time.frameCount+" time="+Time.time.ToString("F6")+
                    " operation="+operation+" visit="+id+" owner="+Owner(owner)+" generation="+assignment.Generation+
                    " target="+P(target)+" layout="+test.adapter.Revision+" related="+related+" decision="+decision};
                if(observedPath!=null||observedObstacles!=null) lines.Add(" OBSERVED path="+PathText(observedPath)+" obstacles="+PathText(observedObstacles)+" "+geometry);
                lines.Add(" canonical epoch="+test.owner.Snapshot.Epoch+" paid="+string.Join(";",test.owner.Snapshot.PaidVisitIds)+" orders="+
                    string.Join(";",test.owner.Snapshot.Orders.Select(p=>p.Value.OrderId+"/"+p.Value.CustomerId+"/"+p.Value.State))+
                    " tokens="+string.Join(";",test.customers.Capacity.GetReservations().Select(r=>r.OwnerId+":"+r.Token.Id+":"+r.Kind+":"+r.State)));
                var current=(PickupQueueService)Read(test.customers,"pickupQueue");
                foreach(var actualOwner in entries.Values.Cast<object>().Select(v=>(PickupQueueService)Read(v,"PickupOwner")).Concat(new[]{current,owner}).Where(q=>q!=null).Distinct())
                {
                    var reservations=(IDictionary)Read(actualOwner,"reservations");
                    var tracked=entries.Values.Cast<object>().Count(v=>(bool)Read(v,"PickupAssigned")&&ReferenceEquals(Read(v,"PickupOwner"),actualOwner));
                    var slots=(Vector3[])Read(actualOwner,"waitSlots");
                    lines.Add(" owner="+Owner(actualOwner)+" trueCount="+reservations.Count+" trackedCount="+tracked+" canReserve="+actualOwner.CanReserve+
                        " nextTracked="+tracked+" candidate="+(tracked<slots.Length?P(slots[tracked]):"OUTSIDE")+" currentQueue="+ReferenceEquals(actualOwner,current)+
                        " leases="+string.Join(";",actualOwner.ServiceLeases.Select(l=>l.Key+"@"+P(l.Value))));
                    foreach(DictionaryEntry pair in reservations)
                        lines.Add(" reservation="+pair.Key+" owner="+Owner(actualOwner)+" generation="+Read(pair.Value,"Generation")+" target="+P((Vector3)Read(pair.Value,"Target"))+
                            " paid="+Read(pair.Value,"Paid")+" slot="+Read(pair.Value,"Slot"));
                }
                foreach(DictionaryEntry pair in entries)
                {
                    var visit=pair.Value;var actor=(NavigationActor)Read(visit,"Actor");
                    var pickup=(PickupQueueService)Read(visit,"PickupOwner");
                    var route=(Queue<Vector3>)Read(visit,"Route");
                    var a=(QueueAssignment)Read(visit,"Assignment");
                    lines.Add(" live="+pair.Key+" state="+Read(visit,"State")+" resume="+Read(visit,"ResumeState")+
                        " owner="+Owner(pickup)+" request="+Read(visit,"RequestId")+" generation="+Read(visit,"Generation")+
                        " pose="+(actor==null?"NULL":P(actor.transform.position))+" assignment="+a.Generation+"@"+P(a.Position)+
                        " lease="+(pickup!=null&&pickup.TryGetServiceLease((string)pair.Key,out var lease)?P(lease):"NONE")+
                        " pending="+Read(visit,"PendingPickupSegment")+" segment="+P(((NavigationTarget)Read(visit,"CurrentSegmentTarget")).Position)+
                        " route="+PathText(route));
                    var reserved=(QueueAssignment?)Read(visit,"ReservedPickup");
                    var owned=(QueueAssignment)Read(visit,"OwnedRouteAssignment");
                    lines.Add(" certificate="+pair.Key+" reserved="+(reserved.HasValue?reserved.Value.Generation+"@"+P(reserved.Value.Position):"NONE")+
                        " reservedOwner="+Owner((PickupQueueService)Read(visit,"ReservedPickupOwner"))+" reservedLayout="+Read(visit,"ReservedPickupRevision")+
                        " reservedPath="+PathText((IReadOnlyList<Vector3>)Read(visit,"ReservedPickupPath"))+
                        " ownedOwner="+Owner((PickupQueueService)Read(visit,"OwnedRouteOwner"))+" ownedPhase="+Read(visit,"OwnedRoutePhase")+
                        " ownedAssignment="+owned.Generation+"@"+P(owned.Position)+" ownedLayout="+Read(visit,"OwnedRouteRevision")+
                        " ownedSegments="+Read(visit,"OwnedRouteSegments")+" actualSegments="+Read(visit,"Segments")+
                        " ownedRouteSourceSame="+ReferenceEquals(Read(visit,"OwnedRouteSource"),route)+
                        " ownedPath="+PathText((IReadOnlyList<Vector3>)Read(visit,"OwnedRoutePath"))+
                        " bindingReason="+BindingReason(visit)+" reservedBindingReason="+ReservedBindingReason(visit));
                    if(operation.EndsWith(".published")&&(string)pair.Key==id)
                        lines.Add(" PUBLICATION previousObserved="+(lastAssignment.TryGetValue(id,out var oldAssignment)?oldAssignment:"NONE")+" new="+a.Generation+"@"+P(a.Position));
                    lastAssignment[(string)pair.Key]=a.Generation+"@"+P(a.Position);
                    if(operation.EndsWith(".published")&&(string)pair.Key!=id&&actor!=null)
                    {
                        var request=(long)Read(visit,"RequestId");
                        var path=(IReadOnlyList<Vector3>)Read(visit,"OwnedRoutePath");
                        var clearance=(float)typeof(CustomerFlowController).GetMethod("RouteClearance",Fields).Invoke(test.customers,new object[]{actor.Settings});
                        var obstacles=(Vector3[])typeof(CustomerFlowController).GetMethod("CorridorObstacles",Fields)
                            .Invoke(test.customers,new[]{visit});
                        lines.Add(" PUBLICATION_OTHER_OWNED ownerVisit="+pair.Key+" binding="+BindingReason(visit)+" "+Geometry(visit,path,obstacles,target));
                        if(path!=null&&path.Count>1&&!CustomerRoutePlanner.IsClear(path,new[]{target},clearance))
                        {
                            var conflict="event="+events.Count+" later="+id+" target="+P(target)+" earlier="+pair.Key+
                                " request="+request+" owner="+Owner(pickup)+" route="+PathText(path)+" binding="+BindingReason(visit);
                            if(FirstReservationConflict==null) FirstReservationConflict=conflict;
                            lines.Add("PUBLISHED_FULL_CACHE_INTERSECTION_NOT_AUTOMATIC_AUTHORITY "+conflict);
                        }
                    }
                }
                lines.Add(" bodies="+string.Join(";",test.world.RegisteredActors.Where(a=>a!=null).Select(a=>a.ActorId+"@"+P(a.transform.position))));
                if(subject!=null&&(operation=="collection.before-acquire"||operation=="collection.acquire-result"))
                {
                    var actor=(NavigationActor)Read(subject,"Actor");var goal=((Transform)Read(test.customers,"exit")).position;
                    var obstacles=(Vector3[])typeof(CustomerFlowController).GetMethod("CorridorObstacles",Fields).Invoke(test.customers,new[]{subject});
                    var query=(IReadOnlyList<Vector3>)typeof(CustomerFlowController).GetMethod("PlanPickupExit",Fields).Invoke(test.customers,new object[]{target,goal,obstacles,actor.Settings});
                    lines.Add(" DIAGNOSTIC_ONLY_side_exit policy=all-current-bodies-formal-targets-unpaid-reservations-active-leases-except-self actualPose="+P(actor.transform.position)+
                        " queriedSide="+P(target)+" exit="+P(goal)+" path="+PathText(query)+" obstacles="+PathText(obstacles)+" NOT_MOVEMENT_CERTIFICATE=True");
                    var bodies=test.world.RegisteredActors.Where(a=>a!=null&&a!=actor).Select(a=>a.transform.position).ToArray();
                    var bodyQuery=(IReadOnlyList<Vector3>)typeof(CustomerFlowController).GetMethod("PlanPickupExit",Fields).Invoke(test.customers,new object[]{target,goal,bodies,actor.Settings});
                    lines.Add(" DIAGNOSTIC_ONLY_side_exit policy=actual-bodies-only-control actualPose="+P(actor.transform.position)+" queriedSide="+P(target)+
                        " path="+PathText(bodyQuery)+" obstacles="+PathText(bodies)+" NOT_MOVEMENT_CERTIFICATE=True");
                }
                events.Add(string.Join("\n",lines));
            }
            private string BindingReason(object visit)
            {
                if(Read(visit,"OwnedRoutePath")==null)return "no-owned-cache";
                var actor=(NavigationActor)Read(visit,"Actor");if(actor==null||!actor.isActiveAndEnabled)return "actor-inactive";
                if(!Read(visit,"State").Equals(Read(visit,"OwnedRoutePhase")))return "phase-changed";
                if((int)Read(visit,"OwnedRouteRevision")!=test.adapter.Revision)return "layout-changed";
                if(!ReferenceEquals(Read(visit,"OwnedRouteOwner"),Read(visit,"PickupOwner")))return "owner-changed";
                var a=(QueueAssignment)Read(visit,"Assignment");var cached=(QueueAssignment)Read(visit,"OwnedRouteAssignment");
                if(a.Generation!=cached.Generation||a.Position!=cached.Position)return "assignment-changed";
                var phase=(CustomerVisitState)Read(visit,"State");
                if(phase==CustomerVisitState.Exiting)return (bool)Read(visit,"HasCollected")?"valid-exit":"not-collected";
                if(!(bool)Read(visit,"PickupAssigned"))return "not-assigned";
                if(phase==CustomerVisitState.MovingToPickupService)
                {
                    var owner=(PickupQueueService)Read(visit,"PickupOwner");
                    if(owner==null||!owner.ServiceLeases.TryGetValue((string)Read(visit,"Id"),out var lease)||lease!=cached.Position)return "service-lease-changed";
                }
                return "binding-fields-match";
            }
            private string ReservedBindingReason(object visit)
            {
                if(!(bool)Read(visit,"PickupAssigned"))return "not-assigned";
                if((bool)Read(visit,"PickupInTransit")||(bool)Read(visit,"HasCollected"))return "transferred-or-collected";
                var actor=(NavigationActor)Read(visit,"Actor");if(actor==null||!actor.isActiveAndEnabled)return "actor-inactive";
                if((CustomerVisitState)Read(visit,"State")==CustomerVisitState.Blocked)return "blocked";
                if(!ReferenceEquals(Read(visit,"ReservedPickupOwner"),Read(visit,"PickupOwner")))return "owner-changed";
                var assignment=(QueueAssignment?)Read(visit,"ReservedPickup");if(!assignment.HasValue)return "no-assignment-cache";
                if((int)Read(visit,"ReservedPickupRevision")!=test.adapter.Revision)return "layout-changed";
                if(Read(visit,"ReservedPickupPath")==null)return "no-path-cache";
                var reservations=(IDictionary)Read(Read(visit,"PickupOwner"),"reservations");var id=(string)Read(visit,"Id");
                if(!reservations.Contains(id))return "LIVE_ENTRY_MISSING";
                var entry=reservations[id];
                return (int)Read(entry,"Generation")==assignment.Value.Generation&&(Vector3)Read(entry,"Target")==assignment.Value.Position?
                    "live-entry-matches":"LIVE_ENTRY_GENERATION_OR_TARGET_MISMATCH";
            }
            private string PointOwners(Vector3 point)
            {
                var names=test.world.RegisteredActors.Where(a=>a!=null&&(a.transform.position-point).sqrMagnitude<1e-8f).Select(a=>"body:"+a.ActorId).ToList();
                var entries=(IDictionary)Read(test.customers,"visits");
                foreach(DictionaryEntry pair in entries)
                {
                    var a=(QueueAssignment)Read(pair.Value,"Assignment");if((a.Position-point).sqrMagnitude<1e-8f)names.Add("formal:"+pair.Key+":"+Read(pair.Value,"State"));
                    var reserved=(QueueAssignment?)Read(pair.Value,"ReservedPickup");if(reserved.HasValue&&(reserved.Value.Position-point).sqrMagnitude<1e-8f)names.Add("reserved:"+pair.Key);
                }
                foreach(var owner in entries.Values.Cast<object>().Select(v=>(PickupQueueService)Read(v,"PickupOwner")).Where(q=>q!=null).Distinct())
                    foreach(var lease in owner.ServiceLeases)if((lease.Value-point).sqrMagnitude<1e-8f)names.Add("lease:"+lease.Key);
                return string.Join(",",names);
            }
            private string Geometry(object visit,IReadOnlyList<Vector3> path,Vector3[] obstacles,Vector3 target)
            {
                if(visit==null||path==null||path.Count<2)return "geometry=unavailable";
                var actor=(NavigationActor)Read(visit,"Actor");if(actor==null)return "geometry=no-actor";
                var clearance=(float)typeof(CustomerFlowController).GetMethod("RouteClearance",Fields).Invoke(test.customers,new object[]{actor.Settings});
                var pose=actor.transform.position;var best=0;var distance=float.PositiveInfinity;var projected=path[0];
                for(var i=1;i<path.Count;i++)
                {
                    var step=path[i]-path[i-1];step.y=0;var offset=pose-path[i-1];offset.y=0;
                    var t=step.sqrMagnitude==0?0:Mathf.Clamp01(Vector3.Dot(offset,step)/step.sqrMagnitude);
                    var point=path[i-1]+step*t;var delta=point-pose;delta.y=0;
                    if(delta.sqrMagnitude<distance){distance=delta.sqrMagnitude;best=i-1;projected=point;}
                }
                var suffix=new[]{pose}.Concat(path.Skip(best+1)).ToArray();var first="NONE";
                if(obstacles!=null)
                    for(var i=1;i<path.Count&&first=="NONE";i++)foreach(var obstacle in obstacles)
                        if(!CustomerRoutePlanner.IsClear(new[]{path[i-1],path[i]},new[]{obstacle},clearance))
                        {first="segment:"+(i-1)+" owners:"+PointOwners(obstacle)+" beforeProjectionSegment:"+(i-1<best);break;}
                var suffixClear=obstacles==null||CustomerRoutePlanner.IsClear(suffix,obstacles,clearance);
                var targetOnSuffix=!CustomerRoutePlanner.IsClear(suffix,new[]{target},clearance);
                return "firstBlocker="+first+" closestSegment="+best+" diagnosticSuffixClear="+suffixClear+" targetOnDiagnosticSuffix="+targetOnSuffix+
                    " | DIAGNOSTIC_GEOMETRIC_PROJECTION_NOT_CERTIFICATE closest="+P(projected)+" horizontalError="+Mathf.Sqrt(distance).ToString("R")+
                    " suffix="+PathText(suffix);
            }
            private void Step(NavigationActor actor,Vector3 start,Vector3 velocity,Vector3 cornerDelta,
                Vector3 dynamicDelta,Vector3 staticDelta,Vector3 navDelta,Vector3 finalDelta,float dt)
            {
                if(stallTrace)
                {
                    StallStep(actor,start,velocity,cornerDelta,dynamicDelta,staticDelta,navDelta,finalDelta,dt);
                    return;
                }
                if(frontTrace)
                {
                    // Read only the deltas already computed by NavigationWorld. No second
                    // DesiredVelocity, corner, avoidance, clamp or movement evaluation.
                    var visits=(IDictionary)Read(test.customers,"visits");
                    var requestsByActor=(IDictionary)Read(test.world.Service,"byActor");
                    if(!visits.Contains(actor.ActorId) || !requestsByActor.Contains(actor.ActorId) || (velocity*dt).sqrMagnitude<1e-12f) return;
                    var native=requestsByActor[actor.ActorId];var requestIdNow=(long)Read(native,"Id");
                    var layer=cornerDelta.sqrMagnitude<1e-12f?"corner":dynamicDelta.sqrMagnitude<1e-12f?"dynamic":
                        staticDelta.sqrMagnitude<1e-12f?"static":navDelta.sqrMagnitude<1e-12f?"nav":
                        finalDelta.sqrMagnitude<1e-12f?"sweep":finalDelta.sqrMagnitude<(velocity*dt).sqrMagnitude*.01f?"reduced":"none";
                    if(layer=="none" || !firstMovementLosses.Add(actor.ActorId+"/"+requestIdNow+"/"+layer)) return;
                    if(motion.Count>=4096){motionOverflow++;return;}
                    var visit=visits[actor.ActorId];var nativeDriver=Read(native,"Driver");
                    var assignmentNow=(QueueAssignment)Read(visit,"Assignment");
                    motion.Add("FIRST_COMPUTED_LOSS sample="+motion.Count+" frame="+Time.frameCount+" time="+Time.time.ToString("F6")+
                        " actor="+actor.ActorId+" phase="+Read(visit,"State")+" request="+requestIdNow+" generation="+Read(visit,"Generation")+
                        " assignment="+assignmentNow.Generation+"@"+P(assignmentNow.Position)+" layer="+layer+" start="+P(start)+" velocity="+P(velocity)+" dt="+dt.ToString("R")+
                        " corner="+P(cornerDelta)+" dynamic="+P(dynamicDelta)+" static="+P(staticDelta)+" nav="+P(navDelta)+" sweep="+P(finalDelta)+
                        " accepted="+P(actor.transform.position)+" target="+P(((NavigationTarget)Read(visit,"CurrentSegmentTarget")).Position)+
                        " finalTarget="+P(((NavigationTarget)Read(visit,"FinalTarget")).Position)+" owned="+PathText((IReadOnlyList<Vector3>)Read(visit,"OwnedRoutePath"))+
                        " unissued="+PathText((Queue<Vector3>)Read(visit,"Route"))+" noProgress="+Read(native,"NoProgressSeconds")+
                        " cornerIndex="+Read(nativeDriver,"corner")+" raw="+PathText((Vector3[])Read(nativeDriver,"rawCorners"))+
                        " corrected="+PathText((Vector3[])Read(nativeDriver,"corners"))+
                        " bodies="+string.Join(";",test.world.RegisteredActors.Select(a=>a.ActorId+"@"+P(a.transform.position))));
                    return;
                }
                if(selectedActor!=null&&actor.ActorId!=selectedActor) return;
                if(selectedActor==null&&(start.x> -3.45f||Mathf.Abs(start.z)>.1f||
                    (velocity*dt).sqrMagnitude<1e-10f||finalDelta.sqrMagnitude>1e-12f)) return;
                var requests=(IDictionary)Read(test.world.Service,"byActor");
                if(!requests.Contains(actor.ActorId)) return;
                var request=requests[actor.ActorId];var requestId=(long)Read(request,"Id");
                if(selectedActor==null)
                {
                    if(!((IDictionary)Read(test.customers,"visits")).Contains(actor.ActorId)) return;
                    selectedActor=actor.ActorId;selectedRequest=requestId;
                }
                if(requestId!=selectedRequest) return;
                if(motion.Count>=4096) { motionOverflow++;return; }
                var driver=Read(request,"Driver");
                if(motion.Count==0&&staticDelta.sqrMagnitude>0&&navDelta.sqrMagnitude==0&&actor.transform.position==start&&
                    Environment.GetCommandLineArgs().Contains("-boundaryQuery"))
                    motion.Add(BoundaryQueries(actor,driver,staticDelta));
                motion.Add("sample="+motion.Count+" frame="+Time.frameCount+" time="+Time.time.ToString("F6")+
                    " actor="+actor.ActorId+" request="+requestId+" driverGeneration="+Read(driver,"generation")+
                    " revision="+Read(driver,"revision")+" requestGeneration="+Read(request,"Generation")+
                    " noProgress="+Read(request,"NoProgressSeconds")+" segmentSeconds="+Read(request,"SegmentSeconds")+
                    " retry="+Read(request,"RetryCount")+" moving="+Read(driver,"moving")+" cornerIndex="+Read(driver,"corner")+
                    " start="+P(start)+" velocity="+P(velocity)+" dt="+dt.ToString("R")+
                    " corner="+P(cornerDelta)+" dynamic="+P(dynamicDelta)+" static="+P(staticDelta)+
                    " nav="+P(navDelta)+" sweep="+P(finalDelta)+" accepted="+P(actor.transform.position)+
                    " destination="+P((Vector3)Read(driver,"destination"))+
                    " raw="+PathText((Vector3[])Read(driver,"rawCorners"))+" corrected="+PathText((Vector3[])Read(driver,"corners"))+
                    " adjusted="+string.Join(",",(bool[])Read(driver,"adjustedCorners")));
            }
            // Passive snapshot only: reflection reads stored fields, never Observe/DesiredVelocity/planners.
            private string ActorContext(string id)
            {
                if(id==null) return "actor=UNASSIGNED caller=in-adjacent-semantic-events";
                var visits=(IDictionary)Read(test.customers,"visits");
                var requests=(IDictionary)Read(test.world.Service,"byActor");
                var actor=test.world.RegisteredActors.FirstOrDefault(a=>a!=null&&a.ActorId==id);
                var result="actor="+id+" layout="+test.adapter.Revision;
                if(visits.Contains(id))
                {
                    var v=visits[id];var a=(QueueAssignment)Read(v,"Assignment");
                    result+=" phase="+Read(v,"State")+" visitGeneration="+Read(v,"Generation")+" visitRequest="+Read(v,"RequestId")+
                        " collected="+Read(v,"HasCollected")+" segments="+Read(v,"Segments")+" replans="+Read(v,"Replans")+
                        " pendingExit="+Read(v,"PendingExitSegment")+" pendingPickup="+Read(v,"PendingPickupSegment")+
                        " assignment="+a.Generation+"@"+P(a.Position)+" owner="+Owner((PickupQueueService)Read(v,"PickupOwner"))+
                        " segment="+P(((NavigationTarget)Read(v,"CurrentSegmentTarget")).Position)+
                        " final="+P(((NavigationTarget)Read(v,"FinalTarget")).Position)+
                        " route="+PathText((Queue<Vector3>)Read(v,"Route"))+" owned="+PathText((IReadOnlyList<Vector3>)Read(v,"OwnedRoutePath"))+
                        " ownedSegments="+Read(v,"OwnedRouteSegments")+" ownedIssuedRequest="+Read(v,"OwnedIssuedRequest");
                }
                else
                {
                    var staff=((IList)Read(test.employees,"employees")).Cast<object>().FirstOrDefault(e=>(string)Read(e,"Id")==id);
                    if(staff!=null)
                    {
                        var runner=Read(staff,"Runner"); var snap=test.employees.Snapshot.First(e=>e.ActorId==id);
                        result+=" phase="+snap.State+" handle="+snap.HandleId+" taskGeneration="+(runner==null?"NULL":Read(runner,"generation"));
                    }
                }
                if(requests.Contains(id))
                {
                    var r=requests[id];var d=Read(r,"Driver");
                    result+=" request="+Read(r,"Id")+" pathGeneration="+Read(r,"Generation")+" requestLayout="+Read(r,"LayoutRevision")+
                        " original="+P(((NavigationTarget)Read(r,"Original")).Position)+" noProgress="+Read(r,"NoProgressSeconds")+
                        " retry="+Read(r,"RetryCount")+" cornerIndex="+Read(d,"corner")+" resolved="+P((Vector3)Read(d,"destination"))+
                        " raw="+PathText((Vector3[])Read(d,"rawCorners"))+" current="+PathText((Vector3[])Read(d,"corners"))+
                        " adjusted="+string.Join(",",(bool[])Read(d,"adjustedCorners"));
                }
                else result+=" request=0 serviceRequest=ABSENT";
                if(actor!=null) result+=" pose="+P(actor.transform.position)+" settings="+JsonUtility.ToJson(actor.Settings);
                return result;
            }
            private static string RetentionIdentity(string id,long request,string phase,int visitGeneration,long handle,
                int? pathGeneration,int? requestLayout,int? taskGeneration,int layout)
                => id+"/request="+request+"/phase="+phase+"/visitGeneration="+(visitGeneration>=0?visitGeneration.ToString():"NA")+
                    "/handle="+handle+"/pathGeneration="+(pathGeneration?.ToString()??"UNAVAILABLE_NO_SERVICE_REQUEST")+
                    "/requestLayout="+(requestLayout?.ToString()??"UNAVAILABLE_NO_SERVICE_REQUEST")+
                    "/taskGeneration="+(taskGeneration?.ToString()??"UNAVAILABLE_OR_NOT_EMPLOYEE")+"/layout="+layout;
            private string Identity(string id)
            {
                if(id==null)return "UNASSIGNED";
                var requests=(IDictionary)Read(test.world.Service,"byActor");
                var request=requests.Contains(id)?requests[id]:null;
                var requestId=request==null?0:(long)Read(request,"Id");
                var pathGeneration=request==null?(int?)null:(int)Read(request,"Generation");
                var requestLayout=request==null?(int?)null:(int)Read(request,"LayoutRevision");
                var visits=(IDictionary)Read(test.customers,"visits");
                if(visits.Contains(id)) return RememberRequestIdentity(id,requestId,RetentionIdentity(id,requestId,Read(visits[id],"State").ToString(),
                    (int)Read(visits[id],"Generation"),0,pathGeneration,requestLayout,null,test.adapter.Revision));
                var staff=((IList)Read(test.employees,"employees")).Cast<object>().FirstOrDefault(e=>(string)Read(e,"Id")==id);
                var snapshot=test.employees.Snapshot.FirstOrDefault(e=>e.ActorId==id);
                var runner=staff==null?null:Read(staff,"Runner");
                return RememberRequestIdentity(id,requestId,RetentionIdentity(id,requestId,snapshot.State.ToString(),-1,snapshot.HandleId,pathGeneration,requestLayout,
                    runner==null?(int?)null:(int)Read(runner,"generation"),test.adapter.Revision));
            }
            private string RememberRequestIdentity(string id,long request,string identity)
            {
                // Read from the real registered request, never create or borrow ownership.
                if(request!=0) observedRequestIdentities[id+"/"+request]=identity;
                return identity;
            }
            private string EventIdentity(string id,string data)
            {
                var current=Identity(id);
                // Finish removes byActor before callback. Join its actual result id to the
                // last observed registered generation; request0 planning never borrows it.
                var marker=data.IndexOf("request=",StringComparison.Ordinal);
                if(id==null||marker<0) return current;
                var start=marker+8;var end=start;
                while(end<data.Length&&char.IsDigit(data[end]))end++;
                if(!long.TryParse(data.Substring(start,end-start),out var request)||request==0)return current;
                return observedRequestIdentities.TryGetValue(id+"/"+request,out var identity)?identity:
                    current+"/receiptRequest="+request+"/receiptGeneration=UNKNOWN_NOT_OBSERVED";
            }
            public int PlannerReceiptCount(string actor) => events.Count(e=>e.Contains("operation=plan.exit-direct actor="+actor+" "));
            public bool PlannerPurposesSeen => events.Any(e=>e.Contains("purpose=acceptance.actual-query"))&&events.Any(e=>e.Contains("purpose=acceptance.counterfactual-query"));
            public int SyntheticRingCount => preFailure.Keys.Count(k=>k.StartsWith("SYNTHETIC-actor/"));
            public int SyntheticLayerTransitions => events.Count(e=>e.Contains("source=SYNTHETIC_METADATA operation=computed-layer"));
            public int SyntheticFrozenCount => frozenIdentities.Count;
            public void SyntheticSample(int generation,int layout,int taskGeneration,string layer)
            {
                const string id="SYNTHETIC-actor";
                var key=RetentionIdentity(id,700,"CarryingToPickUp",-1,8,generation,layout,taskGeneration,layout);
                if(frozenIdentities.Contains(key))return;
                RetainSample(key,"SYNTHETIC_METADATA_ONLY pathGeneration="+generation+" layout="+layout+" taskGeneration="+taskGeneration);
                StallDecision("SYNTHETIC_METADATA","computed-layer",id,"SYNTHETIC_METADATA_ONLY",layer,false,key);
            }
            public void SyntheticCallback(int generation,int layout,int taskGeneration,string status)
            {
                const string id="SYNTHETIC-actor";
                var key=RetentionIdentity(id,700,"CarryingToPickUp",-1,8,generation,layout,taskGeneration,layout);
                var data="SYNTHETIC_METADATA_ONLY status="+status;
                StallDecision("SYNTHETIC_METADATA","callback",id,data,data,EmployeeTerminal("callback",data),key);
            }
            private static bool EmployeeTerminal(string operation,string data)
                => operation.Contains("refused")||operation=="callback"&&data.Contains("status=Failed");
            private void DriverDecision(NavigationActor actor,string operation,string data)
                => StallDecision("driver",operation,actor.ActorId,data,operation+"/"+data,false);
            private void EmployeeDecision(string actor,string operation,string data)
                => StallDecision("employee",operation,actor,data,operation+"/"+data,
                    EmployeeTerminal(operation,data));
            private void StallDecision(string source,string operation,string id,string data,string semantic,bool terminal,string explicitIdentity=null)
            {
                if(source=="customer") StallCustomerSeen=true;
                if(source=="driver") StallDriverSeen=true;
                var sequence=++semanticObserved;
                var key=source+"/"+operation+"/"+id;
                var identity=explicitIdentity??EventIdentity(id,data);
                var signature=identity+"/"+semantic;
                if(!terminal&&semanticState.TryGetValue(key,out var previous)&&previous==signature)
                { semanticSuppressed++; return; }
                semanticState[key]=signature;
                if(events.Count>=80000) { eventOverflow++; return; }
                var owners=((IDictionary)Read(test.customers,"visits")).Values.Cast<object>()
                    .Select(v=>(PickupQueueService)Read(v,"PickupOwner")).Concat(new[]{(PickupQueueService)Read(test.customers,"pickupQueue")}).Where(q=>q!=null).Distinct();
                events.Add("sequence="+sequence+" frame="+Time.frameCount+" time="+Time.time.ToString("F6")+" source="+source+" operation="+operation+
                    " "+ActorContext(id)+" identity="+identity+"\n ORIGINAL "+data+"\n bodies="+
                    string.Join(";",test.world.RegisteredActors.Where(a=>a!=null).Select(a=>a.ActorId+"@"+P(a.transform.position)))+
                    " leases="+string.Join(";",owners.SelectMany(o=>o.ServiceLeases.Select(l=>Owner(o)+":"+l.Key+"@"+P(l.Value)))));
                if(terminal&&id!=null&&frozenIdentities.Add(identity))
                {
                    motion.Add("FIRST_TERMINAL sequence="+sequence+" frame="+Time.frameCount+" actor="+id+" identity="+identity+" source="+source+" operation="+operation+" "+data);
                    // Include all actors' bounded recent request rings at this terminal decision.
                    foreach(var ring in preFailure) motion.Add("PRE_FAILURE_RING key="+ring.Key+" count="+ring.Value.Count+"\n"+string.Join("\n",ring.Value));
                }
            }
            private void RetainSample(string key,string line)
            {
                if(!preFailure.TryGetValue(key,out var ring))
                {
                    if(preFailure.Count>=2048) { ringDropped++; return; }
                    preFailure.Add(key,ring=new Queue<string>());
                }
                if(ring.Count==12) { ring.Dequeue();ringEvicted++; }
                ring.Enqueue(line);
            }
            private void StallStep(NavigationActor actor,Vector3 start,Vector3 velocity,Vector3 cornerDelta,
                Vector3 dynamicDelta,Vector3 staticDelta,Vector3 navDelta,Vector3 finalDelta,float dt)
            {
                ringObserved++;
                var key=Identity(actor.ActorId);
                if(frozenIdentities.Contains(key)) { repeatedObservations++;return; }
                var layer=(velocity*dt).sqrMagnitude<1e-12f?"zero-desired":cornerDelta.sqrMagnitude<1e-12f?"corner":
                    dynamicDelta.sqrMagnitude<1e-12f?"dynamic":staticDelta.sqrMagnitude<1e-12f?"static":
                    navDelta.sqrMagnitude<1e-12f?"nav":finalDelta.sqrMagnitude<1e-12f?"sweep":"motion";
                if(layer=="zero-desired"&&!((IDictionary)Read(test.customers,"visits")).Contains(actor.ActorId)&&
                    !((IDictionary)Read(test.world.Service,"byActor")).Contains(actor.ActorId)) StallIdleEmployeeSeen=true;
                var line="frame="+Time.frameCount+" time="+Time.time.ToString("F6")+" layer="+layer+" "+ActorContext(actor.ActorId)+
                    " start="+P(start)+" velocity="+P(velocity)+" dt="+dt.ToString("R")+" corner="+P(cornerDelta)+
                    " dynamic="+P(dynamicDelta)+" static="+P(staticDelta)+" nav="+P(navDelta)+" sweep="+P(finalDelta)+" accepted="+P(actor.transform.position);
                RetainSample(key,line);
                // Layer changes, including zero velocity and request 0, are semantic transitions.
                StallDecision("world","computed-layer",actor.ActorId,line,layer,false);
            }
            private static string BoundaryQueries(NavigationActor actor,object driver,Vector3 input)
            {
                // Post-stage pure query corroboration, not a second motion evaluation.
                // 原NavClamp已归零且pose未动；只读NavMesh，绝不修改driver或actor。
                var filter=new UnityEngine.AI.NavMeshQueryFilter{agentTypeID=(int?)Read(driver,"ownedAgentType")??actor.Agent.agentTypeID,
                    areaMask=actor.Agent.areaMask};
                var position=actor.transform.position;input.y=0;
                var startHit=UnityEngine.AI.NavMesh.SamplePosition(position,out var start,.05f,filter);
                var first=UnityEngine.AI.NavMesh.Raycast(position,position+input,out var hit,filter);
                var result="POST_STAGE_QUERY active="+actor.gameObject.activeInHierarchy+" enabled="+actor.Agent.enabled+
                    " onMesh="+actor.Agent.isOnNavMesh+" actualAgentType="+actor.Agent.agentTypeID+" filterType="+filter.agentTypeID+
                    " startSample="+startHit+" start="+P(start.position)+" startOffset="+P(start.position-position)+
                    " firstRay="+first+" hit="+P(hit.position)+" normal="+P(hit.normal)+" distance="+hit.distance.ToString("R");
                result+="\n"+ShortGeometryQueries(position,input,filter,actor.Settings.Epsilon);
                var delta=input;
                if(first)
                {
                    var normal=hit.normal;normal.y=0;
                    if(!float.IsNaN(normal.x+normal.y+normal.z)&&!float.IsInfinity(normal.x+normal.y+normal.z)&&normal.sqrMagnitude>1e-12f)
                    { normal.Normalize();var inward=Vector3.Dot(delta,normal);if(inward<0) delta=Vector3.ClampMagnitude(delta-normal*inward,delta.magnitude); }
                    var second=UnityEngine.AI.NavMesh.Raycast(position,position+delta,out var secondHit,filter);
                    result+=" tangent="+P(delta)+" secondRay="+second+" secondHit="+P(secondHit.position)+" secondNormal="+P(secondHit.normal);
                    if(second){var offset=secondHit.position-position;offset.y=0;delta=Vector3.ClampMagnitude(delta,Mathf.Max(0,offset.magnitude-actor.Settings.Epsilon));}
                }
                var end=position+delta;
                var endHit=UnityEngine.AI.NavMesh.SamplePosition(end,out var sample,.05f,filter);
                return result+" computedDelta="+P(delta)+" endSample="+endHit+" end="+P(end)+" sample="+P(sample.position)+
                    " sampleOffset="+P(sample.position-end)+" epsilon="+actor.Settings.Epsilon.ToString("R");
            }
            private static string ShortGeometryQueries(Vector3 position,Vector3 input,UnityEngine.AI.NavMeshQueryFilter filter,float epsilon)
            {
                var intended=position+input;
                var sampleOk=UnityEngine.AI.NavMesh.SamplePosition(intended,out var exactEnd,.05f,filter);
                var path=new UnityEngine.AI.NavMeshPath();
                var found=UnityEngine.AI.NavMesh.CalculatePath(position,intended,filter,path);
                var edgeOk=UnityEngine.AI.NavMesh.FindClosestEdge(position,out var edge,filter);
                var result="PURE_SHORT_GEOMETRY start="+P(position)+" originalEnd="+P(intended)+" endSample="+sampleOk+
                    " sampledEnd="+P(exactEnd.position)+" endOffset="+P(exactEnd.position-intended)+" pathBool="+found+
                    " pathStatus="+path.status+" corners="+PathText(path.corners)+" closestEdge="+edgeOk+
                    " edgePos="+P(edge.position)+" edgeNormal="+P(edge.normal)+" edgeDistance="+edge.distance.ToString("R");
                var normal=edge.normal;normal.y=0;
                if(edgeOk&&normal.sqrMagnitude>1e-12f&&!float.IsNaN(normal.x+normal.z)&&!float.IsInfinity(normal.x+normal.z))
                {
                    normal.Normalize();
                    foreach(var amount in new[]{epsilon,10*epsilon})
                    {
                        var queryStart=position+normal*amount;
                        var blocked=UnityEngine.AI.NavMesh.Raycast(queryStart,queryStart+input,out var shifted,filter);
                        result+=" queryOnlyInward="+amount.ToString("R")+" ray="+blocked+" hit="+P(shifted.position)+" normal="+P(shifted.normal);
                    }
                }
                return result;
            }
            public void Finish()
            {
                if(test.world!=null) typeof(NavigationWorld).GetField("StepObserver",Fields).SetValue(test.world,null);
                if(test.customers!=null) typeof(CustomerFlowController).GetField("PickupObserver",Fields).SetValue(test.customers,null);
                if(test.customers!=null) typeof(CustomerFlowController).GetField("CorridorObserver",Fields).SetValue(test.customers,null);
                if(stallTrace)
                {
                    typeof(NavMeshMovementDriver).GetField("PathObserver",Fields|System.Reflection.BindingFlags.Static).SetValue(null,null);
                    typeof(EmployeeFlowController).GetField("MovementObserver",Fields).SetValue(test.employees,null);
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory,"semantics.txt"),"BOUNDED_SEMANTIC_TRANSITIONS Observed="+semanticObserved+
                        " RepeatedSameSemanticSuppressed="+semanticSuppressed+" CapacityDropped="+eventOverflow+" ActorFiltered=0 MissingStage=NOT_EVALUATED\n\n"+string.Join("\n\n",events));
                    File.WriteAllText(Path.Combine(directory,"rings.txt"),"BOUNDED_RING_NOT_FULL_TIMELINE PerIdentity=12 Observed="+ringObserved+
                        " OldSamplesEvicted="+ringEvicted+" IdentityCapacityDropped="+ringDropped+" PostActorTerminalSuppressed="+repeatedObservations+
                        " FrozenIdentities="+string.Join(";",frozenIdentities)+"\n"+string.Join("\n",motion)+"\nFINAL_RINGS\n"+
                        string.Join("\n",preFailure.Select(r=>"key="+r.Key+"\n"+string.Join("\n",r.Value))));
                    File.WriteAllText(Path.Combine(directory,"terminal.txt"),test.UserCounterDiagnostic());
                    TestContext.WriteLine("STALL_TRACE "+directory+" semantics="+events.Count+" semanticDropped="+eventOverflow+" rings="+preFailure.Count+" ringDropped="+ringDropped);
                    return;
                }
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory,"chronology.txt"),"Overflow="+eventOverflow+" Observations="+observationSequence+" RepeatedSuppressed="+repeatedObservations+"\n"+string.Join("\n\n",events));
                File.WriteAllText(Path.Combine(directory,"motion.txt"),"SelectedActor="+selectedActor+" Request="+selectedRequest+
                    " Overflow="+motionOverflow+"\n"+string.Join("\n",motion)+"\nTERMINAL\n"+test.UserCounterDiagnostic());
                TestContext.WriteLine("CORRIDOR_TRACE "+directory+" events="+events.Count+" motion="+motion.Count);
            }
        }

        private string UserTrafficPathQueries(string stage, string probeId = null)
        {
            // Readonly diagnostics, including counterfactual BODY removal as evidence only.
            // 只读诊断：不移动角色、不改request/route/目标/lease，不授权等待或派发。
            var result = new List<string> { "TRAFFIC_QUERY " + stage };
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var entries = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers, "visits");
            var accepted = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(world.Service, "byId");
            object Read(object v, string name) => v.GetType().GetField(name).GetValue(v);
            object Call(string name, params object[] args) => typeof(CustomerFlowController).GetMethod(name, flags).Invoke(customers, args);
            foreach (var snapshot in customers.Snapshot.Where(v => v.State == CustomerVisitState.Blocked || v.VisitId == probeId))
            {
                var visit = entries[snapshot.VisitId]; var route = (Queue<Vector3>)Read(visit, "Route");
                if (route == null || route.Count == 0) continue;
                var actor = (NavigationActor)Read(visit, "Actor");
                var path = adapter.GetCompletePath(actor.transform.position, route.Peek());
                var clearance = (float)Call("RouteClearance", actor.Settings);
                result.Add("blocked=" + snapshot.VisitId + " resume=" + Read(visit, "ResumeState") + " segments=" + Read(visit, "Segments") +
                    " replans=" + Read(visit, "Replans") + " next=" + route.Peek().ToString("F6") + " wait=" + Call("CanWaitForMovingPickupBodies", visit, path));
                foreach (System.Collections.DictionaryEntry pair in entries)
                    if ((string)pair.Key != snapshot.VisitId && (bool)Call("ReservesRouteTarget", pair.Value, snapshot.VisitId, null))
                        result.Add("reserved=" + pair.Key + " target=" + ((QueueAssignment)Read(pair.Value, "Assignment")).Position.ToString("F6") +
                            " onNext=" + !CustomerRoutePlanner.IsClear(path, new[] { ((QueueAssignment)Read(pair.Value, "Assignment")).Position }, clearance));
                foreach (System.Collections.DictionaryEntry pair in entries)
                {
                    var other = pair.Value; var body = (NavigationActor)Read(other, "Actor");
                    var request = (long)Read(other, "RequestId"); if (body == null || request == 0) continue;
                    var remaining = (IReadOnlyList<Vector3>)Call("ExpandRoute", body.transform.position,
                        new[] { ((NavigationTarget)Read(other, "CurrentSegmentTarget")).Position }.Concat((Queue<Vector3>)Read(other, "Route") ?? Enumerable.Empty<Vector3>()));
                    var obstacles = (Vector3[])Call("RouteObstacles", body.ActorId, null, false);
                    result.Add("moving=" + body.ActorId + " state=" + Read(other, "State") + " accepted=" + accepted.Contains(request) +
                        " bodyOnNext=" + !CustomerRoutePlanner.IsClear(path, new[] { body.transform.position }, clearance) +
                        " targetOnNext=" + !CustomerRoutePlanner.IsClear(path, new[] { ((NavigationTarget)Read(other, "FinalTarget")).Position }, clearance) +
                        " remainingClear=" + CustomerRoutePlanner.IsClear(remaining, obstacles, (float)Call("RouteClearance", body.Settings)) +
                        " remainingBlockers=" + string.Join(";", world.RegisteredActors.Where(a => a != body && !CustomerRoutePlanner.IsClear(remaining, new[] { a.transform.position }, clearance)).Select(a => a.ActorId)));
                }
            }
            var pickup = CafeLoopRuntimeProof.Field<PickupQueueService>(customers, "pickupQueue");
            if (pickup == null) return string.Join("\n", result);
            var slots = CafeLoopRuntimeProof.Field<Vector3[]>(pickup, "waitSlots");
            result.Add("pickup reservations=" + CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(pickup, "reservations").Count +
                " canReserve=" + pickup.CanReserve + " leases=" + string.Join(";", pickup.ServiceLeases.Select(p => p.Key + "@" + p.Value.ToString("F6"))) +
                " targets=" + string.Join(";", customers.CounterAssignments.Select(a => a.VisitId + "@" + a.Position.ToString("F6"))));
            foreach (var front in customers.CounterAssignments.Where(a => a.SlotIndex == 0 && a.Arrived))
            {
                var actor = world.RegisteredActors.Single(a => a.ActorId == front.VisitId);
                var obstacles = (Vector3[])Call("RouteObstacles", actor.ActorId, null, false);
                var clearance = (float)Call("RouteClearance", actor.Settings);
                var plan = Call("BuildPickupWaitRoute", actor.transform.position, slots[0], actor.ActorId, actor.Settings);
                result.Add("counterFront=" + actor.ActorId + " pose=" + actor.transform.position.ToString("F6") + " pickupHead=" + slots[0].ToString("F6") + " actualPickupPlan=" + (plan == null ? "NULL" : "COMPLETE"));
                foreach (var body in world.RegisteredActors.Where(a => a != actor))
                {
                    var without = obstacles.Where(p => Vector3.Distance(p, body.transform.position) > .001f).ToArray();
                    var hypothetical = new CustomerRoutePlanner().Build(actor.transform.position, slots[0], without, clearance, adapter.GetCompletePath);
                    result.Add("readonlyRemove=" + body.ActorId + " state=" + (entries.Contains(body.ActorId) ? Read(entries[body.ActorId], "State") : "STAFF") + " route=" + (hypothetical == null ? "NULL" : string.Join(";", hypothetical)));
                }
            }
            return string.Join("\n", result);
        }

        private string UserExitPathQueries(string stage)
        {
            // Read-only path queries: no requests, actors, leases or gameplay state are changed.
            // 只查询当前完整路线和安全中转点，不发移动请求，不忽略任何角色身体。
            var result = new List<string> { "EXIT_QUERY " + stage };
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var entries = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers, "visits");
            var runtime = Find<CafeLayoutRuntime>();
            var root = CafeLoopRuntimeProof.Field<Transform>(customers, "gridRoot");
            var goal = CafeLoopRuntimeProof.Field<Transform>(customers, "exit").position;
            foreach (var visit in customers.Snapshot.Where(v => v.State == CustomerVisitState.Blocked))
            {
                var entry = entries[visit.VisitId];
                var actor = world.RegisteredActors.Single(a => a.ActorId == visit.VisitId);
                var obstacles = (Vector3[])typeof(CustomerFlowController).GetMethod("RouteObstacles", flags)
                    .Invoke(customers, new object[] { visit.VisitId, null, false });
                var clearance = (float)typeof(CustomerFlowController).GetMethod("RouteClearance", flags)
                    .Invoke(customers, new object[] { actor.Settings });
                var path = adapter.GetCompletePath(visit.Position, goal);
                var planned = (IReadOnlyList<Vector3>)typeof(CustomerFlowController).GetMethod("PlanPickupExit", flags)
                    .Invoke(customers, new object[] { visit.Position, goal, obstacles, actor.Settings });
                result.Add("visit=" + visit.VisitId + " from=" + visit.Position.ToString("F6") + " exit=" + goal.ToString("F6") +
                    " fromWalkable=" + adapter.IsPointWalkable(visit.Position) + " exitWalkable=" + adapter.IsPointWalkable(goal) +
                    " clearance=" + clearance + " native=" + (path == null ? "NULL" : string.Join(";", path)) +
                    " nativeClear=" + CustomerRoutePlanner.IsClear(path, obstacles, clearance) +
                    " currentExitPlan=" + (planned == null ? "NULL" : string.Join(";", planned)) +
                    " hasCollected=" + entry.GetType().GetField("HasCollected").GetValue(entry));
                result.Add("obstacles=" + string.Join(";", obstacles.Select(p => p.ToString("F6"))));
                var candidates = 0;
                Vector3? clearVia = null;
                foreach (var tile in runtime.RoomSurfaceLayout.FloorTiles.Keys)
                {
                    var cell = runtime.Layout.GridSettings.CellSize;
                    var via = root.TransformPoint(new Vector3((tile.X + .5f) * cell, 0, (tile.Y + .5f) * cell));
                    via.y = visit.Position.y;
                    if (!adapter.IsPointWalkable(via)) continue;
                    candidates++;
                    if (!CustomerRoutePlanner.IsClear(adapter.GetCompletePath(visit.Position, via), obstacles, clearance) ||
                        !CustomerRoutePlanner.IsClear(adapter.GetCompletePath(via, goal), obstacles, clearance)) continue;
                    clearVia = via;
                    break;
                }
                result.Add("finiteFloorCellQuery count=" + candidates + " allBodiesRetainedClearVia=" +
                    (clearVia.HasValue ? clearVia.Value.ToString("F6") : "NONE"));
            }
            return string.Join("\n", result);
        }

        private IEnumerator UserMoveCounterSupport(string id, GridPosition destination)
        {
            var session = CafeLoopRuntimeProof.Field<DecorationSession>(Find<DecorationModeController>(), "session");
            Assert.That(session.BeginExisting(id).Succeeded, Is.True);
            Assert.That(session.MovePreview(destination).Succeeded, Is.True);
            yield return MatrixConfirmFurniture();
        }

        private string UserCounterDiagnostic()
        {
            var entries = CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers, "visits");
            var details = new List<string>();
            foreach (System.Collections.DictionaryEntry pair in entries)
            {
                var item = pair.Value;
                object Read(string name) => item.GetType().GetField(name).GetValue(item);
                details.Add(pair.Key + " state=" + Read("State") + " failure=" + Read("Failure") +
                    " request=" + Read("RequestId") + " pendingPickup=" + Read("PendingPickupSegment") +
                    " pendingExit=" + Read("PendingExitSegment") + " yieldPhase=" + Read("DeliveryYieldPhase"));
            }
            return CafeLoopStatusView.Format(owner.ViewSnapshot) + "\ncanProgress=" + owner.CanProgress +
                " deliveryFailure=" + typeof(CustomerFlowController).GetProperty("PickupDeliveryFailure",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(customers) +
                "\n" + MatrixTrafficDescription() +
                "\nstaff=" + string.Join(";", employees.Snapshot.Select(e => e.Role + "/" + e.State + "/" + e.Failure)) +
                "\nbodies=" + string.Join(";", world.RegisteredActors.Select(a => a.ActorId + "@" + a.transform.position.ToString("F6") +
                    (a.Agent != null && a.Agent.enabled && a.Agent.isOnNavMesh ? " target=" + a.Agent.destination.ToString("F6") +
                    " stopped=" + a.Agent.isStopped + " path=" + a.Agent.pathStatus : " no active agent"))) +
                "\nvisits=" + string.Join(" | ", details);
        }
    }
}
#endif

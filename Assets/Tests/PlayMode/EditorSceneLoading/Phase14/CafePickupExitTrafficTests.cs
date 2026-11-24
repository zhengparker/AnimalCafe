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
            => ObserveRealCoreUnissuedPickupBeforeLaterExit();
        [UnityTest] public IEnumerator DisabledEarlierUnissuedPickupDoesNotKeepExitAuthority()
            => ObserveRealCoreUnissuedPickupBeforeLaterExit(true);
        [UnityTest] public IEnumerator BlockedEarlierUnissuedPickupDoesNotKeepExitWaiting()
            => ObserveRealCoreUnissuedPickupBeforeLaterExit(invalidation:2);
        [UnityTest] public IEnumerator EarlierPickupDisabledAfterExitWaitRetiresPriority()
            => ObserveRealCoreUnissuedPickupBeforeLaterExit(invalidation:3);
        [UnityTest] public IEnumerator EarlierPickupObstructedAfterExitWaitRetiresPriority()
            => ObserveRealCoreUnissuedPickupBeforeLaterExit(invalidation:4);
        [UnityTest] public IEnumerator CanonicalUntransferredTargetCannotAuthorizePickupExitWait()
            => ObserveRealCanonicalUntransferredTarget();

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

        private IEnumerator ObserveRealCoreUnissuedPickupBeforeLaterExit(bool disableBeforeIssue=false,int invalidation=0,bool requireAcquiredPriority=false,bool requireStrictQualification=false,bool requireServiceLeaseRevalidation=false,bool requireDetourCandidate=false,bool requireAdmissionCurrentRoute=false)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            object Read(object value,string name)=>value.GetType().GetField(name,flags)?.GetValue(value) ??
                value.GetType().GetProperty(name,flags)?.GetValue(value);
            bool Flag(object value,string name)=>(bool)Read(value,name);
            ServiceTrafficCoordinator core=null;
            IDictionary entries=null,visits=null,native=null;
            CafeLoopService service=null;
            var pathsConflict=typeof(ServiceTrafficCoordinator).GetMethod("PathsConflict",BindingFlags.Static|BindingFlags.NonPublic);
            var receipts=new HashSet<string>();var observers=new List<CafeActualExitObserver>();
            var tokens=new Dictionary<string,AnimalCafe.Capacity.CapacityToken>();
            var errors=new List<string>();var records=new List<string>();
            string collector=null,incoming=null;object earlierEntry=null,earlierLeg=null,exitEntry=null;
            TrafficIntentKey earlierKey=default,exitKey=default;TrafficDecision earlierSource=null;
            TrafficRouteProof earlierProof=null;NavigationActor earlierBody=null;
            var capture=false;var attempted=false;var collected=false;var candidateSeen=false;var ownerCross=false;
            var waiting=false;var exitBound=false;var observing=false;var tracking=true;var earlierFirst=0;var exitFirst=0;
            var priorityObserved=false;var prioritySourceSequence=0L;var priorityAcquired=0L;var priorityExitSequence=0L;
            var priorityOwnerQueries=0;var priorityExitQueries=0;var priorityDebt=float.PositiveInfinity;var priorityStamp=default(TrafficNativeStamp);
            var priorityTokens=new Dictionary<string,AnimalCafe.Capacity.CapacityToken[]>();
            var strictQualificationObserved=false;
            NavigationActor detourBody=null;Vector3 detourPosition=default;Vector3[] detourPath=null,detourWaypoints=null;int[] detourLegEnds=null;
            object detourFrozen=null,detourResources=null;var detourFirst=0;var detourSequence=0L;var detourQueries=0;var detourDebt=0f;
            var detourWaitSamples=0;var detourSampleFrame=-1;var detourBoundSampled=false;
            FieldInfo leaseHook=null;Delegate leasePrior=null,leaseInstalled=null;
            PickupQueueService heldPickup=null;PickupQueueService.ReservationClaim heldLeaseClaim=null;
            TrafficParkingFact heldServiceFact=null;Vector3 heldLeasePosition=default;object heldCollector=null,heldAdmission=null;
            object leaseFrozen=null,leaseResources=null;TrafficDecision leaseWaitDecision=null;
            var leaseWaitCaptured=false;var leaseNativeArrived=false;var leaseCheckpoint=false;var leaseRevalidated=false;
            var leaseBoundSampled=false;var leaseDepartureSeen=false;var leaseReleased=false;
            var leaseNativeAdvanced=false;var leaseWaitBodyVersion=0L;var leaseWaitOwnerPosition=default(Vector3);object leaseOriginalNative=null;
            var leaseFirst=0;var leaseQueries=0;var leaseSequence=0L;var leaseDebt=0f;var leaseStamp=default(TrafficNativeStamp);
            var leaseTokenStates=new Dictionary<AnimalCafe.Capacity.CapacityToken,AnimalCafe.Capacity.ReservationState>();
            string lastPredicate=null;var predicateTransitions=0;var predicateOverflow=false;
            string lastHintLeg=null;var hintRecords=0;var hintOverflow=false;
            var disabledApplied=false;var disabledEvaluated=false;var disabledBoundSampled=false;
            var disabledPose=default(Vector3);var disabledFrame=-1;string lastDisabledDecision=null;
            var disabledDecisions=0;
            NavigationActor actualObstruction=null;var postWaitCapacities=Array.Empty<int>();var postWaitBody=default(Vector3);
            var contributorReady=false;var contributorApplied=false;var contributorEvaluated=false;var contributorCancelled=false;
            var contributorStamp=default(TrafficNativeStamp);var contributorPose=default(Vector3);var contributorCollectorPose=default(Vector3);
            PickupQueueService contributorPickup=null;PickupQueueService.ReservationClaim contributorClaim=null;
            var contributorTokens=Array.Empty<AnimalCafe.Capacity.CapacityToken>();object contributorFrozen=null,contributorResources=null;
            var contributorDebt=0f;var contributorQueries=0;var contributorOwnerQueries=0;var contributorSequence=0L;var contributorProxyEnabled=false;
            TrafficDecision contributorOriginalWait=null;string contributorDecisionSignature=null;var contributorDecisionRecords=0;
            float Pair(NavigationActor a,NavigationActor b)=>a.Settings.AgentRadius+b.Settings.AgentRadius+
                Math.Max(a.Settings.CollisionSkin+a.Settings.Epsilon,b.Settings.CollisionSkin+b.Settings.Epsilon);
            NavigationActor ObstructOriginalCurrentLeg()
            {
                var path=(Vector3[])Read(earlierLeg,"Path");Assert.That(path.Length,Is.GreaterThan(1));
                var length=0f;for(var i=1;i<path.Length;i++)length+=Vector3.Distance(path[i-1],path[i]);
                Assert.That(length,Is.GreaterThan(0));var remaining=length*.5f;var midpoint=path[0];
                for(var i=1;i<path.Length;i++)
                {
                    var segment=Vector3.Distance(path[i-1],path[i]);
                    if(remaining<=segment){midpoint=Vector3.Lerp(path[i-1],path[i],remaining/segment);break;}remaining-=segment;
                }
                // Exactly one real retained-leg midpoint. Normal floor/body/Register guards may reject this premise.
                // 只取原真实腿中点一次；不搜索替代点、不移动旧body、不缩小clearance。
                var blocker=Blocker(midpoint,"real-unissued-leg-obstruction-"+invalidation);
                Assert.That(world.RegisteredActors.Contains(blocker),Is.True);
                Assert.That(CustomerRoutePlanner.IsClear(path,new[]{blocker.transform.position},Pair(earlierBody,blocker)),Is.False,
                    "A registered physical body must actually obstruct this same original leg");return blocker;
            }
            string DisabledFacts()
            {
                if(core==null || entries==null || visits==null)return "NOT_BOUND";
                try
                {
                    string Key(TrafficIntentKey key)=>key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/e"+key.Epoch+"/r"+key.LayoutRevision+"/"+key.Context;
                    string Value(object value,string name)=>value==null?"NULL":Convert.ToString(Read(value,name));
                    var snapshot=(TrafficWorldSnapshot)Read(core,"snapshot");var rows=new List<string>();
                    foreach(var id in new[]{collector,incoming})
                    {
                        if(id==null || !visits.Contains(id)){rows.Add("visit="+id+" absent=True");continue;}
                        var visit=visits[id];var move=Read(visit,"TrafficMove");var paid=Read(visit,"PaidPickup");
                        var body=Read(visit,"Actor") as NavigationActor;var source=move==null?null:Read(move,"Source") as TrafficDecision;
                        var key=move==null?default:(TrafficIntentKey)Read(move,"Intent");
                        var entry=move!=null && entries.Contains(key)?entries[key]:null;
                        var leg=entry==null?null:Read(entry,"Leg");var decision=entry==null?null:Read(entry,"Decision") as TrafficDecision;
                        var fact=snapshot.Bodies.FirstOrDefault(b=>ReferenceEquals(b.ActorLife,body));
                        var stamp=leg==null?default:(TrafficNativeStamp)Read(leg,"Stamp");
                        rows.Add("visit="+id+" state="+Value(visit,"State")+" resume="+Value(visit,"ResumeState")+
                            " failure="+Value(visit,"Failure")+" request="+Value(visit,"RequestId")+" move="+(move==null?"NULL":Key(key))+
                            " moveIndex="+Value(move,"Index")+" moveGeneration="+Value(move,"NativeGeneration")+
                            " sourceAcq="+(source==null?"NULL":source.AcquiredSequence.ToString())+
                            " samePermit="+(source!=null && entry!=null && ReferenceEquals(Read(entry,"JourneyPermit"),source))+
                            " entryRetired="+Value(entry,"Retired")+" queued="+Value(entry,"Queued")+" prepared="+Value(entry,"Prepared")+
                            " active="+Value(entry,"Active")+" first="+Value(entry,"FirstFrame")+" queries="+Value(entry,"Queries")+" wait="+Value(entry,"WaitRemaining")+
                            " decision="+decision?.Kind+" reason="+decision?.Reason+
                            " blockers=["+string.Join(";",decision?.Blockers.Select(b=>Key(b.Owner)+"/"+b.ResourceId+"/"+b.Proof)??Array.Empty<string>())+"]"+
                            " legIndex="+Value(leg,"Index")+" legGeneration="+Value(leg,"Generation")+
                            " committed="+Value(leg,"Committed")+" bound="+Value(leg,"Bound")+" completed="+Value(leg,"Completed")+
                            " suspended="+Value(leg,"Suspended")+" needsQuery="+Value(leg,"NeedsQuery")+
                            " stamp="+stamp.RequestId+"/"+stamp.LegGeneration+" nativeLive="+(native!=null && native.Contains(stamp.RequestId))+
                            " actorActive="+(body!=null && body.isActiveAndEnabled)+" agentEnabled="+(body!=null && body.Agent!=null && body.Agent.enabled)+
                            " onMesh="+(body!=null && body.Agent!=null && body.Agent.enabled && body.Agent.isOnNavMesh)+
                            " pose="+(body==null?"NULL":body.transform.position.ToString("F6"))+
                            " bodyEnabled="+fact?.Enabled+" bodyActive="+fact?.ActorActive+" bodyFinite="+fact?.RemainingSeconds+
                            " accepted="+Value(paid,"Accepted")+" transferred="+Value(paid,"Transferred")+" paidFailed="+Value(paid,"Failed"));
                    }
                    return "frame="+Time.frameCount+" snapshot="+snapshot.Version+"/"+snapshot.BodyVersion+" receipts="+receipts.Count+
                        " observerErrors="+errors.Count+" ["+string.Join(" | ",rows)+"]";
                }
                catch(Exception error){return "UNKNOWN "+error.GetType().Name+": "+error.Message;}
            }
            void ObserveDisabledSource()
            {
                var snapshot=(TrafficWorldSnapshot)Read(core,"snapshot");
                {
                    var facts=snapshot.Bodies.Where(b=>ReferenceEquals(b.ActorLife,earlierBody)).ToArray();
                    Assert.That(facts.Length,Is.EqualTo(1),"Disable retains the physical body fact");
                    if(disableBeforeIssue || invalidation==3)
                    {
                        if(facts[0].ActorActive)return; // Require the normal driver to publish the real lifecycle input.
                        Assert.That(earlierBody.isActiveAndEnabled,Is.False);
                        Assert.That(earlierBody.transform.position,Is.EqualTo(disabledPose));
                    }
                    else
                    {
                        var physical=snapshot.Bodies.SingleOrDefault(b=>ReferenceEquals(b.ActorLife,actualObstruction));
                        if(physical==null)return; // No decision claim before the registered obstacle is published.
                        Assert.That(physical.Enabled&&physical.ActorActive,Is.True);
                        Assert.That(CustomerRoutePlanner.IsClear((Vector3[])Read(earlierLeg,"Path"),new[]{physical.Position},Pair(earlierBody,actualObstruction)),Is.False);
                    }
                    if(invalidation==3 || invalidation==4)
                    {
                        Assert.That(waiting && candidateSeen && ownerCross,Is.True,"Actual original-source WAIT must precede invalidation");
                        Assert.That(entries.Contains(exitKey)&&ReferenceEquals(entries[exitKey],exitEntry),Is.True);
                        var current=core.GetDecision(exitKey);
                        if(current.Kind==TrafficDecisionKind.DeferredByBudget)return; // Scheduling alone is not a retirement witness.
                        records.Add("CORE_POST_WAIT_INVALIDATION mode="+invalidation+" kind="+current.Kind+" reason="+current.Reason+
                            " bodyFinite="+facts[0].IndependentlyProgressing+" bodyRemaining="+facts[0].RemainingSeconds+
                            " blockers=["+string.Join(";",current.Blockers.Select(b=>b.Owner+"/"+b.ResourceId+"/"+b.Proof))+"]");
                        Assert.That(current.Kind==TrafficDecisionKind.WaitingForTraffic&&current.Blockers.Any(b=>b.Owner.Equals(earlierKey)),Is.False,
                            "Disabled/physically obstructed original owner cannot retain an independent finite release promise");
                        Assert.That((int)Read(exitEntry,"FirstFrame"),Is.EqualTo(exitFirst));
                        var stamp=Read(exitEntry,"Leg")==null?default:(TrafficNativeStamp)Read(Read(exitEntry,"Leg"),"Stamp");
                        if((long)Read(visits[collector],"RequestId")==0)
                            Assert.That(((NavigationActor)Read(visits[collector],"Actor")).transform.position,Is.EqualTo(postWaitBody));
                        else
                        {
                            var leg=Read(exitEntry,"Leg");
                            Assert.That(Flag(leg,"Committed")&&Flag(leg,"Bound")&&!Flag(leg,"Completed")&&native.Contains(stamp.RequestId)&&
                                stamp.RequestId==(long)Read(visits[collector],"RequestId")&&ReferenceEquals(Read(leg,"ActorLife"),Read(visits[collector],"Actor")),Is.True,
                                "Any actual progress requires the original Exit's real Core+Native authority");
                        }
                        CollectionAssert.AreEqual(postWaitCapacities,customers.Capacity.GetCapacities().Select(c=>c.Used));
                        Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,tokens[incoming])).State,
                            Is.Not.EqualTo(AnimalCafe.Capacity.ReservationState.Released));
                        Assert.That(owner.Snapshot.PaidVisitIds.Contains(incoming),Is.True);
                        Assert.That(receipts,Is.Empty,"Priority retirement cannot fabricate a departure or release capacity");
                        disabledEvaluated=true;return;
                    }
                    Assert.That((long)Read(visits[incoming],"RequestId"),Is.EqualTo(0L));
                    Assert.That(native.Values.Cast<object>().Any(n=>(string)Read(n,"ActorId")==incoming),Is.False,
                        "A disabled unissued owner must not acquire a Native request");
                    Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,tokens[incoming])).State,
                        Is.Not.EqualTo(AnimalCafe.Capacity.ReservationState.Released));
                    Assert.That(owner.Snapshot.PaidVisitIds.Contains(incoming),Is.True,"Disable cannot remove real paid ownership");
                    var original=core.GetDecision(exitKey);
                    Assert.That(original.Kind==TrafficDecisionKind.WaitingForTraffic && original.Blockers.Any(b=>b.Owner.ActorId==incoming),Is.False,
                        "The actual decision cannot claim finite release from this disabled actor");
                    var signature=original.Kind+"/"+original.Reason+"/"+original.AcquiredSequence;
                    if(signature!=lastDisabledDecision && disabledDecisions<8)
                    {
                        lastDisabledDecision=signature;disabledDecisions++;
                        records.Add("CORE_DISABLED_EXIT_DECISION frame="+Time.frameCount+" disabledFrame="+disabledFrame+
                            " kind="+original.Kind+" reason="+original.Reason+" acquired="+original.AcquiredSequence);
                    }
                    if(original.Kind!=TrafficDecisionKind.DeferredByBudget)disabledEvaluated=true;
                }
                foreach(DictionaryEntry row in entries)
                {
                    var key=(TrafficIntentKey)row.Key;if(key.ActorId!=collector || key.IntentId!="actual-exit" || Flag(row.Value,"Retired"))continue;
                    var entry=row.Value;var decision=core.GetDecision(key);var jobs=(IList)Read(entry,"Jobs");Vector3[] actual=null;
                    Assert.That(ReferenceEquals(entry,exitEntry) && key.Equals(exitKey),Is.True,
                        "Original exit source cannot be replaced before explicit recovery");
                    if((int)Read(entry,"JobIndex")>0 && jobs.Count>0 && Flag(jobs[0],"complete"))actual=Read(jobs[0],"ExpandedPath") as Vector3[];
                    var primary=Read(entry,"JourneyPermit") as TrafficDecision;var originalRequest=Read(entry,"JourneyRequest") as TrafficRequest;
                    if((actual==null || actual.Length<2) && primary!=null && originalRequest!=null)
                        actual=primary.ProofRoutes.FirstOrDefault(p=>p.ResourceId==originalRequest.ResourceId)?.ExpandedPath.ToArray();
                    if(ReferenceEquals(entry,exitEntry) && actual!=null && actual.Length>1)
                    {
                        candidateSeen=true;
                        var collectorBody=(NavigationActor)Read(visits[collector],"Actor");
                        var clearance=earlierBody.Settings.AgentRadius+collectorBody.Settings.AgentRadius+
                            Math.Max(earlierBody.Settings.CollisionSkin+earlierBody.Settings.Epsilon,collectorBody.Settings.CollisionSkin+collectorBody.Settings.Epsilon);
                        ownerCross|=(bool)pathsConflict.Invoke(null,new object[]{actual,(Vector3[])Read(earlierLeg,"Path"),clearance});
                    }
                    var leg=Read(entry,"Leg");
                    if(leg==null || !Flag(leg,"Committed") || !Flag(leg,"Bound") || Flag(leg,"Completed"))continue;
                    var stamp=(TrafficNativeStamp)Read(leg,"Stamp");var visit=visits[collector];var move=Read(visit,"TrafficMove");
                    var source=move==null?null:Read(move,"Source") as TrafficDecision;
                    Assert.That(native.Contains(stamp.RequestId) && stamp.RequestId==(long)Read(visit,"RequestId") && source!=null &&
                        source.AcquiredSequence>0 && ReferenceEquals(Read(entry,"JourneyPermit"),source) &&
                        ReferenceEquals(Read(leg,"ActorLife"),Read(visit,"Actor")),Is.True,"Exit issue needs actual Core and Native authority");
                    {
                        var body=(NavigationActor)Read(visit,"Actor");
                        var clearance=earlierBody.Settings.AgentRadius+body.Settings.AgentRadius+
                            Math.Max(earlierBody.Settings.CollisionSkin+earlierBody.Settings.Epsilon,body.Settings.CollisionSkin+body.Settings.Epsilon);
                        var near=typeof(ServiceTrafficCoordinator).GetMethod("NearPath",BindingFlags.Static|BindingFlags.NonPublic);
                        Assert.That((bool)near.Invoke(null,new object[]{disabledPose,(Vector3[])Read(leg,"Path"),clearance}),Is.False,
                            "A real alternative exit must clear the still-present earlier body");
                        if(actualObstruction!=null)Assert.That(CustomerRoutePlanner.IsClear((Vector3[])Read(leg,"Path"),
                            new[]{actualObstruction.transform.position},Pair(body,actualObstruction)),Is.True,
                            "A real issued alternate must clear the registered physical obstruction");
                    }
                    exitBound=true;
                    if(!disabledBoundSampled)
                    {
                        // Sample the first genuine Bound now, before another normal frame can advance departure.
                        // 首次真实发腿即采样；不等下一帧，也不把Scene销毁当作实际Exit。
                        Assert.That(receipts,Is.Empty,"First actual issue cannot fabricate a departure");
                        Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,tokens[incoming])).State,
                            Is.Not.EqualTo(AnimalCafe.Capacity.ReservationState.Released));
                        Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==collector).State,Is.EqualTo(OrderState.Completed));
                        disabledBoundSampled=true;
                        records.Add("CORE_DISABLED_FIRST_BOUND frame="+Time.frameCount+" request="+stamp.RequestId+
                            " ownerTokenRetained=True noFakeExits=True realCoreNative=True pathClearsDisabledBody=True");
                    }
                }
            }
            void TryApplyContributorInvalidation()
            {
                if(!contributorReady || contributorApplied || !visits.Contains(incoming) || !visits.Contains(collector) ||
                    !entries.Contains(earlierKey) || !ReferenceEquals(entries[earlierKey],earlierEntry) || !ReferenceEquals(Read(earlierEntry,"Leg"),earlierLeg) ||
                    !ReferenceEquals(Read(earlierEntry,"JourneyPermit"),earlierSource))return;
                var wait=core.GetDecision(exitKey);var stamp=(TrafficNativeStamp)Read(earlierLeg,"Stamp");
                if(wait.Kind!=TrafficDecisionKind.WaitingForTraffic || !wait.Blockers.Any(b=>b.Owner.Equals(earlierKey)&&b.Proof=="current-conflict-segment") ||
                    !Flag(earlierLeg,"Committed") || !Flag(earlierLeg,"Bound") || Flag(earlierLeg,"Completed") ||
                    stamp.RequestId<=0 || !native.Contains(stamp.RequestId) || stamp.RequestId!=(long)Read(visits[incoming],"RequestId"))return;
                var jobs=(IList)Read(exitEntry,"Jobs");var current=Read(earlierEntry,"CurrentPath") as Vector3[];
                var actual=jobs.Count>0 && Flag(jobs[0],"complete")?Read(jobs[0],"ExpandedPath") as Vector3[]:null;
                if(actual==null || actual.Length<2 || current==null || current.Length<2 ||
                    !(bool)pathsConflict.Invoke(null,new object[]{actual,current,Pair(earlierBody,(NavigationActor)Read(visits[collector],"Actor"))}))return;
                var byActor=(IDictionary)Read(world.Service,"byActor");Assert.That(byActor.Contains(incoming),Is.True);
                Assert.That(byActor[incoming],Is.SameAs(native[stamp.RequestId]));Assert.That(Read(earlierLeg,"ActorLife"),Is.SameAs(earlierBody));
                Assert.That(Read(native[stamp.RequestId],"ActorId"),Is.EqualTo(incoming));Assert.That(earlierSource.AcquiredSequence,Is.GreaterThan(0));
                contributorPickup=(PickupQueueService)Read(visits[incoming],"PickupOwner");
                Assert.That(contributorPickup.TryCaptureReservation(incoming,out contributorClaim),Is.True);Assert.That(contributorClaim.IsCurrent,Is.True);
                contributorTokens=customers.Capacity.GetReservations().Where(record=>record.OwnerId==incoming).Select(record=>record.Token).ToArray();
                Assert.That(contributorTokens,Has.Length.EqualTo(3));Assert.That(earlierBody.gameObject.activeInHierarchy,Is.True);Assert.That(earlierBody.enabled,Is.True);
                Assert.That(earlierBody.Proxy,Is.Not.Null);contributorProxyEnabled=earlierBody.Proxy.enabled;
                contributorStamp=stamp;contributorPose=earlierBody.transform.position;contributorCollectorPose=((NavigationActor)Read(visits[collector],"Actor")).transform.position;
                contributorFrozen=Read(exitEntry,"FrozenOwners");contributorResources=Read(exitEntry,"FrozenResources");contributorDebt=(float)Read(exitEntry,"WaitRemaining");
                Assert.That((IEnumerable<TrafficIntentKey>)contributorFrozen,Is.EquivalentTo(new[]{earlierKey}));Assert.That(contributorDebt,Is.GreaterThan(0));
                contributorQueries=core.QueriesConsumed(exitKey);contributorOwnerQueries=core.QueriesConsumed(earlierKey);contributorSequence=wait.RequestSequence;
                contributorOriginalWait=wait; // Original decision object, before the real public lifecycle call queues a recheck.
                Assert.That(receipts,Is.Empty);contributorApplied=true; // Before a synchronous real lifecycle callback can publish again.
                if(invalidation==5){contributorCancelled=world.Service.Cancel(stamp.RequestId);Assert.That(contributorCancelled,Is.True);}
                else earlierBody.enabled=false; // Component only; keep GameObject and Collider unchanged.
                records.Add("CORE_REAL_CONTRIBUTOR_INVALIDATED mode="+invalidation+" key="+earlierKey+" originalNative="+stamp.RequestId+"/"+stamp.LegGeneration+
                    " oldSource="+earlierSource.AcquiredSequence+" actualWAIT=True publicCancel="+contributorCancelled+" componentEnabled="+earlierBody.enabled+" gameObjectActive="+earlierBody.gameObject.activeInHierarchy);
            }
            void ObserveInvalidatedContributor()
            {
                var snapshot=(TrafficWorldSnapshot)Read(core,"snapshot");var bodies=snapshot.Bodies.Where(body=>ReferenceEquals(body.ActorLife,earlierBody)).ToArray();
                Assert.That(bodies.Length,Is.EqualTo(1),"Cancellation/disable must not erase the physical body");
                if(bodies[0].IndependentlyProgressing || bodies[0].RemainingSeconds!=0 || invalidation==6 && bodies[0].Enabled)return;
                Assert.That(world.RegisteredActors.Contains(earlierBody),Is.True);Assert.That(earlierBody.gameObject.activeInHierarchy,Is.True);
                Assert.That(bodies[0].ActorActive,Is.EqualTo(invalidation==5),"Body ActorActive is actor.isActiveAndEnabled, not GameObject activity");
                Assert.That(earlierBody.enabled,Is.EqualTo(invalidation==5));Assert.That(earlierBody.Proxy.enabled,Is.EqualTo(contributorProxyEnabled));
                Assert.That(Vector3.Distance(earlierBody.transform.position,contributorPose),Is.LessThanOrEqualTo(earlierBody.Settings.Epsilon));
                Assert.That(Vector3.Distance(bodies[0].Position,contributorPose),Is.LessThanOrEqualTo(earlierBody.Settings.Epsilon));
                Assert.That(native.Contains(contributorStamp.RequestId),Is.False);Assert.That((long)Read(visits[incoming],"RequestId"),Is.Zero);
                Assert.That(native.Values.Cast<object>().Any(request=>(string)Read(request,"ActorId")==incoming),Is.False);
                Assert.That(core.TryGetCommittedJourney(earlierKey,out _),Is.False);Assert.That(core.HasActiveGrant(earlierKey),Is.False);
                Assert.That(core.CanIssueLeg(earlierKey,earlierBody,contributorStamp.LegGeneration),Is.False);Assert.That(core.TryCommit(earlierSource),Is.False);
                Assert.That(Read(earlierEntry,"Retired"),Is.EqualTo(true));Assert.That(Read(earlierEntry,"FirstFrame"),Is.EqualTo(earlierFirst));
                Assert.That(core.QueriesConsumed(earlierKey),Is.EqualTo(contributorOwnerQueries));
                Assert.That(contributorPickup.TryCaptureReservation(incoming,out var retained),Is.True);
                Assert.That(retained.IsCurrent && contributorClaim.IsSameReservation(retained),Is.True);Assert.That(retained.Assignment,Is.EqualTo(contributorClaim.Assignment));
                CollectionAssert.AreEquivalent(contributorTokens,customers.Capacity.GetReservations().Where(record=>record.OwnerId==incoming).Select(record=>record.Token));
                Assert.That(customers.Capacity.GetReservations().Where(record=>record.OwnerId==incoming &&
                    (record.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers || record.Kind==AnimalCafe.Capacity.CapacityKind.PickUp)).All(record=>record.State!=AnimalCafe.Capacity.ReservationState.Released),Is.True);
                Assert.That(owner.Snapshot.PaidVisitIds.Contains(incoming),Is.True);Assert.That(receipts,Is.Empty);
                Assert.That(entries.Contains(exitKey)&&ReferenceEquals(entries[exitKey],exitEntry),Is.True);var current=core.GetDecision(exitKey);
                Assert.That(Read(exitEntry,"FirstFrame"),Is.EqualTo(exitFirst));Assert.That(current.RequestSequence,Is.EqualTo(contributorSequence));
                Assert.That(core.QueriesConsumed(exitKey),Is.GreaterThanOrEqualTo(contributorQueries));
                Assert.That((float)Read(exitEntry,"WaitRemaining"),Is.InRange(0f,contributorDebt));
                Assert.That(Read(exitEntry,"FrozenOwners"),Is.SameAs(contributorFrozen));Assert.That(Read(exitEntry,"FrozenResources"),Is.SameAs(contributorResources));
                Assert.That((IEnumerable<TrafficIntentKey>)contributorFrozen,Is.EquivalentTo(new[]{earlierKey}));
                if(invalidation==5)
                {
                    var sameOriginalWait=ReferenceEquals(current,contributorOriginalWait);var queued=Flag(exitEntry,"Queued");
                    var jobs=(IList)Read(exitEntry,"Jobs");
                    var signature=current.Kind+"/"+current.Reason+"/sameOriginal="+sameOriginalWait+"/queued="+queued+"/job="+Read(exitEntry,"JobIndex")+"/"+jobs.Count;
                    if(signature!=contributorDecisionSignature && contributorDecisionRecords<8)
                    {
                        contributorDecisionSignature=signature;contributorDecisionRecords++;
                        records.Add("CORE_CANCEL_DECISION_RECHECK ordinal="+contributorDecisionRecords+" frame="+Time.frameCount+" queryFrame="+Read(core,"queryFrame")+
                            " originalDecision="+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(contributorOriginalWait)+
                            " currentDecision="+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(current)+" "+signature+
                            " acquired="+current.AcquiredSequence+" prepared="+Read(exitEntry,"Prepared")+" active="+Read(exitEntry,"Active")+
                            " native="+Read(visits[collector],"RequestId"));
                    }
                    if(sameOriginalWait && queued)
                    {
                        // Retire -> NotifyChanged queues the original waiter without publishing a new decision.
                        // 旧缓存WAIT且原ready工作已排队，不是新的finite承诺；继续原6000等真实重评。
                        Assert.That(current.Kind,Is.EqualTo(TrafficDecisionKind.WaitingForTraffic));Assert.That(current.AcquiredSequence,Is.Zero);
                        Assert.That(Flag(exitEntry,"Prepared")||Flag(exitEntry,"Active")||core.HasActiveGrant(exitKey),Is.False);
                        Assert.That((long)Read(visits[collector],"RequestId"),Is.Zero);return;
                    }
                }
                if(current.Kind==TrafficDecisionKind.DeferredByBudget)return; // Budget scheduling alone proves no retirement or failure.
                Assert.That(current.Kind==TrafficDecisionKind.WaitingForTraffic && current.Blockers.Any(block=>block.Owner.ActorId==incoming),Is.False,
                    "No phase/body alias of this cancelled/disabled actor may promise finite release");
                var requestId=(long)Read(visits[collector],"RequestId");
                if(current.Kind==TrafficDecisionKind.Granted)
                {
                    var path=Read(exitEntry,"CurrentPath") as Vector3[];Assert.That(path,Is.Not.Null);Assert.That(path.Length,Is.GreaterThan(1));
                    var request=(TrafficRequest)Read(exitEntry,"Request");
                    foreach(var body in snapshot.Bodies.Where(body=>body.Owner.ActorId!=collector))
                        Assert.That(CustomerRoutePlanner.IsClear(path,new[]{body.Position},request.Radius+body.Radius+Math.Max(request.SafetyMargin,body.SafetyMargin)),Is.True);
                    foreach(var fact in snapshot.Parking.Where(fact=>fact.Owner.ActorId!=collector))
                        Assert.That(CustomerRoutePlanner.IsClear(path,new[]{fact.Position},request.Radius+fact.Radius+Math.Max(request.SafetyMargin,fact.SafetyMargin)),Is.True);
                    Assert.That(current.ProofRoutes.Any(proof=>proof.ResourceId==request.ResourceId && proof.ExpandedPath.Count>1),Is.True,
                        "Any safe alternative needs this actual original full Core proof");
                }
                if(requestId>0)
                {
                    var leg=Read(exitEntry,"Leg");Assert.That(leg,Is.Not.Null);var stamp=(TrafficNativeStamp)Read(leg,"Stamp");
                    var byActor=(IDictionary)Read(world.Service,"byActor");
                    Assert.That(Flag(leg,"Committed")&&Flag(leg,"Bound")&&!Flag(leg,"Completed")&&stamp.RequestId==requestId&&native.Contains(requestId),Is.True);
                    Assert.That(byActor.Contains(collector)&&ReferenceEquals(byActor[collector],native[requestId]),Is.True);
                    Assert.That(Read(leg,"ActorLife"),Is.SameAs(Read(visits[collector],"Actor")));
                }
                else Assert.That(((NavigationActor)Read(visits[collector],"Actor")).transform.position,Is.EqualTo(contributorCollectorPose));
                Assert.That(owner.Snapshot.Orders.Values.Single(order=>order.CustomerId==collector).State,Is.EqualTo(OrderState.Completed));
                records.Add("CORE_REAL_CONTRIBUTOR_RECHECK mode="+invalidation+" kind="+current.Kind+" reason="+current.Reason+
                    " oldNativeRemoved=True oldSourceRetired=True sameBody/Pqs/Tokens=True originalExitEntry=True actualNative="+requestId+" noFakeExits=True");
                contributorEvaluated=true;
            }
            void CheckDetourLedger()
            {
                Assert.That(entries[exitKey],Is.SameAs(exitEntry));Assert.That(Read(exitEntry,"FirstFrame"),Is.EqualTo(detourFirst));
                Assert.That(Read(exitEntry,"RequestSequence"),Is.EqualTo(detourSequence));Assert.That(Read(exitEntry,"ExplicitRecoveryUsed"),Is.EqualTo(false));
                Assert.That(Read(exitEntry,"FrozenOwners"),Is.SameAs(detourFrozen));Assert.That(Read(exitEntry,"FrozenResources"),Is.SameAs(detourResources));
                var q=core.QueriesConsumed(exitKey);Assert.That(q,Is.InRange(detourQueries,(int)Read(core,"queriesPerIntent")));detourQueries=q;
                var debt=(float)Read(exitEntry,"WaitRemaining");Assert.That(debt,Is.InRange(0f,detourDebt));detourDebt=debt;
                Assert.That(world.RegisteredActors.Contains(detourBody)&&detourBody.isActiveAndEnabled,Is.True);
                Assert.That(detourBody.transform.position,Is.EqualTo(detourPosition));
            }
            void ObserveDetourWait(object entry,TrafficDecision decision)
            {
                var jobs=(IList)Read(entry,"Jobs");Assert.That(jobs.Count,Is.GreaterThan(0));var job=jobs[0];
                Assert.That(Flag(job,"complete")&&!Flag(job,"HasUnfinishedBase"),Is.True,"PREMISE: a genuine complete original Core primary is required");
                var expanded=(Vector3[])Read(job,"ExpandedPath");var waypoints=(Vector3[])Read(job,"Waypoints");var legEnds=(int[])Read(job,"LegEnds");
                var request=(TrafficRequest)Read(entry,"Request");var clearance=request.Radius+detourBody.Settings.AgentRadius+
                    Math.Max(request.SafetyMargin,detourBody.Settings.CollisionSkin+detourBody.Settings.Epsilon);
                Assert.That(CustomerRoutePlanner.IsClear(new[]{request.Start,request.Target},new[]{detourPosition},clearance),Is.False,
                    "PREMISE: the fixed real body must obstruct the original direct exit chord");
                Assert.That(CustomerRoutePlanner.IsClear(expanded,new[]{detourPosition},clearance),Is.True,
                    "PREMISE: the original complete Core candidate must really detour clear of this body");
                Assert.That(expanded.Length,Is.GreaterThan(2));Assert.That(waypoints.Length,Is.InRange(1,24));
                Assert.That(legEnds.Length,Is.EqualTo(waypoints.Length));Assert.That(legEnds.Last(),Is.EqualTo(expanded.Length-1));
                if(detourPath==null)
                {
                    detourPath=expanded.ToArray();detourWaypoints=waypoints.ToArray();detourLegEnds=legEnds.ToArray();
                    detourFirst=(int)Read(entry,"FirstFrame");detourSequence=decision.RequestSequence;detourQueries=core.QueriesConsumed(exitKey);
                    detourDebt=(float)Read(entry,"WaitRemaining");Assert.That(detourDebt,Is.GreaterThan(0));
                    detourFrozen=Read(entry,"FrozenOwners");detourResources=Read(entry,"FrozenResources");
                }
                else
                {
                    CollectionAssert.AreEqual(detourPath,expanded,"The genuine original complete detour geometry must survive finite waiting");
                    CollectionAssert.AreEqual(detourWaypoints,waypoints);CollectionAssert.AreEqual(detourLegEnds,legEnds);
                }
                CheckDetourLedger();Assert.That((long)Read(visits[collector],"RequestId"),Is.Zero);
                Assert.That(decision.AcquiredSequence,Is.Zero);Assert.That(Read(entry,"Leg"),Is.Null);
                if(detourSampleFrame!=Time.frameCount){detourSampleFrame=Time.frameCount;detourWaitSamples++;}
            }
            void CheckLeaseLedger()
            {
                Assert.That(entries[exitKey],Is.SameAs(exitEntry));Assert.That(Read(exitEntry,"FirstFrame"),Is.EqualTo(leaseFirst));
                Assert.That(Read(exitEntry,"RequestSequence"),Is.EqualTo(leaseSequence));
                Assert.That(Read(exitEntry,"FrozenOwners"),Is.SameAs(leaseFrozen));Assert.That(Read(exitEntry,"FrozenResources"),Is.SameAs(leaseResources));
                var q=core.QueriesConsumed(exitKey);Assert.That(q,Is.InRange(leaseQueries,(int)Read(core,"queriesPerIntent")));leaseQueries=q;
                var debt=(float)Read(exitEntry,"WaitRemaining");Assert.That(debt,Is.InRange(0f,leaseDebt));leaseDebt=debt;
                Assert.That(Read(exitEntry,"ExplicitRecoveryUsed"),Is.EqualTo(false));
            }
            bool CheckHeldServiceLease(bool checkSnapshot=true)
            {
                Assert.That(visits[collector],Is.SameAs(heldCollector));Assert.That(Read(heldCollector,"PickupOwner"),Is.SameAs(heldPickup));
                Assert.That(Read(heldCollector,"Admission"),Is.SameAs(heldAdmission));
                Assert.That(heldPickup.TryGetServiceLease(collector,out var position)&&position.Equals(heldLeasePosition),Is.True);
                Assert.That(heldPickup.TryCaptureReservation(collector,out var current)&&current.IsCurrentCollectionLease&&
                    heldLeaseClaim.IsSameReservation(current)&&heldLeaseClaim.IsSameCurrentClaim(current),Is.True);
                var freshSnapshot=false;
                if(checkSnapshot)
                {
                    var snapshot=(TrafficWorldSnapshot)Read(core,"snapshot");
                    var fact=snapshot.Parking.Single(p=>p.ResourceId=="service/"+collector&&ReferenceEquals(p.CanonicalOwner,heldPickup));
                    Assert.That(fact.Position,Is.EqualTo(heldLeasePosition));Assert.That(fact.BusinessOwner,Is.SameAs(heldCollector));
                    Assert.That(fact.ReleaseObligation,Is.SameAs(heldAdmission));
                    // A later ordinary business mutation may precede the next Core snapshot publication.
                    // 版号落后只是不具备current snapshot证明；真实当前lease/claim/token仍严格核验。
                    if(fact.OwnerVersion==heldPickup.Version)
                    {
                        Assert.That(((ITrafficClaimCheckpoint)heldServiceFact.SemanticCheckpoint).IsSameCurrentClaim(
                            (ITrafficClaimCheckpoint)fact.SemanticCheckpoint),Is.True);freshSnapshot=true;
                    }
                }
                foreach(var pair in leaseTokenStates)
                    Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,pair.Key)).State,Is.EqualTo(pair.Value));
                return freshSnapshot;
            }
            void CaptureLeaseWait(TrafficDecision decision)
            {
                if(leaseWaitCaptured)return;
                heldCollector=visits[collector];heldAdmission=Read(heldCollector,"Admission");heldPickup=(PickupQueueService)Read(heldCollector,"PickupOwner");
                Assert.That(Flag(heldCollector,"HasCollected")&&!Flag(heldCollector,"PickupDeparted"),Is.True);
                Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==collector).State,Is.EqualTo(OrderState.Completed));
                Assert.That(heldPickup.TryGetServiceLease(collector,out heldLeasePosition),Is.True);
                Assert.That(heldPickup.TryCaptureReservation(collector,out heldLeaseClaim)&&heldLeaseClaim.IsCurrentCollectionLease,Is.True);
                heldServiceFact=((TrafficWorldSnapshot)Read(core,"snapshot")).Parking.Single(p=>p.ResourceId=="service/"+collector&&ReferenceEquals(p.CanonicalOwner,heldPickup));
                foreach(var r in customers.Capacity.GetReservations().Where(r=>r.OwnerId==collector))leaseTokenStates.Add(r.Token,r.State);
                Assert.That(leaseTokenStates.Count,Is.EqualTo(3));
                leaseFrozen=Read(exitEntry,"FrozenOwners");leaseResources=Read(exitEntry,"FrozenResources");
                leaseFirst=(int)Read(exitEntry,"FirstFrame");leaseSequence=(long)Read(exitEntry,"RequestSequence");
                leaseQueries=core.QueriesConsumed(exitKey);leaseDebt=(float)Read(exitEntry,"WaitRemaining");Assert.That(leaseDebt,Is.GreaterThan(0));
                leaseWaitDecision=decision;leaseStamp=(TrafficNativeStamp)Read(earlierLeg,"Stamp");leaseOriginalNative=native[leaseStamp.RequestId];
                var snapshot=(TrafficWorldSnapshot)Read(core,"snapshot");leaseWaitBodyVersion=snapshot.BodyVersion;
                leaseWaitOwnerPosition=snapshot.Bodies.Single(body=>ReferenceEquals(body.ActorLife,earlierBody)&&body.Owner.Equals(earlierKey)).Position;
                CheckHeldServiceLease();CheckLeaseLedger();leaseWaitCaptured=true;
                records.Add("REAL_SERVICE_LEASE_WAIT originalExit="+exitKey+" ownerNative="+leaseStamp.RequestId+"/"+leaseStamp.LegGeneration+
                    " actualCollectionClaim=True originalTokens=True cacheIsNotAuthority=True");
            }
            bool ObserveOriginalLeaseOwnerProgress()
            {
                if(leaseNativeAdvanced)return true;
                if(!entries.Contains(earlierKey)||!ReferenceEquals(entries[earlierKey],earlierEntry)||Flag(earlierEntry,"Retired")||
                    !ReferenceEquals(Read(earlierEntry,"JourneyPermit"),earlierSource)||!ReferenceEquals(Read(earlierEntry,"Leg"),earlierLeg)||
                    !ReferenceEquals(Read(earlierLeg,"Proof"),earlierProof)||!ReferenceEquals(Read(earlierLeg,"ActorLife"),earlierBody)||
                    !Flag(earlierLeg,"Committed")||!Flag(earlierLeg,"Bound")||Flag(earlierLeg,"Completed")||Flag(earlierLeg,"Suspended")||
                    !((TrafficNativeStamp)Read(earlierLeg,"Stamp")).Equals(leaseStamp)||!native.Contains(leaseStamp.RequestId)||
                    !ReferenceEquals(native[leaseStamp.RequestId],leaseOriginalNative)||(long)Read(visits[incoming],"RequestId")!=leaseStamp.RequestId)return false;
                var byActor=(IDictionary)Read(world.Service,"byActor");
                if(!byActor.Contains(incoming)||!ReferenceEquals(byActor[incoming],leaseOriginalNative))return false;
                var snapshot=(TrafficWorldSnapshot)Read(core,"snapshot");
                var fact=snapshot.Bodies.SingleOrDefault(body=>ReferenceEquals(body.ActorLife,earlierBody)&&body.Owner.Equals(earlierKey));
                if(fact==null||!fact.Enabled||!fact.ActorActive||snapshot.BodyVersion<=leaseWaitBodyVersion||fact.Position.Equals(leaseWaitOwnerPosition))return false;
                leaseNativeAdvanced=true;return true; // Actual same Native motion, not an invented Arrived/Completed milestone.
            }
            void LeaseReceipt(string operation,string id,string other,Vector3 position,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                if(!observing||!leaseWaitCaptured)return;
                try
                {
                    if(id==incoming&&!leaseReleased&&!Flag(heldCollector,"PickupDeparted")&&(operation=="segment.callback-result"||operation=="segment.callback")&&ReferenceEquals(Read(earlierEntry,"Leg"),earlierLeg))
                    {
                        Assert.That(Read(earlierEntry,"JourneyPermit"),Is.SameAs(earlierSource));Assert.That(Read(earlierLeg,"Proof"),Is.SameAs(earlierProof));
                        Assert.That(Read(earlierLeg,"ActorLife"),Is.SameAs(earlierBody));Assert.That((TrafficNativeStamp)Read(earlierLeg,"Stamp"),Is.EqualTo(leaseStamp));
                        if(operation=="segment.callback-result")
                        {
                            var text=" "+result+" ";Assert.That(text.Contains(" status=Arrived ")&&text.Contains(" retry=0 ")&&
                                text.Contains(" request="+leaseStamp.RequestId+" ")&&text.Contains(" issuedGeneration="+leaseStamp.LegGeneration+" "),Is.True);
                            leaseNativeArrived=true;
                        }
                        else
                        {
                            Assert.That(leaseNativeArrived&&result=="Arrived"&&Flag(earlierLeg,"Completed"),Is.True);
                            CheckHeldServiceLease(false);CheckLeaseLedger();leaseCheckpoint=true;
                        }
                    }
                    if(id==collector&&operation=="lease.departure-before-remove")
                    {
                        Assert.That(leaseRevalidated&&leaseBoundSampled,Is.True,"PREMISE: real revalidation and Bound must precede actual departure");
                        var actor=(NavigationActor)Read(heldCollector,"Actor");var s=actor.Settings;
                        Assert.That(Vector3.Distance(actor.transform.position,(Vector3)Read(heldCollector,"PickupPosition")),
                            Is.GreaterThan(2*s.AgentRadius+s.CollisionSkin+s.Epsilon+s.ArrivalDistance));
                        Assert.That(heldPickup.TryGetServiceLease(collector,out var retained)&&retained.Equals(heldLeasePosition),Is.True);
                        Assert.That(heldPickup.TryCaptureReservation(collector,out var claim)&&heldLeaseClaim.IsSameReservation(claim)&&claim.IsCurrentCollectionLease,Is.True);
                        leaseDepartureSeen=true;
                    }
                    if(id==collector&&operation=="lease.departure-removed")
                    {
                        Assert.That(leaseDepartureSeen&&Flag(heldCollector,"PickupDeparted"),Is.True);
                        Assert.That(heldPickup.TryGetServiceLease(collector,out _),Is.False);Assert.That(heldPickup.TryCaptureReservation(collector,out _),Is.False);leaseReleased=true;
                    }
                }
                catch(Exception error){if(errors.Count<3)errors.Add(error.ToString());}
            }
            void TracePredicate(bool collectorCanCollect)
            {
                if(capture)return;
                var visit=visits.Contains(incoming)?visits[incoming]:null;
                var paid=visit==null?null:Read(visit,"PaidPickup");var move=visit==null?null:Read(visit,"TrafficMove");
                var key=move==null?default:(TrafficIntentKey)Read(move,"Intent");
                var entry=move!=null && entries.Contains(key)?entries[key]:null;
                var source=move==null?null:Read(move,"Source") as TrafficDecision;
                var leg=entry==null?null:Read(entry,"Leg");var proof=move==null?null:Read(move,"Proof");
                var body=visit==null?null:Read(visit,"Actor") as NavigationActor;
                var collectorVisit=visits.Contains(collector)?visits[collector]:null;
                string Value(object value,string name)=>value==null?"NULL":Convert.ToString(Read(value,name));
                var stamp=leg==null?default:(TrafficNativeStamp)Read(leg,"Stamp");
                var predicate="collectorCanCollect="+collectorCanCollect+" collectorState="+Value(collectorVisit,"State")+
                    " serviceArrived="+Value(collectorVisit,"ServiceArrived")+" incomingPresent="+(visit!=null)+
                    " incomingState="+Value(visit,"State")+" paidPresent="+(paid!=null)+" accepted="+Value(paid,"Accepted")+
                    " transferred="+Value(paid,"Transferred")+" paidFailed="+Value(paid,"Failed")+
                    " movePresent="+(move!=null)+" moveKey="+(move==null?"NULL":key.ToString())+
                    " sourcePresent="+(source!=null)+" sourceKey="+(source==null?"NULL":source.Intent.ToString())+
                    " acq="+(source==null?"NULL":source.AcquiredSequence.ToString())+
                    " entryPresent="+(entry!=null)+" retired="+Value(entry,"Retired")+
                    " samePermit="+(source!=null && entry!=null && ReferenceEquals(Read(entry,"JourneyPermit"),source))+
                    " proofPresent="+(proof!=null)+" proofInSource="+(proof!=null && source!=null && source.ProofRoutes.Any(p=>ReferenceEquals(p,proof)))+
                    " legPresent="+(leg!=null)+" index="+Value(leg,"Index")+" moveIndex="+Value(move,"Index")+
                    " generation="+Value(leg,"Generation")+" moveGeneration="+Value(move,"NativeGeneration")+
                    " sameProof="+(proof!=null && leg!=null && ReferenceEquals(Read(leg,"Proof"),proof))+
                    " sameLife="+(body!=null && leg!=null && ReferenceEquals(Read(leg,"ActorLife"),body))+
                    " actorActive="+(body!=null && body.isActiveAndEnabled)+" queued="+Value(entry,"Queued")+
                    " prepared="+Value(entry,"Prepared")+" active="+Value(entry,"Active")+
                    " committed="+Value(leg,"Committed")+" bound="+Value(leg,"Bound")+
                    " completed="+Value(leg,"Completed")+" suspended="+Value(leg,"Suspended")+
                    " needsQuery="+Value(leg,"NeedsQuery")+" request="+Value(visit,"RequestId")+
                    " stampRequest="+stamp.RequestId+" stampGeneration="+stamp.LegGeneration;
                if(predicate==lastPredicate)return;
                lastPredicate=predicate;predicateTransitions++;
                if(predicateTransitions<=32)records.Add("CORE_UNISSUED_PREDICATE transition="+predicateTransitions+" frame="+Time.frameCount+" "+predicate);
                else predicateOverflow=true;
            }
            void CollectOriginalOnce()
            {
                attempted=true;var held=owner.HoldCollection;owner.HoldCollection=false;
                try{collected=service.TryCollect(collector,out var reason);records.Add("CORE_UNISSUED_COLLECT accepted="+collected+" reason="+reason);}
                finally{owner.HoldCollection=held;}
            }
            bool OriginalIncomingBoundForAdmission()
            {
                if(!capture||collected||earlierEntry==null||earlierLeg==null||!entries.Contains(earlierKey)||
                    !ReferenceEquals(entries[earlierKey],earlierEntry)||!ReferenceEquals(Read(earlierEntry,"Leg"),earlierLeg)||
                    !ReferenceEquals(Read(earlierEntry,"JourneyPermit"),earlierSource)||Flag(earlierEntry,"Retired")||
                    !Flag(earlierLeg,"Committed")||!Flag(earlierLeg,"Bound")||Flag(earlierLeg,"Completed")||Flag(earlierLeg,"Suspended"))return false;
                var stamp=(TrafficNativeStamp)Read(earlierLeg,"Stamp");var byActor=(IDictionary)Read(world.Service,"byActor");
                if(stamp.RequestId<=0||!native.Contains(stamp.RequestId)||!byActor.Contains(incoming)||
                    !ReferenceEquals(byActor[incoming],native[stamp.RequestId])||(long)Read(visits[incoming],"RequestId")!=stamp.RequestId)return false;
                Assert.That(Read(earlierLeg,"Proof"),Is.SameAs(earlierProof));Assert.That(Read(earlierLeg,"ActorLife"),Is.SameAs(earlierBody));
                Assert.That(Read(earlierEntry,"FirstFrame"),Is.EqualTo(earlierFirst));
                priorityStamp=stamp;return true;
            }
            IEnumerator ObserveUnbornCurrentRouteRefusal(int firstWindowFrame)
            {
                const string premise="ADMISSION_CURRENT_ROUTE_NOT_PROVEN: ";
                var queue=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue");
                var originalSlots=((Vector3[])Read(queue,"slots")).ToArray();
                var originalClock=CafeLoopRuntimeProof.Field<CustomerSpawnClock>(customers,"clock");
                var originalAuto=customers.AutoSpawn;var savedScale=Time.timeScale;var savedCapture=Time.captureDeltaTime;
                var savedStepping=CafeLoopRuntimeProof.Field<bool>(owner,"stepping");
                var clock=new CustomerSpawnClock(()=>.1f);object anchor=null,admissionEntry=null;
                TrafficIntentKey admissionKey=default;var observationComplete=false;
                object pinnedJob=null;TrafficRequest pinnedRequest=null;var pinnedSequence=0L;var jobReplacements=0;var pinObservations=0;
                void PinOriginalJob(CafeLoopSnapshot ignored)
                {
                    if(admissionEntry==null)return;
                    try
                    {
                        Assert.That(entries[admissionKey],Is.SameAs(admissionEntry));var jobs=(IList)Read(admissionEntry,"Jobs");
                        if(jobs.Count==0)return; // Hold clears the list, not the original referenced RouteJob object.
                        if(pinnedJob!=null&&!ReferenceEquals(pinnedJob,jobs[0]))jobReplacements++;
                        pinnedJob=jobs[0];pinnedRequest=(TrafficRequest)Read(admissionEntry,"Request");pinnedSequence=(long)Read(admissionEntry,"RequestSequence");pinObservations++;
                    }
                    catch(Exception error){if(errors.Count<3)errors.Add(error.ToString());}
                }
                try
                {
                    // Freeze only real simulation time while the original owner prepares this one source.
                    // 只冻结模拟时间；Core仍由normal owner准备，未出生机会不获Native/业务权限。
                    CafeLoopRuntimeProof.Set(owner,"stepping",true);Time.captureDeltaTime=0;Time.timeScale=0;
                    for(var transition=0;Time.deltaTime!=0 && transition<3;transition++)yield return null;
                    Assert.That(Time.deltaTime,Is.Zero,premise+"zero-delta frame");
                    CafeLoopRuntimeProof.Set(owner,"stepping",false);yield return null;
                    Assert.That(OriginalIncomingBoundForAdmission(),Is.True,premise+"original real paid Bound Native before collection");
                    Assert.That(attempted||collected,Is.False);Assert.That(Read(visits[collector],"HasCollected"),Is.EqualTo(false));
                    Assert.That(Read(visits[collector],"ServiceArrived"),Is.EqualTo(true));
                    Assert.That(owner.Snapshot.Orders.Values.Single(order=>order.CustomerId==collector).State,Is.EqualTo(OrderState.ReadyForPickup));
                    Assert.That(entries.Keys.Cast<TrafficIntentKey>().Any(k=>k.ActorId==collector&&k.IntentId=="actual-exit"&&!Flag(entries[k],"Retired")),Is.False,
                        premise+"do not install admission behind an already-collected Exit readiness gate");
                    Assert.That(Read(earlierEntry,"Leg"),Is.SameAs(earlierLeg));Assert.That(Flag(earlierLeg,"Bound")&&!Flag(earlierLeg,"Completed"),Is.True);
                    Assert.That(Read(earlierEntry,"JourneyPermit"),Is.SameAs(earlierSource));
                    Assert.That(queue.Snapshot.Count,Is.EqualTo(1),premise+"only original paid guest still owns Counter");
                    Assert.That(queue.Snapshot[0].VisitId,Is.EqualTo(incoming));
                    var beforeRows=queue.Snapshot.ToArray();var nextSlot=beforeRows.Length;
                    Assert.That(nextSlot,Is.LessThan(originalSlots.Length));
                    var nextSlots=originalSlots.ToArray();var tail=new Vector3(3.5f,0,-.5f);nextSlots[nextSlot]=tail;
                    Assert.That(adapter.IsPointWalkable(tail),Is.True,premise+"one fixed unpublished NEXT input; no replacement search");
                    queue.UpdateSlots(nextSlots);CollectionAssert.AreEqual(beforeRows,queue.Snapshot);
                    var nextVisit=CafeLoopRuntimeProof.Field<long>(customers,"nextVisit");
                    var beforeIds=customers.Snapshot.Select(row=>row.VisitId).ToArray();var beforeBodies=world.RegisteredActors.ToArray();
                    var beforePaid=owner.Snapshot.PaidVisitIds.ToArray();var beforeOrders=owner.Snapshot.Orders.Keys.ToArray();
                    var beforeTokens=customers.Capacity.GetReservations().Select(row=>(row.Token,row.State)).ToArray();
                    var beforeNative=native.Keys.Cast<object>().ToArray();var beforeNextNative=CafeLoopRuntimeProof.Field<long>(world.Service,"nextId");
                    var sourceAge=Read(earlierEntry,"FirstFrame");var originalQueries=core.QueriesConsumed(earlierKey);
                    void NoPublication()
                    {
                        Assert.That(Time.frameCount-firstWindowFrame,Is.LessThan(6000),premise+"same original 6000 preparation window");
                        Assert.That(errors,Is.Empty,string.Join("\n",errors));Assert.That(Time.deltaTime,Is.Zero);
                        Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.EqualTo(nextVisit));
                        CollectionAssert.AreEquivalent(beforeIds,customers.Snapshot.Select(row=>row.VisitId));
                        CollectionAssert.AreEquivalent(beforeBodies,world.RegisteredActors);CollectionAssert.AreEqual(beforeRows,queue.Snapshot);
                        CollectionAssert.AreEqual(beforePaid,owner.Snapshot.PaidVisitIds);CollectionAssert.AreEqual(beforeOrders,owner.Snapshot.Orders.Keys);
                        CollectionAssert.AreEqual(beforeTokens,customers.Capacity.GetReservations().Select(row=>(row.Token,row.State)));
                        CollectionAssert.AreEquivalent(beforeNative,native.Keys.Cast<object>());
                        Assert.That(CafeLoopRuntimeProof.Field<long>(world.Service,"nextId"),Is.EqualTo(beforeNextNative));
                        Assert.That(Read(earlierEntry,"JourneyPermit"),Is.SameAs(earlierSource));Assert.That(Read(earlierEntry,"Leg"),Is.SameAs(earlierLeg));
                        Assert.That(Read(earlierEntry,"FirstFrame"),Is.EqualTo(sourceAge));Assert.That(core.QueriesConsumed(earlierKey),Is.EqualTo(originalQueries));
                        Assert.That(Read(visits[collector],"HasCollected"),Is.EqualTo(false));Assert.That(attempted||collected,Is.False);
                    }
                    CafeLoopRuntimeProof.Set(customers,"clock",clock);Assert.That(clock.Tick(1,true,false),Is.True);
                    tools.ResumeArrivals();customers.Step(1f/60); // One real automatic Submit; never manual TrySpawn or Core ProcessReady.
                    NoPublication();anchor=Read(customers,"pendingAdmissionAnchor");Assert.That(anchor,Is.Not.Null,premise+"actual automatic anchor");
                    admissionKey=(TrafficIntentKey)Read(anchor,"Key");Assert.That(admissionKey.IntentId,Is.EqualTo("counter-admission"));
                    Assert.That(Read(anchor,"Automatic"),Is.EqualTo(true));Assert.That(Read(anchor,"Submitted"),Is.EqualTo(true),premise+"not only a pre-submit endpoint gate");
                    Assert.That(Read(anchor,"Owner"),Is.SameAs(queue));Assert.That(Read(anchor,"Coordinator"),Is.SameAs(core));
                    Assert.That(entries.Contains(admissionKey),Is.True);admissionEntry=entries[admissionKey];
                    owner.SnapshotChanged+=PinOriginalJob;PinOriginalJob(owner.Snapshot);
                    var first=Read(admissionEntry,"FirstFrame");long sequence=0;var queries=core.QueriesConsumed(admissionKey);
                    var finite=Read(admissionEntry,"FiniteLimit");var debt=0f;var debtCaptured=false;
                    while(true)
                    {
                        NoPublication();Assert.That(Read(customers,"pendingAdmissionAnchor"),Is.SameAs(anchor));Assert.That(entries[admissionKey],Is.SameAs(admissionEntry));
                        Assert.That(Read(admissionEntry,"FirstFrame"),Is.EqualTo(first));Assert.That(Read(admissionEntry,"FiniteLimit"),Is.EqualTo(finite));
                        if(Read(admissionEntry,"FrozenOwners")!=null || Flag(admissionEntry,"AdmissionWaitStarted"))
                        {
                            var remaining=(float)Read(admissionEntry,"WaitRemaining");
                            if(debtCaptured)Assert.That(remaining,Is.InRange(0f,debt));else Assert.That(remaining,Is.GreaterThan(0));
                            debt=remaining;debtCaptured=true;
                        }
                        var decision=core.GetDecision(admissionKey);if(sequence==0)sequence=decision.RequestSequence;
                        Assert.That(decision.RequestSequence,Is.EqualTo(sequence));Assert.That(decision.AcquiredSequence,Is.Zero);
                        Assert.That(core.HasActiveGrant(admissionKey),Is.False);Assert.That(Read(admissionEntry,"Leg"),Is.Null);Assert.That(Read(admissionEntry,"JourneyPermit"),Is.Null);
                        Assert.That(core.QueriesConsumed(admissionKey),Is.InRange(queries,16384));queries=core.QueriesConsumed(admissionKey);
                        var request=(TrafficRequest)Read(admissionEntry,"Request");Assert.That(request.Intent,Is.EqualTo(admissionKey));Assert.That(request.Target,Is.EqualTo(tail));
                        Assert.That(visits.Contains(admissionKey.ActorId),Is.False);Assert.That(request.Snapshot.Bodies.Any(body=>body.Owner.ActorId==admissionKey.ActorId),Is.False);
                        Assert.That(request.Snapshot.Parking.Any(fact=>fact.Owner.ActorId==admissionKey.ActorId),Is.False);
                        Assert.That(customers.Capacity.GetReservations().Any(row=>row.OwnerId==admissionKey.ActorId),Is.False);
                        var typedHeld=decision.Kind==TrafficDecisionKind.DeferredByBudget&&decision.Reason=="admission-eligibility/route-capability-unavailable";
                        if(decision.Kind==TrafficDecisionKind.WaitingForTraffic||typedHeld)
                        {
                            object originalJob;
                            if(typedHeld)
                            {
                                Assert.That(Read(admissionEntry,"UnpaidEligibility"),Is.Null);Assert.That(Read(admissionEntry,"AdmissionReadiness"),Is.Not.Null);
                                Assert.That(Flag(admissionEntry,"UnpaidEligibilityHeld"),Is.True);Assert.That(Read(admissionEntry,"UnpaidEligibilitySpatialRefusal"),Is.SameAs(decision));
                                Assert.That(Read(admissionEntry,"FrozenOwners"),Is.Null,"Preparation hold must not invent WAIT owners");
                                TestContext.WriteLine("ADMISSION_HELD_ORIGINAL jobPinned="+(pinnedJob!=null)+" observations="+pinObservations+" replacements="+jobReplacements+
                                    " blockers=["+string.Join(";",decision.Blockers.Select(block=>block.Owner.ActorId+"/"+block.Owner.IntentId+":"+block.ResourceId+":"+block.Proof))+"] queries="+queries);
                                Assert.That(decision.Blockers.Any(block=>block.Owner.Equals(earlierKey)&&block.Proof=="current-conflict-segment"),Is.True,
                                    premise+"held spatial failure must retain actual current-route blocker; an empty/static-role refusal is not this feature");
                                Assert.That(pinnedJob,Is.Not.Null,premise+"no normal-frame original primary job observed before Hold cleared the list");
                                Assert.That(jobReplacements,Is.Zero,premise+"fixed zero-time facts must retain the observed original primary");
                                Assert.That(pinnedSequence,Is.EqualTo(sequence));Assert.That(pinnedRequest.Intent,Is.EqualTo(admissionKey));
                                Assert.That(pinnedRequest.Snapshot.Version,Is.EqualTo(request.Snapshot.Version));Assert.That(pinnedRequest.Snapshot.BodyVersion,Is.EqualTo(request.Snapshot.BodyVersion));
                                originalJob=pinnedJob;
                            }
                            else {var jobs=(IList)Read(admissionEntry,"Jobs");Assert.That(jobs.Count,Is.GreaterThan(0));originalJob=jobs[0];}
                            Assert.That(Flag(originalJob,"complete"),Is.True,premise+"original actual primary must be complete");
                            Assert.That(Read(originalJob,"start"),Is.EqualTo(request.Start));Assert.That(Read(originalJob,"goal"),Is.EqualTo(request.Target));
                            var full=(Vector3[])Read(originalJob,"ExpandedPath");var current=(Vector3[])Read(earlierEntry,"CurrentPath");
                            Assert.That(full.Length,Is.GreaterThan(1));Assert.That(current.Length,Is.GreaterThan(1));
                            var ownerRequest=(TrafficRequest)Read(earlierEntry,"Request");var clearance=request.Radius+ownerRequest.Radius+Math.Max(request.SafetyMargin,ownerRequest.SafetyMargin);
                            Assert.That(CustomerRoutePlanner.IsClear(current,new[]{tail},clearance),Is.True,premise+"endpoint alone must not cause rejection");
                            foreach(var body in request.Snapshot.Bodies)Assert.That(CustomerRoutePlanner.IsClear(new[]{tail},new[]{body.Position},request.Radius+body.Radius+Math.Max(request.SafetyMargin,body.SafetyMargin)),Is.True,premise+"endpoint BODY clear");
                            foreach(var fact in request.Snapshot.Parking)Assert.That(CustomerRoutePlanner.IsClear(new[]{tail},new[]{fact.Position},request.Radius+fact.Radius+Math.Max(request.SafetyMargin,fact.SafetyMargin)),Is.True,premise+"endpoint canonical clear");
                            Assert.That((bool)pathsConflict.Invoke(null,new object[]{full,current,clearance}),Is.True,premise+"actual complete initial route crosses actual current MOVE");
                            Assert.That(decision.Blockers.Any(block=>block.Owner.Equals(earlierKey)&&block.Proof=="current-conflict-segment"&&block.ResourceId==ownerRequest.ResourceId),Is.True,
                                premise+"actual blocker must be this issued source, not an unissued future-only right");
                            Assert.That(native.Contains(priorityStamp.RequestId),Is.True);Assert.That(Read(earlierLeg,"Stamp"),Is.EqualTo(priorityStamp));
                            customers.Step(1f/60);NoPublication();Assert.That(clock.RemainingSeconds,Is.Zero);
                            Assert.That(Read(customers,"pendingAdmissionAnchor"),Is.SameAs(anchor));Assert.That(core.GetDecision(admissionKey).AcquiredSequence,Is.Zero);
                            records.Add("REAL_UNBORN_CURRENT_ROUTE_REFUSAL key="+admissionKey.ActorId+" source="+earlierKey.ActorId+" wholeRoute=["+string.Join(";",full)+"] current=["+string.Join(";",current)+"] endpointSafe=True noPublication=True queries="+queries);
                            break;
                        }
                        Assert.That(decision.Kind,Is.EqualTo(TrafficDecisionKind.DeferredByBudget),premise+decision.Kind+"/"+decision.Reason);
                        Assert.That(decision.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None),premise+decision.Reason);
                        Assert.That(new[]{"ready-batch-pending","canonical-anchor-ready-batch-pending","native-query-frame-budget"},Has.Member(decision.Reason),
                            premise+"unexpected original stage: "+decision.Kind+"/"+decision.Reason+" job="+Read(admissionEntry,"JobIndex")+" queries="+queries);
                        yield return null;
                    }
                    tools.HoldArrivals();Assert.That(Read(customers,"pendingAdmissionAnchor"),Is.Null);
                    Assert.That(Flag(admissionEntry,"Retired"),Is.True);Assert.That(core.HasActiveGrant(admissionKey),Is.False);NoPublication();observationComplete=true;
                }
                finally
                {
                    owner.SnapshotChanged-=PinOriginalJob;
                    if(!observationComplete)
                    {
                        // Nested assertion failure must not turn scene destruction into a fake business exit.
                        observing=false;tracking=false;owner.SnapshotChanged-=Observe;
                        foreach(var observer in observers)if(observer!=null)observer.Receipt=null;
                    }
                    tools.HoldArrivals();queue.UpdateSlots(originalSlots);CafeLoopRuntimeProof.Set(customers,"clock",originalClock);
                    customers.AutoSpawn=originalAuto;CafeLoopRuntimeProof.Set(owner,"stepping",savedStepping);Time.captureDeltaTime=savedCapture;Time.timeScale=savedScale;
                }
            }

            void Observe(CafeLoopSnapshot ignored)
            {
                if(!observing)return;
                if((invalidation==5 || invalidation==6) && contributorApplied)return; // Ordinary coroutine owns the new lifecycle inputs/recheck.
                try
                {
                    Assert.That(ReferenceEquals(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic"),core) &&
                        ReferenceEquals(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic"),core),Is.True,
                        "Fixture authority changed: do not observe an earlier epoch/Core");
                    var collectorCanCollect=customers.CanCollectPickup(collector);
                    TracePredicate(collectorCanCollect);
                    if(!capture && visits.Contains(incoming) && collectorCanCollect)
                    {
                        var visit=visits[incoming];var paid=Read(visit,"PaidPickup");var move=Read(visit,"TrafficMove");
                        if(paid==null || move==null || !Flag(paid,"Accepted") || !Flag(paid,"Transferred") || Flag(paid,"Failed"))return;
                        var key=(TrafficIntentKey)Read(move,"Intent");if(key.IntentId!="pickup-paid-movement" || !entries.Contains(key))return;
                        var entry=entries[key];var leg=Read(entry,"Leg");var source=Read(move,"Source") as TrafficDecision;
                        var body=Read(visit,"Actor") as NavigationActor;var proof=Read(move,"Proof") as TrafficRouteProof;
                        if(leg==null || source==null || proof==null || body==null || !body.isActiveAndEnabled || Flag(entry,"Retired") ||
                            source.AcquiredSequence<=0 || !ReferenceEquals(Read(entry,"JourneyPermit"),source) ||
                            !ReferenceEquals(Read(leg,"Proof"),proof) || !source.ProofRoutes.Any(p=>ReferenceEquals(p,proof)) ||
                            !ReferenceEquals(Read(leg,"ActorLife"),body) || (int)Read(leg,"Index")!=(int)Read(move,"Index") ||
                            (int)Read(leg,"Generation")!=(int)Read(move,"NativeGeneration") ||
                            Flag(leg,"Committed") || Flag(leg,"Bound") || Flag(leg,"Completed") || Flag(leg,"Suspended") ||
                            (!Flag(entry,"Queued") && !Flag(entry,"Prepared")) || (long)Read(visit,"RequestId")!=0 ||
                            !((TrafficNativeStamp)Read(leg,"Stamp")).Equals(default(TrafficNativeStamp)))return;
                        // Timing hint only: use this actual current leg, never the full future suffix.
                        // 只安排真实领取时机；最终冲突仍由原actual-exit完整primary证明。
                        var legPath=Read(leg,"Path") as Vector3[];
                        var collectorBody=(NavigationActor)Read(visits[collector],"Actor");
                        var chord=new[]{collectorBody.transform.position,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position};
                        var hintClearance=body.Settings.AgentRadius+collectorBody.Settings.AgentRadius+
                            Math.Max(body.Settings.CollisionSkin+body.Settings.Epsilon,collectorBody.Settings.CollisionSkin+collectorBody.Settings.Epsilon);
                        var timingHint=legPath!=null && legPath.Length>1 && (bool)pathsConflict.Invoke(null,new object[]{legPath,chord,hintClearance});
                        var hintLeg=Read(leg,"Index")+"/"+Read(leg,"Generation")+"/"+timingHint;
                        if(hintLeg!=lastHintLeg)
                        {
                            lastHintLeg=hintLeg;
                            if(hintRecords<8)
                            {
                                hintRecords++;records.Add("CORE_UNISSUED_TIMING_HINT authority=False leg="+Read(leg,"Index")+
                                    " generation="+Read(leg,"Generation")+" intersectsChord="+timingHint+
                                    " path=["+string.Join(";",(legPath??Array.Empty<Vector3>()).Select(p=>p.ToString("F6")))+"] chord=["+
                                    string.Join(";",chord.Select(p=>p.ToString("F6")))+"]");
                            }
                            else hintOverflow=true;
                        }
                        if(!timingHint)return;
                        capture=true;earlierKey=key;earlierEntry=entry;earlierLeg=leg;earlierSource=source;earlierProof=proof;
                        earlierBody=body;earlierFirst=(int)Read(entry,"FirstFrame");
                        if(requireAcquiredPriority){prioritySourceSequence=source.RequestSequence;priorityAcquired=source.AcquiredSequence;priorityOwnerQueries=core.QueriesConsumed(key);}
                        records.Add("CORE_UNISSUED_CAPTURE key="+key+" leg="+Read(leg,"Index")+" generation="+Read(leg,"Generation")+
                            " acquired="+source.AcquiredSequence+" first="+earlierFirst+" request=0 bound=False frame="+Time.frameCount);
                        // Ordinary business event, once. No diagnostic callback or private movement mutation.
                        // 仅正常业务事件中真实领取一次；Core和Native继续由原driver推进。
                        if(!requireAdmissionCurrentRoute)CollectOriginalOnce();
                        if((disableBeforeIssue || invalidation==2) && collected)
                        {
                            var originals=new List<DictionaryEntry>();
                            foreach(DictionaryEntry row in entries)
                                if(((TrafficIntentKey)row.Key).ActorId==collector && ((TrafficIntentKey)row.Key).IntentId=="actual-exit" &&
                                    !Flag(row.Value,"Retired"))originals.Add(row);
                            Assert.That(originals.Count,Is.EqualTo(1),"Real collection must first create its original actual-exit source");
                            exitEntry=originals[0].Value;exitKey=(TrafficIntentKey)originals[0].Key;exitFirst=(int)Read(exitEntry,"FirstFrame");
                            Assert.That((long)Read(visits[collector],"RequestId"),Is.EqualTo(0L));
                            disabledPose=body.transform.position;disabledFrame=Time.frameCount;
                            var capacities=customers.Capacity.GetCapacities().Select(c=>c.Used).ToArray();
                            var fifo=owner.Snapshot.PaidVisitIds.ToArray();
                            var pickup=(PickupQueueService)Read(visit,"PickupOwner");
                            Assert.That(pickup.TryGetReservation(incoming,out var originalClaim),Is.True);
                            if(invalidation==2)actualObstruction=ObstructOriginalCurrentLeg();
                            else body.enabled=false; // Existing component-disable base remains its original input.
                            disabledApplied=true; // Same real collection event, before the next normal frame.
                            Assert.That(body.transform.position,Is.EqualTo(disabledPose));
                            Assert.That((long)Read(visit,"RequestId"),Is.EqualTo(0L));
                            Assert.That(((TrafficNativeStamp)Read(leg,"Stamp")).Equals(default(TrafficNativeStamp)),Is.True);
                            CollectionAssert.AreEqual(capacities,customers.Capacity.GetCapacities().Select(c=>c.Used));
                            CollectionAssert.AreEqual(fifo,owner.Snapshot.PaidVisitIds);
                            Assert.That(pickup.TryGetReservation(incoming,out var retainedClaim) && retainedClaim.Equals(originalClaim),Is.True);
                            Assert.That(receipts,Is.Empty,"Disable cannot fabricate a departure");
                        }
                    }
                    if(!capture || !collected || !visits.Contains(collector))return;
                    if((disableBeforeIssue || invalidation!=0) && disabledApplied){ObserveDisabledSource();return;}
                    foreach(DictionaryEntry row in entries)
                    {
                        var key=(TrafficIntentKey)row.Key;if(key.ActorId!=collector || key.IntentId!="actual-exit")continue;
                        var entry=row.Value;if(Flag(entry,"Retired"))continue;
                        if(exitEntry==null){exitEntry=entry;exitKey=key;exitFirst=(int)Read(entry,"FirstFrame");}
                        if(!ReferenceEquals(entry,exitEntry) || !key.Equals(exitKey))throw new InvalidOperationException("Original actual-exit source replaced");
                        var decision=core.GetDecision(key);var jobs=(IList)Read(entry,"Jobs");Vector3[] actual=null;
                        if((int)Read(entry,"JobIndex")>0 && jobs.Count>0 && Flag(jobs[0],"complete"))actual=Read(jobs[0],"ExpandedPath") as Vector3[];
                        if(actual==null || actual.Length<2)
                            actual=decision.ProofRoutes.FirstOrDefault(p=>p.ResourceId==((TrafficRequest)Read(entry,"Request")).ResourceId)?.ExpandedPath.ToArray();
                        if(actual!=null && actual.Length>1)
                        {
                            candidateSeen=true;
                            var sameEarlier=entries.Contains(earlierKey) && ReferenceEquals(entries[earlierKey],earlierEntry) &&
                                ReferenceEquals(Read(earlierEntry,"JourneyPermit"),earlierSource) && ReferenceEquals(Read(earlierEntry,"Leg"),earlierLeg) &&
                                ReferenceEquals(Read(earlierLeg,"Proof"),earlierProof) && ReferenceEquals(Read(earlierLeg,"ActorLife"),earlierBody) &&
                                !Flag(earlierEntry,"Retired") && !Flag(earlierLeg,"Completed") && (Flag(earlierEntry,"Active") || Flag(earlierEntry,"Prepared"));
                            var current=Read(earlierEntry,"CurrentPath") as Vector3[];
                            var collectorBody=(NavigationActor)Read(visits[collector],"Actor");
                            var clearance=earlierBody.Settings.AgentRadius+collectorBody.Settings.AgentRadius+
                                Math.Max(earlierBody.Settings.CollisionSkin+earlierBody.Settings.Epsilon,collectorBody.Settings.CollisionSkin+collectorBody.Settings.Epsilon);
                            var crossing=sameEarlier && current!=null && current.Length>1 && (bool)pathsConflict.Invoke(null,new object[]{actual,current,clearance});
                            if(crossing && !ownerCross)
                                records.Add("CORE_UNISSUED_ACTUAL_CONFLICT exit="+key+" earlier="+earlierKey+" leg="+Read(earlierLeg,"Index")+
                                    " candidate=["+string.Join(";",actual.Select(p=>p.ToString("F6")))+"] current=["+string.Join(";",current.Select(p=>p.ToString("F6")))+"]");
                            ownerCross|=crossing;
                            if(crossing && decision.Kind==TrafficDecisionKind.WaitingForTraffic && decision.Blockers.Any(b=>b.Owner.Equals(earlierKey) &&
                                b.Proof=="current-conflict-segment" && b.ResourceId==((TrafficRequest)Read(earlierEntry,"Request")).ResourceId))
                            {
                                if(requireAcquiredPriority)
                                {
                                    // A separate chronological/current-MOVE assertion, not an alias of the golden branch.
                                    // 原Entry/Source/age/query/debt不重建；必须实际观察同一Bound Native而非只看finite提示。
                                    Assert.That(prioritySourceSequence,Is.GreaterThan(0));Assert.That(priorityAcquired,Is.GreaterThan(0));
                                    Assert.That(earlierSource.RequestSequence,Is.EqualTo(prioritySourceSequence));Assert.That(earlierSource.AcquiredSequence,Is.EqualTo(priorityAcquired));
                                    Assert.That(Read(earlierEntry,"RequestSequence"),Is.EqualTo(prioritySourceSequence));Assert.That(Read(earlierEntry,"AcquiredSequence"),Is.EqualTo(priorityAcquired));
                                    Assert.That(core.TryGetCommittedJourney(earlierKey,out var stillOwned),Is.True);Assert.That(stillOwned,Is.SameAs(earlierSource));
                                    Assert.That(Read(earlierEntry,"FirstFrame"),Is.EqualTo(earlierFirst));Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(exitFirst));
                                    Assert.That(decision.RequestSequence,Is.GreaterThan(prioritySourceSequence),"The later actual-exit phase cannot inherit its older Visit's priority");
                                    if(priorityExitSequence==0)priorityExitSequence=decision.RequestSequence;Assert.That(decision.RequestSequence,Is.EqualTo(priorityExitSequence));
                                    Assert.That(decision.AcquiredSequence,Is.Zero);Assert.That(Read(entry,"Leg"),Is.Null);Assert.That((long)Read(visits[collector],"RequestId"),Is.Zero);
                                    var ownerQueries=core.QueriesConsumed(earlierKey);var exitQueries=core.QueriesConsumed(key);var limit=(int)Read(core,"queriesPerIntent");
                                    Assert.That(ownerQueries,Is.InRange(priorityOwnerQueries,limit));Assert.That(exitQueries,Is.InRange(priorityExitQueries,limit));priorityOwnerQueries=ownerQueries;priorityExitQueries=exitQueries;
                                    var debt=(float)Read(entry,"WaitRemaining");Assert.That(debt,Is.GreaterThan(0).And.LessThanOrEqualTo(priorityDebt));priorityDebt=debt;
                                    var stamp=(TrafficNativeStamp)Read(earlierLeg,"Stamp");
                                    if(Flag(earlierLeg,"Committed")&&Flag(earlierLeg,"Bound")&&stamp.RequestId>0&&native.Contains(stamp.RequestId)&&stamp.RequestId==(long)Read(visits[incoming],"RequestId"))
                                    {
                                        var rows=(IDictionary)Read(world.Service,"byActor");Assert.That(rows.Contains(incoming),Is.True);Assert.That(rows[incoming],Is.SameAs(native[stamp.RequestId]));
                                        Assert.That(Read(native[stamp.RequestId],"ActorId"),Is.EqualTo(incoming));Assert.That(Read(earlierLeg,"ActorLife"),Is.SameAs(earlierBody));
                                        if(priorityObserved)Assert.That(stamp,Is.EqualTo(priorityStamp));else records.Add("MAIN_CURRENT_LEG_PRIORITY originalSource="+earlierKey+" sequence="+prioritySourceSequence+" acquired="+priorityAcquired+
                                            " actualRequest="+stamp.RequestId+" generation="+stamp.LegGeneration+" laterSequence="+decision.RequestSequence+" laterAcquired=0 sameOwnedSource=True noRetireRevive=True");
                                        if(requireStrictQualification)
                                        {
                                            var snapshot=(TrafficWorldSnapshot)Read(core,"snapshot");var ownerRequest=(TrafficRequest)Read(earlierEntry,"Request");
                                            var qualifiedPath=(Vector3[])Read(earlierEntry,"CurrentPath");var ownBodies=snapshot.Bodies.Where(body=>ReferenceEquals(body.ActorLife,earlierBody)).ToArray();
                                            Assert.That(ownBodies,Has.Length.EqualTo(1));Assert.That(ownBodies[0].Owner,Is.EqualTo(earlierKey));
                                            Assert.That(ownBodies[0].Enabled&&ownBodies[0].ActorActive,Is.True);Assert.That(ownerRequest.IndependentlyProgressing,Is.True);
                                            Assert.That(ownerRequest.FiniteSeconds,Is.GreaterThan(0));Assert.That(qualifiedPath.Length,Is.GreaterThan(1));
                                            Assert.That(qualifiedPath[0],Is.EqualTo(ownBodies[0].Position));
                                            Assert.That(ownerRequest.Target,Is.EqualTo(earlierProof.Waypoints[(int)Read(earlierLeg,"Index")]));
                                            Assert.That(ReferenceEquals(Read(earlierLeg,"Proof"),earlierProof)&&earlierSource.ProofRoutes.Any(proof=>ReferenceEquals(proof,earlierProof)),Is.True);
                                            foreach(var proof in earlierSource.ProofRoutes)
                                            {
                                                Assert.That(proof.ExpandedPath.Count,Is.GreaterThan(0));Assert.That(proof.Waypoints.Count,Is.GreaterThan(0));
                                                Assert.That(proof.LegEnds.Count,Is.EqualTo(proof.Waypoints.Count));Assert.That(proof.LegEnds.Last(),Is.EqualTo(proof.ExpandedPath.Count-1));
                                            }
                                            foreach(var body in snapshot.Bodies.Where(body=>body.Owner.ActorId!=incoming))
                                                Assert.That(CustomerRoutePlanner.IsClear(qualifiedPath,new[]{body.Position},ownerRequest.Radius+body.Radius+Math.Max(ownerRequest.SafetyMargin,body.SafetyMargin)),Is.True,
                                                    "PREMISE: actual current Native remainder is independently body-clear");
                                            foreach(var fact in snapshot.Parking.Where(fact=>fact.Owner.ActorId!=incoming))
                                                Assert.That(CustomerRoutePlanner.IsClear(qualifiedPath,new[]{fact.Position},ownerRequest.Radius+fact.Radius+Math.Max(ownerRequest.SafetyMargin,fact.SafetyMargin)),Is.True,
                                                    "PREMISE: actual current Native remainder is independently canonical-clear");
                                            Assert.That(Read(native[stamp.RequestId],"RetryCount"),Is.EqualTo(0));Assert.That(Read(native[stamp.RequestId],"Recovering"),Is.EqualTo(false));
                                            Assert.That(Read(native[stamp.RequestId],"OriginalFailure"),Is.EqualTo(NavigationFailure.None));
                                            Assert.That(((NavigationTarget)Read(native[stamp.RequestId],"Original")).Position,Is.EqualTo(ownerRequest.Target));
                                            var own=snapshot.Parking.Single(fact=>fact.Owner.ActorId==incoming && fact.ResourceId==ownerRequest.ResourceId);
                                            Assert.That(own.CanonicalOwner,Is.SameAs(Read(visits[incoming],"PickupOwner")));
                                            Assert.That(own.BusinessOwner,Is.SameAs(visits[incoming]));Assert.That(own.ReleaseObligation,Is.SameAs(Read(visits[incoming],"Admission")));
                                            Assert.That(own.SemanticCheckpoint is PickupQueueService.ReservationClaim reservation && reservation.IsCurrent,Is.True);
                                            Assert.That((IEnumerable<TrafficIntentKey>)Read(entry,"FrozenOwners"),Is.EquivalentTo(new[]{earlierKey}),"PREMISE: this qualifier has exactly one genuine finite dependency");
                                            var frozenResources=(IDictionary)Read(entry,"FrozenResources");Assert.That(frozenResources.Contains(earlierKey),Is.True);
                                            var originalResources=((IEnumerable)frozenResources[earlierKey]).Cast<string>().ToArray();
                                            Assert.That(originalResources,Has.Member(ownerRequest.ResourceId));Assert.That(originalResources.All(resource=>resource=="body"||resource==ownerRequest.ResourceId),Is.True);
                                            Assert.That(((IEnumerable)Read(entry,"WaitingBlockers")).Cast<TrafficBlocker>().Any(block=>block.Owner.Equals(earlierKey)&&
                                                block.ResourceId==ownerRequest.ResourceId&&block.Proof=="current-conflict-segment"),Is.True);
                                            strictQualificationObserved=true;
                                            records.Add("CORE_REAL_STRICT_QUALIFICATION sameOriginalSource=True fullProof=True actualBoundNative="+stamp.RequestId+"/"+stamp.LegGeneration+
                                                " currentBodyAndCanonicalClear=True realPqsClaim=True originalFrozenOnly=True remaining="+debt);
                                        }
                                        priorityStamp=stamp;priorityObserved=true;
                                        if(requireServiceLeaseRevalidation)CaptureLeaseWait(decision);
                                        if(requireDetourCandidate)ObserveDetourWait(entry,decision);
                                    }
                                }
                                if(invalidation==5 || invalidation==6)
                                {
                                    var stamp=(TrafficNativeStamp)Read(earlierLeg,"Stamp");
                                    if(Flag(earlierLeg,"Committed")&&Flag(earlierLeg,"Bound")&&!Flag(earlierLeg,"Completed")&&stamp.RequestId>0&&native.Contains(stamp.RequestId)&&
                                        stamp.RequestId==(long)Read(visits[incoming],"RequestId"))contributorReady=true;
                                }
                                waiting=true;
                                if((long)Read(visits[collector],"RequestId")!=0 || decision.AcquiredSequence!=0 || (int)Read(earlierEntry,"FirstFrame")!=earlierFirst)
                                    throw new InvalidOperationException("Original conflict wait issued/re-aged a source");
                                if(invalidation==3 || invalidation==4)
                                {
                                    disabledPose=earlierBody.transform.position;disabledFrame=Time.frameCount;
                                    postWaitBody=collectorBody.transform.position;
                                    postWaitCapacities=customers.Capacity.GetCapacities().Select(c=>c.Used).ToArray();
                                    Assert.That(receipts,Is.Empty);
                                    if(invalidation==3)earlierBody.gameObject.SetActive(false); // Exact old post-WAIT lifecycle input.
                                    else actualObstruction=ObstructOriginalCurrentLeg();
                                    disabledApplied=true;return;
                                }
                            }
                        }
                        if(requireServiceLeaseRevalidation&&leaseWaitCaptured&&!leaseReleased)
                        {
                            CheckLeaseLedger();
                            if(!Flag(heldCollector,"PickupDeparted"))CheckHeldServiceLease();
                            var realOwnerWake=leaseCheckpoint||ObserveOriginalLeaseOwnerProgress();
                            if(!leaseRevalidated&&realOwnerWake&&!ReferenceEquals(decision,leaseWaitDecision)&&
                                (decision.Kind==TrafficDecisionKind.WaitingForTraffic||decision.Kind==TrafficDecisionKind.Granted))
                            {
                                Assert.That(decision.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None));
                                if(CheckHeldServiceLease())
                                {
                                    Assert.That(actual,Is.Not.Null);Assert.That(actual.Length,Is.GreaterThan(1));
                                    if(decision.Kind==TrafficDecisionKind.Granted)
                                    {
                                        var request=(TrafficRequest)Read(entry,"Request");var snapshot=(TrafficWorldSnapshot)Read(core,"snapshot");
                                        foreach(var body in snapshot.Bodies.Where(body=>body.Owner.ActorId!=collector))
                                            Assert.That(CustomerRoutePlanner.IsClear(actual,new[]{body.Position},request.Radius+body.Radius+Math.Max(request.SafetyMargin,body.SafetyMargin)),Is.True);
                                        foreach(var claim in snapshot.Parking.Where(claim=>claim.Owner.ActorId!=collector))
                                            Assert.That(CustomerRoutePlanner.IsClear(actual,new[]{claim.Position},request.Radius+claim.Radius+Math.Max(request.SafetyMargin,claim.SafetyMargin)),Is.True);
                                    }
                                    leaseRevalidated=true;
                                }
                            }
                        }
                        var exitLeg=Read(entry,"Leg");
                        if(exitLeg!=null && Flag(exitLeg,"Committed") && Flag(exitLeg,"Bound") && !Flag(exitLeg,"Completed"))
                        {
                            var stamp=(TrafficNativeStamp)Read(exitLeg,"Stamp");
                            if(requireServiceLeaseRevalidation&&leaseWaitCaptured&&!leaseBoundSampled&&(leaseCheckpoint||leaseNativeAdvanced)&&leaseRevalidated)
                            {
                                Assert.That((leaseCheckpoint||leaseNativeAdvanced)&&leaseRevalidated,Is.True,"PREMISE: actual original Native progress or completion must wake the normal Exit revalidation");
                                CheckHeldServiceLease();CheckLeaseLedger();leaseBoundSampled=true;
                                records.Add("REAL_SERVICE_LEASE_REVALIDATED sameExitEntry=True sameFrozen=True originalNativeAdvanced="+leaseNativeAdvanced+" originalNativeArrivedCompleted="+leaseCheckpoint+" actualExitNative="+stamp.RequestId+"/"+stamp.LegGeneration);
                            }
                            if(requireDetourCandidate&&!detourBoundSampled)
                            {
                                Assert.That(detourWaitSamples,Is.GreaterThanOrEqualTo(2),"PREMISE: genuine same-current-leg WAIT must retain the detour over distinct normal frames");
                                CheckDetourLedger();var source=Read(entry,"JourneyPermit") as TrafficDecision;Assert.That(source,Is.Not.Null);
                                var proof=source.ProofRoutes.Single(p=>p.ResourceId==((TrafficRequest)Read(entry,"Request")).ResourceId);
                                CollectionAssert.AreEqual(detourPath,proof.ExpandedPath);CollectionAssert.AreEqual(detourWaypoints,proof.Waypoints);
                                CollectionAssert.AreEqual(detourLegEnds,proof.LegEnds);detourBoundSampled=true;
                                records.Add("REAL_PLANNED_DETOUR retainedFrames="+detourWaitSamples+" originalEntry=True originalCompleteGeometry=True originalFrozen=True actualNative="+stamp.RequestId+"/"+stamp.LegGeneration);
                            }
                            exitBound|=waiting && native.Contains(stamp.RequestId) && stamp.RequestId==(long)Read(visits[collector],"RequestId") &&
                                ReferenceEquals(Read(exitLeg,"ActorLife"),Read(visits[collector],"Actor")) && (int)Read(entry,"FirstFrame")==exitFirst;
                        }
                    }
                }
                catch(Exception error){if(errors.Count<3)errors.Add(error.ToString());}
            }
            try
            {
                // Same formal Photo Decor recipe; default Main entry/exit configuration stays in use.
                // 使用已验证的两排中间PU布局；只本base切换fixture，不改Photo测试或Scene资产。
                yield return MatrixStart();
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
                decor.ExitDecorationMode();
                yield return Until(()=>owner.CanProgress&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                yield return null; // Let the normal owner publish the final layout/Core binding.
                // Bind after fixture layout rebuilds its epoch/Core, before any customer business.
                // 布局准备结束后绑定真实runtime；不持有重建前的旧Core。
                core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic");
                Assert.That(core,Is.Not.Null);
                Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic"),Is.SameAs(core));
                entries=CafeLoopRuntimeProof.Field<IDictionary>(core,"entries");
                visits=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");
                native=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byId");
                service=CafeLoopRuntimeProof.Field<CafeLoopService>(owner,"service");
                yield return AwaitTrafficAdmission();collector=customers.Snapshot.Single().VisitId;
                yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==collector&&o.State==OrderState.ReadyForPickup)&&
                    employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                yield return PreparePositiveLongService(collector);
                Assert.That(customers.TryMoveToPickupService(collector,out var reason),Is.True,reason);
                yield return Until(()=>Flag(visits[collector],"ServiceArrived"));Assert.That(owner.HoldCollection,Is.True);
                if(requireDetourCandidate)
                {
                    var collectorBody=(NavigationActor)Read(visits[collector],"Actor");
                    detourPosition=CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position+Vector3.forward*1.3f;
                    detourPosition.y=collectorBody.transform.position.y;
                    Assert.That(adapter.IsPointWalkable(detourPosition),Is.True,"PREMISE: the single fixed detour obstruction must be walkable");
                    Assert.That(world.RegisteredActors.Where(body=>body!=null).All(body=>Vector3.Distance(body.transform.position,detourPosition)>=Pair(collectorBody,body)),Is.True,
                        "PREMISE: the fixed obstruction cannot overlap any actual actor; no replacement point is searched");
                    detourBody=Blocker(detourPosition,"actual-exit-fixed-detour-body");
                    Assert.That(world.RegisteredActors.Contains(detourBody),Is.True);
                    Assert.That(world.RegisteredActors.Where(body=>body!=null&&body!=detourBody).All(body=>Vector3.Distance(body.transform.position,detourPosition)>=Pair(detourBody,body)),Is.True);
                }
                yield return AwaitTrafficAdmission();incoming=customers.Snapshot.Single(v=>v.VisitId!=collector).VisitId;
                foreach(var id in new[]{collector,incoming})
                {
                    tokens[id]=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id&&r.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers).Token;
                    if(requireAcquiredPriority){priorityTokens[id]=customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray();Assert.That(priorityTokens[id],Has.Length.EqualTo(3));}
                    var observer=world.RegisteredActors.Single(a=>a.ActorId==id).gameObject.AddComponent<CafeActualExitObserver>();
                    observer.VisitId=id;observer.Receipt=(visit,pose)=>
                    {
                        if(!tracking)return;
                        Assert.That(Vector3.Distance(pose,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position),Is.LessThanOrEqualTo(.081f));
                        Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,tokens[visit])).State,
                            Is.EqualTo(AnimalCafe.Capacity.ReservationState.Released));
                        Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==visit).State,Is.EqualTo(OrderState.Completed));
                        Assert.That(receipts.Add(visit),Is.True);
                    };observers.Add(observer);
                }
                observing=true;owner.SnapshotChanged+=Observe;
                if(requireServiceLeaseRevalidation)
                {
                    leaseHook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);Assert.That(leaseHook,Is.Not.Null);
                    leasePrior=leaseHook.GetValue(customers) as Delegate;
                    leaseInstalled=Delegate.Combine(leasePrior,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)LeaseReceipt);
                    leaseHook.SetValue(customers,leaseInstalled);
                }
                if(invalidation==5 || invalidation==6)
                {
                    for(var frame=0;frame<6000 && !contributorEvaluated && errors.Count==0;frame++)
                    {
                        TryApplyContributorInvalidation();if(contributorApplied)ObserveInvalidatedContributor();
                        if(!contributorEvaluated)yield return null;
                    }
                    Assert.That(errors,Is.Empty,string.Join("\n",errors));
                    Assert.That(capture&&attempted&&collected&&candidateSeen&&ownerCross&&waiting&&contributorReady&&contributorApplied&&contributorEvaluated,Is.True,
                        "PremiseNotProven: original current-leg Bound Native WAIT, lifecycle input and actual re-evaluation must occur within the same 6000 window");
                    Assert.That(invalidation==5?contributorCancelled:!earlierBody.enabled,Is.True);Assert.That(receipts,Is.Empty);
                    yield break; // Original negative scope: no reenable, Resume or invented two-customer drain.
                }
                if(invalidation==3 || invalidation==4)
                {
                    for(var frame=0;frame<6000 && !disabledEvaluated && errors.Count==0;frame++)yield return null;
                    Assert.That(errors,Is.Empty,string.Join("\n",errors));
                    Assert.That(capture&&attempted&&collected&&candidateSeen&&ownerCross&&waiting&&disabledApplied&&disabledEvaluated,Is.True,
                        "PremiseNotProven: real original-source WAIT and subsequent invalidation were not both evaluated within 6000 frames");
                    Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==collector).State,Is.EqualTo(OrderState.Completed));
                    yield break; // Original post-WAIT negative cases require no invented recovery/drain.
                }
                if(disableBeforeIssue || invalidation==2)
                {
                    for(var frame=0;frame<6000 && !disabledBoundSampled && errors.Count==0;frame++)yield return null;
                    Assert.That(errors,Is.Empty,string.Join("\n",errors));
                    Assert.That(capture&&attempted&&collected&&disabledApplied&&disabledEvaluated,Is.True,
                        "PremiseNotProven: real once-collection/disable source never received an actual decision");
                    Assert.That(candidateSeen,Is.True,"Original disabled-case exit must have a complete actual primary; policy refusal stays visible");
                    Assert.That(ownerCross,Is.True,"Actual original primary must cross the captured earlier leg geometry");
                    Assert.That(disabledBoundSampled&&exitBound,Is.True,
                        "Within the original bound, a real safe exit must issue while the disabled owner's token stays retained");
                    yield break; // Original negative contract ends here; no reenable or new recovery requirement.
                }
                else
                {
                var originalWaitWindow=Time.frameCount;
                if(requireAdmissionCurrentRoute)
                {
                    while(Time.frameCount-originalWaitWindow<6000&&!OriginalIncomingBoundForAdmission()&&errors.Count==0)yield return null;
                    Assert.That(errors,Is.Empty,string.Join("\n",errors));
                    Assert.That(OriginalIncomingBoundForAdmission(),Is.True,"ADMISSION_CURRENT_ROUTE_NOT_PROVEN: no original paid Bound window before collection");
                    yield return ObserveUnbornCurrentRouteRefusal(originalWaitWindow);
                    // Restore a genuine positive normal owner frame before the one original collection (zero-delta preparation closes its operation gate).
                    yield return null;Assert.That(Time.deltaTime,Is.GreaterThan(0));
                    CollectOriginalOnce();Assert.That(collected,Is.True);
                }
                for(var frame=0;frame<6000 && Time.frameCount-originalWaitWindow<6000 && !waiting && errors.Count==0;frame++)yield return null;
                Assert.That(errors,Is.Empty,string.Join("\n",errors));
                Assert.That(capture&&attempted&&collected,Is.True,"PremiseNotProven: no real paid/transferred unissued strict-leg collection window");
                Assert.That(candidateSeen,Is.True,"PremiseNotProven: no original complete actual-exit primary plan observed");
                Assert.That(ownerCross,Is.True,"PremiseNotProven: actual exit never crossed that same real current leg");
                Assert.That(waiting,Is.True,"Actual original conflict must wait before exit native dispatch");
                Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==collector).State,Is.EqualTo(OrderState.Completed));
                Assert.That(receipts,Is.Empty,"Waiting cannot fabricate a departure");
                tools.ResumeCollection();
                yield return Until(()=>receipts.Count==2&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                Assert.That(errors,Is.Empty,string.Join("\n",errors));
                Assert.That(exitBound,Is.True,"Same original actual-exit Entry must later issue a real Bound Native leg");
                if(requireDetourCandidate)
                {
                    Assert.That(detourBoundSampled&&detourWaitSamples>=2,Is.True,"PREMISE: real retained detour and actual Bound were not both witnessed");
                    Assert.That(detourBody.isActiveAndEnabled&&world.RegisteredActors.Contains(detourBody),Is.True);
                    Assert.That(detourBody.transform.position,Is.EqualTo(detourPosition));
                }
                if(requireServiceLeaseRevalidation)
                {
                    Assert.That(leaseWaitCaptured&&(leaseNativeAdvanced||leaseNativeArrived&&leaseCheckpoint)&&leaseRevalidated&&leaseBoundSampled&&leaseDepartureSeen&&leaseReleased,Is.True,
                        "PREMISE: same lease must survive actual finite WAIT/checkpoint/revalidation/Bound and retire only on actual departure");
                    Assert.That(heldLeaseClaim.IsCurrentCollectionLease,Is.False);
                }
                Assert.That(owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed),Is.True);
                Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                if(requireAcquiredPriority)
                {
                    Assert.That(priorityObserved,Is.True,"PremiseNotProven: exact original Bound Native current-leg priority was not observed; do not substitute a future-only route");
                    if(requireStrictQualification)Assert.That(strictQualificationObserved,Is.True,"PremiseNotProven: full real current Source/proof/Native/claim qualification was not observed");
                    Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(2));Assert.That(owner.Snapshot.PaidVisitIds.Count,Is.EqualTo(2));
                    foreach(var pair in priorityTokens){var original=customers.Capacity.GetReservations().Where(r=>r.OwnerId==pair.Key).ToArray();CollectionAssert.AreEquivalent(pair.Value,original.Select(r=>r.Token));Assert.That(original.All(r=>r.State==AnimalCafe.Capacity.ReservationState.Released),Is.True);}
                }
                }
            }
            finally
            {
                if(disableBeforeIssue)records.Add("CORE_DISABLED_FINAL "+DisabledFacts());
                observing=false;owner.SnapshotChanged-=Observe;tracking=false;
                if(leaseInstalled!=null&&leaseHook!=null)
                {
                    var current=leaseHook.GetValue(customers);
                    if(ReferenceEquals(current,leaseInstalled))leaseHook.SetValue(customers,leasePrior);
                    else if(!ReferenceEquals(current,leasePrior))errors.Add("UNKNOWN: service lease CorridorObserver replacement preserved");
                }
                if(detourBody!=null){world.Unregister(detourBody);UnityEngine.Object.DestroyImmediate(detourBody.gameObject);}
                if(actualObstruction!=null){world.Unregister(actualObstruction);UnityEngine.Object.DestroyImmediate(actualObstruction.gameObject);}
                foreach(var observer in observers)if(observer!=null)observer.Receipt=null;
                TestContext.WriteLine("CORE_UNISSUED_SUMMARY capture="+capture+" candidateSeen="+candidateSeen+" ownerCross="+ownerCross+
                    " actualWait="+waiting+" originalExitBound="+exitBound+" actualExits="+receipts.Count+
                    " predicateTransitions="+predicateTransitions+" predicateStored="+Math.Min(predicateTransitions,32)+" predicateOverflow="+predicateOverflow+
                    " timingHintRecords="+hintRecords+" timingHintOverflow="+hintOverflow+
                    " disableCase="+disableBeforeIssue+" disabledEvaluated="+disabledEvaluated+" firstBoundSampled="+disabledBoundSampled);
                foreach(var record in records)TestContext.WriteLine(record);
                if(disableBeforeIssue)foreach(var error in errors)TestContext.WriteLine("CORE_DISABLED_OBSERVER_ERROR "+error);
            }
        }

        // Explicit empty compatibility input; no source, claim, Visit or published slot is rewritten.
        // 首次proposal前一次初始化真实空队列；不修改已发布的owner/slot或业务身份。
        private PickupQueueService InitializeCanonicalUntransferredInput()
        {
            Assert.That(TestContext.CurrentContext.Test.Name,Is.EqualTo(nameof(CanonicalUntransferredTargetCannotAuthorizePickupExitWait)));
            Assert.That(customers.Snapshot,Is.Empty);Assert.That(owner.Snapshot.Orders,Is.Empty);Assert.That(owner.Snapshot.PaidVisitIds,Is.Empty);
            Assert.That(customers.Capacity.GetReservations(),Is.Empty);Assert.That(customers.CounterAssignments,Is.Empty);
            Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.Zero);
            Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(customers,"trafficCandidates").Count,Is.Zero);
            Assert.That(CafeLoopRuntimeProof.Field<object>(customers,"pendingAdmissionAnchor"),Is.Null);
            Assert.That(CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue"),Is.Null);
            var facts=MovementAdmissionFacts();var nextRequest=CafeLoopRuntimeProof.Field<long>(world.Service,"nextId");
            var runtime=Find<CafeLayoutRuntime>();var pickupId=runtime.FunctionalSurfaceLayout.PickUpPoints.Single().InstanceId;
            Assert.That(customers.TryConfigurePickupFromLayout(pickupId,out var reason),Is.True,reason);
            var sides=CafeLoopRuntimeProof.Field<NavigationTarget[]>(customers,"pickupSides");
            var side=sides.Single(s=>s.Position==CafeLoopRuntimeProof.Field<Vector3>(customers,"pickupCustomer"));
            var exit=CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position;
            var outward=exit-side.Position;outward.y=0;Assert.That(outward.magnitude,Is.GreaterThan(3.3f));outward.Normalize();
            var head=side.Position+outward*1.1f;var prefabs=CafeLoopRuntimeProof.Field<NavigationActor[]>(customers,"prefabs");
            var clearance=prefabs.Max(a=>2*a.Settings.AgentRadius+a.Settings.CollisionSkin+a.Settings.Epsilon+a.Settings.ArrivalDistance);
            var arrival=prefabs.Min(a=>a.Settings.ArrivalDistance+a.Settings.Epsilon);
            Assert.That(Vector3.Distance(side.Position,CafeLoopRuntimeProof.Field<Vector3>(customers,"pickupEmployee")),Is.GreaterThanOrEqualTo(clearance),
                "PremiseNotProven: this Main separate-side fixture must retain its original nonshared service semantics");
            var grid=CafeLoopRuntimeProof.Field<Transform>(customers,"gridRoot");var size=runtime.Layout.GridSettings.CellSize;
            bool InEntrance(Vector3 p)
            {
                var q=grid.InverseTransformPoint(p);
                return runtime.Layout.Reservations.Any(r=>r.Type==LayoutReservationType.EntranceClearance &&
                    q.x>=r.Origin.X*size-.46f&&q.x<=(r.Origin.X+r.Size.Width)*size+.46f&&
                    q.z>=r.Origin.Y*size-.46f&&q.z<=(r.Origin.Y+r.Size.Height)*size+.46f);
            }
            Assert.That(customers.Capacity.Limits.PickUp,Is.GreaterThanOrEqualTo(2));
            // Two declared geometry members for two real reservations; CapacityService limits stay unchanged.
            // 本控制输入只声明两格几何，不修改系统容量规则或token限额。
            var slots=new CounterQueuePlanner(1.1f,preserveStraightPrefix:true).Build(head,outward,2,
                (point,previous)=>adapter.IsPointArrivable(point,arrival)&&!InEntrance(point)&&Vector3.Distance(point,exit)>=1.2f&&
                    sides.All(s=>Vector3.Distance(point,s.Position)>=clearance)&&
                    world.RegisteredActors.All(a=>a==null||Vector3.Distance(a.transform.position,point)>=clearance)&&
                    (previous==null||adapter.HasClearPath(previous.Value,point))&&adapter.HasClearPath(point,side.Position),
                sampleEdge:(from,to)=>{var path=adapter.GetCompletePath(from,to);return path==null||path.Count==0?(Vector3?)null:path[path.Count-1];}).ToArray();
            Assert.That(slots.Length,Is.GreaterThanOrEqualTo(2),"PremiseNotProven: the one fixed formal-side corridor cannot hold the two actual reservations");
            Assert.That(slots[0],Is.EqualTo(head));Assert.That(Vector3.Distance(slots[1],head+outward*1.1f),Is.LessThan(.001f),
                "PremiseNotProven: the original fixed second slot must stay on the side-to-exit chord; no alternative head/slot search");
            var pickup=new PickupQueueService(slots,sides.Select(s=>s.Position).ToArray());
            Assert.That(pickup.Snapshot,Is.Empty);Assert.That(pickup.ServiceLeases,Is.Empty);Assert.That(pickup.Version,Is.Zero);
            CafeLoopRuntimeProof.Set(customers,"pickupWaitSlots",slots);CafeLoopRuntimeProof.Set(customers,"pickupOutward",outward);
            CafeLoopRuntimeProof.Set(customers,"pickupQueue",pickup);
            Assert.That(CafeLoopRuntimeProof.Field<object>(customers,"pickupBlueprint"),Is.Null);
            Assert.That(MovementAdmissionFacts(),Is.EqualTo(facts));Assert.That(CafeLoopRuntimeProof.Field<long>(world.Service,"nextId"),Is.EqualTo(nextRequest));
            TestContext.WriteLine("CANONICAL_UNTRANSFERRED_EMPTY_INPUT head="+head.ToString("F6")+" next="+slots[1].ToString("F6")+
                " formalSide="+side.Position.ToString("F6")+" slots="+slots.Length+" beforeFirstSource=True shared=False blueprint=NULL");return pickup;
        }

        private IEnumerator ObserveRealCanonicalUntransferredTarget()
        {
            const string premise="PremiseNotProven: ";var startFrame=Time.frameCount;
            var originalStepping=CafeLoopRuntimeProof.Field<bool>(owner,"stepping");NavigationActor blocker=null;
            CafeActualExitObserver exitObserver=null;var observing=true;var exits=0;var exitPose=Vector3.zero;
            var corridorHook=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(corridorHook,Is.Not.Null);var priorCorridor=corridorHook.GetValue(customers);object installedCorridor=null;
            void Deadline()=>Assert.That(Time.frameCount-startFrame,Is.LessThan(6000),premise+"one original 6000-frame window covers all preparation and actual traffic");
            IEnumerator Bounded(IEnumerator routine)
            {
                try
                {
                    while(routine.MoveNext())
                    {
                        Deadline();
                        if(routine.Current is IEnumerator child)yield return Bounded(child);
                        else {Assert.That(routine.Current,Is.Null,"Only original normal-frame waits are supported");yield return null;}
                        Deadline();
                    }
                }
                finally{(routine as IDisposable)?.Dispose();}
            }
            try
            {
                yield return Bounded(AddCounters(new GridPosition(4,4)));Find<DecorationModeController>().ExitDecorationMode();
                yield return Bounded(Until(()=>owner.CanProgress&&employees.Snapshot.All(e=>e.State==EmployeeState.Idle)));
                var pickup=InitializeCanonicalUntransferredInput();
                yield return Bounded(AwaitTrafficAdmission());var collector=customers.Snapshot.Single().VisitId;
                yield return Bounded(Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==collector&&o.State==OrderState.ReadyForPickup)&&
                    employees.Snapshot.All(e=>e.State==EmployeeState.Idle)));
                yield return Bounded(PreparePositiveLongService(collector));
                Assert.That(customers.TryMoveToPickupService(collector,out var reason),Is.True,reason);
                var visits=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");
                yield return Bounded(Until(()=>(bool)MovementRead(visits[collector],"ServiceArrived")));
                Assert.That(owner.HoldCollection,Is.True);Assert.That(pickup.TryGetServiceLease(collector,out var lease),Is.True);
                yield return Bounded(AwaitTrafficAdmission());var unpaid=customers.Snapshot.Single(v=>v.VisitId!=collector).VisitId;
                yield return Bounded(Until(()=>customers.CafeSnapshot.Single(v=>v.VisitId==unpaid).CounterArrived));
                CafeLoopRuntimeProof.Set(owner,"stepping",true);
                yield return Bounded(PreparePositiveUnpaidReservation(unpaid));
                Assert.That(customers.TryReservePickup(unpaid,out reason),Is.True,reason); // Sole canonical reservation commit.
                var visit=visits[unpaid];var admission=MovementRead(visit,"Admission");var unpaidBody=(NavigationActor)MovementRead(visit,"Actor");
                Assert.That(pickup.TryCaptureReservation(unpaid,out var claim)&&claim.IsCurrent,Is.True);
                Assert.That(claim.Assignment.SlotIndex,Is.EqualTo(1));var target=claim.Assignment.Position;
                var core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic");
                Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic"),Is.SameAs(core));
                var entries=CafeLoopRuntimeProof.Field<IDictionary>(core,"entries");var native=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byId");
                var parking=MovementRead(visit,"PickupParking");Assert.That(parking,Is.Not.Null);
                var parkingSource=(TrafficDecision)MovementRead(parking,"Source");
                Assert.That(parkingSource.AcquiredSequence,Is.GreaterThan(0));Assert.That(core.TryGetCommittedJourney(parkingSource.Intent,out var retainedSource),Is.True);
                Assert.That(retainedSource,Is.SameAs(parkingSource));
                var legacyPath=(IReadOnlyList<Vector3>)MovementRead(visit,"ReservedPickupPath");
                Assert.That(legacyPath,Is.Not.Null);Assert.That(legacyPath.Count,Is.GreaterThan(1));
                var collectorBody=(NavigationActor)MovementRead(visits[collector],"Actor");var exit=CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position;
                float Pair(NavigationActor a,NavigationActor b)=>a.Settings.AgentRadius+b.Settings.AgentRadius+
                    Math.Max(a.Settings.CollisionSkin+a.Settings.Epsilon,b.Settings.CollisionSkin+b.Settings.Epsilon);
                Assert.That(CustomerRoutePlanner.IsClear(new[]{lease,exit},new[]{target},Pair(collectorBody,unpaidBody)),Is.False,
                    premise+"the actual unpaid canonical target must obstruct the old direct collection exit chord");
                var length=0f;for(var i=1;i<legacyPath.Count;i++)length+=Vector3.Distance(legacyPath[i-1],legacyPath[i]);
                Assert.That(length,Is.GreaterThan(0));var half=length*.5f;var midpoint=legacyPath[0];
                for(var i=1;i<legacyPath.Count;i++)
                {var part=Vector3.Distance(legacyPath[i-1],legacyPath[i]);if(half<=part){midpoint=Vector3.Lerp(legacyPath[i-1],legacyPath[i],half/part);break;}half-=part;}
                blocker=Blocker(midpoint,"canonical-untransferred-original-proof-obstruction");
                Assert.That(CustomerRoutePlanner.IsClear(legacyPath,new[]{blocker.transform.position},Pair(unpaidBody,blocker)),Is.False,
                    "The original cached route is now physically invalid, without private cache/owner/slot writes");
                Assert.That(customers.TryReservePickup(unpaid,out reason),Is.True,
                    "Identity is retained canonical ownership, not a new movement proof: "+reason);
                Assert.That(MovementRead(visit,"ReservedPickupPath"),Is.SameAs(legacyPath));
                var unpaidTokens=customers.Capacity.GetReservations().Where(r=>r.OwnerId==unpaid).ToArray();
                var collectorToken=customers.Capacity.GetReservations().Single(r=>r.OwnerId==collector&&r.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers).Token;
                var collectorVisit=visits[collector];var collectorAdmission=MovementRead(collectorVisit,"Admission");
                TrafficIntentKey exitKey=default;object exitEntry=null;var first=0;var sequence=0L;var queries=0;var actualProof=false;var actualNative=false;
                TrafficDecision originalExitSource=null;TrafficRouteProof originalExitProof=null;var finalNativeArrived=false;
                var finalNativePose=Vector3.zero;Exception finalArrivalError=null;
                void UnpaidCanonicalRemains()
                {
                    Deadline();Assert.That(visits[unpaid],Is.SameAs(visit));Assert.That(MovementRead(visit,"Admission"),Is.SameAs(admission));
                    Assert.That(owner.Snapshot.PaidVisitIds.Contains(unpaid),Is.False);Assert.That(owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==unpaid),Is.False);
                    Assert.That(MovementRead(visit,"PaidPickup"),Is.Null);Assert.That(MovementRead(visit,"PickupInTransit"),Is.EqualTo(false));
                    Assert.That(core.HasActiveGrant(parkingSource.Intent),Is.True);
                    Assert.That(core.CanIssueLeg(parkingSource.Intent,unpaidBody,(int)MovementRead(visit,"Generation")),Is.False,
                        "Canonical parking ownership alone is not a Native leg permission");
                    Assert.That((long)MovementRead(visit,"RequestId"),Is.EqualTo(0L));
                    Assert.That(native.Values.Cast<object>().Any(n=>(string)MovementRead(n,"ActorId")==unpaid),Is.False);
                    Assert.That(pickup.TryCaptureReservation(unpaid,out var current)&&current.IsCurrent&&claim.IsSameReservation(current),Is.True);
                    Assert.That(current.Assignment,Is.EqualTo(claim.Assignment));
                    var row=CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations")[unpaid];Assert.That(MovementRead(row,"Paid"),Is.EqualTo(false));
                    foreach(var token in unpaidTokens)
                    {var actual=customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,token.Token));Assert.That(actual.State,Is.EqualTo(token.State));Assert.That(actual.OwnerId,Is.EqualTo(unpaid));}
                    Assert.That(finalArrivalError,Is.Null,finalArrivalError?.ToString());
                    var removed=!visits.Contains(collector);
                    if(removed)
                    {
                        Assert.That(finalNativeArrived&&actualProof&&actualNative,Is.True,"Removal requires the same original final Native Arrived receipt, not just Released capacity");
                        Assert.That(Vector3.Distance(finalNativePose,exit),Is.LessThanOrEqualTo(.081f));
                        Assert.That(MovementRead(collectorVisit,"Actor"),Is.SameAs(collectorBody));Assert.That(MovementRead(collectorVisit,"Admission"),Is.SameAs(collectorAdmission));
                        Assert.That(MovementRead(collectorVisit,"State"),Is.EqualTo(CustomerVisitState.Removed));
                        Assert.That(world.RegisteredActors.Any(a=>ReferenceEquals(a,collectorBody)),Is.False);
                        Assert.That(native.Values.Cast<object>().Any(n=>(string)MovementRead(n,"ActorId")==collector),Is.False);
                        Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor").Contains(collector),Is.False);
                        Assert.That(entries[exitKey],Is.SameAs(exitEntry));Assert.That(MovementRead(exitEntry,"Retired"),Is.EqualTo(true));
                        Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==collector).State,Is.EqualTo(OrderState.Completed));
                    }
                    else
                    {
                        Assert.That(exits,Is.Zero);Assert.That(visits[collector],Is.SameAs(collectorVisit));
                        Assert.That(MovementRead(collectorVisit,"Actor"),Is.SameAs(collectorBody));
                    }
                    Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,collectorToken)).State,
                        Is.EqualTo(removed?AnimalCafe.Capacity.ReservationState.Released:AnimalCafe.Capacity.ReservationState.Occupied));
                }
                Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string> observeFinalArrival=(operation,id,other,position,path,obstacles,result)=>
                {
                    if(!observing||operation!="segment.callback-result"||id!=collector||!(" "+result+" ").Contains(" status=Arrived ")||Vector3.Distance(position,exit)>.081f)return;
                    try
                    {
                        var currentMove=MovementRead(collectorVisit,"TrafficMove");
                        var currentProof=currentMove==null?null:MovementRead(currentMove,"Proof") as TrafficRouteProof;
                        if(currentProof==null||(int)MovementRead(currentMove,"Index")!=currentProof.Waypoints.Count-1)return;
                        Assert.That(finalNativeArrived,Is.False);Assert.That(actualProof&&actualNative,Is.True);
                        Assert.That(exitEntry,Is.Not.Null);Assert.That(entries[exitKey],Is.SameAs(exitEntry));
                        Assert.That(visits[collector],Is.SameAs(collectorVisit));Assert.That(MovementRead(collectorVisit,"Actor"),Is.SameAs(collectorBody));
                        var move=MovementRead(collectorVisit,"TrafficMove");var leg=MovementRead(exitEntry,"Leg");Assert.That(move,Is.Not.Null);Assert.That(leg,Is.Not.Null);
                        Assert.That((TrafficIntentKey)MovementRead(move,"Intent"),Is.EqualTo(exitKey));
                        Assert.That(MovementRead(move,"Source"),Is.SameAs(originalExitSource));Assert.That(MovementRead(exitEntry,"JourneyPermit"),Is.SameAs(originalExitSource));
                        Assert.That(MovementRead(move,"Proof"),Is.SameAs(originalExitProof));Assert.That(MovementRead(leg,"Proof"),Is.SameAs(originalExitProof));
                        Assert.That(MovementRead(leg,"ActorLife"),Is.SameAs(collectorBody));Assert.That(MovementRead(leg,"Index"),Is.EqualTo(originalExitProof.Waypoints.Count-1));
                        Assert.That((bool)MovementRead(leg,"Committed")&&(bool)MovementRead(leg,"Bound")&&!(bool)MovementRead(leg,"Completed"),Is.True);
                        var stamp=(TrafficNativeStamp)MovementRead(leg,"Stamp");Assert.That(stamp.RequestId,Is.GreaterThan(0));
                        Assert.That((long)MovementRead(collectorVisit,"RequestId"),Is.EqualTo(stamp.RequestId));
                        Assert.That((int)MovementRead(move,"NativeGeneration"),Is.EqualTo(stamp.LegGeneration));
                        Assert.That((" "+result+" ").Contains(" request="+stamp.RequestId+" ")&&(" "+result+" ").Contains(" issuedGeneration="+stamp.LegGeneration+" "),Is.True);
                        Assert.That(((TrafficRequest)MovementRead(exitEntry,"Request")).Target,Is.EqualTo(exit));Assert.That(collectorBody.transform.position,Is.EqualTo(position));
                        finalNativePose=position;finalNativeArrived=true; // Receipt only; never Complete/Retire/Dispose from the observer.
                    }
                    catch(Exception error){finalArrivalError=finalArrivalError??error;}
                };
                installedCorridor=Delegate.Combine(priorCorridor as Delegate,observeFinalArrival);corridorHook.SetValue(customers,installedCorridor);
                exitObserver=collectorBody.gameObject.AddComponent<CafeActualExitObserver>();exitObserver.VisitId=collector;
                exitObserver.Receipt=(id,pose)=>{if(!observing)return;Assert.That(finalNativeArrived,Is.True);Assert.That(id,Is.EqualTo(collector));Assert.That(exits,Is.Zero);exits++;exitPose=pose;
                    Assert.That(Vector3.Distance(pose,exit),Is.LessThanOrEqualTo(.081f));
                    Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,collectorToken)).State,Is.EqualTo(AnimalCafe.Capacity.ReservationState.Released));};
                var service=CafeLoopRuntimeProof.Field<CafeLoopService>(owner,"service");var held=owner.HoldCollection;
                Assert.That(held,Is.True);var collectStepping=CafeLoopRuntimeProof.Field<bool>(owner,"stepping");Assert.That(collectStepping,Is.True);
                var collectPose=collectorBody.transform.position;
                // Zero-time reservation preparation closes the service commit window. Restore it through one real owner tick.
                // delta0返回帧不能直接Collect；原latch保持冻结，等真实正delta再由正常owner建立业务提交窗口。
                for(var transition=0;transition<3 && Time.deltaTime<=0;transition++)
                {
                    Deadline();UnpaidCanonicalRemains();yield return null;Deadline();UnpaidCanonicalRemains();
                    Assert.That(collectorBody.transform.position,Is.EqualTo(collectPose));Assert.That(owner.HoldCollection,Is.True);
                }
                Assert.That(Time.deltaTime,Is.GreaterThan(0),premise+"actual positive-delta collection tick was not reached within 3 frozen transitions");
                Assert.That(customers.CanCollectPickup(collector)&&(bool)MovementRead(visits[collector],"ServiceArrived"),Is.True);
                Assert.That(pickup.TryGetServiceLease(collector,out var beforeCollectLease)&&beforeCollectLease==lease,Is.True);
                CafeLoopRuntimeProof.Set(owner,"stepping",false);
                try{owner.Step(Time.deltaTime);}finally{CafeLoopRuntimeProof.Set(owner,"stepping",collectStepping);}
                Deadline();UnpaidCanonicalRemains();Assert.That(owner.HoldCollection,Is.True);
                Assert.That(collectorBody.transform.position,Is.EqualTo(collectPose));Assert.That(customers.CanCollectPickup(collector),Is.True);
                owner.HoldCollection=false;
                try{Assert.That(service.TryCollect(collector,out reason),Is.True,reason);}finally{owner.HoldCollection=held;}
                Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==collector).State,Is.EqualTo(OrderState.Completed));
                bool Observed()
                {
                    UnpaidCanonicalRemains();var snapshot=(TrafficWorldSnapshot)MovementRead(core,"snapshot");
                    var facts=snapshot.Parking.Where(p=>p.ResourceId=="pickup/"+unpaid&&ReferenceEquals(p.CanonicalOwner,pickup)).ToArray();
                    Assert.That(facts.Length,Is.EqualTo(1));Assert.That(facts[0].Position,Is.EqualTo(target));
                    Assert.That(ReferenceEquals(facts[0].BusinessOwner,visit)&&ReferenceEquals(facts[0].ReleaseObligation,admission),Is.True);
                    Assert.That(facts[0].SemanticCheckpoint is PickupQueueService.ReservationClaim factClaim&&factClaim.IsCurrent&&claim.IsSameReservation(factClaim),Is.True);
                    if(exits>0)return true;
                    if(!visits.Contains(collector))return false; // Same real arrival already proved above; still wait for the mandatory frame-end OnDestroy receipt.
                    var candidates=new List<DictionaryEntry>();foreach(DictionaryEntry row in entries)
                        if(((TrafficIntentKey)row.Key).ActorId==collector&&((TrafficIntentKey)row.Key).IntentId=="actual-exit"&&!(bool)MovementRead(row.Value,"Retired"))candidates.Add(row);
                    Assert.That(candidates.Count,Is.EqualTo(1),"No replacement or invented Exit source");
                    if(exitEntry==null){exitEntry=candidates[0].Value;exitKey=(TrafficIntentKey)candidates[0].Key;first=(int)MovementRead(exitEntry,"FirstFrame");}
                    Assert.That(candidates[0].Value,Is.SameAs(exitEntry));Assert.That((TrafficIntentKey)candidates[0].Key,Is.EqualTo(exitKey));
                    Assert.That((int)MovementRead(exitEntry,"FirstFrame"),Is.EqualTo(first));
                    var decision=(TrafficDecision)MovementRead(exitEntry,"Decision");
                    Assert.That(decision.FailureDisposition,Is.Not.EqualTo(TrafficFailureDisposition.Terminal),
                        "Original Exit must honestly prove the fixed geometry; actual refusal: "+decision.Kind+"/"+decision.Reason);
                    var currentSequence=decision.RequestSequence;
                    if(sequence==0&&currentSequence>0)sequence=currentSequence;if(sequence>0)Assert.That(currentSequence,Is.EqualTo(sequence));
                    var spent=(int)MovementRead(exitEntry,"Queries");Assert.That(spent,Is.GreaterThanOrEqualTo(queries).And.LessThanOrEqualTo(16384));queries=spent;
                    Assert.That(decision.Kind==TrafficDecisionKind.WaitingForTraffic&&decision.Blockers.Any(b=>b.ResourceId=="pickup/"+unpaid),Is.False,
                        "An unpaid canonical target cannot supply a finite release promise to this Exit");
                    var source=MovementRead(exitEntry,"JourneyPermit") as TrafficDecision;var request=MovementRead(exitEntry,"JourneyRequest") as TrafficRequest;
                    if(source==null||request==null)return false;
                    var proof=source.ProofRoutes.Single(p=>p.ResourceId==request.ResourceId);
                    if(originalExitSource==null){originalExitSource=source;originalExitProof=proof;}
                    Assert.That(source,Is.SameAs(originalExitSource));Assert.That(proof,Is.SameAs(originalExitProof));
                    var complete=new[]{request.Start}.Concat(proof.ExpandedPath).Concat(new[]{request.Target}).ToArray();
                    Assert.That(proof.ExpandedPath.Count,Is.GreaterThan(1));Assert.That(CustomerRoutePlanner.IsClear(complete,new[]{target},Pair(collectorBody,unpaidBody)),Is.True);
                    Assert.That(CustomerRoutePlanner.IsClear(complete,new[]{blocker.transform.position},Pair(collectorBody,blocker)),Is.True);
                    foreach(var body in snapshot.Bodies.Where(b=>b.Owner.ActorId!=collector))
                        Assert.That(CustomerRoutePlanner.IsClear(complete,new[]{body.Position},request.Radius+body.Radius+
                            Math.Max(request.SafetyMargin,body.SafetyMargin)),Is.True,"The actual full Exit proof must clear every retained nonself body");
                    foreach(var fact in snapshot.Parking.Where(p=>p.Owner.ActorId!=collector))
                        Assert.That(CustomerRoutePlanner.IsClear(complete,new[]{fact.Position},request.Radius+fact.Radius+
                            Math.Max(request.SafetyMargin,fact.SafetyMargin)),Is.True,"The actual full Exit proof must clear every retained nonself canonical claim");
                    actualProof=true;var leg=MovementRead(exitEntry,"Leg");
                    if(leg!=null&&(bool)MovementRead(leg,"Committed")&&(bool)MovementRead(leg,"Bound")&&!(bool)MovementRead(leg,"Completed"))
                    {
                        var stamp=(TrafficNativeStamp)MovementRead(leg,"Stamp");
                        Assert.That(source.AcquiredSequence,Is.GreaterThan(0));Assert.That(MovementRead(leg,"ActorLife"),Is.SameAs(collectorBody));
                        Assert.That(native.Contains(stamp.RequestId)&&(long)MovementRead(visits[collector],"RequestId")==stamp.RequestId,Is.True);
                        Assert.That(MovementRead(native[stamp.RequestId],"ActorId"),Is.EqualTo(collector));actualNative=true;
                    }
                    return false;
                }
                // Existing controlled traffic driver preserves service work/unpaid state, charges real traffic debt and uses actual World/Native motion.
                // 沿用原controlled driver：不推进收银付款，不伪造到站；真实移动、Core deadline仍正常推进。
                yield return Bounded(DriveCustomerTrafficWithoutService(Observed));
                Assert.That(actualProof&&actualNative,Is.True,premise+"a real original complete safe proof and Bound Native exit were not both observed");
                Assert.That(exits,Is.EqualTo(1));Assert.That(Vector3.Distance(exitPose,exit),Is.LessThanOrEqualTo(.081f));UnpaidCanonicalRemains();
                TestContext.WriteLine("CANONICAL_UNTRANSFERRED_REAL_EXIT proof=True Native=True actualExit=1 unpaidCanonicalRetained=True invalidOriginalPath=True sharedFrameWindow=6000 queries="+queries);
            }
            finally
            {
                observing=false;if(exitObserver!=null)exitObserver.Receipt=null;
                if(installedCorridor!=null&&ReferenceEquals(corridorHook.GetValue(customers),installedCorridor))corridorHook.SetValue(customers,priorCorridor);
                if(blocker!=null){world.Unregister(blocker);UnityEngine.Object.DestroyImmediate(blocker.gameObject);}
                if(owner!=null)CafeLoopRuntimeProof.Set(owner,"stepping",originalStepping);
            }
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
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000,requireCurrentSnapshot:true);
            Assert.That(customers.TrySpawn(), Is.True);
            var first = customers.Snapshot.Single().VisitId;
            yield return Until(() => customers.CafeSnapshot.Single().CounterArrived);
            CafeLoopRuntimeProof.Set(owner,"stepping",true);
            yield return PreparePositiveUnpaidReservation(first);
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
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000,requireCurrentSnapshot:true);
            Assert.That(customers.TrySpawn(), Is.True);
            var second = customers.Snapshot.Single(v => v.VisitId != first).VisitId;
            yield return Until(() => customers.PickupAssignments.Any(a => a.VisitId == second && a.Arrived) &&
                owner.Snapshot.Orders.Values.Any(o => o.CustomerId == second && o.State == OrderState.ReadyForPickup) &&
                employees.Snapshot.All(e => e.State == EmployeeState.Idle));
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000,requireCurrentSnapshot:true);
            Assert.That(customers.TrySpawn(), Is.True);
            var earlier = customers.Snapshot.Single(v => v.VisitId != first && v.VisitId != second).VisitId;
            yield return Until(() => customers.CafeSnapshot.Single(v => v.VisitId == earlier).CounterArrived);
            CafeLoopRuntimeProof.Set(owner,"stepping",true);
            Action unpaidPhysicalUnchanged=null;
            yield return PreparePositiveUnpaidReservation(earlier,(source,verify)=>unpaidPhysicalUnchanged=verify);
            Assert.That(customers.TryReservePickup(earlier, out reason), Is.True, reason);
            Assert.That(pickup.TryGetReservation(earlier, out var assignment), Is.True);
            Assert.That(assignment.Position, Is.EqualTo(projected[2]));
            Assert.That(assignment.Arrived, Is.False);
            // Preserve the real unpaid owner while preparing the original fourth admission, without positive-time business.
            // 复用已验证的zero-time实际身体receipt；只准备原source，原public caller仍负责出生。
            var savedCapture=Time.captureDeltaTime;var savedScale=Time.timeScale;
            try
            {
                Assert.That(unpaidPhysicalUnchanged,Is.Not.Null);Assert.That(Time.deltaTime,Is.Zero);
                Time.captureDeltaTime=0;Time.timeScale=0;unpaidPhysicalUnchanged();
                CafeLoopRuntimeProof.Set(owner,"stepping",false);
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000,unpaidPhysicalUnchanged,requireCurrentSnapshot:true);
                unpaidPhysicalUnchanged();CafeLoopRuntimeProof.Set(owner,"stepping",true);
                Assert.That(customers.TrySpawn(), Is.True, customers.Status);
                var incoming = customers.Snapshot.Single(v => v.VisitId != first && v.VisitId != second && v.VisitId != earlier).VisitId;
                ready(earlier, incoming, pickup);
            }
            finally {Time.captureDeltaTime=savedCapture;Time.timeScale=savedScale;}
        }

        [UnityTest] public IEnumerator RealReservedPickupGoalWitnessUsesActualIncomingReservationProducer()
            => ObserveRealReservedPickupGoal(false);

        private IEnumerator ObserveRealReservedPickupGoal(bool completeCase,string contract=null)
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            object Read(object value,string name)=>value==null?null:value.GetType().GetField(name,flags)?.GetValue(value) ?? value?.GetType().GetProperty(name,flags)?.GetValue(value);
            bool Flag(object value,string name)=>(bool)Read(value,name);
            string Key(TrafficIntentKey value)=>value.ActorId+"/"+value.IntentId+"/g"+value.Generation+"/e"+value.Epoch;
            var savedCapture=Time.captureDeltaTime;var savedScale=Time.timeScale;
            string earlier=null,incoming=null;PickupQueueService pickup=null;ServiceTrafficCoordinator core=null;
            IDictionary entries=null,visits=null;var records=new List<string>();var errors=new List<string>();
            var goalFailures=new HashSet<TrafficIntentKey>();var transitions=0;var overflow=false;string last=null;
            var latestFailureRole=new Dictionary<TrafficIntentKey,string>();
            var window=false;var zero=false;var witnessed=false;var active=false;object earlierReservation=null;
            Action<CafeLoopSnapshot> acceptanceObserver=null,contractObserver=null;var accepted=false;var arrived=false;var positiveComplete=false;
            object arrivalOriginalLeg=null,arrivalOriginalEntry=null;
            TrafficIntentKey arrivalOriginalKey=default;TrafficNativeStamp arrivalOriginalStamp=default;
            var goalStillProtected=false;var fifoStarted=false;var fifoCollected=false;var fifoExited=false;var trackFifo=false;
            var positiveRemaining=6000;
            CafeActualExitObserver fifoExitObserver=null;
            object windowAnchor=null,windowEntry=null;TrafficIntentKey windowKey=default;var windowFirst=0;var windowQueries=0;var windowSequence=0L;
            FieldInfo diagnostic=null;object previousDiagnostic=null;Action<TrafficIntentKey,string,TrafficRequest,TrafficRequest> observeProof=null;
            bool WindowCurrent()
            {
                if(earlier==null || incoming==null || visits==null || !visits.Contains(earlier) || !visits.Contains(incoming))return false;
                var oldVisit=visits[earlier];var nextVisit=visits[incoming];var paid=Read(oldVisit,"PaidPickup");
                var counter=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue");var head=counter.Snapshot.FirstOrDefault();
                return paid!=null && Flag(paid,"Accepted") && Flag(paid,"Transferred") && !Flag(paid,"Failed") &&
                    pickup.TryGetReservation(earlier,out var held) && !held.Arrived &&
                    (CustomerVisitState)Read(nextVisit,"State")==CustomerVisitState.Queued && Flag(nextVisit,"CounterArrived") &&
                    head.VisitId==incoming && head.SlotIndex==0 && head.Arrived && !Flag(nextVisit,"PickupAssigned") &&
                    Read(nextVisit,"PaidPickup")==null && !owner.Snapshot.PaidVisitIds.Contains(incoming) &&
                    !owner.Snapshot.Orders.Values.Any(order=>order.CustomerId==incoming);
            }
            void Observe(CafeLoopSnapshot ignored)
            {
                if(!active || window)return;
                try
                {
                    Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic"),Is.SameAs(core));
                    Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic"),Is.SameAs(core));
                    var oldVisit=visits.Contains(earlier)?visits[earlier]:null;var nextVisit=visits.Contains(incoming)?visits[incoming]:null;
                    var paid=Read(oldVisit,"PaidPickup");var head=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue").Snapshot.FirstOrDefault();
                    var signature="earlierPaid="+(paid!=null && Flag(paid,"Accepted"))+" transferred="+(paid!=null && Flag(paid,"Transferred"))+
                        " earlierArrived="+(pickup.TryGetReservation(earlier,out var held)?held.Arrived.ToString():"MISSING")+
                        " incomingState="+Read(nextVisit,"State")+" counterArrived="+Read(nextVisit,"CounterArrived")+
                        " head="+head.VisitId+"/"+head.SlotIndex+"/"+head.Arrived+" pickupAssigned="+Read(nextVisit,"PickupAssigned")+
                        " anchor="+(Read(nextVisit,"PendingReservationAnchor")!=null)+" incomingPaid="+(Read(nextVisit,"PaidPickup")!=null);
                    if(signature!=last)
                    {last=signature;transitions++;if(transitions<=32)records.Add("GOAL_WINDOW frame="+Time.frameCount+" "+signature);else overflow=true;}
                    if(!WindowCurrent())return;
                    window=true;earlierReservation=CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations")[earlier];
                    windowAnchor=Read(nextVisit,"PendingReservationAnchor");
                    if(windowAnchor!=null)
                    {
                        windowKey=(TrafficIntentKey)Read(windowAnchor,"Key");Assert.That(entries.Contains(windowKey),Is.True);
                        windowEntry=entries[windowKey];windowFirst=(int)Read(windowEntry,"FirstFrame");
                        windowQueries=(int)Read(windowEntry,"Queries");windowSequence=(long)Read(windowEntry,"RequestSequence");
                    }
                    // Ordinary business event only. Enter zero-time; physical baseline is taken after delta actually reaches zero.
                    // 正常事件只冻结时间；不还原身体、不制造到位，不在diagnostic callback执行业务。
                    Time.captureDeltaTime=0;Time.timeScale=0;
                    records.Add("GOAL_WINDOW_CAPTURE frame="+Time.frameCount+" earlierGoal="+held.Position.ToString("F6"));
                }
                catch(Exception error){if(errors.Count<4)errors.Add(error.ToString());}
            }
            try
            {
                yield return PrepareRealCanonicalParkingGoal((a,b,q)=>{earlier=a;incoming=b;pickup=q;});
                core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
                entries=CafeLoopRuntimeProof.Field<IDictionary>(core,"entries");visits=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");
                var native=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byId");
                var nativeByActor=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor");
                Assert.That((int)Read(core,"queriesPerFrame"),Is.EqualTo(128));Assert.That((int)Read(core,"queriesPerIntent"),Is.EqualTo(16384));
                diagnostic=typeof(ServiceTrafficCoordinator).GetField("UnpaidPreparationObserver",flags);
                previousDiagnostic=diagnostic.GetValue(core);Assert.That(previousDiagnostic,Is.Null,"Own only this focused witness's read-only diagnostic");
                observeProof=(intent,stage,request,previous)=>
                {
                    if(intent.ActorId!=incoming || stage!="actual-spatial-refusal" || !entries.Contains(intent))return;
                    try
                    {
                        var row=entries[intent];var job=(int)Read(row,"JobIndex")-(request.PlanPrimaryRoute?1:0);
                        var role=job>=0 && job<request.Requirements.Count?request.Requirements[job].ResourceId:null;
                        if(role=="existing-goal/"+earlier || role=="existing-goal/"+earlier+"/exit-capability")goalFailures.Add(intent);
                        latestFailureRole[intent]=role;
                        if(records.Count<64)records.Add("GOAL_ORIGINAL_SPATIAL_REFUSAL key="+Key(intent)+" failedRole="+(role??"PRIMARY_OR_ENDPOINT")+
                            " queries="+Read(row,"Queries")+" target="+request.Target.ToString("F6"));
                    }
                    catch(Exception error){if(errors.Count<4)errors.Add(error.ToString());}
                };
                diagnostic.SetValue(core,observeProof);owner.SnapshotChanged+=Observe;active=true;
                CafeLoopRuntimeProof.Set(owner,"stepping",false);
                for(var frame=0;frame<6000 && !window && errors.Count==0;frame++)yield return null;
                Assert.That(errors,Is.Empty,string.Join("\n",errors));
                Assert.That(window,Is.True,"PremiseNotProven: no actual paid/unarrived earlier owner and arrived unpaid incoming Counter head within 6000 frames");
                for(var transition=0;transition<3 && Time.deltaTime!=0;transition++)yield return null;
                Assert.That(Time.deltaTime,Is.Zero,"PremiseNotProven: real zero-time transition missing");zero=true;
                Assert.That(WindowCurrent(),Is.True,"PremiseNotProven: actual business window ended before zero-time, without pose repair");
                Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations")[earlier],Is.SameAs(earlierReservation));
                var oldAssignment=default(QueueAssignment);Assert.That(pickup.TryGetReservation(earlier,out oldAssignment),Is.True);
                var bodies=world.RegisteredActors.OrderBy(actor=>actor.ActorId,StringComparer.Ordinal)
                    .Select(actor=>(Actor:actor,Pose:actor.transform.position,Rotation:actor.transform.rotation)).ToArray();
                var requests=native.Keys.Cast<long>().OrderBy(handle=>handle).Select(handle=>(Id:handle,Request:native[handle],Generation:(int)Read(native[handle],"Generation"))).ToArray();
                var orders=owner.Snapshot.Orders.ToArray();var paidIds=owner.Snapshot.PaidVisitIds.ToArray();var tokens=customers.Capacity.GetReservations().ToArray();
                var counterAssignments=customers.CounterAssignments.ToArray();var nextNumber=CafeLoopRuntimeProof.Field<long>(customers,"nextVisit");
                var visit=visits[incoming];var actorLife=Read(visit,"Actor");var anchor=Read(visit,"PendingReservationAnchor");
                if(windowAnchor!=null)
                {
                    Assert.That(anchor,Is.SameAs(windowAnchor));Assert.That(entries[windowKey],Is.SameAs(windowEntry));
                    Assert.That(Read(windowEntry,"FirstFrame"),Is.EqualTo(windowFirst));
                    Assert.That((int)Read(windowEntry,"Queries"),Is.GreaterThanOrEqualTo(windowQueries));
                    if(windowSequence>0)Assert.That(Read(windowEntry,"RequestSequence"),Is.EqualTo(windowSequence));
                }
                void PhysicalUnchanged()
                {
                    Assert.That(Time.deltaTime,Is.Zero);Assert.That(Time.timeScale,Is.Zero);Assert.That(Time.captureDeltaTime,Is.Zero);
                    Assert.That(WindowCurrent(),Is.True);Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.EqualTo(nextNumber));
                    Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic"),Is.SameAs(core));
                    Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic"),Is.SameAs(core));
                    CollectionAssert.AreEqual(bodies.Select(row=>row.Actor),world.RegisteredActors.OrderBy(actor=>actor.ActorId,StringComparer.Ordinal));
                    foreach(var row in bodies){Assert.That(row.Actor.transform.position,Is.EqualTo(row.Pose));Assert.That(row.Actor.transform.rotation,Is.EqualTo(row.Rotation));}
                    CollectionAssert.AreEquivalent(requests.Select(row=>row.Id),native.Keys.Cast<long>());
                    foreach(var row in requests){Assert.That(native[row.Id],Is.SameAs(row.Request));Assert.That(Read(row.Request,"Generation"),Is.EqualTo(row.Generation));}
                    CollectionAssert.AreEqual(paidIds,owner.Snapshot.PaidVisitIds);CollectionAssert.AreEqual(counterAssignments,customers.CounterAssignments);
                    CollectionAssert.AreEquivalent(orders.Select(pair=>pair.Key),owner.Snapshot.Orders.Keys);
                    foreach(var pair in orders){Assert.That(owner.Snapshot.Orders[pair.Key].CustomerId,Is.EqualTo(pair.Value.CustomerId));Assert.That(owner.Snapshot.Orders[pair.Key].State,Is.EqualTo(pair.Value.State));}
                    var current=customers.Capacity.GetReservations().ToArray();Assert.That(current.Length,Is.EqualTo(tokens.Length));
                    foreach(var token in tokens){var retained=current.Single(row=>ReferenceEquals(row.Token,token.Token));Assert.That(retained.OwnerId,Is.EqualTo(token.OwnerId));Assert.That(retained.Kind,Is.EqualTo(token.Kind));Assert.That(retained.State,Is.EqualTo(token.State));}
                    Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations")[earlier],Is.SameAs(earlierReservation));
                    Assert.That(pickup.TryGetReservation(earlier,out var retainedGoal) && retainedGoal.Equals(oldAssignment),Is.True);
                    Assert.That(pickup.TryGetReservation(incoming,out _),Is.False);Assert.That((long)Read(visit,"RequestId"),Is.EqualTo(0L));
                    Assert.That(pickup.ServiceLeases.Any(lease=>lease.Key==incoming),Is.False,"Preparation cannot grant an incoming service lease");
                    Assert.That(native.Values.Cast<object>().Any(row=>(string)Read(row,"ActorId")==incoming),Is.False);
                }
                PhysicalUnchanged();
                if(anchor==null)
                {
                    Assert.That(customers.TryReservePickup(incoming,out var reason),Is.False,"One real original prime cannot acquire the protected candidate: "+reason);
                    anchor=Read(visit,"PendingReservationAnchor");
                }
                Assert.That(anchor,Is.Not.Null,"PremiseNotProven: real Counter-head public prime retained no source");
                var sourceKey=(TrafficIntentKey)Read(anchor,"Key");Assert.That(entries.Contains(sourceKey),Is.True);var entry=entries[sourceKey];
                var first=(int)Read(entry,"FirstFrame");var finite=(float)Read(entry,"FiniteLimit");var counterClaim=Read(anchor,"CounterClaim");
                var eligibility=Read(anchor,"Eligibility");Assert.That(eligibility,Is.Not.Null);
                var preview=(PickupQueueService.PreparedMutation)Read(anchor,"Preview");Assert.That(preview.IsCurrent,Is.True);
                var assignment=preview.Assignment;var previousQueries=(int)Read(entry,"Queries");var sequence=(long)Read(entry,"RequestSequence");
                records.Add("GOAL_REAL_PRODUCER_SOURCE key="+Key(sourceKey)+" first="+first+" sequence="+sequence+
                    " queries="+previousQueries+" target="+assignment.Position.ToString("F6")+" earlierGoal="+oldAssignment.Position.ToString("F6")+
                    " roles=["+string.Join(";",((TrafficRequest)Read(entry,"Request")).Requirements.Select(role=>role.ResourceId+":"+role.Start.ToString("F4")+"->"+role.Goal.ToString("F4")))+"]");
                var previousWait=(float)Read(entry,"WaitRemaining");var waitStarted=previousWait>0;TrafficDecision result=null;
                // No source renewal is authorized by unrelated PQS version changes or earlier MarkArrived.
                // 默认坚持同一真实机会；没有typed context变更receipt就不接受新key/age/debt。
                var contractGeneration=Read(visit,"Generation");var contractPhase=Read(visit,"TrafficPhaseSequence");
                var contractCounterWasCurrent=(bool)Read(counterClaim,"IsCurrent");
                void SameOriginalContractSource()
                {
                    Assert.That(Read(visit,"PendingReservationAnchor"),Is.SameAs(anchor),"PremiseNotProven: original typed opportunity changed; a global PQS version is not a source-renewal receipt");
                    Assert.That(Read(anchor,"Key"),Is.EqualTo(sourceKey));Assert.That(entries[sourceKey],Is.SameAs(entry));
                    Assert.That(Read(anchor,"CounterClaim"),Is.SameAs(counterClaim));Assert.That(Read(anchor,"Eligibility"),Is.SameAs(eligibility));
                    if(contractCounterWasCurrent)Assert.That(Read(counterClaim,"IsCurrent"),Is.EqualTo(true));
                    var currentPreview=Read(anchor,"Preview") as PickupQueueService.PreparedMutation;Assert.That(currentPreview,Is.Not.Null);
                    Assert.That(currentPreview.Assignment,Is.EqualTo(assignment),"Metadata refresh may replace Preview, never its original assignment");
                    var currentRequest=Read(entry,"Request") as TrafficRequest;Assert.That(currentRequest,Is.Not.Null);Assert.That(currentRequest.Target,Is.EqualTo(assignment.Position));
                    Assert.That(Read(entry,"UnpaidEligibility"),Is.SameAs(eligibility));
                    Assert.That(Read(anchor,"Actor"),Is.SameAs(actorLife));Assert.That(Read(anchor,"Visit"),Is.SameAs(visit));Assert.That(Read(anchor,"Coordinator"),Is.SameAs(core));
                    Assert.That(Read(visit,"Generation"),Is.EqualTo(contractGeneration));Assert.That(Read(visit,"TrafficPhaseSequence"),Is.EqualTo(contractPhase));
                    Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(first));Assert.That(Read(entry,"FiniteLimit"),Is.EqualTo(finite));
                    var count=(int)Read(entry,"Queries");Assert.That(count,Is.InRange(previousQueries,16384));previousQueries=count;
                    var currentSequence=(long)Read(entry,"RequestSequence");if(sequence==0&&currentSequence>0)sequence=currentSequence;Assert.That(currentSequence,Is.EqualTo(sequence));
                    var debt=(float)Read(entry,"WaitRemaining");if(waitStarted)Assert.That(debt,Is.LessThanOrEqualTo(previousWait));if(debt>0)waitStarted=true;previousWait=debt;
                    Assert.That(Read(entry,"AcquiredSequence"),Is.EqualTo(0L));Assert.That(Read(entry,"JourneyPermit"),Is.Null);Assert.That(Read(entry,"Leg"),Is.Null);
                    Assert.That(core.HasActiveGrant(sourceKey),Is.False);Assert.That(core.TryGetCommittedJourney(sourceKey,out _),Is.False);
                    Assert.That(core.QueriesThisFrame,Is.LessThanOrEqualTo(128));
                }
                var near=typeof(ServiceTrafficCoordinator).GetMethod("NearPath",BindingFlags.Static|BindingFlags.NonPublic);
                for(var frame=0;frame<=6000;frame++)
                {
                    PhysicalUnchanged();Assert.That(errors,Is.Empty,string.Join("\n",errors));
                    Assert.That(Read(visit,"PendingReservationAnchor"),Is.SameAs(anchor));Assert.That(entries[sourceKey],Is.SameAs(entry));
                    Assert.That(Read(anchor,"CounterClaim"),Is.SameAs(counterClaim));Assert.That(Read(anchor,"Eligibility"),Is.SameAs(eligibility));
                    Assert.That(Read(counterClaim,"IsCurrent"),Is.EqualTo(true));Assert.That(Read(anchor,"Visit"),Is.SameAs(visit));
                    Assert.That(Read(anchor,"Coordinator"),Is.SameAs(core));Assert.That(Read(anchor,"Owner"),Is.SameAs(pickup));
                    Assert.That(Read(entry,"UnpaidEligibility"),Is.SameAs(eligibility));Assert.That(Read(entry,"Leg"),Is.Null);
                    Assert.That(Read(anchor,"Key"),Is.EqualTo(sourceKey));Assert.That(Read(anchor,"Actor"),Is.SameAs(actorLife));
                    Assert.That(Read(anchor,"FirstFrame"),Is.EqualTo(first));
                    Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(first));Assert.That(Read(entry,"FiniteLimit"),Is.EqualTo(finite));
                    var currentQueries=(int)Read(entry,"Queries");Assert.That(currentQueries,Is.InRange(previousQueries,16384));previousQueries=currentQueries;
                    Assert.That(core.QueriesThisFrame,Is.LessThanOrEqualTo(128));
                    var currentSequence=(long)Read(entry,"RequestSequence");if(sequence==0 && currentSequence>0)sequence=currentSequence;
                    Assert.That(currentSequence,Is.EqualTo(sequence));
                    var remaining=(float)Read(entry,"WaitRemaining");if(waitStarted)Assert.That(remaining,Is.LessThanOrEqualTo(previousWait));
                    if(remaining>0)waitStarted=true;previousWait=remaining;
                    Assert.That(Read(entry,"AcquiredSequence"),Is.EqualTo(0L));Assert.That(Read(entry,"JourneyPermit"),Is.Null);
                    Assert.That(core.HasActiveGrant(sourceKey),Is.False);Assert.That(core.TryGetCommittedJourney(sourceKey,out _),Is.False);
                    preview=(PickupQueueService.PreparedMutation)Read(anchor,"Preview");Assert.That(preview.IsCurrent && preview.Assignment.Equals(assignment),Is.True);
                    var request=(TrafficRequest)Read(entry,"Request");result=core.GetDecision(sourceKey);
                    Assert.That(request.Intent,Is.EqualTo(sourceKey));Assert.That(request.RequestedFrame,Is.EqualTo(first));Assert.That(request.Target,Is.EqualTo(assignment.Position));
                    Assert.That(request.ResourceId,Is.EqualTo("pickup/"+incoming));
                    Assert.That(result.AcquiredSequence,Is.Zero);Assert.That(request.NativeStamp.RequestId,Is.Zero);
                    var earlierMove=Read(visits[earlier],"TrafficMove");var earlierIntent=earlierMove==null?default:(TrafficIntentKey)Read(earlierMove,"Intent");
                    var earlierEntry=earlierMove!=null && entries.Contains(earlierIntent)?entries[earlierIntent]:null;
                    var currentPath=Read(earlierEntry,"CurrentPath") as Vector3[];
                    var oldBody=(NavigationActor)Read(visits[earlier],"Actor");
                    var clearance=request.Radius+oldBody.Settings.AgentRadius+Math.Max(request.SafetyMargin,oldBody.Settings.CollisionSkin+oldBody.Settings.Epsilon);
                    var currentConflict=currentPath!=null && currentPath.Length>1 && (bool)near.Invoke(null,new object[]{request.Target,currentPath,clearance});
                    var actualEarlierEdge=result.Blockers.Any(block=>block.Owner.Equals(earlierIntent) && block.ResourceId=="pickup/"+earlier &&
                        block.Proof=="selected-journey-parking-protection");
                    // Only the same real Bound leg's complete unissued selected suffix is additional evidence.
                    // Only current geometry is trimmed; a consumed current-leg prefix is never a future-route witness.
                    var earlierOwnedSource=Read(earlierMove,"Source") as TrafficDecision;
                    var earlierLeg=Read(earlierEntry,"Leg");var earlierProof=Read(earlierLeg,"Proof") as TrafficRouteProof;
                    var unissuedSuffix=Read(earlierLeg,"Suffix") as Vector3[];
                    var currentNativeStamp=Read(earlierLeg,"Stamp") is TrafficNativeStamp actualStamp?actualStamp:default;
                    var nativeRequest=currentNativeStamp.RequestId>0 && native.Contains(currentNativeStamp.RequestId)?native[currentNativeStamp.RequestId]:null;
                    var suffixAuthenticated=earlierEntry!=null && earlierOwnedSource!=null && earlierOwnedSource.AcquiredSequence>0 &&
                        Read(earlierEntry,"JourneyPermit")==earlierOwnedSource && !Flag(earlierEntry,"Retired") && Flag(earlierEntry,"Active") &&
                        earlierLeg!=null && Flag(earlierLeg,"Committed") && Flag(earlierLeg,"Bound") && !Flag(earlierLeg,"Completed") && !Flag(earlierLeg,"Suspended") &&
                        oldBody.isActiveAndEnabled && ReferenceEquals(Read(earlierLeg,"ActorLife"),oldBody) &&
                        currentNativeStamp.LegGeneration==(int)Read(earlierLeg,"Generation") &&
                        currentNativeStamp.LegGeneration==(int)Read(earlierMove,"NativeGeneration") &&
                        currentNativeStamp.LegGeneration==(int)Read(visits[earlier],"Generation") &&
                        currentNativeStamp.RequestId==(long)Read(visits[earlier],"RequestId") && nativeRequest!=null &&
                        (string)Read(nativeRequest,"ActorId")==earlier && nativeByActor.Contains(earlier) && ReferenceEquals(nativeByActor[earlier],nativeRequest) &&
                        earlierProof!=null && ReferenceEquals(Read(earlierMove,"Proof"),earlierProof) &&
                        earlierOwnedSource.ProofRoutes.Any(proof=>ReferenceEquals(proof,earlierProof)) && earlierProof.ResourceId=="pickup/"+earlier &&
                        earlierProof.Waypoints.Count>0 && earlierProof.Waypoints.Last()==oldAssignment.Position &&
                        unissuedSuffix!=null && unissuedSuffix.Length>1 && unissuedSuffix.Contains(oldAssignment.Position) &&
                        core.TryGetCommittedJourney(earlierIntent,out var ownedSource) && ReferenceEquals(ownedSource,earlierOwnedSource) &&
                        core.TryGetCommittedLegProvenance(earlierIntent,oldBody,currentNativeStamp.LegGeneration,out var issuedSource,out _) &&
                        ReferenceEquals(issuedSource,earlierOwnedSource);
                    var selectedSuffixConflict=suffixAuthenticated && (bool)near.Invoke(null,new object[]{request.Target,unissuedSuffix,clearance});
                    if(contract=="arrival"&&frame==0)
                    {
                        Assert.That(suffixAuthenticated,Is.True,"PremiseNotProven: capture a genuinely Bound original customer Native leg before real arrival");
                        arrivalOriginalLeg=earlierLeg;arrivalOriginalEntry=earlierEntry;arrivalOriginalKey=earlierIntent;arrivalOriginalStamp=currentNativeStamp;
                    }
                    if(frame==0)records.Add("GOAL_EARLIER_CURRENT key="+(earlierMove==null?"NONE":Key(earlierIntent))+
                        " pose="+oldBody.transform.position.ToString("F6")+" path=["+string.Join(";",(currentPath??Array.Empty<Vector3>()).Select(point=>point.ToString("F6")))+"]"+
                        " ownedSourceAcquired="+(earlierOwnedSource?.AcquiredSequence??0)+" currentNativeStamp="+currentNativeStamp.RequestId+"/"+currentNativeStamp.LegGeneration+
                        " suffixAuthenticated="+suffixAuthenticated+" selectedSuffixConflict="+selectedSuffixConflict+
                        " unissuedSuffix=["+string.Join(";",(unissuedSuffix??Array.Empty<Vector3>()).Select(point=>point.ToString("F6")))+"]");
                    var held=Flag(entry,"UnpaidEligibilityHeld");var refusal=Read(entry,"UnpaidEligibilitySpatialRefusal") as TrafficDecision;
                    var goalConflict=goalFailures.Contains(sourceKey) && held && refusal!=null &&
                        ((IEnumerable)Read(entry,"UnpaidEligibilitySpatialActors")).Cast<string>().Contains(earlier);
                    witnessed=(held || result.Kind==TrafficDecisionKind.WaitingForTraffic) && (((currentConflict||selectedSuffixConflict)&&actualEarlierEdge)||goalConflict);
                    if(witnessed)
                    {
                        records.Add("GOAL_REAL_PRODUCER_WITNESS key="+Key(sourceKey)+" kind="+result.Kind+" reason="+result.Reason+
                            " first="+first+" sequence="+sequence+" queries="+currentQueries+" wait="+remaining+" target="+request.Target.ToString("F6")+
                            " currentPathConflict="+currentConflict+" selectedSuffixConflict="+selectedSuffixConflict+" suffixAuthenticated="+suffixAuthenticated+
                            " originalEarlierEdge="+actualEarlierEdge+" originalGoalFailure="+goalConflict+" acquired=0 noIncomingReservationNativePayment=True");
                        break;
                    }
                    if(held || result.Kind==TrafficDecisionKind.Granted || result.FailureDisposition==TrafficFailureDisposition.Terminal ||
                        result.FailureDisposition==TrafficFailureDisposition.AlternateAllowed)
                    {records.Add("GOAL_OTHER_ACTUAL_RESULT key="+Key(sourceKey)+" kind="+result.Kind+" reason="+result.Reason+" held="+held+" queries="+currentQueries+" currentConflict="+currentConflict+" selectedSuffixConflict="+selectedSuffixConflict+" suffixAuthenticated="+suffixAuthenticated+" earlierEdge="+actualEarlierEdge+" roleEvidence="+goalFailures.Contains(sourceKey)+" blockers=["+string.Join(";",result.Blockers.Select(block=>Key(block.Owner)+"/"+block.ResourceId+"/"+block.Proof))+"]");break;}
                    Assert.That(frame,Is.LessThan(6000),"PremiseNotProven: original actual source never exposed a protected-goal result");
                    yield return null;
                }
                Assert.That(witnessed,Is.True,"PremiseNotProven: real producer exposed no actual earlier current-route/unissued-selected-suffix/goal refusal; plain Deferred is not proof");
                Assert.That(errors,Is.Empty,string.Join("\n",errors));PhysicalUnchanged();
                if(contract=="arrival")
                {
                    Assert.That(arrivalOriginalLeg,Is.Not.Null,"PremiseNotProven: original owned Bound Native leg was not captured");
                    var arrivalSeen=false;var arrivalRemaining=6000;
                    contractObserver=ignored=>
                    {
                        if(arrivalSeen)return;
                        try
                        {
                            SameOriginalContractSource();
                            if(!pickup.TryGetReservation(earlier,out var actual)||!actual.Arrived)return;
                            Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations")[earlier],Is.SameAs(earlierReservation));
                            arrivalSeen=true;Time.captureDeltaTime=0;Time.timeScale=0;
                        }
                        catch(Exception error){if(errors.Count<4)errors.Add(error.ToString());}
                    };
                    owner.SnapshotChanged+=contractObserver;Time.captureDeltaTime=savedCapture;Time.timeScale=savedScale;
                    while(arrivalRemaining>0&&!arrivalSeen&&errors.Count==0){arrivalRemaining--;yield return null;}
                    Assert.That(errors,Is.Empty,string.Join("\n",errors));Assert.That(arrivalSeen,Is.True,"PremiseNotProven: real canonical Arrived callback was not observed");
                    for(var transition=0;transition<3&&Time.deltaTime!=0;transition++){Assert.That(arrivalRemaining--,Is.GreaterThan(0));yield return null;}
                    Assert.That(Time.deltaTime,Is.Zero);var arrivedBody=(NavigationActor)Read(visits[earlier],"Actor");
                    Assert.That(pickup.TryGetReservation(earlier,out var actualGoal)&&actualGoal.Arrived,Is.True);
                    Assert.That(actualGoal.Position,Is.EqualTo(oldAssignment.Position));Assert.That(actualGoal.Generation,Is.EqualTo(oldAssignment.Generation));
                    Assert.That(Flag(arrivalOriginalLeg,"Completed"),Is.True,"Original Bound Native leg must really complete, not merely lose its Entry");
                    Assert.That(native.Contains(arrivalOriginalStamp.RequestId),Is.False);Assert.That((long)Read(visits[earlier],"RequestId"),Is.Zero);
                    Assert.That(nativeByActor.Contains(earlier),Is.False);Assert.That(Read(visits[earlier],"State"),Is.EqualTo(CustomerVisitState.PickupWaiting));
                    Assert.That(Vector3.Distance(arrivedBody.transform.position,actualGoal.Position),Is.LessThanOrEqualTo(arrivedBody.Settings.ArrivalDistance+.001f));
                    var finishedLeg=Read(arrivalOriginalEntry,"Leg");if(finishedLeg!=null)Assert.That(Flag(finishedLeg,"Completed"),Is.True);
                    Assert.That(Flag(arrivalOriginalEntry,"Active"),Is.False);Assert.That(((Vector3[])Read(arrivalOriginalEntry,"CurrentPath")).Length,Is.Zero);
                    var arrivedPoses=world.RegisteredActors.Select(a=>(Actor:a,Pose:a.transform.position)).ToArray();
                    void ArrivedUnpaidUnchanged()
                    {
                        Assert.That(Time.deltaTime,Is.Zero);SameOriginalContractSource();Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic"),Is.SameAs(core));
                        foreach(var pose in arrivedPoses)Assert.That(pose.Actor.transform.position,Is.EqualTo(pose.Pose));
                        Assert.That(pickup.TryGetReservation(earlier,out var retained)&&retained.Equals(actualGoal),Is.True);
                        Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations")[earlier],Is.SameAs(earlierReservation));
                        Assert.That(customers.Capacity.GetCapacities().Single(c=>c.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers).Used,Is.EqualTo(4));
                        Assert.That(Flag(visit,"PickupAssigned"),Is.False);Assert.That(Read(visit,"PaidPickup"),Is.Null);Assert.That(pickup.TryGetReservation(incoming,out _),Is.False);
                        Assert.That(owner.Snapshot.PaidVisitIds.Contains(incoming)||owner.Snapshot.Orders.Values.Any(order=>order.CustomerId==incoming),Is.False);
                        Assert.That((long)Read(visit,"RequestId"),Is.Zero);Assert.That(native.Values.Cast<object>().Any(row=>(string)Read(row,"ActorId")==incoming),Is.False);
                    }
                    var arrivedHeld=false;object arrivedAnchor=null,arrivedEntry=null;TrafficIntentKey arrivedKey=default;
                    while(arrivalRemaining-->0&&!arrivedHeld)
                    {
                        ArrivedUnpaidUnchanged();Assert.That(errors,Is.Empty,string.Join("\n",errors));arrivedAnchor=Read(visit,"PendingReservationAnchor");
                        if(arrivedAnchor!=null)
                        {
                            arrivedKey=(TrafficIntentKey)Read(arrivedAnchor,"Key");Assert.That(arrivedKey,Is.EqualTo(sourceKey));
                            arrivedEntry=entries[arrivedKey];Assert.That(arrivedEntry,Is.SameAs(entry));var current=core.GetDecision(arrivedKey);
                            arrivedHeld=Flag(arrivedEntry,"UnpaidEligibilityHeld")&&latestFailureRole.TryGetValue(arrivedKey,out var role)&&role=="existing-goal/"+earlier+"/exit-capability";
                            if(arrivedHeld)
                            {
                                Assert.That(current.Kind,Is.Not.EqualTo(TrafficDecisionKind.WaitingForTraffic));Assert.That(current.AcquiredSequence,Is.Zero);
                                Assert.That(current.Blockers.Any(b=>b.Owner.Equals(arrivalOriginalKey)&&(b.ResourceId=="body"||b.Proof=="current-conflict-segment"||b.Proof=="selected-journey-parking-protection")),Is.False);
                                break;
                            }
                        }
                        yield return null;
                    }
                    Assert.That(arrivedHeld,Is.True,"PremiseNotProven: actual Arrived owner must expose retained exit-capability refusal, not a stale movement WAIT");
                    var age=Read(arrivedEntry,"FirstFrame");var seq=Read(arrivedEntry,"RequestSequence");var q=(int)Read(arrivedEntry,"Queries");var debt=Read(arrivedEntry,"WaitRemaining");
                    Assert.That(customers.TryReservePickup(incoming,out var arrivedReason),Is.False,arrivedReason);ArrivedUnpaidUnchanged();
                    Assert.That(Read(visit,"PendingReservationAnchor"),Is.SameAs(arrivedAnchor));Assert.That(entries[arrivedKey],Is.SameAs(arrivedEntry));
                    Assert.That(Read(arrivedEntry,"FirstFrame"),Is.EqualTo(age));Assert.That(Read(arrivedEntry,"RequestSequence"),Is.EqualTo(seq));
                    Assert.That(Read(arrivedEntry,"Queries"),Is.EqualTo(q));Assert.That(Read(arrivedEntry,"WaitRemaining"),Is.EqualTo(debt));
                    Assert.That(Read(arrivedEntry,"AcquiredSequence"),Is.EqualTo(0L));Assert.That(Read(arrivedEntry,"JourneyPermit"),Is.Null);
                    Assert.That(arrivedKey,Is.EqualTo(sourceKey));Assert.That(arrivedEntry,Is.SameAs(entry));SameOriginalContractSource();
                    Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(first));Assert.That(Read(entry,"RequestSequence"),Is.EqualTo(sequence));
                    Assert.That((int)Read(entry,"Queries"),Is.GreaterThanOrEqualTo(previousQueries));
                    records.Add("GOAL_REAL_ARRIVAL original="+Key(arrivalOriginalKey)+" native="+arrivalOriginalStamp.RequestId+"/"+arrivalOriginalStamp.LegGeneration+
                        " originalLegCompleted=True oldNativeGone=True currentPathEmpty=True actualExitCapabilityHeld=True incoming="+Key(arrivedKey)+" usedFrames="+(6000-arrivalRemaining));
                    yield break;
                }
                if(completeCase)
                {
                    // Independent control, one CFC-created descriptor/target only. Granted is preparation, not a PQS allocation.
                    // 不相关控制只等原兼容probe真正Granted，再由原caller撤销；不重定向它去测负例。
                    var probe=typeof(CustomerFlowController).GetMethod("NewPickupTargetClear",flags);
                    var unrelated=new Vector3(2.5f,0,-2.7f);
                    Assert.That((bool)probe.Invoke(customers,new object[]{visit,unrelated}),Is.False,"Original unrelated prime must be asynchronous");
                    var probes=new List<DictionaryEntry>();
                    foreach(DictionaryEntry row in entries)
                        if(((TrafficIntentKey)row.Key).ActorId==incoming && ((TrafficIntentKey)row.Key).IntentId=="parking-probe")probes.Add(row);
                    Assert.That(probes.Count,Is.EqualTo(1));var probeKey=(TrafficIntentKey)probes[0].Key;var probeEntry=probes[0].Value;
                    var probeFirst=(int)Read(probeEntry,"FirstFrame");var probeQueries=(int)Read(probeEntry,"Queries");var probeSequence=(long)Read(probeEntry,"RequestSequence");
                    for(var frame=0;frame<=6000;frame++)
                    {
                        PhysicalUnchanged();Assert.That(entries[probeKey],Is.SameAs(probeEntry));
                        Assert.That(Read(probeEntry,"FirstFrame"),Is.EqualTo(probeFirst));
                        var count=(int)Read(probeEntry,"Queries");Assert.That(count,Is.InRange(probeQueries,16384));probeQueries=count;
                        var seq=(long)Read(probeEntry,"RequestSequence");if(probeSequence==0 && seq>0)probeSequence=seq;
                        Assert.That(seq,Is.EqualTo(probeSequence));Assert.That(core.QueriesThisFrame,Is.LessThanOrEqualTo(128));
                        Assert.That(Read(probeEntry,"AcquiredSequence"),Is.EqualTo(0L));Assert.That(Read(probeEntry,"JourneyPermit"),Is.Null);
                        Assert.That(((TrafficRequest)Read(probeEntry,"Request")).Target,Is.EqualTo(unrelated));
                        var decision=core.GetDecision(probeKey);
                        Assert.That(decision.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None),decision.Kind+"/"+decision.Reason);
                        Assert.That(entries[sourceKey],Is.SameAs(entry));Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(first));
                        Assert.That(Read(entry,"RequestSequence"),Is.EqualTo(sequence));
                        var originalQueries=(int)Read(entry,"Queries");Assert.That(originalQueries,Is.InRange(previousQueries,16384));previousQueries=originalQueries;
                        var originalDebt=(float)Read(entry,"WaitRemaining");if(waitStarted)Assert.That(originalDebt,Is.LessThanOrEqualTo(previousWait));
                        if(originalDebt>0)waitStarted=true;previousWait=originalDebt;
                        Assert.That(Read(visit,"PendingReservationAnchor"),Is.SameAs(anchor));Assert.That(Read(entry,"AcquiredSequence"),Is.EqualTo(0L));
                        if(decision.Kind==TrafficDecisionKind.Granted)break;
                        Assert.That(decision.Kind==TrafficDecisionKind.DeferredByBudget || decision.Kind==TrafficDecisionKind.WaitingForTraffic,Is.True);
                        Assert.That(frame,Is.LessThan(6000),"Actual unrelated control never proved Granted");yield return null;
                    }
                    Assert.That((bool)probe.Invoke(customers,new object[]{visit,unrelated}),Is.True,"An unrelated candidate needs actual current proof, not initial Deferred");
                    Assert.That(Read(probeEntry,"Discarded"),Is.EqualTo(true));Assert.That(Read(probeEntry,"AcquiredSequence"),Is.EqualTo(0L));PhysicalUnchanged();
                    records.Add("GOAL_UNRELATED_PREPARED_CONTROL key="+Key(probeKey)+" first="+probeFirst+" queries="+probeQueries+" sameTarget=True originalCallerDiscarded=True acquired=0 noCanonicalAllocation=True");

                    // Resume ordinary service. Acceptance may precede Arrived if the current consumed conflict is safely clear.
                    // 正常推进后只采真实共同提交；不要求到终点后才可接受，也不删除原PQS owner。
                    var totals=tokens.Where(token=>token.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers).ToArray();
                    var positiveTransitions=0;var positiveOverflow=false;string lastPositive=null;
                    string lastCollectionPublication=null;var gapLogged=false;
                    string fifoGuest=null;NavigationActor fifoBody=null;
                    string Claims()=>string.Join(";",pickup.Snapshot.Select(claim=>claim.VisitId+"/"+claim.SlotIndex+"/"+claim.Generation+"/"+claim.Position.ToString("F6")+"/"+claim.Arrived));
                    string Descriptor(TrafficRequest value)=>value==null?"NULL":value.Target.ToString("F6")+"|"+
                        string.Join(";",value.Requirements.OrderBy(role=>role.ResourceId,StringComparer.Ordinal).Select(role=>role.ResourceId+":"+role.Start.ToString("F6")+"->"+role.Goal.ToString("F6")));
                    var trackedKey=sourceKey;var trackedEntry=entry;var trackedAnchor=anchor;var trackedFirst=first;var trackedFinite=finite;
                    var trackedSequence=sequence;var trackedQueries=previousQueries;var trackedWait=previousWait;var trackedWaitStarted=waitStarted;
                    var trackedClaims=Claims();var trackedDescriptor=Descriptor((TrafficRequest)Read(entry,"Request"));var trackedVersion=pickup.Version;
                    var sourceLives=1;var totalObservedQueries=previousQueries;
                    acceptanceObserver=ignored=>
                    {
                        try
                        {
                            Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic"),Is.SameAs(core));
                            Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic"),Is.SameAs(core));
                            if(fifoStarted && !fifoCollected)
                            {
                                var completed=owner.Snapshot.Orders.Values.Any(order=>order.CustomerId==fifoGuest&&order.State==OrderState.Completed);
                                var confirmed=visits.Contains(fifoGuest)&&Flag(visits[fifoGuest],"HasCollected");
                                var publication=completed+"/"+confirmed;
                                if(publication!=lastCollectionPublication){lastCollectionPublication=publication;records.Add("GOAL_FIFO_PUBLICATION frame="+Time.frameCount+" completed="+completed+" hasCollected="+confirmed);}
                                // Publication may be observed in pieces; wait for both facts instead of asserting atomic visibility.
                                if(completed&&confirmed)
                                {
                                    fifoCollected=true;tools.HoldCollection(); // Rehold before a later head can acquire the occupied service lease.
                                    records.Add("GOAL_FIFO_COLLECTED frame="+Time.frameCount+" visit="+fifoGuest+" reheldPublicly=True");
                                }
                            }
                            Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations")[earlier],Is.SameAs(earlierReservation));
                            Assert.That(pickup.TryGetReservation(earlier,out var oldGoal),Is.True);arrived=oldGoal.Arrived;
                            if(!fifoStarted){Assert.That(oldGoal.Position,Is.EqualTo(oldAssignment.Position));Assert.That(oldGoal.Generation,Is.EqualTo(oldAssignment.Generation));}
                            foreach(var total in totals)
                            {
                                var state=customers.Capacity.GetReservations().Single(token=>ReferenceEquals(token.Token,total.Token)).State;
                                if(fifoStarted && total.OwnerId==fifoGuest && state==AnimalCafe.Capacity.ReservationState.Released)
                                    Assert.That(fifoExited || fifoBody!=null && Vector3.Distance(fifoBody.transform.position,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position)<=fifoBody.Settings.ArrivalDistance+.001f,
                                        Is.True,"Total release requires actual exit pose or the recorded actual destruction");
                                else Assert.That(state,Is.EqualTo(AnimalCafe.Capacity.ReservationState.Occupied),"No other customer can disappear during clearance");
                            }
                            Assert.That(pickup.ServiceLeases.Any(lease=>lease.Key==incoming),Is.False);
                            if(!accepted)
                            {
                                var expectedTokens=tokens.Where(token=>token.OwnerId==incoming).ToArray();
                                var actualTokens=customers.Capacity.GetReservations().Where(token=>token.OwnerId==incoming).ToArray();Assert.That(actualTokens.Length,Is.EqualTo(expectedTokens.Length));
                                foreach(var expected in expectedTokens){var retained=actualTokens.Single(token=>ReferenceEquals(token.Token,expected.Token));Assert.That(retained.Kind,Is.EqualTo(expected.Kind));Assert.That(retained.State,Is.EqualTo(expected.State));}
                                var currentAnchor=Read(visit,"PendingReservationAnchor");var parking=Read(visit,"PickupParking");
                                var canonicalSource=parking==null?null:Read(parking,"Source") as TrafficDecision;
                                var currentKey=currentAnchor!=null?(TrafficIntentKey)Read(currentAnchor,"Key"):canonicalSource!=null?canonicalSource.Intent:trackedKey;
                                if(currentAnchor==null && !Flag(visit,"PickupAssigned"))
                                {
                                    Assert.That(Flag(trackedEntry,"Retired")||Flag(trackedEntry,"Discarded"),Is.True,"A source gap requires an actually stopped old proposal");
                                    var oldClaim=trackedAnchor==null?null:Read(trackedAnchor,"CounterClaim");
                                    Assert.That(pickup.Version!=trackedVersion&&Claims()!=trackedClaims || oldClaim!=null&&!Flag(oldClaim,"IsCurrent"),Is.True,
                                        "A source gap needs a real canonical lifecycle receipt");
                                    Assert.That(Read(trackedEntry,"FirstFrame"),Is.EqualTo(trackedFirst));Assert.That(Read(trackedEntry,"RequestSequence"),Is.EqualTo(trackedSequence));
                                    var spent=(int)Read(trackedEntry,"Queries");Assert.That(spent,Is.InRange(trackedQueries,16384));totalObservedQueries+=spent-trackedQueries;trackedQueries=spent;
                                    var oldDebt=(float)Read(trackedEntry,"WaitRemaining");if(trackedWaitStarted)Assert.That(oldDebt,Is.LessThanOrEqualTo(trackedWait));trackedWait=oldDebt;
                                    Assert.That(Read(trackedEntry,"AcquiredSequence"),Is.EqualTo(0L));Assert.That(Read(trackedEntry,"JourneyPermit"),Is.Null);
                                    Assert.That(pickup.TryGetReservation(incoming,out _),Is.False);Assert.That(Read(visit,"PaidPickup"),Is.Null);
                                    Assert.That(owner.Snapshot.PaidVisitIds.Contains(incoming)||owner.Snapshot.Orders.Values.Any(order=>order.CustomerId==incoming),Is.False);
                                    Assert.That((long)Read(visit,"RequestId"),Is.EqualTo(0L));Assert.That(native.Values.Cast<object>().Any(row=>(string)Read(row,"ActorId")==incoming),Is.False);
                                    if(!gapLogged){gapLogged=true;records.Add("GOAL_STOPPED_SOURCE_GAP frame="+Time.frameCount+" old="+Key(trackedKey)+" first="+trackedFirst+" queries="+spent+" wait="+oldDebt+" noPermission=True");}
                                    return; // Await the real CFC descriptor; never manufacture a replacement in the fixture.
                                }
                                if(!currentKey.Equals(trackedKey))
                                {
                                    // Accept only a stopped old proposal and an actual changed canonical producer descriptor.
                                    // 只认证真实canonical变更后的CFC新上下文；不复活旧Terminal或重置旧账本。
                                    Assert.That(entries.Contains(currentKey),Is.True);var nextEntry=entries[currentKey];
                                    var nextRequest=(Read(nextEntry,"Request")??Read(nextEntry,"JourneyRequest")) as TrafficRequest;
                                    var oldProducerClaim=trackedAnchor==null?null:Read(trackedAnchor,"CounterClaim");
                                    var canonicalChanged=pickup.Version!=trackedVersion && Claims()!=trackedClaims;
                                    var counterChanged=oldProducerClaim!=null && !Flag(oldProducerClaim,"IsCurrent");
                                    Assert.That(canonicalChanged||counterChanged,Is.True,"New source needs a real canonical lifecycle receipt, not a budget retry");
                                    Assert.That(Flag(trackedEntry,"Retired")||Flag(trackedEntry,"Discarded"),Is.True);
                                    Assert.That(Read(trackedEntry,"FirstFrame"),Is.EqualTo(trackedFirst));Assert.That(Read(trackedEntry,"RequestSequence"),Is.EqualTo(trackedSequence));
                                    var finalOldQueries=(int)Read(trackedEntry,"Queries");Assert.That(finalOldQueries,Is.InRange(trackedQueries,16384));totalObservedQueries+=finalOldQueries-trackedQueries;
                                    if(trackedWaitStarted)Assert.That((float)Read(trackedEntry,"WaitRemaining"),Is.LessThanOrEqualTo(trackedWait));
                                    Assert.That(Read(trackedEntry,"AcquiredSequence"),Is.EqualTo(0L));Assert.That(Read(trackedEntry,"JourneyPermit"),Is.Null);
                                    Assert.That(nextRequest,Is.Not.Null);Assert.That(nextRequest.RequestedFrame,Is.EqualTo((int)Read(nextEntry,"FirstFrame")));
                                    Assert.That((int)Read(nextEntry,"FirstFrame"),Is.GreaterThanOrEqualTo(trackedFirst));
                                    Assert.That(currentKey.ActorId==incoming && currentKey.IntentId=="pickup-wait" && currentKey.Session==sourceKey.Session && currentKey.Epoch==sourceKey.Epoch && currentKey.LayoutRevision==sourceKey.LayoutRevision,Is.True);
                                    if(currentAnchor!=null)
                                    {Assert.That(Read(currentAnchor,"Coordinator"),Is.SameAs(core));Assert.That(Read(currentAnchor,"Owner"),Is.SameAs(pickup));Assert.That(Read(currentAnchor,"Visit"),Is.SameAs(visit));Assert.That(Read(currentAnchor,"Actor"),Is.SameAs(actorLife));}
                                    records.Add("GOAL_PRODUCER_LIFECYCLE frame="+Time.frameCount+" old="+Key(trackedKey)+" oldFirst="+trackedFirst+
                                        " oldQueries="+Read(trackedEntry,"Queries")+" oldWait="+Read(trackedEntry,"WaitRemaining")+" new="+Key(currentKey)+" actualPqsVersion="+pickup.Version+
                                        " canonicalChanged="+canonicalChanged+" counterClaimChanged="+counterChanged+" descriptorChanged="+(Descriptor(nextRequest)!=trackedDescriptor));
                                    trackedKey=currentKey;trackedEntry=nextEntry;trackedAnchor=currentAnchor;trackedFirst=(int)Read(nextEntry,"FirstFrame");trackedFinite=(float)Read(nextEntry,"FiniteLimit");
                                    trackedSequence=(long)Read(nextEntry,"RequestSequence");trackedQueries=0;trackedWait=0;trackedWaitStarted=false;sourceLives++;
                                    trackedClaims=Claims();trackedDescriptor=Descriptor(nextRequest);trackedVersion=pickup.Version;
                                    gapLogged=false;
                                }
                                Assert.That(entries[trackedKey],Is.SameAs(trackedEntry));Assert.That(Read(trackedEntry,"FirstFrame"),Is.EqualTo(trackedFirst));
                                Assert.That(Read(trackedEntry,"FiniteLimit"),Is.EqualTo(trackedFinite));
                                var seq=(long)Read(trackedEntry,"RequestSequence");if(trackedSequence==0&&seq>0)trackedSequence=seq;
                                Assert.That(seq,Is.EqualTo(trackedSequence));var count=(int)Read(trackedEntry,"Queries");Assert.That(count,Is.InRange(trackedQueries,16384));
                                totalObservedQueries+=count-trackedQueries;trackedQueries=count;Assert.That(core.QueriesThisFrame,Is.LessThanOrEqualTo(128));
                                var debt=(float)Read(trackedEntry,"WaitRemaining");if(trackedWaitStarted)Assert.That(debt,Is.LessThanOrEqualTo(trackedWait));
                                if(debt>0)trackedWaitStarted=true;trackedWait=debt;
                                var decision=core.GetDecision(trackedKey);var signature=Key(trackedKey)+"/"+decision.Kind+"/"+decision.Reason+"/"+count+"/"+oldGoal.Generation+"/"+oldGoal.Position+"/"+arrived;
                                if(signature!=lastPositive)
                                {lastPositive=signature;positiveTransitions++;if(positiveTransitions<=24)records.Add("GOAL_REPROOF frame="+Time.frameCount+" key="+Key(trackedKey)+" kind="+decision.Kind+" reason="+decision.Reason+" queries="+count+" wait="+debt+" oldGoal="+oldGoal.Position.ToString("F6")+" oldGeneration="+oldGoal.Generation+" earlierArrived="+arrived+" fifo="+fifoStarted);else positiveOverflow=true;}
                                if(!Flag(visit,"PickupAssigned"))
                                {
                                    Assert.That(Read(visit,"PendingReservationAnchor"),Is.SameAs(trackedAnchor));
                                    Assert.That(pickup.TryGetReservation(incoming,out _),Is.False);Assert.That(Read(trackedEntry,"AcquiredSequence"),Is.EqualTo(0L));
                                    Assert.That(Read(visit,"PaidPickup"),Is.Null);Assert.That(owner.Snapshot.PaidVisitIds.Contains(incoming),Is.False);
                                    Assert.That(owner.Snapshot.Orders.Values.Any(order=>order.CustomerId==incoming),Is.False);Assert.That((long)Read(visit,"RequestId"),Is.EqualTo(0L));
                                    if(!fifoStarted && arrived && Flag(trackedEntry,"UnpaidEligibilityHeld") &&
                                        latestFailureRole.TryGetValue(trackedKey,out var failedRole) && failedRole=="existing-goal/"+earlier+"/exit-capability")
                                    {
                                        if(!goalStillProtected)records.Add("GOAL_ARRIVED_EXIT_STILL_PROTECTED frame="+Time.frameCount+" key="+Key(trackedKey)+" queries="+count+" noAcquire=True");
                                        goalStillProtected=true;Time.captureDeltaTime=0;Time.timeScale=0;
                                    }
                                }
                                else
                                {
                                    Assert.That(pickup.TryGetReservation(incoming,out var actual),Is.True);
                                    var usedPreview=trackedAnchor==null?null:Read(trackedAnchor,"Preview") as PickupQueueService.PreparedMutation;
                                    if(usedPreview!=null)Assert.That(actual,Is.EqualTo(usedPreview.Assignment));
                                    var permit=Read(trackedEntry,"JourneyPermit") as TrafficDecision;
                                    Assert.That(permit,Is.Not.Null);Assert.That(permit.Intent,Is.EqualTo(trackedKey));Assert.That(permit.AcquiredSequence,Is.GreaterThan(0));
                                    Assert.That(Read(parking,"Source"),Is.SameAs(permit));Assert.That(Read(parking,"Coordinator"),Is.SameAs(core));
                                    Assert.That(Read(parking,"Owner"),Is.SameAs(pickup));Assert.That(Read(parking,"Actor"),Is.SameAs(actorLife));
                                    var actualClaim=Read(parking,"Claim");Assert.That(actualClaim,Is.Not.Null);Assert.That(Read(actualClaim,"Assignment"),Is.EqualTo(actual));
                                    Assert.That(Read(visit,"PendingReservationAnchor"),Is.Null);Assert.That(Read(visit,"PaidPickup"),Is.Null);
                                    Assert.That(owner.Snapshot.PaidVisitIds.Contains(incoming),Is.False);Assert.That((long)Read(visit,"RequestId"),Is.EqualTo(0L));
                                    Assert.That(native.Values.Cast<object>().Any(row=>(string)Read(row,"ActorId")==incoming),Is.False);
                                    var currentRequest=(TrafficRequest)Read(trackedEntry,"JourneyRequest");var primary=permit.ProofRoutes.Single(proof=>proof.ResourceId=="pickup/"+incoming);
                                    Assert.That(currentRequest.Target,Is.EqualTo(actual.Position));Assert.That(primary.Waypoints.Last(),Is.EqualTo(actual.Position));Assert.That(primary.ExpandedPath.Count,Is.GreaterThan(0));
                                    var roleId="existing-goal/"+earlier+"/exit-capability";var role=currentRequest.Requirements.Single(requirement=>requirement.ResourceId==roleId);
                                    var goalProof=permit.ProofRoutes.Single(proof=>proof.ResourceId==roleId);
                                    Assert.That(role.Start,Is.EqualTo(oldGoal.Position));Assert.That(goalProof.Waypoints.Last(),Is.EqualTo(role.Goal));Assert.That(goalProof.ExpandedPath.Count,Is.GreaterThan(0));
                                    var oldBody=(NavigationActor)Read(visits[earlier],"Actor");var margin=currentRequest.Radius+oldBody.Settings.AgentRadius+Math.Max(currentRequest.SafetyMargin,oldBody.Settings.CollisionSkin+oldBody.Settings.Epsilon);
                                    Assert.That((bool)near.Invoke(null,new object[]{oldBody.transform.position,new[]{actual.Position},margin}),Is.False);
                                    accepted=true;records.Add("GOAL_CANONICAL_ACCEPT frame="+Time.frameCount+" key="+Key(trackedKey)+" first="+trackedFirst+" sequence="+trackedSequence+
                                        " queries="+count+" actualTarget="+actual.Position.ToString("F6")+" initialTarget="+assignment.Position.ToString("F6")+
                                        " sourceLives="+sourceLives+" totalObservedQueries="+totalObservedQueries+" earlierArrived="+arrived+" fifo="+fifoStarted+
                                        " currentCanonicalGoalExitProof=True reservationObjectRetained=True traceOverflow="+positiveOverflow);
                                }
                            }
                            if(accepted && arrived && (!fifoStarted || fifoExited))
                            {
                                var oldBody=(NavigationActor)Read(visits[earlier],"Actor");
                                Assert.That(Vector3.Distance(oldBody.transform.position,oldGoal.Position),Is.LessThanOrEqualTo(oldBody.Settings.ArrivalDistance+.001f));
                                positiveComplete=true;Time.captureDeltaTime=0;Time.timeScale=0;
                            }
                        }
                        catch(Exception error){if(errors.Count<4)errors.Add(error.ToString());}
                    };
                    owner.SnapshotChanged+=acceptanceObserver;Time.captureDeltaTime=savedCapture;Time.timeScale=savedScale;
                    while(positiveRemaining>0 && !positiveComplete && !goalStillProtected && errors.Count==0){positiveRemaining--;yield return null;}
                    Assert.That(errors,Is.Empty,string.Join("\n",errors));
                    {
                        Assert.That(goalStillProtected,Is.True,"PremiseNotProven: this four-corner fixture must expose the actual arrived-goal exit-capability refusal before FIFO clearance");
                        var fifoHead=pickup.Snapshot.First();fifoGuest=fifoHead.VisitId;
                        Assert.That(fifoGuest!=earlier && fifoGuest!=incoming && fifoHead.Arrived,Is.True,"Only a real older FIFO head may clear first");
                        Assert.That(owner.Snapshot.Orders.Values.Single(order=>order.CustomerId==fifoGuest).State,Is.EqualTo(OrderState.ReadyForPickup));
                        fifoBody=world.RegisteredActors.Single(body=>body.ActorId==fifoGuest);var fifoTotal=totals.Single(total=>total.OwnerId==fifoGuest).Token;
                        fifoExitObserver=fifoBody.gameObject.AddComponent<CafeActualExitObserver>();fifoExitObserver.VisitId=fifoGuest;trackFifo=true;
                        fifoExitObserver.Receipt=(leaving,pose)=>
                        {
                            if(!trackFifo)return;
                            try
                            {
                                Assert.That(leaving,Is.EqualTo(fifoGuest));Assert.That(fifoExited,Is.False);
                                Assert.That(Vector3.Distance(pose,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position),Is.LessThanOrEqualTo(.081f));
                                Assert.That(owner.Snapshot.Orders.Values.Single(order=>order.CustomerId==leaving).State,Is.EqualTo(OrderState.Completed));
                                Assert.That(customers.Capacity.GetReservations().Single(total=>ReferenceEquals(total.Token,fifoTotal)).State,Is.EqualTo(AnimalCafe.Capacity.ReservationState.Released));
                                fifoExited=true;records.Add("GOAL_FIFO_ACTUAL_EXIT frame="+Time.frameCount+" visit="+leaving+" originalTotalReleased=True");
                            }
                            catch(Exception error){if(errors.Count<4)errors.Add(error.ToString());}
                        };
                        positiveComplete=false;fifoStarted=true;tools.ResumeCollection();Time.captureDeltaTime=savedCapture;Time.timeScale=savedScale;
                        while(positiveRemaining>0 && !positiveComplete && errors.Count==0){positiveRemaining--;yield return null;}
                        Assert.That(errors,Is.Empty,string.Join("\n",errors));
                        Assert.That(fifoCollected&&fifoExited,Is.True,"Only real FIFO collection and actual exit can justify capacity release");
                    }
                    records.Add("GOAL_POSITIVE_FRAME_BUDGET remaining="+positiveRemaining+" used="+(6000-positiveRemaining));
                    Assert.That(accepted&&arrived&&positiveComplete,Is.True,"Real clearance must yield current canonical proof without bypassing any old-goal exit capability");
                    Assert.That(pickup.TryGetReservation(incoming,out _),Is.True);
                    Assert.That(pickup.TryGetReservation(earlier,out var oldFinal)&&oldFinal.Arrived,Is.True);
                    Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations")[earlier],Is.SameAs(earlierReservation));
                    Assert.That(customers.Capacity.GetCapacities().Single(capacity=>capacity.Kind==AnimalCafe.Capacity.CapacityKind.TotalCustomers).Used,Is.EqualTo(3));
                    Assert.That(world.RegisteredActors.Any(body=>ReferenceEquals(body,actorLife)),Is.True);
                }
            }
            finally
            {
                trackFifo=false;if(fifoExitObserver!=null)fifoExitObserver.Receipt=null;
                active=false;if(owner!=null){owner.SnapshotChanged-=Observe;if(acceptanceObserver!=null)owner.SnapshotChanged-=acceptanceObserver;if(contractObserver!=null)owner.SnapshotChanged-=contractObserver;}
                if(diagnostic!=null && core!=null && ReferenceEquals(diagnostic.GetValue(core),observeProof))diagnostic.SetValue(core,previousDiagnostic);
                if(owner!=null)CafeLoopRuntimeProof.Set(owner,"stepping",false);
                Time.captureDeltaTime=savedCapture;Time.timeScale=savedScale;
                TestContext.WriteLine("GOAL_REAL_PRODUCER_SUMMARY window="+window+" zeroTime="+zero+" witnessed="+witnessed+" transitions="+transitions+" overflow="+overflow+
                    " completeCase="+completeCase+" canonicalAccepted="+accepted+" earlierActuallyArrived="+arrived+" positiveComplete="+positiveComplete+
                    " arrivedExitStillProtected="+goalStillProtected+" fifoStarted="+fifoStarted+" fifoCollected="+fifoCollected+" fifoActualExit="+fifoExited+
                    " positiveFramesUsed="+(6000-positiveRemaining)+" positiveFramesRemaining="+positiveRemaining);
                foreach(var record in records)TestContext.WriteLine(record);foreach(var error in errors)TestContext.WriteLine("GOAL_WITNESS_ERROR "+error);
            }
        }

        [UnityTest] public IEnumerator RealReservedPickupGoalRejectsSealingNewParkingTargetUntilRelease()
            => ObserveRealReservedPickupGoal(true);

        private bool CanonicalParkingTargetClear(string incoming,Vector3 target)
        {
            var visits=CafeLoopRuntimeProof.Field<System.Collections.IDictionary>(customers,"visits");
            return (bool)typeof(CustomerFlowController).GetMethod("NewPickupTargetClear",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(customers,new object[]{visits[incoming],target});
        }
        // Legacy compatibility only: one synchronous read-only probe, never Main typed authority.
        // 仅测旧比较合同；屏蔽Core期间不yield、不推进业务、不发布Native移动，finally恢复原引用。
        private bool LegacyCanonicalParkingTargetClear(string incoming,Vector3 target)
        {
            Assert.That(CafeLoopRuntimeProof.Field<bool>(owner,"stepping"),Is.True);
            Assert.That(CafeLoopRuntimeProof.Field<object>(customers,"CorridorObserver"),Is.Null);
            var originalCore=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic");
            Assert.That(originalCore,Is.Not.Null);
            Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic"),Is.SameAs(originalCore));
            var native=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byId");
            var requests=native.Cast<DictionaryEntry>().ToArray();
            var nextRequest=CafeLoopRuntimeProof.Field<long>(world.Service,"nextId");var frame=Time.frameCount;
            try
            {
                CafeLoopRuntimeProof.Set(customers,"traffic",null);
                return CanonicalParkingTargetClear(incoming,target);
            }
            finally
            {
                CafeLoopRuntimeProof.Set(customers,"traffic",originalCore);
                Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic"),Is.SameAs(originalCore));
                Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic"),Is.SameAs(originalCore));
                Assert.That(CafeLoopRuntimeProof.Field<bool>(owner,"stepping"),Is.True);Assert.That(Time.frameCount,Is.EqualTo(frame));
                Assert.That(CafeLoopRuntimeProof.Field<long>(world.Service,"nextId"),Is.EqualTo(nextRequest),"Legacy probe cannot mint a Native movement request");
                Assert.That(native.Count,Is.EqualTo(requests.Length));
                foreach(var request in requests)Assert.That(native[request.Key],Is.SameAs(request.Value));
                TestContext.WriteLine("LEGACY_CANONICAL_PROBE compatibilityOnly=True sameCoreRestored=True noNewNativeMovement=True sameFrame=True");
            }
        }
        [UnityTest] public IEnumerator CanonicalPickupGoalProtectionRetiresAfterRealArrival()
            => ObserveRealReservedPickupGoal(false,"arrival");
        [UnityTest] public IEnumerator LegacyCanonicalPickupGoalBudgetUnknownCreatesNoCommitAndRealOwnerRemovalClearsIt()
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
                Assert.That(LegacyCanonicalParkingTargetClear(incoming,target),Is.False);
                Assert.That(CafeLoopRuntimeProof.Field<string>(customers,"pickupGoalProofStatus"),Is.EqualTo("unknown-budget"));
                Assert.That(LegacyCanonicalParkingTargetClear(incoming,target),Is.False);
                Assert.That(CafeLoopRuntimeProof.Field<int>(customers,"pickupGoalQueries"),Is.EqualTo(4000));
                Assert.That(pickup.TryGetReservation(incoming,out _),Is.False);
                CollectionAssert.AreEqual(tokens,customers.Capacity.GetReservations().Select(r=>r.Token));
                CollectionAssert.AreEqual(paid,owner.Snapshot.PaidVisitIds);
                Assert.That(pickup.Remove(earlier),Is.True);
                Assert.That(LegacyCanonicalParkingTargetClear(incoming,target),Is.True,
                    "The real owner wins over a stale Visit PickupAssigned flag and UNKNOWN cache");
                Assert.That(pickup.TryGetReservation(earlier,out _),Is.False,"Readonly validation cannot recreate removed ownership");
                customers.ReleasePickupAssignment(earlier);
            }
            finally {if(owner!=null)CafeLoopRuntimeProof.Set(owner,"stepping",false);}
        }
        [UnityTest] public IEnumerator LegacyCanonicalPickupGoalWithoutOriginalPathProofDoesNotKeepPriority()
        {
            string earlier=null,incoming=null;PickupQueueService pickup=null;
            try
            {
                yield return PrepareRealCanonicalParkingGoal((a,b,q)=>{earlier=a;incoming=b;pickup=q;});
                Assert.That(LegacyCanonicalParkingTargetClear(incoming,new Vector3(-3.6f,0,-1.6f)),Is.False);
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
                Assert.That(LegacyCanonicalParkingTargetClear(incoming,new Vector3(-3.6f,0,-.5f)),Is.True,
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

        // 原当前返程 WAIT 前提尚未复现；仅显式运行，未通过不代表负例已覆盖。
        // Opt-in reproduction only: current Returning WAIT remains NOT_PROVEN; all original assertions remain.
        [Explicit("NOT_PROVEN: requires an actual current Returning-leg WAIT before failure; opt-in reproduction, no equivalent Native coverage claimed.")]
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
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            object Read(object value,string name)=>value==null?null:value.GetType().GetField(name,flags)?.GetValue(value)??value?.GetType().GetProperty(name,flags)?.GetValue(value);
            object Call(string name,params object[] args)=>typeof(CustomerFlowController).GetMethod(name,flags).Invoke(customers,args);
            NavigationActor obstruction=null;var savedScale=Time.timeScale;var savedCapture=Time.captureDeltaTime;
            try
            {
                yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000,requireCurrentSnapshot:true);
                Assert.That(customers.TrySpawn(),Is.True,customers.Status);var id=customers.Snapshot.Single().VisitId;
                yield return Until(()=>customers.CafeSnapshot.Single().CounterArrived);CafeLoopRuntimeProof.Set(owner,"stepping",true);
                yield return PreparePositiveUnpaidReservation(id);Assert.That(customers.TryReservePickup(id,out var reason),Is.True,reason);
                var visits=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits");var visit=visits[id];var actor=(NavigationActor)Read(visit,"Actor");
                var pickup=(PickupQueueService)Read(visit,"PickupOwner");var rows=CafeLoopRuntimeProof.Field<IDictionary>(pickup,"reservations");var live=rows[id];
                var target=(Vector3)Read(live,"Target");var generation=Read(live,"Generation");var parking=Read(visit,"PickupParking");var source=(TrafficDecision)Read(parking,"Source");
                var historical=Read(visit,"ReservedPickupPath") as IReadOnlyList<Vector3>;Assert.That(historical,Is.Not.Null);
                var core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic");var entries=CafeLoopRuntimeProof.Field<IDictionary>(core,"entries");
                var native=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byId");var nativeIds=native.Keys.Cast<long>().ToArray();
                var bodies=world.RegisteredActors.Select(a=>(Actor:a,Pose:a.transform.position,Rotation:a.transform.rotation)).ToArray();
                var tokens=customers.Capacity.GetReservations().ToArray();var counter=customers.CounterAssignments.ToArray();
                var phase=Read(visit,"TrafficPhaseSequence");var admission=Read(visit,"Admission");var queries=core.QueriesConsumed(source.Intent);
                Assert.That(owner.Snapshot.PaidVisitIds,Is.Empty);Assert.That(owner.Snapshot.Orders,Is.Empty);Assert.That((long)Read(visit,"RequestId"),Is.Zero);
                void UnpaidPhysicalUnchanged()
                {
                    Assert.That(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic"),Is.SameAs(core));
                    Assert.That(visits[id],Is.SameAs(visit));Assert.That(Read(visit,"Admission"),Is.SameAs(admission));
                    foreach(var body in bodies){Assert.That(body.Actor.transform.position,Is.EqualTo(body.Pose));Assert.That(body.Actor.transform.rotation,Is.EqualTo(body.Rotation));}
                    Assert.That(world.RegisteredActors.Count(),Is.EqualTo(bodies.Length+(obstruction==null?0:1)));
                    CollectionAssert.AreEquivalent(nativeIds,native.Keys.Cast<long>());CollectionAssert.AreEqual(counter,customers.CounterAssignments);
                    Assert.That(owner.Snapshot.PaidVisitIds,Is.Empty);Assert.That(owner.Snapshot.Orders,Is.Empty);Assert.That(Read(visit,"PaidPickup"),Is.Null);
                    Assert.That((long)Read(visit,"RequestId"),Is.Zero);var current=customers.Capacity.GetReservations().ToArray();Assert.That(current.Length,Is.EqualTo(tokens.Length));
                    foreach(var token in tokens){var row=current.Single(a=>ReferenceEquals(a.Token,token.Token));Assert.That(row.Kind,Is.EqualTo(token.Kind));Assert.That(row.State,Is.EqualTo(token.State));Assert.That(row.OwnerId,Is.EqualTo(token.OwnerId));}
                }
                // The original virtual goal remains unchanged; initialize one NEW body once toward the actual Counter actor.
                // 只确定新blocker的初始姿态；不改Preview/Source目标，不重采样候选或移动既有actor。
                var originalProposal=Read(entries[source.Intent],"JourneyRequest") as TrafficRequest;Assert.That(originalProposal,Is.Not.Null);
                Assert.That(originalProposal.Target,Is.EqualTo(target));Assert.That(adapter.IsPointWalkable(target),Is.True);
                var blockerPrefab=CafeLoopRuntimeProof.Field<NavigationActor>(tools,"blockerPrefab");
                var placementStep=actor.Settings.TargetSampleDistance;var blockerPose=Vector3.MoveTowards(target,actor.transform.position,placementStep);
                var originalPairClearance=originalProposal.Radius+blockerPrefab.Settings.AgentRadius+
                    Mathf.Max(originalProposal.SafetyMargin,blockerPrefab.Settings.CollisionSkin+blockerPrefab.Settings.Epsilon);
                Assert.That(Vector3.Distance(blockerPose,target),Is.GreaterThan(0));Assert.That(Vector3.Distance(blockerPose,target),Is.LessThan(originalPairClearance));
                Assert.That(adapter.IsPointWalkable(blockerPose),Is.True,"The one fixed inward body start must be on the original owned floor");
                Assert.That(world.RegisteredActors.All(body=>Vector3.Distance(body.transform.position,blockerPose)>.92f),Is.True,
                    "Keep the original blocker helper's existing-body separation at this new body's fixed start");
                obstruction=UnityEngine.Object.Instantiate(blockerPrefab,blockerPose,Quaternion.identity,tools.transform);
                var initialized=obstruction.TryInitializeRuntimeId("reserved-target-revalidation-obstruction");
                TestContext.WriteLine("UNTRANSFERRED_BLOCKER_INIT result="+initialized);Assert.That(initialized,Is.True,"One original Init only; failure stops the fixture");
                var enabled=obstruction.TryEnableSteadyPathMotion();
                TestContext.WriteLine("UNTRANSFERRED_BLOCKER_ENABLE result="+enabled);Assert.That(enabled,Is.True,"One original Enable only; failure stops the fixture");
                var registered=world.Register(obstruction);
                TestContext.WriteLine("UNTRANSFERRED_BLOCKER_REGISTER result="+registered+" target="+target.ToString("F9")+" pose="+obstruction.transform.position.ToString("F9")+
                    " placementStep="+placementStep.ToString("R")+" pairClearance="+originalPairClearance.ToString("R")+" sourceAcquired="+source.AcquiredSequence+" extraPathQueries=0");
                Assert.That(registered,Is.True,"One original Register at the fixed inward start; no retry or alternative placement");
                Assert.That(world.RegisteredActors.Any(body=>ReferenceEquals(body,obstruction)),Is.True);
                Assert.That(Read(obstruction,"<World>k__BackingField"),Is.SameAs(world));Assert.That(obstruction.ActorId,Is.EqualTo("reserved-target-revalidation-obstruction"));
                Assert.That(obstruction.Agent,Is.Not.Null);Assert.That(obstruction.Agent.enabled&&obstruction.Agent.isOnNavMesh,Is.True);
                Assert.That(obstruction.transform.position,Is.EqualTo(blockerPose));Assert.That(Vector3.Distance(obstruction.transform.position,target),Is.LessThan(originalPairClearance));
                Assert.That(Read(live,"Target"),Is.EqualTo(target));Assert.That(Read(parking,"Source"),Is.SameAs(source));Assert.That(originalProposal.Target,Is.EqualTo(target));
                var clearance=actor.Settings.AgentRadius+obstruction.Settings.AgentRadius+
                    Mathf.Max(actor.Settings.CollisionSkin+actor.Settings.Epsilon,obstruction.Settings.CollisionSkin+obstruction.Settings.Epsilon);
                Assert.That(clearance,Is.EqualTo(originalPairClearance));
                Assert.That(CustomerRoutePlanner.IsClear(historical,new[]{obstruction.transform.position},clearance),Is.False,
                    "The real body obstructs historical geometry; existing unpaid identity is not a newly issued movement proof");
                Assert.That(customers.TryReservePickup(id,out reason),Is.True,reason);UnpaidPhysicalUnchanged();
                var registeredFact=((TrafficWorldSnapshot)Read(core,"snapshot")).Bodies.Single(body=>ReferenceEquals(body.ActorLife,obstruction));
                Assert.That(registeredFact.Owner.ActorId,Is.EqualTo(obstruction.ActorId));Assert.That(registeredFact.Position,Is.EqualTo(blockerPose));
                Assert.That(registeredFact.Enabled&&registeredFact.ActorActive,Is.True);
                Assert.That(rows[id],Is.SameAs(live));Assert.That(Read(live,"Generation"),Is.EqualTo(generation));Assert.That(Read(live,"Paid"),Is.EqualTo(false));
                Assert.That(Read(visit,"ReservedPickupPath"),Is.SameAs(historical));Assert.That(Read(visit,"PickupParking"),Is.SameAs(parking));
                Assert.That(Read(parking,"Source"),Is.SameAs(source));Assert.That(core.QueriesConsumed(source.Intent),Is.EqualTo(queries));
                CollectionAssert.Contains((Vector3[])Call("RouteObstacles","unrelated-route-probe",null,false),target);
                customers.ReleasePickupAssignment(id);UnpaidPhysicalUnchanged();
                Assert.That(rows.Contains(id),Is.False);Assert.That(Read(visit,"PickupAssigned"),Is.EqualTo(false));Assert.That(Read(visit,"ReservedPickupPath"),Is.Null);
                Assert.That(Read(visit,"TrafficPhaseSequence"),Is.Not.EqualTo(phase));Assert.That(core.HasActiveGrant(source.Intent),Is.False);
                Assert.That(Read(entries[source.Intent],"Retired"),Is.EqualTo(true),"The public unused release really retires the old canonical source");
                var remaining=6000;Time.timeScale=0;Time.captureDeltaTime=0;
                for(var transition=0;transition<3&&Time.deltaTime!=0;transition++)
                {Assert.That(remaining--,Is.GreaterThan(0));UnpaidPhysicalUnchanged();yield return null;}
                Assert.That(Time.deltaTime,Is.Zero);UnpaidPhysicalUnchanged();
                Assert.That(customers.TryReservePickup(id,out reason),Is.False,"The new actual producer must not acquire through the real body: "+reason);
                var anchor=Read(visit,"PendingReservationAnchor");Assert.That(anchor,Is.Not.Null);var key=(TrafficIntentKey)Read(anchor,"Key");
                Assert.That(key.Equals(source.Intent),Is.False);var entry=entries[key];var first=Read(entry,"FirstFrame");var sequence=0L;var spent=0;
                // Capture at this actual released-source prime, before any held/clear helper can recapture metadata.
                // 原prime就固定身份；Preview对象可刷新/暂stale，但assignment与source绑定不得被替换。
                var heldCounterClaim=Read(anchor,"CounterClaim");var heldEligibility=Read(anchor,"Eligibility");var heldActor=Read(anchor,"Actor");
                var heldFinite=Read(entry,"FiniteLimit");var heldPreview=Read(anchor,"Preview") as PickupQueueService.PreparedMutation;
                Assert.That(heldCounterClaim,Is.Not.Null);Assert.That(heldEligibility,Is.Not.Null);Assert.That(heldActor,Is.SameAs(actor));Assert.That(heldPreview,Is.Not.Null);
                var heldAssignment=heldPreview.Assignment;Assert.That(heldAssignment.Position,Is.EqualTo(target));
                var heldCounterWasCurrent=(bool)Read(heldCounterClaim,"IsCurrent");
                void SameReleasedPreparation()
                {
                    Assert.That(Read(visit,"PendingReservationAnchor"),Is.SameAs(anchor));Assert.That(entries[key],Is.SameAs(entry));Assert.That(Read(anchor,"Key"),Is.EqualTo(key));
                    Assert.That(Read(anchor,"CounterClaim"),Is.SameAs(heldCounterClaim));if(heldCounterWasCurrent)Assert.That(Read(heldCounterClaim,"IsCurrent"),Is.EqualTo(true));
                    Assert.That(Read(anchor,"Eligibility"),Is.SameAs(heldEligibility));Assert.That(Read(entry,"UnpaidEligibility"),Is.SameAs(heldEligibility));
                    Assert.That(Read(anchor,"Actor"),Is.SameAs(heldActor));Assert.That(Read(visit,"Actor"),Is.SameAs(heldActor));Assert.That(Read(entry,"FiniteLimit"),Is.EqualTo(heldFinite));
                    var previewNow=Read(anchor,"Preview") as PickupQueueService.PreparedMutation;Assert.That(previewNow,Is.Not.Null);Assert.That(previewNow.Assignment,Is.EqualTo(heldAssignment));
                    var requestNow=Read(entry,"Request") as TrafficRequest;Assert.That(requestNow,Is.Not.Null);Assert.That(requestNow.Target,Is.EqualTo(heldAssignment.Position));
                }
                CafeLoopRuntimeProof.Set(owner,"stepping",false);var held=false;
                while(remaining-->0&&!held)
                {
                    Assert.That(Time.deltaTime,Is.Zero);SameReleasedPreparation();UnpaidPhysicalUnchanged();Assert.That(Read(visit,"PendingReservationAnchor"),Is.SameAs(anchor));Assert.That(entries[key],Is.SameAs(entry));
                    Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(first));var q=(int)Read(entry,"Queries");Assert.That(q,Is.InRange(spent,16384));spent=q;
                    var seq=(long)Read(entry,"RequestSequence");if(sequence==0&&seq>0)sequence=seq;Assert.That(seq,Is.EqualTo(sequence));
                    Assert.That(Read(entry,"AcquiredSequence"),Is.EqualTo(0L));Assert.That(Read(entry,"JourneyPermit"),Is.Null);Assert.That(Read(entry,"Leg"),Is.Null);
                    Assert.That(core.HasActiveGrant(key),Is.False);Assert.That(pickup.TryGetReservation(id,out _),Is.False);Assert.That(core.QueriesThisFrame,Is.LessThanOrEqualTo(128));
                    var request=(TrafficRequest)Read(entry,"Request");Assert.That(request.Target,Is.EqualTo(target));var decision=core.GetDecision(key);
                    held=(bool)Read(entry,"UnpaidEligibilityHeld") && decision.Reason=="unpaid-eligibility/endpoint-occupied" &&
                        decision.Blockers.Any(b=>b.Owner.ActorId==obstruction.ActorId&&b.ResourceId=="body"&&b.Proof=="live-body-checkpoint");
                    if(held)break;Assert.That(decision.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None),decision.Reason);yield return null;
                }
                Assert.That(held,Is.True,"PremiseNotProven: actual new source never recorded the original body's spatial endpoint refusal");SameReleasedPreparation();
                CafeLoopRuntimeProof.Set(owner,"stepping",true);var debt=Read(entry,"WaitRemaining");
                Assert.That(customers.TryReservePickup(id,out reason),Is.False,reason);UnpaidPhysicalUnchanged();
                Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(first));Assert.That(Read(entry,"RequestSequence"),Is.EqualTo(sequence));Assert.That(Read(entry,"Queries"),Is.EqualTo(spent));
                Assert.That(Read(entry,"WaitRemaining"),Is.EqualTo(debt));Assert.That(Read(entry,"AcquiredSequence"),Is.EqualTo(0L));
                SameReleasedPreparation();world.Unregister(obstruction);UnityEngine.Object.DestroyImmediate(obstruction.gameObject);obstruction=null;SameReleasedPreparation();UnpaidPhysicalUnchanged();
                CollectionAssert.DoesNotContain((Vector3[])Call("RouteObstacles","unrelated-route-probe",null,false),target,"The removed body and released old canonical row cannot remain phantom obstacles");
                TrafficDecision prepared=null;Action verify=null;var preparation=PreparePositiveUnpaidReservation(id,(decision,check)=>{prepared=decision;verify=check;});
                try
                {
                    while(true)
                    {
                        SameReleasedPreparation();var advanced=preparation.MoveNext();SameReleasedPreparation();if(!advanced)break;
                        Assert.That(remaining--,Is.GreaterThan(0),"Original shared 6000-frame blocked+clear preparation window exhausted");
                        Assert.That(preparation.Current,Is.Null,"Do not ignore a nested/custom yield");yield return null;SameReleasedPreparation();
                    }
                }
                finally{(preparation as IDisposable)?.Dispose();}
                SameReleasedPreparation();Assert.That(((PickupQueueService.PreparedMutation)Read(anchor,"Preview")).IsCurrent,Is.True);
                Assert.That(prepared,Is.Not.Null);Assert.That(prepared.Intent,Is.EqualTo(key));Assert.That(Read(visit,"PendingReservationAnchor"),Is.SameAs(anchor));
                Assert.That(entries[key],Is.SameAs(entry));Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(first));Assert.That(Read(entry,"RequestSequence"),Is.EqualTo(sequence));
                Assert.That((int)Read(entry,"Queries"),Is.InRange(spent,16384));Assert.That((float)Read(entry,"WaitRemaining"),Is.LessThanOrEqualTo((float)debt));
                Assert.That(verify,Is.Not.Null);verify();Assert.That(customers.TryReservePickup(id,out reason),Is.True,reason);UnpaidPhysicalUnchanged();
                Assert.That(rows[id],Is.Not.SameAs(live),"A released canonical object cannot resurrect");Assert.That(pickup.TryGetReservation(id,out var currentGoal),Is.True);
                Assert.That(currentGoal.Position,Is.EqualTo(target));Assert.That(currentGoal.Generation,Is.Not.EqualTo(generation));Assert.That(Read(rows[id],"Paid"),Is.EqualTo(false));
                Assert.That(Read(Read(visit,"PickupParking"),"Source"),Is.SameAs(prepared));Assert.That(prepared.AcquiredSequence,Is.GreaterThan(0));
                CollectionAssert.Contains((Vector3[])Call("RouteObstacles","unrelated-route-probe",null,false),target);
                CollectionAssert.DoesNotContain((Vector3[])Call("RouteObstacles",id,null,false),target,"Own canonical access does not erase unrelated bodies");
                customers.ReleasePickupAssignment(id);UnpaidPhysicalUnchanged();Assert.That(rows.Contains(id),Is.False);
                CollectionAssert.DoesNotContain((Vector3[])Call("RouteObstacles","unrelated-route-probe",null,false),target);
                Assert.That(customers.CafeSnapshot.Single().ExitArrived,Is.False,"Unused release is not an actual exit");
                TestContext.WriteLine("UNTRANSFERRED_ACTUAL_PRODUCER old="+source.Intent+" new="+key+" actualHeld=True sameSourceAfterClear=True newCanonicalObject=True framesUsed="+(6000-remaining));
            }
            finally
            {
                if(obstruction!=null){world.Unregister(obstruction);UnityEngine.Object.DestroyImmediate(obstruction.gameObject);}
                if(owner!=null)CafeLoopRuntimeProof.Set(owner,"stepping",false);Time.timeScale=savedScale;Time.captureDeltaTime=savedCapture;
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

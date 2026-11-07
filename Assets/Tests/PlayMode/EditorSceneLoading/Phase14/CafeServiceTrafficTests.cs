#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode.Phase14
{
    public sealed partial class CafePickupSideTests
    {
        private Action collectionReleaseWitnessCleanup;
        [TearDown] public void CleanupCollectionReleaseWitness()=>collectionReleaseWitnessCleanup?.Invoke();
        [UnityTest] public IEnumerator TwoStaticSidesCaptureOriginalFiniteCollectionReleasePrerequisite()
            => ObserveFiniteCollectionRelease(stopReturnOwner:false);
        [UnityTest] public IEnumerator CollectionReleasePreservesOriginalPaidVisitDuringActorAvailabilityHold()
            => ObserveFiniteCollectionRelease(stopReturnOwner:true);
        [UnityTest] public IEnumerator CollectionReleaseStopsOriginalPaidVisitWhenActualReturnAgentFails()
            => ObserveFiniteCollectionRelease(stopReturnOwner:true,faultAgent:true);
        [UnityTest] public IEnumerator UnrelatedCounterJoinDoesNotNotifyUnchangedOriginalParkingWait()
            => ObserveFiniteCollectionRelease(stopReturnOwner:false,semanticWitness:true,unrelatedCounterDelta:true);
        [UnityTest] public IEnumerator ActualOriginalLeaseReleaseNotifiesAndWakesTheSameParkingWait()
            => ObserveFiniteCollectionRelease(stopReturnOwner:false,semanticWitness:true);
        private IEnumerator ObserveFiniteCollectionRelease(bool stopReturnOwner,bool faultAgent=false,bool semanticWitness=false,bool unrelatedCounterDelta=false)
        {
            object Read(object value,string name)=>value==null?null:MovementRead(value,name);
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var errors=new List<string>();var records=new List<string>();var finished=false;
            yield return AddCounters(new GridPosition(6,7),new GridPosition(7,6));var decor=Find<DecorationModeController>();decor.ExitDecorationMode();
            yield return Until(()=>owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            var pickup=Find<CafeLayoutRuntime>().FunctionalSurfaceLayout.PickUpPoints.Single();var sides=customers.GetPickupServiceTargets(pickup.InstanceId);
            Assert.That(sides.Count,Is.EqualTo(2));var south=sides.Single(t=>t.Facing.HasValue && Vector3.Dot(t.Facing.Value,Vector3.forward)>.9f);
            var block=Blocker(south.Position,"two-side-south-blocker");yield return null;yield return Until(()=>customers.Snapshot.Count>0 || customers.TrySpawn());
            var id=customers.Snapshot.Single().VisitId;
            Action verifyPreheatUnborn=null,verifyPreheatBirth=null;Func<string> preheatFacts=null;
            if(unrelatedCounterDelta)
            {
                // Use the original Ready wait only: submit one real unborn opportunity early, then let the normal driver prepare it.
                var preheatCore=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");var preheatEntries=(IDictionary)Read(preheatCore,"entries");
                object anchor=null,sourceEntry=null;TrafficIntentKey anchorKey=default;var first=-1;var submitted=false;var phaseSeen=false;var firstSequence=0L;
                string Key(TrafficIntentKey k)=>k.Session+"/e"+k.Epoch+"/"+k.ActorId+"/"+k.IntentId+"/g"+k.Generation+"/r"+k.LayoutRevision+"/"+k.Context;
                string Facts()=>anchor==null?"anchor=NONE":("key="+Key(anchorKey)+" first="+first+" seq="+(sourceEntry==null?"NOT_SUBMITTED":Read(sourceEntry,"RequestSequence")?.ToString())+
                    " queries="+Read(sourceEntry,"Queries")+" kind="+(Read(sourceEntry,"Decision") as TrafficDecision)?.Kind+" submitted="+Read(anchor,"Submitted"));
                void Check(bool unborn)
                {
                    if(!ReferenceEquals(CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic"),preheatCore) ||
                        anchor==null || unborn && (!ReferenceEquals(Read(customers,"pendingAdmissionAnchor"),anchor) ||
                        !((TrafficIntentKey)Read(anchor,"Key")).Equals(anchorKey) || (int)Read(anchor,"FirstFrame")!=first ||
                        !ReferenceEquals(Read(anchor,"Owner"),Read(customers,"queue")) || !Equals(Read(customers,"nextVisit"),Read(anchor,"Before"))))
                        Assert.Ignore("NOT_PROVEN: original preheat anchor/context changed; no replacement opportunity");
                    if(submitted)
                    {
                        var actual=preheatEntries.Contains(anchorKey)?preheatEntries[anchorKey]:null;var decision=Read(actual,"Decision") as TrafficDecision;
                        if(!ReferenceEquals(actual,sourceEntry) || actual==null || (int)Read(actual,"FirstFrame")!=first)
                            Assert.Ignore("NOT_PROVEN: original preheat source identity/frame was lost");
                        var preheatSequenceNow=(long)Read(actual,"RequestSequence");if(firstSequence==0)firstSequence=preheatSequenceNow;
                        if(preheatSequenceNow!=firstSequence || unborn && ((bool)Read(actual,"Retired") || decision==null || decision.FailureDisposition==TrafficFailureDisposition.Terminal))
                            Assert.Ignore("NOT_PROVEN: original preheat source stopped or changed; no Terminal retry");
                        if(unborn && (((TrafficRequest)Read(actual,"Request"))?.RequestedFrame!=first || (long)Read(actual,"AcquiredSequence")!=0))
                            Assert.Ignore("NOT_PROVEN: preheat source was already consumed or lost its original frame");
                    }
                    if(unborn && (customers.Snapshot.Any(v=>v.VisitId==anchorKey.ActorId) || customers.CounterAssignments.Any(a=>a.VisitId==anchorKey.ActorId) ||
                        world.RegisteredActors.Any(a=>a!=null && a.ActorId==anchorKey.ActorId) || customers.Capacity.GetReservations().Any(r=>r.OwnerId==anchorKey.ActorId)))
                        Assert.Ignore("NOT_PROVEN: preheat opportunity was already born; no synthetic cleanup");
                    if(!unborn && (!customers.Snapshot.Any(v=>v.VisitId==anchorKey.ActorId) || !customers.CounterAssignments.Any(a=>a.VisitId==anchorKey.ActorId) ||
                        (long)Read(sourceEntry,"AcquiredSequence")<=0))
                        Assert.Ignore("NOT_PROVEN: actual Counter Join did not consume the original preheated source");
                }
                yield return Until(()=>
                {
                    var ready=owner.Snapshot.Orders.Count==1 && owner.Snapshot.Orders.Values.Single().State==OrderState.ReadyForPickup;
                    if(submitted){Check(true);return ready;} // No further public TrySpawn after the first actual submission.
                    if(ready)return true;
                    if(customers.CounterAssignments.Count!=0 || !customers.PickupAssignments.Any(a=>a.VisitId==id) || owner.Snapshot.Orders.Count!=1)return false;
                    if(!phaseSeen){phaseSeen=true;TestContext.WriteLine("M3_PREHEAT_PHASE frame="+Time.frameCount+" firstVisit="+id+" phase="+customers.Snapshot.Single(v=>v.VisitId==id).State+" counterEmpty=True pickupAssigned=True order="+owner.Snapshot.Orders.Values.Single().State);}
                    if(anchor!=null)Check(true);
                    else if(Read(customers,"pendingAdmissionAnchor")!=null)Assert.Ignore("NOT_PROVEN: a different admission opportunity existed before preheat");
                    if(customers.TrySpawn())Assert.Ignore("NOT_PROVEN: early admission was already born before the original WAIT");
                    var current=Read(customers,"pendingAdmissionAnchor");if(current==null)return false;
                    if(anchor==null){anchor=current;anchorKey=(TrafficIntentKey)Read(anchor,"Key");first=(int)Read(anchor,"FirstFrame");}
                    Check(true);submitted=(bool)Read(anchor,"Submitted");
                    if(submitted){sourceEntry=preheatEntries.Contains(anchorKey)?preheatEntries[anchorKey]:null;Check(true);TestContext.WriteLine("M3_PREHEAT_SUBMITTED frame="+Time.frameCount+" "+Facts());}
                    return false;
                });
                if(!submitted){TestContext.WriteLine("M3_PREHEAT status=NOT_PROVEN phaseSeen="+phaseSeen+" "+Facts());Assert.Ignore("NOT_PROVEN: no same unborn submission existed before the original Ready checkpoint");}
                Check(true);TestContext.WriteLine("M3_PREHEAT_READY frame="+Time.frameCount+" "+Facts());
                verifyPreheatUnborn=()=>Check(true);verifyPreheatBirth=()=>Check(false);preheatFacts=Facts;
            }
            else yield return Until(()=>owner.Snapshot.Orders.Count==1 && owner.Snapshot.Orders.Values.Single().State==OrderState.ReadyForPickup);
            var wait=customers.PickupAssignments.Single();Assert.That(sides.All(t=>Vector3.Distance(t.Position,wait.Position)>.99f),Is.True,"Preparing waiter cannot preoccupy the sole remaining delivery side");
            var core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");var entries=(IDictionary)Read(core,"entries");
            var corridor=typeof(CustomerFlowController).GetField("CorridorObserver",flags);var published=typeof(CustomerFlowController).GetField("PickupObserver",flags);
            var previous=corridor.GetValue(customers);var previousPublished=published.GetValue(customers);
            object visit=null,cursor=null,record=null,entry=null,total=null,history=null;NavigationActor actor=null,returnActor=null;
            PickupQueueService queue=null;PickupQueueService.ReservationClaim claim=null;PickupQueueService.DeliveryLeaseClaim lease=null;
            TrafficIntentKey key=default,returnKey=default;TrafficDecision returnSource=null;TrafficNativeStamp returnStamp=default;
            var sampled=false;var natural=false;var bound=false;var waitSeen=false;var released=false;var commits=0;var publishedValid=false;
            var m3CallArmed=false;var m3CallSampled=false;var m3RefusalSeen=false;object m3CallAnchor=null,m3CallEntry=null;ServiceTrafficCoordinator m3CallCore=null;
            var birth=-1;var index=-1;var count=0;var sequence=0L;var queries=0;var remaining=0f;var finite=0f;
            object frozenOwners=null,frozenResources=null;Vector3 target=default;
            var semanticArmed=false;var unrelatedNotifications=0;var unrelatedSampled=false;var unrelatedQueued=false;var unrelatedWake=false;
            var releaseNotified=false;var releaseQueued=false;var releaseWake=false;var releaseFirst=-1;var releaseSequence=0L;
            CounterQueueService semanticCounter=null;long counterVersionBefore=-1;TrafficParkingFact protectedParking=null;
            FieldInfo parkingHook=null;object previousParking=null;Action<TrafficParkingFact> combinedParking=null;
            TrafficParkingFact CurrentProtectedParking()=>((TrafficWorldSnapshot)Read(core,"snapshot")).Parking.FirstOrDefault(p=>
                p.Owner.Equals(returnKey) && p.ResourceId=="service/"+returnActor.ActorId && ReferenceEquals(p.CanonicalOwner,queue));
            bool SameProtectedParking(TrafficParkingFact p)=>p!=null && protectedParking!=null && p.Owner.Equals(protectedParking.Owner) &&
                ReferenceEquals(p.CanonicalOwner,protectedParking.CanonicalOwner) && p.ResourceId==protectedParking.ResourceId &&
                ReferenceEquals(p.BusinessOwner,protectedParking.BusinessOwner) && ReferenceEquals(p.ReleaseObligation,protectedParking.ReleaseObligation) &&
                p.Position==protectedParking.Position && p.Radius==protectedParking.Radius && p.SafetyMargin==protectedParking.SafetyMargin;
            void ObserveParkingNotification(TrafficParkingFact actual)
            {
                if(!semanticWitness || !semanticArmed || entry==null || !SameProtectedParking(actual))return;
                try
                {
                    var queued=(bool)Read(entry,"Queued");var wake=((IEnumerable)Read(core,"wakeups")).Cast<TrafficIntentKey>().Contains(key);
                    // Copy stored original-source outcomes only; no policy, refresh, query or pump call here.
                    if(lease.HasRetiredLifetime)
                    {
                        if(releaseNotified)return;releaseNotified=true;releaseQueued=queued;releaseWake=wake;
                        releaseFirst=(int)Read(entry,"FirstFrame");releaseSequence=(long)Read(entry,"RequestSequence");
                        records.Add("M3_RELEASE_NOTIFY frame="+Time.frameCount+" key="+key+" queued="+queued+" wake="+wake+" first="+releaseFirst+" seq="+releaseSequence);
                    }
                    else if(unrelatedCounterDelta && semanticCounter.Version!=counterVersionBefore)
                    {
                        unrelatedNotifications++;
                        if(unrelatedSampled)return;unrelatedSampled=true;unrelatedQueued=queued;unrelatedWake=wake;
                        records.Add("M3_UNRELATED_NOTIFY frame="+Time.frameCount+" actualCounterVersion="+semanticCounter.Version+" queued="+queued+" wake="+wake+
                            " owner="+actual.Owner.ActorId+" resource="+actual.ResourceId+" oldOwnerVersion="+actual.OwnerVersion);
                    }
                }
                catch(Exception error){errors.Add("M3 actual Notify observation UNKNOWN: "+error);}
            }
            string M3AdmissionFacts()
            {
                try
                {
                    var d=Read(m3CallEntry,"Decision") as TrafficDecision;var request=Read(m3CallEntry,"Request") as TrafficRequest;
                    var current=Read(m3CallCore,"snapshot") as TrafficWorldSnapshot;var preview=Read(m3CallAnchor,"Preview") as CounterQueueService.PreparedMutation;
                    string Key(TrafficIntentKey k)=>k.Session+"/e"+k.Epoch+"/"+k.ActorId+"/"+k.IntentId+"/g"+k.Generation+"/r"+k.LayoutRevision+"/"+k.Context;
                    string Snap(TrafficWorldSnapshot v)=>v==null?"NONE":v.Version+"/"+v.BodyVersion;
                    return "key="+(d==null?"NOT_RETAINED":Key(d.Intent))+" first="+Read(m3CallEntry,"FirstFrame")+" seq="+Read(m3CallEntry,"RequestSequence")+" acq="+Read(m3CallEntry,"AcquiredSequence")+" q="+Read(m3CallEntry,"Queries")+
                        " decision="+(d==null?"NONE":d.Kind+"/"+d.Reason+"/"+d.FailureDisposition)+" prepared="+Read(m3CallEntry,"Prepared")+" queued="+Read(m3CallEntry,"Queued")+" retired="+Read(m3CallEntry,"Retired")+
                        " requestSnapshot="+Snap(request?.Snapshot)+" currentSnapshot="+Snap(current)+" sameAnchor="+ReferenceEquals(Read(customers,"pendingAdmissionAnchor"),m3CallAnchor)+
                        " preview="+(preview==null?"NONE":Read(preview,"Kind")+"/"+Read(preview,"Id")+"/version="+Read(preview,"ExpectedVersion")+"/consumed="+Read(preview,"Consumed")+"/row="+preview.Assignment.VisitId+"/g"+preview.Assignment.Generation);
                }
                catch(Exception error){errors.Add("M3 admission refusal facts UNKNOWN: "+error);return "UNKNOWN";}
            }
            void ObserveFirst(string operation,string customer,string other,Vector3 ignored,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                if(unrelatedCounterDelta && m3CallArmed && !m3RefusalSeen && operation=="admission.original-refusal")
                {m3RefusalSeen=true;try{records.Add("M3_ADMISSION_REFUSAL frame="+Time.frameCount+" stage="+result+" "+M3AdmissionFacts());}catch(Exception error){errors.Add("M3 admission refusal callback UNKNOWN: "+error);}return;}
                if(sampled || customer!=id || operation!="traffic.collection-prerequisite-preflight")return;sampled=true;
                try
                {
                    visit=((IDictionary)Read(customers,"visits"))[id];actor=(NavigationActor)Read(visit,"Actor");queue=(PickupQueueService)Read(visit,"PickupOwner");
                    cursor=((IDictionary)Read(customers,"trafficCandidates")).Values.Cast<object>().Single(c=>
                        ((TrafficIntentKey)Read(c,"Key")).ActorId==id && ((TrafficIntentKey)Read(c,"Key")).IntentId=="pickup-service" &&
                        ((TrafficIntentKey)Read(c,"Key")).Context==id+"/"+Read(visit,"TrafficPhaseSequence"));
                    key=(TrafficIntentKey)Read(cursor,"Key");var frames=(IDictionary)Read(visit,"TrafficCandidateFrames");birth=frames.Contains(key)?(int)frames[key]:Time.frameCount;
                    var originalSides=(IReadOnlyList<NavigationTarget>)Read(cursor,"Sides");count=originalSides.Count;
                    history=Read(customers,"pickupDeliveryHistory");returnActor=(NavigationActor)Read(history,"Actor");
                    if(queue==null || history==null || returnActor==null){records.Add("COLLECTION_RELEASE_PREMISE UNKNOWN missing actual owner/return history");return;}
                    returnKey=(TrafficIntentKey)Read(history,"Key");if(!entries.Contains(returnKey)){records.Add("COLLECTION_RELEASE_PREMISE UNKNOWN missing original return entry");return;}
                    var returnEntry=entries[returnKey];var leg=Read(returnEntry,"Leg");var request=(TrafficRequest)Read(returnEntry,"Request");
                    returnSource=(TrafficDecision)Read(returnEntry,"Decision");var returnProof=Read(leg,"Proof") as TrafficRouteProof;
                    var deliverySource=Read(history,"Source") as TrafficDecision;
                    if(leg==null || request==null || returnSource==null || returnProof==null){records.Add("COLLECTION_RELEASE_PREMISE UNKNOWN actual return source already retired or not issued");return;}
                    returnStamp=(TrafficNativeStamp)Read(leg,"Stamp");
                    var snapshot=(TrafficWorldSnapshot)Read(core,"snapshot");var body=snapshot.Bodies.SingleOrDefault(b=>b.Owner.Equals(returnKey));
                    var returnRow=((IEnumerable)Read(employees,"employees")).Cast<object>().SingleOrDefault(row=>ReferenceEquals(Read(row,"Actor"),returnActor));
                    var returnPort=Read(returnRow,"Port");var returnRunner=Read(returnRow,"Runner");var returnMove=Read(returnPort,"trafficMove");var runnerLeg=Read(returnRunner,"moveLeg");
                    var prepared=(bool)Read(returnEntry,"Prepared");var active=(bool)Read(returnEntry,"Active");var queued=(bool)Read(returnEntry,"Queued");
                    var legBound=(bool)Read(leg,"Bound");var committed=(bool)Read(leg,"Committed");var completed=(bool)Read(leg,"Completed");
                    var currentPath=(Vector3[])Read(returnEntry,"CurrentPath");var legPath=(Vector3[])Read(leg,"Path");
                    var proofCurrent=request.Snapshot.Version==snapshot.Version && request.Snapshot.BodyVersion==snapshot.BodyVersion;
                    var actualMoveCurrent=returnMove!=null && runnerLeg!=null && ReferenceEquals(Read(returnMove,"Coordinator"),core) &&
                        ReferenceEquals(Read(returnMove,"Source"),deliverySource) && ReferenceEquals(Read(returnMove,"Proof"),returnProof) &&
                        ((TrafficIntentKey)Read(returnMove,"Intent")).Equals(returnKey) && (string)Read(returnMove,"Resource")=="selected-home" &&
                        (EmployeeState)Read(returnRunner,"state")==EmployeeState.Returning && (EmployeeState)Read(runnerLeg,"State")==EmployeeState.Returning &&
                        (int)Read(returnMove,"Index")== (int)Read(leg,"Index") && (int)Read(returnMove,"NativeGeneration")>0 &&
                        (int)Read(returnMove,"NativeGeneration")== (int)Read(leg,"Generation") && (int)Read(runnerLeg,"NativeLegGeneration")== (int)Read(leg,"Generation") &&
                        ((NavigationTarget)Read(returnMove,"Target")).Position==request.Target && ((NavigationTarget)Read(returnMove,"Goal")).Equals((NavigationTarget)Read(history,"Work")) &&
                        !((bool)Read(returnMove,"Suspended")) && !((bool)Read(runnerLeg,"Completed")) && ReferenceEquals(Read(leg,"ActorLife"),returnActor);
                    // Same immutable snapshot + the actual completed Core proof establish the prepared geometry; no policy/query replay.
                    // 同一快照的真实 Core proof 作为证据，不重跑 CanProgress/IndependentPathClear 或复制碰撞规则。
                    var preparedProof=runnerLeg!=null && legPath!=null && prepared && !active && !queued && !committed && !legBound && proofCurrent &&
                        !(bool)Read(leg,"NeedsQuery") && body!=null && body.Position==request.Start &&
                        (bool)Read(runnerLeg,"AwaitingTraffic") && (long)Read(runnerLeg,"RequestId")==0 &&
                        returnSource.Blockers.Count==0 && returnSource.ProofRoutes.Any(proof=>proof.ResourceId=="selected-home" && proof.ExpandedPath.SequenceEqual(legPath));
                    var boundProof=active && !prepared && committed && legBound && returnStamp.RequestId>0 && returnStamp.LegGeneration==(int)Read(leg,"Generation") &&
                        runnerLeg!=null && (long)Read(runnerLeg,"RequestId")==returnStamp.RequestId && body!=null && body.IndependentlyProgressing && body.RemainingSeconds>0;
                    var returnProgress=actualMoveCurrent && !completed && !(bool)Read(leg,"Suspended") && !(bool)Read(returnEntry,"Retired") &&
                        !(bool)Read(returnEntry,"Parking") && Read(returnEntry,"PendingHandoff")==null && request.IndependentlyProgressing &&
                        request.FiniteSeconds>0 && !float.IsNaN(request.FiniteSeconds) && !float.IsInfinity(request.FiniteSeconds) &&
                        currentPath.Length>1 && legPath.Length>0 && (preparedProof || boundProof);
                    records.Add("COLLECTION_RELEASE_RETURN prepared="+prepared+" active="+active+" queued="+queued+" independent="+request.IndependentlyProgressing+
                        " finite="+request.FiniteSeconds+" currentPath="+string.Join(";",currentPath.Select(point=>point.ToString("R")))+" legCompleted="+completed+" committed="+committed+
                        " bound="+legBound+" moveCurrent="+actualMoveCurrent+" movementSourceSame="+ReferenceEquals(Read(returnMove,"Source"),deliverySource)+
                        " nativeGeneration="+Read(returnMove,"NativeGeneration")+" legGeneration="+Read(leg,"Generation")+" runnerGeneration="+Read(runnerLeg,"NativeLegGeneration")+
                        " runnerRequest="+Read(runnerLeg,"RequestId")+" awaiting="+Read(runnerLeg,"AwaitingTraffic")+" snapshot="+request.Snapshot.Version+"/"+request.Snapshot.BodyVersion+
                        " currentSnapshot="+snapshot.Version+"/"+snapshot.BodyVersion+" proofCurrent="+proofCurrent+" preparedProof="+preparedProof+" boundProof="+boundProof+" progress="+returnProgress);
                    if(!queue.TryCaptureDeliveryLease(returnActor.ActorId,out lease))return;
                    target=lease.Position;index=originalSides.ToList().FindIndex(side=>side.Position==target);
                    var evidence=new object[]{id,target,null,null};
                    var actualPrerequisite=(bool)typeof(PickupQueueService).GetMethod("TryCaptureCollectionReleasePrerequisite",flags).Invoke(queue,evidence);
                    claim=(PickupQueueService.ReservationClaim)evidence[2];var soleLease=(PickupQueueService.DeliveryLeaseClaim)evidence[3];
                    total=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied).Token;
                    var pending=Read(visit,"PendingTraffic");var predecessor=pending==null?null:Read(pending,"Intent");
                    var predecessorEntry=predecessor!=null && entries.Contains(predecessor)?entries[predecessor]:null;
                    var predecessorDecision=Read(predecessorEntry,"Decision") as TrafficDecision;
                    records.Add("COLLECTION_RELEASE_CUSTOMER_SOURCE pending="+(pending!=null)+" predecessor="+predecessorDecision?.Kind+"/"+predecessorDecision?.FailureDisposition+
                        " handoff="+(Read(predecessorEntry,"PendingHandoff")!=null)+" originalFinite="+Read(predecessorEntry,"FiniteLimit"));
                    natural=(pending==null || predecessorDecision!=null && predecessorDecision.FailureDisposition==TrafficFailureDisposition.None) && actualPrerequisite && soleLease!=null && soleLease.IsSameLease(lease) && lease.IsCurrent && claim!=null && claim.IsCurrent && claim.Assignment.Arrived &&
                        claim.Assignment.SlotIndex==0 && (CustomerVisitState)Read(visit,"State")==CustomerVisitState.PickupWaiting && (int)Read(visit,"DeliveryYieldPhase")==0 &&
                        (long)Read(visit,"RequestId")==0 && actor.isActiveAndEnabled && world.RegisteredActors.Contains(actor) && count==2 && index>=0 && target!=south.Position &&
                        !entries.Contains(key) && Read(cursor,"Source")==null && !((IEnumerable)Read(cursor,"AttemptedIndices")).Cast<object>().Any() &&
                        snapshot.Bodies.Any(b=>b.Owner.ActorId==block.ActorId && b.Position==south.Position && b.Enabled && !b.IndependentlyProgressing) && body!=null && ReferenceEquals(body.ActorLife,returnActor) && body.Enabled && body.ActorActive &&
                        returnProgress && request.ResourceId=="selected-home" && returnProof.ResourceId=="selected-home" && returnProof.Waypoints.Count>0 &&
                        returnProof.ExpandedPath.Count>0 && returnProof.LegEnds.Count==returnProof.Waypoints.Count && returnProof.Waypoints.Last()==((NavigationTarget)Read(history,"Work")).Position &&
                        returnSource.Kind==TrafficDecisionKind.Granted && returnSource.FailureDisposition==TrafficFailureDisposition.None && returnSource.AcquiredSequence>0 &&
                        deliverySource!=null && ReferenceEquals(Read(returnEntry,"JourneyPermit"),deliverySource) && deliverySource.ProofRoutes.Any(p=>ReferenceEquals(p,returnProof)) &&
                        ((PickupQueueService.DeliveryLeaseClaim)Read(history,"Lease")).IsSameLease(lease);
                    record=Read(cursor,"CollectionRelease");
                    bound=record!=null && ReferenceEquals(Read(record,"Flow"),customers) && ReferenceEquals(Read(record,"Coordinator"),core) &&
                        ReferenceEquals(Read(record,"Visit"),visit) && ReferenceEquals(Read(record,"Actor"),actor) && ReferenceEquals(Read(record,"Admission"),Read(visit,"Admission")) &&
                        ReferenceEquals(Read(record,"Total"),total) && ReferenceEquals(Read(record,"Capacity"),customers.Capacity) && ReferenceEquals(Read(record,"Owner"),queue) && ReferenceEquals(Read(record,"Cursor"),cursor) &&
                        ReferenceEquals(Read(record,"History"),history) && ReferenceEquals(Read(record,"ReturnActor"),returnActor) && ReferenceEquals(Read(record,"ReturnSource"),returnSource) && ReferenceEquals(Read(record,"ReturnProof"),returnProof) &&
                        ((TrafficIntentKey)Read(record,"Key")).Equals(key) && ((TrafficIntentKey)Read(record,"ReturnKey")).Equals(returnKey) &&
                        ((TrafficNativeStamp)Read(record,"ReturnStamp")).Equals(returnStamp) && (int)Read(record,"FirstFrame")==birth && (int)Read(record,"CandidateIndex")==index &&
                        ((QueueAssignment)Read(record,"Assignment")).Equals(claim.Assignment) && (int)Read(record,"Generation")==(int)Read(visit,"Generation") &&
                        (int)Read(record,"PhaseSequence")== (int)Read(visit,"TrafficPhaseSequence") && ReferenceEquals(Read(record,"Pending"),pending) &&
                        ReferenceEquals(Read(record,"Issued"),Read(visit,"IssuedTraffic")) && ReferenceEquals(Read(record,"Selection"),Read(visit,"TrafficSelection")) &&
                        ReferenceEquals(Read(record,"Movement"),Read(visit,"TrafficMove")) && ReferenceEquals(Read(record,"World"),world) && ReferenceEquals(Read(record,"Adapter"),adapter) &&
                        (float)Read(record,"Radius")==actor.Settings.AgentRadius && (float)Read(record,"Skin")==actor.Settings.CollisionSkin && (float)Read(record,"Epsilon")==actor.Settings.Epsilon &&
                        (long)Read(record,"OwnerLife")== (long)Read(customers,"pickupPreparationLife") && (Vector3)Read(record,"Start")==actor.transform.position &&
                        (Vector3)Read(record,"Target")==target && ((PickupQueueService.ReservationClaim)Read(record,"Claim")).IsSameReservation(claim) &&
                        ((PickupQueueService.DeliveryLeaseClaim)Read(record,"Lease")).IsSameLease(lease) && Read(record,"CurrentPreview")==null &&
                        (Read(record,"FixedRequirements") as IReadOnlyList<TrafficRouteRequirement>)?.Any(r=>r.ResourceId=="collection-exit-capability" &&
                            r.Start==target && r.Goal==((Transform)Read(customers,"exit")).position && !r.SelectedJourney && !r.AvoidProposedParking)==true;
                    records.Add("COLLECTION_RELEASE_CAPTURE natural="+natural+" bound="+bound+" record="+(record!=null)+" frame="+Time.frameCount+" birth="+birth+
                        " key="+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+" target="+target+" candidate="+index+"/"+count+" return="+returnSource.Kind+"/"+returnSource.AcquiredSequence+
                        " returnBound="+Read(leg,"Bound")+" native="+returnStamp.RequestId+"/"+returnStamp.LegGeneration+" bodyRemaining="+body?.RemainingSeconds+" queueVersion="+queue.Version);
                }
                catch(Exception error){errors.Add(error.ToString());}
            }
            void ObserveCommit(string operation,string customer,PickupQueueService actualOwner,QueueAssignment assignment)
            {
                if(operation!="collection.published" || customer!=id)return;commits++;
                try
                {
                    var actual=entries[key];var primary=(TrafficRequest)(Read(actual,"JourneyRequest")??Read(actual,"Request"));
                    var preview=Read(record,"CurrentPreview") as PickupQueueService.PreparedMutation;
                    var actualSequence=(long)Read(actual,"RequestSequence");if(sequence==0)sequence=actualSequence;
                    publishedValid=commits==1 && ReferenceEquals(actual,entry) && ReferenceEquals(actualOwner,queue) && assignment.Position==target && lease.HasRetiredLifetime &&
                        !queue.TryGetServiceLease(returnActor.ActorId,out _) && (int)Read(actual,"FirstFrame")==birth && primary.RequestedFrame==birth &&
                        actualSequence>0 && actualSequence==sequence && (int)Read(actual,"CandidateCount")==count && (int)Read(actual,"CurrentIndex")==index &&
                        (int)Read(actual,"Queries")>=queries && (int)Read(actual,"Queries")<=(int)Read(core,"queriesPerIntent") && (float)Read(actual,"FiniteLimit")==finite &&
                        (!waitSeen || ReferenceEquals(Read(actual,"FrozenOwners"),frozenOwners) && ReferenceEquals(Read(actual,"FrozenResources"),frozenResources) && (float)Read(actual,"WaitRemaining")<=remaining) &&
                        customers.Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,total) && r.State==ReservationState.Occupied) &&
                        preview!=null && (bool)Read(preview,"Consumed") && ReferenceEquals(Read(preview,"owner"),queue) && (string)Read(preview,"Id")==id &&
                        (PickupQueueService.MutationKind)Read(preview,"Kind")==PickupQueueService.MutationKind.CollectionLease && preview.Assignment.Position==target;
                    released|=lease.HasRetiredLifetime;records.Add("COLLECTION_RELEASE_COMMIT once="+commits+" valid="+publishedValid+" frame="+Time.frameCount+
                        " first="+Read(actual,"FirstFrame")+" seq="+Read(actual,"RequestSequence")+" acq="+Read(actual,"AcquiredSequence")+" queries="+Read(actual,"Queries")+" wait="+Read(actual,"WaitRemaining"));
                }
                catch(Exception error){errors.Add(error.ToString());}
            }
            var combined=Delegate.Combine(previous as Delegate,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)ObserveFirst);
            var combinedPublished=Delegate.Combine(previousPublished as Delegate,(Action<string,string,PickupQueueService,QueueAssignment>)ObserveCommit);
            if(semanticWitness)
            {
                parkingHook=typeof(CustomerFlowController).GetField("ParkingNotificationObserver",flags);Assert.That(parkingHook,Is.Not.Null,"COMPILE_SCAFFOLD: actual original Notify observer");
                previousParking=parkingHook.GetValue(customers);combinedParking=(Action<TrafficParkingFact>)Delegate.Combine(previousParking as Delegate,(Action<TrafficParkingFact>)ObserveParkingNotification);
            }
            collectionReleaseWitnessCleanup=()=>
            {
                if(finished)return;finished=true;collectionReleaseWitnessCleanup=null;
                try{if(ReferenceEquals(corridor.GetValue(customers),combined))corridor.SetValue(customers,previous);else errors.Add("Corridor observer replaced externally");}
                catch(Exception error){errors.Add(error.ToString());}
                try{if(ReferenceEquals(published.GetValue(customers),combinedPublished))published.SetValue(customers,previousPublished);else errors.Add("Pickup observer replaced externally");}
                catch(Exception error){errors.Add(error.ToString());}
                if(semanticWitness && parkingHook!=null)
                    try{if(ReferenceEquals(parkingHook.GetValue(customers),combinedParking))parkingHook.SetValue(customers,previousParking);else errors.Add("M3 parking observer replaced externally");}
                    catch(Exception error){errors.Add(error.ToString());}
                foreach(var line in records)TestContext.WriteLine(line);
                TestContext.WriteLine("COLLECTION_RELEASE_WITNESS cap1="+sampled+" natural="+natural+" wait="+waitSeen+" retired="+released+" commits="+commits+" errors="+errors.Count);
                if(unrelatedCounterDelta)TestContext.WriteLine("M3_ADMISSION_REFUSAL_WITNESS status="+(errors.Count>0?"UNKNOWN":m3RefusalSeen?"OBSERVED":"NOT_PROVEN")+" sampledCall="+m3CallSampled+" cap=1");
                foreach(var error in errors)TestContext.WriteLine("COLLECTION_RELEASE_ERROR UNKNOWN "+error);
            };
            corridor.SetValue(customers,combined);published.SetValue(customers,combinedPublished);
            if(semanticWitness)parkingHook.SetValue(customers,combinedParking);
            try
            {
                tools.ResumeCollection();yield return Until(()=>sampled);
                Assert.That(errors,Is.Empty);if(!natural)Assert.Ignore("NOT_PROVEN: original actual head/sole lease/authenticated prepared-or-bound finite return source did not coincide; no fabricated timing bridge");
                Assert.That(bound,Is.True,"FEATURE: original finite collection prerequisite must be production-captured before first Submit, without a fabricated Preview");
                entry=entries[key];Assert.That(entry,Is.Not.Null);finite=(float)Read(entry,"FiniteLimit");
                void ObserveLedger()
                {
                    Assert.That(errors,Is.Empty);if(commits>0)return; // Commit observer retains the primary accounting before ordinary strict/exit retirement.
                    Assert.That(entries[key],Is.SameAs(entry));Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(birth));
                    var primary=(TrafficRequest)(Read(entry,"JourneyRequest")??Read(entry,"Request"));Assert.That(primary.RequestedFrame,Is.EqualTo(birth));
                    Assert.That(Read(entry,"CandidateCount"),Is.EqualTo(count));Assert.That(Read(entry,"CurrentIndex"),Is.EqualTo(index));
                    CollectionAssert.AreEquivalent(new[]{index},((IEnumerable)Read(entry,"AttemptedIndices")).Cast<int>().ToArray());
                    Assert.That(Read(entry,"FiniteLimit"),Is.EqualTo(finite));var currentSequence=(long)Read(entry,"RequestSequence");
                    if(sequence==0)sequence=currentSequence;else Assert.That(currentSequence,Is.EqualTo(sequence));
                    var spent=(int)Read(entry,"Queries");Assert.That(spent,Is.GreaterThanOrEqualTo(queries));Assert.That(spent,Is.LessThanOrEqualTo((int)Read(core,"queriesPerIntent")));queries=spent;
                    var decision=(TrafficDecision)Read(entry,"Decision");released|=lease.HasRetiredLifetime;
                    if(lease.IsCurrent){Assert.That(Read(record,"CurrentPreview"),Is.Null);Assert.That(Read(entry,"AcquiredSequence"),Is.EqualTo(0L));Assert.That(commits,Is.EqualTo(0));}
                    Assert.That(decision.FailureDisposition,Is.Not.EqualTo(TrafficFailureDisposition.Terminal),"Original prerequisite stopped; no retry/new-key/permission fallback");
                    if(!waitSeen && decision.Kind==TrafficDecisionKind.WaitingForTraffic && decision.Blockers.Any(b=>b.Owner.Equals(returnKey) && b.ResourceId=="service/"+returnActor.ActorId))
                    {
                        waitSeen=true;remaining=(float)Read(entry,"WaitRemaining");frozenOwners=Read(entry,"FrozenOwners");frozenResources=Read(entry,"FrozenResources");
                        Assert.That(remaining,Is.GreaterThan(0));Assert.That(queries,Is.EqualTo(0));Assert.That(Read(entry,"AcquiredSequence"),Is.EqualTo(0L));
                        records.Add("COLLECTION_RELEASE_WAIT first="+birth+" seq="+sequence+" queries="+queries+" wait="+remaining+" candidate="+index+"/"+count+" frame="+Time.frameCount);
                    }
                    if(waitSeen){var left=(float)Read(entry,"WaitRemaining");Assert.That(left,Is.LessThanOrEqualTo(remaining));remaining=left;}
                }
                if(semanticWitness)
                {
                    yield return Until(()=>{ObserveLedger();return waitSeen || commits>0;});
                    if(!waitSeen || commits!=0 || !lease.IsCurrent)Assert.Ignore("NOT_PROVEN: original actual parking WAIT/held-lease overlap missing");
                    protectedParking=CurrentProtectedParking();Assert.That(protectedParking,Is.Not.Null,"PREMISE: exact original service owner/resource parking fact");
                    semanticCounter=Read(customers,"queue") as CounterQueueService;Assert.That(semanticCounter,Is.Not.Null);
                    counterVersionBefore=semanticCounter.Version;semanticArmed=true;
                    if(unrelatedCounterDelta)
                    {
                        var originalPickupVersion=queue.Version;var originalAssignment=claim.Assignment;
                        var originalGeneration=(int)Read(visit,"Generation");var originalPhase=(CustomerVisitState)Read(visit,"State");
                        var originalReturnState=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State;
                        verifyPreheatUnborn();records.Add("M3_PREHEAT_BASELINE "+preheatFacts()+" originalWait="+((TrafficDecision)Read(entry,"Decision")).Kind+" first="+Read(entry,"FirstFrame")+" seq="+Read(entry,"RequestSequence")+" parkedResource="+protectedParking.ResourceId+" parkedVersion="+protectedParking.OwnerVersion+" claim="+claim.Assignment.VisitId+"/g"+claim.Assignment.Generation+" phase="+originalPhase+" returnState="+originalReturnState);
                        var known=customers.Snapshot.Select(v=>v.VisitId).ToArray();var admitted=false;
                        // Poll the ordinary asynchronous public admission once per normal frame; no synthetic Queue ID or private write.
                        for(var frame=0;frame<6000;frame++)
                        {
                            admitted=customers.Snapshot.Any(v=>!known.Contains(v.VisitId));
                            if(admitted && semanticCounter.Version!=counterVersionBefore || !lease.IsCurrent || commits>0)break;
                            verifyPreheatUnborn(); // Stop rather than retry a replaced/Terminal source.
                            if(!m3CallSampled)
                            {
                                var m3Anchor=Read(customers,"pendingAdmissionAnchor");var m3Key=Read(m3Anchor,"Key");
                                var m3Entry=m3Key is TrafficIntentKey && entries.Contains(m3Key)?entries[m3Key]:null;
                                if((Read(m3Entry,"Decision") as TrafficDecision)?.Kind==TrafficDecisionKind.Granted)
                                {m3CallSampled=true;m3CallArmed=true;m3CallAnchor=m3Anchor;m3CallEntry=m3Entry;m3CallCore=Read(m3Anchor,"Coordinator") as ServiceTrafficCoordinator;records.Add("M3_ADMISSION_BEFORE frame="+Time.frameCount+" "+M3AdmissionFacts());}
                            }
                            var m3Accepted=false;var m3Returned=false;
                            try{m3Accepted=customers.TrySpawn();m3Returned=true;}
                            finally{if(m3CallArmed){records.Add("M3_ADMISSION_AFTER frame="+Time.frameCount+" returned="+m3Returned+" accepted="+m3Accepted+" "+M3AdmissionFacts());m3CallArmed=false;}}
                            yield return null;ObserveLedger();
                        }
                        var newIds=customers.Snapshot.Where(v=>!known.Contains(v.VisitId)).Select(v=>v.VisitId).ToArray();
                        admitted=newIds.Length>0 && semanticCounter.Snapshot.Any(a=>newIds.Contains(a.VisitId));
                        if(admitted){verifyPreheatBirth();records.Add("M3_PREHEAT_BIRTH "+preheatFacts()+" actualNewIds=["+string.Join(";",newIds)+"]");}
                        var body=((TrafficWorldSnapshot)Read(core,"snapshot")).Bodies.FirstOrDefault(p=>ReferenceEquals(p.ActorLife,returnActor));
                        var current=CurrentProtectedParking();
                        // Copy each original evaluated operand once; later short-circuit terms remain explicitly unknown.
                        var premise=new Dictionary<string,string>();
                        T Capture<T>(string name,T value)
                        {
                            try{premise[name]=value is QueueAssignment row?row.VisitId+"/slot="+row.SlotIndex+"/g"+row.Generation+"/pose="+row.Position.ToString("F9")+"/arrived="+row.Arrived:Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture);}
                            catch(Exception){}return value;
                        }
                        var independent=Capture("admitted",admitted) &&
                            Capture("sameCounter",ReferenceEquals(Read(customers,"queue"),semanticCounter)) &&
                            Capture("counterVersionChanged",Capture("counterVersionAtCondition",semanticCounter.Version)!=counterVersionBefore) &&
                            Capture("pickupVersionSame",Capture("pickupVersionAtCondition",queue.Version)==originalPickupVersion) &&
                            Capture("protectedParkingSame",SameProtectedParking(current)) &&
                            Capture("parkingOwnerVersionSame",Capture("parkingOwnerVersionAtCondition",current.OwnerVersion)==protectedParking.OwnerVersion) &&
                            Capture("leaseCaptured",queue.TryCaptureDeliveryLease(returnActor.ActorId,out var heldLease)) &&
                            Capture("sameLease",lease.IsSameLease(heldLease)) &&
                            Capture("leaseCurrent",lease.IsCurrent) &&
                            Capture("claimCaptured",queue.TryCaptureReservation(id,out var heldClaim)) &&
                            Capture("sameClaim",claim.IsSameReservation(heldClaim)) &&
                            Capture("claimCurrent",claim.IsCurrent) &&
                            Capture("claimRowSame",Capture("claimRowAtCondition",heldClaim.Assignment).Equals(originalAssignment)) &&
                            Capture("historySame",ReferenceEquals(Read(customers,"pickupDeliveryHistory"),history)) &&
                            Capture("bodyPresent",body!=null) &&
                            Capture("bodyActive",body.ActorActive) &&
                            Capture("bodyEnabled",body.Enabled) &&
                            Capture("sameActor",ReferenceEquals(Read(visit,"Actor"),actor)) &&
                            Capture("generationSame",Capture("generationAtCondition",(int)Read(visit,"Generation"))==originalGeneration) &&
                            Capture("phaseSame",Capture("phaseAtCondition",(CustomerVisitState)Read(visit,"State"))==originalPhase) &&
                            Capture("expectedPhaseIsPickupWaiting",originalPhase==CustomerVisitState.PickupWaiting) &&
                            Capture("returnStateSame",Capture("returnStateAtCondition",employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).State)==originalReturnState) &&
                            Capture("noCommits",commits==0);
                        if(!independent)
                        {
                            try
                            {
                                string Key(TrafficIntentKey k)=>k.Session+"/e"+k.Epoch+"/"+k.ActorId+"/"+k.IntentId+"/g"+k.Generation+"/r"+k.LayoutRevision+"/"+k.Context;
                                string Identity(object o)=>o==null?"null":o.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(o);
                                string Parking(TrafficParkingFact p)=>p==null?"NONE":Key(p.Owner)+"/"+p.ResourceId+"/v"+p.OwnerVersion+"/pose="+p.Position.ToString("F9")+"/radius="+p.Radius+"/margin="+p.SafetyMargin+"/canonical="+Identity(p.CanonicalOwner)+"/business="+Identity(p.BusinessOwner)+"/release="+Identity(p.ReleaseObligation);
                                string Decision(object e){var d=Read(e,"Decision") as TrafficDecision;return d==null?"NOT_RETAINED":Key(d.Intent)+"/"+d.Kind+"/"+d.Reason+"/disposition="+d.FailureDisposition+"/seq="+d.RequestSequence+"/acq="+d.AcquiredSequence+"/first="+Read(e,"FirstFrame")+"/queries="+Read(e,"Queries")+"/retired="+Read(e,"Retired");}
                                var anchor=Read(customers,"pendingAdmissionAnchor");var admissionCore=Read(anchor,"Coordinator") as ServiceTrafficCoordinator;
                                var admissionEntries=Read(admissionCore,"entries") as IDictionary;var admissionKey=Read(anchor,"Key");
                                var admissionEntry=admissionKey is TrafficIntentKey && admissionEntries!=null && admissionEntries.Contains(admissionKey)?admissionEntries[admissionKey]:null;
                                TestContext.WriteLine("M3_UNRELATED_PREMISE cap=1 independent=False admitted="+admitted+" newIds=["+string.Join(";",newIds)+"] counterVersion="+counterVersionBefore+"->"+semanticCounter.Version+" pickupVersion="+originalPickupVersion+"->"+queue.Version+" commits="+commits+" body="+(body==null?"NONE":body.ActorActive+"/"+body.Enabled+"/independent="+body.IndependentlyProgressing+"/remaining="+body.RemainingSeconds)+
                                    " originalReturnState="+originalReturnState+" originalGeneration="+originalGeneration+" originalPhase="+originalPhase+" generationNow="+Read(visit,"Generation")+" phaseNow="+Read(visit,"State"));
                                TestContext.WriteLine("M3_UNRELATED_QUALIFIERS "+string.Join(";",new[]{"admitted","sameCounter","counterVersionChanged","pickupVersionSame","protectedParkingSame","parkingOwnerVersionSame","leaseCaptured","sameLease","leaseCurrent","claimCaptured","sameClaim","claimCurrent","claimRowSame","historySame","bodyPresent","bodyActive","bodyEnabled","sameActor","generationSame","phaseSame","expectedPhaseIsPickupWaiting","returnStateSame","noCommits"}.Select(n=>n+"="+(premise.TryGetValue(n,out var v)?v:"UNKNOWN_NOT_EVALUATED")))+" evaluatedValues=["+string.Join(";",premise.Select(p=>p.Key+"="+p.Value))+"]");
                                TestContext.WriteLine("M3_UNRELATED_PARKING protected="+Parking(protectedParking)+" current="+Parking(current));
                                TestContext.WriteLine("M3_UNRELATED_ADMISSION anchor="+Identity(anchor)+" key="+(admissionKey is TrafficIntentKey a?Key(a):"NONE")+" first="+Read(anchor,"FirstFrame")+" submitted="+Read(anchor,"Submitted")+" core="+Identity(admissionCore)+" sameCore="+ReferenceEquals(admissionCore,core)+" decision="+Decision(admissionEntry));
                                TestContext.WriteLine("M3_UNRELATED_NEW_CORE ["+string.Join(";",entries.Keys.Cast<TrafficIntentKey>().Where(k=>newIds.Contains(k.ActorId)).Select(k=>Decision(entries[k])))+"]");
                            }
                            catch(Exception error){try{TestContext.WriteLine("M3_UNRELATED_PREMISE UNKNOWN "+error);}catch(Exception){}}
                        }
                        if(!independent)Assert.Ignore("NOT_PROVEN: actual additional counter Join did not coincide with unchanged protected claim/lifetime/phase/bodylife");
                        Assert.That(errors,Is.Empty);records.Add("M3_UNRELATED_COUNTER realJoin=True protectedSame=True counter="+counterVersionBefore+"->"+semanticCounter.Version+
                            " notifications="+unrelatedNotifications+" queuedAtActualNotify="+unrelatedQueued+" wakeAtActualNotify="+unrelatedWake+" first="+birth+" seq="+sequence);
                        Assert.That(unrelatedNotifications,Is.Zero,"PRODUCT: unrelated actual Counter Queue version must not notify this unchanged original service parking resource");
                        Assert.That(Read(entry,"Queued"),Is.EqualTo(false));CollectionAssert.DoesNotContain(((IEnumerable)Read(core,"wakeups")).Cast<TrafficIntentKey>().ToArray(),key);
                        Assert.That(core.GetDecision(key).Kind,Is.EqualTo(TrafficDecisionKind.WaitingForTraffic));Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(birth));
                        Assert.That(Read(entry,"RequestSequence"),Is.EqualTo(sequence));Assert.That(Read(entry,"Queries"),Is.EqualTo(0));
                        Assert.That(Read(entry,"FrozenOwners"),Is.SameAs(frozenOwners));Assert.That(Read(entry,"FrozenResources"),Is.SameAs(frozenResources));
                        yield break;
                    }
                }
                if(stopReturnOwner)
                {
                    // First establish the actual original parking-edge WAIT; no synthetic blocker or forced clock.
                    yield return Until(()=>{ObserveLedger();return waitSeen || commits>0;});
                    if(!waitSeen || !lease.IsCurrent || commits!=0)Assert.Ignore("NOT_PROVEN: no actual held-lease parking WAIT remained before the public lifecycle change");
                    var releaseDistance=2*returnActor.Settings.AgentRadius+returnActor.Settings.CollisionSkin+returnActor.Settings.Epsilon+returnActor.Settings.ArrivalDistance;
                    if(Vector3.Distance(returnActor.transform.position,lease.Position)>releaseDistance)
                        Assert.Ignore("NOT_PROVEN: return actor had already physically vacated its lease; this negative needs the original parked body");
                    var originalPosition=returnActor.transform.position;var originalAssignment=claim.Assignment;
                    var customerKeys=entries.Keys.Cast<TrafficIntentKey>().Where(k=>k.ActorId==id).ToArray();
                    var beforeWait=(float)Read(entry,"WaitRemaining");var beforeQueries=(int)Read(entry,"Queries");
                    var disableWitness=System.Environment.GetCommandLineArgs().Contains("-trafficCollectionReleaseDisableWitness");
                    var disableFrame=Time.frameCount;var lastWitnessFrame=-1;var normalWitnesses=0;
                    void ObserveDisable(string stage)
                    {
                        if(!disableWitness)return;
                        try
                        {
                            object Property(object value,string name)=>value.GetType().GetProperty(name,flags).GetValue(value);
                            string Source(string role,TrafficIntentKey intent)
                            {
                                var exists=entries.Contains(intent);var actual=exists?entries[intent]:null;var decision=Read(actual,"Decision") as TrafficDecision;
                                return "COLLECTION_RELEASE_DISABLE_SOURCE stage="+stage+" frame="+Time.frameCount+" role="+role+
                                    " key="+intent.Session+"/e"+intent.Epoch+"/"+intent.ActorId+"/"+intent.IntentId+"/g"+intent.Generation+"/r"+intent.LayoutRevision+"/"+intent.Context+
                                    " containsIntent="+exists+" kind="+decision?.Kind+" disposition="+decision?.FailureDisposition+" reason="+decision?.Reason+
                                    " queued="+Read(actual,"Queued")+" prepared="+Read(actual,"Prepared")+" active="+Read(actual,"Active")+" retired="+Read(actual,"Retired")+
                                    " first="+Read(actual,"FirstFrame")+" seq="+Read(actual,"RequestSequence")+" acq="+Read(actual,"AcquiredSequence")+" queries="+Read(actual,"Queries")+
                                    " wait="+Read(actual,"WaitRemaining")+" handoffRemaining="+Read(Read(actual,"PendingHandoff"),"Remaining")+" paused="+Read(core,"paused");
                            }
                            var predecessorKey=(TrafficIntentKey)Read(Read(record,"Pending"),"Intent");
                            var actualSnapshot=(TrafficWorldSnapshot)Read(core,"snapshot");var actualBody=actualSnapshot.Bodies.FirstOrDefault(b=>ReferenceEquals(b.ActorLife,returnActor));
                            records.Add("COLLECTION_RELEASE_DISABLE_GATE stage="+stage+" frame="+Time.frameCount+" ownerCanProgress="+owner.CanProgress+
                                " baseGate="+Property(owner,"BaseGate")+" comboReady="+Read(owner,"comboReady")+" ownerHeld="+Read(owner,"held")+
                                " cfcCanProgress="+Property(customers,"CanIntegratedProgress")+" cfcHeld="+Read(customers,"integratedHeld")+
                                " timeSpeed="+time.CurrentSpeed+" resumeBlocked="+time.IsResumeBlocked+" corePaused="+Read(core,"paused")+
                                " actorEnabled="+returnActor.enabled+" actorActive="+returnActor.isActiveAndEnabled+" registered="+world.RegisteredActors.Contains(returnActor)+
                                " bodyPresent="+(actualBody!=null)+" bodyEnabled="+actualBody?.Enabled+" bodyActive="+actualBody?.ActorActive+
                                " bodyProgress="+actualBody?.IndependentlyProgressing+" bodyRemaining="+actualBody?.RemainingSeconds+
                                " leaseCurrent="+lease.IsCurrent+" visitState="+Read(visit,"State")+" snapshot="+actualSnapshot.Version+"/"+actualSnapshot.BodyVersion);
                            records.Add(Source("candidate",key));records.Add(Source("predecessor",predecessorKey));records.Add(Source("return",returnKey));
                        }
                        catch(Exception error){errors.Add("Collection disable witness: "+error);}
                    }
                    var originalPredecessorKey=(TrafficIntentKey)Read(Read(record,"Pending"),"Intent");
                    var originalPredecessor=entries[originalPredecessorKey];var originalHandoff=Read(originalPredecessor,"PendingHandoff");
                    Assert.That(originalHandoff,Is.Not.Null);var originalHandoffRemaining=(float)Read(originalHandoff,"Remaining");
                    ObserveDisable("before");
                    // Actor disable is an availability hold; Agent disable leaves the actual Actor/combo identity active.
                    // 两种真实 component fault 分别验证暂停账本与正常 Core 推进，不强行打开业务 gate。
                    if(faultAgent)returnActor.Agent.enabled=false;
                    else returnActor.enabled=false;
                    ObserveDisable("sync-after");
                    records.Add("COLLECTION_RELEASE_FAULT component="+(faultAgent?"NavMeshAgent":"NavigationActor")+" frame="+Time.frameCount);
                    if(!faultAgent)
                    {
                        for(var frame=0;frame<10;frame++)
                        {
                            yield return null;
                            if(normalWitnesses<3)ObserveDisable("hold-normal-"+(++normalWitnesses));
                            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Normal));Assert.That(time.IsResumeBlocked,Is.False);
                            Assert.That(owner.CanProgress,Is.False);Assert.That(Read(owner,"comboReady"),Is.EqualTo(false));Assert.That(Read(core,"paused"),Is.EqualTo(true));
                            Assert.That(returnActor.isActiveAndEnabled,Is.False);Assert.That(Read(visit,"State"),Is.EqualTo(CustomerVisitState.PickupWaiting));
                            Assert.That(entries[key],Is.SameAs(entry));Assert.That(Read(entry,"Queued"),Is.EqualTo(true));
                            var heldDecision=(TrafficDecision)Read(entry,"Decision");
                            Assert.That(heldDecision.Kind,Is.EqualTo(TrafficDecisionKind.WaitingForTraffic));Assert.That(heldDecision.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None));
                            Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(birth));Assert.That(((TrafficRequest)Read(entry,"Request")).RequestedFrame,Is.EqualTo(birth));
                            Assert.That(Read(entry,"RequestSequence"),Is.EqualTo(sequence));Assert.That(Read(entry,"AcquiredSequence"),Is.EqualTo(0L));
                            Assert.That(Read(entry,"CandidateCount"),Is.EqualTo(count));Assert.That(Read(entry,"CurrentIndex"),Is.EqualTo(index));Assert.That(Read(entry,"FiniteLimit"),Is.EqualTo(finite));
                            CollectionAssert.AreEquivalent(new[]{index},((IEnumerable)Read(entry,"AttemptedIndices")).Cast<int>().ToArray());
                            Assert.That(Read(entry,"WaitRemaining"),Is.EqualTo(beforeWait));Assert.That(Read(entry,"Queries"),Is.EqualTo(beforeQueries));
                            Assert.That(Read(entry,"FrozenOwners"),Is.SameAs(frozenOwners));Assert.That(Read(entry,"FrozenResources"),Is.SameAs(frozenResources));
                            Assert.That(entries[originalPredecessorKey],Is.SameAs(originalPredecessor));Assert.That(Read(originalPredecessor,"PendingHandoff"),Is.SameAs(originalHandoff));
                            Assert.That(Read(originalHandoff,"Remaining"),Is.EqualTo(originalHandoffRemaining));
                            CollectionAssert.AreEquivalent(customerKeys,entries.Keys.Cast<TrafficIntentKey>().Where(k=>k.ActorId==id).ToArray());
                            Assert.That(Read(record,"CurrentPreview"),Is.Null);Assert.That(commits,Is.EqualTo(0));
                            Assert.That(claim.IsCurrent,Is.True);Assert.That(claim.Assignment,Is.EqualTo(originalAssignment));Assert.That(lease.IsCurrent,Is.True);
                            Assert.That(returnActor.transform.position,Is.EqualTo(originalPosition));Assert.That(world.RegisteredActors.Contains(returnActor),Is.True);
                            var heldBody=((TrafficWorldSnapshot)Read(core,"snapshot")).Bodies.Single(b=>ReferenceEquals(b.ActorLife,returnActor));
                            Assert.That(heldBody.Enabled,Is.False);Assert.That(heldBody.IndependentlyProgressing,Is.False);
                            Assert.That(customers.Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,total) && r.State==ReservationState.Occupied),Is.True);
                            Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.ReadyForPickup));
                        }
                        Assert.That(errors,Is.Empty);records.Add("COLLECTION_RELEASE_HOLD originalKey="+key+" first="+birth+" seq="+sequence+" wait="+beforeWait+" handoff="+originalHandoffRemaining+" commits="+commits);
                        yield break;
                    }
                    yield return Until(()=>
                    {
                        Assert.That(owner.CanProgress,Is.True,"PREMISE: real Agent fault must leave the original business combo gate open");
                        Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Normal));Assert.That(time.IsResumeBlocked,Is.False);
                        Assert.That(returnActor.isActiveAndEnabled,Is.True);Assert.That(returnActor.Agent.enabled,Is.False);
                        if(Time.frameCount>disableFrame)
                        {
                            Assert.That(Read(core,"paused"),Is.EqualTo(false));
                            var faultBody=((TrafficWorldSnapshot)Read(core,"snapshot")).Bodies.Single(b=>ReferenceEquals(b.ActorLife,returnActor));
                            Assert.That(faultBody.Enabled,Is.False);Assert.That(faultBody.ActorActive,Is.True);
                        }
                        if(disableWitness && normalWitnesses<3 && Time.frameCount>disableFrame && Time.frameCount!=lastWitnessFrame)
                        {lastWitnessFrame=Time.frameCount;ObserveDisable("normal-"+(++normalWitnesses));}
                        return Read(visit,"State").Equals(CustomerVisitState.Blocked);
                    });
                    var stopped=(TrafficDecision)Read(entry,"Decision");
                    Assert.That(stopped.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.Terminal),"Actual finite Core stop must precede explicit paid-visit Blocked");
                    Assert.That(commits,Is.EqualTo(0));Assert.That(Read(entry,"AcquiredSequence"),Is.EqualTo(0L));
                    Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(birth));Assert.That(Read(entry,"RequestSequence"),Is.EqualTo(sequence));
                    Assert.That(Read(entry,"CandidateCount"),Is.EqualTo(count));Assert.That(Read(entry,"CurrentIndex"),Is.EqualTo(index));
                    CollectionAssert.AreEquivalent(new[]{index},((IEnumerable)Read(entry,"AttemptedIndices")).Cast<int>().ToArray());
                    Assert.That((float)Read(entry,"WaitRemaining"),Is.LessThanOrEqualTo(beforeWait));Assert.That((int)Read(entry,"Queries"),Is.GreaterThanOrEqualTo(beforeQueries));
                    Assert.That((int)Read(entry,"Queries"),Is.LessThanOrEqualTo((int)Read(core,"queriesPerIntent")));
                    Assert.That(Read(record,"CurrentPreview"),Is.Null);Assert.That(claim.IsCurrent,Is.True);Assert.That(claim.Assignment,Is.EqualTo(originalAssignment));
                    Assert.That(lease.IsCurrent,Is.True);Assert.That(returnActor.transform.position,Is.EqualTo(originalPosition));Assert.That(world.RegisteredActors.Contains(returnActor),Is.True);
                    Assert.That(customers.Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,total) && r.State==ReservationState.Occupied),Is.True);
                    Assert.That(owner.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.ReadyForPickup));
                    for(var frame=0;frame<10;frame++)
                    {
                        yield return null;Assert.That(Read(visit,"State"),Is.EqualTo(CustomerVisitState.Blocked));
                        Assert.That(Read(entry,"Decision"),Is.SameAs(stopped));Assert.That(Read(entry,"RequestSequence"),Is.EqualTo(sequence));
                        CollectionAssert.AreEquivalent(customerKeys,entries.Keys.Cast<TrafficIntentKey>().Where(k=>k.ActorId==id).ToArray());
                        Assert.That(commits,Is.EqualTo(0));Assert.That(lease.IsCurrent && claim.IsCurrent,Is.True);
                    }
                    Assert.That(errors,Is.Empty);records.Add("COLLECTION_RELEASE_TERMINAL actualBlocked=True originalKey="+key+" first="+birth+" seq="+sequence+" reason="+stopped.Reason);
                    yield break;
                }
                ObserveLedger();yield return Until(()=>{ObserveLedger();return owner.Snapshot.Orders.Values.Single().State==OrderState.Completed;});
                Assert.That(errors,Is.Empty);Assert.That(released && publishedValid,Is.True,"Real lease retirement must precede exactly one preview-backed commit with original accounting");Assert.That(commits,Is.EqualTo(1));
                if(!waitSeen)Assert.Ignore("NOT_PROVEN: actual parking-edge finite WAIT was not observed; completion alone cannot prove the bridge");
                if(semanticWitness)
                {
                    Assert.That(errors,Is.Empty);Assert.That(releaseNotified,Is.True,"PRODUCT: actual original lease lifetime retirement must notify its original parking resource");
                    Assert.That(releaseQueued && releaseWake,Is.True,"Actual original Notify must queue/wake the same waiting intent");
                    Assert.That(releaseFirst,Is.EqualTo(birth));Assert.That(releaseSequence,Is.EqualTo(sequence));
                }
                world.Unregister(block);block.gameObject.SetActive(false);UnityEngine.Object.Destroy(block.gameObject);
                yield return Until(()=>customers.Capacity.GetCapacities().All(c=>c.Used==0) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            }
            finally{collectionReleaseWitnessCleanup?.Invoke();}
        }

        [UnityTest] public IEnumerator PickupAdvanceAndServiceProposalsPreserveCanonicalOwnerBeforeCommit()
            => TwoPaidWaitersProgressAfterRealHeadExit(true);
        [UnityTest] public IEnumerator TwoPaidWaitersActuallyExitAfterHeadReleasesPickup()
            => TwoPaidWaitersProgressAfterRealHeadExit(false);
        [UnityTest] public IEnumerator RealAdvanceWinnerRetiresUnselectedPreparedServiceSibling()
            => TwoPaidWaitersProgressAfterRealHeadExit(false,true);

        [UnityTest] public IEnumerator SingleSideDeliveryDeclaresOriginalBoundedYieldOption()
            => OriginalPlannedYield(false);
        [UnityTest] public IEnumerator SingleSideDeliveryWaitsForActualYieldArrivalBeforeReservedOption()
            => OriginalPlannedYield(true);

        private IEnumerator OriginalPlannedYield(bool followChild)
        {
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            object Read(object value,string name)=>value==null?null:MovementRead(value,name);
            // The proposed private plan is absent on M1. Absence is data, never a reflection exception.
            // M1 不创建 seal；只读取真实 producer 已挂载的 plan，缺失时返回 null。
            object Optional(object value,string name)=>value==null?null:
                value.GetType().GetField(name,flags)?.GetValue(value)??value.GetType().GetProperty(name,flags)?.GetValue(value);
            var core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);var previous=hook.GetValue(customers);
            var errors=new List<string>();var failures=new List<string>();
            var sampled=false;var natural=false;var headerSeen=false;var childSeen=false;var pendingSeen=false;
            var arrivedSeen=false;var committed=false;var stopped=false;var advances=0;var advanceAccepted=false;
            object cursor=null,plan=null,visit=null,entry=null,childEntry=null,context=null;NavigationActor employee=null,customer=null;
            PickupQueueService queue=null;PickupQueueService.ReservationClaim originalClaim=null;QueueAssignment originalRow=default;
            TrafficIntentKey parentKey=default,childKey=default;var originalFrame=0;var baseCount=0;var declaredCount=0;
            var headerCount=0;long headerSequence=0,originalSequence=0;var lastQueries=0;var initialFacts="NOT_OBSERVED";
            object[] waitingLedger=null;object[] beforeAdvance=null;var birthFrame=0;
            string Key(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
            void Check(bool condition,string why){if(!condition && !failures.Contains(why))failures.Add(why);}
            IDictionary Entries()=>(IDictionary)Read(core,"entries");
            void ObserveAssignedSequence()
            {
                var sequence=(long)Read(entry,"RequestSequence");
                // The queued header starts at 0; retain the first actual positive assignment exactly once.
                // 首次真实分配不是 reset；之后改变正值或回到0必须失败。
                if(originalSequence==0 && sequence>0)originalSequence=sequence;
                Check(sequence==originalSequence,"The first assigned parent sequence cannot change or return to zero");
            }
            object[] Ledger()=>new[]{Read(entry,"FirstFrame"),Read(entry,"RequestSequence"),Read(entry,"AcquiredSequence"),
                Read(entry,"Queries"),Read(entry,"FiniteLimit"),Read(entry,"WaitRemaining"),Read(entry,"FrozenOwners"),
                Read(entry,"FrozenResources"),Read(entry,"WaitingBlockers")};
            bool SameLedger(object[] a,object[] b)=>a!=null && b!=null && a.Length==b.Length && a.Zip(b,(x,y)=>Equals(x,y)).All(v=>v);
            var stopWitnessEnabled=followChild && Environment.GetCommandLineArgs().Contains("-trafficPlannedYieldStoppedWitness");
            var stopWitnessObserved=false;
            void ObserveStoppedYield()
            {
                if(!stopWitnessEnabled || stopWitnessObserved)return;stopWitnessObserved=true;
                try
                {
                    string Ref(object value)=>value==null?"NULL":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value).ToString();
                    string Fields(object value,params string[] names)=>value==null?"UNKNOWN(no-retained-object)":
                        string.Join(";",names.Select(name=>name+"="+(Optional(value,name)??"NULL_OR_UNAVAILABLE")));
                    string Row(QueueAssignment value)=>value.VisitId+"/g"+value.Generation+"/slot"+value.SlotIndex+"/arrived="+value.Arrived+"/"+value.Position.ToString("F9");
                    void Snapshot(string tag,TrafficWorldSnapshot snapshot)
                    {
                        if(snapshot==null){TestContext.WriteLine(tag+"=UNKNOWN(no-retained-snapshot)");return;}
                        TestContext.WriteLine(tag+" ref="+Ref(snapshot)+" version="+snapshot.Version+" bodyVersion="+snapshot.BodyVersion+
                            " bodies=["+string.Join(";",snapshot.Bodies.Select(body=>Key(body.Owner)+"/life="+Ref(body.ActorLife)+"/pose="+body.Position.ToString("F9")+
                                "/r="+body.Radius+"/s="+body.SafetyMargin+"/enabled="+body.Enabled+"/active="+body.ActorActive+"/progress="+body.IndependentlyProgressing))+ "]"+
                            " parking=["+string.Join(";",snapshot.Parking.Select(claim=>Key(claim.Owner)+"/owner="+Ref(claim.CanonicalOwner)+"/v="+claim.OwnerVersion+
                                "/business="+Ref(claim.BusinessOwner)+"/release="+Ref(claim.ReleaseObligation)+"/target="+claim.Position.ToString("F9")))+"]");
                    }
                    var map=Entries();var actual=childSeen && map.Contains(childKey)?map[childKey]:null;
                    var request=Optional(actual,"Request") as TrafficRequest;var source=Optional(actual,"Decision") as TrafficDecision;
                    var plannedChild=Optional(plan,"Child");var plannedSource=Optional(plan,"ChildSource") as TrafficDecision;
                    var retained=Optional(actual,"JourneyPermit") as TrafficDecision;var leg=Optional(actual,"Leg");
                    var originalPlanClaim=Optional(plan,"Claim") as PickupQueueService.ReservationClaim;
                    var yieldedClaim=Optional(plan,"YieldClaim") as PickupQueueService.ReservationClaim;
                    var rowFound=queue.TryGetReservation(originalRow.VisitId,out var currentRow);
                    var claimFound=queue.TryCaptureReservation(originalRow.VisitId,out var currentClaim);
                    var currentSnapshot=Read(customers,"trafficSnapshot") as TrafficWorldSnapshot;
                    var coreSnapshot=Read(core,"snapshot") as TrafficWorldSnapshot;
                    TestContext.WriteLine("PLANNED_YIELD_STOP status=OBSERVED cap=1 frame="+Time.frameCount+
                        " timing=after-product-stop-before-controlled-cleanup originalFailBranch=UNKNOWN originalNativeResult=UNKNOWN"+
                        " parent="+Key(parentKey)+" child="+(childSeen?Key(childKey):"NOT_OBSERVED")+" core="+Ref(core)+
                        " currentCoreSame="+ReferenceEquals(Read(customers,"traffic"),core)+" snapshotRefSame="+ReferenceEquals(currentSnapshot,coreSnapshot)+
                        " childEntry="+Ref(actual)+" previousEntrySame="+ReferenceEquals(actual,childEntry));
                    TestContext.WriteLine("YIELD_STOP_CHILD kind="+source?.Kind+" disposition="+source?.FailureDisposition+" reason="+source?.Reason+" "+
                        Fields(actual,"FirstFrame","Queries","RequestSequence","AcquiredSequence","CurrentIndex","CandidateCount","JobIndex","Queued","Prepared","Active","Retired","Discarded","EverCanonicalAttempt","FiniteLimit","WaitRemaining")+
                        " attempted=["+string.Join(",",(Optional(actual,"AttemptedIndices") as IEnumerable)?.Cast<object>()??Enumerable.Empty<object>())+"]"+
                        " jobs="+(Optional(actual,"Jobs") as IList)?.Count+" leg="+Ref(leg)+" "+Fields(leg,"Committed","Bound","Completed","Generation","Stamp")+
                        " journey="+Ref(retained)+" planSource="+Ref(plannedSource)+" planSourceSameJourney="+ReferenceEquals(plannedSource,retained));
                    TestContext.WriteLine("YIELD_STOP_REQUEST requested="+request?.RequestedFrame+" start="+request?.Start.ToString("F9")+" target="+request?.Target.ToString("F9")+
                        " primary="+request?.PlanPrimaryRoute+" resource="+request?.ResourceId+" native="+request?.NativeStamp.RequestId+
                        " requirements=["+(request==null?"UNKNOWN":string.Join(";",request.Requirements.Select(proof=>proof.ResourceId+"/"+proof.Start.ToString("F9")+"->"+proof.Goal.ToString("F9")+"/selected="+proof.SelectedJourney)))+"]");
                    Snapshot("YIELD_STOP_ORIGINAL_REQUEST",request?.Snapshot);Snapshot("YIELD_STOP_CURRENT_CFC",currentSnapshot);
                    if(!ReferenceEquals(currentSnapshot,coreSnapshot))Snapshot("YIELD_STOP_CURRENT_CORE",coreSnapshot);
                    TestContext.WriteLine("YIELD_STOP_PLAN "+Fields(plan,"Generation","PhaseSequence","FirstFrame","YieldGeneration","Radius","Skin","Epsilon")+
                        " childOrigin="+Optional(plannedChild,"Origin")+" childIndex="+Optional(plannedChild,"CurrentIndex")+" originalClaimCurrent="+originalPlanClaim?.IsCurrent+
                        " yieldedClaimCurrent="+yieldedClaim?.IsCurrent+" sameOriginalLineage="+(claimFound && originalPlanClaim!=null && originalPlanClaim.IsSameReservation(currentClaim))+
                        " originalRow="+Row(originalRow)+" actualRow="+(rowFound?Row(currentRow):"MISSING")+" actualClaimCurrent="+(claimFound && currentClaim.IsCurrent)+
                        " contextSame="+ReferenceEquals(context,Optional(plan,"Context"))+" contextCurrentNow="+Optional(context,"IsCurrent")+
                        " contextBeforeOriginalFailure=UNKNOWN(post-Blocked-may-invalidate)");
                    TestContext.WriteLine("YIELD_STOP_VISIT "+Fields(visit,"State","Generation","TrafficPhaseSequence","DeliveryYieldPhase","RequestId","PendingPickupSegment","TrafficMovePending","PickupAssigned","PickupDeparted","HasCollected","Failure")+
                        " actualActorSame="+ReferenceEquals(Read(visit,"Actor"),customer)+" pose="+customer.transform.position.ToString("F9")+
                        " ownerSame="+ReferenceEquals(Read(visit,"PickupOwner"),queue)+" queueVersion="+queue.Version+" admissionSame="+ReferenceEquals(Read(visit,"Admission"),Optional(plan,"Admission"))+
                        " yieldedSourceSameParent="+ReferenceEquals(Read(visit,"YieldDeliverySource"),cursor)+" move="+Ref(Read(visit,"TrafficMove"))+
                        " moveSource="+Ref(Optional(Read(visit,"TrafficMove"),"Source"))+" total=["+string.Join(";",customers.Capacity.GetReservations().Where(r=>r.OwnerId==originalRow.VisitId).Select(r=>r.Kind+"/"+r.State+"/"+Ref(r.Token)))+"]");
                    TestContext.WriteLine("YIELD_STOP_PARENT "+Fields(entry,"FirstFrame","RequestSequence","AcquiredSequence","Queries","CurrentIndex","CandidateCount","Queued","Retired")+
                        " decision="+Fields(Read(entry,"Decision"),"Kind","FailureDisposition","Reason")+" staff=["+
                        string.Join(";",employees.Snapshot.Select(staff=>staff.ActorId+"/"+staff.Role+"/"+staff.State+"/"+staff.Failure))+"] errors=NONE");
                }
                catch(Exception error){errors.Add("PLANNED_YIELD_STOP observation error: "+error);}
            }

            bool ActualChildArrived()
            {
                if(!childSeen || !Entries().Contains(childKey) || !queue.TryGetReservation((string)Read(visit,"Id"),out var row) ||
                    !queue.TryCaptureReservation(row.VisitId,out var claim))return false;
                var actualChild=Entries()[childKey];var primary=Read(actualChild,"JourneyRequest") as TrafficRequest;
                var permit=Read(actualChild,"JourneyPermit") as TrafficDecision;var leg=Read(actualChild,"Leg");
                return ReferenceEquals(actualChild,childEntry) && permit!=null && permit.Intent.Equals(childKey) && permit.AcquiredSequence>0 &&
                    primary!=null && (int)Read(visit,"DeliveryYieldPhase")==2 && row.Arrived && claim.IsCurrent &&
                    claim.IsSameReservation(originalClaim) && !originalClaim.IsCurrent && row.Generation!=originalRow.Generation && row.Position==primary.Target &&
                    ReferenceEquals(Read(visit,"YieldDeliverySource"),cursor) && (long)Read(visit,"RequestId")==0 &&
                    (leg==null || (bool)Read(leg,"Completed")) && !((IDictionary)Read(world.Service,"byActor")).Contains(originalRow.VisitId) &&
                    ReferenceEquals(Read(visit,"Actor"),customer) && Vector3.Distance(customer.transform.position,row.Position)<=customer.Settings.ArrivalDistance+.001f;
            }
            void Observe(string operation,string id,string related,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                if(errors.Count!=0)return;
                try
                {
                    if(!sampled && operation=="traffic.candidate-preflight" && result!=null && result.Contains("role=Delivery"))
                    {
                        sampled=true;var cursors=(IDictionary)Read(customers,"trafficCandidates");
                        parentKey=cursors.Keys.Cast<TrafficIntentKey>().Single(k=>k.IntentId=="employee-pickup-journey" && Read(cursors[k],"Source")==null);
                        cursor=cursors[parentKey];context=Read(cursor,"DeliveryContext");plan=Optional(cursor,"OriginalDeliveryPlan");
                        employee=(NavigationActor)Read(cursor,"ActorLife");queue=(PickupQueueService)Read(customers,"pickupQueue");
                        var rows=queue.Snapshot.Where(r=>r.Arrived).ToArray();
                        originalFrame=Time.frameCount;baseCount=((IEnumerable)Read(cursor,"Sides")).Cast<object>().Count();
                        declaredCount=(int)Optional(cursor,"Count");
                        if(rows.Length==1)
                        {
                            originalRow=rows[0];visit=((IDictionary)Read(customers,"visits"))[originalRow.VisitId];customer=(NavigationActor)Read(visit,"Actor");
                            var total=customers.Capacity.GetReservations().Single(r=>r.OwnerId==originalRow.VisitId && r.Kind==CapacityKind.TotalCustomers);
                            var paid=Read(visit,"PaidPickup");var staff=employees.Snapshot.Single(e=>e.ActorId==parentKey.ActorId);
                            natural=baseCount==1 && world.RegisteredActors.Count()==14 && queue.TryCaptureReservation(originalRow.VisitId,out originalClaim) &&
                                originalClaim.IsCurrent && paid!=null && (bool)Read(paid,"Transferred") && total.State==ReservationState.Occupied &&
                                Read(visit,"Admission")!=null && ReferenceEquals(Read(visit,"PickupOwner"),queue) &&
                                (CustomerVisitState)Read(visit,"State")==CustomerVisitState.PickupWaiting && (int)Read(visit,"DeliveryYieldPhase")==0 &&
                                customer.isActiveAndEnabled && world.RegisteredActors.Any(a=>ReferenceEquals(a,customer)) &&
                                Vector3.Distance(customer.transform.position,originalRow.Position)<=customer.Settings.ArrivalDistance+.001f &&
                                staff.State==EmployeeState.WaitingToDeliver && context!=null && (bool)Optional(context,"IsCurrent") &&
                                ReferenceEquals(Optional(context,"ActorLife"),employee) && Read(cursor,"Source")==null &&
                                !Entries().Contains(parentKey) && !queue.TryGetServiceLease(parentKey.ActorId,out _) &&
                                !((IDictionary)Read(world.Service,"byActor")).Contains(parentKey.ActorId);
                        }
                        initialFacts="frame="+originalFrame+" parent="+Key(parentKey)+" base="+baseCount+" declared="+declaredCount+
                            " planPresent="+(plan!=null)+" settledRows="+rows.Length+" bodies="+world.RegisteredActors.Count()+
                            " row="+originalRow.Generation+"/"+originalRow.SlotIndex+"/arrived="+originalRow.Arrived+" firstSubmit="+!Entries().Contains(parentKey)+
                            " claimCurrent="+(originalClaim?.IsCurrent==true)+" contextCurrent="+Optional(context,"IsCurrent")+
                            " leaseNone="+!queue.TryGetServiceLease(parentKey.ActorId,out _)+" coreAcquired=NOT_APPLICABLE(unsubmitted)";
                    }
                    if(!followChild || !sampled || !natural)return;
                    if(!childSeen && operation=="traffic.pickup-family-preflight" && id==originalRow.VisitId &&
                        result!=null && result.Contains("operation=BeginYield;"))
                    {
                        childSeen=true;var keys=(IDictionary)Read(customers,"trafficKeys");
                        var phase=(int)Read(visit,"TrafficPhaseSequence");
                        childKey=keys.Values.Cast<TrafficIntentKey>().Single(k=>k.ActorId==id && k.IntentId=="pickup-yield" && k.Context==id+"/"+phase);
                        var frames=(IDictionary)Read(visit,"TrafficCandidateFrames");
                        birthFrame=frames.Contains(childKey)?(int)frames[childKey]:Time.frameCount;
                        var preparation=typeof(CustomerFlowController).GetMethod("FindPickupPreparation",flags).Invoke(customers,new[]{visit,(object)childKey});
                        Check(preparation!=null && ReferenceEquals(Read(preparation,"Visit"),visit) &&
                            ReferenceEquals(Read(preparation,"DeliveryCursor"),cursor),"Child must originate from the original actual Visit and delivery cursor");
                        Check(!Entries().Contains(childKey),"Original child source must be observed before its first Submit");
                        Check(Entries().Contains(parentKey),"Parent must already be submitted before child preparation");
                        entry=Entries()[parentKey];ObserveAssignedSequence();
                        Check(originalSequence>0,"Actual child preparation requires the parent's assigned positive source sequence");waitingLedger=Ledger();
                        Check(((TrafficDecision)Read(entry,"Decision")).FailureDisposition==TrafficFailureDisposition.AlternateAllowed,
                            "The child strategy requires the original parent's current AlternateAllowed source");
                    }
                    if(operation=="traffic.candidate-before-advance" && result!=null && result.Contains("role=Delivery") && result.Contains("key="+Key(parentKey)+" "))
                    {
                        advances++;entry=Entries()[parentKey];arrivedSeen=ActualChildArrived();
                        Check(arrivedSeen,"Reserved parent option cannot advance before actual child phase2, arrival and new held claim");
                        Check(advances==1,"The original reserved strategy can be advanced only once");
                        Check(result.Contains("nextIndex="+baseCount+" "),"The one declared reserved index must follow original base sides");
                        Check(SameLedger(waitingLedger,Ledger()),"Parent age, sequences, queries, wait and frozen/current edges must survive the child prerequisite");
                        beforeAdvance=Ledger();
                    }
                }
                catch(Exception error){errors.Add(error.ToString());}
            }
            bool Checkpoint()
            {
                if(errors.Count!=0 || failures.Count!=0)return true;
                if(!sampled)return false;
                try
                {
                    if(!Entries().Contains(parentKey)){Check(false,"Original preflight must publish its actual Core header");return true;}
                    var actualEntry=Entries()[parentKey];
                    if(!headerSeen)
                    {
                        headerSeen=true;entry=actualEntry;headerCount=(int)Read(entry,"CandidateCount");headerSequence=(long)Read(entry,"RequestSequence");
                        lastQueries=(int)Read(entry,"Queries");
                        Check((int)Read(entry,"FirstFrame")==originalFrame && ((TrafficRequest)Read(entry,"Request")).RequestedFrame==originalFrame,
                            "Original parent logical birth must survive its first Submit");
                    }
                    Check(ReferenceEquals(entry,actualEntry),"Parent entry cannot be replaced or rekeyed");
                    Check((int)Read(entry,"FirstFrame")==originalFrame,"Parent birth cannot reset");ObserveAssignedSequence();
                    Check((int)Read(entry,"Queries")>=lastQueries,"Parent spent query ledger cannot reset");lastQueries=(int)Read(entry,"Queries");
                    if(!followChild)return true;
                    var decision=(TrafficDecision)Read(entry,"Decision");
                    if(childSeen && Entries().Contains(childKey))
                    {
                        var found=Entries()[childKey];if(childEntry==null)childEntry=found;
                        Check(ReferenceEquals(childEntry,found) && (int)Read(found,"FirstFrame")==birthFrame,"Child identity/birth cannot reset");
                        var phase=(int)Read(visit,"DeliveryYieldPhase");var childDecision=(TrafficDecision)Read(found,"Decision");
                        var queued=(bool)Read(found,"Queued") && childDecision.AcquiredSequence==0;
                        var request=(long)Read(visit,"RequestId");var leg=Read(found,"Leg");var native=(IDictionary)Read(world.Service,"byActor");
                        var moving=phase==1 && request>0 && leg!=null && (bool)Read(leg,"Committed") && (bool)Read(leg,"Bound") &&
                            !(bool)Read(leg,"Completed") && ReferenceEquals(Read(leg,"ActorLife"),customer) &&
                            ((TrafficNativeStamp)Read(leg,"Stamp")).RequestId==request && native.Contains(originalRow.VisitId) &&
                            (long)Read(native[originalRow.VisitId],"Id")==request;
                        if(queued || moving)
                        {
                            pendingSeen=true;
                            Check((int)Read(entry,"CurrentIndex")<baseCount && !((IEnumerable)Read(entry,"AttemptedIndices")).Cast<int>().Contains(baseCount),
                                "Pending or moving child cannot consume the reserved parent option");
                            Check((long)Read(entry,"AcquiredSequence")==0 && Read(entry,"JourneyPermit")==null && Read(entry,"Leg")==null &&
                                !queue.TryGetServiceLease(parentKey.ActorId,out _) && !((IDictionary)Read(world.Service,"byActor")).Contains(parentKey.ActorId),
                                "Pending or moving child cannot acquire a staff lease or issue a staff Native leg");
                            Check(SameLedger(waitingLedger,Ledger()),"Parent ledger must remain unchanged while awaiting its actual child");
                        }
                        if(ActualChildArrived())arrivedSeen=true;
                    }
                    if(advances>0)
                    {
                        advanceAccepted=(int)Read(entry,"CurrentIndex")==baseCount && ((IEnumerable)Read(entry,"AttemptedIndices")).Cast<int>().Contains(baseCount);
                        Check(advanceAccepted,"The witnessed reserved advance must consume that original index");
                        Check((int)Read(entry,"Queries")>=(int)beforeAdvance[3] && Equals(Read(entry,"FiniteLimit"),beforeAdvance[4]),
                            "Reproof must retain spent queries and original finite limit");
                    }
                    var primary=Read(entry,"JourneyRequest") as TrafficRequest;var permit=Read(entry,"JourneyPermit") as TrafficDecision;
                    if(permit!=null && permit.AcquiredSequence>0)
                    {
                        committed=true;
                        Check(arrivedSeen && advances==1 && advanceAccepted,"Canonical parent delivery requires its one actual arrived-child strategy");
                        Check(primary!=null && primary.Intent.Equals(parentKey) && primary.RequestedFrame==originalFrame && permit.RequestSequence==originalSequence,
                            "The original primary key, birth and sequence must survive canonical commit");
                        Check(queue.TryGetServiceLease(parentKey.ActorId,out _),"Actual delivery commit must publish its real lease");
                        return true;
                    }
                    stopped=decision.FailureDisposition==TrafficFailureDisposition.Terminal ||
                        employees.Snapshot.Single(e=>e.ActorId==parentKey.ActorId).State==EmployeeState.Blocked;
                    if(stopped && stopWitnessEnabled)ObserveStoppedYield();
                    return stopped || failures.Count!=0;
                }
                catch(Exception error){errors.Add(error.ToString());return true;}
            }
            try
            {
                hook.SetValue(customers,Delegate.Combine(previous as Delegate,
                    (Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)Observe));
                yield return ActualPaidYield(false,preparationObserved:Checkpoint);
                TestContext.WriteLine("ORIGINAL_YIELD_PLAN naturalPremise="+natural+" "+initialFacts+" headerCount="+headerCount+
                    " requested="+originalFrame+" rawHeaderSequence="+headerSequence+" requestSequence="+originalSequence+" queries="+lastQueries+" child="+(childSeen?Key(childKey):"NOT_OBSERVED")+
                    " childBirth="+birthFrame+" pending="+pendingSeen+" arrived="+arrivedSeen+" advances="+advances+" committed="+committed+" stopped="+stopped+
                    " deeperControl="+(committed?"OBSERVED":"NOT_PROVEN"));
                Assert.That(errors,Is.Empty,"YIELD FIXTURE: read-only observer/checkpoint errors");
                Assert.That(sampled && natural && headerSeen,Is.True,"YIELD FIXTURE NOT_PROVEN: authentic settled paid waiter and first typed source/header required");
                Assert.That(failures,Is.Empty,"YIELD PRODUCT: source/async-prerequisite invariants");
                if(!followChild)
                {
                    Assert.That(headerCount,Is.EqualTo(baseCount+1),"YIELD FEATURE RED: original Core header must declare the bounded after-yield option before first Submit");
                    Assert.That(declaredCount,Is.EqualTo(baseCount+1),"The pre-Submit declaration and original Core count must agree");
                    Assert.That(plan,Is.Not.Null,"YIELD FEATURE: the actual producer must retain its original plan");
                }
                else
                {
                    if(plan==null && !childSeen && headerCount==baseCount)
                        Assert.Ignore("YIELD NOT_PROVEN: current original plan has no reserved option; no authentic child pending/arrival guard was exercised");
                    Assert.That(headerCount,Is.EqualTo(baseCount+1),"Declared count cannot be appended after original failure");
                    Assert.That(stopped,Is.False,"Actual original child strategy failed; explicit recovery remains separate");
                    Assert.That(childSeen && pendingSeen && arrivedSeen && committed && advances==1,Is.True,
                        "YIELD NOT_PROVEN/FAIL: real pending child, arrived phase2 and exactly one original parent commit are required");
                }
            }
            finally{hook.SetValue(customers,previous);}
        }

        [UnityTest] public IEnumerator CollectionPreparationRefreshesAfterRealQueuedCashierCompletion()=>CollectionPreparationLifecycle("metadata");
        [UnityTest] public IEnumerator CollectionPreparationRetainsIdentityAcrossSameOwnerAvailability()=>CollectionPreparationLifecycle("availability");
        [UnityTest] public IEnumerator CollectionPreparationRejectsRealDecorationOwnerReplacement()=>CollectionPreparationLifecycle("replacement");
        [UnityTest] public IEnumerator CollectionPreparationCannotReuseActualConsumedPreview()=>CollectionPreparationLifecycle("consumed");

        [UnityTest] public IEnumerator CurrentPredecessorAllowsCanonicalCollectionAndActualNativeArrival()=>CollectionPreparationLifecycle("predecessor-valid");
        [UnityTest] public IEnumerator ExpiredPredecessorBlocksBeforeCanonicalCollectionApply()=>CollectionPreparationLifecycle("predecessor-expired");
        private Action collectionPreparationWitnessCleanup;
        // NUnit TearDown also runs when Unity abandons the parent iterator after a nested failure.
        // 这里只恢复hook/输出已复制事实；不查询scene，也不让观察异常阻止原scene/time/input清理。
        [TearDown] public void CleanupCollectionPreparationWitness()=>collectionPreparationWitnessCleanup?.Invoke();

        private IEnumerator CollectionPreparationLifecycle(string mode)
        {
            object Read(object value,string name)=>value==null?null:MovementRead(value,name);
            string Ref(object value)=>value==null?"NULL":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value).ToString();
            string Key(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
            string Items(object values)=>values is IEnumerable list?string.Join(",",list.Cast<object>().Select(value=>value?.ToString())):"NULL";
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var errors=new List<string>();var records=new List<string>();var finished=false;
            var core=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var flow=customers;var entries=(IDictionary)Read(core,"entries");
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);
            var snapshotHook=typeof(ServiceTrafficCoordinator).GetField("SnapshotChangedObserver",flags);
            var previous=hook.GetValue(flow);var previousSnapshot=snapshotHook.GetValue(core);
            var resolve=typeof(CustomerFlowController).GetMethod("FindPickupPreparation",flags);
            var currentMethod=typeof(CustomerFlowController).GetMethod("PickupFamilyPreparationCurrent",flags);
            var first="";object visit=null,preparation=null,entry=null,admission=null,total=null;
            NavigationActor actor=null;PickupQueueService queue=null;
            PickupQueueService.ReservationClaim claim=null;PickupQueueService.PreparedMutation preview=null;
            TrafficIntentKey key=default;QueueAssignment row=default;Vector3 pose=default;
            var sampled=false;var natural=false;var bound=false;var originalCurrent=false;var failedSample=false;var failureCurrent=false;
            var birth=-1;var generation=0;var phaseSequence=0;var candidate=-1;var count=0;var queries=0;
            var sequence=0L;var finiteLimit=0f;var priorWait=0f;var originalAttempted="";
            bool Current()=>preparation!=null && (bool)currentMethod.Invoke(flow,new[]{preparation});
            bool SameBusiness()=>visit!=null && ReferenceEquals(((IDictionary)Read(flow,"visits"))[first],visit) &&
                ReferenceEquals(Read(visit,"Actor"),actor) && ReferenceEquals(Read(visit,"Admission"),admission) &&
                ReferenceEquals(Read(flow,"pickupQueue"),queue) && ReferenceEquals(Read(visit,"PickupOwner"),queue) &&
                actor!=null && actor.transform.position==pose && claim.IsCurrent && queue.TryGetReservation(first,out var actual) && actual.Equals(row) && actual.Arrived &&
                (int)Read(visit,"Generation")==generation && (int)Read(visit,"TrafficPhaseSequence")==phaseSequence &&
                (int)Read(visit,"DeliveryYieldPhase")==0 && (CustomerVisitState)Read(visit,"State")==CustomerVisitState.PickupWaiting &&
                flow.Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,total) && r.State==ReservationState.Occupied);
            void Errors()=>Assert.That(errors,Is.Empty,"M2B observation UNKNOWN; errors are asserted outside product callbacks");
            var availabilityWitness=mode=="availability" && Environment.GetCommandLineArgs().Contains("-trafficPickupAvailabilityIdentityWitness");
            var availabilityStages=0;
            void AvailabilityFacts(int stage,string label)
            {
                if(!availabilityWitness || (availabilityStages&stage)!=0)return;availabilityStages|=stage;
                try
                {
                    var cursors=(IDictionary)Read(flow,"trafficCandidates");var actualCursor=cursors.Contains(key)?cursors[key]:null;
                    var liveVisits=(IDictionary)Read(flow,"visits");var liveVisit=liveVisits.Contains(first)?liveVisits[first]:null;
                    string Decision(object value)=>value is TrafficDecision decision?decision.Kind+"/"+decision.FailureDisposition+"/"+decision.Reason:"NULL";
                    void Pair(string name,object actual)
                    {
                        var original=Read(preparation,name);
                        records.Add("M2B_AVAILABILITY_REF stage="+label+" field="+name+" original="+Ref(original)+" actual="+Ref(actual)+" same="+ReferenceEquals(original,actual));
                    }
                    records.Add("M2B_AVAILABILITY_STAGE capCycle=1 stage="+label+" frame="+Time.frameCount+" key="+Key(key)+
                        " enabled="+flow.isActiveAndEnabled+" disposed="+Read(flow,"disposed")+" held="+Read(flow,"integratedHeld")+
                        " suspended="+Read(flow,"suspended")+" ownerLife="+Read(preparation,"OwnerLife")+"/"+Read(flow,"pickupPreparationLife")+
                        " session="+Read(flow,"trafficSession")+" epoch="+Read(flow,"trafficEpoch")+" adapterRevision="+((NavigationLayoutAdapter)Read(flow,"adapter")).Revision);
                    Pair("Flow",flow);Pair("Coordinator",Read(flow,"traffic"));Pair("World",Read(flow,"world"));Pair("Adapter",Read(flow,"adapter"));
                    Pair("Capacity",flow.Capacity);Pair("Owner",Read(flow,"pickupQueue"));Pair("Visit",liveVisit);Pair("Admission",Read(visit,"Admission"));Pair("Actor",Read(visit,"Actor"));
                    Pair("Selection",Read(visit,"TrafficSelection"));Pair("Issued",Read(visit,"IssuedTraffic"));Pair("Pending",Read(visit,"PendingTraffic"));Pair("Movement",Read(visit,"TrafficMove"));Pair("Cursor",actualCursor);
                    records.Add("M2B_AVAILABILITY_SOURCE stage="+label+" storedSeal="+Ref(Read(actualCursor,"PickupPreparation"))+" originalSeal="+Ref(preparation)+
                        " visitOwner="+Ref(Read(visit,"PickupOwner"))+" originalOwner="+Ref(queue)+" actorActive="+actor.isActiveAndEnabled+
                        " registered="+world.RegisteredActors.Any(a=>ReferenceEquals(a,actor))+" pose="+actor.transform.position+" originalStart="+Read(preparation,"Start")+
                        " settings="+actor.Settings.AgentRadius+"/"+actor.Settings.CollisionSkin+"/"+actor.Settings.Epsilon+
                        " originalSettings="+Read(preparation,"Radius")+"/"+Read(preparation,"Skin")+"/"+Read(preparation,"Epsilon"));
                    foreach(var names in new[]{new[]{"Phase","State"},new[]{"Generation","Generation"},new[]{"PhaseSequence","TrafficPhaseSequence"},new[]{"YieldPhase","DeliveryYieldPhase"}})
                        records.Add("M2B_AVAILABILITY_VALUE stage="+label+" field="+names[0]+" original="+Read(preparation,names[0])+" actual="+Read(visit,names[1]));
                    foreach(var name in new[]{"PickupAssigned","PickupDeparted","HasCollected","RequestId","PendingPickupSegment","TrafficMovePending"})
                        records.Add("M2B_AVAILABILITY_FLAG stage="+label+" field="+name+" value="+Read(visit,name));
                    records.Add("M2B_AVAILABILITY_PREVIEW stage="+label+" sameOriginal="+ReferenceEquals(Read(preparation,"Preview"),preview)+" owner="+Ref(Read(preview,"owner"))+
                        " consumed="+Read(preview,"Consumed")+" operation="+Read(preview,"Kind")+" id="+Read(preview,"Id")+" target="+preview.Assignment.Position+
                        " expectedVersion="+Read(preview,"ExpectedVersion")+" ownerVersion="+queue.Version+" heldClaimCurrent="+claim.IsCurrent+
                        " originalCandidate="+Read(preparation,"CandidateIndex")+" currentCandidate="+Read(actualCursor,"CurrentIndex")+" cursorSource="+Decision(Read(actualCursor,"Source"))+
                        " cursorOrigin="+Read(actualCursor,"Origin")+" cursorActor="+Ref(Read(actualCursor,"ActorLife"))+" cursorRole="+Read(actualCursor,"Role"));
                    var pending=Read(preparation,"Pending");var predecessor=pending==null?null:Read(pending,"Intent");
                    var previousEntry=predecessor!=null && entries.Contains(predecessor)?entries[predecessor]:null;
                    records.Add("M2B_AVAILABILITY_HANDOFF stage="+label+" originalPending="+Ref(pending)+" actualPending="+Ref(Read(visit,"PendingTraffic"))+
                        " marked="+Read(pending,"Marked")+" handle="+Ref(Read(pending,"Handle"))+" predecessor="+predecessor+
                        " predecessorDecision="+Decision(Read(previousEntry,"Decision"))+" retired="+Read(previousEntry,"Retired")+" active="+Read(previousEntry,"Active")+
                        " pendingHandoff="+Ref(Read(previousEntry,"PendingHandoff"))+" childDecision="+Decision(Read(entry,"Decision"))+" childFirst="+Read(entry,"FirstFrame")+
                        " childSeq="+Read(entry,"RequestSequence")+" childAcq="+Read(entry,"AcquiredSequence")+" childQueries="+Read(entry,"Queries"));
                }
                catch(Exception error){errors.Add("Availability identity observation UNKNOWN stage="+label+" "+error);}
            }
            void BeforeSubmit(string operation,string id,string other,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                if(sampled || id!=first || operation!="traffic.pickup-family-preflight" || result==null ||
                    !result.StartsWith("operation=CollectionLease;",StringComparison.Ordinal))return;
                sampled=true;
                try
                {
                    visit=((IDictionary)Read(flow,"visits"))[first];actor=(NavigationActor)Read(visit,"Actor");
                    admission=Read(visit,"Admission");queue=(PickupQueueService)Read(visit,"PickupOwner");pose=actor.transform.position;
                    generation=(int)Read(visit,"Generation");phaseSequence=(int)Read(visit,"TrafficPhaseSequence");
                    key=((IDictionary)Read(flow,"trafficKeys")).Values.Cast<TrafficIntentKey>().Single(k=>k.ActorId==first &&
                        k.IntentId=="pickup-service" && k.Context==first+"/"+phaseSequence);
                    var frames=(IDictionary)Read(visit,"TrafficCandidateFrames");birth=frames.Contains(key)?(int)frames[key]:Time.frameCount;
                    total=flow.Capacity.GetReservations().Single(r=>r.OwnerId==first && r.Kind==CapacityKind.TotalCustomers && r.State==ReservationState.Occupied).Token;
                    var rowFound=queue.TryGetReservation(first,out row);var claimFound=queue.TryCaptureReservation(first,out claim);
                    natural=rowFound && claimFound && claim.IsCurrent && row.Arrived && !entries.Contains(key) &&
                        actor.isActiveAndEnabled && world.RegisteredActors.Any(a=>ReferenceEquals(a,actor)) &&
                        Vector3.Distance(pose,row.Position)<=actor.Settings.ArrivalDistance+.001f &&
                        (CustomerVisitState)Read(visit,"State")==CustomerVisitState.PickupWaiting && (int)Read(visit,"DeliveryYieldPhase")==0 &&
                        owner.HoldCollection && owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==first && o.State==OrderState.ReadyForPickup);
                    preparation=resolve.Invoke(flow,new object[]{visit,key});preview=(PickupQueueService.PreparedMutation)Read(preparation,"Preview");
                    candidate=int.Parse(result.Split(';').Single(p=>p.StartsWith("candidate=",StringComparison.Ordinal)).Substring(10));
                    var held=(PickupQueueService.ReservationClaim)Read(preparation,"Claim");
                    bound=preparation!=null && ReferenceEquals(Read(preparation,"Coordinator"),core) && ReferenceEquals(Read(preparation,"Visit"),visit) &&
                        ReferenceEquals(Read(preparation,"Actor"),actor) && ReferenceEquals(Read(preparation,"Admission"),admission) &&
                        ReferenceEquals(Read(preparation,"Total"),total) && ReferenceEquals(Read(preparation,"Owner"),queue) &&
                        ((TrafficIntentKey)Read(preparation,"Key")).Equals(key) && (int)Read(preparation,"FirstFrame")==birth &&
                        (int)Read(preparation,"CandidateIndex")==candidate && ((QueueAssignment)Read(preparation,"Assignment")).Equals(row) &&
                        (Vector3)Read(preparation,"Start")==pose && (Vector3)Read(preparation,"Target")==target &&
                        held!=null && held.IsCurrent && held.IsSameReservation(claim) && preview!=null && !(bool)Read(preview,"Consumed") && preview.IsCurrent &&
                        ReferenceEquals(Read(preview,"owner"),queue) && (string)Read(preview,"Id")==first && (PickupQueueService.MutationKind)Read(preview,"Kind")==PickupQueueService.MutationKind.CollectionLease;
                    originalCurrent=Current();
                    records.Add("M2B_CAPTURE mode="+mode+" natural="+natural+" bound="+bound+" current="+originalCurrent+" frame="+Time.frameCount+
                        " key="+Key(key)+" birth="+birth+" row="+row.Generation+"/"+row.SlotIndex+"/"+row.Arrived+" preview="+Ref(preview));
                }
                catch(Exception error){errors.Add(error.ToString());}
            }
            void SnapshotChanged(TrafficRequest original,TrafficWorldSnapshot next)
            {
                if(failedSample || !sampled || !original.Intent.Equals(key))return;failedSample=true;
                try
                {
                    failureCurrent=Current() && SameBusiness();
                    records.Add("M2B_SNAPSHOT_ORIGINAL cap=1 frame="+Time.frameCount+" key="+Key(key)+" sourceCurrent="+failureCurrent+
                        " sameEntry="+ReferenceEquals(entries[key],entry)+" sameRequest="+ReferenceEquals(Read(entry,"Request"),original)+
                        " versions="+original.Snapshot.Version+"/"+original.Snapshot.BodyVersion+"->"+next.Version+"/"+next.BodyVersion+
                        " birth="+Read(entry,"FirstFrame")+" requestFrame="+original.RequestedFrame+" seq="+Read(entry,"RequestSequence")+
                        " acq="+Read(entry,"AcquiredSequence")+" queries="+Read(entry,"Queries")+" wait="+Read(entry,"WaitRemaining")+
                        " candidate="+Read(entry,"CurrentIndex")+"/"+Read(entry,"CandidateCount")+" attempted="+Items(Read(entry,"AttemptedIndices")));
                }
                catch(Exception error){errors.Add(error.ToString());}
            }
            Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string> observer=BeforeSubmit;
            Action<TrafficRequest,TrafficWorldSnapshot> failureObserver=SnapshotChanged;
            var combined=Delegate.Combine(previous as Delegate,observer);var combinedFailure=Delegate.Combine(previousSnapshot as Delegate,failureObserver);
            collectionPreparationWitnessCleanup=()=>
            {
                if(finished)return;finished=true;collectionPreparationWitnessCleanup=null;
                try{if(ReferenceEquals(hook.GetValue(flow),combined))hook.SetValue(flow,previous);else errors.Add("Corridor hook replaced externally");}
                catch(Exception error){errors.Add(error.ToString());}
                try{if(ReferenceEquals(snapshotHook.GetValue(core),combinedFailure))snapshotHook.SetValue(core,previousSnapshot);else errors.Add("Snapshot hook replaced externally");}
                catch(Exception error){errors.Add(error.ToString());}
                foreach(var line in records)TestContext.WriteLine(line);
                TestContext.WriteLine("M2B_WITNESS mode="+mode+" cap1="+sampled+" originalSnapshotFailure="+failedSample+" errors="+errors.Count);
                foreach(var error in errors)TestContext.WriteLine("M2B_OBSERVATION_ERROR UNKNOWN "+error);
            };
            hook.SetValue(flow,combined);snapshotHook.SetValue(core,combinedFailure);
            try
            {
                yield return AwaitTrafficAdmission();first=customers.Snapshot.Single().VisitId;
                yield return Until(()=>owner.Snapshot.Orders.Count==1 && customers.PickupAssignments.Count==1 &&
                    owner.Snapshot.Orders.Values.Single().State==OrderState.ReadyForPickup && customers.PickupAssignments.Single().Arrived &&
                    employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                var second="";
                if(mode=="metadata")
                {
                    yield return AwaitTrafficAdmission();second=customers.Snapshot.Single(v=>v.VisitId!=first).VisitId;
                    var service=Read(owner,"service");var queued=service.GetType().GetMethod("HasQueuedCashierCompletion",flags);
                    var queuedCompletion=false;
                    for(var frame=0;frame<6000;frame++)
                    {
                        queuedCompletion=(bool)queued.Invoke(service,new object[]{second});if(queuedCompletion)break;
                        if(owner.Snapshot.Orders.Count>1)break;
                        yield return null;
                    }
                    if(!queuedCompletion)Assert.Ignore("NOT_PROVEN: genuine second queued CashierCompletion window was not observed; no fabricated payment timing");
                    Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1));
                }
                Assert.That(customers.TryMoveToPickupService(first,out var submitReason),Is.False,"Original first Submit must be asynchronous, not a canonical Collection commit");
                Errors();Assert.That(sampled && natural,Is.True,"M2B FIXTURE NOT_PROVEN: authentic ready/arrived original source before first Submit");
                Assert.That(bound && originalCurrent,Is.True,"M2B source must be genuinely captured/current before testing freshness");
                entry=entries[key];Assert.That(entry,Is.Not.Null);count=(int)Read(entry,"CandidateCount");queries=(int)Read(entry,"Queries");
                sequence=(long)Read(entry,"RequestSequence");finiteLimit=(float)Read(entry,"FiniteLimit");priorWait=(float)Read(entry,"WaitRemaining");
                originalAttempted=Items(Read(entry,"AttemptedIndices"));var initial=(TrafficRequest)Read(entry,"Request");
                Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(birth));Assert.That(initial.RequestedFrame,Is.EqualTo(birth));
                Assert.That(Read(entry,"Queued"),Is.EqualTo(true));Assert.That(Read(entry,"AcquiredSequence"),Is.EqualTo(0L));
                void Accounting()
                {
                    Errors();Assert.That(entries[key],Is.SameAs(entry));Assert.That(Read(entry,"FirstFrame"),Is.EqualTo(birth));
                    Assert.That(((TrafficRequest)Read(entry,"Request")).RequestedFrame,Is.EqualTo(birth));
                    Assert.That(Read(entry,"CandidateCount"),Is.EqualTo(count));Assert.That(Read(entry,"CurrentIndex"),Is.EqualTo(candidate));
                    Assert.That(Items(Read(entry,"AttemptedIndices")),Is.EqualTo(originalAttempted));Assert.That(Read(entry,"FiniteLimit"),Is.EqualTo(finiteLimit));
                    var seq=(long)Read(entry,"RequestSequence");if(sequence==0)sequence=seq;else Assert.That(seq,Is.EqualTo(sequence));
                    Assert.That(Read(entry,"AcquiredSequence"),Is.EqualTo(0L));var spent=(int)Read(entry,"Queries");
                    Assert.That(spent,Is.GreaterThanOrEqualTo(queries));Assert.That(spent,Is.LessThanOrEqualTo((int)Read(core,"queriesPerIntent")));queries=spent;
                    var wait=(float)Read(entry,"WaitRemaining");if(priorWait>0)Assert.That(wait,Is.LessThanOrEqualTo(priorWait));priorWait=wait;
                    Assert.That(Read(preparation,"Preview"),Is.SameAs(preview));Assert.That(resolve.Invoke(flow,new[]{visit,(object)key}),Is.SameAs(preparation));
                }
                if(mode=="predecessor-valid" || mode=="predecessor-expired")
                {
                    var pending=Read(visit,"PendingTraffic");Assert.That(pending,Is.Not.Null,"NATIVE PREMISE: genuine completed predecessor receipt");
                    var handle=Read(pending,"Handle") as TrafficPhaseHandoff;var predecessorSource=Read(pending,"Source") as TrafficDecision;
                    var predecessorKey=(TrafficIntentKey)Read(pending,"Intent");
                    Assert.That(handle,Is.Not.Null);Assert.That(predecessorSource,Is.Not.Null);Assert.That(entries.Contains(predecessorKey),Is.True);
                    var oldState=Read(entries[predecessorKey],"PendingHandoff");Assert.That(oldState,Is.Not.Null,"NATIVE PREMISE: original finite handoff is still live");
                    Assert.That(Read(oldState,"Handle"),Is.SameAs(handle));Assert.That(Read(oldState,"Source"),Is.SameAs(predecessorSource));
                    var preflight=typeof(ServiceTrafficCoordinator).GetMethod("IsCurrentPhaseHandoff",flags);Assert.That(preflight,Is.Not.Null);
                    bool ValidPredecessor()=>(bool)preflight.Invoke(core,new object[]{handle,predecessorSource,actor});
                    Assert.That(ValidPredecessor(),Is.True,"NATIVE PREMISE: exact actual predecessor is valid before normal held frames");
                    var expired=mode=="predecessor-expired";var observed=false;
                    for(var frame=0;frame<6000;frame++)
                    {
                        var old=core.GetDecision(predecessorKey);var next=core.GetDecision(key);
                        observed=expired?old.Reason=="phase-handoff-aborted:original-budget-exhausted":next.Kind==TrafficDecisionKind.Granted;
                        if(observed)break;
                        yield return null;Accounting();Assert.That(SameBusiness(),Is.True,"Held original actor/total/order/reservation remain actual and unchanged");
                    }
                    Assert.That(observed,Is.True,"NATIVE PREMISE: normal frames must expose the requested valid/expired predecessor boundary");
                    Assert.That(core.GetDecision(key).Kind,Is.EqualTo(TrafficDecisionKind.Granted),"NATIVE PREMISE: actual current Collection candidate is ready before the precommit gate");
                    Assert.That(ValidPredecessor(),Is.EqualTo(!expired));Assert.That(queue.TryGetServiceLease(first,out _),Is.False);
                    var beforeVersion=queue.Version;var beforeQueries=core.QueriesConsumed(key);var orderState=owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==first).State;
                    var accepted=customers.TryMoveToPickupService(first,out var guardReason);
                    Assert.That(accepted,Is.EqualTo(!expired),expired?
                        "PRODUCT: an already-expired predecessor must fail before canonical Collection Apply":
                        "PRODUCT: a current predecessor must still allow the original canonical Collection submission");
                    if(expired)
                    {
                        Assert.That((CustomerVisitState)Read(visit,"State"),Is.EqualTo(CustomerVisitState.Blocked));Assert.That(Read(visit,"Failure"),Is.Not.Null.And.Not.Empty);
                        Assert.That(queue.Version,Is.EqualTo(beforeVersion));Assert.That(queue.TryGetServiceLease(first,out _),Is.False);
                        Assert.That(queue.TryGetReservation(first,out var held),Is.True);Assert.That(held,Is.EqualTo(row));Assert.That(claim.IsCurrent,Is.True);
                        Assert.That(actor.transform.position,Is.EqualTo(pose));Assert.That(Read(visit,"Actor"),Is.SameAs(actor));Assert.That(Read(visit,"RequestId"),Is.EqualTo(0L));
                        Assert.That(flow.Capacity.GetReservations().Any(r=>ReferenceEquals(r.Token,total) && r.State==ReservationState.Occupied),Is.True);
                        Assert.That(owner.Snapshot.Orders.Values.Single(o=>o.CustomerId==first).State,Is.EqualTo(orderState));
                        Assert.That(Read(entry,"Retired"),Is.EqualTo(true));Assert.That(core.QueriesConsumed(key),Is.EqualTo(beforeQueries));Assert.That(core.HasActiveGrant(key),Is.False);
                        records.Add("HANDOFF_PRECOMMIT expired=True queueApply=False blocked=True key="+Key(key)+" reason="+guardReason);
                        Errors();yield break;
                    }
                    Assert.That(queue.TryGetServiceLease(first,out var target),Is.True);Assert.That(target,Is.EqualTo((Vector3)Read(preparation,"Target")));
                    for(var frame=0;frame<6000 && (CustomerVisitState)Read(visit,"State")!=CustomerVisitState.PickupService;frame++)yield return null;
                    Assert.That((CustomerVisitState)Read(visit,"State"),Is.EqualTo(CustomerVisitState.PickupService));Assert.That(Read(visit,"ServiceArrived"),Is.EqualTo(true));
                    var leg=Read(entry,"Leg");Assert.That(leg,Is.Not.Null);Assert.That(Read(leg,"Bound"),Is.EqualTo(true));Assert.That(Read(leg,"Completed"),Is.EqualTo(true));
                    Assert.That(((TrafficNativeStamp)Read(leg,"Stamp")).RequestId,Is.GreaterThan(0));Assert.That(Read(visit,"RequestId"),Is.EqualTo(0L));
                    records.Add("HANDOFF_PRECOMMIT expired=False queueApply=True strictNativeArrived=True key="+Key(key));
                    Errors();yield break;
                }
                if(mode=="replacement")
                {
                    var decor=Find<DecorationModeController>();var layout=Find<CafeLayoutRuntime>();
                    decor.EnterDecorationMode();yield return null;
                    var machine=layout.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
                    var began=decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,machine.InstanceId);
                    var rotated=began && decor.TryRotateFunctionalSurfacePreview();var confirmed=rotated && decor.TryConfirmFunctionalSurfacePreview();
                    decor.ExitDecorationMode();
                    if(!confirmed)Assert.Ignore("NOT_PROVEN: actual CoffeeMachine layout rotation was not confirmed; Held alone is not owner replacement");
                    PickupQueueService replacement=null;var retired=false;
                    for(var frame=0;frame<6000;frame++)
                    {
                        var liveVisits=(IDictionary)Read(flow,"visits");
                        retired=!claim.IsCurrent && (!liveVisits.Contains(first) || !ReferenceEquals(liveVisits[first],visit));
                        replacement=Read(flow,"pickupQueue") as PickupQueueService;
                        if(retired && replacement!=null && !ReferenceEquals(replacement,queue))break;
                        // Real new reception/payment may be needed to instantiate the replacement Queue.
                        // 旧visit已退休后才让真实新顾客走正常接待/付款；不直接创建或修改owner。
                        if(retired && owner.CanProgress && customers.Snapshot.Count==0)customers.TrySpawn();
                        yield return null;
                    }
                    if(!retired || replacement==null || ReferenceEquals(replacement,queue))
                        Assert.Ignore("NOT_PROVEN: confirmed layout did not retire the actual old visit/claim and create a different non-null Queue through normal business");
                    Assert.That(claim.IsCurrent,Is.False);Assert.That(replacement,Is.Not.SameAs(queue));
                    records.Add("M2B_REPLACEMENT confirmed=True oldVisitRetired=True oldClaimCurrent=False oldOwner="+Ref(queue)+" newOwner="+Ref(replacement));
                    var oldVersion=queue.Version;var newVersion=replacement.Version;
                    Assert.That(Current(),Is.False,"Actual retired owner/visit and real new Queue must reject the original seal");
                    Assert.That(customers.TryMoveToPickupService(first,out _),Is.False);
                    Assert.That(queue.Version,Is.EqualTo(oldVersion));Assert.That(Read(flow,"pickupQueue"),Is.SameAs(replacement));
                    Assert.That(replacement?.Version,Is.EqualTo(newVersion));Assert.That((bool)Read(preview,"Consumed"),Is.False);Errors();yield break;
                }
                if(mode=="availability")
                {
                    AvailabilityFacts(1,"before-disable");
                    customers.enabled=false;
                    AvailabilityFacts(2,"after-disable");
                    Assert.That(customers.isActiveAndEnabled,Is.False);Assert.That(Current(),Is.False,"Inactive owner cannot authorize preparation");
                    customers.enabled=true;tools.ResumeBlockedFlow(); // Synchronous real callbacks; no intervening frame can Terminalize the source.
                    AvailabilityFacts(4,"after-resume");
                    if(!SameBusiness())Assert.Ignore("NOT_PROVEN: real availability cycle changed canonical owner/row/pose/phase; same-owner premise absent");
                    if(core.GetDecision(key).FailureDisposition==TrafficFailureDisposition.Terminal)
                        Assert.Ignore("NOT_PROVEN: actual source already Terminal during availability; never demand revival");
                    Accounting();Assert.That(Current(),Is.True,"FEATURE: unchanged original pending source must survive benign same-owner availability without recapture");
                    Errors();yield break; // Cross-frame unavailability remains a separate future premise.
                }
                if(mode=="metadata")
                {
                    var delta=false;var stable=0;TrafficWorldSnapshot stableSnapshot=null;TrafficDecision stableDecision=null;string stableLedger=null;
                    for(var frame=0;frame<6000;frame++)
                    {
                        yield return null;Accounting();Assert.That(SameBusiness(),Is.True,"Original held business facts changed; freshness premise is not established");
                        Assert.That(Current(),Is.True,"FEATURE: original semantic source stays current through external payment metadata");
                        var now=(TrafficWorldSnapshot)Read(core,"snapshot");var request=(TrafficRequest)Read(entry,"Request");var decision=core.GetDecision(key);
                        delta|=now.Version!=initial.Snapshot.Version || now.BodyVersion!=initial.Snapshot.BodyVersion;
                        if(decision.FailureDisposition==TrafficFailureDisposition.Terminal)
                        {
                            Assert.That(delta && failedSample && failureCurrent,Is.True,"NOT_PROVEN: terminal failure lacks authenticated original metadata witness");
                            Assert.Fail("FEATURE RED: authentic original Collection source became Terminal after real external Cashier payment metadata");
                        }
                        if(!delta || owner.Snapshot.Orders.Count!=2 || !customers.PickupAssignments.All(a=>a.Arrived) ||
                            !employees.Snapshot.All(e=>e.State==EmployeeState.Idle) || decision.Kind!=TrafficDecisionKind.Granted){stable=0;continue;}
                        var ledger=Read(entry,"FirstFrame")+"/"+Read(entry,"RequestSequence")+"/"+Read(entry,"AcquiredSequence")+"/"+queries+"/"+
                            Read(entry,"WaitRemaining")+"/"+Items(Read(entry,"FrozenOwners"))+"/"+Ref(Read(entry,"FrozenResources"))+"/"+Ref(Read(entry,"WaitingBlockers"));
                        if(stableSnapshot!=null && stableSnapshot.Version==now.Version && stableSnapshot.BodyVersion==now.BodyVersion)
                        {
                            Assert.That(request.Snapshot.Version,Is.EqualTo(now.Version));Assert.That(request.Snapshot.BodyVersion,Is.EqualTo(now.BodyVersion));
                            Assert.That(decision,Is.SameAs(stableDecision),"Stable prepared source must not be repeatedly revoked");Assert.That(ledger,Is.EqualTo(stableLedger));
                            stable++;if(stable>=2){records.Add("M2B_METADATA refreshed=True stableFrames=2 key="+Key(key)+" birth="+birth+" sequence="+sequence+" queries="+queries);yield break;}
                        }
                        else stable=0;
                        stableSnapshot=now;stableDecision=decision;stableLedger=ledger;
                    }
                    Assert.Ignore("NOT_PROVEN: actual external metadata delta plus two stable uncommitted prepared frames was unavailable");
                }
                for(var frame=0;frame<6000 && core.GetDecision(key).Kind!=TrafficDecisionKind.Granted;frame++)
                {yield return null;Accounting();Assert.That(core.GetDecision(key).FailureDisposition,Is.Not.EqualTo(TrafficFailureDisposition.Terminal),"Actual preparation failed before consumed-preview premise");}
                var granted=core.GetDecision(key);if(granted.Kind!=TrafficDecisionKind.Granted)Assert.Ignore("NOT_PROVEN: actual fresh uncommitted grant unavailable");
                Assert.That(customers.TryMoveToPickupService(first,out _),Is.True,"Actual public Collection commit required");
                Assert.That(queue.TryGetServiceLease(first,out var lease),Is.True);Assert.That(lease,Is.EqualTo((Vector3)Read(preparation,"Target")));
                var committedVersion=queue.Version;Assert.That(Current(),Is.False);Assert.That(core.TryCommit(granted),Is.False,"Consumed actual grant cannot canonical-commit twice");
                Assert.That(queue.Version,Is.EqualTo(committedVersion));Errors();
                records.Add("M2B_CONSUMED actualLease=True oldSealCurrent=False originalPreviewConsumed="+(bool)Read(preview,"Consumed")+" rejection=compound-phase/claim/acquired");
                Assert.That(Read(preparation,"Preview"),Is.SameAs(preview),"Original sealed preview is retained as provenance");
                var currentPreviewField=preparation.GetType().GetField("CurrentPreview",flags);
                var consumed=currentPreviewField?.GetValue(preparation) as PickupQueueService.PreparedMutation;
                if(consumed==null)Assert.Ignore("NOT_PROVEN: actual consumed CurrentPreview object is not retained; no fabricated consumed flag");
                Assert.That(Read(consumed,"owner"),Is.SameAs(queue));Assert.That(Read(consumed,"Kind"),Is.EqualTo(PickupQueueService.MutationKind.CollectionLease));
                Assert.That(Read(consumed,"Id"),Is.EqualTo(first));Assert.That(consumed.Assignment.VisitId,Is.EqualTo(first));
                Assert.That(consumed.Assignment.Position,Is.EqualTo((Vector3)Read(preparation,"Target")));Assert.That((bool)Read(consumed,"Consumed"),Is.True);
                var replayVersion=queue.Version;Assert.That(consumed.TryApply(out _),Is.False,"Actual consumed mutation cannot apply again");
                Assert.That(queue.Version,Is.EqualTo(replayVersion));Assert.That(queue.Version,Is.EqualTo(committedVersion));
                records.Add("M2B_CONSUMED_CURRENT actualObject=True consumed=True applyRejected=True versionUnchanged=True originalPreviewRetained=True guardCausality=compound-version/phase-not-isolated");
            }
            finally{collectionPreparationWitnessCleanup?.Invoke();}
        }

        [UnityTest] public IEnumerator PickupCollectionCapturesOriginalLeasePreparationBeforeSubmit()
            => PickupFamilyOriginalPreparation(PickupQueueService.MutationKind.CollectionLease);
        [UnityTest] public IEnumerator SingleSidePickupYieldedCollectionCapturesOriginalPreparationBeforeSubmit()
            => PickupFamilyOriginalPreparation(PickupQueueService.MutationKind.YieldedCollectionLease);
        [UnityTest] public IEnumerator SingleSidePickupBeginYieldCapturesOriginalPreparationBeforeSubmit()
            => PickupFamilyOriginalPreparation(PickupQueueService.MutationKind.BeginYield);
        [UnityTest] public IEnumerator SingleSidePickupReturnYieldCapturesOriginalPreparationBeforeSubmit()
            => PickupFamilyOriginalPreparation(PickupQueueService.MutationKind.ReturnYield);

        private IEnumerator PickupFamilyOriginalPreparation(PickupQueueService.MutationKind expected)
        {
            object Read(object value,string field)=>value==null?null:MovementRead(value,field);
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);
            var previous=hook.GetValue(customers);
            var currentMethod=typeof(CustomerFlowController).GetMethod("PickupFamilyPreparationCurrent",flags);
            var resolve=typeof(CustomerFlowController).GetMethod("FindPickupPreparation",flags);
            Assert.That(currentMethod,Is.Not.Null,"M1 inert family declaration must exist");
            var errors=new List<string>();var sampled=false;var natural=false;var bound=false;var current=false;
            object captured=null;var facts="NOT_OBSERVED";
            var yieldWitnessEnabled=Environment.GetCommandLineArgs().Contains("-trafficYieldFailureWitness") &&
                expected==PickupQueueService.MutationKind.BeginYield && TestContext.CurrentContext.Test.Name==
                "SingleSidePickupBeginYieldCapturesOriginalPreparationBeforeSubmit";
            var yieldWitnessObserved=false;var yieldWitnessReported=false;
            string YieldRef(object value)=>value==null?"NULL":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value).ToString();
            string YieldKey(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
            void YieldOutput(string line)
            {
                try{TestContext.WriteLine(line);yieldWitnessReported=true;}catch(Exception){}
            }
            void YieldSnapshot(string label,TrafficWorldSnapshot snapshot)
            {
                if(snapshot==null){YieldOutput(label+"=UNKNOWN(no-retained-snapshot)");return;}
                YieldOutput(label+" ref="+YieldRef(snapshot)+" version="+snapshot.Version+" bodyVersion="+snapshot.BodyVersion+
                    " bodies="+snapshot.Bodies.Count+" parking="+snapshot.Parking.Count);
                foreach(var body in snapshot.Bodies)
                    YieldOutput(label+".body key="+YieldKey(body.Owner)+" life="+YieldRef(body.ActorLife)+" pose="+body.Position.ToString("F9")+
                        " radius="+body.Radius+" safety="+body.SafetyMargin+" enabled="+body.Enabled+" active="+body.ActorActive+
                        " independentlyProgressing="+body.IndependentlyProgressing+" remaining="+body.RemainingSeconds);
                foreach(var claim in snapshot.Parking)
                    YieldOutput(label+".claim key="+YieldKey(claim.Owner)+" resource="+claim.ResourceId+" canonical="+YieldRef(claim.CanonicalOwner)+
                        " ownerVersion="+claim.OwnerVersion+" business="+YieldRef(claim.BusinessOwner)+" release="+YieldRef(claim.ReleaseObligation)+
                        " pose="+claim.Position.ToString("F9")+" radius="+claim.Radius+" safety="+claim.SafetyMargin);
            }
            void YieldFailureFacts(string original)
            {
                YieldOutput("[TRAFFIC_YIELD_FAILURE_WITNESS] status=OBSERVED "+original);
                var actualCore=(ServiceTrafficCoordinator)Read(customers,"traffic");
                var keyPart=original?.Split(';').SingleOrDefault(part=>part.StartsWith("key=",StringComparison.Ordinal));
                if(keyPart==null)throw new InvalidOperationException("sourcepoint did not retain a full key");
                var entryMap=(IDictionary)Read(actualCore,"entries");
                var keys=entryMap.Keys.Cast<TrafficIntentKey>().Where(key=>YieldKey(key)==keyPart.Substring(4)).ToArray();
                if(keys.Length!=1)throw new InvalidOperationException("actual sourcepoint entry key was not unique");
                var key=keys[0];var entry=entryMap[key];var decision=(TrafficDecision)Read(entry,"Decision");
                var request=(TrafficRequest)Read(entry,"Request");var jobs=Read(entry,"Jobs") as IList;
                var jobIndex=(int)Read(entry,"JobIndex");var job=jobs!=null && jobIndex>=0 && jobIndex<jobs.Count?jobs[jobIndex]:null;
                var hasPrimary=!(bool)Read(entry,"Parking") || request.PlanPrimaryRoute;var requirementIndex=jobIndex-(hasPrimary?1:0);
                var primaryDescriptor=hasPrimary && jobIndex==0;
                var requirementDescriptor=requirementIndex>=0 && requirementIndex<request.Requirements.Count?request.Requirements[requirementIndex]:null;
                YieldOutput("YIELD_CORE ref="+YieldRef(actualCore)+" sameInitialCore="+ReferenceEquals(actualCore,coordinator)+
                    " paused="+Read(actualCore,"paused")+" closed="+Read(actualCore,"closed")+" entryRef="+YieldRef(entry)+
                    " decisionRef="+YieldRef(decision)+" decision="+decision.Kind+"/"+decision.FailureDisposition+" reason="+decision.Reason+
                    " requestRef="+YieldRef(request)+" fullKey="+YieldKey(request.Intent));
                YieldOutput("YIELD_ACCOUNTING frame="+Time.frameCount+" firstFrame="+Read(entry,"FirstFrame")+
                    " ageFrames="+(Time.frameCount-(int)Read(entry,"FirstFrame"))+" queries="+Read(entry,"Queries")+
                    " requestSequence="+Read(entry,"RequestSequence")+" acquiredSequence="+Read(entry,"AcquiredSequence")+
                    " count="+Read(entry,"CandidateCount")+" index="+Read(entry,"CurrentIndex")+
                    " attempted="+string.Join(",",((IEnumerable)Read(entry,"AttemptedIndices")).Cast<int>().OrderBy(value=>value))+
                    " jobIndex="+jobIndex+" jobsCount="+(jobs==null?"UNKNOWN":jobs.Count.ToString())+
                    " prepared="+Read(entry,"Prepared")+" active="+Read(entry,"Active")+" queued="+Read(entry,"Queued")+
                    " retired="+Read(entry,"Retired")+" discarded="+Read(entry,"Discarded")+" finiteLimit="+Read(entry,"FiniteLimit")+
                    " waitRemaining="+Read(entry,"WaitRemaining"));
                YieldOutput("YIELD_REQUEST requestedFrame="+request.RequestedFrame+" resource="+request.ResourceId+
                    " start="+request.Start.ToString("F9")+" target="+request.Target.ToString("F9")+" radius="+request.Radius+
                    " safety="+request.SafetyMargin+" planPrimary="+request.PlanPrimaryRoute+" selectedJourney="+request.SelectedJourney.Count+
                    " requirements="+request.Requirements.Count+" nativeRequest="+request.NativeStamp.RequestId+" nativeGeneration="+request.NativeStamp.LegGeneration);
                YieldOutput("YIELD_JOB retainedRef="+YieldRef(job)+" storedResource=UNKNOWN(RouteJob-has-no-resource-field)"+
                    " descriptorFromRequest="+(primaryDescriptor?"primary/"+request.ResourceId:requirementDescriptor==null?"UNKNOWN":"requirement/"+requirementDescriptor.ResourceId)+
                    " descriptorStart="+(primaryDescriptor?request.Start.ToString("F9"):requirementDescriptor==null?"UNKNOWN":requirementDescriptor.Start.ToString("F9"))+
                    " descriptorGoal="+(primaryDescriptor?request.Target.ToString("F9"):requirementDescriptor==null?"UNKNOWN":requirementDescriptor.Goal.ToString("F9"))+
                    " storedStart="+(job==null?"UNKNOWN(no-retained-job)":((Vector3)Read(job,"start")).ToString("F9"))+
                    " storedGoal="+(job==null?"UNKNOWN(no-retained-job)":((Vector3)Read(job,"goal")).ToString("F9"))+
                    " storedNativeEdgeCache="+(job==null?"UNKNOWN(no-retained-job)":((IDictionary)Read(job,"paths")).Count.ToString())+
                    " originalNativeResult=UNKNOWN(no-retained-last-query-result)");
                var retainedPath=Read(entry,"CurrentPath") as Vector3[];var retainedJourney=Read(entry,"Journey") as Vector3[];
                YieldOutput("YIELD_RETAINED currentPathCount="+(retainedPath==null?"UNKNOWN":retainedPath.Length.ToString())+
                    " journeyCount="+(retainedJourney==null?"UNKNOWN":retainedJourney.Length.ToString())+
                    " currentPath="+(retainedPath==null?"UNKNOWN":string.Join(";",retainedPath.Select(point=>point.ToString("F9"))))+
                    " journey="+(retainedJourney==null?"UNKNOWN":string.Join(";",retainedJourney.Select(point=>point.ToString("F9"))))+
                    " scope=current-stored-facts-not-original-Native-query");
                for(var index=0;index<request.Requirements.Count;index++)
                {
                    var proof=request.Requirements[index];
                    YieldOutput("YIELD_PROOF index="+index+" resource="+proof.ResourceId+" start="+proof.Start.ToString("F9")+
                        " goal="+proof.Goal.ToString("F9")+" clearance="+proof.Clearance+" selected="+proof.SelectedJourney+
                        " avoidProposed="+proof.AvoidProposedParking+" obstacles="+string.Join(";",proof.Obstacles.Select(point=>point.ToString("F9"))));
                }
                YieldSnapshot("YIELD_REQUEST_SNAPSHOT",request.Snapshot);YieldSnapshot("YIELD_CURRENT_SNAPSHOT",Read(actualCore,"snapshot") as TrafficWorldSnapshot);
                var cursorMap=Read(customers,"trafficCandidates") as IDictionary;var cursor=cursorMap!=null && cursorMap.Contains(key)?cursorMap[key]:null;
                YieldOutput("YIELD_CURSOR ref="+YieldRef(cursor)+" sourceRef="+YieldRef(Read(cursor,"Source"))+
                    " originalContextRef="+YieldRef(Read(cursor,"DeliveryContext"))+" previewRef="+YieldRef(Read(cursor,"DeliveryPreview")));
                foreach(var staff in employees.Snapshot)
                {
                    var actor=world.RegisteredActors.FirstOrDefault(value=>value.ActorId==staff.ActorId);
                    YieldOutput("YIELD_STAFF actor="+staff.ActorId+" role="+staff.Role+" state="+staff.State+" handle="+staff.HandleId+
                        " remaining="+staff.RemainingWorkSeconds+" failure="+staff.Failure+" life="+YieldRef(actor)+
                        " pose="+(actor==null?"UNKNOWN":actor.transform.position.ToString("F9")));
                }
                var visitMap=Read(customers,"visits") as IDictionary;
                foreach(var visitSnapshot in customers.Snapshot)
                {
                    var visit=visitMap!=null && visitMap.Contains(visitSnapshot.VisitId)?visitMap[visitSnapshot.VisitId]:null;
                    var actor=Read(visit,"Actor") as NavigationActor;
                    YieldOutput("YIELD_WAITER id="+visitSnapshot.VisitId+" state="+visitSnapshot.State+" generation="+Read(visit,"Generation")+
                        " phaseSequence="+Read(visit,"TrafficPhaseSequence")+" yieldPhase="+Read(visit,"DeliveryYieldPhase")+
                        " request="+Read(visit,"RequestId")+" collected="+Read(visit,"HasCollected")+" ownerRef="+YieldRef(Read(visit,"PickupOwner"))+
                        " life="+YieldRef(actor)+" pose="+(actor==null?"UNKNOWN":actor.transform.position.ToString("F9")));
                }
                foreach(var row in customers.PickupAssignments)
                    YieldOutput("YIELD_ROW id="+row.VisitId+" generation="+row.Generation+" slot="+row.SlotIndex+" arrived="+row.Arrived+" pose="+row.Position.ToString("F9"));
            }
            void YieldOriginalBlocked(CafeTaskEvent value)
            {
                if(!yieldWitnessEnabled || value.Role!=EmployeeRole.Barista || value.Kind!=CafeTaskEventKind.Blocked ||
                    value.Failure==null || !value.Failure.Contains("bounded-route-proof-failed"))return;
                employees.IntegratedTaskEvent-=YieldOriginalBlocked;
                if(!yieldWitnessReported)YieldOutput("[TRAFFIC_YIELD_FAILURE_WITNESS] status=OUTPUT_MISSING sourcepointObserved="+
                    yieldWitnessObserved+" originalBlocked=True facts=UNKNOWN failure="+value.Failure);
            }
            void BeforeSubmit(string operation,string id,string related,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                if(yieldWitnessEnabled && !yieldWitnessObserved && (operation=="traffic.delivery-yield-failure-witness" ||
                    operation=="traffic.delivery-yield-failure-witness-error"))
                {
                    yieldWitnessObserved=true;
                    try
                    {
                        if(operation.EndsWith("-error",StringComparison.Ordinal))YieldOutput("[TRAFFIC_YIELD_FAILURE_WITNESS] status=UNKNOWN "+result);
                        else YieldFailureFacts(result);
                    }
                    catch(Exception error){YieldOutput("[TRAFFIC_YIELD_FAILURE_WITNESS] status=UNKNOWN read="+error.GetType().Name+":"+error.Message);}
                }
                if(sampled || operation!="traffic.pickup-family-preflight" || result==null ||
                    !result.StartsWith("operation="+expected+";",StringComparison.Ordinal))return;
                sampled=true; // cap 1; errors are asserted outside the production callback.
                try
                {
                    var visit=((IDictionary)Read(customers,"visits"))[id];var actor=(NavigationActor)Read(visit,"Actor");
                    var queue=(PickupQueueService)Read(visit,"PickupOwner");
                    var rowFound=queue.TryGetReservation(id,out var row);var claimFound=queue.TryCaptureReservation(id,out var claim);
                    var phase=(CustomerVisitState)Read(visit,"State");var yieldPhase=(int)Read(visit,"DeliveryYieldPhase");
                    var phaseSequence=(int)Read(visit,"TrafficPhaseSequence");var entries=(IDictionary)Read(coordinator,"entries");
                    var intentName=expected==PickupQueueService.MutationKind.BeginYield?"pickup-yield":
                        expected==PickupQueueService.MutationKind.ReturnYield?"pickup-yield-return":"pickup-service";
                    var key=((IDictionary)Read(customers,"trafficKeys")).Values.Cast<TrafficIntentKey>().Single(k=>
                        k.ActorId==id && k.IntentId==intentName && k.Context==id+"/"+phaseSequence);
                    var sourceFrame=Time.frameCount;var snapshot=(TrafficWorldSnapshot)Read(customers,"trafficSnapshot");
                    // A declared child may have an earlier real logical birth than this first Submit callback.
                    var sourceFrames=(IDictionary)Read(visit,"TrafficCandidateFrames");
                    var sourceBirth=sourceFrames.Contains(key)?(int)sourceFrames[key]:sourceFrame;
                    var total=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers);
                    var staff=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);
                    var staffActor=world.RegisteredActors.Single(a=>a.ActorId==staff.ActorId);
                    var deliveryAcquired=(bool)Read(customers,"deliveryAcquired");var deliveryWasAway=(bool)Read(customers,"deliveryWasAway");
                    var deliveryHome=(NavigationTarget)Read(customers,"deliveryWorkTarget");
                    var actualHome=Vector3.Distance(staffActor.transform.position,deliveryHome.Position)<=staffActor.Settings.ArrivalDistance+.001f;
                    var ready=owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==id && o.State==OrderState.ReadyForPickup);
                    var phasePremise=expected==PickupQueueService.MutationKind.CollectionLease?yieldPhase==0 && ready:
                        expected==PickupQueueService.MutationKind.YieldedCollectionLease?yieldPhase==2 && ready && staff.State==EmployeeState.Returning:
                        expected==PickupQueueService.MutationKind.BeginYield?yieldPhase==0 && staff.State==EmployeeState.WaitingToDeliver:
                        yieldPhase==2 && deliveryAcquired && deliveryWasAway && actualHome;
                    natural=rowFound && claimFound && claim.IsCurrent && row.Arrived && row.VisitId==id &&
                        actor!=null && actor.isActiveAndEnabled && world.RegisteredActors.Any(a=>ReferenceEquals(a,actor)) &&
                        Vector3.Distance(actor.transform.position,row.Position)<=actor.Settings.ArrivalDistance+.001f &&
                        phase==CustomerVisitState.PickupWaiting && phasePremise && total.State==ReservationState.Occupied &&
                        !entries.Contains(key) && snapshot!=null && result.Contains(";frame="+sourceFrame+";");
                    captured=resolve.Invoke(customers,new object[]{visit,key});
                    var cursors=(IDictionary)Read(customers,"trafficCandidates");var cursor=cursors.Contains(key)?cursors[key]:null;
                    var candidateIndex=int.Parse(result.Split(';').Single(part=>part.StartsWith("candidate=",StringComparison.Ordinal)).Substring(10));
                    var preview=(PickupQueueService.PreparedMutation)Read(captured,"Preview");
                    var storedClaim=(PickupQueueService.ReservationClaim)Read(captured,"Claim");
                    bound=captured!=null && ReferenceEquals(Read(captured,"Coordinator"),coordinator) &&
                        ReferenceEquals(Read(captured,"Visit"),visit) && ReferenceEquals(Read(captured,"Actor"),actor) &&
                        ReferenceEquals(Read(captured,"Admission"),Read(visit,"Admission")) && ReferenceEquals(Read(captured,"Total"),total.Token) &&
                        ReferenceEquals(Read(captured,"Owner"),queue) && ((TrafficIntentKey)Read(captured,"Key")).Equals(key) &&
                        (int)Read(captured,"FirstFrame")==sourceBirth && (int)Read(captured,"Generation")== (int)Read(visit,"Generation") &&
                        (int)Read(captured,"PhaseSequence")==phaseSequence && (int)Read(captured,"YieldPhase")==yieldPhase &&
                        ReferenceEquals(Read(captured,"Cursor"),cursor) && (int)Read(captured,"CandidateIndex")==candidateIndex &&
                        (CustomerVisitState)Read(captured,"Phase")==phase && (Vector3)Read(captured,"Start")==actor.transform.position &&
                        (Vector3)Read(captured,"Target")==target && ((QueueAssignment)Read(captured,"Assignment")).Equals(row) &&
                        (PickupQueueService.MutationKind)Read(captured,"Operation")==expected &&
                        storedClaim!=null && storedClaim.IsCurrent && storedClaim.IsSameReservation(claim) &&
                        preview!=null && preview.IsCurrent && ReferenceEquals(Read(preview,"owner"),queue) &&
                        (string)Read(preview,"Id")==id && (PickupQueueService.MutationKind)Read(preview,"Kind")==expected &&
                        preview.Assignment.VisitId==id && preview.Assignment.Position==target;
                    // The forecast preview generation is not the generation of the actual held claim.
                    // 只认证当前真实 preview；不能要求全局 next-generation == held-generation+1。
                    if(bound && expected==PickupQueueService.MutationKind.BeginYield)
                    {
                        var context=Read(captured,"DeliveryContext");
                        bound=ReferenceEquals(Read(captured,"DeliveryActor"),staffActor) && context!=null &&
                            (bool)context.GetType().GetProperty("IsCurrent",flags).GetValue(context);
                    }
                    if(bound && expected==PickupQueueService.MutationKind.ReturnYield)
                        bound=ReferenceEquals(Read(captured,"DeliveryActor"),staffActor) &&
                            (bool)Read(captured,"DeliveryAcquired")==deliveryAcquired && (bool)Read(captured,"DeliveryWasAway")==deliveryWasAway &&
                            ((NavigationTarget)Read(captured,"DeliveryWorkTarget")).Position==deliveryHome.Position &&
                            Read(captured,"DeliveryLease") is PickupQueueService.DeliveryLeaseClaim;
                    current=captured!=null && (bool)currentMethod.Invoke(customers,new[]{captured});
                    facts="frame="+sourceFrame+" logicalBirth="+sourceBirth+" operation="+expected+" key="+key+" phase="+phase+" yield="+yieldPhase+
                        " row="+row.Generation+"/"+row.SlotIndex+"/arrived="+row.Arrived+" claim="+claimFound+
                        " queueVersion="+queue.Version+" snapshot="+snapshot.Version+"/"+snapshot.BodyVersion+
                        " firstSubmit="+!entries.Contains(key)+" staff="+staff.State+" actualHome="+actualHome+
                        " acquiredHistory="+deliveryAcquired+" wasAwayHistory="+deliveryWasAway+
                        " originalPreview="+(preview==null?"UNKNOWN_UNTIL_CAPTURE":"ACTUAL_RECORD")+
                        " previewForecast="+(preview==null?"UNKNOWN":preview.Assignment.Generation.ToString());
                }
                catch(Exception error){errors.Add(error.ToString());}
            }
            try
            {
                if(yieldWitnessEnabled)employees.IntegratedTaskEvent+=YieldOriginalBlocked;
                hook.SetValue(customers,Delegate.Combine(previous as Delegate,
                    (Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)BeforeSubmit));
                if(expected==PickupQueueService.MutationKind.CollectionLease)
                {
                    yield return AwaitTrafficAdmission();
                    yield return Until(()=>owner.Snapshot.Orders.Count==1 && customers.PickupAssignments.Count==1 &&
                        owner.Snapshot.Orders.Values.Single().State==OrderState.ReadyForPickup &&
                        customers.PickupAssignments.Single().Arrived && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
                    tools.ResumeCollection();
                    for(var frame=0;frame<3000 && !sampled && errors.Count==0;frame++)
                    {
                        if(customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked) || employees.Snapshot.Any(e=>e.State==EmployeeState.Blocked))break;
                        yield return null;
                    }
                }
                else
                    yield return ActualPaidYield(expected==PickupQueueService.MutationKind.YieldedCollectionLease,
                        preparationObserved:()=>sampled || errors.Count!=0);
                TestContext.WriteLine("PICKUP_FAMILY_M1 naturalPremise="+natural+" "+facts+
                    " featureExpectedRecord=True actualRecord="+(captured!=null)+" bound="+bound+" current="+current+" deeperControls=NOT_PROVEN");
                Assert.That(errors,Is.Empty,"PICKUP_FAMILY FIXTURE: read-only observer errors");
                Assert.That(sampled && natural,Is.True,"PICKUP_FAMILY FIXTURE NOT_PROVEN: actual original operation/claim/history before first Submit was unavailable; "+facts);
                Assert.That(captured,Is.Not.Null,"PICKUP_FAMILY FEATURE RED: authentic original source requires a production-owned preparation record");
                Assert.That(bound,Is.True,"PICKUP_FAMILY FEATURE: record must bind actual original owner/claim/preview/phase/history");
                Assert.That(current,Is.True,"PICKUP_FAMILY FEATURE: original authentic captured producer is current");
            }
            finally{if(yieldWitnessEnabled)employees.IntegratedTaskEvent-=YieldOriginalBlocked;hook.SetValue(customers,previous);}
        }

        [UnityTest] public IEnumerator FinishedCashierEfcRecreationUsesNewInitialMovementAfterRealHomeReturn()
        {
            // Real payment and home return precede lifecycle invalidation; no synthetic key or Native result.
            // 先完成真实业务与返岗，再用公开 enable/Resume 检查新生命周期，旧 tombstone 不复活。
            object Read(object value,string field)=>value==null?null:MovementRead(value,field);
            string Ref(object value)=>value==null?"NULL":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value).ToString();
            string Key(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
            string Token(object value)=>value is CafeTaskToken token?token.SessionId+"/e"+token.Epoch+"/s"+token.Sequence+"/a"+token.Attempt:"NULL";
            string Checkpoint(object value)=>value is CafeWorkCheckpoint checkpoint?checkpoint.Role+"/"+checkpoint.VisitId+"/"+checkpoint.Stage+
                "/remaining="+checkpoint.RemainingWorkSeconds+"/order="+(checkpoint.Order.HasValue?checkpoint.Order.Value.SessionId+"/e"+checkpoint.Order.Value.Epoch+"/"+checkpoint.Order.Value.OrderId:"NONE"):"NULL";
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var cashierRow=((IEnumerable)Read(employees,"employees")).Cast<object>().Single(row=>(EmployeeRole)Read(row,"Role")==EmployeeRole.Cashier);
            var actor=(NavigationActor)Read(cashierRow,"Actor");var cashierId=actor.ActorId;
            var oldPort=Read(cashierRow,"Port");var oldRunner=Read(cashierRow,"Runner");var oldSession=Read(employees,"session");
            var initialMove=Read(oldPort,"trafficMove");
            Assert.That(initialMove,Is.Not.Null,"FIXTURE NOT_PROVEN: retain the actual completed initial movement before any business task");
            var oldKey=(TrafficIntentKey)Read(initialMove,"Intent");var core=(ServiceTrafficCoordinator)Read(initialMove,"Coordinator");
            var entries=(IDictionary)Read(core,"entries");
            Assert.That(entries.Contains(oldKey),Is.True,"FIXTURE NOT_PROVEN: actual initial source must exist");
            var oldEntry=entries[oldKey];
            Assert.That(oldKey.ActorId,Is.EqualTo(cashierId));Assert.That(oldKey.IntentId,Is.EqualTo("employee-work"));
            Assert.That((bool)Read(oldEntry,"Retired"),Is.True,"PRODUCT: actual complete initial arrival retires its permission before any payment");
            var initialTombstone=(TrafficDecision)Read(oldEntry,"Decision");
            Assert.That(initialTombstone.Kind,Is.EqualTo(TrafficDecisionKind.Cancelled));
            Assert.That(initialTombstone.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.Terminal));
            Assert.That(initialTombstone.Reason,Is.EqualTo("intent-retired"));
            Assert.That(Read(oldEntry,"Request"),Is.Null);Assert.That(Read(oldEntry,"JourneyRequest"),Is.Null);
            Assert.That(Read(oldEntry,"JourneyPermit"),Is.Null);Assert.That(Read(oldEntry,"Leg"),Is.Null);
            yield return PrimePickupFamilyAdmission();
            var errors=new List<string>();var paid=0;var returned=0;var arrivals=0;long arrivalRequest=0;
            CafeTaskEvent? payment=null,homeReturn=null;object arrivalPort=null,arrivalRunner=null;TrafficIntentKey arrivalKey=default;
            TrafficRequest arrivalOriginalRequest=null;var arrivalSourceBound=false;var arrivalPose=Vector3.zero;
            var movementHook=typeof(EmployeeFlowController).GetField("MovementObserver",flags);
            Assert.That(movementHook,Is.Not.Null,"FIXTURE NOT_PROVEN: existing read-only Editor movement observer is required");
            var previousMovement=movementHook.GetValue(employees);var movementInstalled=false;
            void Task(CafeTaskEvent value)
            {
                if(value.Role==EmployeeRole.Cashier && value.Kind==CafeTaskEventKind.ServiceFinished){paid++;payment=value;}
                if(value.Role==EmployeeRole.Barista && value.Kind==CafeTaskEventKind.Returned){returned++;homeReturn=value;}
            }
            void Movement(string id,string operation,string details)
            {
                if(id!=cashierId || operation!="callback" || arrivals!=0 || details==null || !details.Contains(" status=Arrived "))return;
                try
                {
                    var port=Read(cashierRow,"Port");var runner=Read(cashierRow,"Runner");var leg=Read(runner,"moveLeg");
                    if(ReferenceEquals(port,oldPort) || leg==null || (EmployeeState)Read(leg,"State")!=EmployeeState.MovingToWork)return;
                    var move=Read(port,"trafficMove");if(move==null)throw new InvalidOperationException("actual new initial callback has no retained movement");
                    var request=details.Split(' ').Single(part=>part.StartsWith("request=",StringComparison.Ordinal)).Substring(8);
                    arrivalRequest=long.Parse(request,CultureInfo.InvariantCulture);arrivalPort=port;arrivalRunner=runner;
                    arrivalKey=(TrafficIntentKey)Read(move,"Intent");arrivals++;
                    var actualCore=(ServiceTrafficCoordinator)Read(move,"Coordinator");
                    var actualEntry=((IDictionary)Read(actualCore,"entries"))[arrivalKey];var actualLeg=Read(actualEntry,"Leg");
                    arrivalOriginalRequest=Read(actualEntry,"JourneyRequest") as TrafficRequest;arrivalPose=actor.transform.position;
                    arrivalSourceBound=arrivalOriginalRequest!=null && arrivalOriginalRequest.Intent.Equals(arrivalKey) &&
                        Read(move,"Source")!=null && ReferenceEquals(Read(actualEntry,"JourneyPermit"),Read(move,"Source")) &&
                        actualLeg!=null && (bool)Read(actualLeg,"Committed") && (bool)Read(actualLeg,"Bound") &&
                        !(bool)Read(actualLeg,"Completed") && ReferenceEquals(Read(actualLeg,"ActorLife"),actor) &&
                        ((TrafficNativeStamp)Read(actualLeg,"Stamp")).RequestId==arrivalRequest &&
                        ((TrafficNativeStamp)Read(actualLeg,"Stamp")).LegGeneration==(int)Read(leg,"NativeLegGeneration");
                    TestContext.WriteLine("CASHIER_RECREATION_NATIVE actualArrived=True key="+Key(arrivalKey)+" request="+arrivalRequest+
                        " port="+Ref(port)+" runner="+Ref(runner)+" nativeGeneration="+Read(leg,"NativeLegGeneration")+
                        " sourceBound="+arrivalSourceBound+" actualOriginalStart="+(arrivalOriginalRequest==null?"UNKNOWN":arrivalOriginalRequest.Start.ToString("F9"))+
                        " actualArrivedPose="+arrivalPose.ToString("F9")+" details="+details);
                }
                catch(Exception error){if(errors.Count<4)errors.Add(error.ToString());}
            }
            employees.IntegratedTaskEvent+=Task;
            try
            {
                Assert.That(customers.TrySpawn(),Is.True,customers.Status);var id=customers.Snapshot.Single().VisitId;
                bool HomeReady()=>paid==1 && returned==1 && owner.Snapshot.Orders.Count==1 &&
                    owner.Snapshot.Orders.Values.Single().State==OrderState.ReadyForPickup &&
                    customers.PickupAssignments.Count==1 && customers.PickupAssignments.Single().Arrived &&
                    employees.AreEmployeesAtWorkStations(out _);
                for(var frame=0;frame<6000 && !HomeReady();frame++)
                {
                    if(customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked) || employees.Snapshot.Any(e=>e.State==EmployeeState.Blocked))break;
                    yield return null;
                }
                TestContext.WriteLine("CASHIER_RECREATION_PREMISE homeReady="+HomeReady()+" paid="+paid+" returned="+returned+
                    " staff="+string.Join(";",employees.Snapshot.Select(e=>e.Role+"/"+e.State+"/"+e.Failure)));
                Assert.That(HomeReady(),Is.True,"FIXTURE NOT_PROVEN: real Cashier Finished and real Barista Returned/home must precede disable");
                Assert.That(payment.Value.ActorId,Is.EqualTo(cashierId));Assert.That(payment.Value.Checkpoint.VisitId,Is.EqualTo(id));
                Assert.That(payment.Value.Checkpoint.Stage,Is.EqualTo(CafeWorkStage.Finished));
                Assert.That(homeReturn.Value.Checkpoint.VisitId,Is.EqualTo(id));Assert.That(homeReturn.Value.Checkpoint.Stage,Is.EqualTo(CafeWorkStage.Finished));
                var visit=((IDictionary)Read(customers,"visits"))[id];var admission=Read(visit,"Admission");
                var order=owner.Snapshot.Orders.Single().Key;var nextVisit=Read(customers,"nextVisit");
                var tokens=customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray();
                Assert.That(tokens.Length,Is.GreaterThan(0));Assert.That(admission,Is.Not.Null);
                var savedToken=Read(cashierRow,"AcceptedToken");var savedCheckpoint=Read(cashierRow,"Checkpoint");
                Assert.That(savedToken,Is.Not.Null);Assert.That(savedCheckpoint,Is.Not.Null);
                Assert.That(((CafeWorkCheckpoint)savedCheckpoint).Stage,Is.EqualTo(CafeWorkStage.Finished));
                var retired=(TrafficDecision)Read(oldEntry,"Decision");
                Assert.That((bool)Read(oldEntry,"Retired"),Is.True,"PRODUCT: the initial tombstone stays retired through real payment and return");
                Assert.That(retired,Is.SameAs(initialTombstone),"Real payment must not resurrect or replace the retired initial tombstone");
                Assert.That(retired.Kind,Is.EqualTo(TrafficDecisionKind.Cancelled));Assert.That(retired.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.Terminal));
                Assert.That(retired.Reason,Is.EqualTo("intent-retired"));
                var home=actor.transform.position;var workTarget=((EmployeeStationBinding)Read(cashierRow,"Binding")).WorkTarget;
                TestContext.WriteLine("CASHIER_RECREATION_BEFORE oldKey="+Key(oldKey)+" oldEntry="+Ref(oldEntry)+" oldDecision="+retired.Kind+"/"+retired.FailureDisposition+
                    " oldPort="+Ref(oldPort)+" oldRunner="+Ref(oldRunner)+" session="+oldSession+" historyToken="+Token(savedToken)+" checkpoint="+Checkpoint(savedCheckpoint)+
                    " runnerHasCheckpoint="+Read(oldRunner,"hasCheckpoint")+" pose="+home.ToString("F9"));
                movementHook.SetValue(employees,Delegate.Combine(previousMovement as Delegate,(Action<string,string,string>)Movement));movementInstalled=true;
                employees.enabled=false;yield return null;
                Assert.That(actor.transform.position,Is.EqualTo(home),"FIXTURE: disabled home body remains unchanged before reactivation");
                employees.enabled=true;owner.ResumeBlockedFlow(); // One public recovery, never a Terminal polling retry.
                Assert.That(actor.transform.position,Is.EqualTo(home),"PRODUCT: synchronous enable/Resume cannot teleport before the first recovery frame");
                for(var frame=0;frame<600;frame++)
                {
                    var states=employees.Snapshot;
                    if(states.Any(e=>e.State==EmployeeState.Blocked) ||
                        states.All(e=>e.State==EmployeeState.Idle) && employees.AreEmployeesAtWorkStations(out _))break;
                    yield return null;
                }
                var newPort=Read(cashierRow,"Port");var newRunner=Read(cashierRow,"Runner");var newMove=Read(newPort,"trafficMove");
                var newContext=Read(newMove,"PreparationContext");var stamp=Read(newContext,"stamp");
                var sameKey=newMove!=null && ((TrafficIntentKey)Read(newMove,"Intent")).Equals(oldKey);
                var stateAfter=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier);
                string Current(object value)=>value==null?"UNKNOWN(no-original-seal)":value.GetType().GetProperty("IsCurrent",flags)?.GetValue(value)?.ToString()??"UNKNOWN(no-getter)";
                try
                {
                    TestContext.WriteLine("CASHIER_RECREATION_AFTER coreSame="+ReferenceEquals(Read(customers,"traffic"),core)+
                        " rowSame="+((IEnumerable)Read(employees,"employees")).Cast<object>().Any(row=>ReferenceEquals(row,cashierRow))+
                        " port="+Ref(newPort)+" oldPort="+Ref(oldPort)+" runner="+Ref(newRunner)+" oldRunner="+Ref(oldRunner)+
                        " session="+Read(employees,"session")+" oldSession="+oldSession+" state="+stateAfter.State+" failure="+stateAfter.Failure+
                        " actualNewKey="+(newMove==null?"UNKNOWN(no-retained-move)":Key((TrafficIntentKey)Read(newMove,"Intent")))+" sameOldKey="+sameKey+
                        " actualNewCoordinatorSame="+(newMove!=null && ReferenceEquals(Read(newMove,"Coordinator"),core))+
                        " actualNewEntry="+(newMove!=null && entries.Contains((TrafficIntentKey)Read(newMove,"Intent"))?Ref(entries[(TrafficIntentKey)Read(newMove,"Intent")]):"UNKNOWN(no-entry)")+
                        " oldEntrySame="+(entries.Contains(oldKey) && ReferenceEquals(entries[oldKey],oldEntry))+" oldRetired="+Read(oldEntry,"Retired")+
                        " oldDecision="+((TrafficDecision)Read(oldEntry,"Decision")).Kind+"/"+((TrafficDecision)Read(oldEntry,"Decision")).FailureDisposition+
                        " oldReason="+((TrafficDecision)Read(oldEntry,"Decision")).Reason+
                        " newHasCheckpoint="+Read(newRunner,"hasCheckpoint")+" actualToken="+Token(Read(cashierRow,"AcceptedToken"))+" actualCheckpoint="+Checkpoint(Read(cashierRow,"Checkpoint"))+
                        " runnerTokenStored="+Token(Read(newRunner,"taskToken"))+" runnerCheckpointStored="+Checkpoint(Read(newRunner,"checkpoint"))+
                        " pendingSource="+Ref(Read(newMove,"PendingSource"))+" selectedSource="+Ref(Read(newMove,"Source"))+
                        " context="+Ref(newContext)+" contextCurrent="+Current(newContext)+" stamp="+Ref(stamp)+" stampCurrent="+Current(stamp)+
                        " newLeg="+Ref(Read(newRunner,"moveLeg"))+" moveSequence="+Read(newPort,"workMoveSequence")+" actualNativeArrivals="+arrivals+
                        " pose="+actor.transform.position.ToString("F9")+" originalProducerSeal="+(newContext==null?"UNKNOWN(no-original-capture)":"ACTUAL_RETAINED_CONTEXT"));
                }
                catch(Exception error){errors.Add("Read-only final witness UNKNOWN: "+error);}
                Assert.That(errors,Is.Empty,"FIXTURE NOT_PROVEN: existing observer/read-only witness failed");
                Assert.That(newPort,Is.Not.Null);Assert.That(ReferenceEquals(newPort,oldPort),Is.False);
                Assert.That(newRunner,Is.Not.Null);Assert.That(ReferenceEquals(newRunner,oldRunner),Is.False);
                Assert.That(Read(employees,"session"),Is.Not.EqualTo(oldSession));
                Assert.That(Read(customers,"traffic"),Is.SameAs(core));Assert.That(entries[oldKey],Is.SameAs(oldEntry));
                Assert.That((bool)Read(oldEntry,"Retired"),Is.True);Assert.That(((TrafficDecision)Read(oldEntry,"Decision")).FailureDisposition,Is.EqualTo(TrafficFailureDisposition.Terminal));
                Assert.That(Read(cashierRow,"Actor"),Is.SameAs(actor));Assert.That(world.RegisteredActors.Single(a=>a.ActorId==cashierId),Is.SameAs(actor));
                Assert.That(actor.isActiveAndEnabled,Is.True);
                Assert.That(Vector3.Distance(actor.transform.position,workTarget.Position),Is.LessThanOrEqualTo(actor.Settings.ArrivalDistance));
                Assert.That(((IDictionary)Read(customers,"visits"))[id],Is.SameAs(visit));Assert.That(Read(visit,"Admission"),Is.SameAs(admission));
                Assert.That(Read(customers,"nextVisit"),Is.EqualTo(nextVisit));Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(1));
                Assert.That(owner.Snapshot.Orders.Single().Key,Is.EqualTo(order));Assert.That(owner.Snapshot.Orders[order].State,Is.EqualTo(OrderState.ReadyForPickup));
                CollectionAssert.AreEquivalent(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray());
                Assert.That(Read(cashierRow,"AcceptedToken"),Is.EqualTo(savedToken));Assert.That(Read(cashierRow,"Checkpoint"),Is.EqualTo(savedCheckpoint));
                Assert.That(paid,Is.EqualTo(1));Assert.That(returned,Is.EqualTo(1));
                Assert.That(stateAfter.State,Is.EqualTo(EmployeeState.Idle),"PRODUCT: public EFC recreation/one Owner Resume must restore Finished Cashier at the real workstation; "+stateAfter.Failure);
                Assert.That(newMove,Is.Not.Null,"PRODUCT: recreated initial movement must retain its actual source identity");
                Assert.That(sameKey,Is.False,"PRODUCT: new lifecycle cannot consume or resurrect the old retired initial intent");
                Assert.That(arrivals,Is.EqualTo(1),"PRODUCT: new initial return requires exactly one real Native Arrived, even when already at home");
                Assert.That(arrivalRequest,Is.GreaterThan(0));Assert.That(arrivalPort,Is.SameAs(newPort));Assert.That(arrivalRunner,Is.SameAs(newRunner));
                Assert.That(arrivalKey,Is.EqualTo((TrafficIntentKey)Read(newMove,"Intent")));
                Assert.That(arrivalSourceBound,Is.True,"PRODUCT: real new initial Arrived must retain its committed/bound source and actual actor/request generation");
                Assert.That(arrivalOriginalRequest,Is.Not.Null);Assert.That(arrivalOriginalRequest.Start,Is.EqualTo(home));
                var completedInitialEntry=entries[(TrafficIntentKey)Read(newMove,"Intent")];
                Assert.That((bool)Read(completedInitialEntry,"Retired"),Is.True,"Actual recreated initial completion also retires only its complete permission");
                Assert.That(Read(completedInitialEntry,"Request"),Is.Null);Assert.That(Read(completedInitialEntry,"JourneyRequest"),Is.Null);
                Assert.That(Read(completedInitialEntry,"JourneyPermit"),Is.Null);Assert.That(Read(completedInitialEntry,"Leg"),Is.Null);
                Assert.That(Vector3.Distance(arrivalPose,workTarget.Position),Is.LessThanOrEqualTo(actor.Settings.ArrivalDistance));
                Assert.That(employees.AreEmployeesAtWorkStations(out var reason),Is.True,reason);
            }
            finally
            {
                employees.IntegratedTaskEvent-=Task;
                if(movementInstalled)movementHook.SetValue(employees,previousMovement);
            }
        }

        private TrafficWorldSnapshot CurrentTrafficFacts()
            => (TrafficWorldSnapshot)typeof(CustomerFlowController).GetMethod("RefreshTrafficSnapshot",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(customers,null);

        [UnityTest] public IEnumerator OpenDriverGateChecksPreserveOriginalUnbornAdmission()
            => ObserveClosingAdmissionOpportunity("open");
        [UnityTest] public IEnumerator PauseAlonePreservesOriginalUnbornAdmission()
            => ObserveClosingAdmissionOpportunity("pause");
        [UnityTest] public IEnumerator ClosingRetiresUnbornAndNextDayAdmitsFreshOpportunityOnce()
            => ObserveClosingAdmissionOpportunity("closing");

        private sealed class ClosingAdmissionClock
        {
            internal double Now;
            internal double Read()=>Now;
        }
        private sealed class ClosingAdmissionProbe
        {
            internal object Anchor,Entry;
            internal ServiceTrafficCoordinator Coordinator;
            internal TrafficIntentKey Key;
            internal TrafficDecision Permission;
            internal object[] Ledger;
        }
        private static readonly string[] ClosingAdmissionLedgerFields=
            {"FirstFrame","RequestSequence","AcquiredSequence","Queries","WaitRemaining","FiniteLimit","CurrentIndex","CandidateCount"};
        private static object[] ClosingAdmissionLedger(object entry)
            =>ClosingAdmissionLedgerFields.Select(name=>MovementRead(entry,name)).ToArray();
        private void AssertNoUnbornAdmissionAllocation(TrafficIntentKey key)
        {
            Assert.That(customers.Snapshot,Is.Empty);Assert.That(customers.CounterAssignments,Is.Empty);
            Assert.That(world.RegisteredActors.Any(actor=>actor.ActorId==key.ActorId),Is.False);
            Assert.That(customers.GetComponentsInChildren<NavigationActor>(true).Any(actor=>actor.ActorId==key.ActorId),Is.False);
            Assert.That(customers.Capacity.GetReservations().Any(row=>row.OwnerId==key.ActorId),Is.False);
        }
        private IEnumerator PrepareRealUnbornAdmission(ClosingAdmissionProbe probe)
        {
            Assert.That(customers.AutoSpawn,Is.False,"FIXTURE: the existing manual-admission scene must not race a real birth");
            Assert.That(owner.CanProgress,Is.True);Assert.That(customers.Snapshot,Is.Empty);
            Assert.That(customers.TrySpawn(),Is.False,"FIXTURE: a new traffic proposal must await the original ready pump");
            probe.Anchor=MovementRead(customers,"pendingAdmissionAnchor");Assert.That(probe.Anchor,Is.Not.Null);
            Assert.That(MovementRead(probe.Anchor,"Submitted"),Is.EqualTo(true));
            probe.Coordinator=(ServiceTrafficCoordinator)MovementRead(probe.Anchor,"Coordinator");
            probe.Key=(TrafficIntentKey)MovementRead(probe.Anchor,"Key");
            var entries=(IDictionary)MovementRead(probe.Coordinator,"entries");Assert.That(entries.Contains(probe.Key),Is.True);
            probe.Entry=entries[probe.Key];AssertNoUnbornAdmissionAllocation(probe.Key);
            // Only normal owner frames process this exact proposal; no second TrySpawn can create an actor here.
            // 仅等待原 driver 核验；不直写状态、不直接调用 Core ProcessReady、不造 Queue/token。
            for(var frame=0;frame<600 && probe.Coordinator.GetDecision(probe.Key).Kind!=TrafficDecisionKind.Granted;frame++)yield return null;
            probe.Permission=probe.Coordinator.GetDecision(probe.Key);
            Assert.That(probe.Permission.Kind,Is.EqualTo(TrafficDecisionKind.Granted),"FIXTURE: actual normal driver must prepare the original admission");
            Assert.That(MovementRead(customers,"pendingAdmissionAnchor"),Is.SameAs(probe.Anchor));
            Assert.That(MovementRead(probe.Entry,"Retired"),Is.EqualTo(false));Assert.That(probe.Coordinator.HasActiveGrant(probe.Key),Is.False);
            AssertNoUnbornAdmissionAllocation(probe.Key);probe.Ledger=ClosingAdmissionLedger(probe.Entry);
        }
        private IEnumerator ObserveClosingAdmissionOpportunity(string mode)
        {
            Assert.That(MovementRead(customers,"admissionOwner"),Is.Null,"FIXTURE: P14 scene has no competing BusinessDay owner");
            var speed=time.CurrentSpeed;var clock=new ClosingAdmissionClock();
            var day=owner.gameObject.AddComponent<AnimalCafe.BusinessDay.BusinessDayController>();
            var beforeNext=(long)MovementRead(customers,"nextVisit");var session=MovementRead(customers,"session");
            var epoch=MovementRead(customers,"trafficEpoch");var probe=new ClosingAdmissionProbe();TrafficIntentKey admittedKey=default;
            try
            {
                day.Initialize(owner,new AnimalCafe.BusinessDay.BusinessDaySettings(10,5),clock.Read,true);
                yield return PrepareRealUnbornAdmission(probe);
                var queue=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue");var queueVersion=queue.Version;
                var facts=MovementAdmissionFacts();
                if(mode=="closing")
                {
                    time.SetPaused();clock.Now=10;day.BeforeService();
                    Assert.That(day.Snapshot.Phase,Is.EqualTo(AnimalCafe.BusinessDay.BusinessDayPhase.Closing));
                    Assert.That(MovementRead(probe.Entry,"Retired"),Is.EqualTo(true),
                        "PRODUCT: real Closing must retire its unborn pending admission even while scene time is Paused");
                    Assert.That(MovementRead(probe.Entry,"Request"),Is.Null);Assert.That(probe.Coordinator.GetDecision(probe.Key).Kind,Is.EqualTo(TrafficDecisionKind.Cancelled));
                    Assert.That(MovementRead(customers,"pendingAdmissionAnchor"),Is.Null);AssertNoUnbornAdmissionAllocation(probe.Key);
                    Assert.That(queue.Version,Is.EqualTo(queueVersion));Assert.That(MovementAdmissionFacts(),Is.EqualTo(facts));
                    CollectionAssert.AreEqual(probe.Ledger,ClosingAdmissionLedger(probe.Entry),"Retirement cannot refill or erase the original ledger");
                    Assert.That(probe.Coordinator.TryCommit(probe.Permission),Is.False);
                    day.BeforeService();CollectionAssert.AreEqual(probe.Ledger,ClosingAdmissionLedger(probe.Entry),"Repeated real Closing checks are idempotent");
                    Assert.That(time.TrySetSpeed(speed),Is.True);
                    for(var frame=0;frame<600 && day.Snapshot.Phase!=AnimalCafe.BusinessDay.BusinessDayPhase.Rest;frame++)yield return null;
                    Assert.That(day.Snapshot.Phase,Is.EqualTo(AnimalCafe.BusinessDay.BusinessDayPhase.Rest),owner.GetDrainSnapshot().Reason);
                    Assert.That(owner.GetDrainSnapshot().IsDrained,Is.True,"Only the real drain result may open Rest; no fabricated AfterService input");
                    Assert.That(day.Snapshot.LastClosedDay.DayNumber,Is.EqualTo(1));
                    clock.Now=15;day.BeforeService();
                    Assert.That(day.Snapshot.Phase,Is.EqualTo(AnimalCafe.BusinessDay.BusinessDayPhase.Open));Assert.That(day.Snapshot.DayNumber,Is.EqualTo(2));
                    Assert.That(MovementRead(customers,"nextVisit"),Is.EqualTo(beforeNext));
                    Assert.That(customers.TrySpawn(),Is.False,"A genuine new opportunity must use normal traffic preparation again");
                    var fresh=MovementRead(customers,"pendingAdmissionAnchor");Assert.That(fresh,Is.Not.Null);Assert.That(fresh,Is.Not.SameAs(probe.Anchor));
                    admittedKey=(TrafficIntentKey)MovementRead(fresh,"Key");
                    Assert.That(admittedKey,Is.Not.EqualTo(probe.Key),"PRODUCT: reopening cannot resubmit the same retired session-nextVisit key");
                    Assert.That(admittedKey.ActorId,Is.EqualTo(probe.Key.ActorId));Assert.That(admittedKey.Generation,Is.GreaterThan(probe.Key.Generation));
                    Assert.That(admittedKey.Session,Is.EqualTo(probe.Key.Session));Assert.That(admittedKey.Epoch,Is.EqualTo(probe.Key.Epoch));
                    Assert.That(MovementRead(fresh,"Coordinator"),Is.SameAs(probe.Coordinator));
                    CollectionAssert.AreEqual(probe.Ledger,ClosingAdmissionLedger(probe.Entry));AssertNoUnbornAdmissionAllocation(admittedKey);
                }
                else
                {
                    if(mode=="pause"){time.SetPaused();owner.Step(0);day.BeforeService();}
                    else for(var index=0;index<3;index++){clock.Now=index+1;day.BeforeService();}
                    Assert.That(day.Snapshot.Phase,Is.EqualTo(AnimalCafe.BusinessDay.BusinessDayPhase.Open));
                    Assert.That(MovementRead(customers,"pendingAdmissionAnchor"),Is.SameAs(probe.Anchor));
                    Assert.That(MovementRead(probe.Entry,"Retired"),Is.EqualTo(false),"Open fail-closed gate checks or Pause alone cannot cancel the same opportunity");
                    Assert.That((TrafficIntentKey)MovementRead(probe.Anchor,"Key"),Is.EqualTo(probe.Key));
                    CollectionAssert.AreEqual(probe.Ledger,ClosingAdmissionLedger(probe.Entry));AssertNoUnbornAdmissionAllocation(probe.Key);
                    Assert.That(queue.Version,Is.EqualTo(queueVersion));Assert.That(MovementAdmissionFacts(),Is.EqualTo(facts));
                    if(mode=="pause"){Assert.That(time.TrySetSpeed(speed),Is.True);owner.Step(0);}
                    admittedKey=probe.Key;
                }
                yield return AwaitTrafficAdmission("The actual original/new opportunity must admit through the normal public policy and driver path");
                Assert.That(MovementRead(customers,"nextVisit"),Is.EqualTo(beforeNext+1));
                Assert.That(MovementRead(customers,"session"),Is.EqualTo(session));Assert.That(MovementRead(customers,"trafficEpoch"),Is.EqualTo(epoch));
                Assert.That(customers.Snapshot.Select(row=>row.VisitId),Is.EqualTo(new[]{admittedKey.ActorId}));
                Assert.That(world.RegisteredActors.Count(actor=>actor.ActorId==admittedKey.ActorId),Is.EqualTo(1));
                Assert.That(customers.Capacity.GetReservations().Count(row=>row.OwnerId==admittedKey.ActorId),Is.EqualTo(3));
                for(var frame=0;frame<12;frame++)yield return null;
                Assert.That(MovementRead(customers,"nextVisit"),Is.EqualTo(beforeNext+1),"The new opportunity cannot create a duplicate canonical visit");
                if(mode=="closing")
                {
                    Assert.That(MovementRead(probe.Entry,"Retired"),Is.EqualTo(true));
                    CollectionAssert.AreEqual(probe.Ledger,ClosingAdmissionLedger(probe.Entry));
                    Assert.That(((IDictionary)MovementRead(probe.Coordinator,"entries")).Contains(probe.Key),Is.True,"Old retired key remains diagnosis/lookup-visible");
                }
            }
            finally
            {
                if(time.CurrentSpeed==GameSpeed.Paused)time.TrySetSpeed(speed);
                if(day!=null)UnityEngine.Object.DestroyImmediate(day);
            }
        }

        [UnityTest] public IEnumerator AdmissionPolicyRefusalBeforeCanonicalBirthResumesOriginalProposalExactlyOnce()
        {
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var queue=CafeLoopRuntimeProof.Field<CounterQueueService>(customers,"queue");
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var previous=hook.GetValue(customers);var originalOwner=(UnityEngine.Object)MovementRead(customers,"admissionOwner");
            var originalPolicy=(Func<bool>)MovementRead(customers,"canAdmit");var originallyRequired=customers.RequiresAdmissionPolicy;
            var policyOwner=originalOwner!=null?originalOwner:customers;
            var errors=new List<string>();var checks=new List<bool>();var publications=new List<string>();
            var keys=new List<TrafficIntentKey>();var permissions=new List<TrafficDecision>();var accounting=new List<object[]>();
            var targets=new List<Vector3>();var endpoints=new List<Vector3>();var attempts=new List<int[]>();
            var fields=new[]{"FirstFrame","RequestSequence","AcquiredSequence","Queries","WaitRemaining","FiniteLimit","CurrentIndex","CandidateCount"};
            var gateClosed=false;var withinSpawn=false;var refused=false;var accepted=false;var refusalReturned=false;
            bool[] checksBeforeRefusal=null,refusalChecks=null;object entry=null,resumedEntry=null,anchor=null,frozen=null,resources=null,edges=null;
            NavigationActor rejectedActor=null;TrafficRequest originalRequest=null;string beforeFacts=null;long beforeQueueVersion=0;
            var originalNext=(long)MovementRead(customers,"nextVisit");var originalSession=owner.Snapshot.SessionId;var originalEpoch=MovementRead(customers,"trafficEpoch");
            bool Policy()
            {
                // Preserve the original policy; only the real final precommit check sees this temporary refusal.
                // 保留原 policy 判断；公开注册的临时门禁只在真实 birth-ready 后关闭。
                var allowed=(originalPolicy!=null?originalPolicy():!originallyRequired) && !gateClosed;
                if(withinSpawn)checks.Add(allowed);return allowed;
            }
            void TryOriginalSpawn()
            {
                checks.Clear();withinSpawn=true;
                try{accepted=customers.TrySpawn();}finally{withinSpawn=false;}
            }
            void Observe(string operation,string id,string other,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                try
                {
                    if(operation=="admission.published"){publications.Add(id);return;}
                    if(operation!="admission.birth-ready")return;
                    if(keys.Count>=2){errors.Add("Unexpected third birth-ready attempt");return;}
                    var entries=CafeLoopRuntimeProof.Field<IDictionary>(coordinator,"entries");
                    var key=entries.Keys.Cast<TrafficIntentKey>().Single(k=>k.ActorId==result && k.IntentId=="counter-admission");
                    var current=entries[key];var decision=coordinator.GetDecision(key);var request=(TrafficRequest)MovementRead(current,"Request");
                    keys.Add(key);permissions.Add(decision);accounting.Add(fields.Select(name=>MovementRead(current,name)).ToArray());
                    targets.Add(target);endpoints.Add(decision.ProofRoutes.Single(p=>p.ResourceId=="counter/"+result).Waypoints.Last());
                    attempts.Add(((HashSet<int>)MovementRead(current,"AttemptedIndices")).ToArray());
                    if(refused){resumedEntry=current;return;}
                    entry=current;originalRequest=request;anchor=MovementRead(customers,"pendingAdmissionAnchor");
                    frozen=MovementRead(entry,"FrozenOwners");resources=MovementRead(entry,"FrozenResources");edges=MovementRead(entry,"WaitingBlockers");
                    rejectedActor=customers.GetComponentsInChildren<NavigationActor>(true).Single(actor=>actor.ActorId==result);
                    beforeFacts=MovementAdmissionFacts();beforeQueueVersion=queue.Version;checksBeforeRefusal=checks.ToArray();
                    refused=true;gateClosed=true;
                }
                catch(Exception error){if(errors.Count<8)errors.Add(error.ToString());}
            }
            try
            {
                customers.AutoSpawn=false;
                if(originalOwner!=null)customers.UnregisterAdmissionPolicy(originalOwner);
                customers.RegisterAdmissionPolicy(policyOwner,Policy);
                hook.SetValue(customers,Delegate.Combine(previous as Delegate,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)Observe));
                for(var frame=0;frame<600 && !refused && errors.Count==0;frame++)
                {
                    TryOriginalSpawn();
                    if(refused){refusalReturned=accepted;refusalChecks=checks.ToArray();break;}
                    yield return null;
                }
                Assert.That(errors,Is.Empty,"Observer errors are reported outside product callbacks");
                Assert.That(refused,Is.True,"FIXTURE: original P14 ready pump must reach the real final birth policy check");
                CollectionAssert.AreEqual(new[]{true},checksBeforeRefusal,"The first policy check allowed this actual TrySpawn call");
                CollectionAssert.AreEqual(new[]{true,false},refusalChecks,"The original final policy check refused the same call");
                Assert.That(refusalReturned,Is.False);Assert.That(publications,Is.Empty);Assert.That(MovementAdmissionFacts(),Is.EqualTo(beforeFacts));
                Assert.That(queue.Version,Is.EqualTo(beforeQueueVersion));Assert.That(customers.CounterAssignments,Is.Empty);
                Assert.That(customers.Snapshot,Is.Empty);Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                Assert.That(customers.Capacity.GetReservations().Any(r=>r.OwnerId==keys[0].ActorId),Is.False);
                Assert.That(world.RegisteredActors.Any(actor=>actor.ActorId==keys[0].ActorId),Is.False);
                CollectionAssert.AreEqual(accounting[0],fields.Select(name=>MovementRead(entry,name)).ToArray(),"Refusal cannot renew any original accounting");
                Assert.That(MovementRead(customers,"pendingAdmissionAnchor"),Is.SameAs(anchor));
                Assert.That(MovementRead(entry,"FrozenOwners"),Is.SameAs(frozen));Assert.That(MovementRead(entry,"FrozenResources"),Is.SameAs(resources));
                Assert.That(MovementRead(entry,"WaitingBlockers"),Is.SameAs(edges));
                Assert.That(coordinator.TryCommit(permissions[0]),Is.False,"The refused birth permission is invalid immediately");
                Assert.That(coordinator.HasActiveGrant(keys[0]),Is.False);Assert.That(MovementRead(entry,"EverCanonicalAttempt"),Is.EqualTo(false));
                TestContext.WriteLine("FIX5_REFUSED key="+keys[0].ActorId+"/"+keys[0].IntentId+"/g"+keys[0].Generation+
                    " first="+accounting[0][0]+" sequence="+accounting[0][1]+" queries="+accounting[0][3]+" wait="+accounting[0][4]+
                    " decision="+coordinator.GetDecision(keys[0]).Kind+"/"+coordinator.GetDecision(keys[0]).FailureDisposition+" queueVersion="+queue.Version+" canonical="+beforeFacts);
                yield return null;
                Assert.That(rejectedActor==null,Is.True,"The rejected temporary actor must actually be destroyed before recovery");
                gateClosed=false;
                for(var frame=0;frame<600 && !accepted && errors.Count==0;frame++){TryOriginalSpawn();if(!accepted)yield return null;}
                TestContext.WriteLine("FIX5_RECOVERED accepted="+accepted+" ready="+keys.Count+" published="+publications.Count+
                    " originalDecision="+coordinator.GetDecision(keys[0]).Kind+"/"+coordinator.GetDecision(keys[0]).FailureDisposition+
                    " originalQueries="+coordinator.QueriesConsumed(keys[0])+" sameSession="+(owner.Snapshot.SessionId==originalSession)+
                    " next="+MovementRead(customers,"nextVisit")+" errors="+string.Join(";",errors));
                Assert.That(errors,Is.Empty);
                Assert.That(accepted,Is.True,"PRODUCT: recovery of the original policy must resume the same pre-canonical admission proposal");
                Assert.That(keys,Has.Count.EqualTo(2));Assert.That(keys[1],Is.EqualTo(keys[0]));Assert.That(resumedEntry,Is.SameAs(entry));
                Assert.That(permissions[1],Is.Not.SameAs(permissions[0]));CollectionAssert.AreEqual(targets,endpoints);
                Assert.That(originalRequest.RequestedFrame,Is.EqualTo(accounting[0][0]));
                for(var i=0;i<fields.Length;i++)
                    if(fields[i]!="Queries" && fields[i]!="WaitRemaining")Assert.That(accounting[1][i],Is.EqualTo(accounting[0][i]),fields[i]);
                Assert.That((int)accounting[1][3],Is.GreaterThan((int)accounting[0][3]),"Recovered birth requires fresh proof with cumulative query spending");
                Assert.That((float)accounting[1][4],Is.LessThanOrEqualTo((float)accounting[0][4]));CollectionAssert.AreEquivalent(attempts[0],attempts[1]);
                Assert.That(owner.Snapshot.SessionId,Is.EqualTo(originalSession));Assert.That(MovementRead(customers,"trafficEpoch"),Is.EqualTo(originalEpoch));
                Assert.That(MovementRead(customers,"nextVisit"),Is.EqualTo(originalNext+1));Assert.That(publications,Is.EqualTo(new[]{keys[0].ActorId}));
                Assert.That(customers.Snapshot.Select(v=>v.VisitId),Is.EqualTo(new[]{keys[0].ActorId}));
                Assert.That(world.RegisteredActors.Count(actor=>actor.ActorId==keys[0].ActorId),Is.EqualTo(1));
                Assert.That(customers.Capacity.GetReservations().Count(r=>r.OwnerId==keys[0].ActorId),Is.EqualTo(3));
                Assert.That(coordinator.TryCommit(permissions[0]),Is.False);
                for(var frame=0;frame<12;frame++)yield return null;
                Assert.That(errors,Is.Empty);Assert.That(publications,Is.EqualTo(new[]{keys[0].ActorId}));
                Assert.That(MovementRead(customers,"nextVisit"),Is.EqualTo(originalNext+1));Assert.That(keys,Has.Count.EqualTo(2));
            }
            finally
            {
                hook.SetValue(customers,previous);customers.AutoSpawn=false;customers.UnregisterAdmissionPolicy(policyOwner);
                if(originalOwner!=null && originalPolicy!=null)customers.RegisterAdmissionPolicy(originalOwner,originalPolicy);
            }
        }

        [UnityTest] public IEnumerator LogicalCounterFrontAtTailCannotPreparePickupBeforeActualHeadArrival()
            => ObservePhysicalCounterHeadEligibility(true);
        [UnityTest] public IEnumerator ActualCounterSlotZeroArrivalAllowsOriginalPickupReservationAndCashier()
            => ObservePhysicalCounterHeadEligibility(false);

        private IEnumerator ObservePhysicalCounterHeadEligibility(bool tailCase)
        {
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var corridorHook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);
            var pickupHook=typeof(CustomerFlowController).GetField("PickupObserver",flags);
            var priorCorridor=corridorHook.GetValue(customers);var priorPickup=pickupHook.GetValue(customers);
            var admitted=new List<string>();var errors=new List<string>();var records=new List<string>();var earlyParking=new HashSet<TrafficIntentKey>();
            string tested=null,first=null;object visit=null,admission=null;NavigationActor actor=null;CapacityToken[] originalTokens=null;
            var logicalTail=false;var tailAfterRead=false;var tailReadPending=false;var tailAttempts=0;var earlyCashier=false;
            var headAttempt=false;var reservePublished=false;var working=false;var actualArrived=false;var callbackArrived=false;var bound=false;var nativeLive=false;
            var arrivalFrame=-1;var reserveFrame=-1;var workingFrame=-1;var tailSlot=-1;var tailGeneration=0;
            long nativeRequest=0;var nativeGeneration=0;TrafficNativeStamp stamp=default;
            Vector3 tailPose=default,tailTarget=default,headPosition=default,arrivalPosition=default,nativeTarget=default;
            QueueAssignment arrivedAssignment=default;var phase=tailCase?CustomerVisitState.Advancing:CustomerVisitState.Entering;
            object Read(object value,string name)=>value==null?null:MovementRead(value,name);
            void Record(string value){if(records.Count<32)records.Add("frame="+Time.frameCount+" "+value);}
            void Safe(Action action){try{action();}catch(Exception error){if(errors.Count<16)errors.Add(error.ToString());}}
            long Number(string details,string prefix)
            {
                var part=(details??"").Split(' ').FirstOrDefault(p=>p.StartsWith(prefix,StringComparison.Ordinal));
                return part!=null && long.TryParse(part.Substring(prefix.Length),out var value)?value:0;
            }
            void ReadAfterTailAttempt()
            {
                if(!tailReadPending)return;tailReadPending=false;tailAfterRead=true;
                var entries=CafeLoopRuntimeProof.Field<IDictionary>(coordinator,"entries");
                foreach(var key in entries.Keys.Cast<TrafficIntentKey>().Where(k=>k.ActorId==tested))
                {
                    var entry=entries[key];var request=(TrafficRequest)Read(entry,"Request");
                    if((bool)Read(entry,"Parking") && request.ResourceId=="pickup/"+tested && earlyParking.Add(key))
                        Record("EARLY_PARKING phase="+key.IntentId+" generation="+key.Generation+" first="+Read(entry,"FirstFrame")+
                            " start="+request.Start.ToString("F9")+" acquired="+((TrafficDecision)Read(entry,"Decision")).AcquiredSequence);
                }
            }
            void ObserveCorridor(string operation,string id,string other,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string details)=>Safe(()=>
            {
                if(operation=="admission.published" && !admitted.Contains(id))
                {
                    admitted.Add(id);if(first==null)first=id;
                    if(admitted.Count==(tailCase?2:1))
                    {
                        tested=id;visit=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[id];actor=(NavigationActor)Read(visit,"Actor");admission=Read(visit,"Admission");
                        originalTokens=customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray();
                    }
                }
                if(id!=tested || visit==null)return;
                var queue=(CounterQueueService)Read(customers,"queue");var rows=queue.Snapshot;
                headPosition=((Vector3[])Read(queue,"slots"))[0];
                if(operation=="reserve.attempt" && rows.Count>0 && rows[0].VisitId==id && rows[0].Arrived &&
                    (CustomerVisitState)Read(visit,"State")==CustomerVisitState.Queued && (bool)Read(visit,"CounterArrived"))
                {
                    var assigned=rows[0];
                    if(assigned.SlotIndex==1 && !(bool)Read(visit,"PickupAssigned"))
                    {
                        tailAttempts++;tailReadPending=true;
                        if(!logicalTail)
                        {
                            logicalTail=true;tailSlot=assigned.SlotIndex;tailGeneration=assigned.Generation;tailTarget=assigned.Position;tailPose=actor.transform.position;
                            Record("LOGICAL_HEAD_AT_TAIL slot="+tailSlot+" arrived="+assigned.Arrived+" generation="+tailGeneration+
                                " pose="+tailPose.ToString("F9")+" assignment="+tailTarget.ToString("F9")+" actualHead="+headPosition.ToString("F9")+
                                " firstRemoved="+rows.All(a=>a.VisitId!=first)+" firstPaid="+owner.Snapshot.PaidVisitIds.Contains(first));
                        }
                    }
                    if(assigned.SlotIndex==0 && actualArrived && assigned.Generation==arrivedAssignment.Generation)headAttempt=true;
                }
                if(operation=="reserve.result" || operation=="reserve.traffic-decision")ReadAfterTailAttempt();
                if(operation=="segment.submitted" && (details??"").Contains("accepted=True") && (CustomerVisitState)Read(visit,"State")==phase)
                {
                    var move=Read(visit,"TrafficMove");var proof=(TrafficRouteProof)Read(move,"Proof");
                    if((int)Read(move,"Index")!=proof.Waypoints.Count-1)return;
                    var key=(TrafficIntentKey)Read(move,"Intent");var entry=CafeLoopRuntimeProof.Field<IDictionary>(coordinator,"entries")[key];var leg=Read(entry,"Leg");
                    nativeRequest=Number(details,"request=");nativeGeneration=(int)Read(move,"NativeGeneration");stamp=(TrafficNativeStamp)Read(leg,"Stamp");nativeTarget=target;
                    bound=(bool)Read(leg,"Committed") && (bool)Read(leg,"Bound") && stamp.Equals(new TrafficNativeStamp(nativeRequest,nativeGeneration));
                    var live=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor");nativeLive=live.Contains(id) && (long)Read(live[id],"Id")==nativeRequest;
                    Record("HEAD_NATIVE phase="+phase+" request="+nativeRequest+" bound="+bound+" target="+target.ToString("F9"));
                }
                if(operation=="segment.callback-result" && nativeRequest>0 && Number(details,"request=")==nativeRequest)
                {callbackArrived=(details??"").Contains("status=Arrived");arrivalPosition=target;}
                if(operation=="arrival.canonical-committed" && nativeRequest>0 && Number(details,"request=")==nativeRequest && (details??"").Contains("phase="+phase))
                {
                    actualArrived=true;arrivalFrame=Time.frameCount;arrivedAssignment=queue.Snapshot.Single(a=>a.VisitId==id);
                    Record("HEAD_ACTUAL_ARRIVED slot="+arrivedAssignment.SlotIndex+" arrived="+arrivedAssignment.Arrived+" generation="+arrivedAssignment.Generation+
                        " pose="+actor.transform.position.ToString("F9")+" callback="+callbackArrived);
                }
            });
            void ObservePickup(string operation,string id,PickupQueueService queue,QueueAssignment assignment)=>Safe(()=>
            {
                if(id!=tested)return;ReadAfterTailAttempt();
                if(operation!="reserve.published")return;
                reservePublished=true;reserveFrame=Time.frameCount;
                Record("PICKUP_RESERVED afterActualHead="+actualArrived+" counterSlot="+customers.CounterAssignments.Single(a=>a.VisitId==id).SlotIndex);
            });
            void ObserveCashier(CafeTaskEvent value)=>Safe(()=>
            {
                if(value.Role!=EmployeeRole.Cashier || value.Checkpoint.VisitId!=tested || value.Kind!=CafeTaskEventKind.Progress ||
                    value.Checkpoint.Stage!=CafeWorkStage.Working || value.Checkpoint.RemainingWorkSeconds<=0)return;
                if(!actualArrived)earlyCashier=true;
                if(!working){working=true;workingFrame=Time.frameCount;Record("CASHIER_WORKING afterActualHead="+actualArrived+" handle="+value.HandleId);}
            });
            try
            {
                customers.AutoSpawn=false;
                corridorHook.SetValue(customers,Delegate.Combine(priorCorridor as Delegate,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)ObserveCorridor));
                pickupHook.SetValue(customers,Delegate.Combine(priorPickup as Delegate,(Action<string,string,PickupQueueService,QueueAssignment>)ObservePickup));
                employees.IntegratedTaskEvent+=ObserveCashier;
                for(var frame=0;frame<6000 && !actualArrived && errors.Count==0;frame++)
                {
                    if(admitted.Count<(tailCase?2:1))customers.TrySpawn();
                    yield return null;Safe(ReadAfterTailAttempt);
                }
                TestContext.WriteLine("PHYSICAL_COUNTER_HEAD tailCase="+tailCase+" admitted="+admitted.Count+" logicalTail="+logicalTail+
                    " tailAttempts="+tailAttempts+" earlyParking="+earlyParking.Count+" headArrived="+actualArrived+" earlyCashier="+earlyCashier);
                foreach(var record in records)TestContext.WriteLine("PHYSICAL_HEAD_WITNESS "+record);
                Assert.That(errors,Is.Empty,"Read-only head eligibility observers must not alter runtime callbacks");
                Assert.That(admitted.Count,Is.EqualTo(tailCase?2:1));Assert.That(actor,Is.Not.Null);Assert.That(originalTokens,Has.Length.EqualTo(3));
                if(tailCase)
                {
                    Assert.That(logicalTail && tailAfterRead,Is.True,"FIXTURE: original public Reserve was requested for the actual logical front still at slot1");
                    Assert.That(tailSlot,Is.EqualTo(1));Assert.That(Vector3.Distance(tailPose,tailTarget),Is.LessThanOrEqualTo(actor.Settings.ArrivalDistance));
                    Assert.That(Vector3.Distance(tailPose,headPosition),Is.GreaterThan(actor.Settings.ArrivalDistance));
                }
                Assert.That(actualArrived && callbackArrived && bound && nativeLive,Is.True,"FIXTURE: matching real accepted/bound final Counter Native Arrived and canonical Mark");
                Assert.That(nativeRequest,Is.GreaterThan(0));Assert.That(stamp.LegGeneration,Is.EqualTo(nativeGeneration));
                Assert.That(arrivedAssignment.SlotIndex,Is.Zero);Assert.That(arrivedAssignment.Arrived,Is.True);
                Assert.That(arrivedAssignment.Position,Is.EqualTo(headPosition));Assert.That(Vector3.Distance(arrivalPosition,nativeTarget),Is.LessThanOrEqualTo(actor.Settings.ArrivalDistance));
                Assert.That(Vector3.Distance(arrivalPosition,headPosition),Is.LessThanOrEqualTo(actor.Settings.ArrivalDistance));
                if(tailCase)Assert.That(arrivedAssignment.Generation,Is.GreaterThan(tailGeneration));
                Assert.That(earlyParking,Is.Empty,"PRODUCT: an unowned pickup parking proposal cannot begin at the old tail before actual Counter slot0 arrival");
                Assert.That(earlyCashier,Is.False,"PRODUCT: Cashier work starts only after actual Counter head arrival");
                for(var frame=0;frame<6000 && !(reservePublished && working) && errors.Count==0;frame++)yield return null;
                Assert.That(errors,Is.Empty);Assert.That(headAttempt && reservePublished && working,Is.True,"PRODUCT: actual slot0 arrival enables the unchanged Reserve and real Cashier path");
                Assert.That(reserveFrame,Is.GreaterThanOrEqualTo(arrivalFrame));Assert.That(workingFrame,Is.GreaterThanOrEqualTo(reserveFrame));
                Assert.That(Read(visit,"Admission"),Is.SameAs(admission));Assert.That(Read(visit,"Actor"),Is.SameAs(actor));
                CollectionAssert.AreEquivalent(originalTokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==tested).Select(r=>r.Token));
            }
            finally
            {
                employees.IntegratedTaskEvent-=ObserveCashier;corridorHook.SetValue(customers,priorCorridor);pickupHook.SetValue(customers,priorPickup);customers.AutoSpawn=false;
            }
        }

        [UnityTest] public IEnumerator JointOwnedPickupReservationSurvivesRealCashierCompletionWithoutResubmission()
            => ObserveOwnedPickupPaidCheckpoint(0);
        [UnityTest] public IEnumerator RealPaidCheckpointOwnsOneNewPickupMovementThroughNativeArrival()
            => ObserveOwnedPickupPaidCheckpoint(1);
        [UnityTest] public IEnumerator RealPaidCheckpointWithNewBodyKeepsReservationAndRejectsTransferAndNative()
            => ObserveOwnedPickupPaidCheckpoint(2);
        [UnityTest] public IEnumerator RealPaidPreparationFailureRequiresExplicitResumeAfterBodyRepairAndActuallyExits()
            => ObserveOwnedPickupPaidCheckpoint(2,true);
        [UnityTest] public IEnumerator FailedPaidPreparationReceiptSurvivesSameRevisionNavGapBeforeExplicitResume()
            => ObserveOwnedPickupPaidCheckpoint(2,true,1);
        [UnityTest] public IEnumerator AuthorizedPaidPreparationAttemptSurvivesSameRevisionNavGapBeforeTransfer()
            => ObserveOwnedPickupPaidCheckpoint(2,true,2);

        // Observe one real Reserve -> Cashier Finished -> Create/paid -> Transfer journey.
        // 只在原 Create 成功后放入真实阻挡身体；不补付款、queue Mark、Native callback 或 ready batch。
        private Action paidFailureWitnessCleanup;
        [TearDown] public void CleanupPaidFailureWitness()=>paidFailureWitnessCleanup?.Invoke();

        private IEnumerator ObserveOwnedPickupPaidCheckpoint(int mode,bool resumeAfterRepair=false,int navGapStage=0)
        {
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var service=CafeLoopRuntimeProof.Field<CafeLoopService>(owner,"service");
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var pickupHook=typeof(CustomerFlowController).GetField("PickupObserver",flags);
            var corridorHook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);
            var createHook=typeof(CafeLoopService).GetField("createOrder",flags);
            var priorPickup=pickupHook.GetValue(customers);var priorCorridor=corridorHook.GetValue(customers);
            var priorCreate=(Func<OrderService,string,string,OrderResult>)createHook.GetValue(service);
            var waitHook=typeof(ServiceTrafficCoordinator).GetField("WaitFailureObserver",flags);
            var priorWait=waitHook.GetValue(coordinator);
            object failureEntry=null,failureTask=null,failureVisit=null,failureActor=null,failureAdmission=null;
            TrafficRequest failureRequest=null;TrafficWorldSnapshot failureSnapshot=null;TrafficIntentKey failureIntent=default;
            CafeOrderKey failureOrder=default;var failureObserved=false;var failureFrame=-1;var failureFirst=-1;
            var errors=new List<string>();var violations=new List<string>();var records=new List<string>();var overflow=0;
            string id=null;object visit=null,admission=null,reservationEntry=null,parkingEntry=null;
            NavigationActor actor=null,blocker=null;PickupQueueService pickup=null;PickupQueueService.ReservationClaim originalClaim=null;
            TrafficDecision parkingSource=null,movementSource=null;TrafficRequest parkingRequest=null,movementPrimary=null;
            TrafficIntentKey parkingIntent=default,movementIntent=default;CafeOrderKey paidKey=default;
            CapacityToken[] tokens=null;QueueAssignment originalAssignment=default,transferredAssignment=default;
            var paidCandidates=new HashSet<TrafficIntentKey>();var sourceRefs=new List<TrafficDecision>();
            var reserveFrame=-1;var finishedFrame=-1;var createFrame=-1;var transferFrame=-1;var firstIssueFrame=-1;
            var firstFrame=-1;var originalFirstFrame=-1;var originalQueries=0;var originalWait=0f;var ordinaryFrames=0;
            long originalAcquired=0,originalSequence=0,originalVersion=0,orderNumber=0,lastRequest=0;
            var publications=0;var creates=0;var transfers=0;var accepted=0;var paidPolls=0;var cashProgress=false;
            var sameCashTask=false;var actualPaid=false;var sameTransferredEntry=false;var transferHadPaid=false;
            var bound=false;var nativeLive=false;var callbackArrived=false;var canonicalArrived=false;var parkingActiveAtTransfer=false;
            CafeTaskToken cashToken=default;TrafficNativeStamp lastStamp=default;Vector3 reservePose=default,callbackPose=default,arrivalTarget=default;
            var printedRecords=0;var exitArrived=false;var removed=false;var completedSeen=false;var completedObservations=0;var recoveredLedgerArrived=false;
            var exitRequest=0L;var exitCallbacks=0;var ownerResumeCalls=0;
            var navGapProducts=new List<string>();var navGapReleased=false;var navGapObserving=false;var navGapCallbacks=0;
            var departure=CafeLoopRuntimeProof.Field<Transform>(customers,"exit");
            void Record(string value){if(records.Count<32)records.Add("frame="+Time.frameCount+" "+value);else overflow++;}
            void FlushRecords(){while(printedRecords<records.Count)TestContext.WriteLine("OWNED_PAID_WITNESS "+records[printedRecords++]);}
            void Violation(string value){if(violations.Count<32)violations.Add(value);}
            void Safe(Action action){try{action();}catch(Exception error){if(errors.Count<16)errors.Add(error.ToString());}}
            void GapProduct(Action action){try{action();}catch(AssertionException error){navGapProducts.Add(error.Message);}}
            object Read(object value,string field)=>value==null?null:MovementRead(value,field);
            IDictionary Entries()=>CafeLoopRuntimeProof.Field<IDictionary>(coordinator,"entries");
            long Number(string details,string prefix)
            {
                var part=(details??"").Split(' ').FirstOrDefault(p=>p.StartsWith(prefix,StringComparison.Ordinal));
                return part!=null && long.TryParse(part.Substring(prefix.Length),out var number)?number:0;
            }
            void SampleOwned(string boundary)
            {
                if(parkingSource==null || transferFrame>=0)return;
                var current=(IDictionary)Read(pickup,"reservations");
                if(!current.Contains(id) || !ReferenceEquals(current[id],reservationEntry) || !originalClaim.IsCurrent)
                    Violation(boundary+": original canonical reservation changed before Transfer");
                if(!ReferenceEquals(Read(visit,"PickupTrafficPermit"),parkingSource) ||
                    !ReferenceEquals(Read(parkingEntry,"JourneyPermit"),parkingSource) ||
                    !ReferenceEquals(Read(parkingEntry,"JourneyRequest"),parkingRequest) || !coordinator.HasActiveGrant(parkingIntent))
                    Violation(boundary+": joint-owned parking source was resubmitted/replaced/deactivated");
                if(coordinator.QueriesConsumed(parkingIntent)!=originalQueries || (int)Read(parkingEntry,"FirstFrame")!=originalFirstFrame ||
                    (float)Read(parkingEntry,"WaitRemaining")!=originalWait ||
                    ((TrafficDecision)Read(parkingEntry,"Decision")).RequestSequence!=originalSequence ||
                    ((TrafficDecision)Read(parkingEntry,"Decision")).AcquiredSequence!=originalAcquired)
                    Violation(boundary+": owned parking priority/query/age/budget changed");
                if(pickup.Version!=originalVersion)Violation(boundary+": unpaid canonical reservation was mutated again");
            }
            void DumpOwnedGuards(string boundary)
            {
                var held=Read(visit,"PickupParking");var source=(TrafficDecision)Read(held,"Source");
                var proof=(TrafficRouteProof)Read(held,"Proof");var fact=(TrafficParkingFact)Read(held,"Fact");
                var claim=(PickupQueueService.ReservationClaim)Read(held,"Claim");var token=(CapacityToken)Read(held,"Token");
                var snapshot=(TrafficWorldSnapshot)Read(coordinator,"snapshot");var body=snapshot.Bodies.SingleOrDefault(b=>b.Owner.ActorId==id);
                var reserved=(QueueAssignment?)Read(visit,"ReservedPickup");pickup.TryCaptureReservation(id,out var currentClaim);
                var checks=new List<KeyValuePair<string,bool>>();
                void Check(string name,bool value)=>checks.Add(new KeyValuePair<string,bool>(name,value));
                Check("heldFlags",!(bool)Read(held,"Retired") && (bool)Read(visit,"PickupAssigned") && !(bool)Read(visit,"PickupInTransit") && !(bool)Read(visit,"HasCollected"));
                Check("actor",actor!=null && actor.isActiveAndEnabled && ReferenceEquals(Read(held,"Actor"),actor));
                Check("admission",ReferenceEquals(Read(held,"Admission"),admission));Check("coordinator",ReferenceEquals(Read(held,"Coordinator"),coordinator));
                Check("owner",ReferenceEquals(Read(held,"Owner"),pickup) && ReferenceEquals(Read(visit,"PickupOwner"),pickup));
                Check("revision",(int)Read(held,"Revision")==adapter.Revision);
                Check("source",source!=null && ReferenceEquals(source,Read(visit,"PickupTrafficPermit")));
                Check("sourceContext",source.Intent.Session==(string)Read(customers,"trafficSession") && source.Intent.Epoch==(long)Read(customers,"trafficEpoch") && source.Intent.LayoutRevision==adapter.Revision);
                Check("activeGrant",coordinator.HasActiveGrant(source.Intent));
                Check("retained",coordinator.TryGetCommittedJourney(source.Intent,out var retained) && ReferenceEquals(retained,source));
                Check("claim",claim?.IsCurrent==true && currentClaim?.IsCurrent==true && claim.IsSameReservation(currentClaim));
                Check("assignment",reserved.HasValue && reserved.Value.VisitId==currentClaim.Assignment.VisitId && reserved.Value.Generation==currentClaim.Assignment.Generation &&
                    reserved.Value.SlotIndex==currentClaim.Assignment.SlotIndex && reserved.Value.Position==currentClaim.Assignment.Position);
                Check("reservedOwnerRevision",ReferenceEquals(Read(visit,"ReservedPickupOwner"),pickup) && (int)Read(visit,"ReservedPickupRevision")==adapter.Revision);
                Check("proofMember",proof!=null && source.ProofRoutes.Contains(proof));
                Check("pathRef",ReferenceEquals(proof.ExpandedPath,Read(visit,"ReservedPickupPath")));Check("routeRef",ReferenceEquals(proof.Waypoints,Read(visit,"ReservedPickupRoute")));
                Check("resource",proof.ResourceId=="pickup/"+id && proof.Waypoints.Count>0);
                Check("endpointExact",proof.Waypoints.Count>0 && proof.Waypoints.Last()==currentClaim.Assignment.Position);
                Check("factOwner",fact!=null && ReferenceEquals(fact.CanonicalOwner,pickup) && ReferenceEquals(fact.BusinessOwner,visit) && ReferenceEquals(fact.ReleaseObligation,admission));
                Check("factPosition",fact.Position==currentClaim.Assignment.Position);Check("factRadius",fact.Radius==actor.Settings.AgentRadius);
                var skin=actor.Settings.CollisionSkin;var epsilon=actor.Settings.Epsilon;
                Check("factSafetyInputs",(float)Read(held,"CollisionSkin")==skin && (float)Read(held,"Epsilon")==epsilon);
                var tokenState=customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,token));
                Check("token",tokenState.OwnerId==id && tokenState.Kind==CapacityKind.PickUp && tokenState.State==ReservationState.Reserved);
                Check("navAvailable",world.LayoutAvailable);
                Check("body",body!=null && body.Enabled && body.ActorActive && ReferenceEquals(body.ActorLife,actor) && body.Position==actor.transform.position);
                Check("currentFact",snapshot.Parking.Any(p=>p.ResourceId==fact.ResourceId && ReferenceEquals(p.CanonicalOwner,pickup) &&
                    ReferenceEquals(p.BusinessOwner,visit) && ReferenceEquals(p.ReleaseObligation,admission) && p.OwnerVersion==pickup.Version &&
                    p.Position==fact.Position && p.Radius==fact.Radius && p.SafetyMargin==fact.SafetyMargin));
                var identity=(bool)typeof(CustomerFlowController).GetMethod("PickupParkingIdentityCurrent",flags).Invoke(customers,new[]{visit});
                var current=(bool)typeof(CustomerFlowController).GetMethod("PickupParkingCurrent",flags).Invoke(customers,new object[]{visit,snapshot});
                var actualFact=snapshot.Parking.Single(p=>p.ResourceId==fact.ResourceId && ReferenceEquals(p.CanonicalOwner,pickup));
                var nominal=BitConverter.ToSingle(BitConverter.GetBytes(skin+epsilon),0);
                string FloatBits(float value)=>value.ToString("R")+"/0x"+BitConverter.ToInt32(BitConverter.GetBytes(value),0).ToString("X8");
                Record("OWNED_GUARDS boundary="+boundary+" identity="+identity+" current="+current+" firstFalse="+checks.FirstOrDefault(p=>!p.Value).Key+
                    " checks=["+string.Join(";",checks.Select(p=>p.Key+"="+p.Value))+"] rawEndpoint="+proof.Waypoints.Last().ToString("F9")+
                    " canonicalTarget="+currentClaim.Assignment.Position.ToString("F9")+" endpointDistance="+Vector3.Distance(proof.Waypoints.Last(),currentClaim.Assignment.Position).ToString("R")+
                    " arrivalTolerance="+actor.Settings.ArrivalDistance.ToString("R")+" sourceStart="+parkingRequest.Start.ToString("F9")+
                    " sourceTarget="+parkingRequest.Target.ToString("F9")+" token="+tokenState.State+" canonicalVersion="+pickup.Version+" snapshot="+snapshot.Version+"/"+snapshot.BodyVersion+
                    " capturedSafety="+FloatBits(fact.SafetyMargin)+" currentSafety="+FloatBits(actualFact.SafetyMargin)+
                    " skin="+FloatBits(skin)+" epsilon="+FloatBits(epsilon)+" normalizedNominal="+FloatBits(nominal)+
                    " nominalEqual="+(nominal==fact.SafetyMargin)+" originalInlineEqual="+(fact.SafetyMargin==skin+epsilon));
            }
            void ObservePickup(string operation,string incoming,PickupQueueService queue,QueueAssignment assignment)=>Safe(()=>
            {
                if(operation=="reserve.published" && parkingSource==null)
                {
                    id=incoming;visit=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[id];admission=Read(visit,"Admission");
                    actor=(NavigationActor)Read(visit,"Actor");pickup=queue;originalAssignment=assignment;reservePose=actor.transform.position;
                    parkingSource=(TrafficDecision)Read(visit,"PickupTrafficPermit");parkingIntent=parkingSource.Intent;parkingEntry=Entries()[parkingIntent];
                    parkingRequest=(TrafficRequest)Read(parkingEntry,"JourneyRequest");reserveFrame=Time.frameCount;
                    originalFirstFrame=(int)Read(parkingEntry,"FirstFrame");originalQueries=coordinator.QueriesConsumed(parkingIntent);
                    originalWait=(float)Read(parkingEntry,"WaitRemaining");originalAcquired=parkingSource.AcquiredSequence;originalSequence=parkingSource.RequestSequence;
                    originalVersion=queue.Version;queue.TryCaptureReservation(id,out originalClaim);
                    reservationEntry=((IDictionary)Read(queue,"reservations"))[id];
                    tokens=customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token).ToArray();
                    publications++;Record("RESERVE joint=True paid="+owner.Snapshot.PaidVisitIds.Contains(id)+" source="+parkingIntent+
                        " acquired="+originalAcquired+" queries="+originalQueries+" first="+originalFirstFrame+" generation="+assignment.Generation);
                    DumpOwnedGuards("reserve.published");return;
                }
                if(incoming!=id)return;
                if(operation=="reserve.published" || operation=="reserve.revalidated")
                {publications++;Record("RESERVE_REPEAT operation="+operation);SampleOwned(operation);}
                if(operation!="transfer.published")return;
                transfers++;transferFrame=Time.frameCount;transferredAssignment=assignment;
                sameTransferredEntry=ReferenceEquals(((IDictionary)Read(queue,"reservations"))[id],reservationEntry) &&
                    queue.TryCaptureReservation(id,out var currentClaim) && originalClaim.IsSameReservation(currentClaim) && currentClaim.IsCurrent;
                var snapshot=owner.Snapshot;transferHadPaid=snapshot.PaidVisitIds.Contains(id) &&
                    snapshot.Orders.Any(p=>p.Key.OrderId==orderNumber && p.Value.CustomerId==id);
                parkingActiveAtTransfer=coordinator.HasActiveGrant(parkingIntent);
                Record("TRANSFER sameEntry="+sameTransferredEntry+" actualPaid="+transferHadPaid+" oldParkingActive="+parkingActiveAtTransfer+
                    " generation="+assignment.Generation+" paidFifo="+string.Join(";",queue.Snapshot.Select(a=>a.VisitId)));
            });
            void ObserveCorridor(string operation,string incoming,string other,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string details)=>Safe(()=>
            {
                if(incoming!=id || parkingSource==null)return;
                if(navGapObserving && operation=="segment.callback-result")navGapCallbacks++;
                if(resumeAfterRepair)
                {
                    if(operation=="segment.submitted" && (details??"").Contains("accepted=True") &&
                        (CustomerVisitState)Read(visit,"State")==CustomerVisitState.Exiting)
                    {exitRequest=Number(details,"request=");Record("EXIT_NATIVE "+details);}
                    if(operation=="segment.callback-result" && exitRequest>0 && Number(details,"request=")==exitRequest &&
                        (details??"").Contains("status=Arrived"))
                    {exitCallbacks++;exitArrived=Vector3.Distance(target,departure.position)<=actor.Settings.ArrivalDistance;Record("EXIT_CALLBACK pose="+target+" "+details);}
                    if(operation=="visit.removed")
                    {removed=true;Record("REMOVED actualExit="+exitArrived+" pose="+actor.transform.position);}
                }
                if(operation=="reserve.attempt")SampleOwned(operation);
                if(operation=="segment.submitted" && (details??"").Contains("accepted=True") &&
                    (CustomerVisitState)Read(visit,"State")==CustomerVisitState.MovingToPickup)
                {
                    accepted++;var move=Read(visit,"TrafficMove");var intent=(TrafficIntentKey)Read(move,"Intent");var entry=Entries()[intent];
                    var source=(TrafficDecision)Read(move,"Source");var leg=Read(entry,"Leg");
                    var request=Number(details,"request=");var stamp=(TrafficNativeStamp)Read(leg,"Stamp");
                    var live=CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor");
                    bound|=(bool)Read(leg,"Committed") && (bool)Read(leg,"Bound") && stamp.RequestId==request && request>0;
                    nativeLive|=live.Contains(id) && (long)Read(live[id],"Id")==request;
                    if(movementSource==null)
                    {
                        movementSource=source;movementIntent=intent;movementPrimary=(TrafficRequest)Read(entry,"JourneyRequest");
                        firstFrame=(int)Read(entry,"FirstFrame");firstIssueFrame=Time.frameCount;
                    }
                    if(!sourceRefs.Contains(source))sourceRefs.Add(source);
                    var proof=(TrafficRouteProof)Read(move,"Proof");var index=(int)Read(move,"Index");
                    if(index==proof.Waypoints.Count-1){lastRequest=request;lastStamp=stamp;arrivalTarget=target;}
                    Record("NATIVE source="+intent+" request="+request+" bound="+(bool)Read(leg,"Bound")+" paid="+owner.Snapshot.PaidVisitIds.Contains(id));
                    if(resumeAfterRepair)Record("RECOVERY_SOURCE first="+Read(entry,"FirstFrame")+" sequence="+source.RequestSequence+
                        " acquired="+source.AcquiredSequence+" queries="+coordinator.QueriesConsumed(intent)+" wait="+Read(entry,"WaitRemaining")+
                        " primaryRequestedFrame="+movementPrimary.RequestedFrame+" originalEntry="+sameTransferredEntry+" pose="+actor.transform.position);
                }
                if(operation=="segment.callback-result" && lastRequest>0 && Number(details,"request=")==lastRequest)
                {callbackArrived=(details??"").Contains("status=Arrived");callbackPose=target;}
                if(operation=="arrival.canonical-committed" && lastRequest>0 && Number(details,"request=")==lastRequest &&
                    (details??"").Contains("phase="+CustomerVisitState.MovingToPickup))
                {
                    canonicalArrived=true;Record("ARRIVED callback="+callbackArrived+" state="+Read(visit,"State"));
                    if(resumeAfterRepair)recoveredLedgerArrived=(bool)Read(visit,"PickupArrived") && pickup.Snapshot.Single(a=>a.VisitId==id).Arrived;
                }
            });
            void ObserveCashier(CafeTaskEvent value)=>Safe(()=>
            {
                if(value.Role!=EmployeeRole.Cashier || value.Checkpoint.VisitId!=id)return;
                if(value.Kind==CafeTaskEventKind.Progress && value.Checkpoint.Stage==CafeWorkStage.Working && value.Checkpoint.RemainingWorkSeconds>0)
                {cashProgress=true;cashToken=value.Token;}
                if(value.Kind==CafeTaskEventKind.ServiceFinished && value.Checkpoint.Stage==CafeWorkStage.Finished)
                {
                    finishedFrame=Time.frameCount;sameCashTask=cashProgress && value.Token.Equals(cashToken) && value.HandleId>0 && value.Generation>0;
                    SampleOwned("cashier.actual-finished");Record("CASHIER_FINISHED sameTask="+sameCashTask);DumpOwnedGuards("cashier.actual-finished");
                }
            });
            // Capture only the actual pre-Fail boundary; never promote an earlier Deferred snapshot.
            // 仅原 WaitOrFail 拒绝点保留原对象，退休后不得补签证据。
            void ObservePaidFailure(string original)=>Safe(()=>
            {
                if(failureObserved || id==null || visit==null)return;
                var task=Read(visit,"PaidPickup");
                if(!(Read(task,"Intent") is TrafficIntentKey key) || key.IntentId!="pickup-paid-movement" || key.ActorId!=id)return;
                var fullKey=key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
                if(original==null || !original.Contains(" intent="+fullKey+" previous="))return;
                failureObserved=true;var entries=Entries();var entry=entries.Contains(key)?entries[key]:null;
                var request=Read(entry,"Request") as TrafficRequest;
                if(entry==null || request==null || !request.Intent.Equals(key) ||
                    !(Read(entry,"Key") is TrafficIntentKey entryKey) || !entryKey.Equals(key) ||
                    !Equals(Read(entry,"Retired"),false) || Read(entry,"JourneyRequest")!=null ||
                    request.ResourceId!="pickup/"+id || request.Target!=originalAssignment.Position ||
                    !ReferenceEquals(Read(visit,"Actor"),actor) || !ReferenceEquals(Read(visit,"Admission"),admission) ||
                    !(Read(task,"Order") is CafeOrderKey order) || order.OrderId!=orderNumber)
                {errors.Add("Paid original failure witness identity mismatch: "+original);return;}
                failureEntry=entry;failureRequest=request;failureIntent=key;failureTask=task;failureVisit=visit;
                failureActor=actor;failureAdmission=admission;failureOrder=order;failureFrame=Time.frameCount;
                failureFirst=(int)Read(entry,"FirstFrame");failureSnapshot=CafeLoopRuntimeProof.Field<TrafficWorldSnapshot>(coordinator,"snapshot");
                Record("PAID_ORIGINAL_FAILURE "+original+" first="+failureFirst+" requestFrame="+request.RequestedFrame+
                    " requestSnapshot="+request.Snapshot.Version+"/"+request.Snapshot.BodyVersion+
                    " currentSnapshot="+failureSnapshot.Version+"/"+failureSnapshot.BodyVersion+" order="+order.OrderId+
                    " sameTask=True sameVisit=True sameActor=True sameAdmission=True");
            });
            var waitRestored=false;
            void RestorePaidFailureWitness()
            {
                if(waitRestored)return;waitRestored=true;
                try{waitHook.SetValue(coordinator,priorWait);}
                finally{paidFailureWitnessCleanup=null;FlushRecords();}
            }
            var nullPaidRequestObserved=false;var paidHistoryObserved=false;
            void ObservePaid()
            {
                if(id==null)return;
                var snapshot=owner.Snapshot;
                if(snapshot.PaidVisitIds.Contains(id) && snapshot.Orders.Any(p=>p.Key.OrderId==orderNumber && p.Value.CustomerId==id))
                {
                    actualPaid=true;paidKey=snapshot.Orders.Single(p=>p.Key.OrderId==orderNumber && p.Value.CustomerId==id).Key;paidPolls++;
                    var entries=Entries();
                    foreach(var key in entries.Keys.Cast<TrafficIntentKey>().Where(k=>k.ActorId==id && !k.Equals(parkingIntent)))
                    {
                        var entry=entries[key];var request=(TrafficRequest)Read(entry,"JourneyRequest")??(TrafficRequest)Read(entry,"Request");
                        if(request==null)
                        {
                            var retired=Read(entry,"Retired");var prepared=Read(entry,"Prepared");var active=Read(entry,"Active");var queued=Read(entry,"Queued");
                            var journey=Read(entry,"Journey");var permit=Read(entry,"JourneyPermit");var leg=Read(entry,"Leg");var handoff=Read(entry,"PendingHandoff");
                            var decision=Read(entry,"Decision") as TrafficDecision;
                            var oldCounter=(key.IntentId=="counter-admission" || key.IntentId=="counter-advance") &&
                                Read(entry,"Key") is TrafficIntentKey entryKey && entryKey.Equals(key) && Equals(retired,true) &&
                                Equals(prepared,false) && Equals(active,false) && Equals(queued,false) &&
                                journey is Vector3[] retained && retained.Length==0 && permit==null && leg==null && handoff==null &&
                                decision!=null && decision.Intent.Equals(key) && decision.Kind==TrafficDecisionKind.Cancelled &&
                                decision.FailureDisposition==TrafficFailureDisposition.Terminal;
                            var originalPaidFailure=key.IntentId=="pickup-paid-movement" && failureRequest!=null &&
                                ReferenceEquals(entry,failureEntry) && failureIntent.Equals(key) && failureRequest.Intent.Equals(key) &&
                                ReferenceEquals(visit,failureVisit) && ReferenceEquals(actor,failureActor) && ReferenceEquals(admission,failureAdmission) &&
                                ReferenceEquals(Read(visit,"PaidPickup"),failureTask) && failureOrder.Equals(paidKey) &&
                                Equals(Read(entry,"Key"),key) && Equals(retired,true) && Equals(prepared,false) && Equals(active,false) && Equals(queued,false) &&
                                journey is Vector3[] failedJourney && failedJourney.Length==0 && permit==null && leg==null && handoff==null &&
                                decision!=null && decision.Intent.Equals(key) && decision.Kind==TrafficDecisionKind.PlanningFailed &&
                                decision.FailureDisposition==TrafficFailureDisposition.Terminal;
                            var facts="PAID_NULL_REQUEST key="+key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+
                                "/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context+" retired="+retired+" prepared="+prepared+
                                " active="+active+" queued="+queued+" journey="+(journey is Vector3[] points?points.Length.ToString():"UNKNOWN")+
                                " permit="+(permit!=null)+" leg="+(leg!=null)+" handoff="+(handoff!=null)+
                                " decision="+(decision==null?"UNKNOWN":decision.Kind+"/"+decision.FailureDisposition)+" retiredCounter="+oldCounter;
                            if(!nullPaidRequestObserved){nullPaidRequestObserved=true;Record(facts);}
                            // Paid history requires the same Entry and original pre-Fail payload; unknown still fails.
                            // 已退休 paid 也必须绑定原失败证据，不能泛跳过 null。
                            if(originalPaidFailure)
                            {
                                paidCandidates.Add(key);
                                if(!paidHistoryObserved){paidHistoryObserved=true;Record(facts+" originalFailureFrame="+failureFrame+" sameFailureEntry=True");}
                            }
                            else if(!oldCounter && errors.Count<16)errors.Add("ObservePaid missing request/original failure witness: "+facts);
                            continue;
                        }
                        if(request.ResourceId=="pickup/"+id && request.Target==originalAssignment.Position)paidCandidates.Add(key);
                    }
                }
            }
            try
            {
                customers.AutoSpawn=false;
                paidFailureWitnessCleanup=RestorePaidFailureWitness;
                waitHook.SetValue(coordinator,Delegate.Combine(priorWait as Delegate,(Action<string>)ObservePaidFailure));
                pickupHook.SetValue(customers,Delegate.Combine(priorPickup as Delegate,(Action<string,string,PickupQueueService,QueueAssignment>)ObservePickup));
                corridorHook.SetValue(customers,Delegate.Combine(priorCorridor as Delegate,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)ObserveCorridor));
                employees.IntegratedTaskEvent+=ObserveCashier;
                createHook.SetValue(service,new Func<OrderService,string,string,OrderResult>((orders,incoming,product)=>
                {
                    var result=priorCreate(orders,incoming,product); // Exactly the original real Create call.
                    Safe(()=>
                    {
                        if(incoming!=id)return;creates++;
                        if(!result.Succeeded)return;createFrame=Time.frameCount;orderNumber=result.Order.OrderId;
                        Record("CREATE succeeded=True order="+orderNumber+" paidBeforeReturn="+owner.Snapshot.PaidVisitIds.Contains(id));
                        if(mode==2 && blocker==null)blocker=Blocker(originalAssignment.Position,"paid-pickup-current-body");
                    });
                    return result;
                }));
                var admitted=false;
                for(var frame=0;frame<2400 && errors.Count==0;frame++)
                {
                    if(!admitted)admitted=customers.TrySpawn();
                    yield return null;
                    Safe(()=>{if(reserveFrame>=0 && Time.frameCount>reserveFrame){ordinaryFrames++;SampleOwned("ordinary-frame");}ObservePaid();});
                    if(mode==0 && actualPaid || mode==1 && canonicalArrived || mode==2 && actualPaid && paidPolls>=5)break;
                }
                TestContext.WriteLine("OWNED_PAID_CHECKPOINT mode="+mode+" reserve="+reserveFrame+" finished="+finishedFrame+" create="+createFrame+
                    " transfer="+transferFrame+" issue="+firstIssueFrame+" publications="+publications+" creates="+creates+" transfers="+transfers+
                    " accepted="+accepted+" candidates="+paidCandidates.Count+" overflow="+overflow+" violations=["+string.Join(";",violations)+"]");
                FlushRecords();
                Assert.That(errors,Is.Empty,"Observer exceptions must not alter the original production callback");
                Assert.That(admitted,Is.True);Assert.That(parkingSource,Is.Not.Null,"FIXTURE: actual joint committed unpaid parking source");
                Assert.That(parkingRequest,Is.Not.Null);Assert.That(originalClaim,Is.Not.Null);Assert.That(reservationEntry,Is.Not.Null);
                Assert.That(originalAcquired,Is.GreaterThan(0));Assert.That(originalQueries,Is.GreaterThan(0));Assert.That(tokens,Has.Length.EqualTo(3));
                Assert.That(cashProgress && sameCashTask,Is.True,"FIXTURE: the same real Cashier task worked and actually Finished");
                Assert.That(finishedFrame,Is.GreaterThan(reserveFrame));Assert.That(ordinaryFrames,Is.GreaterThan(1));
                Assert.That(actualPaid,Is.True,"FIXTURE: actual Order result and Service paid.Add both occurred");Assert.That(creates,Is.EqualTo(1));
                Assert.That(paidKey.OrderId,Is.EqualTo(orderNumber));Assert.That(paidKey.SessionId,Is.EqualTo(owner.Snapshot.SessionId));
                Assert.That(owner.Snapshot.Orders.Count(p=>p.Value.CustomerId==id),Is.EqualTo(1));Assert.That(owner.Snapshot.PaidVisitIds.Count(v=>v==id),Is.EqualTo(1));
                Assert.That(Read(visit,"Admission"),Is.SameAs(admission));Assert.That(Read(visit,"Actor"),Is.SameAs(actor));
                CollectionAssert.AreEquivalent(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
                Assert.That(customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).State,Is.EqualTo(ReservationState.Occupied));
                if(mode==0)
                {
                    Assert.That(publications,Is.EqualTo(1),"PRODUCT: an already joint-owned reservation is not a new parking attempt after Cashier Finished");
                    Assert.That(violations,Is.Empty,"PRODUCT: exact reservation/source/priority/query age survive ordinary frames until real Transfer");
                    yield break;
                }
                if(mode==2)
                {
                    Assert.That(blocker,Is.Not.Null);Assert.That(world.RegisteredActors.Contains(blocker),Is.True);
                    Assert.That(blocker.transform.position,Is.EqualTo(originalAssignment.Position));Assert.That(actor.transform.position,Is.EqualTo(reservePose));
                    Assert.That(transfers,Is.Zero,"PRODUCT: new actual body rejects Transfer before paid FIFO publication");
                    Assert.That(pickup.Snapshot.Any(a=>a.VisitId==id),Is.False);Assert.That(accepted,Is.Zero,"PRODUCT: no Native movement through the actual body");
                    Assert.That(Read(visit,"PickupInTransit"),Is.EqualTo(false));Assert.That(originalClaim.IsCurrent,Is.True);
                    Assert.That(((IDictionary)Read(pickup,"reservations"))[id],Is.SameAs(reservationEntry));
                    Assert.That(pickup.TryGetReservation(id,out var held),Is.True);Assert.That(held.Generation,Is.EqualTo(originalAssignment.Generation));
                }
                else
                {
                    Assert.That(transfers,Is.EqualTo(1));Assert.That(transferHadPaid && sameTransferredEntry,Is.True);
                    Assert.That(transferredAssignment.Generation,Is.GreaterThan(originalAssignment.Generation));
                    Assert.That(parkingActiveAtTransfer,Is.True,"PRODUCT: original parking protects the target until replacement strict commit");
                    Assert.That(accepted,Is.GreaterThan(0));Assert.That(bound && nativeLive,Is.True,"FIXTURE: original accepted and bound Native request");
                    Assert.That(lastRequest,Is.GreaterThan(0));Assert.That(lastStamp.RequestId,Is.EqualTo(lastRequest));
                    Assert.That(callbackArrived && canonicalArrived,Is.True,"FIXTURE: matching actual final Arrived followed by canonical Mark");
                    Assert.That(Vector3.Distance(callbackPose,arrivalTarget),Is.LessThanOrEqualTo(actor.Settings.ArrivalDistance));
                    Assert.That(Read(visit,"PickupArrived"),Is.EqualTo(true));Assert.That(pickup.Snapshot.Single(a=>a.VisitId==id).Arrived,Is.True);
                    Assert.That(movementSource,Is.Not.Null);Assert.That(movementSource,Is.Not.SameAs(parkingSource),"PRODUCT: real payment starts a movement source distinct from completed unpaid parking");
                    Assert.That(movementIntent,Is.Not.EqualTo(parkingIntent));Assert.That(sourceRefs,Has.Count.EqualTo(1),"One paid phase retains one source across its Native legs");
                    Assert.That(firstFrame,Is.EqualTo(createFrame));Assert.That(movementPrimary.RequestedFrame,Is.EqualTo(createFrame));
                    Assert.That(firstIssueFrame,Is.GreaterThanOrEqualTo(createFrame));
                }
                Assert.That(paidCandidates,Has.Count.EqualTo(1),"PRODUCT: one actual paid Order owns one new movement phase, including a blocked first candidate");
                var candidate=Entries()[paidCandidates.Single()];Assert.That((int)Read(candidate,"FirstFrame"),Is.EqualTo(createFrame));
                Assert.That(violations,Is.Empty,"PRODUCT: paid movement does not reopen the completed unpaid parking attempt");
                if(!resumeAfterRepair)yield break;

                // 原 mode2 的全部拒绝断言已完成；以下只读实际失败，再撤掉本测试的 body。
                // Finish the original negative witness before testing explicit recovery.
                var paid=Read(visit,"PaidPickup");var failedIntent=paidCandidates.Single();
                var failedSource=(TrafficDecision)Read(candidate,"Decision");var failedPrimary=failureRequest;
                Assert.That(failedPrimary,Is.Not.Null,"FIXTURE: actual pre-Fail request must be captured before retirement clears payload");
                Assert.That(candidate,Is.SameAs(failureEntry));Assert.That(failedIntent,Is.EqualTo(failureIntent));
                Assert.That(failureFirst,Is.EqualTo(createFrame));Assert.That(failureFrame,Is.GreaterThanOrEqualTo(createFrame));
                Assert.That(failureSnapshot,Is.Not.Null);Assert.That(failureOrder,Is.EqualTo(paidKey));
                var failedQueries=coordinator.QueriesConsumed(failedIntent);var failedWait=(float)Read(candidate,"WaitRemaining");
                var failedPhase=(int)Read(visit,"TrafficPhaseSequence");var failedGeneration=(int)Read(visit,"Generation");
                var failedPose=actor.transform.position;
                var arrivalTolerance=actor.Settings.ArrivalDistance;
                var frozenOwners=((IEnumerable<TrafficIntentKey>)Read(candidate,"FrozenOwners"))?.ToArray();
                Record("PREPARATION_FAILED kind="+failedSource.Kind+" reason="+failedSource.Reason+" intent="+failedIntent+
                    " first="+Read(candidate,"FirstFrame")+" sequence="+failedSource.RequestSequence+" acquired="+failedSource.AcquiredSequence+
                    " queries="+failedQueries+" wait="+failedWait+" parking="+Read(candidate,"Parking")+" leg="+(Read(candidate,"Leg")!=null)+
                    " committed="+(Read(candidate,"JourneyPermit")!=null)+" paidFailed="+Read(paid,"Failed")+" body="+failedPose+
                    " blockers=["+string.Join(";",failedSource.Blockers.Select(b=>b.Owner.ActorId+"/"+b.Proof))+"]");
                Assert.That(failedSource,Is.SameAs(coordinator.GetDecision(failedIntent)),"FIXTURE: exact current terminal source");
                Assert.That(failedSource.Kind,Is.EqualTo(TrafficDecisionKind.PlanningFailed),"FIXTURE: real preparation failure, not a NULL topology/Unreachable substitute");
                Assert.That(failedSource.Blockers.Any(b=>b.Owner.ActorId==blocker.ActorId),Is.True,"FIXTURE: actual inserted body is in original terminal decision");
                var failedBody=failedPrimary.Snapshot.Bodies.Single(b=>b.Owner.ActorId==id);
                Assert.That(failedBody.ActorLife,Is.SameAs(actor));Assert.That(failedBody.Position,Is.EqualTo(failedPose));
                Assert.That(failedPrimary.Snapshot.Bodies.Any(b=>ReferenceEquals(b.ActorLife,blocker) && b.Position==originalAssignment.Position),Is.True);
                Assert.That(failedPrimary.ProvenFailure.HasValue,Is.False);Assert.That(Read(candidate,"Parking"),Is.EqualTo(false));
                Assert.That(Read(candidate,"Leg"),Is.Null);Assert.That(Read(candidate,"JourneyPermit"),Is.Null);
                Assert.That(Read(candidate,"JourneyRequest"),Is.Null);Assert.That(Read(candidate,"PendingHandoff"),Is.Null);
                Assert.That(failedSource.AcquiredSequence,Is.Zero);Assert.That(Read(candidate,"Active"),Is.EqualTo(false));
                Assert.That(Read(candidate,"Prepared"),Is.EqualTo(false));Assert.That(Read(candidate,"Queued"),Is.EqualTo(false));
                Assert.That(Read(paid,"Failed"),Is.EqualTo(true));Assert.That(Read(paid,"Transferred"),Is.EqualTo(false));
                Assert.That(Read(paid,"Accepted"),Is.EqualTo(true));Assert.That(Read(paid,"Intent"),Is.EqualTo(failedIntent));
                Assert.That(Read(paid,"Order"),Is.EqualTo(paidKey));Assert.That(Read(visit,"State"),Is.EqualTo(CustomerVisitState.Blocked));
                var counter=customers.CounterAssignments.Single(a=>a.VisitId==id);
                Assert.That(counter.SlotIndex,Is.Zero);Assert.That(counter.Arrived,Is.True);Assert.That(Read(visit,"CounterArrived"),Is.EqualTo(true));
                Assert.That(Vector3.Distance(actor.transform.position,counter.Position),Is.LessThanOrEqualTo(actor.Settings.ArrivalDistance));
                var total=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token;
                void AssertHeld()
                {
                    Assert.That(Read(visit,"PaidPickup"),Is.SameAs(paid));Assert.That(Read(paid,"Order"),Is.EqualTo(paidKey));
                    Assert.That(creates,Is.EqualTo(1));Assert.That(owner.Snapshot.Orders.Keys,Is.EquivalentTo(new[]{paidKey}));
                    Assert.That(owner.Snapshot.PaidVisitIds,Is.EquivalentTo(new[]{id}));
                    Assert.That(Read(visit,"Actor"),Is.SameAs(actor));Assert.That(Read(visit,"Admission"),Is.SameAs(admission));
                    CollectionAssert.AreEquivalent(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
                    Assert.That(originalClaim.IsCurrent,Is.True);Assert.That(((IDictionary)Read(pickup,"reservations"))[id],Is.SameAs(reservationEntry));
                    Assert.That(pickup.TryGetReservation(id,out var held),Is.True);Assert.That(held.Generation,Is.EqualTo(originalAssignment.Generation));
                    Assert.That(held.SlotIndex,Is.EqualTo(originalAssignment.SlotIndex));Assert.That(held.Position,Is.EqualTo(originalAssignment.Position));
                    Assert.That(customers.CounterAssignments.Single(a=>a.VisitId==id).Generation,Is.EqualTo(counter.Generation));
                    Assert.That(customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.CounterQueue).State,Is.EqualTo(ReservationState.Occupied));
                    Assert.That(customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.PickUp).State,Is.EqualTo(ReservationState.Reserved));
                    Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,total)).State,Is.EqualTo(ReservationState.Occupied));
                }
                AssertHeld();SampleOwned("terminal-before-repair");
                var authenticatedReceipt=Read(paid,"RecoveryReceipt");
                IEnumerator ObservePreparationNavGap(bool authorized)
                {
                    var checkpoint=Read(paid,"Recovery");var registry=(IDictionary)Read(coordinator,"recoveryReceipts");
                    var activeIntent=(TrafficIntentKey)Read(paid,"ActiveIntent");var activeEntry=Entries()[activeIntent];
                    var gapQueries=coordinator.QueriesConsumed(activeIntent);var gapWait=Read(activeEntry,"WaitRemaining");
                    var gapFirst=Read(activeEntry,"FirstFrame");var gapPhase=Read(visit,"TrafficPhaseSequence");
                    var gapGeneration=Read(visit,"Generation");
                    var revision=adapter.Revision;var epoch=owner.Snapshot.Epoch;var session=owner.Snapshot.SessionId;
                    Assert.That(checkpoint,Is.Not.Null,"FIXTURE: captured logical preparation checkpoint exists before the Nav gap");
                    Assert.That(authenticatedReceipt,Is.Not.Null,"FIXTURE: original actual failure authenticated a nonce");
                    Assert.That(Read(checkpoint,"BeforeTransfer"),Is.EqualTo(true));
                    Assert.That(transfers,Is.Zero);Assert.That(accepted,Is.Zero);Assert.That(Read(visit,"TrafficMove"),Is.Null);
                    Assert.That(Read(visit,"PendingTraffic"),Is.Null);Assert.That(Read(activeEntry,"Leg"),Is.Null);
                    Assert.That(registry.Contains(authenticatedReceipt),Is.EqualTo(!authorized));
                    Assert.That(Read(paid,"RecoveryUsed"),Is.EqualTo(authorized));Assert.That(Read(paid,"RecoveryPending"),Is.EqualTo(authorized));
                    Assert.That(Read(paid,"Failed"),Is.EqualTo(!authorized));AssertHeld();
                    if(authorized)
                    {
                        Assert.That(ownerResumeCalls,Is.EqualTo(1));Assert.That(activeIntent,Is.Not.EqualTo(failedIntent));
                        Assert.That(((TrafficDecision)Read(activeEntry,"Decision")).Kind,Is.EqualTo(TrafficDecisionKind.DeferredByBudget));
                        Assert.That(Read(paid,"RecoveryReceipt"),Is.Null,"FIXTURE: this same nonce was consumed exactly once before any Transfer");
                    }
                    else {Assert.That(ownerResumeCalls,Is.Zero);Assert.That(Read(paid,"RecoveryReceipt"),Is.SameAs(authenticatedReceipt));}
                    Record("NAV_GAP_BEGIN stage="+navGapStage+" authorized="+authorized+" nonceRegistered="+registry.Contains(authenticatedReceipt)+
                        " first="+gapFirst+" query="+gapQueries+" wait="+gapWait+" intent="+activeIntent+" transfers="+transfers+" native="+accepted);
                    navGapReleased=true;navGapObserving=true;adapter.ReleaseOwnedNavigation();
                    Assert.That(world.LayoutAvailable,Is.False,"FIXTURE: actual owned Nav release");
                    Assert.That(actor.Agent.enabled,Is.False,"FIXTURE: only the Nav agent is disabled");
                    Assert.That(actor.isActiveAndEnabled,Is.True);Assert.That(world.RegisteredActors.Contains(actor),Is.True);
                    var releasedFrame=Time.frameCount;
                    yield return null; // Exactly one ordinary unavailable frame, never a synthetic LayoutChanged callback.
                    Assert.That(Time.frameCount,Is.EqualTo(releasedFrame+1));Assert.That(errors,Is.Empty,"FIXTURE: original observer callback errors");
                    Assert.That(adapter.Revision,Is.EqualTo(revision));Assert.That(owner.Snapshot.Epoch,Is.EqualTo(epoch));
                    Assert.That(owner.Snapshot.SessionId,Is.EqualTo(session));Assert.That(actor.isActiveAndEnabled,Is.True);
                    Assert.That(world.RegisteredActors.Contains(actor),Is.True);Assert.That(actor.transform.position,Is.EqualTo(failedPose));AssertHeld();
                    Assert.That(customers.CounterAssignments.Single(a=>a.VisitId==id).Arrived,Is.True);
                    Assert.That(customers.CounterAssignments.Single(a=>a.VisitId==id).SlotIndex,Is.Zero);
                    var snapshotBody=((TrafficWorldSnapshot)Read(coordinator,"snapshot")).Bodies.Single(b=>b.Owner.ActorId==id);
                    Assert.That(snapshotBody.ActorLife,Is.SameAs(actor));Assert.That(snapshotBody.ActorActive,Is.True);
                    Record("NAV_GAP_ONE_FRAME stage="+navGapStage+" nav="+world.LayoutAvailable+" agent="+actor.Agent.enabled+
                        " snapshotEnabled="+snapshotBody.Enabled+" actorActive="+snapshotBody.ActorActive+" nonceRegistered="+registry.Contains(authenticatedReceipt)+
                        " sameCheckpoint="+ReferenceEquals(Read(paid,"Recovery"),checkpoint)+" sameReceipt="+ReferenceEquals(Read(paid,"RecoveryReceipt"),authenticatedReceipt)+
                        " pending="+Read(paid,"RecoveryPending")+" failed="+Read(paid,"Failed")+" state="+Read(visit,"State")+
                        " intent="+Read(paid,"ActiveIntent")+" retired="+Read(activeEntry,"Retired")+" query="+coordinator.QueriesConsumed(activeIntent));
                    // Capture all PRODUCT failures separately; a missing checkpoint must not turn into a fixture NullReference.
                    // 不解引用可能已被产品丢弃的 checkpoint；先恢复 Nav，再在主流程报告完整产品证据。
                    GapProduct(()=>Assert.That(Read(paid,"Recovery"),Is.SameAs(checkpoint),"logical preparation checkpoint must survive Nav-only gap"));
                    GapProduct(()=>Assert.That(registry.Contains(authenticatedReceipt),Is.EqualTo(!authorized),"unconsumed nonce survives; consumed nonce stays consumed"));
                    GapProduct(()=>Assert.That(Read(paid,"RecoveryReceipt"),authorized?Is.Null:Is.SameAs(authenticatedReceipt),"consumer receipt ownership"));
                    GapProduct(()=>Assert.That(Read(paid,"RecoveryUsed"),Is.EqualTo(authorized),"one Owner authorization budget"));
                    GapProduct(()=>Assert.That(Read(paid,"RecoveryPending"),Is.EqualTo(authorized),"authorized pending attempt survives"));
                    GapProduct(()=>Assert.That(Read(paid,"Failed"),Is.EqualTo(!authorized),"Nav-only gap cannot poison an authorized attempt"));
                    GapProduct(()=>Assert.That(Read(visit,"State"),Is.EqualTo(authorized?CustomerVisitState.MovingToPickup:CustomerVisitState.Blocked),"same business state"));
                    GapProduct(()=>Assert.That(Read(paid,"ActiveIntent"),Is.EqualTo(activeIntent),"same attempt identity"));
                    GapProduct(()=>Assert.That(Read(visit,"TrafficPhaseSequence"),Is.EqualTo(gapPhase),"same business phase"));
                    GapProduct(()=>Assert.That(Read(visit,"Generation"),Is.EqualTo(gapGeneration),"unissued consumer generation cannot be cancelled"));
                    GapProduct(()=>Assert.That(Read(activeEntry,"FirstFrame"),Is.EqualTo(gapFirst),"original age"));
                    GapProduct(()=>Assert.That(Read(activeEntry,"WaitRemaining"),Is.EqualTo(gapWait),"original wait accounting"));
                    GapProduct(()=>Assert.That(coordinator.QueriesConsumed(activeIntent),Is.EqualTo(gapQueries),"no query while unavailable"));
                    GapProduct(()=>Assert.That(Read(activeEntry,"Retired"),Is.EqualTo(!authorized),"authorized unissued attempt cannot retire"));
                    GapProduct(()=>Assert.That(transfers,Is.Zero,"no Transfer while unavailable"));GapProduct(()=>Assert.That(accepted,Is.Zero,"no Native while unavailable"));
                    GapProduct(()=>Assert.That(navGapCallbacks,Is.Zero,"an unissued attempt cannot have a Native cancellation callback"));
                    GapProduct(()=>Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor").Contains(id),Is.False));
                    Assert.That(adapter.RebuildAndValidate().CanResume,Is.True,"FIXTURE: original same-revision rebuild succeeds");time.SetNormal();
                    navGapObserving=false;navGapReleased=false;
                    Assert.That(adapter.Revision,Is.EqualTo(revision));Assert.That(owner.Snapshot.Epoch,Is.EqualTo(epoch));
                    Assert.That(owner.Snapshot.SessionId,Is.EqualTo(session));Assert.That(world.LayoutAvailable,Is.True);
                    Assert.That(actor.Agent.enabled,Is.True);Assert.That(actor.isActiveAndEnabled,Is.True);AssertHeld();
                    Record("NAV_GAP_RESTORED stage="+navGapStage+" sameCheckpoint="+ReferenceEquals(Read(paid,"Recovery"),checkpoint)+
                        " nonceRegistered="+registry.Contains(authenticatedReceipt)+" ownerCalls="+ownerResumeCalls+" productErrors="+navGapProducts.Count);
                }
                var removedBlockerId=blocker.ActorId;world.Unregister(blocker);blocker.gameObject.SetActive(false);UnityEngine.Object.Destroy(blocker.gameObject);blocker=null;
                Record("BODY_REPAIRED id="+removedBlockerId);
                if(navGapStage==1)yield return ObservePreparationNavGap(false);
                for(var frame=0;frame<120;frame++)
                {
                    yield return null;Assert.That(errors,Is.Empty,"FIXTURE: observer errors before explicit Resume");AssertHeld();SampleOwned("repaired-ordinary-frame");
                    Assert.That(world.RegisteredActors.Any(a=>a!=null && a.ActorId==removedBlockerId),Is.False);
                    Assert.That(Read(candidate,"Decision"),Is.SameAs(failedSource));Assert.That(coordinator.QueriesConsumed(failedIntent),Is.EqualTo(failedQueries));
                    Assert.That(Read(candidate,"WaitRemaining"),Is.EqualTo(failedWait));Assert.That(Read(paid,"Intent"),Is.EqualTo(failedIntent));
                    Assert.That(Read(visit,"TrafficPhaseSequence"),Is.EqualTo(failedPhase));Assert.That(Read(visit,"Generation"),Is.EqualTo(failedGeneration));
                    Assert.That(Read(paid,"Failed"),Is.EqualTo(true));Assert.That(Read(paid,"Transferred"),Is.EqualTo(false));
                    Assert.That(Read(visit,"State"),Is.EqualTo(CustomerVisitState.Blocked));Assert.That(actor.transform.position,Is.EqualTo(failedPose));
                    Assert.That(transfers,Is.Zero);Assert.That(accepted,Is.Zero);Assert.That(pickup.Snapshot.Any(a=>a.VisitId==id),Is.False);
                    Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(world.Service,"byActor").Contains(id),Is.False);
                    var currentCandidates=Entries().Keys.Cast<TrafficIntentKey>().Where(k=>k.ActorId==id && k.IntentId==failedIntent.IntentId).ToArray();
                    Assert.That(currentCandidates,Is.EquivalentTo(new[]{failedIntent}),"PRODUCT: ordinary repaired polling cannot create a recovery attempt");
                }
                Assert.That(violations,Is.Empty);Record("REPAIRED_POLL frames=120 noRetry=True queries="+failedQueries);
                tools.ResumeCollection();ownerResumeCalls++;owner.ResumeBlockedFlow();Record("OWNER_RESUME calls="+ownerResumeCalls);
                if(navGapStage==2)yield return ObservePreparationNavGap(true);
                for(var frame=0;frame<12000 && !removed && errors.Count==0;frame++)
                {
                    yield return null;
                    if(owner.Snapshot.Orders.TryGetValue(paidKey,out var order) && order.State==OrderState.Completed && !completedSeen)
                    {completedSeen=true;completedObservations++;}
                }
                Record("RECOVERY_RESULT calls="+ownerResumeCalls+" transfers="+transfers+" accepted="+accepted+" canonicalArrived="+canonicalArrived+
                    " actualExit="+exitArrived+" removed="+removed+" state="+Read(visit,"State"));FlushRecords();
                Assert.That(errors,Is.Empty,"FIXTURE: original callback observer errors; PRODUCT conditions are asserted outside Safe");
                if(navGapStage!=0)
                {
                    Assert.That(ownerResumeCalls,Is.EqualTo(1));
                    Assert.That(navGapProducts,Is.Empty,"PRODUCT: same-revision Nav-only gap must preserve the authenticated logical preparation checkpoint and its one authorized attempt");
                }
                Assert.That(transfers,Is.EqualTo(1),"PRODUCT: repaired real paid preparation failure plus explicit Owner Resume must make its original joint Transfer");
                Assert.That(transferHadPaid && sameTransferredEntry && parkingActiveAtTransfer,Is.True);
                Assert.That(transferredAssignment.Generation,Is.GreaterThan(originalAssignment.Generation));
                Assert.That(accepted,Is.GreaterThan(0));Assert.That(bound && nativeLive,Is.True,"PRODUCT: fresh strict committed and bound Native movement");
                Assert.That(movementSource,Is.Not.SameAs(failedSource));Assert.That(movementSource,Is.Not.SameAs(parkingSource));
                Assert.That(movementIntent,Is.Not.EqualTo(failedIntent));Assert.That(Read(paid,"Intent"),Is.EqualTo(failedIntent));
                Assert.That(Read(paid,"Order"),Is.EqualTo(paidKey));Assert.That(Read(paid,"FirstFrame"),Is.EqualTo(createFrame));
                Assert.That(firstFrame,Is.EqualTo(createFrame));Assert.That(movementPrimary.RequestedFrame,Is.EqualTo(createFrame));
                Assert.That(movementPrimary,Is.Not.SameAs(failedPrimary));Assert.That(movementSource.RequestSequence,Is.EqualTo(failedSource.RequestSequence));
                Assert.That(coordinator.QueriesConsumed(movementIntent),Is.GreaterThan(failedQueries));
                Assert.That(Read(candidate,"WaitRemaining"),Is.EqualTo(failedWait));
                CollectionAssert.AreEquivalent(frozenOwners??Array.Empty<TrafficIntentKey>(),
                    ((IEnumerable<TrafficIntentKey>)Read(candidate,"FrozenOwners"))??Array.Empty<TrafficIntentKey>());
                Assert.That(sourceRefs,Has.Count.EqualTo(1));Assert.That(callbackArrived && canonicalArrived && recoveredLedgerArrived,Is.True);
                Assert.That(Vector3.Distance(callbackPose,arrivalTarget),Is.LessThanOrEqualTo(arrivalTolerance));
                Assert.That(exitArrived && removed,Is.True,"PRODUCT: actual endpoint Exit callback and production removal are required");
                Assert.That(exitCallbacks,Is.EqualTo(1));Assert.That(completedObservations,Is.EqualTo(1));Assert.That(creates,Is.EqualTo(1));
                Assert.That(owner.Snapshot.Orders.Keys,Is.EquivalentTo(new[]{paidKey}));Assert.That(owner.Snapshot.Orders[paidKey].State,Is.EqualTo(OrderState.Completed));
                CollectionAssert.AreEquivalent(tokens,customers.Capacity.GetReservations().Where(r=>r.OwnerId==id).Select(r=>r.Token));
                Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,total)).State,Is.EqualTo(ReservationState.Released));
                Assert.That(CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits").Contains(id),Is.False);
                yield return null;Assert.That(actor==null,Is.True,"PRODUCT: original actor body is destroyed after actual Exit");
            }
            finally
            {
                RestorePaidFailureWitness();
                createHook.SetValue(service,priorCreate);employees.IntegratedTaskEvent-=ObserveCashier;
                pickupHook.SetValue(customers,priorPickup);corridorHook.SetValue(customers,priorCorridor);customers.AutoSpawn=false;
                if(navGapReleased && !world.LayoutAvailable)adapter.RebuildAndValidate();
                if(blocker!=null){world.Unregister(blocker);blocker.gameObject.SetActive(false);UnityEngine.Object.Destroy(blocker.gameObject);}
            }
        }

        [UnityTest] public IEnumerator D2UnissuedMovementCapturesActualInitialProducerBeforeSubmit()=>D2InitialMovementProducer("capture");
        [UnityTest] public IEnumerator D2UnissuedMovementRefreshesAfterActualCashierBirthAndCompletesOnce()=>D2InitialMovementProducer("cashier-birth");
        [UnityTest] public IEnumerator D2UnissuedMovementRejectsActualConfiguredRowReplacement()=>D2InitialMovementProducer("row-replaced");
        [UnityTest] public IEnumerator D2UnissuedMovementRejectsActuallyConsumedInitialMove()=>D2InitialMovementProducer("consumed");

        private IEnumerator D2InitialMovementProducer(string scenario)
        {
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            object Read(object value,string name)=>value==null?null:MovementRead(value,name);
            bool Current(object value)=>value!=null && (bool)value.GetType().GetProperty("IsCurrent",flags).GetValue(value);
            NavigationActor Prefab(string species)=>AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Phase13/Employees/"+species+"/PF_"+species+"_Employee.prefab").GetComponent<NavigationActor>();
            var layout=Find<CafeLayoutRuntime>();var decoration=Find<DecorationModeController>();var cups=Find<PickupCupDisplay>();
            var grid=(Transform)new SerializedObject(decoration).FindProperty("gridRoot").objectReferenceValue;
            var coordinator=(ServiceTrafficCoordinator)Read(owner,"traffic");
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);
            var previous=(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)hook.GetValue(customers);
            var movementHook=typeof(EmployeeFlowController).GetField("MovementObserver",flags);
            var previousMovement=(Action<string,string,string>)movementHook.GetValue(employees);
            var errors=new List<string>();var sampled=false;var natural=false;var bound=false;var valid=false;
            object row=null,port=null,runner=null,leg=null,move=null,context=null,stamp=null,binding=null,entry=null;
            NavigationActor actor=null;TrafficIntentKey key=default;TrafficWorldSnapshot original=null;
            var first=0;var callbacks=0;var submissions=0;string premise="NOT_OBSERVED";
            TrafficRequest primary=null;TrafficDecision permit=null;object arrivalEntry=null;
            var originalArrivalObserved=false;var originalArrivalBound=false;var arrivalFirst=0;var arrivalQueries=0;
            long arrivalRequestSequence=0,arrivalAcquisition=0;
            void MovementEvent(string id,string operation,string detail)
            {
                // Save the original real callback facts before production legitimately releases heavy payloads.
                // 真实原 Arrived 退休前只读保存证据；不重放 callback，不把 payload 写回产品。
                if(!sampled || id!=key.ActorId)return;
                if(operation=="callback")
                {
                    callbacks++;
                    if(!originalArrivalObserved && detail!=null && detail.Contains(" status=Arrived "))
                    {
                        try
                        {
                            var actualEntry=((IDictionary)Read(coordinator,"entries"))[key];var actualLeg=Read(actualEntry,"Leg");
                            var native=(TrafficNativeStamp)Read(actualLeg,"Stamp");
                            var request=long.Parse(detail.Split(' ').Single(part=>part.StartsWith("request=",StringComparison.Ordinal)).Substring(8),CultureInfo.InvariantCulture);
                            primary=(TrafficRequest)Read(actualEntry,"JourneyRequest");permit=(TrafficDecision)Read(actualEntry,"JourneyPermit");arrivalEntry=actualEntry;
                            arrivalFirst=(int)Read(actualEntry,"FirstFrame");arrivalQueries=(int)Read(actualEntry,"Queries");
                            arrivalRequestSequence=(long)Read(actualEntry,"RequestSequence");arrivalAcquisition=(long)Read(actualEntry,"AcquiredSequence");
                            originalArrivalBound=ReferenceEquals(actualEntry,entry) && ReferenceEquals(Read(row,"Port"),port) &&
                                ReferenceEquals(Read(row,"Runner"),runner) && ReferenceEquals(Read(row,"Actor"),actor) && ReferenceEquals(Read(port,"trafficMove"),move) &&
                                ReferenceEquals(Read(move,"Source"),permit) && !(bool)Read(actualEntry,"Retired") &&
                                (bool)Read(actualLeg,"Committed") && (bool)Read(actualLeg,"Bound") && !(bool)Read(actualLeg,"Completed") &&
                                ReferenceEquals(Read(actualLeg,"ActorLife"),actor) && native.RequestId==request &&
                                native.LegGeneration==(int)Read(leg,"NativeLegGeneration");
                            originalArrivalObserved=true;
                        }
                        catch(Exception error){errors.Add("Original Native arrival witness: "+error);}
                    }
                }
                if(operation=="submitted")submissions++;
            }
            void BeforeSubmit(string operation,string id,string related,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                if(sampled || operation!="traffic.movement-preflight")return;
                try
                {
                    var candidate=((IEnumerable)Read(employees,"employees")).Cast<object>().SingleOrDefault(e=>(string)Read(e,"Id")==id);
                    if(candidate==null || (EmployeeRole)Read(candidate,"Role")!=EmployeeRole.Barista)return;
                    sampled=true;row=candidate;port=Read(row,"Port");runner=Read(row,"Runner");leg=Read(runner,"moveLeg");
                    move=Read(port,"trafficMove");actor=(NavigationActor)Read(row,"Actor");binding=Read(row,"Binding");
                    key=(TrafficIntentKey)Read(move,"Intent");first=Time.frameCount;original=(TrafficWorldSnapshot)Read(customers,"trafficSnapshot");
                    var entries=(IDictionary)Read(coordinator,"entries");var station=(EmployeeStationBinding)binding;
                    natural=actor!=null && actor.isActiveAndEnabled && world.RegisteredActors.Any(a=>ReferenceEquals(a,actor)) &&
                        ReferenceEquals(Read(move,"Coordinator"),coordinator) && key.IntentId=="employee-work" && !entries.Contains(key) &&
                        Read(move,"Source")==null && leg!=null && (bool)Read(leg,"AwaitingTraffic") && !(bool)Read(leg,"Completed") &&
                        (long)Read(leg,"RequestId")==0 && (int)Read(leg,"NativeLegGeneration")>0 &&
                        (EmployeeState)Read(runner,"state")==EmployeeState.MovingToWork && !(bool)Read(runner,"hasCheckpoint") &&
                        (long)Read(runner,"handleId")==0 && station.Role==EmployeeRole.Barista && station.WorkTarget.Position==target &&
                        ((NavigationTarget)Read(leg,"Target")).Position==target && ((NavigationTarget)Read(move,"Goal")).Position==target &&
                        Read(row,"AcceptedToken")==null && Read(row,"Checkpoint")==null && owner.Snapshot.Orders.Count==0 && customers.Snapshot.Count==0 &&
                        !world.RegisteredActors.Any(a=>a.ActorId==owner.EmployeeId(EmployeeRole.Cashier));
                    context=Read(move,"PreparationContext");stamp=Read(context,"stamp");
                    bound=context!=null && stamp!=null && ReferenceEquals(Read(context,"owner"),employees) && ReferenceEquals(Read(context,"employee"),row) &&
                        ReferenceEquals(Read(context,"port"),port) && ReferenceEquals(Read(context,"runner"),runner) && ReferenceEquals(Read(context,"actor"),actor) &&
                        ReferenceEquals(Read(context,"binding"),binding) && ReferenceEquals(Read(context,"movement"),move) &&
                        ReferenceEquals(Read(stamp,"owner"),runner) && ReferenceEquals(Read(stamp,"leg"),leg) &&
                        ReferenceEquals(Read(stamp,"environment"),port) && ReferenceEquals(Read(stamp,"binding"),binding) && !(bool)Read(stamp,"hasCheckpoint") &&
                        (int)Read(move,"PreparationFrame")==first && (Vector3)Read(move,"PreparationOrigin")==actor.transform.position;
                    valid=Current(context) && Current(stamp);
                    premise="frame="+first+" key="+result+" actualRole="+Read(row,"Role")+" state="+Read(runner,"state")+
                        " hasCheckpoint="+Read(runner,"hasCheckpoint")+" assignedTask=NOT_APPLICABLE legGeneration="+Read(leg,"NativeLegGeneration")+
                        " legRequest="+Read(leg,"RequestId")+" awaiting="+Read(leg,"AwaitingTraffic")+" firstSubmit="+!entries.Contains(key)+
                        " originalSnapshot="+original.Version+"/"+original.BodyVersion;
                }
                catch(Exception error){errors.Add(error.ToString());}
            }
            void NotProven(string reason)
            {TestContext.WriteLine("D2_MOVEMENT_PRODUCER deeperControl=NOT_PROVEN scenario="+scenario+" premise="+reason);Assert.Ignore("D2 MOVEMENT NOT_PROVEN: "+reason);}
            void AssertEmptyBusiness()
            {
                Assert.That(owner.Snapshot.Orders,Is.Empty);Assert.That(customers.Snapshot,Is.Empty);
                Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
            }
            try
            {
                AssertEmptyBusiness();Assert.That(employees.Snapshot.All(e=>e.State==EmployeeState.Idle),Is.True);
                hook.SetValue(customers,previous+(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)BeforeSubmit);
                movementHook.SetValue(employees,previousMovement+(Action<string,string,string>)MovementEvent);
                // Original public missing-prefab recipe: real cleanup/reconfigure, no pose or task-field mutation.
                employees.Configure(world,adapter,layout,decoration,time,customers,grid,null,Prefab("Westie"),cups);
                yield return null;yield return null;
                for(var frame=0;frame<600 && !sampled;frame++)yield return null;
                TestContext.WriteLine("D2_MOVEMENT_PRODUCER scenario="+scenario+" naturalPremise="+natural+" "+premise+
                    " featureExpectedCapture=True actualContext="+(context!=null)+" actualStamp="+(stamp!=null)+" bound="+bound+" current="+valid+" deeperControl=NOT_PROVEN");
                Assert.That(errors,Is.Empty,"D2 MOVEMENT FIXTURE: read-only original callback errors");
                Assert.That(sampled && natural,Is.True,"D2 MOVEMENT FIXTURE unavailable: actual initial awaiting MoveLeg before first Submit required; NOT feature RED");
                Assert.That(employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).State,Is.EqualTo(EmployeeState.Blocked));
                Assert.That(context!=null && stamp!=null && bound && valid,Is.True,"D2 MOVEMENT FEATURE RED: production must retain its actual initial sealed context before first Submit; deeper controls NOT_PROVEN");
                AssertEmptyBusiness();if(scenario=="capture")yield break;
                if(!Current(context) || !Current(stamp)){NotProven("original unissued producer already changed before lifecycle probe");yield break;}
                if(scenario=="row-replaced")
                {
                    employees.Configure(world,adapter,layout,decoration,time,customers,grid,Prefab("Shiba"),Prefab("Westie"),cups);
                    yield return null;yield return null;
                    var replacement=((IEnumerable)Read(employees,"employees")).Cast<object>().Single(e=>(EmployeeRole)Read(e,"Role")==EmployeeRole.Barista);
                    Assert.That(replacement,Is.Not.SameAs(row),"Real Configure must replace the actual employee row");
                    Assert.That(world.RegisteredActors.Any(a=>ReferenceEquals(a,actor)),Is.False);
                    var queries=coordinator.QueriesConsumed(key);var entries=((IDictionary)Read(coordinator,"entries")).Count;
                    Assert.That(Current(context),Is.False);Assert.That(Current(stamp),Is.False);
                    Assert.That(coordinator.QueriesConsumed(key),Is.EqualTo(queries));Assert.That(((IDictionary)Read(coordinator,"entries")).Count,Is.EqualTo(entries));
                    AssertEmptyBusiness();Assert.That(errors,Is.Empty);
                    TestContext.WriteLine("D2_MOVEMENT_PRODUCER actualConfigureRowReplacement=True oldSourceRejected=True");yield break;
                }
                var serialized=new SerializedObject(employees);serialized.FindProperty("cashierPrefab").objectReferenceValue=Prefab("Shiba");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                for(var frame=0;frame<10;frame++)yield return null;
                Assert.That(world.RegisteredActors.Any(a=>a.ActorId==owner.EmployeeId(EmployeeRole.Cashier)),Is.False,"Repair alone must never retry initial failure");
                Assert.That(owner.CanProgress,Is.False);Assert.That(customers.AutoSpawn,Is.False);
                var table=(IDictionary)Read(coordinator,"entries");Assert.That(table.Contains(key),Is.True);entry=table[key];
                Assert.That((int)Read(entry,"FirstFrame"),Is.EqualTo(first));Assert.That((long)Read(entry,"AcquiredSequence"),Is.Zero);
                owner.ResumeBlockedFlow();
                for(var frame=0;frame<1200 && !employees.Snapshot.All(e=>e.State==EmployeeState.Idle);frame++)
                {
                    Assert.That(((IDictionary)Read(coordinator,"entries"))[key],Is.SameAs(entry));
                    Assert.That((int)Read(entry,"FirstFrame"),Is.EqualTo(first));
                    if(!(bool)Read(leg,"Completed"))
                        Assert.That(((TrafficDecision)Read(entry,"Decision")).FailureDisposition,Is.Not.EqualTo(TrafficFailureDisposition.Terminal),"Original unissued movement must not become Terminal after external cashier birth");
                    yield return null;
                }
                Assert.That(employees.Snapshot.All(e=>e.State==EmployeeState.Idle),Is.True,string.Join(";",employees.Snapshot.Select(e=>e.Role+"/"+e.State+"/"+e.Failure)));
                Assert.That(owner.CanProgress,Is.True);Assert.That(world.RegisteredActors.Count,Is.EqualTo(2));AssertEmptyBusiness();
                Assert.That((bool)Read(leg,"Completed"),Is.True,"The actual original initial MoveLeg must be consumed by real completion");
                Assert.That((long)Read(leg,"RequestId"),Is.GreaterThan(0));Assert.That(Read(runner,"moveLeg"),Is.Null);
                Assert.That(Current(context),Is.False);Assert.That(Current(stamp),Is.False);
                Assert.That(originalArrivalObserved && originalArrivalBound,Is.True,"FIXTURE NOT_PROVEN: save the actual original committed/bound Native arrival before retirement");
                Assert.That(arrivalEntry,Is.SameAs(entry));Assert.That(((IDictionary)Read(coordinator,"entries"))[key],Is.SameAs(entry));
                Assert.That((bool)Read(entry,"Retired"),Is.True,"PRODUCT: complete initial arrival retires the complete permission");
                var tombstone=(TrafficDecision)Read(entry,"Decision");
                Assert.That(tombstone.Intent,Is.EqualTo(key));Assert.That(tombstone.Kind,Is.EqualTo(TrafficDecisionKind.Cancelled));
                Assert.That(tombstone.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.Terminal));Assert.That(tombstone.Reason,Is.EqualTo("intent-retired"));
                Assert.That(Read(entry,"Request"),Is.Null);Assert.That(Read(entry,"JourneyRequest"),Is.Null);Assert.That(Read(entry,"JourneyPermit"),Is.Null);
                Assert.That(Read(entry,"PreparationBody"),Is.Null);Assert.That((IEnumerable)Read(entry,"PreparationClaims"),Is.Empty);
                Assert.That(Read(entry,"Leg"),Is.Null);Assert.That(Read(entry,"PendingHandoff"),Is.Null);
                Assert.That((IEnumerable)Read(entry,"CurrentPath"),Is.Empty);Assert.That((IEnumerable)Read(entry,"Journey"),Is.Empty);Assert.That((IEnumerable)Read(entry,"Jobs"),Is.Empty);
                Assert.That((int)Read(entry,"FirstFrame"),Is.EqualTo(arrivalFirst));Assert.That((int)Read(entry,"Queries"),Is.EqualTo(arrivalQueries));
                Assert.That((long)Read(entry,"RequestSequence"),Is.EqualTo(arrivalRequestSequence));Assert.That((long)Read(entry,"AcquiredSequence"),Is.EqualTo(arrivalAcquisition));
                Assert.That(world.RegisteredActors.Single(a=>a.ActorId==key.ActorId),Is.SameAs(actor));Assert.That(actor.isActiveAndEnabled,Is.True);
                Assert.That(primary,Is.Not.Null);Assert.That(permit,Is.Not.Null);Assert.That(primary.Intent,Is.EqualTo(key));
                Assert.That(primary.RequestedFrame,Is.EqualTo(first));Assert.That((int)Read(entry,"FirstFrame"),Is.EqualTo(first));
                var acquisition=permit.AcquiredSequence;Assert.That(acquisition,Is.GreaterThan(0));
                Assert.That(submissions,Is.EqualTo(1));Assert.That(callbacks,Is.EqualTo(1),"Exactly one actual initial completion callback");
                for(var frame=0;frame<30;frame++)yield return null;
                Assert.That(submissions,Is.EqualTo(1));Assert.That(callbacks,Is.EqualTo(1));Assert.That(permit.AcquiredSequence,Is.EqualTo(acquisition));
                Assert.That(errors,Is.Empty);AssertEmptyBusiness();
                if(scenario=="cashier-birth")
                {
                    var cashier=world.RegisteredActors.Single(a=>a.ActorId==owner.EmployeeId(EmployeeRole.Cashier));
                    if(primary.Snapshot.BodyVersion==original.BodyVersion || !primary.Snapshot.Bodies.Any(b=>ReferenceEquals(b.ActorLife,cashier)))
                    {NotProven("actual cashier was born, but no retained primary pre-commit cashier-body delta witness");yield break;}
                    Assert.That(primary.Snapshot.BodyVersion,Is.GreaterThan(original.BodyVersion));
                    Assert.That(original.Bodies.Any(b=>b.Owner.ActorId==cashier.ActorId),Is.False);
                    TestContext.WriteLine("D2_MOVEMENT_PRODUCER cashierBirthRefresh=True first="+first+" original="+original.Version+"/"+original.BodyVersion+
                        " primary="+primary.Snapshot.Version+"/"+primary.Snapshot.BodyVersion+" acquired="+acquisition+" callbacks="+callbacks);
                }
                else TestContext.WriteLine("D2_MOVEMENT_PRODUCER actualInitialMoveConsumed=True oldStampRejected=True strictBoundObservation="+originalArrivalBound+" completeInitialRetired=True");
            }
            finally {hook.SetValue(customers,previous);movementHook.SetValue(employees,previousMovement);}
        }

        [UnityTest] public IEnumerator CounterAdvanceCapturesActualOriginalQueueBeforeSubmit()=>CounterAdvanceProducer("capture");
        [UnityTest] public IEnumerator CounterAdvanceRefreshesSameSourceAndCommitsOnce()=>CounterAdvanceProducer("refresh-once");
        [UnityTest] public IEnumerator CounterAdvanceRejectsRealAssignmentLineageRetirement()=>CounterAdvanceProducer("lineage-retired");
        [UnityTest] public IEnumerator CounterAdvanceRejectsRealDecorationOwnerRetirement()=>CounterAdvanceProducer("decor-retired");

        private IEnumerator CounterAdvanceProducer(string scenario)
        {
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            object Read(object value,string name)=>value==null?null:MovementRead(value,name);
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);
            var previous=(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)hook.GetValue(customers);
            var currentMethod=typeof(CustomerFlowController).GetMethod("CounterAdvanceAnchorCurrent",flags);
            Assert.That(currentMethod,Is.Not.Null,"Counter M1 declaration must exist");
            bool Current(object anchor)=>anchor!=null && (bool)currentMethod.Invoke(customers,new[]{anchor});
            var errors=new List<string>();var pollErrors=new List<string>();var sampled=false;var natural=false;var bound=false;var valid=false;
            var sourceText="NOT_OBSERVED";string sourceKeyText=null;object visit=null,anchor=null,entryIdentity=null;
            CounterQueueService counter=null;CounterQueueService.AssignmentClaim actualClaim=null;
            CounterQueueService.PreparedMutation originalPreview=null;QueueAssignment actualAssignment=default;
            TrafficIntentKey key=default;TrafficWorldSnapshot submitted=null;NavigationActor actor=null;CapacityToken total=null;
            int sourceFrame=0,polls=0;Vector3 target=default;long lastQueries=0;
            void BeforeSubmit(string operation,string id,string related,Vector3 point,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                if(operation!="traffic.counter-advance-preflight")return;
                if(sampled)
                {
                    if(result!=sourceKeyText)return;
                    polls++;
                    try
                    {
                        var entries=(IDictionary)Read(coordinator,"entries");
                        if(!ReferenceEquals(Read(visit,"PendingCounterAdvance"),anchor))pollErrors.Add("Original record replaced during same-source polling");
                        if(entries.Contains(key))
                        {
                            var current=entries[key];if(entryIdentity==null)entryIdentity=current;
                            if(!ReferenceEquals(entryIdentity,current) || (int)Read(current,"FirstFrame")!=sourceFrame)pollErrors.Add("Original Entry/age replaced");
                            var queries=(int)Read(current,"Queries");if(queries<lastQueries)pollErrors.Add("Queries reset");lastQueries=queries;
                        }
                    }
                    catch(Exception error){errors.Add(error.ToString());}
                    return;
                }
                sampled=true;polls=1;sourceKeyText=result;
                try
                {
                    var visits=(IDictionary)Read(customers,"visits");visit=visits[id];actor=(NavigationActor)Read(visit,"Actor");
                    counter=(CounterQueueService)Read(customers,"queue");var rows=counter.Snapshot.Where(a=>a.VisitId==id).ToArray();
                    var keys=(IDictionary)Read(customers,"trafficKeys");var entries=(IDictionary)Read(coordinator,"entries");
                    key=keys.Values.Cast<TrafficIntentKey>().Single(k=>k.ActorId==id && k.IntentId=="counter-advance" && !entries.Contains(k));
                    var claimed=counter.TryCaptureAssignment(id,out actualClaim);actualAssignment=rows.Single();target=point;sourceFrame=Time.frameCount;
                    submitted=(TrafficWorldSnapshot)Read(customers,"trafficSnapshot");
                    total=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token;
                    natural=claimed && actualClaim.IsCurrent && actor!=null && actor.isActiveAndEnabled && world.RegisteredActors.Any(a=>ReferenceEquals(a,actor)) &&
                        actualAssignment.Arrived && actualAssignment.SlotIndex>0 && actualAssignment.Position==actor.transform.position && target!=actualAssignment.Position &&
                        (CustomerVisitState)Read(visit,"State")==CustomerVisitState.Queued && Read(visit,"PendingCounter")==null && !entries.Contains(key);
                    anchor=Read(visit,"PendingCounterAdvance");originalPreview=(CounterQueueService.PreparedMutation)Read(anchor,"Preview");
                    var storedClaim=(CounterQueueService.AssignmentClaim)Read(anchor,"Claim");
                    bound=anchor!=null && ReferenceEquals(Read(anchor,"Visit"),visit) && ReferenceEquals(Read(anchor,"Actor"),actor) &&
                        ReferenceEquals(Read(anchor,"Counter"),counter) && ReferenceEquals(Read(anchor,"Total"),total) &&
                        ReferenceEquals(Read(anchor,"Coordinator"),coordinator) && ReferenceEquals(Read(anchor,"Admission"),Read(visit,"Admission")) &&
                        (int)Read(anchor,"Generation")== (int)Read(visit,"Generation") && (Vector3)Read(anchor,"Start")==actor.transform.position &&
                        ((QueueAssignment)Read(anchor,"Assignment")).Equals(actualAssignment) &&
                        ((TrafficIntentKey)Read(anchor,"Key")).Equals(key) && (int)Read(anchor,"FirstFrame")==sourceFrame &&
                        storedClaim!=null && storedClaim.IsCurrent && storedClaim.IsSameReservation(actualClaim) &&
                        originalPreview!=null && originalPreview.IsCurrent && ReferenceEquals(Read(originalPreview,"owner"),counter) &&
                        (long)Read(originalPreview,"ExpectedVersion")==counter.Version && Read(originalPreview,"Kind").ToString()=="Advance" &&
                        (string)Read(originalPreview,"Id")==id && originalPreview.Assignment.Position==target &&
                        originalPreview.Assignment.Generation==actualAssignment.Generation+1 && originalPreview.Assignment.VisitId==id;
                    valid=Current(anchor);
                    sourceText="frame="+sourceFrame+" actor="+id+" phase="+key.IntentId+"/g"+key.Generation+" queueVersion="+counter.Version+
                        " actualRow="+actualAssignment.Generation+"/slot"+actualAssignment.SlotIndex+"/arrived="+actualAssignment.Arrived+
                        " copiedArrived="+((QueueAssignment)Read(visit,"Assignment")).Arrived+" actualClaim="+claimed+" firstSubmit="+!entries.Contains(key)+
                        " preview="+(originalPreview==null?"UNKNOWN_UNTIL_CAPTURE":"ACTUAL_RECORD")+" snapshot="+submitted.Version+"/"+submitted.BodyVersion;
                }
                catch(Exception error){errors.Add(error.ToString());}
            }
            void NotProven(string reason)
            {TestContext.WriteLine("COUNTER_PRODUCER deeperControl=NOT_PROVEN scenario="+scenario+" reason="+reason);Assert.Ignore("COUNTER NOT_PROVEN: "+reason);}
            try
            {
                hook.SetValue(customers,previous+(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)BeforeSubmit);
                tools.ResumeCollection();customers.AutoSpawn=true;
                for(var frame=0;frame<6000 && !sampled;frame++)yield return null;
                TestContext.WriteLine("COUNTER_PRODUCER scenario="+scenario+" naturalPremise="+natural+" "+sourceText+
                    " featureExpectedRecord=True actualRecord="+(anchor!=null)+" bound="+bound+" current="+valid+" deeperControl=NOT_PROVEN");
                Assert.That(errors,Is.Empty,"COUNTER FIXTURE: read-only preflight observer errors");
                Assert.That(sampled && natural,Is.True,"COUNTER FIXTURE unavailable: authentic actual queue Advance before first Submit required; not feature RED");
                Assert.That(anchor!=null && bound && valid,Is.True,"COUNTER FEATURE RED: actual original producer record/claim/preview must be captured before Submit");
                Assert.That(pollErrors,Is.Empty);if(scenario=="capture")yield break;
                if(scenario=="refresh-once")
                {
                    object entry=null;TrafficRequest primary=null;TrafficDecision permit=null;QueueAssignment? advanced=null;
                    for(var frame=0;frame<1200;frame++)
                    {
                        var entries=(IDictionary)Read(coordinator,"entries");
                        if(entries.Contains(key))
                        {
                            entry=entries[key];primary=(TrafficRequest)Read(entry,"JourneyRequest");permit=(TrafficDecision)Read(entry,"JourneyPermit");
                            Assert.That((int)Read(entry,"FirstFrame"),Is.EqualTo(sourceFrame));
                            Assert.That(((TrafficDecision)Read(entry,"Decision")).FailureDisposition,Is.Not.EqualTo(TrafficFailureDisposition.Terminal));
                        }
                        var rows=counter.Snapshot.Where(a=>a.VisitId==key.ActorId).ToArray();
                        if(rows.Length==1 && rows[0].Generation>actualAssignment.Generation){advanced=rows[0];break;}
                        yield return null;
                    }
                    Assert.That(advanced.HasValue && primary!=null && permit!=null,Is.True,"PRODUCT: original counter proposal must make its real canonical Advance");
                    Assert.That(primary.Intent,Is.EqualTo(key));Assert.That(primary.RequestedFrame,Is.EqualTo(sourceFrame));
                    Assert.That(advanced.Value.Generation,Is.EqualTo(actualAssignment.Generation+1));Assert.That(advanced.Value.Position,Is.EqualTo(target));
                    Assert.That(permit.AcquiredSequence,Is.GreaterThan(0));Assert.That((bool)Read(Read(anchor,"Preview"),"Consumed"),Is.True);
                    Assert.That(pollErrors,Is.Empty);Assert.That(errors,Is.Empty);
                    if(polls<2 || primary.Snapshot.Version==submitted.Version){NotProven("no repeated same-source polling with actual pre-commit metadata delta");yield break;}
                    Assert.That(primary.Snapshot.Version,Is.GreaterThan(submitted.Version));Assert.That(entry,Is.SameAs(entryIdentity));
                    var acquisition=permit.AcquiredSequence;
                    for(var frame=0;frame<30;frame++)
                    {
                        var rows=counter.Snapshot.Where(a=>a.VisitId==key.ActorId).ToArray();
                        if(rows.Length==1)Assert.That(rows[0].Generation,Is.EqualTo(actualAssignment.Generation+1),"Same preparation cannot apply Advance twice");
                        Assert.That(permit.AcquiredSequence,Is.EqualTo(acquisition));yield return null;
                    }
                    TestContext.WriteLine("COUNTER_PRODUCER refreshWitness=retained-primary polls="+polls+" old="+submitted.Version+" primary="+primary.Snapshot.Version+
                        " first="+sourceFrame+" acquisition="+acquisition+" actualAdvanceGeneration="+advanced.Value.Generation);yield break;
                }
                if(!actualClaim.IsCurrent || !Current(anchor))
                {NotProven("original preparation was already consumed or retired before the requested lifecycle probe");yield break;}
                if(scenario=="lineage-retired")customers.RemoveVisit(key.ActorId);
                else
                {
                    var decor=Find<DecorationModeController>();var layout=Find<CafeLayoutRuntime>();decor.EnterDecorationMode();yield return null;
                    var machine=layout.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
                    var began=decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,machine.InstanceId);
                    var rotated=began && decor.TryRotateFunctionalSurfacePreview();var confirmed=rotated && decor.TryConfirmFunctionalSurfacePreview();decor.ExitDecorationMode();
                    for(var frame=0;frame<600 && actualClaim.IsCurrent;frame++)yield return null;
                    if(!confirmed || actualClaim.IsCurrent){NotProven("real Decor did not retire the original actual counter assignment owner");yield break;}
                }
                Assert.That(actualClaim.IsCurrent,Is.False,"Actual original queue lineage must be retired before testing stale provenance");
                var currentCounter=(CounterQueueService)Read(customers,"queue");var oldVersion=counter.Version;var currentVersion=currentCounter?.Version;
                var queries=coordinator.QueriesConsumed(key);var entryCount=((IDictionary)Read(coordinator,"entries")).Count;
                Assert.That(Current(anchor),Is.False,"PRODUCT: actual producer loss invalidates the original uncommitted record");
                Assert.That(counter.Version,Is.EqualTo(oldVersion));Assert.That(ReferenceEquals(Read(customers,"queue"),currentCounter),Is.True);
                Assert.That(currentCounter?.Version,Is.EqualTo(currentVersion));Assert.That(coordinator.QueriesConsumed(key),Is.EqualTo(queries));
                Assert.That(((IDictionary)Read(coordinator,"entries")).Count,Is.EqualTo(entryCount));
                Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,total)).State,Is.EqualTo(ReservationState.Released));
                TestContext.WriteLine("COUNTER_PRODUCER deeperControl=OBSERVED_REJECTED scenario="+scenario+" oldOwnerVersion="+oldVersion);
            }
            finally {hook.SetValue(customers,previous);customers.AutoSpawn=false;}
        }

        [UnityTest] public IEnumerator D2PreparedDeliveryCapturesOriginalRealProducerBeforeSubmit()=>D2PreparedDeliveryProducer("capture");
        [UnityTest] public IEnumerator D2PreparedDeliveryRefreshesAtRealPrePumpMetadataCheckpoint()=>D2PreparedDeliveryProducer("metadata");
        [UnityTest] public IEnumerator D2PreparedDeliveryReprovesNaturallyChangedExternalObstacles()=>D2PreparedDeliveryProducer("external-obstacles");
        [UnityTest] public IEnumerator D2PreparedDeliveryRejectsForeignActualEfcOwner()=>D2PreparedDeliveryProducer("foreign-efc");
        [UnityTest] public IEnumerator D2PreparedDeliveryRejectsActualRunnerReplacement()=>D2PreparedDeliveryProducer("runner-replaced");
        [UnityTest] public IEnumerator D2PreparedDeliveryRejectsSameIdActualActorReplacement()=>D2PreparedDeliveryProducer("actor-replaced");
        [UnityTest] public IEnumerator D2PreparedDeliveryRejectsNaturallyChangedOwnPose()=>D2PreparedDeliveryProducer("own-pose");
        [UnityTest] public IEnumerator D2PreparedDeliveryRejectsActualTaskRetryAttempt()=>D2PreparedDeliveryProducer("task-attempt");
        [UnityTest] public IEnumerator D2PreparedDeliveryRejectsNaturalCheckpointChange()=>D2PreparedDeliveryProducer("checkpoint");
        [UnityTest] public IEnumerator D2PreparedDeliveryRejectsActualDecorationBindingChange()=>D2PreparedDeliveryProducer("binding");
        [UnityTest] public IEnumerator D2PreparedDeliveryRejectsActualSelectedNativeLeg()=>D2PreparedDeliveryProducer("selected-leg");
        [UnityTest] public IEnumerator D2PreparedDeliveryRejectsActuallyConsumedDeliveryPreview()=>D2PreparedDeliveryProducer("consumed-preview");

        private IEnumerator D2PreparedDeliveryProducer(string scenario)
        {
            var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);
            var previous=(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)hook.GetValue(customers);
            var capture=typeof(EmployeeFlowController).GetMethod("TryCapturePreparedDeliveryContext",flags);
            var validate=typeof(EmployeeFlowController).GetMethod("IsCurrentPreparedDeliveryContext",flags);
            var runnerCapture=typeof(EmployeeTaskRunner).GetMethod("TryCapturePreparedPickupStamp",flags);
            Assert.That(capture,Is.Not.Null,"D2 FIXTURE: inert EFC capture declaration");Assert.That(validate,Is.Not.Null);
            Assert.That(runnerCapture,Is.Not.Null,"D2 FIXTURE: inert Runner stamp declaration");
            bool sampled=false,premise=false,captured=false,runnerCaptured=false,validAtSource=false,runnerValidAtSource=false;
            string premiseText="NOT_OBSERVED";var errors=new List<string>();object stamp=null,sourceStamp=null,runnerStamp=null,staff=null,runner=null,cursor=null,binding=null;
            NavigationActor actor=null;TrafficIntentKey key=default;TrafficWorldSnapshot submitted=null;Vector3 originalPose=default;int originalFrame=0;
            CafeTaskToken originalToken=default;CafeWorkCheckpoint originalCheckpoint=default;
            object Read(object value,string name)=>value==null?null:MovementRead(value,name);
            bool Current(object context)=>context!=null && (bool)validate.Invoke(employees,new[]{context});
            string Obstacles(TrafficWorldSnapshot snapshot)=>string.Join(";",snapshot.Parking.Where(p=>p.Owner.ActorId!=key.ActorId).Select(p=>p.Position)
                .Concat(snapshot.Bodies.Where(b=>b.Owner.ActorId!=key.ActorId && !b.IndependentlyProgressing).Select(b=>b.Position)).Distinct()
                .Select(p=>p.ToString("F9")).OrderBy(p=>p,StringComparer.Ordinal));
            object CurrentCursor()=>((IDictionary)Read(customers,"trafficCandidates")).Contains(key)?((IDictionary)Read(customers,"trafficCandidates"))[key]:null;
            void BeforeSubmit(string operation,string id,string related,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                if(sampled || operation!="traffic.candidate-preflight" || result==null || !result.Contains("role=Delivery"))return;
                sampled=true;
                try
                {
                    // Natural original preflight only: no task/pose/Entry writes or fabricated route.
                    // 在真实 Prepared 首次提交前捕获；所有断言留在 callback 外。
                    var candidates=(IDictionary)Read(customers,"trafficCandidates");
                    key=candidates.Keys.Cast<TrafficIntentKey>().Single(k=>k.IntentId=="employee-pickup-journey" && Read(candidates[k],"Source")==null);
                    cursor=candidates[key];staff=((IEnumerable)Read(employees,"employees")).Cast<object>().Single(r=>(string)Read(r,"Id")==key.ActorId);
                    runner=Read(staff,"Runner");actor=(NavigationActor)Read(staff,"Actor");binding=Read(staff,"Binding");
                    originalToken=(CafeTaskToken)Read(runner,"taskToken");originalCheckpoint=(CafeWorkCheckpoint)Read(runner,"checkpoint");
                    originalPose=actor.transform.position;originalFrame=Time.frameCount;submitted=(TrafficWorldSnapshot)Read(customers,"trafficSnapshot");
                    var entries=(IDictionary)Read(coordinator,"entries");var accepted=Read(staff,"AcceptedToken");
                    premise=actor.isActiveAndEnabled && world.RegisteredActors.Any(a=>ReferenceEquals(a,actor)) &&
                        (EmployeeState)Read(runner,"state")==EmployeeState.WaitingToDeliver && originalCheckpoint.Stage==CafeWorkStage.Prepared &&
                        (long)Read(runner,"handleId")>0 && accepted!=null && accepted.Equals(originalToken) && Read(runner,"moveLeg")==null &&
                        Read(runner,"selectedDeliveryTarget")==null && !entries.Contains(key) && Read(cursor,"Source")==null;
                    premiseText="frame="+originalFrame+" actor="+key.ActorId+" phase="+key.IntentId+"/g"+key.Generation+" firstSubmit="+!entries.Contains(key)+
                        " state="+Read(runner,"state")+" stage="+originalCheckpoint.Stage+" handle="+Read(runner,"handleId")+
                        " token="+originalToken.SessionId+"/"+originalToken.Epoch+"/"+originalToken.Sequence+"/"+originalToken.Attempt+
                        " acceptedMatches="+(accepted!=null && accepted.Equals(originalToken))+" snapshot="+submitted.Version+"/"+submitted.BodyVersion;
                    if(!premise)return;
                    var runnerArgs=new object[]{null};runnerCaptured=(bool)runnerCapture.Invoke(runner,runnerArgs);runnerStamp=runnerArgs[0];
                    runnerValidAtSource=runnerStamp!=null && (bool)runnerStamp.GetType().GetProperty("IsCurrent",flags).GetValue(runnerStamp);
                    var args=new object[]{key.ActorId,null};captured=(bool)capture.Invoke(employees,args);stamp=args[1];
                    validAtSource=Current(stamp);sourceStamp=Read(cursor,"DeliveryContext");
                }
                catch(Exception error){errors.Add(error.ToString());}
            }
            void SkipPremise(string reason)
            {
                TestContext.WriteLine("D2_PRODUCER deeperControl=NOT_PROVEN scenario="+scenario+" premise="+reason);
                Assert.Ignore("D2 NOT_PROVEN / no authentic deeper fixture: "+reason);
            }
            try
            {
                hook.SetValue(customers,previous+(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)BeforeSubmit);
                tools.ResumeCollection();customers.AutoSpawn=true;
                for(var frame=0;frame<2400 && !sampled;frame++)yield return null;
                TestContext.WriteLine("D2_PRODUCER scenario="+scenario+" naturalPremise="+premise+" "+premiseText+
                    " featureExpectedCapture=True actualCapture="+captured+" runnerCapture="+runnerCaptured+" validAtOriginalSource="+validAtSource+" runnerStampCurrent="+runnerValidAtSource+
                    " deeperControl="+(captured?"PENDING":"NOT_PROVEN"));
                Assert.That(errors,Is.Empty,"D2 FIXTURE: read-only natural callback errors");
                Assert.That(sampled && premise,Is.True,"D2 FIXTURE unavailable: must observe genuine Prepared before first Submit; this is NOT feature RED");
                Assert.That(captured && stamp!=null && runnerCaptured && runnerStamp!=null,Is.True,"D2 FEATURE RED: genuine initial Prepared capture must succeed; deeper control NOT_PROVEN while declarations are inert");
                Assert.That(validAtSource && runnerValidAtSource,Is.True,"D2 FEATURE RED: original valid captures must authenticate at their real source before any negative transition");
                Assert.That(sourceStamp,Is.Not.Null,"D2 FEATURE RED: actual CFC delivery cursor must retain original sealed context before Submit");
                if(scenario=="capture")yield break;
                if(scenario=="foreign-efc")
                {SkipPremise("single initialized EFC scene; no authenticated second owner, no fabricated binding/recipient");yield break;}
                var originalInputs=Obstacles(submitted);
                if(scenario=="metadata" || scenario=="external-obstacles")
                {
                    bool changed=false;TrafficWorldSnapshot now=null;TrafficRequest request=null;TrafficDecision decision=null;object observedEntry=null;
                    for(var frame=0;frame<240;frame++)
                    {
                        now=(TrafficWorldSnapshot)Read(customers,"trafficSnapshot");var entries=(IDictionary)Read(coordinator,"entries");
                        if(entries.Contains(key))
                        {
                            var entry=entries[key];observedEntry=entry;request=(TrafficRequest)Read(entry,"Request");decision=(TrafficDecision)Read(entry,"Decision");
                            changed=scenario=="metadata"?now.Version!=submitted.Version:Obstacles(now)!=originalInputs && (bool)Read(entry,"Queued");
                            if(changed)break;
                            if(decision.FailureDisposition==TrafficFailureDisposition.Terminal || decision.AcquiredSequence>0)break;
                        }
                        yield return null;
                    }
                    if(scenario=="metadata" && observedEntry!=null)
                    {
                        var first=(int)Read(observedEntry,"FirstFrame");var acquired=(long)Read(observedEntry,"AcquiredSequence");
                        var leg=Read(observedEntry,"Leg");var primary=(TrafficRequest)Read(observedEntry,"JourneyRequest");
                        var permit=(TrafficDecision)Read(observedEntry,"JourneyPermit");var queued=(bool)Read(observedEntry,"Queued");
                        TestContext.WriteLine("D2_METADATA_STAGE key="+key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+
                            "/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context+" frame="+Time.frameCount+" preflight="+originalFrame+
                            " first="+first+" age="+(Time.frameCount-first)+" acquired="+acquired+" queued="+queued+" leg="+(leg!=null)+
                            " decision="+decision.Kind+"/"+decision.FailureDisposition+" queries="+coordinator.QueriesConsumed(key)+
                            " currentFrame="+request.RequestedFrame+" currentSnapshot="+request.Snapshot.Version+"/"+request.Snapshot.BodyVersion+
                            " primary="+(primary!=null)+" primaryFrame="+primary?.RequestedFrame+" primarySnapshot="+primary?.Snapshot.Version+"/"+primary?.Snapshot.BodyVersion+
                            " submitted="+submitted.Version+"/"+submitted.BodyVersion+" now="+now.Version+"/"+now.BodyVersion);
                        Assert.That(first,Is.EqualTo(originalFrame),"Original Entry birth must not change through refresh or strict-leg scheduling");
                        Assert.That(request.Intent,Is.EqualTo(key));
                        Assert.That(decision.FailureDisposition,Is.Not.EqualTo(TrafficFailureDisposition.Terminal),"Same authenticated producer must not terminalize on external metadata alone");
                        if(acquired>0)
                        {
                            // Core TryCommit retains the immutable primary request; PrepareLeg changes only the current scheduling request.
                            // 实际 retained primary 仍须保持原出生年龄；不能把 leg frame 当成新预算。
                            if(primary==null || permit==null){SkipPremise("no authentic retained primary after acquisition");yield break;}
                            Assert.That(permit.Intent,Is.EqualTo(key));Assert.That(permit.AcquiredSequence,Is.EqualTo(acquired));
                            Assert.That((bool)Read(observedEntry,"JourneyParking"),Is.True);request=primary;
                        }
                        else if(leg!=null || primary!=null || !queued)
                        {SkipPremise("no queued primary or authentic retained primary witness");yield break;}
                        Assert.That(request.Intent,Is.EqualTo(key));Assert.That(request.RequestedFrame,Is.EqualTo(originalFrame));
                        Assert.That(request.PlanPrimaryRoute,Is.True);Assert.That(request.NativeStamp,Is.EqualTo(default(TrafficNativeStamp)));
                        Assert.That(request.ResourceId,Is.EqualTo("pickup/"+key.ActorId));
                        Assert.That(request.Target,Is.EqualTo(((IReadOnlyList<NavigationTarget>)Read(cursor,"Sides"))[0].Position));
                        Assert.That(request.Snapshot.Version,Is.GreaterThanOrEqualTo(submitted.Version));
                        if(request.Snapshot.Version==submitted.Version)
                        {SkipPremise("no actual primary snapshot delta before commit; current leg delta cannot prove queued refresh");yield break;}
                        if(acquired==0)Assert.That(request.Snapshot.Version,Is.EqualTo(now.Version),"D2 FEATURE: actual pre-pump producer refreshed the original pending request");
                        else Assert.That(request.Snapshot.Version,Is.LessThanOrEqualTo(now.Version),"Retained primary records its pre-commit snapshot, not later leg metadata");
                        TestContext.WriteLine("D2_PRODUCER authenticatedNaturalDelta=metadata witness="+(acquired>0?"retained-primary":"queued-primary")+
                            " old="+submitted.Version+" primary="+request.Snapshot.Version+" now="+now.Version+" first="+first+" decision="+decision.Kind);
                        yield break;
                    }
                    if(!changed){SkipPremise(scenario=="metadata"?"no natural canonical delta before phase completion":"no natural changed obstacle inputs while this original phase remained queued");yield break;}
                    Assert.That(request.Snapshot.Version,Is.EqualTo(now.Version),"D2 FEATURE: actual pre-pump producer refreshed the original pending request");
                    Assert.That(decision.FailureDisposition,Is.Not.EqualTo(TrafficFailureDisposition.Terminal),"Same authenticated producer must not terminalize on external metadata alone");
                    Assert.That(request.Intent,Is.EqualTo(key));Assert.That(request.RequestedFrame,Is.EqualTo(originalFrame));
                    Assert.That(request.Target,Is.EqualTo(((IReadOnlyList<NavigationTarget>)Read(cursor,"Sides"))[0].Position));
                    TestContext.WriteLine("D2_PRODUCER authenticatedNaturalDelta="+scenario+" old="+submitted.Version+" new="+now.Version+" decision="+decision.Kind);
                    yield break;
                }
                PickupQueueService bindingQueue=null;
                if(scenario=="runner-replaced")
                {
                    employees.DiscardIntegratedTasksForLayout();employees.ResumeIntegratedEmployees();
                    for(var frame=0;frame<240 && ReferenceEquals(Read(staff,"Runner"),runner);frame++)yield return null;
                    if(Read(staff,"Runner")==null || ReferenceEquals(Read(staff,"Runner"),runner)){SkipPremise("public layout task retirement did not produce another actual Runner");yield break;}
                }
                else if(scenario=="actor-replaced")
                {
                    UnityEngine.Object.Destroy(actor.gameObject);yield return null;
                    employees.DiscardIntegratedTasksForLayout();employees.ResumeIntegratedEmployees();
                    for(var frame=0;frame<240 && ((NavigationActor)Read(staff,"Actor")==null || ReferenceEquals(Read(staff,"Actor"),actor));frame++)yield return null;
                    var replacement=(NavigationActor)Read(staff,"Actor");
                    if(replacement==null || ReferenceEquals(replacement,actor) || replacement.ActorId!=key.ActorId){SkipPremise("public owner lifecycle did not recreate same-ID actual ActorLife");yield break;}
                }
                else if(scenario=="task-attempt")
                {
                    employees.HoldIntegratedTasks();tools.ResumeBlockedFlow();
                    for(var frame=0;frame<1200;frame++)
                    {
                        var retryRunner=Read(staff,"Runner");var retryToken=retryRunner==null?default:(CafeTaskToken)Read(retryRunner,"taskToken");
                        if(retryToken.Attempt!=originalToken.Attempt && retryToken.Attempt>0)break;
                        yield return null;
                    }
                    var currentRunner=Read(staff,"Runner");var token=currentRunner==null?default:(CafeTaskToken)Read(currentRunner,"taskToken");
                    if(token.Attempt==originalToken.Attempt || token.Attempt==0){SkipPremise("real task hold/resume did not issue a new accepted attempt");yield break;}
                }
                else if(scenario=="binding")
                {
                    bindingQueue=(PickupQueueService)Read(customers,"pickupQueue");Assert.That(bindingQueue,Is.Not.Null,"Real original owner must exist before Decor retirement");
                    var decor=Find<DecorationModeController>();var layout=Find<CafeLayoutRuntime>();decor.EnterDecorationMode();yield return null;
                    var machine=layout.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
                    var began=decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,machine.InstanceId);
                    var rotated=began && decor.TryRotateFunctionalSurfacePreview();var confirmed=rotated && decor.TryConfirmFunctionalSurfacePreview();decor.ExitDecorationMode();
                    for(var frame=0;frame<600 && ReferenceEquals(Read(staff,"Binding"),binding);frame++)yield return null;
                    if(!confirmed || Read(staff,"Binding")==null || ReferenceEquals(Read(staff,"Binding"),binding)){SkipPremise("real Decor rotation did not publish a changed valid station binding");yield break;}
                }
                else
                {
                    bool changed=false;
                    for(var frame=0;frame<2400;frame++)
                    {
                        var currentRunner=Read(staff,"Runner");var checkpoint=currentRunner==null?default:(CafeWorkCheckpoint)Read(currentRunner,"checkpoint");
                        var leg=Read(currentRunner,"moveLeg");var currentCursor=CurrentCursor();var preview=Read(currentCursor,"DeliveryPreview");
                        changed=scenario=="own-pose"?actor!=null && actor.transform.position!=originalPose:
                            scenario=="checkpoint"?checkpoint.Stage!=originalCheckpoint.Stage || !Nullable.Equals(checkpoint.Order,originalCheckpoint.Order):
                            scenario=="selected-leg"?leg!=null && (long)Read(leg,"RequestId")!=0 && Read(currentRunner,"deliveryRoute")!=null:
                            preview!=null && (bool)Read(preview,"Consumed") && ((PickupQueueService)Read(customers,"pickupQueue")).ServiceLeases.Any(p=>p.Key==key.ActorId);
                        if(changed)break;
                        yield return null;
                    }
                    if(!changed){SkipPremise("natural lifecycle did not establish "+scenario+"; no manual pose/Arrived/lease/task edits permitted");yield break;}
                }
                Assert.That(Current(stamp),Is.False,"D2 PRODUCT: authentic original stamp cannot survive "+scenario);
                var method=typeof(CustomerFlowController).GetMethods(flags).Single(m=>m.Name=="TryPrepareDeliveryTraffic" && m.GetParameters()[0].ParameterType==stamp.GetType());
                var args=new object[]{stamp,null,null,default(NavigationTarget),null,null};
                if(scenario=="binding")
                {
                    var oldVersion=bindingQueue.Version;var currentQueue=(PickupQueueService)Read(customers,"pickupQueue");
                    var currentVersion=currentQueue?.Version;var queries=coordinator.QueriesConsumed(key);
                    Assert.That((bool)method.Invoke(customers,args),Is.False,"D2 PRODUCT: typed CFC consumer rejects invalid producer without reissuing authority");
                    Assert.That(bindingQueue.Version,Is.EqualTo(oldVersion));
                    Assert.That(ReferenceEquals(Read(customers,"pickupQueue"),currentQueue),Is.True,"Rejected probe cannot replace the current nullable owner");
                    Assert.That(currentQueue?.Version,Is.EqualTo(currentVersion));Assert.That(coordinator.QueriesConsumed(key),Is.EqualTo(queries));
                    TestContext.WriteLine("D2_BINDING_OWNERS originalVersion="+oldVersion+" currentPresent="+(currentQueue!=null)+" currentVersion="+currentVersion);
                }
                else
                {
                    var queue=(PickupQueueService)Read(customers,"pickupQueue");var version=queue.Version;var queries=coordinator.QueriesConsumed(key);
                    Assert.That((bool)method.Invoke(customers,args),Is.False,"D2 PRODUCT: typed CFC consumer rejects invalid producer without reissuing authority");
                    Assert.That(queue.Version,Is.EqualTo(version));Assert.That(coordinator.QueriesConsumed(key),Is.EqualTo(queries));
                }
                TestContext.WriteLine("D2_PRODUCER deeperControl=OBSERVED_REJECTED scenario="+scenario);
            }
            finally {hook.SetValue(customers,previous);customers.AutoSpawn=false;}
        }

        [UnityTest] public IEnumerator ThreeNaturalCustomersReleasePickupDependenciesAndActuallyExit()
        {
            var seen=new List<string>();var tokens=new Dictionary<string,CapacityToken>();var exits=new HashSet<string>();
            var errors=new List<string>();var observers=new List<CafeActualExitObserver>();var tracking=true;
            var departure=CafeLoopRuntimeProof.Field<Transform>(customers,"exit");
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var transitions=new List<string>();var previous=new Dictionary<TrafficIntentKey,string>();var transitionOverflow=false;
            var prepareHook=typeof(ServiceTrafficCoordinator).GetField("PrepareLegRejectionObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var previousPrepare=prepareHook.GetValue(coordinator);var prepareRecords=new List<string>();var prepareErrors=new List<string>();var prepareOverflow=0;
            var sourceAgeSeen=new HashSet<TrafficIntentKey>();var sourceAgeRecords=new List<string>();var sourceAgeErrors=new List<string>();
            var birthHook=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var previousBirth=(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)birthHook.GetValue(customers);
            var birthRecords=new List<string>();var birthErrors=new List<string>();var birthOverflow=0;
            var snapshotHook=typeof(ServiceTrafficCoordinator).GetField("SnapshotChangedObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var previousSnapshot=snapshotHook.GetValue(coordinator);var snapshotSampled=false;
            var snapshotRecords=new List<string>();var snapshotErrors=new List<string>();var snapshotSource="UNKNOWN";
            var counterSnapshotSampled=false;var counterSnapshotRecords=new List<string>();var counterSnapshotErrors=new List<string>();
            var commitEnabled=Environment.GetCommandLineArgs().Contains("-trafficThreeNaturalHandoffFalse");
            var commitSampled=false;var commitRecords=new List<string>();var commitErrors=new List<string>();
            string Ref(object value)=>value==null?"NULL":System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value).ToString(CultureInfo.InvariantCulture);
            string Key(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
            object Read(object value,string field)=>value==null?null:MovementRead(value,field);
            string Facts(TrafficWorldSnapshot value)
            {
                if(value==null)return "NULL";
                return "ref="+Ref(value)+" version="+value.Version+" bodyVersion="+value.BodyVersion+
                    " bodies=["+string.Join(";",value.Bodies.Select(b=>Key(b.Owner)+" life="+Ref(b.ActorLife)+" pose="+b.Position.ToString("F9")+
                        " radius="+b.Radius.ToString("R")+" safety="+b.SafetyMargin.ToString("R")+" enabled="+b.Enabled+" actorActive="+b.ActorActive+
                        " progressing="+b.IndependentlyProgressing+" seconds="+b.RemainingSeconds.ToString("R")))+
                    "] parking=["+string.Join(";",value.Parking.Select(p=>Key(p.Owner)+" resource="+p.ResourceId+" canonical="+Ref(p.CanonicalOwner)+
                        " ownerVersion="+p.OwnerVersion+" business="+Ref(p.BusinessOwner)+" release="+Ref(p.ReleaseObligation)+" pose="+p.Position.ToString("F9")+
                        " radius="+p.Radius.ToString("R")+" safety="+p.SafetyMargin.ToString("R")))+"]";
            }
            string SnapshotMember(object value,string name)
            {
                if(value==null)return "NULL";
                var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
                var field=value.GetType().GetField(name,flags);var property=value.GetType().GetProperty(name,flags);
                if(field==null && property==null)throw new MissingMemberException(value.GetType().FullName,name);
                var item=field!=null?field.GetValue(value):property.GetValue(value);
                if(item is Vector3 point)return point.ToString("F9");
                if(item is NavigationTarget target)return target.Position.ToString("F9")+"/facing="+target.Facing;
                return item?.ToString()??"NULL";
            }
            string SnapshotMembers(object value,params string[] names)=>string.Join(" ",names.Select(n=>n+"="+SnapshotMember(value,n)));
            string SnapshotTargets(object value)=>value is IEnumerable<NavigationTarget> targets
                ?string.Join(";",targets.Select(t=>t.Position.ToString("F9")+"/facing="+t.Facing)):"NULL";
            string SnapshotToken(object value)=>SnapshotMembers(value,"SessionId","Epoch","Sequence","Attempt");
            string SnapshotCheckpoint(object value)=>SnapshotMembers(value,"Role","VisitId","Order","Stage","RemainingWorkSeconds");
            void CaptureSnapshotChanged(TrafficRequest request,TrafficWorldSnapshot current)
            {
                if(request.Intent.IntentId!="employee-pickup-journey" || snapshotSampled)return;
                snapshotSampled=true; // One original branch event, including an unsuccessful observation / 最多一次。
                try
                {
                    var key=request.Intent;var entries=(IDictionary)Read(coordinator,"entries");var entry=entries[key];
                    var decision=(TrafficDecision)Read(entry,"Decision");var first=(int)Read(entry,"FirstFrame");
                    snapshotRecords.Add("D2_SNAPSHOT_ORIGINAL frame="+Time.frameCount+" key="+Key(key)+" request="+Ref(request)+
                        " decision="+Ref(decision)+" kind="+decision.Kind+" reason="+decision.Reason+" disposition="+decision.FailureDisposition+
                        " oldVersion="+request.Snapshot.Version+" oldBodyVersion="+request.Snapshot.BodyVersion+
                        " newVersion="+current.Version+" newBodyVersion="+current.BodyVersion+" firstFrame="+first+" ageFrames="+(Time.frameCount-first)+
                        " requestedFrame="+request.RequestedFrame+" "+SnapshotMembers(entry,"RequestSequence","AcquiredSequence","Queries","WaitRemaining",
                            "FiniteLimit","CurrentIndex","CandidateCount","Queued","Prepared","Active","Retired","Discarded","EverCanonicalAttempt","ExplicitRecoveryUsed")+
                        " queryCap="+Read(coordinator,"queriesPerIntent")+" attempted=["+string.Join(",",((IEnumerable<int>)Read(entry,"AttemptedIndices")).OrderBy(i=>i))+"]"+
                        " leg="+Ref(Read(entry,"Leg"))+" permit="+Ref(Read(entry,"JourneyPermit"))+" handoff="+Ref(Read(entry,"PendingHandoff"))+
                        " frozenOwners=["+string.Join(";",((IEnumerable<TrafficIntentKey>)Read(entry,"FrozenOwners")??Enumerable.Empty<TrafficIntentKey>()).Select(Key))+"]"+
                        " waiting=["+string.Join(";",((IEnumerable<TrafficBlocker>)Read(entry,"WaitingBlockers")).Select(b=>Key(b.Owner)+"/"+b.ResourceId+"/"+b.Proof))+"]");
                    snapshotRecords.Add("D2_REQUEST start="+request.Start.ToString("F9")+" target="+request.Target.ToString("F9")+" resource="+request.ResourceId+
                        " radius="+request.Radius+" skin="+request.SafetyMargin+" native="+SnapshotMembers(request.NativeStamp,"RequestId","LegGeneration")+
                        " selected=["+string.Join(";",request.SelectedJourney.Select(p=>p.ToString("F9")))+"] requirements=["+
                        string.Join(";",request.Requirements.Select(r=>r.ResourceId+" start="+r.Start.ToString("F9")+" goal="+r.Goal.ToString("F9")+
                            " clearance="+r.Clearance+" selected="+r.SelectedJourney+" avoidParking="+r.AvoidProposedParking+
                            " obstacles=["+string.Join(";",r.Obstacles.Select(p=>p.ToString("F9")))+"]"))+"]");
                    snapshotRecords.Add("D2_SAVED "+Facts(request.Snapshot));snapshotRecords.Add("D2_CURRENT "+Facts(current));
                    var frozenResources=(IDictionary)Read(entry,"FrozenResources");
                    snapshotRecords.Add("D2_DEPENDENCIES frozenResources=["+(frozenResources==null?"NULL":string.Join(";",frozenResources.Keys.Cast<TrafficIntentKey>()
                        .Select(k=>Key(k)+"=["+string.Join(",",((IEnumerable<string>)frozenResources[k]).OrderBy(r=>r,StringComparer.Ordinal))+"]")))+"]"+
                        " readonlyCurrentDeliveryObstacleInputs=["+string.Join(";",current.Parking.Where(p=>p.Owner.ActorId!=key.ActorId).Select(p=>p.Position)
                            .Concat(current.Bodies.Where(b=>b.Owner.ActorId!=key.ActorId && !b.IndependentlyProgressing).Select(b=>b.Position)).Distinct().Select(p=>p.ToString("F9")))+"]");
                    var roster=((IEnumerable)Read(employees,"employees")).Cast<object>();var staff=roster.Single(r=>(string)Read(r,"Id")==key.ActorId);
                    var runner=Read(staff,"Runner");var actor=(NavigationActor)Read(staff,"Actor");var binding=Read(staff,"Binding");
                    var oldBody=request.Snapshot.Bodies.SingleOrDefault(b=>b.Owner.ActorId==key.ActorId);
                    var newBody=current.Bodies.SingleOrDefault(b=>b.Owner.ActorId==key.ActorId);
                    snapshotRecords.Add("D2_EMPLOYEE staff="+Ref(staff)+" runner="+Ref(runner)+" actor="+Ref(actor)+" oldLifeEqualsActor="+ReferenceEquals(oldBody?.ActorLife,actor)+
                        " newLifeEqualsActor="+ReferenceEquals(newBody?.ActorLife,actor)+" pose="+actor.transform.position.ToString("F9")+" active="+actor.isActiveAndEnabled+
                        " binding="+Ref(binding)+" "+SnapshotMembers(binding,"Role","WorkStationId","PickUpId","WorkTarget","PickUpTarget","CupAddress","LayoutRevision")+
                        " needsBinding="+Read(staff,"NeedsBinding")+" acceptedToken=["+SnapshotToken(Read(staff,"AcceptedToken"))+"] staffCheckpoint=["+
                        SnapshotCheckpoint(Read(staff,"Checkpoint"))+"] runnerToken=["+SnapshotToken(Read(runner,"taskToken"))+"] runnerCheckpoint=["+
                        SnapshotCheckpoint(Read(runner,"checkpoint"))+"] "+SnapshotMembers(runner,"state","handleId","generation","layoutRevision","trafficSelectionVersion","disposed","retiring","hasCheckpoint")+
                        " moveLeg="+Ref(Read(runner,"moveLeg"))+" selectedDeliveryTarget="+SnapshotMember(runner,"selectedDeliveryTarget")+
                        " outward=["+SnapshotTargets(Read(runner,"deliveryRoute"))+"] home=["+SnapshotTargets(Read(runner,"returnRoute"))+"]");
                    var cursors=(IDictionary)Read(customers,"trafficCandidates");var cursor=cursors.Contains(key)?cursors[key]:null;
                    snapshotRecords.Add("D2_PRODUCER cursor="+Ref(cursor)+" actorLife="+Ref(Read(cursor,"ActorLife"))+" source="+Ref(Read(cursor,"Source"))+
                        " "+SnapshotMembers(cursor,"Role","Origin","CurrentIndex","Count")+" sides=["+SnapshotTargets(Read(cursor,"Sides"))+"]"+
                        " deliveryWorkTarget="+SnapshotMember(customers,"deliveryWorkTarget")+" deliveryAcquired="+Read(customers,"deliveryAcquired")+
                        " deliveryAttempt="+Read(customers,"deliveryAttempt")+" ORIGINAL_queueVersion_taskStamp_binding_preview=UNKNOWN (not retained by original request)");
                    var visits=(IDictionary)Read(customers,"visits");
                    snapshotRecords.Add("D2_CURRENT_GOAL_PRODUCERS ["+string.Join(";",visits.Keys.Cast<string>().OrderBy(id=>id,StringComparer.Ordinal).Select(id=>{
                        var visit=visits[id];var body=(NavigationActor)Read(visit,"Actor");
                        return id+" visit="+Ref(visit)+" actor="+Ref(body)+" pose="+(body==null?"NULL":body.transform.position.ToString("F9"))+
                            " pickupOwner="+Ref(Read(visit,"PickupOwner"))+" admission="+Ref(Read(visit,"Admission"))+" "+
                            SnapshotMembers(visit,"State","Generation","PickupAssigned","PickupInTransit","HasCollected","DeliveryYieldPhase");
                    }))+"]");
                    var pickup=(PickupQueueService)Read(customers,"pickupQueue");var counter=(CounterQueueService)Read(customers,"queue");
                    var reservations=(IDictionary)Read(pickup,"reservations");
                    snapshotRecords.Add("D2_CANONICAL pickup="+Ref(pickup)+" version="+pickup.Version+" reservations=["+
                        string.Join(";",reservations.Keys.Cast<object>().OrderBy(k=>k.ToString(),StringComparer.Ordinal).Select(k=>k+" entry="+Ref(reservations[k])+" "+
                            SnapshotMembers(reservations[k],"Slot","Generation","Target","Paid","Arrived")))+"] leases=["+
                        string.Join(";",pickup.ServiceLeases.Select(p=>p.Key+"@"+p.Value.ToString("F9")))+"] paidFIFO=["+
                        string.Join(";",pickup.Snapshot.Select(a=>a.VisitId+"/g"+a.Generation+"/slot"+a.SlotIndex+"@"+a.Position.ToString("F9")+"/arrived="+a.Arrived))+
                        "] counter="+Ref(counter)+" version="+counter.Version+" assignments=["+
                        string.Join(";",counter.Snapshot.Select(a=>a.VisitId+"/g"+a.Generation+"/slot"+a.SlotIndex+"@"+a.Position.ToString("F9")+"/arrived="+a.Arrived))+"]");
                    var business=((CafeLoopService)Read(owner,"service")).Snapshot;var cups=(PickupCupDisplay)Read(owner,"cups");
                    snapshotRecords.Add("D2_BUSINESS session="+business.SessionId+" epoch="+business.Epoch+" orders=["+
                        string.Join(";",business.Orders.Select(p=>p.Key+"/"+p.Value.CustomerId+"/"+p.Value.State))+"] paid=["+string.Join(";",business.PaidVisitIds)+
                        "] collectedExit=["+string.Join(";",business.CollectedExitVisitIds)+"] deliveries=["+
                        string.Join(";",business.Delivery.Select(d=>d.Key+"/"+d.VisitId+"/"+d.Address+"/"+d.DeliverySequence))+"] cups=["+
                        string.Join(";",cups.VisibleOrders.Select(p=>p.Key+"/life="+Ref(p.Value)+"/active="+(p.Value!=null && p.Value.activeSelf)))+"]");
                }
                catch(Exception error){snapshotErrors.Add(error.ToString());}
            }
            void CaptureCounterSnapshotChanged(TrafficRequest request,TrafficWorldSnapshot current)
            {
                if(request.Intent.IntentId!="counter-advance" || counterSnapshotSampled)return;
                counterSnapshotSampled=true; // First original branch only; no producer preparation or canonical writes.
                try
                {
                    var key=request.Intent;var entries=(IDictionary)Read(coordinator,"entries");var entry=entries[key];
                    var decision=(TrafficDecision)Read(entry,"Decision");var first=(int)Read(entry,"FirstFrame");
                    string Body(TrafficBodyFact b)=>b==null?"NULL":Key(b.Owner)+" life="+Ref(b.ActorLife)+" pose="+b.Position.ToString("F9")+
                        " radius="+b.Radius+" safety="+b.SafetyMargin+" active="+b.ActorActive+" enabled="+b.Enabled+
                        " progressing="+b.IndependentlyProgressing+" seconds="+b.RemainingSeconds;
                    string Claim(TrafficParkingFact p)=>Key(p.Owner)+" resource="+p.ResourceId+" canonical="+Ref(p.CanonicalOwner)+
                        " business="+Ref(p.BusinessOwner)+" release="+Ref(p.ReleaseObligation)+" ownerVersion="+p.OwnerVersion+
                        " pose="+p.Position.ToString("F9")+" radius="+p.Radius+" safety="+p.SafetyMargin;
                    string Assignment(QueueAssignment a)=>a.VisitId+"/g"+a.Generation+"/slot"+a.SlotIndex+"@"+a.Position.ToString("F9")+"/arrived="+a.Arrived;
                    string ObstacleInputs(string ignored)=>string.Join(";",current.Parking.Where(p=>p.Owner.ActorId!=ignored).Select(p=>p.Position)
                        .Concat(current.Bodies.Where(b=>b.Owner.ActorId!=ignored && !b.IndependentlyProgressing).Select(b=>b.Position)).Distinct().Select(p=>p.ToString("F9")));
                    counterSnapshotRecords.Add("D2_COUNTER_ORIGINAL frame="+Time.frameCount+" key="+Key(key)+" entry="+Ref(entry)+" request="+Ref(request)+
                        " sameEntryRequest="+ReferenceEquals(request,Read(entry,"Request"))+" source="+Ref(decision)+" decision="+decision.Kind+"/"+decision.FailureDisposition+"/"+decision.Reason+
                        " old="+request.Snapshot.Version+"/"+request.Snapshot.BodyVersion+" new="+current.Version+"/"+current.BodyVersion+
                        " first="+first+" age="+(Time.frameCount-first)+" requestedFrame="+request.RequestedFrame+" "+
                        SnapshotMembers(entry,"RequestSequence","AcquiredSequence","Queries","WaitRemaining","FiniteLimit","CurrentIndex","CandidateCount",
                            "Parking","Queued","Prepared","Active","Retired","Discarded","PreparationIdentityCaptured","PreparationFailed","EverCanonicalAttempt","ExplicitRecoveryUsed","LegacyLegCompleted","JobIndex")+
                        " queryCap="+Read(coordinator,"queriesPerIntent")+" waitCap="+Read(coordinator,"waitSeconds")+" attempted=["+string.Join(",",((IEnumerable<int>)Read(entry,"AttemptedIndices")).OrderBy(i=>i))+"]"+
                        " leg="+Ref(Read(entry,"Leg"))+" permit="+Ref(Read(entry,"JourneyPermit"))+" journeyRequest="+Ref(Read(entry,"JourneyRequest"))+
                        " handoff="+Ref(Read(entry,"PendingHandoff"))+" journeyCount="+((Vector3[])Read(entry,"Journey")).Length);
                    counterSnapshotRecords.Add("D2_COUNTER_REQUEST start="+request.Start.ToString("F9")+" target="+request.Target.ToString("F9")+" resource="+request.ResourceId+
                        " radius="+request.Radius+" safety="+request.SafetyMargin+" finite="+request.FiniteSeconds+" primary="+request.PlanPrimaryRoute+
                        " independentlyProgressing="+request.IndependentlyProgressing+" native="+SnapshotMembers(request.NativeStamp,"RequestId","LegGeneration")+
                        " selected=["+string.Join(";",request.SelectedJourney.Select(p=>p.ToString("F9")))+"] requirements=["+
                        string.Join(";",request.Requirements.Select(r=>r.ResourceId+" start="+r.Start.ToString("F9")+" goal="+r.Goal.ToString("F9")+
                            " clearance="+r.Clearance+" selected="+r.SelectedJourney+" avoidParking="+r.AvoidProposedParking+
                            " obstacles=["+string.Join(";",r.Obstacles.Select(p=>p.ToString("F9")))+"]"))+"]");
                    var originalBody=(TrafficBodyFact)Read(entry,"PreparationBody");var originalClaims=(TrafficParkingFact[])Read(entry,"PreparationClaims");
                    counterSnapshotRecords.Add("D2_COUNTER_PREPARATION body=["+Body(originalBody)+"] claims=["+string.Join(";",originalClaims.Select(Claim))+"]");
                    counterSnapshotRecords.Add("D2_COUNTER_SAVED "+Facts(request.Snapshot));counterSnapshotRecords.Add("D2_COUNTER_CURRENT "+Facts(current));
                    var frozen=(IDictionary)Read(entry,"FrozenResources");
                    counterSnapshotRecords.Add("D2_COUNTER_EDGES frozenOwners=["+string.Join(";",((IEnumerable<TrafficIntentKey>)Read(entry,"FrozenOwners")??Enumerable.Empty<TrafficIntentKey>()).Select(Key))+
                        "] frozenResources=["+(frozen==null?"NULL":string.Join(";",frozen.Keys.Cast<TrafficIntentKey>().Select(k=>Key(k)+"=["+
                            string.Join(",",((IEnumerable<string>)frozen[k]).OrderBy(r=>r,StringComparer.Ordinal))+"]")))+"] waiting=["+
                        string.Join(";",((IEnumerable<TrafficBlocker>)Read(entry,"WaitingBlockers")).Select(b=>Key(b.Owner)+"/"+b.ResourceId+"/"+b.Proof))+
                        "] decisionBlockers=["+string.Join(";",decision.Blockers.Select(b=>Key(b.Owner)+"/"+b.ResourceId+"/"+b.Proof))+"] currentPrimaryObstacleInputs=["+ObstacleInputs(key.ActorId)+"]");
                    var visits=(IDictionary)Read(customers,"visits");var visit=visits[key.ActorId];var actor=(NavigationActor)Read(visit,"Actor");
                    var counter=(CounterQueueService)Read(customers,"queue");var body=current.Bodies.SingleOrDefault(b=>b.Owner.ActorId==key.ActorId);
                    counterSnapshotRecords.Add("D2_COUNTER_VISIT visit="+Ref(visit)+" actor="+Ref(actor)+" originalLifeSame="+ReferenceEquals(originalBody?.ActorLife,actor)+
                        " currentLifeSame="+ReferenceEquals(body?.ActorLife,actor)+" currentBody=["+Body(body)+"] admission="+Ref(Read(visit,"Admission"))+
                        " admissionFacts=["+SnapshotMembers(Read(visit,"Admission"),"owner","disposed","inside","queued","queueLeft","pickupCancelled","pickupWaiting","pickupLeft")+"]"+
                        " pose="+(actor==null?"NULL":actor.transform.position.ToString("F9"))+" assignment="+Assignment((QueueAssignment)Read(visit,"Assignment"))+" "+
                        SnapshotMembers(visit,"State","ResumeState","Generation","RequestId","QueueLeft","CounterArrived","CounterPosition","AdvanceStart","TrafficPhaseSequence","TrafficMovePending","HasCollected")+
                        " selection="+Ref(Read(visit,"TrafficSelection"))+" issued="+Ref(Read(visit,"IssuedTraffic"))+" pendingCounter="+Ref(Read(visit,"PendingCounter"))+
                        " pendingTraffic="+Ref(Read(visit,"PendingTraffic"))+" sourceFrames=["+string.Join(";",((IDictionary)Read(visit,"TrafficCandidateFrames")).Keys.Cast<TrafficIntentKey>()
                            .Select(k=>Key(k)+"="+((IDictionary)Read(visit,"TrafficCandidateFrames"))[k]))+"]");
                    counterSnapshotRecords.Add("D2_COUNTER_CANONICAL owner="+Ref(counter)+" version="+counter?.Version+" assignments=["+
                        (counter==null?"NULL":string.Join(";",counter.Snapshot.Select(Assignment)))+"] entries=["+
                        (counter==null?"NULL":string.Join(";",((IEnumerable)Read(counter,"entries")).Cast<object>().Select(e=>"ref="+Ref(e)+" "+
                            SnapshotMembers(e,"Id","Slot","Generation","Position","Arrived","Moving","NeedsReplan"))))+"] exiting="+Ref(Read(counter,"exiting"))+
                        " originalPreview=UNKNOWN (not retained by original producer); originalMutableVisitTaskFacts=UNKNOWN");
                    counterSnapshotRecords.Add("D2_COUNTER_CURRENT_GOALS ["+string.Join(";",visits.Keys.Cast<string>().OrderBy(id=>id,StringComparer.Ordinal).Select(id=>{
                        var v=visits[id];var a=(NavigationActor)Read(v,"Actor");var pickup=Read(v,"PickupOwner");
                        var eligible=id!=key.ActorId && a!=null && (bool)Read(v,"PickupAssigned") && !(bool)Read(v,"HasCollected") && pickup!=null &&
                            (!(bool)Read(v,"PickupInTransit") || (CustomerVisitState)Read(v,"State")==CustomerVisitState.PickupWaiting || (CustomerVisitState)Read(v,"State")==CustomerVisitState.PickupService);
                        var claims=current.Parking.Where(p=>p.Owner.ActorId==id && p.ResourceId=="pickup/"+id && ReferenceEquals(p.CanonicalOwner,pickup)).ToArray();
                        return id+" visit="+Ref(v)+" actor="+Ref(a)+" eligible="+eligible+" role=existing-goal/"+id+" selected=False avoidParking=True"+
                            " start="+(a==null?"UNKNOWN":a.transform.position.ToString("F9"))+
                            " clearance="+(a==null?"UNKNOWN":(a.Settings.AgentRadius+world.RegisteredActors.Where(b=>b!=null).Select(b=>b.Settings.AgentRadius)
                                .DefaultIfEmpty(a.Settings.AgentRadius).Max()+a.Settings.CollisionSkin+a.Settings.Epsilon).ToString("R"))+
                            " currentActualClaimGoals=["+string.Join(";",claims.Select(Claim))+"]"+
                            " obstacles=["+ObstacleInputs(id)+"] "+SnapshotMembers(v,"State","PickupAssigned","PickupInTransit","HasCollected");
                    }))+"]");
                    var business=((CafeLoopService)Read(owner,"service")).Snapshot;
                    counterSnapshotRecords.Add("D2_COUNTER_BUSINESS session="+business.SessionId+" epoch="+business.Epoch+" orders=["+
                        string.Join(";",business.Orders.Select(p=>p.Key+"/"+p.Value.CustomerId+"/"+p.Value.State))+"] totals=["+
                        string.Join(";",customers.Capacity.GetReservations().Where(r=>r.Kind==CapacityKind.TotalCustomers).Select(r=>r.OwnerId+" token="+Ref(r.Token)+" state="+r.State))+
                        "] paid=["+string.Join(";",business.PaidVisitIds)+"] collectedExit=["+string.Join(";",business.CollectedExitVisitIds)+"]");
                }
                catch(Exception error){counterSnapshotErrors.Add(error.ToString());}
            }
            void CaptureBirth(string operation,string id,string related,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                if(operation!="admission.birth-ready")return;
                if(birthRecords.Count>=32 || birthErrors.Count>=16){birthOverflow++;return;}
                try
                {
                    // The original seam passes prepared.Assignment.Position; do not create another preview/query.
                    // 只记录原提交路径的真实 target/primary；异常留到 coroutine 外层断言，不能改变产品回调。
                    var entries=(IDictionary)MovementRead(coordinator,"entries");
                    var key=entries.Keys.Cast<TrafficIntentKey>().Single(k=>k.ActorId==result && k.IntentId=="counter-admission");
                    var entry=entries[key];var request=(TrafficRequest)Read(entry,"Request");var decision=(TrafficDecision)Read(entry,"Decision");
                    var primary=decision.ProofRoutes.SingleOrDefault(p=>p.ResourceId==request.ResourceId);
                    var current=(TrafficWorldSnapshot)Read(coordinator,"snapshot");var counter=(CounterQueueService)Read(customers,"queue");
                    birthRecords.Add("frame="+Time.frameCount+" key="+Key(key)+" source="+Ref(decision)+" decision="+decision.Kind+"/"+decision.FailureDisposition+
                        " previewTargetFromOriginalSeam="+target.ToString("F9")+" primaryResource="+(primary?.ResourceId??"NONE")+
                        " primaryEndpoint="+(primary!=null && primary.Waypoints.Count>0?primary.Waypoints.Last().ToString("F9"):"NONE")+
                        " requestTarget="+request.Target.ToString("F9")+" requestVersion="+request.Snapshot.Version+" requestBodyVersion="+request.Snapshot.BodyVersion+
                        " currentVersion="+current.Version+" currentBodyVersion="+current.BodyVersion+" counter="+Ref(counter)+" counterVersion="+counter.Version+
                        " firstFrame="+Read(entry,"FirstFrame")+" requestedFrame="+request.RequestedFrame+" requestSequence="+decision.RequestSequence+
                        " acquiredSequence="+decision.AcquiredSequence+" queries="+coordinator.QueriesConsumed(key));
                }
                catch(Exception error){if(birthErrors.Count<16)birthErrors.Add(error.ToString());}
            }
            void CapturePrepare(string original)
            {
                if(prepareRecords.Count>=32 || prepareErrors.Count>=16){prepareOverflow++;return;}
                var recordIndex=prepareRecords.Count;prepareRecords.Add(original);
                try
                {
                    // Capture the existing original rejection seam; no Refresh/Prepare/query or callback mutation.
                    // 原短路结果原样保留；下面的只读检查另标readonly，不能冒充首次CanIssue判断。
                    var fields=original.Split(' ');
                    string Value(string prefix)=>fields.First(p=>p.StartsWith(prefix,StringComparison.Ordinal)).Substring(prefix.Length);
                    var actorId=Value("actor=");var phase=Value("intent=");var phaseGeneration=int.Parse(Value("phaseGen="),CultureInfo.InvariantCulture);
                    var entries=(IDictionary)MovementRead(coordinator,"entries");
                    var key=entries.Keys.Cast<TrafficIntentKey>().Single(k=>k.ActorId==actorId && k.IntentId==phase && k.Generation==phaseGeneration);
                    var entry=entries[key];var request=(TrafficRequest)Read(entry,"Request");
                    var journey=(TrafficRequest)Read(entry,"JourneyRequest");var source=Read(entry,"JourneyPermit");
                    var rows=(IDictionary)MovementRead(customers,"visits");var visit=rows.Contains(actorId)?rows[actorId]:null;
                    var move=Read(visit,"TrafficMove");var selected=Read(visit,"TrafficSelection");var pending=Read(visit,"PendingTraffic");
                    var phaseFrames=(IDictionary)Read(visit,"TrafficCandidateFrames");
                    var actor=Read(visit,"Actor");var generation=move==null?0:(int)Read(move,"NativeGeneration");
                    var canIssue=coordinator.CanIssueLeg(key,actor,generation);
                    var provenance=coordinator.TryGetCommittedLegProvenance(key,actor,generation,out var readSource,out var readSnapshot);
                    prepareRecords[recordIndex]=original+"\nPREPARE_CONSUMER_READONLY key="+Key(key)+" visit="+Ref(visit)+" state="+Read(visit,"State")+
                        " generation="+Read(visit,"Generation")+" request="+Read(visit,"RequestId")+" move="+Ref(move)+" moveSource="+Ref(Read(move,"Source"))+
                        " journeySource="+Ref(source)+" sameSource="+ReferenceEquals(Read(move,"Source"),source)+" moveIndex="+Read(move,"Index")+
                        " moveNativeGeneration="+generation+" moveSuspended="+Read(move,"Suspended")+" firstFrame="+Read(entry,"FirstFrame")+
                        " primaryRequestedFrame="+journey?.RequestedFrame+" strictRequestedFrame="+request?.RequestedFrame+
                        " selected="+Ref(selected)+" selectedIntent="+(selected==null?"NULL":Key((TrafficIntentKey)Read(selected,"Intent")))+
                        " selectedSource="+Ref(Read(selected,"Source"))+" selectedFirstFrame="+Read(selected,"FirstFrame")+
                        " cachedPhaseFrame="+(phaseFrames!=null && phaseFrames.Contains(key)?phaseFrames[key]:"NOT_RECORDED")+
                        " phaseFrameCount="+phaseFrames?.Count+" pending="+Ref(pending)+" pendingHandle="+Ref(Read(pending,"Handle"))+
                        " pendingSource="+Ref(Read(pending,"Source"))+" readonlyCanIssue="+canIssue+" readonlyProvenance="+provenance+
                        " readonlySource="+Ref(readSource)+" readonlySnapshot="+Ref(readSnapshot)+" publicationException="+coordinator.LastPublicationException+
                        "\nPREPARE_SAVED_STRICT "+Facts(request?.Snapshot)+"\nPREPARE_CURRENT "+Facts((TrafficWorldSnapshot)Read(coordinator,"snapshot"))+
                        "\nPREPARE_PRIMARY "+Facts(journey?.Snapshot);
                }
                catch(Exception error){if(prepareErrors.Count<16)prepareErrors.Add(error.ToString());}
            }
            void ObserveSourceAge(TrafficIntentKey key,object entry)
            {
                // This witness concerns real post-birth phase sources; prospective admission has no Visit yet.
                if(key.IntentId=="counter-admission" || sourceAgeErrors.Count>=16)return;
                try
                {
                    var rows=(IDictionary)MovementRead(customers,"visits");var visit=rows.Contains(key.ActorId)?rows[key.ActorId]:null;
                    var move=Read(visit,"TrafficMove");var selected=Read(visit,"TrafficSelection");
                    if(move==null || selected==null || !((TrafficIntentKey)Read(move,"Intent")).Equals(key) ||
                        !((TrafficIntentKey)Read(selected,"Intent")).Equals(key))return;
                    var source=Read(entry,"JourneyPermit");var primary=(TrafficRequest)Read(entry,"JourneyRequest");
                    if(source==null || primary==null || !ReferenceEquals(source,Read(move,"Source")) ||
                        !ReferenceEquals(source,Read(selected,"Source")))return;
                    var first=(int)Read(entry,"FirstFrame");var selectedFirst=(int)Read(selected,"FirstFrame");
                    var record="frame="+Time.frameCount+" key="+Key(key)+" source="+Ref(source)+" firstFrame="+first+
                        " primaryRequestedFrame="+primary.RequestedFrame+" selectedFirstFrame="+selectedFirst;
                    if(sourceAgeSeen.Count<32 && sourceAgeSeen.Add(key))sourceAgeRecords.Add(record);
                    if(selectedFirst!=first || primary.RequestedFrame!=first)sourceAgeErrors.Add(record);
                }
                catch(Exception error){if(sourceAgeErrors.Count<16)sourceAgeErrors.Add(error.ToString());}
            }
            void CaptureCommitRefusal(string operation,string actorId,string originalPhase,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string original)
            {
                if(!commitEnabled || commitSampled || operation!="traffic.customer-handoff-refused" ||
                    seen.Count<2 || actorId!=seen[1] || originalPhase!="pickup-service")return;
                commitSampled=true;
                try
                {
                    string Fields(object value,params string[] names)=>value==null?"NULL(no-stored-object)":
                        string.Join(";",names.Select(name=>name+"="+(Read(value,name)??"NULL")));
                    string StoredKey(object value)=>value is TrafficIntentKey storedKey?Key(storedKey):"UNKNOWN(no-stored-key)";
                    var visits=(IDictionary)Read(customers,"visits");var visit=visits.Contains(actorId)?visits[actorId]:null;
                    var move=Read(visit,"TrafficMove");var selection=Read(visit,"TrafficSelection");var pending=Read(visit,"PendingTraffic");
                    var key=(TrafficIntentKey)Read(move,"Intent");
                    var originalGeneration=original.Split(' ').Single(value=>value.StartsWith("originalGeneration=",StringComparison.Ordinal));
                    if(key.ActorId!=actorId || key.IntentId!=originalPhase || key.Generation!=int.Parse(originalGeneration.Substring("originalGeneration=".Length),CultureInfo.InvariantCulture))
                        throw new InvalidOperationException("Stored original decision/current pickup-service key binding mismatch");
                    var sourceCore=Read(move,"Coordinator") as ServiceTrafficCoordinator;
                    var entries=sourceCore==null?null:(IDictionary)Read(sourceCore,"entries");var entry=entries!=null && entries.Contains(key)?entries[key]:null;
                    var handle=Read(pending,"Handle");var handleKey=Read(handle,"<Predecessor>k__BackingField");
                    var predecessor=handleKey??Read(pending,"Intent");var previousCore=Read(pending,"Coordinator") as ServiceTrafficCoordinator;
                    var previousEntries=previousCore==null?null:(IDictionary)Read(previousCore,"entries");
                    var previousEntry=predecessor is TrafficIntentKey previousKey && previousEntries!=null && previousEntries.Contains(previousKey)?previousEntries[previousKey]:null;
                    var previousState=Read(previousEntry,"PendingHandoff");var previousDecision=Read(previousEntry,"Decision") as TrafficDecision;
                    var leg=Read(entry,"Leg");var currentDecision=Read(entry,"Decision") as TrafficDecision;
                    commitRecords.Add("THREE_HANDOFF_FALSE frame="+Time.frameCount+" actor="+actorId+" key="+Key(key)+" original=["+original+"]"+
                        " target="+target.ToString("F9")+" sourceCore="+Ref(sourceCore)+" flowCore="+Ref(Read(customers,"traffic"))+
                        " visit=["+Fields(visit,"State","Generation","RequestId","TrafficMovePending","PendingPickupSegment")+"]"+
                        " currentDecision="+Ref(currentDecision)+"/"+currentDecision?.Kind+"/"+currentDecision?.Reason+
                        " entry=["+Fields(entry,"FirstFrame","RequestSequence","AcquiredSequence","Queries","WaitRemaining","Prepared","Active","Queued","Retired")+"]"+
                        " moveRef="+Ref(move)+" moveSource="+Ref(Read(move,"Source"))+" selectedSource="+Ref(Read(selection,"Source"))+
                        " strictSource="+Ref(Read(entry,"JourneyPermit"))+" primaryRequest="+Ref(Read(entry,"JourneyRequest"))+
                        " legRef="+Ref(leg)+" leg=["+Fields(leg,"Index","Generation","Committed","Bound","Completed","Suspended","ReprepareAllowed")+"]"+
                        " pendingRef="+Ref(pending)+" pendingIntent="+StoredKey(Read(pending,"Intent"))+" pendingSource="+Ref(Read(pending,"Source"))+" handleRef="+Ref(handle)+
                        " handlePredecessor="+StoredKey(handleKey)+" previousCore="+Ref(previousCore)+" previousEntry="+Ref(previousEntry)+
                        " previousDecision="+Ref(previousDecision)+"/"+previousDecision?.Kind+"/"+previousDecision?.Reason+
                        " previousPending="+Ref(previousState)+" previousPendingState=["+Fields(previousState,"Remaining")+"]"+
                        " previousCoreHandle="+Ref(Read(previousState,"Handle"))+" previousCoreSource="+Ref(Read(previousState,"Source")));
                }
                catch(Exception error)
                {
                    commitErrors.Add(error.ToString());prepareErrors.Add("THREE_HANDOFF_FALSE observer: "+error);
                }
            }
            void Watch(CafeLoopSnapshot snapshot)
            {
                foreach(var visit in customers.Snapshot)
                {
                    if(tokens.ContainsKey(visit.VisitId))continue;
                    var id=visit.VisitId;seen.Add(id);
                    tokens[id]=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token;
                    var body=world.RegisteredActors.Single(a=>a.ActorId==id);var observer=body.gameObject.AddComponent<CafeActualExitObserver>();observers.Add(observer);observer.VisitId=id;
                    observer.Receipt=(leaving,pose)=>
                    {
                        if(!tracking)return;
                        if(Vector3.Distance(pose,departure.position)>.081f || customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,tokens[leaving])).State!=ReservationState.Released ||
                            !owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==leaving && o.State==OrderState.Completed) || !exits.Add(leaving))errors.Add(leaving);
                    };
                }
                if(seen.Count>=3)customers.AutoSpawn=false;
            }
            owner.SnapshotChanged+=Watch;
            using(var trace=new TrafficWaitFailureTrace(coordinator))
            try
            {
                try
                {
                    // Source IO is outside the product callback / source hash 只在订阅前读取。
                    var root=Directory.GetParent(Application.dataPath).FullName;
                    var sourceFiles=new[]{"Assets/Scripts/Navigation/ServiceTrafficCoordinator.cs","Assets/Scripts/Customers/CustomerFlowController.cs",
                        "Assets/Scripts/CafeLoop/CafeLoopController.cs","Assets/Scripts/Employees/EmployeeFlowController.cs","Assets/Scripts/Employees/EmployeeTaskRunner.cs",
                        "Assets/Tests/PlayMode/EditorSceneLoading/Phase14/CafeServiceTrafficTests.cs",
                        ".superpowers/sdd/2026-10-30-service-traffic-coordination/task5-d2-metadata-observation-source/manifest.json"};
                    snapshotSource="baseline=A1D7B2C069B0FC7F58DA61F7415EFFB70014509E3BED8F765B42EAFEDAB3EBA0 "+string.Join(";",sourceFiles.Select(path=>{
                        using(var sha=System.Security.Cryptography.SHA256.Create())return path+"="+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(root,path)))).Replace("-","");
                    }));
                }
                catch(Exception error){snapshotErrors.Add("source binding: "+error);}
                snapshotHook.SetValue(coordinator,(Action<TrafficRequest,TrafficWorldSnapshot>)CaptureSnapshotChanged);
                snapshotHook.SetValue(coordinator,(Action<TrafficRequest,TrafficWorldSnapshot>)snapshotHook.GetValue(coordinator)+(Action<TrafficRequest,TrafficWorldSnapshot>)CaptureCounterSnapshotChanged);
                prepareHook.SetValue(coordinator,(Action<string>)CapturePrepare);
                birthHook.SetValue(customers,previousBirth+(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)CaptureBirth);
                if(commitEnabled)birthHook.SetValue(customers,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)birthHook.GetValue(customers)+(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)CaptureCommitRefusal);
                tools.ResumeCollection();customers.AutoSpawn=true;
                for(var frame=0;frame<6000 && exits.Count<3;frame++)
                {
                    yield return null;
                    foreach(DictionaryEntry pair in CafeLoopRuntimeProof.Field<IDictionary>(coordinator,"entries"))
                    {
                        var key=(TrafficIntentKey)pair.Key;if(!seen.Contains(key.ActorId))continue;
                        var entry=pair.Value;var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                        ObserveSourceAge(key,entry);
                        var decision=(TrafficDecision)entry.GetType().GetField("Decision",flags).GetValue(entry);
                        var fingerprint=decision.Kind+"/"+decision.Reason+"/"+string.Join(";",decision.Blockers.Select(b=>b.Owner.ActorId+"/"+b.Owner.IntentId+"/g"+b.Owner.Generation+"/"+b.Proof));
                        if(previous.TryGetValue(key,out var prior) && prior==fingerprint)continue;previous[key]=fingerprint;
                        if(transitions.Count>=256){transitionOverflow=true;continue;}
                        var frozen=(IEnumerable<TrafficIntentKey>)entry.GetType().GetField("FrozenOwners",flags).GetValue(entry);
                        transitions.Add("frame="+Time.frameCount+" actor="+key.ActorId+" phase="+key.IntentId+"/g"+key.Generation+" decision="+fingerprint+
                            " requestSequence="+decision.RequestSequence+" acquired="+decision.AcquiredSequence+" queries="+coordinator.QueriesConsumed(key)+
                            " remaining="+entry.GetType().GetField("WaitRemaining",flags).GetValue(entry)+" frozen="+
                            string.Join(";",(frozen??Enumerable.Empty<TrafficIntentKey>()).Select(k=>k.ActorId+"/"+k.IntentId+"/g"+k.Generation)));
                    }
                }
                TestContext.WriteLine("THREE_NATURAL admitted="+seen.Count+" actualExit="+exits.Count+" transitionOverflow="+transitionOverflow);
                foreach(var transition in transitions)TestContext.WriteLine("TRAFFIC_TRANSITION "+transition);
                TestContext.WriteLine("THREE_PREPARE_ORIGINAL records="+prepareRecords.Count+" overflow="+prepareOverflow+" errors="+string.Join(";",prepareErrors));
                foreach(var record in prepareRecords)TestContext.WriteLine(record);
                foreach(var record in sourceAgeRecords)TestContext.WriteLine("THREE_SOURCE_AGE "+record);
                TestContext.WriteLine("THREE_ADMISSION_ORIGINAL records="+birthRecords.Count+" overflow="+birthOverflow+" errors="+string.Join(";",birthErrors));
                foreach(var record in birthRecords)TestContext.WriteLine("THREE_ADMISSION_ANCHOR "+record);
                if(commitEnabled)
                {
                    TestContext.WriteLine("THREE_HANDOFF_FALSE_STATUS status="+(commitSampled?(commitErrors.Count==0?"OBSERVED":"ERROR"):"NOT_OBSERVED")+" eventCap=1 errors="+string.Join(";",commitErrors)+" source="+snapshotSource);
                    foreach(var record in commitRecords)TestContext.WriteLine(record);
                }
                TestContext.WriteLine("THREE_D2_SNAPSHOT sampled="+snapshotSampled+" eventCap=1 errors="+string.Join(";",snapshotErrors)+" source="+snapshotSource);
                foreach(var record in snapshotRecords)TestContext.WriteLine(record);
                TestContext.WriteLine("THREE_D2_COUNTER_SNAPSHOT sampled="+counterSnapshotSampled+" status="+(counterSnapshotSampled?"OBSERVED":"NOT_OBSERVED")+
                    " eventCap=1 errors="+string.Join(";",counterSnapshotErrors)+" source="+snapshotSource);
                foreach(var record in counterSnapshotRecords)TestContext.WriteLine(record);
                TestContext.WriteLine(TwentyTrafficFailureTrace.Current(world,customers,employees));
                Assert.That(counterSnapshotErrors,Is.Empty,"Read-only original counter snapshot diagnostics must not alter the product branch");
                Assert.That(snapshotErrors,Is.Empty,"Read-only original snapshot failure diagnostics must not fail or alter the product branch");
                Assert.That(birthErrors,Is.Empty,"Read-only original admission seam diagnostics must not fail or alter the product callback");
                Assert.That(birthOverflow,Is.Zero,"Original admission witness must stay bounded without dropping evidence");
                Assert.That(prepareErrors,Is.Empty,"Read-only original PrepareLeg diagnostics must not fail or mutate the callback");
                Assert.That(sourceAgeSeen.Any(k=>k.IntentId=="counter-advance"),Is.True,"FIXTURE: observe the real selected counter-advance source age");
                Assert.That(sourceAgeErrors,Is.Empty,"PRODUCT: interleaved phase proposals preserve each original source/selection age");
                Assert.That(errors,Is.Empty);Assert.That(seen.Count,Is.EqualTo(3),"FIXTURE: require three real natural admissions");
                Assert.That(exits.Contains(seen[0]) && exits.Contains(seen[1]),Is.True,"FIXTURE: earlier real customers must actually release their business and body resources");
                Assert.That(exits.Contains(seen[2]),Is.True,"PRODUCT: released pickup dependencies cannot permanently strand the later original intent");
                Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(3));Assert.That(owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed),Is.True);
                Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
            }
            finally
            {
                snapshotHook.SetValue(coordinator,previousSnapshot);
                prepareHook.SetValue(coordinator,previousPrepare);
                birthHook.SetValue(customers,previousBirth);
                tracking=false;customers.AutoSpawn=false;owner.SnapshotChanged-=Watch;
                foreach(var observer in observers)if(observer!=null)observer.Receipt=null;
            }
        }

        private IEnumerator TwoPaidWaitersProgressAfterRealHeadExit(bool checkPreparation,bool checkSibling=false)
        {
            yield return AwaitTrafficAdmission();var first=customers.Snapshot.Single().VisitId;
            yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==first && o.State==OrderState.ReadyForPickup) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            yield return AwaitTrafficAdmission();var second=customers.Snapshot.Single(v=>v.VisitId!=first).VisitId;
            yield return Until(()=>owner.Snapshot.Orders.Count==2 && owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.ReadyForPickup) &&
                customers.PickupAssignments.Count==2 && customers.PickupAssignments.All(a=>a.Arrived) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            var queue=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue");
            Assert.That(queue.Snapshot.Single(a=>a.VisitId==second).SlotIndex,Is.EqualTo(1),"FIXTURE: real second paid waiter reached slot1");
            CollectionAssert.AreEquivalent(new[]{first,second},owner.Snapshot.PaidVisitIds);
            var totals=new[]{first,second}.ToDictionary(id=>id,id=>customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers).Token);
            var receipts=new HashSet<string>();var receiptErrors=new List<string>();var tracking=true;
            var exit=CafeLoopRuntimeProof.Field<Transform>(customers,"exit");
            foreach(var id in new[]{first,second})
            {
                var actor=world.RegisteredActors.Single(a=>a.ActorId==id);var observer=actor.gameObject.AddComponent<CafeActualExitObserver>();observer.VisitId=id;
                observer.Receipt=(leaving,pose)=>
                {
                    if(!tracking)return;
                    if(Vector3.Distance(pose,exit.position)>.081f || customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,totals[leaving])).State!=ReservationState.Released ||
                        !owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==leaving && o.State==OrderState.Completed) || !receipts.Add(leaving))receiptErrors.Add(leaving);
                };
            }
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic);var previous=hook.GetValue(customers);
            var pickupHook=typeof(CustomerFlowController).GetField("PickupObserver",BindingFlags.Instance|BindingFlags.NonPublic);var previousPickup=pickupHook.GetValue(customers);
            var sampled=false;var preparationAccepted=false;var preparationReason="";long beforeVersion=0,afterVersion=0,beforeQueue=0,afterQueue=0;
            TrafficIntentKey beforeOwner=default,afterOwner=default;
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var siblings=Array.Empty<TrafficDecision>();var winnerActive=false;var winnerAcquired=0L;
            PickupQueueService publishedQueue=null;QueueAssignment publishedAssignment=default;
            var winnerFixture="";var winnerBoundary="";
            var waitTrace=checkSibling?new TrafficWaitFailureTrace(coordinator):null;
            try
            {
                if(checkPreparation || checkSibling)
                {
                    Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string> sample=(operation,id,other,target,path,obstacles,result)=>
                    {
                        if(checkSibling)
                        {
                            if(sampled || operation!="advance.published" || id!=second)return;
                            sampled=true;
                            try
                            {
                                string Key(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
                                bool Same(QueueAssignment a,QueueAssignment b)=>a.VisitId==b.VisitId && a.Generation==b.Generation &&
                                    a.SlotIndex==b.SlotIndex && a.Position==b.Position && a.Arrived==b.Arrived;
                                var oldBodyOwner=CafeLoopRuntimeProof.Field<Dictionary<string,TrafficIntentKey>>(customers,"trafficActorKeys")[second];
                                var entries=CafeLoopRuntimeProof.Field<IDictionary>(coordinator,"entries");
                                var visit=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[second];
                                // Publication precedes SelectCustomerTraffic/strict successor; body owner still identifies the predecessor.
                                // 本回调已递增business sequence，当前joint winner的context是该次递增之前的值。
                                var context=second+"/"+((int)MovementRead(visit,"TrafficPhaseSequence")-1);
                                var canonical=publishedQueue?.Snapshot.Where(a=>a.VisitId==second).ToArray();
                                var assignmentCurrent=ReferenceEquals(publishedQueue,queue) && ReferenceEquals(publishedQueue,MovementRead(visit,"PickupOwner")) &&
                                    canonical?.Length==1 && publishedAssignment.VisitId==second && publishedAssignment.Position==target &&
                                    Same(canonical[0],publishedAssignment) && Same((QueueAssignment)MovementRead(visit,"Assignment"),publishedAssignment);
                                var state=owner.Snapshot;
                                var candidates=entries.Keys.Cast<TrafficIntentKey>().Select(key=>new {Key=key,Entry=entries[key],
                                    Source=(TrafficDecision)MovementRead(entries[key],"JourneyPermit"),Request=(TrafficRequest)MovementRead(entries[key],"JourneyRequest")})
                                    .Where(row=>row.Key.Session==state.SessionId && row.Key.Epoch==state.Epoch && row.Key.LayoutRevision==adapter.Revision &&
                                        row.Key.ActorId==second && row.Key.IntentId=="pickup-advance" && row.Key.Context==context && row.Key.Generation>0 &&
                                        row.Source!=null && row.Request!=null && row.Source.Intent.Equals(row.Key) && row.Request.Intent.Equals(row.Key) &&
                                        row.Source.AcquiredSequence>0 && row.Request.ResourceId=="pickup/"+second && row.Request.Target==publishedAssignment.Position &&
                                        ReferenceEquals(row.Source,MovementRead(row.Entry,"Decision")) && row.Source.Kind==TrafficDecisionKind.Granted).ToArray();
                                siblings=entries.Values.Cast<object>().Select(e=>(TrafficDecision)MovementRead(e,"Decision"))
                                    .Where(d=>d.Intent.ActorId==second && d.Intent.IntentId=="pickup-service" && d.AcquiredSequence==0 && d.RequestSequence>0).ToArray();
                                winnerBoundary="frame="+Time.frameCount+" oldBodyOwner="+Key(oldBodyOwner)+" oldActive="+coordinator.HasActiveGrant(oldBodyOwner)+
                                    " oldAcquired="+coordinator.GetDecision(oldBodyOwner).AcquiredSequence+" assignmentCurrent="+assignmentCurrent+
                                    " assignment="+publishedAssignment.Generation+"/slot"+publishedAssignment.SlotIndex+"/"+publishedAssignment.Position+" candidates="+candidates.Length;
                                if(!assignmentCurrent || candidates.Length!=1)
                                {winnerFixture="Published canonical assignment or unique exact committed advance source missing: "+winnerBoundary;return;}
                                var winner=candidates[0];winnerActive=coordinator.HasActiveGrant(winner.Key);winnerAcquired=winner.Source.AcquiredSequence;
                                winnerBoundary+=" winner="+Key(winner.Key)+" winnerActive="+winnerActive+" winnerAcquired="+winnerAcquired+
                                    " winnerRetired="+MovementRead(winner.Entry,"Retired")+" winnerKind="+winner.Source.Kind;
                            }
                            catch(Exception error){winnerFixture=error.ToString();}
                            return;
                        }
                        if(sampled || operation!="lease.departure-removed" || id!=first)return;
                        sampled=true;var before=CurrentTrafficFacts();beforeVersion=before.Version;beforeQueue=queue.Version;
                        beforeOwner=before.Parking.Single(p=>p.Owner.ActorId==second && p.ResourceId=="pickup/"+second).Owner;
                        // Public proposal during a real slot1 -> head transition; never run the ready batch here.
                        preparationAccepted=customers.TryMoveToPickupService(second,out preparationReason);
                        var after=CurrentTrafficFacts();afterVersion=after.Version;afterQueue=queue.Version;
                        afterOwner=after.Parking.Single(p=>p.Owner.ActorId==second && p.ResourceId=="pickup/"+second).Owner;
                    };
                    if(checkSibling)pickupHook.SetValue(customers,(Action<string,string,PickupQueueService,QueueAssignment>)((operation,id,ownerQueue,assignment)=>
                    {
                        if(!sampled && operation=="advance.published" && id==second){publishedQueue=ownerQueue;publishedAssignment=assignment;}
                        sample(operation,id,null,assignment.Position,null,null,null);
                    }));
                    else hook.SetValue(customers,sample);
                }
                tools.ResumeCollection();
                var windowStart=Time.frameCount;
                for(var frame=0;frame<6000 && (checkPreparation||checkSibling?!sampled:receipts.Count<2);frame++)yield return null;
                TestContext.WriteLine("TWO_PAID_WAITERS sampled="+sampled+" proposalAccepted="+preparationAccepted+" reason="+preparationReason+
                    " canonicalVersion="+beforeVersion+"->"+afterVersion+" queueVersion="+beforeQueue+"->"+afterQueue+" canonicalOwnerEqual="+beforeOwner.Equals(afterOwner)+
                    " firstActualExit="+receipts.Contains(first)+" secondActualExit="+receipts.Contains(second));
                TestContext.WriteLine(TwentyTrafficFailureTrace.Current(world,customers,employees));
                Assert.That(receiptErrors,Is.Empty);
                if(checkSibling)
                {
                    TestContext.WriteLine("REAL_SIBLING_BOUNDARY "+winnerBoundary);
                    TestContext.WriteLine("REAL_SIBLING winnerActive="+winnerActive+" winnerAcquired="+winnerAcquired+" siblings="+
                        string.Join(";",siblings.Select(d=>d.Intent.IntentId+"/g"+d.Intent.Generation+"/"+d.Kind+"/acquired"+d.AcquiredSequence+"/seq"+d.RequestSequence)));
                    Assert.That(winnerFixture,Is.Empty,"FIXTURE: identify the exact committed advance source at its real publication boundary");
                    Assert.That(sampled && winnerActive && winnerAcquired>0,Is.True,"FIXTURE: real advance must jointly commit before the publication witness");
                    Assert.That(siblings.Length,Is.GreaterThan(0),"FIXTURE: require an actually evaluated, never-acquired alternative service proposal");
                    Assert.That(siblings.All(d=>d.Kind==TrafficDecisionKind.Cancelled),Is.True,"PRODUCT: the real winner must retire its unselected provisional before publication");
                    while(Time.frameCount-windowStart<6000 && receipts.Count<2)yield return null;
                    TestContext.WriteLine("REAL_SIBLING_COMPLETION firstActualExit="+receipts.Contains(first)+" sameSecondActualExit="+receipts.Contains(second));
                    if(receipts.Count<2)TestContext.WriteLine(TwentyTrafficFailureTrace.Current(world,customers,employees));
                    Assert.That(receiptErrors,Is.Empty);
                    Assert.That(receipts.Contains(first) && receipts.Contains(second),Is.True,"PRODUCT: discarded alternative must not strand the same paid second customer before its real Exit");
                }
                else if(checkPreparation)
                {
                    Assert.That(sampled,Is.True,"FIXTURE: real first pickup departure must release its lease");
                    Assert.That(preparationAccepted,Is.False,"FIXTURE: newly proposed service intent must await its normal batch");
                    Assert.That(preparationReason,Does.Contain("ready-batch-pending"));
                    Assert.That(afterQueue,Is.EqualTo(beforeQueue),"No canonical Queue mutation before permission");
                    Assert.That(afterOwner,Is.EqualTo(beforeOwner),"PRODUCT: uncommitted phase proposal must not publish a new canonical parking owner");
                    Assert.That(afterVersion,Is.EqualTo(beforeVersion),"PRODUCT: unchanged canonical facts must retain their version");
                }
                else
                {
                    Assert.That(receipts.Contains(first),Is.True,"FIXTURE: actual first Exit is required");
                    Assert.That(receipts.Contains(second),Is.True,"PRODUCT: ready paid slot1 owner must advance/collect and actually Exit within the bounded window");
                    Assert.That(owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed),Is.True);
                    Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                }
            }
            finally{tracking=false;hook.SetValue(customers,previous);pickupHook.SetValue(customers,previousPickup);waitTrace?.Dispose();}
        }

        [UnityTest] public IEnumerator RetiredEmployeeJourneyDoesNotMaskActualNextWorkingProgress()
        {
            yield return AwaitTrafficAdmission();var first=customers.Snapshot.Single().VisitId;
            yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==first && o.State==OrderState.ReadyForPickup) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            var actorId=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).ActorId;
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var keys=CafeLoopRuntimeProof.Field<Dictionary<string,TrafficIntentKey>>(customers,"trafficKeys");
            var old=keys.Values.Single(k=>k.ActorId==actorId && k.IntentId=="employee-pickup-journey");
            Assert.That(coordinator.GetDecision(old).Kind,Is.EqualTo(TrafficDecisionKind.Cancelled),"FIXTURE: first real return retired the selected journey");
            yield return AwaitTrafficAdmission();
            yield return Until(()=>employees.Snapshot.Any(e=>e.ActorId==actorId && e.State==EmployeeState.Working && e.RemainingWorkSeconds>.25f));
            var fact=CurrentTrafficFacts().Bodies.Single(b=>b.Owner.ActorId==actorId);
            var arguments=new object[]{fact.Owner,0f};
            var progressing=(bool)typeof(ServiceTrafficCoordinator).GetMethod("CanProgress",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(coordinator,arguments);
            TestContext.WriteLine("NEXT_WORKING_PROGRESS actual="+employees.Snapshot.Single(e=>e.ActorId==actorId).State+" bodyEnabled="+fact.Enabled+
                " bodyFinite="+fact.IndependentlyProgressing+" remaining="+fact.RemainingSeconds+" oldOwner="+fact.Owner.Equals(old)+" result="+progressing+" proofSeconds="+arguments[1]);
            Assert.That(fact.Enabled && fact.IndependentlyProgressing && fact.RemainingSeconds>0,Is.True,"FIXTURE: actual enabled Working body has a finite remaining task");
            Assert.That(progressing,Is.True,"PRODUCT: retired previous journey must not mask the next real Working checkpoint");
            Assert.That((float)arguments[1],Is.GreaterThan(0));
        }

        [UnityTest] public IEnumerator InitialExitNativeNullTerminatesWithoutLosingCollectedFacts()
            => InitialExitTerminalPreservesFacts("native-null","bounded-route-proof-failed");
        [UnityTest] public IEnumerator InitialExitStaticBodyTerminatesWithoutLosingCollectedFacts()
            => InitialExitTerminalPreservesFacts("static-body","no-independent-finite-release-or-cycle");
        [UnityTest] public IEnumerator InitialExitFiniteWaitExhaustionTerminatesWithoutLosingCollectedFacts()
            => InitialExitTerminalPreservesFacts("finite-wait","wait-budget-exhausted");
        [UnityTest] public IEnumerator InitialExitStaticBodyExplicitRecoveryRenewsIntentAndActuallyExits()
            => InitialExitTerminalPreservesFacts("static-body","no-independent-finite-release-or-cycle",true);

        // Controlled integration: all business and arrival facts are produced by public real flow.
        // 仅在真实领取后的dispatch观察点改变fixture出口；不伪造path、Arrived、付款或杯子。
        private IEnumerator InitialExitTerminalPreservesFacts(string mode,string expectedReason,bool recover=false)
        {
            yield return AwaitTrafficAdmission();
            var id=customers.Snapshot.Single().VisitId;
            yield return Until(()=>owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==id && o.State==OrderState.ReadyForPickup) &&
                employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            yield return Until(()=>
            {
                customers.TryMoveToPickupService(id,out _);
                return customers.CanCollectPickup(id);
            });
            Assert.That(owner.HoldCollection,Is.True);
            var order=owner.Snapshot.Orders.Single(p=>p.Value.CustomerId==id).Key;
            var body=world.RegisteredActors.Single(a=>a.ActorId==id);
            var total=customers.Capacity.GetReservations().Single(r=>r.OwnerId==id && r.Kind==CapacityKind.TotalCustomers);
            var token=total.Token;var tokenState=total.State;
            var exit=CafeLoopRuntimeProof.Field<Transform>(customers,"exit");var originalExit=exit.position;
            var coordinator=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(owner,"traffic");
            var waitField=typeof(ServiceTrafficCoordinator).GetField("waitSeconds",BindingFlags.Instance|BindingFlags.NonPublic);
            var originalWait=(float)waitField.GetValue(coordinator);
            var hook=typeof(CustomerFlowController).GetField("CorridorObserver",BindingFlags.Instance|BindingFlags.NonPublic);
            var originalHook=hook.GetValue(customers);
            NavigationActor obstruction=null;NavigationActor finiteOwner=null;
            var dispatched=false;var observedWaiting=false;var actualNativeNull=false;var actualExit=false;var validActualExit=false;var observing=true;
            var observer=body.gameObject.AddComponent<CafeActualExitObserver>();observer.VisitId=id;
            observer.Receipt=(leaving,pose)=>
            {
                if(!observing)return;actualExit=true;
                validActualExit=Vector3.Distance(pose,originalExit)<=.081f &&
                    customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,token)).State==ReservationState.Released;
            };
            try
            {
                if(mode=="finite-wait")
                {
                    yield return AwaitTrafficAdmission();
                    yield return Until(()=>employees.Snapshot.Any(e=>e.Role==EmployeeRole.Cashier && e.State==EmployeeState.Working && e.RemainingWorkSeconds>.25f));
                    var cashier=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier);
                    finiteOwner=world.RegisteredActors.Single(a=>a.ActorId==cashier.ActorId);
                    // Short policy budget makes a real, still-working independent owner exhaust it deterministically.
                    // 只缩短本fixture等待上限；真实工作剩余时间和身体位置不改。
                    waitField.SetValue(coordinator,.001f);
                }
                Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string> onDispatch=(operation,visit,other,target,path,obstacles,result)=>
                {
                    if(operation!="exit.actual-dispatch" || visit!=id || dispatched)return;
                    dispatched=true;
                    Assert.That(owner.Snapshot.Orders[order].State,Is.EqualTo(OrderState.Completed),"FIXTURE: genuine collection precedes dispatch");
                    if(mode=="native-null")
                    {
                        exit.position=originalExit+Vector3.right*1000;
                        actualNativeNull=adapter.GetCompletePath(body.transform.position,exit.position)==null;
                    }
                    else if(mode=="static-body")obstruction=Blocker(originalExit,"initial-exit-static-body");
                    else
                    {
                        Assert.That(employees.Snapshot.Single(e=>e.ActorId==finiteOwner.ActorId).State,Is.EqualTo(EmployeeState.Working));
                        exit.position=finiteOwner.transform.position;
                    }
                };
                hook.SetValue(customers,onDispatch);tools.ResumeCollection();
                for(var frame=0;frame<600 && !dispatched;frame++)yield return null;
                Assert.That(dispatched,Is.True,"FIXTURE: real collection must reach the initial exit entry");
                var keys=CafeLoopRuntimeProof.Field<Dictionary<string,TrafficIntentKey>>(customers,"trafficKeys");
                var key=keys.Values.Single(k=>k.ActorId==id && k.IntentId=="actual-exit");
                TrafficDecision terminal=null;
                for(var frame=0;frame<120;frame++)
                {
                    var decision=coordinator.GetDecision(key);
                    observedWaiting|=decision.Kind==TrafficDecisionKind.WaitingForTraffic;
                    if(decision.Kind==TrafficDecisionKind.PlanningFailed){terminal=decision;break;}
                    yield return null;
                }
                TestContext.WriteLine("INITIAL_EXIT_TERMINAL mode="+mode+" dispatched="+dispatched+" nativeNull="+actualNativeNull+
                    " waiting="+observedWaiting+" kind="+terminal?.Kind+" reason="+terminal?.Reason+" queries="+coordinator.QueriesConsumed(key)+
                    " requestSequence="+terminal?.RequestSequence+" body="+body.transform.position.ToString("F9")+" target="+exit.position.ToString("F9"));
                Assert.That(terminal,Is.Not.Null,"FIXTURE: require actual typed terminal decision, not an initial Deferred");
                Assert.That(terminal.Reason,Is.EqualTo(expectedReason));
                if(mode=="native-null")Assert.That(actualNativeNull,Is.True,"FIXTURE: actual owned Native query returns null");
                if(mode=="finite-wait")Assert.That(observedWaiting,Is.True,"FIXTURE: actual finite independent release must first enter WaitingForTraffic");
                var queries=coordinator.QueriesConsumed(key);var requestSequence=terminal.RequestSequence;
                var heldCup=body.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="CustomerHeldCup");
                var heldPose=body.transform.position;
                for(var frame=0;frame<8;frame++)yield return null;
                var visitEntry=CafeLoopRuntimeProof.Field<IDictionary>(customers,"visits")[id];
                object Read(string name)=>visitEntry.GetType().GetField(name).GetValue(visitEntry);
                TestContext.WriteLine("INITIAL_EXIT_FINAL mode="+mode+" state="+Read("State")+" pending="+Read("PendingExitSegment")+
                    " request="+Read("RequestId")+" collected="+Read("HasCollected")+" failure="+Read("Failure")+" actualExit="+actualExit);
                Assert.That(keys.Values.Count(k=>k.ActorId==id && k.IntentId=="actual-exit"),Is.EqualTo(1),"No fresh intent may reset terminal budgets");
                Assert.That(coordinator.GetDecision(key).RequestSequence,Is.EqualTo(requestSequence));
                Assert.That(coordinator.QueriesConsumed(key),Is.EqualTo(queries));
                Assert.That(owner.Snapshot.PaidVisitIds.Contains(id),Is.True);
                Assert.That(owner.Snapshot.Orders[order].State,Is.EqualTo(OrderState.Completed));
                Assert.That(Read("HasCollected"),Is.EqualTo(true));
                Assert.That(heldCup!=null,Is.True);Assert.That(body.transform.position,Is.EqualTo(heldPose));
                Assert.That(world.RegisteredActors.Contains(body),Is.True);Assert.That(actualExit,Is.False);
                Assert.That(customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,token)).State,Is.EqualTo(tokenState));
                Assert.That(Read("RequestId"),Is.EqualTo(0L));
                Assert.That(customers.Snapshot.Single(v=>v.VisitId==id).State,Is.EqualTo(CustomerVisitState.Blocked),
                    "PRODUCT: same-context terminal initial-exit decision must explicitly fail, never remain pending forever");
                Assert.That(Read("PendingExitSegment"),Is.EqualTo(false));
                Assert.That((string)Read("Failure"),Does.Contain(expectedReason));
                if(recover)
                {
                    var exitSequence=Read("ExitSequence");
                    // Real repair plus an explicit recovery command, never an automatic fresh budget.
                    // 移除真实障碍后明确恢复；付款、领取和原Total token继续沿用。
                    world.Unregister(obstruction);obstruction.gameObject.SetActive(false);UnityEngine.Object.Destroy(obstruction.gameObject);obstruction=null;
                    tools.ResumeBlockedFlow();
                    Assert.That(coordinator.GetDecision(key).Kind,Is.EqualTo(TrafficDecisionKind.Cancelled),
                        "PRODUCT: explicit recovery must retire the failed initial-exit intent");
                    var replacement=keys.Values.Single(k=>k.ActorId==id && k.IntentId=="actual-exit");
                    Assert.That(replacement.Equals(key),Is.False,"Only explicit recovery acquires a new phase intent");
                    Assert.That(replacement.Generation,Is.GreaterThan(key.Generation));
                    Assert.That(Read("ExitSequence"),Is.EqualTo(exitSequence));
                    Assert.That(world.RegisteredActors.Single(a=>a.ActorId==id),Is.SameAs(body));
                    Assert.That(heldCup!=null,Is.True);
                    for(var frame=0;frame<1200 && !actualExit;frame++)yield return null;
                    Assert.That(actualExit && validActualExit,Is.True,"PRODUCT: repaired owner must reach the real exit and release its original Total token");
                    Assert.That(owner.Snapshot.Orders[order].State,Is.EqualTo(OrderState.Completed));
                    Assert.That(owner.Snapshot.PaidVisitIds.Count(v=>v==id),Is.EqualTo(1));
                }
            }
            finally
            {
                observing=false;hook.SetValue(customers,originalHook);exit.position=originalExit;waitField.SetValue(coordinator,originalWait);
                if(obstruction!=null){world.Unregister(obstruction);obstruction.gameObject.SetActive(false);UnityEngine.Object.Destroy(obstruction.gameObject);}
            }
        }

        [Test] public void R19NativeCostCountsActualCallsAndLeavesOffSilent()
        {
            var probe=typeof(NavigationLayoutAdapter).Assembly.GetType("AnimalCafe.Navigation.NavigationCostObservation",true);
            const BindingFlags fields=BindingFlags.Static|BindingFlags.NonPublic;
            var native=probe.GetField("NativeCall",fields);var ready=probe.GetField("ProcessReadyElapsed",fields);var errors=probe.GetField("ObserverErrors",fields);
            var priorNative=native.GetValue(null);var priorReady=ready.GetValue(null);var priorErrors=(int)errors.GetValue(null);
            Assert.That(priorNative,Is.Null);Assert.That(priorReady,Is.Null);
            var pose=world.RegisteredActors.First().transform.position;
            Assert.That(adapter.IsPointWalkable(pose),Is.True,"FIXTURE: real existing baked floor, no pose write");
            var calls=new List<int[]>();var elapsed=new List<long>();var readyFrames=new List<int>();
            var core=new ServiceTrafficCoordinator("r19",1,1,new TrafficWorldSnapshot(1,1),(from,to)=>throw new InvalidOperationException("Unexpected query in empty ready batch"));
            try
            {
                var off=adapter.GetCompletePath(pose,pose);Assert.That(off,Is.Not.Null);
                Assert.That(adapter.HasClearPath(pose,pose),Is.True);core.ProcessReady(1);
                Assert.That(calls,Is.Empty);Assert.That(elapsed,Is.Empty);Assert.That((int)errors.GetValue(null),Is.EqualTo(priorErrors));
                native.SetValue(null,(Action<int,int,int>)((site,frame,outcome)=>calls.Add(new[]{site,frame,outcome})));
                ready.SetValue(null,(Action<int,long>)((frame,ticks)=>{readyFrames.Add(frame);elapsed.Add(ticks);}));
                var frameNow=Time.frameCount;
                Assert.That(adapter.HasClearPath(pose,pose),Is.True);var on=adapter.GetCompletePath(pose,pose);
                CollectionAssert.AreEqual(off,on,"Observation must not change the actual Native result");
                var filter=(UnityEngine.AI.NavMeshQueryFilter)typeof(NavigationLayoutAdapter).GetProperty("OwnedFilter",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(adapter);
                // One real Native false-result smoke at the diagnostic primitive, not claimed as a Driver branch.
                // 真实off-mesh起点返回false；不伪造Native结果，也不称已覆盖Driver rebind/edge。
                var failed=(bool)probe.GetMethod("CalculatePath",fields).Invoke(null,new object[]{1,new Vector3(10000,0,10000),pose,filter,new UnityEngine.AI.NavMeshPath()});
                Assert.That(failed,Is.False,"FIXTURE: raw Native off-mesh start fails");
                core.ProcessReady(2);Assert.Throws<ArgumentOutOfRangeException>(()=>core.ProcessReady(1));
                Assert.That(calls,Has.Count.EqualTo(3),"FEATURE: exactly two actual Adapter calls and one raw Native failure");
                CollectionAssert.AreEqual(new[]{0,1,1},calls.Select(c=>c[0]));CollectionAssert.AreEqual(new[]{1,1,0},calls.Select(c=>c[2]));
                Assert.That(calls.All(c=>c[1]==frameNow),Is.True);Assert.That(elapsed,Has.Count.EqualTo(2));
                CollectionAssert.AreEqual(new[]{2,1},readyFrames);Assert.That(elapsed.All(t=>t>=0),Is.True);
                Assert.That(core.QueriesThisFrame,Is.Zero);Assert.That((int)errors.GetValue(null),Is.EqualTo(priorErrors));
            }
            finally{native.SetValue(null,priorNative);ready.SetValue(null,priorReady);errors.SetValue(null,priorErrors);}
        }

        [Test] public void R19CostObserverExceptionsPreserveResultsAndOriginalExceptions()
        {
            var probe=typeof(NavigationLayoutAdapter).Assembly.GetType("AnimalCafe.Navigation.NavigationCostObservation",true);
            const BindingFlags fields=BindingFlags.Static|BindingFlags.NonPublic;
            var native=probe.GetField("NativeCall",fields);var ready=probe.GetField("ProcessReadyElapsed",fields);var errors=probe.GetField("ObserverErrors",fields);
            var priorNative=native.GetValue(null);var priorReady=ready.GetValue(null);var priorErrors=(int)errors.GetValue(null);
            Assert.That(priorNative,Is.Null);Assert.That(priorReady,Is.Null);
            var pose=world.RegisteredActors.First().transform.position;var expected=adapter.GetCompletePath(pose,pose);Assert.That(expected,Is.Not.Null);
            var core=new ServiceTrafficCoordinator("r19-throws",1,1,new TrafficWorldSnapshot(1,1),(from,to)=>throw new InvalidOperationException("Unexpected query"));
            try
            {
                native.SetValue(null,(Action<int,int,int>)((site,frame,outcome)=>throw new InvalidOperationException("Intentional observer error")));
                ready.SetValue(null,(Action<int,long>)((frame,ticks)=>throw new InvalidOperationException("Intentional observer error")));
                CollectionAssert.AreEqual(expected,adapter.GetCompletePath(pose,pose));
                Assert.DoesNotThrow(()=>core.ProcessReady(2));Assert.Throws<ArgumentOutOfRangeException>(()=>core.ProcessReady(1));
                Assert.That((int)errors.GetValue(null)-priorErrors,Is.EqualTo(3),"FEATURE: all observer errors isolated, including original exception exit");
                Assert.That(core.QueriesThisFrame,Is.Zero);
            }
            finally{native.SetValue(null,priorNative);ready.SetValue(null,priorReady);errors.SetValue(null,priorErrors);}
        }

        private Action r19CostCleanup;
        [TearDown] public void CleanupR19CostObservation()
        {var cleanup=r19CostCleanup;r19CostCleanup=null;cleanup?.Invoke();}

        // Main opt-in only / 复用原只读 witness；错误在 mandatory cleanup 后报告。
        private R17DeliveryFailureWitness BeginMainDeliveryFailureWitness(ServiceTrafficOriginalTrace trace, out Action finish)
        {
            finish=null;
            if(!Environment.GetCommandLineArgs().Contains("-mainDeliveryFailureWitness") &&
                !Environment.GetCommandLineArgs().Contains("-mainDeliveryFailureBoundaryOnly"))return null;
            var r=new R17Witness { Tracking=false, Stage="Main-original-window", Directory=Path.Combine("outputs","phase15",
                "main-delivery-failure-witness",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")) };
            Directory.CreateDirectory(r.Directory);
            var errors=new List<Exception>();var finished=false;var preCalled=false;var postCalled=false;
            var priorPre=controlledHandoffCleanup;var priorPost=parkingEndpointWitnessCleanup;
            Action<string> pre=null;Action post=null;
            void Attempt(Action action){try{action();}catch(Exception error){errors.Add(error);}}
            Action finishWitness=()=>
            {
                if(finished)return;finished=true;
                Attempt(()=>r.DeliveryWitness?.Finish());
                Attempt(()=>TestContext.WriteLine("MAIN_DELIVERY_FAILURE_WITNESS directory="+r.Directory+" tracking=False errors="+errors.Count));
            };
            pre=reason=>
            {
                if(preCalled)return;preCalled=true;
                Attempt(finishWitness); // Restore witness before the outer trace / LIFO.
                Attempt(trace.Dispose); // Idempotent fallback if Unity did not dispose the test iterator.
                if(ReferenceEquals(controlledHandoffCleanup,pre))controlledHandoffCleanup=priorPre;
                else errors.Add(new InvalidOperationException("Main pre-cleanup hook replaced; replacement preserved"));
                Attempt(()=>priorPre?.Invoke(reason)); // Never prevent mandatory scene/input/time cleanup.
            };
            post=()=>
            {
                if(postCalled)return;postCalled=true;
                if(ReferenceEquals(parkingEndpointWitnessCleanup,post))parkingEndpointWitnessCleanup=priorPost;
                else errors.Add(new InvalidOperationException("Main post-cleanup hook replaced; replacement preserved"));
                Attempt(()=>priorPost?.Invoke());
                if(!preCalled || !finished)errors.Add(new InvalidOperationException("Main pre-unload cleanup did not complete"));
                Assert.That(errors,Is.Empty,"MAIN_DELIVERY_WITNESS: mandatory cleanup completed; "+string.Join(";",errors));
            };
            controlledHandoffCleanup=pre;parkingEndpointWitnessCleanup=post;finish=finishWitness;
            r.DeliveryWitness=new R17DeliveryFailureWitness(this,r);
            return r.DeliveryWitness;
        }

        [UnityTest,Timeout(1500000)] public IEnumerator MainDefaultCounterPlacementCompletesThreeFreshNaturalCohorts()
        {
            for(var iteration=1;iteration<=3;iteration++)
            {
                TestContext.WriteLine("MAIN_FRESH_COHORT iteration="+iteration);
                yield return MainDefaultCounterPlacementCompletesServiceWithoutTrafficDeadlock();
            }
        }

        // Regression: a later parking reservation must not strand a Prepared delivery.
        // 真实Main、自然来客、真实Exit；不注入route／身体，也不调用恢复按钮。
        [UnityTest,Timeout(480000)]
        public IEnumerator MainDefaultCounterPlacementCompletesServiceWithoutTrafficDeadlock()
        {
            yield return TrafficOriginalMainLayout();
            var seen=new List<string>();
            var tokens=new Dictionary<string,CapacityToken>();
            var exits=new Dictionary<string,Vector3>();
            var receiptErrors=new List<string>();
            var observers=new List<CafeActualExitObserver>();
            var tracking=true;
            var stoppedArrivals=false;
            var drainSceneStart=0f;var drainWallStart=0d;var drainSeconds=0d;
            var drained=false;
            var firstBlocked="";
            // Boundary-only diagnosis never samples current state every frame / 只在原拒绝回调取证。
            var boundaryWitnessOnly=Environment.GetCommandLineArgs().Contains("-mainDeliveryFailureBoundaryOnly");
            var storedWait=true; // Test-only passive failure cache; no observer or pre-failure sampling.
            var storedWaitCore=CafeLoopRuntimeProof.Field<ServiceTrafficCoordinator>(customers,"traffic");
            var storedWaitIntent=Environment.GetCommandLineArgs().FirstOrDefault(arg=>arg.StartsWith("-waitFailureIntent=",StringComparison.Ordinal))?.Substring("-waitFailureIntent=".Length)??"employee-pickup-journey";
            if(storedWait)typeof(ServiceTrafficCoordinator).GetField("WaitFailureDiagnosticIntent",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(storedWaitCore,storedWaitIntent);
            TestContext.WriteLine("MAIN_WAIT_FAILURE_INTENT "+storedWaitIntent);
            var sceneStart=Time.time;
            var timer=Stopwatch.StartNew();
            // Controlled input only: real clock and collection controls; no visit/order/Native mutation.
            // 仅重放合法来客输入；原 driver 仍决定付款、站位、移动和离店。
            var replay=Environment.GetCommandLineArgs().FirstOrDefault(arg=>arg.StartsWith("-mainArrivalReplay=",StringComparison.Ordinal))?.Substring("-mainArrivalReplay=".Length);
            var replayEnabled=!string.IsNullOrEmpty(replay);
            Assert.That(!replayEnabled || replay=="paid" || replay=="paid-gate" || replay=="exit" || replay=="exit-gate" || replay=="exit-after-head",Is.True,"FIXTURE: known controlled replay mode");
            var replayTargets=replay=="exit" || replay=="exit-gate" || replay=="exit-after-head"?new[]{6.72209f,10.97073f,12.74005f}:
                replay=="paid-gate"?new[]{1.20890f,2.96570f,11.76960f}:new[]{1.20890f,2.96570f,11.76960f,15.48880f};
            var originalClock=CafeLoopRuntimeProof.Field<CustomerSpawnClock>(customers,"clock");
            var originalCollection=owner.HoldCollection;
            var replayCollectionReleased=replay!="paid-gate";var replayArrivalsHeld=false;
            var replayExitCollectionHeld=false;var replayExitArrivalWaitObserved=false;
            var replaySecondHeadHeld=false;var replayThirdResumed=false;var replayThirdNativeLogged=false;
            object ReplayEntry(string actor,string intent)
            {
                foreach(DictionaryEntry pair in (IDictionary)MovementRead(storedWaitCore,"entries"))
                {
                    var key=(TrafficIntentKey)pair.Key;
                    if(key.ActorId==actor && key.IntentId==intent && !(bool)MovementRead(pair.Value,"Retired"))return pair.Value;
                }
                return null;
            }
            using(var trace=new ServiceTrafficOriginalTrace(customers,employees,world))
            {
                if(trace.FixedCostWindow)r19CostCleanup=trace.EndCostWindow;
                void WatchVisits(CafeLoopSnapshot snapshot)
                {
                    if(!tracking)return;
                    foreach(var visit in customers.Snapshot)
                    {
                        if(tokens.ContainsKey(visit.VisitId))continue;
                        var actor=world.RegisteredActors.FirstOrDefault(a=>a.ActorId==visit.VisitId);
                        var total=customers.Capacity.GetReservations().Where(r=>r.OwnerId==visit.VisitId && r.Kind==CapacityKind.TotalCustomers).ToArray();
                        if(actor==null || total.Length!=1)
                        { receiptErrors.Add("Missing actual body/original Total token for "+visit.VisitId);continue; }
                        var id=visit.VisitId;
                        tokens.Add(id,total[0].Token);seen.Add(id);
                        var observer=actor.gameObject.AddComponent<CafeActualExitObserver>();
                        observers.Add(observer);observer.VisitId=id;
                        observer.Receipt=(leaving,pose)=>
                        {
                            if(!tracking)return;
                            var released=customers.Capacity.GetReservations().Where(r=>ReferenceEquals(r.Token,tokens[leaving])).ToArray();
                            var completed=owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==leaving && o.State==OrderState.Completed);
                            var atExit=Vector3.Distance(pose,CafeLoopRuntimeProof.Field<Transform>(customers,"exit").position)<=.081f;
                            if(!completed || !atExit || released.Length!=1 || released[0].State!=ReservationState.Released)
                                receiptErrors.Add("Invalid actual Exit "+leaving+" completed="+completed+" atExit="+atExit+" token="+(released.Length==1?released[0].State.ToString():"MISSING"));
                            if(exits.ContainsKey(leaving))receiptErrors.Add("Duplicate Exit "+leaving);
                            else exits.Add(leaving,pose);
                            trace.Record("ACTUAL_EXIT id="+leaving+" pose="+pose.ToString("F9")+" completed="+completed+" atExit="+atExit+
                                " originalTotal="+(released.Length==1?released[0].State.ToString():"MISSING"));
                        };
                        trace.Record("NATURAL_ADMISSION id="+id+" originalTotal="+total[0].Token);
                    }
                }
                R17DeliveryFailureWitness deliveryWitness=null;Action finishDeliveryWitness=null;
                UnpaidBudgetWitness unpaidBudget=null;
                owner.SnapshotChanged+=WatchVisits;
                try
                {
                    if(Environment.GetCommandLineArgs().Contains("-unpaidBudgetWitness"))unpaidBudget=new UnpaidBudgetWitness(customers,world);
                    deliveryWitness=BeginMainDeliveryFailureWitness(trace,out finishDeliveryWitness);
                    trace.InstallReservationWaitFailureWitness();
                    // Preserve the default production random clock and 1x speed / 不改来客抽样或timer。
                    Assert.That(time.TrySetSpeed(GameSpeed.Normal),Is.True,"FIXTURE: Main must run at normal speed");
                    trace.BeginCostWindow();
                    if(replayEnabled)
                    {
                        Assert.That(CafeLoopRuntimeProof.Field<long>(customers,"nextVisit"),Is.Zero,"FIXTURE: replay starts before actual birth");
                        CafeLoopRuntimeProof.Set(customers,"clock",new CustomerSpawnClock(()=>
                        {
                            var next=CafeLoopRuntimeProof.Field<long>(customers,"nextVisit");
                            if(replay=="exit-after-head" && next==2)return 0f;
                            var tickCompensation=replay=="exit-after-head"?Time.deltaTime:0f;
                            return next>=replayTargets.Length?1f:Mathf.Clamp01((replayTargets[(int)next]-(Time.time-sceneStart)+tickCompensation)/10f);
                        }));
                        trace.Record("CONTROLLED_ARRIVAL_REPLAY mode="+replay+" targets="+string.Join(";",replayTargets)+" bound=90");
                    }
                    if(replayCollectionReleased)tools.ResumeCollection();else tools.HoldCollection();
                    tools.ResumeArrivals();
                    // A natural cohort may still be preparing its last order at 120s. Give its one-way stop
                    // one bounded drain window; R19 fixed-cost sampling retains its original exact 120s.
                    // 停止来客时仅记录一次排空起点；进度/Waiting不续期，全部原业务断言仍须通过。
                    while(replayEnabled?Time.time-sceneStart<90f && timer.Elapsed.TotalSeconds<90d:
                        (!stoppedArrivals || trace.FixedCostWindow
                        ? Time.time-sceneStart<120f && timer.Elapsed.TotalSeconds<120d
                        : Time.time-drainSceneStart<drainSeconds && timer.Elapsed.TotalSeconds-drainWallStart<drainSeconds))
                    {
                        WatchVisits(owner.Snapshot);
                        if(replayEnabled && !replayArrivalsHeld && seen.Count>=replayTargets.Length)
                        { tools.HoldArrivals();replayArrivalsHeld=true; }
                        // Legal zero-delay opportunity after the actual second head arrival; no forced admission.
                        // 第二位真正到柜台后才开放第三位来客，避免提前一帧启动另一段准备过程。
                        if(replay=="exit-after-head" && seen.Count==2 && !replaySecondHeadHeld)
                        { tools.HoldArrivals();replaySecondHeadHeld=true;trace.Record("CONTROLLED_SECOND_HEAD_HOLD second="+seen[1]); }
                        if(replay=="exit-after-head" && replaySecondHeadHeld && !replayThirdResumed && seen.Count==2 &&
                            customers.CounterAssignments.Any(a=>a.VisitId==seen[1] && a.Arrived))
                        { tools.ResumeArrivals();replayThirdResumed=true;trace.Record("CONTROLLED_THIRD_OPPORTUNITY secondActualCounterArrived="+seen[1]); }
                        if(replay=="exit-after-head" && !replayThirdNativeLogged && seen.Count>=3)
                        {
                            var third=ReplayEntry(seen[2],"pickup-paid-movement");var leg=third==null?null:MovementRead(third,"Leg");
                            if(leg!=null && (bool)MovementRead(leg,"Committed") && (bool)MovementRead(leg,"Bound") && !(bool)MovementRead(leg,"Completed"))
                            { replayThirdNativeLogged=true;trace.Record("CONTROLLED_THIRD_ACTUAL_BOUND third="+seen[2]); }
                        }
                        if(replay=="exit-gate" && seen.Count>=1 && !replayExitCollectionHeld && owner.Snapshot.CollectedExitVisitIds.Contains(seen[0]))
                        { tools.HoldCollection();replayExitCollectionHeld=true;replayCollectionReleased=false;trace.Record("CONTROLLED_EXIT_HOLD afterActualCollected="+seen[0]); }
                        if(replay=="exit-gate" && replayExitCollectionHeld && !replayCollectionReleased && seen.Count>=3 &&
                            customers.PickupAssignments.Any(a=>a.VisitId==seen[1] && a.Arrived))
                        {
                            var third=ReplayEntry(seen[2],"pickup-paid-movement");var leg=third==null?null:MovementRead(third,"Leg");
                            if(leg!=null && (bool)MovementRead(third,"Active") && (bool)MovementRead(leg,"Committed") &&
                                (bool)MovementRead(leg,"Bound") && !(bool)MovementRead(leg,"Completed"))
                            { tools.ResumeCollection();replayCollectionReleased=true;trace.Record("CONTROLLED_EXIT_RELEASE thirdActualBound="+seen[2]); }
                        }
                        if((replay=="exit-gate" && replayExitCollectionHeld && replayCollectionReleased || replay=="exit-after-head") &&
                            !replayExitArrivalWaitObserved && seen.Count>=3 &&
                            customers.PickupAssignments.Any(a=>a.VisitId==seen[2] && a.Arrived))
                        {
                            var second=ReplayEntry(seen[1],"actual-exit");var decision=second==null?null:MovementRead(second,"Decision") as TrafficDecision;
                            var frozen=second==null?null:MovementRead(second,"FrozenOwners") as IEnumerable<TrafficIntentKey>;
                            if(decision!=null && (decision.Kind==TrafficDecisionKind.WaitingForTraffic || decision.Kind==TrafficDecisionKind.DeferredByBudget) &&
                                frozen!=null && frozen.Any(k=>k.ActorId==seen[2] && k.IntentId=="pickup-paid-movement"))
                            { replayExitArrivalWaitObserved=true;trace.Record("CONTROLLED_EXIT_WAIT_AT_THIRD_ARRIVAL second="+seen[1]+" third="+seen[2]); }
                        }
                        if(replay=="paid-gate" && !replayCollectionReleased && seen.Count>=3 &&
                            employees.TryCaptureOrderCheckpoint(EmployeeRole.Cashier,out var replayCashier) &&
                            replayCashier.VisitId==seen[2] && replayCashier.Stage==CafeWorkStage.Working &&
                            employees.Snapshot.Any(e=>e.Role==EmployeeRole.Cashier && e.State==EmployeeState.Working && e.RemainingWorkSeconds>0) &&
                            employees.Snapshot.Any(e=>e.Role==EmployeeRole.Barista && e.State==EmployeeState.Returning) &&
                            owner.Snapshot.Orders.Values.Any(o=>o.CustomerId==seen[0] && o.State==OrderState.ReadyForPickup) &&
                            customers.PickupAssignments.Any(a=>a.VisitId==seen[0] && a.Arrived))
                        { tools.ResumeCollection();replayCollectionReleased=true;trace.Record("CONTROLLED_COLLECTION_RELEASE third="+seen[2]); }
                        trace.Sample();
                        if(firstBlocked.Length==0 && (employees.Snapshot.Any(e=>e.State==EmployeeState.Blocked) || customers.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked)))
                        {
                            firstBlocked="sceneSeconds="+(Time.time-sceneStart).ToString("F3",CultureInfo.InvariantCulture)+" "+trace.State();
                            finishDeliveryWitness?.Invoke(); // Original Core failure hooks already fired; freeze their first evidence.
                            trace.Milestone("FIRST_BLOCKED "+firstBlocked);
                            if(storedWait)TestContext.WriteLine("MAIN_ORIGINAL_WAIT "+typeof(ServiceTrafficCoordinator).GetField("WaitFailureDiagnostic",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(storedWaitCore));
                            if(storedWait)TestContext.WriteLine("MAIN_ORIGINAL_WAIT_DROPPED "+typeof(ServiceTrafficCoordinator).GetField("WaitFailureDiagnosticDropped",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(storedWaitCore));
                        }
                        if(firstBlocked.Length!=0)break; // Confirmed original assertion failure; keep positive window and all assertions unchanged.
                        if(firstBlocked.Length==0 && !boundaryWitnessOnly)deliveryWitness?.Sample(); // Do not follow a retired failure source through the remaining window.
                        // Stop only after genuine success; all admissions before this use the normal clock.
                        if(!stoppedArrivals && exits.Count>=3)
                        {
                            customers.AutoSpawn=false;stoppedArrivals=true;drainSceneStart=Time.time;drainWallStart=timer.Elapsed.TotalSeconds;
                            // Current Main normally completes about one guest per 14.6s; this is a bounded test allowance.
                            // 按停接待时真实余下人数只记一次：每位20秒＋末员工返程10秒，之后不续期。
                            var remaining=seen.Count-exits.Count;
                            Assert.That(remaining,Is.InRange(0,customers.Capacity.Limits.TotalCustomers),"FIXTURE: capture only the actual admitted remaining cohort");
                            drainSeconds=20d*remaining+10d;
                            trace.Record("SUCCESS_COHORT_STOP_ARRIVALS remaining="+remaining+" totalLimit="+customers.Capacity.Limits.TotalCustomers+" drainSeconds="+drainSeconds.ToString("F0",CultureInfo.InvariantCulture));
                        }
                        drained=stoppedArrivals && seen.All(exits.ContainsKey) && customers.Snapshot.Count==0 && customers.Capacity.GetCapacities().All(c=>c.Used==0) &&
                            employees.Snapshot.All(e=>e.State==EmployeeState.Idle);
                        trace.CostPhase(stoppedArrivals?(drained?2:1):0);
                        if(drained && !trace.FixedCostWindow)break;
                        var before=timer.Elapsed.TotalMilliseconds;
                        yield return null;
                        trace.Frame(timer.Elapsed.TotalMilliseconds-before);
                    }
                    trace.CostBoundaryReached(Time.time-sceneStart>=120f || timer.Elapsed.TotalSeconds>=120d);
                    var final="FINAL sceneSeconds="+(Time.time-sceneStart).ToString("F3",CultureInfo.InvariantCulture)+" wallSeconds="+
                        timer.Elapsed.TotalSeconds.ToString("F3",CultureInfo.InvariantCulture)+" firstBlocked="+firstBlocked+" seen="+seen.Count+" exits="+exits.Count+" drained="+drained+
                        " missingExitIds=["+string.Join(";",seen.Where(id=>!exits.ContainsKey(id)))+"] completedIds=["+
                        string.Join(";",owner.Snapshot.Orders.Values.Where(o=>o.State==OrderState.Completed).Select(o=>o.CustomerId))+"] "+trace.State();
                    var memoHits=typeof(ServiceTrafficCoordinator).GetProperty("NativeChunkHits",BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(storedWaitCore);
                    final+=" nativeChunkHits="+(memoHits?.ToString()??"UNKNOWN");
                    trace.Milestone(final);TestContext.WriteLine(final);
                    if(!drained || seen.Count<3)
                    {
                        TestContext.WriteLine(MainAdmissionFailureStored());
                        TestContext.WriteLine("MAIN_VISIT_STORED "+TwentyTrafficFailureTrace.CounterGapCurrent(world,customers,storedOnly:true));
                    }
                }
                finally
                {
                    if(replayEnabled)
                    {
                        CafeLoopRuntimeProof.Set(customers,"clock",originalClock);
                        if(originalCollection)tools.HoldCollection();else tools.ResumeCollection();
                    }
                    unpaidBudget?.Dispose();
                    finishDeliveryWitness?.Invoke();
                    // Close cost sampling before assertions/teardown; the last partial frame remains in the table.
                    trace.EndCostWindow();r19CostCleanup=null;
                    // Remove callbacks before scene teardown; destruction during cleanup is never an Exit.
                    tracking=false;owner.SnapshotChanged-=WatchVisits;customers.AutoSpawn=false;
                    foreach(var observer in observers)if(observer!=null)observer.Receipt=null;
                }
                if(replayEnabled)Assert.That(replayCollectionReleased,Is.True,"FIXTURE: controlled collection predicate must actually occur; otherwise NOT_PROVEN");
                Assert.That(trace.ObserverErrors,Is.Empty,"OBSERVER: callbacks must not alter or interrupt original game decisions");
                Assert.That(trace.Dropped,Is.Zero,"OBSERVER: bounded trace overflow is an evidence gap");
                Assert.That(seen.Count,Is.GreaterThanOrEqualTo(3),"FIXTURE: natural arrivals must establish first and subsequent real visits");
                Assert.That(receiptErrors,Is.Empty,"EXIT_RECEIPT: "+string.Join(";",receiptErrors));
                Assert.That(firstBlocked,Is.Empty,"PRODUCT: normal Main service must not enter Blocked; "+firstBlocked);
                if(replay=="exit-gate" || replay=="exit-after-head")Assert.That(replayExitArrivalWaitObserved,Is.True,"FIXTURE: actual Exit2 must still wait across real Pickup3 arrival; otherwise NOT_PROVEN");
                Assert.That(exits.ContainsKey(seen[0]),Is.True,"PRODUCT: the original first customer's real Exit is required, not merely Completed");
                Assert.That(exits.Count,Is.GreaterThanOrEqualTo(3),"PRODUCT: require real first and subsequent Exits within the bounded arrivals/drain windows");
                Assert.That(seen.All(exits.ContainsKey),Is.True,"PRODUCT: every naturally admitted body needs its own real Exit receipt");
                Assert.That(drained,Is.True,"PRODUCT: original admitted bodies and original capacity tokens must really drain");
                Assert.That(tokens.Keys.All(id=>customers.Capacity.GetReservations().Single(r=>ReferenceEquals(r.Token,tokens[id])).State==ReservationState.Released),Is.True);
                Assert.That(owner.Snapshot.Orders.Count,Is.EqualTo(seen.Count));
                Assert.That(owner.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed),Is.True);
            }
        }

        [UnityTest]
        public IEnumerator MainSelectedEmployeeJourneyRejectsProjectedLaterParkingReservation()
        {
            yield return TrafficOriginalMainLayout();
            tools.HoldArrivals();tools.HoldCollection();yield return null;
            // Controlled side obstruction, not the original natural Main fixture.
            // 真实静止身体挡住South服务侧，由生产planner自行选择其它实际往返路线。
            var blockedSide=new Vector3(-.5f,0,.5f);
            var pickupId=Find<CafeLayoutRuntime>().FunctionalSurfaceLayout.PickUpPoints.Single().InstanceId;
            TestContext.WriteLine("CONTROLLED_SIDE_BLOCKED notOriginalNaturalMain=True side="+blockedSide.ToString("F9")+
                " actualSides=["+string.Join(";",customers.GetPickupServiceTargets(pickupId).Select(t=>t.Position.ToString("F9")))+"]");
            Assert.That(customers.GetPickupServiceTargets(pickupId).Any(t=>Vector3.Distance(t.Position,blockedSide)<.001f),Is.True,
                "FIXTURE: the controlled blocker must occupy the actual South service side");
            var sideBlocker=Blocker(blockedSide,"projected-parking-south-side-blocker");
            try
            {
            var bound=Stopwatch.StartNew();var sceneStart=Time.time;
            bool WithinBound()=>bound.Elapsed.TotalSeconds<60 && Time.time-sceneStart<60;
            yield return AwaitTrafficAdmission("FIXTURE: controlled first visit must be formally admitted");
            var first=customers.Snapshot.Single().VisitId;
            while(WithinBound() && !employees.Snapshot.Any(e=>e.Role==EmployeeRole.Barista && e.State==EmployeeState.Working && e.RemainingWorkSeconds>0 && e.RemainingWorkSeconds<=.25f))yield return null;
            Assert.That(employees.Snapshot.Any(e=>e.Role==EmployeeRole.Barista && e.State==EmployeeState.Working && e.RemainingWorkSeconds>0 && e.RemainingWorkSeconds<=.25f),Is.True,"FIXTURE: real first preparation must reach its near-completion timing boundary");
            yield return AwaitTrafficAdmission("FIXTURE: controlled second admission must use the real API");
            var second=customers.Snapshot.Single(v=>v.VisitId!=first).VisitId;
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            object Read(object instance,string name)=>instance?.GetType().GetField(name,flags)?.GetValue(instance);
            object BaristaRow()=>((IEnumerable)Read(employees,"employees")).Cast<object>().Single(row=>(EmployeeRole)Read(row,"Role")==EmployeeRole.Barista);
            EmployeeTaskRunner Runner()=>Read(BaristaRow(),"Runner") as EmployeeTaskRunner;
            while(WithinBound() && (Read(Runner(),"deliveryRoute")==null || Read(Runner(),"returnRoute")==null))yield return null;
            var runner=Runner();var staff=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);
            Assert.That(runner,Is.Not.Null);
            Assert.That(Read(runner,"deliveryRoute"),Is.Not.Null,"FIXTURE: first real selected outward journey required");
            Assert.That(Read(runner,"returnRoute"),Is.Not.Null,"FIXTURE: first real selected return journey required");
            Assert.That(customers.Snapshot.Single(v=>v.VisitId==second).State,Is.EqualTo(CustomerVisitState.Entering),"FIXTURE: second must still be walking when the first employee selects its real journey");
            var outward=((NavigationTarget[])Read(runner,"deliveryRoute")).ToArray();
            var home=((NavigationTarget[])Read(runner,"returnRoute")).ToArray();
            var handle=staff.HandleId;var revision=adapter.Revision;var taskToken=Read(runner,"taskToken");
            var actor=world.RegisteredActors.Single(a=>a.ActorId==staff.ActorId);
            var clearance=actor.Settings.AgentRadius+world.RegisteredActors.Max(a=>a.Settings.AgentRadius)+actor.Settings.CollisionSkin+actor.Settings.Epsilon;
            // One fixture expansion of the actual stored routes; this controlled test is separate from the no-requery baseline.
            // 仅展开一次已选Native路线，不注入route／request／pose／Arrived或付款事实。
            List<Vector3> Expand(Vector3 from,IEnumerable<NavigationTarget> targets)
            {
                var result=new List<Vector3>{from};
                foreach(var target in targets)
                {var leg=adapter.GetCompletePath(result.Last(),target.Position);Assert.That(leg,Is.Not.Null,"FIXTURE: selected real route must expand through the owned Native adapter");result.AddRange(leg.Skip(1));}
                return result;
            }
            var selectedOutward=Expand(actor.transform.position,outward.Skip((int)Read(runner,"deliveryRouteIndex")));
            var selectedHome=Expand(outward.Last().Position,home);
            var queue=(PickupQueueService)Read(customers,"pickupQueue");
            var slots=(Vector3[])Read(queue,"waitSlots");
            var retained=(Vector3[])Read(customers,"pickupWaitSlots");
            var originalSlots=slots.ToArray();var originalRetained=retained.ToArray();
            var samples=new List<Vector3>();
            for(var i=1;i<selectedHome.Count;i++)
                for(var j=1;j<=8;j++) samples.Add(Vector3.Lerp(selectedHome[i-1],selectedHome[i],j/8f));
            var projected=false;var projectedTarget=default(Vector3);var projectedSlot=-1;
            string Facts()
            {
                var reservations=(IDictionary)Read(queue,"reservations");
                return "queueGeneration="+Read(queue,"generation")+" reservations=["+string.Join(";",reservations.Keys.Cast<object>().OrderBy(k=>k.ToString(),StringComparer.Ordinal).Select(k=>{
                    var value=reservations[k];return k+"/"+Read(value,"Slot")+"/"+Read(value,"Generation")+"/"+Read(value,"Target")+"/"+Read(value,"Paid");}))+
                    "] tokens=["+string.Join(";",customers.Capacity.GetReservations().Select(r=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(r.Token)+"/"+r.OwnerId+"/"+r.Kind+"/"+r.State))+
                    "] orders=["+string.Join(";",owner.Snapshot.Orders.Select(p=>p.Key+"/"+p.Value.State))+"] paid=["+string.Join(";",owner.Snapshot.PaidVisitIds)+"]";
            }
            var candidate=false;var validAtDecision=false;var decided=false;var accepted=false;
            var ownerActuallyReturned=false;var lateUnsafePublication=false;
            bool OwnerJourneyLive()
            {
                var current=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);
                return ReferenceEquals(Runner(),runner) && current.HandleId==handle && Equals(Read(runner,"taskToken"),taskToken) &&
                    (((NavigationTarget[])Read(runner,"returnRoute"))?.SequenceEqual(home)??false);
            }
            var before="";var after="";var decision="";var errors=new List<string>();var witness=new List<string>();
            witness.Add("CONTROLLED_PROJECTED_NEXT_SLOT notOriginalNaturalReplay=True notNaturalPlannerSlot=True controlledSideBlocked=True blocker="+sideBlocker.ActorId+
                " blockerPose="+sideBlocker.transform.position.ToString("F9")+" blockerRadius="+sideBlocker.Settings.AgentRadius+
                " first="+first+" second="+second+" handle="+handle+" revision="+revision+" clearance="+clearance+
                " taskToken="+taskToken+" outward=["+string.Join(";",selectedOutward.Select(p=>p.ToString("F9")))+"] return=["+string.Join(";",selectedHome.Select(p=>p.ToString("F9")))+"] originalSlots=["+string.Join(";",slots.Select(p=>p.ToString("F9")))+"]");
            void Safe(Action action){try{action();}catch(Exception error){errors.Add(error.ToString());}}
            var corridorHook=typeof(CustomerFlowController).GetField("CorridorObserver",flags);
            var pickupHook=typeof(CustomerFlowController).GetField("PickupObserver",flags);
            var priorCorridor=corridorHook.GetValue(customers);var priorPickup=pickupHook.GetValue(customers);
            Assert.That(priorCorridor,Is.Null);Assert.That(priorPickup,Is.Null);
            try
            {
                corridorHook.SetValue(customers,(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)((op,id,related,target,path,obstacles,result)=>Safe(()=>{
                    if(!decided && candidate && (id==second || related==second))
                        witness.Add("DECISION_GUARD op="+op+" id="+id+" related="+related+" result="+result+
                            " target="+target.ToString("F9")+" path=["+string.Join(";",(path??Array.Empty<Vector3>()).Select(p=>p.ToString("F9")))+"]");
                    if(id!=second || decided)return;
                    if(op=="reserve.attempt" && !projected)
                    {
                        var entry=((IDictionary)Read(customers,"visits"))[second];
                        if((CustomerVisitState)Read(entry,"State")!=CustomerVisitState.Queued || !(bool)Read(entry,"CounterArrived") ||
                            (bool)Read(entry,"PickupAssigned") || !customers.CounterAssignments.Any(a=>a.VisitId==second && a.SlotIndex==0 && a.Arrived))return;
                        projectedSlot=((IDictionary)Read(queue,"reservations")).Count;
                        // Independent geometry only: never choose a point by asking the parking policy under test.
                        // 只投影尚未分配的下一候位；不修改任何已有reservation、身体、request或付款。
                        var occupied=world.RegisteredActors.Select(a=>a.transform.position).Concat(queue.ServiceLeases.Values)
                            .Concat(slots.Take(projectedSlot)).ToArray();
                        var safe=samples.Where(p=>occupied.All(pose=>Vector3.Distance(pose,p)>Mathf.Max(1.05f,clearance)) && adapter.IsPointWalkable(p)).ToArray();
                        witness.Add("PROJECTION_PRECONDITION frame="+Time.frameCount+" sameQueue="+ReferenceEquals(Read(customers,"pickupQueue"),queue)+
                            " nextSlot="+projectedSlot+" queueSlots="+slots.Length+" retainedSlots="+retained.Length+" canReserve="+queue.CanReserve+
                            " sampled="+samples.Count+" safe="+safe.Length+" occupied=["+string.Join(";",occupied.Select(p=>p.ToString("F9")))+"]");
                        if(safe.Length==0 || projectedSlot>=slots.Length || projectedSlot>=retained.Length ||
                            !ReferenceEquals(Read(customers,"pickupQueue"),queue))return;
                        projectedTarget=safe.OrderByDescending(p=>occupied.Min(pose=>Vector3.Distance(pose,p))).First();
                        slots[projectedSlot]=projectedTarget;retained[projectedSlot]=projectedTarget;projected=true;
                    }
                    if(op=="reserve.candidate")
                    {
                        candidate=true;
                        var current=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);
                        var entry=((IDictionary)Read(customers,"visits"))[second];
                        var sameReturn=(((NavigationTarget[])Read(runner,"returnRoute"))?.SequenceEqual(home)??false);
                        var walkable=adapter.IsPointWalkable(target);
                        var bodyClear=world.RegisteredActors.All(a=>Vector3.Distance(a.transform.position,target)>Mathf.Max(1.05f,clearance));
                        var leaseClear=queue.ServiceLeases.Values.All(pose=>Vector3.Distance(pose,target)>Mathf.Max(1.05f,clearance));
                        validAtDecision=ReferenceEquals(Runner(),runner) && current.HandleId==handle && adapter.Revision==revision && Equals(Read(runner,"taskToken"),taskToken) &&
                            (current.State==EmployeeState.CarryingToPickUp || current.State==EmployeeState.WaitingToDeliver) &&
                            sameReturn &&
                            (int)Read(runner,"returnRouteIndex")==0 && !(bool)Read(entry,"PickupAssigned") &&
                            (bool)Read(entry,"CounterArrived") && (CustomerVisitState)Read(entry,"State")==CustomerVisitState.Queued &&
                            customers.CounterAssignments.Any(a=>a.VisitId==second && a.SlotIndex==0 && a.Arrived) &&
                            !owner.Snapshot.PaidVisitIds.Contains(second) && projected && ReferenceEquals(Read(customers,"pickupQueue"),queue) &&
                            Vector3.Distance(target,projectedTarget)<.001f && walkable && bodyClear && leaseClear &&
                            !CustomerRoutePlanner.IsClear(selectedHome,new[]{target},clearance);
                        before=Facts();
                        witness.Add("ORIGINAL_RESERVE_CANDIDATE frame="+Time.frameCount+" validSelectedReturnConflict="+validAtDecision+" projected="+projected+
                            " nextSlot="+projectedSlot+" current="+current.State+" handle="+current.HandleId+" sameRunner="+ReferenceEquals(Runner(),runner)+
                            " revision="+adapter.Revision+" sameTask="+Equals(Read(runner,"taskToken"),taskToken)+" sameReturn="+sameReturn+" returnIndex="+Read(runner,"returnRouteIndex")+
                            " incomingState="+Read(entry,"State")+" counterArrived="+Read(entry,"CounterArrived")+" pickupAssigned="+Read(entry,"PickupAssigned")+
                            " actualFront="+customers.CounterAssignments.Any(a=>a.VisitId==second && a.SlotIndex==0 && a.Arrived)+
                            " paid="+owner.Snapshot.PaidVisitIds.Contains(second)+" target="+target.ToString("F9")+
                            " projectedTarget="+projectedTarget.ToString("F9")+" walkable="+walkable+" bodyClear="+bodyClear+" leaseClear="+leaseClear+
                            " homeConflict="+!CustomerRoutePlanner.IsClear(selectedHome,new[]{target},clearance)+" "+before);
                    }
                    else if(candidate && op=="reserve.result" && result!="existing-accepted")
                    {decided=true;decision=result;after=Facts();witness.Add("ORIGINAL_RESERVE_RESULT "+result+" "+after);}
                })));
                pickupHook.SetValue(customers,(Action<string,string,PickupQueueService,QueueAssignment>)((op,id,currentQueue,assignment)=>Safe(()=>{
                    if(id!=second || !candidate)return;
                    if(decided)
                    {
                        if(op=="reserve.accepted" && OwnerJourneyLive())
                        {accepted=true;lateUnsafePublication=true;witness.Add("UNSAFE_LATER_PUBLICATION whileOriginalJourneyLive=True "+Facts());}
                        return;
                    }
                    if(op=="reserve.accepted" || op=="reserve.deferred-service")
                    {decided=true;accepted=op=="reserve.accepted";decision=op;after=Facts();witness.Add("ORIGINAL_PICKUP_DECISION "+op+" target="+assignment.Position.ToString("F9")+" "+after);}
                })));
                while(WithinBound() && errors.Count==0)
                {
                    if(decided && accepted)break;
                    if(decided && !OwnerJourneyLive())
                    {
                        var current=employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista);
                        ownerActuallyReturned=current.State==EmployeeState.Idle && Vector3.Distance(actor.transform.position,home.Last().Position)<=.081f;
                        witness.Add("OWNER_FINAL state="+current.State+" actualPose="+actor.transform.position.ToString("F9")+" actualReturned="+ownerActuallyReturned);break;
                    }
                    yield return null;
                }
            }
            finally
            {
                Array.Copy(originalSlots,slots,slots.Length);Array.Copy(originalRetained,retained,retained.Length);
                pickupHook.SetValue(customers,priorPickup);corridorHook.SetValue(customers,priorCorridor);
                var report="CONTROLLED_FINAL candidate="+candidate+" validAtDecision="+validAtDecision+" decided="+decided+" accepted="+accepted+" decision="+decision+
                    " ownerActuallyReturned="+ownerActuallyReturned+" lateUnsafePublication="+lateUnsafePublication+" errors="+string.Join(";",errors);
                witness.Add(report);TestContext.WriteLine(string.Join("\n",witness));
                var directory=Path.Combine("outputs","phase15","service-traffic-coordination-20261030","controlled-parking",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                Directory.CreateDirectory(directory);File.WriteAllLines(Path.Combine(directory,"original-entry-witness.txt"),witness);
            }
            Assert.That(errors,Is.Empty,"OBSERVER: controlled decision witness must not interrupt business flow");
            Assert.That(candidate && decided,Is.True,"FIXTURE: real second front must reach the original new-reservation decision; "+decision);
            Assert.That(validAtDecision,Is.True,"FIXTURE: original entry requires the same still-valid selected first employee return and unpaid real front");
            Assert.That(accepted,Is.False,"PRODUCT: later parking reservation must not cover the already selected employee return journey");
            Assert.That(after,Is.EqualTo(before),"PRODUCT: refused parking must preserve canonical reservation, original capacity, order and payment facts");
            Assert.That(ownerActuallyReturned,Is.True,"PRODUCT: the protected employee must actually return while the later parking stays uncommitted; an initial Deferred alone is insufficient");
            }
            finally
            {
                if(sideBlocker!=null)
                { world.Unregister(sideBlocker);sideBlocker.gameObject.SetActive(false);UnityEngine.Object.Destroy(sideBlocker.gameObject); }
            }
        }

        // Cold failure facts only: never call policy/currentness/path APIs or sample during normal frames.
        // 仅测试结束失败时读字段，不改变运行时序或重试原请求。
        private string MainAdmissionFailureStored()
        {
            const BindingFlags flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            object Raw(object value,string name)=>value==null?null:(value.GetType().GetField(name,flags)??value.GetType().GetField("<"+name+">k__BackingField",flags))?.GetValue(value);
            string Fields(object value,params string[] names)=>string.Join(" ",names.Select(name=>name+"="+Raw(value,name)));
            var anchor=Raw(customers,"pendingAdmissionAnchor");var core=Raw(customers,"traffic");var entries=Raw(core,"entries") as IDictionary;
            var key=Raw(anchor,"Key");var entry=key!=null && entries!=null && entries.Contains(key)?entries[key]:null;
            var request=Raw(entry,"Request") as TrafficRequest;var decision=Raw(entry,"Decision") as TrafficDecision;
            return "MAIN_ADMISSION_STORED "+Fields(customers,"nextVisit","autoSpawn","integratedAdmissionAllowed","integratedHeld","suspended","disposed","admissionRejected","admissionRouteUnavailable")+
                " clock="+Fields(Raw(customers,"clock"),"armed","RemainingSeconds")+" anchor="+Fields(anchor,"Submitted","Before","FirstFrame","Position","Session")+
                " key="+(key is TrafficIntentKey k?k.ActorId+"/"+k.IntentId+"/g"+k.Generation:"NONE")+
                " entry="+Fields(entry,"Queued","Prepared","Active","Retired","Discarded","Queries","FirstFrame","WaitRemaining","AdmissionHeld","ProducerPreviewHeld","AcquiredSequence")+
                " decision="+decision?.Kind+"/"+decision?.Reason+"/"+decision?.FailureDisposition+
                " blockers=["+string.Join(";",(decision?.Blockers??Array.Empty<TrafficBlocker>()).Select(block=>block.Owner.ActorId+"/"+block.Owner.IntentId+"/g"+block.Owner.Generation+"/"+block.ResourceId+"/"+block.Proof))+"]"+
                " request="+request?.Start.ToString("F9")+"->"+request?.Target.ToString("F9")+" requestWorld="+request?.Snapshot.Version+"/"+request?.Snapshot.BodyVersion+
                " currentWorld="+Fields(Raw(core,"snapshot"),"Version","BodyVersion")+" core="+Fields(core,"closed","paused")+
                " storedWait="+Raw(core,"WaitFailureDiagnostic");
        }

        private IEnumerator TrafficOriginalMainLayout()
        {
            // Use the existing fixture's cleanup conventions, but do not use MatrixStart's moved starter.
            Time.captureDeltaTime=0; // Original Main uses real 1x time; teardown restores the prior value.
            var old=scene;var oldInputs=inputs;
            SceneManager.SetActiveScene(SceneManager.CreateScene("service traffic Main switch"));
            yield return SceneManager.UnloadSceneAsync(old);Phase8SceneInputTestCleanup.DisposeReleasedAssets(oldInputs);
            scene=EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",new LoadSceneParameters(LoadSceneMode.Single));yield return null;
            owner=Find<CafeLoopController>();customers=Find<CustomerFlowController>();employees=Find<EmployeeFlowController>();
            tools=Find<CafeLoopValidationController>();world=Find<NavigationWorld>();adapter=Find<NavigationLayoutAdapter>();time=Find<GameTimeService>();
            inputs=Phase8SceneInputTestCleanup.CaptureAssets(scene);deliveryStarts.Clear();
            Assert.That(CafeLoopRuntimeProof.Field<bool>(tools,"seedValidationLayout"),Is.False,"FIXTURE: ordinary Main never seeds a validation layout");
            Assert.That(world.RegisteredActors,Is.Empty,"FIXTURE: incomplete original Main must have no NPCs");
            var runtime=Find<CafeLayoutRuntime>();var decor=Find<DecorationModeController>();
            var initial=runtime.Layout.FurnitureInstances.Single();
            Assert.That(initial.Position,Is.EqualTo(new GridPosition(2,3)),"FIXTURE: do not move the original starter counter");
            Assert.That(initial.Rotation,Is.EqualTo(FurnitureRotation.Degrees0));
            Assert.That(runtime.FunctionalSurfaceLayout.MountedInstances,Is.Empty);
            Assert.That(runtime.FunctionalSurfaceLayout.PickUpPoints,Is.Empty);
            decor.EnterDecorationMode();yield return null;
            var catalog=CafeLoopRuntimeProof.Field<FurnitureContentCatalog>(decor,"contentCatalog");
            var starterSlot=catalog.BuildSurfaceSlotCatalog(runtime.Layout.GridSettings).GetForSupport(initial.DefinitionId).Single().SlotId;
            Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.CashRegister,"equipment.cash-register.01",new SurfaceSlotAddress(initial.InstanceId,starterSlot)),Is.True);
            Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);yield return null;
            yield return TrafficOriginalCounter();
            var counter=runtime.Layout.FurnitureInstances.Single(f=>f.DefinitionId=="counter.preset.1x3");
            Assert.That(counter.Position,Is.EqualTo(new GridPosition(3,5)),"FIXTURE: final rotated counter origin must match the original layout");
            Assert.That(counter.Rotation,Is.EqualTo(FurnitureRotation.Degrees90));
            var slots=catalog.BuildSurfaceSlotCatalog(runtime.Layout.GridSettings).GetForSupport(counter.DefinitionId).OrderBy(s=>s.LocalCell.Y).ToArray();
            Assert.That(slots.Select(s=>s.SlotId),Is.EqualTo(new[]{"slot.0","slot.1","slot.2"}),"FIXTURE: actual counter surface model determines left and middle slots");
            Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.CoffeeMachine,"equipment.coffee-machine.01",new SurfaceSlotAddress(counter.InstanceId,slots[1].SlotId)),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True); // support90 + mounted90 = actual180
            Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);yield return null;
            Assert.That(decor.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint,"pickup.point",new SurfaceSlotAddress(counter.InstanceId,slots[0].SlotId)),Is.True);
            Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);yield return null;
            var furniture=Find<FurnitureSceneRegistry>();
            Assert.That(furniture.TryGet(counter.InstanceId,out var counterRoot),Is.True);
            TestContext.WriteLine("ORIGINAL_COUNTER_READBACK origin="+counter.Position+" rotation="+counter.Rotation+" local="+counterRoot.transform.localPosition.ToString("F9")+" localEuler="+counterRoot.transform.localEulerAngles.ToString("F9"));
            Assert.That(Vector3.Distance(counterRoot.transform.localPosition,new Vector3(4.5f,0,5.5f)),Is.LessThan(.0001f),"FIXTURE: screenshot counter root local position; actual="+counterRoot.transform.localPosition.ToString("F9"));
            Assert.That(Quaternion.Angle(counterRoot.transform.localRotation,Quaternion.Euler(0,90,0)),Is.LessThan(.01f),"FIXTURE: screenshot counter root local rotation");
            var machine=runtime.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
            var cash=runtime.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.DefinitionId=="equipment.cash-register.01");
            Assert.That(cash.Address.SupportFurnitureInstanceId,Is.EqualTo(initial.InstanceId));
            Assert.That(cash.Rotation,Is.EqualTo(FurnitureRotation.Degrees0));
            Assert.That(machine.Address,Is.EqualTo(new SurfaceSlotAddress(counter.InstanceId,"slot.1")));
            Assert.That(machine.Rotation,Is.EqualTo(FurnitureRotation.Degrees90));
            Assert.That(Find<SurfaceMountedSceneRegistry>().TryGet(machine.InstanceId,out var machineRoot),Is.True);
            TestContext.WriteLine("ORIGINAL_MACHINE_READBACK local="+machineRoot.transform.localPosition.ToString("F9")+" localEuler="+machineRoot.transform.localEulerAngles.ToString("F9"));
            Assert.That(Vector3.Distance(machineRoot.transform.localPosition,new Vector3(4.5f,.42f,5.5f)),Is.LessThan(.0001f),"FIXTURE: screenshot CM root local position; actual="+machineRoot.transform.localPosition.ToString("F9"));
            Assert.That(Quaternion.Angle(machineRoot.transform.localRotation,Quaternion.Euler(0,180,0)),Is.LessThan(.01f),"FIXTURE: screenshot CM root local rotation");
            Assert.That(runtime.FunctionalSurfaceLayout.PickUpPoints.Single().Address,Is.EqualTo(new SurfaceSlotAddress(counter.InstanceId,"slot.0")));
            Assert.That(runtime.Layout.FurnitureInstances.Single(f=>f.InstanceId==initial.InstanceId).Position,Is.EqualTo(new GridPosition(2,3)));
            Assert.That(runtime.Layout.FurnitureInstances.Count,Is.EqualTo(2));
            runtime.RecalculateReadiness();
            Assert.That(runtime.CurrentReadiness.CanOpenForBusiness,Is.True,"FIXTURE: original formal combination must be Ready");
            // Prevent no arrivals: no HoldArrivals/clock replacement; the normal controller defaults stay enabled.
            Assert.That(customers.AutoSpawn,Is.True,"FIXTURE: preserve Main's default automatic arrivals");
            Assert.That(owner.HoldCollection,Is.False,"FIXTURE: preserve ordinary automatic collection");
            decor.ExitDecorationMode();
            for(var frame=0;frame<300 && !(owner.CanProgress && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));frame++)yield return null;
            Assert.That(owner.CanProgress,Is.True,"FIXTURE: formal Main Done must permit normal business");
            Assert.That(employees.Snapshot.Count,Is.EqualTo(2),"FIXTURE: normal staff must really initialize");
            Assert.That(employees.Snapshot.All(e=>e.State==EmployeeState.Idle),Is.True);
            Assert.That(customers.Snapshot,Is.Empty,"FIXTURE: observers must precede the first natural admission");
            Assert.That(Vector3.Distance(CafeLoopRuntimeProof.Field<Vector3>(customers,"pickupCustomer"),new Vector3(-.5f,0,.5f)),Is.LessThan(.0001f),"FIXTURE: original observed Pickup customer geometry");
            Assert.That(Vector3.Distance(CafeLoopRuntimeProof.Field<Vector3>(customers,"pickupEmployee"),new Vector3(-.5f,0,2.5f)),Is.LessThan(.0001f),"FIXTURE: original observed Pickup employee geometry");
            Assert.That(Vector3.Distance(CafeLoopRuntimeProof.Field<Vector3>(customers,"pickupOutward"),new Vector3(0,0,-1)),Is.LessThan(.0001f),"FIXTURE: original observed outward");
            TestContext.WriteLine("ORIGINAL_MAIN_LAYOUT starter=(2,3)/0 CR=starter/0 counter=(3,5)/90 rootLocal=(4.5,0,5.5) CM=slot.1/90 actualLocal=(4.5,.42,5.5)/180 pickup=slot.0 customer=(-.5,0,.5) employee=(-.5,0,2.5) outward=(0,0,-1) seedValidationLayout=False");
        }

        private IEnumerator TrafficOriginalCounter()
        {
            var decor=Find<DecorationModeController>();
            var asset=AssetDatabase.FindAssets("t:FurnitureDefinitionAsset")
                .Select(g=>AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .Single(a=>a.DefinitionId=="counter.preset.1x3");
            Assert.That(decor.TrySelectCatalogueItem(new DecorationCatalogueItemModel(asset.DefinitionId,asset.DisplayName,null,DecorationCatalogueItemKind.Furniture,false,asset)),Is.True);
            var session=CafeLoopRuntimeProof.Field<DecorationSession>(decor,"session");
            // Rotation preserves preview centre and can move its origin / 先旋转，再设置最终grid origin。
            Assert.That(session.RotatePreview().Succeeded,Is.True,"FIXTURE: formal counter quarter-turn must succeed");
            Assert.That(session.MovePreview(new GridPosition(3,5)).Succeeded,Is.True,"FIXTURE: final rotated counter placement must be legal");
            yield return MatrixConfirmFurniture();
        }

        // Original observer values and stored fields only. No path planner, Observe, DesiredVelocity or NavMesh requery.
        // Default-off copies of original preparation decisions; never replay guards or Native queries.
        private sealed class UnpaidBudgetWitness:IDisposable
        {
            private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            private readonly CustomerFlowController flow;private readonly NavigationWorld world;private readonly object core;private readonly FieldInfo hook;
            private readonly Delegate prior;private readonly Action<TrafficIntentKey,string,TrafficRequest,TrafficRequest> installed;
            private readonly Dictionary<TrafficIntentKey,Dictionary<string,int>> counts=new Dictionary<TrafficIntentKey,Dictionary<string,int>>();
            private readonly Dictionary<TrafficIntentKey,int> buckets=new Dictionary<TrafficIntentKey,int>();
            private readonly List<string> records=new List<string>();private string terminal;private readonly List<string> errors=new List<string>();
            private static object Raw(object value,string name)=>value==null?null:(value.GetType().GetField(name,Flags)??value.GetType().GetField("<"+name+">k__BackingField",Flags))?.GetValue(value);
            private static string Ref(object value)=>value==null?"NULL":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            private static string Fields(object value,params string[] names)=>string.Join(" ",names.Select(n=>n+"="+Raw(value,n)));
            internal UnpaidBudgetWitness(CustomerFlowController flow,NavigationWorld world)
            {
                this.flow=flow;this.world=world;core=Raw(flow,"traffic");hook=core.GetType().GetField("UnpaidPreparationObserver",Flags);
                prior=hook.GetValue(core) as Delegate;installed=Observe;hook.SetValue(core,Delegate.Combine(prior,installed));
            }
            private void Observe(TrafficIntentKey key,string stage,TrafficRequest request,TrafficRequest previous)
            {
                try
                {
                    if(!counts.TryGetValue(key,out var row))
                    {if(counts.Count>=32)return;row=new Dictionary<string,int>();counts.Add(key,row);}
                    row[stage]=row.TryGetValue(stage,out var count)?count+1:1;
                    var entries=Raw(core,"entries") as IDictionary;if(entries==null || !entries.Contains(key))return;var entry=entries[key];
                    var queries=(int)Raw(entry,"Queries");var bucket=queries/1024;
                    if(!buckets.TryGetValue(key,out var old) || bucket!=old || stage!="no-bound-native")
                    {
                        buckets[key]=bucket;
                        var jobs=(IList)Raw(entry,"Jobs");var index=(int)Raw(entry,"JobIndex");var job=index>=0 && index<jobs.Count?jobs[index]:null;
                        var spatial=Raw(entry,"UnpaidEligibilitySpatialRefusal") as TrafficDecision;
                        var actors=(IEnumerable<string>)Raw(entry,"UnpaidEligibilitySpatialActors");
                        var line="frame="+Time.frameCount+" key="+key.ActorId+"/"+key.IntentId+"/g"+key.Generation+" stage="+stage+" entry="+Ref(entry)+" "+
                            Fields(entry,"FirstFrame","Queries","RequestSequence","AcquiredSequence","Queued","Prepared","Active","Retired","UnpaidEligibilityHeld","ProducerPreviewHeld","UnpaidEligibilityQueryFault","CandidateCount","CurrentIndex")+
                            " attempts=["+string.Join(";",(IEnumerable<int>)Raw(entry,"AttemptedIndices"))+"]"+
                            " job="+index+"/"+jobs.Count+" jobRef="+Ref(job)+" frontier="+Fields(job,"started","complete","current","next")+" spatial="+spatial?.Kind+"/"+spatial?.Reason+
                            " causalActors=["+string.Join(";",actors)+"] counts="+string.Join(";",row.Select(p=>p.Key+":"+p.Value))+
                            " roles=["+string.Join(";",request.Requirements.Select(r=>r.ResourceId+":"+r.Start.ToString("F6")+"->"+r.Goal.ToString("F6")+" obstacles="+r.Obstacles.Count))+"]";
                        line+=" jobs=["+string.Join(";",jobs.Cast<object>().Select((item,i)=>
                        {
                            var obstacles=Raw(item,"obstacles") as IEnumerable;var start=(Vector3)Raw(item,"start");var goal=(Vector3)Raw(item,"goal");
                            return i+":"+Ref(item)+" "+Fields(item,"started","complete","current","next")+" edges="+((IDictionary)Raw(item,"paths")).Count+
                                " start="+start.ToString("F6")+" goal="+goal.ToString("F6")+" obstacles=["+string.Join(";",obstacles.Cast<object>().Select(o=>
                                    Fields(o,"Position","Clearance")))+"]";
                        }))+"]";
                        var oldRoles=previous?.Requirements??Array.Empty<TrafficRouteRequirement>();
                        line+=" roleChanges=["+string.Join(";",request.Requirements.Select(role=>
                        {
                            var priorRole=oldRoles.FirstOrDefault(r=>r.ResourceId==role.ResourceId);
                            if(priorRole==null)return role.ResourceId+"/added";
                            var changes="";
                            if(priorRole.Start!=role.Start || priorRole.Goal!=role.Goal)changes+=" endpoints "+priorRole.Start.ToString("F6")+"->"+priorRole.Goal.ToString("F6");
                            if(!priorRole.Obstacles.SequenceEqual(role.Obstacles))changes+=" obstacles old=["+string.Join(";",priorRole.Obstacles.Take(6).Select(p=>p.ToString("F6")))+"] new=["+string.Join(";",role.Obstacles.Take(6).Select(p=>p.ToString("F6")))+"]";
                            return role.ResourceId+changes;
                        }))+ "] removed=["+string.Join(";",oldRoles.Where(r=>!request.Requirements.Any(n=>n.ResourceId==r.ResourceId)).Select(r=>r.ResourceId))+"]";
                        foreach(var body in request.Snapshot.Bodies.Where(b=>b.Owner.ActorId!=key.ActorId))
                        {
                            var moving=entries.Contains(body.Owner)?entries[body.Owner]:null;var leg=Raw(moving,"Leg");
                            line+=" BODY actor="+body.Owner.ActorId+" owner="+body.Owner.IntentId+"/g"+body.Owner.Generation+" life="+Ref(body.ActorLife)+" pose="+body.Position.ToString("F6")+
                                " enabled="+body.Enabled+" active="+body.ActorActive+" entry="+Ref(moving)+" "+Fields(moving,"Active","Prepared","Queued","Retired","Parking","AcquiredSequence")+
                                " permit="+Ref(Raw(moving,"JourneyPermit"))+" leg="+Ref(leg)+" "+Fields(leg,"Committed","Bound","Completed","Generation","Index","Suspended","ReprepareAllowed")+
                                " stamp="+Fields(Raw(leg,"Stamp"),"RequestId","LegGeneration")+" requestStamp="+Fields(Raw(Raw(moving,"Request"),"NativeStamp"),"RequestId","LegGeneration")+" decision="+Fields(Raw(moving,"Decision"),"Kind","Reason","FailureDisposition")+" legLife="+Ref(Raw(leg,"ActorLife"));
                        }
                        if(records.Count<512)records.Add(line);
                    }
                    if(stage=="query-budget-exhausted" && terminal==null)
                        terminal=TwentyTrafficFailureTrace.CounterGapCurrent(world,flow,storedOnly:true);
                }
                catch(Exception error){if(errors.Count<4)errors.Add(error.ToString());}
            }
            public void Dispose()
            {
                hook.SetValue(core,prior);
                foreach(var record in records)TestContext.WriteLine("UNPAID_PREPARATION "+record);
                foreach(var row in counts)TestContext.WriteLine("UNPAID_COUNTS "+row.Key.ActorId+"/g"+row.Key.Generation+" "+string.Join(";",row.Value.Select(p=>p.Key+":"+p.Value)));
                if(terminal!=null)TestContext.WriteLine("UNPAID_BUDGET_ORIGINAL_COLD "+terminal);
                TestContext.WriteLine("UNPAID_BUDGET_WITNESS records="+records.Count+" actors="+counts.Count+" errors="+string.Join(";",errors));
            }
        }

        private sealed class ServiceTrafficOriginalTrace : IDisposable
        {
            private const BindingFlags Fields=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
            private const int RecordLimit=200000;
            private readonly CustomerFlowController customers;
            private readonly EmployeeFlowController employees;
            private readonly NavigationWorld world;
            private readonly List<string> records=new List<string>();
            private readonly List<string> milestones=new List<string>();
            private readonly List<double> frameMs=new List<double>();
            private readonly List<Action> restore=new List<Action>();
            private readonly Dictionary<string,int> exitQueries=new Dictionary<string,int>();
            private readonly Dictionary<int,int> goalQueries=new Dictionary<int,int>();
            private readonly Dictionary<string,int> driverStages=new Dictionary<string,int>();
            private readonly Dictionary<string,string> lastCorridor=new Dictionary<string,string>();
            private readonly Dictionary<string,int> repeatedCorridor=new Dictionary<string,int>();
            private readonly bool summaryTrace=System.Environment.GetCommandLineArgs().Contains("-serviceTrafficSummaryTrace");
            private long summaryCorridorOmitted,summaryDriverOmitted;
            public bool FixedCostWindow { get; }=Environment.GetCommandLineArgs().Contains("-serviceTrafficNativeCount");
            // Fifteen Native outcome cells, ready calls/ticks, phase-at-last-loop, rounded frame microseconds.
            // 仅ON创建有界frame表，回调不格式化、不查询、不调用产品API。
            private Dictionary<int,long[]> costFrames;
            private Action costRestore;private Func<int> costErrorCount;
            private int costErrorsAtStart,costObserverErrors,costPhase;private long costOverflow;
            private bool costWindow,costStarted,costFinished,costReachedBoundary,disposed;
            private int costStartFrame=-1,costEndFrame=-1;
            private string reservations;
            private bool reservationWaitEnabled,reservationWaitInstalled,reservationWaitCaptured;
            private string reservationWaitMessage;
            private List<string> reservationWaitFacts;
            private float nextSample;
            public readonly List<string> ObserverErrors=new List<string>();
            public int Dropped { get; private set; }
            public ServiceTrafficOriginalTrace(CustomerFlowController c,EmployeeFlowController e,NavigationWorld w)
            {
                customers=c;employees=e;world=w;
                try
                {
                Hook(c,"PickupObserver",(Action<string,string,PickupQueueService,QueueAssignment>)((op,id,q,a)=>Safe(()=>Record("PICKUP "+op+" id="+id+" assignment="+Assignment(a)+" canonical="+Reservations(q)+" "+Staff()))));
                Hook(c,"CorridorObserver",(Action<string,string,string,Vector3,IReadOnlyList<Vector3>,Vector3[],string>)Corridor);
                Hook(c,"SegmentFailureObserver",(Action<string>)(message=>Safe(()=>Record(message))));
                Hook(e,"MovementObserver",(Action<string,string,string>)((id,op,value)=>Safe(()=>Record("EMPLOYEE "+op+" id="+id+" "+value+" "+Staff()+" "+Reservations(Read(c,"pickupQueue") as PickupQueueService)))));
                var path=typeof(NavMeshMovementDriver).GetField("PathObserver",BindingFlags.Static|BindingFlags.NonPublic);
                var prior=path.GetValue(null);Assert.That(prior,Is.Null,"OBSERVER: do not replace an active observer");
                path.SetValue(null,(Action<NavigationActor,string,string>)((actor,stage,value)=>Safe(()=>{
                    driverStages[stage]=driverStages.TryGetValue(stage,out var count)?count+1:1;
                    if(summaryTrace){summaryDriverOmitted++;return;}
                    Record("DRIVER "+stage+" id="+actor.ActorId+" pose="+actor.transform.position.ToString("F9")+" "+value);
                })));
                restore.Add(()=>path.SetValue(null,prior));
                if(FixedCostWindow)InstallCostObservers();
                Record("TRACE_BEGIN traceMode="+(summaryTrace?"Summary":"Full")+" maxRecords="+RecordLimit+" allAdapterNativeQueryCount=UNKNOWN originalObservers=True posthocNativeRequery=False");
                }
                catch
                {
                    // A failed constructor has no using/Dispose owner yet / 安装失败也必须恢复原hooks。
                    EndCostWindow();
                    for(var i=restore.Count-1;i>=0;i--)restore[i]();
                    throw;
                }
            }
            public void InstallReservationWaitFailureWitness()
            {
                if(!Environment.GetCommandLineArgs().Contains("-reservationWaitFailureWitness"))return;
                reservationWaitEnabled=true;reservationWaitFacts=new List<string>();var core=Read(customers,"traffic");
                Hook(core,"WaitFailureObserver",(Action<string>)(message=>Safe(()=>CaptureReservationWait(core,message))));
                reservationWaitInstalled=true;
            }
            private static string ReservationKey(TrafficIntentKey key)=>key.Session+"/e"+key.Epoch+"/"+key.ActorId+"/"+key.IntentId+
                "/g"+key.Generation+"/r"+key.LayoutRevision+"/"+key.Context;
            private static string ReservationRef(object value)=>value==null?"NULL":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            private static string ReservationFlags(object value,params string[] names)=>value==null?"NULL":string.Join(" ",names.Select(n=>n+"="+Read(value,n)));
            private static string ReservationDecision(TrafficDecision value)=>value==null?"NULL":ReservationRef(value)+"/"+value.Kind+"/"+value.Reason+"/"+
                value.FailureDisposition+"/seq"+value.RequestSequence+"/acq"+value.AcquiredSequence;
            private void CaptureReservationWait(object core,string message)
            {
                if(reservationWaitCaptured || message==null || !message.StartsWith("WAIT_ORIGINAL ",StringComparison.Ordinal))return;
                var start=message.IndexOf(" intent=",StringComparison.Ordinal);var end=message.IndexOf(" previous=",start<0?0:start,StringComparison.Ordinal);
                if(start<0 || end<=start)return;var intent=message.Substring(start+8,end-start-8);
                var visits=Read(customers,"visits") as IDictionary;var entries=Read(core,"entries") as IDictionary;
                if(visits==null || entries==null)throw new InvalidOperationException("Reservation raw registry unavailable");
                foreach(var visit in visits.Values.Cast<object>())
                {
                    var anchor=Read(visit,"PendingReservationAnchor");if(!(Read(anchor,"Key") is TrafficIntentKey key) || key.IntentId!="pickup-wait" ||
                        ReservationKey(key)!=intent || !ReferenceEquals(Read(anchor,"Coordinator"),core))continue;
                    // Lock the original message FIRST; Safe isolates all formatting from the original Fail.
                    // 精确当前anchor key；不调用GetDecision/CanProgress/IsCurrent，不拿终态重算判定。
                    reservationWaitCaptured=true;reservationWaitMessage=message;
                    var selected=entries.Contains(key)?entries[key]:null;var snapshot=Read(core,"snapshot") as TrafficWorldSnapshot;
                    reservationWaitFacts.Add("CAPTURE_AT_ORIGINAL_BEFORE_FAIL key="+intent+" anchor="+ReservationRef(anchor)+" entry="+ReservationRef(selected)+" "+
                        ReservationFlags(anchor,"Submitted","SideIndex","FirstFrame","Generation","Cursor","Phase"));
                    reservationWaitFacts.Add("VISIT "+ReservationFlags(visit,"Id","State","Generation","TrafficPhaseSequence","RequestId","PickupAssigned","HasCollected")+
                        " PendingTraffic="+ReservationRef(Read(visit,"PendingTraffic"))+" IssuedTraffic="+ReservationRef(Read(visit,"IssuedTraffic")));
                    var actors=new HashSet<string>{key.ActorId};
                    foreach(var frozen in (Read(selected,"FrozenOwners") as IEnumerable<TrafficIntentKey>)??Enumerable.Empty<TrafficIntentKey>())actors.Add(frozen.ActorId);
                    foreach(var block in (Read(selected,"WaitingBlockers") as IEnumerable<TrafficBlocker>)??Enumerable.Empty<TrafficBlocker>())actors.Add(block.Owner.ActorId);
                    var ownerStart=message.IndexOf(" owner=",StringComparison.Ordinal);var ownerEnd=message.IndexOf(" proof=",ownerStart<0?0:ownerStart,StringComparison.Ordinal);
                    var owner=ownerStart>=0 && ownerEnd>ownerStart?message.Substring(ownerStart+7,ownerEnd-ownerStart-7):"NOT_CAPTURED";
                    foreach(TrafficIntentKey candidate in entries.Keys)if(ReservationKey(candidate)==owner)actors.Add(candidate.ActorId);
                    if(snapshot!=null)foreach(var body in snapshot.Bodies)if(ReservationKey(body.Owner)==owner)actors.Add(body.Owner.ActorId);
                    var related=entries.Keys.Cast<TrafficIntentKey>().Where(k=>actors.Contains(k.ActorId)).ToArray();
                    reservationWaitFacts.Add("RELEVANT_ENTRIES count="+related.Length+" overflow="+(related.Length>128));
                    foreach(var candidate in related.Take(128))
                    {
                        var entry=entries[candidate];reservationWaitFacts.Add("ENTRY "+ReservationKey(candidate)+" ref="+ReservationRef(entry)+" "+
                            ReservationFlags(entry,"FirstFrame","Queries","RequestSequence","AcquiredSequence","FiniteLimit","WaitRemaining","Parking","Prepared","Queued","Active","Retired","JobIndex")+
                            " decision="+ReservationDecision(Read(entry,"Decision") as TrafficDecision)+" permit="+ReservationDecision(Read(entry,"JourneyPermit") as TrafficDecision)+
                            " current="+Points(Read(entry,"CurrentPath") as IEnumerable<Vector3>)+" journey="+Points(Read(entry,"Journey") as IEnumerable<Vector3>));
                        reservationWaitFacts.Add("FROZEN "+string.Join(";",((Read(entry,"FrozenOwners") as IEnumerable<TrafficIntentKey>)??Enumerable.Empty<TrafficIntentKey>()).Select(ReservationKey)));
                        var resources=Read(entry,"FrozenResources") as IDictionary;
                        if(resources!=null)foreach(TrafficIntentKey resourceOwner in resources.Keys)reservationWaitFacts.Add("FROZEN_RESOURCE "+ReservationKey(resourceOwner)+"={"+string.Join(";",((IEnumerable)resources[resourceOwner]).Cast<string>())+"}");
                        foreach(var block in (Read(entry,"WaitingBlockers") as IEnumerable<TrafficBlocker>)??Enumerable.Empty<TrafficBlocker>())reservationWaitFacts.Add("WAITING_BLOCKER "+ReservationKey(block.Owner)+"/"+block.ResourceId+"/"+block.Proof);
                        var request=Read(entry,"Request") as TrafficRequest;
                        reservationWaitFacts.Add(request==null?"REQUEST NULL":"REQUEST ref="+ReservationRef(request)+" key="+ReservationKey(request.Intent)+" frame="+request.RequestedFrame+
                            " world="+ReservationRef(request.Snapshot)+"/"+request.Snapshot?.Version+"/"+request.Snapshot?.BodyVersion+" start="+request.Start.ToString("F9")+" goal="+request.Target.ToString("F9")+
                            " radius="+request.Radius+" margin="+request.SafetyMargin+" selected="+Points(request.SelectedJourney));
                        var jobs=Read(entry,"Jobs") as IEnumerable;
                        if(jobs!=null)foreach(var job in jobs.Cast<object>())reservationWaitFacts.Add("JOB ref="+ReservationRef(job)+" waypoints="+Points(Read(job,"<Waypoints>k__BackingField") as IEnumerable<Vector3>)+
                            " expanded="+Points(Read(job,"<ExpandedPath>k__BackingField") as IEnumerable<Vector3>)+" legEnds=["+string.Join(",",(Read(job,"<LegEnds>k__BackingField") as int[])??Array.Empty<int>())+"]");
                        var leg=Read(entry,"Leg");reservationWaitFacts.Add("LEG ref="+ReservationRef(leg)+" "+ReservationFlags(leg,"Index","Generation","Committed","Bound","Completed","NeedsQuery","Suspended")+
                            " actorLife="+ReservationRef(Read(leg,"ActorLife"))+" path="+Points(Read(leg,"Path") as IEnumerable<Vector3>)+" suffix="+Points(Read(leg,"Suffix") as IEnumerable<Vector3>));
                        var handoff=Read(entry,"PendingHandoff");reservationWaitFacts.Add("HANDOFF ref="+ReservationRef(handoff)+" "+ReservationFlags(handoff,"Remaining","InspectingPreparation","CommittingPreparedSuccessor")+
                            " handle="+ReservationRef(Read(handoff,"Handle"))+" source="+ReservationDecision(Read(handoff,"Source") as TrafficDecision)+" successor="+ReservationRef(Read(handoff,"PreparedSuccessor")));
                        var dependencies=Read(handoff,"Dependencies") as IDictionary;
                        if(dependencies!=null)foreach(var dependent in dependencies.Keys.Cast<object>().Take(128))reservationWaitFacts.Add("HANDOFF_DEPENDENCY ref="+ReservationRef(dependent)+
                            " key="+(Read(dependent,"Key") is TrafficIntentKey dependencyKey?ReservationKey(dependencyKey):"NOT_CAPTURED")+" isSelectedEntry="+ReferenceEquals(dependent,selected)+
                            " resources={"+string.Join(";",((IEnumerable)dependencies[dependent]).Cast<string>())+"}");
                    }
                    reservationWaitFacts.Add("SNAPSHOT ref="+ReservationRef(snapshot)+" version="+snapshot?.Version+" bodyVersion="+snapshot?.BodyVersion);
                    if(snapshot!=null)
                    {
                        foreach(var body in snapshot.Bodies)reservationWaitFacts.Add("BODY "+ReservationKey(body.Owner)+" pose="+body.Position.ToString("F9")+" radius="+body.Radius+" margin="+body.SafetyMargin+
                            " enabled="+body.Enabled+" active="+body.ActorActive+" finite="+body.IndependentlyProgressing+" remaining="+body.RemainingSeconds+" life="+ReservationRef(body.ActorLife));
                        foreach(var p in snapshot.Parking)reservationWaitFacts.Add("PARKING "+ReservationKey(p.Owner)+" resource="+p.ResourceId+" pose="+p.Position.ToString("F9")+" radius="+p.Radius+" margin="+p.SafetyMargin+
                            " owner="+ReservationRef(p.CanonicalOwner)+" version="+p.OwnerVersion+" business="+ReservationRef(p.BusinessOwner)+" obligation="+ReservationRef(p.ReleaseObligation)+" marker="+ReservationRef(p.SemanticCheckpoint));
                    }
                    return;
                }
            }
            private void InstallCostObservers()
            {
                var type=typeof(NavigationLayoutAdapter).Assembly.GetType("AnimalCafe.Navigation.NavigationCostObservation",true);
                const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
                var native=type.GetField("NativeCall",flags);var ready=type.GetField("ProcessReadyElapsed",flags);var errors=type.GetField("ObserverErrors",flags);
                var priorNative=native.GetValue(null);var priorReady=ready.GetValue(null);
                Assert.That(priorNative,Is.Null,"R19: do not replace an active Native observer");
                Assert.That(priorReady,Is.Null,"R19: do not replace an active ready observer");
                costFrames=new Dictionary<int,long[]>();costErrorCount=()=>(int)errors.GetValue(null);costErrorsAtStart=costErrorCount();
                costRestore=()=>{native.SetValue(null,priorNative);ready.SetValue(null,priorReady);};
                native.SetValue(null,(Action<int,int,int>)ObserveNativeCost);
                ready.SetValue(null,(Action<int,long>)ObserveReadyCost);
            }
            private long[] CostFrame(int frame)
            {
                if(costFrames.TryGetValue(frame,out var row))return row;
                if(costFrames.Count>=200000){costOverflow++;return null;}
                row=new long[19];row[17]=costPhase;row[18]=-1;costFrames.Add(frame,row);return row; // -1: elapsed not sampled, never zero.
            }
            private void CostError(Exception error)
            {if(ObserverErrors.Count<16)ObserverErrors.Add("R19 observer: "+error);}
            private void ObserveNativeCost(int site,int frame,int outcome)
            {
                if(!costWindow)return;
                try
                {
                    if(site<0 || site>4 || outcome< -1 || outcome>1)throw new InvalidOperationException("R19 unexpected site/outcome");
                    var row=CostFrame(frame);if(row!=null)row[site*3+outcome+1]++;
                }
                catch(Exception error){CostError(error);}
            }
            private void ObserveReadyCost(int frame,long ticks)
            {
                if(!costWindow)return;
                try{var row=CostFrame(frame);if(row!=null){row[15]++;row[16]+=ticks;}}
                catch(Exception error){CostError(error);}
            }
            public void BeginCostWindow()
            {
                if(!FixedCostWindow || costStarted || costFinished)return;
                costStarted=true;costStartFrame=Time.frameCount;costWindow=true;CostPhase(0);
            }
            public void CostBoundaryReached(bool reached)
            {if(costWindow)costReachedBoundary=reached;} // Called only after the original while exits normally.
            public void CostPhase(int phase)
            {
                if(!costWindow)return;costPhase=phase;
                var row=CostFrame(Time.frameCount);if(row!=null)row[17]=phase; // Include zero-query frames.
            }
            public void EndCostWindow()
            {
                if(!FixedCostWindow || costFinished)return;costWindow=false;costFinished=true;
                if(costStarted)costEndFrame=Time.frameCount;
                try{costRestore?.Invoke();}
                finally
                {
                    costRestore=null;costObserverErrors=costErrorCount==null?0:costErrorCount()-costErrorsAtStart;
                    if(costObserverErrors!=0)ObserverErrors.Add("R19 isolated callback errors="+costObserverErrors);
                    if(costOverflow!=0)ObserverErrors.Add("R19 frame table overflow="+costOverflow);
                }
            }
            private string AdapterNativeCount()
                =>FixedCostWindow && costStarted && costFinished && costReachedBoundary && costOverflow==0 && costObserverErrors==0 && ObserverErrors.Count==0
                    ?costFrames.Values.Sum(r=>r.Take(6).Sum()).ToString(CultureInfo.InvariantCulture)
                    :"UNKNOWN (counter disabled or observation incomplete; no diagnostic requery)";
            private void WriteCostEvidence(string directory)
            {
                if(!FixedCostWindow)return;
                var totals=new long[17];foreach(var row in costFrames.Values)for(var i=0;i<totals.Length;i++)totals[i]+=row[i];
                var counterClosed=costStarted && costFinished;
                var countsComplete=counterClosed && costOverflow==0 && costObserverErrors==0 && ObserverErrors.Count==0;
                var fullWindow=countsComplete && costReachedBoundary;
                var report="R19_COST rawNativeBoolOnly=True internalEngineNavOperations=UNKNOWN elapsed=ProcessReady synchronous inclusive (not OS CPU)"+
                    " stopwatchFrequency="+Stopwatch.Frequency+" frames="+costFrames.Count+" overflow="+costOverflow+
                    " callbackErrors="+costObserverErrors+" observerErrors="+ObserverErrors.Count+
                    " counterWindowStarted="+costStarted+" counterWindowClosed="+counterClosed+" observedCountsComplete="+countsComplete+
                    " original120BoundaryReached="+costReachedBoundary+" full120WindowComplete="+fullWindow+
                    " startFrame="+(costStarted?costStartFrame.ToString():"UNKNOWN")+" endFrame="+(counterClosed?costEndFrame.ToString():"UNKNOWN")+
                    " benchmarkAcceptance=NOT_EVALUATED (original NUnit business assertions remain authoritative)"+
                    " observedPartialNativeCalls="+(costStarted?totals.Take(15).Sum().ToString(CultureInfo.InvariantCulture):"UNKNOWN")+" allFiveExplicitNativeCalls="+
                    (fullWindow?totals.Take(15).Sum().ToString(CultureInfo.InvariantCulture):"UNKNOWN")+" adapter="+AdapterNativeCount()+
                    " observedReadyCalls="+(costStarted?totals[15].ToString():"UNKNOWN")+" observedReadyElapsedMs="+
                    (costStarted?(totals[16]*1000d/Stopwatch.Frequency).ToString("F6",CultureInfo.InvariantCulture):"UNKNOWN");
                for(var site=0;site<5;site++)report+="\nsite="+site+" observedThrows="+(costStarted?totals[site*3].ToString():"UNKNOWN")+
                    " observedFalse="+(costStarted?totals[site*3+1].ToString():"UNKNOWN")+" observedTrue="+(costStarted?totals[site*3+2].ToString():"UNKNOWN");
                for(var phase=0;phase<3;phase++)
                {
                    var rows=costFrames.Values.Where(v=>v[17]==phase).ToArray();
                    report+="\nphase="+phase+" (0active/1draining/2idle; last loop sample) frames="+rows.Length+
                        " observedCalls="+(costStarted?rows.Sum(v=>v.Take(15).Sum()).ToString():"UNKNOWN")+
                        " observedReadyTicks="+(costStarted?rows.Sum(v=>v[16]).ToString():"UNKNOWN");
                }
                File.WriteAllText(Path.Combine(directory,"native-cost-summary.txt"),report);
                const string header="frame,aPath_throw,aPath_false,aPath_true,aComplete_throw,aComplete_false,aComplete_true,driverStart_throw,driverStart_false,driverStart_true,rebind_throw,rebind_false,rebind_true,edge_throw,edge_false,edge_true,readyCalls,readyTicks,phaseLastLoop,frameElapsedMicroseconds,frameElapsedKnown";
                File.WriteAllLines(Path.Combine(directory,"native-cost-frames.csv"),new[]{header}.Concat(costFrames.OrderBy(p=>p.Key).Select(p=>
                    p.Key+","+string.Join(",",p.Value.Take(18))+","+(p.Value[18]<0?"UNKNOWN":p.Value[18].ToString())+","+(p.Value[18]>=0))));
                TestContext.WriteLine(report);
            }
            private void Hook(object owner,string name,Delegate callback)
            {
                var field=owner.GetType().GetField(name,Fields);Assert.That(field,Is.Not.Null,"OBSERVER: existing hook "+name);
                var prior=field.GetValue(owner);Assert.That(prior,Is.Null,"OBSERVER: do not replace active "+name);
                field.SetValue(owner,callback);restore.Add(()=>
                {
                    if(!ReferenceEquals(field.GetValue(owner),callback))throw new InvalidOperationException("OBSERVER: replacement preserved for "+name);
                    field.SetValue(owner,prior);
                });
            }
            private void Safe(Action action)
            {
                try{action();}
                catch(Exception error){if(ObserverErrors.Count<16)ObserverErrors.Add(error.ToString());}
            }
            public void Record(string value)
            {
                if(records.Count>=RecordLimit){Dropped++;return;}
                records.Add("frame="+Time.frameCount+" sceneTime="+Time.time.ToString("F6",CultureInfo.InvariantCulture)+" "+value);
            }
            public void Milestone(string value)
            {
                // Separate bounded state receipt survives an event-buffer overflow / 关键状态独立保存。
                if(milestones.Count>=256){Dropped++;return;}
                milestones.Add("frame="+Time.frameCount+" sceneTime="+Time.time.ToString("F6",CultureInfo.InvariantCulture)+" "+value);
            }
            private static object Read(object owner,string name)=>owner?.GetType().GetField(name,Fields)?.GetValue(owner);
            private static string Points(IEnumerable<Vector3> path)=>path==null?"NOT_OBSERVED":"["+string.Join(";",path.Select(p=>p.ToString("F9")))+"]";
            private static string Targets(object targets)=>targets is IEnumerable<NavigationTarget> route?Points(route.Select(t=>t.Position)):"NO_CURRENT_SUFFIX";
            private static string Assignment(QueueAssignment a)=>a.VisitId+"/slot"+a.SlotIndex+"/g"+a.Generation+"@"+a.Position.ToString("F9")+"/arrived="+a.Arrived;
            private static string Reservations(PickupQueueService queue)
            {
                if(queue==null)return "queue=NULL";
                var map=(IDictionary)Read(queue,"reservations");
                return "reservations=["+string.Join(";",map.Keys.Cast<object>().OrderBy(k=>k.ToString(),StringComparer.Ordinal).Select(k=>{
                    var entry=map[k];return k+"/slot"+Read(entry,"Slot")+"/g"+Read(entry,"Generation")+"@"+((Vector3)Read(entry,"Target")).ToString("F9")+"/paid="+Read(entry,"Paid")+"/arrived="+Read(entry,"Arrived");
                }))+ "] paidFIFO=["+string.Join(";",queue.Snapshot.Select(Assignment))+"] leases=["+string.Join(";",queue.ServiceLeases.Select(p=>p.Key+"@"+p.Value.ToString("F9")))+"]";
            }
            private string Staff()
            {
                var roster=(IEnumerable)Read(employees,"employees");var drivers=(IDictionary)Read(world,"drivers");
                return "staff=["+string.Join(";",roster.Cast<object>().Select(row=>{
                    var runner=Read(row,"Runner");var actor=Read(row,"Actor") as NavigationActor;
                    var driver=actor!=null && drivers.Contains(actor)?drivers[actor]:null;
                    return Read(row,"Role")+"/"+Read(row,"Id")+" pose="+(actor==null?"NULL":actor.transform.position.ToString("F9"))+
                        " outwardIndex="+Read(runner,"deliveryRouteIndex")+" outwardStored="+Targets(Read(runner,"deliveryRoute"))+
                        " returnIndex="+Read(runner,"returnRouteIndex")+" returnStored="+Targets(Read(runner,"returnRoute"))+
                        " selectedOutwardSuffix="+Targets(runner?.GetType().GetProperty("RemainingPickupDeliveryRoute",Fields)?.GetValue(runner))+
                        " selectedReturnSuffix="+Targets(runner?.GetType().GetProperty("RemainingPickupReturnRoute",Fields)?.GetValue(runner))+
                        " driverCorner="+Read(driver,"corner")+" raw="+Points(Read(driver,"rawCorners") as IEnumerable<Vector3>)+
                        " current="+Points(Read(driver,"corners") as IEnumerable<Vector3>)+" nativeMoving="+Read(driver,"moving");
                }))+"]";
            }
            public string State()=>"customers=["+string.Join(";",customers.Snapshot.Select(v=>v.VisitId+"/"+v.State+"@"+v.Position.ToString("F9")))+"] employees=["+
                string.Join(";",employees.Snapshot.Select(e=>e.ActorId+"/"+e.State+"/"+e.Failure+"/h"+e.HandleId))+"] "+Reservations(Read(customers,"pickupQueue") as PickupQueueService)+" "+Staff();
            private void Corridor(string operation,string caller,string related,Vector3 target,IReadOnlyList<Vector3> path,Vector3[] obstacles,string result)
            {
                Safe(()=>{
                    if(operation.StartsWith("plan.exit",StringComparison.Ordinal) && result!=null)
                    {
                        var parts=result.Split(' ');var invocation=parts.FirstOrDefault(p=>p.StartsWith("invocation=",StringComparison.Ordinal));
                        var purpose=parts.FirstOrDefault(p=>p.StartsWith("purpose=",StringComparison.Ordinal));
                        var cost=parts.FirstOrDefault(p=>p.StartsWith("queries=",StringComparison.Ordinal));
                        if(invocation!=null && cost!=null && result.Contains("queryKind=raw-native"))exitQueries[purpose+" "+invocation]=int.Parse(cost.Substring(8),CultureInfo.InvariantCulture);
                    }
                    // Summary preserves existing counters without formatting hot Native geometry or requerying it.
                    // 省略热路径详情不算证据buffer溢出；关键状态、真实Exit与业务事件仍保留。
                    if(summaryTrace){summaryCorridorOmitted++;return;}
                    var semantic="CORRIDOR "+operation+" caller="+caller+" related="+related+" target="+target.ToString("F9")+" originalPath="+Points(path)+
                        " originalObstacles="+Points(obstacles)+" originalResult="+result;
                    var key=operation+"|"+caller+"|"+related;
                    if(lastCorridor.TryGetValue(key,out var previous) && previous==semantic)
                    {repeatedCorridor[key]=repeatedCorridor.TryGetValue(key,out var repeat)?repeat+1:1;return;}
                    lastCorridor[key]=semantic;
                    Record(semantic+
                        ((operation=="reserve.candidate" || operation=="reserve.attempt" || operation=="employee.path-check")?" "+Reservations(Read(customers,"pickupQueue") as PickupQueueService)+" "+Staff():""));
                });
            }
            public void Sample()
            {
                Safe(()=>{
                    var frame=(int)Read(customers,"pickupGoalQueryFrame");var count=(int)Read(customers,"pickupGoalQueries");
                    if(frame>=0)goalQueries[frame]=goalQueries.TryGetValue(frame,out var prior)?Math.Max(prior,count):count;
                    var current=Reservations(Read(customers,"pickupQueue") as PickupQueueService);
                    if(current!=reservations){reservations=current;Record("CANONICAL_CHANGE "+current+" "+Staff());}
                    if(Time.time>=nextSample){nextSample=Time.time+1;Milestone("FRAME_STATE "+State());}
                });
            }
            public void Frame(double milliseconds)
            {
                frameMs.Add(milliseconds);
                if(costWindow){var row=CostFrame(Time.frameCount);if(row!=null)row[18]=(long)Math.Round(milliseconds*1000d);}
            }
            public void Dispose()
            {
                if(disposed)return;disposed=true;EndCostWindow();
                var restoreErrors=new List<Exception>();
                for(var i=restore.Count-1;i>=0;i--)
                    try{restore[i]();}catch(Exception error){restoreErrors.Add(error);}
                if(restoreErrors.Count>0)throw new AggregateException("OBSERVER: cleanup UNKNOWN; all hook restorers attempted",restoreErrors);
                var directory=Path.Combine("outputs","phase15","service-traffic-coordination-20261030","task-1",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
                Directory.CreateDirectory(directory);
                File.WriteAllLines(Path.Combine(directory,"original-decision-trace.txt"),records);
                File.WriteAllLines(Path.Combine(directory,"critical-state-trace.txt"),milestones);
                if(reservationWaitEnabled)File.WriteAllLines(Path.Combine(directory,"reservation-wait-failure-witness.txt"),
                    new[]{"status="+(reservationWaitCaptured?"OBSERVED_ORIGINAL_BEFORE_FAIL":"NOT_OBSERVED")+" installed="+reservationWaitInstalled+
                        " cap=1 observerErrors="+string.Join(";",ObserverErrors),reservationWaitMessage??"ORIGINAL_MESSAGE_NOT_CAPTURED"}.Concat(reservationWaitFacts??new List<string>()));
                var ordered=frameMs.OrderBy(value=>value).ToArray();
                double Percentile(double unit)=>ordered.Length==0?double.NaN:ordered[Math.Min(ordered.Length-1,(int)Math.Ceiling(unit*ordered.Length)-1)];
                var baseline="method="+TestContext.CurrentContext.Test.FullName+"\nutc="+DateTime.UtcNow.ToString("O")+
                    "\ntraceMode="+(summaryTrace?"Summary":"Full")+" summaryCorridorEventsOmitted="+summaryCorridorOmitted+" summaryDriverEventsOmitted="+summaryDriverOmitted+
                    "\ntraceEnabled=True\nframeMetric=Stopwatch real elapsed around yield-null; includes engine/test/trace overhead; scene captureDeltaTime="+Time.captureDeltaTime+
                    "\nframes="+frameMs.Count+" P95ms="+Percentile(.95).ToString("F3",CultureInfo.InvariantCulture)+" P99ms="+Percentile(.99).ToString("F3",CultureInfo.InvariantCulture)+
                    " maxMs="+(ordered.Length==0?double.NaN:ordered.Last()).ToString("F3",CultureInfo.InvariantCulture)+
                    " framesOver500ms="+frameMs.Count(value=>value>500d)+
                    "\nallAdapterNativeQueryCount="+AdapterNativeCount()+
                    "\nobservedRawExitInvocations="+exitQueries.Count+" observedRawExitNativeQueries="+exitQueries.Values.Sum()+" maxQueriesPerRawExit="+(exitQueries.Count==0?0:exitQueries.Values.Max())+
                    "\nobservedGoalBudgetFrames="+goalQueries.Count+" observedGoalBudgetQueries="+goalQueries.Values.Sum()+" maxGoalQueriesPerFrame="+(goalQueries.Count==0?0:goalQueries.Values.Max())+
                    "\noriginalDriverStages="+string.Join(";",driverStages.Select(p=>p.Key+"="+p.Value))+
                    "\nsemanticExactRepeatCorridorEventsOmitted="+repeatedCorridor.Values.Sum()+" byOperationActor="+string.Join(";",repeatedCorridor.Select(p=>p.Key+"="+p.Value))+
                    "\nrecords="+records.Count+" dropped="+Dropped+" observerErrors="+string.Join(";",ObserverErrors);
                WriteCostEvidence(directory);
                File.WriteAllText(Path.Combine(directory,"baseline.txt"),baseline);
                File.WriteAllLines(Path.Combine(directory,"frame-ms.csv"),new[]{"sample,elapsed_ms"}.Concat(frameMs.Select((value,index)=>index+","+value.ToString("F6",CultureInfo.InvariantCulture))));
                TestContext.WriteLine("SERVICE_TRAFFIC_ORIGINAL_TRACE "+directory+"\n"+baseline);
            }
        }
    }
}
#endif

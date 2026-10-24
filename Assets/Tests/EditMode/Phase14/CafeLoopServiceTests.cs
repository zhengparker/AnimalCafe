using System;
using System.Linq;
using System.Reflection;
using AnimalCafe.CafeLoop;
using AnimalCafe.Capacity;
using AnimalCafe.Customers;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using AnimalCafe.Orders;
using NUnit.Framework;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase14
{
    public class CafeLoopServiceTests
    {
        private CafeLoopEnvironmentFake env;
        private CafeLoopService service;
        private static SurfaceSlotAddress Slot => CafeDeliveryStoreTests.Slot;
        [SetUp] public void Setup()
        { env=new CafeLoopEnvironmentFake{CollectionAllowed=false}; service=new CafeLoopService("session",env); AddVisit("a"); }
        [TearDown] public void Cleanup() { env.OnProject=null; service.Dispose(); }
        private void AddVisit(string id) => env.VisitValues.Add(new CafeVisitSnapshot(id,CustomerVisitState.Queued,
            Vector3.zero,counterArrived:true,layoutRevision:7));
        private void AtPickup(string id)
        {
            var i=env.VisitValues.FindIndex(v=>v.VisitId==id);
            env.VisitValues[i]=new CafeVisitSnapshot(id,CustomerVisitState.PickupService,Vector3.zero,
                counterArrived:true,pickupArrived:true,layoutRevision:7);
        }
        private int Index(EmployeeRole role) => env.StartedCheckpoints.FindLastIndex(c=>c.Role==role);
        private CafeTaskEvent Event(EmployeeRole role,CafeTaskEventKind kind,CafeWorkStage stage,float remaining=0,int generation=1,long request=0)
        {
            var i=Index(role); Assert.That(i,Is.GreaterThanOrEqualTo(0),"A real service dispatch must exist first");
            var saved=env.StartedCheckpoints[i]; var token=env.StartedTokens[i];
            return new CafeTaskEvent(token,role,env.EmployeeId(role),token.Sequence*10+token.Attempt,request,generation,7,kind,
                new CafeWorkCheckpoint(role,saved.VisitId,saved.Order,stage,remaining));
        }
        private void Start() { service.Tick(.1f); Assert.That(Index(EmployeeRole.Cashier),Is.GreaterThanOrEqualTo(0)); }
        private void FinishCashier()
        {
            service.ReportTask(Event(EmployeeRole.Cashier,CafeTaskEventKind.Progress,CafeWorkStage.Working,1));
            service.ReportTask(Event(EmployeeRole.Cashier,CafeTaskEventKind.ServiceFinished,CafeWorkStage.Finished));
            env.IdleRoles.Add(EmployeeRole.Cashier); service.Tick(.1f);
        }
        private CafeTaskEvent Arrival()
        {
            service.ReportTask(Event(EmployeeRole.Barista,CafeTaskEventKind.Progress,CafeWorkStage.Working,3));
            service.ReportTask(Event(EmployeeRole.Barista,CafeTaskEventKind.Prepared,CafeWorkStage.Prepared,generation:2));
            var arrival=Event(EmployeeRole.Barista,CafeTaskEventKind.DeliveryArrived,CafeWorkStage.Prepared,generation:3,request:71);
            service.ReportTask(arrival);
            return arrival;
        }
        private CafeTaskEvent Deliver()
        {
            var arrival=Arrival();
            Assert.That(service.TryCommitDelivery(arrival,Slot,out var reason),Is.True,reason);
            return arrival;
        }
        private void ReturnBarista()
        {
            service.ReportTask(Event(EmployeeRole.Barista,CafeTaskEventKind.Returned,CafeWorkStage.Finished,generation:4,request:72));
            env.IdleRoles.Add(EmployeeRole.Barista); service.Tick(.1f);
        }
        private Delegate Inject(string field,Delegate value)
        {
            var info=typeof(CafeLoopService).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(info,Is.Not.Null); var previous=(Delegate)info.GetValue(service); info.SetValue(service,value); return previous;
        }

        [Test] public void InitialCashierDispatchUsesOneSecondBeforeAnyPaidOrder()
        {
            Start();
            Assert.That(env.StartedCheckpoints[Index(EmployeeRole.Cashier)].RemainingWorkSeconds,Is.EqualTo(1f));
            Assert.That(service.Snapshot.Orders,Is.Empty);
        }

        [Test] public void CashierCompletionCreatesOnePaidOrder()
        {
            Start(); FinishCashier();
            Assert.That(service.Snapshot.Orders.Count,Is.EqualTo(1));
            CollectionAssert.AreEqual(new[]{"a"},service.Snapshot.PaidVisitIds);
            Assert.That(service.Snapshot.Orders.Single().Value.ClaimantId,Is.EqualTo("barista-real"));
            for(var i=0;i<10;i++) service.Tick(.1f);
            Assert.That(service.Snapshot.Orders.Count,Is.EqualTo(1));
            Assert.That(env.PickupMoves.Count(v=>v=="a"),Is.EqualTo(1));
        }
        [Test] public void NotArrivedAndWrongPhaseCannotChargeThenDuplicateSuccessCreatesOnlyOneOrder()
        {
            env.VisitValues[0]=new CafeVisitSnapshot("a",CustomerVisitState.Queued,Vector3.zero,counterArrived:false,layoutRevision:7);
            service.Tick(10); Assert.That(env.StartedTokens,Is.Empty); Assert.That(service.Snapshot.Orders,Is.Empty);
            env.VisitValues[0]=new CafeVisitSnapshot("a",CustomerVisitState.Entering,Vector3.zero,counterArrived:true,layoutRevision:7);
            service.Tick(10); Assert.That(env.StartedTokens,Is.Empty);
            env.VisitValues[0]=new CafeVisitSnapshot("a",CustomerVisitState.Queued,Vector3.zero,counterArrived:true,layoutRevision:7);
            Start(); var success=Event(EmployeeRole.Cashier,CafeTaskEventKind.ServiceFinished,CafeWorkStage.Finished);
            service.ReportTask(success); service.ReportTask(success); env.IdleRoles.Add(EmployeeRole.Cashier); service.Tick(.1f);
            service.ReportTask(success); service.Tick(.1f);
            Assert.That(service.Snapshot.Orders.Count,Is.EqualTo(1)); CollectionAssert.AreEqual(new[]{"a"},service.Snapshot.PaidVisitIds);
            Assert.That(env.PickupMoves.Count(id=>id=="a"),Is.EqualTo(1));
        }
        [Test] public void PhysicallyAllowedButUnreadyCollectionCannotMutateAnyBusinessOrReservation()
        {
            Start(); FinishCashier(); AtPickup("a"); env.CollectionAllowed=true;
            var reserved=env.ReservedVisits.ToArray(); var moves=env.ExitMoves.Count;
            Assert.That(service.TryCollect("a",out _),Is.False);
            Assert.That(service.Snapshot.Orders.Single().Value.State,Is.EqualTo(OrderState.Preparing));
            Assert.That(service.Snapshot.Delivery,Is.Empty); Assert.That(service.Snapshot.CollectedExitVisitIds,Is.Empty);
            CollectionAssert.AreEquivalent(reserved,env.ReservedVisits); Assert.That(env.ExitMoves.Count,Is.EqualTo(moves));
        }
        [Test] public void CreateRejectedLeavesNoPartialPaidRecordAndRestoresFinishedWithoutTwoMoreSeconds()
        {
            var calls=0;
            var original=Inject("createOrder",new Func<OrderService,string,string,OrderResult>((orders,visit,product)=>
            { calls++; return orders.Create("",product); }));
            Start(); FinishCashier();
            Assert.That(calls,Is.EqualTo(1)); Assert.That(service.Snapshot.Orders,Is.Empty);
            Assert.That(service.Snapshot.PaidVisitIds,Is.Empty); Assert.That(env.PickupMoves,Is.Empty);
            Assert.That(service.Snapshot.Employees.Single(c=>c.Role==EmployeeRole.Cashier).Stage,Is.EqualTo(CafeWorkStage.Finished));
            var old=env.StartedTokens[Index(EmployeeRole.Cashier)];
            for(var i=0;i<5;i++) service.Tick(1);
            Assert.That(calls,Is.EqualTo(1),"Blocked is not an automatic retry");
            Inject("createOrder",original); service.ResumeBlocked();
            var restored=env.StartedTokens[Index(EmployeeRole.Cashier)];
            Assert.That(restored.Sequence,Is.EqualTo(old.Sequence)); Assert.That(restored.Attempt,Is.GreaterThan(old.Attempt));
            Assert.That(env.StartedCheckpoints[Index(EmployeeRole.Cashier)].Stage,Is.EqualTo(CafeWorkStage.Finished));
            Assert.That(env.StartedCheckpoints[Index(EmployeeRole.Cashier)].RemainingWorkSeconds,Is.Zero);
            FinishCashier(); Assert.That(service.Snapshot.Orders.Count,Is.EqualTo(1));
        }
        [Test] public void RejectedTaskStartRetainsClaimWithoutClaimingAnotherOrder()
        {
            Start(); env.StartAllowed=false; FinishCashier();
            var claimed=service.Snapshot.Orders.Single();
            Assert.That(claimed.Value.State,Is.EqualTo(OrderState.Claimed));
            Assert.That(claimed.Value.ClaimantId,Is.EqualTo("barista-real"));
            service.Tick(10); Assert.That(service.Snapshot.Orders.Single().Value.State,Is.EqualTo(OrderState.Claimed));
            env.StartAllowed=true; service.ResumeBlocked();
            Assert.That(env.StartedCheckpoints[Index(EmployeeRole.Barista)].Order,Is.EqualTo(claimed.Key));
            Assert.That(service.Snapshot.Orders.Single().Value.State,Is.EqualTo(OrderState.Preparing));
        }
        [Test] public void ReturningBaristaCannotClaimNextEvenIfIdlePortIsWrong()
        {
            AddVisit("b"); Start(); FinishCashier(); FinishCashier(); AtPickup("a"); Deliver();
            env.IdleRoles.Add(EmployeeRole.Barista); service.Tick(.1f);
            Assert.That(service.Snapshot.Orders.Values.Single(o=>o.CustomerId=="b").State,Is.EqualTo(OrderState.Waiting));
            ReturnBarista();
            Assert.That(service.Snapshot.Orders.Values.Single(o=>o.CustomerId=="b").State,Is.EqualTo(OrderState.Preparing));
        }
        [Test] public void WrongOwnerOrNonHeadCannotCollectEvenWhenPortReturnsTrue()
        {
            AddVisit("b"); Start(); FinishCashier(); FinishCashier(); AtPickup("a"); AtPickup("b"); Deliver(); ReturnBarista(); Deliver();
            env.VisitValues.Reverse(); env.CollectionAllowed=true;
            Assert.That(service.TryCollect("b",out _),Is.False);
            Assert.That(service.TryCollect("wrong",out _),Is.False);
            Assert.That(service.TryCollect("a",out var reason),Is.True,reason);
            CollectionAssert.AreEqual(new[]{"a"},service.Snapshot.CollectedExitVisitIds);
            Assert.That(service.Snapshot.Delivery.Single().VisitId,Is.EqualTo("b"));
            Assert.That(service.TryCollect("a",out _),Is.False);
        }
        [Test] public void CompleteRejectedKeepsReadyDeliveryAndOriginalCapacity()
        {
            var capacity=new CapacityService(64); CustomerAdmission.TryCreate(capacity,"a",out var lease);
            lease.MarkInside(); lease.MarkQueued(); lease.LeaveQueue(); lease.MarkPickupWaiting();
            try
            {
                Start(); FinishCashier(); AtPickup("a"); Deliver(); env.CollectionAllowed=true;
                var calls=0; var original=Inject("completeOrder",new Func<OrderService,long,string,OrderResult>((orders,id,visit)=>
                { calls++; return orders.Complete(id,"wrong-owner"); }));
                var before=service.Snapshot.Delivery.Single();
                var reserved=env.ReservedVisits.ToArray(); var pickups=env.PickupMoves.ToArray(); var exits=env.ExitMoves.ToArray();
                Assert.That(service.TryCollect("a",out _),Is.False); Assert.That(calls,Is.EqualTo(1));
                Assert.That(service.Snapshot.Orders.Single().Value.State,Is.EqualTo(OrderState.ReadyForPickup));
                Assert.That(service.Snapshot.Delivery.Single(),Is.EqualTo(before));
                Assert.That(service.Snapshot.CollectedExitVisitIds,Is.Empty);
                CollectionAssert.AreEquivalent(reserved,env.ReservedVisits,"Rejected Complete cannot release the actual service reservation");
                CollectionAssert.AreEqual(pickups,env.PickupMoves); CollectionAssert.AreEqual(exits,env.ExitMoves);
                CollectionAssert.AreEqual(new[]{1,0,1},capacity.GetCapacities().Select(c=>c.Used));
                service.Tick(1); Assert.That(calls,Is.EqualTo(1));
                Inject("completeOrder",original); service.ResumeBlocked();
                Assert.That(service.TryCollect("a",out var reason),Is.True,reason);
                CollectionAssert.AreEqual(new[]{1,0,1},capacity.GetCapacities().Select(c=>c.Used));
            }
            finally { lease.Dispose(); }
        }
        [Test] public void CommitPublishesBeforeReentrantProjectionAndPreservesCanonicalCollectedIds()
        {
            Start(); FinishCashier(); AtPickup("a"); Deliver(); env.CollectionAllowed=true;
            CafeLoopSnapshot atProjection=null;
            env.OnProject=()=>{if(atProjection!=null) return; atProjection=service.Snapshot; service.ClearOrdersForDecoration(1);};
            Assert.That(service.TryCollect("a",out var reason),Is.True,reason);
            Assert.That(atProjection.Orders.Single().Value.State,Is.EqualTo(OrderState.Completed));
            Assert.That(atProjection.Delivery,Is.Empty); Assert.That(atProjection.CollectedExitVisitIds,Does.Contain("a"));
            Assert.That(service.Snapshot.Epoch,Is.EqualTo(2)); Assert.That(service.Snapshot.Orders,Is.Empty);
            Assert.That(service.Snapshot.CollectedExitVisitIds,Does.Contain("a")); Assert.That(env.ExitMoves,Is.Empty,"Do not issue a stale post-clear move");
        }
        [Test] public void ReplayCannotAffectNewEpochEvenWhenNumericOrderIdIsReused()
        {
            Start(); FinishCashier(); var old=Deliver(); service.ClearOrdersForDecoration(1);
            var epoch=service.Snapshot.Epoch; service.ClearOrdersForDecoration(1); Assert.That(service.Snapshot.Epoch,Is.EqualTo(epoch));
            env.VisitValues.Clear(); AddVisit("new"); env.IdleRoles.Add(EmployeeRole.Cashier); env.IdleRoles.Add(EmployeeRole.Barista);
            Start(); FinishCashier();
            Assert.That(service.Snapshot.Orders.Single().Key.OrderId,Is.EqualTo(1));
            Assert.That(service.Snapshot.Orders.Single().Key.Epoch,Is.EqualTo(2));
            service.ReportTask(old); Assert.That(service.TryCommitDelivery(old,Slot,out _),Is.False);
            Assert.That(service.Snapshot.Delivery,Is.Empty); Assert.That(service.Snapshot.PaidVisitIds,Does.Not.Contain("a"));
        }
        [Test] public void ReadyFactOverridesReentrantOldPreparedCheckpointAndRestoreOnlyReturns()
        {
            Start(); FinishCashier(); AtPickup("a");
            env.OnProject=()=>
            {
                service.ReportTask(Event(EmployeeRole.Barista,CafeTaskEventKind.Blocked,CafeWorkStage.Prepared,generation:3,request:71));
                env.Gate=false;
            };
            var arrival=Deliver();
            Assert.That(service.Snapshot.Orders.Single().Value.State,Is.EqualTo(OrderState.ReadyForPickup));
            Assert.That(service.Snapshot.Employees.Single(c=>c.Role==EmployeeRole.Barista).Stage,Is.GreaterThanOrEqualTo(CafeWorkStage.Delivered));
            env.OnProject=null; env.Gate=true; env.IdleRoles.Add(EmployeeRole.Barista); service.ResumeBlocked();
            var restored=env.StartedCheckpoints[Index(EmployeeRole.Barista)];
            Assert.That(restored.Stage,Is.GreaterThanOrEqualTo(CafeWorkStage.Delivered)); Assert.That(restored.RemainingWorkSeconds,Is.Zero);
            Assert.That(service.TryCommitDelivery(arrival,Slot,out _),Is.False);
            Assert.That(service.Snapshot.Delivery.Count,Is.EqualTo(1));
        }
        [Test] public void ProjectionExceptionCannotRollbackReadyOrCompleted()
        {
            Start(); FinishCashier(); AtPickup("a"); env.OnProject=()=>throw new InvalidOperationException("renderer unavailable");
            Deliver(); Assert.That(service.Snapshot.Orders.Single().Value.State,Is.EqualTo(OrderState.ReadyForPickup));
            Assert.That(service.Snapshot.Delivery.Count,Is.EqualTo(1)); env.CollectionAllowed=true;
            Assert.That(service.TryCollect("a",out var reason),Is.True,reason);
            Assert.That(service.Snapshot.Orders.Single().Value.State,Is.EqualTo(OrderState.Completed));
            Assert.That(service.Snapshot.Delivery,Is.Empty); Assert.That(service.Snapshot.CollectedExitVisitIds,Does.Contain("a"));
            Assert.That(service.Snapshot.Failure,Does.Contain("renderer"));
        }
        [Test] public void WorkingProgressIsSavedDuringPauseAndServiceNeverSubtractsTimeTwice()
        {
            Start(); env.Gate=false;
            service.ReportTask(Event(EmployeeRole.Cashier,CafeTaskEventKind.Progress,CafeWorkStage.Working,.75f));
            service.Tick(0);
            Assert.That(service.Snapshot.Employees.Single().RemainingWorkSeconds,Is.EqualTo(.75f));
            service.ReportTask(Event(EmployeeRole.Cashier,CafeTaskEventKind.Blocked,CafeWorkStage.Working,.75f));
            env.Gate=true; env.IdleRoles.Add(EmployeeRole.Cashier); service.ResumeBlocked();
            Assert.That(env.StartedCheckpoints[Index(EmployeeRole.Cashier)].RemainingWorkSeconds,Is.EqualTo(.75f));
            service.Tick(100); Assert.That(service.Snapshot.Orders,Is.Empty);
            Assert.That(service.Snapshot.Employees.Single().RemainingWorkSeconds,Is.EqualTo(.75f));
        }
        [Test] public void ZeroTickAndPauseCannotConsumeSuccessfulCompletion()
        {
            Start(); service.Tick(0);
            service.ReportTask(Event(EmployeeRole.Cashier,CafeTaskEventKind.ServiceFinished,CafeWorkStage.Finished));
            service.Tick(0); Assert.That(service.Snapshot.Orders,Is.Empty);
            env.Gate=false; service.Tick(1); Assert.That(service.Snapshot.Orders,Is.Empty);
            env.Gate=true; service.Tick(.1f); Assert.That(service.Snapshot.Orders.Count,Is.EqualTo(1));
        }
        [Test] public void SynchronousStartCompletionDoesNotOverwriteFinishedContext()
        {
            env.OnStart=(token,checkpoint)=>
            {
                if(checkpoint.Role!=EmployeeRole.Cashier) return;
                service.ReportTask(new CafeTaskEvent(token,checkpoint.Role,"cashier-real",11,0,1,7,
                    CafeTaskEventKind.ServiceFinished,new CafeWorkCheckpoint(checkpoint.Role,checkpoint.VisitId,null,CafeWorkStage.Finished,0)));
                env.IdleRoles.Add(EmployeeRole.Cashier);
            };
            Start(); service.Tick(.1f);
            Assert.That(service.Snapshot.Orders.Count,Is.EqualTo(1));
            Assert.That(service.Snapshot.Failure,Is.Empty);
        }
        [Test] public void SynchronousStartDecorationCannotBecomeBlockedInTheNewEpoch()
        {
            env.OnStart=(token,checkpoint)=>service.ClearOrdersForDecoration(1);
            service.Tick(.1f);
            Assert.That(service.Snapshot.Epoch,Is.EqualTo(2)); Assert.That(service.Snapshot.Employees,Is.Empty);
            Assert.That(service.Snapshot.Failure,Is.Empty);
        }
        [Test] public void ExitMustBeActualAndCanBeConsumedWhilePaused()
        {
            Start(); FinishCashier(); AtPickup("a"); Deliver(); env.CollectionAllowed=true;
            Assert.That(service.TryCollect("a",out _),Is.True);
            env.VisitValues.Clear(); env.Gate=false; service.Tick(0);
            Assert.That(service.Snapshot.CollectedExitVisitIds,Does.Contain("a"),"Disappearance is not exit evidence");
            env.VisitValues.Add(new CafeVisitSnapshot("a",CustomerVisitState.Exiting,Vector3.zero,exitArrived:true,isEnabled:false));
            service.Tick(0); Assert.That(service.Snapshot.CollectedExitVisitIds,Is.Empty);
        }
        [Test] public void NoPickupReservationMeansWaitWithoutChargeOrWork()
        {
            env.ReserveAllowed=false; service.Tick(.1f); service.Tick(1);
            Assert.That(service.Snapshot.Orders,Is.Empty); Assert.That(env.StartedTokens,Is.Empty);
            env.ReserveAllowed=true; service.Tick(.1f); Assert.That(env.StartedTokens.Count,Is.EqualTo(1));
        }
        [Test] public void FinishedCashierWaitsForTemporarilyUnavailableReservedPathWithoutRestartOrCharge()
        {
            Start(); var token=env.StartedTokens[Index(EmployeeRole.Cashier)]; env.ReserveAllowed=false; FinishCashier();
            Assert.That(service.Snapshot.Status,Is.Not.EqualTo("Blocked")); Assert.That(service.Snapshot.Orders,Is.Empty);
            Assert.That(service.Snapshot.Employees.Single().Stage,Is.EqualTo(CafeWorkStage.Finished));
            for(var i=0;i<3;i++) service.Tick(.1f);
            Assert.That(env.StartedCheckpoints.Count(c=>c.Role==EmployeeRole.Cashier),Is.EqualTo(1));
            Assert.That(env.ReservedVisits,Does.Contain("a")); Assert.That(service.Snapshot.PaidVisitIds,Is.Empty);
            env.ReserveAllowed=true; service.Tick(0); Assert.That(service.Snapshot.Orders,Is.Empty);
            service.Tick(.1f); Assert.That(service.Snapshot.Orders.Count,Is.EqualTo(1));
            Assert.That(env.StartedTokens[Index(EmployeeRole.Cashier)],Is.EqualTo(token));
            Assert.That(env.PickupMoves.Count(id=>id=="a"),Is.EqualTo(1));
        }
        [Test] public void FinishedProgressAloneCannotCreateAndPendingSubmissionWaitsForEnabledQueuedVisit()
        {
            Start(); service.ReportTask(Event(EmployeeRole.Cashier,CafeTaskEventKind.Progress,CafeWorkStage.Finished));
            service.Tick(.1f); Assert.That(service.Snapshot.Orders,Is.Empty,"Finished checkpoint is not a service completion receipt");
            env.ReserveAllowed=false; FinishCashier(); env.ReserveAllowed=true;
            env.VisitValues[0]=new CafeVisitSnapshot("a",CustomerVisitState.Blocked,Vector3.zero,counterArrived:true,isEnabled:false,layoutRevision:7);
            service.Tick(.1f); Assert.That(service.Snapshot.Orders,Is.Empty);
            env.VisitValues[0]=new CafeVisitSnapshot("a",CustomerVisitState.Blocked,Vector3.zero,counterArrived:true,isEnabled:true,layoutRevision:7);
            service.Tick(.1f); Assert.That(service.Snapshot.Orders,Is.Empty,"Reenabled but Blocked is not a current queue-head arrival");
            env.VisitValues[0]=new CafeVisitSnapshot("a",CustomerVisitState.Queued,Vector3.zero,counterArrived:true,layoutRevision:7);
            service.Tick(.1f); Assert.That(service.Snapshot.Orders.Count,Is.EqualTo(1));
            Assert.That(env.StartedCheckpoints.Count(c=>c.Role==EmployeeRole.Cashier),Is.EqualTo(1));
        }

        [Test] public void ReturnedEventCannotInventSuccessfulDeliveryOrReleaseBarista()
        {
            AddVisit("b"); Start(); FinishCashier(); FinishCashier();
            service.ReportTask(Event(EmployeeRole.Barista,CafeTaskEventKind.Returned,CafeWorkStage.Finished,generation:3,request:71));
            env.IdleRoles.Add(EmployeeRole.Barista); service.Tick(.1f);
            Assert.That(service.Snapshot.Orders.Values.Single(o=>o.CustomerId=="a").State,Is.EqualTo(OrderState.Preparing));
            Assert.That(service.Snapshot.Orders.Values.Single(o=>o.CustomerId=="b").State,Is.EqualTo(OrderState.Waiting));
            Assert.That(service.Snapshot.Delivery,Is.Empty);
        }
        [Test] public void DeliveryRequiresRegisteredExactArrivalAndValidSlotBeforeReady()
        {
            Start(); FinishCashier();
            var arrival=Event(EmployeeRole.Barista,CafeTaskEventKind.DeliveryArrived,CafeWorkStage.Prepared,generation:3,request:71);
            Assert.That(service.TryCommitDelivery(arrival,Slot,out _),Is.False,"Query true alone cannot register an event");
            service.ReportTask(arrival);
            Assert.That(service.TryCommitDelivery(arrival,default,out _),Is.False);
            var wrong=new CafeTaskEvent(arrival.Token,arrival.Role,arrival.ActorId,arrival.HandleId+1,arrival.RequestId,
                arrival.Generation,arrival.LayoutRevision,arrival.Kind,arrival.Checkpoint);
            Assert.That(service.TryCommitDelivery(wrong,Slot,out _),Is.False);
            wrong=new CafeTaskEvent(arrival.Token,arrival.Role,arrival.ActorId,arrival.HandleId,arrival.RequestId,
                arrival.Generation-1,arrival.LayoutRevision,arrival.Kind,arrival.Checkpoint);
            Assert.That(service.TryCommitDelivery(wrong,Slot,out _),Is.False);
            Assert.That(service.Snapshot.Orders.Single().Value.State,Is.EqualTo(OrderState.Preparing));
            Assert.That(service.TryCommitDelivery(arrival,Slot,out var reason),Is.True,reason);
            Assert.That(service.TryCommitDelivery(arrival,Slot,out _),Is.False);
            Assert.That(service.Snapshot.Delivery.Count,Is.EqualTo(1));
        }
        [TestCase(false)] [TestCase(true)]
        public void DeliveryQueryReentryCannotCommitAgainstChangedGateOrEpoch(bool clear)
        {
            service.Dispose(); var probe=new QueryProbe(env); service=new CafeLoopService("session",probe);
            Start(); FinishCashier(); var arrival=Arrival();
            probe.OnQuery=()=>{if(clear) service.ClearOrdersForDecoration(1); else env.Gate=false;};
            Assert.That(service.TryCommitDelivery(arrival,Slot,out _),Is.False);
            Assert.That(service.Snapshot.Delivery,Is.Empty);
            if(clear) Assert.That(service.Snapshot.Orders,Is.Empty);
            else Assert.That(service.Snapshot.Orders.Single().Value.State,Is.EqualTo(OrderState.Preparing));
        }
        [Test] public void CollectionQueryReentryCannotCompleteAnOldEpoch()
        {
            service.Dispose(); var probe=new QueryProbe(env); service=new CafeLoopService("session",probe);
            Start(); FinishCashier(); AtPickup("a"); Deliver(); env.CollectionAllowed=true;
            probe.OnQuery=()=>service.ClearOrdersForDecoration(1);
            Assert.That(service.TryCollect("a",out _),Is.False);
            Assert.That(service.Snapshot.Orders,Is.Empty); Assert.That(service.Snapshot.CollectedExitVisitIds,Is.Empty);
        }
        [Test] public void ReplayedPreparedCannotRegressReturningCheckpointBeforeExplicitRestore()
        {
            Start(); FinishCashier(); AtPickup("a"); Deliver();
            service.ReportTask(Event(EmployeeRole.Barista,CafeTaskEventKind.Progress,CafeWorkStage.Returning,generation:4,request:72));
            service.ReportTask(Event(EmployeeRole.Barista,CafeTaskEventKind.Blocked,CafeWorkStage.Returning,generation:4,request:72));
            service.ReportTask(Event(EmployeeRole.Barista,CafeTaskEventKind.Prepared,CafeWorkStage.Prepared,generation:4,request:72));
            Assert.That(service.Snapshot.Employees.Single(c=>c.Role==EmployeeRole.Barista).Stage,Is.EqualTo(CafeWorkStage.Returning));
            env.IdleRoles.Add(EmployeeRole.Barista); service.ResumeBlocked();
            Assert.That(env.StartedCheckpoints[Index(EmployeeRole.Barista)].Stage,Is.EqualTo(CafeWorkStage.Returning));
            Assert.That(env.StartedCheckpoints[Index(EmployeeRole.Barista)].RemainingWorkSeconds,Is.Zero);
            Assert.That(service.Snapshot.Delivery.Count,Is.EqualTo(1));
            Assert.That(service.Snapshot.Orders.Single().Value.State,Is.EqualTo(OrderState.ReadyForPickup));
        }
        [Test] public void NewLayoutBlockedPreservesCheckpointButUnexpectedSuccessRevisionIsRejected()
        {
            Start(); var progress=Event(EmployeeRole.Cashier,CafeTaskEventKind.Progress,CafeWorkStage.Working,.75f);
            service.ReportTask(progress);
            var unexpected=new CafeTaskEvent(progress.Token,progress.Role,progress.ActorId,progress.HandleId,0,
                progress.Generation,8,CafeTaskEventKind.ServiceFinished,
                new CafeWorkCheckpoint(EmployeeRole.Cashier,"a",null,CafeWorkStage.Finished,0));
            service.ReportTask(unexpected); service.Tick(.1f);
            Assert.That(service.Snapshot.Orders,Is.Empty,"A success event cannot silently advance layout identity");
            var blocked=new CafeTaskEvent(progress.Token,progress.Role,progress.ActorId,progress.HandleId,0,
                progress.Generation+1,8,CafeTaskEventKind.Blocked,progress.Checkpoint,"layout invalidated");
            service.ReportTask(blocked); env.IdleRoles.Add(EmployeeRole.Cashier); service.ResumeBlocked();
            var restored=env.StartedTokens[Index(EmployeeRole.Cashier)];
            Assert.That(restored.Attempt,Is.GreaterThan(progress.Token.Attempt));
            Assert.That(env.StartedCheckpoints[Index(EmployeeRole.Cashier)].RemainingWorkSeconds,Is.EqualTo(.75f));
            var count=env.StartedTokens.Count; service.ReportTask(blocked); service.ResumeBlocked();
            Assert.That(env.StartedTokens.Count,Is.EqualTo(count),"A retired attempt's old revision cannot block the restored task");
        }
        [Test] public void SynchronousBaristaPauseDefersPreparationUntilNextPositiveAllowedTick()
        {
            Start();
            env.OnStart=(token,checkpoint)=>{if(checkpoint.Role==EmployeeRole.Barista) env.Gate=false;};
            FinishCashier();
            Assert.That(service.Snapshot.Orders.Single().Value.State,Is.EqualTo(OrderState.Claimed));
            var starts=env.StartedTokens.Count;
            env.Gate=true; service.Tick(0);
            Assert.That(service.Snapshot.Orders.Single().Value.State,Is.EqualTo(OrderState.Claimed));
            service.Tick(.1f);
            Assert.That(service.Snapshot.Orders.Single().Value.State,Is.EqualTo(OrderState.Preparing));
            Assert.That(env.StartedTokens.Count,Is.EqualTo(starts),"Only consume the accepted task fact; do not dispatch/claim again");
        }
        [Test] public void RejectedRestoredAttemptCannotInheritOuterStartAcceptance()
        {
            CafeTaskToken original=default;
            env.OnStart=(token,checkpoint)=>
            {
                original=token;
                var saved=new CafeWorkCheckpoint(EmployeeRole.Cashier,"a",null,CafeWorkStage.Working,.5f);
                service.ReportTask(new CafeTaskEvent(token,EmployeeRole.Cashier,"cashier-real",11,0,1,7,
                    CafeTaskEventKind.Progress,saved));
                service.ReportTask(new CafeTaskEvent(token,EmployeeRole.Cashier,"cashier-real",11,0,1,7,
                    CafeTaskEventKind.Blocked,saved,"held during start"));
                env.OnStart=null; env.StartAllowed=false;
                service.ResumeBlocked(); // New attempt is rejected before outer TryStartWork returns true.
            };
            Start();
            var rejected=new CafeTaskToken(original.SessionId,original.Epoch,original.Sequence,original.Attempt+1);
            service.ReportTask(new CafeTaskEvent(rejected,EmployeeRole.Cashier,"cashier-real",12,0,1,7,
                CafeTaskEventKind.ServiceFinished,new CafeWorkCheckpoint(EmployeeRole.Cashier,"a",null,CafeWorkStage.Finished,0)));
            env.StartAllowed=true; env.IdleRoles.Add(EmployeeRole.Cashier); service.ResumeBlocked();
            var restored=env.StartedCheckpoints[Index(EmployeeRole.Cashier)];
            Assert.That(restored.Stage,Is.EqualTo(CafeWorkStage.Working),"Rejected attempt must not inherit the outer dispatch's acceptance");
            Assert.That(restored.RemainingWorkSeconds,Is.EqualTo(.5f));
            Assert.That(service.Snapshot.Orders,Is.Empty);
        }
        private sealed class QueryProbe : ICafeLoopEnvironment
        {
            private readonly CafeLoopEnvironmentFake inner;
            internal Action OnQuery;
            internal QueryProbe(CafeLoopEnvironmentFake inner) { this.inner=inner; }
            public bool CanProgress=>inner.CanProgress;
            public System.Collections.Generic.IReadOnlyList<CafeVisitSnapshot> Visits=>inner.Visits;
            public bool IsEmployeeIdle(EmployeeRole role)=>inner.IsEmployeeIdle(role);
            public string EmployeeId(EmployeeRole role)=>inner.EmployeeId(role);
            public bool TryReservePickup(string id,out string reason)=>inner.TryReservePickup(id,out reason);
            public void ReleasePickupAssignment(string id)=>inner.ReleasePickupAssignment(id);
            public bool TryMoveToPickup(string id,out string reason)=>inner.TryMoveToPickup(id,out reason);
            public bool TryMoveToExit(string id,bool collected,out string reason)=>inner.TryMoveToExit(id,collected,out reason);
            public bool TryStartWork(CafeTaskToken token,CafeWorkCheckpoint checkpoint,out string reason)=>inner.TryStartWork(token,checkpoint,out reason);
            public bool IsCurrentDeliveryArrival(CafeTaskEvent arrival,SurfaceSlotAddress address)
            { OnQuery?.Invoke(); return inner.IsCurrentDeliveryArrival(arrival,address); }
            public bool CanCollect(string id,CafeOrderKey key) { OnQuery?.Invoke(); return inner.CanCollect(id,key); }
            public void ProjectDeliveries(System.Collections.Generic.IReadOnlyList<CafeDeliverySnapshot> deliveries)=>inner.ProjectDeliveries(deliveries);
        }
    }
}

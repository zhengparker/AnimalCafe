using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using AnimalCafe.Customers;

namespace AnimalCafe.Tests.EditMode.Phase12
{
    public class CounterQueueTests
    {
        private static void RestartOnlyBlockedCounterOwner(CounterQueueService queue,string[] ids)
        {
            var method=typeof(CounterQueueService).GetMethod("RestartAssignments",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,
                null,new[]{typeof(System.Collections.Generic.IReadOnlyCollection<string>)},null);
            Assert.That(method,Is.Not.Null,"FEATURE: integrated recovery must not invalidate healthy Counter ownership by globally restarting the queue");
            method.Invoke(queue,new object[]{ids});
        }
        [Test]
        public void TargetedBlockedRestartInvalidatesPreviewButPreservesHealthyMovingClaim()
        {
            var queue=new CounterQueueService(new[]{Vector3.zero,Vector3.forward*3,Vector3.forward*6});
            foreach(var id in new[]{"retry","healthy"}){Assert.That(queue.TryJoin(id),Is.True);Assert.That(queue.Complete(id,1),Is.True);}
            queue.UpdateSlots(new[]{Vector3.right,Vector3.right+Vector3.forward*3,Vector3.right+Vector3.forward*6});
            Assert.That(queue.TryGetNextAdvance(out var retry),Is.True);Assert.That(retry.VisitId,Is.EqualTo("retry"));
            Assert.That(queue.TryGetNextAdvance(out var healthy),Is.True);Assert.That(healthy.VisitId,Is.EqualTo("healthy"));
            Assert.That(queue.TryCaptureAssignment("retry",out var retryClaim),Is.True);Assert.That(queue.TryCaptureAssignment("healthy",out var healthyClaim),Is.True);
            Assert.That(queue.TryPrepareMutation(CounterQueueService.MutationKind.Join,"future",queue.Version,out var preview),Is.True);
            var version=queue.Version;var before=queue.Snapshot.ToArray();
            RestartOnlyBlockedCounterOwner(queue,new[]{"retry"});
            Assert.That(queue.Version,Is.GreaterThan(version));Assert.That(preview.IsCurrent,Is.False,"Canonical Version still rejects every stale uncommitted preview");
            Assert.That(queue.Snapshot.Single(row=>row.VisitId=="healthy"),Is.EqualTo(healthy));
            Assert.That(queue.TryCaptureAssignment("healthy",out var freshHealthy),Is.True);
            Assert.That(healthyClaim.IsSameCurrentClaim(freshHealthy),Is.True,"Healthy owned Counter checkpoint keeps its exact Moving/NeedsReplan semantics");
            Assert.That(queue.TryCaptureAssignment("retry",out var freshRetry),Is.True);Assert.That(retryClaim.IsSameCurrentClaim(freshRetry),Is.False);
            CollectionAssert.AreEqual(before,queue.Snapshot,"A restart changes no actual target, generation or arrival label before the real next advance");
            Assert.That(queue.TryGetNextAdvance(out var resumed),Is.True);Assert.That(resumed.VisitId,Is.EqualTo("retry"));
            Assert.That(resumed.Generation,Is.GreaterThan(retry.Generation));Assert.That(resumed.Position,Is.EqualTo(retry.Position));
            Assert.That(queue.Complete("healthy",healthy.Generation),Is.True,"The unchanged healthy actual in-flight arrival is still accepted");
            Assert.That(queue.Complete("retry",retry.Generation),Is.False);Assert.That(queue.Complete("retry",resumed.Generation),Is.True);
            Assert.That(queue.Snapshot.All(row=>row.Arrived),Is.True);
        }
        [TestCase(false)][TestCase(true)]
        public void TargetedRestartWithoutBlockedCounterOwnerDoesNotTouchQueueOrClaims(bool unknown)
        {
            var queue=new CounterQueueService(new[]{Vector3.zero,Vector3.forward*3});
            Assert.That(queue.TryJoin("healthy"),Is.True);Assert.That(queue.Complete("healthy",1),Is.True);
            Assert.That(queue.TryCaptureAssignment("healthy",out var original),Is.True);
            var version=queue.Version;var before=queue.Snapshot.ToArray();
            RestartOnlyBlockedCounterOwner(queue,unknown?new[]{"absent"}:Array.Empty<string>());
            Assert.That(queue.Version,Is.EqualTo(version));CollectionAssert.AreEqual(before,queue.Snapshot);
            Assert.That(queue.TryCaptureAssignment("healthy",out var after),Is.True);Assert.That(original.IsSameCurrentClaim(after),Is.True);
        }
        [Test] public void CounterReservationLineageSurvivesActualAdvanceAndArrival()
        {
            var queue=CounterWithAdvanceCandidate();
            Assert.That(queue.TryCaptureAssignment("next",out var original),Is.True);
            var oldAssignment=original.Assignment;
            Assert.That(queue.TryGetNextAdvance(out var advanced),Is.True);
            Assert.That(advanced.Generation,Is.GreaterThan(oldAssignment.Generation));
            Assert.That(queue.Complete("next",advanced.Generation),Is.True);
            Assert.That(queue.Snapshot.Single().Arrived,Is.True,"FIXTURE: real canonical advance and arrival completed");
            Assert.That(queue.TryCaptureAssignment("next",out var current),Is.True);
            Assert.That(original.IsCurrent,Is.False);Assert.That(current.IsCurrent,Is.True);
            var before=queue.Snapshot.ToArray();var version=queue.Version;
            Assert.That(original.IsSameReservation(current),Is.True,"One real Entry keeps lineage across a changed assignment generation");
            Assert.That(current.IsSameReservation(original),Is.True);
            Assert.That(original.Assignment,Is.EqualTo(oldAssignment),"Captured assignment remains immutable");
            CollectionAssert.AreEqual(before,queue.Snapshot);Assert.That(queue.Version,Is.EqualTo(version));
        }

        [Test] public void CounterReservationLineageSurvivesUnrelatedMetadataWithoutWrites()
        {
            var queue=new CounterQueueService(new[]{Vector3.zero,Vector3.forward*3});
            Assert.That(queue.TryJoin("own"),Is.True);Assert.That(queue.Complete("own",1),Is.True);
            Assert.That(queue.TryCaptureAssignment("own",out var original),Is.True);
            var priorVersion=queue.Version;queue.UpdateSlots(new[]{Vector3.zero,Vector3.forward*4});
            Assert.That(queue.Version,Is.GreaterThan(priorVersion));Assert.That(original.IsCurrent,Is.True);
            Assert.That(queue.TryCaptureAssignment("own",out var current),Is.True);
            var before=queue.Snapshot.ToArray();var version=queue.Version;
            Assert.That(original.IsSameReservation(current),Is.True);
            Assert.That(current.IsSameReservation(original),Is.True);
            Assert.That(original.IsSameReservation(null),Is.False);
            CollectionAssert.AreEqual(before,queue.Snapshot);Assert.That(queue.Version,Is.EqualTo(version));
        }

        [TestCase("remove-rejoin")] [TestCase("rollback")] [TestCase("foreign")]
        public void CounterReservationLineageRejectsReplacedOrForeignEntry(string replacement)
        {
            var queue=replacement=="rollback"?CounterWithAdvanceCandidate():new CounterQueueService(new[]{Vector3.zero});
            var id=replacement=="rollback"?"next":"same";
            if(replacement!="rollback")Assert.That(queue.TryJoin(id),Is.True);
            Assert.That(queue.TryCaptureAssignment(id,out var original),Is.True);
            var before=queue.Snapshot.ToArray();var target=queue;
            if(replacement=="remove-rejoin")
            {
                Assert.That(queue.Remove(id),Is.True);Assert.That(queue.TryJoin(id),Is.True);
                CollectionAssert.AreEqual(before,queue.Snapshot,"FIXTURE: remove/rejoin restores identical public assignment values");
            }
            else if(replacement=="rollback")
            {
                Assert.That(queue.TryPrepareMutation(CounterQueueService.MutationKind.Advance,id,queue.Version,out var prepared),Is.True);
                Assert.That(prepared.TryApply(out var receipt),Is.True);receipt.RollbackOwnMutation();
                CollectionAssert.AreEqual(before,queue.Snapshot,"FIXTURE: actual rollback restores values with a replacement Entry");
            }
            else { target=new CounterQueueService(new[]{Vector3.zero});Assert.That(target.TryJoin(id),Is.True); }
            Assert.That(target.TryCaptureAssignment(id,out var current),Is.True);Assert.That(current.IsCurrent,Is.True);
            if(replacement!="foreign")Assert.That(original.IsCurrent,Is.False);
            var version=queue.Version;var targetVersion=target.Version;
            Assert.That(original.IsSameReservation(current),Is.False,"Equal id/assignment values do not authorize another canonical Entry");
            Assert.That(current.IsSameReservation(original),Is.False);
            Assert.That(current.IsSameReservation(current),Is.True,"Control: the actual current claim must recognize its own Entry");
            Assert.That(queue.Version,Is.EqualTo(version));Assert.That(target.Version,Is.EqualTo(targetVersion));
        }

        [Test] public void CounterAssignmentClaimIsReadOnlyAndSurvivesUnrelatedTailMetadata()
        {
            var queue=new CounterQueueService(new[]{Vector3.zero,Vector3.forward*3});Assert.That(queue.TryJoin("own"),Is.True);
            var before=queue.Snapshot.ToArray();var version=queue.Version;
            Assert.That(queue.TryCaptureAssignment("own",out var claim),Is.True);Assert.That(claim.IsCurrent,Is.True);
            Assert.That(claim.Assignment,Is.EqualTo(before.Single()));CollectionAssert.AreEqual(before,queue.Snapshot);Assert.That(queue.Version,Is.EqualTo(version));
            queue.UpdateSlots(new[]{Vector3.zero,Vector3.forward*4});Assert.That(queue.Version,Is.GreaterThan(version));
            Assert.That(claim.IsCurrent,Is.True,"An unallocated tail change does not replace this exact canonical assignment");
            Assert.That(queue.Complete("own",1),Is.True);Assert.That(claim.IsCurrent,Is.True,"Actual arrival is separate from assignment ownership");
        }
        [Test] public void CounterAssignmentClaimRejectsRemovedAndRejoinedSameValueOwner()
        {
            var queue=new CounterQueueService(new[]{Vector3.zero});Assert.That(queue.TryJoin("same"),Is.True);
            Assert.That(queue.TryCaptureAssignment("same",out var claim),Is.True);Assert.That(claim.IsCurrent,Is.True);
            var before=queue.Snapshot.Single();Assert.That(queue.Remove("same"),Is.True);Assert.That(queue.TryJoin("same"),Is.True);
            Assert.That(queue.Snapshot.Single(),Is.EqualTo(before),"FIXTURE: every public assignment value is identical after replacement");
            Assert.That(claim.IsCurrent,Is.False,"Entry identity, not merely id/generation/position, rejects ABA");
            Assert.That(queue.TryCaptureAssignment("same",out var replacement),Is.True);Assert.That(replacement.IsCurrent,Is.True);
        }
        [Test] public void CounterAssignmentClaimRejectsActualAssignmentGenerationChange()
        {
            var queue=CounterWithAdvanceCandidate();Assert.That(queue.TryCaptureAssignment("next",out var claim),Is.True);
            Assert.That(claim.IsCurrent,Is.True);Assert.That(queue.TryGetNextAdvance(out var advanced),Is.True);
            Assert.That(advanced.Generation,Is.GreaterThan(claim.Assignment.Generation));Assert.That(claim.IsCurrent,Is.False);
        }
        [Test] public void CounterAssignmentClaimSurvivesOtherOwnerRemovalWithoutTouchingEitherLedger()
        {
            var queue=new CounterQueueService(new[]{Vector3.zero,Vector3.forward*3});Assert.That(queue.TryJoin("own"),Is.True);Assert.That(queue.TryJoin("other"),Is.True);
            Assert.That(queue.TryCaptureAssignment("own",out var claim),Is.True);Assert.That(queue.Remove("other"),Is.True);
            var before=queue.Snapshot.ToArray();var version=queue.Version;
            Assert.That(claim.IsCurrent,Is.True);CollectionAssert.AreEqual(before,queue.Snapshot);Assert.That(queue.Version,Is.EqualTo(version));
        }
        [Test] public void CounterAssignmentClaimCannotBeCapturedForMissingOwner()
        {
            var queue=new CounterQueueService(new[]{Vector3.zero});var version=queue.Version;
            Assert.That(queue.TryCaptureAssignment("missing",out var claim),Is.False);Assert.That(claim,Is.Null);
            Assert.That(queue.Snapshot,Is.Empty);Assert.That(queue.Version,Is.EqualTo(version));
        }
        private static CounterQueueService CounterWithAdvanceCandidate()
        {
            var queue=new CounterQueueService(new[]{Vector3.zero,Vector3.forward*3,Vector3.forward*6});
            Assert.That(queue.TryJoin("head"),Is.True);
            Assert.That(queue.TryJoin("next"),Is.True);
            foreach(var assignment in queue.Snapshot) Assert.That(queue.Complete(assignment.VisitId,assignment.Generation),Is.True);
            Assert.That(queue.Remove("head"),Is.True);
            return queue;
        }

        [TestCase("join")] [TestCase("complete")] [TestCase("front-exit")]
        [TestCase("remove")] [TestCase("advance")] [TestCase("slots")] [TestCase("restart")]
        public void CounterCanonicalMutationInvalidatesPreparedVersion(string operation)
        {
            var queue=CounterWithAdvanceCandidate();
            var before=queue.Version;
            switch(operation)
            {
                case "join":
                    queue=new CounterQueueService(new[]{Vector3.zero});before=queue.Version;
                    Assert.That(queue.TryJoin("new"),Is.True);break;
                case "complete":
                    Assert.That(queue.TryGetNextAdvance(out var assignment),Is.True);before=queue.Version;
                    Assert.That(queue.Complete("next",assignment.Generation),Is.True);break;
                case "front-exit": Assert.That(queue.TryBeginFrontExit(out _),Is.True);break;
                case "remove": Assert.That(queue.Remove("next"),Is.True);break;
                case "advance": Assert.That(queue.TryGetNextAdvance(out _),Is.True);break;
                case "slots": queue.UpdateSlots(new[]{Vector3.right*4,Vector3.right*4+Vector3.forward*3});break;
                case "restart": queue.RestartAssignments();break;
            }
            Assert.That(queue.Version,Is.GreaterThan(before),"Canonical changes must invalidate captured permissions.");
        }

        [Test] public void CounterRejectedAndRepeatedArrivalDoNotInvalidateVersion()
        {
            var queue=CounterWithAdvanceCandidate();var before=queue.Version;
            Assert.That(queue.TryJoin("next"),Is.False);
            Assert.That(queue.Remove("missing"),Is.False);
            Assert.That(queue.Complete("next",99),Is.False);
            Assert.That(queue.TryGetNextAdvance(out _,candidate=>false),Is.False);
            Assert.That(queue.Complete("next",1),Is.True,"Repeated actual arrival remains an idempotent acknowledgement.");
            Assert.That(queue.Version,Is.EqualTo(before));
        }

        [TestCase("remove")] [TestCase("replace-owner")] [TestCase("slots")] [TestCase("restart")]
        public void CounterAdvanceRejectsReentrantCanonicalMutation(string operation)
        {
            var queue=CounterWithAdvanceCandidate();QueueAssignment[] afterCallback=null;
            var accepted=queue.TryGetNextAdvance(out var stale,candidate=>
            {
                switch(operation)
                {
                    case "remove": Assert.That(queue.Remove("next"),Is.True);break;
                    case "replace-owner":
                        Assert.That(queue.Remove("next"),Is.True);Assert.That(queue.TryJoin("next"),Is.True);break;
                    case "slots": queue.UpdateSlots(new[]{Vector3.right*4,Vector3.right*4+Vector3.forward*3});break;
                    case "restart": queue.RestartAssignments();break;
                }
                afterCallback=queue.Snapshot.ToArray();return true;
            });
            Assert.That(accepted,Is.False,"A callback may invalidate the captured Entry even when it returns true.");
            Assert.That(stale.VisitId,Is.Null);
            CollectionAssert.AreEqual(afterCallback,queue.Snapshot,"Rejecting the stale candidate must preserve the callback's own changes.");
        }

        [TestCase(CounterQueueService.MutationKind.Join)] [TestCase(CounterQueueService.MutationKind.Advance)]
        public void CounterPreparationIsReadOnlyAndNamesExactCandidate(CounterQueueService.MutationKind kind)
        {
            var queue=kind==CounterQueueService.MutationKind.Join
                ?new CounterQueueService(new[]{Vector3.zero,Vector3.forward*3}):CounterWithAdvanceCandidate();
            var id=kind==CounterQueueService.MutationKind.Join?"new":"next";
            var before=queue.Snapshot.ToArray();var version=queue.Version;
            Assert.That(queue.TryPrepareMutation(kind,id,version,out var prepared),Is.True);
            Assert.That(prepared.IsCurrent,Is.True);
            Assert.That(prepared.Assignment.VisitId,Is.EqualTo(id));
            Assert.That(prepared.Assignment.SlotIndex,Is.Zero);
            Assert.That(prepared.Assignment.Position,Is.EqualTo(Vector3.zero));
            Assert.That(prepared.Assignment.Generation,Is.EqualTo(kind==CounterQueueService.MutationKind.Join?1:2));
            Assert.That(prepared.Assignment.Arrived,Is.False);
            CollectionAssert.AreEqual(before,queue.Snapshot);
            Assert.That(queue.Version,Is.EqualTo(version));
        }

        [Test] public void CounterPreparationRejectsForeignAdvanceOwnerAndStaleVersion()
        {
            var queue=CounterWithAdvanceCandidate();var version=queue.Version;
            Assert.That(queue.TryPrepareMutation(CounterQueueService.MutationKind.Advance,"next",version,out var valid),Is.True);
            Assert.That(queue.TryPrepareMutation(CounterQueueService.MutationKind.Advance,"other",version,out _),Is.False);
            Assert.That(queue.TryPrepareMutation(CounterQueueService.MutationKind.Join," next ",version,out _),Is.False);
            queue.RestartAssignments();
            Assert.That(queue.TryPrepareMutation(CounterQueueService.MutationKind.Advance,"next",version,out _),Is.False);
            Assert.That(valid.IsCurrent,Is.False);
        }

        [TestCase(CounterQueueService.MutationKind.Join)] [TestCase(CounterQueueService.MutationKind.Advance)]
        public void CounterPreparedApplyCommitsOnceAndRejectsOldArrival(CounterQueueService.MutationKind kind)
        {
            var queue=kind==CounterQueueService.MutationKind.Join
                ?new CounterQueueService(new[]{Vector3.zero,Vector3.forward*3}):CounterWithAdvanceCandidate();
            var id=kind==CounterQueueService.MutationKind.Join?"new":"next";
            Assert.That(queue.TryPrepareMutation(kind,id,queue.Version,out var prepared),Is.True);
            var version=queue.Version;
            Assert.That(prepared.TryApply(out var receipt),Is.True);
            Assert.That(receipt,Is.Not.Null);Assert.That(receipt.IsCurrent,Is.True);
            Assert.That(prepared.IsCurrent,Is.False);
            Assert.That(queue.Version,Is.GreaterThan(version));
            Assert.That(queue.Snapshot.Single(),Is.EqualTo(prepared.Assignment));
            var committedVersion=queue.Version;
            Assert.That(prepared.TryApply(out var duplicate),Is.False);Assert.That(duplicate,Is.Null);
            Assert.That(queue.Version,Is.EqualTo(committedVersion));
            Assert.That(queue.Complete(id,prepared.Assignment.Generation-1),Is.False);
            Assert.That(queue.Complete(id,prepared.Assignment.Generation),Is.True);
            Assert.That(queue.Snapshot.Single().Arrived,Is.True);
        }

        [Test] public void CounterPreparedApplyRejectsForeignQueueWithoutConsumingOwnerProposal()
        {
            var owner=new CounterQueueService(new[]{Vector3.zero});var foreign=new CounterQueueService(new[]{Vector3.right*4});
            Assert.That(owner.TryPrepareMutation(CounterQueueService.MutationKind.Join,"new",owner.Version,out var prepared),Is.True);
            Assert.That(foreign.TryApplyPrepared(prepared,out var refused),Is.False);Assert.That(refused,Is.Null);
            Assert.That(foreign.Snapshot,Is.Empty);Assert.That(owner.Snapshot,Is.Empty);
            Assert.That(prepared.IsCurrent,Is.True);
            Assert.That(owner.TryApplyPrepared(prepared,out var receipt),Is.True);Assert.That(receipt.IsCurrent,Is.True);
            Assert.That(owner.Snapshot.Single().Position,Is.EqualTo(Vector3.zero));
        }

        [Test] public void CounterPreparedApplyRejectsStaleVersionWithoutChangingNewOwner()
        {
            var queue=new CounterQueueService(new[]{Vector3.zero,Vector3.forward*3});
            Assert.That(queue.TryPrepareMutation(CounterQueueService.MutationKind.Join,"old",queue.Version,out var prepared),Is.True);
            Assert.That(queue.TryJoin("new"),Is.True);var current=queue.Snapshot.ToArray();var version=queue.Version;
            Assert.That(prepared.IsCurrent,Is.False);
            Assert.That(prepared.TryApply(out var receipt),Is.False);Assert.That(receipt,Is.Null);
            CollectionAssert.AreEqual(current,queue.Snapshot);Assert.That(queue.Version,Is.EqualTo(version));
        }

        [TestCase(CounterQueueService.MutationKind.Join)] [TestCase(CounterQueueService.MutationKind.Advance)]
        public void CounterReceiptRollsBackOwnMutationOnceWithoutRevivingPreparedVersion(CounterQueueService.MutationKind kind)
        {
            var queue=kind==CounterQueueService.MutationKind.Join
                ?new CounterQueueService(new[]{Vector3.zero,Vector3.forward*3}):CounterWithAdvanceCandidate();
            var id=kind==CounterQueueService.MutationKind.Join?"new":"next";
            var before=queue.Snapshot.ToArray();
            Assert.That(queue.TryPrepareMutation(kind,id,queue.Version,out var prepared),Is.True);
            Assert.That(prepared.TryApply(out var receipt),Is.True);var appliedVersion=queue.Version;
            receipt.RollbackOwnMutation();
            CollectionAssert.AreEqual(before,queue.Snapshot);
            Assert.That(queue.Version,Is.GreaterThan(appliedVersion),"Rollback cannot make an older proposal current again.");
            Assert.That(prepared.IsCurrent,Is.False);Assert.That(receipt.IsCurrent,Is.False);
            var rolledBackVersion=queue.Version;receipt.RollbackOwnMutation();
            Assert.That(queue.Version,Is.EqualTo(rolledBackVersion));CollectionAssert.AreEqual(before,queue.Snapshot);
            Assert.That(prepared.TryApply(out _),Is.False);
        }

        [Test] public void CounterStaleReceiptCannotEraseReplacementOwner()
        {
            var queue=new CounterQueueService(new[]{Vector3.zero,Vector3.forward*3});
            Assert.That(queue.TryPrepareMutation(CounterQueueService.MutationKind.Join,"same",queue.Version,out var prepared),Is.True);
            Assert.That(prepared.TryApply(out var receipt),Is.True);
            Assert.That(queue.Remove("same"),Is.True);Assert.That(queue.TryJoin("same"),Is.True);
            Assert.That(queue.Complete("same",1),Is.True);
            var replacement=queue.Snapshot.ToArray();var version=queue.Version;
            Assert.That(receipt.IsCurrent,Is.False);
            Assert.Throws<InvalidOperationException>(()=>receipt.RollbackOwnMutation());
            CollectionAssert.AreEqual(replacement,queue.Snapshot);Assert.That(queue.Version,Is.EqualTo(version));
        }

        [Test] public void AdmissionCanReserveStableTailWhileEarlierCustomerAdvances()
        {
            var queue=new CounterQueueService(new[]{Vector3.zero,Vector3.forward*3,Vector3.forward*6});
            Assert.That(queue.TryJoin("a"),Is.True); Assert.That(queue.Complete("a",1),Is.True);
            Assert.That(queue.TryJoin("b"),Is.True); Assert.That(queue.Complete("b",1),Is.True);
            Assert.That(queue.Remove("a"),Is.True);
            Assert.That(queue.CanJoin,Is.False,"Unassigned old slots must not publish a duplicate tail");
            Assert.That(queue.TryGetNextAdvance(out var advance),Is.True);
            Assert.That(queue.CanJoin,Is.True,"A stable tail is reservable before the earlier customer arrives");
            Assert.That(queue.TryJoin("c"),Is.True);
            Assert.That(queue.Snapshot[1].Position,Is.EqualTo(Vector3.forward*3));
            Assert.That(queue.Complete("b",1),Is.False,"Admission must not change assignment generations");
            Assert.That(queue.Complete("b",advance.Generation),Is.True);
            Assert.That(queue.Complete("c",1),Is.True);
            CollectionAssert.AreEqual(new[]{"b","c"},queue.Snapshot.Select(a=>a.VisitId).ToArray());
        }

        [Test] public void ExitingHeadKeepsItsSlotUntilRemovedBeforeAdmission()
        {
            var queue=new CounterQueueService(new[]{Vector3.zero,Vector3.forward*3});
            Assert.That(queue.TryJoin("a"),Is.True); Assert.That(queue.Complete("a",1),Is.True);
            Assert.That(queue.TryBeginFrontExit(out _),Is.True);
            Assert.That(queue.TryJoin("b"),Is.False);
            Assert.That(queue.Remove("a"),Is.True);
            Assert.That(queue.TryJoin("b"),Is.True);
            Assert.That(queue.Snapshot.Single().Position,Is.EqualTo(Vector3.zero));
        }

        [Test] public void DefaultSpacingFitsThirdCustomerBehindSecondAtSafeFloorEdge()
        {
            var slots=new CounterQueuePlanner().Build(new Vector3(2.5f,0,2.5f),Vector3.back,3,
                (p,previous)=>p.x>=.5f && p.z>=.5f);
            CollectionAssert.AreEqual(new[]{new Vector3(2.5f,0,2.5f),new Vector3(2.5f,0,1.5f),
                new Vector3(2.5f,0,.5f)},slots,"Third customer should use the safe cell behind the second before turning");
        }
        [Test] public void StraightQueueStartsAtHeadWithSafeSpacing()
        {
            var slots = new CounterQueuePlanner(1.1f).Build(Vector3.zero, Vector3.forward, 3, (p, previous) => true);
            Assert.That(slots.Count, Is.EqualTo(3));
            Assert.That(slots[0], Is.EqualTo(Vector3.zero));
            Assert.That(slots[2], Is.EqualTo(Vector3.forward * 2.2f));
        }
        [Test] public void ObstacleChoosesLeftBeforeRightAndStopsWhenNoSpace()
        {
            var slots = new CounterQueuePlanner(1.1f).Build(Vector3.zero, Vector3.forward, 3,
                (p, previous) => p.z >= 0 && p.z < 1 && p.x >= -1.3f && p.x <= 1.3f);
            Assert.That(slots.Count, Is.EqualTo(2));
            Assert.That(slots[1].x,Is.LessThanOrEqualTo(-1.1f),"Equal length edge-aligned branches still prefer left");
            Assert.That(slots[1].x,Is.GreaterThanOrEqualTo(-1.3f));
            Assert.That(Mathf.Abs(slots[1].z),Is.LessThan(.001f));
        }
        [Test] public void SlotsNeverLoopOrCrowdNonAdjacentSlots()
        {
            var slots = new CounterQueuePlanner(1.1f).Build(Vector3.zero, Vector3.forward, 100,
                (p, previous) => Mathf.Abs(p.x) <= 2 && Mathf.Abs(p.z) <= 2);
            Assert.That(slots.Count, Is.GreaterThanOrEqualTo(2));
            for (var i = 0; i < slots.Count; i++)
                for (var j = 0; j < i; j++) Assert.That(Vector3.Distance(slots[i], slots[j]), Is.GreaterThanOrEqualTo(1.099f));
            Assert.That(slots.Count, Is.LessThan(100));
        }
        [Test] public void RotatedHeadRetainsRelativeLeftPriority()
        {
            var rotation = Quaternion.Euler(0, 90, 0);
            var slots = new CounterQueuePlanner(1.1f).Build(Vector3.zero, rotation * Vector3.forward, 2,
                (p, previous) => !previous.HasValue || Vector3.Distance(p, rotation * Vector3.forward * 1.1f) > .1f);
            Assert.That(slots.Count, Is.EqualTo(2));
            Assert.That(Vector3.Distance(slots[1], rotation * Vector3.left * 1.1f), Is.LessThan(.001f));
        }
        [Test] public void InvalidSpacingOrDirectionIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CounterQueuePlanner(.999f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CounterQueuePlanner(1.1f).Build(Vector3.zero, Vector3.zero, 2, (p, q) => true));
        }
        [TestCase(0)] [TestCase(90)] [TestCase(180)] [TestCase(270)]
        public void BoundaryDeadEndRetriesTheOtherTurnWithoutCrowding(int yaw)
        {
            var rotation=Quaternion.Euler(0,yaw,0);
            var cells=new[]{Vector3.zero,Vector3.forward,Vector3.forward+Vector3.left,
                Vector3.forward+Vector3.right,Vector3.forward+Vector3.right*2,
                Vector3.forward*2+Vector3.right*2,Vector3.forward*3+Vector3.right*2};
            var allowed=cells.Select(p=>rotation*(p*1.1f)).ToArray();
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,rotation*Vector3.forward,6,
                (p,previous)=>allowed.Any(a=>Vector3.Distance(a,p)<.001f));
            Assert.That(slots.Count,Is.EqualTo(6),"The first left branch stops at three; the existing right branch has six safe slots");
            Assert.That(Vector3.Distance(slots[2],rotation*((Vector3.forward+Vector3.right)*1.1f)),Is.LessThan(.001f));
            for(var i=1;i<slots.Count;i++)
            {
                Assert.That(Vector3.Distance(slots[i-1],slots[i]),Is.EqualTo(1.1f).Within(.001f));
                for(var j=0;j<i;j++) Assert.That(Vector3.Distance(slots[j],slots[i]),Is.GreaterThanOrEqualTo(1.099f));
            }
        }
        [Test] public void BoundarySearchIsFiniteAndDeterministicWhenCapacityCannotFit()
        {
            var checks=0;
            Func<Vector3,Vector3?,bool> legal=(p,previous)=>
            { checks++; return Mathf.Abs(p.x)<=2.21f && Mathf.Abs(p.z)<=2.21f; };
            var planner=new CounterQueuePlanner(1.1f);
            var first=planner.Build(Vector3.zero,Vector3.forward,100,legal);
            Assert.That(checks,Is.LessThanOrEqualTo(257),"At most 256 candidates plus the head; no unbounded search");
            checks=0;
            CollectionAssert.AreEqual(first,planner.Build(Vector3.zero,Vector3.forward,100,legal));
            Assert.That(checks,Is.LessThanOrEqualTo(257));
            Assert.That(first.Count,Is.LessThan(100));
            Assert.That(first.Count,Is.GreaterThan(1));
        }
        [Test] public void BacktrackingReachabilityUsesOnlyTheCurrentQueuePrefix()
        {
            var left=(Vector3.forward+Vector3.left)*1.1f;
            var right=(Vector3.forward+Vector3.right)*1.1f;
            var allowed=new[]{Vector3.zero,Vector3.forward*1.1f,left,right,
                (Vector3.forward+Vector3.right*2)*1.1f,(Vector3.forward*2+Vector3.right*2)*1.1f};
            var testedLeft=false; var retriedRight=false;
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,Vector3.forward,5,
                (p,previous)=>allowed.Any(a=>Vector3.Distance(a,p)<.001f),(p,prefix)=>
                {
                    Assert.That(prefix.Any(a=>Vector3.Distance(a,p)<.001f),Is.False,"Candidate is not an already occupied slot");
                    if(Vector3.Distance(p,left)<.001f) testedLeft=true;
                    if(testedLeft && Vector3.Distance(p,right)<.001f)
                    {
                        retriedRight=true;
                        Assert.That(prefix.Count,Is.EqualTo(2));
                        Assert.That(prefix.Any(a=>Vector3.Distance(a,left)<.001f),Is.False,"Discarded turn must not remain a virtual actor");
                    }
                    return true;
                });
            Assert.That(testedLeft && retriedRight,Is.True);
            Assert.That(slots.Count,Is.EqualTo(5));
        }
        [Test] public void ForwardAssignmentsCanOverlapAndKeepFifo()
        {
            var queue = new CounterQueueService(new[] { Vector3.zero, Vector3.forward * 1.1f, Vector3.forward * 2.2f });
            Assert.That(queue.TryJoin("a"), Is.True); Assert.That(queue.TryJoin("b"), Is.True); Assert.That(queue.TryJoin("c"), Is.True);
            Assert.That(queue.TryJoin("d"), Is.False);
            Assert.That(queue.TryBeginFrontExit(out _), Is.False);
            foreach (var a in queue.Snapshot) queue.Complete(a.VisitId, a.Generation);
            Assert.That(queue.TryBeginFrontExit(out var front), Is.True); Assert.That(front, Is.EqualTo("a"));
            Assert.That(queue.TryBeginFrontExit(out _), Is.False);
            queue.Remove("a");
            var held=queue.Snapshot.ToArray();
            Assert.That(queue.TryGetNextAdvance(out _,candidate=>false),Is.False,"Conflicting route waits before assignment commit");
            CollectionAssert.AreEqual(held,queue.Snapshot,"Waiting must retain each target and generation");
            Assert.That(queue.TryGetNextAdvance(out var next), Is.True); Assert.That(next.VisitId, Is.EqualTo("b"));
            Assert.That(queue.TryGetNextAdvance(out var follower), Is.True,"Follower assignment must not wait for leader Complete");
            Assert.That(follower.VisitId, Is.EqualTo("c"));
            Assert.That(queue.TryGetNextAdvance(out _), Is.False);
            Assert.That(queue.Complete("b", next.Generation - 1), Is.False);
            Assert.That(queue.Complete("c", follower.Generation), Is.True,"Callbacks belong to each assignment");
            Assert.That(queue.TryBeginFrontExit(out _), Is.False,"Front has not actually arrived");
            Assert.That(queue.Complete("b", next.Generation), Is.True);
            Assert.That(queue.Snapshot[0].VisitId, Is.EqualTo("b"));
        }
        [TestCase(0)] [TestCase(90)] [TestCase(180)] [TestCase(270)]
        public void EdgeRowUsesRemainingSpaceWithUniformMinimumSpacing(int yaw)
        {
            var rotation=Quaternion.Euler(0,yaw,0); var inverse=Quaternion.Inverse(rotation);
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,rotation*Vector3.forward,6,(p,previous)=>
            { var local=inverse*p; return Mathf.Abs(local.x)<.01f && local.z>=-.001f && local.z<=4; });
            Assert.That(slots.Count,Is.EqualTo(4));
            Assert.That(slots[0],Is.EqualTo(Vector3.zero),"Head must remain anchored");
            Assert.That((inverse*slots.Last()).z,Is.GreaterThan(3.97f),"Old fixed spacing leaves the safe 3.3-to-4.0 edge strip unused");
            var gap=Vector3.Distance(slots[0],slots[1]);
            Assert.That(gap,Is.GreaterThanOrEqualTo(1.1f));
            for(var i=1;i<slots.Count;i++) Assert.That(Vector3.Distance(slots[i-1],slots[i]),Is.EqualTo(gap).Within(.001f));
        }
        [Test] public void EdgeAlignmentRejectsUnreachableEndAndRestoresTheOriginalRow()
        {
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,Vector3.forward,6,
                (p,previous)=>Mathf.Abs(p.x)<.01f && p.z>=0 && p.z<=4,(p,prefix)=>p.z<3.6f);
            Assert.That(slots.Count,Is.EqualTo(4));
            for(var i=0;i<slots.Count;i++) Assert.That(Vector3.Distance(slots[i],Vector3.forward*(i*1.1f)),Is.LessThan(.001f));
        }
        [Test] public void EdgeSpaceSmallerThanMinimumCannotMoveHeadOrAddACustomer()
        {
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,Vector3.forward,4,
                (p,previous)=>Mathf.Abs(p.x)<.01f && p.z>=0 && p.z<=.9f);
            CollectionAssert.AreEqual(new[]{Vector3.zero},slots);
        }
        [Test] public void EdgeAdjustmentKeepsShortRowsCompact()
        {
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,Vector3.forward,3,
                (p,previous)=>Mathf.Abs(p.x)<.01f && p.z>=0 && p.z<=2);
            Assert.That(slots.Count,Is.EqualTo(2));
            Assert.That(Vector3.Distance(slots[0],slots[1]),Is.EqualTo(1.1f).Within(.001f),"An edge adjustment over 25% must restore the compact original row");
        }
        [Test] public void FullQueueKeepsItsLastStraightSegmentWithoutAnExtraEdgeExtension()
        {
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,Vector3.forward,4,
                (p,previous)=>Mathf.Abs(p.x)<.01f && p.z>=0 && p.z<=4);
            Assert.That(slots.Count,Is.EqualTo(4));
            Assert.That(slots.Last().z,Is.EqualTo(3.3f).Within(.001f),"Capacity is already filled; do not stretch a segment that has no next turn");
        }
        [Test] public void EdgeSamplingHeightKeepsTheRowOnItsOriginalHorizontalPlane()
        {
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,Vector3.forward,6,
                (p,previous)=>Mathf.Abs(p.x)<.01f && p.z>=0 && p.z<=4,null,
                (from,point)=>point+Vector3.up*.025f);
            Assert.That(slots.Last().z,Is.GreaterThan(3.97f));
            Assert.That(slots.All(p=>Mathf.Abs(p.y)<.001f),Is.True);
        }
        [Test] public void ShrinkingSlotsKeepsPeopleAndAllowsSafeFrontExit()
        {
            var queue = new CounterQueueService(new[] { Vector3.zero, Vector3.forward * 1.1f, Vector3.forward * 2.2f });
            queue.TryJoin("a"); queue.TryJoin("b"); queue.TryJoin("c");
            foreach (var a in queue.Snapshot) queue.Complete(a.VisitId, a.Generation);
            queue.UpdateSlots(new[] { Vector3.zero, Vector3.forward * 1.1f });
            Assert.That(queue.Snapshot.Count, Is.EqualTo(3)); Assert.That(queue.CanJoin, Is.False);
            Assert.That(queue.TryBeginFrontExit(out var front), Is.True); Assert.That(front, Is.EqualTo("a"));
        }
        [Test] public void RestartConcurrentAssignmentsRejectsEachOldGeneration()
        {
            var queue=new CounterQueueService(new[]{Vector3.zero,Vector3.forward*1.1f});
            queue.TryJoin("a"); queue.TryJoin("b");
            foreach(var assignment in queue.Snapshot) queue.Complete(assignment.VisitId,assignment.Generation);
            queue.UpdateSlots(new[]{Vector3.right*3,Vector3.right*3+Vector3.forward*1.1f});
            Assert.That(queue.TryGetNextAdvance(out var first),Is.True);
            Assert.That(queue.TryGetNextAdvance(out var second),Is.True);
            Assert.That(queue.CanJoin,Is.False); Assert.That(queue.TryBeginFrontExit(out _),Is.False);
            queue.RestartAssignments();
            Assert.That(queue.TryGetNextAdvance(out var resumedFirst),Is.True);
            Assert.That(queue.TryGetNextAdvance(out var resumedSecond),Is.True);
            Assert.That(queue.Complete("a",first.Generation),Is.False);
            Assert.That(queue.Complete("b",second.Generation),Is.False);
            Assert.That(queue.Complete("b",resumedSecond.Generation),Is.True);
            Assert.That(queue.Complete("a",resumedFirst.Generation),Is.True);
            CollectionAssert.AreEqual(new[]{"a","b"},queue.Snapshot.Select(a=>a.VisitId).ToArray());
        }
    }
}

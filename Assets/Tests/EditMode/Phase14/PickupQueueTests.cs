using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Customers;
using NUnit.Framework;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase14
{
    public class PickupQueueTests
    {
        private static readonly Vector3 Service = new Vector3(0, 0, -2);
        private static PickupQueueService Queue(bool shared = true, int count = 5)
            => new PickupQueueService(Enumerable.Range(0, count)
                .Select(i => new Vector3(0, 0, i * 1.1f)).ToArray(), Service, shared);

        private static QueueAssignment Paid(PickupQueueService queue, string id)
        {
            Assert.That(queue.TryReserve(id, out _), Is.True);
            Assert.That(queue.TryBeginTransfer(id, out var assignment), Is.True);
            return assignment;
        }

        [Test] public void OnlyPaidHeadCanCollectOwnOrder()
        {
            var queue = Queue();
            Assert.That(queue.TryReserve("unpaid", out var unpaid), Is.True);
            var first = Paid(queue, "first");
            var second = Paid(queue, "second");
            Assert.That(queue.MarkArrived("first", first.Generation), Is.True);
            Assert.That(queue.MarkArrived("second", second.Generation), Is.True);
            Assert.That(queue.MarkArrived("unpaid", unpaid.Generation), Is.False);
            Assert.That(queue.TryAcquireCollection("unpaid"), Is.False);
            Assert.That(queue.TryAcquireCollection("wrong-visit"), Is.False);
            Assert.That(queue.TryAcquireCollection("second"), Is.False);
            // Order ownership / Ready由调用层验证；本层只保证paid FIFO与实际到位。
            Assert.That(queue.TryAcquireCollection("first"), Is.True);
            CollectionAssert.AreEqual(new[] { "first", "second" }, queue.Snapshot.Select(a => a.VisitId));
        }

        [Test] public void LastWaitSlotHasOneOwner()
        {
            var queue = Queue();
            var assignments = new List<QueueAssignment>();
            for (var i = 0; i < 5; i++)
            {
                Assert.That(queue.TryReserve("visit-" + i, out var assignment), Is.True);
                assignments.Add(assignment);
            }
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, assignments.Select(a => a.SlotIndex));
            Assert.That(assignments.Select(a => a.Position).Distinct().Count(), Is.EqualTo(5));
            Assert.That(assignments[4].Position, Is.EqualTo(new Vector3(0, 0, 4.4f)));
            Assert.That(queue.TryReserve("sixth", out _), Is.False);
            Assert.That(queue.TryReserve("visit-4", out var repeated), Is.True);
            Assert.That(repeated, Is.EqualTo(assignments[4]));
            Assert.That(queue.TryReserve("sixth", out _), Is.False);
            Assert.That(queue.Snapshot, Is.Empty, "A physical reservation is not a paid FIFO entry");
        }

        [Test] public void SuccessfulTransferOrderWinsOverEarlyReservationOrder()
        {
            var queue = Queue();
            Assert.That(queue.TryReserve("early", out _), Is.True);
            Assert.That(queue.TryReserve("later", out _), Is.True);
            Assert.That(queue.TryBeginTransfer("later", out var later), Is.True);
            Assert.That(queue.TryBeginTransfer("early", out _), Is.True);
            Assert.That(queue.TryBeginTransfer("later", out var repeated), Is.True);
            Assert.That(repeated, Is.EqualTo(later));
            CollectionAssert.AreEqual(new[] { "later", "early" }, queue.Snapshot.Select(a => a.VisitId));
        }

        [Test] public void SharedServicePositionLetsDeliveryFinishBeforeCollection()
        {
            var queue = Queue();
            var first = Paid(queue, "customer");
            Assert.That(queue.MarkArrived("customer", first.Generation), Is.True);
            Assert.That(queue.TryAcquireDelivery("barista"), Is.True);
            Assert.That(queue.TryAcquireDelivery("barista"), Is.True);
            Assert.That(queue.TryAcquireDelivery("other-employee"), Is.False);
            Assert.That(queue.TryAcquireCollection("customer"), Is.False);
            queue.ReleaseServicePosition("wrong-actor");
            Assert.That(queue.TryAcquireCollection("customer"), Is.False,
                "Cup commit or another actor cannot release the employee body lease");
            queue.ReleaseServicePosition("barista"); // caller observed actual departure
            Assert.That(queue.TryAcquireCollection("customer"), Is.True);
            Assert.That(queue.TryAcquireDelivery("barista"), Is.False);
            queue.ReleaseServicePosition("customer");
            Assert.That(queue.TryAcquireDelivery("barista"), Is.True);
        }

        [Test] public void HoldCollectionDoesNotHoldDeliveryLease()
        {
            var queue = Queue();
            for (var i = 0; i < 5; i++)
            {
                var assignment = Paid(queue, "visit-" + i);
                Assert.That(queue.MarkArrived(assignment.VisitId, assignment.Generation), Is.True);
            }
            var held = queue.Snapshot.ToArray();
            // Hold在上层不请求collection lease；到wait位本身不能抢交付点。
            for (var delivery = 0; delivery < 5; delivery++)
            {
                Assert.That(queue.TryAcquireDelivery("barista"), Is.True);
                CollectionAssert.AreEqual(held, queue.Snapshot);
                queue.ReleaseServicePosition("barista");
            }
            Assert.That(queue.TryAcquireCollection("visit-1"), Is.False);
            Assert.That(queue.TryAcquireCollection("visit-0"), Is.True);
        }

        [Test] public void FailedTransferKeepsReservation()
        {
            var queue = Queue(count: 1);
            Assert.That(queue.TryReserve("owner", out var reserved), Is.True);
            Assert.That(queue.TryBeginTransfer("wrong-owner", out _), Is.False);
            Assert.That(queue.TryReserve("other", out _), Is.False);
            Assert.That(queue.TryReserve("owner", out var stillReserved), Is.True);
            Assert.That(stillReserved, Is.EqualTo(reserved));
            Assert.That(queue.Snapshot, Is.Empty);
            Assert.That(queue.TryBeginTransfer("owner", out var paid), Is.True);
            Assert.That(paid.SlotIndex, Is.EqualTo(reserved.SlotIndex));
            Assert.That(paid.Position, Is.EqualTo(reserved.Position));
            Assert.That(queue.TryBeginTransfer("wrong-owner", out _), Is.False);
            Assert.That(queue.Snapshot.Single(), Is.EqualTo(paid));
        }

        [Test] public void CollectionRequiresActualWaitArrivalAndGetsFreshServiceArrival()
        {
            var queue = Queue();
            var first = Paid(queue, "first");
            Assert.That(queue.TryAcquireCollection("first"), Is.False);
            Assert.That(queue.MarkArrived("first", first.Generation + 1), Is.False);
            Assert.That(queue.MarkArrived("first", first.Generation), Is.True);
            Assert.That(queue.TryAcquireCollection("first"), Is.True);
            var service = queue.Snapshot.Single();
            Assert.That(service.Position, Is.EqualTo(Service));
            Assert.That(service.Arrived, Is.False, "Lease grant is not actual service arrival");
            Assert.That(service.Generation, Is.GreaterThan(first.Generation));
            Assert.That(queue.MarkArrived("first", first.Generation), Is.False);
            Assert.That(queue.TryAcquireCollection("first"), Is.True, "Repeated lease request is idempotent");
            Assert.That(queue.Snapshot.Single(), Is.EqualTo(service));
            Assert.That(queue.MarkArrived("first", service.Generation), Is.True);
            Assert.That(queue.Snapshot.Single().Arrived, Is.True);
        }

        [Test] public void OutOfOrderArrivalDoesNotAdvancePaidFollower()
        {
            var queue = Queue();
            var first = Paid(queue, "first");
            var second = Paid(queue, "second");
            Assert.That(queue.MarkArrived("second", first.Generation), Is.False);
            Assert.That(queue.MarkArrived("second", second.Generation), Is.True);
            Assert.That(queue.TryAcquireCollection("second"), Is.False);
            Assert.That(queue.MarkArrived("first", first.Generation), Is.True);
            Assert.That(queue.TryAcquireCollection("first"), Is.True);
            Assert.That(queue.Remove("first"), Is.True);
            Assert.That(queue.TryAcquireCollection("second"), Is.False,
                "Removing the queue entry does not prove the first body left service");
            queue.ReleaseServicePosition("first");
            Assert.That(queue.TryAcquireCollection("second"), Is.True);
        }

        [Test] public void MovingToServiceRetainsTheOwnedWaitSlotUntilRemoval()
        {
            var queue = Queue(count: 1);
            var first = Paid(queue, "first");
            Assert.That(queue.MarkArrived("first", first.Generation), Is.True);
            Assert.That(queue.TryAcquireCollection("first"), Is.True);
            Assert.That(queue.TryReserve("second", out _), Is.False);
            Assert.That(queue.Remove("first"), Is.True);
            Assert.That(queue.TryReserve("second", out var next), Is.True);
            Assert.That(next.Position, Is.EqualTo(Vector3.zero));
            Assert.That(queue.TryAcquireDelivery("barista"), Is.False);
            queue.ReleaseServicePosition("first");
            Assert.That(queue.TryAcquireDelivery("barista"), Is.True);
        }

        [Test] public void SeparateServicePositionsAllowDeliveryDuringCollection()
        {
            var queue = Queue(shared: false);
            var first = Paid(queue, "first");
            Assert.That(queue.MarkArrived("first", first.Generation), Is.True);
            Assert.That(queue.TryAcquireCollection("first"), Is.True);
            Assert.That(queue.TryAcquireDelivery("barista"), Is.True);
            Assert.That(queue.TryAcquireDelivery("other"), Is.False);
            queue.ReleaseServicePosition("barista");
            Assert.That(queue.TryAcquireDelivery("other"), Is.True);
            Assert.That(queue.TryAcquireCollection("first"), Is.True);
        }

        [Test] public void RemoveOnlyReleasesItsOwnReservationAndRejectsOldArrival()
        {
            var queue = Queue(count: 2);
            var first = Paid(queue, "first");
            var second = Paid(queue, "second");
            Assert.That(queue.Remove("unknown"), Is.False);
            Assert.That(queue.Remove("first"), Is.True);
            Assert.That(queue.Remove("first"), Is.False);
            Assert.That(queue.MarkArrived("first", first.Generation), Is.False);
            Assert.That(queue.TryReserve("third", out _), Is.False, "A new tail cannot steal the vacant head slot");
            Assert.That(queue.Snapshot.Single(), Is.EqualTo(second));
            Assert.That(queue.MarkArrived("second", second.Generation), Is.True);
            Assert.That(queue.TryGetNextAdvance(out var advanced), Is.True);
            Assert.That(advanced.VisitId, Is.EqualTo("second"));
            Assert.That(advanced.SlotIndex, Is.EqualTo(0));
            Assert.That(queue.MarkArrived("second", second.Generation), Is.False);
            Assert.That(queue.MarkArrived("second", advanced.Generation), Is.True);
            Assert.That(queue.TryReserve("third", out var third), Is.True);
            Assert.That(third.SlotIndex, Is.EqualTo(1));
            Assert.That(queue.TryReserve("fourth", out _), Is.False);
            Assert.That(queue.Snapshot.Single().VisitId, Is.EqualTo("second"));
            Assert.That(queue.Snapshot.Single().Arrived, Is.True);
        }

        [Test] public void ReusedVisitCannotAcceptRetiredGenerationOrInheritOldServiceLease()
        {
            var queue = Queue(count: 1);
            var old = Paid(queue, "visit");
            Assert.That(queue.MarkArrived("visit", old.Generation), Is.True);
            Assert.That(queue.TryAcquireCollection("visit"), Is.True);
            Assert.That(queue.Remove("visit"), Is.True);
            Assert.That(queue.TryReserve("visit", out _), Is.False);
            queue.ReleaseServicePosition("visit");
            var fresh = Paid(queue, "visit");
            Assert.That(fresh.Generation, Is.GreaterThan(old.Generation));
            Assert.That(queue.MarkArrived("visit", old.Generation), Is.False);
            Assert.That(queue.TryAcquireCollection("visit"), Is.False);
            Assert.That(queue.MarkArrived("visit", fresh.Generation), Is.True);
        }

        [Test] public void SnapshotIsAnImmutablePaidFifoCopy()
        {
            var queue = Queue();
            var first = Paid(queue, "first");
            var snapshot = queue.Snapshot;
            Assert.That(queue.MarkArrived("first", first.Generation), Is.True);
            Paid(queue, "second");
            Assert.That(snapshot.Count, Is.EqualTo(1));
            Assert.That(snapshot[0].Arrived, Is.False);
            Assert.Throws<NotSupportedException>(() => ((IList<QueueAssignment>)snapshot).Clear());
        }

        [TestCase(null)] [TestCase("")] [TestCase(" ")] [TestCase(" visit")] [TestCase("visit ")]
        public void InvalidIdsCannotTakeOrReleaseAnotherActorsSlot(string invalid)
        {
            var queue = Queue(count: 1);
            var first = Paid(queue, "first");
            Assert.That(queue.TryReserve(invalid, out _), Is.False);
            Assert.That(queue.TryBeginTransfer(invalid, out _), Is.False);
            Assert.That(queue.MarkArrived(invalid, first.Generation), Is.False);
            Assert.That(queue.TryAcquireDelivery(invalid), Is.False);
            Assert.That(queue.TryAcquireCollection(invalid), Is.False);
            Assert.That(queue.Remove(invalid), Is.False);
            Assert.That(queue.TryAcquireDelivery("barista"), Is.True);
            queue.ReleaseServicePosition(invalid);
            Assert.That(queue.TryAcquireDelivery("other"), Is.False);
            Assert.That(queue.Snapshot.Single(), Is.EqualTo(first));
        }

        [Test] public void VisitAndEmployeeCannotShareAnActiveLeaseIdentity()
        {
            var queue = Queue();
            Paid(queue, "visit");
            Assert.That(queue.TryAcquireDelivery("visit"), Is.False);
            Assert.That(queue.TryAcquireDelivery("barista"), Is.True);
            Assert.That(queue.TryReserve("barista", out _), Is.False);
        }

        [Test] public void ConstructorCopiesSlotsAndHasNoHiddenServiceCapacity()
        {
            var slots = new[] { Vector3.zero };
            var queue = new PickupQueueService(slots, Service, true);
            slots[0] = Vector3.right * 100;
            Assert.That(queue.TryReserve("first", out var first), Is.True);
            Assert.That(first.Position, Is.EqualTo(Vector3.zero));
            Assert.That(queue.TryReserve("second", out _), Is.False);
            Assert.That(Queue(count: 0).TryReserve("first", out _), Is.False);
        }

        [Test] public void ConstructorRejectsInvalidOrOverlappingWaitingSlots()
        {
            Assert.Throws<ArgumentNullException>(() => new PickupQueueService(null, Service, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupQueueService(
                new[] { new Vector3(float.NaN, 0, 0) }, Service, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PickupQueueService(
                new[] { Vector3.zero }, new Vector3(0, float.PositiveInfinity, 0), true));
            Assert.Throws<ArgumentException>(() => new PickupQueueService(
                new[] { Vector3.zero, Vector3.zero }, Service, true));
            Assert.Throws<ArgumentException>(() => new PickupQueueService(new[] { Service }, Service, true));
        }

        [Test] public void DepartedHeadAdvancesFollowersBeforeANewTailCanReserve()
        {
            var queue = Queue(count: 4);
            var first = Paid(queue, "first");
            var second = Paid(queue, "second");
            var third = Paid(queue, "third");
            foreach (var assignment in queue.Snapshot)
                Assert.That(queue.MarkArrived(assignment.VisitId, assignment.Generation), Is.True);
            Assert.That(queue.TryAcquireCollection("first"), Is.True);
            Assert.That(queue.Remove("first"), Is.True);
            queue.ReleaseServicePosition("first");
            Assert.That(queue.TryReserve("new-tail", out _), Is.False, "Vacant front is reserved for advancing older customers");
            Assert.That(queue.CanReserve, Is.False);
            Assert.That(queue.TryGetNextAdvance(out var next), Is.True);
            Assert.That(next.VisitId, Is.EqualTo("second"));
            Assert.That(next.SlotIndex, Is.EqualTo(0));
            Assert.That(next.Position, Is.EqualTo(Vector3.zero));
            Assert.That(next.Generation, Is.GreaterThan(second.Generation));
            Assert.That(next.Arrived, Is.False);
            Assert.That(queue.MarkArrived("second", second.Generation), Is.False);
            Assert.That(queue.TryAcquireCollection("second"), Is.False);
            Assert.That(queue.TryGetNextAdvance(out var follower), Is.True,
                "Caller may permit the next route once the real preceding body has vacated it");
            Assert.That(follower.VisitId, Is.EqualTo("third"));
            Assert.That(follower.SlotIndex, Is.EqualTo(1));
            Assert.That(follower.Position, Is.EqualTo(new Vector3(0, 0, 1.1f)));
            Assert.That(follower.Generation, Is.GreaterThan(third.Generation));
            Assert.That(follower.Arrived, Is.False);
            Assert.That(queue.TryGetNextAdvance(out _), Is.False);
            Assert.That(queue.CanReserve, Is.False, "The old tail body has not yet left its prior target");
            Assert.That(queue.MarkArrived("third", third.Generation), Is.False);
            Assert.That(queue.MarkArrived("third", follower.Generation), Is.True);
            Assert.That(queue.CanReserve, Is.False, "Second customer still has not actually arrived");
            Assert.That(queue.MarkArrived("second", next.Generation), Is.True);
            Assert.That(queue.CanReserve, Is.True);
            Assert.That(queue.TryReserve("new-tail", out var tail), Is.True);
            Assert.That(tail.SlotIndex, Is.EqualTo(2));
            Assert.That(tail.Position, Is.EqualTo(new Vector3(0, 0, 2.2f)));
            Assert.That(queue.TryBeginTransfer("new-tail", out _), Is.True);
            CollectionAssert.AreEqual(new[] { "second", "third", "new-tail" }, queue.Snapshot.Select(a => a.VisitId));
            Assert.That(queue.TryAcquireCollection("third"), Is.False);
            Assert.That(queue.TryAcquireCollection("second"), Is.True);
        }

        [Test] public void AdvancingLeaderKeepsFollowerBehindItsOwnedSlotWhenFrontIsReleased()
        {
            var queue=Queue(count:4);
            foreach(var id in new[]{"first","second","third","fourth"}) Paid(queue,id);
            foreach(var item in queue.Snapshot) queue.MarkArrived(item.VisitId,item.Generation);
            queue.Remove("first");
            Assert.That(queue.TryGetNextAdvance(out var second),Is.True);
            Assert.That(second.SlotIndex,Is.EqualTo(0));
            Assert.That(queue.TryGetNextAdvance(out var third),Is.True);
            Assert.That(third.SlotIndex,Is.EqualTo(1));
            queue.MarkArrived("second",second.Generation); queue.Remove("second");
            Assert.That(queue.TryGetNextAdvance(out var fourth),Is.True,"Parallel advance may continue behind the moving leader");
            Assert.That(fourth.VisitId,Is.EqualTo("fourth"));
            Assert.That(fourth.SlotIndex,Is.EqualTo(2),"The vacant head remains ahead of the older moving customer; no overtaking");
        }

        [Test] public void RefusedAdvanceKeepsOldAssignmentAndGenerationWithoutAdmittingANewTail()
        {
            var queue = Queue(count: 3);
            Paid(queue, "first");
            Paid(queue, "second");
            Paid(queue, "third");
            foreach (var assignment in queue.Snapshot)
                Assert.That(queue.MarkArrived(assignment.VisitId, assignment.Generation), Is.True);
            Assert.That(queue.Remove("first"), Is.True);
            var before = queue.Snapshot.ToArray();
            var inspected = false;
            Assert.That(queue.TryGetNextAdvance(out _, candidate =>
            {
                inspected = true;
                Assert.That(candidate.VisitId, Is.EqualTo("second"));
                Assert.That(candidate.Position, Is.EqualTo(Vector3.zero));
                Assert.That(candidate.Arrived, Is.False);
                return false; // Actual NavWorld/body/route check rejects this move.
            }), Is.False);
            Assert.That(inspected, Is.True);
            CollectionAssert.AreEqual(before, queue.Snapshot, "No partial target, arrival or generation commit");
            Assert.That(queue.CanReserve, Is.False);
            Assert.That(queue.TryReserve("new-tail", out _), Is.False);
            Assert.That(queue.TryGetNextAdvance(out var approved, candidate => true), Is.True);
            Assert.That(approved.VisitId, Is.EqualTo("second"));
            Assert.That(approved.Generation, Is.EqualTo(before[1].Generation + 1), "Rejected proposal consumed no generation");
            var moving = queue.Snapshot.ToArray();
            Assert.That(queue.TryGetNextAdvance(out _, candidate => false), Is.False);
            CollectionAssert.AreEqual(moving, queue.Snapshot);
            Assert.That(queue.Snapshot[0].Arrived, Is.False);
            Assert.That(queue.Snapshot[1].Arrived, Is.True);
        }

        [Test] public void AdvanceWaitsForInitialArrivalAndCannotMoveTheServiceLeaseOwner()
        {
            var queue = Queue(count: 3);
            var first = Paid(queue, "first");
            var second = Paid(queue, "second");
            Assert.That(queue.MarkArrived("first", first.Generation), Is.True);
            Assert.That(queue.TryAcquireCollection("first"), Is.True);
            Assert.That(queue.Remove("first"), Is.True);
            Assert.That(queue.TryGetNextAdvance(out _), Is.False, "Actual service body still owns its lease");
            queue.ReleaseServicePosition("first");
            Assert.That(queue.TryGetNextAdvance(out _), Is.False, "Second is still traveling to its initial wait target");
            Assert.That(queue.MarkArrived("second", second.Generation), Is.True);
            Assert.That(queue.TryGetNextAdvance(out var advanced), Is.True);
            Assert.That(advanced.VisitId, Is.EqualTo("second"));
            Assert.That(advanced.Position, Is.EqualTo(Vector3.zero));
        }

        [Test] public void AdvancePreservesUnpaidPhysicalReservationUntilItsTransfer()
        {
            var queue = Queue(count: 3);
            var first = Paid(queue, "first");
            var second = Paid(queue, "second");
            Assert.That(queue.TryReserve("unpaid", out var unpaid), Is.True);
            Assert.That(queue.MarkArrived("first", first.Generation), Is.True);
            Assert.That(queue.MarkArrived("second", second.Generation), Is.True);
            Assert.That(queue.Remove("first"), Is.True);
            Assert.That(queue.TryGetNextAdvance(out var advanced), Is.True);
            Assert.That(advanced.VisitId, Is.EqualTo("second"));
            Assert.That(queue.MarkArrived("second", advanced.Generation), Is.True);
            Assert.That(queue.TryReserve("unpaid", out var stillReserved), Is.True);
            Assert.That(stillReserved, Is.EqualTo(unpaid));
            Assert.That(queue.CanReserve, Is.False, "The prepaid gap cannot be stolen by a later reservation");
            Assert.That(queue.TryGetNextAdvance(out _), Is.False, "Unpaid is not yet a movable FIFO member");
            Assert.That(queue.TryBeginTransfer("unpaid", out var paid), Is.True);
            Assert.That(queue.MarkArrived("unpaid", paid.Generation), Is.True);
            Assert.That(queue.TryGetNextAdvance(out var last), Is.True);
            Assert.That(last.VisitId, Is.EqualTo("unpaid"));
            Assert.That(last.SlotIndex, Is.EqualTo(1));
            Assert.That(queue.MarkArrived("unpaid", last.Generation), Is.True);
            Assert.That(queue.CanReserve, Is.True);
        }
    }
}

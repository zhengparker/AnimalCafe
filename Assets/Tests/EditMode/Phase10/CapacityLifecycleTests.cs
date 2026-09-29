using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Capacity;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase10
{
    public sealed class CapacityLifecycleTests
    {
        [TestCase(CapacityKind.TotalCustomers)]
        [TestCase(CapacityKind.CounterQueue)]
        [TestCase(CapacityKind.PickUp)]
        public void E024_OccupyMovesOnlyItsKindToOccupiedWithoutChangingUsed(CapacityKind kind)
        {
            var service = new CapacityService(16);
            var reserved = service.TryReserve("V1", new[] { kind }).Reservations.Single();
            var result = service.Occupy(reserved.Token, "V1");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.FailureReason, Is.EqualTo(CapacityFailureReason.None));
            var occupied = result.Reservations.Single();
            Assert.That(occupied.Token, Is.SameAs(reserved.Token));
            Assert.That(occupied.State, Is.EqualTo(ReservationState.Occupied));
            Assert.That(occupied, Is.Not.SameAs(reserved));
            Assert.That(reserved.State, Is.EqualTo(ReservationState.Reserved));
            var capacity = service.GetCapacities().Single(x => x.Kind == kind);
            Assert.That(capacity.Reserved, Is.Zero);
            Assert.That(capacity.Occupied, Is.EqualTo(1));
            Assert.That(capacity.Used, Is.EqualTo(1));
            Assert.That(capacity.Available, Is.EqualTo(capacity.Limit - 1));
        }

        [TestCase(CapacityKind.TotalCustomers)]
        [TestCase(CapacityKind.CounterQueue)]
        [TestCase(CapacityKind.PickUp)]
        public void E026_ReleaseReservedReturnsCapacityWithoutOccupy(CapacityKind kind)
        {
            var service = new CapacityService(16);
            var reserved = service.TryReserve("V1", new[] { kind }).Reservations.Single();
            var result = service.Release(reserved.Token, "V1");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Reservations.Single().State, Is.EqualTo(ReservationState.Released));
            Assert.That(reserved.State, Is.EqualTo(ReservationState.Reserved));
            var capacity = service.GetCapacities().Single(x => x.Kind == kind);
            Assert.That(capacity.Reserved, Is.Zero);
            Assert.That(capacity.Occupied, Is.Zero);
            Assert.That(capacity.Used, Is.Zero);
            Assert.That(capacity.Available, Is.EqualTo(capacity.Limit));
            Assert.That(service.GetReservations().Single().State, Is.EqualTo(ReservationState.Released));
        }

        [Test]
        public void E027_ReleaseOccupiedReturnsOnlyItsCapacity()
        {
            var service = new CapacityService(16);
            var first = service.TryReserveAdmission("V1").Reservations;
            var other = service.TryReserve("V2", new[] { CapacityKind.CounterQueue })
                .Reservations.Single();
            service.Occupy(first[1].Token, "V1");
            var beforeOther = service.GetReservations().Single(x => x.Token == other.Token);

            var result = service.Release(first[1].Token, "V1");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Reservations.Single().State, Is.EqualTo(ReservationState.Released));
            var counter = service.GetCapacities().Single(x => x.Kind == CapacityKind.CounterQueue);
            Assert.That(counter.Reserved, Is.EqualTo(1));
            Assert.That(counter.Occupied, Is.Zero);
            Assert.That(counter.Used, Is.EqualTo(1));
            var afterOther = service.GetReservations().Single(x => x.Token == other.Token);
            Assert.That(afterOther, Is.Not.SameAs(beforeOther));
            Assert.That(afterOther.OwnerId, Is.EqualTo("V2"));
            Assert.That(afterOther.Token, Is.SameAs(other.Token));
            Assert.That(afterOther.State, Is.EqualTo(ReservationState.Reserved));
            Assert.That(other.State, Is.EqualTo(ReservationState.Reserved));
            Assert.That(service.GetCapacities().Single(x => x.Kind == CapacityKind.TotalCustomers).Used,
                Is.EqualTo(1));
            Assert.That(service.GetCapacities().Single(x => x.Kind == CapacityKind.PickUp).Used,
                Is.EqualTo(1));
        }

        [Test]
        public void E028_RepeatedReleaseByOwnerSucceedsWithoutChangingStateOrNextId()
        {
            var service = new CapacityService(16);
            var token = service.TryReserve("V1", new[] { CapacityKind.PickUp })
                .Reservations.Single().Token;
            Assert.That(service.Release(token, "V1").Succeeded, Is.True);
            var before = CapacityTestSupport.Capture(service);

            var repeated = service.Release(token, "V1");

            Assert.That(repeated.Succeeded, Is.True);
            Assert.That(repeated.FailureReason, Is.EqualTo(CapacityFailureReason.None));
            Assert.That(repeated.Reservations.Single().State, Is.EqualTo(ReservationState.Released));
            Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
            Assert.That(service.TryReserve("V2", new[] { CapacityKind.PickUp })
                .Reservations.Single().Token.Id, Is.EqualTo(2));
        }

        [TestCase(ReservationState.Reserved, false, true)]
        [TestCase(ReservationState.Reserved, false, false)]
        [TestCase(ReservationState.Reserved, true, true)]
        [TestCase(ReservationState.Reserved, true, false)]
        [TestCase(ReservationState.Occupied, false, true)]
        [TestCase(ReservationState.Occupied, false, false)]
        [TestCase(ReservationState.Occupied, true, true)]
        [TestCase(ReservationState.Occupied, true, false)]
        [TestCase(ReservationState.Released, false, true)]
        [TestCase(ReservationState.Released, false, false)]
        [TestCase(ReservationState.Released, true, true)]
        [TestCase(ReservationState.Released, true, false)]
        public void E025_E029_E030_E034_StateOperationOwnerMatrix(
            ReservationState initial, bool release, bool correctOwner)
        {
            var service = new CapacityService(16);
            var token = service.TryReserve("V1", new[] { CapacityKind.PickUp })
                .Reservations.Single().Token;
            if (initial == ReservationState.Occupied)
            {
                Assert.That(service.Occupy(token, "V1").Succeeded, Is.True);
            }
            else if (initial == ReservationState.Released)
            {
                Assert.That(service.Release(token, "V1").Succeeded, Is.True);
            }
            var before = CapacityTestSupport.Capture(service);

            var result = release
                ? service.Release(token, correctOwner ? "V1" : "V2")
                : service.Occupy(token, correctOwner ? "V1" : "V2");

            var shouldSucceed = correctOwner && (release || initial == ReservationState.Reserved);
            Assert.That(result.Succeeded, Is.EqualTo(shouldSucceed));
            Assert.That(result.FailureReason, Is.EqualTo(correctOwner
                ? shouldSucceed ? CapacityFailureReason.None : CapacityFailureReason.InvalidTransition
                : CapacityFailureReason.WrongOwner));
            Assert.That(result.Reservations.Count, Is.EqualTo(shouldSucceed ? 1 : 0));
            if (!shouldSucceed || initial == ReservationState.Released)
            {
                Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
            }
            else
            {
                Assert.That(service.GetReservations().Single().State,
                    Is.EqualTo(release ? ReservationState.Released : ReservationState.Occupied));
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase(" V1")]
        [TestCase("V1 ")]
        public void E030_E035_InvalidOwnerWinsOverNullTokenAndDoesNotChangeState(string owner)
        {
            var service = new CapacityService(16);
            var before = CapacityTestSupport.Capture(service);
            AssertFailureUnchanged(service, before, service.Occupy(null, owner),
                CapacityFailureReason.InvalidOwnerId);
            AssertFailureUnchanged(service, before, service.Release(null, owner),
                CapacityFailureReason.InvalidOwnerId);
        }

        [Test]
        public void E031_NullAndOtherServiceTokensCannotAccessSameId()
        {
            var first = new CapacityService(16);
            var second = new CapacityService(16);
            var token = first.TryReserve("V1", new[] { CapacityKind.PickUp })
                .Reservations.Single().Token;
            var otherToken = second.TryReserve("V1", new[] { CapacityKind.PickUp })
                .Reservations.Single().Token;
            Assert.That(token.Id, Is.EqualTo(otherToken.Id));
            var beforeFirst = CapacityTestSupport.Capture(first);
            var beforeSecond = CapacityTestSupport.Capture(second);
            foreach (var invalid in new[] { null, otherToken })
            {
                AssertFailureUnchanged(first, beforeFirst, first.Occupy(invalid, "V1"),
                    CapacityFailureReason.InvalidToken);
                AssertFailureUnchanged(first, beforeFirst, first.Release(invalid, "V1"),
                    CapacityFailureReason.InvalidToken);
            }
            Assert.That(CapacityTestSupport.Capture(second), Is.EqualTo(beforeSecond));
        }

        [Test]
        public void E032_OldReleasedTokenCannotChangeNewTokenForSameOwnerAndKind()
        {
            var service = new CapacityService(16);
            var oldToken = service.TryReserve("V1", new[] { CapacityKind.PickUp })
                .Reservations.Single().Token;
            Assert.That(service.Release(oldToken, "V1").Succeeded, Is.True);
            var newToken = service.TryReserve("V1", new[] { CapacityKind.PickUp })
                .Reservations.Single().Token;
            Assert.That(newToken.Id, Is.EqualTo(2));
            var before = CapacityTestSupport.Capture(service);
            Assert.That(service.Release(oldToken, "V1").Succeeded, Is.True);
            Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
            AssertFailureUnchanged(service, before, service.Occupy(oldToken, "V1"),
                CapacityFailureReason.InvalidTransition);
            AssertFailureUnchanged(service, before, service.Release(oldToken, "V2"),
                CapacityFailureReason.WrongOwner);
            Assert.That(service.GetReservations().Single(x => x.Token == newToken).State,
                Is.EqualTo(ReservationState.Reserved));
            Assert.That(service.GetCapacities().Single(x => x.Kind == CapacityKind.PickUp).Used,
                Is.EqualTo(1));
        }

        [Test]
        public void E033_SavedQueriesAndSnapshotsStayFixedAcrossOccupyReleaseAndNewReserve()
        {
            var service = new CapacityService(16);
            var first = service.TryReserve("V1", new[] { CapacityKind.PickUp })
                .Reservations.Single();
            var oldCapacities = service.GetCapacities();
            var oldReservations = service.GetReservations();
            Assert.Throws<NotSupportedException>(() =>
                ((IList<CapacitySnapshot>)oldCapacities)[0] = null);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<ReservationSnapshot>)oldReservations)[0] = null);

            service.Occupy(first.Token, "V1");
            service.Release(first.Token, "V1");
            service.TryReserve("V2", new[] { CapacityKind.TotalCustomers });

            Assert.That(oldCapacities.Select(x => x.Used), Is.EqualTo(new[] { 0, 0, 1 }));
            Assert.That(oldCapacities[2].Reserved, Is.EqualTo(1));
            Assert.That(oldReservations.Count, Is.EqualTo(1));
            Assert.That(oldReservations[0].State, Is.EqualTo(ReservationState.Reserved));
            Assert.That(first.State, Is.EqualTo(ReservationState.Reserved));
            Assert.That(service.GetReservations().Select(x => x.Token.Id),
                Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(service.GetReservations().Select(x => x.State),
                Is.EqualTo(new[] { ReservationState.Released, ReservationState.Reserved }));
            Assert.That(service.GetCapacities().Select(x => x.Kind),
                Is.EqualTo(new[] { CapacityKind.TotalCustomers,
                    CapacityKind.CounterQueue, CapacityKind.PickUp }));
        }

        private static void AssertFailureUnchanged(CapacityService service, string before,
            CapacityResult result, CapacityFailureReason reason)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(reason));
            Assert.That(result.Reservations, Is.Empty);
            Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
        }
    }
}

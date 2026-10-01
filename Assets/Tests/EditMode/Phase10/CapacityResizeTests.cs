using System;
using System.Linq;
using AnimalCafe.Capacity;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase10
{
    public sealed class CapacityResizeTests
    {
        [Test]
        public void E036_ExpansionReplacesLimitsButKeepsReservationsAndDoesNotRefill()
        {
            var service = new CapacityService(16);
            var first = service.TryReserveAdmission("V1").Reservations;
            Assert.That(service.Occupy(first[0].Token, "V1").Succeeded, Is.True);
            var before = service.GetReservations();

            var updated = service.UpdateFloorCellCount(32);

            Assert.That(updated.Succeeded, Is.True);
            Assert.That(updated.FailureReason, Is.EqualTo(CapacityFailureReason.None));
            Assert.That(updated.Reservations, Is.Empty);
            CapacityTestSupport.AssertLimits(service, 32, 8, 4, 8);
            Assert.That(service.GetReservations().Select(x => x.Token),
                Is.EqualTo(before.Select(x => x.Token)));
            Assert.That(service.GetReservations().Select(x => x.State),
                Is.EqualTo(before.Select(x => x.State)));
            Assert.That(service.GetReservations().Count, Is.EqualTo(3));
            Assert.That(service.GetCapacities().Select(x => x.Used),
                Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(service.TryReserve("V2", new[] { CapacityKind.PickUp })
                .Reservations.Single().Token.Id, Is.EqualTo(4));
        }

        [Test]
        public void E037_ShrinkPreservesOverageAndBlocksEveryNewKind()
        {
            var service = new CapacityService(16);
            service.TryReserveAdmission("V1");
            service.TryReserveAdmission("V2");
            var tokens = service.GetReservations().Select(x => x.Token).ToArray();

            Assert.That(service.UpdateFloorCellCount(4).Succeeded, Is.True);

            CapacityTestSupport.AssertLimits(service, 4, 1, 1, 1);
            foreach (var capacity in service.GetCapacities())
            {
                Assert.That(capacity.Used, Is.EqualTo(2));
                Assert.That(capacity.Available, Is.Zero);
                Assert.That(capacity.OverCapacity, Is.EqualTo(1));
            }
            Assert.That(service.GetReservations().Select(x => x.Token), Is.EqualTo(tokens));
            var before = CapacityTestSupport.Capture(service);
            var heldTokens = service.GetReservations().Select(x => x.Token).ToArray();
            var rejected = service.TryReserve("V3", new[] { CapacityKind.TotalCustomers });
            CapacityTestSupport.AssertFailureUnchanged(service, before, rejected,
                CapacityFailureReason.CapacityOverLimit);
            var afterRejected = service.GetReservations();
            Assert.That(afterRejected.Count, Is.EqualTo(heldTokens.Length));
            for (var i = 0; i < heldTokens.Length; i++)
                Assert.That(afterRejected[i].Token, Is.SameAs(heldTokens[i]));
        }

        [Test]
        public void E038_OldReservationsCanOccupyDuringDrainingThenReleaseAtZero()
        {
            var service = new CapacityService(16);
            service.TryReserveAdmission("V1");
            service.TryReserveAdmission("V2");
            Assert.That(service.UpdateFloorCellCount(0).Succeeded, Is.True);
            var held = service.GetReservations();
            foreach (var reservation in held)
            {
                var used = service.GetCapacities()[(int)reservation.Kind].Used;
                Assert.That(service.Occupy(reservation.Token, reservation.OwnerId).Succeeded, Is.True);
                Assert.That(service.GetCapacities()[(int)reservation.Kind].Used, Is.EqualTo(used));
            }
            foreach (var reservation in held)
            {
                Assert.That(service.Release(reservation.Token, reservation.OwnerId).Succeeded, Is.True);
            }
            Assert.That(service.GetCapacities().All(x => x.Used == 0 && x.Available == 0 &&
                x.OverCapacity == 0), Is.True);
            Assert.That(service.TryReserveAdmission("V3").FailureReason,
                Is.EqualTo(CapacityFailureReason.InsufficientCapacity));
        }

        [Test]
        public void E039_GlobalOverageBlocksOtherKindUntilDrained()
        {
            var service = new CapacityService(32);
            for (var i = 1; i <= 4; i++)
                Assert.That(service.TryReserve($"V{i}", new[] { CapacityKind.CounterQueue })
                    .Succeeded, Is.True);
            Assert.That(service.UpdateFloorCellCount(16).Succeeded, Is.True);
            var before = CapacityTestSupport.Capture(service);
            var heldTokens = service.GetReservations().Select(x => x.Token).ToArray();
            CapacityTestSupport.AssertFailureUnchanged(service, before,
                service.TryReserve("V5", new[] { CapacityKind.TotalCustomers }),
                CapacityFailureReason.CapacityOverLimit);
            var afterRejected = service.GetReservations();
            Assert.That(afterRejected.Count, Is.EqualTo(heldTokens.Length));
            for (var i = 0; i < heldTokens.Length; i++)
                Assert.That(afterRejected[i].Token, Is.SameAs(heldTokens[i]));

            foreach (var record in service.GetReservations().Take(2))
                Assert.That(service.Release(record.Token, record.OwnerId).Succeeded, Is.True);
            Assert.That(service.GetCapacities()[1].OverCapacity, Is.Zero);
            Assert.That(service.CanAdmit, Is.False); // Counter remains full.
            Assert.That(service.TryReserve("V5", new[] { CapacityKind.TotalCustomers })
                .Succeeded, Is.True);
        }

        [Test]
        public void E040_CanAdmitRequiresAllThreeSlotsAndDoesNotAutoRefill()
        {
            var service = new CapacityService(32);
            for (var i = 1; i <= 4; i++)
                Assert.That(service.TryReserveAdmission($"V{i}").Succeeded, Is.True);
            Assert.That(service.UpdateFloorCellCount(16).Succeeded, Is.True);
            Assert.That(service.CanAdmit, Is.False);

            var held = service.GetReservations();
            foreach (var record in held.Where(x => x.Kind == CapacityKind.CounterQueue).Take(3))
                Assert.That(service.Release(record.Token, record.OwnerId).Succeeded, Is.True);
            Assert.That(service.GetCapacities()[1].Used, Is.EqualTo(1));
            Assert.That(service.CanAdmit, Is.False);
            Assert.That(service.TryReserveAdmission("V5").FailureReason,
                Is.EqualTo(CapacityFailureReason.InsufficientCapacity));
            foreach (var kind in new[] { CapacityKind.TotalCustomers, CapacityKind.PickUp })
                Assert.That(service.Release(held.First(x => x.OwnerId == "V1" && x.Kind == kind)
                    .Token, "V1").Succeeded, Is.True);
            Assert.That(service.CanAdmit, Is.True);
            Assert.That(service.GetReservations().Count, Is.EqualTo(12));
            Assert.That(service.TryReserveAdmission("V5").Succeeded, Is.True);
            Assert.That(service.GetReservations().Count, Is.EqualTo(15));

            var expanded = new CapacityService(4);
            expanded.TryReserveAdmission("A");
            Assert.That(expanded.UpdateFloorCellCount(32).Succeeded, Is.True);
            Assert.That(expanded.GetReservations().Count, Is.EqualTo(3));
        }

        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void E041_InvalidUpdateReturnsFailureWithoutChangingIdentityOrNextId(int floor)
        {
            var service = new CapacityService(16);
            var held = service.TryReserveAdmission("V1").Reservations;
            var before = CapacityTestSupport.Capture(service);
            CapacityTestSupport.AssertFailureUnchanged(service, before,
                service.UpdateFloorCellCount(floor), CapacityFailureReason.InvalidFloorCellCount);
            Assert.That(service.Occupy(held[0].Token, "V1").Succeeded, Is.True);
            Assert.That(service.Release(held[0].Token, "V1").Succeeded, Is.True);
            Assert.That(service.TryReserve("V2", new[] { CapacityKind.TotalCustomers })
                .Reservations.Single().Token.Id, Is.EqualTo(4));
        }

        [Test]
        public void E042_RepeatedAndSameThresholdUpdatesKeepIdentityAndCounts()
        {
            var service = new CapacityService(16);
            var token = service.TryReserve("V1", new[] { CapacityKind.TotalCustomers })
                .Reservations.Single().Token;
            foreach (var floor in new[] { 16, 16, 17, 19, 19 })
            {
                Assert.That(service.UpdateFloorCellCount(floor).Succeeded, Is.True);
                CapacityTestSupport.AssertLimits(service, floor, 4, 2, 4);
                Assert.That(service.GetReservations().Single().Token, Is.SameAs(token));
                Assert.That(service.GetCapacities()[0].Used, Is.EqualTo(1));
            }
            Assert.That(service.TryReserve("V2", new[] { CapacityKind.TotalCustomers })
                .Reservations.Single().Token.Id, Is.EqualTo(2));
        }

        [Test]
        public void E042_UpdateKeepsCustomSessionRules()
        {
            var rules = new CapacityRules(cellsPerCustomer: 5, counterPercent: 25);
            var service = new CapacityService(25, rules, firstTokenId: 80);
            var token = service.TryReserve("V1", new[] { CapacityKind.CounterQueue })
                .Reservations.Single().Token;
            Assert.That(service.UpdateFloorCellCount(50).Succeeded, Is.True);
            CapacityTestSupport.AssertLimits(service, 50, 10, 3, 10);
            Assert.That(service.GetReservations().Single().Token, Is.SameAs(token));
            Assert.That(service.TryReserve("V2", new[] { CapacityKind.CounterQueue })
                .Reservations.Single().Token.Id, Is.EqualTo(81));
        }

        [Test]
        public void E043_ConfirmedLayoutUnionRefreshIsExplicit()
        {
            var layout = CapacityTestSupport.CreateLayout();
            layout.AddRegion(CapacityTestSupport.Region("first", 0, 0, 4, 4));
            var service = new CapacityService(FloorCapacitySource.CountInteriorCells(layout));
            var token = service.TryReserveAdmission("V1").Reservations[0].Token;
            CapacityTestSupport.AssertLimits(service, 16, 4, 2, 4);

            layout.AddRegion(CapacityTestSupport.Region("second", 4, 0, 4, 4));
            Assert.That(FloorCapacitySource.CountInteriorCells(layout), Is.EqualTo(32));
            CapacityTestSupport.AssertLimits(service, 16, 4, 2, 4);
            Assert.That(service.UpdateFloorCellCount(FloorCapacitySource.CountInteriorCells(layout))
                .Succeeded, Is.True);
            CapacityTestSupport.AssertLimits(service, 32, 8, 4, 8);

            var replacement = CapacityTestSupport.CreateLayout();
            replacement.AddRegion(CapacityTestSupport.Region("confirmed", 0, 0, 2, 2));
            Assert.That(service.UpdateFloorCellCount(FloorCapacitySource.CountInteriorCells(replacement))
                .Succeeded, Is.True);
            CapacityTestSupport.AssertLimits(service, 4, 1, 1, 1);
            Assert.That(service.GetReservations()[0].Token, Is.SameAs(token));
            Assert.That(FloorCapacitySource.CountInteriorCells(layout), Is.EqualTo(32));
        }
    }
}

using System.Linq;
using AnimalCafe.Capacity;
using AnimalCafe.Orders;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase10
{
    public sealed class CapacityOrderBoundaryTests
    {
        [Test]
        public void E045_CompletedOrderKeepsVisitCapacityUntilExplicitDeparture()
        {
            var capacity = new CapacityService(16);
            var orders = new OrderService();
            var held = capacity.TryReserveAdmission("V1").Reservations;
            Assert.That(capacity.Occupy(held[0].Token, "V1").Succeeded, Is.True);
            Assert.That(capacity.Occupy(held[1].Token, "V1").Succeeded, Is.True);
            var beforeOrder = CapacityTestSupport.Capture(capacity);

            var created = orders.Create("C1", "coffee");
            Assert.That(created.Succeeded, Is.True);
            var orderId = created.Order.OrderId;
            Assert.That(CapacityTestSupport.Capture(capacity), Is.EqualTo(beforeOrder));
            Assert.That(capacity.GetReservations()[1].State,
                Is.EqualTo(ReservationState.Occupied));

            Assert.That(capacity.Release(held[1].Token, "V1").Succeeded, Is.True);
            Assert.That(capacity.Occupy(held[2].Token, "V1").Succeeded, Is.True);
            var beforeFulfillment = CapacityTestSupport.Capture(capacity);
            Assert.That(orders.ClaimNext("E1").Succeeded, Is.True);
            Assert.That(orders.StartPreparation(orderId, "E1").Succeeded, Is.True);
            Assert.That(orders.MarkReadyForPickup(orderId, "E1").Succeeded, Is.True);
            Assert.That(orders.Complete(orderId, "C1").Succeeded, Is.True);
            Assert.That(orders.TryGetOrder(orderId, out var complete), Is.True);
            Assert.That(complete.State, Is.EqualTo(OrderState.Completed));
            Assert.That(CapacityTestSupport.Capture(capacity), Is.EqualTo(beforeFulfillment));
            Assert.That(capacity.GetReservations()[2].State,
                Is.EqualTo(ReservationState.Occupied));
            Assert.That(capacity.GetReservations()[0].State,
                Is.EqualTo(ReservationState.Occupied));

            Assert.That(capacity.Release(held[2].Token, "V1").Succeeded, Is.True);
            Assert.That(capacity.GetCapacities().Select(x => x.Used),
                Is.EqualTo(new[] { 1, 0, 0 }));
            Assert.That(capacity.Release(held[0].Token, "V1").Succeeded, Is.True);
            Assert.That(capacity.GetCapacities().All(x => x.Used == 0), Is.True);
            Assert.That(orders.TryGetOrder(orderId, out complete), Is.True);
            Assert.That(complete.State, Is.EqualTo(OrderState.Completed));
        }

        [Test]
        public void E046_WrongCustomerAndFailedOrderDoNotMeanVisitDeparted()
        {
            var capacity = new CapacityService(16);
            var orders = new OrderService();
            var held = capacity.TryReserveAdmission("V1").Reservations;
            Assert.That(capacity.Occupy(held[0].Token, "V1").Succeeded, Is.True);
            Assert.That(capacity.Occupy(held[1].Token, "V1").Succeeded, Is.True);
            Assert.That(capacity.Release(held[1].Token, "V1").Succeeded, Is.True);
            Assert.That(capacity.Occupy(held[2].Token, "V1").Succeeded, Is.True);
            var before = CapacityTestSupport.Capture(capacity);

            var created = orders.Create("C1", "coffee");
            Assert.That(created.Succeeded, Is.True);
            var id = created.Order.OrderId;
            Assert.That(CapacityTestSupport.Capture(capacity), Is.EqualTo(before));
            Assert.That(orders.ClaimNext("E1").Succeeded, Is.True);
            Assert.That(orders.StartPreparation(id, "E1").Succeeded, Is.True);
            Assert.That(orders.MarkReadyForPickup(id, "E1").Succeeded, Is.True);
            Assert.That(CapacityTestSupport.Capture(capacity), Is.EqualTo(before));

            var wrong = orders.Complete(id, "C2");
            Assert.That(wrong.Succeeded, Is.False);
            Assert.That(wrong.FailureReason, Is.EqualTo(OrderOperationFailureReason.WrongCustomer));
            Assert.That(orders.TryGetOrder(id, out var stillReady), Is.True);
            Assert.That(stillReady.State, Is.EqualTo(OrderState.ReadyForPickup));
            Assert.That(CapacityTestSupport.Capture(capacity), Is.EqualTo(before));

            Assert.That(orders.Fail(id, "diagnostic.stop").Succeeded, Is.True);
            Assert.That(orders.TryGetOrder(id, out var failed), Is.True);
            Assert.That(failed.State, Is.EqualTo(OrderState.Failed));
            Assert.That(CapacityTestSupport.Capture(capacity), Is.EqualTo(before));

            var notEntered = capacity.TryReserveAdmission("V2").Reservations;
            foreach (var reservation in notEntered)
                Assert.That(capacity.Release(reservation.Token, "V2").Succeeded, Is.True);
            Assert.That(capacity.GetCapacities().Select(x => x.Used),
                Is.EqualTo(new[] { 1, 0, 1 }));
        }

        [Test]
        public void E047_IndependentP9OrderChangesDoNotMutateCapacity()
        {
            var capacity = new CapacityService(16);
            capacity.TryReserveAdmission("V1");
            var before = CapacityTestSupport.Capture(capacity);
            var orders = new OrderService();
            var id = orders.Create("C1", "tea").Order.OrderId;
            Assert.That(orders.Fail(id, "diagnostic.stop").Succeeded, Is.True);
            Assert.That(CapacityTestSupport.Capture(capacity), Is.EqualTo(before));
        }
    }
}

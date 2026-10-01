using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Orders;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase9
{
    public sealed class OrderClaimTests
    {
        [Test]
        public void E018_PreparingIsBusyButReadyCanClaimAgainWithoutChangingOldOwner()
        {
            var service = new OrderService();
            service.Create("C1", "coffee"); service.Create("C2", "coffee");
            service.ClaimNext("E1");
            OrderTestSupport.AssertSuccess(service.StartPreparation(1, "E1"), 1);
            var before = service.GetOrders();
            var queue = service.GetWaitingOrderIds();
            OrderTestSupport.AssertFailure(service.ClaimNext("E1"), OrderOperationFailureReason.EmployeeBusy);
            OrderTestSupport.AssertUnchanged(service, before, queue);
            OrderTestSupport.AssertSuccess(service.MarkReadyForPickup(1, "E1"), 1);
            OrderTestSupport.AssertSuccess(service.ClaimNext("E1"), 2);
            Assert.That(service.GetOrders().Select(order => order.State),
                Is.EqualTo(new[] { OrderState.ReadyForPickup, OrderState.Claimed }));
            Assert.That(service.GetOrders().Select(order => order.ClaimantId), Is.EqualTo(new[] { "E1", "E1" }));
        }

        [TestCase(OrderState.Claimed)]
        [TestCase(OrderState.Preparing)]
        public void E019_FailingActiveOrderMakesEmployeeAvailable(OrderState state)
        {
            var service = new OrderService();
            service.Create("C1", "coffee"); service.Create("C2", "coffee");
            service.ClaimNext("E1");
            if (state == OrderState.Preparing) service.StartPreparation(1, "E1");
            OrderTestSupport.AssertSuccess(service.Fail(1, "diagnostic.stop"), 1);
            OrderTestSupport.AssertSuccess(service.ClaimNext("E1"), 2);
            Assert.That(service.GetOrders()[0].State, Is.EqualTo(OrderState.Failed));
            Assert.That(service.GetOrders()[0].ClaimantId, Is.EqualTo("E1"));
            Assert.That(service.GetOrders()[0].TerminationReason, Is.EqualTo("diagnostic.stop"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void E020_TerminatingOldReadyOrderDoesNotReleaseNewActiveOrder(bool failOld)
        {
            var service = new OrderService();
            service.Create("C1", "coffee"); service.Create("C2", "coffee"); service.Create("C3", "coffee");
            service.ClaimNext("E1"); service.StartPreparation(1, "E1");
            service.MarkReadyForPickup(1, "E1"); service.ClaimNext("E1");
            if (failOld) OrderTestSupport.AssertSuccess(service.Fail(1, "diagnostic.stop"), 1);
            else OrderTestSupport.AssertSuccess(service.Complete(1, "C1"), 1);
            var before = service.GetOrders();
            var queue = service.GetWaitingOrderIds();
            OrderTestSupport.AssertFailure(service.ClaimNext("E1"), OrderOperationFailureReason.EmployeeBusy);
            OrderTestSupport.AssertUnchanged(service, before, queue);
            Assert.That(service.GetOrders()[1].State, Is.EqualTo(OrderState.Claimed));
            Assert.That(service.GetWaitingOrderIds(), Is.EqualTo(new long[] { 3 }));
        }

        [TestCase(1L, 2L, 3L)]
        [TestCase(2L, 1L, 3L)]
        [TestCase(3L, 1L, 2L)]
        public void E021_FailWaitingAtAnyPositionPreservesRemainingFifo(long failedId, long first, long second)
        {
            var service = new OrderService();
            service.Create("C1", "coffee"); service.Create("C2", "coffee"); service.Create("C3", "coffee");
            OrderTestSupport.AssertSuccess(service.Fail(failedId, "diagnostic.stop"), failedId);
            Assert.That(service.GetWaitingOrderIds(), Is.EqualTo(new[] { first, second }));
            OrderTestSupport.AssertSuccess(service.ClaimNext("E1"), first);
            OrderTestSupport.AssertSuccess(service.ClaimNext("E2"), second);
            Assert.That(service.GetOrders().Single(order => order.OrderId == failedId).State, Is.EqualTo(OrderState.Failed));
            OrderTestSupport.AssertInvariant(service, new long[] { 1, 2, 3 });
        }

        [Test]
        public void E013_EmptyQueueReturnsNoWaitingOrdersWithoutConsumingId()
        {
            var service = new OrderService();
            var beforeOrders = service.GetOrders();
            var beforeQueue = service.GetWaitingOrderIds();

            OrderTestSupport.AssertFailure(service.ClaimNext("E1"), OrderOperationFailureReason.NoWaitingOrders);

            AssertUnchanged(service, beforeOrders, beforeQueue);
            OrderTestSupport.AssertSuccess(service.Create("C1", "coffee"), 1);
        }

        [Test]
        public void E014_ThreeEmployeesClaimExactlyOneOrderEachInFifoOrder()
        {
            var service = new OrderService();
            service.Create("C1", "coffee");
            service.Create("C2", "coffee");
            service.Create("C3", "coffee");
            var oldSnapshot = service.GetOrders()[0];
            var oldQueue = service.GetWaitingOrderIds();

            AssertClaim(service, "E1", 1, 2, 3);
            AssertClaim(service, "E2", 2, 3);
            AssertClaim(service, "E3", 3);
            OrderTestSupport.AssertFailure(service.ClaimNext("E4"), OrderOperationFailureReason.NoWaitingOrders);

            Assert.That(oldSnapshot.State, Is.EqualTo(OrderState.Waiting));
            Assert.That(oldSnapshot.ClaimantId, Is.Null);
            Assert.That(oldQueue, Is.EqualTo(new long[] { 1, 2, 3 }));
            AssertWaitingInvariant(service);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("\t")]
        [TestCase(" E1")]
        [TestCase("E1 ")]
        public void E015_InvalidEmployeeLeavesOrdersQueueAndNextIdUntouched(string employeeId)
        {
            var service = new OrderService();
            service.Create("C1", "coffee");
            service.Create("C2", "coffee");
            var beforeOrders = service.GetOrders();
            var beforeQueue = service.GetWaitingOrderIds();

            OrderTestSupport.AssertFailure(service.ClaimNext(employeeId), OrderOperationFailureReason.InvalidEmployeeId);

            AssertUnchanged(service, beforeOrders, beforeQueue);
            OrderTestSupport.AssertSuccess(service.Create("C3", "coffee"), 3);
            AssertClaim(service, "E1", 1, 2, 3);
            OrderTestSupport.AssertFailure(service.ClaimNext("E1"), OrderOperationFailureReason.EmployeeBusy);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void E016_ClaimedEmployeeIsBusyEvenWhenQueueIsEmpty(bool hasSecondWaitingOrder)
        {
            var service = new OrderService();
            service.Create("C1", "coffee");
            if (hasSecondWaitingOrder)
            {
                service.Create("C2", "coffee");
            }

            OrderTestSupport.AssertSuccess(service.ClaimNext("E1"), 1);
            var beforeOrders = service.GetOrders();
            var beforeQueue = service.GetWaitingOrderIds();

            OrderTestSupport.AssertFailure(service.ClaimNext("E1"), OrderOperationFailureReason.EmployeeBusy);

            AssertUnchanged(service, beforeOrders, beforeQueue);
            OrderTestSupport.AssertSuccess(service.Create("C3", "coffee"), hasSecondWaitingOrder ? 3 : 2);
            OrderTestSupport.AssertFailure(service.ClaimNext("E1"), OrderOperationFailureReason.EmployeeBusy);
        }

        [Test]
        public void E017_SequentialClaimsKeepDistinctOwnersAndDoNotRewriteFirstOrder()
        {
            var service = new OrderService();
            service.Create("C1", "coffee");
            service.Create("C2", "coffee");

            var firstClaim = service.ClaimNext("E1");
            var secondClaim = service.ClaimNext("E2");

            OrderTestSupport.AssertSuccess(firstClaim, 1);
            OrderTestSupport.AssertSuccess(secondClaim, 2);
            Assert.That(firstClaim.Order.ClaimantId, Is.EqualTo("E1"));
            Assert.That(secondClaim.Order.ClaimantId, Is.EqualTo("E2"));
            Assert.That(firstClaim.Order.State, Is.EqualTo(OrderState.Claimed));
            Assert.That(secondClaim.Order.State, Is.EqualTo(OrderState.Claimed));
            Assert.That(service.GetOrders().Select(order => order.ClaimantId), Is.EqualTo(new[] { "E1", "E2" }));
            Assert.That(service.GetWaitingOrderIds(), Is.Empty);
            AssertWaitingInvariant(service);
        }

        private static void AssertClaim(OrderService service, string employeeId, long expectedId, params long[] expectedQueue)
        {
            var result = service.ClaimNext(employeeId);
            OrderTestSupport.AssertSuccess(result, expectedId);
            Assert.That(result.Order.State, Is.EqualTo(OrderState.Claimed));
            Assert.That(result.Order.ClaimantId, Is.EqualTo(employeeId));
            Assert.That(result.Order.TerminationReason, Is.Null);
            Assert.That(service.GetWaitingOrderIds(), Is.EqualTo(expectedQueue));
            Assert.That(service.TryGetOrder(expectedId, out var stored), Is.True);
            OrderTestSupport.AssertSnapshot(stored, result.Order);
            AssertWaitingInvariant(service);
        }

        private static void AssertUnchanged(OrderService service, IReadOnlyList<OrderSnapshot> orders,
            IReadOnlyList<long> waitingIds)
        {
            var currentOrders = service.GetOrders();
            Assert.That(currentOrders.Count, Is.EqualTo(orders.Count));
            for (var i = 0; i < orders.Count; i++)
            {
                OrderTestSupport.AssertSnapshot(currentOrders[i], orders[i]);
            }

            Assert.That(service.GetWaitingOrderIds(), Is.EqualTo(waitingIds));
            AssertWaitingInvariant(service);
        }

        private static void AssertWaitingInvariant(OrderService service)
        {
            var orders = service.GetOrders();
            var waiting = service.GetWaitingOrderIds();
            Assert.That(orders.All(order => order.OrderId > 0), Is.True);
            Assert.That(orders.Select(order => order.OrderId).Distinct().Count(), Is.EqualTo(orders.Count));
            Assert.That(waiting, Is.EqualTo(orders.Where(order => order.State == OrderState.Waiting)
                .Select(order => order.OrderId)));
            Assert.That(orders.Where(order => order.State == OrderState.Claimed)
                .Select(order => order.ClaimantId).Distinct().Count(),
                Is.EqualTo(orders.Count(order => order.State == OrderState.Claimed)));
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using AnimalCafe.Orders;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase9
{
    public sealed class OrderCreationQueryTests
    {
        [Test]
        public void E001_NewServiceHasEmptyQueriesAndMissingOrderIsNull()
        {
            var service = new OrderService();

            Assert.That(service.GetOrders(), Is.Empty);
            Assert.That(service.GetWaitingOrderIds(), Is.Empty);
            Assert.That(service.TryGetOrder(1, out var order), Is.False);
            Assert.That(order, Is.Null);
        }

        [Test]
        public void E002_CreateReturnsWaitingOrderAndQueuesItsId()
        {
            var service = new OrderService();
            var result = service.Create("C1", "coffee");

            OrderTestSupport.AssertSuccess(result, 1);
            Assert.That(result.Order.CustomerId, Is.EqualTo("C1"));
            Assert.That(result.Order.ProductId, Is.EqualTo("coffee"));
            Assert.That(result.Order.ClaimantId, Is.Null);
            Assert.That(result.Order.State, Is.EqualTo(OrderState.Waiting));
            Assert.That(result.Order.TerminationReason, Is.Null);
            OrderTestSupport.AssertWaitingIds(service, 1);
            Assert.That(service.TryGetOrder(1, out var found), Is.True);
            OrderTestSupport.AssertSnapshot(found, result.Order);
        }

        [Test]
        public void E003_SequentialCreatesReturnUniqueIncreasingIdsAndOrderedQueries()
        {
            var service = new OrderService();

            OrderTestSupport.AssertSuccess(service.Create("C1", "coffee"), 1);
            OrderTestSupport.AssertSuccess(service.Create("C2", "coffee"), 2);
            OrderTestSupport.AssertSuccess(service.Create("C3", "coffee"), 3);
            OrderTestSupport.AssertWaitingIds(service, 1, 2, 3);
            Assert.That(service.GetOrders().Select(order => order.OrderId).Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void E004_IdenticalRequestsCreateSeparateOrders()
        {
            var service = new OrderService();

            OrderTestSupport.AssertSuccess(service.Create("C1", "coffee"), 1);
            OrderTestSupport.AssertSuccess(service.Create("C1", "coffee"), 2);
            OrderTestSupport.AssertWaitingIds(service, 1, 2);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("\t")]
        [TestCase(" C1")]
        [TestCase("C1 ")]
        public void E005_InvalidCustomerPreservesStateAndNextId(string customerId)
        {
            var service = new OrderService();
            var beforeOrders = service.GetOrders();
            var beforeQueue = service.GetWaitingOrderIds();

            OrderTestSupport.AssertFailure(service.Create(customerId, "coffee"), OrderOperationFailureReason.InvalidCustomerId);
            Assert.That(service.GetOrders(), Is.EqualTo(beforeOrders));
            Assert.That(service.GetWaitingOrderIds(), Is.EqualTo(beforeQueue));
            OrderTestSupport.AssertSuccess(service.Create("C1", "coffee"), 1);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("\t")]
        [TestCase(" coffee")]
        [TestCase("coffee ")]
        public void E005_InvalidProductPreservesStateAndNextId(string productId)
        {
            var service = new OrderService();
            var first = service.Create("C1", "coffee").Order;
            var beforeOrders = service.GetOrders();
            var beforeQueue = service.GetWaitingOrderIds();

            OrderTestSupport.AssertFailure(service.Create("C2", productId), OrderOperationFailureReason.InvalidProductId);
            Assert.That(service.GetOrders().Count, Is.EqualTo(1));
            OrderTestSupport.AssertSnapshot(service.GetOrders()[0], first);
            Assert.That(service.GetOrders(), Is.EqualTo(beforeOrders));
            Assert.That(service.GetWaitingOrderIds(), Is.EqualTo(beforeQueue));
            OrderTestSupport.AssertSuccess(service.Create("C2", "coffee"), 2);
        }

        [Test]
        public void E005_CustomerErrorPrecedesProductError()
        {
            var service = new OrderService();

            OrderTestSupport.AssertFailure(service.Create("", ""), OrderOperationFailureReason.InvalidCustomerId);
            OrderTestSupport.AssertSuccess(service.Create("C1", "coffee"), 1);
        }

        [Test]
        public void E006_IdsKeepCaseUnicodeAndUnknownProductVerbatim()
        {
            var service = new OrderService();
            var first = service.Create("C1", "coffee").Order;
            var second = service.Create("c1", "COFFEE").Order;
            var third = service.Create("小明", "未知商品").Order;

            Assert.That(first.CustomerId, Is.EqualTo("C1"));
            Assert.That(first.ProductId, Is.EqualTo("coffee"));
            Assert.That(second.CustomerId, Is.EqualTo("c1"));
            Assert.That(second.ProductId, Is.EqualTo("COFFEE"));
            Assert.That(third.CustomerId, Is.EqualTo("小明"));
            Assert.That(third.ProductId, Is.EqualTo("未知商品"));
            OrderTestSupport.AssertWaitingIds(service, 1, 2, 3);
        }

        [TestCase(0L)]
        [TestCase(-1L)]
        [TestCase(long.MinValue)]
        public void E007_NonPositiveFirstIdIsRejected(long firstOrderId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OrderService(firstOrderId));
        }

        [Test]
        public void E007_DefaultAndCustomFirstIdsAreUsed()
        {
            OrderTestSupport.AssertSuccess(new OrderService().Create("C1", "coffee"), 1);
            OrderTestSupport.AssertSuccess(new OrderService(5).Create("C1", "coffee"), 5);
        }

        [Test]
        public void E008_MaxIdCanBeCreatedOnceWithoutWrapping()
        {
            var service = new OrderService(long.MaxValue - 1);
            OrderTestSupport.AssertSuccess(service.Create("C1", "coffee"), long.MaxValue - 1);
            OrderTestSupport.AssertSuccess(service.Create("C2", "coffee"), long.MaxValue);
            var beforeOrders = service.GetOrders();
            var beforeQueue = service.GetWaitingOrderIds();

            OrderTestSupport.AssertFailure(service.Create("C3", "coffee"), OrderOperationFailureReason.IdExhausted);
            Assert.That(service.GetOrders().Select(order => order.OrderId), Is.EqualTo(new[] { long.MaxValue - 1, long.MaxValue }));
            Assert.That(service.GetWaitingOrderIds(), Is.EqualTo(beforeQueue));
            Assert.That(service.GetOrders().Select(order => order.OrderId), Is.EqualTo(beforeOrders.Select(order => order.OrderId)));
            OrderTestSupport.AssertFailure(service.Create("C4", "coffee"), OrderOperationFailureReason.IdExhausted);
        }

        [Test]
        public void E008_InvalidInputPrecedesExhaustion()
        {
            var service = new OrderService(long.MaxValue);
            service.Create("C1", "coffee");

            OrderTestSupport.AssertFailure(service.Create("", "coffee"), OrderOperationFailureReason.InvalidCustomerId);
            OrderTestSupport.AssertFailure(service.Create("C2", ""), OrderOperationFailureReason.InvalidProductId);
            OrderTestSupport.AssertFailure(service.Create("C2", "coffee"), OrderOperationFailureReason.IdExhausted);
        }

        [TestCase(0L)]
        [TestCase(-1L)]
        [TestCase(long.MinValue)]
        [TestCase(999L)]
        public void E009_InvalidOrMissingQueryReturnsFalseAndNullWithoutMutation(long orderId)
        {
            var service = new OrderService();
            var first = service.Create("C1", "coffee").Order;

            Assert.That(service.TryGetOrder(orderId, out var found), Is.False);
            Assert.That(found, Is.Null);
            OrderTestSupport.AssertSnapshot(service.GetOrders()[0], first);
            OrderTestSupport.AssertWaitingIds(service, 1);
        }

        [Test]
        public void E010_SeparateServicesKeepIndependentIdsAndOrders()
        {
            var first = new OrderService();
            var second = new OrderService();

            OrderTestSupport.AssertSuccess(first.Create("C1", "coffee"), 1);
            OrderTestSupport.AssertSuccess(second.Create("C2", "tea"), 1);
            OrderTestSupport.AssertSuccess(first.Create("C3", "cake"), 2);
            OrderTestSupport.AssertWaitingIds(first, 1, 2);
            OrderTestSupport.AssertWaitingIds(second, 1);
            Assert.That(second.GetOrders()[0].CustomerId, Is.EqualTo("C2"));
        }

        [Test]
        public void E011_EarlierQueryCollectionsDoNotGrowAfterCreate()
        {
            var service = new OrderService();
            service.Create("C1", "coffee");
            var oldOrders = service.GetOrders();
            var oldQueue = service.GetWaitingOrderIds();

            service.Create("C2", "coffee");

            Assert.That(oldOrders.Select(order => order.OrderId), Is.EqualTo(new long[] { 1 }));
            Assert.That(oldQueue, Is.EqualTo(new long[] { 1 }));
            OrderTestSupport.AssertWaitingIds(service, 1, 2);
        }

        [Test]
        public void E012_QueryCollectionsAndSnapshotCannotModifyService()
        {
            var service = new OrderService();
            service.Create("C1", "coffee");
            var orders = service.GetOrders();
            var waiting = service.GetWaitingOrderIds();

            OrderTestSupport.AssertReadOnly(orders, orders[0]);
            OrderTestSupport.AssertReadOnly(waiting, 999L);
            foreach (var property in typeof(OrderSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.That(property.GetSetMethod(), Is.Null, property.Name);
            }

            OrderTestSupport.AssertWaitingIds(service, 1);
            Assert.That(service.GetOrders()[0].CustomerId, Is.EqualTo("C1"));
        }
    }
}

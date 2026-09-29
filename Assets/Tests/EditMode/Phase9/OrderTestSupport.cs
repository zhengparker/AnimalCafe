using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Orders;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase9
{
    internal static class OrderTestSupport
    {
        internal static OrderService InState(OrderState state)
        {
            var service = new OrderService();
            AssertSuccess(service.Create("C1", "coffee"), 1);
            if (state == OrderState.Waiting) return service;
            AssertSuccess(service.ClaimNext("E1"), 1);
            if (state == OrderState.Claimed) return service;
            AssertSuccess(service.StartPreparation(1, "E1"), 1);
            if (state == OrderState.Preparing) return service;
            AssertSuccess(service.MarkReadyForPickup(1, "E1"), 1);
            if (state == OrderState.ReadyForPickup) return service;
            if (state == OrderState.Completed) AssertSuccess(service.Complete(1, "C1"), 1);
            else if (state == OrderState.Failed) AssertSuccess(service.Fail(1, "diagnostic.stop"), 1);
            else Assert.Fail("Unknown state: " + state);
            return service;
        }

        internal static void AssertInvariant(OrderService service, IReadOnlyList<long> creationOrder)
        {
            var orders = service.GetOrders();
            var ids = orders.Select(order => order.OrderId).ToArray();
            Assert.That(ids.All(id => id > 0), Is.True);
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            Assert.That(ids, Is.Ordered.Ascending);
            Assert.That(service.GetWaitingOrderIds(), Is.EqualTo(creationOrder.Where(id =>
                service.TryGetOrder(id, out var order) && order.State == OrderState.Waiting)));
            Assert.That(orders.Where(order => order.State == OrderState.Claimed || order.State == OrderState.Preparing)
                .GroupBy(order => order.ClaimantId, StringComparer.Ordinal).All(group => group.Count() == 1), Is.True);
            Assert.That(orders.Where(order => order.State == OrderState.Completed || order.State == OrderState.Failed)
                .All(order => order.State != OrderState.Failed || !string.IsNullOrWhiteSpace(order.TerminationReason)), Is.True);
            Assert.That(orders.Where(order => order.State != OrderState.Failed)
                .All(order => order.TerminationReason == null), Is.True);
        }

        internal static void AssertUnchanged(OrderService service, IReadOnlyList<OrderSnapshot> beforeOrders,
            IReadOnlyList<long> beforeWaiting)
        {
            var after = service.GetOrders();
            Assert.That(after.Count, Is.EqualTo(beforeOrders.Count));
            for (var i = 0; i < after.Count; i++) AssertSnapshot(after[i], beforeOrders[i]);
            Assert.That(service.GetWaitingOrderIds(), Is.EqualTo(beforeWaiting));
        }

        // U probe：先确认原资料未变，再检查 busy，最后用真实 Create 验证编号未消耗。
        // This helper intentionally creates one probe order; call it after the rejected operation.
        internal static void AssertAtomicFailure(OrderService service, IReadOnlyList<OrderSnapshot> beforeOrders,
            IReadOnlyList<long> beforeWaiting, long expectedNextId)
        {
            AssertUnchanged(service, beforeOrders, beforeWaiting);
            AssertBusyEmployeesStillBusy(service, beforeOrders);
            AssertSuccess(service.Create("C-probe", "coffee"), expectedNextId);
            foreach (var previous in beforeOrders)
            {
                Assert.That(service.TryGetOrder(previous.OrderId, out var current), Is.True);
                AssertSnapshot(current, previous);
            }
        }

        internal static void AssertExhaustedAtomicFailure(OrderService service,
            IReadOnlyList<OrderSnapshot> beforeOrders, IReadOnlyList<long> beforeWaiting)
        {
            AssertUnchanged(service, beforeOrders, beforeWaiting);
            AssertBusyEmployeesStillBusy(service, beforeOrders);
            AssertFailure(service.Create("C-probe", "coffee"), OrderOperationFailureReason.IdExhausted);
            AssertFailure(service.Create("C-probe-again", "coffee"), OrderOperationFailureReason.IdExhausted);
            AssertUnchanged(service, beforeOrders, beforeWaiting);
        }

        private static void AssertBusyEmployeesStillBusy(OrderService service,
            IReadOnlyList<OrderSnapshot> beforeOrders)
        {
            foreach (var employee in beforeOrders.Where(order => order.State == OrderState.Claimed ||
                order.State == OrderState.Preparing).Select(order => order.ClaimantId).Distinct(StringComparer.Ordinal))
                AssertFailure(service.ClaimNext(employee), OrderOperationFailureReason.EmployeeBusy);
        }

        internal static void AssertSuccess(OrderResult result, long orderId)
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.FailureReason, Is.EqualTo(OrderOperationFailureReason.None));
            Assert.That(result.Order, Is.Not.Null);
            Assert.That(result.Order.OrderId, Is.EqualTo(orderId));
        }

        internal static void AssertFailure(OrderResult result, OrderOperationFailureReason reason)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(reason));
            Assert.That(result.Order, Is.Null);
        }

        internal static void AssertWaitingIds(OrderService service, params long[] expected)
        {
            Assert.That(service.GetWaitingOrderIds(), Is.EqualTo(expected));
            Assert.That(service.GetOrders().Select(order => order.OrderId), Is.EqualTo(expected));
            Assert.That(service.GetOrders().All(order => order.State == OrderState.Waiting), Is.True);
        }

        internal static void AssertSnapshot(OrderSnapshot actual, OrderSnapshot expected)
        {
            Assert.That(actual.OrderId, Is.EqualTo(expected.OrderId));
            Assert.That(actual.CustomerId, Is.EqualTo(expected.CustomerId));
            Assert.That(actual.ProductId, Is.EqualTo(expected.ProductId));
            Assert.That(actual.ClaimantId, Is.EqualTo(expected.ClaimantId));
            Assert.That(actual.State, Is.EqualTo(expected.State));
            Assert.That(actual.TerminationReason, Is.EqualTo(expected.TerminationReason));
        }

        internal static void AssertReadOnly<T>(IReadOnlyList<T> items, T attemptedItem)
        {
            var mutable = items as IList<T>;
            if (mutable == null)
            {
                return;
            }

            Assert.Throws<NotSupportedException>(() => mutable.Add(attemptedItem));
            Assert.Throws<NotSupportedException>(() => mutable.Clear());
            if (items.Count > 0)
            {
                Assert.Throws<NotSupportedException>(() => mutable[0] = attemptedItem);
            }
        }
    }
}

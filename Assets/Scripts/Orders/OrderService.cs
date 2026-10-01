using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimalCafe.Orders
{
    public sealed class OrderService
    {
        private readonly Dictionary<long, OrderSnapshot> orders = new Dictionary<long, OrderSnapshot>();
        private readonly List<long> waitingOrderIds = new List<long>();
        private long nextOrderId;
        private bool idExhausted;

        public OrderService(long firstOrderId = 1)
        {
            if (firstOrderId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(firstOrderId));
            }

            nextOrderId = firstOrderId;
        }

        public OrderResult Create(string customerId, string productId)
        {
            // 先验证输入，失败不能消耗订单编号。
            if (!IsValidId(customerId))
            {
                return new OrderResult(false, OrderOperationFailureReason.InvalidCustomerId, null);
            }

            if (!IsValidId(productId))
            {
                return new OrderResult(false, OrderOperationFailureReason.InvalidProductId, null);
            }

            if (idExhausted)
            {
                return new OrderResult(false, OrderOperationFailureReason.IdExhausted, null);
            }

            var orderId = nextOrderId;
            var order = new OrderSnapshot(orderId, customerId, productId, null, OrderState.Waiting, null);
            orders.Add(orderId, order);
            waitingOrderIds.Add(orderId);

            // long.MaxValue 可以使用一次，之后不能回绕或复用。
            if (orderId == long.MaxValue)
            {
                idExhausted = true;
            }
            else
            {
                nextOrderId = orderId + 1;
            }

            return new OrderResult(true, OrderOperationFailureReason.None, order);
        }

        public OrderResult ClaimNext(string employeeId)
        {
            if (!IsValidId(employeeId))
            {
                return new OrderResult(false, OrderOperationFailureReason.InvalidEmployeeId, null);
            }

            // Claimed 和 Preparing 都占用员工；先检查 busy，再检查空队列。
            if (orders.Values.Any(order => string.Equals(order.ClaimantId, employeeId, StringComparison.Ordinal)
                && (order.State == OrderState.Claimed || order.State == OrderState.Preparing)))
            {
                return new OrderResult(false, OrderOperationFailureReason.EmployeeBusy, null);
            }

            if (waitingOrderIds.Count == 0)
            {
                return new OrderResult(false, OrderOperationFailureReason.NoWaitingOrders, null);
            }

            var orderId = waitingOrderIds[0];
            var waiting = orders[orderId];
            var claimed = new OrderSnapshot(orderId, waiting.CustomerId, waiting.ProductId,
                employeeId, OrderState.Claimed, null);
            orders[orderId] = claimed;
            waitingOrderIds.RemoveAt(0);
            return new OrderResult(true, OrderOperationFailureReason.None, claimed);
        }

        public OrderResult StartPreparation(long orderId, string employeeId)
        {
            if (orderId <= 0) return Failure(OrderOperationFailureReason.InvalidOrderId);
            if (!IsValidId(employeeId)) return Failure(OrderOperationFailureReason.InvalidEmployeeId);
            if (!orders.TryGetValue(orderId, out var order)) return Failure(OrderOperationFailureReason.OrderNotFound);
            if (IsTerminal(order)) return Failure(OrderOperationFailureReason.OrderTerminal);
            if (order.State != OrderState.Claimed) return Failure(OrderOperationFailureReason.InvalidTransition);
            if (!string.Equals(order.ClaimantId, employeeId, StringComparison.Ordinal))
                return Failure(OrderOperationFailureReason.NotClaimOwner);

            return Replace(order, OrderState.Preparing, null);
        }

        public OrderResult MarkReadyForPickup(long orderId, string employeeId)
        {
            if (orderId <= 0) return Failure(OrderOperationFailureReason.InvalidOrderId);
            if (!IsValidId(employeeId)) return Failure(OrderOperationFailureReason.InvalidEmployeeId);
            if (!orders.TryGetValue(orderId, out var order)) return Failure(OrderOperationFailureReason.OrderNotFound);
            if (IsTerminal(order)) return Failure(OrderOperationFailureReason.OrderTerminal);
            if (order.State != OrderState.Preparing) return Failure(OrderOperationFailureReason.InvalidTransition);
            if (!string.Equals(order.ClaimantId, employeeId, StringComparison.Ordinal))
                return Failure(OrderOperationFailureReason.NotClaimOwner);

            // 上层确认已送达 Pick-up 后才调用；Preparing 包含送达前过程。
            return Replace(order, OrderState.ReadyForPickup, null);
        }

        public OrderResult Complete(long orderId, string customerId)
        {
            if (orderId <= 0) return Failure(OrderOperationFailureReason.InvalidOrderId);
            if (!IsValidId(customerId)) return Failure(OrderOperationFailureReason.InvalidCustomerId);
            if (!orders.TryGetValue(orderId, out var order)) return Failure(OrderOperationFailureReason.OrderNotFound);
            if (IsTerminal(order)) return Failure(OrderOperationFailureReason.OrderTerminal);
            if (order.State != OrderState.ReadyForPickup) return Failure(OrderOperationFailureReason.InvalidTransition);
            if (!string.Equals(order.CustomerId, customerId, StringComparison.Ordinal))
                return Failure(OrderOperationFailureReason.WrongCustomer);

            // 只有正确顾客实际领取后，上层才能调用 Complete。
            return Replace(order, OrderState.Completed, null);
        }

        public OrderResult Fail(long orderId, string reason)
        {
            if (orderId <= 0) return Failure(OrderOperationFailureReason.InvalidOrderId);
            if (string.IsNullOrWhiteSpace(reason)) return Failure(OrderOperationFailureReason.InvalidFailureReason);
            if (!orders.TryGetValue(orderId, out var order)) return Failure(OrderOperationFailureReason.OrderNotFound);
            if (IsTerminal(order)) return Failure(OrderOperationFailureReason.OrderTerminal);

            if (order.State == OrderState.Waiting) waitingOrderIds.Remove(orderId);
            return Replace(order, OrderState.Failed, reason);
        }

        private OrderResult Replace(OrderSnapshot previous, OrderState state, string reason)
        {
            var current = new OrderSnapshot(previous.OrderId, previous.CustomerId, previous.ProductId,
                previous.ClaimantId, state, reason);
            orders[previous.OrderId] = current;
            return new OrderResult(true, OrderOperationFailureReason.None, current);
        }

        private static bool IsTerminal(OrderSnapshot order)
        {
            return order.State == OrderState.Completed || order.State == OrderState.Failed;
        }

        private static OrderResult Failure(OrderOperationFailureReason reason)
        {
            return new OrderResult(false, reason, null);
        }

        public bool TryGetOrder(long orderId, out OrderSnapshot order)
        {
            if (orderId <= 0)
            {
                order = null;
                return false;
            }

            return orders.TryGetValue(orderId, out order);
        }

        public IReadOnlyList<OrderSnapshot> GetOrders()
        {
            return orders.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList().AsReadOnly();
        }

        public IReadOnlyList<long> GetWaitingOrderIds()
        {
            return new List<long>(waitingOrderIds).AsReadOnly();
        }

        private static bool IsValidId(string value)
        {
            return !string.IsNullOrWhiteSpace(value)
                && !char.IsWhiteSpace(value[0])
                && !char.IsWhiteSpace(value[value.Length - 1]);
        }
    }
}

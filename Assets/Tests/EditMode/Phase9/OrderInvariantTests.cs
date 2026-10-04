using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Orders;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase9
{
    public sealed class OrderInvariantTests
    {
        [Test]
        public void E037_TerminationNeverReusesIdsIncludingExhaustedMaximum()
        {
            var service = new OrderService();
            service.Create("C1", "coffee"); service.ClaimNext("E1");
            service.StartPreparation(1, "E1"); service.MarkReadyForPickup(1, "E1");
            service.Complete(1, "C1");
            OrderTestSupport.AssertSuccess(service.Create("C2", "coffee"), 2);
            service.Fail(2, "diagnostic.stop");
            OrderTestSupport.AssertSuccess(service.Create("C3", "coffee"), 3);

            var max = new OrderService(long.MaxValue);
            OrderTestSupport.AssertSuccess(max.Create("C1", "coffee"), long.MaxValue);
            OrderTestSupport.AssertSuccess(max.Fail(long.MaxValue, "diagnostic.stop"), long.MaxValue);
            OrderTestSupport.AssertFailure(max.Create("C2", "coffee"), OrderOperationFailureReason.IdExhausted);
            Assert.That(max.GetOrders().Select(order => order.OrderId), Is.EqualTo(new[] { long.MaxValue }));
        }

        [Test]
        public void E038_Seed17TwoHundredCommandsPreserveInvariantsAndRejectedCallsAreAtomic()
        {
            const int seed = 17;
            var random = new Random(seed);
            var service = new OrderService();
            var created = new List<long>();
            var terminal = new Dictionary<long, OrderSnapshot>();
            for (var step = 0; step < 200; step++)
            {
                var command = random.Next(6);
                var orders = service.GetOrders();
                var target = orders.Count == 0 ? 999 : orders[random.Next(orders.Count)].OrderId;
                var invalid = random.Next(5) == 0;
                var actor = invalid ? " bad" : "E" + random.Next(1, 4);
                var customer = invalid ? " bad" : "C" + random.Next(1, 5);
                var commandName = new[] { "Create", "ClaimNext", "StartPreparation", "MarkReadyForPickup",
                    "Complete", "Fail" }[command];
                var description = $"seed={seed}, step={step}, operation={commandName}, target={target}, invalid={invalid}";
                var beforeOrders = service.GetOrders();
                var beforeQueue = service.GetWaitingOrderIds();
                OrderResult result;
                switch (command)
                {
                    case 0: result = service.Create(customer, "coffee"); break;
                    case 1: result = service.ClaimNext(actor); break;
                    case 2: result = service.StartPreparation(target, actor); break;
                    case 3: result = service.MarkReadyForPickup(target, actor); break;
                    case 4: result = service.Complete(target, customer); break;
                    default: result = service.Fail(target, invalid ? " " : "diagnostic.stop"); break;
                }
                try
                {
                    if (!result.Succeeded)
                    {
                        Assert.That(result.FailureReason, Is.Not.EqualTo(OrderOperationFailureReason.None));
                        Assert.That(result.Order, Is.Null);
                        OrderTestSupport.AssertUnchanged(service, beforeOrders, beforeQueue);
                        // 独立 probe：检查失败后原员工仍 busy；此请求本身不应改变状态。
                        foreach (var active in beforeOrders.Where(order => order.State == OrderState.Claimed ||
                            order.State == OrderState.Preparing))
                            OrderTestSupport.AssertFailure(service.ClaimNext(active.ClaimantId),
                                OrderOperationFailureReason.EmployeeBusy);
                    }
                    else
                    {
                        Assert.That(result.FailureReason, Is.EqualTo(OrderOperationFailureReason.None));
                        Assert.That(result.Order, Is.Not.Null);
                        if (command == 0)
                        {
                            Assert.That(result.Order.OrderId, Is.EqualTo(created.Count + 1));
                            created.Add(result.Order.OrderId);
                        }
                        if (result.Order.State == OrderState.Completed || result.Order.State == OrderState.Failed)
                            terminal[result.Order.OrderId] = result.Order;
                    }
                    OrderTestSupport.AssertInvariant(service, created);
                    foreach (var entry in terminal)
                    {
                        Assert.That(service.TryGetOrder(entry.Key, out var current), Is.True);
                        OrderTestSupport.AssertSnapshot(current, entry.Value);
                    }
                }
                catch (Exception exception)
                {
                    Assert.Fail(description + ": " + exception.Message);
                }
            }
        }

        [Test]
        public void E039_OneThousandOrdersEndExactlyOnceWithNoQueueOrBusyLeft()
        {
            var service = new OrderService();
            var created = new List<long>();
            for (var i = 1; i <= 1000; i++)
            {
                OrderTestSupport.AssertSuccess(service.Create("C" + i, "coffee"), i);
                created.Add(i);
            }
            for (var i = 1; i <= 1000; i++)
            {
                var employee = i % 2 == 0 ? "E2" : "E1";
                OrderTestSupport.AssertSuccess(service.ClaimNext(employee), i);
                if (i % 3 == 0) OrderTestSupport.AssertSuccess(service.Fail(i, "diagnostic.stop"), i);
                else
                {
                    OrderTestSupport.AssertSuccess(service.StartPreparation(i, employee), i);
                    OrderTestSupport.AssertSuccess(service.MarkReadyForPickup(i, employee), i);
                    OrderTestSupport.AssertSuccess(service.Complete(i, "C" + i), i);
                }
            }
            Assert.That(service.GetOrders().Count, Is.EqualTo(1000));
            Assert.That(service.GetOrders().Count(order => order.State == OrderState.Completed), Is.EqualTo(667));
            Assert.That(service.GetOrders().Count(order => order.State == OrderState.Failed), Is.EqualTo(333));
            Assert.That(service.GetWaitingOrderIds(), Is.Empty);
            OrderTestSupport.AssertFailure(service.ClaimNext("E1"), OrderOperationFailureReason.NoWaitingOrders);
            OrderTestSupport.AssertFailure(service.ClaimNext("E2"), OrderOperationFailureReason.NoWaitingOrders);
            OrderTestSupport.AssertInvariant(service, created);
        }

        [Test]
        public void E040_ExhaustionOnlyBlocksCreateNotExistingReadyOrWaitingOrders()
        {
            var service = new OrderService(long.MaxValue - 1);
            OrderTestSupport.AssertSuccess(service.Create("C1", "coffee"), long.MaxValue - 1);
            OrderTestSupport.AssertSuccess(service.Create("C2", "coffee"), long.MaxValue);
            OrderTestSupport.AssertSuccess(service.ClaimNext("E1"), long.MaxValue - 1);
            service.StartPreparation(long.MaxValue - 1, "E1");
            service.MarkReadyForPickup(long.MaxValue - 1, "E1");
            OrderTestSupport.AssertFailure(service.Create("C3", "coffee"), OrderOperationFailureReason.IdExhausted);
            OrderTestSupport.AssertSuccess(service.ClaimNext("E1"), long.MaxValue);
            OrderTestSupport.AssertSuccess(service.Complete(long.MaxValue - 1, "C1"), long.MaxValue - 1);
            OrderTestSupport.AssertSuccess(service.Fail(long.MaxValue, "diagnostic.stop"), long.MaxValue);
            OrderTestSupport.AssertFailure(service.Create("C4", "coffee"), OrderOperationFailureReason.IdExhausted);
            OrderTestSupport.AssertInvariant(service, new[] { long.MaxValue - 1, long.MaxValue });
        }

        [TestCase("StartPreparation", OrderOperationFailureReason.NotClaimOwner)]
        [TestCase("MarkReadyForPickup", OrderOperationFailureReason.InvalidTransition)]
        [TestCase("Complete", OrderOperationFailureReason.InvalidTransition)]
        [TestCase("Fail", OrderOperationFailureReason.InvalidFailureReason)]
        public void E040_RejectedOperationAtIdLimitKeepsBusyAndExhaustion(string operation,
            OrderOperationFailureReason expectedReason)
        {
            var service = new OrderService(long.MaxValue - 1);
            service.Create("C1", "coffee");
            service.Create("C2", "coffee");
            OrderTestSupport.AssertSuccess(service.ClaimNext("E1"), long.MaxValue - 1);
            var before = service.GetOrders();
            var queue = service.GetWaitingOrderIds();
            OrderResult result;
            switch (operation)
            {
                case "StartPreparation": result = service.StartPreparation(long.MaxValue - 1, "E2"); break;
                case "MarkReadyForPickup": result = service.MarkReadyForPickup(long.MaxValue - 1, "E1"); break;
                case "Complete": result = service.Complete(long.MaxValue - 1, "C1"); break;
                default: result = service.Fail(long.MaxValue - 1, " "); break;
            }
            OrderTestSupport.AssertFailure(result, expectedReason);
            OrderTestSupport.AssertExhaustedAtomicFailure(service, before, queue);
            OrderTestSupport.AssertInvariant(service, new[] { long.MaxValue - 1, long.MaxValue });
        }
    }
}

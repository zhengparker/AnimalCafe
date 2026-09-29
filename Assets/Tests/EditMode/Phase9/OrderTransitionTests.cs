using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Orders;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase9
{
    public sealed class OrderTransitionTests
    {
        public enum Operation { Start, Ready, Complete, Fail }

        [Test]
        public void E022_CollectionByCorrectCustomerCompletesOnlyAfterDelivery()
        {
            var service = OrderTestSupport.InState(OrderState.Claimed);
            var preparing = service.StartPreparation(1, "E1");
            OrderTestSupport.AssertSuccess(preparing, 1);
            Assert.That(preparing.Order.State, Is.EqualTo(OrderState.Preparing));
            var delivered = service.MarkReadyForPickup(1, "E1");
            OrderTestSupport.AssertSuccess(delivered, 1);
            Assert.That(delivered.Order.State, Is.EqualTo(OrderState.ReadyForPickup));
            Assert.That(service.TryGetOrder(1, out var ready), Is.True);
            Assert.That(ready.State, Is.EqualTo(OrderState.ReadyForPickup));
            var completed = service.Complete(1, "C1");
            OrderTestSupport.AssertSuccess(completed, 1);
            Assert.That(completed.Order.State, Is.EqualTo(OrderState.Completed));
        }

        [TestCase(OrderState.Waiting, Operation.Start, OrderOperationFailureReason.InvalidTransition, OrderState.Waiting)]
        [TestCase(OrderState.Waiting, Operation.Ready, OrderOperationFailureReason.InvalidTransition, OrderState.Waiting)]
        [TestCase(OrderState.Waiting, Operation.Complete, OrderOperationFailureReason.InvalidTransition, OrderState.Waiting)]
        [TestCase(OrderState.Waiting, Operation.Fail, OrderOperationFailureReason.None, OrderState.Failed)]
        [TestCase(OrderState.Claimed, Operation.Start, OrderOperationFailureReason.None, OrderState.Preparing)]
        [TestCase(OrderState.Claimed, Operation.Ready, OrderOperationFailureReason.InvalidTransition, OrderState.Claimed)]
        [TestCase(OrderState.Claimed, Operation.Complete, OrderOperationFailureReason.InvalidTransition, OrderState.Claimed)]
        [TestCase(OrderState.Claimed, Operation.Fail, OrderOperationFailureReason.None, OrderState.Failed)]
        [TestCase(OrderState.Preparing, Operation.Start, OrderOperationFailureReason.InvalidTransition, OrderState.Preparing)]
        [TestCase(OrderState.Preparing, Operation.Ready, OrderOperationFailureReason.None, OrderState.ReadyForPickup)]
        [TestCase(OrderState.Preparing, Operation.Complete, OrderOperationFailureReason.InvalidTransition, OrderState.Preparing)]
        [TestCase(OrderState.Preparing, Operation.Fail, OrderOperationFailureReason.None, OrderState.Failed)]
        [TestCase(OrderState.ReadyForPickup, Operation.Start, OrderOperationFailureReason.InvalidTransition, OrderState.ReadyForPickup)]
        [TestCase(OrderState.ReadyForPickup, Operation.Ready, OrderOperationFailureReason.InvalidTransition, OrderState.ReadyForPickup)]
        [TestCase(OrderState.ReadyForPickup, Operation.Complete, OrderOperationFailureReason.None, OrderState.Completed)]
        [TestCase(OrderState.ReadyForPickup, Operation.Fail, OrderOperationFailureReason.None, OrderState.Failed)]
        [TestCase(OrderState.Completed, Operation.Start, OrderOperationFailureReason.OrderTerminal, OrderState.Completed)]
        [TestCase(OrderState.Completed, Operation.Ready, OrderOperationFailureReason.OrderTerminal, OrderState.Completed)]
        [TestCase(OrderState.Completed, Operation.Complete, OrderOperationFailureReason.OrderTerminal, OrderState.Completed)]
        [TestCase(OrderState.Completed, Operation.Fail, OrderOperationFailureReason.OrderTerminal, OrderState.Completed)]
        [TestCase(OrderState.Failed, Operation.Start, OrderOperationFailureReason.OrderTerminal, OrderState.Failed)]
        [TestCase(OrderState.Failed, Operation.Ready, OrderOperationFailureReason.OrderTerminal, OrderState.Failed)]
        [TestCase(OrderState.Failed, Operation.Complete, OrderOperationFailureReason.OrderTerminal, OrderState.Failed)]
        [TestCase(OrderState.Failed, Operation.Fail, OrderOperationFailureReason.OrderTerminal, OrderState.Failed)]
        public void E023_SixStatesByFourOperationsFollowTransitionMatrix(OrderState state,
            Operation operation, OrderOperationFailureReason failure, OrderState expectedState)
        {
            var service = OrderTestSupport.InState(state);
            var beforeOrders = service.GetOrders();
            var beforeWaiting = service.GetWaitingOrderIds();
            var result = Apply(service, operation, 1, "E1", "C1", "diagnostic.stop");
            if (failure == OrderOperationFailureReason.None)
            {
                OrderTestSupport.AssertSuccess(result, 1);
                Assert.That(result.Order.State, Is.EqualTo(expectedState));
            }
            else
            {
                OrderTestSupport.AssertFailure(result, failure);
                OrderTestSupport.AssertAtomicFailure(service, beforeOrders, beforeWaiting, 2);
            }
            Assert.That(service.TryGetOrder(1, out var current), Is.True);
            Assert.That(current.State, Is.EqualTo(expectedState));
            OrderTestSupport.AssertInvariant(service, failure == OrderOperationFailureReason.None
                ? new long[] { 1 } : new long[] { 1, 2 });
        }

        [Test]
        public void E024_WrongEmployeeCannotStartOrDeliver()
        {
            var service = OrderTestSupport.InState(OrderState.Claimed);
            var before = service.GetOrders();
            var queue = service.GetWaitingOrderIds();
            OrderTestSupport.AssertFailure(service.StartPreparation(1, "E2"), OrderOperationFailureReason.NotClaimOwner);
            OrderTestSupport.AssertAtomicFailure(service, before, queue, 2);
            OrderTestSupport.AssertSuccess(service.StartPreparation(1, "E1"), 1);
            before = service.GetOrders();
            queue = service.GetWaitingOrderIds();
            OrderTestSupport.AssertFailure(service.MarkReadyForPickup(1, "E2"), OrderOperationFailureReason.NotClaimOwner);
            OrderTestSupport.AssertAtomicFailure(service, before, queue, 3);
            OrderTestSupport.AssertSuccess(service.MarkReadyForPickup(1, "E1"), 1);
        }

        [TestCase("C2")]
        [TestCase("c1")]
        public void E025_OnlyExactCustomerMayCollect(string customerId)
        {
            var service = OrderTestSupport.InState(OrderState.ReadyForPickup);
            var before = service.GetOrders();
            var queue = service.GetWaitingOrderIds();
            OrderTestSupport.AssertFailure(service.Complete(1, customerId), OrderOperationFailureReason.WrongCustomer);
            OrderTestSupport.AssertAtomicFailure(service, before, queue, 2);
            OrderTestSupport.AssertSuccess(service.Complete(1, "C1"), 1);
        }

        [Test]
        public void E026_RepeatedTransitionsCannotChangeReadyOrTerminal()
        {
            var service = OrderTestSupport.InState(OrderState.Claimed);
            OrderTestSupport.AssertSuccess(service.StartPreparation(1, "E1"), 1);
            var before = service.GetOrders();
            var queue = service.GetWaitingOrderIds();
            OrderTestSupport.AssertFailure(service.StartPreparation(1, "E1"), OrderOperationFailureReason.InvalidTransition);
            OrderTestSupport.AssertAtomicFailure(service, before, queue, 2);
            OrderTestSupport.AssertSuccess(service.MarkReadyForPickup(1, "E1"), 1);
            before = service.GetOrders();
            queue = service.GetWaitingOrderIds();
            OrderTestSupport.AssertFailure(service.MarkReadyForPickup(1, "E1"), OrderOperationFailureReason.InvalidTransition);
            OrderTestSupport.AssertAtomicFailure(service, before, queue, 3);
            OrderTestSupport.AssertSuccess(service.Complete(1, "C1"), 1);
            before = service.GetOrders();
            queue = service.GetWaitingOrderIds();
            OrderTestSupport.AssertFailure(service.Complete(1, "C1"), OrderOperationFailureReason.OrderTerminal);
            OrderTestSupport.AssertAtomicFailure(service, before, queue, 4);
            before = service.GetOrders();
            queue = service.GetWaitingOrderIds();
            OrderTestSupport.AssertFailure(service.Fail(1, "other"), OrderOperationFailureReason.OrderTerminal);
            OrderTestSupport.AssertAtomicFailure(service, before, queue, 5);
        }

        [TestCase(OrderState.Waiting)]
        [TestCase(OrderState.Claimed)]
        [TestCase(OrderState.Preparing)]
        [TestCase(OrderState.ReadyForPickup)]
        public void E027_FailAnyNonterminalStatePreservesOwnerAndRemovesWaiting(OrderState state)
        {
            var service = OrderTestSupport.InState(state);
            var previous = service.GetOrders()[0];
            var result = service.Fail(1, "diagnostic.stop");
            OrderTestSupport.AssertSuccess(result, 1);
            Assert.That(result.Order.State, Is.EqualTo(OrderState.Failed));
            Assert.That(result.Order.TerminationReason, Is.EqualTo("diagnostic.stop"));
            Assert.That(result.Order.ClaimantId, Is.EqualTo(previous.ClaimantId));
            Assert.That(service.GetWaitingOrderIds(), Is.Empty);
            service.Create("C2", "coffee");
            OrderTestSupport.AssertSuccess(service.ClaimNext("E1"), 2);
            OrderTestSupport.AssertInvariant(service, new long[] { 1, 2 });
        }

        private static IEnumerable<TestCaseData> InvalidFailureReasons()
        {
            foreach (var state in new[] { OrderState.Waiting, OrderState.Claimed,
                OrderState.Preparing, OrderState.ReadyForPickup })
            {
                foreach (var reason in new string[] { null, "", " ", "\t" })
                    yield return new TestCaseData(state, reason).SetName("E028_" + state + "_" +
                        (reason == null ? "Null" : reason == "" ? "Empty" : reason == " " ? "Space" : "Tab"));
            }
        }

        [TestCaseSource(nameof(InvalidFailureReasons))]
        public void E028_InvalidFailureReasonIsAtomicAndValidReasonKeepsWhitespace(OrderState state, string reason)
        {
            var service = OrderTestSupport.InState(state);
            var before = service.GetOrders();
            var queue = service.GetWaitingOrderIds();
            OrderTestSupport.AssertFailure(service.Fail(1, reason), OrderOperationFailureReason.InvalidFailureReason);
            OrderTestSupport.AssertAtomicFailure(service, before, queue, 2);
            var failed = service.Fail(1, "  diagnostic.stop  ");
            OrderTestSupport.AssertSuccess(failed, 1);
            Assert.That(failed.Order.TerminationReason,
                Is.EqualTo("  diagnostic.stop  "));
        }

        [TestCase(OrderState.Completed)]
        [TestCase(OrderState.Failed)]
        public void E029_TerminalRejectsAllOperationsAndRetainsOriginalReason(OrderState state)
        {
            var service = OrderTestSupport.InState(state);
            OrderTestSupport.AssertSuccess(service.Create("C2", "coffee"), 2);
            OrderTestSupport.AssertSuccess(service.ClaimNext("E1"), 2);
            var nextId = 3L;
            foreach (Operation operation in Enum.GetValues(typeof(Operation)))
            {
                var before = service.GetOrders();
                var queue = service.GetWaitingOrderIds();
                OrderTestSupport.AssertFailure(Apply(service, operation, 1, "E1", "C1", "other"),
                    OrderOperationFailureReason.OrderTerminal);
                OrderTestSupport.AssertAtomicFailure(service, before, queue, nextId++);
            }
        }

        [TestCase(0L, OrderOperationFailureReason.InvalidOrderId)]
        [TestCase(-1L, OrderOperationFailureReason.InvalidOrderId)]
        [TestCase(long.MinValue, OrderOperationFailureReason.InvalidOrderId)]
        [TestCase(999L, OrderOperationFailureReason.OrderNotFound)]
        public void E030_AllIdOperationsDistinguishInvalidFromUnknownId(long orderId, OrderOperationFailureReason failure)
        {
            var service = OrderTestSupport.InState(OrderState.ReadyForPickup);
            OrderTestSupport.AssertSuccess(service.Create("C2", "coffee"), 2);
            OrderTestSupport.AssertSuccess(service.ClaimNext("E1"), 2);
            var nextId = 3L;
            foreach (Operation operation in Enum.GetValues(typeof(Operation)))
            {
                var before = service.GetOrders();
                var queue = service.GetWaitingOrderIds();
                OrderTestSupport.AssertFailure(Apply(service, operation, orderId, "E1", "C1", "diagnostic.stop"), failure);
                OrderTestSupport.AssertAtomicFailure(service, before, queue, nextId++);
            }
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("\t")]
        [TestCase(" E1")]
        [TestCase("E1 ")]
        public void E031_InvalidActorFormatBeatsMissingTerminalAndWrongState(string actor)
        {
            foreach (var state in new[] { OrderState.Waiting, OrderState.Completed })
            {
                var service = OrderTestSupport.InState(state);
                var before = service.GetOrders();
                var queue = service.GetWaitingOrderIds();
                foreach (var id in new long[] { 1, 999 })
                {
                    OrderTestSupport.AssertFailure(service.StartPreparation(id, actor), OrderOperationFailureReason.InvalidEmployeeId);
                    OrderTestSupport.AssertFailure(service.MarkReadyForPickup(id, actor), OrderOperationFailureReason.InvalidEmployeeId);
                    OrderTestSupport.AssertFailure(service.Complete(id, actor), OrderOperationFailureReason.InvalidCustomerId);
                }
                OrderTestSupport.AssertAtomicFailure(service, before, queue, 2);
            }
        }

        [Test]
        public void E032_MultipleErrorsUseDocumentedPrecedence()
        {
            var service = OrderTestSupport.InState(OrderState.Completed);
            var before = service.GetOrders();
            var queue = service.GetWaitingOrderIds();
            OrderTestSupport.AssertFailure(service.Create(null, null), OrderOperationFailureReason.InvalidCustomerId);
            foreach (Operation operation in Enum.GetValues(typeof(Operation)))
            {
                OrderTestSupport.AssertFailure(Apply(service, operation, 0, null, null, null),
                    OrderOperationFailureReason.InvalidOrderId);
                OrderTestSupport.AssertFailure(Apply(service, operation, 999, null, null, null),
                    operation == Operation.Fail ? OrderOperationFailureReason.InvalidFailureReason :
                        operation == Operation.Complete ? OrderOperationFailureReason.InvalidCustomerId :
                            OrderOperationFailureReason.InvalidEmployeeId);
                OrderTestSupport.AssertFailure(Apply(service, operation, 1, "E2", "C2", "other"),
                    OrderOperationFailureReason.OrderTerminal);
            }
            OrderTestSupport.AssertAtomicFailure(service, before, queue, 2);
            var afterCreate = service.GetOrders();
            OrderTestSupport.AssertFailure(service.StartPreparation(2, "E2"), OrderOperationFailureReason.InvalidTransition);
            OrderTestSupport.AssertFailure(service.MarkReadyForPickup(2, "E2"), OrderOperationFailureReason.InvalidTransition);
            OrderTestSupport.AssertFailure(service.Complete(2, "C9"), OrderOperationFailureReason.InvalidTransition);
            OrderTestSupport.AssertUnchanged(service, afterCreate, new long[] { 2 });
            Assert.That(queue, Is.Empty);
        }

        [TestCase(OrderState.Waiting)]
        [TestCase(OrderState.Claimed)]
        [TestCase(OrderState.Preparing)]
        public void E033_QueriesDoNotAdvanceBlockedOrderAndWorkCanResume(OrderState state)
        {
            var service = OrderTestSupport.InState(state);
            var before = service.GetOrders();
            var queue = service.GetWaitingOrderIds();
            for (var i = 0; i < 3; i++)
            {
                Assert.That(service.TryGetOrder(1, out _), Is.True);
                service.GetOrders();
                service.GetWaitingOrderIds();
            }
            OrderTestSupport.AssertUnchanged(service, before, queue);
            if (state == OrderState.Waiting) OrderTestSupport.AssertSuccess(service.ClaimNext("E1"), 1);
            if (state != OrderState.Preparing) OrderTestSupport.AssertSuccess(service.StartPreparation(1, "E1"), 1);
            OrderTestSupport.AssertSuccess(service.MarkReadyForPickup(1, "E1"), 1);
            OrderTestSupport.AssertSuccess(service.Complete(1, "C1"), 1);
        }

        [Test]
        public void E034_FifoClaimDoesNotRequireFifoCompletion()
        {
            var service = new OrderService();
            service.Create("C1", "coffee"); service.Create("C2", "coffee");
            OrderTestSupport.AssertSuccess(service.ClaimNext("E1"), 1);
            OrderTestSupport.AssertSuccess(service.ClaimNext("E2"), 2);
            service.StartPreparation(2, "E2"); service.MarkReadyForPickup(2, "E2");
            OrderTestSupport.AssertSuccess(service.Complete(2, "C2"), 2);
            service.StartPreparation(1, "E1"); service.MarkReadyForPickup(1, "E1");
            OrderTestSupport.AssertSuccess(service.Complete(1, "C1"), 1);
            Assert.That(service.GetOrders().Select(order => order.State),
                Is.EqualTo(new[] { OrderState.Completed, OrderState.Completed }));
        }

        [Test]
        public void E035_RejectedCallsDoNotPoisonLaterWork()
        {
            var service = OrderTestSupport.InState(OrderState.Claimed);
            OrderTestSupport.AssertFailure(service.StartPreparation(1, "E2"), OrderOperationFailureReason.NotClaimOwner);
            OrderTestSupport.AssertFailure(service.Complete(1, "C1"), OrderOperationFailureReason.InvalidTransition);
            service.StartPreparation(1, "E1"); service.MarkReadyForPickup(1, "E1");
            OrderTestSupport.AssertFailure(service.Complete(1, "C2"), OrderOperationFailureReason.WrongCustomer);
            OrderTestSupport.AssertSuccess(service.Complete(1, "C1"), 1);
            OrderTestSupport.AssertSuccess(service.Create("C2", "coffee"), 2);
            OrderTestSupport.AssertSuccess(service.ClaimNext("E1"), 2);
        }

        [Test]
        public void E036_OldAndResultSnapshotsStayFrozenAfterLaterTransitions()
        {
            var service = new OrderService();
            var created = service.Create("C1", "coffee");
            var waiting = service.GetOrders()[0];
            service.ClaimNext("E1"); service.StartPreparation(1, "E1");
            var ready = service.MarkReadyForPickup(1, "E1");
            service.Complete(1, "C1");
            Assert.That(created.Order.State, Is.EqualTo(OrderState.Waiting));
            Assert.That(waiting.State, Is.EqualTo(OrderState.Waiting));
            Assert.That(ready.Order.State, Is.EqualTo(OrderState.ReadyForPickup));
            Assert.That(service.GetOrders()[0].State, Is.EqualTo(OrderState.Completed));
        }

        private static OrderResult Apply(OrderService service, Operation operation, long orderId,
            string employee, string customer, string reason)
        {
            switch (operation)
            {
                case Operation.Start: return service.StartPreparation(orderId, employee);
                case Operation.Ready: return service.MarkReadyForPickup(orderId, employee);
                case Operation.Complete: return service.Complete(orderId, customer);
                default: return service.Fail(orderId, reason);
            }
        }

    }
}

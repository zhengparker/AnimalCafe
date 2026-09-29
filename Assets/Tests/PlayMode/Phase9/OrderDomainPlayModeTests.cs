using System.Collections;
using System.Linq;
using AnimalCafe.Orders;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode.Phase9
{
    public sealed class OrderDomainPlayModeTests
    {
        private float originalTimeScale;

        [SetUp]
        public void RememberTimeScale()
        {
            originalTimeScale = Time.timeScale;
        }

        [TearDown]
        public void RestoreTimeScale()
        {
            Time.timeScale = originalTimeScale;
        }

        [UnityTest]
        public IEnumerator P9_P_001_FullOrderFlowSurvivesFrameBoundaries()
        {
            var service = new OrderService();
            Assert.That(service.Create("C1", "coffee").Order.State, Is.EqualTo(OrderState.Waiting));
            yield return null;
            Assert.That(service.ClaimNext("E1").Order.State, Is.EqualTo(OrderState.Claimed));
            yield return null;
            Assert.That(service.StartPreparation(1, "E1").Order.State, Is.EqualTo(OrderState.Preparing));
            yield return null;
            Assert.That(service.MarkReadyForPickup(1, "E1").Order.State, Is.EqualTo(OrderState.ReadyForPickup));
            yield return null;
            var completed = service.Complete(1, "C1");
            Assert.That(completed.Succeeded, Is.True);
            Assert.That(completed.Order.State, Is.EqualTo(OrderState.Completed));
            Assert.That(service.GetWaitingOrderIds(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator P9_P_002_PauseNormalAndDoubleSpeedDoNotAdvanceOrders()
        {
            var service = new OrderService();
            for (var i = 1; i <= 4; i++)
                Assert.That(service.Create("C" + i, "coffee").Succeeded, Is.True);
            Assert.That(service.ClaimNext("E1").Order.OrderId, Is.EqualTo(1));
            Assert.That(service.StartPreparation(1, "E1").Succeeded, Is.True);
            Assert.That(service.MarkReadyForPickup(1, "E1").Succeeded, Is.True);
            Assert.That(service.ClaimNext("E2").Order.OrderId, Is.EqualTo(2));
            Assert.That(service.StartPreparation(2, "E2").Succeeded, Is.True);
            Assert.That(service.ClaimNext("E3").Order.OrderId, Is.EqualTo(3));

            foreach (var speed in new[] { 0f, 1f, 2f })
            {
                Time.timeScale = speed;
                for (var frame = 0; frame < 3; frame++) yield return null;
                yield return new WaitForSecondsRealtime(0.1f);

                Assert.That(service.GetOrders().Take(4).Select(order => order.State),
                    Is.EqualTo(new[] { OrderState.ReadyForPickup, OrderState.Preparing,
                        OrderState.Claimed, OrderState.Waiting }));
                Assert.That(service.GetOrders().Take(4).Select(order => order.ClaimantId),
                    Is.EqualTo(new[] { "E1", "E2", "E3", null }));
                Assert.That(service.GetWaitingOrderIds(), Is.EqualTo(new long[] { 4 }));
                Assert.That(service.TryGetOrder(1, out var ready), Is.True);
                Assert.That(ready.State, Is.EqualTo(OrderState.ReadyForPickup));
                // 各个 timeScale 下都能用合法诊断命令；只推进新建的 probe 单。
                // Legal diagnostic commands work at each speed; the probe order is separate.
                var probe = service.Create("C-probe-" + speed, "tea");
                Assert.That(probe.Succeeded, Is.True);
                Assert.That(service.Fail(probe.Order.OrderId, "diagnostic.stop").Succeeded, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator P9_P_003_NewServiceDoesNotInheritPreviousSessionAcrossFrames()
        {
            CreateAndReleaseFirstService();
            yield return null;
            Time.timeScale = 2f;
            var next = new OrderService();
            Assert.That(next.GetOrders(), Is.Empty);
            Assert.That(next.GetWaitingOrderIds(), Is.Empty);
            yield return null;
            var created = next.Create("C2", "tea");
            Assert.That(created.Order.OrderId, Is.EqualTo(1));
            Assert.That(next.ClaimNext("E2").Order.OrderId, Is.EqualTo(1));
        }

        private static void CreateAndReleaseFirstService()
        {
            var first = new OrderService();
            first.Create("C1", "coffee");
            first.ClaimNext("E1");
        }
    }
}

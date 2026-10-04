using System.Linq;
using AnimalCafe.EditorTools.Phase9;
using AnimalCafe.Orders;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.EditMode.Phase9
{
    public sealed class OrderDebugWindowTests
    {
        private OrderDomainDebugWindow window;

        [TearDown]
        public void CloseTestWindow()
        {
            if (window != null) window.Close();
            window = null;
        }

        [Test]
        public void P9_D_001_IndependentSessionsHaveIndependentServicesAndIds()
        {
            var first = new OrderDebugSession();
            var second = new OrderDebugSession();
            Assert.That(first.Service, Is.Not.SameAs(second.Service));
            Assert.That(first.Service.Create("C1", "coffee").Order.OrderId, Is.EqualTo(1));
            Assert.That(second.Service.GetOrders(), Is.Empty);
            Assert.That(second.Service.Create("C2", "tea").Order.OrderId, Is.EqualTo(1));
        }

        [Test]
        public void P9_D_002_ResetReplacesServiceAndStartsIdsAgain()
        {
            var session = new OrderDebugSession();
            var oldService = session.Service;
            oldService.Create("C1", "coffee");
            oldService.Create("C2", "tea");
            oldService.ClaimNext("E1");
            oldService.StartPreparation(1, "E1");
            oldService.MarkReadyForPickup(1, "E1");
            oldService.Fail(2, "diagnostic.stop");

            session.Reset();

            Assert.That(session.Service, Is.Not.SameAs(oldService));
            Assert.That(session.Service.GetOrders(), Is.Empty);
            Assert.That(session.Service.GetWaitingOrderIds(), Is.Empty);
            Assert.That(session.Service.Create("C3", "coffee").Order.OrderId, Is.EqualTo(1));
            oldService.Create("C4", "tea");
            Assert.That(session.Service.GetOrders().Count, Is.EqualTo(1));
        }

        [Test]
        public void P9_D_003_WindowResetClearsServiceInputsResultAndQueries()
        {
            window = NewWindow();
            window.CustomerId = "C9";
            window.ProductId = "tea";
            window.EmployeeId = "E9";
            window.OrderId = 72;
            window.FailureReason = "custom";
            window.RunCreate();
            var oldService = window.Session.Service;

            window.ResetSession();

            Assert.That(window.Session.Service, Is.Not.SameAs(oldService));
            Assert.That(window.CustomerId, Is.EqualTo("C1"));
            Assert.That(window.ProductId, Is.EqualTo("coffee"));
            Assert.That(window.EmployeeId, Is.EqualTo("E1"));
            Assert.That(window.OrderId, Is.EqualTo(1));
            Assert.That(window.FailureReason, Is.EqualTo("diagnostic.stop"));
            Assert.That(window.LastResult, Is.Null);
            Assert.That(window.Orders, Is.Empty);
            Assert.That(window.WaitingOrderIds, Is.Empty);
            Assert.That(window.RunCreate().Order.OrderId, Is.EqualTo(1));
        }

        [Test]
        public void P9_D_004_CloseThenNewWindowHasFreshSession()
        {
            window = NewWindow();
            window.RunCreate();
            var oldService = window.Session.Service;
            window.Close();
            window = null;

            window = NewWindow();

            Assert.That(window.Session.Service, Is.Not.SameAs(oldService));
            Assert.That(window.Orders, Is.Empty);
            Assert.That(window.RunCreate().Order.OrderId, Is.EqualTo(1));
        }

        [Test]
        public void P9_D_005_OpenOperateResetClosePreservesEditorAndSceneState()
        {
            var activeScene = SceneManager.GetActiveScene();
            var selected = Selection.objects.ToArray();
            var timeScale = Time.timeScale;
            var scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var dirty = scenes.Select(scene => scene.isDirty).ToArray();
            var rootIds = scenes.Select(scene => scene.GetRootGameObjects().Select(root => root.GetEntityId()).ToArray()).ToArray();

            try
            {
                window = NewWindow();
                window.RunCreate();
                window.RunClaimNext();
                window.RunStartPreparation();
                window.ResetSession();
                window.Close();
                window = null;

                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(activeScene));
                Assert.That(Selection.objects, Is.EqualTo(selected));
                Assert.That(Time.timeScale, Is.EqualTo(timeScale));
                Assert.That(SceneManager.sceneCount, Is.EqualTo(scenes.Length));
                for (var i = 0; i < scenes.Length; i++)
                {
                    Assert.That(SceneManager.GetSceneAt(i), Is.EqualTo(scenes[i]));
                    Assert.That(scenes[i].isDirty, Is.EqualTo(dirty[i]));
                    Assert.That(scenes[i].GetRootGameObjects().Select(root => root.GetEntityId()), Is.EqualTo(rootIds[i]));
                }
            }
            finally
            {
                Time.timeScale = timeScale;
            }
        }

        [Test]
        public void P9_D_006_WindowCommandsShowErrorsWithoutMutatingClaim()
        {
            window = NewWindow();
            Assert.That(window.RunClaimNext().FailureReason, Is.EqualTo(OrderOperationFailureReason.NoWaitingOrders));
            Assert.That(window.RunCreate().Succeeded, Is.True);
            window.OrderId = 999;
            Assert.That(window.RunClaimNext().Succeeded, Is.True);
            Assert.That(window.OrderId, Is.EqualTo(999));
            window.OrderId = 1;
            window.EmployeeId = "E2";
            Assert.That(window.RunStartPreparation().FailureReason, Is.EqualTo(OrderOperationFailureReason.NotClaimOwner));
            Assert.That(window.LastResult.FailureReason, Is.EqualTo(OrderOperationFailureReason.NotClaimOwner));
            Assert.That(window.Orders.Single().State, Is.EqualTo(OrderState.Claimed));
            Assert.That(window.Orders.Single().ClaimantId, Is.EqualTo("E1"));

            window.EmployeeId = "E1";
            Assert.That(window.RunStartPreparation().Succeeded, Is.True);
            Assert.That(window.RunMarkReadyForPickup().Succeeded, Is.True);
            window.CustomerId = "C2";
            Assert.That(window.RunSimulateCustomerCollection().FailureReason,
                Is.EqualTo(OrderOperationFailureReason.WrongCustomer));
            window.CustomerId = "C1";
            Assert.That(window.RunSimulateCustomerCollection().Order.State, Is.EqualTo(OrderState.Completed));
            Assert.That(window.RunCreate().Order.OrderId, Is.EqualTo(2));
            window.OrderId = 2;
            Assert.That(window.RunFail().Order.State, Is.EqualTo(OrderState.Failed));
        }

        private static OrderDomainDebugWindow NewWindow()
        {
            var testWindow = ScriptableObject.CreateInstance<OrderDomainDebugWindow>();
            testWindow.Show();
            return testWindow;
        }
    }
}

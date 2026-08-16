using AnimalCafe.UI.Foundation;
using NUnit.Framework;

namespace AnimalCafe.Tests.Phase5
{
    public sealed class UiNavigationCoordinatorTests
    {
        [Test]
        public void AT006_EmptyNavigation_BackIsNotHandledAndHudStaysOpen()
        {
            var coordinator = new UiNavigationCoordinator();
            var hud = CreateView("Hud", UiViewKind.Hud, UiOutsideDismissPolicy.NotDismissible);
            hud.Open();

            var handled = coordinator.TryHandleBack();

            Assert.That(handled, Is.False);
            Assert.That(hud.IsOpen, Is.True);
        }

        [Test]
        public void AT007_OpeningSecondMainPanel_ClosesTheFirstPanel()
        {
            var coordinator = new UiNavigationCoordinator();
            var panelA = CreateView("PanelA", UiViewKind.MainPanel, UiOutsideDismissPolicy.NotDismissible);
            var panelB = CreateView("PanelB", UiViewKind.MainPanel, UiOutsideDismissPolicy.NotDismissible);

            coordinator.OpenMainPanel(panelA);
            coordinator.OpenMainPanel(panelB);

            Assert.That(panelA.IsOpen, Is.False);
            Assert.That(panelB.IsOpen, Is.True);
            Assert.That(coordinator.ActiveMainPanel, Is.SameAs(panelB));
        }

        [Test]
        public void AT008_BackWithBottomSheet_ClosesOnlyTheBottomSheet()
        {
            var coordinator = new UiNavigationCoordinator();
            var panel = CreateView("Panel", UiViewKind.MainPanel, UiOutsideDismissPolicy.NotDismissible);
            var bottomSheet = CreateView("Sheet", UiViewKind.BottomSheet, UiOutsideDismissPolicy.Dismissible);

            coordinator.OpenMainPanel(panel);
            coordinator.OpenBottomSheet(bottomSheet);
            var handled = coordinator.TryHandleBack();

            Assert.That(handled, Is.True);
            Assert.That(bottomSheet.IsOpen, Is.False);
            Assert.That(panel.IsOpen, Is.True);
        }

        [Test]
        public void AT009_BackWithNestedModals_ClosesTheModalStackBeforeThePanel()
        {
            var coordinator = new UiNavigationCoordinator();
            var panel = CreateView("Panel", UiViewKind.MainPanel, UiOutsideDismissPolicy.NotDismissible);
            var modalA = CreateView("ModalA", UiViewKind.Modal, UiOutsideDismissPolicy.NotDismissible);
            var modalB = CreateView("ModalB", UiViewKind.Modal, UiOutsideDismissPolicy.NotDismissible);

            coordinator.OpenMainPanel(panel);
            coordinator.PushModal(modalA);
            coordinator.PushModal(modalB);

            Assert.That(coordinator.TryHandleBack(), Is.True);
            Assert.That(modalB.IsOpen, Is.False);
            Assert.That(modalA.IsOpen, Is.True);
            Assert.That(panel.IsOpen, Is.True);

            Assert.That(coordinator.TryHandleBack(), Is.True);
            Assert.That(modalA.IsOpen, Is.False);
            Assert.That(panel.IsOpen, Is.True);

            Assert.That(coordinator.TryHandleBack(), Is.True);
            Assert.That(panel.IsOpen, Is.False);
        }

        [Test]
        public void AT010_OutsideDismissOnCriticalModal_IsNotHandled()
        {
            var coordinator = new UiNavigationCoordinator();
            var criticalModal = CreateView("Critical", UiViewKind.Modal, UiOutsideDismissPolicy.NotDismissible);

            coordinator.PushModal(criticalModal);
            var handled = coordinator.RequestOutsideDismiss();

            Assert.That(handled, Is.False);
            Assert.That(criticalModal.IsOpen, Is.True);
        }

        [Test]
        public void AT011_OutsideDismissOnOrdinaryBottomSheet_ClosesTheSheet()
        {
            var coordinator = new UiNavigationCoordinator();
            var bottomSheet = CreateView("Ordinary", UiViewKind.BottomSheet, UiOutsideDismissPolicy.Dismissible);

            coordinator.OpenBottomSheet(bottomSheet);
            var handled = coordinator.RequestOutsideDismiss();

            Assert.That(handled, Is.True);
            Assert.That(bottomSheet.IsOpen, Is.False);
        }

        [Test]
        public void AT012_DestroyedOrRepeatedlyClosedHandle_CleansUpSafely()
        {
            var coordinator = new UiNavigationCoordinator();
            var panel = CreateView("Panel", UiViewKind.MainPanel, UiOutsideDismissPolicy.NotDismissible);
            var handle = coordinator.OpenMainPanel(panel);

            panel.Destroy();

            Assert.DoesNotThrow(() => handle.Close());
            Assert.That(coordinator.ActiveMainPanel, Is.Null);
            Assert.DoesNotThrow(() => handle.Close());
            Assert.That(coordinator.TryHandleBack(), Is.False);
        }

        private UiView CreateView(
            string viewId,
            UiViewKind kind,
            UiOutsideDismissPolicy outsideDismissPolicy)
        {
            return new UiView(viewId, kind, UiPausePolicy.ContinueGame, outsideDismissPolicy);
        }
    }
}

using AnimalCafe.UI.Foundation;
using NUnit.Framework;

namespace AnimalCafe.Tests.Phase5
{
    public sealed class UiPointerBoundaryTests
    {
        [Test]
        public void AT023_UiPress_RemainsUiOwnedUntilRelease()
        {
            var boundary = new UiPointerBoundary();

            boundary.RegisterUiPointerPress(0);

            Assert.That(boundary.GetOwnership(0), Is.EqualTo(UiPointerOwnership.Ui));
            Assert.That(boundary.CanProcessScenePointer(0), Is.False);

            boundary.ReleasePointer(0);

            Assert.That(boundary.GetOwnership(0), Is.EqualTo(UiPointerOwnership.None));
        }

        [Test]
        public void AT024_ScenePress_CannotBeClaimedByUiBeforeRelease()
        {
            var boundary = new UiPointerBoundary();

            boundary.RegisterScenePointerPress(0);
            boundary.RegisterUiPointerPress(0);

            Assert.That(boundary.GetOwnership(0), Is.EqualTo(UiPointerOwnership.Scene));
            Assert.That(boundary.CanProcessScenePointer(0), Is.True);
        }

        [Test]
        public void AT025_Release_ClearsOwnershipForTheNextGesture()
        {
            var boundary = new UiPointerBoundary();
            boundary.RegisterUiPointerPress(0);
            boundary.ReleasePointer(0);

            boundary.RegisterScenePointerPress(0);

            Assert.That(boundary.GetOwnership(0), Is.EqualTo(UiPointerOwnership.Scene));
            Assert.That(boundary.CanProcessScenePointer(0), Is.True);
        }

        [Test]
        public void AT026_TwoPointerIds_KeepIndependentOwnership()
        {
            var boundary = new UiPointerBoundary();

            boundary.RegisterUiPointerPress(3);
            boundary.RegisterScenePointerPress(7);

            Assert.That(boundary.GetOwnership(3), Is.EqualTo(UiPointerOwnership.Ui));
            Assert.That(boundary.GetOwnership(7), Is.EqualTo(UiPointerOwnership.Scene));
            Assert.That(boundary.CanProcessScenePointer(3), Is.False);
            Assert.That(boundary.CanProcessScenePointer(7), Is.True);
        }

        [Test]
        public void AT027_ModalSceneBlock_BlocksEveryScenePointer()
        {
            var boundary = new UiPointerBoundary();
            boundary.RegisterScenePointerPress(0);
            boundary.RegisterScenePointerPress(1);

            using (boundary.AcquireSceneBlock())
            {
                Assert.That(boundary.CanProcessScenePointer(0), Is.False);
                Assert.That(boundary.CanProcessScenePointer(1), Is.False);
            }

            Assert.That(boundary.CanProcessScenePointer(0), Is.True);
            Assert.That(boundary.CanProcessScenePointer(1), Is.True);
        }

        [Test]
        public void AT028_ToastWithoutModal_DoesNotClaimPointerOwnership()
        {
            var boundary = new UiPointerBoundary();

            boundary.NotifyToastShown();

            Assert.That(boundary.GetOwnership(42), Is.EqualTo(UiPointerOwnership.None));
            Assert.That(boundary.CanProcessScenePointer(42), Is.True);
        }
    }
}

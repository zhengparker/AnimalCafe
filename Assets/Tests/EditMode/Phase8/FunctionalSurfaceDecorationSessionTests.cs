using System.Linq;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class FunctionalSurfaceDecorationSessionTests
    {
        private const string SupportDefinitionId = "furniture.counter.long";
        private const string CashRegisterDefinitionId = "equipment.cash-register";
        private const string SupportInstanceId = "7f17d8fa59f64be0a6689666ce4a28d2";
        private const string CashRegisterInstanceId = "9f17d8fa59f64be0a6689666ce4a28d2";
        private const string PickUpPointInstanceId = "af17d8fa59f64be0a6689666ce4a28d2";
        private const string ConflictInstanceId = "bf17d8fa59f64be0a6689666ce4a28d2";

        [Test]
        public void BeginCreateMounted_CancelDiscardsOnlyPreviewWithoutOccupyingSlot()
        {
            var layout = CreateFunctionalLayout();
            var address = Address("slot.0");
            var session = new FunctionalSurfaceDecorationSession(layout);

            var begin = session.BeginCreateMounted(CashRegisterDefinitionId, address);

            Assert.That(begin.Succeeded, Is.True);
            Assert.That(session.ActivePreview.Kind,
                Is.EqualTo(FunctionalSurfacePreviewKind.MountedEquipment));
            Assert.That(session.ActivePreview.DefinitionId,
                Is.EqualTo(CashRegisterDefinitionId));
            Assert.That(session.ActivePreview.Address, Is.EqualTo(address));
            Assert.That(session.ActivePreview.Rotation,
                Is.EqualTo(FurnitureRotation.Degrees0));
            Assert.That(session.ActivePreview.IsNew, Is.True);
            Assert.That(session.ActivePreview.CanConfirm, Is.True);
            Assert.That(layout.MountedInstances, Is.Empty);
            Assert.That(layout.TryGetOccupant(address, out _), Is.False);

            session.Cancel();

            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(layout.MountedInstances, Is.Empty);
            Assert.That(layout.TryGetOccupant(address, out _), Is.False);
        }

        [Test]
        public void BeginMoveMounted_MoveRotateThenCancelPreservesOriginalConfirmedState()
        {
            var layout = CreateFunctionalLayout();
            var originalAddress = Address("slot.0");
            var targetAddress = Address("slot.1");
            Assert.That(layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId,
                CashRegisterDefinitionId,
                originalAddress,
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);
            var session = new FunctionalSurfaceDecorationSession(layout);

            Assert.That(session.BeginMoveMounted(CashRegisterInstanceId).Succeeded, Is.True);
            Assert.That(session.MovePreview(targetAddress).Succeeded, Is.True);
            Assert.That(session.RotatePreview().Succeeded, Is.True);
            Assert.That(session.ActivePreview.Address, Is.EqualTo(targetAddress));
            Assert.That(session.ActivePreview.Rotation,
                Is.EqualTo(FurnitureRotation.Degrees90));
            Assert.That(layout.MountedInstances.Single().Address, Is.EqualTo(originalAddress));
            Assert.That(layout.MountedInstances.Single().Rotation,
                Is.EqualTo(FurnitureRotation.Degrees0));

            session.Cancel();

            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(layout.MountedInstances.Single().InstanceId,
                Is.EqualTo(CashRegisterInstanceId));
            Assert.That(layout.MountedInstances.Single().Address, Is.EqualTo(originalAddress));
            Assert.That(layout.MountedInstances.Single().Rotation,
                Is.EqualTo(FurnitureRotation.Degrees0));
            Assert.That(layout.TryGetOccupant(originalAddress, out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(CashRegisterInstanceId));
            Assert.That(layout.TryGetOccupant(targetAddress, out _), Is.False);
        }

        [Test]
        public void BeginCreatePickUp_CancelDiscardsOnlyPreviewWithoutOccupyingSlot()
        {
            var layout = CreateFunctionalLayout();
            var address = Address("slot.1");
            var session = new FunctionalSurfaceDecorationSession(layout);

            var begin = session.BeginCreatePickUp(address);

            Assert.That(begin.Succeeded, Is.True);
            Assert.That(session.ActivePreview.Kind,
                Is.EqualTo(FunctionalSurfacePreviewKind.PickUpPoint));
            Assert.That(session.ActivePreview.DefinitionId, Is.Null);
            Assert.That(session.ActivePreview.IsNew, Is.True);
            Assert.That(session.ActivePreview.CanConfirm, Is.True);
            Assert.That(layout.PickUpPoints, Is.Empty);

            session.Cancel();

            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(layout.PickUpPoints, Is.Empty);
            Assert.That(layout.TryGetOccupant(address, out _), Is.False);
        }

        [Test]
        public void BeginCreatePickUp_NoWalkableAdjacentAnchorCreatesInvalidPreview()
        {
            var cafeLayout = CreateCafeLayout();
            cafeLayout.AddReservation(new LayoutReservation(
                "blocked.pickup-begin",
                LayoutReservationType.Blocked,
                new GridPosition(1, 1),
                new GridSize(1, 1)));
            var layout = CreateFunctionalLayout(cafeLayout);
            var address = Address("slot.1");
            var session = new FunctionalSurfaceDecorationSession(layout);

            var result = session.BeginCreatePickUp(address);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.NoValidInteractionAnchor));
            Assert.That(session.ActivePreview, Is.Not.Null);
            Assert.That(session.ActivePreview.CanConfirm, Is.False);
            Assert.That(layout.PickUpPoints, Is.Empty);
            Assert.That(layout.TryGetOccupant(address, out _), Is.False);
        }

        [Test]
        public void Confirm_NewPickUpRejectsCurrentAnchorBlockWithoutPublishingOccupancy()
        {
            var cafeLayout = CreateCafeLayout();
            var layout = CreateFunctionalLayout(cafeLayout);
            var address = Address("slot.1");
            var session = new FunctionalSurfaceDecorationSession(layout);
            Assert.That(session.BeginCreatePickUp(address).Succeeded, Is.True);
            cafeLayout.AddReservation(new LayoutReservation(
                "blocked.pickup-before-confirm",
                LayoutReservationType.Blocked,
                new GridPosition(1, 1),
                new GridSize(1, 1)));

            var result = session.Confirm();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.NoValidInteractionAnchor));
            Assert.That(session.ActivePreview, Is.Not.Null);
            Assert.That(session.ActivePreview.CanConfirm, Is.False);
            Assert.That(layout.PickUpPoints, Is.Empty);
            Assert.That(layout.TryGetOccupant(address, out _), Is.False);
        }

        [Test]
        public void Confirm_MovedPickUpRejectsCurrentAnchorBlockAndPreservesOriginalBinding()
        {
            var cafeLayout = CreateCafeLayout();
            var layout = CreateFunctionalLayout(cafeLayout);
            var originalAddress = Address("slot.0");
            var targetAddress = Address("slot.1");
            Assert.That(layout.PlacePickUp(new PickUpPointInstance(
                PickUpPointInstanceId,
                originalAddress)).Succeeded,
                Is.True);
            var session = new FunctionalSurfaceDecorationSession(layout);
            Assert.That(session.BeginMovePickUp(PickUpPointInstanceId).Succeeded, Is.True);
            Assert.That(session.MovePreview(targetAddress).Succeeded, Is.True);
            cafeLayout.AddReservation(new LayoutReservation(
                "blocked.pickup-move-before-confirm",
                LayoutReservationType.Blocked,
                new GridPosition(1, 1),
                new GridSize(1, 1)));

            var result = session.Confirm();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.NoValidInteractionAnchor));
            Assert.That(session.ActivePreview, Is.Not.Null);
            Assert.That(session.ActivePreview.CanConfirm, Is.False);
            Assert.That(layout.PickUpPoints.Single().InstanceId,
                Is.EqualTo(PickUpPointInstanceId));
            Assert.That(layout.PickUpPoints.Single().Address, Is.EqualTo(originalAddress));
            Assert.That(layout.TryGetOccupant(originalAddress, out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(PickUpPointInstanceId));
            Assert.That(layout.TryGetOccupant(targetAddress, out _), Is.False);
        }

        [Test]
        public void BeginMovePickUp_MoveThenCancelPreservesAddressAndIdentity()
        {
            var layout = CreateFunctionalLayout();
            var originalAddress = Address("slot.1");
            var targetAddress = Address("slot.2");
            Assert.That(layout.PlacePickUp(new PickUpPointInstance(
                PickUpPointInstanceId,
                originalAddress)).Succeeded,
                Is.True);
            var session = new FunctionalSurfaceDecorationSession(layout);

            Assert.That(session.BeginMovePickUp(PickUpPointInstanceId).Succeeded, Is.True);
            Assert.That(session.MovePreview(targetAddress).Succeeded, Is.True);
            session.Cancel();

            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(layout.PickUpPoints.Single().InstanceId,
                Is.EqualTo(PickUpPointInstanceId));
            Assert.That(layout.PickUpPoints.Single().Address, Is.EqualTo(originalAddress));
            Assert.That(layout.TryGetOccupant(originalAddress, out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(PickUpPointInstanceId));
            Assert.That(layout.TryGetOccupant(targetAddress, out _), Is.False);
        }

        [Test]
        public void RotatePreview_PickUpReturnsUnsupportedActionWithoutChangingPreview()
        {
            var layout = CreateFunctionalLayout();
            var address = Address("slot.1");
            var session = new FunctionalSurfaceDecorationSession(layout);
            Assert.That(session.BeginCreatePickUp(address).Succeeded, Is.True);
            var before = session.ActivePreview;

            var result = session.RotatePreview();

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.UnsupportedAction));
            Assert.That(session.ActivePreview, Is.SameAs(before));
            Assert.That(session.ActivePreview.Address, Is.EqualTo(address));
            Assert.That(session.ActivePreview.Rotation,
                Is.EqualTo(FurnitureRotation.Degrees0));
            Assert.That(session.ActivePreview.Validation.Succeeded, Is.True);
            Assert.That(layout.PickUpPoints, Is.Empty);
        }

        [Test]
        public void Confirm_NewMountedPublishesPreviewAddressAndRotationExactlyOnce()
        {
            var layout = CreateFunctionalLayout();
            var address = Address("slot.2");
            var session = new FunctionalSurfaceDecorationSession(layout);
            Assert.That(session.BeginCreateMounted(CashRegisterDefinitionId, address).Succeeded,
                Is.True);
            Assert.That(session.RotatePreview().Succeeded, Is.True);
            var previewId = session.ActivePreview.InstanceId;

            var result = session.Confirm();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(layout.MountedInstances, Has.Count.EqualTo(1));
            Assert.That(layout.MountedInstances.Single().InstanceId, Is.EqualTo(previewId));
            Assert.That(layout.MountedInstances.Single().Address, Is.EqualTo(address));
            Assert.That(layout.MountedInstances.Single().Rotation,
                Is.EqualTo(FurnitureRotation.Degrees90));
            Assert.That(session.Confirm().FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.InstanceNotFound));
            Assert.That(layout.MountedInstances, Has.Count.EqualTo(1));
        }

        [TestCase(FunctionalSurfacePreviewKind.MountedEquipment)]
        [TestCase(FunctionalSurfacePreviewKind.PickUpPoint)]
        public void ConfirmStore_ExistingContentRemovesOnlySelectedItemAndReleasesSlot(
            FunctionalSurfacePreviewKind kind)
        {
            var layout = CreateFunctionalLayout();
            var selectedAddress = Address("slot.0");
            var otherAddress = Address("slot.1");
            Assert.That(layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId,
                CashRegisterDefinitionId,
                selectedAddress,
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);
            Assert.That(layout.PlacePickUp(new PickUpPointInstance(
                PickUpPointInstanceId,
                otherAddress)).Succeeded,
                Is.True);
            var session = new FunctionalSurfaceDecorationSession(layout);
            var begin = kind == FunctionalSurfacePreviewKind.MountedEquipment
                ? session.BeginMoveMounted(CashRegisterInstanceId)
                : session.BeginMovePickUp(PickUpPointInstanceId);
            Assert.That(begin.Succeeded, Is.True);

            var result = session.ConfirmStore();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.ActivePreview, Is.Null);
            if (kind == FunctionalSurfacePreviewKind.MountedEquipment)
            {
                Assert.That(layout.MountedInstances, Is.Empty);
                Assert.That(layout.PickUpPoints.Single().InstanceId,
                    Is.EqualTo(PickUpPointInstanceId));
                Assert.That(layout.TryGetOccupant(selectedAddress, out _), Is.False);
                Assert.That(layout.TryGetOccupant(otherAddress, out var other), Is.True);
                Assert.That(other, Is.EqualTo(PickUpPointInstanceId));
            }
            else
            {
                Assert.That(layout.PickUpPoints, Is.Empty);
                Assert.That(layout.MountedInstances.Single().InstanceId,
                    Is.EqualTo(CashRegisterInstanceId));
                Assert.That(layout.TryGetOccupant(otherAddress, out _), Is.False);
                Assert.That(layout.TryGetOccupant(selectedAddress, out var other), Is.True);
                Assert.That(other, Is.EqualTo(CashRegisterInstanceId));
            }
        }

        [Test]
        public void Confirm_ConflictInjectedAfterValidPreviewIsAtomicAndKeepsPreviewRecoverable()
        {
            var layout = CreateFunctionalLayout();
            var originalAddress = Address("slot.0");
            var targetAddress = Address("slot.1");
            Assert.That(layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId,
                CashRegisterDefinitionId,
                originalAddress,
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);
            var session = new FunctionalSurfaceDecorationSession(layout);
            Assert.That(session.BeginMoveMounted(CashRegisterInstanceId).Succeeded, Is.True);
            Assert.That(session.MovePreview(targetAddress).Succeeded, Is.True);
            Assert.That(session.RotatePreview().Succeeded, Is.True);
            Assert.That(session.ActivePreview.CanConfirm, Is.True);

            Assert.That(layout.PlacePickUp(new PickUpPointInstance(
                ConflictInstanceId,
                targetAddress)).Succeeded,
                Is.True);

            var result = session.Confirm();

            Assert.That(result.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.SlotOccupied));
            Assert.That(session.ActivePreview, Is.Not.Null);
            Assert.That(session.ActivePreview.Validation.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.SlotOccupied));
            Assert.That(session.ActivePreview.CanConfirm, Is.False);
            Assert.That(layout.MountedInstances, Has.Count.EqualTo(1));
            Assert.That(layout.MountedInstances.Single().InstanceId,
                Is.EqualTo(CashRegisterInstanceId));
            Assert.That(layout.MountedInstances.Single().Address, Is.EqualTo(originalAddress));
            Assert.That(layout.MountedInstances.Single().Rotation,
                Is.EqualTo(FurnitureRotation.Degrees0));
            Assert.That(layout.TryGetOccupant(originalAddress, out var original), Is.True);
            Assert.That(original, Is.EqualTo(CashRegisterInstanceId));
            Assert.That(layout.TryGetOccupant(targetAddress, out var target), Is.True);
            Assert.That(target, Is.EqualTo(ConflictInstanceId));

            session.Cancel();
            Assert.That(layout.MountedInstances.Single().Address, Is.EqualTo(originalAddress));
        }

        [Test]
        public void Confirm_InitiallyInvalidPreviewFreshlyRevalidatesAfterOccupantIsRemoved()
        {
            var layout = CreateFunctionalLayout();
            var originalAddress = Address("slot.0");
            var targetAddress = Address("slot.1");
            Assert.That(layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId,
                CashRegisterDefinitionId,
                originalAddress,
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);
            Assert.That(layout.PlacePickUp(new PickUpPointInstance(
                ConflictInstanceId,
                targetAddress)).Succeeded,
                Is.True);
            var session = new FunctionalSurfaceDecorationSession(layout);
            Assert.That(session.BeginMoveMounted(CashRegisterInstanceId).Succeeded, Is.True);
            Assert.That(session.MovePreview(targetAddress).FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.SlotOccupied));
            Assert.That(session.ActivePreview.CanConfirm, Is.False);

            Assert.That(layout.RemovePickUp(ConflictInstanceId).Succeeded, Is.True);

            var result = session.Confirm();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(layout.MountedInstances, Has.Count.EqualTo(1));
            Assert.That(layout.MountedInstances.Single().InstanceId,
                Is.EqualTo(CashRegisterInstanceId));
            Assert.That(layout.MountedInstances.Single().Address, Is.EqualTo(targetAddress));
            Assert.That(layout.MountedInstances.Single().Rotation,
                Is.EqualTo(FurnitureRotation.Degrees0));
            Assert.That(layout.TryGetOccupant(originalAddress, out _), Is.False);
            Assert.That(layout.TryGetOccupant(targetAddress, out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(CashRegisterInstanceId));
            Assert.That(layout.PickUpPoints, Is.Empty);
        }

        [Test]
        public void Confirm_ExistingMountedPublishesCombinedMoveAndRotationOnce()
        {
            var layout = CreateFunctionalLayout();
            var originalAddress = Address("slot.0");
            var targetAddress = Address("slot.2");
            Assert.That(layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId,
                CashRegisterDefinitionId,
                originalAddress,
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);
            var session = new FunctionalSurfaceDecorationSession(layout);

            Assert.That(session.BeginMoveMounted(CashRegisterInstanceId).Succeeded, Is.True);
            Assert.That(session.MovePreview(targetAddress).Succeeded, Is.True);
            Assert.That(session.RotatePreview().Succeeded, Is.True);

            var result = session.Confirm();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(layout.MountedInstances, Has.Count.EqualTo(1));
            Assert.That(layout.MountedInstances.Single().InstanceId,
                Is.EqualTo(CashRegisterInstanceId));
            Assert.That(layout.MountedInstances.Single().Address, Is.EqualTo(targetAddress));
            Assert.That(layout.MountedInstances.Single().Rotation,
                Is.EqualTo(FurnitureRotation.Degrees90));
            Assert.That(layout.TryGetOccupant(originalAddress, out _), Is.False);
            Assert.That(layout.TryGetOccupant(targetAddress, out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(CashRegisterInstanceId));
        }

        private static FunctionalSurfaceLayout CreateFunctionalLayout(CafeLayout cafeLayout = null)
        {
            var catalog = new FurnitureDefinitionCatalog(new[]
            {
                new FurnitureDefinition(
                    SupportDefinitionId,
                    "Long Counter",
                    new GridSize(3, 1),
                    PlacementSurfaceType.Floor),
                new FurnitureDefinition(
                    CashRegisterDefinitionId,
                    "Cash Register",
                    new GridSize(1, 1),
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CashRegister)
            });
            cafeLayout ??= CreateCafeLayout();

            return new FunctionalSurfaceLayout(
                cafeLayout,
                catalog,
                new SurfaceSlotCatalog(new[]
                {
                    new SurfaceSlotDefinition(
                        SupportDefinitionId,
                        "slot.0",
                        new GridPosition(0, 0)),
                    new SurfaceSlotDefinition(
                        SupportDefinitionId,
                        "slot.1",
                        new GridPosition(1, 0)),
                    new SurfaceSlotDefinition(
                        SupportDefinitionId,
                        "slot.2",
                        new GridPosition(2, 0))
                }));
        }

        private static CafeLayout CreateCafeLayout()
        {
            var catalog = new FurnitureDefinitionCatalog(new[]
            {
                new FurnitureDefinition(
                    SupportDefinitionId,
                    "Long Counter",
                    new GridSize(3, 1),
                    PlacementSurfaceType.Floor),
                new FurnitureDefinition(
                    CashRegisterDefinitionId,
                    "Cash Register",
                    new GridSize(1, 1),
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CashRegister)
            });
            var cafeLayout = new CafeLayout(new GridSettings(1f), catalog);
            cafeLayout.AddRegion(new LayoutRegion(
                "region.main", new GridPosition(0, 0), new GridSize(8, 8), LayoutZoneType.Interior));
            Assert.That(cafeLayout.PlaceFurniture(FurnitureInstance.Restore(
                SupportInstanceId, SupportDefinitionId, new GridPosition(0, 0), FurnitureRotation.Degrees0)).Succeeded,
                Is.True);
            return cafeLayout;
        }

        private static SurfaceSlotAddress Address(string slotId)
        {
            return new SurfaceSlotAddress(SupportInstanceId, slotId);
        }
    }
}

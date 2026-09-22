using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class FunctionalSurfaceLayoutTests
    {
        private const string SupportDefinitionId = "furniture.counter.long";
        private const string CashRegisterDefinitionId = "equipment.cash-register";
        private const string CoffeeMachineDefinitionId = "equipment.coffee-machine";
        private const string FloorOnlyDefinitionId = "equipment.floor-only";
        private const string SupportInstanceId = "7f17d8fa59f64be0a6689666ce4a28d2";
        private const string OtherSupportInstanceId = "8a28e9ab60a75cf1b7790777df5b39e3";
        private const string CashRegisterInstanceId = "9f17d8fa59f64be0a6689666ce4a28d2";
        private const string CoffeeMachineInstanceId = "af17d8fa59f64be0a6689666ce4a28d2";
        private const string PickUpPointInstanceId = "bf17d8fa59f64be0a6689666ce4a28d2";
        private const string SecondPickUpPointInstanceId = "cf17d8fa59f64be0a6689666ce4a28d2";

        [Test]
        public void PlaceMounted_CompatibleCashRegisterOccupiesItsSurfaceSlot()
        {
            var layout = CreateFunctionalLayout();
            var address = Address("slot.0");

            var result = layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId,
                CashRegisterDefinitionId,
                address,
                FurnitureRotation.Degrees0));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(layout.MountedInstances, Has.Count.EqualTo(1));
            Assert.That(layout.TryGetOccupant(address, out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(CashRegisterInstanceId));
        }

        [Test]
        public void PlaceMounted_CompatibleCoffeeMachineDoesNotAlterFloorFurnitureOccupancy()
        {
            var cafeLayout = CreateCafeLayout();
            var functionalLayout = CreateFunctionalLayout(cafeLayout);
            var floorCount = cafeLayout.OccupiedCellCount;

            var result = functionalLayout.PlaceMounted(new SurfaceMountedInstance(
                CoffeeMachineInstanceId,
                CoffeeMachineDefinitionId,
                Address("slot.1"),
                FurnitureRotation.Degrees90));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(cafeLayout.OccupiedCellCount, Is.EqualTo(floorCount));
        }

        [Test]
        public void PlaceMounted_MoveRotateAndRemovePreserveTheConfirmedInstanceIdentity()
        {
            var layout = CreateFunctionalLayout();
            var firstAddress = Address("slot.0");
            var secondAddress = Address("slot.1");
            Assert.That(layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId,
                CashRegisterDefinitionId,
                firstAddress,
                FurnitureRotation.Degrees0)).Succeeded, Is.True);

            Assert.That(layout.MoveMounted(CashRegisterInstanceId, secondAddress).Succeeded, Is.True);
            Assert.That(layout.RotateMounted(CashRegisterInstanceId, FurnitureRotation.Degrees270).Succeeded,
                Is.True);
            Assert.That(layout.TryGetOccupant(firstAddress, out _), Is.False);
            Assert.That(layout.TryGetOccupant(secondAddress, out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(CashRegisterInstanceId));
            Assert.That(layout.MountedInstances.Single().Rotation, Is.EqualTo(FurnitureRotation.Degrees270));

            Assert.That(layout.RemoveMounted(CashRegisterInstanceId).Succeeded, Is.True);
            Assert.That(layout.MountedInstances, Is.Empty);
            Assert.That(layout.TryGetOccupant(secondAddress, out _), Is.False);
        }

        [Test]
        public void PlacePickUp_MoveAndRemoveUseTheSameSharedSurfaceSlotOccupancy()
        {
            var layout = CreateFunctionalLayout();
            var firstAddress = Address("slot.1");
            var secondAddress = Address("slot.2");
            Assert.That(layout.PlacePickUp(new PickUpPointInstance(PickUpPointInstanceId, firstAddress)).Succeeded,
                Is.True);

            Assert.That(layout.MovePickUp(PickUpPointInstanceId, secondAddress).Succeeded, Is.True);
            Assert.That(layout.TryGetOccupant(firstAddress, out _), Is.False);
            Assert.That(layout.TryGetOccupant(secondAddress, out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(PickUpPointInstanceId));

            Assert.That(layout.RemovePickUp(PickUpPointInstanceId).Succeeded, Is.True);
            Assert.That(layout.PickUpPoints, Is.Empty);
            Assert.That(layout.TryGetOccupant(secondAddress, out _), Is.False);
        }

        [Test]
        public void PlacePickUp_NoWalkableAdjacentAnchorFailsWithoutOccupyingSlot()
        {
            var cafeLayout = CreateCafeLayout();
            cafeLayout.AddReservation(new LayoutReservation(
                "blocked.pickup-only-exit",
                LayoutReservationType.Blocked,
                new GridPosition(1, 1),
                new GridSize(1, 1)));
            var layout = CreateFunctionalLayout(cafeLayout);
            var address = Address("slot.1");

            var result = layout.PlacePickUp(new PickUpPointInstance(
                PickUpPointInstanceId,
                address));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.NoValidInteractionAnchor));
            Assert.That(layout.PickUpPoints, Is.Empty);
            Assert.That(layout.TryGetOccupant(address, out _), Is.False);
        }

        [Test]
        public void PlaceMounted_AllowsMultipleIndependentFunctionalInstanceIds()
        {
            var layout = CreateFunctionalLayout();

            Assert.That(layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId, CashRegisterDefinitionId, Address("slot.0"), FurnitureRotation.Degrees0)).Succeeded,
                Is.True);
            Assert.That(layout.PlaceMounted(new SurfaceMountedInstance(
                CoffeeMachineInstanceId, CoffeeMachineDefinitionId, Address("slot.1"), FurnitureRotation.Degrees180)).Succeeded,
                Is.True);
            Assert.That(layout.PlacePickUp(new PickUpPointInstance(PickUpPointInstanceId, Address("slot.2"))).Succeeded,
                Is.True);

            Assert.That(layout.MountedInstances.Select(instance => instance.InstanceId),
                Is.EqualTo(new[] { CashRegisterInstanceId, CoffeeMachineInstanceId }));
            Assert.That(layout.PickUpPoints.Select(instance => instance.InstanceId),
                Is.EqualTo(new[] { PickUpPointInstanceId }));
            Assert.That(layout.GetContentIdsForSupport(SupportInstanceId),
                Is.EqualTo(new[] { CashRegisterInstanceId, CoffeeMachineInstanceId, PickUpPointInstanceId }));
        }

        [Test]
        public void PlaceMounted_OccupiedSlotFailsWithoutChangingOriginalOccupantOrCounts()
        {
            var layout = CreateFunctionalLayout();
            var occupiedAddress = Address("slot.0");
            Assert.That(layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId, CashRegisterDefinitionId, occupiedAddress, FurnitureRotation.Degrees0)).Succeeded,
                Is.True);

            var result = layout.PlaceMounted(new SurfaceMountedInstance(
                CoffeeMachineInstanceId, CoffeeMachineDefinitionId, occupiedAddress, FurnitureRotation.Degrees0));

            Assert.That(result.FailureReason, Is.EqualTo(FunctionalSurfacePlacementFailureReason.SlotOccupied));
            Assert.That(layout.MountedInstances, Has.Count.EqualTo(1));
            Assert.That(layout.TryGetOccupant(occupiedAddress, out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(CashRegisterInstanceId));
        }

        [Test]
        public void PlacePickUp_SecondPointOnOccupiedSlotFailsWithoutChangingOriginalPoint()
        {
            var layout = CreateFunctionalLayout();
            var occupiedAddress = Address("slot.0");
            Assert.That(layout.PlacePickUp(new PickUpPointInstance(PickUpPointInstanceId, occupiedAddress)).Succeeded,
                Is.True);

            var result = layout.PlacePickUp(new PickUpPointInstance(
                SecondPickUpPointInstanceId, occupiedAddress));

            Assert.That(result.FailureReason, Is.EqualTo(FunctionalSurfacePlacementFailureReason.SlotOccupied));
            Assert.That(layout.PickUpPoints.Single().Address, Is.EqualTo(occupiedAddress));
            Assert.That(layout.TryGetOccupant(occupiedAddress, out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(PickUpPointInstanceId));
        }

        [Test]
        public void PlaceMounted_UnsupportedSurfaceDefinitionFailsWithoutMutatingSurfaceOccupancy()
        {
            var layout = CreateFunctionalLayout();
            var address = Address("slot.0");

            var result = layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId,
                FloorOnlyDefinitionId,
                address,
                FurnitureRotation.Degrees0));

            Assert.That(result.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.UnsupportedPlacementSurface));
            Assert.That(layout.MountedInstances, Is.Empty);
            Assert.That(layout.TryGetOccupant(address, out _), Is.False);
        }

        [Test]
        public void PlaceMounted_MissingSupportOrSlotReturnsSpecificFailureWithoutMutation()
        {
            var layout = CreateFunctionalLayout();
            var missingSupport = new SurfaceSlotAddress(OtherSupportInstanceId, "slot.0");
            var missingSlot = Address("slot.missing");

            var missingSupportResult = layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId, CashRegisterDefinitionId, missingSupport, FurnitureRotation.Degrees0));
            var missingSlotResult = layout.PlaceMounted(new SurfaceMountedInstance(
                CoffeeMachineInstanceId, CoffeeMachineDefinitionId, missingSlot, FurnitureRotation.Degrees0));

            Assert.That(missingSupportResult.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.MissingSupportFurniture));
            Assert.That(missingSlotResult.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.MissingSurfaceSlot));
            Assert.That(layout.MountedInstances, Is.Empty);
            Assert.That(layout.PickUpPoints, Is.Empty);
        }

        [Test]
        public void FailedMoveOrRotationLeavesTheConfirmedAddressRotationAndOccupancyExact()
        {
            var layout = CreateFunctionalLayout();
            var originalAddress = Address("slot.0");
            var occupiedAddress = Address("slot.1");
            Assert.That(layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId, CashRegisterDefinitionId, originalAddress, FurnitureRotation.Degrees90)).Succeeded,
                Is.True);
            Assert.That(layout.PlacePickUp(new PickUpPointInstance(PickUpPointInstanceId, occupiedAddress)).Succeeded,
                Is.True);

            var moveResult = layout.MoveMounted(CashRegisterInstanceId, occupiedAddress);
            var rotationResult = layout.RotateMounted(CashRegisterInstanceId, (FurnitureRotation)45);

            Assert.That(moveResult.FailureReason, Is.EqualTo(FunctionalSurfacePlacementFailureReason.SlotOccupied));
            Assert.That(rotationResult.FailureReason, Is.EqualTo(FunctionalSurfacePlacementFailureReason.InvalidRotation));
            Assert.That(layout.MountedInstances.Single().Address, Is.EqualTo(originalAddress));
            Assert.That(layout.MountedInstances.Single().Rotation, Is.EqualTo(FurnitureRotation.Degrees90));
            Assert.That(layout.TryGetOccupant(originalAddress, out var originalOccupant), Is.True);
            Assert.That(originalOccupant, Is.EqualTo(CashRegisterInstanceId));
            Assert.That(layout.TryGetOccupant(occupiedAddress, out var occupiedOccupant), Is.True);
            Assert.That(occupiedOccupant, Is.EqualTo(PickUpPointInstanceId));
        }

        [Test]
        public void MoveMounted_InvalidAddressReturnsFailureWithoutChangingConfirmedOccupancy()
        {
            var layout = CreateFunctionalLayout();
            var originalAddress = Address("slot.0");
            Assert.That(layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId,
                CashRegisterDefinitionId,
                originalAddress,
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);

            var result = layout.MoveMounted(CashRegisterInstanceId, default);

            Assert.That(result.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.InvalidSurfaceSlotAddress));
            Assert.That(layout.MountedInstances.Single().Address, Is.EqualTo(originalAddress));
            Assert.That(layout.TryGetOccupant(originalAddress, out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(CashRegisterInstanceId));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void PlaceSurfaceContent_SameInstanceIdAcrossKindsFailsWithoutChangingTheOriginalContent(
            bool placeMountedFirst)
        {
            var layout = CreateFunctionalLayout();
            var sharedInstanceId = "df17d8fa59f64be0a6689666ce4a28d2";
            var address = Address("slot.0");
            var mounted = new SurfaceMountedInstance(
                sharedInstanceId,
                CashRegisterDefinitionId,
                address,
                FurnitureRotation.Degrees0);
            var pickUp = new PickUpPointInstance(sharedInstanceId, address);

            var secondResult = placeMountedFirst
                ? PlaceAfterMounted(layout, mounted, pickUp)
                : PlaceAfterPickUp(layout, mounted, pickUp);

            Assert.That(secondResult.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.InstanceAlreadyPlaced));
            Assert.That(layout.MountedInstances, Has.Count.EqualTo(placeMountedFirst ? 1 : 0));
            Assert.That(layout.PickUpPoints, Has.Count.EqualTo(placeMountedFirst ? 0 : 1));
            Assert.That(layout.TryGetOccupant(address, out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(sharedInstanceId));
            Assert.That(layout.GetContentIdsForSupport(SupportInstanceId),
                Is.EqualTo(new[] { sharedInstanceId }));
        }

        [Test]
        public void MovePickUp_OccupiedSlotFailsWithoutChangingEitherOccupantOrCount()
        {
            var layout = CreateFunctionalLayout();
            var originalAddress = Address("slot.0");
            var occupiedAddress = Address("slot.1");
            Assert.That(layout.PlacePickUp(new PickUpPointInstance(
                PickUpPointInstanceId,
                originalAddress)).Succeeded,
                Is.True);
            Assert.That(layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId,
                CashRegisterDefinitionId,
                occupiedAddress,
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);

            var result = layout.MovePickUp(PickUpPointInstanceId, occupiedAddress);

            Assert.That(result.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.SlotOccupied));
            AssertFailedPickUpMovePreservesConfirmedState(
                layout,
                originalAddress,
                occupiedAddress,
                CashRegisterInstanceId);
        }

        [Test]
        public void MovePickUp_NoWalkableAdjacentAnchorPreservesOriginalBindingAndOccupancy()
        {
            var cafeLayout = CreateCafeLayout();
            var layout = CreateFunctionalLayout(cafeLayout);
            var originalAddress = Address("slot.0");
            var targetAddress = Address("slot.1");
            Assert.That(layout.PlacePickUp(new PickUpPointInstance(
                PickUpPointInstanceId,
                originalAddress)).Succeeded,
                Is.True);
            cafeLayout.AddReservation(new LayoutReservation(
                "blocked.pickup-move-target",
                LayoutReservationType.Blocked,
                new GridPosition(1, 1),
                new GridSize(1, 1)));

            var result = layout.MovePickUp(PickUpPointInstanceId, targetAddress);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.NoValidInteractionAnchor));
            AssertFailedPickUpMovePreservesConfirmedState(
                layout,
                originalAddress,
                targetAddress,
                null);
        }

        [Test]
        public void MovePickUp_MissingSupportFailsWithoutChangingTheConfirmedPoint()
        {
            var layout = CreateFunctionalLayout();
            var originalAddress = Address("slot.0");
            Assert.That(layout.PlacePickUp(new PickUpPointInstance(
                PickUpPointInstanceId,
                originalAddress)).Succeeded,
                Is.True);

            var missingSupportAddress = new SurfaceSlotAddress(
                OtherSupportInstanceId,
                "slot.0");

            var result = layout.MovePickUp(
                PickUpPointInstanceId,
                missingSupportAddress);

            Assert.That(result.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.MissingSupportFurniture));
            AssertFailedPickUpMovePreservesConfirmedState(
                layout,
                originalAddress,
                missingSupportAddress,
                null);
        }

        [Test]
        public void MovePickUp_MissingSlotFailsWithoutChangingTheConfirmedPoint()
        {
            var layout = CreateFunctionalLayout();
            var originalAddress = Address("slot.0");
            var missingSlotAddress = Address("slot.missing");
            Assert.That(layout.PlacePickUp(new PickUpPointInstance(
                PickUpPointInstanceId,
                originalAddress)).Succeeded,
                Is.True);

            var result = layout.MovePickUp(PickUpPointInstanceId, missingSlotAddress);

            Assert.That(result.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.MissingSurfaceSlot));
            AssertFailedPickUpMovePreservesConfirmedState(
                layout,
                originalAddress,
                missingSlotAddress,
                null);
        }

        [Test]
        public void MovePickUp_DefaultAddressReturnsFailureWithoutChangingTheConfirmedPoint()
        {
            var layout = CreateFunctionalLayout();
            var originalAddress = Address("slot.0");
            Assert.That(layout.PlacePickUp(new PickUpPointInstance(
                PickUpPointInstanceId,
                originalAddress)).Succeeded,
                Is.True);

            var result = layout.MovePickUp(PickUpPointInstanceId, default);

            Assert.That(result.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.InvalidSurfaceSlotAddress));
            AssertFailedPickUpMovePreservesConfirmedState(
                layout,
                originalAddress,
                default,
                null);
        }

        private static FunctionalSurfaceLayout CreateFunctionalLayout(CafeLayout cafeLayout = null)
        {
            return new FunctionalSurfaceLayout(
                cafeLayout ?? CreateCafeLayout(),
                CreateCatalog(),
                new SurfaceSlotCatalog(new[]
                {
                    new SurfaceSlotDefinition(SupportDefinitionId, "slot.0", new GridPosition(0, 0)),
                    new SurfaceSlotDefinition(SupportDefinitionId, "slot.1", new GridPosition(1, 0)),
                    new SurfaceSlotDefinition(SupportDefinitionId, "slot.2", new GridPosition(2, 0))
                }));
        }

        private static CafeLayout CreateCafeLayout()
        {
            var layout = new CafeLayout(new GridSettings(1f), CreateCatalog());
            layout.AddRegion(new LayoutRegion(
                "region.main", new GridPosition(0, 0), new GridSize(10, 10), LayoutZoneType.Interior));
            Assert.That(layout.PlaceFurniture(FurnitureInstance.Restore(
                SupportInstanceId,
                SupportDefinitionId,
                new GridPosition(0, 0),
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);
            return layout;
        }

        private static FurnitureDefinitionCatalog CreateCatalog()
        {
            return new FurnitureDefinitionCatalog(new[]
            {
                new FurnitureDefinition(SupportDefinitionId, "Long Counter", new GridSize(3, 1), PlacementSurfaceType.Floor),
                new FurnitureDefinition(CashRegisterDefinitionId, "Cash Register", new GridSize(1, 1), PlacementSurfaceType.FurnitureSurface, FurnitureFunctionType.CashRegister),
                new FurnitureDefinition(CoffeeMachineDefinitionId, "Coffee Machine", new GridSize(1, 1), PlacementSurfaceType.FurnitureSurface, FurnitureFunctionType.CoffeeMachine),
                new FurnitureDefinition(FloorOnlyDefinitionId, "Floor Register", new GridSize(1, 1), PlacementSurfaceType.Floor, FurnitureFunctionType.CashRegister)
            });
        }

        private static SurfaceSlotAddress Address(string slotId)
        {
            return new SurfaceSlotAddress(SupportInstanceId, slotId);
        }

        private static FunctionalSurfacePlacementResult PlaceAfterMounted(
            FunctionalSurfaceLayout layout,
            SurfaceMountedInstance mounted,
            PickUpPointInstance pickUp)
        {
            Assert.That(layout.PlaceMounted(mounted).Succeeded, Is.True);
            return layout.PlacePickUp(pickUp);
        }

        private static FunctionalSurfacePlacementResult PlaceAfterPickUp(
            FunctionalSurfaceLayout layout,
            SurfaceMountedInstance mounted,
            PickUpPointInstance pickUp)
        {
            Assert.That(layout.PlacePickUp(pickUp).Succeeded, Is.True);
            return layout.PlaceMounted(mounted);
        }

        private static void AssertFailedPickUpMovePreservesConfirmedState(
            FunctionalSurfaceLayout layout,
            SurfaceSlotAddress originalAddress,
            SurfaceSlotAddress targetAddress,
            string expectedTargetOccupant)
        {
            Assert.That(layout.PickUpPoints, Has.Count.EqualTo(1));
            Assert.That(layout.MountedInstances,
                Has.Count.EqualTo(expectedTargetOccupant == null ? 0 : 1));
            Assert.That(layout.PickUpPoints.Single().Address, Is.EqualTo(originalAddress));
            Assert.That(layout.TryGetOccupant(originalAddress, out var originalOccupant), Is.True);
            Assert.That(originalOccupant, Is.EqualTo(PickUpPointInstanceId));
            Assert.That(layout.TryGetOccupant(targetAddress, out var targetOccupant),
                Is.EqualTo(expectedTargetOccupant != null));
            Assert.That(targetOccupant, Is.EqualTo(expectedTargetOccupant));
        }
    }
}

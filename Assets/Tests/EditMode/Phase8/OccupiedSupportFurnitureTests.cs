using System.Linq;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class OccupiedSupportFurnitureTests
    {
        private const string SupportDefinitionId = "furniture.counter.single";
        private const string CashRegisterDefinitionId = "equipment.cash-register";
        private const string SupportInstanceId = "7f17d8fa59f64be0a6689666ce4a28d2";
        private const string CashRegisterInstanceId = "9f17d8fa59f64be0a6689666ce4a28d2";
        private const string PickUpPointInstanceId = "af17d8fa59f64be0a6689666ce4a28d2";

        [Test]
        public void GetContentIdsForSupport_ReturnsAllConfirmedContentNeededToBlockStore()
        {
            var layout = CreateFunctionalLayout();
            Assert.That(layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId,
                CashRegisterDefinitionId,
                new SurfaceSlotAddress(SupportInstanceId, "slot.0"),
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);
            Assert.That(layout.PlacePickUp(new PickUpPointInstance(
                PickUpPointInstanceId,
                new SurfaceSlotAddress(SupportInstanceId, "slot.1"))).Succeeded,
                Is.True);

            var contentIds = layout.GetContentIdsForSupport(SupportInstanceId);

            Assert.That(contentIds, Is.EqualTo(new[]
            {
                CashRegisterInstanceId,
                PickUpPointInstanceId
            }));
        }

        [Test]
        public void GetContentIdsForSupport_RemovingOneContentDoesNotCascadeOrChangeOtherBindings()
        {
            var layout = CreateFunctionalLayout();
            var mountedAddress = new SurfaceSlotAddress(SupportInstanceId, "slot.0");
            var pickUpAddress = new SurfaceSlotAddress(SupportInstanceId, "slot.1");
            Assert.That(layout.PlaceMounted(new SurfaceMountedInstance(
                CashRegisterInstanceId,
                CashRegisterDefinitionId,
                mountedAddress,
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);
            Assert.That(layout.PlacePickUp(new PickUpPointInstance(PickUpPointInstanceId, pickUpAddress)).Succeeded,
                Is.True);

            Assert.That(layout.RemoveMounted(CashRegisterInstanceId).Succeeded, Is.True);

            Assert.That(layout.GetContentIdsForSupport(SupportInstanceId),
                Is.EqualTo(new[] { PickUpPointInstanceId }));
            Assert.That(layout.PickUpPoints.Single().Address, Is.EqualTo(pickUpAddress));
            Assert.That(layout.TryGetOccupant(pickUpAddress, out var occupant), Is.True);
            Assert.That(occupant, Is.EqualTo(PickUpPointInstanceId));
        }

        [Test]
        public void GetContentIdsForSupport_UnoccupiedSupportHasNoStoreBlockers()
        {
            var layout = CreateFunctionalLayout();

            Assert.That(layout.GetContentIdsForSupport(SupportInstanceId), Is.Empty);
        }

        private static FunctionalSurfaceLayout CreateFunctionalLayout()
        {
            var catalog = new FurnitureDefinitionCatalog(new[]
            {
                new FurnitureDefinition(SupportDefinitionId, "Single Counter", new GridSize(2, 1), PlacementSurfaceType.Floor),
                new FurnitureDefinition(CashRegisterDefinitionId, "Cash Register", new GridSize(1, 1), PlacementSurfaceType.FurnitureSurface, FurnitureFunctionType.CashRegister)
            });
            var cafeLayout = new CafeLayout(new GridSettings(1f), catalog);
            cafeLayout.AddRegion(new LayoutRegion(
                "region.main", new GridPosition(0, 0), new GridSize(4, 4), LayoutZoneType.Interior));
            Assert.That(cafeLayout.PlaceFurniture(FurnitureInstance.Restore(
                SupportInstanceId,
                SupportDefinitionId,
                new GridPosition(0, 0),
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);

            return new FunctionalSurfaceLayout(cafeLayout, catalog, new SurfaceSlotCatalog(new[]
            {
                new SurfaceSlotDefinition(SupportDefinitionId, "slot.0", new GridPosition(0, 0)),
                new SurfaceSlotDefinition(SupportDefinitionId, "slot.1", new GridPosition(1, 0))
            }));
        }
    }
}

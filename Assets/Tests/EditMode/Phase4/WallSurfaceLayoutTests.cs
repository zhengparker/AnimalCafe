using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.Phase4
{
    public sealed class WallSurfaceLayoutTests
    {
        [Test]
        public void OneByTwoFootprint_OccupiesSameColumnAcrossBothRows()
        {
            var wall = new WallSurfaceLayout("wall.back-right", 8, 2);
            var item = new WallMountedInstance(
                "window.01",
                "window.basic.01",
                "wall.back-right",
                new WallSlotPosition(3, 0),
                new WallFootprint(1, 2));

            Assert.That(wall.TryPlace(item).Succeeded, Is.True);
            Assert.That(
                wall.TryGetOccupant(new WallSlotPosition(3, 0), out var lowerOwner),
                Is.True);
            Assert.That(lowerOwner, Is.EqualTo("window.01"));
            Assert.That(
                wall.TryGetOccupant(new WallSlotPosition(3, 1), out var upperOwner),
                Is.True);
            Assert.That(upperOwner, Is.EqualTo("window.01"));
        }

        [Test]
        public void TwoByOneFootprint_AtValidPositionOccupiesTwoAdjacentColumns()
        {
            var wall = new WallSurfaceLayout("wall.back-right", 8, 2);
            var item = CreateItem(
                "sign.01",
                new WallSlotPosition(3, 0),
                new WallFootprint(2, 1));

            Assert.That(wall.TryPlace(item).Succeeded, Is.True);
            Assert.That(
                wall.TryGetOccupant(new WallSlotPosition(3, 0), out var leftOwner),
                Is.True);
            Assert.That(leftOwner, Is.EqualTo("sign.01"));
            Assert.That(
                wall.TryGetOccupant(new WallSlotPosition(4, 0), out var rightOwner),
                Is.True);
            Assert.That(rightOwner, Is.EqualTo("sign.01"));
            Assert.That(wall.OccupiedSlotCount, Is.EqualTo(2));
            Assert.That(wall.MountedItems, Is.EqualTo(new[] { item }));
        }

        [TestCase(0, 2)]
        [TestCase(-1, 2)]
        [TestCase(2, 0)]
        [TestCase(2, -1)]
        public void Constructor_RejectsNonPositiveWallDimensions(
            int columns,
            int rows)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new WallSurfaceLayout("wall.back-right", columns, rows));
        }

        [TestCase(0, 1)]
        [TestCase(-1, 1)]
        [TestCase(1, 0)]
        [TestCase(1, -1)]
        public void Footprint_RejectsNonPositiveDimensions(int width, int height)
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new WallFootprint(width, height));
        }

        [TestCase("")]
        [TestCase("Window Main")]
        public void WallMountedInstance_RejectsMalformedStableItemId(string itemId)
        {
            Assert.Throws<System.ArgumentException>(() =>
                CreateItem(
                    itemId,
                    new WallSlotPosition(0, 0),
                    new WallFootprint(1, 1)));
        }

        [Test]
        public void WallMountedInstance_RejectsNullStableItemId()
        {
            Assert.Throws<System.ArgumentNullException>(() =>
                CreateItem(
                    null,
                    new WallSlotPosition(0, 0),
                    new WallFootprint(1, 1)));
        }

        [Test]
        public void TryPlace_OneByTwoStartingAtFinalRow_IsRejectedWithoutOccupancy()
        {
            var wall = new WallSurfaceLayout("wall.back-right", 8, 2);

            var result = wall.TryPlace(CreateItem(
                "window.01",
                new WallSlotPosition(3, 1),
                new WallFootprint(1, 2)));

            Assert.That(result.FailureReason, Is.EqualTo(WallPlacementFailureReason.OutOfBounds));
            Assert.That(wall.OccupiedSlotCount, Is.Zero);
            Assert.That(wall.MountedItems, Is.Empty);
        }

        [Test]
        public void TryPlace_TwoByOneStartingAtFinalColumn_IsRejectedWithoutOccupancy()
        {
            var wall = new WallSurfaceLayout("wall.back-right", 8, 2);

            var result = wall.TryPlace(CreateItem(
                "sign.01",
                new WallSlotPosition(7, 0),
                new WallFootprint(2, 1)));

            Assert.That(result.FailureReason, Is.EqualTo(WallPlacementFailureReason.OutOfBounds));
            Assert.That(wall.OccupiedSlotCount, Is.Zero);
            Assert.That(wall.MountedItems, Is.Empty);
        }

        [Test]
        public void TryPlace_OverlappingItem_IsRejectedWithoutReplacingExistingOwner()
        {
            var wall = new WallSurfaceLayout("wall.back-right", 8, 2);
            Assert.That(wall.TryPlace(CreateItem(
                "window.01",
                new WallSlotPosition(3, 0),
                new WallFootprint(1, 2))).Succeeded, Is.True);

            var result = wall.TryPlace(CreateItem(
                "poster.01",
                new WallSlotPosition(3, 1),
                new WallFootprint(1, 1)));

            Assert.That(result.FailureReason, Is.EqualTo(WallPlacementFailureReason.Overlap));
            Assert.That(wall.OccupiedSlotCount, Is.EqualTo(2));
            Assert.That(
                wall.TryGetOccupant(new WallSlotPosition(3, 1), out var owner),
                Is.True);
            Assert.That(owner, Is.EqualTo("window.01"));
            Assert.That(wall.MountedItems, Has.Count.EqualTo(1));
        }

        [Test]
        public void TryPlace_ItemForAnotherSurface_IsRejectedWithoutOccupancy()
        {
            var wall = new WallSurfaceLayout("wall.back-right", 8, 2);

            var result = wall.TryPlace(new WallMountedInstance(
                "window.01",
                "window.basic.01",
                "wall.front-left",
                new WallSlotPosition(3, 0),
                new WallFootprint(1, 1)));

            Assert.That(result.FailureReason, Is.EqualTo(WallPlacementFailureReason.SurfaceMismatch));
            Assert.That(wall.OccupiedSlotCount, Is.Zero);
            Assert.That(wall.MountedItems, Is.Empty);
        }

        [Test]
        public void TryPlace_DuplicateItemId_IsRejectedWithoutReplacingOriginalItem()
        {
            var wall = new WallSurfaceLayout("wall.back-right", 8, 2);
            var original = CreateItem(
                "window.01",
                new WallSlotPosition(3, 0),
                new WallFootprint(1, 1));
            Assert.That(wall.TryPlace(original).Succeeded, Is.True);

            var result = wall.TryPlace(CreateItem(
                "window.01",
                new WallSlotPosition(4, 0),
                new WallFootprint(1, 1)));

            Assert.That(result.FailureReason, Is.EqualTo(WallPlacementFailureReason.ItemAlreadyPlaced));
            Assert.That(wall.MountedItems, Is.EqualTo(new[] { original }));
            Assert.That(wall.OccupiedSlotCount, Is.EqualTo(1));
            Assert.That(
                wall.TryGetOccupant(new WallSlotPosition(4, 0), out _),
                Is.False);
        }

        [Test]
        public void TryMove_OutOfBounds_PreservesOriginalPlacement()
        {
            var wall = new WallSurfaceLayout("wall.back-right", 8, 2);
            var original = CreateItem(
                "window.01",
                new WallSlotPosition(3, 0),
                new WallFootprint(1, 2));
            Assert.That(wall.TryPlace(original).Succeeded, Is.True);

            var result = wall.TryMove("window.01", new WallSlotPosition(3, 1));

            Assert.That(result.FailureReason, Is.EqualTo(WallPlacementFailureReason.OutOfBounds));
            Assert.That(wall.MountedItems, Is.EqualTo(new[] { original }));
            Assert.That(wall.OccupiedSlotCount, Is.EqualTo(2));
            Assert.That(
                wall.TryGetOccupant(new WallSlotPosition(3, 0), out var lowerOwner),
                Is.True);
            Assert.That(lowerOwner, Is.EqualTo("window.01"));
            Assert.That(
                wall.TryGetOccupant(new WallSlotPosition(3, 1), out var upperOwner),
                Is.True);
            Assert.That(upperOwner, Is.EqualTo("window.01"));
        }

        [Test]
        public void TryMove_ValidPosition_ReleasesOldSlotsAndAssignsNewSlotsToSameOwner()
        {
            var wall = new WallSurfaceLayout("wall.back-right", 8, 2);
            Assert.That(wall.TryPlace(CreateItem(
                "window.01",
                new WallSlotPosition(3, 0),
                new WallFootprint(1, 1))).Succeeded, Is.True);

            var result = wall.TryMove("window.01", new WallSlotPosition(5, 1));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(wall.OccupiedSlotCount, Is.EqualTo(1));
            Assert.That(
                wall.TryGetOccupant(new WallSlotPosition(3, 0), out _),
                Is.False);
            Assert.That(
                wall.TryGetOccupant(new WallSlotPosition(5, 1), out var newOwner),
                Is.True);
            Assert.That(newOwner, Is.EqualTo("window.01"));
            Assert.That(wall.MountedItems[0].Position, Is.EqualTo(new WallSlotPosition(5, 1)));
        }

        [Test]
        public void TryMove_OccupiedPosition_PreservesBothItemsAndTheirSlots()
        {
            var wall = new WallSurfaceLayout("wall.back-right", 8, 2);
            var window = CreateItem(
                "window.01",
                new WallSlotPosition(3, 0),
                new WallFootprint(1, 1));
            var poster = CreateItem(
                "poster.01",
                new WallSlotPosition(4, 0),
                new WallFootprint(1, 1));
            Assert.That(wall.TryPlace(window).Succeeded, Is.True);
            Assert.That(wall.TryPlace(poster).Succeeded, Is.True);

            var result = wall.TryMove("window.01", new WallSlotPosition(4, 0));

            Assert.That(result.FailureReason, Is.EqualTo(WallPlacementFailureReason.Overlap));
            Assert.That(wall.MountedItems, Is.EqualTo(new[] { window, poster }));
            Assert.That(wall.OccupiedSlotCount, Is.EqualTo(2));
            Assert.That(
                wall.TryGetOccupant(new WallSlotPosition(3, 0), out var windowOwner),
                Is.True);
            Assert.That(windowOwner, Is.EqualTo("window.01"));
            Assert.That(
                wall.TryGetOccupant(new WallSlotPosition(4, 0), out var posterOwner),
                Is.True);
            Assert.That(posterOwner, Is.EqualTo("poster.01"));
        }

        [Test]
        public void TryRemove_RemovesOnlyTargetOwnerSlots()
        {
            var wall = new WallSurfaceLayout("wall.back-right", 8, 2);
            Assert.That(wall.TryPlace(CreateItem(
                "window.01",
                new WallSlotPosition(3, 0),
                new WallFootprint(1, 2))).Succeeded, Is.True);
            Assert.That(wall.TryPlace(CreateItem(
                "poster.01",
                new WallSlotPosition(4, 0),
                new WallFootprint(1, 1))).Succeeded, Is.True);

            var result = wall.TryRemove("window.01");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(wall.OccupiedSlotCount, Is.EqualTo(1));
            Assert.That(
                wall.TryGetOccupant(new WallSlotPosition(3, 0), out _),
                Is.False);
            Assert.That(
                wall.TryGetOccupant(new WallSlotPosition(3, 1), out _),
                Is.False);
            Assert.That(
                wall.TryGetOccupant(new WallSlotPosition(4, 0), out var remainingOwner),
                Is.True);
            Assert.That(remainingOwner, Is.EqualTo("poster.01"));
        }

        [Test]
        public void TryRemove_MissingItem_IsSafeFailure()
        {
            var wall = new WallSurfaceLayout("wall.back-right", 8, 2);

            var result = wall.TryRemove("window.01");

            Assert.That(result.FailureReason, Is.EqualTo(WallPlacementFailureReason.ItemNotFound));
            Assert.That(wall.OccupiedSlotCount, Is.Zero);
            Assert.That(wall.MountedItems, Is.Empty);
        }

        [Test]
        public void WallMutations_LeaveFloorLayoutOccupancyUnchanged()
        {
            var floor = new CafeLayout(
                new GridSettings(1f),
                new FurnitureDefinitionCatalog(new[]
                {
                    new FurnitureDefinition(
                        "furniture.counter",
                        "Counter",
                        new GridSize(1, 1),
                        PlacementSurfaceType.Floor)
                }));
            floor.AddRegion(new LayoutRegion(
                "region.main",
                new GridPosition(0, 0),
                new GridSize(8, 8),
                LayoutZoneType.Interior));
            var floorItem = FurnitureInstance.Restore(
                "11111111111111111111111111111111",
                "furniture.counter",
                new GridPosition(2, 2),
                FurnitureRotation.Degrees0);
            Assert.That(floor.PlaceFurniture(floorItem).Succeeded, Is.True);

            var wall = new WallSurfaceLayout("wall.back-right", 8, 2);
            Assert.That(wall.TryPlace(CreateItem(
                "window.01",
                new WallSlotPosition(3, 0),
                new WallFootprint(1, 1))).Succeeded, Is.True);
            Assert.That(wall.TryRemove("window.01").Succeeded, Is.True);

            Assert.That(floor.OccupiedCellCount, Is.EqualTo(1));
            Assert.That(
                floor.TryGetOccupant(new GridPosition(2, 2), out var floorOwner),
                Is.True);
            Assert.That(floorOwner, Is.EqualTo(floorItem.InstanceId));
        }

        private static WallMountedInstance CreateItem(
            string itemId,
            WallSlotPosition position,
            WallFootprint footprint)
        {
            return new WallMountedInstance(
                itemId,
                "wall.fixture.01",
                "wall.back-right",
                position,
                footprint);
        }
    }
}

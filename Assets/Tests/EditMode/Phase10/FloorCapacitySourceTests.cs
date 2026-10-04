using System;
using AnimalCafe.Capacity;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase10
{
    public sealed class FloorCapacitySourceTests
    {
        [Test]
        public void E005_EightByEightInteriorProducesBaselineLimits()
        {
            var bounds = new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8));
            var layout = CapacityTestSupport.CreateLayout(bounds);
            layout.AddRegion(CapacityTestSupport.Region("region.main", 0, 0, 8, 8));

            var cells = FloorCapacitySource.CountInteriorCells(layout);
            var limits = new CapacityRules().CalculateLimits(cells);
            Assert.That(cells, Is.EqualTo(64));
            Assert.That(limits.TotalCustomers, Is.EqualTo(16));
            Assert.That(limits.CounterQueue, Is.EqualTo(8));
            Assert.That(limits.PickUp, Is.EqualTo(16));
        }

        [Test]
        public void E006_EmptyExteriorAndNullLayouts()
        {
            var layout = CapacityTestSupport.CreateLayout();
            Assert.That(FloorCapacitySource.CountInteriorCells(layout), Is.Zero);
            layout.AddRegion(CapacityTestSupport.Region(
                "region.exterior", 0, 0, 3, 3, LayoutZoneType.Exterior));
            Assert.That(FloorCapacitySource.CountInteriorCells(layout), Is.Zero);
            Assert.Throws<ArgumentNullException>(
                () => FloorCapacitySource.CountInteriorCells(null));
        }

        [Test]
        public void E007_OverlappingAndSeparatedRegionsCountOnlyTheirUnion()
        {
            var layout = CapacityTestSupport.CreateLayout();
            layout.AddRegion(CapacityTestSupport.Region("region.first", 0, 0, 4, 4));
            layout.AddRegion(CapacityTestSupport.Region("region.second", 2, 0, 4, 4));
            Assert.That(FloorCapacitySource.CountInteriorCells(layout), Is.EqualTo(24));
            layout.AddRegion(CapacityTestSupport.Region("region.duplicate", 0, 0, 4, 4));
            Assert.That(FloorCapacitySource.CountInteriorCells(layout), Is.EqualTo(24));
            layout.AddRegion(CapacityTestSupport.Region("region.separate", 8, 0, 1, 1));
            Assert.That(FloorCapacitySource.CountInteriorCells(layout), Is.EqualTo(25));
        }

        [Test]
        public void E008_BoundsClipPartialAndExternalRegions()
        {
            var bounds = new LayoutBounds(new GridPosition(0, 0), new GridSize(4, 4));
            var layout = CapacityTestSupport.CreateLayout(bounds);
            layout.AddRegion(CapacityTestSupport.Region("region.wide", -1, -1, 6, 6));
            Assert.That(FloorCapacitySource.CountInteriorCells(layout), Is.EqualTo(16));
            layout.AddRegion(CapacityTestSupport.Region("region.outside", 9, 9, 2, 2));
            Assert.That(FloorCapacitySource.CountInteriorCells(layout), Is.EqualTo(16));

            var withoutBounds = CapacityTestSupport.CreateLayout();
            withoutBounds.AddRegion(CapacityTestSupport.Region("region.west", -3, -2, 2, 3));
            Assert.That(FloorCapacitySource.CountInteriorCells(withoutBounds), Is.EqualTo(6));
        }

        [Test]
        public void E009_FurnitureAndReservationsDoNotReduceAreaOrChangeRegionQueries()
        {
            var layout = CapacityTestSupport.CreateLayout();
            layout.AddRegion(CapacityTestSupport.Region("region.main", 0, 0, 4, 4));
            var floorPosition = new GridPosition(1, 1);
            var outsidePosition = new GridPosition(5, 5);
            var regions = layout.UnlockedRegions.Count;
            Assert.That(FloorCapacitySource.CountInteriorCells(layout), Is.EqualTo(16));

            var furniture = FurnitureInstance.CreateNew(
                CapacityTestSupport.FurnitureId, new GridPosition(0, 0),
                FurnitureRotation.Degrees0);
            Assert.That(layout.PlaceFurniture(furniture).Succeeded, Is.True);
            Assert.That(layout.MoveFurniture(furniture.InstanceId, new GridPosition(2, 2)).Succeeded,
                Is.True);
            layout.AddReservation(new LayoutReservation("entrance.clearance",
                LayoutReservationType.EntranceClearance,
                new GridPosition(0, 0), new GridSize(1, 1)));
            layout.AddReservation(new LayoutReservation("temporary.blocked",
                LayoutReservationType.Blocked,
                new GridPosition(1, 0), new GridSize(1, 1)));

            Assert.That(FloorCapacitySource.CountInteriorCells(layout), Is.EqualTo(16));
            Assert.That(layout.UnlockedRegions.Count, Is.EqualTo(regions));
            Assert.That(layout.IsInsideUnlockedRegion(floorPosition), Is.True);
            Assert.That(layout.IsInsideUnlockedRegion(outsidePosition), Is.False);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(2));
            Assert.That(layout.Reservations.Count, Is.EqualTo(2));
        }

        [Test]
        public void E010_MaximumSingleCellIsValidButExtendedRegionOverflows()
        {
            var layout = CapacityTestSupport.CreateLayout();
            layout.AddRegion(CapacityTestSupport.Region(
                "region.max", int.MaxValue, 0, 1, 1));
            Assert.That(FloorCapacitySource.CountInteriorCells(layout), Is.EqualTo(1));
            layout.AddRegion(CapacityTestSupport.Region(
                "region.overflow", int.MaxValue, 0, 2, 1));
            Assert.Throws<OverflowException>(
                () => FloorCapacitySource.CountInteriorCells(layout));
        }

        [Test]
        public void E010_NegativeCoordinatesRemainValid()
        {
            var layout = CapacityTestSupport.CreateLayout();
            layout.AddRegion(CapacityTestSupport.Region("region.negative", -3, -2, 2, 2));
            Assert.That(FloorCapacitySource.CountInteriorCells(layout), Is.EqualTo(4));
        }
    }
}

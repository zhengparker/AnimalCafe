using System;
using System.Collections.Generic;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.Phase6
{
    public sealed class CafeLayoutPreviewValidationTests
    {
        private const string ExistingInstanceId =
            "e4ca5e4ea4984b27ba6e2e0545054966";
        private const string OtherInstanceId =
            "f5db6f5fb5a94c38cb7f3f1656165077";

        [TestCase("counter.preset.1x1", 0, 0, FurnitureRotation.Degrees0, 1)]
        [TestCase("counter.preset.1x2", 0, 0, FurnitureRotation.Degrees0, 2)]
        [TestCase("counter.preset.1x3", 0, 0, FurnitureRotation.Degrees0, 3)]
        [TestCase("counter.preset.2x3", 2, 2, FurnitureRotation.Degrees0, 6)]
        public void GetFurnitureFootprintCells_ReturnsEveryAuthoredCell(
            string definitionId,
            int x,
            int y,
            FurnitureRotation rotation,
            int expectedCount)
        {
            var layout = CreateFullyUnlockedLayout();

            var cells = layout.GetFurnitureFootprintCells(
                definitionId,
                new GridPosition(x, y),
                rotation);

            Assert.That(cells, Has.Count.EqualTo(expectedCount));
            Assert.That(cells, Is.Unique);
        }

        [Test]
        public void GetFurnitureFootprintCells_RotatesTwoByThreeToThreeByTwo()
        {
            var layout = CreateFullyUnlockedLayout();

            var cells = layout.GetFurnitureFootprintCells(
                "counter.preset.2x3",
                new GridPosition(2, 2),
                FurnitureRotation.Degrees90);

            Assert.That(cells, Is.EquivalentTo(new[]
            {
                new GridPosition(2, 2),
                new GridPosition(2, 3),
                new GridPosition(3, 2),
                new GridPosition(3, 3),
                new GridPosition(4, 2),
                new GridPosition(4, 3)
            }));
        }

        [TestCase(FurnitureRotation.Degrees0)]
        [TestCase(FurnitureRotation.Degrees90)]
        [TestCase(FurnitureRotation.Degrees180)]
        [TestCase(FurnitureRotation.Degrees270)]
        public void GetFurnitureFootprintCells_FourRotationsRestoreTheAuthoredFootprint(
            FurnitureRotation rotation)
        {
            var layout = CreateFullyUnlockedLayout();

            var cells = layout.GetFurnitureFootprintCells(
                "counter.preset.1x3",
                new GridPosition(2, 2),
                rotation);

            Assert.That(cells, Has.Count.EqualTo(3));
            Assert.That(cells, Is.Unique);
        }

        [Test]
        public void ValidateFurniturePlacement_ValidPreviewDoesNotMutateFormalLayout()
        {
            var layout = CreateFullyUnlockedLayout();
            var beforeCount = layout.OccupiedCellCount;

            var result = layout.ValidateFurniturePlacement(
                "counter.preset.2x3",
                new GridPosition(2, 2),
                FurnitureRotation.Degrees90);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(beforeCount));
            Assert.That(layout.FurnitureInstances, Is.Empty);
            Assert.That(
                layout.GetFurnitureFootprintCells(
                    "counter.preset.2x3",
                    new GridPosition(2, 2),
                    FurnitureRotation.Degrees90),
                Has.Count.EqualTo(6));
        }

        [Test]
        public void ValidateFurniturePlacement_WallOnlyDefinitionReturnsUnsupportedPlacementSurface()
        {
            var layout = CreateFullyUnlockedLayout();

            var result = layout.ValidateFurniturePlacement(
                "fixture.wall.only",
                new GridPosition(2, 2),
                FurnitureRotation.Degrees0);

            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.UnsupportedPlacementSurface));
        }

        [TestCase(-1, 0)]
        [TestCase(8, 0)]
        [TestCase(0, -1)]
        [TestCase(0, 8)]
        public void ValidateFurniturePlacement_OutsideEightByEightBoundsReturnsOutOfLayoutBounds(
            int x,
            int y)
        {
            var layout = CreateFullyUnlockedLayout();

            var result = layout.ValidateFurniturePlacement(
                "counter.preset.1x1",
                new GridPosition(x, y),
                FurnitureRotation.Degrees0);

            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.OutOfLayoutBounds));
        }

        [Test]
        public void ValidateFurniturePlacement_InsideBoundsButOutsideUnlockedRegionReturnsLockedCell()
        {
            var layout = CreatePartiallyUnlockedLayout();

            var result = layout.ValidateFurniturePlacement(
                "counter.preset.1x2",
                new GridPosition(3, 0),
                FurnitureRotation.Degrees90);

            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.LockedCell));
        }

        [Test]
        public void ValidateFurniturePlacement_GenericBlockedReservationReturnsBlocked()
        {
            var layout = CreateFullyUnlockedLayout();
            layout.AddReservation(new LayoutReservation(
                "blocked.service",
                LayoutReservationType.Blocked,
                new GridPosition(3, 3),
                new GridSize(1, 1)));

            var result = layout.ValidateFurniturePlacement(
                "counter.preset.1x1",
                new GridPosition(3, 3),
                FurnitureRotation.Degrees0);

            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.Blocked));
        }

        [Test]
        public void ValidateFurniturePlacement_EntranceReservationReturnsEntranceClearanceReason()
        {
            var layout = CreateFullyUnlockedLayout();
            layout.AddReservation(new LayoutReservation(
                "entrance.main",
                LayoutReservationType.EntranceClearance,
                new GridPosition(3, 3),
                new GridSize(1, 1)));

            var result = layout.ValidateFurniturePlacement(
                "counter.preset.1x1",
                new GridPosition(3, 3),
                FurnitureRotation.Degrees0);

            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.ReservedEntranceClearance));
        }

        [Test]
        public void ValidateFurniturePlacement_UsesStableFailureOrdering()
        {
            var layout = CreatePartiallyUnlockedLayout();
            layout.AddReservation(new LayoutReservation(
                "blocked.edge",
                LayoutReservationType.Blocked,
                new GridPosition(3, 0),
                new GridSize(1, 1)));

            var result = layout.ValidateFurniturePlacement(
                "counter.preset.1x2",
                new GridPosition(3, 0),
                FurnitureRotation.Degrees90);

            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.LockedCell));
        }

        [Test]
        public void ValidateFurniturePlacement_ExistingFurnitureIgnoresOnlyItsOwnCells()
        {
            var layout = CreateFullyUnlockedLayout();
            var existing = FurnitureInstance.Restore(
                ExistingInstanceId,
                "counter.preset.1x2",
                new GridPosition(1, 1),
                FurnitureRotation.Degrees0);
            var other = FurnitureInstance.Restore(
                OtherInstanceId,
                "counter.preset.1x1",
                new GridPosition(4, 1),
                FurnitureRotation.Degrees0);
            Assert.That(layout.PlaceFurniture(existing).Succeeded, Is.True);
            Assert.That(layout.PlaceFurniture(other).Succeeded, Is.True);

            var selfResult = layout.ValidateFurniturePlacement(
                existing.DefinitionId,
                existing.Position,
                existing.Rotation,
                ExistingInstanceId);
            var otherResult = layout.ValidateFurniturePlacement(
                existing.DefinitionId,
                other.Position,
                existing.Rotation,
                ExistingInstanceId);

            Assert.That(selfResult.Succeeded, Is.True);
            Assert.That(
                otherResult.FailureReason,
                Is.EqualTo(PlacementFailureReason.Overlap));
        }

        [Test]
        public void ValidateFurniturePlacement_UnknownIgnoredInstanceReturnsInstanceNotFound()
        {
            var layout = CreateFullyUnlockedLayout();

            var result = layout.ValidateFurniturePlacement(
                "counter.preset.1x1",
                new GridPosition(1, 1),
                FurnitureRotation.Degrees0,
                ExistingInstanceId);

            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.InstanceNotFound));
        }

        [Test]
        public void ValidateFurniturePlacement_InvalidIgnoredInstanceReturnsInstanceNotFound()
        {
            var layout = CreateFullyUnlockedLayout();

            var result = layout.ValidateFurniturePlacement(
                "counter.preset.1x1",
                new GridPosition(1, 1),
                FurnitureRotation.Degrees0,
                "not-a-guid");

            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.InstanceNotFound));
        }

        [Test]
        public void UpdateFurniturePlacement_ChangesPositionAndRotationAtomicallyWithSameInstanceId()
        {
            var layout = CreateFullyUnlockedLayout();
            var existing = FurnitureInstance.Restore(
                ExistingInstanceId,
                "counter.preset.1x2",
                new GridPosition(1, 1),
                FurnitureRotation.Degrees0);
            Assert.That(layout.PlaceFurniture(existing).Succeeded, Is.True);

            var result = layout.UpdateFurniturePlacement(
                ExistingInstanceId,
                new GridPosition(4, 4),
                FurnitureRotation.Degrees90);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                layout.TryGetFurnitureInstance(ExistingInstanceId, out var updated),
                Is.True);
            Assert.That(updated.InstanceId, Is.EqualTo(ExistingInstanceId));
            Assert.That(updated.Position, Is.EqualTo(new GridPosition(4, 4)));
            Assert.That(updated.Rotation, Is.EqualTo(FurnitureRotation.Degrees90));
            Assert.That(layout.TryGetOccupant(new GridPosition(1, 1), out _), Is.False);
            Assert.That(layout.TryGetOccupant(new GridPosition(4, 4), out var firstOwner), Is.True);
            Assert.That(firstOwner, Is.EqualTo(ExistingInstanceId));
            Assert.That(layout.TryGetOccupant(new GridPosition(5, 4), out var secondOwner), Is.True);
            Assert.That(secondOwner, Is.EqualTo(ExistingInstanceId));
        }

        [Test]
        public void UpdateFurniturePlacement_InvalidCandidatePreservesOriginalPlacement()
        {
            var layout = CreateFullyUnlockedLayout();
            var existing = FurnitureInstance.Restore(
                ExistingInstanceId,
                "counter.preset.1x2",
                new GridPosition(1, 1),
                FurnitureRotation.Degrees0);
            Assert.That(layout.PlaceFurniture(existing).Succeeded, Is.True);

            var result = layout.UpdateFurniturePlacement(
                ExistingInstanceId,
                new GridPosition(7, 7),
                FurnitureRotation.Degrees0);

            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.OutOfLayoutBounds));
            Assert.That(
                layout.TryGetFurnitureInstance(ExistingInstanceId, out var unchanged),
                Is.True);
            Assert.That(unchanged.Position, Is.EqualTo(new GridPosition(1, 1)));
            Assert.That(unchanged.Rotation, Is.EqualTo(FurnitureRotation.Degrees0));
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(2));
        }

        [Test]
        public void LayoutBounds_ContainsOnlyCellsInsideItsOriginAndSize()
        {
            var bounds = new LayoutBounds(
                new GridPosition(2, 3),
                new GridSize(2, 2));

            Assert.That(bounds.Contains(new GridPosition(2, 3)), Is.True);
            Assert.That(bounds.Contains(new GridPosition(3, 4)), Is.True);
            Assert.That(bounds.Contains(new GridPosition(4, 4)), Is.False);
            Assert.That(bounds.Contains(new GridPosition(3, 5)), Is.False);
        }

        private static CafeLayout CreateFullyUnlockedLayout()
        {
            var layout = CreateLayout();
            layout.AddRegion(new LayoutRegion(
                "region.main",
                new GridPosition(0, 0),
                new GridSize(8, 8),
                LayoutZoneType.Interior));
            return layout;
        }

        private static CafeLayout CreatePartiallyUnlockedLayout()
        {
            var layout = CreateLayout();
            layout.AddRegion(new LayoutRegion(
                "region.unlocked",
                new GridPosition(0, 0),
                new GridSize(4, 8),
                LayoutZoneType.Interior));
            return layout;
        }

        private static CafeLayout CreateLayout()
        {
            return new CafeLayout(
                new GridSettings(1f),
                new FurnitureDefinitionCatalog(new[]
                {
                    new FurnitureDefinition(
                        "counter.preset.1x1",
                        "1 x 1 Counter Module",
                        new GridSize(1, 1),
                        PlacementSurfaceType.Floor),
                    new FurnitureDefinition(
                        "counter.preset.1x2",
                        "1 x 2 Counter Module",
                        new GridSize(1, 2),
                        PlacementSurfaceType.Floor),
                    new FurnitureDefinition(
                        "counter.preset.1x3",
                        "1 x 3 Counter Module",
                        new GridSize(1, 3),
                        PlacementSurfaceType.Floor),
                    new FurnitureDefinition(
                        "counter.preset.2x3",
                        "2 x 3 Counter Module",
                        new GridSize(2, 3),
                        PlacementSurfaceType.Floor),
                    new FurnitureDefinition(
                        "fixture.wall.only",
                        "Wall-only Fixture",
                        new GridSize(1, 1),
                        PlacementSurfaceType.Wall)
                }),
                new LayoutBounds(
                    new GridPosition(0, 0),
                    new GridSize(8, 8)));
        }
    }
}

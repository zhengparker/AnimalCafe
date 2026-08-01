using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests
{
    public sealed class GridPlacementTests
    {
        private const string OneByOneDefinitionId = "furniture.one-by-one";
        private const string TwoByOneDefinitionId = "furniture.two-by-one";
        private const string TwoByTwoDefinitionId = "furniture.two-by-two";
        private const string TwoByThreeDefinitionId = "furniture.two-by-three";
        private const string ThreeByOneDefinitionId = "furniture.three-by-one";
        private const string FirstInstanceId = "11111111111111111111111111111111";
        private const string SecondInstanceId = "22222222222222222222222222222222";

        [Test]
        public void PlacementResult_SuccessHasNoFailureReason()
        {
            var result = PlacementResult.Success();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.None));
        }

        [TestCase(PlacementFailureReason.OutOfUnlockedRegion)]
        [TestCase(PlacementFailureReason.Overlap)]
        [TestCase(PlacementFailureReason.InstanceNotFound)]
        [TestCase(PlacementFailureReason.InstanceAlreadyPlaced)]
        [TestCase(PlacementFailureReason.UnsupportedPlacementSurface)]
        public void PlacementResult_FailureStoresReason(
            PlacementFailureReason reason)
        {
            var result = PlacementResult.Failure(reason);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(reason));
        }

        [Test]
        public void PlacementResult_FailureRejectsNone()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlacementResult.Failure(
                    PlacementFailureReason.None));
        }

        [Test]
        public void PlacementResult_FailureRejectsUnknownReason()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => PlacementResult.Failure(
                    (PlacementFailureReason)99));
        }

        [Test]
        public void Place_OneByOneOccupiesOneCell()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 4, 7, 1, 1);
            var instance = CreateInstance(
                FirstInstanceId,
                OneByOneDefinitionId,
                4,
                7);

            var result = layout.PlaceFurniture(instance);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(layout.FurnitureInstances, Is.EqualTo(new[] { instance }));
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(1));
            AssertOccupant(layout, 4, 7, FirstInstanceId);
        }

        [Test]
        public void Place_WallOnlyDefinitionReturnsUnsupportedSurfaceWithoutMutation()
        {
            var layout = new CafeLayout(
                new GridSettings(1f),
                new FurnitureDefinitionCatalog(new[]
                {
                    CreateDefinition(
                        OneByOneDefinitionId,
                        1,
                        1,
                        PlacementSurfaceType.Wall)
                }));
            AddRegion(layout, "region.main", 0, 0, 1, 1);
            var instance = CreateInstance(
                FirstInstanceId,
                OneByOneDefinitionId,
                0,
                0);

            var result = layout.PlaceFurniture(instance);

            AssertRejectedWithoutMutation(
                layout,
                instance,
                result,
                PlacementFailureReason.UnsupportedPlacementSurface);
            AssertCellIsEmpty(layout, 0, 0);
        }

        [Test]
        public void Place_FurnitureSurfaceOnlyDefinitionReturnsUnsupportedSurfaceWithoutMutation()
        {
            var layout = new CafeLayout(
                new GridSettings(1f),
                new FurnitureDefinitionCatalog(new[]
                {
                    CreateDefinition(
                        OneByOneDefinitionId,
                        1,
                        1,
                        PlacementSurfaceType.FurnitureSurface)
                }));
            AddRegion(layout, "region.main", 0, 0, 1, 1);
            var instance = CreateInstance(
                FirstInstanceId,
                OneByOneDefinitionId,
                0,
                0);

            var result = layout.PlaceFurniture(instance);

            AssertRejectedWithoutMutation(
                layout,
                instance,
                result,
                PlacementFailureReason.UnsupportedPlacementSurface);
            AssertCellIsEmpty(layout, 0, 0);
        }

        [Test]
        public void Place_FloorAndWallDefinitionCanUseFloorGrid()
        {
            var layout = new CafeLayout(
                new GridSettings(1f),
                new FurnitureDefinitionCatalog(new[]
                {
                    CreateDefinition(
                        TwoByOneDefinitionId,
                        2,
                        1,
                        PlacementSurfaceType.Floor |
                        PlacementSurfaceType.Wall)
                }));
            AddRegion(layout, "region.main", 0, 0, 2, 1);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);

            var result = layout.PlaceFurniture(instance);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.FailureReason, Is.EqualTo(PlacementFailureReason.None));
            Assert.That(layout.FurnitureInstances, Is.EqualTo(new[] { instance }));
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(2));
            AssertOccupant(layout, 0, 0, FirstInstanceId);
            AssertOccupant(layout, 1, 0, FirstInstanceId);
        }

        [TestCase(FurnitureRotation.Degrees0, 2, 3)]
        [TestCase(FurnitureRotation.Degrees90, 3, 2)]
        [TestCase(FurnitureRotation.Degrees180, 2, 3)]
        [TestCase(FurnitureRotation.Degrees270, 3, 2)]
        public void Place_NonSquareFootprintOccupiesExpectedCells(
            FurnitureRotation rotation,
            int expectedWidth,
            int expectedHeight)
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 10, 20, 3, 3);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByThreeDefinitionId,
                10,
                20,
                rotation);

            var result = layout.PlaceFurniture(instance);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(6));
            for (var x = 10; x < 10 + expectedWidth; x++)
            {
                for (var y = 20; y < 20 + expectedHeight; y++)
                {
                    AssertOccupant(layout, x, y, FirstInstanceId);
                }
            }
        }

        [Test]
        public void Place_ExactlyTouchesEveryRegionBoundary()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.exact", -2, -3, 2, 3);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByThreeDefinitionId,
                -2,
                -3);

            var result = layout.PlaceFurniture(instance);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(6));
            AssertOccupant(layout, -2, -3, FirstInstanceId);
            AssertOccupant(layout, -1, -1, FirstInstanceId);
        }

        [Test]
        public void Place_OneCellPastRightBoundaryFailsWithoutMutation()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 2);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                1,
                0);

            var result = layout.PlaceFurniture(instance);

            AssertRejectedWithoutMutation(
                layout,
                instance,
                result,
                PlacementFailureReason.OutOfUnlockedRegion);
        }

        [Test]
        public void Place_OneCellPastTopBoundaryFailsWithoutMutation()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 2);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                1,
                FurnitureRotation.Degrees90);

            var result = layout.PlaceFurniture(instance);

            AssertRejectedWithoutMutation(
                layout,
                instance,
                result,
                PlacementFailureReason.OutOfUnlockedRegion);
        }

        [Test]
        public void Place_PartlyOutsideRegionFailsWithoutMutation()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 3, 3);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByTwoDefinitionId,
                -1,
                1);

            var result = layout.PlaceFurniture(instance);

            AssertRejectedWithoutMutation(
                layout,
                instance,
                result,
                PlacementFailureReason.OutOfUnlockedRegion);
        }

        [Test]
        public void Place_InNegativeCoordinateRegionSucceeds()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.negative", -5, -4, 3, 3);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByTwoDefinitionId,
                -5,
                -4);

            var result = layout.PlaceFurniture(instance);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(4));
            AssertOccupant(layout, -5, -4, FirstInstanceId);
            AssertOccupant(layout, -4, -3, FirstInstanceId);
        }

        [Test]
        public void Place_CanSpanAdjacentUnlockedRegions()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.left", 0, 0, 1, 2);
            AddRegion(layout, "region.right", 1, 0, 1, 2);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByTwoDefinitionId,
                0,
                0);

            var result = layout.PlaceFurniture(instance);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(4));
        }

        [Test]
        public void Place_CannotSpanOneCellLockedGap()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.left", 0, 0, 1, 1);
            AddRegion(layout, "region.right", 2, 0, 1, 1);
            var instance = CreateInstance(
                FirstInstanceId,
                ThreeByOneDefinitionId,
                0,
                0);

            var result = layout.PlaceFurniture(instance);

            AssertRejectedWithoutMutation(
                layout,
                instance,
                result,
                PlacementFailureReason.OutOfUnlockedRegion);
        }

        [Test]
        public void Place_OverlappingRegionsDoNotDuplicateCells()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.first", 0, 0, 2, 1);
            AddRegion(layout, "region.second", 0, 0, 2, 1);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);

            var result = layout.PlaceFurniture(instance);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(2));
            AssertOccupant(layout, 0, 0, FirstInstanceId);
            AssertOccupant(layout, 1, 0, FirstInstanceId);
        }

        [Test]
        public void Place_WithNoUnlockedRegionsFailsWithoutMutation()
        {
            var layout = CreateLayout();
            var instance = CreateInstance(
                FirstInstanceId,
                OneByOneDefinitionId,
                0,
                0);

            var result = layout.PlaceFurniture(instance);

            AssertRejectedWithoutMutation(
                layout,
                instance,
                result,
                PlacementFailureReason.OutOfUnlockedRegion);
        }

        [Test]
        public void Place_AdjacentFurnitureSucceeds()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 4, 1);
            var first = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            var second = CreateInstance(
                SecondInstanceId,
                TwoByOneDefinitionId,
                2,
                0);

            Assert.That(layout.PlaceFurniture(first).Succeeded, Is.True);
            var result = layout.PlaceFurniture(second);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(layout.FurnitureInstances, Is.EqualTo(new[] { first, second }));
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(4));
            AssertOccupant(layout, 1, 0, FirstInstanceId);
            AssertOccupant(layout, 2, 0, SecondInstanceId);
        }

        [Test]
        public void Place_OneCellOverlapFailsWithoutMutation()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 4, 1);
            var first = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(first).Succeeded, Is.True);
            var existingInstances = layout.FurnitureInstances.ToArray();
            var second = CreateInstance(
                SecondInstanceId,
                TwoByOneDefinitionId,
                1,
                0);

            var result = layout.PlaceFurniture(second);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(PlacementFailureReason.Overlap));
            Assert.That(layout.FurnitureInstances, Is.EqualTo(existingInstances));
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(2));
            AssertOccupant(layout, 0, 0, FirstInstanceId);
            AssertOccupant(layout, 1, 0, FirstInstanceId);
            Assert.That(layout.TryGetFurnitureInstance(SecondInstanceId, out _), Is.False);
        }

        [Test]
        public void Place_RepeatedInstanceReturnsAlreadyPlacedWithoutMutation()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 4, 1);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);
            var existingInstances = layout.FurnitureInstances.ToArray();
            var repeated = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                2,
                0);

            var result = layout.PlaceFurniture(repeated);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.InstanceAlreadyPlaced));
            Assert.That(layout.FurnitureInstances, Is.EqualTo(existingInstances));
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(2));
            AssertOccupant(layout, 0, 0, FirstInstanceId);
            AssertOccupant(layout, 1, 0, FirstInstanceId);
            Assert.That(layout.TryGetOccupant(new GridPosition(2, 0), out var owner), Is.False);
            Assert.That(owner, Is.Null);
        }

        [Test]
        public void Place_UnknownDefinitionThrowsWithoutMutation()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 2);
            var instance = CreateInstance(
                FirstInstanceId,
                "furniture.unknown",
                0,
                0);

            var exception = Assert.Throws<ArgumentException>(
                () => layout.PlaceFurniture(instance));

            StringAssert.Contains("furniture.unknown", exception.Message);
            Assert.That(layout.FurnitureInstances, Is.Empty);
            Assert.That(layout.OccupiedCellCount, Is.Zero);
            Assert.That(layout.TryGetFurnitureInstance(FirstInstanceId, out _), Is.False);
        }

        [Test]
        public void Place_ExtremeCoordinateOverflowFailsWithoutMutation()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.extreme", int.MaxValue, 0, 1, 1);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                int.MaxValue,
                0);

            var result = layout.PlaceFurniture(instance);

            AssertRejectedWithoutMutation(
                layout,
                instance,
                result,
                PlacementFailureReason.OutOfUnlockedRegion);
        }

        [Test]
        public void Place_ExtremeYCoordinateOverflowFailsWithoutMutation()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.extreme", 0, int.MaxValue, 1, 1);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                int.MaxValue,
                FurnitureRotation.Degrees90);

            var result = layout.PlaceFurniture(instance);

            AssertRejectedWithoutMutation(
                layout,
                instance,
                result,
                PlacementFailureReason.OutOfUnlockedRegion);
        }

        [Test]
        public void Place_RegionTopCalculationNearIntMaxDoesNotOverflow()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.extreme", 0, int.MaxValue - 1, 1, 2);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                int.MaxValue - 1,
                FurnitureRotation.Degrees90);

            var result = layout.PlaceFurniture(instance);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.None));
            Assert.That(layout.FurnitureInstances, Is.EqualTo(new[] { instance }));
            Assert.That(
                layout.TryGetFurnitureInstance(
                    FirstInstanceId,
                    out var indexedInstance),
                Is.True);
            Assert.That(indexedInstance, Is.SameAs(instance));
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(2));
            AssertOccupant(layout, 0, int.MaxValue - 1, FirstInstanceId);
            AssertOccupant(layout, 0, int.MaxValue, FirstInstanceId);
        }

        [Test]
        public void Place_RegionRightCalculationNearIntMaxDoesNotOverflow()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.extreme", int.MaxValue - 1, 0, 2, 1);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                int.MaxValue - 1,
                0);

            var result = layout.PlaceFurniture(instance);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.None));
            Assert.That(layout.FurnitureInstances, Is.EqualTo(new[] { instance }));
            Assert.That(
                layout.TryGetFurnitureInstance(
                    FirstInstanceId,
                    out var indexedInstance),
                Is.True);
            Assert.That(indexedInstance, Is.SameAs(instance));
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(2));
            AssertOccupant(layout, int.MaxValue - 1, 0, FirstInstanceId);
            AssertOccupant(layout, int.MaxValue, 0, FirstInstanceId);
            AssertCellIsEmpty(layout, int.MinValue, 0);
        }

        [Test]
        public void OccupancyQuery_ReturnsOwnerForOccupiedCell()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 1);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(instance).Succeeded, Is.True);

            var found = layout.TryGetOccupant(
                new GridPosition(1, 0),
                out var instanceId);

            Assert.That(found, Is.True);
            Assert.That(instanceId, Is.EqualTo(FirstInstanceId));
        }

        [Test]
        public void OccupancyQuery_ReturnsFalseAndNullForEmptyCell()
        {
            var layout = CreateLayout();

            var found = layout.TryGetOccupant(
                new GridPosition(8, 9),
                out var instanceId);

            Assert.That(found, Is.False);
            Assert.That(instanceId, Is.Null);
        }

        [Test]
        public void Move_ToEmptyUnlockedPositionSucceeds()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 5, 1);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);

            var result = layout.MoveFurniture(
                FirstInstanceId,
                new GridPosition(3, 0));

            Assert.That(result.Succeeded, Is.True);
            AssertSuccessfulReplacement(
                layout,
                original,
                new GridPosition(3, 0),
                FurnitureRotation.Degrees0);
            AssertCellIsEmpty(layout, 0, 0);
            AssertCellIsEmpty(layout, 1, 0);
            AssertOccupant(layout, 3, 0, FirstInstanceId);
            AssertOccupant(layout, 4, 0, FirstInstanceId);
        }

        [Test]
        public void Move_ReleasesEveryOldCellAndOccupiesEveryNewCell()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 5, 3);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByTwoDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);

            var result = layout.MoveFurniture(
                FirstInstanceId,
                new GridPosition(3, 1));

            Assert.That(result.Succeeded, Is.True);
            AssertSuccessfulReplacement(
                layout,
                original,
                new GridPosition(3, 1),
                FurnitureRotation.Degrees0);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(4));
            AssertCellIsEmpty(layout, 0, 0);
            AssertCellIsEmpty(layout, 0, 1);
            AssertCellIsEmpty(layout, 1, 0);
            AssertCellIsEmpty(layout, 1, 1);
            AssertOccupant(layout, 3, 1, FirstInstanceId);
            AssertOccupant(layout, 3, 2, FirstInstanceId);
            AssertOccupant(layout, 4, 1, FirstInstanceId);
            AssertOccupant(layout, 4, 2, FirstInstanceId);
        }

        [Test]
        public void Move_ToCurrentPositionIsIdempotent()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 1);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);

            var result = layout.MoveFurniture(
                FirstInstanceId,
                new GridPosition(0, 0));

            Assert.That(result.Succeeded, Is.True);
            AssertSuccessfulReplacement(
                layout,
                original,
                new GridPosition(0, 0),
                FurnitureRotation.Degrees0);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(2));
            AssertOccupant(layout, 0, 0, FirstInstanceId);
            AssertOccupant(layout, 1, 0, FirstInstanceId);
        }

        [Test]
        public void Move_CanReuseSomeOfItsOwnCells()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 3, 1);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);

            var result = layout.MoveFurniture(
                FirstInstanceId,
                new GridPosition(1, 0));

            Assert.That(result.Succeeded, Is.True);
            AssertSuccessfulReplacement(
                layout,
                original,
                new GridPosition(1, 0),
                FurnitureRotation.Degrees0);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(2));
            AssertCellIsEmpty(layout, 0, 0);
            AssertOccupant(layout, 1, 0, FirstInstanceId);
            AssertOccupant(layout, 2, 0, FirstInstanceId);
        }

        [Test]
        public void Move_OutOfRegionPreservesExactOldState()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 3, 1);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);
            var snapshot = CaptureState(
                layout,
                new GridPosition(0, 0),
                new GridPosition(1, 0),
                new GridPosition(2, 0),
                new GridPosition(3, 0));

            var result = layout.MoveFurniture(
                FirstInstanceId,
                new GridPosition(2, 0));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.OutOfUnlockedRegion));
            AssertStateIsUnchanged(layout, snapshot);
        }

        [Test]
        public void Move_IntoLockedGapPreservesExactOldState()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.left", 0, 0, 4, 1);
            AddRegion(layout, "region.right", 5, 0, 1, 1);
            var original = CreateInstance(
                FirstInstanceId,
                ThreeByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);
            var snapshot = CaptureState(
                layout,
                new GridPosition(0, 0),
                new GridPosition(1, 0),
                new GridPosition(2, 0),
                new GridPosition(3, 0),
                new GridPosition(4, 0),
                new GridPosition(5, 0));

            var result = layout.MoveFurniture(
                FirstInstanceId,
                new GridPosition(3, 0));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.OutOfUnlockedRegion));
            AssertStateIsUnchanged(layout, snapshot);
        }

        [Test]
        public void Move_OverlapPreservesExactOldState()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 5, 1);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            var blocker = CreateInstance(
                SecondInstanceId,
                TwoByOneDefinitionId,
                3,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);
            Assert.That(layout.PlaceFurniture(blocker).Succeeded, Is.True);
            var snapshot = CaptureState(
                layout,
                new GridPosition(0, 0),
                new GridPosition(1, 0),
                new GridPosition(2, 0),
                new GridPosition(3, 0),
                new GridPosition(4, 0));

            var result = layout.MoveFurniture(
                FirstInstanceId,
                new GridPosition(2, 0));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.Overlap));
            AssertStateIsUnchanged(layout, snapshot);
        }

        [Test]
        public void Move_FailureDoesNotBlockNextLegalMove()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 5, 1);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            var blocker = CreateInstance(
                SecondInstanceId,
                OneByOneDefinitionId,
                2,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);
            Assert.That(layout.PlaceFurniture(blocker).Succeeded, Is.True);
            var beforeFailure = CaptureState(
                layout,
                new GridPosition(0, 0),
                new GridPosition(1, 0),
                new GridPosition(2, 0),
                new GridPosition(3, 0),
                new GridPosition(4, 0));

            var failedResult = layout.MoveFurniture(
                FirstInstanceId,
                new GridPosition(1, 0));

            Assert.That(failedResult.Succeeded, Is.False);
            Assert.That(
                failedResult.FailureReason,
                Is.EqualTo(PlacementFailureReason.Overlap));
            AssertStateIsUnchanged(layout, beforeFailure);

            var successfulResult = layout.MoveFurniture(
                FirstInstanceId,
                new GridPosition(3, 0));

            Assert.That(successfulResult.Succeeded, Is.True);
            AssertSuccessfulReplacement(
                layout,
                original,
                new GridPosition(3, 0),
                FurnitureRotation.Degrees0,
                expectedInstanceCount: 2);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(3));
            AssertCellIsEmpty(layout, 0, 0);
            AssertCellIsEmpty(layout, 1, 0);
            AssertOccupant(layout, 2, 0, SecondInstanceId);
            AssertOccupant(layout, 3, 0, FirstInstanceId);
            AssertOccupant(layout, 4, 0, FirstInstanceId);
        }

        [Test]
        public void Move_UnknownValidInstanceReturnsInstanceNotFound()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 2);
            var snapshot = CaptureState(
                layout,
                new GridPosition(1, 1));

            var result = layout.MoveFurniture(
                "8a28e9ab60a75cf1b7790777df5b39e3",
                new GridPosition(1, 1));

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.InstanceNotFound));
            AssertStateIsUnchanged(layout, snapshot);
        }

        [Test]
        public void Move_InvalidInstanceIdThrowsWithoutMutation()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 3, 1);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);
            var snapshot = CaptureState(
                layout,
                new GridPosition(0, 0),
                new GridPosition(1, 0),
                new GridPosition(2, 0));

            Assert.Throws<ArgumentException>(
                () => layout.MoveFurniture(
                    "invalid-id",
                    new GridPosition(1, 0)));

            AssertStateIsUnchanged(layout, snapshot);
        }

        [Test]
        public void Rotate_NonSquareFurnitureUpdatesEveryOccupiedCell()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 3, 3);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByThreeDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);

            var result = layout.RotateFurniture(
                FirstInstanceId,
                FurnitureRotation.Degrees90);

            Assert.That(result.Succeeded, Is.True);
            AssertSuccessfulReplacement(
                layout,
                original,
                new GridPosition(0, 0),
                FurnitureRotation.Degrees90);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(6));
            AssertCellIsEmpty(layout, 0, 2);
            AssertCellIsEmpty(layout, 1, 2);
            AssertOccupant(layout, 0, 0, FirstInstanceId);
            AssertOccupant(layout, 0, 1, FirstInstanceId);
            AssertOccupant(layout, 1, 0, FirstInstanceId);
            AssertOccupant(layout, 1, 1, FirstInstanceId);
            AssertOccupant(layout, 2, 0, FirstInstanceId);
            AssertOccupant(layout, 2, 1, FirstInstanceId);
        }

        [Test]
        public void Rotate_CanReuseItsOwnCells()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 2);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);

            var result = layout.RotateFurniture(
                FirstInstanceId,
                FurnitureRotation.Degrees90);

            Assert.That(result.Succeeded, Is.True);
            AssertSuccessfulReplacement(
                layout,
                original,
                new GridPosition(0, 0),
                FurnitureRotation.Degrees90);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(2));
            AssertCellIsEmpty(layout, 1, 0);
            AssertOccupant(layout, 0, 0, FirstInstanceId);
            AssertOccupant(layout, 0, 1, FirstInstanceId);
        }

        [Test]
        public void Rotate_ToCurrentRotationIsIdempotent()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 1);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);

            var result = layout.RotateFurniture(
                FirstInstanceId,
                FurnitureRotation.Degrees0);

            Assert.That(result.Succeeded, Is.True);
            AssertSuccessfulReplacement(
                layout,
                original,
                new GridPosition(0, 0),
                FurnitureRotation.Degrees0);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(2));
            AssertOccupant(layout, 0, 0, FirstInstanceId);
            AssertOccupant(layout, 1, 0, FirstInstanceId);
        }

        [Test]
        public void Rotate_OutOfRegionPreservesOldRotationAndCells()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 3);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByThreeDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);
            var snapshot = CaptureState(
                layout,
                new GridPosition(0, 0),
                new GridPosition(0, 1),
                new GridPosition(0, 2),
                new GridPosition(1, 0),
                new GridPosition(1, 1),
                new GridPosition(1, 2),
                new GridPosition(2, 0),
                new GridPosition(2, 1));

            var result = layout.RotateFurniture(
                FirstInstanceId,
                FurnitureRotation.Degrees90);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.OutOfUnlockedRegion));
            AssertStateIsUnchanged(layout, snapshot);
        }

        [Test]
        public void Rotate_OverlapPreservesOldRotationAndCells()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 3, 3);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByThreeDefinitionId,
                0,
                0);
            var blocker = CreateInstance(
                SecondInstanceId,
                OneByOneDefinitionId,
                2,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);
            Assert.That(layout.PlaceFurniture(blocker).Succeeded, Is.True);
            var snapshot = CaptureState(
                layout,
                new GridPosition(0, 0),
                new GridPosition(0, 1),
                new GridPosition(0, 2),
                new GridPosition(1, 0),
                new GridPosition(1, 1),
                new GridPosition(1, 2),
                new GridPosition(2, 0),
                new GridPosition(2, 1));

            var result = layout.RotateFurniture(
                FirstInstanceId,
                FurnitureRotation.Degrees90);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.Overlap));
            AssertStateIsUnchanged(layout, snapshot);
        }

        [Test]
        public void Rotate_UnknownValidInstanceReturnsInstanceNotFound()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 2);
            var snapshot = CaptureState(
                layout,
                new GridPosition(0, 0),
                new GridPosition(0, 1));

            var result = layout.RotateFurniture(
                "8a28e9ab60a75cf1b7790777df5b39e3",
                FurnitureRotation.Degrees90);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.InstanceNotFound));
            AssertStateIsUnchanged(layout, snapshot);
        }

        [Test]
        public void Rotate_UnknownValidInstanceWithInvalidRotationThrowsWithoutMutation()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 1);
            var existing = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(existing).Succeeded, Is.True);
            var snapshot = CaptureState(
                layout,
                new GridPosition(0, 0),
                new GridPosition(1, 0));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => layout.RotateFurniture(
                    "8a28e9ab60a75cf1b7790777df5b39e3",
                    (FurnitureRotation)99));

            AssertStateIsUnchanged(layout, snapshot);
        }

        [Test]
        public void Rotate_InvalidInstanceIdThrowsWithoutMutation()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 2);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);
            var snapshot = CaptureState(
                layout,
                new GridPosition(0, 0),
                new GridPosition(1, 0),
                new GridPosition(0, 1));

            Assert.Throws<ArgumentException>(
                () => layout.RotateFurniture(
                    "invalid-id",
                    FurnitureRotation.Degrees90));

            AssertStateIsUnchanged(layout, snapshot);
        }

        [Test]
        public void Rotate_InvalidRotationThrowsWithoutMutation()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 2);
            var original = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(original).Succeeded, Is.True);
            var snapshot = CaptureState(
                layout,
                new GridPosition(0, 0),
                new GridPosition(1, 0),
                new GridPosition(0, 1));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => layout.RotateFurniture(
                    FirstInstanceId,
                    (FurnitureRotation)99));

            AssertStateIsUnchanged(layout, snapshot);
        }

        [Test]
        public void Remove_DeletesInstanceAndEveryOwnedCell()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 3, 2);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByThreeDefinitionId,
                0,
                0,
                FurnitureRotation.Degrees90);
            Assert.That(layout.PlaceFurniture(instance).Succeeded, Is.True);

            var result = layout.RemoveFurniture(FirstInstanceId);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.None));
            Assert.That(
                layout.TryGetFurnitureInstance(FirstInstanceId, out _),
                Is.False);
            AssertExactLayoutState(
                layout,
                Array.Empty<FurnitureInstance>(),
                new Dictionary<GridPosition, string>(),
                new GridPosition(0, 0),
                new GridPosition(0, 1),
                new GridPosition(1, 0),
                new GridPosition(1, 1),
                new GridPosition(2, 0),
                new GridPosition(2, 1));
        }

        [Test]
        public void Remove_DoesNotChangeOtherFurniture()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 5, 2);
            var removed = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            var remaining = CreateInstance(
                SecondInstanceId,
                TwoByTwoDefinitionId,
                3,
                0);
            Assert.That(layout.PlaceFurniture(removed).Succeeded, Is.True);
            Assert.That(layout.PlaceFurniture(remaining).Succeeded, Is.True);

            var result = layout.RemoveFurniture(FirstInstanceId);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.None));
            Assert.That(
                layout.TryGetFurnitureInstance(FirstInstanceId, out _),
                Is.False);
            AssertExactLayoutState(
                layout,
                new[] { remaining },
                new Dictionary<GridPosition, string>
                {
                    [new GridPosition(3, 0)] = SecondInstanceId,
                    [new GridPosition(3, 1)] = SecondInstanceId,
                    [new GridPosition(4, 0)] = SecondInstanceId,
                    [new GridPosition(4, 1)] = SecondInstanceId
                },
                new GridPosition(0, 0),
                new GridPosition(1, 0));
        }

        [Test]
        public void Remove_RepeatedCallReturnsInstanceNotFound()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 1);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(instance).Succeeded, Is.True);
            Assert.That(layout.RemoveFurniture(FirstInstanceId).Succeeded, Is.True);

            var result = layout.RemoveFurniture(FirstInstanceId);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.InstanceNotFound));
            Assert.That(
                layout.TryGetFurnitureInstance(FirstInstanceId, out _),
                Is.False);
            AssertExactLayoutState(
                layout,
                Array.Empty<FurnitureInstance>(),
                new Dictionary<GridPosition, string>(),
                new GridPosition(0, 0),
                new GridPosition(1, 0));
        }

        [Test]
        public void Remove_RepeatedCallNeverReleasesOtherFurniture()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 4, 1);
            var removed = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            var remaining = CreateInstance(
                SecondInstanceId,
                TwoByOneDefinitionId,
                2,
                0);
            Assert.That(layout.PlaceFurniture(removed).Succeeded, Is.True);
            Assert.That(layout.PlaceFurniture(remaining).Succeeded, Is.True);
            Assert.That(layout.RemoveFurniture(FirstInstanceId).Succeeded, Is.True);

            var result = layout.RemoveFurniture(FirstInstanceId);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.InstanceNotFound));
            Assert.That(
                layout.TryGetFurnitureInstance(FirstInstanceId, out _),
                Is.False);
            AssertExactLayoutState(
                layout,
                new[] { remaining },
                new Dictionary<GridPosition, string>
                {
                    [new GridPosition(2, 0)] = SecondInstanceId,
                    [new GridPosition(3, 0)] = SecondInstanceId
                },
                new GridPosition(0, 0),
                new GridPosition(1, 0));
        }

        [Test]
        public void Remove_FreesCellsForNewFurniture()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 1);
            var removed = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            var replacement = CreateInstance(
                SecondInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(removed).Succeeded, Is.True);

            var removeResult = layout.RemoveFurniture(FirstInstanceId);
            var placeResult = layout.PlaceFurniture(replacement);

            Assert.That(removeResult.Succeeded, Is.True);
            Assert.That(
                removeResult.FailureReason,
                Is.EqualTo(PlacementFailureReason.None));
            Assert.That(placeResult.Succeeded, Is.True);
            Assert.That(
                placeResult.FailureReason,
                Is.EqualTo(PlacementFailureReason.None));
            Assert.That(
                layout.TryGetFurnitureInstance(FirstInstanceId, out _),
                Is.False);
            AssertExactLayoutState(
                layout,
                new[] { replacement },
                new Dictionary<GridPosition, string>
                {
                    [new GridPosition(0, 0)] = SecondInstanceId,
                    [new GridPosition(1, 0)] = SecondInstanceId
                });
        }

        [Test]
        public void Remove_UnknownValidInstanceReturnsInstanceNotFound()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 1);
            var existing = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(existing).Succeeded, Is.True);

            var result = layout.RemoveFurniture(SecondInstanceId);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.InstanceNotFound));
            Assert.That(
                layout.TryGetFurnitureInstance(SecondInstanceId, out _),
                Is.False);
            AssertExactLayoutState(
                layout,
                new[] { existing },
                new Dictionary<GridPosition, string>
                {
                    [new GridPosition(0, 0)] = FirstInstanceId,
                    [new GridPosition(1, 0)] = FirstInstanceId
                });
        }

        [Test]
        public void Remove_InvalidInstanceIdThrowsWithoutMutation()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 2, 1);
            var existing = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(existing).Succeeded, Is.True);

            var exception = Assert.Throws<ArgumentException>(
                () => layout.RemoveFurniture("invalid-id"));

            Assert.That(exception.ParamName, Is.EqualTo("instanceId"));
            Assert.That(
                layout.TryGetFurnitureInstance(
                    FirstInstanceId,
                    out var indexedInstance),
                Is.True);
            Assert.That(indexedInstance, Is.SameAs(existing));
            AssertExactLayoutState(
                layout,
                new[] { existing },
                new Dictionary<GridPosition, string>
                {
                    [new GridPosition(0, 0)] = FirstInstanceId,
                    [new GridPosition(1, 0)] = FirstInstanceId
                });
        }

        [Test]
        public void Consistency_EveryOccupiedOwnerExistsInLayout()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 5, 3);
            var first = CreateInstance(
                FirstInstanceId,
                TwoByThreeDefinitionId,
                0,
                0,
                FurnitureRotation.Degrees90);
            var second = CreateInstance(
                SecondInstanceId,
                TwoByTwoDefinitionId,
                3,
                1);
            Assert.That(layout.PlaceFurniture(first).Succeeded, Is.True);
            Assert.That(layout.PlaceFurniture(second).Succeeded, Is.True);

            var occupiedCells = new[]
            {
                new GridPosition(0, 0),
                new GridPosition(0, 1),
                new GridPosition(1, 0),
                new GridPosition(1, 1),
                new GridPosition(2, 0),
                new GridPosition(2, 1),
                new GridPosition(3, 1),
                new GridPosition(3, 2),
                new GridPosition(4, 1),
                new GridPosition(4, 2)
            };

            Assert.That(layout.OccupiedCellCount, Is.EqualTo(occupiedCells.Length));
            foreach (var cell in occupiedCells)
            {
                Assert.That(layout.TryGetOccupant(cell, out var owner), Is.True);
                Assert.That(
                    layout.TryGetFurnitureInstance(owner, out var instance),
                    Is.True);
                Assert.That(instance.InstanceId, Is.EqualTo(owner));
            }
        }

        [Test]
        public void Consistency_EveryInstanceOwnsItsFullRotatedFootprint()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 5, 3);
            var first = CreateInstance(
                FirstInstanceId,
                TwoByThreeDefinitionId,
                0,
                0,
                FurnitureRotation.Degrees90);
            var second = CreateInstance(
                SecondInstanceId,
                TwoByOneDefinitionId,
                4,
                0,
                FurnitureRotation.Degrees90);
            Assert.That(layout.PlaceFurniture(first).Succeeded, Is.True);
            Assert.That(layout.PlaceFurniture(second).Succeeded, Is.True);

            Assert.That(layout.FurnitureInstances, Is.EqualTo(new[] { first, second }));
            AssertOccupant(layout, 0, 0, FirstInstanceId);
            AssertOccupant(layout, 0, 1, FirstInstanceId);
            AssertOccupant(layout, 1, 0, FirstInstanceId);
            AssertOccupant(layout, 1, 1, FirstInstanceId);
            AssertOccupant(layout, 2, 0, FirstInstanceId);
            AssertOccupant(layout, 2, 1, FirstInstanceId);
            AssertOccupant(layout, 4, 0, SecondInstanceId);
            AssertOccupant(layout, 4, 1, SecondInstanceId);
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(8));
        }

        [Test]
        public void Consistency_OccupiedCountEqualsSumOfPlacedFootprints()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 6, 3);
            var first = CreateInstance(
                FirstInstanceId,
                TwoByThreeDefinitionId,
                0,
                0,
                FurnitureRotation.Degrees90);
            var second = CreateInstance(
                SecondInstanceId,
                TwoByTwoDefinitionId,
                4,
                0);
            Assert.That(layout.PlaceFurniture(first).Succeeded, Is.True);
            Assert.That(layout.PlaceFurniture(second).Succeeded, Is.True);

            const int firstFootprintArea = 6;
            const int secondFootprintArea = 4;
            Assert.That(
                layout.OccupiedCellCount,
                Is.EqualTo(firstFootprintArea + secondFootprintArea));
            Assert.That(layout.FurnitureInstances.Count, Is.EqualTo(2));
        }

        [Test]
        public void Consistency_RejectedOperationsPreserveInstanceAndOccupancySnapshots()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 5, 3);
            var first = CreateInstance(
                FirstInstanceId,
                TwoByThreeDefinitionId,
                0,
                0);
            var second = CreateInstance(
                SecondInstanceId,
                OneByOneDefinitionId,
                2,
                0);
            Assert.That(layout.PlaceFurniture(first).Succeeded, Is.True);
            Assert.That(layout.PlaceFurniture(second).Succeeded, Is.True);
            var everyCell = Enumerable.Range(0, 5)
                .SelectMany(x => Enumerable.Range(0, 3)
                    .Select(y => new GridPosition(x, y)))
                .ToArray();
            var snapshot = CaptureState(layout, everyCell);
            var rejectedPlace = CreateInstance(
                "33333333333333333333333333333333",
                OneByOneDefinitionId,
                2,
                0);

            var placeResult = layout.PlaceFurniture(rejectedPlace);

            Assert.That(placeResult.Succeeded, Is.False);
            Assert.That(
                placeResult.FailureReason,
                Is.EqualTo(PlacementFailureReason.Overlap));
            AssertStateIsUnchanged(layout, snapshot);

            var moveResult = layout.MoveFurniture(
                FirstInstanceId,
                new GridPosition(1, 0));

            Assert.That(moveResult.Succeeded, Is.False);
            Assert.That(
                moveResult.FailureReason,
                Is.EqualTo(PlacementFailureReason.Overlap));
            AssertStateIsUnchanged(layout, snapshot);

            var rotateResult = layout.RotateFurniture(
                FirstInstanceId,
                FurnitureRotation.Degrees90);

            Assert.That(rotateResult.Succeeded, Is.False);
            Assert.That(
                rotateResult.FailureReason,
                Is.EqualTo(PlacementFailureReason.Overlap));
            AssertStateIsUnchanged(layout, snapshot);
        }

        [Test]
        public void SnapshotGuard_DetectsInPlacePositionMutation()
        {
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 3, 1);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);
            Assert.That(layout.PlaceFurniture(instance).Succeeded, Is.True);
            var snapshot = CaptureState(
                layout,
                new GridPosition(0, 0),
                new GridPosition(1, 0));
            var positionField = typeof(FurnitureInstance).GetField(
                "<Position>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(positionField, Is.Not.Null);

            positionField.SetValue(
                instance,
                new GridPosition(1, 0));

            Assert.Throws<AssertionException>(
                () => AssertStateIsUnchanged(layout, snapshot));
        }

        [Test]
        public void Consistency_AllMutationPathsRemainSceneIndependent()
        {
            var sceneBefore = SceneManager.GetActiveScene();
            var layout = CreateLayout();
            AddRegion(layout, "region.main", 0, 0, 4, 3);
            var instance = CreateInstance(
                FirstInstanceId,
                TwoByOneDefinitionId,
                0,
                0);

            Assert.That(layout.PlaceFurniture(instance).Succeeded, Is.True);
            Assert.That(
                layout.MoveFurniture(
                    FirstInstanceId,
                    new GridPosition(1, 0)).Succeeded,
                Is.True);
            Assert.That(
                layout.RotateFurniture(
                    FirstInstanceId,
                    FurnitureRotation.Degrees90).Succeeded,
                Is.True);
            Assert.That(layout.RemoveFurniture(FirstInstanceId).Succeeded, Is.True);

            var sceneAfter = SceneManager.GetActiveScene();
            Assert.That(sceneAfter.handle, Is.EqualTo(sceneBefore.handle));
            Assert.That(sceneAfter.path, Is.EqualTo(sceneBefore.path));
            Assert.That(layout.FurnitureInstances, Is.Empty);
            Assert.That(layout.OccupiedCellCount, Is.Zero);
        }

        private static CafeLayout CreateLayout()
        {
            var definitions = new[]
            {
                CreateDefinition(OneByOneDefinitionId, 1, 1),
                CreateDefinition(TwoByOneDefinitionId, 2, 1),
                CreateDefinition(TwoByTwoDefinitionId, 2, 2),
                CreateDefinition(TwoByThreeDefinitionId, 2, 3),
                CreateDefinition(ThreeByOneDefinitionId, 3, 1)
            };

            return new CafeLayout(
                new GridSettings(1f),
                new FurnitureDefinitionCatalog(definitions));
        }

        private static FurnitureDefinition CreateDefinition(
            string id,
            int width,
            int height,
            PlacementSurfaceType allowedPlacementSurfaces =
                PlacementSurfaceType.Floor)
        {
            return new FurnitureDefinition(
                id,
                id,
                new GridSize(width, height),
                allowedPlacementSurfaces);
        }

        private static FurnitureInstance CreateInstance(
            string instanceId,
            string definitionId,
            int x,
            int y,
            FurnitureRotation rotation = FurnitureRotation.Degrees0)
        {
            return FurnitureInstance.Restore(
                instanceId,
                definitionId,
                new GridPosition(x, y),
                rotation);
        }

        private static void AddRegion(
            CafeLayout layout,
            string id,
            int x,
            int y,
            int width,
            int height)
        {
            layout.AddRegion(new LayoutRegion(
                id,
                new GridPosition(x, y),
                new GridSize(width, height),
                LayoutZoneType.Interior));
        }

        private static LayoutStateSnapshot CaptureState(
            CafeLayout layout,
            params GridPosition[] watchedCells)
        {
            var instances = layout.FurnitureInstances
                .Select(instance => new FurnitureInstanceStateSnapshot(
                    instance,
                    instance.InstanceId,
                    instance.DefinitionId,
                    instance.Position,
                    instance.Rotation))
                .ToArray();
            var occupants = new Dictionary<GridPosition, string>();

            foreach (var cell in watchedCells)
            {
                layout.TryGetOccupant(cell, out var owner);
                occupants.Add(cell, owner);
            }

            return new LayoutStateSnapshot(
                instances,
                layout.OccupiedCellCount,
                occupants);
        }

        private static void AssertStateIsUnchanged(
            CafeLayout layout,
            LayoutStateSnapshot snapshot)
        {
            Assert.That(
                layout.FurnitureInstances.Count,
                Is.EqualTo(snapshot.Instances.Length));
            Assert.That(
                layout.OccupiedCellCount,
                Is.EqualTo(snapshot.OccupiedCellCount));

            for (var index = 0; index < snapshot.Instances.Length; index++)
            {
                var expected = snapshot.Instances[index];
                var actual = layout.FurnitureInstances[index];

                Assert.That(actual, Is.SameAs(expected.ObjectReference));
                Assert.That(actual.InstanceId, Is.EqualTo(expected.InstanceId));
                Assert.That(actual.DefinitionId, Is.EqualTo(expected.DefinitionId));
                Assert.That(actual.Position, Is.EqualTo(expected.Position));
                Assert.That(actual.Rotation, Is.EqualTo(expected.Rotation));
                Assert.That(
                    layout.TryGetFurnitureInstance(
                        expected.InstanceId,
                        out var indexedInstance),
                    Is.True);
                Assert.That(
                    indexedInstance,
                    Is.SameAs(expected.ObjectReference));
            }

            foreach (var expected in snapshot.Occupants)
            {
                var found = layout.TryGetOccupant(
                    expected.Key,
                    out var actualOwner);

                Assert.That(found, Is.EqualTo(expected.Value != null));
                Assert.That(actualOwner, Is.EqualTo(expected.Value));
            }
        }

        private static void AssertSuccessfulReplacement(
            CafeLayout layout,
            FurnitureInstance original,
            GridPosition expectedPosition,
            FurnitureRotation expectedRotation,
            int expectedInstanceCount = 1)
        {
            Assert.That(
                layout.TryGetFurnitureInstance(
                    original.InstanceId,
                    out var replacement),
                Is.True);
            Assert.That(replacement, Is.Not.SameAs(original));
            Assert.That(replacement.InstanceId, Is.EqualTo(original.InstanceId));
            Assert.That(replacement.DefinitionId, Is.EqualTo(original.DefinitionId));
            Assert.That(replacement.Position, Is.EqualTo(expectedPosition));
            Assert.That(replacement.Rotation, Is.EqualTo(expectedRotation));
            Assert.That(
                layout.FurnitureInstances.Count,
                Is.EqualTo(expectedInstanceCount));
            Assert.That(layout.FurnitureInstances[0], Is.SameAs(replacement));
        }

        private static void AssertExactLayoutState(
            CafeLayout layout,
            IReadOnlyList<FurnitureInstance> expectedInstances,
            IReadOnlyDictionary<GridPosition, string> expectedOccupants,
            params GridPosition[] expectedEmptyCells)
        {
            Assert.That(layout.FurnitureInstances, Is.EqualTo(expectedInstances));
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(expectedOccupants.Count));

            foreach (var expectedInstance in expectedInstances)
            {
                Assert.That(
                    layout.TryGetFurnitureInstance(
                        expectedInstance.InstanceId,
                        out var actualInstance),
                    Is.True);
                Assert.That(actualInstance, Is.SameAs(expectedInstance));
            }

            foreach (var expectedOccupant in expectedOccupants)
            {
                Assert.That(
                    layout.TryGetOccupant(
                        expectedOccupant.Key,
                        out var actualOwner),
                    Is.True);
                Assert.That(actualOwner, Is.EqualTo(expectedOccupant.Value));
            }

            foreach (var emptyCell in expectedEmptyCells)
            {
                Assert.That(layout.TryGetOccupant(emptyCell, out var owner), Is.False);
                Assert.That(owner, Is.Null);
            }
        }

        private static void AssertRejectedWithoutMutation(
            CafeLayout layout,
            FurnitureInstance instance,
            PlacementResult result,
            PlacementFailureReason expectedReason)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(expectedReason));
            Assert.That(layout.FurnitureInstances, Is.Empty);
            Assert.That(layout.OccupiedCellCount, Is.Zero);
            Assert.That(
                layout.TryGetFurnitureInstance(instance.InstanceId, out _),
                Is.False);
        }

        private static void AssertOccupant(
            CafeLayout layout,
            int x,
            int y,
            string expectedInstanceId)
        {
            Assert.That(
                layout.TryGetOccupant(
                    new GridPosition(x, y),
                    out var instanceId),
                Is.True);
            Assert.That(instanceId, Is.EqualTo(expectedInstanceId));
        }

        private static void AssertCellIsEmpty(
            CafeLayout layout,
            int x,
            int y)
        {
            Assert.That(
                layout.TryGetOccupant(
                    new GridPosition(x, y),
                    out var instanceId),
                Is.False);
            Assert.That(instanceId, Is.Null);
        }

        private sealed class LayoutStateSnapshot
        {
            public FurnitureInstanceStateSnapshot[] Instances { get; }
            public int OccupiedCellCount { get; }
            public IReadOnlyDictionary<GridPosition, string> Occupants { get; }

            public LayoutStateSnapshot(
                FurnitureInstanceStateSnapshot[] instances,
                int occupiedCellCount,
                IReadOnlyDictionary<GridPosition, string> occupants)
            {
                Instances = instances;
                OccupiedCellCount = occupiedCellCount;
                Occupants = occupants;
            }
        }

        private sealed class FurnitureInstanceStateSnapshot
        {
            public FurnitureInstance ObjectReference { get; }
            public string InstanceId { get; }
            public string DefinitionId { get; }
            public GridPosition Position { get; }
            public FurnitureRotation Rotation { get; }

            public FurnitureInstanceStateSnapshot(
                FurnitureInstance objectReference,
                string instanceId,
                string definitionId,
                GridPosition position,
                FurnitureRotation rotation)
            {
                ObjectReference = objectReference;
                InstanceId = instanceId;
                DefinitionId = definitionId;
                Position = position;
                Rotation = rotation;
            }
        }
    }
}

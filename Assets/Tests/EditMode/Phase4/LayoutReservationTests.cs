using System;
using System.Collections.Generic;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.Phase4
{
    public sealed class LayoutReservationTests
    {
        private const string CounterDefinitionId = "furniture.counter";
        private const string TallDefinitionId = "furniture.tall";
        private const string CounterInstanceId = "11111111111111111111111111111111";
        private const string TallInstanceId = "22222222222222222222222222222222";

        [Test]
        public void EntranceReservation_ContainsAllFourCells()
        {
            var reservation = new LayoutReservation(
                "entrance.main",
                LayoutReservationType.EntranceClearance,
                new GridPosition(3, 0),
                new GridSize(2, 2));

            Assert.That(reservation.Contains(new GridPosition(3, 0)), Is.True);
            Assert.That(reservation.Contains(new GridPosition(4, 1)), Is.True);
            Assert.That(reservation.Contains(new GridPosition(5, 1)), Is.False);
        }

        [TestCase(null, typeof(ArgumentNullException))]
        [TestCase("", typeof(ArgumentException))]
        [TestCase("   ", typeof(ArgumentException))]
        [TestCase("Entrance Main", typeof(ArgumentException))]
        public void LayoutReservation_RejectsNullOrMalformedId(
            string id,
            Type exceptionType)
        {
            Assert.Throws(exceptionType, () => new LayoutReservation(
                id,
                LayoutReservationType.EntranceClearance,
                new GridPosition(0, 0),
                new GridSize(1, 1)));
        }

        [Test]
        public void LayoutReservation_RejectsUnknownType()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LayoutReservation(
                    "entrance.main",
                    (LayoutReservationType)99,
                    new GridPosition(0, 0),
                    new GridSize(1, 1)));
        }

        [Test]
        public void LayoutReservation_UsesOverflowSafeBounds()
        {
            var reservation = new LayoutReservation(
                "entrance.edge",
                LayoutReservationType.EntranceClearance,
                new GridPosition(int.MaxValue - 1, int.MaxValue - 1),
                new GridSize(2, 2));

            Assert.That(
                reservation.Contains(new GridPosition(int.MaxValue, int.MaxValue)),
                Is.True);
            Assert.That(
                reservation.Contains(new GridPosition(int.MinValue, int.MinValue)),
                Is.False);
        }

        [Test]
        public void LayoutReservation_AllowsNonTwoByTwoSizeForFutureFixtureValidation()
        {
            var reservation = new LayoutReservation(
                "entrance.future-fixture",
                LayoutReservationType.EntranceClearance,
                new GridPosition(2, 3),
                new GridSize(1, 3));

            Assert.That(reservation.Contains(new GridPosition(2, 5)), Is.True);
            Assert.That(reservation.Contains(new GridPosition(3, 5)), Is.False);
        }

        [Test]
        public void CafeLayout_AddReservationExposesReadOnlyReservationView()
        {
            var layout = CreateUnlockedEightByEightLayout();
            var reservation = CreateEntranceClearance(new GridPosition(3, 0));

            layout.AddReservation(reservation);

            Assert.That(layout.Reservations, Is.EqualTo(new[] { reservation }));
            var reservations = layout.Reservations as IList<LayoutReservation>;
            Assert.That(reservations, Is.Not.Null);
            Assert.Throws<NotSupportedException>(() => reservations.Add(
                CreateEntranceClearance(new GridPosition(0, 0))));
        }

        [Test]
        public void CafeLayout_AddReservationRejectsNullWithoutMutation()
        {
            var layout = CreateUnlockedEightByEightLayout();

            Assert.Throws<ArgumentNullException>(() => layout.AddReservation(null));

            Assert.That(layout.Reservations, Is.Empty);
        }

        [Test]
        public void CafeLayout_AddReservationRejectsDuplicateIdWithoutMutation()
        {
            var layout = CreateUnlockedEightByEightLayout();
            var original = CreateEntranceClearance(new GridPosition(3, 0));
            layout.AddReservation(original);

            var exception = Assert.Throws<ArgumentException>(() =>
                layout.AddReservation(new LayoutReservation(
                    "entrance.main",
                    LayoutReservationType.EntranceClearance,
                    new GridPosition(5, 5),
                    new GridSize(1, 1))));

            StringAssert.Contains("entrance.main", exception.Message);
            Assert.That(layout.Reservations, Is.EqualTo(new[] { original }));
        }

        [Test]
        public void CafeLayout_AddReservationOverlappingPlacedFurnitureRejectsWithoutMutation()
        {
            var layout = CreateUnlockedEightByEightLayout();
            var instance = CreateInstance(
                CounterInstanceId,
                CounterDefinitionId,
                new GridPosition(0, 0));
            Assert.That(layout.PlaceFurniture(instance).Succeeded, Is.True);

            var exception = Assert.Throws<ArgumentException>(() =>
                layout.AddReservation(CreateEntranceClearance(new GridPosition(0, 0))));

            StringAssert.Contains("entrance.main", exception.Message);
            Assert.That(layout.Reservations, Is.Empty);
            Assert.That(layout.FurnitureInstances, Is.EqualTo(new[] { instance }));
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(2));
            Assert.That(
                layout.TryGetOccupant(new GridPosition(0, 0), out var firstOwner),
                Is.True);
            Assert.That(firstOwner, Is.EqualTo(CounterInstanceId));
            Assert.That(
                layout.TryGetOccupant(new GridPosition(1, 0), out var secondOwner),
                Is.True);
            Assert.That(secondOwner, Is.EqualTo(CounterInstanceId));
        }

        [Test]
        public void PlaceFurniture_IntersectingEntranceClearance_IsRejectedAtomically()
        {
            var layout = CreateUnlockedEightByEightLayout();
            layout.AddReservation(CreateEntranceClearance(new GridPosition(3, 0)));

            var result = layout.PlaceFurniture(CreateInstance(
                CounterInstanceId,
                CounterDefinitionId,
                new GridPosition(4, 1)));

            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.ReservedEntranceClearance));
            Assert.That(layout.OccupiedCellCount, Is.Zero);
            Assert.That(layout.FurnitureInstances, Is.Empty);
        }

        [Test]
        public void MoveFurniture_IntersectingEntranceClearance_PreservesOriginalPlacement()
        {
            var layout = CreateUnlockedEightByEightLayout();
            var instance = CreateInstance(
                CounterInstanceId,
                CounterDefinitionId,
                new GridPosition(0, 0));
            Assert.That(layout.PlaceFurniture(instance).Succeeded, Is.True);
            layout.AddReservation(CreateEntranceClearance(new GridPosition(3, 0)));

            var result = layout.MoveFurniture(
                CounterInstanceId,
                new GridPosition(3, 0));

            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.ReservedEntranceClearance));
            AssertOriginalPlacementIsUnchanged(
                layout,
                instance,
                new GridPosition(0, 0),
                new GridPosition(1, 0));
        }

        [Test]
        public void RotateFurniture_IntersectingEntranceClearance_PreservesOriginalPlacement()
        {
            var layout = CreateUnlockedEightByEightLayout();
            var instance = CreateInstance(
                TallInstanceId,
                TallDefinitionId,
                new GridPosition(2, 0));
            Assert.That(layout.PlaceFurniture(instance).Succeeded, Is.True);
            layout.AddReservation(CreateEntranceClearance(new GridPosition(3, 0)));

            var result = layout.RotateFurniture(
                TallInstanceId,
                FurnitureRotation.Degrees90);

            Assert.That(
                result.FailureReason,
                Is.EqualTo(PlacementFailureReason.ReservedEntranceClearance));
            AssertOriginalPlacementIsUnchanged(
                layout,
                instance,
                new GridPosition(2, 0),
                new GridPosition(2, 1));
        }

        [Test]
        public void Reservation_DoesNotCreateFurnitureOccupancy()
        {
            var layout = CreateUnlockedEightByEightLayout();
            layout.AddReservation(CreateEntranceClearance(new GridPosition(3, 0)));

            var hasOccupant = layout.TryGetOccupant(
                new GridPosition(3, 0),
                out var occupantId);

            Assert.That(hasOccupant, Is.False);
            Assert.That(occupantId, Is.Null);
            Assert.That(layout.OccupiedCellCount, Is.Zero);
            Assert.That(layout.FurnitureInstances, Is.Empty);
        }

        private static CafeLayout CreateUnlockedEightByEightLayout()
        {
            var layout = new CafeLayout(
                new GridSettings(1f),
                new FurnitureDefinitionCatalog(new[]
                {
                    new FurnitureDefinition(
                        CounterDefinitionId,
                        "Counter",
                        new GridSize(2, 1),
                        PlacementSurfaceType.Floor),
                    new FurnitureDefinition(
                        TallDefinitionId,
                        "Tall Fixture",
                        new GridSize(1, 2),
                        PlacementSurfaceType.Floor)
                }));
            layout.AddRegion(new LayoutRegion(
                "region.main",
                new GridPosition(0, 0),
                new GridSize(8, 8),
                LayoutZoneType.Interior));
            return layout;
        }

        private static LayoutReservation CreateEntranceClearance(GridPosition origin)
        {
            return new LayoutReservation(
                "entrance.main",
                LayoutReservationType.EntranceClearance,
                origin,
                new GridSize(2, 2));
        }

        private static FurnitureInstance CreateInstance(
            string instanceId,
            string definitionId,
            GridPosition position)
        {
            return FurnitureInstance.Restore(
                instanceId,
                definitionId,
                position,
                FurnitureRotation.Degrees0);
        }

        private static void AssertOriginalPlacementIsUnchanged(
            CafeLayout layout,
            FurnitureInstance original,
            params GridPosition[] occupiedCells)
        {
            Assert.That(layout.FurnitureInstances, Is.EqualTo(new[] { original }));
            Assert.That(layout.OccupiedCellCount, Is.EqualTo(occupiedCells.Length));
            Assert.That(
                layout.TryGetFurnitureInstance(original.InstanceId, out var indexed),
                Is.True);
            Assert.That(indexed, Is.SameAs(original));

            foreach (var cell in occupiedCells)
            {
                Assert.That(layout.TryGetOccupant(cell, out var owner), Is.True);
                Assert.That(owner, Is.EqualTo(original.InstanceId));
            }
        }

    }
}

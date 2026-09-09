using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class InteractionAnchorResolverTests
    {
        private const string SupportDefinitionId = "furniture.counter.long";
        private const string CashRegisterDefinitionId = "equipment.cash-register";
        private const string CoffeeMachineDefinitionId = "equipment.coffee-machine";
        private const string SupportInstanceId = "7f17d8fa59f64be0a6689666ce4a28d2";
        private const string MountedInstanceId = "9f17d8fa59f64be0a6689666ce4a28d2";
        private const string PickUpInstanceId = "af17d8fa59f64be0a6689666ce4a28d2";

        private static readonly object[] CashRegisterRotationCases =
        {
            new object[] { FurnitureRotation.Degrees0, new GridPosition(10, 13), CardinalDirection.South, new GridPosition(10, 11), CardinalDirection.North },
            new object[] { FurnitureRotation.Degrees90, new GridPosition(11, 12), CardinalDirection.West, new GridPosition(9, 12), CardinalDirection.East },
            new object[] { FurnitureRotation.Degrees180, new GridPosition(10, 11), CardinalDirection.North, new GridPosition(10, 13), CardinalDirection.South },
            new object[] { FurnitureRotation.Degrees270, new GridPosition(9, 12), CardinalDirection.East, new GridPosition(11, 12), CardinalDirection.West }
        };

        private static readonly object[] CoffeeMachineRotationCases =
        {
            new object[] { FurnitureRotation.Degrees0, new GridPosition(10, 13), CardinalDirection.South },
            new object[] { FurnitureRotation.Degrees90, new GridPosition(11, 12), CardinalDirection.West },
            new object[] { FurnitureRotation.Degrees180, new GridPosition(10, 11), CardinalDirection.North },
            new object[] { FurnitureRotation.Degrees270, new GridPosition(9, 12), CardinalDirection.East }
        };

        [TestCaseSource(nameof(CashRegisterRotationCases))]
        public void ResolveMounted_CashRegisterReturnsExactAutomaticAnchorsForEveryEquipmentRotation(
            FurnitureRotation rotation,
            GridPosition expectedEmployeePosition,
            CardinalDirection expectedEmployeeFacing,
            GridPosition expectedCustomerPosition,
            CardinalDirection expectedCustomerFacing)
        {
            var layout = CreateLayout(FurnitureRotation.Degrees0, new GridPosition(10, 10));
            var instance = Mounted(CashRegisterDefinitionId, rotation);

            var result = new InteractionAnchorResolver().ResolveMounted(
                instance,
                layout,
                CreateSlots(),
                CreateDirections());

            AssertAnchor(result, InteractionRole.Employee, expectedEmployeePosition, expectedEmployeeFacing);
            AssertAnchor(result, InteractionRole.Customer, expectedCustomerPosition, expectedCustomerFacing);
            Assert.That(result.Anchors, Has.Count.EqualTo(2));
        }

        [TestCaseSource(nameof(CoffeeMachineRotationCases))]
        public void ResolveMounted_CoffeeMachineReturnsOnlyEmployeeAnchorForEveryEquipmentRotation(
            FurnitureRotation rotation,
            GridPosition expectedPosition,
            CardinalDirection expectedFacing)
        {
            var layout = CreateLayout(FurnitureRotation.Degrees0, new GridPosition(10, 10));

            var result = new InteractionAnchorResolver().ResolveMounted(
                Mounted(CoffeeMachineDefinitionId, rotation),
                layout,
                CreateSlots(),
                CreateDirections());

            AssertAnchor(result, InteractionRole.Employee, expectedPosition, expectedFacing);
            Assert.That(result.TryGetAnchor(InteractionRole.Customer, out _), Is.False);
            Assert.That(result.Anchors, Has.Count.EqualTo(1));
        }

        [Test]
        public void ResolveMounted_RecalculatesFromCurrentSupportMoveAndCombinedRotationsWithoutChangingBinding()
        {
            var layout = CreateLayout(FurnitureRotation.Degrees0, new GridPosition(10, 10));
            var instance = Mounted(CashRegisterDefinitionId, FurnitureRotation.Degrees90);
            var resolver = new InteractionAnchorResolver();

            var before = resolver.ResolveMounted(instance, layout, CreateSlots(), CreateDirections());
            AssertAnchor(before, InteractionRole.Employee, new GridPosition(11, 12), CardinalDirection.West);
            Assert.That(layout.MoveFurniture(SupportInstanceId, new GridPosition(20, 20)).Succeeded, Is.True);
            Assert.That(layout.RotateFurniture(SupportInstanceId, FurnitureRotation.Degrees90).Succeeded, Is.True);

            var after = resolver.ResolveMounted(instance, layout, CreateSlots(), CreateDirections());

            Assert.That(instance.Address, Is.EqualTo(new SurfaceSlotAddress(SupportInstanceId, "slot.2")));
            AssertAnchor(after, InteractionRole.Employee, new GridPosition(22, 19), CardinalDirection.North);
            AssertAnchor(after, InteractionRole.Customer, new GridPosition(22, 21), CardinalDirection.South);
        }

        [Test]
        public void ResolveMounted_PreservesCalculatedAnchorWhenCellIsOccupiedForReadinessValidation()
        {
            var layout = CreateLayout(FurnitureRotation.Degrees0, new GridPosition(10, 10));
            Assert.That(layout.PlaceFurniture(FurnitureInstance.Restore(
                "8f17d8fa59f64be0a6689666ce4a28d2",
                "furniture.blocker",
                new GridPosition(10, 13),
                FurnitureRotation.Degrees0)).Succeeded, Is.True);

            var result = new InteractionAnchorResolver().ResolveMounted(
                Mounted(CashRegisterDefinitionId, FurnitureRotation.Degrees0),
                layout,
                CreateSlots(),
                CreateDirections());

            AssertAnchor(result, InteractionRole.Employee, new GridPosition(10, 13), CardinalDirection.South);
            Assert.That(layout.TryGetOccupant(new GridPosition(10, 13), out _), Is.True);
        }

        [Test]
        public void ResolveMounted_PreservesCalculatedAnchorOutsideUnlockedRegionForReadinessValidation()
        {
            var layout = CreateLayout(FurnitureRotation.Degrees0, new GridPosition(10, 37));

            var result = new InteractionAnchorResolver().ResolveMounted(
                Mounted(CashRegisterDefinitionId, FurnitureRotation.Degrees0),
                layout,
                CreateSlots(),
                CreateDirections());

            AssertAnchor(result, InteractionRole.Employee, new GridPosition(10, 40), CardinalDirection.South);
        }

        [Test]
        public void ResolveMounted_PreservesCalculatedAnchorOnBlockedReservationForReadinessValidation()
        {
            var layout = CreateLayout(FurnitureRotation.Degrees0, new GridPosition(10, 10));
            layout.AddReservation(new LayoutReservation(
                "blocked.anchor",
                LayoutReservationType.Blocked,
                new GridPosition(10, 13),
                new GridSize(1, 1)));

            var result = new InteractionAnchorResolver().ResolveMounted(
                Mounted(CashRegisterDefinitionId, FurnitureRotation.Degrees0),
                layout,
                CreateSlots(),
                CreateDirections());

            AssertAnchor(result, InteractionRole.Employee, new GridPosition(10, 13), CardinalDirection.South);
        }

        [Test]
        public void ResolvePickUp_PrefersNorthSouthOppositePairAndIsDeterministic()
        {
            var layout = CreateLayout(FurnitureRotation.Degrees0, new GridPosition(10, 10));
            var resolver = new InteractionAnchorResolver();
            var instance = PickUp();

            var first = resolver.ResolvePickUp(instance, layout, CreateSlots(), _ => true);
            var second = resolver.ResolvePickUp(instance, layout, CreateSlots(), _ => true);

            AssertAnchor(first, InteractionRole.Employee, new GridPosition(10, 13), CardinalDirection.South);
            AssertAnchor(first, InteractionRole.Customer, new GridPosition(10, 11), CardinalDirection.North);
            Assert.That(second.Anchors, Is.EqualTo(first.Anchors));
        }

        [Test]
        public void ResolvePickUp_UsesEastWestWhenFirstOppositePairIsUnavailable()
        {
            var result = ResolvePickUpWhere(position =>
                position == new GridPosition(11, 12) ||
                position == new GridPosition(9, 12));

            AssertAnchor(result, InteractionRole.Employee, new GridPosition(11, 12), CardinalDirection.West);
            AssertAnchor(result, InteractionRole.Customer, new GridPosition(9, 12), CardinalDirection.East);
        }

        [Test]
        public void ResolvePickUp_UsesFirstTwoDistinctWalkableCellsInCardinalOrder()
        {
            var result = ResolvePickUpWhere(position =>
                position == new GridPosition(10, 13) ||
                position == new GridPosition(11, 12));

            AssertAnchor(result, InteractionRole.Employee, new GridPosition(10, 13), CardinalDirection.South);
            AssertAnchor(result, InteractionRole.Customer, new GridPosition(11, 12), CardinalDirection.West);
        }

        [Test]
        public void ResolvePickUp_SharesTheOnlyWalkableCellBetweenBothRoles()
        {
            var result = ResolvePickUpWhere(position => position == new GridPosition(9, 12));

            AssertAnchor(result, InteractionRole.Employee, new GridPosition(9, 12), CardinalDirection.East);
            AssertAnchor(result, InteractionRole.Customer, new GridPosition(9, 12), CardinalDirection.East);
        }

        [Test]
        public void ResolvePickUp_NoWalkableAdjacentCellReturnsNoAnchors()
        {
            var result = ResolvePickUpWhere(_ => false);

            Assert.That(result.Anchors, Is.Empty);
        }

        private static ResolvedStationAnchors ResolvePickUpWhere(Func<GridPosition, bool> isWalkable)
        {
            var layout = CreateLayout(FurnitureRotation.Degrees0, new GridPosition(10, 10));
            return new InteractionAnchorResolver().ResolvePickUp(
                PickUp(),
                layout,
                CreateSlots(),
                isWalkable);
        }

        private static CafeLayout CreateLayout(FurnitureRotation rotation, GridPosition position)
        {
            var catalog = new FurnitureDefinitionCatalog(new[]
            {
                new FurnitureDefinition(
                    SupportDefinitionId,
                    "Long Counter",
                    new GridSize(1, 3),
                    PlacementSurfaceType.Floor),
                new FurnitureDefinition(
                    "furniture.blocker",
                    "Blocker",
                    new GridSize(1, 1),
                    PlacementSurfaceType.Floor)
            });
            var layout = new CafeLayout(new GridSettings(1f), catalog);
            layout.AddRegion(new LayoutRegion(
                "interior",
                new GridPosition(0, 0),
                new GridSize(40, 40),
                LayoutZoneType.Interior));
            Assert.That(layout.PlaceFurniture(FurnitureInstance.Restore(
                SupportInstanceId,
                SupportDefinitionId,
                position,
                rotation)).Succeeded, Is.True);
            return layout;
        }

        private static SurfaceSlotCatalog CreateSlots()
        {
            return new SurfaceSlotCatalog(new[]
            {
                new SurfaceSlotDefinition(SupportDefinitionId, "slot.2", new GridPosition(0, 2))
            });
        }

        private static FunctionalDirectionCatalog CreateDirections()
        {
            return new FunctionalDirectionCatalog(
                new Dictionary<string, CashRegisterSides>
                {
                    [CashRegisterDefinitionId] = new CashRegisterSides(
                        CardinalDirection.North,
                        CardinalDirection.South)
                },
                new Dictionary<string, CardinalDirection>
                {
                    [CoffeeMachineDefinitionId] = CardinalDirection.North
                });
        }

        private static SurfaceMountedInstance Mounted(
            string definitionId,
            FurnitureRotation rotation)
        {
            return new SurfaceMountedInstance(
                MountedInstanceId,
                definitionId,
                new SurfaceSlotAddress(SupportInstanceId, "slot.2"),
                rotation);
        }

        private static PickUpPointInstance PickUp()
        {
            return new PickUpPointInstance(
                PickUpInstanceId,
                new SurfaceSlotAddress(SupportInstanceId, "slot.2"));
        }

        private static void AssertAnchor(
            ResolvedStationAnchors result,
            InteractionRole role,
            GridPosition expectedPosition,
            CardinalDirection expectedFacing)
        {
            Assert.That(result.TryGetAnchor(role, out var anchor), Is.True);
            Assert.That(anchor.Role, Is.EqualTo(role));
            Assert.That(anchor.Position, Is.EqualTo(expectedPosition));
            Assert.That(anchor.Facing, Is.EqualTo(expectedFacing));
        }
    }
}

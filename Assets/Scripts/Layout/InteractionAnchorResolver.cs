using System;
using System.Collections.Generic;
using AnimalCafe.Content;

namespace AnimalCafe.Layout
{
    public sealed class InteractionAnchorResolver
    {
        private static readonly CardinalDirection[] CandidateDirections =
        {
            CardinalDirection.North,
            CardinalDirection.East,
            CardinalDirection.South,
            CardinalDirection.West
        };

        public ResolvedStationAnchors ResolveMounted(
            SurfaceMountedInstance instance,
            CafeLayout cafeLayout,
            SurfaceSlotCatalog slots,
            FunctionalDirectionCatalog directions)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (cafeLayout == null) throw new ArgumentNullException(nameof(cafeLayout));
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (directions == null) throw new ArgumentNullException(nameof(directions));

            if (!TryResolveSlotCell(instance.Address, cafeLayout, slots, out var slotCell, out var supportRotation))
            {
                return ResolvedStationAnchors.Empty;
            }

            if (directions.TryGetCashRegisterSides(instance.DefinitionId, out var sides))
            {
                var rotatedSides = sides
                    .Rotate(instance.Rotation)
                    .Rotate(supportRotation);
                return new ResolvedStationAnchors(new[]
                {
                    CreateAnchor(InteractionRole.Employee, slotCell, rotatedSides.EmployeeSide),
                    CreateAnchor(InteractionRole.Customer, slotCell, rotatedSides.CustomerSide)
                });
            }

            if (directions.TryGetCoffeeMachineDirection(instance.DefinitionId, out var direction))
            {
                var worldDirection = direction
                    .Rotate(instance.Rotation)
                    .Rotate(supportRotation);
                return new ResolvedStationAnchors(new[]
                {
                    CreateAnchor(InteractionRole.Employee, slotCell, worldDirection)
                });
            }

            return ResolvedStationAnchors.Empty;
        }

        public ResolvedStationAnchors ResolvePickUp(
            PickUpPointInstance instance,
            CafeLayout cafeLayout,
            SurfaceSlotCatalog slots,
            Func<GridPosition, bool> isWalkable)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (cafeLayout == null) throw new ArgumentNullException(nameof(cafeLayout));
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (isWalkable == null) throw new ArgumentNullException(nameof(isWalkable));

            if (!TryResolveSlotCell(instance.Address, cafeLayout, slots, out var slotCell, out _))
            {
                return ResolvedStationAnchors.Empty;
            }

            var walkableDirections = new List<CardinalDirection>();
            foreach (var direction in CandidateDirections)
            {
                if (TryOffset(slotCell, direction, out var candidate) && isWalkable(candidate))
                {
                    walkableDirections.Add(direction);
                }
            }

            if (walkableDirections.Count == 0)
            {
                return ResolvedStationAnchors.Empty;
            }

            for (var index = 0; index < walkableDirections.Count; index++)
            {
                var first = walkableDirections[index];
                var opposite = first.Rotate(FurnitureRotation.Degrees180);
                if (walkableDirections.Contains(opposite))
                {
                    return CreatePair(slotCell, first, opposite);
                }
            }

            if (walkableDirections.Count > 1)
            {
                return CreatePair(slotCell, walkableDirections[0], walkableDirections[1]);
            }

            return CreatePair(slotCell, walkableDirections[0], walkableDirections[0]);
        }

        private static ResolvedStationAnchors CreatePair(
            GridPosition slotCell,
            CardinalDirection employeeDirection,
            CardinalDirection customerDirection)
        {
            return new ResolvedStationAnchors(new[]
            {
                CreateAnchor(InteractionRole.Employee, slotCell, employeeDirection),
                CreateAnchor(InteractionRole.Customer, slotCell, customerDirection)
            });
        }

        private static InteractionAnchor CreateAnchor(
            InteractionRole role,
            GridPosition slotCell,
            CardinalDirection outwardDirection)
        {
            if (!TryOffset(slotCell, outwardDirection, out var position))
            {
                throw new InvalidOperationException("Interaction anchor position exceeds GridPosition range.");
            }

            return new InteractionAnchor(
                role,
                position,
                outwardDirection.Rotate(FurnitureRotation.Degrees180));
        }

        private static bool TryResolveSlotCell(
            SurfaceSlotAddress address,
            CafeLayout cafeLayout,
            SurfaceSlotCatalog slots,
            out GridPosition worldCell,
            out FurnitureRotation supportRotation)
        {
            worldCell = default;
            supportRotation = FurnitureRotation.Degrees0;

            if (!cafeLayout.TryGetFurnitureInstance(
                address.SupportFurnitureInstanceId,
                out var support) ||
                !slots.TryGet(support.DefinitionId, address.SlotId, out var slot))
            {
                return false;
            }

            var neutralFootprint = cafeLayout.GetFurnitureFootprintCells(
                support.DefinitionId,
                new GridPosition(0, 0),
                FurnitureRotation.Degrees0);
            var width = 0;
            var height = 0;
            foreach (var cell in neutralFootprint)
            {
                width = Math.Max(width, cell.X + 1);
                height = Math.Max(height, cell.Y + 1);
            }

            if (slot.LocalCell.X < 0 || slot.LocalCell.X >= width ||
                slot.LocalCell.Y < 0 || slot.LocalCell.Y >= height)
            {
                return false;
            }

            var rotatedLocalCell = RotateLocalCell(
                slot.LocalCell,
                width,
                height,
                support.Rotation);
            if (!TryAdd(support.Position, rotatedLocalCell, out worldCell))
            {
                return false;
            }

            supportRotation = support.Rotation;
            return true;
        }

        private static GridPosition RotateLocalCell(
            GridPosition localCell,
            int width,
            int height,
            FurnitureRotation rotation)
        {
            switch (rotation)
            {
                case FurnitureRotation.Degrees0:
                    return localCell;
                case FurnitureRotation.Degrees90:
                    return new GridPosition(localCell.Y, width - 1 - localCell.X);
                case FurnitureRotation.Degrees180:
                    return new GridPosition(width - 1 - localCell.X, height - 1 - localCell.Y);
                case FurnitureRotation.Degrees270:
                    return new GridPosition(height - 1 - localCell.Y, localCell.X);
                default:
                    throw new ArgumentOutOfRangeException(nameof(rotation), rotation, "Rotation must be a defined quarter turn.");
            }
        }

        private static bool TryOffset(
            GridPosition position,
            CardinalDirection direction,
            out GridPosition result)
        {
            switch (direction)
            {
                case CardinalDirection.North:
                    return TryAdd(position, new GridPosition(0, 1), out result);
                case CardinalDirection.East:
                    return TryAdd(position, new GridPosition(1, 0), out result);
                case CardinalDirection.South:
                    return TryAdd(position, new GridPosition(0, -1), out result);
                case CardinalDirection.West:
                    return TryAdd(position, new GridPosition(-1, 0), out result);
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, "Direction must be cardinal.");
            }
        }

        private static bool TryAdd(
            GridPosition first,
            GridPosition second,
            out GridPosition result)
        {
            var x = (long)first.X + second.X;
            var y = (long)first.Y + second.Y;
            if (x < int.MinValue || x > int.MaxValue ||
                y < int.MinValue || y > int.MaxValue)
            {
                result = default;
                return false;
            }

            result = new GridPosition((int)x, (int)y);
            return true;
        }
    }
}

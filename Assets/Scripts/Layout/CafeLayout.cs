using System;
using System.Collections.Generic;

namespace AnimalCafe.Layout
{
    public sealed class CafeLayout
    {
        private readonly FurnitureDefinitionCatalog definitionCatalog;
        private readonly List<LayoutRegion> unlockedRegions;
        private readonly Dictionary<string, LayoutRegion> regionsById;
        private readonly List<LayoutReservation> reservations;
        private readonly Dictionary<string, LayoutReservation> reservationsById;
        private readonly List<FurnitureInstance> furnitureInstances;
        private readonly Dictionary<string, FurnitureInstance> furnitureInstancesById;
        private readonly Dictionary<GridPosition, string> occupantByCell;
        private readonly LayoutBounds? layoutBounds;

        public GridSettings GridSettings { get; }
        public IReadOnlyList<LayoutRegion> UnlockedRegions { get; }
        public IReadOnlyList<LayoutReservation> Reservations { get; }
        public IReadOnlyList<FurnitureInstance> FurnitureInstances { get; }
        public int OccupiedCellCount => occupantByCell.Count;

        public CafeLayout(
            GridSettings gridSettings,
            FurnitureDefinitionCatalog definitionCatalog)
            : this(gridSettings, definitionCatalog, null)
        {
        }

        public CafeLayout(
            GridSettings gridSettings,
            FurnitureDefinitionCatalog definitionCatalog,
            LayoutBounds layoutBounds)
            : this(gridSettings, definitionCatalog, (LayoutBounds?)layoutBounds)
        {
        }

        private CafeLayout(
            GridSettings gridSettings,
            FurnitureDefinitionCatalog definitionCatalog,
            LayoutBounds? layoutBounds)
        {
            GridSettings = gridSettings ??
                throw new ArgumentNullException(nameof(gridSettings));
            this.definitionCatalog = definitionCatalog ??
                throw new ArgumentNullException(nameof(definitionCatalog));
            this.layoutBounds = layoutBounds;

            unlockedRegions = new List<LayoutRegion>();
            regionsById = new Dictionary<string, LayoutRegion>(StringComparer.Ordinal);
            reservations = new List<LayoutReservation>();
            reservationsById =
                new Dictionary<string, LayoutReservation>(StringComparer.Ordinal);
            furnitureInstances = new List<FurnitureInstance>();
            furnitureInstancesById =
                new Dictionary<string, FurnitureInstance>(StringComparer.Ordinal);
            occupantByCell = new Dictionary<GridPosition, string>();

            UnlockedRegions = unlockedRegions.AsReadOnly();
            Reservations = reservations.AsReadOnly();
            FurnitureInstances = furnitureInstances.AsReadOnly();
        }

        public void AddRegion(LayoutRegion region)
        {
            if (region == null)
            {
                throw new ArgumentNullException(nameof(region));
            }

            if (regionsById.ContainsKey(region.Id))
            {
                throw new ArgumentException(
                    $"Duplicate Region ID '{region.Id}'.",
                    nameof(region));
            }

            regionsById.Add(region.Id, region);
            unlockedRegions.Add(region);
        }

        public void AddReservation(LayoutReservation reservation)
        {
            if (reservation == null)
            {
                throw new ArgumentNullException(nameof(reservation));
            }

            if (reservationsById.ContainsKey(reservation.Id))
            {
                throw new ArgumentException(
                    $"Duplicate Reservation ID '{reservation.Id}'.",
                    nameof(reservation));
            }

            if (ReservationIntersectsFurniture(reservation))
            {
                throw new ArgumentException(
                    $"Reservation ID '{reservation.Id}' overlaps placed furniture.",
                    nameof(reservation));
            }

            reservationsById.Add(reservation.Id, reservation);
            reservations.Add(reservation);
        }

        public PlacementResult PlaceFurniture(FurnitureInstance instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (!definitionCatalog.TryGet(
                instance.DefinitionId,
                out var definition))
            {
                throw new ArgumentException(
                    $"Unknown Furniture Definition ID '{instance.DefinitionId}'.",
                    nameof(instance));
            }

            if ((definition.AllowedPlacementSurfaces &
                PlacementSurfaceType.Floor) == 0)
            {
                return PlacementResult.Failure(
                    PlacementFailureReason.UnsupportedPlacementSurface);
            }

            if (furnitureInstancesById.ContainsKey(instance.InstanceId))
            {
                return PlacementResult.Failure(
                    PlacementFailureReason.InstanceAlreadyPlaced);
            }

            var validationResult = ValidateFurniturePlacement(
                instance.DefinitionId,
                instance.Position,
                instance.Rotation);
            if (!validationResult.Succeeded)
            {
                return validationResult;
            }

            var cells = GetFurnitureFootprintCells(
                instance.DefinitionId,
                instance.Position,
                instance.Rotation);

            furnitureInstancesById.Add(instance.InstanceId, instance);
            furnitureInstances.Add(instance);

            foreach (var cell in cells)
            {
                occupantByCell.Add(cell, instance.InstanceId);
            }

            return PlacementResult.Success();
        }

        public bool TryGetOccupant(
            GridPosition position,
            out string instanceId)
        {
            return occupantByCell.TryGetValue(position, out instanceId);
        }

        public bool TryGetFurnitureInstance(
            string instanceId,
            out FurnitureInstance instance)
        {
            ValidateInstanceId(instanceId);
            return furnitureInstancesById.TryGetValue(instanceId, out instance);
        }

        public PlacementResult MoveFurniture(
            string instanceId,
            GridPosition newPosition)
        {
            ValidateInstanceId(instanceId);

            if (!furnitureInstancesById.TryGetValue(
                instanceId,
                out var current))
            {
                return PlacementResult.Failure(
                    PlacementFailureReason.InstanceNotFound);
            }

            return UpdateFurniturePlacement(
                instanceId,
                newPosition,
                current.Rotation);
        }

        public PlacementResult RotateFurniture(
            string instanceId,
            FurnitureRotation newRotation)
        {
            ValidateInstanceId(instanceId);
            FurnitureInstance.ValidateRotation(newRotation);

            if (!furnitureInstancesById.TryGetValue(
                instanceId,
                out var current))
            {
                return PlacementResult.Failure(
                    PlacementFailureReason.InstanceNotFound);
            }

            return UpdateFurniturePlacement(
                instanceId,
                current.Position,
                newRotation);
        }

        public PlacementResult ValidateFurniturePlacement(
            string definitionId,
            GridPosition position,
            FurnitureRotation rotation,
            string ignoredInstanceId = null)
        {
            if (ignoredInstanceId != null)
            {
                if (!StableId.IsValidFurnitureInstanceId(ignoredInstanceId) ||
                    !furnitureInstancesById.ContainsKey(ignoredInstanceId))
                {
                    return PlacementResult.Failure(
                        PlacementFailureReason.InstanceNotFound);
                }
            }

            var definition = definitionCatalog.GetRequired(definitionId);
            if ((definition.AllowedPlacementSurfaces &
                PlacementSurfaceType.Floor) == 0)
            {
                return PlacementResult.Failure(
                    PlacementFailureReason.UnsupportedPlacementSurface);
            }

            FurnitureInstance.ValidateRotation(rotation);

            if (!TryGetFootprintCells(
                definition,
                position,
                rotation,
                out var cells))
            {
                return PlacementResult.Failure(
                    layoutBounds.HasValue
                        ? PlacementFailureReason.OutOfLayoutBounds
                        : PlacementFailureReason.OutOfUnlockedRegion);
            }

            return ValidateCandidateCells(cells, ignoredInstanceId);
        }

        public PlacementResult UpdateFurniturePlacement(
            string instanceId,
            GridPosition position,
            FurnitureRotation rotation)
        {
            ValidateInstanceId(instanceId);
            FurnitureInstance.ValidateRotation(rotation);

            if (!furnitureInstancesById.TryGetValue(instanceId, out var current))
            {
                return PlacementResult.Failure(
                    PlacementFailureReason.InstanceNotFound);
            }

            return ReplaceFurniturePlacement(current, position, rotation);
        }

        public IReadOnlyList<GridPosition> GetFurnitureFootprintCells(
            string definitionId,
            GridPosition position,
            FurnitureRotation rotation)
        {
            var definition = definitionCatalog.GetRequired(definitionId);
            FurnitureInstance.ValidateRotation(rotation);

            if (!TryGetFootprintCells(definition, position, rotation, out var cells))
            {
                return new List<GridPosition>().AsReadOnly();
            }

            return cells.AsReadOnly();
        }

        public PlacementResult RemoveFurniture(string instanceId)
        {
            ValidateInstanceId(instanceId);

            if (!furnitureInstancesById.TryGetValue(
                instanceId,
                out var instance))
            {
                return PlacementResult.Failure(
                    PlacementFailureReason.InstanceNotFound);
            }

            ReleaseCellsOwnedBy(instanceId);
            furnitureInstancesById.Remove(instanceId);
            furnitureInstances.Remove(instance);

            return PlacementResult.Success();
        }

        private PlacementResult ReplaceFurniturePlacement(
            FurnitureInstance current,
            GridPosition position,
            FurnitureRotation rotation)
        {
            var validationResult = ValidateFurniturePlacement(
                current.DefinitionId,
                position,
                rotation,
                current.InstanceId);
            if (!validationResult.Succeeded)
            {
                return validationResult;
            }

            var candidate = current.WithPlacement(position, rotation);
            var candidateCells = GetFurnitureFootprintCells(
                candidate.DefinitionId,
                candidate.Position,
                candidate.Rotation);

            var listIndex = furnitureInstances.FindIndex(
                instance => string.Equals(
                    instance.InstanceId,
                    current.InstanceId,
                    StringComparison.Ordinal));

            ReleaseCellsOwnedBy(current.InstanceId);
            furnitureInstances[listIndex] = candidate;
            furnitureInstancesById[current.InstanceId] = candidate;

            foreach (var cell in candidateCells)
            {
                occupantByCell.Add(cell, candidate.InstanceId);
            }

            return PlacementResult.Success();
        }

        private void ReleaseCellsOwnedBy(string instanceId)
        {
            var ownedCells = new List<GridPosition>();

            foreach (var occupant in occupantByCell)
            {
                if (string.Equals(
                    occupant.Value,
                    instanceId,
                    StringComparison.Ordinal))
                {
                    ownedCells.Add(occupant.Key);
                }
            }

            foreach (var cell in ownedCells)
            {
                occupantByCell.Remove(cell);
            }
        }

        private bool TryGetFootprintCells(
            FurnitureDefinition definition,
            GridPosition position,
            FurnitureRotation rotation,
            out List<GridPosition> cells)
        {
            var rotatedSize = definition.Footprint.Rotate(rotation);
            cells = new List<GridPosition>();

            for (var x = 0; x < rotatedSize.Width; x++)
            {
                for (var y = 0; y < rotatedSize.Height; y++)
                {
                    var cellX = (long)position.X + x;
                    var cellY = (long)position.Y + y;

                    if (cellX < int.MinValue || cellX > int.MaxValue ||
                        cellY < int.MinValue || cellY > int.MaxValue)
                    {
                        cells.Clear();
                        return false;
                    }

                    cells.Add(new GridPosition((int)cellX, (int)cellY));
                }
            }

            return true;
        }

        private bool IsCellUnlocked(GridPosition cell)
        {
            foreach (var region in unlockedRegions)
            {
                var right = (long)region.Origin.X + region.Size.Width;
                var top = (long)region.Origin.Y + region.Size.Height;

                if (cell.X >= region.Origin.X &&
                    cell.X < right &&
                    cell.Y >= region.Origin.Y &&
                    cell.Y < top)
                {
                    return true;
                }
            }

            return false;
        }

        private PlacementResult ValidateCandidateCells(
            IReadOnlyList<GridPosition> cells,
            string ignoredInstanceId = null)
        {
            foreach (var cell in cells)
            {
                if (layoutBounds.HasValue &&
                    !layoutBounds.Value.Contains(cell))
                {
                    return PlacementResult.Failure(
                        PlacementFailureReason.OutOfLayoutBounds);
                }
            }

            foreach (var cell in cells)
            {
                if (!IsCellUnlocked(cell))
                {
                    return PlacementResult.Failure(
                        GetLockedCellFailureReason());
                }
            }

            foreach (var cell in cells)
            {
                var reservationFailure = GetReservationFailureReason(cell);
                if (reservationFailure.HasValue)
                {
                    return PlacementResult.Failure(reservationFailure.Value);
                }
            }

            foreach (var cell in cells)
            {
                if (occupantByCell.TryGetValue(cell, out var occupantId) &&
                    !string.Equals(
                        occupantId,
                        ignoredInstanceId,
                        StringComparison.Ordinal))
                {
                    return PlacementResult.Failure(
                        PlacementFailureReason.Overlap);
                }
            }

            return PlacementResult.Success();
        }

        private PlacementFailureReason? GetReservationFailureReason(
            GridPosition cell)
        {
            foreach (var reservation in reservations)
            {
                if (reservation.Contains(cell))
                {
                    switch (reservation.Type)
                    {
                        case LayoutReservationType.EntranceClearance:
                            return PlacementFailureReason.ReservedEntranceClearance;
                        case LayoutReservationType.Blocked:
                            return PlacementFailureReason.Blocked;
                    }
                }
            }

            return null;
        }

        private PlacementFailureReason GetLockedCellFailureReason()
        {
            return layoutBounds.HasValue
                ? PlacementFailureReason.LockedCell
                : PlacementFailureReason.OutOfUnlockedRegion;
        }

        private bool ReservationIntersectsFurniture(LayoutReservation reservation)
        {
            foreach (var occupiedCell in occupantByCell.Keys)
            {
                if (reservation.Contains(occupiedCell))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateInstanceId(string instanceId)
        {
            if (instanceId == null)
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            if (!StableId.IsValidFurnitureInstanceId(instanceId))
            {
                throw new ArgumentException(
                    "Instance ID has an invalid format.",
                    nameof(instanceId));
            }
        }
    }
}

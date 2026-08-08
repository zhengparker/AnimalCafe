using System;
using System.Collections.Generic;

namespace AnimalCafe.Layout
{
    public enum WallPlacementFailureReason
    {
        None = 0,
        OutOfBounds = 1,
        Overlap = 2,
        SurfaceMismatch = 3,
        ItemAlreadyPlaced = 4,
        ItemNotFound = 5
    }

    public sealed class WallPlacementResult
    {
        public bool Succeeded { get; }
        public WallPlacementFailureReason FailureReason { get; }

        private WallPlacementResult(bool succeeded, WallPlacementFailureReason failureReason)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
        }

        public static WallPlacementResult Success()
        {
            return new WallPlacementResult(true, WallPlacementFailureReason.None);
        }

        public static WallPlacementResult Failure(WallPlacementFailureReason reason)
        {
            return new WallPlacementResult(false, reason);
        }
    }

    public sealed class WallSurfaceLayout
    {
        private readonly Dictionary<WallSlotPosition, string> occupantBySlot =
            new Dictionary<WallSlotPosition, string>();
        private readonly List<WallMountedInstance> mountedItems =
            new List<WallMountedInstance>();
        private readonly Dictionary<string, WallMountedInstance> mountedItemsById =
            new Dictionary<string, WallMountedInstance>(StringComparer.Ordinal);

        public string SurfaceId { get; }
        public int ColumnCount { get; }
        public int RowCount { get; }
        public IReadOnlyList<WallMountedInstance> MountedItems { get; }
        public int OccupiedSlotCount => occupantBySlot.Count;

        public WallSurfaceLayout(string surfaceId, int columnCount, int rowCount)
        {
            WallMountedInstance.ValidateId(surfaceId, nameof(surfaceId));

            if (columnCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(columnCount),
                    columnCount,
                    "Wall column count must be at least one.");
            }

            if (rowCount < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rowCount),
                    rowCount,
                    "Wall row count must be at least one.");
            }

            SurfaceId = surfaceId;
            ColumnCount = columnCount;
            RowCount = rowCount;
            MountedItems = mountedItems.AsReadOnly();
        }

        public WallPlacementResult TryPlace(WallMountedInstance item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (!string.Equals(item.SurfaceId, SurfaceId, StringComparison.Ordinal))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.SurfaceMismatch);
            }

            if (mountedItemsById.ContainsKey(item.InstanceId))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.ItemAlreadyPlaced);
            }

            if (!TryGetFootprintSlots(item, out var slots))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.OutOfBounds);
            }

            var validationResult = ValidateCandidateSlots(slots);
            if (!validationResult.Succeeded)
            {
                return validationResult;
            }

            mountedItemsById.Add(item.InstanceId, item);
            mountedItems.Add(item);
            OccupySlots(slots, item.InstanceId);

            return WallPlacementResult.Success();
        }

        public WallPlacementResult TryMove(
            string itemId,
            WallSlotPosition newPosition)
        {
            WallMountedInstance.ValidateId(itemId, nameof(itemId));

            if (!mountedItemsById.TryGetValue(itemId, out var current))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.ItemNotFound);
            }

            var candidate = current.WithPosition(newPosition);
            if (!TryGetFootprintSlots(candidate, out var slots))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.OutOfBounds);
            }

            var validationResult = ValidateCandidateSlots(slots, current.InstanceId);
            if (!validationResult.Succeeded)
            {
                return validationResult;
            }

            var listIndex = mountedItems.FindIndex(item => string.Equals(
                item.InstanceId,
                current.InstanceId,
                StringComparison.Ordinal));
            ReleaseSlotsOwnedBy(current.InstanceId);
            mountedItems[listIndex] = candidate;
            mountedItemsById[current.InstanceId] = candidate;
            OccupySlots(slots, candidate.InstanceId);

            return WallPlacementResult.Success();
        }

        public WallPlacementResult TryRemove(string itemId)
        {
            WallMountedInstance.ValidateId(itemId, nameof(itemId));

            if (!mountedItemsById.TryGetValue(itemId, out var item))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.ItemNotFound);
            }

            ReleaseSlotsOwnedBy(item.InstanceId);
            mountedItemsById.Remove(item.InstanceId);
            mountedItems.Remove(item);

            return WallPlacementResult.Success();
        }

        public bool TryGetOccupant(WallSlotPosition position, out string itemId)
        {
            return occupantBySlot.TryGetValue(position, out itemId);
        }

        private bool TryGetFootprintSlots(
            WallMountedInstance item,
            out List<WallSlotPosition> slots)
        {
            slots = new List<WallSlotPosition>();

            for (var column = 0; column < item.Footprint.Width; column++)
            {
                for (var row = 0; row < item.Footprint.Height; row++)
                {
                    var slotColumn = (long)item.Position.Column + column;
                    var slotRow = (long)item.Position.Row + row;

                    if (slotColumn < 0 || slotColumn >= ColumnCount ||
                        slotRow < 0 || slotRow >= RowCount)
                    {
                        slots.Clear();
                        return false;
                    }

                    slots.Add(new WallSlotPosition((int)slotColumn, (int)slotRow));
                }
            }

            return true;
        }

        private WallPlacementResult ValidateCandidateSlots(
            IReadOnlyList<WallSlotPosition> slots,
            string ignoredItemId = null)
        {
            foreach (var slot in slots)
            {
                if (occupantBySlot.TryGetValue(slot, out var occupantId) &&
                    !string.Equals(occupantId, ignoredItemId, StringComparison.Ordinal))
                {
                    return WallPlacementResult.Failure(
                        WallPlacementFailureReason.Overlap);
                }
            }

            return WallPlacementResult.Success();
        }

        private void OccupySlots(
            IReadOnlyList<WallSlotPosition> slots,
            string itemId)
        {
            foreach (var slot in slots)
            {
                occupantBySlot.Add(slot, itemId);
            }
        }

        private void ReleaseSlotsOwnedBy(string itemId)
        {
            var ownedSlots = new List<WallSlotPosition>();

            foreach (var occupant in occupantBySlot)
            {
                if (string.Equals(occupant.Value, itemId, StringComparison.Ordinal))
                {
                    ownedSlots.Add(occupant.Key);
                }
            }

            foreach (var slot in ownedSlots)
            {
                occupantBySlot.Remove(slot);
            }
        }
    }
}

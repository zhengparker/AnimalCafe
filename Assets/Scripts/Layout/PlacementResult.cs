using System;

namespace AnimalCafe.Layout
{
    public enum PlacementFailureReason
    {
        None = 0,
        OutOfUnlockedRegion = 1,
        Overlap = 2,
        InstanceNotFound = 3,
        InstanceAlreadyPlaced = 4,
        UnsupportedPlacementSurface = 5,
        ReservedEntranceClearance = 6,
        OutOfLayoutBounds = 7,
        LockedCell = 8,
        Blocked = 9
    }

    public sealed class PlacementResult
    {
        public bool Succeeded { get; }
        public PlacementFailureReason FailureReason { get; }

        private PlacementResult(
            bool succeeded,
            PlacementFailureReason failureReason)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
        }

        public static PlacementResult Success()
        {
            return new PlacementResult(
                true,
                PlacementFailureReason.None);
        }

        public static PlacementResult Failure(
            PlacementFailureReason reason)
        {
            if (reason == PlacementFailureReason.None ||
                !Enum.IsDefined(
                    typeof(PlacementFailureReason),
                    reason))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(reason),
                    reason,
                    "Failure reason must be a known non-None value.");
            }

            return new PlacementResult(false, reason);
        }
    }
}

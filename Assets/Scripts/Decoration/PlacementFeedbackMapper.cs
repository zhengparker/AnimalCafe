using System;
using AnimalCafe.Layout;

namespace AnimalCafe.Decoration
{
    public enum PlacementFeedbackKey
    {
        None,
        Occupied,
        OutsideUnlockedArea,
        Locked,
        Blocked,
        EntranceClearance,
        UnsupportedSurface,
        MissingInstance
    }

    public static class PlacementFeedbackMapper
    {
        public static PlacementFeedbackKey Map(PlacementResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            switch (result.FailureReason)
            {
                case PlacementFailureReason.None:
                    return PlacementFeedbackKey.None;
                case PlacementFailureReason.Overlap:
                    return PlacementFeedbackKey.Occupied;
                case PlacementFailureReason.OutOfUnlockedRegion:
                case PlacementFailureReason.OutOfLayoutBounds:
                    return PlacementFeedbackKey.OutsideUnlockedArea;
                case PlacementFailureReason.LockedCell:
                    return PlacementFeedbackKey.Locked;
                case PlacementFailureReason.Blocked:
                    return PlacementFeedbackKey.Blocked;
                case PlacementFailureReason.ReservedEntranceClearance:
                    return PlacementFeedbackKey.EntranceClearance;
                case PlacementFailureReason.UnsupportedPlacementSurface:
                    return PlacementFeedbackKey.UnsupportedSurface;
                case PlacementFailureReason.InstanceNotFound:
                case PlacementFailureReason.InstanceAlreadyPlaced:
                    return PlacementFeedbackKey.MissingInstance;
                default:
                    return PlacementFeedbackKey.None;
            }
        }
    }
}

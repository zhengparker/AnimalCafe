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
        MissingInstance,
        WallOverlap,
        WallOutOfBounds,
        WallCrossCorner,
        WallSurfaceMissing,
        SelectWallTarget,
        SelectFloorGridTarget
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

        public static PlacementFeedbackKey Map(WallPlacementResult result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            switch (result.FailureReason)
            {
                case WallPlacementFailureReason.None:
                    return PlacementFeedbackKey.None;
                case WallPlacementFailureReason.Overlap:
                    return PlacementFeedbackKey.WallOverlap;
                case WallPlacementFailureReason.OutOfBounds:
                    return PlacementFeedbackKey.WallOutOfBounds;
                case WallPlacementFailureReason.CrossCorner:
                    return PlacementFeedbackKey.WallCrossCorner;
                case WallPlacementFailureReason.SurfaceMismatch:
                case WallPlacementFailureReason.SurfaceMissing:
                    return PlacementFeedbackKey.WallSurfaceMissing;
                default:
                    return PlacementFeedbackKey.None;
            }
        }
    }
}

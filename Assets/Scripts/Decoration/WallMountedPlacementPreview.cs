using AnimalCafe.Layout;

namespace AnimalCafe.Decoration
{
    public sealed class WallMountedPlacementPreview
    {
        public string DefinitionId { get; }
        public string InstanceId { get; }
        public string SurfaceId { get; }
        public WallSlotPosition Position { get; }
        public WallFootprint Footprint { get; }
        public bool IsExisting => InstanceId != null;
        public bool IsValid { get; }
        public WallPlacementFailureReason FailureReason { get; }
        public bool CanConfirm => IsValid && !IsStoreConfirmationPending;
        public bool IsStoreConfirmationPending { get; }

        internal WallMountedPlacementPreview(
            string definitionId,
            string instanceId,
            string surfaceId,
            WallSlotPosition position,
            WallFootprint footprint,
            WallPlacementResult placementResult,
            bool isStoreConfirmationPending)
        {
            DefinitionId = definitionId;
            InstanceId = instanceId;
            SurfaceId = surfaceId;
            Position = position;
            Footprint = footprint;
            IsValid = placementResult.Succeeded;
            FailureReason = placementResult.FailureReason;
            IsStoreConfirmationPending = isStoreConfirmationPending;
        }

        internal WallMountedPlacementPreview WithPlacement(
            string surfaceId,
            WallSlotPosition position,
            WallPlacementResult result)
        {
            return new WallMountedPlacementPreview(
                DefinitionId,
                InstanceId,
                surfaceId,
                position,
                Footprint,
                result,
                false);
        }

        internal WallMountedPlacementPreview WithResult(
            WallPlacementResult result)
        {
            return new WallMountedPlacementPreview(
                DefinitionId,
                InstanceId,
                SurfaceId,
                Position,
                Footprint,
                result,
                IsStoreConfirmationPending);
        }

        internal WallMountedPlacementPreview WithStoreConfirmation(
            bool pending)
        {
            var result = IsValid
                ? WallPlacementResult.Success()
                : WallPlacementResult.Failure(FailureReason);
            return new WallMountedPlacementPreview(
                DefinitionId,
                InstanceId,
                SurfaceId,
                Position,
                Footprint,
                result,
                pending);
        }
    }
}

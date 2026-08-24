using AnimalCafe.Layout;

namespace AnimalCafe.Decoration
{
    public sealed class FurniturePlacementPreview
    {
        public string DefinitionId { get; }
        public string SourceInstanceId { get; }
        public GridPosition OriginalPosition { get; }
        public FurnitureRotation OriginalRotation { get; }
        public GridPosition ProposedPosition { get; }
        public FurnitureRotation ProposedRotation { get; }
        public PlacementResult PlacementResult { get; }
        public bool IsNew => SourceInstanceId == null;

        internal FurniturePlacementPreview(
            string definitionId,
            string sourceInstanceId,
            GridPosition originalPosition,
            FurnitureRotation originalRotation,
            GridPosition proposedPosition,
            FurnitureRotation proposedRotation,
            PlacementResult placementResult)
        {
            DefinitionId = definitionId;
            SourceInstanceId = sourceInstanceId;
            OriginalPosition = originalPosition;
            OriginalRotation = originalRotation;
            ProposedPosition = proposedPosition;
            ProposedRotation = proposedRotation;
            PlacementResult = placementResult;
        }

        internal FurniturePlacementPreview WithProposedPlacement(
            GridPosition position,
            FurnitureRotation rotation,
            PlacementResult placementResult)
        {
            return new FurniturePlacementPreview(
                DefinitionId,
                SourceInstanceId,
                OriginalPosition,
                OriginalRotation,
                position,
                rotation,
                placementResult);
        }
    }
}

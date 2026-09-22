using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
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
        public IReadOnlyList<string> StoreBlockerContentIds { get; }
        public bool IsNew => SourceInstanceId == null;

        internal FurniturePlacementPreview(
            string definitionId,
            string sourceInstanceId,
            GridPosition originalPosition,
            FurnitureRotation originalRotation,
            GridPosition proposedPosition,
            FurnitureRotation proposedRotation,
            PlacementResult placementResult,
            IEnumerable<string> storeBlockerContentIds = null)
        {
            DefinitionId = definitionId;
            SourceInstanceId = sourceInstanceId;
            OriginalPosition = originalPosition;
            OriginalRotation = originalRotation;
            ProposedPosition = proposedPosition;
            ProposedRotation = proposedRotation;
            PlacementResult = placementResult;
            StoreBlockerContentIds = new ReadOnlyCollection<string>(
                (storeBlockerContentIds ?? Array.Empty<string>()).ToList());
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
                placementResult,
                StoreBlockerContentIds);
        }

        internal FurniturePlacementPreview WithStoreBlockerContentIds(
            IEnumerable<string> contentIds)
        {
            return new FurniturePlacementPreview(
                DefinitionId,
                SourceInstanceId,
                OriginalPosition,
                OriginalRotation,
                ProposedPosition,
                ProposedRotation,
                PlacementResult,
                contentIds);
        }
    }
}

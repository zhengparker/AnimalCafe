using System;
using AnimalCafe.Layout;

namespace AnimalCafe.Decoration
{
    public sealed class DecorationSession
    {
        private readonly CafeLayout layout;

        public DecorationSessionState State { get; private set; }
        public FurniturePlacementPreview ActivePreview { get; private set; }

        public DecorationSession(CafeLayout layout)
        {
            this.layout = layout ?? throw new ArgumentNullException(nameof(layout));
            State = DecorationSessionState.Closed;
        }

        public void Enter()
        {
            if (State == DecorationSessionState.Closed)
            {
                State = DecorationSessionState.BrowsingCatalogue;
            }
        }

        public void Exit()
        {
            CancelPreview();
            State = DecorationSessionState.Closed;
        }

        public void BeginNew(string definitionId, GridPosition position)
        {
            CancelPreview();

            var rotation = FurnitureRotation.Degrees0;
            var result = layout.ValidateFurniturePlacement(
                definitionId,
                position,
                rotation);
            ActivePreview = new FurniturePlacementPreview(
                definitionId,
                null,
                position,
                rotation,
                position,
                rotation,
                result);
            State = DecorationSessionState.PreviewingNewFurniture;
        }

        public PlacementResult BeginExisting(string instanceId)
        {
            CancelPreview();

            if (!layout.TryGetFurnitureInstance(instanceId, out var instance))
            {
                State = DecorationSessionState.BrowsingCatalogue;
                return PlacementResult.Failure(PlacementFailureReason.InstanceNotFound);
            }

            var result = layout.ValidateFurniturePlacement(
                instance.DefinitionId,
                instance.Position,
                instance.Rotation,
                instance.InstanceId);
            ActivePreview = new FurniturePlacementPreview(
                instance.DefinitionId,
                instance.InstanceId,
                instance.Position,
                instance.Rotation,
                instance.Position,
                instance.Rotation,
                result);
            State = DecorationSessionState.EditingExistingFurniture;
            return result;
        }

        public PlacementResult MovePreview(GridPosition position)
        {
            if (ActivePreview == null ||
                State == DecorationSessionState.ConfirmingStore)
            {
                return PlacementResult.Success();
            }

            var result = layout.ValidateFurniturePlacement(
                ActivePreview.DefinitionId,
                position,
                ActivePreview.ProposedRotation,
                ActivePreview.SourceInstanceId);
            ActivePreview = ActivePreview.WithProposedPlacement(
                position,
                ActivePreview.ProposedRotation,
                result);
            return result;
        }

        public PlacementResult RotatePreview()
        {
            if (ActivePreview == null ||
                State == DecorationSessionState.ConfirmingStore)
            {
                return PlacementResult.Success();
            }

            var rotation = NextRotation(ActivePreview.ProposedRotation);
            var oldSize = GetRotatedFootprintSize(
                ActivePreview.DefinitionId,
                ActivePreview.ProposedRotation);
            var newSize = GetRotatedFootprintSize(
                ActivePreview.DefinitionId,
                rotation);
            var position = new GridPosition(
                checked(ActivePreview.ProposedPosition.X
                    + (oldSize.Width - newSize.Width) / 2),
                checked(ActivePreview.ProposedPosition.Y
                    + (oldSize.Height - newSize.Height) / 2));
            var result = layout.ValidateFurniturePlacement(
                ActivePreview.DefinitionId,
                position,
                rotation,
                ActivePreview.SourceInstanceId);
            ActivePreview = ActivePreview.WithProposedPlacement(
                position,
                rotation,
                result);
            return result;
        }

        public PlacementResult ConfirmPreview()
        {
            if (ActivePreview == null ||
                State == DecorationSessionState.ConfirmingStore)
            {
                return PlacementResult.Success();
            }

            var result = ActivePreview.IsNew
                ? layout.PlaceFurniture(FurnitureInstance.CreateNew(
                    ActivePreview.DefinitionId,
                    ActivePreview.ProposedPosition,
                    ActivePreview.ProposedRotation))
                : layout.UpdateFurniturePlacement(
                    ActivePreview.SourceInstanceId,
                    ActivePreview.ProposedPosition,
                    ActivePreview.ProposedRotation);

            if (!result.Succeeded)
            {
                ActivePreview = ActivePreview.WithProposedPlacement(
                    ActivePreview.ProposedPosition,
                    ActivePreview.ProposedRotation,
                    result);
                return result;
            }

            ActivePreview = null;
            State = DecorationSessionState.BrowsingCatalogue;
            return result;
        }

        public void CancelPreview()
        {
            if (ActivePreview != null)
            {
                ActivePreview = null;
            }

            if (State != DecorationSessionState.Closed)
            {
                State = DecorationSessionState.BrowsingCatalogue;
            }
        }

        public bool BeginStoreConfirmation()
        {
            if (State != DecorationSessionState.EditingExistingFurniture ||
                ActivePreview == null ||
                ActivePreview.IsNew)
            {
                return false;
            }

            State = DecorationSessionState.ConfirmingStore;
            return true;
        }

        public void DismissStoreConfirmation()
        {
            if (State == DecorationSessionState.ConfirmingStore &&
                ActivePreview != null &&
                !ActivePreview.IsNew)
            {
                State = DecorationSessionState.EditingExistingFurniture;
            }
        }

        public PlacementResult ConfirmStore()
        {
            if (State != DecorationSessionState.ConfirmingStore ||
                ActivePreview == null ||
                ActivePreview.IsNew)
            {
                return PlacementResult.Success();
            }

            var result = layout.RemoveFurniture(ActivePreview.SourceInstanceId);
            if (!result.Succeeded)
            {
                ActivePreview = ActivePreview.WithProposedPlacement(
                    ActivePreview.ProposedPosition,
                    ActivePreview.ProposedRotation,
                    result);
                return result;
            }

            ActivePreview = null;
            State = DecorationSessionState.BrowsingCatalogue;
            return result;
        }

        private static FurnitureRotation NextRotation(FurnitureRotation rotation)
        {
            switch (rotation)
            {
                case FurnitureRotation.Degrees0:
                    return FurnitureRotation.Degrees90;
                case FurnitureRotation.Degrees90:
                    return FurnitureRotation.Degrees180;
                case FurnitureRotation.Degrees180:
                    return FurnitureRotation.Degrees270;
                case FurnitureRotation.Degrees270:
                    return FurnitureRotation.Degrees0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(rotation), rotation, "Rotation must be a known value.");
            }
        }

        private GridSize GetRotatedFootprintSize(
            string definitionId,
            FurnitureRotation rotation)
        {
            var cells = layout.GetFurnitureFootprintCells(
                definitionId,
                new GridPosition(0, 0),
                rotation);
            if (cells.Count == 0)
            {
                throw new InvalidOperationException(
                    $"Furniture Definition '{definitionId}' produced an empty footprint.");
            }

            var maxX = 0;
            var maxY = 0;
            for (var index = 0; index < cells.Count; index++)
            {
                maxX = Math.Max(maxX, cells[index].X);
                maxY = Math.Max(maxY, cells[index].Y);
            }

            return new GridSize(maxX + 1, maxY + 1);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Layout;

namespace AnimalCafe.Decoration
{
    public sealed class WallMountedDecorationSession
    {
        private readonly WallMountedLayout confirmedLayout;
        private readonly Dictionary<string, DefinitionBinding> definitionsById;

        public WallMountedPlacementPreview ActivePreview { get; private set; }

        public WallMountedDecorationSession(
            WallMountedLayout confirmedLayout,
            IEnumerable<WallMountedDefinitionAsset> definitions)
        {
            this.confirmedLayout = confirmedLayout ??
                throw new ArgumentNullException(nameof(confirmedLayout));
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            definitionsById = new Dictionary<string, DefinitionBinding>(
                StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                ValidateDefinition(definition, nameof(definitions));
                if (definitionsById.ContainsKey(definition.DefinitionId))
                {
                    throw new ArgumentException(
                        "Wall-mounted Definition IDs must be unique.",
                        nameof(definitions));
                }

                definitionsById.Add(
                    definition.DefinitionId,
                    new DefinitionBinding(
                        definition.DefinitionId,
                        new WallFootprint(
                            definition.FootprintWidth,
                            definition.FootprintHeight)));
            }
        }

        public void BeginNew(
            string definitionId,
            string preferredSurfaceId,
            WallSlotPosition preferredPosition)
        {
            EnsureNoActivePreview();
            if (definitionId == null ||
                !definitionsById.TryGetValue(definitionId, out var definition))
            {
                throw new ArgumentException(
                    "The wall-mounted Definition is not bound to this session.",
                    nameof(definitionId));
            }

            if (TryFindNearestValidPlacement(
                definition,
                preferredSurfaceId,
                preferredPosition,
                out var surfaceId,
                out var position,
                out var result))
            {
                ActivePreview = CreatePreview(
                    definition,
                    null,
                    surfaceId,
                    position,
                    result);
                return;
            }

            result = ValidatePreviewPlacement(
                definition,
                preferredSurfaceId,
                preferredPosition,
                null);
            ActivePreview = CreatePreview(
                definition,
                null,
                preferredSurfaceId,
                preferredPosition,
                result);
        }

        public WallPlacementResult BeginExisting(string instanceId)
        {
            EnsureNoActivePreview();
            if (!TryGetInstance(instanceId, out var instance))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.ItemNotFound);
            }

            if (!definitionsById.TryGetValue(
                instance.DefinitionId,
                out var definition))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.ItemNotFound);
            }

            var result = confirmedLayout.ValidatePlacement(
                definition.Id,
                instance.SurfaceId,
                instance.Position,
                definition.Footprint,
                instance.InstanceId);
            ActivePreview = CreatePreview(
                definition,
                instance.InstanceId,
                instance.SurfaceId,
                instance.Position,
                result);
            return result;
        }

        public WallPlacementResult MovePreview(
            string surfaceId,
            WallSlotPosition position)
        {
            if (ActivePreview == null)
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.ItemNotFound);
            }

            if (ActivePreview.IsStoreConfirmationPending)
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.ConfirmationPending);
            }

            var definition = definitionsById[ActivePreview.DefinitionId];
            var result = ValidatePreviewPlacement(
                definition,
                surfaceId,
                position,
                ActivePreview.InstanceId);
            ActivePreview = ActivePreview.WithPlacement(
                surfaceId,
                position,
                result);
            return result;
        }

        public WallPlacementResult ConfirmPreview()
        {
            if (ActivePreview == null)
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.ItemNotFound);
            }

            if (ActivePreview.IsStoreConfirmationPending)
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.ConfirmationPending);
            }

            if (!ActivePreview.CanConfirm)
            {
                return ResultFromPreview(ActivePreview);
            }

            var result = ActivePreview.IsExisting
                ? confirmedLayout.Move(
                    ActivePreview.InstanceId,
                    ActivePreview.SurfaceId,
                    ActivePreview.Position)
                : confirmedLayout.Place(new WallMountedInstance(
                    CreateInstanceId(),
                    ActivePreview.DefinitionId,
                    ActivePreview.SurfaceId,
                    ActivePreview.Position,
                    ActivePreview.Footprint));

            if (!result.Succeeded)
            {
                ActivePreview = ActivePreview.WithResult(result);
                return result;
            }

            ActivePreview = null;
            return result;
        }

        public void CancelPreview()
        {
            ActivePreview = null;
        }

        public bool BeginStoreConfirmation()
        {
            if (ActivePreview == null ||
                !ActivePreview.IsExisting ||
                ActivePreview.IsStoreConfirmationPending)
            {
                return false;
            }

            ActivePreview = ActivePreview.WithStoreConfirmation(true);
            return true;
        }

        public void DismissStoreConfirmation()
        {
            if (ActivePreview != null &&
                ActivePreview.IsExisting &&
                ActivePreview.IsStoreConfirmationPending)
            {
                ActivePreview = ActivePreview.WithStoreConfirmation(false);
            }
        }

        public WallPlacementResult ConfirmStore()
        {
            if (ActivePreview == null ||
                !ActivePreview.IsExisting ||
                !ActivePreview.IsStoreConfirmationPending)
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.ItemNotFound);
            }

            var result = confirmedLayout.Remove(ActivePreview.InstanceId);
            if (!result.Succeeded)
            {
                ActivePreview = ActivePreview.WithResult(result);
                return result;
            }

            ActivePreview = null;
            return result;
        }

        private bool TryFindNearestValidPlacement(
            DefinitionBinding definition,
            string preferredSurfaceId,
            WallSlotPosition preferredPosition,
            out string bestSurfaceId,
            out WallSlotPosition bestPosition,
            out WallPlacementResult bestResult)
        {
            bestSurfaceId = null;
            bestPosition = preferredPosition;
            bestResult = null;
            var found = false;
            var bestSurfacePenalty = int.MaxValue;
            var bestDistance = long.MaxValue;

            foreach (var surface in confirmedLayout.Surfaces
                .OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                for (var column = 0; column < surface.Value.ColumnCount; column++)
                {
                    for (var row = 0; row < surface.Value.RowCount; row++)
                    {
                        var candidate = new WallSlotPosition(column, row);
                        var result = confirmedLayout.ValidatePlacement(
                            definition.Id,
                            surface.Key,
                            candidate,
                            definition.Footprint);
                        if (!result.Succeeded)
                        {
                            continue;
                        }

                        var distance = Math.Abs(
                                (long)column - preferredPosition.Column)
                            + Math.Abs((long)row - preferredPosition.Row);
                        var surfacePenalty = string.Equals(
                            surface.Key,
                            preferredSurfaceId,
                            StringComparison.Ordinal) ? 0 : 1;
                        if (!found ||
                            surfacePenalty < bestSurfacePenalty ||
                            (surfacePenalty == bestSurfacePenalty && distance < bestDistance) ||
                            (surfacePenalty == bestSurfacePenalty && distance == bestDistance && IsEarlierCandidate(
                                surface.Key,
                                candidate,
                                bestSurfaceId,
                                bestPosition)))
                        {
                            found = true;
                            bestSurfacePenalty = surfacePenalty;
                            bestDistance = distance;
                            bestSurfaceId = surface.Key;
                            bestPosition = candidate;
                            bestResult = result;
                        }
                    }
                }
            }

            return found;
        }

        private WallPlacementResult ValidatePreviewPlacement(
            DefinitionBinding definition,
            string surfaceId,
            WallSlotPosition position,
            string ignoredInstanceId)
        {
            if (string.IsNullOrEmpty(surfaceId))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.CrossCorner);
            }

            if (!confirmedLayout.Surfaces.ContainsKey(surfaceId))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.SurfaceMissing);
            }

            return confirmedLayout.ValidatePlacement(
                definition.Id,
                surfaceId,
                position,
                definition.Footprint,
                ignoredInstanceId);
        }

        private static bool IsEarlierCandidate(
            string candidateSurfaceId,
            WallSlotPosition candidatePosition,
            string currentSurfaceId,
            WallSlotPosition currentPosition)
        {
            var surfaceComparison = string.CompareOrdinal(
                candidateSurfaceId,
                currentSurfaceId);
            if (surfaceComparison != 0)
            {
                return surfaceComparison < 0;
            }

            return candidatePosition.Column < currentPosition.Column ||
                (candidatePosition.Column == currentPosition.Column &&
                    candidatePosition.Row < currentPosition.Row);
        }

        private static WallMountedPlacementPreview CreatePreview(
            DefinitionBinding definition,
            string instanceId,
            string surfaceId,
            WallSlotPosition position,
            WallPlacementResult result)
        {
            return new WallMountedPlacementPreview(
                definition.Id,
                instanceId,
                surfaceId,
                position,
                definition.Footprint,
                result,
                false);
        }

        private bool TryGetInstance(
            string instanceId,
            out WallMountedInstance instance)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                instance = null;
                return false;
            }

            try
            {
                return confirmedLayout.TryGetInstance(instanceId, out instance);
            }
            catch (ArgumentException)
            {
                instance = null;
                return false;
            }
        }

        private void EnsureNoActivePreview()
        {
            if (ActivePreview != null)
            {
                throw new InvalidOperationException(
                    "The active wall-mounted Preview must finish first.");
            }
        }

        private static WallPlacementResult ResultFromPreview(
            WallMountedPlacementPreview preview)
        {
            return preview.IsValid
                ? WallPlacementResult.Success()
                : WallPlacementResult.Failure(preview.FailureReason);
        }

        private static string CreateInstanceId()
        {
            return "wall-mounted." + Guid.NewGuid().ToString("N");
        }

        private static void ValidateDefinition(
            WallMountedDefinitionAsset definition,
            string paramName)
        {
            if (definition == null)
            {
                throw new ArgumentException(
                    "Wall-mounted Definitions cannot contain null entries.",
                    paramName);
            }

            try
            {
                WallMountedInstance.ValidateId(definition.DefinitionId, paramName);
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException(
                    "Wall-mounted Definition ID has an invalid format.",
                    paramName,
                    exception);
            }

            if (string.IsNullOrWhiteSpace(definition.DisplayName) ||
                definition.FootprintWidth < 1 ||
                definition.FootprintHeight < 1 ||
                definition.Prefab == null ||
                definition.Thumbnail == null ||
                float.IsNaN(definition.MaxVisualDepth) ||
                float.IsInfinity(definition.MaxVisualDepth) ||
                definition.MaxVisualDepth < 0f ||
                definition.MaxVisualDepth >
                    WallMountedDefinitionAsset.MaximumVisualDepth)
            {
                throw new ArgumentException(
                    "Wall-mounted Definition binding is incomplete or invalid.",
                    paramName);
            }
        }

        private readonly struct DefinitionBinding
        {
            public string Id { get; }
            public WallFootprint Footprint { get; }

            public DefinitionBinding(string id, WallFootprint footprint)
            {
                Id = id;
                Footprint = footprint;
            }
        }
    }
}

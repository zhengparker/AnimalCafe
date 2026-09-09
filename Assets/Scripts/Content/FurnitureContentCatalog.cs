using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Content
{
    [CreateAssetMenu(menuName = "AnimalCafe/Content/Furniture Catalog")]
    public sealed class FurnitureContentCatalog : ScriptableObject
    {
        [SerializeField] private List<FurnitureDefinitionAsset> entries =
            new List<FurnitureDefinitionAsset>();

        private Dictionary<string, GameObject> prefabsById =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);

        public FurnitureDefinitionCatalog BuildRuntimeCatalog()
        {
            var runtimeDefinitions = new List<FurnitureDefinition>();
            var prefabSnapshot = new Dictionary<string, GameObject>(StringComparer.Ordinal);

            foreach (var entry in EnumerateValidatedEntries())
            {
                runtimeDefinitions.Add(entry.Definition);
                prefabSnapshot.Add(entry.Definition.Id, entry.Asset.Prefab);
            }

            var runtimeCatalog = new FurnitureDefinitionCatalog(runtimeDefinitions);

            // Publish only after every entry has converted and validated successfully.
            prefabsById = prefabSnapshot;
            return runtimeCatalog;
        }

        public SurfaceSlotCatalog BuildSurfaceSlotCatalog(GridSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var definitions = new List<SurfaceSlotDefinition>();

            foreach (var entry in EnumerateValidatedEntries())
            {
                var markers = entry.Asset.Prefab
                    .GetComponentsInChildren<SurfaceSlotMarker>(true)
                    .OrderBy(marker => marker.SlotId, StringComparer.Ordinal);
                foreach (var marker in markers)
                {
                    var localCell = ResolveLocalCell(
                        entry.Definition,
                        entry.Asset.Prefab.transform,
                        marker,
                        settings.CellSize);
                    definitions.Add(new SurfaceSlotDefinition(
                        entry.Definition.Id,
                        marker.SlotId,
                        localCell));
                }
            }

            return new SurfaceSlotCatalog(definitions);
        }

        public FunctionalDirectionCatalog BuildFunctionalDirectionCatalog()
        {
            var cashRegisters = new Dictionary<string, CashRegisterSides>(StringComparer.Ordinal);
            var coffeeMachines = new Dictionary<string, CardinalDirection>(StringComparer.Ordinal);

            foreach (var entry in EnumerateValidatedEntries())
            {
                switch (entry.Definition.FunctionType)
                {
                    case FurnitureFunctionType.None:
                        break;
                    case FurnitureFunctionType.CashRegister:
                        cashRegisters.Add(
                            entry.Definition.Id,
                            CashRegisterSideMarker.ReadSidesFrom(entry.Asset.Prefab));
                        break;
                    case FurnitureFunctionType.CoffeeMachine:
                        coffeeMachines.Add(
                            entry.Definition.Id,
                            ReadCoffeeMachineDirection(entry.Definition.Id, entry.Asset.Prefab));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(entry.Definition.FunctionType),
                            entry.Definition.FunctionType,
                            "Furniture function type must be defined.");
                }
            }

            return new FunctionalDirectionCatalog(cashRegisters, coffeeMachines);
        }

        public bool TryGetPrefab(string definitionId, out GameObject prefab)
        {
            FurnitureDefinition.ValidateDefinitionId(definitionId, nameof(definitionId));
            return prefabsById.TryGetValue(definitionId, out prefab);
        }

        public bool TryGetDefinitionAsset(
            string definitionId,
            out FurnitureDefinitionAsset definitionAsset)
        {
            FurnitureDefinition.ValidateDefinitionId(definitionId, nameof(definitionId));

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null && string.Equals(
                    entry.DefinitionId,
                    definitionId,
                    StringComparison.Ordinal))
                {
                    definitionAsset = entry;
                    return true;
                }
            }

            definitionAsset = null;
            return false;
        }

        private static GridPosition ResolveLocalCell(
            FurnitureDefinition definition,
            Transform prefabRoot,
            SurfaceSlotMarker marker,
            float cellSize)
        {
            if (marker == null)
            {
                throw new ArgumentException(
                    $"Support definition '{definition.Id}' contains a null Surface Slot marker.");
            }

            try
            {
                LayoutStableId.Validate(marker.SlotId, nameof(marker.SlotId));
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException(
                    $"Surface Slot '{FormatSlotId(marker.SlotId)}' on support definition " +
                    $"'{definition.Id}' has an invalid Slot ID.",
                    exception);
            }

            var localPosition = prefabRoot.InverseTransformPoint(marker.transform.position);
            var localX = ResolveCellCoordinate(
                localPosition.x,
                definition.Footprint.Width,
                cellSize,
                definition.Id,
                marker.SlotId,
                "X");
            var localY = ResolveCellCoordinate(
                localPosition.z,
                definition.Footprint.Height,
                cellSize,
                definition.Id,
                marker.SlotId,
                "Z");
            return new GridPosition(localX, localY);
        }

        private static int ResolveCellCoordinate(
            float coordinate,
            int cellCount,
            float cellSize,
            string definitionId,
            string slotId,
            string axis)
        {
            const float Tolerance = 0.0001f;
            var centeredCoordinate = coordinate / cellSize + (cellCount - 1) * 0.5f;
            var rounded = Mathf.Round(centeredCoordinate);
            if (float.IsNaN(coordinate) ||
                float.IsInfinity(coordinate) ||
                !Mathf.Approximately(centeredCoordinate, rounded) ||
                Mathf.Abs(centeredCoordinate - rounded) > Tolerance ||
                rounded < 0f ||
                rounded >= cellCount)
            {
                throw new ArgumentException(
                    $"Surface Slot '{slotId}' on support definition '{definitionId}' " +
                    $"does not resolve to one local footprint cell on {axis}.");
            }

            return (int)rounded;
        }

        private static string FormatSlotId(string slotId)
        {
            if (slotId == null)
            {
                return "<null>";
            }

            return string.IsNullOrWhiteSpace(slotId) ? "<blank>" : slotId;
        }

        private IEnumerable<ValidatedEntry> EnumerateValidatedEntries()
        {
            var knownDefinitionIds = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < entries.Count; index++)
            {
                var asset = entries[index];
                if (asset == null)
                {
                    throw new ArgumentException(
                        $"Furniture content entry at index {index} must not be null.",
                        nameof(entries));
                }

                var definition = asset.ToRuntimeDefinition();
                if (asset.Prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Furniture content entry '{definition.Id}' must reference a Prefab.");
                }

                if (!knownDefinitionIds.Add(definition.Id))
                {
                    throw new ArgumentException(
                        $"Duplicate Furniture Definition ID '{definition.Id}'.",
                        nameof(entries));
                }

                yield return new ValidatedEntry(asset, definition);
            }
        }

        private static CardinalDirection ReadCoffeeMachineDirection(
            string definitionId,
            GameObject prefab)
        {
            var markers = prefab.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform != prefab.transform && transform.name == "ForwardMarker")
                .ToArray();
            if (markers.Length != 1)
            {
                throw new ArgumentException(
                    $"Coffee Machine definition '{definitionId}' must contain exactly one ForwardMarker.",
                    nameof(prefab));
            }

            var localForward = prefab.transform.InverseTransformDirection(markers[0].forward).normalized;
            const float CardinalTolerance = 0.999f;
            if (Vector3.Dot(localForward, Vector3.forward) >= CardinalTolerance)
            {
                return CardinalDirection.North;
            }

            if (Vector3.Dot(localForward, Vector3.right) >= CardinalTolerance)
            {
                return CardinalDirection.East;
            }

            if (Vector3.Dot(localForward, Vector3.back) >= CardinalTolerance)
            {
                return CardinalDirection.South;
            }

            if (Vector3.Dot(localForward, Vector3.left) >= CardinalTolerance)
            {
                return CardinalDirection.West;
            }

            throw new ArgumentException(
                $"Coffee Machine definition '{definitionId}' ForwardMarker must face one local cardinal direction.",
                nameof(prefab));
        }

        private readonly struct ValidatedEntry
        {
            public FurnitureDefinitionAsset Asset { get; }
            public FurnitureDefinition Definition { get; }

            public ValidatedEntry(
                FurnitureDefinitionAsset asset,
                FurnitureDefinition definition)
            {
                Asset = asset;
                Definition = definition;
            }
        }
    }
}

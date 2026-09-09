using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AnimalCafe.Layout
{
    public sealed class SurfaceSlotCatalog
    {
        private readonly Dictionary<string, SurfaceSlotDefinition> definitionsByKey;
        private readonly Dictionary<string, IReadOnlyList<SurfaceSlotDefinition>> definitionsBySupport;

        public SurfaceSlotCatalog(IEnumerable<SurfaceSlotDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            var ordered = definitions.ToList();
            if (ordered.Any(definition => definition == null))
            {
                throw new ArgumentException(
                    "Surface slot definitions must not contain null values.",
                    nameof(definitions));
            }

            ordered.Sort(CompareDefinitions);
            definitionsByKey = new Dictionary<string, SurfaceSlotDefinition>(StringComparer.Ordinal);
            var supportSnapshots = new Dictionary<string, List<SurfaceSlotDefinition>>(
                StringComparer.Ordinal);

            foreach (var definition in ordered)
            {
                var key = CreateKey(definition.SupportDefinitionId, definition.SlotId);
                if (definitionsByKey.ContainsKey(key))
                {
                    throw new ArgumentException(
                        $"Duplicate Surface Slot '{definition.SlotId}' for support definition " +
                        $"'{definition.SupportDefinitionId}'.",
                        nameof(definitions));
                }

                definitionsByKey.Add(key, definition);
                if (!supportSnapshots.TryGetValue(definition.SupportDefinitionId, out var forSupport))
                {
                    forSupport = new List<SurfaceSlotDefinition>();
                    supportSnapshots.Add(definition.SupportDefinitionId, forSupport);
                }

                forSupport.Add(definition);
            }

            definitionsBySupport = new Dictionary<string, IReadOnlyList<SurfaceSlotDefinition>>(
                StringComparer.Ordinal);
            foreach (var pair in supportSnapshots)
            {
                definitionsBySupport.Add(
                    pair.Key,
                    new ReadOnlyCollection<SurfaceSlotDefinition>(pair.Value));
            }
        }

        public bool TryGet(
            string supportDefinitionId,
            string slotId,
            out SurfaceSlotDefinition definition)
        {
            FurnitureDefinition.ValidateDefinitionId(
                supportDefinitionId,
                nameof(supportDefinitionId));
            LayoutStableId.Validate(slotId, nameof(slotId));
            return definitionsByKey.TryGetValue(
                CreateKey(supportDefinitionId, slotId),
                out definition);
        }

        public IReadOnlyList<SurfaceSlotDefinition> GetForSupport(string supportDefinitionId)
        {
            FurnitureDefinition.ValidateDefinitionId(
                supportDefinitionId,
                nameof(supportDefinitionId));

            return definitionsBySupport.TryGetValue(supportDefinitionId, out var definitions)
                ? definitions
                : Array.Empty<SurfaceSlotDefinition>();
        }

        private static int CompareDefinitions(
            SurfaceSlotDefinition left,
            SurfaceSlotDefinition right)
        {
            var supportComparison = string.Compare(
                left.SupportDefinitionId,
                right.SupportDefinitionId,
                StringComparison.Ordinal);
            return supportComparison != 0
                ? supportComparison
                : string.Compare(left.SlotId, right.SlotId, StringComparison.Ordinal);
        }

        private static string CreateKey(string supportDefinitionId, string slotId)
        {
            return supportDefinitionId + "\u001f" + slotId;
        }
    }
}

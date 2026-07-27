using System;
using System.Collections.Generic;

namespace AnimalCafe.Layout
{
    public sealed class FurnitureDefinitionCatalog
    {
        private readonly Dictionary<string, FurnitureDefinition> definitionsById;

        public IReadOnlyList<FurnitureDefinition> Definitions { get; }

        public FurnitureDefinitionCatalog(IEnumerable<FurnitureDefinition> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            var definitionSnapshot = new List<FurnitureDefinition>();
            definitionsById = new Dictionary<string, FurnitureDefinition>(StringComparer.Ordinal);

            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException(
                        "Definitions must not contain null items.",
                        nameof(definitions));
                }

                if (definitionsById.ContainsKey(definition.Id))
                {
                    throw new ArgumentException(
                        $"Duplicate Definition ID '{definition.Id}'.",
                        nameof(definitions));
                }

                definitionsById.Add(definition.Id, definition);
                definitionSnapshot.Add(definition);
            }

            Definitions = definitionSnapshot.AsReadOnly();
        }

        public bool TryGet(string definitionId, out FurnitureDefinition definition)
        {
            FurnitureDefinition.ValidateDefinitionId(definitionId, nameof(definitionId));
            return definitionsById.TryGetValue(definitionId, out definition);
        }

        public FurnitureDefinition GetRequired(string definitionId)
        {
            FurnitureDefinition.ValidateDefinitionId(definitionId, nameof(definitionId));

            if (definitionsById.TryGetValue(definitionId, out var definition))
            {
                return definition;
            }

            throw new KeyNotFoundException(
                $"No Furniture Definition exists with ID '{definitionId}'.");
        }
    }
}

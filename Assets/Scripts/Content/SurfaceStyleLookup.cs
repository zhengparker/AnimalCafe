using System;
using System.Collections.Generic;

namespace AnimalCafe.Content
{
    /// <summary>Read-only stable-ID lookup consumed by Scene render views.</summary>
    public sealed class SurfaceStyleLookup
    {
        private readonly Dictionary<string, SurfaceStyleDefinitionAsset> definitionsById;

        public SurfaceStyleLookup(IEnumerable<SurfaceStyleDefinitionAsset> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            definitionsById = new Dictionary<string, SurfaceStyleDefinitionAsset>(
                StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.StyleId))
                {
                    throw new ArgumentException("Every Surface Style definition needs a stable ID.",
                        nameof(definitions));
                }

                if ((!definition.IsNoneOption && definition.Material == null) ||
                    (definition.IsNoneOption && definition.Kind != SurfaceStyleKind.Wainscoting))
                {
                    throw new ArgumentException("Surface Style definition is not renderable.",
                        nameof(definitions));
                }

                definitionsById.Add(definition.StyleId, definition);
            }
        }

        public bool TryGet(string styleId, out SurfaceStyleDefinitionAsset definition)
        {
            if (styleId != null && definitionsById.TryGetValue(styleId, out definition))
            {
                return true;
            }

            definition = null;
            return false;
        }

        public SurfaceStyleDefinitionAsset GetRequired(string styleId, SurfaceStyleKind expectedKind)
        {
            if (!TryGet(styleId, out var definition) || definition.Kind != expectedKind)
            {
                throw new ArgumentException(
                    $"Missing {expectedKind} Surface Style '{styleId}'.", nameof(styleId));
            }

            return definition;
        }
    }
}

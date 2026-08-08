using System;
using System.Collections.Generic;
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

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry == null)
                {
                    throw new ArgumentException(
                        $"Furniture content entry at index {index} must not be null.",
                        nameof(entries));
                }

                var runtimeDefinition = entry.ToRuntimeDefinition();
                if (entry.Prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Furniture content entry '{runtimeDefinition.Id}' must reference a Prefab.");
                }

                if (prefabSnapshot.ContainsKey(runtimeDefinition.Id))
                {
                    throw new ArgumentException(
                        $"Duplicate Furniture Definition ID '{runtimeDefinition.Id}'.",
                        nameof(entries));
                }

                runtimeDefinitions.Add(runtimeDefinition);
                prefabSnapshot.Add(runtimeDefinition.Id, entry.Prefab);
            }

            var runtimeCatalog = new FurnitureDefinitionCatalog(runtimeDefinitions);

            // Publish only after every entry has converted and validated successfully.
            prefabsById = prefabSnapshot;
            return runtimeCatalog;
        }

        public bool TryGetPrefab(string definitionId, out GameObject prefab)
        {
            FurnitureDefinition.ValidateDefinitionId(definitionId, nameof(definitionId));
            return prefabsById.TryGetValue(definitionId, out prefab);
        }
    }
}

using System;
using System.Collections.Generic;

namespace AnimalCafe.Layout
{
    public sealed class CafeLayout
    {
        private readonly FurnitureDefinitionCatalog definitionCatalog;
        private readonly List<LayoutRegion> unlockedRegions;
        private readonly Dictionary<string, LayoutRegion> regionsById;
        private readonly List<FurnitureInstance> furnitureInstances;
        private readonly Dictionary<string, FurnitureInstance> furnitureInstancesById;

        public GridSettings GridSettings { get; }
        public IReadOnlyList<LayoutRegion> UnlockedRegions { get; }
        public IReadOnlyList<FurnitureInstance> FurnitureInstances { get; }

        public CafeLayout(
            GridSettings gridSettings,
            FurnitureDefinitionCatalog definitionCatalog)
        {
            GridSettings = gridSettings ??
                throw new ArgumentNullException(nameof(gridSettings));
            this.definitionCatalog = definitionCatalog ??
                throw new ArgumentNullException(nameof(definitionCatalog));

            unlockedRegions = new List<LayoutRegion>();
            regionsById = new Dictionary<string, LayoutRegion>(StringComparer.Ordinal);
            furnitureInstances = new List<FurnitureInstance>();
            furnitureInstancesById =
                new Dictionary<string, FurnitureInstance>(StringComparer.Ordinal);

            UnlockedRegions = unlockedRegions.AsReadOnly();
            FurnitureInstances = furnitureInstances.AsReadOnly();
        }

        public void AddRegion(LayoutRegion region)
        {
            if (region == null)
            {
                throw new ArgumentNullException(nameof(region));
            }

            if (regionsById.ContainsKey(region.Id))
            {
                throw new ArgumentException(
                    $"Duplicate Region ID '{region.Id}'.",
                    nameof(region));
            }

            regionsById.Add(region.Id, region);
            unlockedRegions.Add(region);
        }

        public void AddFurnitureInstance(FurnitureInstance instance)
        {
            if (instance == null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            if (!definitionCatalog.TryGet(instance.DefinitionId, out _))
            {
                throw new ArgumentException(
                    $"Unknown Furniture Definition ID '{instance.DefinitionId}'.",
                    nameof(instance));
            }

            if (furnitureInstancesById.ContainsKey(instance.InstanceId))
            {
                throw new ArgumentException(
                    $"Duplicate Furniture Instance ID '{instance.InstanceId}'.",
                    nameof(instance));
            }

            furnitureInstancesById.Add(instance.InstanceId, instance);
            furnitureInstances.Add(instance);
        }

        public bool TryGetFurnitureInstance(
            string instanceId,
            out FurnitureInstance instance)
        {
            ValidateInstanceId(instanceId);
            return furnitureInstancesById.TryGetValue(instanceId, out instance);
        }

        private static void ValidateInstanceId(string instanceId)
        {
            if (instanceId == null)
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            if (!StableId.IsValidFurnitureInstanceId(instanceId))
            {
                throw new ArgumentException(
                    "Instance ID has an invalid format.",
                    nameof(instanceId));
            }
        }
    }
}

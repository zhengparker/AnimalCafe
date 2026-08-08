using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Content
{
    [CreateAssetMenu(menuName = "AnimalCafe/Content/Furniture Definition")]
    public sealed class FurnitureDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string definitionId;
        [SerializeField] private string displayName;
        [SerializeField, Min(1)]
        [Tooltip("Gameplay footprint width in 1m Grid cells. Minimum 1.")]
        private int footprintWidth = 1;
        [SerializeField, Min(1)]
        [Tooltip("Gameplay footprint depth in 1m Grid cells. Minimum 1.")]
        private int footprintDepth = 1;
        [SerializeField] private PlacementSurfaceType allowedPlacementSurfaces;
        [SerializeField] private FurnitureFunctionType functionType;
        [SerializeField] private GameObject prefab;

        public string DefinitionId => definitionId;
        public string DisplayName => displayName;
        public int FootprintWidth => footprintWidth;
        public int FootprintDepth => footprintDepth;
        public PlacementSurfaceType AllowedPlacementSurfaces => allowedPlacementSurfaces;
        public FurnitureFunctionType FunctionType => functionType;
        public GameObject Prefab => prefab;

        // The runtime definition remains pure C#; this asset only converts Inspector data.
        public FurnitureDefinition ToRuntimeDefinition()
        {
            return new FurnitureDefinition(
                definitionId,
                displayName,
                new GridSize(footprintWidth, footprintDepth),
                allowedPlacementSurfaces,
                functionType);
        }
    }
}

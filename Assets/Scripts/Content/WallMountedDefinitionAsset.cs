using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Content
{
    [CreateAssetMenu(menuName = "AnimalCafe/Content/Wall Mounted Definition")]
    public sealed class WallMountedDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string definitionId;
        [SerializeField] private string displayName;
        [SerializeField, Min(1)] private int footprintWidth = 1;
        [SerializeField, Min(1)] private int footprintHeight = 1;
        [SerializeField] private GameObject prefab;

        public string DefinitionId => definitionId;
        public string DisplayName => displayName;
        public WallFootprint Footprint => new WallFootprint(footprintWidth, footprintHeight);
        public GameObject Prefab => prefab;
    }
}

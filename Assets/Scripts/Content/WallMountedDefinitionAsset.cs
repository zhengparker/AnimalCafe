using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Content
{
    [CreateAssetMenu(menuName = "AnimalCafe/Content/Wall Mounted Definition")]
    public sealed class WallMountedDefinitionAsset : ScriptableObject
    {
        public const float MaximumVisualDepth = 0.35f;

        [SerializeField] private string definitionId;
        [SerializeField] private string displayName;
        [SerializeField, Min(1)] private int footprintWidth = 1;
        [SerializeField, Min(1)] private int footprintHeight = 1;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Sprite thumbnail;
        [SerializeField, Min(0f), Tooltip("Maximum visual wall-normal depth in metres. Maximum 0.35 m.")]
        private float maxVisualDepth = MaximumVisualDepth;

        public string DefinitionId => definitionId;
        public string DisplayName => displayName;
        public int FootprintWidth => footprintWidth;
        public int FootprintHeight => footprintHeight;
        public WallFootprint Footprint => new WallFootprint(footprintWidth, footprintHeight);
        public GameObject Prefab => prefab;
        public Sprite Thumbnail => thumbnail;
        public float MaxVisualDepth => maxVisualDepth;

    }
}

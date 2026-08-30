using UnityEngine;

namespace AnimalCafe.Content
{
    public enum SurfaceStyleKind
    {
        Paint,
        Wallpaper,
        Wainscoting,
        Floor
    }
    public enum SurfaceStyleVerticalMapping { OneGrid, FullWall, WaistReference, NotApplicable }

    [CreateAssetMenu(menuName = "AnimalCafe/Content/Surface Style Definition")]
    public sealed class SurfaceStyleDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string styleId;
        [SerializeField] private string displayName;
        [SerializeField] private SurfaceStyleKind kind;
        [SerializeField] private Material material;
        [SerializeField] private Sprite thumbnail;
        [SerializeField] private bool isNoneOption;
        [SerializeField, Min(0f)] private float worldTileWidthMeters = 1f;
        [SerializeField, Min(0f)] private float worldTileHeightMeters = 1f;
        [SerializeField] private SurfaceStyleVerticalMapping verticalMapping = SurfaceStyleVerticalMapping.OneGrid;

        public string StyleId => styleId;
        public string DisplayName => displayName;
        public SurfaceStyleKind Kind => kind;
        public Material Material => material;
        public Sprite Thumbnail => thumbnail;
        public bool IsNoneOption => isNoneOption;
        public Vector2 WorldTileSizeMeters => new Vector2(worldTileWidthMeters, worldTileHeightMeters);
        public float WorldTileWidthMeters => worldTileWidthMeters;
        public float WorldTileHeightMeters => worldTileHeightMeters;
        public SurfaceStyleVerticalMapping VerticalMapping => verticalMapping;

    }
}

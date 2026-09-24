using UnityEngine;

namespace AnimalCafe.UI.P8R
{
    /// <summary>Non-destructive slices of the approved transparent status artwork.
    /// 保留批准PNG原图，只生成三个Sprite切片。</summary>
    public static class P8RStatusIcons
    {
        private static readonly Sprite[] icons = new Sprite[3];
        private static readonly Rect[] bounds = {
            new Rect(193, 131, 464, 460), new Rect(864, 154, 548, 391), new Rect(1738, 137, 120, 461)
        };
        public static Sprite Get(int index)
        {
            index = Mathf.Clamp(index, 0, 2);
            if (icons[index] != null) return icons[index];
            var texture = Resources.Load<Texture2D>("UI/P8R/StatusIcons/approved-status-strip");
            if (texture == null) return null;
            icons[index] = Sprite.Create(texture, bounds[index], Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect);
            icons[index].name = new[] { "status_empty", "status_ready", "status_adjust" }[index];
            return icons[index];
        }
    }
}

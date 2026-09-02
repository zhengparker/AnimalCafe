using System;

namespace AnimalCafe.Layout
{
    public readonly struct FloorTileAppearance
    {
        public GridPosition Position { get; }
        public string StyleId { get; }
        public SurfaceRotation Rotation { get; }

        public FloorTileAppearance(
            GridPosition position,
            string styleId,
            SurfaceRotation rotation)
        {
            WallMountedInstance.ValidateId(styleId, nameof(styleId));

            if (!Enum.IsDefined(typeof(SurfaceRotation), rotation))
            {
                throw new ArgumentOutOfRangeException(nameof(rotation));
            }

            Position = position;
            StyleId = styleId;
            Rotation = rotation;
        }
    }
}

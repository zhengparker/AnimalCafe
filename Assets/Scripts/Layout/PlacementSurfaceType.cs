using System;

namespace AnimalCafe.Layout
{
    [Flags]
    public enum PlacementSurfaceType
    {
        None = 0,
        Floor = 1 << 0,
        Wall = 1 << 1,
        FurnitureSurface = 1 << 2
    }
}

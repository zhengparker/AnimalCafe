using System;
using System.Collections.Generic;

namespace AnimalCafe.Layout
{
    [Serializable]
    public sealed class WallAppearanceSnapshotEntry
    {
        public string SurfaceId;
        public string BaseStyleId;
        public string WainscotingStyleId;
    }

    [Serializable]
    public sealed class FloorTileAppearanceSnapshotEntry
    {
        public int X;
        public int Y;
        public string StyleId;
        public SurfaceRotation Rotation;
    }

    [Serializable]
    public sealed class RoomSurfaceSnapshot
    {
        public string RoomId;
        public List<WallAppearanceSnapshotEntry> Walls;
        public List<FloorTileAppearanceSnapshotEntry> FloorTiles;
    }
}

using System;
using System.Collections.Generic;

namespace AnimalCafe.Layout
{
    [Serializable]
    public sealed class WallMountedSurfaceSnapshotEntry
    {
        public string SurfaceId;
        public int Columns;
        public int Rows;
    }

    [Serializable]
    public sealed class WallMountedInstanceSnapshotEntry
    {
        public string InstanceId;
        public string DefinitionId;
        public string SurfaceId;
        public int Column;
        public int Row;
        public int FootprintWidth;
        public int FootprintHeight;
    }

    [Serializable]
    public sealed class WallMountedLayoutSnapshot
    {
        public List<WallMountedSurfaceSnapshotEntry> Surfaces;
        public List<WallMountedInstanceSnapshotEntry> Instances;
    }
}

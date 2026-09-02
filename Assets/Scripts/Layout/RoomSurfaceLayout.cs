using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AnimalCafe.Layout
{
    public sealed class RoomSurfaceLayout
    {
        private const int FloorGridSize = 8;
        private const int RequiredWallCount = 2;

        private SurfaceState state;

        public string RoomId { get; }
        public IReadOnlyDictionary<string, WallAppearance> Walls => state.Walls;
        public IReadOnlyDictionary<GridPosition, FloorTileAppearance> FloorTiles =>
            state.FloorTiles;

        public RoomSurfaceLayout(
            string roomId,
            IEnumerable<WallAppearance> walls,
            IEnumerable<FloorTileAppearance> floorTiles)
        {
            WallMountedInstance.ValidateId(roomId, nameof(roomId));

            if (walls == null)
            {
                throw new ArgumentNullException(nameof(walls));
            }

            if (floorTiles == null)
            {
                throw new ArgumentNullException(nameof(floorTiles));
            }

            var wallsBySurfaceId =
                new Dictionary<string, WallAppearance>(StringComparer.Ordinal);
            foreach (var wall in walls)
            {
                if (wallsBySurfaceId.ContainsKey(wall.SurfaceId))
                {
                    throw new ArgumentException(
                        "Each wall surface ID must be unique.",
                        nameof(walls));
                }

                wallsBySurfaceId.Add(wall.SurfaceId, wall);
            }

            if (wallsBySurfaceId.Count != RequiredWallCount)
            {
                throw new ArgumentException(
                    "A room surface layout must include exactly two walls.",
                    nameof(walls));
            }

            var floorTilesByPosition =
                new Dictionary<GridPosition, FloorTileAppearance>();
            foreach (var floorTile in floorTiles)
            {
                ValidateFloorPosition(floorTile.Position, nameof(floorTiles));

                if (floorTilesByPosition.ContainsKey(floorTile.Position))
                {
                    throw new ArgumentException(
                        "Each floor position must be unique.",
                        nameof(floorTiles));
                }

                floorTilesByPosition.Add(floorTile.Position, floorTile);
            }

            ValidateCompleteFloorGrid(floorTilesByPosition, nameof(floorTiles));

            RoomId = roomId;
            state = new SurfaceState(wallsBySurfaceId, floorTilesByPosition);
        }

        public bool TryGetWall(string surfaceId, out WallAppearance value)
        {
            WallMountedInstance.ValidateId(surfaceId, nameof(surfaceId));
            return state.WallsBySurfaceId.TryGetValue(surfaceId, out value);
        }

        public bool TryGetFloor(GridPosition position, out FloorTileAppearance value)
        {
            return state.FloorTilesByPosition.TryGetValue(position, out value);
        }

        public void ReplaceWall(WallAppearance value)
        {
            if (!state.WallsBySurfaceId.ContainsKey(value.SurfaceId))
            {
                throw new ArgumentException(
                    "The wall surface ID does not belong to this room.",
                    nameof(value));
            }

            state.WallsBySurfaceId[value.SurfaceId] = value;
        }

        public void ReplaceFloor(FloorTileAppearance value)
        {
            if (!state.FloorTilesByPosition.ContainsKey(value.Position))
            {
                throw new ArgumentException(
                    "The floor position does not belong to this room.",
                    nameof(value));
            }

            state.FloorTilesByPosition[value.Position] = value;
        }

        public void ReplaceAllFloors(string styleId, SurfaceRotation rotation)
        {
            WallMountedInstance.ValidateId(styleId, nameof(styleId));

            if (!Enum.IsDefined(typeof(SurfaceRotation), rotation))
            {
                throw new ArgumentOutOfRangeException(nameof(rotation));
            }

            var positions = new List<GridPosition>(state.FloorTilesByPosition.Keys);
            foreach (var position in positions)
            {
                state.FloorTilesByPosition[position] = new FloorTileAppearance(
                    position,
                    styleId,
                    rotation);
            }
        }

        public RoomSurfaceSnapshot CaptureSnapshot()
        {
            return new RoomSurfaceSnapshot
            {
                RoomId = RoomId,
                Walls = state.WallsBySurfaceId
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new WallAppearanceSnapshotEntry
                    {
                        SurfaceId = pair.Value.SurfaceId,
                        BaseStyleId = pair.Value.BaseStyleId,
                        WainscotingStyleId = pair.Value.WainscotingStyleId
                    })
                    .ToList(),
                FloorTiles = state.FloorTilesByPosition
                    .OrderBy(pair => pair.Key.X)
                    .ThenBy(pair => pair.Key.Y)
                    .Select(pair => new FloorTileAppearanceSnapshotEntry
                    {
                        X = pair.Value.Position.X,
                        Y = pair.Value.Position.Y,
                        StyleId = pair.Value.StyleId,
                        Rotation = pair.Value.Rotation
                    })
                    .ToList()
            };
        }

        public static RoomSurfaceLayout FromSnapshot(RoomSurfaceSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (snapshot.Walls == null)
            {
                throw new ArgumentException(
                    "Snapshot walls are required.",
                    nameof(snapshot));
            }

            if (snapshot.FloorTiles == null)
            {
                throw new ArgumentException(
                    "Snapshot floor tiles are required.",
                    nameof(snapshot));
            }

            var walls = new List<WallAppearance>(snapshot.Walls.Count);
            foreach (var wall in snapshot.Walls)
            {
                if (wall == null)
                {
                    throw new ArgumentException(
                        "Snapshot walls cannot contain null entries.",
                        nameof(snapshot));
                }

                // JsonUtility restores a serialized null string field as string.Empty.
                var wainscotingStyleId = wall.WainscotingStyleId == string.Empty
                    ? null
                    : wall.WainscotingStyleId;

                walls.Add(new WallAppearance(
                    wall.SurfaceId,
                    wall.BaseStyleId,
                    wainscotingStyleId));
            }

            var floorTiles = new List<FloorTileAppearance>(snapshot.FloorTiles.Count);
            foreach (var floorTile in snapshot.FloorTiles)
            {
                if (floorTile == null)
                {
                    throw new ArgumentException(
                        "Snapshot floor tiles cannot contain null entries.",
                        nameof(snapshot));
                }

                floorTiles.Add(new FloorTileAppearance(
                    new GridPosition(floorTile.X, floorTile.Y),
                    floorTile.StyleId,
                    floorTile.Rotation));
            }

            return new RoomSurfaceLayout(snapshot.RoomId, walls, floorTiles);
        }

        public void ApplySnapshot(RoomSurfaceSnapshot snapshot)
        {
            var candidate = FromSnapshot(snapshot);
            if (!string.Equals(RoomId, candidate.RoomId, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Snapshot Room ID must match the existing room.",
                    nameof(snapshot));
            }

            var currentWallIds = new HashSet<string>(
                state.WallsBySurfaceId.Keys,
                StringComparer.Ordinal);
            if (!currentWallIds.SetEquals(candidate.state.WallsBySurfaceId.Keys))
            {
                throw new ArgumentException(
                    "Snapshot Wall Surface IDs must match the existing room.",
                    nameof(snapshot));
            }

            // Candidate construction validates the full graph before this single state swap.
            state = candidate.state;
        }

        private static void ValidateFloorPosition(GridPosition position, string paramName)
        {
            if (position.X < 0 || position.X >= FloorGridSize ||
                position.Y < 0 || position.Y >= FloorGridSize)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    "Floor tiles must be inside the fixed 8 by 8 room grid.");
            }
        }

        private static void ValidateCompleteFloorGrid(
            IReadOnlyDictionary<GridPosition, FloorTileAppearance> floorTiles,
            string paramName)
        {
            if (floorTiles.Count != FloorGridSize * FloorGridSize)
            {
                throw new ArgumentException(
                    "A room surface layout must include all 64 floor tiles.",
                    paramName);
            }
        }

        private sealed class SurfaceState
        {
            public Dictionary<string, WallAppearance> WallsBySurfaceId { get; }
            public Dictionary<GridPosition, FloorTileAppearance> FloorTilesByPosition { get; }
            public IReadOnlyDictionary<string, WallAppearance> Walls { get; }
            public IReadOnlyDictionary<GridPosition, FloorTileAppearance> FloorTiles { get; }

            public SurfaceState(
                Dictionary<string, WallAppearance> wallsBySurfaceId,
                Dictionary<GridPosition, FloorTileAppearance> floorTilesByPosition)
            {
                WallsBySurfaceId = wallsBySurfaceId;
                FloorTilesByPosition = floorTilesByPosition;
                Walls = new ReadOnlyDictionary<string, WallAppearance>(wallsBySurfaceId);
                FloorTiles = new ReadOnlyDictionary<GridPosition, FloorTileAppearance>(
                    floorTilesByPosition);
            }
        }
    }
}

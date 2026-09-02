using System.Collections.Generic;
using AnimalCafe.Layout;

namespace AnimalCafe.Decoration
{
    public sealed class SurfacePreviewTransaction
    {
        private readonly RoomSurfaceSnapshot proposedSnapshot;
        private readonly IReadOnlyList<GridPosition> previewedFloorPositions;

        public SurfaceEditScope Scope { get; }
        public string TargetWallSurfaceId { get; }
        public GridPosition? SelectedFloorPosition { get; }
        public string ArmedStyleId { get; }
        public SurfaceRotation ArmedRotation { get; }
        public IReadOnlyList<GridPosition> PreviewedFloorPositions => previewedFloorPositions;
        public bool CanUndo { get; }
        public bool HasChanges { get; }
        public string UsingStyleId { get; }
        public string PreviewStyleId { get; }
        public string UsingWallBaseStyleId { get; }
        public string PreviewWallBaseStyleId { get; }
        public string UsingWallWainscotingStyleId { get; }
        public string PreviewWallWainscotingStyleId { get; }
        public RoomSurfaceSnapshot ProposedSnapshot => CopySnapshot(proposedSnapshot);

        internal SurfacePreviewTransaction(
            SurfaceEditScope scope,
            string targetWallSurfaceId,
            GridPosition? selectedFloorPosition,
            string armedStyleId,
            SurfaceRotation armedRotation,
            bool canUndo,
            bool hasChanges,
            string usingStyleId,
            string previewStyleId,
            string usingWallBaseStyleId,
            string previewWallBaseStyleId,
            string usingWallWainscotingStyleId,
            string previewWallWainscotingStyleId,
            RoomSurfaceSnapshot proposedSnapshot)
            : this(
                scope,
                targetWallSurfaceId,
                selectedFloorPosition,
                armedStyleId,
                armedRotation,
                canUndo,
                hasChanges,
                usingStyleId,
                previewStyleId,
                usingWallBaseStyleId,
                previewWallBaseStyleId,
                usingWallWainscotingStyleId,
                previewWallWainscotingStyleId,
                proposedSnapshot,
                proposedSnapshot)
        {
        }

        internal SurfacePreviewTransaction(
            SurfaceEditScope scope,
            string targetWallSurfaceId,
            GridPosition? selectedFloorPosition,
            string armedStyleId,
            SurfaceRotation armedRotation,
            bool canUndo,
            bool hasChanges,
            string usingStyleId,
            string previewStyleId,
            string usingWallBaseStyleId,
            string previewWallBaseStyleId,
            string usingWallWainscotingStyleId,
            string previewWallWainscotingStyleId,
            RoomSurfaceSnapshot baselineSnapshot,
            RoomSurfaceSnapshot proposedSnapshot)
        {
            Scope = scope;
            TargetWallSurfaceId = targetWallSurfaceId;
            SelectedFloorPosition = selectedFloorPosition;
            ArmedStyleId = armedStyleId;
            ArmedRotation = armedRotation;
            CanUndo = canUndo;
            HasChanges = hasChanges;
            UsingStyleId = usingStyleId;
            PreviewStyleId = previewStyleId;
            UsingWallBaseStyleId = usingWallBaseStyleId;
            PreviewWallBaseStyleId = previewWallBaseStyleId;
            UsingWallWainscotingStyleId = usingWallWainscotingStyleId;
            PreviewWallWainscotingStyleId = previewWallWainscotingStyleId;
            this.proposedSnapshot = CopySnapshot(proposedSnapshot);
            previewedFloorPositions = CreatePreviewedFloorPositions(
                scope,
                baselineSnapshot,
                this.proposedSnapshot);
        }

        private static IReadOnlyList<GridPosition> CreatePreviewedFloorPositions(
            SurfaceEditScope scope,
            RoomSurfaceSnapshot baseline,
            RoomSurfaceSnapshot proposed)
        {
            if (scope != SurfaceEditScope.SingleGridFloor)
            {
                return new List<GridPosition>().AsReadOnly();
            }

            var baselineByPosition = new Dictionary<GridPosition, FloorTileAppearanceSnapshotEntry>();
            foreach (var entry in baseline.FloorTiles)
            {
                baselineByPosition.Add(new GridPosition(entry.X, entry.Y), entry);
            }

            var positions = new List<GridPosition>();
            foreach (var entry in proposed.FloorTiles)
            {
                var position = new GridPosition(entry.X, entry.Y);
                if (!baselineByPosition.TryGetValue(position, out var baselineEntry) ||
                    baselineEntry.StyleId != entry.StyleId ||
                    baselineEntry.Rotation != entry.Rotation)
                {
                    positions.Add(position);
                }
            }

            positions.Sort((left, right) =>
            {
                var byX = left.X.CompareTo(right.X);
                return byX != 0 ? byX : left.Y.CompareTo(right.Y);
            });
            return positions.AsReadOnly();
        }

        private static RoomSurfaceSnapshot CopySnapshot(RoomSurfaceSnapshot source)
        {
            var walls = new List<WallAppearanceSnapshotEntry>(source.Walls.Count);
            foreach (var wall in source.Walls)
            {
                walls.Add(new WallAppearanceSnapshotEntry
                {
                    SurfaceId = wall.SurfaceId,
                    BaseStyleId = wall.BaseStyleId,
                    WainscotingStyleId = wall.WainscotingStyleId
                });
            }

            var floors = new List<FloorTileAppearanceSnapshotEntry>(
                source.FloorTiles.Count);
            foreach (var floor in source.FloorTiles)
            {
                floors.Add(new FloorTileAppearanceSnapshotEntry
                {
                    X = floor.X,
                    Y = floor.Y,
                    StyleId = floor.StyleId,
                    Rotation = floor.Rotation
                });
            }

            return new RoomSurfaceSnapshot
            {
                RoomId = source.RoomId,
                Walls = walls,
                FloorTiles = floors
            };
        }
    }
}

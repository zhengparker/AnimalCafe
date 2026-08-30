using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    public sealed class CafeLayoutRuntime : MonoBehaviour
    {
        internal const string InitialInstanceId = "00000000000000000000000000000001";
        private const string InitialDefinitionId = "furniture.counter.module.01";

        [SerializeField] private FurnitureContentCatalog contentCatalog;
        [SerializeField] private EntrancePortalAuthoring entrancePortal;

        public CafeLayout Layout { get; private set; }
        public RoomSurfaceLayout RoomSurfaceLayout { get; private set; }
        public WallMountedLayout WallMountedLayout { get; private set; }

        public void InitializePhase7Layouts(
            string roomId,
            IEnumerable<WallSurfaceAuthoring> wallAuthoring,
            string initialWallBaseStyleId,
            string initialFloorStyleId)
        {
            if (RoomSurfaceLayout != null || WallMountedLayout != null)
            {
                return;
            }

            if (wallAuthoring == null)
            {
                throw new ArgumentNullException(nameof(wallAuthoring));
            }

            var authoredWalls = wallAuthoring.ToArray();
            if (authoredWalls.Any(item => item == null))
            {
                throw new ArgumentException(
                    "Wall authoring cannot contain null entries.",
                    nameof(wallAuthoring));
            }

            var appearances = authoredWalls.Select(item =>
                new WallAppearance(item.SurfaceId, initialWallBaseStyleId, null));
            var floorTiles = new List<FloorTileAppearance>(64);
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    floorTiles.Add(new FloorTileAppearance(
                        new GridPosition(x, y),
                        initialFloorStyleId,
                        SurfaceRotation.Degrees0));
                }
            }

            var surfaceLayouts = authoredWalls.Select(item =>
                new WallSurfaceLayout(item.SurfaceId, item.Columns, item.Rows));

            // Build both candidates before publishing either property.
            var roomCandidate = new RoomSurfaceLayout(roomId, appearances, floorTiles);
            var mountedCandidate = new WallMountedLayout(surfaceLayouts);
            RoomSurfaceLayout = roomCandidate;
            WallMountedLayout = mountedCandidate;
        }

        internal bool UsesContentCatalog(FurnitureContentCatalog candidate)
        {
            return ReferenceEquals(contentCatalog, candidate);
        }

        public void Initialize()
        {
            if (Layout != null)
            {
                return;
            }

            if (contentCatalog == null)
            {
                throw new InvalidOperationException(
                    "CafeLayoutRuntime requires the shared FurnitureContentCatalog.");
            }

            if (entrancePortal == null)
            {
                throw new InvalidOperationException(
                    "CafeLayoutRuntime requires the configured EntrancePortalAuthoring.");
            }

            var runtimeCatalog = contentCatalog.BuildRuntimeCatalog();
            var settings = new GridSettings(1f);
            var bounds = new LayoutBounds(
                new GridPosition(0, 0),
                new GridSize(8, 8));
            var candidate = new CafeLayout(settings, runtimeCatalog, bounds);
            candidate.AddRegion(new LayoutRegion(
                "region.main",
                bounds.Origin,
                bounds.Size,
                LayoutZoneType.Interior));

            var entrance = entrancePortal.CreateReservation();
            ValidateEntranceReservation(entrance);
            candidate.AddReservation(entrance);

            var counter = FurnitureInstance.Restore(
                InitialInstanceId,
                InitialDefinitionId,
                new GridPosition(2, 3),
                FurnitureRotation.Degrees0);
            var placement = candidate.PlaceFurniture(counter);
            if (!placement.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Initial Counter placement was rejected: {placement.FailureReason}.");
            }

            Layout = candidate;
        }

        private static void ValidateEntranceReservation(LayoutReservation reservation)
        {
            if (reservation == null
                || !string.Equals(reservation.Id, "entrance.main", StringComparison.Ordinal)
                || reservation.Type != LayoutReservationType.EntranceClearance
                || reservation.Origin != new GridPosition(3, 0)
                || reservation.Size != new GridSize(2, 2))
            {
                throw new InvalidOperationException(
                    "The configured entrance must create entrance.main EntranceClearance at (3,0) with size 2 x 2.");
            }
        }
    }
}

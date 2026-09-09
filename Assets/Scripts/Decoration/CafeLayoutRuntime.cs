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

        private SurfaceSlotCatalog surfaceSlotCatalog;
        private FunctionalDirectionCatalog functionalDirectionCatalog;
        private LayoutReadinessReport currentReadiness;

        public CafeLayout Layout { get; private set; }
        public RoomSurfaceLayout RoomSurfaceLayout { get; private set; }
        public WallMountedLayout WallMountedLayout { get; private set; }
        public FunctionalSurfaceLayout FunctionalSurfaceLayout { get; private set; }
        public LayoutReadinessReport CurrentReadiness => currentReadiness;
        public int ReadinessVersion { get; private set; }
        internal SurfaceSlotCatalog SurfaceSlotCatalog => surfaceSlotCatalog;

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
            if (Layout != null &&
                FunctionalSurfaceLayout != null &&
                currentReadiness != null)
            {
                return;
            }

            if (Layout != null ||
                FunctionalSurfaceLayout != null ||
                currentReadiness != null)
            {
                throw new InvalidOperationException(
                    "CafeLayoutRuntime cannot initialize from a partial runtime state.");
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

            var runtimeCatalogCandidate = contentCatalog.BuildRuntimeCatalog();
            var settings = new GridSettings(1f);
            var bounds = new LayoutBounds(
                new GridPosition(0, 0),
                new GridSize(8, 8));
            var layoutCandidate = new CafeLayout(settings, runtimeCatalogCandidate, bounds);
            layoutCandidate.AddRegion(new LayoutRegion(
                "region.main",
                bounds.Origin,
                bounds.Size,
                LayoutZoneType.Interior));

            var entrance = entrancePortal.CreateReservation();
            ValidateEntranceReservation(entrance);
            layoutCandidate.AddReservation(entrance);

            var counter = FurnitureInstance.Restore(
                InitialInstanceId,
                InitialDefinitionId,
                new GridPosition(2, 3),
                FurnitureRotation.Degrees0);
            var placement = layoutCandidate.PlaceFurniture(counter);
            if (!placement.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Initial Counter placement was rejected: {placement.FailureReason}.");
            }

            // Build every Phase 8 candidate before publishing any runtime property.
            var slotCatalogCandidate = contentCatalog.BuildSurfaceSlotCatalog(settings);
            var directionCatalogCandidate = contentCatalog.BuildFunctionalDirectionCatalog();
            var functionalLayoutCandidate = new FunctionalSurfaceLayout(
                layoutCandidate,
                runtimeCatalogCandidate,
                slotCatalogCandidate);
            var readinessCandidate = new LayoutReadinessEvaluator().Evaluate(
                layoutCandidate,
                functionalLayoutCandidate,
                slotCatalogCandidate,
                directionCatalogCandidate);

            surfaceSlotCatalog = slotCatalogCandidate;
            functionalDirectionCatalog = directionCatalogCandidate;
            Layout = layoutCandidate;
            FunctionalSurfaceLayout = functionalLayoutCandidate;
            currentReadiness = readinessCandidate;
            ReadinessVersion = 1;
        }

        public void RecalculateReadiness()
        {
            if (Layout == null ||
                FunctionalSurfaceLayout == null ||
                surfaceSlotCatalog == null ||
                functionalDirectionCatalog == null)
            {
                throw new InvalidOperationException(
                    "CafeLayoutRuntime must be initialized before recalculating readiness.");
            }

            var nextReadiness = new LayoutReadinessEvaluator().Evaluate(
                Layout,
                FunctionalSurfaceLayout,
                surfaceSlotCatalog,
                functionalDirectionCatalog);
            currentReadiness = nextReadiness;
            ReadinessVersion = checked(ReadinessVersion + 1);
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

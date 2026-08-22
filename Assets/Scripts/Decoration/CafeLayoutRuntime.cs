using System;
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

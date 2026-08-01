using System;
using System.Text.RegularExpressions;

namespace AnimalCafe.Layout
{
    public sealed class FurnitureDefinition
    {
        public const int MaxFootprintCellCount = 1024;

        private const PlacementSurfaceType KnownPlacementSurfaces =
            PlacementSurfaceType.Floor |
            PlacementSurfaceType.Wall |
            PlacementSurfaceType.FurnitureSurface;

        private static readonly Regex DefinitionIdPattern = new Regex(
            "^[a-z0-9][a-z0-9._-]*$",
            RegexOptions.CultureInvariant);

        public string Id { get; }
        public string DisplayName { get; }
        public GridSize Footprint { get; }
        public PlacementSurfaceType AllowedPlacementSurfaces { get; }

        public FurnitureDefinition(
            string id,
            string displayName,
            GridSize footprint,
            PlacementSurfaceType allowedPlacementSurfaces)
        {
            ValidateDefinitionId(id, nameof(id));

            if (displayName == null)
            {
                throw new ArgumentNullException(nameof(displayName));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("Display name must not be empty or whitespace.", nameof(displayName));
            }

            if (footprint.Width < 1 || footprint.Height < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(footprint),
                    footprint,
                    "Footprint width and height must each be at least one.");
            }

            var footprintCellCount =
                (long)footprint.Width * footprint.Height;
            if (footprintCellCount > MaxFootprintCellCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(footprint),
                    footprint,
                    $"Furniture footprint must contain at most {MaxFootprintCellCount} cells; actual area was {footprintCellCount}.");
            }

            if (allowedPlacementSurfaces == PlacementSurfaceType.None ||
                (allowedPlacementSurfaces & ~KnownPlacementSurfaces) != PlacementSurfaceType.None)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(allowedPlacementSurfaces),
                    allowedPlacementSurfaces,
                    "Allowed placement surfaces must contain only known non-None flags.");
            }

            Id = id;
            DisplayName = displayName;
            Footprint = footprint;
            AllowedPlacementSurfaces = allowedPlacementSurfaces;
        }

        internal static void ValidateDefinitionId(string id, string paramName)
        {
            if (id == null)
            {
                throw new ArgumentNullException(paramName);
            }

            if (string.IsNullOrWhiteSpace(id) || !DefinitionIdPattern.IsMatch(id))
            {
                throw new ArgumentException("Definition ID has an invalid format.", paramName);
            }
        }
    }
}

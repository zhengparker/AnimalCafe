using System;

namespace AnimalCafe.Layout
{
    public sealed class LayoutRegion
    {
        public string Id { get; }
        public GridPosition Origin { get; }
        public GridSize Size { get; }
        public LayoutZoneType ZoneType { get; }

        public LayoutRegion(
            string id,
            GridPosition origin,
            GridSize size,
            LayoutZoneType zoneType)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException(
                    "Region ID must not be empty or whitespace.",
                    nameof(id));
            }

            if (size.Width < 1 || size.Height < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(size),
                    size,
                    "Region size must have a width and height of at least one.");
            }

            switch (zoneType)
            {
                case LayoutZoneType.Interior:
                case LayoutZoneType.Exterior:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(zoneType),
                        zoneType,
                        "Zone type must be a known value.");
            }

            Id = id;
            Origin = origin;
            Size = size;
            ZoneType = zoneType;
        }
    }
}

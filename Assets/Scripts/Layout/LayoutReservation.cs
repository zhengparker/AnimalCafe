using System;
using System.Text.RegularExpressions;

namespace AnimalCafe.Layout
{
    public sealed class LayoutReservation
    {
        private static readonly Regex ReservationIdPattern = new Regex(
            "^[a-z0-9][a-z0-9._-]*$",
            RegexOptions.CultureInvariant);

        public string Id { get; }
        public LayoutReservationType Type { get; }
        public GridPosition Origin { get; }
        public GridSize Size { get; }

        public LayoutReservation(
            string id,
            LayoutReservationType type,
            GridPosition origin,
            GridSize size)
        {
            ValidateId(id);

            if (size.Width < 1 || size.Height < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(size),
                    size,
                    "Reservation size must have a width and height of at least one.");
            }

            if (!Enum.IsDefined(typeof(LayoutReservationType), type))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(type),
                    type,
                    "Reservation type must be a known value.");
            }

            Id = id;
            Type = type;
            Origin = origin;
            Size = size;
        }

        public bool Contains(GridPosition position)
        {
            var right = (long)Origin.X + Size.Width;
            var top = (long)Origin.Y + Size.Height;

            return position.X >= Origin.X &&
                   position.X < right &&
                   position.Y >= Origin.Y &&
                   position.Y < top;
        }

        private static void ValidateId(string id)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(id) ||
                !ReservationIdPattern.IsMatch(id))
            {
                throw new ArgumentException(
                    "Reservation ID has an invalid format.",
                    nameof(id));
            }
        }
    }
}

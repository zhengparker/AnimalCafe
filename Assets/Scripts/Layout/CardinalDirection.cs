using System;

namespace AnimalCafe.Layout
{
    public enum CardinalDirection
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3
    }

    public static class CardinalDirectionExtensions
    {
        public static CardinalDirection Rotate(
            this CardinalDirection direction,
            FurnitureRotation rotation)
        {
            if (!Enum.IsDefined(typeof(CardinalDirection), direction))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(direction),
                    direction,
                    "Cardinal direction must be a defined value.");
            }

            if (!Enum.IsDefined(typeof(FurnitureRotation), rotation))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rotation),
                    rotation,
                    "Furniture rotation must be a defined value.");
            }

            return (CardinalDirection)(((int)direction + ((int)rotation / 90)) % 4);
        }
    }
}

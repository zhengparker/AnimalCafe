using System;

namespace AnimalCafe.Layout
{
    public readonly struct LayoutBounds
    {
        public GridPosition Origin { get; }
        public GridSize Size { get; }

        public LayoutBounds(GridPosition origin, GridSize size)
        {
            if (size.Width < 1 || size.Height < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(size),
                    size,
                    "Layout bounds size must have a width and height of at least one.");
            }

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
    }
}

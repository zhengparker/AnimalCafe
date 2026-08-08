using System;

namespace AnimalCafe.Layout
{
    public readonly struct WallFootprint
    {
        public int Width { get; }
        public int Height { get; }

        public WallFootprint(int width, int height)
        {
            if (width < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(width),
                    width,
                    "Wall footprint width must be at least one.");
            }

            if (height < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(height),
                    height,
                    "Wall footprint height must be at least one.");
            }

            Width = width;
            Height = height;
        }
    }
}

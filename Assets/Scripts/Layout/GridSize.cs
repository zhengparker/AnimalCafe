using System;

namespace AnimalCafe.Layout
{
    public readonly struct GridSize : IEquatable<GridSize>
    {
        public int Width { get; }
        public int Height { get; }

        public GridSize(int width, int height)
        {
            if (width < 1) throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be at least one.");
            if (height < 1) throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be at least one.");
            Width = width;
            Height = height;
        }

        public GridSize Rotate(FurnitureRotation rotation)
        {
            switch (rotation)
            {
                case FurnitureRotation.Degrees0:
                case FurnitureRotation.Degrees180:
                    return this;
                case FurnitureRotation.Degrees90:
                case FurnitureRotation.Degrees270:
                    return new GridSize(Height, Width);
                default:
                    throw new ArgumentOutOfRangeException(nameof(rotation), rotation, "Rotation must be a defined quarter turn.");
            }
        }

        public bool Equals(GridSize other) => Width == other.Width && Height == other.Height;
        public override bool Equals(object obj) => obj is GridSize other && Equals(other);
        public override int GetHashCode() { unchecked { return (Width * 397) ^ Height; } }
        public static bool operator ==(GridSize left, GridSize right) => left.Equals(right);
        public static bool operator !=(GridSize left, GridSize right) => !left.Equals(right);
    }
}

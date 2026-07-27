using System;

namespace AnimalCafe.Layout
{
    public sealed class GridSettings
    {
        public float CellSize { get; }

        public GridSettings(float cellSize)
        {
            if (cellSize <= 0f || float.IsNaN(cellSize) || float.IsInfinity(cellSize))
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Cell size must be finite and greater than zero.");
            }

            CellSize = cellSize;
        }
    }
}

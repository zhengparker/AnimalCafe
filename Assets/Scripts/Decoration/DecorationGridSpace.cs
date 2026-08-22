using System;
using System.Collections.Generic;
using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    /// <summary>
    /// Maps Layout Grid coordinates into a root whose origin is the southwest
    /// corner of the configured Layout bounds.
    /// å°† Layout Grid åæ ‡æ˜ å°„åˆ°ä»¥ southwest corner ä¸ºåŽŸç‚¹çš„ Scene rootã€‚
    /// </summary>
    public readonly struct DecorationGridSpace
    {
        public GridSettings Settings { get; }
        public LayoutBounds Bounds { get; }

        public DecorationGridSpace(GridSettings settings, LayoutBounds bounds)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (bounds.Size.Width < 1 || bounds.Size.Height < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(bounds),
                    bounds,
                    "Layout bounds must have a width and height of at least one.");
            }

            Bounds = bounds;
        }

        public Vector3 GetCellCenterLocal(GridPosition cell, float height = 0f)
        {
            ValidateHeight(height);
            var localX = ((double)cell.X - Bounds.Origin.X + 0.5d) * Settings.CellSize;
            var localZ = ((double)cell.Y - Bounds.Origin.Y + 0.5d) * Settings.CellSize;
            return new Vector3(
                ToFiniteCoordinate(localX),
                height,
                ToFiniteCoordinate(localZ));
        }

        public Vector3 GetFootprintCenterLocal(
            IReadOnlyList<GridPosition> cells,
            float height = 0f)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (cells.Count == 0)
            {
                throw new ArgumentException(
                    "A footprint must contain at least one Grid cell.",
                    nameof(cells));
            }

            ValidateHeight(height);
            var minX = cells[0].X;
            var maxX = cells[0].X;
            var minY = cells[0].Y;
            var maxY = cells[0].Y;

            for (var index = 1; index < cells.Count; index++)
            {
                minX = Math.Min(minX, cells[index].X);
                maxX = Math.Max(maxX, cells[index].X);
                minY = Math.Min(minY, cells[index].Y);
                maxY = Math.Max(maxY, cells[index].Y);
            }

            var localX = (
                (long)minX - Bounds.Origin.X +
                (long)maxX - Bounds.Origin.X + 1L) *
                0.5d * Settings.CellSize;
            var localZ = (
                (long)minY - Bounds.Origin.Y +
                (long)maxY - Bounds.Origin.Y + 1L) *
                0.5d * Settings.CellSize;

            return new Vector3(
                ToFiniteCoordinate(localX),
                height,
                ToFiniteCoordinate(localZ));
        }

        public Quaternion GetLocalRotation(FurnitureRotation rotation)
        {
            switch (rotation)
            {
                case FurnitureRotation.Degrees0:
                    return Quaternion.identity;
                case FurnitureRotation.Degrees90:
                    return Quaternion.Euler(0f, 90f, 0f);
                case FurnitureRotation.Degrees180:
                    return Quaternion.Euler(0f, 180f, 0f);
                case FurnitureRotation.Degrees270:
                    return Quaternion.Euler(0f, 270f, 0f);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(rotation),
                        rotation,
                        "Rotation must be a defined quarter turn.");
            }
        }

        private static void ValidateHeight(float height)
        {
            if (float.IsNaN(height) || float.IsInfinity(height))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(height),
                    height,
                    "Height must be finite.");
            }
        }

        private static float ToFiniteCoordinate(double value)
        {
            if (double.IsNaN(value) ||
                double.IsInfinity(value) ||
                value > float.MaxValue ||
                value < -float.MaxValue)
            {
                throw new OverflowException(
                    "Computed Grid coordinate exceeds the finite float range.");
            }

            return (float)value;
        }
    }
}

using System;
using System.Collections.Generic;
using AnimalCafe.Layout;

namespace AnimalCafe.Capacity
{
    public static class FloorCapacitySource
    {
        public static int CountInteriorCells(CafeLayout layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            var cells = new HashSet<GridPosition>();
            foreach (var region in layout.UnlockedRegions)
            {
                if (region.ZoneType != LayoutZoneType.Interior)
                {
                    continue;
                }

                // 枚举前检查最后一个格子的坐标；int.MaxValue 的单格仍合法。
                var lastX = (long)region.Origin.X + region.Size.Width - 1L;
                var lastY = (long)region.Origin.Y + region.Size.Height - 1L;
                if (lastX > int.MaxValue || lastY > int.MaxValue)
                {
                    throw new OverflowException("Interior region exceeds GridPosition range.");
                }

                for (long x = region.Origin.X; x <= lastX; x++)
                {
                    for (long y = region.Origin.Y; y <= lastY; y++)
                    {
                        var position = new GridPosition((int)x, (int)y);
                        if (!layout.IsInsideUnlockedRegion(position) ||
                            cells.Contains(position))
                        {
                            continue;
                        }

                        if (cells.Count == int.MaxValue)
                        {
                            throw new OverflowException("Interior cell count exceeds int range.");
                        }

                        cells.Add(position);
                    }
                }
            }

            return cells.Count;
        }
    }
}

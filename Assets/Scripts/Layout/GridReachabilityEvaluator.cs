using System;
using System.Collections.Generic;

namespace AnimalCafe.Layout
{
    public sealed class GridReachabilityEvaluator
    {
        private static readonly GridPosition[] NeighborOffsets =
        {
            new GridPosition(0, 1),
            new GridPosition(1, 0),
            new GridPosition(0, -1),
            new GridPosition(-1, 0)
        };

        public bool IsReachable(
            GridPosition start,
            GridPosition target,
            Func<GridPosition, bool> isWalkable)
        {
            if (isWalkable == null)
            {
                throw new ArgumentNullException(nameof(isWalkable));
            }

            if (!isWalkable(start))
            {
                return false;
            }

            var visited = new HashSet<GridPosition> { start };
            var pending = new Queue<GridPosition>();
            pending.Enqueue(start);

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                if (current == target)
                {
                    return true;
                }

                foreach (var offset in NeighborOffsets)
                {
                    if (!TryAdd(current, offset, out var neighbor) ||
                        !visited.Add(neighbor) ||
                        !isWalkable(neighbor))
                    {
                        continue;
                    }

                    pending.Enqueue(neighbor);
                }
            }

            return false;
        }

        private static bool TryAdd(
            GridPosition position,
            GridPosition offset,
            out GridPosition result)
        {
            var x = (long)position.X + offset.X;
            var y = (long)position.Y + offset.Y;
            if (x < int.MinValue || x > int.MaxValue ||
                y < int.MinValue || y > int.MaxValue)
            {
                result = default;
                return false;
            }

            result = new GridPosition((int)x, (int)y);
            return true;
        }
    }
}

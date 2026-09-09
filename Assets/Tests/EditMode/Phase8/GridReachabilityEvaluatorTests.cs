using System.Collections.Generic;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class GridReachabilityEvaluatorTests
    {
        [Test]
        public void IsReachable_FindsRouteAroundObstacle()
        {
            var walkable = new HashSet<GridPosition>();
            for (var x = 0; x < 5; x++)
            {
                for (var y = 0; y < 3; y++)
                {
                    walkable.Add(new GridPosition(x, y));
                }
            }

            walkable.Remove(new GridPosition(1, 0));
            walkable.Remove(new GridPosition(2, 0));
            walkable.Remove(new GridPosition(3, 0));

            var result = new GridReachabilityEvaluator().IsReachable(
                new GridPosition(0, 0),
                new GridPosition(4, 0),
                walkable.Contains);

            Assert.That(result, Is.True);
        }

        [Test]
        public void IsReachable_RejectsBlockedStartAndTarget()
        {
            var evaluator = new GridReachabilityEvaluator();
            var walkable = new HashSet<GridPosition>
            {
                new GridPosition(0, 0),
                new GridPosition(1, 0)
            };

            Assert.That(
                evaluator.IsReachable(
                    new GridPosition(-1, 0),
                    new GridPosition(1, 0),
                    walkable.Contains),
                Is.False);
            Assert.That(
                evaluator.IsReachable(
                    new GridPosition(0, 0),
                    new GridPosition(2, 0),
                    walkable.Contains),
                Is.False);
        }

        [Test]
        public void IsReachable_VisitsImmediateNeighborsNorthEastSouthWest()
        {
            var inspected = new List<GridPosition>();
            var start = new GridPosition(10, 10);

            var result = new GridReachabilityEvaluator().IsReachable(
                start,
                new GridPosition(20, 20),
                position =>
                {
                    inspected.Add(position);
                    return position == start;
                });

            Assert.That(result, Is.False);
            Assert.That(inspected, Is.EqualTo(new[]
            {
                start,
                new GridPosition(10, 11),
                new GridPosition(11, 10),
                new GridPosition(10, 9),
                new GridPosition(9, 10)
            }));
        }

        [Test]
        public void IsReachable_LargeLegalRegionCompletesIterativelyAroundBarrier()
        {
            const int size = 128;
            var start = new GridPosition(0, 0);
            var target = new GridPosition(size - 1, size - 1);

            var result = new GridReachabilityEvaluator().IsReachable(
                start,
                target,
                position =>
                    position.X >= 0 && position.X < size &&
                    position.Y >= 0 && position.Y < size &&
                    (position.X != 64 || position.Y == size - 2));

            Assert.That(result, Is.True);
        }

        [Test]
        public void IsReachable_DoesNotInspectWrappedCoordinateAtGridIntegerBoundary()
        {
            var start = new GridPosition(int.MaxValue, 0);
            var wrappedCoordinate = new GridPosition(int.MinValue, 0);
            var inspected = new List<GridPosition>();

            var result = new GridReachabilityEvaluator().IsReachable(
                start,
                wrappedCoordinate,
                position =>
                {
                    inspected.Add(position);
                    return position == start || position == wrappedCoordinate;
                });

            Assert.That(result, Is.False);
            Assert.That(inspected, Has.No.Member(wrappedCoordinate));
        }
    }
}

using System.Collections.Generic;
using AnimalCafe.Navigation;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase11
{
    internal sealed class FakeNavigationDriver : INavigationDriver
    {
        public Vector3 Position { get; set; }
        public NavigationPathState PathState { get; set; } = NavigationPathState.Complete;
        public NavigationFailure BeginFailure { get; set; }
        public NavigationFailure ObservationFailure { get; set; }
        public float PathLength { get; set; } = 1f;
        public float RemainingDistance { get; set; } = 1f;
        public float ActualSpeed { get; set; }
        public float FacingErrorDegrees { get; set; }
        public Vector3? ResolvedTargetPosition { get; set; }
        public int Generation { get; private set; }
        public int LayoutRevision { get; private set; }
        public int StopCount { get; private set; }
        public readonly List<Vector3> BeginPositions = new List<Vector3>();
        public readonly List<NavigationTarget> Targets = new List<NavigationTarget>();
        public bool UseStaleGeneration { get; set; }

        public NavigationFailure BeginPath(NavigationTarget target, int generation, int layoutRevision)
        {
            BeginPositions.Add(Position);
            Targets.Add(target);
            Generation = generation;
            LayoutRevision = layoutRevision;
            return BeginFailure;
        }

        public NavigationObservation Observe() => new NavigationObservation(
            UseStaleGeneration ? Generation - 1 : Generation, LayoutRevision, PathState,
            Position, PathLength, RemainingDistance, ActualSpeed, FacingErrorDegrees,
            ObservationFailure, ResolvedTargetPosition);
        public void Stop() => StopCount++;
    }
}

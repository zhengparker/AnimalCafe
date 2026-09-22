using System;
using System.Collections.Generic;

namespace AnimalCafe.Layout
{
    public enum LayoutStationType
    {
        CashRegister,
        CoffeeMachine,
        PickUpPoint
    }

    public sealed class StationReadiness
    {
        public LayoutStationType FunctionType { get; }
        public string InstanceId { get; }
        public string SupportFurnitureInstanceId { get; }
        public string SurfaceSlotId { get; }
        public bool IsValid { get; }
        public ResolvedStationAnchors Anchors { get; }
        public IReadOnlyList<int> ReachableComponentIds { get; }
        public IReadOnlyList<LayoutReadinessFailure> Failures { get; }

        internal StationReadiness(
            LayoutStationType functionType,
            string instanceId,
            string supportFurnitureInstanceId,
            string surfaceSlotId,
            ResolvedStationAnchors anchors,
            IEnumerable<int> reachableComponentIds,
            IEnumerable<LayoutReadinessFailure> failures)
        {
            FunctionType = functionType;
            InstanceId = instanceId;
            SupportFurnitureInstanceId = supportFurnitureInstanceId;
            SurfaceSlotId = surfaceSlotId;
            Anchors = anchors ?? throw new ArgumentNullException(nameof(anchors));

            var componentSnapshot = new List<int>(
                reachableComponentIds ?? throw new ArgumentNullException(nameof(reachableComponentIds)));
            componentSnapshot.Sort();
            ReachableComponentIds = componentSnapshot.AsReadOnly();

            var failureSnapshot = new List<LayoutReadinessFailure>(
                failures ?? throw new ArgumentNullException(nameof(failures)));
            Failures = failureSnapshot.AsReadOnly();
            IsValid = failureSnapshot.Count == 0;
        }
    }
}

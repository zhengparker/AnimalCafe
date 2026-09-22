namespace AnimalCafe.Layout
{
    public enum LayoutReadinessSeverity
    {
        Warning,
        Blocking
    }

    public enum LayoutReadinessFailureCode
    {
        MissingCashRegister,
        MissingCoffeeMachine,
        MissingPickUpPoint,
        MissingSupportFurniture,
        MissingSurfaceSlot,
        DuplicateSurfaceOccupancy,
        AnchorOutOfBounds,
        AnchorBlocked,
        AnchorUnreachable,
        NoCompleteReachableServiceCombination,
        MissingFunctionalDirection,
        InvalidFunctionalDefinition
    }

    public sealed class LayoutReadinessFailure
    {
        public LayoutReadinessSeverity Severity { get; }
        public LayoutReadinessFailureCode Code { get; }
        public LayoutStationType? FunctionType { get; }
        public string InstanceId { get; }
        public string SupportFurnitureInstanceId { get; }
        public string SurfaceSlotId { get; }
        public InteractionRole? Role { get; }
        public GridPosition? Position { get; }
        public string Message { get; }

        internal LayoutReadinessFailure(
            LayoutReadinessSeverity severity,
            LayoutReadinessFailureCode code,
            LayoutStationType? functionType,
            string instanceId,
            string supportFurnitureInstanceId,
            string surfaceSlotId,
            InteractionRole? role,
            GridPosition? position,
            string message)
        {
            Severity = severity;
            Code = code;
            FunctionType = functionType;
            InstanceId = instanceId;
            SupportFurnitureInstanceId = supportFurnitureInstanceId;
            SurfaceSlotId = surfaceSlotId;
            Role = role;
            Position = position;
            Message = message;
        }
    }
}

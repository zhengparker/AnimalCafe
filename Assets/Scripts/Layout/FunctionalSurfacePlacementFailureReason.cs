namespace AnimalCafe.Layout
{
    public enum FunctionalSurfacePlacementFailureReason
    {
        None = 0,
        MissingDefinition = 1,
        UnsupportedFunction = 2,
        UnsupportedPlacementSurface = 3,
        MissingSupportFurniture = 4,
        MissingSurfaceSlot = 5,
        SlotOccupied = 6,
        InstanceAlreadyPlaced = 7,
        InstanceNotFound = 8,
        InvalidRotation = 9,
        InvalidInstance = 10,
        InvalidSurfaceSlotAddress = 11,
        UnsupportedAction = 12,
        NoValidInteractionAnchor = 13
    }
}

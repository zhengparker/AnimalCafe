namespace AnimalCafe.Capacity
{
    public enum CapacityFailureReason
    {
        None,
        InvalidOwnerId,
        InvalidKinds,
        OwnerAlreadyReserved,
        CapacityOverLimit,
        InsufficientCapacity,
        TokenIdExhausted,
        InvalidToken,
        WrongOwner,
        InvalidTransition,
        InvalidFloorCellCount
    }
}

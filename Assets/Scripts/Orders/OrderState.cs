namespace AnimalCafe.Orders
{
    public enum OrderState
    {
        Waiting,
        Claimed,
        Preparing,
        ReadyForPickup,
        Completed,
        Failed
    }
}

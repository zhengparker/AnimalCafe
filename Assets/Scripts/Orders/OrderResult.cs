namespace AnimalCafe.Orders
{
    public sealed class OrderResult
    {
        public bool Succeeded { get; }
        public OrderOperationFailureReason FailureReason { get; }
        public OrderSnapshot Order { get; }

        internal OrderResult(bool succeeded, OrderOperationFailureReason failureReason, OrderSnapshot order)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            Order = order;
        }
    }
}

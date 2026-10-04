namespace AnimalCafe.Orders
{
    public sealed class OrderSnapshot
    {
        public long OrderId { get; }
        public string CustomerId { get; }
        public string ProductId { get; }
        public string ClaimantId { get; }
        public OrderState State { get; }
        public string TerminationReason { get; }

        internal OrderSnapshot(long orderId, string customerId, string productId, string claimantId,
            OrderState state, string terminationReason)
        {
            OrderId = orderId;
            CustomerId = customerId;
            ProductId = productId;
            ClaimantId = claimantId;
            State = state;
            TerminationReason = terminationReason;
        }
    }
}

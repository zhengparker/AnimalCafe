namespace AnimalCafe.Orders
{
    public enum OrderOperationFailureReason
    {
        None,
        InvalidCustomerId,
        InvalidProductId,
        InvalidEmployeeId,
        InvalidOrderId,
        OrderNotFound,
        NoWaitingOrders,
        EmployeeBusy,
        NotClaimOwner,
        WrongCustomer,
        InvalidTransition,
        OrderTerminal,
        InvalidFailureReason,
        IdExhausted
    }
}

namespace AnimalCafe.Capacity
{
    public sealed class ReservationSnapshot
    {
        public CapacityToken Token { get; }
        public string OwnerId { get; }
        public CapacityKind Kind { get; }
        public ReservationState State { get; }

        internal ReservationSnapshot(CapacityToken token, string ownerId,
            CapacityKind kind, ReservationState state)
        {
            Token = token;
            OwnerId = ownerId;
            Kind = kind;
            State = state;
        }
    }
}

namespace AnimalCafe.Capacity
{
    public sealed class CapacityToken
    {
        public long Id { get; }
        internal CapacityService Issuer { get; }

        internal CapacityToken(long id, CapacityService issuer)
        {
            Id = id;
            Issuer = issuer;
        }
    }
}

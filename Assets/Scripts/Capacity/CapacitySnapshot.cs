namespace AnimalCafe.Capacity
{
    public sealed class CapacitySnapshot
    {
        public CapacityKind Kind { get; }
        public int Limit { get; }
        public int Reserved { get; }
        public int Occupied { get; }
        public int Used { get; }
        public int Available { get; }
        public int OverCapacity { get; }

        internal CapacitySnapshot(CapacityKind kind, int limit, int reserved, int occupied)
        {
            Kind = kind;
            Limit = limit;
            Reserved = reserved;
            Occupied = occupied;
            Used = reserved + occupied;
            Available = System.Math.Max(0, limit - Used);
            OverCapacity = System.Math.Max(0, Used - limit);
        }
    }
}

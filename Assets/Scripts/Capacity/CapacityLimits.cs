namespace AnimalCafe.Capacity
{
    public sealed class CapacityLimits
    {
        public int FloorCellCount { get; }
        public int TotalCustomers { get; }
        public int CounterQueue { get; }
        public int PickUp { get; }

        internal CapacityLimits(int floorCellCount, int totalCustomers,
            int counterQueue, int pickUp)
        {
            FloorCellCount = floorCellCount;
            TotalCustomers = totalCustomers;
            CounterQueue = counterQueue;
            PickUp = pickUp;
        }
    }
}

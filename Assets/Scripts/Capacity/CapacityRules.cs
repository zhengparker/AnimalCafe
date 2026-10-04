using System;

namespace AnimalCafe.Capacity
{
    public sealed class CapacityRules
    {
        public int CellsPerCustomer { get; }
        public int CounterPercent { get; }

        public CapacityRules(int cellsPerCustomer = 4, int counterPercent = 50)
        {
            if (cellsPerCustomer <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cellsPerCustomer));
            }

            if (counterPercent < 1 || counterPercent > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(counterPercent));
            }

            CellsPerCustomer = cellsPerCustomer;
            CounterPercent = counterPercent;
        }

        public CapacityLimits CalculateLimits(int floorCellCount)
        {
            if (floorCellCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(floorCellCount));
            }

            var total = floorCellCount / CellsPerCustomer;
            // 先转 long 再相乘，避免最大合法 Floor 格数造成整数溢出。
            var counter = (int)(((long)total * CounterPercent + 99L) / 100L);
            return new CapacityLimits(floorCellCount, total, counter, total);
        }
    }
}

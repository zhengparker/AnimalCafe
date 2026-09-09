namespace AnimalCafe.Layout
{
    public sealed class LayoutReadinessSummary
    {
        public int TotalCount { get; }
        public int ValidCount { get; }
        public int InvalidCount { get; }

        internal LayoutReadinessSummary(
            int totalCount,
            int validCount)
        {
            TotalCount = totalCount;
            ValidCount = validCount;
            InvalidCount = totalCount - validCount;
        }
    }
}

using System;

namespace AnimalCafe.Layout
{
    public static class StableId
    {
        public static string NewFurnitureInstanceId()
        {
            return Guid.NewGuid().ToString("N");
        }

        public static bool IsValidFurnitureInstanceId(string value)
        {
            return value != null &&
                   string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal) &&
                   Guid.TryParseExact(value, "N", out _);
        }
    }
}

using System;

namespace AnimalCafe.Layout
{
    public static class StableId
    {
        public static string NewFurnitureInstanceId()
        {
            return NewStableId();
        }

        public static string NewSurfaceMountedInstanceId()
        {
            return NewStableId();
        }

        public static string NewPickUpPointInstanceId()
        {
            return NewStableId();
        }

        public static bool IsValidFurnitureInstanceId(string value)
        {
            return value != null &&
                   string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal) &&
                   Guid.TryParseExact(value, "N", out _);
        }

        private static string NewStableId()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}

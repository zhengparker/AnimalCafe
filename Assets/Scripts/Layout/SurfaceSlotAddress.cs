using System;

namespace AnimalCafe.Layout
{
    public readonly struct SurfaceSlotAddress : IEquatable<SurfaceSlotAddress>
    {
        public string SupportFurnitureInstanceId { get; }
        public string SlotId { get; }

        public SurfaceSlotAddress(string supportFurnitureInstanceId, string slotId)
        {
            if (supportFurnitureInstanceId == null)
            {
                throw new ArgumentNullException(nameof(supportFurnitureInstanceId));
            }

            if (!StableId.IsValidFurnitureInstanceId(supportFurnitureInstanceId))
            {
                throw new ArgumentException(
                    "Support furniture instance ID has an invalid format.",
                    nameof(supportFurnitureInstanceId));
            }

            LayoutStableId.Validate(slotId, nameof(slotId));
            SupportFurnitureInstanceId = supportFurnitureInstanceId;
            SlotId = slotId;
        }

        internal static void Validate(SurfaceSlotAddress address, string paramName)
        {
            if (!StableId.IsValidFurnitureInstanceId(address.SupportFurnitureInstanceId) ||
                !LayoutStableId.IsValid(address.SlotId))
            {
                throw new ArgumentException(
                    "Surface slot address has invalid stable IDs.",
                    paramName);
            }
        }

        public bool Equals(SurfaceSlotAddress other)
        {
            return string.Equals(
                       SupportFurnitureInstanceId,
                       other.SupportFurnitureInstanceId,
                       StringComparison.Ordinal) &&
                   string.Equals(SlotId, other.SlotId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is SurfaceSlotAddress other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((SupportFurnitureInstanceId != null
                            ? StringComparer.Ordinal.GetHashCode(SupportFurnitureInstanceId)
                            : 0) * 397) ^
                       (SlotId != null ? StringComparer.Ordinal.GetHashCode(SlotId) : 0);
            }
        }

        public static bool operator ==(SurfaceSlotAddress left, SurfaceSlotAddress right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SurfaceSlotAddress left, SurfaceSlotAddress right)
        {
            return !left.Equals(right);
        }
    }
}

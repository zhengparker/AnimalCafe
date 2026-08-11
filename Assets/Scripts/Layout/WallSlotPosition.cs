using System;

namespace AnimalCafe.Layout
{
    public readonly struct WallSlotPosition : IEquatable<WallSlotPosition>
    {
        public int Column { get; }
        public int Row { get; }

        public WallSlotPosition(int column, int row)
        {
            Column = column;
            Row = row;
        }

        public bool Equals(WallSlotPosition other)
        {
            return Column == other.Column && Row == other.Row;
        }

        public override bool Equals(object obj)
        {
            return obj is WallSlotPosition other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Column * 397) ^ Row;
            }
        }

        public static bool operator ==(WallSlotPosition left, WallSlotPosition right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WallSlotPosition left, WallSlotPosition right)
        {
            return !left.Equals(right);
        }
    }
}

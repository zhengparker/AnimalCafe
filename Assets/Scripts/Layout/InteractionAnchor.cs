using System;

namespace AnimalCafe.Layout
{
    public readonly struct InteractionAnchor : IEquatable<InteractionAnchor>
    {
        public InteractionRole Role { get; }
        public GridPosition Position { get; }
        public CardinalDirection Facing { get; }

        public InteractionAnchor(
            InteractionRole role,
            GridPosition position,
            CardinalDirection facing)
        {
            if (!Enum.IsDefined(typeof(InteractionRole), role))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(role),
                    role,
                    "Interaction role must be a defined value.");
            }

            if (!Enum.IsDefined(typeof(CardinalDirection), facing))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(facing),
                    facing,
                    "Anchor facing must be a defined cardinal direction.");
            }

            Role = role;
            Position = position;
            Facing = facing;
        }

        public bool Equals(InteractionAnchor other)
        {
            return Role == other.Role &&
                   Position == other.Position &&
                   Facing == other.Facing;
        }

        public override bool Equals(object obj)
        {
            return obj is InteractionAnchor other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (((int)Role * 397) ^ Position.GetHashCode()) * 397 ^ (int)Facing;
            }
        }

        public static bool operator ==(InteractionAnchor left, InteractionAnchor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(InteractionAnchor left, InteractionAnchor right)
        {
            return !left.Equals(right);
        }
    }
}

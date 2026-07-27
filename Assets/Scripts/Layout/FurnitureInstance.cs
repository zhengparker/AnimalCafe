using System;

namespace AnimalCafe.Layout
{
    public sealed class FurnitureInstance
    {
        public string InstanceId { get; }
        public string DefinitionId { get; }
        public GridPosition Position { get; }
        public FurnitureRotation Rotation { get; }

        private FurnitureInstance(
            string instanceId,
            string definitionId,
            GridPosition position,
            FurnitureRotation rotation)
        {
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Position = position;
            Rotation = rotation;
        }

        public static FurnitureInstance CreateNew(
            string definitionId,
            GridPosition position,
            FurnitureRotation rotation)
        {
            FurnitureDefinition.ValidateDefinitionId(definitionId, nameof(definitionId));
            ValidateRotation(rotation);

            return new FurnitureInstance(
                StableId.NewFurnitureInstanceId(),
                definitionId,
                position,
                rotation);
        }

        public static FurnitureInstance Restore(
            string instanceId,
            string definitionId,
            GridPosition position,
            FurnitureRotation rotation)
        {
            ValidateInstanceId(instanceId);
            FurnitureDefinition.ValidateDefinitionId(definitionId, nameof(definitionId));
            ValidateRotation(rotation);

            return new FurnitureInstance(instanceId, definitionId, position, rotation);
        }

        private static void ValidateInstanceId(string instanceId)
        {
            if (instanceId == null)
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            if (!StableId.IsValidFurnitureInstanceId(instanceId))
            {
                throw new ArgumentException("Instance ID has an invalid format.", nameof(instanceId));
            }
        }

        private static void ValidateRotation(FurnitureRotation rotation)
        {
            switch (rotation)
            {
                case FurnitureRotation.Degrees0:
                case FurnitureRotation.Degrees90:
                case FurnitureRotation.Degrees180:
                case FurnitureRotation.Degrees270:
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(rotation), rotation, "Rotation must be a known value.");
            }
        }
    }
}

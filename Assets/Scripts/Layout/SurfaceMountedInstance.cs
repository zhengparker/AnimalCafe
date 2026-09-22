using System;

namespace AnimalCafe.Layout
{
    public sealed class SurfaceMountedInstance
    {
        public string InstanceId { get; }
        public string DefinitionId { get; }
        public SurfaceSlotAddress Address { get; }
        public FurnitureRotation Rotation { get; }

        public SurfaceMountedInstance(
            string instanceId,
            string definitionId,
            SurfaceSlotAddress address,
            FurnitureRotation rotation)
        {
            ValidateInstanceId(instanceId);
            FurnitureDefinition.ValidateDefinitionId(definitionId, nameof(definitionId));
            FurnitureInstance.ValidateRotation(rotation);
            SurfaceSlotAddress.Validate(address, nameof(address));

            InstanceId = instanceId;
            DefinitionId = definitionId;
            Address = address;
            Rotation = rotation;
        }

        private static void ValidateInstanceId(string instanceId)
        {
            if (instanceId == null)
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            if (!StableId.IsValidFurnitureInstanceId(instanceId))
            {
                throw new ArgumentException(
                    "Surface-mounted instance ID has an invalid format.",
                    nameof(instanceId));
            }
        }
    }
}

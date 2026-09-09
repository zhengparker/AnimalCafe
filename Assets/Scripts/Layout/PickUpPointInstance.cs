using System;

namespace AnimalCafe.Layout
{
    public sealed class PickUpPointInstance
    {
        public string InstanceId { get; }
        public SurfaceSlotAddress Address { get; }

        public PickUpPointInstance(string instanceId, SurfaceSlotAddress address)
        {
            if (instanceId == null)
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            if (!StableId.IsValidFurnitureInstanceId(instanceId))
            {
                throw new ArgumentException(
                    "Pick-up point instance ID has an invalid format.",
                    nameof(instanceId));
            }

            SurfaceSlotAddress.Validate(address, nameof(address));
            InstanceId = instanceId;
            Address = address;
        }
    }
}

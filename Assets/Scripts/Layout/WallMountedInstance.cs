namespace AnimalCafe.Layout
{
    public sealed class WallMountedInstance
    {
        public string InstanceId { get; }
        public string DefinitionId { get; }
        public string SurfaceId { get; }
        public WallSlotPosition Position { get; }
        public WallFootprint Footprint { get; }

        public WallMountedInstance(
            string instanceId,
            string definitionId,
            string surfaceId,
            WallSlotPosition position,
            WallFootprint footprint)
        {
            ValidateId(instanceId, nameof(instanceId));
            ValidateId(definitionId, nameof(definitionId));
            ValidateId(surfaceId, nameof(surfaceId));

            InstanceId = instanceId;
            DefinitionId = definitionId;
            SurfaceId = surfaceId;
            Position = position;
            Footprint = footprint;
        }

        internal WallMountedInstance WithPosition(WallSlotPosition position)
        {
            return WithPlacement(SurfaceId, position);
        }

        internal WallMountedInstance WithPlacement(
            string surfaceId,
            WallSlotPosition position)
        {
            return new WallMountedInstance(
                InstanceId,
                DefinitionId,
                surfaceId,
                position,
                Footprint);
        }

        internal static void ValidateId(string value, string paramName)
        {
            LayoutStableId.Validate(value, paramName);
        }
    }
}

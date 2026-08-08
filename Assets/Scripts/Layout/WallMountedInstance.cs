using System;
using System.Text.RegularExpressions;

namespace AnimalCafe.Layout
{
    public sealed class WallMountedInstance
    {
        private static readonly Regex StableIdPattern = new Regex(
            "^[a-z0-9][a-z0-9._-]*$",
            RegexOptions.CultureInvariant);

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
            return new WallMountedInstance(
                InstanceId,
                DefinitionId,
                SurfaceId,
                position,
                Footprint);
        }

        internal static void ValidateId(string value, string paramName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(paramName);
            }

            if (string.IsNullOrWhiteSpace(value) ||
                !StableIdPattern.IsMatch(value))
            {
                throw new ArgumentException(
                    "Wall stable ID has an invalid format.",
                    paramName);
            }
        }
    }
}

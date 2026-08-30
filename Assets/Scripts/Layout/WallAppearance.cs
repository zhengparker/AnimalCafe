using System;

namespace AnimalCafe.Layout
{
    public readonly struct WallAppearance
    {
        public string SurfaceId { get; }
        public string BaseStyleId { get; }
        public string WainscotingStyleId { get; }

        public WallAppearance(
            string surfaceId,
            string baseStyleId,
            string wainscotingStyleId)
        {
            WallMountedInstance.ValidateId(surfaceId, nameof(surfaceId));
            WallMountedInstance.ValidateId(baseStyleId, nameof(baseStyleId));

            if (wainscotingStyleId != null)
            {
                WallMountedInstance.ValidateId(
                    wainscotingStyleId,
                    nameof(wainscotingStyleId));
            }

            SurfaceId = surfaceId;
            BaseStyleId = baseStyleId;
            WainscotingStyleId = wainscotingStyleId;
        }
    }
}

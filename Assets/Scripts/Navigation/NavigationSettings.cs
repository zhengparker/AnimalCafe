using System;

namespace AnimalCafe.Navigation
{
    [Serializable]
    public sealed class NavigationSettings
    {
        public float AgentRadius = 0.45f;
        public float CapsuleHeight = 1.30f;
        public float CollisionSkin = 0.01f;
        public float Epsilon = 0.001f;
        public float MaxSpeed = 1.2f;
        public float TurnSpeedDegrees = 360f;
        public float ArrivalDistance = 0.08f;
        public float ArrivalSpeed = 0.05f;
        public float FacingToleranceDegrees = 5f;
        public float TargetSampleDistance = 0.15f;
        public float NoProgressSeconds = 3f;
        public float ProgressDistance = 0.02f;
        public float PathPendingSeconds = 2f;
        public float MinimumSegmentSeconds = 10f;
        public float SegmentLengthMultiplier = 3f;
        public float MaxSubstepSeconds = 1f / 60f;
        public int MaxSubstepsPerFrame = 16;

        public void Validate()
        {
            Positive(AgentRadius, nameof(AgentRadius));
            Positive(CapsuleHeight, nameof(CapsuleHeight));
            Positive(CollisionSkin, nameof(CollisionSkin));
            Positive(Epsilon, nameof(Epsilon));
            Positive(MaxSpeed, nameof(MaxSpeed));
            Positive(TurnSpeedDegrees, nameof(TurnSpeedDegrees));
            Positive(ArrivalDistance, nameof(ArrivalDistance));
            Positive(ArrivalSpeed, nameof(ArrivalSpeed));
            Positive(FacingToleranceDegrees, nameof(FacingToleranceDegrees));
            Positive(TargetSampleDistance, nameof(TargetSampleDistance));
            Positive(NoProgressSeconds, nameof(NoProgressSeconds));
            Positive(ProgressDistance, nameof(ProgressDistance));
            Positive(PathPendingSeconds, nameof(PathPendingSeconds));
            Positive(MinimumSegmentSeconds, nameof(MinimumSegmentSeconds));
            Positive(SegmentLengthMultiplier, nameof(SegmentLengthMultiplier));
            Positive(MaxSubstepSeconds, nameof(MaxSubstepSeconds));
            if (CapsuleHeight < 2 * AgentRadius || CollisionSkin >= AgentRadius ||
                Epsilon >= AgentRadius || ArrivalDistance >= AgentRadius ||
                ArrivalSpeed >= MaxSpeed || FacingToleranceDegrees > 180 ||
                TargetSampleDistance > AgentRadius || ProgressDistance > AgentRadius ||
                MaxSubstepSeconds > 1f / 60f || MaxSubstepsPerFrame < 1 || MaxSubstepsPerFrame > 16)
                throw new ArgumentOutOfRangeException(nameof(NavigationSettings));
        }

        private static void Positive(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}

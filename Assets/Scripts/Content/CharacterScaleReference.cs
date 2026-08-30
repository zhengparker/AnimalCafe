namespace AnimalCafe.Content
{
    /// <summary>
    /// Single shared scale contract for character-relative environment details.
    /// 角色比例的单一共享合同；Task 9 / visual acceptance may revise this one place.
    /// </summary>
    public static class CharacterScaleReference
    {
        public const float CharacterScaleReferenceHeightMeters = 1.30f;
        public const float SharedCharacterWaistHeightMeters =
            CharacterScaleReferenceHeightMeters * 0.5f;

        public static float GetNormalizedWainscotingCutoff(float canonicalWallHeightMeters)
        {
            if (canonicalWallHeightMeters <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(canonicalWallHeightMeters));
            }

            return SharedCharacterWaistHeightMeters / canonicalWallHeightMeters;
        }
    }
}

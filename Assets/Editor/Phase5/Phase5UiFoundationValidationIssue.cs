namespace AnimalCafe.EditorTools.Phase5
{
    /// <summary>
    /// Stable editor validation codes for the Phase 5 UI foundation scene.
    /// Phase 5 UI foundation 场景的稳定 Editor validation code。
    /// </summary>
    public enum Phase5UiFoundationIssueCode
    {
        DuplicateUiRoot,
        DuplicateCanvas,
        DuplicateEventSystem,
        MissingLogicalLayer,
        MissingThemeToken,
        MissingFont,
        TouchTargetBelowMinimum,
        InvalidRaycastPolicy,
        MultipleStrongFrostOwners
    }

    public sealed class Phase5UiFoundationValidationIssue
    {
        public Phase5UiFoundationValidationIssue(
            Phase5UiFoundationIssueCode code,
            string assetPath,
            string objectPath,
            string message)
        {
            Code = code;
            AssetPath = assetPath ?? string.Empty;
            ObjectPath = objectPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public Phase5UiFoundationIssueCode Code { get; }

        public string AssetPath { get; }

        public string ObjectPath { get; }

        public string Message { get; }
    }
}

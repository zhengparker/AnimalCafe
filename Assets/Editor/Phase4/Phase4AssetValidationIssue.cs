using System;

namespace AnimalCafe.EditorTools.Phase4
{
    public sealed class Phase4AssetValidationIssue
    {
        public Phase4AssetValidationIssue(
            Phase4AssetIssueCode code,
            string assetPath,
            string message)
        {
            Code = code;
            AssetPath = assetPath ?? throw new ArgumentNullException(nameof(assetPath));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public Phase4AssetIssueCode Code { get; }

        public string AssetPath { get; }

        public string Message { get; }
    }
}

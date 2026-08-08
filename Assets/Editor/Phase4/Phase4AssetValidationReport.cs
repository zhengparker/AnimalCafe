using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimalCafe.EditorTools.Phase4
{
    public sealed class Phase4AssetValidationReport
    {
        public Phase4AssetValidationReport(
            int validAssetCount,
            int invalidAssetCount,
            IEnumerable<Phase4AssetValidationIssue> issues)
        {
            if (validAssetCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(validAssetCount));
            }

            if (invalidAssetCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(invalidAssetCount));
            }

            ValidAssetCount = validAssetCount;
            InvalidAssetCount = invalidAssetCount;
            var issueSnapshot = (issues ?? throw new ArgumentNullException(nameof(issues)))
                .ToArray();
            Issues = Array.AsReadOnly(issueSnapshot);
        }

        public int ValidAssetCount { get; }

        public int InvalidAssetCount { get; }

        public int AssetCount => ValidAssetCount + InvalidAssetCount;

        public IReadOnlyList<Phase4AssetValidationIssue> Issues { get; }
    }
}

using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.Phase4
{
    public static class Phase4ValidationMenu
    {
        [MenuItem("AnimalCafe/Phase 4/Validate Production Content")]
        public static void ValidateProductionContent()
        {
            var report = Phase4AssetValidator.ValidateAll();
            Debug.Log(FormatSummary(report));
        }

        [MenuItem("AnimalCafe/Phase 4/Build Validation Scene")]
        public static void BuildValidationScene()
        {
            Phase4ValidationSceneSetup.BuildSceneFromMenu();
        }

        public static string FormatSummary(Phase4AssetValidationReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var builder = new StringBuilder();
            builder.Append("[Phase 4 Validation] valid=")
                .Append(report.ValidAssetCount)
                .Append(" invalid=")
                .Append(report.InvalidAssetCount)
                .Append(" issues=")
                .Append(report.Issues.Count);

            foreach (var issue in report.Issues)
            {
                builder.AppendLine()
                    .Append("- [")
                    .Append(issue.Code)
                    .Append("] ")
                    .Append(issue.AssetPath)
                    .Append(": ")
                    .Append(issue.Message);
            }

            return builder.ToString();
        }
    }
}

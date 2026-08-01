using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.AssetPipeline
{
    internal static class BenchmarkAssetValidationMenu
    {
        [MenuItem("AnimalCafe/Validation/Validate Benchmark Assets")]
        private static void ValidateBenchmarkAssets()
        {
            var report = BenchmarkAssetValidator.ValidateAllBenchmarks();
            if (report.IsValid)
            {
                Debug.Log("<color=green>Benchmark asset validation passed: 0 issues.</color>");
                return;
            }

            foreach (var issue in report.Issues)
            {
                Debug.LogError($"{issue.AssetPath}: {issue.Code}");
            }

            var firstInvalidAsset = report.Issues
                .Select(issue => AssetDatabase.LoadMainAssetAtPath(issue.AssetPath))
                .FirstOrDefault(asset => asset != null);
            if (firstInvalidAsset != null)
            {
                Selection.activeObject = firstInvalidAsset;
                EditorGUIUtility.PingObject(firstInvalidAsset);
            }
        }
    }
}

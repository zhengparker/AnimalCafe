using System;

namespace AnimalCafe.EditorTools.AssetPipeline
{
    internal sealed class BenchmarkAssetValidationTarget
    {
        public BenchmarkAssetValidationTarget(BenchmarkAssetKind kind, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException("An asset path is required.", nameof(assetPath));
            }

            Kind = kind;
            AssetPath = assetPath;
        }

        public BenchmarkAssetKind Kind { get; }

        public string AssetPath { get; }
    }
}

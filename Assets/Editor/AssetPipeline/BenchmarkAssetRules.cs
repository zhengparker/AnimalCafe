using System;
using UnityEngine;

namespace AnimalCafe.EditorTools.AssetPipeline
{
    public sealed class BenchmarkAssetRules
    {
        public BenchmarkAssetRules(
            Vector3 targetSize,
            float boundsTolerance,
            int maxLod0Triangles,
            int maxLod1Triangles,
            float maxLod1TriangleRatio,
            int maxMaterialSlots,
            int maxColliders,
            bool requiresLodGroup)
        {
            TargetSize = targetSize;
            BoundsTolerance = boundsTolerance;
            MaxLod0Triangles = maxLod0Triangles;
            MaxLod1Triangles = maxLod1Triangles;
            MaxLod1TriangleRatio = maxLod1TriangleRatio;
            MaxMaterialSlots = maxMaterialSlots;
            MaxColliders = maxColliders;
            RequiresLodGroup = requiresLodGroup;
        }

        public Vector3 TargetSize { get; }

        public float BoundsTolerance { get; }

        public int MaxLod0Triangles { get; }

        public int MaxLod1Triangles { get; }

        public float MaxLod1TriangleRatio { get; }

        public int MaxMaterialSlots { get; }

        public int MaxColliders { get; }

        public bool RequiresLodGroup { get; }

        public static BenchmarkAssetRules For(BenchmarkAssetKind kind)
        {
            switch (kind)
            {
                case BenchmarkAssetKind.WorkTable:
                    return new BenchmarkAssetRules(
                        new Vector3(0.90f, 0.65f, 0.90f),
                        0.05f,
                        1500,
                        0,
                        0f,
                        2,
                        3,
                        false);
                case BenchmarkAssetKind.CoffeeMachine:
                    return new BenchmarkAssetRules(
                        new Vector3(0.65f, 0.62f, 0.50f),
                        0.10f,
                        5000,
                        2500,
                        0.60f,
                        3,
                        2,
                        true);
                case BenchmarkAssetKind.CeramicCup:
                    return new BenchmarkAssetRules(
                        new Vector3(0.14f, 0.16f, 0.14f),
                        0.10f,
                        800,
                        0,
                        0f,
                        1,
                        1,
                        false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }
}

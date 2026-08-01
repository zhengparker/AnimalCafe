using System;
using System.Collections;
using System.Collections.Generic;

namespace AnimalCafe.EditorTools.AssetPipeline
{
    public sealed class BenchmarkAssetValidationReport
    {
        private readonly ValidationIssueCollection issues;

        public BenchmarkAssetValidationReport(
            IEnumerable<BenchmarkAssetValidationIssue> issues)
            : this(issues, 0, 0)
        {
        }

        public BenchmarkAssetValidationReport(
            IEnumerable<BenchmarkAssetValidationIssue> issues,
            int materialSlotCount,
            int uniqueSharedMaterialCount)
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            if (materialSlotCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(materialSlotCount));
            }

            if (uniqueSharedMaterialCount < 0 || uniqueSharedMaterialCount > materialSlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(uniqueSharedMaterialCount));
            }

            this.issues = new ValidationIssueCollection(issues);
            MaterialSlotCount = materialSlotCount;
            UniqueSharedMaterialCount = uniqueSharedMaterialCount;
        }

        public IReadOnlyList<BenchmarkAssetValidationIssue> Issues => issues;

        public int MaterialSlotCount { get; }

        public int UniqueSharedMaterialCount { get; }

        public bool IsValid => Issues.Count == 0;

        private sealed class ValidationIssueCollection : IReadOnlyList<BenchmarkAssetValidationIssue>
        {
            private readonly List<BenchmarkAssetValidationIssue> snapshot;

            public ValidationIssueCollection(
                IEnumerable<BenchmarkAssetValidationIssue> source)
            {
                snapshot = new List<BenchmarkAssetValidationIssue>(source);
            }

            public int Count => snapshot.Count;

            public BenchmarkAssetValidationIssue this[int index] => snapshot[index];

            public IEnumerator<BenchmarkAssetValidationIssue> GetEnumerator()
            {
                return snapshot.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}

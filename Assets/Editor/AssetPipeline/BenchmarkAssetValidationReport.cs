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
        {
            if (issues == null)
            {
                throw new ArgumentNullException(nameof(issues));
            }

            this.issues = new ValidationIssueCollection(issues);
        }

        public IReadOnlyList<BenchmarkAssetValidationIssue> Issues => issues;

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

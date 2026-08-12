using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AnimalCafe.EditorTools.Phase5
{
    public sealed class Phase5UiFoundationValidationReport
    {
        public Phase5UiFoundationValidationReport(IEnumerable<Phase5UiFoundationValidationIssue> issues)
        {
            if (issues == null) throw new ArgumentNullException(nameof(issues));
            Issues = new ReadOnlyCollection<Phase5UiFoundationValidationIssue>(
                new List<Phase5UiFoundationValidationIssue>(issues));
        }

        public IReadOnlyList<Phase5UiFoundationValidationIssue> Issues { get; }

        public bool IsValid => Issues.Count == 0;
    }
}

using System.Collections.Generic;
using AnimalCafe.EditorTools.AssetPipeline;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.AssetPipeline
{
    public sealed class BenchmarkAssetValidatorContractTests
    {
        [TearDown]
        public void TearDown()
        {
            BenchmarkAssetTestFactory.DeleteGeneratedAssets();
        }

        [Test]
        public void ValidationReport_WithNoIssuesIsValidAndDoesNotExposeMutableState()
        {
            var source = new List<BenchmarkAssetValidationIssue>();
            var report = new BenchmarkAssetValidationReport(source);

            source.Add(new BenchmarkAssetValidationIssue(
                BenchmarkAssetIssueCode.InvalidName,
                "Assets/Invalid.prefab",
                "Invalid name."));

            Assert.That(report.IsValid, Is.True);
            Assert.That(report.Issues, Is.Empty);
            Assert.That(
                report.Issues,
                Is.Not.InstanceOf<IList<BenchmarkAssetValidationIssue>>());
        }
    }
}

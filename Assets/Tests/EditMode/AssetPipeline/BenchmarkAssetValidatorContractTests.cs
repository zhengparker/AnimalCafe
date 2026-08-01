using System;
using System.Collections.Generic;
using AnimalCafe.EditorTools.AssetPipeline;
using NUnit.Framework;
using UnityEditor;

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

        [Test]
        public void CreatePrefab_WithPathSeparatorRejectsNameBeforeCreatingAssets()
        {
            BenchmarkAssetTestFactory.DeleteGeneratedAssets();

            var exception = Assert.Throws<ArgumentException>(() =>
                BenchmarkAssetTestFactory.CreatePrefab(
                    "invalid/name",
                    new UnityEngine.Vector3(1f, 1f, 1f),
                    1));

            StringAssert.Contains("plain filename", exception.Message);
            Assert.That(
                AssetDatabase.IsValidFolder(BenchmarkAssetTestFactory.GeneratedFolderPath),
                Is.False);
        }
    }
}

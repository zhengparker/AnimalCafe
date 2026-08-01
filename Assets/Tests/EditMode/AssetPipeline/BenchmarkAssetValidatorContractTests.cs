using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.EditorTools.AssetPipeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.AssetPipeline
{
    public sealed class BenchmarkAssetValidatorContractTests
    {
        private const string WorkTablePrefabName = "PF_Benchmark_WorkTable_01";

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

        [Test]
        public void CreatePrefabAtPath_DeleteGeneratedAssetsRemovesTemporaryBenchmarkFixture()
        {
            var path = CreateWorkTablePrefab();

            BenchmarkAssetTestFactory.DeleteGeneratedAssets();

            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Null);
            Assert.That(
                AssetDatabase.IsValidFolder(BenchmarkAssetTestFactory.BenchmarkPrefabFolderPath),
                Is.False);
            Assert.That(AssetDatabase.IsValidFolder("Assets/Tests/Generated"), Is.False);
        }

        [Test]
        public void ValidatePrefab_ApprovedWorkTableReturnsNoStructuralIssues()
        {
            var path = CreateWorkTablePrefab();

            Assert.That(
                BenchmarkAssetValidator.ValidatePrefab(path, BenchmarkAssetKind.WorkTable).Issues,
                Is.Empty);
        }

        [Test]
        public void ValidatePrefab_PathOutsideBenchmarkFolderReportsInvalidAssetPath()
        {
            var prefab = BenchmarkAssetTestFactory.CreatePrefab(
                WorkTablePrefabName,
                new Vector3(0.90f, 0.65f, 0.90f),
                1);
            var path = AssetDatabase.GetAssetPath(prefab);

            AssertCodes(path, BenchmarkAssetIssueCode.InvalidAssetPath);
        }

        [Test]
        public void ValidatePrefab_NameWithSpacesReportsInvalidName()
        {
            var path = CreatePrefabAtBenchmarkPath(
                "PF_Benchmark_Work Table_01",
                new Vector3(0.90f, 0.65f, 0.90f));

            AssertCodes(path, BenchmarkAssetIssueCode.InvalidName);
        }

        [Test]
        public void ValidatePrefab_WrongPrefixReportsInvalidName()
        {
            var path = CreatePrefabAtBenchmarkPath(
                "WorkTable_01",
                new Vector3(0.90f, 0.65f, 0.90f));

            AssertCodes(path, BenchmarkAssetIssueCode.InvalidName);
        }

        [Test]
        public void ValidatePrefab_NonAsciiNameReportsInvalidName()
        {
            var path = CreatePrefabAtBenchmarkPath(
                "PF_Benchmark_WorkTable_é_01",
                new Vector3(0.90f, 0.65f, 0.90f));

            AssertCodes(path, BenchmarkAssetIssueCode.InvalidName);
        }

        [Test]
        public void ValidatePrefab_RootScaleUsedForCorrectionReportsRootTransformNotIdentity()
        {
            var path = CreateWorkTablePrefab(root => root.transform.localScale = new Vector3(1.1f, 1f, 1f));

            AssertCodes(path, BenchmarkAssetIssueCode.RootTransformNotIdentity);
        }

        [Test]
        public void ValidatePrefab_RootRotationUsedForCorrectionReportsRootTransformNotIdentity()
        {
            var path = CreateWorkTablePrefab(root => root.transform.localRotation = Quaternion.Euler(0f, 15f, 0f));

            AssertCodes(path, BenchmarkAssetIssueCode.RootTransformNotIdentity);
        }

        [Test]
        public void ValidatePrefab_BoundsInsideTolerancePass()
        {
            var path = CreateWorkTablePrefab(
                bounds: new Vector3(0.944f, 0.682f, 0.856f));

            Assert.That(
                BenchmarkAssetValidator.ValidatePrefab(path, BenchmarkAssetKind.WorkTable).Issues,
                Is.Empty);
        }

        [Test]
        public void ValidatePrefab_WidthAboveToleranceReportsBoundsOutsideTolerance()
        {
            var path = CreateWorkTablePrefab(bounds: new Vector3(0.946f, 0.65f, 0.90f));

            AssertCodes(path, BenchmarkAssetIssueCode.BoundsOutsideTolerance);
        }

        [Test]
        public void ValidatePrefab_HeightBelowToleranceReportsBoundsOutsideTolerance()
        {
            var path = CreateWorkTablePrefab(bounds: new Vector3(0.90f, 0.617f, 0.90f));

            AssertCodes(path, BenchmarkAssetIssueCode.BoundsOutsideTolerance);
        }

        [Test]
        public void ValidatePrefab_VisibleBoundsBelowZeroReportsBelowGround()
        {
            var path = CreateWorkTablePrefab(root => root.transform.Find("Visual").localPosition = new Vector3(0f, -0.006f, 0f));

            AssertCodes(path, BenchmarkAssetIssueCode.BelowGround);
        }

        [Test]
        public void ValidatePrefab_MissingForwardMarkerReportsInvalidForwardMarker()
        {
            var path = CreateWorkTablePrefab(root => UnityEngine.Object.DestroyImmediate(root.transform.Find("ForwardMarker").gameObject));

            AssertCodes(path, BenchmarkAssetIssueCode.InvalidForwardMarker);
        }

        [Test]
        public void ValidatePrefab_ForwardMarkerBehindOriginReportsInvalidForwardMarker()
        {
            var path = CreateWorkTablePrefab(root => root.transform.Find("ForwardMarker").localPosition = new Vector3(0f, 0.05f, -0.01f));

            AssertCodes(path, BenchmarkAssetIssueCode.InvalidForwardMarker);
        }

        [Test]
        public void ValidatePrefab_ForwardMarkerRotatedAwayFromPositiveZReportsInvalidForwardMarker()
        {
            var path = CreateWorkTablePrefab(root => root.transform.Find("ForwardMarker").localRotation = Quaternion.Euler(0f, 2f, 0f));

            AssertCodes(path, BenchmarkAssetIssueCode.InvalidForwardMarker);
        }

        [Test]
        public void ValidatePrefab_MultipleBreaksReportsEveryIssue()
        {
            var path = CreatePrefabAtBenchmarkPath(
                "Wrong WorkTable",
                new Vector3(1f, 0.65f, 0.90f),
                root =>
                {
                    root.transform.localScale = new Vector3(1.1f, 1f, 1f);
                    root.transform.Find("Visual").localPosition = new Vector3(0f, -0.006f, 0f);
                    root.transform.Find("ForwardMarker").localPosition = new Vector3(0f, 0.05f, -0.01f);
                });

            AssertCodes(
                path,
                BenchmarkAssetIssueCode.InvalidName,
                BenchmarkAssetIssueCode.RootTransformNotIdentity,
                BenchmarkAssetIssueCode.BoundsOutsideTolerance,
                BenchmarkAssetIssueCode.BelowGround,
                BenchmarkAssetIssueCode.InvalidForwardMarker);
        }

        private static string CreateWorkTablePrefab(
            Action<GameObject> configure = null,
            Vector3? bounds = null)
        {
            return CreatePrefabAtBenchmarkPath(
                WorkTablePrefabName,
                bounds ?? new Vector3(0.90f, 0.65f, 0.90f),
                configure);
        }

        private static string CreatePrefabAtBenchmarkPath(
            string prefabName,
            Vector3 bounds,
            Action<GameObject> configure = null)
        {
            var assetPath = $"{BenchmarkAssetTestFactory.BenchmarkPrefabFolderPath}/{prefabName}.prefab";
            BenchmarkAssetTestFactory.CreatePrefabAtPath(assetPath, bounds, 1, configure);
            return assetPath;
        }

        private static void AssertCodes(string path, params BenchmarkAssetIssueCode[] expectedCodes)
        {
            var report = BenchmarkAssetValidator.ValidatePrefab(path, BenchmarkAssetKind.WorkTable);
            Assert.That(report.Issues.Select(issue => issue.Code), Is.EquivalentTo(expectedCodes));
        }
    }
}

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
        private BenchmarkAssetTestFactory fixture;

        [SetUp]
        public void SetUp()
        {
            fixture = new BenchmarkAssetTestFactory();
        }

        [TearDown]
        public void TearDown()
        {
            fixture.Dispose();
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
            fixture.DeleteGeneratedAssets();

            var exception = Assert.Throws<ArgumentException>(() =>
                fixture.CreatePrefab(
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

            fixture.DeleteGeneratedAssets();

            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.Null);
            Assert.That(
                AssetDatabase.IsValidFolder(BenchmarkAssetTestFactory.BenchmarkPrefabFolderPath),
                Is.False);
            Assert.That(AssetDatabase.IsValidFolder("Assets/Tests/Generated"), Is.False);
            Assert.That(AssetDatabase.IsValidFolder("Assets/Art/VisualPipeline/Benchmarks"), Is.False);
            Assert.That(AssetDatabase.IsValidFolder("Assets/Art/VisualPipeline"), Is.False);
        }

        [Test]
        public void CreatePrefabAtPath_ExistingBenchmarkPrefabFailsBeforeChangingOriginalPrefab()
        {
            var path = CreateWorkTablePrefab();
            var originalGuid = AssetDatabase.AssetPathToGUID(path);
            var originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            Assert.Throws<InvalidOperationException>(() =>
                fixture.CreatePrefabAtPath(
                    path,
                    new Vector3(1f, 1f, 1f),
                    1));

            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(path), Is.SameAs(originalPrefab));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(originalGuid));
        }

        [Test]
        public void DeleteGeneratedAssets_PreExistingEmptyBenchmarkParentRemainsUntouched()
        {
            const string userFolderPath = "Assets/Art/VisualPipeline";
            AssetDatabase.CreateFolder("Assets/Art", "VisualPipeline");
            var originalGuid = AssetDatabase.AssetPathToGUID(userFolderPath);
            try
            {
                CreateWorkTablePrefab();

                fixture.DeleteGeneratedAssets();

                Assert.That(AssetDatabase.IsValidFolder(userFolderPath), Is.True);
                Assert.That(AssetDatabase.AssetPathToGUID(userFolderPath), Is.EqualTo(originalGuid));
            }
            finally
            {
                if (AssetDatabase.IsValidFolder(userFolderPath))
                {
                    AssetDatabase.DeleteAsset(userFolderPath);
                }
            }
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
            var prefab = fixture.CreatePrefab(
                WorkTablePrefabName,
                new Vector3(0.90f, 0.65f, 0.90f),
                1);
            var path = AssetDatabase.GetAssetPath(prefab);

            AssertCodes(path, BenchmarkAssetIssueCode.InvalidAssetPath);
        }

        [Test]
        public void ValidatePrefab_PathWithBackslashesReportsInvalidAssetPath()
        {
            var path = CreateWorkTablePrefab().Replace('/', '\\');

            AssertCodes(path, BenchmarkAssetIssueCode.InvalidAssetPath);
        }

        [Test]
        public void ValidatePrefab_NameWithSpacesReportsInvalidName()
        {
            var path = CreatePrefabAtBenchmarkPath(
                "PF_Benchmark_Work Table_01",
                new Vector3(0.90f, 0.65f, 0.90f));

            AssertCodes(path, BenchmarkAssetIssueCode.InvalidAssetPath, BenchmarkAssetIssueCode.InvalidName);
        }

        [Test]
        public void ValidatePrefab_WrongPrefixReportsInvalidName()
        {
            var path = CreatePrefabAtBenchmarkPath(
                "WorkTable_01",
                new Vector3(0.90f, 0.65f, 0.90f));

            AssertCodes(path, BenchmarkAssetIssueCode.InvalidAssetPath, BenchmarkAssetIssueCode.InvalidName);
        }

        [Test]
        public void ValidatePrefab_NonAsciiNameReportsInvalidName()
        {
            var path = CreatePrefabAtBenchmarkPath(
                "PF_Benchmark_WorkTable_é_01",
                new Vector3(0.90f, 0.65f, 0.90f));

            AssertCodes(path, BenchmarkAssetIssueCode.InvalidAssetPath, BenchmarkAssetIssueCode.InvalidName);
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
        public void ValidatePrefab_NestedRotatedAndScaledRendererUsesRootLocalBounds()
        {
            var path = CreateWorkTablePrefab(root =>
            {
                var visual = root.transform.Find("Visual");
                var container = new GameObject("VisualContainer");
                container.transform.SetParent(root.transform, false);
                container.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                container.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                visual.SetParent(container.transform, false);
                visual.localScale = new Vector3(2f, 2f, 2f);
            });

            Assert.That(
                BenchmarkAssetValidator.ValidatePrefab(path, BenchmarkAssetKind.WorkTable).Issues,
                Is.Empty);
        }

        [Test]
        public void ValidatePrefab_DisabledRendererIsExcludedFromVisibleBounds()
        {
            var path = CreateWorkTablePrefab(root =>
            {
                var disabledVisual = UnityEngine.Object.Instantiate(root.transform.Find("Visual").gameObject);
                disabledVisual.name = "DisabledVisual";
                disabledVisual.transform.SetParent(root.transform, false);
                disabledVisual.transform.localPosition = new Vector3(10f, 0f, 0f);
                disabledVisual.GetComponent<Renderer>().enabled = false;
            });

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
        public void ValidatePrefab_DuplicateForwardMarkerReportsInvalidForwardMarker()
        {
            var path = CreateWorkTablePrefab(root =>
            {
                var duplicateMarker = new GameObject("ForwardMarker");
                duplicateMarker.transform.SetParent(root.transform, false);
                duplicateMarker.transform.localPosition = new Vector3(0f, 0.05f, 0.30f);
            });

            AssertCodes(path, BenchmarkAssetIssueCode.InvalidForwardMarker);
        }

        [Test]
        public void ValidatePrefab_ForwardMarkerWithRendererReportsInvalidForwardMarker()
        {
            var path = CreateWorkTablePrefab(root => root.transform.Find("ForwardMarker").gameObject.AddComponent<MeshRenderer>());

            AssertCodes(path, BenchmarkAssetIssueCode.InvalidForwardMarker);
        }

        [Test]
        public void ValidatePrefab_ForwardMarkerWithMeshFilterReportsInvalidForwardMarker()
        {
            var path = CreateWorkTablePrefab(root => root.transform.Find("ForwardMarker").gameObject.AddComponent<MeshFilter>());

            AssertCodes(path, BenchmarkAssetIssueCode.InvalidForwardMarker);
        }

        [Test]
        public void ValidatePrefab_ForwardMarkerWithColliderReportsInvalidForwardMarker()
        {
            var path = CreateWorkTablePrefab(root => root.transform.Find("ForwardMarker").gameObject.AddComponent<BoxCollider>());

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
                BenchmarkAssetIssueCode.InvalidAssetPath,
                BenchmarkAssetIssueCode.InvalidName,
                BenchmarkAssetIssueCode.RootTransformNotIdentity,
                BenchmarkAssetIssueCode.BoundsOutsideTolerance,
                BenchmarkAssetIssueCode.BelowGround,
                BenchmarkAssetIssueCode.InvalidForwardMarker);
        }

        private string CreateWorkTablePrefab(
            Action<GameObject> configure = null,
            Vector3? bounds = null)
        {
            return CreatePrefabAtBenchmarkPath(
                WorkTablePrefabName,
                bounds ?? new Vector3(0.90f, 0.65f, 0.90f),
                configure);
        }

        private string CreatePrefabAtBenchmarkPath(
            string prefabName,
            Vector3 bounds,
            Action<GameObject> configure = null)
        {
            var assetPath = $"{BenchmarkAssetTestFactory.BenchmarkPrefabFolderPath}/{prefabName}.prefab";
            fixture.CreatePrefabAtPath(assetPath, bounds, 1, configure);
            return assetPath;
        }

        private static void AssertCodes(string path, params BenchmarkAssetIssueCode[] expectedCodes)
        {
            var report = BenchmarkAssetValidator.ValidatePrefab(path, BenchmarkAssetKind.WorkTable);
            Assert.That(report.Issues.Select(issue => issue.Code), Is.EquivalentTo(expectedCodes));
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
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
            Assert.That(report.MaterialSlotCount, Is.EqualTo(0));
            Assert.That(report.UniqueSharedMaterialCount, Is.EqualTo(0));
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
                AssetDatabase.IsValidFolder(fixture.FixtureFolderPath),
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
            Assert.That(AssetDatabase.IsValidFolder(fixture.FixtureFolderPath), Is.False);
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
        public void CreatePrefabAtPath_ExternalBenchmarkPrefabSurvivesFailedCreateAndFixtureTeardown()
        {
            var path = $"{BenchmarkAssetTestFactory.BenchmarkPrefabFolderPath}/{WorkTablePrefabName}.prefab";
            var createdFolders = EnsureTestAssetFolders(BenchmarkAssetTestFactory.BenchmarkPrefabFolderPath);
            CreateExternalPrefab(path, "ExternallyOwnedWorkTable");
            var original = CapturePrefabIdentity(path);
            try
            {
                Assert.Throws<InvalidOperationException>(() =>
                    fixture.CreatePrefabAtPath(path, new Vector3(1f, 1f, 1f), 1));

                fixture.Dispose();

                AssertPrefabIdentityIsUnchanged(original);
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                DeleteTestAssetFolders(createdFolders);
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FixtureInstances_SanitizedHelperNameCollisionDoesNotAffectForeignPrefabBeforeOwnerDisposes(
            bool disposeFirstFactoryFirst)
        {
            const string firstPath = "Assets/Tests/FixtureCollisionA/A-B.prefab";
            const string secondPath = "Assets/Tests/FixtureCollisionB/A_B.prefab";
            using (var firstFixture = new BenchmarkAssetTestFactory())
            using (var secondFixture = new BenchmarkAssetTestFactory())
            {
                firstFixture.CreatePrefabAtPath(firstPath, new Vector3(0.90f, 0.65f, 0.90f), 1);
                var firstIdentity = CapturePrefabIdentity(firstPath);
                secondFixture.CreatePrefabAtPath(secondPath, new Vector3(0.90f, 0.65f, 0.90f), 1);
                var secondIdentity = CapturePrefabIdentity(secondPath);

                Assert.That(secondIdentity.MeshGuid, Is.Not.EqualTo(firstIdentity.MeshGuid));
                Assert.That(secondIdentity.MaterialGuid, Is.Not.EqualTo(firstIdentity.MaterialGuid));

                if (disposeFirstFactoryFirst)
                {
                    firstFixture.Dispose();
                    AssertPrefabIdentityIsUnchanged(secondIdentity);
                    secondFixture.Dispose();
                }
                else
                {
                    secondFixture.Dispose();
                    AssertPrefabIdentityIsUnchanged(firstIdentity);
                    firstFixture.Dispose();
                }
            }
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
        public void ValidatePrefab_AsymmetricCoffeeMachineNestedTransformUsesRootLocalBounds()
        {
            var path = CreatePrefabAtBenchmarkPath(
                BenchmarkAssetKind.CoffeeMachine,
                "PF_Benchmark_CoffeeMachine_01",
                new Vector3(1.30f, 0.62f, 0.25f),
                root =>
                {
                    root.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    var visual = root.transform.Find("Visual");
                    var container = new GameObject("CoffeeMachineVisualContainer");
                    container.transform.SetParent(root.transform, false);
                    container.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    container.transform.localScale = new Vector3(0.5f, 1f, 2f);
                    visual.SetParent(container.transform, false);
                });

            var report = BenchmarkAssetValidator.ValidatePrefab(path, BenchmarkAssetKind.CoffeeMachine);
            Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain(BenchmarkAssetIssueCode.RootTransformNotIdentity));
            Assert.That(report.Issues.Select(issue => issue.Code), Has.None.EqualTo(BenchmarkAssetIssueCode.BoundsOutsideTolerance));
        }

        [Test]
        public void ValidatePrefab_VisibleBoundsAboveFloorToleranceReportsBoundsOutsideTolerance()
        {
            var path = CreateWorkTablePrefab(root =>
                root.transform.Find("Visual").localPosition = new Vector3(0f, 0.006f, 0f));

            AssertCodes(path, BenchmarkAssetIssueCode.BoundsOutsideTolerance);
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

        [Test]
        public void Collider_ApprovedPrimitiveCollidersPass()
        {
            var path = CreateWorkTablePrefab(root =>
            {
                var sphere = root.AddComponent<SphereCollider>();
                sphere.center = new Vector3(0f, 0.325f, 0f);
                sphere.radius = 0.1f;

                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.center = new Vector3(0f, 0.325f, 0f);
                capsule.radius = 0.1f;
                capsule.height = 0.4f;
            });

            Assert.That(
                BenchmarkAssetValidator.ValidatePrefab(path, BenchmarkAssetKind.WorkTable).Issues,
                Is.Empty);
        }

        [Test]
        public void Collider_MeshColliderReportsInvalidColliderType()
        {
            var path = CreateWorkTablePrefab(root => root.AddComponent<MeshCollider>());

            AssertHasCode(path, BenchmarkAssetKind.WorkTable, BenchmarkAssetIssueCode.InvalidColliderType);
        }

        [Test]
        public void Collider_TooManyCollidersReportsColliderBudgetExceeded()
        {
            var path = CreateWorkTablePrefab(root =>
            {
                AddContainedBoxCollider(root);
                AddContainedBoxCollider(root);
                AddContainedBoxCollider(root);
            });

            AssertHasCode(path, BenchmarkAssetKind.WorkTable, BenchmarkAssetIssueCode.ColliderBudgetExceeded);
        }

        [Test]
        public void Collider_TriggerReportsTriggerColliderNotAllowed()
        {
            var path = CreateWorkTablePrefab(root => root.GetComponent<BoxCollider>().isTrigger = true);

            AssertHasCode(path, BenchmarkAssetKind.WorkTable, BenchmarkAssetIssueCode.TriggerColliderNotAllowed);
        }

        [Test]
        public void Collider_BoundsFarOutsideVisibleModelReportsColliderOutsideModelBounds()
        {
            var path = CreateWorkTablePrefab(root =>
                root.GetComponent<BoxCollider>().center = new Vector3(1f, 0.325f, 0f));

            AssertHasCode(path, BenchmarkAssetKind.WorkTable, BenchmarkAssetIssueCode.ColliderOutsideModelBounds);
        }

        [Test]
        public void References_MissingRendererMaterialReportsMissingReference()
        {
            var path = CreateWorkTablePrefab(root =>
                root.transform.Find("Visual").GetComponent<MeshRenderer>().sharedMaterial = null);

            AssertHasCode(path, BenchmarkAssetKind.WorkTable, BenchmarkAssetIssueCode.MissingReference);
        }

        [Test]
        public void BatchValidation_ReturnsIssuesForAllThreeAssetsWithoutStoppingEarly()
        {
            var workTablePath = CreatePrefabAtBenchmarkPath(
                "PF_Benchmark_WorkTable_01",
                new Vector3(0.90f, 0.65f, 0.90f),
                root => root.AddComponent<MeshCollider>());
            var coffeeMachinePath = CreatePrefabAtBenchmarkPath(
                "PF_Benchmark_CoffeeMachine_01",
                new Vector3(0.65f, 0.62f, 0.50f),
                root => root.AddComponent<MeshCollider>());
            var ceramicCupPath = CreatePrefabAtBenchmarkPath(
                "PF_Benchmark_CeramicCup_01",
                new Vector3(0.14f, 0.16f, 0.14f),
                root => root.AddComponent<MeshCollider>());

            var report = BenchmarkAssetValidator.ValidateAllBenchmarks();

            Assert.That(
                report.Issues.Where(issue => issue.Code == BenchmarkAssetIssueCode.InvalidColliderType)
                    .Select(issue => issue.AssetPath),
                Is.EqualTo(new[] { workTablePath, coffeeMachinePath, ceramicCupPath }));
        }

        [Test]
        public void BatchValidation_MissingExpectedPrefabReportsMissingReference()
        {
            var report = BenchmarkAssetValidator.ValidateAllBenchmarks();

            Assert.That(
                report.Issues.Select(issue => new { issue.Code, issue.AssetPath }),
                Is.EqualTo(new[]
                {
                    new
                    {
                        Code = BenchmarkAssetIssueCode.MissingReference,
                        AssetPath = $"{BenchmarkAssetTestFactory.BenchmarkPrefabFolderPath}/PF_Benchmark_WorkTable_01.prefab"
                    },
                    new
                    {
                        Code = BenchmarkAssetIssueCode.MissingReference,
                        AssetPath = $"{BenchmarkAssetTestFactory.BenchmarkPrefabFolderPath}/PF_Benchmark_CoffeeMachine_01.prefab"
                    },
                    new
                    {
                        Code = BenchmarkAssetIssueCode.MissingReference,
                        AssetPath = $"{BenchmarkAssetTestFactory.BenchmarkPrefabFolderPath}/PF_Benchmark_CeramicCup_01.prefab"
                    }
                }));
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

        private string CreatePrefabAtBenchmarkPath(
            BenchmarkAssetKind kind,
            string prefabName,
            Vector3 bounds,
            Action<GameObject> configure = null)
        {
            var assetPath = $"{BenchmarkAssetTestFactory.BenchmarkPrefabFolderPath}/{prefabName}.prefab";
            fixture.CreatePrefabAtPath(assetPath, bounds, 1, configure);
            return assetPath;
        }

        private static PrefabIdentity CapturePrefabIdentity(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, "Expected saved Prefab to exist.");

            var visual = prefab.transform.Find("Visual");
            if (visual == null)
            {
                return new PrefabIdentity(
                    prefabPath,
                    AssetDatabase.AssetPathToGUID(prefabPath),
                    prefab.name,
                    null,
                    null,
                    null,
                    null);
            }

            var meshFilter = visual.GetComponent<MeshFilter>();
            var renderer = visual.GetComponent<MeshRenderer>();
            var meshPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
            var materialPath = AssetDatabase.GetAssetPath(renderer.sharedMaterial);
            return new PrefabIdentity(
                prefabPath,
                AssetDatabase.AssetPathToGUID(prefabPath),
                prefab.name,
                meshPath,
                AssetDatabase.AssetPathToGUID(meshPath),
                materialPath,
                AssetDatabase.AssetPathToGUID(materialPath));
        }

        private static void AssertPrefabIdentityIsUnchanged(PrefabIdentity identity)
        {
            var current = CapturePrefabIdentity(identity.PrefabPath);
            Assert.That(current.PrefabGuid, Is.EqualTo(identity.PrefabGuid));
            Assert.That(current.PrefabName, Is.EqualTo(identity.PrefabName));
            Assert.That(current.MeshPath, Is.EqualTo(identity.MeshPath));
            Assert.That(current.MeshGuid, Is.EqualTo(identity.MeshGuid));
            Assert.That(current.MaterialPath, Is.EqualTo(identity.MaterialPath));
            Assert.That(current.MaterialGuid, Is.EqualTo(identity.MaterialGuid));
        }

        private static List<string> EnsureTestAssetFolders(string assetFolderPath)
        {
            var createdFolders = new List<string>();
            EnsureTestAssetFolders(assetFolderPath, createdFolders);
            return createdFolders;
        }

        private static void EnsureTestAssetFolders(string assetFolderPath, ICollection<string> createdFolders)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            EnsureTestAssetFolders(parent, createdFolders);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolderPath));
            createdFolders.Add(assetFolderPath);
        }

        private static void CreateExternalPrefab(string prefabPath, string rootName)
        {
            var root = new GameObject(rootName);
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void DeleteTestAssetFolders(IEnumerable<string> createdFolders)
        {
            foreach (var folderPath in createdFolders.OrderByDescending(path => path.Length))
            {
                if (AssetDatabase.IsValidFolder(folderPath))
                {
                    AssetDatabase.DeleteAsset(folderPath);
                }
            }

            AssetDatabase.Refresh();
        }

        private sealed class PrefabIdentity
        {
            public PrefabIdentity(
                string prefabPath,
                string prefabGuid,
                string prefabName,
                string meshPath,
                string meshGuid,
                string materialPath,
                string materialGuid)
            {
                PrefabPath = prefabPath;
                PrefabGuid = prefabGuid;
                PrefabName = prefabName;
                MeshPath = meshPath;
                MeshGuid = meshGuid;
                MaterialPath = materialPath;
                MaterialGuid = materialGuid;
            }

            public string PrefabPath { get; }
            public string PrefabGuid { get; }
            public string PrefabName { get; }
            public string MeshPath { get; }
            public string MeshGuid { get; }
            public string MaterialPath { get; }
            public string MaterialGuid { get; }
        }

        private static void AssertCodes(string path, params BenchmarkAssetIssueCode[] expectedCodes)
        {
            var report = BenchmarkAssetValidator.ValidatePrefab(path, BenchmarkAssetKind.WorkTable);
            Assert.That(report.Issues.Select(issue => issue.Code), Is.EquivalentTo(expectedCodes));
        }

        private static void AddContainedBoxCollider(GameObject root)
        {
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.325f, 0f);
            collider.size = new Vector3(0.1f, 0.1f, 0.1f);
        }

        private static void AssertHasCode(
            string path,
            BenchmarkAssetKind kind,
            BenchmarkAssetIssueCode expectedCode)
        {
            Assert.That(
                BenchmarkAssetValidator.ValidatePrefab(path, kind).Issues.Select(issue => issue.Code),
                Does.Contain(expectedCode));
        }
    }
}

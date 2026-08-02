using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AnimalCafe.EditorTools.AssetPipeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.EditMode.AssetPipeline
{
    public sealed class BenchmarkAssetValidatorReviewTests
    {
        private const string BenchmarkPrefabFolderPath =
            BenchmarkAssetTestFactory.BenchmarkPrefabFolderPath;
        private static readonly Vector3 WorkTableSize = new Vector3(0.90f, 0.65f, 0.90f);
        private static readonly Vector3 CoffeeMachineSize = new Vector3(0.65f, 0.62f, 0.50f);
        private static readonly Vector3 CeramicCupSize = new Vector3(0.14f, 0.16f, 0.14f);

        private BenchmarkAssetTestFactory fixture;
        private UnityEngine.Object selectionBeforeTest;

        [SetUp]
        public void SetUp()
        {
            selectionBeforeTest = Selection.activeObject;
            fixture = new BenchmarkAssetTestFactory();
        }

        [TearDown]
        public void TearDown()
        {
            Selection.activeObject = selectionBeforeTest;
            fixture.Dispose();
        }

        [Test]
        public void Collider_DisabledColliderDoesNotConsumeBudgetOrReportColliderIssues()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, root =>
            {
                root.AddComponent<MeshCollider>().enabled = false;
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(10f, 0.325f, 0f);
                collider.isTrigger = true;
                collider.enabled = false;
            });

            Assert.That(Validate(path, BenchmarkAssetKind.WorkTable), Is.Empty);
        }

        [Test]
        public void Collider_InactiveParentColliderDoesNotConsumeBudgetOrReportColliderIssues()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, root =>
            {
                var inactiveParent = new GameObject("InactiveColliderParent");
                inactiveParent.transform.SetParent(root.transform, false);
                inactiveParent.AddComponent<MeshCollider>();
                var collider = inactiveParent.AddComponent<BoxCollider>();
                collider.center = new Vector3(10f, 0.325f, 0f);
                collider.isTrigger = true;
                inactiveParent.SetActive(false);
            });

            Assert.That(Validate(path, BenchmarkAssetKind.WorkTable), Is.Empty);
        }

        [Test]
        public void Collider_VisibleBoundsEnvelopeAtExactlyPointZeroFiveMetersPasses()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, root =>
            {
                var collider = root.GetComponent<BoxCollider>();
                collider.size = new Vector3(0.1f, 0.1f, 0.1f);
                collider.center = new Vector3(0f, 0.325f, 0f);
            });

            var visibleBounds = GetRootVisibleBounds(path);
            var visibleMaximum = visibleBounds.max.x;
            SetRootBoxColliderMaximumX(path, visibleMaximum + 0.05f);
            var colliderBounds = GetRootBoxColliderBounds(path);
            var report = fixture.ValidatePrefab(path, BenchmarkAssetKind.WorkTable);
            Assert.That(colliderBounds.max.x, Is.EqualTo(visibleMaximum + 0.05f));
            Assert.That(report.Issues, Is.Empty);
        }

        [Test]
        public void Collider_VisibleBoundsEnvelopeJustOverPointZeroFiveMetersReportsOutsideBounds()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, root =>
                root.GetComponent<BoxCollider>().size = new Vector3(1.0002f, 0.65f, 0.90f));

            Assert.That(
                Validate(path, BenchmarkAssetKind.WorkTable),
                Does.Contain(BenchmarkAssetIssueCode.ColliderOutsideModelBounds));
        }

        [Test]
        public void Collider_VisibleBoundsEnvelopeAtNextRepresentableValueBeyondPointZeroFiveMetersReportsOutsideBounds()
        {
            var nextMaximum = NextFloatTowardPositiveInfinity(0.50f);
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, root =>
            {
                var collider = root.GetComponent<BoxCollider>();
                collider.center = new Vector3(
                    nextMaximum - collider.size.x * 0.5f,
                    collider.center.y,
                    collider.center.z);
            });

            var actualMaximum = GetRootBoxColliderBounds(path).max.x;
            Assert.That(actualMaximum, Is.GreaterThan(0.50f));
            Assert.That(actualMaximum - 0.50f, Is.LessThan(0.000001f));
            Assert.That(
                Validate(path, BenchmarkAssetKind.WorkTable),
                Does.Contain(BenchmarkAssetIssueCode.ColliderOutsideModelBounds));
        }

        [Test]
        public void Collider_FloorBoundaryAtNegativePointZeroZeroFiveMetersPasses()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, root =>
                root.GetComponent<BoxCollider>().center = new Vector3(0f, 0.32f, 0f));

            Assert.That(Validate(path, BenchmarkAssetKind.WorkTable), Is.Empty);
        }

        [Test]
        public void Collider_FloorBoundaryBelowNegativePointZeroZeroFiveMetersReportsOutsideBounds()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, root =>
                root.GetComponent<BoxCollider>().center = new Vector3(0f, 0.3199f, 0f));

            Assert.That(
                Validate(path, BenchmarkAssetKind.WorkTable),
                Does.Contain(BenchmarkAssetIssueCode.ColliderOutsideModelBounds));
        }

        [Test]
        public void Collider_FloorAtNextRepresentableValueBelowNegativePointZeroZeroFiveMetersReportsOutsideBounds()
        {
            var nextMinimum = NextFloatTowardNegativeInfinity(-0.005f);
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, root =>
            {
                var collider = root.GetComponent<BoxCollider>();
                collider.size = new Vector3(0.1f, 0f, 0.1f);
                collider.center = new Vector3(
                    collider.center.x,
                    nextMinimum,
                    collider.center.z);
            });

            var actualMinimum = GetRootBoxColliderBounds(path).min.y;
            Assert.That(actualMinimum, Is.LessThan(-0.005f));
            Assert.That(-0.005f - actualMinimum, Is.LessThan(0.000001f));
            Assert.That(
                Validate(path, BenchmarkAssetKind.WorkTable),
                Does.Contain(BenchmarkAssetIssueCode.ColliderOutsideModelBounds));
        }

        [Test]
        public void Collider_TransformedChildUsesWorldBounds()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, root =>
            {
                UnityEngine.Object.DestroyImmediate(root.GetComponent<BoxCollider>());
                var child = new GameObject("ColliderAnchor");
                child.transform.SetParent(root.transform, false);
                child.transform.localPosition = new Vector3(0f, 0.325f, 0f);
                child.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
                child.transform.localScale = new Vector3(1.2f, 0.8f, 0.6f);
                child.AddComponent<BoxCollider>().size = new Vector3(0.2f, 0.2f, 0.2f);
            });

            Assert.That(Validate(path, BenchmarkAssetKind.WorkTable), Is.Empty);
        }

        [Test]
        public void BatchValidation_AggregatesMaterialSlotsAndCrossPrefabUniqueMaterials()
        {
            CreateValidBatchPrefabs();

            var report = fixture.ValidateAllBenchmarks();

            Assert.That(report.IsValid, Is.True);
            Assert.That(report.MaterialSlotCount, Is.EqualTo(4));
            Assert.That(report.UniqueSharedMaterialCount, Is.EqualTo(1));
            Assert.That(report.Issues, Is.Not.InstanceOf<System.Collections.Generic.IList<BenchmarkAssetValidationIssue>>());
        }

        [Test]
        public void BatchValidation_MissingPrefabsKeepsZeroMaterialTotals()
        {
            var report = fixture.ValidateAllBenchmarks();

            Assert.That(report.MaterialSlotCount, Is.EqualTo(0));
            Assert.That(report.UniqueSharedMaterialCount, Is.EqualTo(0));
            Assert.That(report.Issues.Count, Is.EqualTo(3));
        }

        [Test]
        public void ProductionBenchmarks_NonReadableImportedMeshesCompleteValidation()
        {
            var report = BenchmarkAssetValidator.ValidateAllBenchmarks();

            Assert.That(
                report.Issues.Select(issue => issue.Code),
                Has.None.EqualTo(BenchmarkAssetIssueCode.InvalidAssetPath));
            Assert.That(report.IsValid, Is.True);
        }

        [Test]
        public void Menu_InvalidAssetsLogsEveryIssueSelectsFirstExistingAssetAndDoesNotMutateAssets()
        {
            var paths = CreateInvalidBatchPrefabs();
            var originalStates = new[]
            {
                CapturePrefabState(paths.WorkTablePath),
                CapturePrefabState(paths.CoffeeMachinePath),
                CapturePrefabState(paths.CeramicCupPath)
            };
            LogAssert.Expect(LogType.Error, new Regex($"^{Regex.Escape($"{paths.WorkTablePath}: InvalidColliderType")}$"));
            LogAssert.Expect(LogType.Error, new Regex($"^{Regex.Escape($"{paths.CoffeeMachinePath}: MissingLodGroup")}$"));
            LogAssert.Expect(LogType.Error, new Regex($"^{Regex.Escape($"{paths.CoffeeMachinePath}: InvalidColliderType")}$"));
            LogAssert.Expect(LogType.Error, new Regex($"^{Regex.Escape($"{paths.CeramicCupPath}: ColliderBudgetExceeded")}$"));
            LogAssert.Expect(LogType.Error, new Regex($"^{Regex.Escape($"{paths.CeramicCupPath}: InvalidColliderType")}$"));

            var report = fixture.ExecuteValidationMenu();

            LogAssert.NoUnexpectedReceived();
            Assert.That(report.IsValid, Is.False);
            Assert.That(Selection.activeObject, Is.EqualTo(AssetDatabase.LoadMainAssetAtPath(paths.WorkTablePath)));
            AssertPrefabStateIsUnchanged(originalStates[0]);
            AssertPrefabStateIsUnchanged(originalStates[1]);
            AssertPrefabStateIsUnchanged(originalStates[2]);
        }

        [Test]
        public void Menu_ValidAssetsLogsOnlyGreenSummary()
        {
            CreateValidBatchPrefabs();
            LogAssert.Expect(
                LogType.Log,
                new Regex("^<color=green>Benchmark asset validation passed: 0 issues.</color>$"));

            var report = fixture.ExecuteValidationMenu();

            LogAssert.NoUnexpectedReceived();
            Assert.That(report.IsValid, Is.True);
            Assert.That(report.Issues, Is.Empty);
        }

        private string CreatePrefab(BenchmarkAssetKind kind, Action<GameObject> configure = null)
        {
            var path = $"{BenchmarkPrefabFolderPath}/PF_Benchmark_{kind}_01.prefab";
            fixture.CreatePrefabAtPath(path, SizeFor(kind), 1, configure);
            return path;
        }

        private BenchmarkPrefabPaths CreateValidBatchPrefabs()
        {
            var sharedMaterial = fixture.CreateMaterialAsset(Shader.Find("Universal Render Pipeline/Lit"));
            var workTablePath = CreatePrefab(BenchmarkAssetKind.WorkTable, root =>
                root.transform.Find("Visual").GetComponent<MeshRenderer>().sharedMaterial = sharedMaterial);
            var coffeeMachinePath = CreatePrefab(BenchmarkAssetKind.CoffeeMachine, root => ConfigureValidCoffeeMachine(root, sharedMaterial));
            var ceramicCupPath = CreatePrefab(BenchmarkAssetKind.CeramicCup, root =>
                root.transform.Find("Visual").GetComponent<MeshRenderer>().sharedMaterial = sharedMaterial);
            return new BenchmarkPrefabPaths(workTablePath, coffeeMachinePath, ceramicCupPath);
        }

        private BenchmarkPrefabPaths CreateInvalidBatchPrefabs()
        {
            return new BenchmarkPrefabPaths(
                CreatePrefab(BenchmarkAssetKind.WorkTable, root => root.AddComponent<MeshCollider>()),
                CreatePrefab(BenchmarkAssetKind.CoffeeMachine, root => root.AddComponent<MeshCollider>()),
                CreatePrefab(BenchmarkAssetKind.CeramicCup, root => root.AddComponent<MeshCollider>()));
        }

        private void ConfigureValidCoffeeMachine(GameObject root, Material sharedMaterial)
        {
            var lod0 = root.transform.Find("Visual").GetComponent<MeshRenderer>();
            lod0.GetComponent<MeshFilter>().sharedMesh = fixture.CreateMeshAsset(CoffeeMachineSize, 2);
            lod0.sharedMaterial = sharedMaterial;

            var lod1Object = new GameObject("Lod1");
            lod1Object.transform.SetParent(root.transform, false);
            lod1Object.AddComponent<MeshFilter>().sharedMesh = fixture.CreateMeshAsset(CoffeeMachineSize, 1);
            var lod1 = lod1Object.AddComponent<MeshRenderer>();
            lod1.sharedMaterial = sharedMaterial;

            var lodGroup = root.AddComponent<LODGroup>();
            lodGroup.SetLODs(new[]
            {
                new LOD(0.5f, new Renderer[] { lod0 }),
                new LOD(0.1f, new Renderer[] { lod1 })
            });
        }

        private BenchmarkAssetIssueCode[] Validate(string path, BenchmarkAssetKind kind)
        {
            return fixture.ValidatePrefab(path, kind).Issues
                .Select(issue => issue.Code)
                .ToArray();
        }

        private static Vector3 SizeFor(BenchmarkAssetKind kind)
        {
            switch (kind)
            {
                case BenchmarkAssetKind.WorkTable:
                    return WorkTableSize;
                case BenchmarkAssetKind.CoffeeMachine:
                    return CoffeeMachineSize;
                case BenchmarkAssetKind.CeramicCup:
                    return CeramicCupSize;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static PrefabState CapturePrefabState(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath);
            return new PrefabState(
                assetPath,
                AssetDatabase.AssetPathToGUID(assetPath),
                AssetDatabase.GetAssetDependencyHash(assetPath),
                importer.assetBundleName,
                importer.assetBundleVariant,
                importer.userData);
        }

        private static Bounds GetRootBoxColliderBounds(string assetPath)
        {
            var root = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                return root.GetComponent<BoxCollider>().bounds;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetRootBoxColliderMaximumX(string assetPath, float targetMaximum)
        {
            var root = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                var collider = root.GetComponent<BoxCollider>();
                collider.center += new Vector3(targetMaximum - collider.bounds.max.x, 0f, 0f);
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Bounds GetRootVisibleBounds(string assetPath)
        {
            var root = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                return root.transform.Find("Visual").GetComponent<Renderer>().bounds;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static float NextFloatTowardPositiveInfinity(float value)
        {
            var bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            return BitConverter.ToSingle(BitConverter.GetBytes(bits + 1), 0);
        }

        private static float NextFloatTowardNegativeInfinity(float value)
        {
            var bits = BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            return BitConverter.ToSingle(BitConverter.GetBytes(bits + 1), 0);
        }

        private static void AssertPrefabStateIsUnchanged(PrefabState expected)
        {
            var current = CapturePrefabState(expected.AssetPath);
            Assert.That(current.Guid, Is.EqualTo(expected.Guid));
            Assert.That(current.DependencyHash, Is.EqualTo(expected.DependencyHash));
            Assert.That(current.AssetBundleName, Is.EqualTo(expected.AssetBundleName));
            Assert.That(current.AssetBundleVariant, Is.EqualTo(expected.AssetBundleVariant));
            Assert.That(current.UserData, Is.EqualTo(expected.UserData));
        }

        private sealed class BenchmarkPrefabPaths
        {
            public BenchmarkPrefabPaths(string workTablePath, string coffeeMachinePath, string ceramicCupPath)
            {
                WorkTablePath = workTablePath;
                CoffeeMachinePath = coffeeMachinePath;
                CeramicCupPath = ceramicCupPath;
            }

            public string WorkTablePath { get; }

            public string CoffeeMachinePath { get; }

            public string CeramicCupPath { get; }
        }

        private sealed class PrefabState
        {
            public PrefabState(
                string assetPath,
                string guid,
                Hash128 dependencyHash,
                string assetBundleName,
                string assetBundleVariant,
                string userData)
            {
                AssetPath = assetPath;
                Guid = guid;
                DependencyHash = dependencyHash;
                AssetBundleName = assetBundleName;
                AssetBundleVariant = assetBundleVariant;
                UserData = userData;
            }

            public string AssetPath { get; }

            public string Guid { get; }

            public Hash128 DependencyHash { get; }

            public string AssetBundleName { get; }

            public string AssetBundleVariant { get; }

            public string UserData { get; }
        }
    }
}

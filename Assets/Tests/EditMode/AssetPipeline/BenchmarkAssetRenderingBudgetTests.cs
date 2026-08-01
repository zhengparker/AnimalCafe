using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AnimalCafe.EditorTools.AssetPipeline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.AssetPipeline
{
    public sealed class BenchmarkAssetRenderingBudgetTests
    {
        private static readonly Vector3 CoffeeMachineSize = new Vector3(0.65f, 0.62f, 0.50f);
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
        public void Rendering_ApprovedOpaqueUrpLitSharedMaterialPasses()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 6000);

            Assert.That(Validate(path, BenchmarkAssetKind.WorkTable), Is.Empty);
        }

        [Test]
        public void Rendering_CeramicCupAt6000TrianglesPasses()
        {
            var path = CreatePrefab(BenchmarkAssetKind.CeramicCup, 6000);

            Assert.That(Validate(path, BenchmarkAssetKind.CeramicCup), Is.Empty);
        }

        [Test]
        public void Rendering_ValidSharedMaterialsExposeSlotAndUniqueMaterialCounts()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 1, root =>
            {
                var sourceRenderer = root.transform.Find("Visual").GetComponent<MeshRenderer>();
                AddRenderer(root, "SharedMaterialVisual", sourceRenderer.GetComponent<MeshFilter>().sharedMesh, sourceRenderer.sharedMaterial);
            });

            var report = fixture.ValidatePrefab(path, BenchmarkAssetKind.WorkTable);

            Assert.That(report.IsValid, Is.True);
            Assert.That(report.MaterialSlotCount, Is.EqualTo(2));
            Assert.That(report.UniqueSharedMaterialCount, Is.EqualTo(1));
        }

        [Test]
        public void Rendering_ValidDistinctMaterialsExposeSlotAndUniqueMaterialCounts()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 1, root =>
            {
                var sourceRenderer = root.transform.Find("Visual").GetComponent<MeshRenderer>();
                var distinctMaterial = fixture.CreateMaterialAsset(Shader.Find("Universal Render Pipeline/Lit"));
                AddRenderer(root, "DistinctMaterialVisual", sourceRenderer.GetComponent<MeshFilter>().sharedMesh, distinctMaterial);
            });

            var report = fixture.ValidatePrefab(path, BenchmarkAssetKind.WorkTable);

            Assert.That(report.IsValid, Is.True);
            Assert.That(report.MaterialSlotCount, Is.EqualTo(2));
            Assert.That(report.UniqueSharedMaterialCount, Is.EqualTo(2));
        }

        [Test]
        public void Rendering_MissingMeshReportsMissingMesh()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 1, root =>
                root.transform.Find("Visual").GetComponent<MeshFilter>().sharedMesh = null);

            AssertHasCode(path, BenchmarkAssetKind.WorkTable, BenchmarkAssetIssueCode.MissingMesh);
        }

        [Test]
        public void Rendering_TableAbove6000TrianglesReportsTriangleBudgetExceeded()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 6001);

            AssertHasCode(path, BenchmarkAssetKind.WorkTable, BenchmarkAssetIssueCode.TriangleBudgetExceeded);
        }

        [Test]
        public void Rendering_MachineAbove6000TrianglesReportsTriangleBudgetExceeded()
        {
            var path = CreatePrefab(BenchmarkAssetKind.CoffeeMachine, 6001);

            AssertHasCode(path, BenchmarkAssetKind.CoffeeMachine, BenchmarkAssetIssueCode.TriangleBudgetExceeded);
        }

        [Test]
        public void Rendering_CupAbove6000TrianglesReportsTriangleBudgetExceeded()
        {
            var path = CreatePrefab(BenchmarkAssetKind.CeramicCup, 6001);

            AssertHasCode(path, BenchmarkAssetKind.CeramicCup, BenchmarkAssetIssueCode.TriangleBudgetExceeded);
        }

        [Test]
        public void Rendering_MissingMaterialReportsMissingMaterial()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 1, root =>
                root.transform.Find("Visual").GetComponent<MeshRenderer>().sharedMaterials = new Material[] { null });

            AssertHasCode(path, BenchmarkAssetKind.WorkTable, BenchmarkAssetIssueCode.MissingMaterial);
        }

        [Test]
        public void Rendering_TooManyMaterialSlotsReportsMaterialSlotBudgetExceeded()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 1, root =>
            {
                var renderer = root.transform.Find("Visual").GetComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { renderer.sharedMaterial, renderer.sharedMaterial, renderer.sharedMaterial };
            });

            AssertHasCode(path, BenchmarkAssetKind.WorkTable, BenchmarkAssetIssueCode.MaterialSlotBudgetExceeded);
        }

        [Test]
        public void Rendering_MaterialSlotsWithinKindBudgetButAboveMeshSubmeshesReportsMaterialSubmeshMismatch()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 1, root =>
            {
                var renderer = root.transform.Find("Visual").GetComponent<MeshRenderer>();
                var firstMaterial = renderer.sharedMaterial;
                var secondMaterial = fixture.CreateMaterialAsset(
                    Shader.Find("Universal Render Pipeline/Lit"));
                renderer.sharedMaterials = new[] { firstMaterial, secondMaterial };
            });

            Assert.That(
                Validate(path, BenchmarkAssetKind.WorkTable).Select(code => code.ToString()),
                Does.Contain("MaterialSubmeshMismatch"));
        }

        [Test]
        public void Rendering_NonUrpLitShaderReportsInvalidShader()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 1, root =>
            {
                var material = fixture.CreateMaterialAsset(Shader.Find("Sprites/Default"));
                root.transform.Find("Visual").GetComponent<MeshRenderer>().sharedMaterial = material;
            });

            AssertHasCode(path, BenchmarkAssetKind.WorkTable, BenchmarkAssetIssueCode.InvalidShader);
        }

        [Test]
        public void Rendering_NonUrpLitMaterialStillChecksProjectTextureBudget()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 1, root =>
            {
                var material = fixture.CreateMaterialAsset(Shader.Find("Sprites/Default"));
                material.SetTexture("_MainTex", fixture.CreateTextureAsset(1024, 1024));
                root.transform.Find("Visual").GetComponent<MeshRenderer>().sharedMaterial = material;
            });

            Assert.That(
                Validate(path, BenchmarkAssetKind.WorkTable),
                Is.SupersetOf(new[]
                {
                    BenchmarkAssetIssueCode.InvalidShader,
                    BenchmarkAssetIssueCode.TextureBudgetExceeded
                }));
        }

        [Test]
        public void Rendering_NullShaderReportsInvalidShaderAndContinuesCollectingIssues()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 1, root =>
            {
                var renderer = root.transform.Find("Visual").GetComponent<MeshRenderer>();
                var material = fixture.CreateMaterialAsset(Shader.Find("Sprites/Default"));
                material.shader = null;
                renderer.sharedMaterial = material;
                root.transform.Find("Visual").GetComponent<MeshFilter>().sharedMesh = null;
            });

            var codes = Validate(path, BenchmarkAssetKind.WorkTable);

            Assert.That(codes, Does.Contain(BenchmarkAssetIssueCode.InvalidShader));
            Assert.That(codes, Does.Contain(BenchmarkAssetIssueCode.MissingMesh));
            Assert.That(codes, Has.None.EqualTo(BenchmarkAssetIssueCode.InvalidAssetPath));
        }

        [Test]
        public void Rendering_TransparentSurfaceReportsTransparentMaterial()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 1, root =>
                root.transform.Find("Visual").GetComponent<MeshRenderer>().sharedMaterial.SetFloat("_Surface", 1f));

            AssertHasCode(path, BenchmarkAssetKind.WorkTable, BenchmarkAssetIssueCode.TransparentMaterial);
        }

        [Test]
        public void Rendering_Texture512Passes()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 1, root =>
            {
                var texture = fixture.CreateTextureAsset(512, 512);
                root.transform.Find("Visual").GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_BaseMap", texture);
            });

            Assert.That(Validate(path, BenchmarkAssetKind.WorkTable), Is.Empty);
        }

        [Test]
        public void Rendering_Texture1024ReportsTextureBudgetExceeded()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 1, root =>
            {
                var texture = fixture.CreateTextureAsset(1024, 1024);
                root.transform.Find("Visual").GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_BaseMap", texture);
            });

            AssertHasCode(path, BenchmarkAssetKind.WorkTable, BenchmarkAssetIssueCode.TextureBudgetExceeded);
        }

        [Test]
        public void Rendering_DeletedSerializedTextureReferenceReportsMissingReference()
        {
            string texturePath = null;
            string materialPath = null;
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 1, root =>
            {
                var renderer = root.transform.Find("Visual").GetComponent<MeshRenderer>();
                var texture = fixture.CreateTextureAsset(16, 16);
                texturePath = AssetDatabase.GetAssetPath(texture);
                materialPath = AssetDatabase.GetAssetPath(renderer.sharedMaterial);
                renderer.sharedMaterial.SetTexture("_BaseMap", texture);
                EditorUtility.SetDirty(renderer.sharedMaterial);
            });
            AssetDatabase.SaveAssets();

            Assert.That(AssetDatabase.DeleteAsset(texturePath), Is.True);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(materialPath, ImportAssetOptions.ForceUpdate);

            var reloadedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.That(File.Exists(ToAbsoluteProjectPath(texturePath)), Is.False);
            Assert.That(reloadedMaterial.GetTexture("_BaseMap"), Is.Null,
                "The deleted Texture must resolve to null while its saved Material reference remains broken.");
            AssertHasCode(path, BenchmarkAssetKind.WorkTable, BenchmarkAssetIssueCode.MissingReference);
        }

        [Test]
        public void Rendering_UnusedNullShaderTexturePropertiesDoNotReportMissingReference()
        {
            var path = CreatePrefab(BenchmarkAssetKind.WorkTable, 1);

            Assert.That(
                Validate(path, BenchmarkAssetKind.WorkTable),
                Has.None.EqualTo(BenchmarkAssetIssueCode.MissingReference));
        }

        [TestCase(
            "Assets/Art/VisualPipeline/Benchmarks/Materials/M_Benchmark_WarmWood_01.mat",
            0f,
            0.18f)]
        [TestCase(
            "Assets/Art/VisualPipeline/Benchmarks/Materials/M_Benchmark_SageMetal_01.mat",
            0.55f,
            0.42f)]
        [TestCase(
            "Assets/Art/VisualPipeline/Benchmarks/Materials/M_Benchmark_CreamCeramic_01.mat",
            0f,
            0.30f)]
        [TestCase(
            "Assets/Art/VisualPipeline/Benchmarks/Materials/M_Benchmark_HoneyAccent_01.mat",
            0f,
            0.24f)]
        public void ProductionMaterials_UseApprovedOpaqueUrpLitSurfaceProperties(
            string materialPath,
            float expectedMetallic,
            float expectedSmoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

            Assert.That(material, Is.Not.Null, materialPath);
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(material.GetFloat("_Surface"), Is.EqualTo(0f));
            Assert.That(material.GetFloat("_Metallic"), Is.EqualTo(expectedMetallic).Within(0.0001f));
            Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(expectedSmoothness).Within(0.0001f));
        }

        [Test]
        public void ProductionCoffeeMachine_RendererMaterialCountsMatchImportedSubmeshes()
        {
            const string prefabPath =
                "Assets/Art/VisualPipeline/Benchmarks/Prefabs/PF_Benchmark_CoffeeMachine_01.prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
                    .ToArray();

                Assert.That(renderers, Has.Length.EqualTo(2));
                foreach (var renderer in renderers)
                {
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(mesh.subMeshCount),
                        renderer.name);
                    Assert.That(renderer.sharedMaterials.Count(material => material != null),
                        Is.EqualTo(mesh.subMeshCount), renderer.name);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [TestCase("Assets/Art/VisualPipeline/Benchmarks/Models/SM_Benchmark_WorkTable_01.fbx")]
        [TestCase("Assets/Art/VisualPipeline/Benchmarks/Models/SM_Benchmark_CoffeeMachine_01.fbx")]
        [TestCase("Assets/Art/VisualPipeline/Benchmarks/Models/SM_Benchmark_CeramicCup_01.fbx")]
        public void ProductionFbx_ContainsNoMachineAbsolutePathStrings(string assetPath)
        {
            var bytes = File.ReadAllBytes(ToAbsoluteProjectPath(assetPath));
            var printableStrings = ExtractPrintableAsciiStrings(bytes, 8);
            var machinePaths = printableStrings
                .Where(value => Regex.IsMatch(
                    value,
                    @"(?i)(?:[a-z]:[\\/]|\\\\[a-z0-9._-]+[\\/]|/(?:Users|home|Volumes|private|mnt)/)"))
                .ToArray();

            Assert.That(machinePaths, Is.Empty,
                $"{assetPath} contains machine-specific absolute FBX strings: {string.Join(" | ", machinePaths)}");
        }

        [Test]
        public void Lod_MachineWithTwoValidLevelsPasses()
        {
            var path = CreateMachineWithLods(4000, 2000);

            Assert.That(Validate(path, BenchmarkAssetKind.CoffeeMachine), Is.Empty);
        }

        [Test]
        public void Lod_MachineAt6000Lod0TrianglesPasses()
        {
            var path = CreateMachineWithLods(6000, 2400);

            Assert.That(Validate(path, BenchmarkAssetKind.CoffeeMachine), Is.Empty);
        }

        [Test]
        public void Lod_MachineWithoutLodGroupReportsMissingLodGroup()
        {
            var path = CreatePrefab(BenchmarkAssetKind.CoffeeMachine, 1);

            AssertHasCode(path, BenchmarkAssetKind.CoffeeMachine, BenchmarkAssetIssueCode.MissingLodGroup);
        }

        [Test]
        public void Lod_MachineWithoutSecondLevelReportsMissingLod1()
        {
            var path = CreatePrefab(BenchmarkAssetKind.CoffeeMachine, 1, root =>
            {
                var renderer = root.transform.Find("Visual").GetComponent<MeshRenderer>();
                var lodGroup = root.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[] { new LOD(0.5f, new Renderer[] { renderer }) });
            });

            AssertHasCode(path, BenchmarkAssetKind.CoffeeMachine, BenchmarkAssetIssueCode.MissingLod1);
        }

        [Test]
        public void Lod_MachineLod1Above2500ReportsLodTriangleBudgetExceeded()
        {
            var path = CreateMachineWithLods(5000, 2501);

            AssertHasCode(path, BenchmarkAssetKind.CoffeeMachine, BenchmarkAssetIssueCode.LodTriangleBudgetExceeded);
        }

        [Test]
        public void Lod_MachineLod1AboveSixtyPercentReportsLodReductionInsufficient()
        {
            var path = CreateMachineWithLods(4000, 2401);

            AssertHasCode(path, BenchmarkAssetKind.CoffeeMachine, BenchmarkAssetIssueCode.LodReductionInsufficient);
        }

        [Test]
        public void Lod_MachineRepeatedRendererMeshWithinLevelIsCountedOnce()
        {
            var path = CreateMachineWithLods(3000, 1500, true);

            Assert.That(Validate(path, BenchmarkAssetKind.CoffeeMachine), Is.Empty);
        }

        [Test]
        public void Lod_MachineDisabledOverBudgetRendererInBothLevelsIsExcluded()
        {
            var path = CreateMachineWithLods(4000, 2000, disabledSharedLodRendererTriangleCount: 3000);

            Assert.That(Validate(path, BenchmarkAssetKind.CoffeeMachine), Is.Empty);
        }

        [Test]
        public void Lod_MachineForwardMarkerRendererInLod1IsExcludedFromLodBudget()
        {
            var path = CreateMachineWithLods(4000, 2000, forwardMarkerLod1TriangleCount: 3000);
            var codes = Validate(path, BenchmarkAssetKind.CoffeeMachine);

            Assert.That(codes, Does.Contain(BenchmarkAssetIssueCode.InvalidForwardMarker));
            Assert.That(codes, Has.None.EqualTo(BenchmarkAssetIssueCode.TriangleBudgetExceeded));
            Assert.That(codes, Has.None.EqualTo(BenchmarkAssetIssueCode.LodTriangleBudgetExceeded));
            Assert.That(codes, Has.None.EqualTo(BenchmarkAssetIssueCode.LodReductionInsufficient));
        }

        [Test]
        public void Lod_MachineEnabledRendererOutsideLodGroupCountsAgainstLod0Budget()
        {
            var path = CreateMachineWithLods(4000, 2000, false, 2001);

            AssertHasCode(path, BenchmarkAssetKind.CoffeeMachine, BenchmarkAssetIssueCode.TriangleBudgetExceeded);
        }

        [Test]
        public void Lod_MachineRendererReusedAcrossLevelsReportsLodReductionInsufficient()
        {
            var path = CreatePrefab(BenchmarkAssetKind.CoffeeMachine, 1, root =>
            {
                var renderer = root.transform.Find("Visual").GetComponent<MeshRenderer>();
                var lodGroup = root.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[]
                {
                    new LOD(0.5f, new Renderer[] { renderer }),
                    new LOD(0.1f, new Renderer[] { renderer })
                });
            });

            AssertHasCode(path, BenchmarkAssetKind.CoffeeMachine, BenchmarkAssetIssueCode.LodReductionInsufficient);
        }

        [Test]
        public void Lod_MachineLod1WithNullRendererReportsMissingLod1()
        {
            var path = CreatePrefab(BenchmarkAssetKind.CoffeeMachine, 1, root =>
            {
                var renderer = root.transform.Find("Visual").GetComponent<MeshRenderer>();
                var lodGroup = root.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[]
                {
                    new LOD(0.5f, new Renderer[] { renderer }),
                    new LOD(0.1f, new Renderer[] { null })
                });
            });

            AssertHasCode(path, BenchmarkAssetKind.CoffeeMachine, BenchmarkAssetIssueCode.MissingLod1);
        }

        [Test]
        public void Lod_TableAndCupDoNotRequireLodGroup()
        {
            var tablePath = CreatePrefab(BenchmarkAssetKind.WorkTable, 1);
            var cupPath = CreatePrefab(BenchmarkAssetKind.CeramicCup, 1);

            Assert.That(Validate(tablePath, BenchmarkAssetKind.WorkTable), Is.Empty);
            Assert.That(Validate(cupPath, BenchmarkAssetKind.CeramicCup), Is.Empty);
        }

        private string CreateMachineWithLods(
            int lod0TriangleCount,
            int lod1TriangleCount,
            bool duplicateLod0Renderer = false,
            int extraVisibleTriangleCount = 0,
            int disabledSharedLodRendererTriangleCount = 0,
            int forwardMarkerLod1TriangleCount = 0)
        {
            return CreatePrefab(BenchmarkAssetKind.CoffeeMachine, 1, root =>
            {
                var visual = root.transform.Find("Visual");
                var sourceRenderer = visual.GetComponent<MeshRenderer>();
                sourceRenderer.enabled = false;
                var lod0Renderer = AddRenderer(root, "Lod0", fixture.CreateMeshAsset(CoffeeMachineSize, lod0TriangleCount), sourceRenderer.sharedMaterial);
                var lod0Renderers = duplicateLod0Renderer
                    ? new Renderer[] { lod0Renderer, AddRenderer(root, "Lod0Duplicate", lod0Renderer.GetComponent<MeshFilter>().sharedMesh, sourceRenderer.sharedMaterial) }
                    : new Renderer[] { lod0Renderer };
                var lod1Renderer = AddRenderer(root, "Lod1", fixture.CreateMeshAsset(CoffeeMachineSize, lod1TriangleCount), sourceRenderer.sharedMaterial);
                var lodGroup = root.AddComponent<LODGroup>();
                lodGroup.SetLODs(new[]
                {
                    new LOD(0.5f, lod0Renderers),
                    new LOD(0.1f, new Renderer[] { lod1Renderer })
                });
                var lods = lodGroup.GetLODs();

                if (extraVisibleTriangleCount > 0)
                {
                    AddRenderer(root, "OutsideLodGroup", fixture.CreateMeshAsset(CoffeeMachineSize, extraVisibleTriangleCount), sourceRenderer.sharedMaterial);
                }

                if (disabledSharedLodRendererTriangleCount > 0)
                {
                    var disabledRenderer = AddRenderer(
                        root,
                        "DisabledSharedLodRenderer",
                        fixture.CreateMeshAsset(CoffeeMachineSize, disabledSharedLodRendererTriangleCount),
                        sourceRenderer.sharedMaterial);
                    disabledRenderer.enabled = false;
                    lods[0].renderers = lods[0].renderers.Concat(new Renderer[] { disabledRenderer }).ToArray();
                    lods[1].renderers = lods[1].renderers.Concat(new Renderer[] { disabledRenderer }).ToArray();
                    lodGroup.SetLODs(lods);
                }

                if (forwardMarkerLod1TriangleCount > 0)
                {
                    var marker = root.transform.Find("ForwardMarker").gameObject;
                    marker.AddComponent<MeshFilter>().sharedMesh = fixture.CreateMeshAsset(CoffeeMachineSize, forwardMarkerLod1TriangleCount);
                    var markerRenderer = marker.AddComponent<MeshRenderer>();
                    markerRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
                    lods[1].renderers = lods[1].renderers.Concat(new Renderer[] { markerRenderer }).ToArray();
                    lodGroup.SetLODs(lods);
                }
            });
        }

        private string CreatePrefab(BenchmarkAssetKind kind, int triangleCount, Action<GameObject> configure = null)
        {
            var path = $"{BenchmarkAssetTestFactory.BenchmarkPrefabFolderPath}/PF_Benchmark_{kind}_01.prefab";
            fixture.CreatePrefabAtPath(path, SizeFor(kind), triangleCount, configure);
            return path;
        }

        private static MeshRenderer AddRenderer(GameObject root, string name, Mesh mesh, Material material)
        {
            var visual = new GameObject(name);
            visual.transform.SetParent(root.transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private static Vector3 SizeFor(BenchmarkAssetKind kind)
        {
            switch (kind)
            {
                case BenchmarkAssetKind.WorkTable:
                    return new Vector3(0.90f, 0.65f, 0.90f);
                case BenchmarkAssetKind.CoffeeMachine:
                    return CoffeeMachineSize;
                case BenchmarkAssetKind.CeramicCup:
                    return new Vector3(0.14f, 0.16f, 0.14f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private BenchmarkAssetIssueCode[] Validate(string path, BenchmarkAssetKind kind)
        {
            return fixture.ValidatePrefab(path, kind).Issues.Select(issue => issue.Code).ToArray();
        }

        private void AssertHasCode(string path, BenchmarkAssetKind kind, BenchmarkAssetIssueCode expectedCode)
        {
            Assert.That(Validate(path, kind), Does.Contain(expectedCode));
        }

        private static string[] ExtractPrintableAsciiStrings(byte[] bytes, int minimumLength)
        {
            var strings = new List<string>();
            var current = new StringBuilder();
            foreach (var value in bytes)
            {
                if (value >= 0x20 && value <= 0x7e)
                {
                    current.Append((char)value);
                    continue;
                }

                if (current.Length >= minimumLength)
                {
                    strings.Add(current.ToString());
                }

                current.Clear();
            }

            if (current.Length >= minimumLength)
            {
                strings.Add(current.ToString());
            }

            return strings.ToArray();
        }

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}

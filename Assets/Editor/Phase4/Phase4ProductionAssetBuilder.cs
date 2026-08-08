using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.Phase4
{
    /// <summary>
    /// Owns the reproducible Phase 4 Blender source and FBX production contract.
    /// Raw inputs are verified before and after Blender runs and are never saved.
    /// </summary>
    public static class Phase4ProductionAssetBuilder
    {
        public const string WorkTableSourceSha256 =
            "CDA670B6DEAF309225E1636AA3B07EEBECC6D7D8497939027BC05659C156F60A";
        public const string CashRegisterRawSourceSha256 =
            "28859431416BD3D40D0C52D9F56DE9CD577566964094CD945B69C9120253321D";

        public const string CounterBlendPath =
            "ArtSource/Phase4/Blender/SM_Furniture_CounterModule_01.blend";
        public const string CashRegisterBlendPath =
            "ArtSource/Phase4/Blender/SM_Equipment_CashRegister_01.blend";
        public const string CounterFbxPath =
            "Assets/Art/Phase4/Models/SM_Furniture_CounterModule_01.fbx";
        public const string CashRegisterFbxPath =
            "Assets/Art/Phase4/Models/SM_Equipment_CashRegister_01.fbx";
        public const string CashRegisterTexturePath =
            "Assets/Art/Phase4/Textures/T_Equipment_CashRegister_BaseColor_01.png";
        public const string AutomationScriptPath =
            "ArtSource/Phase4/Tools/BuildPhase4ProductionAssets.py";
        public const string BlenderExecutablePath = "E:/Blender/blender.exe";

        public const string MaterialFolderPath = "Assets/Art/Phase4/Materials";
        public const string PrefabFolderPath = "Assets/Art/Phase4/Prefabs";
        public const string DefinitionFolderPath = "Assets/Art/Phase4/Definitions";
        public const string CatalogueFolderPath = "Assets/Art/Phase4/Catalogues";

        public const string WorkTablePrefabPath =
            PrefabFolderPath + "/PF_Furniture_WorkTable_01.prefab";
        public const string CounterPrefabPath =
            PrefabFolderPath + "/PF_Furniture_CounterModule_01.prefab";
        public const string CoffeeMachinePrefabPath =
            PrefabFolderPath + "/PF_Equipment_CoffeeMachine_01.prefab";
        public const string CashRegisterPrefabPath =
            PrefabFolderPath + "/PF_Equipment_CashRegister_01.prefab";
        public const string CeramicCupPrefabPath =
            PrefabFolderPath + "/PF_Item_CeramicCup_01.prefab";
        public const string WindowPrefabPath =
            PrefabFolderPath + "/PF_Wall_Window_01.prefab";
        public const string LongCounterFixturePrefabPath =
            PrefabFolderPath + "/PF_Validation_Counter_1x3_01.prefab";

        public const string WorkTableDefinitionPath =
            DefinitionFolderPath + "/FD_Furniture_WorkTable_01.asset";
        public const string CounterDefinitionPath =
            DefinitionFolderPath + "/FD_Furniture_CounterModule_01.asset";
        public const string CoffeeMachineDefinitionPath =
            DefinitionFolderPath + "/FD_Equipment_CoffeeMachine_01.asset";
        public const string CashRegisterDefinitionPath =
            DefinitionFolderPath + "/FD_Equipment_CashRegister_01.asset";
        public const string CeramicCupDefinitionPath =
            DefinitionFolderPath + "/FD_Item_CeramicCup_01.asset";
        public const string WindowDefinitionPath =
            DefinitionFolderPath + "/WD_Wall_Window_01.asset";
        public const string CataloguePath =
            CatalogueFolderPath + "/FC_Phase4Production.asset";

        private const string WorkTableBenchmarkPrefabPath =
            "Assets/Art/VisualPipeline/Benchmarks/Prefabs/PF_Benchmark_WorkTable_01.prefab";
        private const string CoffeeMachineBenchmarkPrefabPath =
            "Assets/Art/VisualPipeline/Benchmarks/Prefabs/PF_Benchmark_CoffeeMachine_01.prefab";
        private const string CeramicCupBenchmarkPrefabPath =
            "Assets/Art/VisualPipeline/Benchmarks/Prefabs/PF_Benchmark_CeramicCup_01.prefab";
        private const string WorkTableBenchmarkMaterialPath =
            "Assets/Art/VisualPipeline/Benchmarks/Materials/M_Benchmark_WorkTableOriginal_01.mat";
        private const string CoffeeMachineBenchmarkMaterialPath =
            "Assets/Art/VisualPipeline/Benchmarks/Materials/M_Benchmark_CoffeeMachineOriginal_01.mat";
        private const string CeramicCupBenchmarkMaterialPath =
            "Assets/Art/VisualPipeline/Benchmarks/Materials/M_Benchmark_CeramicCupOriginal_01.mat";

        public static IReadOnlyList<string> ProductionAssetPaths { get; } = new[]
        {
            MaterialFolderPath + "/M_Furniture_WorkTable_01.mat",
            MaterialFolderPath + "/M_Furniture_CounterModule_01.mat",
            MaterialFolderPath + "/M_Equipment_CoffeeMachine_01.mat",
            MaterialFolderPath + "/M_Equipment_CashRegister_01.mat",
            MaterialFolderPath + "/M_Item_CeramicCup_01.mat",
            MaterialFolderPath + "/M_Wall_Window_01.mat",
            WorkTablePrefabPath,
            CounterPrefabPath,
            CoffeeMachinePrefabPath,
            CashRegisterPrefabPath,
            CeramicCupPrefabPath,
            WindowPrefabPath,
            LongCounterFixturePrefabPath,
            WorkTableDefinitionPath,
            CounterDefinitionPath,
            CoffeeMachineDefinitionPath,
            CashRegisterDefinitionPath,
            WindowDefinitionPath,
            CataloguePath
        };

        public const float BoundsTolerance = 0.03f;
        public const int TargetTextureSize = 512;
        public const int MaximumTextureSize = 1024;
        public const int MaximumCashRegisterTriangles = 6000;

        // Unity axes are X=width, Y=height, Z=depth.
        public static readonly Vector3 CounterTargetBounds =
            new Vector3(1.00f, 0.72f, 1.00f);
        public static readonly Vector3 CashRegisterTargetBounds =
            new Vector3(0.43f, 0.45f, 0.26f);

        public static string ProjectRootPath =>
            Directory.GetParent(Application.dataPath)?.FullName ??
            throw new InvalidOperationException("Could not resolve the Unity project root.");

        public static string WorkTableSourcePath => GetAbsoluteProjectPath(
            "ArtSource/VisualPipeline/Benchmarks/Blender/SM_Benchmark_WorkTable_01.blend");

        public static string CashRegisterRawSourcePath => Path.GetFullPath(Path.Combine(
            ProjectRootPath,
            "..",
            "..",
            "Blender Model Item",
            "vintage computer monitor 3d model.glb"));

        public static bool IsWithinTargetBounds(Vector3 actual, Vector3 target)
        {
            const float comparisonEpsilon = 0.00001f;
            return Mathf.Abs(actual.x - target.x) <= BoundsTolerance + comparisonEpsilon &&
                Mathf.Abs(actual.y - target.y) <= BoundsTolerance + comparisonEpsilon &&
                Mathf.Abs(actual.z - target.z) <= BoundsTolerance + comparisonEpsilon;
        }

        public static bool IsTextureSizeAllowed(int width, int height)
        {
            return width > 0 && height > 0 &&
                width <= MaximumTextureSize && height <= MaximumTextureSize;
        }

        public static bool IsCashRegisterTriangleCountAllowed(int triangleCount)
        {
            return triangleCount > 0 && triangleCount <= MaximumCashRegisterTriangles;
        }

        [MenuItem("AnimalCafe/Phase 4/Build Production Model Sources")]
        public static void BuildProductionModelSources()
        {
            var workTableHashBefore = RequireSourceHash(
                WorkTableSourcePath,
                WorkTableSourceSha256);
            var cashHashBefore = RequireSourceHash(
                CashRegisterRawSourcePath,
                CashRegisterRawSourceSha256);
            var blenderPath = Path.GetFullPath(BlenderExecutablePath);
            var scriptPath = GetAbsoluteProjectPath(AutomationScriptPath);

            RequireFile(blenderPath, "Blender 5.2.0 LTS executable");
            RequireFile(scriptPath, "Phase 4 Blender automation script");

            var startInfo = new ProcessStartInfo
            {
                FileName = blenderPath,
                Arguments =
                    $"--background --factory-startup --python \"{scriptPath}\" -- " +
                    $"\"{ProjectRootPath}\" \"{WorkTableSourcePath}\" " +
                    $"\"{CashRegisterRawSourcePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = ProjectRootPath
            };

            using (var process = Process.Start(startInfo) ??
                throw new InvalidOperationException("Blender process did not start."))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"Blender production failed with exit code {process.ExitCode}.");
                }
            }

            var workTableHashAfter = RequireSourceHash(
                WorkTableSourcePath,
                WorkTableSourceSha256);
            var cashHashAfter = RequireSourceHash(
                CashRegisterRawSourcePath,
                CashRegisterRawSourceSha256);
            if (!string.Equals(workTableHashBefore, workTableHashAfter, StringComparison.Ordinal) ||
                !string.Equals(cashHashBefore, cashHashAfter, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("A protected raw source changed during production.");
            }

            RequireFile(GetAbsoluteProjectPath(CounterBlendPath), "Counter Blender source");
            RequireFile(GetAbsoluteProjectPath(CashRegisterBlendPath), "Cash Register Blender source");
            RequireFile(GetAbsoluteProjectPath(CounterFbxPath), "Counter FBX");
            RequireFile(GetAbsoluteProjectPath(CashRegisterFbxPath), "Cash Register FBX");
            RequireFile(GetAbsoluteProjectPath(CashRegisterTexturePath), "Cash Register Base Color");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        [MenuItem("AnimalCafe/Phase 4/Build Production Content")]
        public static void BuildProductionContent()
        {
            ConfigureProductionModelImporter(CounterFbxPath);
            ConfigureProductionModelImporter(CashRegisterFbxPath);
            EnsureAssetFolder(MaterialFolderPath);
            EnsureAssetFolder(PrefabFolderPath);
            EnsureAssetFolder(DefinitionFolderPath);
            EnsureAssetFolder(CatalogueFolderPath);

            var workTableMaterial = EnsureMaterialFromSource(
                WorkTableBenchmarkMaterialPath,
                MaterialFolderPath + "/M_Furniture_WorkTable_01.mat");
            var counterMaterial = EnsureMaterialFromSource(
                WorkTableBenchmarkMaterialPath,
                MaterialFolderPath + "/M_Furniture_CounterModule_01.mat");
            var coffeeMaterial = EnsureMaterialFromSource(
                CoffeeMachineBenchmarkMaterialPath,
                MaterialFolderPath + "/M_Equipment_CoffeeMachine_01.mat");
            var cupMaterial = EnsureMaterialFromSource(
                CeramicCupBenchmarkMaterialPath,
                MaterialFolderPath + "/M_Item_CeramicCup_01.mat");
            var cashMaterial = EnsureCashRegisterMaterial();
            var windowMaterial = EnsureMaterialFromSource(
                WorkTableBenchmarkMaterialPath,
                MaterialFolderPath + "/M_Wall_Window_01.mat");

            BuildClonedBenchmarkPrefab(
                WorkTableBenchmarkPrefabPath,
                WorkTablePrefabPath,
                "PF_Furniture_WorkTable_01",
                workTableMaterial,
                root => AddSurfaceSlot(root.transform, "slot.0", GetVisibleBounds(root).max.y));
            BuildImportedModelPrefab(
                CounterFbxPath,
                CounterPrefabPath,
                "PF_Furniture_CounterModule_01",
                counterMaterial,
                root => AddSurfaceSlot(root.transform, "slot.0", GetVisibleBounds(root).max.y));
            BuildClonedBenchmarkPrefab(
                CoffeeMachineBenchmarkPrefabPath,
                CoffeeMachinePrefabPath,
                "PF_Equipment_CoffeeMachine_01",
                coffeeMaterial,
                root =>
                {
                    KeepOnlyCoffeeLod0(root);
                    AddCoffeeForwardMarker(root);
                });
            BuildImportedModelPrefab(
                CashRegisterFbxPath,
                CashRegisterPrefabPath,
                "PF_Equipment_CashRegister_01",
                cashMaterial,
                AddCashRegisterSides);
            BuildClonedBenchmarkPrefab(
                CeramicCupBenchmarkPrefabPath,
                CeramicCupPrefabPath,
                "PF_Item_CeramicCup_01",
                cupMaterial,
                null);
            BuildWindowPrefab(windowMaterial);
            BuildLongCounterFixture(counterMaterial);

            var definitions = new[]
            {
                EnsureFurnitureDefinition(
                    WorkTableDefinitionPath,
                    "furniture.work-table.01",
                    "Work Table",
                    PlacementSurfaceType.Floor,
                    FurnitureFunctionType.None,
                    WorkTablePrefabPath),
                EnsureFurnitureDefinition(
                    CounterDefinitionPath,
                    "furniture.counter.module.01",
                    "Counter Module",
                    PlacementSurfaceType.Floor,
                    FurnitureFunctionType.None,
                    CounterPrefabPath),
                EnsureFurnitureDefinition(
                    CoffeeMachineDefinitionPath,
                    "equipment.coffee-machine.01",
                    "Coffee Machine",
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CoffeeMachine,
                    CoffeeMachinePrefabPath),
                EnsureFurnitureDefinition(
                    CashRegisterDefinitionPath,
                    "equipment.cash-register.01",
                    "Cash Register",
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CashRegister,
                    CashRegisterPrefabPath)
            };
            var windowDefinition = EnsureWindowDefinition();

            AssetDatabase.SaveAssets();
            ValidateBeforeCataloguePublish(definitions, windowDefinition);
            PublishCatalogueAtomically(definitions);
            AssetDatabase.SaveAssets();
        }

        private static void BuildClonedBenchmarkPrefab(
            string sourcePath,
            string destinationPath,
            string rootName,
            Material material,
            Action<GameObject> configure)
        {
            RequireAsset<GameObject>(sourcePath, "approved P3 benchmark Prefab");
            var root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                root.name = rootName;
                ResetRootTransform(root.transform);
                RemovePhase4SpatialMarkers(root);
                AssignMaterial(root, material);
                configure?.Invoke(root);
                PrefabUtility.SaveAsPrefabAsset(root, destinationPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BuildImportedModelPrefab(
            string modelPath,
            string destinationPath,
            string rootName,
            Material material,
            Action<GameObject> configure)
        {
            var model = RequireAsset<GameObject>(modelPath, "Phase 4 imported Model");
            var root = new GameObject(rootName);
            try
            {
                ResetRootTransform(root.transform);
                var modelInstance = UnityEngine.Object.Instantiate(model, root.transform);
                modelInstance.name = "Model";
                ResetRootTransform(modelInstance.transform);
                AssignMaterial(root, material);
                AddBoundsCollider(root, GetVisibleBounds(root));
                configure?.Invoke(root);
                PrefabUtility.SaveAsPrefabAsset(root, destinationPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildWindowPrefab(Material material)
        {
            var root = new GameObject("PF_Wall_Window_01");
            try
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "WindowVisual";
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = new Vector3(0.9f, 0.9f, 0.08f);
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.GetComponent<Renderer>().sharedMaterial = material;
                AddBoundsCollider(root, GetVisibleBounds(root));
                ResetRootTransform(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, WindowPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildLongCounterFixture(Material material)
        {
            var model = RequireAsset<GameObject>(CounterFbxPath, "Counter imported Model");
            var root = new GameObject("PF_Validation_Counter_1x3_01");
            try
            {
                for (var index = 0; index < 3; index++)
                {
                    var modelInstance = UnityEngine.Object.Instantiate(model, root.transform);
                    modelInstance.name = $"CounterVisual_{index}";
                    modelInstance.transform.localPosition = new Vector3(0f, 0f, index - 1f);
                    modelInstance.transform.localRotation = Quaternion.identity;
                    modelInstance.transform.localScale = Vector3.one;
                    AssignMaterial(modelInstance, material);
                    AddSurfaceSlot(
                        root.transform,
                        $"slot.{index}",
                        CounterTargetBounds.y,
                        new Vector3(0f, 0f, index - 1f));
                }

                AddBoundsCollider(root, GetVisibleBounds(root));
                ResetRootTransform(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, LongCounterFixturePrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static FurnitureDefinitionAsset EnsureFurnitureDefinition(
            string assetPath,
            string definitionId,
            string displayName,
            PlacementSurfaceType placementSurface,
            FurnitureFunctionType functionType,
            string prefabPath)
        {
            var definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("definitionId").stringValue = definitionId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("footprintWidth").intValue = 1;
            serialized.FindProperty("footprintDepth").intValue = 1;
            serialized.FindProperty("allowedPlacementSurfaces").intValue = (int)placementSurface;
            serialized.FindProperty("functionType").intValue = (int)functionType;
            serialized.FindProperty("prefab").objectReferenceValue =
                RequireAsset<GameObject>(prefabPath, "production Prefab");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static WallMountedDefinitionAsset EnsureWindowDefinition()
        {
            var definition = AssetDatabase.LoadAssetAtPath<WallMountedDefinitionAsset>(
                WindowDefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<WallMountedDefinitionAsset>();
                AssetDatabase.CreateAsset(definition, WindowDefinitionPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("definitionId").stringValue = "wall.window.01";
            serialized.FindProperty("displayName").stringValue = "Window";
            serialized.FindProperty("footprintWidth").intValue = 1;
            serialized.FindProperty("footprintHeight").intValue = 1;
            serialized.FindProperty("prefab").objectReferenceValue =
                RequireAsset<GameObject>(WindowPrefabPath, "Window Prefab");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static void ValidateBeforeCataloguePublish(
            IEnumerable<FurnitureDefinitionAsset> definitions,
            WallMountedDefinitionAsset windowDefinition)
        {
            var furnitureReport = Phase4AssetValidator.ValidateAll(definitions);
            var wallReport = Phase4AssetValidator.ValidateWallContent(
                Array.Empty<WallSurfaceAuthoring>(),
                new[] { windowDefinition },
                Array.Empty<WallMountedInstance>());
            var issues = furnitureReport.Issues.Concat(wallReport.Issues).ToArray();
            if (issues.Length > 0)
            {
                throw new InvalidOperationException(
                    "Phase 4 production content failed validation before catalogue publish:\n" +
                    string.Join("\n", issues.Select(issue =>
                        $"{issue.Code} | {issue.AssetPath} | {issue.Message}")));
            }
        }

        private static void ConfigureProductionModelImporter(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"Expected ModelImporter at {assetPath}.");
            }

            if (!importer.bakeAxisConversion)
            {
                return;
            }

            importer.bakeAxisConversion = false;
            importer.SaveAndReimport();
        }

        private static void KeepOnlyCoffeeLod0(GameObject root)
        {
            var lodGroup = root.GetComponentInChildren<LODGroup>(true);
            if (lodGroup == null)
            {
                return;
            }

            var lods = lodGroup.GetLODs();
            if (lods.Length == 0)
            {
                throw new InvalidOperationException("Coffee Machine LODGroup has no LOD0.");
            }

            var lod0Renderers = new HashSet<Renderer>(lods[0].renderers);
            var removedObjects = new HashSet<GameObject>();
            foreach (var renderer in lods.Skip(1)
                .SelectMany(lod => lod.renderers)
                .Where(renderer => renderer != null && !lod0Renderers.Contains(renderer)))
            {
                if (removedObjects.Add(renderer.gameObject))
                {
                    UnityEngine.Object.DestroyImmediate(renderer.gameObject);
                }
            }

            UnityEngine.Object.DestroyImmediate(lodGroup);
        }

        private static void PublishCatalogueAtomically(
            IReadOnlyList<FurnitureDefinitionAsset> definitions)
        {
            var candidate = ScriptableObject.CreateInstance<FurnitureContentCatalog>();
            try
            {
                SetCatalogueEntries(candidate, definitions);
                candidate.BuildRuntimeCatalog();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(candidate);
            }

            var catalogue = AssetDatabase.LoadAssetAtPath<FurnitureContentCatalog>(CataloguePath);
            if (catalogue == null)
            {
                catalogue = ScriptableObject.CreateInstance<FurnitureContentCatalog>();
                AssetDatabase.CreateAsset(catalogue, CataloguePath);
            }

            SetCatalogueEntries(catalogue, definitions);
        }

        private static void SetCatalogueEntries(
            FurnitureContentCatalog catalogue,
            IReadOnlyList<FurnitureDefinitionAsset> definitions)
        {
            var serialized = new SerializedObject(catalogue);
            var entries = serialized.FindProperty("entries");
            entries.arraySize = definitions.Count;
            for (var index = 0; index < definitions.Count; index++)
            {
                entries.GetArrayElementAtIndex(index).objectReferenceValue = definitions[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material EnsureMaterialFromSource(
            string sourcePath,
            string destinationPath)
        {
            var source = RequireAsset<Material>(sourcePath, "approved P3 benchmark Material");
            var material = AssetDatabase.LoadAssetAtPath<Material>(destinationPath);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, destinationPath);
            }
            else
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
            }

            material.name = Path.GetFileNameWithoutExtension(destinationPath);
            material.SetFloat("_Surface", 0f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureCashRegisterMaterial()
        {
            var destinationPath = MaterialFolderPath + "/M_Equipment_CashRegister_01.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(destinationPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ??
                throw new InvalidOperationException("Universal Render Pipeline/Lit is required.");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, destinationPath);
            }

            material.name = "M_Equipment_CashRegister_01";
            material.shader = shader;
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture(
                "_BaseMap",
                RequireAsset<Texture2D>(CashRegisterTexturePath, "Cash Register Base Color"));
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void AssignMaterial(GameObject root, Material material)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var mesh = renderer is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh
                    : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                var count = mesh == null ? Math.Max(1, renderer.sharedMaterials.Length) :
                    Math.Max(1, mesh.subMeshCount);
                renderer.sharedMaterials = Enumerable.Repeat(material, count).ToArray();
            }
        }

        private static void RemovePhase4SpatialMarkers(GameObject root)
        {
            foreach (var marker in root.GetComponentsInChildren<SurfaceSlotMarker>(true))
            {
                UnityEngine.Object.DestroyImmediate(marker.gameObject);
            }

            foreach (var marker in root.GetComponentsInChildren<CashRegisterSideMarker>(true))
            {
                UnityEngine.Object.DestroyImmediate(marker.gameObject);
            }

            var forwardMarkers = root.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform != root.transform &&
                    string.Equals(transform.name, "ForwardMarker", StringComparison.Ordinal))
                .ToArray();
            foreach (var marker in forwardMarkers)
            {
                UnityEngine.Object.DestroyImmediate(marker.gameObject);
            }
        }

        private static void AddCoffeeForwardMarker(GameObject root)
        {
            var bounds = GetVisibleBounds(root);
            var marker = new GameObject("ForwardMarker");
            marker.transform.SetParent(root.transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.05f, bounds.max.z + 0.05f);
            marker.transform.localRotation = Quaternion.identity;
        }

        private static void AddCashRegisterSides(GameObject root)
        {
            var bounds = GetVisibleBounds(root);
            AddCashRegisterSide(
                root.transform,
                "EmployeeSide",
                CashRegisterSideType.Employee,
                CardinalDirection.North,
                new Vector3(0f, 0f, bounds.max.z + 0.05f));
            AddCashRegisterSide(
                root.transform,
                "CustomerSide",
                CashRegisterSideType.Customer,
                CardinalDirection.South,
                new Vector3(0f, 0f, bounds.min.z - 0.05f));
        }

        private static void AddCashRegisterSide(
            Transform parent,
            string name,
            CashRegisterSideType sideType,
            CardinalDirection direction,
            Vector3 localPosition)
        {
            var markerObject = new GameObject(name);
            markerObject.transform.SetParent(parent, false);
            markerObject.transform.localPosition = localPosition;
            markerObject.transform.localRotation = Quaternion.identity;
            var marker = markerObject.AddComponent<CashRegisterSideMarker>();
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("sideType").intValue = (int)sideType;
            serialized.FindProperty("localDirection").intValue = (int)direction;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSurfaceSlot(
            Transform parent,
            string slotId,
            float localY,
            Vector3? localOffset = null)
        {
            var markerObject = new GameObject(slotId);
            markerObject.transform.SetParent(parent, false);
            var offset = localOffset ?? Vector3.zero;
            markerObject.transform.localPosition = new Vector3(offset.x, localY, offset.z);
            markerObject.transform.localRotation = Quaternion.identity;
            var marker = markerObject.AddComponent<SurfaceSlotMarker>();
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("slotId").stringValue = slotId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Bounds GetVisibleBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                .ToArray();
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException($"{root.name} has no visible Renderer.");
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static void AddBoundsCollider(GameObject root, Bounds bounds)
        {
            var collider = root.AddComponent<BoxCollider>();
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = bounds.size;
            collider.isTrigger = false;
        }

        private static void ResetRootTransform(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private static T RequireAsset<T>(string assetPath, string label)
            where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                throw new InvalidOperationException($"Missing {label}: {assetPath}");
            }

            return asset;
        }

        private static void EnsureAssetFolder(string assetFolderPath)
        {
            var segments = assetFolderPath.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static string GetAbsoluteProjectPath(string projectRelativePath)
        {
            return Path.GetFullPath(Path.Combine(
                ProjectRootPath,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static string RequireSourceHash(string path, string expectedSha256)
        {
            RequireFile(path, "protected raw source");
            string actual;
            using (var stream = File.OpenRead(path))
            using (var sha256 = SHA256.Create())
            {
                actual = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
            }

            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Protected source hash mismatch at {path}. Expected {expectedSha256}; actual {actual}.");
            }

            return actual;
        }

        private static void RequireFile(string path, string label)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Missing {label}: {path}", path);
            }
        }
    }
}

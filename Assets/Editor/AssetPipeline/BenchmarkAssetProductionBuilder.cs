using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.AssetPipeline
{
    /// <summary>
    /// Rebuilds the three approved benchmark Prefabs from the production FBX files.
    /// This tool deliberately owns only generated Materials and Prefab assembly;
    /// mesh shape, pivot, scale, and forward direction stay in Blender/FBX.
    /// </summary>
    public static class BenchmarkAssetProductionBuilder
    {
        private const string RootPath = "Assets/Art/VisualPipeline/Benchmarks";
        private const string ModelPath = RootPath + "/Models";
        private const string MaterialPath = RootPath + "/Materials";
        private const string PrefabPath = RootPath + "/Prefabs";

        [MenuItem("AnimalCafe/Asset Pipeline/Build Benchmark Production Assets")]
        public static void Build()
        {
            EnsureFolder(ModelPath);
            EnsureFolder(MaterialPath);
            EnsureFolder(PrefabPath);
            AssetDatabase.Refresh();

            var warmWood = EnsureMaterial("M_Benchmark_WarmWood_01", new Color(0.42f, 0.20f, 0.08f));
            var sageMetal = EnsureMaterial("M_Benchmark_SageMetal_01", new Color(0.14f, 0.25f, 0.18f));
            var creamCeramic = EnsureMaterial("M_Benchmark_CreamCeramic_01", new Color(0.87f, 0.80f, 0.65f));
            EnsureMaterial("M_Benchmark_HoneyAccent_01", new Color(0.92f, 0.55f, 0.08f));

            CreateSimplePrefab(
                "PF_Benchmark_WorkTable_01",
                "SM_Benchmark_WorkTable_01",
                "SM_Benchmark_WorkTable_01",
                new[] { warmWood },
                new Vector3(0.90f, 0.65f, 0.90f));
            CreateCoffeeMachinePrefab(creamCeramic, sageMetal);
            CreateSimplePrefab(
                "PF_Benchmark_CeramicCup_01",
                "SM_Benchmark_CeramicCup_01",
                "SM_Benchmark_CeramicCup_01",
                new[] { creamCeramic },
                new Vector3(0.14f, 0.16f, 0.14f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("AnimalCafe/Asset Pipeline/Log Benchmark Asset Diagnostics")]
        public static void LogDiagnostics()
        {
            LogMeshDiagnostics("SM_Benchmark_WorkTable_01", "SM_Benchmark_WorkTable_01");
            LogMeshDiagnostics("SM_Benchmark_CoffeeMachine_01", "SM_Benchmark_CoffeeMachine_01_LOD0");
            LogMeshDiagnostics("SM_Benchmark_CoffeeMachine_01", "SM_Benchmark_CoffeeMachine_01_LOD1");
            LogMeshDiagnostics("SM_Benchmark_CeramicCup_01", "SM_Benchmark_CeramicCup_01");

            foreach (var prefabName in new[]
            {
                "PF_Benchmark_WorkTable_01",
                "PF_Benchmark_CoffeeMachine_01",
                "PF_Benchmark_CeramicCup_01"
            })
            {
                var assetPath = $"{PrefabPath}/{prefabName}.prefab";
                var root = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                    var colliders = root.GetComponentsInChildren<Collider>(true);
                    var lodGroups = root.GetComponentsInChildren<LODGroup>(true);
                    var colliderDetails = string.Join(",", colliders.Select(collider =>
                        collider is BoxCollider box
                            ? $"Box(center={box.center},size={box.size})"
                            : collider.GetType().Name));
                    Debug.Log(
                        $"TASK5_DIAGNOSTIC prefab={prefabName} " +
                        $"renderers={renderers.Length} colliders={colliders.Length} " +
                        $"lodGroups={lodGroups.Length} lodCount=" +
                        $"{string.Join(",", lodGroups.Select(group => group.GetLODs().Length))} " +
                        $"colliderDetails={colliderDetails}");

                    foreach (var renderer in renderers)
                    {
                        var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                        var texturePaths = renderer.sharedMaterials
                            .Where(material => material != null)
                            .SelectMany(material => material.GetTexturePropertyNames()
                                .Select(propertyName => AssetDatabase.GetAssetPath(material.GetTexture(propertyName))))
                            .Where(path => !string.IsNullOrEmpty(path))
                            .Distinct()
                            .ToArray();
                        Debug.Log(
                            $"TASK5_DIAGNOSTIC prefab={prefabName} renderer={renderer.name} " +
                            $"localPosition={renderer.transform.localPosition} " +
                            $"localRotation={renderer.transform.localRotation.eulerAngles} " +
                            $"meshBounds={mesh.bounds} triangles={mesh.triangles.Length / 3} " +
                            $"materialSlots={renderer.sharedMaterials.Length} " +
                            $"uniqueMaterials={renderer.sharedMaterials.Where(material => material != null).Distinct().Count()} " +
                            $"textureReferences={texturePaths.Length} worldBounds={renderer.bounds}");
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void CreateSimplePrefab(
            string prefabName,
            string fbxStem,
            string meshName,
            Material[] materials,
            Vector3 colliderSize)
        {
            var root = new GameObject(prefabName);
            try
            {
                CreateVisual(root.transform, "Visual", fbxStem, meshName, materials);
                AddForwardMarker(root.transform, colliderSize.z * 0.5f + 0.05f);
                AddBoxCollider(root, colliderSize);
                SavePrefab(root, prefabName);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateCoffeeMachinePrefab(Material creamCeramic, Material sageMetal)
        {
            const string prefabName = "PF_Benchmark_CoffeeMachine_01";
            var root = new GameObject(prefabName);
            try
            {
                var visual = InstantiateModel(root.transform, "Visual", "SM_Benchmark_CoffeeMachine_01");
                var renderers = visual.GetComponentsInChildren<MeshRenderer>(true);
                var lod0 = FindRenderer(renderers, "SM_Benchmark_CoffeeMachine_01_LOD0");
                var lod1 = FindRenderer(renderers, "SM_Benchmark_CoffeeMachine_01_LOD1");
                lod0.enabled = true;
                lod1.enabled = true;
                lod0.sharedMaterials = new[] { creamCeramic, sageMetal };
                lod1.sharedMaterials = new[] { sageMetal };
                if (visual.GetComponentsInChildren<LODGroup>(true).Length != 1)
                {
                    throw new InvalidOperationException("Coffee Machine FBX must import exactly one LODGroup.");
                }

                AddForwardMarker(root.transform, 0.32f);
                AddBoxCollider(root, new Vector3(0.65f, 0.62f, 0.50f));
                SavePrefab(root, prefabName);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static MeshRenderer CreateVisual(
            Transform parent,
            string name,
            string fbxStem,
            string meshName,
            Material[] materials)
        {
            var visual = InstantiateModel(parent, name, fbxStem);
            var renderers = visual.GetComponentsInChildren<MeshRenderer>(true);
            var renderer = FindRenderer(renderers, meshName);

            foreach (var candidate in renderers)
            {
                candidate.enabled = candidate == renderer;
            }

            renderer.sharedMaterials = materials;
            return renderer;
        }

        private static GameObject InstantiateModel(Transform parent, string name, string fbxStem)
        {
            var modelPath = $"{ModelPath}/{fbxStem}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                throw new InvalidOperationException($"Expected imported model at {modelPath}.");
            }

            var visual = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (visual == null)
            {
                throw new InvalidOperationException($"Could not instantiate imported model at {modelPath}.");
            }

            visual.name = name;
            visual.transform.SetParent(parent, false);
            return visual;
        }

        private static MeshRenderer FindRenderer(MeshRenderer[] renderers, string meshName)
        {
            var renderer = renderers.SingleOrDefault(candidate =>
            {
                var meshFilter = candidate.GetComponent<MeshFilter>();
                return meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.name == meshName;
            });
            if (renderer == null)
            {
                throw new InvalidOperationException($"Expected mesh renderer {meshName}.");
            }

            return renderer;
        }

        private static void AddForwardMarker(Transform parent, float localZ)
        {
            var marker = new GameObject("ForwardMarker");
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(0f, 0.05f, localZ);
            marker.transform.localRotation = Quaternion.identity;
        }

        private static void AddBoxCollider(GameObject root, Vector3 size)
        {
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, size.y * 0.5f, 0f);
            collider.size = size;
            collider.isTrigger = false;
        }

        private static void SavePrefab(GameObject root, string prefabName)
        {
            var assetPath = $"{PrefabPath}/{prefabName}.prefab";
            root.name = prefabName;
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
        }

        private static Material EnsureMaterial(string name, Color baseColor)
        {
            var assetPath = $"{MaterialPath}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    throw new InvalidOperationException("Universal Render Pipeline/Lit is required.");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.name = name;
            material.shader = Shader.Find("Universal Render Pipeline/Lit");
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Smoothness", 0.18f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh LoadMesh(string fbxStem, string meshName)
        {
            var fbxPath = $"{ModelPath}/{fbxStem}.fbx";
            var mesh = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<Mesh>()
                .SingleOrDefault(candidate => candidate.name == meshName);
            if (mesh == null)
            {
                throw new InvalidOperationException($"Expected mesh {meshName} in {fbxPath}.");
            }

            return mesh;
        }

        private static void LogMeshDiagnostics(string fbxStem, string meshName)
        {
            var mesh = LoadMesh(fbxStem, meshName);
            Debug.Log($"TASK5_DIAGNOSTIC fbx={fbxStem} mesh={meshName} bounds={mesh.bounds}");
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidOperationException($"Cannot create asset folder {assetFolderPath}.");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolderPath));
        }
    }
}

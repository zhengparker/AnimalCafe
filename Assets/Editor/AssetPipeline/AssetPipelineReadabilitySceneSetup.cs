using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalCafe.EditorTools.AssetPipeline
{
    /// <summary>
    /// Builds the isolated benchmark readability fixture. It owns only the
    /// validation Scene and never changes gameplay Scenes or Build Settings.
    /// </summary>
    public static class AssetPipelineReadabilitySceneSetup
    {
        private const string ScenePath =
            "Assets/Scenes/Validation/AssetPipelineReadability.unity";
        private const string PrefabFolderPath =
            "Assets/Art/VisualPipeline/Benchmarks/Prefabs";
        private const string NeutralMaterialPath =
            "Assets/Art/VisualPipeline/Benchmarks/Materials/M_Benchmark_CreamCeramic_01.mat";

        [MenuItem("AnimalCafe/Validation/Build Asset Readability Scene")]
        public static void BuildScene()
        {
            EnsureFolder(Path.GetDirectoryName(ScenePath)?.Replace('\\', '/'));

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            scene.name = "AssetPipelineReadability";

            var root = new GameObject("AssetReadabilityRoot");
            CreateCamera(root.transform);
            CreateSingleAssetDisplay(root.transform);
            CreateBatchDisplay(root.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void CreateCamera(Transform parent)
        {
            var cameraRoot = new GameObject("CameraRoot");
            cameraRoot.transform.SetParent(parent, false);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.transform.SetParent(cameraRoot.transform, false);
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(-10f, 10f, -10f),
                Quaternion.Euler(35.264f, 45f, 0f));
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.12f, 0.15f, 0.18f, 1f);
        }

        private static void CreateSingleAssetDisplay(Transform parent)
        {
            var display = new GameObject("SingleAssetDisplay");
            display.transform.SetParent(parent, false);

            InstantiateBenchmarkPrefab("WorkTable", display.transform,
                new Vector3(0f, 0f, 0f));
            InstantiateBenchmarkPrefab("CoffeeMachine", display.transform,
                new Vector3(-0.12f, 0.65f, 0f));
            InstantiateBenchmarkPrefab("CeramicCup", display.transform,
                new Vector3(0.25f, 0.65f, 0.16f));
            CreateCharacterScaleReference(display.transform,
                new Vector3(1.25f, 0f, 0f));
        }

        private static void CreateBatchDisplay(Transform parent)
        {
            var batch = new GameObject("BatchDisplay");
            batch.transform.SetParent(parent, false);
            batch.transform.localPosition = new Vector3(-5f, 0f, 8f);

            CreateBatchGroup(batch.transform, "WorkTables_20", "WorkTable", 0f);
            CreateBatchGroup(batch.transform, "Machines_20", "CoffeeMachine", 10f);
            CreateBatchGroup(batch.transform, "Cups_20", "CeramicCup", 20f);
        }

        private static void CreateBatchGroup(
            Transform parent,
            string groupName,
            string prefabStem,
            float groupOffsetZ)
        {
            var group = new GameObject(groupName);
            group.transform.SetParent(parent, false);
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 5; column++)
                {
                    InstantiateBenchmarkPrefab(
                        prefabStem,
                        group.transform,
                        new Vector3(column * 2f, 0f, groupOffsetZ + row * 2f));
                }
            }
        }

        private static void CreateCharacterScaleReference(
            Transform parent,
            Vector3 localPosition)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(NeutralMaterialPath);
            if (material == null || material.shader == null ||
                material.shader.name != "Universal Render Pipeline/Lit")
            {
                throw new InvalidOperationException(
                    "The neutral character reference requires the shared URP Lit material.");
            }

            var reference = new GameObject("CharacterScaleReference_1_30m");
            reference.transform.SetParent(parent, false);
            reference.transform.localPosition = localPosition;

            var silhouette = GameObject.CreatePrimitive(PrimitiveType.Cube);
            silhouette.name = "NeutralSilhouette";
            silhouette.transform.SetParent(reference.transform, false);
            silhouette.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            silhouette.transform.localScale = new Vector3(0.35f, 1.30f, 0.18f);
            var collider = silhouette.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            silhouette.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void InstantiateBenchmarkPrefab(
            string prefabStem,
            Transform parent,
            Vector3 localPosition)
        {
            var prefabName = $"PF_Benchmark_{prefabStem}_01";
            var prefabPath = $"{PrefabFolderPath}/{prefabName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Expected benchmark Prefab at {prefabPath}.");
            }

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Could not instantiate benchmark Prefab at {prefabPath}.");
            }

            instance.name = prefabName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
        }

        private static void EnsureFolder(string assetFolderPath)
        {
            if (string.IsNullOrEmpty(assetFolderPath) ||
                AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidOperationException(
                    $"Cannot create asset folder {assetFolderPath}.");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolderPath));
        }
    }
}

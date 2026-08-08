using System;
using AnimalCafe.Diagnostics;
using AnimalCafe.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalCafe.EditorTools.Phase4
{
    /// <summary>
    /// Creates the explicitly temporary MainCafe objects used for manual Phase 4 review.
    /// 通过 Unity Editor API 可重复生成 MainCafe 的临时人工检查物件。
    /// </summary>
    public static class MainCafeManualReviewFixtureSetup
    {
        private const string MainCafeScenePath = "Assets/Scenes/MainCafe.unity";
        private const string RootName = "TEMP_P4_ManualReviewFixtures_DELETE_LATER";
        private const string MovingCubeName = "ReviewCube_Moving";
        private const string StaticCubeName = "ReviewCube_Static";
        private const string MovingMaterialPath =
            "Assets/Art/Phase4/Materials/M_TEMP_ManualReviewCube_Moving.mat";
        private const string StaticMaterialPath =
            "Assets/Art/Phase4/Materials/M_TEMP_ManualReviewCube_Static.mat";

        private static readonly Vector3 MovingPointA =
            new(-2f, 0.5f, -1f);
        private static readonly Vector3 MovingPointB =
            new(2f, 0.5f, -1f);
        private static readonly Vector3 StaticCubePosition =
            new(0.5f, 0.5f, 2f);
        private static readonly Color MovingColor =
            new(0.92f, 0.36f, 0.20f, 1f);
        private static readonly Color StaticColor =
            new(0.32f, 0.62f, 0.42f, 1f);

        /// <summary>
        /// Command-line entry point. Rebuilds the exact temporary fixture hierarchy.
        /// 命令行入口：重新生成固定名称的临时检查层级。
        /// </summary>
        [MenuItem("AnimalCafe/Phase 4/Add MainCafe Manual Review Cubes")]
        public static void Apply()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Required shader 'Universal Render Pipeline/Lit' was not found.");
            }

            var mainCafeScene = EditorSceneManager.OpenScene(
                MainCafeScenePath,
                OpenSceneMode.Single);
            if (mainCafeScene.path != MainCafeScenePath)
            {
                throw new InvalidOperationException(
                    $"Refusing to save unexpected Scene '{mainCafeScene.path}'.");
            }

            var movingMaterial = CreateOrUpdateMaterial(
                MovingMaterialPath,
                shader,
                MovingColor);
            var staticMaterial = CreateOrUpdateMaterial(
                StaticMaterialPath,
                shader,
                StaticColor);

            DeleteExistingFixtureRoots(mainCafeScene);

            var root = new GameObject(RootName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            CreateMovingCube(root.transform, movingMaterial);
            CreateStaticCube(root.transform, staticMaterial);

            EditorSceneManager.MarkSceneDirty(mainCafeScene);
            if (!EditorSceneManager.SaveScene(mainCafeScene))
            {
                throw new InvalidOperationException(
                    $"Failed to save '{MainCafeScenePath}'.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"[MainCafeManualReviewFixtureSetup] Saved {RootName} with two children.");
        }

        private static Material CreateOrUpdateMaterial(
            string path,
            Shader shader,
            Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void DeleteExistingFixtureRoots(Scene mainCafeScene)
        {
            foreach (var root in mainCafeScene.GetRootGameObjects())
            {
                if (root.name == RootName)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private static void CreateMovingCube(Transform root, Material material)
        {
            var cube = CreateCube(
                MovingCubeName,
                root,
                MovingPointA,
                material);
            var mover = cube.AddComponent<ManualReviewPingPongMover>();
            mover.Configure(MovingPointA, MovingPointB, 1.5f);
        }

        private static void CreateStaticCube(Transform root, Material material)
        {
            CreateCube(
                StaticCubeName,
                root,
                StaticCubePosition,
                material);
        }

        private static GameObject CreateCube(
            string name,
            Transform root,
            Vector3 localPosition,
            Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(root, false);
            cube.transform.localPosition = localPosition;
            var meshRenderer = cube.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;

            var selectable = cube.AddComponent<ColorSelectable>();
            selectable.Configure(meshRenderer);
            return cube;
        }
    }
}

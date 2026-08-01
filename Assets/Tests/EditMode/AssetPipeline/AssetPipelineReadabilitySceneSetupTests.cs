using System.Linq;
using AnimalCafe.EditorTools.AssetPipeline;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.EditMode.AssetPipeline
{
    public sealed class AssetPipelineReadabilitySceneSetupTests
    {
        private const string ScenePath =
            "Assets/Scenes/Validation/AssetPipelineReadability.unity";
        private const string MainCafePath = "Assets/Scenes/MainCafe.unity";

        [Test]
        public void Setup_CreatesDedicatedValidationScene()
        {
            AssetPipelineReadabilitySceneSetup.BuildScene();

            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath),
                Is.Not.Null);
        }

        [Test]
        public void Setup_CreatesOneOrthographicIsometricCamera()
        {
            var scene = BuildAndOpenScene();
            var cameras = Object.FindObjectsByType<UnityEngine.Camera>(
                FindObjectsInactive.Include);

            Assert.That(cameras, Has.Length.EqualTo(1));
            Assert.That(cameras[0].orthographic, Is.True);
            Assert.That(cameras[0].orthographicSize, Is.EqualTo(7f));
            Assert.That(cameras[0].transform.rotation.eulerAngles.x,
                Is.EqualTo(35.264f).Within(0.01f));
            Assert.That(cameras[0].transform.rotation.eulerAngles.y,
                Is.EqualTo(45f).Within(0.01f));
            Assert.That(scene.name, Is.EqualTo("AssetPipelineReadability"));
        }

        [Test]
        public void Setup_CreatesOneSingleAssetDisplayRoot()
        {
            var scene = BuildAndOpenScene();

            Assert.That(FindNamedObjects(scene, "SingleAssetDisplay"), Has.Length.EqualTo(1));
        }

        [Test]
        public void Setup_CreatesOneCharacterScaleReferenceAtOnePointThreeMeters()
        {
            var scene = BuildAndOpenScene();
            var references = FindNamedObjects(scene, "CharacterScaleReference_1_30m");

            Assert.That(references, Has.Length.EqualTo(1));
            var renderers = references[0].GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            var bounds = CalculateBounds(renderers);
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(bounds.size.y, Is.EqualTo(1.30f).Within(0.001f));
            Assert.That(references[0].GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(references[0].GetComponentsInChildren<Animator>(true), Is.Empty);
        }

        [Test]
        public void Setup_CreatesTwentyInstancesOfEachBenchmarkInBatchRoot()
        {
            var scene = BuildAndOpenScene();
            var batchRoot = FindNamedObjects(scene, "BatchDisplay");

            Assert.That(batchRoot, Has.Length.EqualTo(1));
            Assert.That(CountPrefabInstances(batchRoot[0].transform, "PF_Benchmark_WorkTable_01"), Is.EqualTo(20));
            Assert.That(CountPrefabInstances(batchRoot[0].transform, "PF_Benchmark_CoffeeMachine_01"), Is.EqualTo(20));
            Assert.That(CountPrefabInstances(batchRoot[0].transform, "PF_Benchmark_CeramicCup_01"), Is.EqualTo(20));
        }

        [Test]
        public void Setup_RepeatedRunDoesNotDuplicateObjects()
        {
            AssetPipelineReadabilitySceneSetup.BuildScene();
            AssetPipelineReadabilitySceneSetup.BuildScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Assert.That(FindNamedObjects(scene, "AssetReadabilityRoot"), Has.Length.EqualTo(1));
            Assert.That(FindNamedObjects(scene, "Main Camera"), Has.Length.EqualTo(1));
            Assert.That(FindNamedObjects(scene, "SingleAssetDisplay"), Has.Length.EqualTo(1));
            Assert.That(FindNamedObjects(scene, "BatchDisplay"), Has.Length.EqualTo(1));
            Assert.That(CountPrefabInstances(scene, "PF_Benchmark_WorkTable_01"), Is.EqualTo(21));
            Assert.That(CountPrefabInstances(scene, "PF_Benchmark_CoffeeMachine_01"), Is.EqualTo(21));
            Assert.That(CountPrefabInstances(scene, "PF_Benchmark_CeramicCup_01"), Is.EqualTo(21));
        }

        [Test]
        public void Setup_DoesNotModifyMainCafeScene()
        {
            var before = AssetDatabase.GetAssetDependencyHash(MainCafePath);

            AssetPipelineReadabilitySceneSetup.BuildScene();

            Assert.That(AssetDatabase.GetAssetDependencyHash(MainCafePath), Is.EqualTo(before));
        }

        private static Scene BuildAndOpenScene()
        {
            AssetPipelineReadabilitySceneSetup.BuildScene();
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static GameObject[] FindNamedObjects(Scene scene, string name)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(candidate => candidate.name == name)
                .Select(candidate => candidate.gameObject)
                .ToArray();
        }

        private static int CountPrefabInstances(Scene scene, string prefabName)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Count(candidate => candidate.name == prefabName);
        }

        private static int CountPrefabInstances(Transform root, string prefabName)
        {
            return root.GetComponentsInChildren<Transform>(true)
                .Count(candidate => candidate.name == prefabName);
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }
    }
}

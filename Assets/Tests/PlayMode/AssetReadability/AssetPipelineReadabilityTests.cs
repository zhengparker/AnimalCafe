using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode.AssetReadability
{
    public sealed class AssetPipelineReadabilityTests : IPrebuildSetup, IPostBuildCleanup
    {
        private const string ScenePath =
            "Assets/Scenes/Validation/AssetPipelineReadability.unity";

        public void Setup()
        {
            if (EditorBuildSettings.scenes.Any(scene => scene.path == ScenePath))
            {
                return;
            }

            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) })
                .ToArray();
        }

        public void Cleanup()
        {
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Where(scene => scene.path != ScenePath)
                .ToArray();
        }

        [UnityTest]
        public IEnumerator ReadabilityScene_LoadsWithoutMissingBenchmarkReferences()
        {
            yield return LoadScene();

            var prefabRoots = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include)
                .Where(candidate => candidate.name.StartsWith("PF_Benchmark_"))
                .ToArray();
            Assert.That(prefabRoots, Has.Length.EqualTo(63));
            Assert.That(prefabRoots.SelectMany(root => root.GetComponentsInChildren<Renderer>(true)),
                Has.None.Null);
        }

        [UnityTest]
        public IEnumerator ReadabilityScene_ContainsExactlySixtyBatchInstances()
        {
            yield return LoadScene();

            var batchRoot = GameObject.Find("BatchDisplay");
            Assert.That(batchRoot, Is.Not.Null);
            Assert.That(batchRoot.GetComponentsInChildren<Transform>(true)
                .Count(candidate => candidate.name.StartsWith("PF_Benchmark_")), Is.EqualTo(60));
        }

        [UnityTest]
        public IEnumerator ReadabilityScene_CharacterScaleReferenceIsOnePointThreeMetersHigh()
        {
            yield return LoadScene();

            var reference = GameObject.Find("CharacterScaleReference_1_30m");
            Assert.That(reference, Is.Not.Null);
            var renderers = reference.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Is.Not.Empty);
            var bounds = CalculateBounds(renderers);
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(bounds.size.y, Is.EqualTo(1.30f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator ReadabilityScene_CameraIsOrthographicAndUsesSizeSeven()
        {
            yield return LoadScene();

            var cameras = Object.FindObjectsByType<UnityEngine.Camera>(
                FindObjectsInactive.Include);
            Assert.That(cameras, Has.Length.EqualTo(1));
            Assert.That(cameras[0].orthographic, Is.True);
            Assert.That(cameras[0].orthographicSize, Is.EqualTo(7f));
        }

        [UnityTest]
        public IEnumerator ReadabilityScene_AllRenderersUseUrpLitMaterials()
        {
            yield return LoadScene();

            var materials = Object.FindObjectsByType<Renderer>(
                    FindObjectsInactive.Include)
                .SelectMany(renderer => renderer.sharedMaterials)
                .Where(material => material != null)
                .ToArray();
            Assert.That(materials, Is.Not.Empty);
            Assert.That(materials,
                Has.All.Matches<Material>(material =>
                    material.shader.name == "Universal Render Pipeline/Lit"));
        }

        [UnityTest]
        public IEnumerator ReadabilityScene_CoffeeMachineHasTwoValidLodLevels()
        {
            yield return LoadScene();

            var machine = GameObject.Find("PF_Benchmark_CoffeeMachine_01");
            Assert.That(machine, Is.Not.Null);
            var lodGroups = machine.GetComponentsInChildren<LODGroup>(true);
            Assert.That(lodGroups, Has.Length.EqualTo(1));
            var lods = lodGroups[0].GetLODs();
            Assert.That(lods, Has.Length.EqualTo(2));
            Assert.That(lods.All(lod => lod.renderers.Length > 0), Is.True);
        }

        [UnityTest]
        public IEnumerator ReadabilityScene_ProducesNoUnexpectedErrorLogs()
        {
            yield return LoadScene();
            LogAssert.NoUnexpectedReceived();
        }

        private static IEnumerator LoadScene()
        {
            var operation = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(operation, Is.Not.Null);
            yield return new WaitUntil(() => operation.isDone);
            yield return null;
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

using System.Collections;
using System.Linq;
using NUnit.Framework;
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
            AssetPipelineReadabilityBuildSettingsScope.Setup();
        }

        public void Cleanup()
        {
            AssetPipelineReadabilityBuildSettingsScope.Cleanup();
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
            Assert.That(prefabRoots.Count(root => root.name == "PF_Benchmark_WorkTable_01"), Is.EqualTo(21));
            Assert.That(prefabRoots.Count(root => root.name == "PF_Benchmark_CoffeeMachine_01"), Is.EqualTo(21));
            Assert.That(prefabRoots.Count(root => root.name == "PF_Benchmark_CeramicCup_01"), Is.EqualTo(21));

            var meshRenderers = prefabRoots
                .SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true))
                .ToArray();
            var skinnedRenderers = prefabRoots
                .SelectMany(root => root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                .ToArray();
            var renderers = meshRenderers.Cast<Renderer>()
                .Concat(skinnedRenderers)
                .ToArray();
            Assert.That(renderers, Is.Not.Empty);
            Assert.That(meshRenderers.Select(renderer =>
                    renderer.GetComponent<MeshFilter>()?.sharedMesh),
                Has.None.Null);
            Assert.That(skinnedRenderers.Select(renderer => renderer.sharedMesh),
                Has.None.Null);
            Assert.That(renderers, Has.All.Matches<Renderer>(renderer =>
                renderer.sharedMaterials.Length > 0 &&
                renderer.sharedMaterials.All(material => material != null)));

            var lods = prefabRoots
                .SelectMany(root => root.GetComponentsInChildren<LODGroup>(true))
                .SelectMany(group => group.GetLODs())
                .ToArray();
            Assert.That(lods, Is.Not.Empty);
            Assert.That(lods, Has.All.Matches<LOD>(lod =>
                lod.renderers.Length > 0 && lod.renderers.All(renderer => renderer != null)));
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .SelectMany(transform => transform.gameObject.GetComponents<MonoBehaviour>()), Has.None.Null);
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

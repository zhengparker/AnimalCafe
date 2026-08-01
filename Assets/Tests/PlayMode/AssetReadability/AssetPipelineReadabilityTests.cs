using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
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
            Assert.That(prefabRoots, Has.Length.EqualTo(64));
            Assert.That(prefabRoots.Count(root => root.name == "PF_Benchmark_WorkTable_01"), Is.EqualTo(22));
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
        public IEnumerator ReadabilityScene_CameraIsOrthographicAndUsesSizeFour()
        {
            yield return LoadScene();

            var cameras = Object.FindObjectsByType<UnityEngine.Camera>(
                FindObjectsInactive.Include);
            Assert.That(cameras, Has.Length.EqualTo(1));
            Assert.That(cameras[0].orthographic, Is.True);
            Assert.That(cameras[0].orthographicSize, Is.EqualTo(4f));
        }

        [UnityTest]
        public IEnumerator ReadabilityScene_CameraUsesPaleYellowSolidColorBackground()
        {
            yield return LoadScene();

            var camera = Object.FindAnyObjectByType<UnityEngine.Camera>();
            Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(camera.backgroundColor,
                Is.EqualTo((Color)new Color32(0xF2, 0xE6, 0xB8, 0xFF)));
        }

        [UnityTest]
        public IEnumerator ReadabilityScene_CameraUsesSceneSpecificSmaaHigh()
        {
            yield return LoadScene();

            var camera = Object.FindAnyObjectByType<UnityEngine.Camera>();
            var cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
            Assert.That(cameraData, Is.Not.Null);
            Assert.That(cameraData.antialiasing,
                Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
            Assert.That(cameraData.antialiasingQuality,
                Is.EqualTo(AntialiasingQuality.High));
            Assert.That(cameraData.renderPostProcessing, Is.True,
                "SMAA requires this validation Camera's post-processing path.");
        }

        [UnityTest]
        public IEnumerator ReadabilityScene_UsesTwoSeparateCenteredTabletopStations()
        {
            yield return LoadScene();

            var display = GameObject.Find("SingleAssetDisplay");
            var tables = display.transform.Cast<Transform>()
                .Where(child => child.name == "PF_Benchmark_WorkTable_01")
                .ToArray();
            var machine = display.transform.Cast<Transform>()
                .Single(child => child.name == "PF_Benchmark_CoffeeMachine_01");
            var cup = display.transform.Cast<Transform>()
                .Single(child => child.name == "PF_Benchmark_CeramicCup_01");

            Assert.That(tables, Has.Length.EqualTo(2));
            Assert.That(machine.localPosition.x,
                Is.EqualTo(tables[0].localPosition.x).Within(0.001f));
            Assert.That(machine.localPosition.z,
                Is.EqualTo(tables[0].localPosition.z).Within(0.001f));
            Assert.That(cup.localPosition.x,
                Is.EqualTo(tables[1].localPosition.x).Within(0.001f));
            Assert.That(cup.localPosition.z,
                Is.EqualTo(tables[1].localPosition.z).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator ReadabilityScene_CharacterReferenceDoesNotOverlapRightStationInCameraView()
        {
            yield return LoadScene();

            var display = GameObject.Find("SingleAssetDisplay");
            var camera = Object.FindAnyObjectByType<UnityEngine.Camera>();
            var rightTable = display.transform.Cast<Transform>()
                .Where(child => child.name == "PF_Benchmark_WorkTable_01")
                .OrderByDescending(child => child.localPosition.x)
                .First();
            var cup = display.transform.Cast<Transform>()
                .Single(child => child.name == "PF_Benchmark_CeramicCup_01");
            var reference = GameObject.Find("CharacterScaleReference_1_30m");

            var stationBounds = CalculateBounds(
                rightTable.GetComponentsInChildren<Renderer>(true));
            stationBounds.Encapsulate(CalculateBounds(
                cup.GetComponentsInChildren<Renderer>(true)));
            var stationRect = ProjectBoundsToViewport(camera, stationBounds);
            var referenceRect = ProjectBoundsToViewport(camera, CalculateBounds(
                reference.GetComponentsInChildren<Renderer>(true)));
            const float readabilityMargin = 0.01f;
            stationRect.xMin -= readabilityMargin;
            stationRect.xMax += readabilityMargin;
            stationRect.yMin -= readabilityMargin;
            stationRect.yMax += readabilityMargin;

            Assert.That(stationRect.Overlaps(referenceRect), Is.False,
                "The reference must not overlap the right table/cup in Camera view.");
        }

        [UnityTest]
        public IEnumerator ReadabilityScene_AllSingleDisplayRenderersFitInsideSizeFourCameraViewport()
        {
            yield return LoadScene();

            var display = GameObject.Find("SingleAssetDisplay");
            var camera = Object.FindAnyObjectByType<UnityEngine.Camera>();
            const float safeMargin = 0.01f;

            Assert.That(camera.orthographicSize, Is.EqualTo(4f));
            foreach (var renderer in display.GetComponentsInChildren<Renderer>(true))
            {
                var viewportRect = ProjectBoundsToViewport(camera, renderer.bounds);
                Assert.That(viewportRect.xMin, Is.GreaterThanOrEqualTo(safeMargin),
                    $"{renderer.name} extends beyond the left safe margin.");
                Assert.That(viewportRect.xMax, Is.LessThanOrEqualTo(1f - safeMargin),
                    $"{renderer.name} extends beyond the right safe margin.");
                Assert.That(viewportRect.yMin, Is.GreaterThanOrEqualTo(safeMargin),
                    $"{renderer.name} extends below the bottom safe margin.");
                Assert.That(viewportRect.yMax, Is.LessThanOrEqualTo(1f - safeMargin),
                    $"{renderer.name} extends above the top safe margin.");
            }
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

        private static Rect ProjectBoundsToViewport(
            UnityEngine.Camera camera,
            Bounds bounds)
        {
            var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var x = -1; x <= 1; x += 2)
            {
                for (var y = -1; y <= 1; y += 2)
                {
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var world = bounds.center + Vector3.Scale(
                            bounds.extents, new Vector3(x, y, z));
                        var viewport = camera.WorldToViewportPoint(world);
                        minimum = Vector2.Min(minimum, viewport);
                        maximum = Vector2.Max(maximum, viewport);
                    }
                }
            }

            return Rect.MinMaxRect(
                minimum.x, minimum.y, maximum.x, maximum.y);
        }
    }
}

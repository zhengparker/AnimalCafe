using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
            var cameras = UnityEngine.Object.FindObjectsByType<UnityEngine.Camera>(
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
        public void Setup_BatchPrefabRendererBoundsDoNotOverlap()
        {
            var scene = BuildAndOpenScene();
            var batchRoot = FindNamedObjects(scene, "BatchDisplay").Single();
            var instances = batchRoot.GetComponentsInChildren<Transform>(true)
                .Where(candidate => candidate.parent != null &&
                    candidate.name.StartsWith("PF_Benchmark_"))
                .Select(candidate => new
                {
                    candidate.name,
                    Bounds = CalculateBounds(
                        candidate.GetComponentsInChildren<Renderer>(true))
                })
                .ToArray();

            Assert.That(instances, Has.Length.EqualTo(60));
            for (var first = 0; first < instances.Length; first++)
            {
                for (var second = first + 1; second < instances.Length; second++)
                {
                    var expanded = instances[first].Bounds;
                    expanded.Expand(0.5f);
                    Assert.That(expanded.Intersects(instances[second].Bounds), Is.False,
                        $"{instances[first].name} overlaps or is closer than 0.25m to {instances[second].name}.");
                }
            }
        }

        [Test]
        public void Setup_CreatesExactApprovedHierarchy()
        {
            var scene = BuildAndOpenScene();
            Assert.That(scene.GetRootGameObjects().Select(root => root.name),
                Is.EquivalentTo(new[] { "AssetReadabilityRoot" }));

            var root = FindNamedObjects(scene, "AssetReadabilityRoot").Single();
            var cameraRoot = FindNamedObjects(scene, "CameraRoot").Single();
            var singleDisplay = FindNamedObjects(scene, "SingleAssetDisplay").Single();
            var batchDisplay = FindNamedObjects(scene, "BatchDisplay").Single();

            AssertDirectChildren(root.transform,
                "CameraRoot", "SingleAssetDisplay", "BatchDisplay");
            AssertDirectChildren(cameraRoot.transform, "Main Camera");
            AssertDirectChildren(singleDisplay.transform,
                "PF_Benchmark_WorkTable_01",
                "PF_Benchmark_CoffeeMachine_01",
                "PF_Benchmark_CeramicCup_01",
                "CharacterScaleReference_1_30m");
            AssertDirectChildren(batchDisplay.transform,
                "WorkTables_20", "Machines_20", "Cups_20");

            AssertBatchGroup(batchDisplay.transform, "WorkTables_20",
                "PF_Benchmark_WorkTable_01");
            AssertBatchGroup(batchDisplay.transform, "Machines_20",
                "PF_Benchmark_CoffeeMachine_01");
            AssertBatchGroup(batchDisplay.transform, "Cups_20",
                "PF_Benchmark_CeramicCup_01");
        }

        [Test]
        public void Setup_DoesNotSaveUnrelatedDirtyAsset()
        {
            const string folder = "Assets/Tests/ReadabilityDirtyAsset";
            const string assetPath = folder + "/Unrelated.png";
            EnsureFolder(folder);
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var metaPath = assetPath + ".meta";
            var before = File.ReadAllText(metaPath);
            try
            {
                var unrelated = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                unrelated.isReadable = !unrelated.isReadable;
                EditorUtility.SetDirty(unrelated);
                var afterDirty = File.ReadAllText(metaPath);
                Assert.That(afterDirty, Is.EqualTo(before),
                    "A: Unsaved TextureImporter edit must not change the raw .meta file.");
                Assert.That(EditorUtility.IsDirty(unrelated), Is.True);

                AssetPipelineReadabilitySceneSetup.BuildScene();

                Assert.That(File.ReadAllText(metaPath), Is.EqualTo(before));
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void Menu_CancelWithDirtySceneLeavesSetupContentAndFileUnchanged()
        {
            const string folder = "Assets/Tests/ReadabilityDirtyScene";
            const string dirtyScenePath = folder + "/DirtyScene.unity";
            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var dirtyScene = default(Scene);
            EnsureFolder(folder);
            try
            {
                dirtyScene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
                dirtyScene.name = "DirtyScene";
                new GameObject("SavedRoot");
                Assert.That(EditorSceneManager.SaveScene(dirtyScene, dirtyScenePath), Is.True);
                var fileHashBefore = ComputeSha256(dirtyScenePath);

                new GameObject("UnsavedRoot");
                EditorSceneManager.MarkSceneDirty(dirtyScene);
                Assert.That(dirtyScene.isDirty, Is.True);
                var setupBefore = CaptureSceneSetup();
                var contentBefore = CaptureSceneContent(dirtyScene);
                var activeSceneBefore = SceneManager.GetActiveScene().path;

                var built = AssetPipelineReadabilitySceneSetup.TryBuildSceneFromMenu(
                    () => false);

                Assert.That(built, Is.False);
                Assert.That(CaptureSceneSetup(), Is.EqualTo(setupBefore));
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(activeSceneBefore));
                Assert.That(dirtyScene.isLoaded, Is.True);
                Assert.That(dirtyScene.isDirty, Is.True);
                Assert.That(CaptureSceneContent(dirtyScene), Is.EqualTo(contentBefore));
                Assert.That(ComputeSha256(dirtyScenePath), Is.EqualTo(fileHashBefore));
            }
            finally
            {
                try
                {
                    if (dirtyScene.IsValid() && dirtyScene.isLoaded && dirtyScene.isDirty)
                    {
                        EditorSceneManager.SaveScene(dirtyScene);
                    }

                    if (originalSetup.Length > 0 && originalSetup.Count(setup => setup.isActive) == 1)
                    {
                        EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                    }
                    else
                    {
                        // Batchmode can begin with an unsaved scene that is absent from SceneSetup.
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    }
                }
                finally
                {
                    AssetDatabase.DeleteAsset(folder);
                }
            }
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
            Assert.That(renderers, Is.Not.Empty,
                "Every benchmark instance must have visible Renderer bounds.");
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private static string[] CaptureSceneSetup()
        {
            return EditorSceneManager.GetSceneManagerSetup()
                .Select(setup => $"{setup.path}|{setup.isLoaded}|{setup.isActive}")
                .ToArray();
        }

        private static string[] CaptureSceneContent(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform =>
                    $"{GetHierarchyPath(transform)}|{transform.localPosition}|" +
                    $"{transform.localRotation}|{transform.localScale}")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private static string ComputeSha256(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var absolutePath = Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            using (var stream = File.OpenRead(absolutePath))
            using (var sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(stream))
                    .Replace("-", string.Empty);
            }
        }

        private static void AssertDirectChildren(Transform parent, params string[] expected)
        {
            Assert.That(parent.Cast<Transform>().Select(child => child.name),
                Is.EquivalentTo(expected),
                $"{parent.name} direct children do not match the approved hierarchy.");
        }

        private static void AssertBatchGroup(
            Transform batchDisplay,
            string groupName,
            string expectedPrefabName)
        {
            var group = batchDisplay.Cast<Transform>()
                .Single(child => child.name == groupName);
            Assert.That(group.Cast<Transform>().Select(child => child.name),
                Is.All.EqualTo(expectedPrefabName));
            Assert.That(group.childCount, Is.EqualTo(20));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

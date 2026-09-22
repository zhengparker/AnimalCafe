using System;
using System.IO;
using System.Linq;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.P8R;
using AnimalCafe.EditorTools.Phase6;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RLegacyGuardTests
    {
        [TestCase("runtime-transform")]
        [TestCase("runtime-missing")]
        [TestCase("runtime-extra-child")]
        [TestCase("runtime-duplicate")]
        [TestCase("environment-extra-child")]
        [TestCase("environment-extra-component")]
        [TestCase("feedback-safe-area")]
        [TestCase("feedback-sibling")]
        [TestCase("action-extra-child")]
        [TestCase("action-face-missing")]
        [TestCase("action-hit-missing")]
        public void ApprovedP8R_CorruptStructureIsRefusedWithoutAnyMutation(string damage)
        {
            var path = "Assets/__P8RGuard_" + Guid.NewGuid().ToString("N") + ".unity";
            var active = SceneManager.GetActiveScene();
            Assert.That(AssetDatabase.CopyAsset("Assets/Scenes/MainCafe.unity", path), Is.True);
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                T Find<T>() where T : Component => scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
                var controller = Find<DecorationModeController>();
                Assert.That(P8RCompleteUiBuilder.HasCompleteWiring(controller), Is.True, "Fixture must begin approved P8R.");
                Assert.That(P8RCompleteUiBuilder.GuardLegacy(controller), Is.True, "The intact graph must be accepted before corruption.");
                var runtime = scene.GetRootGameObjects().Single(root => root.name == "Phase8_FunctionalRuntime");
                var environment = scene.GetRootGameObjects().Single(root => root.name == "P4_Environment");
                var feedback = Find<ValidationMessageView>();
                switch (damage)
                {
                    case "runtime-transform": runtime.transform.position = new Vector3(3, 4, 5); break;
                    case "runtime-missing": UnityEngine.Object.DestroyImmediate(runtime); break;
                    case "runtime-extra-child": new GameObject("Unknown").transform.SetParent(runtime.transform, false); break;
                    case "runtime-duplicate":
                        var duplicate = new GameObject("Phase6_DecorationRuntime");
                        SceneManager.MoveGameObjectToScene(duplicate, scene); break;
                    case "environment-extra-child": new GameObject("Unknown").transform.SetParent(environment.transform, false); break;
                    case "environment-extra-component": environment.AddComponent<BoxCollider>(); break;
                    case "feedback-safe-area": UnityEngine.Object.DestroyImmediate(feedback.transform.parent.GetComponent<SafeAreaContainer>()); break;
                    case "feedback-sibling": feedback.transform.parent.SetAsLastSibling(); break;
                    case "action-extra-child": new GameObject("Unknown", typeof(RectTransform)).transform.SetParent(Find<DecorationActionBarView>().transform, false); break;
                    case "action-face-missing":
                        UnityEngine.Object.DestroyImmediate(Find<DecorationActionBarView>().GetComponentsInChildren<UnityEngine.UI.Button>(true)
                            .Single(button => button.name == "RotateButton").transform.Find("Face").gameObject); break;
                    case "action-hit-missing":
                        UnityEngine.Object.DestroyImmediate(Find<DecorationActionBarView>().GetComponentsInChildren<UnityEngine.UI.Button>(true)
                            .Single(button => button.name == "RotateButton").GetComponent<UnityEngine.UI.Image>()); break;
                }
                EditorSceneManager.MarkSceneDirty(scene);
                var before = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true))
                    .Select(item => item.GetType().FullName + "|" + EditorJsonUtility.ToJson(item)).ToArray();
                var files = new[] { path, path + ".meta", "Assets/Scenes/MainCafe.unity", "Assets/Scenes/MainCafe.unity.meta" }
                    .ToDictionary(file => file, File.ReadAllBytes);
                var setup = EditorSceneManager.GetSceneManagerSetup().Select(item => item.path + "|" + item.isActive + "|" + item.isLoaded).ToArray();
                Assert.That(() => P8RCompleteUiBuilder.GuardLegacy(controller), Throws.InvalidOperationException,
                    "A P8R UI reference graph alone must not silently accept corrupted non-UI structure.");
                Assert.That(scene.isDirty, Is.True, "Rejecting must preserve the test-owned unsaved changes.");
                Assert.That(scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true))
                    .Select(item => item.GetType().FullName + "|" + EditorJsonUtility.ToJson(item)), Is.EqualTo(before));
                Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(item => item.path + "|" + item.isActive + "|" + item.isLoaded), Is.EqualTo(setup));
                foreach (var file in files) Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value), file.Key);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(path);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }

        [Test]
        public void Phase6_P8RNoOpStillValidatesDependenciesBeforeReturning()
        {
            var path = "Assets/Scenes/MainCafe.unity";
            var before = File.ReadAllBytes(path);
            try
            {
                Phase6DecorationSceneSetup.DependencyResolverOverrideForTests = target =>
                    Phase6DecorationSceneSetup.CreateMalformedDependencyForTests(target, Phase6SceneSetupDependency.ContentCatalog);
                var error = Assert.Throws<InvalidOperationException>(Phase6DecorationSceneSetup.ConfigureMainCafe);
                Assert.That(error.Message, Does.Contain("depend").IgnoreCase);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            }
            finally { Phase6DecorationSceneSetup.DependencyResolverOverrideForTests = null; }
        }
    }
}

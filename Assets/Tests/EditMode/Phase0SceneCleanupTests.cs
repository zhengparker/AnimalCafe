using System.IO;
using System.Linq;
using AnimalCafe.Camera;
using AnimalCafe.Core.Time;
using AnimalCafe.EditorTools;
using AnimalCafe.Input;
using AnimalCafe.Interaction;
using AnimalCafe.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.Tests
{
    public sealed class Phase0SceneCleanupTests
    {
        [Test]
        public void ConfigurePhase0Scene_RemovesLegacyDemoAndRemainsIdempotent()
        {
            const string scenePath = "Assets/Scenes/MainCafe.unity";
            var originalBytes = File.ReadAllBytes(scenePath);

            try
            {
                var scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Single);
                RemoveNamedRootObjects(
                    scene,
                    "Phase0_Runtime",
                    "Phase0_TimeControls",
                    "EventSystem");
                new GameObject("Phase0_Demo");
                var inactiveDemoRoot = new GameObject("Phase0_Demo");
                inactiveDemoRoot.SetActive(false);

                var inactiveRuntime = new GameObject("Phase0_Runtime");
                inactiveRuntime.SetActive(false);
                inactiveRuntime.AddComponent<MouseCameraInput>();
                inactiveRuntime.AddComponent<MouseCameraInput>();
                new GameObject("Phase0_Runtime");

                var inactiveCanvas = new GameObject("Phase0_TimeControls");
                inactiveCanvas.SetActive(false);
                new GameObject("Phase0_TimeControls");

                var duplicateUiRoot = new GameObject("UI Root");
                duplicateUiRoot.SetActive(false);

                var inactiveEventSystem = new GameObject("EventSystem");
                inactiveEventSystem.SetActive(false);
                new GameObject("EventSystem");
                EditorSceneManager.SaveScene(scene);

                Phase0SceneSetup.ConfigurePhase0Scene();
                Phase0SceneSetup.ConfigurePhase0Scene();

                var configuredScene = SceneManager.GetSceneByPath(scenePath);
                Assert.That(
                    CountNamedRootObjects(configuredScene, "Phase0_Demo"),
                    Is.Zero);
                Assert.That(
                    CountNamedRootObjects(configuredScene, "Phase0_Runtime"),
                    Is.EqualTo(1));
                Assert.That(
                    CountNamedRootObjects(configuredScene, "Phase0_TimeControls"),
                    Is.Zero);
                Assert.That(
                    CountNamedObjects(configuredScene, "UI Root"),
                    Is.EqualTo(1));
                Assert.That(
                    CountNamedRootObjects(configuredScene, "EventSystem"),
                    Is.Zero);
                AssertCanonicalRoot<MouseCameraInput>(
                    configuredScene,
                    "Phase0_Runtime");
                Assert.That(
                    FindAll<EventSystem>(configuredScene),
                    Has.Length.EqualTo(1));

                var runtime = GetNamedRoot(
                    configuredScene,
                    "Phase0_Runtime");
                AssertSingleComponent<MouseCameraInput>(runtime);
                AssertSingleComponent<CafeCameraController>(runtime);
                AssertSingleComponent<SceneInteractionController>(runtime);
                AssertSingleComponent<GameTimeService>(runtime);

                var uiRoot = FindAll<Transform>(configuredScene)
                    .Single(transform => transform.name == "UI Root");
                Assert.That(uiRoot.gameObject.activeSelf, Is.True);
                var timePanels = uiRoot.GetComponentsInChildren<TimeControlPanel>(true);
                Assert.That(timePanels, Has.Length.EqualTo(1));
                Assert.That(timePanels[0].name, Is.EqualTo("RightRail"));

                var eventSystem = FindAll<EventSystem>(configuredScene).Single().gameObject;
                Assert.That(eventSystem.transform.IsChildOf(uiRoot), Is.True);
                AssertSingleComponent<EventSystem>(eventSystem);
                Assert.That(
                    eventSystem.GetComponents<Component>().Count(
                        component => string.Equals(
                            component.GetType().FullName,
                            "UnityEngine.InputSystem.UI.InputSystemUIInputModule",
                            System.StringComparison.Ordinal)),
                    Is.EqualTo(1));
            }
            finally
            {
                File.WriteAllBytes(scenePath, originalBytes);
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Single);
            }
        }

        private static void AssertCanonicalRoot<T>(
            Scene scene,
            string objectName)
            where T : Component
        {
            var matches = scene.GetRootGameObjects()
                .Where(root => string.Equals(
                    root.name,
                    objectName,
                    System.StringComparison.Ordinal))
                .ToArray();

            Assert.That(matches, Has.Length.EqualTo(1));
            Assert.That(matches[0].activeSelf, Is.True);
            Assert.That(matches[0].GetComponents<T>(), Has.Length.EqualTo(1));
        }

        private static int CountNamedRootObjects(
            Scene scene,
            string objectName)
        {
            var count = 0;
            foreach (var gameObject in scene.GetRootGameObjects())
            {
                if (gameObject.name == objectName)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountNamedObjects(Scene scene, string objectName)
        {
            return FindAll<Transform>(scene).Count(transform =>
                string.Equals(transform.name, objectName, System.StringComparison.Ordinal));
        }

        private static T[] FindAll<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static GameObject GetNamedRoot(
            Scene scene,
            string objectName)
        {
            return scene.GetRootGameObjects().Single(
                root => string.Equals(
                    root.name,
                    objectName,
                    System.StringComparison.Ordinal));
        }

        private static void AssertSingleComponent<T>(GameObject gameObject)
            where T : Component
        {
            Assert.That(
                gameObject.GetComponents<T>(),
                Has.Length.EqualTo(1));
        }

        private static void RemoveNamedRootObjects(
            Scene scene,
            params string[] objectNames)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (objectNames.Contains(
                        root.name,
                        System.StringComparer.Ordinal))
                {
                    Object.DestroyImmediate(root);
                }
            }
        }
    }
}

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
                    Is.EqualTo(1));
                Assert.That(
                    CountNamedRootObjects(configuredScene, "EventSystem"),
                    Is.EqualTo(1));
                AssertCanonicalRoot<MouseCameraInput>(
                    configuredScene,
                    "Phase0_Runtime");
                AssertCanonicalRoot<Canvas>(
                    configuredScene,
                    "Phase0_TimeControls");
                AssertCanonicalRoot<EventSystem>(
                    configuredScene,
                    "EventSystem");

                var runtime = GetNamedRoot(
                    configuredScene,
                    "Phase0_Runtime");
                AssertSingleComponent<MouseCameraInput>(runtime);
                AssertSingleComponent<CafeCameraController>(runtime);
                AssertSingleComponent<SceneInteractionController>(runtime);
                AssertSingleComponent<GameTimeService>(runtime);

                var canvas = GetNamedRoot(
                    configuredScene,
                    "Phase0_TimeControls");
                AssertSingleComponent<RectTransform>(canvas);
                AssertSingleComponent<Canvas>(canvas);
                AssertSingleComponent<CanvasScaler>(canvas);
                AssertSingleComponent<GraphicRaycaster>(canvas);
                var timePanel = canvas.transform.Find("TimePanel");
                Assert.That(timePanel, Is.Not.Null);
                AssertSingleComponent<TimeControlPanel>(
                    timePanel.gameObject);

                var eventSystem = GetNamedRoot(
                    configuredScene,
                    "EventSystem");
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

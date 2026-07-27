using System.IO;
using AnimalCafe.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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
                new GameObject("Phase0_Demo");
                var inactiveDemoRoot = new GameObject("Phase0_Demo");
                inactiveDemoRoot.SetActive(false);
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
            }
            finally
            {
                File.WriteAllBytes(scenePath, originalBytes);
                AssetDatabase.Refresh();
            }
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
    }
}

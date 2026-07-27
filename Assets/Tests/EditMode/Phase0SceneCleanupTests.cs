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
                EditorSceneManager.SaveScene(scene);

                Phase0SceneSetup.ConfigurePhase0Scene();
                Phase0SceneSetup.ConfigurePhase0Scene();

                Assert.That(GameObject.Find("Phase0_Demo"), Is.Null);
                Assert.That(CountNamedObjects("Phase0_Runtime"), Is.EqualTo(1));
                Assert.That(CountNamedObjects("Phase0_TimeControls"), Is.EqualTo(1));
                Assert.That(CountNamedObjects("EventSystem"), Is.EqualTo(1));
            }
            finally
            {
                File.WriteAllBytes(scenePath, originalBytes);
                AssetDatabase.Refresh();
            }
        }

        private static int CountNamedObjects(string objectName)
        {
            var count = 0;
            foreach (var gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject.scene.IsValid()
                    && gameObject.scene.isLoaded
                    && gameObject.name == objectName)
                {
                    count++;
                }
            }

            return count;
        }
    }
}

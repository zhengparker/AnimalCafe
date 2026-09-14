using System;
using System.Linq;
using AnimalCafe.Core.Time;
using AnimalCafe.EditorTools;
using AnimalCafe.EditorTools.Phase5;
using AnimalCafe.Input;
using AnimalCafe.Interaction;
using AnimalCafe.UI;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.Tests.Phase5
{
    public sealed class Phase5MainCafeMigrationTests
    {
        private const string MainCafePath = "Assets/Scenes/MainCafe.unity";
        private AnimalCafe.Tests.EditMode.LegacyMainCafeFixture legacy;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(Phase5UiAssetPaths.ThemePath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(Phase5UiAssetPaths.UiRootPrefabPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimalCafe.Camera.CameraSettings>("Assets/Config/DefaultCameraSettings.asset"), Is.Not.Null);
            legacy = new AnimalCafe.Tests.EditMode.LegacyMainCafeFixture();
        }
        [TearDown] public void RestoreLegacy() { legacy?.Dispose(); legacy = null; }

        [Test]
        public void ConfigurePhase0Target_MigratesTimeControlsIntoOneThemedPhase5UiRoot()
        {
            var scene = EditorSceneManager.OpenScene(MainCafePath, OpenSceneMode.Additive);
            var assetPath = "Assets/__Phase5Caller_" + Guid.NewGuid().ToString("N") + ".mat";
            try
            {
            var callerMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(callerMaterial, assetPath);
            AssetDatabase.SaveAssetIfDirty(callerMaterial);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            callerMaterial = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            var callerBytes = System.IO.File.ReadAllBytes(assetPath);
            callerMaterial.color = Color.magenta;
            EditorUtility.SetDirty(callerMaterial);
            Assert.That(EditorUtility.IsDirty(callerMaterial), Is.True, "Fixture must reach target configuration dirty.");
            Phase0SceneSetup.ConfigurePhase0Scene(scene);
            var uiRoot = FindAll(scene, "UI Root").Single();
            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(Phase5UiAssetPaths.ThemePath);

            Assert.That(FindAll(scene, "Phase0_TimeControls"), Is.Empty);
            Assert.That(FindAll<TimeControlPanel>(scene), Has.Length.EqualTo(1));
            Assert.That(FindAll<EventSystem>(scene), Has.Length.EqualTo(1));
            Assert.That(FindAll<GameTimeService>(scene), Has.Length.EqualTo(1));
            Assert.That(FindAll<MouseCameraInput>(scene), Has.Length.EqualTo(1));
            Assert.That(FindAll<SceneInteractionController>(scene), Has.Length.EqualTo(1));

            var panel = uiRoot.GetComponentsInChildren<TimeControlPanel>(true).Single();
            var buttons = panel.GetComponentsInChildren<Button>(true);
            var timeButtons = buttons.Where(button =>
                button.name == "PauseButton"
                || button.name == "NormalButton"
                || button.name == "FastButton").ToArray();
            CollectionAssert.AreEquivalent(
                new[] { "PauseButton", "NormalButton", "FastButton" },
                timeButtons.Select(button => button.name));
            Assert.That(panel.GetComponentsInChildren<Text>(true), Is.Empty);
            var timeLabels = timeButtons.Select(button =>
                button.GetComponentInChildren<TMP_Text>(true)).ToArray();
            Assert.That(timeLabels, Has.Length.EqualTo(3));
            Assert.That(theme, Is.Not.Null);
            Assert.That(timeLabels.All(label =>
                label.font == theme.Typography.Label.FontAsset), Is.True);
            Assert.That(timeButtons.All(button =>
                button.GetComponent<AnimalCafeButtonView>() != null), Is.True,
                "MainCafe time controls must use the reusable Phase 5 Button presentation.");
            Assert.That(timeButtons.All(button => button.GetComponent<Shadow>() != null), Is.True,
                "MainCafe time controls must keep the Phase 5 elevation cue.");
            Assert.That(EditorUtility.IsDirty(callerMaterial), Is.True, "Target configuration must not save unrelated assets.");
            Assert.That(callerMaterial.color, Is.EqualTo(Color.magenta));
            Assert.That(System.IO.File.ReadAllBytes(assetPath), Is.EqualTo(callerBytes));
            }
            finally { AssetDatabase.DeleteAsset(assetPath); }
        }

        [Test]
        public void ConfigurePhase0Target_Twice_KeepsOneUiRootAndOneInfrastructureInstance()
        {
            var first = EditorSceneManager.OpenScene(MainCafePath, OpenSceneMode.Additive);
            Phase0SceneSetup.ConfigurePhase0Scene(first);
            var firstInventory = CaptureSingletonInventory(first);

            Phase0SceneSetup.ConfigurePhase0Scene(first);
            Assert.That(EditorSceneManager.SaveScene(first), Is.True,
                "This test explicitly persists its owned target before checking the reload.");
            EditorSceneManager.CloseScene(first, true);
            var second = EditorSceneManager.OpenScene(MainCafePath, OpenSceneMode.Additive);

            Assert.That(CaptureSingletonInventory(second), Is.EqualTo(firstInventory));
            Assert.That(FindAll(second, "UI Root"), Has.Length.EqualTo(1));
            Assert.That(FindAll<EventSystem>(second), Has.Length.EqualTo(1));
        }

        [Test]
        public void MainCafe_IsTheSoleEnabledProductionBuildSettingsScene()
        {
            Assert.That(EditorBuildSettings.scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path), Is.EquivalentTo(new[] { MainCafePath }));
        }

        private static string[] CaptureSingletonInventory(Scene scene) => new[]
        {
            "UI Root=" + FindAll(scene, "UI Root").Length,
            "EventSystem=" + FindAll<EventSystem>(scene).Length,
            "GameTimeService=" + FindAll<GameTimeService>(scene).Length,
            "MouseCameraInput=" + FindAll<MouseCameraInput>(scene).Length,
            "SceneInteractionController=" + FindAll<SceneInteractionController>(scene).Length,
            "TimeControlPanel=" + FindAll<TimeControlPanel>(scene).Length
        };

        private static GameObject[] FindAll(Scene scene, string name) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name == name)
                .Select(transform => transform.gameObject)
                .ToArray();

        private static T[] FindAll<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}

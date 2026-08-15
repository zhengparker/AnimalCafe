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

        [SetUp]
        public void SetUp() => Phase5UiAssetBuilder.BuildAll();

        [Test]
        public void ConfigurePhase0Scene_MigratesTimeControlsIntoOneThemedPhase5UiRoot()
        {
            Phase0SceneSetup.ConfigurePhase0Scene();
            var scene = EditorSceneManager.OpenScene(MainCafePath, OpenSceneMode.Single);
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
            CollectionAssert.AreEquivalent(
                new[] { "PauseButton", "NormalButton", "FastButton" },
                buttons.Select(button => button.name));
            Assert.That(panel.GetComponentsInChildren<Text>(true), Is.Empty);
            Assert.That(panel.GetComponentsInChildren<TMP_Text>(true), Has.Length.EqualTo(3));
            Assert.That(theme, Is.Not.Null);
            Assert.That(panel.GetComponentsInChildren<TMP_Text>(true)
                .All(label => label.font == theme.Typography.Label.FontAsset), Is.True);
            Assert.That(panel.GetComponentsInChildren<AnimalCafeButtonView>(true), Has.Length.EqualTo(3),
                "MainCafe time controls must use the reusable Phase 5 Button presentation.");
            Assert.That(buttons.All(button => button.GetComponent<Shadow>() != null), Is.True,
                "MainCafe time controls must keep the Phase 5 elevation cue.");
        }

        [Test]
        public void ConfigurePhase0Scene_Twice_KeepsOneUiRootAndOneInfrastructureInstance()
        {
            Phase0SceneSetup.ConfigurePhase0Scene();
            var first = EditorSceneManager.OpenScene(MainCafePath, OpenSceneMode.Single);
            var firstInventory = CaptureSingletonInventory(first);

            Phase0SceneSetup.ConfigurePhase0Scene();
            var second = EditorSceneManager.OpenScene(MainCafePath, OpenSceneMode.Single);

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

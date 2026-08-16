using System;
using System.IO;
using System.Linq;
using AnimalCafe.Diagnostics;
using AnimalCafe.EditorTools.Phase5;
using AnimalCafe.Interaction;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Feedback;
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
    public sealed class Phase5UiFoundationSceneSetupTests
    {
        [SetUp]
        public void SetUp() => Phase5UiAssetBuilder.BuildAll();

        [Test]
        public void BuildScene_CreatesApprovedValidationInventoryAndCleanValidatorReport()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = EditorSceneManager.OpenScene(
                Phase5UiFoundationSceneSetup.ScenePath,
                OpenSceneMode.Single);

            Assert.That(scene.GetRootGameObjects().Count(root => root.name == "Phase5UiFoundationRoot"),
                Is.EqualTo(1));
            Assert.That(FindAll<Canvas>(scene).Select(canvas => canvas.name), Is.EquivalentTo(new[]
            {
                "HUD Canvas", "Screen Canvas", "Toast Canvas"
            }));
            Assert.That(FindAll<EventSystem>(scene), Has.Length.EqualTo(1));
            Assert.That(Find(scene, "HUD Layer"), Is.Not.Null);
            Assert.That(Find(scene, "Panel Layer"), Is.Not.Null);
            Assert.That(Find(scene, "Modal Layer"), Is.Not.Null);
            Assert.That(Find(scene, "Toast Layer"), Is.Not.Null);

            Assert.That(Find(scene, "Component Gallery"), Is.Not.Null);
            Assert.That(Find(scene, "Selectable Coffee Machine").GetComponent<ColorSelectable>(), Is.Not.Null);
            Assert.That(Find(scene, "Selectable Coffee Machine").GetComponent<Collider>(), Is.Not.Null);
            Assert.That(Find(scene, "Scaled Time Mover").GetComponent<ManualReviewPingPongMover>(), Is.Not.Null);
            Assert.That(Find(scene, "Long Localized Label").GetComponent<TMP_Text>(), Is.Not.Null);
            Assert.That(Find(scene, "Safe Area").GetComponent<SafeAreaContainer>(), Is.Not.Null);
            Assert.That(Find(scene, "Toast Fixture").GetComponent<ToastView>(), Is.Not.Null);
            Assert.That(Find(scene, "Tooltip Fixture").GetComponent<TooltipView>(), Is.Not.Null);
            Assert.That(Find(scene, "Validation Message Fixture").GetComponent<ValidationMessageView>(), Is.Not.Null);

            var report = Phase5UiFoundationValidator.Validate(
                scene,
                AssetDatabase.LoadAssetAtPath<AnimalCafe.UI.Foundation.AnimalCafeUiTheme>(
                    Phase5UiAssetPaths.ThemePath));
            Assert.That(report.Issues, Is.Empty);
        }

        [Test]
        public void BuildScene_TwiceIsIdempotentAndKeepsOneOfEveryFoundationObject()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var firstScene = EditorSceneManager.OpenScene(
                Phase5UiFoundationSceneSetup.ScenePath,
                OpenSceneMode.Single);
            var firstInventory = CaptureInventory(firstScene);
            var firstGuid = AssetDatabase.AssetPathToGUID(Phase5UiFoundationSceneSetup.ScenePath);

            Phase5UiFoundationSceneSetup.BuildScene();
            var secondScene = EditorSceneManager.OpenScene(
                Phase5UiFoundationSceneSetup.ScenePath,
                OpenSceneMode.Single);

            Assert.That(AssetDatabase.AssetPathToGUID(Phase5UiFoundationSceneSetup.ScenePath), Is.EqualTo(firstGuid));
            Assert.That(CaptureInventory(secondScene), Is.EqualTo(firstInventory));
            Assert.That(FindAll<Canvas>(secondScene), Has.Length.EqualTo(3));
            Assert.That(FindAll<EventSystem>(secondScene), Has.Length.EqualTo(1));
            Assert.That(FindAll<SafeAreaContainer>(secondScene), Has.Length.EqualTo(1));
        }

        [Test]
        public void ValidationScene_IsNotIncludedInProductionBuildSettings()
        {
            Phase5UiFoundationSceneSetup.BuildScene();

            Assert.That(EditorBuildSettings.scenes.Any(scene =>
                scene.path == Phase5UiFoundationSceneSetup.ScenePath && scene.enabled), Is.False);
        }

        private static GameObject Find(Scene scene, string name) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .SingleOrDefault(transform => transform.name == name)?.gameObject;

        private static T[] FindAll<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static string[] CaptureInventory(Scene scene) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => HierarchyPath(transform) + ":" + string.Join(",",
                    transform.GetComponents<Component>()
                        .Where(component => component != null)
                        .Select(component => component.GetType().FullName)
                        .OrderBy(type => type, StringComparer.Ordinal)))
                .OrderBy(entry => entry, StringComparer.Ordinal)
                .ToArray();

        private static string HierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }
    }
}

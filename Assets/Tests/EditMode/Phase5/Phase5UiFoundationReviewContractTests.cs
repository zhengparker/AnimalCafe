using System.Linq;
using AnimalCafe.EditorTools.Phase5;
using AnimalCafe.Interaction;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.Phase5
{
    public sealed class Phase5UiFoundationReviewContractTests
    {
        [Test]
        public void Validator_NestedUiRootsAndNestedLayers_AreStrictlyValidatedWithStablePaths()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = EditorSceneManager.OpenScene(Phase5UiFoundationSceneSetup.ScenePath, OpenSceneMode.Single);
            var root = Find(scene, "UI Root");
            var duplicate = Object.Instantiate(root, root.transform.parent);
            duplicate.name = "UI Root";
            Object.DestroyImmediate(root.transform.Find("HUD Canvas/HUD Layer").gameObject);

            var report = Phase5UiFoundationValidator.Validate(
                scene,
                AssetDatabase.LoadAssetAtPath<AnimalCafe.UI.Foundation.AnimalCafeUiTheme>(
                    Phase5UiAssetPaths.ThemePath));

            Assert.That(report.Issues.Any(issue =>
                issue.Code == Phase5UiFoundationIssueCode.DuplicateUiRoot &&
                issue.ObjectPath.Contains("Phase5UiFoundationRoot/UI Root[1]")), Is.True);
            Assert.That(report.Issues.Any(issue =>
                issue.Code == Phase5UiFoundationIssueCode.MissingLogicalLayer &&
                issue.ObjectPath == "Phase5UiFoundationRoot/UI Root/HUD Canvas/HUD Layer"), Is.True);
        }

        [Test]
        public void CanonicalAssetContract_IncludesExactlyOneValidationSceneAtApprovedPath()
        {
            Assert.That(Phase5UiAssetPaths.RequiredAssetPaths,
                Does.Contain(Phase5UiFoundationSceneSetup.ScenePath));
            Assert.That(AssetDatabase.FindAssets("Phase5UiFoundation t:Scene")
                    .Select(AssetDatabase.GUIDToAssetPath),
                Is.EquivalentTo(new[] { Phase5UiFoundationSceneSetup.ScenePath }));
        }

        [Test]
        public void ValidationScene_ContainsRealSelectionInputChainAndCompleteGalleryFixtures()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = EditorSceneManager.OpenScene(Phase5UiFoundationSceneSetup.ScenePath, OpenSceneMode.Single);
            Assert.That(Find(scene, "Scene Interaction Controller").GetComponent<SceneInteractionController>(), Is.Not.Null);
            Assert.That(Find(scene, "UI Root").GetComponentsInChildren<EventSystem>(true), Has.Length.EqualTo(1));
            foreach (var required in new[]
            {
                "Solid Panel Fixture", "Light Frost Panel Fixture", "Strong Frost Panel Fixture",
                "Modal Fixture", "Pause Game Button", "Continue Game Button", "Reduced Motion Toggle",
                "Open Second Strong Frost Button", "Safe Area Confirm Button", "Validation Repair Button"
            }) Assert.That(Find(scene, required), Is.Not.Null, required);
        }

        [Test]
        public void ValidationScene_IsAbsentFromBuildSettingsEvenWhenDisabledEntryIsInjected()
        {
            var original = EditorBuildSettings.scenes;
            try
            {
                EditorBuildSettings.scenes = original.Concat(new[]
                {
                    new EditorBuildSettingsScene(Phase5UiFoundationSceneSetup.ScenePath, false)
                }).ToArray();
                Phase5UiFoundationSceneSetup.BuildScene();
                Assert.That(EditorBuildSettings.scenes.Any(entry =>
                    entry.path == Phase5UiFoundationSceneSetup.ScenePath), Is.False);
            }
            finally { EditorBuildSettings.scenes = original; }
        }

        private static GameObject Find(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Single(transform => transform.name == name).gameObject;
    }
}

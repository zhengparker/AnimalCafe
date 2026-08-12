using System.Collections.Generic;
using System.Linq;
using AnimalCafe.EditorTools.Phase5;
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
    public sealed class Phase5UiFoundationValidatorTests
    {
        private const string FixtureFolder = "Assets/Tests/Phase5UiFoundationValidatorFixture";
        private const string FixtureScenePath = FixtureFolder + "/Phase5UiFoundationValidatorFixture.unity";
        private readonly List<GameObject> createdObjects = new();
        private Scene scene;
        private AnimalCafeUiTheme theme;

        [SetUp]
        public void SetUp()
        {
            Phase5UiAssetBuilder.BuildAll();
            EnsureFolder(FixtureFolder);
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(EditorSceneManager.SaveScene(scene, FixtureScenePath, false), Is.True);
            scene = SceneManager.GetActiveScene();
            Assert.That(scene.path, Is.EqualTo(FixtureScenePath));
            theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(Phase5UiAssetPaths.ThemePath);
            Assert.That(theme, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in createdObjects.Where(created => created != null))
                Object.DestroyImmediate(created);
            AssetDatabase.DeleteAsset(FixtureFolder);
            AssetDatabase.Refresh();
        }

        [Test]
        public void Validate_DuplicateRootCanvasAndEventSystem_ReportsStableCodesAndObjectPaths()
        {
            CreateCanonicalHierarchy();
            Create("UI Root");
            CreateCanvas("HUD Canvas", "HUD Layer");
            Create("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            SaveFixtureScene();

            var report = Phase5UiFoundationValidator.Validate(scene, theme);

            AssertIssue(report, Phase5UiFoundationIssueCode.DuplicateUiRoot, "UI Root[1]");
            AssertIssue(report, Phase5UiFoundationIssueCode.DuplicateCanvas, "HUD Canvas[1]");
            AssertIssue(report, Phase5UiFoundationIssueCode.DuplicateEventSystem, "EventSystem[1]");
        }

        [Test]
        public void Validate_MissingLogicalLayerThemeTokenAndFont_ReportsStableCodesAndAssetPaths()
        {
            CreateCanonicalHierarchy(includeToastLayer: false);
            theme.Typography = new UiTypographyTokens
            {
                Heading = new UiTextStyleToken(null, 28f, FontStyles.Bold, 0f),
                Body = theme.Typography.Body,
                Label = theme.Typography.Label
            };
            SaveFixtureScene();

            var report = Phase5UiFoundationValidator.Validate(scene, theme);

            AssertIssue(report, Phase5UiFoundationIssueCode.MissingLogicalLayer, "UI Root/Toast Canvas/Toast Layer");
            AssertIssue(
                report,
                Phase5UiFoundationIssueCode.MissingThemeToken,
                "Typography/Heading",
                Phase5UiAssetPaths.ThemePath);
            AssertIssue(
                report,
                Phase5UiFoundationIssueCode.MissingFont,
                "Typography/Heading",
                Phase5UiAssetPaths.ThemePath);
        }

        [Test]
        public void Validate_ColorAndMaterialThemeFailures_PreserveFullStableTokenPaths()
        {
            CreateCanonicalHierarchy();
            var colors = theme.Colors;
            colors.Text = Color.clear;
            theme.Colors = colors;
            var materials = theme.Materials;
            materials.StrongFrost = null;
            theme.Materials = materials;
            SaveFixtureScene();

            var report = Phase5UiFoundationValidator.Validate(scene, theme);

            AssertIssue(
                report,
                Phase5UiFoundationIssueCode.MissingThemeToken,
                "Colors/TEXT",
                Phase5UiAssetPaths.ThemePath);
            AssertIssue(
                report,
                Phase5UiFoundationIssueCode.MissingThemeToken,
                "Materials/STRONG_FROST",
                Phase5UiAssetPaths.ThemePath);
        }

        [Test]
        public void Validate_SmallTouchTargetAndIncorrectRaycastPolicies_ReportStableCodes()
        {
            CreateCanonicalHierarchy();
            var button = Create("ConfirmButton", typeof(Image), typeof(Button));
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(47f, 48f);
            var toastCanvas = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                .Single(canvas => canvas.name == "Toast Canvas");
            toastCanvas.GetComponent<GraphicRaycaster>().enabled = true;
            SaveFixtureScene();

            var report = Phase5UiFoundationValidator.Validate(scene, theme);

            AssertIssue(report, Phase5UiFoundationIssueCode.TouchTargetBelowMinimum, "ConfirmButton");
            AssertIssue(report, Phase5UiFoundationIssueCode.InvalidRaycastPolicy, "UI Root/Toast Canvas");
        }

        [Test]
        public void Validate_MultipleResolvedStrongFrostOwners_ReportsEveryOwnerWithStablePaths()
        {
            CreateCanonicalHierarchy();
            CreateStrongFrostPanel("Strong A", new StrongFrostLease(true));
            CreateStrongFrostPanel("Strong B", new StrongFrostLease(true));
            SaveFixtureScene();

            var report = Phase5UiFoundationValidator.Validate(scene, theme);

            var issues = report.Issues.Where(issue =>
                issue.Code == Phase5UiFoundationIssueCode.MultipleStrongFrostOwners).ToArray();
            Assert.That(issues.Select(issue => issue.ObjectPath), Is.EquivalentTo(new[]
            {
                "Strong A", "Strong B"
            }));
            Assert.That(issues.All(issue => issue.AssetPath == FixtureScenePath), Is.True);
        }

        private void CreateCanonicalHierarchy(bool includeToastLayer = true)
        {
            var root = Create("UI Root");
            CreateCanvas("HUD Canvas", "HUD Layer", root.transform);
            var screen = CreateCanvas("Screen Canvas", "Panel Layer", root.transform);
            Create("Modal Layer", parent: screen.transform);
            var toast = CreateCanvas("Toast Canvas", includeToastLayer ? "Toast Layer" : null, root.transform);
            toast.GetComponent<GraphicRaycaster>().enabled = false;
            Create("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private GameObject CreateCanvas(string name, string layerName, Transform parent = null)
        {
            var canvas = Create(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.transform.SetParent(parent, false);
            if (!string.IsNullOrEmpty(layerName)) Create(layerName, parent: canvas.transform);
            return canvas;
        }

        private void CreateStrongFrostPanel(string name, StrongFrostLease lease)
        {
            var panel = Create(name, typeof(Image), typeof(AnimalCafePanelView));
            panel.GetComponent<AnimalCafePanelView>().Configure(theme, UiPanelStyle.StrongFrost, lease);
            Assert.That(panel.GetComponent<AnimalCafePanelView>().ResolvedStyle, Is.EqualTo(UiPanelStyle.StrongFrost));
        }

        private GameObject Create(string name, params System.Type[] components) => Create(name, null, components);

        private GameObject Create(string name, Transform parent = null, params System.Type[] components)
        {
            var types = new List<System.Type> { typeof(RectTransform) };
            types.AddRange(components);
            var instance = new GameObject(name, types.ToArray());
            SceneManager.MoveGameObjectToScene(instance, scene);
            instance.transform.SetParent(parent, false);
            createdObjects.Add(instance);
            return instance;
        }

        private void SaveFixtureScene()
        {
            EditorSceneManager.SaveScene(scene, FixtureScenePath, true);
        }

        private static void AssertIssue(
            Phase5UiFoundationValidationReport report,
            Phase5UiFoundationIssueCode code,
            string expectedPath,
            string expectedAssetPath = FixtureScenePath)
        {
            var issue = report.Issues.Single(candidate =>
                candidate.Code == code && candidate.ObjectPath == expectedPath);
            Assert.That(issue.AssetPath, Is.EqualTo(expectedAssetPath));
        }

        private static void EnsureFolder(string path)
        {
            var current = "Assets";
            foreach (var segment in path.Substring("Assets/".Length).Split('/'))
            {
                var next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment);
                current = next;
            }
        }
    }
}

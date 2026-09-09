using System.Linq;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.Phase8;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class Phase8ValidatorTests
    {
        [TestCase(Phase8AssetPaths.MainCafeScenePath)]
        [TestCase(Phase8AssetPaths.ValidationScenePath)]
        public void ValidateOpenScene_ConfiguredPersistentScene_HasNoIssues(string scenePath)
        {
            Phase8AssetBuilder.BuildAssets();
            if (scenePath == Phase8AssetPaths.MainCafeScenePath)
                Phase8SceneSetup.ConfigureMainCafe();
            else
                Phase8SceneSetup.ConfigureValidationScene();

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var report = Phase8Validator.ValidateOpenScene();
                Assert.That(report.Issues, Is.Empty,
                    string.Join("\n", report.Issues.Select(issue =>
                        issue.Code + ": " + issue.Message)));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void ValidateOpenScene_CorruptFixture_ReportsStableIssuesWithoutThrowing()
        {
            Phase8AssetBuilder.BuildAssets();
            Phase8SceneSetup.ConfigureValidationScene();
            var scene = EditorSceneManager.OpenScene(
                Phase8AssetPaths.ValidationScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var source = scene.GetRootGameObjects().Single(root =>
                root.name == Phase8AssetPaths.RuntimeRootName);
            var duplicate = Object.Instantiate(source);
            duplicate.name = source.name;
            SceneManager.MoveGameObjectToScene(duplicate, scene);
            var controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true))
                .First();
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("phase8FurnitureCatalogueAsset").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                Phase8ValidationReport first = null;
                Assert.DoesNotThrow(() => first = Phase8Validator.ValidateOpenScene());
                var second = Phase8Validator.ValidateOpenScene();
                Assert.That(first.Issues.Select(issue => issue.Code + "|" + issue.Message),
                    Is.EqualTo(second.Issues.Select(issue => issue.Code + "|" + issue.Message)));
                Assert.That(first.Issues, Has.Some.Matches<Phase8ValidationIssue>(issue =>
                    issue.Code == "P8-SCENE-ROOTS"));
                Assert.That(first.Issues, Has.Some.Matches<Phase8ValidationIssue>(issue =>
                    issue.Code == "P8-SCENE-REFERENCES"));
            }
            finally
            {
                Object.DestroyImmediate(duplicate);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase("missing")]
        [TestCase("duplicate")]
        [TestCase("unbound")]
        [TestCase("renamed")]
        [TestCase("button-unbound")]
        [TestCase("wrong-source")]
        public void ValidateOpenScene_Phase7UiCompatibilityDrift_ReportsStableIssue(
            string corruption)
        {
            Phase8AssetBuilder.BuildAssets();
            Phase8SceneSetup.ConfigureValidationScene();
            var scene = EditorSceneManager.OpenScene(
                Phase8AssetPaths.ValidationScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var controller = FindAll<DecorationModeController>(scene).Single();
            var range = FindAll<DecorationFloorRangeView>(scene).SingleOrDefault();
            if (corruption == "missing" && range != null)
            {
                Object.DestroyImmediate(range.gameObject);
            }
            else if (corruption == "duplicate")
            {
                var duplicate = new GameObject(
                    "DuplicateFloorRange", typeof(RectTransform),
                    typeof(DecorationFloorRangeView));
                SceneManager.MoveGameObjectToScene(duplicate, scene);
            }
            else if (corruption == "unbound")
            {
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("floorRangeView").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else if (corruption == "renamed")
            {
                range.name = "FloorRange_Renamed";
            }
            else if (corruption == "button-unbound")
            {
                var serialized = new SerializedObject(range);
                serialized.FindProperty("singleGridButton").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else if (corruption == "wrong-source")
            {
                var replacement = CreateSceneOwnedFloorRange(
                    range.transform.parent,
                    scene);
                Object.DestroyImmediate(range.gameObject);
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("floorRangeView").objectReferenceValue = replacement;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            try
            {
                var first = Phase8Validator.ValidateOpenScene();
                var second = Phase8Validator.ValidateOpenScene();
                Assert.That(first.Issues.Select(issue => issue.Code + "|" + issue.Message),
                    Is.EqualTo(second.Issues.Select(issue => issue.Code + "|" + issue.Message)));
                Assert.That(first.Issues, Has.Some.Matches<Phase8ValidationIssue>(issue =>
                    issue.Code == "P8-SCENE-PHASE7-UI"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase("renamed")]
        [TestCase("swapped-button-references")]
        public void ValidateOpenScene_CataloguePrefabFloorRangeDrift_ReportsAssetIssue(
            string corruption)
        {
            Phase8AssetBuilder.BuildAssets();
            Phase8SceneSetup.ConfigureValidationScene();
            MutateCatalogueRangePrefab(corruption);
            var scene = EditorSceneManager.OpenScene(
                Phase8AssetPaths.ValidationScenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var first = Phase8Validator.ValidateOpenScene();
                var second = Phase8Validator.ValidateOpenScene();
                Assert.That(first.Issues.Select(issue => issue.Code + "|" + issue.Message),
                    Is.EqualTo(second.Issues.Select(issue => issue.Code + "|" + issue.Message)));
                Assert.That(first.Issues, Has.Some.Matches<Phase8ValidationIssue>(issue =>
                    issue.Code == "P8-ASSET-PHASE7-UI"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                MutateCatalogueRangePrefab("restore");
            }
        }

        [TestCase(Phase8AssetPaths.MainCafeScenePath, "wrong-singleton",
            "P8-SCENE-REFERENCES")]
        [TestCase(Phase8AssetPaths.MainCafeScenePath, "wrong-root",
            "P8-SCENE-REFERENCES")]
        [TestCase(Phase8AssetPaths.MainCafeScenePath, "wrong-prefab-asset",
            "P8-SCENE-REFERENCES")]
        [TestCase(Phase8AssetPaths.ValidationScenePath, "wrong-debug-root",
            "P8-SCENE-DEBUG-REFERENCES")]
        [TestCase(Phase8AssetPaths.ValidationScenePath, "wrong-debug-material",
            "P8-SCENE-DEBUG-REFERENCES")]
        public void ValidateOpenScene_WrongNonNullControllerReference_ReportsStableIssue(
            string scenePath,
            string corruption,
            string expectedCode)
        {
            Phase8AssetBuilder.BuildAssets();
            if (scenePath == Phase8AssetPaths.MainCafeScenePath)
                Phase8SceneSetup.ConfigureMainCafe();
            else
                Phase8SceneSetup.ConfigureValidationScene();

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var controller = FindAll<DecorationModeController>(scene).Single();
            var serialized = new SerializedObject(controller);
            if (corruption == "wrong-singleton")
            {
                var wrongRoot = new GameObject(
                    "WrongSurfaceMountedRegistry", typeof(SurfaceMountedSceneRegistry));
                SceneManager.MoveGameObjectToScene(wrongRoot, scene);
                serialized.FindProperty("surfaceMountedSceneRegistry").objectReferenceValue =
                    wrongRoot.GetComponent<SurfaceMountedSceneRegistry>();
            }
            else if (corruption == "wrong-root")
            {
                serialized.FindProperty("functionalSurfacePreviewRoot")
                    .objectReferenceValue = FindAll<Transform>(scene).Single(item =>
                        item.name == Phase8AssetPaths.PickUpIndicatorRootName);
            }
            else if (corruption == "wrong-prefab-asset")
            {
                serialized.FindProperty("functionalSurfacePreviewPrefab")
                    .objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
                        Phase8AssetPaths.PickUpPointIndicatorPrefabPath);
            }
            else if (corruption == "wrong-debug-root")
            {
                serialized.FindProperty("interactionAnchorDebugRoot")
                    .objectReferenceValue = FindAll<Transform>(scene).Single(item =>
                        item.name == Phase8AssetPaths.PreviewRootName);
            }
            else if (corruption == "wrong-debug-material")
            {
                serialized.FindProperty("employeeAnchorDebugMaterial")
                    .objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(
                        Phase8AssetPaths.CustomerAnchorDebugMaterialPath);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                var first = Phase8Validator.ValidateOpenScene();
                var second = Phase8Validator.ValidateOpenScene();
                Assert.That(first.Issues.Select(issue => issue.Code + "|" + issue.Message),
                    Is.EqualTo(second.Issues.Select(issue => issue.Code + "|" + issue.Message)));
                Assert.That(first.Issues, Has.Some.Matches<Phase8ValidationIssue>(issue =>
                    issue.Code == expectedCode));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static DecorationFloorRangeView CreateSceneOwnedFloorRange(
            Transform parent,
            Scene scene)
        {
            var root = new GameObject(
                "FloorRange", typeof(RectTransform), typeof(DecorationFloorRangeView));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(parent, false);
            var range = root.GetComponent<DecorationFloorRangeView>();
            var whole = CreateRangeButton(root.transform, "WholeRoomButton");
            var single = CreateRangeButton(root.transform, "SingleGridButton");
            var serialized = new SerializedObject(range);
            serialized.FindProperty("wholeRoomButton").objectReferenceValue = whole;
            serialized.FindProperty("singleGridButton").objectReferenceValue = single;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(range), Is.Null);
            return range;
        }

        private static Button CreateRangeButton(Transform parent, string name)
        {
            var root = new GameObject(
                name, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            return root.GetComponent<Button>();
        }

        private static void MutateCatalogueRangePrefab(string corruption)
        {
            var root = PrefabUtility.LoadPrefabContents(
                Phase8AssetPaths.CataloguePrefabPath);
            try
            {
                var range = root.GetComponentsInChildren<DecorationFloorRangeView>(true)
                    .Single();
                var whole = range.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "WholeRoomButton");
                var single = range.GetComponentsInChildren<Button>(true)
                    .Single(button => button.name == "SingleGridButton");
                range.name = corruption == "renamed"
                    ? "FloorRange_Renamed"
                    : "FloorRange";
                var serialized = new SerializedObject(range);
                serialized.FindProperty("wholeRoomButton").objectReferenceValue =
                    corruption == "swapped-button-references" ? single : whole;
                serialized.FindProperty("singleGridButton").objectReferenceValue =
                    corruption == "swapped-button-references" ? whole : single;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    Phase8AssetPaths.CataloguePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static T[] FindAll<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
    }
}

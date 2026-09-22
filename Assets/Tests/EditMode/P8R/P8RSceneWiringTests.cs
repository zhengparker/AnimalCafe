using System.Linq;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.P8R;
using AnimalCafe.EditorTools.Phase8;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RSceneWiringTests
    {
        [TestCase("p8rAppearance")]
        [TestCase("modeTabsView")]
        [TestCase("floorRangeView")]
        public void IncompletePresentation_IsNotAcceptedAsCompleteP8R(string property)
        {
            var scene = EditorSceneManager.OpenScene(Phase8AssetPaths.MainCafeScenePath, OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true)).Single();
                var so = new SerializedObject(controller);
                so.FindProperty(property).objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(P8RFurnitureUiBuilder.HasP8RWiring(controller), Is.False,
                    "Missing presentation references must not be accepted as safe no-op wiring.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void PartialP8R_FailsBeforeLegacySceneGraphMutatesAnything()
        {
            var scene = EditorSceneManager.OpenScene(Phase8AssetPaths.MainCafeScenePath, OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true)).Single();
                var so = new SerializedObject(controller);
                so.FindProperty("actionBarView").objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                var catalogue = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DecorationCatalogueView>(true)).Single();
                var graph = typeof(Phase8SceneSetup).GetMethod("ConfigureSceneGraph",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.That(() => graph.Invoke(null, new object[] { scene, false }), Throws.Exception,
                    "A partial P8R graph must fail closed, before old authoring replaces UI.");
                Assert.That(catalogue == null, Is.False, "Existing P8R Catalogue instance must survive rejected legacy authoring.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void CompleteP8R_ValidatesAndBothAuthoringCommandsAreByteStable()
        {
            var path = Phase8AssetPaths.MainCafeScenePath;
            var before = System.IO.File.ReadAllBytes(path);
            P8RFurnitureUiBuilder.BuildApprovedFurnitureUi();
            Phase8SceneSetup.ConfigureMainCafe();
            Assert.That(System.IO.File.ReadAllBytes(path), Is.EqualTo(before));
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                Assert.That(Phase8Validator.ValidateOpenScene().Issues, Is.Empty);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [TestCase("actionBarView")]
        [TestCase("storeModalView")]
        [TestCase("functionalSurfacePreviewPrefab")]
        public void RejectedMigration_RetainsEveryOriginalUiReferenceAndSceneFile(string missingReference)
        {
            // The existing validation scene is a real legacy UI graph. Changes stay in memory.
            var path = Phase8AssetPaths.ValidationScenePath;
            var disk = System.IO.File.ReadAllBytes(path);
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true)).Single();
                var so = new SerializedObject(controller);
                so.FindProperty(missingReference).objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                var before = UiSnapshot(scene, controller);
                var method = typeof(P8RFurnitureUiBuilder).GetMethod("WireScene",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                Assert.That(() => method.Invoke(null, new object[] { scene }), Throws.Exception);
                Assert.That(UiSnapshot(scene, controller), Is.EqualTo(before),
                    "Missing later UI references fail before replacement; rejected candidate validation rolls back all replacements.");
                Assert.That(System.IO.File.ReadAllBytes(path), Is.EqualTo(disk));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        [Test]
        public void ExistingUnloadedMainCafe_RemainsInTheSameSceneListAfterNoOpAuthoring()
        {
            var path = Phase8AssetPaths.MainCafeScenePath;
            var active = SceneManager.GetActiveScene();
            Assert.That(SceneManager.GetSceneByPath(path).IsValid(), Is.False, "Fixture must own the added MainCafe entry.");
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            EditorSceneManager.CloseScene(scene, false);
            var before = EditorSceneManager.GetSceneManagerSetup().Select(item => item.path + "|" + item.isLoaded + "|" + item.isActive).ToArray();
            var disk = System.IO.File.ReadAllBytes(path);
            try
            {
                P8RFurnitureUiBuilder.BuildApprovedFurnitureUi();
                Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(item => item.path + "|" + item.isLoaded + "|" + item.isActive), Is.EqualTo(before));
                Assert.That(System.IO.File.ReadAllBytes(path), Is.EqualTo(disk));
            }
            finally
            {
                var current = SceneManager.GetSceneByPath(path);
                if (current.IsValid()) EditorSceneManager.CloseScene(current, true);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }

        private static string UiSnapshot(Scene scene, DecorationModeController controller)
        {
            var so = new SerializedObject(controller);
            var refs = new[] { "catalogueView", "actionBarView", "storeModalView", "modeTabsView", "floorRangeView" }
                .Select(name =>
                {
                    var item = so.FindProperty(name).objectReferenceValue;
                    return name + "=" + (item == null ? "null" : AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(item)));
                });
            var hierarchy = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                // Undo may regenerate TMP's derived fallback material mesh; it is not an authored scene node.
                .Where(item => item.GetComponent<TMPro.TMP_SubMeshUI>() == null)
                .Select(item => item.name + ":" + item.gameObject.activeSelf).OrderBy(item => item);
            return string.Join("|", refs) + string.Join("|", hierarchy);
        }
    }
}

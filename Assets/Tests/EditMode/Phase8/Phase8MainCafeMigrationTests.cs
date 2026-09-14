using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.Phase7;
using AnimalCafe.EditorTools.Phase8;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class Phase8SceneSetupSafetyTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ConfigureLoadedScene_PreservesOpenOrderAndOriginalActiveScene(bool targetIsActive)
        {
            using var fixture = new TemporaryScene();
            var target = fixture.OpenTarget();
            fixture.RequireRepair(target);
            SceneManager.SetActiveScene(targetIsActive ? target : fixture.CallerScene);
            var setup = CaptureSetup();

            Configure(fixture.ScenePath);

            Assert.That(CaptureSetup(), Is.EqualTo(setup));
            Assert.That(SceneManager.GetSceneByPath(fixture.ScenePath).isLoaded, Is.True);
            Assert.That(fixture.RuntimeRoot(SceneManager.GetSceneByPath(fixture.ScenePath))
                .transform.position, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ConfigureScene_NewRuntimeObjectsDoNotDirtyOtherScenes()
        {
            using var fixture = new TemporaryScene();
            var target = fixture.OpenTarget();
            UnityEngine.Object.DestroyImmediate(fixture.RuntimeRoot(target));
            EditorSceneManager.MarkSceneDirty(target);
            Assert.That(EditorSceneManager.SaveScene(target, fixture.ScenePath), Is.True);
            SceneManager.SetActiveScene(fixture.CallerScene);
            Assert.That(fixture.CallerScene.isDirty, Is.False, "Caller fixture must begin clean.");

            Configure(fixture.ScenePath);

            Assert.That(fixture.CallerScene.isDirty, Is.False,
                "Migration must create its temporary GameObjects in the target Scene.");
        }

        [Test]
        public void ConfigureUnloadedScene_PreservesUnrelatedDirtySceneAndActiveScene()
        {
            using var fixture = new TemporaryScene();
            var target = fixture.OpenTarget();
            fixture.RequireRepair(target);
            EditorSceneManager.CloseScene(target, true);
            var marker = new GameObject("Unsaved caller work");
            SceneManager.MoveGameObjectToScene(marker, fixture.CallerScene);
            EditorSceneManager.MarkSceneDirty(fixture.CallerScene);
            SceneManager.SetActiveScene(fixture.CallerScene);
            var setup = CaptureSetup();

            Configure(fixture.ScenePath);

            Assert.That(CaptureSetup(), Is.EqualTo(setup));
            Assert.That(marker != null, Is.True);
            Assert.That(fixture.CallerScene.isDirty, Is.True);
            Assert.That(SceneManager.GetSceneByPath(fixture.ScenePath).isLoaded, Is.False);
        }

        [Test]
        public void ConfigureUnloadedSceneEntry_PreservesItsPlaceInSceneSetup()
        {
            using var fixture = new TemporaryScene();
            var target = fixture.OpenTarget();
            fixture.RequireRepair(target);
            EditorSceneManager.MoveSceneBefore(target, fixture.CallerScene);
            EditorSceneManager.CloseScene(target, false);
            SceneManager.SetActiveScene(fixture.CallerScene);
            var setup = CaptureSetup();

            Configure(fixture.ScenePath);

            Assert.That(CaptureSetup(), Is.EqualTo(setup));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConfigureScene_DoesNotSaveUnrelatedDirtyAssets(bool targetIsLoaded)
        {
            using var fixture = new TemporaryScene();
            if (targetIsLoaded) fixture.OpenTarget();
            var assetPath = Path.GetDirectoryName(fixture.ScenePath).Replace('\\', '/') + "/CallerMaterial.mat";
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, assetPath);
            AssetDatabase.SaveAssetIfDirty(material);
            var bytes = File.ReadAllBytes(assetPath);
            material.color = Color.magenta;
            EditorUtility.SetDirty(material);
            Assert.That(EditorUtility.IsDirty(material), Is.True, "Fixture must start dirty.");

            var failure = Assert.Throws<InvalidOperationException>(() => Configure(fixture.ScenePath));
            Assert.That(failure.Message, Does.Contain("dirty").IgnoreCase);
            Assert.That(failure.Message, Does.Contain(assetPath));
            var actual = new[]
            {
                material != null,
                EditorUtility.IsDirty(material),
                material != null && material.color == Color.magenta,
                File.ReadAllBytes(assetPath).SequenceEqual(bytes)
            };
            Debug.Log($"Dirty asset after Scene setup: targetLoaded={targetIsLoaded}; alive,dirty,color,disk={string.Join(",", actual)}");
            Assert.That(actual, Is.EqualTo(new[] { true, true, true, true }),
                "Caller asset must retain its object, dirty flag, in-memory color and original file.");
        }

        [Test]
        public void ConfigureFirstValidationScene_WithDirtyAsset_DoesNotCreateTarget()
        {
            using var fixture = new TemporaryScene();
            var folder = Path.GetDirectoryName(fixture.ScenePath).Replace('\\', '/');
            var targetPath = folder + "/NewValidation.unity";
            var materialPath = folder + "/CallerMaterial.mat";
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssetIfDirty(material);
            var bytes = File.ReadAllBytes(materialPath);
            material.color = Color.magenta;
            EditorUtility.SetDirty(material);
            var method = typeof(Phase8SceneSetup).GetMethod("ConfigureValidationSceneCore",
                BindingFlags.Static | BindingFlags.NonPublic);

            var failure = Assert.Throws<TargetInvocationException>(() =>
                method.Invoke(null, new object[] { fixture.ScenePath, targetPath }));

            Assert.That(failure.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(failure.InnerException.Message, Does.Contain(materialPath));
            Assert.That(File.Exists(targetPath), Is.False,
                "Validation preflight must run before publishing the first Scene copy.");
            Assert.That(File.Exists(targetPath + ".meta"), Is.False);
            Assert.That(EditorUtility.IsDirty(material), Is.True);
            Assert.That(material.color, Is.EqualTo(Color.magenta));
            Assert.That(File.ReadAllBytes(materialPath), Is.EqualTo(bytes));
        }

        [Test]
        public void ConfigureDirtyLoadedTarget_RefusesWithoutSavingOrChangingCallerWork()
        {
            using var fixture = new TemporaryScene();
            var target = fixture.OpenTarget();
            var marker = new GameObject("Unsaved target work");
            SceneManager.MoveGameObjectToScene(marker, target);
            EditorSceneManager.MarkSceneDirty(target);
            SceneManager.SetActiveScene(target);
            var bytes = File.ReadAllBytes(fixture.ScenePath);
            var setup = CaptureSetup();

            var failure = Assert.Throws<InvalidOperationException>(() => Configure(fixture.ScenePath));

            Assert.That(failure.Message, Does.Contain("dirty").IgnoreCase);
            Assert.That(File.ReadAllBytes(fixture.ScenePath), Is.EqualTo(bytes));
            Assert.That(CaptureSetup(), Is.EqualTo(setup));
            Assert.That(marker != null, Is.True);
            Assert.That(target.isDirty, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConfigureInvalidCandidate_RollsBackDiskAndLoadedScene(bool keepTargetLoaded)
        {
            using var fixture = new TemporaryScene();
            var target = fixture.OpenTarget();
            var extraCanvas = new GameObject("Invalid extra Canvas", typeof(Canvas));
            SceneManager.MoveGameObjectToScene(extraCanvas, target);
            fixture.RequireRepair(target);
            if (!keepTargetLoaded)
                EditorSceneManager.CloseScene(target, true);
            SceneManager.SetActiveScene(keepTargetLoaded ? target : fixture.CallerScene);
            var bytes = File.ReadAllBytes(fixture.ScenePath);
            var metadata = File.ReadAllBytes(fixture.ScenePath + ".meta");
            var setup = CaptureSetup();

            var failure = Assert.Throws<InvalidOperationException>(() => Configure(fixture.ScenePath));

            Assert.That(failure.Message, Does.Contain("P8-SCENE-CANVAS"));
            Assert.That(File.ReadAllBytes(fixture.ScenePath), Is.EqualTo(bytes),
                "Rejected migration must not publish any Scene changes.");
            Assert.That(File.ReadAllBytes(fixture.ScenePath + ".meta"), Is.EqualTo(metadata));
            Assert.That(CaptureSetup(), Is.EqualTo(setup));
            if (keepTargetLoaded)
            {
                var restored = SceneManager.GetSceneByPath(fixture.ScenePath);
                Assert.That(restored.isLoaded, Is.True);
                Assert.That(restored.isDirty, Is.False);
                Assert.That(fixture.RuntimeRoot(restored).transform.position,
                    Is.EqualTo(new Vector3(3f, 4f, 5f)));
            }
        }

        [Test]
        public void ConfigureInvalidOnlyLoadedScene_RestoresItsOriginalMemoryAndFile()
        {
            var originalScenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt).ToArray();
            var originalDefaultBatchScene = Application.isBatchMode && originalScenes.Length == 1
                && !originalScenes[0].isDirty && string.IsNullOrEmpty(originalScenes[0].path)
                && originalScenes[0].GetRootGameObjects().Select(root => root.name)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .SequenceEqual(new[] { "Directional Light", "Main Camera" });
            if (originalScenes.Any(scene => scene.isDirty
                    || (string.IsNullOrEmpty(scene.path) && scene.rootCount != 0
                        && !originalDefaultBatchScene))
                || (originalScenes.Length > 1
                    && originalScenes.Any(scene => string.IsNullOrEmpty(scene.path))))
                Assert.Ignore("The single-Scene test preserves caller work; it requires clean saved Scenes or one empty Untitled Scene. Current: "
                    + string.Join("; ", originalScenes.Select(scene =>
                        $"path='{scene.path}',dirty={scene.isDirty},roots={string.Join(",", scene.GetRootGameObjects().Select(root => root.name))}")));

            var originalSetup = EditorSceneManager.GetSceneManagerSetup();
            var originalWasEmpty = originalScenes.All(scene => string.IsNullOrEmpty(scene.path));
            var selection = Selection.objects.Select(GlobalObjectId.GetGlobalObjectIdSlow).ToArray();
            var activeSelection = Array.IndexOf(Selection.objects, Selection.activeObject);
            using var fixture = new TemporaryScene();
            try
            {
                var target = fixture.OpenTarget();
                var extraCanvas = new GameObject("Invalid extra Canvas", typeof(Canvas));
                SceneManager.MoveGameObjectToScene(extraCanvas, target);
                fixture.RequireRepair(target);
                EditorSceneManager.OpenScene(fixture.ScenePath, OpenSceneMode.Single);
                Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
                var bytes = File.ReadAllBytes(fixture.ScenePath);

                var failure = Assert.Throws<InvalidOperationException>(() => Configure(fixture.ScenePath));

                Assert.That(failure.Message, Does.Contain("P8-SCENE-CANVAS"));
                Assert.That(File.ReadAllBytes(fixture.ScenePath), Is.EqualTo(bytes));
                var restored = SceneManager.GetSceneByPath(fixture.ScenePath);
                Assert.That(SceneManager.sceneCount, Is.EqualTo(1));
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(restored));
                Assert.That(restored.isLoaded, Is.True);
                Assert.That(restored.isDirty, Is.False);
                Assert.That(fixture.RuntimeRoot(restored).transform.position,
                    Is.EqualTo(new Vector3(3f, 4f, 5f)));
            }
            finally
            {
                if (originalDefaultBatchScene)
                    EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                else if (originalSetup.Length == 0 || originalWasEmpty)
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                else
                    EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                var restoredSelection = selection.Select(GlobalObjectId.GlobalObjectIdentifierToObjectSlow).ToArray();
                Selection.objects = restoredSelection;
                if (activeSelection >= 0) Selection.activeObject = restoredSelection[activeSelection];
            }
        }

        private static void Configure(string path)
        {
            var method = typeof(Phase8SceneSetup).GetMethod("ConfigureScene",
                BindingFlags.Static | BindingFlags.NonPublic);
            try { method.Invoke(null, new object[] { path, false }); }
            catch (TargetInvocationException exception) { throw exception.InnerException; }
        }

        private static string[] CaptureSetup() => EditorSceneManager.GetSceneManagerSetup()
            .Select(scene => scene.path + "|" + scene.isLoaded + "|" + scene.isActive).ToArray();

        // Every mutation belongs to a temporary Scene copy, never the production Scene.
        private sealed class TemporaryScene : IDisposable
        {
            private readonly string folder;
            private readonly Scene previousActive;
            public readonly Scene CallerScene;
            public readonly string ScenePath;

            public TemporaryScene()
            {
                previousActive = SceneManager.GetActiveScene();
                folder = "Assets/__Phase8SceneSafety_" + Guid.NewGuid().ToString("N");
                AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
                ScenePath = folder + "/Candidate.unity";
                try
                {
                    LegacyMainCafeFixture.Install(ScenePath);
                    // The test runner may own an unsaved Untitled Scene. Like Phase 6,
                    // create an independent Scene asset without saving that caller Scene.
                    var createScene = typeof(EditorSceneManager).GetMethod("CreateSceneAsset",
                        BindingFlags.Static | BindingFlags.NonPublic, null,
                        new[] { typeof(string), typeof(bool) }, null);
                    var callerPath = folder + "/Caller.unity";
                    if (createScene == null || !(bool)createScene.Invoke(null,
                            new object[] { callerPath, false }))
                        throw new InvalidOperationException("Could not create the temporary caller Scene.");
                    CallerScene = EditorSceneManager.OpenScene(callerPath, OpenSceneMode.Additive);
                    SceneManager.SetActiveScene(CallerScene);
                }
                catch
                {
                    AssetDatabase.DeleteAsset(folder);
                    throw;
                }
            }

            public Scene OpenTarget() => EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            public GameObject RuntimeRoot(Scene scene) => scene.GetRootGameObjects()
                .Single(root => root.name == Phase8AssetPaths.RuntimeRootName);

            public void RequireRepair(Scene scene)
            {
                RuntimeRoot(scene).transform.position = new Vector3(3f, 4f, 5f);
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(EditorSceneManager.SaveScene(scene, ScenePath), Is.True);
            }

            public void Dispose()
            {
                var target = SceneManager.GetSceneByPath(ScenePath);
                if (target.IsValid()) EditorSceneManager.CloseScene(target, true);
                if (CallerScene.IsValid() && CallerScene.isLoaded)
                    EditorSceneManager.CloseScene(CallerScene, true);
                if (previousActive.IsValid() && previousActive.isLoaded)
                    SceneManager.SetActiveScene(previousActive);
                AssetDatabase.DeleteAsset(folder);
            }
        }
    }

    public sealed class Phase8MainCafeMigrationTests
    {
        private LegacyMainCafeFixture legacy;
        [SetUp] public void SetUpLegacy()
        {
            legacy = new LegacyMainCafeFixture();
        }
        [TearDown] public void RestoreLegacy() { legacy?.Dispose(); legacy = null; }
        [Test]
        public void ConfigureMainCafe_Twice_IsByteStableWithOneRuntimeOwnerAndStableReferences()
        {
            Phase8AssetBuilder.BuildAssets();
            Phase8SceneSetup.ConfigureMainCafe();
            var firstHash = Hash(Phase8AssetPaths.MainCafeScenePath);
            var firstReferences = CapturePhase8References(Phase8AssetPaths.MainCafeScenePath);

            Phase8SceneSetup.ConfigureMainCafe();

            Assert.That(Hash(Phase8AssetPaths.MainCafeScenePath), Is.EqualTo(firstHash));
            Assert.That(CapturePhase8References(Phase8AssetPaths.MainCafeScenePath),
                Is.EqualTo(firstReferences));

            var scene = EditorSceneManager.OpenScene(
                Phase8AssetPaths.MainCafeScenePath, OpenSceneMode.Additive);
            try
            {
                Assert.That(scene.GetRootGameObjects().Count(root =>
                    root.name == Phase8AssetPaths.RuntimeRootName), Is.EqualTo(1));
                Assert.That(FindAll<DecorationModeController>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<SurfaceMountedSceneRegistry>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<SurfaceMountedPreviewView>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<PickUpPointIndicatorView>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<DecorationCatalogueView>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<DecorationActionBarView>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<DecorationModeTabsView>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<DecorationFloorRangeView>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<Canvas>(scene).Select(canvas => canvas.name),
                    Is.EquivalentTo(new[] { "HUD Canvas", "Screen Canvas", "Toast Canvas" }));
                Assert.That(FindAll<InteractionAnchorDebugView>(scene), Is.Empty);
                Assert.That(FindAll<Transform>(scene).Any(item =>
                    item.name.StartsWith("AnchorDebug_")), Is.False);

                var controller = FindAll<DecorationModeController>(scene).Single();
                var serialized = new SerializedObject(controller);
                Assert.That(AssetDatabase.GetAssetPath(
                    serialized.FindProperty("catalogueAsset").objectReferenceValue),
                    Is.EqualTo(Phase8AssetPaths.LegacyDecorationCataloguePath));
                Assert.That(AssetDatabase.GetAssetPath(
                    serialized.FindProperty("phase8FurnitureCatalogueAsset").objectReferenceValue),
                    Is.EqualTo(Phase8AssetPaths.FurnitureCataloguePath));
                Assert.That(AssetDatabase.GetAssetPath(
                    serialized.FindProperty("contentCatalog").objectReferenceValue),
                    Is.EqualTo(Phase8AssetPaths.ProductionContentCataloguePath));
                AssertPrefabSource(serialized, "catalogueView",
                    Phase8AssetPaths.CataloguePrefabPath);
                AssertPrefabSource(serialized, "actionBarView",
                    Phase8AssetPaths.ActionBarPrefabPath);
                Assert.That(serialized.FindProperty("modeTabsView").objectReferenceValue,
                    Is.SameAs(FindAll<DecorationModeTabsView>(scene).Single()));
                Assert.That(serialized.FindProperty("floorRangeView").objectReferenceValue,
                    Is.SameAs(FindAll<DecorationFloorRangeView>(scene).Single()));
                foreach (var propertyName in Phase8AssetPaths.RequiredControllerReferences)
                {
                    var property = serialized.FindProperty(propertyName);
                    Assert.That(property, Is.Not.Null, propertyName);
                    Assert.That(property.objectReferenceValue, Is.Not.Null, propertyName);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void StartupCatalogueComposition_NullPhase8KeepsExactLegacyCategories_NonNullAddsThreeRowsOnce()
        {
            Phase8AssetBuilder.BuildAssets();
            var legacy = DecorationCatalogueModelBuilder.Build(
                AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                    Phase8AssetPaths.LegacyDecorationCataloguePath),
                AssetDatabase.LoadAssetAtPath<SurfaceStyleCatalogueAsset>(
                    Phase7AssetPaths.FloorCataloguePath),
                AssetDatabase.LoadAssetAtPath<SurfaceStyleCatalogueAsset>(
                    Phase7AssetPaths.WallpaperCataloguePath),
                AssetDatabase.LoadAssetAtPath<SurfaceStyleCatalogueAsset>(
                    Phase7AssetPaths.PaintCataloguePath),
                AssetDatabase.LoadAssetAtPath<SurfaceStyleCatalogueAsset>(
                    Phase7AssetPaths.WainscotingCataloguePath),
                AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(
                    Phase7AssetPaths.WallMountedProductionCataloguePath),
                AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(
                    Phase7AssetPaths.WindowCataloguePath));
            var compose = typeof(DecorationModeController).GetMethod(
                "ComposeStartupCatalogueCategories",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(compose, Is.Not.Null,
                "Task 9 needs one deterministic composition seam used by fresh Scene Awake.");

            var nullResult = (IReadOnlyList<DecorationCategoryModel>)compose.Invoke(
                null, new object[] { legacy, null });
            Assert.That(nullResult, Is.SameAs(legacy));
            Assert.That(nullResult.Select(category => category.CategoryId),
                Is.EqualTo(new[]
                {
                    "furniture", "floor", "wallpaper", "paint",
                    "wainscoting", "wall-decor", "windows"
                }));

            var phase8 = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                Phase8AssetPaths.FurnitureCataloguePath);
            var combined = (IReadOnlyList<DecorationCategoryModel>)compose.Invoke(
                null, new object[] { legacy, phase8 });
            Assert.That(combined.Select(category => category.CategoryId),
                Is.EqualTo(new[]
                {
                    "furniture", "cash-register", "coffee-machine",
                    "floor", "wallpaper", "paint", "wainscoting", "wall-decor", "windows"
                }));
            Assert.That(combined.Select(category => category.CategoryId).Distinct().Count(),
                Is.EqualTo(combined.Count));
            Assert.That(combined.Take(3).Select(category => category.Items.Count),
                Is.EqualTo(new[] { 4, 1, 1 }));
        }

        [Test]
        public void ConfigureValidationScene_Twice_IsByteStableAndKeepsDebugOutOfMainCafe()
        {
            Phase8AssetBuilder.BuildAssets();
            Phase8SceneSetup.ConfigureValidationScene();
            var firstHash = Hash(Phase8AssetPaths.ValidationScenePath);
            Phase8SceneSetup.ConfigureValidationScene();
            Assert.That(Hash(Phase8AssetPaths.ValidationScenePath), Is.EqualTo(firstHash));

            var validation = EditorSceneManager.OpenScene(
                Phase8AssetPaths.ValidationScenePath, OpenSceneMode.Additive);
            try
            {
                Assert.That(FindAll<InteractionAnchorDebugView>(validation), Has.Length.EqualTo(1));
                var controller = FindAll<DecorationModeController>(validation).Single();
                var serialized = new SerializedObject(controller);
                Assert.That(serialized.FindProperty("interactionAnchorDebugVisible").boolValue,
                    Is.True);
                Assert.That(serialized.FindProperty("interactionAnchorDebugView")
                    .objectReferenceValue, Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(validation, true);
            }
        }

        private static string[] CapturePhase8References(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                var controller = FindAll<DecorationModeController>(scene).Single();
                var serialized = new SerializedObject(controller);
                return Phase8AssetPaths.RequiredControllerReferences
                    .Concat(new[]
                    {
                        "phase8FurnitureCatalogueAsset", "modeTabsView", "floorRangeView"
                    })
                    .Select(name => GlobalObjectId.GetGlobalObjectIdSlow(
                        serialized.FindProperty(name).objectReferenceValue).ToString())
                    .ToArray();
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void AssertPrefabSource(
            SerializedObject serialized,
            string propertyName,
            string expectedPath)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(
                serialized.FindProperty(propertyName).objectReferenceValue);
            Assert.That(AssetDatabase.GetAssetPath(source), Is.EqualTo(expectedPath));
        }

        private static T[] FindAll<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();

        private static string Hash(string path)
        {
            using var sha = SHA256.Create();
            return System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));
        }
    }
}

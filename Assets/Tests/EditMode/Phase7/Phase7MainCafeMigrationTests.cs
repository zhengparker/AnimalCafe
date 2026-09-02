using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.Phase7;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEditor;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using AnimalCafe.UI.Decoration;

namespace AnimalCafe.Tests.EditMode.Phase7
{
    public sealed class Phase7MainCafeMigrationTests
    {
        [Test]
        public void MigrateMainCafe_RemovesTemporaryPreplacedWindowButKeepsWindowCatalogueEntries()
        {
            Phase7DecorationSceneSetup.MigrateMainCafe();
            var scene = EditorSceneManager.OpenScene(Phase7AssetPaths.MainCafeScenePath, OpenSceneMode.Additive);
            try
            {
                var window = FindAll<Transform>(scene).Single(item => item.name == "P4_Window_BackRight_C3_R0");
                Assert.That(window.gameObject.activeSelf, Is.False,
                    "The temporary preplaced window must not appear in gameplay.");
                Assert.That(window.GetComponent<WallMountedSeedAuthoring>(), Is.Null);
                var controller = FindAll<DecorationModeController>(scene).Single();
                var so = new SerializedObject(controller);
                Assert.That(so.FindProperty("phase7MountedSeeds").arraySize, Is.Zero);
                var catalogue = AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(Phase7AssetPaths.WindowCataloguePath);
                Assert.That(catalogue.Entries.Count, Is.EqualTo(2));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void Phase6Validator_AllowsOnlyCanonicalMainCafeWindowInactiveOverride()
        {
            Phase7DecorationSceneSetup.MigrateMainCafe();
            var baseline = AnimalCafe.EditorTools.Phase6.Phase6DecorationValidator.ValidateAll();
            Assert.That(baseline.Issues.Any(issue =>
                issue.Code == AnimalCafe.EditorTools.Phase6.Phase6DecorationIssueCode.EnvironmentPrefabDrift
                && issue.AssetPath == Phase7AssetPaths.MainCafeScenePath
                && issue.ObjectPath.EndsWith("P4_Window_BackRight_C3_R0")), Is.False);

            var scene = EditorSceneManager.OpenScene(Phase7AssetPaths.MainCafeScenePath, OpenSceneMode.Additive);
            try
            {
                var otherPrefabRoot = FindAll<Transform>(scene).Single(item => item.name == "P4_Wall_BackLeft");
                otherPrefabRoot.gameObject.SetActive(false);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                var drift = AnimalCafe.EditorTools.Phase6.Phase6DecorationValidator.ValidateAll();
                Assert.That(drift.Issues.Any(issue =>
                    issue.Code == AnimalCafe.EditorTools.Phase6.Phase6DecorationIssueCode.EnvironmentPrefabDrift
                    && issue.AssetPath == Phase7AssetPaths.MainCafeScenePath
                    && issue.ObjectPath.EndsWith("P4_Wall_BackLeft")), Is.True,
                    "The exception must not allow m_IsActive drift on another MainCafe Prefab root.");
                otherPrefabRoot.gameObject.SetActive(true);
                PrefabUtility.RevertPropertyOverride(
                    new SerializedObject(otherPrefabRoot.gameObject).FindProperty("m_IsActive"),
                    InteractionMode.AutomatedAction);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                var otherPrefabRoot = FindAll<Transform>(scene).Single(item => item.name == "P4_Wall_BackLeft");
                if (!otherPrefabRoot.gameObject.activeSelf)
                {
                    otherPrefabRoot.gameObject.SetActive(true);
                    PrefabUtility.RevertPropertyOverride(
                        new SerializedObject(otherPrefabRoot.gameObject).FindProperty("m_IsActive"),
                        InteractionMode.AutomatedAction);
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void Phase6Validator_AllowsOnlyStructurallyExactCanonicalPhase7WallExtensions()
        {
            Phase7DecorationSceneSetup.MigrateMainCafe();

            var issues = AnimalCafe.EditorTools.Phase6.Phase6DecorationValidator
                .ValidateAll().Issues;
            foreach (var wallName in new[] { "P4_Wall_BackLeft", "P4_Wall_BackRight" })
            {
                Assert.That(issues.Any(issue =>
                    issue.Code == AnimalCafe.EditorTools.Phase6.Phase6DecorationIssueCode.EnvironmentPrefabDrift
                    && issue.AssetPath == Phase7AssetPaths.MainCafeScenePath
                    && issue.ObjectPath.Contains(wallName)), Is.False,
                    wallName + " must accept only the exact generated Phase 7 wall extension.");
            }
        }

        [TestCase("P4_Wall_BackLeft", "wrong-name")]
        [TestCase("P4_Wall_BackLeft", "extra-component")]
        [TestCase("P4_Wall_BackLeft", "transform")]
        [TestCase("P4_Wall_BackLeft", "material")]
        [TestCase("P4_Wall_BackLeft", "shadow")]
        [TestCase("P4_Wall_BackLeft", "override")]
        [TestCase("P4_Wall_BackLeft", "nested-prefab")]
        [TestCase("P4_Wall_BackLeft", "custom-mesh")]
        [TestCase("P4_Wall_BackRight", "wrong-name")]
        [TestCase("P4_Wall_BackRight", "extra-component")]
        [TestCase("P4_Wall_BackRight", "transform")]
        [TestCase("P4_Wall_BackRight", "material")]
        [TestCase("P4_Wall_BackRight", "shadow")]
        [TestCase("P4_Wall_BackRight", "override")]
        [TestCase("P4_Wall_BackRight", "nested-prefab")]
        [TestCase("P4_Wall_BackRight", "custom-mesh")]
        public void Phase6Validator_RejectsHostilePhase7WallExtensionMutation(
            string wallName,
            string mutation)
        {
            Phase7DecorationSceneSetup.MigrateMainCafe();
            var scene = EditorSceneManager.OpenScene(
                Phase7AssetPaths.MainCafeScenePath,
                OpenSceneMode.Additive);
            Mesh substitutedMesh = null;
            try
            {
                var wall = FindAll<Transform>(scene).Single(item => item.name == wallName);
                var finish = wall.Find("Phase7_WallFinish");
                var bodyRenderer = wall.Find("WallVisual").GetComponent<Renderer>();
                var finishRenderer = finish.GetComponent<Renderer>();

                switch (mutation)
                {
                    case "wrong-name":
                        finish.name = "Phase7_WallFinish_Wrong";
                        break;
                    case "extra-component":
                        finish.gameObject.AddComponent<BoxCollider>();
                        break;
                    case "transform":
                        finish.localPosition += new Vector3(.1f, 0f, 0f);
                        break;
                    case "material":
                        finishRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                            "Assets/Art/Phase4/Environment/Materials/M_Environment_Entrance_01.mat");
                        break;
                    case "shadow":
                        finishRenderer.shadowCastingMode =
                            UnityEngine.Rendering.ShadowCastingMode.On;
                        break;
                    case "override":
                        bodyRenderer.renderingLayerMask = 1u;
                        break;
                    case "nested-prefab":
                    {
                        var nestedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                            "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Entrance_2x2.prefab");
                        var nestedInstance = PrefabUtility.InstantiatePrefab(
                            nestedPrefab,
                            scene) as GameObject;
                        nestedInstance.transform.SetParent(wall, false);
                        break;
                    }
                    case "custom-mesh":
                        substitutedMesh = new Mesh { name = "Cube" };
                        finish.GetComponent<MeshFilter>().sharedMesh = substitutedMesh;
                        break;
                    default:
                        Assert.Fail("Unknown hostile wall mutation: " + mutation);
                        break;
                }

                var issues = AnimalCafe.EditorTools.Phase6.Phase6DecorationValidator
                    .ValidateAll().Issues;
                Assert.That(issues.Any(issue =>
                    issue.Code == AnimalCafe.EditorTools.Phase6.Phase6DecorationIssueCode.EnvironmentPrefabDrift
                    && issue.AssetPath == Phase7AssetPaths.MainCafeScenePath
                    && issue.ObjectPath.Contains(wallName)), Is.True,
                    wallName + " / " + mutation
                    + " must remain outside the canonical Phase 7 allowlist.");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (substitutedMesh != null)
                    UnityEngine.Object.DestroyImmediate(substitutedMesh);
            }
        }

        [Test]
        public void MigrateMainCafe_PlacesTheOnlyModeTabsInsideTheBottomSheet()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();
            Phase7DecorationSceneSetup.MigrateMainCafe();
            var scene = EditorSceneManager.OpenScene(Phase7AssetPaths.MainCafeScenePath, OpenSceneMode.Additive);
            try
            {
                var tabs = FindAll<DecorationModeTabsView>(scene).ToArray();
                var catalogue = FindAll<DecorationCatalogueView>(scene).Single();
                var actionBars = FindAll<DecorationActionBarView>(scene).ToArray();
                Assert.That(tabs, Has.Length.EqualTo(1));
                Assert.That(actionBars, Has.Length.EqualTo(1),
                    "MainCafe must reuse one Action Bar instead of authoring a Surface duplicate.");
                Assert.That(tabs[0].transform.IsChildOf(catalogue.transform), Is.True,
                    "Mode tabs must move with the Bottom Sheet and sit at its top edge.");
                var catalogueSo = new SerializedObject(catalogue);
                var surfaceFooter = catalogueSo.FindProperty("surfaceFooterHost")?.objectReferenceValue as RectTransform;
                Assert.That(surfaceFooter, Is.Not.Null);
                Assert.That(surfaceFooter.parent, Is.SameAs(catalogue.transform),
                    "Surface footer must be hosted by the same moving Sheet root.");
                var ranges = FindAll<DecorationFloorRangeView>(scene).ToArray();
                Assert.That(ranges, Has.Length.EqualTo(1));
                Assert.That(ranges[0].transform.parent, Is.SameAs(surfaceFooter),
                    "Floor range controls must move with the Surface footer instead of overlaying it from a fixed screen position.");
                foreach (var rangeButton in ranges[0].GetComponentsInChildren<UnityEngine.UI.Button>(true))
                {
                    Assert.That(((RectTransform)rangeButton.transform).anchoredPosition.y,
                        Is.GreaterThanOrEqualTo(94f),
                        rangeButton.name + " must occupy the first footer row above the action buttons.");
                }
                var tabRect = (RectTransform)tabs[0].transform;
                Assert.That(tabRect.anchoredPosition.y, Is.GreaterThanOrEqualTo(700f));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void MigrateMainCafe_Twice_IsIdempotentAndPreservesCanonicalRoots()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();
            Phase7DecorationSceneSetup.MigrateMainCafe();
            var first=Hash(Phase7AssetPaths.MainCafeScenePath);
            Phase7DecorationSceneSetup.MigrateMainCafe();
            Assert.That(Hash(Phase7AssetPaths.MainCafeScenePath),Is.EqualTo(first));

            var scene = EditorSceneManager.OpenScene(
                Phase7AssetPaths.MainCafeScenePath, OpenSceneMode.Additive);
            try
            {
                Assert.That(FindAll<DecorationModeController>(scene).Count(), Is.EqualTo(1));
                Assert.That(FindAll<WallSurfaceRegistry>(scene).Count(), Is.EqualTo(1));
                Assert.That(FindAll<FloorSurfaceGridView>(scene).Count(), Is.EqualTo(1));
                var walls = FindAll<WallSurfaceAuthoring>(scene).ToArray();
                Assert.That(walls.Select(wall => wall.SurfaceId),
                    Is.EquivalentTo(new[] { "wall.back-left", "wall.back-right" }));
                Assert.That(walls.All(wall => wall.Columns == 8 && wall.Rows == 2), Is.True);
                var transforms = scene.GetRootGameObjects().SelectMany(root =>
                    root.GetComponentsInChildren<UnityEngine.Transform>(true)).ToArray();
                Assert.That(transforms.Any(transform => transform.name == "DecorationSpaceRoot"), Is.True);
                Assert.That(transforms.Any(transform => transform.name.Contains("Window")), Is.True);
                Assert.That(transforms.Count(transform=>transform.name=="P4_Window_BackRight_C3_R0"),Is.EqualTo(1));
                var windowCatalogue=AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(Phase7AssetPaths.WindowCataloguePath);
                Assert.That(windowCatalogue.Entries.Select(entry=>entry.DefinitionId),Is.EqualTo(new[]{"window.canonical.phase4","window.tall-glass.1x2.01"}));
                Assert.That(AssetDatabase.GetAssetPath(windowCatalogue.Entries.Single(entry=>entry.DefinitionId=="window.canonical.phase4").Prefab),Is.EqualTo(Phase7AssetPaths.FormalPrefabFolder+"/PF_Window_1x1_01.prefab"));
                var canonicalWindow=transforms.Single(transform=>transform.name=="P4_Window_BackRight_C3_R0");
                Assert.That(canonicalWindow.gameObject.activeSelf,Is.False);
                Assert.That(canonicalWindow.GetComponent<WallMountedSeedAuthoring>(),Is.Null);
                var entrance=transforms.Single(transform=>transform.name=="P4_Entrance").gameObject;
                Assert.That(entrance.GetComponent<EntrancePortalAuthoring>(),Is.Not.Null);
                Assert.That(entrance.GetComponentsInChildren<Renderer>(true).SelectMany(renderer=>renderer.sharedMaterials).Where(material=>material!=null)
                    .Any(material=>AssetDatabase.GetAssetPath(material)=="Assets/Art/Phase4/Environment/Materials/M_Environment_Entrance_01.mat"),Is.True);
                Assert.That(scene.GetRootGameObjects().Count(root=>root.name=="Phase6_DecorationRuntime"),Is.EqualTo(1));
                Assert.That(scene.GetRootGameObjects().Count(root=>root.name=="Phase7_InteriorRuntime"),Is.EqualTo(1));
                Assert.That(transforms.Count(transform=>transform.name=="Phase7_UIRuntime"),Is.EqualTo(1));
                var controller=FindAll<DecorationModeController>(scene).Single();
                var serialized=new SerializedObject(controller);
                Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(serialized.FindProperty("catalogueView").objectReferenceValue)),Is.EqualTo(Phase7AssetPaths.CataloguePrefabPath));
                Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(serialized.FindProperty("actionBarView").objectReferenceValue)),Is.EqualTo(Phase7AssetPaths.ActionBarPrefabPath));
                Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(serialized.FindProperty("exitModalView").objectReferenceValue)),Is.EqualTo(Phase7AssetPaths.ExitModalPrefabPath));
                var bodyMaterial=AssetDatabase.LoadAssetAtPath<Material>(Phase7AssetPaths.WallBodyMaterialPath);
                Assert.That(bodyMaterial,Is.Not.Null);Assert.That(bodyMaterial.shader.name,Is.EqualTo("Universal Render Pipeline/Lit"));
                foreach(var wall in walls)
                {
                    Assert.That(wall.transform.Find("WallVisual").GetComponent<Renderer>().sharedMaterial,Is.SameAs(bodyMaterial),wall.SurfaceId);
                    foreach(var childName in new[]{"Phase7_WallFinish","Phase7_WainscotingFinish","Phase7_WainscotingRailLip","Phase7_WainscotingBaseboardLip"})
                    {
                        var child=wall.transform.Find(childName);
                        Assert.That(child,Is.Not.Null,wall.SurfaceId+" "+childName);
                        Assert.That(child.GetComponentsInChildren<Collider>(true),Is.Empty,wall.SurfaceId+" "+childName);
                    }
                }
                foreach(var property in new[]{"modeTabsView","floorRangeView","exitModalView","projectionValidMaterial","projectionInvalidMaterial"})
                {
                    var value=serialized.FindProperty(property);
                    Assert.That(value,Is.Not.Null,property);
                    Assert.That(value.objectReferenceValue,Is.Not.Null,property);
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void Setup_BothScenes_AuthorsVisibleNonOverlappingPhase7UiAndTestOnlyFixtures()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();
            Phase7DecorationSceneSetup.MigrateMainCafe();
            Phase7DecorationSceneSetup.ConfigureValidationScene();
            foreach(var path in new[]{Phase7AssetPaths.MainCafeScenePath,Phase7AssetPaths.ValidationScenePath})
            {
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    var ui=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true)).Single(x=>x.name=="Phase7_UIRuntime");
                    Assert.That(ui.localScale,Is.EqualTo(Vector3.one),path);
                    Assert.That(ui.lossyScale.x,Is.GreaterThan(.1f),path+" must remain visibly scaled under its CanvasScaler hierarchy");
                    var tabs=FindAll<DecorationModeTabsView>(scene).Single();
                    var range=FindAll<DecorationFloorRangeView>(scene).Single();
                    var exitModal=FindAll<DecorationExitModalView>(scene).Single();
                    Assert.That(exitModal.transform.parent.name,Is.EqualTo("Screen Canvas"),path);
                    Assert.That(exitModal.transform.IsChildOf(ui),Is.False,
                        path+": Exit Modal must not inherit the Bottom Sheet runtime bounds.");
                    var buttons=ui.GetComponentsInChildren<UnityEngine.UI.Button>(true)
                        .Concat(tabs.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                        .Concat(range.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                        .Concat(exitModal.GetComponentsInChildren<UnityEngine.UI.Button>(true)).ToArray();
                    Assert.That(buttons.Length,Is.GreaterThanOrEqualTo(8));
                    Assert.That(buttons.All(button=>button.GetComponentInChildren<TMPro.TMP_Text>(true)!=null),Is.True);
                    var rects=buttons.Select(button=>(RectTransform)button.transform).ToArray();
                    Assert.That(rects.All(rect=>WorldRect(rect).width>=18f&&WorldRect(rect).height>=18f),Is.True);
                    for(var i=0;i<rects.Length;i++)for(var j=i+1;j<rects.Length;j++)
                    {
                        if(!rects[i].gameObject.activeInHierarchy||!rects[j].gameObject.activeInHierarchy)continue;
                        var modalPair=rects[i].GetComponentInParent<DecorationExitModalView>(true)!=null
                            ||rects[j].GetComponentInParent<DecorationExitModalView>(true)!=null;
                        if(modalPair)continue;
                        var first=WorldRect(rects[i]);var second=WorldRect(rects[j]);var folderPair=rects[i].parent==rects[j].parent&&rects[i].parent.GetComponent<AnimalCafe.UI.Decoration.DecorationModeTabsView>()!=null;
                        if(folderPair){var overlap=Mathf.Min(first.xMax,second.xMax)-Mathf.Max(first.xMin,second.xMin);Assert.That(overlap,Is.LessThanOrEqualTo(20f),$"{path}: folder overlap {rects[i].name}/{rects[j].name}");}
                        else Assert.That(first.Overlaps(second),Is.False,$"{path}: {rects[i].name}/{rects[j].name}");
                    }
                    if(path==Phase7AssetPaths.ValidationScenePath)
                    {
                        var names=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>(true)).Select(x=>x.name);
                        Assert.That(names,Does.Contain("TEST_ONLY_WallFixture_2x2"));
                        Assert.That(names,Does.Contain("TEST_ONLY_WallFixture_3x2"));
                    }
                }
                finally{EditorSceneManager.CloseScene(scene,true);}
            }
        }

        [Test]
        public void ConfigureValidationScene_Twice_PreservesExactSceneBytes()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();Phase7DecorationSceneSetup.ConfigureValidationScene();
            var first=Hash(Phase7AssetPaths.ValidationScenePath);Phase7DecorationSceneSetup.ConfigureValidationScene();
            Assert.That(Hash(Phase7AssetPaths.ValidationScenePath),Is.EqualTo(first));
        }

        [TestCase("wrong-object")]
        [TestCase("wrong-instance")]
        [TestCase("wrong-definition")]
        [TestCase("wrong-surface")]
        [TestCase("wrong-slot")]
        [TestCase("wrong-footprint")]
        [TestCase("duplicate")]
        public void Phase6Validator_RejectsEveryNonCanonicalWindowSeedVariant(string variant)
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();
            Phase7DecorationSceneSetup.MigrateMainCafe();
            var scene = EditorSceneManager.OpenScene(Phase7AssetPaths.MainCafeScenePath, OpenSceneMode.Additive);
            try
            {
                var transforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                var window = transforms.Single(x => x.name == "P4_Window_BackRight_C3_R0").gameObject;
                var target = window.AddComponent<WallMountedSeedAuthoring>();
                if (variant == "wrong-object")
                {
                    UnityEngine.Object.DestroyImmediate(target);
                    target = transforms.Single(x => x.name == "P4_Wall_BackRight").gameObject.AddComponent<WallMountedSeedAuthoring>();
                }
                else if (variant == "duplicate")
                    window.AddComponent<WallMountedSeedAuthoring>();

                var serialized = new SerializedObject(target);
                CopySeed(serialized);
                switch (variant)
                {
                    case "wrong-instance": serialized.FindProperty("instanceId").stringValue = "wall-mounted.main.window.other"; break;
                    case "wrong-definition": serialized.FindProperty("definitionId").stringValue = "window.other"; break;
                    case "wrong-surface": serialized.FindProperty("surfaceId").stringValue = "wall.back-left"; break;
                    case "wrong-slot": serialized.FindProperty("column").intValue = 2; break;
                    case "wrong-footprint": serialized.FindProperty("footprintWidth").intValue = 2; break;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var issues = AnimalCafe.EditorTools.Phase6.Phase6DecorationValidator.ValidateAll().Issues;
                Assert.That(issues.Any(issue => issue.Code == AnimalCafe.EditorTools.Phase6.Phase6DecorationIssueCode.EnvironmentPrefabDrift
                    && issue.AssetPath == Phase7AssetPaths.MainCafeScenePath), Is.True, variant);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        private static void CopySeed(SerializedObject serialized)
        {
            serialized.FindProperty("instanceId").stringValue = "wall-mounted.main.window.canonical.01";
            serialized.FindProperty("definitionId").stringValue = "window.canonical.phase4";
            serialized.FindProperty("surfaceId").stringValue = "wall.back-right";
            serialized.FindProperty("column").intValue = 3;
            serialized.FindProperty("row").intValue = 0;
            serialized.FindProperty("footprintWidth").intValue = 1;
            serialized.FindProperty("footprintHeight").intValue = 1;
        }

        private static Rect WorldRect(RectTransform rect)
        { var corners=new Vector3[4];rect.GetWorldCorners(corners);return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y); }

        private static T[] FindAll<T>(Scene scene) where T : UnityEngine.Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private static string Hash(string path){using var sha=SHA256.Create();return System.BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)));}
    }
}

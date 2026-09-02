using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.EditorTools.Phase7;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.EditMode.Phase7
{
    public sealed class Phase7ValidatorTests
    {
        [Test]
        public void ValidateAll_CommittedProductionState_HasNoIssuesAndExcludesValidationScene()
        {
            var report = Phase7Validator.ValidateAll();

            Assert.That(report.Issues, Is.Empty,
                string.Join("\n", report.Issues.Select(issue => issue.Code + ": " + issue.Message)));
            Assert.That(EditorBuildSettings.scenes.Any(scene =>
                scene.enabled && scene.path == Phase7AssetPaths.ValidationScenePath), Is.False);
            Assert.That(EditorBuildSettings.scenes.Count(scene =>
                scene.enabled && scene.path == Phase7AssetPaths.MainCafeScenePath), Is.EqualTo(1));
        }

        [Test]
        public void ValidateAll_DuplicateInteriorRuntimeReportsRootIssueWithoutThrowing()
        {
            var existingMain = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .FirstOrDefault(scene => scene.path == Phase7AssetPaths.MainCafeScenePath);
            var existingValidation = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .FirstOrDefault(scene => scene.path == Phase7AssetPaths.ValidationScenePath);
            if (existingMain.IsValid() || existingValidation.IsValid())
                Assert.Ignore(
                    "Duplicate-root validation owns temporary target Scene handles; "
                    + "close caller-owned Phase 7 target Scenes before running it.");
            var main = EditorSceneManager.OpenScene(
                Phase7AssetPaths.MainCafeScenePath,
                OpenSceneMode.Additive);
            var validation = EditorSceneManager.OpenScene(
                Phase7AssetPaths.ValidationScenePath,
                OpenSceneMode.Additive);
            var source = main.GetRootGameObjects()
                .Single(root => root.name == "Phase7_InteriorRuntime");
            var duplicate = Object.Instantiate(source);
            duplicate.name = source.name;
            SceneManager.MoveGameObjectToScene(duplicate, main);
            var laterWall = validation.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WallSurfaceAuthoring>(true))
                .First();
            var laterWallSerialized = new SerializedObject(laterWall);
            var columns = laterWallSerialized.FindProperty("columns");
            var originalColumns = columns.intValue;
            columns.intValue = originalColumns - 1;
            laterWallSerialized.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                Phase7ValidationReport report = null;
                Assert.DoesNotThrow(() => report = Phase7Validator.ValidateAll(),
                    "Duplicate roots must be reported instead of terminating validation.");
                Assert.That(report.Issues, Has.Some.Matches<Phase7ValidationIssue>(issue =>
                    issue.Code == "P7-SCENE-ROOTS"
                    && issue.Message == Phase7AssetPaths.MainCafeScenePath));
                Assert.That(report.Issues, Has.Some.Matches<Phase7ValidationIssue>(issue =>
                    issue.Code == "P7-SCENE-WALL-SLOTS"
                    && issue.Message == Phase7AssetPaths.ValidationScenePath),
                    "Validation must continue into the later Scene after reporting duplicate roots.");
            }
            finally
            {
                Object.DestroyImmediate(duplicate);
                columns.intValue = originalColumns;
                laterWallSerialized.ApplyModifiedPropertiesWithoutUndo();
                if (validation.IsValid() && validation.isLoaded)
                    EditorSceneManager.CloseScene(validation, true);
                if (main.IsValid() && main.isLoaded)
                    EditorSceneManager.CloseScene(main, true);
            }
        }

        [Test]
        public void ValidateSurfaceDefinition_RejectsWrongNoneKindAndMissingNormalMaterial()
        {
            var definition = ScriptableObject.CreateInstance<SurfaceStyleDefinitionAsset>();
            try
            {
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("styleId").stringValue = "invalid.none";
                serialized.FindProperty("kind").enumValueIndex = (int)SurfaceStyleKind.Paint;
                serialized.FindProperty("isNoneOption").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(Phase7Validator.ValidateSurfaceDefinition(definition)
                    .Select(issue => issue.Code), Does.Contain("P7-SURFACE-NONE-KIND"));
            }
            finally { Object.DestroyImmediate(definition); }
        }

        [Test]
        public void SharedWaistCutoff_IsNamedAndDerivedFromCanonicalWallHeight()
        {
            Assert.That(CharacterScaleReference.SharedCharacterWaistHeightMeters, Is.EqualTo(0.65f));
            Assert.That(CharacterScaleReference.GetNormalizedWainscotingCutoff(2f), Is.EqualTo(0.325f));
        }

        [Test]
        public void ProductionStyleLookup_AcceptsWainscotingNoneAsNonRenderableRemovalOption()
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();
            var entries=new[]{Phase7AssetPaths.PaintCataloguePath,Phase7AssetPaths.WallpaperCataloguePath,
                Phase7AssetPaths.WainscotingCataloguePath,Phase7AssetPaths.FloorCataloguePath}
                .SelectMany(path=>AssetDatabase.LoadAssetAtPath<SurfaceStyleCatalogueAsset>(path).Entries);
            Assert.DoesNotThrow(()=>new SurfaceStyleLookup(entries));
        }

        [TestCase("wall-decor.monitor.01", "PF_WallDecor_1x1_01")]
        [TestCase("wall-decor.shiba-painting.01", "PF_WallDecor_1x2_01")]
        [TestCase("wall-decor.wood-shelf.01", "PF_WallDecor_2x1_01")]
        [TestCase("window.canonical.phase4", "PF_Window_1x1_01")]
        [TestCase("window.tall-glass.1x2.01", "PF_Window_1x2_01")]
        public void ProductionWallMountedEntry_BindsItsRealVisiblePrefab(
            string definitionId,
            string expectedPrefabName)
        {
            var definitions = new[]
                {
                    Phase7AssetPaths.WallMountedProductionCataloguePath,
                    Phase7AssetPaths.WindowCataloguePath
                }
                .SelectMany(path => AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(path).Entries)
                .ToArray();
            var definition = definitions.Single(item => item.DefinitionId == definitionId);

            Assert.That(definition.Prefab, Is.Not.Null, definitionId);
            Assert.That(definition.Prefab.name, Is.EqualTo(expectedPrefabName), definitionId);
            Assert.That(AssetDatabase.GetAssetPath(definition.Prefab),
                Is.EqualTo(Phase7AssetPaths.FormalPrefabFolder + "/" + expectedPrefabName + ".prefab"));
            Assert.That(definition.Prefab.GetComponentsInChildren<Renderer>(true), Is.Not.Empty,
                definitionId + " must bind a real visible production prefab, not a thumbnail/fallback.");
            Assert.That(definition.Prefab.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null),
                Is.Not.Empty, definitionId + " must retain production Materials.");
            Assert.That(definition.Prefab.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(definition.Prefab.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void PlayerSettingsAndCanonicalEntranceMaterial_AreNotDestructivelyRewritten()
        {
            const string globalSettingsPath="Assets/Settings/UniversalRenderPipelineGlobalSettings.asset";
            const string entrancePath="Assets/Art/Phase4/Environment/Materials/M_Environment_Entrance_01.mat";
            var globalSettingsBefore=File.ReadAllBytes(globalSettingsPath);
            var entranceBefore=File.ReadAllBytes(entrancePath);
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();
            Assert.That(File.ReadAllBytes(globalSettingsPath),Is.EqualTo(globalSettingsBefore),
                "The Phase 7 builder must preserve URP global settings byte-for-byte.");
            Assert.That(File.ReadAllBytes(entrancePath),Is.EqualTo(entranceBefore),
                "The Phase 7 builder must preserve the canonical Entrance Material byte-for-byte.");
            var entrance=AssetDatabase.LoadAssetAtPath<Material>(entrancePath);
            Assert.That(entrance,Is.Not.Null);
            Assert.That(entrance.HasProperty("_EmissionMap"),Is.True);
            Assert.That(entrance.HasProperty("_EmissionColor"),Is.True);
            Assert.That(entrance.GetColor("_EmissionColor").maxColorComponent,Is.GreaterThan(0f),
                "Unity may normalize the serialized keyword cache; loaded emission semantics remain authoritative.");
        }

        [TestCase("footprint")]
        [TestCase("root-transform")]
        [TestCase("min-y")]
        [TestCase("min-z")]
        [TestCase("depth")]
        [TestCase("max-depth")]
        [TestCase("collider-trigger")]
        [TestCase("collider-layer")]
        [TestCase("rigidbody")]
        [TestCase("nav")]
        [TestCase("mesh-uv")]
        [TestCase("material")]
        [TestCase("alpha")]
        [TestCase("glass")]
        [TestCase("thumbnail")]
        [TestCase("thumbnail-hash")]
        [TestCase("thumbnail-backdrop")]
        [TestCase("raw-hash")]
        [TestCase("derived-hash")]
        public void ValidateAll_RejectsEachFormalAssetDriftWithoutBuilderRepair(string drift)
        {
            Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();
            var decor=AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(Phase7AssetPaths.WallMountedProductionCataloguePath);
            var windows=AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(Phase7AssetPaths.WindowCataloguePath);
            var definition=drift=="glass"?windows.Entries[0]:decor.Entries[0];
            string mutatedFile=null;byte[] originalBytes=null;string temporaryMesh=null;
            try
            {
                if(drift=="footprint")SetInt(definition,"footprintWidth",2);
                else if(drift=="max-depth")SetFloat(definition,"maxVisualDepth",0f);
                else if(drift=="thumbnail")SetObject(definition,"thumbnail",null);
                else if(drift=="alpha")
                {
                    definition=decor.Entries.Single(x=>x.DefinitionId=="wall-decor.shiba-painting.01");
                    var material=definition.Prefab.GetComponentsInChildren<Renderer>(true).SelectMany(x=>x.sharedMaterials).Single(x=>x.name.Contains("ShibaPortrait"));
                    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");material.renderQueue=2000;EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
                }
                else if(drift=="glass")
                {
                    var material=definition.Prefab.GetComponentsInChildren<Renderer>(true).SelectMany(x=>x.sharedMaterials).First(x=>x.name.Contains("Glass"));
                    material.SetInt("_ZWrite",1);material.renderQueue=2000;EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
                }
                else if(drift=="thumbnail-backdrop")
                {
                    mutatedFile=AssetDatabase.GetAssetPath(definition.Thumbnail);
                    originalBytes=File.ReadAllBytes(mutatedFile);
                    var opaqueBackdrop=new Texture2D(256,256,TextureFormat.RGBA32,false);
                    try
                    {
                        opaqueBackdrop.SetPixels(Enumerable.Repeat(
                            new Color(.72f,.62f,.46f,1f),256*256).ToArray());
                        opaqueBackdrop.Apply(false,false);
                        File.WriteAllBytes(mutatedFile,opaqueBackdrop.EncodeToPNG());
                    }
                    finally{Object.DestroyImmediate(opaqueBackdrop);}
                    AssetDatabase.ImportAsset(mutatedFile,ImportAssetOptions.ForceSynchronousImport);
                }
                else if(drift.EndsWith("hash"))
                {
                    mutatedFile=drift=="raw-hash"?Phase7AssetPaths.RawSourceFolder+"/Wall monitor 3d model.glb":drift=="derived-hash"?"ArtSource/Phase7/Derived/SM_WallDecor_Monitor_01.fbx":AssetDatabase.GetAssetPath(definition.Thumbnail);
                    Phase7Validator.HashOverrideForTests=path=>
                        string.Equals(path,mutatedFile,System.StringComparison.Ordinal)
                            ? "INJECTED-HASH-DRIFT"
                            : null;
                }
                else
                {
                    var path=AssetDatabase.GetAssetPath(definition.Prefab);var root=PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        var visual=root.transform.Find("Visual");var collider=root.GetComponentInChildren<Collider>(true);
                        if(drift=="root-transform")root.transform.localPosition=Vector3.one;
                        else if(drift=="min-y")visual.localPosition+=Vector3.down*.2f;
                        else if(drift=="min-z")visual.localPosition+=Vector3.back*.2f;
                        else if(drift=="depth")visual.localScale=new Vector3(visual.localScale.x,visual.localScale.y,visual.localScale.z*4f);
                        else if(drift=="collider-trigger")collider.isTrigger=false;
                        else if(drift=="collider-layer")collider.gameObject.layer=8;
                        else if(drift=="rigidbody")root.AddComponent<Rigidbody>();
                        else if(drift=="nav")root.AddComponent<NavMeshObstacle>();
                        else if(drift=="material")root.GetComponentInChildren<Renderer>(true).sharedMaterial=null;
                        else if(drift=="mesh-uv")
                        {
                            temporaryMesh="Assets/Art/Phase7/Models/TEMP_NoUv.asset";var source=root.GetComponentInChildren<MeshFilter>(true).sharedMesh;
                            var mesh=new Mesh{name="TEMP_NoUv"};mesh.vertices=source.vertices;mesh.triangles=source.triangles;AssetDatabase.CreateAsset(mesh,temporaryMesh);foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))filter.sharedMesh=mesh;
                        }
                        PrefabUtility.SaveAsPrefabAsset(root,path);
                    }
                    finally{PrefabUtility.UnloadPrefabContents(root);}
                }
                var report=Phase7Validator.ValidateAll();
                Assert.That(report.Issues.Select(issue=>issue.Code),Does.Contain(ExpectedCode(drift)),
                    drift+" did not produce its dedicated validation issue: "+string.Join(",",report.Issues.Select(issue=>issue.Code)));
            }
            finally
            {
                Phase7Validator.HashOverrideForTests=null;
                if(mutatedFile!=null&&originalBytes!=null){File.WriteAllBytes(mutatedFile,originalBytes);AssetDatabase.ImportAsset(mutatedFile,ImportAssetOptions.ForceSynchronousImport);}
                if(temporaryMesh!=null)AssetDatabase.DeleteAsset(temporaryMesh);
                Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();
            }
        }

        private static void SetInt(Object target,string name,int value){var so=new SerializedObject(target);so.FindProperty(name).intValue=value;so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(target);}
        private static void SetFloat(Object target,string name,float value){var so=new SerializedObject(target);so.FindProperty(name).floatValue=value;so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(target);}
        private static void SetObject(Object target,string name,Object value){var so=new SerializedObject(target);so.FindProperty(name).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssetIfDirty(target);}
        private static string ExpectedCode(string drift)=>drift switch
        {
            "footprint"=>"P7-MOUNTED-FOOTPRINT","root-transform"=>"P7-MOUNTED-ROOT",
            "min-y"=>"P7-MOUNTED-BOUNDS","min-z"=>"P7-MOUNTED-BOUNDS","depth"=>"P7-MOUNTED-BOUNDS",
            "max-depth"=>"P7-MOUNTED-DEPTH-CONSISTENCY","collider-trigger"=>"P7-MOUNTED-SELECTION-COLLIDER",
            "collider-layer"=>"P7-MOUNTED-SELECTION-COLLIDER","rigidbody"=>"P7-MOUNTED-RIGIDBODY",
            "nav"=>"P7-MOUNTED-NAV","mesh-uv"=>"P7-MOUNTED-MESH-UV","material"=>"P7-MOUNTED-MATERIAL",
            "alpha"=>"P7-PAINTING-ALPHA","glass"=>"P7-WINDOW-GLASS","thumbnail"=>"P7-MOUNTED-THUMBNAIL",
            "thumbnail-hash"=>"P7-MOUNTED-THUMBNAIL-HASH","thumbnail-backdrop"=>"P7-MOUNTED-THUMBNAIL-BACKDROP","raw-hash"=>"P7-PROVENANCE-RAW",
            "derived-hash"=>"P7-PROVENANCE-DERIVED",_=>throw new System.ArgumentOutOfRangeException(nameof(drift))
        };
    }
}

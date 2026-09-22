using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.Phase8;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class Phase8AssetBuilderTests
    {
        private const string SourceFontPath = "Assets/UI/Phase5/Fonts/NotoSansSC-Regular.otf";

        [TestCase("builder")]
        [TestCase("scene")]
        [TestCase("p8r")]
        public void DirtyAssetGuard_AllowsGeneratedFontAtlasWithoutSavingOrClearingIt(string entry)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            Assert.That(font, Is.Not.Null);
            Assert.That(font.dynamic, Is.True);
            var atlas = font.material.mainTexture;
            Assert.That(atlas, Is.TypeOf<Texture2D>());
            Assert.That(AssetDatabase.IsSubAsset(atlas), Is.True);
            Assert.That(atlas.hideFlags & HideFlags.NotEditable, Is.Not.EqualTo(HideFlags.None));
            var savedAsset = File.ReadAllBytes(SourceFontPath);
            var savedMeta = File.ReadAllBytes(SourceFontPath + ".meta");
            var wasDirty = EditorUtility.IsDirty(atlas);
            try
            {
                EditorUtility.SetDirty(atlas);
                Assert.DoesNotThrow(() => InvokeDirtyAssetGuard(entry));
                Assert.That(EditorUtility.IsDirty(atlas), Is.True,
                    "检查只能识别动态字形缓存，不能自动保存或清除它。");
                Assert.That(File.ReadAllBytes(SourceFontPath), Is.EqualTo(savedAsset));
                Assert.That(File.ReadAllBytes(SourceFontPath + ".meta"), Is.EqualTo(savedMeta));
            }
            finally
            {
                if (!wasDirty) EditorUtility.ClearDirty(atlas);
            }
        }

        [Test]
        public void DirtyAssetGuard_StillRejectsRealFontMaterialImporterAndTmpAtlasEdits(
            [Values("builder", "scene", "p8r")] string entry,
            [Values("font", "material", "importer", "tmp-atlas")] string editedObject)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            UnityEngine.Object target;
            switch (editedObject)
            {
                case "font": target = font; break;
                case "material": target = font.material; break;
                case "importer": target = AssetImporter.GetAtPath(SourceFontPath); break;
                default:
                    target = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(Phase8AssetPaths.UiFontPath)
                        .atlasTextures[0];
                    break;
            }
            Assert.That(target, Is.Not.Null);
            var path = AssetDatabase.GetAssetPath(target);
            var savedAsset = File.ReadAllBytes(path);
            var savedMeta = File.ReadAllBytes(path + ".meta");
            var wasDirty = EditorUtility.IsDirty(target);
            try
            {
                EditorUtility.SetDirty(target);
                var failure = Assert.Throws<System.InvalidOperationException>(() => InvokeDirtyAssetGuard(entry));
                Assert.That(failure.Message, Does.Contain("dirty").IgnoreCase);
                Assert.That(EditorUtility.IsDirty(target), Is.True,
                    "拒绝时保留真实未保存编辑，不能自动保存或清除 dirty 状态。");
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(savedAsset));
                Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(savedMeta));
            }
            finally
            {
                // Restore only the flag this test introduced; preserve pre-existing edits.
                if (!wasDirty) EditorUtility.ClearDirty(target);
            }
        }

        private static void InvokeDirtyAssetGuard(string entry)
        {
            var type = entry == "builder" ? typeof(Phase8AssetBuilder)
                : entry == "scene" ? typeof(Phase8SceneSetup)
                : typeof(AnimalCafe.EditorTools.P8R.P8RFurnitureUiBuilder);
            var name = entry == "builder" ? "RequireNoDirtyProjectAssets"
                : entry == "scene" ? "RequireConfigurationPreconditions" : "RequireCleanLoadedAssets";
            var method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            try
            {
                method.Invoke(null, entry == "scene"
                    ? new object[] { Phase8AssetPaths.MainCafeScenePath, false } : null);
            }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                throw exception.InnerException;
            }
        }

        [Test]
        public void ReviewFix_BuildAssetsRejectsUnrelatedDirtyMaterialWithoutSavingIt()
        {
            var folder = "Assets/Phase8ReviewDirty_" + System.Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var path = folder + "/Unrelated.mat";
            try
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                Assert.That(shader, Is.Not.Null);
                var material = new Material(shader) { name = "Unrelated", color = Color.white };
                AssetDatabase.CreateAsset(material, path);
                AssetDatabase.SaveAssetIfDirty(material);
                var savedAsset = File.ReadAllBytes(path);
                var savedMeta = File.ReadAllBytes(path + ".meta");
                material.color = Color.magenta;
                EditorUtility.SetDirty(material);

                var failure = Assert.Throws<System.InvalidOperationException>(() =>
                    Phase8AssetBuilder.BuildAssets());

                Assert.That(failure.Message, Does.Contain("dirty").IgnoreCase);
                Assert.That(failure.Message, Does.Contain(path));
                Assert.That(material, Is.Not.Null);
                Assert.That(EditorUtility.IsDirty(material), Is.True,
                    "拒绝构建不能替用户保存或撤销无关的未保存编辑。");
                Assert.That(material.color, Is.EqualTo(Color.magenta));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(savedAsset));
                Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(savedMeta));
            }
            finally
            {
                // This GUID folder belongs only to this test, even on the expected RED path.
                // 只清理本测试创建的临时资源，绝不使用全局 SaveAssets。
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase("builder", nameof(Phase8AssetBuilder.BuildAssets),
            "Tools/AnimalCafe/Phase 8/Build Assets")]
        [TestCase("setup", nameof(Phase8SceneSetup.ConfigureValidationScene),
            "Tools/AnimalCafe/Phase 8/Configure Validation Scene")]
        [TestCase("setup", nameof(Phase8SceneSetup.ConfigureMainCafe),
            "Tools/AnimalCafe/Phase 8/Configure MainCafe")]
        public void PublicToolsMenu_RegistersExactRequiredPath(
            string owner,
            string methodName,
            string expectedMenuPath)
        {
            var type = owner == "builder"
                ? typeof(Phase8AssetBuilder)
                : typeof(Phase8SceneSetup);
            var method = type.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var attribute = method.GetCustomAttributes(typeof(MenuItem), false)
                .Cast<MenuItem>()
                .Single();

            Assert.That(attribute.menuItem, Is.EqualTo(expectedMenuPath));
        }

        [Test]
        public void BuildAssets_Twice_PreservesOneMixedCatalogueAndPhase4ProductionDefinitions()
        {
            Phase8AssetBuilder.BuildAssets();
            var firstGuid = AssetDatabase.AssetPathToGUID(
                Phase8AssetPaths.FurnitureCataloguePath);
            var firstCatalogue = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                Phase8AssetPaths.FurnitureCataloguePath);
            var firstReferences = firstCatalogue.Entries
                .Select(entry => GlobalObjectId.GetGlobalObjectIdSlow(entry.Definition).ToString())
                .ToArray();

            Phase8AssetBuilder.BuildAssets();

            var catalogue = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                Phase8AssetPaths.FurnitureCataloguePath);
            Assert.That(catalogue, Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(Phase8AssetPaths.FurnitureCataloguePath),
                Is.EqualTo(firstGuid));
            Assert.That(catalogue.Entries.Select(entry =>
                    GlobalObjectId.GetGlobalObjectIdSlow(entry.Definition).ToString()),
                Is.EqualTo(firstReferences));

            var rows = DecorationCatalogueModelBuilder.BuildFurnitureTab(catalogue);
            Assert.That(rows.Select(row => row.CategoryId), Is.EqualTo(new[]
            {
                "furniture", "cash-register", "coffee-machine"
            }));
            Assert.That(rows[0].Items, Has.Count.EqualTo(4));
            Assert.That(rows[1].Items.Select(item => item.ItemId),
                Is.EqualTo(new[] { "equipment.cash-register.01" }));
            Assert.That(rows[2].Items.Select(item => item.ItemId),
                Is.EqualTo(new[] { "equipment.coffee-machine.01" }));

            AssertDefinitionPath(catalogue, "equipment.cash-register.01",
                Phase8AssetPaths.CashRegisterDefinitionPath);
            AssertDefinitionPath(catalogue, "equipment.coffee-machine.01",
                Phase8AssetPaths.CoffeeMachineDefinitionPath);
            Assert.That(catalogue.Entries.All(entry => entry.Thumbnail != null), Is.True);
            Assert.That(catalogue.Entries
                .Where(entry => entry.Definition.FunctionType != FurnitureFunctionType.None)
                .All(entry => AssetDatabase.GetAssetPath(entry.Thumbnail)
                    .StartsWith(Phase8AssetPaths.ThumbnailFolder)), Is.True);
        }

        [Test]
        public void BuildAssets_UsesExistingPhase8UiPrefabsAndDoesNotCreateReplacementModels()
        {
            Phase8AssetBuilder.BuildAssets();

            foreach (var path in new[]
            {
                Phase8AssetPaths.CataloguePrefabPath,
                Phase8AssetPaths.ActionBarPrefabPath,
                Phase8AssetPaths.FunctionalSurfacePreviewPrefabPath,
                Phase8AssetPaths.PickUpPointIndicatorPrefabPath
            })
            {
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null, path);
            }

            foreach (var path in new[]
            {
                Phase8AssetPaths.CashRegisterDefinitionPath,
                Phase8AssetPaths.CoffeeMachineDefinitionPath
            })
            {
                var definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(path);
                Assert.That(definition, Is.Not.Null, path);
                Assert.That(definition.Prefab, Is.Not.Null, path);
                Assert.That(AssetDatabase.GetAssetPath(definition.Prefab),
                    Does.StartWith("Assets/Art/Phase4/Prefabs/"), path);
            }

            var cataloguePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase8AssetPaths.CataloguePrefabPath);
            var catalogueView = cataloguePrefab.GetComponent<DecorationCatalogueView>();
            var tabs = cataloguePrefab.GetComponentsInChildren<DecorationModeTabsView>(true);
            var ranges = cataloguePrefab.GetComponentsInChildren<DecorationFloorRangeView>(true);
            Assert.That(tabs, Has.Length.EqualTo(1));
            Assert.That(ranges, Has.Length.EqualTo(1));
            Assert.That(ranges[0].transform.parent, Is.SameAs(catalogueView.SurfaceFooterHost));
            Assert.That(ranges[0].GetComponentsInChildren<Button>(true).Select(button => button.name),
                Is.EquivalentTo(new[] { "WholeRoomButton", "SingleGridButton" }));
        }

        [Test]
        public void BuildAssets_RenamedExistingFloorRange_RestoresCanonicalName()
        {
            Phase8AssetBuilder.BuildAssets();
            RenameFloorRange("FloorRange_Renamed");
            try
            {
                Phase8AssetBuilder.BuildAssets();

                var root = AssetDatabase.LoadAssetAtPath<GameObject>(
                    Phase8AssetPaths.CataloguePrefabPath);
                var range = root.GetComponentsInChildren<DecorationFloorRangeView>(true)
                    .Single();
                Assert.That(range.name, Is.EqualTo("FloorRange"));
            }
            finally
            {
                RenameFloorRange("FloorRange");
            }
        }

        [Test]
        public void UpdatePickUpIndicatorAssets_Twice_PreservesReferencesAndOnlyUpdatesPickUpAssets()
        {
            var method = typeof(Phase8AssetBuilder).GetMethod(
                "UpdatePickUpIndicatorAssets", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Pick-up needs its own bounded authoring entry.");
            var path = Phase8AssetPaths.PickUpPointIndicatorPrefabPath;
            var originalGuid = AssetDatabase.AssetPathToGUID(path);
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var footprint = original.transform.Find("Footprint");
            var footprintMesh = footprint.GetComponent<MeshFilter>().sharedMesh;
            var footprintMaterial = footprint.GetComponent<Renderer>().sharedMaterial;
            var footprintPosition = footprint.localPosition;
            var footprintScale = footprint.localScale;
            var meshId = GlobalObjectId.GetGlobalObjectIdSlow(
                original.transform.Find("InvertedSquarePyramid")
                    .GetComponent<MeshFilter>().sharedMesh).ToString();
            var protectedPaths = new[]
            {
                Phase8AssetPaths.FunctionalSurfacePreviewPrefabPath,
                Phase8AssetPaths.CataloguePrefabPath,
                AssetDatabase.GetAssetPath(footprintMaterial)
            };
            var protectedContents = protectedPaths.Select(File.ReadAllBytes).ToArray();

            method.Invoke(null, null);
            var materialPath = "Assets/UI/Phase8/Materials/M_PickUpPoint_Indicator.mat";
            var materialGuid = AssetDatabase.AssetPathToGUID(materialPath);
            Assert.That(materialGuid, Is.Not.Empty);
            method.Invoke(null, null);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var rebuilt = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var rebuiltPyramid = rebuilt.transform.Find("InvertedSquarePyramid");
            var rebuiltMesh = rebuiltPyramid.GetComponent<MeshFilter>().sharedMesh;
            var rebuiltMaterial = rebuiltPyramid.GetComponent<Renderer>().sharedMaterial;
            var rebuiltFootprint = rebuilt.transform.Find("Footprint");
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(originalGuid));
            Assert.That(GlobalObjectId.GetGlobalObjectIdSlow(rebuiltMesh).ToString(),
                Is.EqualTo(meshId), "Existing mesh subasset references must survive authoring.");
            Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Count(),
                Is.EqualTo(1), "Repeated authoring must not accumulate mesh subassets.");
            Assert.That(rebuiltMesh.bounds.size.x, Is.InRange(.4f, .65f));
            Assert.That(rebuiltMesh.bounds.size.y, Is.EqualTo(.56f).Within(.001f));
            Assert.That(rebuiltMesh.vertexCount, Is.EqualTo(4));
            Assert.That(rebuiltFootprint.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(footprintMesh));
            Assert.That(rebuiltFootprint.GetComponent<Renderer>().sharedMaterial,
                Is.SameAs(footprintMaterial));
            Assert.That(rebuiltFootprint.localPosition, Is.EqualTo(footprintPosition));
            Assert.That(rebuiltFootprint.localScale, Is.EqualTo(footprintScale));
            Assert.That(AssetDatabase.GetAssetPath(rebuiltMaterial), Is.EqualTo(materialPath));
            Assert.That(AssetDatabase.AssetPathToGUID(materialPath), Is.EqualTo(materialGuid));
            Assert.That(rebuiltMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));
            Assert.That(rebuiltMaterial.GetFloat("_Surface"), Is.EqualTo(1));
            Assert.That(AssetDatabase.GetAssetPath(rebuiltMaterial.GetTexture("_BaseMap")),
                Is.EqualTo("Assets/UI/P8R/WorldMarkers/pickup_point.png"));
            for (var index = 0; index < protectedPaths.Length; index++)
            {
                Assert.That(File.ReadAllBytes(protectedPaths[index]),
                    Is.EqualTo(protectedContents[index]), protectedPaths[index]);
            }
        }

        private static void RenameFloorRange(string name)
        {
            var root = PrefabUtility.LoadPrefabContents(
                Phase8AssetPaths.CataloguePrefabPath);
            try
            {
                root.GetComponentsInChildren<DecorationFloorRangeView>(true)
                    .Single().name = name;
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    Phase8AssetPaths.CataloguePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AssertDefinitionPath(
            DecorationCatalogueAsset catalogue,
            string definitionId,
            string expectedPath)
        {
            var entry = catalogue.Entries.Single(candidate =>
                candidate.Definition.DefinitionId == definitionId);
            Assert.That(AssetDatabase.GetAssetPath(entry.Definition), Is.EqualTo(expectedPath));
        }
    }
}

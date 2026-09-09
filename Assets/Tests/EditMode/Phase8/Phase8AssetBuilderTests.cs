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
            Assert.That(rebuiltMesh.bounds.size.x, Is.EqualTo(.28f).Within(.001f));
            Assert.That(rebuiltMesh.bounds.size.y, Is.EqualTo(.5f).Within(.001f));
            Assert.That(rebuiltFootprint.GetComponent<MeshFilter>().sharedMesh,
                Is.SameAs(footprintMesh));
            Assert.That(rebuiltFootprint.GetComponent<Renderer>().sharedMaterial,
                Is.SameAs(footprintMaterial));
            Assert.That(rebuiltFootprint.localPosition, Is.EqualTo(footprintPosition));
            Assert.That(rebuiltFootprint.localScale, Is.EqualTo(footprintScale));
            Assert.That(AssetDatabase.GetAssetPath(rebuiltMaterial), Is.EqualTo(materialPath));
            Assert.That(AssetDatabase.AssetPathToGUID(materialPath), Is.EqualTo(materialGuid));
            Assert.That(rebuiltMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(rebuiltMaterial.GetFloat("_Surface"), Is.Zero);
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

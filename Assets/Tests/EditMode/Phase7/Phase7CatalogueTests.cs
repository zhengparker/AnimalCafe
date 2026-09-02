using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.Phase6;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.Phase7
{
    public sealed class Phase7CatalogueTests
    {
        [Test]
        public void Build_ProducesSevenStableCategoriesWithOnlyTheirTypedItems()
        {
            using var fixture = CreateValidFixture();

            var categories = fixture.BuildModels();

            Assert.That(categories.Select(category => category.CategoryId), Is.EqualTo(new[]
            {
                "furniture", "floor", "wallpaper", "paint", "wainscoting", "wall-decor", "windows"
            }));
            Assert.That(categories.Select(category => category.DisplayName), Is.EqualTo(new[]
            {
                "Furniture", "Floor", "Wallpaper", "Paint", "Wainscoting", "Wall Decor", "Windows"
            }));
            Assert.That(categories[0].Items.All(item =>
                item.Kind == DecorationCatalogueItemKind.Furniture), Is.True);
            Assert.That(categories[1].Items.All(item =>
                item.Kind == DecorationCatalogueItemKind.Floor), Is.True);
            Assert.That(categories.Skip(2).Take(3).SelectMany(category => category.Items)
                .All(item => item.Kind == DecorationCatalogueItemKind.WallSurface), Is.True);
            Assert.That(categories.Skip(5).SelectMany(category => category.Items)
                .All(item => item.Kind == DecorationCatalogueItemKind.WallMounted), Is.True);
        }

        [Test]
        public void Build_KeepsFourPhase6FurnitureItemsWithoutFootprintPresentation()
        {
            using var fixture = CreateValidFixture();
            fixture.Furniture = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                Phase6DecorationAssetPaths.DecorationCataloguePath);

            var furniture = fixture.BuildModels().Single(category => category.CategoryId == "furniture");

            Assert.That(furniture.Items, Has.Count.EqualTo(4));
            Assert.That(furniture.Items.All(item => item.Thumbnail != null), Is.True);
            Assert.That(furniture.Items.All(item => !string.IsNullOrWhiteSpace(item.DisplayName)), Is.True);
            Assert.That(typeof(DecorationCatalogueItemModel).GetProperties()
                .Any(property => property.Name.IndexOf("footprint", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False);
        }

        [Test]
        public void SurfaceValidation_AllowsNoneOnlyForWainscotingAndRequiresNormalVisuals()
        {
            using var fixture = CreateValidFixture();
            var none = fixture.Wainscoting.Entries.Single(style => style.IsNoneOption);

            Assert.DoesNotThrow(() => fixture.BuildModels());
            Assert.That(none.Material, Is.Null);
            Assert.That(none.Thumbnail, Is.Not.Null);

            SetSurface(none, material: fixture.Material);
            AssertValidationIssue(() => fixture.BuildModels(),
                DecorationCatalogueValidationIssueCode.NoneHasMaterial,
                "wainscoting", "wainscoting.none");

            SetSurface(none, clearMaterial: true);
            SetSurface(fixture.Paint.Entries[0], clearThumbnail: true);
            AssertValidationIssue(() => fixture.BuildModels(),
                DecorationCatalogueValidationIssueCode.MissingThumbnail,
                "paint", "paint.cream");

            SetSurface(fixture.Paint.Entries[0], thumbnail: fixture.CreateSprite("S_PaintRestored"));
            SetSurface(fixture.Paint.Entries[0], isNoneOption: true);
            AssertValidationIssue(() => fixture.BuildModels(),
                DecorationCatalogueValidationIssueCode.NoneWrongKind,
                "paint", "paint.cream");
        }

        [Test]
        public void Build_RejectsDuplicateStyleIdAndWrongSurfaceRowBeforePublishingModels()
        {
            using var fixture = CreateValidFixture();
            var duplicate = fixture.CreateSurface(
                "paint.cream", "Duplicate", SurfaceStyleKind.Paint, fixture.Material);
            AddSurface(fixture.Paint, duplicate);

            AssertValidationIssue(() => fixture.BuildModels(),
                DecorationCatalogueValidationIssueCode.DuplicateItemId,
                "paint", "paint.cream");

            RemoveLastSurface(fixture.Paint);
            SetCatalogueKind(fixture.Paint, SurfaceStyleKind.Wallpaper);
            AssertValidationIssue(() => fixture.BuildModels(),
                DecorationCatalogueValidationIssueCode.WrongCategoryKind,
                "paint", null);
        }

        [Test]
        public void WallMountedValidation_RequiresPrefabThumbnailIntegerFootprintAndShallowDepth()
        {
            using var fixture = CreateValidFixture();
            var wallDecor = fixture.WallDecor.Entries[0];

            Assert.DoesNotThrow(() => fixture.BuildModels());
            SetWallMounted(wallDecor, clearPrefab: true);
            AssertValidationIssue(() => fixture.BuildModels(),
                DecorationCatalogueValidationIssueCode.MissingPrefab,
                "wall-decor", "wall.decor.picture.01");

            SetWallMounted(
                wallDecor,
                prefab: fixture.CreatePrefab("PF_Restored"),
                clearThumbnail: true);
            AssertValidationIssue(() => fixture.BuildModels(),
                DecorationCatalogueValidationIssueCode.MissingThumbnail,
                "wall-decor", "wall.decor.picture.01");

            SetWallMounted(wallDecor, thumbnail: fixture.CreateSprite("S_Restored"), footprintWidth: 0);
            AssertValidationIssue(() => fixture.BuildModels(),
                DecorationCatalogueValidationIssueCode.InvalidFootprint,
                "wall-decor", "wall.decor.picture.01");

            SetWallMounted(wallDecor, footprintWidth: 1, maxVisualDepth: 0.351f);
            AssertValidationIssue(() => fixture.BuildModels(),
                DecorationCatalogueValidationIssueCode.InvalidVisualDepth,
                "wall-decor", "wall.decor.picture.01");

            SetWallMounted(wallDecor, maxVisualDepth: 0.35f);
            Assert.DoesNotThrow(() => fixture.BuildModels());
        }

        [Test]
        public void Build_UsesExactInitialContentCountsAndAvailableSpriteItemsWithoutRuntimeThumbnailObjects()
        {
            using var fixture = CreateValidFixture();

            var categories = fixture.BuildModels();

            Assert.That(categories.Single(category => category.CategoryId == "paint").Items, Has.Count.EqualTo(3));
            Assert.That(categories.Single(category => category.CategoryId == "wallpaper").Items, Has.Count.EqualTo(2));
            Assert.That(categories.Single(category => category.CategoryId == "wainscoting").Items,
                Has.Count.EqualTo(3));
            Assert.That(categories.Single(category => category.CategoryId == "floor").Items, Has.Count.EqualTo(3));
            Assert.That(categories.Single(category => category.CategoryId == "wall-decor").Items, Has.Count.EqualTo(3));
            Assert.That(categories.Single(category => category.CategoryId == "windows").Items, Has.Count.EqualTo(1));
            Assert.That(categories.SelectMany(category => category.Items)
                .All(item => item.Availability == DecorationCatalogueItemAvailability.Available
                    && item.Thumbnail is Sprite), Is.True);
            Assert.That(typeof(DecorationCatalogueItemModel).GetProperties()
                .Select(property => property.PropertyType), Has.None.EqualTo(typeof(UnityEngine.Camera))
                .And.None.EqualTo(typeof(RenderTexture)));
        }

        [Test]
        public void ContentAndPresentationContracts_DoNotAddEconomyOrPersistenceFields()
        {
            var types = new[]
            {
                typeof(SurfaceStyleDefinitionAsset),
                typeof(SurfaceStyleCatalogueAsset),
                typeof(WallMountedDefinitionAsset),
                typeof(WallMountedCatalogueAsset),
                typeof(DecorationCatalogueItemModel),
                typeof(DecorationCategoryModel)
            };

            var names = types.SelectMany(type => type.GetFields(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => field.Name))
                .Concat(types.SelectMany(type => type.GetProperties()
                    .Select(property => property.Name)))
                .ToArray();

            Assert.That(names, Has.None.Matches<string>(name =>
                name.IndexOf("price", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("count", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("quantity", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("unlock", StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("save", StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [Test]
        public void PublishedCatalogueCollections_AreDefensiveReadOnlySnapshots()
        {
            using var fixture = CreateValidFixture();
            var surfaceEntries = fixture.Paint.Entries;
            var wallMountedEntries = fixture.WallDecor.Entries;
            var models = fixture.BuildModels();
            var paintItems = models.Single(category => category.CategoryId == "paint").Items;
            var callerItems = new List<DecorationCatalogueItemModel>
            {
                new DecorationCatalogueItemModel(
                    "fixture.item", "Fixture", fixture.CreateSprite("S_Model"),
                    DecorationCatalogueItemKind.Floor, false)
            };
            var category = new DecorationCategoryModel("fixture", "Fixture", callerItems);

            AddSurface(fixture.Paint, fixture.CreateSurface(
                "paint.extra", "Extra", SurfaceStyleKind.Paint, fixture.Material));
            AddWallMounted(fixture.WallDecor, CreateWallMounted(fixture, "wall.decor.extra", "Extra"));
            callerItems.Clear();

            Assert.That(surfaceEntries, Has.Count.EqualTo(3));
            Assert.That(wallMountedEntries, Has.Count.EqualTo(3));
            Assert.That(paintItems, Has.Count.EqualTo(3));
            Assert.That(category.Items, Has.Count.EqualTo(1));
            Assert.That(surfaceEntries, Is.Not.TypeOf<List<SurfaceStyleDefinitionAsset>>());
            Assert.That(wallMountedEntries, Is.Not.TypeOf<List<WallMountedDefinitionAsset>>());
            Assert.That(models, Is.Not.TypeOf<List<DecorationCategoryModel>>());
            Assert.That(paintItems, Is.Not.TypeOf<List<DecorationCatalogueItemModel>>());
            Assert.Throws<NotSupportedException>(() =>
                ((IList<SurfaceStyleDefinitionAsset>)surfaceEntries).Add(null));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<DecorationCatalogueItemModel>)category.Items).Add(null));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<DecorationCategoryModel>)models).Add(null));
        }

        [Test]
        public void Build_RejectsNullSourceEntryWithStructuredFirstIssue()
        {
            using var fixture = CreateValidFixture();
            SetSurfaceEntry(fixture.Paint, 0, null);

            AssertValidationIssue(
                () => fixture.BuildModels(),
                DecorationCatalogueValidationIssueCode.NullEntry,
                "paint",
                null);
        }

        [Test]
        public void CategoryModel_RejectsNullItemBeforePublication()
        {
            using var fixture = CreateValidFixture();

            Assert.That(() => new DecorationCategoryModel(
                    "fixture", "Fixture", new DecorationCatalogueItemModel[] { null }),
                Throws.ArgumentException);
        }

        [Test]
        public void Build_ReportsDeterministicStructuredIssuesForSurfaceContracts()
        {
            using (var duplicate = CreateValidFixture())
            {
                SetSurfaceId(duplicate.Wallpaper.Entries[0], "paint.cream");
                AssertValidationIssue(() => duplicate.BuildModels(),
                    DecorationCatalogueValidationIssueCode.DuplicateItemId,
                    "paint", "paint.cream");
            }

            using (var wrongKind = CreateValidFixture())
            {
                SetSurfaceKind(wrongKind.Paint.Entries[0], SurfaceStyleKind.Wallpaper);
                AssertValidationIssue(() => wrongKind.BuildModels(),
                    DecorationCatalogueValidationIssueCode.WrongCategoryKind,
                    "paint", "paint.cream");
            }

            using (var invalidId = CreateValidFixture())
            {
                SetSurfaceId(invalidId.Paint.Entries[0], "Paint Cream");
                AssertValidationIssue(() => invalidId.BuildModels(),
                    DecorationCatalogueValidationIssueCode.InvalidStableId,
                    "paint", "Paint Cream");
            }

            using (var missingMaterial = CreateValidFixture())
            {
                SetSurface(missingMaterial.Paint.Entries[0], clearMaterial: true);
                AssertValidationIssue(() => missingMaterial.BuildModels(),
                    DecorationCatalogueValidationIssueCode.MissingMaterial,
                    "paint", "paint.cream");
            }

            using (var missingThumbnail = CreateValidFixture())
            {
                SetSurface(missingThumbnail.Paint.Entries[0], clearThumbnail: true);
                AssertValidationIssue(() => missingThumbnail.BuildModels(),
                    DecorationCatalogueValidationIssueCode.MissingThumbnail,
                    "paint", "paint.cream");
            }
        }

        [Test]
        public void Build_ReportsDeterministicStructuredIssuesForNoneContracts()
        {
            using (var wrongKind = CreateValidFixture())
            {
                SetSurfaceKind(wrongKind.Wainscoting.Entries[2], SurfaceStyleKind.Paint);
                AssertValidationIssue(() => wrongKind.BuildModels(),
                    DecorationCatalogueValidationIssueCode.NoneWrongKind,
                    "wainscoting", "wainscoting.none");
            }

            using (var hasMaterial = CreateValidFixture())
            {
                SetSurface(hasMaterial.Wainscoting.Entries[2], material: hasMaterial.Material);
                AssertValidationIssue(() => hasMaterial.BuildModels(),
                    DecorationCatalogueValidationIssueCode.NoneHasMaterial,
                    "wainscoting", "wainscoting.none");
            }

            using (var missingIcon = CreateValidFixture())
            {
                SetSurface(missingIcon.Wainscoting.Entries[2], clearThumbnail: true);
                AssertValidationIssue(() => missingIcon.BuildModels(),
                    DecorationCatalogueValidationIssueCode.NoneMissingIcon,
                    "wainscoting", "wainscoting.none");
            }
        }

        [Test]
        public void Build_ReportsDeterministicStructuredIssuesForWallMountedContracts()
        {
            using (var missingPrefab = CreateValidFixture())
            {
                SetWallMounted(missingPrefab.WallDecor.Entries[0], clearPrefab: true);
                AssertValidationIssue(() => missingPrefab.BuildModels(),
                    DecorationCatalogueValidationIssueCode.MissingPrefab,
                    "wall-decor", "wall.decor.picture.01");
            }

            using (var missingSprite = CreateValidFixture())
            {
                SetWallMounted(missingSprite.WallDecor.Entries[0], clearThumbnail: true);
                AssertValidationIssue(() => missingSprite.BuildModels(),
                    DecorationCatalogueValidationIssueCode.MissingThumbnail,
                    "wall-decor", "wall.decor.picture.01");
            }

            foreach (var footprint in new[] { 0, -1 })
            {
                using var invalidFootprint = CreateValidFixture();
                SetWallMounted(invalidFootprint.WallDecor.Entries[0], footprintWidth: footprint);
                AssertValidationIssue(() => invalidFootprint.BuildModels(),
                    DecorationCatalogueValidationIssueCode.InvalidFootprint,
                    "wall-decor", "wall.decor.picture.01");
            }

            foreach (var depth in new[] { -0.01f, float.NaN, float.PositiveInfinity, 0.351f })
            {
                using var invalidDepth = CreateValidFixture();
                SetWallMounted(invalidDepth.WallDecor.Entries[0], maxVisualDepth: depth);
                AssertValidationIssue(() => invalidDepth.BuildModels(),
                    DecorationCatalogueValidationIssueCode.InvalidVisualDepth,
                    "wall-decor", "wall.decor.picture.01");
            }
        }

        private static void AssertValidationIssue(
            TestDelegate action,
            DecorationCatalogueValidationIssueCode code,
            string categoryId,
            string itemId)
        {
            var exception = Assert.Throws<DecorationCatalogueValidationException>(action);
            Assert.That(exception.Code, Is.EqualTo(code));
            Assert.That(exception.CategoryId, Is.EqualTo(categoryId));
            Assert.That(exception.ItemId, Is.EqualTo(itemId));
        }

        private static void SetSurface(
            SurfaceStyleDefinitionAsset definition,
            Material material = null,
            Sprite thumbnail = null,
            bool? isNoneOption = null,
            bool clearMaterial = false,
            bool clearThumbnail = false)
        {
            var serialized = new SerializedObject(definition);
            if (material != null || clearMaterial)
            {
                serialized.FindProperty("material").objectReferenceValue = material;
            }

            if (thumbnail != null || clearThumbnail)
            {
                serialized.FindProperty("thumbnail").objectReferenceValue = thumbnail;
            }

            if (isNoneOption.HasValue)
            {
                serialized.FindProperty("isNoneOption").boolValue = isNoneOption.Value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetCatalogueKind(SurfaceStyleCatalogueAsset catalogue, SurfaceStyleKind kind)
        {
            var serialized = new SerializedObject(catalogue);
            serialized.FindProperty("kind").intValue = (int)kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSurface(
            SurfaceStyleCatalogueAsset catalogue,
            SurfaceStyleDefinitionAsset definition)
        {
            var serialized = new SerializedObject(catalogue);
            var entries = serialized.FindProperty("entries");
            entries.InsertArrayElementAtIndex(entries.arraySize);
            entries.GetArrayElementAtIndex(entries.arraySize - 1).objectReferenceValue = definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSurfaceEntry(
            SurfaceStyleCatalogueAsset catalogue,
            int index,
            SurfaceStyleDefinitionAsset definition)
        {
            var serialized = new SerializedObject(catalogue);
            serialized.FindProperty("entries").GetArrayElementAtIndex(index)
                .objectReferenceValue = definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSurfaceId(SurfaceStyleDefinitionAsset definition, string styleId)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("styleId").stringValue = styleId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSurfaceKind(SurfaceStyleDefinitionAsset definition, SurfaceStyleKind kind)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("kind").intValue = (int)kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddWallMounted(
            WallMountedCatalogueAsset catalogue,
            WallMountedDefinitionAsset definition)
        {
            var serialized = new SerializedObject(catalogue);
            var entries = serialized.FindProperty("entries");
            entries.InsertArrayElementAtIndex(entries.arraySize);
            entries.GetArrayElementAtIndex(entries.arraySize - 1).objectReferenceValue = definition;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveLastSurface(SurfaceStyleCatalogueAsset catalogue)
        {
            var serialized = new SerializedObject(catalogue);
            var entries = serialized.FindProperty("entries");
            entries.DeleteArrayElementAtIndex(entries.arraySize - 1);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetWallMounted(
            WallMountedDefinitionAsset definition,
            GameObject prefab = null,
            Sprite thumbnail = null,
            int? footprintWidth = null,
            float? maxVisualDepth = null,
            bool clearPrefab = false,
            bool clearThumbnail = false)
        {
            var serialized = new SerializedObject(definition);
            if (prefab != null || clearPrefab)
            {
                serialized.FindProperty("prefab").objectReferenceValue = prefab;
            }

            if (thumbnail != null || clearThumbnail)
            {
                serialized.FindProperty("thumbnail").objectReferenceValue = thumbnail;
            }

            if (footprintWidth.HasValue)
            {
                serialized.FindProperty("footprintWidth").intValue = footprintWidth.Value;
            }

            if (maxVisualDepth.HasValue)
            {
                serialized.FindProperty("maxVisualDepth").floatValue = maxVisualDepth.Value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class CatalogueFixture : IDisposable
        {
            private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

            public DecorationCatalogueAsset Furniture { get; set; }
            public SurfaceStyleCatalogueAsset Floor { get; set; }
            public SurfaceStyleCatalogueAsset Wallpaper { get; set; }
            public SurfaceStyleCatalogueAsset Paint { get; set; }
            public SurfaceStyleCatalogueAsset Wainscoting { get; set; }
            public WallMountedCatalogueAsset WallDecor { get; set; }
            public WallMountedCatalogueAsset Windows { get; set; }
            public Material Material { get; private set; }

            public IReadOnlyList<DecorationCategoryModel> BuildModels()
            {
                return DecorationCatalogueModelBuilder.Build(
                    Furniture, Floor, Wallpaper, Paint, Wainscoting, WallDecor, Windows);
            }

            public SurfaceStyleDefinitionAsset CreateSurface(
                string styleId,
                string displayName,
                SurfaceStyleKind kind,
                Material material,
                bool isNoneOption = false)
            {
                var definition = Track(ScriptableObject.CreateInstance<SurfaceStyleDefinitionAsset>());
                var serialized = new SerializedObject(definition);
                serialized.FindProperty("styleId").stringValue = styleId;
                serialized.FindProperty("displayName").stringValue = displayName;
                serialized.FindProperty("kind").intValue = (int)kind;
                serialized.FindProperty("material").objectReferenceValue = material;
                serialized.FindProperty("thumbnail").objectReferenceValue = CreateSprite("S_" + styleId);
                serialized.FindProperty("isNoneOption").boolValue = isNoneOption;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return definition;
            }

            public Sprite CreateSprite(string name)
            {
                var texture = Track(new Texture2D(2, 2) { name = "T_" + name });
                var sprite = Track(Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f));
                sprite.name = name;
                return sprite;
            }

            public GameObject CreatePrefab(string name)
            {
                return Track(new GameObject(name));
            }

            public void Dispose()
            {
                foreach (var asset in created.AsEnumerable().Reverse())
                {
                    if (asset != null)
                    {
                        UnityEngine.Object.DestroyImmediate(asset);
                    }
                }
            }

            public T Track<T>(T asset) where T : UnityEngine.Object
            {
                created.Add(asset);
                return asset;
            }

            public void SetMaterial(Material material)
            {
                Material = material;
            }
        }

        private static CatalogueFixture CreateValidFixture()
        {
            var fixture = new CatalogueFixture();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            fixture.SetMaterial(fixture.Track(new Material(shader) { name = "M_Phase7Fixture" }));
            fixture.Furniture = fixture.Track(ScriptableObject.CreateInstance<DecorationCatalogueAsset>());
            fixture.Floor = CreateSurfaceCatalogue(fixture, SurfaceStyleKind.Floor, new[]
            {
                fixture.CreateSurface("floor.warm-wood", "Warm Wood", SurfaceStyleKind.Floor, fixture.Material),
                fixture.CreateSurface("floor.cool-stone", "Cool Stone", SurfaceStyleKind.Floor, fixture.Material),
                fixture.CreateSurface("floor.terracotta", "Terracotta", SurfaceStyleKind.Floor, fixture.Material)
            });
            fixture.Wallpaper = CreateSurfaceCatalogue(fixture, SurfaceStyleKind.Wallpaper, new[]
            {
                fixture.CreateSurface("wallpaper.cream-floral", "Cream Floral", SurfaceStyleKind.Wallpaper, fixture.Material),
                fixture.CreateSurface("wallpaper.sage-sprig", "Sage Sprig", SurfaceStyleKind.Wallpaper, fixture.Material)
            });
            fixture.Paint = CreateSurfaceCatalogue(fixture, SurfaceStyleKind.Paint, new[]
            {
                fixture.CreateSurface("paint.cream", "Cream", SurfaceStyleKind.Paint, fixture.Material),
                fixture.CreateSurface("paint.sage", "Sage", SurfaceStyleKind.Paint, fixture.Material),
                fixture.CreateSurface("paint.terracotta", "Terracotta", SurfaceStyleKind.Paint, fixture.Material)
            });
            fixture.Wainscoting = CreateSurfaceCatalogue(fixture, SurfaceStyleKind.Wainscoting, new[]
            {
                fixture.CreateSurface("wainscoting.warm-white-rail", "Warm White + Rail", SurfaceStyleKind.Wainscoting, fixture.Material),
                fixture.CreateSurface("wainscoting.sage-plain", "Sage Plain", SurfaceStyleKind.Wainscoting, fixture.Material),
                fixture.CreateSurface("wainscoting.none", "None", SurfaceStyleKind.Wainscoting, null, true)
            });
            fixture.WallDecor = CreateWallMountedCatalogue(fixture, WallMountedCatalogueKind.WallDecor, new[]
            {
                CreateWallMounted(fixture, "wall.decor.picture.01", "Picture"),
                CreateWallMounted(fixture, "wall.decor.shelf.01", "Shelf"),
                CreateWallMounted(fixture, "wall.decor.clock.01", "Clock")
            });
            fixture.Windows = CreateWallMountedCatalogue(fixture, WallMountedCatalogueKind.Windows, new[]
            {
                CreateWallMounted(fixture, "wall.window.01", "Window")
            });
            return fixture;
        }

        private static SurfaceStyleCatalogueAsset CreateSurfaceCatalogue(
            CatalogueFixture fixture,
            SurfaceStyleKind kind,
            SurfaceStyleDefinitionAsset[] definitions)
        {
            var catalogue = fixture.Track(ScriptableObject.CreateInstance<SurfaceStyleCatalogueAsset>());
            var serialized = new SerializedObject(catalogue);
            serialized.FindProperty("kind").intValue = (int)kind;
            var entries = serialized.FindProperty("entries");
            entries.arraySize = definitions.Length;
            for (var index = 0; index < definitions.Length; index++)
            {
                entries.GetArrayElementAtIndex(index).objectReferenceValue = definitions[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return catalogue;
        }

        private static WallMountedCatalogueAsset CreateWallMountedCatalogue(
            CatalogueFixture fixture,
            WallMountedCatalogueKind kind,
            WallMountedDefinitionAsset[] definitions)
        {
            var catalogue = fixture.Track(ScriptableObject.CreateInstance<WallMountedCatalogueAsset>());
            var serialized = new SerializedObject(catalogue);
            serialized.FindProperty("kind").intValue = (int)kind;
            var entries = serialized.FindProperty("entries");
            entries.arraySize = definitions.Length;
            for (var index = 0; index < definitions.Length; index++)
            {
                entries.GetArrayElementAtIndex(index).objectReferenceValue = definitions[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return catalogue;
        }

        private static WallMountedDefinitionAsset CreateWallMounted(
            CatalogueFixture fixture,
            string definitionId,
            string displayName)
        {
            var definition = fixture.Track(ScriptableObject.CreateInstance<WallMountedDefinitionAsset>());
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("definitionId").stringValue = definitionId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("footprintWidth").intValue = 1;
            serialized.FindProperty("footprintHeight").intValue = 1;
            serialized.FindProperty("prefab").objectReferenceValue = fixture.CreatePrefab("PF_" + definitionId);
            serialized.FindProperty("thumbnail").objectReferenceValue = fixture.CreateSprite("S_" + definitionId);
            serialized.FindProperty("maxVisualDepth").floatValue = 0.35f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }
    }
}

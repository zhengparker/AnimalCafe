using System;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.Phase6;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase6
{
    public sealed class DecorationCatalogueTests
    {
        [Test]
        public void DecorationCatalogue_ContainsOnlyApprovedCounterPresetsInStableOrder()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var catalogue = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                Phase6DecorationAssetPaths.DecorationCataloguePath);

            Assert.That(catalogue, Is.Not.Null);
            Assert.That(catalogue.Entries.Select(entry => entry.Definition.DefinitionId),
                Is.EqualTo(new[]
                {
                    "furniture.counter.module.01",
                    "counter.preset.1x2",
                    "counter.preset.1x3",
                    "counter.preset.2x3"
                }));
        }

        [Test]
        public void DecorationCatalogue_ExcludesWorkTableAndEveryNonFloorDefinition()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var catalogue = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                Phase6DecorationAssetPaths.DecorationCataloguePath);

            Assert.That(catalogue.Entries.Select(entry => entry.Definition.DefinitionId),
                Has.None.EqualTo("furniture.work-table.01"));
            Assert.That(catalogue.Entries.All(entry =>
                    entry.Definition.AllowedPlacementSurfaces == PlacementSurfaceType.Floor),
                Is.True);
            Assert.That(catalogue.Entries.Select(entry => entry.Definition.DefinitionId),
                Has.None.EqualTo("equipment.cash-register.01"));
            Assert.That(catalogue.Entries.Select(entry => entry.Definition.DefinitionId),
                Has.None.EqualTo("equipment.coffee-machine.01"));
            Assert.That(catalogue.Entries.Select(entry => entry.Definition.DefinitionId),
                Has.None.EqualTo("wall.window.01"));
        }

        [Test]
        public void Entries_ReferenceOnlyDefinitionAndThumbnailWithoutDuplicatedGameplayData()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var catalogue = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                Phase6DecorationAssetPaths.DecorationCataloguePath);

            Assert.That(typeof(DecorationCatalogueEntry)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(field => field.Name), Is.EquivalentTo(new[] { "definition", "thumbnail" }));
            Assert.That(catalogue.Entries.All(entry => entry.Thumbnail != null), Is.True);
            Assert.That(catalogue.Entries.Select(entry =>
                    $"{entry.Definition.FootprintWidth} x {entry.Definition.FootprintDepth}"),
                Is.EqualTo(new[] { "1 x 1", "1 x 2", "1 x 3", "2 x 3" }));
            Assert.That(typeof(DecorationCatalogueEntry)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(field => field.Name.IndexOf("inventory", StringComparison.OrdinalIgnoreCase) >= 0),
                Is.False);
        }

        [Test]
        public void Entries_MissingDefinitionIsRejectedWithSpecificIndex()
        {
            var catalogue = CreateCatalogue(null, CreateSprite());
            try
            {
                Assert.That(catalogue.Entries, Has.Count.EqualTo(1));
                Assert.That(() => Phase6DecorationAssetBuilder.ValidateDecorationCatalogue(catalogue),
                    Throws.TypeOf<InvalidOperationException>().With.Message.Contains("index 0")
                        .And.Message.Contains("Definition"));
            }
            finally
            {
                DestroyCatalogueFixture(catalogue);
            }
        }

        [Test]
        public void Entries_DefinitionWithoutPrefabIsRejectedWithSpecificId()
        {
            var definition = CreateDefinition("counter.preset.missing-prefab", null);
            var catalogue = CreateCatalogue(definition, CreateSprite());
            try
            {
                Assert.That(catalogue.Entries, Has.Count.EqualTo(1));
                Assert.That(() => Phase6DecorationAssetBuilder.ValidateDecorationCatalogue(catalogue),
                    Throws.TypeOf<InvalidOperationException>().With.Message.Contains("counter.preset.missing-prefab")
                        .And.Message.Contains("Prefab"));
            }
            finally
            {
                DestroyCatalogueFixture(catalogue);
            }
        }

        [Test]
        public void Entries_MissingThumbnailIsRejectedWithSpecificDefinitionId()
        {
            var prefab = new GameObject("PF_Counter_ThumbnailFixture");
            var definition = CreateDefinition("counter.preset.missing-thumbnail", prefab);
            var catalogue = CreateCatalogue(definition, null);
            try
            {
                Assert.That(catalogue.Entries, Has.Count.EqualTo(1));
                Assert.That(() => Phase6DecorationAssetBuilder.ValidateDecorationCatalogue(catalogue),
                    Throws.TypeOf<InvalidOperationException>().With.Message.Contains("counter.preset.missing-thumbnail")
                        .And.Message.Contains("thumbnail"));
            }
            finally
            {
                DestroyCatalogueFixture(catalogue);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        [Test]
        public void Entries_DuplicateDefinitionIdsAreRejected()
        {
            var firstPrefab = new GameObject("PF_Counter_First");
            var secondPrefab = new GameObject("PF_Counter_Second");
            var first = CreateDefinition("counter.preset.duplicate", firstPrefab);
            var second = CreateDefinition("counter.preset.duplicate", secondPrefab);
            var firstSprite = CreateSprite();
            var secondSprite = CreateSprite();
            var catalogue = CreateCatalogue(
                new[] { first, second },
                new[] { firstSprite, secondSprite });
            try
            {
                Assert.That(catalogue.Entries, Has.Count.EqualTo(2));
                Assert.That(() => Phase6DecorationAssetBuilder.ValidateDecorationCatalogue(catalogue),
                    Throws.TypeOf<ArgumentException>().With.Message.Contains("counter.preset.duplicate")
                        .And.Message.Contains(first.name)
                        .And.Message.Contains(second.name));
            }
            finally
            {
                DestroyCatalogueFixture(catalogue);
                UnityEngine.Object.DestroyImmediate(firstPrefab);
                UnityEngine.Object.DestroyImmediate(secondPrefab);
            }
        }

        [Test]
        public void Entries_RepeatedReadKeepsExactlyOneEntryPerDefinition()
        {
            Phase6DecorationAssetBuilder.BuildAll();
            var catalogue = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                Phase6DecorationAssetPaths.DecorationCataloguePath);

            for (var reopen = 0; reopen < 3; reopen++)
            {
                var ids = catalogue.Entries.Select(entry => entry.Definition.DefinitionId).ToArray();
                Assert.That(ids, Has.Length.EqualTo(4));
                Assert.That(ids.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(4));
            }
        }

        private static DecorationCatalogueAsset CreateCatalogue(
            FurnitureDefinitionAsset definition,
            Sprite thumbnail)
        {
            return CreateCatalogue(new[] { definition }, new[] { thumbnail });
        }

        private static DecorationCatalogueAsset CreateCatalogue(
            FurnitureDefinitionAsset[] definitions,
            Sprite[] thumbnails)
        {
            var catalogue = ScriptableObject.CreateInstance<DecorationCatalogueAsset>();
            var serialized = new SerializedObject(catalogue);
            var entries = serialized.FindProperty("entries");
            entries.arraySize = definitions.Length;
            for (var index = 0; index < definitions.Length; index++)
            {
                var element = entries.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("definition").objectReferenceValue = definitions[index];
                element.FindPropertyRelative("thumbnail").objectReferenceValue = thumbnails[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return catalogue;
        }

        private static FurnitureDefinitionAsset CreateDefinition(string id, GameObject prefab)
        {
            var definition = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
            definition.name = id.Replace('.', '_');
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("definitionId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = "Counter Fixture";
            serialized.FindProperty("footprintWidth").intValue = 1;
            serialized.FindProperty("footprintDepth").intValue = 1;
            serialized.FindProperty("allowedPlacementSurfaces").intValue =
                (int)PlacementSurfaceType.Floor;
            serialized.FindProperty("functionType").intValue = (int)FurnitureFunctionType.None;
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static Sprite CreateSprite()
        {
            var texture = new Texture2D(2, 2) { name = "T_CounterFixture" };
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            sprite.name = "S_CounterFixture";
            return sprite;
        }

        private static void DestroyCatalogueFixture(DecorationCatalogueAsset catalogue)
        {
            var serialized = new SerializedObject(catalogue);
            var entries = serialized.FindProperty("entries");
            for (var index = 0; index < entries.arraySize; index++)
            {
                var definition = entries.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("definition").objectReferenceValue;
                var sprite = entries.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("thumbnail").objectReferenceValue as Sprite;
                if (definition != null)
                {
                    UnityEngine.Object.DestroyImmediate(definition);
                }

                if (sprite != null)
                {
                    var texture = sprite.texture;
                    UnityEngine.Object.DestroyImmediate(sprite);
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }

            UnityEngine.Object.DestroyImmediate(catalogue);
        }
    }
}

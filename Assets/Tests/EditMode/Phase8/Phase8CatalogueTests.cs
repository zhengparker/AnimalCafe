using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class Phase8CatalogueTests
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = owned.Count - 1; index >= 0; index--)
            {
                if (owned[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned[index]);
                }
            }

            owned.Clear();
        }

        [Test]
        public void ItemKind_PreservesPhase7SerializedValuesAndAddsPhase8Kinds()
        {
            Assert.That((int)DecorationCatalogueItemKind.Furniture, Is.EqualTo(0));
            Assert.That((int)DecorationCatalogueItemKind.Floor, Is.EqualTo(1));
            Assert.That((int)DecorationCatalogueItemKind.WallSurface, Is.EqualTo(2));
            Assert.That((int)DecorationCatalogueItemKind.WallMounted, Is.EqualTo(3));
            Assert.That(ParseKind("CashRegister"), Is.Not.EqualTo(DecorationCatalogueItemKind.Furniture));
            Assert.That(ParseKind("CoffeeMachine"), Is.Not.EqualTo(DecorationCatalogueItemKind.Furniture));
            Assert.That(ParseKind("PickUpPoint"), Is.Not.EqualTo(DecorationCatalogueItemKind.Furniture));
        }

        [Test]
        public void BuildFurnitureTab_PartitionsEntriesByPlacementSurfaceAndFunctionInStableRowOrder()
        {
            var floorA = CreateDefinition(
                "furniture.counter.a", "Counter A", PlacementSurfaceType.Floor,
                FurnitureFunctionType.None);
            var coffee = CreateDefinition(
                "equipment.coffee-machine.a", "Coffee Machine A",
                PlacementSurfaceType.FurnitureSurface, FurnitureFunctionType.CoffeeMachine);
            var cash = CreateDefinition(
                "equipment.cash-register.a", "Cash Register A",
                PlacementSurfaceType.FurnitureSurface, FurnitureFunctionType.CashRegister);
            var floorB = CreateDefinition(
                "furniture.counter.b", "Counter B", PlacementSurfaceType.Floor,
                FurnitureFunctionType.None);
            var catalogue = CreateCatalogue(coffee, floorA, cash, floorB);

            var rows = BuildFurnitureTab(catalogue);

            Assert.That(rows.Select(row => row.CategoryId), Is.EqualTo(new[]
            {
                "furniture", "cash-register", "coffee-machine"
            }));
            Assert.That(rows.Select(row => row.DisplayName), Is.EqualTo(new[]
            {
                "Furniture", "Cash Register", "Coffee Machine"
            }));
            Assert.That(rows[0].Items.Select(item => item.ItemId), Is.EqualTo(new[]
            {
                "furniture.counter.a", "furniture.counter.b"
            }));
            Assert.That(rows[1].Items.Single().ItemId, Is.EqualTo("equipment.cash-register.a"));
            Assert.That(rows[2].Items.Single().ItemId, Is.EqualTo("equipment.coffee-machine.a"));
            Assert.That(rows[0].Items, Has.All.Property("Kind")
                .EqualTo(DecorationCatalogueItemKind.Furniture));
            Assert.That(rows[1].Items.Single().Kind, Is.EqualTo(ParseKind("CashRegister")));
            Assert.That(rows[2].Items.Single().Kind, Is.EqualTo(ParseKind("CoffeeMachine")));
            Assert.That(rows.SelectMany(row => row.Items).Any(item =>
                item.Kind.Equals(ParseKind("PickUpPoint"))), Is.False,
                "Pick-up is a tab action, not a fake definition or card.");
        }

        [Test]
        public void BuildFurnitureTab_KeepsAllThreeRowsWhenEveryRowIsEmpty()
        {
            var rows = BuildFurnitureTab(CreateCatalogue());

            Assert.That(rows.Select(row => row.DisplayName), Is.EqualTo(new[]
            {
                "Furniture", "Cash Register", "Coffee Machine"
            }));
            Assert.That(rows.All(row => row.Items.Count == 0), Is.True);
        }

        [Test]
        public void BuildFurnitureTab_RejectsAFunctionPlacedOnTheWrongSurface()
        {
            var invalid = CreateDefinition(
                "equipment.cash-register.floor", "Floor Register",
                PlacementSurfaceType.Floor, FurnitureFunctionType.CashRegister);

            var exception = Assert.Throws<TargetInvocationException>(() =>
                BuildFurnitureTab(CreateCatalogue(invalid)));

            Assert.That(exception.InnerException,
                Is.TypeOf<DecorationCatalogueValidationException>());
            var issue = (DecorationCatalogueValidationException)exception.InnerException;
            Assert.That(issue.Code,
                Is.EqualTo(DecorationCatalogueValidationIssueCode.WrongCategoryKind));
            Assert.That(issue.CategoryId, Is.EqualTo("furniture-tab"));
            Assert.That(issue.ItemId, Is.EqualTo("equipment.cash-register.floor"));
        }

        [Test]
        public void BuildFurnitureTab_RejectsNullEntryAndNullDefinitionWithLegacyStructuredIssue()
        {
            var nullEntryIssue = AssertValidationIssue(
                CreateRawCatalogue((DecorationCatalogueEntry)null),
                DecorationCatalogueValidationIssueCode.NullEntry,
                null);
            var nullDefinitionIssue = AssertValidationIssue(
                CreateRawCatalogue(CreateRawEntry(null, "null-definition")),
                DecorationCatalogueValidationIssueCode.NullEntry,
                null);

            Assert.That(nullEntryIssue.CategoryId, Is.EqualTo("furniture-tab"));
            Assert.That(nullDefinitionIssue.CategoryId, Is.EqualTo("furniture-tab"));
        }

        [Test]
        public void BuildFurnitureTab_RejectsDuplicateStableIdAcrossFurnitureRows()
        {
            const string duplicateId = "equipment.duplicate";
            var catalogue = CreateCatalogue(
                CreateDefinition(duplicateId, "Duplicate Furniture",
                    PlacementSurfaceType.Floor, FurnitureFunctionType.None),
                CreateDefinition(duplicateId, "Duplicate Register",
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CashRegister));

            var issue = AssertValidationIssue(catalogue,
                DecorationCatalogueValidationIssueCode.DuplicateItemId,
                duplicateId);

            Assert.That(issue.CategoryId, Is.EqualTo("furniture-tab"));
        }

        [TestCase(PlacementSurfaceType.FurnitureSurface, FurnitureFunctionType.None,
            "furniture-surface-none")]
        [TestCase(PlacementSurfaceType.Floor, FurnitureFunctionType.CashRegister,
            "floor-cash-register")]
        [TestCase(PlacementSurfaceType.Floor, FurnitureFunctionType.CoffeeMachine,
            "floor-coffee-machine")]
        [TestCase(PlacementSurfaceType.Floor | PlacementSurfaceType.FurnitureSurface,
            FurnitureFunctionType.None, "combined-none")]
        [TestCase(PlacementSurfaceType.Floor | PlacementSurfaceType.FurnitureSurface,
            FurnitureFunctionType.CashRegister, "combined-cash-register")]
        [TestCase(PlacementSurfaceType.Floor | PlacementSurfaceType.FurnitureSurface,
            FurnitureFunctionType.CoffeeMachine, "combined-coffee-machine")]
        [TestCase(PlacementSurfaceType.Wall, FurnitureFunctionType.None,
            "wall-none")]
        [TestCase(PlacementSurfaceType.None, FurnitureFunctionType.None,
            "no-surface-none")]
        public void BuildFurnitureTab_RequiresExactSurfaceAndFunctionPair(
            PlacementSurfaceType surfaces,
            FurnitureFunctionType functionType,
            string suffix)
        {
            var itemId = "invalid." + suffix;
            var invalid = CreateDefinition(itemId, "Invalid " + suffix,
                surfaces, functionType);

            var issue = AssertValidationIssue(CreateCatalogue(invalid),
                DecorationCatalogueValidationIssueCode.WrongCategoryKind,
                itemId);

            Assert.That(issue.CategoryId, Is.EqualTo("furniture-tab"));
        }

        [Test]
        public void Entries_ReturnsADefensiveReadOnlySnapshot()
        {
            var catalogue = CreateCatalogue(CreateDefinition(
                "furniture.counter.snapshot", "Snapshot Counter",
                PlacementSurfaceType.Floor, FurnitureFunctionType.None));
            var firstRead = catalogue.Entries;

            SetCatalogueEntries(catalogue, Array.Empty<FurnitureDefinitionAsset>());

            Assert.That(firstRead, Has.Count.EqualTo(1));
            Assert.That(catalogue.Entries, Is.Empty);
            Assert.That(firstRead, Is.Not.TypeOf<List<DecorationCatalogueEntry>>());
            Assert.Throws<NotSupportedException>(() =>
                ((IList<DecorationCatalogueEntry>)firstRead).Add(null));
        }

        private static DecorationCatalogueItemKind ParseKind(string name)
        {
            Assert.That(Enum.TryParse(name, out DecorationCatalogueItemKind result), Is.True,
                "Task 7 catalogue kind '" + name + "' is missing.");
            return result;
        }

        private static DecorationCatalogueValidationException AssertValidationIssue(
            DecorationCatalogueAsset catalogue,
            DecorationCatalogueValidationIssueCode expectedCode,
            string expectedItemId)
        {
            var wrapper = Assert.Throws<TargetInvocationException>(() =>
                BuildFurnitureTab(catalogue));
            Assert.That(wrapper.InnerException,
                Is.TypeOf<DecorationCatalogueValidationException>());
            var issue = (DecorationCatalogueValidationException)wrapper.InnerException;
            Assert.That(issue.Code, Is.EqualTo(expectedCode));
            Assert.That(issue.ItemId, Is.EqualTo(expectedItemId));
            return issue;
        }

        private static IReadOnlyList<DecorationCategoryModel> BuildFurnitureTab(
            DecorationCatalogueAsset catalogue)
        {
            var method = typeof(DecorationCatalogueModelBuilder).GetMethod(
                "BuildFurnitureTab",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(DecorationCatalogueAsset) },
                null);
            Assert.That(method, Is.Not.Null,
                "Task 7 requires the additive Furniture Tab model-builder entry point.");
            return (IReadOnlyList<DecorationCategoryModel>)method.Invoke(
                null, new object[] { catalogue });
        }

        private FurnitureDefinitionAsset CreateDefinition(
            string id,
            string displayName,
            PlacementSurfaceType surfaces,
            FurnitureFunctionType functionType)
        {
            var definition = Track(ScriptableObject.CreateInstance<FurnitureDefinitionAsset>());
            var prefab = Track(new GameObject("PF_" + id));
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("definitionId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("footprintWidth").intValue = 1;
            serialized.FindProperty("footprintDepth").intValue = 1;
            serialized.FindProperty("allowedPlacementSurfaces").intValue = (int)surfaces;
            serialized.FindProperty("functionType").intValue = (int)functionType;
            serialized.FindProperty("prefab").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private DecorationCatalogueAsset CreateCatalogue(
            params FurnitureDefinitionAsset[] definitions)
        {
            var catalogue = Track(ScriptableObject.CreateInstance<DecorationCatalogueAsset>());
            SetCatalogueEntries(catalogue, definitions);
            return catalogue;
        }

        private DecorationCatalogueAsset CreateRawCatalogue(
            params DecorationCatalogueEntry[] entries)
        {
            var catalogue = Track(ScriptableObject.CreateInstance<DecorationCatalogueAsset>());
            var field = typeof(DecorationCatalogueAsset).GetField(
                "entries", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(catalogue, new List<DecorationCatalogueEntry>(entries));
            return catalogue;
        }

        private DecorationCatalogueEntry CreateRawEntry(
            FurnitureDefinitionAsset definition,
            string spriteName)
        {
            var entry = new DecorationCatalogueEntry();
            var definitionField = typeof(DecorationCatalogueEntry).GetField(
                "definition", BindingFlags.Instance | BindingFlags.NonPublic);
            var thumbnailField = typeof(DecorationCatalogueEntry).GetField(
                "thumbnail", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(definitionField, Is.Not.Null);
            Assert.That(thumbnailField, Is.Not.Null);
            definitionField.SetValue(entry, definition);
            thumbnailField.SetValue(entry, CreateSprite("S_" + spriteName));
            return entry;
        }

        private void SetCatalogueEntries(
            DecorationCatalogueAsset catalogue,
            IReadOnlyList<FurnitureDefinitionAsset> definitions)
        {
            var serialized = new SerializedObject(catalogue);
            var entries = serialized.FindProperty("entries");
            entries.arraySize = definitions.Count;
            for (var index = 0; index < definitions.Count; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("definition").objectReferenceValue = definitions[index];
                entry.FindPropertyRelative("thumbnail").objectReferenceValue = CreateSprite(
                    "S_" + definitions[index].DefinitionId);
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private Sprite CreateSprite(string name)
        {
            var texture = Track(new Texture2D(2, 2) { name = "T_" + name });
            var sprite = Track(Sprite.Create(
                texture, new Rect(0f, 0f, 2f, 2f), Vector2.one * 0.5f));
            sprite.name = name;
            return sprite;
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            owned.Add(value);
            return value;
        }
    }
}

using System;
using System.Collections.Generic;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class FunctionalDirectionCatalogTests
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
        public void BuildFunctionalDirectionCatalog_SnapshotsCashSidesAndCoffeeForwardMarker()
        {
            var cash = CreateEntry(
                "equipment.cash-register",
                FurnitureFunctionType.CashRegister);
            var employee = AddCashSide(cash.Prefab, CashRegisterSideType.Employee, CardinalDirection.West);
            var customer = AddCashSide(cash.Prefab, CashRegisterSideType.Customer, CardinalDirection.East);
            var coffee = CreateEntry(
                "equipment.coffee-machine",
                FurnitureFunctionType.CoffeeMachine);
            var forward = AddForwardMarker(coffee.Prefab);
            var content = CreateContentCatalog(cash, coffee);

            var snapshot = content.BuildFunctionalDirectionCatalog();
            SetSerialized(employee, "localDirection", CardinalDirection.North);
            SetSerialized(customer, "localDirection", CardinalDirection.South);
            forward.localRotation = Quaternion.Euler(0f, 90f, 0f);

            Assert.That(snapshot.TryGetCashRegisterSides(cash.DefinitionId, out var sides), Is.True);
            Assert.That(sides.EmployeeSide, Is.EqualTo(CardinalDirection.West));
            Assert.That(sides.CustomerSide, Is.EqualTo(CardinalDirection.East));
            Assert.That(snapshot.TryGetCoffeeMachineDirection(coffee.DefinitionId, out var direction), Is.True);
            Assert.That(direction, Is.EqualTo(CardinalDirection.North));
        }

        [Test]
        public void BuildFunctionalDirectionCatalog_IgnoresNonFunctionalFurniture()
        {
            var support = CreateEntry("furniture.counter.long", FurnitureFunctionType.None);
            var content = CreateContentCatalog(support);

            var snapshot = content.BuildFunctionalDirectionCatalog();

            Assert.That(snapshot.TryGetCashRegisterSides(support.DefinitionId, out _), Is.False);
            Assert.That(snapshot.TryGetCoffeeMachineDirection(support.DefinitionId, out _), Is.False);
        }

        [Test]
        public void BuildFunctionalDirectionCatalog_RejectsCoffeeWithoutExactlyOneForwardMarker()
        {
            var coffee = CreateEntry(
                "equipment.coffee-machine",
                FurnitureFunctionType.CoffeeMachine);
            var content = CreateContentCatalog(coffee);

            var exception = Assert.Throws<ArgumentException>(() =>
                content.BuildFunctionalDirectionCatalog());

            Assert.That(exception.Message, Does.Contain(coffee.DefinitionId));
            Assert.That(exception.Message, Does.Contain("ForwardMarker"));
        }

        [Test]
        public void SharedEntryTraversal_PreservesSurfaceSlotFailureOrderBeforeLaterInvalidEntry()
        {
            var support = CreateEntry("furniture.counter.long", FurnitureFunctionType.None);
            AddSurfaceSlot(support.Prefab, "slot.0", new Vector3(0.25f, 0f, 0f));
            var content = CreateContentCatalog(support, null);

            var exception = Assert.Throws<ArgumentException>(() =>
                content.BuildSurfaceSlotCatalog(new GridSettings(1f)));

            Assert.That(exception.Message, Does.Contain(support.DefinitionId));
            Assert.That(exception.Message, Does.Contain("slot.0"));
        }

        private FurnitureContentCatalog CreateContentCatalog(
            params FurnitureDefinitionAsset[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<FurnitureContentCatalog>();
            owned.Add(catalog);
            var serializedCatalog = new SerializedObject(catalog);
            var property = serializedCatalog.FindProperty("entries");
            property.arraySize = entries.Length;
            for (var index = 0; index < entries.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = entries[index];
            }

            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private FurnitureDefinitionAsset CreateEntry(
            string definitionId,
            FurnitureFunctionType functionType)
        {
            var entry = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
            owned.Add(entry);
            SetSerialized(entry, "definitionId", definitionId);
            SetSerialized(entry, "displayName", "Test Furniture");
            SetSerialized(entry, "footprintWidth", 1);
            SetSerialized(entry, "footprintDepth", 1);
            SetSerialized(entry, "allowedPlacementSurfaces", functionType == FurnitureFunctionType.None
                ? PlacementSurfaceType.Floor
                : PlacementSurfaceType.FurnitureSurface);
            SetSerialized(entry, "functionType", functionType);

            var prefab = new GameObject(definitionId);
            owned.Add(prefab);
            SetSerialized(entry, "prefab", prefab);
            return entry;
        }

        private CashRegisterSideMarker AddCashSide(
            GameObject root,
            CashRegisterSideType sideType,
            CardinalDirection direction)
        {
            var child = new GameObject(sideType.ToString());
            child.transform.SetParent(root.transform, false);
            var marker = child.AddComponent<CashRegisterSideMarker>();
            SetSerialized(marker, "sideType", sideType);
            SetSerialized(marker, "localDirection", direction);
            return marker;
        }

        private static Transform AddForwardMarker(GameObject root)
        {
            var child = new GameObject("ForwardMarker");
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = new Vector3(0f, 0.2f, 0.3f);
            child.transform.localRotation = Quaternion.identity;
            return child.transform;
        }

        private static void AddSurfaceSlot(
            GameObject root,
            string slotId,
            Vector3 localPosition)
        {
            var child = new GameObject(slotId);
            child.transform.SetParent(root.transform, false);
            child.transform.localPosition = localPosition;
            var marker = child.AddComponent<SurfaceSlotMarker>();
            SetSerialized(marker, "slotId", slotId);
        }

        private static void SetSerialized(
            UnityEngine.Object target,
            string propertyName,
            object value)
        {
            var serializedTarget = new SerializedObject(target);
            var property = serializedTarget.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, $"Missing serialized property '{propertyName}'.");

            switch (property.propertyType)
            {
                case SerializedPropertyType.String:
                    property.stringValue = (string)value;
                    break;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    property.intValue = Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = (UnityEngine.Object)value;
                    break;
                default:
                    Assert.Fail($"Unsupported serialized property type '{property.propertyType}'.");
                    break;
            }

            serializedTarget.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

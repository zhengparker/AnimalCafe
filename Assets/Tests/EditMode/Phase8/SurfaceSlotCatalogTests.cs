using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class SurfaceSlotCatalogTests
    {
        [Test]
        public void BuildSurfaceSlotCatalog_ConvertsNeutralLongCounterMarkersToOrderedLocalCells()
        {
            var content = CreateContentCatalog(
                CreateEntry(
                    "furniture.counter.long.01",
                    1,
                    3,
                    ("slot.2", new Vector3(0f, 0.72f, 1f)),
                    ("slot.0", new Vector3(0f, 0.72f, -1f)),
                    ("slot.1", new Vector3(0f, 0.72f, 0f))));

            var catalog = content.BuildSurfaceSlotCatalog(new GridSettings(1f));

            var slots = catalog.GetForSupport("furniture.counter.long.01");
            Assert.That(slots.Select(slot => slot.SlotId),
                Is.EqualTo(new[] { "slot.0", "slot.1", "slot.2" }));
            Assert.That(slots.Select(slot => slot.LocalCell), Is.EqualTo(new[]
            {
                new GridPosition(0, 0),
                new GridPosition(0, 1),
                new GridPosition(0, 2)
            }));
        }

        [Test]
        public void BuildSurfaceSlotCatalog_UsesGridSettingsCellSizeForMarkerConversion()
        {
            var content = CreateContentCatalog(CreateEntry(
                "furniture.counter.long.01",
                1,
                3,
                ("slot.0", new Vector3(0f, 0.72f, -2f)),
                ("slot.1", new Vector3(0f, 0.72f, 0f)),
                ("slot.2", new Vector3(0f, 0.72f, 2f))));

            var catalog = content.BuildSurfaceSlotCatalog(new GridSettings(2f));

            Assert.That(catalog.GetForSupport("furniture.counter.long.01")
                .Select(slot => slot.LocalCell), Is.EqualTo(new[]
            {
                new GridPosition(0, 0),
                new GridPosition(0, 1),
                new GridPosition(0, 2)
            }));
        }

        [Test]
        public void BuildSurfaceSlotCatalog_RejectsDuplicateSlotWithDefinitionAndSlotDiagnostics()
        {
            var content = CreateContentCatalog(CreateEntry(
                "furniture.counter.module.01",
                1,
                1,
                ("slot.0", Vector3.zero),
                ("slot.0", Vector3.zero)));

            var exception = Assert.Throws<ArgumentException>(() =>
                content.BuildSurfaceSlotCatalog(new GridSettings(1f)));

            Assert.That(exception.Message, Does.Contain("furniture.counter.module.01"));
            Assert.That(exception.Message, Does.Contain("slot.0"));
        }

        [Test]
        public void BuildSurfaceSlotCatalog_RejectsOffCellMarkerWithDefinitionAndSlotDiagnostics()
        {
            var content = CreateContentCatalog(CreateEntry(
                "furniture.counter.module.01",
                1,
                1,
                ("slot.0", new Vector3(0.25f, 0.72f, 0f))));

            var exception = Assert.Throws<ArgumentException>(() =>
                content.BuildSurfaceSlotCatalog(new GridSettings(1f)));

            Assert.That(exception.Message, Does.Contain("furniture.counter.module.01"));
            Assert.That(exception.Message, Does.Contain("slot.0"));
        }

        [Test]
        public void BuildSurfaceSlotCatalog_RejectsMarkerOutsideNeutralFootprintWithDefinitionAndSlotDiagnostics()
        {
            var content = CreateContentCatalog(CreateEntry(
                "furniture.counter.module.01",
                1,
                1,
                ("slot.0", new Vector3(1f, 0.72f, 0f))));

            var exception = Assert.Throws<ArgumentException>(() =>
                content.BuildSurfaceSlotCatalog(new GridSettings(1f)));

            Assert.That(exception.Message, Does.Contain("furniture.counter.module.01"));
            Assert.That(exception.Message, Does.Contain("slot.0"));
        }

        [Test]
        public void BuildSurfaceSlotCatalog_RejectsNullMarkerSlotIdWithIdentifiableDiagnostic()
        {
            var content = CreateContentCatalog(CreateEntry(
                "furniture.counter.module.01",
                1,
                1,
                (null, Vector3.zero)));

            var exception = Assert.Throws<ArgumentException>(() =>
                content.BuildSurfaceSlotCatalog(new GridSettings(1f)));

            Assert.That(exception.Message, Does.Contain("furniture.counter.module.01"));
            Assert.That(exception.Message, Does.Contain("<null>"));
        }

        [Test]
        public void BuildSurfaceSlotCatalog_RejectsBlankMarkerSlotIdWithIdentifiableDiagnostic()
        {
            var content = CreateContentCatalog(CreateEntry(
                "furniture.counter.module.01",
                1,
                1,
                ("   ", Vector3.zero)));

            var exception = Assert.Throws<ArgumentException>(() =>
                content.BuildSurfaceSlotCatalog(new GridSettings(1f)));

            Assert.That(exception.Message, Does.Contain("furniture.counter.module.01"));
            Assert.That(exception.Message, Does.Contain("<blank>"));
        }

        private static FurnitureContentCatalog CreateContentCatalog(
            params FurnitureDefinitionAsset[] entries)
        {
            var catalog = ScriptableObject.CreateInstance<FurnitureContentCatalog>();
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

        private static FurnitureDefinitionAsset CreateEntry(
            string definitionId,
            int width,
            int depth,
            params (string slotId, Vector3 localPosition)[] markers)
        {
            var entry = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
            SetSerialized(entry, "definitionId", definitionId);
            SetSerialized(entry, "displayName", "Test support");
            SetSerialized(entry, "footprintWidth", width);
            SetSerialized(entry, "footprintDepth", depth);
            SetSerialized(entry, "allowedPlacementSurfaces", PlacementSurfaceType.Floor);
            SetSerialized(entry, "functionType", FurnitureFunctionType.None);

            var prefab = new GameObject(definitionId);
            foreach (var markerData in markers)
            {
                var markerObject = new GameObject(markerData.slotId);
                markerObject.transform.SetParent(prefab.transform, false);
                markerObject.transform.localPosition = markerData.localPosition;
                var marker = markerObject.AddComponent<SurfaceSlotMarker>();
                SetSerialized(marker, "slotId", markerData.slotId);
            }

            SetSerialized(entry, "prefab", prefab);
            return entry;
        }

        private static void SetSerialized(UnityEngine.Object target, string propertyName, object value)
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

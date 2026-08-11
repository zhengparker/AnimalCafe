using System;
using System.Collections.Generic;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.Phase4
{
    public sealed class FurnitureContentCatalogTests
    {
        [Test]
        public void BuildRuntimeCatalog_MapsSameStableIdToDefinitionAndPrefab()
        {
            var content = CreateContentCatalog(CreateValidAsset("furniture.counter.module.01"));

            var runtime = content.BuildRuntimeCatalog();

            Assert.That(
                runtime.GetRequired("furniture.counter.module.01").Footprint,
                Is.EqualTo(new GridSize(1, 1)));
            Assert.That(content.TryGetPrefab("furniture.counter.module.01", out var prefab), Is.True);
            Assert.That(prefab, Is.Not.Null);
        }

        [Test]
        public void BuildRuntimeCatalog_RejectsEntryWithoutPrefab()
        {
            var entry = CreateValidAsset("furniture.counter.module.01");
            SetSerialized(entry, "prefab", null);
            var content = CreateContentCatalog(entry);

            Assert.Throws<InvalidOperationException>(() => content.BuildRuntimeCatalog());
            Assert.That(content.TryGetPrefab("furniture.counter.module.01", out _), Is.False);
        }

        [Test]
        public void BuildRuntimeCatalog_RejectsDuplicateStableIdsWithoutPublishingPartialPrefabLookup()
        {
            var content = CreateContentCatalog(
                CreateValidAsset("furniture.counter.module.01"),
                CreateValidAsset("furniture.counter.module.01"));

            Assert.Throws<ArgumentException>(() => content.BuildRuntimeCatalog());
            Assert.That(content.TryGetPrefab("furniture.counter.module.01", out _), Is.False);
        }

        [Test]
        public void BuildRuntimeCatalog_RejectsNullEntryWithoutPublishingPartialPrefabLookup()
        {
            var content = CreateContentCatalog(
                CreateValidAsset("furniture.counter.module.01"),
                null);

            Assert.Throws<ArgumentException>(() => content.BuildRuntimeCatalog());
            Assert.That(content.TryGetPrefab("furniture.counter.module.01", out _), Is.False);
        }

        [Test]
        public void BuildRuntimeCatalog_FailedRebuildPreservesPreviouslyPublishedPrefabSnapshot()
        {
            var firstEntry = CreateValidAsset("furniture.counter.module.01");
            var content = CreateContentCatalog(firstEntry);
            content.BuildRuntimeCatalog();

            var invalidEntry = CreateValidAsset("furniture.table.round.01");
            SetSerialized(invalidEntry, "prefab", null);
            SetEntries(content, firstEntry, invalidEntry);

            Assert.Throws<InvalidOperationException>(() => content.BuildRuntimeCatalog());
            Assert.That(content.TryGetPrefab("furniture.counter.module.01", out var cachedPrefab), Is.True);
            Assert.That(cachedPrefab, Is.SameAs(firstEntry.Prefab));
            Assert.That(content.TryGetPrefab("furniture.table.round.01", out _), Is.False);
        }

        [Test]
        public void BuildRuntimeCatalog_PreservesSerializedListOrder()
        {
            var secondInSortOrder = CreateValidAsset("furniture.table.round.01");
            var firstInSortOrder = CreateValidAsset("furniture.counter.module.01");
            var content = CreateContentCatalog(secondInSortOrder, firstInSortOrder);

            var runtime = content.BuildRuntimeCatalog();

            Assert.That(runtime.Definitions[0].Id, Is.EqualTo("furniture.table.round.01"));
            Assert.That(runtime.Definitions[1].Id, Is.EqualTo("furniture.counter.module.01"));
        }

        private static FurnitureContentCatalog CreateContentCatalog(params FurnitureDefinitionAsset[] entries)
        {
            var content = ScriptableObject.CreateInstance<FurnitureContentCatalog>();
            SetEntries(content, entries);
            return content;
        }

        private static FurnitureDefinitionAsset CreateValidAsset(string definitionId)
        {
            var asset = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
            SetSerialized(asset, "definitionId", definitionId);
            SetSerialized(asset, "displayName", "Test Furniture");
            SetSerialized(asset, "footprintWidth", 1);
            SetSerialized(asset, "footprintDepth", 1);
            SetSerialized(asset, "allowedPlacementSurfaces", PlacementSurfaceType.Floor);
            SetSerialized(asset, "functionType", FurnitureFunctionType.None);
            SetSerialized(asset, "prefab", new GameObject(definitionId));
            return asset;
        }

        private static void SetEntries(
            FurnitureContentCatalog content,
            params FurnitureDefinitionAsset[] entries)
        {
            var serializedContent = new SerializedObject(content);
            var property = serializedContent.FindProperty("entries");

            Assert.That(property, Is.Not.Null, "Missing serialized property 'entries'.");
            property.arraySize = entries.Length;
            for (var index = 0; index < entries.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = entries[index];
            }

            serializedContent.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerialized(
            FurnitureDefinitionAsset asset,
            string propertyName,
            object value)
        {
            var serializedAsset = new SerializedObject(asset);
            var property = serializedAsset.FindProperty(propertyName);

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

            serializedAsset.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

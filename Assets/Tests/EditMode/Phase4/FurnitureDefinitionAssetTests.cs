using System;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.Phase4
{
    public sealed class FurnitureDefinitionAssetTests
    {
        [Test]
        public void ToRuntimeDefinition_PreservesInspectorAuthoredValues()
        {
            var asset = CreateAsset(
                "equipment.cash-register.01",
                "Cash Register",
                2,
                3,
                PlacementSurfaceType.FurnitureSurface,
                FurnitureFunctionType.CashRegister);

            var runtime = asset.ToRuntimeDefinition();

            Assert.That(runtime.Id, Is.EqualTo("equipment.cash-register.01"));
            Assert.That(runtime.DisplayName, Is.EqualTo("Cash Register"));
            Assert.That(runtime.Footprint, Is.EqualTo(new GridSize(2, 3)));
            Assert.That(runtime.AllowedPlacementSurfaces, Is.EqualTo(PlacementSurfaceType.FurnitureSurface));
            Assert.That(runtime.FunctionType, Is.EqualTo(FurnitureFunctionType.CashRegister));
        }

        [TestCase(1, 1, FurnitureRotation.Degrees90, 1, 1)]
        [TestCase(1, 3, FurnitureRotation.Degrees90, 3, 1)]
        [TestCase(1, 3, FurnitureRotation.Degrees270, 3, 1)]
        public void ToRuntimeDefinition_ApprovedFootprintRotationReturnsExpectedSize(
            int width,
            int depth,
            FurnitureRotation rotation,
            int expectedWidth,
            int expectedDepth)
        {
            var asset = CreateAsset(
                "furniture.rotation-fixture.01",
                "Rotation Fixture",
                width,
                depth,
                PlacementSurfaceType.Floor,
                FurnitureFunctionType.None);

            var rotated = asset.ToRuntimeDefinition().Footprint.Rotate(rotation);

            Assert.That(rotated, Is.EqualTo(new GridSize(expectedWidth, expectedDepth)));
        }

        [Test]
        public void ToRuntimeDefinition_MaximumLegalOneBy1024RotatesTo1024ByOne()
        {
            var asset = CreateAsset(
                "furniture.maximum-footprint.01",
                "Maximum Footprint",
                1,
                FurnitureDefinition.MaxFootprintCellCount,
                PlacementSurfaceType.Floor,
                FurnitureFunctionType.None);

            var runtime = asset.ToRuntimeDefinition();
            var rotated = runtime.Footprint.Rotate(FurnitureRotation.Degrees90);

            Assert.That(runtime.Footprint,
                Is.EqualTo(new GridSize(1, FurnitureDefinition.MaxFootprintCellCount)));
            Assert.That(rotated,
                Is.EqualTo(new GridSize(FurnitureDefinition.MaxFootprintCellCount, 1)));
        }

        [TestCase(0, 1)]
        [TestCase(1, 0)]
        [TestCase(-1, 1)]
        [TestCase(1, -1)]
        public void ToRuntimeDefinition_RejectsNonPositiveInspectorFootprint(int width, int depth)
        {
            var asset = CreateAsset(
                "furniture.counter.module.01",
                "Counter",
                width,
                depth,
                PlacementSurfaceType.Floor,
                FurnitureFunctionType.None);

            Assert.Throws<ArgumentOutOfRangeException>(() => asset.ToRuntimeDefinition());
        }

        [TestCase(33, 32)]
        [TestCase(1, 1025)]
        public void ToRuntimeDefinition_RejectsOversizedInspectorFootprint(int width, int depth)
        {
            var asset = CreateAsset(
                "furniture.counter.module.01",
                "Counter",
                width,
                depth,
                PlacementSurfaceType.Floor,
                FurnitureFunctionType.None);

            Assert.Throws<ArgumentOutOfRangeException>(() => asset.ToRuntimeDefinition());
        }

        [Test]
        public void ToRuntimeDefinition_RejectsInvalidPlacementSurfaceValue()
        {
            var asset = CreateAsset(
                "furniture.counter.module.01",
                "Counter",
                1,
                1,
                PlacementSurfaceType.None,
                FurnitureFunctionType.None);

            Assert.Throws<ArgumentOutOfRangeException>(() => asset.ToRuntimeDefinition());
        }

        [Test]
        public void ToRuntimeDefinition_RejectsInvalidFunctionTypeValue()
        {
            var asset = CreateAsset(
                "furniture.counter.module.01",
                "Counter",
                1,
                1,
                PlacementSurfaceType.Floor,
                (FurnitureFunctionType)99);

            Assert.Throws<ArgumentOutOfRangeException>(() => asset.ToRuntimeDefinition());
        }

        [Test]
        public void ToRuntimeDefinition_DoesNotMutateInspectorAuthoredValues()
        {
            var asset = CreateAsset(
                "furniture.table.round.01",
                "Round Table",
                2,
                2,
                PlacementSurfaceType.Floor | PlacementSurfaceType.FurnitureSurface,
                FurnitureFunctionType.None);

            asset.ToRuntimeDefinition();

            Assert.That(asset.DefinitionId, Is.EqualTo("furniture.table.round.01"));
            Assert.That(asset.DisplayName, Is.EqualTo("Round Table"));
            Assert.That(asset.FootprintWidth, Is.EqualTo(2));
            Assert.That(asset.FootprintDepth, Is.EqualTo(2));
            Assert.That(
                asset.AllowedPlacementSurfaces,
                Is.EqualTo(PlacementSurfaceType.Floor | PlacementSurfaceType.FurnitureSurface));
            Assert.That(asset.FunctionType, Is.EqualTo(FurnitureFunctionType.None));
        }

        private static FurnitureDefinitionAsset CreateAsset(
            string definitionId,
            string displayName,
            int width,
            int depth,
            PlacementSurfaceType placementSurface,
            FurnitureFunctionType functionType)
        {
            var asset = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
            SetSerialized(asset, "definitionId", definitionId);
            SetSerialized(asset, "displayName", displayName);
            SetSerialized(asset, "footprintWidth", width);
            SetSerialized(asset, "footprintDepth", depth);
            SetSerialized(asset, "allowedPlacementSurfaces", placementSurface);
            SetSerialized(asset, "functionType", functionType);
            return asset;
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
                default:
                    Assert.Fail($"Unsupported serialized property type '{property.propertyType}'.");
                    break;
            }

            serializedAsset.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEngine;

namespace AnimalCafe.Tests
{
    public sealed class FurnitureDefinitionTests
    {
        [Test]
        public void FurnitureDefinition_ValidValuesStoreExactly()
        {
            var definition = new FurnitureDefinition(
                "furniture.counter.basic",
                "Basic Counter",
                new GridSize(2, 1),
                PlacementSurfaceType.Floor);

            Assert.That(definition.Id, Is.EqualTo("furniture.counter.basic"));
            Assert.That(definition.DisplayName, Is.EqualTo("Basic Counter"));
            Assert.That(definition.Footprint, Is.EqualTo(new GridSize(2, 1)));
            Assert.That(definition.AllowedPlacementSurfaces, Is.EqualTo(PlacementSurfaceType.Floor));
        }

        [Test]
        public void FurnitureDefinition_MultipleAllowedSurfacesStoreExactFlags()
        {
            var surfaces = PlacementSurfaceType.Floor | PlacementSurfaceType.Wall;

            var definition = new FurnitureDefinition("decor.wall_lamp", "Wall Lamp", new GridSize(1, 1), surfaces);

            Assert.That(definition.AllowedPlacementSurfaces, Is.EqualTo(surfaces));
        }

        [Test]
        public void FurnitureDefinition_NullIdThrows()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FurnitureDefinition(null, "Basic Counter", new GridSize(2, 1), PlacementSurfaceType.Floor));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Furniture.Counter")]
        [TestCase("furniture counter")]
        [TestCase("furniture/counter")]
        [TestCase("furniture\\counter")]
        public void FurnitureDefinition_InvalidIdThrows(string id)
        {
            Assert.Throws<ArgumentException>(() =>
                new FurnitureDefinition(id, "Basic Counter", new GridSize(2, 1), PlacementSurfaceType.Floor));
        }

        [Test]
        public void FurnitureDefinition_NullDisplayNameThrows()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new FurnitureDefinition("furniture.counter.basic", null, new GridSize(2, 1), PlacementSurfaceType.Floor));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void FurnitureDefinition_EmptyOrWhitespaceDisplayNameThrows(string displayName)
        {
            Assert.Throws<ArgumentException>(() =>
                new FurnitureDefinition("furniture.counter.basic", displayName, new GridSize(2, 1), PlacementSurfaceType.Floor));
        }

        [Test]
        public void FurnitureDefinition_NoneSurfaceThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new FurnitureDefinition("furniture.counter.basic", "Basic Counter", new GridSize(2, 1), PlacementSurfaceType.None));
        }

        [Test]
        public void FurnitureDefinition_UnknownSurfaceFlagThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new FurnitureDefinition("furniture.counter.basic", "Basic Counter", new GridSize(2, 1), (PlacementSurfaceType)128));
        }

        [Test]
        public void FurnitureDefinition_IdentityDoesNotDependOnDisplayName()
        {
            var first = new FurnitureDefinition("furniture.counter.basic", "Basic Counter", new GridSize(2, 1), PlacementSurfaceType.Floor);
            var renamed = new FurnitureDefinition("furniture.counter.basic", "Counter", new GridSize(2, 1), PlacementSurfaceType.Floor);

            Assert.That(first.Id, Is.EqualTo(renamed.Id));
        }

        [Test]
        public void FurnitureDefinition_ValidDisplayNameIsNotTrimmedOrAltered()
        {
            var definition = new FurnitureDefinition(
                "furniture.counter.basic",
                "  Basic Counter  ",
                new GridSize(2, 1),
                PlacementSurfaceType.Floor);

            Assert.That(definition.DisplayName, Is.EqualTo("  Basic Counter  "));
        }

        [Test]
        public void FurnitureDefinition_InstanceFieldsDoNotReferenceUnityObjects()
        {
            var unityObjectFields = typeof(FurnitureDefinition)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(field => typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType));

            Assert.That(unityObjectFields, Is.Empty);
        }
    }
}

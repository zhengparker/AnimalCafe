using System;
using System.Linq;
using System.Reflection;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEngine;

namespace AnimalCafe.Tests
{
    public sealed class FurnitureInstanceTests
    {
        [Test]
        public void StableId_OneThousandGeneratedIdsAreUniqueAndValid()
        {
            var ids = Enumerable.Range(0, 1000)
                .Select(_ => StableId.NewFurnitureInstanceId())
                .ToArray();

            Assert.That(ids, Is.Unique);
            Assert.That(ids.All(StableId.IsValidFurnitureInstanceId), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("7f17d8fa-59f6-4be0-a668-9666ce4a28d2")]
        [TestCase("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
        [TestCase("7F17D8FA59F64BE0A6689666CE4A28D2")]
        [TestCase("not-a-guid")]
        public void StableId_InvalidValueIsRejected(string value)
        {
            Assert.That(StableId.IsValidFurnitureInstanceId(value), Is.False);
        }

        [Test]
        public void FurnitureInstance_CreateNewUsesUniqueStableId()
        {
            var first = FurnitureInstance.CreateNew("furniture.counter.basic", new GridPosition(2, 3), FurnitureRotation.Degrees0);
            var second = FurnitureInstance.CreateNew("furniture.counter.basic", new GridPosition(2, 3), FurnitureRotation.Degrees0);

            Assert.That(StableId.IsValidFurnitureInstanceId(first.InstanceId), Is.True);
            Assert.That(StableId.IsValidFurnitureInstanceId(second.InstanceId), Is.True);
            Assert.That(first.InstanceId, Is.Not.EqualTo(second.InstanceId));
        }

        [Test]
        public void FurnitureInstance_RestorePreservesValidId()
        {
            const string instanceId = "7f17d8fa59f64be0a6689666ce4a28d2";

            var instance = FurnitureInstance.Restore(
                instanceId,
                "furniture.counter.basic",
                new GridPosition(-5, 9),
                FurnitureRotation.Degrees270);

            Assert.That(instance.InstanceId, Is.EqualTo(instanceId));
        }

        [Test]
        public void FurnitureInstance_RestoreRejectsInvalidRotation()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FurnitureInstance.Restore(
                "7f17d8fa59f64be0a6689666ce4a28d2",
                "furniture.counter.basic",
                new GridPosition(0, 0),
                (FurnitureRotation)45));
        }

        [Test]
        public void FurnitureInstance_ValidValuesAreStoredExactly()
        {
            var position = new GridPosition(-4, 12);
            var instance = FurnitureInstance.Restore(
                "7f17d8fa59f64be0a6689666ce4a28d2",
                "furniture.counter.basic",
                position,
                FurnitureRotation.Degrees180);

            Assert.That(instance.InstanceId, Is.EqualTo("7f17d8fa59f64be0a6689666ce4a28d2"));
            Assert.That(instance.DefinitionId, Is.EqualTo("furniture.counter.basic"));
            Assert.That(instance.Position, Is.EqualTo(position));
            Assert.That(instance.Rotation, Is.EqualTo(FurnitureRotation.Degrees180));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Furniture.Counter")]
        public void FurnitureInstance_CreateNewRejectsInvalidDefinitionId(string definitionId)
        {
            Assert.Throws<ArgumentException>(() => FurnitureInstance.CreateNew(
                definitionId,
                new GridPosition(0, 0),
                FurnitureRotation.Degrees0));
        }

        [Test]
        public void FurnitureInstance_CreateNewRejectsNullDefinitionId()
        {
            Assert.Throws<ArgumentNullException>(() => FurnitureInstance.CreateNew(
                null,
                new GridPosition(0, 0),
                FurnitureRotation.Degrees0));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Furniture.Counter")]
        public void FurnitureInstance_RestoreRejectsInvalidDefinitionId(string definitionId)
        {
            Assert.Throws<ArgumentException>(() => FurnitureInstance.Restore(
                "7f17d8fa59f64be0a6689666ce4a28d2",
                definitionId,
                new GridPosition(0, 0),
                FurnitureRotation.Degrees0));
        }

        [Test]
        public void FurnitureInstance_RestoreRejectsNullDefinitionId()
        {
            Assert.Throws<ArgumentNullException>(() => FurnitureInstance.Restore(
                "7f17d8fa59f64be0a6689666ce4a28d2",
                null,
                new GridPosition(0, 0),
                FurnitureRotation.Degrees0));
        }

        [TestCase("")]
        [TestCase("7f17d8fa-59f6-4be0-a668-9666ce4a28d2")]
        [TestCase("7F17D8FA59F64BE0A6689666CE4A28D2")]
        public void FurnitureInstance_RestoreRejectsInvalidInstanceId(string instanceId)
        {
            Assert.Throws<ArgumentException>(() => FurnitureInstance.Restore(
                instanceId,
                "furniture.counter.basic",
                new GridPosition(0, 0),
                FurnitureRotation.Degrees0));
        }

        [Test]
        public void FurnitureInstance_RestoreRejectsNullInstanceId()
        {
            Assert.Throws<ArgumentNullException>(() => FurnitureInstance.Restore(
                null,
                "furniture.counter.basic",
                new GridPosition(0, 0),
                FurnitureRotation.Degrees0));
        }

        [Test]
        public void FurnitureInstance_SameDefinitionCreatesDistinctInstances()
        {
            var first = FurnitureInstance.CreateNew("furniture.counter.basic", new GridPosition(0, 0), FurnitureRotation.Degrees0);
            var second = FurnitureInstance.CreateNew("furniture.counter.basic", new GridPosition(1, 0), FurnitureRotation.Degrees90);

            Assert.That(first.DefinitionId, Is.EqualTo(second.DefinitionId));
            Assert.That(first.InstanceId, Is.Not.EqualTo(second.InstanceId));
        }

        [Test]
        public void FurnitureInstance_HasNoUnityObjectFields()
        {
            var unityObjectFields = typeof(FurnitureInstance)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(field => typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType));

            Assert.That(unityObjectFields, Is.Empty);
        }

        [Test]
        public void FurnitureInstance_HasNoDefinitionOrGridSizeData()
        {
            var members = typeof(FurnitureInstance)
                .GetMembers(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .OfType<PropertyInfo>()
                .Select(property => property.PropertyType)
                .Concat(typeof(FurnitureInstance)
                    .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                    .Select(field => field.FieldType));

            Assert.That(members.Any(type => type == typeof(GridSize)), Is.False);
            Assert.That(members.Any(type => type == typeof(FurnitureDefinition)), Is.False);
        }

        [Test]
        public void FurnitureInstance_PublicInstanceDataContainsOnlyLayoutIdentityAndTransform()
        {
            var properties = typeof(FurnitureInstance)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Select(property => property.Name);

            Assert.That(properties, Is.EquivalentTo(new[]
            {
                "InstanceId",
                "DefinitionId",
                "Position",
                "Rotation"
            }));
        }
    }
}

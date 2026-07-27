using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests
{
    public sealed class CafeLayoutTests
    {
        private const string KnownDefinitionId = "furniture.counter.basic";
        private const string FirstInstanceId = "7f17d8fa59f64be0a6689666ce4a28d2";
        private const string SecondInstanceId = "8a28e9ab60a75cf1b7790777df5b39e3";

        [TestCase(LayoutZoneType.Interior)]
        [TestCase(LayoutZoneType.Exterior)]
        public void LayoutRegion_AcceptsKnownZoneTypes(LayoutZoneType zoneType)
        {
            var region = new LayoutRegion(
                "region.main",
                new GridPosition(0, 0),
                new GridSize(4, 3),
                zoneType);

            Assert.That(region.Id, Is.EqualTo("region.main"));
            Assert.That(region.Origin, Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(region.Size, Is.EqualTo(new GridSize(4, 3)));
            Assert.That(region.ZoneType, Is.EqualTo(zoneType));
        }

        [Test]
        public void LayoutRegion_AcceptsNegativeOrigin()
        {
            var region = new LayoutRegion(
                "region.west",
                new GridPosition(-10, -4),
                new GridSize(2, 2),
                LayoutZoneType.Interior);

            Assert.That(region.Origin, Is.EqualTo(new GridPosition(-10, -4)));
        }

        [Test]
        public void LayoutRegion_NullIdThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new LayoutRegion(
                null,
                new GridPosition(0, 0),
                new GridSize(1, 1),
                LayoutZoneType.Interior));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void LayoutRegion_EmptyOrWhitespaceIdThrows(string id)
        {
            Assert.Throws<ArgumentException>(() => new LayoutRegion(
                id,
                new GridPosition(0, 0),
                new GridSize(1, 1),
                LayoutZoneType.Interior));
        }

        [Test]
        public void LayoutRegion_InvalidSizeIsRejectedByGridSize()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GridSize(0, 1));
        }

        [Test]
        public void LayoutRegion_DefaultGridSizeThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LayoutRegion(
                "region.invalid",
                new GridPosition(0, 0),
                default,
                LayoutZoneType.Interior));
        }

        [Test]
        public void LayoutRegion_InvalidZoneThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LayoutRegion(
                "region.main",
                new GridPosition(0, 0),
                new GridSize(1, 1),
                (LayoutZoneType)99));
        }

        [Test]
        public void CafeLayout_ConstructorRejectsNullDependencies()
        {
            var settings = new GridSettings(1f);
            var catalog = CreateCatalog();

            Assert.Throws<ArgumentNullException>(() => new CafeLayout(null, catalog));
            Assert.Throws<ArgumentNullException>(() => new CafeLayout(settings, null));
        }

        [Test]
        public void CafeLayout_StartsEmptyAndPreservesGridSettings()
        {
            var settings = new GridSettings(1f);
            var layout = new CafeLayout(settings, CreateCatalog());

            Assert.That(layout.GridSettings, Is.SameAs(settings));
            Assert.That(layout.UnlockedRegions, Is.Empty);
            Assert.That(layout.FurnitureInstances, Is.Empty);
        }

        [Test]
        public void CafeLayout_AddsInteriorAndExteriorRegions()
        {
            var layout = CreateLayout();
            var interior = CreateRegion("region.interior", 0, 0, 2, 2, LayoutZoneType.Interior);
            var exterior = CreateRegion("region.exterior", 2, 0, 2, 2, LayoutZoneType.Exterior);

            layout.AddRegion(interior);
            layout.AddRegion(exterior);

            Assert.That(layout.UnlockedRegions, Is.EqualTo(new[] { interior, exterior }));
        }

        [Test]
        public void CafeLayout_AddRegionNullThrowsWithoutMutation()
        {
            var layout = CreateLayout();

            Assert.Throws<ArgumentNullException>(() => layout.AddRegion(null));
            Assert.That(layout.UnlockedRegions, Is.Empty);
        }

        [Test]
        public void CafeLayout_RejectsDuplicateRegionIdWithoutMutation()
        {
            var layout = CreateLayout();
            var original = CreateRegion("region.main", 0, 0, 2, 2);
            layout.AddRegion(original);

            var exception = Assert.Throws<ArgumentException>(() =>
                layout.AddRegion(CreateRegion("region.main", 5, 5, 1, 1)));

            StringAssert.Contains("region.main", exception.Message);
            Assert.That(layout.UnlockedRegions, Is.EqualTo(new[] { original }));
        }

        [Test]
        public void CafeLayout_RegionIdsUseOrdinalComparison()
        {
            var layout = CreateLayout();

            layout.AddRegion(CreateRegion("Region.Main", 0, 0, 1, 1));
            layout.AddRegion(CreateRegion("region.main", 1, 0, 1, 1));

            Assert.That(layout.UnlockedRegions.Count, Is.EqualTo(2));
        }

        [Test]
        public void CafeLayout_AllowsAdjacentRegions()
        {
            var layout = CreateLayout();

            layout.AddRegion(CreateRegion("region.left", 0, 0, 2, 2));
            layout.AddRegion(CreateRegion("region.right", 2, 0, 2, 2));

            Assert.That(layout.UnlockedRegions.Count, Is.EqualTo(2));
        }

        [Test]
        public void CafeLayout_AllowsOverlappingRegionsBecauseConflictRulesArePhase2()
        {
            var layout = CreateLayout();

            layout.AddRegion(CreateRegion("region.first", 0, 0, 3, 3));
            layout.AddRegion(CreateRegion("region.second", 1, 1, 3, 3));

            Assert.That(layout.UnlockedRegions.Count, Is.EqualTo(2));
        }

        [Test]
        public void CafeLayout_AddsInstanceWithKnownDefinition()
        {
            var layout = CreateLayout();
            var instance = CreateInstance(FirstInstanceId);

            layout.AddFurnitureInstance(instance);

            Assert.That(layout.FurnitureInstances, Is.EqualTo(new[] { instance }));
        }

        [Test]
        public void CafeLayout_AddFurnitureInstanceNullThrowsWithoutMutation()
        {
            var layout = CreateLayout();

            Assert.Throws<ArgumentNullException>(() => layout.AddFurnitureInstance(null));
            Assert.That(layout.FurnitureInstances, Is.Empty);
        }

        [Test]
        public void CafeLayout_RejectsInstanceWithUnknownDefinitionWithoutMutation()
        {
            var layout = CreateLayout();
            var unknown = FurnitureInstance.Restore(
                FirstInstanceId,
                "furniture.unknown",
                new GridPosition(0, 0),
                FurnitureRotation.Degrees0);

            var exception = Assert.Throws<ArgumentException>(
                () => layout.AddFurnitureInstance(unknown));

            StringAssert.Contains("furniture.unknown", exception.Message);
            Assert.That(layout.FurnitureInstances, Is.Empty);
            Assert.That(layout.TryGetFurnitureInstance(FirstInstanceId, out _), Is.False);
        }

        [Test]
        public void CafeLayout_RejectsDuplicateInstanceWithoutMutation()
        {
            var layout = CreateLayout();
            var original = CreateInstance(FirstInstanceId);
            layout.AddFurnitureInstance(original);

            var duplicate = CreateInstance(FirstInstanceId, new GridPosition(8, 9));
            var exception = Assert.Throws<ArgumentException>(
                () => layout.AddFurnitureInstance(duplicate));

            StringAssert.Contains(FirstInstanceId, exception.Message);
            Assert.That(layout.FurnitureInstances, Is.EqualTo(new[] { original }));
        }

        [Test]
        public void CafeLayout_TryGetKnownInstanceReturnsExactObject()
        {
            var layout = CreateLayout();
            var instance = CreateInstance(FirstInstanceId);
            layout.AddFurnitureInstance(instance);

            var found = layout.TryGetFurnitureInstance(FirstInstanceId, out var result);

            Assert.That(found, Is.True);
            Assert.That(result, Is.SameAs(instance));
        }

        [Test]
        public void CafeLayout_TryGetUnknownValidInstanceReturnsFalseAndNull()
        {
            var layout = CreateLayout();

            var found = layout.TryGetFurnitureInstance(FirstInstanceId, out var result);

            Assert.That(found, Is.False);
            Assert.That(result, Is.Null);
        }

        [TestCase(null, typeof(ArgumentNullException))]
        [TestCase("", typeof(ArgumentException))]
        [TestCase("not-a-guid", typeof(ArgumentException))]
        public void CafeLayout_TryGetValidatesStableInstanceId(string instanceId, Type exceptionType)
        {
            var layout = CreateLayout();

            Assert.Throws(exceptionType, () => layout.TryGetFurnitureInstance(instanceId, out _));
        }

        [Test]
        public void CafeLayout_ExposedCollectionsCannotBeCastAndMutated()
        {
            var layout = CreateLayout();
            var region = CreateRegion("region.main", 0, 0, 2, 2);
            var instance = CreateInstance(FirstInstanceId);
            layout.AddRegion(region);
            layout.AddFurnitureInstance(instance);
            var regions = layout.UnlockedRegions as IList<LayoutRegion>;
            var instances = layout.FurnitureInstances as IList<FurnitureInstance>;

            Assert.That(regions, Is.Not.Null);
            Assert.That(instances, Is.Not.Null);
            Assert.Throws<NotSupportedException>(
                () => regions.Add(CreateRegion("region.other", 4, 4, 1, 1)));
            Assert.Throws<NotSupportedException>(
                () => instances.Add(CreateInstance(SecondInstanceId)));
            Assert.That(layout.UnlockedRegions, Is.EqualTo(new[] { region }));
            Assert.That(layout.FurnitureInstances, Is.EqualTo(new[] { instance }));
        }

        [Test]
        public void CafeLayout_AllowsSamePositionBecauseOccupancyIsPhase2()
        {
            var layout = CreateLayout();

            layout.AddFurnitureInstance(CreateInstance(FirstInstanceId, new GridPosition(3, 4)));
            layout.AddFurnitureInstance(CreateInstance(SecondInstanceId, new GridPosition(3, 4)));

            Assert.That(layout.FurnitureInstances.Count, Is.EqualTo(2));
        }

        [Test]
        public void CafeLayout_AllowsInstanceOutsideRegionsBecausePlacementIsPhase2()
        {
            var layout = CreateLayout();
            layout.AddRegion(CreateRegion("region.main", 0, 0, 2, 2));

            layout.AddFurnitureInstance(CreateInstance(FirstInstanceId, new GridPosition(100, -100)));

            Assert.That(layout.FurnitureInstances.Count, Is.EqualTo(1));
        }

        [Test]
        public void LayoutDomainTests_DoNotLoadMainCafeScene()
        {
            var scenePathBefore = SceneManager.GetActiveScene().path;
            var layout = CreateLayout();

            layout.AddRegion(CreateRegion("region.main", 0, 0, 2, 2));
            layout.AddFurnitureInstance(CreateInstance(FirstInstanceId));

            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(scenePathBefore));
            Assert.That(SceneManager.GetActiveScene().path, Does.Not.EndWith("MainCafe.unity"));
        }

        [Test]
        public void Task4LayoutDomainTypes_HaveNoUnityObjectOrSceneFields()
        {
            var domainTypes = new[]
            {
                typeof(FurnitureDefinitionCatalog),
                typeof(LayoutRegion),
                typeof(CafeLayout)
            };

            var forbiddenFields = domainTypes
                .SelectMany(type => type.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public))
                .Where(field => ContainsForbiddenReference(field.FieldType));

            Assert.That(forbiddenFields, Is.Empty);
        }

        private static CafeLayout CreateLayout()
        {
            return new CafeLayout(new GridSettings(1f), CreateCatalog());
        }

        private static FurnitureDefinitionCatalog CreateCatalog()
        {
            return new FurnitureDefinitionCatalog(new[]
            {
                new FurnitureDefinition(
                    KnownDefinitionId,
                    "Basic Counter",
                    new GridSize(2, 1),
                    PlacementSurfaceType.Floor)
            });
        }

        private static FurnitureInstance CreateInstance(
            string instanceId,
            GridPosition? position = null)
        {
            return FurnitureInstance.Restore(
                instanceId,
                KnownDefinitionId,
                position ?? new GridPosition(0, 0),
                FurnitureRotation.Degrees0);
        }

        private static LayoutRegion CreateRegion(
            string id,
            int x,
            int y,
            int width,
            int height,
            LayoutZoneType zoneType = LayoutZoneType.Interior)
        {
            return new LayoutRegion(
                id,
                new GridPosition(x, y),
                new GridSize(width, height),
                zoneType);
        }

        private static bool ContainsForbiddenReference(Type type)
        {
            if (typeof(UnityEngine.Object).IsAssignableFrom(type) ||
                type == typeof(Scene))
            {
                return true;
            }

            return type.IsGenericType &&
                   type.GetGenericArguments().Any(ContainsForbiddenReference);
        }
    }
}

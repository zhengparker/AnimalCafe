using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests
{
    public sealed class FurnitureDefinitionCatalogTests
    {
        [Test]
        public void Catalog_TryGetKnownIdReturnsExactDefinition()
        {
            var definition = CreateDefinition("furniture.counter.basic");
            var catalog = new FurnitureDefinitionCatalog(new[] { definition });

            var found = catalog.TryGet(definition.Id, out var result);

            Assert.That(found, Is.True);
            Assert.That(result, Is.SameAs(definition));
        }

        [Test]
        public void Catalog_TryGetUnknownValidIdReturnsFalseAndNull()
        {
            var catalog = new FurnitureDefinitionCatalog(new[]
            {
                CreateDefinition("furniture.counter.basic")
            });

            var found = catalog.TryGet("furniture.unknown", out var result);

            Assert.That(found, Is.False);
            Assert.That(result, Is.Null);
        }

        [Test]
        public void Catalog_GetRequiredKnownIdReturnsExactDefinition()
        {
            var definition = CreateDefinition("furniture.counter.basic");
            var catalog = new FurnitureDefinitionCatalog(new[] { definition });

            Assert.That(catalog.GetRequired(definition.Id), Is.SameAs(definition));
        }

        [Test]
        public void Catalog_GetRequiredUnknownIdThrowsWithId()
        {
            var catalog = new FurnitureDefinitionCatalog(Array.Empty<FurnitureDefinition>());

            var exception = Assert.Throws<KeyNotFoundException>(
                () => catalog.GetRequired("furniture.unknown"));

            StringAssert.Contains("furniture.unknown", exception.Message);
        }

        [Test]
        public void Catalog_DuplicateDefinitionIdThrowsWithId()
        {
            var definitions = new[]
            {
                CreateDefinition("furniture.counter.basic"),
                CreateDefinition("furniture.counter.basic")
            };

            var exception = Assert.Throws<ArgumentException>(
                () => new FurnitureDefinitionCatalog(definitions));

            StringAssert.Contains("furniture.counter.basic", exception.Message);
        }

        [Test]
        public void Catalog_NullDefinitionsCollectionThrows()
        {
            Assert.Throws<ArgumentNullException>(
                () => new FurnitureDefinitionCatalog(null));
        }

        [Test]
        public void Catalog_NullDefinitionItemThrows()
        {
            var definitions = new FurnitureDefinition[]
            {
                CreateDefinition("furniture.counter.basic"),
                null
            };

            Assert.Throws<ArgumentException>(
                () => new FurnitureDefinitionCatalog(definitions));
        }

        [Test]
        public void Catalog_DefensivelyCopiesInput()
        {
            var definitions = new List<FurnitureDefinition>
            {
                CreateDefinition("furniture.counter.basic")
            };
            var catalog = new FurnitureDefinitionCatalog(definitions);

            definitions.Clear();
            definitions.Add(CreateDefinition("furniture.table.round"));

            Assert.That(catalog.Definitions.Count, Is.EqualTo(1));
            Assert.That(catalog.Definitions[0].Id, Is.EqualTo("furniture.counter.basic"));
            Assert.That(catalog.TryGet("furniture.table.round", out _), Is.False);
        }

        [Test]
        public void Catalog_DefinitionsCannotBeCastAndMutated()
        {
            var catalog = new FurnitureDefinitionCatalog(new[]
            {
                CreateDefinition("furniture.counter.basic")
            });
            var mutableView = catalog.Definitions as IList<FurnitureDefinition>;

            Assert.That(mutableView, Is.Not.Null);
            Assert.Throws<NotSupportedException>(
                () => mutableView.Add(CreateDefinition("furniture.table.round")));
            Assert.That(catalog.Definitions.Count, Is.EqualTo(1));
            Assert.That(catalog.TryGet("furniture.table.round", out _), Is.False);
        }

        [Test]
        public void Catalog_LookupUsesOrdinalComparisonAcrossCulture()
        {
            var originalCulture = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
                var definition = CreateDefinition("furniture.item.basic");
                var catalog = new FurnitureDefinitionCatalog(new[] { definition });

                Assert.That(catalog.TryGet("furniture.item.basic", out var result), Is.True);
                Assert.That(result, Is.SameAs(definition));
                Assert.That(catalog.TryGet("furniture.item.other", out _), Is.False);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [Test]
        public void Catalog_DefinitionDictionaryUsesOrdinalComparer()
        {
            var catalog = new FurnitureDefinitionCatalog(new[]
            {
                CreateDefinition("furniture.counter.basic")
            });
            var field = typeof(FurnitureDefinitionCatalog).GetField(
                "definitionsById",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, "definitionsById field is required.");
            Assert.That(
                field.FieldType,
                Is.EqualTo(typeof(Dictionary<string, FurnitureDefinition>)),
                "definitionsById must be Dictionary<string, FurnitureDefinition>.");

            var dictionary = field.GetValue(catalog) as Dictionary<string, FurnitureDefinition>;

            Assert.That(
                dictionary,
                Is.Not.Null,
                "definitionsById must contain Dictionary<string, FurnitureDefinition>.");
            Assert.That(
                dictionary.Comparer == StringComparer.Ordinal,
                Is.True,
                "definitionsById must use StringComparer.Ordinal.");
        }

        [TestCase(null, typeof(ArgumentNullException))]
        [TestCase("", typeof(ArgumentException))]
        [TestCase("   ", typeof(ArgumentException))]
        [TestCase("Furniture.Counter", typeof(ArgumentException))]
        public void Catalog_LookupsReuseDefinitionIdValidation(string id, Type expectedExceptionType)
        {
            var catalog = new FurnitureDefinitionCatalog(Array.Empty<FurnitureDefinition>());

            Assert.Throws(expectedExceptionType, () => catalog.TryGet(id, out _));
            Assert.Throws(expectedExceptionType, () => catalog.GetRequired(id));
        }

        [TestCase(32, 32)]
        [TestCase(1, 1024)]
        [TestCase(1024, 1)]
        public void Definition_FootprintAtMaximumCellCountSucceeds(
            int width,
            int height)
        {
            var definition = CreateDefinition(
                $"furniture.max.{width}x{height}",
                width,
                height);

            Assert.That(
                (long)definition.Footprint.Width * definition.Footprint.Height,
                Is.EqualTo(FurnitureDefinition.MaxFootprintCellCount));
        }

        [TestCase(1, 1025)]
        [TestCase(1025, 1)]
        [TestCase(int.MaxValue, 1)]
        [TestCase(int.MaxValue, int.MaxValue)]
        public void Definition_FootprintAboveMaximumCellCountThrows(
            int width,
            int height)
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => CreateDefinition(
                    $"furniture.oversized.{width}x{height}",
                    width,
                    height));

            Assert.That(exception.ParamName, Is.EqualTo("footprint"));
            StringAssert.Contains("1024", exception.Message);
        }

        private static FurnitureDefinition CreateDefinition(string id)
        {
            return CreateDefinition(id, 1, 1);
        }

        private static FurnitureDefinition CreateDefinition(
            string id,
            int width,
            int height)
        {
            return new FurnitureDefinition(
                id,
                "Test Furniture",
                new GridSize(width, height),
                PlacementSurfaceType.Floor);
        }
    }
}

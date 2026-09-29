using System;
using System.Reflection;
using AnimalCafe.Capacity;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase10
{
    public sealed class CapacityRulesTests
    {
        [TestCase(0, 0, 0)]
        [TestCase(1, 0, 0)]
        [TestCase(3, 0, 0)]
        [TestCase(4, 1, 1)]
        [TestCase(7, 1, 1)]
        [TestCase(8, 2, 1)]
        [TestCase(12, 3, 2)]
        [TestCase(20, 5, 3)]
        [TestCase(25, 6, 3)]
        [TestCase(32, 8, 4)]
        [TestCase(48, 12, 6)]
        [TestCase(64, 16, 8)]
        public void E001_DefaultRules_UseSpecifiedWholeNumberLimits(
            int floorCells, int total, int counter)
        {
            var limits = new CapacityRules().CalculateLimits(floorCells);
            AssertLimits(limits, floorCells, total, counter, total);
        }

        [Test]
        public void E002_MaximumFloorCount_DoesNotOverflow()
        {
            AssertLimits(new CapacityRules().CalculateLimits(int.MaxValue),
                int.MaxValue, 536870911, 268435456, 536870911);
            AssertLimits(new CapacityRules(1, 100).CalculateLimits(int.MaxValue),
                int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
        }

        [Test]
        public void E003_CustomRules_UseCeilingAndExposeReadOnlyProperties()
        {
            var rules = new CapacityRules(5, 25);
            Assert.That(rules.CellsPerCustomer, Is.EqualTo(5));
            Assert.That(rules.CounterPercent, Is.EqualTo(25));
            AssertLimits(rules.CalculateLimits(25), 25, 5, 2, 5);
            AssertLimits(new CapacityRules(4, 1).CalculateLimits(4), 4, 1, 1, 1);
            AssertLimits(new CapacityRules(4, 100).CalculateLimits(4), 4, 1, 1, 1);
            Assert.That(typeof(CapacityRules).GetProperty("CellsPerCustomer").SetMethod,
                Is.Null);
            Assert.That(typeof(CapacityRules).GetProperty("CounterPercent").SetMethod,
                Is.Null);
            foreach (var property in typeof(CapacityLimits).GetProperties(
                BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.That(property.SetMethod, Is.Null, property.Name);
            }
        }

        [Test]
        public void E004_NegativeFloorCountIsRejectedWithoutChangingRules()
        {
            var rules = new CapacityRules();
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.CalculateLimits(-1));
            AssertLimits(rules.CalculateLimits(20), 20, 5, 3, 5);
        }

        [TestCase(0, 50)]
        [TestCase(-1, 50)]
        [TestCase(4, 0)]
        [TestCase(4, -1)]
        [TestCase(4, 101)]
        public void E004_InvalidRulesAreRejected(int cellsPerCustomer, int percent)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CapacityRules(cellsPerCustomer, percent));
        }

        private static void AssertLimits(
            CapacityLimits limits, int floor, int total, int counter, int pickup)
        {
            Assert.That(limits.FloorCellCount, Is.EqualTo(floor));
            Assert.That(limits.TotalCustomers, Is.EqualTo(total));
            Assert.That(limits.CounterQueue, Is.EqualTo(counter));
            Assert.That(limits.PickUp, Is.EqualTo(pickup));
        }
    }
}

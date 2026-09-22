using System;
using System.Linq;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class SurfaceSlotDomainTests
    {
        private const string SupportInstanceId = "7f17d8fa59f64be0a6689666ce4a28d2";

        [Test]
        public void SurfaceSlotAddress_SameStablePartsHaveValueEqualityAndHashCode()
        {
            var first = new SurfaceSlotAddress(SupportInstanceId, "slot.0");
            var second = new SurfaceSlotAddress(SupportInstanceId, "slot.0");

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
            Assert.That(first, Is.Not.EqualTo(new SurfaceSlotAddress(
                "8f17d8fa59f64be0a6689666ce4a28d2", "slot.0")));
            Assert.That(first, Is.Not.EqualTo(new SurfaceSlotAddress(SupportInstanceId, "slot.1")));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("Slot.0")]
        public void SurfaceSlotAddress_RejectsInvalidSlotId(string slotId)
        {
            Assert.Throws<ArgumentException>(() =>
                new SurfaceSlotAddress(SupportInstanceId, slotId));
        }

        [Test]
        public void SurfaceSlotAddress_RejectsNullSlotId()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SurfaceSlotAddress(SupportInstanceId, null));
        }

        [Test]
        public void SurfaceSlotAddress_RejectsInvalidSupportInstanceId()
        {
            Assert.Throws<ArgumentException>(() =>
                new SurfaceSlotAddress("not-a-guid", "slot.0"));
        }

        [Test]
        public void StableId_SurfaceMountedAndPickUpFactoriesProduceDistinctValidIds()
        {
            var mountedIds = Enumerable.Range(0, 32)
                .Select(_ => StableId.NewSurfaceMountedInstanceId())
                .ToArray();
            var pickUpIds = Enumerable.Range(0, 32)
                .Select(_ => StableId.NewPickUpPointInstanceId())
                .ToArray();

            Assert.That(mountedIds, Is.Unique);
            Assert.That(pickUpIds, Is.Unique);
            Assert.That(mountedIds.Concat(pickUpIds), Is.Unique);
            Assert.That(mountedIds.All(StableId.IsValidFurnitureInstanceId), Is.True);
            Assert.That(pickUpIds.All(StableId.IsValidFurnitureInstanceId), Is.True);
        }

        [Test]
        public void SurfaceMountedAndPickUpInstances_PreserveOnlyStableDomainSnapshotData()
        {
            var address = new SurfaceSlotAddress(SupportInstanceId, "slot.0");
            var mounted = new SurfaceMountedInstance(
                "9f17d8fa59f64be0a6689666ce4a28d2",
                "equipment.coffee-machine.01",
                address,
                FurnitureRotation.Degrees90);
            var pickUp = new PickUpPointInstance(
                "af17d8fa59f64be0a6689666ce4a28d2",
                address);

            Assert.That(mounted.InstanceId, Is.EqualTo("9f17d8fa59f64be0a6689666ce4a28d2"));
            Assert.That(mounted.DefinitionId, Is.EqualTo("equipment.coffee-machine.01"));
            Assert.That(mounted.Address, Is.EqualTo(address));
            Assert.That(mounted.Rotation, Is.EqualTo(FurnitureRotation.Degrees90));
            Assert.That(pickUp.InstanceId, Is.EqualTo("af17d8fa59f64be0a6689666ce4a28d2"));
            Assert.That(pickUp.Address, Is.EqualTo(address));
        }

        [Test]
        public void SurfaceMountedInstance_RejectsDefaultSurfaceSlotAddress()
        {
            Assert.Throws<ArgumentException>(() => new SurfaceMountedInstance(
                "9f17d8fa59f64be0a6689666ce4a28d2",
                "equipment.coffee-machine.01",
                default,
                FurnitureRotation.Degrees0));
        }

        [Test]
        public void PickUpPointInstance_RejectsDefaultSurfaceSlotAddress()
        {
            Assert.Throws<ArgumentException>(() => new PickUpPointInstance(
                "af17d8fa59f64be0a6689666ce4a28d2",
                default));
        }

        [Test]
        public void SurfaceSlotCatalog_SortsDefinitionsAndSlotsAndRejectsDuplicateKeys()
        {
            var catalog = new SurfaceSlotCatalog(new[]
            {
                new SurfaceSlotDefinition("furniture.work-table.01", "slot.1", new GridPosition(0, 0)),
                new SurfaceSlotDefinition("furniture.counter.long.01", "slot.2", new GridPosition(0, 2)),
                new SurfaceSlotDefinition("furniture.counter.long.01", "slot.0", new GridPosition(0, 0)),
                new SurfaceSlotDefinition("furniture.counter.long.01", "slot.1", new GridPosition(0, 1))
            });

            Assert.That(catalog.GetForSupport("furniture.counter.long.01")
                .Select(slot => slot.SlotId),
                Is.EqualTo(new[] { "slot.0", "slot.1", "slot.2" }));
            Assert.That(catalog.TryGet("furniture.counter.long.01", "slot.1", out var slot), Is.True);
            Assert.That(slot.LocalCell, Is.EqualTo(new GridPosition(0, 1)));
            Assert.That(catalog.TryGet("furniture.counter.long.01", "slot.9", out _), Is.False);

            Assert.Throws<ArgumentException>(() => new SurfaceSlotCatalog(new[]
            {
                new SurfaceSlotDefinition("furniture.counter.long.01", "slot.0", new GridPosition(0, 0)),
                new SurfaceSlotDefinition("furniture.counter.long.01", "slot.0", new GridPosition(0, 1))
            }));
        }
    }
}

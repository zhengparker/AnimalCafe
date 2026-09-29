using System;
using System.Linq;
using AnimalCafe.Capacity;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase10
{
    internal static class CapacityTestSupport
    {
        internal const string FurnitureId = "furniture.counter.basic";

        // 快照指纹比较公开值；token 对象身份由针对性测试另行核对。
        // Fingerprint compares public values; focused tests check token reference identity.
        internal static string Capture(CapacityService service)
        {
            var limits = service.Limits;
            var capacities = string.Join("|", service.GetCapacities().Select(x =>
                $"{x.Kind}:{x.Limit}:{x.Reserved}:{x.Occupied}:{x.Used}:{x.Available}:{x.OverCapacity}"));
            var reservations = string.Join("|", service.GetReservations().Select(x =>
                $"{x.Token.Id}:{x.OwnerId}:{x.Kind}:{x.State}"));
            return $"{limits.FloorCellCount}:{limits.TotalCustomers}:{limits.CounterQueue}:{limits.PickUp};" +
                $"{service.CanAdmit};{capacities};{reservations}";
        }

        internal static void AssertLimits(CapacityService service, int floor,
            int total, int counter, int pickUp)
        {
            Assert.That(service.Limits.FloorCellCount, Is.EqualTo(floor));
            Assert.That(service.Limits.TotalCustomers, Is.EqualTo(total));
            Assert.That(service.Limits.CounterQueue, Is.EqualTo(counter));
            Assert.That(service.Limits.PickUp, Is.EqualTo(pickUp));
        }

        internal static void AssertFailureUnchanged(CapacityService service, string before,
            CapacityResult result, CapacityFailureReason reason)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(reason));
            Assert.That(result.Reservations, Is.Empty);
            Assert.That(Capture(service), Is.EqualTo(before));
        }

        internal static void AssertInvariants(CapacityService service)
        {
            var reservations = service.GetReservations();
            var capacities = service.GetCapacities();
            Assert.That(reservations.Select(x => x.Token.Id).Distinct().Count(),
                Is.EqualTo(reservations.Count));
            Assert.That(reservations.All(x => x.Token.Id > 0), Is.True);
            Assert.That(reservations.Select(x => x.Token.Id), Is.Ordered);
            Assert.That(reservations.Where(x => x.State != ReservationState.Released)
                .GroupBy(x => new { x.OwnerId, x.Kind }).All(x => x.Count() == 1), Is.True);
            foreach (var capacity in capacities)
            {
                var active = reservations.Where(x => x.Kind == capacity.Kind).ToArray();
                var reserved = active.Count(x => x.State == ReservationState.Reserved);
                var occupied = active.Count(x => x.State == ReservationState.Occupied);
                Assert.That(capacity.Reserved, Is.EqualTo(reserved));
                Assert.That(capacity.Occupied, Is.EqualTo(occupied));
                Assert.That(capacity.Used, Is.EqualTo(reserved + occupied));
                Assert.That(capacity.Available, Is.EqualTo(Math.Max(0, capacity.Limit - capacity.Used)));
                Assert.That(capacity.OverCapacity, Is.EqualTo(Math.Max(0, capacity.Used - capacity.Limit)));
            }
            Assert.That(service.CanAdmit, Is.EqualTo(capacities.All(x => x.Available > 0)));
        }

        internal static CafeLayout CreateLayout(LayoutBounds? bounds = null)
        {
            var catalog = new FurnitureDefinitionCatalog(new[]
            {
                new FurnitureDefinition(
                    FurnitureId,
                    "Basic Counter",
                    new GridSize(2, 1),
                    PlacementSurfaceType.Floor)
            });
            var settings = new GridSettings(1f);
            return bounds.HasValue
                ? new CafeLayout(settings, catalog, bounds.Value)
                : new CafeLayout(settings, catalog);
        }

        internal static LayoutRegion Region(
            string id, int x, int y, int width, int height,
            LayoutZoneType zone = LayoutZoneType.Interior)
        {
            return new LayoutRegion(id, new GridPosition(x, y),
                new GridSize(width, height), zone);
        }
    }
}

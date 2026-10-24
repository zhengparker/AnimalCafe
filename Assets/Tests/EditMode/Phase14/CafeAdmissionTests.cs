using System.Linq;
using AnimalCafe.Capacity;
using AnimalCafe.Customers;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase14
{
    public class CafeAdmissionTests
    {
        [Test] public void PaymentDoesNotReleaseCounterUntilDeparture()
        {
            var capacity = new CapacityService(64);
            Assert.That(CustomerAdmission.TryCreate(capacity, "visit", out var admission), Is.True);
            Assert.That(admission.MarkInside(), Is.True);
            Assert.That(admission.MarkQueued(), Is.True);
            var tokens = capacity.GetReservations().Select(r => r.Token).ToArray();
            // Payment/Order creation does not mutate the admission lease.
            Assert.That(capacity.GetCapacities()[1].Occupied, Is.EqualTo(1));
            Assert.That(capacity.GetCapacities()[2].Reserved, Is.EqualTo(1));
            admission.LeaveQueue(); admission.LeaveQueue();
            Assert.That(admission.MarkPickupWaiting(), Is.True);
            Assert.That(capacity.GetCapacities()[1].Used, Is.Zero);
            Assert.That(capacity.GetCapacities()[0].Occupied, Is.EqualTo(1));
            CollectionAssert.AreEqual(tokens, capacity.GetReservations().Select(r => r.Token));
            admission.Dispose();
        }

        [Test] public void CollectionDoesNotReleaseTotalUntilExit()
        {
            var capacity = new CapacityService(64);
            CustomerAdmission.TryCreate(capacity, "visit", out var admission);
            admission.MarkInside(); admission.MarkQueued(); admission.LeaveQueue();
            Assert.That(admission.MarkPickupWaiting(), Is.True);
            Assert.That(capacity.GetCapacities()[2].Occupied, Is.EqualTo(1));
            // Collection retains pickup until actual departure, total until exit.
            Assert.That(capacity.GetCapacities()[0].Occupied, Is.EqualTo(1));
            admission.LeavePickup(); admission.LeavePickup();
            Assert.That(capacity.GetCapacities()[2].Used, Is.Zero);
            Assert.That(admission.MarkPickupWaiting(), Is.False);
            Assert.That(capacity.GetCapacities()[0].Occupied, Is.EqualTo(1));
            admission.Dispose(); admission.Dispose();
            Assert.That(capacity.GetCapacities().All(c => c.Used == 0), Is.True);
        }

        [Test] public void PickupOccupiedOnActualArrival()
        {
            var capacity = new CapacityService(64);
            CustomerAdmission.TryCreate(capacity, "visit", out var admission);
            admission.MarkInside(); admission.MarkQueued();
            Assert.That(capacity.GetCapacities()[2].Reserved, Is.EqualTo(1));
            Assert.That(capacity.GetCapacities()[2].Occupied, Is.Zero);
            Assert.That(admission.MarkPickupWaiting(), Is.True);
            Assert.That(admission.MarkPickupWaiting(), Is.True);
            Assert.That(capacity.GetCapacities()[2].Reserved, Is.Zero);
            Assert.That(capacity.GetCapacities()[2].Occupied, Is.EqualTo(1));
            admission.Dispose();
            Assert.That(admission.MarkPickupWaiting(), Is.False);
        }

        [Test] public void IntegratedTransferNeverCancelsPickupReservation()
        {
            var capacity = new CapacityService(64);
            CustomerAdmission.TryCreate(capacity, "first", out var first);
            CustomerAdmission.TryCreate(capacity, "other", out var other);
            first.MarkInside(); first.MarkQueued(); first.LeaveQueue();
            var before = capacity.GetReservations().Where(r => r.OwnerId == "other").ToArray();
            Assert.That(first.MarkPickupWaiting(), Is.True);
            first.LeavePickup();
            CollectionAssert.AreEqual(before.Select(r => r.Token),
                capacity.GetReservations().Where(r => r.OwnerId == "other").Select(r => r.Token));
            Assert.That(capacity.GetCapacities()[2].Reserved, Is.EqualTo(1));
            Assert.That(other.MarkPickupWaiting(), Is.True);
            first.Dispose(); other.Dispose();
        }

        [Test] public void CancelledPickupCannotBeReoccupiedAndDoesNotAffectAnotherOwner()
        {
            var capacity = new CapacityService(64);
            CustomerAdmission.TryCreate(capacity, "first", out var first);
            CustomerAdmission.TryCreate(capacity, "other", out var other);
            first.CancelUnusedPickup();
            Assert.That(first.MarkPickupWaiting(), Is.False);
            Assert.That(other.MarkPickupWaiting(), Is.True);
            first.LeavePickup();
            Assert.That(capacity.GetCapacities()[2].Occupied, Is.EqualTo(1));
            first.Dispose(); other.Dispose();
        }
    }
}

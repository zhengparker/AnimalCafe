using NUnit.Framework;
using AnimalCafe.Customers;
using AnimalCafe.Capacity;

namespace AnimalCafe.Tests.EditMode.Phase12
{
    public class CustomerAdmissionTests
    {
        [Test] public void FailedPreparationRollsBackAllThreeReservationsExactlyOnce()
        {
            var capacity=new CapacityService(32);
            Assert.That(CustomerAdmission.TryCreate(capacity,"a",out var lease),Is.True);
            Assert.That(capacity.GetCapacities()[0].Reserved,Is.EqualTo(1));
            lease.Dispose(); lease.Dispose();
            foreach(var ledger in capacity.GetCapacities())
            { Assert.That(ledger.Reserved,Is.Zero); Assert.That(ledger.Occupied,Is.Zero); }
        }
        [Test] public void FullAdmissionDoesNotCreatePartialLease()
        {
            var capacity=new CapacityService(4);
            Assert.That(CustomerAdmission.TryCreate(capacity,"a",out var first),Is.True);
            Assert.That(CustomerAdmission.TryCreate(capacity,"b",out var second),Is.False);
            Assert.That(second,Is.Null); Assert.That(capacity.GetReservations().Count,Is.EqualTo(3)); first.Dispose();
        }
        [Test] public void QueueLeaveDoesNotReleaseTotalBeforeRealExit()
        {
            var capacity=new CapacityService(32);
            Assert.That(CustomerAdmission.TryCreate(capacity,"a",out var lease),Is.True);
            Assert.That(lease.MarkInside(),Is.True); Assert.That(lease.MarkQueued(),Is.True);
            lease.CancelUnusedPickup(); lease.LeaveQueue(); lease.LeaveQueue();
            Assert.That(capacity.GetCapacities()[0].Occupied,Is.EqualTo(1));
            Assert.That(capacity.GetCapacities()[1].Occupied,Is.Zero);
            Assert.That(capacity.GetCapacities()[2].Reserved,Is.Zero);
            lease.Dispose(); Assert.That(capacity.GetCapacities()[0].Occupied,Is.Zero);
        }
        [Test] public void SameOwnerCannotReserveTwiceOrReuseDisposedLease()
        {
            var capacity=new CapacityService(32);
            Assert.That(CustomerAdmission.TryCreate(capacity,"a",out var lease),Is.True);
            Assert.That(CustomerAdmission.TryCreate(capacity,"a",out _),Is.False);
            lease.Dispose(); Assert.That(lease.MarkInside(),Is.False);
        }
    }
}

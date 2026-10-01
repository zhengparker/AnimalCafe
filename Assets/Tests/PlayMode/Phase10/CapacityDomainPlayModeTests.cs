using System.Collections;
using System.Linq;
using AnimalCafe.Capacity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode.Phase10
{
    public sealed class CapacityDomainPlayModeTests
    {
        private float originalTimeScale;

        [SetUp]
        public void RememberTimeScale() => originalTimeScale = Time.timeScale;

        [TearDown]
        public void RestoreTimeScale() => Time.timeScale = originalTimeScale;

        [UnityTest]
        public IEnumerator P001_AdmissionOccupyShrinkAndReleaseSurviveFrames()
        {
            var service = new CapacityService(8);
            var held = service.TryReserveAdmission("V1").Reservations;
            Assert.That(held.Select(x => x.Token.Id), Is.EqualTo(new long[] { 1, 2, 3 }));
            yield return null;
            Assert.That(service.GetReservations().All(x => x.State == ReservationState.Reserved), Is.True);
            Assert.That(service.Occupy(held[0].Token, "V1").Succeeded, Is.True);
            Assert.That(service.Occupy(held[1].Token, "V1").Succeeded, Is.True);
            Assert.That(service.UpdateFloorCellCount(0).Succeeded, Is.True);
            yield return null;
            Assert.That(service.GetCapacities().Select(x => x.Used), Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(service.GetCapacities().All(x => x.OverCapacity == 1), Is.True);
            Assert.That(service.Occupy(held[2].Token, "V1").Succeeded, Is.True);
            Assert.That(service.TryReserveAdmission("V2").FailureReason,
                Is.EqualTo(CapacityFailureReason.CapacityOverLimit));
            foreach (var reservation in held)
                Assert.That(service.Release(reservation.Token, "V1").Succeeded, Is.True);
            yield return null;
            Assert.That(service.GetCapacities().All(x => x.Used == 0), Is.True);
            Assert.That(service.GetReservations().All(x => x.State == ReservationState.Released), Is.True);
            Assert.That(service.UpdateFloorCellCount(8).Succeeded, Is.True);
            Assert.That(service.GetReservations().Count, Is.EqualTo(3));
            Assert.That(service.TryReserveAdmission("V2").Reservations[0].Token.Id, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator P002_PauseNormalAndDoubleSpeedDoNotChangeCapacityWithoutCommands()
        {
            var service = new CapacityService(8);
            var held = service.TryReserveAdmission("V1").Reservations;
            foreach (var speed in new[] { 0f, 1f, 2f })
            {
                Time.timeScale = speed;
                var countBeforeWait = service.GetReservations().Count;
                for (var frame = 0; frame < 3; frame++) yield return null;
                yield return new WaitForSecondsRealtime(0.1f);
                var afterWait = service.GetReservations();
                Assert.That(afterWait.Count, Is.EqualTo(countBeforeWait));
                for (var i = 0; i < held.Count; i++)
                {
                    Assert.That(afterWait[i].Token, Is.SameAs(held[i].Token));
                    Assert.That(afterWait[i].Token.Id, Is.EqualTo(i + 1));
                    Assert.That(afterWait[i].OwnerId, Is.EqualTo("V1"));
                    Assert.That(afterWait[i].Kind, Is.EqualTo((CapacityKind)i));
                    Assert.That(afterWait[i].State, Is.EqualTo(ReservationState.Reserved));
                }
                Assert.That(service.GetCapacities().Select(x => x.Used),
                    Is.EqualTo(new[] { 1, 1, 1 }));
                // 显式命令在各速度下执行 / explicit commands work at each speed.
                var probeOwner = "probe-" + speed;
                var probe = service.TryReserve(probeOwner,
                    new[] { CapacityKind.TotalCustomers });
                Assert.That(probe.Succeeded, Is.True);
                var probeToken = probe.Reservations[0].Token;
                Assert.That(service.Release(probeToken, probeOwner)
                    .Succeeded, Is.True);
                var releasedProbe = service.GetReservations().Single(x => x.Token.Id == probeToken.Id);
                Assert.That(releasedProbe.Token, Is.SameAs(probeToken));
                Assert.That(releasedProbe.OwnerId, Is.EqualTo(probeOwner));
                Assert.That(releasedProbe.Kind, Is.EqualTo(CapacityKind.TotalCustomers));
                Assert.That(releasedProbe.State, Is.EqualTo(ReservationState.Released));
                Assert.That(service.GetCapacities().Select(x => x.Used),
                    Is.EqualTo(new[] { 1, 1, 1 }));
            }
            Assert.That(service.Release(held[0].Token, "V1").Succeeded, Is.True);
        }

        [UnityTest]
        public IEnumerator P003_NewServiceDoesNotInheritOldTokensAcrossFrames()
        {
            var first = new CapacityService(8);
            var oldToken = first.TryReserveAdmission("V1").Reservations[0].Token;
            yield return null;
            var second = new CapacityService(8);
            Assert.That(second.GetReservations(), Is.Empty);
            Assert.That(second.GetCapacities().All(x => x.Used == 0), Is.True);
            Assert.That(second.Occupy(oldToken, "V1").FailureReason,
                Is.EqualTo(CapacityFailureReason.InvalidToken));
            yield return null;
            Assert.That(second.TryReserveAdmission("V1").Reservations[0].Token.Id, Is.EqualTo(1));
            Assert.That(first.GetReservations()[0].Token, Is.SameAs(oldToken));
        }
    }
}

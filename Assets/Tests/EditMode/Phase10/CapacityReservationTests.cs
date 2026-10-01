using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Capacity;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase10
{
    public sealed class CapacityReservationTests
    {
        [Test]
        public void E011_NewServiceStartsEmptyAndRejectsInvalidConstructorInputs()
        {
            var service = new CapacityService(16);
            Assert.That(service.Limits.FloorCellCount, Is.EqualTo(16));
            Assert.That(service.CanAdmit, Is.True);
            Assert.That(service.GetReservations(), Is.Empty);
            AssertCapacity(service, CapacityKind.TotalCustomers, 4, 0);
            AssertCapacity(service, CapacityKind.CounterQueue, 2, 0);
            AssertCapacity(service, CapacityKind.PickUp, 4, 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => new CapacityService(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CapacityService(16, firstTokenId: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CapacityService(16, firstTokenId: -1));
        }

        [Test]
        public void E012_AdmissionReservesThreeSortedTokensWithoutOccupying()
        {
            var service = new CapacityService(16);
            var result = service.TryReserveAdmission("V1");
            AssertSuccess(result, CapacityKind.TotalCustomers, CapacityKind.CounterQueue, CapacityKind.PickUp);
            Assert.That(result.Reservations.Select(x => x.Token.Id), Is.EqualTo(new long[] { 1, 2, 3 }));
            Assert.That(result.Reservations.All(x => x.OwnerId == "V1" &&
                x.State == ReservationState.Reserved), Is.True);
            Assert.That(service.GetReservations().Select(x => x.Token),
                Is.EqualTo(result.Reservations.Select(x => x.Token)));
            foreach (var capacity in service.GetCapacities())
            {
                Assert.That(capacity.Reserved, Is.EqualTo(1));
                Assert.That(capacity.Occupied, Is.Zero);
                Assert.That(capacity.Used, Is.EqualTo(1));
            }
        }

        [TestCase(new[] { CapacityKind.PickUp }, new[] { CapacityKind.PickUp })]
        [TestCase(new[] { CapacityKind.PickUp, CapacityKind.TotalCustomers },
            new[] { CapacityKind.TotalCustomers, CapacityKind.PickUp })]
        [TestCase(new[] { CapacityKind.PickUp, CapacityKind.CounterQueue,
            CapacityKind.TotalCustomers }, new[] { CapacityKind.TotalCustomers,
            CapacityKind.CounterQueue, CapacityKind.PickUp })]
        public void E013_BatchInputOrderDoesNotChangeTokenOrder(
            CapacityKind[] requested, CapacityKind[] expected)
        {
            var service = new CapacityService(16);
            var result = service.TryReserve("V1", requested);
            AssertSuccess(result, expected);
            Assert.That(result.Reservations.Select(x => x.Token.Id),
                Is.EqualTo(Enumerable.Range(1, expected.Length).Select(x => (long)x)));
            foreach (var capacity in service.GetCapacities())
            {
                Assert.That(capacity.Used, Is.EqualTo(expected.Contains(capacity.Kind) ? 1 : 0));
                Assert.That(capacity.Used, Is.EqualTo(capacity.Reserved + capacity.Occupied));
            }
        }

        [Test]
        public void E014_OwnerIdsAreOrdinalAndPreserveOriginalText()
        {
            var service = new CapacityService(24);
            foreach (var owner in new[] { "V1", "v1", "访客-1" })
            {
                AssertSuccess(service.TryReserve(owner, new[] { CapacityKind.CounterQueue }),
                    CapacityKind.CounterQueue);
            }
            Assert.That(service.GetReservations().Select(x => x.OwnerId),
                Is.EqualTo(new[] { "V1", "v1", "访客-1" }));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("\t")]
        [TestCase(" V1")]
        [TestCase("V1 ")]
        public void E015_InvalidOwnerDoesNotWriteOrConsumeId(string owner)
        {
            var service = new CapacityService(16);
            var before = CapacityTestSupport.Capture(service);
            AssertFailure(service.TryReserveAdmission(owner), CapacityFailureReason.InvalidOwnerId);
            Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
            Assert.That(service.TryReserve("probe", new[] { CapacityKind.TotalCustomers })
                .Reservations.Single().Token.Id, Is.EqualTo(1));
        }

        [Test]
        public void E016_InvalidBatchesAreAtomicAndDoNotConsumeId()
        {
            IReadOnlyList<CapacityKind>[] invalid =
            {
                null,
                Array.Empty<CapacityKind>(),
                new[] { CapacityKind.TotalCustomers, CapacityKind.TotalCustomers },
                new[] { (CapacityKind)123 },
                new[] { CapacityKind.TotalCustomers, (CapacityKind)123 },
                new[] { (CapacityKind)123, CapacityKind.TotalCustomers }
            };
            foreach (var kinds in invalid)
            {
                var service = new CapacityService(16);
                var before = CapacityTestSupport.Capture(service);
                AssertFailure(service.TryReserve("V1", kinds), CapacityFailureReason.InvalidKinds);
                Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
                Assert.That(service.TryReserve("V1", new[] { CapacityKind.PickUp })
                    .Reservations.Single().Token.Id, Is.EqualTo(1));
            }
        }

        [Test]
        public void E017_ExistingKindRejectsWholeBatchButOtherKindCanBeReserved()
        {
            var service = new CapacityService(16);
            AssertSuccess(service.TryReserve("V1", new[] { CapacityKind.CounterQueue }),
                CapacityKind.CounterQueue);
            var before = CapacityTestSupport.Capture(service);
            AssertFailure(service.TryReserve("V1", new[]
                { CapacityKind.TotalCustomers, CapacityKind.CounterQueue }),
                CapacityFailureReason.OwnerAlreadyReserved);
            Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
            var other = service.TryReserve("V1", new[] { CapacityKind.TotalCustomers });
            AssertSuccess(other, CapacityKind.TotalCustomers);
            Assert.That(other.Reservations.Single().Token.Id, Is.EqualTo(2));
        }

        [Test]
        public void E018_CounterFullRejectsWholeAdmissionAndLeavesNextId()
        {
            var service = new CapacityService(16);
            AssertSuccess(service.TryReserveAdmission("V1"), CapacityKind.TotalCustomers,
                CapacityKind.CounterQueue, CapacityKind.PickUp);
            AssertSuccess(service.TryReserveAdmission("V2"), CapacityKind.TotalCustomers,
                CapacityKind.CounterQueue, CapacityKind.PickUp);
            var before = CapacityTestSupport.Capture(service);
            AssertFailure(service.TryReserveAdmission("V3"),
                CapacityFailureReason.InsufficientCapacity);
            Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
            Assert.That(service.TryReserve("V3", new[] { CapacityKind.TotalCustomers })
                .Reservations.Single().Token.Id, Is.EqualTo(7));
        }

        [TestCase(CapacityKind.PickUp)]
        [TestCase(CapacityKind.TotalCustomers)]
        public void E019_E020_SingleFullKindRejectsAdmissionWithoutPartialWrite(CapacityKind kind)
        {
            var service = new CapacityService(8);
            AssertSuccess(service.TryReserve("V1", new[] { kind }), kind);
            AssertSuccess(service.TryReserve("V2", new[] { kind }), kind);
            var before = CapacityTestSupport.Capture(service);
            AssertFailure(service.TryReserveAdmission("V3"),
                CapacityFailureReason.InsufficientCapacity);
            Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
            var otherKind = kind == CapacityKind.PickUp
                ? CapacityKind.TotalCustomers : CapacityKind.PickUp;
            Assert.That(service.TryReserve("V3", new[] { otherKind })
                .Reservations.Single().Token.Id, Is.EqualTo(3));
        }

        [Test]
        public void E020_ZeroFloorRejectsEverySingleKind()
        {
            var service = new CapacityService(0);
            foreach (CapacityKind kind in Enum.GetValues(typeof(CapacityKind)))
            {
                AssertFailure(service.TryReserve("V1", new[] { kind }),
                    CapacityFailureReason.InsufficientCapacity);
            }
            Assert.That(service.GetReservations(), Is.Empty);
            Assert.That(service.CanAdmit, Is.False);
        }

        [Test]
        public void E021_LastThreeIdsCanBeIssuedWithoutWraparound()
        {
            var service = new CapacityService(16, firstTokenId: long.MaxValue - 2);
            var result = service.TryReserveAdmission("V1");
            AssertSuccess(result, CapacityKind.TotalCustomers, CapacityKind.CounterQueue,
                CapacityKind.PickUp);
            Assert.That(result.Reservations.Select(x => x.Token.Id),
                Is.EqualTo(new[] { long.MaxValue - 2, long.MaxValue - 1, long.MaxValue }));
            var before = CapacityTestSupport.Capture(service);
            AssertFailure(service.TryReserve("V2", new[] { CapacityKind.TotalCustomers }),
                CapacityFailureReason.TokenIdExhausted);
            Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
        }

        [Test]
        public void E021_InsufficientIdsRejectsWholeBatchWithoutUsingEitherId()
        {
            var service = new CapacityService(16, firstTokenId: long.MaxValue - 1);
            var before = CapacityTestSupport.Capture(service);
            AssertFailure(service.TryReserveAdmission("V1"),
                CapacityFailureReason.TokenIdExhausted);
            Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
            var result = service.TryReserve("V1", new[]
                { CapacityKind.PickUp, CapacityKind.TotalCustomers });
            AssertSuccess(result, CapacityKind.TotalCustomers, CapacityKind.PickUp);
            Assert.That(result.Reservations.Select(x => x.Token.Id),
                Is.EqualTo(new[] { long.MaxValue - 1, long.MaxValue }));
        }

        [Test]
        public void E022_InvalidRequestThenValidRequestDoesNotLoseOwnerOrId()
        {
            var service = new CapacityService(16);
            AssertFailure(service.TryReserve("V1", new[] { (CapacityKind)123 }),
                CapacityFailureReason.InvalidKinds);
            var result = service.TryReserveAdmission("V1");
            AssertSuccess(result, CapacityKind.TotalCustomers, CapacityKind.CounterQueue,
                CapacityKind.PickUp);
            Assert.That(result.Reservations.Select(x => x.Token.Id),
                Is.EqualTo(new long[] { 1, 2, 3 }));
        }

        [Test]
        public void E022_SequentialRequestsForLastSlotAllowOnlyFirst()
        {
            var service = new CapacityService(4);
            AssertSuccess(service.TryReserveAdmission("V1"), CapacityKind.TotalCustomers,
                CapacityKind.CounterQueue, CapacityKind.PickUp);
            var before = CapacityTestSupport.Capture(service);
            AssertFailure(service.TryReserveAdmission("V2"),
                CapacityFailureReason.InsufficientCapacity);
            Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
        }

        [Test]
        public void E023_InputAndResultRemainStableAndTokenHasNoPublicConstructor()
        {
            var service = new CapacityService(16);
            var input = new List<CapacityKind> { CapacityKind.PickUp };
            var first = service.TryReserve("V1", input);
            AssertSuccess(first, CapacityKind.PickUp);
            input[0] = CapacityKind.CounterQueue;
            var second = service.TryReserve("V2", input);
            AssertSuccess(second, CapacityKind.CounterQueue);
            Assert.That(first.Reservations.Single().Kind, Is.EqualTo(CapacityKind.PickUp));
            Assert.That(service.GetReservations().Select(x => x.Kind),
                Is.EqualTo(new[] { CapacityKind.PickUp, CapacityKind.CounterQueue }));
            Assert.That(typeof(CapacityToken).GetConstructors(), Is.Empty);
            foreach (var type in new[] { typeof(CapacityToken),
                typeof(ReservationSnapshot), typeof(CapacitySnapshot) })
            {
                foreach (var property in type.GetProperties(BindingFlags.Public |
                    BindingFlags.Instance))
                {
                    Assert.That(property.SetMethod, Is.Null, type.Name + "." + property.Name);
                }
            }
        }

        [Test]
        public void E023_DifferentServicesDoNotShareTokenIdsOrState()
        {
            var first = new CapacityService(16);
            var second = new CapacityService(16);
            var a = first.TryReserve("V1", new[] { CapacityKind.PickUp });
            var b = second.TryReserve("V1", new[] { CapacityKind.PickUp });
            Assert.That(a.Reservations.Single().Token.Id, Is.EqualTo(1));
            Assert.That(b.Reservations.Single().Token.Id, Is.EqualTo(1));
            Assert.That(a.Reservations.Single().Token,
                Is.Not.SameAs(b.Reservations.Single().Token));
            Assert.That(first.GetReservations().Single().Token,
                Is.SameAs(a.Reservations.Single().Token));
            Assert.That(second.GetReservations().Single().Token,
                Is.SameAs(b.Reservations.Single().Token));
        }

        [Test]
        public void E017_OccupiedStillBlocksDuplicateButReleasedAllowsNewToken()
        {
            var service = new CapacityService(16);
            var first = service.TryReserve("V1", new[] { CapacityKind.CounterQueue })
                .Reservations.Single().Token;
            Assert.That(service.Occupy(first, "V1").Succeeded, Is.True);
            var before = CapacityTestSupport.Capture(service);
            AssertFailure(service.TryReserve("V1", new[] { CapacityKind.CounterQueue }),
                CapacityFailureReason.OwnerAlreadyReserved);
            Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
            Assert.That(service.Release(first, "V1").Succeeded, Is.True);
            var second = service.TryReserve("V1", new[] { CapacityKind.CounterQueue });
            AssertSuccess(second, CapacityKind.CounterQueue);
            Assert.That(second.Reservations.Single().Token.Id, Is.EqualTo(2));
        }

        [Test]
        public void E018_ReleasingCounterRecoversAtomicAdmission()
        {
            var service = new CapacityService(16);
            var first = service.TryReserveAdmission("V1").Reservations;
            service.TryReserveAdmission("V2");
            var before = CapacityTestSupport.Capture(service);
            AssertFailure(service.TryReserveAdmission("V3"),
                CapacityFailureReason.InsufficientCapacity);
            Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
            Assert.That(service.Release(first[1].Token, "V1").Succeeded, Is.True);
            var recovered = service.TryReserveAdmission("V3");
            AssertSuccess(recovered, CapacityKind.TotalCustomers,
                CapacityKind.CounterQueue, CapacityKind.PickUp);
            Assert.That(recovered.Reservations.Select(x => x.Token.Id),
                Is.EqualTo(new long[] { 7, 8, 9 }));
            Assert.That(service.GetCapacities().Select(x => x.Used),
                Is.EqualTo(new[] { 3, 2, 3 }));
        }

        [TestCase(CapacityKind.PickUp)]
        [TestCase(CapacityKind.TotalCustomers)]
        public void E019_E020_ReleasingSingleFullKindRecoversAdmission(CapacityKind kind)
        {
            var service = new CapacityService(8);
            var first = service.TryReserve("V1", new[] { kind }).Reservations.Single().Token;
            service.TryReserve("V2", new[] { kind });
            var before = CapacityTestSupport.Capture(service);
            AssertFailure(service.TryReserveAdmission("V3"),
                CapacityFailureReason.InsufficientCapacity);
            Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
            Assert.That(service.Release(first, "V1").Succeeded, Is.True);
            var recovered = service.TryReserveAdmission("V3");
            AssertSuccess(recovered, CapacityKind.TotalCustomers,
                CapacityKind.CounterQueue, CapacityKind.PickUp);
            Assert.That(recovered.Reservations.Select(x => x.Token.Id),
                Is.EqualTo(new long[] { 3, 4, 5 }));
        }

        [Test]
        public void E022_CapacityFailureThenReleaseAndRetryPreservesNextId()
        {
            var service = new CapacityService(4);
            var first = service.TryReserveAdmission("V1").Reservations;
            AssertFailure(service.TryReserveAdmission("V2"),
                CapacityFailureReason.InsufficientCapacity);
            foreach (var reservation in first)
            {
                Assert.That(service.Release(reservation.Token, "V1").Succeeded, Is.True);
            }
            var recovered = service.TryReserveAdmission("V2");
            AssertSuccess(recovered, CapacityKind.TotalCustomers,
                CapacityKind.CounterQueue, CapacityKind.PickUp);
            Assert.That(recovered.Reservations.Select(x => x.Token.Id),
                Is.EqualTo(new long[] { 4, 5, 6 }));
        }

        [Test]
        public void E035_CompoundReserveErrorsFollowValidationPriority()
        {
            var service = new CapacityService(4, firstTokenId: long.MaxValue);
            var first = service.TryReserve("V1", new[] { CapacityKind.CounterQueue });
            AssertSuccess(first, CapacityKind.CounterQueue);
            var before = CapacityTestSupport.Capture(service);
            AssertFailure(service.TryReserve("V1", new[]
                { CapacityKind.CounterQueue, (CapacityKind)99 }),
                CapacityFailureReason.InvalidKinds);
            AssertFailure(service.TryReserve("V1", new[] { CapacityKind.CounterQueue }),
                CapacityFailureReason.OwnerAlreadyReserved);
            AssertFailure(service.TryReserve("V2", new[] { CapacityKind.CounterQueue }),
                CapacityFailureReason.InsufficientCapacity);
            Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
        }

        private static void AssertSuccess(CapacityResult result, params CapacityKind[] kinds)
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.FailureReason, Is.EqualTo(CapacityFailureReason.None));
            Assert.That(result.Reservations.Select(x => x.Kind), Is.EqualTo(kinds));
        }

        private static void AssertFailure(CapacityResult result, CapacityFailureReason reason)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(reason));
            Assert.That(result.Reservations, Is.Empty);
        }

        private static void AssertCapacity(CapacityService service, CapacityKind kind,
            int limit, int used)
        {
            var snapshot = service.GetCapacities().Single(x => x.Kind == kind);
            Assert.That(snapshot.Limit, Is.EqualTo(limit));
            Assert.That(snapshot.Used, Is.EqualTo(used));
            Assert.That(snapshot.Available, Is.EqualTo(limit - used));
            Assert.That(snapshot.OverCapacity, Is.Zero);
        }
    }
}

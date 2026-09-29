using System;
using System.Linq;
using AnimalCafe.Capacity;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase10
{
    public sealed class CapacityInvariantTests
    {
        [Test]
        public void E035_E039_OveragePriorityFollowsInputAndDuplicateOwnerChecks()
        {
            var service = new CapacityService(32);
            var owner = service.TryReserve("V1", new[] { CapacityKind.CounterQueue });
            for (var i = 2; i <= 4; i++)
                Assert.That(service.TryReserve($"V{i}", new[] { CapacityKind.CounterQueue })
                    .Succeeded, Is.True);
            Assert.That(service.UpdateFloorCellCount(16).Succeeded, Is.True);
            var before = CapacityTestSupport.Capture(service);
            CapacityTestSupport.AssertFailureUnchanged(service, before,
                service.TryReserve(" bad", new[] { CapacityKind.TotalCustomers }),
                CapacityFailureReason.InvalidOwnerId);
            CapacityTestSupport.AssertFailureUnchanged(service, before,
                service.TryReserve("V5", new[] { CapacityKind.TotalCustomers,
                    CapacityKind.TotalCustomers }), CapacityFailureReason.InvalidKinds);
            CapacityTestSupport.AssertFailureUnchanged(service, before,
                service.TryReserve("V1", new[] { CapacityKind.CounterQueue }),
                CapacityFailureReason.OwnerAlreadyReserved);
            CapacityTestSupport.AssertFailureUnchanged(service, before,
                service.TryReserve("V5", new[] { CapacityKind.TotalCustomers }),
                CapacityFailureReason.CapacityOverLimit);
            Assert.That(owner.Reservations.Single().Token,
                Is.SameAs(service.GetReservations().First().Token));
        }

        [Test]
        public void E044_Seed17TwoHundredPublicOperationsPreserveInvariants()
        {
            const int seed = 17;
            var random = new Random(seed);
            var service = new CapacityService(16);
            var foreign = new CapacityService(16);
            var foreignToken = foreign.TryReserve("foreign", new[] { CapacityKind.PickUp })
                .Reservations.Single().Token;
            for (var step = 0; step < 200; step++)
            {
                var action = random.Next(7);
                var owner = $"V{random.Next(1, 10)}";
                var kind = (CapacityKind)random.Next(3);
                var records = service.GetReservations();
                var selected = records.Count == 0 ? null : records[random.Next(records.Count)];
                var command = $"action={action} owner={owner} kind={kind} token={selected?.Token.Id}";
                try
                {
                    var before = CapacityTestSupport.Capture(service);
                    var beforeCapacities = service.GetCapacities();
                    CapacityResult result;
                    switch (action)
                    {
                        case 0:
                            result = service.TryReserve(owner, new[] { kind });
                            break;
                        case 1:
                            result = service.TryReserveAdmission(owner);
                            break;
                        case 2:
                            result = service.Occupy(selected?.Token, selected?.OwnerId ?? owner);
                            break;
                        case 3:
                            result = service.Release(selected?.Token, selected?.OwnerId ?? owner);
                            break;
                        case 4:
                            result = service.Occupy(selected?.Token, "wrong-owner");
                            break;
                        case 5:
                            result = service.Release(foreignToken, owner);
                            break;
                        default:
                            var floor = random.Next(5) == 0 ? -1 : random.Next(0, 37);
                            command += $" floor={floor}";
                            result = service.UpdateFloorCellCount(floor);
                            break;
                    }
                    if (!result.Succeeded)
                    {
                        Assert.That(result.FailureReason, Is.Not.EqualTo(CapacityFailureReason.None));
                        Assert.That(result.Reservations, Is.Empty);
                        Assert.That(CapacityTestSupport.Capture(service), Is.EqualTo(before));
                    }
                    else
                    {
                        Assert.That(result.FailureReason, Is.EqualTo(CapacityFailureReason.None));
                        if (action == 0 || action == 1)
                        {
                            Assert.That(beforeCapacities.All(x => x.OverCapacity == 0), Is.True);
                            Assert.That(service.GetCapacities().All(x => x.OverCapacity == 0), Is.True);
                        }
                        if (action == 2)
                        {
                            Assert.That(service.GetCapacities().Select(x => x.Used),
                                Is.EqualTo(beforeCapacities.Select(x => x.Used)));
                        }
                    }
                    CapacityTestSupport.AssertInvariants(service);
                }
                catch (AssertionException ex)
                {
                    throw new AssertionException($"seed={seed} step={step} {command}: {ex.Message}");
                }
            }
        }

        [Test]
        public void E047_RepeatedQueriesAndOtherServicesNeverChangeThisService()
        {
            var first = new CapacityService(16);
            var second = new CapacityService(16);
            var token = first.TryReserveAdmission("V1").Reservations[0].Token;
            var before = CapacityTestSupport.Capture(first);
            for (var i = 0; i < 100; i++)
            {
                first.GetReservations();
                first.GetCapacities();
                _ = first.CanAdmit;
                var other = second.TryReserve($"V{i}", new[] { CapacityKind.TotalCustomers });
                Assert.That(other.Succeeded, Is.True);
                Assert.That(second.Release(other.Reservations.Single().Token, $"V{i}")
                    .Succeeded, Is.True);
            }
            Assert.That(CapacityTestSupport.Capture(first), Is.EqualTo(before));
            Assert.That(first.GetReservations()[0].Token, Is.SameAs(token));
            Assert.That(second.TryReserve("V1", new[] { CapacityKind.PickUp }).Succeeded, Is.True);
            Assert.That(CapacityTestSupport.Capture(first), Is.EqualTo(before));
        }

        [Test]
        public void E048_CanAdmitChecksAllKindsButDoesNotPromiseOwnerOrTokenSuccess()
        {
            foreach (var kind in new[] { CapacityKind.TotalCustomers,
                CapacityKind.CounterQueue, CapacityKind.PickUp })
            {
                var service = new CapacityService(16);
                var limit = service.GetCapacities()[(int)kind].Limit;
                for (var i = 0; i < limit; i++)
                    Assert.That(service.TryReserve($"V{i}", new[] { kind }).Succeeded, Is.True);
                Assert.That(service.CanAdmit, Is.False, $"full kind={kind}");
            }

            var available = new CapacityService(16);
            foreach (var kind in new[] { CapacityKind.TotalCustomers,
                CapacityKind.CounterQueue, CapacityKind.PickUp })
            {
                var limit = available.GetCapacities()[(int)kind].Limit;
                for (var i = 1; i < limit; i++)
                    Assert.That(available.TryReserve($"{kind}-{i}", new[] { kind })
                        .Succeeded, Is.True);
            }
            Assert.That(available.CanAdmit, Is.True);
            var before = CapacityTestSupport.Capture(available);
            CapacityTestSupport.AssertFailureUnchanged(available, before,
                available.TryReserve("TotalCustomers-1", new[] { CapacityKind.TotalCustomers }),
                CapacityFailureReason.OwnerAlreadyReserved);

            var exhausted = new CapacityService(16, firstTokenId: long.MaxValue);
            Assert.That(exhausted.CanAdmit, Is.True);
            var exhaustedBefore = CapacityTestSupport.Capture(exhausted);
            CapacityTestSupport.AssertFailureUnchanged(exhausted, exhaustedBefore,
                exhausted.TryReserveAdmission("V1"), CapacityFailureReason.TokenIdExhausted);

            var over = new CapacityService(32);
            for (var i = 1; i <= 2; i++)
                Assert.That(over.TryReserveAdmission($"V{i}").Succeeded, Is.True);
            over.UpdateFloorCellCount(16);
            Assert.That(over.CanAdmit, Is.False);
            Assert.That(over.GetCapacities().All(x => x.OverCapacity == 0), Is.True);
            over.UpdateFloorCellCount(4);
            Assert.That(over.CanAdmit, Is.False);
            Assert.That(over.GetCapacities().All(x => x.OverCapacity > 0), Is.True);
        }
    }
}

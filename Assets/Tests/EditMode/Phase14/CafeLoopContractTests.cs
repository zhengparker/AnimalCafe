using System;
using System.Collections.Generic;
using AnimalCafe.CafeLoop;
using AnimalCafe.Employees;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase14
{
    public sealed class CafeLoopContractTests
    {
        [Test]
        public void OrderKeySeparatesEpochAndSession()
        {
            // 相同数字ID不能命中旧批次 / Epoch and session prevent stale identity reuse.
            var values = new Dictionary<CafeOrderKey, string>
            {
                [new CafeOrderKey("s", 1, 1)] = "old",
                [new CafeOrderKey("s", 2, 1)] = "new",
                [new CafeOrderKey("other", 1, 1)] = "other session"
            };
            Assert.That(values.Count, Is.EqualTo(3));
            Assert.That(values[new CafeOrderKey("s", 2, 1)], Is.EqualTo("new"));
        }

        [Test]
        public void OldAttemptCannotMatchRestoredTask()
        {
            var old = new CafeTaskToken("s", 1, 7, 1);
            var restored = new CafeTaskToken("s", 1, 7, 2);
            var tokens = new HashSet<CafeTaskToken> { old, restored };
            Assert.That(tokens.Count, Is.EqualTo(2));
            Assert.That(tokens.Contains(new CafeTaskToken("s", 1, 7, 2)), Is.True);
            Assert.That(tokens.Contains(new CafeTaskToken("s", 2, 7, 2)), Is.False);
        }

        [TestCase(null)] [TestCase("")] [TestCase(" ")] [TestCase(" s")]
        public void InvalidSessionCannotBecomeBusinessIdentity(string session)
        {
            Assert.Throws<ArgumentException>(() => new CafeOrderKey(session, 1, 1));
            Assert.Throws<ArgumentException>(() => new CafeTaskToken(session, 1, 1, 1));
        }

        [TestCase(0)] [TestCase(-1)]
        public void NonpositiveEpochOrIdCannotBecomeBusinessIdentity(long value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CafeOrderKey("s", value, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CafeOrderKey("s", 1, value));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CafeTaskToken("s", 1, value, 1));
        }

        [TestCase(0)] [TestCase(-1)]
        public void NonpositiveAttemptCannotBecomeTaskIdentity(int attempt)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CafeTaskToken("s", 1, 1, attempt));
        }

        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void CheckpointRejectsInvalidRemainingTime(float remaining)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CafeWorkCheckpoint(
                EmployeeRole.Cashier, "visit1", null, CafeWorkStage.Working, remaining));
        }

        [Test]
        public void PreparedCheckpointCannotRestartWorkingTime()
        {
            var order = new CafeOrderKey("s", 1, 1);
            Assert.Throws<ArgumentException>(() => new CafeWorkCheckpoint(
                EmployeeRole.Barista, "visit1", order, CafeWorkStage.Prepared, 3f));
            var saved = new CafeWorkCheckpoint(EmployeeRole.Barista, "visit1", order,
                CafeWorkStage.Prepared, 0f);
            Assert.That(saved.RemainingWorkSeconds, Is.Zero);
        }

        [Test]
        public void SnapshotsCannotMutateOwnerState()
        {
            var paid = new List<string> { "visit1" };
            var snapshot = new CafeLoopSnapshot("s", 1, paidVisitIds: paid);
            paid.Add("visit2");
            Assert.That(snapshot.PaidVisitIds, Is.EqualTo(new[] { "visit1" }));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<string>)snapshot.PaidVisitIds).Add("intruder"));
        }

        [Test]
        public void SnapshotDoesNotRetainMutableDeliverySource()
        {
            var deliveries = new List<CafeDeliverySnapshot>();
            var snapshot = new CafeLoopSnapshot("s", 1, delivery: deliveries);
            deliveries.Add(default);
            Assert.That(snapshot.Delivery, Is.Empty);
        }
    }
}

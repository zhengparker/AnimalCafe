using System;
using NUnit.Framework;
using AnimalCafe.Customers;

namespace AnimalCafe.Tests.EditMode.Phase12
{
    public class CustomerSpawnClockTests
    {
        [TestCase(0f, 1f)] [TestCase(.5f, 3f)] [TestCase(1f, 5f)]
        public void UniformIntervalHasExactApprovedBounds(float random, float seconds)
        {
            var clock = new CustomerSpawnClock(() => random);
            Assert.That(clock.Tick(0, true, false), Is.False);
            Assert.That(clock.RemainingSeconds, Is.EqualTo(seconds));
            Assert.That(clock.Tick(seconds - .1f, true, false), Is.False);
            Assert.That(clock.Tick(.101f, true, false), Is.True);
        }
        [Test] public void LongFrameDoesNotCatchUp()
        {
            var calls = 0; var clock = new CustomerSpawnClock(() => { calls++; return 0; });
            Assert.That(clock.Tick(30, true, false), Is.True);
            Assert.That(clock.RemainingSeconds, Is.Zero);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(clock.Tick(0, true, false), Is.False);
            CompleteAdmission(clock);
            Assert.That(clock.RemainingSeconds, Is.EqualTo(1));
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(clock.Tick(0, true, false), Is.False);
        }
        [Test] public void PausePreservesRemainingButBlockedRecoveryDrawsNewInterval()
        {
            var calls = 0; var clock = new CustomerSpawnClock(() => { calls++; return .5f; });
            clock.Tick(1, true, false);
            clock.Tick(0, true, false);
            Assert.That(clock.RemainingSeconds, Is.EqualTo(2));
            clock.Tick(100, false, false);
            Assert.That(clock.Tick(0, true, false), Is.False);
            Assert.That(clock.RemainingSeconds, Is.EqualTo(3));
            Assert.That(calls, Is.EqualTo(2));
            clock.Tick(10, true, true);
            clock.Tick(0, true, false);
            Assert.That(calls, Is.EqualTo(3));
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(-1f)]
        public void InvalidDeltaDoesNotMutateClock(float delta)
        {
            var clock = new CustomerSpawnClock(() => 0);
            clock.Tick(0, true, false);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Tick(delta, true, false));
            Assert.That(clock.RemainingSeconds, Is.EqualTo(1));
        }
        [TestCase(-.1f)] [TestCase(1.1f)] [TestCase(float.NaN)]
        public void InvalidRandomIsRejected(float random)
        {
            var clock = new CustomerSpawnClock(() => random);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Tick(0, true, false));
        }
        [Test] public void FailedAttemptKeepsOneDueArrivalUntilAdmissionSucceeds()
        {
            var calls=0; var clock=new CustomerSpawnClock(()=> { calls++; return 0; });
            Assert.That(clock.Tick(1,true,false),Is.True); // caller attempts admission and rejects it
            Assert.That(clock.RemainingSeconds,Is.Zero);
            Assert.That(clock.Tick(0,true,false),Is.False);
            for(var attempt=0;attempt<4;attempt++) Assert.That(clock.Tick(.01f,true,false),Is.True);
            Assert.That(calls,Is.EqualTo(1),"A blocked arrival must not draw new random intervals");
            CompleteAdmission(clock);
            Assert.That(clock.RemainingSeconds,Is.EqualTo(1));
            Assert.That(calls,Is.EqualTo(2));
            Assert.That(clock.Tick(.99f,true,false),Is.False);
            Assert.That(clock.Tick(.011f,true,false),Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void FullCapacityOrDecorationClearsDueArrivalWithoutBacklog(bool decorating)
        {
            var clock=new CustomerSpawnClock(()=>.25f);
            Assert.That(clock.Tick(2,true,false),Is.True);
            Assert.That(clock.RemainingSeconds,Is.Zero);
            Assert.That(clock.Tick(30,decorating,decorating),Is.False);
            Assert.That(clock.Tick(0,true,false),Is.False);
            Assert.That(clock.RemainingSeconds,Is.EqualTo(2));
        }
        private static void CompleteAdmission(CustomerSpawnClock clock)
        {
            clock.CompleteAdmission();
        }
    }
}


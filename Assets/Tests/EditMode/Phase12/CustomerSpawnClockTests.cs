using System;
using NUnit.Framework;
using AnimalCafe.Customers;

namespace AnimalCafe.Tests.EditMode.Phase12
{
    public class CustomerSpawnClockTests
    {
        [TestCase(0f, 0f)] [TestCase(.5f, 5f)] [TestCase(1f, 10f)]
        public void UniformIntervalHasExactApprovedBounds(float random, float seconds)
        {
            var clock = new CustomerSpawnClock(() => random);
            Assert.That(clock.Tick(0, true, false), Is.False);
            Assert.That(clock.RemainingSeconds, Is.EqualTo(seconds));
            Assert.That(clock.Tick(Math.Max(0, seconds - .1f), true, false), Is.False);
            Assert.That(clock.Tick(.101f, true, false), Is.True);
        }
        [Test] public void ZeroIntervalWaitsForPositiveTimeAndRetainsOnlyOneDueArrival()
        {
            var calls=0;var clock=new CustomerSpawnClock(()=>{calls++;return 0;});
            Assert.That(clock.Tick(0,true,false),Is.False);
            Assert.That(clock.RemainingSeconds,Is.Zero);
            Assert.That(calls,Is.EqualTo(1));
            Assert.That(clock.Tick(.016f,true,false),Is.True);
            Assert.That(clock.Tick(0,true,false),Is.False,"暂停不能释放零秒来客 / Pause keeps the due arrival");
            Assert.That(clock.Tick(.016f,true,false),Is.True);
            Assert.That(calls,Is.EqualTo(1),"拒绝后只保留同一位，不重抽 / No backlog or redraw");
            clock.CompleteAdmission();
            Assert.That(calls,Is.EqualTo(2));
            Assert.That(clock.RemainingSeconds,Is.Zero);
            Assert.That(clock.Tick(0,true,false),Is.False);
            Assert.That(clock.Tick(.016f,true,false),Is.True);
            Assert.That(clock.Tick(30,false,false),Is.False,"容量不足时不放行 / Capacity gate");
            Assert.That(clock.Tick(30,true,true),Is.False,"装修中不放行 / Decoration gate");
            Assert.That(calls,Is.EqualTo(2));
            Assert.That(clock.Tick(0,true,false),Is.False);
            Assert.That(calls,Is.EqualTo(3),"恢复后只开启一轮 / Restart one interval");
        }

        [Test] public void LongFrameDoesNotCatchUp()
        {
            var calls = 0; var clock = new CustomerSpawnClock(() => { calls++; return 0; });
            Assert.That(clock.Tick(30, true, false), Is.True);
            Assert.That(clock.RemainingSeconds, Is.Zero);
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(clock.Tick(0, true, false), Is.False);
            CompleteAdmission(clock);
            Assert.That(clock.RemainingSeconds, Is.Zero);
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(clock.Tick(0, true, false), Is.False);
        }
        [Test] public void PausePreservesRemainingButBlockedRecoveryDrawsNewInterval()
        {
            var calls = 0; var clock = new CustomerSpawnClock(() => { calls++; return .5f; });
            clock.Tick(1, true, false);
            clock.Tick(0, true, false);
            Assert.That(clock.RemainingSeconds, Is.EqualTo(4));
            clock.Tick(100, false, false);
            Assert.That(clock.Tick(0, true, false), Is.False);
            Assert.That(clock.RemainingSeconds, Is.EqualTo(5));
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
            Assert.That(clock.RemainingSeconds, Is.Zero);
        }
        [TestCase(-.1f)] [TestCase(1.1f)] [TestCase(float.NaN)]
        public void InvalidRandomIsRejected(float random)
        {
            var clock = new CustomerSpawnClock(() => random);
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Tick(0, true, false));
        }
        [Test] public void FailedAttemptKeepsOneDueArrivalUntilAdmissionSucceeds()
        {
            var calls=0; var clock=new CustomerSpawnClock(()=> { calls++; return calls==1?0f:.2f; });
            Assert.That(clock.Tick(1,true,false),Is.True); // caller attempts admission and rejects it
            Assert.That(clock.RemainingSeconds,Is.Zero);
            Assert.That(clock.Tick(0,true,false),Is.False);
            for(var attempt=0;attempt<4;attempt++) Assert.That(clock.Tick(.01f,true,false),Is.True);
            Assert.That(calls,Is.EqualTo(1),"A blocked arrival must not draw new random intervals");
            CompleteAdmission(clock);
            Assert.That(clock.RemainingSeconds,Is.EqualTo(2));
            Assert.That(calls,Is.EqualTo(2));
            Assert.That(clock.Tick(1.99f,true,false),Is.False);
            Assert.That(clock.Tick(.011f,true,false),Is.True);
        }
        [TestCase(false)] [TestCase(true)]
        public void FullCapacityOrDecorationClearsDueArrivalWithoutBacklog(bool decorating)
        {
            var clock=new CustomerSpawnClock(()=>.2f);
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


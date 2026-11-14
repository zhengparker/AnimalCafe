using System;
using System.Reflection;
using AnimalCafe.BusinessDay;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase15
{
    public class BusinessDayServiceTests
    {
        private static BusinessDayService Create() => new BusinessDayService(new BusinessDaySettings(8, 2), 0);
        private static BusinessDayDrainResult Drained => new BusinessDayDrainResult(true, "");
        private static BusinessDayDrainResult Pending => new BusinessDayDrainResult(false, "顾客尚未离店");
        private static BusinessDayService Resting(long dayNumber = 1)
        {
            var day = new BusinessDayService(new BusinessDaySettings(8, 2), 0, dayNumber);
            day.BeforeService(8); day.AfterService(9, Drained);
            return day;
        }

        [Test] public void DefaultsUseEightHoursAndTwoHours()
        {
            var day = new BusinessDayService(new BusinessDaySettings(), 0);
            Assert.That(day.CanAdmit(28799), Is.True);
            Assert.That(day.CanAdmit(28800), Is.False);
            day.BeforeService(28800); day.AfterService(28810, Drained);
            Assert.That(day.GetSnapshot(28810).RemainingSeconds, Is.EqualTo(7200));
        }

        [Test] public void CutoffIsExclusiveForAdmission()
        {
            var day = Create();
            Assert.That(day.CanAdmit(7.999), Is.True);
            Assert.That(day.CanAdmit(8), Is.False);
            Assert.That(day.CanAdmit(100), Is.False);
            Assert.That(day.GetSnapshot(8).Phase, Is.EqualTo(BusinessDayPhase.Open));
            Assert.That(day.GetSnapshot(8).RemainingSeconds, Is.Zero);
            day.BeforeService(8); day.BeforeService(8);
            Assert.That(day.GetSnapshot(8).Phase, Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(day.GetSnapshot(8).TransitionSequence, Is.EqualTo(1));
        }

        [Test] public void ClosingWaitsForDrainAndStartsRestAtActualObservation()
        {
            var day = Create(); day.BeforeService(8); day.AfterService(9, Pending);
            Assert.That(day.GetSnapshot(9).Phase, Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(day.GetSnapshot(9).DrainReason, Is.EqualTo("顾客尚未离店"));
            day.BeforeService(1000000);
            Assert.That(day.GetSnapshot(1000000).Phase, Is.EqualTo(BusinessDayPhase.Closing));
            day.AfterService(1000000, Drained);
            var snapshot = day.GetSnapshot(1000000);
            Assert.That(snapshot.RemainingSeconds, Is.EqualTo(2));
            Assert.That(snapshot.LastClosedDay.EndKind, Is.EqualTo(BusinessDayEndKind.CompletedDrain));
            Assert.That(snapshot.TransitionSequence, Is.EqualTo(2));
            day.AfterService(1000001, Drained);
            Assert.That(day.GetSnapshot(1000001).RemainingSeconds, Is.EqualTo(1));
        }

        [Test] public void RestReopensWithoutButtonAndIncrementsDayOnce()
        {
            var day = Resting();
            Assert.That(day.CanAdmit(10), Is.False);
            day.BeforeService(100); day.BeforeService(100); day.AfterService(100, Drained);
            var snapshot = day.GetSnapshot(100);
            Assert.That(snapshot.Phase, Is.EqualTo(BusinessDayPhase.Open));
            Assert.That(snapshot.DayNumber, Is.EqualTo(2));
            Assert.That(snapshot.RemainingSeconds, Is.EqualTo(8));
            Assert.That(snapshot.TransitionSequence, Is.EqualTo(3));
            Assert.That(day.CanAdmit(107.999), Is.True);
            Assert.That(day.CanAdmit(108), Is.False);
        }

        [Test] public void LayoutVersionIsRecordedOnceAndLastCloseSurvivesNextOpen()
        {
            var day = Create(); day.RecordLayoutInterruption(7); day.RecordLayoutInterruption(7);
            day.RecordLayoutInterruption(6); day.RecordLayoutInterruption(9);
            Assert.That(day.GetSnapshot(0).LayoutInterruptionCount, Is.EqualTo(2));
            day.BeforeService(8); day.AfterService(9, Drained);
            var closed = day.GetSnapshot(9).LastClosedDay;
            Assert.That(closed.DayNumber, Is.EqualTo(1));
            Assert.That(closed.EndKind, Is.EqualTo(BusinessDayEndKind.LayoutInterrupted));
            Assert.That(closed.LayoutInterruptionCount, Is.EqualTo(2));
            day.BeforeService(11); day.RecordLayoutInterruption(9); day.RecordLayoutInterruption(8);
            Assert.That(day.GetSnapshot(11).LayoutInterruptionCount, Is.Zero);
            Assert.That(day.GetSnapshot(11).LastClosedDay, Is.SameAs(closed));
            day.RecordLayoutInterruption(10);
            Assert.That(day.GetSnapshot(11).LayoutInterruptionCount, Is.EqualTo(1));
            Assert.That(closed.LayoutInterruptionCount, Is.EqualTo(2));
        }

        [Test] public void ClosingKeepsBlockedReasonUntilTrueDrain()
        {
            var day = Create(); day.BeforeService(800); day.AfterService(800, Pending);
            day.BeforeService(90000000);
            var snapshot = day.GetSnapshot(90000000);
            Assert.That(snapshot.Phase, Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(snapshot.DrainReason, Is.EqualTo("顾客尚未离店"));
            Assert.That(snapshot.LastClosedDay, Is.Null);
        }

        [Test] public void RestDecorationRetiresVersionWithoutChangingEitherDay()
        {
            var day = Resting(); var closed = day.GetSnapshot(9).LastClosedDay;
            day.RecordLayoutInterruption(12);
            Assert.That(day.GetSnapshot(9).LayoutInterruptionCount, Is.Zero,
                "Rest没有活动服务，不能追改已结束结果或预记下一天");
            Assert.That(day.GetSnapshot(9).LastClosedDay, Is.SameAs(closed));
            Assert.That(closed.EndKind, Is.EqualTo(BusinessDayEndKind.CompletedDrain));
            day.BeforeService(11); day.RecordLayoutInterruption(12);
            Assert.That(day.GetSnapshot(11).LayoutInterruptionCount, Is.Zero, "Rest版本跨日仍应退休");
            day.RecordLayoutInterruption(13);
            Assert.That(day.GetSnapshot(11).LayoutInterruptionCount, Is.EqualTo(1));
        }

        [Test] public void QueryDoesNotAdvanceTimeOrMutateFrozenSnapshot()
        {
            var day = Create(); var first = day.GetSnapshot(0);
            Assert.That(first.RemainingSeconds, Is.EqualTo(8));
            Assert.That(day.CanAdmit(100), Is.False);
            Assert.That(day.GetSnapshot(100).TransitionSequence, Is.Zero);
            day.BeforeService(1); day.RecordLayoutInterruption(1);
            Assert.That(day.CanAdmit(1), Is.True);
            Assert.That(first.LayoutInterruptionCount, Is.Zero);
            Assert.That(first.RemainingSeconds, Is.EqualTo(8));
        }

        [Test] public void DrainWhileOpenCannotCloseEarly()
        {
            var day = Create(); day.AfterService(1, Drained);
            Assert.That(day.CanAdmit(1), Is.True);
            Assert.That(day.GetSnapshot(1).LastClosedDay, Is.Null);
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)]
        public void SettingsRejectNonPositiveOrNonFinite(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BusinessDaySettings(value, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BusinessDaySettings(8, value));
        }

        [TestCase(-1)] [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)] [TestCase(double.NegativeInfinity)]
        public void InvalidObservationIsRejectedByEveryEntryWithoutMutation(double value)
        {
            var day = Create();
            Assert.Throws<ArgumentOutOfRangeException>(() => new BusinessDayService(new BusinessDaySettings(), value));
            Assert.Throws<ArgumentOutOfRangeException>(() => day.BeforeService(value));
            Assert.Throws<ArgumentOutOfRangeException>(() => day.AfterService(value, Drained));
            Assert.Throws<ArgumentOutOfRangeException>(() => day.CanAdmit(value));
            Assert.Throws<ArgumentOutOfRangeException>(() => day.GetSnapshot(value));
            Assert.That(day.CanAdmit(0), Is.True);
            Assert.That(day.GetSnapshot(0).TransitionSequence, Is.Zero);
        }

        [Test] public void RollbackIsRejectedByMutatorsAndQueriesWithoutMutation()
        {
            var day = Create(); day.BeforeService(4);
            Assert.Throws<ArgumentOutOfRangeException>(() => day.BeforeService(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => day.AfterService(3, Drained));
            Assert.Throws<ArgumentOutOfRangeException>(() => day.CanAdmit(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => day.GetSnapshot(3));
            Assert.That(day.GetSnapshot(4).RemainingSeconds, Is.EqualTo(4));
        }

        [TestCase(null)] [TestCase("")] [TestCase("  ")]
        public void NonDrainedResultRequiresReason(string reason)
        { Assert.Throws<ArgumentException>(() => new BusinessDayDrainResult(false, reason)); }

        [Test] public void DefaultDrainCannotProveSuccessOrAdvanceObservation()
        {
            var day = Create(); day.BeforeService(8);
            Assert.Throws<ArgumentException>(() => day.AfterService(9, default));
            Assert.That(day.GetSnapshot(8).Phase, Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(day.GetSnapshot(8).LastClosedDay, Is.Null);
        }

        [TestCase(0)] [TestCase(-1)]
        public void InvalidInitialDayIsRejected(long value)
        { Assert.Throws<ArgumentOutOfRangeException>(() => new BusinessDayService(new BusinessDaySettings(), 0, value)); }

        [Test] public void NullSettingsAreRejected()
        { Assert.Throws<ArgumentNullException>(() => new BusinessDayService(null, 0)); }

        [TestCase(0)] [TestCase(-1)]
        public void InvalidLayoutVersionDoesNotChangeState(long value)
        {
            var day = Create();
            Assert.Throws<ArgumentOutOfRangeException>(() => day.RecordLayoutInterruption(value));
            Assert.That(day.GetSnapshot(0).LayoutInterruptionCount, Is.Zero);
            day.RecordLayoutInterruption(1);
            Assert.That(day.GetSnapshot(0).LayoutInterruptionCount, Is.EqualTo(1));
        }

        [Test] public void DayOverflowPreservesRestAndLastObservation()
        {
            var day = Resting(long.MaxValue); var previous = day.GetSnapshot(9);
            Assert.Throws<OverflowException>(() => day.BeforeService(11));
            Assert.That(day.GetSnapshot(9).Phase, Is.EqualTo(BusinessDayPhase.Rest));
            Assert.That(day.GetSnapshot(9).DayNumber, Is.EqualTo(long.MaxValue));
            Assert.That(day.GetSnapshot(9).TransitionSequence, Is.EqualTo(previous.TransitionSequence));
        }

        [Test] public void SequenceOverflowPreservesOpenAndLastObservation()
        {
            var day = Create(); SetCounter(day, "transitionSequence", long.MaxValue);
            Assert.Throws<OverflowException>(() => day.BeforeService(8));
            Assert.That(day.GetSnapshot(0).Phase, Is.EqualTo(BusinessDayPhase.Open));
            Assert.That(day.GetSnapshot(0).TransitionSequence, Is.EqualTo(long.MaxValue));
        }

        [Test] public void SequenceOverflowPreservesClosingWhenDrainIsObserved()
        {
            var day = Create(); day.BeforeService(8); day.AfterService(8, Pending);
            SetCounter(day, "transitionSequence", long.MaxValue);
            Assert.Throws<OverflowException>(() => day.AfterService(9, Drained));
            var snapshot = day.GetSnapshot(8);
            Assert.That(snapshot.Phase, Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(snapshot.DrainReason, Is.EqualTo("顾客尚未离店"));
            Assert.That(snapshot.LastClosedDay, Is.Null);
        }

        [Test] public void DeadlineOverflowOrUnrepresentableDurationIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new BusinessDayService(new BusinessDaySettings(double.MaxValue, 2), double.MaxValue));
            var day = new BusinessDayService(new BusinessDaySettings(8, double.MaxValue), 0);
            day.BeforeService(8);
            Assert.Throws<ArgumentOutOfRangeException>(() => day.AfterService(double.MaxValue, Drained));
            Assert.That(day.GetSnapshot(8).Phase, Is.EqualTo(BusinessDayPhase.Closing));
        }

        // 极限计数无法实际循环到达；reflection只用于制造边界，不给生产代码加test-only API。
        private static void SetCounter(BusinessDayService day, string name, long value)
        {
            var field = typeof(BusinessDayService).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "边界测试需要真实计数字段"); field.SetValue(day, value);
        }
    }
}

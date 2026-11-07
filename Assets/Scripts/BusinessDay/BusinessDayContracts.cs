using System;

namespace AnimalCafe.BusinessDay
{
    public enum BusinessDayPhase { Open, Closing, Rest }
    public enum BusinessDayEndKind { CompletedDrain, LayoutInterrupted }

    public sealed class BusinessDaySettings
    {
        public double OpenSeconds { get; }
        public double RestSeconds { get; }
        public BusinessDaySettings(double openSeconds = 28800, double restSeconds = 7200)
        {
            if (double.IsNaN(openSeconds) || double.IsInfinity(openSeconds) || openSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(openSeconds), "营业时长必须是有限正数。");
            if (double.IsNaN(restSeconds) || double.IsInfinity(restSeconds) || restSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(restSeconds), "休息时长必须是有限正数。");
            OpenSeconds = openSeconds; RestSeconds = restSeconds;
        }
    }

    public readonly struct BusinessDayDrainResult
    {
        public bool IsDrained { get; }
        public string Reason { get; }
        public BusinessDayDrainResult(bool isDrained, string reason)
        {
            if (!isDrained && string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("尚未排空时必须提供原因。", nameof(reason));
            IsDrained = isDrained; Reason = reason ?? string.Empty;
        }
    }

    public sealed class BusinessDayClosedDay
    {
        public long DayNumber { get; }
        public BusinessDayEndKind EndKind { get; }
        public long LayoutInterruptionCount { get; }
        internal BusinessDayClosedDay(long dayNumber, BusinessDayEndKind endKind, long count)
        { DayNumber = dayNumber; EndKind = endKind; LayoutInterruptionCount = count; }
    }

    public sealed class BusinessDaySnapshot
    {
        public long DayNumber { get; }
        public BusinessDayPhase Phase { get; }
        public double RemainingSeconds { get; }
        public long TransitionSequence { get; }
        public long LayoutInterruptionCount { get; }
        public BusinessDayClosedDay LastClosedDay { get; }
        public string DrainReason { get; }
        internal BusinessDaySnapshot(long dayNumber, BusinessDayPhase phase, double remainingSeconds,
            long transitionSequence, long count, BusinessDayClosedDay lastClosedDay, string drainReason)
        {
            DayNumber = dayNumber; Phase = phase; RemainingSeconds = remainingSeconds;
            TransitionSequence = transitionSequence; LayoutInterruptionCount = count;
            LastClosedDay = lastClosedDay; DrainReason = drainReason;
        }
    }
}

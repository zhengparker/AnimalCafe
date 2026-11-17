using System;

namespace AnimalCafe.BusinessDay
{
    public sealed class BusinessDayService
    {
        private readonly BusinessDaySettings settings;
        private double lastObservedSeconds;
        private double deadlineSeconds;
        private long dayNumber;
        private long transitionSequence;
        private long layoutInterruptionCount;
        private long lastLayoutVersion;
        private BusinessDayPhase phase;
        private BusinessDayClosedDay lastClosedDay;
        private string drainReason = string.Empty;

        public BusinessDayService(BusinessDaySettings settings, double nowSeconds, long initialDay = 1)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            ValidateTime(nowSeconds);
            if (initialDay <= 0) throw new ArgumentOutOfRangeException(nameof(initialDay));
            var deadline = Deadline(nowSeconds, settings.OpenSeconds);
            this.settings = settings;
            dayNumber = initialDay;
            lastObservedSeconds = nowSeconds;
            deadlineSeconds = deadline;
            phase = BusinessDayPhase.Open;
        }

        public void BeforeService(double nowSeconds)
        {
            ValidateObservation(nowSeconds);
            if (phase == BusinessDayPhase.Open && nowSeconds >= deadlineSeconds)
            {
                // 先检查所有可能失败的计算，再一次写入，避免溢出后留下半个transition。
                var nextSequence = checked(transitionSequence + 1);
                phase = BusinessDayPhase.Closing;
                transitionSequence = nextSequence;
                drainReason = "等待服务排空";
            }
            else if (phase == BusinessDayPhase.Rest && nowSeconds >= deadlineSeconds)
            {
                var nextDay = checked(dayNumber + 1);
                var nextSequence = checked(transitionSequence + 1);
                var nextDeadline = Deadline(nowSeconds, settings.OpenSeconds);
                // 从实际观察时刻开新一天；长帧不循环补发错过的营业日。
                dayNumber = nextDay;
                transitionSequence = nextSequence;
                deadlineSeconds = nextDeadline;
                phase = BusinessDayPhase.Open;
                layoutInterruptionCount = 0;
                drainReason = string.Empty;
            }
            lastObservedSeconds = nowSeconds;
        }

        public void AfterService(double nowSeconds, BusinessDayDrainResult drain)
        {
            ValidateObservation(nowSeconds);
            // default(struct)没有明确原因，不能当成有效的排空证明。
            if (!drain.IsDrained && string.IsNullOrWhiteSpace(drain.Reason))
                throw new ArgumentException("尚未排空时必须提供原因。", nameof(drain));
            if (phase == BusinessDayPhase.Closing)
            {
                if (drain.IsDrained)
                {
                    var nextSequence = checked(transitionSequence + 1);
                    var nextDeadline = Deadline(nowSeconds, settings.RestSeconds);
                    var endKind = layoutInterruptionCount > 0
                        ? BusinessDayEndKind.LayoutInterrupted : BusinessDayEndKind.CompletedDrain;
                    var closedDay = new BusinessDayClosedDay(dayNumber, endKind, layoutInterruptionCount);
                    // Rest只从真实drain观察开始，不回填到营业截止时刻。
                    transitionSequence = nextSequence;
                    deadlineSeconds = nextDeadline;
                    lastClosedDay = closedDay;
                    phase = BusinessDayPhase.Rest;
                    drainReason = string.Empty;
                }
                else drainReason = drain.Reason;
            }
            lastObservedSeconds = nowSeconds;
        }

        public bool CanAdmit(double nowSeconds)
        {
            ValidateObservation(nowSeconds);
            // driver尚未推进时也按真实截止拒绝；query不修改Phase或观测时间。
            return phase == BusinessDayPhase.Open && nowSeconds < deadlineSeconds;
        }

        public BusinessDaySnapshot GetSnapshot(double nowSeconds)
        {
            ValidateObservation(nowSeconds);
            var remaining = phase == BusinessDayPhase.Closing ? 0 : Math.Max(0, deadlineSeconds - nowSeconds);
            return new BusinessDaySnapshot(dayNumber, phase, remaining, transitionSequence,
                layoutInterruptionCount, lastClosedDay, drainReason);
        }

        public void RecordLayoutInterruption(long layoutVersion)
        {
            if (layoutVersion <= 0) throw new ArgumentOutOfRangeException(nameof(layoutVersion));
            // version跨日保留；重复或较旧callback不会给新一天重复记账。
            if (layoutVersion <= lastLayoutVersion) return;
            // Rest期间没有活动服务可中断：只退休版本，既不追改LastClosedDay，也不预记下一天。
            if (phase == BusinessDayPhase.Rest) { lastLayoutVersion = layoutVersion; return; }
            var nextCount = checked(layoutInterruptionCount + 1);
            layoutInterruptionCount = nextCount;
            lastLayoutVersion = layoutVersion;
        }

        private void ValidateObservation(double nowSeconds)
        {
            ValidateTime(nowSeconds);
            if (nowSeconds < lastObservedSeconds)
                throw new ArgumentOutOfRangeException(nameof(nowSeconds), "观测时间不能回退。");
        }

        private static void ValidateTime(double nowSeconds)
        {
            if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds) || nowSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(nowSeconds), "时间必须是有限非负数。");
        }

        private static double Deadline(double nowSeconds, double duration)
        {
            var deadline = nowSeconds + duration;
            if (double.IsInfinity(deadline) || deadline <= nowSeconds)
                throw new ArgumentOutOfRangeException(nameof(nowSeconds), "截止时间溢出或时长无法表示。");
            return deadline;
        }
    }
}

using System;
namespace AnimalCafe.Customers
{
    public sealed class CustomerSpawnClock
    {
        private readonly Func<float> random;
        private bool armed;
        public CustomerSpawnClock(Func<float> nextUnitRandom)
        { random = nextUnitRandom ?? throw new ArgumentNullException(nameof(nextUnitRandom)); }
        public float RemainingSeconds { get; private set; }
        public bool Tick(float scaledDeltaTime, bool canAdmit, bool decorating)
        {
            if (float.IsNaN(scaledDeltaTime) || float.IsInfinity(scaledDeltaTime) || scaledDeltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(scaledDeltaTime));
            if (!canAdmit || decorating) { armed=false; RemainingSeconds=0; return false; }
            if (!armed) { RemainingSeconds=Draw(); armed=true; }
            if (scaledDeltaTime==0) return false; // 暂停时不释放已经到期的顾客。
            if (scaledDeltaTime < RemainingSeconds) { RemainingSeconds-=scaledDeltaTime; return false; }
            // 仅保留一位到期顾客；失败不重新抽签，不累计补发 / one pending arrival, no backlog.
            RemainingSeconds=0;
            return true;
        }
        public void CompleteAdmission()
        {
            if (!armed || RemainingSeconds>0) return;
            // 接纳成功后才开始下一轮，长帧剩余时间不补发。
            RemainingSeconds=Draw();
        }
        private float Draw()
        {
            var value=random();
            if (float.IsNaN(value) || float.IsInfinity(value) || value<0 || value>1)
                throw new ArgumentOutOfRangeException("random");
            return 1+4*value;
        }
    }
}

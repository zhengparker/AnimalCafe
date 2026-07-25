namespace AnimalCafe.Core.Time
{
    /// <summary>
    /// 供 gameplay systems 使用的统一时间控制 contract。
    /// Shared time-control contract for gameplay systems.
    /// </summary>
    public interface IGameTimeService
    {
        GameSpeed CurrentSpeed { get; }

        bool TrySetSpeed(GameSpeed speed);
    }
}

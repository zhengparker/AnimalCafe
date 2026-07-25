namespace AnimalCafe.Core.Time
{
    /// <summary>
    /// 游戏允许使用的速度。数值同时代表 Unity time scale。
    /// Supported game speeds; each value maps to Unity's time scale.
    /// </summary>
    public enum GameSpeed
    {
        Paused = 0,
        Normal = 1,
        Fast = 2
    }
}

using System;
using AnimalCafe.Core.Time;

namespace AnimalCafe.Core.Events
{
    /// <summary>
    /// Phase 0 的小型跨系统 event bus。
    /// Small cross-system event bus for Phase 0.
    /// </summary>
    public static class GameEventBus
    {
        public static event Action<SelectionChangedEvent> SelectionChanged;

        public static event Action<GameSpeedChangedEvent> GameSpeedChanged;

        public static void PublishSelectionChanged(
            UnityEngine.Object previous,
            UnityEngine.Object current)
        {
            SelectionChanged?.Invoke(new SelectionChangedEvent(previous, current));
        }

        public static void PublishGameSpeedChanged(GameSpeed previous, GameSpeed current)
        {
            GameSpeedChanged?.Invoke(new GameSpeedChangedEvent(previous, current));
        }

        public static void ResetForTests()
        {
            SelectionChanged = null;
            GameSpeedChanged = null;
        }
    }
}

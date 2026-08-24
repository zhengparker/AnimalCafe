using AnimalCafe.Core.Events;
using UnityEngine;

namespace AnimalCafe.Core.Time
{
    /// <summary>
    /// 集中管理 Pause、1x 和 2x。
    /// Central owner for Pause, 1x, and 2x game time.
    /// </summary>
    public sealed class GameTimeService : MonoBehaviour, IGameTimeService
    {
        private static GameTimeService activeOwner;
        private GameSpeed lastRunningSpeed = GameSpeed.Normal;

        public GameSpeed CurrentSpeed { get; private set; } = GameSpeed.Normal;

        private void Awake()
        {
            if (activeOwner == null)
            {
                activeOwner = this;
                UnityEngine.Time.timeScale = (float)CurrentSpeed;
            }
        }

        private void OnDestroy()
        {
            if (activeOwner != this)
            {
                return;
            }

            UnityEngine.Time.timeScale = 1f;
            activeOwner = null;
        }

        public bool TrySetSpeed(GameSpeed speed)
        {
            if (activeOwner != this)
            {
                Debug.LogWarning(
                    "[GameTimeService] Ignored speed change from duplicate instance.");
                return false;
            }

            if (!IsSupported(speed))
            {
                Debug.LogWarning($"[GameTimeService] Unsupported game speed: {(int)speed}.");
                return false;
            }

            if (speed == CurrentSpeed)
            {
                UnityEngine.Time.timeScale = (float)speed;
                return true;
            }

            var previous = CurrentSpeed;
            CurrentSpeed = speed;
            if (speed != GameSpeed.Paused)
            {
                lastRunningSpeed = speed;
            }

            UnityEngine.Time.timeScale = (float)speed;
            GameEventBus.PublishGameSpeedChanged(previous, speed);
            return true;
        }

        public void SetPaused()
        {
            TrySetSpeed(GameSpeed.Paused);
        }

        /// <summary>
        /// Pause the game, or resume the speed that was active before Pause.
        /// 暂停游戏，或恢复暂停前使用的速度。
        /// </summary>
        public void TogglePaused()
        {
            TrySetSpeed(CurrentSpeed == GameSpeed.Paused
                ? lastRunningSpeed
                : GameSpeed.Paused);
        }

        public void SetNormal()
        {
            TrySetSpeed(GameSpeed.Normal);
        }

        public void SetFast()
        {
            TrySetSpeed(GameSpeed.Fast);
        }

        private static bool IsSupported(GameSpeed speed)
        {
            return speed == GameSpeed.Paused
                || speed == GameSpeed.Normal
                || speed == GameSpeed.Fast;
        }
    }
}

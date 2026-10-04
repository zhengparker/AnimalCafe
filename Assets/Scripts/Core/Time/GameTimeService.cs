using System;
using System.Collections.Generic;
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
        private readonly List<ResumeBlock> resumeBlocks = new List<ResumeBlock>();
        public bool IsResumeBlocked => resumeBlocks.Count != 0;
        public string ResumeBlockReason => IsResumeBlocked ? resumeBlocks[0].Reason : string.Empty;
        public event Action ResumeAvailabilityChanged;
        // Explicit choices invalidate an older UI restore, including Pause while already paused.
        public int ExplicitChoiceVersion { get; private set; }

        public IDisposable AcquireResumeBlock(object owner, string reason)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            var block = new ResumeBlock(this, owner, reason ?? string.Empty);
            resumeBlocks.Add(block);
            TrySetAutomaticSpeed(GameSpeed.Paused);
            ResumeAvailabilityChanged?.Invoke();
            return block;
        }

        private sealed class ResumeBlock : IDisposable
        {
            private GameTimeService service;
            public object Owner { get; }
            public string Reason { get; }
            public ResumeBlock(GameTimeService service, object owner, string reason)
            { this.service = service; Owner = owner; Reason = reason; }
            public void Dispose()
            {
                var current = service; service = null;
                if (current == null || !current.resumeBlocks.Remove(this)) return;
                current.ResumeAvailabilityChanged?.Invoke();
            }
        }

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
            => SetSpeed(speed, true);

        public bool TrySetAutomaticSpeed(GameSpeed speed)
            => SetSpeed(speed, false);

        private bool SetSpeed(GameSpeed speed, bool explicitChoice)
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

            if (speed != GameSpeed.Paused && IsResumeBlocked) return false;
            if (explicitChoice) ExplicitChoiceVersion++;

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

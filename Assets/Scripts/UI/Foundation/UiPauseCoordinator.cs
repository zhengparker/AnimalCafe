using System;
using System.Collections.Generic;
using AnimalCafe.Core.Time;

namespace AnimalCafe.UI.Foundation
{
    /// <summary>
    /// A disposable reason that keeps the game paused while its owning UI view is active.
    /// UI view 活跃期间保持游戏暂停的可释放 reason。
    /// </summary>
    public interface IUiPauseHandle : IDisposable
    {
    }

    /// <summary>
    /// Coordinates UI pause reasons through the shared game-time service.
    /// 通过共享 game-time service 集中协调 UI pause reasons。
    /// </summary>
    public sealed class UiPauseCoordinator
    {
        private readonly IGameTimeService gameTimeService;
        private readonly List<PauseReason> activePauseReasons = new List<PauseReason>();
        private GameSpeed speedBeforeFirstPause;
        private bool hasSavedSpeed;

        public UiPauseCoordinator(IGameTimeService gameTimeService)
        {
            this.gameTimeService = gameTimeService
                ?? throw new ArgumentNullException(nameof(gameTimeService));
        }

        /// <summary>
        /// Acquires a lifecycle handle for a UI view. ContinueGame views receive a no-op handle.
        /// 为 UI view 获取生命周期 handle；ContinueGame view 获得 no-op handle。
        /// </summary>
        public IUiPauseHandle Acquire(object owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (!(owner is UiView view))
            {
                throw new ArgumentException("A UI pause reason must be owned by a UiView.", nameof(owner));
            }

            if (view.PausePolicy == UiPausePolicy.ContinueGame)
            {
                return NoopUiPauseHandle.Instance;
            }

            if (activePauseReasons.Count == 0)
            {
                speedBeforeFirstPause = gameTimeService.CurrentSpeed;
                hasSavedSpeed = true;
                gameTimeService.TrySetSpeed(GameSpeed.Paused);
            }

            var reason = new PauseReason(owner);
            activePauseReasons.Add(reason);
            return new UiPauseHandle(this, reason);
        }

        /// <summary>
        /// Explicit lifecycle cleanup for a disabled or destroyed owner.
        /// 为 disabled 或 destroyed owner 提供明确的生命周期清理入口。
        /// </summary>
        public void ReleaseForOwner(object owner)
        {
            if (owner == null)
            {
                return;
            }

            var releasedAnyReason = false;
            for (var index = activePauseReasons.Count - 1; index >= 0; index--)
            {
                if (ReferenceEquals(activePauseReasons[index].Owner, owner))
                {
                    activePauseReasons.RemoveAt(index);
                    releasedAnyReason = true;
                }
            }

            if (releasedAnyReason)
            {
                RestorePreviousSpeedIfNoReasonsRemain();
            }
        }

        private void Release(PauseReason reason)
        {
            if (reason == null || !activePauseReasons.Remove(reason))
            {
                return;
            }

            RestorePreviousSpeedIfNoReasonsRemain();
        }

        private void RestorePreviousSpeedIfNoReasonsRemain()
        {
            if (activePauseReasons.Count != 0 || !hasSavedSpeed)
            {
                return;
            }

            gameTimeService.TrySetSpeed(speedBeforeFirstPause);
            hasSavedSpeed = false;
        }

        private sealed class PauseReason
        {
            public PauseReason(object owner)
            {
                Owner = owner;
            }

            public object Owner { get; }
        }

        private sealed class UiPauseHandle : IUiPauseHandle
        {
            private UiPauseCoordinator coordinator;
            private PauseReason reason;

            public UiPauseHandle(UiPauseCoordinator coordinator, PauseReason reason)
            {
                this.coordinator = coordinator;
                this.reason = reason;
            }

            public void Dispose()
            {
                if (coordinator == null)
                {
                    return;
                }

                coordinator.Release(reason);
                coordinator = null;
                reason = null;
            }
        }

        private sealed class NoopUiPauseHandle : IUiPauseHandle
        {
            public static readonly NoopUiPauseHandle Instance = new NoopUiPauseHandle();

            public void Dispose()
            {
            }
        }
    }
}

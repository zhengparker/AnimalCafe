using System;
using System.Collections.Generic;
using AnimalCafe.Core.Time;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;

namespace AnimalCafe.Tests.Phase5
{
    public sealed class UiPauseCoordinatorTests
    {
        [Test]
        public void AT013_FirstPauseReason_RemembersFastAndPausesThroughGameTimeService()
        {
            var gameTime = new FakeGameTimeService(GameSpeed.Fast);
            var coordinator = new UiPauseCoordinator(gameTime);

            using (coordinator.Acquire(CreateView("Decoration", UiPausePolicy.PauseGame)))
            {
                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            }

            Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
        }

        [Test]
        public void AT014_ReleasingOneOfTwoPauseReasons_KeepsGamePaused()
        {
            var gameTime = new FakeGameTimeService(GameSpeed.Fast);
            var coordinator = new UiPauseCoordinator(gameTime);
            var first = coordinator.Acquire(CreateView("Panel", UiPausePolicy.PauseGame));
            var second = coordinator.Acquire(CreateView("Modal", UiPausePolicy.PauseGame));

            second.Dispose();

            Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));

            first.Dispose();
        }

        [Test]
        public void AT015_ReleasingLastPauseReason_RestoresTheExactPreviousFastSpeed()
        {
            var gameTime = new FakeGameTimeService(GameSpeed.Fast);
            var coordinator = new UiPauseCoordinator(gameTime);
            var handle = coordinator.Acquire(CreateView("Decoration", UiPausePolicy.PauseGame));

            handle.Dispose();

            Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
        }

        [Test]
        public void AT016_AcquiringContinueGameView_DoesNotChangeGameSpeed()
        {
            var gameTime = new FakeGameTimeService(GameSpeed.Fast);
            var coordinator = new UiPauseCoordinator(gameTime);

            using (coordinator.Acquire(CreateView("Resources", UiPausePolicy.ContinueGame)))
            {
                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
            }

            Assert.That(gameTime.SpeedChanges, Is.Empty);
        }

        [Test]
        public void AT017_ReleasingOneOwnersReasons_LeavesOtherPauseReasonActive()
        {
            var gameTime = new FakeGameTimeService(GameSpeed.Fast);
            var coordinator = new UiPauseCoordinator(gameTime);
            var panel = CreateView("Panel", UiPausePolicy.PauseGame);
            var modal = CreateView("Modal", UiPausePolicy.PauseGame);
            coordinator.Acquire(panel);
            var modalHandle = coordinator.Acquire(modal);

            coordinator.ReleaseForOwner(panel);

            Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));

            modalHandle.Dispose();
            Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
        }

        [Test]
        public void DisposingTheSamePauseHandleTwice_RestoresSpeedOnlyOnce()
        {
            var gameTime = new FakeGameTimeService(GameSpeed.Normal);
            var coordinator = new UiPauseCoordinator(gameTime);
            var handle = coordinator.Acquire(CreateView("Decoration", UiPausePolicy.PauseGame));

            Assert.DoesNotThrow(() => handle.Dispose());
            Assert.DoesNotThrow(() => handle.Dispose());

            Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
            Assert.That(gameTime.SpeedChanges, Is.EqualTo(new[] { GameSpeed.Paused, GameSpeed.Normal }));
        }

        [Test]
        public void AcquiringPauseReason_WhenGameTimeRejectsPause_ThrowsWithoutRegisteringAReason()
        {
            var gameTime = new FakeGameTimeService(GameSpeed.Fast);
            gameTime.EnqueueResult(false);
            var coordinator = new UiPauseCoordinator(gameTime);

            var exception = Assert.Throws<InvalidOperationException>(
                () => coordinator.Acquire(CreateView("Decoration", UiPausePolicy.PauseGame)));

            Assert.That(
                exception.Message,
                Is.EqualTo("UI pause request was rejected by the game time service."));
            Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
            Assert.That(coordinator.HasPendingRestore, Is.False);

            using (coordinator.Acquire(CreateView("DecorationRetry", UiPausePolicy.PauseGame)))
            {
                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            }

            Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
        }

        [Test]
        public void ReleasingLastReason_WhenGameTimeRejectsRestore_KeepsPendingSpeedUntilRetrySucceeds()
        {
            var gameTime = new FakeGameTimeService(GameSpeed.Fast);
            var coordinator = new UiPauseCoordinator(gameTime);
            var handle = coordinator.Acquire(CreateView("Decoration", UiPausePolicy.PauseGame));
            gameTime.EnqueueResult(false);

            handle.Dispose();

            Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Assert.That(coordinator.HasPendingRestore, Is.True);

            gameTime.EnqueueResult(false);
            Assert.That(coordinator.TryRestorePendingSpeed(), Is.False);
            Assert.That(coordinator.HasPendingRestore, Is.True);
            Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));

            gameTime.EnqueueResult(true);
            Assert.That(coordinator.TryRestorePendingSpeed(), Is.True);
            Assert.That(coordinator.HasPendingRestore, Is.False);
            Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
        }

        [Test]
        public void AcquiringAfterFailedRestore_PreservesOriginalSpeedUntilTheNewReasonReleases()
        {
            var gameTime = new FakeGameTimeService(GameSpeed.Fast);
            var coordinator = new UiPauseCoordinator(gameTime);
            var firstHandle = coordinator.Acquire(CreateView("First", UiPausePolicy.PauseGame));
            gameTime.EnqueueResult(false);

            firstHandle.Dispose();

            var secondHandle = coordinator.Acquire(CreateView("Second", UiPausePolicy.PauseGame));

            Assert.That(coordinator.HasPendingRestore, Is.True);
            Assert.That(coordinator.TryRestorePendingSpeed(), Is.False);

            secondHandle.Dispose();

            Assert.That(coordinator.HasPendingRestore, Is.False);
            Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
        }

        private static UiView CreateView(string viewId, UiPausePolicy pausePolicy)
        {
            return new UiView(
                viewId,
                UiViewKind.MainPanel,
                pausePolicy,
                UiOutsideDismissPolicy.NotDismissible);
        }

        private sealed class FakeGameTimeService : IGameTimeService
        {
            private readonly Queue<bool> results = new Queue<bool>();
            private readonly List<GameSpeed> speedChanges = new List<GameSpeed>();

            public FakeGameTimeService(GameSpeed initialSpeed)
            {
                CurrentSpeed = initialSpeed;
            }

            public GameSpeed CurrentSpeed { get; private set; }

            public IReadOnlyList<GameSpeed> SpeedChanges => speedChanges;

            public void EnqueueResult(bool result)
            {
                results.Enqueue(result);
            }

            public bool TrySetSpeed(GameSpeed speed)
            {
                if (results.Count > 0 && !results.Dequeue())
                {
                    return false;
                }

                CurrentSpeed = speed;
                speedChanges.Add(speed);
                return true;
            }
        }
    }
}

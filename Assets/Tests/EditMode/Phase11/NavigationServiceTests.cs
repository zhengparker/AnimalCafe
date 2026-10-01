using System;
using AnimalCafe.Navigation;
using NUnit.Framework;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase11
{
    public sealed class NavigationServiceTests
    {
        private NavigationService service;
        private FakeNavigationDriver driver;
        private MovementResult? result;
        private int callbackCount;

        [SetUp]
        public void SetUp()
        {
            service = new NavigationService(new NavigationSettings());
            driver = new FakeNavigationDriver();
            result = null;
            callbackCount = 0;
            Assert.That(service.Register("A", driver), Is.True);
        }

        private MoveStartResult Start(NavigationTarget? recovery = null)
            => service.MoveTo("A", new NavigationTarget(new Vector3(1, 0, 0)),
                r => { result = r; callbackCount++; }, recovery);

        [Test]
        public void TerminalFailureStopsDriverOnceBeforeCallback()
        {
            driver.BeginFailure = NavigationFailure.InvalidTarget;
            var stopsAtCallback = 0;
            service.MoveTo("A", new NavigationTarget(Vector3.right), r => {
                result = r; stopsAtCallback = driver.StopCount;
            });
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Failed));
            Assert.That(stopsAtCallback, Is.EqualTo(1));
            Assert.That(driver.StopCount, Is.EqualTo(1));
        }

        [Test]
        public void RetriesOnceFromLatestPosition()
        {
            var start = Start();
            Assert.That(start.Accepted, Is.True);
            driver.Position = new Vector3(0.25f, 0, 0);
            service.Tick(3.1f);
            Assert.That(driver.BeginPositions.Count, Is.EqualTo(2));
            Assert.That(driver.BeginPositions[1], Is.EqualTo(driver.Position));
            service.Tick(3.1f);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Failed));
            Assert.That(result.Value.RetryCount, Is.EqualTo(1));
            Assert.That(driver.BeginPositions.Count, Is.EqualTo(2));
        }

        [Test]
        public void PauseDoesNotConsumeTimeout()
        {
            Start();
            service.Tick(0);
            service.Tick(0);
            Assert.That(driver.BeginPositions.Count, Is.EqualTo(1));
            Assert.That(result, Is.Null);
            service.Tick(2.9f);
            Assert.That(driver.BeginPositions.Count, Is.EqualTo(1));
            service.Tick(0.2f);
            Assert.That(driver.BeginPositions.Count, Is.EqualTo(2));
        }

        [Test]
        public void RecoveryIsNotArrival()
        {
            Start(new NavigationTarget(new Vector3(-1, 0, 0)));
            service.Tick(3.1f);
            service.Tick(3.1f);
            Assert.That(driver.Targets.Count, Is.EqualTo(3));
            Assert.That(driver.Targets[2].Position, Is.EqualTo(new Vector3(-1, 0, 0)));
            driver.Position = new Vector3(-1, 0, 0);
            driver.RemainingDistance = 0;
            service.Tick(0.1f);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Recovered));
            Assert.That(result.Value.OriginalFailure, Is.Not.EqualTo(NavigationFailure.None));
            Assert.That(callbackCount, Is.EqualTo(1));
        }

        [Test]
        public void RejectsBusyWithoutReplacing()
        {
            var first = Start();
            var second = Start();
            Assert.That(second.Accepted, Is.False);
            Assert.That(second.RequestId, Is.Zero);
            Assert.That(second.Reason, Is.EqualTo(NavigationFailure.Busy));
            Assert.That(callbackCount, Is.Zero);
            Assert.That(service.Cancel(first.RequestId), Is.True);
            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Cancelled));
        }

        [Test]
        public void TerminalCallbackIsOnceAndReentryIsSafe()
        {
            MoveStartResult next = default;
            var first = service.MoveTo("A", new NavigationTarget(new Vector3(1, 0, 0)), r =>
            {
                callbackCount++;
                next = Start();
            });
            Assert.That(service.Cancel(first.RequestId), Is.True);
            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(next.Accepted, Is.True);
            Assert.That(service.Cancel(first.RequestId), Is.False);
            Assert.That(service.Cancel(next.RequestId), Is.True);
            Assert.That(callbackCount, Is.EqualTo(2));
        }

        [Test]
        public void OldGenerationIsIgnored()
        {
            Start();
            driver.UseStaleGeneration = true;
            driver.Position = new Vector3(1, 0, 0);
            driver.RemainingDistance = 0;
            service.Tick(0.1f);
            Assert.That(result, Is.Null);
            driver.UseStaleGeneration = false;
            service.Tick(0.1f);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Arrived));
        }

        [Test]
        public void OscillationWithoutNetProgressFailsAfterOneRetry()
        {
            driver.PathLength = 1;
            Start();
            for (var i = 0; i < 11; i++)
            {
                driver.RemainingDistance = i % 2 == 0 ? 0.8f : 1f;
                service.Tick(1f);
            }
            Assert.That(driver.BeginPositions.Count, Is.EqualTo(2));
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Failed));
        }

        [Test]
        public void TurningFarFromDestinationDoesNotResetStuckTimer()
        {
            service.MoveTo("A", new NavigationTarget(Vector3.right, Vector3.forward), r => result = r);
            driver.FacingErrorDegrees = 20f;
            service.Tick(1f);
            driver.FacingErrorDegrees = 15f;
            service.Tick(1f);
            driver.FacingErrorDegrees = 10f;
            service.Tick(1f);
            Assert.That(driver.BeginPositions.Count, Is.EqualTo(2));
            Assert.That(result, Is.Null);
        }

        [Test]
        public void FinalFacingProgressCanFinishWithoutPrematureRetry()
        {
            service.MoveTo("A", new NavigationTarget(Vector3.right, Vector3.forward), r => result = r);
            driver.Position = Vector3.right;
            driver.RemainingDistance = 0f;
            driver.FacingErrorDegrees = 20f;
            service.Tick(1f);
            driver.FacingErrorDegrees = 15f;
            service.Tick(1f);
            driver.FacingErrorDegrees = 10f;
            service.Tick(1f);
            Assert.That(driver.BeginPositions.Count, Is.EqualTo(1));
            Assert.That(result, Is.Null);
            driver.FacingErrorDegrees = 5f;
            service.Tick(1f);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Arrived));
            Assert.That(result.Value.RetryCount, Is.Zero);
        }

        [Test]
        public void FinalFacingProgressCannotExtendSegmentDeadline()
        {
            service.MoveTo("A", new NavigationTarget(Vector3.right, Vector3.forward), r => result = r);
            driver.Position = Vector3.right;
            driver.RemainingDistance = 0f;
            for (var i = 0; i < 10; i++)
            {
                driver.FacingErrorDegrees = 100f - i * 5f;
                service.Tick(1f);
            }
            Assert.That(driver.BeginPositions.Count, Is.EqualTo(2));
            Assert.That(result, Is.Null);
        }

        [Test]
        public void LongFrameCountsFullTimeout()
        {
            Start();
            service.Tick(12f);
            Assert.That(driver.BeginPositions.Count, Is.EqualTo(2));
            service.Tick(12f);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Failed));
        }

        [Test]
        public void LaterPathLengthCannotExtendSegmentDeadline()
        {
            driver.PathLength = 1f;
            Start();
            service.Tick(1f);
            driver.PathLength = 100f;
            for (var i = 0; i < 9; i++)
            {
                driver.RemainingDistance = 1f - (i + 1) * 0.03f;
                service.Tick(1f);
            }
            Assert.That(driver.BeginPositions.Count, Is.EqualTo(2));
            Assert.That(result, Is.Null);
        }

        [Test]
        public void InvalidDeltaHasNoSideEffects()
        {
            Start();
            Assert.Throws<ArgumentOutOfRangeException>(() => service.Tick(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.Tick(-1));
            Assert.That(driver.BeginPositions.Count, Is.EqualTo(1));
            Assert.That(callbackCount, Is.Zero);
        }

        [Test]
        public void LayoutChangeEndsOldRequestOnce()
        {
            Start();
            service.InvalidateLayout(1);
            service.InvalidateLayout(2);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.LayoutChanged));
            Assert.That(callbackCount, Is.EqualTo(1));
        }

        [Test]
        public void UnregisterEndsActiveRequestOnce()
        {
            Start();
            service.Unregister("A");
            service.Unregister("A");
            Assert.That(result.Value.Reason, Is.EqualTo(NavigationFailure.ActorUnavailable));
            Assert.That(callbackCount, Is.EqualTo(1));
        }

        [Test]
        public void InitialInvalidTargetFailsWithoutRecovery()
        {
            driver.BeginFailure = NavigationFailure.InvalidTarget;
            Start(new NavigationTarget(new Vector3(-1, 0, 0)));
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Failed));
            Assert.That(driver.Targets.Count, Is.EqualTo(1));
        }

        [Test]
        public void RecoveryPathFailureRetainsBothReasons()
        {
            Start(new NavigationTarget(new Vector3(-1, 0, 0)));
            service.Tick(3.1f);
            service.Tick(3.1f);
            driver.PathState = NavigationPathState.Invalid;
            service.Tick(0.1f);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Failed));
            Assert.That(result.Value.Reason, Is.EqualTo(NavigationFailure.RecoveryFailed));
            Assert.That(result.Value.OriginalFailure, Is.EqualTo(NavigationFailure.MovementTimeout));
            Assert.That(result.Value.RecoveryFailure, Is.EqualTo(NavigationFailure.IncompletePath));
            Assert.That(driver.Targets.Count, Is.EqualTo(3));
            Assert.That(callbackCount, Is.EqualTo(1));
        }

        [Test]
        public void ArrivalRequiresActualPositionRemainingPathAndSlowSpeed()
        {
            Start();
            driver.Position = new Vector3(1, 0, 0);
            driver.RemainingDistance = 0.2f;
            service.Tick(0.1f);
            Assert.That(result, Is.Null);
            driver.RemainingDistance = 0;
            driver.ActualSpeed = 0.2f;
            service.Tick(0.1f);
            Assert.That(result, Is.Null);
            driver.ActualSpeed = 0;
            service.Tick(0.1f);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Arrived));
        }

        [Test]
        public void ArrivalUsesValidatedSampledDestination()
        {
            Start();
            driver.ResolvedTargetPosition = new Vector3(1.12f, 0, 0);
            driver.Position = driver.ResolvedTargetPosition.Value;
            driver.RemainingDistance = 0;
            service.Tick(0.1f);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Arrived));
        }

        [Test]
        public void SampleBeyondLimitCannotClaimArrival()
        {
            Start();
            driver.ResolvedTargetPosition = new Vector3(1.2f, 0, 0);
            driver.Position = driver.ResolvedTargetPosition.Value;
            driver.RemainingDistance = 0;
            service.Tick(0.1f);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Failed));
            Assert.That(result.Value.Reason, Is.EqualTo(NavigationFailure.InvalidTarget));
        }

        [Test]
        public void SampleOutsideAllowedRegionCannotClaimArrival()
        {
            var target = new NavigationTarget(new Vector3(1, 0, 0),
                allowedRegion: new Bounds(new Vector3(1, 0, 0), new Vector3(0.2f, 1, 0.2f)));
            service.MoveTo("A", target, r => result = r);
            driver.ResolvedTargetPosition = new Vector3(1.12f, 0, 0);
            driver.Position = driver.ResolvedTargetPosition.Value;
            driver.RemainingDistance = 0;
            service.Tick(0.1f);
            Assert.That(result.Value.Reason, Is.EqualTo(NavigationFailure.InvalidTarget));
        }

        [Test]
        public void PendingPathTimeoutRetriesOnce()
        {
            driver.PathState = NavigationPathState.Pending;
            Start();
            service.Tick(2.1f);
            Assert.That(driver.Targets.Count, Is.EqualTo(2));
            service.Tick(2.1f);
            Assert.That(result.Value.Reason, Is.EqualTo(NavigationFailure.PathTimeout));
            Assert.That(callbackCount, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateActorAndUnknownActorAreRejected()
        {
            Assert.That(service.Register("A", new FakeNavigationDriver()), Is.False);
            Assert.That(service.Register("", new FakeNavigationDriver()), Is.False);
            Assert.That(service.Register("B", null), Is.False);
            var unknown = service.MoveTo("missing", new NavigationTarget(Vector3.one), _ => { });
            Assert.That(unknown.Accepted, Is.False);
            Assert.That(unknown.RequestId, Is.Zero);
            Assert.That(unknown.Reason, Is.EqualTo(NavigationFailure.UnknownActor));
        }

        [Test]
        public void ShutdownEndsRequestAndRejectsCallbackReentry()
        {
            MoveStartResult reentry = default;
            service.MoveTo("A", new NavigationTarget(Vector3.one), _ =>
            {
                callbackCount++;
                reentry = Start();
            });
            service.Shutdown();
            service.Shutdown();
            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(reentry.Accepted, Is.False);
            Assert.That(driver.Targets.Count, Is.EqualTo(1));
        }

        [Test]
        public void TargetAndSettingsRejectInvalidNumbers()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationTarget(new Vector3(float.NaN, 0, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationTarget(Vector3.zero, Vector3.zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationTarget(Vector3.zero,
                allowedRegion: new Bounds(new Vector3(10, 0, 0), Vector3.one)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationService(new NavigationSettings { AgentRadius = -1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationService(new NavigationSettings { MaxSpeed = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NavigationService(new NavigationSettings { NoProgressSeconds = float.PositiveInfinity }));
        }
    }
}

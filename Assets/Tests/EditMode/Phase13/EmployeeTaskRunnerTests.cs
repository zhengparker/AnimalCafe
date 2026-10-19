using System;
using AnimalCafe.Employees;
using AnimalCafe.Navigation;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase13
{
    public sealed class EmployeeTaskRunnerTests
    {
        [Test]
        public void CashierCompletesAfterExactlyTwoGameSeconds()
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Cashier, out var env);
            Assert.That(runner.TryStart(), Is.True);
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Working));
            Assert.That(env.Visual, Is.EqualTo(EmployeeVisualMode.Working));
            runner.Tick(1.99f);
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.None));
            runner.Tick(.01f);
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.Succeeded));
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Idle));
            Assert.That(runner.Snapshot.HandleId, Is.Zero);
            runner.Tick(100);
            Assert.That(env.CleanupCount, Is.EqualTo(1));
        }

        [Test]
        public void BaristaRequiresPickupArrivedThenActualReturn()
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            runner.TryStart(); runner.Tick(2.99f);
            Assert.That(env.Held, Is.Zero);
            runner.Tick(.01f);
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.CarryingToPickUp));
            Assert.That(env.Visual, Is.EqualTo(EmployeeVisualMode.Carrying));
            Assert.That(env.Held, Is.EqualTo(1));
            Assert.That(env.Moves[1].Target.Position.x, Is.EqualTo(5));
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.None));
            env.Complete(1);
            Assert.That(env.Placed, Is.EqualTo(1));
            Assert.That(env.PlacedAddress, Is.EqualTo(EmployeeTaskEnvironmentFake.Binding(EmployeeRole.Barista).CupAddress));
            Assert.That(env.Held, Is.Zero);
            Assert.That(runner.Snapshot.Delivered, Is.True);
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.Succeeded));
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Returning));
            Assert.That(runner.Snapshot.HandleId, Is.GreaterThan(0));
            Assert.That(env.Moves[2].Target.Position.x, Is.Zero);
            Assert.That(runner.TryStart(), Is.False);
            env.Complete(2);
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Idle));
            Assert.That(runner.Snapshot.HandleId, Is.Zero);
            Assert.That(env.CleanupCount, Is.EqualTo(1));
        }

        [TestCase(EmployeeRole.Cashier)] [TestCase(EmployeeRole.Barista)]
        public void BusyEmployeeRejectsSecondHandle(EmployeeRole role)
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(role, out var env);
            Assert.That(runner.TryStart(), Is.True);
            long handle = runner.Snapshot.HandleId;
            Assert.That(runner.TryStart(), Is.False);
            Assert.That(runner.TryAssignBinding(EmployeeTaskEnvironmentFake.Binding(role)), Is.False);
            Assert.That(runner.Snapshot.HandleId, Is.EqualTo(handle));
            Assert.That(env.Moves.Count, Is.EqualTo(1));
        }

        [Test]
        public void BindingDoesNotClaimEmployeeAlreadyAtWork()
        {
            var env = new EmployeeTaskEnvironmentFake();
            var runner = new EmployeeTaskRunner(env.ActorId, EmployeeRole.Cashier, env);
            Assert.That(runner.TryStart(), Is.False);
            Assert.That(runner.TryAssignBinding(EmployeeTaskEnvironmentFake.Binding(EmployeeRole.Cashier)), Is.True);
            Assert.That(runner.TryStart(), Is.False);
            runner.TryReturnToWork();
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.MovingToWork));
            Assert.That(runner.TryStart(), Is.False);
            env.Complete(0);
            Assert.That(runner.TryStart(), Is.True);
        }

        [Test]
        public void SynchronousCompletionCannotOverwriteNextLeg()
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            env.Immediate.Enqueue(MovementStatus.Arrived); // pickup completes inside MoveTo
            runner.TryStart(); runner.Tick(3);
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Returning));
            Assert.That(env.Placed, Is.EqualTo(1));
            runner.Cancel("stop while returning");
            Assert.That(env.Cancelled, Does.Contain(3L));
            Assert.That(env.Cancelled, Has.No.Member(2L));
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.Succeeded));
        }

        [Test]
        public void AllSynchronousLegsCompleteOnlyOnce()
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            env.Immediate.Enqueue(MovementStatus.Arrived); env.Immediate.Enqueue(MovementStatus.Arrived);
            runner.TryStart(); runner.Tick(3);
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Idle));
            Assert.That(env.CleanupCount, Is.EqualTo(1));
            env.Complete(1); env.Complete(2);
            Assert.That(env.Placed, Is.EqualTo(1));
            Assert.That(env.CleanupCount, Is.EqualTo(1));
        }

        [TestCase(MovementStatus.Recovered)] [TestCase(MovementStatus.Failed)]
        [TestCase(MovementStatus.Cancelled)] [TestCase(MovementStatus.LayoutChanged)]
        public void RecoveredIsNotDelivered(MovementStatus status)
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            runner.TryStart(); runner.Tick(3); env.Complete(1, status);
            Assert.That(env.Placed, Is.Zero);
            Assert.That(env.Held, Is.Zero);
            Assert.That(runner.Snapshot.Delivered, Is.False);
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.Failed));
            Assert.That(env.CleanupCount, Is.EqualTo(1));
            Assert.That(runner.Snapshot.State, Is.Not.EqualTo(EmployeeState.Idle));
        }

        [Test]
        public void StaleSessionCannotPlaceCup()
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            runner.TryStart(); runner.Tick(3);
            long oldHandle = runner.Snapshot.HandleId;
            runner.Cancel("old session"); env.Complete(2);
            runner.TryStart(); runner.Tick(3);
            Assert.That(runner.Snapshot.HandleId, Is.GreaterThan(oldHandle));
            env.Complete(1);
            Assert.That(env.Placed, Is.Zero);
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.CarryingToPickUp));
            env.Complete(3); env.Complete(3);
            Assert.That(env.Placed, Is.EqualTo(1));
        }

        [Test]
        public void WrongRequestOrActorCannotCompleteDelivery()
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            runner.TryStart(); runner.Tick(3);
            env.Complete(1, requestId: 999); env.Complete(1, actorId: "employee.other");
            Assert.That(env.Placed, Is.Zero);
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.CarryingToPickUp));
            env.Complete(1);
            Assert.That(env.Placed, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantCancelCleansOnce()
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            runner.TryStart(); runner.Tick(3);
            bool restarted = false;
            env.OnCancel = () => { runner.Cancel("nested cancel"); env.Complete(1); restarted |= runner.TryStart(); };
            env.OnCleanup = () => { runner.Cancel("nested cleanup"); restarted |= runner.TryStart(); };
            runner.Cancel("outer"); runner.Cancel("repeat");
            Assert.That(restarted, Is.False);
            Assert.That(env.CleanupCount, Is.EqualTo(1));
            Assert.That(env.Cancelled.Count, Is.EqualTo(1));
            Assert.That(env.Placed, Is.Zero);
            Assert.That(env.Held, Is.Zero);
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.Cancelled));
        }

        [Test]
        public void PausedArrivalDefersDelivery()
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            runner.TryStart(); runner.Tick(3); env.Gate = false; env.Complete(1);
            runner.Tick(10);
            Assert.That(env.Placed, Is.Zero);
            Assert.That(runner.Snapshot.Delivered, Is.False);
            env.Gate = true; runner.Tick(0);
            Assert.That(env.Placed, Is.Zero);
            runner.Tick(.1f); runner.Tick(.1f); env.Complete(1);
            Assert.That(env.Placed, Is.EqualTo(1));
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Returning));
        }

        [Test]
        public void PausedArrivalCancelledBeforeResumeCannotPlaceCup()
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            runner.TryStart(); runner.Tick(3); env.Gate = false; env.Complete(1);
            runner.Cancel("cancel paused"); env.Gate = true; runner.Tick(.1f);
            Assert.That(env.Placed, Is.Zero);
            Assert.That(env.CleanupCount, Is.EqualTo(1));
        }

        [TestCase(MovementStatus.Failed)] [TestCase(MovementStatus.Recovered)]
        public void ReturnFailurePreservesDeliveredOutcome(MovementStatus status)
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            runner.TryStart(); runner.Tick(3); env.Complete(1); env.Complete(2, status);
            Assert.That(runner.Snapshot.Delivered, Is.True);
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.Succeeded));
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Blocked));
            Assert.That(runner.Snapshot.Failure, Is.Not.Empty);
            Assert.That(runner.Snapshot.HandleId, Is.Zero);
            Assert.That(env.Placed, Is.EqualTo(1));
            Assert.That(env.CleanupCount, Is.EqualTo(1));
            runner.Tick(100);
            Assert.That(env.Moves.Count, Is.EqualTo(3));
            Assert.That(runner.TryStart(), Is.False);
            runner.TryReturnToWork(); env.Complete(3);
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Idle));
        }

        [Test]
        public void LayoutInvalidationRetiresOldCallbacksAndRequiresFreshBinding()
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            runner.TryStart(); runner.Tick(3); runner.InvalidateLayout(8); runner.InvalidateLayout(9);
            env.Complete(1);
            Assert.That(env.Placed, Is.Zero);
            Assert.That(env.CleanupCount, Is.EqualTo(1));
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Blocked));
            Assert.That(runner.TryAssignBinding(EmployeeTaskEnvironmentFake.Binding(EmployeeRole.Barista)), Is.False);
            Assert.That(runner.TryAssignBinding(EmployeeTaskEnvironmentFake.Binding(EmployeeRole.Barista, 9)), Is.True);
            Assert.That(runner.TryStart(), Is.False);
            runner.TryReturnToWork(); env.Complete(2);
            Assert.That(runner.TryStart(), Is.True);
        }

        [Test]
        public void DisposeRetiresTaskAndRecoveryCannotRestart()
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            runner.TryStart(); runner.Tick(3); runner.Dispose(); runner.Dispose(); env.Complete(1);
            runner.TryReturnToWork(); runner.Tick(100);
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Disabled));
            Assert.That(runner.TryStart(), Is.False);
            Assert.That(env.CleanupCount, Is.EqualTo(1));
            Assert.That(env.Placed, Is.Zero);
        }

        [TestCase(true)] [TestCase(false)]
        public void ReentrantDisposeDuringPlacementCannotReviveTask(bool placeResult)
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            runner.TryStart(); runner.Tick(3);
            env.PlaceSucceeds = placeResult;
            env.OnPlaceCup = () => runner.Dispose();
            Assert.DoesNotThrow(() => env.Complete(1));
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Disabled));
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.Cancelled));
            Assert.That(runner.Snapshot.Delivered, Is.False);
            Assert.That(runner.Snapshot.HandleId, Is.Zero);
            Assert.That(env.Moves.Count, Is.EqualTo(2));
            Assert.That(env.CleanupCount, Is.EqualTo(1));
            runner.Tick(10); env.Complete(1);
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Disabled));
            Assert.That(env.CleanupCount, Is.EqualTo(1));
        }

        [TestCase(true)] [TestCase(false)]
        public void ReentrantLayoutChangeDuringPlacementCannotUseClearedBinding(bool placeResult)
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            runner.TryStart(); runner.Tick(3);
            env.PlaceSucceeds = placeResult;
            EmployeeTaskSnapshot retired = default;
            env.OnPlaceCup = () => { runner.InvalidateLayout(8); retired = runner.Snapshot; };
            Assert.DoesNotThrow(() => env.Complete(1));
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Blocked));
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.Cancelled));
            Assert.That(runner.Snapshot.Delivered, Is.False);
            Assert.That(runner.Snapshot.Failure, Is.EqualTo(retired.Failure));
            Assert.That(runner.Snapshot.HandleId, Is.Zero);
            Assert.That(env.Moves.Count, Is.EqualTo(2));
            Assert.That(env.CleanupCount, Is.EqualTo(1));
            env.Complete(1); runner.Tick(10);
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Blocked));
            Assert.That(env.CleanupCount, Is.EqualTo(1));
        }

        [TestCase(true)] [TestCase(false)]
        public void ReentrantCancelDuringPlacementCannotOverrideRecoveryOrCommitSuccess(bool placeResult)
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            runner.TryStart(); runner.Tick(3);
            env.PlaceSucceeds = placeResult;
            env.OnPlaceCup = () => runner.Cancel("cancel within placement");
            Assert.DoesNotThrow(() => env.Complete(1));
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.MovingToWork));
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.Cancelled));
            Assert.That(runner.Snapshot.Delivered, Is.False);
            Assert.That(runner.Snapshot.HandleId, Is.Zero);
            Assert.That(env.Moves.Count, Is.EqualTo(3)); // only Cancel's recovery, no stale Returning
            Assert.That(env.CleanupCount, Is.EqualTo(1));
            env.Complete(2);
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Idle));
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.Cancelled));
            Assert.That(env.CleanupCount, Is.EqualTo(1));
        }
        [Test]
        public void FailedVisualCannotCommitSuccess()
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            env.PlaceSucceeds = false;
            runner.TryStart(); runner.Tick(3); env.Complete(1);
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.Failed));
            Assert.That(runner.Snapshot.Delivered, Is.False);
            Assert.That(env.Placed, Is.Zero);
            Assert.That(env.CleanupCount, Is.EqualTo(1));
        }

        [Test]
        public void FailedHeldVisualCannotStartDelivery()
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            env.ShowSucceeds = false; runner.TryStart(); runner.Tick(3);
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.Failed));
            Assert.That(env.Placed, Is.Zero);
            Assert.That(env.Moves.Count, Is.EqualTo(2)); // startup plus recovery, no pickup move
            Assert.That(env.Moves[1].Target.Position.x, Is.Zero);
        }

        [Test]
        public void SynchronousMoveRejectionBlocksWithoutClaimingArrival()
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var env);
            env.RejectMove = true; runner.TryStart(); runner.Tick(3);
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Blocked));
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.Failed));
            Assert.That(env.CleanupCount, Is.EqualTo(1));
        }

        [Test]
        public void PauseFreezesWorkAndRejectsNewStart()
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Cashier, out var env);
            env.Gate = false;
            Assert.That(runner.TryStart(), Is.False);
            env.Gate = true; runner.TryStart(); runner.Tick(.5f);
            env.Gate = false; runner.Tick(10); runner.Tick(0);
            Assert.That(runner.Snapshot.RemainingWorkSeconds, Is.EqualTo(1.5f));
            env.Gate = true; runner.Tick(1.5f);
            Assert.That(runner.Snapshot.Outcome, Is.EqualTo(EmployeeTaskOutcome.Succeeded));
        }

        [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidDeltaThrowsWithoutChangingState(float delta)
        {
            var runner = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Cashier, out var env);
            runner.TryStart(); runner.Tick(.5f);
            var before = runner.Snapshot;
            Assert.Throws<ArgumentOutOfRangeException>(() => runner.Tick(delta));
            Assert.That(runner.Snapshot.RemainingWorkSeconds, Is.EqualTo(before.RemainingWorkSeconds));
            Assert.That(runner.Snapshot.HandleId, Is.EqualTo(before.HandleId));
            Assert.That(runner.Snapshot.State, Is.EqualTo(before.State));
        }

        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidDurationCannotCreateRunner(float duration)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new EmployeeTaskRunner("employee.test",
                EmployeeRole.Cashier, new EmployeeTaskEnvironmentFake(), duration));
        }

        [Test]
        public void ConfiguredDurationAndIndependentEmployeesConsumeScaledDeltaOnce()
        {
            var cashier = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Cashier, out var cashEnv, 4);
            var barista = EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista, out var barEnv);
            cashier.TryStart(); barista.TryStart(); cashier.Tick(2); barista.Tick(2);
            Assert.That(cashier.Snapshot.RemainingWorkSeconds, Is.EqualTo(2));
            Assert.That(barista.Snapshot.RemainingWorkSeconds, Is.EqualTo(1));
            var snapshot = cashier.Snapshot;
            cashier.Cancel("only cashier"); barista.Tick(1);
            Assert.That(snapshot.State, Is.EqualTo(EmployeeState.Working)); // immutable historical value
            Assert.That(barista.Snapshot.State, Is.EqualTo(EmployeeState.CarryingToPickUp));
            Assert.That(barEnv.CleanupCount, Is.Zero);
            Assert.That(cashEnv.CleanupCount, Is.EqualTo(1));
        }
    }
}





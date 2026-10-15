using AnimalCafe.Employees;
using AnimalCafe.Navigation;
using NUnit.Framework;
namespace AnimalCafe.Tests.EditMode.Phase13
{
    public sealed class EmployeeLifecycleTests
    {
        [Test] public void FailedTaskReasonSurvivesActualReturnUntilNextStart()
        {
            var runner=EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista,out var env);
            runner.TryStart(); runner.Tick(3); env.Complete(1,MovementStatus.Failed);
            var reason=runner.Snapshot.Failure;
            Assert.That(reason,Is.Not.Empty); env.Complete(2);
            Assert.That(runner.Snapshot.State,Is.EqualTo(EmployeeState.Idle));
            Assert.That(runner.Snapshot.Outcome,Is.EqualTo(EmployeeTaskOutcome.Failed));
            Assert.That(runner.Snapshot.Failure,Is.EqualTo(reason));
            Assert.That(runner.TryStart(),Is.True); Assert.That(runner.Snapshot.Failure,Is.Empty);
        }
        [Test] public void FailedReasonSurvivesRebindAndActualRecoveryUntilNextStart()
        {
            var runner=EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista,out var env);
            Assert.That(runner.TryStart(),Is.True); runner.Tick(3);
            env.Complete(1,MovementStatus.Failed); // Task fails, then automatic return also fails.
            env.Complete(2,MovementStatus.Failed);
            Assert.That(runner.Snapshot.State,Is.EqualTo(EmployeeState.Blocked));
            Assert.That(runner.Snapshot.Outcome,Is.EqualTo(EmployeeTaskOutcome.Failed));
            var reason=runner.Snapshot.Failure; Assert.That(reason,Is.Not.Empty);
            // Explicit controller Retry uses this binding assignment before returning from actual position.
            Assert.That(runner.TryAssignBinding(EmployeeTaskEnvironmentFake.Binding(EmployeeRole.Barista)),Is.True);
            Assert.That(runner.Snapshot.Failure,Is.EqualTo(reason));
            runner.TryReturnToWork();
            Assert.That(runner.Snapshot.State,Is.EqualTo(EmployeeState.MovingToWork));
            env.Complete(3);
            Assert.That(runner.Snapshot.State,Is.EqualTo(EmployeeState.Idle));
            Assert.That(runner.Snapshot.Outcome,Is.EqualTo(EmployeeTaskOutcome.Failed));
            Assert.That(runner.Snapshot.Failure,Is.EqualTo(reason));
            Assert.That(runner.TryStart(),Is.True);
            Assert.That(runner.Snapshot.Failure,Is.Empty);
            Assert.That(runner.Snapshot.Outcome,Is.EqualTo(EmployeeTaskOutcome.None));
        }
        [Test] public void RepeatedSameRevisionSuspensionHasBoundedAutomaticResume()
        {
            var runner=EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista,out var env);
            runner.TryStart(); runner.Tick(3); env.Gate=false; env.Complete(1,MovementStatus.LayoutChanged);
            env.Gate=true; runner.Tick(.1f); env.Gate=false; env.Complete(2,MovementStatus.LayoutChanged);
            env.Gate=true; for(var i=0;i<10;i++) runner.Tick(.1f);
            Assert.That(runner.Snapshot.State,Is.EqualTo(EmployeeState.Blocked));
            Assert.That(runner.Snapshot.HandleId,Is.Zero); Assert.That(runner.Snapshot.Failure,Is.Not.Empty);
            Assert.That(env.Moves.Count,Is.EqualTo(3)); Assert.That(env.CleanupCount,Is.EqualTo(1));
        }
        [Test] public void RepeatedRevisionInvalidationCleansOldHandleOnceAndRejectsOldCallback()
        {
            var runner=EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista,out var env);
            runner.TryStart(); runner.Tick(3); runner.InvalidateLayout(8); runner.InvalidateLayout(9);
            env.Complete(1);
            Assert.That(env.CleanupCount,Is.EqualTo(1)); Assert.That(env.Placed,Is.Zero);
            Assert.That(runner.Snapshot.HandleId,Is.Zero); Assert.That(runner.Snapshot.State,Is.EqualTo(EmployeeState.Blocked));
        }
        [Test] public void SameRevisionSuspensionRetainsHandleCupAndResubmitsOnlyOnceWhenReady()
        {
            var runner=EmployeeTaskEnvironmentFake.Ready(EmployeeRole.Barista,out var env);
            runner.TryStart(); runner.Tick(3); var handle=runner.Snapshot.HandleId;
            env.Gate=false; env.Complete(1,MovementStatus.LayoutChanged); runner.Tick(100);
            Assert.That(runner.Snapshot.HandleId,Is.EqualTo(handle)); Assert.That(env.Held,Is.EqualTo(1));
            Assert.That(env.CleanupCount,Is.Zero); env.Gate=true; runner.Tick(0);
            Assert.That(env.Moves.Count,Is.EqualTo(2)); runner.Tick(.1f); runner.Tick(.1f);
            Assert.That(env.Moves.Count,Is.EqualTo(3)); env.Complete(2);
            Assert.That(runner.Snapshot.Delivered,Is.True); Assert.That(env.Placed,Is.EqualTo(1));
        }
    }
}

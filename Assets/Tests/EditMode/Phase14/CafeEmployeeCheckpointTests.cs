using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.CafeLoop;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using AnimalCafe.Tests.EditMode.Phase13;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase14
{
    public sealed class CafeEmployeeCheckpointTests
    {
        private sealed class Port : IIntegratedEmployeeTaskEnvironment
        {
            internal sealed class Move { internal long Id; internal NavigationTarget Target; internal Action<MovementResult> Done; }
            internal readonly List<Move> Moves = new List<Move>();
            internal bool Gate = true, Lease = true, Show = true, Commit = true, Immediate;
            internal int ShowCalls, Commits, Held, Cleanup;
            internal CafeTaskEvent Arrival;
            internal Action OnShow, OnCommit, OnCleanup;
            public bool IsProgressAllowed => Gate;
            public MoveStartResult MoveTo(NavigationTarget target, Action<MovementResult> completed)
            {
                var move = new Move { Id = Moves.Count + 1, Target=target, Done = completed }; Moves.Add(move);
                if (Immediate) Complete(Moves.Count - 1);
                return new MoveStartResult(true, move.Id, NavigationFailure.None);
            }
            internal void Complete(int index, MovementStatus status = MovementStatus.Arrived)
                => Moves[index].Done(new MovementResult(Moves[index].Id, "employee.test", status,
                    status == MovementStatus.Arrived ? NavigationFailure.None : NavigationFailure.MovementTimeout,
                    NavigationFailure.None, NavigationFailure.None, UnityEngine.Vector3.zero, 0));
            public bool CancelMove(long requestId) => true;
            public bool TryShowHeldCup(out string reason)
            { ShowCalls++; OnShow?.Invoke(); reason = Show ? "" : "missing hand cup"; if (Show) Held = 1; return Show; }
            public bool TryPlaceCup(SurfaceSlotAddress address, out string reason)
            { reason = "P13 placement must not run"; Assert.Fail(reason); return false; }
            public void SetVisualMode(EmployeeVisualMode mode) { }
            public void CleanupTask(long handleId) { Cleanup++; Held = 0; OnCleanup?.Invoke(); }
            public bool TryAcquireDelivery(string actorId) => Lease;
            public bool TryCommitDelivery(CafeTaskEvent arrival, SurfaceSlotAddress address, out string reason)
            { Arrival = arrival; if (Commit) Commits++; OnCommit?.Invoke(); reason = Commit ? "" : "business rejected"; return Commit; }
            public void HideHeldCup() => Held = 0;
        }
        private static CafeTaskToken Token(int attempt = 1, long sequence = 1) => new CafeTaskToken("loop", 1, sequence, attempt);
        private static CafeWorkCheckpoint Work(EmployeeRole role = EmployeeRole.Barista, float seconds = 3,
            CafeWorkStage stage = CafeWorkStage.Working) => new CafeWorkCheckpoint(role, "visit",
                role == EmployeeRole.Barista ? new CafeOrderKey("loop", 1, 1) : (CafeOrderKey?)null, stage, seconds);
        private static EmployeeTaskRunner Ready(EmployeeRole role, out Port port, out List<CafeTaskEvent> events,float? duration=null)
        {
            port = new Port(); var runner = new EmployeeTaskRunner("employee.test", role, port,workDurationSeconds:duration);
            runner.TryAssignBinding(EmployeeTaskEnvironmentFake.Binding(role));
            port.Immediate = true; runner.TryReturnToWork(); port.Immediate = false;
            events = new List<CafeTaskEvent>(); runner.IntegratedTaskEvent += events.Add; return runner;
        }

        [Test] public void ChosenReturnHomeIsProtectedWhileCarryingAndInsideDeliveredCallback()
        {
            var runner=Ready(EmployeeRole.Barista,out var port,out _);port.Lease=false;
            Assert.That(runner.TryStartIntegrated(Token(),Work(stage:CafeWorkStage.Prepared,seconds:0)),Is.True);
            var binding=EmployeeTaskEnvironmentFake.Binding(EmployeeRole.Barista);var side=binding.PickUpTarget.Value;
            var corner=new NavigationTarget(binding.WorkTarget.Position+UnityEngine.Vector3.right*2);
            Assert.That((bool)typeof(EmployeeTaskRunner).GetMethod("TrySelectPickupRoutes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .Invoke(runner,new object[]{side,new[]{side},new[]{corner,binding.WorkTarget}}),Is.True);
            var property=typeof(EmployeeTaskRunner).GetProperty("RemainingPickupReturnRoute",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            IReadOnlyList<NavigationTarget> ReadRoute()=>(IReadOnlyList<NavigationTarget>)property.GetValue(runner);
            port.Lease=true;runner.Tick(.1f);Assert.That(runner.Snapshot.State,Is.EqualTo(EmployeeState.CarryingToPickUp));
            Assert.That(ReadRoute(),Is.Not.Null,"Selected future home already belongs to the carrying attempt");
            Assert.That(ReadRoute().Select(t=>t.Position),Is.EqualTo(new[]{side.Position,corner.Position,binding.WorkTarget.Position}),"Origin is the selected delivery side, never a direct shortcut from the outward pose");
            var delivered=false;runner.IntegratedTaskEvent+=e=>{
                if(e.Kind!=CafeTaskEventKind.Progress || e.Checkpoint.Stage!=CafeWorkStage.Delivered)return;
                delivered=true;Assert.That(ReadRoute(),Is.Not.Null,"Delivered callback before state Returning must keep the same future home");
            };
            port.Complete(1);Assert.That(delivered,Is.True);runner.Cancel("retire");Assert.That(ReadRoute(),Is.Null);
        }

        [Test] public void OneSecondCashierBoundaryAndInterruptedCheckpointFinishExactlyOnce()
        {
            var runner=Ready(EmployeeRole.Cashier,out var port,out var events,1f);
            Assert.That(runner.TryStartIntegrated(Token(),Work(EmployeeRole.Cashier,1)),Is.True);
            runner.Tick(.99f);Assert.That(events.Any(e=>e.Kind==CafeTaskEventKind.ServiceFinished),Is.False);
            port.Gate=false;runner.Tick(100);Assert.That(runner.CaptureCheckpoint().RemainingWorkSeconds,Is.EqualTo(.01f).Within(.000001f));
            runner.Cancel("disable");var checkpoint=runner.CaptureCheckpoint();
            var restored=Ready(EmployeeRole.Cashier,out _,out var restoredEvents,1f);
            Assert.That(restored.TryStartIntegrated(Token(2),checkpoint),Is.True);
            restored.Tick(.01f);restored.Tick(100);
            Assert.That(restoredEvents.Count(e=>e.Kind==CafeTaskEventKind.ServiceFinished),Is.EqualTo(1));
            Assert.That(restored.CaptureCheckpoint().Stage,Is.EqualTo(CafeWorkStage.Finished));
        }

        [Test] public void UnissuedReturnLegWaitsForActualTrafficAndFreezesOnPauseWithoutNewRequest()
        {
            var runner=Ready(EmployeeRole.Barista,out var port,out var events);port.Lease=false;
            Assert.That(runner.TryStartIntegrated(Token(),Work(stage:CafeWorkStage.Prepared,seconds:0)),Is.True);
            var binding=EmployeeTaskEnvironmentFake.Binding(EmployeeRole.Barista);var side=binding.PickUpTarget.Value;
            var corner=new NavigationTarget(binding.WorkTarget.Position+UnityEngine.Vector3.right*2);
            Assert.That((bool)typeof(EmployeeTaskRunner).GetMethod("TrySelectPickupRoutes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .Invoke(runner,new object[]{side,new[]{side},new[]{corner,binding.WorkTarget}}),Is.True);
            var wait=true;var queries=0;
            typeof(EmployeeTaskRunner).GetMethod("SetPickupReturnTrafficWait",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .Invoke(runner,new object[]{new Func<NavigationTarget,bool>(t=>{Assert.That(t.Position,Is.EqualTo(corner.Position));queries++;return wait;})});
            port.Lease=true;runner.Tick(.1f);var handle=runner.Snapshot.HandleId;port.Complete(1);
            Assert.That(port.Moves.Count,Is.EqualTo(2));Assert.That(runner.Snapshot.State,Is.EqualTo(EmployeeState.Returning));
            var pausedQueries=queries;runner.Tick(0);port.Gate=false;runner.Tick(10);
            Assert.That(queries,Is.EqualTo(pausedQueries));Assert.That(port.Moves.Count,Is.EqualTo(2));
            port.Gate=true;runner.Tick(.1f);Assert.That(port.Moves.Count,Is.EqualTo(2));Assert.That(runner.Snapshot.HandleId,Is.EqualTo(handle));
            wait=false;runner.Tick(.1f);Assert.That(port.Moves[2].Target.Position,Is.EqualTo(corner.Position));
            typeof(EmployeeTaskRunner).GetMethod("SetPickupReturnTrafficWait",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(runner,new object[]{null});
            port.Complete(2);Assert.That(port.Moves[3].Target.Position,Is.EqualTo(binding.WorkTarget.Position));
            Assert.That(events.Any(e=>e.Kind==CafeTaskEventKind.Returned),Is.False);port.Complete(3);
            Assert.That(events.Count(e=>e.Kind==CafeTaskEventKind.Returned),Is.EqualTo(1));Assert.That(events.Last().RequestId,Is.EqualTo(4));
            Assert.That(port.Commits,Is.EqualTo(1));Assert.That(port.ShowCalls,Is.EqualTo(1));
        }
        [Test] public void ReentrantReturnTrafficQueryRetiresPendingContextAndOldArrivalCannotReviveIt()
        {
            var runner=Ready(EmployeeRole.Barista,out var port,out var events);port.Lease=false;
            Assert.That(runner.TryStartIntegrated(Token(),Work(stage:CafeWorkStage.Prepared,seconds:0)),Is.True);
            var binding=EmployeeTaskEnvironmentFake.Binding(EmployeeRole.Barista);var side=binding.PickUpTarget.Value;
            Assert.That((bool)typeof(EmployeeTaskRunner).GetMethod("TrySelectPickupRoutes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .Invoke(runner,new object[]{side,new[]{side},new[]{binding.WorkTarget}}),Is.True);
            var query=typeof(EmployeeTaskRunner).GetMethod("SetPickupReturnTrafficWait",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            query.Invoke(runner,new object[]{new Func<NavigationTarget,bool>(t=>{runner.Cancel("retire in pure-query caller");return true;})});
            port.Lease=true;runner.Tick(.1f);port.Complete(1);runner.Tick(1);port.Complete(1);
            Assert.That(runner.Snapshot.State,Is.EqualTo(EmployeeState.Blocked));Assert.That(port.Moves.Count,Is.EqualTo(2));
            var saved=runner.CaptureCheckpoint();Assert.That(saved.Stage,Is.EqualTo(CafeWorkStage.Returning));query.Invoke(runner,new object[]{null});
            Assert.That(runner.TryStartIntegrated(Token(2),saved),Is.True);Assert.That(port.Moves.Count,Is.EqualTo(3));
            port.Complete(1);Assert.That(events.Any(e=>e.Kind==CafeTaskEventKind.Returned),Is.False);port.Complete(2);
            Assert.That(events.Count(e=>e.Kind==CafeTaskEventKind.Returned),Is.EqualTo(1));Assert.That(port.Commits,Is.EqualTo(1));Assert.That(port.ShowCalls,Is.EqualTo(1));
        }

        [Test] public void SelectedReturnRouteConsumesEveryLegAndOnlyFinalMachineArrivalFinishes()
        {
            var runner=Ready(EmployeeRole.Barista,out var port,out var events);port.Lease=false;
            Assert.That(runner.TryStartIntegrated(Token(),Work(stage:CafeWorkStage.Prepared,seconds:0)),Is.True);
            var binding=EmployeeTaskEnvironmentFake.Binding(EmployeeRole.Barista);var side=binding.PickUpTarget.Value;
            var corner=new NavigationTarget(binding.WorkTarget.Position+UnityEngine.Vector3.right*2);
            var home=new[]{corner,binding.WorkTarget};
            Assert.That((bool)typeof(EmployeeTaskRunner).GetMethod("TrySelectPickupRoutes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .Invoke(runner,new object[]{side,new[]{side},home}),Is.True);
            home[0]=new NavigationTarget(UnityEngine.Vector3.one*100); // Caller mutation must not replace an owned route.
            port.Lease=true;runner.Tick(.1f);port.Complete(1);
            Assert.That(port.Moves[2].Target.Position,Is.EqualTo(corner.Position),"Actual return must consume the validated detour, not jump directly to CM");
            Assert.That(events.Any(e=>e.Kind==CafeTaskEventKind.Returned),Is.False);
            port.Gate=false;port.Complete(2);runner.Tick(0);
            Assert.That(port.Moves.Count,Is.EqualTo(3));Assert.That(runner.Snapshot.State,Is.EqualTo(EmployeeState.Returning));
            port.Gate=true;runner.Tick(.1f);Assert.That(port.Moves[3].Target.Position,Is.EqualTo(binding.WorkTarget.Position));
            port.Complete(3);Assert.That(runner.Snapshot.State,Is.EqualTo(EmployeeState.Idle));
            Assert.That(events.Count(e=>e.Kind==CafeTaskEventKind.Returned),Is.EqualTo(1));Assert.That(events.Last().RequestId,Is.EqualTo(4));
            Assert.That(port.Commits,Is.EqualTo(1));Assert.That(port.ShowCalls,Is.EqualTo(1));
        }
        [Test] public void RestoredReturningUsesFreshActualRouteWithoutAnotherPreparationOrDelivery()
        {
            var runner=Ready(EmployeeRole.Barista,out var port,out var events);
            Assert.That(runner.TryStartIntegrated(Token(),Work()),Is.True);runner.Tick(3);port.Complete(1);
            runner.Cancel("return interrupted");var saved=runner.CaptureCheckpoint();
            var binding=EmployeeTaskEnvironmentFake.Binding(EmployeeRole.Barista);
            var corner=new NavigationTarget(binding.WorkTarget.Position+UnityEngine.Vector3.forward*2);
            Assert.That((bool)typeof(EmployeeTaskRunner).GetMethod("ConfigurePickupReturnRoute",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .Invoke(runner,new object[]{new[]{corner,binding.WorkTarget}}),Is.True);
            Assert.That(runner.TryStartIntegrated(Token(2),saved),Is.True);
            Assert.That(port.Moves.Last().Target.Position,Is.EqualTo(corner.Position));
            port.Complete(3);Assert.That(runner.Snapshot.State,Is.EqualTo(EmployeeState.Returning));port.Complete(4);
            Assert.That(runner.Snapshot.State,Is.EqualTo(EmployeeState.Idle));
            Assert.That(port.Commits,Is.EqualTo(1));Assert.That(port.ShowCalls,Is.EqualTo(1));
            Assert.That(events.Count(e=>e.Kind==CafeTaskEventKind.Returned),Is.EqualTo(1));
        }

        [Test] public void WorkingResumesRemainingSeconds()
        {
            var runner = Ready(EmployeeRole.Barista, out var port, out var events);
            Assert.That(runner.TryStartIntegrated(Token(), Work()), Is.True); runner.Tick(1.25f);
            runner.Cancel("interrupted"); var saved = runner.CaptureCheckpoint();
            Assert.That(saved.RemainingWorkSeconds, Is.EqualTo(1.75f));
            Assert.That(events.Last().Kind, Is.EqualTo(CafeTaskEventKind.Blocked));
            Assert.That(runner.TryStartIntegrated(Token(2), saved), Is.True);
            runner.Tick(1.5f); Assert.That(port.ShowCalls, Is.Zero);
            runner.Tick(.25f); Assert.That(port.ShowCalls, Is.EqualTo(1));
            Assert.That(runner.CaptureCheckpoint().Stage, Is.EqualTo(CafeWorkStage.Prepared));
        }
        [TestCase(EmployeeRole.Cashier,2f)] [TestCase(EmployeeRole.Barista,3f)]
        public void IntegratedDurationBoundaryAndOversizedDeltaEmitOnlyOneMilestone(EmployeeRole role,float duration)
        {
            var runner=Ready(role,out var port,out var events);
            Assert.That(runner.TryStartIntegrated(Token(),Work(role,duration)),Is.True);
            runner.Tick(duration-.01f);
            Assert.That(runner.CaptureCheckpoint().Stage,Is.EqualTo(CafeWorkStage.Working));
            Assert.That(events.Exists(e=>e.Kind==CafeTaskEventKind.ServiceFinished || e.Kind==CafeTaskEventKind.Prepared),Is.False);
            runner.Tick(.01f); runner.Tick(100);
            var kind=role==EmployeeRole.Cashier?CafeTaskEventKind.ServiceFinished:CafeTaskEventKind.Prepared;
            Assert.That(events.FindAll(e=>e.Kind==kind).Count,Is.EqualTo(1));
            Assert.That(port.ShowCalls,Is.EqualTo(role==EmployeeRole.Barista?1:0));
            var second=Ready(role,out var otherPort,out var otherEvents);
            Assert.That(second.TryStartIntegrated(Token(),Work(role,duration)),Is.True); second.Tick(100); second.Tick(100);
            Assert.That(otherEvents.FindAll(e=>e.Kind==kind).Count,Is.EqualTo(1));
            Assert.That(otherPort.ShowCalls,Is.EqualTo(role==EmployeeRole.Barista?1:0));
        }
        [Test] public void PreparedFailureDoesNotPrepareAgain()
        {
            var runner = Ready(EmployeeRole.Barista, out var port, out var events);
            Assert.That(runner.TryStartIntegrated(Token(), Work()), Is.True); runner.Tick(3);
            port.Complete(1, MovementStatus.Failed); var saved = runner.CaptureCheckpoint();
            Assert.That(saved.Stage, Is.EqualTo(CafeWorkStage.Prepared)); Assert.That(port.Held, Is.Zero);
            Assert.That(runner.TryStartIntegrated(Token(2), saved), Is.True);
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.CarryingToPickUp));
            Assert.That(port.Moves.Count, Is.EqualTo(3), "Recovery goes directly to pickup without CM detour");
            Assert.That(events.Count(e => e.Kind == CafeTaskEventKind.Prepared), Is.EqualTo(1));
            port.Complete(2); Assert.That(port.Commits, Is.EqualTo(1));
        }
        [Test] public void DeliveredReturnFailureCannotDeliverAgain()
        {
            var runner = Ready(EmployeeRole.Barista, out var port, out var events);
            Assert.That(runner.TryStartIntegrated(Token(), Work()), Is.True); runner.Tick(3); port.Complete(1);
            port.Complete(2, MovementStatus.Failed); var saved = runner.CaptureCheckpoint();
            Assert.That(saved.Stage, Is.EqualTo(CafeWorkStage.Returning));
            Assert.That(runner.TryStartIntegrated(Token(2), saved), Is.True); port.Complete(3);
            Assert.That(port.Commits, Is.EqualTo(1)); Assert.That(port.ShowCalls, Is.EqualTo(1));
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Idle));
            Assert.That(events.Count(e => e.Kind == CafeTaskEventKind.Returned), Is.EqualTo(1));
        }
        [Test] public void DisableCapturesBeforeCleanup()
        {
            var runner = Ready(EmployeeRole.Barista, out var port, out var events);
            Assert.That(runner.TryStartIntegrated(Token(), Work()), Is.True); runner.Tick(.75f);
            port.OnCleanup = () => Assert.That(runner.CaptureCheckpoint().RemainingWorkSeconds, Is.EqualTo(2.25f));
            runner.Dispose(); Assert.That(runner.CaptureCheckpoint().RemainingWorkSeconds, Is.EqualTo(2.25f));
            Assert.That(events.Last().Kind, Is.EqualTo(CafeTaskEventKind.Blocked));
        }
        [Test] public void LateOldHandleDoesNotAdvanceRestoredTask()
        {
            var runner = Ready(EmployeeRole.Barista, out var port, out _);
            Assert.That(runner.TryStartIntegrated(Token(), Work()), Is.True); runner.Tick(3);
            var oldHandle = runner.Snapshot.HandleId; runner.Cancel("retry");
            Assert.That(runner.TryStartIntegrated(Token(2), runner.CaptureCheckpoint()), Is.True);
            Assert.That(runner.Snapshot.HandleId, Is.GreaterThan(oldHandle));
            port.Complete(1); Assert.That(port.Commits, Is.Zero); port.Complete(2); Assert.That(port.Commits, Is.EqualTo(1));
        }
        [Test] public void CashierFinishedRetriesSubmissionWithoutWorkingAgain()
        {
            var runner = Ready(EmployeeRole.Cashier, out var port, out var events);
            Assert.That(runner.TryStartIntegrated(Token(), Work(EmployeeRole.Cashier, 2)), Is.True);
            runner.Tick(2); for (var i = 0; i < 3; i++) runner.Tick(20);
            Assert.That(events.Count(e => e.Kind == CafeTaskEventKind.ServiceFinished), Is.EqualTo(1));
            var saved = runner.CaptureCheckpoint(); Assert.That(saved.Stage, Is.EqualTo(CafeWorkStage.Finished));
            Assert.That(runner.TryStartIntegrated(Token(2), saved), Is.True);
            Assert.That(events.Count(e => e.Kind == CafeTaskEventKind.ServiceFinished), Is.EqualTo(2));
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Idle)); Assert.That(port.ShowCalls, Is.Zero);
        }
        [Test] public void SynchronousMoveCompletionCannotOverwriteAdvancedState()
        {
            var runner = Ready(EmployeeRole.Barista, out var port, out var events);
            Assert.That(runner.TryStartIntegrated(Token(), Work()), Is.True); port.Immediate = true;
            runner.Tick(3); Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Idle));
            Assert.That(port.Commits, Is.EqualTo(1)); Assert.That(events.Last().Kind, Is.EqualTo(CafeTaskEventKind.Returned));
        }
        [Test] public void SharedDeliveryLeaseWaitsWithoutRepeatingPreparationOrVisual()
        {
            var runner = Ready(EmployeeRole.Barista, out var port, out var events); port.Lease = false;
            Assert.That(runner.TryStartIntegrated(Token(), Work()), Is.True); runner.Tick(3);
            for (var i = 0; i < 5; i++) runner.Tick(.2f);
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.WaitingToDeliver));
            Assert.That(port.ShowCalls, Is.EqualTo(1)); Assert.That(port.Moves.Count, Is.EqualTo(1));
            Assert.That(events.Any(e => e.Kind == CafeTaskEventKind.Blocked), Is.False);
            port.Lease = true; runner.Tick(.1f); Assert.That(port.Moves.Count, Is.EqualTo(2));
            Assert.That(runner.HasSelectedDeliveryRoute,Is.False,"External integrated gate retains its existing single-leg contract without a CFC-owned route");
        }
        [Test] public void HeldVisualFailureKeepsPreparedBeforePortCleanup()
        {
            var runner = Ready(EmployeeRole.Barista, out var port, out var events); port.Show = false;
            port.OnShow = () => Assert.That(runner.CaptureCheckpoint().Stage, Is.EqualTo(CafeWorkStage.Prepared));
            Assert.That(runner.TryStartIntegrated(Token(), Work()), Is.True); runner.Tick(3);
            Assert.That(runner.CaptureCheckpoint().Stage, Is.EqualTo(CafeWorkStage.Prepared));
            Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Blocked));
            port.Show = true; Assert.That(runner.TryStartIntegrated(Token(2), runner.CaptureCheckpoint()), Is.True);
            Assert.That(events.Count(e => e.Kind == CafeTaskEventKind.Prepared), Is.EqualTo(1));
        }
        [Test] public void PausedArrivalCommitsOnceOnlyAfterPositiveGameDelta()
        {
            var runner = Ready(EmployeeRole.Barista, out var port, out _);
            Assert.That(runner.TryStartIntegrated(Token(), Work()), Is.True); runner.Tick(3);
            port.Gate = false; port.Complete(1); runner.Tick(10); Assert.That(port.Commits, Is.Zero);
            port.Gate = true; runner.Tick(0); Assert.That(port.Commits, Is.Zero); runner.Tick(.1f);
            port.Complete(1); Assert.That(port.Commits, Is.EqualTo(1));
        }
        [Test] public void ArrivalCarriesCurrentIdentityOnlyInsideCommitWindow()
        {
            var runner = Ready(EmployeeRole.Barista, out var port, out var events);
            Assert.That(runner.TryStartIntegrated(Token(), Work()), Is.True); runner.Tick(3);
            port.OnCommit = () => {
                Assert.That(runner.IsCurrentDeliveryArrival(port.Arrival), Is.True);
                Assert.That(events.Last().Kind, Is.EqualTo(CafeTaskEventKind.DeliveryArrived));
                Assert.That(port.Arrival.RequestId, Is.EqualTo(2));
                Assert.That(port.Arrival.HandleId, Is.GreaterThan(0));
                Assert.That(port.Arrival.Generation, Is.GreaterThan(0));
                Assert.That(port.Arrival.LayoutRevision, Is.EqualTo(7));
            };
            port.Complete(1); Assert.That(runner.IsCurrentDeliveryArrival(port.Arrival), Is.False);
        }
        [Test] public void RejectedCommitKeepsPreparedAndItsReason()
        {
            var runner = Ready(EmployeeRole.Barista, out var port, out _); port.Commit = false;
            Assert.That(runner.TryStartIntegrated(Token(), Work()), Is.True); runner.Tick(3); port.Complete(1);
            Assert.That(runner.CaptureCheckpoint().Stage, Is.EqualTo(CafeWorkStage.Prepared));
            Assert.That(runner.Snapshot.Failure, Is.EqualTo("business rejected")); Assert.That(runner.Snapshot.Delivered, Is.False);
        }
        [Test] public void ReentrantDisableAfterBusinessCommitDoesNotReviveReturning()
        {
            var runner = Ready(EmployeeRole.Barista, out var port, out _);
            Assert.That(runner.TryStartIntegrated(Token(), Work()), Is.True); runner.Tick(3);
            port.OnCommit = runner.Dispose; port.Complete(1);
            Assert.That(port.Commits, Is.EqualTo(1)); Assert.That(runner.Snapshot.State, Is.EqualTo(EmployeeState.Disabled));
            Assert.That(port.Moves.Count, Is.EqualTo(2));
            // Service owns the successful business fact; restore from its Delivered checkpoint.
            var resumed = Ready(EmployeeRole.Barista, out var newPort, out _);
            Assert.That(resumed.TryStartIntegrated(Token(2), Work(stage: CafeWorkStage.Delivered, seconds: 0)), Is.True);
            Assert.That(resumed.Snapshot.State, Is.EqualTo(EmployeeState.Returning)); Assert.That(newPort.ShowCalls, Is.Zero);
        }
        [Test] public void DeliveryRouteOwnsCopiedSegmentsAndOnlyFinalActualRequestCanCommit()
        {
            var runner=Ready(EmployeeRole.Barista,out var port,out var events); port.Lease=false;
            Assert.That(runner.TryStartIntegrated(Token(),Work()),Is.True); runner.Tick(3);
            var final=new NavigationTarget(new UnityEngine.Vector3(4,0,0));
            var route=new[]{new NavigationTarget(new UnityEngine.Vector3(1,0,0)),new NavigationTarget(new UnityEngine.Vector3(2,0,0)),final};
            Assert.That(runner.HasSelectedDeliveryRoute,Is.False);
            Assert.That(runner.TrySelectDeliveryTarget(final,route),Is.True); Assert.That(runner.HasSelectedDeliveryRoute,Is.True); route[0]=final;
            port.Lease=true; runner.Tick(.1f); var handle=runner.Snapshot.HandleId;
            Assert.That(port.Moves[1].Target.Position.x,Is.EqualTo(1));
            port.Complete(1); port.Complete(1); Assert.That(port.Moves.Count,Is.EqualTo(3)); Assert.That(port.Commits,Is.Zero);
            port.Gate=false; port.Complete(2); runner.Tick(.1f); Assert.That(port.Moves.Count,Is.EqualTo(3));
            port.Gate=true; runner.Tick(0); Assert.That(port.Moves.Count,Is.EqualTo(3)); runner.Tick(.1f);
            Assert.That(port.Moves[3].Target.Position,Is.EqualTo(final.Position)); Assert.That(port.Commits,Is.Zero);
            port.Complete(3); Assert.That(port.Commits,Is.EqualTo(1));
            var arrival=events.Single(e=>e.Kind==CafeTaskEventKind.DeliveryArrived);
            Assert.That(arrival.RequestId,Is.EqualTo(port.Moves[3].Id)); Assert.That(arrival.HandleId,Is.EqualTo(handle));
            Assert.That(events.Count(e=>e.Kind==CafeTaskEventKind.Prepared),Is.EqualTo(1));
        }
        [Test] public void SuspendedDeliveryRouteResumesCurrentSegmentAndRetirementRejectsOldArrivals()
        {
            var runner=Ready(EmployeeRole.Barista,out var port,out var events); port.Lease=false;
            Assert.That(runner.TryStartIntegrated(Token(),Work()),Is.True); runner.Tick(3);
            var first=new NavigationTarget(new UnityEngine.Vector3(1,0,0)); var final=new NavigationTarget(new UnityEngine.Vector3(4,0,0));
            Assert.That(runner.TrySelectDeliveryTarget(final,new[]{first,final}),Is.True); port.Lease=true; runner.Tick(.1f);
            port.Gate=false; port.Complete(1,MovementStatus.LayoutChanged); port.Gate=true; runner.Tick(.1f);
            Assert.That(port.Moves[2].Target.Position,Is.EqualTo(first.Position),"Same-revision resume must not skip to the final side");
            port.Complete(1); Assert.That(port.Moves.Count,Is.EqualTo(3));
            port.Complete(2); Assert.That(port.Moves[3].Target.Position,Is.EqualTo(final.Position)); Assert.That(port.Commits,Is.Zero);
            runner.Cancel("explicit hold"); Assert.That(runner.HasSelectedDeliveryRoute,Is.False); var checkpoint=runner.CaptureCheckpoint();
            port.Complete(3); Assert.That(port.Commits,Is.Zero); port.Lease=false;
            Assert.That(runner.TryStartIntegrated(Token(2),checkpoint),Is.True); Assert.That(runner.HasSelectedDeliveryRoute,Is.False);
            Assert.That(runner.TrySelectDeliveryTarget(final,new[]{final}),Is.True); port.Lease=true; runner.Tick(.1f);
            Assert.That(port.Moves[4].Target.Position,Is.EqualTo(final.Position)); port.Complete(3); Assert.That(port.Commits,Is.Zero);
            port.Complete(4); Assert.That(port.Commits,Is.EqualTo(1));
            Assert.That(events.Count(e=>e.Kind==CafeTaskEventKind.Prepared),Is.EqualTo(1));
        }
        [Test] public void DeliveryRouteRejectsOversizedOrWrongFinalAndKeepsSelectedSideStable()
        {
            var runner=Ready(EmployeeRole.Barista,out var port,out _); port.Lease=false;
            Assert.That(runner.TryStartIntegrated(Token(),Work()),Is.True); runner.Tick(3);
            var final=new NavigationTarget(new UnityEngine.Vector3(4,0,0));
            Assert.That(runner.TrySelectDeliveryTarget(final,Enumerable.Repeat(final,25).ToArray()),Is.False);
            Assert.That(runner.TrySelectDeliveryTarget(final,new[]{new NavigationTarget(UnityEngine.Vector3.zero)}),Is.False);
            Assert.That(runner.TrySelectDeliveryTarget(final,new[]{final}),Is.True);
            Assert.That(runner.TrySelectDeliveryTarget(new NavigationTarget(UnityEngine.Vector3.zero)),Is.False);
        }
        [Test] public void DefaultAndMismatchedBusinessIdentitiesCannotStart()
        {
            var runner = Ready(EmployeeRole.Barista, out _, out _);
            Assert.That(runner.TryStartIntegrated(default, Work()), Is.False);
            Assert.That(runner.TryStartIntegrated(Token(), default), Is.False);
            Assert.That(runner.TryStartIntegrated(Token(), Work(EmployeeRole.Cashier, 2)), Is.False);
            Assert.That(runner.TryStartIntegrated(Token(), new CafeWorkCheckpoint(EmployeeRole.Barista, "visit",
                new CafeOrderKey("other", 1, 1), CafeWorkStage.Working, 3)), Is.False);
        }
        [Test] public void RestoredTaskRejectsOldAttemptAndWrongVisit()
        {
            var runner = Ready(EmployeeRole.Barista, out _, out _);
            Assert.That(runner.TryStartIntegrated(Token(), Work()), Is.True); runner.Cancel("hold");
            Assert.That(runner.TryStartIntegrated(Token(), runner.CaptureCheckpoint()), Is.False);
            Assert.That(runner.TryStartIntegrated(Token(2), new CafeWorkCheckpoint(EmployeeRole.Barista,
                "different", new CafeOrderKey("loop", 1, 1), CafeWorkStage.Prepared, 0)), Is.False);
            Assert.That(runner.TryStartIntegrated(Token(2), runner.CaptureCheckpoint()), Is.True);
        }
    }
}

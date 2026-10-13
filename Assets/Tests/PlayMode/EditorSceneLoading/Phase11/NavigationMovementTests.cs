using System.Collections;
using System.Collections.Generic;
using AnimalCafe.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AnimalCafe.Tests.PlayMode.Phase11
{
    public sealed class NavigationMovementTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<Collider> solids = new List<Collider>();
        private readonly List<NavMeshBuildSource> sources = new List<NavMeshBuildSource>();
        private NavMeshData data;
        private NavMeshDataInstance instance;
        private NavigationWorld world;
        private float savedCapture, savedScale;

        [SetUp] public void Setup()
        {
            savedCapture = Time.captureDeltaTime; savedScale = Time.timeScale;
            Time.timeScale = 1; Time.captureDeltaTime = 1f / 60;
            var go = New("World"); world = go.AddComponent<NavigationWorld>(); world.enabled = false;
            Box(new Vector3(0, -.1f, 0), new Vector3(12, .2f, 12), false);
        }

        [TearDown] public void Cleanup()
        {
            Object.DestroyImmediate(world.gameObject);
            foreach (var go in objects) if (go != null) Object.DestroyImmediate(go);
            objects.Clear(); solids.Clear(); sources.Clear(); instance.Remove();
            if (data != null) Object.DestroyImmediate(data);
            Time.captureDeltaTime = savedCapture; Time.timeScale = savedScale;
        }

        [UnityTest] public IEnumerator CompletePathArrives()
        {
            Bake(); var actor = Actor("a", new Vector3(-2, 0, 0));
            Assert.That(world.Register(actor), Is.True);
            MovementResult? result = null;
            world.Service.MoveTo("a", new NavigationTarget(new Vector3(2, 0, 0), Vector3.back), r => result = r);
            yield return Run(() => result.HasValue);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Arrived), result.Value.Reason + " at " + actor.transform.position);
            Assert.That(Vector3.Distance(actor.transform.position, new Vector3(2, 0, 0)), Is.LessThanOrEqualTo(.08f));
            Assert.That(Vector3.Angle(actor.transform.forward, Vector3.back), Is.LessThanOrEqualTo(5));
        }

        [UnityTest] public IEnumerator RuntimeInstancesRegisterWithDistinctIdsAndCannotRenameWhileRegistered()
        {
            Bake(); var first = Actor("authored-a", new Vector3(-2, 0, 0));
            var second = Actor("authored-b", new Vector3(2, 0, 0));
            Assert.That(first.TryInitializeRuntimeId("visit-a"), Is.True);
            Assert.That(second.TryInitializeRuntimeId("visit-b"), Is.True);
            Assert.That(world.Register(first), Is.True); Assert.That(world.Register(second), Is.True);
            Assert.That(first.TryInitializeRuntimeId("visit-c"), Is.False);
            world.Unregister(first);
            Assert.That(first.TryInitializeRuntimeId("visit-c"), Is.False);
            Assert.That(first.ActorId, Is.EqualTo("visit-a"));
            yield return null;
        }

        [UnityTest] public IEnumerator PendingOrPartialNeverArrives()
        {
            Box(new Vector3(0, .7f, 0), new Vector3(.3f, 1.4f, 14), true); Bake();
            var actor = Actor("a", new Vector3(-2, 0, 0)); Assert.That(world.Register(actor), Is.True);
            MovementResult? result = null;
            world.Service.MoveTo("a", new NavigationTarget(new Vector3(2, 0, 0)), r => result = r);
            yield return Run(() => result.HasValue);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Failed));
            Assert.That(result.Value.Reason, Is.EqualTo(NavigationFailure.IncompletePath));
            Assert.That(actor.transform.position, Is.EqualTo(new Vector3(-2, 0, 0)));
        }

        [UnityTest] public IEnumerator OneMetreCorridorPasses()
        {
            Corridor(1); Bake(); var actor = Actor("a", new Vector3(0, 0, -3));
            Assert.That(world.Register(actor), Is.True); MovementResult? result = null;
            world.Service.MoveTo("a", new NavigationTarget(new Vector3(0, 0, 3)), r => result = r);
            yield return Run(() => result.HasValue);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Arrived), result.Value.Reason + " at " + actor.transform.position);
        }

        [UnityTest] public IEnumerator Point85CorridorFails()
        {
            Corridor(.85f); Bake(); var actor = Actor("a", new Vector3(0, 0, -3));
            Assert.That(world.Register(actor), Is.True); MovementResult? result = null;
            world.Service.MoveTo("a", new NavigationTarget(new Vector3(0, 0, 3)), r => result = r);
            yield return Run(() => result.HasValue);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Failed));
        }

        [UnityTest] public IEnumerator WideDualActorsArriveWithoutCrossingSweeps()
        {
            Bake(); yield return Dual(true);
        }

        [UnityTest] public IEnumerator NarrowDualActorsFailSafely()
        {
            Corridor(1); Bake(); yield return Dual(false);
        }

        [UnityTest] public IEnumerator SameSideSamplingOnly()
        {
            Bake();
            var requested = OutsideEdge();
            Assert.That(NavMesh.SamplePosition(requested, out var sample, .15f, NavMesh.AllAreas), Is.True);
            Assert.That(Vector3.Distance(requested, sample.position), Is.GreaterThan(.08f));
            var midpoint = (requested + sample.position) * .5f; midpoint.y = .65f;
            Box(midpoint, new Vector3(.02f, 1.3f, 4), true);
            world.SetGeometry(solids, 2); // Thin registered wall lies strictly between target and sample.
            var actor = Actor("a", new Vector3(-2, 0, 0)); Assert.That(world.Register(actor), Is.True);
            // Public World route supplies the registered divider to its driver.
            MovementResult? result = null;
            world.Service.MoveTo("a", new NavigationTarget(requested), r => result = r);
            yield return Run(() => result.HasValue);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Failed));
            Assert.That(result.Value.Reason, Is.EqualTo(NavigationFailure.InvalidTarget));
        }

        [UnityTest] public IEnumerator VerifiedSampleIsUsedForArrival()
        {
            Bake(); var actor = Actor("a", new Vector3(4, 0, 0)); Assert.That(world.Register(actor), Is.True);
            var requested = OutsideEdge();
            Assert.That(NavMesh.SamplePosition(requested, out var sample, .15f, NavMesh.AllAreas), Is.True);
            MovementResult? result = null;
            world.Service.MoveTo("a", new NavigationTarget(requested), r => result = r);
            yield return Run(() => result.HasValue);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Arrived));
            Assert.That(Vector3.Distance(actor.transform.position, sample.position), Is.LessThanOrEqualTo(.08f));
            Assert.That(Vector3.Distance(actor.transform.position, requested), Is.GreaterThan(.08f));
        }

        [Test] public void LayoutChangedCallbackUsesNewGeometryImmediately()
        {
            Bake(); var actor = Actor("a", new Vector3(4, 0, 0)); Assert.That(world.Register(actor), Is.True);
            var requested = OutsideEdge();
            Assert.That(NavMesh.SamplePosition(requested, out var sample, .15f, NavMesh.AllAreas), Is.True);
            var midpoint = (requested + sample.position) * .5f; midpoint.y = .65f;
            MovementResult? original = null, replacement = null;
            MoveStartResult replacementStart = default;
            var initial = world.Service.MoveTo("a", new NavigationTarget(new Vector3(3, 0, 0)), result =>
            {
                original = result;
                if (result.Status == MovementStatus.LayoutChanged)
                    replacementStart = world.Service.MoveTo("a", new NavigationTarget(requested), next => replacement = next);
            });
            Assert.That(initial.Accepted, Is.True); Assert.That(original.HasValue, Is.False);
            Box(midpoint, new Vector3(.02f, 1.3f, 4), true);
            world.SetGeometry(solids, 2);
            Assert.That(original.Value.Status, Is.EqualTo(MovementStatus.LayoutChanged));
            Assert.That(replacementStart.Accepted, Is.True);
            Assert.That(replacement.HasValue, Is.True, "The reentrant request must immediately validate against the newly published divider");
            Assert.That(replacement.Value.Status, Is.EqualTo(MovementStatus.Failed));
            Assert.That(replacement.Value.Reason, Is.EqualTo(NavigationFailure.InvalidTarget));
            Assert.That(actor.transform.position, Is.EqualTo(new Vector3(4, 0, 0)));
        }

        [Test] public void SamplingCannotLeaveAllowedRegionOrChangeHeight()
        {
            Bake(); var actor = Actor("a", new Vector3(4, 0, 0)); Assert.That(world.Register(actor), Is.True);
            var requested = OutsideEdge(); MovementResult? result = null;
            world.Service.MoveTo("a", new NavigationTarget(requested, null, new Bounds(requested, new Vector3(.02f, .1f, 1))), r => result = r);
            Assert.That(result.Value.Reason, Is.EqualTo(NavigationFailure.InvalidTarget));
            result = null;
            world.Service.MoveTo("a", new NavigationTarget(new Vector3(3, .12f, 0)), r => result = r);
            Assert.That(result.Value.Reason, Is.EqualTo(NavigationFailure.InvalidTarget));
        }

        [UnityTest] public IEnumerator FailedActorRemainsAnObstacle()
        {
            Bake(); var a = Actor("a", Vector3.zero); var b = Actor("b", Vector3.left * 2);
            Assert.That(world.Register(a), Is.True); Assert.That(world.Register(b), Is.True);
            MovementResult? ra = null, rb = null;
            world.Service.MoveTo("a", new NavigationTarget(Vector3.right * 20), r => ra = r);
            Assert.That(ra.Value.Status, Is.EqualTo(MovementStatus.Failed));
            world.Service.MoveTo("b", new NavigationTarget(Vector3.right * 2), r => rb = r);
            for (var frame = 0; frame < 1800 && !rb.HasValue; frame++)
            {
                yield return null; var before = b.transform.position; world.Step(1f / 60);
                var delta = b.transform.position - before;
                var t = delta.sqrMagnitude < 1e-12f ? 0 : Mathf.Clamp01(-Vector3.Dot(before, delta) / delta.sqrMagnitude);
                Assert.That((before + t * delta).magnitude, Is.GreaterThanOrEqualTo(.899f));
                Assert.That(a.transform.position, Is.EqualTo(Vector3.zero));
            }
            Assert.That(rb.HasValue, Is.True); Assert.That(rb.Value.Status, Is.EqualTo(MovementStatus.Arrived));
        }

        [UnityTest] public IEnumerator DestroyedActorCompletesOnceAndUnregisters()
        {
            Bake(); var actor = Actor("a", Vector3.zero); Assert.That(world.Register(actor), Is.True);
            var callbacks = 0; MovementResult result = default;
            world.Service.MoveTo("a", new NavigationTarget(Vector3.right * 2), r => { callbacks++; result = r; });
            Object.Destroy(actor.gameObject); yield return null; world.Step(1f / 60);
            Assert.That(callbacks, Is.EqualTo(1)); Assert.That(result.Reason, Is.EqualTo(NavigationFailure.ActorUnavailable));
            Assert.That(world.Service.MoveTo("a", new NavigationTarget(Vector3.zero), _ => { }).Reason, Is.EqualTo(NavigationFailure.UnknownActor));
        }

        [UnityTest] public IEnumerator UnregisterCallbackCanRegisterSameActorAndKeepFreshMovement()
        {
            Bake(); var actor = Actor("a", new Vector3(-2, 0, 0));
            Assert.That(world.Register(actor), Is.True);
            var oldCallbacks = 0; MovementResult oldResult = default;
            var callbackRegistered = false; MoveStartResult freshStart = default;
            MovementResult? freshResult = null;
            var first = world.Service.MoveTo("a", new NavigationTarget(new Vector3(2, 0, 0)), result =>
            {
                oldCallbacks++; oldResult = result;
                // A completion callback may retire the old registration and start a new one.
                world.Unregister(actor);
                callbackRegistered = world.Register(actor);
                if (callbackRegistered)
                    freshStart = world.Service.MoveTo("a", new NavigationTarget(new Vector3(1, 0, 0)), r => freshResult = r);
            });
            Assert.That(first.Accepted, Is.True);
            world.Unregister(actor);
            Assert.That(oldCallbacks, Is.EqualTo(1));
            Assert.That(oldResult.Reason, Is.EqualTo(NavigationFailure.ActorUnavailable));
            Assert.That(callbackRegistered, Is.True, "The old registration must be gone before its terminal callback");
            Assert.That(freshStart.Accepted, Is.True);
            Assert.That(world.RegisteredActors, Has.Member(actor), "The outer unregister must not remove the new registration");
            Assert.That(freshResult.HasValue, Is.False, "The outer unregister must not terminate the fresh request");
            var start = actor.transform.position;
            yield return Run(() => freshResult.HasValue);
            Assert.That(freshResult.Value.Status, Is.EqualTo(MovementStatus.Arrived), freshResult.Value.Reason.ToString());
            Assert.That(Vector3.Distance(start, actor.transform.position), Is.GreaterThan(1));
            Assert.That(world.RegisteredActors, Has.Member(actor));
            Assert.That(oldCallbacks, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator RealPrefabWalkResumesAfterGameObjectReactivation()
        {
            Bake(); var actor = Actor("a", new Vector3(-2, 0, 0));
            Assert.That(world.Register(actor), Is.True);
            var animator = actor.ModelAnimator;
            var presenter = actor.GetComponent<NavigationWalkPresenter>();
            var oldCallbacks = 0;
            Assert.That(world.Service.MoveTo("a", new NavigationTarget(new Vector3(2, 0, 0)), _ => oldCallbacks++).Accepted, Is.True);
            for (var i = 0; i < 20; i++) { yield return null; world.Step(1f / 60); }
            animator.Update(0);
            Assert.That(presenter.ActualSpeed, Is.GreaterThan(.05f));
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"), Is.True);
            var start = actor.transform.position;
            actor.gameObject.SetActive(false);
            Assert.That(oldCallbacks, Is.EqualTo(1));
            actor.gameObject.SetActive(true);
            Assert.That(actor.transform.position, Is.EqualTo(start), "Reactivation must preserve the actual start");
            MovementResult? fresh = null;
            Assert.That(world.Service.MoveTo("a", new NavigationTarget(new Vector3(2, 0, 0)), r => fresh = r).Accepted, Is.True);
            for (var i = 0; i < 5; i++) { yield return null; world.Step(1f / 60); }
            animator.Update(0);
            Assert.That(Vector3.Distance(start, actor.transform.position), Is.GreaterThan(.01f));
            Assert.That(presenter.ActualSpeed, Is.GreaterThan(.05f));
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"), Is.True,
                "An active moving prefab must show Walk after reactivation");
            yield return Run(() => fresh.HasValue);
            Assert.That(fresh.Value.Status, Is.EqualTo(MovementStatus.Arrived));
            Assert.That(oldCallbacks, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator DisableComponentDuringTravelEndsOnce() => DisableActor(false, false);
        [UnityTest] public IEnumerator DisableComponentWhilePausedEndsOnce() => DisableActor(false, true);
        [UnityTest] public IEnumerator DisableGameObjectDuringTravelEndsOnce() => DisableActor(true, false);
        [UnityTest] public IEnumerator DisableGameObjectWhilePausedEndsOnce() => DisableActor(true, true);
        private IEnumerator DisableActor(bool wholeObject, bool paused)
        {
            Bake(); var actor = Actor("a", new Vector3(-2, 0, 0)); Assert.That(world.Register(actor), Is.True);
            var callbacks = 0; MovementResult result = default;
            var request = world.Service.MoveTo("a", new NavigationTarget(Vector3.right * 2, Vector3.back),
                r => {
                    callbacks++; result = r;
                    Assert.That(actor.GetComponent<NavigationWalkPresenter>().ActualSpeed, Is.Zero);
                    Assert.That(actor.Agent.enabled, Is.False, "Native stop precedes callback");
                    var reentrant = world.Service.MoveTo("a", new NavigationTarget(Vector3.right), _ => Assert.Fail("Disabled callback cannot submit"));
                    Assert.That(reentrant.Accepted, Is.False); Assert.That(reentrant.Reason, Is.EqualTo(NavigationFailure.ActorUnavailable));
                },
                new NavigationTarget(Vector3.left * 3));
            for (var i = 0; i < 20; i++) { yield return null; world.Step(1f / 60); }
            Assert.That(actor.transform.position.x, Is.GreaterThan(-2));
            if (paused) Time.timeScale = 0;
            var position = actor.transform.position; var rotation = actor.transform.rotation;
            if (wholeObject) actor.gameObject.SetActive(false); else actor.enabled = false;
            Assert.That(callbacks, Is.EqualTo(1), "Disable must synchronously complete even with no scaled ticks");
            Assert.That(result.Status, Is.EqualTo(MovementStatus.Failed));
            Assert.That(result.Reason, Is.EqualTo(NavigationFailure.ActorUnavailable)); Assert.That(result.RetryCount, Is.Zero);
            Assert.That(actor.Agent.enabled, Is.False, "Native path intent must be retired");
            var disabledRequest = world.Service.MoveTo("a", new NavigationTarget(Vector3.right), _ => Assert.Fail("Rejected request has no callback"));
            Assert.That(disabledRequest.Accepted, Is.False); Assert.That(disabledRequest.Reason, Is.EqualTo(NavigationFailure.ActorUnavailable));
            for (var i = 0; i < 3; i++) { yield return null; world.Step(100); }
            Assert.That(actor.transform.position, Is.EqualTo(position)); Assert.That(actor.transform.rotation, Is.EqualTo(rotation));
            Assert.That(callbacks, Is.EqualTo(1)); Assert.That(world.Service.Cancel(request.RequestId), Is.False);
            if (wholeObject) actor.gameObject.SetActive(true); else actor.enabled = true;
            Assert.That(actor.transform.position, Is.EqualTo(position)); Time.timeScale = 1;
            MovementResult? fresh = null;
            Assert.That(world.Service.MoveTo("a", new NavigationTarget(Vector3.right * 2), r => fresh = r).Accepted, Is.True);
            yield return Run(() => fresh.HasValue);
            Assert.That(fresh.Value.Status, Is.EqualTo(MovementStatus.Arrived));
            Object.Destroy(actor.gameObject); yield return null;
            Assert.That(callbacks, Is.EqualTo(1)); Assert.That(world.Service.Cancel(request.RequestId), Is.False);
        }
        [UnityTest] public IEnumerator DisabledVisibleBodyStillBlocksAnotherActor()
        {
            Corridor(1); Bake(); var a = Actor("a", Vector3.zero); var b = Actor("b", Vector3.back * 3);
            Assert.That(world.Register(a), Is.True); Assert.That(world.Register(b), Is.True);
            a.enabled = false;
            Assert.That(a.Proxy.enabled && a.gameObject.activeInHierarchy, Is.True);
            MovementResult? result = null;
            world.Service.MoveTo("b", new NavigationTarget(Vector3.forward * 3), r => result = r);
            for (var frame = 0; frame < 1800 && !result.HasValue; frame++)
            {
                yield return null; var before = b.transform.position; world.Step(1f / 60);
                var delta = b.transform.position - before;
                var t = delta.sqrMagnitude < 1e-12f ? 0 : Mathf.Clamp01(-Vector3.Dot(before, delta) / delta.sqrMagnitude);
                Assert.That((before + t * delta).magnitude, Is.GreaterThanOrEqualTo(.899f));
                Assert.That(a.transform.position, Is.EqualTo(Vector3.zero));
            }
            Assert.That(result.HasValue, Is.True); Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Failed));
        }
        [UnityTest] public IEnumerator DisabledActorRebuildMustNotReenableNativeAgent()
        {
            Bake(); var actor=Actor("a",Vector3.zero); Assert.That(world.Register(actor),Is.True);
            actor.enabled=false;
            Assert.That(world.ValidateAndRebindActors(out var reason),Is.True,reason);
            Assert.That(actor.Agent.enabled,Is.False,"Layout rebind must preserve disabled native state");
            yield return null;
        }
        [Test] public void NativeConvexBoundaryKeepsSafeTangentWithoutIncreasingIntent()
        {
            Box(new Vector3(0,.36f,0),new Vector3(1,.72f,1),true); Bake();
            var actor=Actor("a",new Vector3(.55736f,0,.97210f)); Assert.That(world.Register(actor),Is.True);
            var raw=new Vector3(-.014382f,0,.000720f);
            Assert.That(NavMesh.Raycast(actor.transform.position,actor.transform.position+raw,out var hit,NavMesh.AllAreas),Is.True);
            Assert.That(Vector3.Dot(raw,hit.normal),Is.LessThan(0),"Native edge normal points back into walkable space");
            var driver=new NavMeshMovementDriver(actor);
            var projected=(Vector3)typeof(NavMeshMovementDriver).GetMethod("ClampToNavMesh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(driver,new object[]{raw});
            Assert.That(projected.magnitude,Is.GreaterThan(.001f),"Complete path smoothing must retain its safe boundary tangent");
            Assert.That(projected.magnitude,Is.LessThanOrEqualTo(raw.magnitude+.000001f));
            Assert.That(projected.y,Is.Zero);
            Assert.That(Vector3.Dot(projected,hit.normal),Is.GreaterThanOrEqualTo(-.000001f));
            Assert.That(NavMesh.Raycast(actor.transform.position,actor.transform.position+projected,out _,NavMesh.AllAreas),Is.False);
            var safe=new NavigationCollisionGuard().ClampDisplacement(actor,projected,world.RegisteredActors,solids);
            Assert.That(safe,Is.EqualTo(projected));
        }

        private Vector3 OutsideEdge()
        {
            Assert.That(NavMesh.FindClosestEdge(new Vector3(5, 0, 0), out var edge, NavMesh.AllAreas), Is.True);
            return new Vector3(edge.position.x + .10f, 0, 0);
        }

        [UnityTest] public IEnumerator AuthoredStartNeverSnapsOnFirstBindOrRebind()
        {
            Bake(); var position = new Vector3(1.137f, 0, -.437f);
            var actor = Actor("a", position);
            Assert.That(NavMesh.SamplePosition(position, out var hit, .05f, NavMesh.AllAreas), Is.True);
            Assert.That(Mathf.Abs(hit.position.y - position.y), Is.InRange(0, .05f));
            Assert.That(world.Register(actor), Is.True); Assert.That(actor.transform.position, Is.EqualTo(position));
            Assert.That(actor.Agent.updatePosition, Is.False); Assert.That(actor.Agent.updateRotation, Is.False);
            yield return null; Assert.That(actor.transform.position, Is.EqualTo(position));
            world.Unregister(actor); instance.Remove(); Bake();
            Assert.That(world.Register(actor), Is.True); yield return null;
            Assert.That(actor.transform.position, Is.EqualTo(position));
            var invalid = Actor("invalid", new Vector3(6, 0, 0));
            var invalidPosition = invalid.transform.position;
            Assert.That(world.Register(invalid), Is.False);
            Assert.That(invalid.Agent.enabled, Is.False); Assert.That(invalid.transform.position, Is.EqualTo(invalidPosition));
        }

        [UnityTest] public IEnumerator LongFrameHasBoundedMovementAndFullTimeout()
        {
            Bake(); var actor = Actor("a", new Vector3(-2, 0, 0)); Assert.That(world.Register(actor), Is.True);
            MovementResult? result = null;
            world.Service.MoveTo("a", new NavigationTarget(new Vector3(2, 0, 0)), r => result = r);
            yield return null;
            var before = actor.transform.position; world.Step(100);
            Assert.That(Vector3.Distance(before, actor.transform.position), Is.LessThanOrEqualTo(1.2f * 16 / 60 + .001f));
            world.Step(100);
            Assert.That(result.HasValue, Is.True); Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Failed));
            Assert.That(result.Value.RetryCount, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator MixedSteadyAndDefaultActorsArriveWithoutCrossingSweeps()
        { Bake(); yield return Dual(true, true); }

        [UnityTest] public IEnumerator MixedSteadyAndDefaultActorsFailSafelyInNarrowCorridor()
        { Corridor(1); Bake(); yield return Dual(false, true); }

        [UnityTest] public IEnumerator SteadyCornerConnectionRejectsWallAndStationaryBody()
        {
            var wall = Box(new Vector3(0, .7f, 0), new Vector3(.3f, 1.4f, 4), true); Bake();
            var actor = Actor("a", new Vector3(-2, 0, 0)); actor.TryEnableSteadyPathMotion();
            Assert.That(world.Register(actor), Is.True);
            var query = typeof(NavigationWorld).GetMethod("CanTraverseStraight", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That((bool)query.Invoke(world, new object[] { actor, new Vector3(2, 0, 0) }), Is.False, "Cannot skip a wall corner");
            wall.enabled = false; instance.Remove(); solids.Remove(wall); sources.RemoveAt(sources.Count - 1); Bake();
            var body = Actor("b", Vector3.zero); Assert.That(world.Register(body), Is.True); body.enabled = false;
            Assert.That((bool)query.Invoke(world, new object[] { actor, new Vector3(2, 0, 0) }), Is.False, "Disabled visible body still blocks the full connection");
            Assert.That((bool)query.Invoke(world, new object[] { actor, new Vector3(-2, 0, 2) }), Is.True, "Clear owned connection remains usable");
            Assert.That(actor.transform.position, Is.EqualTo(new Vector3(-2, 0, 0)), "Query never moves the actor");
            yield return null;
        }

        [UnityTest] public IEnumerator SteadyShortFinalTargetKeepsPoseAndFacingWithoutOvershoot()
        {
            Bake(); var actor = Actor("a", Vector3.zero); actor.TryEnableSteadyPathMotion(); world.Register(actor);
            MovementResult? result = null; var target = new Vector3(.03f, 0, 0);
            world.Service.MoveTo("a", new NavigationTarget(target, Vector3.back), r => result = r);
            for (var i = 0; i < 120 && !result.HasValue; i++)
            {
                yield return null; world.Step(.1f);
                Assert.That(actor.transform.position.x, Is.InRange(0, .031f), "No overshoot at a target closer than one frame's travel");
            }
            Assert.That(result.HasValue, Is.True); Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Arrived));
            Assert.That(Vector3.Distance(actor.transform.position, target), Is.LessThanOrEqualTo(.001f));
            Assert.That(Vector3.Angle(actor.transform.forward, Vector3.back), Is.LessThanOrEqualTo(5));
        }

        [UnityTest] public IEnumerator SteadyCancelPauseAndRebindDoNotReviveOldCorners()
        {
            Bake(); var actor = Actor("a", new Vector3(-2, 0, 0)); actor.TryEnableSteadyPathMotion(); world.Register(actor);
            var callbacks = 0;
            var request = world.Service.MoveTo("a", new NavigationTarget(new Vector3(2, 0, 0)), _ => callbacks++);
            for (var i = 0; i < 20; i++) { yield return null; world.Step(1f / 60); }
            var paused = actor.transform.position; world.Step(0); Assert.That(actor.transform.position, Is.EqualTo(paused));
            Assert.That(world.Service.Cancel(request.RequestId), Is.True); Assert.That(callbacks, Is.EqualTo(1));
            for (var i = 0; i < 3; i++) { yield return null; world.Step(1f / 60); }
            Assert.That(actor.transform.position, Is.EqualTo(paused));
            MovementResult? disabled = null;
            world.Service.MoveTo("a", new NavigationTarget(new Vector3(2, 0, 0)), r => disabled = r);
            actor.enabled = false; Assert.That(disabled.Value.Reason, Is.EqualTo(NavigationFailure.ActorUnavailable));
            actor.enabled = true; world.SetLayoutAvailable(false); world.SetLayoutAvailable(true);
            MovementResult? fresh = null;
            Assert.That(world.Service.MoveTo("a", new NavigationTarget(new Vector3(-3, 0, 0)), r => fresh = r).Accepted, Is.True);
            yield return Run(() => fresh.HasValue);
            Assert.That(fresh.Value.Status, Is.EqualTo(MovementStatus.Arrived));
            Assert.That(Vector3.Distance(actor.transform.position, new Vector3(-3, 0, 0)), Is.LessThanOrEqualTo(.08f));
            Assert.That(callbacks, Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator SteadyLongFrameNeverAccumulatesCatchUpMovement()
        {
            Bake(); var actor = Actor("a", new Vector3(-2, 0, 0)); actor.TryEnableSteadyPathMotion(); world.Register(actor);
            MovementResult? result = null; world.Service.MoveTo("a", new NavigationTarget(new Vector3(2, 0, 0)), r => result = r);
            yield return null; var before = actor.transform.position; world.Step(100);
            Assert.That(Vector3.Distance(before, actor.transform.position), Is.LessThanOrEqualTo(1.2f * 16 / 60 + .001f));
            world.Step(100); Assert.That(result.HasValue, Is.True); Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Failed));
        }

        [UnityTest] public IEnumerator SteadySampledFurnitureCornerKeepsOriginalArrivalAndFacingTolerance()
        {
            var furniture = Box(new Vector3(0, .7f, 0), new Vector3(2, 1.4f, 2), true); Bake();
            var actor = Actor("a", new Vector3(-3, 0, 2)); actor.TryEnableSteadyPathMotion(); world.Register(actor);
            var path = new NavMeshPath();
            Assert.That(NavMesh.CalculatePath(actor.transform.position, new Vector3(3, 0, -2), NavMesh.AllAreas, path), Is.True);
            Vector3? cornerTarget = null;
            foreach (var point in path.corners)
            {
                var body = new Vector3(point.x, .65f, point.z);
                var clearance = Vector3.Distance(body, furniture.ClosestPoint(body));
                if (clearance >= .45f && clearance < .46f) { cornerTarget = point; break; }
            }
            Assert.That(cornerTarget.HasValue, Is.True, "Fixture requires a native corner inside full skin: " + string.Join(",", path.corners));
            MovementResult? result = null;
            Assert.That(world.Service.MoveTo("a", new NavigationTarget(cornerTarget.Value, Vector3.back), r => result = r).Accepted, Is.True);
            yield return Run(() => result.HasValue);
            Assert.That(result.Value.Status, Is.EqualTo(MovementStatus.Arrived), result.Value.Reason + " at " + actor.transform.position);
            Assert.That(Vector3.Distance(actor.transform.position, result.Value.FinalPosition), Is.LessThanOrEqualTo(.001f));
            Assert.That(Vector3.Distance(actor.transform.position, cornerTarget.Value), Is.LessThanOrEqualTo(.08f));
            Assert.That(Vector3.Angle(actor.transform.forward, Vector3.back), Is.LessThanOrEqualTo(5));
            var finalBody = actor.transform.position + Vector3.up * .65f;
            Assert.That(Vector3.Distance(finalBody, furniture.ClosestPoint(finalBody)), Is.GreaterThanOrEqualTo(.459f));
        }

        private IEnumerator Dual(bool wide, bool mixed = false)
        {
            var a = Actor("a", new Vector3(0, 0, -3)); var b = Actor("b", new Vector3(0, 0, 3));
            if (mixed) { Assert.That(a.TryEnableSteadyPathMotion(), Is.True); Assert.That(b.SteadyPathMotion, Is.False); }
            Assert.That(world.Register(a), Is.True); Assert.That(world.Register(b), Is.True);
            MovementResult? ra = null, rb = null;
            world.Service.MoveTo("a", new NavigationTarget(new Vector3(0, 0, 3)), r => ra = r);
            world.Service.MoveTo("b", new NavigationTarget(new Vector3(0, 0, -3)), r => rb = r);
            var minimum = float.PositiveInfinity;
            for (var frame = 0; frame < 1800 && (!ra.HasValue || !rb.HasValue); frame++)
            {
                yield return null;
                var pa = a.transform.position; var pb = b.transform.position;
                world.Step(1f / 60);
                var relative = pa - pb; var velocity = (a.transform.position - pa) - (b.transform.position - pb);
                var time = velocity.sqrMagnitude < 1e-12f ? 0 : Mathf.Clamp01(-Vector3.Dot(relative, velocity) / velocity.sqrMagnitude);
                minimum = Mathf.Min(minimum, (relative + time * velocity).magnitude);
            }
            Assert.That(minimum, Is.GreaterThanOrEqualTo(.899f), "Independent continuous separation");
            Assert.That(ra.HasValue && rb.HasValue, Is.True, "Both requests must terminate");
            Assert.That(ra.Value.Status, Is.EqualTo(wide ? MovementStatus.Arrived : MovementStatus.Failed), "a " + ra.Value.Reason);
            Assert.That(rb.Value.Status, Is.EqualTo(wide ? MovementStatus.Arrived : MovementStatus.Failed), "b " + rb.Value.Reason);
        }

        private IEnumerator Run(System.Func<bool> complete)
        {
            for (var frame = 0; frame < 1800 && !complete(); frame++) { yield return null; world.Step(1f / 60); }
            Assert.That(complete(), Is.True, "Request did not terminate in 30 game seconds");
        }

        private void Corridor(float width)
        {
            // Walls span the whole floor sideways; there is no alternate route around the corridor.
            var thickness = (12 - width) / 2;
            Box(new Vector3((width + thickness) / 2, .7f, 0), new Vector3(thickness, 1.4f, 4), true);
            Box(new Vector3(-(width + thickness) / 2, .7f, 0), new Vector3(thickness, 1.4f, 4), true);
        }

        private void Bake()
        {
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = .45f; settings.agentHeight = 1.3f; settings.agentClimb = .1f;
            settings.overrideVoxelSize = true; settings.voxelSize = .025f;
            settings.overrideTileSize = true; settings.tileSize = 128; settings.minRegionArea = .01f;
            if (data != null) Object.DestroyImmediate(data);
            data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(Vector3.zero, new Vector3(16, 6, 16)), Vector3.zero, Quaternion.identity);
            Assert.That(data, Is.Not.Null); instance = NavMesh.AddNavMeshData(data);
            Physics.SyncTransforms(); world.SetGeometry(solids, 1);
        }

        private NavigationActor Actor(string id, Vector3 position)
        {
#if UNITY_EDITOR
            var name = id == "b" ? "Westie" : "Shiba";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase11/Characters/" + name + "/PF_" + name + "_Navigation.prefab");
            Assert.That(prefab, Is.Not.Null);
            var go = Object.Instantiate(prefab, position, Quaternion.identity); objects.Add(go);
            var actor = go.GetComponent<NavigationActor>();
            actor.Configure(id, actor.Agent, actor.Proxy, actor.ModelAnimator, actor.ModelRoot);
            return actor;
#else
            throw new System.NotSupportedException("The fixture loads the imported character prefabs in Editor PlayMode.");
#endif
        }

        private BoxCollider Box(Vector3 position, Vector3 size, bool solid)
        {
            var go = New(solid ? "wall" : "floor"); go.transform.position = position;
            var box = go.AddComponent<BoxCollider>(); box.size = size; if (solid) solids.Add(box);
            sources.Add(new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = go.transform.localToWorldMatrix, size = size, area = 0 });
            return box;
        }
        private GameObject New(string name) { var go = new GameObject(name); objects.Add(go); return go; }
    }
}

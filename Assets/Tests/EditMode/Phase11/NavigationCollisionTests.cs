using System.Collections.Generic;
using AnimalCafe.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace AnimalCafe.Tests.EditMode.Phase11
{
    public sealed class NavigationCollisionTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly NavigationCollisionGuard guard = new NavigationCollisionGuard();

        [TearDown] public void Cleanup()
        { foreach (var item in objects) Object.DestroyImmediate(item); objects.Clear(); }

        [Test] public void CrossingSweepsCannotExchangeSides()
        {
            var a = Actor("a", new Vector3(-1, 0, 0));
            var b = Actor("b", new Vector3(1, 0, 0));
            var startA = a.transform.position;
            var startB = b.transform.position;
            var actors = new[] { a, b };
            var da = guard.ClampDisplacement(a, Vector3.right * 2, actors, new Collider[0]);
            a.transform.position += da;
            var db = guard.ClampDisplacement(b, Vector3.left * 2, actors, new Collider[0]);
            // Independent continuous relative-motion check, not a guard success flag.
            Assert.That(MinimumSeparation(startA, da, startB, db), Is.GreaterThanOrEqualTo(.899f));
            Assert.That(startA.x + da.x, Is.LessThan(startB.x + db.x));
        }

        [Test] public void StationaryActorKeepsItsSpace()
        {
            var a = Actor("moving", Vector3.zero);
            var b = Actor("stationary", Vector3.right * 1.5f);
            var displacement = guard.ClampDisplacement(a, Vector3.right * 3, new[] { a, b }, new Collider[0]);
            Assert.That(MinimumSeparation(Vector3.zero, displacement, b.transform.position, Vector3.zero), Is.GreaterThanOrEqualTo(.899f));
        }

        [Test] public void ApprovedDisplacementKeepsCommittedFloatPositionValidForRetry()
        {
            // Float32 subtraction and Transform addition round differently at this skin boundary.
            // 检查真实 guard 和提交后的坐标，不能只检查提交前的相对位移算式。
            var actor = Actor("moving", new Vector3(-8f, 0, 0));
            var other = Actor("stationary", new Vector3(-7.089000225067139f, 0, 0));
            var actors = new[] { actor, other };
            var solids = new Collider[0];
            var startClear = typeof(NavigationCollisionGuard).GetMethod("StartClear",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(startClear, Is.Not.Null);
            Physics.SyncTransforms();
            Assert.That((bool)startClear.Invoke(guard, new object[] { actor, actors, solids }), Is.True);

            var delta = guard.ClampDisplacement(actor, Vector3.right * .02f, actors, solids);
            actor.transform.position += delta; // Same float32 commit as NavigationWorld.
            Physics.SyncTransforms();

            var requiredSeparation = actor.Settings.AgentRadius + other.Settings.AgentRadius + actor.Settings.CollisionSkin;
            var committedSeparation = Vector3.Distance(actor.transform.position, other.transform.position);
            Assert.That((bool)startClear.Invoke(guard, new object[] { actor, actors, solids }), Is.True,
                "Approved actual pose must remain valid for retry; committed separation=" + committedSeparation.ToString("R") +
                ", required radii plus skin=" + requiredSeparation.ToString("R"));
            Assert.That(committedSeparation, Is.GreaterThanOrEqualTo(requiredSeparation),
                "Approved actual pose must retain both .45 m radii plus .01 m skin, without tolerance relaxation");
        }

        [Test] public void ApprovedCrossingSweepProtectsSpaceVacatedByEarlierActor()
        {
            var a = Actor("a", new Vector3(-1, 0, 0));
            var b = Actor("b", new Vector3(0, 0, -1));
            var actors = new[] { a, b };
            var startA = a.transform.position; var startB = b.transform.position;
            var da = guard.ClampDisplacement(a, Vector3.right * 2, actors, new Collider[0]);
            a.transform.position += da;
            // Install the same internal context World owns, without widening the runtime API.
            var sweepType = typeof(NavigationCollisionGuard).GetNestedType("Sweep", System.Reflection.BindingFlags.NonPublic);
            var sweep = System.Activator.CreateInstance(sweepType);
            sweepType.GetField("Actor").SetValue(sweep, a);
            sweepType.GetField("Start").SetValue(sweep, startA);
            sweepType.GetField("Delta").SetValue(sweep, da);
            var array = System.Array.CreateInstance(sweepType, 1); array.SetValue(sweep, 0);
            typeof(NavigationCollisionGuard).GetProperty("ApprovedSweeps", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(guard, array);
            var db = guard.ClampDisplacement(b, Vector3.forward * 2, actors, new Collider[0]);
            Assert.That(da.magnitude, Is.GreaterThan(1.9f), "First actor can traverse safely while second waits");
            Assert.That(MinimumSeparation(startA, da, startB, db), Is.GreaterThanOrEqualTo(.899f));
        }

        [Test] public void QueryOverflowStopsSafely()
        {
            var actor = Actor("a", Vector3.zero);
            for (var i = 0; i < 270; i++) Box(new Vector3(0, .65f, 0), Vector3.one * .02f);
            Physics.SyncTransforms();
            Assert.That(guard.ClampDisplacement(actor, Vector3.right, new[] { actor }, new Collider[0]), Is.EqualTo(Vector3.zero));
        }

        [Test] public void TangentProjectionPreservesOnlyNativeLateralIntention()
        {
            var a = Actor("a", Vector3.zero); var b = Actor("b", new Vector3(0, 0, .911f));
            var method = typeof(NavigationCollisionGuard).GetMethod("ProjectAvoidance", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var proposed = new Vector3(.01f, 0, .02f);
            var tangent = (Vector3)method.Invoke(guard, new object[] { a, proposed, new[] { a, b } });
            Assert.That(tangent.x, Is.EqualTo(proposed.x).Within(.000001f));
            Assert.That(tangent.z, Is.EqualTo(0).Within(.000001f));
            Assert.That(tangent.magnitude, Is.LessThanOrEqualTo(proposed.magnitude));
            var headOn = (Vector3)method.Invoke(guard, new object[] { a, Vector3.forward * .02f, new[] { a, b } });
            Assert.That(headOn, Is.EqualTo(Vector3.zero), "No native lateral intention means no invented detour");
        }

        [Test] public void LongFrameCannotTunnel()
        {
            var actor = Actor("a", Vector3.zero);
            var wall = Box(new Vector3(2, .7f, 0), new Vector3(.05f, 1.4f, 4));
            Physics.SyncTransforms();
            var displacement = guard.ClampDisplacement(actor, Vector3.right * 20, new[] { actor }, new[] { wall });
            Assert.That(displacement.x, Is.InRange(0, 2 - .025f - .45f));
        }

        [Test] public void OnlyRegisteredSolidsBlockAndTriggersAreIgnored()
        {
            var actor = Actor("a", Vector3.zero);
            var preview = Box(new Vector3(1, .7f, 0), Vector3.one);
            var trigger = Box(new Vector3(1, .7f, 0), Vector3.one); trigger.isTrigger = true;
            Physics.SyncTransforms();
            Assert.That(guard.ClampDisplacement(actor, Vector3.right, new[] { actor }, new[] { trigger }), Is.EqualTo(Vector3.right));
        }

                [Test] public void StaticTangentKeepsSkinAndOnlyNativeLateralMotion()
        {
            var actor=Actor("a",Vector3.zero);
            var wall=Box(new Vector3(.97f,.7f,0),new Vector3(1,1.4f,4)); Physics.SyncTransforms();
            var method=typeof(NavigationCollisionGuard).GetMethod("ProjectStaticAvoidance",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var proposed=new Vector3(.02f,0,.02f);
            var tangent=(Vector3)method.Invoke(guard,new object[]{actor,proposed,new Collider[]{wall}});
            Assert.That(tangent.x,Is.EqualTo(0).Within(.000001f));
            Assert.That(tangent.z,Is.EqualTo(proposed.z).Within(.000001f));
            Assert.That(tangent.y,Is.Zero); Assert.That(tangent.magnitude,Is.LessThanOrEqualTo(proposed.magnitude));
            var safe=guard.ClampDisplacement(actor,tangent,new[]{actor},new Collider[]{wall});
            Assert.That(safe.z,Is.GreaterThan(.019f));
            for(var i=0;i<=20;i++)
            {
                var point=actor.transform.position+safe*(i/20f)+Vector3.up*.65f;
                Assert.That(Vector3.Distance(point,wall.ClosestPoint(point)),Is.GreaterThanOrEqualTo(.46f));
            }
            var headOn=(Vector3)method.Invoke(guard,new object[]{actor,Vector3.right*.02f,new Collider[]{wall}});
            Assert.That(headOn,Is.EqualTo(Vector3.zero),"No invented direction for a head-on intention");
        }
        [Test] public void RecordedCounterCornerSentinelPreservesSafeTangentProgress()
        {
            // Exact corner snapshot from the failing variable-frame scene replay.
            // 原始意图略微朝向圆角；真实 guard 应保留安全切向分量，不能直接放行原位移。
            var start = new Vector3(.7954388f, 0, -1.8525846f);
            var actor = Actor("recorded-corner", start);
            var counter = Box(new Vector3(0, .36f, 0), new Vector3(1f, .72f, 3f));
            var solids = new Collider[] { counter };
            var raw = new Vector3(.9186913f, 0, .7720144f) * .000633801043f;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var staticClear = typeof(NavigationCollisionGuard).GetMethod("StaticClear", flags);
            var project = typeof(NavigationCollisionGuard).GetMethod("ProjectStaticAvoidance", flags);
            Assert.That(staticClear, Is.Not.Null);
            Assert.That(project, Is.Not.Null);
            Physics.SyncTransforms();
            Assert.That((bool)staticClear.Invoke(guard, new object[] { actor, start, solids }), Is.True);

            var radius = actor.Settings.AgentRadius + actor.Settings.CollisionSkin;
            var bottom = start + Vector3.up * actor.Settings.AgentRadius;
            var top = start + Vector3.up * (actor.Settings.CapsuleHeight - actor.Settings.AgentRadius);
            var queryHits = new RaycastHit[256];
            var hitCount = Physics.CapsuleCastNonAlloc(bottom, top, radius, raw.normalized,
                queryHits, raw.magnitude, ~0, QueryTriggerInteraction.Ignore);
            var sentinelFound = false;
            TestContext.WriteLine("corner start=" + start.ToString("F9") + ", raw=" + raw.ToString("F9") + ", hitCount=" + hitCount);
            for (var i = 0; i < hitCount; i++)
            {
                var hit = queryHits[i];
                TestContext.WriteLine("hit=" + hit.collider.name + ", registered=" + (hit.collider == counter) +
                    ", distance=" + hit.distance.ToString("R") + ", normal=" + hit.normal.ToString("F9") +
                    ", point=" + hit.point.ToString("F9"));
                if (hit.collider == counter && hit.distance == 0 && hit.point == Vector3.zero &&
                    Vector3.Dot(hit.normal, raw.normalized) < -.9999f) sentinelFound = true;
            }
            Assert.That(sentinelFound, Is.True, "The exact scene snapshot must reproduce the registered initial-overlap sentinel");

            var tangent = (Vector3)project.Invoke(guard, new object[] { actor, raw, solids });
            var safe = guard.ClampDisplacement(actor, tangent, new[] { actor }, solids);
            TestContext.WriteLine("projected=" + tangent.ToString("F9") + ", clamped=" + safe.ToString("F9"));
            Assert.That(safe.x, Is.GreaterThan(raw.x * .9f));
            Assert.That(safe.z, Is.GreaterThan(raw.z * .9f));
            Assert.That(safe.magnitude, Is.LessThanOrEqualTo(raw.magnitude));
            actor.transform.position = start + safe;
            Physics.SyncTransforms();
            var end = actor.transform.position;
            Assert.That(start.x, Is.GreaterThan(.5f));
            Assert.That(end.x, Is.GreaterThan(.5f));
            Assert.That(start.z, Is.LessThan(-1.5f));
            Assert.That(end.z, Is.LessThan(-1.5f));
            // This entire segment remains in the same corner region. Compute its exact closest
            // approach in double precision using the actual committed float32 endpoints.
            // 独立几何检查整段到柜台角点的最小距离，不放宽完整 skin。
            var x = (double)start.x - .5; var z = (double)start.z + 1.5;
            var dx = (double)end.x - start.x; var dz = (double)end.z - start.z;
            var time = System.Math.Max(0, System.Math.Min(1, -(x * dx + z * dz) / (dx * dx + dz * dz)));
            var gap = System.Math.Sqrt((x + time * dx) * (x + time * dx) + (z + time * dz) * (z + time * dz));
            TestContext.WriteLine("continuous corner gap=" + gap.ToString("R") + ", required=" + radius.ToString("R"));
            Assert.That(gap, Is.GreaterThanOrEqualTo((double)radius));
            Assert.That((bool)staticClear.Invoke(guard, new object[] { actor, end, solids }), Is.True);
        }

        [Test] public void RecordedCornerInwardStepIsNotExemptedFromFullSkin()
        {
            var actor = Actor("corner-inward", new Vector3(.7954388f, 0, -1.8525846f));
            var counter = Box(new Vector3(0, .36f, 0), new Vector3(1f, .72f, 3f));
            var raw = new Vector3(.9186913f, 0, .7720144f) * .000633801043f;
            Physics.SyncTransforms();
            var unsafeEnd = actor.transform.position + raw + Vector3.up * actor.Settings.AgentRadius;
            Assert.That(Vector3.Distance(unsafeEnd, counter.ClosestPoint(unsafeEnd)),
                Is.LessThan(actor.Settings.AgentRadius + actor.Settings.CollisionSkin), "The unprojected intention violates skin");
            Assert.That(guard.ClampDisplacement(actor, raw, new[] { actor }, new Collider[] { counter }), Is.EqualTo(Vector3.zero),
                "A sentinel must not let an inward raw step bypass the continuous sweep");
        }

        [Test] public void CornerSentinelCannotBypassSecondSolid()
        {
            var actor = Actor("corner-second-solid", new Vector3(.7954388f, 0, -1.8525846f));
            var counter = Box(new Vector3(0, .36f, 0), new Vector3(1f, .72f, 3f));
            var raw = new Vector3(.9186913f, 0, .7720144f) * .000633801043f;
            var radius = actor.Settings.AgentRadius + actor.Settings.CollisionSkin;
            var wall = Box(new Vector3(actor.transform.position.x + radius + raw.x * .5f + .005f, .65f,
                actor.transform.position.z), new Vector3(.01f, 1.3f, 2f));
            var solids = new Collider[] { counter, wall };
            Physics.SyncTransforms();
            var project = typeof(NavigationCollisionGuard).GetMethod("ProjectStaticAvoidance",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var tangent = (Vector3)project.Invoke(guard, new object[] { actor, raw, solids });
            Assert.That(tangent.x, Is.GreaterThan(raw.x * .9f));
            var safe = guard.ClampDisplacement(actor, tangent, new[] { actor }, solids);
            Assert.That(safe.x, Is.LessThan(raw.x * .5f), "Refining the corner hit must preserve the second wall's restriction");
        }

        [Test] public void CornerSweepCannotCrossCounterEvenWhenBothEndpointsAreClear()
        {
            var actor = Actor("corner-crossing", new Vector3(.7954388f, 0, -1.8525846f));
            var counter = Box(new Vector3(0, .36f, 0), new Vector3(1f, .72f, 3f));
            var delta = new Vector3(-1.6f, 0, 3.7f);
            var solids = new Collider[] { counter };
            var staticClear = typeof(NavigationCollisionGuard).GetMethod("StaticClear",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Physics.SyncTransforms();
            Assert.That((bool)staticClear.Invoke(guard, new object[] { actor, actor.transform.position, solids }), Is.True);
            Assert.That((bool)staticClear.Invoke(guard, new object[] { actor, actor.transform.position + delta, solids }), Is.True);
            Assert.That(guard.ClampDisplacement(actor, delta, new[] { actor }, solids), Is.EqualTo(Vector3.zero),
                "A changed closest feature cannot use an endpoint-only clearance proof");
        }

        [Test] public void StaticProjectionIgnoresPreviewButStopsOnOverflow()
        {
            var actor=Actor("a",Vector3.zero); var preview=Box(new Vector3(.97f,.7f,0),new Vector3(1,1.4f,4));
            Physics.SyncTransforms();
            var method=typeof(NavigationCollisionGuard).GetMethod("ProjectStaticAvoidance",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var proposed=new Vector3(.02f,0,.02f);
            Assert.That((Vector3)method.Invoke(guard,new object[]{actor,proposed,new Collider[0]}),Is.EqualTo(proposed));
            preview.isTrigger=true;
            Assert.That((Vector3)method.Invoke(guard,new object[]{actor,proposed,new Collider[]{preview}}),Is.EqualTo(proposed));
            for(var i=0;i<270;i++) Box(new Vector3(0,.65f,0),Vector3.one*.02f); Physics.SyncTransforms();
            Assert.That((Vector3)method.Invoke(guard,new object[]{actor,proposed,new Collider[0]}),Is.EqualTo(Vector3.zero));
        }

        private NavigationActor Actor(string id, Vector3 position)
        {
            var go = new GameObject(id); objects.Add(go); go.SetActive(false); go.transform.position = position;
            var agent = go.AddComponent<NavMeshAgent>(); agent.enabled = false; agent.radius = .45f; agent.height = 1.30f;
            var capsule = go.AddComponent<CapsuleCollider>(); capsule.radius = .45f; capsule.height = 1.30f; capsule.center = Vector3.up * .65f;
            var actor = go.AddComponent<NavigationActor>(); actor.Configure(id, agent, capsule, null, null); go.SetActive(true); return actor;
        }

        private BoxCollider Box(Vector3 position, Vector3 size)
        {
            var go = new GameObject("solid"); objects.Add(go); go.transform.position = position;
            var box = go.AddComponent<BoxCollider>(); box.size = size; return box;
        }

        private static float MinimumSeparation(Vector3 a, Vector3 da, Vector3 b, Vector3 db)
        {
            var r = a - b; r.y = 0; var v = da - db; v.y = 0;
            var time = v.sqrMagnitude < 1e-9f ? 0 : Mathf.Clamp01(-Vector3.Dot(r, v) / v.sqrMagnitude);
            return (r + time * v).magnitude;
        }
    }
}

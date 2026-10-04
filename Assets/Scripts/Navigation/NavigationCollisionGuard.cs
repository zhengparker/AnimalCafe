using System.Collections.Generic;
using UnityEngine;

namespace AnimalCafe.Navigation
{
    public sealed class NavigationCollisionGuard
    {
        // Full buffers are ambiguous: stop instead of silently dropping an obstacle.
        private readonly Collider[] overlaps = new Collider[256];
        private readonly RaycastHit[] hits = new RaycastHit[256];
        internal struct Sweep { public NavigationActor Actor; public Vector3 Start, Delta; }
        internal IReadOnlyList<Sweep> ApprovedSweeps { get; set; }

        internal Vector3 ProjectAvoidance(NavigationActor actor, Vector3 proposed, IReadOnlyList<NavigationActor> actors)
        {
            var start = actor.transform.position;
            if (DynamicClear(actor, start, proposed, actors)) return proposed;
            foreach (var other in actors)
            {
                if (other == null || other == actor) continue;
                var otherStart = other.transform.position; var otherDelta = Vector3.zero;
                if (ApprovedSweeps != null)
                    foreach (var sweep in ApprovedSweeps)
                        if (sweep.Actor == other) { otherStart = sweep.Start; otherDelta = sweep.Delta; break; }
                if (start.y >= otherStart.y + other.Settings.CapsuleHeight || otherStart.y >= start.y + actor.Settings.CapsuleHeight) continue;
                var normal = start - otherStart; normal.y = 0;
                var relative = proposed - otherDelta; relative.y = 0;
                var t = relative.sqrMagnitude < 1e-12f ? 0 : Mathf.Clamp01(-Vector3.Dot(normal, relative) / relative.sqrMagnitude);
                var radius = actor.Settings.AgentRadius + other.Settings.AgentRadius + actor.Settings.CollisionSkin;
                if ((normal + t * relative).sqrMagnitude >= radius * radius) continue;
                normal.Normalize();
                var inward = Vector3.Dot(proposed, normal);
                // Remove only the inward part of the native intention. Never create a detour or push.
                if (inward < 0) proposed -= normal * inward;
            }
            return proposed;
        }

        internal Vector3 ProjectStaticAvoidance(NavigationActor actor, Vector3 proposed, IReadOnlyList<Collider> solids)
        {
            if (actor == null || !Finite(proposed) || !StaticClear(actor, actor.transform.position, solids)) return Vector3.zero;
            proposed.y = 0;
            if (proposed.sqrMagnitude < 1e-12f) return Vector3.zero;
            Capsule(actor, actor.transform.position, out var bottom, out var top);
            var count = Physics.CapsuleCastNonAlloc(bottom, top, actor.Settings.AgentRadius + actor.Settings.CollisionSkin,
                proposed.normalized, hits, proposed.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return Vector3.zero;
            var nearest = float.PositiveInfinity; var normal = Vector3.zero;
            for (var i = 0; i < count; i++)
                if (RegisteredSolid(actor, hits[i].collider, solids) && hits[i].distance < nearest)
                {
                    nearest = hits[i].distance; normal = hits[i].normal;
                    if (InitialOverlapSentinel(hits[i], proposed.normalized) &&
                        BoxSweepClear(actor, actor.transform.position, Vector3.zero, hits[i].collider))
                    {
                        // A sweep-start sentinel reports -direction, not the actual surface normal.
                        // 用真实 Box 表面法线；最终位移仍须通过完整 skin 的连续几何检查。
                        var axisPoint = actor.transform.position + Vector3.up * actor.Settings.AgentRadius;
                        normal = axisPoint - hits[i].collider.ClosestPoint(axisPoint);
                    }
                }
            normal.y = 0;
            if (normal.sqrMagnitude < 1e-12f) return proposed;
            normal.Normalize();
            var inward = Vector3.Dot(proposed, normal);
            // One projection of native intention, never an invented waypoint or recursive slide.
            // World still validates NavMesh and the complete static/dynamic sweep afterward.
            return inward < 0 ? proposed - normal * inward : proposed;
        }

        public Vector3 ClampDisplacement(NavigationActor actor, Vector3 displacement,
            IReadOnlyList<NavigationActor> actors, IReadOnlyList<Collider> solids)
        {
            if (actor == null || !Finite(displacement)) return Vector3.zero;
            displacement.y = 0;
            var start = actor.transform.position;
            if (!StaticClear(actor, start, solids) || !DynamicClear(actor, start, Vector3.zero, actors)) return Vector3.zero;
            var length = displacement.magnitude;
            if (length <= 0) return Vector3.zero;
            Capsule(actor, start, out var bottom, out var top);
            var count = Physics.CapsuleCastNonAlloc(bottom, top, actor.Settings.AgentRadius + actor.Settings.CollisionSkin,
                displacement / length, hits, length, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return Vector3.zero;
            var allowed = length;
            var direction = displacement / length;
            for (var i = 0; i < count; i++)
                if (RegisteredSolid(actor, hits[i].collider, solids))
                {
                    if (InitialOverlapSentinel(hits[i], direction) && BoxSweepClear(actor, start, displacement, hits[i].collider)) continue;
                    allowed = Mathf.Min(allowed, Mathf.Max(0, hits[i].distance - actor.Settings.Epsilon));
                }
            displacement *= allowed / length;
            if (!DynamicClear(actor, start, displacement, actors))
            {
                var low = 0f; var high = 1f;
                for (var i = 0; i < 16; i++)
                {
                    var mid = (low + high) * .5f;
                    if (DynamicClear(actor, start, displacement * mid, actors)) low = mid; else high = mid;
                }
                displacement *= low;
            }
            // Dynamic shortening changes the rounded Transform endpoint. Prove the final sweep again.
            // 动态碰撞缩短位移后，必须重新检查实际 float32 终点，不能沿用旧终点的证明。
            for (var i = 0; i < count; i++)
                if (RegisteredSolid(actor, hits[i].collider, solids) && InitialOverlapSentinel(hits[i], direction) &&
                    !BoxSweepClear(actor, start, displacement, hits[i].collider)) return Vector3.zero;
            return StaticClear(actor, start + displacement, solids) ? displacement : Vector3.zero;
        }

        private static bool InitialOverlapSentinel(RaycastHit hit, Vector3 direction)
            => hit.distance == 0 && hit.point.sqrMagnitude == 0 && Vector3.Dot(hit.normal, direction) < -.9999f;

        private static bool BoxSweepClear(NavigationActor actor, Vector3 start, Vector3 delta, Collider collider)
        {
            if (!(collider is BoxCollider box)) return false;
            // Only the axis-aligned, unit-scale Box geometry is certified here. Other shapes stop.
            // 当前仅证明世界坐标轴对齐、单位缩放的 Box；不猜测其它形状的安全间距。
            var matrix = box.transform.localToWorldMatrix;
            if (matrix.m00 != 1 || matrix.m11 != 1 || matrix.m22 != 1 ||
                matrix.m01 != 0 || matrix.m02 != 0 || matrix.m10 != 0 ||
                matrix.m12 != 0 || matrix.m20 != 0 || matrix.m21 != 0) return false;
            var center = box.transform.TransformPoint(box.center);
            var end = start + delta; // Exactly the float32 endpoint committed by NavigationWorld.
            var minX = (double)center.x - (double)box.size.x * .5;
            var maxX = (double)center.x + (double)box.size.x * .5;
            var minZ = (double)center.z - (double)box.size.z * .5;
            var maxZ = (double)center.z + (double)box.size.z * .5;
            var sideX = start.x < minX ? -1 : start.x > maxX ? 1 : 0;
            var sideZ = start.z < minZ ? -1 : start.z > maxZ ? 1 : 0;
            var endSideX = end.x < minX ? -1 : end.x > maxX ? 1 : 0;
            var endSideZ = end.z < minZ ? -1 : end.z > maxZ ? 1 : 0;
            if ((sideX == 0 && sideZ == 0) || sideX != endSideX || sideZ != endSideZ) return false;
            var radius = (double)(actor.Settings.AgentRadius + actor.Settings.CollisionSkin);
            if (sideX == 0)
                return System.Math.Min(System.Math.Abs(start.z - (sideZ < 0 ? minZ : maxZ)),
                    System.Math.Abs(end.z - (sideZ < 0 ? minZ : maxZ))) >= radius;
            if (sideZ == 0)
                return System.Math.Min(System.Math.Abs(start.x - (sideX < 0 ? minX : maxX)),
                    System.Math.Abs(end.x - (sideX < 0 ? minX : maxX))) >= radius;

            // Same corner region throughout: the exact segment-to-corner minimum certifies the sweep.
            // 起终点位于同一个角点区域，整段最小距离可直接计算，无采样遗漏或 skin 容差。
            var x = start.x - (sideX < 0 ? minX : maxX);
            var z = start.z - (sideZ < 0 ? minZ : maxZ);
            var dx = (double)end.x - start.x; var dz = (double)end.z - start.z;
            var squaredLength = dx * dx + dz * dz;
            var time = squaredLength == 0 ? 0 : System.Math.Max(0, System.Math.Min(1, -(x * dx + z * dz) / squaredLength));
            x += time * dx; z += time * dz;
            return x * x + z * z >= radius * radius;
        }

        internal bool StartClear(NavigationActor actor, IReadOnlyList<NavigationActor> actors, IReadOnlyList<Collider> solids)
            => StaticClear(actor, actor.transform.position, solids) && DynamicClear(actor, actor.transform.position, Vector3.zero, actors);

        private bool StaticClear(NavigationActor actor, Vector3 position, IReadOnlyList<Collider> solids)
        {
            Capsule(actor, position, out var bottom, out var top);
            var count = Physics.OverlapCapsuleNonAlloc(bottom, top, actor.Settings.AgentRadius + actor.Settings.CollisionSkin,
                overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (var i = 0; i < count; i++) if (RegisteredSolid(actor, overlaps[i], solids)) return false;
            return true;
        }

        private bool DynamicClear(NavigationActor actor, Vector3 start, Vector3 delta, IReadOnlyList<NavigationActor> actors)
        {
            if (actors == null) return true;
            foreach (var other in actors)
            {
                if (other == null || other == actor) continue;
                var otherStart = other.transform.position; var otherDelta = Vector3.zero;
                if (ApprovedSweeps != null)
                    foreach (var sweep in ApprovedSweeps)
                        if (sweep.Actor == other) { otherStart = sweep.Start; otherDelta = sweep.Delta; break; }
                if (start.y >= otherStart.y + other.Settings.CapsuleHeight || otherStart.y >= start.y + actor.Settings.CapsuleHeight) continue;
                var relative = start - otherStart; relative.y = 0;
                var velocity = delta - otherDelta; velocity.y = 0;
                var time = velocity.sqrMagnitude < 1e-12f ? 0 : Mathf.Clamp01(-Vector3.Dot(relative, velocity) / velocity.sqrMagnitude);
                var radius = actor.Settings.AgentRadius + other.Settings.AgentRadius + actor.Settings.CollisionSkin;
                if ((relative + time * velocity).sqrMagnitude < radius * radius) return false;
                // Match the actual Transform commits; float32 addition can round inside the skin.
                // 提交前检查真实终点，避免本步批准后下一步却被判定起点重叠。
                var endRelative = (start + delta) - (otherStart + otherDelta); endRelative.y = 0;
                if (endRelative.sqrMagnitude < radius * radius) return false;
            }
            return true;
        }

        private static void Capsule(NavigationActor actor, Vector3 position, out Vector3 bottom, out Vector3 top)
        {
            bottom = position + Vector3.up * actor.Settings.AgentRadius;
            top = position + Vector3.up * (actor.Settings.CapsuleHeight - actor.Settings.AgentRadius);
        }

        private static bool RegisteredSolid(NavigationActor actor, Collider collider, IReadOnlyList<Collider> solids)
        {
            if (collider == null || !collider.enabled || collider.isTrigger || collider.transform.IsChildOf(actor.transform) || solids == null) return false;
            for (var i = 0; i < solids.Count; i++) if (solids[i] == collider) return true;
            return false;
        }

        internal static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) &&
            !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
    }
}

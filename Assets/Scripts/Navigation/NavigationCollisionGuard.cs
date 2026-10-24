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
            var length = proposed.magnitude;
            if (length == 0) return Vector3.zero;
            // Preserve a finite nonzero final approach; all sweeps and endpoint guards still run.
            // 不用隐藏的近零归一化阈值清空位移；完整碰撞检查仍保留。
            var direction = proposed / length;
            Capsule(actor, actor.transform.position, out var bottom, out var top);
            var count = Physics.CapsuleCastNonAlloc(bottom, top, actor.Settings.AgentRadius + actor.Settings.CollisionSkin,
                direction, hits, length, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return Vector3.zero;
            var nearest = float.PositiveInfinity; var normal = Vector3.zero;
            for (var i = 0; i < count; i++)
                if (RegisteredSolid(actor, hits[i].collider, solids) && hits[i].distance < nearest)
                {
                    nearest = hits[i].distance; normal = hits[i].normal;
                    if (InitialOverlapSentinel(hits[i], direction) &&
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
            if (!Finite(start) || !Finite(delta) || delta.y != 0) return false;
            // Only the axis-aligned, unit-scale Box geometry is certified here. Other shapes stop.
            // 当前仅证明世界坐标轴对齐、单位缩放的 Box；不猜测其它形状的安全间距。
            var matrix = box.transform.localToWorldMatrix;
            if (matrix.m00 != 1 || matrix.m11 != 1 || matrix.m22 != 1 ||
                matrix.m01 != 0 || matrix.m02 != 0 || matrix.m10 != 0 ||
                matrix.m12 != 0 || matrix.m20 != 0 || matrix.m21 != 0) return false;
            var center = box.transform.TransformPoint(box.center);
            var end = start + delta; // Exactly the float32 endpoint committed by NavigationWorld.
            if (!Finite(end)) return false;
            var minX = (double)center.x - (double)box.size.x * .5;
            var maxX = (double)center.x + (double)box.size.x * .5;
            var minZ = (double)center.z - (double)box.size.z * .5;
            var maxZ = (double)center.z + (double)box.size.z * .5;
            Capsule(actor, start, out var bottom, out var top);
            var minY = (double)center.y - (double)box.size.y * .5;
            var maxY = (double)center.y + (double)box.size.y * .5;
            var verticalGap = bottom.y > maxY ? bottom.y - maxY : top.y < minY ? minY - top.y : 0;
            var radius = (double)(actor.Settings.AgentRadius + actor.Settings.CollisionSkin);
            // The vertical capsule axis sweeps a horizontal segment; its Y gap stays constant.
            // 全skin的3D最小距离：低柜须计入球心到柜顶的间隙，高柜重叠轴时该间隙为0。
            var horizontalSquared = SquaredSegmentRectangleDistance(start.x, start.z, end.x, end.z, minX, maxX, minZ, maxZ);
            return horizontalSquared + verticalGap * verticalGap >= radius * radius;
        }

        private static double SquaredSegmentRectangleDistance(double x, double z, double endX, double endZ,
            double minX, double maxX, double minZ, double maxZ)
        {
            var dx = endX - x; var dz = endZ - z; var enter = 0d; var exit = 1d;
            if (ClipAxisInterval(x, dx, minX, maxX, ref enter, ref exit) &&
                ClipAxisInterval(z, dz, minZ, maxZ, ref enter, ref exit)) return 0;
            // Without an intersection, a closest pair includes an endpoint of one boundary segment.
            // 不只看起终点；四个角到完整线段的距离也覆盖跨side/corner的连续最小值。
            var distance = System.Math.Min(SquaredPointRectangleDistance(x, z, minX, maxX, minZ, maxZ),
                SquaredPointRectangleDistance(endX, endZ, minX, maxX, minZ, maxZ));
            distance = System.Math.Min(distance, SquaredPointSegmentDistance(minX, minZ, x, z, dx, dz));
            distance = System.Math.Min(distance, SquaredPointSegmentDistance(minX, maxZ, x, z, dx, dz));
            distance = System.Math.Min(distance, SquaredPointSegmentDistance(maxX, minZ, x, z, dx, dz));
            return System.Math.Min(distance, SquaredPointSegmentDistance(maxX, maxZ, x, z, dx, dz));
        }

        private static bool ClipAxisInterval(double start, double delta, double min, double max, ref double enter, ref double exit)
        {
            if (delta == 0) return start >= min && start <= max;
            var first = (min - start) / delta; var last = (max - start) / delta;
            if (first > last) { var swap = first; first = last; last = swap; }
            enter = System.Math.Max(enter, first); exit = System.Math.Min(exit, last);
            return enter <= exit;
        }

        private static double SquaredPointRectangleDistance(double x, double z, double minX, double maxX, double minZ, double maxZ)
        {
            var dx = x < minX ? minX - x : x > maxX ? x - maxX : 0;
            var dz = z < minZ ? minZ - z : z > maxZ ? z - maxZ : 0;
            return dx * dx + dz * dz;
        }

        private static double SquaredPointSegmentDistance(double pointX, double pointZ, double x, double z, double dx, double dz)
        {
            var offsetX = pointX - x; var offsetZ = pointZ - z; var lengthSquared = dx * dx + dz * dz;
            var time = lengthSquared == 0 ? 0 : System.Math.Max(0, System.Math.Min(1, (offsetX * dx + offsetZ * dz) / lengthSquared));
            offsetX -= time * dx; offsetZ -= time * dz;
            return offsetX * offsetX + offsetZ * offsetZ;
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

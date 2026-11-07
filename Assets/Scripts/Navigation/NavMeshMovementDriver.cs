using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AnimalCafe.Navigation
{
    // Agent 负责寻路与避让；真实 Transform 只能由 NavigationWorld 写入。
    public sealed class NavMeshMovementDriver : INavigationDriver
    {
        private readonly NavigationActor actor;
        private IReadOnlyList<Collider> solids = new Collider[0];
        private NavigationTarget target;
        private Vector3 destination;
        private int generation, revision;
        private float pathLength, actualSpeed;
        private bool moving;
        private bool layoutAvailable = true;
        private int? ownedAgentType;
        private NavigationFailure failure;
        private Vector3[] corners = System.Array.Empty<Vector3>();
        private Vector3[] rawCorners = System.Array.Empty<Vector3>();
        private bool[] adjustedCorners = System.Array.Empty<bool>();
        private int corner;
        private NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = ownedAgentType ?? actor.Agent.agentTypeID, areaMask = actor.Agent.areaMask };
        public Vector3 Position => actor.transform.position;
        public Vector3 DesiredVelocity
        {
            get
            {
                if (!moving || !Bound || actor.Agent.pathPending || actor.Agent.isOnOffMeshLink) return Vector3.zero;
                if (actor.SteadyPathMotion)
                {
                    AdvanceCorner();
                    if (corners.Length == 0) return Vector3.zero;
                    var toward = Horizontal(corners[corner] - Position);
                    if (corner == corners.Length - 1 && toward.magnitude <= actor.Settings.Epsilon)
                    { actor.Agent.isStopped = true; return Vector3.zero; }
                    // Vector3.normalized has a hidden near-zero cutoff. A non-final corner
                    // still needs its last guarded micrometers / 保留拐点前最后的合法微小位移。
                    var length = toward.magnitude;
                    return length > 0 ? toward / length * actor.Settings.MaxSpeed : Vector3.zero;
                }
                if (Vector3.Distance(Position, destination) <= actor.Settings.ArrivalDistance)
                { actor.Agent.isStopped = true; return Vector3.zero; }
                return Vector3.ClampMagnitude(actor.Agent.desiredVelocity, actor.Settings.MaxSpeed);
            }
        }
        internal Vector3? Facing => moving && (actor.SteadyPathMotion
            ? Horizontal(Position - destination).magnitude <= actor.Settings.ArrivalDistance
            : Vector3.Distance(Position, destination) <= actor.Settings.ArrivalDistance) ? target.Facing : null;
        private bool Bound => actor.gameObject.activeInHierarchy && actor.Agent.enabled && actor.Agent.isOnNavMesh &&
            (!ownedAgentType.HasValue || actor.Agent.agentTypeID==ownedAgentType.Value);

        public NavMeshMovementDriver(NavigationActor actor) { this.actor = actor; }
        internal void SetGeometry(IReadOnlyList<Collider> value) { solids = value; }

        internal void SetLayoutAvailable(bool value)
        {
            layoutAvailable=value;
            if(!value) { Stop(); actor.Agent.enabled=false; }
        }
        internal void SetOwnedAgentType(int agentType)
        {
            Stop(); actor.Agent.enabled=false;
            ownedAgentType=agentType; actor.Agent.agentTypeID=agentType;
        }
        internal bool TryBind()
        {
            // NavMesh voxelization may move the surface vertically. Never repair horizontal placement.
            if (ownedAgentType.HasValue && actor.Agent.agentTypeID!=ownedAgentType.Value) return false;
            if (!ValidStart()) return false;
            var agent = actor.Agent;
            agent.updatePosition = false; agent.updateRotation = false; agent.updateUpAxis = false;
            agent.autoRepath = false; agent.autoTraverseOffMeshLink = false;
            agent.speed = actor.Settings.MaxSpeed; agent.angularSpeed = actor.Settings.TurnSpeedDegrees;
            agent.acceleration = 8; agent.stoppingDistance = actor.Settings.ArrivalDistance * .5f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            if(agent.enabled && !agent.isOnNavMesh) agent.enabled=false;
            agent.enabled = true;
            if (!agent.isOnNavMesh) { agent.enabled = false; return false; }
            agent.nextPosition = Position;
            agent.isStopped = true;
            return true;
        }

        internal bool ValidStart()
        {
            if (!NavigationCollisionGuard.Finite(Position) || !NavMesh.SamplePosition(Position, out var hit, .05f, Filter)) return false;
            var delta = hit.position - Position;
            return Mathf.Abs(delta.y) <= .05f && new Vector2(delta.x, delta.z).magnitude <= actor.Settings.Epsilon;
        }

#if UNITY_EDITOR
        internal static System.Action<NavigationActor,string,string> PathObserver;
#endif
        // Record only values already evaluated here. Formatting is opt-in; Player calls vanish.
        // 仅记录原查询结果；未执行的后续stage均为NOT_EVALUATED。
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void TracePath(string stage, bool? outcome=null, Vector3? point=null,
            Vector3[] path=null, NavMeshPathStatus? status=null, NavMeshQueryFilter? queryFilter=null)
        {
#if UNITY_EDITOR
            if(PathObserver==null) return;
            PathObserver(actor,stage,"generation="+generation+" layout="+revision+" outcome="+outcome+
                " requested="+target.Position.ToString("F9")+" point="+(point.HasValue?point.Value.ToString("F9"):"NOT_EVALUATED")+
                " pathStatus="+(status.HasValue?status.ToString():"NOT_EVALUATED")+
                " corners="+(path==null?"NOT_EVALUATED":string.Join(";",System.Array.ConvertAll(path,p=>p.ToString("F9"))))+
                " failure="+failure+" cornerIndex="+corner+" moving="+moving+
                " raw="+string.Join(";",System.Array.ConvertAll(rawCorners,p=>p.ToString("F9")))+
                " current="+string.Join(";",System.Array.ConvertAll(corners,p=>p.ToString("F9")))+
                " adjusted="+string.Join(",",adjustedCorners)+" filterType="+queryFilter?.agentTypeID+" areaMask="+queryFilter?.areaMask);
#endif
        }

        public NavigationFailure BeginPath(NavigationTarget value, int pathGeneration, int layoutRevision)
        {
            Stop(); target = value; generation = pathGeneration; revision = layoutRevision;
            failure = NavigationFailure.None;
            TracePath("begin");
            var active=actor.isActiveAndEnabled; TracePath("start.active",active);
            if (!active) return Fail(NavigationFailure.ActorUnavailable);
            TracePath("start.layout",layoutAvailable);
            if (!layoutAvailable) return Fail(NavigationFailure.LayoutUnavailable);
            if(actor.World != null)
            {
                var canStart=actor.World.CanStartActor(actor); TracePath("start.world",canStart);
                if(!canStart) return Fail(NavigationFailure.InvalidStart);
            }
            var valid=ValidStart(); TracePath("start.valid",valid);
            if(!valid) return Fail(NavigationFailure.InvalidStart);
            var bound=Bound; TracePath("start.bound",bound);
            if(!bound)
            {
                var bind=TryBind(); TracePath("start.bind",bind);
                if(!bind) return Fail(NavigationFailure.InvalidStart);
            }
            var finite=NavigationCollisionGuard.Finite(value.Position); TracePath("target.finite",finite);
            if(!finite) return Fail(NavigationFailure.InvalidTarget);
            var targetFilter=Filter;
            var sampled=NavMesh.SamplePosition(value.Position,out var sample,actor.Settings.TargetSampleDistance,targetFilter);
            TracePath("target.sample",sampled,sample.position,queryFilter:targetFilter);
            if(!sampled) return Fail(NavigationFailure.InvalidTarget);
            var tooFar=Vector3.Distance(value.Position,sample.position)>actor.Settings.TargetSampleDistance+actor.Settings.Epsilon;
            TracePath("target.distance-refused",tooFar,sample.position);
            if(tooFar) return Fail(NavigationFailure.InvalidTarget);
            var wrongHeight=Mathf.Abs(sample.position.y-Position.y)>.05f; TracePath("target.start-height-refused",wrongHeight);
            if(wrongHeight) return Fail(NavigationFailure.InvalidTarget);
            wrongHeight=Mathf.Abs(sample.position.y-value.Position.y)>.05f; TracePath("target.request-height-refused",wrongHeight);
            if(wrongHeight) return Fail(NavigationFailure.InvalidTarget);
            if(value.AllowedRegion.HasValue)
            {
                var inside=value.AllowedRegion.Value.Contains(sample.position); TracePath("target.region",inside);
                if(!inside) return Fail(NavigationFailure.InvalidTarget);
            }
            var sameSide=SameSide(value.Position,sample.position); TracePath("target.side",sameSide);
            if(!sameSide) return Fail(NavigationFailure.InvalidTarget);
            destination = sample.position;
            var path = new NavMeshPath();
            var pathFilter=Filter;
#if UNITY_EDITOR
            var calculated=NavigationCostObservation.CalculatePath(2,Position,destination,pathFilter,path);
#else
            var calculated=NavMesh.CalculatePath(Position,destination,pathFilter,path);
#endif
            TracePath("path.calculate",calculated,destination,queryFilter:pathFilter);
            if(!calculated) return Fail(NavigationFailure.IncompletePath);
            var status=path.status; TracePath("path.status",status==NavMeshPathStatus.PathComplete,status:status);
            if(status!=NavMeshPathStatus.PathComplete) return Fail(NavigationFailure.IncompletePath);
            var completeCorners = path.corners; TracePath("path.corners",completeCorners.Length>0,path:completeCorners);
            if (completeCorners.Length == 0) return Fail(NavigationFailure.IncompletePath);
            actor.Agent.isStopped = false;
            var set=actor.Agent.SetPath(path); TracePath("path.set",set);
            if (!set)
            {
                // Original single rebind, unchanged queries/order and no extra retry.
                TracePath("rebind.layout",layoutAvailable);
                if(!layoutAvailable) return Fail(NavigationFailure.InvalidStart);
                active=actor.isActiveAndEnabled; TracePath("rebind.active",active);
                if(!active) return Fail(NavigationFailure.InvalidStart);
                if(actor.World != null)
                {
                    var canStart=actor.World.CanStartActor(actor); TracePath("rebind.world",canStart);
                    if(!canStart) return Fail(NavigationFailure.InvalidStart);
                }
                valid=ValidStart(); TracePath("rebind.valid",valid);
                if(!valid) return Fail(NavigationFailure.InvalidStart);
                actor.Agent.enabled = false;
                var bind=TryBind(); TracePath("rebind.bind",bind);
                if(!bind) return Fail(NavigationFailure.InvalidStart);
                path = new NavMeshPath();
                pathFilter=Filter;
#if UNITY_EDITOR
                calculated=NavigationCostObservation.CalculatePath(3,Position,destination,pathFilter,path);
#else
                calculated=NavMesh.CalculatePath(Position,destination,pathFilter,path);
#endif
                TracePath("rebind.calculate",calculated,destination,queryFilter:pathFilter);
                if(!calculated) return Fail(NavigationFailure.IncompletePath);
                status=path.status; TracePath("rebind.status",status==NavMeshPathStatus.PathComplete,status:status);
                if(status!=NavMeshPathStatus.PathComplete) return Fail(NavigationFailure.IncompletePath);
                var retryCorners=path.corners; TracePath("rebind.corners",retryCorners.Length>0,path:retryCorners);
                if(retryCorners.Length==0) return Fail(NavigationFailure.IncompletePath);
                actor.Agent.isStopped = false;
                set=actor.Agent.SetPath(path); TracePath("rebind.set",set);
                if(!set) return Fail(NavigationFailure.IncompletePath);
                completeCorners = path.corners;
            }
            pathLength = Length(Position, completeCorners);
            if (actor.SteadyPathMotion)
            {
                rawCorners = completeCorners; corners = (Vector3[])completeCorners.Clone();
                adjustedCorners = new bool[corners.Length]; corner = 0;
            }
            moving = true; TracePath("begin.accepted",true,destination,completeCorners);
            return NavigationFailure.None;
        }

        public NavigationObservation Observe()
        {
            var state = NavigationPathState.Complete;
            var remaining = 0f;
            if (failure != NavigationFailure.None || !Bound || actor.Agent.isOnOffMeshLink)
            { state = NavigationPathState.Invalid; if (failure == NavigationFailure.None) failure = NavigationFailure.InvalidStart; TracePath("observe.invalid-start",false); }
            else if (actor.Agent.pathPending) { state = NavigationPathState.Pending; TracePath("observe.pending"); }
            else if (moving && actor.Agent.pathStatus != NavMeshPathStatus.PathComplete)
            { state = NavigationPathState.Invalid; failure = NavigationFailure.IncompletePath; TracePath("observe.incomplete",false); }
            else if (moving) remaining = actor.SteadyPathMotion ? RemainingCornerDistance()
                : Mathf.Max(Vector3.Distance(Position, destination), Length(Position, actor.Agent.path.corners));
            var facing = target.Facing.HasValue ? Vector3.Angle(actor.transform.forward, target.Facing.Value) : 0;
            return new NavigationObservation(generation, revision, state, Position, pathLength, remaining, actualSpeed,
                facing, failure, destination);
        }

        public void AcceptPose(Vector3 position, Quaternion rotation, float speed)
        {
            actualSpeed = speed;
            // Simulation follows the approved position, never the other way around.
            if (Bound) actor.Agent.nextPosition = position;
        }

        public void Stop()
        {
            moving = false; actualSpeed = 0;
            corners = System.Array.Empty<Vector3>(); corner = 0;
            rawCorners = System.Array.Empty<Vector3>(); adjustedCorners = System.Array.Empty<bool>();
            if (!Bound) return;
            actor.Agent.isStopped = true; actor.Agent.ResetPath(); actor.Agent.velocity = Vector3.zero;
            actor.Agent.nextPosition = Position;
        }

        internal Vector3 ClampToNavMesh(Vector3 delta)
        {
            if (!Bound || !ValidStart() || !NavigationCollisionGuard.Finite(delta)) return Vector3.zero;
            delta.y = 0;
            if (NavMesh.Raycast(Position, Position + delta, out var hit, Filter))
            {
                var original = delta;
                var firstHit = hit;
                // Native smoothing can cut a convex polygon corner. Keep only its safe tangent.
                // Hit normal points into the walkable polygon; never invent a waypoint or push.
                var normal = hit.normal; normal.y = 0;
                if (NavigationCollisionGuard.Finite(normal) && normal.sqrMagnitude > 1e-12f)
                {
                    normal.Normalize();
                    var inward = Vector3.Dot(delta, normal);
                    if (inward < 0)
                        delta = Vector3.ClampMagnitude(delta - normal * inward, delta.magnitude);
                }
                // One projection only; another boundary remains a hard limit.
                if (NavMesh.Raycast(Position, Position + delta, out hit, Filter))
                {
                    if (CanKeepBoundaryTangent(original, firstHit, hit)) delta = original;
                    else
                    {
                        var offset = hit.position - Position; offset.y = 0;
                        delta = Vector3.ClampMagnitude(delta, Mathf.Max(0, offset.magnitude - actor.Settings.Epsilon));
                    }
                }
            }
            var end = Position + delta;
            if (!NavMesh.SamplePosition(end, out var sample, .05f, Filter) ||
                new Vector2(sample.position.x - end.x, sample.position.z - end.z).magnitude > actor.Settings.Epsilon ||
                Mathf.Abs(sample.position.y - end.y) > .05f) return Vector3.zero;
            return delta;
        }

        // Degenerate zero-normal seam only. Every certificate is read-only; the World
        // still applies its original full-body sweep. 查询内移不应用到实际actor。
        private bool CanKeepBoundaryTangent(Vector3 delta, NavMeshHit first, NavMeshHit second)
        {
            var epsilon = actor.Settings.Epsilon;
            if (!moving || !layoutAvailable || !actor.SteadyPathMotion || !Bound ||
                failure != NavigationFailure.None || actor.Agent.pathPending || actor.Agent.isOnOffMeshLink ||
                actor.Agent.pathStatus != NavMeshPathStatus.PathComplete || corner >= corners.Length || delta.sqrMagnitude == 0 ||
                !NavigationCollisionGuard.Finite(first.position) || !NavigationCollisionGuard.Finite(second.position) ||
                first.normal != Vector3.zero || second.normal != Vector3.zero ||
                Horizontal(first.position - Position).magnitude > epsilon || Horizontal(second.position - Position).magnitude > epsilon) return false;
            var toward = Horizontal(corners[corner] - Position);
            // Input must remain on the already owned current leg, never a new waypoint.
            if (toward.sqrMagnitude == 0 || Vector3.Dot(delta, toward) <= 0 ||
                Vector3.Cross(delta.normalized, toward.normalized).sqrMagnitude > epsilon * epsilon ||
                delta.magnitude > toward.magnitude + epsilon) return false;
            if (!NavMesh.FindClosestEdge(Position, out var edge, Filter) || !NavigationCollisionGuard.Finite(edge.position) ||
                !NavigationCollisionGuard.Finite(edge.normal) || Horizontal(edge.position - Position).magnitude > epsilon) return false;
            var normal = Horizontal(edge.normal);
            if (normal.sqrMagnitude <= 1e-12f) return false;
            normal.Normalize();
            // Exact tangent: an outward or inward requested step uses ordinary clipping.
            // 不允许把真实向外位移误判为沿边界；只处理原本的切向输入。
            var outward = Vector3.Dot(delta.normalized, normal);
            if (outward < 0 || outward > 1e-6f) return false;
            var end = Position + delta;
            if (!NavMesh.SamplePosition(end, out var sample, .05f, Filter) ||
                Horizontal(sample.position - end).magnitude > epsilon || Mathf.Abs(sample.position.y - end.y) > .05f) return false;
            var shortPath = new NavMeshPath();
#if UNITY_EDITOR
            if (!NavigationCostObservation.CalculatePath(4,Position,end,Filter,shortPath) || shortPath.status != NavMeshPathStatus.PathComplete) return false;
#else
            if (!NavMesh.CalculatePath(Position, end, Filter, shortPath) || shortPath.status != NavMeshPathStatus.PathComplete) return false;
#endif
            var proof = shortPath.corners;
            if (proof.Length != 2 || Horizontal(proof[0] - Position).magnitude > epsilon ||
                Horizontal(proof[1] - end).magnitude > epsilon || Mathf.Abs(proof[0].y - Position.y) > .05f ||
                Mathf.Abs(proof[1].y - end.y) > .05f) return false;
            var queryStart = Position + normal * epsilon;
            return !NavMesh.Raycast(queryStart, queryStart + delta, out _, Filter);
        }

        // 不越过转角、不补走guard裁剪的距离 / cap each substep at its current corner.
        internal Vector3 ClampToCurrentCorner(Vector3 delta)
        {
            if (!actor.SteadyPathMotion) return delta;
            if (!moving || corners.Length == 0) return Vector3.zero;
            AdvanceCorner();
            return Vector3.ClampMagnitude(delta, Horizontal(corners[corner] - Position).magnitude);
        }
        private void AdvanceCorner()
        {
            // 只根据真实水平pose推进；忽略NavMesh voxel的微小高度偏移。
            while (corner < corners.Length - 1)
            {
                var distance = Horizontal(corners[corner] - Position).magnitude;
                // A near corner can still separate two NavMesh polygons. Finish the tiny
                // approach unless the next complete leg is safe; never cut it by epsilon.
                // 接近拐点不等于已经过弯；下一腿不安全时走完原拐点，避免被边界裁成零。
                if (distance == 0 || (distance <= actor.Settings.Epsilon && actor.World != null &&
                    actor.World.CanTraverseStraight(actor, corners[corner + 1]))) { corner++; continue; }
                // Native corner按Agent半径生成，可能贴近完整skin。只在原到达范围内，
                // 且真实pose到下一corner整段通过原guards时，移除这个多余拐点。
                if (distance <= actor.Settings.ArrivalDistance && actor.World != null &&
                    actor.World.CanTraverseStraight(actor, corners[corner + 1])) { corner++; break; }
                if (corner > 0 && !adjustedCorners[corner])
                {
                    // 提前检查下一腿；贴桌角可能在进入到站范围前就被skin挡住。
                    // Failed connections may become safe as the actual pose/bodies change.
                    if (TrySkinSafeCorner(rawCorners[corner], out var candidate) && actor.World != null)
                    {
                        var offset = candidate - rawCorners[corner];
                        // The full outward offset can leave a narrow NavMesh strip. Try only
                        // bounded shorter offsets; every actual connection keeps both guards.
                        // 完整外移点可能越出窄NavMesh；有限缩短，每条真实连接仍须完整认证。
                        for (var attempt = 0; attempt < 4; attempt++)
                        {
                            candidate = rawCorners[corner] + offset;
                            if (actor.World.CanTraverseStraight(actor, candidate))
                            { corners[corner] = candidate; adjustedCorners[corner] = true; break; }
                            offset *= .5f;
                        }
                    }
                }
                break; // 每次只简化一段；最终destination始终保留。
            }
        }
        private bool TrySkinSafeCorner(Vector3 raw, out Vector3 candidate)
        {
            candidate = raw; var nearest = float.PositiveInfinity; var normal = Vector3.zero;
            var point = raw + Vector3.up * (actor.Settings.CapsuleHeight * .5f);
            foreach (var solid in solids)
            {
                if (solid == null || !solid.enabled || solid.isTrigger) continue;
                var outward = Horizontal(point - solid.ClosestPoint(point)); var distance = outward.magnitude;
                if (distance >= nearest) continue;
                nearest = distance; normal = outward;
            }
            if (!NavigationCollisionGuard.Finite(normal) || normal.sqrMagnitude < 1e-12f ||
                nearest > actor.Settings.AgentRadius + actor.Settings.CollisionSkin + actor.Settings.ArrivalDistance) return false;
            normal.Normalize();
            // 原到站范围内的单个绕角候选，给相邻圆角连接留边距。
            // 不改bake/skin/最终目标；只采用完整guards批准的真实连接。
            candidate = raw + normal * (actor.Settings.ArrivalDistance - actor.Settings.Epsilon);
            return NavigationCollisionGuard.Finite(candidate) &&
                Horizontal(candidate - raw).magnitude <= actor.Settings.ArrivalDistance;
        }
        private float RemainingCornerDistance()
        {
            AdvanceCorner();
            var previous = Position; var distance = 0f;
            for (var i = corner; i < corners.Length; i++)
            { distance += Horizontal(corners[i] - previous).magnitude; previous = corners[i]; }
            return distance;
        }
        private static Vector3 Horizontal(Vector3 value) { value.y = 0; return value; }

        private bool SameSide(Vector3 requested, Vector3 sampled)
        {
            // Check confirmed solids only, at body height; a thin wall cannot be sampled through.
            var from = requested + Vector3.up * (actor.Settings.CapsuleHeight * .5f);
            var to = sampled + Vector3.up * (actor.Settings.CapsuleHeight * .5f);
            foreach (var solid in solids)
            {
                if (solid == null || !solid.enabled || solid.isTrigger || solid.transform.IsChildOf(actor.transform)) continue;
                if ((solid.ClosestPoint(from) - from).sqrMagnitude < 1e-10f ||
                    (solid.ClosestPoint(to) - to).sqrMagnitude < 1e-10f) return false;
                var delta = to - from;
                if (delta.sqrMagnitude > 1e-10f && solid.Raycast(new Ray(from, delta.normalized), out _, delta.magnitude)) return false;
            }
            return true;
        }

        private NavigationFailure Fail(NavigationFailure reason) { failure = reason; TracePath("begin.failed",false); return reason; }
        private static float Length(Vector3 start, Vector3[] corners)
        {
            var length = 0f;
            // Same-floor route: do not count the voxel surface offset again at corner zero.
            foreach (var corner in corners)
            { var delta = corner - start; delta.y = 0; length += delta.magnitude; start = corner; }
            return length;
        }
    }
}

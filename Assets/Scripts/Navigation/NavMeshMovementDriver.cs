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
        private NavMeshQueryFilter Filter => new NavMeshQueryFilter { agentTypeID = ownedAgentType ?? actor.Agent.agentTypeID, areaMask = actor.Agent.areaMask };
        public Vector3 Position => actor.transform.position;
        public Vector3 DesiredVelocity
        {
            get
            {
                if (!moving || !Bound || actor.Agent.pathPending || actor.Agent.isOnOffMeshLink) return Vector3.zero;
                if (Vector3.Distance(Position, destination) <= actor.Settings.ArrivalDistance)
                { actor.Agent.isStopped = true; return Vector3.zero; }
                return Vector3.ClampMagnitude(actor.Agent.desiredVelocity, actor.Settings.MaxSpeed);
            }
        }
        internal Vector3? Facing => moving && Vector3.Distance(Position, destination) <= actor.Settings.ArrivalDistance ? target.Facing : null;
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

        public NavigationFailure BeginPath(NavigationTarget value, int pathGeneration, int layoutRevision)
        {
            Stop(); target = value; generation = pathGeneration; revision = layoutRevision;
            failure = NavigationFailure.None;
            if (!actor.isActiveAndEnabled) return Fail(NavigationFailure.ActorUnavailable);
            if (!layoutAvailable) return Fail(NavigationFailure.LayoutUnavailable);
            if ((actor.World != null && !actor.World.CanStartActor(actor)) || !ValidStart() || (!Bound && !TryBind())) return Fail(NavigationFailure.InvalidStart);
            if (!NavigationCollisionGuard.Finite(value.Position) ||
                !NavMesh.SamplePosition(value.Position, out var sample, actor.Settings.TargetSampleDistance, Filter) ||
                Vector3.Distance(value.Position, sample.position) > actor.Settings.TargetSampleDistance + actor.Settings.Epsilon ||
                Mathf.Abs(sample.position.y - Position.y) > .05f || Mathf.Abs(sample.position.y - value.Position.y) > .05f ||
                (value.AllowedRegion.HasValue && !value.AllowedRegion.Value.Contains(sample.position)) ||
                !SameSide(value.Position, sample.position)) return Fail(NavigationFailure.InvalidTarget);
            destination = sample.position;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(Position, destination, Filter, path) || path.status != NavMeshPathStatus.PathComplete)
                return Fail(NavigationFailure.IncompletePath);
            pathLength = Length(Position, path.corners);
            actor.Agent.isStopped = false;
            if (!actor.Agent.SetPath(path)) return Fail(NavigationFailure.IncompletePath);
            moving = true;
            return NavigationFailure.None;
        }

        public NavigationObservation Observe()
        {
            var state = NavigationPathState.Complete;
            var remaining = 0f;
            if (failure != NavigationFailure.None || !Bound || actor.Agent.isOnOffMeshLink)
            { state = NavigationPathState.Invalid; if (failure == NavigationFailure.None) failure = NavigationFailure.InvalidStart; }
            else if (actor.Agent.pathPending) state = NavigationPathState.Pending;
            else if (moving && actor.Agent.pathStatus != NavMeshPathStatus.PathComplete)
            { state = NavigationPathState.Invalid; failure = NavigationFailure.IncompletePath; }
            else if (moving) remaining = Mathf.Max(Vector3.Distance(Position, destination), Length(Position, actor.Agent.path.corners));
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
                    var offset = hit.position - Position; offset.y = 0;
                    delta = Vector3.ClampMagnitude(delta, Mathf.Max(0, offset.magnitude - actor.Settings.Epsilon));
                }
            }
            var end = Position + delta;
            if (!NavMesh.SamplePosition(end, out var sample, .05f, Filter) ||
                new Vector2(sample.position.x - end.x, sample.position.z - end.z).magnitude > actor.Settings.Epsilon ||
                Mathf.Abs(sample.position.y - end.y) > .05f) return Vector3.zero;
            return delta;
        }

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

        private NavigationFailure Fail(NavigationFailure reason) { failure = reason; return reason; }
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

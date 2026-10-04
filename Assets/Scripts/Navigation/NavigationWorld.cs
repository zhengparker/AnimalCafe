using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnimalCafe.Navigation
{
    // Single writer/coordinator for every registered character.
    public sealed class NavigationWorld : MonoBehaviour
    {
        private readonly NavigationSettings settings = new NavigationSettings();
        private readonly List<NavigationActor> actors = new List<NavigationActor>();
        private readonly Dictionary<NavigationActor, NavMeshMovementDriver> drivers = new Dictionary<NavigationActor, NavMeshMovementDriver>();
        private readonly List<NavigationCollisionGuard.Sweep> sweeps = new List<NavigationCollisionGuard.Sweep>();
        private readonly NavigationCollisionGuard guard = new NavigationCollisionGuard();
        private IReadOnlyList<Collider> solids = new Collider[0];
        private NavigationService service;
        private int? ownedAgentType;
        private IDisposable unavailableResumeBlock;
        private int currentRevision;
        private NavigationActor[] pendingStartupActors;
        // Only the validation fixture pins its initial roster; ordinary worlds stay dynamic.
        internal void RequireStartupActors(IReadOnlyList<NavigationActor> required)
        {
            pendingStartupActors=new NavigationActor[required.Count];
            for(var i=0;i<required.Count;i++) pendingStartupActors[i]=required[i];
        }
        public void HoldUnavailableResumeBlock(AnimalCafe.Core.Time.GameTimeService time)
        {
            if (unavailableResumeBlock == null && time != null)
                unavailableResumeBlock = time.AcquireResumeBlock(this, NavigationDecorationBridge.BlockedMessage);
            SuspendLayout(currentRevision);
        }
        public void ReleaseValidatedResumeBlock()
        {
            if (!LayoutAvailable) return;
            unavailableResumeBlock?.Dispose(); unavailableResumeBlock = null;
        }
        public IReadOnlyList<NavigationActor> RegisteredActors => actors.AsReadOnly();
        public bool LayoutAvailable { get; private set; } = true;
        public NavigationService Service => service ?? (service = new NavigationService(settings));

        public bool Register(NavigationActor actor)
        {
            return LayoutAvailable && RegisterAtCurrentPose(actor);
        }
        private bool RegisterAtCurrentPose(NavigationActor actor)
        {
            if (actor == null || !actor.isActiveAndEnabled || actor.Agent == null || actor.Proxy == null || actor.World != null || drivers.ContainsKey(actor) || !Compatible(actor)) return false;
            foreach (var registered in actors) if (registered.ActorId == actor.ActorId) return false;
            Physics.SyncTransforms(); guard.ApprovedSweeps = null;
            if (!guard.StartClear(actor, actors, solids)) return false;
            var driver = new NavMeshMovementDriver(actor); driver.SetGeometry(solids);
            if(ownedAgentType.HasValue) driver.SetOwnedAgentType(ownedAgentType.Value);
            if (!driver.TryBind() || !Service.Register(actor.ActorId, driver)) return false;
            actors.Add(actor); drivers.Add(actor, driver);
            actor.World = this;
            driver.SetLayoutAvailable(LayoutAvailable);
            actors.Sort((a, b) => string.CompareOrdinal(a.ActorId, b.ActorId));
            // Equal responsibility lets both agents propose local avoidance rather than one pushing ahead.
            foreach (var registered in actors) registered.Agent.avoidancePriority = 50;
            return true;
        }

        public void Unregister(NavigationActor actor)
        {
            if (actor == null || !drivers.TryGetValue(actor, out var driver)) return;
            // Retire World ownership before a terminal callback can register the actor again.
            // 先清理旧注册；回调中新注册的角色不再被外层清理误删。
            drivers.Remove(actor); actors.Remove(actor);
            driver.Stop(); actor.Agent.enabled = false;
            actor.SetMotion(0); actor.World = null;
            Service.Unregister(actor.ActorId);
        }

        internal void SetActorAvailable(NavigationActor actor, bool available)
        {
            if (!drivers.TryGetValue(actor, out var driver)) return;
            if (!available)
            {
                driver.Stop(); actor.Agent.enabled = false; actor.SetMotion(0);
            }
            // Keep the proxy reservation, including inactive GameObjects, until unregister/destroy.
            // Re-enable never changes pose; the next path validates the actual start again.
            Service.SetActorAvailable(actor.ActorId, available);
        }

        internal bool CanStartActor(NavigationActor actor)
        {
            Physics.SyncTransforms();
            return actor.isActiveAndEnabled && Compatible(actor) && guard.StartClear(actor, actors, solids);
        }

        public void SetGeometry(IReadOnlyList<Collider> confirmedSolids, int revision)
        {
            currentRevision=revision;
            solids = confirmedSolids == null ? new Collider[0] : new List<Collider>(confirmedSolids);
            Physics.SyncTransforms();
            foreach (var driver in drivers.Values) driver.SetGeometry(solids);
            // Completion callbacks may immediately request a path for the new revision.
            Service.InvalidateLayout(revision);
        }

        public void SuspendLayout(int revision)
        {
            currentRevision=revision;
            LayoutAvailable=false;
            foreach(var entry in drivers) { entry.Value.SetLayoutAvailable(false); if(entry.Key!=null) entry.Key.SetMotion(0); }
            Service.SuspendLayout(revision);
        }
        internal void SetOwnedAgentType(int agentType)
        {
            if(LayoutAvailable) throw new InvalidOperationException("Suspend layout before changing its agent type");
            ownedAgentType=agentType;
            foreach(var driver in drivers.Values) driver.SetOwnedAgentType(agentType);
        }
        public bool ValidateAndRebindActors(out string reason)
        {
            reason=""; Physics.SyncTransforms(); guard.ApprovedSweeps=null;
            if(pendingStartupActors!=null)
                foreach(var required in pendingStartupActors)
                    if(required==null || (!drivers.ContainsKey(required) && !RegisterAtCurrentPose(required)))
                    { reason="Required startup actor is unavailable: "+(required==null ? "missing" : required.ActorId); return false; }
            foreach(var actor in actors)
                if(actor==null || actor.Agent==null || actor.Proxy==null || !Compatible(actor) || !guard.StartClear(actor,actors,solids) || !drivers[actor].ValidStart())
                { reason="Invalid unchanged actor start: "+(actor==null?"missing":actor.ActorId); return false; }
            foreach(var actor in actors)
                if(actor.isActiveAndEnabled && !drivers[actor].TryBind()) { reason="Cannot bind unchanged actor start: "+actor.ActorId; return false; }
            pendingStartupActors=null; // Full initial validation succeeded; normal unregister semantics resume.
            return true;
        }
        public void SetLayoutAvailable(bool available)
        {
            LayoutAvailable=available;
            foreach(var driver in drivers.Values) driver.SetLayoutAvailable(available);
        }
        private void Update() => Step(Time.deltaTime); // GameTimeService already owns timeScale.

        public void Step(float scaledDeltaTime)
        {
            if (float.IsNaN(scaledDeltaTime) || float.IsInfinity(scaledDeltaTime) || scaledDeltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(scaledDeltaTime));
            if (scaledDeltaTime == 0 || !LayoutAvailable) return;
            foreach (var actor in actors)
                if (actor == null || actor.Agent == null || actor.Proxy == null || !Compatible(actor))
                {
                    foreach (var entry in drivers)
                        if (entry.Key != null) { entry.Value.AcceptPose(entry.Key.transform.position, entry.Key.transform.rotation, 0); entry.Key.SetMotion(0); }
                    Service.Tick(scaledDeltaTime);
                    return; // Mutated policy must not shrink another actor's obstacle for one frame.
                }
            var distances = new Dictionary<NavigationActor, float>();
            foreach (var actor in actors) distances[actor] = 0;
            var budget = Mathf.Min(scaledDeltaTime, settings.MaxSubstepSeconds * settings.MaxSubstepsPerFrame);
            Physics.SyncTransforms();
            var count = Mathf.Min(settings.MaxSubstepsPerFrame, Mathf.CeilToInt(budget / settings.MaxSubstepSeconds));
            for (var step = 0; step < count; step++)
            {
                var dt = Mathf.Min(settings.MaxSubstepSeconds, budget); budget -= dt;
                sweeps.Clear(); guard.ApprovedSweeps = sweeps;
                foreach (var actor in actors)
                {
                    if (actor == null || !actor.isActiveAndEnabled) continue;
                    var driver = drivers[actor]; var start = actor.transform.position;
                    var velocity = actor.isActiveAndEnabled ? driver.DesiredVelocity : Vector3.zero;
                    velocity.y = 0;
                    var proposed = guard.ProjectAvoidance(actor, velocity * dt, actors);
                    proposed = guard.ProjectStaticAvoidance(actor, proposed, solids);
                    proposed = driver.ClampToNavMesh(proposed);
                    var delta = guard.ClampDisplacement(actor, proposed, actors, solids);
                    var facing = driver.Facing ?? delta;
                    var rotation = actor.transform.rotation;
                    if (facing.sqrMagnitude > .000001f)
                        rotation = Quaternion.RotateTowards(rotation, Quaternion.LookRotation(facing), settings.TurnSpeedDegrees * dt);
                    actor.transform.SetPositionAndRotation(start + delta, rotation);
                    driver.AcceptPose(start + delta, rotation, delta.magnitude / dt);
                    distances[actor] += delta.magnitude;
                    sweeps.Add(new NavigationCollisionGuard.Sweep { Actor = actor, Start = start, Delta = delta });
                }
            }
            guard.ApprovedSweeps = null;
            foreach (var actor in actors) actor.SetMotion(distances[actor] / scaledDeltaTime);
            Service.Tick(scaledDeltaTime); // 超出运动预算的时间仍计入 deadline，不积累补走。
        }

        private bool Compatible(NavigationActor actor)
        {
            try { actor.Settings.Validate(); } catch (ArgumentOutOfRangeException) { return false; }
            var s = actor.Settings;
            // World/service use one policy. Reject mutated per-actor settings rather than silently disagree.
            if (s.AgentRadius != settings.AgentRadius || s.CapsuleHeight != settings.CapsuleHeight ||
                s.CollisionSkin != settings.CollisionSkin || s.Epsilon != settings.Epsilon ||
                s.MaxSpeed != settings.MaxSpeed || s.TurnSpeedDegrees != settings.TurnSpeedDegrees ||
                s.ArrivalDistance != settings.ArrivalDistance || s.ArrivalSpeed != settings.ArrivalSpeed ||
                s.FacingToleranceDegrees != settings.FacingToleranceDegrees || s.TargetSampleDistance != settings.TargetSampleDistance ||
                s.NoProgressSeconds != settings.NoProgressSeconds || s.ProgressDistance != settings.ProgressDistance ||
                s.PathPendingSeconds != settings.PathPendingSeconds || s.MinimumSegmentSeconds != settings.MinimumSegmentSeconds ||
                s.SegmentLengthMultiplier != settings.SegmentLengthMultiplier || s.MaxSubstepSeconds != settings.MaxSubstepSeconds ||
                s.MaxSubstepsPerFrame != settings.MaxSubstepsPerFrame) return false;
            return !string.IsNullOrWhiteSpace(actor.ActorId) && actor.transform.lossyScale == Vector3.one &&
                Mathf.Abs(actor.Agent.radius - s.AgentRadius) <= s.Epsilon &&
                Mathf.Abs(actor.Agent.height - s.CapsuleHeight) <= s.Epsilon &&
                Mathf.Abs(actor.Proxy.radius - s.AgentRadius) <= s.Epsilon &&
                Mathf.Abs(actor.Proxy.height - s.CapsuleHeight) <= s.Epsilon &&
                actor.Proxy.direction == 1 && Vector3.Distance(actor.Proxy.center, Vector3.up * s.CapsuleHeight * .5f) <= s.Epsilon;
        }

        private void OnDestroy()
        {
            service?.Shutdown();
            foreach (var actor in actors)
                if (actor != null) { actor.SetMotion(0); actor.World = null; if (actor.Agent != null) actor.Agent.enabled = false; }
            unavailableResumeBlock?.Dispose(); unavailableResumeBlock = null;
        }
    }
}

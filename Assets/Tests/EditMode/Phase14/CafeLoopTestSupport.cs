using System;
using System.Collections.Generic;
using AnimalCafe.CafeLoop;
using AnimalCafe.Employees;
using AnimalCafe.Layout;

namespace AnimalCafe.Tests.EditMode.Phase14
{
    // 只替代Scene边界；订单、计时和ownership规则须由真实service执行。
    internal sealed class CafeLoopEnvironmentFake : ICafeLoopEnvironment
    {
        internal bool Gate = true, ReserveAllowed = true, MoveAllowed = true, StartAllowed = true;
        internal bool DeliveryArrivalCurrent = true, CollectionAllowed = true;
        internal readonly List<CafeVisitSnapshot> VisitValues = new List<CafeVisitSnapshot>();
        internal readonly HashSet<EmployeeRole> IdleRoles = new HashSet<EmployeeRole>
            { EmployeeRole.Cashier, EmployeeRole.Barista };
        internal readonly HashSet<string> ReservedVisits = new HashSet<string>(StringComparer.Ordinal);
        internal readonly List<string> PickupMoves = new List<string>(), ExitMoves = new List<string>();
        internal readonly List<CafeTaskToken> StartedTokens = new List<CafeTaskToken>();
        internal readonly List<CafeWorkCheckpoint> StartedCheckpoints = new List<CafeWorkCheckpoint>();
        internal IReadOnlyList<CafeDeliverySnapshot> LastProjection = Array.Empty<CafeDeliverySnapshot>();
        internal Action<CafeTaskToken, CafeWorkCheckpoint> OnStart = null;
        internal Action OnProject = null;

        public bool CanProgress => Gate;
        public IReadOnlyList<CafeVisitSnapshot> Visits => VisitValues.AsReadOnly();
        public bool IsEmployeeIdle(EmployeeRole role) => IdleRoles.Contains(role);
        public string EmployeeId(EmployeeRole role) => role == EmployeeRole.Cashier ? "cashier-real" : "barista-real";
        public bool TryReservePickup(string visitId, out string reason)
        {
            reason = ReserveAllowed ? "" : "No legal pickup slot";
            if (!ReserveAllowed) return false;
            ReservedVisits.Add(visitId); return true;
        }
        public void ReleasePickupAssignment(string visitId) => ReservedVisits.Remove(visitId);
        public bool TryMoveToPickup(string visitId, out string reason)
        {
            reason = MoveAllowed ? "" : "Route blocked";
            if (!MoveAllowed) return false;
            PickupMoves.Add(visitId); return true;
        }
        public bool TryMoveToExit(string visitId, bool hasCollected, out string reason)
        {
            reason = MoveAllowed && hasCollected ? "" : "Exit unavailable or not collected";
            if (!MoveAllowed || !hasCollected) return false;
            ExitMoves.Add(visitId); return true;
        }
        public bool TryStartWork(CafeTaskToken token, CafeWorkCheckpoint checkpoint, out string reason)
        {
            reason = StartAllowed && IdleRoles.Contains(checkpoint.Role) ? "" : "Employee unavailable";
            if (!StartAllowed || !IdleRoles.Remove(checkpoint.Role)) return false;
            StartedTokens.Add(token); StartedCheckpoints.Add(checkpoint);
            OnStart?.Invoke(token, checkpoint); return true;
        }
        public bool IsCurrentDeliveryArrival(CafeTaskEvent arrival, SurfaceSlotAddress address)
            => DeliveryArrivalCurrent;
        public bool CanCollect(string visitId, CafeOrderKey key) => CollectionAllowed;
        public void ProjectDeliveries(IReadOnlyList<CafeDeliverySnapshot> deliveries)
        {
            LastProjection = new List<CafeDeliverySnapshot>(deliveries).AsReadOnly();
            OnProject?.Invoke();
        }
    }
}

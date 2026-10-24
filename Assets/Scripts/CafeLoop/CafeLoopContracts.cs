using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AnimalCafe.Customers;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using AnimalCafe.Orders;
using UnityEngine;

namespace AnimalCafe.CafeLoop
{
    // 不同经营批次的数字Order ID可能相同；必须保留完整身份 / Full business identity.
    public readonly struct CafeOrderKey
    {
        public string SessionId { get; }
        public long Epoch { get; }
        public long OrderId { get; }
        public CafeOrderKey(string sessionId, long epoch, long orderId)
        {
            CafeContractRules.Identity(sessionId, nameof(sessionId));
            CafeContractRules.Positive(epoch, nameof(epoch));
            CafeContractRules.Positive(orderId, nameof(orderId));
            SessionId = sessionId; Epoch = epoch; OrderId = orderId;
        }
    }

    public readonly struct CafeTaskToken
    {
        public string SessionId { get; }
        public long Epoch { get; }
        public long Sequence { get; }
        public int Attempt { get; }
        public CafeTaskToken(string sessionId, long epoch, long sequence, int attempt)
        {
            CafeContractRules.Identity(sessionId, nameof(sessionId));
            CafeContractRules.Positive(epoch, nameof(epoch));
            CafeContractRules.Positive(sequence, nameof(sequence));
            CafeContractRules.Positive(attempt, nameof(attempt));
            SessionId = sessionId; Epoch = epoch; Sequence = sequence; Attempt = attempt;
        }
    }

    public enum CafeWorkStage { Working, Prepared, Delivered, Returning, Finished }
    public enum CafeTaskEventKind { Progress, ServiceFinished, Prepared, DeliveryArrived, Returned, Blocked }

    public readonly struct CafeWorkCheckpoint
    {
        public EmployeeRole Role { get; }
        public string VisitId { get; }
        public CafeOrderKey? Order { get; }
        public CafeWorkStage Stage { get; }
        public float RemainingWorkSeconds { get; }
        public CafeWorkCheckpoint(EmployeeRole role, string visitId, CafeOrderKey? order,
            CafeWorkStage stage, float remainingWorkSeconds)
        {
            if (!Enum.IsDefined(typeof(EmployeeRole), role)) throw new ArgumentOutOfRangeException(nameof(role));
            if (!Enum.IsDefined(typeof(CafeWorkStage), stage)) throw new ArgumentOutOfRangeException(nameof(stage));
            CafeContractRules.Identity(visitId, nameof(visitId));
            if (float.IsNaN(remainingWorkSeconds) || float.IsInfinity(remainingWorkSeconds) || remainingWorkSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(remainingWorkSeconds));
            // 已完成制作的checkpoint不能重新携带工作计时 / No repeated preparation.
            if (stage != CafeWorkStage.Working && remainingWorkSeconds != 0)
                throw new ArgumentException("已完成工作的checkpoint不能保留工作秒数。", nameof(remainingWorkSeconds));
            Role = role; VisitId = visitId; Order = order; Stage = stage; RemainingWorkSeconds = remainingWorkSeconds;
        }
    }

    public readonly struct CafeTaskEvent
    {
        public CafeTaskToken Token { get; }
        public EmployeeRole Role { get; }
        public string ActorId { get; }
        public long HandleId { get; }
        public long RequestId { get; }
        public int Generation { get; }
        public int LayoutRevision { get; }
        public CafeTaskEventKind Kind { get; }
        public CafeWorkCheckpoint Checkpoint { get; }
        public string Failure { get; }
        public CafeTaskEvent(CafeTaskToken token, EmployeeRole role, string actorId, long handleId,
            long requestId, int generation, int layoutRevision, CafeTaskEventKind kind,
            CafeWorkCheckpoint checkpoint, string failure = "")
        {
            Token = token; Role = role; ActorId = actorId; HandleId = handleId; RequestId = requestId;
            Generation = generation; LayoutRevision = layoutRevision; Kind = kind;
            Checkpoint = checkpoint; Failure = failure;
        }
    }

    public readonly struct CafeVisitSnapshot
    {
        public string VisitId { get; }
        public CustomerVisitState NavigationState { get; }
        public Vector3 Position { get; }
        public bool CounterArrived { get; }
        public bool PickupArrived { get; }
        public bool PickupDeparted { get; }
        public bool ExitArrived { get; }
        public bool IsEnabled { get; }
        public int LayoutRevision { get; }
        public CafeVisitSnapshot(string visitId, CustomerVisitState navigationState, Vector3 position,
            bool counterArrived = false, bool pickupArrived = false, bool pickupDeparted = false,
            bool exitArrived = false, bool isEnabled = true, int layoutRevision = 0)
        {
            VisitId = visitId; NavigationState = navigationState; Position = position;
            CounterArrived = counterArrived; PickupArrived = pickupArrived;
            PickupDeparted = pickupDeparted; ExitArrived = exitArrived;
            IsEnabled = isEnabled; LayoutRevision = layoutRevision;
        }
    }

    public readonly struct CafeDeliverySnapshot
    {
        public CafeOrderKey Key { get; }
        public string VisitId { get; }
        public SurfaceSlotAddress Address { get; }
        public long DeliverySequence { get; }
        public CafeDeliverySnapshot(CafeOrderKey key, string visitId, SurfaceSlotAddress address, long deliverySequence)
        { Key = key; VisitId = visitId; Address = address; DeliverySequence = deliverySequence; }
    }

    public sealed class CafeLoopSnapshot
    {
        public string SessionId { get; }
        public long Epoch { get; }
        public IReadOnlyDictionary<CafeOrderKey, OrderSnapshot> Orders { get; }
        public IReadOnlyList<string> PaidVisitIds { get; }
        public IReadOnlyList<string> CollectedExitVisitIds { get; }
        public IReadOnlyList<CafeDeliverySnapshot> Delivery { get; }
        public IReadOnlyList<CafeVisitSnapshot> Visits { get; }
        public IReadOnlyList<CafeWorkCheckpoint> Employees { get; }
        public string Status { get; }
        public string Failure { get; }
        public CafeLoopSnapshot(string sessionId, long epoch,
            IReadOnlyDictionary<CafeOrderKey, OrderSnapshot> orders = null,
            IReadOnlyList<string> paidVisitIds = null, IReadOnlyList<string> collectedExitVisitIds = null,
            IReadOnlyList<CafeDeliverySnapshot> delivery = null, IReadOnlyList<CafeVisitSnapshot> visits = null,
            IReadOnlyList<CafeWorkCheckpoint> employees = null, string status = "", string failure = "")
        {
            CafeContractRules.Identity(sessionId, nameof(sessionId));
            CafeContractRules.Positive(epoch, nameof(epoch));
            SessionId = sessionId; Epoch = epoch;
            Orders = new ReadOnlyDictionary<CafeOrderKey, OrderSnapshot>(orders == null
                ? new Dictionary<CafeOrderKey, OrderSnapshot>()
                : orders.ToDictionary(pair => pair.Key, pair => pair.Value));
            // Readonly接口不够：先copy，避免原List之后改变snapshot / Copy before wrapping.
            PaidVisitIds = CafeContractRules.Copy(paidVisitIds);
            CollectedExitVisitIds = CafeContractRules.Copy(collectedExitVisitIds);
            Delivery = CafeContractRules.Copy(delivery); Visits = CafeContractRules.Copy(visits);
            Employees = CafeContractRules.Copy(employees); Status = status ?? ""; Failure = failure ?? "";
        }
    }

    public delegate bool CafeDeliveryCommit(CafeTaskEvent arrival, SurfaceSlotAddress address, out string reason);

    public interface ICafeLoopEnvironment
    {
        bool CanProgress { get; }
        IReadOnlyList<CafeVisitSnapshot> Visits { get; }
        bool IsEmployeeIdle(EmployeeRole role);
        string EmployeeId(EmployeeRole role);
        bool TryReservePickup(string visitId, out string reason);
        void ReleasePickupAssignment(string visitId);
        bool TryMoveToPickup(string visitId, out string reason);
        bool TryMoveToExit(string visitId, bool hasCollected, out string reason);
        bool TryStartWork(CafeTaskToken token, CafeWorkCheckpoint checkpoint, out string reason);
        bool IsCurrentDeliveryArrival(CafeTaskEvent arrival, SurfaceSlotAddress address);
        bool CanCollect(string visitId, CafeOrderKey key);
        void ProjectDeliveries(IReadOnlyList<CafeDeliverySnapshot> deliveries);
    }

    internal static class CafeContractRules
    {
        internal static void Identity(string value, string parameter)
        {
            if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
                throw new ArgumentException("身份不能为空或含首尾空白 / Invalid business ID.", parameter);
        }
        internal static void Positive(long value, string parameter)
        { if (value <= 0) throw new ArgumentOutOfRangeException(parameter); }
        internal static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
            => new List<T>(source ?? Array.Empty<T>()).AsReadOnly();
    }
}

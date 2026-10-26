using System;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using AnimalCafe.CafeLoop;

namespace AnimalCafe.Employees
{
    public enum EmployeeRole { Cashier, Barista }

    public enum EmployeeState { Initializing, Idle, MovingToWork, Working, CarryingToPickUp, Returning, Blocked, Disabled, WaitingToDeliver }
    public enum EmployeeTaskOutcome { None, Succeeded, Failed, Cancelled }
    public enum EmployeeVisualMode { Idle, Walking, Working, Carrying }

    public readonly struct EmployeeTaskSnapshot
    {
        public string ActorId { get; }
        public EmployeeRole Role { get; }
        public EmployeeState State { get; }
        public long HandleId { get; }
        public EmployeeTaskOutcome Outcome { get; }
        public float RemainingWorkSeconds { get; }
        public bool Delivered { get; }
        public string Failure { get; }
        public EmployeeTaskSnapshot(string actorId, EmployeeRole role, EmployeeState state, long handleId,
            EmployeeTaskOutcome outcome, float remainingWorkSeconds, bool delivered, string failure)
        {
            ActorId = actorId; Role = role; State = state; HandleId = handleId; Outcome = outcome;
            RemainingWorkSeconds = remainingWorkSeconds; Delivered = delivered; Failure = failure;
        }
    }

    public interface IEmployeeTaskEnvironment
    {
        bool IsProgressAllowed { get; }
        MoveStartResult MoveTo(NavigationTarget target, Action<MovementResult> completed);
        bool CancelMove(long requestId);
        bool TryShowHeldCup(out string reason);
        bool TryPlaceCup(SurfaceSlotAddress address, out string reason);
        void SetVisualMode(EmployeeVisualMode mode);
        void CleanupTask(long handleId);
    }
    // P14扩展保持独立，P13 ports无需实现新成员 / Optional integrated task port.
    public interface IIntegratedEmployeeTaskEnvironment : IEmployeeTaskEnvironment
    {
        bool TryAcquireDelivery(string actorId);
        bool TryCommitDelivery(CafeTaskEvent arrival, SurfaceSlotAddress address, out string reason);
        void HideHeldCup();
    }
    public sealed class EmployeeStationBinding
    {
        public EmployeeRole Role { get; }
        public string WorkStationId { get; }
        public string PickUpId { get; }
        public NavigationTarget WorkTarget { get; }
        public NavigationTarget? PickUpTarget { get; }
        public SurfaceSlotAddress? CupAddress { get; }
        public int LayoutRevision { get; }

        public EmployeeStationBinding(EmployeeRole role, string workStationId, string pickUpId,
            NavigationTarget workTarget, NavigationTarget? pickUpTarget,
            SurfaceSlotAddress? cupAddress, int layoutRevision)
        {
            if (!Enum.IsDefined(typeof(EmployeeRole), role))
                throw new ArgumentOutOfRangeException(nameof(role));
            if (!StableId.IsValidFurnitureInstanceId(workStationId))
                throw new ArgumentException("Work station 必须使用正式 InstanceId。", nameof(workStationId));
            if (layoutRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(layoutRevision));

            // Cashier 没有交付目标；Barista 必须同时保存正式 Pick-up ID、位置和 Slot。
            if (role == EmployeeRole.Cashier)
            {
                if (!string.IsNullOrEmpty(pickUpId) || pickUpTarget.HasValue || cupAddress.HasValue)
                    throw new ArgumentException("Cashier binding 不能包含 Pick-up 数据。");
            }
            else
            {
                if (!StableId.IsValidFurnitureInstanceId(pickUpId) || !pickUpTarget.HasValue || !cupAddress.HasValue)
                    throw new ArgumentException("Barista binding 缺少完整 Pick-up 数据。");
                SurfaceSlotAddress.Validate(cupAddress.Value, nameof(cupAddress));
            }

            Role = role;
            WorkStationId = workStationId;
            PickUpId = pickUpId;
            WorkTarget = workTarget;
            PickUpTarget = pickUpTarget;
            CupAddress = cupAddress;
            LayoutRevision = layoutRevision;
        }
    }
}


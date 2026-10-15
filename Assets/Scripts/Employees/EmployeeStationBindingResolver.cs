using System;
using System.Linq;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using UnityEngine;

namespace AnimalCafe.Employees
{
    public sealed class EmployeeStationBindingResolver
    {
        public bool TryResolve(EmployeeRole role, LayoutReadinessReport report, string selectedRegisterId,
            int layoutRevision, Func<InteractionAnchor, NavigationTarget> toTarget,
            Func<Vector3, Vector3, bool> canReach, out EmployeeStationBinding binding, out string reason)
        {
            binding = null;
            if (!Enum.IsDefined(typeof(EmployeeRole), role) || layoutRevision < 0 ||
                report == null || toTarget == null || canReach == null)
            {
                reason = "Employee binding 参数或 Layout readiness 无效。";
                return false;
            }

            if (role == EmployeeRole.Cashier)
            {
                // 与 P12 保持同一 ordinal fallback；已选中的 CR 失效时不能偷偷换岗。
                var registers = report.Stations
                    .Where(station => station.FunctionType == LayoutStationType.CashRegister)
                    .OrderBy(station => station.InstanceId, StringComparer.Ordinal);
                var cash = string.IsNullOrEmpty(selectedRegisterId)
                    ? registers.FirstOrDefault(station => station.IsValid)
                    : registers.FirstOrDefault(station => station.InstanceId == selectedRegisterId);
                if (cash != null && cash.IsValid &&
                    cash.Anchors.TryGetAnchor(InteractionRole.Employee, out var workAnchor))
                {
                    binding = new EmployeeStationBinding(role, cash.InstanceId, null,
                        toTarget(workAnchor), null, null, layoutRevision);
                    reason = "";
                    return true;
                }

                reason = string.IsNullOrEmpty(selectedRegisterId)
                    ? "没有可用 Cash Register Employee Anchor。"
                    : $"选中的 Cash Register '{selectedRegisterId}' 缺失或 Employee Anchor 不可用。";
                return false;
            }

            var machines = report.Stations
                .Where(station => station.IsValid && station.FunctionType == LayoutStationType.CoffeeMachine)
                .OrderBy(station => station.InstanceId, StringComparer.Ordinal);
            var pickups = report.Stations
                .Where(station => station.IsValid && station.FunctionType == LayoutStationType.PickUpPoint)
                .OrderBy(station => station.InstanceId, StringComparer.Ordinal).ToArray();

            foreach (var machine in machines)
            {
                if (!machine.Anchors.TryGetAnchor(InteractionRole.Employee, out var workAnchor)) continue;
                var workTarget = toTarget(workAnchor);
                foreach (var pickup in pickups)
                {
                    if (!pickup.Anchors.TryGetAnchor(InteractionRole.Employee, out var deliveryAnchor)) continue;
                    var deliveryTarget = toTarget(deliveryAnchor);
                    // 制杯、交付、返回组成完整路径；这里只查询，不生成 actor 或 Cup。
                    if (!canReach(workTarget.Position, deliveryTarget.Position) ||
                        !canReach(deliveryTarget.Position, workTarget.Position)) continue;

                    binding = new EmployeeStationBinding(role, machine.InstanceId, pickup.InstanceId,
                        workTarget, deliveryTarget,
                        new SurfaceSlotAddress(pickup.SupportFurnitureInstanceId, pickup.SurfaceSlotId),
                        layoutRevision);
                    reason = "";
                    return true;
                }
            }

            reason = "没有具备完整 Employee Anchor 往返路径的 Coffee Machine / Pick-up Point 组合。";
            return false;
        }
    }
}

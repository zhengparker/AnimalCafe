using System.Linq;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Navigation;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.Employees
{
    /// <summary>受控任务入口 / Small scene debug HUD, no order or automatic task queue.</summary>
    public sealed class EmployeeTaskValidationController : MonoBehaviour
    {
        [SerializeField] private EmployeeFlowController employees;
        [SerializeField] private CustomerFlowController customers;
        [SerializeField] private GameTimeService time;
        [SerializeField] private DecorationModeController decoration;
        [SerializeField] private NavigationDecorationBridge bridge;
        [SerializeField] private PickupCupDisplay cups;
        [SerializeField] private GameObject hud;
        [SerializeField] private Text status;
        [SerializeField] private Button cashierButton, baristaButton;
        private string feedback = "等待员工回到岗位 / Wait for Idle";

        public void Configure(EmployeeFlowController employees, CustomerFlowController customers, GameTimeService time)
        { this.employees = employees; this.customers = customers; this.time = time; }
        private void OnEnable()
        {
            if (cashierButton != null) cashierButton.onClick.AddListener(StartCashier);
            if (baristaButton != null) baristaButton.onClick.AddListener(StartBarista);
        }
        private void OnDisable()
        {
            if (cashierButton != null) cashierButton.onClick.RemoveListener(StartCashier);
            if (baristaButton != null) baristaButton.onClick.RemoveListener(StartBarista);
        }
        private void StartCashier() => StartTask(EmployeeRole.Cashier);
        private void StartBarista() => StartTask(EmployeeRole.Barista);
        private void StartTask(EmployeeRole role)
        {
            if (employees != null && employees.TryStartTask(role)) { feedback = role + "：任务已开始"; return; }
            var snapshot = employees == null ? default : employees.Snapshot.FirstOrDefault(value => value.Role == role);
            feedback = time == null ? "GameTime 未接线" : time.CurrentSpeed == GameSpeed.Paused || time.IsResumeBlocked
                ? "暂停 / Paused：" + time.ResumeBlockReason
                : role + "：" + snapshot.State + "，忙碌或岗位未就绪 / Busy or not ready " + snapshot.Failure;
        }
        private void LateUpdate()
        {
            // Decor owns its existing tools; only hide the debug overlay while it is open.
            if (hud != null) hud.SetActive(decoration == null || !decoration.IsOpen);
            if (status == null || employees == null) return;
            status.text = string.Join("\n", employees.Snapshot.Select(value =>
            {
                var bound = employees.TryGetBinding(value.Role, out var binding);
                var count = bound && binding.CupAddress.HasValue && cups != null ? cups.VisibleCount(binding.CupAddress.Value) : 0;
                return value.Role + " " + value.State + " " + value.RemainingWorkSeconds.ToString("F1") + "s " + value.Outcome +
                    "\n" + (bound ? binding.WorkStationId + (binding.CupAddress.HasValue ? " → " + binding.PickUpId + " [" + binding.CupAddress.Value.SlotId + "] " + count + "/3杯" : "") : "等待有效设备") +
                    (string.IsNullOrEmpty(value.Failure) ? "" : "\n" + value.Failure);
            })) + "\nP12 CR: " + (customers == null ? "未接线" : customers.SelectedRegisterId) + "\n" + feedback;
        }
        [ContextMenu("P13/Cancel Barista Task")] private void CancelBarista() => employees.TryCancelTask(EmployeeRole.Barista);
        [ContextMenu("P13/Cancel Cashier Task")] private void CancelCashier() => employees.TryCancelTask(EmployeeRole.Cashier);
        [ContextMenu("P13/Enable Customer Arrivals")] private void EnableArrivals() { if (customers != null) customers.AutoSpawn = true; }
        [ContextMenu("P13/Disable Customer Arrivals")] private void DisableArrivals() { if (customers != null) customers.AutoSpawn = false; }
        [ContextMenu("P13/Let Front Customer Leave")] private void Leave() { if (customers != null) customers.TryLetFrontLeave(); }
        [ContextMenu("P13/Retry Blocked Employees")] private void Retry()
        { if (bridge != null) bridge.ResumeAfterRepair(); if (employees != null) employees.RetryBlockedEmployees(); }
    }
}

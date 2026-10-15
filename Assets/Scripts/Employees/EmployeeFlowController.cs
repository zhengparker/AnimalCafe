using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using UnityEngine;

namespace AnimalCafe.Employees
{
    /// <summary>两名员工的 Scene 接线；移动只交给 NavigationWorld / Scene orchestration only.</summary>
    public sealed class EmployeeFlowController : MonoBehaviour
    {
        [SerializeField] private NavigationWorld world;
        [SerializeField] private NavigationLayoutAdapter adapter;
        [SerializeField] private CafeLayoutRuntime layout;
        [SerializeField] private DecorationModeController decoration;
        [SerializeField] private GameTimeService time;
        [SerializeField] private CustomerFlowController customers;
        [SerializeField] private Transform gridRoot;
        [SerializeField] private NavigationActor cashierPrefab, baristaPrefab;
        [SerializeField] private PickupCupDisplay display;
        // Task7 builder assigns the original Cup explicitly; missing configuration must fail.
        [SerializeField] private GameObject cupPrefab;

        private sealed class Employee
        {
            internal EmployeeRole Role;
            internal string Id, Failure = "";
            internal bool SpawnAttempted, NeedsBinding;
            internal NavigationActor Actor;
            internal EmployeeStationBinding Binding;
            internal EmployeeVisualPresenter Visual;
            internal EmployeeTaskRunner Runner;
            internal Environment Port;
        }
        private readonly List<Employee> employees = new List<Employee>(2);
        private readonly EmployeeStationBindingResolver resolver = new EmployeeStationBindingResolver();
        private string session;
        private bool disposed;
        private int observedRevision;
        private long completedDecorVersion;

        public IReadOnlyList<EmployeeTaskSnapshot> Snapshot => employees.Select(employee => employee.Runner?.Snapshot ??
            new EmployeeTaskSnapshot(employee.Id, employee.Role,
                string.IsNullOrEmpty(employee.Failure) ? EmployeeState.Initializing : EmployeeState.Blocked,
                0, EmployeeTaskOutcome.None, 0, false, employee.Failure)).ToList().AsReadOnly();

        public void Configure(NavigationWorld world, NavigationLayoutAdapter adapter, CafeLayoutRuntime layout,
            DecorationModeController decoration, GameTimeService time, CustomerFlowController customers,
            Transform gridRoot, NavigationActor cashierPrefab, NavigationActor baristaPrefab, PickupCupDisplay display)
        {
            CleanupSession();
            this.world = world; this.adapter = adapter; this.layout = layout; this.decoration = decoration;
            this.time = time; this.customers = customers; this.gridRoot = gridRoot;
            this.cashierPrefab = cashierPrefab; this.baristaPrefab = baristaPrefab; this.display = display;
            BeginSession();
        }

        private void Awake() => BeginSession();
        private void BeginSession()
        {
            disposed = false; session = Guid.NewGuid().ToString("N"); employees.Clear();
            observedRevision = adapter == null ? -1 : adapter.Revision;
            completedDecorVersion = decoration == null ? 0 : decoration.CompletedLayoutChangeVersion;
            foreach (EmployeeRole role in Enum.GetValues(typeof(EmployeeRole)))
                employees.Add(new Employee { Role = role, Id = "employee-" + session + "-" + role });
            if (display != null) display.Configure(cupPrefab, ResolveConfirmedSlot);
        }
        private void Update() => Step(Time.deltaTime); // GameTimeService has already scaled this delta.

        private bool Ready => !disposed && isActiveAndEnabled && world != null && world.isActiveAndEnabled &&
            adapter != null && adapter.isActiveAndEnabled && adapter.World == world &&
            layout != null && layout.Layout != null && layout.FunctionalSurfaceLayout != null &&
            layout.CurrentReadiness != null && layout.CurrentReadiness.CanOpenForBusiness &&
            adapter.CurrentReadiness.CanResume && adapter.PublishedRevision == adapter.Revision &&
            world.LayoutAvailable && gridRoot != null && time != null && customers != null &&
            !string.IsNullOrEmpty(customers.SelectedRegisterId) && SelectionCurrent;
        // P12 may run later this frame. Wait until its selected CR matches the current formal ordinal choice.
        private bool SelectionCurrent => layout.CurrentReadiness.Stations
            .Where(station => station.IsValid && station.FunctionType == LayoutStationType.CashRegister)
            .OrderBy(station => station.InstanceId, StringComparer.Ordinal)
            .Select(station => station.InstanceId).FirstOrDefault() == customers.SelectedRegisterId;
        private bool ProgressAllowed => Ready && time.CurrentSpeed != GameSpeed.Paused &&
            !time.IsResumeBlocked && (decoration == null || !decoration.IsOpen);

        public void Step(float scaledDeltaTime)
        {
            if (float.IsNaN(scaledDeltaTime) || float.IsInfinity(scaledDeltaTime) || scaledDeltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(scaledDeltaTime));
            if (disposed || !isActiveAndEnabled) return;
            ObserveLayoutChanges();
            foreach (var employee in employees)
            {
                if (employee.SpawnAttempted && employee.Runner != null &&
                    (employee.Actor == null || !employee.Actor.isActiveAndEnabled))
                {
                    employee.Runner?.Dispose(); employee.Port?.Dispose(); employee.NeedsBinding = true;
                    continue; // Keep World registration and its body reservation.
                }
                if (!ProgressAllowed) continue;
                if (employee.Actor != null && (employee.Runner == null || employee.Runner.Snapshot.State == EmployeeState.Disabled))
                { StartRunner(employee); employee.NeedsBinding = true; }
                if (employee.Runner == null && !employee.SpawnAttempted) TryCreate(employee);
                else if (employee.NeedsBinding) Rebind(employee);
                employee.Runner?.Tick(scaledDeltaTime);
            }
        }

        public bool TryStartTask(EmployeeRole role)
        {
            var employee = employees.FirstOrDefault(value => value.Role == role);
            return ProgressAllowed && employee?.Actor != null && employee.Actor.isActiveAndEnabled && employee.Runner != null && ValidBinding(employee.Binding) && employee.Runner.TryStart();
        }
        // HUD reads the controller's actual immutable choice, never resolves a second binding.
        public bool TryGetBinding(EmployeeRole role, out EmployeeStationBinding binding)
        {
            binding = employees.FirstOrDefault(value => value.Role == role)?.Binding;
            return binding != null;
        }
        public bool TryCancelTask(EmployeeRole role)
        {
            var employee = employees.FirstOrDefault(value => value.Role == role);
            if (disposed || employee?.Runner == null || employee.Runner.Snapshot.HandleId == 0) return false;
            employee.Runner.Cancel("玩家取消任务 / Task cancelled"); return true;
        }
        public void RetryBlockedEmployees()
        {
            if (!ProgressAllowed) return;
            foreach (var employee in employees)
            {
                if (employee.SpawnAttempted && employee.Runner != null && (employee.Actor == null || !employee.Actor.isActiveAndEnabled)) continue;
                if (employee.Actor != null && (employee.Runner == null || employee.Runner.Snapshot.State == EmployeeState.Disabled))
                { StartRunner(employee); employee.NeedsBinding = true; }
                if (employee.NeedsBinding) { Rebind(employee); continue; }
                if (employee.Runner == null) { employee.SpawnAttempted = false; TryCreate(employee); }
                else if (employee.Runner.Snapshot.State == EmployeeState.Blocked)
                    Rebind(employee);
            }
        }

        private void ObserveLayoutChanges()
        {
            if (adapter == null) return;
            if (observedRevision != adapter.Revision)
            {
                observedRevision = adapter.Revision;
                foreach (var employee in employees)
                {
                    employee.Runner?.InvalidateLayout(observedRevision);
                    if (employee.Runner != null && layout != null && customers != null &&
                        !resolver.TryResolve(employee.Role, layout.CurrentReadiness, customers.SelectedRegisterId,
                            observedRevision, ToTarget, adapter.HasClearPath, out _, out var reason))
                        employee.Runner.RejectBinding("工作站不可用 / Station unavailable: " + reason);
                    employee.NeedsBinding = employee.Actor != null;
                }
                display?.RefreshConfirmedSupports();
            }
            var version = decoration == null ? completedDecorVersion : decoration.CompletedLayoutChangeVersion;
            if (version != completedDecorVersion)
            {
                completedDecorVersion = version;
                foreach (var employee in employees) employee.NeedsBinding = employee.Actor != null;
                display?.RefreshConfirmedSupports();
            }
        }

        private void StartRunner(Employee employee)
        {
            employee.Port?.Dispose();
            employee.Port = new Environment(this, employee, session);
            employee.Runner = new EmployeeTaskRunner(employee.Id, employee.Role, employee.Port);
        }

        private void Rebind(Employee employee)
        {
            if (!ProgressAllowed || employee.Actor == null || !employee.Actor.isActiveAndEnabled) return;
            // Confirm can happen repeatedly while Decor remains open. Only its completed session may rebind.
            employee.NeedsBinding = false;
            if (!resolver.TryResolve(employee.Role, layout.CurrentReadiness, customers.SelectedRegisterId,
                adapter.Revision, ToTarget, adapter.HasClearPath, out var binding, out var reason) || !ValidBinding(binding))
            { employee.Runner.RejectBinding(string.IsNullOrEmpty(reason) ? "正式工作站不可用 / Station unavailable" : reason); return; }
            employee.Binding = binding;
            if (employee.Runner.TryAssignBinding(binding)) employee.Runner.TryReturnToWork();
        }

        private void TryCreate(Employee employee)
        {
            if (!resolver.TryResolve(employee.Role, layout.CurrentReadiness, customers.SelectedRegisterId,
                adapter.Revision, ToTarget, adapter.HasClearPath, out var binding, out var reason))
            { employee.Failure = reason; return; }
            employee.Binding = binding;
            if (!ValidBinding(binding)) { employee.Failure = "正式工作站或取餐 Slot 不可用 / Station unavailable"; return; }
            var prefab = employee.Role == EmployeeRole.Cashier ? cashierPrefab : baristaPrefab;
            if (prefab == null) { employee.Failure = "员工 Prefab 缺失 / Missing employee prefab"; return; }
            employee.SpawnAttempted = true;
            if (!adapter.IsPointWalkable(binding.WorkTarget.Position))
            { employee.Failure = "岗位不在当前 owned NavMesh 上 / Work anchor unavailable"; return; }

            var actor = Instantiate(prefab, binding.WorkTarget.Position,
                Quaternion.LookRotation(binding.WorkTarget.Facing.Value), transform);
            // Register is the final unchanged-body, solid and owned NavMesh test. Never project/teleport.
            if (!actor.TryInitializeRuntimeId(employee.Id) || !actor.TryEnableSteadyPathMotion() || !world.Register(actor))
            {
                world.Unregister(actor); actor.gameObject.SetActive(false); Destroy(actor.gameObject);
                employee.Failure = "岗位无法安全注册员工 / Unsafe employee spawn"; return;
            }
            employee.Actor = actor;
            employee.Visual = actor.GetComponent<EmployeeVisualPresenter>();
            if (employee.Visual != null) employee.Visual.Configure(actor, cupPrefab);
            StartRunner(employee);
            employee.Failure = "";
            employee.Runner.TryAssignBinding(binding);
            employee.Runner.TryReturnToWork(); // Even a safe initial pose needs an actual Arrived callback.
        }

        private NavigationTarget ToTarget(InteractionAnchor anchor)
        {
            var size = layout.Layout.GridSettings.CellSize;
            var point = gridRoot.TransformPoint(new Vector3((anchor.Position.X + .5f) * size, 0, (anchor.Position.Y + .5f) * size));
            var direction = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left }[(int)anchor.Facing];
            return new NavigationTarget(point, gridRoot.TransformDirection(direction));
        }

        private bool ValidBinding(EmployeeStationBinding binding)
        {
            if (binding == null || layout == null || layout.FunctionalSurfaceLayout == null ||
                adapter == null || binding.LayoutRevision != adapter.Revision) return false;
            var expected = binding.Role == EmployeeRole.Cashier ? LayoutStationType.CashRegister : LayoutStationType.CoffeeMachine;
            if (!layout.FunctionalSurfaceLayout.MountedInstances.Any(item => item.InstanceId == binding.WorkStationId) ||
                !layout.CurrentReadiness.Stations.Any(station => station.InstanceId == binding.WorkStationId && station.IsValid && station.FunctionType == expected))
                return false;
            if (binding.Role == EmployeeRole.Cashier) return customers != null && binding.WorkStationId == customers.SelectedRegisterId;
            return layout.FunctionalSurfaceLayout.PickUpPoints.Any(item => item.InstanceId == binding.PickUpId &&
                item.Address.Equals(binding.CupAddress.Value)) && ResolveConfirmedSlot(binding.CupAddress.Value) != null;
        }

        private Transform ResolveConfirmedSlot(SurfaceSlotAddress address)
        {
            // Scene objects can outlive domain removals. Check domain membership before the registry.
            if (disposed || layout == null || layout.Layout == null || layout.FunctionalSurfaceLayout == null ||
                !layout.Layout.TryGetFurnitureInstance(address.SupportFurnitureInstanceId, out var support) ||
                layout.SurfaceSlotCatalog == null || !layout.SurfaceSlotCatalog.TryGet(support.DefinitionId, address.SlotId, out _) ||
                !layout.FunctionalSurfaceLayout.PickUpPoints.Any(pickup => pickup.Address.Equals(address))) return null;
            var registry = decoration == null ? null : decoration.GetComponent<FurnitureSceneRegistry>();
            return FunctionalSurfaceViewPositioning.TryResolveSlot(address, registry, out _, out var slot) ? slot : null;
        }

        private void CleanupSession()
        {
            disposed = true; session = null; // Retire the session before external cancel/destroy callbacks.
            foreach (var employee in employees)
            {
                employee.Runner?.Dispose(); employee.Port?.Dispose();
                if (employee.Actor == null) continue;
                if (world != null) world.Unregister(employee.Actor);
                employee.Actor.gameObject.SetActive(false); Destroy(employee.Actor.gameObject);
            }
        }
        private void OnDisable()
        {
            disposed = true; session = null; // Retire callback ports before cancellation can reenter.
            foreach (var employee in employees) { employee.Runner?.Dispose(); employee.Port?.Dispose(); }
        }
        private void OnEnable()
        {
            if (!disposed) return;
            disposed = false; session = Guid.NewGuid().ToString("N");
            foreach (var employee in employees)
            {
                if (employee.Actor == null) continue; // Destroyed bodies stay retired; failed initial spawns may still retry.
                employee.Runner = null; employee.Port = null; employee.NeedsBinding = true;
            }
        }
        private void OnDestroy() => CleanupSession();

        /// <summary>One employee's small runtime port; no shared task framework.</summary>
        private sealed class Environment : IEmployeeTaskEnvironment, IDisposable
        {
            private readonly EmployeeFlowController owner;
            private readonly Employee employee;
            private readonly string session;
            private readonly HashSet<long> requests = new HashSet<long>();
            internal Environment(EmployeeFlowController owner, Employee employee, string session)
            { this.owner = owner; this.employee = employee; this.session = session; }
            private bool Live => owner != null && !owner.disposed && owner.session == session && ReferenceEquals(employee.Port, this) && employee.Actor != null;
            public bool IsProgressAllowed => Live && employee.Actor.isActiveAndEnabled && owner.ProgressAllowed;

            public MoveStartResult MoveTo(NavigationTarget target, Action<MovementResult> completed)
            {
                if (!IsProgressAllowed) return new MoveStartResult(false, 0, NavigationFailure.ActorUnavailable);
                bool finished = false;
                var result = owner.world.Service.MoveTo(employee.Id, target, movement =>
                {
                    finished = true; requests.Remove(movement.RequestId);
                    if (!Live) return;
                    // Revision advances before synchronous LayoutChanged callbacks and before Decor Done.
                    if (owner.adapter == null || employee.Binding.LayoutRevision != owner.adapter.Revision)
                    { owner.ObserveLayoutChanges(); return; }
                    if (!employee.Actor.isActiveAndEnabled)
                    { employee.Runner.Dispose(); employee.NeedsBinding = true; return; }
                    completed(movement);
                }, employee.Binding.WorkTarget);
                if (result.Accepted && !finished) requests.Add(result.RequestId);
                return result;
            }
            public bool CancelMove(long requestId)
            {
                if (!requests.Remove(requestId) || owner.world == null) return false;
                return owner.world.Service.Cancel(requestId);
            }
            public bool TryShowHeldCup(out string reason)
            {
                reason = "员工 Visual / Cup 配置不可用 / Missing employee visual or cup";
                return IsProgressAllowed && employee.Visual != null && owner.cupPrefab != null &&
                    employee.Visual.TryShowHeldCup(out reason);
            }
            public bool TryPlaceCup(SurfaceSlotAddress address, out string reason)
            {
                reason = "正式取餐 Slot / Cup 展示不可用 / Pickup display unavailable";
                if (!IsProgressAllowed || employee.Visual == null || owner.display == null ||
                    !owner.ValidBinding(employee.Binding) || !employee.Binding.CupAddress.Value.Equals(address)) return false;
                if (!owner.display.TryDeliver(address, out reason)) return false;
                // Only a committed display delivery transfers cup ownership away from the hand.
                employee.Visual.HideHeldCup(); return true;
            }
            public void SetVisualMode(EmployeeVisualMode mode)
            { if (Live && employee.Visual != null) employee.Visual.SetMode(mode); }
            public void CleanupTask(long handleId)
            { if (employee.Visual != null) employee.Visual.HideHeldCup(); }
            public void Dispose()
            {
                foreach (var request in requests.ToArray()) CancelMove(request);
                // Cancel stops the driver now; publish that stop before selecting Idle animation.
                if (employee.Actor != null) employee.Actor.SetMotion(0);
                if (employee.Visual != null) { employee.Visual.HideHeldCup(); employee.Visual.SetMode(EmployeeVisualMode.Idle); }
            }
        }
    }
}

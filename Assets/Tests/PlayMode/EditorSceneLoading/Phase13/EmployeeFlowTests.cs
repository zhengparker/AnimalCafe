#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using AnimalCafe.Tests.PlayMode.EditorSceneLoading;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode.Phase13
{
    /// <summary>真实 P12 formal layout / owned NavMesh; no substitute movement driver.</summary>
    public sealed class EmployeeFlowTests
    {
        private Scene scene;
        private NavigationWorld world;
        private NavigationLayoutAdapter adapter;
        private CafeLayoutRuntime layout;
        private DecorationModeController decoration;
        private GameTimeService time;
        private CustomerFlowController customers;
        private Transform grid;
        private EmployeeFlowController flow;
        private PickupCupDisplay display;
        private NavigationActor blocker;
        private float savedCapture, savedScale;
        private UnityEngine.InputSystem.InputActionAsset[] inputAssets;
        private const string CupPath = "Assets/Art/Phase4/Prefabs/PF_Item_CeramicCup_01.prefab";
        private const string MachineId = "12000000000000000000000000000005";
        private const string PickupId = "12000000000000000000000000000006";

        [UnitySetUp] public IEnumerator Setup()
        {
            savedCapture = Time.captureDeltaTime; savedScale = Time.timeScale;
            Time.captureDeltaTime = 1f / 60;
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            customers = Find<CustomerFlowController>(); customers.AutoSpawn = false;
            world = Find<NavigationWorld>(); adapter = Find<NavigationLayoutAdapter>();
            layout = Find<CafeLayoutRuntime>(); decoration = Find<DecorationModeController>(); time = Find<GameTimeService>();
            grid = Reference<Transform>(decoration, "gridRoot");
            inputAssets = Phase8SceneInputTestCleanup.CaptureAssets(scene);
            // Configure alone never selects P12's CR. Let its real Update establish that choice.
            for (var frame = 0; frame < 120 && string.IsNullOrEmpty(customers.SelectedRegisterId); frame++) yield return null;
            Assert.That(customers.SelectedRegisterId, Is.EqualTo("12000000000000000000000000000004"), customers.Status);
            Assert.That(layout.CurrentReadiness.CanOpenForBusiness, Is.True);
            Assert.That(adapter.CurrentReadiness.CanResume, Is.True, adapter.CurrentReadiness.Reason);
            Assert.That(adapter.PublishedRevision, Is.EqualTo(adapter.Revision));
            Assert.That(world.LayoutAvailable, Is.True); Assert.That(world.RegisteredActors, Is.Empty);
            Assert.That(time.TrySetSpeed(GameSpeed.Normal), Is.True, time.ResumeBlockReason);
            var host = new GameObject("Task5 employees"); SceneManager.MoveGameObjectToScene(host, scene);
            var displayHost = new GameObject("Task5 cup display"); displayHost.transform.SetParent(host.transform, false);
            display = displayHost.AddComponent<PickupCupDisplay>(); flow = host.AddComponent<EmployeeFlowController>();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (blocker != null) { world.Unregister(blocker); Object.Destroy(blocker.gameObject); }
            if (flow != null) Object.Destroy(flow.gameObject);
            yield return null;
            if (scene.IsValid() && scene.isLoaded)
            {
                var empty = SceneManager.CreateScene("Task5 cleanup"); SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets ?? Array.Empty<UnityEngine.InputSystem.InputActionAsset>());
            Time.captureDeltaTime = savedCapture; Time.timeScale = savedScale;
        }

        [UnityTest] public IEnumerator CreatesTwoUniqueEmployeesAtSafeWorkAnchors()
        {
            Configure(); flow.Step(0);
            Assert.That(flow.Snapshot.Count, Is.EqualTo(2), "Missing employee runtime generation");
            Assert.That(flow.Snapshot.Select(s => s.ActorId), Is.Unique);
            Assert.That(world.RegisteredActors.Count, Is.EqualTo(2));
            foreach (var snapshot in flow.Snapshot)
            {
                Assert.That(snapshot.State, Is.EqualTo(EmployeeState.MovingToWork), "Spawn is not proof of Arrived");
                var actor = Actor(snapshot.Role); AssertPose(actor, Work(snapshot.Role));
                Assert.That(adapter.IsPointWalkable(actor.transform.position), Is.True);
                Assert.That(actor.TryInitializeRuntimeId("illegal-renaming"), Is.False);
            }
            var positions = world.RegisteredActors.Select(a => a.transform.position).ToArray();
            Assert.That(Vector3.Distance(positions[0], positions[1]), Is.GreaterThan(.911f));
            yield return Ready();
            Assert.That(flow.TryStartTask(EmployeeRole.Cashier), Is.True);
            Assert.That(flow.TryStartTask(EmployeeRole.Cashier), Is.False);
            yield return Wait(() => State(EmployeeRole.Cashier).State == EmployeeState.Idle);
            Assert.That(State(EmployeeRole.Cashier).Outcome, Is.EqualTo(EmployeeTaskOutcome.Succeeded));
        }

        [UnityTest] public IEnumerator RealBaristaPlacesCupOnlyAfterPickupArrived()
        {
            Configure(); yield return Ready();
            Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.True);
            Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.False);
            time.SetPaused(); var remaining = State(EmployeeRole.Barista).RemainingWorkSeconds;
            flow.Step(100); yield return null;
            Assert.That(State(EmployeeRole.Barista).RemainingWorkSeconds, Is.EqualTo(remaining));
            Assert.That(flow.TryStartTask(EmployeeRole.Cashier), Is.False);
            Assert.That(display.VisibleCount(Address), Is.Zero);
            time.SetNormal(); yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.CarryingToPickUp);
            Assert.That(HeldCount, Is.EqualTo(1)); Assert.That(display.VisibleCount(Address), Is.Zero);
            var start = Actor(EmployeeRole.Barista).transform.position;
            yield return WaitForDelivery();
            Assert.That(Vector3.Distance(start, Actor(EmployeeRole.Barista).transform.position), Is.GreaterThan(1));
            AssertPose(Actor(EmployeeRole.Barista), Pickup);
            Assert.That(display.VisibleCount(Address), Is.EqualTo(1)); Assert.That(HeldCount, Is.Zero);
            Assert.That(State(EmployeeRole.Barista).State, Is.EqualTo(EmployeeState.Returning));
            Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.False, "Returning is busy");
            yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.Idle);
            AssertPose(Actor(EmployeeRole.Barista), Work(EmployeeRole.Barista));
            Assert.That(State(EmployeeRole.Barista).Delivered, Is.True);
        }

        [UnityTest] public IEnumerator FiveRealDeliveriesKeepThreeCupsAndReturnIdle()
        {
            Configure(); yield return Ready(); var deliveredTransitions = 0; var returnedCycles = 0;
            for (var cycle = 1; cycle <= 5; cycle++)
            {
                Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.True);
                Assert.That(State(EmployeeRole.Barista).Delivered, Is.False);
                yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.CarryingToPickUp);
                Assert.That(HeldCount, Is.EqualTo(1));
                yield return WaitForDelivery(); deliveredTransitions++;
                AssertPose(Actor(EmployeeRole.Barista), Pickup);
                Assert.That(display.VisibleCount(Address), Is.EqualTo(Math.Min(cycle, 3)));
                Assert.That(State(EmployeeRole.Barista).Outcome, Is.EqualTo(EmployeeTaskOutcome.Succeeded));
                Assert.That(State(EmployeeRole.Barista).State, Is.EqualTo(EmployeeState.Returning));
                yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.Idle);
                AssertPose(Actor(EmployeeRole.Barista), Work(EmployeeRole.Barista)); returnedCycles++;
                TestContext.WriteLine($"REAL_CYCLE {cycle}: delivered=true, returned=Idle, cups={display.VisibleCount(Address)}, workPose={Actor(EmployeeRole.Barista).transform.position}");
            }
            Assert.That(deliveredTransitions, Is.EqualTo(5)); Assert.That(returnedCycles, Is.EqualTo(5));
            Assert.That(Slot().GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("PickupCup_")), Is.EqualTo(3));
        }

        [UnityTest] public IEnumerator BlockedCarrierStopsWithoutLosingCup()
        {
            Configure(); yield return Ready();
            blocker = Object.Instantiate(Prefab("Shiba"), Pickup.Position, Quaternion.LookRotation(Pickup.Facing.Value));
            SceneManager.MoveGameObjectToScene(blocker.gameObject, scene);
            Assert.That(blocker.TryInitializeRuntimeId("task5-stationary-blocker"), Is.True);
            Assert.That(world.Register(blocker), Is.True);
            Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.True);
            yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.CarryingToPickUp);
            var stoppedFrames = 0; var consecutiveStopped = 0;
            var carrier = Actor(EmployeeRole.Barista); var last = carrier.transform.position;
            for (var frame = 0; frame < 1800 && State(EmployeeRole.Barista).HandleId != 0; frame++)
            {
                Assert.That(State(EmployeeRole.Barista).Delivered, Is.False);
                Assert.That(State(EmployeeRole.Barista).State, Is.EqualTo(EmployeeState.CarryingToPickUp));
                Assert.That(HeldCount, Is.EqualTo(1), "Keep hand ownership until bounded navigation terminates");
                Assert.That(display.VisibleCount(Address), Is.Zero);
                Assert.That(Vector3.Distance(carrier.transform.position, blocker.transform.position), Is.GreaterThanOrEqualTo(.909f));
                yield return null;
                if (State(EmployeeRole.Barista).HandleId != 0 && Vector3.Distance(last, carrier.transform.position) < .0001f)
                {
                    consecutiveStopped++;
                    if (consecutiveStopped >= 3)
                    {
                        stoppedFrames++;
                        Assert.That(carrier.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hold"), Is.True);
                        Assert.That(carrier.ModelAnimator.GetLayerWeight(1), Is.EqualTo(1));
                    }
                }
                else consecutiveStopped = 0;
                last = carrier.transform.position;
            }
            Assert.That(stoppedFrames, Is.GreaterThan(30), "Real blocked lower body must visibly stop");
            Assert.That(State(EmployeeRole.Barista).HandleId, Is.Zero, "P11 retry/recovery is bounded");
            Assert.That(State(EmployeeRole.Barista).Delivered, Is.False);
            Assert.That(State(EmployeeRole.Barista).Outcome, Is.EqualTo(EmployeeTaskOutcome.Failed));
            Assert.That(display.VisibleCount(Address), Is.Zero);
            yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.Idle || State(EmployeeRole.Barista).State == EmployeeState.Blocked);
        }

        [UnityTest] public IEnumerator MissingCupConfigurationCannotReportDeliverySuccess()
        {
            Configure(false); yield return Ready();
            Assert.That(flow.TryStartTask(EmployeeRole.Cashier), Is.True, "Cashier does not need cups");
            Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.True);
            yield return Wait(() => State(EmployeeRole.Barista).Outcome != EmployeeTaskOutcome.None);
            Assert.That(State(EmployeeRole.Barista).Outcome, Is.EqualTo(EmployeeTaskOutcome.Failed));
            Assert.That(State(EmployeeRole.Barista).Delivered, Is.False); Assert.That(State(EmployeeRole.Barista).Failure, Is.Not.Empty);
            Assert.That(display.VisibleCount(Address), Is.Zero); Assert.That(HeldCount, Is.Zero);
        }

        [UnityTest] public IEnumerator MissingVisualCannotReportDeliverySuccess()
        {
            Configure(); yield return Ready();
            Object.Destroy(Actor(EmployeeRole.Barista).GetComponent<EmployeeVisualPresenter>()); yield return null;
            Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.True);
            yield return Wait(() => State(EmployeeRole.Barista).Outcome != EmployeeTaskOutcome.None);
            Assert.That(State(EmployeeRole.Barista).Outcome, Is.EqualTo(EmployeeTaskOutcome.Failed));
            Assert.That(State(EmployeeRole.Barista).Delivered, Is.False); Assert.That(display.VisibleCount(Address), Is.Zero);
        }

        [UnityTest] public IEnumerator DisplayDestroyCleansOwnedCupsOutsideItsHost()
        {
            Configure(); yield return Ready(); Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.True);
            yield return WaitForDelivery(); var slot = Slot();
            Assert.That(slot.GetComponentsInChildren<Transform>().Any(t => t.name == "PickupCup_1"), Is.True);
            Object.Destroy(display.gameObject); yield return null; yield return null;
            Assert.That(slot.GetComponentsInChildren<Transform>().Any(t => t.name.StartsWith("PickupCup_")), Is.False);
        }

        [UnityTest] public IEnumerator RemovedFormalPickupCannotReceiveCupThroughStaleRepresentation()
        {
            Configure(); yield return Ready(); Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.True);
            yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.CarryingToPickUp);
            var slot = Slot(); Assert.That(layout.FunctionalSurfaceLayout.RemovePickUp(PickupId).Succeeded, Is.True);
            // Deliberately leave the actual registry and readiness snapshot unchanged.
            yield return Wait(() => State(EmployeeRole.Barista).Outcome != EmployeeTaskOutcome.None);
            Assert.That(State(EmployeeRole.Barista).Delivered, Is.False);
            Assert.That(State(EmployeeRole.Barista).Outcome, Is.EqualTo(EmployeeTaskOutcome.Failed));
            Assert.That(slot.GetComponentsInChildren<Transform>().Any(t => t.name.StartsWith("PickupCup_")), Is.False);
        }

        private void Configure(bool cup = true)
        {
            var serialized = new SerializedObject(flow);
            serialized.FindProperty("cupPrefab").objectReferenceValue = cup ? AssetDatabase.LoadAssetAtPath<GameObject>(CupPath) : null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            flow.Configure(world, adapter, layout, decoration, time, customers, grid, Prefab("Shiba"), Prefab("Westie"), display);
        }
        private T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
        private static T Reference<T>(Object owner, string field) where T : Object => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        private static NavigationActor Prefab(string species) => AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Art/Phase13/Employees/{species}/PF_{species}_Employee.prefab").GetComponent<NavigationActor>();
        private EmployeeTaskSnapshot State(EmployeeRole role) => flow.Snapshot.Single(s => s.Role == role);
        private NavigationActor Actor(EmployeeRole role) => world.RegisteredActors.Single(a => a.ActorId == State(role).ActorId);
        private int HeldCount => Actor(EmployeeRole.Barista).GetComponentsInChildren<Transform>().Count(t => t.name == "EmployeeHeldCup");
        private SurfaceSlotAddress Address => new SurfaceSlotAddress("12000000000000000000000000000003", "slot.0");
        private NavigationTarget Work(EmployeeRole role) => Target(role == EmployeeRole.Barista ? MachineId : customers.SelectedRegisterId);
        private NavigationTarget Pickup => Target(PickupId);
        private NavigationTarget Target(string id)
        {
            var station = layout.CurrentReadiness.Stations.Single(s => s.InstanceId == id);
            Assert.That(station.Anchors.TryGetAnchor(InteractionRole.Employee, out var anchor), Is.True);
            var local = new Vector3(anchor.Position.X + .5f, 0, anchor.Position.Y + .5f);
            var facing = new[] { Vector3.forward, Vector3.right, Vector3.back, Vector3.left }[(int)anchor.Facing];
            return new NavigationTarget(grid.TransformPoint(local), grid.TransformDirection(facing));
        }
        private Transform Slot()
        {
            var registry = decoration.GetComponent<FurnitureSceneRegistry>();
            Assert.That(registry.TryGet(Address.SupportFurnitureInstanceId, out var support), Is.True);
            return support.GetComponentsInChildren<SurfaceSlotMarker>().Single(m => m.SlotId == Address.SlotId).transform;
        }
        private IEnumerator Ready() => Wait(() => flow.Snapshot.Count == 2 && flow.Snapshot.All(s => s.State == EmployeeState.Idle));
        private IEnumerator WaitForDelivery() => Wait(() => State(EmployeeRole.Barista).Delivered);
        private IEnumerator Wait(Func<bool> completed)
        {
            for (var frame = 0; frame < 1800 && !completed(); frame++) yield return null;
            Assert.That(completed(), Is.True, "Timeout: " + string.Join(" | ", flow.Snapshot.Select(s => s.Role + " " + s.State + " " + s.Outcome + " " + s.Failure)));
        }
        private static void AssertPose(NavigationActor actor, NavigationTarget target)
        {
            Assert.That(Vector3.Distance(actor.transform.position, target.Position), Is.LessThanOrEqualTo(.08f));
            Assert.That(Vector3.Angle(actor.transform.forward, target.Facing.Value), Is.LessThanOrEqualTo(5));
        }
    }
}
#endif

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
    public sealed class EmployeeDecorationTests
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
            var host = new GameObject("Task6 employees"); SceneManager.MoveGameObjectToScene(host, scene);
            var displayHost = new GameObject("Task6 cup display"); displayHost.transform.SetParent(host.transform, false);
            display = displayHost.AddComponent<PickupCupDisplay>(); flow = host.AddComponent<EmployeeFlowController>();
        }

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (blocker != null) { world.Unregister(blocker); Object.Destroy(blocker.gameObject); }
            if (flow != null) Object.Destroy(flow.gameObject);
            yield return null;
            if (scene.IsValid() && scene.isLoaded)
            {
                var empty = SceneManager.CreateScene("Task6 cleanup"); SceneManager.SetActiveScene(empty);
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets ?? Array.Empty<UnityEngine.InputSystem.InputActionAsset>());
            Time.captureDeltaTime = savedCapture; Time.timeScale = savedScale;
        }

        [UnityTest] public IEnumerator UnchangedDecorationResumesSameTask()
        {
            Configure(); yield return Ready(); Assert.That(flow.TryStartTask(EmployeeRole.Barista),Is.True);
            yield return WaitForDelivery(); yield return Ready(); // One independently owned tabletop cup.
            Assert.That(display.VisibleCount(Address),Is.EqualTo(1));
            var tableCup=Slot().Cast<Transform>().Single(t=>t.name=="PickupCup_1");
            Assert.That(flow.TryGetBinding(EmployeeRole.Barista,out var binding),Is.True);
            var formal=layout.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.InstanceId==MachineId);
            Assert.That(flow.TryStartTask(EmployeeRole.Barista),Is.True); yield return null;
            decoration.EnterDecorationMode();
            var before=State(EmployeeRole.Barista); var revision=adapter.Revision;
            var completed=decoration.CompletedLayoutChangeVersion;
            Assert.That(decoration.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,MachineId),Is.True);
            Assert.That(decoration.TryRotateFunctionalSurfacePreview(),Is.True);
            Assert.That(decoration.ActiveFunctionalSurfacePreview.CanConfirm,Is.True,"Use an actual legal changed Preview");
            Assert.That(decoration.ActiveFunctionalSurfacePreview.Rotation,Is.Not.EqualTo(formal.Rotation));
            decoration.CancelFunctionalSurfacePreview();
            Assert.That(decoration.ActiveFunctionalSurfacePreview,Is.Null);
            for(var i=0;i<4;i++) yield return null;
            var after=layout.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.InstanceId==MachineId);
            Assert.That(after.Address,Is.EqualTo(formal.Address)); Assert.That(after.Rotation,Is.EqualTo(formal.Rotation));
            Assert.That(adapter.Revision,Is.EqualTo(revision)); Assert.That(decoration.CompletedLayoutChangeVersion,Is.EqualTo(completed));
            Assert.That(flow.TryGetBinding(EmployeeRole.Barista,out var afterBinding),Is.True);
            Assert.That(afterBinding,Is.SameAs(binding));
            Assert.That(State(EmployeeRole.Barista).HandleId,Is.EqualTo(before.HandleId));
            Assert.That(State(EmployeeRole.Barista).RemainingWorkSeconds,Is.EqualTo(before.RemainingWorkSeconds));
            Assert.That(display.VisibleCount(Address),Is.EqualTo(1)); Assert.That(tableCup != null,Is.True);
            Assert.That(tableCup.parent,Is.SameAs(Slot()),"Cancelled preview keeps the existing table cup and support");
            Assert.That(decoration.TryRequestExit(),Is.True); flow.Step(0);
            Assert.That(State(EmployeeRole.Barista).HandleId,Is.EqualTo(before.HandleId));
            Assert.That(State(EmployeeRole.Barista).RemainingWorkSeconds,Is.EqualTo(before.RemainingWorkSeconds));
            yield return WaitForDelivery(); yield return Ready();
            Assert.That(display.VisibleCount(Address),Is.EqualTo(2)); AssertPose(Actor(EmployeeRole.Barista),Work(EmployeeRole.Barista));
            TestContext.WriteLine("QA02_REAL_PREVIEW_CANCEL formal placement/revision/binding/handle/work remaining/table cup preserved; same task delivered and actually returned");
        }

        [UnityTest] public IEnumerator UnchangedDecorationPreservesCarrierAndRequest()
        {
            Configure(); yield return Ready(); flow.TryStartTask(EmployeeRole.Barista);
            yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.CarryingToPickUp);
            var before = State(EmployeeRole.Barista); var actor = Actor(EmployeeRole.Barista);
            decoration.EnterDecorationMode(); var pose=actor.transform.position;
            for (var i=0;i<5;i++) yield return null;
            Assert.That(actor.transform.position, Is.EqualTo(pose)); Assert.That(HeldCount, Is.EqualTo(1));
            Assert.That(State(EmployeeRole.Barista).HandleId, Is.EqualTo(before.HandleId));
            Assert.That(decoration.TryRequestExit(), Is.True); flow.Step(0);
            Assert.That(State(EmployeeRole.Barista).HandleId, Is.EqualTo(before.HandleId));
            yield return WaitForDelivery();
        }

        [UnityTest] public IEnumerator ConfirmedEditRetiresOldTaskAndRepeatedConfirmKeepsEmployeeIdentity()
        {
            Configure(); yield return Ready(); flow.TryStartTask(EmployeeRole.Barista);
            yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.CarryingToPickUp);
            yield return null; yield return null;
            var actors=world.RegisteredActors.ToArray(); var ids=flow.Snapshot.Select(x=>x.ActorId).ToArray();
            var barista=Actor(EmployeeRole.Barista); var pose=barista.transform.position;
            decoration.EnterDecorationMode(); var version=decoration.CompletedLayoutChangeVersion;
            RotateMachine(); flow.Step(0);
            Assert.That(decoration.CompletedLayoutChangeVersion, Is.EqualTo(version));
            Assert.That(State(EmployeeRole.Barista).HandleId, Is.Zero);
            Assert.That(State(EmployeeRole.Barista).Outcome, Is.EqualTo(EmployeeTaskOutcome.Cancelled));
            Assert.That(HeldCount, Is.Zero); Assert.That(display.VisibleCount(Address), Is.Zero);
            RotateMachine(); flow.Step(0);
            CollectionAssert.AreEqual(ids, flow.Snapshot.Select(x=>x.ActorId).ToArray());
            CollectionAssert.AreEquivalent(actors,world.RegisteredActors);
            Assert.That(barista.transform.position, Is.EqualTo(pose));
            Assert.That(decoration.TryRequestExit(), Is.True); flow.Step(0);
            Assert.That(barista.transform.position, Is.EqualTo(pose), "Rebind must not teleport");
            yield return Ready(); AssertPose(barista,Work(EmployeeRole.Barista));
            CollectionAssert.AreEqual(ids, flow.Snapshot.Select(x=>x.ActorId).ToArray());
            TestContext.WriteLine("DECOR_REAL_RETURN same bodies and IDs; actual final="+barista.transform.position);
        }

        [UnityTest] public IEnumerator FlowDisableCancelsOwnedTaskAndReenableReturnsSameBody()
        {
            Configure(); yield return Ready(); flow.TryStartTask(EmployeeRole.Barista);
            yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.CarryingToPickUp);
            yield return null; var actor=Actor(EmployeeRole.Barista); var id=actor.ActorId; var pose=actor.transform.position;
            flow.enabled=false;
            Assert.That(actor.GetComponent<NavigationWalkPresenter>().ActualSpeed,Is.Zero,"Disable must publish stop before the next World update");
            yield return null;
            Assert.That(State(EmployeeRole.Barista).State, Is.EqualTo(EmployeeState.Disabled));
            Assert.That(State(EmployeeRole.Barista).HandleId, Is.Zero); Assert.That(HeldCount, Is.Zero);
            Assert.That(world.RegisteredActors.Contains(actor), Is.True);
            Assert.That(actor.transform.position, Is.EqualTo(pose));
            Assert.That(actor.GetComponent<NavigationWalkPresenter>().ActualSpeed,Is.Zero,"Cancelled body must publish actual stopped motion");
            Assert.That(actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hold"),Is.True);
            Assert.That(actor.ModelAnimator.GetLayerWeight(1),Is.Zero);
            flow.enabled=true; flow.RetryBlockedEmployees(); flow.Step(0);
            Assert.That(actor.transform.position, Is.EqualTo(pose));
            yield return Ready(); Assert.That(Actor(EmployeeRole.Barista),Is.SameAs(actor));
            Assert.That(actor.ActorId,Is.EqualTo(id)); AssertPose(actor,Work(EmployeeRole.Barista));
            Assert.That(State(EmployeeRole.Barista).Delivered, Is.False);
        }

        [UnityTest] public IEnumerator DisabledBodyCannotBeBypassed()
        {
            Configure(); yield return Ready(); var actor=Actor(EmployeeRole.Barista);
            flow.TryStartTask(EmployeeRole.Barista); actor.enabled=false; flow.Step(0);
            Assert.That(State(EmployeeRole.Barista).HandleId,Is.Zero);
            Assert.That(State(EmployeeRole.Barista).State,Is.EqualTo(EmployeeState.Disabled));
            Assert.That(flow.TryStartTask(EmployeeRole.Barista),Is.False);
            Assert.That(world.RegisteredActors.Contains(actor),Is.True);
            var overlapping=Object.Instantiate(Prefab("Shiba"),actor.transform.position,actor.transform.rotation);
            Assert.That(overlapping.TryInitializeRuntimeId("task6-disabled-body-overlap"),Is.True);
            Assert.That(world.Register(overlapping),Is.False,"Disabled body still reserves its physical space");
            Object.Destroy(overlapping.gameObject);
            actor.enabled=true; flow.Step(0); yield return Ready();
            Assert.That(Actor(EmployeeRole.Barista),Is.SameAs(actor));
        }

        [UnityTest] public IEnumerator SameRevisionNavigationSuspensionResumesSameHandle()
        {
            Configure(); yield return Ready(); flow.TryStartTask(EmployeeRole.Barista);
            yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.CarryingToPickUp);
            var handle=State(EmployeeRole.Barista).HandleId; var revision=adapter.Revision;
            adapter.ReleaseOwnedNavigation(); flow.Step(0);
            Assert.That(State(EmployeeRole.Barista).HandleId,Is.EqualTo(handle));
            Assert.That(State(EmployeeRole.Barista).Outcome,Is.EqualTo(EmployeeTaskOutcome.None));
            Assert.That(HeldCount,Is.EqualTo(1));
            Assert.That(adapter.RebuildAndValidate().CanResume,Is.True); time.SetNormal();
            Assert.That(adapter.Revision,Is.EqualTo(revision));
            yield return WaitForDelivery();
        }

        [UnityTest] public IEnumerator DisableDoesNotRevokeDeliveredTableCup()
        {
            Configure(); yield return Ready(); flow.TryStartTask(EmployeeRole.Barista); yield return WaitForDelivery();
            flow.enabled=false; yield return null;
            Assert.That(display.VisibleCount(Address),Is.EqualTo(1));
            Assert.That(State(EmployeeRole.Barista).HandleId,Is.Zero);
        }

        [UnityTest] public IEnumerator RemovedPickupRejectsStaleSceneObjectAndRefreshesOwnedCup()
        {
            Configure(); yield return Ready(); flow.TryStartTask(EmployeeRole.Barista); yield return WaitForDelivery();
            yield return Ready(); var slot=Slot();
            Assert.That(layout.FunctionalSurfaceLayout.RemovePickUp(PickupId).Succeeded,Is.True);
            // Scene representation deliberately survives the formal domain removal.
            decoration.RefreshConfirmedNavigationPresentation(); flow.Step(0); yield return null;
            Assert.That(slot != null,Is.True);
            Assert.That(display.VisibleCount(Address),Is.Zero);
            Assert.That(flow.TryStartTask(EmployeeRole.Barista),Is.False);
        }

        [UnityTest] public IEnumerator CashierWaitsForP12NewRegisterWhenEmployeeStepsFirst()
        {
            Configure(); yield return Ready(); var cashier=Actor(EmployeeRole.Cashier); var id=cashier.ActorId;
            customers.enabled=false;
            decoration.EnterDecorationMode();
            var support=layout.Layout.FurnitureInstances.First();
            const string supportId="13000000000000000000000000000001";
            const string registerId="01000000000000000000000000000001";
            Assert.That(layout.Layout.PlaceFurniture(FurnitureInstance.Restore(supportId,support.DefinitionId,
                new GridPosition(4,6),FurnitureRotation.Degrees0)).Succeeded,Is.True);
            Assert.That(layout.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance(registerId,
                "equipment.cash-register.01",new SurfaceSlotAddress(supportId,"slot.0"),FurnitureRotation.Degrees0)).Succeeded,Is.True);
            decoration.RefreshConfirmedNavigationPresentation();
            Assert.That(decoration.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,registerId),Is.True);
            Assert.That(decoration.TryRotateFunctionalSurfacePreview(),Is.True);
            Assert.That(decoration.TryConfirmFunctionalSurfacePreview(),Is.True);
            Assert.That(decoration.TryRequestExit(),Is.True);
            flow.Step(0); // P12 has not observed CompletedLayoutChangeVersion yet.
            Assert.That(customers.SelectedRegisterId,Is.EqualTo("12000000000000000000000000000004"));
            customers.Step(0); Assert.That(customers.SelectedRegisterId,Is.EqualTo(registerId));
            flow.Step(0); yield return Ready();
            Assert.That(Actor(EmployeeRole.Cashier),Is.SameAs(cashier)); Assert.That(cashier.ActorId,Is.EqualTo(id));
            AssertPose(cashier,Target(registerId)); Assert.That(flow.TryStartTask(EmployeeRole.Cashier),Is.True);
        }

        [UnityTest] public IEnumerator MissingStationIsReadableAndRepairReturnsActualBody()
        {
            Configure(); yield return Ready(); flow.TryStartTask(EmployeeRole.Barista);
            yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.CarryingToPickUp);
            yield return null; var actor=Actor(EmployeeRole.Barista); var pose=actor.transform.position;
            var machine=layout.FunctionalSurfaceLayout.MountedInstances.Single(m=>m.InstanceId==MachineId);
            decoration.EnterDecorationMode();
            Assert.That(decoration.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,MachineId),Is.True);
            Assert.That(decoration.TryStoreFunctionalSurfacePreview(),Is.True);
            flow.Step(0);
            Assert.That(State(EmployeeRole.Barista).State,Is.EqualTo(EmployeeState.Blocked));
            Assert.That(State(EmployeeRole.Barista).Failure,Does.Contain("Station"));
            Assert.That(decoration.TryRequestExit(),Is.True); flow.Step(0);
            Assert.That(actor.transform.position,Is.EqualTo(pose)); Assert.That(flow.TryStartTask(EmployeeRole.Barista),Is.False);
            Assert.That(layout.FunctionalSurfaceLayout.PlaceMounted(machine).Succeeded,Is.True);
            decoration.RefreshConfirmedNavigationPresentation();
            Assert.That(Find<NavigationDecorationBridge>().ResumeAfterRepair(),Is.True); time.SetNormal();
            flow.RetryBlockedEmployees(); flow.Step(0);
            Assert.That(actor.transform.position,Is.EqualTo(pose));
            yield return Ready(); Assert.That(Actor(EmployeeRole.Barista),Is.SameAs(actor)); AssertPose(actor,Work(EmployeeRole.Barista));
        }

        [UnityTest] public IEnumerator DestroyedActorRetiresOnlyItsOwnedTask()
        {
            Configure(); yield return Ready(); flow.TryStartTask(EmployeeRole.Barista);
            Assert.That(State(EmployeeRole.Barista).State,Is.EqualTo(EmployeeState.Working));
            var cashier=Actor(EmployeeRole.Cashier); Object.Destroy(Actor(EmployeeRole.Barista).gameObject);
            yield return null; flow.Step(0);
            Assert.That(State(EmployeeRole.Barista).State,Is.EqualTo(EmployeeState.Disabled));
            Assert.That(State(EmployeeRole.Barista).HandleId,Is.Zero);
            Assert.That(world.RegisteredActors.Count,Is.EqualTo(1)); Assert.That(world.RegisteredActors.Single(),Is.SameAs(cashier));
            Assert.That(flow.TryStartTask(EmployeeRole.Cashier),Is.True);
            Assert.That(display.VisibleCount(Address),Is.Zero);
        }

        private void RotateMachine()
        {
            Assert.That(decoration.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,MachineId),Is.True);
            Assert.That(decoration.TryRotateFunctionalSurfacePreview(),Is.True);
            Assert.That(decoration.TryConfirmFunctionalSurfacePreview(),Is.True);
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

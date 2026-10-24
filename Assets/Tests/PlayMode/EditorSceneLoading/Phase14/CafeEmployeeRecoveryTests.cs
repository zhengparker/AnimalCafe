#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.CafeLoop;
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

namespace AnimalCafe.Tests.PlayMode.Phase14
{
    public sealed class CafeEmployeeRecoveryTests
    {
        private Scene scene;
        private NavigationWorld world;
        private NavigationLayoutAdapter adapter;
        private CafeLayoutRuntime layout;
        private DecorationModeController decoration;
        private GameTimeService time;
        private CustomerFlowController customers;
        private EmployeeFlowController flow;
        private readonly List<CafeTaskEvent> events = new List<CafeTaskEvent>();
        private UnityEngine.InputSystem.InputActionAsset[] inputAssets;
        private float capture, scale;
        private int commits;
        private bool disableOnCommit;
        private CafeTaskEvent lastArrival;
        private static CafeTaskToken Token(int attempt = 1) => new CafeTaskToken("real-loop", 1, 1, attempt);
        private static CafeWorkCheckpoint Work(EmployeeRole role = EmployeeRole.Barista, float seconds = 3,
            CafeWorkStage stage = CafeWorkStage.Working) => new CafeWorkCheckpoint(role, "real-visit",
                role == EmployeeRole.Barista ? new CafeOrderKey("real-loop", 1, 1) : (CafeOrderKey?)null, stage, seconds);

        [UnitySetUp] public IEnumerator Setup()
        {
            capture = Time.captureDeltaTime; scale = Time.timeScale; Time.captureDeltaTime = 1f / 60;
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/Validation/Phase12CustomerQueue.unity",
                new LoadSceneParameters(LoadSceneMode.Single)); yield return null;
            customers = Find<CustomerFlowController>(); customers.AutoSpawn = false;
            world = Find<NavigationWorld>(); adapter = Find<NavigationLayoutAdapter>(); layout = Find<CafeLayoutRuntime>();
            decoration = Find<DecorationModeController>(); time = Find<GameTimeService>();
            inputAssets = Phase8SceneInputTestCleanup.CaptureAssets(scene);
            for (var f = 0; f < 120 && string.IsNullOrEmpty(customers.SelectedRegisterId); f++) yield return null;
            Assert.That(time.TrySetSpeed(GameSpeed.Normal), Is.True, time.ResumeBlockReason);
            var host = new GameObject("P14 employee recovery fixture"); SceneManager.MoveGameObjectToScene(host, scene);
            var display = host.AddComponent<PickupCupDisplay>(); flow = host.AddComponent<EmployeeFlowController>();
            var serial = new SerializedObject(flow);
            serial.FindProperty("cupPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Art/Phase4/Prefabs/PF_Item_CeramicCup_01.prefab"); serial.ApplyModifiedPropertiesWithoutUndo();
            var grid = (Transform)new SerializedObject(decoration).FindProperty("gridRoot").objectReferenceValue;
            flow.Configure(world, adapter, layout, decoration, time, customers, grid, Prefab("Shiba"), Prefab("Westie"), display);
            flow.SetIntegratedMode(true); flow.SetDeliveryGate(_ => true); flow.SetDeliveryCommit(Commit);
            flow.IntegratedTaskEvent += events.Add;
            yield return Wait(() => flow.Snapshot.All(e => e.State == EmployeeState.Idle));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (flow != null) Object.Destroy(flow.gameObject); yield return null;
            if (scene.IsValid() && scene.isLoaded)
            { var empty = SceneManager.CreateScene("P14 employee cleanup"); SceneManager.SetActiveScene(empty); yield return SceneManager.UnloadSceneAsync(scene); }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets ?? Array.Empty<UnityEngine.InputSystem.InputActionAsset>());
            Time.captureDeltaTime = capture; Time.timeScale = scale; events.Clear(); commits = 0; disableOnCommit = false;
        }
        [UnityTest] public IEnumerator WorkingDisableCapturesRemainingAndPreservesSingleDriver()
        {
            Assert.That(flow.TryStartOrderTask(Token(), Work(), out var reason), Is.True, reason);
            flow.Step(.75f); var actor = Actor(); var pose = actor.transform.position;
            Assert.That(flow.TryCaptureOrderCheckpoint(EmployeeRole.Barista, out var saved), Is.True);
            Assert.That(saved.RemainingWorkSeconds, Is.EqualTo(2.25f));
            yield return null; yield return null;
            Assert.That(flow.TryCaptureOrderCheckpoint(EmployeeRole.Barista, out saved), Is.True);
            Assert.That(saved.RemainingWorkSeconds, Is.EqualTo(2.25f), "Legacy Update must not tick P14");
            flow.enabled = false; yield return null;
            Assert.That(flow.TryCaptureOrderCheckpoint(EmployeeRole.Barista, out saved), Is.True);
            Assert.That(saved.RemainingWorkSeconds, Is.EqualTo(2.25f)); Assert.That(world.RegisteredActors.Contains(actor), Is.True);
            flow.enabled = true; Assert.That(flow.TryStartOrderTask(Token(2), saved, out reason), Is.True, reason);
            Assert.That(actor.transform.position, Is.EqualTo(pose)); flow.Step(2);
            Assert.That(State().State, Is.EqualTo(EmployeeState.Working)); flow.Step(.25f);
            Assert.That(State().State, Is.EqualTo(EmployeeState.CarryingToPickUp));
        }
        [UnityTest] public IEnumerator PreparedDisableRebuildsHandCupAndContinuesFromActualPose()
        {
            Assert.That(flow.TryStartOrderTask(Token(), Work(), out var reason), Is.True, reason); flow.Step(3);
            yield return Wait(() => Vector3.Distance(Actor().transform.position, WorkPosition()) > .3f);
            var actor = Actor(); var pose = actor.transform.position; var id = actor.ActorId; var oldHandle = State().HandleId;
            Assert.That(Held, Is.EqualTo(1)); flow.enabled = false; yield return null;
            Assert.That(Held, Is.Zero); Assert.That(flow.TryCaptureOrderCheckpoint(EmployeeRole.Barista, out var saved), Is.True);
            Assert.That(saved.Stage, Is.EqualTo(CafeWorkStage.Prepared)); flow.enabled = true;
            Assert.That(flow.TryStartOrderTask(Token(2), saved, out reason), Is.True, reason);
            Assert.That(actor.transform.position, Is.EqualTo(pose)); Assert.That(actor.ActorId, Is.EqualTo(id));
            Assert.That(State().State, Is.EqualTo(EmployeeState.CarryingToPickUp)); Assert.That(Held, Is.EqualTo(1));
            Assert.That(State().HandleId, Is.GreaterThan(oldHandle));
            yield return Wait(() => commits == 1); yield return Wait(() => State().State == EmployeeState.Idle);
            Assert.That(events.Count(e => e.Kind == CafeTaskEventKind.Prepared), Is.EqualTo(1));
            Assert.That(commits, Is.EqualTo(1)); Assert.That(Held, Is.Zero);
        }
        [UnityTest] public IEnumerator CommittedDeliveryDisableRestoresReturnWithoutSecondDelivery()
        {
            disableOnCommit = true;
            Assert.That(flow.TryStartOrderTask(Token(), Work(), out var reason), Is.True, reason); flow.Step(3);
            yield return Wait(() => commits == 1);
            Assert.That(flow.enabled, Is.False); Assert.That(State().State, Is.EqualTo(EmployeeState.Disabled));
            var actor = Actor(); var pose = actor.transform.position;
            Assert.That(flow.IsCurrentDeliveryArrival(lastArrival, Address), Is.False);
            flow.enabled = true; disableOnCommit = false;
            Assert.That(flow.TryStartOrderTask(Token(2), Work(seconds: 0, stage: CafeWorkStage.Delivered), out reason), Is.True, reason);
            Assert.That(actor.transform.position, Is.EqualTo(pose)); Assert.That(Held, Is.Zero);
            yield return Wait(() => State().State == EmployeeState.Idle); Assert.That(commits, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator ActorDisableRetainsCheckpointAndBodyUntilExplicitNewAttempt()
        {
            Assert.That(flow.TryStartOrderTask(Token(), Work(), out var reason), Is.True, reason); flow.Step(3);
            var actor = Actor(); var pose = actor.transform.position; actor.enabled = false; flow.Step(0); yield return null;
            Assert.That(flow.TryCaptureOrderCheckpoint(EmployeeRole.Barista, out var saved), Is.True);
            Assert.That(saved.Stage, Is.EqualTo(CafeWorkStage.Prepared)); Assert.That(world.RegisteredActors.Contains(actor), Is.True);
            actor.enabled = true; flow.Step(.1f);
            Assert.That(actor.transform.position, Is.EqualTo(pose)); Assert.That(commits, Is.Zero);
            Assert.That(flow.TryStartOrderTask(Token(2), saved, out reason), Is.True, reason);
            yield return Wait(() => commits == 1); Assert.That(Actor(), Is.SameAs(actor));
        }
        [UnityTest] public IEnumerator IntegratedLegacyApisCannotCancelOrStartWork()
        {
            Assert.That(flow.TryStartTask(EmployeeRole.Cashier), Is.False);
            Assert.That(flow.TryStartOrderTask(Token(), Work(), out var reason), Is.True, reason);
            var handle = State().HandleId; Assert.That(flow.TryCancelTask(EmployeeRole.Barista), Is.False);
            flow.RetryBlockedEmployees(); Assert.That(State().HandleId, Is.EqualTo(handle));
            flow.HoldIntegratedTasks(); Assert.That(flow.TryCaptureOrderCheckpoint(EmployeeRole.Barista, out var saved), Is.True);
            yield return null; flow.Step(.1f); Assert.That(State().State, Is.EqualTo(EmployeeState.Blocked));
            Assert.That(flow.TryStartOrderTask(Token(), saved, out reason), Is.False);
        }
        [UnityTest] public IEnumerator IntegratedModeIsPersistedBySceneSerialization()
        {
            var serializedMode = new SerializedObject(flow).FindProperty("integratedMode");
            Assert.That(serializedMode, Is.Not.Null, "Scene serialization must retain the sole P14 driver mode");
            Assert.That(serializedMode.boolValue, Is.True); yield return null;
        }
        private bool Commit(CafeTaskEvent arrival, SurfaceSlotAddress address, out string reason)
        {
            Assert.That(flow.IsCurrentDeliveryArrival(arrival, address), Is.True);
            Assert.That(flow.IsCurrentDeliveryArrival(new CafeTaskEvent(arrival.Token, arrival.Role, arrival.ActorId,
                arrival.HandleId, arrival.RequestId + 1, arrival.Generation, arrival.LayoutRevision, arrival.Kind, arrival.Checkpoint), address), Is.False);
            Assert.That(Vector3.Distance(Actor().transform.position, PickupPosition()), Is.LessThanOrEqualTo(.08f));
            Assert.That(events.Last().Kind, Is.EqualTo(CafeTaskEventKind.DeliveryArrived));
            lastArrival = arrival; commits++; reason = ""; if (disableOnCommit) flow.enabled = false; return true;
        }
        private T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();
        private static NavigationActor Prefab(string species) => AssetDatabase.LoadAssetAtPath<GameObject>(
            $"Assets/Art/Phase13/Employees/{species}/PF_{species}_Employee.prefab").GetComponent<NavigationActor>();
        private EmployeeTaskSnapshot State() => flow.Snapshot.Single(e => e.Role == EmployeeRole.Barista);
        private NavigationActor Actor() => world.RegisteredActors.Single(a => a.ActorId == State().ActorId);
        private int Held => Actor().GetComponentsInChildren<Transform>().Count(t => t.name == "EmployeeHeldCup");
        private SurfaceSlotAddress Address => new SurfaceSlotAddress("12000000000000000000000000000003", "slot.0");
        private Vector3 WorkPosition() { Assert.That(flow.TryGetBinding(EmployeeRole.Barista, out var b), Is.True); return b.WorkTarget.Position; }
        private Vector3 PickupPosition() { Assert.That(flow.TryGetBinding(EmployeeRole.Barista, out var b), Is.True); return b.PickUpTarget.Value.Position; }
        private IEnumerator Wait(Func<bool> condition)
        {
            for (var f = 0; f < 2400 && !condition(); f++) { if (flow != null && flow.enabled) flow.Step(Time.deltaTime); yield return null; }
            Assert.That(condition(), Is.True, string.Join(" | ", flow.Snapshot.Select(e => e.Role + " " + e.State + " " + e.Failure)));
        }
    }
}
#endif

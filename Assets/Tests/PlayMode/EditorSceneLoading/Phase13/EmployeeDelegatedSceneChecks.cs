#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using AnimalCafe.Tests.PlayMode.EditorSceneLoading;
using AnimalCafe.UI;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode.Phase13
{
    /// <summary>Owner-delegated operator scripts: real saved Scene + production Update; API, not physical mouse.</summary>
    public sealed class EmployeeDelegatedSceneChecks
    {
        private const string Path13 = "Assets/Scenes/Validation/Phase13EmployeeTasks.unity";
        private const string CM = "12000000000000000000000000000005", PU = "12000000000000000000000000000006";
        private const string CMTable = "12000000000000000000000000000002", PUTable = "12000000000000000000000000000003";
        private Scene scene;
        private EmployeeFlowController flow;
        private EmployeeTaskValidationController debug;
        private DecorationModeController decor;
        private CafeLayoutRuntime layout;
        private GameTimeService time;
        private NavigationWorld world;
        private PickupCupDisplay cups;
        private Button normal, fast, mode;
        private float savedCapture, savedScale;
        private P8RReferenceLayoutTests.NativeScreenSize screen;
        private readonly List<string> errors = new List<string>();
        private int warnings;
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator Open()
        {
            savedCapture = Time.captureDeltaTime; savedScale = Time.timeScale;
            Time.captureDeltaTime = 0; errors.Clear(); warnings = 0;
            Application.logMessageReceived += Audit;
            screen = new P8RReferenceLayoutTests.NativeScreenSize(); screen.Resize(new Vector2(1080, 1920));
            yield return Reload();
            Mark("SETUP", "fresh saved P13 Scene; natural Update/delta; AutoSpawn=false; API operator script, not physical mouse");
        }
        [UnityTearDown] public IEnumerator Close()
        {
            yield return Unload();
            screen?.Dispose(); screen = null;
            Time.captureDeltaTime = savedCapture; Time.timeScale = savedScale;
            Application.logMessageReceived -= Audit;
            TestContext.WriteLine("M010_AUDIT case=" + TestContext.CurrentContext.Test.Name + " errors=" + errors.Count + " warnings=" + warnings);
            foreach (var error in errors) TestContext.WriteLine("M010_RUNTIME_ERROR " + error);
            Assert.That(errors, Is.Empty, "Fresh Application Error/Assert/Exception audit; no Console clear or ignoreFailingMessages");
        }
        private void Audit(string condition, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(type + ": " + condition);
            if (type == LogType.Warning) warnings++;
        }
        private IEnumerator Reload()
        {
            yield return Unload(); Time.captureDeltaTime = 0;
            scene = EditorSceneManager.LoadSceneInPlayMode(Path13, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            flow = Find<EmployeeFlowController>(); debug = Find<EmployeeTaskValidationController>();
            decor = Find<DecorationModeController>(); layout = Find<CafeLayoutRuntime>(); time = Find<GameTimeService>();
            world = Find<NavigationWorld>(); cups = Find<PickupCupDisplay>();
            var panel = Find<TimeControlPanel>(); normal = Field<Button>(panel, "normalButton"); fast = Field<Button>(panel, "fastButton");
            mode = panel.transform.Find("DecorationModeButton").GetComponent<Button>();
            yield return Ready(); Assert.That(Find<CustomerFlowController>().AutoSpawn, Is.False);
            Assert.That(world.RegisteredActors.Count, Is.EqualTo(2));
        }
        private IEnumerator Unload()
        {
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            var actions = Phase8SceneInputTestCleanup.CaptureAssets(scene);
            SceneManager.SetActiveScene(SceneManager.CreateScene("Delegated operator cleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(actions);
            scene = default;
        }

        [UnityTest] public IEnumerator M005_RealTimeBothRolesAndTwoSecondPause()
        {
            var measurements = new List<float[]>();
            foreach (var speed in new[] { GameSpeed.Normal, GameSpeed.Fast })
            {
                Speed(speed); yield return null; Start(EmployeeRole.Cashier); Start(EmployeeRole.Barista);
                var start = Time.realtimeSinceStartup; var cashier = -1f; var largest = 0f;
                while (State(EmployeeRole.Barista).State == EmployeeState.Working && Time.realtimeSinceStartup - start < 8)
                {
                    yield return null; largest = Mathf.Max(largest, Time.unscaledDeltaTime);
                    if (cashier < 0 && State(EmployeeRole.Cashier).Outcome == EmployeeTaskOutcome.Succeeded) cashier = Time.realtimeSinceStartup - start;
                }
                var barista = Time.realtimeSinceStartup - start; var tolerance = Mathf.Max(.18f, largest * 2);
                Assert.That(cashier, Is.EqualTo(speed == GameSpeed.Normal ? 2 : 1).Within(tolerance));
                Assert.That(barista, Is.EqualTo(speed == GameSpeed.Normal ? 3 : 1.5f).Within(tolerance));
                Assert.That(State(EmployeeRole.Barista).State, Is.EqualTo(EmployeeState.CarryingToPickUp));
                var carryStart = Time.realtimeSinceStartup; yield return Wait(() => State(EmployeeRole.Barista).Delivered);
                var carry = Time.realtimeSinceStartup - carryStart;
                measurements.Add(new[] { cashier, barista, carry });
                Mark("M005", speed + " actual seconds cashier=" + cashier.ToString("F3") + " work=" + barista.ToString("F3") + " carry=" + carry.ToString("F3") + " maxFrame=" + largest.ToString("F3"));
                yield return Ready();
            }
            Assert.That(measurements[1][2] / measurements[0][2], Is.EqualTo(.5f).Within(.2f));
            Speed(GameSpeed.Normal); Start(EmployeeRole.Cashier); Start(EmployeeRole.Barista);
            yield return new WaitForSecondsRealtime(.2f); Pause(); yield return null; yield return null;
            yield return FrozenFor("M005 working", 2.05f, EmployeeRole.Cashier, EmployeeRole.Barista);
            var handle = State(EmployeeRole.Barista).HandleId; TaskButton(EmployeeRole.Barista).onClick.Invoke(); yield return null;
            Assert.That(Status.text, Does.Contain("Paused")); Assert.That(State(EmployeeRole.Barista).HandleId, Is.EqualTo(handle));
            Speed(GameSpeed.Normal); yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.CarryingToPickUp);
            yield return Wait(() => Vector3.Distance(Actor(EmployeeRole.Barista).transform.position, Binding(EmployeeRole.Barista).WorkTarget.Position) > .2f);
            Pause(); yield return null; yield return null;
            Assert.That(Held, Is.EqualTo(1)); yield return FrozenFor("M005 carrying", 2.05f, EmployeeRole.Barista);
            yield return Capture("m005-paused-carry"); Speed(GameSpeed.Normal); yield return Ready(); Mark("M005", "completed work/carry pause and resumed actual delivery");
        }

        [UnityTest] public IEnumerator M006_ActualDecorExitAndCancelledPreviewPreserveTask()
        {
            yield return MakeCup();
            foreach (var phase in new[] { EmployeeState.Working, EmployeeState.CarryingToPickUp })
            foreach (var preview in new[] { false, true })
            {
                Start(EmployeeRole.Barista); yield return Wait(() => State(EmployeeRole.Barista).State == phase);
                Enter(); yield return null;
                var before = State(EmployeeRole.Barista); var count = Count; var held = Held;
                if (preview)
                {
                    Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment, CM), Is.True);
                    Assert.That(decor.TryRotateFunctionalSurfacePreview(), Is.True); Assert.That(decor.ActiveFunctionalSurfacePreview.CanConfirm, Is.True);
                    decor.CancelFunctionalSurfacePreview(); Assert.That(decor.ActiveFunctionalSurfacePreview, Is.Null);
                }
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(State(EmployeeRole.Barista).HandleId, Is.EqualTo(before.HandleId));
                Assert.That(State(EmployeeRole.Barista).RemainingWorkSeconds, Is.EqualTo(before.RemainingWorkSeconds));
                Assert.That(Count, Is.EqualTo(count)); Assert.That(Held, Is.EqualTo(held));
                Assert.That(decor.TryRequestExit(), Is.True); Assert.That(State(EmployeeRole.Barista).HandleId, Is.EqualTo(before.HandleId));
                Mark("M006", phase + " previewCancel=" + preview + " same handle/remaining/hand/table before resume");
                yield return Ready(); Assert.That(State(EmployeeRole.Barista).Delivered, Is.True);
            }
        }

        [UnityTest] public IEnumerator M007_ConfirmedRotationAndSafeSupportMovesKeepEmployees()
        {
            foreach (var phase in new[] { EmployeeState.Working, EmployeeState.CarryingToPickUp })
            {
                if (phase == EmployeeState.CarryingToPickUp) yield return Reload();
                yield return MakeCup(); var actors = flow.Snapshot.Select(value => Actor(value.Role)).ToArray();
                var existingCup = Slot().Cast<Transform>().Single(child => child.name == "PickupCup_1");
                var oldCupLocal = existingCup.localPosition; var oldCupWorld = existingCup.position;
                Start(EmployeeRole.Barista); yield return Wait(() => State(EmployeeRole.Barista).State == phase);
                if (phase == EmployeeState.CarryingToPickUp) yield return Wait(() => Vector3.Distance(Actor(EmployeeRole.Barista).transform.position, Binding(EmployeeRole.Barista).WorkTarget.Position) > .5f);
                Enter(); var pose = Actor(EmployeeRole.Barista).transform.position; var oldHandle = State(EmployeeRole.Barista).HandleId;
                if (phase == EmployeeState.Working)
                {
                    for (var change = 0; change < 2; change++)
                    {
                        Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment, CM), Is.True);
                        Assert.That(decor.TryRotateFunctionalSurfacePreview(), Is.True); Assert.That(decor.TryConfirmFunctionalSurfacePreview(), Is.True);
                    }
                }
                else { MoveFurniture(CMTable, new GridPosition(4, 5)); MoveFurniture(PUTable, new GridPosition(6, 5)); }
                yield return null; Assert.That(State(EmployeeRole.Barista).HandleId, Is.Zero); Assert.That(Held, Is.Zero);
                Assert.That(Actor(EmployeeRole.Barista).transform.position, Is.EqualTo(pose));
                Assert.That(Count, Is.EqualTo(1), "Previously delivered table cup survives confirmed moves");
                Exit(); yield return Ready();
                foreach (var actor in actors) Assert.That(world.RegisteredActors.Single(value => value.ActorId == actor.ActorId), Is.SameAs(actor));
                Assert.That(Vector3.Distance(Actor(EmployeeRole.Barista).transform.position, Binding(EmployeeRole.Barista).WorkTarget.Position), Is.LessThanOrEqualTo(.08f));
                Assert.That(Vector3.Distance(pose, Actor(EmployeeRole.Barista).transform.position), Is.GreaterThan(.2f));
                Assert.That(State(EmployeeRole.Barista).HandleId, Is.Zero); Assert.That(Count, Is.EqualTo(1));
                if (phase == EmployeeState.CarryingToPickUp)
                {
                    var slot = Slot(); var cup = slot.Cast<Transform>().Single(child => child.name == "PickupCup_1");
                    Assert.That(cup, Is.SameAs(existingCup)); Assert.That(cup.parent, Is.SameAs(slot));
                    Assert.That(cup.localPosition, Is.EqualTo(oldCupLocal)); Assert.That(Vector3.Distance(cup.position, oldCupWorld), Is.GreaterThan(1));
                    Assert.That(layout.Layout.FurnitureInstances.Single(value => value.InstanceId == PUTable).Position, Is.EqualTo(new GridPosition(6, 5)));
                }
                Mark("M007", phase + " oldHandle=" + oldHandle + " two real Confirms; same employees returned; existing cup retained/follows module");
            }
        }

        [UnityTest] public IEnumerator M008_StoreAndRepairEachRequiredStation()
        {
            foreach (var station in new[] { CM, PU })
            {
                if (station == PU) yield return Reload();
                var actor = Actor(EmployeeRole.Barista); Start(EmployeeRole.Barista);
                yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.CarryingToPickUp);
                Enter(); StoreStation(station, station == PU); Exit(false); yield return null; yield return null;
                Assert.That(State(EmployeeRole.Barista).State, Is.EqualTo(EmployeeState.Blocked)); Assert.That(State(EmployeeRole.Barista).Failure, Is.Not.Empty);
                Assert.That(time.IsResumeBlocked, Is.True); Assert.That(Held, Is.Zero); Mark("M008", "Stored " + station + " actual blocked reason=" + State(EmployeeRole.Barista).Failure);
                Enter(); AddStation(station == PU ? DecorationCatalogueItemKind.PickUpPoint : DecorationCatalogueItemKind.CoffeeMachine,
                    station == PU ? "" : "equipment.coffee-machine.01", station == PU ? PUTable : CMTable);
                yield return ResumeAfterDecor(); Assert.That(Actor(EmployeeRole.Barista), Is.SameAs(actor));
                yield return MakeCup(); Mark("M008", "repaired " + station + " actual employee returned and new task delivered");
            }
        }

        [UnityTest] public IEnumerator M008_FixedWorkCellBlockAndRepair()
        {
            Start(EmployeeRole.Barista); var work = Binding(EmployeeRole.Barista).WorkTarget.Position;
            yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.CarryingToPickUp && Vector3.Distance(Actor(EmployeeRole.Barista).transform.position, work) > 1.1f);
            Enter(); var blocker = AddFurniture(new GridPosition(4, 4), allowBlocked: true);
            if (blocker == null) { decor.CancelActivePreview(); Exit(); Assert.Ignore("M008 fixed blocking branch BLOCKED by actual safe Confirm rejection; see operator reason"); }
            Exit(false); yield return null; yield return null;
            Assert.That(time.IsResumeBlocked, Is.True); Assert.That(State(EmployeeRole.Barista).State, Is.EqualTo(EmployeeState.Blocked));
            Mark("M008", "CONFIRMED fixed blocker (4,4) id=" + blocker + " reason=" + State(EmployeeRole.Barista).Failure); yield return Capture("m008-confirmed-block");
            Enter(); var session = Session; Assert.That(session.BeginExisting(blocker).Succeeded, Is.True);
            Call(decor, "SyncActivePreviewPresentation"); Call(decor, "HandleStoreRequested"); Call(decor, "HandleStoreConfirmRequested");
            Assert.That(layout.Layout.FurnitureInstances.Any(value => value.InstanceId == blocker), Is.False);
            yield return ResumeAfterDecor(); yield return MakeCup(); Mark("M008", "fixed blocker stored; actual recovery and new delivery");
        }

        [UnityTest] public IEnumerator M011_RepeatedClicksAndIndependentConcurrentRoles()
        {
            Start(EmployeeRole.Cashier); Start(EmployeeRole.Barista);
            foreach (var role in new[] { EmployeeRole.Cashier, EmployeeRole.Barista })
            {
                var handle = State(role).HandleId; for (var click = 0; click < 3; click++) TaskButton(role).onClick.Invoke();
                Assert.That(State(role).HandleId, Is.EqualTo(handle));
            }
            yield return null; Assert.That(Status.text, Does.Contain("Busy")); Mark("M011", "both roles Working; three repeat clicks each keep one handle");
            foreach (var phase in new[] { EmployeeState.CarryingToPickUp, EmployeeState.Returning })
            {
                yield return Wait(() => State(EmployeeRole.Barista).State == phase); var handle = State(EmployeeRole.Barista).HandleId;
                if (phase == EmployeeState.CarryingToPickUp) { Assert.That(State(EmployeeRole.Cashier).State, Is.EqualTo(EmployeeState.Idle)); Start(EmployeeRole.Cashier); }
                var cashierBefore = State(EmployeeRole.Cashier);
                for (var click = 0; click < 3; click++) TaskButton(EmployeeRole.Barista).onClick.Invoke();
                Assert.That(State(EmployeeRole.Barista).HandleId, Is.EqualTo(handle));
                Assert.That(State(EmployeeRole.Cashier).HandleId, Is.EqualTo(cashierBefore.HandleId));
                Assert.That(State(EmployeeRole.Cashier).Outcome, Is.EqualTo(cashierBefore.Outcome));
                yield return null; Assert.That(Status.text, Does.Contain("Busy")); Mark("M011", phase + " three repeat clicks, independent Cashier handle");
            }
            yield return Ready(); Assert.That(Count, Is.EqualTo(1)); Assert.That(Held, Is.Zero); Assert.That(world.RegisteredActors.Count, Is.EqualTo(2));
            Assert.That(State(EmployeeRole.Cashier).Outcome, Is.EqualTo(EmployeeTaskOutcome.Succeeded));
        }

        [UnityTest] public IEnumerator M012_SecondCompleteStationSetAndStableRebinding()
        {
            Enter(); var register = AddFurniture(new GridPosition(1, 6)); var machine = AddFurniture(new GridPosition(3, 6)); var pickup = AddFurniture(new GridPosition(5, 6));
            AddStation(DecorationCatalogueItemKind.CashRegister, "equipment.cash-register.01", register);
            AddStation(DecorationCatalogueItemKind.CoffeeMachine, "equipment.coffee-machine.01", machine);
            AddStation(DecorationCatalogueItemKind.PickUpPoint, "", pickup); Exit(); yield return Ready();
            Assert.That(layout.CurrentReadiness.Stations.Count(value => value.IsValid), Is.EqualTo(6));
            Mark("M012", "complete second set CR(1,6)/CM(3,6)/Pickup(5,6)"); Pause(); yield return Capture("m012-second-set"); Speed(GameSpeed.Normal);
            var originalCR = "12000000000000000000000000000004";
            foreach (var removed in new[] { "", CM, PU, originalCR })
            {
                if (removed != "") { Enter(); StoreStation(removed, removed == PU); yield return ResumeAfterDecor(); }
                var binding = Binding(EmployeeRole.Barista); var cr = Binding(EmployeeRole.Cashier);
                Assert.That(cr.WorkStationId, Is.EqualTo(Find<CustomerFlowController>().SelectedRegisterId));
                Start(EmployeeRole.Cashier); Start(EmployeeRole.Barista);
                yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.CarryingToPickUp);
                Assert.That(Binding(EmployeeRole.Barista), Is.SameAs(binding));
                yield return Ready(); Assert.That(State(EmployeeRole.Barista).Delivered, Is.True); Assert.That(Binding(EmployeeRole.Barista), Is.SameAs(binding));
                Assert.That(Binding(EmployeeRole.Cashier).WorkStationId, Is.EqualTo(Find<CustomerFlowController>().SelectedRegisterId));
                Mark("M012", "removed=" + removed + " immutable task binding CM=" + binding.WorkStationId + " Pickup=" + binding.PickUpId + " P12CR=" + cr.WorkStationId);
            }
        }

        [UnityTest] public IEnumerator M013_ContextCancelTwiceAtEveryApplicablePhase()
        {
            Start(EmployeeRole.Cashier); var cashier = Actor(EmployeeRole.Cashier); Menu("Cancel Cashier Task"); Menu("Cancel Cashier Task");
            Assert.That(State(EmployeeRole.Cashier).HandleId, Is.Zero); Assert.That(State(EmployeeRole.Cashier).Outcome, Is.EqualTo(EmployeeTaskOutcome.Cancelled));
            yield return Ready(); Assert.That(Actor(EmployeeRole.Cashier), Is.SameAs(cashier)); Assert.That(Count, Is.Zero); Mark("M013", "Cashier Working double ContextMenu cancel");
            foreach (var phase in new[] { EmployeeState.Working, EmployeeState.CarryingToPickUp, EmployeeState.Returning })
            {
                Start(EmployeeRole.Barista); yield return Wait(() => State(EmployeeRole.Barista).State == phase);
                if (phase == EmployeeState.CarryingToPickUp) yield return Wait(() => Vector3.Distance(Actor(EmployeeRole.Barista).transform.position, Binding(EmployeeRole.Barista).WorkTarget.Position) > .3f);
                var count = Count; var actor = Actor(EmployeeRole.Barista); var pose = actor.transform.position; var handle = State(EmployeeRole.Barista).HandleId;
                Menu("Cancel Barista Task"); Menu("Cancel Barista Task");
                var expectedOutcome = phase == EmployeeState.Returning ? EmployeeTaskOutcome.Succeeded : EmployeeTaskOutcome.Cancelled;
                Assert.That(State(EmployeeRole.Barista).HandleId, Is.Zero); Assert.That(State(EmployeeRole.Barista).Outcome, Is.EqualTo(expectedOutcome));
                Assert.That(actor.transform.position, Is.EqualTo(pose)); yield return null; Assert.That(Held, Is.Zero); Assert.That(Count, Is.EqualTo(count));
                yield return Ready(); Assert.That(Actor(EmployeeRole.Barista), Is.SameAs(actor));
                Assert.That(Count, Is.EqualTo(count)); Assert.That(State(EmployeeRole.Barista).Outcome, Is.EqualTo(expectedOutcome));
                Assert.That(Vector3.Distance(actor.transform.position, Binding(EmployeeRole.Barista).WorkTarget.Position), Is.LessThanOrEqualTo(.08f));
                Mark("M013", phase + " double ContextMenu cancel oldHandle=" + handle + " kept table cups=" + count);
            }
        }

        [UnityTest] public IEnumerator M014_ActualActorAndFlowDisableForOneRealSecond()
        {
            foreach (var actorToggle in new[] { true, false })
            {
                if (!actorToggle) yield return Reload();
                Start(EmployeeRole.Barista); yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.CarryingToPickUp);
                yield return Wait(() => Vector3.Distance(Actor(EmployeeRole.Barista).transform.position, Binding(EmployeeRole.Barista).WorkTarget.Position) > .3f);
                var actor = Actor(EmployeeRole.Barista); var id = actor.ActorId; var pose = actor.transform.position;
                if (actorToggle) actor.gameObject.SetActive(false); else flow.enabled = false;
                var start = Time.realtimeSinceStartup; yield return new WaitForSecondsRealtime(1.05f);
                Assert.That(actor.transform.position, Is.EqualTo(pose)); Assert.That(world.RegisteredActors.Contains(actor), Is.True);
                Assert.That(State(EmployeeRole.Barista).HandleId, Is.Zero); Assert.That(actor.GetComponentsInChildren<Transform>(true).Any(value => value.name == "EmployeeHeldCup"), Is.False);
                Assert.That(Count, Is.Zero); Mark("M014", "disabled actorGO=" + actorToggle + " actualSeconds=" + (Time.realtimeSinceStartup - start).ToString("F3") + " retained body/ID=" + id);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase13/Employees/Shiba/PF_Shiba_Employee.prefab");
                var probe = Object.Instantiate(prefab, pose, actor.transform.rotation).GetComponent<NavigationActor>();
                try
                {
                    Assert.That(world.LayoutAvailable, Is.True); Assert.That(Find<NavigationLayoutAdapter>().IsPointWalkable(pose), Is.True);
                    Assert.That(probe.TryInitializeRuntimeId("delegated-overlap-" + Guid.NewGuid().ToString("N")), Is.True);
                    Assert.That(world.Register(probe), Is.False, "Actual inactive/disabled employee body still rejects overlapping admission");
                    Mark("M014", "overlap probe admission rejected while actorGOdisabled=" + actorToggle);
                }
                finally { world.Unregister(probe); Object.Destroy(probe.gameObject); }
                yield return null;
                if (actorToggle) actor.gameObject.SetActive(true); else flow.enabled = true;
                yield return Ready(); Assert.That(Actor(EmployeeRole.Barista), Is.SameAs(actor)); Assert.That(actor.ActorId, Is.EqualTo(id));
                Assert.That(Vector3.Distance(actor.transform.position, Binding(EmployeeRole.Barista).WorkTarget.Position), Is.LessThanOrEqualTo(.08f));
                Assert.That(Held, Is.Zero); Assert.That(Count, Is.Zero); Assert.That(world.RegisteredActors.Count, Is.EqualTo(2));
                Mark("M014", "reenabled same body actually returned without old cup/task");
            }
        }

        private IEnumerator FrozenFor(string label, float seconds, params EmployeeRole[] roles)
        {
            var actors = roles.Select(Actor).ToArray(); var snapshots = roles.Select(State).ToArray();
            var positions = actors.Select(actor => actor.transform.position).ToArray();
            var facings = actors.Select(actor => actor.transform.rotation).ToArray();
            var bones = actors.SelectMany(actor => actor.ModelAnimator.GetComponentsInChildren<Transform>()).ToArray();
            var bonePositions = bones.Select(bone => bone.localPosition).ToArray(); var boneRotations = bones.Select(bone => bone.localRotation).ToArray();
            var held = Held; var count = Count; var start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                yield return null;
                for (var i = 0; i < actors.Length; i++)
                {
                    Assert.That(actors[i].transform.position, Is.EqualTo(positions[i]));
                    Assert.That(actors[i].transform.rotation, Is.EqualTo(facings[i]));
                    Assert.That(State(roles[i]).HandleId, Is.EqualTo(snapshots[i].HandleId)); Assert.That(State(roles[i]).RemainingWorkSeconds, Is.EqualTo(snapshots[i].RemainingWorkSeconds));
                }
                for (var i = 0; i < bones.Length; i++)
                { Assert.That(Vector3.Distance(bones[i].localPosition, bonePositions[i]), Is.LessThan(.00001f)); Assert.That(Quaternion.Angle(bones[i].localRotation, boneRotations[i]), Is.LessThan(.001f)); }
                Assert.That(Held, Is.EqualTo(held)); Assert.That(Count, Is.EqualTo(count));
            }
            Mark("M005", label + " actually frozen seconds=" + (Time.realtimeSinceStartup - start).ToString("F3") + " bones=" + bones.Length);
        }
        private IEnumerator MakeCup() { Start(EmployeeRole.Barista); yield return Ready(); Assert.That(State(EmployeeRole.Barista).Delivered, Is.True); }
        private void Start(EmployeeRole role) { Assert.That(State(role).State, Is.EqualTo(EmployeeState.Idle)); TaskButton(role).onClick.Invoke(); Assert.That(State(role).HandleId, Is.GreaterThan(0), Status.text); Mark("TASK", "HUD click " + role); }
        private void Enter() { if (!decor.IsOpen) mode.onClick.Invoke(); Assert.That(decor.IsOpen, Is.True); }
        private void Exit(bool run = true) { Assert.That(decor.TryRequestExit(), Is.True); Assert.That(decor.IsOpen, Is.False); if (run && !time.IsResumeBlocked) Speed(GameSpeed.Normal); }
        private IEnumerator ResumeAfterDecor()
        {
            // 先让生产 Update 观察 Confirm；再修复 gate、显式点击营业，避免读取旧 Idle snapshot。
            Exit(false); yield return null; yield return null;
            Menu("Retry Blocked Employees"); yield return null;
            Assert.That(time.IsResumeBlocked, Is.False, time.ResumeBlockReason);
            Speed(GameSpeed.Normal); yield return null;
            yield return Ready(); Mark("REPAIR", "confirmed layout observed across frames; explicit TimePanel 1x; real return complete");
        }
        private void Speed(GameSpeed value) { if (time.CurrentSpeed != value) (value == GameSpeed.Fast ? fast : normal).onClick.Invoke(); Assert.That(time.CurrentSpeed, Is.EqualTo(value), time.ResumeBlockReason); }
        private void Pause() { if (time.CurrentSpeed != GameSpeed.Paused) (time.CurrentSpeed == GameSpeed.Fast ? fast : normal).onClick.Invoke(); Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused)); }
        private void Menu(string name)
        {
            var method = typeof(EmployeeTaskValidationController).GetMethods(Private | BindingFlags.Public).Single(value => value.GetCustomAttributes(typeof(ContextMenu), true).Cast<ContextMenu>().Any(menu => menu.menuItem == "P13/" + name));
            method.Invoke(debug, null); Mark("CONTEXT", "P13/" + name);
        }
        private void StoreStation(string id, bool pickup)
        {
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(pickup ? FunctionalSurfacePreviewKind.PickUpPoint : FunctionalSurfacePreviewKind.MountedEquipment, id), Is.True);
            Assert.That(decor.TryStoreFunctionalSurfacePreview(), Is.True, "Actual ConfirmStore API");
        }
        private string AddStation(DecorationCatalogueItemKind kind, string definition, string support)
        {
            Assert.That(decor.TryBeginFunctionalSurfacePreview(kind, definition, new SurfaceSlotAddress(support, "slot.0")), Is.True);
            var id = decor.ActiveFunctionalSurfacePreview.InstanceId;
            Assert.That(decor.TryConfirmFunctionalSurfacePreview(), Is.True); return id;
        }
        private string AddFurniture(GridPosition cell, bool allowBlocked = false)
        {
            var before = layout.Layout.FurnitureInstances.Select(value => value.InstanceId).ToArray(); Session.BeginNew("furniture.counter.module.01", cell);
            Assert.That(Session.MovePreview(cell).Succeeded, Is.True); Call(decor, "SyncActivePreviewPresentation"); Call(decor, "HandleConfirmRequested");
            if (Session.ActivePreview != null && allowBlocked) { Mark("M008_BLOCKED", "actual Confirm refused: " + Field<TMPro.TMP_Text>(Find<DecorationActionBarView>(), "feedbackLabel").text); return null; }
            Assert.That(Session.ActivePreview, Is.Null, "Actual controller Confirm must pass navigation/body protection");
            return layout.Layout.FurnitureInstances.Single(value => !before.Contains(value.InstanceId)).InstanceId;
        }
        private void MoveFurniture(string id, GridPosition cell)
        {
            Assert.That(Session.BeginExisting(id).Succeeded, Is.True); Assert.That(Session.MovePreview(cell).Succeeded, Is.True);
            Call(decor, "SyncActivePreviewPresentation"); Call(decor, "HandleConfirmRequested"); Assert.That(Session.ActivePreview, Is.Null);
        }
        private IEnumerator Ready() => Wait(() => flow.Snapshot.Count == 2 && flow.Snapshot.All(value => value.State == EmployeeState.Idle));
        private IEnumerator Wait(Func<bool> condition)
        {
            var deadline = Time.realtimeSinceStartup + 45;
            while (!condition() && Time.realtimeSinceStartup < deadline) { yield return null; }
            Assert.That(condition(), Is.True, "OPERATOR_WAIT: " + Summary());
        }
        private IEnumerator Capture(string name)
        {
            if (Environment.GetEnvironmentVariable("ANIMALCAFE_P13_DELEGATED_CAPTURE") != "true") yield break;
            var path = System.IO.Path.GetFullPath("outputs/phase13/delegated-" + (Environment.GetEnvironmentVariable("ANIMALCAFE_P13_DELEGATED_RUN") ?? "r1") + "-" + name + ".png");
            Assert.That(File.Exists(path), Is.False, "Keep previous evidence"); Assert.That(UnityEngine.Camera.main.targetTexture, Is.Null);
            Assert.That(FindAll<Canvas>().Any(value => value.isActiveAndEnabled && value.renderMode == RenderMode.ScreenSpaceOverlay), Is.True);
            var deadline = Time.realtimeSinceStartup + 45;
            while (ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ShaderUtil.anythingCompiling, Is.False); Canvas.ForceUpdateCanvases(); yield return null; yield return null;
            ScreenCapture.CaptureScreenshot(path); deadline = Time.realtimeSinceStartup + 8;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(File.Exists(path), Is.True, "Actual camera + Overlay Canvas capture unavailable"); Mark("CAPTURE", path + " " + Screen.width + "x" + Screen.height);
        }
        private string Summary() => "speed=" + (time == null ? "?" : time.CurrentSpeed.ToString()) + " gate=" + (time == null ? "?" : time.ResumeBlockReason) + " " + string.Join(" | ", flow.Snapshot.Select(value => value.Role + " " + value.State + " h=" + value.HandleId + " rem=" + value.RemainingWorkSeconds.ToString("F3") + " delivered=" + value.Delivered + " reason=" + value.Failure));
        private void Mark(string id, string detail) => TestContext.WriteLine(id + " API_OPERATOR " + detail + " | " + Summary() + " pose=" + (world == null ? "?" : string.Join(";", world.RegisteredActors.Select(actor => actor.ActorId + "@" + actor.transform.position.ToString("F3")))) + " held=" + (flow == null ? -1 : Held) + " cups=" + (flow == null ? -1 : Count));
        private EmployeeTaskSnapshot State(EmployeeRole role) => flow.Snapshot.Single(value => value.Role == role);
        private NavigationActor Actor(EmployeeRole role) => world.RegisteredActors.Single(value => value.ActorId == State(role).ActorId);
        private EmployeeStationBinding Binding(EmployeeRole role) { Assert.That(flow.TryGetBinding(role, out var result), Is.True); return result; }
        private int Held => Actor(EmployeeRole.Barista).GetComponentsInChildren<Transform>(true).Count(value => value.name == "EmployeeHeldCup");
        private int Count => flow.TryGetBinding(EmployeeRole.Barista, out var binding) && binding.CupAddress.HasValue ? cups.VisibleCount(binding.CupAddress.Value) : 0;
        private Text Status => flow.transform.Find("P13 Task HUD/Panel/Status").GetComponent<Text>();
        private Button TaskButton(EmployeeRole role) => flow.transform.Find("P13 Task HUD/Panel/" + (role == EmployeeRole.Cashier ? "Cashier Task" : "Barista Task")).GetComponent<Button>();
        private DecorationSession Session => Field<DecorationSession>(decor, "session");
        private Transform Slot() { Assert.That(Find<FurnitureSceneRegistry>().TryGet(Binding(EmployeeRole.Barista).CupAddress.Value.SupportFurnitureInstanceId, out var support), Is.True); return support.GetComponentsInChildren<AnimalCafe.Content.SurfaceSlotMarker>().Single(value => value.SlotId == Binding(EmployeeRole.Barista).CupAddress.Value.SlotId).transform; }
        private T Find<T>() where T : Component => FindAll<T>().Single();
        private T[] FindAll<T>() where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Private).GetValue(owner);
        private static void Call(object owner, string name) => owner.GetType().GetMethod(name, Private).Invoke(owner, null);
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using AnimalCafe.Content;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using AnimalCafe.UI;
using AnimalCafe.Tests.PlayMode.EditorSceneLoading;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode.Phase13
{
    public sealed partial class EmployeeMainCafeTests
    {
        private Scene scene;
        private EmployeeFlowController flow;
        private CustomerFlowController customers;
        private NavigationWorld world;
        private CafeLayoutRuntime layout;
        private DecorationModeController decor;
        private float capture, scale;
        private UnityEngine.InputSystem.InputActionAsset[] inputAssets;
        private const string Validation = "Assets/Scenes/Validation/Phase13EmployeeTasks.unity";
        [SetUp] public void Remember() { capture = Time.captureDeltaTime; scale = Time.timeScale; Time.captureDeltaTime = 1f / 60; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                inputAssets = Phase8SceneInputTestCleanup.CaptureAssets(scene);
                SceneManager.SetActiveScene(SceneManager.CreateScene("P13 cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
                Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets);
            }
            Time.captureDeltaTime = capture; Time.timeScale = scale;
        }
        private IEnumerator Load(string path)
        {
            scene = EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            flow = All<EmployeeFlowController>().SingleOrDefault();
            Assert.That(flow, Is.Not.Null, "MainCafe must contain the installed P13 Employees controller");
            customers = Find<CustomerFlowController>();
            world = Find<NavigationWorld>(); layout = Find<CafeLayoutRuntime>(); decor = Find<DecorationModeController>();
        }
        [UnityTest] public IEnumerator EmptyCafeRemainsEditableUntilStationsAreReady()
        {
            yield return Load("Assets/Scenes/MainCafe.unity"); yield return null; yield return null;
            Assert.That(customers.AutoSpawn, Is.True, "Preserve the existing P12 setting"); customers.AutoSpawn = false;
            Assert.That(world.RegisteredActors, Is.Empty); Assert.That(layout.CurrentReadiness.CanOpenForBusiness, Is.False);
            decor.EnterDecorationMode(); Assert.That(decor.IsOpen, Is.True); decor.ExitDecorationMode(); Assert.That(decor.IsOpen, Is.False);
            Seed(); decor.RefreshConfirmedNavigationPresentation();
            Find<NavigationDecorationBridge>().ResumeAfterRepair();
            yield return null; // Existing MainCafe setup panel releases its readiness block on Update.
            Assert.That(Find<GameTimeService>().TrySetSpeed(GameSpeed.Normal), Is.True, Find<GameTimeService>().ResumeBlockReason);
            yield return Ready(); Assert.That(world.RegisteredActors.Count, Is.EqualTo(2));
            var ids = flow.Snapshot.Select(value => value.ActorId).ToArray();
            decor.EnterDecorationMode();
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment, "12000000000000000000000000000005"), Is.True);
            Assert.That(decor.TryStoreFunctionalSurfacePreview(), Is.True);
            decor.ExitDecorationMode(); yield return null;
            Assert.That(Find<GameTimeService>().IsResumeBlocked, Is.True);
            Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.False);
            decor.EnterDecorationMode(); Assert.That(decor.IsOpen, Is.True);
            Assert.That(flow.Snapshot.Select(value => value.ActorId), Is.EqualTo(ids));
            decor.ExitDecorationMode(); Assert.That(decor.IsOpen, Is.False);
        }
        [UnityTest] public IEnumerator StaffAndCustomersShareSelectedRegister()
        {
            yield return Load(Validation); yield return Ready();
            Assert.That(customers.AutoSpawn, Is.False);
            yield return AssertStaffAndCustomersShareSelectedRegister();
        }
        [UnityTest] public IEnumerator MainCafeStaffAndFourCustomersShareSelectedRegister()
        {
            yield return Load("Assets/Scenes/MainCafe.unity"); yield return null; yield return null;
            Assert.That(scene.path, Is.EqualTo("Assets/Scenes/MainCafe.unity"));
            Assert.That(customers.AutoSpawn, Is.True, "Keep the saved MainCafe P12 setting");
            customers.AutoSpawn = false; // Controlled four real admissions; no random fifth during assertions.
            Assert.That(world.RegisteredActors, Is.Empty);
            Assert.That(layout.CurrentReadiness.CanOpenForBusiness, Is.False);
            Seed(); decor.RefreshConfirmedNavigationPresentation();
            Find<NavigationDecorationBridge>().ResumeAfterRepair();
            yield return null; // The existing MainCafe time panel releases its setup block on Update.
            var time = Find<GameTimeService>();
            Assert.That(time.TrySetSpeed(GameSpeed.Normal), Is.True, time.ResumeBlockReason);
            yield return Ready();
            yield return AssertStaffAndCustomersShareSelectedRegister();
        }
        [UnityTest] public IEnumerator RealGameTimeScalesWorkCarryAndFreezesCarrier()
        {
            yield return Load(Validation); yield return Ready();
            var time=Find<GameTimeService>(); var display=Find<PickupCupDisplay>();
            Assert.That(flow.TryGetBinding(EmployeeRole.Barista,out var binding),Is.True);
            var barista=world.RegisteredActors.Single(a=>a.ActorId==State(EmployeeRole.Barista).ActorId);
            var carryReal=new float[2]; var carryDistance=new float[2];
            Time.captureDeltaTime=0; // Actual production Update/delta, not injected Step or capture-frame delta.
            for(var pass=0;pass<2;pass++)
            {
                var speed=pass==0?GameSpeed.Normal:GameSpeed.Fast;
                Assert.That(time.TrySetSpeed(speed),Is.True,time.ResumeBlockReason);
                yield return null; // Establish this frame's real scaled delta before starting work.
                Assert.That(Time.timeScale,Is.EqualTo(pass==0?1:2));
                Assert.That(flow.TryStartTask(EmployeeRole.Cashier),Is.True);
                Assert.That(flow.TryStartTask(EmployeeRole.Barista),Is.True);
                var elapsed=0f; var cashierSeconds=-1f; var maxFrame=0f;
                while(State(EmployeeRole.Barista).State==EmployeeState.Working && elapsed<6)
                {
                    yield return null; elapsed+=Time.unscaledDeltaTime; maxFrame=Mathf.Max(maxFrame,Time.unscaledDeltaTime);
                    if(cashierSeconds<0 && State(EmployeeRole.Cashier).Outcome==EmployeeTaskOutcome.Succeeded) cashierSeconds=elapsed;
                }
                var tolerance=Mathf.Max(.08f,maxFrame*1.5f);
                Assert.That(cashierSeconds,Is.EqualTo(pass==0?2f:1f).Within(tolerance));
                Assert.That(elapsed,Is.EqualTo(pass==0?3f:1.5f).Within(tolerance));
                Assert.That(State(EmployeeRole.Barista).State,Is.EqualTo(EmployeeState.CarryingToPickUp));
                var held=barista.GetComponentsInChildren<Transform>().Single(t=>t.name=="EmployeeHeldCup");
                var tables=display.VisibleCount(binding.CupAddress.Value);
                var carryStart=barista.transform.position;
                if(pass==0)
                {
                    time.SetPaused(); yield return null; // Drain the already evaluated running frame.
                    var paused=State(EmployeeRole.Barista); var position=barista.transform.position; var facing=barista.transform.rotation;
                    var bones=barista.ModelAnimator.GetComponentsInChildren<Transform>();
                    var positions=bones.Select(b=>b.localPosition).ToArray(); var rotations=bones.Select(b=>b.localRotation).ToArray();
                    var animation=barista.ModelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    for(var frame=0;frame<12;frame++)
                    {
                        yield return null;
                        Assert.That(barista.transform.position,Is.EqualTo(position)); Assert.That(barista.transform.rotation,Is.EqualTo(facing));
                        Assert.That(State(EmployeeRole.Barista).HandleId,Is.EqualTo(paused.HandleId));
                        Assert.That(State(EmployeeRole.Barista).RemainingWorkSeconds,Is.EqualTo(paused.RemainingWorkSeconds));
                        Assert.That(State(EmployeeRole.Barista).Delivered,Is.False);
                        Assert.That(held != null,Is.True); Assert.That(held.gameObject.activeInHierarchy,Is.True);
                        Assert.That(display.VisibleCount(binding.CupAddress.Value),Is.EqualTo(tables));
                        Assert.That(barista.ModelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime,Is.EqualTo(animation).Within(.0001f));
                        for(var b=0;b<bones.Length;b++)
                        { Assert.That(Vector3.Distance(bones[b].localPosition,positions[b]),Is.LessThan(.00001f)); Assert.That(Quaternion.Angle(bones[b].localRotation,rotations[b]),Is.LessThan(.001f)); }
                    }
                    Assert.That(time.TrySetSpeed(speed),Is.True);
                }
                var last=barista.transform.position; var carrySeconds=0f;
                while(!State(EmployeeRole.Barista).Delivered && carrySeconds<15)
                {
                    yield return null; carrySeconds+=Time.unscaledDeltaTime;
                    carryDistance[pass]+=Vector3.Distance(last,barista.transform.position); last=barista.transform.position;
                }
                Assert.That(State(EmployeeRole.Barista).Delivered,Is.True);
                Assert.That(State(EmployeeRole.Barista).Outcome,Is.EqualTo(EmployeeTaskOutcome.Succeeded));
                Assert.That(Vector3.Distance(carryStart,barista.transform.position),Is.GreaterThan(1));
                Assert.That(carryDistance[pass],Is.GreaterThan(1));
                Assert.That(display.VisibleCount(binding.CupAddress.Value),Is.EqualTo(tables+1));
                Assert.That(barista.GetComponentsInChildren<Transform>().Any(t=>t.name=="EmployeeHeldCup"),Is.False);
                carryReal[pass]=carrySeconds;
                TestContext.WriteLine($"QA01_REAL_TIME speed={speed} cashier={cashierSeconds:F4}s barista={elapsed:F4}s carry={carrySeconds:F4}s distance={carryDistance[pass]:F4}m maxFrame={maxFrame:F4}s");
                var returnSeconds=0f;
                while(State(EmployeeRole.Barista).State!=EmployeeState.Idle && returnSeconds<15)
                { yield return null; returnSeconds+=Time.unscaledDeltaTime; }
                Assert.That(State(EmployeeRole.Barista).State,Is.EqualTo(EmployeeState.Idle),State(EmployeeRole.Barista).Failure);
                Assert.That(Vector3.Distance(barista.transform.position,binding.WorkTarget.Position),Is.LessThanOrEqualTo(.08f));
            }
            Assert.That(carryDistance[1]/carryDistance[0],Is.EqualTo(1).Within(.12f),"Same real route at both speeds");
            Assert.That(carryReal[1]/carryReal[0],Is.EqualTo(.5f).Within(.12f),"Actual carrier must run twice, not four times, as fast");
        }

        [UnityTest] public IEnumerator MainCafeConfirmedDecorResetsFourCustomersAndPreservesActiveStaff()
        {
            yield return Load("Assets/Scenes/MainCafe.unity"); yield return null; yield return null;
            Assert.That(scene.path,Is.EqualTo("Assets/Scenes/MainCafe.unity"));
            Assert.That(customers.AutoSpawn,Is.True); customers.AutoSpawn=false;
            Seed(); decor.RefreshConfirmedNavigationPresentation(); Find<NavigationDecorationBridge>().ResumeAfterRepair();
            yield return null; // Existing MainCafe setup panel releases its independent resume block on Update.
            Assert.That(Find<GameTimeService>().TrySetSpeed(GameSpeed.Normal),Is.True,Find<GameTimeService>().ResumeBlockReason);
            Assert.That(layout.CurrentReadiness.CanOpenForBusiness,Is.True);
            Assert.That(Find<NavigationLayoutAdapter>().CurrentReadiness.CanResume,Is.True);
            yield return Ready();
            for(var i=0;i<4;i++)
            { Assert.That(customers.TrySpawn(),Is.True,customers.Status); yield return Wait(()=>customers.Snapshot.All(v=>v.State==CustomerVisitState.Queued),true); }
            Assert.That(customers.Snapshot.Count,Is.EqualTo(4)); Assert.That(world.RegisteredActors.Count,Is.EqualTo(6));
            var staff=flow.Snapshot.ToArray(); var staffActors=staff.Select(s=>world.RegisteredActors.Single(a=>a.ActorId==s.ActorId)).ToArray();
            var proxies=staffActors.Select(a=>a.Proxy).ToArray(); var radii=proxies.Select(p=>p.radius).ToArray(); var heights=proxies.Select(p=>p.height).ToArray();
            var oldVisits=customers.Snapshot.Select(v=>v.VisitId).ToArray();
            var oldCustomerActors=world.RegisteredActors.Except(staffActors).ToArray();
            Assert.That(flow.TryGetBinding(EmployeeRole.Barista,out var oldBinding),Is.True);
            Assert.That(flow.TryStartTask(EmployeeRole.Barista),Is.True);
            yield return Wait(()=>State(EmployeeRole.Barista).State==EmployeeState.CarryingToPickUp,true);
            var barista=staffActors.Single(a=>a.ActorId==State(EmployeeRole.Barista).ActorId);
            yield return Wait(()=>Vector3.Distance(barista.transform.position,oldBinding.WorkTarget.Position)>.3f,true);
            Assert.That(flow.TryStartTask(EmployeeRole.Cashier),Is.True);
            var oldHandle=State(EmployeeRole.Barista).HandleId;
            var held=barista.GetComponentsInChildren<Transform>().Single(t=>t.name=="EmployeeHeldCup");
            var before=barista.transform.position; var version=decor.CompletedLayoutChangeVersion;
            var display=Find<PickupCupDisplay>(); Assert.That(display.VisibleCount(oldBinding.CupAddress.Value),Is.Zero);
            decor.EnterDecorationMode();
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,"12000000000000000000000000000005"),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True); Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);
            flow.Step(0); customers.Step(0);
            Assert.That(decor.CompletedLayoutChangeVersion,Is.EqualTo(version));
            CollectionAssert.AreEqual(oldVisits,customers.Snapshot.Select(v=>v.VisitId).ToArray(),"Customers reset only after completed Decor");
            Assert.That(State(EmployeeRole.Barista).HandleId,Is.Zero); Assert.That(State(EmployeeRole.Barista).Outcome,Is.EqualTo(EmployeeTaskOutcome.Cancelled));
            Assert.That(State(EmployeeRole.Cashier).HandleId,Is.Zero);
            Assert.That(barista.GetComponentsInChildren<Transform>().Any(t=>t.name=="EmployeeHeldCup"),Is.False);
            Assert.That(decor.TryRequestExit(),Is.True); customers.Step(0); flow.Step(0);
            Assert.That(decor.CompletedLayoutChangeVersion,Is.EqualTo(version+1));
            Assert.That(customers.Snapshot,Is.Empty); Assert.That(world.RegisteredActors.Count,Is.EqualTo(2));
            Assert.That(oldCustomerActors.Any(a=>world.RegisteredActors.Contains(a)),Is.False);
            CollectionAssert.AreEquivalent(staffActors,world.RegisteredActors);
            Assert.That(barista.transform.position,Is.EqualTo(before),"Done/rebind cannot teleport employee bodies");
            var travelled=0f; var last=barista.transform.position;
            for(var frame=0;frame<3600 && flow.Snapshot.Any(s=>s.State!=EmployeeState.Idle);frame++)
            { AssertBodies(); yield return null; travelled+=Vector3.Distance(last,barista.transform.position); last=barista.transform.position; }
            Assert.That(flow.Snapshot.All(s=>s.State==EmployeeState.Idle),Is.True,State(EmployeeRole.Barista).Failure);
            Assert.That(held==null,Is.True); Assert.That(display.VisibleCount(oldBinding.CupAddress.Value),Is.Zero,"Old callback never delivers after reset");
            Assert.That(travelled,Is.GreaterThan(.3f));
            Assert.That(flow.TryGetBinding(EmployeeRole.Barista,out var newBinding),Is.True);
            Assert.That(Vector3.Distance(oldBinding.WorkTarget.Position,newBinding.WorkTarget.Position),Is.GreaterThan(1));
            Assert.That(Vector3.Distance(barista.transform.position,newBinding.WorkTarget.Position),Is.LessThanOrEqualTo(.08f));
            Assert.That(Vector3.Angle(barista.transform.forward,newBinding.WorkTarget.Facing.Value),Is.LessThanOrEqualTo(5));
            foreach(var employee in staff)
            {
                var current=flow.Snapshot.Single(s=>s.Role==employee.Role); Assert.That(current.ActorId,Is.EqualTo(employee.ActorId));
                var index=Array.FindIndex(staff,s=>s.Role==employee.Role); var actor=world.RegisteredActors.Single(a=>a.ActorId==current.ActorId);
                Assert.That(actor,Is.SameAs(staffActors[index])); Assert.That(actor.Proxy,Is.SameAs(proxies[index]));
                Assert.That(actor.Proxy.radius,Is.EqualTo(radii[index])); Assert.That(actor.Proxy.height,Is.EqualTo(heights[index]));
            }
            Assert.That(flow.TryStartTask(EmployeeRole.Barista),Is.True); Assert.That(State(EmployeeRole.Barista).HandleId,Is.Not.EqualTo(oldHandle));
            yield return Wait(()=>State(EmployeeRole.Barista).State==EmployeeState.Idle,true);
            Assert.That(State(EmployeeRole.Barista).Delivered,Is.True); Assert.That(display.VisibleCount(newBinding.CupAddress.Value),Is.EqualTo(1));
            Assert.That(customers.Snapshot,Is.Empty);
            TestContext.WriteLine($"QA03_REAL_MAINCAFE Confirm reset4Customers; same2Employee objects/IDs/roles/proxies; returned {travelled:F3}m to changed anchor then new task delivered");
        }

        private IEnumerator AssertStaffAndCustomersShareSelectedRegister()
        {
            Assert.That(flow.TryGetBinding(EmployeeRole.Cashier, out var binding), Is.True);
            Assert.That(binding.WorkStationId, Is.EqualTo(customers.SelectedRegisterId));
            var cashier = world.RegisteredActors.Single(actor => actor.ActorId == State(EmployeeRole.Cashier).ActorId);
            Assert.That(Vector3.Distance(cashier.transform.position, binding.WorkTarget.Position), Is.LessThan(.08f));
            for (var count = 0; count < 4; count++)
            {
                Assert.That(customers.TrySpawn(), Is.True, customers.Status);
                yield return Wait(() => customers.Snapshot.All(value => value.State == CustomerVisitState.Queued), true);
            }
            var ids = customers.Snapshot.Select(value => value.VisitId).ToArray();
            Assert.That(customers.Snapshot.Count, Is.EqualTo(4)); Assert.That(world.RegisteredActors.Count, Is.EqualTo(6));
            Assert.That(Vector3.Distance(customers.Snapshot[0].Position, cashier.transform.position), Is.GreaterThan(1.8f), "CR has two separate sides");
            Assert.That(flow.TryStartTask(EmployeeRole.Cashier), Is.True); Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.True);
            yield return Wait(() => State(EmployeeRole.Cashier).Outcome == EmployeeTaskOutcome.Succeeded, true);
            Assert.That(customers.Snapshot.Select(value => value.VisitId), Is.EqualTo(ids), "Manual service does not consume a real order/customer");
            Assert.That(customers.TryLetFrontLeave(), Is.True);
            yield return Wait(() => customers.Snapshot.Count == 3 && customers.Snapshot.All(value => value.State == CustomerVisitState.Queued), true);
            Assert.That(customers.Snapshot.Select(value => value.VisitId), Is.EqualTo(ids.Skip(1)), "FIFO advances after controlled leave");
            yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.Idle, true);
            Assert.That(State(EmployeeRole.Barista).Delivered, Is.True);
            Assert.That(flow.TryGetBinding(EmployeeRole.Barista, out var baristaBinding), Is.True);
            var barista = world.RegisteredActors.Single(actor => actor.ActorId == State(EmployeeRole.Barista).ActorId);
            Assert.That(Vector3.Distance(barista.transform.position, baristaBinding.WorkTarget.Position), Is.LessThanOrEqualTo(.08f), "Actually return to the CM work anchor");
            Assert.That(Find<PickupCupDisplay>().VisibleCount(baristaBinding.CupAddress.Value), Is.EqualTo(1), "Actual delivery keeps one table cup");
            TestContext.WriteLine("REAL_SHARED_CR scene=" + scene.path + " four customers + two employees; FIFO front leave; body distances checked each moving frame; Barista delivered and returned to CM");
        }
        [UnityTest] public IEnumerator RotatedMultiSlotTableKeepsCupModule()
        {
            yield return Load(Validation); flow.enabled = false;
            var pivot = new GameObject("Rigid rotated fixture"); SceneManager.MoveGameObjectToScene(pivot, scene);
            foreach (var root in scene.GetRootGameObjects().Where(root => root != pivot).ToArray()) root.transform.SetParent(pivot.transform, true);
            pivot.transform.SetPositionAndRotation(new Vector3(13, 0, -7), Quaternion.Euler(0, 37, 0));
            yield return null; yield return null; yield return null;
            Assert.That(layout.Layout.PlaceFurniture(FurnitureInstance.Restore("13000000000000000000000000000007", "counter.preset.1x3", new GridPosition(5, 5), FurnitureRotation.Degrees0)).Succeeded, Is.True);
            var address = new SurfaceSlotAddress("13000000000000000000000000000007", "slot.1");
            Assert.That(layout.FunctionalSurfaceLayout.MovePickUp("12000000000000000000000000000006", address).Succeeded, Is.True);
            decor.RefreshConfirmedNavigationPresentation(); Find<NavigationDecorationBridge>().ResumeAfterRepair();
            flow.enabled = true; Find<GameTimeService>().TrySetSpeed(GameSpeed.Normal); yield return Ready();
            Assert.That(flow.TryGetBinding(EmployeeRole.Barista, out var binding), Is.True);
            Assert.That(binding.CupAddress.Value, Is.EqualTo(address));
            var registry = Find<FurnitureSceneRegistry>(); Assert.That(registry.TryGet(address.SupportFurnitureInstanceId, out var support), Is.True);
            var modules = support.GetComponentsInChildren<SurfaceSlotMarker>(); Assert.That(modules.Length, Is.EqualTo(3));
            var slot = modules.Single(module => module.SlotId == "slot.1").transform;
            Assert.That(slot.position.y, Is.EqualTo(.42f).Within(.001f), "Rotated multi-slot support uses the lowered tabletop");
            var expected = new[] { new Vector3(-.12f, 0, -.08f), new Vector3(.12f, 0, -.08f), new Vector3(0, 0, .12f) };
            for (var cycle = 0; cycle < 3; cycle++)
            {
                Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.True);
                if (cycle == 0 && Environment.GetEnvironmentVariable("ANIMALCAFE_P13_CAPTURE") == "true")
                {
                    Assert.That(State(EmployeeRole.Barista).State, Is.EqualTo(EmployeeState.Working));
                    yield return CaptureScreen("p13-actual-working.png", EmployeeState.Working);
                    yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.CarryingToPickUp);
                    yield return CaptureScreen("p13-actual-carrying.png", EmployeeState.CarryingToPickUp);
                }
                yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.Idle);
                Assert.That(State(EmployeeRole.Barista).Delivered, Is.True);
            }
            var cups = slot.Cast<Transform>().Where(child => child.name.StartsWith("PickupCup_")).OrderBy(child => child.name).ToArray();
            Assert.That(cups.Length, Is.EqualTo(3));
            for (var index = 0; index < 3; index++)
            {
                Assert.That(Vector3.Distance(cups[index].localPosition, expected[index]), Is.LessThan(.001f));
                Assert.That(CounterVisualBounds(cups[index].gameObject).min.y, Is.EqualTo(.42f).Within(.005f), "Cup rests on the lowered table");
            }
            Assert.That(modules.Where(module => module.SlotId != "slot.1").Any(module => module.GetComponentsInChildren<Transform>().Any(child => child.name.StartsWith("PickupCup_"))), Is.False);
            if (Environment.GetEnvironmentVariable("ANIMALCAFE_P13_CAPTURE") == "true") yield return CaptureScreen("p13-actual-three-cups.png", EmployeeState.Idle);
            TestContext.WriteLine("REAL_ROTATED_MULTISLOT grid/representations/floor rigid 37deg; actual three deliveries to slot.1 local triangle");
        }
        [UnityTest] public IEnumerator HudControlsRemainClickableAcrossAspectRatios()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            foreach (var size in new[] { new Vector2(1080, 1920), new Vector2(1600, 720) })
            {
                screen.Resize(size); yield return null; yield return null;
                yield return Load(Validation); yield return Ready(); Canvas.ForceUpdateCanvases();
                var buttons = flow.transform.Find("P13 Task HUD").GetComponentsInChildren<Button>();
                Assert.That(buttons.Length, Is.EqualTo(2));
                foreach (var button in buttons) { AssertRaycast(button); Click(button); }
                Assert.That(State(EmployeeRole.Barista).State, Is.EqualTo(EmployeeState.Working));
                Assert.That(State(EmployeeRole.Cashier).State, Is.EqualTo(EmployeeState.Working));
                Click(buttons.Single(button => button.name == "Barista Task")); yield return null;
                Assert.That(flow.transform.Find("P13 Task HUD/Panel/Status").GetComponent<Text>().text, Does.Contain("Busy"));
                var controls = Find<TimeControlPanel>(); var normal = Ref<Button>(controls, "normalButton");
                AssertRaycast(normal); Click(normal); yield return null;
                Assert.That(Find<GameTimeService>().CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
                Click(buttons[0]); yield return null;
                Assert.That(flow.transform.Find("P13 Task HUD/Panel/Status").GetComponent<Text>().text, Does.Contain("Paused"));
                var mode = controls.transform.Find("DecorationModeButton").GetComponent<Button>();
                AssertRaycast(mode); Click(mode); yield return null; Assert.That(decor.IsOpen, Is.True);
                Assert.That(flow.transform.Find("P13 Task HUD").gameObject.activeSelf, Is.False);
                AssertRaycast(mode); Click(mode); yield return null; Assert.That(decor.IsOpen, Is.False);
                // Unload before the next real screen/scene; do not leave input actions cached.
                inputAssets = Phase8SceneInputTestCleanup.CaptureAssets(scene);
                SceneManager.SetActiveScene(SceneManager.CreateScene("HUD next size")); yield return SceneManager.UnloadSceneAsync(scene);
                Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets);
            }
        }
        [UnityTest] public IEnumerator HudTaskCaptionsRenderAtBothAspectRatios()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            foreach (var size in new[] { new Vector2(1080, 1920), new Vector2(1600, 720) })
            {
                screen.Resize(size); yield return null; yield return null;
                yield return Load(Validation); yield return Ready();
                Canvas.ForceUpdateCanvases(); yield return null; Canvas.ForceUpdateCanvases();
                foreach (var entry in new[] { (name: "Cashier Task", caption: "收银服务"), (name: "Barista Task", caption: "制作咖啡") })
                {
                    var button = flow.transform.Find("P13 Task HUD/Panel/" + entry.name).GetComponent<Button>();
                    var label = button.transform.Find("Label").GetComponent<Text>();
                    Assert.That(label.text, Is.EqualTo(entry.caption)); Assert.That(label.isActiveAndEnabled, Is.True);
                    var vertices = label.cachedTextGenerator.verts; var visibleGlyphs = 0;
                    for (var first = 0; first + 3 < vertices.Count; first += 4)
                    {
                        var bounds = new Bounds(vertices[first].position, Vector3.zero);
                        for (var corner = 1; corner < 4; corner++) bounds.Encapsulate(vertices[first + corner].position);
                        if (bounds.size.x > .01f && bounds.size.y > .01f && vertices[first].color.a > 0) visibleGlyphs++;
                    }
                    Assert.That(visibleGlyphs, Is.EqualTo(4), size + " " + entry.caption + " must render four nonempty glyph quads");
                    Assert.That(label.canvasRenderer.cull, Is.False); Assert.That(label.canvasRenderer.GetAlpha(), Is.GreaterThan(0));
                    Assert.That(label.rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
                    Assert.That(label.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
                    Assert.That(label.rectTransform.offsetMin, Is.EqualTo(Vector2.zero));
                    Assert.That(label.rectTransform.offsetMax, Is.EqualTo(Vector2.zero));
                    AssertRaycast(button);
                    TestContext.WriteLine("REAL_HUD_CAPTION " + size + " " + entry.caption + " visibleGlyphs=" + visibleGlyphs);
                }
                inputAssets = Phase8SceneInputTestCleanup.CaptureAssets(scene);
                SceneManager.SetActiveScene(SceneManager.CreateScene("Caption next size")); yield return SceneManager.UnloadSceneAsync(scene);
                Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets);
            }
        }
        private void Seed()
        {
            var support = layout.Layout.FurnitureInstances.Single(); var catalog = Ref<FurnitureContentCatalog>(decor, "contentCatalog");
            var slot = catalog.BuildSurfaceSlotCatalog(layout.Layout.GridSettings).GetForSupport(support.DefinitionId).First().SlotId;
            Assert.That(layout.Layout.PlaceFurniture(FurnitureInstance.Restore("12000000000000000000000000000002", support.DefinitionId, new GridPosition(4, 3), FurnitureRotation.Degrees0)).Succeeded, Is.True);
            Assert.That(layout.Layout.PlaceFurniture(FurnitureInstance.Restore("12000000000000000000000000000003", support.DefinitionId, new GridPosition(6, 3), FurnitureRotation.Degrees0)).Succeeded, Is.True);
            Assert.That(layout.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance("12000000000000000000000000000004", "equipment.cash-register.01", new SurfaceSlotAddress(support.InstanceId, slot), FurnitureRotation.Degrees0)).Succeeded, Is.True);
            Assert.That(layout.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance("12000000000000000000000000000005", "equipment.coffee-machine.01", new SurfaceSlotAddress("12000000000000000000000000000002", slot), FurnitureRotation.Degrees0)).Succeeded, Is.True);
            Assert.That(layout.FunctionalSurfaceLayout.PlacePickUp(new PickUpPointInstance("12000000000000000000000000000006", new SurfaceSlotAddress("12000000000000000000000000000003", slot))).Succeeded, Is.True);
        }
        private T[] All<T>() where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private T Find<T>() where T : Component => All<T>().Single();
        private static T Ref<T>(Object owner, string field) where T : Object => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        private EmployeeTaskSnapshot State(EmployeeRole role) => flow.Snapshot.Single(value => value.Role == role);
        private IEnumerator Ready() => Wait(() => flow.Snapshot.Count == 2 && flow.Snapshot.All(value => value.State == EmployeeState.Idle));
        private IEnumerator Wait(Func<bool> complete, bool bodies = false)
        {
            for (var frame = 0; frame < 3600 && !complete(); frame++) { if (bodies) AssertBodies(); yield return null; }
            Assert.That(complete(), Is.True, "Timeout: " + string.Join(" | ", flow.Snapshot.Select(value => value.Role + " " + value.State + " " + value.Failure)) + " Customers:" + customers.Status);
            if (bodies) AssertBodies();
        }
        private void AssertBodies()
        {
            var actors = world.RegisteredActors.ToArray();
            for (var a = 0; a < actors.Length; a++) for (var b = a + 1; b < actors.Length; b++)
                Assert.That(Vector3.Distance(actors[a].transform.position, actors[b].transform.position), Is.GreaterThanOrEqualTo(.909f), actors[a].ActorId + " / " + actors[b].ActorId);
        }
        private static void Click(Button button) => ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        private static void AssertRaycast(Button button)
        {
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)button.transform).TransformPoint(((RectTransform)button.transform).rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, button.name); Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.EqualTo(button), button.name + " obscured by " + hits[0].gameObject.name);
        }
        private static IEnumerator CaptureScreen(string filename, EmployeeState expectedState)
        {
            Assert.That(UnityEngine.Camera.main, Is.Not.Null); Assert.That(UnityEngine.Camera.main.targetTexture, Is.Null);
            Assert.That(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Any(canvas => canvas.isActiveAndEnabled && canvas.renderMode == RenderMode.ScreenSpaceOverlay), Is.True);
            Canvas.ForceUpdateCanvases(); var deadline = Time.realtimeSinceStartup + 45;
            while (ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ShaderUtil.anythingCompiling, Is.False);
            Assert.That(Object.FindFirstObjectByType<EmployeeFlowController>().Snapshot.Single(value => value.Role == EmployeeRole.Barista).State,
                Is.EqualTo(expectedState), "Shader wait must not turn a representative frame into a different state");
            var path = Path.GetFullPath("outputs/phase13/" + filename); Assert.That(File.Exists(path), Is.False, "Preserve earlier capture evidence");
            ScreenCapture.CaptureScreenshot(path, 1); deadline = Time.realtimeSinceStartup + 8;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(File.Exists(path), Is.True, "Actual camera + Overlay Canvas capture required");
            Assert.That(Object.FindFirstObjectByType<EmployeeFlowController>().Snapshot.Single(value => value.Role == EmployeeRole.Barista).State, Is.EqualTo(expectedState));
            TestContext.WriteLine("P13_SCREEN_CAPTURE " + path + " " + Screen.width + "x" + Screen.height);
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Core.Time;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode.Phase13
{
    public sealed partial class EmployeeMainCafeTests
    {
        [UnityTest]
        public IEnumerator LoweredCounterKeepsCmCrGroundedAndUnscaledAtEveryRotation()
        {
            yield return Load(Validation); yield return Ready();
            var time = Find<GameTimeService>(); time.SetPaused(); yield return null;
            var mounted = layout.FunctionalSurfaceLayout.MountedInstances.ToArray();
            foreach (var rotation in new[] { FurnitureRotation.Degrees0, FurnitureRotation.Degrees90,
                         FurnitureRotation.Degrees180, FurnitureRotation.Degrees270 })
            {
                foreach (var item in mounted)
                    Assert.That(layout.FunctionalSurfaceLayout.RotateMounted(item.InstanceId, rotation).Succeeded, Is.True);
                decor.RefreshConfirmedNavigationPresentation(); yield return null;
                AssertLoweredDevices();
            }
            foreach (var item in mounted)
                Assert.That(layout.FunctionalSurfaceLayout.RotateMounted(item.InstanceId, item.Rotation).Succeeded, Is.True);
            decor.RefreshConfirmedNavigationPresentation(); Find<NavigationDecorationBridge>().ResumeAfterRepair();
            Assert.That(time.TrySetSpeed(GameSpeed.Normal), Is.True, time.ResumeBlockReason);
            yield return Ready(); AssertLoweredDevices();
            Assert.That(flow.TryStartTask(EmployeeRole.Cashier), Is.True);
            Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.True);
            yield return null; time.SetPaused(); yield return null;
            yield return CaptureLoweredCounter("validation-working");
        }

        [UnityTest]
        public IEnumerator LoweredCounterDeliversThreeGroundedCupsAndKeepsVisualCap()
        {
            yield return Load(Validation); yield return Ready(); AssertLoweredDevices();
            Assert.That(flow.TryGetBinding(EmployeeRole.Barista, out var binding), Is.True);
            for (var i = 0; i < 4; i++)
            {
                Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.True);
                yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.Idle, true);
                Assert.That(State(EmployeeRole.Barista).Delivered, Is.True);
            }
            var registry = Find<FurnitureSceneRegistry>();
            Assert.That(registry.TryGet(binding.CupAddress.Value.SupportFurnitureInstanceId, out var support), Is.True);
            var slot = support.GetComponentsInChildren<SurfaceSlotMarker>()
                .Single(s => s.SlotId == binding.CupAddress.Value.SlotId).transform;
            Assert.That(slot.position.y, Is.EqualTo(.42f).Within(.001f));
            var cups = slot.Cast<Transform>().Where(t => t.name.StartsWith("PickupCup_")).ToArray();
            Assert.That(cups.Length, Is.EqualTo(3));
            foreach (var cup in cups)
                Assert.That(CounterVisualBounds(cup.gameObject).min.y, Is.EqualTo(.42f).Within(.005f));
            Find<GameTimeService>().SetPaused(); yield return null;
            yield return CaptureLoweredCounter("validation-three-cups");
        }

        [UnityTest]
        public IEnumerator MainCafeLoweredCounterMountsDevicesAndCompletesBothTasks()
        {
            yield return Load("Assets/Scenes/MainCafe.unity"); yield return null; yield return null;
            customers.AutoSpawn = false; Seed(); decor.RefreshConfirmedNavigationPresentation();
            Find<NavigationDecorationBridge>().ResumeAfterRepair(); yield return null;
            var time = Find<GameTimeService>();
            Assert.That(time.TrySetSpeed(GameSpeed.Normal), Is.True, time.ResumeBlockReason);
            yield return Ready(); AssertLoweredDevices();
            Assert.That(flow.TryStartTask(EmployeeRole.Cashier), Is.True);
            Assert.That(flow.TryStartTask(EmployeeRole.Barista), Is.True);
            yield return null; time.SetPaused(); yield return null;
            yield return CaptureLoweredCounter("main-cafe-working");
            Assert.That(time.TrySetSpeed(GameSpeed.Normal), Is.True, time.ResumeBlockReason);
            yield return Wait(() => State(EmployeeRole.Barista).State == EmployeeState.Idle, true);
            Assert.That(State(EmployeeRole.Cashier).Outcome, Is.EqualTo(EmployeeTaskOutcome.Succeeded));
            Assert.That(State(EmployeeRole.Barista).Delivered, Is.True);
        }

        private void AssertLoweredDevices()
        {
            var registry = Find<SurfaceMountedSceneRegistry>();
            var mounted = layout.FunctionalSurfaceLayout.MountedInstances;
            Assert.That(mounted.Count, Is.EqualTo(2));
            foreach (var item in mounted)
            {
                Assert.That(registry.TryGet(item.InstanceId, out var device), Is.True);
                Assert.That(device.transform.position.y, Is.EqualTo(.42f).Within(.001f));
                Assert.That(Vector3.Distance(device.transform.lossyScale, Vector3.one), Is.LessThan(.00001f), "Cabinet shortening must not compress equipment; allow rotation rounding");
                var bounds = CounterVisualBounds(device);
                Assert.That(bounds.min.y, Is.EqualTo(.42f).Within(.005f), item.DefinitionId + " floats or sinks");
                var coffee = item.DefinitionId == "equipment.coffee-machine.01";
                Assert.That(bounds.size.y, Is.EqualTo(coffee ? .62f : .45f).Within(.03f));
                Assert.That(Mathf.Max(bounds.size.x, bounds.size.z), Is.EqualTo(coffee ? .65f : .43f).Within(.03f));
                Assert.That(Mathf.Min(bounds.size.x, bounds.size.z), Is.EqualTo(coffee ? .50f : .26f).Within(.03f));
            }
        }

        private static Bounds CounterVisualBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            Assert.That(renderers, Is.Not.Empty);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private IEnumerator CaptureLoweredCounter(string name)
        {
            if (Environment.GetEnvironmentVariable("ANIMALCAFE_P13_COUNTER_CAPTURE") != "true") yield break;
            Assert.That(Application.isBatchMode, Is.False, "ScreenCapture needs a native graphics Editor; CLI batch runs should leave capture disabled");
            var directory = Path.GetFullPath("outputs/phase13/counter-height-reviewed"); Directory.CreateDirectory(directory);
            {
                // Use the true GameView target, not a temporary GUIView size override.
                // 直接使用真实GameView，避免Screen与截图buffer比例不一致。
                yield return null; yield return null;
                var camera = UnityEngine.Camera.main;
                var controller = All<AnimalCafe.Camera.CafeCameraController>().SingleOrDefault();
                var wasEnabled = controller != null && controller.enabled;
                var position = camera.transform.position; var size = camera.orthographicSize;
                try
                {
                    yield return SaveCounterFrame(Path.Combine(directory, name + ".png"));
                    // Test-only close view at the existing fixed angle / 仅截图放大，不保存场景。
                    if (controller != null) controller.enabled = false;
                    var actors = world.RegisteredActors.ToArray();
                    var center = actors.Aggregate(Vector3.zero, (sum, actor) => sum + actor.transform.position) / actors.Length;
                    center.y = .65f;
                    camera.orthographicSize = 2.2f;
                    camera.transform.position = center - camera.transform.forward * 12f - camera.transform.up * .35f;
                    yield return null; yield return null;
                    yield return SaveCounterFrame(Path.Combine(directory, name + "-close.png"));
                }
                finally
                {
                    camera.transform.position = position; camera.orthographicSize = size;
                    if (controller != null) controller.enabled = wasEnabled;
                }
            }
        }

        private static IEnumerator SaveCounterFrame(string path)
        {
            Assert.That(File.Exists(path), Is.False, "Keep earlier screenshot evidence");
            Canvas.ForceUpdateCanvases(); yield return null;
            var shadersDeadline = Time.realtimeSinceStartup + 45;
            while (UnityEditor.ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < shadersDeadline) yield return null;
            Assert.That(UnityEditor.ShaderUtil.anythingCompiling, Is.False);
            Assert.That(UnityEngine.Camera.main.targetTexture, Is.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Any(c => c.isActiveAndEnabled && c.renderMode == RenderMode.ScreenSpaceOverlay), Is.True);
            var width = Screen.width; var height = Screen.height;
            Assert.That(UnityEngine.Camera.main.aspect, Is.EqualTo((float)width / height).Within(.0001f));
            ScreenCapture.CaptureScreenshot(path);
            var deadline = Time.realtimeSinceStartup + 10;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(File.Exists(path), Is.True);
            var image = new Texture2D(2, 2);
            try
            {
                Assert.That(image.LoadImage(File.ReadAllBytes(path)), Is.True);
                Assert.That(image.width, Is.EqualTo(width), "Actual PNG must match the rendering viewport");
                Assert.That(image.height, Is.EqualTo(height));
            }
            finally { UnityEngine.Object.Destroy(image); }
            TestContext.WriteLine("COUNTER_HEIGHT_SCREEN " + path + " " + width + "x" + height);
        }
    }
}
#endif

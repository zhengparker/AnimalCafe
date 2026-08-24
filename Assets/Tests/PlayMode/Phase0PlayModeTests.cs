using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AnimalCafe.Core.Events;
using AnimalCafe.Core.Time;
using AnimalCafe.Diagnostics;
using AnimalCafe.Input;
using CafeCameraController = AnimalCafe.Camera.CafeCameraController;
using CameraSettings = AnimalCafe.Camera.CameraSettings;
using AnimalCafe.Interaction;
using AnimalCafe.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class ScaledTimeTestFixture : MonoBehaviour
    {
        private Vector3 startPoint;
        private Vector3 endPoint;
        private float unitsPerSecond;

        private void Update()
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                endPoint,
                unitsPerSecond * Time.deltaTime);
        }

        public void Configure(
            Vector3 start,
            Vector3 end,
            float movementSpeed)
        {
            startPoint = start;
            endPoint = end;
            unitsPerSecond = movementSpeed;
            ResetToStart();
        }

        public void ResetToStart()
        {
            transform.position = startPoint;
        }
    }

    public sealed class CameraInputTestFixture :
        MonoBehaviour,
        ICameraInputSource
    {
        public CameraInputFrame NextFrame { get; set; }

        public CameraInputFrame ReadFrame()
        {
            var frame = NextFrame;
            NextFrame = default;
            return frame;
        }
    }

    public sealed class Phase0PlayModeTests
    {
        private static readonly string[] LegacyPhase1ObjectNames =
        {
            "Phase1_Runtime",
            "Phase1_Cafe",
            "Phase1_Characters",
            "Phase1_UI",
            "__Phase1SetupOwned",
            "Floor_Main",
            "Floor_LowerWing",
            "Floor_UpperWing",
            "Counter_Main",
            "Counter_Pickup",
            "Counter_Main_Visual",
            "Counter_Pickup_Visual",
            "Counter_Horizontal",
            "Counter_Vertical",
            "CoffeeMachine",
            "CafeStations",
            "CounterQueue_0",
            "CounterQueue_1",
            "CounterQueue_2",
            "PickupSlot_0",
            "PickupSlot_1",
            "Cashier_Cat",
            "Barista_Fox",
            "Customer_Bunny_PrefabSource",
            "CustomerSpawner",
            "Phase1StatusCanvas",
            "CafeStatusPanel"
        };

        [TearDown]
        public void TearDown()
        {
            foreach (var service in Resources.FindObjectsOfTypeAll<GameTimeService>())
            {
                if (service != null && service.gameObject.scene.IsValid())
                {
                    Object.DestroyImmediate(service.gameObject);
                }
            }

            Time.timeScale = 1f;
        }

        [Test]
        public void GameTime_AcceptsOnlySupportedSpeeds()
        {
            var gameObject = new GameObject("GameTimeService");
            var service = gameObject.AddComponent<GameTimeService>();

            Assert.That(service.TrySetSpeed(GameSpeed.Fast), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(2f));

            LogAssert.Expect(LogType.Warning, "[GameTimeService] Unsupported game speed: 3.");
            Assert.That(service.TrySetSpeed((GameSpeed)3), Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(2f));

            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void GameTime_PauseSetsTimeScaleToZero()
        {
            var gameObject = new GameObject("GameTimeService");
            var service = gameObject.AddComponent<GameTimeService>();

            service.SetPaused();

            Assert.That(service.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Assert.That(Time.timeScale, Is.Zero);
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void GameTime_DuplicateCannotChangeTimeOrPublishEvent()
        {
            var ownerObject = new GameObject("GameTimeServiceOwner");
            var duplicateObject = new GameObject("GameTimeServiceDuplicate");
            var owner = ownerObject.AddComponent<GameTimeService>();
            var duplicate = duplicateObject.AddComponent<GameTimeService>();
            var eventCount = 0;

            try
            {
                GameEventBus.GameSpeedChanged += _ => eventCount++;

                Assert.That(owner.TrySetSpeed(GameSpeed.Fast), Is.True);
                Assert.That(eventCount, Is.EqualTo(1));

                LogAssert.Expect(
                    LogType.Warning,
                    "[GameTimeService] Ignored speed change from duplicate instance.");
                Assert.That(duplicate.TrySetSpeed(GameSpeed.Paused), Is.False);
                Assert.That(duplicate.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
                Assert.That(Time.timeScale, Is.EqualTo(2f));
                Assert.That(eventCount, Is.EqualTo(1));
            }
            finally
            {
                GameEventBus.ResetForTests();
                Object.DestroyImmediate(duplicateObject);
                Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void GameTime_DestroyingDuplicateDoesNotAffectOwner()
        {
            var ownerObject = new GameObject("GameTimeServiceOwner");
            var duplicateObject = new GameObject("GameTimeServiceDuplicate");
            var owner = ownerObject.AddComponent<GameTimeService>();
            duplicateObject.AddComponent<GameTimeService>();

            try
            {
                Assert.That(owner.TrySetSpeed(GameSpeed.Fast), Is.True);

                Object.DestroyImmediate(duplicateObject);

                Assert.That(owner.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
                Assert.That(Time.timeScale, Is.EqualTo(2f));
            }
            finally
            {
                GameEventBus.ResetForTests();
                Object.DestroyImmediate(duplicateObject);
                Object.DestroyImmediate(ownerObject);
            }
        }

        [Test]
        public void GameTime_DuplicateDoesNotAutoPromoteAfterOwnerIsDestroyed()
        {
            var ownerObject = new GameObject("GameTimeServiceOwner");
            var duplicateObject = new GameObject("GameTimeServiceDuplicate");
            var owner = ownerObject.AddComponent<GameTimeService>();
            var duplicate = duplicateObject.AddComponent<GameTimeService>();

            try
            {
                Assert.That(owner.TrySetSpeed(GameSpeed.Fast), Is.True);

                Object.DestroyImmediate(ownerObject);

                Assert.That(Time.timeScale, Is.EqualTo(1f));
                LogAssert.Expect(
                    LogType.Warning,
                    "[GameTimeService] Ignored speed change from duplicate instance.");
                Assert.That(duplicate.TrySetSpeed(GameSpeed.Paused), Is.False);
                Assert.That(duplicate.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally
            {
                GameEventBus.ResetForTests();
                Object.DestroyImmediate(duplicateObject);
                Object.DestroyImmediate(ownerObject);
            }
        }

        [TestCase(3f, 6f, true)]
        [TestCase(6f, 6f, true)]
        [TestCase(6.1f, 6f, false)]
        [TestCase(0f, -1f, true)]
        public void MouseInput_TapDependsOnDragDistance(
            float dragDistance,
            float threshold,
            bool expected)
        {
            Assert.That(
                MouseCameraInput.IsTapDistance(dragDistance, threshold),
                Is.EqualTo(expected));
        }

        [Test]
        public void Camera_ClampsPositionAndZoomToConfiguredBounds()
        {
            var cameraObject = new GameObject("TestCamera");
            var unityCamera = cameraObject.AddComponent<UnityEngine.Camera>();
            unityCamera.orthographic = true;
            var controller = cameraObject.AddComponent<CafeCameraController>();
            var settings = ScriptableObject.CreateInstance<CameraSettings>();
            settings.PositionMin = new Vector2(-5f, -4f);
            settings.PositionMax = new Vector2(5f, 4f);
            settings.MinOrthographicSize = 4f;
            settings.MaxOrthographicSize = 10f;
            controller.Configure(unityCamera, settings, null);

            cameraObject.transform.position = new Vector3(12f, 15f, -9f);
            unityCamera.orthographicSize = 15f;
            controller.ClampToBounds();

            Assert.That(cameraObject.transform.position.x, Is.EqualTo(5f));
            Assert.That(cameraObject.transform.position.y, Is.EqualTo(15f));
            Assert.That(cameraObject.transform.position.z, Is.EqualTo(-4f));
            Assert.That(unityCamera.orthographicSize, Is.EqualTo(10f));

            Object.DestroyImmediate(settings);
            Object.DestroyImmediate(cameraObject);
        }

        [TestCase(0.1f)]
        [TestCase(120f)]
        public void Camera_ZoomTreatsAnyWheelEventAsOneStep(float wheelDelta)
        {
            var cameraObject = new GameObject("ZoomCamera");
            var unityCamera = cameraObject.AddComponent<UnityEngine.Camera>();
            unityCamera.orthographic = true;
            unityCamera.orthographicSize = 7f;
            var controller = cameraObject.AddComponent<CafeCameraController>();
            var settings = ScriptableObject.CreateInstance<CameraSettings>();
            settings.ZoomSpeed = 0.75f;
            settings.MinOrthographicSize = 4f;
            settings.MaxOrthographicSize = 12f;
            controller.Configure(unityCamera, settings, null);

            controller.ApplyZoom(wheelDelta);

            Assert.That(unityCamera.orthographicSize, Is.EqualTo(6.25f).Within(0.001f));
            Object.DestroyImmediate(settings);
            Object.DestroyImmediate(cameraObject);
        }

        [Test]
        public void ColorSelectable_SelectAndDeselectUpdateState()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var material = AssignPortableTestMaterial(cube);
            try
            {
                var selectable = cube.AddComponent<ColorSelectable>();

                selectable.Select();
                Assert.That(selectable.IsSelected, Is.True);

                selectable.Deselect();
                Assert.That(selectable.IsSelected, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
            }
        }

        [Test]
        public void ColorSelectable_ConfigureBindsRendererAndSupportsSelection()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var renderer = cube.GetComponent<MeshRenderer>();
            var material = AssignPortableTestMaterial(cube);
            var selectable = cube.AddComponent<ColorSelectable>();

            try
            {
                selectable.Configure(renderer);

                var targetRendererField = typeof(ColorSelectable).GetField(
                    "targetRenderer",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(targetRendererField, Is.Not.Null);
                Assert.That(
                    targetRendererField.GetValue(selectable),
                    Is.SameAs(renderer));

                selectable.Select();
                Assert.That(selectable.IsSelected, Is.True);
                selectable.Deselect();
                Assert.That(selectable.IsSelected, Is.False);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(cube);
            }
        }

        [UnityTest]
        public IEnumerator ColorSelectable_MissingMaterialWarnsOnceAndRecovers()
        {
            var gameObject = new GameObject("MissingMaterial");
            var renderer = gameObject.AddComponent<MeshRenderer>();
            var selectable = gameObject.AddComponent<ColorSelectable>();
            Material workingMaterial = null;

            try
            {
                LogAssert.Expect(
                    LogType.Warning,
                    "[ColorSelectable] Renderer material must expose _BaseColor or _Color.");
                selectable.Select();
                yield return null;

                Assert.That(selectable.IsSelected, Is.False);
                Assert.That(selectable.enabled, Is.True);

                selectable.Select();
                workingMaterial = new Material(
                    Shader.Find("Universal Render Pipeline/Lit"));
                renderer.sharedMaterial = workingMaterial;
                selectable.Select();

                Assert.That(selectable.IsSelected, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(workingMaterial);
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void Interaction_SelectsSwitchesAndClearsSelection()
        {
            var cameraObject = new GameObject("InteractionCamera");
            var unityCamera = cameraObject.AddComponent<UnityEngine.Camera>();
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var interactionObject = new GameObject("Interaction");
            var interaction = interactionObject.AddComponent<SceneInteractionController>();
            interaction.Configure(unityCamera, null);

            var cubeA = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var materialA = AssignPortableTestMaterial(cubeA);
            cubeA.transform.position = new Vector3(-1f, 0f, 0f);
            var selectableA = cubeA.AddComponent<ColorSelectable>();
            var cubeB = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var materialB = AssignPortableTestMaterial(cubeB);
            cubeB.transform.position = new Vector3(1f, 0f, 0f);
            var selectableB = cubeB.AddComponent<ColorSelectable>();
            Physics.SyncTransforms();

            interaction.TrySelectAt(unityCamera.WorldToScreenPoint(cubeA.transform.position));
            Assert.That(interaction.CurrentSelection, Is.SameAs(selectableA));
            Assert.That(selectableA.IsSelected, Is.True);

            interaction.TrySelectAt(unityCamera.WorldToScreenPoint(cubeB.transform.position));
            Assert.That(selectableA.IsSelected, Is.False);
            Assert.That(selectableB.IsSelected, Is.True);

            interaction.TrySelectAt(new Vector2(-1000f, -1000f));
            Assert.That(interaction.CurrentSelection, Is.Null);
            Assert.That(selectableB.IsSelected, Is.False);

            Object.DestroyImmediate(cubeA);
            Object.DestroyImmediate(cubeB);
            Object.DestroyImmediate(materialA);
            Object.DestroyImmediate(materialB);
            Object.DestroyImmediate(interactionObject);
            Object.DestroyImmediate(cameraObject);
        }

        [UnityTest]
        public IEnumerator Interaction_DisabledSelectionClearsOnce()
        {
            var fixture = CreateInteractionFixture();
            var events = new List<SelectionChangedEvent>();
            GameEventBus.SelectionChanged += events.Add;

            try
            {
                fixture.Interaction.TrySelectAt(
                    fixture.Camera.WorldToScreenPoint(
                        fixture.Selectable.transform.position));
                events.Clear();

                fixture.Selectable.enabled = false;
                yield return null;
                yield return null;

                Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
                Assert.That(events, Has.Count.EqualTo(1));
                Assert.That(events[0].Previous, Is.SameAs(fixture.Selectable));
                Assert.That(events[0].Current, Is.Null);
            }
            finally
            {
                GameEventBus.ResetForTests();
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Interaction_InactiveGameObjectClearsSelectionOnce()
        {
            var fixture = CreateInteractionFixture();
            var events = new List<SelectionChangedEvent>();
            GameEventBus.SelectionChanged += events.Add;

            try
            {
                fixture.Interaction.TrySelectAt(
                    fixture.Camera.WorldToScreenPoint(
                        fixture.Selectable.transform.position));
                events.Clear();

                fixture.Selectable.gameObject.SetActive(false);
                yield return null;
                yield return null;

                Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
                Assert.That(events, Has.Count.EqualTo(1));
                Assert.That(events[0].Previous, Is.SameAs(fixture.Selectable));
                Assert.That(events[0].Current, Is.Null);
            }
            finally
            {
                GameEventBus.ResetForTests();
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Interaction_DestroyedSelectionClearsWithoutException()
        {
            var fixture = CreateInteractionFixture();
            var events = new List<SelectionChangedEvent>();
            GameEventBus.SelectionChanged += events.Add;

            try
            {
                fixture.Interaction.TrySelectAt(
                    fixture.Camera.WorldToScreenPoint(
                        fixture.Selectable.transform.position));
                var destroyedSelection = fixture.Selectable;
                events.Clear();

                Object.Destroy(fixture.Selectable.gameObject);
                yield return null;
                yield return null;

                Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
                Assert.That(events, Has.Count.EqualTo(1));
                Assert.That(
                    events[0].Previous,
                    Is.SameAs(destroyedSelection));
                Assert.That(events[0].Current, Is.Null);
            }
            finally
            {
                GameEventBus.ResetForTests();
                fixture.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator Interaction_ReenabledSelectableCanBeSelectedAgain()
        {
            var fixture = CreateInteractionFixture();

            try
            {
                var screenPosition = fixture.Camera.WorldToScreenPoint(
                    fixture.Selectable.transform.position);
                fixture.Interaction.TrySelectAt(screenPosition);

                fixture.Selectable.enabled = false;
                yield return null;
                Assert.That(fixture.Interaction.CurrentSelection, Is.Null);

                fixture.Selectable.enabled = true;
                Physics.SyncTransforms();
                fixture.Interaction.TrySelectAt(screenPosition);

                Assert.That(
                    fixture.Interaction.CurrentSelection,
                    Is.SameAs(fixture.Selectable));
                Assert.That(fixture.Selectable.IsSelected, Is.True);
            }
            finally
            {
                GameEventBus.ResetForTests();
                fixture.Dispose();
            }
        }

        [Test]
        public void LegacyPhase1Predicate_DetectsInactiveExactNameFixture()
        {
            var legacyRoot = new GameObject("Phase1_Runtime");
            legacyRoot.SetActive(false);

            try
            {
                Assert.That(
                    SceneContainsExactNamedObject(
                        legacyRoot.scene,
                        "Phase1_Runtime"),
                    Is.True);
                Assert.That(
                    SceneContainsExactNamedObject(
                        legacyRoot.scene,
                        "Phase1_Runtime_Copy"),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(legacyRoot);
            }
        }

        [Test]
        public void NavMeshPredicate_DistinguishesEmptySettingsFromBakedData()
        {
            Assert.That(
                HasBakedOrRuntimeNavMesh(default),
                Is.False);

            var bakedData = new NavMeshTriangulation
            {
                vertices = new[]
                {
                    Vector3.zero,
                    Vector3.right,
                    Vector3.forward
                },
                indices = new[] { 0, 1, 2 },
                areas = new[] { 0 }
            };

            Assert.That(
                HasBakedOrRuntimeNavMesh(bakedData),
                Is.True);
        }

        [UnityTest]
        public IEnumerator TimeMover_FastMovesFartherThanNormal()
        {
            var serviceObject = new GameObject("GameTimeService");
            var service = serviceObject.AddComponent<GameTimeService>();
            var moverObject = new GameObject("ScaledTimeFixture");
            var mover = moverObject.AddComponent<ScaledTimeTestFixture>();
            mover.Configure(Vector3.zero, Vector3.right * 10f, 1f);

            service.SetNormal();
            mover.ResetToStart();
            yield return new WaitForSecondsRealtime(0.25f);
            var normalDistance = mover.transform.position.x;

            service.SetFast();
            mover.ResetToStart();
            yield return new WaitForSecondsRealtime(0.25f);
            var fastDistance = mover.transform.position.x;

            Assert.That(fastDistance, Is.GreaterThan(normalDistance * 1.7f));
            Object.DestroyImmediate(serviceObject);
            Object.DestroyImmediate(moverObject);
        }

        [UnityTest]
        public IEnumerator TimeMover_PauseStopsMovement()
        {
            var serviceObject = new GameObject("GameTimeService");
            var service = serviceObject.AddComponent<GameTimeService>();
            var moverObject = new GameObject("ScaledTimeFixture");
            var mover = moverObject.AddComponent<ScaledTimeTestFixture>();
            mover.Configure(Vector3.zero, Vector3.right * 10f, 1f);

            service.SetPaused();
            var start = mover.transform.position;
            yield return new WaitForSecondsRealtime(0.15f);

            Assert.That(
                Vector3.Distance(start, mover.transform.position),
                Is.LessThan(0.001f));
            Object.DestroyImmediate(serviceObject);
            Object.DestroyImmediate(moverObject);
        }

        [Test]
        public void TimeControlPanel_ButtonsSetExpectedSpeeds()
        {
            var serviceObject = new GameObject("GameTimeService");
            var service = serviceObject.AddComponent<GameTimeService>();
            var panelObject = new GameObject("TimeControlPanel");
            var panel = panelObject.AddComponent<TimeControlPanel>();
            var pause = new GameObject("Pause").AddComponent<Button>();
            var normal = new GameObject("Normal").AddComponent<Button>();
            var fast = new GameObject("Fast").AddComponent<Button>();
            panel.Configure(service, pause, normal, fast);

            pause.onClick.Invoke();
            Assert.That(service.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            normal.onClick.Invoke();
            Assert.That(service.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
            fast.onClick.Invoke();
            Assert.That(service.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
            pause.onClick.Invoke();
            Assert.That(service.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            pause.onClick.Invoke();
            Assert.That(service.CurrentSpeed, Is.EqualTo(GameSpeed.Fast),
                "Resume must restore the speed that was active before Pause.");

            Object.DestroyImmediate(pause.gameObject);
            Object.DestroyImmediate(normal.gameObject);
            Object.DestroyImmediate(fast.gameObject);
            Object.DestroyImmediate(panelObject);
            Object.DestroyImmediate(serviceObject);
        }

        [UnityTest]
        public IEnumerator MainCafe_LoadsWithRequiredPhase0Objects()
        {
            yield return SceneManager.LoadSceneAsync("MainCafe");
            yield return null;

            var runtimeRoot = GameObject.Find("Phase0_Runtime");
            var uiRoot = GameObject.Find("UI Root");
            var environmentRoot = GameObject.Find("P4_Environment");
            var decorationRoot = GameObject.Find("Phase6_DecorationRuntime");

            Assert.That(CountLoadedSceneObjects("Phase0_Runtime"), Is.EqualTo(1));
            Assert.That(
                CountLoadedSceneObjects("TEMP_P4_ManualReviewFixtures_DELETE_LATER"),
                Is.Zero);
            Assert.That(environmentRoot, Is.Not.Null);
            Assert.That(environmentRoot.scene,
                Is.EqualTo(SceneManager.GetSceneByName("MainCafe")));
            Assert.That(environmentRoot.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(environmentRoot.transform.rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(environmentRoot.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(environmentRoot.transform.Find("P4_Floor_8x8"), Is.Not.Null);
            Assert.That(environmentRoot.transform.Find("P4_Wall_BackLeft"), Is.Not.Null);
            Assert.That(environmentRoot.transform.Find("P4_Wall_BackRight"), Is.Not.Null);
            Assert.That(environmentRoot.transform.Find("P4_Entrance"), Is.Not.Null);
            Assert.That(
                environmentRoot.transform.Find(
                    "P4_Wall_BackRight/P4_Window_BackRight_C3_R0"),
                Is.Not.Null);
            Assert.That(decorationRoot, Is.Not.Null);
            Assert.That(decorationRoot.transform.Find("DecorationSpaceRoot"), Is.Not.Null);
            Assert.That(GameObject.Find("DecorationModeButton"), Is.Not.Null);

            Assert.That(runtimeRoot, Is.Not.Null);
            Assert.That(runtimeRoot.GetComponent<GameTimeService>(), Is.Not.Null);
            Assert.That(runtimeRoot.GetComponent<MouseCameraInput>(), Is.Not.Null);
            Assert.That(runtimeRoot.GetComponent<CafeCameraController>(), Is.Not.Null);
            Assert.That(
                runtimeRoot.GetComponent<SceneInteractionController>(),
                Is.Not.Null);
            Assert.That(GameObject.Find("Phase0_Demo"), Is.Null);
            Assert.That(GameObject.Find("Selectable_Blue"), Is.Null);
            Assert.That(GameObject.Find("Selectable_Green"), Is.Null);
            Assert.That(GameObject.Find("Time_Test_Mover"), Is.Null);
            Assert.That(GameObject.Find("CafeFloor"), Is.Null);
            Assert.That(CountLoadedSceneObjects("Phase0_TimeControls"), Is.Zero);
            Assert.That(CountLoadedSceneObjects("UI Root"), Is.EqualTo(1));
            Assert.That(uiRoot, Is.Not.Null);
            Assert.That(
                uiRoot.GetComponentInChildren<TimeControlPanel>(true),
                Is.Not.Null);
            Assert.That(CountLoadedSceneObjects("EventSystem"), Is.EqualTo(1));

            var mainCafeScene = SceneManager.GetSceneByName("MainCafe");
            foreach (var legacyObjectName in LegacyPhase1ObjectNames)
            {
                Assert.That(
                    SceneContainsExactNamedObject(
                        mainCafeScene,
                        legacyObjectName),
                    Is.False,
                    $"MainCafe contains legacy Phase 1 object '{legacyObjectName}'.");
            }

            Assert.That(
                HasBakedOrRuntimeNavMesh(NavMesh.CalculateTriangulation()),
                Is.False,
                "MainCafe contains baked or runtime NavMesh data.");

            var mainCamera = GameObject.Find("Main Camera").GetComponent<UnityEngine.Camera>();
            var cameraController = runtimeRoot.GetComponent<CafeCameraController>();
            Assert.That(
                Quaternion.Angle(
                    mainCamera.transform.rotation,
                    Quaternion.Euler(35.264f, 45f, 0f)),
                Is.LessThan(0.1f));
            cameraController.ApplyPan(new Vector2(0f, 10000f));
            Assert.That(mainCamera.transform.position.z, Is.EqualTo(-12f).Within(0.001f));
            cameraController.ApplyPan(new Vector2(0f, -10000f));
            Assert.That(mainCamera.transform.position.z, Is.EqualTo(-8f).Within(0.001f));

            var selectableObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var material = AssignPortableTestMaterial(selectableObject);
            try
            {
                var selectable = selectableObject.AddComponent<ColorSelectable>();
                selectable.Select();

                Assert.That(selectable.IsSelected, Is.True);
                Assert.That(selectable.enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(selectableObject);
            }
        }

        private static Material AssignPortableTestMaterial(GameObject gameObject)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            return material;
        }

        private static int CountLoadedSceneObjects(string objectName)
        {
            var count = 0;
            foreach (var gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (gameObject.scene.IsValid()
                    && gameObject.scene.isLoaded
                    && gameObject.name == objectName)
                {
                    count++;
                }
            }

            return count;
        }

        private static InteractionFixture CreateInteractionFixture()
        {
            var cameraObject = new GameObject("InteractionFixtureCamera");
            var unityCamera = cameraObject.AddComponent<UnityEngine.Camera>();
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var interactionObject = new GameObject("InteractionFixtureController");
            var interaction =
                interactionObject.AddComponent<SceneInteractionController>();
            var inputSource =
                interactionObject.AddComponent<CameraInputTestFixture>();
            interaction.Configure(unityCamera, inputSource);

            var selectableObject =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            selectableObject.name = "InteractionFixtureSelectable";
            var material = AssignPortableTestMaterial(selectableObject);
            var selectable =
                selectableObject.AddComponent<ColorSelectable>();
            Physics.SyncTransforms();

            return new InteractionFixture(
                cameraObject,
                unityCamera,
                interactionObject,
                interaction,
                inputSource,
                selectableObject,
                selectable,
                material);
        }

        private static bool SceneContainsExactNamedObject(
            Scene scene,
            string objectName)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (string.Equals(
                        transform.name,
                        objectName,
                        System.StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasBakedOrRuntimeNavMesh(
            NavMeshTriangulation triangulation)
        {
            return (triangulation.vertices != null
                    && triangulation.vertices.Length > 0)
                || (triangulation.indices != null
                    && triangulation.indices.Length > 0);
        }

        private sealed class InteractionFixture : System.IDisposable
        {
            private readonly GameObject cameraObject;
            private readonly GameObject interactionObject;
            private readonly GameObject selectableObject;
            private readonly Material selectableMaterial;

            public InteractionFixture(
                GameObject cameraObject,
                UnityEngine.Camera camera,
                GameObject interactionObject,
                SceneInteractionController interaction,
                CameraInputTestFixture input,
                GameObject selectableObject,
                ColorSelectable selectable,
                Material selectableMaterial)
            {
                this.cameraObject = cameraObject;
                Camera = camera;
                this.interactionObject = interactionObject;
                Interaction = interaction;
                Input = input;
                this.selectableObject = selectableObject;
                Selectable = selectable;
                this.selectableMaterial = selectableMaterial;
            }

            public UnityEngine.Camera Camera { get; }

            public SceneInteractionController Interaction { get; }

            public CameraInputTestFixture Input { get; }

            public ColorSelectable Selectable { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(selectableObject);
                Object.DestroyImmediate(selectableMaterial);
                Object.DestroyImmediate(interactionObject);
                Object.DestroyImmediate(cameraObject);
            }
        }

    }

    public sealed class RealUiSceneInteractionTests
    {
        [UnityTest]
        public IEnumerator QueueMousePosition_ProcessesAbsoluteStateImmediately()
        {
            using var focusScope = new InputFocusIsolationScope();
            var mouse = AddEnabledMouse();

            try
            {
                Assert.That(mouse.added, Is.True);
                Assert.That(mouse.enabled, Is.True);
                Assert.That(Mouse.current, Is.SameAs(mouse));

                var priorPosition = new Vector2(700f, 500f);
                InputSystem.QueueStateEvent(
                    mouse,
                    new MouseState
                    {
                        position = priorPosition
                    });
                Assert.That(
                    mouse.position.ReadValue(),
                    Is.Not.EqualTo(priorPosition));
                InputSystem.Update();
                Assert.That(
                    mouse.position.ReadValue(),
                    Is.EqualTo(priorPosition));
                yield return null;
                Assert.That(
                    mouse.position.ReadValue(),
                    Is.EqualTo(priorPosition));

                var expectedPosition = new Vector2(20f, 30f);
                QueueMousePosition(mouse, expectedPosition);
                Assert.That(
                    mouse.position.ReadValue(),
                    Is.EqualTo(expectedPosition));
                yield return null;

                Assert.That(
                    mouse.position.ReadValue(),
                    Is.EqualTo(expectedPosition));
            }
            finally
            {
                if (mouse.added)
                {
                    InputSystem.RemoveDevice(mouse);
                }
            }
        }

        [UnityTest]
        public IEnumerator RealUiTapAtEmptyWorldPreservesSelection()
        {
            using var focusScope = new InputFocusIsolationScope();
            var fixture = CreateInteractionFixture();
            var mouse = AddEnabledMouse();
            RealUiPointerTestScope uiScope = null;
            var events = new List<SelectionChangedEvent>();
            GameEventBus.SelectionChanged += events.Add;

            try
            {
                uiScope = new RealUiPointerTestScope(mouse);
                fixture.Interaction.TrySelectAt(
                    fixture.Camera.WorldToScreenPoint(
                        fixture.Selectable.transform.position));
                events.Clear();

                fixture.Selectable.transform.position =
                    new Vector3(1000f, 1000f, 0f);
                Physics.SyncTransforms();

                var uiPosition = new Vector2(
                    Screen.width * 0.5f,
                    Screen.height * 0.5f);
                uiScope.PlaceUiAt(uiPosition);
                QueueMousePosition(mouse, uiPosition);
                yield return null;
                yield return null;

                uiScope.AssertPointerState(uiPosition, true);
                fixture.Input.NextFrame = new CameraInputFrame(
                    Vector2.zero,
                    0f,
                    true,
                    uiPosition);
                yield return null;

                Assert.That(
                    fixture.Interaction.CurrentSelection,
                    Is.SameAs(fixture.Selectable));
                Assert.That(fixture.Selectable.IsSelected, Is.True);
                Assert.That(events, Is.Empty);
            }
            finally
            {
                GameEventBus.SelectionChanged -= events.Add;
                GameEventBus.ResetForTests();
                uiScope?.Dispose();
                fixture.Dispose();
                if (mouse.added)
                {
                    InputSystem.RemoveDevice(mouse);
                }
            }
        }

        [UnityTest]
        public IEnumerator RealUiTapDoesNotSelectWorldObjectBehindUi()
        {
            using var focusScope = new InputFocusIsolationScope();
            var fixture = CreateInteractionFixture();
            var mouse = AddEnabledMouse();
            RealUiPointerTestScope uiScope = null;

            try
            {
                uiScope = new RealUiPointerTestScope(mouse);
                var selectablePosition = fixture.Camera.WorldToScreenPoint(
                    fixture.Selectable.transform.position);
                uiScope.PlaceUiAt(selectablePosition);
                QueueMousePosition(mouse, selectablePosition);
                yield return null;
                yield return null;

                uiScope.AssertPointerState(selectablePosition, true);
                fixture.Input.NextFrame = new CameraInputFrame(
                    Vector2.zero,
                    0f,
                    true,
                    selectablePosition);
                yield return null;

                Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
                Assert.That(fixture.Selectable.IsSelected, Is.False);
            }
            finally
            {
                GameEventBus.ResetForTests();
                uiScope?.Dispose();
                fixture.Dispose();
                if (mouse.added)
                {
                    InputSystem.RemoveDevice(mouse);
                }
            }
        }

        [UnityTest]
        public IEnumerator EventSystemTapOutsideUiStillSelectsWorldObject()
        {
            using var focusScope = new InputFocusIsolationScope();
            var fixture = CreateInteractionFixture();
            var mouse = AddEnabledMouse();
            RealUiPointerTestScope uiScope = null;

            try
            {
                uiScope = new RealUiPointerTestScope(mouse);
                var selectablePosition = fixture.Camera.WorldToScreenPoint(
                    fixture.Selectable.transform.position);
                uiScope.PlaceUiAt(new Vector2(40f, 40f));
                QueueMousePosition(mouse, selectablePosition);
                yield return null;
                yield return null;

                uiScope.AssertPointerState(selectablePosition, false);
                fixture.Input.NextFrame = new CameraInputFrame(
                    Vector2.zero,
                    0f,
                    true,
                    selectablePosition);
                yield return null;

                Assert.That(
                    fixture.Interaction.CurrentSelection,
                    Is.SameAs(fixture.Selectable));
                Assert.That(fixture.Selectable.IsSelected, Is.True);
            }
            finally
            {
                GameEventBus.ResetForTests();
                uiScope?.Dispose();
                fixture.Dispose();
                if (mouse.added)
                {
                    InputSystem.RemoveDevice(mouse);
                }
            }
        }

        [UnityTest]
        public IEnumerator NoEventSystemTapStillSelectsWorldObject()
        {
            var fixture = CreateInteractionFixture();
            ExistingEventSystemIsolationScope eventSystemIsolation = null;

            try
            {
                eventSystemIsolation = new ExistingEventSystemIsolationScope();
                Assert.That(EventSystem.current, Is.Null);
                fixture.Input.NextFrame = new CameraInputFrame(
                    Vector2.zero,
                    0f,
                    true,
                    fixture.Camera.WorldToScreenPoint(
                        fixture.Selectable.transform.position));
                yield return null;

                Assert.That(
                    fixture.Interaction.CurrentSelection,
                    Is.SameAs(fixture.Selectable));
                Assert.That(fixture.Selectable.IsSelected, Is.True);
            }
            finally
            {
                GameEventBus.ResetForTests();
                eventSystemIsolation?.Dispose();
                fixture.Dispose();
            }
        }

        private static InteractionFixture CreateInteractionFixture()
        {
            var cameraObject = new GameObject("RealUiInteractionFixtureCamera");
            var unityCamera = cameraObject.AddComponent<UnityEngine.Camera>();
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var interactionObject =
                new GameObject("RealUiInteractionFixtureController");
            var interaction =
                interactionObject.AddComponent<SceneInteractionController>();
            var inputSource =
                interactionObject.AddComponent<CameraInputTestFixture>();
            interaction.Configure(unityCamera, inputSource);

            var selectableObject =
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            selectableObject.name = "RealUiInteractionFixtureSelectable";
            var material = AssignPortableTestMaterial(selectableObject);
            var selectable =
                selectableObject.AddComponent<ColorSelectable>();
            Physics.SyncTransforms();

            return new InteractionFixture(
                cameraObject,
                unityCamera,
                interactionObject,
                interaction,
                inputSource,
                selectableObject,
                selectable,
                material);
        }

        private static Material AssignPortableTestMaterial(GameObject gameObject)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            gameObject.GetComponent<Renderer>().sharedMaterial = material;
            return material;
        }

        private static void QueueMousePosition(
            Mouse mouse,
            Vector2 position)
        {
            InputSystem.QueueStateEvent(
                mouse,
                new MouseState
                {
                    position = position
                });
            InputSystem.Update();
        }

        private static Mouse AddEnabledMouse()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            if (!mouse.enabled)
            {
                InputSystem.EnableDevice(mouse);
            }

            return mouse;
        }

        private sealed class InputFocusIsolationScope : System.IDisposable
        {
            private readonly InputSettings.BackgroundBehavior
                originalBackgroundBehavior;
            private readonly InputSettings.EditorInputBehaviorInPlayMode
                originalEditorInputBehavior;
            private readonly bool originalRunInBackground;

            public InputFocusIsolationScope()
            {
                originalBackgroundBehavior =
                    InputSystem.settings.backgroundBehavior;
                originalEditorInputBehavior =
                    InputSystem.settings.editorInputBehaviorInPlayMode;
                originalRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior =
                    InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode
                        .AllDeviceInputAlwaysGoesToGameView;
            }

            public void Dispose()
            {
                InputSystem.settings.backgroundBehavior =
                    originalBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    originalEditorInputBehavior;
                Application.runInBackground = originalRunInBackground;
            }
        }

        private sealed class InteractionFixture : System.IDisposable
        {
            private readonly GameObject cameraObject;
            private readonly GameObject interactionObject;
            private readonly GameObject selectableObject;
            private readonly Material selectableMaterial;

            public InteractionFixture(
                GameObject cameraObject,
                UnityEngine.Camera camera,
                GameObject interactionObject,
                SceneInteractionController interaction,
                CameraInputTestFixture input,
                GameObject selectableObject,
                ColorSelectable selectable,
                Material selectableMaterial)
            {
                this.cameraObject = cameraObject;
                Camera = camera;
                this.interactionObject = interactionObject;
                Interaction = interaction;
                Input = input;
                this.selectableObject = selectableObject;
                Selectable = selectable;
                this.selectableMaterial = selectableMaterial;
            }

            public UnityEngine.Camera Camera { get; }

            public SceneInteractionController Interaction { get; }

            public CameraInputTestFixture Input { get; }

            public ColorSelectable Selectable { get; }

            public void Dispose()
            {
                Object.DestroyImmediate(selectableObject);
                Object.DestroyImmediate(selectableMaterial);
                Object.DestroyImmediate(interactionObject);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private sealed class RealUiPointerTestScope : System.IDisposable
        {
            private readonly GameObject canvasObject;
            private readonly GameObject eventSystemObject;
            private readonly List<GraphicRaycaster> disabledGraphicRaycasters = new();
            private readonly ExistingEventSystemIsolationScope
                eventSystemIsolation;
            private readonly Mouse mouse;
            private readonly RectTransform uiRect;

            public RealUiPointerTestScope(Mouse virtualMouse)
            {
                mouse = virtualMouse;
                eventSystemIsolation =
                    new ExistingEventSystemIsolationScope();
                DisableExistingGraphicRaycasters();

                canvasObject = new GameObject(
                    "RealUiPointerCanvasFixture",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(GraphicRaycaster));
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;

                var uiObject = new GameObject(
                    "RealUiPointerImageFixture",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                uiRect = uiObject.GetComponent<RectTransform>();
                uiRect.SetParent(canvasObject.transform, false);
                uiRect.anchorMin = Vector2.zero;
                uiRect.anchorMax = Vector2.zero;
                uiRect.pivot = new Vector2(0.5f, 0.5f);
                uiRect.sizeDelta = new Vector2(80f, 80f);
                uiObject.GetComponent<Image>().color =
                    new Color(0.2f, 0.6f, 0.9f, 1f);

                eventSystemObject =
                    new GameObject("RealUiPointerEventSystemFixture");
                eventSystemObject.SetActive(false);
                EventSystem =
                    eventSystemObject.AddComponent<EventSystem>();
                InputModule =
                    eventSystemObject.AddComponent<InputSystemUIInputModule>();
                InputModule.UnassignActions();
                InputModule.AssignDefaultActions();
                eventSystemObject.SetActive(true);
            }

            public EventSystem EventSystem { get; }

            public InputSystemUIInputModule InputModule { get; }

            public void PlaceUiAt(Vector2 screenPosition)
            {
                uiRect.anchoredPosition = screenPosition;
                Canvas.ForceUpdateCanvases();
            }

            public void AssertPointerState(
                Vector2 expectedPosition,
                bool expectedOverUi)
            {
                Assert.That(
                    EventSystem.current,
                    Is.SameAs(EventSystem));
                Assert.That(
                    EventSystem.currentInputModule,
                    Is.SameAs(InputModule));
                Assert.That(InputModule.actionsAsset, Is.Not.Null);
                Assert.That(InputModule.point, Is.Not.Null);
                Assert.That(InputModule.point.action, Is.Not.Null);
                Assert.That(InputModule.point.action.enabled, Is.True);

                var pointUsesVirtualMouse = false;
                foreach (var control in InputModule.point.action.controls)
                {
                    if (control.device == mouse)
                    {
                        pointUsesVirtualMouse = true;
                        break;
                    }
                }

                Assert.That(pointUsesVirtualMouse, Is.True);
                Assert.That(
                    Vector2.Distance(
                        mouse.position.ReadValue(),
                        expectedPosition),
                    Is.LessThan(0.01f));
                Assert.That(
                    Vector2.Distance(
                        InputModule.point.action.ReadValue<Vector2>(),
                        expectedPosition),
                    Is.LessThan(0.01f));

                var pointerEventData = new PointerEventData(EventSystem)
                {
                    position = expectedPosition
                };
                var raycastResults = new List<RaycastResult>();
                EventSystem.RaycastAll(pointerEventData, raycastResults);
                Assert.That(
                    raycastResults.Exists(
                        result => result.gameObject == uiRect.gameObject),
                    Is.EqualTo(expectedOverUi));
                Assert.That(
                    EventSystem.IsPointerOverGameObject(),
                    Is.EqualTo(expectedOverUi));
            }

            public void Dispose()
            {
                if (InputModule != null)
                {
                    InputModule.UnassignActions();
                }

                Object.DestroyImmediate(eventSystemObject);
                Object.DestroyImmediate(canvasObject);
                foreach (var raycaster in disabledGraphicRaycasters)
                {
                    if (raycaster != null)
                    {
                        raycaster.enabled = true;
                    }
                }
                eventSystemIsolation.Dispose();
            }

            private void DisableExistingGraphicRaycasters()
            {
                foreach (var raycaster in
                    Resources.FindObjectsOfTypeAll<GraphicRaycaster>())
                {
                    if (raycaster == null
                        || !raycaster.gameObject.scene.IsValid()
                        || !raycaster.gameObject.scene.isLoaded
                        || !raycaster.enabled)
                    {
                        continue;
                    }

                    disabledGraphicRaycasters.Add(raycaster);
                    raycaster.enabled = false;
                }
            }
        }

        private sealed class ExistingEventSystemIsolationScope :
            System.IDisposable
        {
            private readonly List<GameObject> activeEventSystemObjects = new();

            public ExistingEventSystemIsolationScope()
            {
                foreach (var eventSystem in
                    Resources.FindObjectsOfTypeAll<EventSystem>())
                {
                    if (eventSystem == null
                        || !eventSystem.gameObject.scene.IsValid()
                        || !eventSystem.gameObject.scene.isLoaded
                        || !eventSystem.gameObject.activeSelf)
                    {
                        continue;
                    }

                    activeEventSystemObjects.Add(eventSystem.gameObject);
                    eventSystem.gameObject.SetActive(false);
                }

                Assert.That(EventSystem.current, Is.Null);
            }

            public void Dispose()
            {
                foreach (var eventSystemObject in activeEventSystemObjects)
                {
                    if (eventSystemObject != null)
                    {
                        eventSystemObject.SetActive(true);
                    }
                }
            }
        }
    }

    public sealed class MouseCameraInputIntegrationTests : InputTestFixture
    {
        private Mouse mouse;
        private GameObject inputObject;
        private MouseCameraInput input;

        public override void Setup()
        {
            base.Setup();
            mouse = InputSystem.AddDevice<Mouse>();
            inputObject = new GameObject("MouseCameraInputFixture");
            input = inputObject.AddComponent<MouseCameraInput>();
            input.DragThresholdPixels = 6f;
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(inputObject);
            base.TearDown();
        }

        [UnityTest]
        public IEnumerator MouseInput_ClickReleaseProducesTap()
        {
            Set(mouse.position, new Vector2(10f, 20f));
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            input.ReadFrame();

            Release(mouse.leftButton);
            yield return null;
            var released = input.ReadFrame();

            Assert.That(released.TapReleased, Is.True);
            Assert.That(released.PanDelta, Is.EqualTo(Vector2.zero));
            Assert.That(
                released.PointerPosition,
                Is.EqualTo(new Vector2(10f, 20f)));
        }

        [UnityTest]
        public IEnumerator MouseInput_DragReleaseNeverProducesTap()
        {
            Set(mouse.position, Vector2.zero);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            input.ReadFrame();

            Set(mouse.position, new Vector2(20f, 0f));
            Set(mouse.delta, new Vector2(20f, 0f));
            yield return null;
            var dragged = input.ReadFrame();

            Release(mouse.leftButton);
            yield return null;
            var released = input.ReadFrame();

            Assert.That(dragged.PanDelta, Is.EqualTo(new Vector2(20f, 0f)));
            Assert.That(released.TapReleased, Is.False);
        }

        [UnityTest]
        public IEnumerator MouseInput_ReturningAfterDragStillDoesNotTap()
        {
            Set(mouse.position, Vector2.zero);
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            input.ReadFrame();

            Set(mouse.position, new Vector2(20f, 0f));
            Set(mouse.delta, new Vector2(20f, 0f));
            yield return null;
            input.ReadFrame();

            Set(mouse.position, Vector2.zero);
            Set(mouse.delta, new Vector2(-20f, 0f));
            yield return null;
            input.ReadFrame();

            Release(mouse.leftButton);
            yield return null;
            var released = input.ReadFrame();

            Assert.That(released.TapReleased, Is.False);
            Assert.That(released.PointerPosition, Is.EqualTo(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator MouseInput_TwoConsumersReceiveSameFrameValues()
        {
            Set(mouse.position, new Vector2(12f, 24f));
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            input.ReadFrame();

            Release(mouse.leftButton);
            yield return null;
            var first = input.ReadFrame();
            var second = input.ReadFrame();

            Assert.That(second.PanDelta, Is.EqualTo(first.PanDelta));
            Assert.That(second.ZoomDelta, Is.EqualTo(first.ZoomDelta));
            Assert.That(second.TapReleased, Is.EqualTo(first.TapReleased));
            Assert.That(
                second.PointerPosition,
                Is.EqualTo(first.PointerPosition));
            Assert.That(first.TapReleased, Is.True);
        }

        [UnityTest]
        public IEnumerator MouseInput_PauseStillReadsPointerAndTap()
        {
            Time.timeScale = 0f;
            Set(mouse.position, new Vector2(30f, 40f));
            yield return null;
            Press(mouse.leftButton);
            yield return null;
            input.ReadFrame();

            Release(mouse.leftButton);
            yield return null;
            var released = input.ReadFrame();

            Assert.That(released.TapReleased, Is.True);
            Assert.That(
                released.PointerPosition,
                Is.EqualTo(new Vector2(30f, 40f)));
        }

        [UnityTest]
        public IEnumerator MouseInput_ScrollFlowsToZoomDelta()
        {
            Set(mouse.scroll, new Vector2(0f, 120f));
            yield return null;

            var frame = input.ReadFrame();

            Assert.That(frame.ZoomDelta, Is.EqualTo(120f));
        }
    }
}

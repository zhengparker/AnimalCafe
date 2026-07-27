using System.Collections;
using AnimalCafe.Core.Time;
using AnimalCafe.Input;
using CafeCameraController = AnimalCafe.Camera.CafeCameraController;
using CameraSettings = AnimalCafe.Camera.CameraSettings;
using AnimalCafe.Interaction;
using AnimalCafe.UI;
using NUnit.Framework;
using UnityEngine;
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

    public sealed class Phase0PlayModeTests
    {
        [TearDown]
        public void TearDown()
        {
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
            var selectable = cube.AddComponent<ColorSelectable>();

            selectable.Select();
            Assert.That(selectable.IsSelected, Is.True);

            selectable.Deselect();
            Assert.That(selectable.IsSelected, Is.False);
            Object.DestroyImmediate(cube);
        }

        [Test]
        public void ColorSelectable_RecoversWhenRendererBecomesAvailableAfterAwake()
        {
            var gameObject = new GameObject("LateRenderer");
            var selectable = gameObject.AddComponent<ColorSelectable>();
            gameObject.AddComponent<MeshRenderer>();

            selectable.Select();

            Assert.That(selectable.IsSelected, Is.True);
            Assert.That(selectable.enabled, Is.True);
            Object.DestroyImmediate(gameObject);
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
            cubeA.transform.position = new Vector3(-1f, 0f, 0f);
            var selectableA = cubeA.AddComponent<ColorSelectable>();
            var cubeB = GameObject.CreatePrimitive(PrimitiveType.Cube);
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
            Object.DestroyImmediate(interactionObject);
            Object.DestroyImmediate(cameraObject);
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
            var canvas = GameObject.Find("Phase0_TimeControls");

            Assert.That(CountLoadedSceneObjects("Phase0_Runtime"), Is.EqualTo(1));
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
            Assert.That(CountLoadedSceneObjects("Phase0_TimeControls"), Is.EqualTo(1));
            Assert.That(canvas, Is.Not.Null);
            Assert.That(
                canvas.GetComponentInChildren<TimeControlPanel>(true),
                Is.Not.Null);
            Assert.That(CountLoadedSceneObjects("EventSystem"), Is.EqualTo(1));

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
            try
            {
                var selectable = selectableObject.AddComponent<ColorSelectable>();
                selectable.Select();

                Assert.That(selectable.IsSelected, Is.True);
                Assert.That(selectable.enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(selectableObject);
            }
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
    }
}

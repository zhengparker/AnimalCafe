using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Core.Events;
using AnimalCafe.Core.Time;
using AnimalCafe.Input;
using AnimalCafe.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase5MainCafeMigrationRound1PlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator LoadPauseInputUnloadAndReload_KeepsOneCleanMainCafeRuntime()
        {
            yield return LoadMainCafe();
            var firstScene = SceneManager.GetActiveScene();
            var firstService = Find<GameTimeService>(firstScene, "Phase0_Runtime");
            var firstMouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return Click(firstMouse, Find<Button>(firstScene, "PauseButton"));
                Assert.That(firstService.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));

                QueueMouseState(firstMouse, new Vector2(Screen.width * 0.1f, Screen.height * 0.8f), true);
                yield return null;
                yield return LoadMainCafe();

                var secondScene = SceneManager.GetActiveScene();
                var secondService = Find<GameTimeService>(secondScene, "Phase0_Runtime");
                Assert.That(FindAll(secondScene, "UI Root"), Has.Length.EqualTo(1));
                Assert.That(FindAll<EventSystem>(secondScene), Has.Length.EqualTo(1));
                Assert.That(FindAll<GameTimeService>(secondScene), Has.Length.EqualTo(1));
                Assert.That(FindAll<MouseCameraInput>(secondScene), Has.Length.EqualTo(1));
                Assert.That(FindAll<SceneInteractionController>(secondScene), Has.Length.EqualTo(1));
                Assert.That(secondService.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(Find<SceneInteractionController>(secondScene, "Phase0_Runtime").CurrentSelection, Is.Null);
                QueueMouseState(firstMouse, new Vector2(Screen.width * 0.1f, Screen.height * 0.8f), false);
                yield return null;
                yield return null;
                Assert.That(Find<SceneInteractionController>(secondScene, "Phase0_Runtime").CurrentSelection, Is.Null,
                    "The first physical release after reload must not complete a stale scene gesture.");

                yield return Click(firstMouse, Find<Button>(secondScene, "NormalButton"));
                Assert.That(secondService.CurrentSpeed, Is.EqualTo(GameSpeed.Normal),
                    "A fresh real UI click must work after a reload interrupted an earlier gesture.");
            }
            finally
            {
                if (firstMouse.added) InputSystem.RemoveDevice(firstMouse);
            }
        }

        [UnityTest]
        public IEnumerator WorldSelectAndClear_PublishesExactEvents_AndUiTapDoesNotChangeSelection()
        {
            yield return LoadMainCafe();
            var scene = SceneManager.GetActiveScene();
            var camera = Find<UnityEngine.Camera>(scene, "Main Camera");
            var interaction = Find<SceneInteractionController>(scene, "Phase0_Runtime");
            var selectPosition = FindPositionOutsideUiAndPhysics(EventSystem.current, camera);
            var selectableObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            selectableObject.transform.position = camera.ScreenPointToRay(selectPosition).GetPoint(4f);
            Physics.SyncTransforms();
            var selectable = selectableObject.AddComponent<ColorSelectable>();
            selectable.Configure(selectableObject.GetComponent<Renderer>());
            var clearPosition = FindPositionOutsideUiAndPhysics(EventSystem.current, camera, selectPosition);
            var events = new List<SelectionChangedEvent>();
            GameEventBus.SelectionChanged += events.Add;
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return TapWorld(mouse, selectPosition);
                Assert.That(interaction.CurrentSelection, Is.SameAs(selectable));
                Assert.That(events, Has.Count.EqualTo(1));
                Assert.That(events[0].Previous, Is.Null);
                Assert.That(events[0].Current, Is.SameAs(selectable));

                yield return Click(mouse, Find<Button>(scene, "PauseButton"));
                Assert.That(interaction.CurrentSelection, Is.SameAs(selectable));
                Assert.That(events, Has.Count.EqualTo(1),
                    "A migrated UI tap must not also select or deselect a world object.");

                yield return TapWorld(mouse, clearPosition);
                Assert.That(interaction.CurrentSelection, Is.Null);
                Assert.That(events, Has.Count.EqualTo(2));
                Assert.That(events[1].Previous, Is.SameAs(selectable));
                Assert.That(events[1].Current, Is.Null);
            }
            finally
            {
                GameEventBus.SelectionChanged -= events.Add;
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                Object.Destroy(selectableObject);
            }
        }

        [UnityTest]
        public IEnumerator PauseNormalFast_EmitOneOrderedGameSpeedEventPerRealUiClick()
        {
            yield return LoadMainCafe();
            var scene = SceneManager.GetActiveScene();
            var service = Find<GameTimeService>(scene, "Phase0_Runtime");
            var events = new List<GameSpeedChangedEvent>();
            GameEventBus.GameSpeedChanged += events.Add;
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return Click(mouse, Find<Button>(scene, "PauseButton"));
                yield return Click(mouse, Find<Button>(scene, "NormalButton"));
                yield return Click(mouse, Find<Button>(scene, "FastButton"));

                Assert.That(service.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
                Assert.That(events, Has.Count.EqualTo(3));
                AssertSpeedEvent(events[0], GameSpeed.Normal, GameSpeed.Paused);
                AssertSpeedEvent(events[1], GameSpeed.Paused, GameSpeed.Normal);
                AssertSpeedEvent(events[2], GameSpeed.Normal, GameSpeed.Fast);
            }
            finally
            {
                GameEventBus.GameSpeedChanged -= events.Add;
                if (mouse.added) InputSystem.RemoveDevice(mouse);
                service.SetNormal();
            }
        }

        private static IEnumerator LoadMainCafe()
        {
            var operation = SceneManager.LoadSceneAsync("MainCafe", LoadSceneMode.Single);
            while (!operation.isDone) yield return null;
            yield return null;
        }

        private static IEnumerator TapWorld(Mouse mouse, Vector2 position)
        {
            QueueMouseState(mouse, position, true);
            yield return null;
            yield return null;
            QueueMouseState(mouse, position, false);
            yield return null;
            yield return null;
        }

        private static IEnumerator Click(Mouse mouse, Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var position = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * 0.5f);
            QueueMouseState(mouse, position, true);
            yield return null;
            QueueMouseState(mouse, position, false);
            yield return null;
        }

        private static Vector2 FindPositionOutsideUiAndPhysics(
            EventSystem eventSystem,
            UnityEngine.Camera camera,
            Vector2 excluded = default)
        {
            var candidates = new[]
            {
                new Vector2(Screen.width * 0.12f, Screen.height * 0.88f),
                new Vector2(Screen.width * 0.88f, Screen.height * 0.88f),
                new Vector2(Screen.width * 0.12f, Screen.height * 0.5f),
                new Vector2(Screen.width * 0.88f, Screen.height * 0.5f),
                new Vector2(Screen.width * 0.5f, Screen.height * 0.88f)
            };
            foreach (var candidate in candidates)
            {
                if (candidate == excluded) continue;
                var uiHits = new List<RaycastResult>();
                eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = candidate }, uiHits);
                if (uiHits.Count == 0 && !Physics.Raycast(camera.ScreenPointToRay(candidate))) return candidate;
            }

            Assert.Fail("MainCafe needs two UI-clear, physics-clear positions for selection and deselection coverage.");
            return default;
        }

        private static void QueueMouseState(Mouse mouse, Vector2 position, bool leftDown)
        {
            var state = new MouseState { position = position };
            if (leftDown) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state);
        }

        private static void AssertSpeedEvent(GameSpeedChangedEvent item, GameSpeed previous, GameSpeed current)
        {
            Assert.That(item.Previous, Is.EqualTo(previous));
            Assert.That(item.Current, Is.EqualTo(current));
        }

        private static GameObject[] FindAll(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(transform => transform.name == name).Select(transform => transform.gameObject).ToArray();

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static T Find<T>(Scene scene, string name) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Single(transform => transform.name == name).GetComponent<T>();
    }
}

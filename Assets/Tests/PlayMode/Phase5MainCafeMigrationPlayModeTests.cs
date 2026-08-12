#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Core.Time;
using AnimalCafe.Input;
using AnimalCafe.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase5MainCafeMigrationPlayModeTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator VirtualMouse_ClicksMigratedPauseNormalAndFastButtons()
        {
            yield return LoadMainCafe();
            var scene = SceneManager.GetActiveScene();
            var inputModule = Find<EventSystem>(scene, "EventSystem").GetComponent<InputSystemUIInputModule>();
            var service = Find<GameTimeService>(scene, "Phase0_Runtime");
            Assert.That(inputModule.actionsAsset, Is.Not.Null,
                "Migrated MainCafe must persist Input System UI actions for real pointer input.");

            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            try
            {
                QueueMouseState(mouse, Vector2.zero, false);
                yield return null;
                yield return Click(mouse, Find<Button>(scene, "PauseButton"));
                Assert.That(service.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));

                yield return Click(mouse, Find<Button>(scene, "NormalButton"));
                Assert.That(service.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));

                yield return Click(mouse, Find<Button>(scene, "FastButton"));
                Assert.That(service.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                service.SetNormal();
            }
        }

        [UnityTest]
        public IEnumerator VirtualMouse_WorldTapSelectsObjectOutsideMigratedUi()
        {
            yield return LoadMainCafe();
            var scene = SceneManager.GetActiveScene();
            var inputModule = Find<EventSystem>(scene, "EventSystem").GetComponent<InputSystemUIInputModule>();
            Assert.That(inputModule.actionsAsset, Is.Not.Null,
                "Migrated MainCafe must keep Input System UI actions alongside world input.");
            var camera = Find<UnityEngine.Camera>(scene, "Main Camera");
            var interaction = Find<SceneInteractionController>(scene, "Phase0_Runtime");
            var input = Find<MouseCameraInput>(scene, "Phase0_Runtime");
            var worldPosition = FindScreenPositionOutsideUi(EventSystem.current);
            var selectableObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            selectableObject.name = "Task10 Runtime Selectable";
            selectableObject.transform.position = camera.ScreenPointToRay(
                worldPosition).GetPoint(4f);
            Physics.SyncTransforms();
            var selectable = selectableObject.AddComponent<ColorSelectable>();
            selectable.Configure(selectableObject.GetComponent<Renderer>());
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var position = camera.WorldToScreenPoint(selectableObject.transform.position);
                Assert.That(Vector2.Distance(position, worldPosition), Is.LessThan(0.01f));
                Assert.That(position.x, Is.InRange(0f, Screen.width));
                Assert.That(position.y, Is.InRange(0f, Screen.height));
                Assert.That(Physics.Raycast(camera.ScreenPointToRay(position), out var hit), Is.True);
                Assert.That(hit.collider.gameObject, Is.EqualTo(selectableObject));
                Assert.That(EventSystem.current.IsPointerOverGameObject(mouse.deviceId), Is.False);

                QueueMouseState(mouse, position, true);
                yield return null;
                Assert.That(Mouse.current, Is.SameAs(mouse));
                Assert.That(input.ReadFrame().PointerPressed, Is.True);
                // SceneInteractionController registers world ownership on the frame after press.
                yield return null;
                Assert.That(interaction.enabled, Is.True);
                Assert.That(EventSystem.current.IsPointerOverGameObject(mouse.deviceId), Is.False,
                    "The physical Mouse pointer must remain outside UI at the world target.");
                Assert.That(EventSystem.current.IsPointerOverGameObject(), Is.False,
                    "The controller's default EventSystem pointer query must not treat a world tap as UI.");
                QueueMouseState(mouse, position, false);
                yield return null;
                Assert.That(input.ReadFrame().TapReleased, Is.True);
                yield return null;

                Assert.That(interaction.CurrentSelection, Is.SameAs(selectable));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                Object.Destroy(selectableObject);
            }
        }

        private static IEnumerator LoadMainCafe()
        {
            var operation = SceneManager.LoadSceneAsync("MainCafe", LoadSceneMode.Single);
            while (!operation.isDone) yield return null;
            yield return null;
        }

        private static IEnumerator Click(Mouse mouse, Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var position = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * 0.5f);
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, results);
            Assert.That(results, Is.Not.Empty);
            Assert.That(
                results[0].gameObject == button.gameObject
                || results[0].gameObject.transform.IsChildOf(button.transform),
                Is.True,
                "The pointer must hit the Button or one of its visual children.");
            QueueMouseState(mouse, position, true);
            yield return null;
            yield return null;
            QueueMouseState(mouse, position, false);
            yield return null;
            yield return null;
        }

        private static void QueueMouseState(Mouse mouse, Vector2 position, bool leftDown)
        {
            var state = new MouseState { position = position };
            if (leftDown) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state);
        }

        private static Vector2 FindScreenPositionOutsideUi(EventSystem eventSystem)
        {
            var candidates = new[]
            {
                new Vector2(Screen.width * 0.15f, Screen.height * 0.85f),
                new Vector2(Screen.width * 0.85f, Screen.height * 0.85f),
                new Vector2(Screen.width * 0.15f, Screen.height * 0.5f),
                new Vector2(Screen.width * 0.85f, Screen.height * 0.5f)
            };
            foreach (var candidate in candidates)
            {
                var hits = new List<RaycastResult>();
                eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = candidate }, hits);
                if (hits.Count == 0) return candidate;
            }

            Assert.Fail("Migrated MainCafe has no screen position outside UI raycast targets for world selection.");
            return default;
        }

        private static T Find<T>(Scene scene, string name) where T : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Single(transform => transform.name == name)
                .GetComponent<T>();
    }
}
#endif

using System.Collections;
using System.Collections.Generic;
using AnimalCafe.Input;
using AnimalCafe.Interaction;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase5PointerBoundaryPlayModeTests
    {
        [UnityTest]
        public IEnumerator IT005_EventSystemUiPress_DoesNotSelectWorldObjectOnRelease()
        {
            using var fixture = new PointerBoundaryFixture();
            var uiPosition = fixture.SelectablePosition;
            fixture.PlaceUiAt(uiPosition);
            QueueMousePosition(fixture.Mouse, uiPosition);
            yield return null;

            fixture.AssertUiRaycast(uiPosition, true);
            fixture.SendUiPress(uiPosition);
            fixture.Input.NextFrame = new CameraInputFrame(
                Vector2.zero,
                0f,
                true,
                uiPosition);
            yield return null;

            Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
            Assert.That(fixture.Selectable.IsSelected, Is.False);
        }

        [UnityTest]
        public IEnumerator IT006_OutsideDismissRelease_AfterUiClosedDoesNotSelectWorldObject()
        {
            using var fixture = new PointerBoundaryFixture();
            var worldPosition = fixture.SelectablePosition;
            fixture.PlaceUiAt(new Vector2(30f, 30f));
            fixture.SendUiPress(new Vector2(30f, 30f));
            fixture.UiImage.gameObject.SetActive(false);
            QueueMousePosition(fixture.Mouse, worldPosition);
            yield return null;

            fixture.Input.NextFrame = new CameraInputFrame(
                Vector2.zero,
                0f,
                true,
                worldPosition);
            yield return null;

            Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
            Assert.That(fixture.Selectable.IsSelected, Is.False);
        }

        [UnityTest]
        public IEnumerator IT007_UiPressThenDragToWorldRelease_DoesNotSelectWorldObject()
        {
            using var fixture = new PointerBoundaryFixture();
            var worldPosition = fixture.SelectablePosition;
            fixture.PlaceUiAt(new Vector2(30f, 30f));
            fixture.SendUiPress(new Vector2(30f, 30f));
            QueueMousePosition(fixture.Mouse, worldPosition);
            yield return null;

            fixture.AssertUiRaycast(worldPosition, false);
            fixture.Input.NextFrame = new CameraInputFrame(
                Vector2.zero,
                0f,
                true,
                worldPosition);
            yield return null;

            Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
            Assert.That(fixture.Selectable.IsSelected, Is.False);
        }

        [UnityTest]
        public IEnumerator IT008_WorldTap_StillSelectsWorldObject()
        {
            using var fixture = new PointerBoundaryFixture();
            var worldPosition = fixture.SelectablePosition;
            fixture.PlaceUiAt(new Vector2(30f, 30f));
            QueueMousePosition(fixture.Mouse, worldPosition);
            yield return null;

            fixture.AssertUiRaycast(worldPosition, false);
            fixture.Input.NextFrame = new CameraInputFrame(
                Vector2.zero,
                0f,
                true,
                worldPosition);
            yield return null;

            Assert.That(fixture.Interaction.CurrentSelection, Is.SameAs(fixture.Selectable));
            Assert.That(fixture.Selectable.IsSelected, Is.True);
        }

        [UnityTest]
        public IEnumerator IT009_ModalSceneBlock_PreventsWorldSelection()
        {
            using var fixture = new PointerBoundaryFixture();
            using (fixture.Boundary.AcquireSceneBlock())
            {
                fixture.Input.NextFrame = new CameraInputFrame(
                    Vector2.zero,
                    0f,
                    true,
                    fixture.SelectablePosition);
                yield return null;
            }

            Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
            Assert.That(fixture.Selectable.IsSelected, Is.False);
        }

        [UnityTest]
        public IEnumerator IT011_ToastDoesNotBlockWorldSelection()
        {
            using var fixture = new PointerBoundaryFixture();
            fixture.Boundary.NotifyToastShown();
            fixture.Input.NextFrame = new CameraInputFrame(
                Vector2.zero,
                0f,
                true,
                fixture.SelectablePosition);
            yield return null;

            Assert.That(fixture.Interaction.CurrentSelection, Is.SameAs(fixture.Selectable));
            Assert.That(fixture.Selectable.IsSelected, Is.True);
        }

        private static void QueueMousePosition(Mouse mouse, Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            InputSystem.Update();
        }

        private sealed class PointerBoundaryFixture : System.IDisposable
        {
            private readonly GameObject cameraObject;
            private readonly GameObject controllerObject;
            private readonly GameObject selectableObject;
            private readonly Material selectableMaterial;
            private readonly GameObject canvasObject;
            private readonly GameObject eventSystemObject;
            private readonly List<GameObject> disabledEventSystems = new();

            public PointerBoundaryFixture()
            {
                DisableExistingEventSystems();

                cameraObject = new GameObject("Phase5PointerCamera");
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);

                controllerObject = new GameObject("Phase5PointerController");
                Interaction = controllerObject.AddComponent<SceneInteractionController>();
                Input = controllerObject.AddComponent<CameraInputTestFixture>();
                Boundary = new UiPointerBoundary();
                Interaction.Configure(camera, Input, Boundary);

                selectableObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                Assert.That(shader, Is.Not.Null);
                selectableMaterial = new Material(shader);
                selectableObject.GetComponent<Renderer>().sharedMaterial = selectableMaterial;
                Selectable = selectableObject.AddComponent<ColorSelectable>();
                Physics.SyncTransforms();
                SelectablePosition = camera.WorldToScreenPoint(selectableObject.transform.position);

                canvasObject = new GameObject(
                    "Phase5PointerCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(GraphicRaycaster));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var uiImageObject = new GameObject(
                    "Phase5PointerUiImage",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                UiImage = uiImageObject.GetComponent<RectTransform>();
                UiImage.SetParent(canvasObject.transform, false);
                UiImage.anchorMin = Vector2.zero;
                UiImage.anchorMax = Vector2.zero;
                UiImage.pivot = new Vector2(0.5f, 0.5f);
                UiImage.sizeDelta = new Vector2(80f, 80f);
                UiImage.GetComponent<Image>().color = Color.white;
                UiHook = UiImage.gameObject.AddComponent<UiPointerBoundaryEventHook>();
                UiHook.Configure(Boundary);

                eventSystemObject = new GameObject("Phase5PointerEventSystem");
                EventSystem = eventSystemObject.AddComponent<EventSystem>();

                Mouse = InputSystem.AddDevice<Mouse>();
                if (!Mouse.enabled)
                {
                    InputSystem.EnableDevice(Mouse);
                }
            }

            public UiPointerBoundary Boundary { get; }
            public SceneInteractionController Interaction { get; }
            public CameraInputTestFixture Input { get; }
            public ColorSelectable Selectable { get; }
            public Vector2 SelectablePosition { get; }
            public RectTransform UiImage { get; }
            public UiPointerBoundaryEventHook UiHook { get; }
            public EventSystem EventSystem { get; }
            public Mouse Mouse { get; }

            public void PlaceUiAt(Vector2 screenPosition)
            {
                UiImage.anchoredPosition = screenPosition;
                Canvas.ForceUpdateCanvases();
            }

            public void SendUiPress(Vector2 screenPosition)
            {
                ExecuteEvents.Execute(
                    UiImage.gameObject,
                    new PointerEventData(EventSystem)
                    {
                        pointerId = PointerInputModule.kMouseLeftId,
                        position = screenPosition
                    },
                    ExecuteEvents.pointerDownHandler);
            }

            public void AssertUiRaycast(Vector2 screenPosition, bool expectedOverUi)
            {
                var results = new List<RaycastResult>();
                EventSystem.RaycastAll(
                    new PointerEventData(EventSystem) { position = screenPosition },
                    results);
                Assert.That(
                    results.Exists(result => result.gameObject == UiImage.gameObject),
                    Is.EqualTo(expectedOverUi));
            }

            public void Dispose()
            {
                if (Mouse != null && Mouse.added)
                {
                    InputSystem.RemoveDevice(Mouse);
                }

                Object.DestroyImmediate(eventSystemObject);
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(selectableObject);
                Object.DestroyImmediate(selectableMaterial);
                Object.DestroyImmediate(controllerObject);
                Object.DestroyImmediate(cameraObject);

                foreach (var eventSystemObject in disabledEventSystems)
                {
                    if (eventSystemObject != null)
                    {
                        eventSystemObject.SetActive(true);
                    }
                }
            }

            private void DisableExistingEventSystems()
            {
                foreach (var eventSystem in Resources.FindObjectsOfTypeAll<EventSystem>())
                {
                    if (eventSystem.gameObject.scene.IsValid()
                        && eventSystem.gameObject.scene.isLoaded
                        && eventSystem.gameObject.activeSelf)
                    {
                        disabledEventSystems.Add(eventSystem.gameObject);
                        eventSystem.gameObject.SetActive(false);
                    }
                }
            }
        }
    }
}

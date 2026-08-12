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
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase5PointerBoundaryPlayModeTests
    {
        [UnityTest]
        public IEnumerator MouseCameraInput_ReportsRealPointerIdentityAndDragRelease()
        {
            using var focusScope = new InputFocusIsolationScope();
            var mouse = InputSystem.AddDevice<Mouse>();
            var inputObject = new GameObject("Phase5MouseCameraInput");
            var input = inputObject.AddComponent<MouseCameraInput>();
            input.DragThresholdPixels = 6f;

            try
            {
                QueueMouseState(mouse, Vector2.zero, true);
                yield return null;
                var pressed = input.ReadFrame();

                QueueMouseState(mouse, new Vector2(20f, 0f), true);
                yield return null;
                input.ReadFrame();

                QueueMouseState(mouse, new Vector2(20f, 0f), false);
                yield return null;
                var released = input.ReadFrame();

                Assert.That(pressed.PointerId, Is.EqualTo(mouse.deviceId));
                Assert.That(pressed.PointerPressed, Is.True);
                Assert.That(released.PointerReleased, Is.True);
                Assert.That(released.TapReleased, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(inputObject);
                if (mouse.added)
                {
                    InputSystem.RemoveDevice(mouse);
                }
            }
        }

        [UnityTest]
        public IEnumerator IT005_InputSystemUiButtonClick_InvokesOnceWithoutWorldSelection()
        {
            using var fixture = new PointerBoundaryFixture();
            fixture.PlaceButtonAt(fixture.SelectablePosition);

            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            yield return null;
            fixture.QueueMouseState(fixture.SelectablePosition, false);
            yield return null;
            yield return null;

            Assert.That(fixture.ButtonClickCount, Is.EqualTo(1));
            Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
            Assert.That(
                fixture.Boundary.GetOwnership(fixture.Mouse.deviceId),
                Is.EqualTo(UiPointerOwnership.None));
        }

        [UnityTest]
        public IEnumerator IT006_OutsideDismiss_ClosesUiWithoutSelectingWorldOnSameRelease()
        {
            using var fixture = new PointerBoundaryFixture();
            fixture.PlaceButtonAt(fixture.SelectablePosition);
            fixture.ShowDismissibleSheet(new Vector2(30f, 30f));
            fixture.ShowOutsideDismissTarget();
            yield return null;

            Assert.That(
                fixture.TopRaycastTargetAt(fixture.SelectablePosition),
                Is.SameAs(fixture.OutsideDismissObject));

            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            yield return null;
            fixture.QueueMouseState(fixture.SelectablePosition, false);
            yield return null;
            yield return null;

            Assert.That(fixture.OutsideDismissCount, Is.EqualTo(1));
            Assert.That(fixture.SheetObject.activeSelf, Is.False);
            Assert.That(fixture.ButtonClickCount, Is.EqualTo(0));
            Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
        }

        [UnityTest]
        public IEnumerator DisableThenReleaseUiGesture_SuppressesWorldSelectionUntilFreshTap()
        {
            using var fixture = new PointerBoundaryFixture();
            fixture.PlaceButtonAt(fixture.SelectablePosition);

            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            yield return null;
            fixture.Interaction.gameObject.SetActive(false);
            fixture.Interaction.gameObject.SetActive(true);

            fixture.QueueMouseState(fixture.SelectablePosition, false);
            yield return null;
            yield return null;

            Assert.That(fixture.Interaction.CurrentSelection, Is.Null);

            fixture.PlaceButtonAt(new Vector2(30f, 30f));
            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            fixture.QueueMouseState(fixture.SelectablePosition, false);
            yield return null;
            yield return null;

            Assert.That(fixture.Interaction.CurrentSelection, Is.SameAs(fixture.Selectable));
        }

        [UnityTest]
        public IEnumerator DisableThenMissRelease_FreshWorldTapWithSameIdSelectsNormally()
        {
            using var fixture = new PointerBoundaryFixture();
            fixture.PlaceButtonAt(fixture.SelectablePosition);

            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            yield return null;
            fixture.Interaction.gameObject.SetActive(false);

            fixture.QueueMouseState(fixture.SelectablePosition, false);
            yield return null;
            yield return null;

            fixture.Interaction.gameObject.SetActive(true);
            fixture.PlaceButtonAt(new Vector2(30f, 30f));
            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            fixture.QueueMouseState(fixture.SelectablePosition, false);
            yield return null;
            yield return null;

            Assert.That(fixture.Interaction.CurrentSelection, Is.SameAs(fixture.Selectable));
        }

        [UnityTest]
        public IEnumerator ReconfigureThenReleaseSceneGesture_SuppressesWorldSelectionUntilFreshTap()
        {
            using var fixture = new PointerBoundaryFixture();
            fixture.PlaceButtonAt(new Vector2(30f, 30f));

            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            yield return null;
            yield return null;
            var replacementBoundary = new UiPointerBoundary();
            fixture.Interaction.Configure(
                fixture.Camera,
                fixture.Input,
                replacementBoundary);

            fixture.QueueMouseState(fixture.SelectablePosition, false);
            yield return null;
            yield return null;

            Assert.That(fixture.Interaction.CurrentSelection, Is.Null);

            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            fixture.QueueMouseState(fixture.SelectablePosition, false);
            yield return null;
            yield return null;

            Assert.That(fixture.Interaction.CurrentSelection, Is.SameAs(fixture.Selectable));
        }

        [UnityTest]
        public IEnumerator ReconfigureThenMissRelease_FreshNewSourceTapWithSameIdSelectsNormally()
        {
            using var fixture = new PointerBoundaryFixture();
            fixture.PlaceButtonAt(new Vector2(30f, 30f));

            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            yield return null;
            yield return null;

            var replacementObject = new GameObject("Phase5ReplacementInput");
            var replacementInput = replacementObject.AddComponent<CameraInputTestFixture>();
            try
            {
                fixture.Interaction.Configure(
                    fixture.Camera,
                    replacementInput,
                    new UiPointerBoundary());

                replacementInput.NextFrame = new CameraInputFrame(
                    Vector2.zero,
                    0f,
                    false,
                    fixture.SelectablePosition,
                    fixture.Mouse.deviceId,
                    true);
                yield return null;
                replacementInput.NextFrame = default;
                yield return null;
                replacementInput.NextFrame = new CameraInputFrame(
                    Vector2.zero,
                    0f,
                    true,
                    fixture.SelectablePosition,
                    fixture.Mouse.deviceId,
                    false,
                    true);
                yield return null;
                replacementInput.NextFrame = default;
                yield return null;

                Assert.That(
                    fixture.Interaction.CurrentSelection,
                    Is.SameAs(fixture.Selectable));
            }
            finally
            {
                Object.DestroyImmediate(replacementObject);
            }
        }

        [UnityTest]
        public IEnumerator ReconfigureWithoutBoundaryThenMissRelease_FreshNewSourceTapSelectsNormally()
        {
            using var fixture = new PointerBoundaryFixture();
            fixture.PlaceButtonAt(new Vector2(30f, 30f));

            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            yield return null;
            yield return null;

            var replacementObject = new GameObject("Phase5NullBoundaryReplacementInput");
            var replacementInput = replacementObject.AddComponent<CameraInputTestFixture>();
            try
            {
                fixture.Interaction.Configure(fixture.Camera, replacementInput);

                fixture.QueueMouseState(fixture.SelectablePosition, false);
                yield return null;
                yield return null;
                Assert.That(EventSystem.current.IsPointerOverGameObject(fixture.Mouse.deviceId), Is.False);
                Assert.That(EventSystem.current.IsPointerOverGameObject(), Is.False);

                replacementInput.NextFrame = new CameraInputFrame(
                    Vector2.zero,
                    0f,
                    false,
                    fixture.SelectablePosition,
                    fixture.Mouse.deviceId,
                    true);
                yield return null;
                replacementInput.NextFrame = default;
                yield return null;
                replacementInput.NextFrame = new CameraInputFrame(
                    Vector2.zero,
                    0f,
                    true,
                    fixture.SelectablePosition,
                    fixture.Mouse.deviceId,
                    false,
                    true);
                yield return null;

                Assert.That(
                    fixture.Interaction.CurrentSelection,
                    Is.SameAs(fixture.Selectable));
            }
            finally
            {
                Object.DestroyImmediate(replacementObject);
            }
        }

        [UnityTest]
        public IEnumerator Disable_ReleasesActiveUiOwnershipAndAllowsNextUiPress()
        {
            using var fixture = new PointerBoundaryFixture();
            fixture.PlaceButtonAt(fixture.SelectablePosition);

            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(
                fixture.Boundary.GetOwnership(fixture.Mouse.deviceId),
                Is.EqualTo(UiPointerOwnership.Ui));

            fixture.Interaction.gameObject.SetActive(false);

            Assert.That(
                fixture.Boundary.GetOwnership(fixture.Mouse.deviceId),
                Is.EqualTo(UiPointerOwnership.None));

            fixture.Interaction.gameObject.SetActive(true);
            fixture.QueueMouseState(fixture.SelectablePosition, false);
            yield return null;
            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            yield return null;

            Assert.That(
                fixture.Boundary.GetOwnership(fixture.Mouse.deviceId),
                Is.EqualTo(UiPointerOwnership.Ui));
        }

        [UnityTest]
        public IEnumerator Configure_ReleasesRegisteredSceneOwnershipFromPreviousBoundary()
        {
            using var fixture = new PointerBoundaryFixture();
            fixture.PlaceButtonAt(new Vector2(30f, 30f));

            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(
                fixture.Boundary.GetOwnership(fixture.Mouse.deviceId),
                Is.EqualTo(UiPointerOwnership.Scene));

            var replacementBoundary = new UiPointerBoundary();
            fixture.Interaction.Configure(null, fixture.Input, replacementBoundary);

            Assert.That(
                fixture.Boundary.GetOwnership(fixture.Mouse.deviceId),
                Is.EqualTo(UiPointerOwnership.None));
            replacementBoundary.RegisterScenePointerPress(fixture.Mouse.deviceId);
            Assert.That(
                replacementBoundary.GetOwnership(fixture.Mouse.deviceId),
                Is.EqualTo(UiPointerOwnership.Scene));
        }

        [UnityTest]
        public IEnumerator IT007_InputSystemUiPressThenDragToWorld_ClearsOwnershipWithoutSelection()
        {
            using var fixture = new PointerBoundaryFixture();
            var uiPosition = new Vector2(30f, 30f);
            fixture.PlaceButtonAt(uiPosition);

            fixture.QueueMouseState(uiPosition, true);
            yield return null;
            yield return null;
            Assert.That(
                fixture.ButtonPointerId,
                Is.EqualTo(fixture.Mouse.deviceId));
            Assert.That(
                fixture.Boundary.GetOwnership(fixture.Mouse.deviceId),
                Is.EqualTo(UiPointerOwnership.Ui));

            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            fixture.QueueMouseState(fixture.SelectablePosition, false);
            yield return null;
            yield return null;

            Assert.That(fixture.ButtonClickCount, Is.EqualTo(0));
            Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
            Assert.That(
                fixture.Boundary.GetOwnership(fixture.Mouse.deviceId),
                Is.EqualTo(UiPointerOwnership.None));
        }

        [UnityTest]
        public IEnumerator IT008_InputSystemWorldTap_SelectsAndClearsOwnership()
        {
            using var fixture = new PointerBoundaryFixture();
            fixture.PlaceButtonAt(new Vector2(30f, 30f));

            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            fixture.QueueMouseState(fixture.SelectablePosition, false);
            yield return null;
            yield return null;

            Assert.That(fixture.Interaction.CurrentSelection, Is.SameAs(fixture.Selectable));
            Assert.That(
                fixture.Boundary.GetOwnership(fixture.Mouse.deviceId),
                Is.EqualTo(UiPointerOwnership.None));
        }

        [UnityTest]
        public IEnumerator IT009_ModalOverlay_BlocksLowerButtonAndWorld()
        {
            using var fixture = new PointerBoundaryFixture();
            fixture.PlaceButtonAt(fixture.SelectablePosition);
            fixture.ShowModalOverlayAt(fixture.SelectablePosition);
            using (fixture.Boundary.AcquireSceneBlock())
            {
                fixture.QueueMouseState(fixture.SelectablePosition, true);
                yield return null;
                fixture.QueueMouseState(fixture.SelectablePosition, false);
                yield return null;
                yield return null;
            }

            Assert.That(fixture.ButtonClickCount, Is.EqualTo(0));
            Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
        }

        [UnityTest]
        public IEnumerator IT011_ToastOverlayWithoutRaycastTarget_AllowsUnderlyingButtonClick()
        {
            using var fixture = new PointerBoundaryFixture();
            fixture.PlaceButtonAt(fixture.SelectablePosition);
            fixture.ShowToastOverlayAt(fixture.SelectablePosition);

            fixture.QueueMouseState(fixture.SelectablePosition, true);
            yield return null;
            fixture.QueueMouseState(fixture.SelectablePosition, false);
            yield return null;
            yield return null;

            Assert.That(fixture.ButtonClickCount, Is.EqualTo(1));
            Assert.That(fixture.Interaction.CurrentSelection, Is.Null);
        }

        private sealed class PointerBoundaryFixture : System.IDisposable
        {
            private readonly InputFocusIsolationScope inputFocusScope;
            private readonly GameObject cameraObject;
            private readonly GameObject controllerObject;
            private readonly GameObject selectableObject;
            private readonly Material selectableMaterial;
            private readonly GameObject canvasObject;
            private readonly GameObject eventSystemObject;
            private readonly List<GameObject> disabledEventSystems = new();
            private readonly List<GraphicRaycaster> disabledGraphicRaycasters = new();
            private readonly RectTransform buttonRect;
            private readonly Button button;
            private readonly Image modalOverlay;
            private readonly Image toastOverlay;
            private readonly Button outsideDismissButton;
            private readonly RectTransform sheetRect;
            private readonly PointerDownRecorder buttonPointerRecorder;

            public PointerBoundaryFixture()
            {
                inputFocusScope = new InputFocusIsolationScope();
                DisableExistingEventSystems();
                DisableExistingGraphicRaycasters();

                cameraObject = new GameObject("Phase5PointerCamera");
                Camera = cameraObject.AddComponent<UnityEngine.Camera>();
                cameraObject.transform.position = new Vector3(0f, 0f, -10f);

                controllerObject = new GameObject("Phase5PointerController");
                Interaction = controllerObject.AddComponent<SceneInteractionController>();
                Input = controllerObject.AddComponent<MouseCameraInput>();
                Boundary = new UiPointerBoundary();
                Interaction.Configure(Camera, Input, Boundary);

                selectableObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                // Keep this fixture's target in front of any collider left by a loaded
                // integration scene; selection tests must not depend on suite order.
                selectableObject.transform.position = new Vector3(0f, 0f, -8f);
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                Assert.That(shader, Is.Not.Null);
                selectableMaterial = new Material(shader);
                selectableObject.GetComponent<Renderer>().sharedMaterial = selectableMaterial;
                Selectable = selectableObject.AddComponent<ColorSelectable>();
                Physics.SyncTransforms();
                SelectablePosition = Camera.WorldToScreenPoint(selectableObject.transform.position);
                Assert.That(Physics.Raycast(
                    Camera.ScreenPointToRay(SelectablePosition), out var firstHit), Is.True);
                Assert.That(firstHit.collider, Is.SameAs(selectableObject.GetComponent<Collider>()),
                    "Pointer fixture must own the first world hit even after another scene test.");

                canvasObject = new GameObject(
                    "Phase5PointerCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(GraphicRaycaster));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

                ButtonObject = CreateRaycastTarget("Phase5PointerButton", out buttonRect);
                button = ButtonObject.AddComponent<Button>();
                ButtonObject.AddComponent<UiPointerBoundaryEventHook>().Configure(Boundary);
                buttonPointerRecorder = ButtonObject.AddComponent<PointerDownRecorder>();
                button.onClick.AddListener(() => ButtonClickCount++);

                var modalObject = CreateRaycastTarget("Phase5ModalOverlay", out var modalRect);
                modalOverlay = modalObject.GetComponent<Image>();
                modalObject.AddComponent<UiPointerBoundaryEventHook>().Configure(Boundary);
                modalObject.SetActive(false);

                var toastObject = CreateRaycastTarget("Phase5ToastOverlay", out var toastRect);
                toastOverlay = toastObject.GetComponent<Image>();
                toastOverlay.raycastTarget = false;
                toastObject.SetActive(false);

                var outsideDismissObject = CreateRaycastTarget(
                    "Phase5OutsideDismissTarget",
                    out var outsideDismissRect);
                outsideDismissRect.anchorMin = Vector2.zero;
                outsideDismissRect.anchorMax = Vector2.one;
                outsideDismissRect.offsetMin = Vector2.zero;
                outsideDismissRect.offsetMax = Vector2.zero;
                outsideDismissButton = outsideDismissObject.AddComponent<Button>();
                outsideDismissObject.AddComponent<UiPointerBoundaryEventHook>()
                    .Configure(Boundary);
                outsideDismissObject.SetActive(false);

                SheetObject = CreateRaycastTarget("Phase5DismissibleSheet", out sheetRect);
                SheetObject.SetActive(false);
                outsideDismissButton.onClick.AddListener(() =>
                {
                    OutsideDismissCount++;
                    SheetObject.SetActive(false);
                });

                eventSystemObject = new GameObject("Phase5PointerEventSystem");
                EventSystem = eventSystemObject.AddComponent<EventSystem>();
                InputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
                InputModule.UnassignActions();
                InputModule.AssignDefaultActions();

                Mouse = InputSystem.AddDevice<Mouse>();
                if (!Mouse.enabled)
                {
                    InputSystem.EnableDevice(Mouse);
                }
            }

            public UiPointerBoundary Boundary { get; }
            public SceneInteractionController Interaction { get; }
            public MouseCameraInput Input { get; }
            public UnityEngine.Camera Camera { get; }
            public ColorSelectable Selectable { get; }
            public Vector2 SelectablePosition { get; }
            public GameObject ButtonObject { get; }
            public GameObject SheetObject { get; }
            public GameObject OutsideDismissObject => outsideDismissButton.gameObject;
            public EventSystem EventSystem { get; }
            public InputSystemUIInputModule InputModule { get; }
            public Mouse Mouse { get; }
            public int ButtonClickCount { get; private set; }
            public int OutsideDismissCount { get; private set; }
            public int ButtonPointerId => buttonPointerRecorder.PointerId;

            public void PlaceButtonAt(Vector2 screenPosition)
            {
                PlaceAt(buttonRect, screenPosition);
                ButtonObject.SetActive(true);
            }

            public void ShowDismissibleSheet(Vector2 screenPosition)
            {
                PlaceAt(sheetRect, screenPosition);
                SheetObject.SetActive(true);
            }

            public void ShowOutsideDismissTarget()
            {
                outsideDismissButton.gameObject.SetActive(true);
                Canvas.ForceUpdateCanvases();
            }

            public GameObject TopRaycastTargetAt(Vector2 screenPosition)
            {
                var results = new List<RaycastResult>();
                EventSystem.RaycastAll(
                    new PointerEventData(EventSystem) { position = screenPosition },
                    results);
                return results.Count > 0 ? results[0].gameObject : null;
            }

            public void ShowModalOverlayAt(Vector2 screenPosition)
            {
                PlaceAt(modalOverlay.rectTransform, screenPosition);
                modalOverlay.gameObject.SetActive(true);
            }

            public void ShowToastOverlayAt(Vector2 screenPosition)
            {
                PlaceAt(toastOverlay.rectTransform, screenPosition);
                toastOverlay.gameObject.SetActive(true);
            }

            public void QueueMouseState(Vector2 position, bool leftDown)
            {
                Phase5PointerBoundaryPlayModeTests.QueueMouseState(
                    Mouse,
                    position,
                    leftDown);
            }

            public void Dispose()
            {
                InputModule.UnassignActions();
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

                foreach (var raycaster in disabledGraphicRaycasters)
                {
                    if (raycaster != null) raycaster.enabled = true;
                }

                inputFocusScope.Dispose();
            }

            private GameObject CreateRaycastTarget(string name, out RectTransform rect)
            {
                var gameObject = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                rect = gameObject.GetComponent<RectTransform>();
                rect.SetParent(canvasObject.transform, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(100f, 100f);
                gameObject.GetComponent<Image>().color = Color.white;
                return gameObject;
            }

            private static void PlaceAt(RectTransform rect, Vector2 screenPosition)
            {
                rect.anchoredPosition = screenPosition;
                Canvas.ForceUpdateCanvases();
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

            private void DisableExistingGraphicRaycasters()
            {
                foreach (var raycaster in Resources.FindObjectsOfTypeAll<GraphicRaycaster>())
                {
                    if (raycaster.gameObject.scene.IsValid()
                        && raycaster.gameObject.scene.isLoaded
                        && raycaster.enabled)
                    {
                        disabledGraphicRaycasters.Add(raycaster);
                        raycaster.enabled = false;
                    }
                }
            }
        }

        private static void QueueMouseState(Mouse mouse, Vector2 position, bool leftDown)
        {
            var state = new MouseState { position = position };
            if (leftDown)
            {
                state = state.WithButton(MouseButton.Left);
            }

            InputSystem.QueueStateEvent(mouse, state);
        }

        private sealed class PointerDownRecorder : MonoBehaviour, IPointerDownHandler
        {
            public int PointerId { get; private set; } = int.MinValue;

            public void OnPointerDown(PointerEventData eventData)
            {
                PointerId = eventData.pointerId;
            }
        }

        private sealed class InputFocusIsolationScope : System.IDisposable
        {
            private readonly InputSettings.BackgroundBehavior originalBackgroundBehavior;
            private readonly InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
            private readonly bool originalRunInBackground;

            public InputFocusIsolationScope()
            {
                originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
                originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                originalRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            }

            public void Dispose()
            {
                InputSystem.settings.backgroundBehavior = originalBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
                Application.runInBackground = originalRunInBackground;
            }
        }
    }
}

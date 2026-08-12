using System.Collections;
using System.Collections.Generic;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Foundation;
using AnimalCafe.Core.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase5ContainerNavigationPlayModeTests
    {
        [UnityTest]
        public IEnumerator IT010_ConfirmTouch_InvokesOnceAndClosesOnlyTopModal()
        {
            using (var fixture = new ContainerTouchFixture())
            {
                var navigation = new UiNavigationCoordinator();
                var lowerState = CriticalModalState("Lower");
                var topState = CriticalModalState("Top");
                var lower = fixture.CreateModal("Lower", new Vector2(-250f, 0f));
                var top = fixture.CreateModal("Top", new Vector2(250f, 0f));
                var confirmCount = 0;
                lower.View.Configure(navigation, lowerState, lower.Confirm, lower.Cancel, lower.Outside, false);
                top.View.Configure(navigation, topState, top.Confirm, top.Cancel, top.Outside, false);
                top.View.Confirmed += () => confirmCount++;
                lower.View.Open();
                top.View.Open();

                fixture.QueueTap(top.ConfirmPosition);
                yield return null;
                yield return null;

                Assert.That(confirmCount, Is.EqualTo(1));
                Assert.That(topState.IsOpen, Is.False);
                Assert.That(lowerState.IsOpen, Is.True);

                fixture.QueueTap(top.ConfirmPosition);
                yield return null;
                yield return null;

                Assert.That(confirmCount, Is.EqualTo(1));
                Assert.That(lowerState.IsOpen, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator IT013_OrdinaryBottomSheet_ClosesThroughOutsideTouchAndSharedBack()
        {
            using (var fixture = new ContainerTouchFixture())
            {
                var navigation = new UiNavigationCoordinator();
                var state = new UiView(
                    "OrdinarySheet", UiViewKind.BottomSheet, UiPausePolicy.ContinueGame,
                    UiOutsideDismissPolicy.Dismissible);
                var sheet = fixture.CreateBottomSheet();
                sheet.View.Configure(navigation, state, sheet.Outside);
                sheet.View.Open();

                fixture.QueueTap(sheet.OutsidePosition);
                yield return null;
                yield return null;
                Assert.That(state.IsOpen, Is.False);

                sheet.View.Open();
                Assert.That(sheet.View.TryHandleBack(), Is.True);
                Assert.That(state.IsOpen, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator IT014_CriticalModal_OutsideAndBlockedBackStayOpenButCancelTouchCloses()
        {
            using (var fixture = new ContainerTouchFixture())
            {
                var navigation = new UiNavigationCoordinator();
                var state = CriticalModalState("Critical");
                var modal = fixture.CreateModal("Critical", Vector2.zero);
                modal.View.Configure(navigation, state, modal.Confirm, modal.Cancel, modal.Outside, false);
                modal.View.Open();

                fixture.QueueTap(modal.OutsidePosition);
                yield return null;
                yield return null;
                Assert.That(state.IsOpen, Is.True);
                Assert.That(modal.View.TryHandleBack(), Is.False);
                Assert.That(state.IsOpen, Is.True);

                fixture.QueueTap(modal.CancelPosition);
                yield return null;
                yield return null;
                Assert.That(state.IsOpen, Is.False);

                modal.View.Configure(
                    navigation, state, modal.Confirm, modal.Cancel, modal.Outside, true);
                modal.View.Open();
                Assert.That(modal.View.TryHandleBack(), Is.True);
                Assert.That(state.IsOpen, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator IT027_DisableDuringTransition_ReleasesEveryOwnedInteractionResource()
        {
            using (var fixture = new ContainerTouchFixture())
            {
                var navigation = new UiNavigationCoordinator();
                var pointerBoundary = new UiPointerBoundary();
                var gameTime = new FakeGameTimeService(GameSpeed.Normal);
                var pause = new UiPauseCoordinator(gameTime);
                var frost = new StrongFrostLease(isStrongFrostSupported: true);
                var state = CriticalModalState("Interruptible");
                var modal = fixture.CreateModal("Interruptible", Vector2.zero);
                modal.Panel.Configure(fixture.Theme, UiPanelStyle.StrongFrost, frost);
                modal.View.Configure(
                    navigation, state, modal.Confirm, modal.Cancel, modal.Outside, false);
                modal.View.ConfigureLifecycle(
                    pause, pointerBoundary, modal.Group, new UiTransitionRunner(() => false), 1f);
                modal.View.Open();
                yield return null;

                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
                Assert.That(modal.Group.blocksRaycasts, Is.True);

                fixture.QueueTouchBegan(modal.ContentPosition);
                yield return null;
                Assert.That(
                    pointerBoundary.GetOwnership(fixture.PointerId),
                    Is.EqualTo(UiPointerOwnership.Ui));

                modal.Root.SetActive(false);
                yield return null;

                Assert.That(modal.Group.blocksRaycasts, Is.False);
                Assert.That(gameTime.CurrentSpeed, Is.EqualTo(GameSpeed.Normal));
                Assert.That(pointerBoundary.GetOwnership(fixture.PointerId), Is.EqualTo(UiPointerOwnership.None));
                Assert.That(pointerBoundary.CanProcessScenePointer(fixture.PointerId), Is.True);
                Assert.That(state.IsOpen, Is.False);
                var nextOwner = frost.Acquire(new object());
                Assert.That(nextOwner.ResolvedStyle, Is.EqualTo(UiPanelStyle.StrongFrost));
                nextOwner.Dispose();
            }
        }

        private static UiView CriticalModalState(string id)
        {
            return new UiView(
                id, UiViewKind.Modal, UiPausePolicy.PauseGame,
                UiOutsideDismissPolicy.NotDismissible);
        }

        private static Material CreateMaterial()
        {
            return new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        private sealed class ContainerTouchFixture : System.IDisposable
        {
            private readonly GameObject canvasObject;
            private readonly GameObject eventSystemObject;
            private readonly List<GameObject> disabledEventSystems = new List<GameObject>();
            private readonly List<GameObject> ownedObjects = new List<GameObject>();
            private readonly InputSystemUIInputModule inputModule;
            private readonly Touchscreen touchscreen;
            private readonly InputSettings.BackgroundBehavior originalBackgroundBehavior;
            private readonly InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
            private readonly bool originalRunInBackground;
            private int touchId;

            public AnimalCafeUiTheme Theme { get; }
            // InputSystemUIInputModule composes Touch pointerId from deviceId + touchId.
            // Touch 的 EventSystem pointerId 不是单纯的 deviceId。
            public int PointerId => (touchscreen.deviceId << 24) + touchId;

            public ContainerTouchFixture()
            {
                originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
                originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                originalRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

                foreach (var existing in Resources.FindObjectsOfTypeAll<EventSystem>())
                {
                    if (existing.gameObject.scene.IsValid() && existing.gameObject.scene.isLoaded
                        && existing.gameObject.activeSelf)
                    {
                        disabledEventSystems.Add(existing.gameObject);
                        existing.gameObject.SetActive(false);
                    }
                }

                canvasObject = new GameObject(
                    "ContainerCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Theme = ScriptableObject.CreateInstance<AnimalCafeUiTheme>();
                Theme.Materials = new UiMaterialTokens(
                    CreateMaterial(), CreateMaterial(), CreateMaterial(), CreateMaterial());
                eventSystemObject = new GameObject("ContainerEventSystem");
                eventSystemObject.AddComponent<EventSystem>();
                inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
                inputModule.UnassignActions();
                inputModule.AssignDefaultActions();
                touchscreen = InputSystem.AddDevice<Touchscreen>();
                Canvas.ForceUpdateCanvases();
            }

            public ModalFixture CreateModal(string name, Vector2 offset)
            {
                var root = CreateRoot(name + "Modal");
                var outside = CreateButton(name + "Outside", offset + new Vector2(0f, 120f));
                var confirm = CreateButton(name + "Confirm", offset + new Vector2(0f, 0f));
                var cancel = CreateButton(name + "Cancel", offset + new Vector2(0f, -120f));
                return new ModalFixture(
                    root.AddComponent<AnimalCafeModalView>(), outside.Button, confirm.Button, cancel.Button,
                    outside.Position, confirm.Position, cancel.Position, root,
                    root.GetComponent<CanvasGroup>(), root.GetComponent<AnimalCafePanelView>(),
                    new Vector2(Screen.width * 0.5f + 200f, Screen.height * 0.5f));
            }

            public BottomSheetFixture CreateBottomSheet()
            {
                var root = CreateRoot("BottomSheet");
                var outside = CreateButton("SheetOutside", Vector2.zero);
                return new BottomSheetFixture(
                    root.AddComponent<AnimalCafeBottomSheetView>(), outside.Button, outside.Position);
            }

            public void QueueTap(Vector2 position)
            {
                touchId++;
                QueueTouch(position, InputTouchPhase.Began);
                InputSystem.Update();
                QueueTouch(position, InputTouchPhase.Ended);
            }

            public void QueueTouchBegan(Vector2 position)
            {
                touchId++;
                QueueTouch(position, InputTouchPhase.Began);
            }

            private GameObject CreateRoot(string name)
            {
                var root = new GameObject(
                    name, typeof(RectTransform), typeof(CanvasRenderer), typeof(CanvasGroup));
                root.transform.SetParent(canvasObject.transform, false);
                var rect = root.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                root.AddComponent<Image>();
                root.AddComponent<AnimalCafePanelView>();
                ownedObjects.Add(root);
                return root;
            }

            private ButtonFixture CreateButton(string name, Vector2 centeredOffset)
            {
                var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
                buttonObject.transform.SetParent(canvasObject.transform, false);
                var rect = buttonObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = centeredOffset;
                rect.sizeDelta = new Vector2(100f, 100f);
                buttonObject.AddComponent<Image>();
                var button = buttonObject.AddComponent<Button>();
                return new ButtonFixture(
                    button, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) + centeredOffset);
            }

            private void QueueTouch(Vector2 position, InputTouchPhase phase)
            {
                InputSystem.QueueStateEvent(
                    touchscreen,
                    new TouchState
                    {
                        touchId = touchId,
                        phase = phase,
                        position = position,
                        pressure = phase == InputTouchPhase.Ended ? 0f : 1f
                    });
            }

            public void Dispose()
            {
                inputModule.UnassignActions();
                if (touchscreen.added)
                {
                    InputSystem.RemoveDevice(touchscreen);
                }

                foreach (var owned in ownedObjects)
                {
                    Object.DestroyImmediate(owned);
                }

                Object.DestroyImmediate(eventSystemObject);
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(Theme.Materials.Solid);
                Object.DestroyImmediate(Theme.Materials.LightFrost);
                Object.DestroyImmediate(Theme.Materials.StrongFrost);
                Object.DestroyImmediate(Theme.Materials.StrongFrostFallback);
                Object.DestroyImmediate(Theme);
                foreach (var disabled in disabledEventSystems)
                {
                    if (disabled != null)
                    {
                        disabled.SetActive(true);
                    }
                }

                InputSystem.settings.backgroundBehavior = originalBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
                Application.runInBackground = originalRunInBackground;
            }
        }

        private readonly struct ButtonFixture
        {
            public ButtonFixture(Button button, Vector2 position) { Button = button; Position = position; }
            public Button Button { get; }
            public Vector2 Position { get; }
        }

        private readonly struct ModalFixture
        {
            public ModalFixture(AnimalCafeModalView view, Button outside, Button confirm, Button cancel,
                Vector2 outsidePosition, Vector2 confirmPosition, Vector2 cancelPosition,
                GameObject root, CanvasGroup group, AnimalCafePanelView panel, Vector2 contentPosition)
            {
                View = view; Outside = outside; Confirm = confirm; Cancel = cancel;
                OutsidePosition = outsidePosition; ConfirmPosition = confirmPosition;
                CancelPosition = cancelPosition;
                Root = root; Group = group; Panel = panel; ContentPosition = contentPosition;
            }
            public AnimalCafeModalView View { get; }
            public Button Outside { get; }
            public Button Confirm { get; }
            public Button Cancel { get; }
            public Vector2 OutsidePosition { get; }
            public Vector2 ConfirmPosition { get; }
            public Vector2 CancelPosition { get; }
            public GameObject Root { get; }
            public CanvasGroup Group { get; }
            public AnimalCafePanelView Panel { get; }
            public Vector2 ContentPosition { get; }
        }

        private sealed class FakeGameTimeService : IGameTimeService
        {
            public FakeGameTimeService(GameSpeed speed) { CurrentSpeed = speed; }
            public GameSpeed CurrentSpeed { get; private set; }
            public bool TrySetSpeed(GameSpeed speed) { CurrentSpeed = speed; return true; }
        }

        private readonly struct BottomSheetFixture
        {
            public BottomSheetFixture(AnimalCafeBottomSheetView view, Button outside, Vector2 position)
            { View = view; Outside = outside; OutsidePosition = position; }
            public AnimalCafeBottomSheetView View { get; }
            public Button Outside { get; }
            public Vector2 OutsidePosition { get; }
        }
    }
}

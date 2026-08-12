using System.Collections;
using System.Collections.Generic;
using AnimalCafe.UI.Feedback;
using NUnit.Framework;
using TMPro;
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
    public sealed class Phase5FeedbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator IT018_ToastView_AdvancesMergedQueueWithUnscaledTimeWhilePaused()
        {
            var originalTimeScale = Time.timeScale;
            var root = CreateUiObject("ToastRoot");
            var background = root.AddComponent<Image>();
            var labelObject = CreateUiObject("ToastLabel", root.transform);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            var view = root.AddComponent<ToastView>();
            var queue = new ToastQueue(() => Time.unscaledTime);

            try
            {
                view.Configure(queue, label, new Graphic[] { background, label });
                queue.Enqueue(new ToastMessage(ToastType.Info, "Saved", ToastPriority.Normal, 0.05f));
                queue.Enqueue(new ToastMessage(ToastType.Info, "Saved", ToastPriority.Normal, 0.05f));
                queue.Enqueue(new ToastMessage(ToastType.Success, "Ready", ToastPriority.Normal, 0.2f));
                Time.timeScale = 0f;

                yield return null;

                Assert.That(view.IsVisible, Is.True);
                Assert.That(label.text, Does.Contain("Saved"));
                Assert.That(label.text, Does.Contain("2"));
                Assert.That(background.raycastTarget, Is.False);
                Assert.That(label.raycastTarget, Is.False);

                yield return new WaitForSecondsRealtime(0.08f);
                yield return null;

                Assert.That(view.IsVisible, Is.True);
                Assert.That(label.text, Does.Contain("Ready"));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator IT019_TooltipView_RequiresTapAndHasNoHoverDependency()
        {
            using (var fixture = new TooltipTouchFixture())
            {
                fixture.View.SetMessage("Tap to learn about syrup slots");

                yield return null;
                yield return null;

                Assert.That(
                    fixture.Content.activeSelf,
                    Is.False,
                    "Tooltip must not depend on Hover.");
                Assert.That(fixture.View, Is.Not.InstanceOf<IPointerEnterHandler>());
                Assert.That(fixture.TopRaycastTarget(), Is.SameAs(fixture.View.gameObject));

                fixture.QueueTouch(InputTouchPhase.Began);
                yield return null;
                fixture.QueueTouch(InputTouchPhase.Ended);
                yield return null;
                yield return null;

                Assert.That(fixture.Content.activeSelf, Is.True);
                Assert.That(
                    fixture.Label.text,
                    Is.EqualTo("Tap to learn about syrup slots"));

                fixture.View.Close();
                Assert.That(fixture.Content.activeSelf, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator ToastView_Configure_DisablesEveryDescendantGraphicRaycastTarget()
        {
            var root = CreateUiObject("ToastRaycastRoot");
            var listedBackground = root.AddComponent<Image>();
            var labelObject = CreateUiObject("ToastRaycastLabel", root.transform);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            var unlistedObject = CreateUiObject("UnlistedInactiveGraphic", root.transform);
            var unlistedGraphic = unlistedObject.AddComponent<Image>();
            unlistedObject.SetActive(false);
            var view = root.AddComponent<ToastView>();

            try
            {
                Assert.That(unlistedGraphic.raycastTarget, Is.True);

                view.Configure(
                    new ToastQueue(() => Time.unscaledTime),
                    label,
                    new Graphic[] { listedBackground });
                yield return null;

                Assert.That(listedBackground.raycastTarget, Is.False);
                Assert.That(label.raycastTarget, Is.False);
                Assert.That(unlistedGraphic.raycastTarget, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator ToastView_DestroyedMessageLabel_HidesSafelyWithoutLogSpam()
        {
            var root = CreateUiObject("ToastDestroyedLabelRoot");
            var background = root.AddComponent<Image>();
            var labelObject = CreateUiObject("ToastDestroyedLabel", root.transform);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            var view = root.AddComponent<ToastView>();
            var queue = new ToastQueue(() => Time.unscaledTime);

            try
            {
                view.Configure(queue, label, new Graphic[] { background, label });
                queue.Enqueue(NormalToast("Still queued"));
                yield return null;
                Assert.That(view.IsVisible, Is.True);

                Object.DestroyImmediate(labelObject);
                yield return null;
                yield return null;

                LogAssert.NoUnexpectedReceived();
                Assert.That(view.IsVisible, Is.False);
                Assert.That(background.enabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator IT020_ValidationMessage_PersistsSpecificReasonUntilInputIsCorrected()
        {
            var root = CreateUiObject("ValidationMessage");
            var label = root.AddComponent<TextMeshProUGUI>();
            var view = root.AddComponent<ValidationMessageView>();

            try
            {
                view.Configure(label);
                view.SetValidationResult(false, "Choose a coffee bean for this machine");

                yield return null;
                yield return null;

                Assert.That(view.IsVisible, Is.True);
                Assert.That(label.text, Is.EqualTo("Choose a coffee bean for this machine"));

                view.SetValidationResult(false, "Choose a coffee bean for this machine");
                yield return null;

                Assert.That(view.IsVisible, Is.True);
                Assert.That(label.text, Is.EqualTo("Choose a coffee bean for this machine"));

                view.SetValidationResult(true, null);
                yield return null;

                Assert.That(view.IsVisible, Is.False);
                Assert.That(label.text, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateUiObject(string name, Transform parent = null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject;
        }

        private static ToastMessage NormalToast(string content)
        {
            return new ToastMessage(ToastType.Info, content, ToastPriority.Normal, 3f);
        }

        private sealed class TooltipTouchFixture : System.IDisposable
        {
            private readonly GameObject canvasObject;
            private readonly GameObject eventSystemObject;
            private readonly List<GameObject> disabledEventSystems = new List<GameObject>();
            private readonly InputSystemUIInputModule inputModule;
            private readonly Touchscreen touchscreen;
            private readonly InputSettings.BackgroundBehavior originalBackgroundBehavior;
            private readonly InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
            private readonly bool originalRunInBackground;
            private readonly EventSystem eventSystem;

            public TooltipTouchFixture()
            {
                originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
                originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                originalRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior =
                    InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

                foreach (var existing in Resources.FindObjectsOfTypeAll<EventSystem>())
                {
                    if (existing.gameObject.scene.IsValid()
                        && existing.gameObject.scene.isLoaded
                        && existing.gameObject.activeSelf)
                    {
                        disabledEventSystems.Add(existing.gameObject);
                        existing.gameObject.SetActive(false);
                    }
                }

                canvasObject = new GameObject(
                    "TooltipTouchCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(GraphicRaycaster));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

                var action = CreateUiObject("TooltipInfoAction", canvasObject.transform);
                var actionRect = action.GetComponent<RectTransform>();
                actionRect.anchorMin = new Vector2(0.5f, 0.5f);
                actionRect.anchorMax = new Vector2(0.5f, 0.5f);
                actionRect.sizeDelta = new Vector2(160f, 160f);
                action.AddComponent<Image>();
                View = action.AddComponent<TooltipView>();

                Content = CreateUiObject("TooltipContent", action.transform);
                Label = Content.AddComponent<TextMeshProUGUI>();
                View.Configure(Label, Content);

                eventSystemObject = new GameObject("TooltipTouchEventSystem");
                eventSystem = eventSystemObject.AddComponent<EventSystem>();
                inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
                inputModule.UnassignActions();
                inputModule.AssignDefaultActions();
                touchscreen = InputSystem.AddDevice<Touchscreen>();
                Canvas.ForceUpdateCanvases();
            }

            public TooltipView View { get; }

            public GameObject Content { get; }

            public TMP_Text Label { get; }

            public GameObject TopRaycastTarget()
            {
                var results = new List<RaycastResult>();
                eventSystem.RaycastAll(
                    new PointerEventData(eventSystem)
                    {
                        position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                    },
                    results);
                return results.Count > 0 ? results[0].gameObject : null;
            }

            public void QueueTouch(InputTouchPhase phase)
            {
                InputSystem.QueueStateEvent(
                    touchscreen,
                    new TouchState
                    {
                        touchId = 1,
                        phase = phase,
                        position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                        pressure = phase == InputTouchPhase.Ended ? 0f : 1f,
                    });
            }

            public void Dispose()
            {
                inputModule.UnassignActions();
                if (touchscreen.added)
                {
                    InputSystem.RemoveDevice(touchscreen);
                }

                Object.DestroyImmediate(eventSystemObject);
                Object.DestroyImmediate(canvasObject);
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
    }
}

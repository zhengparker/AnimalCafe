#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using AnimalCafe.UI.Feedback;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase5UiFoundationRealTouchTests : InputTestFixture
    {
        [UnityTest]
        public IEnumerator VirtualTouch_UsesSceneEventSystemRaycastAndInvokesToastExactlyOnce()
        {
            EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Validation/Phase5UiFoundation.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene = SceneManager.GetActiveScene();
            var system = Find<EventSystem>(scene, "EventSystem");
            var inputModule = system.GetComponent<InputSystemUIInputModule>();
            Assert.That(inputModule, Is.Not.Null);
            Assert.That(inputModule.actionsAsset, Is.Not.Null,
                "Generated validation scene must configure its own InputSystem UI actions.");
            var touch = InputSystem.AddDevice<Touchscreen>();
            try
            {
                var feedbackSelector = Find<Button>(scene, "Feedback Page Selector");
                yield return Tap(touch, Center(feedbackSelector));
                Assert.That(VisiblePageNames(scene), Is.EqualTo(new[] { "Feedback Page" }),
                    "The real InputSystem route must select Feedback before using its controls.");
            var button = Find<Button>(scene, "Show Toast Button");
            Canvas.ForceUpdateCanvases();
            var rectTransform = button.transform as RectTransform;
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var position = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * 0.5f);
            var canvas = button.GetComponentInParent<Canvas>();
            Assert.That(rectTransform.rect.size.x, Is.GreaterThanOrEqualTo(48f));
            Assert.That(rectTransform.rect.size.y, Is.GreaterThanOrEqualTo(48f));
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(canvas.enabled, Is.True);
            Assert.That(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>().enabled, Is.True);
            Assert.That(position.x, Is.InRange(0f, Screen.width));
            Assert.That(position.y, Is.InRange(0f, Screen.height));
            Assert.That(EventSystem.current, Is.SameAs(system));
            Assert.That(button.targetGraphic, Is.Not.Null);
            Assert.That(button.targetGraphic.enabled, Is.True);
            Assert.That(button.targetGraphic.raycastTarget, Is.True);
            Assert.That(button.targetGraphic.canvas, Is.EqualTo(canvas));
            Assert.That(canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true),
                Does.Contain(button.targetGraphic));
            for (var frame = 0; frame < 3 && button.targetGraphic.depth < 0; frame++)
            {
                yield return null;
            }
            Assert.That(button.targetGraphic.depth, Is.GreaterThanOrEqualTo(0),
                "Generated UI must register controls with its Canvas without test-time repair.");
            Assert.That(button.targetGraphic.canvasRenderer.cull, Is.False);
            Assert.That(button.GetComponentsInParent<CanvasGroup>(true)
                .All(group => group.enabled && group.blocksRaycasts && group.interactable), Is.True);
            Assert.That(corners.All(corner => !float.IsNaN(corner.x) && !float.IsNaN(corner.y)), Is.True);
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(rectTransform, position, null), Is.True);
            var raycasts = new System.Collections.Generic.List<RaycastResult>();
            var eventData = new PointerEventData(system) { position = position };
            canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>().Raycast(eventData, raycasts);
            Assert.That(raycasts.FirstOrDefault().gameObject, Is.EqualTo(button.gameObject),
                "Owning GraphicRaycaster must hit the actual button center before EventSystem aggregation.");
            raycasts.Clear();
            system.RaycastAll(eventData, raycasts);
            Assert.That(raycasts.FirstOrDefault().gameObject, Is.EqualTo(button.gameObject),
                "Actual button center must be the EventSystem's top raycast target.");
            Assert.That(inputModule.point.action.enabled, Is.True);
            Assert.That(inputModule.leftClick.action.enabled, Is.True);
                var recorder = button.gameObject.AddComponent<PointerRecorder>();
                QueueTouch(touch, 1, InputTouchPhase.Began, position);
                InputSystem.Update();
                yield return null;

                Assert.That(recorder.DownCount, Is.EqualTo(1),
                    "Virtual Touch must reach the actual button through InputSystemUIInputModule.");
                Assert.That(recorder.ClickCount, Is.Zero,
                    "A touch press must not dispatch click before its release.");
                QueueTouch(touch, 1, InputTouchPhase.Ended, position);
                InputSystem.Update();
                yield return null;

                Assert.That(recorder.ClickCount, Is.EqualTo(1),
                    "Virtual Touch must release as one real uGUI click.");
                Assert.That(Find<ToastView>(scene, "Toast Fixture")
                    .GetComponentInChildren<TMPro.TMP_Text>(true).text, Does.Contain("Saved"));
            }
            finally { InputSystem.RemoveDevice(touch); }
        }

        private static IEnumerator Tap(Touchscreen device, Vector2 position)
        {
            QueueTouch(device, 1, InputTouchPhase.Began, position);
            InputSystem.Update();
            yield return null;
            QueueTouch(device, 1, InputTouchPhase.Ended, position);
            InputSystem.Update();
            yield return null;
        }

        private static Vector2 Center(Button button)
        {
            var rectTransform = (RectTransform)button.transform;
            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            return RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * 0.5f);
        }

        private static string[] VisiblePageNames(Scene scene) => new[]
            { "Buttons Page", "Panels Page", "Navigation Page", "Feedback Page", "Responsive Motion Page" }
            .Where(name => Find<Transform>(scene, name).gameObject.activeInHierarchy)
            .ToArray();

        private static void QueueTouch(Touchscreen device, int id, InputTouchPhase phase, Vector2 position) =>
            InputSystem.QueueStateEvent(device, new TouchState
            {
                touchId = id,
                phase = phase,
                position = position,
                pressure = phase == InputTouchPhase.Ended ? 0f : 1f
            });

        private static T Find<T>(Scene scene, string name) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Single(transform => transform.name == name).GetComponent<T>();

        private sealed class PointerRecorder : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
        {
            public int DownCount { get; private set; }
            public int ClickCount { get; private set; }
            public void OnPointerDown(PointerEventData eventData) => DownCount++;
            public void OnPointerClick(PointerEventData eventData) => ClickCount++;
        }
    }
}
#endif

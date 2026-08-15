#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using AnimalCafe.Diagnostics;
using AnimalCafe.Interaction;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
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
    public sealed class Phase5UiFoundationScenePlayModeTests : InputTestFixture
    {
        [UnityTearDown]
        public IEnumerator UnloadSceneBeforeInputFixtureRestoresGlobalInput()
        {
            var validationScene = SceneManager.GetActiveScene();
            var cleanupScene = SceneManager.CreateScene("Phase5UiFoundationTestCleanup");
            SceneManager.SetActiveScene(cleanupScene);
            var unload = SceneManager.UnloadSceneAsync(validationScene);
            while (!unload.isDone)
                yield return null;
        }

        [UnityTest]
        public IEnumerator ValidationScene_LoadsWithInteractiveEvidenceFixtures()
        {
            EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Validation/Phase5UiFoundation.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            var scene = SceneManager.GetActiveScene();

            Assert.That(FindAll<EventSystem>(scene), Has.Length.EqualTo(1));
            Assert.That(Find(scene, "Selectable Coffee Machine").GetComponent<ColorSelectable>(), Is.Not.Null);
            Assert.That(Find(scene, "Scaled Time Mover").GetComponent<ManualReviewPingPongMover>(), Is.Not.Null);
            Assert.That(Find(scene, "Long Localized Label").GetComponent<TMP_Text>().text,
                Does.Contain("Coffee Bean").And.Contain("咖啡机"));
            Assert.That(Find(scene, "Safe Area").GetComponent<SafeAreaContainer>(), Is.Not.Null);
            Assert.That(Find(scene, "Toast Fixture").GetComponent<ToastView>(), Is.Not.Null);
            Assert.That(Find(scene, "Tooltip Fixture").GetComponent<TooltipView>(), Is.Not.Null);
            Assert.That(Find(scene, "Validation Message Fixture").GetComponent<ValidationMessageView>(), Is.Not.Null);
            var toast = Find(scene, "Toast Fixture");
            var tooltip = Find(scene, "Tooltip Fixture");
            var validation = Find(scene, "Validation Message Fixture");
            var sheet = Find(scene, "Bottom Sheet Fixture");
            Assert.That(sheet.activeSelf, Is.False);

            Assert.That(Find(scene, "EventSystem").GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            try
            {
                yield return Click(mouse, Find(scene, "Feedback Page Selector").GetComponent<Button>());
                Assert.That(VisiblePageNames(scene), Is.EqualTo(new[] { "Feedback Page" }),
                    "The real InputSystem route must select Feedback before using its controls.");

            Find(scene, "Show Toast Button").GetComponent<Button>().onClick.Invoke();
            Find(scene, "Show Tooltip Button").GetComponent<Button>().onClick.Invoke();
            Find(scene, "Show Validation Error Button").GetComponent<Button>().onClick.Invoke();
            Find(scene, "Open Bottom Sheet Button").GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.That(toast.GetComponentInChildren<TMP_Text>(true).text, Does.Contain("Saved"));
            Assert.That(tooltip.transform.Find("Content").gameObject.activeSelf, Is.True);
            Assert.That(validation.GetComponent<ValidationMessageView>().IsVisible, Is.True);
            Assert.That(validation.GetComponentInChildren<TMP_Text>(true).text, Does.Contain("required"));
            Assert.That(sheet.activeSelf, Is.True);
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator ScaledTimeMover_AdvancesAtNormalTime()
        {
            EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/Validation/Phase5UiFoundation.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var mover = Find(SceneManager.GetActiveScene(), "Scaled Time Mover");
            var start = mover.transform.localPosition;

            yield return new WaitForSecondsRealtime(0.15f);

            Assert.That(mover.transform.localPosition, Is.Not.EqualTo(start));
        }

        private static GameObject Find(Scene scene, string name) =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Single(transform => transform.name == name).gameObject;

        private static T[] FindAll<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static IEnumerator Click(Mouse mouse, Button button)
        {
            var position = Center(button);
            var raycasts = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, raycasts);
            Assert.That(raycasts, Is.Not.Empty, button.name + " must be raycastable.");
            Assert.That(
                raycasts[0].gameObject == button.gameObject
                || raycasts[0].gameObject.transform.IsChildOf(button.transform),
                Is.True, button.name + " must be the top raycast target.");
            QueueMouseState(mouse, position, true);
            InputSystem.Update();
            yield return null;
            yield return null;
            QueueMouseState(mouse, position, false);
            InputSystem.Update();
            yield return null;
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
            .Where(name => Find(scene, name).activeInHierarchy)
            .ToArray();

        private static void QueueMouseState(Mouse mouse, Vector2 position, bool leftDown)
        {
            var state = new MouseState { position = position };
            if (leftDown) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state);
        }
    }
}
#endif

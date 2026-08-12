#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using AnimalCafe.Interaction;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase5UiFoundationRound2RealInputTests : InputTestFixture
    {
        private const string ScenePath = "Assets/Scenes/Validation/Phase5UiFoundation.unity";

        [UnityTearDown]
        public IEnumerator RestoreGlobalRuntimeState()
        {
            Time.timeScale = 1f;
            var activeScene = SceneManager.GetActiveScene();
            foreach (var controller in FindAll<Phase5UiFoundationReviewController>(activeScene))
                if (controller != null) controller.enabled = false;
            foreach (var device in InputSystem.devices.ToArray())
                if (device is Mouse or Keyboard or Touchscreen)
                    InputSystem.RemoveDevice(device);
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator VirtualMouse_AllFourEvidenceControls_UseRealEventSystemClicks()
        {
            yield return LoadScene();
            var scene = SceneManager.GetActiveScene();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return Click(mouse, Find<Button>(scene, "Show Toast Button"));
                Assert.That(Find<ToastView>(scene, "Toast Fixture")
                    .GetComponentInChildren<TMPro.TMP_Text>(true).text, Does.Contain("Saved"));

                yield return Click(mouse, Find<Button>(scene, "Show Tooltip Button"));
                Assert.That(Find(scene, "Tooltip Fixture").transform.Find("Content").gameObject.activeSelf, Is.True);

                yield return Click(mouse, Find<Button>(scene, "Show Validation Error Button"));
                Assert.That(Find<ValidationMessageView>(scene, "Validation Message Fixture").IsVisible, Is.True);

                yield return Click(mouse, Find<Button>(scene, "Open Bottom Sheet Button"));
                Assert.That(Find(scene, "Bottom Sheet Fixture").activeSelf, Is.True);
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator VirtualMouse_SharedBoundary_AllowsWorldSelectionButBlocksUiClickThrough()
        {
            yield return LoadScene();
            var scene = SceneManager.GetActiveScene();
            var controller = Find<SceneInteractionController>(scene, "Scene Interaction Controller");
            var camera = Find<UnityEngine.Camera>(scene, "Main Camera");
            var world = Find(scene, "Selectable Coffee Machine");
            var occludingButton = Find<Button>(scene, "World Occlusion Test Button");
            var worldPosition = camera.WorldToScreenPoint(world.transform.position);
            var buttonPosition = Center(occludingButton);
            Assert.That(Vector2.Distance(worldPosition, buttonPosition), Is.LessThan(20f),
                "The fixed validation button must intentionally cover the selectable world object.");

            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                occludingButton.gameObject.SetActive(false);
                yield return Click(mouse, worldPosition);
                Assert.That(controller.CurrentSelection, Is.Not.Null,
                    "A real world tap must select through the generated Mouse input chain.");
                controller.ClearSelection();

                occludingButton.gameObject.SetActive(true);
                yield return null;
                yield return Click(mouse, occludingButton);
                Assert.That(controller.CurrentSelection, Is.Null,
                    "A UI press/release over selectable world geometry must not pass through.");

                yield return PressMoveRelease(mouse, Center(occludingButton), worldPosition);
                Assert.That(controller.CurrentSelection, Is.Null,
                    "A UI-to-world drag must retain UI ownership through release.");
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator VirtualMouse_ReviewFixtures_AreFunctionalNotNameOnly()
        {
            yield return LoadScene();
            var scene = SceneManager.GetActiveScene();
            Assert.That(scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Any(component => component.GetType().Name == "Phase5UiFoundationReviewController"), Is.True,
                "The scene requires one runtime controller that binds the manual-review workflows.");
            Assert.That(Find(scene, "Safe Area Confirm Button").transform.IsChildOf(
                Find(scene, "Safe Area").transform), Is.True);

            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            try
            {
                QueueMouseState(mouse, Vector2.zero, false);
                yield return null;
                var mover = Find(scene, "Scaled Time Mover").transform;
                yield return Click(mouse, Find<Button>(scene, "Pause Game Button"));
                var pausedPosition = mover.position;
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.That(mover.position, Is.EqualTo(pausedPosition));

                yield return Click(mouse, Find<Button>(scene, "Continue Game Button"));
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.That(mover.position, Is.Not.EqualTo(pausedPosition));

                yield return Click(mouse, Find<Button>(scene, "Reduced Motion Toggle"));
                yield return null;
                Assert.That(Find(scene, "Reduced Motion Status").GetComponent<TMPro.TMP_Text>().text,
                    Does.Contain("On"));

                yield return Click(mouse, Find<Button>(scene, "Open Second Strong Frost Button"));
                Assert.That(Find<AnimalCafePanelView>(scene, "Second Strong Frost Fixture").ResolvedStyle,
                    Is.EqualTo(AnimalCafe.UI.Foundation.UiPanelStyle.LightFrost));

                yield return Click(mouse, Find<Button>(scene, "Open Modal Button"));
                Assert.That(Find(scene, "Modal Fixture").GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator VirtualMouse_BottomSheetOutsideCloseAndValidationRepair_AreExecutable()
        {
            yield return LoadScene();
            var scene = SceneManager.GetActiveScene();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return Click(mouse, Find<Button>(scene, "Show Validation Error Button"));
                var validation = Find<ValidationMessageView>(scene, "Validation Message Fixture");
                Assert.That(validation.IsVisible, Is.True);
                yield return Click(mouse, Find<Button>(scene, "Validation Repair Button"));
                Assert.That(validation.IsVisible, Is.False);

                yield return Click(mouse, Find<Button>(scene, "Open Bottom Sheet Button"));
                var sheet = Find(scene, "Bottom Sheet Fixture");
                Assert.That(sheet.activeSelf, Is.True);
                var outside = sheet.transform.Find("OutsideButton").GetComponent<Button>();
                var sheetGroup = sheet.GetComponent<CanvasGroup>();
                var openDeadline = Time.realtimeSinceStartup + 2f;
                while (Time.realtimeSinceStartup < openDeadline
                       && (sheetGroup.alpha < 0.99f || outside.targetGraphic.depth < 0))
                    yield return null;
                Assert.That(sheetGroup.alpha, Is.GreaterThanOrEqualTo(0.99f),
                    "Outside input is available after the unscaled open transition completes.");
                Assert.That(outside.targetGraphic.depth, Is.GreaterThanOrEqualTo(0),
                    "Activated Bottom Sheet graphics must register with the owning Canvas.");
                var outsideCorners = new Vector3[4];
                ((RectTransform)outside.transform).GetWorldCorners(outsideCorners);
                var raycasts = new System.Collections.Generic.List<RaycastResult>();
                var sampledTopNames = new System.Collections.Generic.List<string>();
                var outsidePoint = Vector2.zero;
                var foundOutsidePoint = false;
                for (var y = 1; y < 10 && !foundOutsidePoint; y++)
                for (var x = 1; x < 10 && !foundOutsidePoint; x++)
                {
                    var candidate = new Vector2(
                        Mathf.Lerp(outsideCorners[0].x, outsideCorners[2].x, x / 10f),
                        Mathf.Lerp(outsideCorners[0].y, outsideCorners[2].y, y / 10f));
                    raycasts.Clear();
                    EventSystem.current.RaycastAll(
                        new PointerEventData(EventSystem.current) { position = candidate }, raycasts);
                    sampledTopNames.Add(raycasts.Count == 0 ? "<none>" : raycasts[0].gameObject.name);
                    if (raycasts.FirstOrDefault().gameObject != outside.targetGraphic.gameObject) continue;
                    outsidePoint = candidate;
                    foundOutsidePoint = true;
                }
                if (!foundOutsidePoint)
                {
                    var contentRect = (RectTransform)sheet.transform.Find("Content");
                    var contentCorners = new Vector3[4];
                    contentRect.GetWorldCorners(contentCorners);
                    var graphic = outside.targetGraphic;
                    var group = sheet.GetComponent<CanvasGroup>();
                    Assert.Fail(
                        "No raycastable outside region. " +
                        $"screen={Screen.width}x{Screen.height}; " +
                        $"outside=({outsideCorners[0]})..({outsideCorners[2]}); " +
                        $"content=({contentCorners[0]})..({contentCorners[2]}); " +
                        $"siblings outside={outside.transform.GetSiblingIndex()} content={contentRect.GetSiblingIndex()}; " +
                        $"graphic enabled={graphic.enabled} raycast={graphic.raycastTarget} depth={graphic.depth} " +
                        $"canvas={(graphic.canvas == null ? "<null>" : graphic.canvas.name)}; " +
                        $"group alpha={group.alpha} blocks={group.blocksRaycasts} interactable={group.interactable}; " +
                        "sampledTop=" + string.Join(",", sampledTopNames));
                }
                yield return Click(mouse, outsidePoint);
                yield return new WaitForSecondsRealtime(0.2f);
                Assert.That(sheet.GetComponent<CanvasGroup>().blocksRaycasts, Is.False,
                    "Outside close must release the sheet's UI/Scene pointer boundary.");
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator DisablingReviewController_ReleasesOwnedPauseLifecycle()
        {
            yield return LoadScene();
            var scene = SceneManager.GetActiveScene();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return Click(mouse, Find<Button>(scene, "Pause Game Button"));
                Assert.That(Time.timeScale, Is.Zero);
                var review = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Phase5UiFoundationReviewController>(true))
                    .Single();
                review.enabled = false;
                yield return null;
                Assert.That(Time.timeScale, Is.EqualTo(1f));
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator VirtualMouse_ExtendedManualControls_ExecutePanelBackToastTooltipAndInterruptionStates()
        {
            yield return LoadScene();
            var scene = SceneManager.GetActiveScene();
            foreach (var controlName in new[]
            {
                "Show Solid Panel Button", "Show Light Frost Panel Button", "Show Strong Frost Panel Button",
                "Force Frost Fallback Button", "Handle Back Button", "Open Second Modal Button",
                "Show Toast Burst Button", "Long Press Tooltip Button", "Close Tooltip Button",
                "Interrupt And Reopen Button"
            }) Assert.That(Find<Button>(scene, controlName), Is.Not.Null, controlName);

            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                yield return Click(mouse, Find<Button>(scene, "Show Solid Panel Button"));
                Assert.That(Find(scene, "Solid Panel Fixture").activeSelf, Is.True);
                Assert.That(Find(scene, "Light Frost Panel Fixture").activeSelf, Is.False);
                yield return Click(mouse, Find<Button>(scene, "Show Light Frost Panel Button"));
                Assert.That(Find(scene, "Light Frost Panel Fixture").activeSelf, Is.True);

                yield return Click(mouse, Find<Button>(scene, "Show Toast Burst Button"));
                Assert.That(Find(scene, "Toast Burst Status").GetComponent<TMPro.TMP_Text>().text,
                    Does.Contain("3").And.Contain("merged"));

                var longPress = Find<Button>(scene, "Long Press Tooltip Button");
                QueueMouseState(mouse, Center(longPress), true);
                InputSystem.Update();
                yield return new WaitForSecondsRealtime(0.65f);
                QueueMouseState(mouse, Center(longPress), false);
                InputSystem.Update();
                yield return null;
                Assert.That(Find(scene, "Tooltip Fixture").transform.Find("Content").gameObject.activeSelf, Is.True);
                yield return Click(mouse, Find<Button>(scene, "Close Tooltip Button"));
                Assert.That(Find(scene, "Tooltip Fixture").transform.Find("Content").gameObject.activeSelf, Is.False);

                yield return Click(mouse, Find<Button>(scene, "Open Modal Button"));
                var modalGroup = Find(scene, "Modal Fixture").GetComponent<CanvasGroup>();
                yield return WaitForGroup(modalGroup, true);
                var blocker = Find(scene, "Modal Fixture").transform.Find("Blocker").GetComponent<Button>();
                var blockerPoint = FindTopRaycastPoint(blocker);
                Assert.That(blockerPoint.HasValue, Is.True,
                    "Critical Modal needs a real outside blocker region.");
                yield return Click(mouse, blockerPoint.Value);
                Assert.That(modalGroup.blocksRaycasts, Is.True,
                    "Critical Modal outside press must not dismiss it.");

                var openSecond = Find<Button>(scene, "Open Second Modal Button");
                var openSecondPoint = FindTopRaycastPoint(openSecond);
                Assert.That(openSecondPoint.HasValue, Is.True,
                    "Primary Modal must expose the real second-Modal entry Button.");
                yield return Click(mouse, openSecondPoint.Value);
                var secondModalGroup = Find(scene, "Second Modal Fixture").GetComponent<CanvasGroup>();
                yield return WaitForGroup(secondModalGroup, true);
                Assert.That(secondModalGroup.blocksRaycasts, Is.True,
                    "Second Modal must actually be open before Back is tested.");
                Assert.That(secondModalGroup.alpha, Is.GreaterThanOrEqualTo(0.99f));
                yield return PressBack(keyboard);
                yield return WaitForGroup(secondModalGroup, false);
                var reviewController = FindAll<Phase5UiFoundationReviewController>(scene).Single();
                Assert.That(reviewController.BackRequestCount, Is.EqualTo(1),
                    reviewController.LastBackTrace);
                Assert.That(modalGroup.blocksRaycasts, Is.True,
                    "Back closes only the top Modal in a two-Modal stack. " +
                    reviewController.LastBackTrace);
                yield return PressBack(keyboard);
                yield return WaitForGroup(modalGroup, false);
                Assert.That(modalGroup.blocksRaycasts, Is.False);

                yield return Click(mouse, Find<Button>(scene, "Interrupt And Reopen Button"));
                yield return WaitForGroup(modalGroup, true);
                Assert.That(modalGroup.blocksRaycasts, Is.True);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator ReloadingValidationScene_ResetsPauseAndContainerLifecycle()
        {
            yield return LoadScene();
            var scene = SceneManager.GetActiveScene();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return Click(mouse, Find<Button>(scene, "Pause Game Button"));
                yield return Click(mouse, Find<Button>(scene, "Open Modal Button"));
                Assert.That(Time.timeScale, Is.Zero);

                yield return LoadScene();
                scene = SceneManager.GetActiveScene();
                Assert.That(Time.timeScale, Is.EqualTo(1f));
                Assert.That(FindAll<EventSystem>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<Phase5UiFoundationReviewController>(scene), Has.Length.EqualTo(1));
                Assert.That(Find(scene, "Modal Fixture").GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }

        private static IEnumerator LoadScene()
        {
            EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
        }

        private static IEnumerator Click(Mouse mouse, Button button)
        {
            var position = Center(button);
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, results);
            Assert.That(results, Is.Not.Empty, button.name + " must be raycastable.");
            Assert.That(
                results[0].gameObject == button.gameObject
                || results[0].gameObject.transform.IsChildOf(button.transform),
                Is.True,
                button.name + " must be the top raycast target.");
            yield return Click(mouse, position);
        }

        private static IEnumerator Click(Mouse mouse, Vector2 position)
        {
            QueueMouseState(mouse, position, true);
            InputSystem.Update();
            yield return null;
            yield return null;
            QueueMouseState(mouse, position, false);
            InputSystem.Update();
            yield return null;
            yield return null;
        }

        private static IEnumerator PressMoveRelease(Mouse mouse, Vector2 start, Vector2 end)
        {
            QueueMouseState(mouse, start, true);
            InputSystem.Update();
            yield return null;
            QueueMouseState(mouse, end, true);
            InputSystem.Update();
            yield return null;
            QueueMouseState(mouse, end, false);
            InputSystem.Update();
            yield return null;
        }

        private static IEnumerator WaitForGroup(CanvasGroup group, bool open)
        {
            var deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline
                   && (open ? group.alpha < 0.99f : group.alpha > 0.01f))
                yield return null;
        }

        private static IEnumerator PressBack(Keyboard keyboard)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        private static Vector2 Center(Button button)
        {
            var rect = (RectTransform)button.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * 0.5f);
        }

        private static Vector2? FindTopRaycastPoint(Button button)
        {
            var corners = new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(corners);
            var results = new System.Collections.Generic.List<RaycastResult>();
            for (var y = 1; y < 10; y++)
            for (var x = 1; x < 10; x++)
            {
                var candidate = new Vector2(
                    Mathf.Lerp(corners[0].x, corners[2].x, x / 10f),
                    Mathf.Lerp(corners[0].y, corners[2].y, y / 10f));
                results.Clear();
                EventSystem.current.RaycastAll(
                    new PointerEventData(EventSystem.current) { position = candidate }, results);
                if (results.FirstOrDefault().gameObject == button.targetGraphic.gameObject)
                    return candidate;
            }
            return null;
        }

        private static void QueueMouseState(Mouse mouse, Vector2 position, bool leftDown)
        {
            var state = new MouseState { position = position };
            if (leftDown) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state);
        }

        private static GameObject Find(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Single(transform => transform.name == name).gameObject;

        private static T Find<T>(Scene scene, string name) where T : Component =>
            Find(scene, name).GetComponent<T>();

        private static T[] FindAll<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    public sealed class P8RTouchRecoveryRouterTests
    {
        [TestCase(DecorationTouchHitKind.Scene)]
        [TestCase(DecorationTouchHitKind.Furniture)]
        [TestCase(DecorationTouchHitKind.WallSlot)]
        [TestCase(DecorationTouchHitKind.FunctionalSurface)]
        [TestCase(DecorationTouchHitKind.Ui)]
        public void MissingPrimaryWithoutTerminal_ReleasesOwnerAndAcceptsNextGesture(
            DecorationTouchHitKind kind)
        {
            var router = new DecorationTouchRouter(2f, 0f);
            var classifier = new FixedHit(kind);
            Process(router, 1, classifier, Point(11, InputTouchPhase.Began, 10));
            Process(router, 2, classifier, Point(11, InputTouchPhase.Moved, 30));

            // A removed device no longer appears in the source snapshot; no Ended is guaranteed.
            // 设备移除后没有 Ended 记录，也必须释放旧 owner，不把取消当成点击。
            var lost = Process(router, 3, classifier);
            Assert.That(lost.Owner, Is.EqualTo(DecorationGestureOwner.None));
            Assert.That(lost.TapReleased, Is.False);
            Assert.That(router.PrimaryTouchId, Is.EqualTo(DecorationTouchRouter.NoTouchId));
            Assert.That(lost.GestureCanceled, Is.EqualTo(kind == DecorationTouchHitKind.FunctionalSurface));

            Process(router, 4, classifier, Point(12, InputTouchPhase.Began, 60));
            Assert.That(router.PrimaryTouchId, Is.EqualTo(12));
            Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.False);
        }

        [Test]
        public void MissingPinchPrimary_DoesNotPromoteHeldSecondaryOrNewFinger()
        {
            var router = new DecorationTouchRouter(2f, 0f);
            var classifier = new FixedHit(DecorationTouchHitKind.Scene);
            Process(router, 1, classifier, Point(1, InputTouchPhase.Began, 10));
            Process(router, 2, classifier, Point(1, InputTouchPhase.Stationary, 10),
                Point(2, InputTouchPhase.Began, 80));
            var lost = Process(router, 3, classifier, Point(2, InputTouchPhase.Stationary, 80),
                Point(3, InputTouchPhase.Began, 100));
            Assert.That(lost.Owner, Is.EqualTo(DecorationGestureOwner.None));
            Assert.That(lost.TapReleased, Is.False);
            Assert.That(router.IsSuppressingUntilAllTouchesUp, Is.True);
            Process(router, 4, classifier, Point(2, InputTouchPhase.Moved, 90));
            Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.None));
            Process(router, 5, classifier);
            Process(router, 6, classifier, Point(4, InputTouchPhase.Began, 40));
            Assert.That(router.Owner, Is.EqualTo(DecorationGestureOwner.Camera));
        }

        [Test]
        public void MissingPinchSecondary_RebasesPrimaryWithoutJumpOrTap()
        {
            var router = new DecorationTouchRouter(2f, 0f);
            var classifier = new FixedHit(DecorationTouchHitKind.Scene);
            Process(router, 1, classifier, Point(1, InputTouchPhase.Began, 10));
            Process(router, 2, classifier, Point(1, InputTouchPhase.Stationary, 10),
                Point(2, InputTouchPhase.Began, 80));
            var lost = Process(router, 3, classifier, Point(1, InputTouchPhase.Moved, 40));
            Assert.That(lost.Owner, Is.EqualTo(DecorationGestureOwner.Camera));
            Assert.That(lost.CameraPanRequested, Is.False);
            Assert.That(lost.PinchZoomRequested, Is.False);
            var resumed = Process(router, 4, classifier, Point(1, InputTouchPhase.Moved, 45));
            Assert.That(resumed.CameraPanRequested, Is.True);
            var released = Process(router, 5, classifier, Point(1, InputTouchPhase.Ended, 45));
            Assert.That(released.TapReleased, Is.False);
        }

        private static DecorationTouchPoint Point(int id, InputTouchPhase phase, float x) =>
            new DecorationTouchPoint(id, new Vector2(x, 10), new Vector2(5, 0), phase);
        private static DecorationTouchRoutingResult Process(DecorationTouchRouter router, int frame,
            IDecorationTouchHitClassifier classifier, params DecorationTouchPoint[] points) =>
            router.ProcessFrame(new DecorationTouchFrame(frame, points), classifier);
        private sealed class FixedHit : IDecorationTouchHitClassifier
        {
            private readonly DecorationTouchHitKind kind;
            public FixedHit(DecorationTouchHitKind value) { kind = value; }
            public DecorationTouchHit ClassifyBegan(int id, Vector2 point) =>
                new DecorationTouchHit(kind, "test-counter");
        }
    }

    public sealed class P8RInputRecoveryPlayModeTests
    {
        private readonly InputTestFixture input = new InputTestFixture();
        private DecorationModeController controller;
        private Touchscreen screen;
        private UnityEngine.Camera camera;
        private readonly Dictionary<int, Vector2> heldContacts = new();
        private Touchscreen contactScreen;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var preResetTime = InputState.currentTime;
            heldContacts.Clear();
            contactScreen = null;
            controller = null;
            // The previous scene must release leases before InputTestFixture resets InputSystem.
            // 先停旧场景输入，再重置测试设备，避免旧 owner 读取或释放新 runtime。
            foreach (var adapter in Object.FindObjectsByType<AnimalCafe.Input.MouseCameraInput>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None)) adapter.enabled = false;
            foreach (var source in Object.FindObjectsByType<InputSystemDecorationTouchSource>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None)) source.enabled = false;
            input.Setup();
            // InputSystemObject keeps the Editor's native play-mode transition timestamps, while
            // InputTestFixture starts a new mock clock. Keep synthetic events beyond that old window.
            // Editor保留原生模式切换时间；对齐mock时钟，避免顺序运行时把新触点误判为旧事件。
            input.currentTime = Math.Max(input.currentTime, preResetTime + .001d);
            screen = InputSystem.AddDevice<Touchscreen>();
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            controller = Object.FindFirstObjectByType<DecorationModeController>();
            Assert.That(controller, Is.Not.Null);
            controller.EnterDecorationMode();
            yield return new WaitForSecondsRealtime(.3f);
            camera = Field<UnityEngine.Camera>(controller, "targetCamera");
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            try
            {
                var active = SceneManager.GetActiveScene();
                var inputAssets = active.IsValid() && active.isLoaded
                    ? Phase8SceneInputTestCleanup.CaptureAssets(active)
                    : Array.Empty<InputActionAsset>();

                // Retire contacts/devices while their real UI module still owns the actions.
                // 先结束本fixture的触点并移除设备，再卸载UI，避免它读取旧设备control缓存。
                if (screen != null && screen.added)
                {
                    if (ReferenceEquals(contactScreen, screen))
                    {
                        foreach (var contact in heldContacts)
                            InputSystem.QueueStateEvent(screen, new TouchState
                            {
                                touchId = contact.Key,
                                phase = InputTouchPhase.Canceled,
                                position = contact.Value
                            });
                    }
                    InputSystem.Update();
                }
                heldContacts.Clear();
                if (controller != null) controller.enabled = false;
                if (active.IsValid() && active.isLoaded)
                {
                    foreach (var adapter in active.GetRootGameObjects().SelectMany(root =>
                                 root.GetComponentsInChildren<AnimalCafe.Input.MouseCameraInput>(true)))
                        adapter.enabled = false;
                    foreach (var source in active.GetRootGameObjects().SelectMany(root =>
                                 root.GetComponentsInChildren<InputSystemDecorationTouchSource>(true)))
                        source.enabled = false;
                }
                if (screen != null && screen.added) InputSystem.RemoveDevice(screen);
                yield return null;

                Time.timeScale = 1f;
                var cleanup = SceneManager.CreateScene("P8RInputRecoveryCleanup");
                SceneManager.SetActiveScene(cleanup);
                if (active.IsValid() && active.isLoaded && active != cleanup)
                {
                    var unload = SceneManager.UnloadSceneAsync(active);
                    while (unload != null && !unload.isDone) yield return null;
                }
                Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets);
            }
            finally { input.TearDown(); }
        }

        [UnityTest]
        public IEnumerator ExitModal_CancelsActiveCameraDragAndRetainsPreview()
        {
            BeginWallPreview();
            var point = FindCameraPoint();
            yield return Contact(41, InputTouchPhase.Began, point);
            yield return Contact(41, InputTouchPhase.Moved, point + Vector2.right * 24);
            Assert.That(Router.Owner, Is.EqualTo(DecorationGestureOwner.Camera));
            var preview = controller.ActiveWallMountedPreview;

            Assert.That(controller.TryRequestExit(), Is.False);
            Assert.That(Router.Owner, Is.EqualTo(DecorationGestureOwner.None),
                "Opening a modal must retire the old scene gesture immediately.");
            Assert.That(Router.IsSuppressingUntilAllTouchesUp, Is.True);
            var before = camera.transform.position;
            yield return Contact(41, InputTouchPhase.Moved, point + Vector2.right * 48);
            Assert.That(Vector3.Distance(camera.transform.position, before), Is.LessThan(.0001f));
            Assert.That(controller.ActiveWallMountedPreview, Is.SameAs(preview));

            var modal = Object.FindFirstObjectByType<DecorationExitModalView>();
            Field<Button>(modal, "continueButton").onClick.Invoke();
            yield return null;
            yield return Contact(41, InputTouchPhase.Moved, point + Vector2.right * 72);
            Assert.That(Vector3.Distance(camera.transform.position, before), Is.LessThan(.0001f),
                "Continue Editing must not resume the finger that opened the modal.");
            yield return Contact(41, InputTouchPhase.Ended, point + Vector2.right * 72);
            Assert.That(Router.IsSuppressingUntilAllTouchesUp, Is.False);
            var freshPoint = FindCameraPoint();
            yield return Contact(42, InputTouchPhase.Began, freshPoint);
            yield return Contact(42, InputTouchPhase.Moved, freshPoint + Vector2.right * 24);
            Assert.That(Vector3.Distance(camera.transform.position, before), Is.GreaterThan(.001f),
                "A fresh contact must work after all old fingers are released.");
            yield return Contact(42, InputTouchPhase.Ended, freshPoint + Vector2.right * 24);
        }

        [UnityTest]
        public IEnumerator ExitModal_BlocksPinchAlreadyInProgress()
        {
            BeginWallPreview();
            var point = FindCameraPoint();
            yield return Contact(51, InputTouchPhase.Began, point);
            yield return Contact(52, InputTouchPhase.Began, point + Vector2.left * 70);
            Assert.That(Router.Owner, Is.EqualTo(DecorationGestureOwner.Pinch));
            Assert.That(controller.TryRequestExit(), Is.False);
            var size = camera.orthographicSize;
            yield return Contact(52, InputTouchPhase.Moved, point + Vector2.left * 110);
            Assert.That(camera.orthographicSize, Is.EqualTo(size).Within(.0001f),
                "A held pinch cannot zoom the scene behind the exit modal.");
            yield return Contact(52, InputTouchPhase.Ended, point + Vector2.left * 110);
            yield return Contact(51, InputTouchPhase.Ended, point);
        }

        [UnityTest]
        public IEnumerator DeviceRemovalDuringSceneDrag_AllowsFreshTouchAfterReconnect()
        {
            var point = FindCameraPoint();
            yield return Contact(61, InputTouchPhase.Began, point);
            yield return Contact(61, InputTouchPhase.Moved, point + Vector2.right * 24);
            Assert.That(Router.Owner, Is.EqualTo(DecorationGestureOwner.Camera));
            InputSystem.RemoveDevice(screen);
            yield return null;
            yield return null;
            Assert.That(Router.Owner, Is.EqualTo(DecorationGestureOwner.None),
                "A disconnected Touchscreen cannot leave a scene owner locked.");

            screen = InputSystem.AddDevice<Touchscreen>();
            var before = camera.transform.position;
            point = FindCameraPoint();
            yield return Contact(62, InputTouchPhase.Began, point);
            yield return Contact(62, InputTouchPhase.Moved, point + Vector2.left * 24);
            Assert.That(Vector3.Distance(camera.transform.position, before), Is.GreaterThan(.001f));
            yield return Contact(62, InputTouchPhase.Ended, point + Vector2.left * 24);
        }

        private DecorationTouchRouter Router => Field<DecorationTouchRouter>(controller, "touchRouter");
        private void BeginWallPreview()
        {
            Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            Assert.That(controller.TryBeginWallMountedPreview("wall-decor.wood-shelf.01",
                "wall.back-left", new WallSlotPosition(4, 0)), Is.True);
            Canvas.ForceUpdateCanvases();
        }

        private Vector2 FindCameraPoint()
        {
            var classifier = (IDecorationTouchHitClassifier)controller;
            for (var y = .75f; y >= .45f; y -= .05f)
            for (var x = .7f; x >= .3f; x -= .05f)
            {
                var point = new Vector2(Screen.width * x, Screen.height * y);
                var kind = classifier.ClassifyBegan(999, point).Kind;
                if (kind == DecorationTouchHitKind.Scene || kind == DecorationTouchHitKind.FloorGrid
                    || kind == DecorationTouchHitKind.WallSurface) return point;
            }
            Assert.Fail("Fixture needs a visible, UI-free camera gesture origin.");
            return default;
        }

        private IEnumerator Contact(int id, InputTouchPhase phase, Vector2 position)
        {
            if (!ReferenceEquals(contactScreen, screen))
            {
                // Device-loss tests replace the screen; never cancel old IDs on the new device.
                // 设备重连后只清理新设备自己的触点。
                heldContacts.Clear();
                contactScreen = screen;
            }
            if (phase == InputTouchPhase.Ended || phase == InputTouchPhase.Canceled)
                heldContacts.Remove(id);
            else
                heldContacts[id] = position;
            InputSystem.QueueStateEvent(screen, new TouchState
                { touchId = id, phase = phase, position = position });
            yield return null;
            yield return null;
        }

        private static T Field<T>(object owner, string name) =>
            (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }
}
#endif

#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    /// <summary>Reserve the real model silhouette outside full button hit roots, not just PNG faces.
    /// 操作栏完整点击区必须避开墙饰模型；不能只验证小底板互不重叠。</summary>
    public sealed class P8RWallDecorActionAvoidanceTests
    {
        private Scene scene;
        private Vector2? previousProfile;
        private float previousTimeScale;
        [SetUp] public void Remember()
        {
            previousProfile = P8RMobileMetrics.EditorLogicalViewportOverride;
            previousTimeScale = Time.timeScale;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                var assets = Phase8SceneInputTestCleanup.CaptureAssets(scene);
                Find<DecorationModeController>().enabled = false;
                SceneManager.SetActiveScene(SceneManager.CreateScene("P8RWallAvoidCleanup"));
                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone) yield return null;
                Phase8SceneInputTestCleanup.DisposeReleasedAssets(assets);
            }
            P8RMobileMetrics.EditorLogicalViewportOverride = previousProfile;
            Time.timeScale = previousTimeScale;
            yield return null;
        }
        private T Find<T>() where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
        private static T Field<T>(object owner, string name) => (T)owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static void Refresh(DecorationModeController controller)
        {
            controller.GetType().GetMethod("UpdateActionPresentation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator MonitorPreview_CompleteTouchRootsAvoidModel_AcrossZoomAndScreenEdges()
        {
            var capture = System.Environment.GetEnvironmentVariable("ANIMALCAFE_WALL_DRAG_CAPTURE") == "1";
            using var screen = capture ? null : new P8RReferenceLayoutTests.NativeScreenSize();
            using var gameView = capture ? new P8RReferenceLayoutTests.RealGameViewSize() : null;
            void Resize(Vector2 size) { if (capture) gameView.Resize(size); else screen.Resize(size); }
            Resize(new Vector2(1080, 1920));
            P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360, 640);
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null; yield return null;
            var controller = Find<DecorationModeController>();
            controller.EnterDecorationMode();
            Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            Assert.That(controller.TryBeginWallMountedPreview("wall-decor.monitor.01", "wall.back-left",
                new WallSlotPosition(4, 0)), Is.True);
            yield return new WaitForSecondsRealtime(.3f);
            var camera = Field<UnityEngine.Camera>(controller, "targetCamera");
            var view = Find<DecorationActionBarView>();
            var preview = Find<WallMountedPreviewView>();
            foreach (var pixels in new[] { new Vector2(1080, 1920), new Vector2(480, 854), new Vector2(1600, 720) })
            {
                Resize(pixels);
                P8RMobileMetrics.EditorLogicalViewportOverride = pixels.x == 1080 ? new Vector2(360, 640)
                    : pixels.x == 480 ? pixels / 1.5f : new Vector2(800, 360);
                yield return new WaitForSecondsRealtime(.2f);
                foreach (var zoom in new[] { 4f, 7f })
                foreach (var horizontal in new[] { .16f, .5f, .84f })
                {
                    camera.orthographicSize = zoom;
                    var model = ModelBounds(preview.CurrentGhost, camera);
                    var target = new Vector2(Screen.width * horizontal, Screen.height * .48f);
                    var worldPerPixel = camera.orthographicSize * 2f / camera.pixelHeight;
                    camera.transform.position += camera.transform.right * ((model.center.x - target.x) * worldPerPixel)
                        + camera.transform.up * ((model.center.y - target.y) * worldPerPixel);
                    Refresh(controller);
                    model = ModelBounds(preview.CurrentGhost, camera);
                    var buttons = new[] { "cancelButton", "confirmButton" }.Select(name => Field<Button>(view, name)).ToArray();
                    var density = P8RMobileMetrics.For(view).PixelsPerLogicalUnit;
                    foreach (var button in buttons)
                    {
                        var hit = UiBounds((RectTransform)button.transform);
                        Assert.That(hit.Overlaps(model), Is.False,
                            pixels + " zoom=" + zoom + " x=" + horizontal + " " + button.name
                            + " transparent touch root covers wall preview. hit=" + hit + " model=" + model);
                        Assert.That(hit.width / density, Is.EqualTo(44f).Within(.01f));
                        Assert.That(hit.height / density, Is.GreaterThanOrEqualTo(47.99f));
                        Assert.That(UiBounds(button.image.rectTransform).width / density, Is.EqualTo(30f).Within(.1f),
                            "Do not solve occlusion by changing the approved button style.");
                        Assert.That(hit.xMin, Is.GreaterThanOrEqualTo(-.1f));
                        Assert.That(hit.xMax, Is.LessThanOrEqualTo(Screen.width + .1f));
                        Assert.That(hit.yMin, Is.GreaterThanOrEqualTo(-.1f));
                        Assert.That(hit.yMax, Is.LessThanOrEqualTo(Screen.height + .1f));
                    }
                    Assert.That(UiBounds((RectTransform)buttons[0].transform).Overlaps(
                        UiBounds((RectTransform)buttons[1].transform)), Is.False);
                    if (capture && zoom == 4f && horizontal > .4f)
                        yield return CaptureNative(pixels, horizontal, camera);
                }
            }
        }

        private static IEnumerator CaptureNative(Vector2 pixels, float horizontal, UnityEngine.Camera camera)
        {
            Assert.That(Application.isBatchMode, Is.False);
            var tag = System.Environment.GetEnvironmentVariable("ANIMALCAFE_WALL_DRAG_RUN");
            Assert.That(tag, Does.Match("^[a-z0-9-]+$"));
            var directory = "outputs/p8r-wall-drag-fix-20260913/" + tag + "/" + (int)pixels.x + "x" + (int)pixels.y;
            System.IO.Directory.CreateDirectory(directory);
            var path = System.IO.Path.GetFullPath(directory + (horizontal == .5f ? "/monitor-center.png" : "/monitor-edge.png"));
            Assert.That(System.IO.File.Exists(path), Is.False, "Preserve previous review evidence.");
            var deadline = Time.realtimeSinceStartup + 45f;
            while (UnityEditor.ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(UnityEditor.ShaderUtil.anythingCompiling, Is.False);
            Assert.That(camera.targetTexture, Is.Null, "Use native rendering, not a substitute camera target.");
            yield return new WaitForEndOfFrame();
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.That(new Vector2(frame.width, frame.height), Is.EqualTo(pixels));
                System.IO.File.WriteAllBytes(path, frame.EncodeToPNG());
            }
            finally { Object.Destroy(frame); }
        }

        internal static Rect ModelBounds(GameObject model, UnityEngine.Camera camera)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy).ToArray();
            Assert.That(renderers, Is.Not.Empty);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var points = Enumerable.Range(0, 8).Select(index => camera.WorldToScreenPoint(new Vector3(
                (index & 1) == 0 ? bounds.min.x : bounds.max.x,
                (index & 2) == 0 ? bounds.min.y : bounds.max.y,
                (index & 4) == 0 ? bounds.min.z : bounds.max.z))).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
        internal static Rect UiBounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
    }
}
#endif

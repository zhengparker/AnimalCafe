#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using AnimalCafe.Decoration;
using AnimalCafe.UI;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    public sealed class P8RUiEnhancementSceneTests
    {
        private Scene scene;
        private float previousTime;
        private Vector2? previousProfile;
        [SetUp] public void Remember() { previousTime = Time.timeScale; previousProfile = P8RMobileMetrics.EditorLogicalViewportOverride; }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                var assets = Phase8SceneInputTestCleanup.CaptureAssets(scene);
                Find<DecorationModeController>().enabled = false;
                SceneManager.SetActiveScene(SceneManager.CreateScene("P8RUiEnhancementCleanup"));
                var unload = SceneManager.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone) yield return null;
                Phase8SceneInputTestCleanup.DisposeReleasedAssets(assets);
            }
            P8RMobileMetrics.EditorLogicalViewportOverride = previousProfile;
            Time.timeScale = previousTime;
        }

        [UnityTest]
        public IEnumerator NativeUiExamples_WhenRequested_CaptureActualGameView()
        {
            if (Environment.GetEnvironmentVariable("ANIMALCAFE_UI_ENHANCEMENT_CAPTURE") != "1" || Application.isBatchMode)
                Assert.Ignore("Opt-in real Editor Game View capture; not a synthetic UI image.");
            var folder = Path.GetFullPath("outputs/p8r-ui-enhancement/native-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(folder);
            using (var screen = new P8RReferenceLayoutTests.RealGameViewSize())
            {
                screen.Resize(new Vector2(1080, 1920));
                P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360, 640);
                scene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity", new LoadSceneParameters(LoadSceneMode.Single));
                yield return null; yield return null; yield return null;
                yield return Capture(folder, "portrait-normal.png");
                Find<AnimalCafe.Core.Time.GameTimeService>().SetFast();
                yield return Capture(folder, "portrait-fast.png");
                Find<AnimalCafe.Core.Time.GameTimeService>().SetPaused();
                yield return Capture(folder, "portrait-paused.png");
                Find<AnimalCafe.Core.Time.GameTimeService>().SetFast();
                Find<DecorationModeController>().EnterDecorationMode();
                Find<DecorationCatalogueView>().ShowCatalogue();
                yield return new WaitForSecondsRealtime(.4f);
                yield return Capture(folder, "portrait-furniture.png");
                Find<DecorationModeController>().TryChangeMode(DecorationModeKind.Floor);
                Find<DecorationModeController>().TrySelectFloorRange(SurfaceEditScope.SingleGridFloor);
                yield return new WaitForSecondsRealtime(.4f);
                yield return Capture(folder, "portrait-floor-panel.png");
                Find<DecorationCatalogueView>().ShowCollapsedHandle();
                yield return new WaitForSecondsRealtime(.4f);
                yield return Capture(folder, "portrait-floor-folded.png");
                Find<DecorationModeController>().TryChangeMode(DecorationModeKind.Furniture);
                Find<DecorationCatalogueView>().ShowCatalogue();
                Find<DecorationCatalogueView>().ShowCollapsedHandle();
                yield return new WaitForSecondsRealtime(.4f);
                yield return Capture(folder, "portrait-folded.png");
                Find<DecorationCatalogueView>().ShowCatalogue();
                yield return new WaitForSecondsRealtime(.4f);
                screen.Resize(new Vector2(1080, 1920));
                P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360, 640);
                yield return new WaitForSecondsRealtime(.4f);
                yield return Capture(folder, "phone-furniture.png");
                screen.Resize(new Vector2(1080, 1920));
                P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360, 640);
                yield return new WaitForSecondsRealtime(.4f);
                yield return Capture(folder, "portrait-preview-furniture.png");
                Find<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>()
                    .Single(tile => tile.ItemId == "furniture.counter.module.01")
                    .GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                yield return new WaitForSecondsRealtime(.4f);
                yield return Capture(folder, "portrait-preview-preview.png");
                Find<DecorationActionBarView>().GetComponentsInChildren<UnityEngine.UI.Button>(true)
                    .Single(button => button.name == "CancelButton").onClick.Invoke();
                screen.Resize(new Vector2(1080, 1920));
                P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360, 640);
                yield return new WaitForSecondsRealtime(.4f);
                var controller = Find<DecorationModeController>();
                var runtime = Find<AnimalCafe.Decoration.CafeLayoutRuntime>();
                var support = runtime.Layout.FurnitureInstances.Single();
                // Create actual confirmed contents through the catalogue and controller, never fake modal copy.
                // 从正式目录放置并确认咖啡机，再选择台面触发真实Store预检查。
                Find<DecorationCatalogueView>().ShowCatalogue();
                Find<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>()
                    .Single(tile => tile.ItemId == "equipment.coffee-machine.01")
                    .GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(controller.TryMoveFunctionalSurfacePreview(new AnimalCafe.Layout.SurfaceSlotAddress(support.InstanceId, "slot.0")), Is.True);
                Find<DecorationActionBarView>().GetComponentsInChildren<UnityEngine.UI.Button>(true)
                    .Single(button => button.name == "ConfirmButton").onClick.Invoke();
                Assert.That(controller.ActiveFunctionalSurfacePreview, Is.Null);
                typeof(DecorationModeController).GetMethod("HandleFurnitureBegan",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { support.InstanceId });
                yield return new WaitForSecondsRealtime(.3f);
                Find<DecorationActionBarView>().GetComponentsInChildren<UnityEngine.UI.Button>(true)
                    .Single(button => button.name == "StoreButton").onClick.Invoke();
                var storeModal = Find<DecorationStoreModalView>();
                Assert.That(storeModal.IsOpen, Is.True);
                Assert.That(storeModal.GetComponentsInChildren<TMPro.TMP_Text>(true).Any(text => text.text == "Clear the counter first"), Is.True);
                Assert.That(runtime.FunctionalSurfaceLayout.MountedInstances.Count, Is.EqualTo(1));
                var acknowledgement = storeModal.GetComponentsInChildren<UnityEngine.UI.Button>()
                    .Single(button => button.GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.text == "Got it"));
                Assert.That(acknowledgement.image.sprite.name, Is.EqualTo("button_primary_normal"));
                Assert.That(P8RCompleteFlowTests.MeasuredInk(acknowledgement.image).width / P8RMobileMetrics.For(storeModal).PixelsPerLogicalUnit,
                    Is.GreaterThanOrEqualTo(150));
                yield return new WaitForSecondsRealtime(.3f);
                yield return Capture(folder, "portrait-occupied-counter-store.png");
                screen.Resize(new Vector2(1080, 1920));
                P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360, 640);
                yield return new WaitForSecondsRealtime(.3f);
                yield return Capture(folder, "phone-occupied-counter-store.png");
                screen.Resize(new Vector2(1080, 1920));
                P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360, 640);
                yield return new WaitForSecondsRealtime(.3f);
                yield return Capture(folder, "portrait-preview-occupied-counter-store.png");
            }
            TestContext.WriteLine("Native UI examples: " + folder);
        }

        private static IEnumerator Capture(string folder, string name)
        {
            Assert.That(Screen.width, Is.EqualTo(1080));
            Assert.That(Screen.height, Is.EqualTo(1920));
            Canvas.ForceUpdateCanvases();
            var deadline = Time.realtimeSinceStartup + 45;
            while (UnityEditor.ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(UnityEditor.ShaderUtil.anythingCompiling, Is.False);
            yield return null;
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(folder, name);
            ScreenCapture.CaptureScreenshot(path, 1);
            for (var i = 0; i < 120 && !File.Exists(path); i++) yield return null;
            Assert.That(File.Exists(path), Is.True);
        }
        private T Find<T>() where T : Component => scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();
    }
}
#endif

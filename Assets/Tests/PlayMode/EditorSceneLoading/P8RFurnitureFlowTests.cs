#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    public sealed class P8RFurnitureFlowTests
    {
        [UnityTest]
        public IEnumerator RealCatalogue_InvalidConfirmThenValidConfirm_CommitsExactlyOnce()
        {
            yield return Load();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            var runtime = Object.FindFirstObjectByType<CafeLayoutRuntime>();
            controller.EnterDecorationMode();
            yield return new WaitForSecondsRealtime(.25f);
            yield return Capture("01-catalogue.png");
            Select("furniture.counter.module.01");
            var session = Field<DecorationSession>(controller, "session");
            var action = Object.FindFirstObjectByType<DecorationActionBarView>();
            var before = runtime.Layout.FurnitureInstances.Count;
            Refresh(controller, session.MovePreview(new GridPosition(-1, -1)));
            Assert.That(Button(action, "ConfirmButton").interactable, Is.False);
            Assert.That(Field<TMP_Text>(action, "feedbackLabel").text, Does.Contain("outside"));
            Button(action, "ConfirmButton").onClick.Invoke();
            Assert.That(runtime.Layout.FurnitureInstances.Count, Is.EqualTo(before));
            yield return Capture("02-invalid-preview.png");
            Refresh(controller, session.MovePreview(new GridPosition(5, 4)));
            Assert.That(Button(action, "ConfirmButton").interactable, Is.True);
            yield return Capture("03-valid-preview.png");
            Button(action, "ConfirmButton").onClick.Invoke();
            Button(action, "ConfirmButton").onClick.Invoke();
            Assert.That(runtime.Layout.FurnitureInstances.Count, Is.EqualTo(before + 1));
            Assert.That(session.ActivePreview, Is.Null);
            yield return Capture("04-confirmed.png");
        }

        [UnityTest]
        public IEnumerator PutAwayCancel_PreservesPreview_AndEditingCancelRestoresExistingFurniture()
        {
            yield return Load();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            var runtime = Object.FindFirstObjectByType<CafeLayoutRuntime>();
            controller.EnterDecorationMode();
            var original = runtime.Layout.FurnitureInstances.Single();
            var originalPosition = original.Position;
            var originalRotation = original.Rotation;
            var session = Field<DecorationSession>(controller, "session");
            Refresh(controller, session.BeginExisting(original.InstanceId));
            Refresh(controller, session.MovePreview(new GridPosition(5, 4)));
            var action = Object.FindFirstObjectByType<DecorationActionBarView>();
            Button(action, "RotateButton").onClick.Invoke();
            Assert.That(session.ActivePreview.ProposedRotation, Is.Not.EqualTo(originalRotation));
            var preview = session.ActivePreview;
            Button(action, "StoreButton").onClick.Invoke();
            var modal = Object.FindFirstObjectByType<DecorationStoreModalView>();
            Assert.That(modal.IsOpen, Is.True);
            Assert.That(Field<TMP_Text>(modal, "titleLabel").text, Does.StartWith("Put away"));
            yield return Capture("05-put-away-modal.png");
            Field<Button>(modal, "cancelButton").onClick.Invoke();
            Assert.That(modal.IsOpen, Is.False);
            // Store confirmation updates an immutable preview, so identity is semantic.
            Assert.That(session.ActivePreview.SourceInstanceId, Is.EqualTo(preview.SourceInstanceId));
            Assert.That(session.ActivePreview.DefinitionId, Is.EqualTo(preview.DefinitionId));
            Assert.That(session.ActivePreview.IsNew, Is.False);
            Assert.That(session.ActivePreview.ProposedRotation, Is.EqualTo(preview.ProposedRotation));
            Assert.That(runtime.Layout.FurnitureInstances.Count, Is.EqualTo(1));
            Assert.That(runtime.Layout.FurnitureInstances.Single().Position, Is.EqualTo(originalPosition));
            Assert.That(runtime.Layout.FurnitureInstances.Single().Rotation, Is.EqualTo(originalRotation));
            Assert.That(session.ActivePreview.ProposedPosition, Is.EqualTo(new GridPosition(5, 4)));
            Assert.That(session.State, Is.EqualTo(DecorationSessionState.EditingExistingFurniture));
            yield return new WaitForSecondsRealtime(.3f);
            AssertActionDoesNotCoverCatalogueControls();
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            catalogue.ShowCatalogue();
            Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
            yield return new WaitForSecondsRealtime(.25f);
            yield return Capture("07-return-to-editing.png");
            Button(catalogue, "ReturnToEditing").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(catalogue.State, Is.EqualTo(DecorationCatalogueState.Collapsed));
            Assert.That(session.ActivePreview.SourceInstanceId, Is.EqualTo(original.InstanceId));
            Button(action, "CancelButton").onClick.Invoke();
            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(runtime.Layout.FurnitureInstances.Single().Position, Is.EqualTo(originalPosition));
            Assert.That(runtime.Layout.FurnitureInstances.Single().Rotation, Is.EqualTo(originalRotation));
            Refresh(controller, session.BeginExisting(original.InstanceId));
            Button(action, "StoreButton").onClick.Invoke();
            Field<Button>(modal, "confirmButton").onClick.Invoke();
            Field<Button>(modal, "confirmButton").onClick.Invoke();
            Assert.That(runtime.Layout.FurnitureInstances.Count, Is.Zero, "Empty Counter Put Away removes the existing instance exactly once.");
            Assert.That(session.ActivePreview, Is.Null);
        }

        [UnityTest]
        public IEnumerator BlockedCounterPutAway_ShowsEquipmentCountsWithoutLosingPreview()
        {
            yield return Load();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            var runtime = Object.FindFirstObjectByType<CafeLayoutRuntime>();
            var support = runtime.Layout.FurnitureInstances.Single();
            controller.EnterDecorationMode();
            Select("equipment.cash-register.01");
            Assert.That(controller.TryMoveFunctionalSurfacePreview(new SurfaceSlotAddress(support.InstanceId, "slot.0")), Is.True);
            var action = Object.FindFirstObjectByType<DecorationActionBarView>();
            Button(action, "ConfirmButton").onClick.Invoke();
            var session = Field<DecorationSession>(controller, "session");
            Refresh(controller, session.BeginExisting(support.InstanceId));
            Button(action, "StoreButton").onClick.Invoke();
            var modal = Object.FindFirstObjectByType<DecorationStoreModalView>();
            Field<Button>(modal, "confirmButton").onClick.Invoke();
            Assert.That(session.ActivePreview, Is.Not.Null);
            Assert.That(runtime.Layout.FurnitureInstances.Count, Is.EqualTo(1));
            Assert.That(runtime.FunctionalSurfaceLayout.MountedInstances.Count, Is.EqualTo(1));
            Assert.That(Field<TMP_Text>(action, "feedbackLabel").text, Does.Contain("Cash Register (1)"));
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(modal.IsOpen, Is.False);
            AssertActionDoesNotCoverCatalogueControls();
            yield return Capture("06-blocked-counter.png");
        }

        private static IEnumerator Load()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator PickupPutAwayCopy_ExplainsTheIndependentPickupButton()
        {
            yield return Load();
            Object.FindFirstObjectByType<DecorationModeController>().EnterDecorationMode();
            var modal = Object.FindObjectsByType<DecorationStoreModalView>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
            modal.ShowFunctionalSurface(DecorationCatalogueItemKind.PickUpPoint);
            Assert.That(Field<TMP_Text>(modal, "bodyLabel").text, Does.Contain("Pickup Point button"));
            modal.CloseForOwnerShutdown();
        }
        private static void AssertActionDoesNotCoverCatalogueControls()
        {
            Canvas.ForceUpdateCanvases();
            var action = Object.FindFirstObjectByType<DecorationActionBarView>();
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            var panel = ScreenRect(Field<RectTransform>(action, "presentationRoot"));
            var tabs = catalogue.GetComponentInChildren<DecorationModeTabsView>();
            foreach (var button in tabs.GetComponentsInChildren<Button>().Where(button => button.gameObject.activeInHierarchy))
                Assert.That(panel.Overlaps(ScreenRect((RectTransform)button.transform)), Is.False,
                    "Idle after modal close: action panel overlaps " + button.name + "; action=" + panel);
            Assert.That(panel.Overlaps(ScreenRect(catalogue.CollapsedHandleRect)), Is.False,
                "Idle after modal close: action panel overlaps Show Catalogue.");
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
        }

        private static void Select(string id) => Object.FindFirstObjectByType<DecorationCatalogueView>()
            .GetComponentsInChildren<DecorationCatalogueTileView>(true).Single(tile => tile.ItemId == id && tile.gameObject.activeInHierarchy)
            .GetComponent<Button>().onClick.Invoke();
        private static Button Button(Component root, string name) => root.GetComponentsInChildren<Button>(true).Single(item => item.name == name);
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static void Refresh(DecorationModeController owner, PlacementResult result)
        {
            typeof(DecorationModeController).GetMethod("SyncActivePreviewPresentation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
            typeof(DecorationModeController).GetMethod("ShowActionForResult", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, new object[] { result });
        }
        private static IEnumerator Capture(string filename)
        {
            if (Environment.GetEnvironmentVariable("ANIMALCAFE_P8R_CAPTURE") != "1") yield break;
            yield return new WaitForSecondsRealtime(.3f);
            var camera = Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsSortMode.None).Single(item => item.CompareTag("MainCamera"));
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(item => item.isRootCanvas)
                .Select(item => new { Canvas = item, item.renderMode, item.worldCamera, item.planeDistance }).ToArray();
            var width = camera.pixelWidth; var height = camera.pixelHeight;
            Assert.That(width, Is.EqualTo(Screen.width)); Assert.That(height, Is.EqualTo(Screen.height));
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = camera.targetTexture; var active = RenderTexture.active;
            target.Create();
            try
            {
                camera.targetTexture = target;
                foreach (var state in canvases)
                { state.Canvas.renderMode = RenderMode.ScreenSpaceCamera; state.Canvas.worldCamera = camera; state.Canvas.planeDistance = .5f; }
                Canvas.ForceUpdateCanvases();
                yield return new WaitForSecondsRealtime(.2f);
                foreach (var state in canvases)
                {
                    Assert.That(state.Canvas.pixelRect.width, Is.EqualTo(width).Within(.5f));
                    Assert.That(state.Canvas.pixelRect.height, Is.EqualTo(height).Within(.5f));
                }
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                var folder = "outputs/p8r-ui-proportion-20260911/furniture"; Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, filename), pixels.EncodeToPNG());
                File.WriteAllText(Path.Combine(folder, "capture-metrics.txt"),
                    "Actual Editor GameView: " + Screen.width + "x" + Screen.height + "; Camera: " + width + "x" + height
                    + "; Synthetic geometry is a separate test. Not mobile hardware acceptance.");
            }
            finally
            {
                camera.targetTexture = previous; RenderTexture.active = active;
                foreach (var state in canvases)
                { state.Canvas.renderMode = state.renderMode; state.Canvas.worldCamera = state.worldCamera; state.Canvas.planeDistance = state.planeDistance; }
                target.Release(); Object.Destroy(target); Object.Destroy(pixels);
                Canvas.ForceUpdateCanvases();
            }
            // Optional native Overlay evidence; bounded frames avoid batch-mode EndOfFrame hangs.
            if (filename == "05-put-away-modal.png")
            {
                var nativePath = Path.GetFullPath("outputs/p8r-ui-proportion-20260911/furniture/05-native-overlay.png");
                ScreenCapture.CaptureScreenshot(nativePath);
                for (var frame = 0; frame < 30 && !File.Exists(nativePath); frame++) yield return null;
                Debug.Log(File.Exists(nativePath) ? "P8R native Overlay screenshot available." : "Native Overlay screenshot unavailable in this batch Editor; camera capture remains layout-only evidence.");
            }
        }
    }
}
#endif

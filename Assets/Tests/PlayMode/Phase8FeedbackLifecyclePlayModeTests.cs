using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase8FeedbackLifecyclePlayModeTests
    {
        [Test]
        public void MissingSurfaceTarget_ExplainsWhereToMoveInsteadOfReportingInvalidData()
        {
            Assert.That(PlacementFeedbackMapper.GetPlayerMessage(FunctionalSurfacePlacementResult.Failure(
                FunctionalSurfacePlacementFailureReason.InvalidSurfaceSlotAddress)),
                Is.EqualTo("请将物件移到柜台上的摆放位"));
        }

        [UnityTest]
        public IEnumerator InvalidPreview_PersistsBeyondToastLifetime_WithoutPublishingReadiness()
        {
            using var h = new EditingFeedbackHarness();
            h.BeginInvalidPickUp();
            Assert.That(h.Status.CurrentMessage, Is.Empty, "An unconfirmed Preview is not a readiness report.");
            yield return new WaitForSecondsRealtime(2.3f);
            Assert.That(h.Feedback.gameObject.activeInHierarchy, Is.True);
            Assert.That(h.Feedback.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(h.Feedback.text, Does.Contain("正在编辑：取餐点").And.Contain("尚未确认").And.Contain("互动位置"));
            Assert.That(h.Controller.TryMoveFunctionalSurfacePreview(
                new SurfaceSlotAddress(new string('1', 32), "slot.2")), Is.True);
            Assert.That(h.Feedback.text, Does.Contain("位置有效，可以确认").And.Not.Contain("没有可用"));
            h.Controller.CancelFunctionalSurfacePreview();
            Assert.That(h.Feedback.gameObject.activeSelf, Is.False);
            Assert.That(h.Status.CurrentMessage, Is.Empty);
        }

        [Test]
        public void ExpandedCatalogue_ReturnRestoresSameInvalidPreview_WithoutMutatingLayout()
        {
            using var h = new EditingFeedbackHarness();
            h.BeginInvalidPickUp();
            var preview = h.Controller.ActiveFunctionalSurfacePreview;
            var version = h.Runtime.ReadinessVersion;
            h.Catalogue.ShowCatalogue();
            Assert.That(h.Controller.TryBeginFunctionalSurfacePreview(
                DecorationCatalogueItemKind.PickUpPoint, null, default), Is.False,
                "Selecting another item in this tab still preserves the current Preview.");
            var button = h.Catalogue.transform.Find("Expanded/EditingContext/ReturnToEditing")?.GetComponent<Button>();
            Assert.That(button, Is.Not.Null, "Expanded catalogue must offer a real Return to Editing button.");
            var text = h.Catalogue.transform.Find("Expanded/EditingContext/Message").GetComponent<TMP_Text>();
            Assert.That(text.text, Does.Contain("请先确认或取消").And.Contain("互动位置"));
            Assert.That(button.gameObject.activeInHierarchy, Is.True);
            button.onClick.Invoke();
            Assert.That(h.Catalogue.State, Is.EqualTo(DecorationCatalogueState.Collapsed));
            Assert.That(h.Action.IsVisible, Is.True);
            Assert.That(h.Feedback.text, Does.Contain("互动位置"));
            Assert.That(h.Controller.ActiveFunctionalSurfacePreview, Is.SameAs(preview));
            Assert.That(h.Runtime.ReadinessVersion, Is.EqualTo(version));
            h.Controller.CancelFunctionalSurfacePreview();
            h.Catalogue.ShowCatalogue();
            Assert.That(button.gameObject.activeInHierarchy, Is.False, "Ended Preview must not leave a stale Return button.");
        }

        private sealed class EditingFeedbackHarness : IDisposable
        {
            private readonly IDisposable inner;
            public DecorationModeController Controller { get; }
            public CafeLayoutRuntime Runtime { get; }
            public DecorationCatalogueView Catalogue { get; }
            public DecorationActionBarView Action { get; }
            public TMP_Text Feedback { get; }
            public ValidationMessageView Status { get; }

            public EditingFeedbackHarness()
            {
                var type = typeof(Phase8FunctionalSurfaceInteractionPlayModeTests)
                    .GetNestedType("FunctionalViewHarness", BindingFlags.NonPublic);
                inner = (IDisposable)Activator.CreateInstance(type, true);
                T Get<T>(string name) => (T)type.GetProperty(name).GetValue(inner);
                var root = Get<GameObject>("Root");
                Controller = Get<DecorationModeController>("Controller");
                // This fixture drives controller commands directly; it has no live input sources.
                // 禁用每帧输入采样，ActionBar 自身的真实反馈 coroutine 仍会运行。
                Controller.enabled = false;
                Runtime = Get<CafeLayoutRuntime>("Runtime");
                Catalogue = Get<DecorationCatalogueView>("Catalogue");
                Action = Get<DecorationActionBarView>("ActionBar");
                var status = Child(root.transform, "Readiness", typeof(TextMeshProUGUI));
                Status = status.AddComponent<ValidationMessageView>();
                Status.Configure(status.GetComponent<TMP_Text>());
                Set(Controller, "validationMessageView", Status);
                var feedback = Child(Action.transform, "Feedback", typeof(TextMeshProUGUI), typeof(CanvasGroup));
                Feedback = feedback.GetComponent<TMP_Text>();
                Set(Action, "feedbackLabel", Feedback);
                Set(Action, "feedbackRoot", (RectTransform)feedback.transform);
                Set(Action, "feedbackCanvasGroup", feedback.GetComponent<CanvasGroup>());
                var expanded = Child(Catalogue.transform, "Expanded");
                var collapsed = Child(Catalogue.transform, "Collapsed");
                var scroll = Child(expanded.transform, "Scroll", typeof(ScrollRect));
                Set(Catalogue, "expandedRoot", expanded);
                Set(Catalogue, "collapsedRoot", collapsed);
                Set(Catalogue, "verticalScroll", scroll.GetComponent<ScrollRect>());
                Catalogue.Configure(new UiPointerBoundary(), new UiTransitionRunner(() => true));
                typeof(DecorationModeController).GetMethod("SubscribeViewEvents", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(Controller, null);
            }

            public void BeginInvalidPickUp()
            {
                Runtime.Layout.AddReservation(new LayoutReservation("blocked.pickup-neighbour", LayoutReservationType.Blocked,
                    new GridPosition(0, 1), new GridSize(1, 1)));
                Assert.That(Controller.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint,
                    null, new SurfaceSlotAddress(new string('1', 32), "slot.0")), Is.True);
                Assert.That(Controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.False);
            }

            public T GetInner<T>(string name) => (T)inner.GetType().GetProperty(name).GetValue(inner);
            public void ConfigureStoreModal() => inner.GetType().GetMethod("ConfigureStoreModal").Invoke(inner, null);
            public void InvokeController(string method) => typeof(DecorationModeController)
                .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Controller, null);

            private static GameObject Child(Transform parent, string name, params Type[] components)
            {
                var child = new GameObject(name, typeof(RectTransform));
                child.transform.SetParent(parent, false);
                foreach (var component in components) child.AddComponent(component);
                return child;
            }

            private static void Set(object target, string field, object value) => target.GetType()
                .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
            public void Dispose() => inner.Dispose();
        }

        [Test]
        public void FailedConfirmAndRejectedNewItem_KeepOriginalReasonAndPreview()
        {
            using var h = new EditingFeedbackHarness();
            h.BeginInvalidPickUp();
            var version = h.Runtime.ReadinessVersion;
            Assert.That(h.Controller.TryConfirmFunctionalSurfacePreview(), Is.False);
            Assert.That(h.Feedback.text, Does.Contain("互动位置"));
            Assert.That(h.Controller.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.CashRegister,
                "equipment.cash-register", new SurfaceSlotAddress(new string('1', 32), "slot.2")), Is.False);
            Assert.That(h.Controller.ActiveFunctionalSurfacePreview.Kind, Is.EqualTo(FunctionalSurfacePreviewKind.PickUpPoint));
            Assert.That(h.Feedback.text, Does.Contain("互动位置").And.Contain("请先确认或取消"));
            Assert.That(h.Runtime.ReadinessVersion, Is.EqualTo(version));
            Assert.That(h.Status.CurrentMessage, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void StoreModal_OwnsInput_AndOnlySuccessfulStoreEndsEditing(bool confirmStore)
        {
            using var h = new EditingFeedbackHarness();
            Assert.That(h.Controller.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint,
                null, new SurfaceSlotAddress(new string('1', 32), "slot.2")), Is.True);
            Assert.That(h.Controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            var id = h.Runtime.FunctionalSurfaceLayout.PickUpPoints.Single().InstanceId;
            Assert.That(h.Controller.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.PickUpPoint, id), Is.True);
            h.Controller.TryMoveFunctionalSurfacePreview(default);
            Assert.That(h.Controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.False);
            var reason = h.Feedback.text;
            var report = h.Status.CurrentMessage;
            var version = h.Runtime.ReadinessVersion;
            h.ConfigureStoreModal();
            h.Catalogue.ShowCatalogue();
            h.InvokeController("HandleStoreRequested");
            var modal = h.GetInner<DecorationStoreModalView>("StoreModal");
            Assert.That(modal.IsOpen, Is.True);
            var sheetStateBeforeReturn = h.Catalogue.State;
            var button = h.Catalogue.transform.Find("Expanded/EditingContext/ReturnToEditing").GetComponent<Button>();
            button.onClick.Invoke();
            Assert.That(modal.IsOpen, Is.True);
            Assert.That(h.Catalogue.State, Is.EqualTo(sheetStateBeforeReturn), "Return cannot steal modal input.");
            h.GetInner<Button>(confirmStore ? "StoreConfirm" : "StoreCancel").onClick.Invoke();
            if (confirmStore)
            {
                Assert.That(h.Controller.ActiveFunctionalSurfacePreview, Is.Null);
                Assert.That(h.Runtime.FunctionalSurfaceLayout.PickUpPoints, Is.Empty);
                Assert.That(h.Runtime.ReadinessVersion, Is.EqualTo(version + 1));
                Assert.That(button.gameObject.activeInHierarchy, Is.False);
            }
            else
            {
                Assert.That(h.Controller.ActiveFunctionalSurfacePreview.InstanceId, Is.EqualTo(id));
                Assert.That(h.Feedback.text, Is.EqualTo(reason));
                Assert.That(h.Runtime.ReadinessVersion, Is.EqualTo(version));
                Assert.That(h.Status.CurrentMessage, Is.EqualTo(report));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PlayerStatus_KeepsDetachedDiagnosticsWithoutAppendingThemToTheLabel(bool validationResult)
        {
            var root = new GameObject("Player feedback", typeof(RectTransform), typeof(TextMeshProUGUI));
            try
            {
                var view = root.AddComponent<ValidationMessageView>();
                view.Configure(root.GetComponent<TextMeshProUGUI>());
                const string station = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
                var ids = new[] { station, "slot.0", station, " " };
                if (validationResult)
                    view.SetValidationResult(false, "这个摆放位已经被占用", ids);
                else
                    view.ShowStatus("这个摆放位已经被占用", ids);
                ids[0] = "changed.after.display";

                Assert.That(view.CurrentMessage, Does.Contain("占用").And.Not.Contain(station).And.Not.Contain("slot.0"));
                Assert.That(view.DiagnosticIds, Is.EqualTo(new[] { station, "slot.0" }),
                    "Diagnostic IDs must survive as a detached snapshot for troubleshooting.");
                Assert.That(view.IsVisible, Is.True);

                view.SetValidationResult(true, string.Empty);
                Assert.That(view.IsVisible, Is.False);
                Assert.That(view.CurrentMessage, Is.Empty);
                Assert.That(view.DiagnosticIds, Is.Empty);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void DestroyedFeedbackView_CleanupIsSafeDuringSceneUnload()
        {
            var root = new GameObject("Destroyed feedback", typeof(RectTransform), typeof(TextMeshProUGUI));
            var view = root.AddComponent<ValidationMessageView>();
            view.Configure(root.GetComponent<TextMeshProUGUI>());
            UnityEngine.Object.DestroyImmediate(root);
            Assert.DoesNotThrow(() => view.Clear());
        }

        [Test]
        public void ConfirmedReadinessRemainsVisible_WhilePickUpPreviewExplainsMissingAdjacentCell()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var ht = typeof(Phase8FunctionalSurfaceInteractionPlayModeTests)
                .GetNestedType("FunctionalViewHarness", BindingFlags.NonPublic);
            using var harness = (IDisposable)Activator.CreateInstance(ht, true);
            T Get<T>(string name) => (T)ht.GetProperty(name).GetValue(harness);
            var root = Get<GameObject>("Root");
            var controller = Get<DecorationModeController>("Controller");
            var runtime = Get<CafeLayoutRuntime>("Runtime");
            var action = Get<DecorationActionBarView>("ActionBar");
            var labelObject = new GameObject("Readiness", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(root.transform, false);
            var view = labelObject.AddComponent<ValidationMessageView>();
            view.Configure(labelObject.GetComponent<TextMeshProUGUI>());
            typeof(DecorationModeController).GetField("validationMessageView", flags).SetValue(controller, view);
            var feedbackObject = new GameObject("PreviewReason", typeof(RectTransform), typeof(TextMeshProUGUI));
            feedbackObject.transform.SetParent(root.transform, false);
            var feedback = feedbackObject.GetComponent<TextMeshProUGUI>();
            typeof(DecorationActionBarView).GetField("feedbackLabel", flags).SetValue(action, feedback);
            Assert.That(controller.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.CashRegister,
                "equipment.cash-register", new SurfaceSlotAddress(new string('1', 32), "slot.2")), Is.True);
            Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            var previousMessage = view.CurrentMessage;
            var previousVersion = runtime.ReadinessVersion;
            // Other neighbours are outside the grid or occupied by the 5x1 counter.
            runtime.Layout.AddReservation(new LayoutReservation("blocked.pickup-neighbour", LayoutReservationType.Blocked,
                new GridPosition(0, 1), new GridSize(1, 1)));
            Assert.That(controller.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint,
                null, new SurfaceSlotAddress(new string('1', 32), "slot.0")), Is.True);
            Assert.That(controller.ActiveFunctionalSurfacePreview.Validation.FailureReason,
                Is.EqualTo(FunctionalSurfacePlacementFailureReason.NoValidInteractionAnchor));
            Assert.That(controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.False);
            Assert.That(view.CurrentMessage, Is.EqualTo(previousMessage));
            Assert.That(runtime.ReadinessVersion, Is.EqualTo(previousVersion));
            Assert.That(previousMessage, Does.StartWith("已确认布局："));
            Assert.That(feedback.text, Does.Contain("正在编辑：取餐点").And.Contain("互动位置"));
        }
    }
}

using System;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase8FeedbackLifecyclePlayModeTests
    {
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
            Assert.That(feedback.text, Does.Contain("adjacent"));
        }
    }
}

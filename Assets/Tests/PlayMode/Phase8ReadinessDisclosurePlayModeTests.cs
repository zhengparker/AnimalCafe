using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI.Feedback;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase8ReadinessDisclosurePlayModeTests
    {
        [Test]
        public void ReadyReport_HidesPresentationButPreservesLatestDiagnosticMessage()
        {
            using var h = new ViewFixture();
            h.View.ShowReadiness(Report(true));

            Assert.That(h.View.IsVisible, Is.False);
            Assert.That(h.View.CurrentMessage, Is.Empty);
            Assert.That(h.Label.enabled, Is.False);
            Assert.That(h.View.GetComponent<Image>().enabled, Is.False);
            Assert.That(h.ActiveDisclosureButtons, Is.Empty);
            Assert.That(h.View.DiagnosticIds, Is.Empty);
            Assert.That(h.Group.alpha, Is.Zero);
            Assert.That(h.Group.interactable, Is.False);
            Assert.That(h.Group.blocksRaycasts, Is.False);
            Assert.That(h.Scroll.enabled, Is.False);
            Assert.That(Read<string>(h.View, "FullReadinessMessage"),
                Is.EqualTo("已确认布局：已就绪\n布局已准备好，可以营业"),
                "Hiding a healthy card must not erase the latest confirmed report.");
        }

        [Test]
        public void HealthyErrorHealthy_OnlyPublishesRealVisibilityChangesAndReleasesInput()
        {
            using var h = new ViewFixture();
            var changes = 0;
            h.View.DetailsVisibilityChanged += () => changes++;

            h.View.ShowReadiness(Report(true));
            Assert.That(h.View.IsVisible, Is.False);
            Assert.That(changes, Is.Zero, "An already hidden healthy report has no geometry change to publish.");

            h.View.ShowReadiness(Report(false, Failure(LayoutReadinessSeverity.Blocking,
                LayoutReadinessFailureCode.MissingCoffeeMachine)));
            Assert.That(h.View.IsVisible, Is.True);
            Assert.That(h.Group.blocksRaycasts, Is.True);
            Assert.That(h.Scroll.enabled, Is.True);
            Assert.That(changes, Is.EqualTo(1));

            h.View.ShowReadiness(Report(true));
            Assert.That(h.View.IsVisible, Is.False);
            Assert.That(h.View.CurrentMessage, Is.Empty);
            Assert.That(h.ActiveDisclosureButtons, Is.Empty);
            Assert.That(h.Group.blocksRaycasts, Is.False);
            Assert.That(h.Group.interactable, Is.False);
            Assert.That(h.Scroll.enabled, Is.False);
            Assert.That(changes, Is.EqualTo(2),
                "Removing the card must publish once so layout consumers drop its obstruction.");

            h.View.ShowReadiness(Report(true));
            Assert.That(changes, Is.EqualTo(2),
                "Repeated healthy reports must not publish a visibility change every frame or refresh.");
        }

        [Test]
        public void SingleMissingStation_ShowsTheActionableProblemWithoutRedundantDetails()
        {
            using var h = new ViewFixture();
            h.View.ShowReadiness(Report(false, Failure(LayoutReadinessSeverity.Blocking,
                LayoutReadinessFailureCode.MissingCoffeeMachine)));

            Assert.That(h.View.CurrentMessage, Does.StartWith("已确认布局：")
                .And.Contain("暂时不能营业").And.Contain("还需要至少一台咖啡机"));
            Assert.That(h.View.CurrentMessage, Does.Not.Contain("\n"));
            Assert.That(h.ActiveDisclosureButtons, Is.Empty);
        }

        [Test]
        public void BlockingReport_DefaultsToPrimaryProblem_AndDetailsKeepWarningsAndDiagnostics()
        {
            using var h = new ViewFixture();
            var report = MixedReport();
            var failures = report.Failures.ToArray();
            h.View.ShowReadiness(report);

            Assert.That(h.View.CurrentMessage, Does.Contain("暂时不能营业")
                .And.Contain("还需要至少一台咖啡机").And.Not.Contain("(2, 3)"));
            var summary = h.View.CurrentMessage;
            var button = h.DisclosureButton();
            Assert.That(button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("查看详情"));
            h.Click(button);

            Assert.That(Read<bool>(h.View, "IsDetailsExpanded"), Is.True);
            Assert.That(h.View.CurrentMessage, Does.Contain("阻挡").And.Contain("提醒")
                .And.Contain("收银机 / 员工 (2, 3)").And.Contain("互动位置被阻挡")
                .And.Contain("还需要至少一台咖啡机"));
            Assert.That(h.View.CurrentMessage, Does.Not.Contain("station.register").And.Not.Contain("slot.0"));
            Assert.That(h.View.DiagnosticIds, Is.EquivalentTo(new[] { "station.register", "support.counter", "slot.0" }));
            Assert.That(Read<string>(h.View, "FullReadinessMessage"), Does.Contain("收银机 / 员工 (2, 3)")
                .And.Contain("还需要至少一台咖啡机"));
            Assert.That(button.GetComponentInChildren<TMP_Text>().text, Is.EqualTo("收起详情"));
            h.Click(button);

            Assert.That(Read<bool>(h.View, "IsDetailsExpanded"), Is.False);
            Assert.That(h.View.CurrentMessage, Is.EqualTo(summary));
            Assert.That(report.CanOpenForBusiness, Is.False);
            Assert.That(report.Failures, Is.EqualTo(failures), "Disclosure changes presentation only.");
        }

        [Test]
        public void ReadyWithWarning_DoesNotPretendEveryStationIsHealthy()
        {
            using var h = new ViewFixture();
            h.View.ShowReadiness(Report(true, Failure(LayoutReadinessSeverity.Warning,
                LayoutReadinessFailureCode.AnchorBlocked, true)));

            Assert.That(h.View.CurrentMessage, Does.StartWith("已确认布局：可以营业，但")
                .And.Contain("互动位置被阻挡"));
            h.Click(h.DisclosureButton());
            Assert.That(h.View.CurrentMessage, Does.Contain("提醒").And.Contain("收银机 / 员工 (2, 3)"));
        }

        [Test]
        public void NewReport_CollapsesOldDetailsAndReplacesTheirContents()
        {
            using var h = new ViewFixture();
            h.View.ShowReadiness(MixedReport());
            h.Click(h.DisclosureButton());
            h.View.ShowReadiness(Report(false,
                Failure(LayoutReadinessSeverity.Blocking, LayoutReadinessFailureCode.MissingPickUpPoint),
                Failure(LayoutReadinessSeverity.Blocking, LayoutReadinessFailureCode.MissingCashRegister)));

            Assert.That(Read<bool>(h.View, "IsDetailsExpanded"), Is.False);
            Assert.That(h.View.CurrentMessage, Does.Contain("还需要至少一个取餐点").And.Not.Contain("收银机 / 员工"));
            Assert.That(h.View.DiagnosticIds, Is.Empty);
            h.Click(h.DisclosureButton());
            Assert.That(h.View.CurrentMessage, Does.Contain("还需要至少一个取餐点")
                .And.Contain("还需要至少一个收银机").And.Not.Contain("互动位置被阻挡"));
        }

        [Test]
        public void Clear_RemovesExpandedDetailsAndStopsItsStaleButtonFromReopeningThem()
        {
            using var h = new ViewFixture();
            Assert.That(h.View.IsVisible, Is.False);
            Assert.That(h.Group.blocksRaycasts, Is.False);
            h.View.ShowReadiness(MixedReport());
            var button = h.DisclosureButton();
            h.Click(button);
            h.View.Clear();
            button.onClick.Invoke();

            Assert.That(h.View.IsVisible, Is.False);
            Assert.That(h.View.CurrentMessage, Is.Empty);
            Assert.That(Read<string>(h.View, "FullReadinessMessage"), Is.Empty);
            Assert.That(Read<bool>(h.View, "IsDetailsExpanded"), Is.False);
            Assert.That(h.ActiveDisclosureButtons, Is.Empty);
            Assert.That(h.View.DiagnosticIds, Is.Empty);
            Assert.That(h.Group.blocksRaycasts, Is.False);
            Assert.That(h.Group.interactable, Is.False);
            Assert.That(h.Scroll.enabled, Is.False);
        }

        [Test]
        public void GenericStatus_ReplacesReadinessWithoutAStaleDisclosureButton()
        {
            using var h = new ViewFixture();
            h.View.ShowReadiness(MixedReport());
            h.Click(h.DisclosureButton());
            h.View.ShowStatus("请先移除柜台上的物件", new[] { "item.1", "item.1" });

            Assert.That(h.View.CurrentMessage, Is.EqualTo("请先移除柜台上的物件"));
            Assert.That(h.View.DiagnosticIds, Is.EqualTo(new[] { "item.1" }));
            Assert.That(h.ActiveDisclosureButtons, Is.Empty);
            Assert.That(Read<bool>(h.View, "IsDetailsExpanded"), Is.False);
            Assert.That(Read<string>(h.View, "FullReadinessMessage"), Is.Empty);
        }

        [Test]
        public void RepeatedConfigure_ReusesOneButtonAndOneClickTogglesOnlyOnce()
        {
            using var h = new ViewFixture();
            h.View.ShowReadiness(MixedReport());
            var first = h.DisclosureButton();
            for (var i = 0; i < 3; i++)
            {
                h.View.Configure(h.Label);
                h.View.ShowReadiness(MixedReport());
            }

            Assert.That(h.View.GetComponentsInChildren<Button>(true), Has.Length.EqualTo(1));
            Assert.That(h.DisclosureButton(), Is.SameAs(first));
            h.Click(first);
            Assert.That(Read<bool>(h.View, "IsDetailsExpanded"), Is.True);
            h.Click(first);
            Assert.That(Read<bool>(h.View, "IsDetailsExpanded"), Is.False);
        }

        [UnityTest]
        public IEnumerator NarrowExpandedReport_KeepsDetailsScrollableAndButtonOutsideTheTextViewport()
        {
            using var h = new ViewFixture(320f);
            var failures = Enumerable.Range(0, 24).Select(i => Failure(LayoutReadinessSeverity.Blocking,
                LayoutReadinessFailureCode.AnchorUnreachable, true, i)).ToArray();
            h.View.ShowReadiness(Report(false, failures));
            var button = h.DisclosureButton();
            h.Click(button);
            Canvas.ForceUpdateCanvases();
            // A real raycast needs a rendered Canvas depth, not only calculated RectTransforms.
            // 等 Canvas 实际绘制一帧，再验证真实点击；不绕过 GraphicRaycaster 的 depth 检查。
            yield return null;
            Canvas.ForceUpdateCanvases();

            Assert.That(((RectTransform)h.View.transform).rect.height, Is.LessThanOrEqualTo(200f));
            Assert.That(h.Scroll.enabled && h.Scroll.vertical, Is.True);
            Assert.That(h.Scroll.content.rect.height, Is.GreaterThan(h.Scroll.viewport.rect.height));
            Assert.That(WorldRect(h.Scroll.viewport).Overlaps(WorldRect((RectTransform)button.transform)), Is.False,
                "The expanded body needs its own scroll viewport, clear of the collapse button.");
            var buttonRect = (RectTransform)button.transform;
            var center = RectTransformUtility.WorldToScreenPoint(null,
                buttonRect.TransformPoint(buttonRect.rect.center));
            var buttonImage = button.GetComponent<Image>();
            Assert.That(buttonImage.depth, Is.GreaterThanOrEqualTo(0), "The fixture button must have been rendered before raycasting.");
            Assert.That(buttonImage.canvasRenderer.cull, Is.False);
            Assert.That(new Rect(0f, 0f, Screen.width, Screen.height).Contains(center), Is.True,
                "The real click must be inside the runner's screen: " + center + " in " + Screen.width + "x" + Screen.height);
            var hits = new List<RaycastResult>();
            h.Events.RaycastAll(new PointerEventData(h.Events) { position = center }, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject == button.gameObject || hits[0].gameObject.transform.IsChildOf(button.transform),
                Is.True, "A details press must hit the UI button before any scene interaction behind it.");
        }

        private static T Read<T>(object target, string name)
        {
            var property = target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, "Readiness disclosure must expose " + name + ".");
            return (T)property.GetValue(target);
        }

        private static LayoutReadinessReport MixedReport() => Report(false,
            Failure(LayoutReadinessSeverity.Warning, LayoutReadinessFailureCode.AnchorBlocked, true),
            Failure(LayoutReadinessSeverity.Blocking, LayoutReadinessFailureCode.MissingCoffeeMachine));

        private static LayoutReadinessReport Report(bool canOpen, params LayoutReadinessFailure[] failures)
        {
            var summary = Construct<LayoutReadinessSummary>(0, 0);
            return Construct<LayoutReadinessReport>(canOpen, Array.Empty<StationReadiness>(), failures,
                summary, summary, summary);
        }

        private static LayoutReadinessFailure Failure(LayoutReadinessSeverity severity,
            LayoutReadinessFailureCode code, bool station = false, int row = 0) =>
            Construct<LayoutReadinessFailure>(severity, code,
                station ? (LayoutStationType?)LayoutStationType.CashRegister : null,
                station ? "station.register" : null, station ? "support.counter" : null,
                station ? "slot.0" : null, station ? (InteractionRole?)InteractionRole.Employee : null,
                station ? (GridPosition?)new GridPosition(2, 3 + row) : null, "Detached diagnostic cause");

        private static T Construct<T>(params object[] arguments) => (T)Activator.CreateInstance(typeof(T),
            BindingFlags.Instance | BindingFlags.NonPublic, null, arguments, null);

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private sealed class ViewFixture : IDisposable
        {
            private readonly GameObject canvasObject;
            private readonly GameObject eventObject;
            public ValidationMessageView View { get; }
            public TMP_Text Label { get; }
            public CanvasGroup Group { get; }
            public ScrollRect Scroll { get; }
            public EventSystem Events { get; }
            public Button[] ActiveDisclosureButtons => View.GetComponentsInChildren<Button>(true)
                .Where(button => button.gameObject.activeInHierarchy).ToArray();

            public ViewFixture(float width = 800f)
            {
                canvasObject = new GameObject("ReadinessDisclosureCanvas", typeof(RectTransform),
                    typeof(Canvas), typeof(GraphicRaycaster));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Events = EventSystem.current;
                if (Events == null)
                {
                    eventObject = new GameObject("ReadinessDisclosureEventSystem", typeof(EventSystem));
                    Events = eventObject.GetComponent<EventSystem>();
                }
                var host = Child("SafeArea", canvasObject.transform);
                host.sizeDelta = new Vector2(width, 600f);
                var root = Child("Readiness", host);
                root.anchorMin = root.anchorMax = new Vector2(.5f, 1f);
                root.pivot = new Vector2(.5f, 1f);
                root.sizeDelta = new Vector2(width - 40f, 160f);
                root.gameObject.AddComponent<Image>();
                Group = root.gameObject.AddComponent<CanvasGroup>();
                Scroll = root.gameObject.AddComponent<ScrollRect>();
                var viewport = Child("Viewport", root);
                Stretch(viewport, new Vector2(12f, 8f), new Vector2(-12f, -8f));
                viewport.gameObject.AddComponent<Image>().color = Color.clear;
                viewport.gameObject.AddComponent<RectMask2D>();
                var content = Child("Content", viewport);
                content.anchorMin = new Vector2(0f, 1f);
                content.anchorMax = Vector2.one;
                content.pivot = new Vector2(.5f, 1f);
                content.sizeDelta = Vector2.zero;
                Label = content.gameObject.AddComponent<TextMeshProUGUI>();
                Label.fontSize = 22f;
                Label.raycastTarget = false;
                Scroll.viewport = viewport;
                Scroll.content = content;
                Scroll.horizontal = false;
                View = root.gameObject.AddComponent<ValidationMessageView>();
                View.Configure(Label);
                Canvas.ForceUpdateCanvases();
            }

            public Button DisclosureButton()
            {
                Assert.That(ActiveDisclosureButtons, Has.Length.EqualTo(1),
                    "A detailed report must offer one real disclosure Button.");
                return ActiveDisclosureButtons[0];
            }

            public void Click(Button button) => ExecuteEvents.Execute(button.gameObject,
                new PointerEventData(Events) { button = PointerEventData.InputButton.Left },
                ExecuteEvents.pointerClickHandler);

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(canvasObject);
                if (eventObject != null) UnityEngine.Object.DestroyImmediate(eventObject);
            }

            private static RectTransform Child(string name, Transform parent)
            {
                var root = new GameObject(name, typeof(RectTransform));
                root.transform.SetParent(parent, false);
                return (RectTransform)root.transform;
            }

            private static void Stretch(RectTransform rect, Vector2 minimum, Vector2 maximum)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = minimum;
                rect.offsetMax = maximum;
            }
        }
    }
}

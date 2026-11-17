#if UNITY_EDITOR
using System.Collections;
using System;
using System.IO;
using System.Linq;
using AnimalCafe.BusinessDay;
using AnimalCafe.CafeLoop;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Feedback;
using AnimalCafe.Tests.PlayMode.EditorSceneLoading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode.Phase15
{
    public sealed class BusinessDayValidationSceneTests : BusinessDayTestSupport
    {
        protected override string RuntimeScenePath => "Assets/Scenes/Validation/Phase15BusinessDay.unity";

        [UnityTest] public IEnumerator ValidationSceneStartsOneReadonlyCycleAndRendersPortraitLandscape()
        {
            var day = Find<BusinessDayController>(); var view = Find<BusinessDayStatusView>();
            Assert.That(day.IsValidationMode, Is.True);
            Assert.That(day.Snapshot.Phase, Is.EqualTo(BusinessDayPhase.Open));
            Assert.That(day.Snapshot.RemainingSeconds, Is.InRange(1, 60));
            Assert.That(view.CurrentText, Does.Contain("Validation").And.Contain("Day 1").And.Contain("Open"));
            var diagnosticView = Find<CafeLoopStatusView>();
            diagnosticView.enabled = false; diagnosticView.enabled = true;
            var diagnostic = (Text)new UnityEditor.SerializedObject(diagnosticView).FindProperty("status").objectReferenceValue;
            Assert.That(diagnostic.enabled && diagnostic.gameObject.activeInHierarchy, Is.True);
            Assert.That(diagnostic.text, Does.Contain("Epoch").And.Contain("Counter").And.Contain("Pickup"));
            var diagnosticBackground = diagnostic.transform.parent.Find("P14 Readonly Background").GetComponent<Image>();
            Assert.That(diagnosticBackground.enabled && diagnosticBackground.gameObject.activeInHierarchy, Is.True);
            var dayText = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Text>(true)).Single(t => t.transform.parent.name == "P15 Readonly Status");
            Assert.That(dayText.transform.parent.GetComponent<Image>().color, Is.EqualTo(new Color(.08f, .1f, .12f, .93f)));
            Assert.That(view.CurrentText, Does.Contain("Open 60s / Rest 15s").And.Contain("LayoutInterrupted"));
            yield return PrepareUnbornAdmission();
            Assert.That(customers.TrySpawn(), Is.True, "Serialized required policy must be connected to the one live owner");
            var output = Path.GetFullPath("outputs/phase15/task-5-screenshots-final"); Directory.CreateDirectory(output);
            var capture = Environment.GetEnvironmentVariable("ANIMALCAFE_P15_NATIVE_CAPTURE") == "1";
            P8RReferenceLayoutTests.NativeScreenSize native = null;
            P8RReferenceLayoutTests.RealGameViewSize gameView = null;
            try
            {
                if (capture) gameView = new P8RReferenceLayoutTests.RealGameViewSize();
                else native = new P8RReferenceLayoutTests.NativeScreenSize();
                foreach (var size in new[] { new Vector2Int(540, 960), new Vector2Int(960, 540) })
                {
                    if (capture) gameView.Resize(size); else native.Resize(size);
                    for (var frame = 0; frame < 8; frame++) yield return null;
                    Canvas.ForceUpdateCanvases();
                    Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(size));
                    // View在业务root上；label通过既有SafeArea分支归属识别。
                    var status = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Text>(true))
                        .Single(t => t.transform.parent.name == "P15 Readonly Status");
                    Assert.That(status.preferredHeight, Is.LessThanOrEqualTo(status.rectTransform.rect.height + 1), "Rendered label must not clip");
                    Assert.That(status.raycastTarget, Is.False); Assert.That(status.GetComponentInParent<SafeAreaContainer>(), Is.Not.Null);
                    var panel = (RectTransform)status.transform.parent;
                    var controls = (RectTransform)Find<AnimalCafe.UI.TimeControlPanel>().transform;
                    Assert.That(Box(panel).Overlaps(Box(controls)), Is.False, "P15 status must leave existing P5 controls unobscured at " + size);
                    var readiness = Find<ValidationMessageView>();
                    Assert.That(readiness.IsVisible, Is.False, "Ready Validation must not reserve a hidden banner");
                    BusinessDayMainSceneTests.AssertHudGeometry(status, Find<AnimalCafe.UI.TimeControlPanel>(), readiness, "ready Validation " + size);
                    if (!capture) { Debug.Log("AC_P15_GEOMETRY_ONLY " + size + "; native screenshot NOT_RUN in batch"); continue; }
                    // 原始Unity framebuffer；不用mockup或图片后处理替代真实渲染。
                    var deadline = Time.realtimeSinceStartup + 45;
                    while (UnityEditor.ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(UnityEditor.ShaderUtil.anythingCompiling, Is.False);
                    var name = $"open-{size.x}x{size.y}.png"; var path = Path.Combine(output, name);
                    Assert.That(File.Exists(path), Is.False, "Do not overwrite earlier review evidence");
                    ScreenCapture.CaptureScreenshot(path);
                    deadline = Time.realtimeSinceStartup + 8;
                    while (!File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
                    Assert.That(File.Exists(path), Is.True, "Normal Editor must provide native Overlay pixels");
                    var image = new Texture2D(2, 2);
                    try
                    {
                        Assert.That(image.LoadImage(File.ReadAllBytes(path)), Is.True);
                        Assert.That(new Vector2Int(image.width, image.height), Is.EqualTo(size));
                        Debug.Log("AC_P15_SCREENSHOT " + name);
                    }
                    finally { UnityEngine.Object.Destroy(image); }
                }
            }
            finally { gameView?.Dispose(); native?.Dispose(); }
        }
        private static Rect Box(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners.Min(c => c.x), corners.Min(c => c.y), corners.Max(c => c.x), corners.Max(c => c.y));
        }

        [UnityTest] public IEnumerator StatusPanelAccommodatesClosingReasonAndInterruptedHistory()
        {
            var view = Find<BusinessDayStatusView>();
            // Freeze only the text publisher; keep the real HUD layout enabled during resize.
            // 停止周期文字刷新，保留真实HUD的resize处理。
            Find<BusinessDayController>().enabled = false;
            var text = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Text>(true))
                .Single(t => t.transform.parent.name == "P15 Readonly Status");
            var day = new BusinessDayService(new BusinessDaySettings(60, 15), 0);
            day.RecordLayoutInterruption(1); day.BeforeService(60); day.AfterService(60, new BusinessDayDrainResult(true, "")); day.BeforeService(75);
            day.RecordLayoutInterruption(2); day.BeforeService(135);
            day.AfterService(135, new BusinessDayDrainResult(false, "Delivery Blocked: 顾客离店路径仍被障碍占用，等待实际服务恢复；不能伪造 Completed。"));
            var closing = day.GetSnapshot(135);
            day.AfterService(135, new BusinessDayDrainResult(true, ""));
            var rest = day.GetSnapshot(135);
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                foreach (var size in new[] { new Vector2Int(540, 960), new Vector2Int(960, 540) })
                foreach (var snapshot in new[] { closing, rest })
                {
                    text.text = BusinessDayStatusView.Format(snapshot, true);
                    screen.Resize(size); yield return BusinessDayMainSceneTests.SettleHud();
                    Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(size));
                    Assert.That(text.text, Does.Contain("Last Day").And.Contain(snapshot.Phase.ToString()));
                    Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), "真实停止原因和上日中断记录不能被截断");
                    BusinessDayMainSceneTests.AssertHudGeometry(text, Find<AnimalCafe.UI.TimeControlPanel>(), Find<ValidationMessageView>(), "long " + snapshot.Phase + " " + size);
                }
            }
        }
    }
}
#endif

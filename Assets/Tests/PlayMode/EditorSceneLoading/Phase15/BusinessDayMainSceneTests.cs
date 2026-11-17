#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using AnimalCafe.BusinessDay;
using AnimalCafe.CafeLoop;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Navigation;
using AnimalCafe.UI;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Feedback;
using AnimalCafe.Tests.PlayMode.EditorSceneLoading;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode.Phase15
{
    public sealed class BusinessDayMainSceneTests
    {
        private Scene scene;
        private UnityEngine.InputSystem.InputActionAsset[] inputs;

        [UnityTest] public IEnumerator MainHudAvoidsVisibleReadinessAt540x960()
        { yield return CheckMainHud(new Vector2Int(540, 960)); }

        [UnityTest] public IEnumerator MainHudAvoidsVisibleReadinessAt960x540()
        { yield return CheckMainHud(new Vector2Int(960, 540)); }

        private IEnumerator CheckMainHud(Vector2Int size)
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            inputs = Phase8SceneInputTestCleanup.CaptureAssets(scene);
            var readiness = Find<ValidationMessageView>();
            var layout = Find<CafeLayoutRuntime>();
            var decor = Find<DecorationModeController>();
            var view = Find<BusinessDayStatusView>();
            var controls = Find<TimeControlPanel>();
            var label = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Text>(true))
                .Single(t => t.transform.parent.name == "P15 Readonly Status");
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(size);
                yield return SettleHud();
                Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(size));
                Assert.That(layout.CurrentReadiness.CanOpenForBusiness, Is.False, "Use original unready Main, never seed validation furniture");
                Assert.That(readiness.IsVisible, Is.True);
                AssertPlayerPresentation();
                AssertHudGeometry(label, controls, readiness, "original unready Main " + size);

                // Public presentation APIs exercise hide/show without changing business readiness.
                // 通过现有显示API检查恢复位置，不修改真实营业条件。
                readiness.Clear(); yield return SettleHud();
                AssertHudGeometry(label, controls, readiness, "hidden " + size);
                var hiddenTop = ScreenBox((RectTransform)label.transform.parent).yMax;
                readiness.ShowReadiness(layout.CurrentReadiness); yield return SettleHud();
                AssertHudGeometry(label, controls, readiness, "shown again " + size);
                Assert.That(ScreenBox((RectTransform)label.transform.parent).yMax, Is.LessThan(hiddenTop - 1), "Visible banner must reserve actual space");
                readiness.enabled = false; yield return SettleHud();
                Assert.That(readiness.IsVisible, Is.True, "Disabling the component leaves its existing Graphic visible");
                AssertHudGeometry(label, controls, readiness, "disabled component still renders banner " + size);
                Assert.That(ScreenBox((RectTransform)label.transform.parent).yMax, Is.LessThan(hiddenTop - 1));
                readiness.enabled = true; yield return SettleHud();
                AssertHudGeometry(label, controls, readiness, "readiness re-enabled " + size);
                readiness.gameObject.SetActive(false); yield return SettleHud();
                Assert.That(ScreenBox((RectTransform)label.transform.parent).yMax, Is.EqualTo(hiddenTop).Within(1), "Inactive readiness must not reserve stale space");
                readiness.gameObject.SetActive(true); yield return SettleHud();
                view.enabled = false; view.enabled = true; yield return SettleHud();
                var serviceView = Find<CafeLoopStatusView>();
                serviceView.enabled = false; serviceView.enabled = true; yield return SettleHud();
                AssertPlayerPresentation();
                AssertHudGeometry(label, controls, readiness, "HUD re-enabled " + size);
                decor.EnterDecorationMode(); yield return SettleHud();
                Assert.That(label.gameObject.activeInHierarchy, Is.False, "Existing shared business HUD is hidden during Decor");
                // Hidden ContentSizeFitter does not run; check the actually visible Decor UI instead.
                // 隐藏HUD不测文字fit，检查真实显示的提示与controls，Done后再测完整HUD。
                Assert.That(controls.gameObject.activeInHierarchy && readiness.gameObject.activeInHierarchy && readiness.IsVisible, Is.True);
                Assert.That(ScreenBox((RectTransform)readiness.transform).Overlaps(ScreenBox((RectTransform)controls.transform)), Is.False, "Decor readiness must leave controls unobscured " + size);
                var timeButton = (Button)new SerializedObject(controls).FindProperty("normalButton").objectReferenceValue;
                var hits = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
                    { position = ScreenBox((RectTransform)timeButton.transform).center }, hits);
                Assert.That(hits, Is.Not.Empty, "Existing time target must remain reachable by raycast during Decor");
                Assert.That(hits[0].gameObject.transform.IsChildOf(timeButton.transform), Is.True, "Readiness must not intercept time controls; Decor keeps its existing speed lock");
                decor.ExitDecorationMode(); yield return SettleHud();
                Assert.That(label.gameObject.activeInHierarchy, Is.True, "Done restores the existing business HUD");
                AssertPlayerPresentation();
                AssertHudGeometry(label, controls, readiness, "Done unready " + size);
                readiness.Clear(); yield return SettleHud();
                Assert.That(ScreenBox((RectTransform)label.transform.parent).yMax, Is.EqualTo(hiddenTop).Within(1), "Hiding after Decor must restore controls-relative placement");
            }
        }

        private void AssertPlayerPresentation()
        {
            var service = Find<CafeLoopStatusView>();
            var status = (Text)new SerializedObject(service).FindProperty("status").objectReferenceValue;
            Assert.That(status.enabled && status.gameObject.activeInHierarchy, Is.False, "P14 diagnostics must remain hidden after refresh/reenable/Done");
            var background = status.transform.parent.Find("P14 Readonly Background").GetComponent<Image>();
            Assert.That(background.enabled && background.gameObject.activeInHierarchy, Is.False);
            Assert.That(Find<BusinessDayStatusView>().CurrentText, Does.Contain("营业条件").And.Not.Contain("LayoutInterrupted").And.Not.Contain("Epoch"));
        }

        internal static IEnumerator SettleHud()
        {
            for (var frame = 0; frame < 8; frame++) yield return null;
            Canvas.ForceUpdateCanvases();
        }

        internal static Rect ScreenBox(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>(true).rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var points = corners.Select(c => RectTransformUtility.WorldToScreenPoint(camera, c)).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }

        internal static void AssertHudGeometry(Text label, TimeControlPanel controls, ValidationMessageView readiness, string context)
        {
            var panel = ScreenBox((RectTransform)label.transform.parent);
            var controlsBox = ScreenBox((RectTransform)controls.transform);
            var safe = ScreenBox((RectTransform)label.GetComponentInParent<SafeAreaContainer>(true).transform);
            Assert.That(panel.Overlaps(controlsBox), Is.False, "P15 HUD overlaps controls: " + context);
            Assert.That(label.preferredHeight, Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1), "P15 content clips: " + context);
            Assert.That(label.raycastTarget, Is.False, "Readonly HUD must pass through input");
            Assert.That(panel.xMin, Is.GreaterThanOrEqualTo(safe.xMin - 1), context);
            Assert.That(panel.xMax, Is.LessThanOrEqualTo(safe.xMax + 1), context);
            Assert.That(panel.yMin, Is.GreaterThanOrEqualTo(safe.yMin - 1), context);
            Assert.That(panel.yMax, Is.LessThanOrEqualTo(safe.yMax + 1), context);
            if (readiness.IsVisible && readiness.gameObject.activeInHierarchy)
            {
                var banner = ScreenBox((RectTransform)readiness.transform);
                Assert.That(banner.Overlaps(controlsBox), Is.False, "Readiness overlaps controls: " + context);
                Assert.That(panel.Overlaps(banner), Is.False, $"P15 HUD overlaps visible readiness: {context}; HUD={panel}; readiness={banner}");
            }
        }

        [UnityTest] public IEnumerator MainUsesFormalCycleAndOriginalUnreadyLayoutRejectsWithoutAllocation()
        {
            scene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            inputs = Phase8SceneInputTestCleanup.CaptureAssets(scene);
            for (var frame = 0; frame < 30; frame++) yield return null;

            var day = Find<BusinessDayController>(); var loop = Find<CafeLoopController>();
            var customers = Find<CustomerFlowController>(); var world = Find<NavigationWorld>();
            var settings = new SerializedObject(day);
            Assert.That(settings.FindProperty("openSeconds").doubleValue, Is.EqualTo(28800));
            Assert.That(settings.FindProperty("restSeconds").doubleValue, Is.EqualTo(7200));
            Assert.That(day.IsValidationMode, Is.False);
            Assert.That(new SerializedObject(customers).FindProperty("requireAdmissionPolicy").boolValue, Is.True);
            Assert.That(new SerializedObject(Find<CafeLoopValidationController>()).FindProperty("seedValidationLayout").boolValue, Is.False);
            var layout = Find<CafeLayoutRuntime>();
            Assert.That(layout.Layout.FurnitureInstances.Count, Is.EqualTo(1),
                "Main keeps the original starter counter; no validation furniture is seeded");
            Assert.That(layout.FunctionalSurfaceLayout.MountedInstances, Is.Empty);
            Assert.That(layout.FunctionalSurfaceLayout.PickUpPoints, Is.Empty);
            Assert.That(layout.CurrentReadiness.CanOpenForBusiness, Is.False);
            Assert.That(day.Snapshot.Phase, Is.EqualTo(BusinessDayPhase.Open));
            Assert.That(day.Snapshot.RemainingSeconds, Is.InRange(28700, 28800));
            Assert.That(Find<BusinessDayStatusView>().CurrentText, Does.Contain("Open").And.Not.Contain("Validation"));
            Assert.That(loop.CanProgress, Is.False, "Scheduled Open cannot bypass actual station readiness");
            Assert.That(loop.ViewSnapshot.Reception, Is.Not.Empty);

            // 真实入口验证：readiness拒绝不能先分配Actor、capacity或Order。
            var actors = world.RegisteredActors.ToArray();
            var capacity = customers.Capacity;
            var reservations = capacity?.GetReservations().Count;
            Assert.That(customers.TrySpawn(), Is.False, loop.ViewSnapshot.Reception);
            CollectionAssert.AreEquivalent(actors, world.RegisteredActors);
            Assert.That(customers.Capacity, Is.SameAs(capacity), "An unready Main need not allocate a ledger");
            Assert.That(customers.Capacity?.GetReservations().Count, Is.EqualTo(reservations));
            Assert.That(customers.Snapshot, Is.Empty);
            Assert.That(loop.Snapshot.Orders, Is.Empty);
            Assert.That(loop.GetDrainSnapshot().IsDrained, Is.False);
            Debug.Log("AC_P15_MAIN_FORMAL_EMPTY_FAILCLOSED " + loop.ViewSnapshot.Reception);
        }

        private T Find<T>() where T : Component => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                SceneManager.SetActiveScene(SceneManager.CreateScene("P15 Main cleanup"));
                yield return SceneManager.UnloadSceneAsync(scene);
            }
            Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputs ?? Array.Empty<UnityEngine.InputSystem.InputActionAsset>());
        }
    }
}
#endif

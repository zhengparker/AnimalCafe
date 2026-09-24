#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    public sealed class P8RActionGeometryTests
    {
        [Test]
        public void ActionAvoidance_AcceptsOnlyAvailableSlotTouchingSafeRightEdge()
        {
            var root = new GameObject("Action boundary regression", typeof(RectTransform), typeof(Canvas));
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/P8R/Prefabs/PF_UI_P8RActionBar.prefab");
                var instance = Object.Instantiate(prefab, root.transform);
                var view = instance.GetComponent<DecorationActionBarView>();
                Assert.That(Field<AnimalCafe.UI.P8R.P8RAppearance>(view, "appearance"), Is.Not.Null);
                var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(view);
                var unit = metrics.Units(1);
                var gap = metrics.Units(8);
                var safe = new Rect(0, 0, 300 * unit, 200 * unit);
                var size = new Vector2(100, 50) * unit;
                // The world obstacle spans the full height: only the exact-width right slot remains.
                // 障碍占满垂直空间，右侧唯一空位必须允许贴住安全区边界。
                var obstacle = new Rect(0, 0, 200 * unit - gap, safe.height);
                var avoid = typeof(DecorationActionBarView).GetMethod("AvoidWorldPresentation",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(avoid, Is.Not.Null);
                var point = (Vector2)avoid.Invoke(view, new object[]
                {
                    new Vector2(100, 100) * unit, size, Vector2.one * .5f, safe, obstacle, null
                });
                var row = new Rect(point - size * .5f, size);
                Inside(row, safe);
                Assert.That(row.Overlaps(obstacle), Is.False, "Rejecting a legal boundary slot must not leave the row over the world obstacle.");
                Assert.That(row.xMin - obstacle.xMax, Is.GreaterThanOrEqualTo(gap - .01f));
                Assert.That(row.xMax, Is.EqualTo(safe.xMax).Within(.01f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator ProductionActionPrefab_SyntheticPortraitLandscape_ClampsFourUsableButtons()
        {
            var external = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(item => new { Item = item, item.enabled }).ToArray();
            foreach (var entry in external) entry.Item.enabled = false;
            var ownedEventSystem = EventSystem.current == null ? new GameObject("P8R Synthetic EventSystem", typeof(EventSystem)) : null;
            try
            {
                foreach (var size in new[] { new Vector2Int(1080, 1920), new Vector2Int(1920, 1080),
                    new Vector2Int(720, 1600), new Vector2Int(1600, 720) })
                    yield return CheckSyntheticGeometry(size);
            }
            finally
            {
                if (ownedEventSystem != null) Object.DestroyImmediate(ownedEventSystem);
                foreach (var entry in external) if (entry.Item != null) entry.Item.enabled = entry.enabled;
            }
        }

        private static IEnumerator CheckSyntheticGeometry(Vector2Int size)
        {
            var target = new RenderTexture(size.x, size.y, 24);
            target.Create();
            var cameraRoot = new GameObject("P8R Synthetic Camera", typeof(UnityEngine.Camera));
            var camera = cameraRoot.GetComponent<UnityEngine.Camera>();
            camera.orthographic = true; camera.targetTexture = target;
            var root = new GameObject("P8R Synthetic Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = .5f;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI/P8R/Prefabs/PF_UI_P8RActionBar.prefab");
            var instance = Object.Instantiate(prefab, root.transform); instance.SetActive(true);
            var safe = new Rect(36, 48, size.x - 72, size.y - 96);
            foreach (var area in instance.GetComponentsInChildren<SafeAreaContainer>(true))
            { area.AutoApplyRuntimeSafeArea = false; area.ApplySafeArea(safe, size); }
            var view = instance.GetComponent<DecorationActionBarView>();
            view.Configure(new UiPointerBoundary(), new UiTransitionRunner(() => true));
            view.SetCatalogueItemActions(DecorationCatalogueItemKind.Furniture, true);
            view.Show(true, true, PlacementFeedbackKey.None);
            try
            {
                Canvas.ForceUpdateCanvases(); yield return null;
                Assert.That(canvas.renderingDisplaySize.x, Is.EqualTo(size.x).Within(.5f));
                Assert.That(canvas.renderingDisplaySize.y, Is.EqualTo(size.y).Within(.5f));
                foreach (var point in new[] { safe.min, safe.max, safe.center })
                {
                    view.SetPresentation(DecorationActionPresentation.Existing, point, safe);
                    Canvas.ForceUpdateCanvases(); yield return null;
                    var buttons = instance.GetComponentsInChildren<Button>().Where(button => button.gameObject.activeInHierarchy).ToArray();
                    Assert.That(buttons.Length, Is.EqualTo(4));
                    var screenRects = buttons.Select(button => ScreenRect(camera, (RectTransform)button.transform)).ToArray();
                    for (var i = 0; i < buttons.Length; i++)
                    {
                        Assert.That(buttons[i].interactable && buttons[i].GetComponent<Image>().raycastTarget, Is.True,
                            "Full-sized hit root remains clickable; decorative face is not the hit region.");
                        Assert.That(buttons[i].image.raycastTarget, Is.False);
                        Assert.That(buttons[i].GetComponent<Image>().color.a, Is.Zero);
                        var face = ScreenRect(camera, buttons[i].image.rectTransform);
                        var density = AnimalCafe.UI.P8R.P8RMobileMetrics.For(buttons[i]).PixelsPerLogicalUnit;
                        Assert.That(face.width / density, Is.EqualTo(30f).Within(.1f), "Approved compact face stays unchanged inside the floating 44x48 hit target.");
                        var corners = new Vector3[4]; ((RectTransform)buttons[i].transform).GetWorldCorners(corners);
                        var logical = corners.Select(corner => root.transform.InverseTransformPoint(corner)).ToArray();
                        Assert.That(logical.Max(p => p.x) - logical.Min(p => p.x), Is.GreaterThanOrEqualTo(43.99f));
                        Assert.That(logical.Max(p => p.y) - logical.Min(p => p.y), Is.GreaterThanOrEqualTo(47.99f));
                        Assert.That(screenRects[i].width / density, Is.EqualTo(44f).Within(.01f));
                        Assert.That(screenRects[i].height / density, Is.GreaterThanOrEqualTo(47.99f));
                        Inside(screenRects[i], safe);
                        for (var j = 0; j < i; j++) Assert.That(screenRects[i].Overlaps(screenRects[j]), Is.False,
                            "Touch x overlap pixels=" + (Mathf.Min(screenRects[i].xMax, screenRects[j].xMax)
                                - Mathf.Max(screenRects[i].xMin, screenRects[j].xMin)).ToString("R"));
                    }
                    var panel = instance.transform.Find("SafeArea/ActionPanel") as RectTransform
                        ?? instance.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "ActionPanel");
                    Inside(ScreenRect(camera, panel), safe);
                }
                Debug.Log("P8R synthetic geometry: " + size + "; four non-overlapping, raycastable 44x48 logical buttons within injected Safe Area. Actual Screen=" + Screen.width + "x" + Screen.height);
                yield return CheckPanelGeometry(root.transform, camera, safe, size);
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(cameraRoot);
                target.Release(); Object.DestroyImmediate(target);
            }
        }

        private static IEnumerator CheckPanelGeometry(Transform parent, UnityEngine.Camera camera, Rect safe, Vector2Int size)
        {
            var catalogueRoot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/UI/P8R/Prefabs/PF_UI_P8RCatalogue.prefab"), parent);
            var modalRoot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/UI/P8R/Prefabs/PF_UI_P8RPutAwayModal.prefab"), parent);
            foreach (var instance in new[] { catalogueRoot, modalRoot })
            {
                instance.SetActive(true);
                foreach (var area in instance.GetComponentsInChildren<SafeAreaContainer>(true))
                { area.AutoApplyRuntimeSafeArea = false; area.ApplySafeArea(safe, size); }
            }
            var catalogue = catalogueRoot.GetComponent<DecorationCatalogueView>();
            // This isolated prefab has no controller. Mirror SetPhase7ChromeVisible:
            // Furniture never displays the Floor-only range controls.
            catalogueRoot.GetComponentInChildren<DecorationFloorRangeView>(true).gameObject.SetActive(false);
            // The modal is tested later; an unconfigured active blocker correctly owns all input.
            modalRoot.SetActive(false);
            catalogue.Configure(new UiPointerBoundary(), new UiTransitionRunner(() => true));
            catalogue.BindCategories("Furniture", new[] { new DecorationCategoryModel("furniture", "Furniture",
                new DecorationCatalogueItemModel[0]) }, null);
            catalogue.SetSheetState(DecorationSheetState.Expanded, false);
            yield return null; Canvas.ForceUpdateCanvases();
            var expanded = Field<GameObject>(catalogue, "expandedRoot");
            var pickup = Field<Button>(catalogue, "pickUpPointButton");
            Inside(ScreenRect(camera, (RectTransform)pickup.transform), ScreenRect(camera, (RectTransform)expanded.transform));
            P8RCompleteFlowTests.AssertPickupReservation(catalogue);
            P8RCompleteFlowTests.AssertTextIconGroup(pickup);
            var tabs = catalogueRoot.GetComponentInChildren<DecorationModeTabsView>();
            var tabRects = new[] { "furnitureButton", "floorButton", "wallButton", "wallDecorButton" }
                .Select(name => Field<Button>(tabs, name)).ToArray();
            Debug.Log("P8R tab diagnostic " + size + ": parent=" + ((RectTransform)tabs.transform.parent).rect
                + "; row=" + ((RectTransform)tabs.transform).rect + "; position=" + ((RectTransform)tabs.transform).anchoredPosition
                + "; parentScreen=" + ScreenRect(camera, (RectTransform)tabs.transform.parent)
                + "; lastTab=" + ScreenRect(camera, (RectTransform)tabRects[3].transform));
            foreach (var mode in new[] { DecorationModeKind.Furniture, DecorationModeKind.Floor, DecorationModeKind.Wall, DecorationModeKind.WallDecor })
            {
                tabs.SetActive(mode); Canvas.ForceUpdateCanvases();
                for (var i = 0; i < tabRects.Length; i++)
                {
                    var rect = ScreenRect(camera, (RectTransform)tabRects[i].transform);
                    Inside(rect, safe); FitsLabel(tabRects[i].transform.Find("Label").GetComponent<TMP_Text>());
                    P8RCompleteFlowTests.AssertTextIconGroup(tabRects[i]);
                    for (var j = 0; j < i; j++) Assert.That(rect.Overlaps(ScreenRect(camera, (RectTransform)tabRects[j].transform)), Is.False);
                }
            }
            var modal = modalRoot.GetComponent<DecorationStoreModalView>();
            modalRoot.SetActive(true);
            modal.Configure(new UiNavigationCoordinator(), new UiPauseCoordinator(parent.gameObject.AddComponent<AnimalCafe.Core.Time.GameTimeService>()),
                new UiPointerBoundary(), new UiTransitionRunner(() => true));
            modal.Show(AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>("Assets/UI/P8R/DC_P8RFurniture.asset").Entries[0].Definition);
            yield return null; Canvas.ForceUpdateCanvases();
            Inside(ScreenRect(camera, modal.ContentRect), safe);
            var body = Field<TMP_Text>(modal, "bodyLabel");
            Assert.That(body.GetPreferredValues(body.text, body.rectTransform.rect.width, Mathf.Infinity).y,
                Is.LessThanOrEqualTo(body.rectTransform.rect.height + .1f));
            foreach (var name in new[] { "confirmButton", "cancelButton" })
            {
                var button = Field<Button>(modal, name);
                Inside(ScreenRect(camera, (RectTransform)button.transform), ScreenRect(camera, modal.ContentRect));
                FitsLabel(button.transform.Find("Label").GetComponent<TMP_Text>());
                P8RCompleteFlowTests.AssertTextIconGroup(button);
            }
            Debug.Log("P8R synthetic panel geometry " + size + ": centered modal and readable tabs/pickup are bounded; no overlapping tabs.");
        }

        private static void FitsLabel(TMP_Text label)
        {
            var preferred = label.GetPreferredValues(label.text);
            Assert.That(label.rectTransform.rect.width, Is.GreaterThanOrEqualTo(preferred.x - .1f), label.text);
            Assert.That(label.rectTransform.rect.height, Is.GreaterThanOrEqualTo(preferred.y - .1f), label.text);
        }
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private static Rect ScreenRect(UnityEngine.Camera camera, RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var points = corners.Select(camera.WorldToScreenPoint).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
        private static void Inside(Rect rect, Rect safe)
        {
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(safe.xMin - .6f));
            Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(safe.yMin - .6f));
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(safe.xMax + .6f));
            Assert.That(rect.yMax, Is.LessThanOrEqualTo(safe.yMax + .6f));
        }
    }
}
#endif

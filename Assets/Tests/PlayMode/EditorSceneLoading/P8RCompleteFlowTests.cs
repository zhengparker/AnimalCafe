#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.Core.Time;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Layout;
using AnimalCafe.UI;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    public sealed class P8RCompleteFlowTests
    {
        private static T Find<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static void Call(object owner, string name) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, null);
        private static IEnumerator Load()
        {
            EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null; yield return null;
        }
        private static DecorationCatalogueTileView Tile(string id) => Find<DecorationCatalogueView>()
            .GetComponentsInChildren<DecorationCatalogueTileView>(true).Single(t => t.ItemId == id && !t.GetComponentsInParent<Transform>(true)
                .Any(parent => parent.name.StartsWith("CategoryRow_", StringComparison.Ordinal) && !parent.gameObject.activeSelf));
        private static void Select(string id) => Tile(id).GetComponent<Button>().onClick.Invoke();
        private static Button Action(string field) => Field<Button>(Find<DecorationActionBarView>(), field);

        [UnityTest]
        public IEnumerator TabSwitch_FurnitureCatalogueKeepsPreviewOutlineWithoutUsingChecks()
        {
            foreach (var id in new[] { "equipment.cash-register.01", "equipment.coffee-machine.01" })
            {
                // MainCafe starts with one counter slot, so each equipment case needs a fresh scene.
                // 默认柜台只有一个 Slot；每种设备独立验证，避免第二件被第一件的 occupancy 正常拒绝。
                yield return Load();
                var controller = Find<DecorationModeController>();
                controller.EnterDecorationMode();
                var catalogue = Find<DecorationCatalogueView>();
                Assert.That(Tile("furniture.counter.module.01").transform.Find("UsingCheck").gameObject.activeSelf, Is.False,
                    "A confirmed counter must not imply the catalogue card is an exclusive selection.");
                Select(id);
                catalogue.ShowCatalogue();
                Assert.That(Tile(id).transform.Find("PreviewOutline").gameObject.activeSelf, Is.True);
                Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True, id + " must occupy the fresh counter slot.");
                catalogue.ShowCatalogue();
                Assert.That(Tile(id).transform.Find("UsingCheck").gameObject.activeSelf, Is.False,
                    "Confirmed equipment may be added again without a green using check.");
                Assert.That(Tile(id).transform.Find("PreviewOutline").gameObject.activeSelf, Is.False);
                Assert.That(catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                    .Where(tile => tile.gameObject.activeInHierarchy)
                    .Any(tile => tile.transform.Find("UsingCheck").gameObject.activeSelf), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator Proportions_ExitReopenKeepsBothTextOnlyChoicesReadable()
        {
            yield return Load(); var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
            Select("furniture.counter.module.01");
            var exit = Find<DecorationExitModalView>();
            for (var pass = 0; pass < 2; pass++)
            {
                controller.TryRequestExit(); yield return new WaitForSecondsRealtime(.2f); Canvas.ForceUpdateCanvases();
                foreach (var field in new[] { "continueButton", "discardButton" })
                {
                    var button = Field<Button>(exit, field); var label = button.transform.Find("Label").GetComponent<TMP_Text>();
                    Assert.That(button.transform.Find("Icon").gameObject.activeSelf, Is.False);
                    Assert.That(label.rectTransform.rect.width, Is.GreaterThanOrEqualTo(label.GetPreferredValues(label.text).x), field);
                    Assert.That(ScreenRect(label.rectTransform).center.x,
                        Is.EqualTo(ScreenRect((RectTransform)button.transform).center.x).Within(.2f));
                }
                Field<Button>(exit, "continueButton").onClick.Invoke(); yield return null;
                Assert.That(Field<DecorationSession>(controller, "session").ActivePreview, Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator Proportions_UnchangedTabsDoNotForceTextReparseEachCanvasRender()
        {
            yield return Load(); Find<DecorationModeController>().EnterDecorationMode();
            yield return new WaitForSecondsRealtime(.4f); Canvas.ForceUpdateCanvases(); yield return null;
            var label = Field<Button>(Find<DecorationModeTabsView>(), "furnitureButton").transform.Find("Label").GetComponent<TMP_Text>();
            var rebuilds = 0; Action<TMP_TextInfo> onRender = _ => rebuilds++;
            label.OnPreRenderText += onRender;
            try
            {
                for (var frame = 0; frame < 3; frame++) { Canvas.ForceUpdateCanvases(); yield return null; }
                Assert.That(rebuilds, Is.Zero, "Unchanged tabs must not force TMP mesh parsing on each Canvas render.");
            }
            finally { label.OnPreRenderText -= onRender; }
        }

        [UnityTest]
        public IEnumerator Proportions_PickupAndVisibleContentAreCenteredInExpandedPanel()
        {
            yield return Load(); Find<DecorationModeController>().EnterDecorationMode();
            yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
            var view = Find<DecorationCatalogueView>();
            var button = Field<Button>(view, "pickUpPointButton");
            AssertPickupReservation(view);
            AssertTextIconGroup(button);
        }

        // The short landscape variant uses the header; ordinary layouts retain the centered footer.
        // 横屏紧凑版允许使用标题行，但不能与其他控件重叠；常规版继续在底部居中。
        internal static void AssertPickupReservation(DecorationCatalogueView catalogue)
        {
            var pickup = Field<Button>(catalogue, "pickUpPointButton");
            var panel = ScreenRect((RectTransform)Field<GameObject>(catalogue, "expandedRoot").transform);
            var hit = ScreenRect((RectTransform)pickup.transform);
            var viewport = ScreenRect(catalogue.VerticalScroll.viewport);
            var density = AnimalCafe.UI.P8R.P8RMobileMetrics.For(pickup).PixelsPerLogicalUnit;
            Assert.That(hit.width / density, Is.GreaterThanOrEqualTo(47.9f));
            Assert.That(hit.height / density, Is.GreaterThanOrEqualTo(47.9f));
            Assert.That(hit.xMin, Is.GreaterThanOrEqualTo(panel.xMin - 1f));
            Assert.That(hit.xMax, Is.LessThanOrEqualTo(panel.xMax + 1f));
            Assert.That(hit.yMin, Is.GreaterThanOrEqualTo(panel.yMin - 1f));
            Assert.That(hit.yMax, Is.LessThanOrEqualTo(panel.yMax + 1f));
            Assert.That(pickup.transform.IsChildOf(catalogue.VerticalScroll.viewport), Is.False);
            Assert.That(hit.Overlaps(viewport), Is.False, "Fixed Pickup must not steal catalogue scrolling.");
            if (hit.yMin >= viewport.yMax - .5f)
            {
                var tabs = catalogue.GetComponentInChildren<DecorationModeTabsView>().GetComponentsInChildren<Button>();
                Assert.That(hit.center.y, Is.EqualTo(ScreenRect((RectTransform)tabs[0].transform).center.y).Within(.6f),
                    "Header Pickup must share the tab row, not float above it.");
            }
            else
            {
                Assert.That(hit.center.x, Is.EqualTo(panel.center.x).Within(.6f), "Footer Pickup stays panel-centered.");
                Assert.That(hit.width, Is.GreaterThan(panel.width * .85f));
                Assert.That(hit.yMax, Is.LessThan(viewport.yMin));
            }
            foreach (var other in catalogue.GetComponentsInChildren<Button>())
                if (other != pickup && !other.transform.IsChildOf(catalogue.VerticalScroll.viewport))
                    Assert.That(hit.Overlaps(ScreenRect((RectTransform)other.transform)), Is.False,
                        "Pickup overlaps fixed " + other.name);
            var title = Field<GameObject>(catalogue, "expandedRoot").transform.Find("P8RCatalogueTitle");
            if (title != null && title.gameObject.activeInHierarchy)
                Assert.That(hit.Overlaps(ScreenRect((RectTransform)title)), Is.False, "Pickup overlaps Catalogue title.");
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = hit.center }, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(pickup));
        }

        [UnityTest]
        public IEnumerator Proportions_FloatingFaceIsSmallerThanPreservedClickableTarget()
        {
            yield return Load(); Find<DecorationModeController>().EnterDecorationMode(); Select("furniture.counter.module.01");
            yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
            foreach (var field in new[] { "cancelButton", "rotateButton", "confirmButton" })
            {
                var button = Action(field); var hit = ScreenRect((RectTransform)button.transform);
                var face = ScreenRect(button.image.rectTransform);
                var scale = button.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
                var density = AnimalCafe.UI.P8R.P8RMobileMetrics.For(button).PixelsPerLogicalUnit;
                Assert.That(hit.width / density, Is.EqualTo(44f).Within(.01f), field + " approved floating hit width");
                Assert.That(face.width / density, Is.EqualTo(30f).Within(.1f), field + " compact platform-logical face");
                Assert.That(face.xMin, Is.GreaterThanOrEqualTo(hit.xMin - .1f));
                Assert.That(face.xMax, Is.LessThanOrEqualTo(hit.xMax + .1f));
                Assert.That(face.center.y, Is.EqualTo(hit.center.y).Within(.1f));
                Assert.That(button.GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(MeasuredInk(button.transform.Find("Icon").GetComponent<Image>()).width,
                    Is.GreaterThanOrEqualTo(17.9f * density), field + " recognizable compact icon ink independent of invisible hit padding");
            }
            var rotate = Action("rotateButton"); var hitRect = ScreenRect((RectTransform)rotate.transform);
            var edge = new Vector2(hitRect.xMin + .25f * rotate.GetComponentInParent<Canvas>().rootCanvas.scaleFactor, hitRect.center.y);
            Assert.That(ScreenRect(rotate.image.rectTransform).Contains(edge), Is.False, "Exercise invisible hit padding, not face.");
            var data = new PointerEventData(EventSystem.current) { position = edge, button = PointerEventData.InputButton.Left };
            var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(rotate), "Actual GraphicRaycaster owns padded edge.");
            var clicks = 0; rotate.onClick.AddListener(() => clicks++);
            ExecuteEvents.Execute(rotate.gameObject, data, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(rotate.gameObject, data, ExecuteEvents.pointerDownHandler);
            Assert.That(rotate.image.overrideSprite.name, Is.EqualTo("button_secondary_pressed"));
            ExecuteEvents.Execute(rotate.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(rotate.gameObject, data, ExecuteEvents.pointerClickHandler);
            Assert.That(clicks, Is.EqualTo(1));
            rotate.interactable = false;
            ExecuteEvents.Execute(rotate.gameObject, data, ExecuteEvents.pointerClickHandler);
            Assert.That(clicks, Is.EqualTo(1), "Disabled padded target must not execute.");
            Assert.That(rotate.image.overrideSprite.name, Is.EqualTo("button_disabled"));
        }

        [UnityTest]
        public IEnumerator FloatingActions_SurfaceToFurnitureAndWallDecor_ResetIconAlignmentEveryTime()
        {
            yield return Load();
            var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
            const string folder = "outputs/p8r-action-alignment-20260911";
            foreach (var surfaceMode in new[] { DecorationModeKind.Floor, DecorationModeKind.Wall })
            {
                Assert.That(controller.TryChangeMode(surfaceMode), Is.True);
                if (surfaceMode == DecorationModeKind.Wall)
                    Assert.That(controller.TryHandleSceneTap(new DecorationTouchHit(DecorationTouchHitKind.WallSurface, surfaceId: "wall.back-left")), Is.True);
                Select(surfaceMode == DecorationModeKind.Floor ? "floor.warm-wood" : "paint.sage");
                yield return null; Canvas.ForceUpdateCanvases();
                foreach (var field in new[] { "cancelButton", "confirmButton" })
                {
                    var button = Action(field);
                    Assert.That(button.transform.Find("Icon").gameObject.activeSelf, Is.False);
                    var face = ScreenRect(button.image.rectTransform);
                    var hit = ScreenRect((RectTransform)button.transform);
                    var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(button);
                    var label = button.transform.Find("Label").GetComponent<TMP_Text>();
                    label.ForceMeshUpdate(true, true);
                    var preferred = label.GetPreferredValues(label.text);
                    Assert.That(label.text, Is.EqualTo(field == "confirmButton" ? "Apply" : "Cancel"),
                        surfaceMode + " " + field + " uses its final surface copy.");
                    Assert.That(label.fontSize / metrics.UnitsPerLogicalUnit, Is.EqualTo(12f).Within(.1f),
                        surfaceMode + " " + field + " compact surface font.");
                    Assert.That(face.width / metrics.PixelsPerLogicalUnit,
                        Is.EqualTo(preferred.x / metrics.UnitsPerLogicalUnit + 12f).Within(.2f),
                        surfaceMode + " " + field + " keeps 6 logical units beside each side of its copy.");
                    Assert.That(face.height / metrics.PixelsPerLogicalUnit, Is.EqualTo(32f).Within(.1f),
                        surfaceMode + " " + field + " compact surface face.");
                    Assert.That(hit.width / metrics.PixelsPerLogicalUnit, Is.GreaterThanOrEqualTo(47.9f),
                        surfaceMode + " " + field + " keeps a separate touch root.");
                    Assert.That(hit.height / metrics.PixelsPerLogicalUnit, Is.GreaterThanOrEqualTo(47.9f),
                        surfaceMode + " " + field + " keeps a separate touch root.");
                    Assert.That(face.xMin, Is.GreaterThanOrEqualTo(hit.xMin - .1f), surfaceMode + " " + field);
                    Assert.That(face.xMax, Is.LessThanOrEqualTo(hit.xMax + .1f), surfaceMode + " " + field);
                    Assert.That(face.yMin, Is.GreaterThanOrEqualTo(hit.yMin - .1f), surfaceMode + " " + field);
                    Assert.That(face.yMax, Is.LessThanOrEqualTo(hit.yMax + .1f), surfaceMode + " " + field);
                    Assert.That(label.isTextTruncated, Is.False, surfaceMode + " " + field);
                }
                yield return Capture("alignment-" + surfaceMode + "-footer.png", folder);
                Action("cancelButton").onClick.Invoke();
                Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
                Assert.That(controller.TryBeginWallMountedPreview("wall-decor.wood-shelf.01", "wall.back-left", new WallSlotPosition(4, 0)), Is.True);
                yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
                yield return Capture("alignment-" + surfaceMode + "-to-shelf.png", folder);
                AssertFloatingInkCentered("cancelButton", "confirmButton");
                Action("confirmButton").onClick.Invoke();
                var shelf = Field<WallMountedLayout>(controller, "phase7WallMountedLayout").CaptureSnapshot().Instances
                    .Single(item => item.DefinitionId == "wall-decor.wood-shelf.01");
                Assert.That(controller.TryHandleSceneTap(new DecorationTouchHit(DecorationTouchHitKind.WallMounted, targetId: shelf.InstanceId)), Is.True);
                yield return null; Canvas.ForceUpdateCanvases();
                AssertFloatingInkCentered("storeButton", "cancelButton", "confirmButton");
                yield return Capture("alignment-" + surfaceMode + "-existing-shelf.png", folder);
                Action("storeButton").onClick.Invoke();
                Field<Button>(Find<DecorationStoreModalView>(), "confirmButton").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
                Select("furniture.counter.module.01");
                yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
                AssertFloatingInkCentered("cancelButton", "rotateButton", "confirmButton");
                yield return Capture("alignment-" + surfaceMode + "-to-furniture.png", folder);
                Action("cancelButton").onClick.Invoke();
            }
        }

        private static void AssertFloatingInkCentered(params string[] fields)
        {
            foreach (var field in fields)
            {
                var button = Action(field); var face = ScreenRect(button.image.rectTransform);
                var icon = button.transform.Find("Icon").GetComponent<Image>();
                Assert.That(icon.gameObject.activeInHierarchy, Is.True, field);
                var ink = MeasuredInk(icon);
                Assert.That(ink.center.x, Is.EqualTo(face.center.x).Within(.5f), field + " visible icon must return from text offset to face center");
                Assert.That(ink.center.y, Is.EqualTo(face.center.y).Within(.5f), field + " vertical ink center");
                Assert.That(face.Contains(ink.min) && face.Contains(ink.max), Is.True, field + " ink stays inside face");
                var scale = button.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
                Assert.That(face.width / AnimalCafe.UI.P8R.P8RMobileMetrics.For(button).PixelsPerLogicalUnit,
                    Is.EqualTo(30f).Within(.1f), field + " compact platform-logical face");
            }
        }

        [UnityTest]
        public IEnumerator Proportions_TextButtonIconsHaveReadableInkAndUnclippedLabels()
        {
            yield return Load(); Find<DecorationModeController>().EnterDecorationMode();
            yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
            var catalogue = Find<DecorationCatalogueView>();
            var buttons = Find<DecorationModeTabsView>().GetComponentsInChildren<Button>()
                .Concat(Find<TimeControlPanel>().GetComponentsInChildren<Button>())
                .Concat(new[] { Field<Button>(catalogue, "pickUpPointButton") });
            foreach (var button in buttons) AssertTextIconGroup(button);
            var status = Field<Image>(Find<ValidationMessageView>(), "statusIcon");
            var ink = MeasuredInk(status); var scale = status.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
            Assert.That(Mathf.Max(ink.width, ink.height) / AnimalCafe.UI.P8R.P8RMobileMetrics.For(status).PixelsPerLogicalUnit,
                Is.GreaterThanOrEqualTo(19.9f), "Compact readiness status ink in platform logical units");
        }

        internal static void AssertTextIconGroup(Button button)
        {
            var icon = button.transform.Find("Icon")?.GetComponent<Image>();
            Assert.That(icon != null && icon.gameObject.activeInHierarchy, Is.True, button.name);
            var ink = MeasuredInk(icon); var scale = button.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
            var visibleLabel = button.transform.Find("Label").GetComponent<TMP_Text>();
            if (!visibleLabel.gameObject.activeSelf)
            {
                var catalogueTab = button.GetComponentInParent<DecorationModeTabsView>() != null;
                Assert.That(catalogueTab || button.GetComponentInParent<TimeControlPanel>() != null, Is.True,
                    "Only the approved HUD controls and four catalogue tabs are icon-only in this group.");
                // Time state sprites span the shared strip; only this segment's clip is visible.
                // 时间图标应对齐各自可见分段，不是整条底板的中心。
                var visibleFace = button.transform.Find("P8RTimeSegmentClip") as RectTransform;
                var face = ScreenRect(visibleFace != null ? visibleFace : button.image.rectTransform);
                Assert.That(Mathf.Max(ink.width, ink.height) / AnimalCafe.UI.P8R.P8RMobileMetrics.For(button).PixelsPerLogicalUnit,
                    Is.GreaterThanOrEqualTo(catalogueTab ? 19.9f : 17.9f));
                Assert.That(ink.center.x, Is.EqualTo(face.center.x).Within(.5f));
                Assert.That(ink.center.y, Is.EqualTo(face.center.y).Within(.5f));
                Assert.That(face.Contains(ink.min) && face.Contains(ink.max), Is.True);
                return;
            }
            var stacked = button.name == "DecorationModeButton" || button.GetComponentInParent<DecorationModeTabsView>() != null;
            Assert.That(Mathf.Max(ink.width, ink.height) / AnimalCafe.UI.P8R.P8RMobileMetrics.For(button).PixelsPerLogicalUnit,
                Is.GreaterThanOrEqualTo(19.9f), button.name + " compact platform-logical visible ink, excluding transparent PNG margins");
            var label = button.transform.Find("Label").GetComponent<TMP_Text>(); label.ForceMeshUpdate();
            var bounds = label.textBounds;
            var canvas = label.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var min = RectTransformUtility.WorldToScreenPoint(camera, label.transform.TransformPoint(bounds.min));
            var max = RectTransformUtility.WorldToScreenPoint(camera, label.transform.TransformPoint(bounds.max));
            var hit = ScreenRect((RectTransform)button.transform);
            if (stacked)
            {
                Assert.That(ink.yMin, Is.GreaterThanOrEqualTo(max.y), button.name + " stacked icon stays above text ink");
                Assert.That(ink.center.x, Is.EqualTo(hit.center.x).Within(1.5f * scale));
                Assert.That((min.x + max.x) * .5f, Is.EqualTo(hit.center.x).Within(1.5f * scale));
                Assert.That(min.x, Is.GreaterThanOrEqualTo(hit.xMin)); Assert.That(max.x, Is.LessThanOrEqualTo(hit.xMax));
                Assert.That(min.y, Is.GreaterThanOrEqualTo(hit.yMin)); Assert.That(ink.yMax, Is.LessThanOrEqualTo(hit.yMax));
                return;
            }
            Assert.That(min.x - ink.xMax, Is.GreaterThanOrEqualTo(3f * scale), button.name + " icon/text gap");
            Assert.That((ink.xMin + max.x) * .5f, Is.EqualTo(hit.center.x).Within(1.5f * scale), button.name + " visible content group center; text="
                + label.text + "; font=" + label.fontSize + "; preferred=" + label.GetPreferredValues(label.text)
                + "; bounds=" + bounds + "; labelPos=" + label.rectTransform.anchoredPosition
                + "; labelRect=" + label.rectTransform.rect + "; margin=" + label.margin
                + "; iconRect=" + icon.rectTransform.rect + "; iconPos=" + icon.rectTransform.anchoredPosition);
            Assert.That(ink.xMin, Is.GreaterThanOrEqualTo(hit.xMin)); Assert.That(max.x, Is.LessThanOrEqualTo(hit.xMax));
            Assert.That(label.GetPreferredValues(label.text).y, Is.LessThanOrEqualTo(label.rectTransform.rect.height + .1f), button.name);
        }

        // Test-only PNG alpha measurement: expected ink comes from actual immutable art, not runtime layout constants.
        internal static Rect MeasuredInk(Image image)
        {
            var texture = new Texture2D(2, 2);
            try
            {
                texture.LoadImage(File.ReadAllBytes(UnityEditor.AssetDatabase.GetAssetPath(image.sprite)));
                var pixels = texture.GetPixels32(); var minX = texture.width; var minY = texture.height; var maxX = -1; var maxY = -1;
                for (var y = 0; y < texture.height; y++) for (var x = 0; x < texture.width; x++)
                    if (pixels[y * texture.width + x].a >= 16) { minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y); maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y); }
                var rect = ScreenRect(image.rectTransform);
                // Image displays the Sprite rect, not necessarily the full source PNG.
                // 按实际 Sprite 范围换算，避免把已裁去的透明留白重复扣除；整图 Sprite 结果不变。
                var source = image.sprite.rect;
                return Rect.MinMaxRect(rect.xMin + rect.width * (minX - source.xMin) / source.width,
                    rect.yMin + rect.height * (minY - source.yMin) / source.height,
                    rect.xMin + rect.width * (maxX + 1 - source.xMin) / source.width,
                    rect.yMin + rect.height * (maxY + 1 - source.yMin) / source.height);
            }
            finally { Object.Destroy(texture); }
        }

        [UnityTest]
        public IEnumerator ReachableHudAndSurfaceControls_Stay48LogicalAndDoNotOverlap()
        {
            yield return Load();
            var c = Find<DecorationModeController>(); c.EnterDecorationMode();
            c.TryChangeMode(DecorationModeKind.Floor); Select("floor.warm-wood");
            yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
            var hud = Find<TimeControlPanel>();
            var buttons = hud.GetComponentsInChildren<Button>().Concat(Find<DecorationFloorRangeView>().GetComponentsInChildren<Button>())
                .Concat(Find<DecorationActionBarView>().GetComponentsInChildren<Button>()).Where(b => b.gameObject.activeInHierarchy).ToArray();
            foreach (var b in buttons)
            {
                var rect = ScreenRect((RectTransform)b.transform);
                var density = AnimalCafe.UI.P8R.P8RMobileMetrics.For(b).PixelsPerLogicalUnit;
                Assert.That(rect.height / density, Is.GreaterThanOrEqualTo(47.99f), b.name + " platform logical target height");
                Assert.That(rect.width / density, Is.GreaterThanOrEqualTo(47.99f), b.name + " platform logical target width");
                Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(0), b.name);
                Assert.That(rect.yMax, Is.LessThanOrEqualTo(Screen.height), b.name);
            }
            var range = Find<DecorationFloorRangeView>().GetComponentsInChildren<Button>();
            foreach (var a in Find<DecorationActionBarView>().GetComponentsInChildren<Button>())
                foreach (var b in range)
                    Assert.That(ScreenRect((RectTransform)a.transform).Overlaps(ScreenRect((RectTransform)b.transform)), Is.False, a.name + " / " + b.name);
            var catalogue = Find<DecorationCatalogueView>();
            catalogue.ShowCatalogue();
            yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
            var scroll = Field<ScrollRect>(catalogue, "verticalScroll");
            Assert.That(ScreenRect(scroll.viewport).height / AnimalCafe.UI.P8R.P8RMobileMetrics.For(catalogue).PixelsPerLogicalUnit,
                Is.GreaterThanOrEqualTo(47.9f), "A surface preview must leave a usable platform-logical catalogue viewport; Screen="
                + Screen.width + "x" + Screen.height + "; logical=" + AnimalCafe.UI.P8R.P8RMobileMetrics.For(catalogue).LogicalViewport
                + "; state=" + catalogue.SheetState + "; expanded=" + Field<GameObject>(catalogue, "expandedRoot").activeInHierarchy
                + "; panel=" + ScreenRect((RectTransform)Field<GameObject>(catalogue, "expandedRoot").transform)
                + "; viewport=" + ScreenRect(scroll.viewport) + "; footer=" + ScreenRect(catalogue.SurfaceFooterHost)
                + "; readiness=" + ScreenRect((RectTransform)Find<ValidationMessageView>().transform));
            var sheet = ScreenRect((RectTransform)Field<GameObject>(catalogue, "expandedRoot").transform);
            foreach (var b in hud.GetComponentsInChildren<Button>())
                Assert.That(sheet.Overlaps(ScreenRect((RectTransform)b.transform)), Is.False, "Sheet covers HUD: " + b.name);
            Assert.That(Field<TMP_Text>(catalogue, "editingContextLabel").gameObject.activeInHierarchy, Is.False);
            catalogue.SetSheetState(DecorationSheetState.CompactPreview, hasActivePreview: true);
            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();
            yield return Capture("26-floor-compact.png");
            var handle = ScreenRect(catalogue.CollapsedHandleRect);
            var tabs = ScreenRect((RectTransform)Find<DecorationModeTabsView>().transform);
            var gap = 6f * AnimalCafe.UI.P8R.P8RMobileMetrics.For(catalogue).PixelsPerLogicalUnit;
            Assert.That(handle.yMin, Is.GreaterThanOrEqualTo(range.Max(b => ScreenRect((RectTransform)b.transform).yMax) + gap),
                "Compact Floor order: actions, range, handle, tabs; all remain separate.");
            Assert.That(tabs.yMin, Is.GreaterThanOrEqualTo(handle.yMax + gap));
            Assert.That(tabs.yMax, Is.LessThanOrEqualTo(Screen.height));
        }
        private static Rect ScreenRect(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var a = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var b = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }

        [UnityTest]
        public IEnumerator HudAndExit_KeepExactPriorSpeedAndOnlyDiscardCurrentPreview()
        {
            yield return Load();
            var controller = Find<DecorationModeController>();
            var service = Find<GameTimeService>();
            var hud = Find<TimeControlPanel>();
            service.SetNormal(); yield return Capture("01-hud-1x.png");
            service.SetFast(); yield return null;
            Assert.That(Field<Button>(hud, "fastButton").image.sprite.name, Is.EqualTo("tab_selected"));
            yield return Capture("02-hud-2x.png");
            service.SetPaused(); yield return Capture("03-hud-paused.png");
            service.SetFast();
            controller.EnterDecorationMode(); yield return null;
            Assert.That(service.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Assert.That(Field<Button>(hud, "pauseButton").GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("Paused"));
            yield return Capture("04-hud-decoration-lock.png");
            Assert.That(controller.TryRequestExit(), Is.True);
            Assert.That(service.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
            controller.EnterDecorationMode();
            var runtime = Find<CafeLayoutRuntime>();
            var before = runtime.Layout.FurnitureInstances.Count;
            Select("furniture.counter.module.01");
            var session = Field<DecorationSession>(controller, "session");
            session.MovePreview(new GridPosition(5, 4));
            Call(controller, "SyncActivePreviewPresentation");
            controller.TryRequestExit();
            var exit = Find<DecorationExitModalView>();
            var preview = session.ActivePreview;
            yield return Capture("05-exit-preview-modal.png");
            Field<Button>(exit, "continueButton").onClick.Invoke();
            Assert.That(session.ActivePreview, Is.SameAs(preview));
            controller.TryRequestExit();
            Field<Button>(exit, "discardButton").onClick.Invoke();
            yield return null;
            Assert.That(session.ActivePreview, Is.Null);
            Assert.That(runtime.Layout.FurnitureInstances.Count, Is.EqualTo(before));
            Assert.That(service.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
        }

        [UnityTest]
        public IEnumerator SurfaceModes_ShowApprovedImagesAndRealAppliedPreviewStates()
        {
            yield return Load();
            var c = Find<DecorationModeController>(); c.EnterDecorationMode();
            Assert.That(c.TryChangeMode(DecorationModeKind.Floor), Is.True);
            yield return Capture("06-floor-catalogue-whole-room.png");
            Select("floor.warm-wood");
            yield return Capture("07-floor-preview-apply-all-undo.png");
            foreach (var field in new[] { "undoLastButton", "rotateButton", "applyAllButton" })
            {
                Assert.That(Action(field).interactable, Is.False, "Whole Room: " + field);
                Assert.That(Action(field).image.overrideSprite.name, Is.EqualTo("button_disabled"), "Whole Room rendered state: " + field);
            }
            Assert.That(Find<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Where(t => t.ItemId != null).All(t => !Field<GameObject>(t, "usingCheck").activeSelf), Is.True,
                "Whole Room must never invent a single applied style.");
            Assert.That(Action("confirmButton").transform.Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo("Apply"));
            Action("confirmButton").onClick.Invoke();
            Assert.That(c.ActiveSurfacePreview, Is.Null);
            Assert.That(c.TrySelectFloorRange(SurfaceEditScope.SingleGridFloor), Is.True);
            Assert.That(c.TrySelectFloorTarget(new GridPosition(1, 1)), Is.True);
            Select("floor.light-tile");
            foreach (var field in new[] { "undoLastButton", "rotateButton", "applyAllButton" })
            {
                Assert.That(Action(field).interactable, Is.True, "Single Grid: " + field);
                Assert.That(Action(field).image.overrideSprite.name, Is.Not.EqualTo("button_disabled"), "Single Grid rendered state: " + field);
            }
            Action("applyAllButton").onClick.Invoke();
            Action("undoLastButton").onClick.Invoke();
            yield return Capture("08-floor-single-grid-preview.png");
            Action("cancelButton").onClick.Invoke();
            Assert.That(c.TryChangeMode(DecorationModeKind.Wall), Is.True);
            Assert.That(c.TryHandleSceneTap(new DecorationTouchHit(DecorationTouchHitKind.WallSurface, surfaceId: "wall.back-left")), Is.True);
            Select("paint.sage");
            Select("wainscoting.sage-plain");
            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();
            Field<ScrollRect>(Find<DecorationCatalogueView>(), "verticalScroll").verticalNormalizedPosition = 0;
            yield return Capture("09-wall-base-and-wains-preview.png");
            Action("confirmButton").onClick.Invoke();
            Assert.That(Field<GameObject>(Tile("paint.sage"), "usingCheck").activeSelf, Is.True);
            Assert.That(Field<GameObject>(Tile("wainscoting.sage-plain"), "usingCheck").activeSelf, Is.True);
            Select("wainscoting.none");
            Canvas.ForceUpdateCanvases();
            Field<ScrollRect>(Find<DecorationCatalogueView>(), "verticalScroll").verticalNormalizedPosition = 0;
            yield return Capture("10-wall-neutral-none-preview.png");
            Action("cancelButton").onClick.Invoke();
        }

        [UnityTest]
        public IEnumerator WallDecorAndWindows_ConfirmedAndPreviewMarkersAreIndependent()
        {
            yield return Load();
            var c = Find<DecorationModeController>(); c.EnterDecorationMode();
            yield return CaptureCatalogueBottom("25-coffee-machine-name-fit.png");
            Assert.That(c.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            yield return Capture("11-wall-decor-window-catalogue.png");
            yield return CaptureCatalogueBottom("24-window-name-fit.png");
            Assert.That(c.TryBeginWallMountedPreview("wall-decor.monitor.01", "wall.back-left", new WallSlotPosition(1, 0)), Is.True);
            yield return Capture("12-wall-decor-preview.png");
            Assert.That(Field<GameObject>(Tile("wall-decor.monitor.01"), "previewOutline").activeSelf, Is.True);
            Action("confirmButton").onClick.Invoke();
            Assert.That(c.ActiveWallMountedPreview, Is.Null);
            Assert.That(Field<GameObject>(Tile("wall-decor.monitor.01"), "usingCheck").activeSelf, Is.True);
            Assert.That(Field<GameObject>(Tile("wall-decor.monitor.01"), "previewOutline").activeSelf, Is.False);
            Assert.That(c.TryBeginWallMountedPreview("wall-decor.monitor.01", "wall.back-right", new WallSlotPosition(4, 0)), Is.True);
            Assert.That(Field<GameObject>(Tile("wall-decor.monitor.01"), "usingCheck").activeSelf, Is.True);
            Assert.That(Field<GameObject>(Tile("wall-decor.monitor.01"), "previewOutline").activeSelf, Is.True);
            yield return Capture("13-wall-decor-applied-and-preview.png");
            c.CancelActivePhase7Preview();
            Assert.That(c.TryBeginWallMountedPreview("window.tall-glass.1x2.01", "wall.back-right", new WallSlotPosition(2, 0)), Is.True);
            yield return Capture("14-window-preview.png");
            Action("confirmButton").onClick.Invoke();
            Assert.That(Field<GameObject>(Tile("window.tall-glass.1x2.01"), "usingCheck").activeSelf, Is.True);
            var layout = Field<WallMountedLayout>(c, "phase7WallMountedLayout");
            foreach (var id in new[] { "wall-decor.monitor.01", "window.tall-glass.1x2.01" })
            {
                var instance = layout.CaptureSnapshot().Instances.Single(item => item.DefinitionId == id);
                Assert.That(c.TryHandleSceneTap(new DecorationTouchHit(DecorationTouchHitKind.WallMounted, targetId: instance.InstanceId)), Is.True);
                Action("storeButton").onClick.Invoke();
                var modal = Find<DecorationStoreModalView>();
                Assert.That(Field<TMP_Text>(modal, "titleLabel").text, Does.Contain(id.StartsWith("window", StringComparison.Ordinal) ? "Tall Glass Window" : "Wall Monitor"));
                yield return Capture(id.StartsWith("window", StringComparison.Ordinal) ? "23-window-put-away.png" : "22-wall-decor-put-away.png");
                Field<Button>(modal, "cancelButton").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(c.ActiveWallMountedPreview, Is.Not.Null);
                c.CancelActivePhase7Preview();
            }
        }

        [UnityTest]
        public IEnumerator Readiness_RealConfirmAndInvalidPreviewKeepConfirmedReport_ThenInjectedPresentationMatrix()
        {
            yield return Load();
            var c = Find<DecorationModeController>(); c.EnterDecorationMode();
            var runtime = Find<CafeLayoutRuntime>();
            Select("equipment.cash-register.01");
            Assert.That(c.TryConfirmFunctionalSurfacePreview(), Is.True);
            var view = Find<ValidationMessageView>();
            Assert.That(view.IsVisible, Is.True);
            var confirmed = runtime.CurrentReadiness;
            var confirmedText = view.CurrentMessage;
            yield return Capture("15-readiness-real-confirmed-blocked.png");
            Select("equipment.coffee-machine.01");
            Assert.That(c.TryMoveFunctionalSurfacePreview(new SurfaceSlotAddress("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "missing.slot")), Is.False);
            Assert.That(runtime.CurrentReadiness, Is.SameAs(confirmed));
            Assert.That(view.CurrentMessage, Is.EqualTo(confirmedText));
            yield return Capture("16-invalid-preview-retains-confirmed-readiness.png");
            c.CancelFunctionalSurfacePreview();
            c.ExitDecorationMode();
            view.ShowReadiness(Report(true));
            Assert.That(view.IsVisible, Is.False, "A healthy report releases HUD space instead of displaying a success icon.");
            Assert.That(Field<Image>(view, "statusIcon").enabled, Is.False);
            Assert.That(view.GetComponent<CanvasGroup>().blocksRaycasts, Is.False);
            yield return Capture("17-readiness-INJECTED-ready.png");
            view.ShowReadiness(Report(true, Failure(LayoutReadinessSeverity.Warning, LayoutReadinessFailureCode.AnchorBlocked, 0)));
            AssertReadinessIcon(view);
            yield return Capture("18-readiness-INJECTED-warning.png");
            var failures = Enumerable.Range(0, 24).Select(i => Failure(i % 2 == 0 ? LayoutReadinessSeverity.Blocking : LayoutReadinessSeverity.Warning,
                (LayoutReadinessFailureCode)(i % 12), i)).ToArray();
            view.ShowReadiness(Report(false, failures));
            AssertReadinessIcon(view);
            yield return Capture("19-readiness-INJECTED-blocked-collapsed.png");
            Field<Button>(view, "disclosureButton").onClick.Invoke(); yield return null;
            Assert.That(view.IsDetailsExpanded, Is.True);
            Assert.That(view.GetComponent<ScrollRect>().vertical, Is.True);
            yield return Capture("20-readiness-INJECTED-expanded-scroll.png");
            view.GetComponent<ScrollRect>().verticalNormalizedPosition = 0;
            yield return Capture("21-readiness-INJECTED-scroll-bottom.png");
            Field<Button>(view, "disclosureButton").onClick.Invoke();
            Assert.That(view.IsDetailsExpanded, Is.False);
        }

        private static T Construct<T>(params object[] args) => (T)Activator.CreateInstance(typeof(T), BindingFlags.Instance | BindingFlags.NonPublic, null, args, null);
        private static void AssertReadinessIcon(ValidationMessageView view)
        {
            Canvas.ForceUpdateCanvases();
            var icon = Field<Image>(view, "statusIcon"); var ink = MeasuredInk(icon);
            var scale = icon.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
            Assert.That(Mathf.Max(ink.width, ink.height) / scale, Is.GreaterThanOrEqualTo(35f));
            Assert.That(ink.xMax + 8f * scale, Is.LessThanOrEqualTo(ScreenRect(view.GetComponent<ScrollRect>().viewport).xMin));
        }
        internal static LayoutReadinessReport Report(bool canOpen, params LayoutReadinessFailure[] failures)
        {
            var summary = Construct<LayoutReadinessSummary>(0, 0);
            return Construct<LayoutReadinessReport>(canOpen, Array.Empty<StationReadiness>(), failures, summary, summary, summary);
        }
        internal static LayoutReadinessFailure Failure(LayoutReadinessSeverity severity, LayoutReadinessFailureCode code, int index) =>
            Construct<LayoutReadinessFailure>(severity, code, (LayoutStationType?)LayoutStationType.CoffeeMachine, "private-" + index,
                "support", "slot", (InteractionRole?)InteractionRole.Employee, (GridPosition?)new GridPosition(index, 2), "Detached diagnostics");

        private static IEnumerator CaptureCatalogueBottom(string filename)
        {
            if (Environment.GetEnvironmentVariable("ANIMALCAFE_P8R_CAPTURE") != "1")
                yield break;

            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();
            var scroll = Field<ScrollRect>(Find<DecorationCatalogueView>(), "verticalScroll");
            var previous = scroll.verticalNormalizedPosition;
            try
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 0;
                Canvas.ForceUpdateCanvases();
                yield return Capture(filename);
            }
            finally
            {
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = previous;
                Canvas.ForceUpdateCanvases();
            }
        }

        internal static IEnumerator Capture(string filename, string folder = "outputs/p8r-ui-proportion-20260911/all-ui")
        {
            var captureSwitch = folder.StartsWith("outputs/p8r-reference-layout-20260911/", StringComparison.Ordinal)
                ? "ANIMALCAFE_P8R_REFERENCE_CAPTURE" : "ANIMALCAFE_P8R_CAPTURE";
            if (Environment.GetEnvironmentVariable(captureSwitch) != "1") yield break;
            var trial = Environment.GetEnvironmentVariable("ANIMALCAFE_P8R_CAPTURE_TRIAL") == "1";
            if (trial && filename != "01-hud-1x.png") yield break;
            yield return new WaitForSecondsRealtime(.3f);
            var camera = Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsSortMode.None).Single(c => c.CompareTag("MainCamera"));
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas)
                .Select(c => new { Canvas = c, c.renderMode, c.worldCamera, c.planeDistance }).ToArray();
            var width = camera.pixelWidth; var height = camera.pixelHeight;
            Assert.That(width, Is.EqualTo(Screen.width)); Assert.That(height, Is.EqualTo(Screen.height));
            var target = new RenderTexture(width, height, 24); target.Create();
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            var previous = camera.targetTexture; var active = RenderTexture.active;
            var data = camera.GetUniversalAdditionalCameraData(); var post = data.renderPostProcessing;
            try
            {
                data.renderPostProcessing = false;
                camera.targetTexture = target;
                foreach (var state in canvases)
                { state.Canvas.renderMode = RenderMode.ScreenSpaceCamera; state.Canvas.worldCamera = camera; state.Canvas.planeDistance = .5f; }
                Canvas.ForceUpdateCanvases(); yield return new WaitForSecondsRealtime(.2f);
                if (filename.StartsWith("07-", StringComparison.Ordinal))
                    foreach (var field in new[] { "undoLastButton", "rotateButton", "applyAllButton" })
                    {
                        Assert.That(Action(field).interactable, Is.False, "Captured Whole Room: " + field);
                        Assert.That(Action(field).image.overrideSprite.name, Is.EqualTo("button_disabled"), "Captured rendered state: " + field);
                        Assert.That(Action(field).image.color, Is.EqualTo(Color.white), field);
                        Assert.That(Action(field).image.canvasRenderer.GetColor(), Is.EqualTo(Color.white), field);
                    }
                if (filename.StartsWith("24-", StringComparison.Ordinal)
                    || filename.StartsWith("25-", StringComparison.Ordinal))
                {
                    var id = filename.StartsWith("24-", StringComparison.Ordinal)
                        ? "window.tall-glass.1x2.01" : "equipment.coffee-machine.01";
                    var label = Field<TMP_Text>(Tile(id), "nameLabel");
                    var bounds = ScreenRect(label.rectTransform);
                    var viewport = ScreenRect(
                        Field<ScrollRect>(Find<DecorationCatalogueView>(), "verticalScroll").viewport);
                    Assert.That(label.GetPreferredValues(label.text,
                        label.rectTransform.rect.width, Mathf.Infinity).y,
                        Is.LessThanOrEqualTo(label.rectTransform.rect.height + .1f));
                    Assert.That(viewport.Contains(bounds.min) && viewport.Contains(bounds.max),
                        Is.True, id + " complete name must be visible in the captured viewport.");
                }
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                if (filename.StartsWith("07-", StringComparison.Ordinal))
                {
                    Color Fill(string field)
                    {
                        var rect = ScreenRect((RectTransform)Action(field).transform);
                        return pixels.GetPixel(Mathf.RoundToInt(rect.xMin + rect.width * .3f), Mathf.RoundToInt(rect.yMin + rect.height * .8f));
                    }
                    var undo = Fill("undoLastButton");
                    foreach (var field in new[] { "rotateButton", "applyAllButton" })
                        Assert.That(Vector3.Distance(new Vector3(undo.r, undo.g, undo.b), new Vector3(Fill(field).r, Fill(field).g, Fill(field).b)),
                            Is.LessThan(.12f), "Same disabled PNG must reach the rendered texture: " + field);
                }
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, trial ? "00-AB-ui-overlay-camera.png" : filename), pixels.EncodeToPNG());
                File.WriteAllText(Path.Combine(folder, "capture-metrics.txt"),
                    "Actual MainCafe camera/GameView " + width + "x" + height
                    + ". Temporary ScreenSpaceCamera with camera postprocessing off, restored afterward. Layout/state-only: world colors differ from production. The separate 00-AB camera-stack trial was rejected because UI was absent. Not native Overlay/device evidence. INJECTED filenames use test reports; other filenames follow real scene flows.");
            }
            finally
            {
                data.renderPostProcessing = post; camera.targetTexture = previous; RenderTexture.active = active;
                foreach (var state in canvases)
                { state.Canvas.renderMode = state.renderMode; state.Canvas.worldCamera = state.worldCamera; state.Canvas.planeDistance = state.planeDistance; }
                target.Release(); Object.Destroy(target); Object.Destroy(pixels); Canvas.ForceUpdateCanvases();
            }
        }
    }
}
#endif

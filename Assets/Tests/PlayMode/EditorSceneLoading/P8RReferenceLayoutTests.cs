#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.Core.Time;
using AnimalCafe.Decoration;
using AnimalCafe.UI;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    public sealed class P8RReferenceLayoutTests
    {
        private Scene ownedScene;
        private float timeScaleBefore;
        private Vector2Int screenBefore;
        private Rect safeAreaBefore;

        [SetUp]
        public void RecordRuntimeBoundary()
        {
            ownedScene = default;
            timeScaleBefore = Time.timeScale;
            screenBefore = new Vector2Int(Screen.width, Screen.height);
            safeAreaBefore = Screen.safeArea;
        }

        [UnityTearDown]
        public IEnumerator ReleaseOwnedSceneBeforeAnyInputSystemReset()
        {
            Assert.That(ownedScene.IsValid() && ownedScene.isLoaded, Is.True, "The actual loaded Scene must remain owned until fixture cleanup.");
            if (ownedScene.IsValid() && ownedScene.isLoaded)
            {
                var inputAssets = Phase8SceneInputTestCleanup.CaptureAssets(ownedScene);
                Assert.That(inputAssets, Is.Not.Empty, "The owned MainCafe input state must be released before another fixture resets InputSystem.");
                foreach (var controller in ownedScene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DecorationModeController>(true)))
                    controller.enabled = false;
                var cleanup = SceneManager.CreateScene("P8RReferenceCleanup");
                SceneManager.SetActiveScene(cleanup);
                var unload = SceneManager.UnloadSceneAsync(ownedScene);
                while (unload != null && !unload.isDone) yield return null;
                Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets);
            }
            Time.timeScale = timeScaleBefore;
            yield return null; // Let native size restore complete before the next fixture captures Screen.
            Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(screenBefore));
            Assert.That(Screen.safeArea, Is.EqualTo(safeAreaBefore));
        }

        private static T Find<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private IEnumerator Load()
        {
            // Use the returned Scene: during replacement GetSceneByPath can still find the old same-path Scene.
            ownedScene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null; yield return null;
            Assert.That(ownedScene, Is.EqualTo(SceneManager.GetSceneByPath("Assets/Scenes/MainCafe.unity")));
            Assert.That(ownedScene.isLoaded, Is.True);
            Canvas.ForceUpdateCanvases();
        }
        private static Rect Box(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var canvas = rect.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var points = corners.Select(p => RectTransformUtility.WorldToScreenPoint(camera, p)).ToArray();
            return Rect.MinMaxRect(points.Min(p => p.x), points.Min(p => p.y), points.Max(p => p.x), points.Max(p => p.y));
        }
        private static Rect Box(Component component) => Box((RectTransform)component.transform);

        [UnityTest]
        public IEnumerator ColoredTabs_MainCafeEntryAndReenableReplaceStaleSerializedIcons()
        {
            yield return Load();
            Find<DecorationModeController>().EnterDecorationMode();
            var tabs = Find<DecorationModeTabsView>();
            Assert.That(tabs.isActiveAndEnabled, Is.True);
            var actions = new[] { "furniture", "floor", "wall", "wall_decor" };
            var fields = new[] { "furnitureButton", "floorButton", "wallButton", "wallDecorButton" };
            var icons = fields.Select(field => Field<Button>(tabs, field).transform.Find("Icon").GetComponent<Image>()).ToArray();
            AssertColoredIcons(); // Entry must work without a test-driven SetActive or mode change.

            // Seed the old scene references and tint on these runtime instances only.
            // 只污染当前场景实例；重新启用必须自动复色，不调用 SetActive 掩盖生命周期遗漏。
            for (var i = 0; i < icons.Length; i++)
            {
                var legacy = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/P8R/RefinedB/Icons/" + actions[i] + "_cocoa.png");
                Assert.That(legacy, Is.Not.Null, actions[i] + " legacy fixture must remain available.");
                icons[i].sprite = legacy;
                icons[i].color = Color.gray;
                icons[i].canvasRenderer.SetColor(Color.gray);
            }
            tabs.enabled = false;
            tabs.enabled = true;
            AssertColoredIcons();
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertColoredIcons();

            var catalogue = Find<DecorationCatalogueView>();
            foreach (var state in new[] { DecorationSheetState.TabsOnly, DecorationSheetState.Expanded })
            {
                catalogue.SetSheetState(state, false);
                yield return new WaitForSecondsRealtime(.3f);
                Canvas.ForceUpdateCanvases();
                AssertColoredIcons();
            }

            void AssertColoredIcons()
            {
                for (var i = 0; i < icons.Length; i++)
                {
                    Assert.That(UnityEditor.AssetDatabase.GetAssetPath(icons[i].sprite),
                        Is.EqualTo("Assets/UI/P8R/TabIcons/Outlined/tab_" + actions[i] + "_color.png"), actions[i]);
                    Assert.That(icons[i].color, Is.EqualTo(Color.white), actions[i] + " Image tint");
                    Assert.That(icons[i].canvasRenderer.GetColor(), Is.EqualTo(Color.white), actions[i] + " renderer tint");
                    var button = Field<Button>(tabs, fields[i]);
                    Assert.That(button.transform.Find("Label").gameObject.activeSelf, Is.False, actions[i] + " label must stay hidden");
                    var ink = P8RCompleteFlowTests.MeasuredInk(icons[i]);
                    var face = Box(button.image);
                    var scale = icons[i].GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
                    Assert.That(Mathf.Max(ink.width, ink.height) / P8RMobileMetrics.For(button).PixelsPerLogicalUnit,
                        Is.InRange(19.8f, 20.2f), actions[i] + " approved compact visible art in platform logical units");
                    Assert.That(ink.center.x, Is.EqualTo(face.center.x).Within(.5f), actions[i]);
                    Assert.That(ink.center.y, Is.EqualTo(face.center.y).Within(.5f), actions[i]);
                    Assert.That(face.Contains(ink.min) && face.Contains(ink.max), Is.True, actions[i]);
                }
            }
        }

        [UnityTest]
        public IEnumerator PickupFloat_ProductionActionBarRemainsStableAcrossCycle()
        {
            yield return Load();
            var controller = Find<DecorationModeController>();
            controller.EnterDecorationMode();
            var runtime = Find<CafeLayoutRuntime>();
            var registry = Find<FurnitureSceneRegistry>();
            var support = AnimalCafe.Layout.FurnitureInstance.CreateNew("furniture.counter.module.01",
                new AnimalCafe.Layout.GridPosition(4, 3), AnimalCafe.Layout.FurnitureRotation.Degrees0);
            Assert.That(runtime.Layout.PlaceFurniture(support).Succeeded, Is.True);
            registry.Rebuild(runtime.Layout.FurnitureInstances);
            var address = new AnimalCafe.Layout.SurfaceSlotAddress(support.InstanceId, "slot.0");
            Assert.That(controller.TryBeginFunctionalSurfacePreview(DecorationCatalogueItemKind.PickUpPoint, null, address), Is.True);
            Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            var point = runtime.FunctionalSurfaceLayout.PickUpPoints.Single(item => item.Address.Equals(address));
            Assert.That(controller.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.PickUpPoint, point.InstanceId), Is.True);
            var indicators = Find<PickUpPointIndicatorView>();
            var oldPreview = indicators.CurrentPreview;
            var oldAnchor = oldPreview.transform.position;
            var oldFootprint = oldPreview.transform.Find("Footprint").position;
            Assert.That(controller.TryMoveFunctionalSurfacePreview(address), Is.True);
            Assert.That(indicators.CurrentPreview, Is.Not.SameAs(oldPreview), "A real preview refresh must recreate the visual.");
            Assert.That(indicators.CurrentPreview.transform.position, Is.EqualTo(oldAnchor));
            Assert.That(indicators.CurrentPreview.transform.Find("Footprint").position, Is.EqualTo(oldFootprint));
            var hover = Field<float>(controller, "sanitizedFurnitureHoverHeight");
            var refreshedSign = indicators.CurrentPreview.transform.Find("InvertedSquarePyramid");
            Assert.That(refreshedSign.position.y - oldAnchor.y - hover, Is.InRange(.0839f, .1401f),
                "Recreating the pickup preview must retain its drag hover separately from the gentle float.");

            var action = Find<DecorationActionBarView>();
            var refresh = typeof(DecorationModeController).GetMethod("UpdateActionPresentation", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(refresh, Is.Not.Null);
            Assert.That(registry.TryGet(support.InstanceId, out var placedSupport), Is.True);
            var slot = placedSupport.GetComponentsInChildren<AnimalCafe.Content.SurfaceSlotMarker>(true)
                .Single(marker => marker.SlotId == "slot.0").transform;
            yield return ObserveCycle(indicators.CurrentPreview, slot, 3);

            Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            typeof(DecorationModeController).GetMethod("HandleFurnitureBegan", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { support.InstanceId });
            Assert.That(Field<DecorationSession>(controller, "session").ActivePreview.SourceInstanceId, Is.EqualTo(support.InstanceId));
            Assert.That(indicators.TryGet(point.InstanceId, out var attachedSign), Is.True);
            var supportPreview = Field<FurniturePreviewView>(controller, "previewView").CurrentPreviewTransform;
            var previewSlot = supportPreview.GetComponentsInChildren<AnimalCafe.Content.SurfaceSlotMarker>(true)
                .Single(marker => marker.SlotId == "slot.0").transform;
            yield return ObserveCycle(attachedSign, previewSlot, 4);

            IEnumerator ObserveCycle(GameObject indicator, Transform observedSlot, int expectedButtons)
            {
                yield return new WaitForSecondsRealtime(.35f); // Let the real sheet/action transitions settle.
                refresh.Invoke(controller, null);
                Canvas.ForceUpdateCanvases();
                var buttons = new[] { "storeButton", "cancelButton", "rotateButton", "confirmButton" }
                    .Select(field => Field<Button>(action, field)).Where(button => button.gameObject.activeInHierarchy).ToArray();
                Assert.That(buttons, Has.Length.EqualTo(expectedButtons));
                var centers = buttons.Select(button => Box(button).center).ToArray();
                var visual = indicator.transform.Find("InvertedSquarePyramid");
                var footprint = indicator.transform.Find("Footprint");
                var anchorPosition = indicator.transform.position;
                var footprintPosition = footprint.position;
                var footprintRotation = footprint.rotation;
                var slotPosition = observedSlot.position;
                var slotRotation = observedSlot.rotation;
                var vertices = visual.GetComponent<MeshFilter>().sharedMesh.vertices;
                var camera = UnityEngine.Camera.main;
                var minimumY = float.PositiveInfinity;
                var maximumY = float.NegativeInfinity;
                var until = Time.realtimeSinceStartup + 3.1f; // Longer than one complete 2.8-second cycle.
                while (Time.realtimeSinceStartup < until)
                {
                    yield return null;
                    // Force the real consumer to recalculate; an idle action bar would hide a bobbing-bounds bug.
                    // 每帧主动刷新真实动作栏，不能因 idle 没刷新而假通过。
                    refresh.Invoke(controller, null);
                    Canvas.ForceUpdateCanvases();
                    minimumY = Mathf.Min(minimumY, visual.position.y);
                    maximumY = Mathf.Max(maximumY, visual.position.y);
                    var corners = vertices.Select(vertex => camera.WorldToScreenPoint(visual.TransformPoint(vertex))).ToArray();
                    Assert.That(corners.All(corner => corner.z > 0), Is.True);
                    var signRect = Rect.MinMaxRect(corners.Min(corner => corner.x), corners.Min(corner => corner.y),
                        corners.Max(corner => corner.x), corners.Max(corner => corner.y));
                    for (var index = 0; index < buttons.Length; index++)
                    {
                        Assert.That(buttons[index].gameObject.activeInHierarchy, Is.True);
                        Assert.That(Vector2.Distance(Box(buttons[index]).center, centers[index]), Is.LessThan(.2f),
                            expectedButtons + "-button action bar must not follow the pickup sign's float.");
                        Assert.That(Box(buttons[index].image).Overlaps(signRect), Is.False,
                            buttons[index].name + " must leave the current rendered sign unobscured throughout its cycle.");
                    }
                    Assert.That(indicator.transform.position, Is.EqualTo(anchorPosition));
                    Assert.That(footprint.position, Is.EqualTo(footprintPosition));
                    Assert.That(footprint.rotation, Is.EqualTo(footprintRotation));
                    Assert.That(observedSlot.position, Is.EqualTo(slotPosition));
                    Assert.That(observedSlot.rotation, Is.EqualTo(slotRotation));
                }
                Assert.That(maximumY - minimumY, Is.GreaterThan(.04f), "The real sign must move while its action bar stays still.");
            }
        }

        [UnityTest]
        public IEnumerator SixFeedback_FloatingPreviewKeepsActionsWithoutExplanation()
        {
            yield return Load(); var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
            SelectSixFeedbackItem("furniture.counter.module.01");
            yield return new WaitForSecondsRealtime(.3f);
            var action = Find<DecorationActionBarView>();
            Assert.That(action.IsVisible, Is.True);
            Assert.That(Field<RectTransform>(action, "feedbackRoot").gameObject.activeInHierarchy, Is.False);
            Assert.That(Field<Button>(action, "cancelButton").gameObject.activeInHierarchy, Is.True);
            Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
            Assert.That(Field<RectTransform>(action, "feedbackRoot").gameObject.activeInHierarchy, Is.False);
        }

        [UnityTest]
        public IEnumerator SixFeedback_CategorySelectionNeverRestoresUnderline()
        {
            yield return Load(); var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
            foreach (var mode in new[] { DecorationModeKind.Floor, DecorationModeKind.Wall, DecorationModeKind.WallDecor, DecorationModeKind.Furniture })
            {
                Assert.That(controller.TryChangeMode(mode), Is.True); yield return null;
                foreach (var button in Find<DecorationModeTabsView>().GetComponentsInChildren<Button>(true))
                    Assert.That(button.transform.Find("SelectedUnderline")?.gameObject.activeSelf ?? false, Is.False, button.name);
            }
        }

        [UnityTest]
        public IEnumerator SixFeedback_ModeBadgeUsesButtonCornersAndCenteredInk()
        {
            yield return Load(); var panel = Find<TimeControlPanel>();
            foreach (var decorating in new[] { false, true, false })
            {
                panel.RefreshP8RModeBadge(decorating); Canvas.ForceUpdateCanvases();
                var label = Field<TMP_Text>(panel, "modeBadgeLabel"); label.ForceMeshUpdate();
                var face = label.GetComponentInParent<Image>();
                Assert.That(face.sprite.name, Is.EqualTo("button_secondary_normal"));
                var center = face.rectTransform.InverseTransformPoint(label.transform.TransformPoint(label.textBounds.center));
                Assert.That(Vector2.Distance(center, face.rectTransform.rect.center + Vector2.up), Is.LessThan(1f), label.text);
            }
        }

        [UnityTest]
        public IEnumerator SixFeedback_SurfacePreviewHasNoExplanationOrReservedGap()
        {
            yield return Load(); var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
            Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            var view = Find<DecorationCatalogueView>(); Canvas.ForceUpdateCanvases();
            var scroll = Field<ScrollRect>(view, "verticalScroll");
            var before = ((RectTransform)scroll.transform.parent).offsetMax.y;
            SelectSixFeedbackItem("floor.warm-wood");
            Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            yield return null; Canvas.ForceUpdateCanvases();
            var message = Field<TMP_Text>(view, "editingContextLabel");
            Assert.That(message == null || !message.gameObject.activeInHierarchy, Is.True);
            Assert.That(((RectTransform)scroll.transform.parent).offsetMax.y, Is.EqualTo(before).Within(.1f));
            Assert.That(Field<Button>(Find<DecorationActionBarView>(), "cancelButton").gameObject.activeInHierarchy, Is.True);
        }

        [UnityTest]
        public IEnumerator FloorTools_CompactRowsKeepSmallCopyAndDistinctTouchTargets()
        {
            var previous = P8RMobileMetrics.EditorLogicalViewportOverride;
            try
            {
                using (var screen = new NativeScreenSize())
                {
                    screen.Resize(new Vector2(1080, 1920));
                    P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360, 640);
                    yield return Load();
                    var controller = Find<DecorationModeController>();
                    controller.EnterDecorationMode();
                    Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                    SelectSixFeedbackItem("floor.warm-wood");
                    var catalogue = Find<DecorationCatalogueView>();
                    catalogue.ShowCatalogue();
                    yield return new WaitForSecondsRealtime(.3f);
                    var range = Find<DecorationFloorRangeView>();
                    var action = Find<DecorationActionBarView>();
                    var ranges = new[] { Field<Button>(range, "wholeRoomButton"), Field<Button>(range, "singleGridButton") };
                    var tools = new[] { "undoLastButton", "rotateButton", "applyAllButton", "cancelButton", "confirmButton" }
                        .Select(name => Field<Button>(action, name)).ToArray();
                    var density = P8RMobileMetrics.For(range).PixelsPerLogicalUnit;
                    foreach (var selected in new[] { SurfaceEditScope.WholeRoomFloor, SurfaceEditScope.SingleGridFloor,
                        SurfaceEditScope.WholeRoomFloor })
                    {
                        range.SetSelected(selected);
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        foreach (var button in ranges.Concat(tools))
                        {
                            var hit = Box(button);
                            Assert.That(hit.width / density, Is.GreaterThanOrEqualTo(47.9f), button.name);
                            Assert.That(hit.height / density, Is.GreaterThanOrEqualTo(47.9f), button.name);
                            Assert.That(Box(button.image).height / density, Is.EqualTo(32f).Within(.2f), button.name);
                            var label = button.transform.Find("Label")?.GetComponent<TMP_Text>();
                            if (label != null && label.gameObject.activeSelf)
                            {
                                Assert.That(label.fontSize / P8RMobileMetrics.For(label).UnitsPerLogicalUnit,
                                    Is.EqualTo(12f).Within(.1f), button.name);
                                label.ForceMeshUpdate();
                                Assert.That(label.isTextTruncated, Is.False, button.name);
                            }
                        }
                        for (var i = 1; i < tools.Length; i++)
                        {
                            Assert.That(Box(tools[i]).Overlaps(Box(tools[i - 1])), Is.False);
                            var gap = (Box(tools[i].image).xMin - Box(tools[i - 1].image).xMax) / density;
                            Assert.That(gap, Is.InRange(0f, 8.2f), tools[i].name + " visible gap");
                        }
                        Assert.That((Box(ranges[1]).xMax - Box(ranges[0]).xMin) / density,
                            Is.LessThan(270f), "Range copy and side padding should not spread across the whole sheet.");
                    }
                }
            }
            finally { P8RMobileMetrics.EditorLogicalViewportOverride = previous; }
        }

        [UnityTest]
        public IEnumerator SixFeedback_RangeInkSpacingSurvivesSelectionChanges()
        {
            yield return Load(); var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
            controller.TryChangeMode(DecorationModeKind.Floor); var view = Find<DecorationFloorRangeView>();
            foreach (var scope in new[] { SurfaceEditScope.WholeRoomFloor, SurfaceEditScope.SingleGridFloor, SurfaceEditScope.WholeRoomFloor })
            {
                view.SetSelected(scope); yield return null; Canvas.ForceUpdateCanvases();
                foreach (var button in new[] { Field<Button>(view, "wholeRoomButton"), Field<Button>(view, "singleGridButton") })
                {
                    var label = button.transform.Find("Label").GetComponent<TMP_Text>(); label.ForceMeshUpdate();
                    var left = RectTransformUtility.WorldToScreenPoint(null, label.transform.TransformPoint(label.textBounds.min)).x;
                    var icon = P8RCompleteFlowTests.MeasuredInk(button.transform.Find("Icon").GetComponent<Image>());
                    var gap = (left - icon.xMax) / P8RMobileMetrics.For(button).PixelsPerLogicalUnit;
                    Assert.That(gap, Is.InRange(3.99f, 4.01f), button.name + " " + scope + " compact gap (0.01 logical rounding tolerance)");
                }
            }
        }

        [UnityTest]
        public IEnumerator SixFeedback_PaintCardsUsePlainMaterialColorsOnly()
        {
            yield return Load(); var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
            controller.TryChangeMode(DecorationModeKind.Wall); yield return null; var view = Find<DecorationCatalogueView>();
            var ids = new[] { "paint.cream", "paint.sage", "paint.terracotta" };
            var colors = new[] { new Color32(242, 230, 184, 255), new Color32(168, 181, 140, 255), new Color32(199, 127, 93, 255) };
            for (var i = 0; i < ids.Length; i++)
            {
                var tile = view.GetComponentsInChildren<DecorationCatalogueTileView>(true).Single(t => t.ItemId == ids[i] && t.gameObject.activeInHierarchy);
                var image = Field<Image>(tile, "thumbnailImage");
                Assert.That(image.enabled, Is.True); Assert.That(image.sprite, Is.Null, ids[i]);
                Assert.That((Color32)image.color, Is.EqualTo(colors[i]));
            }
            controller.TryChangeMode(DecorationModeKind.Floor); yield return null;
            foreach (var tile in view.GetComponentsInChildren<DecorationCatalogueTileView>(true).Where(t => t.gameObject.activeInHierarchy && t.ItemId != null))
            {
                var image = Field<Image>(tile, "thumbnailImage");
                Assert.That(image.sprite, Is.Not.Null); Assert.That(image.color, Is.EqualTo(Color.white));
            }
        }

        private static void SelectSixFeedbackItem(string id) => Find<DecorationCatalogueView>()
            .GetComponentsInChildren<DecorationCatalogueTileView>(true)
            .First(t => t.ItemId == id && t.gameObject.activeInHierarchy).GetComponent<Button>().onClick.Invoke();

        [UnityTest]
        public IEnumerator HeldAction_FinalPointerUpFlushesLatestRequestWithoutControllerInput()
        {
            yield return Load(); var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
            Find<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .First(t => t.ItemId == "furniture.counter.module.01" && t.gameObject.activeInHierarchy).GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f);
            var action = Find<DecorationActionBarView>(); var rotate = Field<Button>(action, "rotateButton");
            var panel = Field<RectTransform>(action, "presentationRoot"); var before = Box(panel).center;
            var first = new PointerEventData(EventSystem.current) { pointerId = 701 };
            var second = new PointerEventData(EventSystem.current) { pointerId = 702 };
            ExecuteEvents.Execute(rotate.gameObject, first, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(rotate.gameObject, second, ExecuteEvents.pointerDownHandler);
            action.SetPresentation(DecorationActionPresentation.New, before + Vector2.right * 80, Screen.safeArea);
            action.SetPresentation(DecorationActionPresentation.New, before + Vector2.right * 160, Screen.safeArea);
            Assert.That(Vector2.Distance(Box(panel).center, before), Is.LessThan(.5f));
            ExecuteEvents.Execute(rotate.gameObject, first, ExecuteEvents.pointerUpHandler);
            yield return null;
            Assert.That(Vector2.Distance(Box(panel).center, before), Is.LessThan(.5f), "One remaining pointer keeps the compact group stable.");
            ExecuteEvents.Execute(rotate.gameObject, second, ExecuteEvents.pointerUpHandler);
            yield return null; yield return null;
            Assert.That(Box(panel).center.x, Is.GreaterThan(before.x + 120), "Last Up itself must apply the latest request after click processing, without controller input.");
            ExecuteEvents.Execute(rotate.gameObject, first, ExecuteEvents.pointerDownHandler);
            action.SetPresentation(DecorationActionPresentation.New, before, Screen.safeArea);
            action.Hide();
            ExecuteEvents.Execute(rotate.gameObject, first, ExecuteEvents.pointerUpHandler);
            yield return null; yield return null;
            Assert.That(action.IsVisible, Is.False, "Hide cancels pending reflow and never resurrects actions.");
            Assert.That(rotate.GetComponent<DecorationPointerBoundaryEventHook>().HasActivePress, Is.False);
        }

        [UnityTest]
        public IEnumerator CatalogueSettledBinding_ReconfigureAndDisableUseTrackedOwner()
        {
            yield return Load();
            var controller = Find<DecorationModeController>(); var a = Find<DecorationCatalogueView>();
            controller.EnterDecorationMode();
            var b = Object.Instantiate(a.gameObject, a.transform.parent).GetComponent<DecorationCatalogueView>();
            var changed = typeof(DecorationCatalogueView).GetField("PresentationSettled", BindingFlags.Instance | BindingFlags.NonPublic);
            int Count(DecorationCatalogueView view) => ((System.Delegate)changed.GetValue(view))?.GetInvocationList().Count(d => d.Target == controller) ?? 0;
            var categories = Field<System.Collections.Generic.IReadOnlyList<DecorationCategoryModel>>(controller, "phase7CatalogueCategories");
            try
            {
                Assert.That(Count(a), Is.EqualTo(1));
                controller.ConfigurePhase7Catalogue(b, categories);
                Assert.That(Count(a), Is.Zero); Assert.That(Count(b), Is.EqualTo(1));
                controller.ConfigurePhase7Catalogue(b, categories); Assert.That(Count(b), Is.EqualTo(1));
                controller.enabled = false; Assert.That(Count(a) + Count(b), Is.Zero);
                controller.enabled = true; Assert.That(Count(b), Is.EqualTo(1));
            }
            finally { controller.enabled = false; Object.Destroy(b.gameObject); }
        }

        [UnityTest]
        public IEnumerator ReadinessBinding_ActiveReconfigureAndDisableDetachExactlyOnce()
        {
            yield return Load();
            var controller = Find<DecorationModeController>(); var a = Find<ValidationMessageView>();
            var b = Object.Instantiate(a.gameObject, a.transform.parent).GetComponent<ValidationMessageView>();
            var eventField = typeof(ValidationMessageView).GetField("DetailsVisibilityChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            int Count(ValidationMessageView view) => ((System.Delegate)eventField.GetValue(view))?.GetInvocationList().Count(d => d.Target == controller) ?? 0;
            try
            {
                Assert.That(Count(a), Is.EqualTo(1));
                controller.ConfigurePhase8Scene(null, null, null, b);
                Assert.That(Count(a), Is.Zero, "Replacing the current view must release A.");
                Assert.That(Count(b), Is.EqualTo(1), "Active Configure must bind B.");
                controller.ConfigurePhase8Scene(null, null, null, b); Assert.That(Count(b), Is.EqualTo(1));
                controller.enabled = false; Assert.That(Count(a) + Count(b), Is.Zero);
                controller.enabled = true; Assert.That(Count(b), Is.EqualTo(1));
            }
            finally { controller.enabled = false; Object.Destroy(b.gameObject); }
        }

        [UnityTest]
        public IEnumerator ActualLandscape_PreviewNoticeLeavesModelVisible_AndModeInkHasPadding()
        {
            using (var screen = new NativeScreenSize())
            foreach (var size in new[] { new Vector2(1600, 720), new Vector2(640, 480) })
            {
                screen.Resize(size); yield return Load();
                var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
                Find<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>(true)
                    .First(t => t.ItemId == "furniture.counter.module.01" && t.gameObject.activeInHierarchy).GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
                var action = Find<DecorationActionBarView>(); var notice = Field<RectTransform>(action, "feedbackRoot");
                Assert.That(notice.gameObject.activeInHierarchy, Is.False, "Preview explanations no longer obscure either landscape size.");
                var modeLabel = Field<TMP_Text>(controller, "decorationModeButtonLabel");
                var button = modeLabel.GetComponentInParent<Button>();
                Assert.That(modeLabel.gameObject.activeSelf, Is.False);
                var inkBottom = P8RCompleteFlowTests.MeasuredInk(button.transform.Find("Icon").GetComponent<Image>()).yMin;
                var bottom = Box(button.image).yMin;
                Assert.That((inkBottom - bottom) / button.GetComponentInParent<Canvas>().rootCanvas.scaleFactor,
                    Is.GreaterThanOrEqualTo(10f), "The mode icon keeps padding inside its compact face.");
            }
        }

        [UnityTest]
        public IEnumerator Polish_NativeOverlayCaptureKeepsProductionRendering()
        {
            yield return Load();
            Assert.That(Find<TimeControlPanel>().GetComponentInParent<Canvas>().rootCanvas.renderMode,
                Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            if (System.Environment.GetEnvironmentVariable("ANIMALCAFE_P8R_POLISH_CAPTURE") != "1") yield break;
            var mobile = System.Environment.GetEnvironmentVariable("ANIMALCAFE_P8R_MOBILE_CAPTURE") == "1";
            var profileProperty = typeof(TimeControlPanel).Assembly.GetType("AnimalCafe.UI.P8R.P8RMobileMetrics")?
                .GetProperty("EditorLogicalViewportOverride", BindingFlags.Public | BindingFlags.Static);
            var previousProfile = profileProperty?.GetValue(null);
            if (mobile) Assert.That(profileProperty, Is.Not.Null, "Mobile capture needs an explicit logical viewport.");
            try
            {
            using (var screen = new RealGameViewSize())
            {
                foreach (var size in mobile
                    ? new[] { new Vector2(1080, 1920), new Vector2(480, 854), new Vector2(1600, 720), new Vector2(1536, 2048) }
                    : new[] { new Vector2(1080, 1920), new Vector2(720, 1280), new Vector2(480, 854), new Vector2(1600, 720) })
                {
                    if (mobile) profileProperty.SetValue(null, size.x == 1536
                        ? new Vector2(768, 1024) : size.x == 480
                        ? size / 1.5f : size * (360f / Mathf.Min(size.x, size.y)));
                    screen.Resize(size); yield return new WaitForSecondsRealtime(.3f); yield return Load();
                    Assert.That(new Vector2(Screen.width, Screen.height), Is.EqualTo(size), "Game View must render the requested resolution.");
                    Assert.That(new Vector2(UnityEngine.Camera.main.pixelWidth, UnityEngine.Camera.main.pixelHeight), Is.EqualTo(size));
                    Assert.That(Find<TimeControlPanel>().GetComponentInParent<Canvas>().rootCanvas.renderMode,
                        Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                    var tag = System.Environment.GetEnvironmentVariable("ANIMALCAFE_P8R_POLISH_RUN");
                    Assert.That(tag, Does.Match("^[a-z0-9-]+$"));
                    var folder = (mobile ? "outputs/p8r-mobile-ui-20260913/" : "outputs/p8r-ui-polish-20260912/")
                        + tag + "/" + (int)size.x + "x" + (int)size.y;
                    var controller = Find<DecorationModeController>(); var view = Find<DecorationCatalogueView>();
                    var time = Find<GameTimeService>();
                    void Select(string id) => view.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                        .First(t => t.ItemId == id && t.gameObject.activeInHierarchy).GetComponent<Button>().onClick.Invoke();
                    Button Action(string field) => Field<Button>(Find<DecorationActionBarView>(), field);
                    yield return CaptureNative("01-hud-1x.png", folder);
                    if (tag.StartsWith("compact-motion-") && size.x == 480)
                    {
                        yield return CaptureTimeSelectionMotion(folder);
                        time.SetNormal();
                        yield return new WaitForSecondsRealtime(.25f);
                    }
                    time.SetFast(); yield return CaptureNative("02-hud-2x.png", folder);
                    time.TogglePaused(); yield return CaptureNative("03-hud-paused.png", folder);
                    time.TogglePaused(); controller.EnterDecorationMode();
                    yield return CaptureNative("04-furniture.png", folder);
                    if (tag.StartsWith("proportion-"))
                    {
                        view.VerticalScroll.StopMovement();
                        Canvas.ForceUpdateCanvases();
                        var equipment = (RectTransform)view.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                            .Single(tile => tile.ItemId == "equipment.cash-register.01" && tile.gameObject.activeInHierarchy).transform;
                        var viewport = view.VerticalScroll.viewport;
                        var equipmentCenter = viewport.InverseTransformPoint(equipment.TransformPoint(equipment.rect.center));
                        view.VerticalScroll.content.anchoredPosition += new Vector2(0, viewport.rect.center.y - equipmentCenter.y);
                        yield return CaptureNative("28-equipment-cards.png", folder);
                        view.VerticalScroll.verticalNormalizedPosition = 1;
                        yield return new WaitForSecondsRealtime(.1f);
                    }
                    Select("furniture.counter.module.01");
                    yield return CaptureNative("12-furniture-preview.png", folder);
                    AssertCollapsedTabsAreCentered(view);
                    view.ShowCatalogue();
                    Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
                    yield return CaptureNative("13-return-without-explanation.png", folder);
                    Field<Button>(view, "returnToEditingButton").onClick.Invoke();
                    yield return new WaitForSecondsRealtime(.25f);
                    Action("cancelButton").onClick.Invoke();
                    Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                    var roomSurfaces = Field<AnimalCafe.Layout.RoomSurfaceLayout>(controller, "phase7RoomSurfaceLayout");
                    var confirmedSurfaces = JsonUtility.ToJson(roomSurfaces.CaptureSnapshot());
                    if (mobile)
                    {
                        var range = Find<DecorationFloorRangeView>();
                        Field<Button>(range, "singleGridButton").onClick.Invoke();
                        yield return CaptureNative("21-single-grid-instruction.png", folder);
                        Field<Button>(range, "wholeRoomButton").onClick.Invoke();
                    }
                    Select("floor.warm-wood");
                    Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                    yield return CaptureNative("05-floor.png", folder);
                    Assert.That(controller.ActiveSurfacePreview, Is.Not.Null);
                    Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
                    Assert.That(controller.ActiveSurfacePreview, Is.Null);
                    Assert.That(JsonUtility.ToJson(roomSurfaces.CaptureSnapshot()), Is.EqualTo(confirmedSurfaces),
                        "Cross-tab discard must preserve every confirmed floor and wall style.");
                    yield return CaptureNative("20-floor-cancelled-after-tab.png", folder);
                    Assert.That(controller.TryHandleSceneTap(new AnimalCafe.Decoration.Input.DecorationTouchHit(
                        AnimalCafe.Decoration.Input.DecorationTouchHitKind.WallSurface, surfaceId: "wall.back-left")), Is.True);
                    Select("paint.sage"); yield return CaptureNative("06-wall.png", folder);
                    Action("cancelButton").onClick.Invoke();
                    Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
                    Assert.That(controller.TryBeginWallMountedPreview("wall-decor.wood-shelf.01", "wall.back-left",
                        new AnimalCafe.Layout.WallSlotPosition(4, 0)), Is.True);
                    var shelfPreview = controller.ActiveWallMountedPreview;
                    view.ShowCatalogue();
                    yield return new WaitForSecondsRealtime(.25f); Canvas.ForceUpdateCanvases();
                    var shelfTile = view.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                        .Single(tile => tile.ItemId == "wall-decor.wood-shelf.01" && tile.gameObject.activeInHierarchy);
                    var shelfOutline = Field<GameObject>(shelfTile, "previewOutline");
                    Assert.That(shelfOutline.activeInHierarchy, Is.True);
                    var shelfDash = shelfOutline.GetComponentInChildren<AnimalCafe.UI.P8R.P8RRoundedDashGraphic>();
                    Assert.That(shelfDash != null && shelfDash.isActiveAndEnabled, Is.True,
                        "The expanded Wall Decor card must show its real dashed preview border.");
                    var shelfIsConfirmed = Field<AnimalCafe.Layout.WallMountedLayout>(controller, "phase7WallMountedLayout")
                        .CaptureSnapshot().Instances.Any(item => item.DefinitionId == "wall-decor.wood-shelf.01");
                    Assert.That(Field<GameObject>(shelfTile, "usingCheck").activeSelf, Is.EqualTo(shelfIsConfirmed),
                        "Wall Decor retains its confirmed-instance using-check rule.");
                    yield return CaptureNative("19-wall-decor-cards.png", folder);
                    if (tag.StartsWith("proportion-"))
                    {
                        view.VerticalScroll.StopMovement();
                        view.VerticalScroll.verticalNormalizedPosition = 0;
                        yield return CaptureNative("29-window-cards.png", folder);
                        view.VerticalScroll.verticalNormalizedPosition = 1;
                        yield return new WaitForSecondsRealtime(.1f);
                    }
                    Field<Button>(view, "returnToEditingButton").onClick.Invoke();
                    yield return new WaitForSecondsRealtime(.25f); Canvas.ForceUpdateCanvases();
                    Assert.That(view.SheetState, Is.EqualTo(DecorationSheetState.CompactPreview));
                    Assert.That(controller.ActiveWallMountedPreview, Is.SameAs(shelfPreview));
                    yield return CaptureNative("07-shelf.png", folder);
                    controller.TryRequestExit(); yield return CaptureNative("08-exit-modal.png", folder);
                    Field<Button>(Find<DecorationExitModalView>(), "continueButton").onClick.Invoke();
                    yield return new WaitForSecondsRealtime(.2f); Action("cancelButton").onClick.Invoke();
                    controller.TryRequestExit();
                    Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
                    yield return CaptureNative("09-hud-restored-2x.png", folder);
                    var appearance = Field<AnimalCafe.UI.P8R.P8RAppearance>(view, "appearance");
                    if (appearance.IsRefinedB)
                    {
                        controller.EnterDecorationMode();
                        var pickup = Field<Button>(view, "pickUpPointButton");
                        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                        ExecuteEvents.Execute(pickup.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                        ExecuteEvents.Execute(pickup.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                        Assert.That(pickup.image.overrideSprite, Is.SameAs(appearance.Sprite("button_primary_pressed")));
                        yield return CaptureNative("10-pickup-pressed.png", folder);
                        ExecuteEvents.Execute(pickup.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                        ExecuteEvents.Execute(pickup.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                        Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
                        Assert.That(controller.TryBeginWallMountedPreview("wall-decor.wood-shelf.01", "wall.back-left",
                            new AnimalCafe.Layout.WallSlotPosition(4, 0)), Is.True);
                        Action("confirmButton").onClick.Invoke();
                        var shelf = Field<AnimalCafe.Layout.WallMountedLayout>(controller, "phase7WallMountedLayout")
                            .CaptureSnapshot().Instances.Single(item => item.DefinitionId == "wall-decor.wood-shelf.01");
                        Assert.That(controller.TryHandleSceneTap(new AnimalCafe.Decoration.Input.DecorationTouchHit(
                            AnimalCafe.Decoration.Input.DecorationTouchHitKind.WallMounted, targetId: shelf.InstanceId)), Is.True);
                        Action("storeButton").onClick.Invoke();
                        yield return CaptureNative("11-put-away-modal.png", folder);
                        Field<Button>(Find<DecorationStoreModalView>(), "cancelButton").onClick.Invoke();
                        yield return new WaitForSecondsRealtime(.2f);
                        Assert.That(controller.ActiveWallMountedPreview, Is.Not.Null, "Cancel must return to the same shelf preview.");
                        Action("cancelButton").onClick.Invoke();
                        Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
                        var runtime = Find<CafeLayoutRuntime>();
                        var support = AnimalCafe.Layout.FurnitureInstance.CreateNew("furniture.counter.module.01",
                            new AnimalCafe.Layout.GridPosition(4, 3), AnimalCafe.Layout.FurnitureRotation.Degrees0);
                        Assert.That(runtime.Layout.PlaceFurniture(support).Succeeded, Is.True);
                        Find<FurnitureSceneRegistry>().Rebuild(runtime.Layout.FurnitureInstances);
                        view.ShowCatalogue(); yield return new WaitForSecondsRealtime(.25f);
                        pickup.onClick.Invoke();
                        var address = new AnimalCafe.Layout.SurfaceSlotAddress(support.InstanceId, "slot.0");
                        Assert.That(controller.TryMoveFunctionalSurfacePreview(address), Is.True);
                        yield return CaptureNative("14-pickup-valid-preview.png", folder);
                        var sign = Find<PickUpPointIndicatorView>().CurrentPreview.transform.Find("InvertedSquarePyramid");
                        Assert.That(Quaternion.Angle(sign.rotation, UnityEngine.Camera.main.transform.rotation), Is.LessThan(.01f));
                        var tip = UnityEngine.Camera.main.WorldToScreenPoint(sign.TransformPoint(Vector3.up * .18f));
                        var anchor = UnityEngine.Camera.main.WorldToScreenPoint(sign.position);
                        Assert.That(tip.x, Is.EqualTo(anchor.x).Within(.1f), "Sign tip stays directly above the slot anchor on screen.");
                        Action("confirmButton").onClick.Invoke();
                        Assert.That(controller.ActiveFunctionalSurfacePreview, Is.Null);
                        var point = runtime.FunctionalSurfaceLayout.PickUpPoints.Single(p => p.Address.Equals(address));
                        view.ShowCatalogue(); yield return new WaitForSecondsRealtime(.25f);
                        pickup.onClick.Invoke();
                        Assert.That(controller.TryMoveFunctionalSurfacePreview(address), Is.False, "Occupied slot must stay invalid.");
                        Assert.That(controller.ActiveFunctionalSurfacePreview.CanConfirm, Is.False);
                        Assert.That(Find<PickUpPointIndicatorView>().TryGet(point.InstanceId, out var coveredConfirmed), Is.True);
                        Assert.That(coveredConfirmed.activeInHierarchy, Is.False, "The occupied-slot preview must not stack two signs or mix two footprint colors.");
                        yield return CaptureNative("15-pickup-invalid-preview.png", folder);
                        var invalidSign = Find<PickUpPointIndicatorView>().CurrentPreview.transform.Find("InvertedSquarePyramid");
                        Assert.That(invalidSign.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(Color.white));
                        var signBlock = new MaterialPropertyBlock(); invalidSign.GetComponent<Renderer>().GetPropertyBlock(signBlock);
                        Assert.That(signBlock.isEmpty, Is.True, "Validity must not tint approved artwork.");
                        Action("cancelButton").onClick.Invoke();
                        yield return CaptureNative("16-pickup-confirmed.png", folder);
                        view.ShowCollapsedHandle(); yield return new WaitForSecondsRealtime(.25f);
                        Assert.That(Find<PickUpPointIndicatorView>().TryGet(point.InstanceId, out var confirmedSign), Is.True);
                        Assert.That(confirmedSign.activeInHierarchy, Is.True, "Cancel restores the original pickup sign.");
                        var visibleSign = confirmedSign.transform.Find("InvertedSquarePyramid").GetComponent<Renderer>();
                        var signScreen = (Vector2)UnityEngine.Camera.main.WorldToScreenPoint(visibleSign.bounds.center);
                        var classifier = (AnimalCafe.Decoration.Input.IDecorationTouchHitClassifier)controller;
                        var signHit = classifier.ClassifyBegan(901, signScreen);
                        Assert.That(signHit.Kind, Is.EqualTo(AnimalCafe.Decoration.Input.DecorationTouchHitKind.FunctionalSurface));
                        Assert.That(signHit.TargetId, Is.EqualTo(point.InstanceId), "The visible cup must select this pickup point.");
                        var nearbyHit = classifier.ClassifyBegan(902, signScreen + Vector2.right * 100f);
                        Assert.That(nearbyHit.TargetId, Is.Not.EqualTo(point.InstanceId), "The sign must not steal nearby empty-space input.");
                        typeof(DecorationModeController).GetMethod("HandleFunctionalSurfaceBegan", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(controller, new object[] { signHit.TargetId });
                        Assert.That(controller.ActiveFunctionalSurfacePreview, Is.Not.Null);
                        yield return CaptureNative("17-pickup-selected.png", folder);
                        Action("cancelButton").onClick.Invoke();
                        typeof(DecorationModeController).GetMethod("HandleFurnitureBegan", BindingFlags.Instance | BindingFlags.NonPublic)
                            .Invoke(controller, new object[] { support.InstanceId });
                        Assert.That(Field<DecorationSession>(controller, "session").ActivePreview.SourceInstanceId, Is.EqualTo(support.InstanceId));
                        yield return CaptureNative("18-furniture-actions.png", folder);
                        AssertCollapsedTabsAreCentered(view);
                        Assert.That(Find<PickUpPointIndicatorView>().TryGet(point.InstanceId, out var movingSign), Is.True);
                        var movingVisual = movingSign.transform.Find("InvertedSquarePyramid");
                        var signCorners = movingVisual.GetComponent<MeshFilter>().sharedMesh.vertices
                            .Select(v => UnityEngine.Camera.main.WorldToScreenPoint(movingVisual.TransformPoint(v))).ToArray();
                        var signRect = Rect.MinMaxRect(signCorners.Min(p => p.x), signCorners.Min(p => p.y),
                            signCorners.Max(p => p.x), signCorners.Max(p => p.y));
                        foreach (var field in new[] { "storeButton", "cancelButton", "rotateButton", "confirmButton" })
                        {
                            Assert.That(Box(Action(field).image).Overlaps(signRect), Is.False,
                                field + ": moving the support must keep its pickup sign unobscured.");
                            Assert.That(Box(Action(field)).Overlaps(Box(Find<ValidationMessageView>())), Is.False,
                                field + ": moving support actions must not cover confirmed readiness.");
                            foreach (var hudButton in Find<TimeControlPanel>().GetComponentsInChildren<Button>())
                                Assert.That(Box(Action(field)).Overlaps(Box(hudButton)), Is.False, field + " overlaps HUD");
                        }
                    }
                    if (mobile)
                    {
                        // Explicit simulated inset, not physical notched-device evidence.
                        // 仅向本次测试 Scene 注入安全边距，不更改 Screen 或项目设置。
                        Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                        Field<Button>(Find<DecorationFloorRangeView>(), "singleGridButton").onClick.Invoke();
                        var density = P8RMobileMetrics.For(view).PixelsPerLogicalUnit;
                        var landscape = size.x > size.y;
                        var inset = new Rect((landscape ? 44f : 24f) * density, 20f * density,
                            size.x - (landscape ? 56f : 32f) * density, size.y - (landscape ? 32f : 64f) * density);
                        foreach (var safe in Object.FindObjectsByType<AnimalCafe.UI.Components.SafeAreaContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        {
                            safe.AutoApplyRuntimeSafeArea = false;
                            safe.ApplySafeArea(inset, size);
                        }
                        yield return CaptureNative("22-INJECTED-safe-area.png", folder);
                        var readinessRect = Box(Find<ValidationMessageView>());
                        foreach (var button in Find<TimeControlPanel>().GetComponentsInChildren<Button>())
                            Assert.That(readinessRect.Overlaps(Box(button)), Is.False, "Inset readiness must stay below the moved HUD.");
                        var instructionRect = Box(Find<DecorationActionBarView>().VisibleInstructionRect);
                        Assert.That(instructionRect.Overlaps(readinessRect), Is.False);
                        foreach (var button in view.GetComponentInChildren<DecorationModeTabsView>().GetComponentsInChildren<Button>())
                            Assert.That(instructionRect.Overlaps(Box(button)), Is.False, "Inset tabs cannot cover the necessary instruction.");
                        var visibleViewport = Box(view.VerticalScroll.viewport);
                        var firstFloorCard = view.GetComponentsInChildren<DecorationCatalogueTileView>()
                            .Where(tile => !string.IsNullOrEmpty(tile.ItemId)).OrderBy(tile => Box(tile).xMin).First();
                        var firstFloorBounds = Box(firstFloorCard);
                        Assert.That((Mathf.Min(firstFloorBounds.xMax, visibleViewport.xMax)
                            - Mathf.Max(firstFloorBounds.xMin, visibleViewport.xMin)) / density, Is.GreaterThanOrEqualTo(47.99f));
                        Assert.That((Mathf.Min(firstFloorBounds.yMax, visibleViewport.yMax)
                            - Mathf.Max(firstFloorBounds.yMin, visibleViewport.yMin)) / density, Is.GreaterThanOrEqualTo(47.99f),
                            "After safe-area clipping a real card must retain a usable target, not only its full offscreen RectTransform.");
                        var exit = Find<DecorationExitModalView>(); exit.Show();
                        yield return CaptureNative("23-INJECTED-safe-area-exit.png", folder);
                        exit.Close(); yield return new WaitForSecondsRealtime(.3f);
                        var store = Find<DecorationStoreModalView>();
                        var furniture = UnityEditor.AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>("Assets/UI/P8R/DC_P8RFurniture.asset");
                        store.Show(furniture.Entries[0].Definition);
                        yield return CaptureNative("24-INJECTED-safe-area-store.png", folder);
                        store.CloseForOwnerShutdown(); yield return new WaitForSecondsRealtime(.3f);
                        store.ShowFunctionalSurface(DecorationCatalogueItemKind.PickUpPoint);
                        yield return CaptureNative("25-INJECTED-safe-area-pickup-copy.png", folder);
                        // Change safe bounds while already open, not just before Show().
                        // 弹窗已经打开后再缩窄安全区，验证运行中的尺寸通知。
                        var narrowerInset = new Rect(inset.x + 20f * density, inset.y,
                            inset.width - 20f * density, inset.height);
                        foreach (var safe in Object.FindObjectsByType<AnimalCafe.UI.Components.SafeAreaContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                            safe.ApplySafeArea(narrowerInset, size);
                        yield return CaptureNative("26-INJECTED-open-store-resize.png", folder);
                        var storeRect = Box(store.ContentRect);
                        Assert.That(storeRect.xMin, Is.GreaterThanOrEqualTo(narrowerInset.xMin - 1f));
                        Assert.That(storeRect.xMax, Is.LessThanOrEqualTo(narrowerInset.xMax + 1f));
                        store.CloseForOwnerShutdown();
                    }
                }
                if (mobile)
                {
                    // Exact constrained-height witness from compact boundary regression.
                    // 额外截取最短横屏的详情与Floor Preview，沿用真实Overlay而非Camera替代图。
                    var boundaryPixels = new Vector2(1138, 640);
                    P8RMobileMetrics.EditorLogicalViewportOverride = boundaryPixels * .5f;
                    screen.Resize(boundaryPixels); yield return new WaitForSecondsRealtime(.3f); yield return Load();
                    var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
                    Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                    Field<Button>(Find<DecorationFloorRangeView>(), "wholeRoomButton").onClick.Invoke();
                    SelectSixFeedbackItem("floor.warm-wood");
                    var catalogue = Find<DecorationCatalogueView>(); catalogue.ShowCatalogue();
                    foreach (var safe in Object.FindObjectsByType<AnimalCafe.UI.Components.SafeAreaContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        safe.AutoApplyRuntimeSafeArea = false;
                        safe.ApplySafeArea(new Rect(88, 40, 1026, 576), boundaryPixels);
                    }
                    Field<Button>(Find<ValidationMessageView>(), "disclosureButton").onClick.Invoke();
                    var boundaryFolder = "outputs/p8r-mobile-ui-20260913/"
                        + System.Environment.GetEnvironmentVariable("ANIMALCAFE_P8R_POLISH_RUN") + "/1138x640";
                    yield return CaptureNative("27-INJECTED-expanded-readiness-floor-preview.png", boundaryFolder);
                    Assert.That(Find<ValidationMessageView>().IsDetailsExpanded, Is.True);
                    AssertCatalogueAvoidsChrome(catalogue);
                }
            }
            }
            finally { if (mobile) profileProperty.SetValue(null, previousProfile); }
        }

        [UnityTest]
        public IEnumerator MobileSmallPhone_FloorPreviewKeepsUsableCardScrollAreaWithoutShrinkingTargets()
        {
            var previous = P8RMobileMetrics.EditorLogicalViewportOverride;
            try
            {
                using (var screen = new NativeScreenSize())
                foreach (var pixels in new[] { new Vector2(480, 854), new Vector2(1080, 1920) })
                {
                    var density = pixels.x == 480 ? 1.5f : 3f;
                    P8RMobileMetrics.EditorLogicalViewportOverride = pixels / density;
                    screen.Resize(pixels); yield return Load();
                    var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
                    Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                    SelectSixFeedbackItem("floor.warm-wood");
                    var catalogue = Find<DecorationCatalogueView>(); catalogue.ShowCatalogue();
                    yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
                    var viewport = Box(catalogue.VerticalScroll.viewport);
                    Assert.That(viewport.height / density, Is.GreaterThanOrEqualTo(47.9f),
                        "The approved shorter sheet can scroll, but must retain usable content instead of a non-tappable strip.");
                    var first = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>()
                        .Where(tile => !string.IsNullOrEmpty(tile.ItemId)).OrderBy(tile => Box(tile).xMin).First();
                    var firstBounds = Box(first);
                    Assert.That((Mathf.Min(firstBounds.xMax, viewport.xMax) - Mathf.Max(firstBounds.xMin, viewport.xMin)) / density,
                        Is.GreaterThanOrEqualTo(47.9f));
                    Assert.That((Mathf.Min(firstBounds.yMax, viewport.yMax) - Mathf.Max(firstBounds.yMin, viewport.yMin)) / density,
                        Is.GreaterThanOrEqualTo(47.9f), "Measure the clipped tile itself, excluding the section heading.");
                    var fixedButtons = Find<DecorationFloorRangeView>().GetComponentsInChildren<Button>()
                        .Concat(Find<DecorationActionBarView>().GetComponentsInChildren<Button>())
                        .Concat(Find<DecorationModeTabsView>().GetComponentsInChildren<Button>())
                        .Append(Field<Button>(catalogue, "collapseButton")).Where(button => button.gameObject.activeInHierarchy).ToArray();
                    foreach (var button in fixedButtons)
                    {
                        Assert.That(Box(button).width / density, Is.GreaterThanOrEqualTo(47.9f), button.name);
                        Assert.That(Box(button).height / density, Is.GreaterThanOrEqualTo(47.9f), button.name);
                    }
                    for (var i = 0; i < fixedButtons.Length; i++) for (var j = 0; j < i; j++)
                        Assert.That(Box(fixedButtons[i]).Overlaps(Box(fixedButtons[j])), Is.False,
                            fixedButtons[i].name + " overlaps " + fixedButtons[j].name);
                }
            }
            finally { P8RMobileMetrics.EditorLogicalViewportOverride = previous; }
        }

        [UnityTest]
        public IEnumerator MobileLandscape_ReturnToEditingSharesHeaderWithoutTakingCardSpace()
        {
            var previous = P8RMobileMetrics.EditorLogicalViewportOverride;
            try
            {
                using (var screen = new NativeScreenSize())
                {
                    P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(800, 360);
                    screen.Resize(new Vector2(1600, 720)); yield return Load();
                    var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
                    var catalogue = Find<DecorationCatalogueView>();
                    yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
                    var browsingHeight = Box(catalogue.VerticalScroll.viewport).height;
                    SelectSixFeedbackItem("furniture.counter.module.01"); catalogue.ShowCatalogue();
                    yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
                    var back = Field<Button>(catalogue, "returnToEditingButton");
                    var panel = Box(Field<GameObject>(catalogue, "expandedRoot").transform);
                    var backRect = Box(back);
                    Assert.That(backRect.xMin, Is.GreaterThanOrEqualTo(panel.xMin));
                    Assert.That(backRect.xMax, Is.LessThanOrEqualTo(panel.xMax));
                    Assert.That(backRect.yMin, Is.GreaterThanOrEqualTo(panel.yMin));
                    Assert.That(backRect.yMax, Is.LessThanOrEqualTo(panel.yMax + 1f));
                    Assert.That(backRect.width / 2f, Is.GreaterThanOrEqualTo(48f));
                    var hits = new System.Collections.Generic.List<RaycastResult>();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = backRect.center }, hits);
                    Assert.That(hits, Is.Not.Empty);
                    Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(back));
                    Assert.That(Box(catalogue.VerticalScroll.viewport).height, Is.GreaterThanOrEqualTo(browsingHeight - 1f),
                        "The existing return action should share the wide header, not reduce cards to a thin strip.");
                    var tabs = catalogue.GetComponentInChildren<DecorationModeTabsView>().GetComponentsInChildren<Button>();
                    Assert.That(Box(back).height / 2f, Is.GreaterThanOrEqualTo(47.99f));
                    foreach (var tab in tabs)
                    {
                        Assert.That(Box(back).Overlaps(Box(tab)), Is.False);
                        Assert.That(Box(back).center.y, Is.EqualTo(Box(tab).center.y).Within(1f));
                    }
                    var state = controller.State;
                    back.onClick.Invoke(); yield return new WaitForSecondsRealtime(.3f);
                    Assert.That(controller.State, Is.EqualTo(state), "Return only changes presentation, not the preview transaction.");
                    Assert.That(catalogue.IsCollapsed, Is.True);
                    Canvas.ForceUpdateCanvases();
                    Assert.That((tabs.Min(tab => Box(tab).xMin) + tabs.Max(tab => Box(tab).xMax)) * .5f,
                        Is.EqualTo(Box(catalogue).center.x).Within(1f), "Collapsed tabs must not reserve space for the hidden wide header.");
                    foreach (var wide in new[] { false, true })
                    {
                        P8RMobileMetrics.EditorLogicalViewportOverride = wide ? new Vector2(800, 360) : new Vector2(360, 640);
                        screen.Resize(wide ? new Vector2(1600, 720) : new Vector2(1080, 1920));
                        catalogue.ShowCatalogue(); yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
                        Assert.That(((RectTransform)back.transform.parent).anchorMax.x, Is.EqualTo(wide ? 0f : 1f),
                            "Switching layout must restore the context anchors, not retain the previous header mode.");
                        panel = Box(Field<GameObject>(catalogue, "expandedRoot").transform); backRect = Box(back);
                        Assert.That(backRect.xMin, Is.GreaterThanOrEqualTo(panel.xMin));
                        Assert.That(backRect.xMax, Is.LessThanOrEqualTo(panel.xMax));
                        Assert.That(controller.State, Is.EqualTo(state));
                    }
                }
            }
            finally { P8RMobileMetrics.EditorLogicalViewportOverride = previous; }
        }

        [UnityTest]
        public IEnumerator MobilePhone_WaitingGridDoesNotFillTheSceneWithEmptyCatalogueSpace()
        {
            var previous = P8RMobileMetrics.EditorLogicalViewportOverride;
            try
            {
                using (var screen = new NativeScreenSize())
                foreach (var pixels in new[] { new Vector2(480, 854), new Vector2(1080, 1920) })
                {
                    var density = pixels.x == 480 ? 1.5f : 3f;
                    P8RMobileMetrics.EditorLogicalViewportOverride = pixels / density;
                    screen.Resize(pixels); yield return Load();
                    var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
                    Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                    Field<Button>(Find<DecorationFloorRangeView>(), "singleGridButton").onClick.Invoke();
                    yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
                    var catalogue = Find<DecorationCatalogueView>();
                    var panel = Field<GameObject>(catalogue, "expandedRoot").GetComponent<RectTransform>();
                    var instruction = Field<RectTransform>(Find<DecorationActionBarView>(), "feedbackRoot");
                    Assert.That(instruction.gameObject.activeInHierarchy, Is.True);
                    Assert.That((Box(instruction).yMin - Box(panel).yMax) / density, Is.GreaterThanOrEqualTo(64f),
                        "Waiting for a floor tap must leave scene space between the instruction and the catalogue without an extra collapse step.");
                    var viewport = Box(catalogue.VerticalScroll.viewport);
                    Assert.That(viewport.height / density, Is.InRange(115.9f, 124f),
                        "A one-row catalogue keeps its full cards, but does not fill the remaining screen with blank space.");
                    var first = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>()
                        .Where(tile => !string.IsNullOrEmpty(tile.ItemId)).OrderBy(tile => Box(tile).xMin).First();
                    Assert.That(Box(first).yMin, Is.GreaterThanOrEqualTo(viewport.yMin - 1));
                    Assert.That(Box(first).yMax, Is.LessThanOrEqualTo(viewport.yMax + 1));
                }
            }
            finally { P8RMobileMetrics.EditorLogicalViewportOverride = previous; }
        }

        [UnityTest]
        public IEnumerator MobileLandscape_WaitingGridKeepsEffectiveCardTargetsOutsideRangeFooter()
        {
            var previous = P8RMobileMetrics.EditorLogicalViewportOverride;
            try
            {
                using (var screen = new NativeScreenSize())
                foreach (var pixels in new[] { new Vector2(1600, 720), new Vector2(1280, 720), new Vector2(1138, 640) })
                {
                    P8RMobileMetrics.EditorLogicalViewportOverride = pixels * .5f;
                    screen.Resize(pixels); yield return Load();
                    var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
                    Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                    Field<Button>(Find<DecorationFloorRangeView>(), "singleGridButton").onClick.Invoke();
                    foreach (var safe in Object.FindObjectsByType<AnimalCafe.UI.Components.SafeAreaContainer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        safe.AutoApplyRuntimeSafeArea = false;
                        safe.ApplySafeArea(new Rect(88, 40, pixels.x - 112, pixels.y - 64), pixels);
                    }
                    yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
                    var catalogue = Find<DecorationCatalogueView>();
                    var viewport = Box(catalogue.VerticalScroll.viewport);
                    var tile = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>()
                        .Where(item => !string.IsNullOrEmpty(item.ItemId)).OrderBy(item => Box(item).xMin).First();
                    var card = Box(tile);
                    var visible = Rect.MinMaxRect(Mathf.Max(card.xMin, viewport.xMin), Mathf.Max(card.yMin, viewport.yMin),
                        Mathf.Min(card.xMax, viewport.xMax), Mathf.Min(card.yMax, viewport.yMax));
                    Assert.That(visible.width / 2f, Is.GreaterThanOrEqualTo(48f));
                    Assert.That(visible.height / 2f, Is.GreaterThanOrEqualTo(48f),
                        "Check the clipped, actually visible card target, not just the compact card RectTransform.");
                    var hits = new System.Collections.Generic.List<RaycastResult>();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = visible.center }, hits);
                    Assert.That(hits, Is.Not.Empty);
                    Assert.That(hits[0].gameObject.GetComponentInParent<DecorationCatalogueTileView>(), Is.SameAs(tile));
                    foreach (var name in new[] { "wholeRoomButton", "singleGridButton" })
                    {
                        var button = Field<Button>(Find<DecorationFloorRangeView>(), name);
                        Assert.That(Box(button).Overlaps(viewport), Is.False,
                            pixels + " " + name + "=" + Box(button) + "; viewport=" + viewport + "; footer=" + Box(catalogue.SurfaceFooterHost));
                        Assert.That(Box(button).height / 2f, Is.GreaterThanOrEqualTo(47.99f));
                    }
                    var footer = Field<RectTransform>(catalogue, "surfaceFooterHost");
                    Assert.That(footer.anchorMin.x, Is.EqualTo(.5f));
                    AssertCatalogueAvoidsChrome(catalogue);
                    Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
                    yield return new WaitForSecondsRealtime(.2f);
                    Assert.That(Box(footer).Overlaps(Box(catalogue.VerticalScroll.viewport)), Is.False,
                        "Wall utility controls can reflow beside or below content, but cannot cover it.");
                    Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                    Field<Button>(Find<DecorationFloorRangeView>(), "wholeRoomButton").onClick.Invoke();
                    SelectSixFeedbackItem("floor.warm-wood"); catalogue.ShowCatalogue();
                    yield return new WaitForSecondsRealtime(.3f);
                    Assert.That(footer.anchorMin.x, Is.EqualTo(.5f), "Short landscape previews retain the compact side footer.");
                    Assert.That(footer.anchorMax.x, Is.EqualTo(.5f));
                    foreach (var field in new[] { "undoLastButton", "rotateButton", "applyAllButton" })
                    {
                        var utility = Field<Button>(Find<DecorationActionBarView>(), field);
                        var icon = utility.transform.Find("Icon")?.GetComponent<Image>();
                        Assert.That(icon, Is.Not.Null, field + " icon-only action must not become an empty button.");
                        Assert.That(icon.gameObject.activeInHierarchy && icon.enabled && icon.sprite != null, Is.True, field);
                        Assert.That(icon.raycastTarget, Is.False);
                        Assert.That(icon.color.a, Is.GreaterThan(0f));
                        Assert.That(utility.transform.Cast<Transform>().Count(child => child.name == "Icon"), Is.EqualTo(1));
                    }
                    viewport = Box(catalogue.VerticalScroll.viewport);
                    AssertCatalogueAvoidsChrome(catalogue);
                    var readiness = Find<ValidationMessageView>();
                    Field<Button>(readiness, "disclosureButton").onClick.Invoke();
                    yield return new WaitForSecondsRealtime(.2f); Canvas.ForceUpdateCanvases();
                    Assert.That(readiness.IsDetailsExpanded, Is.True);
                    TestContext.WriteLine("Compact expanded readiness " + pixels + ": readiness=" + Box(readiness)
                        + "; panel=" + Box(Field<GameObject>(catalogue, "expandedRoot").transform));
                    AssertCatalogueAvoidsChrome(catalogue);
                    Assert.That(Box(footer).Overlaps(viewport), Is.False);
                    foreach (var button in footer.GetComponentsInChildren<Button>())
                    {
                        var hit = Box(button);
                        Assert.That(hit.Overlaps(viewport), Is.False);
                        Assert.That(hit.width / 2f, Is.GreaterThanOrEqualTo(47.99f));
                        Assert.That(hit.height / 2f, Is.GreaterThanOrEqualTo(47.99f));
                        hits.Clear();
                        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = hit.center }, hits);
                        Assert.That(hits, Is.Not.Empty);
                        Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button));
                    }
                    var utilityButtons = new[] { "undoLastButton", "applyAllButton" }
                        .Select(field => Field<Button>(Find<DecorationActionBarView>(), field)).ToArray();
                    var originalIcons = utilityButtons.Select(button => button.transform.Find("Icon")).ToArray();
                    // More logical width restores the original text-only utility layout.
                    // 宽窄来回切换必须复用Icon，不能挤坏原先的文字按钮。
                    P8RMobileMetrics.EditorLogicalViewportOverride = pixels;
                    yield return new WaitForSecondsRealtime(.2f); Canvas.ForceUpdateCanvases();
                    foreach (var button in utilityButtons)
                    {
                        var label = button.transform.Find("Label").GetComponent<TMP_Text>();
                        Assert.That(label.gameObject.activeInHierarchy, Is.True);
                        Assert.That(button.transform.Find("Icon").gameObject.activeSelf, Is.False);
                        Assert.That(label.GetPreferredValues(label.text).x, Is.LessThanOrEqualTo(label.rectTransform.rect.width + 1f));
                    }
                    P8RMobileMetrics.EditorLogicalViewportOverride = pixels * .5f;
                    yield return new WaitForSecondsRealtime(.2f); Canvas.ForceUpdateCanvases();
                    for (var i = 0; i < utilityButtons.Length; i++)
                    {
                        Assert.That(utilityButtons[i].transform.Find("Icon"), Is.SameAs(originalIcons[i]));
                        Assert.That(originalIcons[i].gameObject.activeInHierarchy, Is.True);
                        Assert.That(utilityButtons[i].transform.Cast<Transform>().Count(child => child.name == "Icon"), Is.EqualTo(1));
                    }
                }
            }
            finally { P8RMobileMetrics.EditorLogicalViewportOverride = previous; }
        }

        private static void AssertCatalogueAvoidsChrome(DecorationCatalogueView catalogue)
        {
            var panel = Box(Field<GameObject>(catalogue, "expandedRoot").transform);
            foreach (var field in new[] { "p8rTopObstruction", "p8rInstructionObstruction" })
            {
                var obstruction = Field<RectTransform>(catalogue, field);
                if (obstruction != null && obstruction.gameObject.activeInHierarchy)
                    Assert.That(panel.Overlaps(Box(obstruction)), Is.False,
                        field + "=" + Box(obstruction) + "; panel=" + panel);
            }
        }

        private static void AssertCollapsedTabsAreCentered(DecorationCatalogueView catalogue)
        {
            Assert.That(catalogue.IsCollapsed, Is.True);
            var buttons = catalogue.GetComponentInChildren<DecorationModeTabsView>().GetComponentsInChildren<Button>();
            Assert.That(buttons.Length, Is.EqualTo(4));
            Assert.That((buttons.Min(button => Box(button).xMin) + buttons.Max(button => Box(button).xMax)) * .5f,
                Is.EqualTo(Box(catalogue).center.x).Within(1f), "Every collapse entry point must release hidden header reservations.");
        }

        // Real GameView owns the render target in a normal Editor. In-memory sizes only; no SaveToHDD.
        // 正常 Editor 由真实 GameView 决定尺寸；临时选项在 finally 恢复，不保存用户偏好。
        internal sealed class RealGameViewSize : System.IDisposable
        {
            private readonly UnityEditor.EditorWindow view;
            private readonly UnityEditor.EditorWindow previousFocus = UnityEditor.EditorWindow.focusedWindow;
            private readonly object group;
            private readonly int previousIndex;
            private readonly int previousCount;
            private readonly MethodInfo select;
            private readonly System.Collections.Generic.List<object> owned = new System.Collections.Generic.List<object>();
            public RealGameViewSize()
            {
                Assert.That(Application.isBatchMode, Is.False);
                var assembly = typeof(UnityEditor.Editor).Assembly;
                var viewType = assembly.GetType("UnityEditor.GameView");
                view = Resources.FindObjectsOfTypeAll(viewType).Cast<UnityEditor.EditorWindow>().FirstOrDefault();
                Assert.That(view, Is.Not.Null, "Normal Editor must provide an actual GameView.");
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
                var registry = sizesType.BaseType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                group = sizesType.GetProperty("currentGroup").GetValue(registry);
                previousCount = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
                previousIndex = (int)viewType.GetProperty("selectedSizeIndex").GetValue(view);
                select = viewType.GetMethod("SizeSelectionCallback");
            }
            public void Resize(Vector2 size)
            {
                var assembly = typeof(UnityEditor.Editor).Assembly;
                var fixedKind = System.Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution");
                var temporary = System.Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),
                    fixedKind, (int)size.x, (int)size.y, "P8R native review temporary");
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { temporary });
                owned.Add(temporary);
                select.Invoke(view, new object[] { previousCount + owned.Count - 1, null });
                view.Focus(); view.Repaint();
            }
            public void Dispose()
            {
                try { select.Invoke(view, new object[] { previousIndex, null }); }
                finally
                {
                    for (var i = owned.Count - 1; i >= 0; i--)
                    {
                        var index = previousCount + i;
                        Assert.That(group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { index }), Is.SameAs(owned[i]));
                        group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
                    }
                    view.Repaint();
                    if (previousFocus != null) previousFocus.Focus();
                }
                Assert.That((int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null), Is.EqualTo(previousCount));
            }
        }

        // Native Overlay pixels, no camera substitution, supersampling or production-quality changes.
        // 使用原生 Overlay 截图；只在正常 Editor 启用，拒绝覆盖历史证据。
        private static IEnumerator CaptureTimeSelectionMotion(string folder)
        {
            var hud = Find<TimeControlPanel>();
            var time = Find<GameTimeService>();
            var clip = (RectTransform)hud.transform.Find("P8RTimeStrip/P8RTimeSelection");
            Assert.That(clip, Is.Not.Null);
            // Capture actual intermediate frames, without slowing the production animation.
            // 按真实0.18秒动效抓取中间帧，不为截图修改游戏动画速度。
            foreach (var speed in new[] { GameSpeed.Fast, GameSpeed.Paused })
            {
                var start = clip.anchoredPosition.x;
                time.TrySetSpeed(speed);
                yield return new WaitForSecondsRealtime(.04f);
                yield return new WaitForEndOfFrame();
                var unit = P8RMobileMetrics.For(hud).Units(1);
                var x = clip.anchoredPosition.x / unit;
                Assert.That(x, speed == GameSpeed.Fast
                    ? Is.InRange(start / unit + .01f, 96.02f)
                    : Is.InRange(.01f, start / unit - .01f), "Capture must be a real in-between frame.");
                var path = System.IO.Path.GetFullPath(folder + "/30-motion-" + speed + "-mid.png");
                Assert.That(System.IO.File.Exists(path), Is.False);
                var frame = ScreenCapture.CaptureScreenshotAsTexture();
                try
                {
                    Assert.That(new Vector2(frame.width, frame.height), Is.EqualTo(new Vector2(Screen.width, Screen.height)));
                    System.IO.File.WriteAllBytes(path, frame.EncodeToPNG());
                }
                finally { Object.Destroy(frame); }
                TestContext.WriteLine("Native motion " + speed + " intermediate logical x=" + x);
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(clip.anchoredPosition.x / unit,
                    Is.EqualTo(speed == GameSpeed.Fast ? 96.03125f : 0).Within(.05f));
            }
        }

        private static IEnumerator CaptureNative(string filename, string folder)
        {
            Assert.That(Application.isBatchMode, Is.False, "Native gallery requires a normal Editor.");
            System.IO.Directory.CreateDirectory(folder);
            var path = System.IO.Path.GetFullPath(folder + "/" + filename);
            Assert.That(System.IO.File.Exists(path), Is.False, "Never overwrite earlier review evidence.");
            Canvas.ForceUpdateCanvases(); yield return new WaitForSecondsRealtime(.3f);
            // Editor-only compilation can temporarily draw a cyan dummy shader.
            // 等待真实 shader 就绪再截图，不修改画质或关闭用户的异步编译设置。
            var shaderDeadline = Time.realtimeSinceStartup + 45f;
            while (UnityEditor.ShaderUtil.anythingCompiling && Time.realtimeSinceStartup < shaderDeadline) yield return null;
            Assert.That(UnityEditor.ShaderUtil.anythingCompiling, Is.False, "Do not capture shader compilation placeholders.");
            yield return null; yield return null;
            var canvas = Find<TimeControlPanel>().GetComponentInParent<Canvas>().rootCanvas;
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            Assert.That(UnityEngine.Camera.main.targetTexture, Is.Null);
            ScreenCapture.CaptureScreenshot(path, 1);
            var deadline = Time.realtimeSinceStartup + 8f;
            while (!System.IO.File.Exists(path) && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(System.IO.File.Exists(path), Is.True, "Native capture unavailable; do not substitute a Camera capture.");
            yield return null;
            var pixels = new Texture2D(2, 2);
            try
            {
                Assert.That(pixels.LoadImage(System.IO.File.ReadAllBytes(path)), Is.True);
                Assert.That(new Vector2Int(pixels.width, pixels.height), Is.EqualTo(new Vector2Int(Screen.width, Screen.height)));
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
                if (filename.StartsWith("14-") || filename.StartsWith("15-"))
                {
                    var sign = Find<PickUpPointIndicatorView>().CurrentPreview.transform.Find("InvertedSquarePyramid");
                    var corners = sign.GetComponent<MeshFilter>().sharedMesh.vertices
                        .Select(v => UnityEngine.Camera.main.WorldToScreenPoint(sign.TransformPoint(v))).ToArray();
                    var cream = 0; var teal = 0;
                    for (var y = Mathf.CeilToInt(corners.Min(p => p.y)); y < Mathf.FloorToInt(corners.Max(p => p.y)); y++)
                        for (var x = Mathf.CeilToInt(corners.Min(p => p.x)); x < Mathf.FloorToInt(corners.Max(p => p.x)); x++)
                        {
                            var color = pixels.GetPixel(x, y);
                            if (color.r > .7f && color.g > .6f && color.b > .45f) cream++;
                            if (color.g > color.r * 1.4f && color.b > color.r * 1.2f) teal++;
                        }
                    Assert.That(cream, Is.GreaterThan(5), "The rendered pickup sign must contain its cream cup, not a flat placeholder.");
                    Assert.That(teal, Is.GreaterThan(5), "The approved teal sign must remain visible in both validity states.");
                    var footprint = Find<PickUpPointIndicatorView>().CurrentPreview.transform.Find("Footprint").GetComponent<Renderer>();
                    var footprintScreen = UnityEngine.Camera.main.WorldToScreenPoint(footprint.bounds.center);
                    var stateColor = pixels.GetPixel(Mathf.RoundToInt(footprintScreen.x), Mathf.RoundToInt(footprintScreen.y));
                    if (filename.StartsWith("15-"))
                        Assert.That(stateColor.r, Is.GreaterThan(stateColor.g * 1.4f), "Invalid footprint must render red, not mixed yellow.");
                    else
                        Assert.That(stateColor.g, Is.GreaterThan(stateColor.r * 1.4f), "Valid footprint must render green.");
                }
                if (filename.StartsWith("01-"))
                {
                    var mode = Field<TMP_Text>(Find<DecorationModeController>(), "decorationModeButtonLabel").GetComponentInParent<Button>();
                    var face = Box(mode.image);
                    var sample = pixels.GetPixel(Mathf.RoundToInt(face.xMin + face.width * .2f), Mathf.RoundToInt(face.yMin + face.height * .3f));
                    Assert.That(sample.r, Is.GreaterThan(.8f), "Capture must contain the cream HUD face, not only the camera world.");
                    Assert.That(sample.g, Is.GreaterThan(.7f));
                }
            }
            finally { Object.Destroy(pixels); }
        }


        [UnityTest]
        public IEnumerator Polish_HudIconsStayCompactAcrossSpeedAndDecorationChanges()
        {
            yield return Load();
            var hud = Find<TimeControlPanel>(); var controller = Find<DecorationModeController>();
            var service = Find<GameTimeService>();
            var pause = Field<Button>(hud, "pauseButton"); var normal = Field<Button>(hud, "normalButton");
            var fast = Field<Button>(hud, "fastButton");
            var mode = Field<TMP_Text>(controller, "decorationModeButtonLabel").GetComponentInParent<Button>();
            foreach (var speed in new[] { GameSpeed.Normal, GameSpeed.Fast })
            {
                if (speed == GameSpeed.Fast) service.SetFast(); else service.SetNormal();
                pause.onClick.Invoke(); yield return null;
                Assert.That(service.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
                Assert.That(pause.transform.Find("Icon").GetComponent<Image>().sprite.name, Is.EqualTo("resume_cocoa"));
                pause.onClick.Invoke(); yield return null;
                Assert.That(service.CurrentSpeed, Is.EqualTo(speed), "Resume must retain the previous speed, not force 1x.");
                foreach (var decorating in new[] { true, false })
                {
                    if (decorating) controller.EnterDecorationMode(); else controller.TryRequestExit();
                    yield return null; Canvas.ForceUpdateCanvases();
                    foreach (var button in new[] { pause, normal, fast, mode })
                        Assert.That(button.transform.Find("Label").gameObject.activeSelf, Is.False, button.name + " must stay icon-only after every event.");
                    Assert.That(normal.transform.Find("Icon").GetComponent<Image>().sprite.name,
                        Is.EqualTo(decorating ? "resume_muted" : "resume_cocoa"), "1x uses a single triangle, not a clock.");
                    Assert.That(fast.transform.Find("Icon").GetComponent<Image>().sprite.name,
                        Is.EqualTo(decorating ? "fast_forward_muted" : "fast_forward_cocoa"));
                    var scale = mode.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
                    var density = P8RMobileMetrics.For(mode).PixelsPerLogicalUnit;
                    Assert.That(Box(mode.image).width / density, Is.EqualTo(40f).Within(.1f));
                    Assert.That(Box(mode.image).height / density, Is.EqualTo(40f).Within(.1f));
                    Assert.That(Box(mode).width, Is.GreaterThan(Box(mode.image).width));
                    Assert.That(mode.GetComponent<Image>().color.a, Is.Zero,
                        "Legacy Awake must not paint the invisible hit area into a white rectangle.");
                    var modeInk = P8RCompleteFlowTests.MeasuredInk(mode.transform.Find("Icon").GetComponent<Image>());
                    Assert.That(Mathf.Max(modeInk.width, modeInk.height) / density, Is.InRange(19.8f, 20.2f));
                    // Transparent padding remains a real touch target outside the visible face.
                    var edge = new PointerEventData(EventSystem.current)
                    { position = new Vector2(Box(mode).xMin + scale, Box(mode).center.y) };
                    var hits = new System.Collections.Generic.List<RaycastResult>();
                    EventSystem.current.RaycastAll(edge, hits);
                    Assert.That(hits, Is.Not.Empty);
                    Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(mode));
                    Assert.That(Box(normal).width / density, Is.EqualTo(48f).Within(.1f));
                    Assert.That(Box(pause).Overlaps(Box(normal)), Is.False);
                    Assert.That(Box(normal).Overlaps(Box(fast)), Is.False);
                    Assert.That(mode.GetComponentsInChildren<Transform>(true)
                        .Any(t => (t.name == "Top Highlight" || t.name == "TopHighlight") && t.gameObject.activeInHierarchy), Is.False);
                }
            }
        }

        [UnityTest]
        public IEnumerator TimeOptical_ThreeButtonsHaveEqualGapsAndMobileTouchSize()
        {
            yield return Load();
            var hud = Find<TimeControlPanel>();
            var buttons = new[] { "pauseButton", "normalButton", "fastButton" }.Select(n => Field<Button>(hud, n)).ToArray();
            var scale = P8RMobileMetrics.For(hud).PixelsPerLogicalUnit;
            var boxes = buttons.Select(Box).ToArray();
            Assert.That((boxes[1].xMin - boxes[0].xMax) / scale,
                Is.EqualTo((boxes[2].xMin - boxes[1].xMax) / scale).Within(.1f), "Time controls need equal visible separation.");
            foreach (var box in boxes)
            {
                Assert.That(box.width / scale, Is.EqualTo(48f).Within(.1f));
                Assert.That(box.height / scale, Is.EqualTo(48f).Within(.1f));
            }
        }

        [UnityTest]
        public IEnumerator TimeOptical_DoubleTriangleStaysBalancedAfterEveryStateRefresh()
        {
            yield return Load();
            var hud = Find<TimeControlPanel>(); var controller = Find<DecorationModeController>();
            var time = Find<GameTimeService>();
            var pause = Field<Button>(hud, "pauseButton"); var normal = Field<Button>(hud, "normalButton");
            var fast = Field<Button>(hud, "fastButton");
            var scale = hud.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
            void CheckInk()
            {
                Canvas.ForceUpdateCanvases();
                // Measure the actual PNG alpha, independently of the production ink-bound table.
                // 用源图可见像素验证，不复制 production 中的尺寸常量。
                var one = P8RCompleteFlowTests.MeasuredInk(normal.transform.Find("Icon").GetComponent<Image>());
                var two = P8RCompleteFlowTests.MeasuredInk(fast.transform.Find("Icon").GetComponent<Image>());
                Assert.That(two.height / one.height, Is.InRange(.85f, 1.05f), "2x must not look like a tiny pair next to 1x.");
                Assert.That(two.width / two.height, Is.EqualTo(193f / 122f).Within(.02f), "Do not stretch the double triangle.");
                Assert.That(Vector2.Distance(two.center, Box(fast).center) / scale, Is.LessThan(.1f));
                Assert.That((two.xMin - Box(fast).xMin) / scale, Is.GreaterThanOrEqualTo(10f));
                Assert.That((Box(fast).xMax - two.xMax) / scale, Is.GreaterThanOrEqualTo(10f));
            }
            foreach (var speed in new[] { GameSpeed.Normal, GameSpeed.Fast })
            {
                if (speed == GameSpeed.Fast) time.SetFast(); else time.SetNormal();
                yield return null; CheckInk();
                pause.onClick.Invoke(); yield return null; CheckInk();
                pause.onClick.Invoke(); yield return null; CheckInk();
                Assert.That(time.CurrentSpeed, Is.EqualTo(speed));
                controller.EnterDecorationMode(); yield return null; CheckInk();
                Assert.That(new[] { pause, normal, fast }.All(b => !b.interactable), Is.True);
                controller.TryRequestExit(); yield return null; CheckInk();
                Assert.That(time.CurrentSpeed, Is.EqualTo(speed));
                hud.enabled = false; hud.enabled = true; yield return null; CheckInk();
            }
        }

        [UnityTest]
        public IEnumerator Polish_SurfaceThumbnailFitsWellAfterCategoryRebinding()
        {
            using (var screen = new NativeScreenSize())
            {
                screen.Resize(new Vector2(1080, 1920)); yield return Load();
                var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
                foreach (var mode in new[] { DecorationModeKind.Floor, DecorationModeKind.Wall, DecorationModeKind.Furniture, DecorationModeKind.Floor })
                {
                    Assert.That(controller.TryChangeMode(mode), Is.True);
                    yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
                    var checkedTiles = 0;
                    foreach (var tile in Find<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>()
                        .Where(t => !string.IsNullOrEmpty(t.ItemId)))
                    {
                        var well = tile.transform.Find("ThumbnailWell");
                        var thumb = Field<Image>(tile, "thumbnailImage");
                        if (well == null || !thumb.enabled) continue;
                        checkedTiles++;
                        if (mode != DecorationModeKind.Furniture)
                        {
                            var outer = Box(well); var inner = Box(thumb);
                            Assert.That(inner.xMin, Is.GreaterThan(outer.xMin), tile.ItemId + " left frame padding");
                            Assert.That(inner.xMax, Is.LessThan(outer.xMax), tile.ItemId + " right frame padding");
                            Assert.That(inner.yMin, Is.GreaterThan(outer.yMin), tile.ItemId + " bottom frame padding");
                            Assert.That(inner.yMax, Is.LessThan(outer.yMax), tile.ItemId + " top frame padding");
                            Assert.That(thumb.preserveAspect, Is.True, "Surface texture must not stretch.");
                        }
                        else
                            Assert.That(Field<TMP_Text>(tile, "nameLabel").gameObject.activeSelf, Is.True, "Furniture keeps its name reservation.");
                    }
                    Assert.That(checkedTiles, Is.GreaterThan(0), mode + " must exercise real visible thumbnails.");
                }
            }
        }

        [UnityTest]
        public IEnumerator Hud_TimeSegmentsShareTopLeftRow_AndRestoreExactPreviousSpeed()
        {
            yield return Load(); var hud = Find<TimeControlPanel>();
            var buttons = new[] { "pauseButton", "normalButton", "fastButton" }.Select(n => Field<Button>(hud, n)).ToArray();
            var boxes = buttons.Select(Box).ToArray();
            Assert.That(boxes[0].center.y, Is.EqualTo(boxes[1].center.y).Within(.5f), "Pause and 1x must share a row.");
            Assert.That(boxes[1].center.y, Is.EqualTo(boxes[2].center.y).Within(.5f));
            Assert.That(boxes[0].xMax, Is.LessThanOrEqualTo(boxes[1].xMin + .1f));
            Assert.That(boxes[1].xMax, Is.LessThanOrEqualTo(boxes[2].xMin + .1f));
            Assert.That(boxes[2].center.x, Is.LessThan(Screen.width * .65f));
            var time = Find<GameTimeService>(); time.SetFast();
            var controller = Find<DecorationModeController>(); controller.EnterDecorationMode(); yield return null;
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Paused));
            Assert.That(buttons.All(b => !b.interactable), Is.True);
            controller.TryRequestExit(); yield return null;
            Assert.That(time.CurrentSpeed, Is.EqualTo(GameSpeed.Fast));
        }

        [UnityTest]
        public IEnumerator Hud_ModeBadgeIsSeparate_ModeTileIsTopRight_ReadinessIsBelowTime()
        {
            yield return Load(); var hud = Find<TimeControlPanel>();
            var badge = hud.transform.Find("P8RModeBadge");
            Assert.That(badge, Is.Not.Null, "Mode retains its own badge in both responsive layouts.");
            var pause = Field<Button>(hud, "pauseButton");
            var mode = Field<TMP_Text>(Find<DecorationModeController>(), "decorationModeButtonLabel").GetComponentInParent<Button>();
            var strip = hud.transform.Find("P8RTimeStrip");
            Assert.That(strip, Is.Not.Null);
            // Compare visible chrome; the 48-unit touch root extends beyond the 32-unit face.
            // 比较可见32单位时间条，不把外扩的48单位透明点击区当成底板。
            var badgeBox = Box(badge); var stripBox = Box(strip);
            var density = P8RMobileMetrics.For(hud).PixelsPerLogicalUnit;
            Assert.That(Vector2.Distance(badgeBox.size, stripBox.size), Is.LessThan(.1f));
            if (badgeBox.xMax < stripBox.xMin)
            {
                Assert.That(badgeBox.center.y, Is.EqualTo(stripBox.center.y).Within(.5f), "Wide HUD puts badge beside time controls.");
                Assert.That((stripBox.xMin - badgeBox.xMax) / density, Is.EqualTo(8f).Within(.1f));
            }
            else
            {
                Assert.That((badgeBox.yMin - stripBox.yMax) / density, Is.EqualTo(8f).Within(.1f), "Narrow HUD retains the approved visible gap.");
                Assert.That(badgeBox.yMin, Is.GreaterThanOrEqualTo(Box(pause).yMax - .1f), "The real time touch target must not overlap the badge.");
            }
            Assert.That(Box(mode).center.x, Is.GreaterThan(Screen.width * .7f));
            var icon = mode.transform.Find("Icon").GetComponent<Image>(); var label = mode.transform.Find("Label").GetComponent<TMP_Text>();
            Assert.That(label.gameObject.activeSelf, Is.False);
            P8RCompleteFlowTests.AssertTextIconGroup(mode);
            var readiness = Find<ValidationMessageView>();
            Assert.That(readiness.IsVisible, Is.True, "Normal HUD must show the existing confirmed-layout report before the first edit.");
            Assert.That(Box(readiness).yMax, Is.LessThan(Box(pause).yMin));
            Assert.That(Box(readiness).Overlaps(Box(mode)), Is.False);
            var normalCopy = badge.GetComponentInChildren<TMP_Text>().text;
            Find<DecorationModeController>().EnterDecorationMode(); yield return null;
            Assert.That(badge.GetComponentInChildren<TMP_Text>().text, Is.Not.EqualTo(normalCopy));
            Assert.That(label.gameObject.activeSelf, Is.False);
            P8RCompleteFlowTests.AssertTextIconGroup(mode);
            Find<DecorationModeController>().TryRequestExit(); yield return null;
            Assert.That(label.gameObject.activeSelf, Is.False);
            P8RCompleteFlowTests.AssertTextIconGroup(mode);
        }

        [UnityTest]
        public IEnumerator FloatingActions_CompactLogicalGaps_NoTooltipsAfterModeCycles_KeepRaycastAndDisabledRules()
        {
            yield return Load(); var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
            foreach (var mode in new[] { DecorationModeKind.Floor, DecorationModeKind.WallDecor, DecorationModeKind.Furniture })
            {
                Assert.That(controller.TryChangeMode(mode), Is.True);
                if (mode == DecorationModeKind.Floor) continue;
                if (mode == DecorationModeKind.WallDecor)
                    Assert.That(controller.TryBeginWallMountedPreview("wall-decor.wood-shelf.01", "wall.back-left", new AnimalCafe.Layout.WallSlotPosition(4, 0)), Is.True);
                else Find<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>(true)
                    .First(t => t.ItemId == "furniture.counter.module.01" && t.gameObject.activeInHierarchy).GetComponent<Button>().onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
                var view = Find<DecorationActionBarView>();
                var buttons = view.GetComponentsInChildren<Button>().Where(b => b.gameObject.activeInHierarchy && new[] { "CancelButton", "RotateButton", "ConfirmButton", "StoreButton" }.Contains(b.name)).OrderBy(b => Box(b).x).ToArray();
                Assert.That(buttons.Length, Is.GreaterThanOrEqualTo(2));
                var scale = view.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
                var density = P8RMobileMetrics.For(view).PixelsPerLogicalUnit;
                for (var i = 0; i < buttons.Length; i++)
                {
                    var button = buttons[i]; var hit = Box(button); var face = Box(button.image);
                    Assert.That(face.width / density, Is.EqualTo(30).Within(.1f));
                    // World-corner conversion can round 48 to 47.9999886; keep a 0.01-unit tolerance.
                    // 只容忍坐标换算的微小浮点误差，实际点击与不重叠断言仍保留。
                    Assert.That(hit.width / density, Is.GreaterThanOrEqualTo(47.99f));
                    if (i > 0)
                    {
                        Assert.That((face.xMin - Box(buttons[i - 1].image).xMax) / density, Is.EqualTo(12f).Within(.1f));
                        Assert.That(hit.Overlaps(Box(buttons[i - 1])), Is.False);
                    }
                    var data = new PointerEventData(EventSystem.current) { position = hit.center, button = PointerEventData.InputButton.Left };
                    var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
                    Assert.That(hits.Count, Is.GreaterThan(0), mode + " " + button.name + " hit=" + hit + "; Screen=" + Screen.width + "x" + Screen.height);
                    Assert.That(hits.First().gameObject.GetComponentInParent<Button>(), Is.SameAs(button));
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerEnterHandler);
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.selectHandler);
                    Assert.That(button.transform.Find("Tooltip")?.gameObject.activeInHierarchy ?? false, Is.False);
                    var clicks = 0; button.onClick.AddListener(() => clicks++);
                    var enabled = button.interactable; button.interactable = false;
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
                    Assert.That(clicks, Is.Zero); button.interactable = enabled;
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
                    ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.deselectHandler);
                }
                Field<Button>(view, "cancelButton").onClick.Invoke(); yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Catalogue_HeaderTabsViewportAndFixedPickup_HaveDistinctOrderedReservations()
        {
            yield return Load(); Find<DecorationModeController>().EnterDecorationMode();
            yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
            var view = Find<DecorationCatalogueView>();
            var panel = Field<GameObject>(view, "expandedRoot");
            var title = panel.transform.Find("P8RCatalogueTitle");
            Assert.That(title, Is.Not.Null, "The authored title remains available for the responsive wide header.");
            var tabs = Find<DecorationModeTabsView>();
            var buttons = new[] { "furnitureButton", "floorButton", "wallButton", "wallDecorButton" }.Select(n => Field<Button>(tabs, n)).ToArray();
            var previousWidth = Box(buttons[0]).width;
            foreach (var button in buttons)
            {
                Assert.That(Box(button).width, Is.EqualTo(previousWidth).Within(.2f));
                if (title.gameObject.activeInHierarchy)
                    Assert.That(Box(button).Overlaps(Box(title)), Is.False, "Visible title must not overlap tabs, whether beside or above them.");
                Assert.That(Box(button).yMin, Is.GreaterThan(Box(view.VerticalScroll.viewport).yMax));
                Assert.That(button.transform.Find("Label").gameObject.activeSelf, Is.False, "Approved categories stay icon-only.");
                Assert.That(Box(button.transform.Find("Icon")).center.y, Is.EqualTo(Box(button).center.y).Within(.5f));
                Assert.That(Box(button).xMin, Is.GreaterThanOrEqualTo(Box(panel.transform).xMin));
                Assert.That(Box(button).xMax, Is.LessThanOrEqualTo(Box(panel.transform).xMax));
            }
            var pickup = Field<Button>(view, "pickUpPointButton");
            P8RCompleteFlowTests.AssertPickupReservation(view);
            Assert.That(view.VerticalScroll.viewport.rect.height, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator FloatingActions_StayBelowExpandedConfirmedReadiness_AndInsideScreen()
        {
            yield return Load(); var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
            Find<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .First(t => t.ItemId == "equipment.cash-register.01" && t.gameObject.activeInHierarchy).GetComponent<Button>().onClick.Invoke();
            Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            Assert.That(controller.TryBeginWallMountedPreview("wall-decor.wood-shelf.01", "wall.back-left", new AnimalCafe.Layout.WallSlotPosition(4, 0)), Is.True);
            var readiness = Find<ValidationMessageView>();
            yield return null; Canvas.ForceUpdateCanvases();
            var view = Find<DecorationActionBarView>();
            var collapsedPlacement = Box(Field<Button>(view, "cancelButton"));
            Field<Button>(readiness, "disclosureButton").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
            Assert.That(readiness.IsDetailsExpanded, Is.True);
            foreach (var button in new[] { Field<Button>(view, "cancelButton"), Field<Button>(view, "confirmButton") })
            {
                var box = Box(button);
                Assert.That(box.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(box.xMax, Is.LessThanOrEqualTo(Screen.width));
                Assert.That(box.yMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(box.yMax, Is.LessThanOrEqualTo(Box(readiness).yMin), "Expanded real confirmed-layout report must not cover the floating controls.");
            }
            Field<Button>(readiness, "disclosureButton").onClick.Invoke(); yield return null;
            // The whole-model avoidance may keep the same position in both states.
            // 收起后恢复有效位置，不要求上移而重新遮住模型。
            Assert.That(Box(Field<Button>(view, "cancelButton")).yMax,
                Is.EqualTo(collapsedPlacement.yMax).Within(.1f),
                "Collapsing details restores the valid pre-expansion placement without pointer movement.");
            Field<Button>(view, "cancelButton").onClick.Invoke(); yield return null;
            Field<Button>(readiness, "disclosureButton").onClick.Invoke(); yield return null;
            Assert.That(view.IsVisible, Is.False, "Disclosure with no preview must not create floating controls.");
        }

        [UnityTest]
        public IEnumerator ReadinessDisclosure_VisibleContentFitsItsClickableTarget()
        {
            yield return Load(); var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
            Find<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .First(t => t.ItemId == "equipment.cash-register.01" && t.gameObject.activeInHierarchy).GetComponent<Button>().onClick.Invoke();
            Assert.That(controller.TryConfirmFunctionalSurfacePreview(), Is.True);
            var readiness = Find<ValidationMessageView>(); var disclosure = Field<Button>(readiness, "disclosureButton");
            foreach (var pass in new[] { 0, 1 })
            {
                Canvas.ForceUpdateCanvases();
                foreach (var text in disclosure.GetComponentsInChildren<TMP_Text>())
                    Assert.That(text.GetPreferredValues(text.text).x, Is.LessThanOrEqualTo(((RectTransform)disclosure.transform).rect.width), "Visible disclosure copy must fit the target.");
                Assert.That(((RectTransform)disclosure.transform).rect.height, Is.GreaterThanOrEqualTo(48));
                disclosure.onClick.Invoke(); yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ActualPortraitAndSmallLandscape_SurfacePreviewKeepsUsableViewportAndSeparateHud()
        {
            using (var screen = new NativeScreenSize())
            {
                foreach (var size in new[] { new Vector2(1080, 1920), new Vector2(1600, 720) })
                {
                    screen.Resize(size); yield return Load();
                    Assert.That(Screen.width, Is.EqualTo((int)size.x)); Assert.That(Screen.height, Is.EqualTo((int)size.y));
                    var controller = Find<DecorationModeController>(); controller.EnterDecorationMode(); controller.TryChangeMode(DecorationModeKind.Floor);
                    Find<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>(true)
                        .First(t => t.ItemId == "floor.warm-wood" && t.gameObject.activeInHierarchy).GetComponent<Button>().onClick.Invoke();
                    yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
                    var view = Find<DecorationCatalogueView>();
                    Assert.That(view.VerticalScroll.viewport.rect.height, Is.GreaterThanOrEqualTo(64f), "Actual " + size + " needs a usable logical scroll area after header, context and footer.");
                    var panel = Box(Field<GameObject>(view, "expandedRoot").transform);
                    foreach (var button in Find<TimeControlPanel>().GetComponentsInChildren<Button>()) Assert.That(panel.Overlaps(Box(button)), Is.False);
                    Debug.Log("P8R actual layout Screen=" + Screen.width + "x" + Screen.height + "; Camera=" + UnityEngine.Camera.main.pixelWidth + "x" + UnityEngine.Camera.main.pixelHeight
                        + "; Canvas=" + view.GetComponentInParent<Canvas>().rootCanvas.renderingDisplaySize + "; viewport logical=" + view.VerticalScroll.viewport.rect.size);
                }
            }
        }

        internal sealed class NativeScreenSize : System.IDisposable
        {
            private readonly Vector2 previous = new Vector2(Screen.width, Screen.height);
            private readonly ScriptableObject nativeView;
            private readonly MethodInfo setter;
            private readonly MethodInfo displaySetter;
            public NativeScreenSize()
            {
                // Installed 6000.5.5f1 GUIView APIs change actual Screen/Camera sizes without a window or EditorPrefs entry.
                var gui = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GUIView");
                setter = gui.GetMethod("SetMainPlayModeViewSize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                displaySetter = gui.GetMethod("SetDisplayViewSize", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                nativeView = ScriptableObject.CreateInstance(gui);
            }
            public void Resize(Vector2 size) { setter.Invoke(nativeView, new object[] { size }); displaySetter.Invoke(nativeView, new object[] { 0, size }); }
            public void Dispose() { try { Resize(previous); } finally { Object.DestroyImmediate(nativeView); } }
        }

        [UnityTest]
        public IEnumerator ReferenceFlows_ActualPortraitAndLandscape_CaptureReviewGallery()
        {
            using (var screen = new NativeScreenSize())
            foreach (var size in new[] { new Vector2(1080, 1920), new Vector2(1600, 720) })
            {
                screen.Resize(size); yield return Load();
                var folder = "outputs/p8r-reference-layout-20260911/" + (size.x < size.y ? "portrait" : "landscape");
                var controller = Find<DecorationModeController>(); var view = Find<DecorationCatalogueView>();
                void Select(string id) => view.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                    .First(t => t.ItemId == id && t.gameObject.activeInHierarchy).GetComponent<Button>().onClick.Invoke();
                Button Action(string field) => Field<Button>(Find<DecorationActionBarView>(), field);
                yield return P8RCompleteFlowTests.Capture("01-normal-hud.png", folder);
                controller.EnterDecorationMode(); yield return P8RCompleteFlowTests.Capture("02-furniture-expanded-pickup.png", folder);
                Select("furniture.counter.module.01"); yield return P8RCompleteFlowTests.Capture("03-furniture-floating.png", folder);
                controller.TryRequestExit(); yield return P8RCompleteFlowTests.Capture("04-exit-preview-modal.png", folder);
                Field<Button>(Find<DecorationExitModalView>(), "continueButton").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.2f); Action("cancelButton").onClick.Invoke();
                Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True); Select("floor.warm-wood");
                yield return P8RCompleteFlowTests.Capture("05-floor-expanded.png", folder);
                view.SetSheetState(DecorationSheetState.CompactPreview, true);
                yield return P8RCompleteFlowTests.Capture("06-floor-compact.png", folder); Action("cancelButton").onClick.Invoke();
                Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
                Assert.That(controller.TryHandleSceneTap(new AnimalCafe.Decoration.Input.DecorationTouchHit(AnimalCafe.Decoration.Input.DecorationTouchHitKind.WallSurface, surfaceId: "wall.back-left")), Is.True);
                Select("paint.sage"); yield return P8RCompleteFlowTests.Capture("07a-wall-expanded.png", folder);
                view.SetSheetState(DecorationSheetState.CompactPreview, true);
                yield return P8RCompleteFlowTests.Capture("08-wall-compact.png", folder); Action("cancelButton").onClick.Invoke();
                Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
                Assert.That(controller.TryBeginWallMountedPreview("wall-decor.wood-shelf.01", "wall.back-left", new AnimalCafe.Layout.WallSlotPosition(4, 0)), Is.True);
                yield return P8RCompleteFlowTests.Capture("09-shelf-new.png", folder); Action("confirmButton").onClick.Invoke();
                var shelf = Field<AnimalCafe.Layout.WallMountedLayout>(controller, "phase7WallMountedLayout").CaptureSnapshot().Instances.Single(i => i.DefinitionId == "wall-decor.wood-shelf.01");
                Assert.That(controller.TryHandleSceneTap(new AnimalCafe.Decoration.Input.DecorationTouchHit(AnimalCafe.Decoration.Input.DecorationTouchHitKind.WallMounted, targetId: shelf.InstanceId)), Is.True);
                yield return P8RCompleteFlowTests.Capture("10-shelf-existing.png", folder); Action("storeButton").onClick.Invoke();
                yield return P8RCompleteFlowTests.Capture("11-shelf-put-away-modal.png", folder);
                Field<Button>(Find<DecorationStoreModalView>(), "cancelButton").onClick.Invoke();
                yield return new WaitForSecondsRealtime(.2f); Assert.That(controller.ActiveWallMountedPreview, Is.Not.Null);
                yield return P8RCompleteFlowTests.Capture("12-shelf-modal-recovery.png", folder); Action("cancelButton").onClick.Invoke();
                view.SetSheetState(DecorationSheetState.TabsOnly, false); yield return P8RCompleteFlowTests.Capture("13-tabs-only.png", folder);
                controller.ExitDecorationMode();
                var readiness = Find<ValidationMessageView>();
                yield return P8RCompleteFlowTests.Capture("14-real-confirmed-readiness.png", folder);
                var failures = Enumerable.Range(0, 24).Select(i => P8RCompleteFlowTests.Failure(AnimalCafe.Layout.LayoutReadinessSeverity.Blocking,
                    (AnimalCafe.Layout.LayoutReadinessFailureCode)(i % 12), i)).ToArray();
                readiness.ShowReadiness(P8RCompleteFlowTests.Report(false, failures));
                Field<Button>(readiness, "disclosureButton").onClick.Invoke();
                Assert.That(readiness.IsDetailsExpanded && readiness.GetComponent<ScrollRect>().vertical, Is.True);
                yield return P8RCompleteFlowTests.Capture("15-INJECTED-long-readiness-expanded.png", folder);
                readiness.GetComponent<ScrollRect>().verticalNormalizedPosition = 0;
                yield return P8RCompleteFlowTests.Capture("16-INJECTED-long-readiness-bottom.png", folder);
                Debug.Log("P8R reference gallery actual Screen=" + Screen.width + "x" + Screen.height + "; Camera=" + UnityEngine.Camera.main.pixelWidth + "x" + UnityEngine.Camera.main.pixelHeight
                    + "; Canvas=" + view.GetComponentInParent<Canvas>().rootCanvas.renderingDisplaySize + "; folder=" + folder);
            }
        }

        [UnityTest]
        public IEnumerator Catalogue_ThreeSheetStatesKeepTabsReachable_AndSurfaceFootersExcludePickup()
        {
            yield return Load(); var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
            var view = Find<DecorationCatalogueView>(); var tabs = Find<DecorationModeTabsView>();
            foreach (var mode in new[] { DecorationModeKind.Furniture, DecorationModeKind.Floor, DecorationModeKind.Wall, DecorationModeKind.WallDecor })
            {
                Assert.That(controller.TryChangeMode(mode), Is.True);
                foreach (var state in new[] { DecorationSheetState.Expanded, DecorationSheetState.CompactPreview, DecorationSheetState.TabsOnly })
                {
                    view.SetSheetState(state, state == DecorationSheetState.CompactPreview);
                    yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases();
                    foreach (var button in tabs.GetComponentsInChildren<Button>())
                    {
                        Assert.That(button.gameObject.activeInHierarchy, Is.True);
                        Assert.That(Box(button).yMin, Is.GreaterThanOrEqualTo(-.5f), mode + " " + state);
                        Assert.That(Box(button).yMax, Is.LessThanOrEqualTo(Screen.height + .5f), mode + " " + state);
                        var data = new PointerEventData(EventSystem.current) { position = Box(button).center };
                        var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
                        Assert.That(hits.Count, Is.GreaterThan(0));
                        Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button));
                    }
                    if (state == DecorationSheetState.Expanded)
                    {
                        Assert.That(Field<Button>(view, "pickUpPointButton").gameObject.activeInHierarchy, Is.EqualTo(mode == DecorationModeKind.Furniture));
                        Assert.That(view.VerticalScroll.viewport.rect.height, Is.GreaterThan(0));
                    }
                }
                view.SetSheetState(DecorationSheetState.Expanded, false);
                yield return new WaitForSecondsRealtime(.3f);
            }
        }
    }
}
#endif

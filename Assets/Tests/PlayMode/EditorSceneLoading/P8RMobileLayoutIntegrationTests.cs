#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.Components;
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
    /// <summary>Production scene geometry measured in explicit Android dp / iOS pt fixtures.
    /// 使用明确的平台 logical viewport，避免将 Canvas unit 当作手机点击尺寸。</summary>
    public sealed class P8RMobileLayoutIntegrationTests
    {
        private Scene ownedScene;
        private float previousTimeScale;
        private object previousLogicalViewport;
        private PropertyInfo logicalViewportOverride;

        [SetUp]
        public void RecordBoundary()
        {
            previousTimeScale = Time.timeScale;
            logicalViewportOverride = typeof(TimeControlPanel).Assembly
                .GetType("AnimalCafe.UI.P8R.P8RMobileMetrics")?
                .GetProperty("EditorLogicalViewportOverride", BindingFlags.Public | BindingFlags.Static);
            previousLogicalViewport = logicalViewportOverride?.GetValue(null);
        }

        [UnityTearDown]
        public IEnumerator ReleaseScene()
        {
            yield return UnloadOwnedScene();
            logicalViewportOverride?.SetValue(null, previousLogicalViewport);
            Time.timeScale = previousTimeScale;
            yield return null;
        }

        private IEnumerator UnloadOwnedScene()
        {
            if (ownedScene.IsValid() && ownedScene.isLoaded)
            {
                var assets = Phase8SceneInputTestCleanup.CaptureAssets(ownedScene);
                foreach (var controller in ownedScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true)))
                    controller.enabled = false;
                SceneManager.SetActiveScene(SceneManager.CreateScene("P8RMobileCleanup"));
                var unload = SceneManager.UnloadSceneAsync(ownedScene);
                while (unload != null && !unload.isDone) yield return null;
                Phase8SceneInputTestCleanup.DisposeReleasedAssets(assets);
            }
            ownedScene = default;
        }

        private IEnumerator Load(Vector2 pixels, Vector2 logical)
        {
            yield return UnloadOwnedScene();
            logicalViewportOverride?.SetValue(null, logical);
            ownedScene = EditorSceneManager.LoadSceneInPlayMode("Assets/Scenes/MainCafe.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null; yield return null;
            Assert.That(new Vector2(Screen.width, Screen.height), Is.EqualTo(pixels));
            Canvas.ForceUpdateCanvases();
        }

        [UnityTest]
        public IEnumerator PhoneAndTablet_HudCatalogueAndFloorActionsKeep48LogicalTargetsWithoutOverlap()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            foreach (var sample in new[] {
                new Vector4(1080, 2400, 360, 800), new Vector4(2400, 1080, 800, 360),
                new Vector4(960, 1704, 320, 568), new Vector4(1920, 1080, 640, 360),
                new Vector4(1440, 1080, 480, 360),
                new Vector4(1536, 2048, 768, 1024), new Vector4(2048, 1536, 1024, 768) })
            {
                var pixels = new Vector2(sample.x, sample.y); var logical = new Vector2(sample.z, sample.w);
                TestContext.WriteLine("Mobile geometry profile: " + logical + " logical / " + pixels + " pixels");
                screen.Resize(pixels); yield return Load(pixels, logical);
                var density = pixels.x / logical.x;
                var hud = Find<TimeControlPanel>();
                var hudButtons = hud.GetComponentsInChildren<Button>().ToArray();
                AssertTargets(hudButtons, density); AssertNoOverlap(hudButtons);
                var normalInk = P8RCompleteFlowTests.MeasuredInk(Field<Button>(hud, "normalButton").transform.Find("Icon").GetComponent<Image>());
                var fastInk = P8RCompleteFlowTests.MeasuredInk(Field<Button>(hud, "fastButton").transform.Find("Icon").GetComponent<Image>());
                Assert.That(fastInk.height / normalInk.height, Is.InRange(.85f, 1.05f), "Wide 2x artwork retains optical height compensation.");
                var readiness = Find<ValidationMessageView>();
                Assert.That(Box(readiness).yMax, Is.LessThanOrEqualTo(hudButtons.Min(button => Box(button).yMin) - density * 4),
                    "The confirmed readiness strip starts below the actual HUD buttons, across separate Canvas branches.");
                var disclosure = Field<Button>(readiness, "disclosureButton");
                if (disclosure != null && disclosure.gameObject.activeInHierarchy) AssertTargets(new[] { disclosure }, density);
                Assert.That(Field<TMP_Text>(readiness, "messageLabel").fontSize * CanvasScale(readiness) / density,
                    Is.GreaterThanOrEqualTo(13.9f), "Compact readiness body stays at least 14 platform logical units, not half-sized text.");

                var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
                yield return Settle();
                var catalogue = Find<DecorationCatalogueView>();
                var tabs = Find<DecorationModeTabsView>().GetComponentsInChildren<Button>().ToArray();
                var collapse = Field<Button>(catalogue, "collapseButton");
                var pickup = Field<Button>(catalogue, "pickUpPointButton");
                AssertTargets(tabs.Concat(new[] { collapse, pickup }).ToArray(), density);
                AssertNoOverlap(hudButtons.Concat(tabs).Concat(new[] { collapse, pickup }).ToArray());
                Assert.That(Box(Field<GameObject>(catalogue, "expandedRoot").transform).yMax,
                    Is.LessThanOrEqualTo(Box(readiness).yMin), "The sheet reserves actual readiness and HUD geometry.");
                foreach (var tile in catalogue.GetComponentsInChildren<DecorationCatalogueTileView>()
                    .Where(tile => tile.ItemId != null))
                {
                    var caption = Field<TMP_Text>(tile, "nameLabel");
                    if (caption != null && caption.gameObject.activeInHierarchy)
                        Assert.That(caption.fontSize * CanvasScale(caption) / density, Is.EqualTo(11.5f).Within(.1f), tile.ItemId);
                }

                Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                    .First(tile => tile.ItemId == "floor.warm-wood" && tile.gameObject.activeInHierarchy).GetComponent<Button>().onClick.Invoke();
                yield return Settle();
                var actions = Find<DecorationActionBarView>().GetComponentsInChildren<Button>().ToArray();
                var ranges = Find<DecorationFloorRangeView>().GetComponentsInChildren<Button>().ToArray();
                AssertTargets(actions.Concat(ranges).ToArray(), density);
                AssertNoOverlap(actions.Concat(ranges).ToArray());
                var rangeView = Find<DecorationFloorRangeView>();
                foreach (var range in new[] { SurfaceEditScope.WholeRoomFloor, SurfaceEditScope.SingleGridFloor })
                {
                    rangeView.SetSelected(range);
                    var whole = Field<Button>(rangeView, "wholeRoomButton"); var single = Field<Button>(rangeView, "singleGridButton");
                    Assert.That(whole.interactable, Is.EqualTo(range != SurfaceEditScope.WholeRoomFloor));
                    Assert.That(single.interactable, Is.EqualTo(range != SurfaceEditScope.SingleGridFloor));
                    Assert.That(whole.transform.Find("Icon").GetComponent<Image>().sprite.name, Is.EqualTo("range_whole_room_color"));
                    Assert.That(single.transform.Find("Icon").GetComponent<Image>().sprite.name, Is.EqualTo("range_single_grid_color"));
                }
                rangeView.SetSelected(SurfaceEditScope.WholeRoomFloor);
                AssertTargets(actions.Concat(ranges).ToArray(), density);
                AssertNoOverlap(actions.Concat(ranges).ToArray());
                var viewport = Box(catalogue.VerticalScroll.viewport);
                Assert.That(viewport.height / density, Is.GreaterThanOrEqualTo(47.9f), "Short landscape retains a usable scroll viewport after real row reservations.");
                foreach (var button in actions.Concat(ranges).Concat(tabs))
                    Assert.That(viewport.Overlaps(Box(button)), Is.False, "Catalogue content must not scroll underneath fixed controls: " + button.name);
            }
        }

        [UnityTest]
        public IEnumerator SmallPhone_FloatingSupportActionsAvoidPickupSignAndFixedUi()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                var pixels = new Vector2(480, 854); var logical = pixels / 1.5f;
                screen.Resize(pixels); yield return Load(pixels, logical);
                var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
                var runtime = Find<CafeLayoutRuntime>();
                var support = FurnitureInstance.CreateNew("furniture.counter.module.01", new GridPosition(4, 3), FurnitureRotation.Degrees0);
                Assert.That(runtime.Layout.PlaceFurniture(support).Succeeded, Is.True);
                Find<FurnitureSceneRegistry>().Rebuild(runtime.Layout.FurnitureInstances);
                var catalogue = Find<DecorationCatalogueView>(); catalogue.ShowCatalogue(); yield return Settle();
                Field<Button>(catalogue, "pickUpPointButton").onClick.Invoke();
                var address = new SurfaceSlotAddress(support.InstanceId, "slot.0");
                Assert.That(controller.TryMoveFunctionalSurfacePreview(address), Is.True);
                Field<Button>(Find<DecorationActionBarView>(), "confirmButton").onClick.Invoke();
                yield return Settle();
                var point = runtime.FunctionalSurfaceLayout.PickUpPoints.Single(item => item.Address.Equals(address));
                typeof(DecorationModeController).GetMethod("HandleFurnitureBegan", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { support.InstanceId });
                yield return Settle();
                Assert.That(Find<PickUpPointIndicatorView>().TryGet(point.InstanceId, out var indicator), Is.True);
                var visual = indicator.transform.Find("InvertedSquarePyramid");
                var vertices = visual.GetComponent<MeshFilter>().sharedMesh.vertices
                    .Select(vertex => UnityEngine.Camera.main.WorldToScreenPoint(visual.TransformPoint(vertex))).ToArray();
                var sign = Rect.MinMaxRect(vertices.Min(v => v.x), vertices.Min(v => v.y), vertices.Max(v => v.x), vertices.Max(v => v.y));
                var actions = Find<DecorationActionBarView>().GetComponentsInChildren<Button>();
                AssertTargets(actions, 1.5f, floating: true); AssertNoOverlap(actions);
                var fixedControls = Find<TimeControlPanel>().GetComponentsInChildren<Button>()
                    .Concat(Find<DecorationModeTabsView>().GetComponentsInChildren<Button>()).ToArray();
                foreach (var action in actions)
                {
                    Assert.That(Box(action).Overlaps(sign), Is.False, action.name + " covers the real pickup sign projection.");
                    Assert.That(Box(action).Overlaps(Box(Find<ValidationMessageView>())), Is.False, action.name + " covers readiness.");
                    Assert.That(Box(action).Overlaps(Box(catalogue.CollapsedHandleRect)), Is.False, action.name + " covers the collapsed handle.");
                    foreach (var fixedControl in fixedControls)
                        Assert.That(Box(action).Overlaps(Box(fixedControl)), Is.False, action.name + " covers " + fixedControl.name);
                }
            }
        }

        [UnityTest]
        public IEnumerator NecessaryInstruction_IsScreenPinnedBelowReadinessAndRestoresAfterModal()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(new Vector2(2400, 1080)); yield return Load(new Vector2(2400, 1080), new Vector2(800, 360));
                var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
                Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
                yield return Settle();
                var action = Find<DecorationActionBarView>(); var notice = Field<RectTransform>(action, "feedbackRoot");
                var copy = Field<TMP_Text>(action, "feedbackLabel");
                var readiness = Find<ValidationMessageView>();
                Assert.That(notice.gameObject.activeInHierarchy, Is.True);
                Assert.That(notice.IsChildOf(action.transform), Is.False, "The necessary instruction is hosted on the screen, outside the bottom ActionBar hierarchy.");
                Assert.That(notice.GetComponentInParent<Canvas>().rootCanvas, Is.SameAs(Find<TimeControlPanel>().GetComponentInParent<Canvas>().rootCanvas));
                Assert.That(Box(notice).center.x, Is.EqualTo(Screen.safeArea.center.x).Within(3f));
                Assert.That(Box(notice).yMax, Is.LessThanOrEqualTo(Box(readiness).yMin - 1f));
                Assert.That(copy.fontSize * CanvasScale(copy) / 3f, Is.InRange(13.9f, 14.1f));
                Assert.That(copy.maxVisibleLines, Is.EqualTo(2));
                Assert.That(copy.GetPreferredValues(copy.text, copy.rectTransform.rect.width, Mathf.Infinity).y,
                    Is.LessThanOrEqualTo(copy.rectTransform.rect.height + 1f), "Necessary instruction height is measured from content.");
                foreach (var graphic in notice.GetComponentsInChildren<Graphic>(true)) Assert.That(graphic.raycastTarget, Is.False);
                var before = Box(notice);
                Find<DecorationCatalogueView>().SetSheetState(DecorationSheetState.TabsOnly, false);
                yield return Settle();
                Assert.That(Box(notice).y, Is.EqualTo(before.y).Within(1f), "Sheet collapse cannot move the instruction.");
                yield return new WaitForSecondsRealtime(2f);
                Assert.That(notice.gameObject.activeInHierarchy, Is.True, "Target instructions do not expire as timed Toasts.");
                screen.Resize(new Vector2(1920, 1080));
                logicalViewportOverride?.SetValue(null, new Vector2(640, 360));
                yield return Settle();
                Assert.That(notice.gameObject.activeInHierarchy, Is.True, "Collapsing the footer cannot disable the instruction owner during resize.");
                Assert.That(Box(notice).center.x, Is.EqualTo(Screen.safeArea.center.x).Within(3f));
                Assert.That(copy.fontSize * CanvasScale(copy) / 3f, Is.InRange(13.9f, 14.1f));
                var modal = Find<DecorationExitModalView>(); modal.Show();
                yield return Settle();
                Assert.That(notice.gameObject.activeInHierarchy && notice.GetComponent<CanvasGroup>().alpha > 0f, Is.False);
                modal.Close(); yield return Settle();
                Assert.That(notice.gameObject.activeInHierarchy && notice.GetComponent<CanvasGroup>().alpha > 0f, Is.True);
                Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
                yield return Settle();
                Assert.That(notice.gameObject.activeInHierarchy, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator NecessaryInstruction_FollowsOwnerDisableAndReenableWithoutModalResurrection()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(new Vector2(1920, 1080)); yield return Load(new Vector2(1920, 1080), new Vector2(640, 360));
                var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
                controller.TryChangeMode(DecorationModeKind.Wall); yield return Settle();
                var action = Find<DecorationActionBarView>();
                var notice = Field<RectTransform>(action, "feedbackRoot");
                Assert.That(notice.gameObject.activeInHierarchy, Is.True);
                var visibilityChanges = 0;
                action.InstructionPresentationChanged += () => visibilityChanges++;
                action.enabled = false;
                Assert.That(notice.gameObject.activeInHierarchy, Is.False);
                Assert.That(action.VisibleInstructionRect, Is.Null);
                Assert.That(visibilityChanges, Is.EqualTo(1), "Owner shutdown releases the actual catalogue obstruction exactly once.");
                action.RefreshInstructionLayout();
                Assert.That(notice.gameObject.activeInHierarchy, Is.False, "A layout callback cannot revive an inactive owner's detached notice.");
                var modal = Find<DecorationExitModalView>(); modal.Show(); yield return Settle();
                modal.Close(); yield return Settle();
                Assert.That(notice.gameObject.activeInHierarchy, Is.False, "Closing a modal cannot revive an inactive owner's detached notice.");
                action.enabled = true; yield return Settle();
                Assert.That(notice.gameObject.activeInHierarchy, Is.True, "Re-enabling the instruction owner restores its still-required notice.");
                Assert.That(action.VisibleInstructionRect, Is.SameAs(notice));
                Assert.That(visibilityChanges, Is.EqualTo(2), "Lifecycle visibility changes do not cause a layout notification loop.");
                Object.Destroy(action); yield return null;
                Assert.DoesNotThrow(() => action.RefreshInstructionLayout(), "Managed teardown references may outlive their Unity component.");
                Assert.DoesNotThrow(() => action.SetInstructionModalCovered(false));
                var readiness = Find<ValidationMessageView>();
                Field<Button>(readiness, "disclosureButton")?.onClick.Invoke();
                yield return Settle();
                modal.Show(); yield return Settle(); modal.Close(); yield return Settle();
            }
        }

        [UnityTest]
        public IEnumerator NecessaryInstruction_AndExpandedReadinessLeaveCatalogueControlsAndViewportClear()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(new Vector2(1920, 1080)); yield return Load(new Vector2(1920, 1080), new Vector2(640, 360));
                var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
                foreach (var mode in new[] { DecorationModeKind.Wall, DecorationModeKind.Floor })
                {
                controller.TryChangeMode(mode);
                if (mode == DecorationModeKind.Floor) Assert.That(controller.TrySelectFloorRange(SurfaceEditScope.SingleGridFloor), Is.True);
                yield return Settle();
                var readiness = Find<ValidationMessageView>();
                var disclosure = Field<Button>(readiness, "disclosureButton");
                if (!readiness.IsDetailsExpanded && disclosure != null && disclosure.gameObject.activeInHierarchy) disclosure.onClick.Invoke();
                yield return Settle();
                var catalogue = Find<DecorationCatalogueView>();
                var notice = Field<RectTransform>(Find<DecorationActionBarView>(), "feedbackRoot");
                var panel = Field<GameObject>(catalogue, "expandedRoot");
                var title = panel.transform.Find("P8RCatalogueTitle");
                Assert.That(Box(notice).Overlaps(Box(title)), Is.False, "Necessary instruction cannot cover the catalogue header.");
                foreach (var button in Find<DecorationModeTabsView>().GetComponentsInChildren<Button>())
                    Assert.That(Box(notice).Overlaps(Box(button)), Is.False, "Necessary instruction cannot cover a category target.");
                Assert.That(Box(notice).Overlaps(Box(catalogue.VerticalScroll.viewport)), Is.False);
                Assert.That(Box(catalogue.VerticalScroll.viewport).height / 3f, Is.GreaterThanOrEqualTo(47.9f));
                if (mode == DecorationModeKind.Floor)
                {
                    var ranges = Find<DecorationFloorRangeView>().GetComponentsInChildren<Button>();
                    AssertTargets(ranges, 3f);
                    foreach (var range in ranges) Assert.That(Box(notice).Overlaps(Box(range)), Is.False);
                }
                }
            }
        }

        [UnityTest]
        public IEnumerator SmallPhone_ReopenedCatalogueRetainsUsableReturnToEditingAction()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(new Vector2(960, 1704)); yield return Load(new Vector2(960, 1704), new Vector2(320, 568));
                var controller = Find<DecorationModeController>(); controller.EnterDecorationMode();
                var catalogue = Find<DecorationCatalogueView>();
                catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                    .First(tile => tile.ItemId == "furniture.counter.module.01" && tile.gameObject.activeInHierarchy).GetComponent<Button>().onClick.Invoke();
                yield return Settle();
                catalogue.ShowCatalogue(); yield return Settle();
                var button = Field<Button>(catalogue, "returnToEditingButton");
                Assert.That(button.gameObject.activeInHierarchy, Is.True);
                AssertTargets(new[] { button }, 3f);
                var label = button.GetComponentInChildren<TMP_Text>();
                Assert.That(label.fontSize * CanvasScale(label) / 3f, Is.GreaterThanOrEqualTo(13.9f));
                Assert.That(Box(catalogue.VerticalScroll.viewport).height / 3f, Is.GreaterThanOrEqualTo(47.9f),
                    "Invisible editing copy must not consume catalogue space behind the Return action.");
                var previous = controller.State;
                button.onClick.Invoke(); yield return Settle();
                Assert.That(controller.State, Is.EqualTo(previous), "Returning to the preview changes only presentation.");
            }
        }

        [UnityTest]
        public IEnumerator PhoneLandscape_ModalsReflowWithoutShrinkingTargetsOrBodyText()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(new Vector2(2400, 1080)); yield return Load(new Vector2(2400, 1080), new Vector2(800, 360));
                var exit = Find<DecorationExitModalView>(); exit.Show(); yield return Settle();
                var buttons = new[] { Field<Button>(exit, "continueButton"), Field<Button>(exit, "discardButton") };
                AssertTargets(buttons, 3f); AssertNoOverlap(buttons);
                AssertModalText(exit, "titleLabel", 16f); AssertModalText(exit, "bodyLabel", 14f);
                exit.Close();
                var store = Find<DecorationStoreModalView>();
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>("Assets/UI/P8R/DC_P8RFurniture.asset");
                store.Show(asset.Entries[0].Definition); yield return Settle();
                buttons = new[] { Field<Button>(store, "confirmButton"), Field<Button>(store, "cancelButton") };
                AssertTargets(buttons, 3f); AssertNoOverlap(buttons);
                AssertModalText(store, "titleLabel", 16f); AssertModalText(store, "bodyLabel", 14f);
                store.CloseForOwnerShutdown();
            }
        }

        [UnityTest]
        public IEnumerator SmallPhone_ModalsRespectAsymmetricSafeAreasAndLongPickupCopy()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            foreach (var logical in new[] { new Vector2(320, 568), new Vector2(640, 360) })
            foreach (var inset in new[] { false, true })
            {
                var pixels = logical * 3f;
                screen.Resize(pixels); yield return Load(pixels, logical);
                var safeLogical = !inset ? new Rect(Vector2.zero, logical) : logical.x == 320
                    ? new Rect(24, 20, 288, 504) : new Rect(44, 20, 584, 328);
                var expectedSafe = new Rect(safeLogical.position * 3f, safeLogical.size * 3f);
                TestContext.WriteLine("Modal profile: " + logical + "; injected SafeArea: " + safeLogical);
                // Inject only into production-owned containers; never manufacture a safe parent in this fixture.
                // 只注入生产已有的安全区容器，不在测试里补布局来掩盖缺链。
                foreach (var area in ownedScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SafeAreaContainer>(true)))
                {
                    area.AutoApplyRuntimeSafeArea = false;
                    area.ApplySafeArea(expectedSafe, pixels);
                }
                yield return Settle();
                var exit = Find<DecorationExitModalView>(); exit.Show(); yield return Settle();
                Assert.That(exit.GetComponentsInChildren<SafeAreaContainer>(true).Length, Is.EqualTo(1),
                    "Exit owns exactly one dedicated card SafeArea host, with no re-enable duplication.");
                AssertModalGeometry(exit, Field<RectTransform>(exit, "modalCard"), expectedSafe,
                    Field<Button>(exit, "continueButton"), Field<Button>(exit, "discardButton"));
                exit.Close(); yield return Settle();
                var store = Find<DecorationStoreModalView>();
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>("Assets/UI/P8R/DC_P8RFurniture.asset");
                store.Show(asset.Entries[0].Definition); yield return Settle();
                AssertModalGeometry(store, store.ContentRect, expectedSafe,
                    Field<Button>(store, "confirmButton"), Field<Button>(store, "cancelButton"));
                store.CloseForOwnerShutdown(); yield return Settle();
                store.ShowFunctionalSurface(DecorationCatalogueItemKind.PickUpPoint); yield return Settle();
                AssertModalGeometry(store, store.ContentRect, expectedSafe,
                    Field<Button>(store, "confirmButton"), Field<Button>(store, "cancelButton"));
                store.CloseForOwnerShutdown(); yield return Settle();
            }
        }

        private static void AssertModalGeometry(object owner, RectTransform card, Rect safe, params Button[] buttons)
        {
            AssertInside(Box(card), safe, "Modal card");
            AssertTargets(buttons, 3f, safe); AssertNoOverlap(buttons);
            AssertModalText(owner, "titleLabel", 16f); AssertModalText(owner, "bodyLabel", 14f);
            var title = Field<TMP_Text>(owner, "titleLabel"); var body = Field<TMP_Text>(owner, "bodyLabel");
            AssertInside(Box(title), safe, "Title"); AssertInside(Box(body), safe, "Body");
            Assert.That(Box(title).yMin, Is.GreaterThanOrEqualTo(Box(body).yMax - 1f));
            foreach (var button in buttons)
            {
                Assert.That(Box(body).yMin, Is.GreaterThanOrEqualTo(Box(button).yMax - 1f));
                var label = button.transform.Find("Label").GetComponent<TMP_Text>();
                Assert.That(label.fontSize * CanvasScale(label) / 3f, Is.GreaterThanOrEqualTo(13.9f));
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, button.name + " label clips");
            }
            foreach (var rect in card.GetComponentsInChildren<RectTransform>(true))
                Assert.That(rect.localScale, Is.EqualTo(Vector3.one), rect.name + " must reflow instead of shrink");
        }

        private static void AssertInside(Rect rect, Rect safe, string message)
        {
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(safe.xMin - 1f), message);
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(safe.xMax + 1f), message);
            Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(safe.yMin - 1f), message);
            Assert.That(rect.yMax, Is.LessThanOrEqualTo(safe.yMax + 1f), message);
        }

        private static void AssertModalText(object owner, string field, float minimum)
        {
            var label = Field<TMP_Text>(owner, field);
            Assert.That(Box(label).height, Is.GreaterThan(0));
            Assert.That(label.fontSize * CanvasScale(label) * label.transform.lossyScale.x /
                label.GetComponentInParent<Canvas>().transform.lossyScale.x / 3f, Is.GreaterThanOrEqualTo(minimum - .1f));
            Assert.That(label.GetPreferredValues(label.text, label.rectTransform.rect.width, Mathf.Infinity).y,
                Is.LessThanOrEqualTo(label.rectTransform.rect.height + 1f));
        }
        private static IEnumerator Settle() { yield return new WaitForSecondsRealtime(.3f); Canvas.ForceUpdateCanvases(); }
        private static T Find<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single();
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private static float CanvasScale(Component component) => component.GetComponentInParent<Canvas>().rootCanvas.scaleFactor;
        private static Rect Box(Component component)
        {
            var rect = (RectTransform)component.transform; var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var canvas = component.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y), points.Max(point => point.x), points.Max(point => point.y));
        }
        private static void AssertTargets(Button[] buttons, float density, Rect? expectedSafe = null, bool floating = false)
        {
            var safe = expectedSafe ?? Screen.safeArea;
            foreach (var button in buttons)
            {
                var rect = Box(button);
                // Only Furniture/Wall Decor floating tools use the approved 44-wide touch root.
                // 仅浮动按钮采用44宽度，其他控件继续保留原48点击区检查。
                Assert.That(rect.width / density, floating ? Is.EqualTo(44f).Within(.1f)
                    : Is.GreaterThanOrEqualTo(47.9f), button.name + " target width");
                Assert.That(rect.height / density, Is.GreaterThanOrEqualTo(47.9f), button.name + " target height");
                AssertInside(rect, safe, button.name);
            }
        }
        private static void AssertNoOverlap(Button[] buttons)
        {
            for (var i = 0; i < buttons.Length; i++)
                for (var j = 0; j < i; j++)
                    Assert.That(Box(buttons[i]).Overlaps(Box(buttons[j])), Is.False, buttons[i].name + " overlaps " + buttons[j].name);
        }
    }
}
#endif

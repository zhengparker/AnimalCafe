#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.UI;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    /// <summary>
    /// Verifies the compact visible chrome separately from the unchanged touch roots.
    /// 真实 MainCafe 场景中分别验证较小可见外观与至少 48 logical-unit 的点击范围。
    /// </summary>
    public sealed class P8RCompactChromeTests
    {
        private Scene ownedScene;
        private Vector2? previousProfile;
        private float previousTimeScale;

        [SetUp]
        public void RecordBoundary()
        {
            previousProfile = P8RMobileMetrics.EditorLogicalViewportOverride;
            previousTimeScale = Time.timeScale;
        }

        [UnityTearDown]
        public IEnumerator ReleaseScene()
        {
            yield return UnloadOwnedScene();
            P8RMobileMetrics.EditorLogicalViewportOverride = previousProfile;
            Time.timeScale = previousTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CategoryInfo_ShowsStationHelp_DismissesAndClosesWithCatalogue()
        {
            using var screen = new P8RReferenceLayoutTests.NativeScreenSize();
            var profile = ChromeProfile.All[0]; screen.Resize(profile.Pixels);
            yield return Load(profile);
            var controller = Component<DecorationModeController>(); controller.EnterDecorationMode();
            var view = Component<DecorationCatalogueView>(); view.ShowCatalogue();
            yield return Settle();
            var infos = view.GetComponentsInChildren<Button>(true).Where(button => button.name == "CategoryInfo").ToArray();
            Assert.That(infos.Length, Is.EqualTo(2));
            foreach (var id in new[] { "cash-register", "coffee-machine" })
            {
                var button = infos.Single(candidate => candidate.transform.parent.name == "CategoryRow_" + id);
                CatalogueTestScrolling.Reveal(button);
                yield return null;
                var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                UnityEngine.EventSystems.EventSystem.current.RaycastAll(new UnityEngine.EventSystems.PointerEventData(
                    UnityEngine.EventSystems.EventSystem.current) { position = Box(button).center }, hits);
                Assert.That(hits.First().gameObject.GetComponentInParent<Button>(), Is.SameAs(button));
                button.onClick.Invoke();
                yield return null;
                var dismiss = view.GetComponentsInChildren<Button>().Single(candidate => candidate.name == "CategoryHelpDismiss");
                var card = dismiss.transform.Find("CategoryHelpCard");
                var label = card.GetComponentInChildren<TMP_Text>();
                Assert.That(label.text, Does.Contain("counter"));
                Assert.That(label.text, Does.Contain(id == "cash-register" ? "pay" : "coffee"));
                AssertTextFits(label, id);
                AssertInside(Box(card), Box(dismiss), id);
                dismiss.onClick.Invoke();
                Assert.That(dismiss.gameObject.activeSelf, Is.False);
                button.onClick.Invoke();
                view.ShowCollapsedHandle();
                yield return Settle();
                Assert.That(dismiss.gameObject.activeSelf, Is.False);
                view.ShowCatalogue(); yield return Settle();
            }
            infos[0].onClick.Invoke();
            Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            yield return Settle();
            Assert.That(view.GetComponentsInChildren<Button>().Any(button => button.name == "CategoryHelpDismiss"), Is.False);
        }

        [UnityTest]
        public IEnumerator FloorRanges_StayInOpenPanelWithStableFaces_AndInstructionIsCentered()
        {
            using var screen = new P8RReferenceLayoutTests.NativeScreenSize();
            var profile = ChromeProfile.All[0]; screen.Resize(profile.Pixels);
            yield return Load(profile);
            var controller = Component<DecorationModeController>();
            controller.EnterDecorationMode();
            Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            var view = Component<DecorationCatalogueView>(); view.ShowCatalogue();
            yield return Settle();
            var ranges = Component<DecorationFloorRangeView>();
            var buttons = ranges.GetComponentsInChildren<Button>();
            var widths = buttons.Select(button => Box(button.image).width).ToArray();
            Assert.That(widths[0], Is.EqualTo(widths[1]).Within(.1f));
            foreach (var scope in new[] { SurfaceEditScope.SingleGridFloor, SurfaceEditScope.WholeRoomFloor })
            {
                Assert.That(controller.TrySelectFloorRange(scope), Is.True);
                yield return Settle();
                for (var i = 0; i < buttons.Length; i++)
                {
                    Assert.That(Box(buttons[i].image).width, Is.EqualTo(widths[i]).Within(.1f));
                    Assert.That(buttons[i].GetComponentInChildren<TMP_Text>().fontStyle.HasFlag(FontStyles.Bold), Is.True);
                }
            }
            controller.TrySelectFloorRange(SurfaceEditScope.SingleGridFloor);
            yield return Settle();
            var instruction = Component<DecorationActionBarView>().VisibleInstructionRect;
            Assert.That(instruction, Is.Not.Null);
            var text = instruction.GetComponentInChildren<TMP_Text>();
            Assert.That(text.alignment, Is.EqualTo(TextAlignmentOptions.MidlineGeoAligned));
            Assert.That(Box(text).center.x, Is.EqualTo(Box(instruction).center.x).Within(.1f));
            Assert.That(instruction.GetComponent<Image>().color.a, Is.EqualTo(.75f));
            foreach (var state in new[] { DecorationSheetState.TabsOnly, DecorationSheetState.CompactPreview, DecorationSheetState.Expanded })
            {
                view.SetSheetState(state, state == DecorationSheetState.CompactPreview);
                yield return Settle();
                var group = ranges.GetComponent<CanvasGroup>();
                Assert.That(group.alpha, Is.EqualTo(state == DecorationSheetState.Expanded ? 1 : 0));
                Assert.That(group.blocksRaycasts, Is.EqualTo(state == DecorationSheetState.Expanded));
            }
        }

        [UnityTest]
        public IEnumerator CatalogueToggle_PreservesTabWidthsAndUniformGapsAcrossFoldStates()
        {
            using var screen = new P8RReferenceLayoutTests.NativeScreenSize();
            foreach (var profile in ChromeProfile.All)
            {
                screen.Resize(profile.Pixels);
                yield return Load(profile);
                var controller = Component<DecorationModeController>();
                controller.EnterDecorationMode();
                var view = Component<DecorationCatalogueView>();
                view.ShowCatalogue();
                yield return new WaitForSecondsRealtime(.3f);
                var tabs = Component<DecorationModeTabsView>().GetComponentsInChildren<Button>()
                    .OrderBy(button => Box(button).xMin).ToArray();
                var toggle = (Button)typeof(DecorationCatalogueView).GetField("collapseButton",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                var density = P8RMobileMetrics.For(view).PixelsPerLogicalUnit;
                var before = tabs.Select(button => Box(button)).ToArray();
                var mode = Component<TimeControlPanel>().transform.Find("P8RModeBadge").GetComponentInChildren<TMP_Text>();
                Assert.That(mode.fontStyle.HasFlag(FontStyles.Bold), Is.True);
                foreach (var state in new[] { DecorationSheetState.TabsOnly, DecorationSheetState.CompactPreview, DecorationSheetState.Expanded })
                {
                    view.SetSheetState(state, state == DecorationSheetState.CompactPreview);
                    yield return new WaitForSecondsRealtime(.3f);
                    Assert.That(toggle.gameObject.activeInHierarchy, Is.True, profile.Name);
                    Assert.That(view.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "Add Another"), Is.False);
                    for (var i = 0; i < tabs.Length; i++)
                    {
                        Assert.That(Box(tabs[i]).xMin, Is.EqualTo(before[i].xMin).Within(1), profile.Name);
                        Assert.That(Box(tabs[i]).width, Is.EqualTo(before[i].width).Within(1), profile.Name);
                        if (i > 0) Assert.That((Box(tabs[i].image).xMin - Box(tabs[i - 1].image).xMax) / density,
                            Is.EqualTo(4).Within(.2f), profile.Name + " tab gap");
                    }
                    Assert.That((Box(toggle.image).xMin - Box(tabs.Last().image).xMax) / density,
                        Is.EqualTo(4).Within(.2f), profile.Name + " toggle gap");
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(toggle.transform.Find("Icon").localEulerAngles.z,
                        state == DecorationSheetState.Expanded ? 0 : 180)), Is.LessThan(.1f));
                    AssertNoOverlap(tabs.Concat(new[] { toggle }).ToArray());
                }
                toggle.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(view.IsCollapsed, Is.True);
                toggle.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(view.IsCollapsed, Is.False);
                yield return UnloadOwnedScene();
            }
        }

        [UnityTest]
        public IEnumerator TwoTimeControls_KeepSeparateAccessibleTouchRoots()
        {
            using var screen = new P8RReferenceLayoutTests.NativeScreenSize();
            var profile = ChromeProfile.All[0];
            screen.Resize(profile.Pixels);
            yield return Load(profile);
            var hud = Component<TimeControlPanel>();
            var buttons = TimeButtons(hud);
            var density = P8RMobileMetrics.For(hud).PixelsPerLogicalUnit;
            AssertTargets(buttons, density, profile.SafePixels);
            AssertNoOverlap(buttons);
            AssertTimeStrip(hud, buttons, density, profile.Name);
        }

        [UnityTest]
        public IEnumerator TwoTimeControls_StateRefreshesReuseFacesAndPreservePauseSpeedSemantics()
        {
            using var screen = new P8RReferenceLayoutTests.NativeScreenSize();
            var profile = ChromeProfile.All[0];
            screen.Resize(profile.Pixels);
            yield return Load(profile);
            var hud = Component<TimeControlPanel>();
            var buttons = TimeButtons(hud);
            var pause = buttons[0]; var speed = buttons[1];
            var faces = buttons.Select(button => button.image).ToArray();
            var time = Component<AnimalCafe.Core.Time.GameTimeService>();
            var density = P8RMobileMetrics.For(hud).PixelsPerLogicalUnit;
            time.SetNormal();
            AssertTimeStrip(hud, buttons, density, profile.Name + " normal");
            Assert.That(buttons[0].image.sprite.name, Is.EqualTo("tab_selected"));
            speed.onClick.Invoke();
            Assert.That(time.CurrentSpeed, Is.EqualTo(AnimalCafe.Core.Time.GameSpeed.Fast));
            Assert.That(buttons[1].transform.Find("Icon").GetComponent<Image>().sprite.name, Is.EqualTo("resume_cocoa"));
            Assert.That(speed.image.sprite.name, Is.EqualTo("tab_selected"));
            speed.onClick.Invoke();
            Assert.That(time.CurrentSpeed, Is.EqualTo(AnimalCafe.Core.Time.GameSpeed.Paused));
            Assert.That(pause.transform.Find("Icon").GetComponent<Image>().sprite.name, Is.EqualTo("pause_cocoa"));
            Assert.That(speed.interactable, Is.True);
            Assert.That(buttons[1].transform.Find("Icon").GetComponent<Image>().sprite.name, Is.EqualTo("resume_cocoa"));
            hud.SetDecorationPauseLock(true);
            Assert.That(buttons.All(button => !button.interactable), Is.True);
            Assert.That(buttons.All(button => button.image.sprite.name == "tab_unavailable"), Is.True);
            Assert.That(pause.transform.Find("Icon").GetComponent<Image>().sprite.name, Is.EqualTo("lock_muted"));
            hud.SetDecorationPauseLock(false);
            hud.enabled = false; hud.enabled = true;
            Assert.That(pause.interactable, Is.True);
            Assert.That(speed.interactable, Is.True, "Unlock preserves the still-paused service.");
            pause.onClick.Invoke();
            Assert.That(time.CurrentSpeed, Is.EqualTo(AnimalCafe.Core.Time.GameSpeed.Normal));
            Assert.That(buttons.Select(button => button.image), Is.EqualTo(faces));
            AssertTimeStrip(hud, buttons, density, profile.Name + " resumed");
        }

        [UnityTest]
        public IEnumerator TwoTimeControls_ResizeReusesStateFacesAndHitTargets()
        {
            using var screen = new P8RReferenceLayoutTests.NativeScreenSize();
            var portrait = ChromeProfile.All[0]; var landscape = ChromeProfile.All[2];
            screen.Resize(portrait.Pixels);
            yield return Load(portrait);
            var hud = Component<TimeControlPanel>();
            var buttons = TimeButtons(hud);
            buttons[1].onClick.Invoke();
            var faces = buttons.Select(button => button.image).ToArray();
            var strip = hud.transform.Find("P8RTimeStrip");
            foreach (var profile in new[] { landscape, portrait })
            {
                screen.Resize(profile.Pixels);
                P8RMobileMetrics.EditorLogicalViewportOverride = profile.Logical;
                foreach (var area in Components<SafeAreaContainer>())
                {
                    area.AutoApplyRuntimeSafeArea = false;
                    area.ApplySafeArea(profile.SafePixels, profile.Pixels);
                }
                yield return Settle();
                var density = P8RMobileMetrics.For(hud).PixelsPerLogicalUnit;
                AssertTargets(buttons, density, profile.SafePixels);
                AssertNoOverlap(buttons);
                AssertTimeStrip(hud, buttons, density, profile.Name);
                Assert.That(buttons[1].transform.Find("Icon").GetComponent<Image>().sprite.name, Is.EqualTo("resume_cocoa"));
                Assert.That(hud.transform.Find("P8RTimeStrip"), Is.SameAs(strip));
                Assert.That(buttons.Select(button => button.image), Is.EqualTo(faces));
            }
        }
        [UnityTest]
        public IEnumerator CatalogueTabs_RestoreAdjacentEqualWidthFaces_AfterModeSwitch()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                var profile = ChromeProfile.All[0];
                screen.Resize(profile.Pixels);
                yield return Load(profile);
                var controller = Component<DecorationModeController>();
                controller.EnterDecorationMode();
                yield return Settle();
                foreach (var mode in new[] { DecorationModeKind.Furniture, DecorationModeKind.Floor,
                    DecorationModeKind.Wall, DecorationModeKind.WallDecor })
                {
                    Assert.That(controller.TryChangeMode(mode), Is.True);
                    yield return Settle();
                    var tabs = Component<DecorationModeTabsView>();
                    var buttons = tabs.GetComponentsInChildren<Button>()
                        .OrderBy(button => Box(button).xMin).ToArray();
                    var density = P8RMobileMetrics.For(tabs).PixelsPerLogicalUnit;
                    Assert.That(buttons.Length, Is.EqualTo(4));
                    AssertTargets(buttons, density, profile.SafePixels);
                    AssertNoOverlap(buttons);
                    AssertVisibleGaps(buttons, density, 6.2f);
                    foreach (var button in buttons)
                    {
                        AssertTabFace(button, density);
                        Assert.That(Box(button.image).width, Is.EqualTo(Box(buttons[0].image).width).Within(1f));
                        Assert.That(button.transform.Find("Label").gameObject.activeSelf, Is.False);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator FloatingActions_SmallerFacesAndTighterGroup_KeepSeparateTouchRoots()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                var profile = ChromeProfile.All[0];
                screen.Resize(profile.Pixels);
                yield return Load(profile);
                Component<DecorationModeController>().EnterDecorationMode();
                yield return Settle();
                var tile = Component<DecorationCatalogueView>().GetComponentsInChildren<DecorationCatalogueTileView>(true)
                    .First(candidate => candidate.ItemId == "furniture.counter.module.01" && candidate.gameObject.activeInHierarchy);
                tile.GetComponent<Button>().onClick.Invoke();
                yield return Settle();
                var action = Component<DecorationActionBarView>();
                var buttons = new[] { "storeButton", "cancelButton", "rotateButton", "confirmButton" }
                    .Select(name => Field<Button>(action, name))
                    .Where(button => button != null && button.gameObject.activeInHierarchy)
                    .OrderBy(button => Box(button).xMin).ToArray();
                var density = P8RMobileMetrics.For(action).PixelsPerLogicalUnit;
                AssertTargets(buttons, density, profile.SafePixels, floating: true);
                AssertNoOverlap(buttons);
                foreach (var button in buttons) AssertFace(button, 30f, density);
                AssertVisibleGaps(buttons, density, 9.6f);
            }
        }

        [UnityTest]
        public IEnumerator MainCafe_FourViewportsAndInjectedSafeAreas_KeepCompactChromeReadableAndTouchable()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                foreach (var profile in ChromeProfile.All)
                {
                    TestContext.WriteLine("Compact chrome profile: " + profile.Name);
                    screen.Resize(profile.Pixels);
                    yield return Load(profile);

                    var hud = Component<TimeControlPanel>();
                    var badge = hud.transform.Find("P8RModeBadge").GetComponentInChildren<TMP_Text>();
                    var decor = hud.transform.Find("DecorationModeButton/Label").GetComponent<TMP_Text>();
                    AssertFont(badge, 14f, profile.Density, "Normal badge");
                    AssertFont(decor, 14f, profile.Density, "Decor button");
                    var summary = Component<ValidationMessageView>().transform.Find("NormalReadinessSummary").GetComponent<TMP_Text>();
                    summary.ForceMeshUpdate();
                    Assert.That(summary.textInfo.lineCount, Is.EqualTo(1), profile.Name);
                    Assert.That(summary.isTextOverflowing, Is.False, profile.Name);
                    Assert.That(summary.fontSize, Is.LessThanOrEqualTo(P8RMobileMetrics.For(summary).Units(12) + .01f));
                    AssertTextFits(summary, profile.Name);
                    Component<DecorationModeController>().EnterDecorationMode();
                    yield return Settle();
                    AssertHudAndReadiness(profile);
                    yield return AssertFloatingActionsAndInstruction(profile);
                    yield return AssertModalTypography(profile);
                }
            }
        }

        private IEnumerator Load(ChromeProfile profile)
        {
            yield return UnloadOwnedScene();
            P8RMobileMetrics.EditorLogicalViewportOverride = profile.Logical;
            ownedScene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/MainCafe.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            yield return null;
            Assert.That(new Vector2(Screen.width, Screen.height), Is.EqualTo(profile.Pixels), profile.Name);

            foreach (var area in Components<SafeAreaContainer>())
            {
                area.AutoApplyRuntimeSafeArea = false;
                area.ApplySafeArea(profile.SafePixels, profile.Pixels);
            }

            yield return Settle();
        }

        private IEnumerator UnloadOwnedScene()
        {
            if (!ownedScene.IsValid() || !ownedScene.isLoaded)
            {
                ownedScene = default;
                yield break;
            }

            var assets = Phase8SceneInputTestCleanup.CaptureAssets(ownedScene);
            foreach (var controller in Components<DecorationModeController>())
            {
                controller.enabled = false;
            }

            SceneManager.SetActiveScene(SceneManager.CreateScene("P8RCompactChromeCleanup"));
            var unload = SceneManager.UnloadSceneAsync(ownedScene);
            while (unload != null && !unload.isDone)
            {
                yield return null;
            }

            Phase8SceneInputTestCleanup.DisposeReleasedAssets(assets);
            ownedScene = default;
        }

        private void AssertHudAndReadiness(ChromeProfile profile)
        {
            var hud = Component<TimeControlPanel>();
            var pause = Field<Button>(hud, "pauseButton");
            var normal = Field<Button>(hud, "normalButton");
            var fast = Field<Button>(hud, "fastButton");
            var mode = hud.transform.Find("DecorationModeButton").GetComponent<Button>();
            var density = P8RMobileMetrics.For(hud).PixelsPerLogicalUnit;
            var timeButtons = new[] { normal, fast };

            AssertTargets(timeButtons.Append(mode).ToArray(), density, profile.SafePixels);
            AssertNoOverlap(timeButtons.Append(mode).ToArray());
            AssertTimeStrip(hud, timeButtons, density, profile.Name);
            Assert.That(Box(mode.image).height / density, Is.EqualTo(48f).Within(.2f));
            AssertInk(normal.transform.Find("Icon").GetComponent<Image>(), 20f, density);
            AssertInk(mode.transform.Find("Icon").GetComponent<Image>(), 20f, density);
            var normalInk = P8RCompleteFlowTests.MeasuredInk(normal.transform.Find("Icon").GetComponent<Image>());
            var fastInk = P8RCompleteFlowTests.MeasuredInk(fast.transform.Find("Icon").GetComponent<Image>());
            Assert.That(fastInk.height, Is.EqualTo(normalInk.height).Within(.2f * density));
            Assert.That(fastInk.width / density, Is.LessThanOrEqualTo(36.1f));
            var badge = hud.transform.Find("P8RModeBadge").GetComponentInChildren<TMP_Text>(true);
            AssertFont(badge, 14f, density, profile.Name + " mode badge");
            var badgeBox = Box(badge.transform.parent);
            if (badgeBox.xMax < Box(normal).xMin)
            {
                Assert.That(badgeBox.center.y, Is.EqualTo(Box(normal).center.y).Within(1f),
                    profile.Name + ": wide badge and time roots share one visual row.");
            }
            else
            {
                Assert.That(badgeBox.yMin, Is.GreaterThanOrEqualTo(Box(normal).yMax - 1f),
                    profile.Name + ": narrow badge remains above the time roots.");
            }

            var readiness = Component<ValidationMessageView>();
            Canvas.ForceUpdateCanvases();
            P8RCompleteFlowTests.AssertChecklist(readiness);
            var card = Box(readiness);
            AssertInside(card, profile.SafePixels, profile.Name + " readiness");
            Assert.That(card.xMin, Is.EqualTo(Box(hud.transform.Find("P8RModeBadge")).xMin).Within(1f));
            foreach (var button in timeButtons.Append(mode))
                Assert.That(card.Overlaps(Box(button)), Is.False, "Checklist overlaps HUD " + button.name);
            foreach (var label in readiness.GetComponentsInChildren<TMP_Text>().Where(label => label.enabled))
                AssertFont(label, 12f, density, profile.Name + " readiness body");
        }

        private IEnumerator AssertFloatingActionsAndInstruction(ChromeProfile profile)
        {
            var controller = Component<DecorationModeController>();
            controller.EnterDecorationMode();
            yield return Settle();
            var hud = Component<TimeControlPanel>();
            var pause = Field<Button>(hud, "pauseButton");
            var normal = Field<Button>(hud, "normalButton");
            var fast = Field<Button>(hud, "fastButton");
            var timeButtons = new[] { normal, fast };
            Assert.That(timeButtons.All(button => !button.interactable), Is.True);
            var catalogue = Component<DecorationCatalogueView>();
            var tabs = Component<DecorationModeTabsView>();
            var furnitureTab = Field<Button>(tabs, "furnitureButton");
            var pickup = Field<Button>(catalogue, "pickUpPointButton");
            AssertTimeStrip(hud, timeButtons, P8RMobileMetrics.For(hud).PixelsPerLogicalUnit, profile.Name);
            AssertFaceLifecycle(furnitureTab, profile.Name);
            AssertFaceLifecycle(pickup, profile.Name);

            hud.enabled = false;
            hud.enabled = true;
            Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True, profile.Name);
            Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True, profile.Name);
            tabs.enabled = false;
            tabs.enabled = true;
            catalogue.enabled = false;
            catalogue.enabled = true;
            yield return Settle();

            AssertTimeStrip(hud, timeButtons, P8RMobileMetrics.For(hud).PixelsPerLogicalUnit,
                profile.Name + " re-enabled HUD");
            AssertFaceLifecycle(furnitureTab, profile.Name + " re-enabled colored tab");
            AssertFaceLifecycle(pickup, profile.Name + " re-enabled TextButton");
            Assert.That(normal.transform.Find("Label").gameObject.activeSelf, Is.False);
            AssertTabFace(furnitureTab, P8RMobileMetrics.For(tabs).PixelsPerLogicalUnit);
            var pickupFace = Box(pickup.image);
            var pickupDensity = P8RMobileMetrics.For(catalogue).PixelsPerLogicalUnit;
            Assert.That(pickupFace.height / pickupDensity, Is.EqualTo(34f).Within(.2f), profile.Name);
            Assert.That(pickupFace.width, Is.InRange(34f * pickupDensity, Box(pickup).width), profile.Name);
            AssertInside(pickupFace, Box(pickup), profile.Name + " adaptive TextButton face");

            var tile = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .First(candidate => candidate.ItemId == "furniture.counter.module.01"
                    && candidate.gameObject.activeInHierarchy);
            tile.GetComponent<Button>().onClick.Invoke();
            yield return Settle();

            var action = Component<DecorationActionBarView>();
            var density = P8RMobileMetrics.For(action).PixelsPerLogicalUnit;
            var buttons = new[] { "storeButton", "cancelButton", "rotateButton", "confirmButton" }
                .Select(name => Field<Button>(action, name))
                .Where(button => button != null && button.gameObject.activeInHierarchy)
                .OrderBy(button => Box(button).xMin)
                .ToArray();
            Assert.That(buttons.Length, Is.GreaterThanOrEqualTo(3), profile.Name);
            AssertTargets(buttons, density, profile.SafePixels, floating: true);
            AssertNoOverlap(buttons);
            foreach (var button in buttons)
            {
                AssertFace(button, 30f, density);
                AssertInk(button.transform.Find("Icon").GetComponent<Image>(), 18f, density);
                Assert.That(Box(button).Overlaps(Box(Component<ValidationMessageView>())), Is.False,
                    profile.Name + ": floating action overlaps readiness " + button.name);
                foreach (var fixedButton in Component<TimeControlPanel>().GetComponentsInChildren<Button>())
                {
                    Assert.That(Box(button).Overlaps(Box(fixedButton)), Is.False,
                        profile.Name + ": floating action overlaps HUD " + fixedButton.name);
                }
            }

            Field<Button>(action, "cancelButton").onClick.Invoke();
            yield return Settle();
            Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True, profile.Name);
            yield return Settle();
            var instruction = action.VisibleInstructionRect;
            Assert.That(instruction, Is.Not.Null, profile.Name + ": wall mode needs a visible target instruction.");
            AssertInside(Box(instruction), profile.SafePixels, profile.Name + " instruction");
            var instructionCopy = Field<TMP_Text>(action, "feedbackLabel");
            AssertFont(instructionCopy, 14f, density, profile.Name + " instruction body");
            Assert.That(Box(instruction).height / density, Is.LessThanOrEqualTo(44.1f),
                profile.Name + ": one-line instruction chrome is compact and content-measured.");
            var stateShape = Field<GameObject>(action, "feedbackStateShape");
            Assert.That(Mathf.Max(Box(stateShape.transform).width, Box(stateShape.transform).height) / density,
                Is.EqualTo(20f).Within(.2f), profile.Name + ": instruction status mark");
            Assert.That(Box(instruction).Overlaps(Box(Component<ValidationMessageView>())), Is.False, profile.Name);
            foreach (var graphic in instruction.GetComponentsInChildren<Graphic>(true))
            {
                Assert.That(graphic.raycastTarget, Is.False, profile.Name + ": instruction cannot steal camera gestures.");
            }
        }

        private IEnumerator AssertModalTypography(ChromeProfile profile)
        {
            var modal = Component<DecorationExitModalView>();
            modal.Show();
            yield return Settle();
            var density = P8RMobileMetrics.For(modal).PixelsPerLogicalUnit;
            var card = Field<RectTransform>(modal, "modalCard");
            var title = Field<TMP_Text>(modal, "titleLabel");
            var body = Field<TMP_Text>(modal, "bodyLabel");
            var buttons = new[]
            {
                Field<Button>(modal, "continueButton"),
                Field<Button>(modal, "discardButton")
            };
            AssertInside(Box(card), profile.SafePixels, profile.Name + " modal card");
            AssertTargets(buttons, density, profile.SafePixels);
            AssertNoOverlap(buttons);
            AssertFont(title, 16f, density, profile.Name + " modal title");
            AssertFont(body, 14f, density, profile.Name + " modal body");
            AssertTextFits(title, profile.Name);
            AssertTextFits(body, profile.Name);
            foreach (var button in buttons)
            {
                var label = button.transform.Find("Label").GetComponent<TMP_Text>();
                AssertFont(label, 14f, density, profile.Name + " modal button");
                AssertTextFits(label, profile.Name);
                Assert.That(Box(body).Overlaps(Box(button)), Is.False, profile.Name);
            }
            modal.Close();
            yield return Settle();
        }

        private T Component<T>() where T : Component => Components<T>().Single();

        private T[] Components<T>() where T : Component => ownedScene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static T Field<T>(object owner, string name) => (T)owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private static IEnumerator Settle()
        {
            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();
        }

        private static void AssertFace(Button button, float expectedLogicalExtent, float density)
        {
            Assert.That(button.image, Is.Not.Null, button.name + " face");
            AssertFaceLifecycle(button, button.name);
            var face = Box(button.image);
            Assert.That(face.width / density, Is.EqualTo(expectedLogicalExtent).Within(.2f), button.name + " face width");
            Assert.That(face.height / density, Is.EqualTo(expectedLogicalExtent).Within(.2f), button.name + " face height");
            AssertInside(face, Box(button), button.name + " face inside touch root");
        }

        private static Button[] TimeButtons(TimeControlPanel hud) => new[] { "normalButton", "fastButton" }
            .Select(name => Field<Button>(hud, name)).ToArray();
        private static TMP_Text SpeedLabel(TimeControlPanel hud) => Field<Button>(hud, "normalButton")
            .transform.Find("Label").GetComponent<TMP_Text>();

        private static void AssertTimeStrip(TimeControlPanel hud, Button[] buttons, float density, string profile)
        {
            var containers = hud.GetComponentsInChildren<RectTransform>(true)
                .Where(rect => rect.name == "P8RTimeStrip").ToArray();
            Assert.That(containers.Length, Is.EqualTo(1), profile + ": reusable time container");
            Assert.That(buttons.Length, Is.EqualTo(2));
            Assert.That(Field<Button>(hud, "pauseButton").gameObject.activeInHierarchy, Is.False);
            Assert.That(containers[0].GetComponentsInChildren<Button>(), Has.Length.EqualTo(2));
            Assert.That(containers[0].gameObject.layer, Is.EqualTo(hud.gameObject.layer));
            AssertNoOverlap(buttons);
            foreach (var button in buttons)
            {
                Assert.That(button.gameObject.activeInHierarchy, Is.True);
                var rootImage = button.GetComponent<Image>();
                Assert.That(rootImage.raycastTarget, Is.True);
                Assert.That(rootImage.color.a, Is.Zero.Within(.001f));
                Assert.That(button.image, Is.Not.SameAs(rootImage));
                Assert.That(button.image.raycastTarget, Is.False);
                Assert.That(button.image.sprite.name, Is.EqualTo("tab_idle").Or.EqualTo("tab_selected").Or.EqualTo("tab_unavailable"),
                    profile + ": retain the original time-control skin family");
                Assert.That(button.GetComponentsInChildren<Image>(true).Count(image => image.name == "P8RFace"), Is.EqualTo(1));
                AssertInside(Box(button.image), Box(button), profile + ": face inside touch target");
                Assert.That(Box(button).width / density, Is.GreaterThanOrEqualTo(47.9f));
                Assert.That(Box(button).height / density, Is.GreaterThanOrEqualTo(47.9f));
            }
            Assert.That(SpeedLabel(hud).gameObject.activeInHierarchy, Is.False);
            var fastInk = P8RCompleteFlowTests.MeasuredInk(buttons[1].transform.Find("Icon").GetComponent<Image>());
            Assert.That(fastInk.height / density, Is.EqualTo(20f).Within(.25f), "Fast keeps the single triangle's visible height.");
            Assert.That(fastInk.width / density, Is.LessThanOrEqualTo(36.1f));
            Assert.That(buttons[1].transform.Find("Label").gameObject.activeSelf, Is.False);
            AssertInk(buttons[0].transform.Find("Icon").GetComponent<Image>(), 20, density);
        }
        private static void AssertTabFace(Button button, float density)
        {
            AssertFaceLifecycle(button, button.name);
            Assert.That(Box(button.image).width, Is.EqualTo(Box(button).width).Within(1f),
                button.name + ": colored tab face fills its own equal-width cell.");
            Assert.That(Box(button.image).height / density, Is.EqualTo(34f).Within(.2f));
            AssertInside(Box(button.image), Box(button), button.name + " tab face");
        }

        private static void AssertVisibleGaps(Button[] buttons, float density, float maximum)
        {
            for (var i = 1; i < buttons.Length; i++)
            {
                var gap = (Box(buttons[i].image).xMin - Box(buttons[i - 1].image).xMax) / density;
                Assert.That(gap, Is.InRange(-.1f, maximum),
                    buttons[i].name + ": visible face gap, not merely layout spacing.");
            }
        }

        private static void AssertFaceLifecycle(Button button, string profile)
        {
            var rootImage = button.GetComponent<Image>();
            Assert.That(rootImage, Is.Not.Null, profile + ": " + button.name + " root Image");
            Assert.That(rootImage.color.a, Is.Zero.Within(.001f),
                profile + ": " + button.name + " root must stay visually transparent.");
            Assert.That(rootImage.raycastTarget, Is.True,
                profile + ": " + button.name + " root must retain the touch raycast.");
            Assert.That(button.image, Is.Not.Null, profile + ": " + button.name + " face");
            Assert.That(button.image, Is.Not.SameAs(rootImage),
                profile + ": " + button.name + " face must remain separate from its root.");
            Assert.That(button.image.raycastTarget, Is.False,
                profile + ": " + button.name + " face cannot steal the root raycast.");
            var generatedFaces = button.transform.Cast<Transform>()
                .Count(child => child.name == "P8RFace");
            Assert.That(generatedFaces, Is.LessThanOrEqualTo(1),
                profile + ": " + button.name + " must not accumulate P8RFace children.");
            if (button.image.name == "P8RFace")
            {
                Assert.That(generatedFaces, Is.EqualTo(1),
                    profile + ": " + button.name + " generated face must be the one reused child.");
            }
        }

        private static void AssertInk(Image icon, float expectedLogicalExtent, float density)
        {
            Assert.That(icon, Is.Not.Null);
            var ink = P8RCompleteFlowTests.MeasuredInk(icon);
            Assert.That(Mathf.Max(ink.width, ink.height) / density,
                Is.EqualTo(expectedLogicalExtent).Within(.25f), icon.name + " visible ink");
        }

        private static void AssertFont(TMP_Text text, float expectedLogicalSize, float density, string label)
        {
            var rendered = text.fontSize * text.GetComponentInParent<Canvas>().rootCanvas.scaleFactor / density;
            Assert.That(rendered, Is.EqualTo(expectedLogicalSize).Within(.2f), label);
        }

        private static void AssertTextFits(TMP_Text text, string profile)
        {
            var preferred = text.GetPreferredValues(text.text, text.rectTransform.rect.width, Mathf.Infinity);
            Assert.That(preferred.y, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1f),
                profile + ": " + text.name + " clips vertically.");
            text.ForceMeshUpdate();
            Assert.That(text.isTextOverflowing, Is.False, profile + ": " + text.name + " overflows.");
        }

        private static void AssertTargets(Button[] buttons, float density, Rect safe, bool floating = false)
        {
            foreach (var button in buttons)
            {
                var root = Box(button);
                if (floating)
                    Assert.That(root.width / density, Is.EqualTo(44f).Within(.1f), button.name + " floating target width");
                else
                    Assert.That(root.width / density, Is.GreaterThanOrEqualTo(47.9f), button.name + " target width");
                Assert.That(root.height / density, Is.GreaterThanOrEqualTo(47.9f), button.name + " target height");
                AssertInside(root, safe, button.name + " target inside safe area");
                var hitGraphic = button.GetComponent<Graphic>();
                Assert.That(hitGraphic, Is.Not.Null, button.name + " hit graphic");
                Assert.That(hitGraphic.raycastTarget, Is.True, button.name + " hit root remains raycastable");
            }
        }

        private static void AssertNoOverlap(Button[] buttons)
        {
            for (var i = 0; i < buttons.Length; i++)
            {
                for (var j = 0; j < i; j++)
                {
                    Assert.That(Box(buttons[i]).Overlaps(Box(buttons[j])), Is.False,
                        buttons[i].name + " overlaps " + buttons[j].name + "; x overlap pixels="
                        + (Mathf.Min(Box(buttons[i]).xMax, Box(buttons[j]).xMax)
                            - Mathf.Max(Box(buttons[i]).xMin, Box(buttons[j]).xMin)).ToString("R"));
                }
            }
        }

        private static Rect Box(Component component) => Box(component.transform);

        private static Rect Box(Transform transform)
        {
            var rect = (RectTransform)transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var canvas = transform.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
                points.Max(point => point.x), points.Max(point => point.y));
        }

        private static void AssertInside(Rect inner, Rect outer, string label)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - 1f), label + " left");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + 1f), label + " right");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - 1f), label + " bottom");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + 1f), label + " top");
        }

        private readonly struct ChromeProfile
        {
            public ChromeProfile(string name, Vector2 logical, float density, Rect safeLogical)
            {
                Name = name;
                Logical = logical;
                Density = density;
                Pixels = logical * density;
                SafePixels = new Rect(safeLogical.position * density, safeLogical.size * density);
            }

            public string Name { get; }
            public Vector2 Logical { get; }
            public float Density { get; }
            public Vector2 Pixels { get; }
            public Rect SafePixels { get; }

            public static readonly ChromeProfile[] All =
            {
                new ChromeProfile("phone 360x640", new Vector2(360, 640), 3f,
                    new Rect(18, 20, 324, 576)),
                new ChromeProfile("small phone 320x569", new Vector2(320, 569), 3f,
                    new Rect(20, 20, 288, 505)),
                new ChromeProfile("landscape 800x360", new Vector2(800, 360), 3f,
                    new Rect(44, 16, 712, 328)),
                new ChromeProfile("tablet 768x1024", new Vector2(768, 1024), 2f,
                    new Rect(24, 24, 720, 952))
            };
        }
    }
}
#endif

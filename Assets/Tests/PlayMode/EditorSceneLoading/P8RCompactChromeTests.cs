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
        public IEnumerator TimeButtons_FormOneContinuousStrip_WithoutOverlappingTouchRoots()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                var profile = ChromeProfile.All[0];
                screen.Resize(profile.Pixels);
                yield return Load(profile);
                var hud = Component<TimeControlPanel>();
                var buttons = new[] { "pauseButton", "normalButton", "fastButton" }
                    .Select(name => Field<Button>(hud, name)).ToArray();
                var density = P8RMobileMetrics.For(hud).PixelsPerLogicalUnit;
                AssertTargets(buttons, density, profile.SafePixels);
                AssertNoOverlap(buttons);
                AssertTimeStrip(hud, buttons, density, profile.Name);
            }
        }

        [UnityTest]
        public IEnumerator TimeStrip_StateRefreshesReusePresentation_AndPreserveIconStates()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                var profile = ChromeProfile.All[0];
                screen.Resize(profile.Pixels);
                yield return Load(profile);
                var hud = Component<TimeControlPanel>();
                var pause = Field<Button>(hud, "pauseButton");
                var normal = Field<Button>(hud, "normalButton");
                var fast = Field<Button>(hud, "fastButton");
                var buttons = new[] { pause, normal, fast };
                var density = P8RMobileMetrics.For(hud).PixelsPerLogicalUnit;

                normal.onClick.Invoke();
                yield return null;
                AssertTimeStrip(hud, buttons, density, profile.Name + " normal");
                Assert.That(normal.image.sprite.name, Is.EqualTo("tab_selected"));
                Assert.That(pause.image.sprite.name, Is.EqualTo("tab_idle"));
                Assert.That(fast.image.sprite.name, Is.EqualTo("tab_idle"));

                fast.onClick.Invoke();
                yield return null;
                AssertTimeStrip(hud, buttons, density, profile.Name + " fast");
                Assert.That(fast.image.sprite.name, Is.EqualTo("tab_selected"));

                pause.onClick.Invoke();
                yield return null;
                AssertTimeStrip(hud, buttons, density, profile.Name + " paused");
                Assert.That(pause.image.sprite.name, Is.EqualTo("tab_selected"));
                Assert.That(pause.transform.Find("Icon").GetComponent<Image>().sprite.name,
                    Is.EqualTo("resume_cocoa"));

                hud.SetDecorationPauseLock(true);
                Canvas.ForceUpdateCanvases();
                AssertTimeStrip(hud, buttons, density, profile.Name + " locked");
                Assert.That(buttons.All(button => !button.interactable), Is.True);
                Assert.That(buttons.All(button => button.image.sprite.name == "tab_unavailable"), Is.True);
                Assert.That(pause.transform.Find("Icon").GetComponent<Image>().sprite.name,
                    Is.EqualTo("lock_muted"));

                hud.SetDecorationPauseLock(false);
                hud.enabled = false;
                hud.enabled = true;
                Canvas.ForceUpdateCanvases();
                AssertTimeStrip(hud, buttons, density, profile.Name + " re-enabled");
                Assert.That(buttons.All(button => button.interactable), Is.True);
                AssertInk(pause.transform.Find("Icon").GetComponent<Image>(), 18f, density);
                AssertInk(normal.transform.Find("Icon").GetComponent<Image>(), 18f, density);
                AssertInk(fast.transform.Find("Icon").GetComponent<Image>(), 26f, density);
            }
        }

        [UnityTest]
        public IEnumerator TimeStrip_PortraitLandscapePortraitResize_ReusesStateLayoutAndHitTargets()
        {
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                var portrait = ChromeProfile.All[0];
                var landscape = ChromeProfile.All[2];
                screen.Resize(portrait.Pixels);
                yield return Load(portrait);
                var hud = Component<TimeControlPanel>();
                var buttons = new[] { "pauseButton", "normalButton", "fastButton" }
                    .Select(name => Field<Button>(hud, name)).ToArray();
                buttons[2].onClick.Invoke();
                yield return null;
                AssertTimeStrip(hud, buttons, P8RMobileMetrics.For(hud).PixelsPerLogicalUnit,
                    portrait.Name + " before resize");

                var hudId = hud.GetEntityId();
                var stripId = hud.transform.Find("P8RTimeStrip").GetEntityId();
                var separatorIds = hud.transform.Find("P8RTimeStrip").Cast<Transform>()
                    .Where(child => child.name.StartsWith("P8RTimeSeparator"))
                    .OrderBy(child => child.name).Select(child => child.GetEntityId()).ToArray();
                var clipIds = buttons.Select(button =>
                    button.transform.Find("P8RTimeSegmentClip").GetEntityId()).ToArray();
                var faceIds = buttons.Select(button => button.image.GetEntityId()).ToArray();

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
                    Assert.That(hud.GetEntityId(), Is.EqualTo(hudId),
                        profile.Name + ": resize must keep the same HUD instance.");
                    Assert.That(new Vector2(Screen.width, Screen.height), Is.EqualTo(profile.Pixels), profile.Name);
                    var density = P8RMobileMetrics.For(hud).PixelsPerLogicalUnit;
                    AssertTargets(buttons, density, profile.SafePixels);
                    AssertNoOverlap(buttons);
                    AssertTimeStrip(hud, buttons, density, profile.Name + " same-scene resize");
                    Assert.That(buttons[2].image.sprite.name, Is.EqualTo("tab_selected"),
                        profile.Name + ": resize preserves the active 2x state.");
                    Assert.That(hud.transform.Find("P8RTimeStrip").GetEntityId(), Is.EqualTo(stripId), profile.Name);
                    Assert.That(hud.transform.Find("P8RTimeStrip").Cast<Transform>()
                        .Where(child => child.name.StartsWith("P8RTimeSeparator"))
                        .OrderBy(child => child.name).Select(child => child.GetEntityId()),
                        Is.EqualTo(separatorIds), profile.Name + ": separators are reused.");
                    Assert.That(buttons.Select(button =>
                        button.transform.Find("P8RTimeSegmentClip").GetEntityId()),
                        Is.EqualTo(clipIds), profile.Name + ": clips are reused.");
                    Assert.That(buttons.Select(button => button.image.GetEntityId()),
                        Is.EqualTo(faceIds), profile.Name + ": state/press graphics are reused.");
                }
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
                AssertTargets(buttons, density, profile.SafePixels);
                AssertNoOverlap(buttons);
                foreach (var button in buttons) AssertFace(button, 30f, density);
                AssertVisibleGaps(buttons, density, 12.2f);
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
            var timeButtons = new[] { pause, normal, fast };

            AssertTargets(timeButtons.Append(mode).ToArray(), density, profile.SafePixels);
            AssertNoOverlap(timeButtons.Append(mode).ToArray());
            AssertTimeStrip(hud, timeButtons, density, profile.Name);
            AssertFace(mode, 40f, density);
            AssertInk(pause.transform.Find("Icon").GetComponent<Image>(), 18f, density);
            AssertInk(normal.transform.Find("Icon").GetComponent<Image>(), 18f, density);
            AssertInk(fast.transform.Find("Icon").GetComponent<Image>(), 26f, density);
            AssertInk(mode.transform.Find("Icon").GetComponent<Image>(), 20f, density);
            var normalInk = P8RCompleteFlowTests.MeasuredInk(normal.transform.Find("Icon").GetComponent<Image>());
            var fastInk = P8RCompleteFlowTests.MeasuredInk(fast.transform.Find("Icon").GetComponent<Image>());
            Assert.That(fastInk.height / normalInk.height, Is.InRange(.86f, 1.02f),
                profile.Name + ": compact 2x keeps the same optical height as 1x.");

            var badge = hud.transform.Find("P8RModeBadge").GetComponentInChildren<TMP_Text>(true);
            AssertFont(badge, 14f, density, profile.Name + " mode badge");
            var badgeBox = Box(badge.transform.parent);
            if (badgeBox.xMax < Box(pause).xMin)
            {
                Assert.That(badgeBox.center.y, Is.EqualTo(Box(pause).center.y).Within(1f),
                    profile.Name + ": wide badge and time roots share one visual row.");
            }
            else
            {
                Assert.That(badgeBox.yMin, Is.GreaterThanOrEqualTo(Box(pause).yMax - 1f),
                    profile.Name + ": narrow badge remains above the time roots.");
            }

            var readiness = Component<ValidationMessageView>();
            var card = Box(readiness);
            AssertInside(card, profile.SafePixels, profile.Name + " readiness");
            Assert.That(card.height / density, Is.InRange(47.9f, 52.1f),
                profile.Name + ": collapsed readiness is compact without shrinking its disclosure target.");
            foreach (var button in timeButtons.Append(mode))
            {
                Assert.That(card.Overlaps(Box(button)), Is.False,
                    profile.Name + ": readiness overlaps HUD " + button.name);
            }

            var message = Field<TMP_Text>(readiness, "messageLabel");
            var status = Field<Image>(readiness, "statusIcon");
            var disclosure = Field<Button>(readiness, "disclosureButton");
            AssertFont(message, 14f, density, profile.Name + " readiness body");
            AssertInk(status, 20f, density);
            AssertTargets(new[] { disclosure }, density, profile.SafePixels);
            AssertFace(disclosure, 34f, density);
            AssertInk(disclosure.transform.Find("Icon").GetComponent<Image>(), 20f, density);
            AssertFont(Field<TMP_Text>(readiness, "disclosureLabel"), 14f, density,
                profile.Name + " disclosure copy");

            disclosure.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
            Assert.That(readiness.IsDetailsExpanded, Is.True, profile.Name);
            Assert.That(Box(readiness).height / density, Is.LessThanOrEqualTo(112.1f),
                profile.Name + ": expanded diagnostics use a compact scrolling card.");
            AssertTargets(new[] { disclosure }, density, profile.SafePixels);
            AssertInside(Box(readiness), profile.SafePixels, profile.Name + " expanded readiness");
            disclosure.onClick.Invoke();
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
            var timeButtons = new[] { pause, normal, fast };
            var normalInk = P8RCompleteFlowTests.MeasuredInk(normal.transform.Find("Icon").GetComponent<Image>());
            var fastInk = P8RCompleteFlowTests.MeasuredInk(fast.transform.Find("Icon").GetComponent<Image>());
            AssertInk(normal.transform.Find("Icon").GetComponent<Image>(), 18f,
                P8RMobileMetrics.For(hud).PixelsPerLogicalUnit);
            AssertInk(fast.transform.Find("Icon").GetComponent<Image>(), 26f,
                P8RMobileMetrics.For(hud).PixelsPerLogicalUnit);
            Assert.That(fastInk.height / normalInk.height, Is.InRange(.86f, 1.02f),
                profile.Name + ": decoration-lock state refresh preserves 1x/2x optical balance.");
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
            AssertInk(normal.transform.Find("Icon").GetComponent<Image>(), 18f,
                P8RMobileMetrics.For(hud).PixelsPerLogicalUnit);
            AssertInk(fast.transform.Find("Icon").GetComponent<Image>(), 26f,
                P8RMobileMetrics.For(hud).PixelsPerLogicalUnit);
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
            AssertTargets(buttons, density, profile.SafePixels);
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

        private static void AssertTimeStrip(
            TimeControlPanel hud,
            Button[] buttons,
            float density,
            string profile)
        {
            var strips = hud.GetComponentsInChildren<RectTransform>(true)
                .Where(rect => rect.name == "P8RTimeStrip").ToArray();
            Assert.That(strips.Length, Is.EqualTo(1),
                profile + ": time state refresh must reuse one shared strip background.");
            var strip = strips[0];
            var stripImage = strip.GetComponent<Image>();
            Assert.That(stripImage, Is.Not.Null, profile + ": shared time strip background Image");
            Assert.That(stripImage.raycastTarget, Is.False,
                profile + ": shared time strip cannot steal button raycasts.");
            Assert.That(stripImage.sprite, Is.Not.Null);
            Assert.That(stripImage.sprite.name, Is.EqualTo("tab_idle"),
                profile + ": shared time strip keeps the original B palette.");
            Assert.That(strip.gameObject.layer, Is.EqualTo(hud.gameObject.layer),
                profile + ": runtime strip inherits the HUD layer.");

            var ordered = buttons.OrderBy(button => Box(button).xMin).ToArray();
            var stripBox = Box(strip);
            Assert.That(stripBox.width / density, Is.EqualTo(144f + 2f / 64f).Within(.2f),
                profile + ": one strip spans all three 48-unit segments and both subpixel guards.");
            Assert.That(stripBox.height / density, Is.EqualTo(32f).Within(.2f),
                profile + ": shared strip keeps the approved visible height.");
            Assert.That(stripBox.xMin, Is.EqualTo(Box(ordered[0]).xMin).Within(1f), profile);
            Assert.That(stripBox.xMax, Is.EqualTo(Box(ordered[2]).xMax).Within(1f), profile);
            Assert.That(stripBox.center.y, Is.EqualTo(Box(ordered[1]).center.y).Within(1f), profile);

            foreach (var button in ordered)
            {
                var rootImage = button.GetComponent<Image>();
                Assert.That(rootImage, Is.Not.Null, profile + ": time root Image");
                Assert.That(rootImage.color.a, Is.Zero.Within(.001f),
                    profile + ": time root remains visually transparent.");
                Assert.That(rootImage.raycastTarget, Is.True,
                    profile + ": 48-unit time root retains the touch raycast.");
                Assert.That(button.image, Is.Not.Null, profile + ": time state/press graphic");
                Assert.That(button.image, Is.Not.SameAs(rootImage), profile);
                Assert.That(button.image.raycastTarget, Is.False,
                    profile + ": clipped state graphic cannot steal the root raycast.");
                Assert.That(button.image.gameObject.layer, Is.EqualTo(button.gameObject.layer), profile);
                Assert.That(button.GetComponentsInChildren<Image>(true)
                    .Count(image => image.name == "P8RFace"), Is.EqualTo(1),
                    profile + ": refresh must reuse one time state/press graphic.");

                var clips = button.GetComponentsInChildren<RectMask2D>(true)
                    .Where(mask => mask.name == "P8RTimeSegmentClip").ToArray();
                Assert.That(clips.Length, Is.EqualTo(1),
                    profile + ": refresh must reuse one clip for each strip segment.");
                Assert.That(clips[0].gameObject.layer, Is.EqualTo(button.gameObject.layer), profile);
                var icon = button.transform.Find("Icon");
                Assert.That(icon, Is.Not.Null, profile + ": time icon");
                Assert.That(clips[0].transform.GetSiblingIndex(), Is.LessThan(icon.GetSiblingIndex()),
                    profile + ": clipped background must render behind the icon.");
                var clip = Box(clips[0]);
                Assert.That(clip.width, Is.EqualTo(Box(button).width).Within(1f),
                    profile + ": each clip fills its 48-unit segment.");
                Assert.That(clip.height / density, Is.EqualTo(32f).Within(.2f),
                    profile + ": each clip shares the strip height.");
                AssertInside(clip, Box(button), profile + ": segment clip inside touch root");
                AssertInside(clip, stripBox, profile + ": segment clip inside shared strip");

                var face = Box(button.image);
                Assert.That(face.width, Is.EqualTo(stripBox.width).Within(1f),
                    profile + ": each clip samples the same full-width state/press sprite.");
                Assert.That(face.height / density, Is.EqualTo(32f).Within(.2f),
                    profile + ": state/press sprite shares the strip height.");
                Assert.That(face.xMin, Is.EqualTo(stripBox.xMin).Within(1f), profile);
                Assert.That(face.xMax, Is.EqualTo(stripBox.xMax).Within(1f), profile);
                var label = button.transform.Find("Label");
                if (label != null)
                {
                    Assert.That(label.gameObject.activeSelf, Is.False,
                        profile + ": time strip remains icon-only.");
                }
            }

            var separators = strip.GetComponentsInChildren<Image>(true)
                .Where(image => image.name.StartsWith("P8RTimeSeparator"))
                .OrderBy(image => Box(image).center.x).ToArray();
            Assert.That(separators.Length, Is.EqualTo(2),
                profile + ": the three segments need exactly two reusable separators.");
            for (var index = 0; index < separators.Length; index++)
            {
                var separator = separators[index];
                var separatorBox = Box(separator);
                var boundary = (Box(ordered[index]).xMax + Box(ordered[index + 1]).xMin) * .5f;
                Assert.That(separator.raycastTarget, Is.False,
                    profile + ": separator cannot steal button raycasts.");
                Assert.That(separator.gameObject.layer, Is.EqualTo(strip.gameObject.layer), profile);
                Assert.That(separatorBox.width / density, Is.InRange(.5f, 1.1f),
                    profile + ": separator stays visually thin.");
                Assert.That(separatorBox.height, Is.LessThanOrEqualTo(stripBox.height + 1f), profile);
                Assert.That(separatorBox.center.x, Is.EqualTo(boundary).Within(1f),
                    profile + ": separator is centered between adjacent hit roots.");
                Assert.That(separator.color, Is.EqualTo(P8RAppearance.Cocoa),
                    profile + ": separator keeps the original B cocoa palette.");
            }
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

        private static void AssertTargets(Button[] buttons, float density, Rect safe)
        {
            foreach (var button in buttons)
            {
                var root = Box(button);
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

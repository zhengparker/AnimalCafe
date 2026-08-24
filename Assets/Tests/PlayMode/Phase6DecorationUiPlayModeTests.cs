using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Core.Time;
using AnimalCafe.Decoration;
using AnimalCafe.UI;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase6DecorationUiPlayModeTests
    {
        [UnityTest]
        public IEnumerator GameTimeStatusIndicator_PauseStopsAndFastRotatesFasterThanNormal()
        {
            var root = UiObject("GameTimeStatusIndicator");
            try
            {
                var visual = UiObject("RotatingVisual", root.transform)
                    .GetComponent<RectTransform>();
                var gameTime = new FakeGameTimeService();
                var indicator = root.AddComponent<GameTimeStatusIndicator>();
                indicator.Configure(gameTime, visual);

                var initialAngle = visual.localEulerAngles.z;
                indicator.Refresh(1f);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(
                    initialAngle, visual.localEulerAngles.z)), Is.LessThan(0.001f),
                    "Paused indicator must remain still.");

                gameTime.TrySetSpeed(GameSpeed.Normal);
                var beforeNormal = visual.localEulerAngles.z;
                indicator.Refresh(1f);
                var normalDelta = Mathf.Abs(Mathf.DeltaAngle(
                    beforeNormal, visual.localEulerAngles.z));

                gameTime.TrySetSpeed(GameSpeed.Fast);
                var beforeFast = visual.localEulerAngles.z;
                indicator.Refresh(1f);
                var fastDelta = Mathf.Abs(Mathf.DeltaAngle(
                    beforeFast, visual.localEulerAngles.z));

                Assert.That(normalDelta, Is.GreaterThan(0f));
                Assert.That(fastDelta, Is.GreaterThan(normalDelta));
                Assert.That(gameTime.SetRequests, Is.EqualTo(2),
                    "The read-only indicator must not request a speed change.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator TimeControlPanel_DecorationPauseLockDisablesOnlyNormalAndFast()
        {
            var root = UiObject("TimeControlPanel");
            try
            {
                var gameTime = root.AddComponent<GameTimeService>();
                var pause = CreateButton("PauseButton", root.transform, new Vector2(64f, 64f));
                var pauseLabel = UiObject("Label", pause.transform).AddComponent<TextMeshProUGUI>();
                pauseLabel.text = "Pause";
                var normal = CreateButton("NormalButton", root.transform, new Vector2(64f, 64f));
                var fast = CreateButton("FastButton", root.transform, new Vector2(64f, 64f));
                var pauseSelected = UiObject("SelectedVisual", pause.transform);
                var normalSelected = UiObject("SelectedVisual", normal.transform);
                var fastSelected = UiObject("SelectedVisual", fast.transform);
                pauseSelected.SetActive(false);
                normalSelected.SetActive(false);
                fastSelected.SetActive(false);
                var panel = root.AddComponent<TimeControlPanel>();
                panel.Configure(gameTime, pause, normal, fast);

                AssertSelectedSpeed(
                    GameSpeed.Normal,
                    pauseSelected,
                    normalSelected,
                    fastSelected);
                fast.onClick.Invoke();
                Assert.That(pauseLabel.text, Is.EqualTo("Pause"));
                AssertSelectedSpeed(
                    GameSpeed.Fast,
                    pauseSelected,
                    normalSelected,
                    fastSelected);

                gameTime.SetPaused();
                Assert.That(pauseLabel.text, Is.EqualTo("Resume"));

                panel.SetDecorationPauseLock(true);
                Assert.That(pause.interactable, Is.False,
                    "Decoration Mode owns the Pause lease, so Resume is unavailable until Done.");
                Assert.That(normal.interactable, Is.False);
                Assert.That(fast.interactable, Is.False);
                AssertSelectedSpeed(
                    GameSpeed.Paused,
                    pauseSelected,
                    normalSelected,
                    fastSelected);

                panel.SetDecorationPauseLock(false);
                Assert.That(pause.interactable, Is.True);
                Assert.That(pauseLabel.text, Is.EqualTo("Resume"));
                Assert.That(normal.interactable, Is.True);
                Assert.That(fast.interactable, Is.True);
                gameTime.SetFast();
                AssertSelectedSpeed(
                    GameSpeed.Fast,
                    pauseSelected,
                    normalSelected,
                    fastSelected);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            yield return null;
        }

        private static void AssertSelectedSpeed(
            GameSpeed expected,
            GameObject pause,
            GameObject normal,
            GameObject fast)
        {
            Assert.That(pause.activeSelf, Is.EqualTo(expected == GameSpeed.Paused));
            Assert.That(normal.activeSelf, Is.EqualTo(expected == GameSpeed.Normal));
            Assert.That(fast.activeSelf, Is.EqualTo(expected == GameSpeed.Fast));
        }

        [UnityTest]
        public IEnumerator PointerAdapter_ForwardsPointerIdAndNullOrUnconfiguredCallsAreSafe()
        {
            var root = UiObject("PointerAdapter");
            try
            {
                var hook = root.AddComponent<DecorationPointerBoundaryEventHook>();
                Assert.DoesNotThrow(() => hook.OnPointerDown(null));
                Assert.DoesNotThrow(() => hook.OnPointerDown(new PointerEventData(null)
                {
                    pointerId = 17
                }));
                Assert.DoesNotThrow(() => hook.OnPointerUp(null));
                Assert.DoesNotThrow(() => hook.OnPointerUp(new PointerEventData(null)
                {
                    pointerId = 17
                }));

                var pointer = new PointerRegistrar();
                hook.Configure(pointer);
                hook.OnPointerDown(null);
                hook.OnPointerDown(new PointerEventData(null) { pointerId = 73 });
                hook.OnPointerUp(null);
                hook.OnPointerUp(new PointerEventData(null) { pointerId = 73 });
                Assert.That(pointer.UiPresses, Is.EqualTo(new[] { 73 }));
                Assert.That(pointer.Releases, Is.EqualTo(new[] { 73 }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Tile_DirectInvocationRequiresActiveInteractableDefinition()
        {
            using var fixture = new TileFixture();
            var selected = 0;
            fixture.View.Configure(fixture.Pointer);
            fixture.View.Bind(fixture.ValidEntry, _ => selected++);

            fixture.Button.onClick.Invoke();
            Assert.That(selected, Is.EqualTo(1));

            fixture.Button.interactable = false;
            fixture.Button.onClick.Invoke();
            Assert.That(selected, Is.EqualTo(1), "Disabled direct invocation must be guarded.");

            fixture.Button.interactable = true;
            fixture.Root.SetActive(false);
            fixture.Button.onClick.Invoke();
            Assert.That(selected, Is.EqualTo(1), "Inactive direct invocation must be guarded.");

            fixture.Root.SetActive(true);
            fixture.View.Clear();
            fixture.Button.onClick.Invoke();
            Assert.That(selected, Is.EqualTo(1), "Cleared Definition must not emit.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tile_IsInteractableReflectsComponentRootAndButtonEligibility()
        {
            using var fixture = new TileFixture();
            fixture.View.Bind(fixture.ValidEntry, _ => { });
            Assert.That(fixture.View.IsInteractable, Is.True);

            fixture.View.enabled = false;
            Assert.That(fixture.View.IsInteractable, Is.False);
            fixture.View.enabled = true;
            fixture.Root.SetActive(false);
            Assert.That(fixture.View.IsInteractable, Is.False);
            fixture.Root.SetActive(true);
            fixture.Button.interactable = false;
            Assert.That(fixture.View.IsInteractable, Is.False);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tile_RebindOwnsExactlyOneCallbackAndClearPreservesExternalListeners()
        {
            using var fixture = new TileFixture();
            var first = 0;
            var second = 0;
            var external = 0;
            fixture.Button.onClick.AddListener(() => external++);
            fixture.View.Bind(fixture.ValidEntry, _ => first++);
            fixture.View.Bind(fixture.ValidEntry, _ => second++);

            fixture.Button.onClick.Invoke();
            Assert.That(first, Is.Zero);
            Assert.That(second, Is.EqualTo(1));
            Assert.That(external, Is.EqualTo(1));

            fixture.View.Clear();
            fixture.Button.onClick.Invoke();
            Assert.That(second, Is.EqualTo(1));
            Assert.That(external, Is.EqualTo(2),
                "Clear must not use RemoveAllListeners.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Tile_InvalidReferencesProduceDisabledNonblankSpecificFallbacks()
        {
            using var fixture = new TileFixture();
            var selected = 0;
            var cases = new[]
            {
                (CreateEntry(null, fixture.Sprite), "Unavailable", "Missing definition"),
                (CreateEntry(CreateDefinition("missing.prefab", "Missing Prefab", null),
                    fixture.Sprite), "Missing Prefab", "Missing prefab"),
                (CreateEntry(fixture.Definition, null), "Counter Fixture", "Missing thumbnail")
            };

            foreach (var item in cases)
            {
                fixture.Root.SetActive(true);
                fixture.View.Bind(item.Item1, _ => selected++);
                Assert.That(fixture.View.IsInteractable, Is.False);
                Assert.That(fixture.Name.text, Is.EqualTo(item.Item2));
                Assert.That(fixture.Warning.text, Is.EqualTo(item.Item3));
                Assert.That(fixture.Warning.text, Is.Not.Empty);
                Assert.That(fixture.WarningShape.activeSelf, Is.True);
                fixture.Button.onClick.Invoke();
            }

            Assert.That(selected, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Catalogue_RebindReopenAndRepeatedSelectionKeepFourTilesAndOneCallbackEach()
        {
            using var fixture = new CatalogueFixture();
            var selectedIds = new List<string>();
            fixture.View.Selected += definition => selectedIds.Add(definition.DefinitionId);
            fixture.View.Configure(fixture.Pointer, new UiTransitionRunner(() => true));

            for (var cycle = 0; cycle < 3; cycle++)
            {
                fixture.View.Bind(fixture.Catalogue);
                fixture.View.ShowCatalogue();
                yield return null;
                Assert.That(fixture.View.IsCatalogueVisible, Is.True);
                Assert.That(fixture.View.IsCollapsed, Is.False);
                var activeTiles = fixture.Content
                    .GetComponentsInChildren<DecorationCatalogueTileView>(false);
                Assert.That(activeTiles, Has.Length.EqualTo(4));
                Assert.That(activeTiles.Select(tile => tile.Definition.DefinitionId),
                    Is.EqualTo(fixture.Definitions.Select(item => item.DefinitionId)));

                foreach (var tile in activeTiles)
                {
                    tile.GetComponent<Button>().onClick.Invoke();
                }

                fixture.View.Hide();
                fixture.View.ShowCatalogue();
            }

            Assert.That(selectedIds, Has.Count.EqualTo(12));
            foreach (var id in fixture.Definitions.Select(item => item.DefinitionId))
            {
                Assert.That(selectedIds.Count(item => item == id), Is.EqualTo(3), id);
            }

            Assert.That(fixture.Content.GetComponentsInChildren<DecorationCatalogueTileView>(true),
                Has.Length.LessThanOrEqualTo(5), "Four pooled tiles plus one inactive template maximum.");
        }

        [UnityTest]
        public IEnumerator Catalogue_CollapseExpandHideAndInterruptedReopenEndInOneStableState()
        {
            using var fixture = new CatalogueFixture();
            fixture.View.Configure(fixture.Pointer, new UiTransitionRunner(() => false));
            fixture.View.Bind(fixture.Catalogue);

            fixture.View.ShowCatalogue();
            fixture.View.ShowCollapsedHandle();
            fixture.View.ShowCatalogue();
            yield return null;
            yield return null;
            Assert.That(fixture.View.IsCatalogueVisible, Is.True);
            Assert.That(fixture.View.IsCollapsed, Is.False);
            Assert.That(fixture.Expanded.activeSelf, Is.True);
            Assert.That(fixture.Collapsed.activeSelf, Is.False);
            Assert.That(fixture.Group.blocksRaycasts, Is.True);
            Assert.That(fixture.Group.interactable, Is.True);

            fixture.CollapseButton.onClick.Invoke();
            yield return null;
            Assert.That(fixture.View.IsCollapsed, Is.True);
            Assert.That(fixture.Collapsed.activeSelf, Is.True);
            Assert.That(fixture.Collapsed.GetComponent<RectTransform>().rect.size.x,
                Is.GreaterThanOrEqualTo(48f));
            Assert.That(fixture.Collapsed.GetComponent<RectTransform>().rect.size.y,
                Is.GreaterThanOrEqualTo(48f));

            fixture.ExpandButton.onClick.Invoke();
            yield return null;
            Assert.That(fixture.View.IsCollapsed, Is.False);

            fixture.View.Hide();
            yield return null;
            Assert.That(fixture.View.IsCatalogueVisible, Is.False);
            Assert.That(fixture.Group.blocksRaycasts, Is.False);
            Assert.That(fixture.Group.interactable, Is.False);
        }

        [UnityTest]
        public IEnumerator Catalogue_ExplicitStateSlidesWithUnscaledTimeAndReducedMotionSettlesImmediately()
        {
            using var fixture = new CatalogueFixture();
            var rect = fixture.Root.GetComponent<RectTransform>();
            SetField(fixture.View, "expandedAnchoredPosition", Vector2.zero);
            SetField(fixture.View, "collapsedAnchoredPosition", new Vector2(0f, -220f));
            SetField(fixture.View, "hiddenAnchoredPosition", new Vector2(0f, -420f));
            fixture.View.Configure(fixture.Pointer, new UiTransitionRunner(() => false));

            fixture.View.Hide();
            Assert.That(fixture.View.State, Is.EqualTo(DecorationCatalogueState.Hidden));
            Assert.That(fixture.Group.interactable, Is.False);
            Assert.That(fixture.Group.blocksRaycasts, Is.False);

            fixture.View.ShowCatalogue();
            Assert.That(fixture.View.State, Is.EqualTo(DecorationCatalogueState.Expanded));
            Assert.That(fixture.Expanded.activeSelf, Is.True);
            Assert.That(fixture.Collapsed.activeSelf, Is.False);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(Vector2.Distance(rect.anchoredPosition, Vector2.zero), Is.LessThan(0.01f));

            fixture.View.ShowCollapsedHandle();
            Assert.That(fixture.View.State, Is.EqualTo(DecorationCatalogueState.Collapsed));
            Assert.That(fixture.Expanded.activeSelf, Is.False);
            Assert.That(fixture.Collapsed.activeSelf, Is.True);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(Vector2.Distance(
                rect.anchoredPosition, new Vector2(0f, -220f)), Is.LessThan(0.01f));

            fixture.View.Configure(fixture.Pointer, new UiTransitionRunner(() => true));
            fixture.View.ShowCatalogue();
            yield return null;
            Assert.That(Vector2.Distance(rect.anchoredPosition, Vector2.zero), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator Catalogue_ButtonCallbacksRequireEligibleCurrentState()
        {
            using var fixture = new CatalogueFixture();
            fixture.View.Configure(fixture.Pointer, new UiTransitionRunner(() => true));
            fixture.View.Bind(fixture.Catalogue);

            fixture.View.Hide();
            fixture.CollapseButton.onClick.Invoke();
            fixture.ExpandButton.onClick.Invoke();
            Assert.That(fixture.View.IsCatalogueVisible, Is.False);

            fixture.View.ShowCatalogue();
            fixture.CollapseButton.interactable = false;
            fixture.CollapseButton.onClick.Invoke();
            Assert.That(fixture.View.IsCollapsed, Is.False);
            fixture.CollapseButton.interactable = true;
            fixture.View.enabled = false;
            fixture.CollapseButton.onClick.Invoke();
            Assert.That(fixture.View.IsCollapsed, Is.False);
            fixture.View.enabled = true;
            fixture.Root.SetActive(false);
            fixture.CollapseButton.onClick.Invoke();
            Assert.That(fixture.View.IsCollapsed, Is.False);
            fixture.Root.SetActive(true);

            fixture.CollapseButton.onClick.Invoke();
            Assert.That(fixture.View.IsCollapsed, Is.True);
            fixture.CollapseButton.onClick.Invoke();
            Assert.That(fixture.View.IsCollapsed, Is.True,
                "Collapse is ineligible while already collapsed.");
            fixture.ExpandButton.interactable = false;
            fixture.ExpandButton.onClick.Invoke();
            Assert.That(fixture.View.IsCollapsed, Is.True);
            fixture.ExpandButton.interactable = true;
            fixture.ExpandButton.onClick.Invoke();
            Assert.That(fixture.View.IsCollapsed, Is.False);
            fixture.ExpandButton.onClick.Invoke();
            Assert.That(fixture.View.IsCollapsed, Is.False,
                "Expand is ineligible while already expanded.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActionBar_GuardsHiddenDisabledAndInactiveActionsWithoutConsumingLatch()
        {
            using var fixture = new ActionFixture();
            var rotate = 0;
            var confirm = 0;
            var cancel = 0;
            var store = 0;
            fixture.View.RotateRequested += () => rotate++;
            fixture.View.ConfirmRequested += () => confirm++;
            fixture.View.CancelRequested += () => cancel++;
            fixture.View.StoreRequested += () => store++;
            fixture.View.Configure(fixture.Pointer, new UiTransitionRunner(() => true));

            fixture.View.Hide();
            InvokeAll(fixture);
            Assert.That(new[] { rotate, confirm, cancel, store }, Is.All.Zero);

            fixture.View.Show(canStore: false, canConfirm: false, PlacementFeedbackKey.Occupied);
            fixture.Confirm.onClick.Invoke();
            fixture.Store.onClick.Invoke();
            Assert.That(confirm, Is.Zero);
            Assert.That(store, Is.Zero);
            Assert.That(fixture.Store.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Confirm.interactable, Is.False);
            Assert.That(fixture.Feedback.text, Is.EqualTo("Space already occupied"));

            fixture.Cancel.onClick.Invoke();
            Assert.That(cancel, Is.EqualTo(1),
                "Ineligible calls must not consume the terminal window.");
            fixture.Rotate.onClick.Invoke();
            fixture.Confirm.onClick.Invoke();
            Assert.That(rotate, Is.Zero, "Terminal action blocks Rotate until next Show.");
            Assert.That(confirm, Is.Zero);

            fixture.View.Show(canStore: true, canConfirm: true, PlacementFeedbackKey.None);
            fixture.Root.SetActive(false);
            InvokeAll(fixture);
            Assert.That(new[] { rotate, confirm, cancel, store }, Is.EqualTo(new[] { 0, 0, 1, 0 }));
            fixture.Root.SetActive(true);
            fixture.Confirm.onClick.Invoke();
            Assert.That(confirm, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActionBar_RotateRepeatsBeforeExactlyOneMixedTerminalActionAndOnlyShowResets()
        {
            using var fixture = new ActionFixture();
            var rotate = 0;
            var confirm = 0;
            var cancel = 0;
            var store = 0;
            fixture.View.RotateRequested += () => rotate++;
            fixture.View.ConfirmRequested += () => confirm++;
            fixture.View.CancelRequested += () => cancel++;
            fixture.View.StoreRequested += () => store++;
            fixture.View.Configure(fixture.Pointer, new UiTransitionRunner(() => true));
            fixture.View.Show(true, true, PlacementFeedbackKey.None);

            fixture.Rotate.onClick.Invoke();
            fixture.Rotate.onClick.Invoke();
            fixture.Store.onClick.Invoke();
            fixture.Confirm.onClick.Invoke();
            fixture.Cancel.onClick.Invoke();
            Assert.That(rotate, Is.EqualTo(2));
            Assert.That(store, Is.EqualTo(1));
            Assert.That(confirm, Is.Zero);
            Assert.That(cancel, Is.Zero);

            fixture.View.Hide();
            fixture.Root.SetActive(true);
            fixture.Confirm.onClick.Invoke();
            Assert.That(confirm, Is.Zero, "Hide must not reset the terminal latch.");

            fixture.View.Show(true, true, PlacementFeedbackKey.None);
            fixture.Confirm.onClick.Invoke();
            fixture.Store.onClick.Invoke();
            Assert.That(confirm, Is.EqualTo(1));
            Assert.That(store, Is.EqualTo(1));
            yield return null;
        }

        [TestCase(DecorationActionPresentation.New, false)]
        [TestCase(DecorationActionPresentation.Existing, true)]
        public void ActionBar_SetPresentationUsesExactActionOrderAndClampsIntoSafeArea(
            DecorationActionPresentation presentation,
            bool expectsStore)
        {
            using var fixture = new ActionFixture();
            var rect = fixture.Root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(expectsStore ? 260f : 196f, 64f);
            var safeArea = new Rect(24f, 32f, 672f, 1184f);
            fixture.View.Configure(fixture.Pointer, new UiTransitionRunner(() => true));
            fixture.View.Show(expectsStore, true, PlacementFeedbackKey.None);

            fixture.View.SetPresentation(
                presentation,
                new Vector2(700f, 1240f),
                safeArea);

            Assert.That(fixture.Store.gameObject.activeSelf, Is.EqualTo(expectsStore));
            var activeOrder = fixture.Root.transform.Cast<Transform>()
                .Where(child => child.GetComponent<Button>() != null
                    && child.gameObject.activeSelf)
                .Select(child => child.name)
                .ToArray();
            Assert.That(activeOrder, Is.EqualTo(expectsStore
                ? new[] { "StoreButton", "CancelButton", "RotateButton", "ConfirmButton" }
                : new[] { "CancelButton", "RotateButton", "ConfirmButton" }));
            Assert.That(rect.anchoredPosition.x,
                Is.InRange(safeArea.xMin + rect.rect.width * 0.5f,
                    safeArea.xMax - rect.rect.width * 0.5f));
            Assert.That(rect.anchoredPosition.y,
                Is.InRange(safeArea.yMin + rect.rect.height * 0.5f,
                    safeArea.yMax - rect.rect.height * 0.5f));
        }

        [UnityTest]
        public IEnumerator ActionBar_ConvertsScreenAnchorsIntoScaledCanvasAtFourSizesAndEdges()
        {
            foreach (var responsiveCase in CanonicalResponsiveCases())
            {
                using var harness = new ProductionUiHarness(responsiveCase);
                harness.Begin();
                harness.Configure(new UiTransitionRunner(() => true));
                harness.Catalogue.Hide();
                harness.Action.Show(true, true, PlacementFeedbackKey.None);
                var panel = harness.ActionRoot.transform.Find("ActionPanel")
                    .GetComponent<RectTransform>();
                var edgePoints = new[]
                {
                    new Vector2(responsiveCase.SafeArea.xMin, responsiveCase.SafeArea.center.y),
                    new Vector2(responsiveCase.SafeArea.xMax, responsiveCase.SafeArea.center.y),
                    new Vector2(responsiveCase.SafeArea.center.x, responsiveCase.SafeArea.yMin),
                    new Vector2(responsiveCase.SafeArea.center.x, responsiveCase.SafeArea.yMax)
                };

                foreach (var point in edgePoints)
                {
                    harness.Action.SetPresentation(
                        DecorationActionPresentation.Existing,
                        point,
                        responsiveCase.SafeArea);
                    Canvas.ForceUpdateCanvases();
                    yield return null;

                    var rendered = RenderTargetRect(harness.Camera, panel);
                    Assert.That(rendered.xMin,
                        Is.GreaterThanOrEqualTo(responsiveCase.SafeArea.xMin - 0.5f),
                        responsiveCase.Label + " xMin at " + point);
                    Assert.That(rendered.xMax,
                        Is.LessThanOrEqualTo(responsiveCase.SafeArea.xMax + 0.5f),
                        responsiveCase.Label + " xMax at " + point);
                    Assert.That(rendered.yMin,
                        Is.GreaterThanOrEqualTo(responsiveCase.SafeArea.yMin - 0.5f),
                        responsiveCase.Label + " yMin at " + point);
                    Assert.That(rendered.yMax,
                        Is.LessThanOrEqualTo(responsiveCase.SafeArea.yMax + 0.5f),
                        responsiveCase.Label + " yMax at " + point);
                }
            }
        }

        [TestCase(PlacementFeedbackKey.None, "")]
        [TestCase(PlacementFeedbackKey.Occupied, "Space already occupied")]
        [TestCase(PlacementFeedbackKey.OutsideUnlockedArea, "Outside decoration area")]
        [TestCase(PlacementFeedbackKey.Locked, "Area not unlocked")]
        [TestCase(PlacementFeedbackKey.Blocked, "Furniture cannot be placed here")]
        [TestCase(PlacementFeedbackKey.EntranceClearance, "Keep the entrance clear")]
        [TestCase(PlacementFeedbackKey.UnsupportedSurface, "Furniture cannot stand here")]
        [TestCase(PlacementFeedbackKey.MissingInstance, "Furniture changed. Select it again.")]
        public void ActionBar_MapsCanonicalFeedbackAndUsesTextPlusShape(
            PlacementFeedbackKey key,
            string expected)
        {
            using var fixture = new ActionFixture();
            fixture.View.Configure(fixture.Pointer, new UiTransitionRunner(() => true));
            fixture.View.Show(false, key == PlacementFeedbackKey.None, key);

            Assert.That(fixture.Feedback.text, Is.EqualTo(expected));
            Assert.That(fixture.StateShape.activeSelf, Is.EqualTo(key != PlacementFeedbackKey.None));
            Assert.That(fixture.Confirm.interactable, Is.EqualTo(key == PlacementFeedbackKey.None));
        }

        [UnityTest]
        public IEnumerator ActionBar_MouseHoverShowsExactlyOneEnglishTooltipAndExitHidesIt()
        {
            var prefab = LoadEditorAsset<GameObject>(
                "Assets/UI/Phase6/Prefabs/PF_UI_DecorationActionBar.prefab");
            var root = UnityEngine.Object.Instantiate(prefab);
            root.SetActive(true);
            try
            {
                var view = root.GetComponent<DecorationActionBarView>();
                view.Configure(new PointerRegistrar(), new UiTransitionRunner(() => true));
                view.Show(true, true, PlacementFeedbackKey.None);
                var expected = new[]
                {
                    ("StoreButton", "Store"),
                    ("CancelButton", "Cancel"),
                    ("RotateButton", "Rotate"),
                    ("ConfirmButton", "Confirm")
                };

                foreach (var item in expected)
                {
                    var button = root.transform.Find("ActionPanel/" + item.Item1).gameObject;
                    var tooltip = button.transform.Find("Tooltip").gameObject;
                    var eventData = new PointerEventData(null)
                    {
                        pointerEnter = button
                    };
                    ExecuteEvents.Execute(button, eventData, ExecuteEvents.pointerEnterHandler);
                    Assert.That(tooltip.activeSelf, Is.True, item.Item1);
                    Assert.That(tooltip.GetComponentInChildren<TMP_Text>(true).text,
                        Is.EqualTo(item.Item2), item.Item1);
                    Assert.That(expected.Count(pair => root.transform
                            .Find("ActionPanel/" + pair.Item1 + "/Tooltip")
                            .gameObject.activeSelf), Is.EqualTo(1), item.Item1);

                    ExecuteEvents.Execute(button, eventData, ExecuteEvents.pointerExitHandler);
                    Assert.That(tooltip.activeSelf, Is.False, item.Item1);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator ActionBar_StoreAndConfirmEdgeTooltipsStayInsideSafeAreaAndClearRightRailAtFourSizes()
        {
            foreach (var responsiveCase in CanonicalResponsiveCases())
            {
                using var harness = new ProductionUiHarness(responsiveCase);
                harness.Begin();
                harness.Configure(new UiTransitionRunner(() => true));
                harness.Catalogue.Hide();
                harness.Modal.CloseForOwnerShutdown();
                harness.Action.Show(true, true, PlacementFeedbackKey.None);

                var safeRoot = new GameObject(
                    "Task10ActionTooltipRailSafeArea",
                    typeof(RectTransform),
                    typeof(SafeAreaContainer));
                safeRoot.transform.SetParent(harness.CanvasRoot.transform, false);
                var safeRootRect = (RectTransform)safeRoot.transform;
                safeRootRect.anchorMin = Vector2.zero;
                safeRootRect.anchorMax = Vector2.one;
                safeRootRect.offsetMin = Vector2.zero;
                safeRootRect.offsetMax = Vector2.zero;
                var safeArea = safeRoot.GetComponent<SafeAreaContainer>();
                safeArea.AutoApplyRuntimeSafeArea = false;
                safeArea.ApplySafeArea(
                    responsiveCase.SafeArea,
                    new Vector2(responsiveCase.Width, responsiveCase.Height));
                var rail = new GameObject("RightRail", typeof(RectTransform));
                rail.transform.SetParent(safeRoot.transform, false);
                var railRect = (RectTransform)rail.transform;
                railRect.anchorMin = Vector2.one;
                railRect.anchorMax = Vector2.one;
                railRect.pivot = Vector2.one;
                railRect.anchoredPosition = new Vector2(-24f, -24f);
                railRect.sizeDelta = new Vector2(180f, 336f);

                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                var renderedRail = RenderTargetRect(harness.Camera, railRect);
                var usableArea = Rect.MinMaxRect(
                    responsiveCase.SafeArea.xMin,
                    responsiveCase.SafeArea.yMin,
                    renderedRail.xMin,
                    responsiveCase.SafeArea.yMax);
                var edgeCases = new[]
                {
                    (Button: "StoreButton", Point: new Vector2(
                        usableArea.xMin, usableArea.center.y)),
                    (Button: "ConfirmButton", Point: new Vector2(
                        usableArea.xMax, usableArea.center.y))
                };

                foreach (var edgeCase in edgeCases)
                {
                    harness.Action.SetPresentation(
                        DecorationActionPresentation.Existing,
                        edgeCase.Point,
                        usableArea);
                    Canvas.ForceUpdateCanvases();
                    yield return null;

                    var button = harness.ActionRoot.transform
                        .Find("ActionPanel/" + edgeCase.Button).gameObject;
                    var tooltip = button.transform.Find("Tooltip").gameObject;
                    var eventData = new PointerEventData(harness.EventSystem)
                    {
                        pointerEnter = button,
                        position = RenderTargetRect(
                            harness.Camera,
                            button.GetComponent<RectTransform>()).center
                    };
                    ExecuteEvents.Execute(button, eventData, ExecuteEvents.pointerEnterHandler);
                    Canvas.ForceUpdateCanvases();

                    var tooltipRect = RenderTargetRect(
                        harness.Camera,
                        tooltip.GetComponent<RectTransform>());
                    AssertRectInside(
                        responsiveCase.SafeArea,
                        tooltipRect,
                        responsiveCase.Label + " " + edgeCase.Button + " tooltip");
                    Assert.That(tooltipRect.Overlaps(renderedRail), Is.False,
                        responsiveCase.Label + " " + edgeCase.Button
                        + " tooltip must not cover the RightRail.");

                    ExecuteEvents.Execute(button, eventData, ExecuteEvents.pointerExitHandler);
                    Assert.That(tooltip.activeSelf, Is.False, edgeCase.Button);
                }
            }
        }

        [UnityTest]
        public IEnumerator StoreModal_UsesContinueGameBlocksSceneAndOutsideDoesNotDismiss()
        {
            using var fixture = new ModalFixture();
            fixture.Configure(reducedMotion: true);
            fixture.View.Show(fixture.Definition);
            yield return null;

            Assert.That(fixture.View.IsOpen, Is.True);
            Assert.That(fixture.Boundary.CanProcessScenePointer(100), Is.False);
            Assert.That(fixture.GameTime.SetRequests, Is.Zero,
                "ContinueGame modal must not acquire another Pause reason.");
            Assert.That(fixture.Title.text, Is.EqualTo("Store furniture?"));
            Assert.That(fixture.Body.text, Is.EqualTo(
                "This removes it from the current layout. You can place it again from the catalogue."));

            fixture.Blocker.onClick.Invoke();
            Assert.That(fixture.View.IsOpen, Is.True, "Outside tap must not dismiss.");
            Assert.That(fixture.DismissCount, Is.Zero);

            fixture.Cancel.onClick.Invoke();
            yield return null;
            Assert.That(fixture.DismissCount, Is.EqualTo(1));
            Assert.That(fixture.View.IsOpen, Is.False);
            Assert.That(fixture.Boundary.CanProcessScenePointer(100), Is.True);
        }

        [UnityTest]
        public IEnumerator StoreModal_EligibleMixedCompletionEmitsOnceAndNextShowResets()
        {
            using var fixture = new ModalFixture();
            fixture.Configure(reducedMotion: true);

            fixture.Confirm.onClick.Invoke();
            fixture.Cancel.onClick.Invoke();
            Assert.That(fixture.ConfirmCount + fixture.DismissCount, Is.Zero);

            fixture.View.Show(fixture.Definition);
            fixture.Confirm.onClick.Invoke();
            fixture.Cancel.onClick.Invoke();
            fixture.View.TryHandleBack();
            Assert.That(fixture.ConfirmCount, Is.EqualTo(1));
            Assert.That(fixture.DismissCount, Is.Zero);
            Assert.That(fixture.View.IsOpen, Is.False);

            fixture.View.Show(fixture.Definition);
            Assert.That(fixture.View.TryHandleBack(), Is.True);
            fixture.Cancel.onClick.Invoke();
            fixture.Confirm.onClick.Invoke();
            Assert.That(fixture.ConfirmCount, Is.EqualTo(1));
            Assert.That(fixture.DismissCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator StoreModal_HiddenInactiveAndOwnerShutdownDoNotEmitOrResetLatch()
        {
            using var fixture = new ModalFixture();
            fixture.Configure(reducedMotion: true);
            fixture.View.Show(fixture.Definition);
            fixture.Confirm.onClick.Invoke();
            Assert.That(fixture.ConfirmCount, Is.EqualTo(1));

            fixture.View.CloseForOwnerShutdown();
            fixture.Confirm.onClick.Invoke();
            fixture.Cancel.onClick.Invoke();
            Assert.That(fixture.ConfirmCount, Is.EqualTo(1));
            Assert.That(fixture.DismissCount, Is.Zero);

            fixture.View.Show(fixture.Definition);
            fixture.Root.SetActive(false);
            fixture.Confirm.onClick.Invoke();
            fixture.Cancel.onClick.Invoke();
            Assert.That(fixture.ConfirmCount, Is.EqualTo(1));
            Assert.That(fixture.DismissCount, Is.Zero);
            fixture.Root.SetActive(true);
            fixture.View.Show(fixture.Definition);
            fixture.Cancel.onClick.Invoke();
            Assert.That(fixture.DismissCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator StoreModal_OwnerShutdownWhileOpenTopReleasesWithoutEventsAndCanReopen()
        {
            using var fixture = new ModalFixture();
            fixture.Configure(reducedMotion: true);
            fixture.View.Show(fixture.Definition);
            Assert.That(fixture.Boundary.CanProcessScenePointer(31), Is.False);

            fixture.View.CloseForOwnerShutdown();
            fixture.View.CloseForOwnerShutdown();
            Assert.That(fixture.View.IsOpen, Is.False);
            Assert.That(fixture.Boundary.CanProcessScenePointer(31), Is.True);
            Assert.That(fixture.ConfirmCount + fixture.DismissCount, Is.Zero);

            fixture.View.Show(fixture.Definition);
            fixture.Cancel.onClick.Invoke();
            Assert.That(fixture.DismissCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator StoreModal_OwnerShutdownWhileCoveredClosesOnlyOwnerAndCanReopen()
        {
            using var fixture = new ModalFixture();
            fixture.Configure(reducedMotion: true);
            fixture.View.Show(fixture.Definition);
            var covering = new UiView("covering.shutdown", UiViewKind.Modal,
                UiPausePolicy.ContinueGame, UiOutsideDismissPolicy.NotDismissible);
            var coveringHandle = fixture.Navigation.PushModal(covering);
            try
            {
                fixture.View.CloseForOwnerShutdown();
                Assert.That(fixture.View.IsOpen, Is.False);
                Assert.That(fixture.Navigation.IsTopModal(covering), Is.True);
                Assert.That(fixture.Boundary.CanProcessScenePointer(41), Is.True);
                Assert.That(fixture.ConfirmCount + fixture.DismissCount, Is.Zero);
            }
            finally
            {
                coveringHandle.Close();
            }

            fixture.View.Show(fixture.Definition);
            fixture.Confirm.onClick.Invoke();
            Assert.That(fixture.ConfirmCount, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator GeneratedPrefabs_LoadConfigureAndExerciseSerializedRuntimePath()
        {
            var cataloguePrefab = LoadEditorAsset<GameObject>(
                "Assets/UI/Phase6/Prefabs/PF_UI_DecorationCatalogue.prefab");
            var actionPrefab = LoadEditorAsset<GameObject>(
                "Assets/UI/Phase6/Prefabs/PF_UI_DecorationActionBar.prefab");
            var modalPrefab = LoadEditorAsset<GameObject>(
                "Assets/UI/Phase6/Prefabs/PF_UI_DecorationStoreModal.prefab");
            var catalogueAsset = LoadEditorAsset<DecorationCatalogueAsset>(
                "Assets/Art/Phase6/Catalogues/DC_Phase6Decoration.asset");
            var roots = new[]
            {
                UnityEngine.Object.Instantiate(cataloguePrefab),
                UnityEngine.Object.Instantiate(actionPrefab),
                UnityEngine.Object.Instantiate(modalPrefab)
            };
            try
            {
                foreach (var root in roots)
                {
                    root.SetActive(true);
                }

                var pointer = new PointerRegistrar();
                var runner = new UiTransitionRunner(() => true);
                var catalogue = roots[0].GetComponent<DecorationCatalogueView>();
                var selected = 0;
                catalogue.Selected += _ => selected++;
                catalogue.Configure(pointer, runner);
                catalogue.Bind(catalogueAsset);
                catalogue.ShowCatalogue();
                roots[0].GetComponentsInChildren<DecorationCatalogueTileView>(false)[0]
                    .GetComponent<Button>().onClick.Invoke();
                Assert.That(selected, Is.EqualTo(1));

                var action = roots[1].GetComponent<DecorationActionBarView>();
                var confirmed = 0;
                action.ConfirmRequested += () => confirmed++;
                action.Configure(pointer, runner);
                action.Show(false, true, PlacementFeedbackKey.None);
                roots[1].transform.Find("ActionPanel/ConfirmButton")
                    .GetComponent<Button>().onClick.Invoke();
                Assert.That(confirmed, Is.EqualTo(1));

                var modal = roots[2].GetComponent<DecorationStoreModalView>();
                var navigation = new UiNavigationCoordinator();
                var boundary = new UiPointerBoundary();
                var confirms = 0;
                var dismisses = 0;
                modal.ConfirmRequested += () => confirms++;
                modal.DismissRequested += () => dismisses++;
                modal.Configure(navigation, new UiPauseCoordinator(new FakeGameTimeService()),
                    boundary, runner);
                modal.Show(catalogueAsset.Entries[0].Definition);
                roots[2].transform.Find("SafeArea/Content/StoreButton")
                    .GetComponent<Button>().onClick.Invoke();
                modal.Show(catalogueAsset.Entries[0].Definition);
                roots[2].transform.Find("SafeArea/Content/CancelButton")
                    .GetComponent<Button>().onClick.Invoke();
                modal.Show(catalogueAsset.Entries[0].Definition);
                Assert.That(modal.TryHandleBack(), Is.True);
                Assert.That(confirms, Is.EqualTo(1));
                Assert.That(dismisses, Is.EqualTo(2));

                var adapter = roots[1].GetComponentInChildren<DecorationPointerBoundaryEventHook>(true);
                Assert.That(adapter, Is.Not.Null);
                adapter.OnPointerDown(new PointerEventData(null) { pointerId = 812 });
                Assert.That(pointer.UiPresses, Does.Contain(812));
            }
            finally
            {
                foreach (var root in roots)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator GeneratedCataloguePrefab_BindPositionsFourPooledTilesDeterministicallyWithoutOverlap()
        {
            var prefab = LoadEditorAsset<GameObject>(
                "Assets/UI/Phase6/Prefabs/PF_UI_DecorationCatalogue.prefab");
            var catalogueAsset = LoadEditorAsset<DecorationCatalogueAsset>(
                "Assets/Art/Phase6/Catalogues/DC_Phase6Decoration.asset");
            var parent = UiObject("GeneratedCatalogueGeometryParent");
            parent.GetComponent<RectTransform>().sizeDelta = new Vector2(1080f, 1920f);
            var instance = UnityEngine.Object.Instantiate(prefab, parent.transform);
            try
            {
                var safeArea = instance.GetComponent<SafeAreaContainer>();
                safeArea.AutoApplyRuntimeSafeArea = false;
                safeArea.ApplySafeArea(new Rect(0f, 0f, 1080f, 1920f),
                    new Vector2(1080f, 1920f));
                instance.SetActive(true);
                var view = instance.GetComponent<DecorationCatalogueView>();
                view.Configure(new PointerRegistrar(), new UiTransitionRunner(() => true));
                view.Bind(catalogueAsset);
                view.ShowCatalogue();
                yield return null;
                Canvas.ForceUpdateCanvases();

                var first = AssertFourTilesInsideContentWithoutOverlap(instance);

                view.ShowCollapsedHandle();
                view.ShowCatalogue();
                view.Bind(catalogueAsset);
                yield return null;
                Canvas.ForceUpdateCanvases();

                var second = AssertFourTilesInsideContentWithoutOverlap(instance);
                Assert.That(second.Keys, Is.EqualTo(first.Keys));
                foreach (var name in first.Keys)
                {
                    AssertRectEqual(second[name], first[name], name);
                }

                Assert.That(instance.GetComponentsInChildren<DecorationCatalogueTileView>(true),
                    Has.Length.EqualTo(5),
                    "Rebind/reopen must keep four pooled tiles plus one inactive template.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [UnityTest]
        public IEnumerator StoreModal_NonTopModalCannotConsumeCompletionLatch()
        {
            using var fixture = new ModalFixture();
            fixture.Configure(reducedMotion: true);
            fixture.View.Show(fixture.Definition);
            var covering = new UiView("covering.modal", UiViewKind.Modal,
                UiPausePolicy.ContinueGame, UiOutsideDismissPolicy.NotDismissible);
            var coveringHandle = fixture.Navigation.PushModal(covering);
            try
            {
                fixture.Confirm.onClick.Invoke();
                fixture.Cancel.onClick.Invoke();
                Assert.That(fixture.ConfirmCount + fixture.DismissCount, Is.Zero);
            }
            finally
            {
                coveringHandle.Close();
            }

            fixture.Confirm.onClick.Invoke();
            Assert.That(fixture.ConfirmCount, Is.EqualTo(1),
                "Ineligible lower-modal calls must not consume the latch.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseTimeAndReducedMotion_AllViewsRemainInteractiveAndTransitionsSettle()
        {
            var originalTimeScale = Time.timeScale;
            using var catalogue = new CatalogueFixture();
            using var action = new ActionFixture();
            using var modal = new ModalFixture();
            try
            {
                Time.timeScale = 0f;
                var reduced = new UiTransitionRunner(() => true);
                catalogue.View.Configure(catalogue.Pointer, reduced);
                catalogue.View.Bind(catalogue.Catalogue);
                action.View.Configure(action.Pointer, reduced);
                modal.Configure(reducedMotion: true);

                catalogue.View.ShowCatalogue();
                catalogue.CollapseButton.onClick.Invoke();
                catalogue.ExpandButton.onClick.Invoke();
                var selection = 0;
                catalogue.View.Selected += _ => selection++;
                catalogue.Content.GetComponentsInChildren<DecorationCatalogueTileView>(false)[0]
                    .GetComponent<Button>().onClick.Invoke();

                var rotate = 0;
                var cancel = 0;
                action.View.RotateRequested += () => rotate++;
                action.View.CancelRequested += () => cancel++;
                action.View.Show(false, true, PlacementFeedbackKey.None);
                action.Rotate.onClick.Invoke();
                action.Cancel.onClick.Invoke();

                modal.View.Show(modal.Definition);
                modal.Cancel.onClick.Invoke();
                yield return null;

                Assert.That(selection, Is.EqualTo(1));
                Assert.That(rotate, Is.EqualTo(1));
                Assert.That(cancel, Is.EqualTo(1));
                Assert.That(modal.DismissCount, Is.EqualTo(1));
                Assert.That(catalogue.Group.alpha, Is.EqualTo(1f));
                Assert.That(action.Group.alpha, Is.EqualTo(1f));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }
        }

        [UnityTest]
        public IEnumerator GeneratedProductionViews_FourCanonicalRenderTargetsRaycastAndTouchEssentialActionsInsideSimulatedSafeArea()
        {
            var cases = new[]
            {
                new ResponsiveCase(1080, 1920, new Rect(24f, 96f, 1032f, 1740f)),
                new ResponsiveCase(720, 1280, new Rect(18f, 64f, 684f, 1152f)),
                new ResponsiveCase(1080, 2400, new Rect(24f, 120f, 1032f, 2160f)),
                new ResponsiveCase(2400, 1080, new Rect(96f, 48f, 2208f, 984f))
            };

            foreach (var responsiveCase in cases)
            {
                yield return RunResponsiveTouchBranch(responsiveCase);
            }
        }

        [UnityTest]
        public IEnumerator GeneratedCatalogue_LandscapeReservesRightRailAndCopyHierarchyUsesNonzeroSafeArea()
        {
            var theme = LoadEditorAsset<AnimalCafeUiTheme>(
                "Assets/UI/Phase5/Theme/AnimalCafeUiTheme.asset");
            foreach (var responsiveCase in CanonicalResponsiveCases())
            {
                var harness = new ProductionUiHarness(responsiveCase);
                try
                {
                    harness.Begin();
                    harness.Configure(new UiTransitionRunner(() => true));
                    harness.Action.Hide();
                    harness.Modal.CloseForOwnerShutdown();
                    harness.Catalogue.ShowCatalogue();

                    var safeRoot = new GameObject(
                        "Task10ResponsiveRailSafeArea",
                        typeof(RectTransform),
                        typeof(SafeAreaContainer));
                    safeRoot.transform.SetParent(harness.CanvasRoot.transform, false);
                    var safeRootRect = (RectTransform)safeRoot.transform;
                    safeRootRect.anchorMin = Vector2.zero;
                    safeRootRect.anchorMax = Vector2.one;
                    safeRootRect.offsetMin = Vector2.zero;
                    safeRootRect.offsetMax = Vector2.zero;
                    var safeArea = safeRoot.GetComponent<SafeAreaContainer>();
                    safeArea.AutoApplyRuntimeSafeArea = false;
                    safeArea.ApplySafeArea(
                        responsiveCase.SafeArea,
                        new Vector2(responsiveCase.Width, responsiveCase.Height));
                    var rail = new GameObject("RightRail", typeof(RectTransform));
                    rail.transform.SetParent(safeRoot.transform, false);
                    var railRect = (RectTransform)rail.transform;
                    railRect.anchorMin = Vector2.one;
                    railRect.anchorMax = Vector2.one;
                    railRect.pivot = Vector2.one;
                    railRect.anchoredPosition = new Vector2(-24f, -24f);
                    railRect.sizeDelta = new Vector2(180f, 336f);

                    Canvas.ForceUpdateCanvases();
                    yield return null;
                    Canvas.ForceUpdateCanvases();

                    Assert.That(responsiveCase.SafeArea.xMin, Is.GreaterThan(0f));
                    Assert.That(responsiveCase.SafeArea.yMin, Is.GreaterThan(0f));
                    Assert.That(responsiveCase.Width - responsiveCase.SafeArea.xMax,
                        Is.GreaterThan(0f));
                    Assert.That(responsiveCase.Height - responsiveCase.SafeArea.yMax,
                        Is.GreaterThan(0f));
                    var expandedRect = RenderTargetRect(harness.Camera,
                        harness.CatalogueRoot.transform.Find("ExpandedSheet")
                            .GetComponent<RectTransform>());
                    var renderedRail = RenderTargetRect(harness.Camera, railRect);
                    AssertRectInside(responsiveCase.SafeArea, expandedRect,
                        responsiveCase.Label + " Catalogue expanded sheet");
                    AssertRectInside(responsiveCase.SafeArea, renderedRail,
                        responsiveCase.Label + " RightRail");
                    if (responsiveCase.Width > responsiveCase.Height)
                    {
                        Assert.That(expandedRect.Overlaps(renderedRail), Is.False,
                            responsiveCase.Label
                            + " expanded Catalogue must leave the player-visible RightRail clear.");
                        Assert.That(expandedRect.xMax, Is.LessThanOrEqualTo(renderedRail.xMin));
                    }

                    var title = harness.CatalogueRoot.transform
                        .Find("ExpandedSheet/Title").GetComponent<TMP_Text>();
                    Assert.That(title.text, Is.EqualTo("Furniture Catalogue"));
                    Assert.That(title.fontSize, Is.EqualTo(theme.Typography.Heading.FontSize));
                    Assert.That(title.fontStyle, Is.EqualTo(theme.Typography.Heading.FontStyle));
                    var titleRect = RenderedTextRect(harness.Camera, title);
                    AssertRectInside(responsiveCase.SafeArea, titleRect,
                        responsiveCase.Label + " Catalogue title");

                    var tiles = harness.CatalogueRoot
                        .GetComponentsInChildren<DecorationCatalogueTileView>(false)
                        .Where(tile => tile.Definition != null)
                        .OrderBy(tile => tile.name)
                        .ToArray();
                    Assert.That(tiles, Has.Length.EqualTo(4));
                    foreach (var tile in tiles)
                    {
                        var name = tile.transform.Find("Name").GetComponent<TMP_Text>();
                        var footprint = tile.transform.Find("Footprint").GetComponent<TMP_Text>();
                        Assert.That(name.text, Is.EqualTo(tile.Definition.DisplayName));
                        Assert.That(name.fontSize, Is.EqualTo(theme.Typography.Body.FontSize));
                        Assert.That(name.fontStyle, Is.EqualTo(theme.Typography.Body.FontStyle));
                        Assert.That(footprint.text, Is.EqualTo(
                            tile.Definition.FootprintWidth + " × "
                            + tile.Definition.FootprintDepth));
                        Assert.That(footprint.fontSize,
                            Is.EqualTo(theme.Typography.Label.FontSize));
                        Assert.That(footprint.fontStyle,
                            Is.EqualTo(theme.Typography.Label.FontStyle));
                        var nameRect = RenderedTextRect(harness.Camera, name);
                        var footprintRect = RenderedTextRect(harness.Camera, footprint);
                        AssertRectInside(responsiveCase.SafeArea, nameRect,
                            responsiveCase.Label + " " + tile.Definition.DefinitionId + " Name");
                        AssertRectInside(responsiveCase.SafeArea, footprintRect,
                            responsiveCase.Label + " " + tile.Definition.DefinitionId + " Footprint");
                        Assert.That(nameRect.Overlaps(footprintRect), Is.False);
                        if (responsiveCase.Width == 720)
                        {
                            Assert.That(titleRect.height, Is.GreaterThan(nameRect.height));
                            Assert.That(nameRect.height, Is.GreaterThan(footprintRect.height));
                        }
                    }
                }
                finally
                {
                    harness.Dispose();
                }
                harness.AssertDisposed();
            }
        }

        [UnityTest]
        public IEnumerator GeneratedProductionViews_LongLocalizedCopyWrapsAtThemeMinimumWithoutHidingMeaningOrActions()
        {
            var samples = new[]
            {
                new LongCopySample("Counter Module", "Counter Module Plus", false),
                new LongCopySample("Counter 1 x 2", "Counter 1 x 2 Plus", false),
                new LongCopySample("Counter 1 x 3", "Counter 1 x 3 Plus", false),
                new LongCopySample("Counter 2 x 3", "Counter 2 x 3 Plus", false),
                new LongCopySample("Space already occupied", "This space is already occupied.", false),
                new LongCopySample("Outside decoration area", "This is outside decoration area.", false),
                new LongCopySample("Area not unlocked", "Area is not yet unlocked.", false),
                new LongCopySample("Furniture cannot be placed here", "Furniture cannot be placed in this location.", false),
                new LongCopySample("Keep the entrance clear", "Please keep the entrance clear.", false),
                new LongCopySample("Furniture cannot stand here", "Furniture cannot stand on this surface.", false),
                new LongCopySample("Furniture changed. Select it again.", "This furniture changed. Please select it again.", false),
                new LongCopySample("Store furniture?", "Store cafe furniture?", false),
                new LongCopySample(
                    "This removes it from the current layout. You can place it again from the catalogue.",
                    "This removes it from the current layout. You can place it again from the catalogue. Keep it safe for your next layout.",
                    false),
                new LongCopySample("Cancel", "Cancel it", true),
                new LongCopySample("Store", "Store it", true)
            };
            foreach (var sample in samples)
            {
                var ratio = (float)GraphemeCount(sample.Longer) / GraphemeCount(sample.Source);
                var minimum = sample.ShortAction ? 1.20f : 1.30f;
                var maximum = sample.ShortAction ? 1.80f : 1.50f;
                Assert.That(ratio, Is.InRange(minimum, maximum), sample.Longer);
                if (sample.ShortAction)
                {
                    Assert.That(sample.Longer, Does.Contain(sample.Source));
                }
            }

            var feedbackKeys = new[]
            {
                PlacementFeedbackKey.Occupied,
                PlacementFeedbackKey.OutsideUnlockedArea,
                PlacementFeedbackKey.Locked,
                PlacementFeedbackKey.Blocked,
                PlacementFeedbackKey.EntranceClearance,
                PlacementFeedbackKey.UnsupportedSurface,
                PlacementFeedbackKey.MissingInstance
            };
            var exactFootprints = new[]
            {
                "占 1 × 1",
                "占 1 × 2",
                "占 1 × 3",
                "占 2 × 3"
            };
            foreach (var responsiveCase in CanonicalResponsiveCases())
            {
                var harness = new ProductionUiHarness(responsiveCase);
                try
                {
                    harness.Begin();
                    harness.Configure(new UiTransitionRunner(() => true));
                    var catalogueTiles = harness.CatalogueRoot
                        .GetComponentsInChildren<DecorationCatalogueTileView>(false)
                        .Where(tile => tile.Definition != null)
                        .OrderBy(tile => tile.name)
                        .ToArray();
                    var catalogueButtons = catalogueTiles
                        .Select(tile => tile.GetComponent<Button>())
                        .Concat(new[]
                        {
                            harness.CatalogueRoot.transform.Find("ExpandedSheet/CollapseButton")
                                .GetComponent<Button>()
                        }).ToArray();
                    var actionButtons = harness.ActionRoot.GetComponentsInChildren<Button>(true);
                    var modalButtons = new[]
                    {
                        harness.ModalRoot.transform.Find("SafeArea/Content/CancelButton")
                            .GetComponent<Button>(),
                        harness.ModalRoot.transform.Find("SafeArea/Content/StoreButton")
                            .GetComponent<Button>()
                    };

                    for (var index = 0; index < 4; index++)
                    {
                        harness.Modal.CloseForOwnerShutdown();
                        harness.Action.Hide();
                        harness.Catalogue.ShowCatalogue();
                        var name = catalogueTiles[index].transform.Find("Name").GetComponent<TMP_Text>();
                        var footprint = catalogueTiles[index].transform.Find("Footprint").GetComponent<TMP_Text>();
                        name.text = samples[index].Longer;
                        footprint.text = exactFootprints[index];
                        name.textWrappingMode = TextWrappingModes.Normal;
                        footprint.textWrappingMode = TextWrappingModes.Normal;
                        Canvas.ForceUpdateCanvases();
                        name.ForceMeshUpdate(ignoreActiveState: false, forceTextReparsing: true);
                        footprint.ForceMeshUpdate(ignoreActiveState: false, forceTextReparsing: true);
                        Assert.That(name.text, Is.EqualTo(samples[index].Longer),
                            responsiveCase.Label + " tile Name " + index);
                        Assert.That(footprint.text, Is.EqualTo(exactFootprints[index]),
                            responsiveCase.Label + " tile Footprint " + index);
                        Assert.That(name.fontSize,
                            Is.GreaterThanOrEqualTo(AnimalCafeUiTheme.MinimumBodyFontSize),
                            responsiveCase.Label + " Name must preserve the Body 16 minimum.");
                        Assert.That(footprint.fontSize,
                            Is.GreaterThanOrEqualTo(AnimalCafeUiTheme.MinimumLabelFontSize),
                            responsiveCase.Label + " Footprint must preserve the Label 14 minimum.");
                        Assert.That(name.overflowMode,
                            Is.Not.EqualTo(TextOverflowModes.Ellipsis)
                                .And.Not.EqualTo(TextOverflowModes.Truncate));
                        Assert.That(footprint.overflowMode,
                            Is.Not.EqualTo(TextOverflowModes.Ellipsis)
                                .And.Not.EqualTo(TextOverflowModes.Truncate));
                        AssertRenderedTextInsideSafeAreaWithoutClipping(
                            name,
                            harness.Camera,
                            responsiveCase.SafeArea,
                            catalogueButtons);
                        AssertRenderedTextInsideSafeAreaWithoutClipping(
                            footprint,
                            harness.Camera,
                            responsiveCase.SafeArea,
                            catalogueButtons);
                        Assert.That(
                            RenderedTextRect(harness.Camera, name).Overlaps(
                                RenderedTextRect(harness.Camera, footprint)),
                            Is.False,
                            responsiveCase.Label + " tile Name and Footprint must not overlap.");
                    }

                    for (var index = 4; index < samples.Length; index++)
                    {
                        TMP_Text text;
                        IEnumerable<Button> essentialButtons;
                        harness.Modal.CloseForOwnerShutdown();
                        if (index < 11)
                        {
                            harness.Catalogue.Hide();
                            harness.Action.Show(true, false, feedbackKeys[index - 4]);
                            text = harness.ActionRoot.transform.Find("FeedbackToast/Message")
                                .GetComponent<TMP_Text>();
                            essentialButtons = actionButtons;
                        }
                        else
                        {
                            harness.Catalogue.Hide();
                            harness.Action.Hide();
                            harness.Modal.Show(harness.CatalogueAsset.Entries[0].Definition);
                            var path = index == 11
                                ? "SafeArea/Content/Title"
                                : index == 12
                                    ? "SafeArea/Content/Body"
                                    : index == 13
                                        ? "SafeArea/Content/CancelButton/Label"
                                        : "SafeArea/Content/StoreButton/Label";
                            text = harness.ModalRoot.transform.Find(path).GetComponent<TMP_Text>();
                            essentialButtons = modalButtons;
                        }

                        text.text = samples[index].Longer;
                        text.textWrappingMode = TextWrappingModes.Normal;
                        Canvas.ForceUpdateCanvases();
                        text.ForceMeshUpdate(ignoreActiveState: false, forceTextReparsing: true);
                        Assert.That(text.text, Is.EqualTo(samples[index].Longer),
                            responsiveCase.Label + " semantic target " + index);
                        var minimumFontSize = index >= 13
                            ? AnimalCafeUiTheme.MinimumLabelFontSize
                            : AnimalCafeUiTheme.MinimumBodyFontSize;
                        Assert.That(text.fontSize,
                            Is.GreaterThanOrEqualTo(minimumFontSize),
                            responsiveCase.Label + " semantic target " + index
                                + " minimum font size");
                        Assert.That(text.overflowMode,
                            Is.Not.EqualTo(TextOverflowModes.Ellipsis)
                                .And.Not.EqualTo(TextOverflowModes.Truncate));
                        AssertRenderedTextInsideSafeAreaWithoutClipping(
                            text,
                            harness.Camera,
                            responsiveCase.SafeArea,
                            essentialButtons);
                    }
                }
                finally
                {
                    harness.Dispose();
                }
                harness.AssertDisposed();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator GeneratedProductionViews_FourThumbnailsRemainDefinitionMatchedNonblankAndConsistentlyFramed()
        {
            var catalogue = LoadEditorAsset<DecorationCatalogueAsset>(
                "Assets/Art/Phase6/Catalogues/DC_Phase6Decoration.asset");
            var expectedIds = new[]
            {
                "furniture.counter.module.01",
                "counter.preset.1x2",
                "counter.preset.1x3",
                "counter.preset.2x3"
            };
            Assert.That(catalogue.Entries.Select(entry => entry.Definition.DefinitionId),
                Is.EqualTo(expectedIds));
            Assert.That(catalogue.Entries, Has.Count.EqualTo(4));
            var dimensions = new HashSet<Vector2Int>();
            var pixelsPerUnit = new HashSet<float>();
            var alphaBoundsById = new Dictionary<string, RectInt>();
            foreach (var entry in catalogue.Entries)
            {
                Assert.That(entry.Thumbnail, Is.Not.Null, entry.Definition.DefinitionId);
                Assert.That(entry.Thumbnail.texture, Is.Not.Null, entry.Definition.DefinitionId);
                Assert.That(entry.Thumbnail.rect.width, Is.GreaterThan(0f));
                Assert.That(entry.Thumbnail.rect.height, Is.GreaterThan(0f));
                var alphaBounds = ReadVisibleAlphaBounds(entry.Thumbnail);
                alphaBoundsById.Add(entry.Definition.DefinitionId, alphaBounds);
                var spriteWidth = Mathf.RoundToInt(entry.Thumbnail.rect.width);
                var spriteHeight = Mathf.RoundToInt(entry.Thumbnail.rect.height);
                Assert.That(alphaBounds.width, Is.GreaterThan(0),
                    entry.Definition.DefinitionId + " visible alpha width");
                Assert.That(alphaBounds.height, Is.GreaterThan(0),
                    entry.Definition.DefinitionId + " visible alpha height");
                Assert.That(alphaBounds.xMin, Is.GreaterThan(0),
                    entry.Definition.DefinitionId + " left framing margin");
                Assert.That(alphaBounds.yMin, Is.GreaterThan(0),
                    entry.Definition.DefinitionId + " bottom framing margin");
                Assert.That(alphaBounds.xMax, Is.LessThan(spriteWidth),
                    entry.Definition.DefinitionId + " right framing margin");
                Assert.That(alphaBounds.yMax, Is.LessThan(spriteHeight),
                    entry.Definition.DefinitionId + " top framing margin");
                Assert.That((alphaBounds.center.x / spriteWidth), Is.InRange(0.35f, 0.65f),
                    entry.Definition.DefinitionId + " horizontal framing center");
                Assert.That((alphaBounds.center.y / spriteHeight), Is.InRange(0.35f, 0.65f),
                    entry.Definition.DefinitionId + " vertical framing center");
                dimensions.Add(new Vector2Int(entry.Thumbnail.texture.width, entry.Thumbnail.texture.height));
                pixelsPerUnit.Add(entry.Thumbnail.pixelsPerUnit);
            }
            Assert.That(dimensions, Has.Count.EqualTo(1));
            Assert.That(pixelsPerUnit, Has.Count.EqualTo(1));

            foreach (var responsiveCase in CanonicalResponsiveCases())
            {
                var harness = new ProductionUiHarness(responsiveCase);
                try
                {
                    harness.Begin();
                    harness.Configure(new UiTransitionRunner(() => true));
                    harness.Action.Hide();
                    harness.Modal.CloseForOwnerShutdown();
                    harness.Catalogue.ShowCatalogue();
                    Canvas.ForceUpdateCanvases();
                    var tiles = harness.CatalogueRoot
                        .GetComponentsInChildren<DecorationCatalogueTileView>(false)
                        .Where(tile => tile.Definition != null)
                        .OrderBy(tile => tile.name)
                        .ToArray();
                    Assert.That(tiles, Has.Length.EqualTo(4));
                    Assert.That(tiles.Select(tile => tile.Definition.DefinitionId), Is.EqualTo(expectedIds));
                    for (var index = 0; index < tiles.Length; index++)
                    {
                        var tile = tiles[index];
                        var image = tile.GetComponentsInChildren<Image>(true)
                            .Single(candidate => candidate.name == "Thumbnail");
                        var entry = catalogue.Entries[index];
                        Assert.That(image.sprite, Is.SameAs(entry.Thumbnail),
                            responsiveCase.Label + " " + expectedIds[index]);
                        Assert.That(image.type, Is.EqualTo(Image.Type.Simple));
                        Assert.That(image.color.a, Is.GreaterThan(0f));
                        Assert.That(alphaBoundsById[expectedIds[index]].width, Is.GreaterThan(0));

                        var tileRect = RenderTargetRect(harness.Camera,
                            tile.GetComponent<RectTransform>());
                        var thumbnailRect = RenderTargetRect(harness.Camera,
                            image.rectTransform);
                        AssertRectInside(responsiveCase.SafeArea, tileRect,
                            responsiveCase.Label + " " + expectedIds[index] + " tile");
                        AssertRectInside(tileRect, thumbnailRect,
                            responsiveCase.Label + " " + expectedIds[index] + " thumbnail");
                        foreach (var mask in image.GetComponentsInParent<RectMask2D>(false))
                        {
                            AssertRectInside(RenderTargetRect(harness.Camera, mask.rectTransform),
                                thumbnailRect,
                                responsiveCase.Label + " " + expectedIds[index] + " mask");
                        }
                    }
                }
                finally
                {
                    harness.Dispose();
                }
                harness.AssertDisposed();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator GeneratedProductionViews_InvalidStateUsesSpecificCopyDisabledConfirmAndVisibleNonColorShape()
        {
            var responsiveCase = CanonicalResponsiveCases()[0];
            var harness = new ProductionUiHarness(responsiveCase);
            try
            {
                harness.Begin();
                harness.Configure(new UiTransitionRunner(() => true));
                harness.Catalogue.Hide();
                harness.Modal.CloseForOwnerShutdown();
                var expected = new Dictionary<PlacementFeedbackKey, string>
                {
                    [PlacementFeedbackKey.Occupied] = "Space already occupied",
                    [PlacementFeedbackKey.OutsideUnlockedArea] = "Outside decoration area",
                    [PlacementFeedbackKey.Locked] = "Area not unlocked",
                    [PlacementFeedbackKey.Blocked] = "Furniture cannot be placed here",
                    [PlacementFeedbackKey.EntranceClearance] = "Keep the entrance clear",
                    [PlacementFeedbackKey.UnsupportedSurface] = "Furniture cannot stand here",
                    [PlacementFeedbackKey.MissingInstance] = "Furniture changed. Select it again."
                };
                var confirm = harness.ActionRoot.transform.Find("ActionPanel/ConfirmButton")
                    .GetComponent<Button>();
                var cancel = harness.ActionRoot.transform.Find("ActionPanel/CancelButton")
                    .GetComponent<Button>();
                var store = harness.ActionRoot.transform.Find("ActionPanel/StoreButton")
                    .GetComponent<Button>();
                var feedback = harness.ActionRoot.transform.Find("FeedbackToast/Message")
                    .GetComponent<TMP_Text>();
                var shape = harness.ActionRoot.transform.Find("FeedbackToast/StateShape")
                    .gameObject;
                foreach (var pair in expected)
                {
                    harness.Action.Show(true, false, pair.Key);
                    Canvas.ForceUpdateCanvases();
                    Assert.That(feedback.text, Is.EqualTo(pair.Value), pair.Key.ToString());
                    Assert.That(confirm.interactable, Is.False, pair.Key.ToString());
                    Assert.That(shape.activeInHierarchy, Is.True, pair.Key.ToString());
                    var shapeGraphic = shape.GetComponent<Graphic>();
                    Assert.That(shapeGraphic.enabled, Is.True, pair.Key.ToString());
                    Assert.That(shapeGraphic.color.a, Is.GreaterThan(0f), pair.Key.ToString());
                    var shapeRect = RenderTargetRect(harness.Camera,
                        shape.GetComponent<RectTransform>());
                    Assert.That(shapeRect.width, Is.GreaterThanOrEqualTo(12f),
                        pair.Key + " shape width");
                    Assert.That(shapeRect.height, Is.GreaterThanOrEqualTo(12f),
                        pair.Key + " shape height");
                    AssertRectInside(responsiveCase.SafeArea, shapeRect,
                        pair.Key + " non-color shape");
                    Assert.That(shapeGraphic.raycastTarget, Is.False,
                        "The non-color cue must remain visible without stealing the action raycast.");
                    Assert.That(cancel.interactable, Is.True);
                    Assert.That(store.interactable, Is.True);
                }

                var cancelCount = 0;
                var storeCount = 0;
                harness.Action.CancelRequested += () => cancelCount++;
                harness.Action.StoreRequested += () => storeCount++;
                harness.Action.Show(true, false, PlacementFeedbackKey.Occupied);
                Canvas.ForceUpdateCanvases();
                yield return null;
                var cancelRecorder = cancel.gameObject.AddComponent<UiTouchRecorder>();
                yield return TapUi(harness.InputFixture, harness.Touch, harness.ActiveTouchIds, 501,
                    harness.Camera, harness.EventSystem, cancel);
                Assert.That(cancelCount, Is.EqualTo(1));
                Assert.That(storeCount, Is.Zero);
                cancelRecorder.AssertCompleteClick(501);

                harness.Action.Show(true, false, PlacementFeedbackKey.Blocked);
                var storeRecorder = store.gameObject.AddComponent<UiTouchRecorder>();
                yield return TapUi(harness.InputFixture, harness.Touch, harness.ActiveTouchIds, 502,
                    harness.Camera, harness.EventSystem, store);
                Assert.That(cancelCount, Is.EqualTo(1));
                Assert.That(storeCount, Is.EqualTo(1));
                storeRecorder.AssertCompleteClick(502);
            }
            finally
            {
                harness.Dispose();
            }
            harness.AssertDisposed();
        }

        [UnityTest]
        public IEnumerator GeneratedProductionViews_ReducedMotionPauseAndRapidReverseFinishOneStableRaycastOwner()
        {
            var original = Time.timeScale;
            var responsiveCase = CanonicalResponsiveCases()[0];
            var harness = new ProductionUiHarness(responsiveCase);
            try
            {
                Time.timeScale = 0f;
                harness.Begin();
                harness.Configure(new UiTransitionRunner(() => false));
                var selections = 0;
                var cancels = 0;
                var dismisses = 0;
                harness.Catalogue.Selected += _ => selections++;
                harness.Action.CancelRequested += () => cancels++;
                harness.Modal.DismissRequested += () => dismisses++;
                var catalogueGroup = harness.CatalogueRoot.GetComponent<CanvasGroup>();
                var actionGroup = harness.ActionRoot.GetComponent<CanvasGroup>();
                var modalGroup = harness.ModalRoot.GetComponent<CanvasGroup>();
                var viewGroups = new[] { catalogueGroup, actionGroup, modalGroup };
                var tile = harness.CatalogueRoot
                    .GetComponentsInChildren<DecorationCatalogueTileView>(false)
                    .First(candidate => candidate.Definition != null)
                    .GetComponent<Button>();
                var actionCancel = harness.ActionRoot.transform.Find("ActionPanel/CancelButton")
                    .GetComponent<Button>();
                var modalCancel = harness.ModalRoot.transform.Find("SafeArea/Content/CancelButton")
                    .GetComponent<Button>();
                var catalogueCollapse = harness.CatalogueRoot.transform
                    .Find("ExpandedSheet/CollapseButton").GetComponent<Button>();
                var catalogueExpand = harness.CatalogueRoot.transform
                    .Find("CollapsedHandle").GetComponent<Button>();

                harness.Catalogue.Hide();
                harness.Action.Hide();
                harness.Modal.CloseForOwnerShutdown();
                yield return WaitForAlpha(catalogueGroup, 0f, "Catalogue initial hide");
                yield return WaitForAlpha(actionGroup, 0f, "Action initial hide");
                Assert.That(modalGroup.alpha, Is.Zero);
                AssertUsableViewOwner(viewGroups, null, "initial hidden views");

                harness.Catalogue.ShowCatalogue();
                yield return WaitForAlphaBetween(catalogueGroup, 0.25f, 0.75f,
                    "Catalogue must be interrupted inside its 0.16s transition window.");
                var catalogueMidAlpha = catalogueGroup.alpha;
                harness.Catalogue.Hide();
                yield return null;
                Assert.That(catalogueGroup.alpha, Is.LessThan(catalogueMidAlpha));
                harness.Catalogue.ShowCatalogue();
                yield return WaitForAlpha(catalogueGroup, 1f, "Catalogue final show");
                Assert.That(actionGroup.alpha, Is.Zero);
                Assert.That(modalGroup.alpha, Is.Zero);
                AssertUsableViewOwner(viewGroups, catalogueGroup, "normal Catalogue final show");
                var catalogueRecorder = tile.gameObject.AddComponent<UiTouchRecorder>();
                yield return TapUi(harness.InputFixture, harness.Touch, harness.ActiveTouchIds, 601,
                    harness.Camera, harness.EventSystem, tile);
                Assert.That(selections, Is.EqualTo(1));
                catalogueRecorder.AssertCompleteClick(601);
                yield return AssertAlphaRemainsStable(catalogueGroup, 1f, 0.20f,
                    "Catalogue must not reverse after settling.");
                AssertUsableViewOwner(viewGroups, catalogueGroup,
                    "normal Catalogue post-duration owner");

                harness.Catalogue.Hide();
                yield return WaitForAlpha(catalogueGroup, 0f, "Catalogue hidden before Action");
                harness.Action.Show(false, true, PlacementFeedbackKey.None);
                yield return WaitForAlphaBetween(actionGroup, 0.25f, 0.75f,
                    "Action must be interrupted inside its 0.12s transition window.");
                var actionMidAlpha = actionGroup.alpha;
                harness.Action.Hide();
                yield return null;
                Assert.That(actionGroup.alpha, Is.LessThan(actionMidAlpha));
                harness.Action.Show(false, true, PlacementFeedbackKey.None);
                yield return WaitForAlpha(actionGroup, 1f, "Action final show");
                Assert.That(catalogueGroup.alpha, Is.Zero);
                Assert.That(modalGroup.alpha, Is.Zero);
                AssertUsableViewOwner(viewGroups, actionGroup, "normal Action final show");
                var actionRecorder = actionCancel.gameObject.AddComponent<UiTouchRecorder>();
                yield return TapUi(harness.InputFixture, harness.Touch, harness.ActiveTouchIds, 602,
                    harness.Camera, harness.EventSystem, actionCancel);
                actionRecorder.AssertCompleteClick(602);
                Assert.That(cancels, Is.EqualTo(1));
                yield return AssertAlphaRemainsStable(actionGroup, 1f, 0.16f,
                    "Action must not reverse after settling.");
                AssertUsableViewOwner(viewGroups, actionGroup,
                    "normal Action post-duration owner");

                harness.Action.Hide();
                yield return WaitForAlpha(actionGroup, 0f, "Action hidden before Modal");
                harness.Modal.Show(harness.CatalogueAsset.Entries[0].Definition);
                yield return WaitForAlphaBetween(modalGroup, 0.25f, 0.75f,
                    "Modal must be interrupted inside its 0.16s transition window.");
                harness.Modal.CloseForOwnerShutdown();
                Assert.That(modalGroup.alpha, Is.Zero);
                harness.Modal.Show(harness.CatalogueAsset.Entries[0].Definition);
                yield return WaitForAlpha(modalGroup, 1f, "Modal final show");
                Assert.That(catalogueGroup.alpha, Is.Zero);
                Assert.That(actionGroup.alpha, Is.Zero);
                AssertUsableViewOwner(viewGroups, modalGroup, "normal Modal final show");
                var modalRecorder = modalCancel.gameObject.AddComponent<UiTouchRecorder>();
                yield return TapUi(harness.InputFixture, harness.Touch, harness.ActiveTouchIds, 603,
                    harness.Camera, harness.EventSystem, modalCancel);
                modalRecorder.AssertCompleteClick(603);
                Assert.That(dismisses, Is.EqualTo(1));
                Assert.That(harness.Modal.IsOpen, Is.False);
                yield return AssertAlphaRemainsStable(modalGroup, 0f, 0.20f,
                    "Modal must not reopen after Cancel settles.");
                AssertUsableViewOwner(viewGroups, null, "normal Modal Cancel post-duration");

                var reduced = new UiTransitionRunner(() => true);
                harness.Configure(reduced);
                var selectionBeforeReduced = selections;
                harness.Catalogue.ShowCatalogue();
                Assert.That(catalogueGroup.alpha, Is.EqualTo(1f));
                Assert.That(actionGroup.alpha, Is.EqualTo(0f));
                AssertUsableViewOwner(viewGroups, catalogueGroup, "reduced Catalogue show");
                Canvas.ForceUpdateCanvases();
                yield return null;
                var reducedCollapseRecorder = catalogueCollapse.gameObject.AddComponent<UiTouchRecorder>();
                yield return TapUi(harness.InputFixture, harness.Touch, harness.ActiveTouchIds, 604,
                    harness.Camera, harness.EventSystem, catalogueCollapse);
                reducedCollapseRecorder.AssertCompleteClick(604);
                Assert.That(harness.Catalogue.IsCollapsed, Is.True);
                Assert.That(catalogueGroup.alpha, Is.EqualTo(1f));
                AssertUsableViewOwner(viewGroups, catalogueGroup, "reduced Catalogue collapse");

                var reducedExpandRecorder = catalogueExpand.gameObject.AddComponent<UiTouchRecorder>();
                yield return TapUi(harness.InputFixture, harness.Touch, harness.ActiveTouchIds, 605,
                    harness.Camera, harness.EventSystem, catalogueExpand);
                reducedExpandRecorder.AssertCompleteClick(605);
                Assert.That(harness.Catalogue.IsCollapsed, Is.False);
                Assert.That(catalogueGroup.alpha, Is.EqualTo(1f));
                AssertUsableViewOwner(viewGroups, catalogueGroup, "reduced Catalogue expand");

                var reducedTileRecorder = tile.gameObject.AddComponent<UiTouchRecorder>();
                yield return TapUi(harness.InputFixture, harness.Touch, harness.ActiveTouchIds, 606,
                    harness.Camera, harness.EventSystem, tile);
                reducedTileRecorder.AssertCompleteClick(606);
                Assert.That(selections, Is.EqualTo(selectionBeforeReduced + 1));
                AssertUsableViewOwner(viewGroups, catalogueGroup, "reduced Catalogue selection");
                harness.Catalogue.Hide();
                Assert.That(catalogueGroup.alpha, Is.EqualTo(0f));
                AssertUsableViewOwner(viewGroups, null, "reduced Catalogue hide");
                yield return AssertAlphaRemainsStable(catalogueGroup, 0f, 0.20f,
                    "Reduced Catalogue show/collapse/expand/hide must not reverse after normal duration.");
                AssertUsableViewOwner(viewGroups, null,
                    "reduced Catalogue post-duration hidden state");

                var cancelsBeforeReduced = cancels;
                harness.Action.Show(false, true, PlacementFeedbackKey.None);
                harness.Action.Hide();
                harness.Action.Show(false, true, PlacementFeedbackKey.None);
                Assert.That(catalogueGroup.alpha, Is.EqualTo(0f));
                Assert.That(actionGroup.alpha, Is.EqualTo(1f));
                AssertUsableViewOwner(viewGroups, actionGroup, "reduced Action show/hide/show");
                Canvas.ForceUpdateCanvases();
                yield return null;
                var reducedActionRecorder = actionCancel.gameObject.AddComponent<UiTouchRecorder>();
                yield return TapUi(harness.InputFixture, harness.Touch, harness.ActiveTouchIds, 607,
                    harness.Camera, harness.EventSystem, actionCancel);
                reducedActionRecorder.AssertCompleteClick(607);
                Assert.That(cancels, Is.EqualTo(cancelsBeforeReduced + 1));
                yield return AssertAlphaRemainsStable(actionGroup, 1f, 0.20f,
                    "Reduced Action show/hide/show must not reverse after normal duration.");
                AssertUsableViewOwner(viewGroups, actionGroup,
                    "reduced Action post-duration owner");

                harness.Action.Hide();
                AssertUsableViewOwner(viewGroups, null, "reduced Action hide");
                var dismissesBeforeReduced = dismisses;
                harness.Modal.Show(harness.CatalogueAsset.Entries[0].Definition);
                AssertUsableViewOwner(viewGroups, modalGroup, "reduced Modal show");
                harness.Modal.CloseForOwnerShutdown();
                AssertUsableViewOwner(viewGroups, null, "reduced Modal owner close");
                harness.Modal.Show(harness.CatalogueAsset.Entries[0].Definition);
                Assert.That(catalogueGroup.alpha, Is.EqualTo(0f));
                Assert.That(actionGroup.alpha, Is.EqualTo(0f));
                Assert.That(modalGroup.alpha, Is.EqualTo(1f));
                AssertUsableViewOwner(viewGroups, modalGroup, "reduced Modal reopen");
                Canvas.ForceUpdateCanvases();
                yield return null;
                var reducedModalRecorder = modalCancel.gameObject.AddComponent<UiTouchRecorder>();
                yield return TapUi(harness.InputFixture, harness.Touch, harness.ActiveTouchIds, 608,
                    harness.Camera, harness.EventSystem, modalCancel);
                reducedModalRecorder.AssertCompleteClick(608);
                Assert.That(dismisses, Is.EqualTo(dismissesBeforeReduced + 1));
                Assert.That(harness.Modal.IsOpen, Is.False);
                Assert.That(modalGroup.alpha, Is.EqualTo(0f));
                AssertUsableViewOwner(viewGroups, null, "reduced Modal Cancel");
                yield return AssertAlphaRemainsStable(modalGroup, 0f, 0.20f,
                    "Reduced Modal owner-close/reopen/Cancel must not reverse after normal duration.");
                AssertUsableViewOwner(viewGroups, null,
                    "reduced Modal post-duration canceled state");

                Assert.That(selections, Is.EqualTo(2));
                Assert.That(cancels, Is.EqualTo(2));
                Assert.That(dismisses, Is.EqualTo(2));
                Assert.That(harness.Boundary.CanProcessScenePointer(987), Is.True,
                    "Owner-close/reopen/Cancel must leave no stale scene block.");
            }
            finally
            {
                try
                {
                    harness.Dispose();
                }
                finally
                {
                    Time.timeScale = original;
                }
            }
            harness.AssertDisposed();
        }

        private IEnumerator RunResponsiveTouchBranch(ResponsiveCase responsiveCase)
        {
            var external = ExternalUiSnapshot.CaptureAll();
            var input = new EmbeddedInputFixture();
            var inputReady = false;
            RenderTexture target = null;
            GameObject cameraRoot = null;
            GameObject canvasRoot = null;
            GameObject eventRoot = null;
            InputSystemUIInputModule module = null;
            Touchscreen touch = null;
            GameObject[] roots = null;
            var activeTouchIds = new HashSet<int>();
            var moduleUnbound = false;
            var eventModuleDestroyed = false;
            try
            {
                external.DisableForIsolation();
                try
                {
                    input.Begin();
                    inputReady = true;

                target = new RenderTexture(responsiveCase.Width, responsiveCase.Height, 24)
                {
                    name = "Task9_" + responsiveCase.Label,
                    antiAliasing = 1
                };
                target.Create();
                cameraRoot = new GameObject("Task9UICamera_" + responsiveCase.Label,
                    typeof(UnityEngine.Camera));
                var camera = cameraRoot.GetComponent<UnityEngine.Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.orthographic = true;
                camera.targetTexture = target;

                canvasRoot = new GameObject("Task9UICanvas_" + responsiveCase.Label,
                    typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = canvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                var scaler = canvasRoot.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                eventRoot = new GameObject("Task9UIEventSystem_" + responsiveCase.Label);
                eventRoot.SetActive(false);
                var eventSystem = eventRoot.AddComponent<EventSystem>();
                module = eventRoot.AddComponent<InputSystemUIInputModule>();
                module.UnassignActions();
                module.AssignDefaultActions();
                AssertModuleActionsAssigned(module);
                eventRoot.SetActive(true);

                var prefabs = LoadProductionUiPrefabs();
                roots = prefabs.Select(prefab => UnityEngine.Object.Instantiate(prefab, canvasRoot.transform))
                    .ToArray();
                foreach (var root in roots) root.SetActive(true);
                var catalogueAsset = LoadEditorAsset<DecorationCatalogueAsset>(
                    "Assets/Art/Phase6/Catalogues/DC_Phase6Decoration.asset");
                var boundary = new UiPointerBoundary();
                var runner = new UiTransitionRunner(() => true);
                var catalogue = roots[0].GetComponent<DecorationCatalogueView>();
                var action = roots[1].GetComponent<DecorationActionBarView>();
                var modal = roots[2].GetComponent<DecorationStoreModalView>();
                foreach (var safeArea in roots.Select(root => root.GetComponentInChildren<SafeAreaContainer>(true)))
                {
                    Assert.That(safeArea, Is.Not.Null);
                    safeArea.AutoApplyRuntimeSafeArea = false;
                    safeArea.ApplySafeArea(
                        responsiveCase.SafeArea,
                        new Vector2(responsiveCase.Width, responsiveCase.Height));
                }
                catalogue.Configure(boundary, runner);
                catalogue.Bind(catalogueAsset);
                catalogue.ShowCatalogue();
                action.Configure(boundary, runner);
                action.Hide();
                modal.Configure(new UiNavigationCoordinator(),
                    new UiPauseCoordinator(new FakeGameTimeService()), boundary, runner);
                modal.CloseForOwnerShutdown();
                Canvas.ForceUpdateCanvases();
                yield return null;

                Assert.That(canvas.renderingDisplaySize.x,
                    Is.EqualTo(responsiveCase.Width).Within(0.5f), responsiveCase.Label);
                Assert.That(canvas.renderingDisplaySize.y,
                    Is.EqualTo(responsiveCase.Height).Within(0.5f), responsiveCase.Label);
                Assert.That(camera.pixelWidth, Is.EqualTo(responsiveCase.Width), responsiveCase.Label);
                Assert.That(camera.pixelHeight, Is.EqualTo(responsiveCase.Height), responsiveCase.Label);

                var tiles = roots[0].GetComponentsInChildren<DecorationCatalogueTileView>(false)
                    .Where(tile => tile.Definition != null).OrderBy(tile => tile.name).ToArray();
                Assert.That(tiles, Has.Length.EqualTo(4));
                var catalogueButtons = tiles.Select(tile => tile.GetComponent<Button>()).ToArray();
                var actionButtons = new[]
                {
                    roots[1].transform.Find("ActionPanel/RotateButton").GetComponent<Button>(),
                    roots[1].transform.Find("ActionPanel/CancelButton").GetComponent<Button>(),
                    roots[1].transform.Find("ActionPanel/ConfirmButton").GetComponent<Button>()
                };
                var essentialCount = catalogueButtons.Length + actionButtons.Length + 2;
                var logicalTargetEvidence = new List<string>(essentialCount);
                foreach (var button in catalogueButtons)
                {
                    logicalTargetEvidence.Add(AssertRenderTargetButton(
                        eventSystem, camera, button, responsiveCase.SafeArea));
                }
                AssertNoOverlap(catalogueButtons, camera, responsiveCase.Label);

                touch = InputSystem.AddDevice<Touchscreen>();
                var catalogueCount = 0;
                catalogue.Selected += _ => catalogueCount++;
                var tileRecorder = catalogueButtons[0].gameObject.AddComponent<UiTouchRecorder>();
                yield return TapUi(input, touch, activeTouchIds, 401, camera, eventSystem,
                    catalogueButtons[0]);
                Assert.That(catalogueCount, Is.EqualTo(1));
                tileRecorder.AssertCompleteClick(401);

                catalogue.Hide();
                action.Show(true, true, PlacementFeedbackKey.None);
                Canvas.ForceUpdateCanvases();
                yield return null;
                foreach (var button in actionButtons)
                {
                    logicalTargetEvidence.Add(AssertRenderTargetButton(
                        eventSystem, camera, button, responsiveCase.SafeArea));
                }
                var rotateCount = 0;
                action.RotateRequested += () => rotateCount++;
                var rotateRecorder = actionButtons[0].gameObject.AddComponent<UiTouchRecorder>();
                yield return TapUi(input, touch, activeTouchIds, 402, camera, eventSystem,
                    actionButtons[0]);
                Assert.That(rotateCount, Is.EqualTo(1));
                rotateRecorder.AssertCompleteClick(402);

                var dismissCount = 0;
                modal.DismissRequested += () => dismissCount++;
                action.Hide();
                modal.Show(catalogueAsset.Entries[0].Definition);
                Canvas.ForceUpdateCanvases();
                yield return null;
                var modalCancel = roots[2].transform.Find("SafeArea/Content/CancelButton").GetComponent<Button>();
                var modalStore = roots[2].transform.Find("SafeArea/Content/StoreButton").GetComponent<Button>();
                logicalTargetEvidence.Add(AssertRenderTargetButton(
                    eventSystem, camera, modalCancel, responsiveCase.SafeArea));
                logicalTargetEvidence.Add(AssertRenderTargetButton(
                    eventSystem, camera, modalStore, responsiveCase.SafeArea));
                var modalRecorder = modalCancel.gameObject.AddComponent<UiTouchRecorder>();
                yield return TapUi(input, touch, activeTouchIds, 403, camera, eventSystem, modalCancel);
                Assert.That(dismissCount, Is.EqualTo(1));
                modalRecorder.AssertCompleteClick(403);

                Assert.That(HasLogicalUiFreePoint(eventSystem, responsiveCase), Is.True,
                    responsiveCase.Label + " must leave a non-empty UI-free logical Scene area.");
                Debug.Log($"TASK9_UI_S01 {responsiveCase.Label} canvas={canvas.renderingDisplaySize.x}x{canvas.renderingDisplaySize.y} "
                    + $"logicalTargets=[{string.Join(",", logicalTargetEvidence)}] "
                    + $"topRaycasts={essentialCount} callbacks=1/1/1 moduleUnbind=pending externalRestore=pending");
                }
                finally
                {
                    try
                    {
                        TerminalizeTrackedTouches(input, touch, activeTouchIds);
                    }
                    finally
                    {
                        try
                        {
                            RemoveFixtureTouchscreen(touch);
                        }
                        finally
                        {
                            try
                            {
                                DestroyTestEventModule(
                                    eventRoot,
                                    module,
                                    out moduleUnbound,
                                    out eventModuleDestroyed);
                            }
                            finally
                            {
                                try
                                {
                                    if (inputReady) input.End();
                                }
                                finally
                                {
                                    DestroyResponsiveUi(
                                        roots,
                                        canvasRoot,
                                        cameraRoot,
                                        target);
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                external.Restore();
            }

            external.AssertRestored();
            Assert.That(activeTouchIds, Is.Empty,
                responsiveCase.Label + " must terminalize every actual fixture Touch id.");
            Assert.That(touch == null || !touch.added, Is.True,
                responsiveCase.Label + " fixture Touchscreen must be removed.");
            Assert.That(moduleUnbound, Is.True,
                responsiveCase.Label + " test module actions must be unassigned before destruction.");
            Assert.That(eventModuleDestroyed, Is.True,
                responsiveCase.Label + " test EventSystem/module root must be destroyed immediately.");
            Debug.Log($"TASK9_UI_S01 {responsiveCase.Label} moduleUnbind=ok externalRestore=ok");
        }

        private static IEnumerator TapUi(
            EmbeddedInputFixture input,
            Touchscreen touch,
            ISet<int> activeTouchIds,
            int rawTouchId,
            UnityEngine.Camera camera,
            EventSystem eventSystem,
            Button button)
        {
            Canvas.ForceUpdateCanvases();
            var position = RenderTargetCenter(camera, button);
            var top = TopGraphic(eventSystem, position);
            Assert.That(top, Is.Not.Null, button.name + " must have a top raycast target.");
            Assert.That(top == button.gameObject || top.transform.IsChildOf(button.transform), Is.True,
                button.name + " must own the top raycast before Touch.");
            AssertUniqueButtonOwner(eventSystem, position, button);
            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            Assert.That(EventSystem.current, Is.SameAs(eventSystem),
                button.name + " test EventSystem must be the current UI dispatcher.");
            Assert.That(eventSystem.isActiveAndEnabled && module != null && module.isActiveAndEnabled,
                Is.True, button.name + " test EventSystem/module must be active before Touch.");
            Assert.That(module.point?.action?.enabled, Is.True,
                button.name + " point action must be enabled before Touch.");
            var recorder = button.GetComponents<UiTouchRecorder>().FirstOrDefault();
            Assert.That(recorder, Is.Not.Null,
                button.name + " must expose a test-owned recorder before real Touch delivery.");
            var eventsBefore = recorder.EventCount;
            activeTouchIds.Add(rawTouchId);
            var deviceTimeBefore = touch.lastUpdateTime;
            var beginEventTime = deviceTimeBefore + 0.000001d;
            yield return WaitForCondition(
                () => InputState.currentTime >= beginEventTime,
                2f,
                button.name + " BeginTouch timestamp did not become due.");
            input.BeginContact(touch, rawTouchId, position, beginEventTime);
            var beganContact = touch.touches.FirstOrDefault(contact =>
                contact.touchId.ReadValue() == rawTouchId
                && contact.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began);
            Assert.That(beganContact, Is.Not.Null,
                button.name + " package BeginTouch " + rawTouchId
                    + " did not reach the Touchscreen state; before=" + deviceTimeBefore
                    + " after=" + touch.lastUpdateTime + ".");
            yield return WaitForCondition(
                () => recorder.EventCount >= eventsBefore + 1,
                2f,
                button.name + " did not receive PointerDown from real package Touch "
                    + rawTouchId + ".");
            var endEventTime = touch.lastUpdateTime + 0.000001d;
            yield return WaitForCondition(
                () => InputState.currentTime >= endEventTime,
                2f,
                button.name + " EndTouch timestamp did not become due.");
            input.EndContact(touch, rawTouchId, position, endEventTime);
            activeTouchIds.Remove(rawTouchId);
            yield return WaitForCondition(
                () => recorder.EventCount >= eventsBefore + 3,
                2f,
                button.name + " did not receive PointerUp/Click from real package Touch "
                    + rawTouchId + ".");
            InputSystem.Update();
            yield return null;
        }

        private static void TerminalizeTrackedTouches(
            EmbeddedInputFixture input,
            Touchscreen touch,
            ISet<int> activeTouchIds)
        {
            if (touch == null || !touch.added) return;
            foreach (var touchId in activeTouchIds.ToArray())
            {
                input.CancelContact(touch, touchId, Vector2.zero);
                activeTouchIds.Remove(touchId);
            }
        }

        private static IEnumerator WaitForCondition(
            Func<bool> condition,
            float realtimeTimeout,
            string failureMessage)
        {
            var deadline = Time.realtimeSinceStartup + realtimeTimeout;
            while (!condition() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(condition(), Is.True, failureMessage);
        }

        private static void RemoveFixtureTouchscreen(Touchscreen touch)
        {
            if (touch != null && touch.added) InputSystem.RemoveDevice(touch);
        }

        private static void DestroyTestEventModule(
            GameObject eventRoot,
            InputSystemUIInputModule module,
            out bool moduleUnbound,
            out bool eventModuleDestroyed)
        {
            moduleUnbound = module == null;
            eventModuleDestroyed = eventRoot == null;
            try
            {
                if (module == null) return;
                module.enabled = false;
                if (eventRoot != null) eventRoot.SetActive(false);
                module.UnassignActions();
                moduleUnbound = ModuleActionsAreUnassigned(module);
            }
            finally
            {
                if (eventRoot != null) UnityEngine.Object.DestroyImmediate(eventRoot);
                eventModuleDestroyed = eventRoot == null && module == null;
            }
        }

        private static void DestroyResponsiveUi(
            GameObject[] roots,
            GameObject canvasRoot,
            GameObject cameraRoot,
            RenderTexture target)
        {
            try
            {
                if (roots != null)
                {
                    foreach (var root in roots.Where(root => root != null))
                        UnityEngine.Object.DestroyImmediate(root);
                }
            }
            finally
            {
                try
                {
                    if (canvasRoot != null) UnityEngine.Object.DestroyImmediate(canvasRoot);
                }
                finally
                {
                    try
                    {
                        if (cameraRoot != null) UnityEngine.Object.DestroyImmediate(cameraRoot);
                    }
                    finally
                    {
                        if (target != null)
                        {
                            target.Release();
                            UnityEngine.Object.DestroyImmediate(target);
                        }
                    }
                }
            }
        }

        private static string AssertRenderTargetButton(
            EventSystem eventSystem,
            UnityEngine.Camera camera,
            Button button,
            Rect safeArea)
        {
            var rect = RenderTargetRect(camera, button.GetComponent<RectTransform>());
            Assert.That(rect.xMin, Is.GreaterThanOrEqualTo(safeArea.xMin - 0.5f), button.name);
            Assert.That(rect.xMax, Is.LessThanOrEqualTo(safeArea.xMax + 0.5f), button.name);
            Assert.That(rect.yMin, Is.GreaterThanOrEqualTo(safeArea.yMin - 0.5f), button.name);
            Assert.That(rect.yMax, Is.LessThanOrEqualTo(safeArea.yMax + 0.5f), button.name);
            var buttonRect = button.GetComponent<RectTransform>();
            var canvas = button.GetComponentInParent<Canvas>();
            var canvasRect = (RectTransform)canvas.transform;
            var worldCorners = new Vector3[4];
            buttonRect.GetWorldCorners(worldCorners);
            var logicalCorners = worldCorners
                .Select(corner => (Vector2)canvasRect.InverseTransformPoint(corner))
                .ToArray();
            var logicalRect = Rect.MinMaxRect(
                logicalCorners.Min(point => point.x), logicalCorners.Min(point => point.y),
                logicalCorners.Max(point => point.x), logicalCorners.Max(point => point.y));
            Assert.That(logicalRect.width, Is.GreaterThanOrEqualTo(47.99f),
                button.name + " logical width");
            Assert.That(logicalRect.height, Is.GreaterThanOrEqualTo(48f),
                button.name + " logical height");
            var display = canvas.renderingDisplaySize;
            var canvasLogicalRect = canvasRect.rect;
            var logicalSafeArea = Rect.MinMaxRect(
                Mathf.Lerp(canvasLogicalRect.xMin, canvasLogicalRect.xMax, safeArea.xMin / display.x),
                Mathf.Lerp(canvasLogicalRect.yMin, canvasLogicalRect.yMax, safeArea.yMin / display.y),
                Mathf.Lerp(canvasLogicalRect.xMin, canvasLogicalRect.xMax, safeArea.xMax / display.x),
                Mathf.Lerp(canvasLogicalRect.yMin, canvasLogicalRect.yMax, safeArea.yMax / display.y));
            Assert.That(logicalRect.xMin, Is.GreaterThanOrEqualTo(logicalSafeArea.xMin - 0.1f),
                button.name + " logical Safe Area xMin");
            Assert.That(logicalRect.xMax, Is.LessThanOrEqualTo(logicalSafeArea.xMax + 0.1f),
                button.name + " logical Safe Area xMax");
            Assert.That(logicalRect.yMin, Is.GreaterThanOrEqualTo(logicalSafeArea.yMin - 0.1f),
                button.name + " logical Safe Area yMin");
            Assert.That(logicalRect.yMax, Is.LessThanOrEqualTo(logicalSafeArea.yMax + 0.1f),
                button.name + " logical Safe Area yMax");
            var top = TopGraphic(eventSystem, rect.center);
            Assert.That(top, Is.Not.Null, button.name);
            Assert.That(top == button.gameObject || top.transform.IsChildOf(button.transform), Is.True,
                button.name);
            return button.name + "="
                + logicalRect.width.ToString("0.##", CultureInfo.InvariantCulture) + "x"
                + logicalRect.height.ToString("0.##", CultureInfo.InvariantCulture)
                + ":safe=true";
        }

        private static void AssertNoOverlap(
            IEnumerable<Button> buttons,
            UnityEngine.Camera camera,
            string label)
        {
            var rects = buttons.Select(button => RenderTargetRect(camera,
                button.GetComponent<RectTransform>())).ToArray();
            for (var left = 0; left < rects.Length; left++)
            for (var right = left + 1; right < rects.Length; right++)
            {
                Assert.That(rects[left].Overlaps(rects[right]), Is.False,
                    label + $" tile {left}/{right}");
            }
        }

        private static Vector2 RenderTargetCenter(UnityEngine.Camera camera, Button button) =>
            RenderTargetRect(camera, button.GetComponent<RectTransform>()).center;

        private static Rect RenderTargetRect(UnityEngine.Camera camera, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var pixels = corners.Select(corner => RectTransformUtility.WorldToScreenPoint(camera, corner)).ToArray();
            return Rect.MinMaxRect(
                pixels.Min(point => point.x), pixels.Min(point => point.y),
                pixels.Max(point => point.x), pixels.Max(point => point.y));
        }

        private static GameObject TopGraphic(EventSystem eventSystem, Vector2 position)
        {
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, results);
            return results.FirstOrDefault().gameObject;
        }

        private static void AssertUniqueButtonOwner(
            EventSystem eventSystem,
            Vector2 position,
            Button expected)
        {
            var results = new List<RaycastResult>();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, results);
            Assert.That(results, Is.Not.Empty,
                expected.name + " must have a real EventSystem raycast result at its Touch point.");
            var topOwner = results[0].gameObject.GetComponentInParent<Button>();
            Assert.That(topOwner, Is.SameAs(expected),
                expected.name + " must be the actual EventSystem top Button owner.");
        }

        private static void AssertUsableViewOwner(
            IReadOnlyList<CanvasGroup> knownViewGroups,
            CanvasGroup expected,
            string label)
        {
            var owners = knownViewGroups.Where(group =>
                    group != null
                    && group.gameObject.activeInHierarchy
                    && group.alpha > 0.05f
                    && group.interactable
                    && group.blocksRaycasts)
                .ToArray();
            if (expected == null)
            {
                Assert.That(owners, Is.Empty, label + " must have no usable Decoration view owner.");
            }
            else
            {
                Assert.That(owners, Has.Length.EqualTo(1),
                    label + " must have exactly one usable top-level Decoration view owner.");
                Assert.That(owners[0], Is.SameAs(expected),
                    label + " must be owned by the expected top-level Decoration view.");
            }

            foreach (var group in knownViewGroups.Where(group => group != null && group != expected))
            {
                Assert.That(group.interactable, Is.False,
                    label + ": hidden non-owner " + group.name + " must not be interactable.");
                Assert.That(group.blocksRaycasts, Is.False,
                    label + ": hidden non-owner " + group.name + " must not block raycasts.");
            }
        }

        private static bool HasLogicalUiFreePoint(EventSystem eventSystem, ResponsiveCase responsiveCase)
        {
            for (var y = 1; y < 10; y++)
            for (var x = 1; x < 10; x++)
            {
                var point = new Vector2(
                    responsiveCase.Width * x / 10f,
                    responsiveCase.Height * y / 10f);
                if (TopGraphic(eventSystem, point) == null) return true;
            }
            return false;
        }

        private static void AssertModuleActionsAssigned(InputSystemUIInputModule module)
        {
            Assert.That(module.actionsAsset, Is.Not.Null);
            Assert.That(module.point, Is.Not.Null);
            Assert.That(module.leftClick, Is.Not.Null);
            Assert.That(module.rightClick, Is.Not.Null);
            Assert.That(module.middleClick, Is.Not.Null);
            Assert.That(module.scrollWheel, Is.Not.Null);
            Assert.That(module.move, Is.Not.Null);
            Assert.That(module.submit, Is.Not.Null);
            Assert.That(module.cancel, Is.Not.Null);
            Assert.That(module.trackedDevicePosition, Is.Not.Null);
            Assert.That(module.trackedDeviceOrientation, Is.Not.Null);
        }

        private static void AssertModuleActionsUnassigned(InputSystemUIInputModule module)
        {
            Assert.That(ModuleActionsAreUnassigned(module), Is.True);
        }

        private static bool ModuleActionsAreUnassigned(InputSystemUIInputModule module)
        {
            if (module == null) return true;
            return module.actionsAsset == null
                && module.point == null
                && module.leftClick == null
                && module.rightClick == null
                && module.middleClick == null
                && module.scrollWheel == null
                && module.move == null
                && module.submit == null
                && module.cancel == null
                && module.trackedDevicePosition == null
                && module.trackedDeviceOrientation == null;
        }

        private static GameObject[] LoadProductionUiPrefabs() => new[]
        {
            LoadEditorAsset<GameObject>("Assets/UI/Phase6/Prefabs/PF_UI_DecorationCatalogue.prefab"),
            LoadEditorAsset<GameObject>("Assets/UI/Phase6/Prefabs/PF_UI_DecorationActionBar.prefab"),
            LoadEditorAsset<GameObject>("Assets/UI/Phase6/Prefabs/PF_UI_DecorationStoreModal.prefab")
        };

        private static int GraphemeCount(string value) =>
            StringInfo.ParseCombiningCharacters(value).Length;

        private static ResponsiveCase[] CanonicalResponsiveCases() => new[]
        {
            new ResponsiveCase(1080, 1920, new Rect(24f, 96f, 1032f, 1740f)),
            new ResponsiveCase(720, 1280, new Rect(18f, 64f, 684f, 1152f)),
            new ResponsiveCase(1080, 2400, new Rect(24f, 120f, 1032f, 2160f)),
            new ResponsiveCase(2400, 1080, new Rect(96f, 48f, 2208f, 984f))
        };

        private static void AssertRenderedTextInsideSafeAreaWithoutClipping(
            TMP_Text text,
            UnityEngine.Camera camera,
            Rect safeArea,
            IEnumerable<Button> essentialButtons)
        {
            Canvas.ForceUpdateCanvases();
            text.ForceMeshUpdate(ignoreActiveState: false, forceTextReparsing: true);
            var rendered = RenderedTextRect(camera, text);
            var textRect = RenderTargetRect(camera, text.rectTransform);
            Assert.That(rendered.width, Is.GreaterThan(0.5f), text.name + " rendered width");
            Assert.That(rendered.height, Is.GreaterThan(0.5f), text.name + " rendered height");
            AssertRectInside(safeArea, rendered, text.name + " rendered Safe Area", 0.75f);
            AssertRectInside(textRect, rendered, text.name + " rendered RectTransform", 1f);
            Assert.That(text.isTextOverflowing, Is.False, text.name + " must not overflow");
            Assert.That(text.isTextTruncated, Is.False, text.name + " must not truncate");

            foreach (var mask in text.GetComponentsInParent<RectMask2D>(includeInactive: false))
            {
                AssertRectInside(
                    RenderTargetRect(camera, mask.rectTransform),
                    rendered,
                    text.name + " RectMask2D " + mask.name,
                    1f);
            }
            foreach (var mask in text.GetComponentsInParent<Mask>(includeInactive: false))
            {
                AssertRectInside(
                    RenderTargetRect(camera, mask.rectTransform),
                    rendered,
                    text.name + " Mask " + mask.name,
                    1f);
            }

            foreach (var button in essentialButtons.Where(button =>
                         button != null && button.gameObject.activeInHierarchy))
            {
                if (text.transform.IsChildOf(button.transform)) continue;
                Assert.That(rendered.Overlaps(RenderTargetRect(camera,
                        button.GetComponent<RectTransform>())), Is.False,
                    text.name + " must not overlap " + button.name);
            }
        }

        private static Rect RenderedTextRect(UnityEngine.Camera camera, TMP_Text text)
        {
            var bounds = text.textBounds;
            var corners = new[]
            {
                new Vector3(bounds.min.x, bounds.min.y),
                new Vector3(bounds.min.x, bounds.max.y),
                new Vector3(bounds.max.x, bounds.min.y),
                new Vector3(bounds.max.x, bounds.max.y)
            };
            var pixels = corners.Select(corner => RectTransformUtility.WorldToScreenPoint(
                camera,
                text.rectTransform.TransformPoint(corner))).ToArray();
            return Rect.MinMaxRect(
                pixels.Min(point => point.x), pixels.Min(point => point.y),
                pixels.Max(point => point.x), pixels.Max(point => point.y));
        }

        private static void AssertRectInside(Rect outer, Rect inner, string label, float tolerance = 0.5f)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - tolerance), label + " xMin");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + tolerance), label + " xMax");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - tolerance), label + " yMin");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + tolerance), label + " yMax");
        }

        private static RectInt ReadVisibleAlphaBounds(Sprite sprite)
        {
            var source = sprite.texture;
            var render = RenderTexture.GetTemporary(source.width, source.height, 0,
                RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            var previous = RenderTexture.active;
            Texture2D readable = null;
            try
            {
                Graphics.Blit(source, render);
                RenderTexture.active = render;
                var width = Mathf.RoundToInt(sprite.rect.width);
                var height = Mathf.RoundToInt(sprite.rect.height);
                readable = new Texture2D(width, height, TextureFormat.RGBA32, false);
                readable.ReadPixels(sprite.rect, 0, 0, false);
                readable.Apply(false, false);
                var pixels = readable.GetPixels32();
                var minX = width;
                var minY = height;
                var maxX = -1;
                var maxY = -1;
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a == 0) continue;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }

                return maxX < minX || maxY < minY
                    ? new RectInt(0, 0, 0, 0)
                    : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            finally
            {
                if (readable != null) UnityEngine.Object.DestroyImmediate(readable);
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(render);
            }
        }

        private static IEnumerator WaitForAlphaBetween(
            CanvasGroup group,
            float minimumExclusive,
            float maximumExclusive,
            string label)
        {
            var deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline
                && !(group.alpha > minimumExclusive && group.alpha < maximumExclusive))
            {
                yield return null;
            }
            Assert.That(group.alpha, Is.GreaterThan(minimumExclusive).And.LessThan(maximumExclusive), label);
        }

        private static IEnumerator WaitForAlpha(CanvasGroup group, float expected, string label)
        {
            var deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline
                && Mathf.Abs(group.alpha - expected) > 0.001f)
            {
                yield return null;
            }
            Assert.That(group.alpha, Is.EqualTo(expected).Within(0.001f), label);
        }

        private static IEnumerator AssertAlphaRemainsStable(
            CanvasGroup group,
            float expected,
            float realtimeDuration,
            string label)
        {
            var deadline = Time.realtimeSinceStartup + realtimeDuration;
            while (Time.realtimeSinceStartup < deadline)
            {
                Assert.That(group.alpha, Is.EqualTo(expected).Within(0.001f), label);
                yield return null;
            }
        }

        private static void InvokeAll(ActionFixture fixture)
        {
            fixture.Rotate.onClick.Invoke();
            fixture.Confirm.onClick.Invoke();
            fixture.Cancel.onClick.Invoke();
            fixture.Store.onClick.Invoke();
        }

        private static SortedDictionary<string, Rect> AssertFourTilesInsideContentWithoutOverlap(
            GameObject catalogueRoot)
        {
            var content = catalogueRoot.transform.Find("ExpandedSheet/Content")
                .GetComponent<RectTransform>();
            var contentRect = WorldRect(content);
            var tiles = content.GetComponentsInChildren<DecorationCatalogueTileView>(false)
                .OrderBy(tile => tile.name, StringComparer.Ordinal)
                .ToArray();
            Assert.That(tiles, Has.Length.EqualTo(4));

            var result = new SortedDictionary<string, Rect>(StringComparer.Ordinal);
            foreach (var tile in tiles)
            {
                var tileRect = WorldRect(tile.GetComponent<RectTransform>());
                Assert.That(tileRect.xMin, Is.GreaterThanOrEqualTo(contentRect.xMin - 0.5f),
                    tile.name);
                Assert.That(tileRect.xMax, Is.LessThanOrEqualTo(contentRect.xMax + 0.5f),
                    tile.name);
                Assert.That(tileRect.yMin, Is.GreaterThanOrEqualTo(contentRect.yMin - 0.5f),
                    tile.name);
                Assert.That(tileRect.yMax, Is.LessThanOrEqualTo(contentRect.yMax + 0.5f),
                    tile.name);
                result.Add(tile.name, tileRect);
            }

            var rects = result.ToArray();
            for (var first = 0; first < rects.Length; first++)
            for (var second = first + 1; second < rects.Length; second++)
            {
                Assert.That(rects[first].Value.Overlaps(rects[second].Value), Is.False,
                    rects[first].Key + " overlaps " + rects[second].Key);
            }

            return result;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void AssertRectEqual(Rect actual, Rect expected, string label)
        {
            Assert.That(actual.xMin, Is.EqualTo(expected.xMin).Within(0.01f), label);
            Assert.That(actual.xMax, Is.EqualTo(expected.xMax).Within(0.01f), label);
            Assert.That(actual.yMin, Is.EqualTo(expected.yMin).Within(0.01f), label);
            Assert.That(actual.yMax, Is.EqualTo(expected.yMax).Within(0.01f), label);
        }

        private static T LoadEditorAsset<T>(string path) where T : UnityEngine.Object
        {
            var assetDatabase = Type.GetType("UnityEditor.AssetDatabase, UnityEditor.CoreModule");
            Assert.That(assetDatabase, Is.Not.Null, "PlayMode must run inside the Unity Editor.");
            var method = assetDatabase.GetMethod(
                "LoadAssetAtPath",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(Type) },
                null);
            Assert.That(method, Is.Not.Null);
            var asset = method.Invoke(null, new object[] { path, typeof(T) }) as T;
            Assert.That(asset, Is.Not.Null, path);
            return asset;
        }

        private static DecorationCatalogueEntry CreateEntry(
            FurnitureDefinitionAsset definition,
            Sprite thumbnail)
        {
            var entry = new DecorationCatalogueEntry();
            SetField(entry, "definition", definition);
            SetField(entry, "thumbnail", thumbnail);
            return entry;
        }

        private static DecorationCatalogueAsset CreateCatalogue(
            IEnumerable<DecorationCatalogueEntry> entries)
        {
            var catalogue = ScriptableObject.CreateInstance<DecorationCatalogueAsset>();
            SetField(catalogue, "entries", entries.ToList());
            return catalogue;
        }

        private static FurnitureDefinitionAsset CreateDefinition(
            string id,
            string displayName,
            GameObject prefab,
            int width = 1,
            int depth = 1)
        {
            var definition = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
            definition.name = id.Replace('.', '_');
            SetField(definition, "definitionId", id);
            SetField(definition, "displayName", displayName);
            SetField(definition, "footprintWidth", width);
            SetField(definition, "footprintDepth", depth);
            SetField(definition, "prefab", prefab);
            return definition;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{name}");
            field.SetValue(target, value);
        }

        private static GameObject UiObject(string name, Transform parent = null)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject;
        }

        private static Button CreateButton(string name, Transform parent, Vector2 size)
        {
            var gameObject = UiObject(name, parent);
            gameObject.GetComponent<RectTransform>().sizeDelta = size;
            gameObject.AddComponent<Image>();
            return gameObject.AddComponent<Button>();
        }

        private sealed class PointerRegistrar : IUiPointerOwnershipRegistrar
        {
            public readonly List<int> UiPresses = new List<int>();
            public readonly List<int> Releases = new List<int>();

            public void RegisterUiPointerPress(int pointerId) => UiPresses.Add(pointerId);
            public void RegisterScenePointerPress(int pointerId) { }
            public bool CanProcessScenePointer(int pointerId) => true;
            public void ReleasePointer(int pointerId) => Releases.Add(pointerId);
        }

        private readonly struct ResponsiveCase
        {
            public ResponsiveCase(int width, int height, Rect safeArea)
            {
                Width = width;
                Height = height;
                SafeArea = safeArea;
            }

            public int Width { get; }
            public int Height { get; }
            public Rect SafeArea { get; }
            public string Label => Width + "x" + Height;
        }

        private sealed class ProductionUiHarness : IDisposable
        {
            private readonly ResponsiveCase responsiveCase;
            private readonly ExternalUiSnapshot external;
            private readonly EmbeddedInputFixture input = new EmbeddedInputFixture();
            private bool inputReady;
            private bool moduleUnbound;
            private bool eventModuleDestroyed;
            private bool disposed;

            public ProductionUiHarness(ResponsiveCase responsiveCase)
            {
                this.responsiveCase = responsiveCase;
                external = ExternalUiSnapshot.CaptureAll();
            }

            public HashSet<int> ActiveTouchIds { get; } = new HashSet<int>();
            public EmbeddedInputFixture InputFixture => input;
            public RenderTexture Target { get; private set; }
            public GameObject CameraRoot { get; private set; }
            public UnityEngine.Camera Camera { get; private set; }
            public GameObject CanvasRoot { get; private set; }
            public Canvas Canvas { get; private set; }
            public GameObject EventRoot { get; private set; }
            public EventSystem EventSystem { get; private set; }
            public InputSystemUIInputModule Module { get; private set; }
            public Touchscreen Touch { get; private set; }
            public GameObject[] Roots { get; private set; }
            public DecorationCatalogueView Catalogue { get; private set; }
            public DecorationActionBarView Action { get; private set; }
            public DecorationStoreModalView Modal { get; private set; }
            public DecorationCatalogueAsset CatalogueAsset { get; private set; }
            public UiPointerBoundary Boundary { get; private set; }

            public GameObject CatalogueRoot => Roots[0];
            public GameObject ActionRoot => Roots[1];
            public GameObject ModalRoot => Roots[2];

            public void Begin()
            {
                external.DisableForIsolation();
                input.Begin();
                inputReady = true;

                Target = new RenderTexture(responsiveCase.Width, responsiveCase.Height, 24)
                {
                    name = "Task9Shared_" + responsiveCase.Label,
                    antiAliasing = 1
                };
                Target.Create();
                CameraRoot = new GameObject("Task9SharedCamera_" + responsiveCase.Label,
                    typeof(UnityEngine.Camera));
                Camera = CameraRoot.GetComponent<UnityEngine.Camera>();
                Camera.clearFlags = CameraClearFlags.SolidColor;
                Camera.orthographic = true;
                Camera.targetTexture = Target;

                CanvasRoot = new GameObject("Task9SharedCanvas_" + responsiveCase.Label,
                    typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas = CanvasRoot.GetComponent<Canvas>();
                Canvas.renderMode = RenderMode.ScreenSpaceCamera;
                Canvas.worldCamera = Camera;
                Canvas.planeDistance = 1f;
                var scaler = CanvasRoot.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                EventRoot = new GameObject("Task9SharedEventSystem_" + responsiveCase.Label);
                EventRoot.SetActive(false);
                EventSystem = EventRoot.AddComponent<EventSystem>();
                Module = EventRoot.AddComponent<InputSystemUIInputModule>();
                Module.UnassignActions();
                Module.AssignDefaultActions();
                AssertModuleActionsAssigned(Module);
                EventRoot.SetActive(true);

                Roots = LoadProductionUiPrefabs()
                    .Select(prefab => UnityEngine.Object.Instantiate(prefab, CanvasRoot.transform))
                    .ToArray();
                foreach (var root in Roots) root.SetActive(true);
                foreach (var safeArea in Roots.Select(root =>
                             root.GetComponentInChildren<SafeAreaContainer>(true)))
                {
                    Assert.That(safeArea, Is.Not.Null);
                    safeArea.AutoApplyRuntimeSafeArea = false;
                    safeArea.ApplySafeArea(
                        responsiveCase.SafeArea,
                        new Vector2(responsiveCase.Width, responsiveCase.Height));
                }

                CatalogueAsset = LoadEditorAsset<DecorationCatalogueAsset>(
                    "Assets/Art/Phase6/Catalogues/DC_Phase6Decoration.asset");
                Boundary = new UiPointerBoundary();
                Catalogue = CatalogueRoot.GetComponent<DecorationCatalogueView>();
                Action = ActionRoot.GetComponent<DecorationActionBarView>();
                Modal = ModalRoot.GetComponent<DecorationStoreModalView>();
                Touch = InputSystem.AddDevice<Touchscreen>();
                Canvas.ForceUpdateCanvases();
            }

            public void Configure(UiTransitionRunner runner)
            {
                Catalogue.Configure(Boundary, runner);
                Catalogue.Bind(CatalogueAsset);
                Action.Configure(Boundary, runner);
                Modal.Configure(
                    new UiNavigationCoordinator(),
                    new UiPauseCoordinator(new FakeGameTimeService()),
                    Boundary,
                    runner);
            }

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                try
                {
                    TerminalizeTrackedTouches(input, Touch, ActiveTouchIds);
                }
                finally
                {
                    try
                    {
                        RemoveFixtureTouchscreen(Touch);
                    }
                    finally
                    {
                        try
                        {
                            DestroyTestEventModule(
                                EventRoot,
                                Module,
                                out moduleUnbound,
                                out eventModuleDestroyed);
                        }
                        finally
                        {
                            try
                            {
                                if (inputReady) input.End();
                            }
                            finally
                            {
                                try
                                {
                                    DestroyResponsiveUi(Roots, CanvasRoot, CameraRoot, Target);
                                }
                                finally
                                {
                                    external.Restore();
                                }
                            }
                        }
                    }
                }
            }

            public void AssertDisposed()
            {
                external.AssertRestored();
                Assert.That(ActiveTouchIds, Is.Empty, responsiveCase.Label);
                Assert.That(Touch == null || !Touch.added, Is.True, responsiveCase.Label);
                Assert.That(moduleUnbound, Is.True, responsiveCase.Label);
                Assert.That(eventModuleDestroyed, Is.True, responsiveCase.Label);
            }
        }

        private readonly struct LongCopySample
        {
            public LongCopySample(string source, string longer, bool shortAction)
            {
                Source = source;
                Longer = longer;
                ShortAction = shortAction;
            }

            public string Source { get; }
            public string Longer { get; }
            public bool ShortAction { get; }
        }

        private sealed class EmbeddedInputFixture : InputTestFixture
        {
            public void Begin() => base.Setup();
            public void End() => base.TearDown();
            public void BeginContact(
                Touchscreen touch,
                int touchId,
                Vector2 position,
                double eventTime) =>
                base.BeginTouch(touchId, position, screen: touch, time: eventTime);
            public void EndContact(
                Touchscreen touch,
                int touchId,
                Vector2 position,
                double eventTime) =>
                base.EndTouch(touchId, position, screen: touch, time: eventTime);
            public void CancelContact(Touchscreen touch, int touchId, Vector2 position) =>
                base.CancelTouch(touchId, position, screen: touch);
        }

        private sealed class UiTouchRecorder : MonoBehaviour,
            IPointerDownHandler,
            IPointerUpHandler,
            IPointerClickHandler,
            IBeginDragHandler,
            IDragHandler,
            IEndDragHandler
        {
            private readonly List<string> events = new List<string>();
            private readonly List<int> rawTouchIds = new List<int>();
            private readonly List<int> pointerIds = new List<int>();

            public int EventCount => events.Count;

            public void OnPointerDown(PointerEventData eventData) => Record("Down", eventData);
            public void OnPointerUp(PointerEventData eventData) => Record("Up", eventData);
            public void OnPointerClick(PointerEventData eventData) => Record("Click", eventData);
            public void OnBeginDrag(PointerEventData eventData) => Record("BeginDrag", eventData);
            public void OnDrag(PointerEventData eventData) => Record("Drag", eventData);
            public void OnEndDrag(PointerEventData eventData) => Record("EndDrag", eventData);

            public void AssertCompleteClick(int rawTouchId)
            {
                Assert.That(events, Is.EqualTo(new[] { "Down", "Up", "Click" }));
                Assert.That(rawTouchIds, Is.All.EqualTo(rawTouchId));
                Assert.That(pointerIds.All(pointerId => pointerId != rawTouchId), Is.True);
            }

            private void Record(string eventName, PointerEventData eventData)
            {
                Assert.That(eventData, Is.TypeOf<ExtendedPointerEventData>());
                var extended = (ExtendedPointerEventData)eventData;
                events.Add(eventName);
                rawTouchIds.Add(extended.touchId);
                pointerIds.Add(eventData.pointerId);
            }
        }

        private sealed class ExternalUiSnapshot
        {
            private readonly ModuleSnapshot[] modules;
            private readonly EventSystemSnapshot[] eventSystems;
            private readonly RaycasterSnapshot[] raycasters;

            private ExternalUiSnapshot(
                ModuleSnapshot[] modules,
                EventSystemSnapshot[] eventSystems,
                RaycasterSnapshot[] raycasters)
            {
                this.modules = modules;
                this.eventSystems = eventSystems;
                this.raycasters = raycasters;
            }

            public static ExternalUiSnapshot CaptureAll()
            {
                var modules = UnityEngine.Object.FindObjectsByType<InputSystemUIInputModule>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Select(module => new ModuleSnapshot(module)).ToArray();
                var systems = UnityEngine.Object.FindObjectsByType<EventSystem>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Select(system => new EventSystemSnapshot(system)).ToArray();
                var raycasters = UnityEngine.Object.FindObjectsByType<GraphicRaycaster>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Select(raycaster => new RaycasterSnapshot(raycaster)).ToArray();
                return new ExternalUiSnapshot(modules, systems, raycasters);
            }

            public void DisableForIsolation()
            {
                foreach (var snapshot in modules) snapshot.Module.enabled = false;
                foreach (var snapshot in eventSystems) snapshot.EventSystem.enabled = false;
                foreach (var snapshot in raycasters) snapshot.Raycaster.enabled = false;
            }

            public void Restore()
            {
                foreach (var snapshot in raycasters) snapshot.Restore();
                foreach (var snapshot in eventSystems) snapshot.Restore();
                foreach (var snapshot in modules) snapshot.Restore();
            }

            public void AssertRestored()
            {
                foreach (var snapshot in raycasters) snapshot.AssertRestored();
                foreach (var snapshot in eventSystems) snapshot.AssertRestored();
                foreach (var snapshot in modules) snapshot.AssertRestored();
            }

            private sealed class ModuleSnapshot
            {
                public ModuleSnapshot(InputSystemUIInputModule module)
                {
                    Module = module;
                    Root = module.gameObject;
                    RootActive = Root.activeSelf;
                    Enabled = module.enabled;
                    ActionsAsset = module.actionsAsset;
                    Point = module.point;
                    LeftClick = module.leftClick;
                    RightClick = module.rightClick;
                    MiddleClick = module.middleClick;
                    ScrollWheel = module.scrollWheel;
                    Move = module.move;
                    Submit = module.submit;
                    Cancel = module.cancel;
                    TrackedPosition = module.trackedDevicePosition;
                    TrackedOrientation = module.trackedDeviceOrientation;
                }

                public InputSystemUIInputModule Module { get; }
                private GameObject Root { get; }
                private bool RootActive { get; }
                private bool Enabled { get; }
                private UnityEngine.InputSystem.InputActionAsset ActionsAsset { get; }
                private UnityEngine.InputSystem.InputActionReference Point { get; }
                private UnityEngine.InputSystem.InputActionReference LeftClick { get; }
                private UnityEngine.InputSystem.InputActionReference RightClick { get; }
                private UnityEngine.InputSystem.InputActionReference MiddleClick { get; }
                private UnityEngine.InputSystem.InputActionReference ScrollWheel { get; }
                private UnityEngine.InputSystem.InputActionReference Move { get; }
                private UnityEngine.InputSystem.InputActionReference Submit { get; }
                private UnityEngine.InputSystem.InputActionReference Cancel { get; }
                private UnityEngine.InputSystem.InputActionReference TrackedPosition { get; }
                private UnityEngine.InputSystem.InputActionReference TrackedOrientation { get; }

                public void Restore()
                {
                    if (Module == null) return;
                    Module.enabled = false;
                    Module.actionsAsset = ActionsAsset;
                    Module.point = Point;
                    Module.leftClick = LeftClick;
                    Module.rightClick = RightClick;
                    Module.middleClick = MiddleClick;
                    Module.scrollWheel = ScrollWheel;
                    Module.move = Move;
                    Module.submit = Submit;
                    Module.cancel = Cancel;
                    Module.trackedDevicePosition = TrackedPosition;
                    Module.trackedDeviceOrientation = TrackedOrientation;
                    Root.SetActive(RootActive);
                    Module.enabled = Enabled;
                }

                public void AssertRestored()
                {
                    Assert.That(Module, Is.Not.Null);
                    Assert.That(Root.activeSelf, Is.EqualTo(RootActive));
                    Assert.That(Module.enabled, Is.EqualTo(Enabled));
                    Assert.That(Module.actionsAsset, Is.SameAs(ActionsAsset));
                    Assert.That(Module.point, Is.SameAs(Point));
                    Assert.That(Module.leftClick, Is.SameAs(LeftClick));
                    Assert.That(Module.rightClick, Is.SameAs(RightClick));
                    Assert.That(Module.middleClick, Is.SameAs(MiddleClick));
                    Assert.That(Module.scrollWheel, Is.SameAs(ScrollWheel));
                    Assert.That(Module.move, Is.SameAs(Move));
                    Assert.That(Module.submit, Is.SameAs(Submit));
                    Assert.That(Module.cancel, Is.SameAs(Cancel));
                    Assert.That(Module.trackedDevicePosition, Is.SameAs(TrackedPosition));
                    Assert.That(Module.trackedDeviceOrientation, Is.SameAs(TrackedOrientation));
                }
            }

            private sealed class EventSystemSnapshot
            {
                public EventSystemSnapshot(EventSystem eventSystem)
                {
                    EventSystem = eventSystem;
                    root = eventSystem.gameObject;
                    rootActive = root.activeSelf;
                    enabled = eventSystem.enabled;
                }
                public EventSystem EventSystem { get; }
                private readonly GameObject root;
                private readonly bool rootActive;
                private readonly bool enabled;
                public void Restore()
                {
                    if (EventSystem == null) return;
                    root.SetActive(rootActive);
                    EventSystem.enabled = enabled;
                }
                public void AssertRestored()
                {
                    Assert.That(EventSystem, Is.Not.Null);
                    Assert.That(root.activeSelf, Is.EqualTo(rootActive));
                    Assert.That(EventSystem.enabled, Is.EqualTo(enabled));
                }
            }

            private sealed class RaycasterSnapshot
            {
                public RaycasterSnapshot(GraphicRaycaster raycaster)
                {
                    Raycaster = raycaster;
                    root = raycaster.gameObject;
                    rootActive = root.activeSelf;
                    enabled = raycaster.enabled;
                }
                public GraphicRaycaster Raycaster { get; }
                private readonly GameObject root;
                private readonly bool rootActive;
                private readonly bool enabled;
                public void Restore()
                {
                    if (Raycaster == null) return;
                    root.SetActive(rootActive);
                    Raycaster.enabled = enabled;
                }
                public void AssertRestored()
                {
                    Assert.That(Raycaster, Is.Not.Null);
                    Assert.That(root.activeSelf, Is.EqualTo(rootActive));
                    Assert.That(Raycaster.enabled, Is.EqualTo(enabled));
                }
            }
        }

        private sealed class TileFixture : IDisposable
        {
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

            public TileFixture()
            {
                Root = UiObject("Tile");
                Root.SetActive(false);
                Button = Root.AddComponent<Button>();
                Root.AddComponent<Image>();
                var thumbnailObject = UiObject("Thumbnail", Root.transform);
                var thumbnail = thumbnailObject.AddComponent<Image>();
                Name = UiObject("Name", Root.transform).AddComponent<TextMeshProUGUI>();
                var footprint = UiObject("Footprint", Root.transform).AddComponent<TextMeshProUGUI>();
                Warning = UiObject("WarningLabel", Root.transform).AddComponent<TextMeshProUGUI>();
                WarningShape = UiObject("WarningShape", Root.transform);
                WarningShape.AddComponent<Image>();
                View = Root.AddComponent<DecorationCatalogueTileView>();
                SetField(View, "button", Button);
                SetField(View, "thumbnailImage", thumbnail);
                SetField(View, "nameLabel", Name);
                SetField(View, "footprintLabel", footprint);
                SetField(View, "warningLabel", Warning);
                SetField(View, "warningShape", WarningShape);
                Pointer = new PointerRegistrar();
                Prefab = new GameObject("PF_TileCounter");
                Definition = CreateDefinition("fixture.counter", "Counter Fixture", Prefab);
                Sprite = CreateSprite(owned);
                ValidEntry = CreateEntry(Definition, Sprite);
                owned.Add(Definition);
                owned.Add(Prefab);
                Root.SetActive(true);
            }

            public GameObject Root { get; }
            public Button Button { get; }
            public TextMeshProUGUI Name { get; }
            public TextMeshProUGUI Warning { get; }
            public GameObject WarningShape { get; }
            public DecorationCatalogueTileView View { get; }
            public PointerRegistrar Pointer { get; }
            public GameObject Prefab { get; }
            public FurnitureDefinitionAsset Definition { get; }
            public Sprite Sprite { get; }
            public DecorationCatalogueEntry ValidEntry { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                foreach (var item in owned.Where(item => item != null))
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }
        }

        private sealed class CatalogueFixture : IDisposable
        {
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

            public CatalogueFixture()
            {
                Root = UiObject("Catalogue");
                Group = Root.AddComponent<CanvasGroup>();
                View = Root.AddComponent<DecorationCatalogueView>();
                Expanded = UiObject("ExpandedSheet", Root.transform);
                Expanded.AddComponent<Image>();
                CollapseButton = CreateButton("CollapseButton", Expanded.transform, new Vector2(48, 48));
                Content = UiObject("Content", Expanded.transform);
                Collapsed = UiObject("CollapsedHandle", Root.transform);
                Collapsed.GetComponent<RectTransform>().sizeDelta = new Vector2(64, 64);
                ExpandButton = Collapsed.AddComponent<Button>();
                Collapsed.AddComponent<Image>();
                var templateObject = UiObject("TileTemplate", Content.transform);
                templateObject.AddComponent<Image>();
                var templateButton = templateObject.AddComponent<Button>();
                var templateThumbnail = UiObject("Thumbnail", templateObject.transform)
                    .AddComponent<Image>();
                var templateName = UiObject("Name", templateObject.transform)
                    .AddComponent<TextMeshProUGUI>();
                var templateFootprint = UiObject("Footprint", templateObject.transform)
                    .AddComponent<TextMeshProUGUI>();
                var templateWarning = UiObject("WarningLabel", templateObject.transform)
                    .AddComponent<TextMeshProUGUI>();
                var warningShape = UiObject("WarningShape", templateObject.transform);
                warningShape.AddComponent<Image>();
                var template = templateObject.AddComponent<DecorationCatalogueTileView>();
                SetField(template, "button", templateButton);
                SetField(template, "thumbnailImage", templateThumbnail);
                SetField(template, "nameLabel", templateName);
                SetField(template, "footprintLabel", templateFootprint);
                SetField(template, "warningLabel", templateWarning);
                SetField(template, "warningShape", warningShape);
                templateObject.SetActive(false);

                SetField(View, "canvasGroup", Group);
                SetField(View, "expandedRoot", Expanded);
                SetField(View, "collapsedRoot", Collapsed);
                SetField(View, "collapseButton", CollapseButton);
                SetField(View, "collapsedHandleButton", ExpandButton);
                SetField(View, "contentRoot", Content.transform);
                SetField(View, "tileTemplate", template);
                Pointer = new PointerRegistrar();

                Definitions = new FurnitureDefinitionAsset[4];
                var entries = new List<DecorationCatalogueEntry>();
                for (var index = 0; index < 4; index++)
                {
                    var prefab = new GameObject("PF_Counter_" + index);
                    var definition = CreateDefinition(
                        "counter.fixture." + index,
                        "Counter Fixture " + index,
                        prefab,
                        index < 3 ? 1 : 2,
                        index + 1);
                    var sprite = CreateSprite(owned);
                    owned.Add(prefab);
                    owned.Add(definition);
                    Definitions[index] = definition;
                    entries.Add(CreateEntry(definition, sprite));
                }

                Catalogue = CreateCatalogue(entries);
                owned.Add(Catalogue);
            }

            public GameObject Root { get; }
            public CanvasGroup Group { get; }
            public DecorationCatalogueView View { get; }
            public GameObject Expanded { get; }
            public GameObject Collapsed { get; }
            public GameObject Content { get; }
            public Button CollapseButton { get; }
            public Button ExpandButton { get; }
            public PointerRegistrar Pointer { get; }
            public FurnitureDefinitionAsset[] Definitions { get; }
            public DecorationCatalogueAsset Catalogue { get; }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                foreach (var item in owned.Where(item => item != null))
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }
        }

        private sealed class ActionFixture : IDisposable
        {
            public ActionFixture()
            {
                Root = UiObject("ActionBar");
                Group = Root.AddComponent<CanvasGroup>();
                View = Root.AddComponent<DecorationActionBarView>();
                Store = CreateButton("StoreButton", Root.transform, new Vector2(160, 64));
                Rotate = CreateButton("RotateButton", Root.transform, new Vector2(160, 64));
                Cancel = CreateButton("CancelButton", Root.transform, new Vector2(160, 64));
                Confirm = CreateButton("ConfirmButton", Root.transform, new Vector2(160, 64));
                Feedback = UiObject("Feedback", Root.transform).AddComponent<TextMeshProUGUI>();
                StateShape = UiObject("StateShape", Root.transform);
                StateShape.AddComponent<Image>();
                SetField(View, "canvasGroup", Group);
                SetField(View, "storeButton", Store);
                SetField(View, "rotateButton", Rotate);
                SetField(View, "cancelButton", Cancel);
                SetField(View, "confirmButton", Confirm);
                SetField(View, "feedbackLabel", Feedback);
                SetField(View, "feedbackStateShape", StateShape);
                Pointer = new PointerRegistrar();
            }

            public GameObject Root { get; }
            public CanvasGroup Group { get; }
            public DecorationActionBarView View { get; }
            public Button Store { get; }
            public Button Rotate { get; }
            public Button Cancel { get; }
            public Button Confirm { get; }
            public TextMeshProUGUI Feedback { get; }
            public GameObject StateShape { get; }
            public PointerRegistrar Pointer { get; }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);
        }

        private sealed class ModalFixture : IDisposable
        {
            private readonly GameObject prefab;

            public ModalFixture()
            {
                Root = UiObject("StoreModal");
                Root.AddComponent<CanvasGroup>();
                var modal = Root.AddComponent<AnimalCafeModalView>();
                View = Root.AddComponent<DecorationStoreModalView>();
                Blocker = CreateButton("ModalBlocker", Root.transform, new Vector2(1080, 1920));
                Cancel = CreateButton("CancelButton", Root.transform, new Vector2(220, 64));
                Confirm = CreateButton("StoreButton", Root.transform, new Vector2(220, 64));
                Title = UiObject("Title", Root.transform).AddComponent<TextMeshProUGUI>();
                Body = UiObject("Body", Root.transform).AddComponent<TextMeshProUGUI>();
                SetField(View, "modalView", modal);
                SetField(View, "confirmButton", Confirm);
                SetField(View, "cancelButton", Cancel);
                SetField(View, "modalBlocker", Blocker);
                SetField(View, "canvasGroup", Root.GetComponent<CanvasGroup>());
                SetField(View, "titleLabel", Title);
                SetField(View, "bodyLabel", Body);
                Navigation = new UiNavigationCoordinator();
                Boundary = new UiPointerBoundary();
                GameTime = new FakeGameTimeService();
                prefab = new GameObject("PF_ModalCounter");
                Definition = CreateDefinition("modal.counter", "Modal Counter", prefab);
                View.ConfirmRequested += () => ConfirmCount++;
                View.DismissRequested += () => DismissCount++;
            }

            public GameObject Root { get; }
            public DecorationStoreModalView View { get; }
            public Button Blocker { get; }
            public Button Cancel { get; }
            public Button Confirm { get; }
            public TextMeshProUGUI Title { get; }
            public TextMeshProUGUI Body { get; }
            public UiNavigationCoordinator Navigation { get; }
            public UiPointerBoundary Boundary { get; }
            public FakeGameTimeService GameTime { get; }
            public FurnitureDefinitionAsset Definition { get; }
            public int ConfirmCount { get; private set; }
            public int DismissCount { get; private set; }

            public void Configure(bool reducedMotion)
            {
                View.Configure(
                    Navigation,
                    new UiPauseCoordinator(GameTime),
                    Boundary,
                    new UiTransitionRunner(() => reducedMotion));
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Root);
                UnityEngine.Object.DestroyImmediate(Definition);
                UnityEngine.Object.DestroyImmediate(prefab);
            }
        }

        private sealed class FakeGameTimeService : IGameTimeService
        {
            public GameSpeed CurrentSpeed { get; private set; } = GameSpeed.Paused;
            public int SetRequests { get; private set; }

            public bool TrySetSpeed(GameSpeed speed)
            {
                SetRequests++;
                CurrentSpeed = speed;
                return true;
            }
        }

        private static Sprite CreateSprite(ICollection<UnityEngine.Object> owned)
        {
            var texture = new Texture2D(2, 2);
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            owned.Add(sprite);
            owned.Add(texture);
            return sprite;
        }
    }
}

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
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
    /// <summary>Owner-approved compact Catalogue checks against the real MainCafe fixture.
    /// 使用真实 MainCafe 验证45%面板、紧凑卡片和48 logical触控范围。</summary>
    public sealed class P8RCompactCatalogueTests
    {
        private Scene ownedScene;
        private float previousTimeScale;
        private Vector2? previousLogicalViewport;

        [SetUp]
        public void RecordRuntimeBoundary()
        {
            ownedScene = default;
            previousTimeScale = Time.timeScale;
            previousLogicalViewport = P8RMobileMetrics.EditorLogicalViewportOverride;
        }

        [UnityTearDown]
        public IEnumerator ReleaseOwnedScene()
        {
            if (ownedScene.IsValid() && ownedScene.isLoaded)
            {
                var inputAssets = Phase8SceneInputTestCleanup.CaptureAssets(ownedScene);
                foreach (var controller in ownedScene.GetRootGameObjects()
                             .SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true)))
                {
                    controller.enabled = false;
                }

                SceneManager.SetActiveScene(SceneManager.CreateScene("P8RCompactCatalogueCleanup"));
                var unload = SceneManager.UnloadSceneAsync(ownedScene);
                while (unload != null && !unload.isDone)
                {
                    yield return null;
                }

                Phase8SceneInputTestCleanup.DisposeReleasedAssets(inputAssets);
            }

            P8RMobileMetrics.EditorLogicalViewportOverride = previousLogicalViewport;
            Time.timeScale = previousTimeScale;
            yield return null;
        }

        [UnityTest]
        public IEnumerator InsetSmallPhone_AllFourTabsCapTheSheetAndKeepARealTileTappable()
        {
            var pixels = new Vector2(960, 1704);
            var logical = new Vector2(320, 568);
            var density = 3f;
            var safePixels = new Rect(24, 24, 912, 1656);
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(pixels);
                yield return Load(pixels, logical, safePixels);

                var controller = Find<DecorationModeController>();
                controller.EnterDecorationMode();
                var catalogue = Find<DecorationCatalogueView>();
                var sawOverflowOwnedByAScrollRect = false;
                foreach (var mode in new[]
                         {
                             DecorationModeKind.Furniture,
                             DecorationModeKind.Floor,
                             DecorationModeKind.Wall,
                             DecorationModeKind.WallDecor
                         })
                {
                    Assert.That(controller.TryChangeMode(mode), Is.True, mode + " must remain reachable.");
                    catalogue.ShowCatalogue();
                    yield return Settle();

                    var panel = Box(Field<GameObject>(catalogue, "expandedRoot").transform);
                    AssertInside(panel, safePixels, mode + " expanded sheet");
                    Assert.That(panel.height, Is.LessThanOrEqualTo(safePixels.height * .45f + 1f),
                        mode + " must cap the whole visible sheet at 45 percent of available safe height.");

                    var tabs = Find<DecorationModeTabsView>().GetComponentsInChildren<Button>().ToArray();
                    var fixedButtons = tabs.Concat(new[] { Field<Button>(catalogue, "collapseButton") })
                        .Where(button => button != null && button.gameObject.activeInHierarchy).ToArray();
                    AssertTargets(fixedButtons, density, safePixels);
                    AssertNoOverlap(fixedButtons);

                    var viewport = Box(catalogue.VerticalScroll.viewport);
                    AssertInside(viewport, panel, mode + " visible Catalogue viewport");
                    Assert.That(viewport.height / density, Is.GreaterThanOrEqualTo(47.9f),
                        mode + " must keep at least one complete logical tap-height inside the clipped viewport.");

                    var tiles = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>()
                        .Where(tile => !string.IsNullOrEmpty(tile.ItemId) && tile.gameObject.activeInHierarchy).ToArray();
                    Assert.That(tiles, Is.Not.Empty, mode + " must retain real production Catalogue content.");
                    foreach (var tile in tiles)
                    {
                        var logicalUnit = P8RMobileMetrics.For(tile).Units(1);
                        var tileRect = (RectTransform)tile.transform;
                        Assert.That(tileRect.rect.width / logicalUnit, Is.EqualTo(68f).Within(.05f),
                            mode + " card width must use the approved compact size.");
                        Assert.That(tileRect.rect.height / logicalUnit, Is.EqualTo(84f).Within(.05f),
                            mode + " card height must use the approved compact size.");
                        var caption = Field<TMP_Text>(tile, "nameLabel");
                        if (caption != null && caption.gameObject.activeInHierarchy)
                        {
                            caption.ForceMeshUpdate(true, true);
                            Assert.That(caption.fontSize / logicalUnit, Is.EqualTo(11.5f).Within(.05f),
                                mode + " visible caption must use the approved compact type size.");
                            Assert.That(caption.rectTransform.rect.width / logicalUnit, Is.EqualTo(62f).Within(.05f),
                                mode + " named caption width must keep the readable horizontal inset.");
                            Assert.That(caption.rectTransform.rect.height / logicalUnit, Is.EqualTo(34f).Within(.05f),
                                mode + " named caption must release vertical space to the thumbnail.");
                            var well = tile.transform.Find("ThumbnailWell") as RectTransform;
                            var thumbnail = Field<Image>(tile, "thumbnailImage").rectTransform;
                            Assert.That(well.rect.width / logicalUnit, Is.EqualTo(58f).Within(.05f));
                            Assert.That(well.rect.height / logicalUnit, Is.EqualTo(40f).Within(.05f),
                                mode + " named thumbnail backing must receive the redistributed height.");
                            Assert.That(thumbnail.rect.width / logicalUnit, Is.EqualTo(52f).Within(.05f));
                            Assert.That(thumbnail.rect.height / logicalUnit, Is.EqualTo(34f).Within(.05f),
                                mode + " named thumbnail preview must receive the redistributed height.");
                            AssertInside(Box(well), Box(tile), mode + " named thumbnail backing");
                            AssertInside(Box(thumbnail), Box(well), mode + " named thumbnail");
                            var preferred = caption.GetPreferredValues(
                                caption.text, caption.rectTransform.rect.width, Mathf.Infinity);
                            Assert.That(caption.isTextTruncated, Is.False,
                                mode + " item " + tile.ItemId + " caption '" + caption.text
                                + "' must render every glyph; rect="
                                + (caption.rectTransform.rect.size / logicalUnit)
                                + " logical, preferredHeight=" + (preferred.y / logicalUnit)
                                + " logical, lineCount=" + caption.textInfo.lineCount
                                + ", characters=" + caption.textInfo.characterCount + ".");
                        }
                        else
                        {
                            var well = tile.transform.Find("ThumbnailWell") as RectTransform;
                            var thumbnail = Field<Image>(tile, "thumbnailImage").rectTransform;
                            Assert.That(Vector2.Distance(well.rect.size / logicalUnit, new Vector2(56f, 72f)), Is.LessThan(.05f),
                                mode + " no-caption surface backing geometry must remain unchanged.");
                            Assert.That(Vector2.Distance(thumbnail.rect.size / logicalUnit, new Vector2(48f, 64f)), Is.LessThan(.05f),
                                mode + " no-caption surface thumbnail geometry must remain unchanged.");
                        }
                    }

                    foreach (var heading in catalogue.CategoryRows
                                 .Select(row => row.HorizontalScroll.GetComponentInChildren<TMP_Text>(true))
                                 .Where(label => label != null && label.name == "CategoryLabel"
                                     && label.gameObject.activeInHierarchy))
                    {
                        var logicalUnit = P8RMobileMetrics.For(heading).Units(1);
                        heading.ForceMeshUpdate(true, true);
                        Assert.That(heading.fontSize / logicalUnit, Is.EqualTo(14f).Within(.05f),
                            mode + " category heading must use the approved compact type size.");
                        Assert.That(heading.isTextTruncated, Is.False,
                            mode + " category heading must remain readable.");
                    }

                    if (mode == DecorationModeKind.Furniture)
                    {
                        var dimensionTile = tiles.Single(tile => tile.ItemId == "counter.preset.1x2");
                        var dimensionLabel = Field<TMP_Text>(dimensionTile, "nameLabel");
                        dimensionLabel.ForceMeshUpdate(true, true);
                        Assert.That(dimensionTile.ItemId, Is.EqualTo("counter.preset.1x2"),
                            "Display formatting must not mutate the catalogue/save identifier.");
                        var renderedCharacters = dimensionLabel.textInfo.characterInfo
                            .Take(dimensionLabel.textInfo.characterCount).ToArray();
                        var renderedText = new string(renderedCharacters
                            .Select(character => character.character).ToArray());
                        Assert.That(renderedText.Replace("\r", string.Empty).Replace("\n", " "),
                            Is.EqualTo("Counter 1 x 2"),
                            "The UI must render every supported ASCII dimension glyph.");
                        var tokenStart = renderedText.IndexOf("1 x 2", StringComparison.Ordinal);
                        Assert.That(renderedCharacters.Skip(tokenStart).Take(5)
                            .Select(character => character.lineNumber).Distinct().Count(), Is.EqualTo(1),
                            "The 1 x 2 dimension token must stay together on one rendered line.");
                    }
                    var visible = tiles.Select(tile => new
                        {
                            Tile = tile,
                            Visible = Intersection(Box(tile), viewport)
                        })
                        .OrderByDescending(candidate => candidate.Visible.width * candidate.Visible.height)
                        .First();
                    Assert.That(visible.Visible.width / density, Is.GreaterThanOrEqualTo(47.9f),
                        mode + " clipped card needs an actual 48-logical horizontal tap area.");
                    Assert.That(visible.Visible.height / density, Is.GreaterThanOrEqualTo(47.9f),
                        mode + " clipped card needs an actual 48-logical vertical tap area.");
                    AssertRaycastResolvesTo(visible.Tile.GetComponent<Button>(), visible.Visible.center);

                    var verticalOverflow = catalogue.VerticalScroll.content.rect.height
                        > catalogue.VerticalScroll.viewport.rect.height + .5f;
                    if (verticalOverflow)
                    {
                        Assert.That(catalogue.VerticalScroll.vertical, Is.True,
                            mode + " overflow must remain internally vertically scrollable.");
                    }

                    var horizontalOverflow = catalogue.CategoryRows.Any(row =>
                        row.HorizontalScroll.content.rect.width > row.HorizontalScroll.viewport.rect.width + .5f);
                    if (horizontalOverflow)
                    {
                        Assert.That(catalogue.CategoryRows.Where(row =>
                                row.HorizontalScroll.content.rect.width > row.HorizontalScroll.viewport.rect.width + .5f)
                            .All(row => row.HorizontalScroll.horizontal), Is.True,
                            mode + " overflow must remain internally horizontally scrollable.");
                    }

                    sawOverflowOwnedByAScrollRect |= verticalOverflow || horizontalOverflow;
                }

                Assert.That(sawOverflowOwnedByAScrollRect, Is.True,
                    "At least one production tab must exercise clipped content owned by an internal ScrollRect.");
            }
        }

        [UnityTest]
        public IEnumerator ReferencePhone_CompactCardKeepsTwoReadableCaptionLinesAndThumbnailBacking()
        {
            var pixels = new Vector2(1080, 1920);
            var logical = new Vector2(360, 640);
            const float density = 3f;
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(pixels);
                yield return Load(pixels, logical);

                var controller = Find<DecorationModeController>();
                controller.EnterDecorationMode();
                Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
                var catalogue = Find<DecorationCatalogueView>();
                catalogue.ShowCatalogue();
                yield return Settle();

                var tile = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>()
                    .Single(candidate => candidate.ItemId == "wall-decor.shiba-painting.01");
                var units = P8RMobileMetrics.For(tile).Units(1);
                var tileRect = (RectTransform)tile.transform;
                var button = tile.GetComponent<Button>();
                var face = Box(button.image);
                Assert.That(tileRect.rect.width / units, Is.EqualTo(68f).Within(.05f),
                    "The visible card must use the approved 68-logical width.");
                Assert.That(tileRect.rect.height / units, Is.EqualTo(84f).Within(.05f),
                    "The visible card must use the approved 84-logical height.");
                Assert.That(face.width / density, Is.EqualTo(68f).Within(.1f), "The rendered card face must shrink with its target.");
                Assert.That(face.height / density, Is.EqualTo(84f).Within(.1f), "The rendered card face must shrink with its target.");
                AssertTarget(button, density, Screen.safeArea);
                AssertRaycastResolvesTo(button, Box(button).center);

                var label = Field<TMP_Text>(tile, "nameLabel");
                label.ForceMeshUpdate(true, true);
                Assert.That(label.fontSize * label.GetComponentInParent<Canvas>().rootCanvas.scaleFactor / density,
                    Is.EqualTo(11.5f).Within(.05f));
                Assert.That(label.rectTransform.rect.size / units, Is.EqualTo(new Vector2(62f, 34f)));
                Assert.That(label.maxVisibleLines, Is.EqualTo(2));
                Assert.That(label.textInfo.lineCount, Is.EqualTo(2));
                Assert.That(label.isTextTruncated, Is.False);
                var visibleCopy = new string(label.textInfo.characterInfo.Take(label.textInfo.characterCount)
                    .Where(character => character.isVisible).Select(character => character.character).ToArray());
                Assert.That(visibleCopy, Is.EqualTo("ShibaPainting"),
                    "Compact geometry must render every glyph, not only preserve label.text.");

                var well = tile.transform.Find("ThumbnailWell").GetComponent<Image>();
                var thumbnail = Field<Image>(tile, "thumbnailImage");
                Assert.That(well.rectTransform.rect.size / units, Is.EqualTo(new Vector2(58f, 40f)));
                Assert.That(thumbnail.rectTransform.rect.size / units, Is.EqualTo(new Vector2(52f, 34f)));
                AssertInside(Box(well), Box(tile), "Thumbnail backing inside card");
                AssertInside(Box(thumbnail), Box(well), "Thumbnail inside backing");
                Assert.That(Box(well).yMin, Is.GreaterThanOrEqualTo(Box(label).yMax - density * .05f),
                    "Thumbnail backing must not cover the two-line caption reservation.");
            }
        }

        [UnityTest]
        public IEnumerator TallPhone_PickupReturnAndSurfacePreviewActionsRemainReachableUnderTheCap()
        {
            var pixels = new Vector2(1080, 2400);
            var logical = new Vector2(360, 800);
            const float density = 3f;
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(pixels);
                yield return Load(pixels, logical);

                var controller = Find<DecorationModeController>();
                controller.EnterDecorationMode();
                var catalogue = Find<DecorationCatalogueView>();
                catalogue.ShowCatalogue();
                yield return Settle();
                var pickup = Field<Button>(catalogue, "pickUpPointButton");
                Assert.That(pickup.gameObject.activeInHierarchy, Is.True);
                AssertTarget(pickup, density, Screen.safeArea);
                AssertRaycastResolvesTo(pickup, Box(pickup).center);
                pickup.onClick.Invoke();
                yield return Settle();
                var store = Find<DecorationStoreModalView>();
                Assert.That(store.ContentRect.gameObject.activeInHierarchy, Is.True,
                    "The preserved pickup action must still open its production modal.");
                store.CloseForOwnerShutdown();
                yield return Settle();
                catalogue.ShowCatalogue();
                yield return Settle();

                var furniture = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                    .First(tile => tile.ItemId == "furniture.counter.module.01" && tile.gameObject.activeInHierarchy);
                furniture.GetComponent<Button>().onClick.Invoke();
                yield return Settle();
                catalogue.ShowCatalogue();
                yield return Settle();
                var returnButton = Field<Button>(catalogue, "returnToEditingButton");
                Assert.That(returnButton.gameObject.activeInHierarchy, Is.True);
                AssertTarget(returnButton, density, Screen.safeArea);
                AssertRaycastResolvesTo(returnButton, Box(returnButton).center);
                var stateBeforeReturn = controller.State;
                returnButton.onClick.Invoke();
                yield return Settle();
                Assert.That(controller.State, Is.EqualTo(stateBeforeReturn),
                    "Return changes presentation only and must preserve the active preview transaction.");
                Field<Button>(Find<DecorationActionBarView>(), "cancelButton").onClick.Invoke();
                yield return Settle();

                Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                var floorTile = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                    .First(tile => tile.ItemId == "floor.warm-wood" && tile.gameObject.activeInHierarchy);
                floorTile.GetComponent<Button>().onClick.Invoke();
                yield return Settle();
                catalogue.ShowCatalogue();
                yield return Settle();

                var panel = Box(Field<GameObject>(catalogue, "expandedRoot").transform);
                Assert.That(panel.height, Is.LessThanOrEqualTo(Screen.safeArea.height * .45f + 1f),
                    "A feasible tall-phone preview state must not evade the 45-percent sheet cap.");
                var range = Find<DecorationFloorRangeView>();
                var fixedControls = new[]
                    {
                        Field<Button>(range, "wholeRoomButton"),
                        Field<Button>(range, "singleGridButton"),
                        Field<Button>(Find<DecorationActionBarView>(), "cancelButton"),
                        Field<Button>(Find<DecorationActionBarView>(), "confirmButton")
                    }
                    .Where(button => button != null && button.gameObject.activeInHierarchy).ToArray();
                Assert.That(fixedControls, Has.Length.EqualTo(4),
                    "Both Floor ranges, Cancel and Confirm must all remain present.");
                AssertTargets(fixedControls, density, Screen.safeArea);
                AssertNoOverlap(fixedControls);
                foreach (var button in fixedControls.Where(button => button.interactable))
                {
                    AssertRaycastResolvesTo(button, Box(button).center);
                }

                Assert.That(Box(catalogue.VerticalScroll.viewport).height / density,
                    Is.GreaterThanOrEqualTo(47.9f),
                    "Fixed actions cannot consume the entire internally scrollable Catalogue viewport.");
                AssertVisibleTileTappable(catalogue, density, "Tall-phone Floor preview");
            }
        }

        [UnityTest]
        public IEnumerator InsetSmallPhone_PendingFloorPreviewStatePreservesControlsAndOneTapHeight()
        {
            var pixels = new Vector2(960, 1704);
            var logical = new Vector2(320, 568);
            const float density = 3f;
            var safePixels = new Rect(24, 24, 912, 1656);
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(pixels);
                yield return Load(pixels, logical, safePixels);

                var controller = Find<DecorationModeController>();
                controller.EnterDecorationMode();
                Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                var catalogue = Find<DecorationCatalogueView>();
                var floorTile = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                    .First(tile => tile.ItemId == "floor.warm-wood" && tile.gameObject.activeInHierarchy);
                floorTile.GetComponent<Button>().onClick.Invoke();
                yield return Settle();
                catalogue.ShowCatalogue();
                yield return Settle();

                var returnButton = Field<Button>(catalogue, "returnToEditingButton");
                Assert.That(returnButton == null || !returnButton.gameObject.activeInHierarchy, Is.True,
                    "Floor preview intentionally has no Return action; only Furniture and Wall Decor support it.");
                var range = Find<DecorationFloorRangeView>();
                var controls = new[]
                    {
                        Field<Button>(range, "wholeRoomButton"),
                        Field<Button>(range, "singleGridButton"),
                        Field<Button>(Find<DecorationActionBarView>(), "cancelButton"),
                        Field<Button>(Find<DecorationActionBarView>(), "confirmButton")
                    }
                    .Where(button => button != null && button.gameObject.activeInHierarchy).ToArray();
                Assert.That(controls, Has.Length.EqualTo(4),
                    "The pending height decision cannot temporarily remove Floor ranges, Cancel or Confirm.");
                AssertTargets(controls, density, safePixels);
                AssertNoOverlap(controls);
                Assert.That(Box(catalogue.VerticalScroll.viewport).height / density,
                    Is.GreaterThanOrEqualTo(47.9f),
                    "While the height policy is pending, preserve one complete logical tap-height without shrinking controls.");
                AssertVisibleTileTappable(catalogue, density, "Pending inset Floor preview");
                Assert.That(Box(Field<GameObject>(catalogue, "expandedRoot").transform).height,
                    Is.LessThanOrEqualTo(safePixels.height * .45f + 1f),
                    "Small-phone preview must follow the same safe-height cap as browsing.");
            }
        }

        [UnityTest]
        public IEnumerator ShortLandscape_AllFourTabsAndFloorPreviewUseTheBoundedCompactLayout()
        {
            var pixels = new Vector2(1920, 1080);
            var logical = new Vector2(640, 360);
            const float density = 3f;
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(pixels);
                yield return Load(pixels, logical);

                var controller = Find<DecorationModeController>();
                controller.EnterDecorationMode();
                var catalogue = Find<DecorationCatalogueView>();
                foreach (var mode in new[]
                         {
                             DecorationModeKind.Furniture,
                             DecorationModeKind.Floor,
                             DecorationModeKind.Wall,
                             DecorationModeKind.WallDecor
                         })
                {
                    Assert.That(controller.TryChangeMode(mode), Is.True);
                    catalogue.ShowCatalogue();
                    yield return Settle();
                    var expanded = Field<GameObject>(catalogue, "expandedRoot");
                    var panel = Box(expanded.transform);
                    Assert.That(panel.height, Is.LessThanOrEqualTo(Screen.safeArea.height * .45f + 1f),
                        mode + " short-landscape browsing sheet");
                    AssertVisibleTileTappable(catalogue, density, mode + " short-landscape browsing");
                    if (mode == DecorationModeKind.Furniture)
                    {
                        var pickup = Field<Button>(catalogue, "pickUpPointButton");
                        var title = expanded.transform.Find("P8RCatalogueTitle");
                        Assert.That(title == null || !title.gameObject.activeInHierarchy, Is.True,
                            "Pickup replaces the redundant Catalogue title in the constrained wide header.");
                        var headerControls = Find<DecorationModeTabsView>().GetComponentsInChildren<Button>()
                            .Concat(new[] { Field<Button>(catalogue, "collapseButton"), pickup }).ToArray();
                        AssertTargets(headerControls, density, Screen.safeArea);
                        AssertNoOverlap(headerControls);
                        Assert.That(Box(pickup).center.y, Is.EqualTo(Box(headerControls[0]).center.y).Within(.1f),
                            "Pickup shares the tabs' visual centerline in the compact wide header.");
                    }
                }

                Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
                catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                    .First(tile => tile.ItemId == "furniture.counter.module.01" && tile.gameObject.activeInHierarchy)
                    .GetComponent<Button>().onClick.Invoke();
                yield return Settle();
                catalogue.ShowCatalogue();
                yield return Settle();
                var returnButton = Field<Button>(catalogue, "returnToEditingButton");
                var pickupButton = Field<Button>(catalogue, "pickUpPointButton");
                Assert.That(returnButton.gameObject.activeInHierarchy, Is.True);
                Assert.That(pickupButton.gameObject.activeInHierarchy, Is.True);
                AssertTargets(new[] { returnButton, pickupButton }, density, Screen.safeArea);
                AssertNoOverlap(new[] { returnButton, pickupButton });
                AssertRaycastResolvesTo(returnButton, Box(returnButton).center);
                AssertRaycastResolvesTo(pickupButton, Box(pickupButton).center);
                Assert.That(Box(Field<GameObject>(catalogue, "expandedRoot").transform).height,
                    Is.LessThanOrEqualTo(Screen.safeArea.height * .45f + 1f),
                    "Reopening Furniture with Return and Pickup must retain the browsing height cap.");
                Field<Button>(Find<DecorationActionBarView>(), "cancelButton").onClick.Invoke();
                yield return Settle();

                Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                    .First(tile => tile.ItemId == "floor.warm-wood" && tile.gameObject.activeInHierarchy)
                    .GetComponent<Button>().onClick.Invoke();
                yield return Settle();
                catalogue.ShowCatalogue();
                yield return Settle();

                var previewPanel = Box(Field<GameObject>(catalogue, "expandedRoot").transform);
                Assert.That(previewPanel.height, Is.LessThanOrEqualTo(Screen.safeArea.height * .45f + 1f),
                    "The measured two-column Floor preview fits the full-height 640x360 safe area.");
                var range = Find<DecorationFloorRangeView>();
                var controls = new[]
                {
                    Field<Button>(range, "wholeRoomButton"),
                    Field<Button>(range, "singleGridButton"),
                    Field<Button>(Find<DecorationActionBarView>(), "cancelButton"),
                    Field<Button>(Find<DecorationActionBarView>(), "confirmButton")
                };
                AssertTargets(controls, density, Screen.safeArea);
                AssertNoOverlap(controls);
                AssertVisibleTileTappable(catalogue, density, "640x360 Floor preview");
            }
        }

        [UnityTest]
        public IEnumerator VeryShortSafeArea_SurfacePreviewCapsSheetAndKeepsEveryToolReachable()
        {
            var pixels = new Vector2(1138, 640);
            var logical = pixels * .5f;
            var safe = new Rect(88, 40, 1026, 576);
            const float density = 2f;
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(pixels);
                yield return Load(pixels, logical, safe);
                var controller = Find<DecorationModeController>();
                controller.EnterDecorationMode();
                var catalogue = Find<DecorationCatalogueView>();
                foreach (var mode in new[] { DecorationModeKind.Floor, DecorationModeKind.Wall })
                {
                    Assert.That(controller.TryChangeMode(mode), Is.True);
                    if (mode == DecorationModeKind.Wall)
                        Assert.That(controller.TryHandleSceneTap(new AnimalCafe.Decoration.Input.DecorationTouchHit(
                            AnimalCafe.Decoration.Input.DecorationTouchHitKind.WallSurface, surfaceId: "wall.back-left")), Is.True);
                    catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                        .First(tile => tile.ItemId == (mode == DecorationModeKind.Floor ? "floor.warm-wood" : "paint.sage")
                            && tile.gameObject.activeInHierarchy).GetComponent<Button>().onClick.Invoke();
                    catalogue.ShowCatalogue();
                    yield return Settle();
                    var panel = Box(Field<GameObject>(catalogue, "expandedRoot").transform);
                    AssertInside(panel, safe, mode + " short-safe-area panel");
                    Assert.That(panel.height, Is.LessThanOrEqualTo(safe.height * .45f + 1f),
                        mode + " preview may scroll its tools, but must not cover more of the scene.");
                    AssertVisibleTileTappable(catalogue, density, mode + " first card");
                    var actions = Find<DecorationActionBarView>();
                    var fields = mode == DecorationModeKind.Floor
                        ? new[] { "undoLastButton", "rotateButton", "applyAllButton", "cancelButton", "confirmButton" }
                        : new[] { "cancelButton", "confirmButton" };
                    var buttons = fields.Select(name => Field<Button>(actions, name)).ToList();
                    if (mode == DecorationModeKind.Floor)
                    {
                        var range = Find<DecorationFloorRangeView>();
                        buttons.Add(Field<Button>(range, "wholeRoomButton"));
                        buttons.Add(Field<Button>(range, "singleGridButton"));
                    }
                    var reached = new HashSet<Button>();
                    for (var step = 0; step <= 20; step++)
                    {
                        catalogue.VerticalScroll.verticalNormalizedPosition = 1f - step / 20f;
                        Canvas.ForceUpdateCanvases();
                        yield return null;
                        foreach (var button in buttons)
                        {
                            Assert.That(button.gameObject.activeInHierarchy, Is.True, mode + " " + button.name + " must not disappear.");
                            var bounds = Box(button);
                            Assert.That(bounds.width / density, Is.GreaterThanOrEqualTo(47.9f));
                            Assert.That(bounds.height / density, Is.GreaterThanOrEqualTo(47.9f));
                            var clip = button.transform.IsChildOf(catalogue.VerticalScroll.content)
                                ? Box(catalogue.VerticalScroll.viewport) : panel;
                            var visible = Intersection(bounds, clip);
                            if (visible.width / density < 47.9f || visible.height / density < 47.9f) continue;
                            AssertRaycastResolvesTo(button, visible.center);
                            reached.Add(button);
                        }
                    }
                    Assert.That(reached, Is.EquivalentTo(buttons), "Every original tool needs a full reachable touch area.");
                    catalogue.SetSheetState(DecorationSheetState.CompactPreview, true);
                    Assert.That(catalogue.SurfaceFooterHost.IsChildOf(catalogue.VerticalScroll.content), Is.False,
                        "Collapse must restore tools before disabling their former scrolling ancestor.");
                    yield return Settle();
                    foreach (var button in buttons)
                    {
                        Assert.That(button.gameObject.activeInHierarchy, Is.True, mode + " collapsed tool");
                        AssertTarget(button, density, safe);
                        AssertRaycastResolvesTo(button, Box(button).center);
                    }
                    catalogue.SetSheetState(DecorationSheetState.Expanded, true);
                    yield return Settle();
                    Field<Button>(actions, "cancelButton").onClick.Invoke();
                    catalogue.ShowCatalogue();
                    yield return Settle();
                    Assert.That(Box(Field<GameObject>(catalogue, "expandedRoot").transform).height,
                        Is.LessThanOrEqualTo(safe.height * .45f + 1f), "Cancel must restore the capped browsing layout.");
                }
            }
        }

        [UnityTest]
        public IEnumerator VeryShortSafeArea_FurniturePreviewRebindAndResizeKeepToolsReachable()
        {
            var pixels = new Vector2(1138, 640);
            var safe = new Rect(88, 40, 1026, 576);
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(pixels);
                yield return Load(pixels, pixels * .5f, safe);
                var controller = Find<DecorationModeController>();
                controller.EnterDecorationMode();
                var catalogue = Find<DecorationCatalogueView>();
                for (var pass = 0; pass < 2; pass++)
                {
                    Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
                    catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                        .First(tile => tile.ItemId == "furniture.counter.module.01" && tile.gameObject.activeInHierarchy)
                        .GetComponent<Button>().onClick.Invoke();
                    catalogue.ShowCatalogue();
                    yield return Settle();
                    var panel = Box(Field<GameObject>(catalogue, "expandedRoot").transform);
                    AssertInside(panel, safe, "Reopened Furniture");
                    Assert.That(panel.height, Is.LessThanOrEqualTo(safe.height * .45f + 1f));
                    var buttons = new[] { Field<Button>(catalogue, "returnToEditingButton"), Field<Button>(catalogue, "pickUpPointButton") };
                    var reached = new HashSet<Button>();
                    for (var step = 0; step <= 160; step++)
                    {
                        catalogue.VerticalScroll.verticalNormalizedPosition = 1f - step / 160f;
                        Canvas.ForceUpdateCanvases();
                        yield return null;
                        foreach (var button in buttons)
                        {
                            Assert.That(button.gameObject.activeInHierarchy, Is.True, "Rebinding must not destroy a reparented tool.");
                            var bounds = Box(button);
                            Assert.That(bounds.width / 2f, Is.GreaterThanOrEqualTo(47.9f));
                            Assert.That(bounds.height / 2f, Is.GreaterThanOrEqualTo(47.9f));
                            var clip = button.transform.IsChildOf(catalogue.VerticalScroll.content)
                                ? Box(catalogue.VerticalScroll.viewport) : panel;
                            var visible = Intersection(bounds, clip);
                            if (visible.width / 2f < 47.9f || visible.height / 2f < 47.9f) continue;
                            AssertRaycastResolvesTo(button, visible.center);
                            reached.Add(button);
                        }
                    }
                    Assert.That(reached, Is.EquivalentTo(buttons));
                    var previewState = controller.State;
                    buttons[0].onClick.Invoke();
                    yield return Settle();
                    Assert.That(controller.State, Is.EqualTo(previewState), "Return must preserve the pending transaction.");
                    catalogue.ShowCatalogue();
                    yield return Settle();
                    Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                    yield return Settle();
                    Assert.That(buttons[0], Is.Not.Null, "Changing tabs must retain the reusable Return control.");
                    Assert.That(buttons[1], Is.Not.Null, "Changing tabs must retain the reusable Pickup control.");
                }

                // Rotate back to a roomy portrait profile: the scrolling fallback must not stick.
                // 回到空间充足的竖屏后，临时滚动工具应恢复固定位置。
                pixels = new Vector2(1080, 1920);
                screen.Resize(pixels);
                P8RMobileMetrics.EditorLogicalViewportOverride = new Vector2(360, 640);
                foreach (var container in ownedScene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SafeAreaContainer>(true)))
                    container.ApplySafeArea(new Rect(Vector2.zero, pixels), pixels);
                Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
                catalogue.ShowCatalogue();
                yield return Settle();
                var pickup = Field<Button>(catalogue, "pickUpPointButton");
                Assert.That(pickup.transform.IsChildOf(catalogue.VerticalScroll.content), Is.False);
                AssertTarget(pickup, 3f, new Rect(Vector2.zero, pixels));
                AssertRaycastResolvesTo(pickup, Box(pickup).center);
            }
        }

        [UnityTest]
        public IEnumerator FloorTab_CompensatesFlatArtworkWithoutChangingOtherIconsOrHitAreas()
        {
            var pixels = new Vector2(960, 1704);
            using (var screen = new P8RReferenceLayoutTests.NativeScreenSize())
            {
                screen.Resize(pixels);
                yield return Load(pixels, new Vector2(320, 568));
                var controller = Find<DecorationModeController>();
                controller.EnterDecorationMode();
                var catalogue = Find<DecorationCatalogueView>();
                catalogue.ShowCatalogue();
                yield return Settle();
                foreach (var mode in new[] { DecorationModeKind.Floor, DecorationModeKind.Furniture })
                {
                    Assert.That(controller.TryChangeMode(mode), Is.True);
                    yield return Settle();
                    var buttons = Find<DecorationModeTabsView>().GetComponentsInChildren<Button>();
                    var icons = buttons.Select(button => button.transform.Find("Icon").GetComponent<Image>()).ToArray();
                    var floor = icons.Single(icon => icon.sprite.name == "tab_floor_color");
                    var others = icons.Where(icon => icon != floor).ToArray();
                    var floorExtent = Mathf.Max(Box(floor).width, Box(floor).height);
                    var otherExtent = others.Average(icon => Mathf.Max(Box(icon).width, Box(icon).height));
                    Assert.That(floorExtent / otherExtent, Is.InRange(1.1f, 1.2f),
                        "The flat floor symbol needs modest optical emphasis, independent of selection.");
                    foreach (var icon in icons)
                    {
                        var bounds = Box(icon);
                        AssertInside(bounds, Box(icon.GetComponentInParent<Button>()), "Tab ink");
                        Assert.That(bounds.width / bounds.height,
                            Is.EqualTo(icon.sprite.rect.width / icon.sprite.rect.height).Within(.01f),
                            "Optical compensation must preserve the original PNG aspect ratio.");
                    }
                    AssertTargets(buttons, 3f, Screen.safeArea);
                    AssertNoOverlap(buttons);
                }
            }
        }

        private IEnumerator Load(Vector2 pixels, Vector2 logical, Rect? injectedSafeArea = null)
        {
            P8RMobileMetrics.EditorLogicalViewportOverride = logical;
            ownedScene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/MainCafe.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;
            yield return null;
            Assert.That(new Vector2(Screen.width, Screen.height), Is.EqualTo(pixels));
            if (injectedSafeArea.HasValue)
            {
                foreach (var safeArea in ownedScene.GetRootGameObjects()
                             .SelectMany(root => root.GetComponentsInChildren<SafeAreaContainer>(true)))
                {
                    safeArea.AutoApplyRuntimeSafeArea = false;
                    safeArea.ApplySafeArea(injectedSafeArea.Value, pixels);
                }

                yield return null;
            }

            Canvas.ForceUpdateCanvases();
        }

        private T Find<T>() where T : Component => ownedScene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).Single();

        private static T Field<T>(object owner, string name) => (T)owner.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private static IEnumerator Settle()
        {
            yield return new WaitForSecondsRealtime(.3f);
            Canvas.ForceUpdateCanvases();
        }

        private static Rect Box(Component component)
        {
            var rect = (RectTransform)component.transform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var canvas = component.GetComponentInParent<Canvas>().rootCanvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var points = corners.Select(point => RectTransformUtility.WorldToScreenPoint(camera, point)).ToArray();
            return Rect.MinMaxRect(points.Min(point => point.x), points.Min(point => point.y),
                points.Max(point => point.x), points.Max(point => point.y));
        }

        private static Rect Intersection(Rect first, Rect second)
        {
            var xMin = Mathf.Max(first.xMin, second.xMin);
            var yMin = Mathf.Max(first.yMin, second.yMin);
            var xMax = Mathf.Min(first.xMax, second.xMax);
            var yMax = Mathf.Min(first.yMax, second.yMax);
            return xMax <= xMin || yMax <= yMin
                ? new Rect(xMin, yMin, 0, 0)
                : Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private static void AssertVisibleTileTappable(
            DecorationCatalogueView catalogue,
            float density,
            string label)
        {
            var viewport = Box(catalogue.VerticalScroll.viewport);
            var candidate = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>()
                .Where(tile => !string.IsNullOrEmpty(tile.ItemId) && tile.gameObject.activeInHierarchy)
                .Select(tile => new { Tile = tile, Visible = Intersection(Box(tile), viewport) })
                .OrderByDescending(item => item.Visible.width * item.Visible.height)
                .First();
            Assert.That(candidate.Visible.width / density, Is.GreaterThanOrEqualTo(47.9f),
                label + " visible tile width");
            Assert.That(candidate.Visible.height / density, Is.GreaterThanOrEqualTo(47.9f),
                label + " visible tile height");
            AssertRaycastResolvesTo(candidate.Tile.GetComponent<Button>(), candidate.Visible.center);
        }

        private static void AssertTarget(Button button, float density, Rect safeArea)
        {
            var rect = Box(button);
            Assert.That(rect.width / density, Is.GreaterThanOrEqualTo(47.9f), button.name + " target width");
            Assert.That(rect.height / density, Is.GreaterThanOrEqualTo(47.9f), button.name + " target height");
            AssertInside(rect, safeArea, button.name);
        }

        private static void AssertTargets(IEnumerable<Button> buttons, float density, Rect safeArea)
        {
            foreach (var button in buttons)
            {
                AssertTarget(button, density, safeArea);
            }
        }

        private static void AssertNoOverlap(IReadOnlyList<Button> buttons)
        {
            for (var index = 0; index < buttons.Count; index++)
            {
                for (var previous = 0; previous < index; previous++)
                {
                    Assert.That(Box(buttons[index]).Overlaps(Box(buttons[previous])), Is.False,
                        buttons[index].name + " overlaps " + buttons[previous].name);
                }
            }
        }

        private static void AssertRaycastResolvesTo(Button expected, Vector2 screenPosition)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            }, hits);
            Assert.That(hits, Is.Not.Empty, expected.name + " must own an actual raycastable screen area.");
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(expected),
                expected.name + " must be the topmost action at the measured tap point.");
        }

        private static void AssertInside(Rect inner, Rect outer, string label)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin - 1f), label + " left");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax + 1f), label + " right");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin - 1f), label + " bottom");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax + 1f), label + " top");
        }
    }
}
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.Content;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Foundation;
using AnimalCafe.UI.Components;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.Phase7
{
    /// <summary>
    /// Task 6 UI contracts.  These tests intentionally use isolated uGUI fixtures:
    /// prefab migration belongs to Task 9.
    /// Task 6 UI 合同。这里使用独立 uGUI fixture；Prefab migration 属于 Task 9。
    /// </summary>
    public sealed class Phase7DecorationUiPlayModeTests
    {
        [TestCase(PlacementFeedbackKey.WallOverlap, "Wall space already occupied")]
        [TestCase(PlacementFeedbackKey.WallOutOfBounds, "Outside wall area")]
        [TestCase(PlacementFeedbackKey.WallCrossCorner, "Place the item fully on one wall")]
        [TestCase(PlacementFeedbackKey.WallSurfaceMissing, "Wall surface unavailable")]
        [TestCase(PlacementFeedbackKey.SelectWallTarget, "Select a wall to edit")]
        [TestCase(PlacementFeedbackKey.SelectFloorGridTarget, "Select a floor grid to edit")]
        [TestCase(PlacementFeedbackKey.None, "")]
        public void Wall_mounted_feedback_uses_specific_beginner_readable_copy(
            PlacementFeedbackKey key, string expected)
        {
            using var fixture = new ActionFixture();
            fixture.View.Show(false, key == PlacementFeedbackKey.None, key);
            Assert.That(fixture.Feedback.text, Is.EqualTo(expected));
        }

        [Test]
        public void Floor_range_defaults_whole_room_and_only_updates_when_gate_accepts()
        {
            using var fixture = new FloorRangeFixture();
            var allow = true;
            var requests = 0;
            fixture.View.RangeRequested += _ => { requests++; return allow; };

            Assert.That(fixture.View.SelectedRange, Is.EqualTo(SurfaceEditScope.WholeRoomFloor));
            fixture.SingleGrid.onClick.Invoke();
            Assert.That(fixture.View.SelectedRange, Is.EqualTo(SurfaceEditScope.SingleGridFloor));
            allow = false;
            fixture.WholeRoom.onClick.Invoke();
            Assert.That(fixture.View.SelectedRange, Is.EqualTo(SurfaceEditScope.SingleGridFloor));
            Assert.That(requests, Is.EqualTo(2));
        }

        [Test]
        public void Floor_range_rebind_disable_and_destroy_never_duplicate_or_leak_button_handlers()
        {
            using var fixture = new FloorRangeFixture();
            var requests = 0;
            fixture.View.RangeRequested += _ => { requests++; return true; };
            fixture.View.Configure(fixture.WholeRoom, fixture.SingleGrid);
            fixture.View.Configure(fixture.WholeRoom, fixture.SingleGrid);
            fixture.SingleGrid.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(1));

            fixture.View.enabled = false;
            fixture.WholeRoom.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(1));
            fixture.View.enabled = true;
            fixture.WholeRoom.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(2));

            UnityEngine.Object.DestroyImmediate(fixture.View);
            fixture.SingleGrid.onClick.Invoke();
            Assert.That(requests, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Tabs_default_to_furniture_keep_active_tab_front_and_preserve_mobile_hit_targets()
        {
            using var fixture = new TabsFixture();
            Assert.That(fixture.View.ActiveMode, Is.EqualTo(DecorationModeKind.Furniture));
            Assert.That(fixture.Furniture.transform.GetSiblingIndex(), Is.EqualTo(fixture.Root.transform.childCount - 1));
            fixture.View.SetActive(DecorationModeKind.Wall);
            yield return null;

            Assert.That(fixture.View.ActiveMode, Is.EqualTo(DecorationModeKind.Wall));
            Assert.That(fixture.Wall.transform.GetSiblingIndex(), Is.EqualTo(fixture.Root.transform.childCount - 1));
            Assert.That(fixture.Wall.image.color,Is.Not.EqualTo(fixture.Furniture.image.color));
            foreach (var button in fixture.Buttons)
            {
                Assert.That(button.GetComponent<RectTransform>().rect.width, Is.GreaterThanOrEqualTo(48f));
                Assert.That(button.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(48f));
            }
        }

        [UnityTest]
        public IEnumerator Tabs_raise_only_active_folder_tab_and_restore_previous_and_disable_state()
        {
            using var fixture=new TabsFixture();yield return null;
            var inactiveY=fixture.Buttons.Where(button=>button!=fixture.Furniture).Select(button=>button.GetComponent<RectTransform>().anchoredPosition.y).Distinct().Single();
            var baseline=fixture.Buttons.ToDictionary(button=>button,button=>inactiveY);
            Assert.That(fixture.Furniture.GetComponent<RectTransform>().anchoredPosition.y,Is.GreaterThan(fixture.Buttons.Where(button=>button!=fixture.Furniture).Max(button=>button.GetComponent<RectTransform>().anchoredPosition.y)));
            fixture.View.SetActive(DecorationModeKind.Wall);yield return null;
            Assert.That(fixture.Wall.GetComponent<RectTransform>().anchoredPosition.y,Is.GreaterThan(fixture.Buttons.Where(button=>button!=fixture.Wall).Max(button=>button.GetComponent<RectTransform>().anchoredPosition.y)));
            Assert.That(fixture.Furniture.GetComponent<RectTransform>().anchoredPosition.y,Is.EqualTo(baseline[fixture.Furniture]));
            fixture.View.enabled=false;
            Assert.That(fixture.Buttons.All(button=>Mathf.Approximately(button.GetComponent<RectTransform>().anchoredPosition.y,baseline[button])),Is.True);
            fixture.View.enabled=true;yield return null;
            Assert.That(fixture.Wall.GetComponent<RectTransform>().anchoredPosition.y,Is.GreaterThan(baseline[fixture.Wall]));
        }

        [Test]
        public void Tabs_destroy_removes_button_handlers_without_reinvoking_selection()
        {
            using var fixture = new TabsFixture();
            var selectionCount = 0;
            fixture.View.Selected += _ => selectionCount++;

            fixture.Furniture.onClick.Invoke();
            Assert.That(selectionCount, Is.EqualTo(1));

            UnityEngine.Object.DestroyImmediate(fixture.View);
            fixture.Furniture.onClick.Invoke();

            Assert.That(selectionCount, Is.EqualTo(1));
        }

        [Test]
        public void Sheet_allows_three_snap_states_but_refuses_tabs_only_while_preview_is_active()
        {
            var root = new GameObject("Catalogue", typeof(RectTransform));
            try
            {
                var view = root.AddComponent<DecorationCatalogueView>();
                view.SetSheetState(DecorationSheetState.TabsOnly, hasActivePreview: false);
                Assert.That(view.SheetState, Is.EqualTo(DecorationSheetState.TabsOnly));

                view.SetSheetState(DecorationSheetState.TabsOnly, hasActivePreview: true);
                Assert.That(view.SheetState, Is.EqualTo(DecorationSheetState.CompactPreview));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Catalogue_builds_labeled_vertical_categories_with_real_horizontal_item_content()
        {
            using var fixture = new CatalogueFixture();
            fixture.View.BindCategories(new[]
            {
                Category("furniture", "Furniture", DecorationCatalogueItemKind.Furniture),
                Category("wallpaper", "Wallpaper", DecorationCatalogueItemKind.WallSurface)
            }, _ => { });

            Assert.That(fixture.View.CategoryRows.Count, Is.EqualTo(2));
            foreach (var row in fixture.View.CategoryRows)
            {
                Assert.That(row.HorizontalScroll.horizontal, Is.True);
                Assert.That(row.HorizontalScroll.vertical, Is.False);
                Assert.That(row.HorizontalScroll.content.GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
            }
            var generatedRow = fixture.GeneratedRow;
            Assert.That(generatedRow.Find("CategoryLabel").GetComponent<TextMeshProUGUI>().text, Is.EqualTo("Wallpaper"));
            Assert.That(generatedRow.GetComponent<ScrollRect>().content
                .GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .SingleOrDefault(tile => tile.ItemId == "wallpaper.one"), Is.Not.Null);
            Assert.That(fixture.View.VerticalScroll.vertical, Is.True);
            Assert.That(fixture.View.VerticalScroll.horizontal, Is.False);
        }

        [Test]
        public void Tiles_follow_furniture_name_only_and_surface_image_only_using_preview_and_none_grammar()
        {
            using var fixture = new TileFixture();
            fixture.View.Bind(new DecorationCatalogueItemModel("chair", "Chair", null,
                DecorationCatalogueItemKind.Furniture, false), _ => { });
            Assert.That(fixture.Name.gameObject.activeSelf, Is.True);
            Assert.That(fixture.Footprint.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Thumbnail.rectTransform.anchorMin.y, Is.GreaterThanOrEqualTo(.18f));

            fixture.View.Bind(new DecorationCatalogueItemModel("wains.none", "None", null,
                DecorationCatalogueItemKind.WallSurface, true), _ => { });
            fixture.View.SetSurfaceState(isUsing: true, isPreview: true);
            Assert.That(fixture.Name.gameObject.activeSelf, Is.False);
            Assert.That(fixture.UsingCheck.activeSelf, Is.True);
            Assert.That(fixture.PreviewOutline.activeSelf, Is.True);
            Assert.That(fixture.NoneIcon.activeSelf, Is.True);
            Assert.That(fixture.Thumbnail.rectTransform.anchorMin.y, Is.LessThanOrEqualTo(.05f),
                "Surface cards have no text row, so their preview image must use the full card.");
        }

        [Test]
        public void MinimalLegacyTile_SetSurfaceState_IgnoresMissingOrDestroyedIndicatorReferences()
        {
            var root = Ui("LegacyMinimalTile");
            try
            {
                var tile = root.AddComponent<DecorationCatalogueTileView>();
                Assert.DoesNotThrow(() => tile.SetSurfaceState(true, true));
                var staleUsing = Ui("DestroyedUsingCheck", root.transform);
                var stalePreview = Ui("DestroyedPreviewOutline", root.transform);
                Set(tile, "usingCheck", staleUsing);
                Set(tile, "previewOutline", stalePreview);
                UnityEngine.Object.DestroyImmediate(staleUsing);
                UnityEngine.Object.DestroyImmediate(stalePreview);
                Assert.DoesNotThrow(() => tile.SetSurfaceState(false, false));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [TestCase(DecorationModeKind.Floor, false, "Undo Last,Rotate,Apply All,Cancel,Confirm")]
        [TestCase(DecorationModeKind.Wall, false, "Cancel,Confirm")]
        [TestCase(DecorationModeKind.Furniture, false, "Cancel,Rotate,Confirm")]
        [TestCase(DecorationModeKind.Furniture, true, "Store,Cancel,Rotate,Confirm")]
        [TestCase(DecorationModeKind.WallDecor, false, "Cancel,Confirm")]
        [TestCase(DecorationModeKind.WallDecor, true, "Store,Cancel,Confirm")]
        public void Action_bar_exposes_exact_mode_matrix_without_overflow(
            DecorationModeKind mode, bool existing, string expected)
        {
            using var fixture = new ActionFixture();
            fixture.View.SetModeActions(mode, existing);
            Assert.That(string.Join(",", fixture.View.VisibleActionLabels), Is.EqualTo(expected));
            Assert.That(fixture.View.HasOverflowActions, Is.False);
        }

        [TestCase(DecorationModeKind.Floor)]
        [TestCase(DecorationModeKind.Wall)]
        public void Surface_action_bar_uses_fixed_footer_layout_and_full_text_primary_buttons(
            DecorationModeKind mode)
        {
            var footer = Ui("SurfaceFooterHost").GetComponent<RectTransform>();
            footer.sizeDelta = new Vector2(600f, 128f);
            using var fixture = new ActionFixture();
            try
            {
                fixture.View.AttachToHost(footer);
                fixture.View.SetModeActions(mode, existing: false);

                var rootRect = fixture.Root.GetComponent<RectTransform>();
                Assert.That(rootRect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(rootRect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(rootRect.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(rootRect.offsetMax, Is.EqualTo(Vector2.zero));
                Assert.That(fixture.Label("cancelButton").text, Is.EqualTo("Cancel"));
                Assert.That(fixture.Label("confirmButton").text, Is.EqualTo("Confirm"));
                Assert.That(fixture.GetButton("cancelButton").GetComponent<RectTransform>().rect.width,
                    Is.GreaterThanOrEqualTo(136f));
                Assert.That(fixture.GetButton("confirmButton").GetComponent<RectTransform>().rect.width,
                    Is.GreaterThanOrEqualTo(136f));
                Assert.That(fixture.Label("cancelButton").enableWordWrapping, Is.False);
                Assert.That(fixture.Label("confirmButton").enableWordWrapping, Is.False);
                Assert.That(fixture.Panel.anchoredPosition.y,
                    Is.EqualTo(mode == DecorationModeKind.Floor ? -32f : 0f).Within(.01f),
                    "Floor actions need the second row; Wall actions stay vertically centred.");

                var footerPosition = fixture.Panel.anchoredPosition;
                fixture.View.SetPresentation(
                    DecorationActionPresentation.New,
                    new Vector2(520f, 420f),
                    new Rect(0f, 0f, 600f, 480f));
                Assert.That(fixture.Panel.anchoredPosition, Is.EqualTo(footerPosition),
                    "Surface buttons belong to the Bottom Sheet footer and must ignore world-preview positioning.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(footer.gameObject);
            }
        }

        [Test]
        public void Surface_text_buttons_disable_tooltips_but_compact_icon_buttons_enable_them()
        {
            using var fixture = new ActionFixture();
            var hooks = new List<DecorationPointerBoundaryEventHook>();
            foreach (var buttonName in new[] { "cancelButton", "confirmButton" })
            {
                var button = fixture.GetButton(buttonName);
                var tooltip = Ui("Tooltip", button.transform);
                tooltip.SetActive(false);
                var hook = button.gameObject.AddComponent<DecorationPointerBoundaryEventHook>();
                Set(hook, "tooltipRoot", tooltip);
                hooks.Add(hook);
            }

            fixture.View.SetModeActions(DecorationModeKind.Floor, existing: false);
            foreach (var hook in hooks)
            {
                hook.OnPointerEnter(null);
                Assert.That(hook.IsTooltipVisible, Is.False,
                    "Readable Surface footer text must not reopen a redundant tooltip on hover.");
            }

            fixture.View.SetModeActions(DecorationModeKind.WallDecor, existing: false);
            foreach (var hook in hooks)
            {
                hook.OnPointerEnter(null);
                Assert.That(hook.IsTooltipVisible, Is.True,
                    "Compact icon actions need their tooltip after leaving Surface mode.");
            }
        }

        [Test]
        public void Furniture_compact_action_uses_rotate_icon_while_floor_keeps_readable_rotate_text()
        {
            using var fixture = new ActionFixture();
            var icon = Ui("Icon", fixture.GetButton("rotateButton").transform).AddComponent<Image>();
            Set(fixture.View, "rotateIcon", icon);

            fixture.View.SetModeActions(DecorationModeKind.Furniture, existing: false);
            Assert.That(icon.gameObject.activeSelf, Is.True);
            Assert.That(icon.raycastTarget, Is.False);
            Assert.That(fixture.Label("rotateButton").gameObject.activeSelf, Is.False,
                "The real icon must replace the temporary R in compact Furniture actions.");

            fixture.View.SetModeActions(DecorationModeKind.Floor, existing: false);
            Assert.That(icon.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Label("rotateButton").gameObject.activeSelf, Is.True);
            Assert.That(fixture.Label("rotateButton").text, Is.EqualTo("Rotate"),
                "Floor keeps the approved full-text action.");
        }

        [UnityTest]
        public IEnumerator Target_selection_instruction_stays_visible_until_a_real_preview_replaces_it()
        {
            using var fixture = new ActionFixture();
            var feedbackGroup = fixture.Feedback.gameObject.AddComponent<CanvasGroup>();
            Set(fixture.View, "feedbackRoot", fixture.Feedback.rectTransform);
            Set(fixture.View, "feedbackCanvasGroup", feedbackGroup);

            fixture.View.ShowInstruction(PlacementFeedbackKey.SelectWallTarget);
            Assert.That(fixture.Panel.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Feedback.gameObject.activeSelf, Is.True);
            Assert.That(fixture.Feedback.text, Is.EqualTo("Select a wall to edit"));
            Assert.That(feedbackGroup.alpha, Is.EqualTo(1f));
            Assert.That(feedbackGroup.blocksRaycasts, Is.False);

            yield return new WaitForSecondsRealtime(2f);
            Assert.That(fixture.Feedback.gameObject.activeSelf, Is.True,
                "Target guidance is an instruction, not the 1.8 second invalid-placement toast.");

            fixture.View.SetModeActions(DecorationModeKind.Wall, existing: false);
            fixture.View.Show(false, canConfirm: false, PlacementFeedbackKey.None);
            Assert.That(fixture.Panel.gameObject.activeSelf, Is.True);
            Assert.That(fixture.Feedback.gameObject.activeSelf, Is.False);
        }

        [TestCase(DecorationModeKind.Furniture, false, "×,R,✓")]
        [TestCase(DecorationModeKind.Furniture, true, "□,×,R,✓")]
        [TestCase(DecorationModeKind.WallDecor, false, "×,✓")]
        [TestCase(DecorationModeKind.WallDecor, true, "□,×,✓")]
        public void Non_surface_action_bar_uses_compact_icon_buttons_that_follow_the_preview(
            DecorationModeKind mode, bool existing, string expected)
        {
            var footer = Ui("SurfaceFooterHost").GetComponent<RectTransform>();
            var floatingHost = Ui("FloatingActionHost").GetComponent<RectTransform>();
            using var fixture = new ActionFixture();
            try
            {
                fixture.View.AttachToHost(footer);
                fixture.View.SetModeActions(DecorationModeKind.Floor, existing: false);
                fixture.View.AttachToHost(floatingHost);
                fixture.View.SetModeActions(mode, existing);
                fixture.View.Show(existing, canConfirm: true, PlacementFeedbackKey.None);
                fixture.View.SetPresentation(
                    existing ? DecorationActionPresentation.Existing : DecorationActionPresentation.New,
                    new Vector2(300f, 240f),
                    new Rect(0f, 0f, 600f, 480f));
                Canvas.ForceUpdateCanvases();

                var visibleLabels = fixture.Panel.Cast<Transform>()
                    .Where(item => item.gameObject.activeSelf)
                    .OrderBy(item => item.GetSiblingIndex())
                    .Select(item => item.Find("Label").GetComponent<TMP_Text>().text)
                    .ToArray();
                var expectedLabels = expected.Split(',');
                CollectionAssert.AreEqual(expectedLabels, visibleLabels);
                Assert.That(fixture.Panel.rect.height, Is.EqualTo(48f).Within(.01f));
                Assert.That(fixture.Panel.rect.width,
                    Is.EqualTo(expectedLabels.Length * 48f + (expectedLabels.Length - 1) * 8f).Within(.01f),
                    "Compact icon actions should stay tightly packed around the active preview.");
                foreach (Transform item in fixture.Panel)
                {
                    if (!item.gameObject.activeSelf)
                    {
                        continue;
                    }

                    var button = item.GetComponent<Button>();
                    Assert.That(((RectTransform)button.transform).rect.width,
                        Is.EqualTo(48f).Within(.01f), item.name);
                    Assert.That(((RectTransform)button.transform).rect.height,
                        Is.EqualTo(48f).Within(.01f), item.name);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(footer.gameObject);
                UnityEngine.Object.DestroyImmediate(floatingHost.gameObject);
            }
        }

        [Test]
        public void Controller_reparents_one_action_bar_between_surface_and_non_surface_hosts()
        {
            var controllerRoot = new GameObject("Controller");
            var catalogueRoot = Ui("CatalogueSheet");
            var surfaceFooter = Ui("SurfaceFooterHost", catalogueRoot.transform).GetComponent<RectTransform>();
            var nonSurfaceHost = Ui("NonSurfaceActionHost").GetComponent<RectTransform>();
            using var action = new ActionFixture();
            try
            {
                var controller = controllerRoot.AddComponent<DecorationModeController>();
                var catalogue = catalogueRoot.AddComponent<DecorationCatalogueView>();
                Set(catalogue, "surfaceFooterHost", surfaceFooter);
                action.Root.transform.SetParent(nonSurfaceHost, false);
                Set(controller, "catalogueView", catalogue);
                Set(controller, "actionBarView", action.View);

                Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
                Assert.That(action.View.transform.parent, Is.SameAs(surfaceFooter));
                Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
                Assert.That(action.View.transform.parent, Is.SameAs(surfaceFooter));
                Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
                Assert.That(action.View.transform.parent, Is.SameAs(nonSurfaceHost));
                Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
                Assert.That(action.View.transform.parent, Is.SameAs(nonSurfaceHost));
                Assert.That(new[] { catalogueRoot, nonSurfaceHost.gameObject }
                    .SelectMany(root => root.GetComponentsInChildren<DecorationActionBarView>(true))
                    .Distinct().Count(), Is.EqualTo(1));

                var cancelCalls = 0;
                action.View.CancelRequested += () => cancelCalls++;
                action.View.SetModeActions(DecorationModeKind.Floor, existing: false);
                action.View.SetModeActions(DecorationModeKind.Wall, existing: false);
                action.GetButton("cancelButton").onClick.Invoke();
                Assert.That(cancelCalls, Is.EqualTo(1),
                    "Reparenting or rebinding must replace owned listeners instead of stacking them.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controllerRoot);
                UnityEngine.Object.DestroyImmediate(catalogueRoot);
                UnityEngine.Object.DestroyImmediate(nonSurfaceHost.gameObject);
            }
        }

        [TestCase(DecorationModeKind.Floor, DecorationSheetState.Expanded)]
        [TestCase(DecorationModeKind.Wall, DecorationSheetState.Expanded)]
        [TestCase(DecorationModeKind.WallDecor, DecorationSheetState.CompactPreview)]
        public void Controller_keeps_surface_catalogue_expanded_during_preview_but_wall_decor_compact(
            DecorationModeKind mode,
            DecorationSheetState expectedState)
        {
            var controllerRoot = new GameObject("Controller");
            var catalogueRoot = Ui("CatalogueSheet");
            using var action = new ActionFixture();
            try
            {
                var controller = controllerRoot.AddComponent<DecorationModeController>();
                var catalogue = catalogueRoot.AddComponent<DecorationCatalogueView>();
                Set(controller, "activeMode", mode);
                Set(controller, "catalogueView", catalogue);
                Set(controller, "actionBarView", action.View);

                Invoke(controller, "ShowPhase7ActionForActivePreview");

                Assert.That(catalogue.SheetState, Is.EqualTo(expectedState));
                Assert.That(catalogue.AreCategoryRowsVisible,
                    Is.EqualTo(expectedState == DecorationSheetState.Expanded));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controllerRoot);
                UnityEngine.Object.DestroyImmediate(catalogueRoot);
            }
        }

        [Test]
        public void Exit_modal_has_only_continue_and_discard_and_releases_the_consumed_ui_pointer()
        {
            using var fixture = new ExitModalFixture();
            fixture.View.Show();
            fixture.Boundary.RegisterUiPointerPress(19);
            fixture.View.NotifyPointerReleased(19);

            Assert.That(fixture.View.ChoiceLabels, Is.EqualTo(new[] { "Continue Editing", "Discard Changes" }));
            Assert.That(fixture.Boundary.CanProcessScenePointer(19), Is.True);
        }

        [Test]
        public void Tab_request_can_be_rejected_before_active_mode_changes()
        {
            using var fixture = new TabsFixture();
            fixture.View.ModeRequested += _ => false;
            Assert.That(fixture.View.RequestMode(DecorationModeKind.Wall), Is.False);
            Assert.That(fixture.View.ActiveMode, Is.EqualTo(DecorationModeKind.Furniture));
        }

        [Test]
        public void Catalogue_surface_state_supports_multiple_current_wall_layers_and_one_preview_by_item_id()
        {
            using var fixture = new CatalogueFixture();
            fixture.View.BindCategories(new[]
            {
                new DecorationCategoryModel("wall", "Wall", new[]
                {
                    new DecorationCatalogueItemModel("base-using", "Base Using", null, DecorationCatalogueItemKind.WallSurface, false),
                    new DecorationCatalogueItemModel("wains-using", "Wains Using", null, DecorationCatalogueItemKind.WallSurface, false),
                    new DecorationCatalogueItemModel("preview", "Preview", null, DecorationCatalogueItemKind.WallSurface, false),
                    new DecorationCatalogueItemModel("none", "None", null, DecorationCatalogueItemKind.WallSurface, true)
                })
            }, _ => { });

            fixture.View.SetSurfaceStates(new[] { "base-using", "wains-using" }, "preview");
            var tiles = fixture.GeneratedRow.GetComponent<ScrollRect>().content
                .GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .ToDictionary(tile => tile.ItemId);
            Assert.That(tiles["base-using"].transform.Find("UsingCheck").gameObject.activeSelf, Is.True);
            Assert.That(tiles["wains-using"].transform.Find("UsingCheck").gameObject.activeSelf, Is.True);
            Assert.That(tiles["base-using"].transform.Find("PreviewOutline").gameObject.activeSelf, Is.False);
            Assert.That(tiles["preview"].transform.Find("UsingCheck").gameObject.activeSelf, Is.False);
            Assert.That(tiles["preview"].transform.Find("PreviewOutline").gameObject.activeSelf, Is.True);
            Assert.That(tiles["none"].transform.Find("NoneIcon").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void Catalogue_creates_one_clickable_tile_per_item_and_rebind_replaces_old_rows()
        {
            using var fixture = new CatalogueFixture();
            var first = new DecorationCatalogueItemModel("first", "First", null, DecorationCatalogueItemKind.Furniture, false);
            var second = new DecorationCatalogueItemModel("second", "Second", null, DecorationCatalogueItemKind.Furniture, false);
            DecorationCatalogueItemModel selected = null; var calls = 0;
            fixture.View.BindCategories(new[] { new DecorationCategoryModel("one", "One", new[] { first, second }) }, item => { selected = item; calls++; });
            var tile = fixture.GeneratedRow.GetComponent<ScrollRect>().content
                .GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Single(item => item.ItemId == "first")
                .GetComponent<Button>();
            tile.onClick.Invoke();
            Assert.That(selected, Is.SameAs(first)); Assert.That(calls, Is.EqualTo(1));
            fixture.View.BindCategories(null, _ => Assert.Fail());
            Assert.That(fixture.View.CategoryRows, Is.Empty);
        }

        [Test]
        public void Sheet_drag_snaps_and_never_hides_active_preview_actions()
        {
            var root = new GameObject("Sheet");
            try { var view = root.AddComponent<DecorationCatalogueView>(); Assert.That(view.ApplySheetDrag(-999f, true), Is.EqualTo(DecorationSheetState.CompactPreview)); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Exit_modal_keeps_ui_pointer_owned_until_same_gesture_releases()
        {
            using var fixture = new ExitModalFixture();
            fixture.Boundary.RegisterUiPointerPress(7); fixture.View.Show(); fixture.View.Close();
            Assert.That(fixture.Boundary.CanProcessScenePointer(7), Is.False);
            fixture.View.NotifyPointerReleased(7);
            Assert.That(fixture.Boundary.CanProcessScenePointer(7), Is.True);
        }

        [Test]
        public void Sheet_state_updates_real_hierarchy_visibility_and_drag_axis_ownership()
        {
            using var fixture = new CatalogueFixture();
            var categoryRoot = Ui("CategoryRoot", fixture.Root.transform);
            var compactRoot = Ui("CompactRoot", fixture.Root.transform);
            var actionRoot = Ui("ActionRoot", fixture.Root.transform);
            categoryRoot.SetActive(false); compactRoot.SetActive(false); actionRoot.SetActive(false);
            Set(fixture.View, "expandedRoot", categoryRoot);
            Set(fixture.View, "collapsedRoot", compactRoot);
            Set(fixture.View, "sheetActionRoot", actionRoot);

            fixture.View.SetSheetState(DecorationSheetState.Expanded, false);
            Assert.That(categoryRoot.activeSelf, Is.True);
            Assert.That(compactRoot.activeSelf, Is.False);
            Assert.That(actionRoot.activeSelf, Is.True);
            fixture.View.SetSheetState(DecorationSheetState.CompactPreview, false);
            Assert.That(categoryRoot.activeSelf, Is.False);
            Assert.That(compactRoot.activeSelf, Is.True);
            Assert.That(actionRoot.activeSelf, Is.True);
            fixture.View.SetSheetState(DecorationSheetState.TabsOnly, false);
            Assert.That(actionRoot.activeSelf, Is.False);

            fixture.View.BindCategories(new[] { Category("furniture", "Furniture", DecorationCatalogueItemKind.Furniture) }, _ => { });
            var horizontal = fixture.GeneratedRow.GetComponent<ScrollRect>();
            var horizontalBefore = horizontal.content.anchoredPosition;
            var verticalBefore = fixture.View.VerticalScroll.content.anchoredPosition;
            Assert.That(fixture.View.TryRouteNestedDrag(new Vector2(40f, 2f)), Is.EqualTo("Horizontal"));
            Assert.That(horizontal.content.anchoredPosition, Is.Not.EqualTo(horizontalBefore));
            Assert.That(fixture.View.VerticalScroll.content.anchoredPosition, Is.EqualTo(verticalBefore));
            Assert.That(fixture.View.NestedDragOwner, Is.SameAs(horizontal));
            Assert.That(fixture.View.IsSceneDragBlocked, Is.True);
            var horizontalAfter = horizontal.content.anchoredPosition;
            fixture.View.EndNestedDrag();
            fixture.View.BeginNestedDrag(horizontal);
            Assert.That(fixture.View.TryRouteNestedDrag(new Vector2(2f, 40f)), Is.EqualTo("Vertical"));
            Assert.That(fixture.View.VerticalScroll.content.anchoredPosition, Is.Not.EqualTo(verticalBefore));
            Assert.That(horizontal.content.anchoredPosition, Is.EqualTo(horizontalAfter));
            fixture.View.EndNestedDrag();
            Assert.That(fixture.View.IsSceneDragBlocked, Is.False);
        }

        [UnityTest]
        public IEnumerator SheetStateCollapse_TweensCatalogueAndTabsWhileSurfaceFooterRemainsPinned()
        {
            var root = Ui("AnimatedCatalogue");
            try
            {
                var view = root.AddComponent<DecorationCatalogueView>();
                Set(view, "canvasGroup", root.AddComponent<CanvasGroup>());
                Set(view, "expandedRoot", Ui("Expanded", root.transform));
                Set(view, "collapsedRoot", Ui("Compact", root.transform));
                Set(view, "expandedAnchoredPosition", Vector2.zero);
                Set(view, "collapsedAnchoredPosition", new Vector2(0f, -100f));
                Set(view, "hiddenAnchoredPosition", new Vector2(0f, -180f));
                var tabs = Ui("ModeTabs", root.transform).GetComponent<RectTransform>();
                tabs.anchoredPosition = new Vector2(0f, 40f);
                var footer = Ui("SurfaceFooterHost", root.transform).GetComponent<RectTransform>();
                footer.anchoredPosition = new Vector2(0f, 20f);
                Set(view, "sheetActionRoot", footer.gameObject);
                Set(view, "surfaceFooterHost", footer);
                Set(view, "surfaceFooterExpandedAnchoredPosition", new Vector2(0f, 20f));
                view.Configure(new UiPointerBoundary(), new UiTransitionRunner(() => false));
                view.SetSheetState(DecorationSheetState.Expanded, false);
                yield return null;
                ((RectTransform)root.transform).anchoredPosition = Vector2.zero;
                var tabsStart = tabs.position;
                var footerStart = footer.position;

                view.SetSheetState(DecorationSheetState.CompactPreview, true);
                yield return null;
                var intermediate = ((RectTransform)root.transform).anchoredPosition.y;
                var group = root.GetComponent<CanvasGroup>();
                Assert.That(group.blocksRaycasts, Is.True,
                    "Visible catalogue controls must remain raycastable while the 0.16s tween is running.");
                Assert.That(group.interactable, Is.True,
                    "A visible tile or collapse control must still respond during the sheet tween.");
                Assert.That(intermediate, Is.LessThan(0f).And.GreaterThan(-100f),
                    "Collapse must expose an intermediate tween frame instead of jumping.");
                Assert.That(tabs.position.y - tabsStart.y, Is.EqualTo(intermediate).Within(.01f),
                    "Raised tabs must travel with their Bottom Sheet parent.");
                Assert.That(footer.position.y, Is.EqualTo(footerStart.y).Within(.01f),
                    "Surface Confirm/Cancel must stay pinned to the visible Bottom Sheet bottom during the tween.");
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(((RectTransform)root.transform).anchoredPosition.y, Is.EqualTo(-100f).Within(.01f));
                Assert.That(footer.position.y, Is.EqualTo(footerStart.y).Within(.01f));
                Assert.That(group.blocksRaycasts, Is.True);
                Assert.That(group.interactable, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void SheetStateTransition_UsesExactApprovedDuration()
        {
            var durationField = typeof(DecorationCatalogueView).GetField(
                "TransitionDuration",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(durationField, Is.Not.Null,
                "The production catalogue transition constant must remain discoverable for regression evidence.");
            Assert.That((float)durationField.GetRawConstantValue(), Is.EqualTo(0.16f),
                "Expanded/Compact/Tabs transitions must use the approved exact 0.16 second duration.");
        }

        [Test]
        public void Sheet_state_exposes_content_visibility_and_partial_next_geometry()
        {
            using var fixture = new CatalogueFixture();
            fixture.View.BindCategories(new[] { Category("furniture", "Furniture", DecorationCatalogueItemKind.Furniture) }, _ => { });
            fixture.View.SetSheetState(DecorationSheetState.CompactPreview, false);
            Assert.That(fixture.View.AreCategoryRowsVisible, Is.False);

            var viewport = fixture.GeneratedRow.Find("Viewport") as RectTransform;
            viewport.sizeDelta = new Vector2(250f, 48f);
            var wideCue = fixture.View.PartialNextCardViewportInset;
            viewport.sizeDelta = new Vector2(200f, 48f);
            var narrowCue = fixture.View.PartialNextCardViewportInset;
            Assert.That(wideCue, Is.GreaterThan(0f));
            Assert.That(narrowCue, Is.GreaterThan(0f));
            Assert.That(narrowCue, Is.Not.EqualTo(wideCue));
        }

        [Test]
        public void Nested_drag_lifecycle_locks_axis_and_source_row_until_end_or_rebind()
        {
            using var fixture = new CatalogueFixture();
            fixture.View.BindCategories(new[]
            {
                Category("one", "One", DecorationCatalogueItemKind.Furniture),
                Category("two", "Two", DecorationCatalogueItemKind.Furniture)
            }, _ => { });
            var first = fixture.Root.transform.Find("Rows/CategoryRow_one").GetComponent<ScrollRect>();
            var second = fixture.Root.transform.Find("Rows/CategoryRow_two").GetComponent<ScrollRect>();
            var firstBefore = first.content.anchoredPosition;
            var secondBefore = second.content.anchoredPosition;

            fixture.View.BeginNestedDrag(second);
            fixture.View.UpdateNestedDrag(new Vector2(2f, 2f)); // below threshold
            fixture.View.UpdateNestedDrag(new Vector2(40f, 2f));
            var secondAfterHorizontal = second.content.anchoredPosition;
            fixture.View.UpdateNestedDrag(new Vector2(1f, 40f)); // must not switch before End
            Assert.That(fixture.View.NestedDragOwner, Is.SameAs(second));
            Assert.That(second.content.anchoredPosition.y, Is.EqualTo(secondAfterHorizontal.y));
            Assert.That(second.content.anchoredPosition.x, Is.Not.EqualTo(secondBefore.x));
            Assert.That(first.content.anchoredPosition, Is.EqualTo(firstBefore));

            fixture.View.EndNestedDrag();
            fixture.View.BeginNestedDrag(second);
            fixture.View.UpdateNestedDrag(new Vector2(1f, 40f));
            Assert.That(fixture.View.NestedDragOwner, Is.SameAs(fixture.View.VerticalScroll));
            fixture.View.BindCategories(null, _ => { });
            Assert.That(fixture.View.NestedDragOwner, Is.Null);
            Assert.That(fixture.View.IsSceneDragBlocked, Is.False);
        }

        [Test]
        public void Nested_drag_accumulates_per_frame_delta_before_threshold_lock()
        {
            using var fixture = new CatalogueFixture();
            fixture.View.BindCategories(new[]
            {
                Category("furniture", "Furniture", DecorationCatalogueItemKind.Furniture)
            }, _ => { });
            var horizontal = fixture.GeneratedRow.GetComponent<ScrollRect>();
            var before = horizontal.content.anchoredPosition;

            fixture.View.BeginNestedDrag(horizontal);
            Assert.That(fixture.View.UpdateNestedDrag(new Vector2(3f, 1f)), Is.EqualTo("Pending"));
            Assert.That(fixture.View.UpdateNestedDrag(new Vector2(3f, 1f)), Is.EqualTo("Pending"));
            Assert.That(fixture.View.UpdateNestedDrag(new Vector2(3f, 1f)), Is.EqualTo("Horizontal"));

            Assert.That(fixture.View.NestedDragOwner, Is.SameAs(horizontal));
            Assert.That(horizontal.content.anchoredPosition.x, Is.GreaterThan(before.x));
            Assert.That(horizontal.content.anchoredPosition.y, Is.EqualTo(before.y));
        }

        [Test]
        public void Nested_drag_disable_releases_owner_and_scene_block()
        {
            using var fixture = new CatalogueFixture();
            fixture.View.BindCategories(new[]
            {
                Category("furniture", "Furniture", DecorationCatalogueItemKind.Furniture)
            }, _ => { });
            fixture.View.BeginNestedDrag(fixture.GeneratedRow.GetComponent<ScrollRect>());
            fixture.View.UpdateNestedDrag(new Vector2(40f, 2f));

            fixture.Root.SetActive(false);

            Assert.That(fixture.View.NestedDragOwner, Is.Null);
            Assert.That(fixture.View.IsSceneDragBlocked, Is.False);
        }

        [Test]
        public void Nested_drag_destroy_releases_owner_and_scene_block()
        {
            using var fixture = new CatalogueFixture();
            fixture.View.BindCategories(new[]
            {
                Category("furniture", "Furniture", DecorationCatalogueItemKind.Furniture)
            }, _ => { });
            fixture.View.BeginNestedDrag(fixture.GeneratedRow.GetComponent<ScrollRect>());
            fixture.View.UpdateNestedDrag(new Vector2(40f, 2f));
            var view = fixture.View;

            UnityEngine.Object.DestroyImmediate(view);

            Assert.That(view.NestedDragOwner, Is.Null);
            Assert.That(view.IsSceneDragBlocked, Is.False);
        }

        [Test]
        public void Shared_modal_opt_in_delays_owned_pointer_release_until_gesture_end()
        {
            var root = new GameObject("Modal");
            try
            {
                var modal = root.AddComponent<AnimalCafeModalView>(); var boundary = new UiPointerBoundary();
                modal.ConfigureDelayedPointerRelease(boundary); modal.RetainPointerUntilGestureEnd(12);
                Assert.That(boundary.CanProcessScenePointer(12), Is.False);
                modal.ReleaseRetainedPointer(12);
                Assert.That(boundary.CanProcessScenePointer(12), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void Shared_modal_reconfigure_releases_old_boundary_and_destroy_releases_current_retention()
        {
            var root = new GameObject("Modal");
            var first = new UiPointerBoundary();
            var second = new UiPointerBoundary();
            var modal = root.AddComponent<AnimalCafeModalView>();
            try
            {
                modal.ConfigureDelayedPointerRelease(first);
                modal.RetainPointerUntilGestureEnd(21);
                modal.ConfigureDelayedPointerRelease(second);

                Assert.That(first.CanProcessScenePointer(21), Is.True);
                modal.RetainPointerUntilGestureEnd(22);
                Assert.That(second.CanProcessScenePointer(22), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            Assert.That(second.CanProcessScenePointer(22), Is.True);
        }

        [Test]
        public void Exit_modal_button_gesture_retains_pointer_until_release()
        {
            using var fixture = new ExitModalFixture();
            fixture.View.ConfigureGestureRetention();
            fixture.View.BeginButtonGesture(5); fixture.View.Close();
            Assert.That(fixture.Boundary.CanProcessScenePointer(5), Is.False);
            fixture.View.NotifyPointerReleased(5);
            Assert.That(fixture.Boundary.CanProcessScenePointer(5), Is.True);
        }

        [Test]
        public void Exit_modal_close_without_active_gesture_deactivates_immediately()
        {
            using var fixture = new ExitModalFixture();
            fixture.View.Show();

            fixture.View.Close();

            Assert.That(fixture.Root.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Exit_modal_real_continue_gesture_blocks_scene_through_pointer_up_frame_then_closes_next_frame()
        {
            using var fixture = new ExitModalFixture();
            var eventSystemRoot = new GameObject("EventSystem", typeof(EventSystem));
            try
            {
                var pointer = new PointerEventData(eventSystemRoot.GetComponent<EventSystem>()) { pointerId = 41 };
                var requested = 0;
                fixture.View.ContinueEditingRequested += () => requested++;
                fixture.View.Show();

                ExecuteEvents.Execute<IPointerDownHandler>(fixture.ContinueButton.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                Assert.That(fixture.Boundary.CanProcessScenePointer(pointer.pointerId), Is.False);

                ExecuteEvents.Execute<IPointerUpHandler>(fixture.ContinueButton.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                var sameGestureSceneMutations = fixture.Boundary.CanProcessScenePointer(pointer.pointerId) ? 1 : 0;
                Assert.That(fixture.Root.activeSelf, Is.True);
                Assert.That(fixture.ContinueButton.interactable, Is.True);
                Assert.That(fixture.DiscardButton.interactable, Is.True);

                ExecuteEvents.Execute<IPointerClickHandler>(fixture.ContinueButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                if (fixture.Boundary.CanProcessScenePointer(pointer.pointerId)) sameGestureSceneMutations++;
                Assert.That(requested, Is.EqualTo(1));
                Assert.That(sameGestureSceneMutations, Is.EqualTo(0));
                Assert.That(fixture.Root.activeSelf, Is.True, "The active host must survive until deferred release.");
                Assert.That(fixture.ContinueButton.interactable, Is.False);
                Assert.That(fixture.DiscardButton.interactable, Is.False);
                Assert.That(fixture.Boundary.CanProcessScenePointer(pointer.pointerId), Is.False);

                yield return null;

                Assert.That(fixture.Boundary.CanProcessScenePointer(pointer.pointerId), Is.True);
                Assert.That(fixture.Root.activeSelf, Is.False);
                Assert.That(fixture.Boundary.CanProcessScenePointer(141), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(eventSystemRoot); }
        }

        [UnityTest]
        public IEnumerator Exit_modal_real_discard_gesture_blocks_scene_through_pointer_up_frame_then_closes_next_frame()
        {
            using var fixture = new ExitModalFixture();
            var eventSystemRoot = new GameObject("EventSystem", typeof(EventSystem));
            try
            {
                var pointer = new PointerEventData(eventSystemRoot.GetComponent<EventSystem>()) { pointerId = 42 };
                var requested = 0;
                fixture.View.DiscardChangesRequested += () => requested++;
                fixture.View.Show();

                ExecuteEvents.Execute<IPointerDownHandler>(fixture.DiscardButton.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                Assert.That(fixture.Boundary.CanProcessScenePointer(pointer.pointerId), Is.False);

                ExecuteEvents.Execute<IPointerUpHandler>(fixture.DiscardButton.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                var sameGestureSceneMutations = fixture.Boundary.CanProcessScenePointer(pointer.pointerId) ? 1 : 0;
                Assert.That(fixture.Root.activeSelf, Is.True);
                Assert.That(fixture.ContinueButton.interactable, Is.True);
                Assert.That(fixture.DiscardButton.interactable, Is.True);

                ExecuteEvents.Execute<IPointerClickHandler>(fixture.DiscardButton.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                if (fixture.Boundary.CanProcessScenePointer(pointer.pointerId)) sameGestureSceneMutations++;
                Assert.That(requested, Is.EqualTo(1));
                Assert.That(sameGestureSceneMutations, Is.EqualTo(0));
                Assert.That(fixture.Root.activeSelf, Is.True, "The active host must survive until deferred release.");
                Assert.That(fixture.ContinueButton.interactable, Is.False);
                Assert.That(fixture.DiscardButton.interactable, Is.False);
                Assert.That(fixture.Boundary.CanProcessScenePointer(pointer.pointerId), Is.False);

                yield return null;

                Assert.That(fixture.Boundary.CanProcessScenePointer(pointer.pointerId), Is.True);
                Assert.That(fixture.Root.activeSelf, Is.False);
                Assert.That(fixture.Boundary.CanProcessScenePointer(142), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(eventSystemRoot); }
        }

        [UnityTest]
        public IEnumerator Exit_modal_pending_release_recovers_on_interface_reconfigure_and_destroy()
        {
            using var fixture = new ExitModalFixture();
            var eventSystemRoot = new GameObject("EventSystem", typeof(EventSystem));
            try
            {
                var eventSystem = eventSystemRoot.GetComponent<EventSystem>();
                var first = new InterfacePointerBoundary();
                var second = new InterfacePointerBoundary();

                fixture.View.Configure(first);
                fixture.View.Show();
                var reconfigurePointer = new PointerEventData(eventSystem) { pointerId = 51 };
                ExecuteEvents.Execute<IPointerDownHandler>(fixture.ContinueButton.gameObject, reconfigurePointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute<IPointerUpHandler>(fixture.ContinueButton.gameObject, reconfigurePointer, ExecuteEvents.pointerUpHandler);
                Assert.That(first.CanProcessScenePointer(51), Is.False);

                fixture.View.Configure(second);
                Assert.That(first.CanProcessScenePointer(51), Is.True);
                yield return null;
                Assert.That(first.CanProcessScenePointer(51), Is.True);
                Assert.That(second.CanProcessScenePointer(51), Is.True);

                fixture.View.Show();
                var destroyPointer = new PointerEventData(eventSystem) { pointerId = 52 };
                ExecuteEvents.Execute<IPointerDownHandler>(fixture.DiscardButton.gameObject, destroyPointer, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute<IPointerUpHandler>(fixture.DiscardButton.gameObject, destroyPointer, ExecuteEvents.pointerUpHandler);
                Assert.That(second.CanProcessScenePointer(52), Is.False);

                UnityEngine.Object.DestroyImmediate(fixture.View);
                Assert.That(second.CanProcessScenePointer(52), Is.True);
                yield return null;
                Assert.That(second.CanProcessScenePointer(52), Is.True);
                Assert.That(second.CanProcessScenePointer(152), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(eventSystemRoot); }
        }

        [Test]
        public void Tile_typed_rebind_replaces_previous_typed_callback_and_null_clears_it()
        {
            using var fixture = new TileFixture();
            var first = 0; var second = 0;
            var item = new DecorationCatalogueItemModel("one", "One", null, DecorationCatalogueItemKind.Furniture, false);
            fixture.View.Bind(item, _ => first++);
            fixture.View.Bind(item, _ => second++);
            fixture.Root.GetComponent<Button>().onClick.Invoke();
            Assert.That(first, Is.EqualTo(0)); Assert.That(second, Is.EqualTo(1));
            fixture.View.Bind((DecorationCatalogueItemModel)null, _ => second++);
            fixture.Root.GetComponent<Button>().onClick.Invoke();
            Assert.That(second, Is.EqualTo(1));
        }

        [Test]
        public void Tile_legacy_and_typed_bindings_never_leave_stale_click_callbacks()
        {
            using var fixture = new TileFixture();
            var legacy = 0; var typed = 0;
            var item = new DecorationCatalogueItemModel("one", "One", null, DecorationCatalogueItemKind.Furniture, false);
            fixture.View.Bind(fixture.LegacyEntry, _ => legacy++);
            fixture.View.Bind(item, _ => typed++);
            Assert.That(fixture.View.Definition, Is.Null);
            Assert.That(fixture.View.ItemId, Is.EqualTo("one"));
            Assert.That(fixture.View.IsInteractable, Is.True);
            fixture.Root.GetComponent<Button>().onClick.Invoke();
            Assert.That(legacy, Is.EqualTo(0)); Assert.That(typed, Is.EqualTo(1));
            fixture.View.Bind(fixture.LegacyEntry, _ => legacy++);
            fixture.Root.GetComponent<Button>().onClick.Invoke();
            Assert.That(legacy, Is.EqualTo(1)); Assert.That(typed, Is.EqualTo(1));
        }

        [Test]
        public void Action_bar_tool_buttons_raise_real_events_once()
        {
            using var fixture = new ActionFixture();
            var undo = 0; var apply = 0;
            fixture.View.UndoLastRequested += () => undo++;
            fixture.View.ApplyAllRequested += () => apply++;
            fixture.View.SetModeActions(DecorationModeKind.Floor, false);
            fixture.GetButton("undoLastButton").onClick.Invoke();
            fixture.GetButton("applyAllButton").onClick.Invoke();
            Assert.That(undo, Is.EqualTo(1)); Assert.That(apply, Is.EqualTo(1));
        }

        private static DecorationCategoryModel Category(string id, string name, DecorationCatalogueItemKind kind)
        {
            return new DecorationCategoryModel(id, name, new[]
            {
                new DecorationCatalogueItemModel(id + ".one", "One", null, kind, false),
                new DecorationCatalogueItemModel(id + ".two", "Two", null, kind, false)
            });
        }

        private sealed class TabsFixture : IDisposable
        {
            public TabsFixture()
            {
                Root = Ui("Tabs");
                Root.SetActive(false);
                View = Root.AddComponent<DecorationModeTabsView>();
                Furniture = Button("Furniture", Root.transform);
                var floor = Button("Floor", Root.transform);
                Wall = Button("Wall", Root.transform);
                var decor = Button("Wall Decor", Root.transform);
                Buttons = new[] { Furniture, floor, Wall, decor };
                Set(View, "furnitureButton", Furniture);
                Set(View, "floorButton", floor);
                Set(View, "wallButton", Wall);
                Set(View, "wallDecorButton", decor);
                Root.SetActive(true);
            }
            public GameObject Root { get; }
            public DecorationModeTabsView View { get; }
            public Button Furniture { get; }
            public Button Wall { get; }
            public Button[] Buttons { get; }
            public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);
        }

        private sealed class FloorRangeFixture : IDisposable
        {
            public FloorRangeFixture()
            {
                Root = new GameObject("FloorRange", typeof(RectTransform));
                WholeRoom = Button("Whole Room", Root.transform);
                SingleGrid = Button("Single Grid", Root.transform);
                View = Root.AddComponent<DecorationFloorRangeView>();
                View.Configure(WholeRoom, SingleGrid);
            }

            public GameObject Root { get; }
            public DecorationFloorRangeView View { get; }
            public Button WholeRoom { get; }
            public Button SingleGrid { get; }
            public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);
        }

        private sealed class CatalogueFixture : IDisposable
        {
            public CatalogueFixture()
            {
                Root = Ui("Catalogue");
                View = Root.AddComponent<DecorationCatalogueView>();
                var vertical = Root.AddComponent<ScrollRect>();
                var rows = Ui("Rows", Root.transform).GetComponent<RectTransform>();
                vertical.content = rows;
                Set(View, "verticalScroll", vertical);
                Set(View, "categoryContent", rows);
                var template = Ui("RowTemplate", rows);
                Ui("CategoryLabel", template.transform).AddComponent<TextMeshProUGUI>();
                var scroll = template.AddComponent<ScrollRect>();
                RowViewport = Ui("Viewport", template.transform).GetComponent<RectTransform>();
                RowViewport.sizeDelta = new Vector2(250f, 48f);
                var content = Ui("Content", RowViewport).GetComponent<RectTransform>();
                content.gameObject.AddComponent<HorizontalLayoutGroup>();
                scroll.viewport = RowViewport;
                scroll.content = content;
                template.SetActive(false);
                Set(View, "categoryRowTemplate", template);
            }
            public GameObject Root { get; }
            public DecorationCatalogueView View { get; }
            public RectTransform RowViewport { get; }
            public Transform GeneratedRow => Root.transform.Find("Rows").GetChild(Root.transform.Find("Rows").childCount - 1);
            public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);
        }

        private sealed class TileFixture : IDisposable
        {
            private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
            public TileFixture()
            {
                Root = Ui("Tile");
                View = Root.AddComponent<DecorationCatalogueTileView>();
                Set(View, "button", Root.AddComponent<Button>());
                Set(View, "thumbnailImage", Thumbnail = Ui("Thumbnail", Root.transform).AddComponent<Image>());
                Thumbnail.rectTransform.anchorMin = new Vector2(0f, .22f);
                Thumbnail.rectTransform.anchorMax = Vector2.one;
                Set(View, "nameLabel", Name = Ui("Name", Root.transform).AddComponent<TextMeshProUGUI>());
                Set(View, "footprintLabel", Footprint = Ui("Footprint", Root.transform).AddComponent<TextMeshProUGUI>());
                Set(View, "usingCheck", UsingCheck = Ui("UsingCheck", Root.transform));
                Set(View, "previewOutline", PreviewOutline = Ui("PreviewOutline", Root.transform));
                Set(View, "noneIcon", NoneIcon = Ui("NoneIcon", Root.transform));
                LegacyPrefab = new GameObject("LegacyPrefab");
                LegacyDefinition = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
                LegacyEntry = new DecorationCatalogueEntry();
                Set(LegacyDefinition, "definitionId", "legacy.fixture"); Set(LegacyDefinition, "displayName", "Legacy"); Set(LegacyDefinition, "prefab", LegacyPrefab);
                Set(LegacyEntry, "definition", LegacyDefinition);
                var texture = new Texture2D(1, 1); texture.SetPixel(0, 0, Color.white); texture.Apply();
                var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
                Set(LegacyEntry, "thumbnail", sprite);
                owned.Add(texture); owned.Add(sprite);
                owned.Add(LegacyPrefab); owned.Add(LegacyDefinition);
            }
            public GameObject Root { get; }
            public DecorationCatalogueTileView View { get; }
            public Image Thumbnail { get; }
            public TextMeshProUGUI Name { get; }
            public TextMeshProUGUI Footprint { get; }
            public GameObject UsingCheck { get; }
            public GameObject PreviewOutline { get; }
            public GameObject NoneIcon { get; }
            public FurnitureDefinitionAsset LegacyDefinition { get; }
            public DecorationCatalogueEntry LegacyEntry { get; }
            public GameObject LegacyPrefab { get; }
            public void Dispose() { UnityEngine.Object.DestroyImmediate(Root); foreach (var item in owned) UnityEngine.Object.DestroyImmediate(item); }
        }

        private sealed class ActionFixture : IDisposable
        {
            public ActionFixture()
            {
                Root = Ui("ActionBar");
                View = Root.AddComponent<DecorationActionBarView>();
                Set(View, "useReadableActionLabels", true);
                Panel = Ui("ActionPanel", Root.transform).GetComponent<RectTransform>();
                Set(View, "presentationRoot", Panel);
                foreach (var field in new[] { "undoLastButton", "applyAllButton", "storeButton", "rotateButton", "cancelButton", "confirmButton" })
                {
                    var button = Button(field, Panel);
                    var label = Ui("Label", button.transform).AddComponent<TextMeshProUGUI>();
                    label.rectTransform.anchorMin = Vector2.one * .5f;
                    label.rectTransform.anchorMax = Vector2.one * .5f;
                    label.rectTransform.sizeDelta = new Vector2(32f, 40f);
                    label.text = field == "rotateButton" ? "R" :
                        field == "cancelButton" ? "×" :
                        field == "confirmButton" ? "✓" : field;
                    Set(View, field, button);
                }
                Feedback = Ui("Feedback", Root.transform).AddComponent<TextMeshProUGUI>();
                Set(View, "feedbackLabel", Feedback);
            }
            public GameObject Root { get; }
            public DecorationActionBarView View { get; }
            public RectTransform Panel { get; }
            public TextMeshProUGUI Feedback { get; }
            public Button GetButton(string name) => Panel.Find(name).GetComponent<Button>();
            public TextMeshProUGUI Label(string name) => GetButton(name).transform.Find("Label").GetComponent<TextMeshProUGUI>();
            public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);
        }

        private sealed class ExitModalFixture : IDisposable
        {
            public ExitModalFixture()
            {
                Root = Ui("ExitModal");
                View = Root.AddComponent<DecorationExitModalView>();
                Set(View, "continueButton", Button("Continue Editing", Root.transform));
                Set(View, "discardButton", Button("Discard Changes", Root.transform));
                Boundary = new UiPointerBoundary();
                View.Configure(Boundary);
            }
            public GameObject Root { get; }
            public DecorationExitModalView View { get; }
            public Button ContinueButton => Root.transform.Find("Continue Editing").GetComponent<Button>();
            public Button DiscardButton => Root.transform.Find("Discard Changes").GetComponent<Button>();
            public UiPointerBoundary Boundary { get; }
            public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);
        }

        private sealed class InterfacePointerBoundary : IUiPointerOwnershipRegistrar
        {
            private readonly HashSet<int> uiPointers = new HashSet<int>();

            public void RegisterUiPointerPress(int pointerId) => uiPointers.Add(pointerId);
            public void RegisterScenePointerPress(int pointerId) { }
            public bool CanProcessScenePointer(int pointerId) => !uiPointers.Contains(pointerId);
            public void ReleasePointer(int pointerId) => uiPointers.Remove(pointerId);
        }

        private static GameObject Ui(string name, Transform parent = null)
        {
            var result = new GameObject(name, typeof(RectTransform));
            result.GetComponent<RectTransform>().sizeDelta = new Vector2(96f, 48f);
            if (parent != null) result.transform.SetParent(parent, false);
            return result;
        }

        private static Button Button(string name, Transform parent)
        {
            var root = Ui(name, parent);
            root.AddComponent<Image>();
            return root.AddComponent<Button>();
        }

        private static void Set(object target, string field, object value)
        {
            var found = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (found == null) throw new MissingFieldException(target.GetType().Name, field);
            found.SetValue(target, value);
        }

        private static void Invoke(object target, string method)
        {
            var found = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            if (found == null) throw new MissingMethodException(target.GetType().Name, method);
            found.Invoke(target, null);
        }
    }
}

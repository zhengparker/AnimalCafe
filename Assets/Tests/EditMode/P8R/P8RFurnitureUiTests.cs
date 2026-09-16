using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RFurnitureUiTests
    {
        private const string Root = "Assets/UI/P8R/";
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        private Vector2? previousLogicalViewport;

        [SetUp]
        public void SetUp()
        {
            previousLogicalViewport = AnimalCafe.UI.P8R.P8RMobileMetrics.EditorLogicalViewportOverride;
            AnimalCafe.UI.P8R.P8RMobileMetrics.EditorLogicalViewportOverride = null;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in owned.AsEnumerable().Reverse())
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            owned.Clear();
            AnimalCafe.UI.P8R.P8RMobileMetrics.EditorLogicalViewportOverride = previousLogicalViewport;
        }

        [Test]
        public void DisplayCatalogue_RetainsDefinitionsAndUsesApprovedThumbnails()
        {
            var old = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                "Assets/UI/Phase8/Catalogues/DC_Phase8Furniture.asset");
            var display = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(Root + "DC_P8RFurniture.asset");
            Assert.That(display, Is.Not.Null, "The approved display catalogue has not been authored.");
            Assert.That(display.Entries.Select(e => e.Definition), Is.EqualTo(old.Entries.Select(e => e.Definition)));
            Assert.That(display.Entries.Count, Is.EqualTo(6));
            foreach (var entry in display.Entries)
                Assert.That(AssetDatabase.GetAssetPath(entry.Thumbnail), Does.StartWith(Root + "Thumbnails/"));
        }

        [TestCase(DecorationCatalogueItemKind.Furniture, true, 4)]
        [TestCase(DecorationCatalogueItemKind.CashRegister, false, 3)]
        [TestCase(DecorationCatalogueItemKind.CoffeeMachine, true, 4)]
        [TestCase(DecorationCatalogueItemKind.PickUpPoint, true, 3)]
        public void ActionRebinding_KeepsPngStatesAndSemanticActions(DecorationCatalogueItemKind kind, bool existing, int count)
        {
            var view = Copy("PF_UI_P8RActionBar").GetComponent<DecorationActionBarView>();
            view.SetCatalogueItemActions(kind, existing);
            view.Show(existing, false, PlacementFeedbackKey.None);
            view.SetPresentation(existing ? DecorationActionPresentation.Existing : DecorationActionPresentation.New,
                new Vector2(500, 1000), new Rect(0, 0, 1080, 1920));
            Assert.That(view.VisibleActionLabels.Length, Is.EqualTo(count));
            Assert.That(view.VisibleActionLabels.Contains("Rotate"), Is.EqualTo(kind != DecorationCatalogueItemKind.PickUpPoint));
            Assert.That(view.VisibleActionLabels.Contains("Store"), Is.EqualTo(existing));
            foreach (var field in new[] { "storeButton", "cancelButton", "rotateButton", "confirmButton" })
            {
                var button = Field<Button>(view, field);
                Assert.That(button.transition, Is.EqualTo(Selectable.Transition.SpriteSwap));
                Assert.That(button.image.color, Is.EqualTo(Color.white));
                Assert.That(button.image.canvasRenderer.GetColor(), Is.EqualTo(Color.white));
                Assert.That(button.colors.normalColor, Is.EqualTo(Color.white));
                var role = field == "storeButton" ? "destructive" : field == "confirmButton" ? "primary" : "secondary";
                AssertFrame(button.spriteState.pressedSprite, "button_" + role + "_pressed");
                AssertFrame(button.spriteState.disabledSprite, "button_disabled");
                Assert.That(button.transform.Find("Icon")?.GetComponent<Image>()?.sprite, Is.Not.Null);
                Assert.That(((RectTransform)button.transform).rect.height, Is.GreaterThanOrEqualTo(48));
                Assert.That(button.transform.Find("Label").gameObject.activeSelf, Is.False,
                    "Floating actions stay icon-only; readable names belong in tooltips.");
            }
            Assert.That(Field<Button>(view, "storeButton").GetComponentsInChildren<TMP_Text>(true)
                .Any(text => text.text == "Put Away"), Is.True);
            Assert.That(Field<Button>(view, "confirmButton").interactable, Is.False);
            var confirmations = 0;
            view.ConfirmRequested += () => confirmations++;
            Field<Button>(view, "confirmButton").onClick.Invoke();
            Assert.That(confirmations, Is.Zero);
            view.Show(existing, true, PlacementFeedbackKey.None);
            Field<Button>(view, "confirmButton").onClick.Invoke();
            Field<Button>(view, "confirmButton").onClick.Invoke();
            Assert.That(confirmations, Is.EqualTo(1));
        }

        [Test]
        public void Tabs_KeepSelectedSpriteAndWhiteTintAcrossModeChanges()
        {
            var tabs = Copy("PF_UI_P8RCatalogue").GetComponentInChildren<DecorationModeTabsView>(true);
            foreach (DecorationModeKind mode in Enum.GetValues(typeof(DecorationModeKind)))
            {
                tabs.SetActive(mode);
                var active = mode == DecorationModeKind.Furniture ? "furnitureButton" : mode == DecorationModeKind.Floor
                    ? "floorButton" : mode == DecorationModeKind.Wall ? "wallButton" : "wallDecorButton";
                foreach (var field in new[] { "furnitureButton", "floorButton", "wallButton", "wallDecorButton" })
                {
                    var button = Field<Button>(tabs, field);
                    Assert.That(button.image.color, Is.EqualTo(Color.white));
                    AssertFrame(button.image.sprite, field == active ? "CategoryTabs/category_tab_selected" : "CategoryTabs/category_tab_idle");
                }
            }
        }

        [Test]
        public void SharedModes_RoundTripKeepsExplicitPngStatesAndCorrectIconOrTextPresentation()
        {
            var view = Copy("PF_UI_P8RActionBar").GetComponent<DecorationActionBarView>();
            foreach (var mode in new[] { DecorationModeKind.Furniture, DecorationModeKind.Floor,
                DecorationModeKind.Wall, DecorationModeKind.WallDecor, DecorationModeKind.Furniture })
            {
                view.SetModeActions(mode, true);
                var surface = mode == DecorationModeKind.Floor || mode == DecorationModeKind.Wall;
                view.Show(!surface, true, PlacementFeedbackKey.None);
                foreach (var button in view.GetComponentsInChildren<Button>(true).Where(button => button.gameObject.activeInHierarchy))
                {
                    Assert.That(button.transition, Is.EqualTo(Selectable.Transition.SpriteSwap), mode + ": " + button.name);
                    var role = button == Field<Button>(view, "confirmButton") ? "primary"
                        : !surface && button == Field<Button>(view, "storeButton") ? "destructive" : "secondary";
                    AssertFrame(button.image.sprite, "button_" + role + "_normal");
                    AssertFrame(button.spriteState.disabledSprite, "button_disabled");
                    var compactFloorUtility = mode == DecorationModeKind.Floor
                        && (button == Field<Button>(view, "undoLastButton")
                            || button == Field<Button>(view, "rotateButton")
                            || button == Field<Button>(view, "applyAllButton"));
                    var iconOnly = !surface || compactFloorUtility;
                    Assert.That(button.transform.Find("Label").gameObject.activeSelf, Is.EqualTo(!iconOnly));
                    var icon = button.transform.Find("Icon");
                    if (icon != null)
                    {
                        Assert.That(icon.gameObject.activeSelf, Is.EqualTo(iconOnly));
                        if (iconOnly) Assert.That(icon.GetComponent<Image>().sprite, Is.Not.Null);
                    }
                }
                if (mode == DecorationModeKind.Floor)
                {
                    view.SetFloorUtilityActionsEnabled(false);
                    Assert.That(Field<Button>(view, "undoLastButton").interactable, Is.False);
                    Assert.That(Field<Button>(view, "rotateButton").interactable, Is.False);
                }
            }
        }

        [Test]
        public void TileRebinding_KeepsTwoLineFurnitureAndIndependentPreviewAppliedStates()
        {
            var catalogue = Copy("PF_UI_P8RCatalogue").GetComponent<DecorationCatalogueView>();
            var tile = Field<DecorationCatalogueTileView>(catalogue, "categoryTileTemplate");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Thumbnails/TH_Equipment_CoffeeMachine_01.png");
            tile.Bind(new DecorationCatalogueItemModel("coffee-machine", "Coffee Machine", sprite,
                DecorationCatalogueItemKind.CoffeeMachine, false), null);
            var text = Field<TMP_Text>(tile, "nameLabel");
            Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
            Assert.That(text.maxVisibleLines, Is.EqualTo(2));
            Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(28));
            tile.SetSurfaceState(true, true);
            Assert.That(Field<GameObject>(tile, "usingCheck").activeSelf, Is.True);
            Assert.That(Field<GameObject>(tile, "previewOutline").activeSelf, Is.True);
            tile.SetSurfaceState(false, true);
            Assert.That(Field<GameObject>(tile, "previewOutline").activeSelf, Is.True);
            tile.Bind(new DecorationCatalogueItemModel("floor", "Floor", sprite,
                DecorationCatalogueItemKind.Floor, false), null);
            Assert.That(text.gameObject.activeSelf, Is.False, "Surface cards remain image-only.");
        }

        [Test]
        public void ModalLayout_HiddenWithoutCanvasKeepsFiniteBoundedContentAndReadableLabels()
        {
            var modal = Copy("PF_UI_P8RPutAwayModal").GetComponent<DecorationStoreModalView>();
            modal.gameObject.SetActive(false);
            Assert.That(modal.GetComponentInParent<Canvas>(), Is.Null);
            Assert.That(modal.ContentRect.anchorMin.y, Is.EqualTo(.5f));
            Assert.That(modal.ContentRect.anchorMax.y, Is.EqualTo(.5f));
            Assert.That(modal.ContentRect.anchoredPosition.y, Is.EqualTo(0f));
            foreach (var field in new[] { "confirmButton", "cancelButton" })
            {
                var button = Field<Button>(modal, field);
                AnimalCafe.UI.P8R.P8RButtonLayout.TextButton(button);
                var label = button.transform.Find("Label").GetComponent<TMP_Text>();
                Assert.That(label.rectTransform.rect.width, Is.GreaterThanOrEqualTo(label.GetPreferredValues(label.text).x));
                var rect = (RectTransform)button.transform;
                foreach (var child in new[] { "Icon", "Label" })
                {
                    var position = ((RectTransform)button.transform.Find(child)).anchoredPosition;
                    Assert.That(float.IsNaN(position.x) || float.IsInfinity(position.x)
                        || float.IsNaN(position.y) || float.IsInfinity(position.y), Is.False, child);
                    Assert.That(rect.rect.Contains(position), Is.True, child + " cannot serialize absent-mesh sentinel outside the button.");
                }
            }
        }

        [Test]
        public void CatalogueControls_HaveBoundedPickupAndReadableTabs()
        {
            var catalogue = Copy("PF_UI_P8RCatalogue").GetComponent<DecorationCatalogueView>();
            ((RectTransform)catalogue.transform).sizeDelta = new Vector2(1080, 1920);
            Canvas.ForceUpdateCanvases();
            var pickup = (RectTransform)Field<Button>(catalogue, "pickUpPointButton").transform;
            Assert.That(pickup.rect.width, Is.GreaterThan(0));
            Assert.That(pickup.rect.width, Is.LessThan(((RectTransform)pickup.parent).rect.width), "Full-width Pickup retains panel side insets.");
            Assert.That(pickup.anchoredPosition.x, Is.Zero, "Fixed footer remains centered in its own panel.");
            var tabs = catalogue.GetComponentInChildren<DecorationModeTabsView>(true);
            foreach (var name in new[] { "furnitureButton", "floorButton", "wallButton", "wallDecorButton" })
            {
                var label = Field<Button>(tabs, name).transform.Find("Label").GetComponent<TMP_Text>();
                Assert.That(label.rectTransform.rect.width, Is.GreaterThanOrEqualTo(label.GetPreferredValues(label.text).x), name);
            }
        }

        [Test]
        public void EditingContext_HidesExplanationButRetainsReadableReturnLabel()
        {
            var catalogue = Copy("PF_UI_P8RCatalogue").GetComponent<DecorationCatalogueView>();
            catalogue.SetEditingContext("Current Preview: Pickup Point - Not yet applied\nMove this item onto a counter surface slot.", true);
            catalogue.ExplainEditingRestriction();
            var message = Field<TMP_Text>(catalogue, "editingContextLabel");
            Assert.That(message.gameObject.activeSelf, Is.False);
            var button = Field<Button>(catalogue, "returnToEditingButton");
            var label = button.transform.Find("Label").GetComponent<TMP_Text>();
            Assert.That(label.GetPreferredValues(label.text).x, Is.LessThanOrEqualTo(label.rectTransform.rect.width + .1f));
        }

        private static void AssertFrame(Sprite actual, string key)
        {
            // Exact state identity: an old skin or a different role must fail, not merely a folder rename.
            // 精确检查状态素材，仍能抓住旧皮肤残留或主次按钮角色混用。
            var expected = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "RefinedB/" + key + ".png");
            Assert.That(expected, Is.Not.Null, "Missing approved Refined B frame: " + key);
            Assert.That(actual, Is.SameAs(expected), "Wrong frame for semantic state: " + key);
        }

        private GameObject Copy(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/" + name + ".prefab");
            Assert.That(prefab, Is.Not.Null, "The approved P8R prefab has not been authored: " + name);
            var copy = UnityEngine.Object.Instantiate(prefab);
            owned.Add(copy);
            copy.SetActive(true);
            return copy;
        }

        private static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.EditorTools.Phase5;
using AnimalCafe.EditorTools.Phase6;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnimalCafe.Tests.EditMode.Phase6
{
    public sealed class Phase6DecorationUiPrefabTests
    {
        private const string LongerTileName = "Counter Module Plus";
        private const string LongerFeedback = "This space is already occupied.";
        private const string LongerModalBody =
            "This removes it from the current layout. You can place it again from the " +
            "catalogue. Keep it safe for your next layout.";

        private static readonly string[] ExpectedUiPaths =
        {
            Phase6DecorationAssetPaths.DecorationUiFontPath,
            Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
            Phase6DecorationAssetPaths.DecorationActionBarPrefabPath,
            Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath
        };

        private static readonly (string PrefabPath, string TextPath, string AccentPath)[]
            OnAccentCopyCases =
            {
                (Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
                    "ExpandedSheet/CollapseButton/Label", "ExpandedSheet/CollapseButton"),
                (Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
                    "ExpandedSheet/Content/TileTemplate/Name",
                    "ExpandedSheet/Content/TileTemplate"),
                (Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
                    "ExpandedSheet/Content/TileTemplate/Footprint",
                    "ExpandedSheet/Content/TileTemplate"),
                (Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
                    "ExpandedSheet/Content/TileTemplate/WarningLabel",
                    "ExpandedSheet/Content/TileTemplate"),
                (Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
                    "CollapsedHandle/Label", "CollapsedHandle"),
                (Phase6DecorationAssetPaths.DecorationActionBarPrefabPath,
                    "ActionPanel/StoreButton/Label", "ActionPanel/StoreButton"),
                (Phase6DecorationAssetPaths.DecorationActionBarPrefabPath,
                    "ActionPanel/RotateButton/Label", "ActionPanel/RotateButton"),
                (Phase6DecorationAssetPaths.DecorationActionBarPrefabPath,
                    "ActionPanel/CancelButton/Label", "ActionPanel/CancelButton"),
                (Phase6DecorationAssetPaths.DecorationActionBarPrefabPath,
                    "ActionPanel/ConfirmButton/Label", "ActionPanel/ConfirmButton"),
                (Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath,
                    "SafeArea/Content/CancelButton/Label", "SafeArea/Content/CancelButton"),
                (Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath,
                    "SafeArea/Content/StoreButton/Label", "SafeArea/Content/StoreButton")
            };

        [Test]
        public void AssetPaths_AreFixedAndIncludedInGeneratedContract()
        {
            Assert.That(Phase6DecorationAssetPaths.UiRootFolderPath,
                Is.EqualTo("Assets/UI/Phase6"));
            Assert.That(Phase6DecorationAssetPaths.UiPrefabFolderPath,
                Is.EqualTo("Assets/UI/Phase6/Prefabs"));
            Assert.That(Phase6DecorationAssetPaths.UiFontFolderPath,
                Is.EqualTo("Assets/UI/Phase6/Fonts"));
            Assert.That(Phase6DecorationAssetPaths.DecorationUiFontPath,
                Is.EqualTo("Assets/UI/Phase6/Fonts/NotoSansSC-Phase6 SDF.asset"));
            Assert.That(Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
                Is.EqualTo("Assets/UI/Phase6/Prefabs/PF_UI_DecorationCatalogue.prefab"));
            Assert.That(Phase6DecorationAssetPaths.DecorationActionBarPrefabPath,
                Is.EqualTo("Assets/UI/Phase6/Prefabs/PF_UI_DecorationActionBar.prefab"));
            Assert.That(Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath,
                Is.EqualTo("Assets/UI/Phase6/Prefabs/PF_UI_DecorationStoreModal.prefab"));
            Assert.That(Phase6DecorationAssetPaths.GeneratedAssetPaths,
                Does.Contain(Phase6DecorationAssetPaths.DecorationUiFontPath));
            Assert.That(Phase6DecorationAssetPaths.GeneratedAssetPaths,
                Does.Contain(Phase6DecorationAssetPaths.DecorationCataloguePrefabPath));
            Assert.That(Phase6DecorationAssetPaths.GeneratedAssetPaths,
                Does.Contain(Phase6DecorationAssetPaths.DecorationActionBarPrefabPath));
            Assert.That(Phase6DecorationAssetPaths.GeneratedAssetPaths,
                Does.Contain(Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath));
        }

        [Test]
        public void BuildAll_CreatesCompleteTask6UiSetAtFixedPaths()
        {
            foreach (var path in ExpectedUiPaths)
            {
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null, path);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.Not.Empty, path);
            }

            Assert.That(LoadPrefab<DecorationCatalogueView>(
                Phase6DecorationAssetPaths.DecorationCataloguePrefabPath), Is.Not.Null);
            Assert.That(LoadPrefab<DecorationActionBarView>(
                Phase6DecorationAssetPaths.DecorationActionBarPrefabPath), Is.Not.Null);
            Assert.That(LoadPrefab<DecorationStoreModalView>(
                Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath), Is.Not.Null);
        }

        [Test]
        public void CataloguePrefab_HasStablePoolTemplateAndNoOutsideOrFullscreenRaycastSurface()
        {
            var root = LoadPrefab<DecorationCatalogueView>(
                Phase6DecorationAssetPaths.DecorationCataloguePrefabPath).gameObject;

            Assert.That(root.transform.Find("ExpandedSheet"), Is.Not.Null);
            Assert.That(root.transform.Find("ExpandedSheet/Content"), Is.Not.Null);
            Assert.That(root.transform.Find("ExpandedSheet/Content/TileTemplate"), Is.Not.Null);
            Assert.That(root.transform.Find("ExpandedSheet/Content/TileTemplate")
                .GetComponent<DecorationCatalogueTileView>(), Is.Not.Null);
            Assert.That(root.transform.Find("CollapsedHandle"), Is.Not.Null);
            var sheet = root.transform.Find("ExpandedSheet");
            Assert.That(sheet.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(sheet.GetComponent<DecorationPointerBoundaryEventHook>(), Is.Not.Null);
            Assert.That(root.GetComponentsInChildren<DecorationCatalogueTileView>(true),
                Has.Length.EqualTo(1), "The nested template is the only authored tile.");
            Assert.That(root.GetComponentsInChildren<Transform>(true)
                .Any(item => item.name == "OutsideButton"), Is.False);

            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.raycastTarget)
                {
                    continue;
                }

                Assert.That(IsFullReferenceScreen(graphic.rectTransform), Is.False,
                    $"Catalogue raycast target '{graphic.name}' must be visibly bounded.");
                Assert.That(graphic.GetComponent<DecorationPointerBoundaryEventHook>(), Is.Not.Null,
                    graphic.name);
            }
        }

        [Test]
        public void ActionBarPrefab_HasOnlyVisibleRegionRaycastsAndExactActions()
        {
            var root = LoadPrefab<DecorationActionBarView>(
                Phase6DecorationAssetPaths.DecorationActionBarPrefabPath).gameObject;

            Assert.That(root.transform.Find("ActionPanel"), Is.Not.Null);
            Assert.That(root.transform.Find("ActionPanel/StoreButton"), Is.Not.Null);
            Assert.That(root.transform.Find("ActionPanel/RotateButton"), Is.Not.Null);
            Assert.That(root.transform.Find("ActionPanel/CancelButton"), Is.Not.Null);
            Assert.That(root.transform.Find("ActionPanel/ConfirmButton"), Is.Not.Null);
            Assert.That(root.transform.Find("FeedbackToast/StateShape"), Is.Not.Null);
            Assert.That(root.transform.Find("FeedbackToast/Message"), Is.Not.Null);
            var panel = root.transform.Find("ActionPanel");
            Assert.That(panel.GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(panel.GetComponent<Image>().color.a, Is.Zero);
            Assert.That(panel.GetComponent<HorizontalLayoutGroup>(), Is.Not.Null);
            Assert.That(root.transform.Find("FeedbackToast").GetComponent<CanvasGroup>()
                .blocksRaycasts, Is.False);

            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)
                .Where(item => item.raycastTarget))
            {
                Assert.That(IsFullReferenceScreen(graphic.rectTransform), Is.False, graphic.name);
                Assert.That(graphic.GetComponent<DecorationPointerBoundaryEventHook>(), Is.Not.Null,
                    graphic.name);
            }
        }

        [Test]
        public void ActionBarPrefab_IsCompactTranslucentSymbolGroupWithMinimumTargets()
        {
            var root = LoadPrefab<DecorationActionBarView>(
                Phase6DecorationAssetPaths.DecorationActionBarPrefabPath).gameObject;
            var panel = root.transform.Find("ActionPanel");
            var panelRect = panel.GetComponent<RectTransform>();
            Assert.That(panelRect.sizeDelta, Is.EqualTo(new Vector2(216f, 48f)));
            Assert.That(panel.GetComponent<Image>().color.a, Is.Zero);

            var expected = new[]
            {
                ("StoreButton", "□"),
                ("CancelButton", "×"),
                ("RotateButton", "R"),
                ("ConfirmButton", "✓")
            };
            foreach (var item in expected)
            {
                var button = panel.Find(item.Item1);
                Assert.That(button, Is.Not.Null, item.Item1);
                var rect = button.GetComponent<RectTransform>().rect;
                Assert.That(rect.width, Is.GreaterThanOrEqualTo(48f), item.Item1);
                Assert.That(rect.height, Is.GreaterThanOrEqualTo(48f), item.Item1);
                Assert.That(button.GetComponentInChildren<TMP_Text>(true).text,
                    Is.EqualTo(item.Item2), item.Item1);
            }
            Assert.That(panel.Cast<Transform>()
                .Where(child => child.GetComponent<Button>() != null)
                .Select(child => child.name).ToArray(), Is.EqualTo(new[]
                {
                    "StoreButton", "CancelButton", "RotateButton", "ConfirmButton"
                }));
        }

        [Test]
        public void ActionBarPrefab_PreservesGlyphsAndAuthorsExactEnglishSemanticTooltips()
        {
            var root = LoadPrefab<DecorationActionBarView>(
                Phase6DecorationAssetPaths.DecorationActionBarPrefabPath).gameObject;
            var expected = new[]
            {
                ("StoreButton", "□", "Store"),
                ("CancelButton", "×", "Cancel"),
                ("RotateButton", "R", "Rotate"),
                ("ConfirmButton", "✓", "Confirm")
            };

            foreach (var item in expected)
            {
                var button = root.transform.Find("ActionPanel/" + item.Item1);
                Assert.That(button, Is.Not.Null, item.Item1);
                Assert.That(button.Find("Label").GetComponent<TMP_Text>().text,
                    Is.EqualTo(item.Item2), item.Item1 + " glyph");
                var semantic = button.GetComponent<DecorationPointerBoundaryEventHook>();
                Assert.That(semantic, Is.Not.Null, item.Item1 + " semantic component");
                var serialized = new SerializedObject(semantic);
                Assert.That(serialized.FindProperty("semanticLabel").stringValue,
                    Is.EqualTo(item.Item3), item.Item1 + " semantic label");
                var tooltip = button.Find("Tooltip");
                var tooltipLabel = button.Find("Tooltip/Label")?.GetComponent<TMP_Text>();
                Assert.That(tooltip, Is.Not.Null, item.Item1 + " tooltip root");
                Assert.That(tooltipLabel, Is.Not.Null, item.Item1 + " tooltip label");
                Assert.That(tooltipLabel.text, Is.EqualTo(item.Item3), item.Item1 + " tooltip copy");
                Assert.That(tooltip.gameObject.activeSelf, Is.False,
                    item.Item1 + " tooltip starts hidden");
                Assert.That(tooltip.GetComponentsInChildren<Graphic>(true)
                    .All(graphic => !graphic.raycastTarget), Is.True,
                    item.Item1 + " tooltip cannot steal pointer ownership");
                Assert.That(serialized.FindProperty("tooltipRoot").objectReferenceValue,
                    Is.SameAs(tooltip.gameObject));
                Assert.That(serialized.FindProperty("tooltipLabel").objectReferenceValue,
                    Is.SameAs(tooltipLabel));
            }
        }

        [Test]
        public void StoreModalPrefab_HasDeliberateBlockerAndNonOutsideDismissibleStructure()
        {
            var view = LoadPrefab<DecorationStoreModalView>(
                Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath);
            var root = view.gameObject;
            var blocker = root.transform.Find("ModalBlocker");

            Assert.That(root.GetComponent<AnimalCafeModalView>(), Is.Not.Null);
            Assert.That(root.GetComponent<CanvasGroup>(), Is.Not.Null);
            Assert.That(blocker, Is.Not.Null);
            Assert.That(blocker.GetComponent<Button>(), Is.Not.Null);
            Assert.That(blocker.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(blocker.GetComponent<DecorationPointerBoundaryEventHook>(), Is.Not.Null);
            AssertFillsParent(blocker.GetComponent<RectTransform>(), "ModalBlocker");
            Assert.That(root.transform.Find("SafeArea/Content/Title"), Is.Not.Null);
            Assert.That(root.transform.Find("SafeArea/Content/Body"), Is.Not.Null);
            Assert.That(root.transform.Find("SafeArea/Content/CancelButton"), Is.Not.Null);
            Assert.That(root.transform.Find("SafeArea/Content/StoreButton"), Is.Not.Null);
            Assert.That(root.transform.Find("OutsideButton"), Is.Null);
        }

        [Test]
        public void UiPrefabs_AreCanvaslessEventSystemlessAndDoNotOwnStrongFrost()
        {
            foreach (var path in ExpectedUiPaths.Skip(1))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(root.GetComponentsInChildren<Canvas>(true), Is.Empty, path);
                Assert.That(root.GetComponentsInChildren<GraphicRaycaster>(true), Is.Empty, path);
                Assert.That(root.GetComponentsInChildren<EventSystem>(true), Is.Empty, path);
                Assert.That(root.GetComponentsInChildren<AnimalCafeBottomSheetView>(true),
                    Is.Empty, path);
                var panels = root.GetComponentsInChildren<AnimalCafePanelView>(true);
                if (path == Phase6DecorationAssetPaths.DecorationActionBarPrefabPath)
                    Assert.That(panels, Is.Empty, path + " must keep its accepted panel-free layout.");
                else
                    Assert.That(panels, Is.Not.Empty, path);
                Assert.That(panels.Any(panel => panel.ResolvedStyle == UiPanelStyle.StrongFrost),
                    Is.False, path);
            }

            AssertPanelStyle(Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
                "ExpandedSheet", UiPanelStyle.LightFrost);
            AssertPanelStyle(Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath,
                "SafeArea/Content", UiPanelStyle.Solid);
        }

        [Test]
        public void UiPrefabs_EveryTask6TextOwnsTask6StaticFontMaterialAndAtlas()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                Phase6DecorationAssetPaths.DecorationUiFontPath);
            var allSubassets = AssetDatabase.LoadAllAssetsAtPath(
                Phase6DecorationAssetPaths.DecorationUiFontPath);
            var localMaterials = allSubassets.OfType<Material>().ToArray();
            var localAtlases = allSubassets.OfType<Texture2D>().ToArray();

            Assert.That(font, Is.Not.Null);
            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(localMaterials, Has.Length.EqualTo(1));
            Assert.That(localAtlases, Has.Length.EqualTo(1));
            AssertSameAssetIdentity(font.material, localMaterials[0]);
            AssertSameAssetIdentity(font.atlasTextures.Single(), localAtlases[0]);
            AssertSameAssetIdentity(localMaterials[0].mainTexture, localAtlases[0]);

            foreach (var path in ExpectedUiPaths.Skip(1))
            foreach (var text in AssetDatabase.LoadAssetAtPath<GameObject>(path)
                .GetComponentsInChildren<TMP_Text>(true))
            {
                AssertSameAssetIdentity(text.font, font, $"{path}/{text.name}");
                AssertSameAssetIdentity(text.fontSharedMaterial, font.material,
                    $"{path}/{text.name}");
                Assert.That(text.enableAutoSizing, Is.False, $"{path}/{text.name}");
            }
        }

        [Test]
        public void UiFont_CoversCanonicalCopyFootprintMultiplicationAndCollapseMinus()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                Phase6DecorationAssetPaths.DecorationUiFontPath);
            var canonical = string.Join("", new[]
            {
                "Furniture Catalogue Catalogue Store Rotate Cancel Confirm",
                "Store furniture? This removes it from the current layout. " +
                    "You can place it again from the catalogue.",
                "这里已有家具超出可装修区域这个区域尚未解锁这里不能放置家具" +
                    "入口区域不能放置家具此处不支持落地家具家具状态已变化，请重新选择",
                "Counter Module Counter 1 x 2 Counter 1 x 3 Counter 2 x 3",
                "1 × 1 1 × 2 1 × 3 2 × 3 Unavailable Missing definition " +
                    "Missing prefab Missing thumbnail −"
            });
            Assert.That(Phase5UiFontCoverage.FindMissingUnicodeScalars(font, canonical), Is.Empty);
            Assert.That(font.HasCharacter('−'), Is.True,
                "The static Phase 6 font must own U+2212 used by the Collapse label.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase6DecorationAssetPaths.DecorationCataloguePrefabPath);
            var parent = new GameObject(
                "CollapseGlyphFixture",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            parent.GetComponent<RectTransform>().sizeDelta = new Vector2(720f, 1280f);
            parent.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var instance = UnityEngine.Object.Instantiate(prefab, parent.transform);
            try
            {
                instance.SetActive(true);
                var label = instance.transform.Find("ExpandedSheet/CollapseButton/Label")
                    .GetComponent<TMP_Text>();
                Assert.That(label.text, Is.EqualTo("−"));
                for (var current = label.transform;
                     current != instance.transform;
                     current = current.parent)
                {
                    current.gameObject.SetActive(true);
                }

                Canvas.ForceUpdateCanvases();
                label.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
                Assert.That(label.textInfo.characterCount, Is.EqualTo(1));
                var character = label.textInfo.characterInfo[0];
                Assert.That(character.character, Is.EqualTo('−'));
                Assert.That(character.isVisible, Is.True,
                    "U+2212 must produce visible TMP geometry, not a missing-glyph placeholder.");
                Assert.That(character.topRight.x - character.bottomLeft.x, Is.GreaterThan(0f));
                Assert.That(character.topRight.y - character.bottomLeft.y, Is.GreaterThan(0f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void UiPrefabs_AllInteractiveTargetsMeet48By48AndOwnPointerHooks()
        {
            foreach (var path in ExpectedUiPaths.Skip(1))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var parent = new GameObject("TouchFixture", typeof(RectTransform));
                parent.GetComponent<RectTransform>().sizeDelta = new Vector2(1080f, 1920f);
                var root = UnityEngine.Object.Instantiate(prefab, parent.transform);
                root.SetActive(true);
                Canvas.ForceUpdateCanvases();
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    var size = button.GetComponent<RectTransform>().rect.size;
                    Assert.That(size.x, Is.GreaterThanOrEqualTo(48f), $"{path}/{button.name}");
                    Assert.That(size.y, Is.GreaterThanOrEqualTo(48f), $"{path}/{button.name}");
                    Assert.That(button.GetComponent<DecorationPointerBoundaryEventHook>(), Is.Not.Null,
                        $"{path}/{button.name}");
                }
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void UiPrefabs_AllButtonsHaveCanonicalEmptyPersistentCallbacks()
        {
            foreach (var path in ExpectedUiPaths.Skip(1))
            foreach (var button in AssetDatabase.LoadAssetAtPath<GameObject>(path)
                .GetComponentsInChildren<Button>(true))
            {
                Assert.That(button.onClick.GetPersistentEventCount(), Is.Zero,
                    $"{path}/{button.name}");
            }
        }

        [Test]
        public void UiPrefabs_CopyWrappingAndNonColorStateCuesArePresent()
        {
            var catalogue = LoadPrefab<DecorationCatalogueView>(
                Phase6DecorationAssetPaths.DecorationCataloguePrefabPath).gameObject;
            var action = LoadPrefab<DecorationActionBarView>(
                Phase6DecorationAssetPaths.DecorationActionBarPrefabPath).gameObject;
            var modal = LoadPrefab<DecorationStoreModalView>(
                Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath).gameObject;

            AssertText(catalogue, "ExpandedSheet/Title", "Furniture Catalogue", 16f);
            AssertText(catalogue, "CollapsedHandle/Label", "Catalogue", 14f);
            AssertText(action, "ActionPanel/StoreButton/Label", "□", 14f);
            AssertText(action, "ActionPanel/RotateButton/Label", "R", 14f);
            AssertText(action, "ActionPanel/CancelButton/Label", "×", 14f);
            AssertText(action, "ActionPanel/ConfirmButton/Label", "✓", 14f);
            AssertText(modal, "SafeArea/Content/Title", "Store furniture?", 16f);
            AssertText(modal, "SafeArea/Content/Body",
                "This removes it from the current layout. You can place it again from the catalogue.",
                16f);

            Assert.That(action.transform.Find("FeedbackToast/StateShape")
                .GetComponent<Graphic>(), Is.Not.Null);
            Assert.That(catalogue.transform.Find(
                "ExpandedSheet/Content/TileTemplate/WarningShape"), Is.Not.Null);
            Assert.That(catalogue.transform.Find(
                "ExpandedSheet/Content/TileTemplate/WarningLabel"), Is.Not.Null);
        }

        [Test]
        public void UiPrefabs_UsePhase5ThemeColorsMaterialsTypographyAndMinimumTouchTokens()
        {
            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(
                Phase5UiAssetPaths.ThemePath);
            Assert.That(theme, Is.Not.Null);
            Assert.That(theme.Sizes.MinimumTouchTargetWidth,
                Is.GreaterThanOrEqualTo(AnimalCafeUiTheme.MinimumTouchTargetSize));
            Assert.That(theme.Sizes.MinimumTouchTargetHeight,
                Is.GreaterThanOrEqualTo(AnimalCafeUiTheme.MinimumTouchTargetSize));

            AssertPanelTheme(Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
                "ExpandedSheet", theme, UiPanelStyle.LightFrost);
            AssertPanelTheme(Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath,
                "SafeArea/Content", theme, UiPanelStyle.Solid);

            var catalogue = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase6DecorationAssetPaths.DecorationCataloguePrefabPath);
            var action = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase6DecorationAssetPaths.DecorationActionBarPrefabPath);
            var modal = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath);
            AssertTextToken(catalogue, "ExpandedSheet/Title", theme.Typography.Heading);
            AssertTextToken(catalogue,
                "ExpandedSheet/Content/TileTemplate/Name", theme.Typography.Body);
            AssertTextToken(catalogue, "CollapsedHandle/Label", theme.Typography.Label);
            AssertTextToken(action,
                "FeedbackToast/Message", theme.Typography.Body);
            AssertTextToken(action,
                "ActionPanel/ConfirmButton/Label", theme.Typography.Label);
            AssertTextToken(modal, "SafeArea/Content/Title", theme.Typography.Heading);
            AssertTextToken(modal, "SafeArea/Content/Body", theme.Typography.Body);
            AssertTextToken(modal,
                "SafeArea/Content/StoreButton/Label", theme.Typography.Label);

            Assert.That(catalogue.transform.Find("ExpandedSheet/Title")
                .GetComponent<TMP_Text>().color, Is.EqualTo(theme.Colors.Text));
            Assert.That(action.transform.Find("FeedbackToast/Message")
                .GetComponent<TMP_Text>().color, Is.EqualTo(theme.Colors.Text));
            Assert.That(modal.transform.Find("SafeArea/Content/Title")
                .GetComponent<TMP_Text>().color, Is.EqualTo(theme.Colors.Text));
            Assert.That(modal.transform.Find("SafeArea/Content/Body")
                .GetComponent<TMP_Text>().color, Is.EqualTo(theme.Colors.Text));

            Assert.That(catalogue.transform.Find(
                    "ExpandedSheet/Content/TileTemplate/WarningShape")
                .GetComponent<Image>().color, Is.EqualTo(theme.Colors.Warning));
            Assert.That(action.transform.Find("FeedbackToast/StateShape")
                .GetComponent<Image>().color, Is.EqualTo(theme.Colors.Warning));

            foreach (var path in ExpectedUiPaths.Skip(1))
            foreach (var button in AssetDatabase.LoadAssetAtPath<GameObject>(path)
                .GetComponentsInChildren<Button>(true))
            {
                if (button.name != "ModalBlocker")
                {
                    Assert.That(button.image.color, Is.EqualTo(theme.Colors.Accent),
                        $"{path}/{button.name}");
                }
            }
        }

        [Test]
        public void UiPrefabs_OnAccentCopyUsesSurfaceRoleAtMinimumContrast()
        {
            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(
                Phase5UiAssetPaths.ThemePath);
            Assert.That(theme, Is.Not.Null);

            var failures = new List<string>();
            foreach (var item in OnAccentCopyCases)
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(item.PrefabPath);
                Assert.That(root, Is.Not.Null, item.PrefabPath);

                var text = root.transform.Find(item.TextPath)?.GetComponent<TMP_Text>();
                var accent = root.transform.Find(item.AccentPath)?.GetComponent<Image>();
                Assert.That(text, Is.Not.Null, $"{item.PrefabPath}/{item.TextPath}");
                Assert.That(accent, Is.Not.Null, $"{item.PrefabPath}/{item.AccentPath}");
                if (text == null || accent == null)
                {
                    continue;
                }

                var label = $"{item.PrefabPath}/{item.TextPath}";
                if (accent.color != theme.Colors.Accent)
                {
                    failures.Add($"{item.AccentPath}: expected Accent background role");
                }

                if (text.color != theme.Colors.Surface)
                {
                    failures.Add($"{label}: expected Surface foreground role");
                }

                var ratio = ContrastRatio(text.color, accent.color);
                if (ratio < 4.5d)
                {
                    failures.Add($"{label}: contrast {ratio:F3}:1 is below 4.5:1");
                }
            }

            Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures));
        }

        [Test]
        public void UiPrefabs_LongerLocalizedCopyWrapsWithoutAutosizeBelowThemeBaseline()
        {
            var cases = new[]
            {
                (Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
                    "ExpandedSheet/Content/TileTemplate/Name",
                    "Counter Module", LongerTileName),
                (Phase6DecorationAssetPaths.DecorationActionBarPrefabPath,
                    "FeedbackToast/Message",
                    "Space already occupied", LongerFeedback),
                (Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath,
                    "SafeArea/Content/Body",
                    "This removes it from the current layout. You can place it again from the catalogue.",
                    LongerModalBody)
            };
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                Phase6DecorationAssetPaths.DecorationUiFontPath);
            Assert.That(Phase5UiFontCoverage.FindMissingUnicodeScalars(
                font,
                string.Concat(cases.Select(item => item.Item4))), Is.Empty,
                "The Task 6-local static atlas must own every exercised longer-copy glyph.");

            foreach (var item in cases)
            {
                var increase = (item.Item4.Length - item.Item3.Length) / (float)item.Item3.Length;
                Assert.That(increase, Is.InRange(0.30f, 0.50f), item.Item2);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(item.Item1);
                var parent = new GameObject("LongCopyParent", typeof(RectTransform));
                parent.GetComponent<RectTransform>().sizeDelta = new Vector2(720f, 1280f);
                var instance = UnityEngine.Object.Instantiate(prefab, parent.transform);
                try
                {
                    foreach (var safeArea in instance.GetComponentsInChildren<SafeAreaContainer>(true))
                    {
                        safeArea.AutoApplyRuntimeSafeArea = false;
                        safeArea.ApplySafeArea(new Rect(0f, 0f, 720f, 1280f),
                            new Vector2(720f, 1280f));
                    }

                    instance.SetActive(true);
                    var text = instance.transform.Find(item.Item2).GetComponent<TMP_Text>();
                    for (var current = text.transform;
                         current != instance.transform;
                         current = current.parent)
                    {
                        current.gameObject.SetActive(true);
                    }
                    text.text = item.Item4;
                    Canvas.ForceUpdateCanvases();
                    text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
                    Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
                    Assert.That(text.enableAutoSizing, Is.False);
                    Assert.That(text.fontSize,
                        Is.GreaterThanOrEqualTo(AnimalCafeUiTheme.MinimumLabelFontSize));
                    var preferred = text.GetPreferredValues(
                        text.text,
                        text.rectTransform.rect.width,
                        0f);
                    Assert.That(preferred.y,
                        Is.LessThanOrEqualTo(text.rectTransform.rect.height + 0.5f),
                        item.Item2 + " must fit its authored height without clipping.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(parent);
                }
            }
        }

        [TestCase(1080f, 1920f, 24f, 96f, 1032f, 1740f)]
        [TestCase(720f, 1280f, 0f, 0f, 720f, 1280f)]
        [TestCase(1080f, 2400f, 0f, 0f, 1080f, 2400f)]
        [TestCase(2400f, 1080f, 96f, 48f, 2208f, 984f)]
        public void CatalogueCollapsedHandle_IsBottomCenteredAndKeepsAtLeast48InsideSafeArea(
            float width,
            float height,
            float safeX,
            float safeY,
            float safeWidth,
            float safeHeight)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase6DecorationAssetPaths.DecorationCataloguePrefabPath);
            var parent = new GameObject("CollapsedHandleParent", typeof(RectTransform));
            parent.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            var instance = UnityEngine.Object.Instantiate(prefab, parent.transform);
            try
            {
                var safeArea = instance.GetComponent<SafeAreaContainer>();
                safeArea.AutoApplyRuntimeSafeArea = false;
                safeArea.ApplySafeArea(new Rect(safeX, safeY, safeWidth, safeHeight),
                    new Vector2(width, height));
                instance.SetActive(true);
                Canvas.ForceUpdateCanvases();
                var safeRect = WorldRect(instance.GetComponent<RectTransform>());
                instance.transform.Find("ExpandedSheet").gameObject.SetActive(false);
                var handle = instance.transform.Find("CollapsedHandle").gameObject;
                handle.SetActive(true);
                Assert.That(handle.GetComponent<RectTransform>().anchoredPosition.y,
                    Is.EqualTo(252f).Within(0.01f),
                    "The handle offsets the Catalogue root's -220 collapsed slide.");
                instance.GetComponent<RectTransform>().anchoredPosition =
                    new Vector2(0f, -220f);
                Canvas.ForceUpdateCanvases();

                var handleRect = WorldRect(handle.GetComponent<RectTransform>());
                Assert.That(handleRect.width,
                    Is.GreaterThanOrEqualTo(AnimalCafeUiTheme.MinimumTouchTargetSize));
                Assert.That(handleRect.height,
                    Is.GreaterThanOrEqualTo(AnimalCafeUiTheme.MinimumTouchTargetSize));
                Assert.That(handleRect.center.x, Is.EqualTo(safeRect.center.x).Within(0.5f));
                Assert.That(handleRect.yMin, Is.EqualTo(safeRect.yMin).Within(0.5f),
                    "The collapsed handle must align to the Safe Area bottom edge.");
                Assert.That(handleRect.yMax, Is.LessThanOrEqualTo(safeRect.yMax + 0.5f));
                Assert.That(handleRect.xMin, Is.GreaterThanOrEqualTo(safeRect.xMin - 0.5f));
                Assert.That(handleRect.xMax, Is.LessThanOrEqualTo(safeRect.xMax + 0.5f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [TestCase(1080f, 1920f, 24f, 96f, 1032f, 1740f)]
        [TestCase(720f, 1280f, 0f, 0f, 720f, 1280f)]
        [TestCase(1080f, 2400f, 0f, 0f, 1080f, 2400f)]
        [TestCase(2400f, 1080f, 96f, 48f, 2208f, 984f)]
        public void UiPrefabs_ResponsiveFixtureKeepsEssentialNormalizedBoundsInsideSafeArea(
            float width,
            float height,
            float safeX,
            float safeY,
            float safeWidth,
            float safeHeight)
        {
            var normalized = SafeAreaContainer.CalculateNormalizedSafeRect(
                new Rect(safeX, safeY, safeWidth, safeHeight),
                new Vector2(width, height));

            Assert.That(normalized.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(normalized.yMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(normalized.xMax, Is.LessThanOrEqualTo(1f));
            Assert.That(normalized.yMax, Is.LessThanOrEqualTo(1f));
            var responsiveFailures = new List<string>();
            foreach (var path in ExpectedUiPaths.Skip(1))
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var parent = new GameObject("ResponsiveParent", typeof(RectTransform));
                var instance = UnityEngine.Object.Instantiate(root, parent.transform);
                try
                {
                    var parentRect = parent.GetComponent<RectTransform>();
                    parentRect.sizeDelta = new Vector2(width, height);
                    var rootRect = instance.GetComponent<RectTransform>();
                    RecordAssertion(responsiveFailures, path + " root anchors", () =>
                    {
                        Assert.That(rootRect.anchorMin, Is.EqualTo(Vector2.zero), path);
                        Assert.That(rootRect.anchorMax, Is.EqualTo(Vector2.one), path);
                    });
                    foreach (var safeArea in instance.GetComponentsInChildren<SafeAreaContainer>(true))
                    {
                        safeArea.AutoApplyRuntimeSafeArea = false;
                        safeArea.ApplySafeArea(
                            new Rect(safeX, safeY, safeWidth, safeHeight),
                            new Vector2(width, height));
                    }

                    instance.SetActive(true);
                    Canvas.ForceUpdateCanvases();
                    foreach (var button in instance.GetComponentsInChildren<Button>(true))
                    {
                        RecordAssertion(responsiveFailures, path + "/" + button.name, () =>
                        {
                            var size = button.GetComponent<RectTransform>().rect.size;
                            Assert.That(size.x, Is.GreaterThanOrEqualTo(48f), $"{path}/{button.name}");
                            Assert.That(size.y, Is.GreaterThanOrEqualTo(48f), $"{path}/{button.name}");
                            if (button.name == "ModalBlocker")
                            {
                                AssertSameWorldRect(
                                    button.GetComponent<RectTransform>(), rootRect, path);
                            }
                            else
                            {
                                AssertRectInsideSafeArea(
                                    button.GetComponent<RectTransform>(), parentRect,
                                    new Rect(safeX, safeY, safeWidth, safeHeight), path);
                            }
                        });
                    }

                    foreach (var essential in GetEssentialNonButtonPaths(path))
                    {
                        RecordAssertion(responsiveFailures, path + "/" + essential, () =>
                        {
                            var target = instance.transform.Find(essential);
                            Assert.That(target, Is.Not.Null, $"{path}/{essential}");
                            AssertRectInsideSafeArea(
                                target.GetComponent<RectTransform>(), parentRect,
                                new Rect(safeX, safeY, safeWidth, safeHeight), path);
                        });
                    }

                    RecordAssertion(responsiveFailures, path + " root-sized raycast scope", () =>
                    {
                        var rootCoveringRaycasts = instance
                            .GetComponentsInChildren<Graphic>(true)
                            .Where(graphic => graphic.raycastTarget
                                && CoversWorldRect(graphic.rectTransform, rootRect))
                            .ToArray();
                        if (path == Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath)
                        {
                            Assert.That(rootCoveringRaycasts.Length, Is.EqualTo(1), path);
                            Assert.That(rootCoveringRaycasts[0].name, Is.EqualTo("ModalBlocker"), path);
                        }
                        else
                        {
                            Assert.That(rootCoveringRaycasts, Is.Empty, path);
                        }
                    });

                    if (path == Phase6DecorationAssetPaths.DecorationCataloguePrefabPath)
                    {
                        RecordAssertion(responsiveFailures, path + " tile name/footprint separation", () =>
                        {
                            var name = instance.transform.Find(
                                "ExpandedSheet/Content/TileTemplate/Name")
                                .GetComponent<RectTransform>();
                            var footprint = instance.transform.Find(
                                "ExpandedSheet/Content/TileTemplate/Footprint")
                                .GetComponent<RectTransform>();
                            Assert.That(WorldRect(name).xMax,
                                Is.LessThanOrEqualTo(WorldRect(footprint).xMin + 0.5f), path);
                        });
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(parent);
                }

                foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    RecordAssertion(responsiveFailures, path + "/" + text.name, () =>
                    {
                        Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal),
                            $"{path}/{text.name}");
                    });
                }
            }

            Assert.That(responsiveFailures, Is.Empty,
                string.Join("\n", responsiveFailures));
        }

        private static T LoadPrefab<T>(string path) where T : Component
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(root, Is.Not.Null, path);
            return root.GetComponent<T>();
        }

        private static bool IsFullReferenceScreen(RectTransform rect)
        {
            return rect != null && rect.rect.width >= 1079f && rect.rect.height >= 1919f;
        }

        private static void AssertFillsParent(RectTransform rect, string label)
        {
            Assert.That(rect, Is.Not.Null, label);
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero), label);
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one), label);
            Assert.That(rect.offsetMin, Is.EqualTo(Vector2.zero), label);
            Assert.That(rect.offsetMax, Is.EqualTo(Vector2.zero), label);
        }

        private static void AssertText(
            GameObject root,
            string path,
            string expected,
            float minimumSize)
        {
            var transform = root.transform.Find(path);
            Assert.That(transform, Is.Not.Null, path);
            var text = transform.GetComponent<TMP_Text>();
            Assert.That(text, Is.Not.Null, path);
            Assert.That(text.text, Is.EqualTo(expected), path);
            Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(minimumSize), path);
            Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal), path);
        }

        private static void AssertSameAssetIdentity(
            UnityEngine.Object actual,
            UnityEngine.Object expected,
            string label = null)
        {
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                actual, out string actualGuid, out long actualId), Is.True, label);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                expected, out string expectedGuid, out long expectedId), Is.True, label);
            Assert.That((actualGuid, actualId), Is.EqualTo((expectedGuid, expectedId)), label);
        }

        private static void AssertPanelStyle(string prefabPath, string childPath, UiPanelStyle style)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.SetActive(true);
                var panel = instance.transform.Find(childPath).GetComponent<AnimalCafePanelView>();
                Assert.That(panel.ResolvedStyle, Is.EqualTo(style), prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void AssertPanelTheme(
            string prefabPath,
            string childPath,
            AnimalCafeUiTheme theme,
            UiPanelStyle style,
            float expectedAlpha = 1f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                instance.SetActive(true);
                var target = instance.transform.Find(childPath);
                var panel = target.GetComponent<AnimalCafePanelView>();
                Assert.That(panel.ResolvedStyle, Is.EqualTo(style));
                AssertSameAssetIdentity(target.GetComponent<Image>().material,
                    style == UiPanelStyle.LightFrost
                        ? theme.Materials.LightFrost
                        : theme.Materials.Solid,
                    prefabPath);
                var expectedColor = theme.Colors.Surface;
                expectedColor.a = expectedAlpha;
                Assert.That(target.GetComponent<Image>().color, Is.EqualTo(expectedColor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void AssertTextToken(
            GameObject root,
            string path,
            UiTextStyleToken token)
        {
            var text = root.transform.Find(path).GetComponent<TMP_Text>();
            Assert.That(text.fontSize, Is.EqualTo(token.FontSize), path);
            Assert.That(text.fontStyle, Is.EqualTo(token.FontStyle), path);
            Assert.That(text.lineSpacing, Is.EqualTo(token.LineSpacing), path);
        }

        private static double ContrastRatio(Color foreground, Color background)
        {
            var foregroundLuminance = RelativeLuminance(foreground);
            var backgroundLuminance = RelativeLuminance(background);
            var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
            var darker = Math.Min(foregroundLuminance, backgroundLuminance);
            return (lighter + 0.05d) / (darker + 0.05d);
        }

        private static double RelativeLuminance(Color color)
        {
            return 0.2126d * LinearizeSrgb(color.r)
                + 0.7152d * LinearizeSrgb(color.g)
                + 0.0722d * LinearizeSrgb(color.b);
        }

        private static double LinearizeSrgb(float channel)
        {
            return channel <= 0.04045f
                ? channel / 12.92d
                : Math.Pow((channel + 0.055d) / 1.055d, 2.4d);
        }

        private static void AssertRectInsideSafeArea(
            RectTransform target,
            RectTransform parent,
            Rect safeArea,
            string label)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var local = parent.InverseTransformPoint(corner);
                var pixel = new Vector2(
                    local.x + parent.rect.width * parent.pivot.x,
                    local.y + parent.rect.height * parent.pivot.y);
                Assert.That(pixel.x, Is.InRange(safeArea.xMin - 0.5f, safeArea.xMax + 0.5f),
                    $"{label}/{target.name} x");
                Assert.That(pixel.y, Is.InRange(safeArea.yMin - 0.5f, safeArea.yMax + 0.5f),
                    $"{label}/{target.name} y");
            }
        }

        private static IEnumerable<string> GetEssentialNonButtonPaths(string prefabPath)
        {
            if (prefabPath == Phase6DecorationAssetPaths.DecorationCataloguePrefabPath)
            {
                return new[]
                {
                    "ExpandedSheet/Title",
                    "ExpandedSheet/Content/TileTemplate/Thumbnail",
                    "ExpandedSheet/Content/TileTemplate/Name",
                    "ExpandedSheet/Content/TileTemplate/Footprint",
                    "ExpandedSheet/Content/TileTemplate/WarningShape",
                    "ExpandedSheet/Content/TileTemplate/WarningLabel"
                };
            }

            if (prefabPath == Phase6DecorationAssetPaths.DecorationActionBarPrefabPath)
            {
                return new[]
                {
                    "FeedbackToast",
                    "FeedbackToast/StateShape",
                    "FeedbackToast/Message"
                };
            }

            return new[] { "SafeArea/Content/Title", "SafeArea/Content/Body" };
        }

        private static void AssertSameWorldRect(
            RectTransform actual,
            RectTransform expected,
            string label)
        {
            var actualCorners = new Vector3[4];
            var expectedCorners = new Vector3[4];
            actual.GetWorldCorners(actualCorners);
            expected.GetWorldCorners(expectedCorners);
            for (var index = 0; index < 4; index++)
            {
                Assert.That(actualCorners[index].x,
                    Is.EqualTo(expectedCorners[index].x).Within(0.5f), label);
                Assert.That(actualCorners[index].y,
                    Is.EqualTo(expectedCorners[index].y).Within(0.5f), label);
            }
        }

        private static bool CoversWorldRect(RectTransform candidate, RectTransform expected)
        {
            var candidateRect = WorldRect(candidate);
            var expectedRect = WorldRect(expected);
            return candidateRect.xMin <= expectedRect.xMin + 0.5f
                && candidateRect.xMax >= expectedRect.xMax - 0.5f
                && candidateRect.yMin <= expectedRect.yMin + 0.5f
                && candidateRect.yMax >= expectedRect.yMax - 0.5f;
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static void RecordAssertion(
            ICollection<string> failures,
            string label,
            Action assertion)
        {
            try
            {
                assertion();
            }
            catch (AssertionException exception)
            {
                failures.Add(label + ": " + exception.Message);
            }
        }
    }
}

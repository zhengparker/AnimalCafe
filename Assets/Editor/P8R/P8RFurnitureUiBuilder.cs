using System;
using System.IO;
using System.Linq;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.Phase6;
using AnimalCafe.EditorTools.Phase8;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.P8R;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.P8R
{
    public static class P8RFurnitureUiPaths
    {
        public const string Root = "Assets/UI/P8R";
        public const string Appearance = Root + "/P8RAppearance.asset";
        public const string Catalogue = Root + "/DC_P8RFurniture.asset";
        public const string CataloguePrefab = Root + "/Prefabs/PF_UI_P8RCatalogue.prefab";
        public const string ActionPrefab = Root + "/Prefabs/PF_UI_P8RActionBar.prefab";
        public const string StorePrefab = Root + "/Prefabs/PF_UI_P8RPutAwayModal.prefab";
    }

    /// <summary>Author only approved copies and their MainCafe references.
    /// 仅通过 Editor API 创建获批副本并连接 MainCafe，不运行旧完整 BuildAssets。</summary>
    public static class P8RFurnitureUiBuilder
    {
        [MenuItem("Tools/AnimalCafe/P8R/Build Furniture UI and Wire MainCafe")]
        public static void BuildApprovedFurnitureUi()
        {
            RequireCleanLoadedAssets();
            EnsureFolder(P8RFurnitureUiPaths.Root + "/Prefabs");
            var appearance = BuildAppearance();
            BuildDisplayCatalogue();
            BuildCopy(Phase8AssetPaths.CataloguePrefabPath, P8RFurnitureUiPaths.CataloguePrefab,
                root => StyleCatalogue(root, appearance));
            BuildCopy(Phase8AssetPaths.ActionBarPrefabPath, P8RFurnitureUiPaths.ActionPrefab,
                root => StyleActionBar(root, appearance));
            BuildCopy(Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath, P8RFurnitureUiPaths.StorePrefab,
                root => StyleModal(root, appearance));
            WireMainCafe();
            Debug.Log("P8R Furniture UI authored. Original P6/P8 assets retained.");
        }

        // Targeted repair for this first integration: the source prefabs are intentionally
        // inactive assets, while MainCafe's scene instances are active CanvasGroup owners.
        [MenuItem("Tools/AnimalCafe/P8R/Repair Furniture UI Instance Activation")]
        public static void RepairFurnitureUiInstanceActivation()
        {
            RequireCleanLoadedAssets();
            var active = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(Phase8AssetPaths.MainCafeScenePath);
            var wasPresent = scene.IsValid();
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(Phase8AssetPaths.MainCafeScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save or revert MainCafe before P8R activation repair.");
                var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true)).Single();
                if (!HasP8RWiring(controller)) throw new InvalidOperationException("Activation repair requires complete approved P8R wiring.");
                foreach (var name in new[] { "catalogueView", "actionBarView", "storeModalView" })
                    Reference<Component>(controller, name).gameObject.SetActive(true);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save P8R activation repair.");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, !wasPresent);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }

        [MenuItem("Tools/AnimalCafe/P8R/Refresh Approved Furniture Presentation")]
        public static void RefreshApprovedFurniturePresentation()
        {
            RequireCleanLoadedAssets();
            if (P8RCompleteUiBuilder.GuardLegacyMainCafe())
                throw new InvalidOperationException("Use Complete All Existing UI for the approved P8R presentation; furniture-only refresh would downgrade surface details.");
            var appearance = Require<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            RefreshCopy(P8RFurnitureUiPaths.CataloguePrefab, root => StyleCatalogue(root, appearance));
            RefreshCopy(P8RFurnitureUiPaths.ActionPrefab, root => StyleActionBar(root, appearance));
            RefreshCopy(P8RFurnitureUiPaths.StorePrefab, root => StyleModal(root, appearance));
        }

        internal static void RefreshCopy(string path, Action<GameObject> style)
        {
            Require<GameObject>(path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try { style(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static P8RAppearance BuildAppearance()
        {
            var existing = AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            if (existing != null) return existing;
            var appearance = ScriptableObject.CreateInstance<P8RAppearance>();
            var so = new SerializedObject(appearance);
            // B replaces the original frame keys; it never adds a second entry for a key.
            // 先收集原素材的唯一 key；B 底板完整时再替换引用，避免新旧皮肤混搭。
            var paths = AssetDatabase.FindAssets("t:Sprite", new[] { P8RFurnitureUiPaths.Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.StartsWith(P8RRefinedBAssets.Root + "/", StringComparison.Ordinal))
                .Where(p => !p.StartsWith(P8RColoredTabAssets.Root + "/", StringComparison.Ordinal))
                .Where(p => !p.StartsWith(P8RColoredActionAssets.Root + "/", StringComparison.Ordinal))
                .Where(p => !p.StartsWith(P8RColoredRangeAssets.Root + "/", StringComparison.Ordinal))
                .OrderBy(p => p, StringComparer.Ordinal).ToArray();
            var refinedB = P8RRefinedBAssets.Keys.All(key =>
                AssetDatabase.LoadAssetAtPath<Sprite>(P8RRefinedBAssets.Root + "/" + key + ".png") != null);
            var strongerGlyphs = refinedB && P8RRefinedBGlyphs.Keys.All(key =>
                AssetDatabase.LoadAssetAtPath<Sprite>(P8RRefinedBGlyphs.PathFor(key)) != null);
            var entries = so.FindProperty("sprites");
            entries.arraySize = paths.Length;
            for (var i = 0; i < paths.Length; i++)
            {
                var key = Path.GetFileNameWithoutExtension(paths[i]);
                var spritePath = refinedB && P8RRefinedBAssets.Keys.Contains(key)
                    ? P8RRefinedBAssets.Root + "/" + key + ".png" : paths[i];
                if (strongerGlyphs && P8RRefinedBGlyphs.PathFor(key) != null) spritePath = P8RRefinedBAssets.PathFor(key);
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("key").stringValue = key;
                entry.FindPropertyRelative("value").objectReferenceValue = Require<Sprite>(spritePath);
            }
            so.FindProperty("refinedB").boolValue = refinedB;
            so.FindProperty("english").objectReferenceValue = Require<TextAsset>(P8RFurnitureUiPaths.Root + "/P8REnglish.json");
            so.FindProperty("font").objectReferenceValue = Require<TMP_FontAsset>(Phase8AssetPaths.UiFontPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(appearance, P8RFurnitureUiPaths.Appearance);
            AssetDatabase.SaveAssetIfDirty(appearance);
            return appearance;
        }

        private static void BuildDisplayCatalogue()
        {
            if (AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(P8RFurnitureUiPaths.Catalogue) != null) return;
            var source = Require<DecorationCatalogueAsset>(Phase8AssetPaths.FurnitureCataloguePath);
            var copy = UnityEngine.Object.Instantiate(source);
            copy.name = "DC_P8RFurniture";
            var so = new SerializedObject(copy);
            var entries = so.FindProperty("entries");
            for (var i = 0; i < entries.arraySize; i++)
            {
                var filename = Path.GetFileName(AssetDatabase.GetAssetPath(source.Entries[i].Thumbnail));
                entries.GetArrayElementAtIndex(i).FindPropertyRelative("thumbnail").objectReferenceValue =
                    Require<Sprite>(P8RFurnitureUiPaths.Root + "/Thumbnails/" + filename);
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(copy, P8RFurnitureUiPaths.Catalogue);
            AssetDatabase.SaveAssetIfDirty(copy);
        }

        private static void BuildCopy(string source, string destination, Action<GameObject> style)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(destination) != null) return;
            var root = PrefabUtility.LoadPrefabContents(source);
            try
            {
                root.name = Path.GetFileNameWithoutExtension(destination);
                style(root);
                PrefabUtility.SaveAsPrefabAsset(root, destination);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        internal static void Prepare(GameObject root, P8RAppearance appearance)
        {
            foreach (var panel in root.GetComponentsInChildren<AnimalCafePanelView>(true)) panel.enabled = false;
            foreach (var button in root.GetComponentsInChildren<AnimalCafeButtonView>(true)) button.enabled = false;
            foreach (var shadow in root.GetComponentsInChildren<Shadow>(true)) shadow.enabled = false;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = appearance.Font; text.color = P8RAppearance.Cocoa; text.raycastTarget = false;
                text.richText = false; text.enableAutoSizing = false;
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
                if (image.name == "TopHighlight" || image.name == "Top Highlight")
                {
                    image.gameObject.SetActive(false);
                    if (PrefabUtility.IsPartOfPrefabInstance(image.gameObject))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(image.gameObject);
                }
        }

        internal static void StyleCatalogue(GameObject root, P8RAppearance appearance)
        {
            Prepare(root, appearance);
            var view = root.GetComponent<DecorationCatalogueView>();
            Set(view, "appearance", appearance);
            appearance.Paint(Reference<GameObject>(view, "expandedRoot").GetComponent<Image>(), "panel_cream");
            var expandedPanel = Reference<GameObject>(view, "expandedRoot").transform;
            var title = expandedPanel.Find("P8RCatalogueTitle")?.GetComponent<TMP_Text>();
            if (title == null)
            {
                var child = new GameObject("P8RCatalogueTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
                child.transform.SetParent(expandedPanel, false); title = child.GetComponent<TMP_Text>();
            }
            title.font = appearance.Font; title.fontSize = 34; title.fontStyle = FontStyles.Bold;
            title.color = P8RAppearance.Cocoa; title.raycastTarget = false; title.richText = false;
            title.text = appearance.Text("catalogue.title"); title.alignment = TextAlignmentOptions.MidlineLeft;
            title.rectTransform.anchorMin = new Vector2(0, 1); title.rectTransform.anchorMax = Vector2.one;
            title.rectTransform.pivot = new Vector2(.5f, 1);
            title.rectTransform.offsetMin = new Vector2(24, -72); title.rectTransform.offsetMax = new Vector2(-96, -16);
            var handle = Reference<Button>(view, "collapsedHandleButton");
            StyleButton(handle, appearance, "catalogue", "secondary", false);
            handle.GetComponentInChildren<TMP_Text>(true).text = appearance.Text("catalogue.expand");
            ((RectTransform)handle.transform).sizeDelta = new Vector2(312, 64);
            StyleButton(Reference<Button>(view, "collapseButton"), appearance, "chevron_down", "secondary", true);
            var pickup = Reference<Button>(view, "pickUpPointButton");
            StyleButton(pickup, appearance, "pickup", appearance.IsRefinedB ? "primary" : "secondary", false);
            pickup.GetComponentInChildren<TMP_Text>(true).text = appearance.Text("catalogue.pickup");
            var pickupRect = (RectTransform)pickup.transform;
            pickupRect.SetParent(Reference<GameObject>(view, "expandedRoot").transform, false);
            pickupRect.anchorMin = Vector2.zero; pickupRect.anchorMax = new Vector2(1, 0);
            pickupRect.pivot = new Vector2(.5f, 0); pickupRect.anchoredPosition = new Vector2(0, 24);
            pickupRect.sizeDelta = new Vector2(-48, 72);
            var categoryHost = root.GetComponentsInChildren<RectTransform>(true).Single(rect => rect.name == "Phase7CategoryCatalogue");
            categoryHost.offsetMin = new Vector2(40, 128); categoryHost.offsetMax = new Vector2(-40, -200);

            var tabs = root.GetComponentInChildren<DecorationModeTabsView>(true);
            Set(tabs, "appearance", appearance);
            P8RCategoryTabAssets.BindIfAvailable(tabs);
            tabs.SetActive(DecorationModeKind.Furniture);
            var tabsRect = (RectTransform)tabs.transform;
            tabsRect.anchorMin = tabsRect.anchorMax = tabsRect.pivot = Vector2.zero;
            tabsRect.anchoredPosition = new Vector2(40, 776); tabsRect.sizeDelta = new Vector2(1000, 96);
            var tabFields = new[] { "furnitureButton", "floorButton", "wallButton", "wallDecorButton" };
            var tabIcons = new[] { "furniture", "floor", "wall", "wall_decor" };
            for (var i = 0; i < tabFields.Length; i++)
            {
                var button = Reference<Button>(tabs, tabFields[i]);
                StyleButton(button, appearance, tabIcons[i], "secondary", false);
                button.GetComponentInChildren<TMP_Text>(true).text = appearance.Text("nav." + tabIcons[i]);
                var tabRect = (RectTransform)button.transform;
                tabRect.anchorMin = new Vector2(i * .25f, 0); tabRect.anchorMax = new Vector2((i + 1) * .25f, 0);
                tabRect.pivot = new Vector2(.5f, 0); tabRect.anchoredPosition = Vector2.zero;
                tabRect.sizeDelta = new Vector2(-8, 96);
                button.GetComponentInChildren<TMP_Text>(true).fontSize = 26;
                var underline = ImageChild(button.transform, "SelectedUnderline");
                underline.sprite = null; underline.color = P8RAppearance.Cocoa; underline.raycastTarget = false;
                underline.rectTransform.anchorMin = Vector2.zero; underline.rectTransform.anchorMax = new Vector2(1, 0);
                underline.rectTransform.pivot = new Vector2(.5f, 0); underline.rectTransform.anchoredPosition = new Vector2(0, 4);
                underline.rectTransform.sizeDelta = new Vector2(-32, 4); underline.gameObject.SetActive(false);
                P8RButtonLayout.StackedButton(button);
            }
            tabs.SetActive(tabs.ActiveMode);

            var row = Reference<GameObject>(view, "categoryRowTemplate");
            var rowLayout = row.GetComponent<LayoutElement>();
            rowLayout.preferredHeight = rowLayout.minHeight = 304;
            var rowScroll = row.GetComponent<ScrollRect>();
            rowScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 256);
            rowScroll.viewport.offsetMax = new Vector2(0, -42);

            var tile = Reference<DecorationCatalogueTileView>(view, "categoryTileTemplate");
            Set(tile, "appearance", appearance);
            var tileRect = (RectTransform)tile.transform;
            tileRect.sizeDelta = new Vector2(216, 256);
            var layout = tile.GetComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = 216;
            layout.minHeight = layout.preferredHeight = 256;
            appearance.Paint(tile.GetComponent<Image>(), "card_base");
            var tileButton = tile.GetComponent<Button>();
            tileButton.transition = Selectable.Transition.SpriteSwap;
            tileButton.spriteState = new SpriteState { pressedSprite = appearance.Sprite("card_preview_outline") };
            tileButton.colors = NeutralColors();
            var well = ImageChild(tile.transform, "ThumbnailWell");
            Stretch(well.rectTransform); well.rectTransform.offsetMin = new Vector2(12, 92);
            well.rectTransform.offsetMax = new Vector2(-12, -12);
            appearance.Paint(well, "card_well"); well.transform.SetAsFirstSibling();
            var label = Reference<TMP_Text>(tile, "nameLabel");
            label.rectTransform.anchorMax = new Vector2(1, .36f);
            label.rectTransform.offsetMin = new Vector2(8, 6); label.rectTransform.offsetMax = new Vector2(-8, -2);
            label.fontSize = 28; label.textWrappingMode = TextWrappingModes.Normal; label.maxVisibleLines = 2;
            var applied = Reference<GameObject>(tile, "usingCheck");
            appearance.Paint(applied.GetComponent<Image>(), "badge_applied", false);
            var appliedRect = (RectTransform)applied.transform;
            appliedRect.anchorMin = appliedRect.anchorMax = Vector2.one;
            appliedRect.anchoredPosition = new Vector2(-24, -24); appliedRect.sizeDelta = Vector2.one * 44;
            foreach (var oldText in applied.GetComponentsInChildren<TMP_Text>(true)) oldText.gameObject.SetActive(false);
            var preview = Reference<GameObject>(tile, "previewOutline");
            appearance.Paint(preview.GetComponent<Image>(), "card_preview_outline");
            var dash = ImageChild(preview.transform, "PreviewDash"); Stretch(dash.rectTransform);
            appearance.Paint(dash, "card_preview_dash");
            applied.transform.SetAsLastSibling();
        }

        internal static void StyleActionBar(GameObject root, P8RAppearance appearance)
        {
            Prepare(root, appearance);
            var view = root.GetComponent<DecorationActionBarView>();
            Set(view, "appearance", appearance);
            var panel = Reference<RectTransform>(view, "presentationRoot");
            // Individual buttons already carry their own depth; keep the following group transparent.
            if (panel.GetComponent<Image>() != null) panel.GetComponent<Image>().enabled = false;
            foreach (var pair in new[] { ("storeButton", "store", "destructive"), ("cancelButton", "cancel", "secondary"),
                ("rotateButton", "rotate", "secondary"), ("confirmButton", "confirm", "primary") })
            {
                var button = Reference<Button>(view, pair.Item1);
                var face = ImageChild(button.transform, "Face"); face.transform.SetAsFirstSibling();
                var hit = button.GetComponent<Image>(); hit.color = Color.clear; hit.sprite = null;
                hit.raycastTarget = true; hit.alphaHitTestMinimumThreshold = 0f;
                button.targetGraphic = face;
                StyleButton(button, appearance, pair.Item2, pair.Item3, true);
                P8RButtonLayout.ActionFace(button, true);
            }
            var feedback = Reference<RectTransform>(view, "feedbackRoot");
            appearance.Paint(feedback.GetComponent<Image>(), "notice_preview");
            var state = Reference<GameObject>(view, "feedbackStateShape");
            appearance.Paint(state.GetComponent<Image>(), "warning_cocoa", false);
            view.SetCatalogueItemActions(DecorationCatalogueItemKind.Furniture, false);
        }

        internal static void StyleModal(GameObject root, P8RAppearance appearance)
        {
            Prepare(root, appearance);
            var view = root.GetComponent<DecorationStoreModalView>();
            Set(view, "appearance", appearance);
            appearance.Paint(view.ContentRect.GetComponent<Image>(), "panel_cream");
            var content = view.ContentRect;
            content.anchorMin = content.anchorMax = content.pivot = Vector2.one * .5f;
            content.anchoredPosition = Vector2.zero; content.sizeDelta = new Vector2(840, 560);
            var title = Reference<TMP_Text>(view, "titleLabel");
            PlaceModalText(title, 40, 184, 84);
            var body = Reference<TMP_Text>(view, "bodyLabel");
            PlaceModalText(body, 34, 26, 224);
            StyleButton(Reference<Button>(view, "confirmButton"), appearance, "store", "destructive", false);
            StyleButton(Reference<Button>(view, "cancelButton"), appearance, "cancel", "secondary", false);
            var buttons = new[] { Reference<Button>(view, "cancelButton"), Reference<Button>(view, "confirmButton") };
            for (var i = 0; i < buttons.Length; i++)
            {
                var rect = (RectTransform)buttons[i].transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
                rect.sizeDelta = new Vector2(336, 88); rect.anchoredPosition = new Vector2(i == 0 ? -184 : 184, -190);
                buttons[i].transform.Find("Label").GetComponent<TMP_Text>().fontSize = 32;
                P8RButtonLayout.TextButton(buttons[i]);
            }
            var blocker = Reference<Button>(view, "modalBlocker");
            appearance.Paint(blocker.image, "modal_scrim", false);
            blocker.image.preserveAspect = false;
            blocker.transition = Selectable.Transition.None;
        }

        internal static void PlaceModalText(TMP_Text text, float size, float y, float height)
        {
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0, .5f); rect.anchorMax = new Vector2(1, .5f);
            rect.pivot = Vector2.one * .5f; rect.sizeDelta = new Vector2(-112, height);
            rect.anchoredPosition = new Vector2(0, y); text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.Normal;
        }

        internal static void StyleButton(Button button, P8RAppearance appearance, string action, string role, bool iconOnly)
        {
            if (button == null) throw new InvalidOperationException("Missing P8R button reference: " + action);
            var icon = ImageChild(button.transform, "Icon");
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(iconOnly ? .5f : 0, .5f);
            icon.rectTransform.anchoredPosition = iconOnly ? Vector2.zero : new Vector2(28, 0);
            icon.rectTransform.sizeDelta = Vector2.one * (iconOnly ? 52 : 32);
            appearance.Button(button, action, role, iconOnly);
            var text = button.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (text != null && !iconOnly)
            {
                Stretch(text.rectTransform);
                text.fontSize = 28; text.rectTransform.offsetMin = new Vector2(52, 4);
                text.rectTransform.offsetMax = new Vector2(-8, -4);
                P8RButtonLayout.TextButton(button);
            }
            var tooltip = button.transform.Find("Tooltip");
            if (tooltip != null)
            {
                appearance.Paint(tooltip.GetComponent<Image>(), "panel_cream");
                foreach (var label in tooltip.GetComponentsInChildren<TMP_Text>(true)) label.text = appearance.Text("action." + action);
            }
            foreach (var hook in button.GetComponents<DecorationPointerBoundaryEventHook>())
            {
                var so = new SerializedObject(hook);
                so.FindProperty("semanticLabel").stringValue = appearance.Text("action." + action);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        public static bool HasP8RWiring(DecorationModeController controller)
        {
            if (controller == null) return false;
            var so = new SerializedObject(controller);
            var appearance = AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            var catalogue = so.FindProperty("catalogueView").objectReferenceValue as DecorationCatalogueView;
            var action = so.FindProperty("actionBarView").objectReferenceValue as DecorationActionBarView;
            var modal = so.FindProperty("storeModalView").objectReferenceValue as DecorationStoreModalView;
            if (appearance == null || catalogue == null || action == null || modal == null) return false;
            var tabs = catalogue.GetComponentsInChildren<DecorationModeTabsView>(true);
            var ranges = catalogue.GetComponentsInChildren<DecorationFloorRangeView>(true);
            return AssetDatabase.GetAssetPath(so.FindProperty("phase8FurnitureCatalogueAsset").objectReferenceValue) == P8RFurnitureUiPaths.Catalogue
                && SourcePath(so.FindProperty("catalogueView").objectReferenceValue) == P8RFurnitureUiPaths.CataloguePrefab
                && SourcePath(so.FindProperty("actionBarView").objectReferenceValue) == P8RFurnitureUiPaths.ActionPrefab
                && SourcePath(so.FindProperty("storeModalView").objectReferenceValue) == P8RFurnitureUiPaths.StorePrefab
                && so.FindProperty("p8rAppearance").objectReferenceValue == appearance
                && Reference<P8RAppearance>(catalogue, "appearance") == appearance
                && Reference<P8RAppearance>(action, "appearance") == appearance
                && Reference<P8RAppearance>(modal, "appearance") == appearance
                && tabs.Length == 1 && ranges.Length == 1
                && so.FindProperty("modeTabsView").objectReferenceValue == tabs[0]
                && so.FindProperty("floorRangeView").objectReferenceValue == ranges[0]
                && Reference<P8RAppearance>(tabs[0], "appearance") == appearance
                && ranges[0].transform.parent == catalogue.SurfaceFooterHost
                && Reference<DecorationCatalogueTileView>(catalogue, "categoryTileTemplate") != null
                && Reference<P8RAppearance>(Reference<DecorationCatalogueTileView>(catalogue, "categoryTileTemplate"), "appearance") == appearance;
        }

        public static bool HasAnyP8RWiring(DecorationModeController controller)
        {
            if (controller == null) return false;
            var so = new SerializedObject(controller);
            if (so.FindProperty("p8rAppearance").objectReferenceValue != null
                || AssetDatabase.GetAssetPath(so.FindProperty("phase8FurnitureCatalogueAsset").objectReferenceValue)
                    .StartsWith(P8RFurnitureUiPaths.Root + "/", StringComparison.Ordinal)) return true;
            // Also inspect scene-owned UI when a controller reference itself was cleared.
            return controller.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Component>(true))
                .Where(item => item is DecorationCatalogueView || item is DecorationActionBarView || item is DecorationStoreModalView)
                .Any(item => SourcePath(item).StartsWith(P8RFurnitureUiPaths.Root + "/", StringComparison.Ordinal));
        }

        private static void WireMainCafe()
        {
            var loaded = SceneManager.GetSceneByPath(Phase8AssetPaths.MainCafeScenePath);
            var wasPresent = loaded.IsValid();
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
                throw new InvalidOperationException("Save or revert MainCafe before targeted P8R authoring.");
            var opened = !loaded.IsValid() || !loaded.isLoaded;
            var active = SceneManager.GetActiveScene();
            var scene = opened ? EditorSceneManager.OpenScene(Phase8AssetPaths.MainCafeScenePath, OpenSceneMode.Additive) : loaded;
            try
            {
                WireScene(scene);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, !wasPresent);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }

        private static void WireScene(Scene scene)
        {
            var controller = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true)).Single();
            if (HasP8RWiring(controller)) return;
            if (HasAnyP8RWiring(controller))
                throw new InvalidOperationException("P8R UI wiring is incomplete. Restore its missing references before authoring; existing UI was retained.");
            var oldCatalogue = Reference<DecorationCatalogueView>(controller, "catalogueView");
            var oldAction = Reference<DecorationActionBarView>(controller, "actionBarView");
            var oldModal = Reference<DecorationStoreModalView>(controller, "storeModalView");
            // Validate every source and target before the first mutation.
            if (oldCatalogue == null || oldAction == null || oldModal == null
                || oldCatalogue.gameObject.scene != scene || oldAction.gameObject.scene != scene || oldModal.gameObject.scene != scene)
                throw new InvalidOperationException("MainCafe requires all three scene-owned UI references before P8R migration.");
            var targetCatalogue = Require<GameObject>(P8RFurnitureUiPaths.CataloguePrefab);
            if (targetCatalogue.GetComponent<DecorationCatalogueView>() == null
                || targetCatalogue.GetComponentsInChildren<DecorationModeTabsView>(true).Length != 1
                || targetCatalogue.GetComponentsInChildren<DecorationFloorRangeView>(true).Length != 1
                || Require<GameObject>(P8RFurnitureUiPaths.ActionPrefab).GetComponent<DecorationActionBarView>() == null
                || Require<GameObject>(P8RFurnitureUiPaths.StorePrefab).GetComponent<DecorationStoreModalView>() == null)
                throw new InvalidOperationException("Approved P8R prefab shapes are incomplete; no scene objects were changed.");
            Require<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            Require<DecorationCatalogueAsset>(P8RFurnitureUiPaths.Catalogue);
            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("P8R Furniture UI migration");
            Undo.RegisterCompleteObjectUndo(controller, "P8R controller references");
            try
            {
            var catalogue = Replace(oldCatalogue, P8RFurnitureUiPaths.CataloguePrefab);
            var action = Replace(oldAction, P8RFurnitureUiPaths.ActionPrefab);
            var modal = Replace(oldModal, P8RFurnitureUiPaths.StorePrefab);
            Set(controller, "catalogueView", catalogue); Set(controller, "actionBarView", action); Set(controller, "storeModalView", modal);
            Set(controller, "modeTabsView", catalogue.GetComponentInChildren<DecorationModeTabsView>(true));
            Set(controller, "floorRangeView", catalogue.GetComponentInChildren<DecorationFloorRangeView>(true));
            Set(controller, "phase8FurnitureCatalogueAsset", Require<DecorationCatalogueAsset>(P8RFurnitureUiPaths.Catalogue));
            Set(controller, "p8rAppearance", Require<P8RAppearance>(P8RFurnitureUiPaths.Appearance));
            SceneManager.SetActiveScene(scene);
            var report = Phase8Validator.ValidateOpenScene();
            if (report.Issues.Count != 0) throw new InvalidOperationException("P8R candidate validation failed: "
                + string.Join("; ", report.Issues.Select(issue => issue.Code + ": " + issue.Message)));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save targeted MainCafe wiring.");
            Undo.CollapseUndoOperations(undoGroup);
            }
            catch { Undo.RevertAllDownToGroup(undoGroup); throw; }
        }

        private static T Replace<T>(T old, string path) where T : Component
        {
            if (old == null) throw new InvalidOperationException("Missing existing UI component: " + typeof(T).Name);
            var parent = old.transform.parent;
            var index = old.transform.GetSiblingIndex();
            var copy = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(path), parent);
            Undo.RegisterCreatedObjectUndo(copy, "Create P8R UI copy");
            copy.transform.SetSiblingIndex(index);
            // Retain only scene placement; internal copied layout belongs to the new prefab.
            var a = (RectTransform)old.transform; var b = (RectTransform)copy.transform;
            b.anchorMin = a.anchorMin; b.anchorMax = a.anchorMax; b.pivot = a.pivot;
            b.anchoredPosition = a.anchoredPosition; b.sizeDelta = a.sizeDelta; b.localScale = a.localScale;
            copy.SetActive(old.gameObject.activeSelf);
            Undo.DestroyObjectImmediate(old.gameObject);
            return copy.GetComponent<T>();
        }

        internal static string SourcePath(UnityEngine.Object value) => AssetDatabase.GetAssetPath(
            value != null ? PrefabUtility.GetCorrespondingObjectFromSource(value) : null);
        internal static T Reference<T>(UnityEngine.Object owner, string name) where T : UnityEngine.Object =>
            (T)new SerializedObject(owner).FindProperty(name).objectReferenceValue;
        internal static void Set(UnityEngine.Object owner, string name, UnityEngine.Object value)
        {
            var so = new SerializedObject(owner); so.FindProperty(name).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        internal static T Require<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing approved P8R asset: " + path);
        internal static Image ImageChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null) { child = new GameObject(name, typeof(RectTransform), typeof(Image)).transform; child.SetParent(parent, false); }
            var image = child.GetComponent<Image>() ?? child.gameObject.AddComponent<Image>();
            image.raycastTarget = false; return image;
        }
        internal static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static ColorBlock NeutralColors() => new ColorBlock { normalColor = Color.white,
            highlightedColor = Color.white, pressedColor = Color.white, selectedColor = Color.white,
            disabledColor = Color.white, colorMultiplier = 1, fadeDuration = 0 };
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        internal static void RequireCleanLoadedAssets()
        {
            var dirty = Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Where(item => item != null && EditorUtility.IsPersistent(item)
                && EditorUtility.IsDirty(item) && AssetDatabase.GetAssetPath(item).StartsWith("Assets/", StringComparison.Ordinal)).ToArray();
            if (dirty.Length != 0) throw new InvalidOperationException("Loaded assets are dirty; save them before P8R authoring.");
        }
    }
}

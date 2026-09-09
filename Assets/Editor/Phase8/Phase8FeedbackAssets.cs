using System;
using System.Linq;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.Phase8
{
    /// <summary>Build readable P8 feedback without changing the shared P5/P6 font.</summary>
    public static class Phase8FeedbackAssets
    {
        private const string SourceFontPath = "Assets/UI/Phase5/Fonts/NotoSansSC-Regular.otf";

        public static TMP_FontAsset EnsureFont()
        {
            var required = RequiredCharacters();
            var live = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Phase8AssetPaths.UiFontPath);
            if (live != null && live.atlasPopulationMode == AtlasPopulationMode.Static
                && required.All(c => char.IsWhiteSpace(c) || live.HasCharacter(c))) return live;
            var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath)
                ?? throw new InvalidOperationException("Missing P8 font source: " + SourceFontPath);
            var candidate = TMP_FontAsset.CreateFontAsset(source, 64, 8, GlyphRenderMode.SDFAA,
                2048, 2048, AtlasPopulationMode.Dynamic, false);
            if (candidate == null) throw new InvalidOperationException("Could not create P8 font candidate.");
            var atlas = candidate.atlasTextures[0];
            var material = candidate.material;
            try
            {
                if (!candidate.TryAddCharacters(required, out var missing) || !string.IsNullOrEmpty(missing))
                    throw new InvalidOperationException("P8 font is missing required glyphs: " + missing);
                candidate.name = "NotoSansSC-Phase8 SDF";
                candidate.atlasPopulationMode = AtlasPopulationMode.Static;
                material.name = "NotoSansSC-Phase8 Material";
                atlas.name = "NotoSansSC-Phase8 Atlas";
                if (live == null)
                {
                    if (!AssetDatabase.IsValidFolder(Phase8AssetPaths.Root + "/Fonts"))
                        AssetDatabase.CreateFolder(Phase8AssetPaths.Root, "Fonts");
                    AssetDatabase.CreateAsset(candidate, Phase8AssetPaths.UiFontPath);
                    AssetDatabase.AddObjectToAsset(atlas, candidate);
                    AssetDatabase.AddObjectToAsset(material, candidate);
                    live = candidate;
                }
                else
                {
                    // Preserve the existing font/material/atlas GUID and local file IDs.
                    var subassets = AssetDatabase.LoadAllAssetsAtPath(Phase8AssetPaths.UiFontPath);
                    var liveAtlas = subassets.OfType<Texture2D>().Single();
                    var liveMaterial = subassets.OfType<Material>().Single();
                    if (liveAtlas.width != atlas.width || liveAtlas.height != atlas.height)
                        throw new InvalidOperationException("P8 font atlas size cannot be replaced in place.");
                    Graphics.CopyTexture(atlas, liveAtlas);
                    EditorUtility.CopySerialized(material, liveMaterial);
                    EditorUtility.CopySerialized(candidate, live);
                    liveMaterial.mainTexture = liveAtlas;
                    live.material = liveMaterial;
                    live.atlasTextures = new[] { liveAtlas };
                    live.ReadFontAssetDefinition();
                    EditorUtility.SetDirty(liveAtlas);
                    EditorUtility.SetDirty(liveMaterial);
                }
                EditorUtility.SetDirty(live);
                AssetDatabase.SaveAssetIfDirty(live);
                return live;
            }
            finally
            {
                if (!EditorUtility.IsPersistent(candidate))
                {
                    UnityEngine.Object.DestroyImmediate(candidate);
                    UnityEngine.Object.DestroyImmediate(material);
                    UnityEngine.Object.DestroyImmediate(atlas);
                }
            }
        }

        private static string RequiredCharacters()
        {
            var functional = Enum.GetValues(typeof(FunctionalSurfacePlacementFailureReason))
                .Cast<FunctionalSurfacePlacementFailureReason>().Select(reason =>
                    PlacementFeedbackMapper.GetPlayerMessage(FunctionalSurfacePlacementResult.Failure(reason)));
            var readiness = Enum.GetValues(typeof(LayoutReadinessFailureCode)).Cast<LayoutReadinessFailureCode>()
                .Select(code => PlacementFeedbackMapper.GetPlayerMessage(code));
            var floor = Enum.GetValues(typeof(PlacementFailureReason)).Cast<PlacementFailureReason>()
                .Select(reason => PlacementFeedbackMapper.GetPlayerMessage(reason == PlacementFailureReason.None
                    ? PlacementResult.Success() : PlacementResult.Failure(reason)));
            var legacy = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/UI/Phase6/Fonts/NotoSansSC-Phase6 SDF.asset");
            var legacyCharacters = legacy == null ? string.Empty : string.Concat(legacy.characterTable
                .Select(character => char.ConvertFromUtf32((int)character.unicode)));
            var text = legacyCharacters + string.Join(" ", functional.Concat(readiness).Concat(floor))
                + "布局已准备好，可以营业但暂时不能营业 收银机 咖啡机 取餐点 员工 客人 Employee Customer 承托 设备 位置 格子（）【】：，。"
                + new string(Enumerable.Range(32, 95).Select(value => (char)value).ToArray());
            return new string(text.Where(c => !char.IsControl(c)).Distinct().OrderBy(c => c).ToArray());
        }

        public static void BindPrefabFonts(TMP_FontAsset font)
        {
            foreach (var path in new[] { Phase8AssetPaths.CataloguePrefabPath, Phase8AssetPaths.ActionBarPrefabPath })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var changed = false;
                    foreach (var label in root.GetComponentsInChildren<TMP_Text>(true))
                    {
                        if (label.font == font) continue;
                        label.font = font;
                        changed = true;
                    }
                    if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        public static bool ConfigureMessageView(ValidationMessageView view)
        {
            var before = Capture(view);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Phase8AssetPaths.UiFontPath)
                ?? throw new InvalidOperationException("Run Phase 8 / Build Assets before configuring feedback.");
            var root = (RectTransform)view.transform;
            // P7's runtime root is a 190px bottom sheet, not a full-screen feedback layer.
            var screenCanvas = view.gameObject.scene.GetRootGameObjects()
                .SelectMany(item => item.GetComponentsInChildren<Canvas>(true))
                .Single(item => item.name == "Screen Canvas");
            // Capture parent-layer repairs before Child adds components or sibling order changes.
            var existingLayer = screenCanvas.transform.Find("Phase8_FeedbackLayer");
            var componentWasMissing = existingLayer == null
                || existingLayer.GetComponent<SafeAreaContainer>() == null;
            var siblingIndexChanged = existingLayer != null && existingLayer.GetSiblingIndex() != 0;
            var feedbackLayer = Child(screenCanvas.transform, "Phase8_FeedbackLayer", typeof(SafeAreaContainer));
            var layerBefore = EditorJsonUtility.ToJson(feedbackLayer);
            var parentChanged = root.parent != feedbackLayer;
            Stretch(feedbackLayer, Vector2.zero, Vector2.zero);
            feedbackLayer.SetAsFirstSibling();
            if (root.parent != feedbackLayer) root.SetParent(feedbackLayer, false);
            root.anchorMin = root.anchorMax = new Vector2(.5f, 1);
            root.pivot = new Vector2(.5f, 1);
            root.anchoredPosition = new Vector2(0, -96);
            var label = view.GetComponentInChildren<TMP_Text>(true)
                ?? throw new InvalidOperationException("P8 validation message is missing its label.");
            var viewport = Child(root, "ReadinessViewport", typeof(Image), typeof(RectMask2D));
            Stretch(viewport, new Vector2(20, 8), new Vector2(-24, -8));
            viewport.GetComponent<Image>().color = Color.clear;
            viewport.GetComponent<Image>().raycastTarget = true;
            if (label.transform.parent != viewport) label.transform.SetParent(viewport, false);
            var content = label.rectTransform;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, 48);
            label.font = font;
            label.fontSize = 22;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.richText = false;
            label.raycastTarget = false;

            var barRect = Child(root, "ReadinessScrollbar", typeof(Image), typeof(Scrollbar));
            barRect.anchorMin = new Vector2(1, 0);
            barRect.anchorMax = Vector2.one;
            barRect.pivot = new Vector2(1, .5f);
            barRect.offsetMin = new Vector2(-16, 8);
            barRect.offsetMax = new Vector2(-6, -8);
            barRect.GetComponent<Image>().color = new Color(1, 1, 1, .12f);
            var handle = Child(barRect, "Handle", typeof(Image));
            Stretch(handle, Vector2.zero, Vector2.zero);
            handle.GetComponent<Image>().color = new Color(1, 1, 1, .65f);
            var bar = barRect.GetComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle;
            bar.targetGraphic = handle.GetComponent<Image>();
            var scroll = view.GetComponent<ScrollRect>() ?? view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            if (view.GetComponent<CanvasGroup>() == null) view.gameObject.AddComponent<CanvasGroup>();
            view.Clear();

            // The shared Store prefab stays unchanged; only this P8 scene uses the wider glyph set.
            var storeChanged = false;
            foreach (var store in view.gameObject.scene.GetRootGameObjects()
                .SelectMany(item => item.GetComponentsInChildren<DecorationStoreModalView>(true)))
            foreach (var text in store.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font == font) continue;
                text.font = font;
                EditorUtility.SetDirty(text);
                storeChanged = true;
            }
            return storeChanged || parentChanged || componentWasMissing || siblingIndexChanged
                || layerBefore != EditorJsonUtility.ToJson(feedbackLayer)
                || before != Capture(view);
        }

        private static string Capture(ValidationMessageView view) => string.Join("\n",
            view.GetComponentsInChildren<Component>(true).Select(component => EditorJsonUtility.ToJson(component)));

        private static RectTransform Child(Transform parent, string name, params Type[] types)
        {
            var existing = parent.Find(name);
            var child = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
            if (existing == null) child.transform.SetParent(parent, false);
            foreach (var type in types) if (child.GetComponent(type) == null) child.AddComponent(type);
            return (RectTransform)child.transform;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }
    }
}

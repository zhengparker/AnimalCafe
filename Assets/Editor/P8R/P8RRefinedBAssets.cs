using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimalCafe.UI.P8R;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.P8R
{
    /// <summary>One geometric source for B's UI frames; bake once in Editor, no runtime drawing.
    /// B 底板共用一组几何规则；只生成独立素材，不改原 PNG、icon 或缩略图。</summary>
    public static class P8RRefinedBAssets
    {
        public const string Root = "Assets/UI/P8R/RefinedB";
        private const int Size = 384;
        private static readonly string[] FrameKeys = {
            "panel_cream", "panel_inset", "panel_light", "button_primary_normal", "button_primary_pressed",
            "button_secondary_normal", "button_secondary_pressed", "button_destructive_normal", "button_destructive_pressed",
            "button_disabled", "card_base", "card_well", "tab_idle", "tab_selected", "tab_unavailable",
            "notice_info", "notice_success", "notice_warning", "notice_error", "notice_preview",
            "card_preview_outline", "card_preview_dash"
        };
        public static IReadOnlyList<string> Keys => FrameKeys;
        public static IEnumerable<string> ReplacementKeys => FrameKeys.Concat(P8RRefinedBGlyphs.Keys);
        public static string PathFor(string key) => P8RColoredActionAssets.PreferredPathFor(key)
            ?? P8RColoredTabAssets.PreferredPathFor(key)
            ?? P8RColoredRangeAssets.PreferredPathFor(key)
            ?? (FrameKeys.Contains(key) ? Root + "/" + key + ".png" : P8RRefinedBGlyphs.PathFor(key));

        private static Color Hex(string value)
        {
            if (!ColorUtility.TryParseHtmlString("#" + value, out var color)) throw new ArgumentException(value);
            return color;
        }

        private static void Style(string key, out Color fill, out Color line, out float radius, out bool outline)
        {
            if (!FrameKeys.Contains(key)) throw new ArgumentException("Unknown B frame: " + key, nameof(key));
            fill = Hex("F9F3E8"); line = Hex("78675A"); radius = key.StartsWith("panel_") ? 72 : 48;
            if (key.StartsWith("notice_")) radius = 64;
            outline = key.StartsWith("card_preview_");
            if (key == "panel_inset" || key == "card_well") { fill = Hex("EFE8DA"); line = Hex("B9AA98"); }
            if (key == "card_base") { fill = Hex("E8D8BF"); line = Hex("B3A18E"); }
            if (key.StartsWith("button_primary") || key == "tab_selected") fill = Hex("FBD5AF");
            if (key.StartsWith("button_destructive")) { fill = Hex("F1D4C6"); line = Hex("9B6757"); }
            if (key == "button_disabled" || key == "tab_unavailable") { fill = Hex("E8E0D5"); line = Hex("B1A496"); }
            if (key == "notice_success") { fill = Hex("EDF2E5"); line = Hex("76876A"); }
            if (key == "notice_warning") { fill = Hex("FAEDCE"); line = Hex("AB8950"); }
            if (key == "notice_error") { fill = Hex("F3DDD1"); line = Hex("A57360"); }
            if (key == "notice_preview") { fill = Hex("F4EADB"); line = Hex("95806B"); }
            if (outline) { fill = Color.clear; line = Hex("B97942"); }
            if (key.EndsWith("_pressed"))
                fill = new Color(fill.r - .035f, fill.g - .045f, fill.b - .05f, 1);
        }

        // Signed distance gives regular curves and alpha-covered edges, without importing a new package.
        // 同一圆角公式用于所有状态，pressed 不改变轮廓或位移。
        private static float Distance(float x, float y, float radius)
        {
            var q = new Vector2(Mathf.Abs(x - 192) - (188 - radius), Mathf.Abs(y - 196) - (184 - radius));
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
        }

        public static Texture2D Render(string key) => Render(key, null);

        // Category-only palette shares the exact B raster geometry; shared keys never change.
        // 只替换分类底板填色，不把新颜色加入共享 FrameKeys 或 Appearance key 表。
        public static Texture2D RenderCategoryTab(string state)
        {
            if (state != "idle" && state != "selected" && state != "unavailable")
                throw new ArgumentException("Unknown category tab state: " + state, nameof(state));
            return Render("tab_" + state, state == "idle" ? Hex("E3E3D3")
                : state == "selected" ? Hex("BFC6A3") : Hex("E8E0D5"));
        }

        private static Texture2D Render(string key, Color? fillOverride)
        {
            Style(key, out var fill, out var line, out var radius, out var outline);
            if (fillOverride.HasValue) fill = fillOverride.Value;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false);
            var pixels = new Color32[Size * Size];
            var shadowColor = Hex("62554B");
            var stroke = key == "panel_inset" || key == "card_well" ? 8f
                : key.StartsWith("panel_") || key.StartsWith("notice_") ? 10f : 9f;
            for (var y = 0; y < Size; y++) for (var x = 0; x < Size; x++)
            {
                var distance = Distance(x + .5f, y + .5f, radius);
                var edge = Mathf.Clamp01(.5f - distance);
                var inner = Mathf.Clamp01(.5f - distance - stroke); // 2.25 controls / 2.5 containers / 2 inset, at existing 4x PPU.
                var grain = (((x * 37 + y * 17) % 11) / 10f - .5f) * .004f;
                var paper = new Color(fill.r + grain, fill.g + grain, fill.b + grain, fill.a);
                var color = Color.Lerp(line, paper, inner);
                var alpha = outline ? Mathf.Max(0, edge - inner) : edge;
                if (key == "card_preview_dash" && ((x + y) / 24) % 2 != 0) alpha = 0;
                // One short soft shadow, behind the face only. Never stack with a Unity Shadow component.
                var shadowDistance = Distance(x + .5f, y + 8.5f, radius);
                var shadow = outline || key == "card_well" ? 0 : .20f * Mathf.Clamp01((6f - shadowDistance) / 6f);
                var total = alpha + shadow * (1 - alpha);
                if (total > 0)
                {
                    color = (color * alpha + shadowColor * (shadow * (1 - alpha))) / total;
                    color.a = total;
                }
                else color = Color.clear;
                pixels[y * Size + x] = color;
            }
            texture.SetPixels32(pixels); texture.Apply(false, false);
            return texture;
        }

        [MenuItem("Tools/AnimalCafe/P8R/Bake Approved Refined B Frames")]
        public static void BuildApproved()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before B authoring.");
            var appearance = AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            if (appearance == null || EditorUtility.IsDirty(appearance)) throw new InvalidOperationException("P8R Appearance is missing or has unsaved changes.");
            var rendered = new Dictionary<string, byte[]>();
            foreach (var key in FrameKeys)
            {
                var path = Root + "/" + key + ".png";
                var importer = AssetImporter.GetAtPath(path);
                if (importer != null && EditorUtility.IsDirty(importer)) throw new InvalidOperationException("Unsaved B importer: " + path);
                var texture = Render(key);
                try { rendered.Add(path, texture.EncodeToPNG()); }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            // All frames are rendered before any asset is written. Source folders are never targets.
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Assets/UI/P8R", "RefinedB");
            foreach (var entry in rendered)
            {
                var changed = !File.Exists(entry.Key) || !File.ReadAllBytes(entry.Key).SequenceEqual(entry.Value);
                if (changed)
                {
                    File.WriteAllBytes(entry.Key, entry.Value);
                    AssetDatabase.ImportAsset(entry.Key, ImportAssetOptions.ForceSynchronousImport);
                }
                if (AssetImporter.GetAtPath(entry.Key) == null) AssetDatabase.ImportAsset(entry.Key, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(entry.Key);
                // A setter can dirty an importer even when its serialized value stays the same.
                // 必须先比较再写；仅比较写入后的 JSON 会留下未保存 importer。
                if (HasApprovedImportSettings(importer)) continue;
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100; importer.spriteBorder = Vector4.one * 96;
                importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
                importer.sRGBTexture = true; importer.mipmapEnabled = false; importer.isReadable = false;
                importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 512; importer.npotScale = TextureImporterNPOTScale.None;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            Debug.Log("P8R B frames baked: " + FrameKeys.Length + "; source PNGs unchanged. Apply the style-only migration to switch UI.");
        }

        private static bool HasApprovedImportSettings(TextureImporter importer)
        {
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            return importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                && importer.spritePixelsPerUnit == 100 && importer.spriteBorder == Vector4.one * 96
                && importer.alphaSource == TextureImporterAlphaSource.FromInput && importer.alphaIsTransparency
                && importer.sRGBTexture && !importer.mipmapEnabled && !importer.isReadable
                && importer.filterMode == FilterMode.Bilinear && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.wrapMode == TextureWrapMode.Clamp && importer.maxTextureSize == 512
                && importer.npotScale == TextureImporterNPOTScale.None && settings.spriteMeshType == SpriteMeshType.FullRect;
        }

        internal static bool BindAppearance(P8RAppearance appearance)
        {
            var serialized = new SerializedObject(appearance); var entries = serialized.FindProperty("sprites");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < entries.arraySize; i++)
                if (!seen.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue))
                    throw new InvalidOperationException("Duplicate P8R appearance key.");
            foreach (var key in ReplacementKeys)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PathFor(key));
                if (sprite == null) throw new InvalidOperationException("Failed B import: " + key);
                var matched = false;
                for (var i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("key").stringValue != key) continue;
                    if (entry.FindPropertyRelative("value").objectReferenceValue != sprite)
                        entry.FindPropertyRelative("value").objectReferenceValue = sprite;
                    matched = true;
                }
                if (!matched) throw new InvalidOperationException("Missing existing appearance key: " + key);
            }
            if (!serialized.FindProperty("refinedB").boolValue) serialized.FindProperty("refinedB").boolValue = true;
            var changed = serialized.hasModifiedProperties;
            if (changed) { serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(appearance); }
            return changed;
        }
    }
}

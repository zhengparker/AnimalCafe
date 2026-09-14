using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.P8R;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.P8R
{
    /// <summary>Import approved original PNGs without rewriting any pixels.
    /// 仅用 Sprite rect 裁去透明边缘；原始 PNG 字节不变。</summary>
    public static class P8RColoredTabAssets
    {
        public const string Root = "Assets/UI/P8R/TabIcons";
        public const string OutlinedRoot = Root + "/Outlined";
        private static readonly string[] Actions = { "furniture", "floor", "wall", "wall_decor" };
        private static readonly string[] Fields = { "furnitureButton", "floorButton", "wallButton", "wallDecorButton" };
        private static readonly string[] Platforms = { "Standalone", "Android", "iPhone" };
        private static string PathFor(string action) => OutlinedRoot + "/tab_" + action + "_color.png";

        internal static string PreferredPathFor(string key)
        {
            var action = Actions.FirstOrDefault(a => key == a + "_cocoa" || key == "tab_" + a + "_color");
            return action != null && Actions.All(a => AssetDatabase.LoadAssetAtPath<Sprite>(PathFor(a)) != null)
                ? PathFor(action) : null;
        }

        [MenuItem("Tools/AnimalCafe/P8R/Import Approved Colored Catalogue Tabs")]
        public static void ImportApproved()
        {
            ImportApproved(Actions.Select(PathFor), "colored tab");
        }

        /// <summary>Shared lossless importer for approved cropped UI artwork.
        /// 共用同一套 alpha rect 与小尺寸 UI import settings，不改写 PNG。</summary>
        internal static void ImportApproved(IEnumerable<string> sourcePaths, string assetLabel)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before importing approved UI artwork.");
            var paths = sourcePaths.ToArray();
            if (paths.Length == 0 || paths.Distinct(StringComparer.Ordinal).Count() != paths.Length)
                throw new InvalidOperationException("Approved " + assetLabel + " paths must be non-empty and unique.");
            // Validate every source before changing any importer. Never encode or save a texture.
            // 先检查全部原图，再改 metadata；不输出加工后的 PNG。
            var bounds = new Dictionary<string, Rect>();
            foreach (var path in paths)
            {
                if (!File.Exists(path)) throw new FileNotFoundException("Approved " + assetLabel + " is missing.", path);
                var current = AssetImporter.GetAtPath(path);
                if (current != null && EditorUtility.IsDirty(current))
                    throw new InvalidOperationException("Unsaved " + assetLabel + " importer: " + path);
                bounds.Add(path, ReadAlphaBounds(path, assetLabel));
            }
            foreach (var item in bounds)
            {
                if (AssetImporter.GetAtPath(item.Key) == null)
                    AssetDatabase.ImportAsset(item.Key, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(item.Key) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Not a texture: " + item.Key);
                var spriteName = Path.GetFileNameWithoutExtension(item.Key);
                if (HasApprovedSettings(importer, spriteName, item.Value)) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.spritePixelsPerUnit = 100;
                importer.spriteBorder = Vector4.zero;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = true;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.mipmapFilter = TextureImporterMipFilter.BoxFilter;
                importer.mipMapsPreserveCoverage = false;
                importer.fadeout = false;
                importer.borderMipmap = false;
                importer.mipMapBias = 0;
                importer.streamingMipmaps = false;
                importer.isReadable = false;
                importer.filterMode = FilterMode.Trilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 2048;
                importer.npotScale = TextureImporterNPOTScale.None;
                foreach (var platform in Platforms) importer.ClearPlatformTextureSettings(platform);
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
#pragma warning disable CS0618 // One rect without introducing a new 2D Sprite package dependency.
                importer.spritesheet = new[] { new SpriteMetaData { name = spriteName, rect = item.Value,
                    alignment = (int)SpriteAlignment.Center, pivot = Vector2.one * .5f, border = Vector4.zero } };
#pragma warning restore CS0618
                importer.SaveAndReimport();
            }
        }

        private static Rect ReadAlphaBounds(string path, string assetLabel)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot decode " + path);
                if (texture.width > 2048 || texture.height > 2048)
                    throw new InvalidOperationException("Approved " + assetLabel + " exceeds lossless import size: " + path);
                var pixels = texture.GetPixels32();
                var minX = texture.width; var minY = texture.height; var maxX = -1; var maxY = -1;
                for (var y = 0; y < texture.height; y++) for (var x = 0; x < texture.width; x++)
                {
                    if (pixels[y * texture.width + x].a < 16) continue;
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }
                if (maxX < minX) throw new InvalidOperationException("Approved " + assetLabel + " has no visible pixels: " + path);
                return new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static bool HasApprovedSettings(TextureImporter importer, string name, Rect bounds)
        {
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
#pragma warning disable CS0618
            var sprites = importer.spritesheet;
#pragma warning restore CS0618
            return importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Multiple
                && importer.spritePixelsPerUnit == 100 && importer.spriteBorder == Vector4.zero
                && importer.alphaSource == TextureImporterAlphaSource.FromInput && importer.alphaIsTransparency && importer.sRGBTexture
                && importer.mipmapEnabled && importer.mipmapFilter == TextureImporterMipFilter.BoxFilter
                && !importer.mipMapsPreserveCoverage && !importer.fadeout && !importer.borderMipmap
                && importer.mipMapBias == 0 && !importer.streamingMipmaps && !importer.isReadable
                && importer.filterMode == FilterMode.Trilinear && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.wrapMode == TextureWrapMode.Clamp && importer.maxTextureSize == 2048
                && importer.npotScale == TextureImporterNPOTScale.None && settings.spriteMeshType == SpriteMeshType.FullRect
                && Platforms.All(p => !importer.GetPlatformTextureSettings(p).overridden)
                && sprites.Length == 1 && sprites[0].name == name && sprites[0].rect == bounds
                && sprites[0].alignment == (int)SpriteAlignment.Center && sprites[0].pivot == Vector2.one * .5f
                && sprites[0].border == Vector4.zero;
        }

        [MenuItem("Tools/AnimalCafe/P8R/Apply Approved Colored Catalogue Tabs")]
        public static void ApplyApproved()
        {
            var appearance = AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P8RFurnitureUiPaths.CataloguePrefab);
            if (appearance == null || prefab == null) throw new InvalidOperationException("P8R Appearance and catalogue prefab must already exist.");
            if (EditorUtility.IsDirty(appearance) || EditorUtility.IsDirty(prefab)
                || prefab.GetComponentsInChildren<Component>(true).Any(c => c != null && EditorUtility.IsDirty(c)))
                throw new InvalidOperationException("Save or revert P8R Appearance/catalogue changes before colored tab authoring.");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == P8RFurnitureUiPaths.CataloguePrefab)
                throw new InvalidOperationException("Close catalogue Prefab Mode before colored tab authoring.");
            var entries = new SerializedObject(appearance).FindProperty("sprites");
            var keys = Enumerable.Range(0, entries.arraySize)
                .Select(i => entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue).ToArray();
            if (keys.Length != 154 || keys.Distinct().Count() != 154 || Actions.Any(a => !keys.Contains(a + "_cocoa")))
                throw new InvalidOperationException("Expected the existing 154 unique P8R keys.");
            TabButtons(prefab); // Validate the four existing references before authoring.
            ImportApproved();
            P8RCategoryTabAssets.BuildApproved();
            var sprites = Actions.Select(a => AssetDatabase.LoadAssetAtPath<Sprite>(PathFor(a))).ToArray();
            if (sprites.Any(s => s == null)) throw new InvalidOperationException("Colored tab import did not produce all four Sprites.");
            var root = PrefabUtility.LoadPrefabContents(P8RFurnitureUiPaths.CataloguePrefab);
            try
            {
                var buttons = TabButtons(root);
                var oldFaces = buttons.Select(b => EditorJsonUtility.ToJson(b.image) + EditorJsonUtility.ToJson(b)).ToArray();
                var icons = buttons.Select(b => b.transform.Find("Icon").GetComponent<Image>()).ToArray();
                var labels = buttons.Select(b => b.transform.Find("Label")).ToArray();
                // Snapshot before SetActive: that lifecycle call can already repair stale icon references.
                // 生命周期会先更新图标；必须提前记录，确保修复真正保存到 prefab。
                var oldIcons = icons.Select((icon, i) => EditorJsonUtility.ToJson(icon) + EditorJsonUtility.ToJson(icon.rectTransform)
                    + (labels[i] != null && labels[i].gameObject.activeSelf)).ToArray();
                var tabs = root.GetComponentInChildren<DecorationModeTabsView>(true);
                var changed = P8RCategoryTabAssets.BindIfAvailable(tabs);
                tabs.SetActive(tabs.ActiveMode);
                for (var i = 0; i < buttons.Length; i++)
                {
                    changed |= oldFaces[i] != EditorJsonUtility.ToJson(buttons[i].image) + EditorJsonUtility.ToJson(buttons[i]);
                    var icon = icons[i];
                    icon.sprite = sprites[i]; icon.color = Color.white; icon.material = null;
                    icon.type = Image.Type.Simple; icon.preserveAspect = true;
                    icon.canvasRenderer.SetColor(Color.white);
                    P8RButtonLayout.StackedButton(buttons[i]);
                    changed |= oldIcons[i] != EditorJsonUtility.ToJson(icon) + EditorJsonUtility.ToJson(icon.rectTransform)
                        + (labels[i] != null && labels[i].gameObject.activeSelf);
                }
                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, P8RFurnitureUiPaths.CataloguePrefab, out var saved);
                    if (!saved) throw new InvalidOperationException("Could not save colored catalogue tabs.");
                }
                var serialized = new SerializedObject(appearance); entries = serialized.FindProperty("sprites");
                for (var i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    var index = Array.FindIndex(Actions, a => entry.FindPropertyRelative("key").stringValue == a + "_cocoa");
                    if (index >= 0 && entry.FindPropertyRelative("value").objectReferenceValue != sprites[index])
                        entry.FindPropertyRelative("value").objectReferenceValue = sprites[index];
                }
                if (serialized.hasModifiedProperties)
                {
                    serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(appearance);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            Debug.Log("P8R colored tabs applied: four existing keys and catalogue icons only; PNG bytes and scenes unchanged.");
        }

        private static Button[] TabButtons(GameObject root)
        {
            var tabs = root.GetComponentInChildren<DecorationModeTabsView>(true);
            if (tabs == null) throw new InvalidOperationException("Catalogue tabs are missing.");
            var serialized = new SerializedObject(tabs);
            return Fields.Select(field =>
            {
                var button = serialized.FindProperty(field)?.objectReferenceValue as Button;
                if (button == null || button.transform.Find("Icon")?.GetComponent<Image>() == null)
                    throw new InvalidOperationException("Catalogue tab/icon is missing: " + field);
                return button;
            }).ToArray();
        }
    }
}

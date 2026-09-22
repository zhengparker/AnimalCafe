using System;
using System.IO;
using System.Linq;
using AnimalCafe.UI.Decoration;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.P8R
{
    /// <summary>Three isolated category frames, with the existing B outline and shadow.
    /// 分类专用底板，不覆盖共享 tab 素材，也不增加 Appearance keys。</summary>
    public static class P8RCategoryTabAssets
    {
        public const string Root = P8RRefinedBAssets.Root + "/CategoryTabs";
        private static readonly string[] States = { "idle", "selected", "unavailable" };
        private static readonly string[] Fields = { "categoryIdleSprite", "categorySelectedSprite", "categoryUnavailableSprite" };
        private static string PathFor(string state) => Root + "/category_tab_" + state + ".png";

        [MenuItem("Tools/AnimalCafe/P8R/Bake Approved Category Tab Frames")]
        public static void BuildApproved()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before category frame authoring.");
            var rendered = States.ToDictionary(PathFor, state =>
            {
                var importer = AssetImporter.GetAtPath(PathFor(state));
                if (importer != null && EditorUtility.IsDirty(importer)) throw new InvalidOperationException("Unsaved category importer: " + PathFor(state));
                var texture = P8RRefinedBAssets.RenderCategoryTab(state);
                try { return texture.EncodeToPNG(); }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            });
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder(P8RRefinedBAssets.Root, "CategoryTabs");
            foreach (var item in rendered)
            {
                if (!File.Exists(item.Key) || !File.ReadAllBytes(item.Key).SequenceEqual(item.Value))
                {
                    File.WriteAllBytes(item.Key, item.Value);
                    AssetDatabase.ImportAsset(item.Key, ImportAssetOptions.ForceSynchronousImport);
                }
                if (AssetImporter.GetAtPath(item.Key) == null) AssetDatabase.ImportAsset(item.Key, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(item.Key);
                if (HasSettings(importer)) continue;
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100; importer.spriteBorder = Vector4.one * 96;
                importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
                importer.sRGBTexture = true; importer.mipmapEnabled = false; importer.isReadable = false;
                importer.filterMode = FilterMode.Bilinear; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 512; importer.npotScale = TextureImporterNPOTScale.None;
                foreach (var platform in new[] { "Standalone", "Android", "iPhone" }) importer.ClearPlatformTextureSettings(platform);
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        private static bool HasSettings(TextureImporter importer)
        {
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            return importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                && importer.spritePixelsPerUnit == 100 && importer.spriteBorder == Vector4.one * 96
                && importer.alphaSource == TextureImporterAlphaSource.FromInput && importer.alphaIsTransparency
                && importer.sRGBTexture && !importer.mipmapEnabled && !importer.isReadable
                && importer.filterMode == FilterMode.Bilinear && importer.textureCompression == TextureImporterCompression.Uncompressed
                && importer.wrapMode == TextureWrapMode.Clamp && importer.maxTextureSize == 512
                && importer.npotScale == TextureImporterNPOTScale.None && settings.spriteMeshType == SpriteMeshType.FullRect
                && new[] { "Standalone", "Android", "iPhone" }.All(p => !importer.GetPlatformTextureSettings(p).overridden);
        }

        internal static bool BindIfAvailable(DecorationModeTabsView tabs)
        {
            var frames = States.Select(s => AssetDatabase.LoadAssetAtPath<Sprite>(PathFor(s))).ToArray();
            if (frames.Any(s => s == null)) return false; // Legacy projects may not have authored category frames yet.
            var serialized = new SerializedObject(tabs);
            for (var i = 0; i < Fields.Length; i++)
            {
                var field = serialized.FindProperty(Fields[i]);
                if (field.objectReferenceValue != frames[i]) field.objectReferenceValue = frames[i];
            }
            var changed = serialized.hasModifiedProperties;
            if (changed) serialized.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }
    }
}

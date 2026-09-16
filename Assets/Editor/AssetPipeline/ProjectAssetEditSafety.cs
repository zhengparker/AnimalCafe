using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.AssetPipeline
{
    internal static class ProjectAssetEditSafety
    {
        internal static bool IsGeneratedDynamicFontAtlas(Object asset)
        {
            // Unity marks its read-only dynamic glyph cache dirty during normal font use.
            // 只识别该 Font 自己生成的 atlas；Font、Importer、Material 和 TMP atlas 仍受保护。
            if (!(asset is Texture2D) || !AssetDatabase.IsSubAsset(asset)
                || (asset.hideFlags & HideFlags.NotEditable) == 0)
                return false;

            var path = AssetDatabase.GetAssetPath(asset);
            var font = AssetDatabase.LoadMainAssetAtPath(path) as Font;
            return font != null && font.dynamic
                && AssetImporter.GetAtPath(path) is TrueTypeFontImporter
                && font.material != null && font.material.mainTexture == asset;
        }
    }
}

using System;
using System.Linq;
using AnimalCafe.UI.P8R;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.P8R
{
    /// <summary>Import the two approved range PNGs and replace their existing cocoa keys only.
    /// 只导入 Whole Room / Single Grid 彩色图，保留其余 Appearance 映射。</summary>
    public static class P8RColoredRangeAssets
    {
        public const string Root = "Assets/UI/P8R/RangeIcons";
        private static readonly string[] Ranges = { "whole_room", "single_grid" };
        private static string PathFor(string range) => Root + "/range_" + range + "_color.png";

        public static string PreferredPathFor(string key)
        {
            var range = Ranges.FirstOrDefault(candidate => key == candidate + "_cocoa");
            return range != null && Ranges.All(candidate =>
                AssetDatabase.LoadAssetAtPath<Sprite>(PathFor(candidate)) != null)
                ? PathFor(range) : null;
        }

        [MenuItem("Tools/AnimalCafe/P8R/Import Approved Colored Ranges")]
        public static void ImportApproved()
        {
            P8RColoredTabAssets.ImportApproved(Ranges.Select(PathFor), "colored range");
        }

        [MenuItem("Tools/AnimalCafe/P8R/Apply Approved Colored Ranges")]
        public static void ApplyApproved()
        {
            var appearance = AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            if (appearance == null) throw new InvalidOperationException("P8R Appearance must already exist.");
            if (EditorUtility.IsDirty(appearance))
                throw new InvalidOperationException("Save or revert P8R Appearance changes before colored range authoring.");
            ValidateEntries(appearance);
            ImportApproved();
            BindAppearance(appearance);
            Debug.Log("P8R colored ranges applied: two existing cocoa keys only; PNG bytes and all other mappings preserved.");
        }

        public static bool BindAppearance(P8RAppearance appearance)
        {
            if (appearance == null) throw new ArgumentNullException(nameof(appearance));
            var entries = ValidateEntries(appearance);
            var sprites = Ranges.ToDictionary(range => range,
                range => AssetDatabase.LoadAssetAtPath<Sprite>(PathFor(range)), StringComparer.Ordinal);
            var missing = sprites.Where(pair => pair.Value == null).Select(pair => pair.Key).FirstOrDefault();
            if (missing != null)
                throw new InvalidOperationException("Colored range import did not produce Sprite: " + missing);
            for (var index = 0; index < entries.arraySize; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                var key = entry.FindPropertyRelative("key").stringValue;
                var range = Ranges.FirstOrDefault(candidate => key == candidate + "_cocoa");
                if (range != null && entry.FindPropertyRelative("value").objectReferenceValue != sprites[range])
                    entry.FindPropertyRelative("value").objectReferenceValue = sprites[range];
            }
            var serialized = entries.serializedObject;
            var changed = serialized.hasModifiedProperties;
            if (changed)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (EditorUtility.IsPersistent(appearance)) AssetDatabase.SaveAssetIfDirty(appearance);
            }
            return changed;
        }

        private static SerializedProperty ValidateEntries(P8RAppearance appearance)
        {
            var entries = new SerializedObject(appearance).FindProperty("sprites");
            var keys = Enumerable.Range(0, entries.arraySize)
                .Select(index => entries.GetArrayElementAtIndex(index).FindPropertyRelative("key").stringValue).ToArray();
            if (keys.Length != 154 || keys.Distinct(StringComparer.Ordinal).Count() != 154
                || Ranges.Any(range => !keys.Contains(range + "_cocoa")))
                throw new InvalidOperationException("Expected the existing 154 unique P8R keys, including both range cocoa keys.");
            return entries;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.UI.P8R;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.P8R
{
    /// <summary>Imports three approved colored actions and rebinds only their existing cocoa keys.
    /// 仅导入三个已批准彩色操作图，并替换 Appearance 中已有的 cocoa key。</summary>
    public static class P8RColoredActionAssets
    {
        public const string Root = "Assets/UI/P8R/ActionIcons";
        private static readonly string[] Actions = { "decorate", "exit", "pickup" };
        private static string PathFor(string action) => Root + "/action_" + action + "_color.png";

        public static string PreferredPathFor(string key)
        {
            var action = Actions.FirstOrDefault(candidate =>
                key == candidate + "_cocoa" || key == "action_" + candidate + "_color");
            return action != null && Actions.All(candidate =>
                AssetDatabase.LoadAssetAtPath<Sprite>(PathFor(candidate)) != null)
                ? PathFor(action) : null;
        }

        [MenuItem("Tools/AnimalCafe/P8R/Import Approved Colored Actions")]
        public static void ImportApproved()
        {
            P8RColoredTabAssets.ImportApproved(Actions.Select(PathFor), "colored action");
        }

        [MenuItem("Tools/AnimalCafe/P8R/Apply Approved Colored Actions")]
        public static void ApplyApproved()
        {
            var appearance = AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            if (appearance == null) throw new InvalidOperationException("P8R Appearance must already exist.");
            if (EditorUtility.IsDirty(appearance))
                throw new InvalidOperationException("Save or revert P8R Appearance changes before colored action authoring.");
            ValidateEntries(appearance);
            ImportApproved();
            BindAppearance(appearance);
            Debug.Log("P8R colored actions applied: three existing cocoa keys only; muted/ivory, PNG bytes, prefabs and scenes unchanged.");
        }

        public static bool BindAppearance(P8RAppearance appearance)
        {
            if (appearance == null) throw new ArgumentNullException(nameof(appearance));
            var entries = ValidateEntries(appearance);
            var sprites = Actions.ToDictionary(action => action,
                action => AssetDatabase.LoadAssetAtPath<Sprite>(PathFor(action)), StringComparer.Ordinal);
            var missing = sprites.Where(pair => pair.Value == null).Select(pair => pair.Key).FirstOrDefault();
            if (missing != null)
                throw new InvalidOperationException("Colored action import did not produce Sprite: " + missing);
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var key = entry.FindPropertyRelative("key").stringValue;
                var action = Actions.FirstOrDefault(candidate => key == candidate + "_cocoa");
                if (action != null && entry.FindPropertyRelative("value").objectReferenceValue != sprites[action])
                    entry.FindPropertyRelative("value").objectReferenceValue = sprites[action];
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
                || Actions.Any(action => !keys.Contains(action + "_cocoa")))
                throw new InvalidOperationException("Expected the existing 154 unique P8R keys, including all three action cocoa keys.");
            return entries;
        }
    }
}

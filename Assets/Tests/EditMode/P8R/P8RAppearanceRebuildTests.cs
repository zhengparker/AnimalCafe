using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.EditorTools.P8R;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RAppearanceRebuildTests
    {
        [Serializable] private sealed class Manifest { public Entry[] files = Array.Empty<Entry>(); }
        [Serializable] private sealed class Entry { public string file = string.Empty; }

        [Test]
        public void BuildAppearance_FromScratchKeeps154UniqueKeysAndPrefersApprovedColoredTabs()
        {
            const string path = P8RFurnitureUiPaths.Appearance;
            var original = AssetDatabase.LoadAssetAtPath<P8RAppearance>(path);
            Assert.That(original, Is.Not.Null, "This integration test preserves the existing authored Appearance.");
            Assert.That(EditorUtility.IsDirty(original), Is.False, "Save Appearance changes before testing a rebuild.");
            var originalGuid = AssetDatabase.AssetPathToGUID(path);
            var originalBytes = File.ReadAllBytes(path);
            var originalMeta = File.ReadAllBytes(path + ".meta");
            var originalJson = EditorJsonUtility.ToJson(original);
            var approved = JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/Editor/P8R/ImportManifest.json"))
                .files.ToDictionary(e => Path.GetFileNameWithoutExtension(e.file),
                    e => P8RFurnitureUiPaths.Root + "/" + e.file, StringComparer.Ordinal);
            Assert.That(approved.Count, Is.EqualTo(154), "Original approved assets must still have unique keys.");
            Assert.That(P8RRefinedBAssets.Keys.Count, Is.EqualTo(22));
            foreach (var key in P8RRefinedBAssets.Keys)
                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(P8RRefinedBAssets.Root + "/" + key + ".png"),
                    Is.Not.Null, "Bake the approved B frame before running this test: " + key);

            var folderName = "__P8RAppearanceRebuild_" + Guid.NewGuid().ToString("N");
            var folder = "Assets/" + folderName;
            var backup = folder + "/" + Path.GetFileName(path);
            var moved = false;
            P8RAppearance created = null;
            string createdGuid = null;
            try
            {
                Assert.That(AssetDatabase.CreateFolder("Assets", folderName), Is.Not.Empty);
                Assert.That(AssetDatabase.MoveAsset(path, backup), Is.Empty);
                moved = true;
                Assert.That(AssetDatabase.LoadAssetAtPath<P8RAppearance>(backup), Is.SameAs(original));

                created = BuildAppearance();
                createdGuid = AssetDatabase.AssetPathToGUID(path);
                Assert.That(created, Is.Not.SameAs(original));
                Assert.That(createdGuid, Is.Not.EqualTo(originalGuid));
                var entries = new SerializedObject(created).FindProperty("sprites");
                Assert.That(entries.arraySize, Is.EqualTo(154), "B adds replacements, not 22 duplicate keys.");
                var actual = Enumerable.Range(0, entries.arraySize).Select(i => entries.GetArrayElementAtIndex(i))
                    .ToDictionary(e => e.FindPropertyRelative("key").stringValue,
                        e => AssetDatabase.GetAssetPath(e.FindPropertyRelative("value").objectReferenceValue),
                        StringComparer.Ordinal);
                Assert.That(actual.Keys, Is.EquivalentTo(approved.Keys));
                foreach (var entry in approved)
                {
                    var colored = new[] { "furniture", "floor", "wall", "wall_decor" }
                        .FirstOrDefault(action => entry.Key == action + "_cocoa");
                    var coloredAction = new[] { "decorate", "exit", "pickup" }
                        .FirstOrDefault(action => entry.Key == action + "_cocoa");
                    var coloredRange = new[] { "whole_room", "single_grid" }
                        .FirstOrDefault(action => entry.Key == action + "_cocoa");
                    var expected = P8RRefinedBAssets.Keys.Contains(entry.Key)
                        ? P8RRefinedBAssets.Root + "/" + entry.Key + ".png"
                        : entry.Value.Contains("/Icons/") || entry.Key.StartsWith("status_")
                            ? P8RRefinedBAssets.Root + "/Icons/" + entry.Key + ".png" : entry.Value;
                    if (colored != null) expected = "Assets/UI/P8R/TabIcons/Outlined/tab_" + colored + "_color.png";
                    if (coloredAction != null) expected = "Assets/UI/P8R/ActionIcons/action_" + coloredAction + "_color.png";
                    if (coloredRange != null) expected = "Assets/UI/P8R/RangeIcons/range_" + coloredRange + "_color.png";
                    Assert.That(actual[entry.Key], Is.EqualTo(expected), entry.Key + " must retain the correct pack.");
                }
                Assert.That(actual.Values.Count(p => p.StartsWith(P8RRefinedBAssets.Root + "/", StringComparison.Ordinal)), Is.EqualTo(111));
                Assert.That(created.IsRefinedB, Is.True, "B sprite mappings must use B's runtime typography/state rules.");
                P8RRefinedBAssets.BindAppearance(created);
                foreach (var action in new[] { "furniture", "floor", "wall", "wall_decor" })
                    Assert.That(AssetDatabase.GetAssetPath(created.Sprite(action + "_cocoa")),
                        Is.EqualTo("Assets/UI/P8R/TabIcons/Outlined/tab_" + action + "_color.png"), "A later B bind must retain approved color art.");
                foreach (var action in new[] { "decorate", "exit", "pickup" })
                    Assert.That(AssetDatabase.GetAssetPath(created.Sprite(action + "_cocoa")),
                        Is.EqualTo("Assets/UI/P8R/ActionIcons/action_" + action + "_color.png"), "A later B bind must retain approved action art.");
                Assert.That(BuildAppearance(), Is.SameAs(created), "An existing Appearance must not be rebuilt again.");
            }
            finally
            {
                // Only remove the asset this invocation returned; unknown targets are never deleted.
                // 只清理本测试拥有的新实例；恢复失败时保留原件备份及路径，禁止继续删目录。
                if (created != null && !string.IsNullOrEmpty(createdGuid))
                {
                    if (createdGuid == originalGuid || AssetDatabase.AssetPathToGUID(path) != createdGuid)
                        throw new InvalidOperationException("Rebuild cleanup target changed. Original retained at " + backup);
                    if (!AssetDatabase.DeleteAsset(path))
                        throw new InvalidOperationException("Could not remove owned test Appearance. Original retained at " + backup);
                }
                if (moved)
                {
                    var error = AssetDatabase.MoveAsset(backup, path);
                    if (!string.IsNullOrEmpty(error))
                        throw new InvalidOperationException("Restore original Appearance from " + backup + ": " + error);
                    moved = false;
                }
                if (AssetDatabase.IsValidFolder(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
                    AssetDatabase.DeleteAsset(folder);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(originalGuid));
                Assert.That(AssetDatabase.LoadAssetAtPath<P8RAppearance>(path), Is.SameAs(original), "Keep existing loaded references intact.");
                Assert.That(EditorJsonUtility.ToJson(original), Is.EqualTo(originalJson), "Original in-memory Appearance changed.");
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(originalBytes));
                Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(originalMeta));
            }
        }

        private static P8RAppearance BuildAppearance()
        {
            var method = typeof(P8RFurnitureUiBuilder).GetMethod("BuildAppearance", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            try { return (P8RAppearance)method.Invoke(null, null); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.EditorTools.P8R;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RRefinedBMigrationTests
    {
        private static readonly string[] Paths = { P8RFurnitureUiPaths.Appearance, P8RFurnitureUiPaths.CataloguePrefab,
            P8RFurnitureUiPaths.ActionPrefab, P8RFurnitureUiPaths.StorePrefab, P8RCompleteUiBuilder.ExitPrefab, "Assets/Scenes/MainCafe.unity" };
        private static void Apply()
        {
            var method = typeof(P8RCompleteUiBuilder).GetMethod("RefreshApprovedRefinedBStyle");
            Assert.That(method, Is.Not.Null, "B migration must preserve existing layout and all four prefab instances.");
            try { method.Invoke(null, null); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RefinedB_StyleOnlyMigrationPreservesLayoutAndRollsBackEveryTarget(bool injectFailure)
        {
            var bytes = Paths.ToDictionary(p => p, File.ReadAllBytes);
            var scene = EditorSceneManager.OpenScene(Paths.Last(), OpenSceneMode.Additive);
            var hook = typeof(P8RCompleteUiBuilder).GetField("AfterSceneStyleForTests", BindingFlags.NonPublic | BindingFlags.Static);
            var appearance = AssetDatabase.LoadAssetAtPath<P8RAppearance>(Paths[0]);
            var oldSprite = appearance.Sprite("confirm_cocoa");
            try
            {
                // Seed one real pre-B mapping; failure must reverse an actual asset mutation, not a no-op.
                // 人为放回一个旧引用，证明失败会恢复真实修改，而不是只测试空事务。
                {
                    var legacy = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/P8R/Icons/confirm_cocoa.png");
                    var serialized = new SerializedObject(appearance); var entries = serialized.FindProperty("sprites");
                    for (var i = 0; i < entries.arraySize; i++)
                        if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue == "confirm_cocoa")
                            entries.GetArrayElementAtIndex(i).FindPropertyRelative("value").objectReferenceValue = legacy;
                    serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(appearance);
                    oldSprite = legacy;
                }
                var beforeApply = Paths.ToDictionary(p => p, File.ReadAllBytes);
                var transforms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<RectTransform>(true)).ToArray();
                var geometry = transforms.ToDictionary(t => t, t => EditorJsonUtility.ToJson(t));
                var objects = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                var images = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Image>(true)).ToArray();
                var imageState = images.ToDictionary(image => image, image => EditorJsonUtility.ToJson(image));
                var selectables = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Selectable>(true)).ToArray();
                var selectableState = selectables.ToDictionary(selectable => selectable,
                    selectable => EditorJsonUtility.ToJson(selectable));
                if (injectFailure)
                {
                    hook.SetValue(null, (Action)(() =>
                    {
                        Assert.That(appearance.Sprite("confirm_cocoa"), Is.Not.SameAs(oldSprite), "Probe must run after an actual mapping change.");
                        throw new InvalidOperationException("B rollback probe");
                    }));
                    Assert.That(Apply, Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo("B rollback probe"));
                    foreach (var entry in beforeApply) Assert.That(File.ReadAllBytes(entry.Key), Is.EqualTo(entry.Value), entry.Key);
                    Assert.That(appearance.Sprite("confirm_cocoa"), Is.SameAs(oldSprite), "Appearance must recover in memory as well as on disk.");
                    foreach (var image in images)
                        Assert.That(EditorJsonUtility.ToJson(image), Is.EqualTo(imageState[image]),
                            image.name + " Image state must recover in memory before the scene is marked clean.");
                    foreach (var selectable in selectables)
                        Assert.That(EditorJsonUtility.ToJson(selectable), Is.EqualTo(selectableState[selectable]),
                            selectable.name + " SpriteState must recover in memory before the scene is marked clean.");
                    foreach (var transform in transforms)
                        Assert.That(EditorJsonUtility.ToJson(transform), Is.EqualTo(geometry[transform]),
                            transform.name + " geometry must recover in memory before the scene is marked clean.");
                    Assert.That(scene.isDirty, Is.False, "Failed styling must not leave the scene dirty.");
                }
                else
                {
                    Apply();
                    Assert.That(AssetDatabase.GetAssetPath(appearance.Sprite("confirm_cocoa")),
                        Is.EqualTo("Assets/UI/P8R/RefinedB/Icons/confirm_cocoa.png"), "An icon-only stale pack must trigger migration.");
                    foreach (var transform in transforms)
                        Assert.That(EditorJsonUtility.ToJson(transform), Is.EqualTo(geometry[transform]), "Do not relayout " + transform.name);
                    Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)), Is.EqualTo(objects));
                    var frames = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Image>(true))
                        .Where(i => i.sprite != null && P8RRefinedBAssets.Keys.Contains(i.sprite.name)).ToArray();
                    Assert.That(frames.Length, Is.GreaterThan(10));
                    foreach (var frame in frames) Assert.That(AssetDatabase.GetAssetPath(frame.sprite), Does.StartWith(P8RRefinedBAssets.Root + "/"), frame.name);
                    var glyphs = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Image>(true))
                        .Where(i => i.sprite != null && P8RRefinedBGlyphs.Keys.Contains(i.sprite.name)).ToArray();
                    Assert.That(glyphs.Length, Is.GreaterThan(8));
                    foreach (var glyph in glyphs) Assert.That(AssetDatabase.GetAssetPath(glyph.sprite),
                        Is.EqualTo(P8RRefinedBGlyphs.PathFor(glyph.sprite.name)), glyph.name + " has stale serialized ink.");
                    // Do not infer coverage from Sprite names: colored tabs deliberately leave the mono family.
                    // 按真实四个按钮检查，防止筛选条件漏掉彩色图，或错误退回单色图。
                    var tabs = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DecorationModeTabsView>(true)).ToArray();
                    Assert.That(tabs, Is.Not.Empty);
                    var tabFields = new[] { "furnitureButton", "floorButton", "wallButton", "wallDecorButton" };
                    var tabActions = new[] { "furniture", "floor", "wall", "wall_decor" };
                    foreach (var tab in tabs)
                    {
                        var serializedTab = new SerializedObject(tab);
                        for (var i = 0; i < tabFields.Length; i++)
                        {
                            var tabButton = serializedTab.FindProperty(tabFields[i]).objectReferenceValue as Button;
                            Assert.That(tabButton, Is.Not.Null, tabFields[i]);
                            var tabIcon = tabButton.transform.Find("Icon").GetComponent<Image>();
                            Assert.That(AssetDatabase.GetAssetPath(tabIcon.sprite),
                                Is.EqualTo("Assets/UI/P8R/TabIcons/Outlined/tab_" + tabActions[i] + "_color.png"), tabFields[i]);
                        }
                    }
                    var after = Paths.ToDictionary(p => p, File.ReadAllBytes);
                    var timestamps = Paths.ToDictionary(p => p, File.GetLastWriteTimeUtc);
                    Apply();
                    foreach (var path in Paths)
                    {
                        Assert.That(File.ReadAllBytes(path), Is.EqualTo(after[path]), "Repeat changes " + path);
                        Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(timestamps[path]), "Repeat writes " + path);
                    }
                }
            }
            finally
            {
                hook.SetValue(null, null);
                EditorSceneManager.CloseScene(scene, true);
                foreach (var entry in bytes) if (!File.ReadAllBytes(entry.Key).SequenceEqual(entry.Value)) File.WriteAllBytes(entry.Key, entry.Value);
                foreach (var path in Paths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
        }
    }
}

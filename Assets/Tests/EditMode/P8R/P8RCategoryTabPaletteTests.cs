using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.P8R;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RCategoryTabPaletteTests
    {
        private const string Frames = "Assets/UI/P8R/RefinedB/CategoryTabs/category_tab_";
        private static readonly string[] States = { "idle", "selected", "unavailable" };
        private static readonly string[] SpriteFields = { "categoryIdleSprite", "categorySelectedSprite", "categoryUnavailableSprite" };
        private static readonly string[] ButtonFields = { "furnitureButton", "floorButton", "wallButton", "wallDecorButton" };
        private readonly List<Object> owned = new();
        [TearDown] public void Cleanup()
        {
            foreach (var value in owned.AsEnumerable().Reverse()) if (value != null) Object.DestroyImmediate(value);
            owned.Clear();
        }

        [TestCase("idle", 227, 227, 211)]
        [TestCase("selected", 191, 198, 163)]
        [TestCase("unavailable", 232, 224, 213)]
        public void CategoryRender_ChangesOnlyApprovedFillAndKeepsBOutlineShadowAndAlpha(string state, int r, int g, int b)
        {
            var method = typeof(P8RRefinedBAssets).GetMethod("RenderCategoryTab", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Category colors must have an isolated B renderer, not overwrite shared tab keys.");
            var category = Own((Texture2D)Invoke(method, state));
            var shared = Own(P8RRefinedBAssets.Render("tab_" + state));
            Assert.That(category.width, Is.EqualTo(384)); Assert.That(category.height, Is.EqualTo(384));
            var actual = category.GetPixels32(); var old = shared.GetPixels32();
            Assert.That(actual.Select(p => p.a), Is.EqualTo(old.Select(p => p.a)), "Keep exact B alpha geometry and soft shadow.");
            Assert.That(actual.Any(p => p.a > 0 && p.a < 255), Is.True, "Edges must retain intermediate alpha, not binary cutouts.");
            Assert.That(actual[196 * 384 + 8], Is.EqualTo(old[196 * 384 + 8]), "Keep the B cocoa outline (warm disabled outline in unavailable).");
            var center = actual[192 * 384 + 192];
            Assert.That((int)center.r, Is.EqualTo(r).Within(1));
            Assert.That((int)center.g, Is.EqualTo(g).Within(1));
            Assert.That((int)center.b, Is.EqualTo(b).Within(1));
            Assert.That(P8RRefinedBAssets.Keys.Count, Is.EqualTo(22));
            Assert.That(P8RRefinedBAssets.ReplacementKeys.Any(k => k.StartsWith("category_tab_")), Is.False);
        }

        [Test]
        public void CategoryStates_UsePrivateFramesAcrossModesDisableAndReenableWithoutChangingSharedTabs()
        {
            var appearance = AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            var originalAppearance = EditorJsonUtility.ToJson(appearance);
            var tabs = CreateTabs(appearance);
            var frames = States.Select(state => SolidSprite("category_tab_" + state)).ToArray();
            var serialized = new SerializedObject(tabs);
            for (var i = 0; i < SpriteFields.Length; i++)
            {
                var field = serialized.FindProperty(SpriteFields[i]);
                Assert.That(field, Is.Not.Null, "Category-only state field is missing: " + SpriteFields[i]);
                field.objectReferenceValue = frames[i];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var buttons = Buttons(tabs);
            var geometry = buttons.Select(b => Geometry((RectTransform)b.transform)).ToArray();
            tabs.gameObject.SetActive(true);
            foreach (var interactable in new[] { true, false, true })
            foreach (DecorationModeKind mode in Enum.GetValues(typeof(DecorationModeKind)))
            {
                foreach (var button in buttons) button.interactable = interactable;
                tabs.SetActive(mode);
                for (var i = 0; i < buttons.Length; i++)
                {
                    var button = buttons[i];
                    Assert.That(button.image.sprite, Is.SameAs(frames[i == (int)mode ? 1 : 0]));
                    Assert.That(button.spriteState.pressedSprite, Is.SameAs(frames[1]));
                    Assert.That(button.spriteState.selectedSprite, Is.SameAs(frames[1]));
                    Assert.That(button.spriteState.disabledSprite, Is.SameAs(frames[2]));
                    Assert.That(button.image.overrideSprite, Is.SameAs(interactable ? button.image.sprite : frames[2]),
                        "The actual SpriteSwap output must use category art, not just its configured state.");
                    Assert.That(button.image.color, Is.EqualTo(Color.white));
                    Assert.That(Geometry((RectTransform)button.transform), Is.EqualTo(geometry[i]));
                }
            }
            tabs.gameObject.SetActive(false); tabs.gameObject.SetActive(true);
            Assert.That(buttons[3].image.sprite, Is.SameAs(frames[1]), "Re-enable must retain the active mode and category palette.");
            var pointer = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
            buttons[0].OnPointerDown(pointer);
            Assert.That(buttons[0].image.overrideSprite, Is.SameAs(frames[1]), "Pressed output uses the dedicated selected frame.");
            buttons[0].OnPointerUp(pointer);
            Assert.That(buttons[0].image.overrideSprite, Is.SameAs(frames[0]));
            // These are the same public state painter used by real speed and floor-range controls.
            // 分类专用引用不能污染速度与范围共用的 Appearance.Tab 路径。
            foreach (var action in new[] { "resume", "fast_forward", "whole_room", "single_grid" })
            {
                var button = CreateButton(action, null);
                appearance.Tab(button, action, false);
                Assert.That(button.image.sprite, Is.SameAs(appearance.Sprite("tab_idle")));
                appearance.Tab(button, action, true);
                Assert.That(button.image.sprite, Is.SameAs(appearance.Sprite("tab_selected")));
                appearance.Tab(button, action, false, true);
                Assert.That(button.image.sprite, Is.SameAs(appearance.Sprite("tab_unavailable")));
            }
            Assert.That(EditorJsonUtility.ToJson(appearance), Is.EqualTo(originalAppearance));
        }

        [Test]
        public void LegacyTabs_WithoutPrivateFramesRetainSharedPaletteFallback()
        {
            var appearance = AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            var tabs = CreateTabs(appearance);
            tabs.SetActive(DecorationModeKind.Floor);
            var buttons = Buttons(tabs);
            for (var i = 0; i < buttons.Length; i++)
            {
                Assert.That(buttons[i].image.sprite, Is.SameAs(appearance.Sprite(i == 1 ? "tab_selected" : "tab_idle")));
                Assert.That(buttons[i].spriteState.disabledSprite, Is.SameAs(appearance.Sprite("tab_unavailable")));
            }
        }

        [Test]
        public void AuthoredCatalogueAndRebuild_KeepDedicatedFramesAndOutlinedIcons()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(P8RFurnitureUiPaths.CataloguePrefab);
            AssertPalette(prefab.GetComponentInChildren<DecorationModeTabsView>(true));
            var copy = Own(Object.Instantiate(prefab));
            var appearance = AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            P8RFurnitureUiBuilder.StyleCatalogue(copy, appearance);
            AssertPalette(copy.GetComponentInChildren<DecorationModeTabsView>(true));
            var actions = new[] { "furniture", "floor", "wall", "wall_decor" };
            var buttons = Buttons(copy.GetComponentInChildren<DecorationModeTabsView>(true));
            for (var i = 0; i < buttons.Length; i++)
            {
                var icon = buttons[i].transform.Find("Icon").GetComponent<Image>();
                Assert.That(AssetDatabase.GetAssetPath(icon.sprite), Is.EqualTo("Assets/UI/P8R/TabIcons/Outlined/tab_" + actions[i] + "_color.png"));
                Assert.That(icon.color, Is.EqualTo(Color.white));
            }
        }

        [Test]
        public void StyleOnlyMigration_UpgradesEarlierColoredSpriteByNameWithoutChangingGeometry()
        {
            var root = Own(new GameObject("Legacy colored tab", typeof(RectTransform), typeof(Image)));
            var image = root.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/P8R/TabIcons/tab_floor_color.png");
            var geometry = Geometry(image.rectTransform);
            var method = typeof(P8RCompleteUiBuilder).GetMethod("StyleBHierarchy", BindingFlags.NonPublic | BindingFlags.Static);
            Invoke(method, root, AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance), true);
            Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo("Assets/UI/P8R/TabIcons/Outlined/tab_floor_color.png"));
            Assert.That(Geometry(image.rectTransform), Is.EqualTo(geometry));
        }

        [Test]
        public void ApplyRepair_AlreadyBoundPaletteStillPersistsAStaleColoredIcon()
        {
            var path = P8RFurnitureUiPaths.CataloguePrefab;
            var original = File.ReadAllBytes(path);
            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(path);
                var tabs = contents.GetComponentInChildren<DecorationModeTabsView>(true);
                AssertPalette(tabs); // This regression starts after the approved initial authoring.
                var icon = Buttons(tabs)[0].transform.Find("Icon").GetComponent<Image>();
                icon.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/P8R/TabIcons/tab_furniture_color.png");
                PrefabUtility.SaveAsPrefabAsset(contents, path, out var seeded);
                Assert.That(seeded, Is.True);
                PrefabUtility.UnloadPrefabContents(contents); contents = null;
                P8RColoredTabAssets.ApplyApproved();
                contents = PrefabUtility.LoadPrefabContents(path);
                icon = Buttons(contents.GetComponentInChildren<DecorationModeTabsView>(true))[0].transform.Find("Icon").GetComponent<Image>();
                Assert.That(AssetDatabase.GetAssetPath(icon.sprite),
                    Is.EqualTo("Assets/UI/P8R/TabIcons/Outlined/tab_furniture_color.png"), "The repaired reference must be persisted, not only changed in memory.");
            }
            finally
            {
                if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                if (!File.ReadAllBytes(path).SequenceEqual(original)) File.WriteAllBytes(path, original);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
        }

        [Test]
        public void ApplyAgain_PreservesBytesAndWriteTimesOfAllSixteenAuthoredTargets()
        {
            var files = new[] { "furniture", "floor", "wall", "wall_decor" }
                .Select(a => "Assets/UI/P8R/TabIcons/Outlined/tab_" + a + "_color.png")
                .Concat(States.Select(s => Frames + s + ".png"))
                .SelectMany(p => new[] { p, p + ".meta" })
                .Concat(new[] { P8RFurnitureUiPaths.Appearance, P8RFurnitureUiPaths.CataloguePrefab }).ToArray();
            Assert.That(files.Length, Is.EqualTo(16));
            var bytes = files.ToDictionary(p => p, File.ReadAllBytes);
            var times = files.ToDictionary(p => p, File.GetLastWriteTimeUtc);
            P8RColoredTabAssets.ApplyApproved();
            foreach (var path in files)
            {
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes[path]), "Repeat apply changes " + path);
                Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(times[path]), "Repeat apply writes " + path);
            }
        }

        private static void AssertPalette(DecorationModeTabsView tabs)
        {
            var serialized = new SerializedObject(tabs);
            for (var i = 0; i < States.Length; i++)
            {
                var field = serialized.FindProperty(SpriteFields[i]);
                Assert.That(field, Is.Not.Null, SpriteFields[i]);
                var sprite = field.objectReferenceValue as Sprite;
                Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(Frames + States[i] + ".png"));
                Assert.That(sprite.name, Is.EqualTo("category_tab_" + States[i]), "Unique names must survive B's name-based mapper.");
                Assert.That(sprite.border, Is.EqualTo(Vector4.one * 96));
            }
        }

        private DecorationModeTabsView CreateTabs(P8RAppearance appearance)
        {
            var root = Own(new GameObject("CategoryTabs", typeof(RectTransform)));
            root.SetActive(false);
            var tabs = root.AddComponent<DecorationModeTabsView>();
            var serialized = new SerializedObject(tabs);
            serialized.FindProperty("appearance").objectReferenceValue = appearance;
            for (var i = 0; i < ButtonFields.Length; i++)
                serialized.FindProperty(ButtonFields[i]).objectReferenceValue = CreateButton(ButtonFields[i], root.transform);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return tabs;
        }
        private Button CreateButton(string name, Transform parent)
        {
            var root = Own(new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)));
            if (parent != null) root.transform.SetParent(parent, false);
            ((RectTransform)root.transform).sizeDelta = new Vector2(220, 96);
            new GameObject("Icon", typeof(RectTransform), typeof(Image)).transform.SetParent(root.transform, false);
            new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).transform.SetParent(root.transform, false);
            return root.GetComponent<Button>();
        }
        private Sprite SolidSprite(string name)
        {
            var texture = Own(new Texture2D(4, 4));
            texture.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray()); texture.Apply();
            var sprite = Own(Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * .5f)); sprite.name = name; return sprite;
        }
        private static Button[] Buttons(DecorationModeTabsView tabs)
        {
            var serialized = new SerializedObject(tabs);
            return ButtonFields.Select(f => (Button)serialized.FindProperty(f).objectReferenceValue).ToArray();
        }
        private T Own<T>(T value) where T : Object { owned.Add(value); return value; }
        private static Vector2[] Geometry(RectTransform rect) => new[] { rect.anchorMin, rect.anchorMax, rect.pivot, rect.anchoredPosition, rect.sizeDelta };
        private static object Invoke(MethodInfo method, params object[] args)
        {
            Assert.That(method, Is.Not.Null);
            try { return method.Invoke(null, args); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
    }
}

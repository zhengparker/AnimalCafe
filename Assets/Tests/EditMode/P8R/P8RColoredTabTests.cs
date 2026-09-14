using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.P8R;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RColoredTabTests
    {
        private const string OriginalRoot = "Assets/UI/P8R/TabIcons";
        private const string Root = OriginalRoot + "/Outlined";
        private static readonly string[] Actions = { "furniture", "floor", "wall", "wall_decor" };
        private static readonly string[] Fields = { "furnitureButton", "floorButton", "wallButton", "wallDecorButton" };
        private readonly List<Object> owned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in owned.AsEnumerable().Reverse()) if (item != null) Object.DestroyImmediate(item);
            owned.Clear();
        }

        [TestCase("furniture")]
        [TestCase("floor")]
        [TestCase("wall")]
        [TestCase("wall_decor")]
        public void ImportedTab_UsesRealAlphaRectWithoutRewritingApprovedPng(string action)
        {
            var import = ImportMethod();
            var path = PathFor(action);
            Assert.That(File.Exists(path), Is.True, "Copy approved artwork before this integration test: " + path);
            var bytes = File.ReadAllBytes(path);
            Invoke(import);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes), "Sprite import must not redraw or resample the PNG.");
            var original = Own(new Texture2D(2, 2, TextureFormat.RGBA32, false));
            Assert.That(original.LoadImage(bytes), Is.True);
            var sourcePixels = original.GetPixels32();
            Assert.That(sourcePixels.Any(p => p.a == 0), Is.True, "The source must have genuinely transparent exterior pixels.");
            Assert.That(sourcePixels.Any(p => p.a >= 240), Is.True, "The source must retain an opaque subject, not faded artwork.");
            Assert.That(HasAntialiasedBoundary(sourcePixels, original.width), Is.True,
                "AA needs real 16..239 edge pixels beside exterior/solid pixels; alpha254 fill is not edge coverage.");
            var bounds = AlphaBounds(sourcePixels, original.width);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null, "The colored tab must be a real imported Sprite.");
            Assert.That(sprite.name, Is.EqualTo("tab_" + action + "_color"));
            Assert.That(sprite.rect, Is.EqualTo(new Rect(bounds.x, bounds.y, bounds.width, bounds.height)));
            Assert.That(sprite.pivot, Is.EqualTo(sprite.rect.size * .5f));
            Assert.That(sprite.texture.width, Is.EqualTo(original.width));
            Assert.That(sprite.texture.height, Is.EqualTo(original.height));
            Assert.That(sprite.border, Is.EqualTo(Vector4.zero));
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(importer.mipmapEnabled, Is.True, "Colored tabs need the same minification protection as mono icons.");
            Assert.That(importer.mipmapFilter, Is.EqualTo(TextureImporterMipFilter.BoxFilter));
            Assert.That(importer.mipMapsPreserveCoverage, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            foreach (var platform in new[] { "Standalone", "Android", "iPhone" })
                Assert.That(importer.GetPlatformTextureSettings(platform).overridden, Is.False, platform);
        }

        [Test]
        public void ImportAgain_PreservesSourceMonoPackAndExistingMetadata()
        {
            var import = ImportMethod();
            Invoke(import);
            var files = Actions.SelectMany(a => new[] { PathFor(a), PathFor(a) + ".meta" })
                .Concat(Directory.GetFiles("Assets/UI/P8R/Icons", "*", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(OriginalRoot, "*", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(P8RRefinedBGlyphs.Root, "*", SearchOption.TopDirectoryOnly)).ToArray();
            var hashes = files.ToDictionary(p => p, Hash);
            var times = files.ToDictionary(p => p, File.GetLastWriteTimeUtc);
            Invoke(import);
            foreach (var path in files)
            {
                Assert.That(Hash(path), Is.EqualTo(hashes[path]), path);
                Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(times[path]), "No-op import must not rewrite " + path);
            }
        }

        [Test]
        public void Appearance_ReplacesOnlyApprovedCocoaKeysAndKeepsMonoVariants()
        {
            var appearance = AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            var entries = new SerializedObject(appearance).FindProperty("sprites");
            var keys = Enumerable.Range(0, entries.arraySize)
                .Select(i => entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue).ToArray();
            Assert.That(keys.Length, Is.EqualTo(154));
            Assert.That(keys.Distinct().Count(), Is.EqualTo(154), "Color art replaces references, never adds duplicate keys.");
            foreach (var key in P8RRefinedBGlyphs.Keys)
            {
                var action = Actions.FirstOrDefault(a => key == a + "_cocoa");
                var coloredAction = new[] { "decorate", "exit", "pickup" }
                    .FirstOrDefault(candidate => key == candidate + "_cocoa");
                var coloredRange = new[] { "whole_room", "single_grid" }
                    .FirstOrDefault(candidate => key == candidate + "_cocoa");
                var expected = action != null ? PathFor(action) : coloredAction != null
                    ? "Assets/UI/P8R/ActionIcons/action_" + coloredAction + "_color.png"
                    : coloredRange != null ? "Assets/UI/P8R/RangeIcons/range_" + coloredRange + "_color.png"
                    : P8RRefinedBGlyphs.Root + "/" + key + ".png";
                Assert.That(AssetDatabase.GetAssetPath(appearance.Sprite(key)), Is.EqualTo(expected), key);
                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(P8RRefinedBGlyphs.Root + "/" + key + ".png"),
                    Is.Not.Null, "The original 98-glyph family remains available: " + key);
            }
        }

        [Test]
        public void ModeChanges_KeepFourTabsIconOnlyWithWhiteTintAndUnchangedButtonsAndFonts()
        {
            var sourceHashes = Actions.ToDictionary(PathFor, a => Hash(PathFor(a)));
            var appearance = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance)));
            var sprites = Actions.Select(ColoredSprite).ToArray();
            var serialized = new SerializedObject(appearance);
            var entries = serialized.FindProperty("sprites");
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var index = Array.FindIndex(Actions, a => entry.FindPropertyRelative("key").stringValue == a + "_cocoa");
                if (index >= 0) entry.FindPropertyRelative("value").objectReferenceValue = sprites[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var root = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(P8RFurnitureUiPaths.CataloguePrefab)));
            var tabs = root.GetComponentInChildren<DecorationModeTabsView>(true);
            var tabState = new SerializedObject(tabs);
            tabState.FindProperty("appearance").objectReferenceValue = appearance;
            tabState.ApplyModifiedPropertiesWithoutUndo();
            var buttons = Fields.Select(f => Field<Button>(tabs, f)).ToArray();
            var rects = buttons.Select(b => RectGeometry((RectTransform)b.transform)).ToArray();
            var labels = buttons.Select(b => b.transform.Find("Label").GetComponent<TMP_Text>()).ToArray();
            var fonts = labels.Select(l => l.font).ToArray();
            var fontSizes = labels.Select(l => l.fontSize).ToArray();
            foreach (var interactable in new[] { true, false, true })
            foreach (DecorationModeKind mode in Enum.GetValues(typeof(DecorationModeKind)))
            {
                foreach (var button in buttons) button.interactable = interactable;
                tabs.SetActive(mode);
                for (var i = 0; i < buttons.Length; i++)
                {
                    var button = buttons[i];
                    var icon = button.transform.Find("Icon").GetComponent<Image>();
                    Assert.That(icon.sprite, Is.SameAs(sprites[i]), "State changes must not restore mono art: " + Actions[i]);
                    Assert.That(icon.color, Is.EqualTo(Color.white));
                    Assert.That(icon.canvasRenderer.GetColor(), Is.EqualTo(Color.white));
                    Assert.That(button.transition, Is.EqualTo(Selectable.Transition.SpriteSwap));
                    Assert.That(RectGeometry((RectTransform)button.transform), Is.EqualTo(rects[i]));
                    Assert.That(labels[i].font, Is.SameAs(fonts[i]));
                    Assert.That(labels[i].fontSize, Is.EqualTo(fontSizes[i]));
                    Assert.That(labels[i].gameObject.activeSelf, Is.False, "Mode/disabled changes must not revive " + Actions[i] + " text.");
                    var height = ((RectTransform)button.transform).rect.height;
                    // 28 logical = 84 authoring units; these wide tabs clamp against two 8-unit margins.
                    AssertIconOnlyGeometry(button, Mathf.Min(28f * 3f, height - 16f));
                }
            }
            foreach (var source in sourceHashes) Assert.That(Hash(source.Key), Is.EqualTo(source.Value), source.Key);
        }

        [Test]
        public void CroppedColorTabs_RepeatedLayoutAndResizeKeepCenteredLargerInkWithoutRevivingLabels()
        {
            foreach (var action in Actions)
            {
                var button = CreateTab(ColoredSprite(action), new Vector2(220, 80));
                var rect = (RectTransform)button.transform;
                var label = button.transform.Find("Label").GetComponent<TMP_Text>();
                var font = label.font;
                Assert.That(label.gameObject.activeSelf, Is.True, "The fixture starts with a visible legacy label.");
                foreach (var height in new[] { 80f, 96f, 80f })
                {
                    rect.sizeDelta = new Vector2(220, height);
                    var geometry = RectGeometry(rect);
                    // The 84-unit logical target is height-limited: 80 - 16 = 64, 96 - 16 = 80.
                    var extent = height == 80 ? 64f : 80f;
                    for (var repeat = 0; repeat < 2; repeat++)
                    {
                        P8RButtonLayout.StackedButton(button);
                        Assert.That(label.gameObject.activeSelf, Is.False, action + ": repeated layout and resize keep text hidden.");
                        AssertIconOnlyGeometry(button, extent);
                        Assert.That(RectGeometry(rect), Is.EqualTo(geometry), "The hit area must not grow with the artwork.");
                        Assert.That(label.font, Is.SameAs(font));
                        Assert.That(label.fontSize, Is.EqualTo(26));
                    }
                }
            }
        }

        [Test]
        public void NarrowColoredTab_ClampsToEightUnitPaddingWithoutStretching()
        {
            var button = CreateTab(ColoredSprite("floor"), new Vector2(36, 80));
            var geometry = RectGeometry((RectTransform)button.transform);
            P8RButtonLayout.StackedButton(button);
            // 80x120 ink inside 36x80 button: width has only 20 units after two 8-unit margins.
            // 用独立尺寸算期望：20x30，不能只钳制高度或把图拉宽。
            AssertIconOnlyGeometry(button, 30f);
            var icon = button.transform.Find("Icon").GetComponent<Image>();
            Assert.That(icon.rectTransform.rect.width, Is.EqualTo(20f).Within(.001f));
            Assert.That(button.transform.Find("Label").gameObject.activeSelf, Is.False);
            Assert.That(RectGeometry((RectTransform)button.transform), Is.EqualTo(geometry));
        }

        [Test]
        public void MonoStackedButton_KeepsVisibleLabelAndOriginalInkSizeAndPlacement()
        {
            var path = P8RRefinedBGlyphs.Root + "/floor_cocoa.png";
            var source = Own(new Texture2D(2, 2, TextureFormat.RGBA32, false));
            Assert.That(source.LoadImage(File.ReadAllBytes(path)), Is.True);
            var bounds = AlphaBounds(source.GetPixels32(), source.width);
            var button = CreateTab(AssetDatabase.LoadAssetAtPath<Sprite>(path), new Vector2(220, 80));
            var rect = (RectTransform)button.transform;
            var icon = button.transform.Find("Icon").GetComponent<Image>();
            var label = button.transform.Find("Label").GetComponent<TMP_Text>();
            var font = label.font;
            foreach (var height in new[] { 80f, 96f })
            {
                rect.sizeDelta = new Vector2(220, height);
                var geometry = RectGeometry(rect);
                P8RButtonLayout.StackedButton(button);
                var size = icon.rectTransform.rect.size;
                var inkSize = Vector2.Scale(size, new Vector2((float)bounds.width / source.width, (float)bounds.height / source.height));
                var inkCenter = icon.rectTransform.anchoredPosition + Vector2.Scale(size,
                    new Vector2(bounds.center.x / source.width - .5f, bounds.center.y / source.height - .5f));
                Assert.That(Mathf.Max(inkSize.x, inkSize.y), Is.EqualTo(height == 80 ? 32f : 40f).Within(.001f));
                Assert.That(inkCenter.x, Is.EqualTo(0).Within(.016f));
                Assert.That(inkCenter.y, Is.EqualTo(height * .2f).Within(.016f));
                Assert.That(label.gameObject.activeSelf, Is.True, "Icon-only mode must not spill into original mono buttons.");
                Assert.That(label.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(0, -height * .25f)));
                Assert.That(label.rectTransform.sizeDelta, Is.EqualTo(new Vector2(208, 40)));
                Assert.That(label.font, Is.SameAs(font)); Assert.That(label.fontSize, Is.EqualTo(26));
                Assert.That(RectGeometry(rect), Is.EqualTo(geometry));
            }
        }

        private Button CreateTab(Sprite sprite, Vector2 size)
        {
            var root = Own(new GameObject("Tab", typeof(RectTransform), typeof(Image), typeof(Button)));
            ((RectTransform)root.transform).sizeDelta = size;
            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(root.transform, false);
            var icon = iconObject.GetComponent<Image>(); icon.preserveAspect = true; icon.sprite = sprite;
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(root.transform, false);
            labelObject.GetComponent<TMP_Text>().fontSize = 26;
            return root.GetComponent<Button>();
        }

        private static void AssertIconOnlyGeometry(Button button, float extent)
        {
            var icon = button.transform.Find("Icon").GetComponent<Image>();
            var rect = icon.rectTransform;
            var size = rect.rect.size;
            var buttonSize = ((RectTransform)button.transform).rect.size;
            // Fixture ink fills its independently created 80x120 cropped Sprite rect.
            // 验证实际 Image 的可见边界，不引用 production 尺寸常量。
            Assert.That(Mathf.Max(size.x, size.y), Is.EqualTo(extent).Within(.001f), button.name + " visible extent");
            Assert.That(size.x / size.y, Is.EqualTo(icon.sprite.rect.width / icon.sprite.rect.height).Within(.0001f), "No aspect stretching.");
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.one * .5f));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one * .5f));
            Assert.That(rect.pivot, Is.EqualTo(Vector2.one * .5f));
            Assert.That(rect.anchoredPosition.x, Is.EqualTo(0).Within(.016f));
            Assert.That(rect.anchoredPosition.y, Is.EqualTo(0).Within(.016f));
            Assert.That((buttonSize.x - size.x) * .5f, Is.GreaterThanOrEqualTo(7.999f), "Horizontal 8-unit padding.");
            Assert.That((buttonSize.y - size.y) * .5f, Is.GreaterThanOrEqualTo(7.999f), "Vertical 8-unit padding.");
        }

        private Sprite ColoredSprite(string action)
        {
            var texture = Own(new Texture2D(120, 160, TextureFormat.RGBA32, false));
            var pixels = new Color32[120 * 160];
            for (var y = 20; y < 140; y++) for (var x = 20; x < 100; x++)
                pixels[y * 120 + x] = new Color32(60, 155, 135, 255);
            texture.SetPixels32(pixels); texture.Apply(false, false);
            var sprite = Own(Sprite.Create(texture, new Rect(20, 20, 80, 120), Vector2.one * .5f, 100));
            sprite.name = "tab_" + action + "_color";
            return sprite;
        }

        private static Vector2[] RectGeometry(RectTransform rect) =>
            new[] { rect.anchorMin, rect.anchorMax, rect.pivot, rect.anchoredPosition, rect.sizeDelta };
        private T Own<T>(T item) where T : Object { owned.Add(item); return item; }
        private static T Field<T>(object target, string name) => (T)target.GetType()
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
        private static string PathFor(string action) => Root + "/tab_" + action + "_color.png";
        private static MethodInfo ImportMethod()
        {
            var type = typeof(P8RFurnitureUiBuilder).Assembly.GetType("AnimalCafe.EditorTools.P8R.P8RColoredTabAssets");
            Assert.That(type, Is.Not.Null, "Four colored tabs need their dedicated byte-preserving importer.");
            var method = type.GetMethod("ImportApproved", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return method;
        }
        private static void Invoke(MethodInfo method)
        {
            try { method.Invoke(null, null); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        private static RectInt AlphaBounds(Color32[] pixels, int width)
        {
            var ink = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].a >= 16).ToArray();
            Assert.That(ink, Is.Not.Empty);
            return new RectInt(ink.Min(i => i % width), ink.Min(i => i / width),
                ink.Max(i => i % width) - ink.Min(i => i % width) + 1,
                ink.Max(i => i / width) - ink.Min(i => i / width) + 1);
        }
        private static bool HasAntialiasedBoundary(Color32[] pixels, int width)
        {
            var height = pixels.Length / width;
            for (var y = 1; y < height - 1; y++) for (var x = 1; x < width - 1; x++)
            {
                var index = y * width + x;
                if (pixels[index].a < 16 || pixels[index].a >= 240) continue;
                foreach (var neighbor in new[] { index - 1, index + 1, index - width, index + width })
                    if (pixels[neighbor].a < 16 || pixels[neighbor].a >= 240) return true;
            }
            return false;
        }

        private static string Hash(string path)
        {
            using (var file = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(file));
        }
    }
}

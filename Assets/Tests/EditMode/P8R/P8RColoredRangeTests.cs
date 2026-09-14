using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using AnimalCafe.EditorTools.P8R;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RColoredRangeTests
    {
        private const string Root = "Assets/UI/P8R/RangeIcons";
        private const string MonoRoot = "Assets/UI/P8R/RefinedB/Icons";
        private static readonly string[] Ranges = { "whole_room", "single_grid" };
        private readonly List<Object> owned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in owned.AsEnumerable().Reverse())
                if (item != null) Object.DestroyImmediate(item);
            owned.Clear();
        }

        [TestCase("whole_room")]
        [TestCase("single_grid")]
        public void ImportedRange_UsesRealAlphaRectAndSmoothSettingsWithoutRewritingPng(string range)
        {
            var import = HelperMethod("ImportApproved");
            var path = PathFor(range);
            Assert.That(File.Exists(path), Is.True, "The approved range PNG must be copied before importing.");
            var bytes = File.ReadAllBytes(path);

            Invoke(import);

            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes), "Import must preserve the approved PNG bytes.");
            var source = Own(new Texture2D(2, 2, TextureFormat.RGBA32, false));
            Assert.That(source.LoadImage(bytes), Is.True);
            var pixels = source.GetPixels32();
            foreach (var index in new[] { 0, source.width - 1, pixels.Length - source.width, pixels.Length - 1 })
                Assert.That(pixels[index].a, Is.Zero, "Source corners must be genuinely transparent.");
            Assert.That(pixels.Any(pixel => pixel.a >= 240), Is.True, "Artwork must have a solid visible subject.");
            Assert.That(HasAntialiasedBoundary(pixels, source.width), Is.True,
                "Real partial alpha must meet transparent or solid pixels at the edge.");
            var bounds = AlphaBounds(pixels, source.width);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.name, Is.EqualTo("range_" + range + "_color"));
            Assert.That(sprite.rect, Is.EqualTo(new Rect(bounds.x, bounds.y, bounds.width, bounds.height)));
            Assert.That(sprite.pivot, Is.EqualTo(sprite.rect.size * .5f));
            Assert.That(sprite.texture.width, Is.EqualTo(source.width));
            Assert.That(sprite.texture.height, Is.EqualTo(source.height));
            Assert.That(sprite.border, Is.EqualTo(Vector4.zero));

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.mipmapFilter, Is.EqualTo(TextureImporterMipFilter.BoxFilter));
            Assert.That(importer.mipMapsPreserveCoverage, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            foreach (var platform in new[] { "Standalone", "Android", "iPhone" })
                Assert.That(importer.GetPlatformTextureSettings(platform).overridden, Is.False, platform);
        }

        [Test]
        public void ImportAgain_PreservesRangeSourcesMetadataAndOriginalMonoVariants()
        {
            var import = HelperMethod("ImportApproved");
            Invoke(import);
            var files = Ranges.SelectMany(range => new[] { PathFor(range), PathFor(range) + ".meta" })
                .Concat(Ranges.SelectMany(range => new[] { "_cocoa", "_ivory", "_muted" }
                    .SelectMany(suffix => new[] { MonoRoot + "/" + range + suffix + ".png",
                        MonoRoot + "/" + range + suffix + ".png.meta" }))).ToArray();
            Assert.That(files.All(File.Exists), Is.True, "Retain the original monochrome range family.");
            var hashes = files.ToDictionary(path => path, Hash);
            var times = files.ToDictionary(path => path, File.GetLastWriteTimeUtc);

            Invoke(import);

            foreach (var path in files)
            {
                Assert.That(Hash(path), Is.EqualTo(hashes[path]), path);
                Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(times[path]), "No-op import must not rewrite " + path);
            }
        }

        [Test]
        public void ScopedBind_ReplacesExactlyTwoCocoaKeysAndPreservesTheOther152Mappings()
        {
            Invoke(HelperMethod("ImportApproved"));
            var appearance = SeedMonoRangeAppearance();
            var before = SpritePaths(appearance);
            var originalB = appearance.IsRefinedB;
            var originalFont = appearance.Font;

            Assert.That(Invoke(HelperMethod("BindAppearance"), appearance), Is.EqualTo(true));

            var after = SpritePaths(appearance);
            Assert.That(after.Count, Is.EqualTo(154));
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
            Assert.That(after.Where(pair => pair.Value != before[pair.Key]).Select(pair => pair.Key),
                Is.EquivalentTo(new[] { "whole_room_cocoa", "single_grid_cocoa" }));
            foreach (var range in Ranges)
                Assert.That(after[range + "_cocoa"], Is.EqualTo(PathFor(range)));
            Assert.That(appearance.IsRefinedB, Is.EqualTo(originalB), "Range art must not change the palette.");
            Assert.That(appearance.Font, Is.SameAs(originalFont));
            var stable = EditorJsonUtility.ToJson(appearance);

            Assert.That(Invoke(HelperMethod("BindAppearance"), appearance), Is.EqualTo(false));
            Assert.That(EditorJsonUtility.ToJson(appearance), Is.EqualTo(stable), "Already-bound Appearance is a no-op.");
        }

        [TestCase("missing")]
        [TestCase("duplicate")]
        [TestCase("extra")]
        public void ScopedBind_RejectsInvalidKeySetBeforeChangingAnyMapping(string corruption)
        {
            var bind = HelperMethod("BindAppearance");
            var appearance = SeedMonoRangeAppearance();
            var serialized = new SerializedObject(appearance);
            var entries = serialized.FindProperty("sprites");
            if (corruption == "extra") entries.InsertArrayElementAtIndex(entries.arraySize);
            else
            {
                var entry = Enumerable.Range(0, entries.arraySize).Select(entries.GetArrayElementAtIndex)
                    .Single(item => item.FindPropertyRelative("key").stringValue == "whole_room_cocoa");
                entry.FindPropertyRelative("key").stringValue = corruption == "duplicate" ? "single_grid_cocoa" : "unexpected_range";
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var before = EditorJsonUtility.ToJson(appearance);

            Assert.Throws<InvalidOperationException>(() => Invoke(bind, appearance));

            Assert.That(EditorJsonUtility.ToJson(appearance), Is.EqualTo(before));
        }

        [Test]
        public void ApplyApproved_RejectsDirtyAppearanceBeforeChangingImportersOrDisk()
        {
            var apply = HelperMethod("ApplyApproved");
            var appearance = AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            Assert.That(EditorUtility.IsDirty(appearance), Is.False, "Fixture requires a clean Appearance.");
            var paths = new[] { P8RFurnitureUiPaths.Appearance }.Concat(Ranges.Select(PathFor))
                .Concat(Ranges.Select(range => PathFor(range) + ".meta")).Where(File.Exists).ToArray();
            var before = paths.ToDictionary(path => path, Hash);
            EditorUtility.SetDirty(appearance);
            try
            {
                Assert.Throws<InvalidOperationException>(() => Invoke(apply));
                Assert.That(EditorUtility.IsDirty(appearance), Is.True, "Do not save a user's pending edits.");
                foreach (var path in paths) Assert.That(Hash(path), Is.EqualTo(before[path]), path);
            }
            finally { EditorUtility.ClearDirty(appearance); }
        }

        [Test]
        public void RefinedBRebinding_KeepsRangeReplacementAndLeavesOtherApprovedPathsUnchanged()
        {
            Invoke(HelperMethod("ImportApproved"));
            var appearance = SeedMonoRangeAppearance();
            var before = SpritePaths(appearance);
            var bindB = typeof(P8RRefinedBAssets).GetMethod("BindAppearance", BindingFlags.NonPublic | BindingFlags.Static);

            Invoke(bindB, appearance);

            var after = SpritePaths(appearance);
            foreach (var pair in before)
            {
                var range = Ranges.FirstOrDefault(candidate => pair.Key == candidate + "_cocoa");
                Assert.That(after[pair.Key], Is.EqualTo(range == null ? pair.Value : PathFor(range)), pair.Key);
            }
            foreach (var range in Ranges)
            {
                Assert.That(P8RRefinedBAssets.PathFor(range + "_cocoa"), Is.EqualTo(PathFor(range)));
                foreach (var suffix in new[] { "_ivory", "_muted" })
                    Assert.That(P8RRefinedBAssets.PathFor(range + suffix), Is.EqualTo(MonoRoot + "/" + range + suffix + ".png"));
            }
            Assert.That(Invoke(bindB, appearance), Is.EqualTo(false), "Rebuild binding is stable on the second pass.");
        }

        [TestCase("whole_room", "Whole Room")]
        [TestCase("single_grid", "Single Grid")]
        public void RangeTabs_KeepReadableLabelsAndCenteredHorizontalCroppedArtwork(string range, string text)
        {
            Invoke(HelperMethod("ImportApproved"));
            var appearance = SeedMonoRangeAppearance();
            Invoke(HelperMethod("BindAppearance"), appearance);
            var button = CreateButton(Vector2.one);
            var metrics = P8RMobileMetrics.For(button);
            ((RectTransform)button.transform).sizeDelta = new Vector2(metrics.Units(200), metrics.Units(56));
            var icon = button.transform.Find("Icon").GetComponent<Image>();
            var label = button.transform.Find("Label").GetComponent<TMP_Text>();
            var buttonGeometry = RectGeometry((RectTransform)button.transform);
            foreach (var selected in new[] { false, true, false })
            {
                appearance.Tab(button, range, selected);
                Assert.That(AssetDatabase.GetAssetPath(icon.sprite), Is.EqualTo(PathFor(range)));
                Assert.That(label.gameObject.activeSelf, Is.True, "Range controls keep their text.");
                Assert.That(label.text, Is.EqualTo(text));
                Assert.That(icon.color, Is.EqualTo(Color.white));
                Assert.That(icon.preserveAspect, Is.True);
                Assert.That(icon.raycastTarget, Is.False);
                var size = icon.rectTransform.rect.size;
                Assert.That(size.x / size.y, Is.EqualTo(icon.sprite.rect.width / icon.sprite.rect.height).Within(.0001f),
                    "Use the cropped Sprite's aspect, not the old square mono canvas.");
                Assert.That(size.x, Is.GreaterThan(0));
                Assert.That(size.y, Is.LessThan(((RectTransform)button.transform).rect.height));
                var iconLeft = icon.rectTransform.anchoredPosition.x - size.x * .5f;
                var iconRight = icon.rectTransform.anchoredPosition.x + size.x * .5f;
                var textWidth = label.GetPreferredValues(label.text).x;
                var textLeft = label.rectTransform.anchoredPosition.x - textWidth * .5f;
                var textRight = label.rectTransform.anchoredPosition.x + textWidth * .5f;
                Assert.That(textLeft - iconRight, Is.GreaterThan(metrics.Units(1)), "Icon and label retain a visible gap.");
                Assert.That((iconLeft + textRight) * .5f, Is.EqualTo(0).Within(.02f), "Center the complete icon/text group.");
                Assert.That(icon.rectTransform.anchoredPosition.y, Is.EqualTo(0).Within(.02f));
                Assert.That(RectGeometry((RectTransform)button.transform), Is.EqualTo(buttonGeometry));
                var iconGeometry = RectGeometry(icon.rectTransform);
                var labelGeometry = RectGeometry(label.rectTransform);
                P8RButtonLayout.TextButton(button);
                Assert.That(RectGeometry(icon.rectTransform), Is.EqualTo(iconGeometry));
                Assert.That(RectGeometry(label.rectTransform), Is.EqualTo(labelGeometry));
            }
        }

        private P8RAppearance SeedMonoRangeAppearance()
        {
            var source = AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            Assert.That(source, Is.Not.Null);
            var appearance = Own(Object.Instantiate(source));
            var serialized = new SerializedObject(appearance);
            var entries = serialized.FindProperty("sprites");
            for (var index = 0; index < entries.arraySize; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                var range = Ranges.FirstOrDefault(candidate => entry.FindPropertyRelative("key").stringValue == candidate + "_cocoa");
                if (range != null) entry.FindPropertyRelative("value").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Sprite>(MonoRoot + "/" + range + "_cocoa.png");
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return appearance;
        }

        private Button CreateButton(Vector2 size)
        {
            var root = Own(new GameObject("RangeButton", typeof(RectTransform), typeof(Image), typeof(Button)));
            ((RectTransform)root.transform).sizeDelta = size;
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(root.transform, false);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(root.transform, false);
            label.GetComponent<TMP_Text>().fontSize = 28;
            return root.GetComponent<Button>();
        }

        private static Dictionary<string, string> SpritePaths(P8RAppearance appearance)
        {
            var entries = new SerializedObject(appearance).FindProperty("sprites");
            Assert.That(entries.arraySize, Is.EqualTo(154));
            return Enumerable.Range(0, entries.arraySize).ToDictionary(
                index => entries.GetArrayElementAtIndex(index).FindPropertyRelative("key").stringValue,
                index => AssetDatabase.GetAssetPath(entries.GetArrayElementAtIndex(index).FindPropertyRelative("value").objectReferenceValue),
                StringComparer.Ordinal);
        }

        private static MethodInfo HelperMethod(string name)
        {
            var type = typeof(P8RFurnitureUiPaths).Assembly.GetType("AnimalCafe.EditorTools.P8R.P8RColoredRangeAssets");
            Assert.That(type, Is.Not.Null, "Two colored ranges need their scoped byte-preserving integration helper.");
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing colored-range helper method: " + name);
            return method;
        }

        private static object Invoke(MethodInfo method, params object[] arguments)
        {
            try { return method.Invoke(null, arguments); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
        }

        private static RectInt AlphaBounds(Color32[] pixels, int width)
        {
            var ink = Enumerable.Range(0, pixels.Length).Where(index => pixels[index].a >= 16).ToArray();
            Assert.That(ink, Is.Not.Empty);
            return new RectInt(ink.Min(index => index % width), ink.Min(index => index / width),
                ink.Max(index => index % width) - ink.Min(index => index % width) + 1,
                ink.Max(index => index / width) - ink.Min(index => index / width) + 1);
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

        private static Vector2[] RectGeometry(RectTransform rect) =>
            new[] { rect.anchorMin, rect.anchorMax, rect.pivot, rect.anchoredPosition, rect.sizeDelta };
        private T Own<T>(T item) where T : Object { owned.Add(item); return item; }
        private static string PathFor(string range) => Root + "/range_" + range + "_color.png";
        private static string Hash(string path)
        {
            using (var file = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(file));
        }
    }
}

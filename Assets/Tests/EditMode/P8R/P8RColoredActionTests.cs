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
    public sealed class P8RColoredActionTests
    {
        private const string Root = "Assets/UI/P8R/ActionIcons";
        private const string MonoRoot = "Assets/UI/P8R/RefinedB/Icons";
        private static readonly string[] Actions = { "decorate", "exit", "pickup" };
        private readonly List<Object> owned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var item in owned.AsEnumerable().Reverse())
                if (item != null) Object.DestroyImmediate(item);
            owned.Clear();
        }

        [TestCase("decorate")]
        [TestCase("exit")]
        [TestCase("pickup")]
        public void ImportedAction_UsesRealAlphaRectAndSmoothUiSettingsWithoutRewritingPng(string action)
        {
            var path = PathFor(action);
            Assert.That(File.Exists(path), Is.True, "Copy the approved action artwork before integration: " + path);
            var bytes = File.ReadAllBytes(path);

            Invoke(HelperMethod("ImportApproved"));

            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes), "Import must preserve the generated PNG bytes exactly.");
            var source = Own(new Texture2D(2, 2, TextureFormat.RGBA32, false));
            Assert.That(source.LoadImage(bytes), Is.True);
            var pixels = source.GetPixels32();
            Assert.That(pixels.Any(pixel => pixel.a == 0), Is.True, "The approved source needs true transparent exterior pixels.");
            Assert.That(pixels.Any(pixel => pixel.a >= 240), Is.True, "The approved source needs solid visible artwork.");
            Assert.That(HasAntialiasedBoundary(pixels, source.width), Is.True,
                "The edge must contain real partial alpha beside transparent or solid pixels.");
            var bounds = AlphaBounds(pixels, source.width);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null, "The approved action must import as a Sprite.");
            Assert.That(sprite.name, Is.EqualTo("action_" + action + "_color"));
            Assert.That(sprite.rect, Is.EqualTo(new Rect(bounds.x, bounds.y, bounds.width, bounds.height)));
            Assert.That(sprite.pivot, Is.EqualTo(sprite.rect.size * .5f));
            Assert.That(sprite.texture.width, Is.EqualTo(source.width));
            Assert.That(sprite.texture.height, Is.EqualTo(source.height));
            Assert.That(sprite.border, Is.EqualTo(Vector4.zero));

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.mipmapFilter, Is.EqualTo(TextureImporterMipFilter.BoxFilter));
            Assert.That(importer.mipMapsPreserveCoverage, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.alphaIsTransparency, Is.True);
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
        public void ImportAgain_PreservesActionSourcesAndEveryOriginalMonoVariant()
        {
            var import = HelperMethod("ImportApproved");
            Invoke(import);
            var files = Actions.SelectMany(action => new[] { PathFor(action), PathFor(action) + ".meta" })
                .Concat(Actions.SelectMany(action => new[] { "_cocoa", "_ivory", "_muted" }
                    .Select(suffix => MonoRoot + "/" + action + suffix + ".png")))
                .ToArray();
            Assert.That(files.All(File.Exists), Is.True, "The integration must retain both generated and monochrome files.");
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
        public void ScopedBind_ReplacesExactlyThreeCocoaKeysAndKeepsAllOther154Mappings()
        {
            Invoke(HelperMethod("ImportApproved"));
            var source = AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            var appearance = Own(Object.Instantiate(source));
            var seeded = new SerializedObject(appearance);
            var seededEntries = seeded.FindProperty("sprites");
            for (var index = 0; index < seededEntries.arraySize; index++)
            {
                var entry = seededEntries.GetArrayElementAtIndex(index);
                var action = Actions.FirstOrDefault(candidate =>
                    entry.FindPropertyRelative("key").stringValue == candidate + "_cocoa");
                if (action != null)
                    entry.FindPropertyRelative("value").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<Sprite>(MonoRoot + "/" + action + "_cocoa.png");
            }
            seeded.ApplyModifiedPropertiesWithoutUndo();
            var before = SpritePaths(appearance);

            Invoke(HelperMethod("BindAppearance"), appearance);

            var after = SpritePaths(appearance);
            Assert.That(after.Count, Is.EqualTo(154));
            Assert.That(after.Keys, Is.EquivalentTo(before.Keys));
            var changed = after.Where(pair => before[pair.Key] != pair.Value).Select(pair => pair.Key).ToArray();
            Assert.That(changed, Is.EquivalentTo(Actions.Select(action => action + "_cocoa")),
                "Colored actions replace only the three approved enabled-state keys.");
            foreach (var action in Actions)
            {
                Assert.That(after[action + "_cocoa"], Is.EqualTo(PathFor(action)));
                Assert.That(after[action + "_ivory"], Is.EqualTo(before[action + "_ivory"]));
                Assert.That(after[action + "_muted"], Is.EqualTo(before[action + "_muted"]));
                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(MonoRoot + "/" + action + "_cocoa.png"), Is.Not.Null,
                    "The original monochrome cocoa file stays available: " + action);
            }
        }

        [Test]
        public void AppearanceButtons_UseTrimmedGeometryWhileKeepingEnterExitIconOnlyAndPickupText()
        {
            Invoke(HelperMethod("ImportApproved"));
            var appearance = Own(Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance)));
            Invoke(HelperMethod("BindAppearance"), appearance);

            foreach (var action in new[] { "decorate", "exit" })
            {
                var button = CreateButton(new Vector2(200, 68));
                var buttonGeometry = RectGeometry((RectTransform)button.transform);
                foreach (var interactable in new[] { true, false, true })
                {
                    button.interactable = interactable;
                    appearance.Button(button, action, iconOnly: true);
                    var icon = button.transform.Find("Icon").GetComponent<Image>();
                    var label = button.transform.Find("Label").GetComponent<TMP_Text>();
                    Assert.That(AssetDatabase.GetAssetPath(icon.sprite), Is.EqualTo(
                        interactable ? PathFor(action) : MonoRoot + "/" + action + "_muted.png"));
                    Assert.That(label.gameObject.activeSelf, Is.False, action + " remains icon-only in every state.");
                    // No-Canvas authoring uses 3 UI units per logical unit; action ink is 24 logical.
                    AssertVisibleInk(icon, 24f * 3f, Vector2.zero);
                    Assert.That(RectGeometry((RectTransform)button.transform), Is.EqualTo(buttonGeometry));
                }
            }

            var pickup = CreateButton(new Vector2(400, 72));
            var pickupGeometry = RectGeometry((RectTransform)pickup.transform);
            foreach (var interactable in new[] { true, false, true })
            {
                pickup.interactable = interactable;
                appearance.Button(pickup, "pickup", "primary");
                var icon = pickup.transform.Find("Icon").GetComponent<Image>();
                var label = pickup.transform.Find("Label").GetComponent<TMP_Text>();
                Assert.That(AssetDatabase.GetAssetPath(icon.sprite), Is.EqualTo(
                    interactable ? PathFor("pickup") : MonoRoot + "/pickup_muted.png"));
                Assert.That(label.gameObject.activeSelf, Is.True, "Pickup keeps its existing readable text.");
                Assert.That(label.text, Is.EqualTo(appearance.Text("action.pickup")));
                AssertVisibleInk(icon, 24f * 3f, null);
                Assert.That(RectGeometry((RectTransform)pickup.transform), Is.EqualTo(pickupGeometry));
            }
        }

        private Button CreateButton(Vector2 size)
        {
            var root = Own(new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button)));
            ((RectTransform)root.transform).sizeDelta = size;
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            icon.transform.SetParent(root.transform, false);
            var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(root.transform, false);
            label.GetComponent<TMP_Text>().fontSize = 28;
            return root.GetComponent<Button>();
        }

        private void AssertVisibleInk(Image icon, float expectedExtent, Vector2? expectedCenter)
        {
            var path = AssetDatabase.GetAssetPath(icon.sprite);
            var texture = Own(new Texture2D(2, 2, TextureFormat.RGBA32, false));
            Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True);
            var bounds = AlphaBounds(texture.GetPixels32(), texture.width);
            var spriteRect = icon.sprite.rect;
            var imageSize = icon.rectTransform.rect.size;
            var inkSize = Vector2.Scale(imageSize, new Vector2(bounds.width / spriteRect.width, bounds.height / spriteRect.height));
            var relativeCenter = new Vector2((bounds.center.x - spriteRect.xMin) / spriteRect.width - .5f,
                (bounds.center.y - spriteRect.yMin) / spriteRect.height - .5f);
            var center = icon.rectTransform.anchoredPosition + Vector2.Scale(imageSize, relativeCenter);
            Assert.That(Mathf.Max(inkSize.x, inkSize.y), Is.EqualTo(expectedExtent).Within(.02f));
            Assert.That(imageSize.x / imageSize.y, Is.EqualTo(spriteRect.width / spriteRect.height).Within(.0001f),
                "The imported action cannot be stretched.");
            if (expectedCenter.HasValue)
            {
                Assert.That(center.x, Is.EqualTo(expectedCenter.Value.x).Within(.02f));
                Assert.That(center.y, Is.EqualTo(expectedCenter.Value.y).Within(.02f));
            }
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
            var type = typeof(P8RFurnitureUiPaths).Assembly.GetType("AnimalCafe.EditorTools.P8R.P8RColoredActionAssets");
            Assert.That(type, Is.Not.Null, "Three colored actions need a dedicated byte-preserving integration helper.");
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Missing colored-action helper method: " + name);
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
        private static string PathFor(string action) => Root + "/action_" + action + "_color.png";
        private static string Hash(string path)
        {
            using (var file = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(file));
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.EditorTools.P8R;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RRefinedBGlyphTests
    {
        private static readonly string[] Actions = {
            "add", "apply_all", "back", "cancel", "catalogue", "chevron_down", "chevron_left",
            "chevron_right", "chevron_up", "clock", "confirm", "decorate", "error", "exit",
            "fast_forward", "floor", "furniture", "info", "lock", "none", "pause", "pickup",
            "resume", "rotate", "single_grid", "store", "undo", "wall", "wall_decor", "warning", "whole_room"
        };
        private static string[] Keys => Actions.SelectMany(a => new[] { a + "_cocoa", a + "_ivory", a + "_muted" })
            .Concat(new[] { "status_info", "status_preview", "status_success", "status_warning", "status_error" }).ToArray();
        private static readonly string[] MatchedActions = { "store", "cancel", "rotate", "confirm" };
        private static string Source(string key) => "Assets/UI/P8R/" + (key.StartsWith("status_") ? "Feedback/" : "Icons/") + key + ".png";
        private static Texture2D Read(string path)
        {
            var result = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.That(result.LoadImage(File.ReadAllBytes(path)), Is.True);
            return result;
        }
        private static Texture2D Render(string key)
        {
            var type = typeof(P8RRefinedBAssets).Assembly.GetType("AnimalCafe.EditorTools.P8R.P8RRefinedBGlyphs");
            Assert.That(type, Is.Not.Null, "The approved stronger glyph pack needs a deterministic source-preserving baker.");
            try { return (Texture2D)type.GetMethod("Render", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { key }); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        private static RectInt Bounds(Color32[] pixels, int size)
        {
            var indices = Enumerable.Range(0, pixels.Length).Where(i => pixels[i].a >= 16).ToArray();
            return new RectInt(indices.Min(i => i % size), indices.Min(i => i / size),
                indices.Max(i => i % size) - indices.Min(i => i % size) + 1,
                indices.Max(i => i / size) - indices.Min(i => i / size) + 1);
        }

        [Test]
        public void RefinedGlyphs_PreserveCanvasColorAndOpticalBoundsWithModestInkOutsideMatchedActions()
        {
            foreach (var key in Actions.Select(a => a + "_cocoa").Concat(Keys.Where(k => k.StartsWith("status_"))))
            {
                var sourceBytes = File.ReadAllBytes(Source(key));
                var source = Read(Source(key));
                Texture2D result = null;
                try
                {
                    result = Render(key);
                    Assert.That(new Vector2Int(result.width, result.height), Is.EqualTo(new Vector2Int(256, 256)), key);
                    var before = source.GetPixels32(); var after = result.GetPixels32();
                    Assert.That(Bounds(after, 256), Is.EqualTo(Bounds(before, 256)), key + ": do not move or enlarge visible ink.");
                    var coverage = after.Sum(p => (double)p.a) / before.Sum(p => (double)p.a);
                    if (!MatchedActions.Any(action => key == action + "_cocoa"))
                        Assert.That(coverage, Is.InRange(1.12, 1.28), key + ": modest strengthening, not a silhouette fill.");
                    var ink = before.OrderByDescending(p => p.a).First();
                    Assert.That(after.Where(p => p.a > 0).All(p => p.r == ink.r && p.g == ink.g && p.b == ink.b), Is.True, key);
                    Assert.That(after.Any(p => p.a > 0 && p.a < 255), Is.True, "Keep antialias coverage.");
                    Assert.That(result.GetPixel(0, 0).a, Is.Zero);
                    Assert.That(File.ReadAllBytes(Source(key)), Is.EqualTo(sourceBytes), "Never write original art.");
                }
                finally { Object.DestroyImmediate(source); if (result != null) Object.DestroyImmediate(result); }
            }
        }

        // Probe real alpha across isolated strokes, away from caps, joins and intersections.
        // 按原图的独立笔画截面测量实际墨迹；能抓到整枚图标加粗却仍线宽不一的回归。
        [TestCase("store", "box side", 57f, 169f, 1f, 0f)]
        [TestCase("store", "box base", 89f, 202f, -.3665f, .9304f)]
        [TestCase("store", "arrow shaft", 128f, 58f, 1f, 0f)]
        [TestCase("cancel", "diagonal", 79f, 80f, .707107f, -.707107f)]
        [TestCase("rotate", "left arc", 44f, 128f, 1f, 0f)]
        [TestCase("rotate", "top arc", 128f, 46f, 0f, 1f)]
        [TestCase("confirm", "diagonal", 168f, 109f, .707107f, .707107f)]
        public void ActionGlyphs_RenderMatchingLocalStrokeWidthAtFloatingButtonSize(
            string action, string segment, float x, float y, float normalX, float normalY)
        {
            var result = Render(action + "_cocoa");
            try
            {
                var pixels = result.GetPixels32();
                var bounds = Bounds(pixels, result.width);
                var sourceWidth = StrokeCoverage(pixels, result.width, new Vector2(x, y),
                    new Vector2(normalX, normalY).normalized);
                var logicalWidth = sourceWidth * 32f / Mathf.Max(bounds.width, bounds.height);
                TestContext.WriteLine($"{action} {segment}: source={sourceWidth:F3}px; floating={logicalWidth:F3} logical units");
                Assert.That(logicalWidth, Is.EqualTo(3.5f).Within(.25f),
                    action + " " + segment + ": match visible line weight at the existing 32-unit optical extent.");
            }
            finally { Object.DestroyImmediate(result); }
        }

        private static float StrokeCoverage(Color32[] pixels, int size, Vector2 point, Vector2 normal)
        {
            const float step = .125f;
            float coverage = 0;
            for (var index = -320; index < 320; index++)
            {
                var sample = point + normal * ((index + .5f) * step);
                // Probe coordinates use PNG top-left origin; Unity's pixel array starts at bottom-left.
                // 坐标来自源 PNG 的左上角，读 Unity 像素时转换 Y 方向。
                var x = Mathf.Clamp(sample.x, 0, size - 1f);
                var y = Mathf.Clamp(size - 1f - sample.y, 0, size - 1f);
                var x0 = Mathf.FloorToInt(x); var y0 = Mathf.FloorToInt(y);
                var x1 = Mathf.Min(x0 + 1, size - 1); var y1 = Mathf.Min(y0 + 1, size - 1);
                var alpha = Mathf.Lerp(
                    Mathf.Lerp(pixels[y0 * size + x0].a, pixels[y0 * size + x1].a, x - x0),
                    Mathf.Lerp(pixels[y1 * size + x0].a, pixels[y1 * size + x1].a, x - x0), y - y0);
                coverage += alpha / 255f * step;
            }
            return coverage;
        }

        [Test]
        public void StrongerGlyphs_ColorVariantsShareGeometryAndBakeDeterministically()
        {
            foreach (var action in Actions)
            {
                var a = Render(action + "_cocoa"); var b = Render(action + "_muted"); var c = Render(action + "_ivory");
                var repeat = Render(action + "_cocoa");
                try
                {
                    Assert.That(b.GetPixels32().Select(p => p.a), Is.EqualTo(a.GetPixels32().Select(p => p.a)), action);
                    Assert.That(c.GetPixels32().Select(p => p.a), Is.EqualTo(a.GetPixels32().Select(p => p.a)), action);
                    Assert.That(repeat.EncodeToPNG(), Is.EqualTo(a.EncodeToPNG()), action);
                }
                finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(c); Object.DestroyImmediate(repeat); }
            }
        }

        [Test]
        public void StrongerGlyphs_AppearanceIncludesEveryColorAndStatusWithOriginalKeySet()
        {
            var appearance = AssetDatabase.LoadAssetAtPath<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            var entries = new SerializedObject(appearance).FindProperty("sprites");
            Assert.That(entries.arraySize, Is.EqualTo(154));
            foreach (var key in Keys)
            {
                var path = "Assets/UI/P8R/RefinedB/Icons/" + key + ".png";
                var coloredTab = new[] { "furniture", "floor", "wall", "wall_decor" }
                    .FirstOrDefault(action => key == action + "_cocoa");
                var coloredAction = new[] { "decorate", "exit", "pickup" }
                    .FirstOrDefault(action => key == action + "_cocoa");
                var coloredRange = new[] { "whole_room", "single_grid" }
                    .FirstOrDefault(action => key == action + "_cocoa");
                var appearancePath = coloredTab != null ? "Assets/UI/P8R/TabIcons/Outlined/tab_" + coloredTab + "_color.png"
                    : coloredAction != null ? "Assets/UI/P8R/ActionIcons/action_" + coloredAction + "_color.png"
                    : coloredRange != null ? "Assets/UI/P8R/RangeIcons/range_" + coloredRange + "_color.png" : path;
                Assert.That(AssetDatabase.GetAssetPath(appearance.Sprite(key)), Is.EqualTo(appearancePath), key);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Trilinear));
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(importer.mipmapEnabled, Is.True);
                Assert.That(importer.mipmapFilter, Is.EqualTo(TextureImporterMipFilter.BoxFilter));
                Assert.That(importer.mipMapsPreserveCoverage, Is.False, "UI uses alpha blending, not an alpha-test cutoff.");
                Assert.That(importer.mipMapBias, Is.Zero);
                Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(path).texture.mipmapCount, Is.EqualTo(9), key);
                Assert.That(importer.spriteBorder, Is.EqualTo(Vector4.zero));
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100));
            }
        }
    }
}

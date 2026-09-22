using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.P8R
{
    /// <summary>Editor-only derivatives of the approved icon family. No runtime effects or source PNG writes.
    /// 保留图形语义、颜色与可见边界；离线匹配动作线宽或增加墨迹覆盖，不改变点击区域。</summary>
    public static class P8RRefinedBGlyphs
    {
        public const string Root = P8RRefinedBAssets.Root + "/Icons";
        private const int Size = 256;
        private static readonly string[] Actions = {
            "add", "apply_all", "back", "cancel", "catalogue", "chevron_down", "chevron_left",
            "chevron_right", "chevron_up", "clock", "confirm", "decorate", "error", "exit",
            "fast_forward", "floor", "furniture", "info", "lock", "none", "pause", "pickup",
            "resume", "rotate", "single_grid", "store", "undo", "wall", "wall_decor", "warning", "whole_room"
        };
        private static readonly string[] GlyphKeys = Actions.SelectMany(a => new[] { a + "_cocoa", a + "_ivory", a + "_muted" })
            .Concat(new[] { "status_info", "status_preview", "status_success", "status_warning", "status_error" }).ToArray();
        private static readonly Dictionary<string, (byte[] source, byte[] result)> Masks = new();
        public static IReadOnlyList<string> Keys => GlyphKeys;
        public static string PathFor(string key) => GlyphKeys.Contains(key) ? Root + "/" + key + ".png" : null;

        [MenuItem("Tools/AnimalCafe/P8R/Apply Approved Stronger B Presentation")]
        public static void BuildApprovedPresentation()
        {
            P8RRefinedBAssets.BuildApproved();
            BuildApproved();
            P8RCompleteUiBuilder.RefreshApprovedRefinedBStyle();
        }

        public static Texture2D Render(string key)
        {
            if (!GlyphKeys.Contains(key)) throw new ArgumentException("Unknown B glyph: " + key, nameof(key));
            var path = "Assets/UI/P8R/" + (key.StartsWith("status_") ? "Feedback/" : "Icons/") + key + ".png";
            var original = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!original.LoadImage(File.ReadAllBytes(path)) || original.width != Size || original.height != Size)
                    throw new InvalidOperationException("Expected an approved 256px glyph: " + key);
                var pixels = original.GetPixels32();
                var alpha = pixels.Select(p => p.a).ToArray();
                var shape = key.Replace("_cocoa", "").Replace("_muted", "").Replace("_ivory", "");
                if (!Masks.TryGetValue(shape, out var cached) || !alpha.SequenceEqual(cached.source))
                {
                    cached = (alpha, IsMatchedAction(shape) ? RenderActionStroke(shape, alpha) : Strengthen(alpha));
                    Masks[shape] = cached;
                }
                var ink = pixels.OrderByDescending(p => p.a).First();
                for (var i = 0; i < pixels.Length; i++)
                    pixels[i] = cached.result[i] == 0 ? new Color32(0, 0, 0, 0) : new Color32(ink.r, ink.g, ink.b, cached.result[i]);
                var result = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                result.SetPixels32(pixels); result.Apply(false, false);
                return result;
            }
            finally { UnityEngine.Object.DestroyImmediate(original); }
        }

        private static RectInt InkBounds(byte[] mask)
        {
            var minX = Size; var minY = Size; var maxX = -1; var maxY = -1;
            for (var y = 0; y < Size; y++) for (var x = 0; x < Size; x++)
                if (mask[y * Size + x] >= 16)
                { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
            if (maxX < 0) throw new InvalidOperationException("Empty icon mask.");
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static bool IsMatchedAction(string shape) =>
            shape == "store" || shape == "cancel" || shape == "rotate" || shape == "confirm";

        private static byte[] RenderActionStroke(string shape, byte[] source)
        {
            var bounds = InkBounds(source);
            // All four float at a 32-unit optical extent. Define width before drawing;
            // fitting a finished bitmap separately on X/Y would change its line weight.
            // 四枚浮动图标共用 3.5 logical 线宽；只内缩端点，不能拉伸成图来凑边界。
            var radius = 3.5f * Math.Max(bounds.width, bounds.height) / 32f * .5f;
            var left = bounds.xMin; var right = bounds.xMax - 1f;
            var top = Size - bounds.yMax; var bottom = Size - bounds.yMin - 1f;
            var lines = new List<(Vector2 start, Vector2 end)>();
            void Line(float ax, float ay, float bx, float by) =>
                lines.Add((new Vector2(ax, ay), new Vector2(bx, by)));

            // These centerlines retain the approved box/down arrow, X, clockwise arrow
            // and check. Coordinates are in the source PNG's top-left convention.
            // 沿用已批准的箱子向下箭头、叉号、顺时针箭头和勾号，不加入新图形。
            switch (shape)
            {
                case "store":
                    Line(128, top + radius, 128, 102);
                    Line(104, 78, 128, 102); Line(128, 102, 151, 78);
                    Line(62, 108, 92, 96); Line(165, 96, 193, 108);
                    Line(left + radius, 134, 62, 108); Line(62, 108, 128, 130);
                    Line(128, 130, 103, 154); Line(103, 154, left + radius, 134);
                    Line(128, 130, 193, 108); Line(193, 108, right - radius, 134);
                    Line(right - radius, 134, 153, 154); Line(153, 154, 128, 130);
                    Line(58, 145, 58, 186); Line(58, 186, 128, bottom - radius);
                    Line(128, bottom - radius, 198, 186); Line(198, 186, 198, 145);
                    Line(128, 130, 128, bottom - radius);
                    break;
                case "cancel":
                    Line(left + radius, top + radius, right - radius, bottom - radius);
                    Line(left + radius, bottom - radius, right - radius, top + radius);
                    break;
                case "confirm":
                    Line(left + radius, 130, 94, bottom - radius);
                    Line(94, bottom - radius, right - radius, top + radius);
                    break;
                case "rotate":
                    var center = new Vector2(127, (top + bottom) * .5f);
                    var arcX = center.x - left - radius;
                    var arcY = (bottom - top) * .5f - radius;
                    Vector2 ArcPoint(float degrees)
                    {
                        var angle = degrees * Mathf.Deg2Rad;
                        return center + new Vector2(Mathf.Cos(angle) * arcX, Mathf.Sin(angle) * arcY);
                    }
                    var previous = ArcPoint(30);
                    // Short segments approximate the existing arc; each uses the same
                    // Euclidean stroke width, including its slightly oval silhouette.
                    for (var degrees = 33; degrees <= 342; degrees += 3)
                    {
                        var next = ArcPoint(degrees);
                        lines.Add((previous, next)); previous = next;
                    }
                    Line(previous.x, previous.y, right - radius, 101);
                    Line(178, 90, right - radius, 101);
                    Line(right - radius, 101, right - radius, 55);
                    break;
                default: throw new ArgumentException("Unknown matched action: " + shape, nameof(shape));
            }

            var mask = new byte[Size * Size];
            var radiusSquared = radius * radius;
            const int samples = 4;
            for (var y = 0; y < Size; y++) for (var x = 0; x < Size; x++)
            {
                var covered = 0;
                for (var sy = 0; sy < samples; sy++) for (var sx = 0; sx < samples; sx++)
                {
                    var point = new Vector2(x + (sx + .5f) / samples - .5f,
                        y + (sy + .5f) / samples - .5f);
                    foreach (var line in lines)
                    {
                        var edge = line.end - line.start;
                        var t = Mathf.Clamp01(Vector2.Dot(point - line.start, edge) / edge.sqrMagnitude);
                        if ((point - line.start - edge * t).sqrMagnitude > radiusSquared) continue;
                        covered++; break;
                    }
                }
                // Union of round-ended segments supplies round joins too. Supersample
                // alpha here; the existing mipmap importer handles small screen sizes.
                // 圆头线段取并集形成圆角；离线覆盖采样保留 AA，屏幕缩小继续交给 mipmap。
                mask[(Size - 1 - y) * Size + x] = (byte)Mathf.RoundToInt(255f * covered / (samples * samples));
            }
            if (InkBounds(mask) != bounds) throw new InvalidOperationException("Action optical bounds changed: " + shape);
            return mask;
        }

        private static float Sample(byte[] mask, float x, float y)
        {
            var x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, Size - 1);
            var y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, Size - 1);
            var x1 = Math.Min(x0 + 1, Size - 1); var y1 = Math.Min(y0 + 1, Size - 1);
            return Mathf.Lerp(Mathf.Lerp(mask[y0 * Size + x0], mask[y0 * Size + x1], x - x0),
                Mathf.Lerp(mask[y1 * Size + x0], mask[y1 * Size + x1], x - x0), y - y0);
        }

        private static byte[] Candidate(byte[] source, int radius)
        {
            var bounds = InkBounds(source);
            var expanded = new byte[source.Length];
            // Circular neighbourhood keeps rounded joins. Work on alpha only, never RGB.
            // 圆形邻域保留圆润转角；随后归回原墨迹范围，避免 icon 变大或偏心。
            for (var y = Math.Max(0, bounds.yMin - radius); y < Math.Min(Size, bounds.yMax + radius); y++)
                for (var x = Math.Max(0, bounds.xMin - radius); x < Math.Min(Size, bounds.xMax + radius); x++)
                {
                    byte value = 0;
                    for (var dy = -radius; dy <= radius; dy++) for (var dx = -radius; dx <= radius; dx++)
                    {
                        if (dx * dx + dy * dy > radius * radius || x + dx < 0 || x + dx >= Size || y + dy < 0 || y + dy >= Size) continue;
                        value = Math.Max(value, source[(y + dy) * Size + x + dx]);
                    }
                    expanded[y * Size + x] = value;
                }
            var outer = InkBounds(expanded);
            var candidate = (byte[])source.Clone();
            for (var y = bounds.yMin; y < bounds.yMax; y++) for (var x = bounds.xMin; x < bounds.xMax; x++)
            {
                var u = outer.xMin + (x - bounds.xMin) * (outer.width - 1f) / (bounds.width - 1);
                var v = outer.yMin + (y - bounds.yMin) * (outer.height - 1f) / (bounds.height - 1);
                var index = y * Size + x;
                candidate[index] = (byte)Math.Max(source[index], Mathf.RoundToInt(Sample(expanded, u, v)));
            }
            return candidate;
        }

        private static byte[] Strengthen(byte[] source)
        {
            var bounds = InkBounds(source);
            // Coverage is a repeatable tuning proxy, not a claim of exact per-segment stroke width.
            // 20% 是整枚图标的墨迹覆盖目标；实际线宽仍需原尺寸截图复核。
            var mass = source.Sum(p => (double)p);
            var previous = source; var previousMass = mass;
            var result = source;
            for (var radius = 1; radius <= 10; radius++)
            {
                var candidate = Candidate(source, radius);
                var candidateMass = candidate.Sum(p => (double)p);
                if (candidateMass >= mass * 1.20)
                {
                    // Blend adjacent radii only: antialias the new edge, never spread a broad translucent halo.
                    var blend = (float)((mass * 1.20 - previousMass) / (candidateMass - previousMass));
                    result = new byte[source.Length];
                    for (var i = 0; i < result.Length; i++)
                        result[i] = (byte)Mathf.RoundToInt(Mathf.Lerp(previous[i], candidate[i], blend));
                    break;
                }
                previous = candidate; previousMass = candidateMass; result = candidate;
            }
            if (InkBounds(result) != bounds) throw new InvalidOperationException("Glyph optical bounds changed.");
            return result;
        }

        [MenuItem("Tools/AnimalCafe/P8R/Bake Approved Stronger B Icons")]
        public static void BuildApproved()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before icon authoring.");
            var rendered = new Dictionary<string, byte[]>();
            foreach (var key in GlyphKeys)
            {
                var path = PathFor(key);
                var importer = AssetImporter.GetAtPath(path);
                if (importer != null && EditorUtility.IsDirty(importer)) throw new InvalidOperationException("Unsaved icon importer: " + path);
                var texture = Render(key);
                try { rendered[path] = texture.EncodeToPNG(); }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            if (!AssetDatabase.IsValidFolder(P8RRefinedBAssets.Root)) AssetDatabase.CreateFolder("Assets/UI/P8R", "RefinedB");
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder(P8RRefinedBAssets.Root, "Icons");
            foreach (var pair in rendered)
            {
                if (!File.Exists(pair.Key) || !File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value))
                {
                    File.WriteAllBytes(pair.Key, pair.Value);
                    AssetDatabase.ImportAsset(pair.Key, ImportAssetOptions.ForceSynchronousImport);
                }
                var importer = (TextureImporter)AssetImporter.GetAtPath(pair.Key);
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                if (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single
                    && importer.spritePixelsPerUnit == 100 && importer.spriteBorder == Vector4.zero
                    && importer.alphaSource == TextureImporterAlphaSource.FromInput && importer.alphaIsTransparency
                    && importer.sRGBTexture && importer.mipmapEnabled && !importer.isReadable
                    && importer.mipmapFilter == TextureImporterMipFilter.BoxFilter && !importer.mipMapsPreserveCoverage
                    && !importer.fadeout && !importer.borderMipmap && importer.mipMapBias == 0 && !importer.streamingMipmaps
                    && importer.filterMode == FilterMode.Trilinear && importer.textureCompression == TextureImporterCompression.Uncompressed
                    && importer.wrapMode == TextureWrapMode.Clamp && importer.maxTextureSize == 256
                    && importer.npotScale == TextureImporterNPOTScale.None && settings.spriteMeshType == SpriteMeshType.FullRect) continue;
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100; importer.spriteBorder = Vector4.zero;
                importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
                // A 256px glyph often covers only 16-40 screen pixels. Prefilter alpha area
                // before minification; a larger PNG or base-level bilinear alone cannot do this.
                // 只给 icon 生成缩图层；保持原 PNG、线条与布局，9-slice 面板不受影响。
                importer.sRGBTexture = true; importer.mipmapEnabled = true; importer.isReadable = false;
                importer.mipmapFilter = TextureImporterMipFilter.BoxFilter; importer.mipMapsPreserveCoverage = false;
                importer.fadeout = false; importer.borderMipmap = false; importer.mipMapBias = 0; importer.streamingMipmaps = false;
                importer.filterMode = FilterMode.Trilinear; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.wrapMode = TextureWrapMode.Clamp; importer.maxTextureSize = 256; importer.npotScale = TextureImporterNPOTScale.None;
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            Debug.Log("P8R stronger B icons baked: " + GlyphKeys.Length + "; originals retained.");
        }
    }
}

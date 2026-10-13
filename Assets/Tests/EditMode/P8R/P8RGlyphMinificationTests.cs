using System;
using System.IO;
using System.Linq;
using AnimalCafe.EditorTools.P8R;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RGlyphMinificationTests
    {
        // Regression: importing only the full-size bitmap produces missed/uneven strokes
        // when the GPU shrinks it. Compare real imported pixels with area coverage.
        // 检查实际 GPU 缩图，而不是只检查源 PNG 有没有半透明像素。
        [TestCase(32)]
        [TestCase(64)]
        public void ImportedGlyphs_ShrinkWithoutLosingSubpixelStrokeCoverage(int size)
        {
            double currentError = 0, legacyError = 0;
            foreach (var key in P8RRefinedBGlyphs.Keys.Where(k => k.EndsWith("_cocoa") || k.StartsWith("status_")))
            {
                var path = P8RRefinedBGlyphs.PathFor(key);
                var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(imported, Is.Not.Null, key);
                var legacy = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(legacy.LoadImage(File.ReadAllBytes(path)), Is.True);
                    legacy.filterMode = FilterMode.Bilinear;
                    legacy.wrapMode = TextureWrapMode.Clamp;
                    var pixels = legacy.GetPixels32();
                    var expected = AreaCoverage(pixels, legacy.width, size);
                    var actual = GpuAlpha(imported, size);
                    var old = GpuAlpha(legacy, size);
                    // Compare the imported base level before blaming mip generation or GPU sampling.
                    // 先核对导入的原始层，区分来源像素、mip 和 GPU 采样差异。
                    var basePixels = GpuAlpha(imported, legacy.width);
                    var sourcePixels = pixels.Select(p => p.a / 255.0).ToArray();
                    if (key == "add_cocoa")
                        WritePixelProbe(path, size, sourcePixels, basePixels,
                            GpuAlpha(legacy, legacy.width), actual, old, expected);
                    TestContext.WriteLine($"{key}: source={legacy.width}x{legacy.height}, imported={imported.width}x{imported.height}, "
                        + $"format={imported.graphicsFormat}, mips={imported.mipmapCount}, "
                        + $"baseError={Error(basePixels, sourcePixels):F6}, mipError={Error(actual, expected):F6}, "
                        + $"legacyError={Error(old, expected):F6}, limit={QualitySettings.globalTextureMipmapLimit}");
                    currentError += Error(actual, expected);
                    legacyError += Error(old, expected);
                    Assert.That(actual.Sum() / expected.Sum(), Is.InRange(.97, 1.03),
                        key + ": antialiasing must preserve ink, not hide it by fading.");
                }
                finally { Object.DestroyImmediate(legacy); }
            }
            TestContext.WriteLine($"{size}px alpha area error: imported={currentError:F6}, unmipped={legacyError:F6}");
            Assert.That(legacyError, Is.GreaterThan(.01), "The fixture must reproduce minification aliasing.");
            Assert.That(currentError, Is.LessThan(legacyError * .5),
                "Display-size area coverage should be substantially closer than unfiltered minification.");
        }

        private static double[] AreaCoverage(Color32[] source, int sourceSize, int size)
        {
            var step = sourceSize / size;
            Assert.That(step * size, Is.EqualTo(sourceSize));
            var result = new double[size * size];
            for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
            {
                double sum = 0;
                for (var sy = 0; sy < step; sy++) for (var sx = 0; sx < step; sx++)
                    sum += source[(y * step + sy) * sourceSize + x * step + sx].a;
                result[y * size + x] = sum / (255 * step * step);
            }
            return result;
        }

        private static double Error(double[] actual, double[] expected) =>
            actual.Select((a, i) => Math.Abs(a - expected[i])).Average();

        private static void WritePixelProbe(string path, int size, double[] source, double[] importedBase,
            double[] legacyBase, double[] importedSmall, double[] legacySmall, double[] expected)
        {
            // One glyph only: binary alpha planes distinguish decoding from GPU copy/resampling.
            // 只保存一个图标的 alpha 数据，定位解码、GPU copy 与缩图的差异。
            var root = $"artifacts/unity/glyph-probe-{size}";
            Directory.CreateDirectory(root);
            File.Copy(path, root + "/source.png", true);
            foreach (var plane in new[] { ("source", source), ("imported-base", importedBase),
                ("legacy-base", legacyBase), ("imported-small", importedSmall),
                ("legacy-small", legacySmall), ("expected-small", expected) })
            {
                using var writer = new BinaryWriter(File.Create(root + "/" + plane.Item1 + ".f32"));
                foreach (var alpha in plane.Item2) writer.Write((float)alpha);
            }
        }

        private static double[] GpuAlpha(Texture texture, int size)
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null),
                "Run this regression with graphics enabled, not -nographics.");
            var target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var before = RenderTexture.active;
            var readback = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Tests/EditMode/P8R/GlyphMipReadback.shader");
            Assert.That(shader, Is.Not.Null);
            var sampling = new Material(shader);
            try
            {
                // Both paths still render on the GPU; only imported glyphs have filtered mip levels.
                // 两条路径都经过真实 GPU；仅导入图标具备预过滤 mip，旧位图仍采样 level 0。
                var mip = Mathf.Min(((Texture2D)texture).mipmapCount - 1,
                    Mathf.Log(texture.width / (float)size, 2));
                sampling.SetFloat("_MipLevel", mip);
                Graphics.Blit(texture, target, sampling);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                readback.Apply(false, false);
                return readback.GetPixels32().Select(p => p.a / 255.0).ToArray();
            }
            finally
            {
                RenderTexture.active = before;
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(sampling);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}

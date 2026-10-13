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
        [TestCase(false)]
        [TestCase(true)]
        public void GpuReadback_PreservesImportedTextureAndExistingDirtyState(bool initiallyDirty)
        {
            var path = P8RRefinedBGlyphs.PathFor("confirm_cocoa");
            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(imported, Is.Not.Null);
            var wasDirty = EditorUtility.IsDirty(imported);
            Assert.That(wasDirty, Is.False, "Fixture requires a clean imported glyph.");
            var serialized = EditorJsonUtility.ToJson(imported);
            var bytes = File.ReadAllBytes(path);
            var anisotropy = imported.anisoLevel;
            try
            {
                // A pre-existing unsaved edit must stay dirty; readback must not hide it.
                // 已有未保存标记必须保留；只读采样也不能把干净资源变成 dirty。
                if (initiallyDirty) EditorUtility.SetDirty(imported);
                Assert.That(GpuAlpha(imported, 32).Length, Is.EqualTo(32 * 32));
                Assert.That(imported.anisoLevel, Is.EqualTo(anisotropy));
                Assert.That(EditorJsonUtility.ToJson(imported), Is.EqualTo(serialized));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
                Assert.That(EditorUtility.IsDirty(imported), Is.EqualTo(initiallyDirty), path);
            }
            finally
            {
                // Remove only this fixture's marker, after verifying the asset is unchanged.
                // 仅清理本测试拥有的标记；不能清除真实未保存的资源修改。
                if (!wasDirty && EditorJsonUtility.ToJson(imported) == serialized)
                    EditorUtility.ClearDirty(imported);
            }
        }

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
                    TestContext.WriteLine($"{key}: source={legacy.width}x{legacy.height}, imported={imported.width}x{imported.height}, "
                        + $"format={imported.graphicsFormat}, mips={imported.mipmapCount}, "
                        + $"baseError={Error(basePixels, sourcePixels):F6}, mipError={Error(actual, expected):F6}, "
                        + $"legacyError={Error(old, expected):F6}, limit={QualitySettings.globalTextureMipmapLimit}");
                    Assert.That(Error(basePixels, sourcePixels), Is.LessThan(1e-6),
                        key + ": a pixel-sized GPU copy must preserve source alpha before testing minification.");
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
            var source = (Texture2D)texture;
            var samplingTexture = new Texture2D(source.width, source.height, source.format, source.mipmapCount,
                !UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(source.graphicsFormat));
            try
            {
                // GPU copy preserves every imported mip without requiring a readable source.
                // 逐层原样复制 GPU 像素；不重新生成 mip，也不改原资源的 Read/Write 设置。
                Graphics.CopyTexture(source, samplingTexture);
                samplingTexture.filterMode = source.filterMode;
                samplingTexture.wrapMode = source.wrapMode;
                Assert.That(samplingTexture.graphicsFormat, Is.EqualTo(source.graphicsFormat));
                Assert.That(samplingTexture.mipmapCount, Is.EqualTo(source.mipmapCount));
                // Flat, pixel-aligned mip coverage must not include an extra anisotropic filter.
                // PC ForceEnable promotes nonzero levels to >=9; control this unrelated sampler state.
                // Restoring a persistent texture's value still leaves its Editor dirty flag set.
                // 在临时副本控制采样；直接改导入纹理，即使恢复数值也会留下 dirty 标记。
                samplingTexture.anisoLevel = 0;
                // Both paths still render on the GPU; only imported glyphs have filtered mip levels.
                // 两条路径都经过真实 GPU；仅导入图标具备预过滤 mip，旧位图仍采样 level 0。
                var mip = Mathf.Min(((Texture2D)samplingTexture).mipmapCount - 1,
                    Mathf.Log(samplingTexture.width / (float)size, 2));
                sampling.SetFloat("_MipLevel", mip);
                Graphics.Blit(samplingTexture, target, sampling);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                readback.Apply(false, false);
                return readback.GetPixels32().Select(p => p.a / 255.0).ToArray();
            }
            finally
            {
                Object.DestroyImmediate(samplingTexture);
                RenderTexture.active = before;
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(sampling);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}

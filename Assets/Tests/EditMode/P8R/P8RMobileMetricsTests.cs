using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AnimalCafe.Tests.P8R
{
    public sealed class P8RMobileMetricsTests
    {
        // Reflection lets the first RED report a missing feature, without breaking compilation.
        // 首次 RED 用 assertion 报告缺失功能；不让整个 Unity test assembly 编译失败。
        private static Type MetricsType
        {
            get
            {
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType("AnimalCafe.UI.P8R.P8RMobileMetrics"))
                    .FirstOrDefault(candidate => candidate != null);
                Assert.That(type, Is.Not.Null, "P8RMobileMetrics must convert platform logical sizes into Canvas units.");
                return type;
            }
        }

        private static object Calculate(Vector2 pixels, Vector2 logical, float scale, string source = "EditorProfile")
        {
            var method = MetricsType.GetMethod("Calculate", BindingFlags.Static | BindingFlags.Public);
            Assert.That(method, Is.Not.Null);
            var sourceType = method.GetParameters()[3].ParameterType;
            return method.Invoke(null, new[] { (object)pixels, logical, scale, Enum.Parse(sourceType, source) });
        }

        private static T Property<T>(object metrics, string name) =>
            (T)MetricsType.GetProperty(name).GetValue(metrics);

        private static float Units(object metrics, float value) =>
            (float)MetricsType.GetMethod("Units").Invoke(metrics, new object[] { value });

        [TestCase(1080, 1920, 360, 640, 1f, 144f)]
        [TestCase(1920, 1080, 640, 360, 1f, 144f)]
        [TestCase(720, 1600, 360, 800, .74535599f, 128.79752f)]
        [TestCase(1600, 720, 800, 360, .74535599f, 128.79752f)]
        [TestCase(1170, 2532, 390, 844, 1.19525973f, 120.47591f)]
        [TestCase(2048, 1536, 1024, 768, 1.23168057f, 77.94229f)]
        public void PlatformViewports_ProduceLiteral48LogicalTargets(
            int width, int height, int logicalWidth, int logicalHeight, float scale, float expectedUnits)
        {
            var metrics = Calculate(new Vector2(width, height), new Vector2(logicalWidth, logicalHeight), scale);
            Assert.That(Units(metrics, 48), Is.EqualTo(expectedUnits).Within(.002f));
            Assert.That(Property<Vector2>(metrics, "LogicalViewport"), Is.EqualTo(new Vector2(logicalWidth, logicalHeight)));
        }

        [Test]
        public void RenderingAtHalfResolution_PreservesLogicalTouchAndFontSizes()
        {
            var full = Calculate(new Vector2(1920, 1080), new Vector2(640, 360), 1f, "Native");
            var half = Calculate(new Vector2(960, 540), new Vector2(640, 360), .5f, "Native");
            Assert.That(Units(full, 48), Is.EqualTo(144f));
            Assert.That(Units(half, 48), Is.EqualTo(144f));
            Assert.That(Units(half, 16), Is.EqualTo(48f));
            Assert.That(Property<float>(half, "PixelsPerLogicalUnit"), Is.EqualTo(1.5f));
            Assert.That(Property<float>(half, "UnitsPerLogicalUnit"), Is.EqualTo(3f));
        }

        [Test]
        public void IPhonePoints_44PointTargetHas132RenderedPixels()
        {
            var metrics = Calculate(new Vector2(1170, 2532), new Vector2(390, 844), 1.19525973f, "Native");
            Assert.That(Units(metrics, 44), Is.EqualTo(110.43625f).Within(.002f));
            Assert.That(Property<float>(metrics, "PixelsPerLogicalUnit"), Is.EqualTo(3f));
        }

        [Test]
        public void SafeAreaInsets_ChangeUsableAreaWithoutChangingDensity()
        {
            var metrics = Calculate(new Vector2(1080, 1920), new Vector2(360, 640), 1f);
            var method = MetricsType.GetMethod("SafeSizeInLogicalUnits");
            Assert.That(method, Is.Not.Null);
            var usable = (Vector2)method.Invoke(metrics, new object[] { new Rect(24, 96, 1032, 1740) });
            Assert.That(usable.x, Is.EqualTo(344f).Within(.001f));
            Assert.That(usable.y, Is.EqualTo(580f).Within(.001f));
            Assert.That(Units(metrics, 48), Is.EqualTo(144f));
        }

        [TestCase("Native")]
        [TestCase("EditorProfile")]
        [TestCase("Estimated")]
        public void MetricProvenance_IsRetainedWithoutPromotingEstimates(string source)
        {
            var metrics = Calculate(new Vector2(1080, 1920), new Vector2(360, 640), 1f, source);
            Assert.That(Property<object>(metrics, "Source").ToString(), Is.EqualTo(source));
        }

        [TestCase(0f, 1920f, 360f, 640f, 1f)]
        [TestCase(1080f, -1f, 360f, 640f, 1f)]
        [TestCase(1080f, 1920f, 0f, 640f, 1f)]
        [TestCase(1080f, 1920f, 360f, float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 1920f, 360f, 640f, 1f)]
        [TestCase(1080f, 1920f, 360f, 640f, 0f)]
        [TestCase(1080f, 1920f, 360f, 640f, float.NaN)]
        public void InvalidCalculationInputs_RejectBeforeProducingBrokenGeometry(
            float width, float height, float logicalWidth, float logicalHeight, float scale)
        {
            // Resolve before Assert.Throws so missing production code remains an ordinary RED.
            var type = MetricsType;
            var error = Assert.Throws<TargetInvocationException>(() =>
                Calculate(new Vector2(width, height), new Vector2(logicalWidth, logicalHeight), scale));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidRequestedSize_RejectsBeforeWritingRectTransform(float size)
        {
            var metrics = Calculate(new Vector2(1080, 1920), new Vector2(360, 640), 1f);
            var error = Assert.Throws<TargetInvocationException>(() => Units(metrics, size));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void ZeroSpacing_IsSupported()
        {
            Assert.That(Units(Calculate(new Vector2(1080, 1920), new Vector2(360, 640), 1f), 0), Is.Zero);
        }

        [Test]
        public void AuthoringWithoutCanvas_UsesDeterministicPhoneGeometryAndEstimatedProvenance()
        {
            var type = MetricsType;
            var profile = type.GetProperty("EditorLogicalViewportOverride");
            var before = profile.GetValue(null);
            var first = new GameObject("Unparented authoring button", typeof(RectTransform));
            var second = new GameObject("Different sized authoring panel", typeof(RectTransform));
            try
            {
                profile.SetValue(null, null);
                ((RectTransform)first.transform).sizeDelta = new Vector2(72, 64);
                ((RectTransform)second.transform).sizeDelta = new Vector2(900, 500);
                foreach (var context in new[] { first.transform, second.transform })
                {
                    var metrics = type.GetMethod("For").Invoke(null, new object[] { context });
                    Assert.That(Property<Vector2>(metrics, "LogicalViewport"), Is.EqualTo(new Vector2(360, 640)));
                    Assert.That(Property<float>(metrics, "UnitsPerLogicalUnit"), Is.EqualTo(3f));
                    Assert.That(Units(metrics, 48), Is.EqualTo(144f));
                    Assert.That(Property<object>(metrics, "Source").ToString(), Is.EqualTo("Estimated"),
                        "Unparented prefab authoring is deterministic geometry, not a measured mobile display.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
                UnityEngine.Object.DestroyImmediate(first);
                profile.SetValue(null, before);
            }
        }

        [Test]
        public void ExplicitEditorProfile_StillOverridesTheNoCanvasAuthoringDefault()
        {
            var type = MetricsType;
            var profile = type.GetProperty("EditorLogicalViewportOverride");
            var before = profile.GetValue(null);
            var pixels = new Vector2(Screen.width, Screen.height);
            Assert.That(pixels.x, Is.GreaterThan(0));
            Assert.That(pixels.y, Is.GreaterThan(0));
            try
            {
                profile.SetValue(null, pixels / 2f);
                var metrics = type.GetMethod("For").Invoke(null, new object[] { null });
                Assert.That(Units(metrics, 48), Is.EqualTo(96f));
                Assert.That(Property<object>(metrics, "Source").ToString(), Is.EqualTo("EditorProfile"));
            }
            finally { profile.SetValue(null, before); }
        }
    }
}

using AnimalCafe.UI.Components;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace AnimalCafe.Tests.Phase5
{
    public sealed class SafeAreaContainerTests
    {
        [Test]
        public void CalculateNormalizedSafeRect_NormalInsets_ReturnsExpectedAnchors()
        {
            var safeArea = new Rect(24f, 96f, 1032f, 1740f);

            var normalized = SafeAreaContainer.CalculateNormalizedSafeRect(
                safeArea,
                new Vector2(1080f, 1920f));

            Assert.That(normalized.xMin, Is.EqualTo(24f / 1080f).Within(0.0001f));
            Assert.That(normalized.yMin, Is.EqualTo(96f / 1920f).Within(0.0001f));
            Assert.That(normalized.xMax, Is.EqualTo(1056f / 1080f).Within(0.0001f));
            Assert.That(normalized.yMax, Is.EqualTo(1836f / 1920f).Within(0.0001f));
        }

        [Test]
        public void CalculateNormalizedSafeRect_ExtremeOutOfBoundsInsets_ClampsWithoutNegativeSize()
        {
            var safeArea = new Rect(-300f, 1900f, 1800f, -400f);

            var normalized = SafeAreaContainer.CalculateNormalizedSafeRect(
                safeArea,
                new Vector2(1080f, 1920f));

            Assert.That(normalized.xMin, Is.InRange(0f, 1f));
            Assert.That(normalized.yMin, Is.InRange(0f, 1f));
            Assert.That(normalized.xMax, Is.InRange(0f, 1f));
            Assert.That(normalized.yMax, Is.InRange(0f, 1f));
            Assert.That(normalized.width, Is.GreaterThanOrEqualTo(0f));
            Assert.That(normalized.height, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void CalculateNormalizedSafeRect_ZeroScreenDimension_ReturnsFullRectFallback()
        {
            var normalized = SafeAreaContainer.CalculateNormalizedSafeRect(
                new Rect(10f, 10f, 50f, 50f),
                Vector2.zero);

            Assert.That(normalized, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
        }

        [TestCase(UiTextStyle.Body, 12f, 16f)]
        [TestCase(UiTextStyle.Label, 10f, 14f)]
        public void ConfigureLocalizedText_LongMixedLabel_WrapsWithoutShrinkingBelowBaseline(
            UiTextStyle style,
            float startingSize,
            float expectedMinimum)
        {
            var gameObject = new GameObject("LocalizedLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            try
            {
                var label = gameObject.GetComponent<TextMeshProUGUI>();
                label.text = "确认 Coffee Order 并保存这个特别长的糖浆口味选择";
                label.fontSize = startingSize;
                label.enableAutoSizing = true;

                SafeAreaContainer.ConfigureLocalizedText(label, style);

                Assert.That(label.enableAutoSizing, Is.False);
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(expectedMinimum));
                Assert.That(label.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
                Assert.That(label.overflowMode, Is.EqualTo(TextOverflowModes.Overflow));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}

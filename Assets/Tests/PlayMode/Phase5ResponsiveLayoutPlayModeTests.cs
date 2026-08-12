using System.Collections;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase5ResponsiveLayoutPlayModeTests
    {
        [UnityTest]
        public IEnumerator PortraitReference_CriticalControlsRemainInsideSafeAreaWithoutOverlap()
        {
            var fixture = CreateFixture(new Vector2(1080f, 1920f));
            try
            {
                fixture.Container.ApplySafeArea(
                    new Rect(24f, 96f, 1032f, 1740f),
                    new Vector2(1080f, 1920f));

                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();

                AssertCriticalControlsInsideAndSeparate(fixture.SafeRect, fixture.Controls);
            }
            finally
            {
                Object.Destroy(fixture.Root);
            }
        }

        [UnityTest]
        public IEnumerator SimulatedTopBottomSideInsets_ApplyExpectedSafeAnchorsAndContainControls()
        {
            var fixture = CreateFixture(new Vector2(2400f, 1080f));
            try
            {
                fixture.Container.ApplySafeArea(
                    new Rect(120f, 72f, 2160f, 936f),
                    new Vector2(2400f, 1080f));

                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();

                Assert.That(fixture.SafeRect.anchorMin, Is.EqualTo(new Vector2(0.05f, 72f / 1080f)));
                Assert.That(fixture.SafeRect.anchorMax, Is.EqualTo(new Vector2(0.95f, 1008f / 1080f)));
                AssertCriticalControlsInsideAndSeparate(fixture.SafeRect, fixture.Controls);
            }
            finally
            {
                Object.Destroy(fixture.Root);
            }
        }

        [UnityTest]
        public IEnumerator SmallerAndTallPortrait_CriticalControlsAdaptWithoutClipping()
        {
            var sizes = new[]
            {
                new Vector2(720f, 1280f),
                new Vector2(1080f, 2400f)
            };

            foreach (var size in sizes)
            {
                var fixture = CreateFixture(size);
                fixture.Container.ApplySafeArea(
                    new Rect(18f, 64f, size.x - 36f, size.y - 128f),
                    size);

                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();

                AssertCriticalControlsInsideAndSeparate(fixture.SafeRect, fixture.Controls);
                Object.Destroy(fixture.Root);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator Landscape_CloseControlRemainsFunctionalAndInsideSafeArea()
        {
            var fixture = CreateFixture(new Vector2(2400f, 1080f));
            try
            {
                fixture.Container.ApplySafeArea(
                    new Rect(96f, 48f, 2208f, 984f),
                    new Vector2(2400f, 1080f));
                var close = fixture.Controls[1].gameObject.AddComponent<Button>();
                var closed = false;
                close.onClick.AddListener(() => closed = true);

                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();

                close.onClick.Invoke();

                Assert.That(closed, Is.True);
                AssertCriticalControlsInsideAndSeparate(fixture.SafeRect, fixture.Controls);
            }
            finally
            {
                Object.Destroy(fixture.Root);
            }
        }

        [UnityTest]
        public IEnumerator LongMixedCjkLatinLabels_ExpandAndWrapWithoutOverlapOrFontShrink()
        {
            var root = new GameObject(
                "LocalizedPanel",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            var theme = ScriptableObject.CreateInstance<AnimalCafeUiTheme>();
            try
            {
                var rootRect = root.GetComponent<RectTransform>();
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                rootRect.sizeDelta = new Vector2(360f, 640f);
                root.GetComponent<VerticalLayoutGroup>().spacing = 16f;
                root.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                theme.Typography = new UiTypographyTokens
                {
                    Body = new UiTextStyleToken(null, 12f, FontStyles.Normal, 0f),
                    Label = new UiTextStyleToken(null, 10f, FontStyles.Normal, 0f)
                };

                var body = CreateLocalizedLabel(rootRect, "Body", UiTextStyle.Body, theme,
                    "今日 Coffee Bean 库存与 syrup 插孔设置，请确认后再保存到这台咖啡机。");
                var label = CreateLocalizedLabel(rootRect, "Label", UiTextStyle.Label, theme,
                    "Confirm Coffee Machine Flavor Selection 确认咖啡机口味选择");

                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
                yield return null;
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);

                var bodyBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rootRect, body.rectTransform);
                var labelBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rootRect, label.rectTransform);
                Assert.That(body.fontSize, Is.GreaterThanOrEqualTo(AnimalCafeUiTheme.MinimumBodyFontSize));
                Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(AnimalCafeUiTheme.MinimumLabelFontSize));
                Assert.That(body.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
                Assert.That(label.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
                Assert.That(bodyBounds.Intersects(labelBounds), Is.False);

                if (!Application.isBatchMode)
                {
                    ScreenCapture.CaptureScreenshot("Logs/evidence/phase5-task7-long-labels.png");
                }
            }
            finally
            {
                Object.Destroy(root);
                Object.Destroy(theme);
            }
        }

        private static LayoutFixture CreateFixture(Vector2 referenceSize)
        {
            var root = new GameObject(
                "ResponsiveCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler));
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = referenceSize;

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceSize;

            var safeObject = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaContainer));
            var safeRect = safeObject.GetComponent<RectTransform>();
            safeRect.SetParent(rootRect, false);
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;

            var controls = new[]
            {
                CreateCriticalControl(safeRect, "Back", new Vector2(0f, 1f), new Vector2(0f, 1f)),
                CreateCriticalControl(safeRect, "Close", new Vector2(1f, 1f), new Vector2(1f, 1f)),
                CreateCriticalControl(safeRect, "Hud", new Vector2(0f, 0f), new Vector2(0f, 0f)),
                CreateCriticalControl(safeRect, "Confirm", new Vector2(1f, 0f), new Vector2(1f, 0f))
            };

            return new LayoutFixture(root, safeRect, safeObject.GetComponent<SafeAreaContainer>(), controls);
        }

        private static RectTransform CreateCriticalControl(
            RectTransform parent,
            string name,
            Vector2 anchor,
            Vector2 pivot)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchor.x < 0.5f
                ? new Vector2(24f, anchor.y < 0.5f ? 24f : -24f)
                : new Vector2(-24f, anchor.y < 0.5f ? 24f : -24f);
            rect.sizeDelta = new Vector2(48f, 48f);
            return rect;
        }

        private static TextMeshProUGUI CreateLocalizedLabel(
            RectTransform parent,
            string name,
            UiTextStyle style,
            AnimalCafeUiTheme theme,
            string text)
        {
            var gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI),
                typeof(ContentSizeFitter),
                typeof(AnimalCafeTextStyle));
            var rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(328f, 48f);

            var label = gameObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            gameObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            gameObject.GetComponent<AnimalCafeTextStyle>().Configure(theme, style, label);
            return label;
        }

        private static void AssertCriticalControlsInsideAndSeparate(
            RectTransform safeRect,
            RectTransform[] controls)
        {
            var safeBounds = new Bounds(safeRect.rect.center, safeRect.rect.size);
            for (var index = 0; index < controls.Length; index++)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(safeRect, controls[index]);
                Assert.That(safeBounds.Contains(bounds.min), Is.True, controls[index].name + " min outside Safe Area");
                Assert.That(safeBounds.Contains(bounds.max), Is.True, controls[index].name + " max outside Safe Area");

                for (var otherIndex = index + 1; otherIndex < controls.Length; otherIndex++)
                {
                    var otherBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                        safeRect,
                        controls[otherIndex]);
                    Assert.That(bounds.Intersects(otherBounds), Is.False,
                        controls[index].name + " overlaps " + controls[otherIndex].name);
                }
            }
        }

        private readonly struct LayoutFixture
        {
            public LayoutFixture(
                GameObject root,
                RectTransform safeRect,
                SafeAreaContainer container,
                RectTransform[] controls)
            {
                Root = root;
                SafeRect = safeRect;
                Container = container;
                Controls = controls;
            }

            public GameObject Root { get; }
            public RectTransform SafeRect { get; }
            public SafeAreaContainer Container { get; }
            public RectTransform[] Controls { get; }
        }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase5ResponsiveLayoutPlayModeTests
    {
        [UnityTest]
        public IEnumerator PortraitReference_RealButtonsStayInsideSafeAreaAndOffsetsAreCleared()
        {
            using var fixture = new ResponsiveFixture(new Vector2(1080f, 1920f));
            fixture.SetNonZeroSafeOffsets();
            fixture.Container.ApplySafeArea(
                new Rect(24f, 96f, 1032f, 1740f),
                new Vector2(1080f, 1920f));

            yield return fixture.UpdateLayout();

            Assert.That(fixture.SafeRect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(fixture.SafeRect.offsetMax, Is.EqualTo(Vector2.zero));
            fixture.AssertViewportAndSafeGeometry(new Rect(24f, 96f, 1032f, 1740f));
            fixture.AssertCriticalButtonsInsideSafeAreaAndSeparate();
        }

        [UnityTest]
        public IEnumerator SmallerAndTallPortrait_RealButtonsAdaptInScreenCoordinates()
        {
            var sizes = new[]
            {
                new Vector2(720f, 1280f),
                new Vector2(1080f, 2400f)
            };

            foreach (var size in sizes)
            {
                using var fixture = new ResponsiveFixture(size);
                fixture.Container.ApplySafeArea(
                    new Rect(18f, 64f, size.x - 36f, size.y - 128f),
                    size);

                yield return fixture.UpdateLayout();

                fixture.AssertViewportAndSafeGeometry(
                    new Rect(18f, 64f, size.x - 36f, size.y - 128f));
                fixture.AssertCriticalButtonsInsideSafeAreaAndSeparate();
            }
        }

        [UnityTest]
        public IEnumerator Landscape_RealVirtualTouchInvokesCloseAndControlRemainsInsideSafeArea()
        {
            using var fixture = new ResponsiveFixture(new Vector2(2400f, 1080f));
            fixture.Container.ApplySafeArea(
                new Rect(96f, 48f, 2208f, 984f),
                new Vector2(2400f, 1080f));

            yield return fixture.UpdateLayout();
            fixture.AssertViewportAndSafeGeometry(new Rect(96f, 48f, 2208f, 984f));
            Assert.That(fixture.TopRaycastTargetAtClose(), Is.SameAs(fixture.Close.gameObject));

            fixture.QueueTouchAtClose(InputTouchPhase.Began);
            yield return null;
            fixture.QueueTouchAtClose(InputTouchPhase.Ended);
            yield return null;
            yield return null;

            Assert.That(fixture.CloseClickCount, Is.EqualTo(1));
            fixture.AssertCriticalButtonsInsideSafeAreaAndSeparate();
        }

        [UnityTest]
        public IEnumerator SimulatedTopBottomSideInsets_UseLiteralAnchorsAndScreenGeometry()
        {
            using var fixture = new ResponsiveFixture(new Vector2(2400f, 1080f));
            fixture.SetNonZeroSafeOffsets();
            fixture.Container.ApplySafeArea(
                new Rect(120f, 72f, 2160f, 936f),
                new Vector2(2400f, 1080f));

            yield return fixture.UpdateLayout();

            Assert.That(fixture.SafeRect.anchorMin.x, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(fixture.SafeRect.anchorMin.y, Is.EqualTo(72f / 1080f).Within(0.0001f));
            Assert.That(fixture.SafeRect.anchorMax.x, Is.EqualTo(0.95f).Within(0.0001f));
            Assert.That(fixture.SafeRect.anchorMax.y, Is.EqualTo(1008f / 1080f).Within(0.0001f));
            Assert.That(fixture.SafeRect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(fixture.SafeRect.offsetMax, Is.EqualTo(Vector2.zero));
            fixture.AssertViewportAndSafeGeometry(new Rect(120f, 72f, 2160f, 936f));
            fixture.AssertCriticalButtonsInsideSafeAreaAndSeparate();
        }

        [UnityTest]
        public IEnumerator LongMixedCjkLatinLabels_HaveApprovedLengthWrapPreferredHeightAndNoLayoutOverlap()
        {
            using var fixture = new LocalizedTextFixture();

            yield return fixture.UpdateLayout();

            fixture.AssertExpandedByThirtyToFiftyPercent();
            fixture.AssertResponsiveTextLayout(fixture.Body, AnimalCafeUiTheme.MinimumBodyFontSize);
            fixture.AssertResponsiveTextLayout(fixture.Label, AnimalCafeUiTheme.MinimumLabelFontSize);

            var bodyBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                fixture.Panel,
                fixture.Body.rectTransform);
            var labelBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                fixture.Panel,
                fixture.Label.rectTransform);
            Assert.That(bodyBounds.Intersects(labelBounds), Is.False);
        }

        [UnityTest]
        public IEnumerator HeadingThemeLayoutBehavior_IsNotRewrittenByLocalizedBodyLabelPolicy()
        {
            var canvasObject = new GameObject(
                "HeadingCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            var gameObject = new GameObject(
                "Heading",
                typeof(RectTransform),
                typeof(TextMeshProUGUI),
                typeof(AnimalCafeTextStyle));
            gameObject.transform.SetParent(canvasObject.transform, false);
            var theme = ScriptableObject.CreateInstance<AnimalCafeUiTheme>();
            try
            {
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                gameObject.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 200f);
                var heading = gameObject.GetComponent<TextMeshProUGUI>();
                heading.text = "Animal Cafe UI Foundation";
                heading.enableAutoSizing = true;
                heading.fontSizeMin = 22f;
                heading.fontSizeMax = 34f;
                heading.textWrappingMode = TextWrappingModes.NoWrap;
                heading.overflowMode = TextOverflowModes.Ellipsis;
                theme.Typography = new UiTypographyTokens
                {
                    Heading = new UiTextStyleToken(null, 28f, FontStyles.Bold, 3f)
                };

                gameObject.GetComponent<AnimalCafeTextStyle>().Configure(theme, UiTextStyle.Heading, heading);
                Assert.That(heading.fontSize, Is.EqualTo(28f));
                Assert.That(heading.enableAutoSizing, Is.True);
                Assert.That(heading.fontSizeMin, Is.EqualTo(22f));
                Assert.That(heading.fontSizeMax, Is.EqualTo(34f));
                Assert.That(heading.textWrappingMode, Is.EqualTo(TextWrappingModes.NoWrap));
                Assert.That(heading.overflowMode, Is.EqualTo(TextOverflowModes.Ellipsis));
                Canvas.ForceUpdateCanvases();
                yield return null;

                Assert.That(heading.preferredWidth, Is.GreaterThan(0f));
            }
            finally
            {
                UnityEngine.Object.Destroy(canvasObject);
                UnityEngine.Object.Destroy(theme);
            }
        }

        private sealed class ResponsiveFixture : IDisposable
        {
            private readonly InputFocusIsolationScope focusScope = new InputFocusIsolationScope();
            private readonly List<GameObject> disabledEventSystems = new List<GameObject>();
            private readonly GameObject eventSystemObject;
            private readonly InputSystemUIInputModule inputModule;
            private readonly Touchscreen touchscreen;
            private readonly Button[] controls;
            private readonly Vector2 viewportSize;

            public ResponsiveFixture(Vector2 referenceResolution)
            {
                viewportSize = referenceResolution;
                DisableExistingEventSystems();

                Root = new GameObject(
                    "ResponsiveCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                Root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = Root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

                var viewportObject = new GameObject("DeterministicViewport", typeof(RectTransform));
                Viewport = viewportObject.GetComponent<RectTransform>();
                Viewport.SetParent(Root.transform, false);
                Viewport.anchorMin = Viewport.anchorMax = new Vector2(0.5f, 0.5f);
                Viewport.pivot = new Vector2(0.5f, 0.5f);
                Viewport.sizeDelta = referenceResolution;
                var fitScale = Mathf.Min(
                    Screen.width / referenceResolution.x,
                    Screen.height / referenceResolution.y);
                Viewport.localScale = Vector3.one * Mathf.Min(1f, fitScale);

                var safeObject = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaContainer));
                SafeRect = safeObject.GetComponent<RectTransform>();
                SafeRect.SetParent(Viewport, false);
                SafeRect.anchorMin = Vector2.zero;
                SafeRect.anchorMax = Vector2.one;
                SafeRect.offsetMin = Vector2.zero;
                SafeRect.offsetMax = Vector2.zero;
                Container = safeObject.GetComponent<SafeAreaContainer>();

                controls = new[]
                {
                    CreateButton("Back", new Vector2(0f, 1f), new Vector2(0f, 1f)),
                    CreateButton("Close", new Vector2(1f, 1f), new Vector2(1f, 1f)),
                    CreateButton("Hud", new Vector2(0f, 0f), new Vector2(0f, 0f)),
                    CreateButton("Confirm", new Vector2(1f, 0f), new Vector2(1f, 0f))
                };
                Close = controls[1];
                Close.onClick.AddListener(() => CloseClickCount++);

                eventSystemObject = new GameObject("ResponsiveEventSystem");
                EventSystem = eventSystemObject.AddComponent<EventSystem>();
                inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
                inputModule.UnassignActions();
                inputModule.AssignDefaultActions();
                touchscreen = InputSystem.AddDevice<Touchscreen>();
                Canvas.ForceUpdateCanvases();
            }

            public GameObject Root { get; }
            public RectTransform Viewport { get; }
            public RectTransform SafeRect { get; }
            public SafeAreaContainer Container { get; }
            public Button Close { get; }
            public EventSystem EventSystem { get; }
            public int CloseClickCount { get; private set; }

            public void SetNonZeroSafeOffsets()
            {
                SafeRect.offsetMin = new Vector2(37f, 29f);
                SafeRect.offsetMax = new Vector2(-41f, -31f);
            }

            public IEnumerator UpdateLayout()
            {
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
            }

            public void QueueTouchAtClose(InputTouchPhase phase)
            {
                InputSystem.QueueStateEvent(
                    touchscreen,
                    new TouchState
                    {
                        touchId = 1,
                        phase = phase,
                        position = GetScreenCenter(Close.transform as RectTransform),
                        pressure = phase == InputTouchPhase.Ended ? 0f : 1f
                    });
            }

            public GameObject TopRaycastTargetAtClose()
            {
                var results = new List<RaycastResult>();
                EventSystem.RaycastAll(
                    new PointerEventData(EventSystem) { position = GetScreenCenter(Close.transform as RectTransform) },
                    results);
                return results.Count > 0 ? results[0].gameObject : null;
            }

            public void AssertCriticalButtonsInsideSafeAreaAndSeparate()
            {
                var safeCorners = GetScreenCorners(SafeRect);
                for (var index = 0; index < controls.Length; index++)
                {
                    var buttonRect = controls[index].transform as RectTransform;
                    var corners = GetScreenCorners(buttonRect);
                    Assert.That(corners[0].x, Is.GreaterThanOrEqualTo(safeCorners[0].x - 0.1f));
                    Assert.That(corners[0].y, Is.GreaterThanOrEqualTo(safeCorners[0].y - 0.1f));
                    Assert.That(corners[2].x, Is.LessThanOrEqualTo(safeCorners[2].x + 0.1f));
                    Assert.That(corners[2].y, Is.LessThanOrEqualTo(safeCorners[2].y + 0.1f));

                    var screenRect = Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
                    for (var other = index + 1; other < controls.Length; other++)
                    {
                        var otherCorners = GetScreenCorners(controls[other].transform as RectTransform);
                        var otherRect = Rect.MinMaxRect(
                            otherCorners[0].x,
                            otherCorners[0].y,
                            otherCorners[2].x,
                            otherCorners[2].y);
                        Assert.That(screenRect.Overlaps(otherRect), Is.False,
                            controls[index].name + " overlaps " + controls[other].name);
                    }
                }
            }

            public void AssertViewportAndSafeGeometry(Rect expectedSafeArea)
            {
                Assert.That(Viewport.rect.width, Is.EqualTo(viewportSize.x).Within(0.01f));
                Assert.That(Viewport.rect.height, Is.EqualTo(viewportSize.y).Within(0.01f));
                Assert.That(
                    Viewport.rect.width / Viewport.rect.height,
                    Is.EqualTo(viewportSize.x / viewportSize.y).Within(0.0001f));

                var safeBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(Viewport, SafeRect);
                var viewportBottomLeft = Viewport.rect.min;
                var actualSafeArea = Rect.MinMaxRect(
                    safeBounds.min.x - viewportBottomLeft.x,
                    safeBounds.min.y - viewportBottomLeft.y,
                    safeBounds.max.x - viewportBottomLeft.x,
                    safeBounds.max.y - viewportBottomLeft.y);
                Assert.That(actualSafeArea.x, Is.EqualTo(expectedSafeArea.x).Within(0.05f));
                Assert.That(actualSafeArea.y, Is.EqualTo(expectedSafeArea.y).Within(0.05f));
                Assert.That(actualSafeArea.width, Is.EqualTo(expectedSafeArea.width).Within(0.05f));
                Assert.That(actualSafeArea.height, Is.EqualTo(expectedSafeArea.height).Within(0.05f));
            }

            public void Dispose()
            {
                inputModule.UnassignActions();
                if (touchscreen.added)
                {
                    InputSystem.RemoveDevice(touchscreen);
                }

                UnityEngine.Object.DestroyImmediate(eventSystemObject);
                UnityEngine.Object.DestroyImmediate(Root);
                foreach (var disabled in disabledEventSystems)
                {
                    if (disabled != null)
                    {
                        disabled.SetActive(true);
                    }
                }

                focusScope.Dispose();
            }

            private Button CreateButton(string name, Vector2 anchor, Vector2 pivot)
            {
                var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                var rect = gameObject.GetComponent<RectTransform>();
                rect.SetParent(SafeRect, false);
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.pivot = pivot;
                rect.anchoredPosition = anchor.x < 0.5f
                    ? new Vector2(24f, anchor.y < 0.5f ? 24f : -24f)
                    : new Vector2(-24f, anchor.y < 0.5f ? 24f : -24f);
                rect.sizeDelta = new Vector2(48f, 48f);
                return gameObject.GetComponent<Button>();
            }

            private void DisableExistingEventSystems()
            {
                foreach (var existing in Resources.FindObjectsOfTypeAll<EventSystem>())
                {
                    if (existing.gameObject.scene.IsValid()
                        && existing.gameObject.scene.isLoaded
                        && existing.gameObject.activeSelf)
                    {
                        disabledEventSystems.Add(existing.gameObject);
                        existing.gameObject.SetActive(false);
                    }
                }
            }
        }

        private sealed class LocalizedTextFixture : IDisposable
        {
            private const string BodyBaseline = "Coffee Bean 库存与 syrup 插孔设置";
            private const string BodyExpanded = "Coffee Bean 库存与 syrup 插孔设置以及口味确认并保存";
            private const string LabelBaseline = "Confirm Coffee Machine 咖啡机";
            private const string LabelExpanded = "Confirm Coffee Machine 咖啡机 Flavor 口味选择";
            private readonly GameObject root;
            private readonly AnimalCafeUiTheme theme;

            public LocalizedTextFixture()
            {
                root = new GameObject(
                    "LocalizedCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                root.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                root.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080f, 1920f);

                var panelObject = new GameObject(
                    "LocalizedPanel",
                    typeof(RectTransform),
                    typeof(VerticalLayoutGroup));
                Panel = panelObject.GetComponent<RectTransform>();
                Panel.SetParent(root.transform, false);
                Panel.anchorMin = Panel.anchorMax = new Vector2(0.5f, 0.5f);
                Panel.sizeDelta = new Vector2(360f, 640f);
                panelObject.GetComponent<VerticalLayoutGroup>().spacing = 16f;

                theme = ScriptableObject.CreateInstance<AnimalCafeUiTheme>();
                theme.Typography = new UiTypographyTokens
                {
                    Body = new UiTextStyleToken(null, 12f, FontStyles.Normal, 0f),
                    Label = new UiTextStyleToken(null, 10f, FontStyles.Normal, 0f)
                };
                Body = CreateLabel("Body", UiTextStyle.Body, BodyExpanded);
                Label = CreateLabel("Label", UiTextStyle.Label, LabelExpanded);
            }

            public RectTransform Panel { get; }
            public TextMeshProUGUI Body { get; }
            public TextMeshProUGUI Label { get; }

            public IEnumerator UpdateLayout()
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(Panel);
                yield return null;
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(Panel);
            }

            public void AssertExpandedByThirtyToFiftyPercent()
            {
                Assert.That((float)BodyExpanded.Length / BodyBaseline.Length, Is.InRange(1.3f, 1.5f));
                Assert.That((float)LabelExpanded.Length / LabelBaseline.Length, Is.InRange(1.3f, 1.5f));
            }

            public void AssertResponsiveTextLayout(TextMeshProUGUI text, float minimumFontSize)
            {
                Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(minimumFontSize));
                Assert.That(text.textWrappingMode, Is.EqualTo(TextWrappingModes.Normal));
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 0.5f));
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(theme);
            }

            private TextMeshProUGUI CreateLabel(string name, UiTextStyle style, string text)
            {
                var gameObject = new GameObject(
                    name,
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI),
                    typeof(ContentSizeFitter),
                    typeof(AnimalCafeTextStyle));
                var rect = gameObject.GetComponent<RectTransform>();
                rect.SetParent(Panel, false);
                rect.sizeDelta = new Vector2(328f, 48f);
                var label = gameObject.GetComponent<TextMeshProUGUI>();
                label.text = text;
                gameObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                gameObject.GetComponent<AnimalCafeTextStyle>().Configure(theme, style, label);
                return label;
            }
        }

        private sealed class InputFocusIsolationScope : IDisposable
        {
            private readonly InputSettings.BackgroundBehavior originalBackgroundBehavior;
            private readonly InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
            private readonly bool originalRunInBackground;

            public InputFocusIsolationScope()
            {
                originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
                originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                originalRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            }

            public void Dispose()
            {
                InputSystem.settings.backgroundBehavior = originalBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
                Application.runInBackground = originalRunInBackground;
            }
        }

        private static Vector3[] GetScreenCorners(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            for (var index = 0; index < corners.Length; index++)
            {
                corners[index] = RectTransformUtility.WorldToScreenPoint(null, corners[index]);
            }

            return corners;
        }

        private static Vector2 GetScreenCenter(RectTransform rect)
        {
            var corners = GetScreenCorners(rect);
            return (corners[0] + corners[2]) * 0.5f;
        }
    }
}

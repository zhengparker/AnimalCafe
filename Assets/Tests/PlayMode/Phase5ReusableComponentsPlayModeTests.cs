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
    public sealed class Phase5ReusableComponentsPlayModeTests
    {
        [UnityTest]
        public IEnumerator IT003_ThreeButtonRoles_UseDefaultPressedDisabledStatesAndDisabledDoesNotInvoke()
        {
            using (var fixture = new TouchButtonFixture())
            {
                foreach (UiButtonRole role in System.Enum.GetValues(typeof(UiButtonRole)))
                {
                    fixture.View.Configure(fixture.Theme, role, fixture.Button, fixture.Background);
                    fixture.Button.interactable = true;
                    yield return null;

                    Assert.That(fixture.View.CurrentState, Is.EqualTo(UiButtonState.Default));
                    Assert.That(fixture.Background.color, Is.EqualTo(fixture.ExpectedColor(role)));
                    var defaultScale = fixture.Button.transform.localScale;

                    fixture.QueueTouch(InputTouchPhase.Began);
                    yield return null;
                    fixture.QueueTouch(InputTouchPhase.Stationary);
                    yield return null;
                    Assert.That(fixture.View.CurrentState, Is.EqualTo(UiButtonState.Pressed));
                    Assert.That(fixture.Background.color,
                        Is.EqualTo(Color.Lerp(fixture.ExpectedColor(role), Color.black, 0.25f)));
                    Assert.That(fixture.Button.transform.localScale,
                        Is.EqualTo(Vector3.Scale(defaultScale, new Vector3(0.97f, 0.97f, 1f))));

                    fixture.QueueTouch(InputTouchPhase.Ended);
                    yield return null;
                    Assert.That(fixture.View.CurrentState, Is.EqualTo(UiButtonState.Default));
                    Assert.That(fixture.Background.color, Is.EqualTo(fixture.ExpectedColor(role)));
                    Assert.That(fixture.Button.transform.localScale, Is.EqualTo(defaultScale));

                    var invocationsBeforeDisabledTap = fixture.InvocationCount;
                    fixture.Button.interactable = false;
                    yield return null;
                    Assert.That(fixture.View.CurrentState, Is.EqualTo(UiButtonState.Disabled));

                    fixture.QueueTouch(InputTouchPhase.Began);
                    yield return null;
                    fixture.QueueTouch(InputTouchPhase.Stationary);
                    yield return null;
                    fixture.QueueTouch(InputTouchPhase.Ended);
                    yield return null;
                    Assert.That(fixture.InvocationCount, Is.EqualTo(invocationsBeforeDisabledTap));
                }
            }
        }

        [UnityTest]
        public IEnumerator IT004_PanelStylesBindThemeMaterialsAndOnlyOnePanelOwnsStrongFrost()
        {
            var theme = ScriptableObject.CreateInstance<AnimalCafeUiTheme>();
            var solid = CreateMaterial();
            var light = CreateMaterial();
            var strong = CreateMaterial();
            var fallback = CreateMaterial();
            theme.Materials = new UiMaterialTokens(solid, light, strong, fallback);
            var leases = new StrongFrostLease(isStrongFrostSupported: true);
            var solidPanel = CreatePanel("SolidPanel");
            var lightPanel = CreatePanel("LightPanel");
            var firstStrongPanel = CreatePanel("FirstStrongPanel");
            var secondStrongPanel = CreatePanel("SecondStrongPanel");

            try
            {
                solidPanel.View.Configure(theme, UiPanelStyle.Solid, leases);
                lightPanel.View.Configure(theme, UiPanelStyle.LightFrost, leases);
                firstStrongPanel.View.Configure(theme, UiPanelStyle.StrongFrost, leases);
                secondStrongPanel.View.Configure(theme, UiPanelStyle.StrongFrost, leases);
                yield return null;

                Assert.That(solidPanel.Image.material, Is.SameAs(solid));
                Assert.That(lightPanel.Image.material, Is.SameAs(light));
                Assert.That(firstStrongPanel.Image.material, Is.SameAs(strong));
                Assert.That(firstStrongPanel.View.ResolvedStyle, Is.EqualTo(UiPanelStyle.StrongFrost));
                Assert.That(secondStrongPanel.Image.material, Is.SameAs(fallback));
                Assert.That(secondStrongPanel.View.ResolvedStyle, Is.EqualTo(UiPanelStyle.LightFrost));
            }
            finally
            {
                Object.DestroyImmediate(solidPanel.Root);
                Object.DestroyImmediate(lightPanel.Root);
                Object.DestroyImmediate(firstStrongPanel.Root);
                Object.DestroyImmediate(secondStrongPanel.Root);
                Object.DestroyImmediate(theme);
                Object.DestroyImmediate(solid);
                Object.DestroyImmediate(light);
                Object.DestroyImmediate(strong);
                Object.DestroyImmediate(fallback);
            }
        }

        [UnityTest]
        public IEnumerator IT026_UnsupportedStrongFrost_UsesFallbackWithoutChangingContentOrControls()
        {
            using (var fixture = new TouchButtonFixture())
            {
                var leases = new StrongFrostLease(isStrongFrostSupported: false);
                fixture.PanelView.Configure(fixture.Theme, UiPanelStyle.StrongFrost, leases);
                fixture.TextStyle.Configure(fixture.Theme, UiTextStyle.Label, fixture.Label);
                fixture.View.Configure(
                    fixture.Theme, UiButtonRole.Primary, fixture.Button, fixture.Background);
                var buttonTransform = fixture.Button.transform;
                yield return null;

                fixture.QueueTouch(InputTouchPhase.Began);
                yield return null;
                fixture.QueueTouch(InputTouchPhase.Stationary);
                yield return null;
                fixture.QueueTouch(InputTouchPhase.Ended);
                yield return null;

                Assert.That(fixture.PanelView.ResolvedStyle, Is.EqualTo(UiPanelStyle.LightFrost));
                Assert.That(fixture.PanelImage.material, Is.SameAs(fixture.Theme.Materials.StrongFrostFallback));
                Assert.That(fixture.Button.transform, Is.SameAs(buttonTransform));
                Assert.That(fixture.InvocationCount, Is.EqualTo(1));
                Assert.That(fixture.Label.fontSize, Is.EqualTo(fixture.Theme.Typography.Label.FontSize));
            }
        }

        [UnityTest]
        public IEnumerator TransitionRunner_PausedGameUsesUnscaledTimeAndReducedMotionStillReachesFinalState()
        {
            var originalTimeScale = Time.timeScale;
            var root = new GameObject("TransitionCanvasGroup", typeof(RectTransform), typeof(CanvasGroup));
            var group = root.GetComponent<CanvasGroup>();

            try
            {
                Time.timeScale = 0f;
                group.alpha = 0f;
                var normalRunner = new UiTransitionRunner(() => false);

                yield return normalRunner.Run(group, visible: true, 0.03f);

                Assert.That(group.alpha, Is.EqualTo(1f));

                group.alpha = 0f;
                var reducedRunner = new UiTransitionRunner(() => true);

                yield return reducedRunner.Run(group, visible: true, 1f);

                Assert.That(group.alpha, Is.EqualTo(1f));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator Fix1_StrongPanel_LeaseFollowsActiveLifecycleAndReResolvesMaterial()
        {
            var theme = ScriptableObject.CreateInstance<AnimalCafeUiTheme>();
            var solid = CreateMaterial();
            var light = CreateMaterial();
            var strong = CreateMaterial();
            var fallback = CreateMaterial();
            theme.Materials = new UiMaterialTokens(solid, light, strong, fallback);
            var leases = new StrongFrostLease(isStrongFrostSupported: true);
            var first = CreatePanel("LifecycleStrongFirst");
            var second = CreatePanel("LifecycleStrongSecond");

            try
            {
                first.Root.SetActive(false);
                second.Root.SetActive(false);
                first.View.Configure(theme, UiPanelStyle.StrongFrost, leases);

                var externalOwner = leases.Acquire(new object());
                Assert.That(
                    externalOwner.ResolvedStyle,
                    Is.EqualTo(UiPanelStyle.StrongFrost),
                    "Inactive Configure must not hold the Strong lease.");

                first.Root.SetActive(true);
                yield return null;
                Assert.That(first.View.ResolvedStyle, Is.EqualTo(UiPanelStyle.LightFrost));
                Assert.That(first.Image.material, Is.SameAs(fallback));

                externalOwner.Dispose();
                first.Root.SetActive(false);
                first.Root.SetActive(true);
                yield return null;
                Assert.That(first.View.ResolvedStyle, Is.EqualTo(UiPanelStyle.StrongFrost));
                Assert.That(first.Image.material, Is.SameAs(strong));

                second.View.Configure(theme, UiPanelStyle.StrongFrost, leases);
                second.Root.SetActive(true);
                yield return null;
                Assert.That(second.View.ResolvedStyle, Is.EqualTo(UiPanelStyle.LightFrost));
                Assert.That(second.Image.material, Is.SameAs(fallback));

                first.Root.SetActive(false);
                var releasedProbe = leases.Acquire(new object());
                Assert.That(releasedProbe.ResolvedStyle, Is.EqualTo(UiPanelStyle.StrongFrost));
                releasedProbe.Dispose();

                second.Root.SetActive(false);
                second.Root.SetActive(true);
                yield return null;
                Assert.That(second.View.ResolvedStyle, Is.EqualTo(UiPanelStyle.StrongFrost));
                Assert.That(second.Image.material, Is.SameAs(strong));

                first.Root.SetActive(true);
                yield return null;
                Assert.That(first.View.ResolvedStyle, Is.EqualTo(UiPanelStyle.LightFrost));
                Assert.That(first.Image.material, Is.SameAs(fallback));
            }
            finally
            {
                Object.DestroyImmediate(first.Root);
                Object.DestroyImmediate(second.Root);
                Object.DestroyImmediate(theme);
                Object.DestroyImmediate(solid);
                Object.DestroyImmediate(light);
                Object.DestroyImmediate(strong);
                Object.DestroyImmediate(fallback);
            }
        }

        private static PanelFixture CreatePanel(string name)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            var image = root.AddComponent<Image>();
            var view = root.AddComponent<AnimalCafePanelView>();
            return new PanelFixture(root, image, view);
        }

        private static Material CreateMaterial()
        {
            return new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        private readonly struct PanelFixture
        {
            public PanelFixture(GameObject root, Image image, AnimalCafePanelView view)
            {
                Root = root;
                Image = image;
                View = view;
            }

            public GameObject Root { get; }
            public Image Image { get; }
            public AnimalCafePanelView View { get; }
        }

        private sealed class TouchButtonFixture : System.IDisposable
        {
            private readonly GameObject canvasObject;
            private readonly GameObject eventSystemObject;
            private readonly List<GameObject> disabledEventSystems = new List<GameObject>();
            private readonly InputSystemUIInputModule inputModule;
            private readonly Touchscreen touchscreen;
            private readonly InputSettings.BackgroundBehavior originalBackgroundBehavior;
            private readonly InputSettings.EditorInputBehaviorInPlayMode originalEditorInputBehavior;
            private readonly bool originalRunInBackground;
            private int touchId;
            private readonly List<Object> ownedAssets = new List<Object>();

            public TouchButtonFixture()
            {
                originalBackgroundBehavior = InputSystem.settings.backgroundBehavior;
                originalEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                originalRunInBackground = Application.runInBackground;
                Application.runInBackground = true;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

                foreach (var existing in Resources.FindObjectsOfTypeAll<EventSystem>())
                {
                    if (existing.gameObject.scene.IsValid() && existing.gameObject.scene.isLoaded
                        && existing.gameObject.activeSelf)
                    {
                        disabledEventSystems.Add(existing.gameObject);
                        existing.gameObject.SetActive(false);
                    }
                }

                Theme = ScriptableObject.CreateInstance<AnimalCafeUiTheme>();
                Theme.Colors = new UiSemanticColorTokens
                {
                    Accent = Color.green,
                    Surface = Color.white,
                    Destructive = Color.red,
                    Disabled = Color.gray
                };
                Theme.Materials = new UiMaterialTokens(
                    CreateOwnedMaterial(), CreateOwnedMaterial(),
                    CreateOwnedMaterial(), CreateOwnedMaterial());
                Theme.Typography = new UiTypographyTokens
                {
                    Label = new UiTextStyleToken(null, 14f, FontStyles.Normal, 0f)
                };

                canvasObject = new GameObject(
                    "ReusableButtonCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
                canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var panelObject = new GameObject(
                    "ReusablePanel", typeof(RectTransform), typeof(CanvasRenderer));
                panelObject.transform.SetParent(canvasObject.transform, false);
                var panelRect = panelObject.GetComponent<RectTransform>();
                panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = new Vector2(320f, 320f);
                PanelImage = panelObject.AddComponent<Image>();
                PanelView = panelObject.AddComponent<AnimalCafePanelView>();
                var buttonObject = new GameObject(
                    "ReusableButton", typeof(RectTransform), typeof(CanvasRenderer));
                buttonObject.transform.SetParent(panelObject.transform, false);
                var rect = buttonObject.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(200f, 200f);
                Background = buttonObject.AddComponent<Image>();
                Button = buttonObject.AddComponent<Button>();
                View = buttonObject.AddComponent<AnimalCafeButtonView>();
                Button.onClick.AddListener(() => InvocationCount++);
                var labelObject = new GameObject(
                    "ReusableLabel", typeof(RectTransform), typeof(CanvasRenderer));
                labelObject.transform.SetParent(buttonObject.transform, false);
                Label = labelObject.AddComponent<TextMeshProUGUI>();
                TextStyle = labelObject.AddComponent<AnimalCafeTextStyle>();

                eventSystemObject = new GameObject("ReusableButtonEventSystem");
                eventSystemObject.AddComponent<EventSystem>();
                inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
                inputModule.UnassignActions();
                inputModule.AssignDefaultActions();
                touchscreen = InputSystem.AddDevice<Touchscreen>();
                touchscreen.MakeCurrent();
                Canvas.ForceUpdateCanvases();
            }

            public AnimalCafeUiTheme Theme { get; }
            public Image Background { get; }
            public Button Button { get; }
            public AnimalCafeButtonView View { get; }
            public AnimalCafePanelView PanelView { get; }
            public Image PanelImage { get; }
            public AnimalCafeTextStyle TextStyle { get; }
            public TMP_Text Label { get; }
            public int InvocationCount { get; private set; }

            public Color ExpectedColor(UiButtonRole role)
            {
                return role switch
                {
                    UiButtonRole.Primary => Theme.Colors.Accent,
                    UiButtonRole.Secondary => Theme.Colors.Surface,
                    UiButtonRole.Destructive => Theme.Colors.Destructive,
                    _ => Color.clear
                };
            }

            public void QueueTouch(InputTouchPhase phase)
            {
                if (phase == InputTouchPhase.Began)
                {
                    touchId++;
                }

                InputSystem.QueueStateEvent(
                    touchscreen,
                    new TouchState
                    {
                        touchId = touchId,
                        phase = phase,
                        position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                        pressure = phase == InputTouchPhase.Ended ? 0f : 1f
                    });
            }

            public void Dispose()
            {
                inputModule.UnassignActions();
                if (touchscreen.added)
                {
                    InputSystem.RemoveDevice(touchscreen);
                }

                Object.DestroyImmediate(eventSystemObject);
                Object.DestroyImmediate(canvasObject);
                Object.DestroyImmediate(Theme);
                foreach (var asset in ownedAssets)
                {
                    Object.DestroyImmediate(asset);
                }
                foreach (var disabled in disabledEventSystems)
                {
                    if (disabled != null)
                    {
                        disabled.SetActive(true);
                    }
                }

                InputSystem.settings.backgroundBehavior = originalBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = originalEditorInputBehavior;
                Application.runInBackground = originalRunInBackground;
            }

            private Material CreateOwnedMaterial()
            {
                var material = CreateMaterial();
                ownedAssets.Add(material);
                return material;
            }
        }
    }
}

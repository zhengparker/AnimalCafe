using AnimalCafe.Core.Events;
using AnimalCafe.Core.Time;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI
{
    /// <summary>
    /// 将 placeholder UI buttons 连接到统一 Game Time service。
    /// Connects placeholder UI buttons to the shared Game Time service.
    /// </summary>
    public sealed class TimeControlPanel : MonoBehaviour
    {
        [SerializeField] private AnimalCafe.UI.P8R.P8RAppearance appearance;
        [SerializeField] private TMP_Text modeBadgeLabel;
        [SerializeField]
        private GameTimeService gameTimeService;

        [SerializeField]
        private Button pauseButton;

        [SerializeField]
        private Button normalButton;

        [SerializeField]
        private Button fastButton;

        [SerializeField]
        private GameObject pauseSelectedVisual;

        [SerializeField]
        private GameObject normalSelectedVisual;

        [SerializeField]
        private GameObject fastSelectedVisual;

        [SerializeField]
        private TMP_Text pauseLabel;

        private bool listenersRegistered;
        private bool speedListenerRegistered;
        private bool decorationPauseLocked;
        private GameSpeed decorationEntrySpeed;
        private bool refreshingP8RLayout;

        private void Start()
        {
            if (!HasRequiredReferences())
            {
                Debug.LogError(
                    "[TimeControlPanel] Game Time service and all three buttons are required.",
                    this);
                enabled = false;
                return;
            }

            RegisterListeners();
            RegisterSpeedListener();
            RefreshSelectedVisuals();
        }

        private void OnEnable()
        {
            AnimalCafe.UI.P8R.P8RMobileMetrics.Changed += RefreshP8RLayout;
            RegisterListeners();
            RegisterSpeedListener();
            RefreshSelectedVisuals();
        }

        private void OnDisable()
        {
            AnimalCafe.UI.P8R.P8RMobileMetrics.Changed -= RefreshP8RLayout;
            RemoveListeners();
            RemoveSpeedListener();
        }

        public void Configure(
            GameTimeService service,
            Button pause,
            Button normal,
            Button fast)
        {
            RemoveListeners();
            gameTimeService = service;
            pauseButton = pause;
            normalButton = normal;
            fastButton = fast;
            ResolveSelectedVisuals();
            RegisterListeners();
            RegisterSpeedListener();
            RefreshSelectedVisuals();
        }

        /// <summary>
        /// Decoration Mode owns a Pause lease, so 1x/2x cannot override it.
        /// Decoration Mode 持有 Pause lease 时，禁止 1x/2x 覆盖该状态。
        /// </summary>
        public void SetDecorationPauseLock(bool locked, GameSpeed? enteringSpeed = null)
        {
            if (locked && !decorationPauseLocked)
                decorationEntrySpeed = enteringSpeed ?? gameTimeService.CurrentSpeed;
            decorationPauseLocked = locked;
            if (normalButton != null)
            {
                normalButton.interactable = !locked;
            }

            if (fastButton != null)
            {
                fastButton.interactable = !locked;
            }

            if (pauseButton != null)
            {
                pauseButton.interactable = !locked;
            }

            RefreshSelectedVisuals();
        }

        private bool HasRequiredReferences()
        {
            return gameTimeService != null
                && pauseButton != null
                && normalButton != null
                && fastButton != null;
        }

        private void RegisterListeners()
        {
            if (listenersRegistered || !HasRequiredReferences())
            {
                return;
            }

            pauseButton.onClick.AddListener(HandlePauseClicked);
            normalButton.onClick.AddListener(HandleNormalClicked);
            fastButton.onClick.AddListener(HandleFastClicked);
            listenersRegistered = true;
        }

        private void HandlePauseClicked()
        {
            if (!decorationPauseLocked) gameTimeService.TogglePaused();
        }

        private void HandleNormalClicked()
        {
            if (decorationPauseLocked) return;
            if (appearance == null) { gameTimeService.SetNormal(); return; }
            // The selected speed doubles as Pause; the first button always returns to Normal.
            // 点击当前速度暂停；暂停后的第一按钮固定回到1倍速。
            if (gameTimeService.CurrentSpeed == GameSpeed.Paused) gameTimeService.SetNormal();
            else if (gameTimeService.CurrentSpeed == GameSpeed.Normal) gameTimeService.SetPaused();
            else gameTimeService.SetNormal();
        }

        private void HandleFastClicked()
        {
            if (decorationPauseLocked) return;
            if (appearance != null && gameTimeService.CurrentSpeed == GameSpeed.Fast) gameTimeService.SetPaused();
            else gameTimeService.SetFast();
        }

        private void RegisterSpeedListener()
        {
            if (speedListenerRegistered || gameTimeService == null)
            {
                return;
            }

            GameEventBus.GameSpeedChanged += HandleGameSpeedChanged;
            speedListenerRegistered = true;
        }

        private void RemoveListeners()
        {
            if (!listenersRegistered)
            {
                return;
            }

            pauseButton.onClick.RemoveListener(HandlePauseClicked);
            normalButton.onClick.RemoveListener(HandleNormalClicked);
            fastButton.onClick.RemoveListener(HandleFastClicked);
            listenersRegistered = false;
        }

        private void RemoveSpeedListener()
        {
            if (!speedListenerRegistered)
            {
                return;
            }

            GameEventBus.GameSpeedChanged -= HandleGameSpeedChanged;
            speedListenerRegistered = false;
        }

        private void HandleGameSpeedChanged(GameSpeedChangedEvent speedChanged)
        {
            RefreshSelectedVisuals(speedChanged.Current);
        }

        private void RefreshSelectedVisuals()
        {
            ResolveSelectedVisuals();
            RefreshSelectedVisuals(gameTimeService != null
                ? gameTimeService.CurrentSpeed
                : GameSpeed.Normal);
        }

        private void RefreshSelectedVisuals(GameSpeed speed)
        {
            ResolvePauseLabel();
            if (appearance != null)
            {
                pauseButton.gameObject.SetActive(false);
                normalButton.gameObject.SetActive(true);
                fastButton.gameObject.SetActive(true);
                normalButton.interactable = fastButton.interactable = !decorationPauseLocked;
                var lockFast = decorationPauseLocked && decorationEntrySpeed == GameSpeed.Fast;
                var lockNormal = decorationPauseLocked && !lockFast;
                appearance.Tab(normalButton, "clock", !decorationPauseLocked && speed != GameSpeed.Fast,
                    decorationPauseLocked, iconOnly: true,
                    iconAction: lockNormal ? "lock" : !decorationPauseLocked && speed == GameSpeed.Paused ? "pause" : "resume");
                appearance.Tab(fastButton, "fast_forward", !decorationPauseLocked && speed == GameSpeed.Fast,
                    decorationPauseLocked, iconOnly: true, iconAction: lockFast ? "lock" : "resume");
                RefreshP8RLayout();
                SetSelected(pauseSelectedVisual, false);
                SetSelected(normalSelectedVisual, false);
                SetSelected(fastSelectedVisual, false);
                return;
            }
            if (pauseLabel != null)
            {
                pauseLabel.text = speed == GameSpeed.Paused ? "Resume" : "Pause";
            }

            if (decorationPauseLocked)
            {
                speed = GameSpeed.Paused;
            }

            SetSelected(pauseSelectedVisual, speed == GameSpeed.Paused);
            SetSelected(normalSelectedVisual, speed == GameSpeed.Normal);
            SetSelected(fastSelectedVisual, speed == GameSpeed.Fast);
        }

        private void ResolveSelectedVisuals()
        {
            pauseSelectedVisual ??= FindSelectedVisual(pauseButton);
            normalSelectedVisual ??= FindSelectedVisual(normalButton);
            fastSelectedVisual ??= FindSelectedVisual(fastButton);
            ResolvePauseLabel();
        }

        private const float ModeHeight = 48f;
        private const float BadgeWidth = 104f;
        private const float TimeControlsWidth = 104f;

        private void RefreshP8RLayout()
        {
            if (appearance == null || refreshingP8RLayout || transform is not RectTransform) return;
            refreshingP8RLayout = true;
            try
            {
                var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
                var root = (RectTransform)transform;
                var availableWidth = transform.parent is RectTransform parent
                    ? parent.rect.width - metrics.Units(16) : metrics.Units(metrics.LogicalViewport.x - 16);
                var mode = transform.Find("DecorationModeButton")?.GetComponent<Button>();
                var modeWidth = metrics.Units(104);
                if (mode != null)
                {
                    var label = mode.transform.Find("Label")?.GetComponent<TMP_Text>();
                    if (label != null)
                    {
                        label.fontSize = metrics.Units(14);
                        modeWidth = Mathf.Max(modeWidth,
                            label.GetPreferredValues(appearance.Text("mode.enter")).x + metrics.Units(48));
                    }
                }
                var sameRow = availableWidth >= metrics.Units(BadgeWidth + 8 + TimeControlsWidth + 8) + modeWidth;
                var height = sameRow ? ModeHeight : ModeHeight * 2 + 8;
                root.anchorMin = new Vector2(0, 1); root.anchorMax = Vector2.one; root.pivot = new Vector2(.5f, 1);
                root.offsetMin = new Vector2(metrics.Units(8), -metrics.Units(height + 8));
                root.offsetMax = new Vector2(-metrics.Units(8), -metrics.Units(8));
                var strip = transform.Find("P8RTimeStrip") as RectTransform;
                if (strip == null)
                {
                    var container = new GameObject("P8RTimeStrip", typeof(RectTransform));
                    container.transform.SetParent(transform, false);
                    strip = container.GetComponent<RectTransform>();
                }
                strip.gameObject.layer = gameObject.layer;
                strip.anchorMin = strip.anchorMax = strip.pivot = new Vector2(0, 1);
                strip.anchoredPosition = new Vector2(metrics.Units(sameRow ? BadgeWidth + 8 : 0),
                    -metrics.Units(sameRow ? 0 : ModeHeight + 8));
                strip.sizeDelta = new Vector2(metrics.Units(TimeControlsWidth), metrics.Units(ModeHeight));
                LayoutTimeButton(normalButton, strip, metrics, 0, 48);
                LayoutTimeButton(fastButton, strip, metrics, 56, 48);
                AnimalCafe.UI.P8R.P8RButtonLayout.IconButtonByHeight(normalButton, 20, 36, ModeHeight);
                AnimalCafe.UI.P8R.P8RButtonLayout.IconButtonByHeight(fastButton, 20, 36, ModeHeight);
                AnimalCafe.UI.P8R.P8RButtonLayout.StableTimeBorder(normalButton, appearance);
                AnimalCafe.UI.P8R.P8RButtonLayout.StableTimeBorder(fastButton, appearance);
                AnimalCafe.UI.P8R.P8RButtonLayout.RepeatedTriangle(fastButton,
                    !(decorationPauseLocked && decorationEntrySpeed == GameSpeed.Fast));
                if (modeBadgeLabel != null && modeBadgeLabel.transform.parent is RectTransform badge)
                {
                    badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(0, 1);
                    badge.anchoredPosition = Vector2.zero;
                    badge.sizeDelta = new Vector2(metrics.Units(BadgeWidth), metrics.Units(ModeHeight));
                    modeBadgeLabel.fontSize = metrics.Units(14);
                }
                RefreshP8RModeBadge(decorationPauseLocked);
                var indicator = GetComponentInChildren<GameTimeStatusIndicator>(true);
                if (indicator != null) indicator.gameObject.SetActive(false);
            }
            finally { refreshingP8RLayout = false; }
        }

        private static void LayoutTimeButton(Button button, RectTransform parent,
            AnimalCafe.UI.P8R.P8RMobileMetrics metrics, float x, float width)
        {
            if (button == null) return;
            var rect = (RectTransform)button.transform;
            if (rect.parent != parent) rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(metrics.Units(x), 0);
            rect.sizeDelta = new Vector2(metrics.Units(width), metrics.Units(ModeHeight));
        }

        private void OnRectTransformDimensionsChange() => RefreshP8RLayout();

        public void RefreshP8RModeBadge(bool decorating)
        {
            if (appearance == null) return;
            var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
            if (modeBadgeLabel != null)
            {
                modeBadgeLabel.fontStyle = FontStyles.Bold;
                modeBadgeLabel.text = appearance.Text(decorating ? "mode.decoration" : "mode.normal");
                modeBadgeLabel.alignment = TextAlignmentOptions.MidlineGeoAligned;
                appearance.Paint(modeBadgeLabel.GetComponentInParent<Image>(), "button_secondary_normal");
                var textRect = modeBadgeLabel.rectTransform;
                textRect.anchoredPosition = Vector2.zero;
                modeBadgeLabel.ForceMeshUpdate(true, true);
                var ink = modeBadgeLabel.textBounds;
                if (ink.size.x > 0 && ink.size.x < textRect.rect.width && ink.size.y < textRect.rect.height
                    && Mathf.Abs(ink.center.x) < textRect.rect.width && Mathf.Abs(ink.center.y) < textRect.rect.height)
                    textRect.anchoredPosition = -(Vector2)ink.center;
            }
            var mode = transform.Find("DecorationModeButton")?.GetComponent<Button>();
            if (mode == null) return;
            appearance.Button(mode, decorating ? "exit" : "decorate", role: "secondary");
            var label = mode.transform.Find("Label")?.GetComponent<TMP_Text>();
            var rect = (RectTransform)mode.transform;
            var width = metrics.Units(104);
            if (label != null)
            {
                label.text = appearance.Text(decorating ? "mode.done" : "mode.enter");
                label.fontStyle = FontStyles.Bold;
                label.fontSize = metrics.Units(14);
                width = Mathf.Max(width, label.GetPreferredValues(label.text).x + metrics.Units(48));
            }
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(width, metrics.Units(ModeHeight));
            AnimalCafe.UI.P8R.P8RButtonLayout.TextButton(mode, 14, ModeHeight, 12);
            // Equal visible heights and a common top row align both mode controls.
            // 左右模式外框共用可见高度、顶边和垂直中心线。
            mode.image.rectTransform.sizeDelta = rect.sizeDelta;
        }
        private void ResolvePauseLabel()
        {
            pauseLabel ??= pauseButton != null
                ? pauseButton.GetComponentInChildren<TMP_Text>(true)
                : null;
        }

        private static GameObject FindSelectedVisual(Button button)
        {
            return button != null
                ? button.transform.Find("SelectedVisual")?.gameObject
                : null;
        }

        private static void SetSelected(GameObject visual, bool selected)
        {
            if (visual != null && visual.activeSelf != selected)
            {
                visual.SetActive(selected);
            }
        }
    }
}

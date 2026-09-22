using System.Collections;
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
        private bool refreshingP8RLayout;
        private const float TimeStripWidth = 144f + 2f / 64f;
        private const float TimeStripHeight = 32f;
        private const float TimeSegmentStride = 48f + 1f / 64f;
        private const float SelectionSlideDuration = .18f;
        private RectTransform timeSelection;
        private Image timeSelectionImage;
        private Coroutine selectionMotion;
        private float selectionPosition;
        private int selectionTarget;
        private bool selectionInitialized;

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
            selectionInitialized = false;
            AnimalCafe.UI.P8R.P8RMobileMetrics.Changed += RefreshP8RLayout;
            RegisterListeners();
            RegisterSpeedListener();
            RefreshSelectedVisuals();
        }

        private void OnDisable()
        {
            StopSelectionMotion();
            selectionInitialized = false;
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
        public void SetDecorationPauseLock(bool locked)
        {
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

            pauseButton.onClick.AddListener(gameTimeService.TogglePaused);
            normalButton.onClick.AddListener(gameTimeService.SetNormal);
            fastButton.onClick.AddListener(gameTimeService.SetFast);
            listenersRegistered = true;
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

            pauseButton.onClick.RemoveListener(gameTimeService.TogglePaused);
            normalButton.onClick.RemoveListener(gameTimeService.SetNormal);
            fastButton.onClick.RemoveListener(gameTimeService.SetFast);
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
                RefreshP8RLayout();
                appearance.Tab(pauseButton, decorationPauseLocked ? "lock" : speed == GameSpeed.Paused ? "resume" : "pause",
                    speed == GameSpeed.Paused || decorationPauseLocked, decorationPauseLocked, iconOnly: true);
                // Keep the 1x semantic label separate from the shared triangle artwork.
                // 1x 仍表示固定普通速度，不变成恢复上一次速度的按钮。
                appearance.Tab(normalButton, "clock", speed == GameSpeed.Normal, decorationPauseLocked, iconOnly: true, iconAction: "resume");
                appearance.Tab(fastButton, "fast_forward", speed == GameSpeed.Fast, decorationPauseLocked, iconOnly: true);
                // The wide double triangle needs optical compensation after Tab rebinds its sprite.
                // 2x 依照宽图比例补偿，缩小后仍与 1x 保持接近的可见高度。
                RefreshP8RTimeButtonFaces();
                RefreshP8RSelection(speed);
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

        private void RefreshP8RLayout()
        {
            if (appearance == null || refreshingP8RLayout || transform is not RectTransform) return;
            refreshingP8RLayout = true;
            var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
            var root = (RectTransform)transform;
            var availableWidth = transform.parent is RectTransform parent
                ? parent.rect.width - metrics.Units(16) : metrics.Units(metrics.LogicalViewport.x - 16);
            // Include a subpixel guard between touch roots, then reserve the mode target.
            // 按整行真实宽度判定，较短横屏也不需要第二排。
            var compact = metrics.LogicalViewport.x > metrics.LogicalViewport.y
                && availableWidth >= metrics.Units(TimeStripWidth * 2 + 8 + 8 + 48);
            var height = compact ? 48f : 80f;
            root.anchorMin = new Vector2(0, 1); root.anchorMax = Vector2.one; root.pivot = new Vector2(.5f, 1);
            root.offsetMin = new Vector2(metrics.Units(8), -metrics.Units(height + 8));
            root.offsetMax = new Vector2(-metrics.Units(8), -metrics.Units(8));
            var timeStrip = EnsureP8RTimeStrip(metrics, compact);
            var index = 0;
            foreach (var button in new[] { pauseButton, normalButton, fastButton })
            {
                if (button == null) continue;
                var rect = (RectTransform)button.transform;
                if (rect.parent != timeStrip) rect.SetParent(timeStrip, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.sizeDelta = Vector2.one * metrics.Units(48);
                // A 1/64-unit guard prevents floating-point edge overlap without visible extra spacing.
                // 点击根节点比 32-unit strip 上下各多 8 units；视觉连续，触控范围仍独立且不重叠。
                rect.anchoredPosition = new Vector2(metrics.Units(index * TimeSegmentStride), metrics.Units(8));
                index++;
                foreach (var label in button.GetComponentsInChildren<TMP_Text>(true)) label.fontSize = metrics.Units(14);
            }
            RefreshP8RTimeStripSeparators(timeStrip, metrics);
            RefreshP8RTimeButtonFaces();
            if (modeBadgeLabel != null && modeBadgeLabel.transform.parent is RectTransform badge)
            {
                badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(0, 1);
                badge.anchoredPosition = new Vector2(0, -metrics.Units(compact ? 8 : 0));
                badge.sizeDelta = new Vector2(metrics.Units(TimeStripWidth), metrics.Units(TimeStripHeight));
                modeBadgeLabel.fontSize = metrics.Units(14);
            }
            RefreshP8RModeBadge(decorationPauseLocked);
            var indicator = GetComponentInChildren<GameTimeStatusIndicator>(true);
            if (indicator != null)
            {
                // P8R already shows Pause/1x/2x state; the old ornamental spinner adds no information.
                indicator.gameObject.SetActive(false);
            }
            refreshingP8RLayout = false;
        }

        private void RefreshP8RTimeButtonFaces()
        {
            RefreshP8RTimeSegment(pauseButton, 18f, 0);
            RefreshP8RTimeSegment(normalButton, 18f, 1);
            RefreshP8RTimeSegment(fastButton, 26f, 2);
            // Locked controls keep their grey faces; otherwise only the shared orange window is visible.
            // 锁定时保留灰色分段；可用时隐藏旧的即时选中色，避免遮住滑动背景。
            foreach (var button in new[] { pauseButton, normalButton, fastButton })
                if (button != null && button.image != null) button.image.enabled = decorationPauseLocked;
            if (transform.Find("P8RTimeStrip") is RectTransform strip)
            {
                strip.Find("P8RTimeSeparator1")?.SetAsLastSibling();
                strip.Find("P8RTimeSeparator2")?.SetAsLastSibling();
            }
        }

        private RectTransform EnsureP8RTimeStrip(
            AnimalCafe.UI.P8R.P8RMobileMetrics metrics,
            bool compact)
        {
            var strip = transform.Find("P8RTimeStrip") as RectTransform;
            if (strip == null)
            {
                var stripObject = new GameObject(
                    "P8RTimeStrip",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                stripObject.transform.SetParent(transform, false);
                strip = stripObject.GetComponent<RectTransform>();
            }

            strip.gameObject.layer = gameObject.layer;
            strip.SetAsFirstSibling();
            strip.anchorMin = strip.anchorMax = strip.pivot = new Vector2(0, 1);
            var position = new Vector2(metrics.Units(compact ? TimeStripWidth + 8 : 0),
                -metrics.Units(compact ? 8 : 40));
            var size = new Vector2(metrics.Units(TimeStripWidth), metrics.Units(TimeStripHeight));
            // Only actual geometry changes snap motion. A speed event also refreshes layout,
            // but must not move the highlight to the new speed before animation starts.
            // 只有真实尺寸/排列变化才结束旧动画；普通速度刷新不能提前跳到终点。
            if (selectionInitialized && ((strip.anchoredPosition - position).sqrMagnitude > .0001f
                || (strip.sizeDelta - size).sqrMagnitude > .0001f))
            {
                StopSelectionMotion();
                selectionPosition = selectionTarget;
            }
            strip.anchoredPosition = position;
            strip.sizeDelta = size;
            var background = strip.GetComponent<Image>();
            appearance.Paint(background, "tab_idle");
            background.raycastTarget = false;
            EnsureP8RSelection(strip, metrics);
            return strip;
        }

        private void EnsureP8RSelection(RectTransform strip, AnimalCafe.UI.P8R.P8RMobileMetrics metrics)
        {
            if (timeSelection == null)
            {
                var window = new GameObject("P8RTimeSelection", typeof(RectTransform), typeof(RectMask2D));
                window.transform.SetParent(strip, false);
                timeSelection = window.GetComponent<RectTransform>();
                var fill = new GameObject("P8RTimeSelectionFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fill.transform.SetParent(timeSelection, false);
                timeSelectionImage = fill.GetComponent<Image>();
            }
            timeSelection.gameObject.layer = strip.gameObject.layer;
            timeSelectionImage.gameObject.layer = strip.gameObject.layer;
            timeSelection.SetAsFirstSibling();
            timeSelection.anchorMin = timeSelection.anchorMax = timeSelection.pivot = new Vector2(0, 1);
            timeSelection.sizeDelta = new Vector2(metrics.Units(48), metrics.Units(TimeStripHeight));
            var fillRect = timeSelectionImage.rectTransform;
            fillRect.anchorMin = fillRect.anchorMax = fillRect.pivot = new Vector2(0, 1);
            fillRect.sizeDelta = strip.sizeDelta;
            appearance.Paint(timeSelectionImage, "tab_selected");
            timeSelectionImage.raycastTarget = false;
            timeSelection.gameObject.SetActive(!decorationPauseLocked);
            PositionP8RSelection();
        }

        private void RefreshP8RSelection(GameSpeed speed)
        {
            if (timeSelection == null) return;
            var target = speed == GameSpeed.Paused ? 0 : speed == GameSpeed.Fast ? 2 : 1;
            if (decorationPauseLocked)
            {
                StopSelectionMotion();
                selectionInitialized = false;
                timeSelection.gameObject.SetActive(false);
                return;
            }
            timeSelection.gameObject.SetActive(true);
            if (!selectionInitialized || !isActiveAndEnabled)
            {
                StopSelectionMotion();
                selectionTarget = target;
                selectionPosition = target;
                selectionInitialized = true;
                PositionP8RSelection();
                return;
            }
            if (selectionTarget == target) return;
            StopSelectionMotion();
            selectionTarget = target;
            selectionMotion = StartCoroutine(SlideP8RSelection(selectionPosition, target));
        }

        private IEnumerator SlideP8RSelection(float from, float to)
        {
            var elapsed = 0f;
            while (elapsed < SelectionSlideDuration)
            {
                yield return null;
                elapsed += UnityEngine.Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / SelectionSlideDuration);
                var eased = 1f - (1f - t) * (1f - t) * (1f - t);
                selectionPosition = Mathf.LerpUnclamped(from, to, eased);
                PositionP8RSelection();
            }
            selectionPosition = to;
            PositionP8RSelection();
            selectionMotion = null;
        }

        private void PositionP8RSelection()
        {
            if (timeSelection == null || timeSelectionImage == null) return;
            var x = selectionPosition * AnimalCafe.UI.P8R.P8RMobileMetrics.For(this).Units(TimeSegmentStride);
            timeSelection.anchoredPosition = new Vector2(x, 0);
            // Move the clipping window, not the whole rounded sprite: outer corners stay aligned.
            // 滑动裁切窗口，底板仍对齐整条时间栏，避免两端圆角被拉动。
            timeSelectionImage.rectTransform.anchoredPosition = new Vector2(-x, 0);
        }

        private void StopSelectionMotion()
        {
            if (selectionMotion != null) StopCoroutine(selectionMotion);
            selectionMotion = null;
        }

        private static void RefreshP8RTimeStripSeparators(
            RectTransform strip,
            AnimalCafe.UI.P8R.P8RMobileMetrics metrics)
        {
            if (strip == null) return;
            for (var index = 1; index <= 2; index++)
            {
                var name = "P8RTimeSeparator" + index;
                var separator = strip.Find(name) as RectTransform;
                if (separator == null)
                {
                    var separatorObject = new GameObject(
                        name,
                        typeof(RectTransform),
                        typeof(CanvasRenderer),
                        typeof(Image));
                    separatorObject.transform.SetParent(strip, false);
                    separator = separatorObject.GetComponent<RectTransform>();
                }

                separator.gameObject.layer = strip.gameObject.layer;
                separator.anchorMin = separator.anchorMax = new Vector2(0, 1);
                separator.pivot = Vector2.one * .5f;
                separator.anchoredPosition = new Vector2(
                    metrics.Units(index * 48f + (index - .5f) / 64f),
                    -metrics.Units(TimeStripHeight * .5f));
                separator.sizeDelta = new Vector2(metrics.Units(1), metrics.Units(22));
                var image = separator.GetComponent<Image>();
                image.sprite = null;
                image.color = AnimalCafe.UI.P8R.P8RAppearance.Cocoa;
                image.material = null;
                image.raycastTarget = false;
                separator.SetAsLastSibling();
            }
        }

        private static void RefreshP8RTimeSegment(Button button, float iconExtent, int segmentIndex)
        {
            if (button == null) return;
            // The authored targetGraphic is the root on first load. Bootstrap the reusable child face once,
            // then keep later refreshes on the zero-face-size path before applying the full-strip slice.
            var needsFace = button.image != null && button.image.transform == button.transform;
            AnimalCafe.UI.P8R.P8RButtonLayout.IconButton(button, iconExtent, needsFace ? 34f : 0f);
            if (button.image == null) return;
            var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(button);
            var clip = button.transform.Find("P8RTimeSegmentClip") as RectTransform;
            if (clip == null)
            {
                var clipObject = new GameObject("P8RTimeSegmentClip", typeof(RectTransform), typeof(RectMask2D));
                clipObject.transform.SetParent(button.transform, false);
                clip = clipObject.GetComponent<RectTransform>();
            }

            clip.gameObject.layer = button.gameObject.layer;
            clip.anchorMin = clip.anchorMax = clip.pivot = Vector2.one * .5f;
            clip.anchoredPosition = Vector2.zero;
            clip.sizeDelta = new Vector2(metrics.Units(48), metrics.Units(TimeStripHeight));
            var face = button.image.rectTransform;
            if (face.parent != clip) face.SetParent(clip, false);
            clip.SetAsFirstSibling();
            face.anchorMin = face.anchorMax = Vector2.one * .5f;
            face.pivot = Vector2.one * .5f;
            face.anchoredPosition = new Vector2(
                (1 - segmentIndex) * metrics.Units(TimeSegmentStride),
                0);
            face.sizeDelta = new Vector2(metrics.Units(TimeStripWidth), metrics.Units(TimeStripHeight));
            button.image.gameObject.layer = button.gameObject.layer;
            button.image.raycastTarget = false;
        }

        private void OnRectTransformDimensionsChange() => RefreshP8RLayout();

        public void RefreshP8RModeBadge(bool decorating)
        {
            if (appearance == null) return;
            if (modeBadgeLabel != null)
            {
                modeBadgeLabel.text = appearance.Text(decorating ? "mode.decoration" : "mode.normal");
                // Match the small controls' corners and center the visible glyphs, not font line metrics.
                // 小标签使用按钮圆角；按实际文字墨迹居中。
                modeBadgeLabel.alignment = TextAlignmentOptions.MidlineGeoAligned;
                appearance.Paint(modeBadgeLabel.GetComponentInParent<Image>(), "button_secondary_normal");
                var textRect = modeBadgeLabel.rectTransform;
                textRect.anchoredPosition = Vector2.zero;
                modeBadgeLabel.ForceMeshUpdate(true, true);
                var ink = modeBadgeLabel.textBounds;
                // TMP geometry alignment may retain asymmetric glyph padding; don't bake invalid prefab bounds.
                if (ink.size.x > 0 && ink.size.x < textRect.rect.width && ink.size.y < textRect.rect.height
                    && Mathf.Abs(ink.center.x) < textRect.rect.width && Mathf.Abs(ink.center.y) < textRect.rect.height)
                    // B face center is 4 source pixels above its texture center, at 4x PPU.
                    // B 底板为下阴影留空：文字对齐可见底板中心，而非含阴影的矩形中心。
                    textRect.anchoredPosition = (appearance.IsRefinedB ? Vector2.up : Vector2.zero) - (Vector2)ink.center;
            }
            var mode = transform.Find("DecorationModeButton")?.GetComponent<Button>();
            if (mode == null) return;
            var rect = (RectTransform)mode.transform;
            var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = Vector2.zero; rect.sizeDelta = Vector2.one * metrics.Units(48);
            var label = mode.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null) label.fontSize = metrics.Units(14);
            AnimalCafe.UI.P8R.P8RButtonLayout.IconButton(mode, 20f, 40f);
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

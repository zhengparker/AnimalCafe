using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.Feedback
{
    /// <summary>
    /// Displays a specific validation reason until its caller reports corrected input.
    /// 显示具体 validation 原因，直到调用方报告输入已修正。
    /// </summary>
    public sealed class ValidationMessageView : MonoBehaviour
    {
        [SerializeField] private AnimalCafe.UI.P8R.P8RAppearance appearance;
        [SerializeField] private Image statusIcon;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private UnityEngine.UI.Graphic background;

        private const float DisclosureHeight = 36f;
        private const float DisclosureGap = 8f;
        private float ActiveDisclosureHeight => appearance != null ? 48f : DisclosureHeight;
        private string DisclosureText => appearance != null ? appearance.Text(IsDetailsExpanded ? "readiness.collapse" : "readiness.expand")
            : IsDetailsExpanded ? "收起详情" : "查看详情";
        private Button disclosureButton;
        private TMP_Text disclosureLabel;
        private string readinessSummary = string.Empty;
        private string readinessDetails = string.Empty;
        private bool hasPendingPreview;
        private RectTransform disclosureViewport;
        private Vector2 viewportOffsetBeforeDisclosure;
        private bool refreshingLayout;
        private RectTransform p8rHud;
        private bool hasP8RHudBottom;
        private float lastP8RHudBottom;
        private Canvas p8rGeometryCanvas;
        private bool hasPublishedP8RBounds;
        private Rect publishedP8RScreenBounds;
        private readonly Vector3[] p8rGeometryCorners = new Vector3[4];

        public void ConfigureP8RHud(RectTransform hud)
        {
            if (p8rHud == hud) return;
            p8rHud = hud;
            RefreshVisibleLayout();
        }
        public event Action DetailsVisibilityChanged;

        public bool IsVisible { get; private set; }
        public bool IsDetailsExpanded { get; private set; }
        public string FullReadinessMessage { get; private set; } = string.Empty;
        public string CurrentMessage => messageLabel != null ? messageLabel.text : string.Empty;
        public IReadOnlyList<string> DiagnosticIds { get; private set; } =
            Array.Empty<string>();

        public void Configure(TMP_Text label)
        {
            messageLabel = label ?? throw new ArgumentNullException(nameof(label));
            background ??= GetComponent<UnityEngine.UI.Graphic>();
            Clear();
        }

        public void SetValidationResult(bool isValid, string specificReason)
        {
            SetValidationResult(isValid, specificReason, null);
        }

        public void SetValidationResult(
            bool isValid,
            string specificReason,
            IEnumerable<string> diagnosticIds)
        {
            if (isValid)
            {
                Clear();
                return;
            }

            ShowStatus(specificReason, diagnosticIds);
        }

        public void ShowStatus(
            string message,
            IEnumerable<string> diagnosticIds = null)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException(
                    "A visible status requires a specific message.",
                    nameof(message));
            }

            ResetReadinessDisclosure();
            var ids = diagnosticIds == null
                ? Array.Empty<string>()
                : diagnosticIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            DiagnosticIds = ids;
            ShowMessage(message);
        }

        private void ShowMessage(string message, bool richText = false)
        {
            // Keep the player message readable; detached IDs remain available for diagnostics.
            // 玩家只看说明文字；独立保存原始 IDs，供排查问题使用。
            messageLabel.richText = richText;
            messageLabel.text = message;
            messageLabel.enabled = true;
            if (background != null) background.enabled = true;
            IsVisible = true;
            RefreshScrollLayout();
        }

        public void ShowReadiness(LayoutReadinessReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            // A new confirmed report always starts collapsed; the full contract stays available.
            // 新的已确认报告默认收起；完整报告继续保留给诊断调用方。
            readinessSummary = PlacementFeedbackMapper.GetReadinessSummary(report);
            readinessDetails = PlacementFeedbackMapper.GetReadinessDetails(report);
            FullReadinessMessage = PlacementFeedbackMapper.GetPlayerMessage(report);
            if (appearance != null)
            {
                readinessSummary = appearance.ReadinessSummary(report);
                readinessDetails = appearance.ReadinessDetails(report, richText: true);
                FullReadinessMessage = appearance.ReadinessDiagnosticMessage(report);
            }
            IsDetailsExpanded = false;
            // IDs remain available to diagnostics without appearing in player-facing text.
            DiagnosticIds = report.Failures.SelectMany(f => new[]
                { f.InstanceId, f.SupportFurnitureInstanceId, f.SurfaceSlotId })
                .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
            // A healthy confirmed layout is useful state, but not an actionable HUD message.
            // 健康报告保留给诊断；只有 warning / blocking 问题占用玩家画面。
            if (report.CanOpenForBusiness && report.Failures.Count == 0)
            {
                var wasVisible = IsVisible;
                SetDisclosureVisible(false);
                HidePresentation();
                if (wasVisible) DetailsVisibilityChanged?.Invoke();
                return;
            }
            if (appearance != null)
            {
                var state = !report.CanOpenForBusiness ? "error" : "warning";
                appearance.Paint(background as Image, "notice_" + state);
                appearance.Paint(statusIcon, "status_" + state, false);
                AnimalCafe.UI.P8R.P8RButtonLayout.StatusIcon(statusIcon);
                if (statusIcon != null) statusIcon.enabled = true;
                messageLabel.font = appearance.Font;
                messageLabel.color = AnimalCafe.UI.P8R.P8RAppearance.Cocoa;
            }
            SetDisclosureVisible(readinessDetails.Length != 0);
            RefreshReadinessMessage();
            // P8R publishes only when its actual screen bounds change; legacy has no bounds publisher.
            // P8R 由真实屏幕边界变化通知；legacy 没有该路径，保留显式通知。
            if (appearance == null) DetailsVisibilityChanged?.Invoke();
        }

        private void ToggleReadinessDetails()
        {
            if (!IsVisible || !isActiveAndEnabled || !gameObject.activeInHierarchy
                || readinessDetails.Length == 0 || disclosureButton == null
                || !disclosureButton.gameObject.activeInHierarchy || !disclosureButton.IsInteractable())
                return;
            IsDetailsExpanded = !IsDetailsExpanded;
            disclosureLabel.text = DisclosureText;
            RefreshReadinessMessage();
            RefreshDisclosureIcon();
            if (appearance == null) DetailsVisibilityChanged?.Invoke();
        }

        // Preview changes only this note, never the confirmed report or disclosure state.
        // Preview 只切换这条备注，不修改已确认报告，也不自动展开/收起。
        public void SetPreviewPending(bool pending)
        {
            if (this == null || hasPendingPreview == pending) return;
            hasPendingPreview = pending;
            if (appearance != null && IsVisible && IsDetailsExpanded && readinessSummary.Length > 0)
                RefreshReadinessMessage();
        }

        private void RefreshReadinessMessage()
        {
            if (appearance == null)
            {
                ShowMessage(IsDetailsExpanded ? readinessSummary + "\n" + readinessDetails : readinessSummary);
                return;
            }
            if (!IsDetailsExpanded)
            {
                ShowMessage(readinessSummary);
                return;
            }
            var text = readinessSummary + "\n\n" + readinessDetails;
            if (hasPendingPreview) text += "\n\n" + appearance.Text("readiness.preview_hint");
            ShowMessage(text, richText: true);
        }

        private void SetDisclosureVisible(bool visible)
        {
            if (visible && disclosureButton == null)
            {
                var buttonObject = new GameObject("ReadinessDetails", typeof(RectTransform), typeof(Image), typeof(Button));
                buttonObject.transform.SetParent(transform, false);
                buttonObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, .12f);
                disclosureButton = buttonObject.GetComponent<Button>();
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(buttonObject.transform, false);
                var labelRect = (RectTransform)labelObject.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                disclosureLabel = labelObject.GetComponent<TMP_Text>();
                disclosureLabel.alignment = TextAlignmentOptions.Center;
                disclosureLabel.raycastTarget = false;
                disclosureLabel.richText = false;
                disclosureLabel.textWrappingMode = TextWrappingModes.NoWrap;
                disclosureButton.onClick.AddListener(ToggleReadinessDetails);
            }
            if (disclosureButton != null)
            {
                disclosureButton.gameObject.SetActive(visible);
                disclosureButton.interactable = visible;
                if (visible)
                {
                    disclosureLabel.font = messageLabel.font;
                    disclosureLabel.fontSize = messageLabel.fontSize;
                    disclosureLabel.color = messageLabel.color;
                    if (appearance != null)
                    {
                        if (disclosureButton.transform.Find("Icon") == null)
                        {
                            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                            iconObject.transform.SetParent(disclosureButton.transform, false);
                        }
                        appearance.Button(disclosureButton, "chevron_down", iconOnly: true);
                        RefreshDisclosureIcon();
                    }
                    disclosureLabel.text = DisclosureText;
                    disclosureButton.transform.SetAsLastSibling();
                }
            }
            ReserveDisclosureSpace(visible);
        }

        private void ReserveDisclosureSpace(bool visible)
        {
            var viewport = GetComponent<ScrollRect>()?.viewport;
            var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
            if (appearance != null)
            {
                // The compact strip keeps disclosure at the right; diagnostics use the same scroll area.
                // 详情按钮独占右侧，完整诊断仍在可滚动区域中，不覆盖正文。
                if (viewport != null)
                {
                    viewport.offsetMin = new Vector2(metrics.Units(40), metrics.Units(6));
                    viewport.offsetMax = new Vector2(-metrics.Units(visible ? 52 : 10), -metrics.Units(6));
                }
                if (visible && disclosureButton != null)
                {
                    var compact = (RectTransform)disclosureButton.transform;
                    compact.anchorMin = compact.anchorMax = compact.pivot = Vector2.one;
                    compact.anchoredPosition = Vector2.zero; compact.sizeDelta = Vector2.one * metrics.Units(48);
                    disclosureLabel.fontSize = metrics.Units(14);
                }
                return;
            }
            if (!visible || viewport != disclosureViewport)
            {
                if (disclosureViewport != null)
                    disclosureViewport.offsetMin = viewportOffsetBeforeDisclosure;
                disclosureViewport = null;
            }
            if (!visible) return;
            if (viewport != null && disclosureViewport == null)
            {
                disclosureViewport = viewport;
                viewportOffsetBeforeDisclosure = viewport.offsetMin;
            }
            var bottom = disclosureViewport != null ? viewportOffsetBeforeDisclosure.y : 8f;
            var buttonRect = (RectTransform)disclosureButton.transform;
            buttonRect.anchorMin = Vector2.zero;
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(.5f, 0f);
            buttonRect.offsetMin = new Vector2(12f, bottom);
            buttonRect.offsetMax = new Vector2(-12f, bottom + ActiveDisclosureHeight);
            if (disclosureViewport != null)
                disclosureViewport.offsetMin = viewportOffsetBeforeDisclosure
                    + Vector2.up * (ActiveDisclosureHeight + DisclosureGap);
        }

        private void RefreshDisclosureIcon()
        {
            if (appearance == null || disclosureButton == null) return;
            var icon = disclosureButton.transform.Find("Icon")?.GetComponent<Image>();
            if (icon == null) return;
            AnimalCafe.UI.P8R.P8RButtonLayout.IconButton(disclosureButton, 20f, 34f);
            icon.rectTransform.localRotation = Quaternion.Euler(0, 0, IsDetailsExpanded ? 180 : 0);
            icon.raycastTarget = false;
        }

        private void ResetReadinessDisclosure()
        {
            readinessSummary = readinessDetails = FullReadinessMessage = string.Empty;
            hasPendingPreview = false;
            IsDetailsExpanded = false;
            SetDisclosureVisible(false);
        }

        private void RefreshScrollLayout()
        {
            var scroll = GetComponent<ScrollRect>();
            if (refreshingLayout || scroll == null || scroll.viewport == null || scroll.content == null) return;
            refreshingLayout = true;
            try
            {
                if (appearance != null)
                {
                    RefreshMobileScrollLayout(scroll);
                    return;
                }
                var rect = (RectTransform)transform;
                var parent = transform.parent as RectTransform;
                var width = parent != null && parent.rect.width > 0
                    ? Mathf.Min(appearance != null ? 660f : 720f, Mathf.Max(240f, parent.rect.width - 40f)) : 720f;
                if (appearance != null)
                {
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                    rect.anchoredPosition = new Vector2(24, -168);
                }
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                messageLabel.textWrappingMode = TextWrappingModes.Normal;
                messageLabel.richText = false;
                Canvas.ForceUpdateCanvases();
                var preferred = messageLabel.GetPreferredValues(messageLabel.text,
                    scroll.viewport.rect.width, Mathf.Infinity);
                var disclosureSpace = appearance != null || readinessDetails.Length == 0 ? 0f : ActiveDisclosureHeight + DisclosureGap;
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    appearance == null ? Mathf.Clamp(Mathf.Ceil(preferred.y) + 16f + disclosureSpace, 64f, 200f)
                        : !IsDetailsExpanded ? 72f : Mathf.Clamp(Mathf.Ceil(preferred.y) + 24f, 72f, 200f));
                scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Ceil(preferred.y));
                scroll.enabled = true;
                scroll.horizontal = false;
                scroll.vertical = preferred.y > scroll.viewport.rect.height;
                scroll.StopMovement();
                scroll.verticalNormalizedPosition = 1f;
                var group = GetComponent<CanvasGroup>();
                if (group != null) { group.alpha = 1; group.interactable = true; group.blocksRaycasts = true; }
                if (scroll.verticalScrollbar != null) scroll.verticalScrollbar.gameObject.SetActive(scroll.vertical);
            }
            finally { refreshingLayout = false; }
        }

        private void RefreshMobileScrollLayout(ScrollRect scroll)
        {
            var rect = (RectTransform)transform;
            var parent = transform.parent as RectTransform;
            if (parent == null) return;
            var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
            p8rGeometryCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            var availableWidth = parent.rect.width - metrics.Units(16);
            var maximumWidth = metrics.Units(440);
            var width = Mathf.Min(maximumWidth, Mathf.Max(metrics.Units(180), availableWidth));
            var top = metrics.Units(8);
            hasP8RHudBottom = p8rHud != null;
            if (p8rHud != null)
            {
                lastP8RHudBottom = ReadP8RHudBottom(parent);
                top = lastP8RHudBottom + metrics.Units(8);
            }
            // Stretch phone strips with their existing safe-area parent; fixed anchors retained stale width.
            // 手机横条跟随已有 SafeArea 父级缩放，父级变化会触发尺寸回调；平板仍限制为440 logical。
            rect.anchorMin = rect.pivot = new Vector2(0, 1);
            rect.anchorMax = new Vector2(availableWidth <= maximumWidth ? 1 : 0, 1);
            rect.anchoredPosition = new Vector2(metrics.Units(8), -top);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            messageLabel.fontSize = metrics.Units(14);
            messageLabel.textWrappingMode = TextWrappingModes.Normal;
            ReserveDisclosureSpace(readinessDetails.Length > 0);
            var textWidth = width - metrics.Units(readinessDetails.Length > 0 ? 92 : 48);
            var preferred = messageLabel.GetPreferredValues(messageLabel.text, Mathf.Max(1, textWidth), Mathf.Infinity);
            var shortLandscape = metrics.LogicalViewport.x > metrics.LogicalViewport.y && metrics.LogicalViewport.y <= 400;
            var constrainedShortLandscape = shortLandscape && parent.rect.height < metrics.Units(320);
            var expandedLogicalLimit = constrainedShortLandscape ? 52 : shortLandscape ? 64 : 112;
            var limit = IsDetailsExpanded ? Mathf.Min(metrics.Units(expandedLogicalLimit), parent.rect.height * .24f) : metrics.Units(52);
            var height = Mathf.Clamp(Mathf.Ceil(preferred.y) + metrics.Units(12), metrics.Units(48), Mathf.Max(metrics.Units(48), limit));
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            scroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Ceil(preferred.y));
            scroll.enabled = true; scroll.horizontal = false;
            scroll.vertical = preferred.y > height - metrics.Units(12);
            scroll.StopMovement(); scroll.verticalNormalizedPosition = 1;
            AnimalCafe.UI.P8R.P8RButtonLayout.StatusIcon(statusIcon);
            RefreshDisclosureIcon();
            var group = GetComponent<CanvasGroup>();
            if (group != null) { group.alpha = 1; group.interactable = true; group.blocksRaycasts = true; }
            if (scroll.verticalScrollbar != null) scroll.verticalScrollbar.gameObject.SetActive(scroll.vertical);
            PublishP8RBoundsIfChanged();
        }

        private void OnEnable()
        {
            hasPublishedP8RBounds = false;
            AnimalCafe.UI.P8R.P8RMobileMetrics.Changed += RefreshVisibleLayout;
            RefreshVisibleLayout();
        }
        private void OnDisable()
        {
            AnimalCafe.UI.P8R.P8RMobileMetrics.Changed -= RefreshVisibleLayout;
            hasPublishedP8RBounds = false;
        }
        private void RefreshVisibleLayout() { if (IsVisible) RefreshScrollLayout(); }

        private float ReadP8RHudBottom(RectTransform parent) => parent.rect.yMax
            - parent.InverseTransformPoint(p8rHud.TransformPoint(new Vector2(0, p8rHud.rect.yMin))).y;

        private void LateUpdate()
        {
            if (this == null || !isActiveAndEnabled || appearance == null || !IsVisible
                || transform.parent is not RectTransform parent) return;
            // Separate Canvas branches can apply SafeArea in either Update order. Compare geometry only
            // after both branches settle; do not read gameplay state or rebuild unchanged TMP content.
            // HUD与提示的SafeArea分支更新顺序不固定；只比较最终边界，变化时才重排，不逐帧重建文字。
            if (p8rHud != null)
            {
                var bottom = ReadP8RHudBottom(parent);
                if (!hasP8RHudBottom || Mathf.Abs(bottom - lastP8RHudBottom) > .05f)
                    RefreshScrollLayout(); // 0.05 root-Canvas units ignores harmless float jitter.
            }
            PublishP8RBoundsIfChanged();
        }

        private void PublishP8RBoundsIfChanged()
        {
            if (this == null || !isActiveAndEnabled || !IsVisible || appearance == null || p8rGeometryCanvas == null) return;
            // A parent can move this card while local size/position remain unchanged (tablet width cap).
            // 父SafeArea平移时本地Rect不变；比较上次已发布的屏幕边界，不依赖分支回调顺序。
            ((RectTransform)transform).GetWorldCorners(p8rGeometryCorners);
            var camera = p8rGeometryCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : p8rGeometryCanvas.worldCamera;
            var minimum = RectTransformUtility.WorldToScreenPoint(camera, p8rGeometryCorners[0]);
            var maximum = minimum;
            for (var i = 1; i < p8rGeometryCorners.Length; i++)
            {
                var point = RectTransformUtility.WorldToScreenPoint(camera, p8rGeometryCorners[i]);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            var bounds = Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            if (hasPublishedP8RBounds && Mathf.Abs(bounds.xMin - publishedP8RScreenBounds.xMin) <= .25f
                && Mathf.Abs(bounds.yMin - publishedP8RScreenBounds.yMin) <= .25f
                && Mathf.Abs(bounds.xMax - publishedP8RScreenBounds.xMax) <= .25f
                && Mathf.Abs(bounds.yMax - publishedP8RScreenBounds.yMax) <= .25f) return;
            // Cache first: consumers may synchronously request layout again. No TMP rebuild here.
            // 先缓存再通知，阻止同步回调递归；纯父级移动只发通知，不重建文字。
            publishedP8RScreenBounds = bounds;
            hasPublishedP8RBounds = true;
            DetailsVisibilityChanged?.Invoke();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (IsVisible) RefreshScrollLayout();
        }

        public void Clear()
        {
            // Scene teardown can call cleanup after Unity has destroyed this component.
            if (this == null) return;
            ResetReadinessDisclosure();
            HidePresentation();
            DiagnosticIds = Array.Empty<string>();
        }

        private void HidePresentation()
        {
            if (messageLabel != null)
            {
                messageLabel.text = string.Empty;
                messageLabel.enabled = false;
            }

            if (background != null) background.enabled = false;
            if (statusIcon != null) statusIcon.enabled = false;
            var scroll = GetComponent<ScrollRect>();
            if (scroll != null) { scroll.StopMovement(); scroll.enabled = false; }
            var group = GetComponent<CanvasGroup>();
            if (group != null) { group.alpha = 0; group.interactable = false; group.blocksRaycasts = false; }

            IsVisible = false;
            hasPublishedP8RBounds = false;
        }
    }
}

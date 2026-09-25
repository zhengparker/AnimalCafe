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
        private bool hasChecklistReport;
        private LayoutReadinessReport checklistReport;
        private readonly RectTransform[] checklistRows = new RectTransform[3];
        private readonly TMP_Text[] checklistLabels = new TMP_Text[3];
        private readonly Image[] checklistIcons = new Image[3];
        private TMP_Text normalSummaryLabel;
        private Image checklistPanelFill;
        private Image checklistPanelBorder;
        private bool decorationMode;
        private RectTransform disclosureViewport;
        private Vector2 viewportOffsetBeforeDisclosure;
        private bool refreshingLayout;
        private RectTransform p8rHud;
        private bool hasP8RHudBottom;
        private float lastP8RHudBottom;
        private Vector2 lastP8RParentSize;
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

        // Mode changes only presentation; keep the confirmed readiness report intact.
        // 模式只影响显示，保留已确认布局的报告。
        public void SetDecorationMode(bool active)
        {
            if (this == null) return; // Owner cleanup may run after the view was destroyed.
            if (decorationMode == active) return;
            decorationMode = active;
            if (appearance != null && hasChecklistReport) ShowReadiness(checklistReport);
        }

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
            if (background != null) background.enabled = appearance == null || !hasChecklistReport;
            IsVisible = true;
            RefreshScrollLayout();
        }

        public void ShowReadiness(LayoutReadinessReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            // Keep the full diagnostic contract separate from the player-facing checklist.
            // 完整诊断继续保留；玩家界面只显示准备清单。
            readinessSummary = PlacementFeedbackMapper.GetReadinessSummary(report);
            readinessDetails = PlacementFeedbackMapper.GetReadinessDetails(report);
            FullReadinessMessage = PlacementFeedbackMapper.GetPlayerMessage(report);
            if (appearance != null)
            {
                readinessSummary = appearance.ChecklistSummary(report);
                readinessDetails = appearance.ChecklistDetails(report);
                FullReadinessMessage = appearance.ReadinessDiagnosticMessage(report);
                checklistReport = report;
                hasChecklistReport = true;
                IsDetailsExpanded = decorationMode;
                RefreshChecklistRows();
            }
            else IsDetailsExpanded = false;
            // IDs remain available to diagnostics without appearing in player-facing text.
            DiagnosticIds = report.Failures.SelectMany(f => new[]
                { f.InstanceId, f.SupportFurnitureInstanceId, f.SurfaceSlotId })
                .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
            // A healthy confirmed layout is useful state, but not an actionable HUD message.
            // 健康报告保留给诊断；只有 warning / blocking 问题占用玩家画面。
            if (appearance == null && report.CanOpenForBusiness && report.Failures.Count == 0)
            {
                var wasVisible = IsVisible;
                SetDisclosureVisible(false);
                HidePresentation();
                if (wasVisible) DetailsVisibilityChanged?.Invoke();
                return;
            }
            if (appearance != null)
            {
                if (background != null) background.enabled = false;
                if (statusIcon != null) statusIcon.enabled = false;
                messageLabel.font = appearance.Font;
                messageLabel.color = AnimalCafe.UI.P8R.P8RAppearance.Cocoa;
            }
            SetDisclosureVisible(appearance == null && readinessDetails.Length != 0);
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
        }

        private void RefreshReadinessMessage()
        {
            if (appearance == null)
            {
                ShowMessage(IsDetailsExpanded ? readinessSummary + "\n" + readinessDetails : readinessSummary);
                return;
            }
            if (normalSummaryLabel != null) normalSummaryLabel.enabled = false;
            SetChecklistPanelVisible(decorationMode || !checklistReport.CanOpenForBusiness);
            if (!decorationMode)
            {
                foreach (var row in checklistRows) if (row != null) row.gameObject.SetActive(false);
                if (checklistReport.CanOpenForBusiness)
                {
                    var wasVisible = IsVisible;
                    HidePresentation();
                    if (wasVisible) DetailsVisibilityChanged?.Invoke();
                    return;
                }
                if (normalSummaryLabel == null)
                {
                    var node = new GameObject("NormalReadinessSummary", typeof(RectTransform), typeof(TextMeshProUGUI));
                    node.transform.SetParent(transform, false);
                    normalSummaryLabel = node.GetComponent<TMP_Text>();
                    normalSummaryLabel.raycastTarget = false;
                    normalSummaryLabel.alignment = TextAlignmentOptions.TopLeft;
                    normalSummaryLabel.textWrappingMode = TextWrappingModes.Normal;
                }
                normalSummaryLabel.font = appearance.Font;
                normalSummaryLabel.fontSharedMaterial = appearance.Font.material;
                normalSummaryLabel.color = AnimalCafe.UI.P8R.P8RAppearance.Cocoa;
                normalSummaryLabel.UpdateMeshPadding();
                normalSummaryLabel.text = appearance.Text("readiness.normal.incomplete");
                normalSummaryLabel.enabled = true;
                ShowMessage(normalSummaryLabel.text);
            }
            else ShowMessage(readinessDetails, richText: true);
            messageLabel.enabled = false; // Retain CurrentMessage without drawing duplicate text.
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
                // Reserve a separate disclosure target; checklist rows share the existing scroll area.
                // 展开按钮独占右侧，清单沿用可滚动区域，不覆盖正文。
                if (viewport != null)
                {
                    viewport.offsetMin = new Vector2(metrics.Units(12), metrics.Units(6));
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
            SetChecklistPanelVisible(false);
            if (normalSummaryLabel != null) normalSummaryLabel.enabled = false;
            readinessSummary = readinessDetails = FullReadinessMessage = string.Empty;
            hasChecklistReport = false;
            checklistReport = null;
            foreach (var row in checklistRows) if (row != null) row.gameObject.SetActive(false);
            hasPendingPreview = false;
            IsDetailsExpanded = false;
            SetDisclosureVisible(false);
        }

        private void RefreshScrollLayout()
        {
            if (appearance != null && hasChecklistReport)
            {
                if (refreshingLayout) return;
                refreshingLayout = true;
                try { RefreshChecklistLayout(); } finally { refreshingLayout = false; }
                return;
            }
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

        // Separate opacity: translucent fill with the original opaque panel border.
        // 底色透明度独立设置；边框直接复用原 panel 素材，保持原色。
        private void EnsureChecklistPanel()
        {
            if (checklistPanelFill != null) return;
            Image CreateLayer(string name, bool fillCenter, float alpha)
            {
                var node = new GameObject(name, typeof(RectTransform), typeof(Image));
                node.transform.SetParent(transform, false);
                var image = node.GetComponent<Image>();
                appearance.Paint(image, "panel_cream");
                image.fillCenter = fillCenter;
                if (!fillCenter) image.sprite = AnimalCafe.UI.P8R.P8RButtonLayout.BorderSprite(image.sprite);
                image.color = new Color(1, 1, 1, alpha);
                image.raycastTarget = false;
                image.rectTransform.anchorMin = Vector2.zero;
                image.rectTransform.anchorMax = Vector2.one;
                image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
                return image;
            }
            checklistPanelFill = CreateLayer("ChecklistPanelFill", true, .75f);
            checklistPanelBorder = CreateLayer("ChecklistPanelBorder", false, 1f);
            checklistPanelBorder.transform.SetAsFirstSibling();
            checklistPanelFill.transform.SetAsFirstSibling();
            SetChecklistPanelVisible(decorationMode);
        }

        private void SetChecklistPanelVisible(bool visible)
        {
            if (checklistPanelFill != null) checklistPanelFill.enabled = visible;
            if (checklistPanelBorder != null) checklistPanelBorder.enabled = visible;
        }

        private void RefreshChecklistRows()
        {
            EnsureChecklistPanel();
            var texts = appearance.ChecklistRows(checklistReport);
            var summaries = new[] { checklistReport.CashRegisters, checklistReport.CoffeeMachines, checklistReport.PickUpPoints };
            for (var i = 0; i < checklistRows.Length; i++)
            {
                if (checklistRows[i] == null)
                {
                    var row = new GameObject("ChecklistRow" + i, typeof(RectTransform));
                    row.transform.SetParent(transform, false);
                    checklistRows[i] = (RectTransform)row.transform;
                    var icon = new GameObject("Status", typeof(RectTransform), typeof(Image));
                    icon.transform.SetParent(row.transform, false);
                    checklistIcons[i] = icon.GetComponent<Image>();
                    checklistIcons[i].preserveAspect = true;
                    var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                    label.transform.SetParent(row.transform, false);
                    checklistLabels[i] = label.GetComponent<TMP_Text>();
                }
                checklistRows[i].gameObject.SetActive(true);
                checklistLabels[i].font = appearance.Font;
                checklistLabels[i].color = AnimalCafe.UI.P8R.P8RAppearance.Cocoa;
                checklistLabels[i].fontSharedMaterial = appearance.Font.material;
                checklistLabels[i].UpdateMeshPadding();
                checklistLabels[i].alignment = TextAlignmentOptions.TopLeft;
                checklistLabels[i].textWrappingMode = TextWrappingModes.Normal;
                checklistLabels[i].richText = true;
                checklistLabels[i].text = texts[i];
                var summary = summaries[i];
                checklistIcons[i].sprite = AnimalCafe.UI.P8R.P8RStatusIcons.Get(
                    summary.ValidCount > 0 ? 1 : summary.TotalCount == 0 ? 0 : 2);
                checklistIcons[i].color = Color.white;

            }
            // This informational list must never claim a scene gesture.
            // 清单只显示状态；所有图形都让场景输入穿透。
            foreach (var graphic in GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            if (background != null) background.enabled = false;
            if (statusIcon != null) statusIcon.enabled = false;
            var group = GetComponent<CanvasGroup>();
            if (group != null) { group.alpha = 1; group.interactable = false; group.blocksRaycasts = false; }
            var scroll = GetComponent<ScrollRect>();
            if (scroll != null)
            {
                scroll.StopMovement(); scroll.enabled = false; scroll.vertical = scroll.horizontal = false;
                if (scroll.verticalScrollbar != null) scroll.verticalScrollbar.gameObject.SetActive(false);
            }
        }

        private void RefreshChecklistLayout()
        {
            var rect = (RectTransform)transform;
            var parent = transform.parent as RectTransform;
            var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
            p8rGeometryCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            var left = metrics.Units(8);
            var top = metrics.Units(8);
            if (parent != null)
            {
                lastP8RParentSize = parent.rect.size;
                if (p8rHud != null)
                {
                    var badge = p8rHud.Find("P8RModeBadge") as RectTransform;
                    if (badge != null)
                        left = parent.InverseTransformPoint(badge.TransformPoint(new Vector2(badge.rect.xMin, 0))).x - parent.rect.xMin;
                    lastP8RHudBottom = ReadP8RHudBottom(parent);
                    hasP8RHudBottom = true;
                    top = lastP8RHudBottom + metrics.Units(8);
                }
            }
            var width = Mathf.Max(metrics.Units(48), Mathf.Min(metrics.Units(300),
                parent != null ? parent.rect.width - left - metrics.Units(8) : metrics.Units(300)));
            var padding = metrics.Units(8);
            if (!decorationMode && normalSummaryLabel != null)
            {
                normalSummaryLabel.fontSize = metrics.Units(14);
                var preferred = normalSummaryLabel.GetPreferredValues(normalSummaryLabel.text, width - padding * 2, Mathf.Infinity);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(left, -top);
                rect.sizeDelta = new Vector2(Mathf.Min(width, Mathf.Ceil(preferred.x) + padding * 2), Mathf.Ceil(preferred.y) + padding * 2);
                var summaryRect = normalSummaryLabel.rectTransform;
                summaryRect.anchorMin = Vector2.zero;
                summaryRect.anchorMax = Vector2.one;
                // Reuse the checklist panel padding for the Normal reminder.
                // Normal 提示沿用清单面板留白。
                summaryRect.offsetMin = Vector2.one * padding;
                summaryRect.offsetMax = -Vector2.one * padding;
                PublishP8RBoundsIfChanged();
                return;
            }
            var iconSize = metrics.Units(14);
            var textLeft = metrics.Units(20);
            var gap = metrics.Units(4);
            width -= padding * 2;
            float usedWidth = 0, height = padding;
            for (var i = 0; i < checklistRows.Length; i++)
            {
                var label = checklistLabels[i];
                label.fontSize = metrics.Units(12);
                var preferred = label.GetPreferredValues(label.text, width - textLeft, Mathf.Infinity);
                var rowHeight = Mathf.Max(iconSize, Mathf.Ceil(preferred.y));
                usedWidth = Mathf.Max(usedWidth, Mathf.Min(width, textLeft + Mathf.Ceil(preferred.x)));
                var row = checklistRows[i];
                row.anchorMin = row.anchorMax = row.pivot = new Vector2(0, 1);
                row.anchoredPosition = new Vector2(padding, -height);
                row.sizeDelta = new Vector2(width, rowHeight);
                var icon = checklistIcons[i].rectTransform;
                icon.anchorMin = icon.anchorMax = icon.pivot = new Vector2(0, 1);
                icon.anchoredPosition = Vector2.zero;
                icon.sizeDelta = Vector2.one * iconSize;
                var textRect = label.rectTransform;
                textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(textLeft, 0); textRect.offsetMax = Vector2.zero;
                height += rowHeight + (i < checklistRows.Length - 1 ? gap : 0);
            }
            // Publish the actual three-row footprint to existing obstacle consumers.
            // 整体Rect保留真实三行边界，沿用原有障碍通知。
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(usedWidth + padding * 2, height + padding);
            foreach (var row in checklistRows) row.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, usedWidth);
            // Align to visible first-line glyphs, not TMP's extra font ascender space.
            // 对齐首行实际字形中心；原因说明换行时，icon仍跟随名称那一行。
            for (var i = 0; i < checklistRows.Length; i++)
            {
                var label = checklistLabels[i];
                label.ForceMeshUpdate();
                var bottom = float.PositiveInfinity;
                var topInk = float.NegativeInfinity;
                for (var j = 0; j < label.textInfo.characterCount; j++)
                {
                    var character = label.textInfo.characterInfo[j];
                    if (character.lineNumber != 0 || !character.isVisible) continue;
                    bottom = Mathf.Min(bottom, character.bottomLeft.y);
                    topInk = Mathf.Max(topInk, character.topRight.y);
                }
                if (float.IsInfinity(bottom) || float.IsInfinity(topInk)) continue;
                var center = checklistRows[i].InverseTransformPoint(label.transform.TransformPoint(
                    new Vector3(0, (bottom + topInk) * .5f, 0)));
                checklistIcons[i].rectTransform.anchoredPosition = new Vector2(0, center.y + iconSize * .5f);
            }
            PublishP8RBoundsIfChanged();
        }
        private void RefreshMobileScrollLayout(ScrollRect scroll)
        {
            var rect = (RectTransform)transform;
            var parent = transform.parent as RectTransform;
            if (parent == null) return;
            lastP8RParentSize = parent.rect.size;
            var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
            p8rGeometryCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
            var availableWidth = parent.rect.width - metrics.Units(16);
            var shortLandscape = metrics.LogicalViewport.x > metrics.LogicalViewport.y && metrics.LogicalViewport.y <= 400;
            // A short landscape checklist shares the row with the complete placement instruction.
            // 短横屏最多占一半可用宽度，给左侧指令及中央 preview 留出空间；长文字继续滚动。
            var maximumWidth = shortLandscape
                ? Mathf.Min(metrics.Units(300), Mathf.Max(metrics.Units(180), availableWidth * .5f))
                : metrics.Units(300);
            var width = Mathf.Min(availableWidth, maximumWidth);
            var top = metrics.Units(8);
            hasP8RHudBottom = p8rHud != null;
            if (p8rHud != null)
            {
                lastP8RHudBottom = ReadP8RHudBottom(parent);
                top = lastP8RHudBottom + metrics.Units(12);
            }
            // Follow the right safe-area edge while allowing narrow parents to resize the card.
            // 清单靠右对齐 SafeArea；窄屏仍随父级变化重新计算宽度。
            rect.anchorMin = new Vector2(availableWidth <= maximumWidth ? 0 : 1, 1);
            rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-metrics.Units(8), -top);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            messageLabel.fontSize = metrics.Units(14);
            messageLabel.textWrappingMode = TextWrappingModes.Normal;
            ReserveDisclosureSpace(readinessDetails.Length > 0);
            var textWidth = width - metrics.Units(readinessDetails.Length > 0 ? 64 : 22);
            var preferred = messageLabel.GetPreferredValues(messageLabel.text, Mathf.Max(1, textWidth), Mathf.Infinity);
            var constrainedShortLandscape = shortLandscape && parent.rect.height < metrics.Units(320);
            var expandedLogicalLimit = constrainedShortLandscape ? 88 : 112;
            var limit = IsDetailsExpanded ? Mathf.Min(metrics.Units(expandedLogicalLimit), parent.rect.height * .32f) : metrics.Units(52);
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

        private float ReadP8RHudBottom(RectTransform parent)
        {
            var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
            var modeButton = p8rHud.Find("DecorationModeButton") as RectTransform;
            // On narrow screens the second time row needs its own space; elsewhere anchor to Decor.
            // 窄屏给第二行时间按钮留空，其他尺寸直接跟随右侧 Decor 底边。
            var anchor = hasChecklistReport ? p8rHud : modeButton != null && parent.rect.width >= metrics.Units(448) ? modeButton : p8rHud;
            return parent.rect.yMax - parent.InverseTransformPoint(
                anchor.TransformPoint(new Vector2(0, anchor.rect.yMin))).y;
        }

        private void LateUpdate()
        {
            if (this == null || !isActiveAndEnabled || appearance == null || !IsVisible
                || transform.parent is not RectTransform parent) return;
            // A capped right-anchored card does not receive a width callback when its parent shrinks.
            // 靠右且限宽的卡片不会自动收到父宽度变化，需在 SafeArea 更新后重新限宽。
            if ((parent.rect.size - lastP8RParentSize).sqrMagnitude > .0025f)
                RefreshScrollLayout();
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
            SetChecklistPanelVisible(false);
            if (normalSummaryLabel != null) normalSummaryLabel.enabled = false;
            foreach (var row in checklistRows) if (row != null) row.gameObject.SetActive(false);
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

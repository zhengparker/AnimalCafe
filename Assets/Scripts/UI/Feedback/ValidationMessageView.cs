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
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private UnityEngine.UI.Graphic background;

        public bool IsVisible { get; private set; }
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

            var ids = diagnosticIds == null
                ? Array.Empty<string>()
                : diagnosticIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            DiagnosticIds = ids;
            // Keep the player message readable; detached IDs remain available for diagnostics.
            // 玩家只看说明文字；独立保存原始 IDs，供排查问题使用。
            messageLabel.text = message;
            messageLabel.enabled = true;
            if (background != null) background.enabled = true;
            IsVisible = true;
            RefreshScrollLayout();
        }

        public void ShowReadiness(LayoutReadinessReport report)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            ShowStatus(PlacementFeedbackMapper.GetPlayerMessage(report));
            // IDs remain available to diagnostics without appearing in player-facing text.
            DiagnosticIds = report.Failures.SelectMany(f => new[]
                { f.InstanceId, f.SupportFurnitureInstanceId, f.SurfaceSlotId })
                .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        }

        private void RefreshScrollLayout()
        {
            var scroll = GetComponent<ScrollRect>();
            if (scroll == null || scroll.viewport == null || scroll.content == null) return;
            var rect = (RectTransform)transform;
            var parent = transform.parent as RectTransform;
            var width = parent != null && parent.rect.width > 0
                ? Mathf.Min(720f, Mathf.Max(240f, parent.rect.width - 40f)) : 720f;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            messageLabel.textWrappingMode = TextWrappingModes.Normal;
            messageLabel.richText = false;
            Canvas.ForceUpdateCanvases();
            var preferred = messageLabel.GetPreferredValues(messageLabel.text,
                scroll.viewport.rect.width, Mathf.Infinity);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Clamp(Mathf.Ceil(preferred.y) + 16f, 64f, 200f));
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

        public void Clear()
        {
            // Scene teardown can call cleanup after Unity has destroyed this component.
            if (this == null) return;
            if (messageLabel != null)
            {
                messageLabel.text = string.Empty;
                messageLabel.enabled = false;
            }

            if (background != null) background.enabled = false;
            var scroll = GetComponent<ScrollRect>();
            if (scroll != null) { scroll.StopMovement(); scroll.enabled = false; }
            var group = GetComponent<CanvasGroup>();
            if (group != null) { group.alpha = 0; group.interactable = false; group.blocksRaycasts = false; }

            IsVisible = false;
            DiagnosticIds = Array.Empty<string>();
        }
    }
}

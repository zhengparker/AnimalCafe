using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AnimalCafe.UI.Feedback
{
    /// <summary>
    /// Touch-first Tooltip entry point. Opening requires an explicit tap, never Hover.
    /// Touch-first Tooltip 入口；需要明确 tap，不依赖 Hover。
    /// </summary>
    public sealed class TooltipView : MonoBehaviour, IPointerClickHandler
    {
        private TMP_Text messageLabel;
        private GameObject contentRoot;
        private string message = string.Empty;

        public void Configure(TMP_Text label, GameObject content)
        {
            messageLabel = label ?? throw new ArgumentNullException(nameof(label));
            contentRoot = content ?? throw new ArgumentNullException(nameof(content));
            contentRoot.SetActive(false);
        }

        public void SetMessage(string specificMessage)
        {
            if (string.IsNullOrWhiteSpace(specificMessage))
            {
                throw new ArgumentException(
                    "Tooltip message must describe the requested information.",
                    nameof(specificMessage));
            }

            message = specificMessage;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (messageLabel == null || contentRoot == null)
            {
                return;
            }

            messageLabel.text = message;
            contentRoot.SetActive(true);
        }

        public void Close()
        {
            if (contentRoot != null)
            {
                contentRoot.SetActive(false);
            }
        }
    }
}

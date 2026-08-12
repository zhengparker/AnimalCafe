using System;
using TMPro;
using UnityEngine;

namespace AnimalCafe.UI.Feedback
{
    /// <summary>
    /// Displays a specific validation reason until its caller reports corrected input.
    /// 显示具体 validation 原因，直到调用方报告输入已修正。
    /// </summary>
    public sealed class ValidationMessageView : MonoBehaviour
    {
        private TMP_Text messageLabel;

        public bool IsVisible { get; private set; }

        public void Configure(TMP_Text label)
        {
            messageLabel = label ?? throw new ArgumentNullException(nameof(label));
            Clear();
        }

        public void SetValidationResult(bool isValid, string specificReason)
        {
            if (isValid)
            {
                Clear();
                return;
            }

            if (string.IsNullOrWhiteSpace(specificReason))
            {
                throw new ArgumentException(
                    "Invalid input requires a specific validation reason.",
                    nameof(specificReason));
            }

            messageLabel.text = specificReason;
            messageLabel.enabled = true;
            IsVisible = true;
        }

        private void Clear()
        {
            if (messageLabel != null)
            {
                messageLabel.text = string.Empty;
                messageLabel.enabled = false;
            }

            IsVisible = false;
        }
    }
}

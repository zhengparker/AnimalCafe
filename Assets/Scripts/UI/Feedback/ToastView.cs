using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.Feedback
{
    /// <summary>
    /// Presents the current queue item. It does not decide when gameplay creates a Toast.
    /// 只负责显示 queue 当前项目，不决定 gameplay 何时创建 Toast。
    /// </summary>
    public sealed class ToastView : MonoBehaviour
    {
        private ToastQueue queue;
        private TMP_Text messageLabel;
        private Graphic[] graphics = Array.Empty<Graphic>();

        public bool IsVisible { get; private set; }

        public void Configure(ToastQueue toastQueue, TMP_Text label, Graphic[] toastGraphics)
        {
            queue = toastQueue ?? throw new ArgumentNullException(nameof(toastQueue));
            messageLabel = label ?? throw new ArgumentNullException(nameof(label));
            graphics = toastGraphics ?? throw new ArgumentNullException(nameof(toastGraphics));

            foreach (var graphic in graphics)
            {
                if (graphic != null)
                {
                    // Toast feedback must never intercept Touch input.
                    // Toast 提示绝不能拦截 Touch input。
                    graphic.raycastTarget = false;
                }
            }

            SetVisible(false);
        }

        private void Update()
        {
            if (queue == null || !queue.TryGetCurrent(out var current))
            {
                SetVisible(false);
                return;
            }

            messageLabel.text = current.MergeCount > 1
                ? $"{current.Message.Content} ×{current.MergeCount}"
                : current.Message.Content;
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            IsVisible = visible;
            foreach (var graphic in graphics)
            {
                if (graphic != null)
                {
                    graphic.enabled = visible;
                }
            }

            if (!visible && messageLabel != null)
            {
                messageLabel.text = string.Empty;
            }
        }
    }
}

using System;
using AnimalCafe.UI.Foundation;
using TMPro;
using UnityEngine;

namespace AnimalCafe.UI.Components
{
    /// <summary>
    /// Converts a device Safe Area into normalized anchors and applies shared localized-text rules.
    /// 将设备 Safe Area 转换为标准化 anchors，并应用共用的本地化文字规则。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaContainer : MonoBehaviour
    {
        /// <summary>
        /// Applies a supplied Safe Area to this container's anchors.
        /// 将指定的 Safe Area 应用到当前容器的 anchors。
        /// </summary>
        public void ApplySafeArea(Rect safeArea, Vector2 screenSize)
        {
            var normalized = CalculateNormalizedSafeRect(safeArea, screenSize);
            var target = (RectTransform)transform;
            target.anchorMin = normalized.min;
            target.anchorMax = normalized.max;
            target.offsetMin = Vector2.zero;
            target.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Returns a normalized, non-inverted rect in the 0-1 range.
        /// 返回限制在 0-1 范围内且不会反转的标准化矩形。
        /// </summary>
        public static Rect CalculateNormalizedSafeRect(Rect safeArea, Vector2 screenSize)
        {
            if (screenSize.x <= 0f || screenSize.y <= 0f)
            {
                return new Rect(0f, 0f, 1f, 1f);
            }

            var firstX = safeArea.x / screenSize.x;
            var secondX = (safeArea.x + safeArea.width) / screenSize.x;
            var firstY = safeArea.y / screenSize.y;
            var secondY = (safeArea.y + safeArea.height) / screenSize.y;

            var xMin = Mathf.Clamp01(Mathf.Min(firstX, secondX));
            var xMax = Mathf.Clamp01(Mathf.Max(firstX, secondX));
            var yMin = Mathf.Clamp01(Mathf.Min(firstY, secondY));
            var yMax = Mathf.Clamp01(Mathf.Max(firstY, secondY));

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        /// <summary>
        /// Keeps localized Body and Label text readable instead of shrinking it to fit.
        /// 本地化 Body 与 Label 文字通过换行扩展，不靠缩小字号塞入空间。
        /// </summary>
        public static void ConfigureLocalizedText(TMP_Text text, UiTextStyle style)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var minimumFontSize = style switch
            {
                UiTextStyle.Body => AnimalCafeUiTheme.MinimumBodyFontSize,
                UiTextStyle.Label => AnimalCafeUiTheme.MinimumLabelFontSize,
                _ => 0f
            };

            text.enableAutoSizing = false;
            text.fontSize = Mathf.Max(text.fontSize, minimumFontSize);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
        }
    }
}

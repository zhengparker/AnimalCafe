using System;
using AnimalCafe.UI.Foundation;
using TMPro;
using UnityEngine;

namespace AnimalCafe.UI.Components
{
    /// <summary>
    /// Applies one semantic typography token to a TMP label.
    /// 将一个语义 typography token 应用到 TMP label。
    /// </summary>
    public sealed class AnimalCafeTextStyle : MonoBehaviour
    {
        public void Configure(AnimalCafeUiTheme theme, UiTextStyle style, TMP_Text target)
        {
            if (theme == null)
            {
                throw new ArgumentNullException(nameof(theme));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            var token = style switch
            {
                UiTextStyle.Heading => theme.Typography.Heading,
                UiTextStyle.Body => theme.Typography.Body,
                UiTextStyle.Label => theme.Typography.Label,
                _ => theme.Typography.Body
            };

            target.font = token.FontAsset;
            target.fontSize = token.FontSize;
            target.fontStyle = token.FontStyle;
            target.lineSpacing = token.LineSpacing;
            SafeAreaContainer.ConfigureLocalizedText(target, style);
        }
    }
}

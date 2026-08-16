using System;
using TMPro;
using UnityEngine;

namespace AnimalCafe.UI.Foundation
{
    public enum UiButtonRole
    {
        Primary,
        Secondary,
        Destructive
    }

    public enum UiButtonState
    {
        Default,
        Pressed,
        Disabled
    }

    public enum UiPanelStyle
    {
        Solid,
        LightFrost,
        StrongFrost
    }

    public enum UiTextStyle
    {
        Heading,
        Body,
        Label
    }

    [Serializable]
    public struct UiSemanticColorTokens
    {
        public Color Background;
        public Color Surface;
        public Color Text;
        public Color Accent;
        public Color Disabled;
        public Color Warning;
        public Color Destructive;
    }

    [Serializable]
    public struct UiTextStyleToken
    {
        public TMP_FontAsset FontAsset;
        public float FontSize;
        public FontStyles FontStyle;
        public float LineSpacing;

        public UiTextStyleToken(
            TMP_FontAsset fontAsset,
            float fontSize,
            FontStyles fontStyle,
            float lineSpacing)
        {
            FontAsset = fontAsset;
            FontSize = fontSize;
            FontStyle = fontStyle;
            LineSpacing = lineSpacing;
        }
    }

    [Serializable]
    public struct UiTypographyTokens
    {
        public UiTextStyleToken Heading;
        public UiTextStyleToken Body;
        public UiTextStyleToken Label;
    }

    [Serializable]
    public struct UiSpacingTokens
    {
        public float ExtraSmall;
        public float Small;
        public float Medium;
        public float Large;
        public float ExtraLarge;

        public UiSpacingTokens(float extraSmall, float small, float medium, float large, float extraLarge)
        {
            ExtraSmall = extraSmall;
            Small = small;
            Medium = medium;
            Large = large;
            ExtraLarge = extraLarge;
        }
    }

    [Serializable]
    public struct UiShapeTokens
    {
        public float CornerRadius;
        public float BorderWidth;

        public UiShapeTokens(float cornerRadius, float borderWidth)
        {
            CornerRadius = cornerRadius;
            BorderWidth = borderWidth;
        }
    }

    [Serializable]
    public struct UiMaterialTokens
    {
        public Material Solid;
        public Material LightFrost;
        public Material StrongFrost;
        public Material StrongFrostFallback;

        public UiMaterialTokens(
            Material solid,
            Material lightFrost,
            Material strongFrost,
            Material strongFrostFallback)
        {
            Solid = solid;
            LightFrost = lightFrost;
            StrongFrost = strongFrost;
            StrongFrostFallback = strongFrostFallback;
        }
    }

    [Serializable]
    public struct UiMotionTokens
    {
        public float ButtonPressDuration;
        public float BottomSheetOpenDuration;
        public float ModalOpenDuration;
        public float ToastFadeInDuration;
        public float ToastDefaultStayDuration;

        public UiMotionTokens(
            float buttonPressDuration,
            float bottomSheetOpenDuration,
            float modalOpenDuration,
            float toastFadeInDuration,
            float toastDefaultStayDuration)
        {
            ButtonPressDuration = buttonPressDuration;
            BottomSheetOpenDuration = bottomSheetOpenDuration;
            ModalOpenDuration = modalOpenDuration;
            ToastFadeInDuration = toastFadeInDuration;
            ToastDefaultStayDuration = toastDefaultStayDuration;
        }
    }

    [Serializable]
    public struct UiSizeTokens
    {
        public float MinimumTouchTargetWidth;
        public float MinimumTouchTargetHeight;
        public float SmallIconContainerSize;
        public float LargeIconContainerSize;

        public UiSizeTokens(
            float minimumTouchTargetWidth,
            float minimumTouchTargetHeight,
            float smallIconContainerSize,
            float largeIconContainerSize)
        {
            MinimumTouchTargetWidth = minimumTouchTargetWidth;
            MinimumTouchTargetHeight = minimumTouchTargetHeight;
            SmallIconContainerSize = smallIconContainerSize;
            LargeIconContainerSize = largeIconContainerSize;
        }
    }
}

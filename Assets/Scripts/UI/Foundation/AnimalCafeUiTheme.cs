using System.Collections.Generic;
using UnityEngine;

namespace AnimalCafe.UI.Foundation
{
    [CreateAssetMenu(menuName = "AnimalCafe/UI/Theme")]
    public sealed class AnimalCafeUiTheme : ScriptableObject
    {
        public const float MinimumBodyFontSize = 16f;
        public const float MinimumLabelFontSize = 14f;
        public const float MinimumTouchTargetSize = 48f;
        public const float MinimumButtonPressDuration = 0.08f;
        public const float MaximumButtonPressDuration = 0.12f;
        public const float BottomSheetOpenDuration = 0.22f;
        public const float ModalOpenDuration = 0.18f;
        public const float ToastFadeInDuration = 0.16f;
        public const float ToastDefaultStayDuration = 2.5f;

        [SerializeField] private UiSemanticColorTokens colors;
        [SerializeField] private UiTypographyTokens typography;
        [SerializeField] private UiSpacingTokens spacing;
        [SerializeField] private UiShapeTokens shape;
        [SerializeField] private UiMaterialTokens materials;
        [SerializeField] private UiMotionTokens motion;
        [SerializeField] private UiSizeTokens sizes;

        public UiSemanticColorTokens Colors { get => colors; set => colors = value; }
        public UiTypographyTokens Typography { get => typography; set => typography = value; }
        public UiSpacingTokens Spacing { get => spacing; set => spacing = value; }
        public UiShapeTokens Shape { get => shape; set => shape = value; }
        public UiMaterialTokens Materials { get => materials; set => materials = value; }
        public UiMotionTokens Motion { get => motion; set => motion = value; }
        public UiSizeTokens Sizes { get => sizes; set => sizes = value; }

        public void Validate(List<string> issues)
        {
            if (issues == null)
            {
                throw new System.ArgumentNullException(nameof(issues));
            }

            ValidateColor(colors.Background, "BACKGROUND", issues);
            ValidateColor(colors.Surface, "SURFACE", issues);
            ValidateColor(colors.Text, "TEXT", issues);
            ValidateColor(colors.Accent, "ACCENT", issues);
            ValidateColor(colors.Disabled, "DISABLED", issues);
            ValidateColor(colors.Warning, "WARNING", issues);
            ValidateColor(colors.Destructive, "DESTRUCTIVE", issues);

            ValidateTextStyle(typography.Heading, UiTextStyle.Heading, 0f, issues);
            ValidateTextStyle(typography.Body, UiTextStyle.Body, MinimumBodyFontSize, issues);
            ValidateTextStyle(typography.Label, UiTextStyle.Label, MinimumLabelFontSize, issues);

            ValidatePositive(spacing.ExtraSmall, "SPACING_EXTRA_SMALL", issues);
            ValidatePositive(spacing.Small, "SPACING_SMALL", issues);
            ValidatePositive(spacing.Medium, "SPACING_MEDIUM", issues);
            ValidatePositive(spacing.Large, "SPACING_LARGE", issues);
            ValidatePositive(spacing.ExtraLarge, "SPACING_EXTRA_LARGE", issues);
            ValidatePositive(shape.CornerRadius, "CORNER_RADIUS", issues);
            ValidatePositive(shape.BorderWidth, "BORDER_WIDTH", issues);

            ValidateMaterial(materials.Solid, "SOLID", issues);
            ValidateMaterial(materials.LightFrost, "LIGHT_FROST", issues);
            ValidateMaterial(materials.StrongFrost, "STRONG_FROST", issues);
            ValidateMaterial(materials.StrongFrostFallback, "STRONG_FROST_FALLBACK", issues);

            ValidateMotion(issues);

            if (sizes.MinimumTouchTargetWidth < MinimumTouchTargetSize ||
                sizes.MinimumTouchTargetHeight < MinimumTouchTargetSize)
            {
                AddIssue(issues, "MINIMUM_TOUCH_TARGET_BELOW_48X48", "MinimumTouchTarget");
            }

            ValidatePositive(sizes.SmallIconContainerSize, "SMALL_ICON_CONTAINER_SIZE", issues);
            ValidatePositive(sizes.LargeIconContainerSize, "LARGE_ICON_CONTAINER_SIZE", issues);
        }

        private void ValidateColor(Color color, string tokenName, List<string> issues)
        {
            if (color.a <= 0f)
            {
                AddIssue(issues, "MISSING_COLOR_" + tokenName, "Colors/" + tokenName);
            }
        }

        private void ValidateTextStyle(
            UiTextStyleToken style,
            UiTextStyle styleName,
            float minimumFontSize,
            List<string> issues)
        {
            if (style.FontAsset == null)
            {
                AddIssue(
                    issues,
                    "MISSING_TYPOGRAPHY_" + styleName.ToString().ToUpperInvariant() + "_FONT",
                    styleName.ToString());
            }

            if (minimumFontSize > 0f && style.FontSize < minimumFontSize)
            {
                AddIssue(
                    issues,
                    styleName.ToString().ToUpperInvariant() + "_FONT_SIZE_BELOW_MINIMUM",
                    styleName.ToString());
            }
        }

        private void ValidateMaterial(Material material, string tokenName, List<string> issues)
        {
            if (material == null)
            {
                AddIssue(issues, "MISSING_MATERIAL_" + tokenName, "Materials/" + tokenName);
            }
        }

        private void ValidateMotion(List<string> issues)
        {
            if (motion.ButtonPressDuration < MinimumButtonPressDuration ||
                motion.ButtonPressDuration > MaximumButtonPressDuration)
            {
                AddIssue(issues, "BUTTON_PRESS_DURATION_OUT_OF_RANGE", "Motion/ButtonPressDuration");
            }

            ValidateMotionDuration(
                motion.BottomSheetOpenDuration,
                BottomSheetOpenDuration,
                "BOTTOM_SHEET_OPEN_DURATION_OUT_OF_RANGE",
                "Motion/BottomSheetOpenDuration",
                issues);
            ValidateMotionDuration(
                motion.ModalOpenDuration,
                ModalOpenDuration,
                "MODAL_OPEN_DURATION_OUT_OF_RANGE",
                "Motion/ModalOpenDuration",
                issues);
            ValidateMotionDuration(
                motion.ToastFadeInDuration,
                ToastFadeInDuration,
                "TOAST_FADE_IN_DURATION_OUT_OF_RANGE",
                "Motion/ToastFadeInDuration",
                issues);
            ValidateMotionDuration(
                motion.ToastDefaultStayDuration,
                ToastDefaultStayDuration,
                "TOAST_DEFAULT_STAY_DURATION_OUT_OF_RANGE",
                "Motion/ToastDefaultStayDuration",
                issues);
        }

        private void ValidateMotionDuration(
            float duration,
            float expectedDuration,
            string issueCode,
            string tokenPath,
            List<string> issues)
        {
            if (!Mathf.Approximately(duration, expectedDuration))
            {
                AddIssue(issues, issueCode, tokenPath);
            }
        }

        private void ValidatePositive(float value, string tokenName, List<string> issues)
        {
            if (value <= 0f)
            {
                AddIssue(issues, "MISSING_OR_INVALID_" + tokenName, tokenName);
            }
        }

        private void AddIssue(List<string> issues, string issueCode, string tokenPath)
        {
            issues.Add(issueCode + ": " + name + "/" + tokenPath);
        }
    }
}

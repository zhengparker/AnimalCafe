using System.Collections.Generic;
using System.Linq;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace AnimalCafe.Tests.Phase5
{
    public sealed class AnimalCafeUiThemeTests
    {
        private readonly List<Object> ownedObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var ownedObject in ownedObjects)
            {
                Object.DestroyImmediate(ownedObject);
            }

            ownedObjects.Clear();
        }

        [Test]
        public void Validate_CompleteSemanticTheme_ReturnsNoIssues()
        {
            var theme = CreateCompleteTheme();
            var issues = new List<string>();

            theme.Validate(issues);

            Assert.That(issues, Is.Empty);
        }

        [Test]
        public void Validate_MissingBodyFont_ReturnsSemanticIssueWithThemeContext()
        {
            var theme = CreateCompleteTheme();
            theme.Typography = new UiTypographyTokens
            {
                Heading = theme.Typography.Heading,
                Body = new UiTextStyleToken(null, 16f, FontStyles.Normal, 0f),
                Label = theme.Typography.Label
            };
            var issues = new List<string>();

            theme.Validate(issues);

            Assert.That(issues, Does.Contain("MISSING_TYPOGRAPHY_BODY_FONT: ThemeFixture/Body"));
        }

        [TestCase(UiTextStyle.Body, 15f, "BODY_FONT_SIZE_BELOW_MINIMUM")]
        [TestCase(UiTextStyle.Label, 13f, "LABEL_FONT_SIZE_BELOW_MINIMUM")]
        public void Validate_TextBelowBaseline_ReturnsSpecificIssue(
            UiTextStyle style,
            float fontSize,
            string expectedIssueCode)
        {
            var theme = CreateCompleteTheme();
            var typography = theme.Typography;
            if (style == UiTextStyle.Body)
            {
                typography.Body = new UiTextStyleToken(CreateFont(), fontSize, FontStyles.Normal, 0f);
            }
            else
            {
                typography.Label = new UiTextStyleToken(CreateFont(), fontSize, FontStyles.Normal, 0f);
            }

            theme.Typography = typography;
            var issues = new List<string>();

            theme.Validate(issues);

            Assert.That(issues, Does.Contain(expectedIssueCode + ": ThemeFixture/" + style));
        }

        [Test]
        public void Validate_TouchTargetNarrowerThan48_ReturnsSpecificIssue()
        {
            var theme = CreateCompleteTheme();
            theme.Sizes = new UiSizeTokens(47f, 48f, 24f, 32f);
            var issues = new List<string>();

            theme.Validate(issues);

            Assert.That(issues, Does.Contain("MINIMUM_TOUCH_TARGET_BELOW_48X48: ThemeFixture/MinimumTouchTarget"));
        }

        [Test]
        public void ButtonRolesAndStates_ProvideExactlyTheApprovedThreeByThreeMatrix()
        {
            var roles = System.Enum.GetValues(typeof(UiButtonRole)).Cast<UiButtonRole>().ToArray();
            var states = System.Enum.GetValues(typeof(UiButtonState)).Cast<UiButtonState>().ToArray();

            Assert.That(roles, Is.EqualTo(new[]
            {
                UiButtonRole.Primary,
                UiButtonRole.Secondary,
                UiButtonRole.Destructive
            }));
            Assert.That(states, Is.EqualTo(new[]
            {
                UiButtonState.Default,
                UiButtonState.Pressed,
                UiButtonState.Disabled
            }));
            Assert.That(roles.Length * states.Length, Is.EqualTo(9));
        }

        [Test]
        public void Validate_ApprovedProvisionalMotionTokens_ReturnsNoIssues()
        {
            var theme = CreateCompleteTheme();
            var issues = new List<string>();

            theme.Validate(issues);

            Assert.That(issues, Is.Empty);
        }

        [TestCase(0f, 0.22f, 0.18f, 0.16f, 2.5f,
            "BUTTON_PRESS_DURATION_OUT_OF_RANGE: ThemeFixture/Motion/ButtonPressDuration")]
        [TestCase(0.13f, 0.22f, 0.18f, 0.16f, 2.5f,
            "BUTTON_PRESS_DURATION_OUT_OF_RANGE: ThemeFixture/Motion/ButtonPressDuration")]
        [TestCase(0.1f, 0.21f, 0.18f, 0.16f, 2.5f,
            "BOTTOM_SHEET_OPEN_DURATION_OUT_OF_RANGE: ThemeFixture/Motion/BottomSheetOpenDuration")]
        public void Validate_MotionOutsideApprovedContract_ReturnsStableIssue(
            float buttonPressDuration,
            float bottomSheetOpenDuration,
            float modalOpenDuration,
            float toastFadeInDuration,
            float toastDefaultStayDuration,
            string expectedIssue)
        {
            var theme = CreateCompleteTheme();
            theme.Motion = new UiMotionTokens(
                buttonPressDuration,
                bottomSheetOpenDuration,
                modalOpenDuration,
                toastFadeInDuration,
                toastDefaultStayDuration);
            var issues = new List<string>();

            theme.Validate(issues);

            Assert.That(issues, Does.Contain(expectedIssue));
        }

        private AnimalCafeUiTheme CreateCompleteTheme()
        {
            var theme = CreateOwned<AnimalCafeUiTheme>();
            theme.name = "ThemeFixture";
            theme.Colors = new UiSemanticColorTokens
            {
                Background = Color.white,
                Surface = Color.gray,
                Text = Color.black,
                Accent = Color.green,
                Disabled = Color.grey,
                Warning = Color.yellow,
                Destructive = Color.red
            };
            theme.Typography = new UiTypographyTokens
            {
                Heading = new UiTextStyleToken(CreateFont(), 24f, FontStyles.Bold, 0f),
                Body = new UiTextStyleToken(CreateFont(), 16f, FontStyles.Normal, 0f),
                Label = new UiTextStyleToken(CreateFont(), 14f, FontStyles.Normal, 0f)
            };
            theme.Spacing = new UiSpacingTokens(4f, 8f, 12f, 16f, 24f);
            theme.Shape = new UiShapeTokens(8f, 1f);
            theme.Materials = new UiMaterialTokens(
                CreateMaterial(),
                CreateMaterial(),
                CreateMaterial(),
                CreateMaterial());
            theme.Motion = new UiMotionTokens(0.1f, 0.22f, 0.18f, 0.16f, 2.5f);
            theme.Sizes = new UiSizeTokens(48f, 48f, 24f, 32f);
            return theme;
        }

        private TMP_FontAsset CreateFont()
        {
            return CreateOwned<TMP_FontAsset>();
        }

        private Material CreateMaterial()
        {
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            ownedObjects.Add(material);
            return material;
        }

        private T CreateOwned<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            ownedObjects.Add(instance);
            return instance;
        }
    }
}

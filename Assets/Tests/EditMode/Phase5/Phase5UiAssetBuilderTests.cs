using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.EditorTools.Phase5;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using UnityEngine.UI;

namespace AnimalCafe.Tests.Phase5
{
    public sealed class Phase5UiAssetBuilderTests
    {
        [Test]
        public void ApprovedPaths_AreCanonicalUniqueAndRemainUnderPhase5Folder()
        {
            var expectedPaths = new[]
            {
                "Assets/UI/Phase5/Theme/AnimalCafeUiTheme.asset",
                "Assets/UI/Phase5/Fonts/NotoSansSC-Regular.otf",
                "Assets/UI/Phase5/Fonts/NotoSansSC-Regular SDF.asset",
                "Assets/UI/Phase5/Fonts/OFL-1.1.txt",
                "Assets/UI/Phase5/Fonts/NotoSansSC-Regular.provenance.txt",
                "Assets/UI/Phase5/Resources/TMP Settings.asset",
                "Assets/UI/Phase5/Resources/LineBreaking Leading Characters.txt",
                "Assets/UI/Phase5/Resources/LineBreaking Following Characters.txt",
                "Assets/UI/Phase5/Shaders/TMP_SDF-Mobile.shader",
                "Assets/UI/Phase5/Shaders/TMPro_Properties.cginc",
                "Assets/UI/Phase5/Shaders/TMP-Essential-Resources.provenance.txt",
                "Assets/UI/Phase5/Materials/M_UI_Solid.mat",
                "Assets/UI/Phase5/Materials/M_UI_LightFrost.mat",
                "Assets/UI/Phase5/Materials/M_UI_StrongFrost.mat",
                "Assets/UI/Phase5/Prefabs/PF_UI_Root.prefab",
                "Assets/UI/Phase5/Prefabs/PF_UI_Panel_Solid.prefab",
                "Assets/UI/Phase5/Prefabs/PF_UI_Panel_LightFrost.prefab",
                "Assets/UI/Phase5/Prefabs/PF_UI_Panel_StrongFrost.prefab",
                "Assets/UI/Phase5/Prefabs/PF_UI_Modal.prefab",
                "Assets/UI/Phase5/Prefabs/PF_UI_BottomSheet.prefab",
                "Assets/UI/Phase5/Prefabs/PF_UI_Toast.prefab",
                "Assets/UI/Phase5/Prefabs/PF_UI_Tooltip.prefab",
                "Assets/UI/Phase5/Prefabs/PF_UI_ValidationMessage.prefab",
                "Assets/UI/Phase5/Prefabs/PF_UI_SafeArea.prefab",
                "Assets/UI/Phase5/Resources/Phase5UiFoundationInputActions.inputactions",
                "Assets/Scenes/Validation/Phase5UiFoundation.unity"
            };

            CollectionAssert.AreEquivalent(expectedPaths, Phase5UiAssetPaths.RequiredAssetPaths);
            Assert.That(Phase5UiAssetPaths.RequiredAssetPaths.Distinct().Count(),
                Is.EqualTo(Phase5UiAssetPaths.RequiredAssetPaths.Count));
            Assert.That(Phase5UiAssetPaths.RequiredAssetPaths.All(path =>
                path.StartsWith("Assets/UI/Phase5/", StringComparison.Ordinal) ||
                path == "Assets/Scenes/Validation/Phase5UiFoundation.unity"), Is.True);
            Assert.That(Phase5UiAssetPaths.ButtonPrefabPaths.Count, Is.EqualTo(9));
            Assert.That(Phase5UiAssetPaths.ButtonPrefabPaths.Distinct().Count(), Is.EqualTo(9));
        }

        [Test]
        public void BuildAll_Twice_PreservesCanonicalGuidsAndProducesOneAssetPerPath()
        {
            Phase5UiAssetBuilder.BuildAll();
            var firstGuids = Phase5UiAssetPaths.AllGeneratedAssetPaths
                .ToDictionary(path => path, AssetDatabase.AssetPathToGUID);

            Phase5UiAssetBuilder.BuildAll();

            foreach (var path in Phase5UiAssetPaths.AllGeneratedAssetPaths)
            {
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Not.Null, path);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(firstGuids[path]), path);
                Assert.That(AssetDatabase.FindAssets(
                    $"{System.IO.Path.GetFileNameWithoutExtension(path)}",
                    new[] { System.IO.Path.GetDirectoryName(path).Replace('\\', '/') })
                    .Count(guid => AssetDatabase.GUIDToAssetPath(guid) == path),
                    Is.EqualTo(1), path);
            }
        }

        [Test]
        public void GeneratedTheme_PassesApprovedThemeContractAndUsesCanonicalResources()
        {
            Phase5UiAssetBuilder.BuildAll();
            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(Phase5UiAssetPaths.ThemePath);
            var issues = new List<string>();

            Assert.That(theme, Is.Not.Null);
            theme.Validate(issues);
            Assert.That(issues, Is.Empty);
            Assert.That(theme.Typography.Body.FontSize, Is.GreaterThanOrEqualTo(16f));
            Assert.That(theme.Typography.Label.FontSize, Is.GreaterThanOrEqualTo(14f));
            Assert.That(theme.Sizes.MinimumTouchTargetWidth, Is.GreaterThanOrEqualTo(48f));
            Assert.That(theme.Sizes.MinimumTouchTargetHeight, Is.GreaterThanOrEqualTo(48f));
            Assert.That(AssetDatabase.GetAssetPath(theme.Typography.Body.FontAsset),
                Is.EqualTo(Phase5UiAssetPaths.TmpFontAssetPath));
            Assert.That(theme.Typography.Body.FontAsset.characterTable.Count, Is.GreaterThan(0));
            Assert.That(theme.Typography.Body.FontAsset.atlasPopulationMode,
                Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(AssetDatabase.GetAssetPath(theme.Materials.Solid),
                Is.EqualTo(Phase5UiAssetPaths.SolidMaterialPath));
            Assert.That(AssetDatabase.GetAssetPath(theme.Materials.StrongFrostFallback),
                Is.EqualTo(Phase5UiAssetPaths.LightFrostMaterialPath));
            Assert.That(TMP_Settings.instance, Is.Not.Null);
            Assert.That(TMP_Settings.leadingCharacters, Is.Not.Null);
            Assert.That(TMP_Settings.followingCharacters, Is.Not.Null);
            var tmpSettings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(
                Phase5UiAssetPaths.TmpSettingsPath);
            var serializedSettings = new SerializedObject(tmpSettings);
            Assert.That(serializedSettings.FindProperty("assetVersion").stringValue,
                Is.EqualTo("2"));
            Assert.That(theme.Typography.Body.FontAsset.material, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(theme.Typography.Body.FontAsset.material),
                Is.EqualTo(Phase5UiAssetPaths.TmpFontAssetPath));
        }

        [Test]
        public void GeneratedButtons_ContainThreeRolesByThreeStatesAndMinimumTouchTargets()
        {
            Phase5UiAssetBuilder.BuildAll();
            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(Phase5UiAssetPaths.ThemePath);
            foreach (UiButtonRole role in Enum.GetValues(typeof(UiButtonRole)))
            foreach (UiButtonState state in Enum.GetValues(typeof(UiButtonState)))
            {
                var path = $"{Phase5UiAssetPaths.Root}/Prefabs/PF_UI_Button_{role}_{state}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(prefab.GetComponent<Button>(), Is.Not.Null, path);
                Assert.That(prefab.GetComponent<Image>(), Is.Not.Null, path);
                Assert.That(prefab.GetComponent<AnimalCafeButtonView>(), Is.Not.Null, path);
                Assert.That(prefab.GetComponentInChildren<TMP_Text>(true), Is.Not.Null, path);
                Assert.That(prefab.GetComponent<RectTransform>().sizeDelta.x, Is.GreaterThanOrEqualTo(48f), path);
                Assert.That(prefab.GetComponent<RectTransform>().sizeDelta.y, Is.GreaterThanOrEqualTo(48f), path);
                Assert.That(prefab.GetComponent<Button>().interactable,
                    Is.EqualTo(state != UiButtonState.Disabled), path);
                Assert.That(prefab.GetComponent<Image>().color,
                    Is.EqualTo(ExpectedButtonColor(theme, role, state)).Using(ColorEqualityComparer.Instance), path);
            }
        }

        [Test]
        public void GeneratedContainersAndFeedback_HaveRequiredRuntimeComponentsAndNoMissingReferences()
        {
            Phase5UiAssetBuilder.BuildAll();
            AssertPrefabHas<AnimalCafePanelView>(Phase5UiAssetPaths.SolidPanelPrefabPath);
            AssertPrefabHas<AnimalCafePanelView>(Phase5UiAssetPaths.LightFrostPanelPrefabPath);
            AssertPrefabHas<AnimalCafePanelView>(Phase5UiAssetPaths.StrongFrostPanelPrefabPath);
            AssertPrefabHas<AnimalCafeModalView>(Phase5UiAssetPaths.ModalPrefabPath);
            AssertPrefabHas<AnimalCafeBottomSheetView>(Phase5UiAssetPaths.BottomSheetPrefabPath);
            AssertPrefabHas<ToastView>(Phase5UiAssetPaths.ToastPrefabPath);
            AssertPrefabHas<TooltipView>(Phase5UiAssetPaths.TooltipPrefabPath);
            AssertPrefabHas<ValidationMessageView>(Phase5UiAssetPaths.ValidationMessagePrefabPath);
            AssertPrefabHas<SafeAreaContainer>(Phase5UiAssetPaths.SafeAreaPrefabPath);

            foreach (var path in Phase5UiAssetPaths.PrefabPaths)
            {
                var dependencies = AssetDatabase.GetDependencies(path, true);
                Assert.That(dependencies, Has.None.Matches<string>(dependency =>
                    string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(dependency))), path);
            }
        }

        [Test]
        public void GeneratedFont_ContainsEveryMixedCjkLatinGlyphAndFitsExpandedBodyAndLabel()
        {
            Phase5UiAssetBuilder.BuildAll();
            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(Phase5UiAssetPaths.ThemePath);
            const string bodyText = "咖啡豆库存 Coffee Beans：今日可制作十二杯香草拿铁";
            const string labelText = "糖浆口味 Syrup Flavor：焦糖与香草";

            Assert.That(Phase5UiFontCoverage.FindMissingUnicodeScalars(
                theme.Typography.Body.FontAsset, bodyText), Is.Empty);
            Assert.That(Phase5UiFontCoverage.FindMissingUnicodeScalars(
                theme.Typography.Label.FontAsset, labelText), Is.Empty);
            AssertTextFits(theme.Typography.Body, bodyText, new Vector2(720f, 160f));
            AssertTextFits(theme.Typography.Label, labelText, new Vector2(600f, 120f));
        }

        private static void AssertPrefabHas<T>(string path) where T : Component
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(prefab.GetComponentInChildren<T>(true), Is.Not.Null, path);
        }

        private static Color ExpectedButtonColor(
            AnimalCafeUiTheme theme,
            UiButtonRole role,
            UiButtonState state)
        {
            if (state == UiButtonState.Disabled)
                return theme.Colors.Disabled;
            var baseColor = role switch
            {
                UiButtonRole.Primary => theme.Colors.Accent,
                UiButtonRole.Secondary => theme.Colors.Surface,
                UiButtonRole.Destructive => theme.Colors.Destructive,
                _ => throw new ArgumentOutOfRangeException(nameof(role))
            };
            return state == UiButtonState.Pressed
                ? Color.Lerp(baseColor, Color.black, 0.15f)
                : baseColor;
        }

        private static void AssertTextFits(UiTextStyleToken token, string textValue, Vector2 size)
        {
            var root = new GameObject(
                "FontCoverageCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var textObject = new GameObject(
                    "FontCoverageFixture", typeof(RectTransform), typeof(TextMeshProUGUI));
                textObject.transform.SetParent(root.transform, false);
                var rect = textObject.GetComponent<RectTransform>();
                rect.sizeDelta = size;
                var text = textObject.GetComponent<TextMeshProUGUI>();
                text.font = token.FontAsset;
                text.fontSize = token.FontSize;
                text.fontStyle = token.FontStyle;
                text.lineSpacing = token.LineSpacing;
                text.enableWordWrapping = true;
                text.overflowMode = TextOverflowModes.Overflow;
                text.text = textValue;
                Canvas.ForceUpdateCanvases();
                text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);

                Assert.That(text.textInfo.characterCount, Is.EqualTo(textValue.Length));
                Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(size.x));
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(size.y));
                Assert.That(text.fontSize, Is.EqualTo(token.FontSize));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}

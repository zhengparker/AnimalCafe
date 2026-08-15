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
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnimalCafe.Tests.Phase5
{
    public sealed class Phase5UiAssetAcceptanceTests
    {
        [SetUp]
        public void BuildCanonicalAssets() => Phase5UiAssetBuilder.BuildAll();

        [Test]
        public void CanonicalButtonPrefab_Reinstantiated_StillRespondsToPressReleaseAndDisable()
        {
            var theme = Load<AnimalCafeUiTheme>(Phase5UiAssetPaths.ThemePath);
            var prefab = Load<GameObject>(ButtonPath(UiButtonRole.Primary, UiButtonState.Default));
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var view = instance.GetComponent<AnimalCafeButtonView>();
                var button = instance.GetComponent<Button>();
                var image = instance.GetComponent<Image>();
                var defaultScale = instance.transform.localScale;
                view.OnPointerDown(new PointerEventData(null));
                Assert.That(image.color,
                    Is.EqualTo(Color.Lerp(theme.Colors.Accent, Color.black, 0.25f)));
                Assert.That(instance.transform.localScale,
                    Is.EqualTo(Vector3.Scale(defaultScale, new Vector3(0.97f, 0.97f, 1f))));
                view.OnPointerUp(new PointerEventData(null));
                Assert.That(image.color, Is.EqualTo(theme.Colors.Accent));
                Assert.That(instance.transform.localScale, Is.EqualTo(defaultScale));
                button.interactable = false;
                view.OnPointerDown(new PointerEventData(null));
                Assert.That(image.color, Is.EqualTo(theme.Colors.Disabled));
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        [Test]
        public void CanonicalButtons_UseOneSlicedRoundedSprite()
        {
            Sprite canonical = null;
            foreach (UiButtonRole role in Enum.GetValues(typeof(UiButtonRole)))
            foreach (UiButtonState state in Enum.GetValues(typeof(UiButtonState)))
            {
                var image = Load<GameObject>(ButtonPath(role, state)).GetComponent<Image>();
                Assert.That(image.sprite, Is.Not.Null, $"{role}/{state} requires the canonical rounded Sprite.");
                Assert.That(image.type, Is.EqualTo(Image.Type.Sliced), $"{role}/{state}");
                Assert.That(image.sprite.border.sqrMagnitude, Is.GreaterThan(0f), $"{role}/{state}");
                canonical ??= image.sprite;
                Assert.That(image.sprite, Is.SameAs(canonical),
                    $"{role}/{state} must reuse one deterministic rounded Sprite.");
            }
        }

        [Test]
        public void CanonicalButtonsAndSolidPanel_HaveSoftElevationWithoutChangingInteractionGeometry()
        {
            foreach (UiButtonRole role in Enum.GetValues(typeof(UiButtonRole)))
            foreach (UiButtonState state in Enum.GetValues(typeof(UiButtonState)))
            {
                var path = ButtonPath(role, state);
                var prefab = Load<GameObject>(path);
                var rect = prefab.GetComponent<RectTransform>();
                var button = prefab.GetComponent<Button>();
                var shadow = prefab.GetComponent<Shadow>();
                var highlight = Find(prefab, "Top Highlight").GetComponent<Image>();

                Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(180f, 56f)), path);
                Assert.That(button.targetGraphic, Is.SameAs(prefab.GetComponent<Image>()), path);
                Assert.That(shadow, Is.Not.Null, $"{path} requires a soft elevation shadow.");
                Assert.That(shadow.effectDistance.y, Is.LessThan(0f), path);
                Assert.That(shadow.effectColor.a, Is.InRange(0.15f, 0.35f), path);
                Assert.That(highlight.raycastTarget, Is.False, path);
            }

            var solid = Load<GameObject>(Phase5UiAssetPaths.SolidPanelPrefabPath);
            var solidShadow = solid.GetComponent<Shadow>();
            var solidHighlight = Find(solid, "Top Highlight").GetComponent<Image>();
            Assert.That(solidShadow, Is.Not.Null);
            Assert.That(solidShadow.effectDistance.y, Is.LessThan(0f));
            Assert.That(solidHighlight.raycastTarget, Is.False);

            foreach (var frostPath in new[]
            {
                Phase5UiAssetPaths.LightFrostPanelPrefabPath,
                Phase5UiAssetPaths.StrongFrostPanelPrefabPath
            })
            {
                Assert.That(Load<GameObject>(frostPath).GetComponent<Shadow>(), Is.Null,
                    $"{frostPath} stays outside the Soft Elevation scope.");
            }
        }

        [Test]
        public void CanonicalButtonAndPanelPrefabs_PersistOwnedBindings()
        {
            foreach (UiButtonRole role in Enum.GetValues(typeof(UiButtonRole)))
            foreach (UiButtonState state in Enum.GetValues(typeof(UiButtonState)))
            {
                var prefab = Load<GameObject>(ButtonPath(role, state));
                var serialized = new SerializedObject(prefab.GetComponent<AnimalCafeButtonView>());
                AssertObjectReference(serialized, "theme", ButtonPath(role, state));
                AssertObjectReference(serialized, "button", ButtonPath(role, state));
                AssertObjectReference(serialized, "background", ButtonPath(role, state));
                Assert.That(serialized.FindProperty("role").enumValueIndex, Is.EqualTo((int)role));
            }

            foreach (var pair in new[]
            {
                (Phase5UiAssetPaths.SolidPanelPrefabPath, UiPanelStyle.Solid),
                (Phase5UiAssetPaths.LightFrostPanelPrefabPath, UiPanelStyle.LightFrost),
                (Phase5UiAssetPaths.StrongFrostPanelPrefabPath, UiPanelStyle.StrongFrost)
            })
            {
                var prefab = Load<GameObject>(pair.Item1);
                var serialized = new SerializedObject(prefab.GetComponent<AnimalCafePanelView>());
                AssertObjectReference(serialized, "configuredTheme", pair.Item1);
                Assert.That(serialized.FindProperty("requestedStyle").enumValueIndex,
                    Is.EqualTo((int)pair.Item2));
            }
        }

        [Test]
        public void CanonicalStrongPanels_Reinstantiated_ShareOneStrongOwnerAndFallback()
        {
            var prefab = Load<GameObject>(Phase5UiAssetPaths.StrongFrostPanelPrefabPath);
            var first = UnityEngine.Object.Instantiate(prefab);
            var second = UnityEngine.Object.Instantiate(prefab);
            try
            {
                first.SetActive(false); second.SetActive(false);
                first.SetActive(true); second.SetActive(true);
                Assert.That(first.GetComponent<AnimalCafePanelView>().ResolvedStyle,
                    Is.EqualTo(UiPanelStyle.StrongFrost));
                Assert.That(second.GetComponent<AnimalCafePanelView>().ResolvedStyle,
                    Is.EqualTo(UiPanelStyle.LightFrost));
                first.SetActive(false);
                second.SetActive(false); second.SetActive(true);
                Assert.That(second.GetComponent<AnimalCafePanelView>().ResolvedStyle,
                    Is.EqualTo(UiPanelStyle.StrongFrost));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void CanonicalFrostMaterials_HaveClearlyDifferentTintAndOpacity()
        {
            Phase5UiAssetBuilder.BuildAll();
            var light = Load<Material>(Phase5UiAssetPaths.LightFrostMaterialPath).color;
            var strong = Load<Material>(Phase5UiAssetPaths.StrongFrostMaterialPath).color;

            Assert.That(strong.a - light.a, Is.GreaterThanOrEqualTo(0.2f),
                "Strong Frost must be materially more opaque than Light Frost.");
            var tintDistance = Mathf.Abs(strong.r - light.r)
                + Mathf.Abs(strong.g - light.g)
                + Mathf.Abs(strong.b - light.b);
            Assert.That(tintDistance, Is.GreaterThanOrEqualTo(0.25f),
                "Strong Frost needs a visibly distinct tint, not a near-identical cream overlay.");
        }

        [Test]
        public void CanonicalContainersAndFeedback_HaveUsableStructuredInternalContracts()
        {
            AssertTouchButton(Phase5UiAssetPaths.ModalPrefabPath, "Blocker");
            AssertTouchButton(Phase5UiAssetPaths.ModalPrefabPath, "ConfirmButton");
            AssertTouchButton(Phase5UiAssetPaths.ModalPrefabPath, "CancelButton");
            Assert.That(Load<GameObject>(Phase5UiAssetPaths.ModalPrefabPath)
                .GetComponent<CanvasGroup>(), Is.Not.Null);

            AssertTouchButton(Phase5UiAssetPaths.BottomSheetPrefabPath, "OutsideButton");
            Assert.That(Find(Load<GameObject>(Phase5UiAssetPaths.BottomSheetPrefabPath), "Content"), Is.Not.Null);
            Assert.That(Load<GameObject>(Phase5UiAssetPaths.BottomSheetPrefabPath)
                .GetComponent<CanvasGroup>(), Is.Not.Null);

            var toast = Load<GameObject>(Phase5UiAssetPaths.ToastPrefabPath);
            AssertReadableLabel(toast, "Label");
            foreach (var graphic in toast.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False);

            var tooltip = Load<GameObject>(Phase5UiAssetPaths.TooltipPrefabPath);
            AssertReadableLabel(tooltip, "Label");
            Assert.That(Find(tooltip, "Content"), Is.Not.Null);

            var validation = Load<GameObject>(Phase5UiAssetPaths.ValidationMessagePrefabPath);
            AssertReadableLabel(validation, "Label");
            Assert.That(Find(validation, "Label").GetComponent<Graphic>().raycastTarget, Is.False);
        }

        [Test]
        public void CanonicalBottomSheet_IsAReadableLowerScreenSurfaceWithExplicitActions()
        {
            var prefab = Load<GameObject>(Phase5UiAssetPaths.BottomSheetPrefabPath);
            var rootRect = prefab.GetComponent<RectTransform>();
            var outside = Find(prefab, "OutsideButton");
            var content = Find(prefab, "Content");
            var contentRect = content.GetComponent<RectTransform>();
            var outsideImage = outside.GetComponent<Image>();

            Assert.That(rootRect.sizeDelta, Is.EqualTo(new Vector2(1080f, 1920f)));
            Assert.That(outside.transform.GetSiblingIndex(), Is.EqualTo(0));
            Assert.That(outsideImage.color.a, Is.InRange(0.25f, 0.45f));
            Assert.That(contentRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(contentRect.anchorMax.x, Is.EqualTo(1f));
            Assert.That(contentRect.anchorMax.y, Is.InRange(0.45f, 0.6f));
            Assert.That(content.GetComponent<Shadow>(), Is.Not.Null);

            var handle = Find(content, "Drag Handle").GetComponent<Image>();
            Assert.That(handle.raycastTarget, Is.False);
            Assert.That(handle.GetComponent<RectTransform>().sizeDelta.x, Is.GreaterThanOrEqualTo(72f));
            AssertReadableCopy(content, "Title", "Order details");
            AssertReadableCopy(content, "Body", "Review your selections before continuing.");
            AssertActionButton(content, "CancelButton", "Cancel");
            AssertActionButton(content, "ConfirmButton", "Confirm");
        }

        [Test]
        public void CanonicalModal_IsAReadableCriticalSurfaceWithDimmedBlockingScrim()
        {
            var prefab = Load<GameObject>(Phase5UiAssetPaths.ModalPrefabPath);
            var rootRect = prefab.GetComponent<RectTransform>();
            var blocker = Find(prefab, "Blocker");
            var blockerImage = blocker.GetComponent<Image>();
            var content = Find(prefab, "Content");
            var contentRect = content.GetComponent<RectTransform>();

            Assert.That(rootRect.sizeDelta, Is.EqualTo(new Vector2(1080f, 1920f)));
            Assert.That(blocker.transform.GetSiblingIndex(), Is.EqualTo(0));
            Assert.That(blockerImage.color.r, Is.LessThan(0.2f));
            Assert.That(blockerImage.color.a, Is.InRange(0.5f, 0.6f));
            Assert.That(contentRect.sizeDelta.x, Is.InRange(620f, 760f));
            Assert.That(contentRect.sizeDelta.y, Is.InRange(480f, 680f));
            Assert.That(content.GetComponent<Shadow>(), Is.Not.Null);
            AssertReadableCopy(content, "Title", "Discard changes?");
            AssertReadableCopy(content, "Body", "Your current changes will not be saved.");
            AssertActionButton(content, "CancelButton", "Cancel");
            AssertActionButton(content, "ConfirmButton", "Discard");
        }

        [Test]
        public void CanonicalUiRoot_AllCanvasesUseApprovedMobileScaling()
        {
            var root = Load<GameObject>(Phase5UiAssetPaths.UiRootPrefabPath);
            var scalers = root.GetComponentsInChildren<CanvasScaler>(true);
            Assert.That(scalers, Has.Length.EqualTo(3));
            foreach (var scaler in scalers)
            {
                Assert.That(scaler.uiScaleMode,
                    Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
                Assert.That(scaler.referenceResolution, Is.EqualTo(new Vector2(1080f, 1920f)));
                Assert.That(scaler.screenMatchMode,
                    Is.EqualTo(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));
                Assert.That(scaler.matchWidthOrHeight, Is.EqualTo(0.5f));
            }
        }

        [Test]
        public void CanonicalButtons_HaveReadableForegroundAndDistinctStates()
        {
            foreach (UiButtonRole role in Enum.GetValues(typeof(UiButtonRole)))
            {
                var colors = new Dictionary<UiButtonState, Color>();
                foreach (UiButtonState state in Enum.GetValues(typeof(UiButtonState)))
                {
                    var prefab = Load<GameObject>(ButtonPath(role, state));
                    var background = prefab.GetComponent<Image>().color;
                    var foreground = prefab.GetComponentInChildren<TMP_Text>(true).color;
                    colors[state] = background;
                    Assert.That(ContrastRatio(foreground, background), Is.GreaterThanOrEqualTo(4.5),
                        $"{role}/{state} foreground must remain readable.");
                }
                Assert.That(colors[UiButtonState.Pressed], Is.Not.EqualTo(colors[UiButtonState.Default]));
                Assert.That(colors[UiButtonState.Disabled], Is.Not.EqualTo(colors[UiButtonState.Default]));
            }
        }

        [Test]
        public void CanonicalNotoFont_ExpandedMixedLabelsFitTogetherWithoutOverflowOrMissingGlyphs()
        {
            const string bodyBaseline = "Coffee Bean 库存与 syrup 插孔设置";
            const string bodyExpanded = "Coffee Bean 库存与 syrup 插孔设置以及口味确认并保存";
            const string labelBaseline = "Confirm Coffee Machine 咖啡机";
            const string labelExpanded = "Confirm Coffee Machine 咖啡机 Flavor 口味选择";
            Assert.That((double)bodyExpanded.Length / bodyBaseline.Length, Is.InRange(1.3, 1.5));
            Assert.That((double)labelExpanded.Length / labelBaseline.Length, Is.InRange(1.3, 1.5));

            var theme = Load<AnimalCafeUiTheme>(Phase5UiAssetPaths.ThemePath);
            Assert.That(Phase5UiFontCoverage.FindMissingUnicodeScalars(
                theme.Typography.Body.FontAsset, bodyExpanded), Is.Empty);
            Assert.That(Phase5UiFontCoverage.FindMissingUnicodeScalars(
                theme.Typography.Label.FontAsset, labelExpanded), Is.Empty);
            var canvas = new GameObject("CanonicalNotoCanvas", typeof(RectTransform), typeof(Canvas));
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(canvas.transform, false);
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(360f, 640f);
            panel.GetComponent<VerticalLayoutGroup>().spacing = 16f;
            var body = CreateCanonicalLabel(panel.transform, "Body", theme.Typography.Body, bodyExpanded);
            var label = CreateCanonicalLabel(panel.transform, "Label", theme.Typography.Label, labelExpanded);
            try
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(panel.GetComponent<RectTransform>());
                body.ForceMeshUpdate(true, true); label.ForceMeshUpdate(true, true);
                AssertGeneratedText(body, bodyExpanded, 16f);
                AssertGeneratedText(label, labelExpanded, 14f);
                var bodyBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    panel.transform, body.rectTransform);
                var labelBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    panel.transform, label.rectTransform);
                Assert.That(bodyBounds.Intersects(labelBounds), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }

        [Test]
        public void Builder_RepairsDamagedCanonicalAssetsWithoutChangingGuids()
        {
            var paths = new[] { Phase5UiAssetPaths.TmpFontAssetPath, Phase5UiAssetPaths.TmpSettingsPath,
                Phase5UiAssetPaths.SolidMaterialPath, Phase5UiAssetPaths.UiRootPrefabPath };
            var guids = paths.ToDictionary(path => path, AssetDatabase.AssetPathToGUID);
            var font = Load<TMP_FontAsset>(Phase5UiAssetPaths.TmpFontAssetPath);
            var canonicalSourceGuid = AssetDatabase.AssetPathToGUID(Phase5UiAssetPaths.FontSourcePath);
            var canonicalMaterial = font.material;
            var canonicalAtlas = font.atlasTexture;
            Assert.That(canonicalSourceGuid, Is.Not.Empty);
            Assert.That(canonicalMaterial, Is.Not.Null);
            Assert.That(canonicalAtlas, Is.Not.Null);
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            font.ClearFontAssetData();
            var serializedFont = new SerializedObject(font);
            serializedFont.FindProperty("m_SourceFontFileGUID").stringValue =
                "00000000000000000000000000000000";
            serializedFont.FindProperty("m_Material").objectReferenceValue = null;
            serializedFont.FindProperty("m_AtlasTextures")
                .GetArrayElementAtIndex(0).objectReferenceValue = null;
            serializedFont.ApplyModifiedPropertiesWithoutUndo();
            var settings = Load<TMP_Settings>(Phase5UiAssetPaths.TmpSettingsPath);
            var serializedSettings = new SerializedObject(settings);
            serializedSettings.FindProperty("assetVersion").stringValue = "damaged";
            serializedSettings.FindProperty("m_defaultFontAsset").objectReferenceValue = null;
            serializedSettings.FindProperty("m_defaultFontAssetPath").stringValue = "Damaged/Font/Path";
            serializedSettings.FindProperty("m_leadingCharacters").objectReferenceValue = null;
            serializedSettings.FindProperty("m_followingCharacters").objectReferenceValue = null;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            Load<Material>(Phase5UiAssetPaths.SolidMaterialPath).shader = Shader.Find("Sprites/Default");
            var root = Load<GameObject>(Phase5UiAssetPaths.UiRootPrefabPath);
            root.GetComponentsInChildren<CanvasScaler>(true)[0].uiScaleMode =
                CanvasScaler.ScaleMode.ConstantPixelSize;
            AssetDatabase.SaveAssets();

            Phase5UiAssetBuilder.BuildAll();

            foreach (var path in paths)
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guids[path]), path);
            font = Load<TMP_FontAsset>(Phase5UiAssetPaths.TmpFontAssetPath);
            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(Phase5UiFontCoverage.FindMissingUnicodeScalars(font,
                "咖啡豆库存 Coffee Beans 糖浆口味 Syrup Flavor"), Is.Empty);
            var repairedFont = new SerializedObject(font);
            Assert.That(repairedFont.FindProperty("m_SourceFontFileGUID").stringValue,
                Is.EqualTo(canonicalSourceGuid));
            Assert.That(font.material, Is.EqualTo(canonicalMaterial));
            Assert.That(font.material.shader.name, Is.EqualTo("TextMeshPro/Mobile/Distance Field"));
            Assert.That(font.atlasTexture, Is.EqualTo(canonicalAtlas));
            Assert.That(font.material.mainTexture, Is.EqualTo(canonicalAtlas));
            settings = Load<TMP_Settings>(Phase5UiAssetPaths.TmpSettingsPath);
            serializedSettings = new SerializedObject(settings);
            Assert.That(serializedSettings.FindProperty("assetVersion").stringValue, Is.EqualTo("2"));
            Assert.That(serializedSettings.FindProperty("m_defaultFontAsset").objectReferenceValue,
                Is.EqualTo(font));
            Assert.That(serializedSettings.FindProperty("m_defaultFontAssetPath").stringValue,
                Is.Empty);
            Assert.That(serializedSettings.FindProperty("m_leadingCharacters").objectReferenceValue,
                Is.EqualTo(Load<TextAsset>(Phase5UiAssetPaths.LeadingCharactersPath)));
            Assert.That(serializedSettings.FindProperty("m_followingCharacters").objectReferenceValue,
                Is.EqualTo(Load<TextAsset>(Phase5UiAssetPaths.FollowingCharactersPath)));
            Assert.That(Load<Material>(Phase5UiAssetPaths.SolidMaterialPath).shader.name,
                Is.EqualTo("UI/Default"));
            Assert.That(Load<GameObject>(Phase5UiAssetPaths.UiRootPrefabPath)
                .GetComponentsInChildren<CanvasScaler>(true)[0].uiScaleMode,
                Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
        }

        [Test]
        public void OfficialSupportAndProvenanceFiles_MatchApprovedHashesAndSourceRecord()
        {
            Assert.That(Phase5UiAssetBuilder.ValidateOfficialResourceEvidence(), Is.Empty);
        }

        private static void AssertObjectReference(SerializedObject serialized, string name, string context)
        {
            var property = serialized.FindProperty(name);
            Assert.That(property, Is.Not.Null, $"Missing serialized field {name}: {context}");
            Assert.That(property.objectReferenceValue, Is.Not.Null, $"Missing {name}: {context}");
        }

        private static void AssertTouchButton(string prefabPath, string childName)
        {
            var child = Find(Load<GameObject>(prefabPath), childName);
            Assert.That(child, Is.Not.Null, $"{prefabPath}/{childName}");
            Assert.That(child.GetComponent<Button>(), Is.Not.Null);
            var size = child.GetComponent<RectTransform>().rect.size;
            Assert.That(size.x, Is.GreaterThanOrEqualTo(48f));
            Assert.That(size.y, Is.GreaterThanOrEqualTo(48f));
        }

        private static void AssertReadableLabel(GameObject root, string name)
        {
            var label = Find(root, name);
            Assert.That(label, Is.Not.Null);
            var rect = label.GetComponent<RectTransform>().rect.size;
            Assert.That(rect.x, Is.GreaterThan(0f));
            Assert.That(rect.y, Is.GreaterThan(0f));
            Assert.That(label.GetComponent<TMP_Text>(), Is.Not.Null);
        }

        private static void AssertReadableCopy(GameObject root, string name, string expected)
        {
            var label = Find(root, name);
            Assert.That(label, Is.Not.Null, name);
            var text = label.GetComponent<TMP_Text>();
            Assert.That(text, Is.Not.Null, name);
            Assert.That(text.text, Is.EqualTo(expected), name);
            Assert.That(text.raycastTarget, Is.False, name);
        }

        private static void AssertActionButton(GameObject root, string name, string expectedLabel)
        {
            var buttonObject = Find(root, name);
            Assert.That(buttonObject, Is.Not.Null, name);
            Assert.That(buttonObject.GetComponent<Button>(), Is.Not.Null, name);
            var size = buttonObject.GetComponent<RectTransform>().sizeDelta;
            Assert.That(size.x, Is.GreaterThanOrEqualTo(160f), name);
            Assert.That(size.y, Is.GreaterThanOrEqualTo(48f), name);
            Assert.That(buttonObject.GetComponentInChildren<TMP_Text>(true).text,
                Is.EqualTo(expectedLabel), name);
        }

        private static TextMeshProUGUI CreateCanonicalLabel(
            Transform parent, string name, UiTextStyleToken token, string content)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(328f, 120f);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.font = token.FontAsset; text.fontSize = token.FontSize;
            text.fontStyle = token.FontStyle; text.lineSpacing = token.LineSpacing;
            text.enableAutoSizing = false; text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow; text.text = content;
            return text;
        }

        private static void AssertGeneratedText(TextMeshProUGUI text, string expected, float baseline)
        {
            Assert.That(text.fontSize, Is.GreaterThanOrEqualTo(baseline));
            Assert.That(text.isTextOverflowing, Is.False);
            Assert.That(text.textInfo.characterCount, Is.EqualTo(expected.Length));
            var rect = text.rectTransform.rect;
            for (var index = 0; index < text.textInfo.characterCount; index++)
            {
                var character = text.textInfo.characterInfo[index];
                if (!character.isVisible) continue;
                Assert.That(character.bottomLeft.x, Is.GreaterThanOrEqualTo(rect.xMin - 0.5f));
                Assert.That(character.topRight.x, Is.LessThanOrEqualTo(rect.xMax + 0.5f));
                Assert.That(character.bottomLeft.y, Is.GreaterThanOrEqualTo(rect.yMin - 0.5f));
                Assert.That(character.topRight.y, Is.LessThanOrEqualTo(rect.yMax + 0.5f));
            }
        }

        private static GameObject Find(GameObject root, string name) =>
            root.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == name)?.gameObject;

        private static string ButtonPath(UiButtonRole role, UiButtonState state) =>
            $"{Phase5UiAssetPaths.Root}/Prefabs/PF_UI_Button_{role}_{state}.prefab";

        private static T Load<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path);

        private static double ContrastRatio(Color first, Color second)
        {
            double Luminance(Color color)
            {
                double Channel(float value) => value <= 0.04045f
                    ? value / 12.92f
                    : Math.Pow((value + 0.055f) / 1.055f, 2.4);
                return 0.2126 * Channel(color.r) + 0.7152 * Channel(color.g) + 0.0722 * Channel(color.b);
            }
            var a = Luminance(first); var b = Luminance(second);
            return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
        }
    }
}

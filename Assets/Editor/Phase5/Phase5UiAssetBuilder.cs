using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.Foundation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.Phase5
{
    public static class Phase5UiAssetBuilder
    {
        public const string FontSha256 = "FAA6C9DF652116DDE789D351359F3D7E5D2285A2B2A1F04A2D7244DF706D5EA9";
        private const string FontSourceUrl = "https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/SubsetOTF/SC/NotoSansSC-Regular.otf";
        private const string TmpShaderSha256 = "44C39AABC7E88E7E1FEFFC1880DF754F4ADF86B62E9512AA89E9D2D65171AFF4";
        private const string TmpIncludeSha256 = "66DB1F03E8D7A413EBA79BCB6602FDB2DE710B586F34A6F2584ED6F68F028E90";
        private const string LeadingCharactersSha256 = "62D3F4D5F64AAF885692D2D13A8313F6918E6E4C5AEB3990A43F614652BFD89C";
        private const string FollowingCharactersSha256 = "3A73E5FFE2510756DD3C2E982FDC1218B4BE82C6E21ABDE88070D6860E1BC8E6";
        private const string RequiredUiCharacters =
            "咖啡豆库存 Coffee Beans：今日可制作十二杯香草拿铁" +
            "糖浆口味 Syrup Flavor：焦糖与香草" +
            "Coffee Bean 库存与 syrup 插孔设置以及口味确认并保存" +
            "Confirm Coffee Machine 咖啡机 Flavor 口味选择" +
            "确认取消返回暂停正常快速主要次要删除错误提示信息装修旋转放置保存" +
            "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
            " ，。：；！？-+/()[]%×";

        [MenuItem("AnimalCafe/Phase 5/Build UI Assets")]
        public static void BuildAll()
        {
            EnsureFolders();
            RequireLicensedFont();
            var evidenceIssues = ValidateOfficialResourceEvidence();
            if (evidenceIssues.Count > 0)
                throw new InvalidOperationException(string.Join("\n", evidenceIssues));
            AssetDatabase.ImportAsset(Phase5UiAssetPaths.TmpShaderIncludePath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(Phase5UiAssetPaths.TmpShaderPath, ImportAssetOptions.ForceSynchronousImport);
            if (Shader.Find("TextMeshPro/Mobile/Distance Field") == null)
                throw new InvalidOperationException("Canonical TMP mobile SDF shader did not import.");
            AssetDatabase.ImportAsset(Phase5UiAssetPaths.FontSourcePath, ImportAssetOptions.ForceSynchronousImport);
            EnsureTmpSettings();
            var font = EnsureTmpFont();
            ConfigureTmpSettings(font);
            var solid = EnsureMaterial(Phase5UiAssetPaths.SolidMaterialPath, new Color(0.96f, 0.91f, 0.82f, 1f));
            var light = EnsureMaterial(Phase5UiAssetPaths.LightFrostMaterialPath, new Color(0.94f, 0.97f, 0.92f, 0.88f));
            var strong = EnsureMaterial(Phase5UiAssetPaths.StrongFrostMaterialPath, new Color(0.86f, 0.92f, 0.86f, 0.94f));
            var theme = EnsureTheme(font, solid, light, strong);
            BuildRoot();
            foreach (UiButtonRole role in Enum.GetValues(typeof(UiButtonRole)))
            foreach (UiButtonState state in Enum.GetValues(typeof(UiButtonState)))
                BuildButton(theme, role, state);
            BuildPanel(theme, UiPanelStyle.Solid, Phase5UiAssetPaths.SolidPanelPrefabPath);
            BuildPanel(theme, UiPanelStyle.LightFrost, Phase5UiAssetPaths.LightFrostPanelPrefabPath);
            BuildPanel(theme, UiPanelStyle.StrongFrost, Phase5UiAssetPaths.StrongFrostPanelPrefabPath);
            BuildModal(theme);
            BuildBottomSheet(theme);
            BuildToast(theme);
            BuildTooltip(theme);
            BuildValidationMessage(theme);
            BuildSimple<SafeAreaContainer>(Phase5UiAssetPaths.SafeAreaPrefabPath, "PF_UI_SafeArea", false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void RequireLicensedFont()
        {
            var root = Directory.GetParent(Application.dataPath).FullName;
            var fontPath = Path.Combine(root, Phase5UiAssetPaths.FontSourcePath);
            var licensePath = Path.Combine(root, Phase5UiAssetPaths.FontLicensePath);
            var provenancePath = Path.Combine(root, Phase5UiAssetPaths.FontProvenancePath);
            if (!File.Exists(fontPath) || !File.Exists(licensePath) || !File.Exists(provenancePath))
                throw new FileNotFoundException("Noto Sans SC source, OFL license and provenance are required.");
            using var stream = File.OpenRead(fontPath);
            using var sha = SHA256.Create();
            var actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            if (!string.Equals(actual, FontSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Noto Sans SC SHA-256 mismatch: {actual}");
            if (!File.ReadAllText(licensePath).Contains("SIL OPEN FONT LICENSE Version 1.1"))
                throw new InvalidOperationException("OFL 1.1 license evidence is invalid.");
        }

        public static IReadOnlyList<string> ValidateOfficialResourceEvidence()
        {
            var issues = new List<string>();
            ValidateHash(Phase5UiAssetPaths.FontSourcePath, FontSha256, issues);
            ValidateHash(Phase5UiAssetPaths.TmpShaderPath, TmpShaderSha256, issues);
            ValidateHash(Phase5UiAssetPaths.TmpShaderIncludePath, TmpIncludeSha256, issues);
            ValidateHash(Phase5UiAssetPaths.LeadingCharactersPath, LeadingCharactersSha256, issues);
            ValidateHash(Phase5UiAssetPaths.FollowingCharactersPath, FollowingCharactersSha256, issues);
            var provenance = AbsolutePath(Phase5UiAssetPaths.FontProvenancePath);
            if (!File.Exists(provenance))
                issues.Add($"Missing font provenance: {Phase5UiAssetPaths.FontProvenancePath}");
            else
            {
                var content = File.ReadAllText(provenance);
                if (!content.Contains(FontSourceUrl)) issues.Add("Font provenance source URL mismatch.");
                if (!content.Contains(FontSha256, StringComparison.OrdinalIgnoreCase))
                    issues.Add("Font provenance SHA-256 mismatch.");
            }
            return issues;
        }

        private static void ValidateHash(string assetPath, string expected, List<string> issues)
        {
            var path = AbsolutePath(assetPath);
            if (!File.Exists(path)) { issues.Add($"Missing official resource: {assetPath}"); return; }
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            var actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                issues.Add($"Official resource SHA-256 mismatch: {assetPath}; actual {actual}.");
        }

        private static string AbsolutePath(string assetPath) => Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            assetPath.Replace('/', Path.DirectorySeparatorChar));

        private static TMP_FontAsset EnsureTmpFont()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(Phase5UiAssetPaths.FontSourcePath)
                ?? throw new InvalidOperationException("Noto Sans SC did not import as a Unity Font.");
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Phase5UiAssetPaths.TmpFontAssetPath);
            if (existing != null)
            {
                RepairTmpFontInPlace(existing, source);
                return existing;
            }
            var asset = TMP_FontAsset.CreateFontAsset(source, 64, 8,
                GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
                throw new InvalidOperationException("TMP failed to create the Noto Sans SC font asset.");
            asset.name = "NotoSansSC-Regular SDF";
            PopulateRequiredFontCharacters(asset);
            AssetDatabase.CreateAsset(asset, Phase5UiAssetPaths.TmpFontAssetPath);
            foreach (var texture in asset.atlasTextures)
                if (texture != null) AssetDatabase.AddObjectToAsset(texture, asset);
            if (asset.material != null) AssetDatabase.AddObjectToAsset(asset.material, asset);
            return asset;
        }

        private static void RepairTmpFontInPlace(TMP_FontAsset font, Font source)
        {
            var serialized = new SerializedObject(font);
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            serialized.Update();
            serialized.FindProperty("m_SourceFontFile").objectReferenceValue = source;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (Phase5UiFontCoverage.FindMissingUnicodeScalars(font, RequiredUiCharacters).Count > 0)
                PopulateRequiredFontCharacters(font);
            else
                font.atlasPopulationMode = AtlasPopulationMode.Static;

            var shader = Shader.Find("TextMeshPro/Mobile/Distance Field")
                ?? throw new InvalidOperationException("Canonical TMP mobile SDF shader missing.");
            if (font.material == null)
            {
                var material = new Material(shader) { name = "Noto Sans SC - Regular Material" };
                material.mainTexture = font.atlasTexture;
                AssetDatabase.AddObjectToAsset(material, font);
                serialized = new SerializedObject(font);
                serialized.FindProperty("m_Material").objectReferenceValue = material;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                font.material.shader = shader;
                font.material.mainTexture = font.atlasTexture;
                EditorUtility.SetDirty(font.material);
            }
            EditorUtility.SetDirty(font);
        }

        private static void PopulateRequiredFontCharacters(TMP_FontAsset font)
        {
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            if (!font.TryAddCharacters(RequiredUiCharacters, out var missing, includeFontFeatures: true)
                || !string.IsNullOrEmpty(missing))
            {
                throw new InvalidOperationException(
                    $"Noto Sans SC could not add required Phase 5 UI glyphs: {missing}");
            }

            // Ship the populated atlas deterministically; later character expansion is an
            // explicit builder decision instead of an accidental runtime font mutation.
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(font);
            foreach (var texture in font.atlasTextures)
                if (texture != null) EditorUtility.SetDirty(texture);
            if (font.material != null) EditorUtility.SetDirty(font.material);
        }

        private static TMP_Settings EnsureTmpSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(Phase5UiAssetPaths.TmpSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<TMP_Settings>();
                AssetDatabase.CreateAsset(settings, Phase5UiAssetPaths.TmpSettingsPath);
                AssetDatabase.SaveAssets();
            }
            if (TMP_Settings.instance == null)
                throw new InvalidOperationException("Canonical Phase 5 TMP Settings could not be loaded from Resources.");
            return settings;
        }

        private static void ConfigureTmpSettings(TMP_FontAsset font)
        {
            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(Phase5UiAssetPaths.TmpSettingsPath);
            var serialized = new SerializedObject(settings);
            serialized.FindProperty("assetVersion").stringValue = "2";
            serialized.FindProperty("m_defaultFontAsset").objectReferenceValue = font;
            serialized.FindProperty("m_defaultFontAssetPath").stringValue = "";
            serialized.FindProperty("m_leadingCharacters").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(Phase5UiAssetPaths.LeadingCharactersPath);
            serialized.FindProperty("m_followingCharacters").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<TextAsset>(Phase5UiAssetPaths.FollowingCharactersPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material EnsureMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("UI/Default") ?? throw new InvalidOperationException("UI/Default shader missing.");
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.color = color; EditorUtility.SetDirty(material); return material;
        }

        private static AnimalCafeUiTheme EnsureTheme(TMP_FontAsset font, Material solid, Material light, Material strong)
        {
            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(Phase5UiAssetPaths.ThemePath);
            if (theme == null) { theme = ScriptableObject.CreateInstance<AnimalCafeUiTheme>(); AssetDatabase.CreateAsset(theme, Phase5UiAssetPaths.ThemePath); }
            theme.Colors = new UiSemanticColorTokens { Background = new Color(0.94f,0.88f,0.78f), Surface = new Color(1f,0.97f,0.9f), Text = new Color(0.12f,0.08f,0.06f), Accent = new Color(0.28f,0.43f,0.31f), Disabled = new Color(0.58f,0.58f,0.53f), Warning = new Color(0.92f,0.62f,0.2f), Destructive = new Color(0.62f,0.18f,0.16f) };
            theme.Typography = new UiTypographyTokens { Heading = new UiTextStyleToken(font, 28f, FontStyles.Bold, 0f), Body = new UiTextStyleToken(font, 16f, FontStyles.Normal, 4f), Label = new UiTextStyleToken(font, 14f, FontStyles.Normal, 2f) };
            theme.Spacing = new UiSpacingTokens(4, 8, 16, 24, 32); theme.Shape = new UiShapeTokens(16, 2);
            theme.Materials = new UiMaterialTokens(solid, light, strong, light);
            theme.Motion = new UiMotionTokens(0.1f, 0.22f, 0.18f, 0.16f, 2.5f);
            theme.Sizes = new UiSizeTokens(48, 48, 48, 64); EditorUtility.SetDirty(theme); return theme;
        }

        private static void BuildRoot()
        {
            var root = UiObject("UI Root");
            try
            {
                AddCanvas(root.transform, "HUD Canvas", 0, "HUD Layer");
                var screen = AddCanvas(root.transform, "Screen Canvas", 100, "Panel Layer");
                UiObject("Modal Layer", screen.transform);
                AddCanvas(root.transform, "Toast Canvas", 200, "Toast Layer").GetComponent<GraphicRaycaster>().enabled = false;
                Save(root, Phase5UiAssetPaths.UiRootPrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject AddCanvas(Transform parent, string name, int order, string layer)
        {
            var go = UiObject(name, parent); var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>(); UiObject(layer, go.transform); return go;
        }

        private static void BuildButton(AnimalCafeUiTheme theme, UiButtonRole role, UiButtonState state)
        {
            var path = $"{Phase5UiAssetPaths.Root}/Prefabs/PF_UI_Button_{role}_{state}.prefab";
            var root = UiObject($"PF_UI_Button_{role}_{state}");
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(180, 56);
                var image = root.AddComponent<Image>(); var button = root.AddComponent<Button>();
                button.interactable = state != UiButtonState.Disabled;
                var view = root.AddComponent<AnimalCafeButtonView>(); view.Configure(theme, role, button, image);
                if (state == UiButtonState.Pressed)
                {
                    // Pressed Prefabs are deterministic visual samples for the Task 9
                    // 3x3 validation fixture. The ordinary Default Prefab retains the
                    // live pointer-state component used by production screens.
                    image.color = Color.Lerp(GetButtonRoleColor(theme, role), Color.black, 0.15f);
                    view.enabled = false;
                }
                var label = UiObject("Label", root.transform); Stretch(label.GetComponent<RectTransform>(), 8f);
                var text = label.AddComponent<TextMeshProUGUI>(); text.font = theme.Typography.Label.FontAsset; text.fontSize = 14; text.text = role.ToString(); text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
                text.color = state == UiButtonState.Disabled || role == UiButtonRole.Secondary
                    ? theme.Colors.Text : Color.white;
                Save(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Color GetButtonRoleColor(AnimalCafeUiTheme theme, UiButtonRole role)
        {
            return role switch
            {
                UiButtonRole.Primary => theme.Colors.Accent,
                UiButtonRole.Secondary => theme.Colors.Surface,
                UiButtonRole.Destructive => theme.Colors.Destructive,
                _ => theme.Colors.Surface
            };
        }

        private static void BuildPanel(AnimalCafeUiTheme theme, UiPanelStyle style, string path)
        {
            var root = UiObject(Path.GetFileNameWithoutExtension(path));
            try { root.AddComponent<Image>(); root.AddComponent<AnimalCafePanelView>().Configure(theme, style, new StrongFrostLease(true)); Save(root, path); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildModal(AnimalCafeUiTheme theme)
        {
            var root = UiObject("PF_UI_Modal");
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(720f, 720f);
                root.AddComponent<Image>().color = theme.Colors.Surface;
                var group = root.AddComponent<CanvasGroup>();
                var blocker = CreateButton(root.transform, "Blocker", new Vector2(720f, 720f));
                blocker.transform.SetAsFirstSibling();
                var content = UiObject("Content", root.transform); content.GetComponent<RectTransform>().sizeDelta = new Vector2(640f, 560f);
                content.AddComponent<Image>().color = theme.Colors.Surface;
                var confirm = CreateButton(content.transform, "ConfirmButton", new Vector2(180f, 56f));
                var cancel = CreateButton(content.transform, "CancelButton", new Vector2(180f, 56f));
                var view = root.AddComponent<AnimalCafeModalView>();
                view.BindPrefabReferences(confirm, cancel, blocker, group);
                Save(root, Phase5UiAssetPaths.ModalPrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildBottomSheet(AnimalCafeUiTheme theme)
        {
            var root = UiObject("PF_UI_BottomSheet");
            try
            {
                root.GetComponent<RectTransform>().sizeDelta = new Vector2(1080f, 960f);
                var group = root.AddComponent<CanvasGroup>();
                var outside = CreateButton(root.transform, "OutsideButton", new Vector2(1080f, 960f));
                var content = UiObject("Content", root.transform); content.GetComponent<RectTransform>().sizeDelta = new Vector2(1080f, 640f);
                content.AddComponent<Image>().color = theme.Colors.Surface;
                var view = root.AddComponent<AnimalCafeBottomSheetView>();
                view.BindPrefabReferences(outside, group);
                Save(root, Phase5UiAssetPaths.BottomSheetPrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildSimple<T>(string path, string name, bool image)
            where T : Component
        {
            var root = UiObject(name);
            try
            {
                if (image) root.AddComponent<Image>();
                root.AddComponent<T>();
                Save(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildToast(AnimalCafeUiTheme theme)
        {
            var root = CreateFeedbackRoot("PF_UI_Toast", theme, new Vector2(640f, 96f), out var text);
            try { root.GetComponent<Image>().raycastTarget = false; text.raycastTarget = false;
                root.AddComponent<ToastView>().Configure(
                    new ToastQueue(() => Time.unscaledTime), text,
                    root.GetComponentsInChildren<Graphic>(true));
                Save(root, Phase5UiAssetPaths.ToastPrefabPath); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildTooltip(AnimalCafeUiTheme theme)
        {
            var root = UiObject("PF_UI_Tooltip");
            try { root.GetComponent<RectTransform>().sizeDelta = new Vector2(560f, 180f); root.AddComponent<Image>().color = theme.Colors.Surface;
                var content = UiObject("Content", root.transform); Stretch(content.GetComponent<RectTransform>(), 12f);
                var label = CreateLabel(content.transform, theme, "Label");
                root.AddComponent<TooltipView>().Configure(label, content); Save(root, Phase5UiAssetPaths.TooltipPrefabPath); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildValidationMessage(AnimalCafeUiTheme theme)
        {
            var root = CreateFeedbackRoot("PF_UI_ValidationMessage", theme, new Vector2(640f, 96f), out var text);
            try { root.GetComponent<Image>().raycastTarget = false; text.raycastTarget = false;
                root.AddComponent<ValidationMessageView>().Configure(text); Save(root, Phase5UiAssetPaths.ValidationMessagePrefabPath); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject CreateFeedbackRoot(string name, AnimalCafeUiTheme theme, Vector2 size, out TextMeshProUGUI text)
        {
            var root = UiObject(name); root.GetComponent<RectTransform>().sizeDelta = size;
            root.AddComponent<Image>().color = theme.Colors.Surface; text = CreateLabel(root.transform, theme, "Label"); return root;
        }

        private static TextMeshProUGUI CreateLabel(Transform parent, AnimalCafeUiTheme theme, string name)
        {
            var label = UiObject(name, parent); Stretch(label.GetComponent<RectTransform>(), 12f);
            var text = label.AddComponent<TextMeshProUGUI>(); text.font = theme.Typography.Body.FontAsset;
            text.fontSize = theme.Typography.Body.FontSize; text.color = theme.Colors.Text;
            text.textWrappingMode = TextWrappingModes.Normal; return text;
        }

        private static Button CreateButton(Transform parent, string name, Vector2 size)
        {
            var go = UiObject(name, parent); go.GetComponent<RectTransform>().sizeDelta = size;
            go.AddComponent<Image>(); return go.AddComponent<Button>();
        }

        private static void Stretch(RectTransform rect, float inset)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(inset, inset); rect.offsetMax = new Vector2(-inset, -inset); }

        private static GameObject UiObject(string name, Transform parent = null)
        { var go = new GameObject(name, typeof(RectTransform)); if (parent != null) go.transform.SetParent(parent, false); return go; }
        private static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
        private static void EnsureFolders()
        {
            foreach (var path in new[] { Phase5UiAssetPaths.Root, Phase5UiAssetPaths.Root+"/Theme", Phase5UiAssetPaths.Root+"/Fonts", Phase5UiAssetPaths.Root+"/Materials", Phase5UiAssetPaths.Root+"/Prefabs", Phase5UiAssetPaths.Root+"/Resources", Phase5UiAssetPaths.Root+"/Shaders" })
            { var parts = path.Split('/'); var current = parts[0]; for (var i=1;i<parts.Length;i++) { var next=current+"/"+parts[i]; if(!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,parts[i]); current=next; } }
        }
    }
}

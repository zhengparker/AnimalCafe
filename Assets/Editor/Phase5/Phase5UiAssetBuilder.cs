using System;
using System.IO;
using System.Security.Cryptography;
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
        private const string RequiredUiCharacters =
            "咖啡豆库存 Coffee Beans：今日可制作十二杯香草拿铁" +
            "糖浆口味 Syrup Flavor：焦糖与香草" +
            "确认取消返回暂停正常快速主要次要删除错误提示信息装修旋转放置保存" +
            "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
            " ，。：；！？-+/()[]%×";

        [MenuItem("AnimalCafe/Phase 5/Build UI Assets")]
        public static void BuildAll()
        {
            EnsureFolders();
            RequireLicensedFont();
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
            BuildSimple<AnimalCafeModalView>(Phase5UiAssetPaths.ModalPrefabPath, "PF_UI_Modal", true);
            BuildSimple<AnimalCafeBottomSheetView>(Phase5UiAssetPaths.BottomSheetPrefabPath, "PF_UI_BottomSheet", true);
            BuildTextPrefab<ToastView>(Phase5UiAssetPaths.ToastPrefabPath, "PF_UI_Toast", font, false);
            BuildTextPrefab<TooltipView>(Phase5UiAssetPaths.TooltipPrefabPath, "PF_UI_Tooltip", font, true);
            BuildTextPrefab<ValidationMessageView>(Phase5UiAssetPaths.ValidationMessagePrefabPath, "PF_UI_ValidationMessage", font, false);
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

        private static TMP_FontAsset EnsureTmpFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(Phase5UiAssetPaths.TmpFontAssetPath);
            if (existing != null && existing.sourceFontFile != null && existing.characterTable.Count > 0)
                return existing;
            if (existing != null)
            {
                AssetDatabase.DeleteAsset(Phase5UiAssetPaths.TmpFontAssetPath);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            var source = AssetDatabase.LoadAssetAtPath<Font>(Phase5UiAssetPaths.FontSourcePath)
                ?? throw new InvalidOperationException("Noto Sans SC did not import as a Unity Font.");
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
            theme.Colors = new UiSemanticColorTokens { Background = new Color(0.94f,0.88f,0.78f), Surface = new Color(1f,0.97f,0.9f), Text = new Color(0.2f,0.15f,0.12f), Accent = new Color(0.36f,0.53f,0.39f), Disabled = new Color(0.58f,0.58f,0.53f), Warning = new Color(0.92f,0.62f,0.2f), Destructive = new Color(0.72f,0.25f,0.22f) };
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
            go.AddComponent<CanvasScaler>(); go.AddComponent<GraphicRaycaster>(); UiObject(layer, go.transform); return go;
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
                var label = UiObject("Label", root.transform); var text = label.AddComponent<TextMeshProUGUI>(); text.font = theme.Typography.Label.FontAsset; text.fontSize = 14; text.text = role.ToString(); text.raycastTarget = false;
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

        private static void BuildSimple<T>(string path, string name, bool image) where T : Component
        {
            var root = UiObject(name);
            try { if (image) root.AddComponent<Image>(); root.AddComponent<T>(); Save(root, path); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildTextPrefab<T>(string path, string name, TMP_FontAsset font, bool image) where T : Component
        {
            var root = UiObject(name);
            try { if (image) root.AddComponent<Image>(); root.AddComponent<T>(); var label = UiObject("Label", root.transform); var text = label.AddComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = 16; text.text = name; if (typeof(T) == typeof(ToastView)) { text.raycastTarget = false; } Save(root, path); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static GameObject UiObject(string name, Transform parent = null)
        { var go = new GameObject(name, typeof(RectTransform)); if (parent != null) go.transform.SetParent(parent, false); return go; }
        private static void Save(GameObject root, string path) => PrefabUtility.SaveAsPrefabAsset(root, path);
        private static void EnsureFolders()
        {
            foreach (var path in new[] { Phase5UiAssetPaths.Root, Phase5UiAssetPaths.Root+"/Theme", Phase5UiAssetPaths.Root+"/Fonts", Phase5UiAssetPaths.Root+"/Materials", Phase5UiAssetPaths.Root+"/Prefabs", Phase5UiAssetPaths.Root+"/Resources", Phase5UiAssetPaths.Root+"/Shaders" })
            { var parts = path.Split('/'); var current = parts[0]; for (var i=1;i<parts.Length;i++) { var next=current+"/"+parts[i]; if(!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current,parts[i]); current=next; } }
        }
    }
}

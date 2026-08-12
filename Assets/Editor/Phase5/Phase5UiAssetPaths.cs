using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.UI.Foundation;

namespace AnimalCafe.EditorTools.Phase5
{
    public static class Phase5UiAssetPaths
    {
        public const string Root = "Assets/UI/Phase5";
        public const string ThemePath = Root + "/Theme/AnimalCafeUiTheme.asset";
        public const string FontSourcePath = Root + "/Fonts/NotoSansSC-Regular.otf";
        public const string TmpFontAssetPath = Root + "/Fonts/NotoSansSC-Regular SDF.asset";
        public const string FontLicensePath = Root + "/Fonts/OFL-1.1.txt";
        public const string FontProvenancePath = Root + "/Fonts/NotoSansSC-Regular.provenance.txt";
        public const string TmpSettingsPath = Root + "/Resources/TMP Settings.asset";
        public const string LeadingCharactersPath = Root + "/Resources/LineBreaking Leading Characters.txt";
        public const string FollowingCharactersPath = Root + "/Resources/LineBreaking Following Characters.txt";
        public const string TmpShaderPath = Root + "/Shaders/TMP_SDF-Mobile.shader";
        public const string TmpShaderIncludePath = Root + "/Shaders/TMPro_Properties.cginc";
        public const string TmpShaderProvenancePath = Root + "/Shaders/TMP-Essential-Resources.provenance.txt";
        public const string SolidMaterialPath = Root + "/Materials/M_UI_Solid.mat";
        public const string LightFrostMaterialPath = Root + "/Materials/M_UI_LightFrost.mat";
        public const string StrongFrostMaterialPath = Root + "/Materials/M_UI_StrongFrost.mat";
        public const string UiRootPrefabPath = Root + "/Prefabs/PF_UI_Root.prefab";
        public const string SolidPanelPrefabPath = Root + "/Prefabs/PF_UI_Panel_Solid.prefab";
        public const string LightFrostPanelPrefabPath = Root + "/Prefabs/PF_UI_Panel_LightFrost.prefab";
        public const string StrongFrostPanelPrefabPath = Root + "/Prefabs/PF_UI_Panel_StrongFrost.prefab";
        public const string ModalPrefabPath = Root + "/Prefabs/PF_UI_Modal.prefab";
        public const string BottomSheetPrefabPath = Root + "/Prefabs/PF_UI_BottomSheet.prefab";
        public const string ToastPrefabPath = Root + "/Prefabs/PF_UI_Toast.prefab";
        public const string TooltipPrefabPath = Root + "/Prefabs/PF_UI_Tooltip.prefab";
        public const string ValidationMessagePrefabPath = Root + "/Prefabs/PF_UI_ValidationMessage.prefab";
        public const string SafeAreaPrefabPath = Root + "/Prefabs/PF_UI_SafeArea.prefab";

        public static IReadOnlyList<string> ButtonPrefabPaths { get; } =
            (from UiButtonRole role in Enum.GetValues(typeof(UiButtonRole))
             from UiButtonState state in Enum.GetValues(typeof(UiButtonState))
             select $"{Root}/Prefabs/PF_UI_Button_{role}_{state}.prefab").ToArray();

        public static IReadOnlyList<string> RequiredAssetPaths { get; } = new[]
        {
            ThemePath, FontSourcePath, TmpFontAssetPath, FontLicensePath, FontProvenancePath, TmpSettingsPath,
            LeadingCharactersPath, FollowingCharactersPath,
            TmpShaderPath, TmpShaderIncludePath, TmpShaderProvenancePath,
            SolidMaterialPath, LightFrostMaterialPath, StrongFrostMaterialPath, UiRootPrefabPath,
            SolidPanelPrefabPath, LightFrostPanelPrefabPath, StrongFrostPanelPrefabPath,
            ModalPrefabPath, BottomSheetPrefabPath, ToastPrefabPath, TooltipPrefabPath,
            ValidationMessagePrefabPath, SafeAreaPrefabPath
        };

        public static IReadOnlyList<string> PrefabPaths { get; } = ButtonPrefabPaths.Concat(new[]
        {
            UiRootPrefabPath, SolidPanelPrefabPath, LightFrostPanelPrefabPath,
            StrongFrostPanelPrefabPath, ModalPrefabPath, BottomSheetPrefabPath,
            ToastPrefabPath, TooltipPrefabPath, ValidationMessagePrefabPath, SafeAreaPrefabPath
        }).ToArray();

        public static IReadOnlyList<string> AllGeneratedAssetPaths { get; } =
            RequiredAssetPaths.Concat(ButtonPrefabPaths).Distinct().ToArray();
    }
}

using System.Collections.Generic;

namespace AnimalCafe.EditorTools.Phase6
{
    public static class Phase6DecorationAssetPaths
    {
        public const string Phase6ArtFolderPath = "Assets/Art/Phase6";
        public const string DefinitionFolderPath = Phase6ArtFolderPath + "/Definitions";
        public const string PrefabFolderPath = Phase6ArtFolderPath + "/Prefabs";
        public const string CatalogueFolderPath = Phase6ArtFolderPath + "/Catalogues";
        public const string UiRootFolderPath = "Assets/UI/Phase6";
        public const string ThumbnailFolderPath = UiRootFolderPath + "/Thumbnails";
        public const string UiPrefabFolderPath = UiRootFolderPath + "/Prefabs";
        public const string UiFontFolderPath = UiRootFolderPath + "/Fonts";

        public const string Counter1x1DefinitionPath =
            "Assets/Art/Phase4/Definitions/FD_Furniture_CounterModule_01.asset";
        public const string Counter1x1PrefabPath =
            "Assets/Art/Phase4/Prefabs/PF_Furniture_CounterModule_01.prefab";
        public const string Counter1x2DefinitionPath =
            DefinitionFolderPath + "/FD_CounterPreset_1x2.asset";
        public const string Counter1x3DefinitionPath =
            DefinitionFolderPath + "/FD_CounterPreset_1x3.asset";
        public const string Counter2x3DefinitionPath =
            DefinitionFolderPath + "/FD_CounterPreset_2x3.asset";
        public const string Counter1x2PrefabPath =
            PrefabFolderPath + "/PF_CounterPreset_1x2.prefab";
        public const string Counter1x3PrefabPath =
            PrefabFolderPath + "/PF_CounterPreset_1x3.prefab";
        public const string Counter2x3PrefabPath =
            PrefabFolderPath + "/PF_CounterPreset_2x3.prefab";

        public const string Counter1x1ThumbnailPath =
            ThumbnailFolderPath + "/TH_CounterPreset_1x1.png";
        public const string Counter1x2ThumbnailPath =
            ThumbnailFolderPath + "/TH_CounterPreset_1x2.png";
        public const string Counter1x3ThumbnailPath =
            ThumbnailFolderPath + "/TH_CounterPreset_1x3.png";
        public const string Counter2x3ThumbnailPath =
            ThumbnailFolderPath + "/TH_CounterPreset_2x3.png";

        public const string DecorationCataloguePath =
            CatalogueFolderPath + "/DC_Phase6Decoration.asset";
        public const string ProductionCataloguePath =
            CatalogueFolderPath + "/FC_Phase6Production.asset";

        public const string DecorationUiFontPath =
            UiFontFolderPath + "/NotoSansSC-Phase6 SDF.asset";
        public const string DecorationCataloguePrefabPath =
            UiPrefabFolderPath + "/PF_UI_DecorationCatalogue.prefab";
        public const string DecorationActionBarPrefabPath =
            UiPrefabFolderPath + "/PF_UI_DecorationActionBar.prefab";
        public const string DecorationStoreModalPrefabPath =
            UiPrefabFolderPath + "/PF_UI_DecorationStoreModal.prefab";

        public static IReadOnlyList<string> ThumbnailPaths { get; } = new[]
        {
            Counter1x1ThumbnailPath,
            Counter1x2ThumbnailPath,
            Counter1x3ThumbnailPath,
            Counter2x3ThumbnailPath
        };

        public static IReadOnlyList<string> GeneratedAssetPaths { get; } = new[]
        {
            Counter1x2DefinitionPath,
            Counter1x3DefinitionPath,
            Counter2x3DefinitionPath,
            Counter1x2PrefabPath,
            Counter1x3PrefabPath,
            Counter2x3PrefabPath,
            Counter1x1ThumbnailPath,
            Counter1x2ThumbnailPath,
            Counter1x3ThumbnailPath,
            Counter2x3ThumbnailPath,
            DecorationCataloguePath,
            ProductionCataloguePath,
            DecorationUiFontPath,
            DecorationCataloguePrefabPath,
            DecorationActionBarPrefabPath,
            DecorationStoreModalPrefabPath
        };
    }
}

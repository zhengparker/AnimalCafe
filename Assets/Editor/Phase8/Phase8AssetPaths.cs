using System.Collections.Generic;

namespace AnimalCafe.EditorTools.Phase8
{
    public static class Phase8AssetPaths
    {
        public const string Root = "Assets/UI/Phase8";
        public const string PrefabFolder = Root + "/Prefabs";
        public const string ThumbnailFolder = Root + "/Thumbnails";
        public const string CatalogueFolder = Root + "/Catalogues";
        public const string MaterialFolder = Root + "/Materials";

        public const string FurnitureCataloguePath =
            CatalogueFolder + "/DC_Phase8Furniture.asset";
        public const string CashRegisterThumbnailPath =
            ThumbnailFolder + "/TH_Equipment_CashRegister_01.png";
        public const string CoffeeMachineThumbnailPath =
            ThumbnailFolder + "/TH_Equipment_CoffeeMachine_01.png";
        public const string EmployeeAnchorDebugMaterialPath =
            MaterialFolder + "/M_AnchorDebug_Employee.mat";
        public const string CustomerAnchorDebugMaterialPath =
            MaterialFolder + "/M_AnchorDebug_Customer.mat";

        public const string CataloguePrefabPath =
            PrefabFolder + "/PF_UI_Phase8DecorationCatalogue.prefab";
        public const string ActionBarPrefabPath =
            PrefabFolder + "/PF_UI_Phase8DecorationActionBar.prefab";
        public const string FunctionalSurfacePreviewPrefabPath =
            PrefabFolder + "/PF_UI_FunctionalSurfacePreview.prefab";
        public const string PickUpPointIndicatorPrefabPath =
            PrefabFolder + "/PF_UI_PickUpPointIndicator.prefab";

        public const string LegacyDecorationCataloguePath =
            "Assets/Art/Phase6/Catalogues/DC_Phase6Decoration.asset";
        public const string ProductionContentCataloguePath =
            "Assets/Art/Phase6/Catalogues/FC_Phase6Production.asset";
        public const string CashRegisterDefinitionPath =
            "Assets/Art/Phase4/Definitions/FD_Equipment_CashRegister_01.asset";
        public const string CoffeeMachineDefinitionPath =
            "Assets/Art/Phase4/Definitions/FD_Equipment_CoffeeMachine_01.asset";
        public const string UiFontPath =
            Root + "/Fonts/NotoSansSC-Phase8 SDF.asset";

        public const string MainCafeScenePath = "Assets/Scenes/MainCafe.unity";
        public const string ValidationScenePath =
            "Assets/Scenes/Validation/Phase8FunctionalFurniture.unity";
        public const string Phase7ValidationScenePath =
            "Assets/Scenes/Validation/Phase7InteriorWalls.unity";

        public const string RuntimeRootName = "Phase8_FunctionalRuntime";
        public const string MountedRepresentationRootName =
            "FunctionalSurfaceRepresentationRoot";
        public const string PreviewRootName = "FunctionalSurfacePreviewRoot";
        public const string PickUpIndicatorRootName = "PickUpPointIndicatorRoot";
        public const string AnchorDebugRootName = "InteractionAnchorDebugRoot";
        public const string ValidationMessageName = "Phase8_ValidationMessage";

        public static IReadOnlyList<string> RequiredControllerReferences { get; } =
            new[]
            {
                "surfaceMountedSceneRegistry",
                "surfaceMountedPreviewView",
                "pickUpPointIndicatorView",
                "validationMessageView",
                "surfaceMountedRepresentationRoot",
                "functionalSurfacePreviewRoot",
                "pickUpPointIndicatorRoot",
                "functionalSurfacePreviewPrefab",
                "pickUpPointIndicatorPrefab"
            };
    }
}

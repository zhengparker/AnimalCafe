namespace AnimalCafe.EditorTools.AssetPipeline
{
    public enum BenchmarkAssetIssueCode
    {
        InvalidAssetPath,
        InvalidName,
        RootTransformNotIdentity,
        BoundsOutsideTolerance,
        BelowGround,
        InvalidForwardMarker,
        MissingMesh,
        TriangleBudgetExceeded,
        MaterialSlotBudgetExceeded,
        MissingMaterial,
        InvalidShader,
        TransparentMaterial,
        TextureBudgetExceeded,
        InvalidColliderType,
        ColliderBudgetExceeded,
        TriggerColliderNotAllowed,
        ColliderOutsideModelBounds,
        MissingLodGroup,
        MissingLod1,
        LodTriangleBudgetExceeded,
        LodReductionInsufficient,
        MissingReference
    }
}

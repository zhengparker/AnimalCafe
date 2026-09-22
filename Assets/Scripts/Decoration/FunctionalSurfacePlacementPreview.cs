using AnimalCafe.Layout;

namespace AnimalCafe.Decoration
{
    public enum FunctionalSurfacePreviewKind
    {
        MountedEquipment = 0,
        PickUpPoint = 1
    }

    public sealed class FunctionalSurfacePlacementPreview
    {
        public FunctionalSurfacePreviewKind Kind { get; }
        public string InstanceId { get; }
        public string DefinitionId { get; }
        public SurfaceSlotAddress Address { get; }
        public FurnitureRotation Rotation { get; }
        public bool IsNew { get; }
        public FunctionalSurfacePlacementResult Validation { get; }
        public bool CanConfirm => Validation != null && Validation.Succeeded;

        internal FunctionalSurfacePlacementPreview(
            FunctionalSurfacePreviewKind kind,
            string instanceId,
            string definitionId,
            SurfaceSlotAddress address,
            FurnitureRotation rotation,
            bool isNew,
            FunctionalSurfacePlacementResult validation)
        {
            Kind = kind;
            InstanceId = instanceId;
            DefinitionId = definitionId;
            Address = address;
            Rotation = rotation;
            IsNew = isNew;
            Validation = validation;
        }

        internal FunctionalSurfacePlacementPreview WithPlacement(
            SurfaceSlotAddress address,
            FurnitureRotation rotation,
            FunctionalSurfacePlacementResult validation)
        {
            return new FunctionalSurfacePlacementPreview(
                Kind,
                InstanceId,
                DefinitionId,
                address,
                rotation,
                IsNew,
                validation);
        }

        internal FunctionalSurfacePlacementPreview WithValidation(
            FunctionalSurfacePlacementResult validation)
        {
            return WithPlacement(Address, Rotation, validation);
        }
    }
}

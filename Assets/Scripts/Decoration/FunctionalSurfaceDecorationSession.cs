using System;
using System.Linq;
using AnimalCafe.Layout;

namespace AnimalCafe.Decoration
{
    public sealed class FunctionalSurfaceDecorationSession
    {
        private readonly FunctionalSurfaceLayout confirmedLayout;

        public FunctionalSurfacePlacementPreview ActivePreview { get; private set; }

        public FunctionalSurfaceDecorationSession(FunctionalSurfaceLayout confirmedLayout)
        {
            this.confirmedLayout = confirmedLayout ??
                throw new ArgumentNullException(nameof(confirmedLayout));
        }

        public FunctionalSurfacePlacementResult BeginCreateMounted(
            string definitionId,
            SurfaceSlotAddress address)
        {
            Cancel();
            var instanceId = StableId.NewSurfaceMountedInstanceId();
            var result = confirmedLayout.ValidateMountedPreview(
                instanceId,
                definitionId,
                address,
                FurnitureRotation.Degrees0,
                null);
            ActivePreview = CreateMountedPreview(
                instanceId,
                definitionId,
                address,
                FurnitureRotation.Degrees0,
                true,
                result);
            return result;
        }

        public FunctionalSurfacePlacementResult BeginCreatePickUp(
            SurfaceSlotAddress address)
        {
            Cancel();
            var instanceId = StableId.NewPickUpPointInstanceId();
            var result = confirmedLayout.ValidatePickUpCandidate(
                instanceId,
                address,
                null);
            ActivePreview = new FunctionalSurfacePlacementPreview(
                FunctionalSurfacePreviewKind.PickUpPoint,
                instanceId,
                null,
                address,
                FurnitureRotation.Degrees0,
                true,
                result);
            return result;
        }

        public FunctionalSurfacePlacementResult BeginMoveMounted(string instanceId)
        {
            Cancel();
            var instance = confirmedLayout.MountedInstances.FirstOrDefault(candidate =>
                string.Equals(candidate.InstanceId, instanceId, StringComparison.Ordinal));
            if (instance == null)
            {
                return MissingInstanceResult(instanceId);
            }

            var result = confirmedLayout.ValidateMountedPreview(
                instance.InstanceId,
                instance.DefinitionId,
                instance.Address,
                instance.Rotation,
                instance.InstanceId);
            ActivePreview = CreateMountedPreview(
                instance.InstanceId,
                instance.DefinitionId,
                instance.Address,
                instance.Rotation,
                false,
                result);
            return result;
        }

        public FunctionalSurfacePlacementResult BeginMovePickUp(string instanceId)
        {
            Cancel();
            var instance = confirmedLayout.PickUpPoints.FirstOrDefault(candidate =>
                string.Equals(candidate.InstanceId, instanceId, StringComparison.Ordinal));
            if (instance == null)
            {
                return MissingInstanceResult(instanceId);
            }

            var result = confirmedLayout.ValidatePickUpCandidate(
                instance.InstanceId,
                instance.Address,
                instance.InstanceId);
            ActivePreview = new FunctionalSurfacePlacementPreview(
                FunctionalSurfacePreviewKind.PickUpPoint,
                instance.InstanceId,
                null,
                instance.Address,
                FurnitureRotation.Degrees0,
                false,
                result);
            return result;
        }

        public FunctionalSurfacePlacementResult MovePreview(SurfaceSlotAddress address)
        {
            if (ActivePreview == null)
            {
                return InstanceNotFoundResult();
            }

            var ignoredInstanceId = ActivePreview.IsNew
                ? null
                : ActivePreview.InstanceId;
            var result = ActivePreview.Kind == FunctionalSurfacePreviewKind.MountedEquipment
                ? confirmedLayout.ValidateMountedPreview(
                    ActivePreview.InstanceId,
                    ActivePreview.DefinitionId,
                    address,
                    ActivePreview.Rotation,
                    ignoredInstanceId)
                : confirmedLayout.ValidatePickUpCandidate(
                    ActivePreview.InstanceId,
                    address,
                    ignoredInstanceId);
            ActivePreview = ActivePreview.WithPlacement(
                address,
                ActivePreview.Rotation,
                result);
            return result;
        }

        public FunctionalSurfacePlacementResult RotatePreview()
        {
            if (ActivePreview == null)
            {
                return InstanceNotFoundResult();
            }

            if (ActivePreview.Kind == FunctionalSurfacePreviewKind.PickUpPoint)
            {
                return FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.UnsupportedAction);
            }

            var rotation = NextRotation(ActivePreview.Rotation);
            var result = confirmedLayout.ValidateMountedPreview(
                ActivePreview.InstanceId,
                ActivePreview.DefinitionId,
                ActivePreview.Address,
                rotation,
                ActivePreview.IsNew ? null : ActivePreview.InstanceId);
            ActivePreview = ActivePreview.WithPlacement(
                ActivePreview.Address,
                rotation,
                result);
            return result;
        }

        public FunctionalSurfacePlacementResult Confirm()
        {
            if (ActivePreview == null)
            {
                return InstanceNotFoundResult();
            }

            var validation = ValidateActivePreview();
            if (!validation.Succeeded)
            {
                ActivePreview = ActivePreview.WithValidation(validation);
                return validation;
            }

            FunctionalSurfacePlacementResult result;
            if (ActivePreview.Kind == FunctionalSurfacePreviewKind.MountedEquipment)
            {
                result = ActivePreview.IsNew
                    ? confirmedLayout.PlaceMounted(new SurfaceMountedInstance(
                        ActivePreview.InstanceId,
                        ActivePreview.DefinitionId,
                        ActivePreview.Address,
                        ActivePreview.Rotation))
                    : confirmedLayout.UpdateMountedPlacement(
                        ActivePreview.InstanceId,
                        ActivePreview.Address,
                        ActivePreview.Rotation);
            }
            else
            {
                result = ActivePreview.IsNew
                    ? confirmedLayout.PlacePickUp(new PickUpPointInstance(
                        ActivePreview.InstanceId,
                        ActivePreview.Address))
                    : confirmedLayout.MovePickUp(
                        ActivePreview.InstanceId,
                        ActivePreview.Address);
            }

            if (!result.Succeeded)
            {
                ActivePreview = ActivePreview.WithValidation(result);
                return result;
            }

            ActivePreview = null;
            return result;
        }

        private FunctionalSurfacePlacementResult ValidateActivePreview()
        {
            var ignoredInstanceId = ActivePreview.IsNew
                ? null
                : ActivePreview.InstanceId;
            return ActivePreview.Kind == FunctionalSurfacePreviewKind.MountedEquipment
                ? confirmedLayout.ValidateMountedPreview(
                    ActivePreview.InstanceId,
                    ActivePreview.DefinitionId,
                    ActivePreview.Address,
                    ActivePreview.Rotation,
                    ignoredInstanceId)
                : confirmedLayout.ValidatePickUpCandidate(
                    ActivePreview.InstanceId,
                    ActivePreview.Address,
                    ignoredInstanceId);
        }

        public void Cancel()
        {
            ActivePreview = null;
        }

        public FunctionalSurfacePlacementResult ConfirmStore()
        {
            if (ActivePreview == null || ActivePreview.IsNew)
            {
                return InstanceNotFoundResult();
            }

            var result = ActivePreview.Kind == FunctionalSurfacePreviewKind.MountedEquipment
                ? confirmedLayout.RemoveMounted(ActivePreview.InstanceId)
                : confirmedLayout.RemovePickUp(ActivePreview.InstanceId);
            if (!result.Succeeded)
            {
                ActivePreview = ActivePreview.WithValidation(result);
                return result;
            }

            ActivePreview = null;
            return result;
        }

        private static FunctionalSurfacePlacementPreview CreateMountedPreview(
            string instanceId,
            string definitionId,
            SurfaceSlotAddress address,
            FurnitureRotation rotation,
            bool isNew,
            FunctionalSurfacePlacementResult result)
        {
            return new FunctionalSurfacePlacementPreview(
                FunctionalSurfacePreviewKind.MountedEquipment,
                instanceId,
                definitionId,
                address,
                rotation,
                isNew,
                result);
        }

        private static FunctionalSurfacePlacementResult MissingInstanceResult(
            string instanceId)
        {
            return StableId.IsValidFurnitureInstanceId(instanceId)
                ? InstanceNotFoundResult()
                : FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.InvalidInstance);
        }

        private static FunctionalSurfacePlacementResult InstanceNotFoundResult()
        {
            return FunctionalSurfacePlacementResult.Failure(
                FunctionalSurfacePlacementFailureReason.InstanceNotFound);
        }

        private static FurnitureRotation NextRotation(FurnitureRotation rotation)
        {
            switch (rotation)
            {
                case FurnitureRotation.Degrees0:
                    return FurnitureRotation.Degrees90;
                case FurnitureRotation.Degrees90:
                    return FurnitureRotation.Degrees180;
                case FurnitureRotation.Degrees180:
                    return FurnitureRotation.Degrees270;
                case FurnitureRotation.Degrees270:
                    return FurnitureRotation.Degrees0;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(rotation),
                        rotation,
                        "Rotation must be a known value.");
            }
        }
    }
}

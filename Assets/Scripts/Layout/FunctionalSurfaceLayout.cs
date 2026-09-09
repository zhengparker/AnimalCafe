using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AnimalCafe.Layout
{
    public sealed class FunctionalSurfaceLayout
    {
        private readonly CafeLayout cafeLayout;
        private readonly FurnitureDefinitionCatalog definitionCatalog;
        private readonly SurfaceSlotCatalog surfaceSlotCatalog;
        private readonly InteractionAnchorResolver anchorResolver;
        private readonly List<SurfaceMountedInstance> mountedInstances;
        private readonly Dictionary<string, SurfaceMountedInstance> mountedInstancesById;
        private readonly List<PickUpPointInstance> pickUpPoints;
        private readonly Dictionary<string, PickUpPointInstance> pickUpPointsById;
        private readonly Dictionary<SurfaceSlotAddress, string> occupantByAddress;

        public IReadOnlyList<SurfaceMountedInstance> MountedInstances { get; }
        public IReadOnlyList<PickUpPointInstance> PickUpPoints { get; }

        public FunctionalSurfaceLayout(
            CafeLayout cafeLayout,
            FurnitureDefinitionCatalog definitionCatalog,
            SurfaceSlotCatalog surfaceSlotCatalog)
        {
            this.cafeLayout = cafeLayout ?? throw new ArgumentNullException(nameof(cafeLayout));
            this.definitionCatalog = definitionCatalog ??
                throw new ArgumentNullException(nameof(definitionCatalog));
            this.surfaceSlotCatalog = surfaceSlotCatalog ??
                throw new ArgumentNullException(nameof(surfaceSlotCatalog));
            anchorResolver = new InteractionAnchorResolver();

            mountedInstances = new List<SurfaceMountedInstance>();
            mountedInstancesById = new Dictionary<string, SurfaceMountedInstance>(
                StringComparer.Ordinal);
            pickUpPoints = new List<PickUpPointInstance>();
            pickUpPointsById = new Dictionary<string, PickUpPointInstance>(StringComparer.Ordinal);
            occupantByAddress = new Dictionary<SurfaceSlotAddress, string>();

            MountedInstances = new ReadOnlyCollection<SurfaceMountedInstance>(mountedInstances);
            PickUpPoints = new ReadOnlyCollection<PickUpPointInstance>(pickUpPoints);
        }

        public FunctionalSurfacePlacementResult PlaceMounted(SurfaceMountedInstance instance)
        {
            if (instance == null)
            {
                return FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.InvalidInstance);
            }

            if (ContainsInstanceId(instance.InstanceId))
            {
                return FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.InstanceAlreadyPlaced);
            }

            var validation = ValidateMountedCandidate(instance, null);
            if (!validation.Succeeded)
            {
                return validation;
            }

            mountedInstancesById.Add(instance.InstanceId, instance);
            mountedInstances.Add(instance);
            occupantByAddress.Add(instance.Address, instance.InstanceId);
            return FunctionalSurfacePlacementResult.Success();
        }

        public FunctionalSurfacePlacementResult MoveMounted(
            string instanceId,
            SurfaceSlotAddress address)
        {
            if (!IsValidInstanceId(instanceId))
            {
                return InvalidInstanceResult();
            }

            if (!IsValidAddress(address))
            {
                return InvalidAddressResult();
            }

            if (!mountedInstancesById.TryGetValue(instanceId, out var current))
            {
                return InstanceNotFoundResult();
            }

            return PublishMountedPlacement(current, address, current.Rotation);
        }

        public FunctionalSurfacePlacementResult RotateMounted(
            string instanceId,
            FurnitureRotation rotation)
        {
            if (!IsValidInstanceId(instanceId))
            {
                return InvalidInstanceResult();
            }

            if (!IsKnownRotation(rotation))
            {
                return FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.InvalidRotation);
            }

            if (!mountedInstancesById.TryGetValue(instanceId, out var current))
            {
                return InstanceNotFoundResult();
            }

            return PublishMountedPlacement(current, current.Address, rotation);
        }

        internal FunctionalSurfacePlacementResult UpdateMountedPlacement(
            string instanceId,
            SurfaceSlotAddress address,
            FurnitureRotation rotation)
        {
            if (!IsValidInstanceId(instanceId))
            {
                return InvalidInstanceResult();
            }

            if (!IsValidAddress(address))
            {
                return InvalidAddressResult();
            }

            if (!IsKnownRotation(rotation))
            {
                return FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.InvalidRotation);
            }

            if (!mountedInstancesById.TryGetValue(instanceId, out var current))
            {
                return InstanceNotFoundResult();
            }

            return PublishMountedPlacement(current, address, rotation);
        }

        private FunctionalSurfacePlacementResult PublishMountedPlacement(
            SurfaceMountedInstance current,
            SurfaceSlotAddress address,
            FurnitureRotation rotation)
        {
            var candidate = new SurfaceMountedInstance(
                current.InstanceId,
                current.DefinitionId,
                address,
                rotation);
            var validation = ValidateMountedCandidate(candidate, current.InstanceId);
            if (!validation.Succeeded)
            {
                return validation;
            }

            var index = mountedInstances.IndexOf(current);
            ReleaseAddress(current.Address, current.InstanceId);
            mountedInstances[index] = candidate;
            mountedInstancesById[current.InstanceId] = candidate;
            occupantByAddress.Add(candidate.Address, candidate.InstanceId);
            return FunctionalSurfacePlacementResult.Success();
        }

        public FunctionalSurfacePlacementResult RemoveMounted(string instanceId)
        {
            if (!IsValidInstanceId(instanceId))
            {
                return InvalidInstanceResult();
            }

            if (!mountedInstancesById.TryGetValue(instanceId, out var instance))
            {
                return InstanceNotFoundResult();
            }

            ReleaseAddress(instance.Address, instance.InstanceId);
            mountedInstancesById.Remove(instance.InstanceId);
            mountedInstances.Remove(instance);
            return FunctionalSurfacePlacementResult.Success();
        }

        public FunctionalSurfacePlacementResult PlacePickUp(PickUpPointInstance instance)
        {
            if (instance == null)
            {
                return FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.InvalidInstance);
            }

            if (ContainsInstanceId(instance.InstanceId))
            {
                return FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.InstanceAlreadyPlaced);
            }

            var validation = ValidatePickUpCandidate(
                instance.InstanceId,
                instance.Address,
                null);
            if (!validation.Succeeded)
            {
                return validation;
            }

            pickUpPointsById.Add(instance.InstanceId, instance);
            pickUpPoints.Add(instance);
            occupantByAddress.Add(instance.Address, instance.InstanceId);
            return FunctionalSurfacePlacementResult.Success();
        }

        public FunctionalSurfacePlacementResult MovePickUp(
            string instanceId,
            SurfaceSlotAddress address)
        {
            if (!IsValidInstanceId(instanceId))
            {
                return InvalidInstanceResult();
            }

            if (!IsValidAddress(address))
            {
                return InvalidAddressResult();
            }

            if (!pickUpPointsById.TryGetValue(instanceId, out var current))
            {
                return InstanceNotFoundResult();
            }

            var candidate = new PickUpPointInstance(current.InstanceId, address);
            var validation = ValidatePickUpCandidate(
                candidate.InstanceId,
                candidate.Address,
                current.InstanceId);
            if (!validation.Succeeded)
            {
                return validation;
            }

            var index = pickUpPoints.IndexOf(current);
            ReleaseAddress(current.Address, current.InstanceId);
            pickUpPoints[index] = candidate;
            pickUpPointsById[current.InstanceId] = candidate;
            occupantByAddress.Add(candidate.Address, candidate.InstanceId);
            return FunctionalSurfacePlacementResult.Success();
        }

        public FunctionalSurfacePlacementResult RemovePickUp(string instanceId)
        {
            if (!IsValidInstanceId(instanceId))
            {
                return InvalidInstanceResult();
            }

            if (!pickUpPointsById.TryGetValue(instanceId, out var instance))
            {
                return InstanceNotFoundResult();
            }

            ReleaseAddress(instance.Address, instance.InstanceId);
            pickUpPointsById.Remove(instance.InstanceId);
            pickUpPoints.Remove(instance);
            return FunctionalSurfacePlacementResult.Success();
        }

        public bool TryGetOccupant(SurfaceSlotAddress address, out string instanceId)
        {
            return occupantByAddress.TryGetValue(address, out instanceId);
        }

        internal bool TryGetDefinition(string definitionId, out FurnitureDefinition definition)
        {
            return definitionCatalog.TryGet(definitionId, out definition);
        }

        public IReadOnlyList<string> GetContentIdsForSupport(string supportFurnitureInstanceId)
        {
            if (!IsValidInstanceId(supportFurnitureInstanceId))
            {
                return Array.Empty<string>();
            }

            var contentIds = new List<string>();
            foreach (var instance in mountedInstances)
            {
                if (string.Equals(
                    instance.Address.SupportFurnitureInstanceId,
                    supportFurnitureInstanceId,
                    StringComparison.Ordinal))
                {
                    contentIds.Add(instance.InstanceId);
                }
            }

            foreach (var point in pickUpPoints)
            {
                if (string.Equals(
                    point.Address.SupportFurnitureInstanceId,
                    supportFurnitureInstanceId,
                    StringComparison.Ordinal))
                {
                    contentIds.Add(point.InstanceId);
                }
            }

            return new ReadOnlyCollection<string>(contentIds);
        }

        internal FunctionalSurfacePlacementResult ValidateMountedCandidate(
            SurfaceMountedInstance candidate,
            string ignoredInstanceId)
        {
            if (!definitionCatalog.TryGet(candidate.DefinitionId, out var definition))
            {
                return FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.MissingDefinition);
            }

            if (definition.FunctionType != FurnitureFunctionType.CashRegister &&
                definition.FunctionType != FurnitureFunctionType.CoffeeMachine)
            {
                return FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.UnsupportedFunction);
            }

            if ((definition.AllowedPlacementSurfaces &
                PlacementSurfaceType.FurnitureSurface) == 0)
            {
                return FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.UnsupportedPlacementSurface);
            }

            return ValidateAddressCandidate(candidate.Address, ignoredInstanceId);
        }

        internal FunctionalSurfacePlacementResult ValidatePickUpCandidate(
            SurfaceSlotAddress address,
            string ignoredInstanceId)
        {
            return ValidatePickUpCandidate(
                "00000000000000000000000000000000",
                address,
                ignoredInstanceId);
        }

        internal FunctionalSurfacePlacementResult ValidatePickUpCandidate(
            string instanceId,
            SurfaceSlotAddress address,
            string ignoredInstanceId)
        {
            if (!IsValidInstanceId(instanceId))
            {
                return InvalidInstanceResult();
            }

            if (!IsValidAddress(address))
            {
                return InvalidAddressResult();
            }

            var addressValidation = ValidateAddressCandidate(address, ignoredInstanceId);
            if (!addressValidation.Succeeded)
            {
                return addressValidation;
            }

            var anchors = anchorResolver.ResolvePickUp(
                new PickUpPointInstance(instanceId, address),
                cafeLayout,
                surfaceSlotCatalog,
                IsPickUpAnchorWalkable);
            return anchors.Anchors.Count > 0
                ? FunctionalSurfacePlacementResult.Success()
                : FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.NoValidInteractionAnchor);
        }

        internal FunctionalSurfacePlacementResult ValidateMountedPreview(
            string instanceId,
            string definitionId,
            SurfaceSlotAddress address,
            FurnitureRotation rotation,
            string ignoredInstanceId)
        {
            if (!IsValidInstanceId(instanceId))
            {
                return InvalidInstanceResult();
            }

            if (!IsValidAddress(address))
            {
                return InvalidAddressResult();
            }

            if (!IsKnownRotation(rotation))
            {
                return FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.InvalidRotation);
            }

            return ValidateMountedCandidate(
                new SurfaceMountedInstance(
                    instanceId,
                    definitionId,
                    address,
                    rotation),
                ignoredInstanceId);
        }

        private FunctionalSurfacePlacementResult ValidateAddressCandidate(
            SurfaceSlotAddress address,
            string ignoredInstanceId)
        {
            if (!cafeLayout.TryGetFurnitureInstance(
                address.SupportFurnitureInstanceId,
                out var supportFurniture))
            {
                return FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.MissingSupportFurniture);
            }

            if (!surfaceSlotCatalog.TryGet(supportFurniture.DefinitionId, address.SlotId, out _))
            {
                return FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.MissingSurfaceSlot);
            }

            if (occupantByAddress.TryGetValue(address, out var occupantId) &&
                !string.Equals(occupantId, ignoredInstanceId, StringComparison.Ordinal))
            {
                return FunctionalSurfacePlacementResult.Failure(
                    FunctionalSurfacePlacementFailureReason.SlotOccupied);
            }

            return FunctionalSurfacePlacementResult.Success();
        }

        private bool IsPickUpAnchorWalkable(GridPosition position)
        {
            return cafeLayout.IsInsideUnlockedRegion(position) &&
                   !cafeLayout.HasReservation(position, LayoutReservationType.Blocked) &&
                   !cafeLayout.TryGetOccupant(position, out _);
        }

        private bool ContainsInstanceId(string instanceId)
        {
            return mountedInstancesById.ContainsKey(instanceId) ||
                   pickUpPointsById.ContainsKey(instanceId);
        }

        private static bool IsValidInstanceId(string instanceId)
        {
            return StableId.IsValidFurnitureInstanceId(instanceId);
        }

        private static bool IsKnownRotation(FurnitureRotation rotation)
        {
            return rotation == FurnitureRotation.Degrees0 ||
                   rotation == FurnitureRotation.Degrees90 ||
                   rotation == FurnitureRotation.Degrees180 ||
                   rotation == FurnitureRotation.Degrees270;
        }

        private static bool IsValidAddress(SurfaceSlotAddress address)
        {
            return IsValidInstanceId(address.SupportFurnitureInstanceId) &&
                   LayoutStableId.IsValid(address.SlotId);
        }

        private void ReleaseAddress(SurfaceSlotAddress address, string instanceId)
        {
            if (occupantByAddress.TryGetValue(address, out var occupantId) &&
                string.Equals(occupantId, instanceId, StringComparison.Ordinal))
            {
                occupantByAddress.Remove(address);
            }
        }

        private static FunctionalSurfacePlacementResult InvalidInstanceResult()
        {
            return FunctionalSurfacePlacementResult.Failure(
                FunctionalSurfacePlacementFailureReason.InvalidInstance);
        }

        private static FunctionalSurfacePlacementResult InstanceNotFoundResult()
        {
            return FunctionalSurfacePlacementResult.Failure(
                FunctionalSurfacePlacementFailureReason.InstanceNotFound);
        }

        private static FunctionalSurfacePlacementResult InvalidAddressResult()
        {
            return FunctionalSurfacePlacementResult.Failure(
                FunctionalSurfacePlacementFailureReason.InvalidSurfaceSlotAddress);
        }
    }
}

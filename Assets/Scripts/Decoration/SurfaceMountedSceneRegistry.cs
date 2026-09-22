using System;
using System.Collections.Generic;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    /// <summary>Owns Scene representations keyed only by confirmed mounted instance IDs.</summary>
    public sealed class SurfaceMountedSceneRegistry : MonoBehaviour
    {
        private readonly Dictionary<string, GameObject> representationsById =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<string, SurfaceMountedInstance> bindingsById =
            new Dictionary<string, SurfaceMountedInstance>(StringComparer.Ordinal);

        private FurnitureContentCatalog contentCatalog;
        private FurnitureSceneRegistry furnitureRegistry;
        private Transform representationRoot;

        public void Configure(
            FurnitureContentCatalog contentCatalog,
            FurnitureSceneRegistry furnitureRegistry,
            Transform representationRoot)
        {
            this.contentCatalog = contentCatalog ??
                throw new ArgumentNullException(nameof(contentCatalog));
            this.furnitureRegistry = furnitureRegistry ??
                throw new ArgumentNullException(nameof(furnitureRegistry));
            this.representationRoot = representationRoot ??
                throw new ArgumentNullException(nameof(representationRoot));
            ClearOwnedRepresentations();
        }

        public void Rebuild(IReadOnlyList<SurfaceMountedInstance> instances)
        {
            EnsureConfigured();
            if (instances == null)
            {
                throw new ArgumentNullException(nameof(instances));
            }

            var countsById = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < instances.Count; index++)
            {
                var instance = instances[index] ?? throw new ArgumentException(
                    $"Surface-mounted instance at index {index} must not be null.",
                    nameof(instances));
                countsById.TryGetValue(instance.InstanceId, out var count);
                countsById[instance.InstanceId] = count + 1;
            }

            ClearOwnedRepresentations();
            for (var index = 0; index < instances.Count; index++)
            {
                var instance = instances[index];
                if (countsById[instance.InstanceId] != 1 ||
                    !contentCatalog.TryGetDefinitionAsset(
                        instance.DefinitionId,
                        out var definition) ||
                    definition.Prefab == null ||
                    !FunctionalSurfaceViewPositioning.TryResolveSlot(
                        instance.Address,
                        furnitureRegistry,
                        out var support,
                        out var slot))
                {
                    continue;
                }

                var representation = Instantiate(
                    definition.Prefab,
                    representationRoot,
                    false);
                representation.name = "SurfaceMounted_" + instance.InstanceId;
                representation.transform.localScale = definition.Prefab.transform.localScale;
                FunctionalSurfaceViewPositioning.Apply(
                    representation.transform,
                    support,
                    slot,
                    instance.Rotation);
                representation.SetActive(true);
                representationsById.Add(instance.InstanceId, representation);
                bindingsById.Add(instance.InstanceId, instance);
            }
        }

        public void ProjectSupportPreview(
            string supportFurnitureInstanceId,
            Transform previewSupport)
        {
            EnsureConfigured();
            if (supportFurnitureInstanceId == null)
            {
                throw new ArgumentNullException(nameof(supportFurnitureInstanceId));
            }
            if (previewSupport == null)
            {
                throw new ArgumentNullException(nameof(previewSupport));
            }

            foreach (var pair in bindingsById)
            {
                var instance = pair.Value;
                if (!string.Equals(
                    instance.Address.SupportFurnitureInstanceId,
                    supportFurnitureInstanceId,
                    StringComparison.Ordinal)
                    || !representationsById.TryGetValue(pair.Key, out var representation)
                    || representation == null)
                {
                    continue;
                }

                var resolved = FunctionalSurfaceViewPositioning.TryResolveSlot(
                    instance.Address,
                    previewSupport,
                    out var slot);
                representation.SetActive(resolved);
                if (resolved)
                {
                    FunctionalSurfaceViewPositioning.Apply(
                        representation.transform,
                        previewSupport,
                        slot,
                        instance.Rotation);
                }
            }
        }

        public bool TryGet(string instanceId, out GameObject representation)
        {
            if (instanceId != null &&
                representationsById.TryGetValue(instanceId, out representation) &&
                representation != null)
            {
                return true;
            }

            representation = null;
            return false;
        }

        private void OnDisable()
        {
            ClearOwnedRepresentations();
        }

        private void OnDestroy()
        {
            ClearOwnedRepresentations();
        }

        private void EnsureConfigured()
        {
            if (contentCatalog == null ||
                furnitureRegistry == null ||
                representationRoot == null)
            {
                throw new InvalidOperationException(
                    "SurfaceMountedSceneRegistry must be configured before use.");
            }
        }

        private void ClearOwnedRepresentations()
        {
            foreach (var representation in representationsById.Values)
            {
                if (representation == null)
                {
                    continue;
                }

                representation.SetActive(false);
                Destroy(representation);
            }

            representationsById.Clear();
            bindingsById.Clear();
        }
    }

    internal static class FunctionalSurfaceViewPositioning
    {
        public static bool TryResolveSlot(
            SurfaceSlotAddress address,
            FurnitureSceneRegistry furnitureRegistry,
            out Transform support,
            out Transform slot)
        {
            support = null;
            slot = null;
            if (furnitureRegistry == null ||
                !furnitureRegistry.TryGet(
                    address.SupportFurnitureInstanceId,
                    out var supportRepresentation))
            {
                return false;
            }

            support = supportRepresentation.transform;
            return TryResolveSlot(address, support, out slot);
        }

        public static bool TryResolveSlot(
            SurfaceSlotAddress address,
            Transform support,
            out Transform slot)
        {
            slot = null;
            if (support == null)
            {
                return false;
            }

            SurfaceSlotMarker match = null;
            foreach (var marker in support.GetComponentsInChildren<SurfaceSlotMarker>(true))
            {
                if (!string.Equals(
                    marker.SlotId,
                    address.SlotId,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                if (match != null)
                {
                    slot = null;
                    return false;
                }

                match = marker;
            }

            if (match == null)
            {
                return false;
            }

            slot = match.transform;
            return true;
        }

        public static void Apply(
            Transform target,
            Transform support,
            Transform slot,
            FurnitureRotation localRotation)
        {
            target.SetPositionAndRotation(
                slot.position,
                support.rotation * ToQuaternion(localRotation));
        }

        public static Quaternion ToQuaternion(FurnitureRotation rotation)
        {
            switch (rotation)
            {
                case FurnitureRotation.Degrees0:
                    return Quaternion.identity;
                case FurnitureRotation.Degrees90:
                    return Quaternion.Euler(0f, 90f, 0f);
                case FurnitureRotation.Degrees180:
                    return Quaternion.Euler(0f, 180f, 0f);
                case FurnitureRotation.Degrees270:
                    return Quaternion.Euler(0f, 270f, 0f);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(rotation),
                        rotation,
                        "Rotation must be a defined quarter turn.");
            }
        }
    }
}

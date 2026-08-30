using System;
using System.Collections.Generic;
using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    /// <summary>Scene-only lookup for stable wall-mounted instance IDs.</summary>
    public sealed class WallMountedSceneRegistry : MonoBehaviour
    {
        private readonly Dictionary<string, GameObject> representationsByInstanceId =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);

        public void Register(string instanceId, GameObject representation)
        {
            LayoutStableId.Validate(instanceId, nameof(instanceId));

            if (representation == null)
            {
                throw new ArgumentNullException(nameof(representation));
            }

            PurgeDestroyedRepresentations();
            if (representationsByInstanceId.TryGetValue(instanceId, out var existing))
            {
                if (existing != null)
                {
                    throw new InvalidOperationException($"Duplicate wall-mounted instance '{instanceId}'.");
                }

                representationsByInstanceId.Remove(instanceId);
            }

            representationsByInstanceId.Add(instanceId, representation);
        }

        public bool TryGet(string instanceId, out GameObject representation)
        {
            if (!LayoutStableId.IsValid(instanceId) ||
                !representationsByInstanceId.TryGetValue(instanceId, out representation))
            {
                representation = null;
                return false;
            }

            if (representation != null)
            {
                return true;
            }

            representationsByInstanceId.Remove(instanceId);
            return false;
        }

        public bool TryGetInstanceId(Collider collider, out string instanceId)
        {
            PurgeDestroyedRepresentations();
            if (collider != null)
            {
                foreach (var pair in representationsByInstanceId)
                {
                    if (pair.Value != null
                        && (collider.gameObject == pair.Value
                            || collider.transform.IsChildOf(pair.Value.transform)))
                    {
                        instanceId = pair.Key;
                        return true;
                    }
                }
            }

            instanceId = null;
            return false;
        }

        public bool Remove(string instanceId, bool destroyRepresentation)
        {
            if (!LayoutStableId.IsValid(instanceId))
            {
                return false;
            }

            PurgeDestroyedRepresentations();
            if (!representationsByInstanceId.TryGetValue(instanceId, out var representation))
            {
                return false;
            }

            representationsByInstanceId.Remove(instanceId);
            if (destroyRepresentation && representation != null)
            {
                Destroy(representation);
            }

            return true;
        }

        private void PurgeDestroyedRepresentations()
        {
            var destroyedIds = new List<string>();
            foreach (var pair in representationsByInstanceId)
            {
                if (pair.Value == null)
                {
                    destroyedIds.Add(pair.Key);
                }
            }

            foreach (var instanceId in destroyedIds)
            {
                representationsByInstanceId.Remove(instanceId);
            }
        }
    }
}

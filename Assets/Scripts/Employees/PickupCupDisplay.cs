using System;
using System.Collections.Generic;
using AnimalCafe.Layout;
using UnityEngine;
using UnityEngine.AI;

namespace AnimalCafe.Employees
{
    /// <summary>纯视觉交付展示 / Up to three bottom-pivot cups per confirmed surface slot.</summary>
    public sealed class PickupCupDisplay : MonoBehaviour
    {
        private static readonly Vector3[] Offsets =
        {
            new Vector3(-.12f, 0f, -.08f),
            new Vector3(.12f, 0f, -.08f),
            new Vector3(0f, 0f, .12f)
        };

        private readonly Dictionary<SurfaceSlotAddress, List<GameObject>> cups =
            new Dictionary<SurfaceSlotAddress, List<GameObject>>();
        private GameObject cupPrefab;
        private Func<SurfaceSlotAddress, Transform> resolveConfirmedModule;

        public void Configure(GameObject cupPrefab,
            Func<SurfaceSlotAddress, Transform> resolveConfirmedModule)
        {
            Clear();
            this.cupPrefab = cupPrefab;
            this.resolveConfirmedModule = resolveConfirmedModule;
        }

        public bool TryDeliver(SurfaceSlotAddress address, out string reason)
        {
            reason = string.Empty;
            var module = Resolve(address);
            if (module == null)
            {
                Remove(address);
                reason = "Confirmed pickup surface slot is unavailable.";
                return false;
            }

            Synchronize(address, module);
            cups.TryGetValue(address, out var existing);
            // 饱和后也必须先验证正式绑定 / Validate even when no new visual is needed.
            if (existing != null && existing.Count == Offsets.Length) return true;
            if (cupPrefab == null)
            {
                reason = "Cup prefab is missing or destroyed.";
                return false;
            }

            GameObject staging = null;
            GameObject candidate = null;
            try
            {
                // Inactive parenting prevents prefab behaviours from registering on activation.
                staging = new GameObject("PickupCup_Staging");
                staging.SetActive(false);
                staging.transform.SetParent(transform, false);
                candidate = Instantiate(cupPrefab, staging.transform, false);
                candidate.SetActive(false);
                if (candidate.GetComponentsInChildren<Renderer>(true).Length == 0 ||
                    candidate.GetComponentsInChildren<MonoBehaviour>(true).Length != 0 ||
                    candidate.GetComponentsInChildren<NavMeshObstacle>(true).Length != 0 ||
                    candidate.GetComponentsInChildren<NavMeshAgent>(true).Length != 0 ||
                    candidate.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                {
                    reason = "Cup prefab must contain only a passive rendered visual and optional colliders.";
                    return false;
                }

                foreach (var collider in candidate.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                    DestroyOwned(collider);
                }

                if (module == null || candidate == null)
                {
                    reason = "Cup visual or confirmed slot disappeared during creation.";
                    return false;
                }

                var count = existing?.Count ?? 0;
                candidate.name = "PickupCup_" + (count + 1);
                candidate.transform.SetParent(module, false);
                candidate.transform.localPosition = Offsets[count];
                candidate.transform.localRotation = Quaternion.identity;
                candidate.transform.localScale = cupPrefab.transform.localScale;
                candidate.SetActive(true);

                // Commit ownership only after the complete visual exists at the surface.
                if (existing == null)
                {
                    existing = new List<GameObject>(Offsets.Length);
                    cups.Add(address, existing);
                }
                existing.Add(candidate);
                candidate = null;
                return true;
            }
            catch (Exception exception)
            {
                reason = "Cup visual creation failed: " + exception.Message;
                return false;
            }
            finally
            {
                DestroyOwned(candidate);
                DestroyOwned(staging);
            }
        }

        public int VisibleCount(SurfaceSlotAddress address)
        {
            if (!cups.TryGetValue(address, out var owned)) return 0;
            owned.RemoveAll(cup => cup == null);
            return owned.Count;
        }

        public void RefreshConfirmedSupports()
        {
            foreach (var address in new List<SurfaceSlotAddress>(cups.Keys))
            {
                var module = Resolve(address);
                if (module == null) Remove(address);
                else Synchronize(address, module);
            }
        }

        public void Clear()
        {
            foreach (var owned in cups.Values)
                foreach (var cup in owned) DestroyOwned(cup);
            cups.Clear();
        }

        private Transform Resolve(SurfaceSlotAddress address)
        {
            if (string.IsNullOrEmpty(address.SupportFurnitureInstanceId) ||
                string.IsNullOrEmpty(address.SlotId) || resolveConfirmedModule == null) return null;
            return resolveConfirmedModule(address);
        }

        private void Synchronize(SurfaceSlotAddress address, Transform module)
        {
            if (!cups.TryGetValue(address, out var owned)) return;
            owned.RemoveAll(cup => cup == null);
            for (var i = 0; i < owned.Count; i++)
            {
                owned[i].transform.SetParent(module, false);
                owned[i].transform.localPosition = Offsets[i];
                owned[i].transform.localRotation = Quaternion.identity;
            }
        }

        private void Remove(SurfaceSlotAddress address)
        {
            if (!cups.TryGetValue(address, out var owned)) return;
            cups.Remove(address);
            foreach (var cup in owned) DestroyOwned(cup);
        }

        private void OnDestroy() => Clear();

        private static void DestroyOwned(UnityEngine.Object target)
        {
            if (target == null) return;
            if (target is GameObject gameObject) gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}

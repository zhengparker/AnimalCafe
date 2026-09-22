using System;
using System.Collections.Generic;
using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    /// <summary>Developer-only anchor rendering, enabled only by an explicit debug flag.</summary>
    public sealed class InteractionAnchorDebugView : MonoBehaviour
    {
        private readonly List<GameObject> ownedMarkers = new List<GameObject>();

        private Transform debugRoot;
        private DecorationGridSpace gridSpace;
        private Material employeeMaterial;
        private Material customerMaterial;
        private bool isConfigured;

        public int VisibleAnchorCount => ownedMarkers.Count;

        public void Configure(
            Transform debugRoot,
            DecorationGridSpace gridSpace,
            Material employeeMaterial,
            Material customerMaterial)
        {
            this.debugRoot = debugRoot ?? throw new ArgumentNullException(nameof(debugRoot));
            this.gridSpace = gridSpace;
            this.employeeMaterial = employeeMaterial ??
                throw new ArgumentNullException(nameof(employeeMaterial));
            this.customerMaterial = customerMaterial ??
                throw new ArgumentNullException(nameof(customerMaterial));
            isConfigured = true;
            Clear();
        }

        public void Rebuild(LayoutReadinessReport report, bool debugVisible)
        {
            EnsureConfigured();
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            Clear();
            if (!debugVisible)
            {
                return;
            }

            foreach (var station in report.Stations)
            {
                foreach (var anchor in station.Anchors.Anchors)
                {
                    var marker = new GameObject(
                        $"AnchorDebug_{anchor.Role}_{station.InstanceId}");
                    marker.transform.SetParent(debugRoot, false);
                    marker.transform.localPosition =
                        gridSpace.GetCellCenterLocal(anchor.Position, 0.08f);
                    marker.transform.localRotation = FacingRotation(anchor.Facing);
                    marker.transform.localScale = new Vector3(0.18f, 0.05f, 0.34f);
                    marker.AddComponent<MeshFilter>().sharedMesh =
                        Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                    marker.AddComponent<MeshRenderer>().sharedMaterial =
                        anchor.Role == InteractionRole.Employee
                            ? employeeMaterial
                            : customerMaterial;
                    ownedMarkers.Add(marker);
                }
            }
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnDestroy()
        {
            Clear();
        }

        private void EnsureConfigured()
        {
            if (!isConfigured ||
                debugRoot == null ||
                employeeMaterial == null ||
                customerMaterial == null)
            {
                throw new InvalidOperationException(
                    "InteractionAnchorDebugView must be configured before use.");
            }
        }

        private void Clear()
        {
            foreach (var marker in ownedMarkers)
            {
                if (marker == null)
                {
                    continue;
                }

                marker.SetActive(false);
                Destroy(marker);
            }

            ownedMarkers.Clear();
        }

        private static Quaternion FacingRotation(CardinalDirection facing)
        {
            switch (facing)
            {
                case CardinalDirection.North:
                    return Quaternion.identity;
                case CardinalDirection.East:
                    return Quaternion.Euler(0f, 90f, 0f);
                case CardinalDirection.South:
                    return Quaternion.Euler(0f, 180f, 0f);
                case CardinalDirection.West:
                    return Quaternion.Euler(0f, 270f, 0f);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(facing),
                        facing,
                        "Anchor facing must be cardinal.");
            }
        }
    }
}

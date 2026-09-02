using System;
using System.Collections.Generic;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    /// <summary>Renders a non-interactive wall-slot footprint projection for active Preview.</summary>
    public sealed class WallMountedPreviewView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly List<Vector3> CheckIconVertices = new List<Vector3>
        {
            new Vector3(-0.45f, -0.05f), new Vector3(-0.35f, -0.15f),
            new Vector3(-0.05f, -0.45f), new Vector3(0.45f, 0.35f),
            new Vector3(0.35f, 0.45f), new Vector3(-0.05f, -0.25f)
        };
        private static readonly int[] CheckIconTriangles =
        {
            0, 1, 2, 2, 3, 4, 2, 4, 5,
            2, 1, 0, 4, 3, 2, 5, 4, 2
        };
        private static readonly List<Vector3> CrossIconVertices = new List<Vector3>
        {
            new Vector3(-0.45f, -0.35f), new Vector3(-0.35f, -0.45f),
            new Vector3(0f, -0.1f), new Vector3(0.35f, -0.45f),
            new Vector3(0.45f, -0.35f), new Vector3(0.1f, 0f),
            new Vector3(0.45f, 0.35f), new Vector3(0.35f, 0.45f),
            new Vector3(0f, 0.1f), new Vector3(-0.35f, 0.45f),
            new Vector3(-0.45f, 0.35f), new Vector3(-0.1f, 0f)
        };
        private static readonly int[] CrossIconTriangles =
        {
            0, 1, 2, 2, 3, 4, 2, 4, 5,
            6, 7, 8, 8, 9, 10, 8, 10, 11,
            2, 1, 0, 4, 3, 2, 5, 4, 2,
            8, 7, 6, 10, 9, 8, 11, 10, 8
        };
        private static readonly List<Vector2> FootprintUvs = new List<Vector2>
        {
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f)
        };
        private static readonly int[] FootprintTriangles = { 0, 2, 1, 2, 3, 1 };

        private readonly List<Vector3> footprintVertices = new List<Vector3>
        {
            Vector3.zero,
            Vector3.zero,
            Vector3.zero,
            Vector3.zero
        };
        private MaterialPropertyBlock propertyBlock;
        private Transform projectionRoot;
        private Material validMaterial;
        private Material invalidMaterial;
        private Mesh currentIconMesh;
        private Mesh currentProjectionMesh;
        private MeshRenderer projectionRenderer;
        private MeshRenderer iconRenderer;
        private GameObject currentPreviewPrefab;
        private bool? currentIconIsValid;
        private float currentFootprintWidth = float.NaN;
        private float currentFootprintHeight = float.NaN;

        public GameObject CurrentProjection { get; private set; }
        public GameObject CurrentGhost { get; private set; }

        public void Configure(Transform projectionRoot, Material validMaterial, Material invalidMaterial)
        {
            this.projectionRoot = projectionRoot ?? throw new ArgumentNullException(nameof(projectionRoot));
            this.validMaterial = validMaterial ?? throw new ArgumentNullException(nameof(validMaterial));
            this.invalidMaterial = invalidMaterial ?? throw new ArgumentNullException(nameof(invalidMaterial));
            propertyBlock = new MaterialPropertyBlock();
            ClearPreview();
        }

        public void ShowWallPreview(
            WallMountedPlacementPreview preview,
            WallSurfaceAuthoring surface,
            bool isValid,
            PlacementFeedbackKey feedback,
            GameObject previewPrefab = null)
        {
            if (preview == null)
            {
                throw new ArgumentNullException(nameof(preview));
            }

            if (surface == null)
            {
                throw new ArgumentNullException(nameof(surface));
            }

            EnsureConfigured();
            if (!string.Equals(preview.SurfaceId, surface.SurfaceId, StringComparison.Ordinal))
            {
                ClearPreview();
                return;
            }

            // Match Furniture Mode with one complete valid/invalid footprint fill.
            EnsureProjectionObjects();
            CurrentProjection.name = isValid
                ? "WallProjection_ValidCheck"
                : "WallProjection_InvalidCross";
            var width = preview.Footprint.Width * surface.SlotSize;
            var height = preview.Footprint.Height * surface.SlotSize;
            var localCenter = new Vector3(
                -surface.Columns * surface.SlotSize * 0.5f +
                (preview.Position.Column + preview.Footprint.Width * 0.5f) * surface.SlotSize,
                (preview.Position.Row + preview.Footprint.Height * 0.5f) * surface.SlotSize,
                0f);
            CurrentProjection.transform.position = surface.GetWallMountedProjectionWorldPosition(
                localCenter,
                WallSurfaceAuthoring.WallMountedPlaneEpsilon);
            CurrentProjection.transform.rotation = Quaternion.LookRotation(
                surface.transform.forward,
                surface.transform.up);
            CurrentProjection.transform.localScale = Vector3.one;
            if (!Mathf.Approximately(currentFootprintWidth, width)
                || !Mathf.Approximately(currentFootprintHeight, height))
            {
                UpdateFootprintFillMesh(currentProjectionMesh, width, height);
                currentFootprintWidth = width;
                currentFootprintHeight = height;
            }
            projectionRenderer.sharedMaterial = isValid ? validMaterial : invalidMaterial;
            UpdateFeedbackIcon(isValid, projectionRenderer.sharedMaterial);
            UpdateGhost(preview, surface, localCenter, previewPrefab);
        }

        public void ClearPreview()
        {
            if (currentIconMesh != null)
            {
                Destroy(currentIconMesh);
                currentIconMesh = null;
            }

            if (currentProjectionMesh != null)
            {
                Destroy(currentProjectionMesh);
                currentProjectionMesh = null;
            }

            if (CurrentProjection != null)
            {
                CurrentProjection.SetActive(false);
                Destroy(CurrentProjection);
                CurrentProjection = null;
            }
            if (CurrentGhost != null)
            {
                CurrentGhost.SetActive(false);
                Destroy(CurrentGhost);
                CurrentGhost = null;
            }
            projectionRenderer = null;
            iconRenderer = null;
            currentPreviewPrefab = null;
            currentIconIsValid = null;
            currentFootprintWidth = float.NaN;
            currentFootprintHeight = float.NaN;
        }

        private void OnDisable()
        {
            ClearPreview();
        }

        private void OnDestroy()
        {
            ClearPreview();
        }

        private void EnsureConfigured()
        {
            if (projectionRoot == null || validMaterial == null || invalidMaterial == null)
            {
                throw new InvalidOperationException("WallMountedPreviewView must be configured first.");
            }
        }

        private void EnsureProjectionObjects()
        {
            if (CurrentProjection != null)
            {
                return;
            }

            CurrentProjection = new GameObject();
            CurrentProjection.transform.SetParent(projectionRoot, true);
            projectionRenderer = CurrentProjection.AddComponent<MeshRenderer>();
            currentProjectionMesh = new Mesh { name = "WallPreviewFootprintFill" };
            CurrentProjection.AddComponent<MeshFilter>().sharedMesh = currentProjectionMesh;

            var icon = new GameObject("ProjectionFeedbackIcon");
            icon.transform.SetParent(CurrentProjection.transform, false);
            // Quad's visible face is local -Z, so its feedback geometry must
            // sit slightly farther toward that same visible side.
            icon.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            icon.transform.localScale = Vector3.one * 0.32f;
            currentIconMesh = new Mesh { name = "WallPreviewFeedbackIcon" };
            icon.AddComponent<MeshFilter>().sharedMesh = currentIconMesh;
            iconRenderer = icon.AddComponent<MeshRenderer>();
        }

        private void UpdateFeedbackIcon(bool isValid, Material material)
        {
            if (currentIconIsValid != isValid)
            {
                UpdateFeedbackIconMesh(currentIconMesh, isValid);
                currentIconIsValid = isValid;
            }
            iconRenderer.sharedMaterial = material;
            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, Color.white);
            propertyBlock.SetColor(ColorId, Color.white);
            iconRenderer.SetPropertyBlock(propertyBlock);
        }

        private void UpdateGhost(
            WallMountedPlacementPreview preview,
            WallSurfaceAuthoring surface,
            Vector3 localCenter,
            GameObject previewPrefab)
        {
            if (previewPrefab == null)
            {
                ClearGhost();
                return;
            }

            if (CurrentGhost == null || currentPreviewPrefab != previewPrefab)
            {
                ClearGhost();
                CurrentGhost = Instantiate(previewPrefab, projectionRoot);
                CurrentGhost.name = previewPrefab.name + "_PreviewGhost";
                currentPreviewPrefab = previewPrefab;
                foreach (var ghostCollider in CurrentGhost.GetComponentsInChildren<Collider>(true))
                {
                    ghostCollider.enabled = false;
                    DestroyImmediate(ghostCollider);
                }
                foreach (var obstacle in CurrentGhost.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
                {
                    obstacle.enabled = false;
                    DestroyImmediate(obstacle);
                }
                foreach (var body in CurrentGhost.GetComponentsInChildren<Rigidbody>(true))
                {
                    body.detectCollisions = false;
                    body.isKinematic = true;
                    DestroyImmediate(body);
                }
            }

            CurrentGhost.transform.localScale = previewPrefab.transform.localScale;
            var wallFacing = surface.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
            var wallContactCenter = surface.GetWallMountedWorldPosition(
                localCenter,
                WallSurfaceAuthoring.WallMountedPlaneEpsilon);
            var bottomPivotPosition = wallContactCenter
                - surface.transform.up * (preview.Footprint.Height * surface.SlotSize * 0.5f);
            CurrentGhost.transform.SetPositionAndRotation(bottomPivotPosition, wallFacing);
        }

        private void ClearGhost()
        {
            if (CurrentGhost != null)
            {
                CurrentGhost.SetActive(false);
                Destroy(CurrentGhost);
                CurrentGhost = null;
            }
            currentPreviewPrefab = null;
        }

        private static void UpdateFeedbackIconMesh(Mesh mesh, bool isValid)
        {
            mesh.Clear();
            mesh.name = isValid ? "WallPreviewCheckMesh" : "WallPreviewCrossMesh";
            if (isValid)
            {
                mesh.SetVertices(CheckIconVertices);
                mesh.SetTriangles(CheckIconTriangles, 0);
            }
            else
            {
                mesh.SetVertices(CrossIconVertices);
                mesh.SetTriangles(CrossIconTriangles, 0);
            }
            mesh.RecalculateBounds();
        }

        private void UpdateFootprintFillMesh(Mesh mesh, float width, float height)
        {
            var halfWidth = width * 0.5f;
            var halfHeight = height * 0.5f;
            mesh.Clear();
            footprintVertices[0] = new Vector3(-halfWidth, -halfHeight);
            footprintVertices[1] = new Vector3(halfWidth, -halfHeight);
            footprintVertices[2] = new Vector3(-halfWidth, halfHeight);
            footprintVertices[3] = new Vector3(halfWidth, halfHeight);
            mesh.SetVertices(footprintVertices);
            mesh.SetUVs(0, FootprintUvs);
            // Keep the same visible local -Z winding as Unity's built-in Quad.
            mesh.SetTriangles(FootprintTriangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}

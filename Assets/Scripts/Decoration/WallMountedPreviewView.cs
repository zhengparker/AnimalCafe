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
        private MaterialPropertyBlock propertyBlock;
        private Transform projectionRoot;
        private Material validMaterial;
        private Material invalidMaterial;
        private Mesh currentIconMesh;
        private Mesh currentProjectionMesh;
        private Material currentIconMaterial;

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
            ClearPreview();
            if (!string.Equals(preview.SurfaceId, surface.SurfaceId, StringComparison.Ordinal))
            {
                return;
            }

            // Match Furniture Mode with one complete valid/invalid footprint fill.
            CurrentProjection = new GameObject();
            var renderer = CurrentProjection.AddComponent<MeshRenderer>();
            CurrentProjection.name = isValid
                ? "WallProjection_ValidCheck"
                : "WallProjection_InvalidCross";
            CurrentProjection.transform.SetParent(projectionRoot, true);
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
            currentProjectionMesh = CreateFootprintFillMesh(width, height);
            CurrentProjection.AddComponent<MeshFilter>().sharedMesh = currentProjectionMesh;
            renderer.sharedMaterial = isValid ? validMaterial : invalidMaterial;
            CreateFeedbackIcon(isValid, renderer.sharedMaterial);
            if (previewPrefab != null)
            {
                CurrentGhost = Instantiate(previewPrefab, projectionRoot);
                CurrentGhost.name = previewPrefab.name + "_PreviewGhost";
                CurrentGhost.transform.localScale = previewPrefab.transform.localScale;
                var wallFacing = surface.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
                var wallContactCenter = surface.GetWallMountedWorldPosition(
                    localCenter,
                    WallSurfaceAuthoring.WallMountedPlaneEpsilon);
                var bottomPivotPosition = wallContactCenter
                    - surface.transform.up * (preview.Footprint.Height * surface.SlotSize * 0.5f);
                CurrentGhost.transform.SetPositionAndRotation(
                    bottomPivotPosition,
                    wallFacing);
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

            if (currentIconMaterial != null)
            {
                Destroy(currentIconMaterial);
                currentIconMaterial = null;
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

        private void CreateFeedbackIcon(bool isValid, Material material)
        {
            var icon = new GameObject("ProjectionFeedbackIcon");
            icon.transform.SetParent(CurrentProjection.transform, false);
            // Quad's visible face is local -Z, so its feedback geometry must
            // sit slightly farther toward that same visible side.
            icon.transform.localPosition = new Vector3(0f, 0f, -0.02f);
            icon.transform.localScale = Vector3.one * 0.32f;
            currentIconMesh = isValid ? CreateCheckMesh() : CreateCrossMesh();
            icon.AddComponent<MeshFilter>().sharedMesh = currentIconMesh;
            currentIconMaterial = new Material(material);
            var renderer = icon.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = currentIconMaterial;
            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, Color.white);
            propertyBlock.SetColor(ColorId, Color.white);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private static Mesh CreateCheckMesh()
        {
            var mesh = new Mesh { name = "WallPreviewCheckMesh" };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-0.45f, -0.05f), new Vector3(-0.35f, -0.15f),
                new Vector3(-0.05f, -0.45f), new Vector3(0.45f, 0.35f),
                new Vector3(0.35f, 0.45f), new Vector3(-0.05f, -0.25f)
            });
            mesh.SetTriangles(new[]
            {
                0, 1, 2, 2, 3, 4, 2, 4, 5,
                2, 1, 0, 4, 3, 2, 5, 4, 2
            }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateFootprintFillMesh(float width, float height)
        {
            var halfWidth = width * 0.5f;
            var halfHeight = height * 0.5f;
            var mesh = new Mesh { name = "WallPreviewFootprintFill" };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-halfWidth, -halfHeight),
                new Vector3(halfWidth, -halfHeight),
                new Vector3(-halfWidth, halfHeight),
                new Vector3(halfWidth, halfHeight)
            });
            mesh.SetUVs(0, new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            });
            // Keep the same visible local -Z winding as Unity's built-in Quad.
            mesh.SetTriangles(new[] { 0, 2, 1, 2, 3, 1 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateCrossMesh()
        {
            var mesh = new Mesh { name = "WallPreviewCrossMesh" };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-0.45f, -0.35f), new Vector3(-0.35f, -0.45f),
                new Vector3(0f, -0.1f), new Vector3(0.35f, -0.45f),
                new Vector3(0.45f, -0.35f), new Vector3(0.1f, 0f),
                new Vector3(0.45f, 0.35f), new Vector3(0.35f, 0.45f),
                new Vector3(0f, 0.1f), new Vector3(-0.35f, 0.45f),
                new Vector3(-0.45f, 0.35f), new Vector3(-0.1f, 0f)
            });
            mesh.SetTriangles(new[]
            {
                0, 1, 2, 2, 3, 4, 2, 4, 5,
                6, 7, 8, 8, 9, 10, 8, 10, 11,
                2, 1, 0, 4, 3, 2, 5, 4, 2,
                8, 7, 6, 10, 9, 8, 11, 10, 8
            }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

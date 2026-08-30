using System;
using System.Collections.Generic;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    /// <summary>Owns render-only Floor tiles; the canonical Floor keeps gameplay authority.</summary>
    public sealed class FloorSurfaceGridView : MonoBehaviour
    {
        private static readonly int QuarterTurnsId = Shader.PropertyToID("_SurfaceRotationQuarterTurns");

        private readonly Dictionary<GridPosition, GameObject> tilesByPosition =
            new Dictionary<GridPosition, GameObject>();
        private readonly Dictionary<GridPosition, int> quarterTurnsByPosition =
            new Dictionary<GridPosition, int>();
        private MaterialPropertyBlock propertyBlock;

        private Transform canonicalFloor;
        private Renderer tileTemplate;
        private float cellSize;
        private DecorationGridSpace gridSpace;
        private float surfaceOffset;
        private SurfaceStyleLookup styleLookup;
        private RoomSurfaceSnapshot confirmedSnapshot;
        private Mesh flatTileMesh;
        private Material selectionFeedbackMaterial;
        private GameObject selectionFeedbackRoot;
        private Mesh selectionOutlineMesh;
        private Mesh previewCheckMesh;

        public int RenderTileCount => tilesByPosition.Count;

        public void Configure(Transform canonicalFloor, Renderer tileTemplate, float cellSizeMeters)
        {
            this.canonicalFloor = canonicalFloor ?? throw new ArgumentNullException(nameof(canonicalFloor));
            this.tileTemplate = tileTemplate ?? throw new ArgumentNullException(nameof(tileTemplate));
            if (cellSizeMeters <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSizeMeters));
            }

            cellSize = cellSizeMeters;
            gridSpace = new DecorationGridSpace(
                new GridSettings(cellSizeMeters),
                new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8)));
            surfaceOffset = 0f;
            styleLookup = null;
            propertyBlock = new MaterialPropertyBlock();
            ClearTiles();
        }

        public void Configure(
            Transform canonicalGridRoot,
            DecorationGridSpace gridSpace,
            Renderer tileTemplate,
            float surfaceOffsetMeters,
            SurfaceStyleLookup styleLookup)
        {
            if (gridSpace.Settings == null)
            {
                throw new ArgumentNullException(nameof(gridSpace));
            }

            if (styleLookup == null)
            {
                throw new ArgumentNullException(nameof(styleLookup));
            }

            Configure(canonicalGridRoot, tileTemplate, gridSpace.Settings.CellSize);
            this.gridSpace = gridSpace;
            surfaceOffset = surfaceOffsetMeters;
            this.styleLookup = styleLookup;
        }

        public void ConfigureSelectionFeedback(Material validFeedbackMaterial)
        {
            selectionFeedbackMaterial = validFeedbackMaterial
                ?? throw new ArgumentNullException(nameof(validFeedbackMaterial));
        }

        public void RenderConfirmed(RoomSurfaceLayout layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            EnsureConfigured();
            confirmedSnapshot = layout.CaptureSnapshot();
            RenderLayout(RoomSurfaceLayout.FromSnapshot(confirmedSnapshot));
        }

        public void RenderPreview(SurfacePreviewTransaction preview)
        {
            if (preview == null)
            {
                throw new ArgumentNullException(nameof(preview));
            }

            EnsureConfigured();
            RenderLayout(RoomSurfaceLayout.FromSnapshot(preview.ProposedSnapshot));
        }

        public void ClearPreview()
        {
            // Scene unload can destroy the authored Floor/template before the
            // controller receives OnDisable. Cleanup is idempotent in that order.
            if (confirmedSnapshot == null || canonicalFloor == null || tileTemplate == null)
            {
                return;
            }

            EnsureConfigured();
            RenderLayout(RoomSurfaceLayout.FromSnapshot(confirmedSnapshot));
        }

        public void RenderSelectionFeedback(
            GridPosition? selected,
            IReadOnlyList<GridPosition> previewed)
        {
            if (previewed == null)
            {
                throw new ArgumentNullException(nameof(previewed));
            }

            if (selectionFeedbackMaterial == null || canonicalFloor == null)
            {
                ClearSelectionFeedback();
                return;
            }

            EnsureSelectionFeedbackRoot();
            HideSelectionFeedbackMarkers();
            if (selected.HasValue)
            {
                CreateFeedbackMarker(
                    $"SelectedOutline_{selected.Value.X}_{selected.Value.Y}",
                    selected.Value,
                    GetOrCreateSelectionOutlineMesh());
            }

            foreach (var position in previewed)
            {
                CreateFeedbackMarker(
                    $"PreviewCheck_{position.X}_{position.Y}",
                    position,
                    GetOrCreatePreviewCheckMesh());
            }
        }

        public void ClearSelectionFeedback()
        {
            if (selectionFeedbackRoot == null)
            {
                return;
            }

            selectionFeedbackRoot.SetActive(false);
            Destroy(selectionFeedbackRoot);
            selectionFeedbackRoot = null;
        }

        private void RenderLayout(RoomSurfaceLayout layout)
        {
            ClearTiles();
            foreach (var tile in layout.FloorTiles.Values)
            {
                var renderTile = CreateFlatRenderTile(tile.Position);

                renderTile.transform.localPosition = gridSpace.GetCellCenterLocal(
                    tile.Position,
                    surfaceOffset);
                renderTile.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                renderTile.transform.localScale = Vector3.one * cellSize;
                var quarterTurns = (int)tile.Rotation;
                quarterTurnsByPosition.Add(tile.Position, quarterTurns);
                foreach (var renderer in renderTile.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetFloat(QuarterTurnsId, quarterTurns);
                    if (styleLookup != null)
                    {
                        var style = styleLookup.GetRequired(tile.StyleId, SurfaceStyleKind.Floor);
                        propertyBlock.SetTexture("_BaseMap", GetTexture(style.Material));
                        propertyBlock.SetColor("_BaseColor", GetColor(style.Material));
                    }
                    renderer.SetPropertyBlock(propertyBlock);
                }

                tilesByPosition.Add(tile.Position, renderTile);
            }
        }

        private GameObject CreateFlatRenderTile(GridPosition position)
        {
            var renderTile = new GameObject($"FloorSurfaceTile_{position.X}_{position.Y}");
            renderTile.layer = tileTemplate.gameObject.layer;
            renderTile.transform.SetParent(canonicalFloor, false);

            var meshFilter = renderTile.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = GetOrCreateFlatTileMesh();
            var meshRenderer = renderTile.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterials = tileTemplate.sharedMaterials;
            meshRenderer.shadowCastingMode = tileTemplate.shadowCastingMode;
            meshRenderer.receiveShadows = tileTemplate.receiveShadows;
            return renderTile;
        }

        private void CreateFeedbackMarker(string name, GridPosition position, Mesh mesh)
        {
            var markerTransform = selectionFeedbackRoot.transform.Find(name);
            var marker = markerTransform == null
                ? new GameObject(name)
                : markerTransform.gameObject;
            marker.transform.SetParent(selectionFeedbackRoot.transform, false);
            marker.SetActive(true);
            marker.transform.localPosition = gridSpace.GetCellCenterLocal(
                position,
                surfaceOffset + 0.015f);
            var filter = marker.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = marker.AddComponent<MeshFilter>();
            }
            filter.sharedMesh = mesh;
            var renderer = marker.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = marker.AddComponent<MeshRenderer>();
            }
            renderer.sharedMaterial = selectionFeedbackMaterial;
        }

        private void EnsureSelectionFeedbackRoot()
        {
            if (selectionFeedbackRoot != null)
            {
                return;
            }

            selectionFeedbackRoot = new GameObject("FloorSelectionFeedback");
            selectionFeedbackRoot.transform.SetParent(canonicalFloor, false);
        }

        private void HideSelectionFeedbackMarkers()
        {
            foreach (Transform marker in selectionFeedbackRoot.transform)
            {
                marker.gameObject.SetActive(false);
            }
        }

        private Mesh GetOrCreateFlatTileMesh()
        {
            if (flatTileMesh != null)
            {
                return flatTileMesh;
            }

            flatTileMesh = new Mesh { name = "FloorSurfaceUnitQuad" };
            flatTileMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            };
            flatTileMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };
            flatTileMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            flatTileMesh.RecalculateNormals();
            flatTileMesh.RecalculateBounds();
            return flatTileMesh;
        }

        private Mesh GetOrCreateSelectionOutlineMesh()
        {
            if (selectionOutlineMesh != null)
            {
                return selectionOutlineMesh;
            }

            selectionOutlineMesh = new Mesh { name = "FloorSelectionOutline" };
            selectionOutlineMesh.vertices = new[]
            {
                new Vector3(-.47f, 0f, -.47f), new Vector3(.47f, 0f, -.47f),
                new Vector3(.47f, 0f, .47f), new Vector3(-.47f, 0f, .47f),
                new Vector3(-.39f, 0f, -.39f), new Vector3(.39f, 0f, -.39f),
                new Vector3(.39f, 0f, .39f), new Vector3(-.39f, 0f, .39f)
            };
            selectionOutlineMesh.triangles = new[]
            {
                0, 4, 1, 1, 4, 5,
                1, 5, 2, 2, 5, 6,
                2, 6, 3, 3, 6, 7,
                3, 7, 0, 0, 7, 4
            };
            selectionOutlineMesh.RecalculateNormals();
            selectionOutlineMesh.RecalculateBounds();
            return selectionOutlineMesh;
        }

        private Mesh GetOrCreatePreviewCheckMesh()
        {
            if (previewCheckMesh != null)
            {
                return previewCheckMesh;
            }

            previewCheckMesh = new Mesh { name = "FloorPreviewCheck" };
            previewCheckMesh.vertices = new[]
            {
                new Vector3(-.22f, 0f, .02f), new Vector3(-.15f, 0f, -.05f),
                new Vector3(-.03f, 0f, .07f), new Vector3(-.10f, 0f, .14f),
                new Vector3(-.03f, 0f, .07f), new Vector3(.20f, 0f, -.16f),
                new Vector3(.27f, 0f, -.09f), new Vector3(.04f, 0f, .14f)
            };
            previewCheckMesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 4, 6, 7 };
            previewCheckMesh.RecalculateNormals();
            previewCheckMesh.RecalculateBounds();
            return previewCheckMesh;
        }

        public int GetQuarterTurns(GridPosition position)
        {
            if (!quarterTurnsByPosition.TryGetValue(position, out var quarterTurns))
            {
                throw new ArgumentException("No render tile exists at this position.", nameof(position));
            }

            return quarterTurns;
        }

        private void OnDisable()
        {
            ClearSelectionFeedback();
            ClearTiles();
        }

        private void OnDestroy()
        {
            if (flatTileMesh != null)
            {
                Destroy(flatTileMesh);
                flatTileMesh = null;
            }
            if (selectionOutlineMesh != null)
            {
                Destroy(selectionOutlineMesh);
                selectionOutlineMesh = null;
            }
            if (previewCheckMesh != null)
            {
                Destroy(previewCheckMesh);
                previewCheckMesh = null;
            }
        }

        private void ClearTiles()
        {
            foreach (var tile in tilesByPosition.Values)
            {
                if (tile != null)
                {
                    tile.SetActive(false);
                    Destroy(tile);
                }
            }

            tilesByPosition.Clear();
            quarterTurnsByPosition.Clear();
        }

        private void EnsureConfigured()
        {
            if (canonicalFloor == null || tileTemplate == null || cellSize <= 0f)
            {
                throw new InvalidOperationException("FloorSurfaceGridView must be configured before rendering.");
            }
        }

        private static Texture GetTexture(Material material)
        {
            return material.HasProperty("_BaseMap")
                ? material.GetTexture("_BaseMap")
                : material.GetTexture("_MainTex");
        }

        private static Color GetColor(Material material)
        {
            return material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material.color;
        }
    }
}

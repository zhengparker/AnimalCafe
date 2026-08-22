using System;
using System.Collections.Generic;
using AnimalCafe.Layout;
using AnimalCafe.UI.Foundation;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    /// <summary>
    /// Runtime-only pooled world Grid and active-footprint presentation.
    /// Runtime-only çš„ Grid / footprint pooled visualã€‚
    /// </summary>
    public sealed class GridHighlightView : MonoBehaviour
    {
        public const float FootprintHeight = 0.025f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly List<GameObject> baseCells = new List<GameObject>();
        private readonly List<FootprintCellVisual> footprintCells =
            new List<FootprintCellVisual>();

        private Transform visualRoot;
        private DecorationGridSpace gridSpace;
        private Material materialTemplate;
        private AnimalCafeUiTheme theme;
        private MaterialPropertyBlock propertyBlock;
        private Mesh quadMesh;
        private bool isConfigured;

        public void Configure(
            Transform root,
            DecorationGridSpace gridSpace,
            Material materialTemplate,
            AnimalCafeUiTheme theme)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (materialTemplate == null)
            {
                throw new ArgumentNullException(nameof(materialTemplate));
            }

            if (theme == null)
            {
                throw new ArgumentNullException(nameof(theme));
            }

            ReleaseOwnedVisuals();
            visualRoot = root;
            this.gridSpace = gridSpace;
            this.materialTemplate = materialTemplate;
            this.theme = theme;
            propertyBlock = new MaterialPropertyBlock();
            quadMesh = CreateQuadMesh();
            isConfigured = true;
        }

        public void ShowGrid(GridSettings settings)
        {
            EnsureConfigured();
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!Mathf.Approximately(settings.CellSize, gridSpace.Settings.CellSize))
            {
                throw new ArgumentException(
                    "Grid settings must match the configured DecorationGridSpace.",
                    nameof(settings));
            }

            EnsureBasePool();
            foreach (var cell in baseCells)
            {
                cell.SetActive(true);
            }
        }

        public void ShowFootprint(IReadOnlyList<GridPosition> cells, bool valid)
        {
            EnsureConfigured();
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (cells.Count == 0)
            {
                throw new ArgumentException(
                    "Footprint must contain at least one Grid cell.",
                    nameof(cells));
            }

            EnsureFootprintPool(cells.Count);
            var color = valid ? theme.Colors.Accent : theme.Colors.Destructive;
            var markColor = theme.Colors.Surface;
            markColor.a = 1f;

            for (var index = 0; index < footprintCells.Count; index++)
            {
                var visual = footprintCells[index];
                var active = index < cells.Count;
                visual.Root.SetActive(active);
                if (!active)
                {
                    continue;
                }

                visual.Root.transform.localPosition =
                    gridSpace.GetCellCenterLocal(cells[index], FootprintHeight);
                ApplyColor(visual.Fill, color);
                ApplyColor(visual.ValidDiamond, markColor);
                ApplyColor(visual.InvalidBarA, markColor);
                ApplyColor(visual.InvalidBarB, markColor);
                visual.ValidDiamond.gameObject.SetActive(valid);
                visual.InvalidBarA.gameObject.SetActive(!valid);
                visual.InvalidBarB.gameObject.SetActive(!valid);
            }
        }

        public void ClearFootprint()
        {
            foreach (var visual in footprintCells)
            {
                visual.Root.SetActive(false);
            }
        }

        public void HideGrid()
        {
            foreach (var cell in baseCells)
            {
                cell.SetActive(false);
            }

            ClearFootprint();
        }

        private void OnDisable()
        {
            HideGrid();
        }

        private void OnDestroy()
        {
            ReleaseOwnedVisuals();
        }

        private void EnsureBasePool()
        {
            if (baseCells.Count > 0)
            {
                return;
            }

            var bounds = gridSpace.Bounds;
            var index = 0;
            for (var x = 0; x < bounds.Size.Width; x++)
            {
                for (var y = 0; y < bounds.Size.Height; y++)
                {
                    var cell = new GridPosition(
                        checked(bounds.Origin.X + x),
                        checked(bounds.Origin.Y + y));
                    var visual = CreateMeshObject(
                        $"BaseCell_{index:00}",
                        visualRoot,
                        new Vector3(
                            gridSpace.Settings.CellSize * 0.94f,
                            1f,
                            gridSpace.Settings.CellSize * 0.94f));
                    visual.transform.localPosition = gridSpace.GetCellCenterLocal(cell);
                    ApplyColor(
                        visual.GetComponent<Renderer>(),
                        theme.Colors.Surface);
                    baseCells.Add(visual);
                    index++;
                }
            }
        }

        private void EnsureFootprintPool(int requiredCount)
        {
            while (footprintCells.Count < requiredCount)
            {
                footprintCells.Add(CreateFootprintCell(footprintCells.Count));
            }
        }

        private FootprintCellVisual CreateFootprintCell(int index)
        {
            var root = new GameObject($"FootprintCell_{index:00}");
            root.transform.SetParent(visualRoot, false);

            var fill = CreateMeshObject(
                "Fill",
                root.transform,
                new Vector3(
                    gridSpace.Settings.CellSize * 0.88f,
                    1f,
                    gridSpace.Settings.CellSize * 0.88f));
            fill.transform.localPosition = Vector3.zero;

            var geometryMark = new GameObject("GeometryMark");
            geometryMark.transform.SetParent(root.transform, false);
            geometryMark.transform.localPosition = new Vector3(0f, 0.008f, 0f);

            var validDiamond = CreateMeshObject(
                "ValidDiamond",
                geometryMark.transform,
                new Vector3(
                    gridSpace.Settings.CellSize * 0.28f,
                    1f,
                    gridSpace.Settings.CellSize * 0.28f));
            validDiamond.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

            var invalidBarA = CreateMeshObject(
                "InvalidBarA",
                geometryMark.transform,
                new Vector3(
                    gridSpace.Settings.CellSize * 0.64f,
                    1f,
                    gridSpace.Settings.CellSize * 0.11f));
            invalidBarA.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);

            var invalidBarB = CreateMeshObject(
                "InvalidBarB",
                geometryMark.transform,
                new Vector3(
                    gridSpace.Settings.CellSize * 0.64f,
                    1f,
                    gridSpace.Settings.CellSize * 0.11f));
            invalidBarB.transform.localRotation = Quaternion.Euler(0f, -45f, 0f);

            root.SetActive(false);
            return new FootprintCellVisual(
                root,
                fill.GetComponent<Renderer>(),
                validDiamond.GetComponent<Renderer>(),
                invalidBarA.GetComponent<Renderer>(),
                invalidBarB.GetComponent<Renderer>());
        }

        private GameObject CreateMeshObject(
            string objectName,
            Transform parent,
            Vector3 localScale)
        {
            var visual = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = localScale;
            visual.GetComponent<MeshFilter>().sharedMesh = quadMesh;
            visual.GetComponent<MeshRenderer>().sharedMaterial = materialTemplate;
            return visual;
        }

        private void ApplyColor(Renderer renderer, Color color)
        {
            propertyBlock.Clear();
            renderer.GetPropertyBlock(propertyBlock);
            if (materialTemplate.HasProperty(BaseColorId))
            {
                propertyBlock.SetColor(BaseColorId, color);
            }

            if (materialTemplate.HasProperty(ColorId))
            {
                propertyBlock.SetColor(ColorId, color);
            }

            renderer.SetPropertyBlock(propertyBlock);
        }

        private void EnsureConfigured()
        {
            if (!isConfigured || visualRoot == null ||
                materialTemplate == null || theme == null)
            {
                throw new InvalidOperationException(
                    "GridHighlightView must be configured before use.");
            }
        }

        private void ReleaseOwnedVisuals()
        {
            foreach (var cell in baseCells)
            {
                DestroyOwned(cell);
            }
            baseCells.Clear();

            foreach (var visual in footprintCells)
            {
                DestroyOwned(visual.Root);
            }
            footprintCells.Clear();

            if (quadMesh != null)
            {
                UnityEngine.Object.Destroy(quadMesh);
                quadMesh = null;
            }
        }

        private static void DestroyOwned(GameObject ownedObject)
        {
            if (ownedObject != null)
            {
                ownedObject.SetActive(false);
                UnityEngine.Object.Destroy(ownedObject);
            }
        }

        private static Mesh CreateQuadMesh()
        {
            var mesh = new Mesh
            {
                name = "DecorationGridQuad"
            };
            mesh.SetVertices(new List<Vector3>
            {
                new Vector3(-0.5f, 0f, -0.5f),
                new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, -0.5f)
            });
            mesh.SetNormals(new List<Vector3>
            {
                Vector3.up,
                Vector3.up,
                Vector3.up,
                Vector3.up
            });
            mesh.SetUVs(0, new List<Vector2>
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private sealed class FootprintCellVisual
        {
            public FootprintCellVisual(
                GameObject root,
                Renderer fill,
                Renderer validDiamond,
                Renderer invalidBarA,
                Renderer invalidBarB)
            {
                Root = root;
                Fill = fill;
                ValidDiamond = validDiamond;
                InvalidBarA = invalidBarA;
                InvalidBarB = invalidBarB;
            }

            public GameObject Root { get; }
            public Renderer Fill { get; }
            public Renderer ValidDiamond { get; }
            public Renderer InvalidBarA { get; }
            public Renderer InvalidBarB { get; }
        }
    }
}

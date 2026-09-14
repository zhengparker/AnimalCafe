using System;
using System.Collections.Generic;
using AnimalCafe.Interaction;
using AnimalCafe.Layout;
using AnimalCafe.UI.Foundation;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    /// <summary>
    /// Owns the single suspended furniture clone used by an active Preview.
    /// Active Preview æ‚¬ç©ºæ¨¡åž‹çš„å•ä¸€ Scene ownerã€‚
    /// </summary>
    public sealed class FurniturePreviewView : MonoBehaviour
    {
        private readonly List<Renderer> previewRenderers = new List<Renderer>();

        private Transform previewRoot;
        private DecorationGridSpace gridSpace;
        private AnimalCafeUiTheme theme;
        private GameObject previewObject;
        private bool isConfigured;

        public Transform CurrentPreviewTransform => previewObject != null ? previewObject.transform : null;

        public void Configure(
            Transform root,
            DecorationGridSpace gridSpace,
            AnimalCafeUiTheme theme)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (theme == null)
            {
                throw new ArgumentNullException(nameof(theme));
            }

            Hide();
            previewRoot = root;
            this.gridSpace = gridSpace;
            this.theme = theme;
            isConfigured = true;
        }

        public void Show(GameObject prefab, IReadOnlyList<GridPosition> cells)
        {
            EnsureConfigured();
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            ValidateCells(cells);
            Hide();

            previewObject = Instantiate(prefab, previewRoot, false);
            previewObject.name = "FurniturePreview_" + prefab.name;
            previewObject.SetActive(true);

            foreach (var collider in previewObject.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (var behaviour in previewObject.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is ISelectable)
                {
                    behaviour.enabled = false;
                }
            }

            previewRenderers.AddRange(
                previewObject.GetComponentsInChildren<Renderer>(true));
            SetPlacement(cells, FurnitureRotation.Degrees0, 0f);
            SetValidity(true);
        }

        public void SetPlacement(
            IReadOnlyList<GridPosition> currentCells,
            FurnitureRotation rotation,
            float hoverHeight)
        {
            EnsurePreviewVisible();
            ValidateCells(currentCells);
            previewObject.transform.localPosition =
                gridSpace.GetFootprintCenterLocal(currentCells, hoverHeight);
            previewObject.transform.localRotation =
                gridSpace.GetLocalRotation(rotation);
        }

        public void SetValidity(bool valid)
        {
            EnsurePreviewVisible();
            // Validity belongs to the footprint and action UI; preserve model appearance.
            // 红绿提示由 footprint 和操作 UI 负责，不覆盖模型材质或已有属性。
        }

        public bool TryGetWorldBounds(out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;
            foreach (var renderer in previewRenderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        public void Hide()
        {
            if (previewObject != null)
            {
                previewObject.SetActive(false);
                UnityEngine.Object.Destroy(previewObject);
                previewObject = null;
            }

            previewRenderers.Clear();
        }

        private void OnDisable()
        {
            Hide();
        }

        private void OnDestroy()
        {
            Hide();
        }

        private void EnsureConfigured()
        {
            if (!isConfigured || previewRoot == null || theme == null)
            {
                throw new InvalidOperationException(
                    "FurniturePreviewView must be configured before use.");
            }
        }

        private void EnsurePreviewVisible()
        {
            EnsureConfigured();
            if (previewObject == null)
            {
                throw new InvalidOperationException(
                    "FurniturePreviewView must Show a Prefab before updating it.");
            }
        }

        private static void ValidateCells(IReadOnlyList<GridPosition> cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            if (cells.Count == 0)
            {
                throw new ArgumentException(
                    "Preview footprint must contain at least one cell.",
                    nameof(cells));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using AnimalCafe.Layout;
using AnimalCafe.UI.Foundation;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    /// <summary>Owns confirmed and Preview Pick-up indicator representations.</summary>
    public sealed class PickUpPointIndicatorView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly Dictionary<string, GameObject> confirmedById =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<string, PickUpPointInstance> bindingsById =
            new Dictionary<string, PickUpPointInstance>(StringComparer.Ordinal);

        private FurnitureSceneRegistry furnitureRegistry;
        private Transform indicatorRoot;
        private GameObject indicatorPrefab;
        private AnimalCafeUiTheme theme;
        private MaterialPropertyBlock propertyBlock;
        private bool decorationModeVisible;

        public GameObject CurrentPreview { get; private set; }

        public void Configure(
            FurnitureSceneRegistry furnitureRegistry,
            Transform indicatorRoot,
            GameObject indicatorPrefab,
            AnimalCafeUiTheme theme)
        {
            this.furnitureRegistry = furnitureRegistry ??
                throw new ArgumentNullException(nameof(furnitureRegistry));
            this.indicatorRoot = indicatorRoot ??
                throw new ArgumentNullException(nameof(indicatorRoot));
            this.indicatorPrefab = indicatorPrefab ??
                throw new ArgumentNullException(nameof(indicatorPrefab));
            this.theme = theme ?? throw new ArgumentNullException(nameof(theme));
            propertyBlock = new MaterialPropertyBlock();
            decorationModeVisible = false;
            ClearAll();
        }

        public void Rebuild(
            IReadOnlyList<PickUpPointInstance> points,
            bool decorationModeVisible)
        {
            EnsureConfigured();
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            for (var index = 0; index < points.Count; index++)
            {
                if (points[index] == null)
                {
                    throw new ArgumentException(
                        $"Pick-up Point at index {index} must not be null.",
                        nameof(points));
                }
            }

            this.decorationModeVisible = decorationModeVisible;
            ClearAll();
            if (!decorationModeVisible)
            {
                return;
            }

            var emittedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var point in points)
            {
                if (!emittedIds.Add(point.InstanceId) ||
                    !FunctionalSurfaceViewPositioning.TryResolveSlot(
                        point.Address,
                        furnitureRegistry,
                        out var support,
                        out var slot))
                {
                    continue;
                }

                var representation = CreateIndicator(
                    "PickUpPoint_" + point.InstanceId,
                    new Pose(slot.position, support.rotation),
                    theme.Colors.Accent);
                confirmedById.Add(point.InstanceId, representation);
                bindingsById.Add(point.InstanceId, point);
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
                var point = pair.Value;
                if (!string.Equals(
                    point.Address.SupportFurnitureInstanceId,
                    supportFurnitureInstanceId,
                    StringComparison.Ordinal)
                    || !confirmedById.TryGetValue(pair.Key, out var representation)
                    || representation == null)
                {
                    continue;
                }

                Transform slot = null;
                var resolved = decorationModeVisible
                    && FunctionalSurfaceViewPositioning.TryResolveSlot(
                        point.Address,
                        previewSupport,
                        out slot);
                representation.SetActive(resolved);
                if (resolved)
                {
                    FunctionalSurfaceViewPositioning.Apply(
                        representation.transform,
                        previewSupport,
                        slot,
                        FurnitureRotation.Degrees0);
                }
            }
        }

        public void ShowPreview(
            FunctionalSurfacePlacementPreview preview,
            Vector3 hoverOffset = default,
            Pose? floorPose = null)
        {
            EnsureConfigured();
            if (preview == null)
            {
                throw new ArgumentNullException(nameof(preview));
            }

            if (preview.Kind != FunctionalSurfacePreviewKind.PickUpPoint)
            {
                throw new ArgumentException(
                    "PickUpPointIndicatorView requires a Pick-up Point Preview.",
                    nameof(preview));
            }

            HidePreview();
            if (!decorationModeVisible)
            {
                return;
            }

            var hasSlot = FunctionalSurfaceViewPositioning.TryResolveSlot(
                preview.Address, furnitureRegistry, out var support, out var slot);
            if (!hasSlot && !floorPose.HasValue)
            {
                return;
            }

            // Floor fallback is visual only; Confirm still requires a valid Slot.
            // 地面位置只用于显示，不会绕过 domain 的 Slot 验证。
            var placement = hasSlot
                ? new Pose(slot.position, support.rotation)
                : floorPose.Value;
            CurrentPreview = CreateIndicator(
                "PickUpPointPreview",
                placement,
                preview.CanConfirm
                    ? theme.Colors.Accent
                    : theme.Colors.Destructive);

            // Lift only the authored model; the footprint stays on the target surface.
            // 只悬浮形体，Footprint 保持贴在桌面或地面。
            var model = CurrentPreview.transform.Find("InvertedSquarePyramid");
            if (model != null)
            {
                model.position += hoverOffset;
            }
        }

        public void HidePreview()
        {
            if (CurrentPreview == null)
            {
                return;
            }

            CurrentPreview.SetActive(false);
            Destroy(CurrentPreview);
            CurrentPreview = null;
        }

        public bool TryGet(string instanceId, out GameObject representation)
        {
            if (instanceId != null &&
                confirmedById.TryGetValue(instanceId, out representation) &&
                representation != null)
            {
                return true;
            }

            representation = null;
            return false;
        }

        private GameObject CreateIndicator(
            string displayName,
            Pose placement,
            Color color)
        {
            var indicator = Instantiate(indicatorPrefab, indicatorRoot, false);
            indicator.name = displayName;
            indicator.transform.localScale = indicatorPrefab.transform.localScale;
            indicator.transform.SetPositionAndRotation(
                placement.position, placement.rotation);
            foreach (var collider in indicator.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (var renderer in indicator.GetComponentsInChildren<Renderer>(true))
            {
                // Keep the model's neutral material; validity belongs only to Footprint.
                // 形体保留固定灰白材质，只有 Footprint 显示有效/无效状态色。
                if (!string.Equals(renderer.gameObject.name, "Footprint", StringComparison.Ordinal))
                {
                    continue;
                }

                propertyBlock.Clear();
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetColor(ColorId, color);
                renderer.SetPropertyBlock(propertyBlock);
            }

            indicator.SetActive(true);
            return indicator;
        }

        private void OnDisable()
        {
            decorationModeVisible = false;
            ClearAll();
        }

        private void OnDestroy()
        {
            ClearAll();
        }

        private void EnsureConfigured()
        {
            if (furnitureRegistry == null ||
                indicatorRoot == null ||
                indicatorPrefab == null ||
                theme == null ||
                propertyBlock == null)
            {
                throw new InvalidOperationException(
                    "PickUpPointIndicatorView must be configured before use.");
            }
        }

        private void ClearAll()
        {
            HidePreview();
            foreach (var representation in confirmedById.Values)
            {
                if (representation == null)
                {
                    continue;
                }

                representation.SetActive(false);
                Destroy(representation);
            }

            confirmedById.Clear();
            bindingsById.Clear();
        }
    }
}

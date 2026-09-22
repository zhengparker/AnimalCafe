using System;
using AnimalCafe.Content;
using AnimalCafe.Interaction;
using AnimalCafe.Layout;
using AnimalCafe.UI.Foundation;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    /// <summary>Renders a candidate-only mounted equipment ghost and one Slot footprint.</summary>
    public sealed class SurfaceMountedPreviewView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private FurnitureContentCatalog contentCatalog;
        private FurnitureSceneRegistry furnitureRegistry;
        private Transform previewRoot;
        private GameObject footprintPrefab;
        private AnimalCafeUiTheme theme;
        private MaterialPropertyBlock propertyBlock;

        public GameObject CurrentGhost { get; private set; }
        public GameObject CurrentFootprint { get; private set; }

        public void Configure(
            FurnitureContentCatalog contentCatalog,
            FurnitureSceneRegistry furnitureRegistry,
            Transform previewRoot,
            GameObject footprintPrefab,
            AnimalCafeUiTheme theme)
        {
            this.contentCatalog = contentCatalog ??
                throw new ArgumentNullException(nameof(contentCatalog));
            this.furnitureRegistry = furnitureRegistry ??
                throw new ArgumentNullException(nameof(furnitureRegistry));
            this.previewRoot = previewRoot ?? throw new ArgumentNullException(nameof(previewRoot));
            this.footprintPrefab = footprintPrefab ??
                throw new ArgumentNullException(nameof(footprintPrefab));
            this.theme = theme ?? throw new ArgumentNullException(nameof(theme));
            propertyBlock = new MaterialPropertyBlock();
            Hide();
        }

        public void Show(
            FunctionalSurfacePlacementPreview preview,
            Vector3 hoverOffset = default,
            Pose? floorPose = null)
        {
            EnsureConfigured();
            if (preview == null)
            {
                throw new ArgumentNullException(nameof(preview));
            }

            if (preview.Kind != FunctionalSurfacePreviewKind.MountedEquipment)
            {
                throw new ArgumentException(
                    "SurfaceMountedPreviewView requires a mounted equipment Preview.",
                    nameof(preview));
            }

            Hide();
            if (!contentCatalog.TryGetDefinitionAsset(
                    preview.DefinitionId,
                    out var definition) ||
                definition.Prefab == null)
            {
                return;
            }

            var hasSlot = FunctionalSurfaceViewPositioning.TryResolveSlot(
                preview.Address, furnitureRegistry, out var support, out var slot);
            if (!hasSlot && !floorPose.HasValue)
            {
                return;
            }

            // Floor fallback is visual only; the candidate still needs a valid Slot to Confirm.
            // 地面位置只用于显示，不能替代 domain 的 Slot 验证。
            var placement = hasSlot
                ? new Pose(slot.position, support.rotation)
                : floorPose.Value;
            CurrentFootprint = Instantiate(footprintPrefab, previewRoot, false);
            CurrentFootprint.name = "FunctionalSurfacePreviewFootprint";
            CurrentFootprint.transform.localScale = footprintPrefab.transform.localScale;
            CurrentFootprint.transform.SetPositionAndRotation(
                placement.position, placement.rotation);
            CurrentFootprint.SetActive(true);

            CurrentGhost = Instantiate(definition.Prefab, previewRoot, false);
            CurrentGhost.name = "FunctionalSurfacePreviewGhost";
            CurrentGhost.transform.localScale = definition.Prefab.transform.localScale;
            CurrentGhost.transform.SetPositionAndRotation(
                placement.position + hoverOffset,
                placement.rotation * FunctionalSurfaceViewPositioning.ToQuaternion(preview.Rotation));
            PreparePreviewObject(CurrentGhost);
            CurrentGhost.SetActive(true);

            var color = preview.CanConfirm
                ? theme.Colors.Accent
                : theme.Colors.Destructive;
            SetColor(CurrentFootprint, color);
            // Keep CR/CM materials intact; only the footprint carries validity color.
            // 设备本体保持原色，仅 footprint 显示有效/无效颜色。
        }

        public void Hide()
        {
            DestroyOwned(CurrentGhost);
            DestroyOwned(CurrentFootprint);
            CurrentGhost = null;
            CurrentFootprint = null;
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
            if (contentCatalog == null ||
                furnitureRegistry == null ||
                previewRoot == null ||
                footprintPrefab == null ||
                theme == null ||
                propertyBlock == null)
            {
                throw new InvalidOperationException(
                    "SurfaceMountedPreviewView must be configured before use.");
            }
        }

        private static void PreparePreviewObject(GameObject previewObject)
        {
            foreach (var collider in previewObject.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            foreach (var body in previewObject.GetComponentsInChildren<Rigidbody>(true))
            {
                body.detectCollisions = false;
                body.isKinematic = true;
            }

            foreach (var obstacle in previewObject
                .GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true))
            {
                obstacle.enabled = false;
            }

            foreach (var behaviour in previewObject.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is ISelectable)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private void SetColor(GameObject target, Color color)
        {
            foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                propertyBlock.Clear();
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, color);
                propertyBlock.SetColor(ColorId, color);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static void DestroyOwned(GameObject ownedObject)
        {
            if (ownedObject == null)
            {
                return;
            }

            ownedObject.SetActive(false);
            Destroy(ownedObject);
        }
    }
}

using System;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.UI.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.Decoration
{
    /// <summary>
    /// Displays one catalogue entry and owns only its own click listener.
    /// 显示一个 catalogue 条目，并且只管理自己的 click listener。
    /// </summary>
    public sealed class DecorationCatalogueTileView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image thumbnailImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text footprintLabel;
        [SerializeField] private TMP_Text warningLabel;
        [SerializeField] private GameObject warningShape;
        [SerializeField] private GameObject usingCheck;
        [SerializeField] private GameObject previewOutline;
        [SerializeField] private GameObject noneIcon;

        private Action<FurnitureDefinitionAsset> selected;

        public FurnitureDefinitionAsset Definition { get; private set; }
        public string ItemId { get; private set; }

        /// <summary>
        /// Supplies the view references for an isolated runtime fallback tile.
        /// Prefabs continue to use their serialized references.
        /// 为独立运行时 fallback tile 提供 view 引用；Prefab 仍使用 serialized 引用。
        /// </summary>
        public void ConfigureRuntimeViews(Button targetButton, Image targetThumbnail,
            TMP_Text targetNameLabel, GameObject targetUsingCheck,
            GameObject targetPreviewOutline, GameObject targetNoneIcon)
        {
            ClearBinding();
            button = targetButton;
            thumbnailImage = targetThumbnail;
            nameLabel = targetNameLabel;
            usingCheck = targetUsingCheck;
            previewOutline = targetPreviewOutline;
            noneIcon = targetNoneIcon;
        }

        public void Bind(DecorationCatalogueItemModel item, Action<DecorationCatalogueItemModel> onSelected)
        {
            ClearBinding();
            ItemId = item?.ItemId; boundItem = item;
            Definition = item?.FurnitureDefinition;
            var surface = item != null && (item.Kind == DecorationCatalogueItemKind.Floor || item.Kind == DecorationCatalogueItemKind.WallSurface);
            if (nameLabel != null) { nameLabel.gameObject.SetActive(!surface); nameLabel.text = surface || item == null ? string.Empty : item.DisplayName; }
            if (footprintLabel != null) footprintLabel.gameObject.SetActive(false);
            if (thumbnailImage != null)
            {
                thumbnailImage.sprite = item?.Thumbnail;
                thumbnailImage.enabled = item?.Thumbnail != null;
                var thumbnailRect = thumbnailImage.rectTransform;
                thumbnailRect.anchorMin = new Vector2(thumbnailRect.anchorMin.x, surface ? 0f : .22f);
                var minimum = thumbnailRect.offsetMin;
                minimum.y = surface ? 6f : 4f;
                thumbnailRect.offsetMin = minimum;
            }
            usingCheck?.SetActive(false); previewOutline?.SetActive(false); noneIcon?.SetActive(item != null && item.IsNoneOption);
            if (button != null) { button.interactable = item != null; button.onClick.RemoveListener(HandleModelClick); selectedModel = onSelected; button.onClick.AddListener(HandleModelClick); }
        }
        public void SetSurfaceState(bool isUsing, bool isPreview)
        {
            // UnityEngine.Object can retain a managed wrapper after its native object
            // is missing/destroyed; ?. only checks CLR null and still throws there.
            if (usingCheck) usingCheck.SetActive(isUsing);
            if (previewOutline) previewOutline.SetActive(isPreview);
        }
        private Action<DecorationCatalogueItemModel> selectedModel;
        private DecorationCatalogueItemModel boundItem;
        private void HandleModelClick() { if (boundItem != null) selectedModel?.Invoke(boundItem); }

        public bool IsInteractable => isActiveAndEnabled
            && gameObject.activeInHierarchy
            && button != null
            && button.isActiveAndEnabled
            && button.interactable
            && (Definition != null || boundItem != null);

        public void Configure(IUiPointerOwnershipRegistrar pointerBoundary)
        {
            if (pointerBoundary == null)
            {
                throw new ArgumentNullException(nameof(pointerBoundary));
            }

            EnsureOwnListener();
            var hooks = GetComponentsInChildren<DecorationPointerBoundaryEventHook>(true);
            foreach (var hook in hooks)
            {
                hook.Configure(pointerBoundary);
            }
        }

        public void Bind(
            DecorationCatalogueEntry entry,
            Action<FurnitureDefinitionAsset> onSelected)
        {
            ClearBinding();
            Clear();
            EnsureOwnListener();
            selected = onSelected;
            Definition = entry?.Definition;

            var missingDefinition = Definition == null;
            var missingPrefab = !missingDefinition && Definition.Prefab == null;
            var missingThumbnail = entry?.Thumbnail == null;
            var valid = !missingDefinition && !missingPrefab && !missingThumbnail;

            if (nameLabel != null)
            {
                nameLabel.text = missingDefinition ? "Unavailable" : Definition.DisplayName;
            }

            if (footprintLabel != null)
            {
                footprintLabel.text = missingDefinition
                    ? string.Empty
                    : Definition.FootprintWidth + " × " + Definition.FootprintDepth;
            }

            if (thumbnailImage != null)
            {
                thumbnailImage.sprite = entry?.Thumbnail;
                thumbnailImage.enabled = entry?.Thumbnail != null;
            }

            var diagnostic = missingDefinition
                ? "Missing definition"
                : missingPrefab
                    ? "Missing prefab"
                    : missingThumbnail
                        ? "Missing thumbnail"
                        : string.Empty;
            if (warningLabel != null)
            {
                warningLabel.text = diagnostic;
            }

            warningShape?.SetActive(!valid);
            if (button != null)
            {
                button.interactable = valid;
            }
        }

        public void Clear()
        {
            ClearBinding();
            Definition = null;
            selected = null;
            if (button != null)
            {
                button.interactable = false;
            }

            if (thumbnailImage != null)
            {
                thumbnailImage.sprite = null;
                thumbnailImage.enabled = false;
            }

            if (nameLabel != null)
            {
                nameLabel.text = string.Empty;
            }

            if (footprintLabel != null)
            {
                footprintLabel.text = string.Empty;
            }

            if (warningLabel != null)
            {
                warningLabel.text = string.Empty;
            }

            warningShape?.SetActive(false);
        }

        private void EnsureOwnListener()
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }

        private void HandleClick()
        {
            if (!isActiveAndEnabled
                || !gameObject.activeInHierarchy
                || !IsInteractable)
            {
                return;
            }

            selected?.Invoke(Definition);
        }

        private void OnDestroy()
        {
            ClearBinding();
        }
        private void ClearBinding()
        {
            button?.onClick.RemoveListener(HandleClick);
            button?.onClick.RemoveListener(HandleModelClick);
            selected = null;
            selectedModel = null;
            boundItem = null;
            Definition = null;
            ItemId = null;
            if (button != null) button.interactable = false;
        }
    }
}

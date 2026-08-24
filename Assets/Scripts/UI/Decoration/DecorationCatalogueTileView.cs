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

        private Action<FurnitureDefinitionAsset> selected;

        public FurnitureDefinitionAsset Definition { get; private set; }

        public bool IsInteractable => isActiveAndEnabled
            && gameObject.activeInHierarchy
            && button != null
            && button.isActiveAndEnabled
            && button.interactable
            && Definition != null;

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
            button?.onClick.RemoveListener(HandleClick);
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
            button?.onClick.RemoveListener(HandleClick);
        }
    }
}

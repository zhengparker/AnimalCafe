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
        private const string CashRegisterThumbnailId = "equipment.cash-register.01";
        // 256 px approved source: keep 22-24 px transparent breathing room around the
        // 101 x 122 subject instead of displaying the full transparent canvas.
        // 保留22-24 px安全留白，只改收银机取景，不改原PNG或卡片尺寸。
        private const float ApprovedCashRegisterSourceSize = 256f;
        private const float CashRegisterFrameX = 54f;
        private const float CashRegisterFrameY = 45f;
        private const float CashRegisterFrameWidth = 148f;
        private const float CashRegisterFrameHeight = 166f;

        [SerializeField] private AnimalCafe.UI.P8R.P8RAppearance appearance;
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
            var surface = item != null && UsesSurfaceImageOnly(item.Kind);
            if (nameLabel != null) { nameLabel.gameObject.SetActive(!surface); nameLabel.text = surface || item == null ? string.Empty : item.DisplayName; }
            if (appearance != null && nameLabel != null)
            {
                nameLabel.text = surface || item == null ? string.Empty : KeepFootprintTogether(
                    appearance.ItemName(item.ItemId,
                        appearance.ItemName(item.FurnitureDefinition, item.DisplayName)),
                    item.FurnitureDefinition);
                nameLabel.font = appearance.Font;
                nameLabel.textWrappingMode = TextWrappingModes.Normal;
                nameLabel.maxVisibleLines = 2;
                nameLabel.fontSize = 28f;
                nameLabel.rectTransform.anchorMax = new Vector2(1, .36f);
                // The approved ASCII font has no ellipsis glyph; two-line fit is verified separately.
                nameLabel.overflowMode = TextOverflowModes.Truncate;
            }
            if (footprintLabel != null) footprintLabel.gameObject.SetActive(false);
            if (thumbnailImage != null)
            {
                var plainPaint = appearance != null && item?.PaintSwatchColor != null;
                var source = plainPaint ? null : appearance != null
                    ? appearance.Thumbnail(item?.ItemId, item?.Thumbnail)
                    : item?.Thumbnail;
                thumbnailImage.sprite = FrameThumbnail(item?.ItemId, source);
                thumbnailImage.color = plainPaint ? item.PaintSwatchColor.Value : Color.white;
                thumbnailImage.enabled = plainPaint || thumbnailImage.sprite != null;
                var thumbnailRect = thumbnailImage.rectTransform;
                thumbnailRect.anchorMin = new Vector2(thumbnailRect.anchorMin.x, surface ? 0f : appearance != null ? .36f : .22f);
                var minimum = thumbnailRect.offsetMin;
                minimum.y = surface ? 6f : 4f;
                thumbnailRect.offsetMin = minimum;
                if (appearance != null && transform.Find("ThumbnailWell") is RectTransform well)
                {
                    // Surface cards have no name row. The well and image must share that reservation.
                    // Wall/Floor 的内框与图片一起铺开，图片在内框中再留 8 单位，不盖住边框。
                    well.anchorMin = Vector2.zero; well.anchorMax = Vector2.one;
                    well.offsetMin = new Vector2(12f, surface ? 12f : 92f);
                    well.offsetMax = new Vector2(-12f, -12f);
                    thumbnailRect.anchorMin = new Vector2(0f, surface ? 0f : .36f);
                    thumbnailRect.anchorMax = Vector2.one;
                    thumbnailRect.offsetMin = surface ? new Vector2(20f, 20f) : new Vector2(6f, 4f);
                    thumbnailRect.offsetMax = surface ? new Vector2(-20f, -20f) : new Vector2(-6f, -6f);
                    thumbnailImage.preserveAspect = true;
                }
            }
            usingCheck?.SetActive(false); previewOutline?.SetActive(false); noneIcon?.SetActive(item != null && item.IsNoneOption);
            if (button != null) { button.interactable = item != null; button.onClick.RemoveListener(HandleModelClick); selectedModel = onSelected; button.onClick.AddListener(HandleModelClick); }
            RefreshMobileLayout();
        }

        public void RefreshMobileLayout()
        {
            if (appearance == null) return;
            var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
            var surface = boundItem != null && UsesSurfaceImageOnly(boundItem.Kind);
            if (nameLabel != null)
            {
                nameLabel.fontSize = metrics.Units(11.5f);
                nameLabel.maxVisibleLines = 2;
                var rect = nameLabel.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1, 0);
                // Keep 62 logical units for two readable lines while returning height to the preview.
                // 保留62单位文字宽度与两行显示，把更多高度还给缩略图。
                rect.offsetMin = new Vector2(metrics.Units(3), metrics.Units(4));
                rect.offsetMax = new Vector2(-metrics.Units(3), metrics.Units(38));
            }
            if (transform.Find("ThumbnailWell") is RectTransform well)
            {
                well.anchorMin = Vector2.zero; well.anchorMax = Vector2.one;
                well.offsetMin = new Vector2(metrics.Units(surface ? 6 : 5),
                    metrics.Units(surface ? 6 : 39));
                well.offsetMax = Vector2.one * -metrics.Units(surface ? 6 : 5);
            }
            if (thumbnailImage != null)
            {
                var rect = thumbnailImage.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(metrics.Units(surface ? 10 : 8),
                    metrics.Units(surface ? 10 : 42));
                rect.offsetMax = Vector2.one * -metrics.Units(surface ? 10 : 8);
            }
        }

        private void OnEnable()
        {
            AnimalCafe.UI.P8R.P8RMobileMetrics.Changed += RefreshMobileLayout;
            RefreshMobileLayout();
        }
        private void OnDisable() => AnimalCafe.UI.P8R.P8RMobileMetrics.Changed -= RefreshMobileLayout;

        private static string KeepFootprintTogether(string displayName, FurnitureDefinitionAsset definition)
        {
            if (definition == null || string.IsNullOrEmpty(displayName)) return displayName;
            var spaced = definition.FootprintWidth + " x " + definition.FootprintDepth;
            var compact = definition.FootprintWidth + "x" + definition.FootprintDepth;
            var token = displayName.Contains(spaced) ? spaced
                : displayName.Contains(compact) ? compact : null;
            if (token == null) return displayName;
            var tokenStart = displayName.IndexOf(token, StringComparison.Ordinal);
            var prefix = displayName.Substring(0, tokenStart).TrimEnd();
            var suffix = displayName.Substring(tokenStart + token.Length);
            return (prefix.Length == 0 ? string.Empty : prefix + "\n") + spaced + suffix;
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
        private Sprite framedThumbnail;
        private Sprite framedThumbnailSource;
        private Rect framedThumbnailSourceRect;
        private void HandleModelClick()
        {
            if (boundItem != null && IsInteractable) selectedModel?.Invoke(boundItem);
        }
        private static bool UsesSurfaceImageOnly(DecorationCatalogueItemKind kind)
        {
            return kind == DecorationCatalogueItemKind.Floor
                || kind == DecorationCatalogueItemKind.WallSurface;
        }

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
                ReleaseFramedThumbnail();
                thumbnailImage.sprite = entry?.Thumbnail;
                thumbnailImage.color = Color.white;
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
                ReleaseFramedThumbnail();
                thumbnailImage.sprite = null;
                thumbnailImage.color = Color.white;
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
            ReleaseFramedThumbnail();
        }

        private Sprite FrameThumbnail(string itemId, Sprite source)
        {
            if (!string.Equals(itemId, CashRegisterThumbnailId, StringComparison.Ordinal)
                || source == null
                || source.texture == null
                || !TryGetApprovedCashSourceRect(source, out var sourceRect))
            {
                ReleaseFramedThumbnail();
                return source;
            }

            if (framedThumbnail != null
                && framedThumbnailSource == source
                && framedThumbnailSourceRect == sourceRect)
            {
                return framedThumbnail;
            }

            ReleaseFramedThumbnail();
            var crop = new Rect(
                sourceRect.x + CashRegisterFrameX,
                sourceRect.y + CashRegisterFrameY,
                CashRegisterFrameWidth,
                CashRegisterFrameHeight);
            if (crop.width < 1f || crop.height < 1f
                || crop.xMin < 0f || crop.yMin < 0f
                || crop.xMax > source.texture.width || crop.yMax > source.texture.height)
            {
                return source;
            }

            framedThumbnail = Sprite.Create(source.texture, crop, new Vector2(.5f, .5f),
                source.pixelsPerUnit, 0u, SpriteMeshType.FullRect, Vector4.zero, false);
            if (framedThumbnail == null)
            {
                return source;
            }

            framedThumbnail.name = source.name + "_Framed";
            framedThumbnail.hideFlags = HideFlags.HideAndDontSave;
            framedThumbnailSource = source;
            framedThumbnailSourceRect = sourceRect;
            return framedThumbnail;
        }

        private static bool TryGetApprovedCashSourceRect(Sprite source, out Rect sourceRect)
        {
            sourceRect = default;
            if (source.packed
                || !Mathf.Approximately(source.rect.width, ApprovedCashRegisterSourceSize)
                || !Mathf.Approximately(source.rect.height, ApprovedCashRegisterSourceSize))
            {
                return false;
            }

            sourceRect = source.textureRect;
            return Mathf.Approximately(sourceRect.width, ApprovedCashRegisterSourceSize)
                && Mathf.Approximately(sourceRect.height, ApprovedCashRegisterSourceSize);
        }

        private void ReleaseFramedThumbnail()
        {
            var owned = framedThumbnail;
            framedThumbnail = null;
            framedThumbnailSource = null;
            framedThumbnailSourceRect = default;
            if (owned == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(owned);
            }
            else
            {
                DestroyImmediate(owned);
            }
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

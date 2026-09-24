using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.UI.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnimalCafe.UI.Decoration
{
    public enum DecorationCatalogueState
    {
        Hidden,
        Expanded,
        Collapsed
    }
    public enum DecorationSheetState { Hidden, Expanded, CompactPreview, TabsOnly }

    public sealed class DecorationCategoryRowView
    {
        public DecorationCategoryRowView(ScrollRect scroll, bool partial) { HorizontalScroll = scroll; RevealsPartialNextCard = partial; }
        public ScrollRect HorizontalScroll { get; }
        public bool RevealsPartialNextCard { get; }
    }

    /// <summary>
    /// Presents the compact mobile catalogue and reuses a small tile pool.
    /// 显示 mobile catalogue，并重复使用小型 tile pool。
    /// </summary>
    public sealed class DecorationCatalogueView : MonoBehaviour
    {
        [SerializeField] private AnimalCafe.UI.P8R.P8RAppearance appearance;
        [SerializeField] private ScrollRect verticalScroll;
        [SerializeField] private RectTransform categoryContent;
        [SerializeField] private GameObject categoryRowTemplate;
        [SerializeField] private DecorationCatalogueTileView categoryTileTemplate;
        [SerializeField] private Button pickUpPointButton;
        private readonly List<DecorationCategoryRowView> categoryRows = new List<DecorationCategoryRowView>();
        private readonly Dictionary<string, BrowsingPosition> browsingMemory = new Dictionary<string, BrowsingPosition>(StringComparer.Ordinal);
        private readonly Dictionary<string, ScrollRect> browsingRows = new Dictionary<string, ScrollRect>(StringComparer.Ordinal);
        private string browsingContextKey;
        private RectTransform p8rTopObstruction;
        private RectTransform p8rInstructionObstruction;
        public void SetInstructionObstruction(RectTransform obstruction)
        {
            p8rInstructionObstruction = obstruction;
            RefreshP8RLayout();
        }
        public bool HasActivePreview => handleHasActivePreview;

        public void SetTopObstruction(RectTransform obstruction)
        {
            p8rTopObstruction = obstruction;
            RefreshP8RLayout();
        }
        private BrowsingPosition pendingBrowsingPosition;
        private Coroutine browsingRestoreCoroutine;
        private ScrollRect nestedDragSource;
        private bool nestedDragSourceHorizontal;
        private int nestedDragAxis;
        private Vector2 nestedDragAccumulatedDelta;
        private Vector2 nestedDragSourceStart;
        private bool nestedVerticalPointerDragStarted;
        private const float NestedDragThreshold = 8f;
        public DecorationSheetState SheetState { get; private set; } = DecorationSheetState.Hidden;
        public bool AreCategoryRowsVisible => SheetState == DecorationSheetState.Expanded;
        public float PartialNextCardViewportInset
        {
            get
            {
                foreach (var row in categoryRows)
                {
                    var scroll = row.HorizontalScroll;
                    if (scroll?.viewport == null || scroll.content == null || scroll.content.childCount == 0)
                        continue;
                    var card = scroll.content.GetChild(0) as RectTransform;
                    if (card == null || card.rect.width <= 0f) continue;
                    var layout = scroll.content.GetComponent<HorizontalLayoutGroup>();
                    var cardStep = card.rect.width + (layout != null ? layout.spacing : 0f);
                    if (cardStep <= 0f) continue;
                    var remainder = Mathf.Repeat(scroll.viewport.rect.width, cardStep);
                    return remainder > 0f ? remainder : Mathf.Min(card.rect.width * 0.25f, card.rect.width - 1f);
                }
                return 0f;
            }
        }
        public IReadOnlyList<DecorationCategoryRowView> CategoryRows => categoryRows;
        public ScrollRect VerticalScroll => verticalScroll;
        public void ConfigureCategoryTemplates(GameObject rowTemplate, DecorationCatalogueTileView tileTemplate)
        {
            categoryRowTemplate = rowTemplate;
            categoryTileTemplate = tileTemplate;
        }
        public void SetSheetState(DecorationSheetState state, bool hasActivePreview)
        {
            handleHasActivePreview = hasActivePreview;
            SheetState = hasActivePreview && state == DecorationSheetState.TabsOnly ? DecorationSheetState.CompactPreview : state;
            RefreshEditingContext();
            if (SheetState != DecorationSheetState.Expanded) EndNestedDrag();
            // Detach tools while their ancestors are still active. OnDisable can refresh layout.
            // 在祖先停用前移回工具，避免 OnDisable 回调在层级停用遍历中改父级。
            var restoreTools = SheetState != DecorationSheetState.Expanded && scrollingToolParents.Count > 0;
            if (restoreTools) RestoreScrollingTools();
            expandedRoot?.SetActive(SheetState == DecorationSheetState.Expanded);
            collapsedRoot?.SetActive(SheetState == DecorationSheetState.CompactPreview);
            sheetActionRoot?.SetActive(SheetState != DecorationSheetState.TabsOnly
                && SheetState != DecorationSheetState.Hidden);
            if (restoreTools) RefreshP8RLayout();
            // ModeTabs is a child of this Bottom Sheet, so tweening the shared root
            // keeps the raised tabs physically attached throughout collapse/expand.
            BeginTransition(SheetState == DecorationSheetState.Expanded
                ? DecorationCatalogueState.Expanded
                : SheetState == DecorationSheetState.Hidden
                    ? DecorationCatalogueState.Hidden
                    : DecorationCatalogueState.Collapsed);
        }
        public DecorationSheetState ApplySheetDrag(float verticalDelta, bool hasActivePreview)
        {
            var requested = verticalDelta < 0f ? DecorationSheetState.TabsOnly : DecorationSheetState.Expanded;
            SetSheetState(requested, hasActivePreview);
            return SheetState;
        }
        public string TryRouteNestedDrag(Vector2 delta)
        {
            if (nestedDragSource == null && categoryRows.Count > 0) BeginNestedDrag(categoryRows[0].HorizontalScroll);
            return UpdateNestedDrag(delta);
        }
        public void BeginNestedDrag(ScrollRect sourceRow)
        {
            CancelBrowsingRestore();
            EndNestedDrag();
            nestedDragSource = sourceRow;
            nestedDragSourceHorizontal = sourceRow != null && sourceRow.horizontal;
            nestedDragAxis = 0;
            nestedDragAccumulatedDelta = Vector2.zero;
            nestedDragSourceStart = sourceRow?.content != null
                ? sourceRow.content.anchoredPosition
                : Vector2.zero;
            nestedVerticalPointerDragStarted = false;
            NestedDragOwner = null;
            IsSceneDragBlocked = sourceRow != null;
        }
        public string UpdateNestedDrag(Vector2 delta)
        {
            return UpdateNestedDrag(delta, moveContent: true);
        }

        private string UpdateNestedDrag(Vector2 delta, bool moveContent)
        {
            if (nestedDragSource == null) return "None";
            var routedDelta = delta;
            if (nestedDragAxis == 0)
            {
                nestedDragAccumulatedDelta += delta;
                if (nestedDragAccumulatedDelta.magnitude < NestedDragThreshold) return "Pending";
                nestedDragAxis = Mathf.Abs(nestedDragAccumulatedDelta.x) > Mathf.Abs(nestedDragAccumulatedDelta.y) ? 1 : 2;
                NestedDragOwner = nestedDragAxis == 1 ? nestedDragSource : verticalScroll;
                routedDelta = nestedDragAccumulatedDelta;
            }
            if (moveContent && NestedDragOwner?.content != null)
                NestedDragOwner.content.anchoredPosition += nestedDragAxis == 1 ? new Vector2(routedDelta.x, 0f) : new Vector2(0f, routedDelta.y);
            return nestedDragAxis == 1 ? "Horizontal" : "Vertical";
        }
        public void EndNestedDrag()
        {
            if (nestedDragSource != null)
                nestedDragSource.horizontal = nestedDragSourceHorizontal;
            nestedDragSource = null;
            nestedDragAxis = 0;
            nestedDragAccumulatedDelta = Vector2.zero;
            nestedDragSourceStart = Vector2.zero;
            nestedVerticalPointerDragStarted = false;
            NestedDragOwner = null;
            IsSceneDragBlocked = false;
        }
        public void BindCategories(IReadOnlyList<DecorationCategoryModel> categories, Action<DecorationCatalogueItemModel> selected)
        {
            BindCategories(null, categories, selected);
        }

        /// <summary>Keep browsing positions only for this Decoration session, keyed by Tab and CategoryId.</summary>
        public void BindCategories(string contextKey, IReadOnlyList<DecorationCategoryModel> categories, Action<DecorationCatalogueItemModel> selected)
        {
            RememberBrowsingPosition();
            CancelBrowsingRestore();
            StopBrowsingMotion();
            browsingContextKey = contextKey;
            RefreshP8RLayout();
            RestoreScrollingTools();
            browsingRows.Clear();
            if (pickUpPointButton != null)
                pickUpPointButton.gameObject.SetActive(IsFurnitureTab(categories));
            if (categoryContent != null)
                foreach (Transform child in categoryContent)
                    if (child.gameObject != categoryRowTemplate)
                    {
                        child.gameObject.SetActive(false);
                        Destroy(child.gameObject);
                    }
            categoryRows.Clear();
            if (verticalScroll != null)
            {
                verticalScroll.vertical = true;
                verticalScroll.horizontal = false;
                // Catalogue uses pointer dragging; mouse wheel remains available for camera zoom.
                // 目录保留 pointer drag，滚轮继续用于 Camera zoom。
                verticalScroll.scrollSensitivity = 0f;
            }
            if (categories == null) return;
            foreach (var category in categories)
            {
                if (category == null) continue;
                GameObject row = categoryRowTemplate != null
                    ? Instantiate(categoryRowTemplate, categoryContent)
                    : CreateRuntimeRow(categoryContent);
                row.name = "CategoryRow_" + category.CategoryId;
                row.SetActive(true);
                var scroll = row.GetComponent<ScrollRect>() ?? row.AddComponent<ScrollRect>();
                scroll.horizontal = true; scroll.vertical = false;
                scroll.scrollSensitivity = 0f;
                ConfigureNestedPointerDrag(row, scroll);
                var label = row.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = appearance != null ? appearance.Text("category." + category.CategoryId) : category.DisplayName;
                var itemContent = scroll.content;
                if (itemContent == null)
                {
                    var content = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup));
                    content.transform.SetParent(row.transform, false);
                    itemContent = content.GetComponent<RectTransform>();
                    scroll.content = itemContent;
                }
                if (itemContent.GetComponent<HorizontalLayoutGroup>() == null)
                    itemContent.gameObject.AddComponent<HorizontalLayoutGroup>();
                for (var itemIndex = 0; itemIndex < category.Items.Count; itemIndex++)
                {
                    var item = category.Items[itemIndex];
                    if (item == null) continue;
                    var tile = CreateCategoryTile(itemContent);
                    tile.name = "CatalogueTile_" + (itemIndex + 1).ToString("D3") + "_" + item.ItemId;
                    tile.gameObject.SetActive(true);
                    tile.Bind(item, clicked =>
                    {
                        selected?.Invoke(clicked);
                        if (clicked?.Kind == DecorationCatalogueItemKind.Furniture
                            && clicked.FurnitureDefinition != null)
                            Selected?.Invoke(clicked.FurnitureDefinition);
                    });
                }
                categoryRows.Add(new DecorationCategoryRowView(scroll, true));
                browsingRows[category.CategoryId] = scroll;
            }
            RefreshP8RLayout();
            if (categoryContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(categoryContent);
            }
            if (verticalScroll != null)
            {
                verticalScroll.StopMovement();
                verticalScroll.verticalNormalizedPosition = 1f;
            }
            if (!string.IsNullOrEmpty(browsingContextKey))
            {
                var position = browsingMemory.TryGetValue(browsingContextKey, out var remembered)
                    ? remembered : new BrowsingPosition();
                ApplyBrowsingPosition(position);
                // Layout can resize rows after binding. Restore again once those bounds are available.
                // 分类重建后还会经历一次布局更新，再按新范围恢复，避免滚到内容之外。
                pendingBrowsingPosition = position;
                if (isActiveAndEnabled)
                    browsingRestoreCoroutine = StartCoroutine(RestoreBrowsingAfterLayout());
            }
        }

        public void ResetBrowsingMemory()
        {
            CancelBrowsingRestore();
            StopBrowsingMotion();
            browsingMemory.Clear();
            browsingContextKey = null;
            ApplyBrowsingPosition(new BrowsingPosition());
        }

        private void RememberBrowsingPosition()
        {
            if (string.IsNullOrEmpty(browsingContextKey)) return;
            if (pendingBrowsingPosition != null)
            {
                browsingMemory[browsingContextKey] = pendingBrowsingPosition;
                return;
            }
            var position = new BrowsingPosition
            {
                Vertical = ClampBrowsingPosition(verticalScroll != null ? verticalScroll.verticalNormalizedPosition : 1f, 1f)
            };
            foreach (var row in browsingRows)
                if (row.Value != null)
                    position.Horizontal[row.Key] = ClampBrowsingPosition(row.Value.horizontalNormalizedPosition, 0f);
            browsingMemory[browsingContextKey] = position;
        }

        private void ApplyBrowsingPosition(BrowsingPosition position)
        {
            if (verticalScroll != null)
            {
                verticalScroll.StopMovement();
                verticalScroll.verticalNormalizedPosition = ClampBrowsingPosition(position.Vertical, 1f);
            }
            foreach (var row in browsingRows)
            {
                if (row.Value == null) continue;
                row.Value.StopMovement();
                row.Value.horizontalNormalizedPosition = position.Horizontal.TryGetValue(row.Key, out var horizontal)
                    ? ClampBrowsingPosition(horizontal, 0f) : 0f;
            }
        }

        private IEnumerator RestoreBrowsingAfterLayout()
        {
            yield return null;
            if (categoryContent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(categoryContent);
            if (pendingBrowsingPosition != null) ApplyBrowsingPosition(pendingBrowsingPosition);
            pendingBrowsingPosition = null;
            browsingRestoreCoroutine = null;
        }

        private void CancelBrowsingRestore()
        {
            if (browsingRestoreCoroutine != null) StopCoroutine(browsingRestoreCoroutine);
            browsingRestoreCoroutine = null;
            pendingBrowsingPosition = null;
        }

        private void StopBrowsingMotion()
        {
            if (nestedVerticalPointerDragStarted && verticalScroll != null)
                verticalScroll.OnEndDrag(new PointerEventData(EventSystem.current));
            EndNestedDrag();
            verticalScroll?.StopMovement();
            foreach (var row in categoryRows) row.HorizontalScroll?.StopMovement();
        }

        private static float ClampBrowsingPosition(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value);
        }

        private sealed class BrowsingPosition
        {
            public float Vertical = 1f;
            public readonly Dictionary<string, float> Horizontal = new Dictionary<string, float>(StringComparer.Ordinal);
        }

        private void ConfigureNestedPointerDrag(GameObject row, ScrollRect sourceRow)
        {
            var trigger = row.GetComponent<EventTrigger>() ?? row.AddComponent<EventTrigger>();
            trigger.triggers = new List<EventTrigger.Entry>();
            AddNestedTrigger(trigger, EventTriggerType.InitializePotentialDrag, data =>
            {
                if (data is PointerEventData pointer)
                {
                    verticalScroll?.OnInitializePotentialDrag(pointer);
                }
            });
            AddNestedTrigger(trigger, EventTriggerType.BeginDrag, data =>
            {
                if (data is PointerEventData)
                {
                    BeginNestedDrag(sourceRow);
                }
            });
            AddNestedTrigger(trigger, EventTriggerType.Drag, data =>
            {
                if (data is PointerEventData pointer)
                {
                    RouteNestedPointerDrag(sourceRow, pointer);
                }
            });
            AddNestedTrigger(trigger, EventTriggerType.EndDrag, data =>
            {
                if (data is PointerEventData pointer)
                {
                    if (nestedVerticalPointerDragStarted)
                    {
                        verticalScroll?.OnEndDrag(pointer);
                    }
                    EndNestedDrag();
                }
            });
        }

        private static void AddNestedTrigger(
            EventTrigger trigger,
            EventTriggerType eventType,
            UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = eventType };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        private void RouteNestedPointerDrag(ScrollRect sourceRow, PointerEventData pointer)
        {
            var owner = UpdateNestedDrag(pointer.delta, moveContent: false);
            if (owner != "Vertical" || verticalScroll == null)
            {
                return;
            }

            if (!nestedVerticalPointerDragStarted)
            {
                nestedVerticalPointerDragStarted = true;
                // StopMovement clears velocity, not ScrollRect's active pointer drag.
                // 锁定纵向后暂时关闭横轴，EndNestedDrag 恢复原设置。
                sourceRow.horizontal = false;
                sourceRow.StopMovement();
                if (sourceRow.content != null)
                {
                    sourceRow.content.anchoredPosition = nestedDragSourceStart;
                }
                verticalScroll.StopMovement();
                verticalScroll.OnBeginDrag(pointer);
                return;
            }

            verticalScroll.OnDrag(pointer);
        }
        private GameObject CreateRuntimeRow(Transform parent)
        {
            var row = new GameObject("CategoryRow", typeof(RectTransform), typeof(ScrollRect));
            row.transform.SetParent(parent, false);
            var label = new GameObject("CategoryLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(row.transform, false);
            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(row.transform, false);
            var content = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            content.transform.SetParent(viewport.transform, false);
            var scroll = row.GetComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = content.GetComponent<RectTransform>();
            return row;
        }
        private DecorationCatalogueTileView CreateCategoryTile(Transform parent)
        {
            if (categoryTileTemplate != null)
                return Instantiate(categoryTileTemplate, parent);

            var root = new GameObject("Item", typeof(RectTransform), typeof(Image), typeof(Button), typeof(DecorationCatalogueTileView));
            root.transform.SetParent(parent, false);
            var thumbnail = new GameObject("Thumbnail", typeof(RectTransform), typeof(Image));
            thumbnail.transform.SetParent(root.transform, false);
            var name = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            name.transform.SetParent(root.transform, false);
            var usingCheck = new GameObject("UsingCheck", typeof(RectTransform));
            usingCheck.transform.SetParent(root.transform, false);
            var previewOutline = new GameObject("PreviewOutline", typeof(RectTransform));
            previewOutline.transform.SetParent(root.transform, false);
            var noneIcon = new GameObject("NoneIcon", typeof(RectTransform));
            noneIcon.transform.SetParent(root.transform, false);
            var tile = root.GetComponent<DecorationCatalogueTileView>();
            tile.ConfigureRuntimeViews(root.GetComponent<Button>(), thumbnail.GetComponent<Image>(),
                name.GetComponent<TMP_Text>(), usingCheck, previewOutline, noneIcon);
            return tile;
        }
        public void SetSurfaceState(string usingItemId, string previewItemId)
        {
            SetSurfaceStates(string.IsNullOrEmpty(usingItemId)
                ? Array.Empty<string>()
                : new[] { usingItemId }, previewItemId);
        }

        public void SetSurfaceStates(IEnumerable<string> usingItemIds, string previewItemId)
        {
            var currentIds = new HashSet<string>(
                usingItemIds?.Where(itemId => !string.IsNullOrEmpty(itemId))
                    ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            foreach (var tile in GetComponentsInChildren<DecorationCatalogueTileView>(true))
                tile.SetSurfaceState(currentIds.Contains(tile.ItemId), tile.ItemId == previewItemId);
        }
        private const float TransitionDuration = 0.16f;
        private const float TileVerticalStep = 144f;
        private const float CatalogueSideInset = 24f;
        private const float LandscapeRightRailInset = 228f;

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject expandedRoot;
        [SerializeField] private GameObject collapsedRoot;
        [SerializeField] private GameObject sheetActionRoot;
        [SerializeField] private RectTransform surfaceFooterHost;
        [SerializeField] private Vector2 surfaceFooterExpandedAnchoredPosition = new Vector2(0f, 24f);
        [SerializeField] private Button collapseButton;
        [SerializeField] private Button collapsedHandleButton;
        [SerializeField] private Transform contentRoot;
        [SerializeField] private DecorationCatalogueTileView tileTemplate;
        [SerializeField] private Vector2 expandedAnchoredPosition;
        [SerializeField] private Vector2 collapsedAnchoredPosition = new Vector2(0f, -220f);
        [SerializeField] private Vector2 hiddenAnchoredPosition = new Vector2(0f, -420f);

        private readonly List<DecorationCatalogueTileView> tilePool =
            new List<DecorationCatalogueTileView>();
        private IUiPointerOwnershipRegistrar pointerBoundary;
        private UiTransitionRunner transitionRunner;
        private Coroutine transitionCoroutine;
        private GameObject editingContextRoot;
        private TMP_Text editingContextLabel;
        private Button returnToEditingButton;
        private RectTransform editingScrollRect;
        private Vector2 editingScrollOriginalOffset;
        private string editingContextMessage;
        private bool editingContextCanReturn;
        private bool editingContextBlocked;
        private TMP_Text collapsedHandleLabel;
        private string originalCollapsedHandleText;
        private bool handleHasActivePreview;
        private const float EditingContextHeight = 120f;
        private bool refreshingP8RLayout;
        private readonly Dictionary<RectTransform, Transform> scrollingToolParents = new();
        private bool FooterScrolls => surfaceFooterHost != null && surfaceFooterHost.parent == categoryContent;

        // Only constrained sheets place the original tools inside the existing scroll content.
        // 仅高度不足时让原有工具随目录滚动；父级变化不重建按钮或预览事务。
        private void SetScrollingTool(RectTransform tool, bool scrolls, float height = 0)
        {
            if (tool == null || categoryContent == null) return;
            if (scrolls)
            {
                if (!scrollingToolParents.ContainsKey(tool)) scrollingToolParents.Add(tool, tool.parent);
                if (tool.parent != categoryContent) tool.SetParent(categoryContent, false);
                tool.SetAsLastSibling();
                var element = tool.GetComponent<LayoutElement>() ?? tool.gameObject.AddComponent<LayoutElement>();
                element.ignoreLayout = false;
                element.minHeight = element.preferredHeight = height;
                element.flexibleHeight = 0;
            }
            else if (scrollingToolParents.TryGetValue(tool, out var parent))
            {
                tool.SetParent(parent, false);
                var element = tool.GetComponent<LayoutElement>();
                if (element != null) element.ignoreLayout = true;
                scrollingToolParents.Remove(tool);
            }
        }

        private void RestoreScrollingTools()
        {
            foreach (var tool in scrollingToolParents.Keys.ToArray()) SetScrollingTool(tool, false);
        }

        public event Action<FurnitureDefinitionAsset> Selected;
        public event Action PickUpPointRequested;
        public event Action ReturnToEditingRequested;
        public event Action<DecorationCatalogueState> StateChanged;
        public event Action PresentationSettled;

        public bool IsCatalogueVisible { get; private set; }
        public bool IsCollapsed { get; private set; }
        /// <summary>True while a nested catalogue ScrollRect owns the current drag.</summary>
        public bool IsSceneDragBlocked { get; private set; }
        public ScrollRect NestedDragOwner { get; private set; }
        public DecorationCatalogueState State { get; private set; } =
            DecorationCatalogueState.Hidden;
        public RectTransform CollapsedHandleRect =>
            collapsedRoot != null ? collapsedRoot.transform as RectTransform : null;
        public RectTransform SurfaceFooterHost => surfaceFooterHost;

        public void SetEditingContext(string message, bool canReturn)
        {
            editingContextMessage = message;
            editingContextCanReturn = canReturn;
            editingContextBlocked = false;
            handleHasActivePreview = !string.IsNullOrEmpty(message);
            if (!string.IsNullOrEmpty(message)) EnsureEditingContext();
            RefreshEditingContext();
        }

        public void ExplainEditingRestriction()
        {
            if (string.IsNullOrEmpty(editingContextMessage)) return;
            editingContextBlocked = true;
            RefreshEditingContext();
        }

        private void EnsureEditingContext()
        {
            if (editingContextRoot != null || expandedRoot == null) return;
            var reference = expandedRoot.GetComponentInChildren<TMP_Text>(true);
            editingContextRoot = new GameObject("EditingContext", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)editingContextRoot.transform;
            rect.SetParent(expandedRoot.transform, false);
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(40, -96 - EditingContextHeight);
            rect.offsetMax = new Vector2(-40, -96);
            editingContextRoot.GetComponent<Image>().color = new Color(1f, .97f, .9f, .96f);
            var message = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            message.transform.SetParent(rect, false);
            editingContextLabel = message.GetComponent<TMP_Text>();
            if (reference != null) editingContextLabel.font = reference.font;
            editingContextLabel.fontSize = 21;
            editingContextLabel.color = new Color(.18f, .15f, .12f);
            editingContextLabel.richText = false;
            editingContextLabel.textWrappingMode = TextWrappingModes.Normal;
            editingContextLabel.alignment = TextAlignmentOptions.MidlineLeft;
            editingContextLabel.raycastTarget = false;
            var labelRect = editingContextLabel.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(12, 8);
            labelRect.offsetMax = new Vector2(-164, -8);
            var button = new GameObject("ReturnToEditing", typeof(RectTransform), typeof(Image), typeof(Button),
                typeof(DecorationPointerBoundaryEventHook));
            var buttonRect = (RectTransform)button.transform;
            buttonRect.SetParent(rect, false);
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1, .5f);
            buttonRect.pivot = new Vector2(1, .5f);
            buttonRect.anchoredPosition = new Vector2(-12, 0);
            buttonRect.sizeDelta = new Vector2(140, 64);
            button.GetComponent<Image>().color = new Color(.88f, .80f, .65f);
            returnToEditingButton = button.GetComponent<Button>();
            returnToEditingButton.targetGraphic = button.GetComponent<Image>();
            var buttonLabel = Instantiate(message, buttonRect, false).GetComponent<TMP_Text>();
            buttonLabel.name = "Label";
            buttonLabel.text = PlacementFeedbackMapper.ReturnToEditing;
            if (appearance != null)
            {
                appearance.Paint(editingContextRoot.GetComponent<Image>(), "notice_preview");
                editingContextRoot.GetComponent<Image>().enabled = false;
                editingContextLabel.gameObject.SetActive(false);
                appearance.Button(returnToEditingButton, "back");
                buttonLabel.text = appearance.Text("catalogue.back");
                buttonLabel.fontSize = 24f;
                buttonRect.sizeDelta = new Vector2(220, 64);
                editingContextLabel.fontSize = 28f;
                editingContextLabel.color = AnimalCafe.UI.P8R.P8RAppearance.Cocoa;
            }
            buttonLabel.alignment = TextAlignmentOptions.Center;
            buttonLabel.rectTransform.offsetMin = new Vector2(8, 4);
            buttonLabel.rectTransform.offsetMax = new Vector2(-8, -4);
            returnToEditingButton.onClick.AddListener(HandleReturnToEditingRequested);
            if (pointerBoundary != null) ConfigurePointerHooks(editingContextRoot);
            editingScrollRect = verticalScroll != null ? verticalScroll.transform as RectTransform : null;
            if (editingScrollRect != null) editingScrollOriginalOffset = editingScrollRect.offsetMax;
        }

        private void RefreshEditingContext()
        {
            // P8R retains only the existing Return button for furniture, not the explanatory card.
            // 家具保留返回编辑按钮；Floor/Wall 不再为说明框预留空间。
            var hasContext = !string.IsNullOrEmpty(editingContextMessage) && (appearance == null || editingContextCanReturn);
            var contextHeight = EditingContextHeight;
            RefreshCollapsedHandleLabel();
            if (editingContextRoot != null)
            {
                editingContextRoot.SetActive(hasContext && SheetState == DecorationSheetState.Expanded);
                editingContextLabel.text = editingContextMessage
                    + (editingContextBlocked ? "\n" + (appearance != null ? appearance.Text("preview.locked_tabs") : PlacementFeedbackMapper.FinishEditingFirst) : string.Empty);
                returnToEditingButton.gameObject.SetActive(hasContext && editingContextCanReturn);
                editingContextLabel.rectTransform.offsetMax = new Vector2(editingContextCanReturn ? (appearance != null ? -244 : -164) : -12, -8);
                if (appearance != null)
                {
                    RefreshP8RLayout();
                    return;
                }
            }
            // Reserve space instead of covering row one; restore the exact original viewport on exit.
            // 留出提示空间，不遮挡首行；结束编辑时恢复原始滚动区域。
            if (editingScrollRect != null)
                editingScrollRect.offsetMax = editingScrollOriginalOffset
                    - (hasContext ? Vector2.up * (contextHeight + 8) : Vector2.zero);
            RefreshP8RLayout();
        }

        private void RefreshP8RLayout()
        {
            if (appearance == null || refreshingP8RLayout || expandedRoot == null || transform is not RectTransform root) return;
            refreshingP8RLayout = true;
            try
            {
                var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
                var side = metrics.Units(8); var padding = metrics.Units(12); var gap = metrics.Units(8);
                var width = Mathf.Max(metrics.Units(240), root.rect.width - side * 2);
                var compactHeader = width >= padding * 2 + metrics.Units(172 + 4 * 48 + 3 * 6);
                var hasContext = !string.IsNullOrEmpty(editingContextMessage) && editingContextCanReturn;
                // On wide screens the existing Return action replaces the redundant title.
                // 宽屏把返回编辑放入标题行，保留卡片浏览高度与原有点击行为。
                var inlineContext = compactHeader && hasContext
                    && (editingContextLabel == null || !editingContextLabel.gameObject.activeSelf)
                    && width >= padding * 2 + metrics.Units(248 + 4 * 48 + 3 * 6);
                // Short phones keep the four categories and collapse target on one row.
                // 矮手机省去重复的 Catalogue 标题，不省略按钮，也不缩小点击区域。
                var minimalHeader = !compactHeader && metrics.LogicalViewport.y <= 700f
                    && width + metrics.Units(.01f) >= metrics.Units(5 * 48 + 4 * 4 + 8);
                var headerPadding = minimalHeader ? metrics.Units(4) : padding;
                var header = metrics.Units(compactHeader ? 52 : minimalHeader ? 56 : 108);
                var availableHeight = root.rect.height - metrics.Units(compactHeader ? 128 : 164);
                foreach (var obstruction in new[] { p8rTopObstruction, p8rInstructionObstruction })
                {
                    if (obstruction == null || !obstruction.gameObject.activeInHierarchy) continue;
                    var bottom = root.InverseTransformPoint(obstruction.TransformPoint(obstruction.rect.min)).y;
                    availableHeight = Mathf.Min(availableHeight, bottom - root.rect.yMin + root.anchoredPosition.y
                        - expandedAnchoredPosition.y - side - metrics.Units(4));
                }
                var panel = (RectTransform)expandedRoot.transform;
                var uncappedHeight = Mathf.Min(metrics.Units(520), Mathf.Max(metrics.Units(120), availableHeight));
                // The complete sheet stays within 45% of safe height; overflow uses its ScrollRect.
                // 整个面板最多占安全区45%，高度不足的工具使用已有目录滚动区。
                var safeHeightCap = Mathf.Min(root.rect.height * .45f, Mathf.Max(0, availableHeight));
                var measure = collapseButton != null ? collapseButton.GetComponentInChildren<TMP_Text>(true) : null;
                var surface = browsingContextKey == "Floor" || browsingContextKey == "Wall";
                var footerPadding = minimalHeader ? metrics.Units(4) : padding;
                var footer = surface ? AnimalCafe.UI.P8R.P8RSurfaceFooterLayout.Measure(this, width - footerPadding * 2,
                    appearance, measure, browsingContextKey == "Floor", handleHasActivePreview) : null;
                var footerHeight = footer != null ? footer.Height : 0;
                var pickupIsVisible = pickUpPointButton != null && pickUpPointButton.gameObject.activeSelf;
                var dualHeaderActions = pickupIsVisible && inlineContext
                    && safeHeightCap < header + metrics.Units(64 + 76)
                    && width >= padding * 2 + metrics.Units(328.03125f + 52 + 4 * 48 + 3 * 6);
                // On a short wide screen, keep the existing Pickup action in the one-row header.
                // Its 48-logical root stays unchanged; this only avoids reserving another row below the cards.
                // 矮宽屏把既有Pickup放入单行header，不缩触控区，也不改变点击行为。
                var pickupInHeader = pickupIsVisible && compactHeader
                    && (!hasContext || dualHeaderActions)
                    && width + metrics.Units(.01f) >= metrics.Units(442)
                    && safeHeightCap < header + metrics.Units(64 + 76);
                // Reuse the existing side-footer treatment before deciding the cap. Otherwise
                // a footer that will ultimately sit beside the cards needlessly forces a tall sheet.
                // 先决定既有侧栏footer，再计算高度；侧栏不会重复占用卡片的垂直空间。
                var shortLandscape = metrics.LogicalViewport.x > metrics.LogicalViewport.y;
                var surfaceGap = shortLandscape ? metrics.Units(4) : gap;
                var asideWidth = metrics.Units(browsingContextKey == "Floor" ? 320 : 160);
                var asideLayout = surface ? AnimalCafe.UI.P8R.P8RSurfaceFooterLayout.Measure(this, asideWidth,
                    appearance, measure, browsingContextKey == "Floor", handleHasActivePreview) : null;
                var asideFooterHeight = asideLayout != null ? asideLayout.Height : 0;
                var asideFooter = surface && SheetState == DecorationSheetState.Expanded
                    && shortLandscape
                    && width >= asideWidth + padding * 2 + gap + metrics.Units(76)
                    && safeHeightCap - header - footerHeight - surfaceGap * 2 < metrics.Units(76);
                var contentGap = safeHeightCap - header - gap < metrics.Units(80)
                    ? metrics.Units(4) : gap;
                var fixedBottomReservation = surface
                    ? asideFooter ? surfaceGap * 2 : footerHeight + surfaceGap * 2
                    : pickupIsVisible && !pickupInHeader ? metrics.Units(64) : contentGap;
                var contextGap = minimalHeader && safeHeightCap < header + metrics.Units(48)
                    + gap + fixedBottomReservation + metrics.Units(76) ? metrics.Units(4) : gap;
                var fixedContextReservation = hasContext && !inlineContext ? metrics.Units(48) + contextGap : 0;
                var fixedReservations = header + fixedContextReservation + fixedBottomReservation;
                var canScrollTools = expandedRoot.activeSelf && categoryContent != null;
                var scrollFooter = canScrollTools && surface && safeHeightCap + metrics.Units(.01f)
                    < fixedReservations + Mathf.Max(metrics.Units(48), asideFooter ? asideFooterHeight : 0);
                if (scrollFooter)
                {
                    asideFooter = false;
                    // Scroll content uses the card column's full width, not the old footer padding.
                    footer = AnimalCafe.UI.P8R.P8RSurfaceFooterLayout.Measure(this, width - padding * 2,
                        appearance, measure, browsingContextKey == "Floor", handleHasActivePreview);
                    footerHeight = footer.Height;
                    fixedBottomReservation = metrics.Units(4);
                }
                var scrollPickup = canScrollTools && pickupIsVisible && !pickupInHeader
                    && safeHeightCap + metrics.Units(.01f) < header + fixedContextReservation
                        + fixedBottomReservation + metrics.Units(76);
                if (scrollPickup) fixedBottomReservation = metrics.Units(4);
                var scrollContext = canScrollTools && hasContext && !inlineContext
                    && safeHeightCap + metrics.Units(.01f) < header + fixedContextReservation
                        + fixedBottomReservation + metrics.Units(76);
                if (scrollContext) fixedContextReservation = 0;
                fixedReservations = header + fixedContextReservation + fixedBottomReservation;
                // A normal row needs its 28-logical heading plus a 48-logical visible card slice.
                // When only that redundant Floor heading prevents the cap, hide it using the
                // existing short-layout pattern instead of clipping the real tile target.
                // 普通行需28标题+48卡片；仅重复Floor标题挡住时沿用既有隐藏模式。
                var hideFloorHeadingForCap = browsingContextKey == "Floor"
                    && safeHeightCap + metrics.Units(.01f) >= fixedReservations + metrics.Units(48)
                    && safeHeightCap < fixedReservations + metrics.Units(76);
                // Keep Furniture/Wall Decor category names; trim only the first heading's blank space.
                // 保留家具与墙饰分类名称，仅在极短屏压缩首行标题下的空白。
                var firstHeadingHeight = Mathf.Clamp((safeHeightCap - fixedReservations) / metrics.Units(1) - 52.125f, 20f, 28f);
                var minimumContentReservation = metrics.Units(hideFloorHeadingForCap ? 48 : 50 + firstHeadingHeight);
                if (asideFooter) minimumContentReservation = Mathf.Max(minimumContentReservation, asideFooterHeight);
                var minimumUsableHeight = fixedReservations + minimumContentReservation;
                var height = Mathf.Min(safeHeightCap, Mathf.Max(Mathf.Min(uncappedHeight, safeHeightCap), minimumUsableHeight));
                // Short content shrinks below the cap. Each row owns an 84-logical card,
                // a 28-logical heading and a small separation; fixed controls keep their reservation.
                // 内容较少时继续收紧；每行保留84卡片、28标题和少量间距。
                if (!hasContext && categoryRows.Count > 0)
                {
                    var rowsHeight = categoryRows.Count * metrics.Units(116)
                        + Mathf.Max(0, categoryRows.Count - 1) * gap;
                    var lowerReservation = fixedBottomReservation;
                    height = Mathf.Min(height, header + rowsHeight + lowerReservation);
                }
                panel.anchorMin = Vector2.zero; panel.anchorMax = new Vector2(1, 0); panel.pivot = new Vector2(.5f, 0);
                panel.anchoredPosition = new Vector2(0, side);
                panel.sizeDelta = new Vector2(-side * 2, height);
                // Restore before applying anchored geometry; scrolling rows are appended below.
                // 先恢复不再滚动的工具父级，再写正常布局；滚动工具稍后追加到内容末尾。
                if (!scrollFooter) SetScrollingTool(surfaceFooterHost, false);
                if (!scrollPickup) SetScrollingTool(pickUpPointButton != null ? (RectTransform)pickUpPointButton.transform : null, false);
                if (!scrollContext) SetScrollingTool(editingContextRoot != null ? (RectTransform)editingContextRoot.transform : null, false);
                // On very short wide screens, place the measured surface footer beside the cards
                // when both columns and the full footer height fit; targets and preview rules stay intact.
                // 短横屏仅在双栏和完整footer高度都能容纳时采用侧栏；不缩按钮、不改变Preview规则。
                if (asideFooter)
                {
                    footer = asideLayout;
                    footerHeight = footer.Height;
                }
                surfaceFooterExpandedAnchoredPosition = new Vector2(
                    asideFooter ? root.rect.width * .5f - side - footerPadding - asideWidth * .5f : 0,
                    side + surfaceGap + (asideFooter ? Mathf.Max(0, height - header - footerHeight - surfaceGap * 2) * .5f : 0));
                if (surfaceFooterHost != null)
                {
                    surfaceFooterHost.anchorMin = asideFooter ? new Vector2(.5f, 0) : Vector2.zero;
                    surfaceFooterHost.anchorMax = asideFooter ? new Vector2(.5f, 0) : new Vector2(1, 0);
                    surfaceFooterHost.pivot = new Vector2(.5f, 0);
                    surfaceFooterHost.sizeDelta = new Vector2(asideFooter ? asideWidth : -side * 2 - footerPadding * 2, footerHeight);
                    surfaceFooterHost.anchoredPosition = surfaceFooterExpandedAnchoredPosition
                        + expandedAnchoredPosition - root.anchoredPosition;
                    // Preview state can change the columns without changing the footer height.
                    // 同高布局仍需同步范围按钮，不能只依赖 RectTransform resize。
                    if (browsingContextKey == "Floor")
                        surfaceFooterHost.GetComponentInChildren<DecorationFloorRangeView>(true)?.RefreshMobileLayout();
                }
                var tabs = GetComponentInChildren<DecorationModeTabsView>(true);
                if (tabs != null)
                {
                    var row = (RectTransform)tabs.transform;
                    // Hidden title/Return/collapse controls must not leave horizontal gaps in the compact bar.
                    // 收起时标题与返回按钮已隐藏，分类行恢复两侧对称留白，不沿用展开标题行的占位。
                    // Legacy furniture ShowCollapsedHandle does not update SheetState; use the actual header visibility.
                    // 家具保留旧的收起入口，此时SheetState可能仍为Expanded；以标题容器实际显示状态为准。
                    var tabsInHeader = expandedRoot.activeSelf;
                    var tabsPadding = tabsInHeader ? headerPadding : padding;
                    var tabsLeading = tabsInHeader && compactHeader
                        ? metrics.Units(dualHeaderActions ? 328.03125f : inlineContext ? 188 : pickupInHeader ? 148 : 112) : 0;
                    var tabsReservation = tabsInHeader
                        ? (compactHeader ? metrics.Units(dualHeaderActions ? 380.03125f : inlineContext ? 248 : pickupInHeader ? 208 : 172)
                            : minimalHeader ? metrics.Units(52) : 0) : 0;
                    row.anchorMin = row.anchorMax = row.pivot = Vector2.zero;
                    row.anchoredPosition = new Vector2(side + tabsPadding + tabsLeading,
                        side + height - header + metrics.Units(4));
                    row.sizeDelta = new Vector2(width - tabsPadding * 2 - tabsReservation, metrics.Units(48));
                    var layout = row.GetComponent<HorizontalLayoutGroup>();
                    if (layout != null) { layout.spacing = metrics.Units(minimalHeader ? 4 : 6); layout.childControlHeight = true; layout.childForceExpandHeight = false; }
                    foreach (var button in tabs.GetComponentsInChildren<Button>(true))
                    {
                        var element = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
                        element.minWidth = metrics.Units(48); element.preferredHeight = metrics.Units(48);
                        ((RectTransform)button.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, metrics.Units(48));
                        AnimalCafe.UI.P8R.P8RButtonLayout.StackedButton(button);
                    }
                }
                var title = panel.Find("P8RCatalogueTitle") as RectTransform;
                if (title != null)
                {
                    title.gameObject.SetActive(!minimalHeader && !inlineContext && !pickupInHeader);
                    title.anchorMin = title.anchorMax = title.pivot = new Vector2(0, 1);
                    title.anchoredPosition = new Vector2(padding, -metrics.Units(compactHeader ? 12 : 8));
                    title.sizeDelta = new Vector2(compactHeader ? metrics.Units(104) : width - metrics.Units(84), metrics.Units(32));
                    var copy = title.GetComponent<TMP_Text>(); if (copy != null) copy.fontSize = metrics.Units(16);
                }
                foreach (var button in new[] { collapseButton, collapsedHandleButton, pickUpPointButton })
                {
                    if (button == null) continue;
                    var rect = (RectTransform)button.transform;
                    if (button == collapseButton)
                    {
                        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
                        rect.anchoredPosition = new Vector2(-metrics.Units(4), -metrics.Units(4));
                        rect.sizeDelta = Vector2.one * metrics.Units(48);
                        button.transform.Find("Label")?.gameObject.SetActive(false);
                        AnimalCafe.UI.P8R.P8RButtonLayout.IconButton(button);
                    }
                    else if (button == pickUpPointButton)
                    {
                        if (pickupInHeader)
                        {
                            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                            rect.anchoredPosition = new Vector2(padding + (dualHeaderActions ? metrics.Units(188.015625f) : 0), 0);
                            rect.sizeDelta = new Vector2(metrics.Units(140), metrics.Units(48));
                        }
                        else
                        {
                            rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(1, 0); rect.pivot = new Vector2(.5f, 0);
                            rect.anchoredPosition = new Vector2(0, gap);
                            rect.sizeDelta = new Vector2(-padding * 2, metrics.Units(48));
                        }
                        var icon = button.transform.Find("Icon")?.GetComponent<Image>();
                        if (icon != null)
                        {
                            appearance.Paint(icon, "pickup" + (button.interactable ? "_cocoa" : "_muted"), false);
                            icon.gameObject.SetActive(true); icon.raycastTarget = false;
                        }
                        AnimalCafe.UI.P8R.P8RButtonLayout.TextButton(button);
                    }
                    else
                    {
                        rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0); rect.pivot = Vector2.one * .5f;
                        rect.sizeDelta = new Vector2(Mathf.Min(width - padding * 2, metrics.Units(240)), metrics.Units(48));
                        AnimalCafe.UI.P8R.P8RButtonLayout.TextButton(button);
                    }
                }
                if (tabs != null && collapsedHandleButton != null)
                {
                    var handle = (RectTransform)collapsedHandleButton.transform; var row = (RectTransform)tabs.transform;
                    handle.anchoredPosition = new Vector2(0, row.anchoredPosition.y - gap - handle.rect.height * .5f);
                    var handleBottom = side + gap * 2 + footerHeight;
                    collapsedAnchoredPosition = expandedAnchoredPosition + new Vector2(0,
                        SheetState == DecorationSheetState.TabsOnly ? side - row.anchoredPosition.y
                            : handleBottom - (handle.anchoredPosition.y - handle.rect.height * .5f));
                    if (IsCollapsed && transitionCoroutine == null)
                    {
                        root.anchoredPosition = collapsedAnchoredPosition;
                        if (surfaceFooterHost != null && !FooterScrolls) surfaceFooterHost.anchoredPosition =
                            surfaceFooterExpandedAnchoredPosition + expandedAnchoredPosition - collapsedAnchoredPosition;
                    }
                }
                var contextHeight = 0f;
                if (editingContextRoot != null)
                {
                    var context = (RectTransform)editingContextRoot.transform;
                    var returnWidth = metrics.Units(180);
                    if (editingContextLabel != null && editingContextLabel.gameObject.activeSelf)
                    {
                        editingContextLabel.fontSize = metrics.Units(16);
                        editingContextLabel.rectTransform.offsetMin = new Vector2(gap, gap);
                        editingContextLabel.rectTransform.offsetMax = new Vector2(-returnWidth - gap * 2, -gap);
                        contextHeight = Mathf.Max(metrics.Units(48), editingContextLabel.GetPreferredValues(
                            editingContextLabel.text, Mathf.Max(metrics.Units(48), width - padding * 2 - returnWidth - gap * 3), Mathf.Infinity).y + gap * 2);
                    }
                    else contextHeight = metrics.Units(48);
                    if (returnToEditingButton != null)
                    {
                        var target = (RectTransform)returnToEditingButton.transform;
                        target.anchorMin = target.anchorMax = new Vector2(1, .5f); target.pivot = new Vector2(1, .5f);
                        target.anchoredPosition = new Vector2(-gap, 0);
                        target.sizeDelta = new Vector2(returnWidth, metrics.Units(48));
                        AnimalCafe.UI.P8R.P8RButtonLayout.TextButton(returnToEditingButton);
                    }
                    context.anchorMin = new Vector2(0, 1);
                    context.anchorMax = new Vector2(inlineContext ? 0 : 1, 1);
                    context.offsetMin = inlineContext ? new Vector2(padding, -metrics.Units(48))
                        : new Vector2(padding, -header - contextHeight);
                    context.offsetMax = inlineContext ? new Vector2(padding + returnWidth + gap, 0)
                        : new Vector2(-padding, -header);
                }
                if (verticalScroll != null)
                {
                    var viewport = (RectTransform)verticalScroll.transform;
                    var host = viewport.parent as RectTransform;
                    if (host != null)
                    {
                        host.offsetMax = new Vector2(-padding - (asideFooter ? asideWidth + gap : 0),
                            -header - (hasContext && !inlineContext && !scrollContext ? contextHeight + contextGap : 0));
                        host.offsetMin = new Vector2(padding, asideFooter ? surfaceGap : fixedBottomReservation);
                    }
                    viewport.offsetMin = viewport.offsetMax = Vector2.zero;
                }
                SetScrollingTool(surfaceFooterHost, scrollFooter, footerHeight);
                SetScrollingTool(editingContextRoot != null ? (RectTransform)editingContextRoot.transform : null,
                    scrollContext, contextHeight);
                SetScrollingTool(pickUpPointButton != null ? (RectTransform)pickUpPointButton.transform : null,
                    scrollPickup, metrics.Units(48));
                RefreshMobileCategoryRows(hideFloorHeadingForCap
                    || asideFooter && height - header - surfaceGap < metrics.Units(80), firstHeadingHeight);
                RefreshOverflowIndicator(Vector2.zero);
            }
            finally { refreshingP8RLayout = false; }
        }

        private Image mobileOverflowIndicator;
        private void RefreshMobileCategoryRows(bool hideRepeatedFloorHeading = false, float firstHeadingHeight = 28f)
        {
            var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
            foreach (var row in categoryRows)
            {
                var headingHeight = row == categoryRows[0] ? firstHeadingHeight : 28f;
                var scroll = row.HorizontalScroll; if (scroll == null) continue;
                var element = scroll.GetComponent<LayoutElement>() ?? scroll.gameObject.AddComponent<LayoutElement>();
                element.minHeight = element.preferredHeight = metrics.Units(hideRepeatedFloorHeading ? 84 : 88 + headingHeight);
                var label = scroll.GetComponentInChildren<TMP_Text>(true);
                if (label != null && label.name == "CategoryLabel")
                {
                    label.gameObject.SetActive(!hideRepeatedFloorHeading);
                    label.fontSize = metrics.Units(14);
                    label.rectTransform.offsetMin = new Vector2(0, -metrics.Units(headingHeight));
                    label.rectTransform.offsetMax = Vector2.zero;
                }
                if (scroll.viewport != null) scroll.viewport.offsetMax = new Vector2(scroll.viewport.offsetMax.x,
                    -metrics.Units(hideRepeatedFloorHeading ? 0 : headingHeight));
                if (scroll.content != null)
                {
                    var layout = scroll.content.GetComponent<HorizontalLayoutGroup>();
                    if (layout != null)
                    {
                        layout.spacing = metrics.Units(8); layout.childControlWidth = true; layout.childControlHeight = true;
                        layout.childForceExpandWidth = false; layout.childForceExpandHeight = false;
                    }
                    foreach (var tile in scroll.content.GetComponentsInChildren<DecorationCatalogueTileView>())
                    {
                        var item = tile.GetComponent<LayoutElement>() ?? tile.gameObject.AddComponent<LayoutElement>();
                        item.minWidth = item.preferredWidth = metrics.Units(68);
                        item.minHeight = item.preferredHeight = metrics.Units(84);
                        tile.RefreshMobileLayout();
                    }
                }
            }
            if (categoryContent != null)
            {
                var layout = categoryContent.GetComponent<VerticalLayoutGroup>(); if (layout != null) layout.spacing = metrics.Units(8);
                LayoutRebuilder.ForceRebuildLayoutImmediate(categoryContent);
            }
        }

        private void RefreshOverflowIndicator(Vector2 ignored)
        {
            if (appearance == null || verticalScroll == null || verticalScroll.viewport == null || verticalScroll.content == null) return;
            if (mobileOverflowIndicator == null)
            {
                var indicator = new GameObject("P8RVerticalOverflow", typeof(RectTransform), typeof(Image));
                indicator.transform.SetParent(verticalScroll.transform, false);
                mobileOverflowIndicator = indicator.GetComponent<Image>();
                mobileOverflowIndicator.color = new Color(.35f, .275f, .227f, .28f);
                mobileOverflowIndicator.raycastTarget = false;
            }
            var viewport = verticalScroll.viewport;
            var overflow = verticalScroll.content.rect.height > viewport.rect.height + .5f;
            mobileOverflowIndicator.gameObject.SetActive(overflow && SheetState == DecorationSheetState.Expanded);
            if (!overflow) return;
            var metrics = AnimalCafe.UI.P8R.P8RMobileMetrics.For(this);
            var height = Mathf.Clamp(viewport.rect.height * viewport.rect.height / verticalScroll.content.rect.height,
                metrics.Units(20), viewport.rect.height);
            var rect = mobileOverflowIndicator.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(metrics.Units(2), height);
            rect.anchoredPosition = new Vector2(-metrics.Units(2), -(viewport.rect.height - height) * (1 - verticalScroll.verticalNormalizedPosition));
        }

        private void RefreshCollapsedHandleLabel()
        {
            if (collapsedHandleLabel == null && collapsedHandleButton != null)
            {
                collapsedHandleLabel = collapsedHandleButton.transform.Find("Label")?.GetComponent<TMP_Text>()
                    ?? collapsedHandleButton.GetComponentInChildren<TMP_Text>(true);
                if (collapsedHandleLabel != null) originalCollapsedHandleText = collapsedHandleLabel.text;
            }
            if (collapsedHandleLabel != null)
                collapsedHandleLabel.text = handleHasActivePreview || !string.IsNullOrEmpty(editingContextMessage)
                    ? originalCollapsedHandleText : appearance != null ? appearance.Text("catalogue.add") : "继续添加";
            if (appearance != null) AnimalCafe.UI.P8R.P8RButtonLayout.TextButton(collapsedHandleButton);
        }

        private void HandleReturnToEditingRequested()
        {
            if (SheetState == DecorationSheetState.Expanded && !string.IsNullOrEmpty(editingContextMessage)
                && editingContextCanReturn && IsEligibleButton(returnToEditingButton))
                ReturnToEditingRequested?.Invoke();
        }

        public void Configure(
            IUiPointerOwnershipRegistrar registrar,
            UiTransitionRunner runner)
        {
            pointerBoundary = registrar ?? throw new ArgumentNullException(nameof(registrar));
            transitionRunner = runner ?? throw new ArgumentNullException(nameof(runner));
            EnsureOwnListeners();
            ConfigurePointerHooks(gameObject);
            RefreshCollapsedHandleLabel();
            foreach (var tile in tilePool)
            {
                tile.Configure(pointerBoundary);
            }
        }

        public void Bind(DecorationCatalogueAsset catalogue)
        {
            if (catalogue == null)
            {
                throw new ArgumentNullException(nameof(catalogue));
            }

            if (tileTemplate == null || contentRoot == null)
            {
                throw new InvalidOperationException("Catalogue prefab references are incomplete.");
            }

            EnsureOwnListeners();
            foreach (var tile in tilePool)
            {
                tile.Clear();
                tile.gameObject.SetActive(false);
            }

            var entries = catalogue.Entries;
            for (var index = 0; index < entries.Count; index++)
            {
                var tile = GetOrCreateTile(index);
                tile.gameObject.SetActive(true);
                if (pointerBoundary != null)
                {
                    tile.Configure(pointerBoundary);
                }

                PositionTile(tile, index, entries.Count);
                tile.Bind(entries[index], HandleTileSelected);
            }
        }

        public void ShowCatalogue()
        {
            // The scroll viewport fills the Sheet, so keep the explicit collapse
            // control above it in the real GraphicRaycaster order.
            collapseButton?.transform.SetAsLastSibling();
            var sheetChanged = SheetState != DecorationSheetState.Expanded;
            SheetState = DecorationSheetState.Expanded;
            RefreshEditingContext();
            if (sheetActionRoot != null) sheetActionRoot.SetActive(true);
            TransitionTo(DecorationCatalogueState.Expanded, forceTransition: sheetChanged);
        }

        public void ShowCollapsedHandle()
        {
            RefreshCollapsedHandleLabel();
            transform.SetAsLastSibling();
            collapsedHandleButton?.transform.SetAsLastSibling();
            TransitionTo(DecorationCatalogueState.Collapsed);
        }

        public void Hide()
        {
            TransitionTo(DecorationCatalogueState.Hidden);
        }

        private DecorationCatalogueTileView GetOrCreateTile(int index)
        {
            if (index < tilePool.Count)
            {
                return tilePool[index];
            }

            var tile = Instantiate(tileTemplate, contentRoot);
            tile.name = "CatalogueTile_" + (index + 1);
            tilePool.Add(tile);
            return tile;
        }

        private void PositionTile(DecorationCatalogueTileView tile, int index, int tileCount)
        {
            var rect = tile.GetComponent<RectTransform>();
            var contentRect = contentRoot as RectTransform;
            if (rect == null || contentRect == null)
            {
                return;
            }

            var totalHeight = rect.rect.height * tileCount
                + (TileVerticalStep - rect.rect.height) * Mathf.Max(0, tileCount - 1);
            var bottomInset = Mathf.Max(0f, (contentRect.rect.height - totalHeight) * 0.5f);
            var position = rect.anchoredPosition;
            position.y = bottomInset + (tileCount - 1 - index) * TileVerticalStep;
            rect.anchoredPosition = position;
        }

        private void HandleTileSelected(FurnitureDefinitionAsset definition)
        {
            if (IsCatalogueVisible && definition != null)
            {
                Selected?.Invoke(definition);
            }
        }

        private void EnsureOwnListeners()
        {
            if (pickUpPointButton != null)
            {
                pickUpPointButton.onClick.RemoveListener(HandlePickUpPointRequested);
                pickUpPointButton.onClick.AddListener(HandlePickUpPointRequested);
            }
            if (collapseButton != null)
            {
                collapseButton.onClick.RemoveListener(HandleCollapseRequested);
                collapseButton.onClick.AddListener(HandleCollapseRequested);
            }

            if (collapsedHandleButton != null)
            {
                collapsedHandleButton.onClick.RemoveListener(HandleExpandRequested);
                collapsedHandleButton.onClick.AddListener(HandleExpandRequested);
            }
        }

        private void HandlePickUpPointRequested()
        {
            if (!IsCatalogueVisible
                || IsCollapsed
                || SheetState != DecorationSheetState.Expanded
                || !IsEligibleButton(pickUpPointButton))
            {
                return;
            }

            PickUpPointRequested?.Invoke();
        }

        private static bool IsFurnitureTab(IReadOnlyList<DecorationCategoryModel> categories)
        {
            if (categories == null) return false;
            for (var index = 0; index < categories.Count; index++)
            {
                var categoryId = categories[index]?.CategoryId;
                if (categoryId == "furniture"
                    || categoryId == "cash-register"
                    || categoryId == "coffee-machine")
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleCollapseRequested()
        {
            if (!IsCatalogueVisible
                || IsCollapsed
                || !IsEligibleButton(collapseButton))
            {
                return;
            }

            ShowCollapsedHandle();
        }

        private void HandleExpandRequested()
        {
            if (!IsCatalogueVisible
                || !IsCollapsed
                || !IsEligibleButton(collapsedHandleButton))
            {
                return;
            }

            ShowCatalogue();
        }

        private bool IsEligibleButton(Button target)
        {
            return isActiveAndEnabled
                && gameObject.activeInHierarchy
                && target != null
                && target.isActiveAndEnabled
                && target.gameObject.activeInHierarchy
                && target.interactable;
        }

        private void ConfigurePointerHooks(GameObject root)
        {
            var hooks = root.GetComponentsInChildren<DecorationPointerBoundaryEventHook>(true);
            foreach (var hook in hooks)
            {
                hook.Configure(pointerBoundary);
            }
        }

        private void OnEnable()
        {
            AnimalCafe.UI.P8R.P8RMobileMetrics.Changed += RefreshP8RLayout;
            if (verticalScroll != null) verticalScroll.onValueChanged.AddListener(RefreshOverflowIndicator);
            ApplyResponsiveExpandedBounds();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyResponsiveExpandedBounds();
        }

        private void OnDisable()
        {
            AnimalCafe.UI.P8R.P8RMobileMetrics.Changed -= RefreshP8RLayout;
            if (verticalScroll != null) verticalScroll.onValueChanged.RemoveListener(RefreshOverflowIndicator);
            RememberBrowsingPosition();
            CancelBrowsingRestore();
            StopBrowsingMotion();
        }

        private void ApplyResponsiveExpandedBounds()
        {
            if (appearance != null) { RefreshP8RLayout(); return; }
            if (transform is not RectTransform rootRect
                || expandedRoot == null
                || expandedRoot.transform is not RectTransform expandedRect)
            {
                return;
            }

            var rightInset = rootRect.rect.width > rootRect.rect.height
                ? LandscapeRightRailInset
                : CatalogueSideInset;
            var minimum = expandedRect.offsetMin;
            var maximum = expandedRect.offsetMax;
            if (Mathf.Approximately(minimum.x, CatalogueSideInset)
                && Mathf.Approximately(maximum.x, -rightInset))
            {
                return;
            }

            minimum.x = CatalogueSideInset;
            maximum.x = -rightInset;
            expandedRect.offsetMin = minimum;
            expandedRect.offsetMax = maximum;
        }

        private void SetInteraction(bool enabled)
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.blocksRaycasts = enabled;
            canvasGroup.interactable = enabled;
        }

        private void TransitionTo(DecorationCatalogueState state, bool forceTransition = false)
        {
            if (state != DecorationCatalogueState.Expanded) EndNestedDrag();
            if (!forceTransition && state != DecorationCatalogueState.Hidden && State == state)
            {
                // Reasserting the same visible state must not restart the 0.16s tween.
                // 同一 visible state 重复调用必须幂等，避免 Confirm 后按钮短暂失去 raycast。
                if (transitionCoroutine == null)
                {
                    IsCatalogueVisible = true;
                    IsCollapsed = state == DecorationCatalogueState.Collapsed;
                    SetInteraction(true);
                }
                return;
            }

            State = state;
            IsCatalogueVisible = state != DecorationCatalogueState.Hidden;
            IsCollapsed = state == DecorationCatalogueState.Collapsed;
            var restoreTools = state != DecorationCatalogueState.Expanded && scrollingToolParents.Count > 0;
            if (restoreTools) RestoreScrollingTools();
            expandedRoot?.SetActive(state == DecorationCatalogueState.Expanded);
            collapsedRoot?.SetActive(state == DecorationCatalogueState.Collapsed);
            if (restoreTools) RefreshP8RLayout();
            SetInteraction(IsCatalogueVisible);
            BeginTransition(state);
            StateChanged?.Invoke(state);
        }

        private void BeginTransition(DecorationCatalogueState state)
        {
            var targetPosition = state switch
            {
                DecorationCatalogueState.Expanded => expandedAnchoredPosition,
                DecorationCatalogueState.Collapsed => collapsedAnchoredPosition,
                _ => hiddenAnchoredPosition
            };
            var footerTargetPosition = surfaceFooterExpandedAnchoredPosition
                + expandedAnchoredPosition
                - targetPosition;
            var visible = state != DecorationCatalogueState.Hidden;
            if (canvasGroup == null || transitionRunner == null || !isActiveAndEnabled)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = visible ? 1f : 0f;
                }

                if (transform is RectTransform immediateRect)
                {
                    immediateRect.anchoredPosition = targetPosition;
                }
                if (surfaceFooterHost != null && !FooterScrolls)
                {
                    surfaceFooterHost.anchoredPosition = footerTargetPosition;
                }
                IsCollapsed = state == DecorationCatalogueState.Collapsed;
                RefreshP8RLayout();
                PresentationSettled?.Invoke();
                return;
            }

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            transitionCoroutine = StartCoroutine(RunTransition(
                state,
                visible,
                targetPosition,
                footerTargetPosition));
        }

        private IEnumerator RunTransition(
            DecorationCatalogueState targetState,
            bool visible,
            Vector2 targetPosition,
            Vector2 footerTargetPosition)
        {
            var duration = transitionRunner.ResolveDuration(
                TransitionDuration, isEssential: false);
            var rect = transform as RectTransform;
            var startPosition = rect != null ? rect.anchoredPosition : Vector2.zero;
            var footerStartPosition = surfaceFooterHost != null
                ? surfaceFooterHost.anchoredPosition
                : Vector2.zero;
            var startAlpha = canvasGroup.alpha;
            var targetAlpha = visible ? 1f : 0f;
            if (duration <= 0f)
            {
                if (rect != null)
                {
                    rect.anchoredPosition = targetPosition;
                }
                if (surfaceFooterHost != null && !FooterScrolls)
                {
                    surfaceFooterHost.anchoredPosition = footerTargetPosition;
                }
                canvasGroup.alpha = targetAlpha;
                IsCollapsed = targetState == DecorationCatalogueState.Collapsed;
                SetInteraction(visible);
                transitionCoroutine = null;
                RefreshP8RLayout();
                PresentationSettled?.Invoke();
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                if (rect != null)
                {
                    rect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, t);
                }
                if (surfaceFooterHost != null && !FooterScrolls)
                {
                    surfaceFooterHost.anchoredPosition = Vector2.Lerp(
                        footerStartPosition,
                        footerTargetPosition,
                        t);
                }
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            if (rect != null)
            {
                rect.anchoredPosition = targetPosition;
            }
            if (surfaceFooterHost != null && !FooterScrolls)
            {
                surfaceFooterHost.anchoredPosition = footerTargetPosition;
            }
            canvasGroup.alpha = targetAlpha;
            IsCollapsed = targetState == DecorationCatalogueState.Collapsed;
            SetInteraction(visible);
            transitionCoroutine = null;
            // Safe-area/instruction geometry may change while this tween holds an older target.
            // 动画期间若安全区或指引改变，结束时按当前布局重算，不覆盖成旧footer位置。
            RefreshP8RLayout();
            PresentationSettled?.Invoke();
        }

        private void OnDestroy()
        {
            CancelBrowsingRestore();
            StopBrowsingMotion();
            collapseButton?.onClick.RemoveListener(HandleCollapseRequested);
            collapsedHandleButton?.onClick.RemoveListener(HandleExpandRequested);
            pickUpPointButton?.onClick.RemoveListener(HandlePickUpPointRequested);
        }
    }
}

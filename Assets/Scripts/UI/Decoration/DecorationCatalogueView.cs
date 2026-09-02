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
        [SerializeField] private ScrollRect verticalScroll;
        [SerializeField] private RectTransform categoryContent;
        [SerializeField] private GameObject categoryRowTemplate;
        [SerializeField] private DecorationCatalogueTileView categoryTileTemplate;
        private readonly List<DecorationCategoryRowView> categoryRows = new List<DecorationCategoryRowView>();
        private ScrollRect nestedDragSource;
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
            SheetState = hasActivePreview && state == DecorationSheetState.TabsOnly ? DecorationSheetState.CompactPreview : state;
            expandedRoot?.SetActive(SheetState == DecorationSheetState.Expanded);
            collapsedRoot?.SetActive(SheetState == DecorationSheetState.CompactPreview);
            sheetActionRoot?.SetActive(SheetState != DecorationSheetState.TabsOnly
                && SheetState != DecorationSheetState.Hidden);
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
            nestedDragSource = sourceRow;
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
            EndNestedDrag();
            if (categoryContent != null)
                foreach (Transform child in categoryContent)
                    if (child.gameObject != categoryRowTemplate)
                    {
                        child.gameObject.SetActive(false);
                        Destroy(child.gameObject);
                    }
            categoryRows.Clear();
            if (verticalScroll != null) { verticalScroll.vertical = true; verticalScroll.horizontal = false; }
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
                ConfigureNestedPointerDrag(row, scroll);
                var label = row.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = category.DisplayName;
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
            }
            if (categoryContent != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(categoryContent);
            }
            if (verticalScroll != null)
            {
                verticalScroll.StopMovement();
                verticalScroll.verticalNormalizedPosition = 1f;
            }
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

        public event Action<FurnitureDefinitionAsset> Selected;
        public event Action<DecorationCatalogueState> StateChanged;

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

        public void Configure(
            IUiPointerOwnershipRegistrar registrar,
            UiTransitionRunner runner)
        {
            pointerBoundary = registrar ?? throw new ArgumentNullException(nameof(registrar));
            transitionRunner = runner ?? throw new ArgumentNullException(nameof(runner));
            EnsureOwnListeners();
            ConfigurePointerHooks(gameObject);
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

            for (var index = 0; index < catalogue.Entries.Count; index++)
            {
                var tile = GetOrCreateTile(index);
                tile.gameObject.SetActive(true);
                if (pointerBoundary != null)
                {
                    tile.Configure(pointerBoundary);
                }

                PositionTile(tile, index, catalogue.Entries.Count);
                tile.Bind(catalogue.Entries[index], HandleTileSelected);
            }
        }

        public void ShowCatalogue()
        {
            // The scroll viewport fills the Sheet, so keep the explicit collapse
            // control above it in the real GraphicRaycaster order.
            collapseButton?.transform.SetAsLastSibling();
            TransitionTo(DecorationCatalogueState.Expanded);
        }

        public void ShowCollapsedHandle()
        {
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
            ApplyResponsiveExpandedBounds();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplyResponsiveExpandedBounds();
        }

        private void OnDisable()
        {
            EndNestedDrag();
        }

        private void ApplyResponsiveExpandedBounds()
        {
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

        private void TransitionTo(DecorationCatalogueState state)
        {
            if (state != DecorationCatalogueState.Hidden && State == state)
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
            expandedRoot?.SetActive(state == DecorationCatalogueState.Expanded);
            collapsedRoot?.SetActive(state == DecorationCatalogueState.Collapsed);
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
                if (surfaceFooterHost != null)
                {
                    surfaceFooterHost.anchoredPosition = footerTargetPosition;
                }
                IsCollapsed = state == DecorationCatalogueState.Collapsed;

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
                if (surfaceFooterHost != null)
                {
                    surfaceFooterHost.anchoredPosition = footerTargetPosition;
                }
                canvasGroup.alpha = targetAlpha;
                IsCollapsed = targetState == DecorationCatalogueState.Collapsed;
                SetInteraction(visible);
                transitionCoroutine = null;
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
                if (surfaceFooterHost != null)
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
            if (surfaceFooterHost != null)
            {
                surfaceFooterHost.anchoredPosition = footerTargetPosition;
            }
            canvasGroup.alpha = targetAlpha;
            IsCollapsed = targetState == DecorationCatalogueState.Collapsed;
            SetInteraction(visible);
            transitionCoroutine = null;
        }

        private void OnDestroy()
        {
            EndNestedDrag();
            collapseButton?.onClick.RemoveListener(HandleCollapseRequested);
            collapsedHandleButton?.onClick.RemoveListener(HandleExpandRequested);
        }
    }
}

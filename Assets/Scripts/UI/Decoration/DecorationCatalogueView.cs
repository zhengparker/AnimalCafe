using System;
using System.Collections;
using System.Collections.Generic;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.UI.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.Decoration
{
    public enum DecorationCatalogueState
    {
        Hidden,
        Expanded,
        Collapsed
    }

    /// <summary>
    /// Presents the compact mobile catalogue and reuses a small tile pool.
    /// 显示 mobile catalogue，并重复使用小型 tile pool。
    /// </summary>
    public sealed class DecorationCatalogueView : MonoBehaviour
    {
        private const float TransitionDuration = 0.16f;
        private const float TileVerticalStep = 144f;
        private const float CatalogueSideInset = 24f;
        private const float LandscapeRightRailInset = 228f;

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private GameObject expandedRoot;
        [SerializeField] private GameObject collapsedRoot;
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
        public DecorationCatalogueState State { get; private set; } =
            DecorationCatalogueState.Hidden;
        public RectTransform CollapsedHandleRect =>
            collapsedRoot != null ? collapsedRoot.transform as RectTransform : null;

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
            TransitionTo(DecorationCatalogueState.Expanded);
        }

        public void ShowCollapsedHandle()
        {
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

                return;
            }

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            transitionCoroutine = StartCoroutine(RunTransition(visible, targetPosition));
        }

        private IEnumerator RunTransition(bool visible, Vector2 targetPosition)
        {
            var duration = transitionRunner.ResolveDuration(
                TransitionDuration, isEssential: false);
            var rect = transform as RectTransform;
            var startPosition = rect != null ? rect.anchoredPosition : Vector2.zero;
            var startAlpha = canvasGroup.alpha;
            var targetAlpha = visible ? 1f : 0f;
            if (duration <= 0f)
            {
                if (rect != null)
                {
                    rect.anchoredPosition = targetPosition;
                }
                canvasGroup.alpha = targetAlpha;
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
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                yield return null;
            }

            if (rect != null)
            {
                rect.anchoredPosition = targetPosition;
            }
            canvasGroup.alpha = targetAlpha;
            transitionCoroutine = null;
        }

        private void OnDestroy()
        {
            collapseButton?.onClick.RemoveListener(HandleCollapseRequested);
            collapsedHandleButton?.onClick.RemoveListener(HandleExpandRequested);
        }
    }
}

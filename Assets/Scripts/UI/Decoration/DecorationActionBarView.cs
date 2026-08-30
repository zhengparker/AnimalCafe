using System;
using System.Collections;
using AnimalCafe.Decoration;
using AnimalCafe.UI.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.Decoration
{
    public enum DecorationActionPresentation
    {
        New,
        Existing
    }

    /// <summary>
    /// Owns the four decoration actions and their shared completion latch.
    /// 管理四个 decoration action 及共用的一次性 completion latch。
    /// </summary>
    public sealed class DecorationActionBarView : MonoBehaviour
    {
        private const float TransitionDuration = 0.12f;
        private const float ToastTransitionDuration = 0.16f;
        private const float ToastStayDuration = 1.8f;
        private const float ToastHiddenOffset = 28f;
        private const float CompactPanelWidth = 160f;
        private const float StorePanelWidth = 216f;
        private const float CompactButtonSize = 48f;
        private const float SurfaceButtonHeight = 52f;
        private const float SurfaceUtilityButtonWidth = 104f;
        private const float SurfacePrimaryButtonWidth = 136f;
        private const float FloorActionRowOffset = -32f;
        private const float ActionSpacing = 8f;

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform presentationRoot;
        [SerializeField] private Button storeButton;
        [SerializeField] private Button undoLastButton;
        [SerializeField] private Button applyAllButton;
        [SerializeField] private Button rotateButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text feedbackLabel;
        [SerializeField] private GameObject feedbackStateShape;
        [SerializeField] private RectTransform feedbackRoot;
        [SerializeField] private CanvasGroup feedbackCanvasGroup;
        [SerializeField] private bool useReadableActionLabels;

        private UiTransitionRunner transitionRunner;
        private Coroutine transitionCoroutine;
        private Coroutine feedbackCoroutine;
        private Vector2 feedbackVisiblePosition;
        private bool feedbackPositionInitialized;
        private bool canStore;
        private bool canConfirm;
        private bool terminalConsumed;
        private bool usesSurfaceFooterPresentation;
        private DecorationModeKind currentMode = DecorationModeKind.Furniture;

        public event Action RotateRequested;
        public event Action UndoLastRequested;
        public event Action ApplyAllRequested;
        public event Action ConfirmRequested;
        public event Action CancelRequested;
        public event Action StoreRequested;

        public bool IsVisible { get; private set; }
        public bool HasOverflowActions => false;
        public string[] VisibleActionLabels { get; private set; } = Array.Empty<string>();
        public void AttachToHost(RectTransform host)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (transform.parent != host)
            {
                transform.SetParent(host, false);
            }

            if (transform is RectTransform rect)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = Vector2.one * 0.5f;
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = Vector2.zero;
                rect.localScale = Vector3.one;
            }
        }

        public void SetModeActions(DecorationModeKind mode, bool existing)
        {
            currentMode = mode;
            IsVisible = true;
            terminalConsumed = false;
            EnsureOwnListeners();
            var labels = mode == DecorationModeKind.Floor ? new[] { "Undo Last", "Rotate", "Apply All", "Cancel", "Confirm" } :
                mode == DecorationModeKind.Wall ? new[] { "Cancel", "Confirm" } :
                mode == DecorationModeKind.Furniture ? (existing ? new[] { "Store", "Cancel", "Rotate", "Confirm" } : new[] { "Cancel", "Rotate", "Confirm" }) :
                (existing ? new[] { "Store", "Cancel", "Confirm" } : new[] { "Cancel", "Confirm" });
            VisibleActionLabels = labels;
            Set(undoLastButton, Array.IndexOf(labels, "Undo Last") >= 0); Set(applyAllButton, Array.IndexOf(labels, "Apply All") >= 0);
            Set(storeButton, Array.IndexOf(labels, "Store") >= 0); Set(rotateButton, Array.IndexOf(labels, "Rotate") >= 0);
            Set(cancelButton, true); Set(confirmButton, true);
            ApplyModePresentation(mode, existing);
        }

        public void SetFloorUtilityActionsEnabled(bool enabled)
        {
            SetInteractable(undoLastButton, enabled);
            SetInteractable(rotateButton, enabled);
            SetInteractable(applyAllButton, enabled);
        }

        private static void Set(Button button, bool visible) { if (button != null) button.gameObject.SetActive(visible); }
        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        public void SetPresentation(
            DecorationActionPresentation presentation,
            Vector2 preferredScreenPoint,
            Rect safeArea)
        {
            if (usesSurfaceFooterPresentation)
            {
                return;
            }

            var showStore = presentation == DecorationActionPresentation.Existing && canStore;
            if (storeButton != null)
            {
                storeButton.gameObject.SetActive(showStore);
            }

            var rect = presentationRoot != null
                ? presentationRoot
                : transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            ApplyCompactPresentation(rect, currentMode, showStore);

            var localPreferred = preferredScreenPoint;
            var localSafeArea = safeArea;
            if (rect.parent is RectTransform parentRect)
            {
                var canvas = rect.GetComponentInParent<Canvas>();
                var eventCamera = canvas != null
                    && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.rootCanvas.worldCamera
                    : null;
                if (TryScreenToAnchoredPoint(
                        rect, parentRect, eventCamera, preferredScreenPoint, out var anchoredPreferred)
                    && TryScreenToAnchoredPoint(
                        rect, parentRect, eventCamera, safeArea.min, out var anchoredMinimum)
                    && TryScreenToAnchoredPoint(
                        rect, parentRect, eventCamera, safeArea.max, out var anchoredMaximum))
                {
                    localPreferred = anchoredPreferred;
                    localSafeArea = Rect.MinMaxRect(
                        Mathf.Min(anchoredMinimum.x, anchoredMaximum.x),
                        Mathf.Min(anchoredMinimum.y, anchoredMaximum.y),
                        Mathf.Max(anchoredMinimum.x, anchoredMaximum.x),
                        Mathf.Max(anchoredMinimum.y, anchoredMaximum.y));
                }
            }

            var size = rect.rect.size;
            if (size.x <= 0f || size.y <= 0f)
            {
                size = rect.sizeDelta;
            }
            rect.anchoredPosition = new Vector2(
                ClampAxis(
                    localPreferred.x,
                    localSafeArea.xMin + size.x * rect.pivot.x,
                    localSafeArea.xMax - size.x * (1f - rect.pivot.x)),
                ClampAxis(
                    localPreferred.y,
                    localSafeArea.yMin + size.y * rect.pivot.y,
                    localSafeArea.yMax - size.y * (1f - rect.pivot.y)));
        }

        private void ApplyModePresentation(DecorationModeKind mode, bool existing)
        {
            usesSurfaceFooterPresentation = mode == DecorationModeKind.Floor
                || mode == DecorationModeKind.Wall;
            var panel = presentationRoot != null
                ? presentationRoot
                : transform as RectTransform;
            if (panel == null)
            {
                return;
            }

            panel.anchorMin = Vector2.one * 0.5f;
            panel.anchorMax = Vector2.one * 0.5f;
            panel.pivot = Vector2.one * 0.5f;
            panel.anchoredPosition = Vector2.zero;
            panel.localScale = Vector3.one;

            if (usesSurfaceFooterPresentation)
            {
                ApplySurfaceFooterPresentation(panel, mode);
                return;
            }

            ApplyCompactPresentation(panel, mode, existing);
        }

        private void ApplySurfaceFooterPresentation(RectTransform panel, DecorationModeKind mode)
        {
            var isFloor = mode == DecorationModeKind.Floor;
            panel.anchoredPosition = new Vector2(0f, isFloor ? FloorActionRowOffset : 0f);
            SetActionSibling(undoLastButton, 0);
            SetActionSibling(rotateButton, isFloor ? 1 : 0);
            SetActionSibling(applyAllButton, 2);
            SetActionSibling(cancelButton, isFloor ? 3 : 0);
            SetActionSibling(confirmButton, isFloor ? 4 : 1);

            ConfigureSurfaceButton(undoLastButton, "Undo Last", SurfaceUtilityButtonWidth, false);
            ConfigureSurfaceButton(rotateButton, "Rotate", SurfaceUtilityButtonWidth, false);
            ConfigureSurfaceButton(applyAllButton, "Apply All", SurfaceUtilityButtonWidth, false);
            ConfigureSurfaceButton(cancelButton, "Cancel", SurfacePrimaryButtonWidth, false);
            ConfigureSurfaceButton(confirmButton, "Confirm", SurfacePrimaryButtonWidth, true);

            var visibleButtonCount = isFloor ? 5 : 2;
            var width = isFloor
                ? SurfaceUtilityButtonWidth * 3f + SurfacePrimaryButtonWidth * 2f
                  + ActionSpacing * (visibleButtonCount - 1)
                : SurfacePrimaryButtonWidth * 2f + ActionSpacing;
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, SurfaceButtonHeight);
        }

        private void ApplyReadableFloatingPresentation(
            RectTransform panel,
            DecorationModeKind mode,
            bool showStore)
        {
            SetFloorUtilityActionsEnabled(true);
            ConfigureSurfaceButton(storeButton, "Store", SurfaceUtilityButtonWidth, false);
            ConfigureSurfaceButton(rotateButton, "Rotate", SurfaceUtilityButtonWidth, false);
            ConfigureSurfaceButton(cancelButton, "Cancel", SurfacePrimaryButtonWidth, false);
            ConfigureSurfaceButton(confirmButton, "Confirm", SurfacePrimaryButtonWidth, true);

            var ordered = new System.Collections.Generic.List<Button>(4);
            if (showStore && storeButton != null && storeButton.gameObject.activeSelf)
            {
                ordered.Add(storeButton);
            }
            if (cancelButton != null && cancelButton.gameObject.activeSelf)
            {
                ordered.Add(cancelButton);
            }
            if (mode == DecorationModeKind.Furniture
                && rotateButton != null
                && rotateButton.gameObject.activeSelf)
            {
                ordered.Add(rotateButton);
            }
            if (confirmButton != null && confirmButton.gameObject.activeSelf)
            {
                ordered.Add(confirmButton);
            }

            for (var index = 0; index < ordered.Count; index++)
            {
                SetActionSibling(ordered[index], index);
            }

            var width = 0f;
            foreach (var button in ordered)
            {
                width += button == cancelButton || button == confirmButton
                    ? SurfacePrimaryButtonWidth
                    : SurfaceUtilityButtonWidth;
            }
            if (ordered.Count > 1)
            {
                width += ActionSpacing * (ordered.Count - 1);
            }
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, SurfaceButtonHeight);
        }

        private void ApplyCompactPresentation(
            RectTransform panel,
            DecorationModeKind mode,
            bool showStore)
        {
            SetFloorUtilityActionsEnabled(true);
            ConfigureCompactButton(storeButton, "□");
            ConfigureCompactButton(rotateButton, "R");
            ConfigureCompactButton(cancelButton, "×");
            ConfigureCompactButton(confirmButton, "✓");
            ConfigureCompactButton(undoLastButton, "Undo");
            ConfigureCompactButton(applyAllButton, "All");

            var ordered = new System.Collections.Generic.List<Button>(4);
            if (showStore && storeButton != null && storeButton.gameObject.activeSelf)
            {
                ordered.Add(storeButton);
            }
            if (cancelButton != null && cancelButton.gameObject.activeSelf)
            {
                ordered.Add(cancelButton);
            }
            if (mode == DecorationModeKind.Furniture
                && rotateButton != null
                && rotateButton.gameObject.activeSelf)
            {
                ordered.Add(rotateButton);
            }
            if (confirmButton != null && confirmButton.gameObject.activeSelf)
            {
                ordered.Add(confirmButton);
            }

            for (var index = 0; index < ordered.Count; index++)
            {
                SetActionSibling(ordered[index], index);
            }

            panel.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                ordered.Count * CompactButtonSize + Mathf.Max(0, ordered.Count - 1) * ActionSpacing);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, CompactButtonSize);
        }

        private static void ConfigureSurfaceButton(
            Button button,
            string label,
            float width,
            bool primary)
        {
            if (button == null)
            {
                return;
            }

            var rect = button.transform as RectTransform;
            if (rect != null)
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, SurfaceButtonHeight);
            }
            if (button.image != null)
            {
                var normalColor = primary
                    ? new Color(0.28f, 0.43f, 0.31f, 1f)
                    : new Color(1f, 0.91f, 0.72f, 1f);
                button.image.color = Color.white;
                var colors = button.colors;
                colors.normalColor = normalColor;
                colors.highlightedColor = primary
                    ? new Color(0.33f, 0.49f, 0.36f, 1f)
                    : new Color(1f, 0.94f, 0.80f, 1f);
                colors.pressedColor = primary
                    ? new Color(0.22f, 0.36f, 0.26f, 1f)
                    : new Color(0.91f, 0.80f, 0.61f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(0.55f, 0.54f, 0.50f, 1f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.1f;
                button.colors = colors;
            }

            var text = FindPrimaryLabel(button);
            if (text != null)
            {
                StretchPrimaryLabel(text);
                text.text = label;
                text.fontSize = 16f;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Truncate;
                text.color = primary
                    ? new Color(1f, 0.97f, 0.90f, 1f)
                    : new Color(0.22f, 0.16f, 0.11f, 1f);
            }
            SetTooltipVisible(button, false);
        }

        private static void ConfigureCompactButton(Button button, string label)
        {
            if (button == null)
            {
                return;
            }

            var rect = button.transform as RectTransform;
            if (rect != null)
            {
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, CompactButtonSize);
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, CompactButtonSize);
            }
            if (button.image != null)
            {
                var normalColor = new Color(0.28f, 0.43f, 0.31f, 1f);
                button.image.color = Color.white;
                var colors = button.colors;
                colors.normalColor = normalColor;
                colors.highlightedColor = new Color(0.33f, 0.49f, 0.36f, 1f);
                colors.pressedColor = new Color(0.22f, 0.36f, 0.26f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(0.55f, 0.54f, 0.50f, 1f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = 0.1f;
                button.colors = colors;
            }

            var text = FindPrimaryLabel(button);
            if (text != null)
            {
                text.text = label;
                text.fontSize = 14f;
                text.enableWordWrapping = false;
                text.overflowMode = TextOverflowModes.Truncate;
                text.color = new Color(1f, 0.97f, 0.90f, 1f);
            }
            SetTooltipEnabled(button, true);
        }

        private static TMP_Text FindPrimaryLabel(Button button)
        {
            return button != null
                ? button.transform.Find("Label")?.GetComponent<TMP_Text>()
                : null;
        }

        private static void StretchPrimaryLabel(TMP_Text text)
        {
            if (text == null || text.rectTransform == null)
            {
                return;
            }

            var rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void SetTooltipVisible(Button button, bool visible)
        {
            SetTooltipEnabled(button, visible);
            var tooltip = button != null ? button.transform.Find("Tooltip") : null;
            if (tooltip != null)
            {
                tooltip.gameObject.SetActive(visible);
            }
        }

        private static void SetTooltipEnabled(Button button, bool enabled)
        {
            if (button == null)
            {
                return;
            }

            foreach (var hook in button.GetComponentsInChildren<DecorationPointerBoundaryEventHook>(true))
            {
                hook.SetTooltipEnabled(enabled);
            }
        }

        private static bool TryScreenToAnchoredPoint(
            RectTransform rect,
            RectTransform parentRect,
            UnityEngine.Camera eventCamera,
            Vector2 screenPoint,
            out Vector2 anchoredPoint)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, screenPoint, eventCamera, out var localPoint))
            {
                anchoredPoint = default;
                return false;
            }

            var anchor = new Vector2(
                Mathf.Lerp(rect.anchorMin.x, rect.anchorMax.x, rect.pivot.x),
                Mathf.Lerp(rect.anchorMin.y, rect.anchorMax.y, rect.pivot.y));
            var anchorReference = Vector2.Scale(parentRect.rect.size, anchor)
                + parentRect.rect.min;
            anchoredPoint = localPoint - anchorReference;
            return true;
        }

        private static float ClampAxis(float value, float minimum, float maximum)
        {
            return minimum <= maximum
                ? Mathf.Clamp(value, minimum, maximum)
                : (minimum + maximum) * 0.5f;
        }

        public void Configure(
            IUiPointerOwnershipRegistrar pointerBoundary,
            UiTransitionRunner runner)
        {
            if (pointerBoundary == null)
            {
                throw new ArgumentNullException(nameof(pointerBoundary));
            }

            transitionRunner = runner ?? throw new ArgumentNullException(nameof(runner));
            EnsureOwnListeners();
            foreach (var hook in GetComponentsInChildren<DecorationPointerBoundaryEventHook>(true))
            {
                hook.Configure(pointerBoundary);
            }
        }

        public void Show(
            bool canStore,
            bool canConfirm,
            PlacementFeedbackKey feedback)
        {
            transform.SetAsLastSibling();
            EnsureOwnListeners();
            this.canStore = canStore;
            this.canConfirm = canConfirm;
            terminalConsumed = false;
            IsVisible = true;
            if (storeButton != null)
            {
                storeButton.gameObject.SetActive(canStore);
                storeButton.interactable = canStore;
            }

            if (!usesSurfaceFooterPresentation && presentationRoot != null)
            {
                ApplyCompactPresentation(presentationRoot, currentMode, canStore);
            }

            if (confirmButton != null)
            {
                confirmButton.interactable = canConfirm;
            }

            PresentFeedback(feedback);
            SetInteraction(true);
            BeginTransition(visible: true);
        }

        public void Hide()
        {
            IsVisible = false;
            HideFeedbackImmediately();
            SetInteraction(false);
            BeginTransition(visible: false);
        }

        private void PresentFeedback(PlacementFeedbackKey feedback)
        {
            var text = GetFeedbackText(feedback);
            if (feedbackLabel != null)
            {
                feedbackLabel.text = text;
            }

            feedbackStateShape?.SetActive(feedback != PlacementFeedbackKey.None);
            if (feedbackRoot == null || feedbackCanvasGroup == null)
            {
                return;
            }

            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
                feedbackCoroutine = null;
            }

            if (feedback == PlacementFeedbackKey.None)
            {
                HideFeedbackImmediately();
                return;
            }

            if (!feedbackPositionInitialized)
            {
                feedbackVisiblePosition = feedbackRoot.anchoredPosition;
                feedbackPositionInitialized = true;
            }
            feedbackCoroutine = StartCoroutine(ShowFeedbackToast());
        }

        private IEnumerator ShowFeedbackToast()
        {
            feedbackRoot.gameObject.SetActive(true);
            feedbackCanvasGroup.blocksRaycasts = false;
            feedbackCanvasGroup.interactable = false;
            var hiddenPosition = feedbackVisiblePosition + Vector2.up * ToastHiddenOffset;
            for (var elapsed = 0f; elapsed < ToastTransitionDuration;
                 elapsed += Time.unscaledDeltaTime)
            {
                var progress = Mathf.Clamp01(elapsed / ToastTransitionDuration);
                feedbackRoot.anchoredPosition = Vector2.Lerp(hiddenPosition, feedbackVisiblePosition, progress);
                feedbackCanvasGroup.alpha = progress;
                yield return null;
            }

            feedbackRoot.anchoredPosition = feedbackVisiblePosition;
            feedbackCanvasGroup.alpha = 1f;
            var stayElapsed = 0f;
            while (stayElapsed < ToastStayDuration)
            {
                stayElapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            feedbackCoroutine = null;
            HideFeedbackImmediately();
        }

        private void HideFeedbackImmediately()
        {
            if (feedbackCoroutine != null)
            {
                StopCoroutine(feedbackCoroutine);
                feedbackCoroutine = null;
            }

            if (feedbackCanvasGroup != null)
            {
                feedbackCanvasGroup.alpha = 0f;
                feedbackCanvasGroup.blocksRaycasts = false;
                feedbackCanvasGroup.interactable = false;
            }

            if (feedbackRoot != null)
            {
                if (feedbackPositionInitialized)
                {
                    feedbackRoot.anchoredPosition = feedbackVisiblePosition;
                }
                feedbackRoot.gameObject.SetActive(false);
            }
        }

        private void HandleRotate()
        {
            if (IsEligible() && !terminalConsumed)
            {
                RotateRequested?.Invoke();
            }
        }
        private void HandleUndoLast() { if (IsEligible() && !terminalConsumed) UndoLastRequested?.Invoke(); }
        private void HandleApplyAll() { if (IsEligible() && !terminalConsumed) ApplyAllRequested?.Invoke(); }

        private void HandleConfirm()
        {
            if (IsEligible() && canConfirm && !terminalConsumed)
            {
                terminalConsumed = true;
                ConfirmRequested?.Invoke();
            }
        }

        private void HandleCancel()
        {
            if (IsEligible() && !terminalConsumed)
            {
                terminalConsumed = true;
                CancelRequested?.Invoke();
            }
        }

        private void HandleStore()
        {
            if (IsEligible() && canStore && !terminalConsumed)
            {
                terminalConsumed = true;
                StoreRequested?.Invoke();
            }
        }

        private bool IsEligible()
        {
            return IsVisible && isActiveAndEnabled && gameObject.activeInHierarchy;
        }

        private static string GetFeedbackText(PlacementFeedbackKey feedback)
        {
            switch (feedback)
            {
                case PlacementFeedbackKey.None:
                    return string.Empty;
                case PlacementFeedbackKey.Occupied:
                    return "Space already occupied";
                case PlacementFeedbackKey.OutsideUnlockedArea:
                    return "Outside decoration area";
                case PlacementFeedbackKey.Locked:
                    return "Area not unlocked";
                case PlacementFeedbackKey.Blocked:
                    return "Furniture cannot be placed here";
                case PlacementFeedbackKey.EntranceClearance:
                    return "Keep the entrance clear";
                case PlacementFeedbackKey.UnsupportedSurface:
                    return "Furniture cannot stand here";
                case PlacementFeedbackKey.MissingInstance:
                    return "Furniture changed. Select it again.";
                case PlacementFeedbackKey.WallOverlap:
                    return "Wall space already occupied";
                case PlacementFeedbackKey.WallOutOfBounds:
                    return "Outside wall area";
                case PlacementFeedbackKey.WallCrossCorner:
                    return "Wall decor cannot cross a corner";
                case PlacementFeedbackKey.WallSurfaceMissing:
                    return "Wall surface unavailable";
                default:
                    return string.Empty;
            }
        }

        private void EnsureOwnListeners()
        {
            ReplaceListener(storeButton, HandleStore);
            ReplaceListener(undoLastButton, HandleUndoLast);
            ReplaceListener(applyAllButton, HandleApplyAll);
            ReplaceListener(rotateButton, HandleRotate);
            ReplaceListener(cancelButton, HandleCancel);
            ReplaceListener(confirmButton, HandleConfirm);
        }

        private static void ReplaceListener(Button target, UnityEngine.Events.UnityAction action)
        {
            if (target == null)
            {
                return;
            }

            target.onClick.RemoveListener(action);
            target.onClick.AddListener(action);
        }

        private static void SetActionSibling(Button button, int index)
        {
            if (button != null)
            {
                button.transform.SetSiblingIndex(index);
            }
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

        private void BeginTransition(bool visible)
        {
            if (canvasGroup == null || transitionRunner == null || !isActiveAndEnabled)
            {
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = visible ? 1f : 0f;
                }

                return;
            }

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
            }

            transitionCoroutine = StartCoroutine(RunTransition(visible));
        }

        private IEnumerator RunTransition(bool visible)
        {
            yield return transitionRunner.Run(canvasGroup, visible, TransitionDuration);
            transitionCoroutine = null;
        }

        private void OnDestroy()
        {
            HideFeedbackImmediately();
            storeButton?.onClick.RemoveListener(HandleStore);
            undoLastButton?.onClick.RemoveListener(HandleUndoLast);
            applyAllButton?.onClick.RemoveListener(HandleApplyAll);
            rotateButton?.onClick.RemoveListener(HandleRotate);
            cancelButton?.onClick.RemoveListener(HandleCancel);
            confirmButton?.onClick.RemoveListener(HandleConfirm);
        }
    }

}

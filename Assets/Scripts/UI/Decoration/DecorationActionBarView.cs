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

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform presentationRoot;
        [SerializeField] private Button storeButton;
        [SerializeField] private Button rotateButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text feedbackLabel;
        [SerializeField] private GameObject feedbackStateShape;
        [SerializeField] private RectTransform feedbackRoot;
        [SerializeField] private CanvasGroup feedbackCanvasGroup;

        private UiTransitionRunner transitionRunner;
        private Coroutine transitionCoroutine;
        private Coroutine feedbackCoroutine;
        private Vector2 feedbackVisiblePosition;
        private bool feedbackPositionInitialized;
        private bool canStore;
        private bool canConfirm;
        private bool terminalConsumed;

        public event Action RotateRequested;
        public event Action ConfirmRequested;
        public event Action CancelRequested;
        public event Action StoreRequested;

        public bool IsVisible { get; private set; }

        public void SetPresentation(
            DecorationActionPresentation presentation,
            Vector2 preferredScreenPoint,
            Rect safeArea)
        {
            var showStore = presentation == DecorationActionPresentation.Existing && canStore;
            if (storeButton != null)
            {
                storeButton.gameObject.SetActive(showStore);
                if (showStore)
                {
                    storeButton.transform.SetSiblingIndex(0);
                }
            }

            SetActionSibling(cancelButton, showStore ? 1 : 0);
            SetActionSibling(rotateButton, showStore ? 2 : 1);
            SetActionSibling(confirmButton, showStore ? 3 : 2);

            var rect = presentationRoot != null
                ? presentationRoot
                : transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            rect.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Horizontal,
                showStore ? StorePanelWidth : CompactPanelWidth);

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
                default:
                    return string.Empty;
            }
        }

        private void EnsureOwnListeners()
        {
            ReplaceListener(storeButton, HandleStore);
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
            rotateButton?.onClick.RemoveListener(HandleRotate);
            cancelButton?.onClick.RemoveListener(HandleCancel);
            confirmButton?.onClick.RemoveListener(HandleConfirm);
        }
    }

}

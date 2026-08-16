using System;
using System.Collections;
using System.Collections.Generic;
using AnimalCafe.UI.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnimalCafe.UI.Components
{
    /// <summary>
    /// Connects an ordinary Bottom Sheet to outside dismissal and shared Back.
    /// 将普通 Bottom Sheet 接入 outside dismiss 与 shared Back。
    /// </summary>
    public sealed class AnimalCafeBottomSheetView : MonoBehaviour, IPointerDownHandler
    {
        private UiNavigationCoordinator navigation;
        private UiView view;
        [SerializeField] private Button outsideButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;
        private Action onConfirm;
        private UiViewHandle navigationHandle;
        private UiPauseCoordinator pauseCoordinator;
        private UiPointerBoundary pointerBoundary;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform content;
        private UiTransitionRunner transitionRunner;
        private float transitionDuration;
        private IUiPauseHandle pauseHandle;
        private Coroutine transitionCoroutine;
        private bool immediateCloseRequested;
        private bool contentPositionCaptured;
        private Vector2 openContentPosition;
        private Vector2 closedContentPosition;
        private readonly HashSet<int> ownedPointerIds = new HashSet<int>();

        public void BindPrefabReferences(Button outside, CanvasGroup group)
        {
            outsideButton = outside ?? throw new ArgumentNullException(nameof(outside));
            canvasGroup = group ?? throw new ArgumentNullException(nameof(group));
        }

        public void BindActionReferences(Button cancel, Button confirm)
        {
            cancelButton = cancel ?? throw new ArgumentNullException(nameof(cancel));
            confirmButton = confirm ?? throw new ArgumentNullException(nameof(confirm));
        }

        public void ConfigureActions(Button cancel, Button confirm, Action confirmed)
        {
            RemoveActionListeners();
            BindActionReferences(cancel, confirm);
            onConfirm = confirmed;
            cancelButton.onClick.AddListener(HandleCancel);
            confirmButton.onClick.AddListener(HandleConfirm);
        }

        public void Configure(
            UiNavigationCoordinator coordinator,
            UiView bottomSheetView,
            Button outside)
        {
            CloseImmediate();
            outsideButton?.onClick.RemoveListener(HandleOutside);
            navigation = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            view = bottomSheetView ?? throw new ArgumentNullException(nameof(bottomSheetView));
            if (view.Kind != UiViewKind.BottomSheet)
            {
                throw new ArgumentException(
                    "Bottom Sheet component requires a BottomSheet UiView.", nameof(bottomSheetView));
            }

            outsideButton = outside ?? throw new ArgumentNullException(nameof(outside));
            outsideButton.onClick.AddListener(HandleOutside);
        }

        public void ConfigureLifecycle(
            UiPauseCoordinator pause,
            UiPointerBoundary boundary,
            CanvasGroup group,
            UiTransitionRunner runner,
            float duration)
        {
            CloseImmediate();
            pauseCoordinator = pause ?? throw new ArgumentNullException(nameof(pause));
            pointerBoundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
            canvasGroup = group ?? throw new ArgumentNullException(nameof(group));
            transitionRunner = runner ?? throw new ArgumentNullException(nameof(runner));
            if (duration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            transitionDuration = duration;
            ResolveContentPositions();
            SetClosedVisualState();
        }

        public void Open()
        {
            CloseImmediate();
            navigationHandle = navigation.OpenBottomSheet(
                view,
                HandleNavigationClosed,
                allowBack: true,
                allowOutside: view.OutsideDismissPolicy == UiOutsideDismissPolicy.Dismissible);
            pauseHandle = pauseCoordinator?.Acquire(view);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
                transitionCoroutine = StartCoroutine(RunTransition(visible: true));
            }
        }

        public bool TryHandleBack()
        {
            return navigation.TryHandleBack(view);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerBoundary == null || eventData == null)
            {
                return;
            }

            pointerBoundary.RegisterUiPointerPress(eventData.pointerId);
            ownedPointerIds.Add(eventData.pointerId);
        }

        private void HandleOutside()
        {
            navigation.RequestOutsideDismiss(view);
        }

        private void HandleCancel()
        {
            navigation.TryHandleBack(view);
        }

        private void HandleConfirm()
        {
            onConfirm?.Invoke();
            navigation.TryHandleBack(view);
        }

        private void RemoveActionListeners()
        {
            cancelButton?.onClick.RemoveListener(HandleCancel);
            confirmButton?.onClick.RemoveListener(HandleConfirm);
        }

        private void BeginClose()
        {
            StopTransition();
            if (canvasGroup == null || transitionRunner == null)
            {
                CloseImmediate();
                return;
            }

            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            transitionCoroutine = StartCoroutine(RunTransition(visible: false));
        }

        private IEnumerator RunTransition(bool visible)
        {
            ResolveContentPositions();
            var duration = transitionRunner.ResolveDuration(transitionDuration, isEssential: false);
            var startAlpha = canvasGroup.alpha;
            var targetAlpha = visible ? 1f : 0f;
            var startPosition = content != null ? content.anchoredPosition : Vector2.zero;
            var targetPosition = visible ? openContentPosition : closedContentPosition;
            if (duration <= 0f)
            {
                canvasGroup.alpha = targetAlpha;
                if (content != null) content.anchoredPosition = targetPosition;
            }
            else
            {
                var elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    var progress = Mathf.Clamp01(elapsed / duration);
                    var eased = progress * progress * (3f - 2f * progress);
                    canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
                    if (content != null)
                        content.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, eased);
                    yield return null;
                }

                canvasGroup.alpha = targetAlpha;
                if (content != null) content.anchoredPosition = targetPosition;
            }

            transitionCoroutine = null;
            if (!visible)
            {
                ReleaseOwnedResources();
                SetClosedVisualState();
            }
        }

        private void CloseImmediate()
        {
            StopTransition();
            immediateCloseRequested = true;
            var handle = navigationHandle;
            navigationHandle = null;
            handle?.Close();
            immediateCloseRequested = false;
            ReleaseOwnedResources();
            SetClosedVisualState();
        }

        private void HandleNavigationClosed()
        {
            navigationHandle = null;
            if (!immediateCloseRequested)
            {
                BeginClose();
            }
        }

        private void ReleaseOwnedResources()
        {
            pauseHandle?.Dispose();
            pauseHandle = null;
            if (pointerBoundary != null)
            {
                foreach (var pointerId in ownedPointerIds)
                {
                    pointerBoundary.ReleasePointer(pointerId);
                }
            }

            ownedPointerIds.Clear();
        }

        private void StopTransition()
        {
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }
        }

        private void SetClosedVisualState()
        {
            if (canvasGroup == null)
            {
                return;
            }

            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
            ResolveContentPositions();
            if (content != null) content.anchoredPosition = closedContentPosition;
        }

        private void ResolveContentPositions()
        {
            if (content == null)
                content = transform.Find("Content") as RectTransform;
            if (content == null || contentPositionCaptured)
                return;

            openContentPosition = content.anchoredPosition;
            var slideDistance = Mathf.Max(content.rect.height, 1f);
            closedContentPosition = openContentPosition + Vector2.down * slideDistance;
            contentPositionCaptured = true;
        }

        private void OnDisable()
        {
            CloseImmediate();
        }

        private void OnDestroy()
        {
            CloseImmediate();
            outsideButton?.onClick.RemoveListener(HandleOutside);
            RemoveActionListeners();
        }
    }
}

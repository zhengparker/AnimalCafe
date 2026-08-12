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
        private Button outsideButton;
        private UiViewHandle navigationHandle;
        private UiPauseCoordinator pauseCoordinator;
        private UiPointerBoundary pointerBoundary;
        private CanvasGroup canvasGroup;
        private UiTransitionRunner transitionRunner;
        private float transitionDuration;
        private IUiPauseHandle pauseHandle;
        private Coroutine transitionCoroutine;
        private readonly HashSet<int> ownedPointerIds = new HashSet<int>();

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
            SetClosedVisualState();
        }

        public void Open()
        {
            CloseImmediate();
            navigationHandle = navigation.OpenBottomSheet(view);
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
            if (!navigation.TryHandleBack())
            {
                return false;
            }

            if (view != null && !view.IsOpen)
            {
                BeginClose();
            }

            return true;
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
            navigation.RequestOutsideDismiss();
            if (view != null && !view.IsOpen)
            {
                BeginClose();
            }
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
            yield return transitionRunner.Run(canvasGroup, visible, transitionDuration);
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
            ReleaseOwnedResources();
            SetClosedVisualState();
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
            navigationHandle?.Close();
            navigationHandle = null;
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
        }

        private void OnDisable()
        {
            CloseImmediate();
        }

        private void OnDestroy()
        {
            CloseImmediate();
            outsideButton?.onClick.RemoveListener(HandleOutside);
        }
    }
}

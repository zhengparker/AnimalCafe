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
    /// Connects a Modal's real uGUI actions to the shared navigation stack.
    /// 将 Modal 的真实 uGUI action 接入 shared navigation stack。
    /// </summary>
    public sealed class AnimalCafeModalView : MonoBehaviour, IPointerDownHandler
    {
        private UiNavigationCoordinator navigation;
        private UiView view;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button outsideButton;
        private bool allowBack;
        private UiViewHandle navigationHandle;
        private UiPauseCoordinator pauseCoordinator;
        private UiPointerBoundary pointerBoundary;
        [SerializeField] private CanvasGroup canvasGroup;
        private UiTransitionRunner transitionRunner;
        private float openDuration;
        private IUiPauseHandle pauseHandle;
        private IDisposable sceneBlockHandle;
        private Coroutine transitionCoroutine;
        private readonly HashSet<int> ownedPointerIds = new HashSet<int>();
        private IUiPointerOwnershipRegistrar delayedPointerBoundary;
        private readonly HashSet<int> delayedPointerIds = new HashSet<int>();

        public void ConfigureDelayedPointerRelease(IUiPointerOwnershipRegistrar boundary)
        {
            if (boundary == null)
            {
                throw new ArgumentNullException(nameof(boundary));
            }

            ReleaseAllRetainedPointers();
            delayedPointerBoundary = boundary;
        }
        public void RetainPointerUntilGestureEnd(int pointerId)
        {
            if (delayedPointerBoundary == null) throw new InvalidOperationException("Configure delayed pointer release first.");
            delayedPointerBoundary.RegisterUiPointerPress(pointerId); delayedPointerIds.Add(pointerId);
        }
        public void ReleaseRetainedPointer(int pointerId)
        {
            if (delayedPointerIds.Remove(pointerId)) delayedPointerBoundary.ReleasePointer(pointerId);
        }
        public void ReleaseAllRetainedPointers()
        {
            if (delayedPointerBoundary != null)
                foreach (var pointerId in delayedPointerIds) delayedPointerBoundary.ReleasePointer(pointerId);
            delayedPointerIds.Clear();
        }

        public event Action Confirmed;

        public void BindPrefabReferences(Button confirm, Button cancel, Button outside, CanvasGroup group)
        {
            confirmButton = confirm ?? throw new ArgumentNullException(nameof(confirm));
            cancelButton = cancel ?? throw new ArgumentNullException(nameof(cancel));
            outsideButton = outside ?? throw new ArgumentNullException(nameof(outside));
            canvasGroup = group ?? throw new ArgumentNullException(nameof(group));
        }

        public void Configure(
            UiNavigationCoordinator coordinator,
            UiView modalView,
            Button confirm,
            Button cancel,
            Button outside,
            bool isBackDismissible)
        {
            Close();
            RemoveListeners();
            Confirmed = null;
            navigation = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            view = modalView ?? throw new ArgumentNullException(nameof(modalView));
            if (view.Kind != UiViewKind.Modal)
            {
                throw new ArgumentException("Modal component requires a Modal UiView.", nameof(modalView));
            }

            confirmButton = confirm ?? throw new ArgumentNullException(nameof(confirm));
            cancelButton = cancel ?? throw new ArgumentNullException(nameof(cancel));
            outsideButton = outside ?? throw new ArgumentNullException(nameof(outside));
            allowBack = isBackDismissible;
            confirmButton.onClick.AddListener(HandleConfirm);
            cancelButton.onClick.AddListener(HandleCancel);
            outsideButton.onClick.AddListener(HandleOutside);
        }

        public void Open()
        {
            navigationHandle?.Close();
            navigationHandle = navigation.PushModal(
                view,
                HandleNavigationClosed,
                allowBack,
                view.OutsideDismissPolicy == UiOutsideDismissPolicy.Dismissible);
            GetComponent<AnimalCafePanelView>()?.AcquireForOpenView();
            AcquireLifecycleResources();
        }

        public void ConfigureLifecycle(
            UiPauseCoordinator pause,
            UiPointerBoundary boundary,
            CanvasGroup group,
            UiTransitionRunner runner,
            float transitionDuration)
        {
            Close();
            pauseCoordinator = pause ?? throw new ArgumentNullException(nameof(pause));
            pointerBoundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
            canvasGroup = group ?? throw new ArgumentNullException(nameof(group));
            transitionRunner = runner ?? throw new ArgumentNullException(nameof(runner));
            if (transitionDuration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(transitionDuration));
            }

            openDuration = transitionDuration;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
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

        public bool TryHandleBack()
        {
            return allowBack && navigation.TryHandleBack(view);
        }

        private void HandleConfirm()
        {
            if (view == null || !view.IsOpen || !navigation.IsTopModal(view))
            {
                return;
            }

            Confirmed?.Invoke();
            Close();
        }

        private void HandleCancel()
        {
            if (view == null || !navigation.IsTopModal(view))
            {
                return;
            }

            Close();
        }

        private void HandleOutside()
        {
            if (view != null
                && navigation.IsTopModal(view)
                && view.OutsideDismissPolicy == UiOutsideDismissPolicy.Dismissible)
            {
                navigation.RequestOutsideDismiss(view);
            }
        }

        private void Close()
        {
            var handle = navigationHandle;
            navigationHandle = null;
            if (handle != null)
            {
                handle.Close();
                return;
            }

            CleanupPresentation();
        }

        private void HandleNavigationClosed()
        {
            navigationHandle = null;
            CleanupPresentation();
        }

        private void CleanupPresentation()
        {
            ReleaseLifecycleResources();
            GetComponent<AnimalCafePanelView>()?.ReleaseForClosedView();
        }

        private void AcquireLifecycleResources()
        {
            if (canvasGroup == null)
            {
                return;
            }

            ReleaseLifecycleResources();
            pauseHandle = pauseCoordinator.Acquire(view);
            sceneBlockHandle = pointerBoundary.AcquireSceneBlock();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
            transitionCoroutine = StartCoroutine(RunOpenTransition());
        }

        private IEnumerator RunOpenTransition()
        {
            yield return transitionRunner.Run(canvasGroup, visible: true, openDuration);
            transitionCoroutine = null;
        }

        private void ReleaseLifecycleResources()
        {
            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }

            pauseHandle?.Dispose();
            pauseHandle = null;
            sceneBlockHandle?.Dispose();
            sceneBlockHandle = null;
            if (pointerBoundary != null)
            {
                foreach (var pointerId in ownedPointerIds)
                {
                    pointerBoundary.ReleasePointer(pointerId);
                }
            }

            ownedPointerIds.Clear();
        }

        private void OnDisable()
        {
            Close();
        }

        private void OnDestroy()
        {
            Close();
            ReleaseAllRetainedPointers();
            RemoveListeners();
        }

        private void RemoveListeners()
        {
            confirmButton?.onClick.RemoveListener(HandleConfirm);
            cancelButton?.onClick.RemoveListener(HandleCancel);
            outsideButton?.onClick.RemoveListener(HandleOutside);
        }
    }
}

using System;
using AnimalCafe.Content;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.Decoration
{
    /// <summary>
    /// Adds decoration-specific copy and one-shot events around the shared modal.
    /// 在 shared modal 外层提供 decoration 文案与一次性事件。
    /// </summary>
    public sealed class DecorationStoreModalView : MonoBehaviour
    {
        private const float TransitionDuration = 0.16f;

        [SerializeField] private AnimalCafeModalView modalView;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button modalBlocker;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;

        private readonly UiView view = new UiView(
            "decoration.store-modal",
            UiViewKind.Modal,
            UiPausePolicy.ContinueGame,
            UiOutsideDismissPolicy.NotDismissible);
        private UiNavigationCoordinator navigation;
        private bool completionConsumed;

        public event Action ConfirmRequested;
        public event Action DismissRequested;

        public bool IsOpen => view.IsOpen;
        public RectTransform ContentRect => titleLabel != null
            ? titleLabel.transform.parent as RectTransform
            : null;

        public void Configure(
            UiNavigationCoordinator coordinator,
            UiPauseCoordinator pauseCoordinator,
            UiPointerBoundary pointerBoundary,
            UiTransitionRunner transitionRunner)
        {
            navigation = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            if (pauseCoordinator == null)
            {
                throw new ArgumentNullException(nameof(pauseCoordinator));
            }

            if (pointerBoundary == null)
            {
                throw new ArgumentNullException(nameof(pointerBoundary));
            }

            if (transitionRunner == null)
            {
                throw new ArgumentNullException(nameof(transitionRunner));
            }

            if (modalView == null
                || confirmButton == null
                || cancelButton == null
                || modalBlocker == null
                || canvasGroup == null)
            {
                throw new InvalidOperationException("Store modal prefab references are incomplete.");
            }

            // Register wrapper listeners first. The shared modal closes after our event fires.
            EnsureOwnListeners();
            modalView.Configure(
                navigation,
                view,
                confirmButton,
                cancelButton,
                modalBlocker,
                isBackDismissible: true);
            modalView.ConfigureLifecycle(
                pauseCoordinator,
                pointerBoundary,
                canvasGroup,
                transitionRunner,
                TransitionDuration);

            foreach (var hook in GetComponentsInChildren<DecorationPointerBoundaryEventHook>(true))
            {
                hook.Configure(pointerBoundary);
            }
        }

        public void Show(FurnitureDefinitionAsset definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            completionConsumed = false;
            if (titleLabel != null)
            {
                titleLabel.text = "Store furniture?";
            }

            if (bodyLabel != null)
            {
                bodyLabel.text =
                    "This removes it from the current layout. You can place it again from the catalogue.";
            }

            modalView.Open();
        }

        public void ShowWallMounted(WallMountedDefinitionAsset definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            completionConsumed = false;
            if (titleLabel != null)
            {
                titleLabel.text = "Store wall decoration?";
            }

            if (bodyLabel != null)
            {
                bodyLabel.text =
                    "This removes it from the current wall. You can place it again from the catalogue.";
            }

            modalView.Open();
        }

        public bool TryHandleBack()
        {
            if (!IsEligible() || completionConsumed)
            {
                return false;
            }

            completionConsumed = true;
            if (!modalView.TryHandleBack())
            {
                completionConsumed = false;
                return false;
            }

            DismissRequested?.Invoke();
            return true;
        }

        public void CloseForOwnerShutdown()
        {
            if (modalView != null && view.IsOpen)
            {
                // The shared modal's own handle can close a covered registration safely.
                // Toggling only that component invokes its protected lifecycle cleanup
                // without disabling this wrapper or affecting a covering modal.
                var wasEnabled = modalView.enabled;
                if (wasEnabled)
                {
                    modalView.enabled = false;
                    modalView.enabled = true;
                }
            }
        }

        private void HandleConfirm()
        {
            if (!IsEligible() || completionConsumed)
            {
                return;
            }

            completionConsumed = true;
            ConfirmRequested?.Invoke();
        }

        private void HandleCancel()
        {
            if (!IsEligible() || completionConsumed)
            {
                return;
            }

            completionConsumed = true;
            DismissRequested?.Invoke();
        }

        private bool IsEligible()
        {
            return navigation != null
                && isActiveAndEnabled
                && gameObject.activeInHierarchy
                && view.IsOpen
                && navigation.IsTopModal(view);
        }

        private void EnsureOwnListeners()
        {
            ReplaceListener(confirmButton, HandleConfirm);
            ReplaceListener(cancelButton, HandleCancel);
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

        private void OnDisable()
        {
            CloseForOwnerShutdown();
        }

        private void OnDestroy()
        {
            CloseForOwnerShutdown();
            confirmButton?.onClick.RemoveListener(HandleConfirm);
            cancelButton?.onClick.RemoveListener(HandleCancel);
        }
    }
}

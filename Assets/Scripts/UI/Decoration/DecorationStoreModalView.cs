using System;
using System.Collections;
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
        [SerializeField] private AnimalCafe.UI.P8R.P8RAppearance appearance;
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
        private Coroutine presentationClosedRoutine;
        private bool refreshingMobileLayout;
        private AnimalCafe.UI.P8R.P8RModalSafeAreaHost mobileSafeAreaHost;

        public event Action ConfirmRequested;
        public event Action DismissRequested;
        public event Action PresentationClosed;

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
            BindMobileSafeAreaHost();
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
            // These listeners run after the shared modal has actually closed.
            // 只通知展示层重算位置，不重复 Confirm/Cancel 的业务事件。
            CancelPresentationClosedNotification();
            ReplaceListener(confirmButton, QueuePresentationClosed);
            ReplaceListener(cancelButton, QueuePresentationClosed);

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
                titleLabel.text = appearance != null ? appearance.Text("store.title").Replace("{item}",
                    appearance.ItemName(definition, definition.DisplayName)) : "Store furniture?";
            }

            if (bodyLabel != null)
            {
                bodyLabel.text = appearance != null ? appearance.Text("store.body") :
                    "This removes it from the current layout. You can place it again from the catalogue.";
            }

            modalView.Open();
            RefreshP8RContentLayout();
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
                titleLabel.text = appearance != null ? appearance.Text("store.title").Replace("{item}",
                    appearance.ItemName(definition.DefinitionId, definition.DisplayName)) : "Store wall decoration?";
            }

            if (bodyLabel != null)
            {
                bodyLabel.text = appearance != null ? appearance.Text("store.body") :
                    "This removes it from the current wall. You can place it again from the catalogue.";
            }

            modalView.Open();
            RefreshP8RContentLayout();
        }

        public void ShowFunctionalSurface(DecorationCatalogueItemKind kind)
        {
            var title = kind switch
            {
                DecorationCatalogueItemKind.CashRegister => "Store cash register?",
                DecorationCatalogueItemKind.CoffeeMachine => "Store coffee machine?",
                DecorationCatalogueItemKind.PickUpPoint => "Store pick-up point?",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind,
                    "Store confirmation requires a functional surface item.")
            };
            completionConsumed = false;
            if (titleLabel != null)
            {
                titleLabel.text = appearance != null ? appearance.Text("store.title").Replace("{item}",
                    appearance.Text(kind == DecorationCatalogueItemKind.PickUpPoint ? "catalogue.pickup" :
                        kind == DecorationCatalogueItemKind.CashRegister ? "item.cash_register" : "item.coffee_machine")) : title;
            }
            if (bodyLabel != null)
            {
                bodyLabel.text = kind == DecorationCatalogueItemKind.PickUpPoint
                    ? "This removes the pick-up point from this surface. You can add it again with the Pick-up Point button."
                    : "This removes it from its current surface. You can place it again from the catalogue.";
                if (appearance != null) bodyLabel.text = appearance.Text(kind == DecorationCatalogueItemKind.PickUpPoint
                    ? "store.pickup_body" : "store.body");
            }

            modalView.Open();
            RefreshP8RContentLayout();
        }

        private void RefreshP8RContentLayout()
        {
            if (appearance == null || refreshingMobileLayout) return;
            refreshingMobileLayout = true;
            try { AnimalCafe.UI.P8R.P8RButtonLayout.Modal(ContentRect, titleLabel, bodyLabel, cancelButton, confirmButton); }
            finally { refreshingMobileLayout = false; }
        }

        private void BindMobileSafeAreaHost()
        {
            if (!Application.isPlaying || appearance == null || ContentRect == null) return;
            var area = ContentRect.GetComponentInParent<SafeAreaContainer>(true);
            if (area == null) return;
            var host = area.GetComponent<AnimalCafe.UI.P8R.P8RModalSafeAreaHost>()
                ?? area.gameObject.AddComponent<AnimalCafe.UI.P8R.P8RModalSafeAreaHost>();
            if (mobileSafeAreaHost != host)
            {
                UnbindMobileSafeAreaHost();
                mobileSafeAreaHost = host;
            }
            // Reuse the existing inner safe area; backdrop and input ownership stay on the root.
            // 只监听原有内部安全区，不新增容器、不移动card或全屏遮罩；重复Configure不累积回调。
            mobileSafeAreaHost.RectChanged -= HandleMobileSafeAreaChanged;
            if (isActiveAndEnabled) mobileSafeAreaHost.RectChanged += HandleMobileSafeAreaChanged;
        }

        private void UnbindMobileSafeAreaHost()
        {
            if (mobileSafeAreaHost != null) mobileSafeAreaHost.RectChanged -= HandleMobileSafeAreaChanged;
        }

        private void HandleMobileSafeAreaChanged()
        {
            if (this != null && isActiveAndEnabled && IsOpen) RefreshP8RContentLayout();
        }

        private void OnEnable()
        {
            AnimalCafe.UI.P8R.P8RMobileMetrics.Changed += RefreshP8RContentLayout;
            BindMobileSafeAreaHost();
        }
        private void OnRectTransformDimensionsChange() { if (IsOpen) RefreshP8RContentLayout(); }

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
            QueuePresentationClosed();
            return true;
        }

        public void CloseForOwnerShutdown()
        {
            CancelPresentationClosedNotification();
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

        private void QueuePresentationClosed()
        {
            if (appearance == null || !isActiveAndEnabled || IsOpen || !completionConsumed) return;
            CancelPresentationClosedNotification();
            presentationClosedRoutine = StartCoroutine(NotifyPresentationClosedNextFrame());
        }

        private IEnumerator NotifyPresentationClosedNextFrame()
        {
            yield return null;
            presentationClosedRoutine = null;
            if (isActiveAndEnabled && !IsOpen) PresentationClosed?.Invoke();
        }

        private void CancelPresentationClosedNotification()
        {
            if (presentationClosedRoutine == null) return;
            StopCoroutine(presentationClosedRoutine);
            presentationClosedRoutine = null;
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
            AnimalCafe.UI.P8R.P8RMobileMetrics.Changed -= RefreshP8RContentLayout;
            UnbindMobileSafeAreaHost();
            CloseForOwnerShutdown();
        }

        private void OnDestroy()
        {
            UnbindMobileSafeAreaHost();
            CloseForOwnerShutdown();
            confirmButton?.onClick.RemoveListener(HandleConfirm);
            cancelButton?.onClick.RemoveListener(HandleCancel);
            confirmButton?.onClick.RemoveListener(QueuePresentationClosed);
            cancelButton?.onClick.RemoveListener(QueuePresentationClosed);
        }
    }
}

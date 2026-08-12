using System;
using AnimalCafe.Core.Time;
using AnimalCafe.Input;
using AnimalCafe.Interaction;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Feedback;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnimalCafe.UI.Foundation
{
    /// <summary>
    /// Runtime wiring for the isolated Phase 5 manual-review scene.
    /// 仅负责 Phase 5 validation scene 的可执行人工验收流程。
    /// </summary>
    public sealed class Phase5UiFoundationReviewController : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera sceneCamera;
        [SerializeField] private MouseCameraInput sceneInput;
        [SerializeField] private SceneInteractionController sceneInteraction;
        [SerializeField] private Transform selectableWorldTarget;
        [SerializeField] private Button worldOcclusionButton;
        [SerializeField] private Button[] pointerOwningButtons;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button reducedMotionButton;
        [SerializeField] private TMP_Text reducedMotionStatus;
        [SerializeField] private Button secondStrongButton;
        [SerializeField] private GameObject secondStrongFixture;
        [SerializeField] private Button openModalButton;
        [SerializeField] private AnimalCafeModalView modal;
        [SerializeField] private Button openBottomSheetButton;
        [SerializeField] private AnimalCafeBottomSheetView bottomSheet;
        [SerializeField] private Button validationRepairButton;
        [SerializeField] private ValidationMessageView validation;
        [SerializeField] private Button safeAreaConfirmButton;
        [SerializeField] private TMP_Text safeAreaStatus;
        [SerializeField] private Button showSolidButton;
        [SerializeField] private Button showLightButton;
        [SerializeField] private Button showStrongButton;
        [SerializeField] private Button forceFallbackButton;
        [SerializeField] private GameObject solidPanel;
        [SerializeField] private GameObject lightPanel;
        [SerializeField] private GameObject strongPanel;
        [SerializeField] private Button handleBackButton;
        [SerializeField] private Button openSecondModalButton;
        [SerializeField] private AnimalCafeModalView secondModal;
        [SerializeField] private Button toastBurstButton;
        [SerializeField] private TMP_Text toastBurstStatus;
        [SerializeField] private Phase5UiFoundationFeedbackController feedback;
        [SerializeField] private Button longPressTooltipButton;
        [SerializeField] private Button closeTooltipButton;
        [SerializeField] private TooltipView tooltip;
        [SerializeField] private Button interruptAndReopenButton;

        private UiPointerBoundary pointerBoundary;
        private UiPauseCoordinator pauseCoordinator;
        private IUiPauseHandle pauseHandle;
        private UiTransitionRunner transitionRunner;
        private UiNavigationCoordinator navigation;
        private bool reducedMotion;
        private bool backHeld;

        public int BackRequestCount { get; private set; }
        public string LastBackTrace { get; private set; } = string.Empty;

        public void Configure(
            UnityEngine.Camera camera,
            MouseCameraInput input,
            SceneInteractionController interaction,
            Transform worldTarget,
            Button occlusionButton,
            Button[] owningButtons,
            Button pause,
            Button resume,
            Button reduced,
            TMP_Text reducedStatus,
            Button secondStrong,
            GameObject secondStrongPanel,
            Button modalButton,
            AnimalCafeModalView modalView,
            Button sheetButton,
            AnimalCafeBottomSheetView sheetView,
            Button repair,
            ValidationMessageView validationView,
            Button safeConfirm,
            TMP_Text safeStatus,
            Button solidButton,
            Button lightButton,
            Button strongButton,
            Button fallbackButton,
            GameObject solidFixture,
            GameObject lightFixture,
            GameObject strongFixture,
            Button backButton,
            Button secondModalButton,
            AnimalCafeModalView secondModalView,
            Button burstButton,
            TMP_Text burstStatus,
            Phase5UiFoundationFeedbackController feedbackController,
            Button longPressButton,
            Button tooltipCloseButton,
            TooltipView tooltipView,
            Button interruptButton)
        {
            sceneCamera = camera;
            sceneInput = input;
            sceneInteraction = interaction;
            selectableWorldTarget = worldTarget;
            worldOcclusionButton = occlusionButton;
            pointerOwningButtons = owningButtons;
            pauseButton = pause;
            continueButton = resume;
            reducedMotionButton = reduced;
            reducedMotionStatus = reducedStatus;
            secondStrongButton = secondStrong;
            secondStrongFixture = secondStrongPanel;
            openModalButton = modalButton;
            modal = modalView;
            openBottomSheetButton = sheetButton;
            bottomSheet = sheetView;
            validationRepairButton = repair;
            validation = validationView;
            safeAreaConfirmButton = safeConfirm;
            safeAreaStatus = safeStatus;
            showSolidButton = solidButton;
            showLightButton = lightButton;
            showStrongButton = strongButton;
            forceFallbackButton = fallbackButton;
            solidPanel = solidFixture;
            lightPanel = lightFixture;
            strongPanel = strongFixture;
            handleBackButton = backButton;
            openSecondModalButton = secondModalButton;
            secondModal = secondModalView;
            toastBurstButton = burstButton;
            toastBurstStatus = burstStatus;
            feedback = feedbackController;
            longPressTooltipButton = longPressButton;
            closeTooltipButton = tooltipCloseButton;
            tooltip = tooltipView;
            interruptAndReopenButton = interruptButton;
        }

        private void Awake()
        {
            ConfigureSharedBoundary();
            navigation = new UiNavigationCoordinator();
            var timeService = GetComponent<GameTimeService>() ?? gameObject.AddComponent<GameTimeService>();
            pauseCoordinator = new UiPauseCoordinator(timeService);
            transitionRunner = new UiTransitionRunner(() => reducedMotion);
            ConfigureModal();
            ConfigureSecondModal();
            ConfigureBottomSheet();
            BindButtons();
            UpdateStatuses();
        }

        private void ConfigureSharedBoundary()
        {
            pointerBoundary = new UiPointerBoundary();
            sceneInteraction.Configure(sceneCamera, sceneInput, pointerBoundary);
            foreach (var button in pointerOwningButtons ?? Array.Empty<Button>())
            {
                if (button == null) continue;
                var hook = button.GetComponent<UiPointerBoundaryEventHook>()
                    ?? button.gameObject.AddComponent<UiPointerBoundaryEventHook>();
                hook.Configure(pointerBoundary);
            }
        }

        private void Start() => AlignOcclusionButton();

        private void Update()
        {
            var isBackPressed = Keyboard.current != null && Keyboard.current.escapeKey.isPressed;
            if (isBackPressed && !backHeld) HandleBack();
            backHeld = isBackPressed;
        }

        private void ConfigureModal()
        {
            if (modal == null) return;
            var buttons = modal.GetComponentsInChildren<Button>(true);
            var confirm = buttons.FirstOrDefaultNamed("ConfirmButton");
            var cancel = buttons.FirstOrDefaultNamed("CancelButton");
            var outside = buttons.FirstOrDefaultNamed("Blocker");
            var group = modal.GetComponent<CanvasGroup>();
            var view = new UiView("validation-modal", UiViewKind.Modal,
                UiPausePolicy.PauseGame, UiOutsideDismissPolicy.NotDismissible);
            modal.Configure(navigation, view, confirm, cancel, outside, true);
            modal.ConfigureLifecycle(pauseCoordinator, pointerBoundary, group, transitionRunner, 0.15f);
        }

        private void ConfigureSecondModal()
        {
            if (secondModal == null) return;
            var buttons = secondModal.GetComponentsInChildren<Button>(true);
            var view = new UiView("validation-second-modal", UiViewKind.Modal,
                UiPausePolicy.PauseGame, UiOutsideDismissPolicy.Dismissible);
            secondModal.Configure(navigation, view,
                buttons.FirstOrDefaultNamed("ConfirmButton"),
                buttons.FirstOrDefaultNamed("CancelButton"),
                buttons.FirstOrDefaultNamed("Blocker"), true);
            secondModal.ConfigureLifecycle(pauseCoordinator, pointerBoundary,
                secondModal.GetComponent<CanvasGroup>(), transitionRunner, 0.15f);
        }

        private void ConfigureBottomSheet()
        {
            if (bottomSheet == null) return;
            var outside = bottomSheet.transform.Find("OutsideButton").GetComponent<Button>();
            var group = bottomSheet.GetComponent<CanvasGroup>();
            var view = new UiView("validation-sheet", UiViewKind.BottomSheet,
                UiPausePolicy.ContinueGame, UiOutsideDismissPolicy.Dismissible);
            bottomSheet.Configure(navigation, view, outside);
            bottomSheet.ConfigureLifecycle(pauseCoordinator, pointerBoundary, group, transitionRunner, 0.15f);
        }

        private void BindButtons()
        {
            pauseButton.onClick.AddListener(Pause);
            continueButton.onClick.AddListener(Continue);
            reducedMotionButton.onClick.AddListener(ToggleReducedMotion);
            secondStrongButton.onClick.AddListener(OpenSecondStrong);
            openModalButton.onClick.AddListener(OpenModal);
            openBottomSheetButton.onClick.AddListener(OpenBottomSheet);
            validationRepairButton.onClick.AddListener(RepairValidation);
            safeAreaConfirmButton.onClick.AddListener(ConfirmSafeArea);
            showSolidButton.onClick.AddListener(() => ShowPanel(solidPanel));
            showLightButton.onClick.AddListener(() => ShowPanel(lightPanel));
            showStrongButton.onClick.AddListener(() => ShowPanel(strongPanel));
            forceFallbackButton.onClick.AddListener(OpenSecondStrong);
            handleBackButton.onClick.AddListener(HandleBack);
            openSecondModalButton.onClick.AddListener(OpenSecondModal);
            toastBurstButton.onClick.AddListener(ShowToastBurst);
            closeTooltipButton.onClick.AddListener(() => tooltip.Close());
            interruptAndReopenButton.onClick.AddListener(InterruptAndReopen);
            longPressTooltipButton.gameObject.AddComponent<Phase5UiFoundationLongPressTooltipTrigger>()
                .Configure(tooltip, 0.5f);
        }

        private void Pause()
        {
            pauseHandle ??= pauseCoordinator.Acquire(new UiView(
                "validation-pause", UiViewKind.MainPanel,
                UiPausePolicy.PauseGame, UiOutsideDismissPolicy.NotDismissible));
        }

        private void Continue()
        {
            pauseHandle?.Dispose();
            pauseHandle = null;
        }

        private void ToggleReducedMotion()
        {
            reducedMotion = !reducedMotion;
            UpdateStatuses();
        }

        private void OpenSecondStrong() => secondStrongFixture.SetActive(true);

        private void OpenModal() => modal.Open();

        private void OpenBottomSheet()
        {
            bottomSheet.gameObject.SetActive(true);
            bottomSheet.Open();
            foreach (var graphic in bottomSheet.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.enabled) continue;
                graphic.enabled = false;
                graphic.enabled = true;
                graphic.SetAllDirty();
            }
            Canvas.ForceUpdateCanvases();
        }

        private void RepairValidation() => validation.SetValidationResult(true, string.Empty);

        private void ConfirmSafeArea()
        {
            safeAreaStatus.text = "Safe Area: Confirmed";
        }

        private void ShowPanel(GameObject selected)
        {
            solidPanel.SetActive(selected == solidPanel);
            lightPanel.SetActive(selected == lightPanel);
            strongPanel.SetActive(selected == strongPanel);
        }

        private void HandleBack()
        {
            BackRequestCount++;
            var primaryBefore = modal != null && modal.GetComponent<CanvasGroup>().blocksRaycasts;
            var secondBefore = secondModal != null && secondModal.GetComponent<CanvasGroup>().blocksRaycasts;
            var handled = navigation.TryHandleBack();
            LastBackTrace = $"request={BackRequestCount} handled={handled} " +
                $"before(primary={primaryBefore},second={secondBefore}) " +
                $"after(primary={modal.GetComponent<CanvasGroup>().blocksRaycasts}," +
                $"second={secondModal.GetComponent<CanvasGroup>().blocksRaycasts})";
        }

        private void OpenSecondModal() => secondModal.Open();

        private void ShowToastBurst()
        {
            var mergedCount = feedback.ShowThreeToastBurstWithDuplicate();
            toastBurstStatus.text = $"3 Toasts, merged x{mergedCount}";
        }

        private void InterruptAndReopen() => StartCoroutine(InterruptAndReopenModal());

        private System.Collections.IEnumerator InterruptAndReopenModal()
        {
            modal.gameObject.SetActive(false);
            yield return null;
            modal.gameObject.SetActive(true);
            modal.Open();
        }

        private void UpdateStatuses()
        {
            reducedMotionStatus.text = "Reduced Motion: " + (reducedMotion ? "On" : "Off");
        }

        private void AlignOcclusionButton()
        {
            if (sceneCamera == null || selectableWorldTarget == null || worldOcclusionButton == null) return;
            var parent = (RectTransform)worldOcclusionButton.transform.parent;
            var screenPoint = sceneCamera.WorldToScreenPoint(selectableWorldTarget.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, null, out var localPoint);
            ((RectTransform)worldOcclusionButton.transform).anchoredPosition = localPoint;
        }

        private void OnDestroy()
        {
            Continue();
            pauseButton?.onClick.RemoveListener(Pause);
            continueButton?.onClick.RemoveListener(Continue);
            reducedMotionButton?.onClick.RemoveListener(ToggleReducedMotion);
            secondStrongButton?.onClick.RemoveListener(OpenSecondStrong);
            openModalButton?.onClick.RemoveListener(OpenModal);
            openBottomSheetButton?.onClick.RemoveListener(OpenBottomSheet);
            validationRepairButton?.onClick.RemoveListener(RepairValidation);
            safeAreaConfirmButton?.onClick.RemoveListener(ConfirmSafeArea);
        }

        private void OnDisable() => CleanupLifecycle();

        private void CleanupLifecycle()
        {
            Continue();
            if (modal != null) modal.gameObject.SetActive(false);
            if (secondModal != null) secondModal.gameObject.SetActive(false);
            if (bottomSheet != null) bottomSheet.gameObject.SetActive(false);
            if (sceneInteraction != null && sceneInput != null && sceneCamera != null)
                ConfigureSharedBoundary();
        }
    }

    public sealed class Phase5UiFoundationLongPressTooltipTrigger : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler
    {
        private TooltipView tooltip;
        private float threshold;
        private float pressedAt;

        public void Configure(TooltipView tooltipView, float seconds)
        {
            tooltip = tooltipView;
            threshold = seconds;
        }

        public void OnPointerDown(PointerEventData eventData) => pressedAt = Time.unscaledTime;

        public void OnPointerUp(PointerEventData eventData)
        {
            if (tooltip == null || Time.unscaledTime - pressedAt < threshold) return;
            tooltip.SetMessage("Long press reveals Touch-safe help.");
            tooltip.OnPointerClick(eventData);
        }
    }

    internal static class Phase5UiFoundationButtonLookup
    {
        public static Button FirstOrDefaultNamed(this Button[] buttons, string name) =>
            Array.Find(buttons, button => button.name == name)
            ?? throw new InvalidOperationException("Missing validation button: " + name);
    }
}

using System;
using AnimalCafe.UI.Feedback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.Foundation
{
    /// <summary>
    /// Scene-only evidence hooks for the Phase 5 validation gallery.
    /// Phase 5 validation gallery 的场景专用 evidence hooks。
    /// </summary>
    public sealed class Phase5UiFoundationFeedbackController : MonoBehaviour
    {
        [SerializeField] private Button showToastButton;
        [SerializeField] private Button showTooltipButton;
        [SerializeField] private Button showValidationButton;
        [SerializeField] private Button openBottomSheetButton;
        [SerializeField] private ToastView toast;
        [SerializeField] private TooltipView tooltip;
        [SerializeField] private ValidationMessageView validation;
        [SerializeField] private GameObject bottomSheet;
        private ToastQueue toastQueue;
        private bool isBound;

        public void Configure(
            Button toastButton,
            Button tooltipButton,
            Button validationButton,
            Button bottomSheetButton,
            ToastView toastView,
            TooltipView tooltipView,
            ValidationMessageView validationView,
            GameObject bottomSheetFixture)
        {
            showToastButton = toastButton ?? throw new ArgumentNullException(nameof(toastButton));
            showTooltipButton = tooltipButton ?? throw new ArgumentNullException(nameof(tooltipButton));
            showValidationButton = validationButton ?? throw new ArgumentNullException(nameof(validationButton));
            openBottomSheetButton = bottomSheetButton ?? throw new ArgumentNullException(nameof(bottomSheetButton));
            this.toast = toastView ?? throw new ArgumentNullException(nameof(toastView));
            tooltip = tooltipView ?? throw new ArgumentNullException(nameof(tooltipView));
            validation = validationView ?? throw new ArgumentNullException(nameof(validationView));
            bottomSheet = bottomSheetFixture ?? throw new ArgumentNullException(nameof(bottomSheetFixture));
            bottomSheet.SetActive(false);
            Bind();
        }

        private void Awake() => Bind();

        private void OnEnable() => Bind();

        private void OnDisable() => Unbind();

        private void OnDestroy()
        {
            Unbind();
        }

        private void Bind()
        {
            if (isBound || showToastButton == null || showTooltipButton == null
                || showValidationButton == null || openBottomSheetButton == null
                || toast == null || tooltip == null || validation == null || bottomSheet == null)
            {
                return;
            }

            var toastLabel = toast.GetComponentInChildren<TMP_Text>(true)
                ?? throw new InvalidOperationException("Toast fixture requires a TMP label.");
            toastQueue = new ToastQueue(() => Time.unscaledTime);
            toast.Configure(toastQueue, toastLabel, toast.GetComponentsInChildren<Graphic>(true));
            showToastButton.onClick.AddListener(ShowToast);
            showTooltipButton.onClick.AddListener(ShowTooltip);
            showValidationButton.onClick.AddListener(ShowValidationError);
            openBottomSheetButton.onClick.AddListener(OpenBottomSheet);
            isBound = true;
        }

        private void Unbind()
        {
            showToastButton?.onClick.RemoveListener(ShowToast);
            showTooltipButton?.onClick.RemoveListener(ShowTooltip);
            showValidationButton?.onClick.RemoveListener(ShowValidationError);
            openBottomSheetButton?.onClick.RemoveListener(OpenBottomSheet);
            isBound = false;
        }

        private void ShowToast() => toastQueue.Enqueue(new ToastMessage(
            ToastType.Success,
            "Saved / 已保存",
            ToastPriority.Normal,
            2.5f));

        private void ShowTooltip()
        {
            tooltip.SetMessage("Tap controls to inspect their Touch-safe behavior.");
            tooltip.OnPointerClick(null);
        }

        private void ShowValidationError() =>
            validation.SetValidationResult(false, "Coffee bean quantity is required.");

        private void OpenBottomSheet() => bottomSheet.SetActive(true);
    }
}

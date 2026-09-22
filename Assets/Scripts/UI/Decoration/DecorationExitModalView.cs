using System;
using System.Collections;
using System.Collections.Generic;
using AnimalCafe.UI.Foundation;
using AnimalCafe.UI.Components;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.Decoration
{
    public sealed class DecorationExitModalView : MonoBehaviour
    {
        [SerializeField] private AnimalCafe.UI.P8R.P8RAppearance appearance;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private RectTransform modalCard;
        private IUiPointerOwnershipRegistrar boundary;
        private AnimalCafeModalView sharedModal;
        private readonly HashSet<int> activeButtonPointerIds = new HashSet<int>();
        private readonly HashSet<int> scheduledPointerReleaseIds = new HashSet<int>();
        private Coroutine scheduledPointerReleaseCoroutine;
        private bool closePending;
        private bool refreshingMobileLayout;
        private AnimalCafe.UI.P8R.P8RModalSafeAreaHost mobileSafeAreaHost;
        public event Action ContinueEditingRequested;
        public event Action DiscardChangesRequested;
        public string[] ChoiceLabels => new[] { "Continue Editing", "Discard Changes" };
        public void Configure(IUiPointerOwnershipRegistrar value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            ReleaseGestureOwnership();
            boundary = value;
            ConfigureGestureRetention();
            EnsureMobileSafeAreaHost();
            Bind();
        }
        public void Show()
        {
            Bind();
            if (appearance != null)
            {
                titleLabel.text = appearance.Text("exit.title");
                bodyLabel.text = appearance.Text("exit.body");
                appearance.Button(continueButton, "back");
                appearance.Button(discardButton, "cancel", "destructive");
                continueButton.transform.Find("Icon")?.gameObject.SetActive(false);
                discardButton.transform.Find("Icon")?.gameObject.SetActive(false);
                continueButton.GetComponentInChildren<TMP_Text>(true).text = appearance.Text("exit.continue");
                discardButton.GetComponentInChildren<TMP_Text>(true).text = appearance.Text("exit.discard");
                AnimalCafe.UI.P8R.P8RButtonLayout.TextOnly(continueButton);
                AnimalCafe.UI.P8R.P8RButtonLayout.TextOnly(discardButton);
            }
            closePending = false;
            gameObject.SetActive(true);
            RefreshSafeAreaLayout();
            SetInteraction(true);
        }
        public void Close()
        {
            closePending = activeButtonPointerIds.Count > 0;
            SetInteraction(false);
            if (!closePending)
            {
                FinalizeClose();
            }
        }
        public void NotifyPointerReleased(int pointerId)
        {
            activeButtonPointerIds.Remove(pointerId);
            sharedModal?.ReleaseRetainedPointer(pointerId);
            boundary?.ReleasePointer(pointerId);
            if (closePending && activeButtonPointerIds.Count == 0)
            {
                FinalizeClose();
            }
        }
        public void ConfigureGestureRetention()
        {
            if (boundary == null)
            {
                throw new InvalidOperationException("Configure pointer ownership before gesture retention.");
            }

            sharedModal = GetComponent<AnimalCafeModalView>() ?? gameObject.AddComponent<AnimalCafeModalView>();
            sharedModal.ConfigureDelayedPointerRelease(boundary);
        }
        public void BeginButtonGesture(int pointerId) => RetainButtonPointer(pointerId);
        public void RetainButtonPointer(int pointerId)
        {
            if (pointerId == int.MinValue)
            {
                return;
            }

            activeButtonPointerIds.Add(pointerId);
            sharedModal?.RetainPointerUntilGestureEnd(pointerId);
        }
        public void ScheduleButtonPointerRelease(int pointerId)
        {
            if (pointerId == int.MinValue || !activeButtonPointerIds.Contains(pointerId))
            {
                return;
            }

            scheduledPointerReleaseIds.Add(pointerId);
            if (scheduledPointerReleaseCoroutine == null)
            {
                scheduledPointerReleaseCoroutine = StartCoroutine(ReleaseScheduledPointersNextFrame());
            }
        }
        public void ReleaseButtonPointer(int pointerId) { if (pointerId != int.MinValue) NotifyPointerReleased(pointerId); }
        private IEnumerator ReleaseScheduledPointersNextFrame()
        {
            yield return null;

            var pointerIds = new int[scheduledPointerReleaseIds.Count];
            scheduledPointerReleaseIds.CopyTo(pointerIds);
            scheduledPointerReleaseIds.Clear();
            scheduledPointerReleaseCoroutine = null;
            foreach (var pointerId in pointerIds)
            {
                NotifyPointerReleased(pointerId);
            }
        }
        private void Bind()
        {
            BindButton(continueButton, HandleContinueEditing);
            BindButton(discardButton, HandleDiscardChanges);
        }
        private void BindButton(Button button, UnityEngine.Events.UnityAction handler)
        {
            if (button == null) return;
            button.onClick.RemoveListener(handler);
            button.onClick.AddListener(handler);
            var hook = button.GetComponent<DecorationExitModalPointerGestureHook>()
                ?? button.gameObject.AddComponent<DecorationExitModalPointerGestureHook>();
            hook.Configure(this);
        }
        private void HandleContinueEditing() { ContinueEditingRequested?.Invoke(); Close(); }
        private void HandleDiscardChanges() { DiscardChangesRequested?.Invoke(); Close(); }
        private void SetInteraction(bool enabled)
        {
            if (continueButton != null) continueButton.interactable = enabled;
            if (discardButton != null) discardButton.interactable = enabled;
        }
        private void FinalizeClose()
        {
            closePending = false;
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
        private void RefreshSafeAreaLayout()
        {
            if (appearance != null)
            {
                if (refreshingMobileLayout) return;
                refreshingMobileLayout = true;
                try
                {
                    EnsureMobileSafeAreaHost();
                    AnimalCafe.UI.P8R.P8RButtonLayout.Modal(modalCard, titleLabel, bodyLabel, continueButton, discardButton);
                }
                finally { refreshingMobileLayout = false; }
                return;
            }
            if (modalCard == null)
            {
                return;
            }

            var safeRect = SafeAreaContainer.CalculateNormalizedSafeRect(
                Screen.safeArea,
                new Vector2(Screen.width, Screen.height));
            var cardAnchor = new Vector2(
                (safeRect.xMin + safeRect.xMax) * .5f,
                Mathf.Lerp(safeRect.yMin, safeRect.yMax, appearance != null ? .5f : .72f));
            modalCard.anchorMin = cardAnchor;
            modalCard.anchorMax = cardAnchor;
            modalCard.anchoredPosition = Vector2.zero;
            if (appearance != null && transform is RectTransform root)
            {
                var available = Vector2.Scale(root.rect.size, safeRect.size) - Vector2.one * 32f;
                var scale = Mathf.Min(1f, available.x / 840f, available.y / 560f);
                modalCard.localScale = Vector3.one * Mathf.Max(.1f, scale);
            }
        }

        private void EnsureMobileSafeAreaHost()
        {
            if (!Application.isPlaying || appearance == null || modalCard == null || mobileSafeAreaHost != null) return;
            var host = new GameObject("P8RModalSafeArea", typeof(RectTransform), typeof(AnimalCafe.UI.P8R.P8RModalSafeAreaHost));
            host.transform.SetParent(modalCard.parent, false);
            mobileSafeAreaHost = host.GetComponent<AnimalCafe.UI.P8R.P8RModalSafeAreaHost>();
            var rect = (RectTransform)host.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            modalCard.SetParent(rect, false);
            mobileSafeAreaHost.RectChanged = HandleMobileSafeAreaChanged;
            host.GetComponent<SafeAreaContainer>().ApplySafeArea(Screen.safeArea, new Vector2(Screen.width, Screen.height));
        }

        private void HandleMobileSafeAreaChanged()
        {
            if (this != null && gameObject.activeInHierarchy) RefreshSafeAreaLayout();
        }
        private void OnRectTransformDimensionsChange()
        {
            if (gameObject.activeInHierarchy)
            {
                RefreshSafeAreaLayout();
            }
        }
        private void ReleaseGestureOwnership()
        {
            CancelScheduledPointerRelease();
            sharedModal?.ReleaseAllRetainedPointers();
            if (boundary != null)
            {
                foreach (var pointerId in activeButtonPointerIds)
                {
                    boundary.ReleasePointer(pointerId);
                }
            }

            activeButtonPointerIds.Clear();
            closePending = false;
        }
        private void CancelScheduledPointerRelease()
        {
            if (scheduledPointerReleaseCoroutine != null)
            {
                StopCoroutine(scheduledPointerReleaseCoroutine);
                scheduledPointerReleaseCoroutine = null;
            }

            scheduledPointerReleaseIds.Clear();
        }
        private void OnDisable()
        {
            AnimalCafe.UI.P8R.P8RMobileMetrics.Changed -= RefreshSafeAreaLayout;
            ReleaseGestureOwnership();
            SetInteraction(false);
        }
        private void OnEnable() => AnimalCafe.UI.P8R.P8RMobileMetrics.Changed += RefreshSafeAreaLayout;
        private void OnDestroy()
        {
            ReleaseGestureOwnership();
            continueButton?.onClick.RemoveListener(HandleContinueEditing);
            discardButton?.onClick.RemoveListener(HandleDiscardChanges);
        }
    }
}

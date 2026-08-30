using System;
using System.Collections;
using System.Collections.Generic;
using AnimalCafe.UI.Foundation;
using AnimalCafe.UI.Components;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.Decoration
{
    public sealed class DecorationExitModalView : MonoBehaviour
    {
        [SerializeField] private Button continueButton;
        [SerializeField] private Button discardButton;
        [SerializeField] private RectTransform modalCard;
        private IUiPointerOwnershipRegistrar boundary;
        private AnimalCafeModalView sharedModal;
        private readonly HashSet<int> activeButtonPointerIds = new HashSet<int>();
        private readonly HashSet<int> scheduledPointerReleaseIds = new HashSet<int>();
        private Coroutine scheduledPointerReleaseCoroutine;
        private bool closePending;
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
            Bind();
        }
        public void Show()
        {
            Bind();
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
            if (modalCard == null)
            {
                return;
            }

            var safeRect = SafeAreaContainer.CalculateNormalizedSafeRect(
                Screen.safeArea,
                new Vector2(Screen.width, Screen.height));
            var cardAnchor = new Vector2(
                (safeRect.xMin + safeRect.xMax) * .5f,
                Mathf.Lerp(safeRect.yMin, safeRect.yMax, .72f));
            modalCard.anchorMin = cardAnchor;
            modalCard.anchorMax = cardAnchor;
            modalCard.anchoredPosition = Vector2.zero;
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
            ReleaseGestureOwnership();
            SetInteraction(false);
        }
        private void OnDestroy()
        {
            ReleaseGestureOwnership();
            continueButton?.onClick.RemoveListener(HandleContinueEditing);
            discardButton?.onClick.RemoveListener(HandleDiscardChanges);
        }
    }
}

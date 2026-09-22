using System;
using System.Collections.Generic;
using AnimalCafe.UI.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace AnimalCafe.UI.Decoration
{
    /// <summary>
    /// Serializable Task 6 adapter that gives UI press ownership to the shared boundary.
    /// 可序列化的 Task 6 adapter，把 UI press ownership 交给 shared boundary。
    /// </summary>
    public sealed class DecorationPointerBoundaryEventHook : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IPointerEnterHandler,
        IPointerExitHandler
    {
        [SerializeField] private string semanticLabel;
        [SerializeField] private GameObject tooltipRoot;
        [SerializeField] private TMP_Text tooltipLabel;
        [SerializeField] private bool tooltipEnabled = true;

        private IUiPointerOwnershipRegistrar pointerBoundary;
        private readonly HashSet<int> activePressIds = new HashSet<int>();

        public string SemanticLabel => semanticLabel;
        public bool HasActivePress => activePressIds.Count > 0;
        public event Action PresentationPressChanged;
        public bool IsTooltipVisible => tooltipRoot != null && tooltipRoot.activeSelf;

        public void Configure(IUiPointerOwnershipRegistrar registrar)
        {
            pointerBoundary = registrar ?? throw new ArgumentNullException(nameof(registrar));
        }

        public void SetTooltipEnabled(bool enabled)
        {
            tooltipEnabled = enabled;
            if (!enabled)
            {
                HideTooltip();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null)
            {
                pointerBoundary?.RegisterUiPointerPress(eventData.pointerId);
                if (activePressIds.Add(eventData.pointerId)) PresentationPressChanged?.Invoke();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null)
            {
                pointerBoundary?.ReleasePointer(eventData.pointerId);
                if (activePressIds.Remove(eventData.pointerId)) PresentationPressChanged?.Invoke();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!tooltipEnabled
                || !isActiveAndEnabled
                || !gameObject.activeInHierarchy
                || tooltipRoot == null)
            {
                return;
            }

            if (transform.parent != null)
            {
                foreach (var sibling in transform.parent
                             .GetComponentsInChildren<DecorationPointerBoundaryEventHook>(true))
                {
                    if (sibling != this)
                    {
                        sibling.HideTooltip();
                    }
                }
            }

            tooltipRoot.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            var eventSystem = EventSystem.current;
            var module = eventSystem != null ? eventSystem.currentInputModule : null;
            if (module == null || !module.isActiveAndEnabled || !eventSystem.isActiveAndEnabled)
                ClearPresentationPresses(); // Terminal UI-module purge, not an ordinary drag-out.
            else if (eventData is ExtendedPointerEventData extended && extended.device != null && !extended.device.added)
            {
                // InputSystem removes stale device pointers with Exit but no Up; retain other held pointers.
                // 只清这个已移除 device 的显示锁，不提前释放 registrar ownership。
                if (activePressIds.Remove(eventData.pointerId)) PresentationPressChanged?.Invoke();
            }
            HideTooltip();
        }

        public void ClearPresentationPresses()
        {
            if (activePressIds.Count == 0) return;
            activePressIds.Clear();
            PresentationPressChanged?.Invoke();
        }

        private void OnDisable()
        {
            ClearPresentationPresses();
            HideTooltip();
        }

        private void HideTooltip()
        {
            if (tooltipRoot != null && tooltipRoot.activeSelf)
            {
                tooltipRoot.SetActive(false);
            }
        }
    }
}

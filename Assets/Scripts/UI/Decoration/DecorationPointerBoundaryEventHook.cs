using System;
using AnimalCafe.UI.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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

        private IUiPointerOwnershipRegistrar pointerBoundary;

        public string SemanticLabel => semanticLabel;
        public bool IsTooltipVisible => tooltipRoot != null && tooltipRoot.activeSelf;

        public void Configure(IUiPointerOwnershipRegistrar registrar)
        {
            pointerBoundary = registrar ?? throw new ArgumentNullException(nameof(registrar));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData != null)
            {
                pointerBoundary?.RegisterUiPointerPress(eventData.pointerId);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData != null)
            {
                pointerBoundary?.ReleasePointer(eventData.pointerId);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy || tooltipRoot == null)
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
            HideTooltip();
        }

        private void OnDisable()
        {
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

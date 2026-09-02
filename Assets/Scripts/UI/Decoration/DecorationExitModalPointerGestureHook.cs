using UnityEngine;
using UnityEngine.EventSystems;

namespace AnimalCafe.UI.Decoration
{
    /// <summary>Forwards the real button gesture lifetime to the exit modal.</summary>
    public sealed class DecorationExitModalPointerGestureHook : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        private DecorationExitModalView owner;
        public void Configure(DecorationExitModalView value) => owner = value;
        public void OnPointerDown(PointerEventData eventData) => owner?.RetainButtonPointer(eventData?.pointerId ?? int.MinValue);
        public void OnPointerUp(PointerEventData eventData) => owner?.ScheduleButtonPointerRelease(eventData?.pointerId ?? int.MinValue);
    }
}

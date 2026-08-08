using UnityEngine;

namespace AnimalCafe.Content
{
    public sealed class SurfaceSlotMarker : MonoBehaviour
    {
        [SerializeField] private string slotId;

        public string SlotId => slotId;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.2f);
        }
    }
}

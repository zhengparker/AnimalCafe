using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Content
{
    public sealed class EntrancePortalAuthoring : MonoBehaviour
    {
        [SerializeField] private string entranceId;
        [SerializeField] private int originX;
        [SerializeField] private int originY;

        public string EntranceId => entranceId;
        public GridPosition Origin => new GridPosition(originX, originY);

        public LayoutReservation CreateReservation()
        {
            return new LayoutReservation(
                entranceId,
                LayoutReservationType.EntranceClearance,
                Origin,
                new GridSize(2, 2));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position, new Vector3(2f, 0.05f, 2f));
        }
    }
}

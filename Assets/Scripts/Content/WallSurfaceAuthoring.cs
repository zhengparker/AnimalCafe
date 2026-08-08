using UnityEngine;

namespace AnimalCafe.Content
{
    public sealed class WallSurfaceAuthoring : MonoBehaviour
    {
        [SerializeField] private string surfaceId;
        [SerializeField, Min(1)] private int columns = 1;
        [SerializeField, Min(1)] private int rows = 1;
        [SerializeField, Min(0.01f)] private float slotSize = 1f;
        [SerializeField] private float gizmoDepthOffset = -0.055f;

        public string SurfaceId => surfaceId;
        public int Columns => columns;
        public int Rows => rows;
        public float SlotSize => slotSize;
        public float GizmoDepthOffset => gizmoDepthOffset;

        private void OnDrawGizmosSelected()
        {
            var originalMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Color.magenta;

            var width = columns * slotSize;
            var height = rows * slotSize;
            var left = -width * 0.5f;

            for (var column = 0; column <= columns; column++)
            {
                var x = left + column * slotSize;
                Gizmos.DrawLine(
                    new Vector3(x, 0f, gizmoDepthOffset),
                    new Vector3(x, height, gizmoDepthOffset));
            }

            for (var row = 0; row <= rows; row++)
            {
                var y = row * slotSize;
                Gizmos.DrawLine(
                    new Vector3(left, y, gizmoDepthOffset),
                    new Vector3(left + width, y, gizmoDepthOffset));
            }

            Gizmos.matrix = originalMatrix;
        }
    }
}

using UnityEngine;

namespace AnimalCafe.Testing
{
    /// <summary>
    /// 使用 scaled delta time 往返移动，直观展示 Pause、1x 和 2x。
    /// Moves between two points using scaled delta time.
    /// </summary>
    public sealed class TimeTestMover : MonoBehaviour
    {
        [SerializeField]
        private Vector3 pointA = new(-3f, 0.75f, 2f);

        [SerializeField]
        private Vector3 pointB = new(3f, 0.75f, 2f);

        [SerializeField, Min(0f)]
        private float unitsPerSecond = 1.5f;

        private bool movingToPointB = true;

        private void Update()
        {
            var target = movingToPointB ? pointB : pointA;
            transform.position = Vector3.MoveTowards(
                transform.position,
                target,
                unitsPerSecond * UnityEngine.Time.deltaTime);

            if (Vector3.Distance(transform.position, target) <= 0.01f)
            {
                movingToPointB = !movingToPointB;
            }
        }

        public void Configure(
            Vector3 startPoint,
            Vector3 endPoint,
            float movementSpeed)
        {
            pointA = startPoint;
            pointB = endPoint;
            unitsPerSecond = Mathf.Max(0f, movementSpeed);
            ResetToStart();
        }

        public void ResetToStart()
        {
            transform.position = pointA;
            movingToPointB = true;
        }
    }
}

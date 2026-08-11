using UnityEngine;

namespace AnimalCafe.Diagnostics
{
    /// <summary>
    /// Temporary visual diagnostic motion for manual time-scale review.
    /// </summary>
    public sealed class ManualReviewPingPongMover : MonoBehaviour
    {
        [SerializeField] private Vector3 localPointA = new Vector3(-2f, 0.5f, -1f);
        [SerializeField] private Vector3 localPointB = new Vector3(2f, 0.5f, -1f);
        [SerializeField, Min(0.01f)] private float unitsPerSecond = 1f;
        private bool movingToPointB = true;

        public Vector3 LocalPointA => localPointA;

        public Vector3 LocalPointB => localPointB;

        public float UnitsPerSecond => unitsPerSecond;

        public void Configure(
            Vector3 configuredLocalPointA,
            Vector3 configuredLocalPointB,
            float configuredUnitsPerSecond)
        {
            localPointA = configuredLocalPointA;
            localPointB = configuredLocalPointB;
            unitsPerSecond = Mathf.Max(0.01f, configuredUnitsPerSecond);
            ResetToStart();
        }

        public void ResetToStart()
        {
            transform.localPosition = localPointA;
            movingToPointB = true;
        }

        private void Update()
        {
            var target = movingToPointB ? localPointB : localPointA;
            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition,
                target,
                unitsPerSecond * Time.deltaTime);

            if (transform.localPosition == target)
            {
                movingToPointB = !movingToPointB;
            }
        }
    }
}

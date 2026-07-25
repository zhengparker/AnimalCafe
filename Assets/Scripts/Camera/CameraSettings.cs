using UnityEngine;

namespace AnimalCafe.Camera
{
    [CreateAssetMenu(
        fileName = "DefaultCameraSettings",
        menuName = "AnimalCafe/Camera Settings")]
    public sealed class CameraSettings : ScriptableObject
    {
        [SerializeField, Min(0.001f)]
        private float panSpeed = 0.02f;

        [SerializeField, Min(0.001f)]
        private float zoomSpeed = 0.01f;

        [SerializeField]
        private Vector2 positionMin = new(-12f, -10f);

        [SerializeField]
        private Vector2 positionMax = new(12f, 10f);

        [SerializeField, Min(0.01f)]
        private float minOrthographicSize = 4f;

        [SerializeField, Min(0.01f)]
        private float maxOrthographicSize = 12f;

        [SerializeField, Min(0f)]
        private float dragThresholdPixels = 6f;

        public float PanSpeed
        {
            get => panSpeed;
            set => panSpeed = Mathf.Max(0.001f, value);
        }

        public float ZoomSpeed
        {
            get => zoomSpeed;
            set => zoomSpeed = Mathf.Max(0.001f, value);
        }

        public Vector2 PositionMin
        {
            get => positionMin;
            set
            {
                positionMin = value;
                NormalizeRanges();
            }
        }

        public Vector2 PositionMax
        {
            get => positionMax;
            set
            {
                positionMax = value;
                NormalizeRanges();
            }
        }

        public float MinOrthographicSize
        {
            get => minOrthographicSize;
            set
            {
                minOrthographicSize = Mathf.Max(0.01f, value);
                NormalizeRanges();
            }
        }

        public float MaxOrthographicSize
        {
            get => maxOrthographicSize;
            set
            {
                maxOrthographicSize = Mathf.Max(0.01f, value);
                NormalizeRanges();
            }
        }

        public float DragThresholdPixels
        {
            get => dragThresholdPixels;
            set => dragThresholdPixels = Mathf.Max(0f, value);
        }

        private void OnValidate()
        {
            panSpeed = Mathf.Max(0.001f, panSpeed);
            zoomSpeed = Mathf.Max(0.001f, zoomSpeed);
            dragThresholdPixels = Mathf.Max(0f, dragThresholdPixels);
            minOrthographicSize = Mathf.Max(0.01f, minOrthographicSize);
            maxOrthographicSize = Mathf.Max(0.01f, maxOrthographicSize);
            NormalizeRanges();
        }

        private void NormalizeRanges()
        {
            var minX = Mathf.Min(positionMin.x, positionMax.x);
            var maxX = Mathf.Max(positionMin.x, positionMax.x);
            var minZ = Mathf.Min(positionMin.y, positionMax.y);
            var maxZ = Mathf.Max(positionMin.y, positionMax.y);
            positionMin = new Vector2(minX, minZ);
            positionMax = new Vector2(maxX, maxZ);

            var minZoom = Mathf.Min(minOrthographicSize, maxOrthographicSize);
            var maxZoom = Mathf.Max(minOrthographicSize, maxOrthographicSize);
            minOrthographicSize = minZoom;
            maxOrthographicSize = maxZoom;
        }
    }
}

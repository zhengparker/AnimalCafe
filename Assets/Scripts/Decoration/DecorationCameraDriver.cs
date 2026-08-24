using AnimalCafe.Camera;
using AnimalCafe.Decoration.Input;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    /// <summary>
    /// Thin adapter for routed Decoration camera commands and edge auto-pan math.
    /// 只负责转交 Decoration Camera 命令与 edge auto-pan 数学。
    /// </summary>
    public sealed class DecorationCameraDriver : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        private float edgeZonePixels = 80f;

        [SerializeField]
        private AnimationCurve normalizedSpeedCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [SerializeField, Min(0f)]
        private float maxEdgeSpeedPixelsPerSecond = 600f;

        private CafeCameraController cameraController;

        public bool IsEdgeAutoPanning { get; private set; }

        public float EdgeZonePixels
        {
            get => edgeZonePixels;
            set => edgeZonePixels = SanitizeNonNegative(value);
        }

        public AnimationCurve NormalizedSpeedCurve
        {
            get => normalizedSpeedCurve;
            set => normalizedSpeedCurve = value;
        }

        public float MaxEdgeSpeedPixelsPerSecond
        {
            get => maxEdgeSpeedPixelsPerSecond;
            set => maxEdgeSpeedPixelsPerSecond = SanitizeNonNegative(value);
        }

        private void OnDisable()
        {
            StopEdgeAutoPan();
        }

        private void OnValidate()
        {
            edgeZonePixels = SanitizeNonNegative(edgeZonePixels);
            maxEdgeSpeedPixelsPerSecond = SanitizeNonNegative(maxEdgeSpeedPixelsPerSecond);
        }

        public void Configure(CafeCameraController controller)
        {
            cameraController = controller;
            StopEdgeAutoPan();
        }

        public void ApplyScenePan(Vector2 screenDelta)
        {
            if (IsFinite(screenDelta))
            {
                cameraController?.ApplyPan(screenDelta);
            }
        }

        public void ApplyPinchZoom(float pinchDistanceDelta)
        {
            if (IsFinite(pinchDistanceDelta))
            {
                cameraController?.ApplyZoom(pinchDistanceDelta);
            }
        }

        public Vector2 ApplyFurnitureEdgeAutoPan(
            DecorationGestureOwner owner,
            bool isDragging,
            Vector2 pointerPosition,
            Rect cameraPixelRect,
            Rect safeArea,
            bool isOverExcludedUiOrModal)
        {
            if (owner != DecorationGestureOwner.Furniture
                || !isDragging
                || isOverExcludedUiOrModal)
            {
                StopEdgeAutoPan();
                return Vector2.zero;
            }

            var delta = CalculateEdgeAutoPanScreenDelta(
                cameraPixelRect,
                safeArea,
                pointerPosition,
                edgeZonePixels,
                normalizedSpeedCurve,
                maxEdgeSpeedPixelsPerSecond,
                Time.unscaledDeltaTime);
            IsEdgeAutoPanning = delta != Vector2.zero;
            if (IsEdgeAutoPanning)
            {
                cameraController?.ApplyPan(delta);
            }

            return delta;
        }

        public void StopEdgeAutoPan()
        {
            IsEdgeAutoPanning = false;
        }

        public static Vector2 CalculateEdgeAutoPanScreenDelta(
            Rect cameraPixelRect,
            Rect safeArea,
            Vector2 pointerPosition,
            float edgeZonePixels,
            AnimationCurve normalizedSpeedCurve,
            float maxSpeedPixelsPerSecond,
            float unscaledDeltaTime)
        {
            if (!IsFinite(cameraPixelRect)
                || !IsFinite(safeArea)
                || !IsFinite(pointerPosition)
                || !IsFinite(edgeZonePixels)
                || !IsFinite(maxSpeedPixelsPerSecond)
                || !IsFinite(unscaledDeltaTime)
                || edgeZonePixels <= 0f
                || maxSpeedPixelsPerSecond <= 0f
                || unscaledDeltaTime <= 0f
                || normalizedSpeedCurve == null)
            {
                return Vector2.zero;
            }

            var minX = Mathf.Max(cameraPixelRect.xMin, safeArea.xMin);
            var maxX = Mathf.Min(cameraPixelRect.xMax, safeArea.xMax);
            var minY = Mathf.Max(cameraPixelRect.yMin, safeArea.yMin);
            var maxY = Mathf.Min(cameraPixelRect.yMax, safeArea.yMax);
            if (maxX <= minX
                || maxY <= minY
                || pointerPosition.x < minX
                || pointerPosition.x > maxX
                || pointerPosition.y < minY
                || pointerPosition.y > maxY)
            {
                return Vector2.zero;
            }

            var xIntent = CalculateAxisIntent(
                pointerPosition.x,
                minX,
                maxX,
                edgeZonePixels,
                normalizedSpeedCurve);
            var yIntent = CalculateAxisIntent(
                pointerPosition.y,
                minY,
                maxY,
                edgeZonePixels,
                normalizedSpeedCurve);
            var velocity = new Vector2(xIntent, yIntent) * maxSpeedPixelsPerSecond;
            velocity = Vector2.ClampMagnitude(velocity, maxSpeedPixelsPerSecond);
            return velocity * unscaledDeltaTime;
        }

        private static float CalculateAxisIntent(
            float position,
            float minimum,
            float maximum,
            float edgeZone,
            AnimationCurve curve)
        {
            var distanceFromMinimum = position - minimum;
            var distanceFromMaximum = maximum - position;
            if (distanceFromMinimum <= edgeZone
                && distanceFromMinimum <= distanceFromMaximum)
            {
                var proximity = 1f - distanceFromMinimum / edgeZone;
                return Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(proximity)));
            }

            if (distanceFromMaximum <= edgeZone)
            {
                var proximity = 1f - distanceFromMaximum / edgeZone;
                return -Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(proximity)));
            }

            return 0f;
        }

        private static float SanitizeNonNegative(float value)
        {
            return IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(Rect value)
        {
            return IsFinite(value.x)
                && IsFinite(value.y)
                && IsFinite(value.width)
                && IsFinite(value.height);
        }
    }
}

using UnityEngine;

namespace AnimalCafe.Content
{
    public sealed class WallSurfaceAuthoring : MonoBehaviour
    {
        public const float WallMountedPlaneEpsilon = 0.001f;
        private const float WallMountedProjectionSafetyOffset = 0.001f;
        private const string BaseFinishName = "Phase7_WallFinish";
        private const string WainscotingFinishName = "Phase7_WainscotingFinish";
        private const string WainscotingRailName = "Phase7_WainscotingRailLip";
        private const string WainscotingBaseboardName = "Phase7_WainscotingBaseboardLip";
        private const string WallVisualName = "WallVisual";

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

        /// <summary>
        /// Returns a contact point just outside the Base Wall Surface.
        /// Wainscoting and trim are appearance layers and must not float the whole item.
        /// 返回紧贴 Base Wall Surface 的墙饰接触点；护墙板与饰线不推开整件物品。
        /// </summary>
        public Vector3 GetWallMountedWorldPosition(
            Vector3 localSurfacePoint,
            float epsilon = WallMountedPlaneEpsilon)
        {
            return GetSurfaceWorldPosition(localSurfacePoint, null, epsilon);
        }

        /// <summary>Returns a point just outside the authoring-facing renderer side.</summary>
        public Vector3 GetProjectionWorldPosition(
            Vector3 localSurfacePoint,
            Renderer surfaceRenderer,
            float epsilon = 0.001f)
        {
            if (surfaceRenderer == null)
            {
                throw new System.ArgumentNullException(nameof(surfaceRenderer));
            }

            return GetSurfaceWorldPosition(localSurfacePoint, surfaceRenderer, epsilon);
        }

        /// <summary>
        /// Returns a footprint point beyond every visible architectural finish.
        /// The real mounted item still uses GetWallMountedWorldPosition so trim does
        /// not make the model float away from the Base Wall Surface.
        /// </summary>
        public Vector3 GetWallMountedProjectionWorldPosition(
            Vector3 localSurfacePoint,
            float epsilon = WallMountedPlaneEpsilon)
        {
            if (epsilon <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(epsilon));
            }

            var outwardNormal = -transform.forward;
            var authoredPosition = GetAuthoredWorldPosition(localSurfacePoint);
            var faceDistance = float.NegativeInfinity;
            foreach (var rendererName in new[]
            {
                BaseFinishName,
                WainscotingFinishName,
                WainscotingRailName,
                WainscotingBaseboardName,
                WallVisualName
            })
            {
                var renderer = transform.Find(rendererName)?.GetComponent<Renderer>();
                if (IsVisible(renderer))
                {
                    faceDistance = Mathf.Max(
                        faceDistance,
                        GetOutwardFaceDistance(renderer, outwardNormal));
                }
            }

            var rootRenderer = GetComponent<Renderer>();
            if (IsVisible(rootRenderer))
            {
                faceDistance = Mathf.Max(
                    faceDistance,
                    GetOutwardFaceDistance(rootRenderer, outwardNormal));
            }

            if (float.IsNegativeInfinity(faceDistance))
            {
                throw new System.InvalidOperationException(
                    $"Wall Surface '{surfaceId}' has no enabled architectural renderer.");
            }

            var authoredDistance = Vector3.Dot(authoredPosition, outwardNormal);
            return authoredPosition
                + outwardNormal * Mathf.Max(
                    0f,
                    faceDistance + epsilon + WallMountedProjectionSafetyOffset - authoredDistance);
        }

        private Vector3 GetSurfaceWorldPosition(
            Vector3 localSurfacePoint,
            Renderer preferredRenderer,
            float epsilon)
        {
            if (epsilon <= 0f)
            {
                throw new System.ArgumentOutOfRangeException(nameof(epsilon));
            }

            var outwardNormal = -transform.forward;
            // SlotSize is specified in world metres. Do not apply the
            // authoring object's visual renderer scale a second time when
            // converting a slot centre to world space.
            var authoredPosition = GetAuthoredWorldPosition(localSurfacePoint);
            var contactRenderer = FindContactRenderer(preferredRenderer);
            if (contactRenderer == null)
            {
                throw new System.InvalidOperationException(
                    $"Wall Surface '{surfaceId}' has no enabled architectural renderer.");
            }

            var faceDistance = GetOutwardFaceDistance(contactRenderer, outwardNormal);
            var authoredDistance = Vector3.Dot(authoredPosition, outwardNormal);
            return authoredPosition + outwardNormal * Mathf.Max(0f, faceDistance + epsilon - authoredDistance);
        }

        private Vector3 GetAuthoredWorldPosition(Vector3 localSurfacePoint)
        {
            // SlotSize is specified in world metres. Do not apply the
            // authoring object's visual renderer scale a second time.
            return transform.position + transform.rotation * new Vector3(
                localSurfacePoint.x,
                localSurfacePoint.y,
                gizmoDepthOffset);
        }

        private Renderer FindContactRenderer(Renderer preferredRenderer)
        {
            if (IsVisible(preferredRenderer))
            {
                return preferredRenderer;
            }

            var baseFinish = transform.Find(BaseFinishName)?.GetComponent<Renderer>();
            if (IsVisible(baseFinish))
            {
                return baseFinish;
            }

            var wallVisual = transform.Find(WallVisualName)?.GetComponent<Renderer>();
            return IsVisible(wallVisual)
                ? wallVisual
                : IsVisible(GetComponent<Renderer>()) ? GetComponent<Renderer>() : null;
        }

        private static bool IsVisible(Renderer candidate)
        {
            return candidate != null
                && candidate.enabled
                && candidate.gameObject.activeInHierarchy;
        }

        private static float GetOutwardFaceDistance(Renderer renderer, Vector3 outwardNormal)
        {
            var bounds = renderer.localBounds;
            var minimum = bounds.min;
            var maximum = bounds.max;
            var faceDistance = float.NegativeInfinity;
            for (var x = 0; x < 2; x++)
            for (var y = 0; y < 2; y++)
            for (var z = 0; z < 2; z++)
            {
                var localCorner = new Vector3(
                    x == 0 ? minimum.x : maximum.x,
                    y == 0 ? minimum.y : maximum.y,
                    z == 0 ? minimum.z : maximum.z);
                faceDistance = Mathf.Max(
                    faceDistance,
                    Vector3.Dot(renderer.transform.TransformPoint(localCorner), outwardNormal));
            }
            return faceDistance;
        }

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

using UnityEngine;

namespace AnimalCafe.UI.P8R
{
    /// <summary>Camera-facing visual motion only; the slot and footprint stay fixed.
    /// 只抬高/浮动牌面，不改变柜台上的取餐位置或 Footprint。</summary>
    [DisallowMultipleComponent]
    public sealed class P8RPickUpSignBillboard : MonoBehaviour
    {
        private const float Lift = .112f; // 20% of the existing .56-unit sign.
        private const float FloatAmplitude = .028f;
        private const float FloatPeriod = 2.8f;
        private UnityEngine.Camera viewCamera;
        private Vector3 authoredLocalPosition;
        private Vector3 hoverOffset;
        private float currentBob;
        private bool capturedRestPosition;

        private void OnEnable()
        {
            CaptureRestPosition();
            LateUpdate();
        }

        private void CaptureRestPosition()
        {
            if (capturedRestPosition) return;
            authoredLocalPosition = transform.localPosition;
            capturedRestPosition = true;
        }

        private void LateUpdate()
        {
            if (viewCamera == null || !viewCamera.isActiveAndEnabled)
                viewCamera = UnityEngine.Camera.main;
            FaceCamera(viewCamera);
            if (Application.isPlaying) ApplyMotion();
        }

        /// <summary>Preview's drag lift is independent of the ambient bob / 拖动悬浮与轻浮动独立。</summary>
        public void SetHoverOffset(Vector3 worldOffset)
        {
            if (!IsFinite(worldOffset.x) || !IsFinite(worldOffset.y) || !IsFinite(worldOffset.z)) return;
            CaptureRestPosition();
            hoverOffset = worldOffset;
            LateUpdate();
        }

        private void ApplyMotion()
        {
            // Shared unscaled phase survives preview recreation and paused Decoration.
            // Preview 反复重建时不重置相位；装修暂停时仍缓慢浮动。
            currentBob = Mathf.Sin(Time.unscaledTime * (2f * Mathf.PI / FloatPeriod)) * FloatAmplitude;
            var rest = transform.parent != null
                ? transform.parent.TransformPoint(authoredLocalPosition) : authoredLocalPosition;
            transform.position = rest + hoverOffset + Vector3.up * (Lift + currentBob);
        }

        /// <summary>Reserve the whole motion envelope so buttons do not bob with the artwork.
        /// 操作栏避让整个浮动范围，不跟随每帧牌面的位置。</summary>
        public Bounds GetPresentationBounds(Renderer renderer)
        {
            // The creator may have set the support pose after OnEnable this frame.
            // 创建时可能先 OnEnable 再旋转 root；同步完整姿态后才计算操作栏位置。
            if (Application.isPlaying && isActiveAndEnabled) LateUpdate();
            var bounds = renderer.bounds;
            if (!Application.isPlaying || !isActiveAndEnabled || renderer.transform != transform) return bounds;
            bounds.center -= Vector3.up * currentBob;
            bounds.Expand(Vector3.up * (2f * FloatAmplitude));
            return bounds;
        }

        private void OnDisable()
        {
            if (capturedRestPosition) transform.localPosition = authoredLocalPosition;
            currentBob = 0;
        }

        public void FaceCamera(UnityEngine.Camera camera)
        {
            if (camera != null) transform.rotation = camera.transform.rotation;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

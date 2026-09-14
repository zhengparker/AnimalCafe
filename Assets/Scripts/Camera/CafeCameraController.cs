using AnimalCafe.Input;
using UnityEngine;

namespace AnimalCafe.Camera
{
    /// <summary>
    /// 设备无关的固定斜俯视 Camera controller。
    /// Device-independent fixed angled top-down camera controller.
    /// </summary>
    public sealed class CafeCameraController : MonoBehaviour
    {
        private const float PinchPixelsPerZoomStep = 40f;

        [SerializeField]
        private UnityEngine.Camera targetCamera;

        [SerializeField]
        private CameraSettings settings;

        [SerializeField]
        private MonoBehaviour inputSourceBehaviour;

        private ICameraInputSource inputSource;

        private void OnEnable()
        {
            CancelHeldTouch();
        }

        private void OnDisable()
        {
            CancelHeldTouch();
        }

        private void CancelHeldTouch()
        {
            // Decoration disables this consumer; held fingers must not carry over.
            // 模式交接时取消旧 Touch，抬手后才接受新手势。
            var source = inputSource ?? inputSourceBehaviour as ICameraInputSource;
            if (source is MouseCameraInput pointerInput)
            {
                pointerInput.CancelTouchGesture();
            }
        }

        private void Start()
        {
            inputSource ??= inputSourceBehaviour as ICameraInputSource;
            if (targetCamera == null || settings == null || inputSource == null)
            {
                Debug.LogError(
                    "[CafeCameraController] Camera, settings, and input source are required.",
                    this);
                enabled = false;
                return;
            }

            targetCamera.orthographic = true;
            ClampToBounds();
        }

        private void Update()
        {
            if (inputSource == null)
            {
                return;
            }

            var inputFrame = inputSource.ReadFrame();
            ApplyPan(inputFrame.PanDelta);
            ApplyZoom(inputFrame.ZoomDelta);
            ApplyPinchZoom(inputFrame.PinchDistanceDelta);
        }

        public void Configure(
            UnityEngine.Camera camera,
            CameraSettings cameraSettings,
            ICameraInputSource cameraInputSource)
        {
            targetCamera = camera;
            settings = cameraSettings;
            inputSource = cameraInputSource;
        }

        public void ApplyPan(Vector2 screenDelta)
        {
            if (targetCamera == null || settings == null || screenDelta == Vector2.zero)
            {
                return;
            }

            var flatForward = Vector3.ProjectOnPlane(
                targetCamera.transform.forward,
                Vector3.up).normalized;
            var flatRight = Vector3.ProjectOnPlane(
                targetCamera.transform.right,
                Vector3.up).normalized;
            var movement = -(flatRight * screenDelta.x + flatForward * screenDelta.y)
                * settings.PanSpeed;
            targetCamera.transform.position += movement;
            ClampToBounds();
        }

        public void ApplyZoom(float scrollDelta)
        {
            if (float.IsNaN(scrollDelta) || float.IsInfinity(scrollDelta)
                || Mathf.Approximately(scrollDelta, 0f))
            {
                return;
            }

            ApplyContinuousZoom(Mathf.Sign(scrollDelta));
        }

        public void ApplyPinchZoom(float pinchDistanceDelta)
        {
            ApplyContinuousZoom(pinchDistanceDelta / PinchPixelsPerZoomStep);
        }

        // Wheel uses whole steps; continuous gestures preserve their displacement.
        // 滚轮保持整步缩放，连续手势保留实际位移，不按输入帧数累计整步。
        public void ApplyContinuousZoom(float zoomSteps)
        {
            if (targetCamera == null || settings == null
                || float.IsNaN(zoomSteps) || float.IsInfinity(zoomSteps)
                || Mathf.Approximately(zoomSteps, 0f))
            {
                return;
            }

            targetCamera.orthographicSize -= zoomSteps * settings.ZoomSpeed;
            ClampToBounds();
        }

        public void ClampToBounds()
        {
            if (targetCamera == null || settings == null)
            {
                return;
            }

            var position = targetCamera.transform.position;
            position.x = Mathf.Clamp(
                position.x,
                settings.PositionMin.x,
                settings.PositionMax.x);
            position.z = Mathf.Clamp(
                position.z,
                settings.PositionMin.y,
                settings.PositionMax.y);
            targetCamera.transform.position = position;
            targetCamera.orthographicSize = Mathf.Clamp(
                targetCamera.orthographicSize,
                settings.MinOrthographicSize,
                settings.MaxOrthographicSize);
        }
    }
}

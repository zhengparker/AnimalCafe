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
        [SerializeField]
        private UnityEngine.Camera targetCamera;

        [SerializeField]
        private CameraSettings settings;

        [SerializeField]
        private MonoBehaviour inputSourceBehaviour;

        private ICameraInputSource inputSource;

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
            if (targetCamera == null || settings == null || Mathf.Approximately(scrollDelta, 0f))
            {
                return;
            }

            targetCamera.orthographicSize -= Mathf.Sign(scrollDelta) * settings.ZoomSpeed;
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

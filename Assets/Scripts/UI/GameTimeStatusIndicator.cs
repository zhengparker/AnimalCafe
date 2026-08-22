using System;
using AnimalCafe.Core.Time;
using UnityEngine;

namespace AnimalCafe.UI
{
    /// <summary>
    /// Read-only visual feedback for Pause, 1x, and 2x game speed.
    /// Pause/1x/2x 的只读视觉反馈；不拥有或修改游戏速度。
    /// </summary>
    public sealed class GameTimeStatusIndicator : MonoBehaviour
    {
        private const float NormalDegreesPerSecond = 90f;

        [SerializeField]
        private RectTransform rotatingVisual;

        [SerializeField]
        private GameTimeService gameTimeService;

        private IGameTimeService configuredGameTimeService;

        public void Configure(
            IGameTimeService service,
            RectTransform visual)
        {
            configuredGameTimeService = service
                ?? throw new ArgumentNullException(nameof(service));
            gameTimeService = service as GameTimeService;
            rotatingVisual = visual
                ?? throw new ArgumentNullException(nameof(visual));
        }

        private void Update()
        {
            Refresh(Time.unscaledDeltaTime);
        }

        public void Refresh(float unscaledDeltaTime)
        {
            var service = configuredGameTimeService ?? gameTimeService;
            if (service == null
                || rotatingVisual == null
                || float.IsNaN(unscaledDeltaTime)
                || float.IsInfinity(unscaledDeltaTime)
                || unscaledDeltaTime <= 0f)
            {
                return;
            }

            var speedMultiplier = (float)service.CurrentSpeed;
            if (speedMultiplier <= 0f)
            {
                return;
            }

            rotatingVisual.Rotate(
                0f,
                0f,
                -NormalDegreesPerSecond * speedMultiplier * unscaledDeltaTime);
        }
    }
}

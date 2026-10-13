using System;
using System.Collections;
using UnityEngine;

namespace AnimalCafe.UI.Foundation
{
    /// <summary>
    /// Resolves transition timing independently from scaled gameplay time.
    /// 解析 UI transition 时长；Reduced Motion 可跳过非必要动画。
    /// </summary>
    public sealed class UiTransitionRunner
    {
        private readonly Func<bool> isReducedMotionEnabled;
        private readonly Func<float> unscaledFrameDelta;

        public UiTransitionRunner(Func<bool> reducedMotionHook, Func<float> unscaledFrameDelta = null)
        {
            isReducedMotionEnabled = reducedMotionHook
                ?? throw new ArgumentNullException(nameof(reducedMotionHook));
            this.unscaledFrameDelta = unscaledFrameDelta ?? (() => Time.unscaledDeltaTime);
        }

        // The default clock remains real unscaled time; callers may supply a deterministic clock.
        // 默认保持真实 unscaled 时间；确定性运行可传入自己的帧步长。
        public float FrameDelta => unscaledFrameDelta();

        public float ResolveDuration(float requestedDuration, bool isEssential)
        {
            if (requestedDuration < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(requestedDuration));
            }

            return isReducedMotionEnabled() && !isEssential
                ? 0f
                : requestedDuration;
        }

        public IEnumerator Run(CanvasGroup group, bool visible, float requestedDuration)
        {
            if (group == null)
            {
                throw new ArgumentNullException(nameof(group));
            }

            var duration = ResolveDuration(requestedDuration, isEssential: false);
            var startAlpha = group.alpha;
            var targetAlpha = visible ? 1f : 0f;
            if (duration <= 0f)
            {
                group.alpha = targetAlpha;
                yield break;
            }

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += FrameDelta;
                group.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            group.alpha = targetAlpha;
        }
    }
}

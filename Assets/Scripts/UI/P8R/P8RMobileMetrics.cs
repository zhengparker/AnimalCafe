using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace AnimalCafe.UI.P8R
{
    public enum P8RMobileMetricsSource { Native, EditorProfile, Estimated }

    /// <summary>
    /// Converts Android dp / iOS points (or an explicit preview profile) into root Canvas units.
    /// 将平台逻辑尺寸换算成根 Canvas units；下级 UI 的 localScale 必须保持 1。
    /// Estimated is a usable phone fallback, not a claim of measured physical size.
    /// Estimated 仅表示手机尺寸估算，不能作为真机 dp/pt 验收证据。
    /// </summary>
    public readonly struct P8RMobileMetrics
    {
        public Vector2 LogicalViewport { get; }
        public float PixelsPerLogicalUnit { get; }
        public float UnitsPerLogicalUnit { get; }
        public P8RMobileMetricsSource Source { get; }

        private P8RMobileMetrics(Vector2 logicalViewport, float pixelsPerLogicalUnit,
            float unitsPerLogicalUnit, P8RMobileMetricsSource source)
        {
            LogicalViewport = logicalViewport;
            PixelsPerLogicalUnit = pixelsPerLogicalUnit;
            UnitsPerLogicalUnit = unitsPerLogicalUnit;
            Source = source;
        }

        public static P8RMobileMetrics Calculate(Vector2 renderPixels, Vector2 logicalViewport,
            float canvasScale, P8RMobileMetricsSource source = P8RMobileMetricsSource.EditorProfile)
        {
            if (!Positive(renderPixels)) throw new ArgumentOutOfRangeException(nameof(renderPixels));
            if (!Positive(logicalViewport)) throw new ArgumentOutOfRangeException(nameof(logicalViewport));
            if (!Positive(canvasScale)) throw new ArgumentOutOfRangeException(nameof(canvasScale));
            // Use the larger ratio if a platform briefly reports rounded/asymmetric dimensions.
            // 平台尺寸四舍五入时取较大比例，避免任一方向的 touch target 小于约定。
            var pixelsPerUnit = Mathf.Max(renderPixels.x / logicalViewport.x, renderPixels.y / logicalViewport.y);
            var canvasUnits = pixelsPerUnit / canvasScale;
            if (!Positive(pixelsPerUnit) || !Positive(canvasUnits))
                throw new ArgumentOutOfRangeException(nameof(canvasScale), "The metric ratio must remain finite and positive.");
            return new P8RMobileMetrics(logicalViewport, pixelsPerUnit, canvasUnits, source);
        }

        public float Units(float logical)
        {
            if (!Finite(logical) || logical < 0f || !Finite(logical * UnitsPerLogicalUnit))
                throw new ArgumentOutOfRangeException(nameof(logical));
            return logical * UnitsPerLogicalUnit;
        }

        public Vector2 SafeSizeInLogicalUnits(Rect safeAreaPixels)
        {
            if (!Finite(safeAreaPixels.width) || !Finite(safeAreaPixels.height))
                throw new ArgumentOutOfRangeException(nameof(safeAreaPixels));
            return new Vector2(Mathf.Max(0, safeAreaPixels.width), Mathf.Max(0, safeAreaPixels.height))
                / PixelsPerLogicalUnit;
        }

        /// <summary>Raised only on the Unity main thread. Views subscribe while enabled.
        /// 仅在 Unity main thread 通知；View 在 OnEnable/OnDisable 配对订阅。</summary>
        public static event Action Changed;

        private static Vector2? editorLogicalViewportOverride;
        /// <summary>Editor screenshot/device fixture profile; null uses a 360-short-side phone.
        /// Editor 可明确指定手机或 tablet 的 logical viewport；不会读取电脑显示器 DPI。</summary>
        public static Vector2? EditorLogicalViewportOverride
        {
            get => editorLogicalViewportOverride;
            set
            {
                if (editorLogicalViewportOverride == value) return;
                editorLogicalViewportOverride = value;
#if UNITY_EDITOR
                Changed?.Invoke();
#endif
            }
        }

        public static P8RMobileMetrics For(Component context)
        {
            var canvas = context != null ? context.GetComponentInParent<Canvas>(true) : null;
            canvas = canvas != null ? canvas.rootCanvas : null;
#if UNITY_EDITOR
            // Prefab authoring must not serialize geometry derived from a developer's Game view.
            // 无 Canvas 的 authoring 使用固定手机参考；有效的显式 profile 仍可覆盖测试尺寸。
            if (canvas == null && (!editorLogicalViewportOverride.HasValue || !Positive(editorLogicalViewportOverride.Value)))
                return Calculate(new Vector2(1080, 1920), new Vector2(360, 640), 1f,
                    P8RMobileMetricsSource.Estimated);
#endif
            // A camera Canvas can target a RenderTexture whose dimensions differ from Screen.
            // 相机 Canvas 的 RenderTexture 不一定等于 Editor Game view 的 Screen 尺寸。
            var renderPixels = canvas != null ? canvas.renderingDisplaySize : Vector2.zero;
            if (!Positive(renderPixels)) renderPixels = new Vector2(Screen.width, Screen.height);
            if (!Positive(renderPixels)) renderPixels = new Vector2(360, 640);
            var scale = canvas != null && Positive(canvas.scaleFactor) ? canvas.scaleFactor : 1f;
#if UNITY_EDITOR
            if (editorLogicalViewportOverride.HasValue)
            {
                var profile = editorLogicalViewportOverride.Value;
                if (Positive(profile)) return Calculate(renderPixels, profile, scale);
                return Calculate(renderPixels, PhoneEstimate(renderPixels), scale, P8RMobileMetricsSource.Estimated);
            }
            return Calculate(renderPixels, PhoneEstimate(renderPixels), scale);
#else
            EnsureNativeHooks();
            RefreshNativeViewport();
            return Calculate(renderPixels, hasNativeViewport ? nativeLogicalViewport : PhoneEstimate(renderPixels),
                scale, hasNativeViewport ? P8RMobileMetricsSource.Native : P8RMobileMetricsSource.Estimated);
#endif
        }

        private static Vector2 PhoneEstimate(Vector2 pixels) => pixels * (360f / Mathf.Min(pixels.x, pixels.y));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Positive(float value) => Finite(value) && value > 0f;
        private static bool Positive(Vector2 value) => Positive(value.x) && Positive(value.y);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeState()
        {
            editorLogicalViewportOverride = null;
            Changed = null;
#if !UNITY_EDITOR
            Canvas.preWillRenderCanvases -= RefreshNativeViewport;
            Application.focusChanged -= OnFocusChanged;
#if UNITY_ANDROID
            AndroidApplication.onConfigurationChanged -= OnAndroidConfigurationChanged;
#endif
            nativeHooksInstalled = false;
            nativeReadPending = false;
            hasNativeViewport = false;
            nativeLogicalViewport = lastRenderPixels = Vector2.zero;
            retryCount = 0;
            retryAfter = 0;
            generation++;
#endif
        }

#if !UNITY_EDITOR
        private static bool nativeHooksInstalled, nativeReadPending, hasNativeViewport;
        private static Vector2 nativeLogicalViewport, lastRenderPixels;
        private static int retryCount, generation;
        private static float retryAfter;

        private static void EnsureNativeHooks()
        {
            if (nativeHooksInstalled) return;
            nativeHooksInstalled = true;
#if UNITY_ANDROID || UNITY_IOS
            // Cheap dimension checks only; successful native queries are cached until a change.
            // 每次渲染仅比较尺寸；native 查询成功后缓存，尺寸/焦点/config 改变才重新读取。
            Canvas.preWillRenderCanvases += RefreshNativeViewport;
            Application.focusChanged += OnFocusChanged;
#if UNITY_ANDROID
            AndroidApplication.onConfigurationChanged += OnAndroidConfigurationChanged;
#endif
#endif
        }

        private static void OnFocusChanged(bool focused)
        {
            if (focused) InvalidateNativeViewport();
        }

#if UNITY_ANDROID
        private static void OnAndroidConfigurationChanged(AndroidConfiguration configuration) => InvalidateNativeViewport();
#endif

        private static void InvalidateNativeViewport()
        {
            generation++;
            retryCount = 0;
            retryAfter = 0;
            hasNativeViewport = false;
        }

        private static void RefreshNativeViewport()
        {
#if UNITY_ANDROID || UNITY_IOS
            var pixels = new Vector2(Screen.width, Screen.height);
            if (!Positive(pixels)) return;
            if (pixels != lastRenderPixels)
            {
                lastRenderPixels = pixels;
                InvalidateNativeViewport();
            }
            if (hasNativeViewport || nativeReadPending || retryCount >= 3 || Time.realtimeSinceStartup < retryAfter) return;
            nativeReadPending = true;
            retryCount++;
            retryAfter = Time.realtimeSinceStartup + 1f;
            var requestGeneration = generation;
#if UNITY_ANDROID
            try { RequestAndroidViewport(requestGeneration); }
            catch (AndroidJavaException) { PublishNativeViewport(requestGeneration, Vector2.zero); }
#else
            var logical = Vector2.zero;
            try
            {
                if (P8R_GetUnityViewportPoints(out var width, out var height) != 0)
                    logical = new Vector2(width, height);
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            PublishNativeViewport(requestGeneration, logical);
#endif
#endif
        }

        private static void PublishNativeViewport(int requestGeneration, Vector2 logical)
        {
            nativeReadPending = false;
            if (requestGeneration != generation || !Positive(logical)) return;
            // Screen can rotate before the native view finishes layout. Retry stale geometry.
            // Screen 先转向时，等待 native view 完成布局后再缓存尺寸。
            var renderAspect = lastRenderPixels.x / lastRenderPixels.y;
            var viewAspect = logical.x / logical.y;
            if (!Positive(renderAspect) || Mathf.Abs(viewAspect / renderAspect - 1f) > .03f) return;
            var changed = !hasNativeViewport || nativeLogicalViewport != logical;
            nativeLogicalViewport = logical;
            hasNativeViewport = true;
            if (changed) Changed?.Invoke();
        }

#if UNITY_ANDROID
        private static void RequestAndroidViewport(int requestGeneration)
        {
            // Unity owns this handle; capture on the Unity thread and never Dispose it.
            // Unity 6 的 GameActivity / Activity 共用此入口；不要猜测 activity 的 private fields。
            var player = AndroidApplication.unityPlayer;
            if (player == null) { PublishNativeViewport(requestGeneration, Vector2.zero); return; }
            AndroidApplication.InvokeOnUIThread(() =>
            {
                var logical = Vector2.zero;
                try
                {
                    // Unity 6000.5 documents getFrameLayout for both application entry modes.
                    // 使用当前 Unity 官方示例的宿主 view accessor，不依赖旧版 getView。
                    using var view = player.Call<AndroidJavaObject>("getFrameLayout");
                    if (view != null)
                    {
                        using var resources = view.Call<AndroidJavaObject>("getResources");
                        using var displayMetrics = resources.Call<AndroidJavaObject>("getDisplayMetrics");
                        var density = displayMetrics.Get<float>("density");
                        var width = view.Call<int>("getWidth");
                        var height = view.Call<int>("getHeight");
                        if (Positive(density) && width > 0 && height > 0)
                            logical = new Vector2(width / density, height / density);
                    }
                }
                catch (AndroidJavaException) { }
                // Never block one main thread waiting for the other.
                // UI thread 只读取 view；回到 Unity main thread 后才更新 UI/发事件。
                AndroidApplication.InvokeOnUnityMainThread(() => PublishNativeViewport(requestGeneration, logical));
            });
        }
#endif
#if UNITY_IOS
        [DllImport("__Internal")]
        private static extern int P8R_GetUnityViewportPoints(out float width, out float height);
#endif
#endif
    }
}

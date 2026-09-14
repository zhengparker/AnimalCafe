using System;
using AnimalCafe.UI.Components;
using UnityEngine;

namespace AnimalCafe.UI.P8R
{
    /// <summary>Notifies its modal only when the dedicated safe-area geometry changes.
    /// 仅在安全区尺寸改变时通知弹窗，不逐帧重建文字。</summary>
    [RequireComponent(typeof(RectTransform), typeof(SafeAreaContainer))]
    public sealed class P8RModalSafeAreaHost : MonoBehaviour
    {
        public Action RectChanged;
        private void OnRectTransformDimensionsChange() => RectChanged?.Invoke();
        private void OnDestroy() => RectChanged = null;
    }
}

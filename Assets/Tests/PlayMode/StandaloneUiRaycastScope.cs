using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode
{
    // Test-only boundary for fixtures that build their own world and UI.
    // 独立 fixture 的 UI raycast 边界；不能用于生产场景。
    internal sealed class StandaloneUiRaycastScope : IDisposable
    {
        private readonly GraphicRaycaster[] callers = UnityEngine.Object.FindObjectsByType<GraphicRaycaster>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Where(item => item.enabled).ToArray();

        public StandaloneUiRaycastScope()
        {
            foreach (var caller in callers) caller.enabled = false;
        }

        public void Dispose()
        {
            foreach (var caller in callers)
                if (caller != null) caller.enabled = true;
        }
    }
}

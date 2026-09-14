#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    internal static class Phase8SceneInputTestCleanup
    {
        internal static InputActionAsset[] CaptureAssets(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<InputSystemUIInputModule>(true))
            .Select(module => module.actionsAsset).Where(asset => asset != null).Distinct().ToArray();

        internal static void DisposeReleasedAssets(InputActionAsset[] assets)
        {
            var remainingModules = Object.FindObjectsByType<InputSystemUIInputModule>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var asset in assets)
            {
                Assert.That(asset.enabled, Is.False,
                    "Unloaded Scene UI actions must be disabled before releasing their runtime state.");
                Assert.That(remainingModules.Any(module => module.actionsAsset == asset), Is.False,
                    "A Scene fixture must not dispose action state still owned by another UI module.");

                // Keep the real serialized asset/bindings, but release the fixture's cached controls
                // before another InputTestFixture swaps the global InputSystem registry.
                // 保留真实资源与 bindings；在下一 fixture reset 前释放本场景留下的运行时缓存。
                foreach (var map in asset.actionMaps) map.Dispose();
            }
        }
    }
}
#endif

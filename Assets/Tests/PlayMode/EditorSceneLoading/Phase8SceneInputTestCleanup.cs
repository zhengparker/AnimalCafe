#if UNITY_EDITOR
using System.Collections;
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
        internal static IEnumerator ReleaseActiveSceneBeforeInputReset()
        {
            // Single loading will replace this scene; release its action caches before swapping registries.
            // Single load 原本就会替换当前 scene；先释放 UI controls，再 reset InputSystem。
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            var assets = CaptureAssets(scene);
            SceneManager.SetActiveScene(SceneManager.CreateScene("BeforeInputResetCleanup"));
            var unload = SceneManager.UnloadSceneAsync(scene);
            while (unload != null && !unload.isDone) yield return null;
            DisposeReleasedAssets(assets);
        }

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

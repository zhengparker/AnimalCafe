using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.EditorTools.Phase6;
using AnimalCafe.EditorTools.Phase7;
using AnimalCafe.EditorTools.Phase8;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.EditMode
{
    // Build a portable legacy seed from existing authoring, not today's production UI.
    // 旧迁移测试使用独立生成的旧版场景；生产场景的 Owner bytes 在 finally 精确恢复。
    internal sealed class LegacyMainCafeFixture : IDisposable
    {
        private const string Main = "Assets/Scenes/MainCafe.unity";
        private static byte[] cachedLegacy;
        private readonly byte[] original;
        private readonly byte[] originalMeta;
        private readonly Scene[] scenes;
        private readonly Scene active;
        private readonly bool mainWasActive;
        private readonly int mainIndex;
        private readonly bool mainWasLoaded;
        private readonly (UnityEngine.Object Value, GlobalObjectId Id)[] selection;
        private readonly (UnityEngine.Object Value, GlobalObjectId Id) activeSelection;
        private readonly EditorBuildSettingsScene[] buildSettings;
        private bool disposed;

        public LegacyMainCafeFixture()
        {
            var main = SceneManager.GetSceneByPath(Main);
            if (main.IsValid() && main.isLoaded && main.isDirty)
                throw new InvalidOperationException("Legacy fixture refuses a caller-owned dirty MainCafe.");
            scenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            if (main.IsValid() && main.isLoaded && scenes.Count(item => item.isLoaded) == 1)
                throw new InvalidOperationException("Legacy fixture requires a separate caller Scene before replacing a sole loaded MainCafe.");
            active = SceneManager.GetActiveScene();
            mainWasActive = active.path == Main;
            mainIndex = Array.FindIndex(scenes, item => item.path == Main);
            mainWasLoaded = main.IsValid() && main.isLoaded;
            selection = Selection.objects.Select(value => (value, GlobalObjectId.GetGlobalObjectIdSlow(value))).ToArray();
            activeSelection = (Selection.activeObject, GlobalObjectId.GetGlobalObjectIdSlow(Selection.activeObject));
            buildSettings = EditorBuildSettings.scenes;
            original = File.ReadAllBytes(Main);
            originalMeta = File.ReadAllBytes(Main + ".meta");
            if (main.IsValid() && !EditorSceneManager.CloseScene(main, true))
                throw new InvalidOperationException("Could not release MainCafe before installing a legacy fixture.");
            try { Install(Main); }
            catch { Dispose(); throw; }
        }

        internal static void Install(string path)
        {
            var bytes = GetLegacyBytes();
            if (!File.Exists(path)) CreateEmptyAsset(path);
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static byte[] GetLegacyBytes()
        {
            if (cachedLegacy != null) return cachedLegacy;
            var active = SceneManager.GetActiveScene();
            var folder = "Assets/__LegacyMainCafeSeed_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var path = folder + "/Seed.unity";
            Scene scene = default;
            try
            {
                var dependencies = Invoke(typeof(Phase6DecorationSceneSetup), "ResolveDependencies", Phase6SceneSetupTarget.Validation);
                scene = (Scene)Invoke(typeof(Phase6DecorationSceneSetup), "CreateValidationCandidate", dependencies, path);
                var contract = scene.GetRootGameObjects().Single(root => root.name == "Phase6_ContractReferences");
                UnityEngine.Object.DestroyImmediate(contract);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save legacy Phase 6 fixture.");
                EditorSceneManager.CloseScene(scene, true);
                Invoke(typeof(Phase7DecorationSceneSetup), "ConfigureScene", path);
                // MainCafe's only path-specific Phase 7 presentation rule.
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var window = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Single(item => item.name == "P4_Window_BackRight_C3_R0");
                window.gameObject.SetActive(false);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save legacy window state.");
                EditorSceneManager.CloseScene(scene, true);
                Invoke(typeof(Phase8SceneSetup), "ConfigureScene", path, false);
                cachedLegacy = File.ReadAllBytes(path);
                return cachedLegacy;
            }
            finally
            {
                scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                // This exact unique folder was created above by this helper only.
                AssetDatabase.DeleteAsset(folder);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }

        private static object Invoke(Type type, string method, params object[] arguments)
        {
            try { return type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, arguments); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }

        private static void CreateEmptyAsset(string path)
        {
            var method = typeof(EditorSceneManager).GetMethod("CreateSceneAsset", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(string), typeof(bool) }, null);
            if (method == null || !(bool)method.Invoke(null, new object[] { path, false }))
                throw new IOException("Could not create isolated legacy Scene: " + path);
        }

        public void Dispose()
        {
            if (disposed) return;
            var scene = SceneManager.GetSceneByPath(Main);
            if (scene.IsValid() && !EditorSceneManager.CloseScene(scene, true))
                throw new InvalidOperationException("Could not release test-owned MainCafe before restoring its original bytes.");
            File.WriteAllBytes(Main, original);
            File.WriteAllBytes(Main + ".meta", originalMeta);
            AssetDatabase.ImportAsset(Main, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            EditorBuildSettings.scenes = buildSettings;
            // Restore only MainCafe's entry; never reopen an unrelated dirty/Untitled Scene.
            // 只恢复本scope替换的场景，保留调用者其他scene的handle和未保存内容。
            if (mainIndex >= 0)
            {
                scene = EditorSceneManager.OpenScene(Main, OpenSceneMode.Additive);
                var next = scenes.Skip(mainIndex + 1).FirstOrDefault(item => item.IsValid());
                var previous = scenes.Take(mainIndex).LastOrDefault(item => item.IsValid());
                if (next.IsValid()) EditorSceneManager.MoveSceneBefore(scene, next);
                else if (previous.IsValid()) EditorSceneManager.MoveSceneAfter(scene, previous);
                if (mainWasActive && mainWasLoaded) SceneManager.SetActiveScene(scene);
                if (!mainWasLoaded) EditorSceneManager.CloseScene(scene, false);
            }
            if (!mainWasActive && active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            var restoredSelection = selection.Select(item => item.Value != null ? item.Value
                : GlobalObjectId.GlobalObjectIdentifierToObjectSlow(item.Id)).ToArray();
            Selection.objects = restoredSelection;
            Selection.activeObject = activeSelection.Value != null ? activeSelection.Value
                : GlobalObjectId.GlobalObjectIdentifierToObjectSlow(activeSelection.Id);
            disposed = true;
        }
    }
}

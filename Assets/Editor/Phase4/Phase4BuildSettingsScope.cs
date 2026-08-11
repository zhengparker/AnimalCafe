using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.Phase4
{
    public static class Phase4BuildSettingsScope
    {
        private const string ScenePath =
            "Assets/Scenes/Validation/Phase4CoreArchitecture.unity";
        private static readonly string DefaultBackupPath = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "Library", "Phase4EnvironmentIntegrationTests", "BuildSettingsBackup.json");

        internal static string BackupPathOverrideForTests { get; set; }

        private static string BackupPath =>
            BackupPathOverrideForTests ?? DefaultBackupPath;

        public static IDisposable Open()
        {
            Setup();
            return new RestoreOnDispose();
        }

        public static void Setup()
        {
            var backupPath = BackupPath;
            if (!File.Exists(backupPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                File.WriteAllText(
                    backupPath,
                    JsonUtility.ToJson(new Snapshot(EditorBuildSettings.scenes)));
            }

            try
            {
                var original = ReadBackup(backupPath);
                EditorBuildSettings.scenes = CreateTemporaryScenes(original);
            }
            catch
            {
                Cleanup();
                throw;
            }
        }

        public static void Cleanup()
        {
            var backupPath = BackupPath;
            if (!File.Exists(backupPath)) return;

            var original = ReadBackup(backupPath);
            var current = EditorBuildSettings.scenes;
            var temporary = CreateTemporaryScenes(original);

            if (ScenesEqual(current, temporary))
            {
                EditorBuildSettings.scenes = original
                    .Select(entry => entry.ToScene())
                    .ToArray();
            }
            else if (original.All(entry => entry.path != ScenePath))
            {
                EditorBuildSettings.scenes = current
                    .Where(scene => scene.path != ScenePath)
                    .ToArray();
            }

            File.Delete(backupPath);
        }

        private static EditorBuildSettingsScene[] CreateTemporaryScenes(
            SceneEntry[] original)
        {
            return original
                .Where(entry => entry.path != ScenePath)
                .Select(entry => entry.ToScene())
                .Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) })
                .ToArray();
        }

        private static bool ScenesEqual(
            EditorBuildSettingsScene[] left,
            EditorBuildSettingsScene[] right)
        {
            return left.Length == right.Length
                && left.Zip(right, (first, second) =>
                        first.path == second.path && first.enabled == second.enabled)
                    .All(equal => equal);
        }

        private static SceneEntry[] ReadBackup(string backupPath)
        {
            return JsonUtility.FromJson<Snapshot>(File.ReadAllText(backupPath)).scenes;
        }

        private sealed class RestoreOnDispose : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed) return;
                Cleanup();
                disposed = true;
            }
        }

        [Serializable]
        private sealed class Snapshot
        {
            public SceneEntry[] scenes;

            public Snapshot(EditorBuildSettingsScene[] source)
            {
                scenes = source.Select(SceneEntry.From).ToArray();
            }
        }

        [Serializable]
        private sealed class SceneEntry
        {
            public string path;
            public bool enabled;

            public static SceneEntry From(EditorBuildSettingsScene scene)
            {
                return new SceneEntry { path = scene.path, enabled = scene.enabled };
            }

            public EditorBuildSettingsScene ToScene()
            {
                return new EditorBuildSettingsScene(path, enabled);
            }
        }
    }
}

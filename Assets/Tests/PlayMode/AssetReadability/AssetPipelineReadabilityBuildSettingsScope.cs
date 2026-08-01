using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.PlayMode.AssetReadability
{
    public static class AssetPipelineReadabilityBuildSettingsScope
    {
        private const string ScenePath =
            "Assets/Scenes/Validation/AssetPipelineReadability.unity";
        private static readonly string DefaultBackupPath = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "Library", "AssetPipelineReadabilityTests", "BuildSettingsBackup.json");
        internal static string BackupPathOverrideForTests { get; set; }

        private static string BackupPath =>
            BackupPathOverrideForTests ?? DefaultBackupPath;

        public static void Setup()
        {
            var backupPath = BackupPath;
            if (!File.Exists(backupPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                File.WriteAllText(backupPath,
                    JsonUtility.ToJson(new Snapshot(EditorBuildSettings.scenes)));
            }

            var original = ReadBackup(backupPath);
            var temporary = original.Select(entry => entry.ToScene()).ToArray();
            var existing = Array.FindIndex(temporary, scene => scene.path == ScenePath);
            if (existing < 0)
            {
                temporary = temporary.Concat(new[] { new EditorBuildSettingsScene(ScenePath, true) }).ToArray();
            }
            else
            {
                temporary[existing] = new EditorBuildSettingsScene(ScenePath, true);
            }

            EditorBuildSettings.scenes = temporary;
        }

        public static void Cleanup()
        {
            var backupPath = BackupPath;
            if (!File.Exists(backupPath)) return;
            EditorBuildSettings.scenes = ReadBackup(backupPath)
                .Select(entry => entry.ToScene())
                .ToArray();
            File.Delete(backupPath);
        }

        private static SceneEntry[] ReadBackup(string backupPath)
        {
            return JsonUtility.FromJson<Snapshot>(File.ReadAllText(backupPath)).scenes;
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

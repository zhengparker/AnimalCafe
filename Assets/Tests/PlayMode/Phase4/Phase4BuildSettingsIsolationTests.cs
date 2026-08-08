using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.PlayMode.Phase4
{
    public sealed class Phase4BuildSettingsIsolationTests
    {
        private const string ScenePath =
            "Assets/Scenes/Validation/Phase4CoreArchitecture.unity";

        [Test]
        public void Scope_AbsentValidationSceneRestoresExactOriginal()
        {
            AssertLifecycle(new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainCafe.unity", true)
            });
        }

        [Test]
        public void Scope_PreexistingDuplicatesOrderAndFlagsRestoreExactOriginal()
        {
            AssertLifecycle(new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainCafe.unity", false),
                new EditorBuildSettingsScene(ScenePath, false),
                new EditorBuildSettingsScene("Assets/Scenes/MainCafe.unity", true),
                new EditorBuildSettingsScene(ScenePath, true)
            });
        }

        [Test]
        public void Scope_DisposeAfterFailureRestoresExactOriginal()
        {
            var original = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainCafe.unity", true),
                new EditorBuildSettingsScene(ScenePath, false)
            };

            WithProjectBuildSettings(original, () =>
            {
                Assert.Throws<AssertionException>(() =>
                {
                    using (Phase4BuildSettingsScope.Open())
                    {
                        AssertValidationSceneIsEnabledOnce();
                        throw new AssertionException("Simulated PlayMode test failure.");
                    }
                });

                AssertScenesEqual(original, EditorBuildSettings.scenes);
            });
        }

        [Test]
        public void Scope_RepeatedSetupAndCleanupRestoresExactOriginal()
        {
            var original = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainCafe.unity", true)
            };

            WithProjectBuildSettings(original, () =>
            {
                Phase4BuildSettingsScope.Setup();
                Phase4BuildSettingsScope.Setup();
                AssertValidationSceneIsEnabledOnce();
                Phase4BuildSettingsScope.Cleanup();
                Phase4BuildSettingsScope.Cleanup();
                AssertScenesEqual(original, EditorBuildSettings.scenes);
            });
        }

        [Test]
        public void Scope_ExternalCleanupBeforeDisposeDoesNotReintroduceExternalScene()
        {
            var projectOriginal = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainCafe.unity", true)
            };
            var externalTemporary = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainCafe.unity", true),
                new EditorBuildSettingsScene(
                    "Assets/Scenes/Validation/ExternalValidation.unity", true)
            };

            WithProjectBuildSettings(projectOriginal, () =>
            {
                EditorBuildSettings.scenes = externalTemporary;
                using (Phase4BuildSettingsScope.Open())
                {
                    AssertValidationSceneIsEnabledOnce();
                    EditorBuildSettings.scenes = projectOriginal;
                }

                AssertScenesEqual(projectOriginal, EditorBuildSettings.scenes);
            });
        }

        private static void AssertLifecycle(EditorBuildSettingsScene[] original)
        {
            WithProjectBuildSettings(original, () =>
            {
                using (Phase4BuildSettingsScope.Open())
                {
                    AssertValidationSceneIsEnabledOnce();
                }

                AssertScenesEqual(original, EditorBuildSettings.scenes);
            });
        }

        private static void WithProjectBuildSettings(
            EditorBuildSettingsScene[] testSettings,
            TestDelegate test)
        {
            var projectOriginal = EditorBuildSettings.scenes;
            var testBackupPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Library", "Phase4EnvironmentIntegrationTests",
                $"ScopeTestBackup-{Guid.NewGuid():N}.json");
            Phase4BuildSettingsScope.BackupPathOverrideForTests = testBackupPath;
            try
            {
                EditorBuildSettings.scenes = testSettings;
                test();
            }
            finally
            {
                Phase4BuildSettingsScope.Cleanup();
                Phase4BuildSettingsScope.BackupPathOverrideForTests = null;
                EditorBuildSettings.scenes = projectOriginal;
            }
        }

        private static void AssertValidationSceneIsEnabledOnce()
        {
            Assert.That(EditorBuildSettings.scenes.Count(scene =>
                scene.path == ScenePath && scene.enabled), Is.EqualTo(1));
        }

        private static void AssertScenesEqual(
            EditorBuildSettingsScene[] expected,
            EditorBuildSettingsScene[] actual)
        {
            Assert.That(actual.Select(scene => scene.path),
                Is.EqualTo(expected.Select(scene => scene.path)));
            Assert.That(actual.Select(scene => scene.enabled),
                Is.EqualTo(expected.Select(scene => scene.enabled)));
        }
    }

    internal static class Phase4BuildSettingsScope
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
            if (!File.Exists(backupPath))
            {
                return;
            }

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
                // Another temporary scope already restored its own state.
                // Preserve that authoritative list and remove only our injected Scene.
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
                if (disposed)
                {
                    return;
                }

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

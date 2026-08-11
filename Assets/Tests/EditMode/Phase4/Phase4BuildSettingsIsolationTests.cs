using System;
using System.IO;
using System.Linq;
using AnimalCafe.EditorTools.Phase4;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase4
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
                new EditorBuildSettingsScene("Assets/Scenes/MainCafe.unity", true)
            };
            WithProjectBuildSettings(original, () =>
            {
                Assert.Throws<AssertionException>(() =>
                {
                    using (Phase4BuildSettingsScope.Open())
                    {
                        throw new AssertionException("Simulated failure.");
                    }
                });
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
                    Assert.That(EditorBuildSettings.scenes.Count(scene =>
                        scene.path == ScenePath && scene.enabled), Is.EqualTo(1));
                    EditorBuildSettings.scenes = projectOriginal;
                }

                AssertScenesEqual(projectOriginal, EditorBuildSettings.scenes);
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
                Phase4BuildSettingsScope.Cleanup();
                Phase4BuildSettingsScope.Cleanup();
                AssertScenesEqual(original, EditorBuildSettings.scenes);
            });
        }

        private static void AssertLifecycle(EditorBuildSettingsScene[] original)
        {
            WithProjectBuildSettings(original, () =>
            {
                using (Phase4BuildSettingsScope.Open())
                {
                    Assert.That(EditorBuildSettings.scenes.Count(scene =>
                        scene.path == ScenePath && scene.enabled), Is.EqualTo(1));
                }
                AssertScenesEqual(original, EditorBuildSettings.scenes);
            });
        }

        private static void WithProjectBuildSettings(
            EditorBuildSettingsScene[] testSettings,
            TestDelegate test)
        {
            var projectOriginal = EditorBuildSettings.scenes;
            var backupPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Library", "Phase4EnvironmentIntegrationTests",
                $"ScopeTestBackup-{Guid.NewGuid():N}.json");
            Phase4BuildSettingsScope.BackupPathOverrideForTests = backupPath;
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
}

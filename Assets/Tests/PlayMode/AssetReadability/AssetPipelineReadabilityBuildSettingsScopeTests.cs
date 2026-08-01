using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.PlayMode.AssetReadability
{
    public sealed class AssetPipelineReadabilityBuildSettingsScopeTests
    {
        private const string ScenePath = "Assets/Scenes/Validation/AssetPipelineReadability.unity";

        [TestCase(true)]
        [TestCase(false)]
        public void Scope_PreexistingValidationSceneRestoresExactOriginal(bool enabled)
        {
            AssertLifecycle(new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainCafe.unity", true),
                new EditorBuildSettingsScene(ScenePath, enabled),
                new EditorBuildSettingsScene("Assets/Scenes/MainCafe.unity", false)
            });
        }

        [Test]
        public void Scope_AbsentValidationSceneRestoresExactOriginal()
        {
            AssertLifecycle(new[] { new EditorBuildSettingsScene("Assets/Scenes/MainCafe.unity", true) });
        }

        [Test]
        public void Scope_DuplicatesAndOrderRestoreExactOriginal()
        {
            AssertLifecycle(new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainCafe.unity", false),
                new EditorBuildSettingsScene(ScenePath, false),
                new EditorBuildSettingsScene("Assets/Scenes/MainCafe.unity", true),
                new EditorBuildSettingsScene(ScenePath, true),
                new EditorBuildSettingsScene("Assets/Scenes/MainCafe.unity", true)
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
                AssetPipelineReadabilityBuildSettingsScope.Setup();
                AssetPipelineReadabilityBuildSettingsScope.Setup();
                AssertValidationSceneIsEnabled();
                AssetPipelineReadabilityBuildSettingsScope.Cleanup();
                AssetPipelineReadabilityBuildSettingsScope.Cleanup();
                AssertScenesEqual(original, EditorBuildSettings.scenes);
            });
        }

        [Test]
        public void Scope_ExceptionCleanupRestoresExactOriginal()
        {
            var original = new[]
            {
                new EditorBuildSettingsScene("Assets/Scenes/MainCafe.unity", true),
                new EditorBuildSettingsScene(ScenePath, false)
            };

            WithProjectBuildSettings(original, () =>
            {
                try
                {
                    AssetPipelineReadabilityBuildSettingsScope.Setup();
                    AssertValidationSceneIsEnabled();
                    throw new AssertionException("Simulated test failure.");
                }
                catch (AssertionException)
                {
                    AssetPipelineReadabilityBuildSettingsScope.Cleanup();
                }

                AssertScenesEqual(original, EditorBuildSettings.scenes);
            });
        }

        private static void AssertLifecycle(EditorBuildSettingsScene[] original)
        {
            WithProjectBuildSettings(original, () =>
            {
                AssetPipelineReadabilityBuildSettingsScope.Setup();
                AssertValidationSceneIsEnabled();
                AssetPipelineReadabilityBuildSettingsScope.Cleanup();
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
                "Library", "AssetPipelineReadabilityTests",
                $"ScopeTestBackup-{Guid.NewGuid():N}.json");
            AssetPipelineReadabilityBuildSettingsScope.BackupPathOverrideForTests =
                testBackupPath;
            try
            {
                EditorBuildSettings.scenes = testSettings;
                test();
            }
            finally
            {
                AssetPipelineReadabilityBuildSettingsScope.Cleanup();
                AssetPipelineReadabilityBuildSettingsScope.BackupPathOverrideForTests = null;
                EditorBuildSettings.scenes = projectOriginal;
            }
        }

        private static void AssertValidationSceneIsEnabled()
        {
            Assert.That(EditorBuildSettings.scenes.Any(scene =>
                scene.path == ScenePath && scene.enabled), Is.True);
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

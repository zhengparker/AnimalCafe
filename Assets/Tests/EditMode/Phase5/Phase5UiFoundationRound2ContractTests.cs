using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using AnimalCafe.EditorTools.Phase5;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.Tests.Phase5
{
    public sealed class Phase5UiFoundationRound2ContractTests
    {
        private AnimalCafeUiTheme Theme =>
            AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(Phase5UiAssetPaths.ThemePath);

        [TestCase("UI Root", "MissingUiRoot")]
        [TestCase("EventSystem", "MissingEventSystem")]
        [TestCase("HUD Canvas", "MissingCanvas")]
        public void Validator_MissingRequiredInventory_ReportsStableCode(
            string objectName,
            string expectedCode)
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();
            UnityEngine.Object.DestroyImmediate(Find(scene, objectName));

            var report = Phase5UiFoundationValidator.Validate(scene, Theme);

            Assert.That(report.Issues.Select(issue => issue.Code.ToString()),
                Does.Contain(expectedCode));
        }

        [Test]
        public void Validator_ExtraCanvasAndExtraLogicalLayer_ReportStableCodes()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();
            var uiRoot = Find(scene, "UI Root").transform;
            var extraCanvas = new GameObject("Debug Canvas", typeof(RectTransform), typeof(Canvas));
            extraCanvas.transform.SetParent(uiRoot, false);
            var extraLayer = new GameObject("Debug Layer", typeof(RectTransform));
            extraLayer.transform.SetParent(Find(scene, "Screen Canvas").transform, false);

            var report = Phase5UiFoundationValidator.Validate(scene, Theme);
            var codes = report.Issues.Select(issue => issue.Code.ToString()).ToArray();

            Assert.That(codes, Does.Contain("UnexpectedCanvas"));
            Assert.That(codes, Does.Contain("UnexpectedLogicalLayer"));
        }

        [Test]
        public void Validator_DuplicateApprovedLogicalLayer_IsRejected()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var scene = OpenValidationScene();
            var hudLayer = Find(scene, "HUD Layer");
            var duplicate = UnityEngine.Object.Instantiate(hudLayer, hudLayer.transform.parent);
            duplicate.name = "HUD Layer";

            var report = Phase5UiFoundationValidator.Validate(scene, Theme);

            Assert.That(report.Issues.Select(issue => issue.Code.ToString()),
                Does.Contain("DuplicateLogicalLayer"));
        }

        [Test]
        public void CanonicalAssetValidator_ReportsMissingAndDuplicateAssetsWithStablePaths()
        {
            var method = typeof(Phase5UiFoundationValidator).GetMethod(
                "ValidateCanonicalAssets",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(IEnumerable<string>), typeof(IEnumerable<string>) },
                null);
            Assert.That(method, Is.Not.Null,
                "Task 9 requires a public non-mutating canonical asset validation pass.");

            var canonical = new[]
            {
                "Assets/UI/Phase5/Test/Missing.asset",
                "Assets/Scenes/Validation/Phase5UiFoundation.unity"
            };
            var discovered = new[]
            {
                "Assets/Scenes/Validation/Phase5UiFoundation.unity",
                "Assets/Temp/Phase5UiFoundation.unity"
            };
            var report = (Phase5UiFoundationValidationReport)method.Invoke(
                null,
                new object[] { canonical, discovered });

            Assert.That(report.Issues.Any(issue =>
                issue.Code.ToString() == "MissingCanonicalAsset" &&
                issue.AssetPath == canonical[0]), Is.True);
            Assert.That(report.Issues.Any(issue =>
                issue.Code.ToString() == "DuplicateCanonicalAsset" &&
                issue.AssetPath == discovered[1]), Is.True);
        }

        [Test]
        public void CanonicalAssetValidator_CurrentRepositoryHasExactlyApprovedGeneratedAssets()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var method = typeof(Phase5UiFoundationValidator).GetMethod(
                "ValidateCanonicalAssets",
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            Assert.That(method, Is.Not.Null,
                "The canonical validator requires a repository-facing non-mutating entry point.");
            var report = (Phase5UiFoundationValidationReport)method.Invoke(null, null);
            Assert.That(report.Issues, Is.Empty);
        }

        [Test]
        public void GeneratedInputAssets_AreAllPartOfCanonicalAssetContract()
        {
            var expected = new[]
            {
                Phase5UiAssetPaths.ValidationInputActionsPath,
                "Assets/UI/Phase5/Resources/Phase5UiFoundationPointReference.asset",
                "Assets/UI/Phase5/Resources/Phase5UiFoundationClickReference.asset",
                "Assets/UI/Phase5/Resources/Phase5UiFoundationScrollWheelReference.asset",
                "Assets/UI/Phase5/Resources/Phase5UiFoundationSubmitReference.asset",
                "Assets/UI/Phase5/Resources/Phase5UiFoundationCancelReference.asset"
            };

            Assert.That(Phase5UiAssetPaths.AllGeneratedAssetPaths,
                Is.SupersetOf(expected));
        }

        [Test]
        public void BuildScene_TwiceProducesIdenticalSerializedSceneAndInputAssets()
        {
            Phase5UiFoundationSceneSetup.BuildScene();
            var first = FingerprintGeneratedEvidence();

            Phase5UiFoundationSceneSetup.BuildScene();
            var second = FingerprintGeneratedEvidence();

            Assert.That(second, Is.EqualTo(first));
        }

        private static string FingerprintGeneratedEvidence()
        {
            var paths = Phase5UiAssetPaths.AllGeneratedAssetPaths
                .Where(path => path == Phase5UiFoundationSceneSetup.ScenePath ||
                               path.Contains("Phase5UiFoundation", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal);
            using var sha = SHA256.Create();
            foreach (var path in paths)
            {
                var bytes = File.ReadAllBytes(Path.GetFullPath(path));
                sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return BitConverter.ToString(sha.Hash).Replace("-", string.Empty);
        }

        private static Scene OpenValidationScene() => EditorSceneManager.OpenScene(
            Phase5UiFoundationSceneSetup.ScenePath,
            OpenSceneMode.Single);

        private static GameObject Find(Scene scene, string name) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Single(transform => transform.name == name).gameObject;
    }
}

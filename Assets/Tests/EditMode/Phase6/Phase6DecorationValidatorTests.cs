using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AnimalCafe.Camera;
using AnimalCafe.Content;
using AnimalCafe.Core.Time;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Input;
using AnimalCafe.EditorTools.Phase6;
using AnimalCafe.Interaction;
using AnimalCafe.UI;
using AnimalCafe.UI.Decoration;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.Tests.EditMode.Phase6
{
    /// <summary>
    /// Task 8 validator contract tests. Every candidate Scene is closed in the
    /// outer finally so a failing assertion cannot leak Scene state.
    /// </summary>
    internal sealed class Phase6DecorationValidatorTests
    {
        private const string MainCafePath = "Assets/Scenes/MainCafe.unity";
        private const string ValidationPath =
            "Assets/Scenes/Validation/Phase6DecorationMode.unity";
        private static readonly object[] CandidateParityCases =
        {
            new object[] { "missing-main-camera", Phase6DecorationIssueCode.MissingMainCamera },
            new object[] { "duplicate-main-camera", Phase6DecorationIssueCode.DuplicateMainCamera },
            new object[] { "missing-light", Phase6DecorationIssueCode.MissingDirectionalLight },
            new object[] { "duplicate-light", Phase6DecorationIssueCode.DuplicateDirectionalLight },
            new object[] { "missing-phase0", Phase6DecorationIssueCode.MissingPhase0Runtime },
            new object[] { "duplicate-phase0", Phase6DecorationIssueCode.DuplicatePhase0Runtime },
            new object[] { "duplicate-mouse", Phase6DecorationIssueCode.DuplicateMouseCameraInput },
            new object[] { "duplicate-camera-controller", Phase6DecorationIssueCode.DuplicateCafeCameraController },
            new object[] { "duplicate-interaction", Phase6DecorationIssueCode.DuplicateSceneInteractionController },
            new object[] { "runtime-location", Phase6DecorationIssueCode.RuntimeComponentLocationDrift },
            new object[] { "camera-binding", Phase6DecorationIssueCode.CameraSettingsBindingDrift },
            new object[] { "missing-environment", Phase6DecorationIssueCode.MissingEnvironmentRoot },
            new object[] { "duplicate-environment", Phase6DecorationIssueCode.DuplicateEnvironmentRoot },
            new object[] { "environment-root-transform", Phase6DecorationIssueCode.EnvironmentTransformDrift },
            new object[] { "missing-floor", Phase6DecorationIssueCode.MissingEnvironmentPrefab },
            new object[] { "wrong-floor", Phase6DecorationIssueCode.EnvironmentPrefabDrift },
            new object[] { "floor-transform", Phase6DecorationIssueCode.EnvironmentTransformDrift },
            new object[] { "grid-overlay", Phase6DecorationIssueCode.FloorGridOverlayStateDrift },
            new object[] { "missing-owner", Phase6DecorationIssueCode.MissingDecorationOwner },
            new object[] { "decoration-owner-transform", Phase6DecorationIssueCode.InvalidGridTransform },
            new object[] { "decoration-space-transform", Phase6DecorationIssueCode.InvalidGridTransform },
            new object[] { "missing-grid", Phase6DecorationIssueCode.MissingGridRoot },
            new object[] { "duplicate-grid", Phase6DecorationIssueCode.DuplicateGridRoot },
            new object[] { "invalid-grid", Phase6DecorationIssueCode.InvalidGridTransform },
            new object[] { "missing-catalogue", Phase6DecorationIssueCode.MissingCatalogueBinding },
            new object[] { "missing-ui-reference", Phase6DecorationIssueCode.MissingUiReference },
            new object[] { "missing-ui-root", Phase6DecorationIssueCode.MissingUiRoot },
            new object[] { "duplicate-ui-root", Phase6DecorationIssueCode.DuplicateUiRoot },
            new object[] { "missing-canvas", Phase6DecorationIssueCode.MissingCanvas },
            new object[] { "duplicate-canvas", Phase6DecorationIssueCode.DuplicateCanvas },
            new object[] { "unexpected-canvas", Phase6DecorationIssueCode.UnexpectedCanvas },
            new object[] { "missing-event-system", Phase6DecorationIssueCode.MissingEventSystem },
            new object[] { "duplicate-event-system", Phase6DecorationIssueCode.DuplicateEventSystem },
            new object[] { "missing-input-module", Phase6DecorationIssueCode.MissingInputSystemUiModule },
            new object[] { "duplicate-input-module", Phase6DecorationIssueCode.DuplicateInputSystemUiModule },
            new object[] { "missing-input-actions", Phase6DecorationIssueCode.MissingInputActions },
            new object[] { "missing-time-panel", Phase6DecorationIssueCode.MissingTimePanel },
            new object[] { "missing-contract-root", Phase6DecorationIssueCode.MissingContractReferenceRoot },
            new object[] { "duplicate-contract-root", Phase6DecorationIssueCode.DuplicateContractReferenceRoot },
            new object[] { "missing-hud-toggle", Phase6DecorationIssueCode.MissingHudToggle },
            new object[] { "temporary-fixture", Phase6DecorationIssueCode.TemporaryFixturePresent },
            new object[] { "unexpected-initial", Phase6DecorationIssueCode.UnexpectedInitialContent },
            new object[] { "missing-definition", Phase6DecorationIssueCode.MissingDefinition },
            new object[] { "missing-prefab", Phase6DecorationIssueCode.MissingPrefab },
            new object[] { "missing-thumbnail", Phase6DecorationIssueCode.MissingThumbnail }
        };
        private static readonly object[] ReviewerContractCases =
        {
            new object[] { "missing-window", Phase6DecorationIssueCode.MissingEnvironmentPrefab },
            new object[] { "wrong-window", Phase6DecorationIssueCode.EnvironmentPrefabDrift },
            new object[] { "window-transform", Phase6DecorationIssueCode.EnvironmentTransformDrift },
            new object[] { "environment-extra-child", Phase6DecorationIssueCode.EnvironmentPrefabDrift },
            new object[] { "environment-extra-component", Phase6DecorationIssueCode.EnvironmentPrefabDrift },
            new object[] { "environment-prefab-extra-child", Phase6DecorationIssueCode.EnvironmentPrefabDrift },
            new object[] { "environment-prefab-extra-component", Phase6DecorationIssueCode.EnvironmentPrefabDrift },
            new object[] { "environment-prefab-override", Phase6DecorationIssueCode.EnvironmentPrefabDrift },
            new object[] { "environment-prefab-property-override", Phase6DecorationIssueCode.EnvironmentPrefabDrift },
            new object[] { "environment-child-order", Phase6DecorationIssueCode.EnvironmentPrefabDrift },
            new object[] { "decoration-owner-inventory", Phase6DecorationIssueCode.InvalidGridTransform },
            new object[] { "decoration-space-extra-child", Phase6DecorationIssueCode.InvalidGridTransform },
            new object[] { "decoration-space-extra-component", Phase6DecorationIssueCode.InvalidGridTransform },
            new object[] { "decoration-space-child-order", Phase6DecorationIssueCode.InvalidGridTransform },
            new object[] { "preview-root-transform", Phase6DecorationIssueCode.InvalidGridTransform },
            new object[] { "shared-task4-binding", Phase6DecorationIssueCode.MismatchedCatalogueBinding },
            new object[] { "shared-task5-binding", Phase6DecorationIssueCode.MissingUiReference },
            new object[] { "shared-task7-binding", Phase6DecorationIssueCode.MissingUiReference },
            new object[] { "event-system-parent", Phase6DecorationIssueCode.MissingEventSystem },
            new object[] { "input-module-owner", Phase6DecorationIssueCode.MissingInputSystemUiModule },
            new object[] { "wrong-input-actions", Phase6DecorationIssueCode.MissingInputActions },
            new object[] { "missing-point-action", Phase6DecorationIssueCode.MissingInputActions },
            new object[] { "wrong-content-catalogue", Phase6DecorationIssueCode.MismatchedCatalogueBinding },
            new object[] { "wrong-decoration-catalogue", Phase6DecorationIssueCode.MismatchedCatalogueBinding },
            new object[] { "decoration-catalogue-order", Phase6DecorationIssueCode.MismatchedCatalogueBinding },
            new object[] { "action-extra-child", Phase6DecorationIssueCode.MissingUiReference },
            new object[] { "modal-extra-component", Phase6DecorationIssueCode.MissingUiReference },
            new object[] { "safearea-extra-child", Phase6DecorationIssueCode.MissingUiReference },
            new object[] { "action-internal-active-drift", Phase6DecorationIssueCode.MissingUiReference },
            new object[] { "catalogue-internal-component", Phase6DecorationIssueCode.MissingUiReference }
        };
        [Test]
        public void ValidateAll_DeduplicatesExactIssuesAndUsesMessageOrdinalTieBreak()
        {
            var scope = new TargetFileScope();
            try
            {
                var report = new Phase6DecorationValidationReport(new[]
                {
                    Issue(Phase6DecorationIssueCode.MissingUiRoot, "B", "Same", "Zulu"),
                    Issue(Phase6DecorationIssueCode.MissingUiRoot, "B", "Same", "Alpha"),
                    Issue(Phase6DecorationIssueCode.MissingUiRoot, "B", "Same", "Alpha"),
                    Issue(Phase6DecorationIssueCode.DuplicateMainCamera, "B", "Same", "Same"),
                    Issue(Phase6DecorationIssueCode.MissingMainCamera, "B", "Same", "Same"),
                    Issue(Phase6DecorationIssueCode.MissingUiRoot, "B", "A", "Zulu"),
                    Issue(Phase6DecorationIssueCode.MissingUiRoot, "B", "Z", "Alpha"),
                    Issue(Phase6DecorationIssueCode.MissingCanvas, "A", "Later", "Message")
                });

                Assert.That(report.Issues, Has.Count.EqualTo(7));
                Assert.That(report.Issues.Select(IssueTuple), Is.EqualTo(new[]
                {
                    (Phase6DecorationIssueCode.MissingCanvas, "A", "Later", "Message"),
                    (Phase6DecorationIssueCode.MissingUiRoot, "B", "A", "Zulu"),
                    (Phase6DecorationIssueCode.MissingMainCamera, "B", "Same", "Same"),
                    (Phase6DecorationIssueCode.DuplicateMainCamera, "B", "Same", "Same"),
                    (Phase6DecorationIssueCode.MissingUiRoot, "B", "Same", "Alpha"),
                    (Phase6DecorationIssueCode.MissingUiRoot, "B", "Same", "Zulu"),
                    (Phase6DecorationIssueCode.MissingUiRoot, "B", "Z", "Alpha")
                }));
                Assert.Throws<NotSupportedException>(() =>
                    ((System.Collections.Generic.IList<Phase6DecorationValidationIssue>)report.Issues)
                    .Add(Issue(Phase6DecorationIssueCode.MissingUiRoot, "C", "X", "Y")));
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ValidateAll_IsReadOnlyAndDeterministic()
        {
            var scope = new TargetFileScope();
            try
            {
                var caller = ValidatorDirtyCallerFixture.Create(scope);
                var before = EditorStateSnapshot.Capture();

                var first = Phase6DecorationValidator.ValidateAll();
                var middle = EditorStateSnapshot.Capture();
                caller.AssertPreserved();
                var second = Phase6DecorationValidator.ValidateAll();
                var after = EditorStateSnapshot.Capture();
                caller.AssertPreserved();

                Assert.That(first.Issues.Select(issue => issue.ToString()),
                    Is.EqualTo(second.Issues.Select(issue => issue.ToString())));
                Assert.That(middle, Is.EqualTo(before));
                Assert.That(after, Is.EqualTo(before));
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(MainCafePath, Phase6DecorationIssueCode.MissingMainCafeScene)]
        [TestCase(ValidationPath, Phase6DecorationIssueCode.MissingValidationScene)]
        public void ValidateAll_ReportsMissingSceneFamilyWithStablePaths(
            string missingPath,
            Phase6DecorationIssueCode expectedCode)
        {
            var scope = new TargetFileScope();
            try
            {
                Phase6DecorationValidator.SceneExistsOverrideForTests = path =>
                    !string.Equals(path, missingPath, StringComparison.Ordinal);
                var report = Phase6DecorationValidator.ValidateAll();
                foreach (var issue in report.Issues)
                {
                    Assert.That(issue.AssetPath, Is.Not.Null.And.Not.Empty);
                    Assert.That(issue.ObjectPath, Is.Not.Null);
                    Assert.That(issue.Message, Is.Not.Null.And.Not.Empty);
                }

                var sceneIssue = report.Issues.Single(issue => issue.Code == expectedCode);
                Assert.That(sceneIssue.AssetPath, Is.EqualTo(missingPath));
                Assert.That(sceneIssue.ObjectPath, Is.EqualTo(string.Empty));
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ValidateAll_AcceptsPackageDefaultInputActionsGuid()
        {
            var scope = new TargetFileScope();
            try
            {
                WithValidationCandidate((scene, report) =>
                {
                    Assert.That(report.Issues.Select(issue => issue.Code),
                        Has.None.EqualTo(Phase6DecorationIssueCode.MissingInputActions));
                    var module = FindComponentsByFullName(
                        scene,
                        "UnityEngine.InputSystem.UI.InputSystemUIInputModule").Single();
                    var actionsAssetProperty = module.GetType().GetProperty("actionsAsset");
                    Assert.That(actionsAssetProperty, Is.Not.Null);
                    var actionsAsset = actionsAssetProperty.GetValue(module) as UnityEngine.Object;
                    Assert.That(AssetDatabase.AssetPathToGUID(
                        AssetDatabase.GetAssetPath(actionsAsset)),
                        Is.EqualTo("ca9f5fa95ffab41fb9a615ab714db018"));
                    var publicReport = PublishCandidateAndValidateAll(scene);
                    Assert.That(publicReport.Issues.Select(issue => issue.Code),
                        Has.None.EqualTo(Phase6DecorationIssueCode.MissingInputActions));
                });
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase("missing-main-camera", Phase6DecorationIssueCode.MissingMainCamera)]
        [TestCase("duplicate-main-camera", Phase6DecorationIssueCode.DuplicateMainCamera)]
        [TestCase("missing-light", Phase6DecorationIssueCode.MissingDirectionalLight)]
        [TestCase("duplicate-light", Phase6DecorationIssueCode.DuplicateDirectionalLight)]
        [TestCase("missing-phase0", Phase6DecorationIssueCode.MissingPhase0Runtime)]
        [TestCase("duplicate-phase0", Phase6DecorationIssueCode.DuplicatePhase0Runtime)]
        [TestCase("duplicate-mouse", Phase6DecorationIssueCode.DuplicateMouseCameraInput)]
        [TestCase("duplicate-camera-controller", Phase6DecorationIssueCode.DuplicateCafeCameraController)]
        [TestCase("duplicate-interaction", Phase6DecorationIssueCode.DuplicateSceneInteractionController)]
        [TestCase("runtime-location", Phase6DecorationIssueCode.RuntimeComponentLocationDrift)]
        [TestCase("camera-binding", Phase6DecorationIssueCode.CameraSettingsBindingDrift)]
        [TestCase("missing-environment", Phase6DecorationIssueCode.MissingEnvironmentRoot)]
        [TestCase("duplicate-environment", Phase6DecorationIssueCode.DuplicateEnvironmentRoot)]
        [TestCase("environment-root-transform", Phase6DecorationIssueCode.EnvironmentTransformDrift)]
        [TestCase("missing-floor", Phase6DecorationIssueCode.MissingEnvironmentPrefab)]
        [TestCase("wrong-floor", Phase6DecorationIssueCode.EnvironmentPrefabDrift)]
        [TestCase("floor-transform", Phase6DecorationIssueCode.EnvironmentTransformDrift)]
        [TestCase("grid-overlay", Phase6DecorationIssueCode.FloorGridOverlayStateDrift)]
        [TestCase("missing-owner", Phase6DecorationIssueCode.MissingDecorationOwner)]
        [TestCase("decoration-owner-transform", Phase6DecorationIssueCode.InvalidGridTransform)]
        [TestCase("decoration-space-transform", Phase6DecorationIssueCode.InvalidGridTransform)]
        [TestCase("missing-grid", Phase6DecorationIssueCode.MissingGridRoot)]
        [TestCase("duplicate-grid", Phase6DecorationIssueCode.DuplicateGridRoot)]
        [TestCase("invalid-grid", Phase6DecorationIssueCode.InvalidGridTransform)]
        [TestCase("missing-catalogue", Phase6DecorationIssueCode.MissingCatalogueBinding)]
        [TestCase("missing-ui-reference", Phase6DecorationIssueCode.MissingUiReference)]
        [TestCase("missing-ui-root", Phase6DecorationIssueCode.MissingUiRoot)]
        [TestCase("duplicate-ui-root", Phase6DecorationIssueCode.DuplicateUiRoot)]
        [TestCase("missing-canvas", Phase6DecorationIssueCode.MissingCanvas)]
        [TestCase("duplicate-canvas", Phase6DecorationIssueCode.DuplicateCanvas)]
        [TestCase("unexpected-canvas", Phase6DecorationIssueCode.UnexpectedCanvas)]
        [TestCase("missing-event-system", Phase6DecorationIssueCode.MissingEventSystem)]
        [TestCase("duplicate-event-system", Phase6DecorationIssueCode.DuplicateEventSystem)]
        [TestCase("missing-input-module", Phase6DecorationIssueCode.MissingInputSystemUiModule)]
        [TestCase("duplicate-input-module", Phase6DecorationIssueCode.DuplicateInputSystemUiModule)]
        [TestCase("missing-input-actions", Phase6DecorationIssueCode.MissingInputActions)]
        [TestCase("missing-time-panel", Phase6DecorationIssueCode.MissingTimePanel)]
        [TestCase("missing-contract-root", Phase6DecorationIssueCode.MissingContractReferenceRoot)]
        [TestCase("duplicate-contract-root", Phase6DecorationIssueCode.DuplicateContractReferenceRoot)]
        [TestCase("missing-hud-toggle", Phase6DecorationIssueCode.MissingHudToggle)]
        [TestCase("temporary-fixture", Phase6DecorationIssueCode.TemporaryFixturePresent)]
        [TestCase("unexpected-initial", Phase6DecorationIssueCode.UnexpectedInitialContent)]
        [TestCase("missing-definition", Phase6DecorationIssueCode.MissingDefinition)]
        [TestCase("missing-prefab", Phase6DecorationIssueCode.MissingPrefab)]
        [TestCase("missing-thumbnail", Phase6DecorationIssueCode.MissingThumbnail)]
        public void ValidateCandidateScene_ReportsEveryStructuralIssueFamily(
            string drift,
            Phase6DecorationIssueCode expected)
        {
            var scope = new TargetFileScope();
            Scene candidate = default;
            try
            {
                candidate = CreatePersistedValidationCandidate();
                ApplyCandidateDrift(candidate, drift, scope);

                var report = Phase6DecorationValidator.ValidateCandidateSceneForTests(
                    candidate,
                    Phase6SceneSetupTarget.Validation);

                Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain(expected),
                    drift);
            }
            finally
            {
                CloseCandidate(candidate);
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ValidateAll_ReportsMismatchedSharedContentAndDuplicateOwners()
        {
            var scope = new TargetFileScope();
            Scene candidate = default;
            try
            {
                candidate = CreatePersistedValidationCandidate();
                var owner = Find(candidate, "Phase6_DecorationRuntime");
                var duplicate = UnityEngine.Object.Instantiate(owner);
                duplicate.name = "Phase6_DecorationRuntime";
                SceneManager.MoveGameObjectToScene(duplicate, candidate);

                var controller = owner.GetComponent<DecorationModeController>();
                var emptyCatalog = ScriptableObject.CreateInstance<AnimalCafe.Content.FurnitureContentCatalog>();
                try
                {
                    SetObjectReference(controller, "contentCatalog", emptyCatalog);
                    var report = Phase6DecorationValidator.ValidateCandidateSceneForTests(
                        candidate,
                        Phase6SceneSetupTarget.Validation);

                    Assert.That(report.Issues.Select(issue => issue.Code),
                        Does.Contain(Phase6DecorationIssueCode.DuplicateDecorationOwner));
                    Assert.That(report.Issues.Select(issue => issue.Code),
                        Does.Contain(Phase6DecorationIssueCode.MismatchedCatalogueBinding));
                    var publicReport = PublishCandidateAndValidateAll(candidate);
                    AssertExactIssue(
                        publicReport,
                        Phase6DecorationIssueCode.DuplicateDecorationOwner,
                        ValidationPath,
                        "Phase6_DecorationRuntime");
                    AssertExactIssue(
                        publicReport,
                        Phase6DecorationIssueCode.MismatchedCatalogueBinding,
                        ValidationPath,
                        "Phase6_DecorationRuntime");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(emptyCatalog);
                }
            }
            finally
            {
                CloseCandidate(candidate);
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(typeof(GameTimeService), Phase6DecorationIssueCode.MissingGameTimeService)]
        [TestCase(typeof(AnimalCafe.Input.MouseCameraInput), Phase6DecorationIssueCode.MissingMouseCameraInput)]
        [TestCase(typeof(AnimalCafe.Camera.CafeCameraController), Phase6DecorationIssueCode.MissingCafeCameraController)]
        [TestCase(typeof(SceneInteractionController), Phase6DecorationIssueCode.MissingSceneInteractionController)]
        public void ValidateCandidateScene_ReportsMissingValidationBaseService(
            Type componentType,
            Phase6DecorationIssueCode expected)
        {
            var scope = new TargetFileScope();
            Scene candidate = default;
            try
            {
                candidate = CreatePersistedValidationCandidate();
                var runtime = Find(candidate, "Phase0_Runtime");
                UnityEngine.Object.DestroyImmediate(runtime.GetComponent(componentType));

                var report = Phase6DecorationValidator.ValidateCandidateSceneForTests(
                    candidate,
                    Phase6SceneSetupTarget.Validation);

                Assert.That(report.Issues.Select(issue => issue.Code), Does.Contain(expected));
                AssertExactIssue(
                    PublishCandidateAndValidateAll(candidate),
                    expected,
                    ValidationPath,
                    "Phase0_Runtime");
            }
            finally
            {
                CloseCandidate(candidate);
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ValidateAll_ReportsMissingDuplicateAndMislocatedValidationBaseServices()
        {
            var scope = new TargetFileScope();
            Scene candidate = default;
            try
            {
                WithValidationCandidate((missingScene, _) =>
                {
                    UnityEngine.Object.DestroyImmediate(
                        Find(missingScene, "Phase0_Runtime").GetComponent<MouseCameraInput>());
                    AssertExactIssue(
                        PublishCandidateAndValidateAll(missingScene),
                        Phase6DecorationIssueCode.MissingMouseCameraInput,
                        ValidationPath,
                        "Phase0_Runtime");
                });

                candidate = CreatePersistedValidationCandidate();
                var misplaced = new GameObject("MisplacedGameTime");
                SceneManager.MoveGameObjectToScene(misplaced, candidate);
                misplaced.AddComponent<GameTimeService>();

                var report = Phase6DecorationValidator.ValidateCandidateSceneForTests(
                    candidate,
                    Phase6SceneSetupTarget.Validation);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase6DecorationIssueCode.DuplicateGameTimeService));
                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase6DecorationIssueCode.RuntimeComponentLocationDrift));
                var publicReport = PublishCandidateAndValidateAll(candidate);
                AssertExactIssue(
                    publicReport,
                    Phase6DecorationIssueCode.DuplicateGameTimeService,
                    ValidationPath,
                    "MisplacedGameTime");
                AssertExactIssue(
                    publicReport,
                    Phase6DecorationIssueCode.RuntimeComponentLocationDrift,
                    ValidationPath,
                    "MisplacedGameTime");
            }
            finally
            {
                CloseCandidate(candidate);
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ValidateAll_ReportsUnexpectedStandaloneInputModule()
        {
            var scope = new TargetFileScope();
            Scene candidate = default;
            try
            {
                candidate = CreatePersistedValidationCandidate();
                Find(candidate, "EventSystem").AddComponent<StandaloneInputModule>();

                var report = Phase6DecorationValidator.ValidateCandidateSceneForTests(
                    candidate,
                    Phase6SceneSetupTarget.Validation);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase6DecorationIssueCode.UnexpectedStandaloneInputModule));
                AssertExactIssue(
                    PublishCandidateAndValidateAll(candidate),
                    Phase6DecorationIssueCode.UnexpectedStandaloneInputModule,
                    ValidationPath,
                    "UI Root/EventSystem");
            }
            finally
            {
                CloseCandidate(candidate);
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ValidateAll_ReportsMissingDuplicateOrMiswiredTimeControls()
        {
            var scope = new TargetFileScope();
            Scene candidate = default;
            try
            {
                candidate = CreatePersistedValidationCandidate();
                var panel = Find(candidate, "RightRail").GetComponent<TimeControlPanel>();
                SetObjectReference(panel, "pauseButton", null);

                var wiringReport = Phase6DecorationValidator.ValidateCandidateSceneForTests(
                    candidate,
                    Phase6SceneSetupTarget.Validation);
                AssertExactIssue(
                    wiringReport,
                    Phase6DecorationIssueCode.TimeControlWiringDrift,
                    ValidationPath,
                    "UI Root/HUD Canvas/HUD Layer/Decoration Safe Area/RightRail");
                AssertExactIssue(
                    PublishCandidateAndValidateAll(candidate),
                    Phase6DecorationIssueCode.TimeControlWiringDrift,
                    ValidationPath,
                    "UI Root/HUD Canvas/HUD Layer/Decoration Safe Area/RightRail");

                SetObjectReference(
                    panel,
                    "pauseButton",
                    Find(candidate, "PauseButton").GetComponent<Button>());
                var duplicate = UnityEngine.Object.Instantiate(
                    Find(candidate, "RightRail"),
                    Find(candidate, "Decoration Safe Area").transform);
                duplicate.name = "RightRail";
                Assert.That(candidate.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                        .Count(transform => transform.name == "RightRail"),
                    Is.EqualTo(2));

                var report = Phase6DecorationValidator.ValidateCandidateSceneForTests(
                    candidate,
                    Phase6SceneSetupTarget.Validation);

                AssertExactIssue(
                    report,
                    Phase6DecorationIssueCode.DuplicateTimePanel,
                    ValidationPath,
                    "UI Root/HUD Canvas/HUD Layer/Decoration Safe Area/RightRail");
                var publicReport = PublishCandidateAndValidateAll(candidate);
                AssertExactIssue(
                    publicReport,
                    Phase6DecorationIssueCode.DuplicateTimePanel,
                    ValidationPath,
                    "UI Root/HUD Canvas/HUD Layer/Decoration Safe Area/RightRail");
            }
            finally
            {
                CloseCandidate(candidate);
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase("child-order")]
        [TestCase("unknown-child")]
        [TestCase("extra-component")]
        [TestCase("rail-geometry")]
        [TestCase("child-spacing")]
        [TestCase("label-copy")]
        [TestCase("label-font")]
        [TestCase("raycast")]
        [TestCase("selected-binding")]
        public void ValidateAll_RejectsExactRightRailContractDrift(string drift)
        {
            var scope = new TargetFileScope();
            Scene candidate = default;
            try
            {
                candidate = CreatePersistedValidationCandidate();
                var rail = Find(candidate, "RightRail").transform;
                switch (drift)
                {
                    case "child-order":
                        rail.Find("FastButton").SetSiblingIndex(2);
                        break;
                    case "unknown-child":
                    {
                        var unexpected = new GameObject("UnexpectedRailChild", typeof(RectTransform));
                        unexpected.transform.SetParent(rail, false);
                        break;
                    }
                    case "extra-component":
                        rail.gameObject.AddComponent<CanvasGroup>();
                        break;
                    case "rail-geometry":
                        ((RectTransform)rail).sizeDelta += Vector2.one;
                        break;
                    case "child-spacing":
                        ((RectTransform)rail.Find("PauseButton")).anchoredPosition += Vector2.up;
                        break;
                    case "label-copy":
                        rail.Find("PauseButton/Label").GetComponent<TMP_Text>().text = "Stop";
                        break;
                    case "label-font":
                    {
                        var label = rail.Find("NormalButton/Label").GetComponent<TMP_Text>();
                        var wrongFont = UnityEngine.Object.Instantiate(label.font);
                        wrongFont.name = "WrongRightRailFont";
                        CreateOwnedAsset(wrongFont, "WrongRightRailFont", scope);
                        label.font = wrongFont;
                        break;
                    }
                    case "raycast":
                        rail.Find("FastButton/SelectedVisual").GetComponent<Image>().raycastTarget = true;
                        break;
                    case "selected-binding":
                        SetObjectReference(
                            rail.GetComponent<TimeControlPanel>(), "pauseSelectedVisual", null);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(drift), drift, null);
                }

                var report = Phase6DecorationValidator.ValidateCandidateSceneForTests(
                    candidate,
                    Phase6SceneSetupTarget.Validation);

                AssertExactIssue(
                    report,
                    Phase6DecorationIssueCode.TimeControlWiringDrift,
                    ValidationPath,
                    "UI Root/HUD Canvas/HUD Layer/Decoration Safe Area/RightRail");
                AssertExactIssue(
                    PublishCandidateAndValidateAll(candidate),
                    Phase6DecorationIssueCode.TimeControlWiringDrift,
                    ValidationPath,
                    "UI Root/HUD Canvas/HUD Layer/Decoration Safe Area/RightRail");
            }
            finally
            {
                CloseCandidate(candidate);
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ValidateAll_ReportsContractReferenceDriftAndGameplayBindings()
        {
            var scope = new TargetFileScope();
            Scene candidate = default;
            try
            {
                candidate = CreatePersistedValidationCandidate();
                var blocked = Find(candidate, "BlockedArea_ReferenceOnly");
                blocked.transform.localPosition += Vector3.right;
                blocked.AddComponent<BoxCollider>();

                var report = Phase6DecorationValidator.ValidateCandidateSceneForTests(
                    candidate,
                    Phase6SceneSetupTarget.Validation);

                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase6DecorationIssueCode.ContractReferenceDrift));
                Assert.That(report.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase6DecorationIssueCode.ContractReferenceGameplayBinding));
                var publicReport = PublishCandidateAndValidateAll(candidate);
                AssertExactIssue(
                    publicReport,
                    Phase6DecorationIssueCode.ContractReferenceDrift,
                    ValidationPath,
                    "Phase6_ContractReferences/BlockedArea_ReferenceOnly");
                AssertExactIssue(
                    publicReport,
                    Phase6DecorationIssueCode.ContractReferenceGameplayBinding,
                    ValidationPath,
                    "Phase6_ContractReferences/BlockedArea_ReferenceOnly");
            }
            finally
            {
                CloseCandidate(candidate);
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ValidateAll_RejectsSerializedFormalCounterButAllowsRuntimeEmptyRoot()
        {
            var scope = new TargetFileScope();
            Scene candidate = default;
            try
            {
                candidate = CreatePersistedValidationCandidate();
                var representation = Find(candidate, "FurnitureRepresentationRoot");
                var clean = Phase6DecorationValidator.ValidateCandidateSceneForTests(
                    candidate,
                    Phase6SceneSetupTarget.Validation);
                Assert.That(clean.Issues.Select(issue => issue.Code),
                    Has.None.EqualTo(Phase6DecorationIssueCode.UnexpectedSerializedRepresentation));
                Assert.That(PublishCandidateAndValidateAll(candidate).Issues
                    .Select(issue => issue.Code),
                    Has.None.EqualTo(Phase6DecorationIssueCode.UnexpectedSerializedRepresentation));

                var clone = new GameObject("SerializedCounterClone");
                clone.transform.SetParent(representation.transform, false);
                var dirty = Phase6DecorationValidator.ValidateCandidateSceneForTests(
                    candidate,
                    Phase6SceneSetupTarget.Validation);

                Assert.That(dirty.Issues.Select(issue => issue.Code),
                    Does.Contain(Phase6DecorationIssueCode.UnexpectedSerializedRepresentation));
                AssertExactIssue(
                    PublishCandidateAndValidateAll(candidate),
                    Phase6DecorationIssueCode.UnexpectedSerializedRepresentation,
                    ValidationPath,
                    "Phase6_DecorationRuntime/DecorationSpaceRoot/FurnitureRepresentationRoot");
            }
            finally
            {
                CloseCandidate(candidate);
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ValidateAll_ReportsRuntimeSaveApiBoundaryWithoutWritingOrRepairingAnything()
        {
            var scope = new TargetFileScope();
            var before = EditorStateSnapshot.Capture();
            try
            {
                var report = Phase6DecorationValidator.ValidateAll();
                var after = EditorStateSnapshot.Capture();

                Assert.That(after, Is.EqualTo(before));
                Assert.That(report.Issues.Where(issue =>
                        issue.Code == Phase6DecorationIssueCode.RuntimeEditorReference
                        || issue.Code == Phase6DecorationIssueCode.SaveBoundaryViolation),
                    Is.Empty,
                    "The accepted Task 7 runtime contains neither UnityEditor nor a persistence writer API.");
            }
            finally
            {
                before.RestoreSelectionOnly();
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ValidateAll_PublicSourceScanReportsInjectedRuntimeEditorAndSaveBoundaries()
        {
            var scope = new TargetFileScope();
            try
            {
                Phase6DecorationValidator.RuntimeSourceOverrideForTests =
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["Assets/Scripts/Decoration/__Task8InjectedRuntimeViolation.cs"] =
                            "using UnityEditor; class Bad { void Save() { "
                            + "System.IO.File.WriteAllText(\"save.json\", \"x\"); } }"
                    };

                var report = Phase6DecorationValidator.ValidateAll();

                const string sourcePath =
                    "Assets/Scripts/Decoration/__Task8InjectedRuntimeViolation.cs";
                AssertExactIssue(
                    report,
                    Phase6DecorationIssueCode.RuntimeEditorReference,
                    sourcePath,
                    string.Empty);
                AssertExactIssue(
                    report,
                    Phase6DecorationIssueCode.SaveBoundaryViolation,
                    sourcePath,
                    string.Empty);
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ValidateAll_PublicBuildSettingsScanReportsInjectedScopeDrift()
        {
            var scope = new TargetFileScope();
            try
            {
                Phase6DecorationValidator.BuildSettingsOverrideForTests = new[]
                {
                    new EditorBuildSettingsScene(ValidationPath, true),
                    new EditorBuildSettingsScene(MainCafePath, false)
                };

                var report = Phase6DecorationValidator.ValidateAll();

                AssertExactIssue(
                    report,
                    Phase6DecorationIssueCode.BuildSettingsScopeDrift,
                    "ProjectSettings/EditorBuildSettings.asset",
                    string.Empty);
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ValidateAll_PublicMissingScriptScanReportsInjectedObjectPath()
        {
            var scope = new TargetFileScope();
            try
            {
                Phase6DecorationValidator.MissingScriptPathsOverrideForTests = new[]
                {
                    ValidationPath + "|Phase6_DecorationRuntime/BrokenComponent"
                };

                var report = Phase6DecorationValidator.ValidateAll();
                var issue = report.Issues.Single(candidate =>
                    candidate.Code == Phase6DecorationIssueCode.MissingScript);
                Assert.That(issue.AssetPath, Is.EqualTo(ValidationPath));
                Assert.That(issue.ObjectPath,
                    Is.EqualTo("Phase6_DecorationRuntime/BrokenComponent"));
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void Validator_SourceContainsNoSetupBuilderSaveRefreshOrBuildSettingsWriter()
        {
            var scope = new TargetFileScope();
            try
            {
                const string path = "Assets/Editor/Phase6/Phase6DecorationValidator.cs";
                var source = File.ReadAllText(path);
                foreach (var pattern in new[]
                         {
                             @"EditorSceneManager\s*\.\s*SaveScene\s*\(",
                             @"AssetDatabase\s*\.\s*SaveAssets\s*\(",
                             @"AssetDatabase\s*\.\s*SaveAssetIfDirty\s*\(",
                             @"AssetDatabase\s*\.\s*Refresh\s*\(",
                             @"EditorBuildSettings\s*\.\s*scenes\s*=",
                             @"Phase6DecorationSceneSetup\s*\.\s*Configure",
                             @"Phase6DecorationAssetBuilder\s*\.\s*Build",
                             @"Phase5UiAssetBuilder\s*\.\s*Build",
                             @"Phase0SceneSetup\s*\.\s*Configure"
                         })
                {
                    Assert.That(Regex.IsMatch(source, pattern, RegexOptions.CultureInvariant),
                        Is.False,
                        path + " contains forbidden mutating token pattern " + pattern);
                }
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase("missing-main-camera", Phase6DecorationIssueCode.MissingMainCamera)]
        [TestCase("duplicate-main-camera", Phase6DecorationIssueCode.DuplicateMainCamera)]
        [TestCase("missing-light", Phase6DecorationIssueCode.MissingDirectionalLight)]
        [TestCase("duplicate-light", Phase6DecorationIssueCode.DuplicateDirectionalLight)]
        [TestCase("missing-phase0", Phase6DecorationIssueCode.MissingPhase0Runtime)]
        [TestCase("duplicate-phase0", Phase6DecorationIssueCode.DuplicatePhase0Runtime)]
        [TestCase("duplicate-mouse", Phase6DecorationIssueCode.DuplicateMouseCameraInput)]
        [TestCase("duplicate-camera-controller", Phase6DecorationIssueCode.DuplicateCafeCameraController)]
        [TestCase("duplicate-interaction", Phase6DecorationIssueCode.DuplicateSceneInteractionController)]
        [TestCase("runtime-location", Phase6DecorationIssueCode.RuntimeComponentLocationDrift)]
        [TestCase("camera-binding", Phase6DecorationIssueCode.CameraSettingsBindingDrift)]
        [TestCase("missing-environment", Phase6DecorationIssueCode.MissingEnvironmentRoot)]
        [TestCase("duplicate-environment", Phase6DecorationIssueCode.DuplicateEnvironmentRoot)]
        [TestCase("environment-root-transform", Phase6DecorationIssueCode.EnvironmentTransformDrift)]
        [TestCase("missing-floor", Phase6DecorationIssueCode.MissingEnvironmentPrefab)]
        [TestCase("wrong-floor", Phase6DecorationIssueCode.EnvironmentPrefabDrift)]
        [TestCase("floor-transform", Phase6DecorationIssueCode.EnvironmentTransformDrift)]
        [TestCase("grid-overlay", Phase6DecorationIssueCode.FloorGridOverlayStateDrift)]
        [TestCase("missing-owner", Phase6DecorationIssueCode.MissingDecorationOwner)]
        [TestCase("decoration-owner-transform", Phase6DecorationIssueCode.InvalidGridTransform)]
        [TestCase("decoration-space-transform", Phase6DecorationIssueCode.InvalidGridTransform)]
        [TestCase("missing-grid", Phase6DecorationIssueCode.MissingGridRoot)]
        [TestCase("duplicate-grid", Phase6DecorationIssueCode.DuplicateGridRoot)]
        [TestCase("invalid-grid", Phase6DecorationIssueCode.InvalidGridTransform)]
        [TestCase("missing-catalogue", Phase6DecorationIssueCode.MissingCatalogueBinding)]
        [TestCase("missing-ui-reference", Phase6DecorationIssueCode.MissingUiReference)]
        [TestCase("missing-ui-root", Phase6DecorationIssueCode.MissingUiRoot)]
        [TestCase("duplicate-ui-root", Phase6DecorationIssueCode.DuplicateUiRoot)]
        [TestCase("missing-canvas", Phase6DecorationIssueCode.MissingCanvas)]
        [TestCase("duplicate-canvas", Phase6DecorationIssueCode.DuplicateCanvas)]
        [TestCase("unexpected-canvas", Phase6DecorationIssueCode.UnexpectedCanvas)]
        [TestCase("missing-event-system", Phase6DecorationIssueCode.MissingEventSystem)]
        [TestCase("duplicate-event-system", Phase6DecorationIssueCode.DuplicateEventSystem)]
        [TestCase("missing-input-module", Phase6DecorationIssueCode.MissingInputSystemUiModule)]
        [TestCase("duplicate-input-module", Phase6DecorationIssueCode.DuplicateInputSystemUiModule)]
        [TestCase("missing-input-actions", Phase6DecorationIssueCode.MissingInputActions)]
        [TestCase("missing-time-panel", Phase6DecorationIssueCode.MissingTimePanel)]
        [TestCase("missing-contract-root", Phase6DecorationIssueCode.MissingContractReferenceRoot)]
        [TestCase("duplicate-contract-root", Phase6DecorationIssueCode.DuplicateContractReferenceRoot)]
        [TestCase("missing-hud-toggle", Phase6DecorationIssueCode.MissingHudToggle)]
        [TestCase("temporary-fixture", Phase6DecorationIssueCode.TemporaryFixturePresent)]
        [TestCase("unexpected-initial", Phase6DecorationIssueCode.UnexpectedInitialContent)]
        [TestCase("missing-definition", Phase6DecorationIssueCode.MissingDefinition)]
        [TestCase("missing-prefab", Phase6DecorationIssueCode.MissingPrefab)]
        [TestCase("missing-thumbnail", Phase6DecorationIssueCode.MissingThumbnail)]
        public void ValidateAll_PublicPersistedSceneReportsRepresentativePositiveFamilies(
            string drift,
            Phase6DecorationIssueCode expected)
        {
            var scope = new TargetFileScope();
            Scene candidate = default;
            try
            {
                candidate = CreatePersistedValidationCandidate();
                ApplyCandidateDrift(candidate, drift, scope);
                var expectedLocation = ExpectedLocationForDrift(candidate, drift);
                Assert.That(EditorSceneManager.SaveScene(
                    candidate,
                    ValidationPath,
                    false), Is.True);
                EditorSceneManager.CloseScene(candidate, true);
                candidate = default;

                var report = Phase6DecorationValidator.ValidateAll();

                Assert.That(report.Issues.Any(issue =>
                        issue.Code == expected
                        && issue.AssetPath == expectedLocation.AssetPath
                        && issue.ObjectPath == expectedLocation.ObjectPath),
                    Is.True,
                    $"{drift} must report the exact persisted asset/object path.");
            }
            finally
            {
                CloseCandidate(candidate);
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCaseSource(nameof(CandidateParityCases))]
        public void ValidateCandidateScene_UsesTheSameRulesAsPersistedValidateAll(
            string drift,
            Phase6DecorationIssueCode expected)
        {
            var scope = new TargetFileScope();
            Scene candidate = default;
            try
            {
                candidate = CreatePersistedValidationCandidate();
                ApplyCandidateDrift(candidate, drift, scope);
                var expectedLocation = ExpectedLocationForDrift(candidate, drift);
                var oldHandle = candidate.handle;
                Assert.That(EditorSceneManager.SaveScene(
                    candidate,
                    ValidationPath,
                    false), Is.True);
                Assert.That(EditorSceneManager.CloseScene(candidate, true), Is.True);
                Assert.That(Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt)
                    .Any(scene => scene.handle == oldHandle), Is.False);
                candidate = default;
                var persistedReport = Phase6DecorationValidator.ValidateAll();
                candidate = EditorSceneManager.OpenScene(
                    ValidationPath,
                    OpenSceneMode.Additive);
                Assert.That(candidate.handle, Is.Not.EqualTo(oldHandle));
                var candidateIssues = Phase6DecorationValidator.ValidateCandidateSceneForTests(
                    candidate,
                    Phase6SceneSetupTarget.Validation).Issues
                    .Select(IssueTuple)
                    .ToArray();
                var candidateAssetPaths = candidateIssues
                    .Select(tuple => tuple.AssetPath)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                var persistedIssues = persistedReport.Issues
                    .Where(issue => candidateAssetPaths.Contains(
                        issue.AssetPath,
                        StringComparer.Ordinal))
                    .Select(IssueTuple)
                    .ToArray();

                Assert.That(persistedIssues, Is.EqualTo(candidateIssues));
                Assert.That(persistedReport.Issues.Any(issue =>
                        issue.Code == expected
                        && issue.AssetPath == expectedLocation.AssetPath
                        && issue.ObjectPath == expectedLocation.ObjectPath),
                    Is.True,
                    $"{drift} must retain its exact persisted public issue location.");
            }
            finally
            {
                CloseCandidate(candidate);
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCaseSource(nameof(ReviewerContractCases))]
        public void ValidateCandidateAndPublic_ReportsCanonicalBindingsAndOwnedManifestDrift(
            string drift,
            Phase6DecorationIssueCode expected)
        {
            var scope = new TargetFileScope();
            Scene candidate = default;
            try
            {
                candidate = CreatePersistedValidationCandidate();
                ApplyCandidateDrift(candidate, drift, scope);
                var expectedLocation = ExpectedLocationForDrift(candidate, drift);

                var candidateReport = Phase6DecorationValidator.ValidateCandidateSceneForTests(
                    candidate,
                    Phase6SceneSetupTarget.Validation);
                AssertExactIssue(
                    candidateReport,
                    expected,
                    expectedLocation.AssetPath,
                    expectedLocation.ObjectPath);

                Assert.That(EditorSceneManager.SaveScene(candidate), Is.True);
                Assert.That(EditorSceneManager.CloseScene(candidate, true), Is.True);
                candidate = default;
                var publicReport = Phase6DecorationValidator.ValidateAll();
                AssertExactIssue(
                    publicReport,
                    expected,
                    expectedLocation.AssetPath,
                    expectedLocation.ObjectPath);
            }
            finally
            {
                CloseCandidate(candidate);
                ClearSeams();
                scope.Dispose();
            }
        }

        private static void ApplyCandidateDrift(
            Scene scene,
            string drift,
            TargetFileScope scope)
        {
            GameObject Duplicate(string name)
            {
                var duplicate = UnityEngine.Object.Instantiate(Find(scene, name));
                duplicate.name = name;
                SceneManager.MoveGameObjectToScene(duplicate, scene);
                return duplicate;
            }

            switch (drift)
            {
                case "missing-main-camera":
                    UnityEngine.Object.DestroyImmediate(Find(scene, "Main Camera"));
                    return;
                case "duplicate-main-camera":
                    Duplicate("Main Camera");
                    return;
                case "missing-light":
                    UnityEngine.Object.DestroyImmediate(Find(scene, "Directional Light"));
                    return;
                case "duplicate-light":
                    Duplicate("Directional Light");
                    return;
                case "missing-phase0":
                    UnityEngine.Object.DestroyImmediate(Find(scene, "Phase0_Runtime"));
                    return;
                case "duplicate-phase0":
                    Duplicate("Phase0_Runtime");
                    return;
                case "duplicate-mouse":
                    Find(scene, "Phase0_Runtime").AddComponent<MouseCameraInput>();
                    return;
                case "duplicate-camera-controller":
                    Find(scene, "Phase0_Runtime").AddComponent<CafeCameraController>();
                    return;
                case "duplicate-interaction":
                    Find(scene, "Phase0_Runtime").AddComponent<SceneInteractionController>();
                    return;
                case "runtime-location":
                {
                    var misplaced = new GameObject("MislocatedRuntime");
                    SceneManager.MoveGameObjectToScene(misplaced, scene);
                    misplaced.AddComponent<GameTimeService>();
                    return;
                }
                case "camera-binding":
                    SetObjectReference(
                        Find(scene, "Phase0_Runtime").GetComponent<CafeCameraController>(),
                        "settings",
                        null);
                    return;
                case "missing-environment":
                    UnityEngine.Object.DestroyImmediate(Find(scene, "P4_Environment"));
                    return;
                case "duplicate-environment":
                    Duplicate("P4_Environment");
                    return;
                case "environment-root-transform":
                    Find(scene, "P4_Environment").transform.localPosition =
                        new Vector3(0.5f, 0f, 0.25f);
                    return;
                case "missing-floor":
                    UnityEngine.Object.DestroyImmediate(Find(scene, "P4_Floor_8x8"));
                    return;
                case "wrong-floor":
                {
                    var old = Find(scene, "P4_Floor_8x8");
                    var parent = old.transform.parent;
                    UnityEngine.Object.DestroyImmediate(old);
                    var substitute = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    substitute.name = "P4_Floor_8x8";
                    substitute.transform.SetParent(parent, false);
                    return;
                }
                case "floor-transform":
                    Find(scene, "P4_Floor_8x8").transform.localPosition = Vector3.right;
                    return;
                case "grid-overlay":
                    Find(scene, "P4_Floor_8x8").transform.Find("GridOverlay")
                        .gameObject.SetActive(true);
                    return;
                case "missing-window":
                    UnityEngine.Object.DestroyImmediate(Find(scene, "P4_Window_BackRight_C3_R0"));
                    return;
                case "wrong-window":
                {
                    var old = Find(scene, "P4_Window_BackRight_C3_R0");
                    var parent = old.transform.parent;
                    UnityEngine.Object.DestroyImmediate(old);
                    var substitute = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    substitute.name = "P4_Window_BackRight_C3_R0";
                    substitute.transform.SetParent(parent, false);
                    return;
                }
                case "window-transform":
                    Find(scene, "P4_Window_BackRight_C3_R0").transform.localPosition +=
                        new Vector3(0.25f, 0f, 0f);
                    return;
                case "environment-extra-child":
                {
                    var extra = new GameObject("Task8_ExtraEnvironmentChild");
                    extra.transform.SetParent(Find(scene, "P4_Environment").transform, false);
                    return;
                }
                case "environment-extra-component":
                    Find(scene, "P4_Environment").AddComponent<BoxCollider>();
                    return;
                case "environment-prefab-extra-child":
                {
                    var extra = new GameObject("Task8_ExtraWallChild");
                    extra.transform.SetParent(Find(scene, "P4_Wall_BackLeft").transform, false);
                    return;
                }
                case "environment-prefab-extra-component":
                    Find(scene, "P4_Wall_BackLeft").AddComponent<BoxCollider>();
                    return;
                case "environment-prefab-override":
                    Find(scene, "P4_Floor_8x8").transform.Find("GridOverlay")
                        .localPosition += Vector3.up;
                    return;
                case "environment-prefab-property-override":
                {
                    var gridLine = Find(scene, "P4_Floor_8x8").transform.Find("GridOverlay")
                        .GetChild(0);
                    gridLine.gameObject.SetActive(!gridLine.gameObject.activeSelf);
                    return;
                }
                case "environment-child-order":
                    Find(scene, "P4_Entrance").transform.SetSiblingIndex(0);
                    return;
                case "missing-owner":
                    UnityEngine.Object.DestroyImmediate(Find(scene, "Phase6_DecorationRuntime"));
                    return;
                case "decoration-owner-transform":
                    Find(scene, "Phase6_DecorationRuntime").transform.localRotation =
                        Quaternion.Euler(0f, 15f, 0f);
                    return;
                case "decoration-space-transform":
                    Find(scene, "DecorationSpaceRoot").transform.localScale =
                        new Vector3(1.25f, 1f, 1f);
                    return;
                case "missing-grid":
                    UnityEngine.Object.DestroyImmediate(Find(scene, "GridVisualRoot"));
                    return;
                case "duplicate-grid":
                {
                    var grid = new GameObject("GridVisualRoot");
                    grid.transform.SetParent(Find(scene, "DecorationSpaceRoot").transform, false);
                    return;
                }
                case "invalid-grid":
                    Find(scene, "GridVisualRoot").transform.localPosition = Vector3.up;
                    return;
                case "decoration-owner-inventory":
                    Find(scene, "Phase6_DecorationRuntime").AddComponent<BoxCollider>();
                    return;
                case "decoration-space-extra-child":
                {
                    var extra = new GameObject("Task8_ExtraDecorationChild");
                    extra.transform.SetParent(Find(scene, "DecorationSpaceRoot").transform, false);
                    return;
                }
                case "decoration-space-extra-component":
                    Find(scene, "DecorationSpaceRoot").AddComponent<BoxCollider>();
                    return;
                case "decoration-space-child-order":
                    Find(scene, "FurniturePreviewRoot").transform.SetSiblingIndex(0);
                    return;
                case "preview-root-transform":
                    Find(scene, "FurniturePreviewRoot").transform.localRotation =
                        Quaternion.Euler(0f, 20f, 0f);
                    return;
                case "shared-task4-binding":
                    SetObjectReference(
                        Find(scene, "Phase6_DecorationRuntime").GetComponent<CafeLayoutRuntime>(),
                        "entrancePortal",
                        null);
                    return;
                case "shared-task5-binding":
                    SetObjectReference(
                        FindAll<DecorationModeController>(scene).Single(),
                        "uiTheme",
                        null);
                    return;
                case "shared-task7-binding":
                    SetObjectReference(
                        FindAll<DecorationModeController>(scene).Single(),
                        "previewView",
                        null);
                    return;
                case "missing-catalogue":
                    SetObjectReference(
                        FindAll<DecorationModeController>(scene).Single(),
                        "catalogueAsset",
                        null);
                    return;
                case "missing-ui-reference":
                    SetObjectReference(
                        FindAll<DecorationModeController>(scene).Single(),
                        "catalogueView",
                        null);
                    return;
                case "missing-ui-root":
                    UnityEngine.Object.DestroyImmediate(Find(scene, "UI Root"));
                    return;
                case "duplicate-ui-root":
                    Duplicate("UI Root");
                    return;
                case "missing-canvas":
                    UnityEngine.Object.DestroyImmediate(Find(scene, "HUD Canvas"));
                    return;
                case "duplicate-canvas":
                {
                    var canvas = new GameObject("HUD Canvas", typeof(RectTransform), typeof(Canvas));
                    canvas.transform.SetParent(Find(scene, "UI Root").transform, false);
                    return;
                }
                case "unexpected-canvas":
                {
                    var canvas = new GameObject(
                        "Unexpected Canvas",
                        typeof(RectTransform),
                        typeof(Canvas));
                    canvas.transform.SetParent(Find(scene, "UI Root").transform, false);
                    return;
                }
                case "missing-event-system":
                    UnityEngine.Object.DestroyImmediate(Find(scene, "EventSystem"));
                    return;
                case "duplicate-event-system":
                {
                    var duplicate = new GameObject("EventSystem", typeof(EventSystem));
                    duplicate.transform.SetParent(Find(scene, "UI Root").transform, false);
                    return;
                }
                case "missing-input-module":
                    UnityEngine.Object.DestroyImmediate(FindComponentsByFullName(
                        scene,
                        "UnityEngine.InputSystem.UI.InputSystemUIInputModule").Single());
                    return;
                case "duplicate-input-module":
                {
                    var module = FindComponentsByFullName(
                        scene,
                        "UnityEngine.InputSystem.UI.InputSystemUIInputModule").Single();
                    Find(scene, "EventSystem").AddComponent(module.GetType());
                    return;
                }
                case "missing-input-actions":
                {
                    var module = FindComponentsByFullName(
                        scene,
                        "UnityEngine.InputSystem.UI.InputSystemUIInputModule").Single();
                    module.GetType().GetProperty("actionsAsset")?.SetValue(module, null);
                    return;
                }
                case "event-system-parent":
                    Find(scene, "EventSystem").transform.SetParent(
                        Find(scene, "HUD Layer").transform,
                        false);
                    return;
                case "input-module-owner":
                {
                    var module = FindComponentsByFullName(
                        scene,
                        "UnityEngine.InputSystem.UI.InputSystemUIInputModule").Single();
                    var moduleType = module.GetType();
                    UnityEngine.Object.DestroyImmediate(module);
                    Find(scene, "UI Root").AddComponent(moduleType);
                    return;
                }
                case "wrong-input-actions":
                {
                    var module = FindComponentsByFullName(
                        scene,
                        "UnityEngine.InputSystem.UI.InputSystemUIInputModule").Single();
                    var actionsProperty = module.GetType().GetProperty("actionsAsset");
                    var canonical = actionsProperty?.GetValue(module) as ScriptableObject;
                    Assert.That(canonical, Is.Not.Null);
                    var actions = ScriptableObject.CreateInstance(canonical.GetType());
                    CreateOwnedAsset(actions, "WrongInputActions", scope);
                    actionsProperty.SetValue(module, actions);
                    return;
                }
                case "missing-point-action":
                {
                    var module = FindComponentsByFullName(
                        scene,
                        "UnityEngine.InputSystem.UI.InputSystemUIInputModule").Single();
                    module.GetType().GetProperty("point")?.SetValue(module, null);
                    return;
                }
                case "missing-time-panel":
                    UnityEngine.Object.DestroyImmediate(Find(scene, "RightRail"));
                    return;
                case "missing-contract-root":
                    UnityEngine.Object.DestroyImmediate(Find(scene, "Phase6_ContractReferences"));
                    return;
                case "duplicate-contract-root":
                    Duplicate("Phase6_ContractReferences");
                    return;
                case "missing-hud-toggle":
                    SetObjectReference(
                        FindAll<DecorationModeController>(scene).Single(),
                        "decorationModeButton",
                        null);
                    return;
                case "temporary-fixture":
                {
                    var temporary = new GameObject(
                        "TEMP_P4_ManualReviewFixtures_DELETE_LATER");
                    SceneManager.MoveGameObjectToScene(temporary, scene);
                    return;
                }
                case "unexpected-initial":
                {
                    var unexpected = new GameObject("PF_Furniture_WorkTable_01");
                    SceneManager.MoveGameObjectToScene(unexpected, scene);
                    return;
                }
                case "missing-definition":
                case "missing-thumbnail":
                {
                    var catalogue = ScriptableObject.CreateInstance<DecorationCatalogueAsset>();
                    var serialized = new SerializedObject(catalogue);
                    var entries = serialized.FindProperty("entries");
                    entries.arraySize = 1;
                    var entry = entries.GetArrayElementAtIndex(0);
                    if (drift == "missing-thumbnail")
                    {
                        var canonical = (DecorationCatalogueAsset)ReadObjectReference(
                            FindAll<DecorationModeController>(scene).Single(),
                            "catalogueAsset");
                        entry.FindPropertyRelative("definition").objectReferenceValue =
                            canonical.Entries[0].Definition;
                    }
                    entry.FindPropertyRelative("thumbnail").objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    CreateOwnedAsset(catalogue, "DecorationCatalogue", scope);
                    SetObjectReference(
                        FindAll<DecorationModeController>(scene).Single(),
                        "catalogueAsset",
                        catalogue);
                    return;
                }
                case "missing-prefab":
                {
                    var definition = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
                    var definitionSerialized = new SerializedObject(definition);
                    definitionSerialized.FindProperty("definitionId").stringValue =
                        "furniture.test.missing-prefab";
                    definitionSerialized.FindProperty("displayName").stringValue = "Missing Prefab";
                    definitionSerialized.FindProperty("footprintWidth").intValue = 1;
                    definitionSerialized.FindProperty("footprintDepth").intValue = 1;
                    definitionSerialized.FindProperty("allowedPlacementSurfaces").intValue = 1;
                    definitionSerialized.ApplyModifiedPropertiesWithoutUndo();
                    CreateOwnedAsset(definition, "MissingPrefabDefinition", scope);
                    var catalog = ScriptableObject.CreateInstance<FurnitureContentCatalog>();
                    var catalogSerialized = new SerializedObject(catalog);
                    var entries = catalogSerialized.FindProperty("entries");
                    entries.arraySize = 1;
                    entries.GetArrayElementAtIndex(0).objectReferenceValue = definition;
                    catalogSerialized.ApplyModifiedPropertiesWithoutUndo();
                    CreateOwnedAsset(catalog, "FurnitureContentCatalog", scope);
                    var owner = Find(scene, "Phase6_DecorationRuntime");
                    SetObjectReference(owner.GetComponent<CafeLayoutRuntime>(),
                        "contentCatalog", catalog);
                    SetObjectReference(owner.GetComponent<DecorationModeController>(),
                        "contentCatalog", catalog);
                    return;
                }
                case "wrong-content-catalogue":
                {
                    var catalog = ScriptableObject.CreateInstance<FurnitureContentCatalog>();
                    CreateOwnedAsset(catalog, "WrongFurnitureContentCatalog", scope);
                    var owner = Find(scene, "Phase6_DecorationRuntime");
                    SetObjectReference(owner.GetComponent<CafeLayoutRuntime>(),
                        "contentCatalog", catalog);
                    SetObjectReference(owner.GetComponent<DecorationModeController>(),
                        "contentCatalog", catalog);
                    return;
                }
                case "wrong-decoration-catalogue":
                {
                    var catalogue = ScriptableObject.CreateInstance<DecorationCatalogueAsset>();
                    CreateOwnedAsset(catalogue, "WrongDecorationCatalogue", scope);
                    SetObjectReference(
                        FindAll<DecorationModeController>(scene).Single(),
                        "catalogueAsset",
                        catalogue);
                    return;
                }
                case "decoration-catalogue-order":
                {
                    var canonical = (DecorationCatalogueAsset)ReadObjectReference(
                        FindAll<DecorationModeController>(scene).Single(),
                        "catalogueAsset");
                    var catalogue = ScriptableObject.CreateInstance<DecorationCatalogueAsset>();
                    var serialized = new SerializedObject(catalogue);
                    var entries = serialized.FindProperty("entries");
                    entries.arraySize = canonical.Entries.Count;
                    for (var index = 0; index < canonical.Entries.Count; index++)
                    {
                        var source = canonical.Entries[canonical.Entries.Count - 1 - index];
                        var entry = entries.GetArrayElementAtIndex(index);
                        entry.FindPropertyRelative("definition").objectReferenceValue =
                            source.Definition;
                        entry.FindPropertyRelative("thumbnail").objectReferenceValue =
                            source.Thumbnail;
                    }
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    CreateOwnedAsset(catalogue, "ReorderedDecorationCatalogue", scope);
                    SetObjectReference(
                        FindAll<DecorationModeController>(scene).Single(),
                        "catalogueAsset",
                        catalogue);
                    return;
                }
                case "action-extra-child":
                {
                    var extra = new GameObject("Task8_ExtraActionChild");
                    extra.transform.SetParent(
                        Find(scene, "PF_UI_DecorationActionBar").transform,
                        false);
                    return;
                }
                case "modal-extra-component":
                    Find(scene, "PF_UI_DecorationStoreModal").AddComponent<BoxCollider>();
                    return;
                case "safearea-extra-child":
                {
                    var extra = new GameObject("Task8_ExtraSafeAreaChild", typeof(RectTransform));
                    extra.transform.SetParent(Find(scene, "Decoration Safe Area").transform, false);
                    return;
                }
                case "action-internal-active-drift":
                    Find(scene, "ActionPanel").SetActive(false);
                    return;
                case "catalogue-internal-component":
                    Find(scene, "ExpandedSheet").AddComponent<BoxCollider>();
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(drift), drift, null);
            }
        }

        private static UnityEngine.Object ReadObjectReference(
            UnityEngine.Object owner,
            string fieldName)
        {
            var serialized = new SerializedObject(owner);
            var property = serialized.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null, fieldName);
            return property.objectReferenceValue;
        }

        private static Scene CreatePersistedValidationCandidate()
        {
            var loaded = SceneManager.GetSceneByPath(ValidationPath);
            Assert.That(!loaded.IsValid() || !loaded.isLoaded, Is.True,
                "The test safety scope requires the Validation target to be caller-closed.");
            if (File.Exists(ValidationPath)
                || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ValidationPath) != null)
            {
                Assert.That(AssetDatabase.DeleteAsset(ValidationPath), Is.True,
                    "The backed-up Validation target must be removed through AssetDatabase.");
            }

            Assert.That(File.Exists(ValidationPath), Is.False);
            Assert.That(File.Exists(ValidationPath + ".meta"), Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ValidationPath), Is.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(
                ValidationPath,
                AssetPathToGUIDOptions.OnlyExistingAssets), Is.Empty);
            return Phase6DecorationSceneSetup.CreatePersistedValidationCandidateForTests();
        }

        private static void WithValidationCandidate(
            Action<Scene, Phase6DecorationValidationReport> assertion)
        {
            Scene candidate = default;
            try
            {
                candidate = CreatePersistedValidationCandidate();
                var report = Phase6DecorationValidator.ValidateCandidateSceneForTests(
                    candidate,
                    Phase6SceneSetupTarget.Validation);
                assertion(candidate, report);
            }
            finally
            {
                CloseCandidate(candidate);
            }
        }

        private static Phase6DecorationValidationReport PublishCandidateAndValidateAll(
            Scene candidate)
        {
            Assert.That(EditorSceneManager.SaveScene(
                candidate,
                ValidationPath,
                false), Is.True);
            return Phase6DecorationValidator.ValidateAll();
        }

        private static (
            Phase6DecorationIssueCode Code,
            string AssetPath,
            string ObjectPath,
            string Message) IssueTuple(Phase6DecorationValidationIssue issue) =>
            (issue.Code, issue.AssetPath, issue.ObjectPath, issue.Message);

        private static (string AssetPath, string ObjectPath) ExpectedLocationForDrift(
            Scene scene,
            string drift)
        {
            if (drift == "missing-definition" || drift == "missing-thumbnail")
            {
                var catalogue = ReadObjectReference(
                    FindAll<DecorationModeController>(scene).Single(),
                    "catalogueAsset");
                return (
                    AssetDatabase.GetAssetPath(catalogue),
                    drift == "missing-definition"
                        ? "entries[0].definition"
                        : "entries[0].thumbnail");
            }

            if (drift == "missing-prefab")
            {
                var owner = Find(scene, "Phase6_DecorationRuntime");
                var content = ReadObjectReference(
                    owner.GetComponent<CafeLayoutRuntime>(),
                    "contentCatalog");
                var serialized = new SerializedObject(content);
                var definition = serialized.FindProperty("entries")
                    .GetArrayElementAtIndex(0)
                    .objectReferenceValue;
                return (AssetDatabase.GetAssetPath(definition), "prefab");
            }

            if (drift == "decoration-catalogue-order")
            {
                var catalogue = ReadObjectReference(
                    FindAll<DecorationModeController>(scene).Single(),
                    "catalogueAsset");
                return (AssetDatabase.GetAssetPath(catalogue), "entries");
            }

            var objectPath = drift switch
            {
                "missing-main-camera" or "duplicate-main-camera" => "Main Camera",
                "missing-light" or "duplicate-light" => "Directional Light",
                "missing-phase0" or "duplicate-phase0" or "duplicate-mouse"
                    or "duplicate-camera-controller" or "duplicate-interaction"
                    or "camera-binding" => "Phase0_Runtime",
                "runtime-location" => "MislocatedRuntime",
                "missing-environment" or "duplicate-environment"
                    or "environment-root-transform" => "P4_Environment",
                "missing-floor" or "wrong-floor" or "floor-transform" =>
                    "P4_Environment/P4_Floor_8x8",
                "grid-overlay" => "P4_Environment/P4_Floor_8x8/GridOverlay",
                "missing-window" or "wrong-window" or "window-transform" =>
                    "P4_Environment/P4_Wall_BackRight/P4_Window_BackRight_C3_R0",
                "environment-extra-child" =>
                    "P4_Environment/Task8_ExtraEnvironmentChild",
                "environment-extra-component" or "environment-child-order" =>
                    "P4_Environment",
                "environment-prefab-extra-child" =>
                    "P4_Environment/P4_Wall_BackLeft/Task8_ExtraWallChild",
                "environment-prefab-extra-component" =>
                    "P4_Environment/P4_Wall_BackLeft",
                "environment-prefab-override" =>
                    "P4_Environment/P4_Floor_8x8/GridOverlay",
                "environment-prefab-property-override" =>
                    "P4_Environment/P4_Floor_8x8/GridOverlay/"
                    + Find(scene, "P4_Floor_8x8").transform.Find("GridOverlay")
                        .GetChild(0).name,
                "missing-owner" or "decoration-owner-transform"
                    or "missing-catalogue" or "missing-ui-reference"
                    or "missing-hud-toggle" or "decoration-owner-inventory"
                    or "shared-task4-binding" or "shared-task5-binding"
                    or "shared-task7-binding" or "wrong-content-catalogue"
                    or "wrong-decoration-catalogue" => "Phase6_DecorationRuntime",
                "decoration-space-transform" =>
                    "Phase6_DecorationRuntime/DecorationSpaceRoot",
                "decoration-space-extra-child" =>
                    "Phase6_DecorationRuntime/DecorationSpaceRoot/Task8_ExtraDecorationChild",
                "decoration-space-extra-component" or "decoration-space-child-order" =>
                    "Phase6_DecorationRuntime/DecorationSpaceRoot",
                "preview-root-transform" =>
                    "Phase6_DecorationRuntime/DecorationSpaceRoot/FurniturePreviewRoot",
                "missing-grid" or "duplicate-grid" or "invalid-grid" =>
                    "Phase6_DecorationRuntime/DecorationSpaceRoot/GridVisualRoot",
                "missing-ui-root" or "duplicate-ui-root" => "UI Root",
                "missing-canvas" or "duplicate-canvas" => "UI Root/HUD Canvas",
                "unexpected-canvas" => "UI Root/Unexpected Canvas",
                "missing-event-system" or "duplicate-event-system"
                    or "missing-input-module" or "duplicate-input-module"
                    or "missing-input-actions" or "event-system-parent"
                    or "input-module-owner" or "wrong-input-actions"
                    or "missing-point-action" => "UI Root/EventSystem",
                "missing-time-panel" =>
                    "UI Root/HUD Canvas/HUD Layer/Decoration Safe Area/RightRail",
                "missing-contract-root" or "duplicate-contract-root" =>
                    "Phase6_ContractReferences",
                "temporary-fixture" => "TEMP_P4_ManualReviewFixtures_DELETE_LATER",
                "unexpected-initial" => "PF_Furniture_WorkTable_01",
                "action-extra-child" =>
                    "UI Root/Screen Canvas/Panel Layer/PF_UI_DecorationActionBar/Task8_ExtraActionChild",
                "modal-extra-component" =>
                    "UI Root/Screen Canvas/Modal Layer/PF_UI_DecorationStoreModal",
                "safearea-extra-child" =>
                    "UI Root/HUD Canvas/HUD Layer/Decoration Safe Area/Task8_ExtraSafeAreaChild",
                "action-internal-active-drift" =>
                    "UI Root/Screen Canvas/Panel Layer/PF_UI_DecorationActionBar/ActionPanel",
                "catalogue-internal-component" =>
                    "UI Root/Screen Canvas/Panel Layer/PF_UI_DecorationCatalogue/ExpandedSheet",
                _ => throw new ArgumentOutOfRangeException(nameof(drift), drift, null)
            };
            return (ValidationPath, objectPath);
        }

        private static T CreateOwnedAsset<T>(
            T asset,
            string label,
            TargetFileScope scope)
            where T : UnityEngine.Object
        {
            var path = "Assets/Tests/EditMode/Phase6/__Task8Validator_"
                + label + "_" + Guid.NewGuid().ToString("N") + ".asset";
            Assert.That(File.Exists(path), Is.False);
            Assert.That(File.Exists(path + ".meta"), Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path), Is.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(
                path,
                AssetPathToGUIDOptions.OnlyExistingAssets), Is.Empty);
            try
            {
                AssetDatabase.CreateAsset(asset, path);
                scope.RegisterOwnedAsset(path, asset);
                Assert.That(AssetDatabase.LoadAssetAtPath<T>(path), Is.SameAs(asset));
                return asset;
            }
            catch
            {
                if (string.Equals(
                        AssetDatabase.GetAssetPath(asset),
                        path,
                        StringComparison.Ordinal))
                {
                    AssetDatabase.DeleteAsset(path);
                }

                throw;
            }
        }

        private static void ClearSeams()
        {
            Phase6DecorationSceneSetup.DependencyResolverOverrideForTests = null;
            Phase6DecorationSceneSetup.Phase4ValidatorOverrideForTests = null;
            Phase6DecorationSceneSetup.Phase5ValidatorOverrideForTests = null;
            Phase6DecorationSceneSetup.DecorationCatalogueValidatorOverrideForTests = null;
            Phase6DecorationSceneSetup.FaultInjectorForTests = null;
            Phase6DecorationSceneSetup.SaveSceneObserverForTests = null;
            Phase6DecorationValidator.SceneExistsOverrideForTests = null;
            Phase6DecorationValidator.BuildSettingsOverrideForTests = null;
            Phase6DecorationValidator.RuntimeSourceOverrideForTests = null;
            Phase6DecorationValidator.MissingScriptPathsOverrideForTests = null;
        }

        private static Phase6DecorationValidationIssue Issue(
            Phase6DecorationIssueCode code,
            string assetPath,
            string objectPath,
            string message) =>
            new Phase6DecorationValidationIssue(code, assetPath, objectPath, message);

        private static void AssertExactIssue(
            Phase6DecorationValidationReport report,
            Phase6DecorationIssueCode code,
            string assetPath,
            string objectPath)
        {
            Assert.That(report.Issues.Any(issue =>
                    issue.Code == code
                    && issue.AssetPath == assetPath
                    && issue.ObjectPath == objectPath),
                Is.True,
                $"Expected {code} at '{assetPath}' / '{objectPath}'.");
        }

        private static void SetObjectReference(
            UnityEngine.Object owner,
            string fieldName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            var property = serialized.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null, fieldName);
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Find(Scene scene, string name) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Single(transform => transform.name == name)
                .gameObject;

        private static T[] FindAll<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();

        private static MonoBehaviour[] FindComponentsByFullName(
            Scene scene,
            string fullName) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
                .Where(component => component != null
                    && component.GetType().FullName == fullName)
                .ToArray();

        private static void CloseCandidate(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static string[] CaptureOpenSceneOrder() =>
            Enumerable.Range(0, SceneManager.sceneCount)
                .Select(index => SceneManager.GetSceneAt(index))
                .Select((scene, index) =>
                    $"{index}|{scene.handle}|{scene.path}|{scene.name}|{scene.isLoaded}|"
                    + $"{scene.isDirty}|active={scene == SceneManager.GetActiveScene()}")
                .ToArray();

        private static string CaptureSceneObjectFingerprint(Scene scene) =>
            string.Join(";", scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform =>
                    $"{EntityId.ToULong(transform.GetEntityId())}:{transform.name}:"
                    + $"{transform.position}:{transform.rotation}:{transform.localScale}:"
                    + $"{transform.gameObject.activeSelf}:"
                    + string.Join(",", transform.GetComponents<Component>()
                        .Select(component => component == null
                            ? "<missing>"
                            : component.GetType().FullName))));

        private static string HashFile(string path)
        {
            if (!File.Exists(path)) return "<absent>";
            using var sha = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))
                .Replace("-", string.Empty);
        }

        private sealed class ValidatorDirtyCallerFixture
        {
            private const float DirtyPanSpeed = 73.25f;
            private const float DirtyLightIntensity = 6.25f;

            private readonly CameraSettings asset;
            private readonly string assetPath;
            private readonly string assetHash;
            private readonly string assetMetaHash;
            private readonly Scene scene;
            private readonly GameObject sceneObject;
            private readonly Light sceneLight;
            private readonly string sceneFingerprint;
            private readonly string[] openSceneOrder;
            private readonly UnityEngine.Object[] selection;
            private readonly int activeSelectionIndex;

            private ValidatorDirtyCallerFixture(
                CameraSettings asset,
                string assetPath,
                string assetHash,
                string assetMetaHash,
                Scene scene,
                GameObject sceneObject,
                Light sceneLight,
                string sceneFingerprint,
                string[] openSceneOrder,
                UnityEngine.Object[] selection,
                int activeSelectionIndex)
            {
                this.asset = asset;
                this.assetPath = assetPath;
                this.assetHash = assetHash;
                this.assetMetaHash = assetMetaHash;
                this.scene = scene;
                this.sceneObject = sceneObject;
                this.sceneLight = sceneLight;
                this.sceneFingerprint = sceneFingerprint;
                this.openSceneOrder = openSceneOrder;
                this.selection = selection;
                this.activeSelectionIndex = activeSelectionIndex;
            }

            public static ValidatorDirtyCallerFixture Create(TargetFileScope scope)
            {
                var asset = CreateOwnedAsset(
                    ScriptableObject.CreateInstance<CameraSettings>(),
                    "ReadOnlyDirtyCaller",
                    scope);
                var assetPath = AssetDatabase.GetAssetPath(asset);
                var assetHash = HashFile(assetPath);
                var assetMetaHash = HashFile(assetPath + ".meta");
                asset.PanSpeed = DirtyPanSpeed;
                EditorUtility.SetDirty(asset);

                var scene = EditorSceneManager.OpenScene(
                    MainCafePath,
                    OpenSceneMode.Additive);
                var sceneObject = new GameObject("Task8_Validator_Dirty_Content");
                SceneManager.MoveGameObjectToScene(sceneObject, scene);
                sceneObject.transform.position = new Vector3(2.5f, 1.25f, -3.75f);
                var sceneLight = sceneObject.AddComponent<Light>();
                sceneLight.intensity = DirtyLightIntensity;
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(SceneManager.SetActiveScene(scene), Is.True);

                Selection.objects = new UnityEngine.Object[] { asset, sceneObject, sceneLight };
                var selection = Selection.objects;
                var activeSelectionIndex = Array.IndexOf(selection, Selection.activeObject);
                Assert.That(selection, Has.Length.EqualTo(3));
                Assert.That(activeSelectionIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(scene.isDirty, Is.True);
                Assert.That(EditorUtility.IsDirty(asset), Is.True);

                return new ValidatorDirtyCallerFixture(
                    asset,
                    assetPath,
                    assetHash,
                    assetMetaHash,
                    scene,
                    sceneObject,
                    sceneLight,
                    CaptureSceneObjectFingerprint(scene),
                    CaptureOpenSceneOrder(),
                    selection,
                    activeSelectionIndex);
            }

            public void AssertPreserved()
            {
                Assert.That(scene.IsValid() && scene.isLoaded, Is.True);
                Assert.That(scene.isDirty, Is.True);
                Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(scene.handle));
                Assert.That(CaptureOpenSceneOrder(), Is.EqualTo(openSceneOrder));
                Assert.That(CaptureSceneObjectFingerprint(scene), Is.EqualTo(sceneFingerprint));
                Assert.That(sceneObject.transform.position,
                    Is.EqualTo(new Vector3(2.5f, 1.25f, -3.75f)));
                Assert.That(sceneLight.intensity, Is.EqualTo(DirtyLightIntensity));

                Assert.That(asset.PanSpeed, Is.EqualTo(DirtyPanSpeed));
                Assert.That(EditorUtility.IsDirty(asset), Is.True);
                Assert.That(AssetDatabase.GetAssetPath(asset), Is.EqualTo(assetPath));
                Assert.That(HashFile(assetPath), Is.EqualTo(assetHash));
                Assert.That(HashFile(assetPath + ".meta"), Is.EqualTo(assetMetaHash));

                Assert.That(Selection.objects, Has.Length.EqualTo(selection.Length));
                for (var index = 0; index < selection.Length; index++)
                    Assert.That(Selection.objects[index], Is.SameAs(selection[index]), index.ToString());
                Assert.That(Array.IndexOf(Selection.objects, Selection.activeObject),
                    Is.EqualTo(activeSelectionIndex));
            }
        }

        private sealed class TargetFileScope : IDisposable
        {
            private readonly FileState main;
            private readonly FileState mainMeta;
            private readonly FileState validation;
            private readonly FileState validationMeta;
            private readonly CapturedScene[] scenes;
            private readonly SceneSetup[] sceneSetup;
            private readonly string unrelatedFingerprint;
            private readonly EditorBuildSettingsScene[] buildSettings;
            private readonly SelectionEntry[] selection;
            private readonly int activeIndex;
            private readonly List<OwnedAsset> ownedAssets = new List<OwnedAsset>();

            public TargetFileScope()
            {
                var callerDirty = Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt)
                    .FirstOrDefault(scene => scene.isDirty);
                if (callerDirty.IsValid())
                {
                    Assert.Ignore(
                        "This validator persistence test will not run while the caller has a dirty "
                        + "Scene; save or close it first.");
                }

                var callerTarget = Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt)
                    .FirstOrDefault(scene => IsTargetPath(scene.path));
                if (callerTarget.IsValid())
                {
                    Assert.Ignore(
                        "This persistence-equivalence test requires both Task 8 target Scenes "
                        + "to be closed by the caller; cleanup never closes caller-owned Scenes.");
                }

                main = FileState.Capture(MainCafePath);
                mainMeta = FileState.Capture(MainCafePath + ".meta");
                validation = FileState.Capture(ValidationPath);
                validationMeta = FileState.Capture(ValidationPath + ".meta");
                scenes = Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(index => CapturedScene.Capture(SceneManager.GetSceneAt(index)))
                    .ToArray();
                sceneSetup = EditorSceneManager.GetSceneManagerSetup();
                unrelatedFingerprint = CaptureUnrelatedFingerprint(scenes);
                buildSettings = EditorBuildSettings.scenes
                    .Select(entry => new EditorBuildSettingsScene(entry.path, entry.enabled))
                    .ToArray();
                selection = Selection.objects.Select(SelectionEntry.Capture).ToArray();
                activeIndex = Array.IndexOf(Selection.objects, Selection.activeObject);
            }

            public void RegisterOwnedAsset(string path, UnityEngine.Object asset)
            {
                var guid = AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(path)
                    || string.IsNullOrEmpty(guid)
                    || asset == null
                    || !ReferenceEquals(
                        AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path),
                        asset))
                {
                    throw new InvalidOperationException(
                        "Only a unique asset successfully created by this test scope may be registered.");
                }

                ownedAssets.Add(new OwnedAsset(path, guid));
            }

            public void Dispose()
            {
                try
                {
                    Selection.objects = Array.Empty<UnityEngine.Object>();
                    CloseTestOwnedScenes(scenes);
                }
                finally
                {
                    try
                    {
                        try
                        {
                            RestoreTarget(MainCafePath, main, mainMeta);
                        }
                        finally
                        {
                            try
                            {
                                RestoreTarget(ValidationPath, validation, validationMeta);
                            }
                            finally
                            {
                                RestoreBuildSettings(buildSettings);
                            }
                        }
                    }
                    finally
                    {
                        try
                        {
                            RestoreSceneSetupIfChanged(sceneSetup);
                        }
                        finally
                        {
                            try
                            {
                                RestoreActiveScene(scenes);
                                var restored = selection.Select(entry => entry.Resolve()).ToArray();
                                Selection.objects = restored;
                                Selection.activeObject = activeIndex >= 0
                                    && activeIndex < restored.Length
                                    ? restored[activeIndex]
                                    : null;
                                Assert.That(
                                    CaptureUnrelatedFingerprint(scenes),
                                    Is.EqualTo(unrelatedFingerprint),
                                    "Validator test cleanup changed caller-owned Scene state.");
                            }
                            finally
                            {
                                CleanupOwnedAssets();
                            }
                        }
                    }
                }
            }

            private void CleanupOwnedAssets()
            {
                foreach (var owned in ownedAssets.AsEnumerable().Reverse())
                {
                    if (string.Equals(
                            AssetDatabase.AssetPathToGUID(owned.Path),
                            owned.Guid,
                            StringComparison.Ordinal))
                    {
                        Assert.That(AssetDatabase.DeleteAsset(owned.Path), Is.True, owned.Path);
                    }

                    Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(owned.Path),
                        Is.Null,
                        owned.Path);
                    Assert.That(AssetDatabase.AssetPathToGUID(
                        owned.Path,
                        AssetPathToGUIDOptions.OnlyExistingAssets), Is.Empty, owned.Path);
                    Assert.That(File.Exists(owned.Path), Is.False, owned.Path);
                    Assert.That(File.Exists(owned.Path + ".meta"), Is.False, owned.Path);
                }
            }
        }

        private readonly struct OwnedAsset
        {
            public readonly string Path;
            public readonly string Guid;

            public OwnedAsset(string path, string guid)
            {
                Path = path;
                Guid = guid;
            }
        }

        private static void RestoreBuildSettings(EditorBuildSettingsScene[] expected)
        {
            var current = EditorBuildSettings.scenes;
            if (!current.Select((entry, index) => $"{index}|{entry.path}|{entry.enabled}")
                    .SequenceEqual(expected.Select((entry, index) =>
                        $"{index}|{entry.path}|{entry.enabled}")))
            {
                EditorBuildSettings.scenes = expected;
            }
        }

        private static void RestoreSceneSetupIfChanged(SceneSetup[] expected)
        {
            var current = EditorSceneManager.GetSceneManagerSetup();
            var currentFingerprint = current.Select((entry, index) =>
                $"{index}|{entry.path}|{entry.isLoaded}|{entry.isActive}");
            var expectedFingerprint = expected.Select((entry, index) =>
                $"{index}|{entry.path}|{entry.isLoaded}|{entry.isActive}");
            if (!currentFingerprint.SequenceEqual(expectedFingerprint))
                EditorSceneManager.RestoreSceneManagerSetup(expected);
        }

        private readonly struct CapturedScene
        {
            public readonly ulong Handle;
            public readonly string Path;
            public readonly bool IsLoaded;
            public readonly bool IsActive;

            private CapturedScene(ulong handle, string path, bool isLoaded, bool isActive)
            {
                Handle = handle;
                Path = path;
                IsLoaded = isLoaded;
                IsActive = isActive;
            }

            public static CapturedScene Capture(Scene scene) => new CapturedScene(
                scene.handle.GetRawData(),
                scene.path,
                scene.isLoaded,
                scene == SceneManager.GetActiveScene());
        }

        private readonly struct SelectionEntry
        {
            private readonly UnityEngine.Object direct;
            private readonly GlobalObjectId globalId;
            private readonly bool useGlobal;

            private SelectionEntry(UnityEngine.Object direct, GlobalObjectId globalId, bool useGlobal)
            {
                this.direct = direct;
                this.globalId = globalId;
                this.useGlobal = useGlobal;
            }

            public static SelectionEntry Capture(UnityEngine.Object value)
            {
                var scene = SceneFor(value);
                var useGlobal = scene.IsValid() && IsTargetPath(scene.path);
                return new SelectionEntry(
                    value,
                    useGlobal ? GlobalObjectId.GetGlobalObjectIdSlow(value) : default,
                    useGlobal);
            }

            public UnityEngine.Object Resolve() => useGlobal
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId)
                : direct;
        }

        private static Scene SceneFor(UnityEngine.Object value)
        {
            if (value is GameObject gameObject) return gameObject.scene;
            if (value is Component component) return component.gameObject.scene;
            return default;
        }

        private static void CloseTestOwnedScenes(CapturedScene[] captured)
        {
            var capturedHandles = captured.Select(entry => entry.Handle).ToArray();
            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (capturedHandles.Contains(scene.handle.GetRawData())) continue;

                EnsureCloseAnchor(scene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void EnsureCloseAnchor(Scene sceneBeingClosed)
        {
            if (SceneManager.sceneCount > 1) return;
            var anchor = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            anchor.name = "Task8_ValidatorCleanupAnchor";
            if (sceneBeingClosed == SceneManager.GetActiveScene())
                SceneManager.SetActiveScene(anchor);
        }

        private static void RestoreTarget(
            string path,
            FileState sceneFile,
            FileState metaFile)
        {
            if (!sceneFile.Existed
                && AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            sceneFile.Restore();
            metaFile.Restore();
            if (sceneFile.Existed) ImportIfPresent(path);
            else
            {
                Assert.That(File.Exists(path), Is.False);
                Assert.That(File.Exists(path + ".meta"), Is.EqualTo(metaFile.Existed));
                Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path), Is.Null);
            }

            foreach (var anchor in Enumerable.Range(0, SceneManager.sceneCount)
                         .Select(SceneManager.GetSceneAt)
                         .Where(scene => scene.name == "Task8_ValidatorCleanupAnchor")
                         .ToArray())
            {
                if (SceneManager.sceneCount > 1)
                    EditorSceneManager.CloseScene(anchor, true);
            }
        }

        private static void RestoreActiveScene(CapturedScene[] captured)
        {
            var active = captured.FirstOrDefault(entry => entry.IsActive);
            if (active.Handle == 0) return;

            var retained = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .FirstOrDefault(scene => scene.handle.GetRawData() == active.Handle);
            if (retained.IsValid())
            {
                SceneManager.SetActiveScene(retained);
                return;
            }

        }

        private static bool IsTargetPath(string path) =>
            string.Equals(path, MainCafePath, StringComparison.Ordinal)
            || string.Equals(path, ValidationPath, StringComparison.Ordinal);

        private static string CaptureUnrelatedFingerprint(CapturedScene[] captured)
        {
            var handles = captured.Select(entry => entry.Handle).ToArray();
            var activeHandle = SceneManager.GetActiveScene().handle.GetRawData();
            return string.Join("\n", Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => handles.Contains(scene.handle.GetRawData()))
                .Select((scene, index) =>
                    $"{index}|{scene.handle.GetRawData()}|{scene.path}|{scene.name}|{scene.isDirty}|"
                    + $"active={scene.handle.GetRawData() == activeHandle}|"
                    + string.Join(";", scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                        .Select(transform =>
                            $"{EntityId.ToULong(transform.GetEntityId())}:{transform.name}:"
                            + $"{transform.localPosition}:{transform.localRotation}:"
                            + $"{transform.localScale}:{transform.gameObject.activeSelf}:"
                            + string.Join(",", transform.GetComponents<Component>()
                                .Select(component => component == null
                                    ? "<missing>"
                                    : component.GetType().FullName))))));
        }

        private readonly struct FileState
        {
            private readonly string path;
            private readonly bool existed;
            private readonly byte[] bytes;

            private FileState(string path, bool existed, byte[] bytes)
            {
                this.path = path;
                this.existed = existed;
                this.bytes = bytes;
            }

            public static FileState Capture(string path) => new FileState(
                path,
                File.Exists(path),
                File.Exists(path) ? File.ReadAllBytes(path) : null);

            public bool Existed => existed;

            public void Restore()
            {
                if (existed)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, bytes);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        private static void ImportIfPresent(string path)
        {
            if (File.Exists(path))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private readonly struct EditorStateSnapshot : IEquatable<EditorStateSnapshot>
        {
            private readonly string sceneSetup;
            private readonly string buildSettings;
            private readonly string selection;
            private readonly string targetBytes;
            private readonly UnityEngine.Object[] selectedObjects;
            private readonly int activeSelectionIndex;

            private EditorStateSnapshot(
                string sceneSetup,
                string buildSettings,
                string selection,
                string targetBytes,
                UnityEngine.Object[] selectedObjects,
                int activeSelectionIndex)
            {
                this.sceneSetup = sceneSetup;
                this.buildSettings = buildSettings;
                this.selection = selection;
                this.targetBytes = targetBytes;
                this.selectedObjects = selectedObjects;
                this.activeSelectionIndex = activeSelectionIndex;
            }

            public static EditorStateSnapshot Capture()
            {
                var setup = string.Join("\n", EditorSceneManager.GetSceneManagerSetup()
                    .Select((entry, index) =>
                        $"{index}|{entry.path}|{entry.isLoaded}|{entry.isActive}"));
                var build = string.Join("\n", EditorBuildSettings.scenes
                    .Select((entry, index) => $"{index}|{entry.path}|{entry.enabled}"));
                var selected = string.Join("\n", Selection.objects.Select((value, index) =>
                    $"{index}|{GlobalObjectId.GetGlobalObjectIdSlow(value)}|{value?.name}"));
                var active = Array.IndexOf(Selection.objects, Selection.activeObject);
                var bytes = Hash(MainCafePath) + "|" + Hash(MainCafePath + ".meta")
                    + "|" + Hash(ValidationPath) + "|" + Hash(ValidationPath + ".meta");
                return new EditorStateSnapshot(
                    setup,
                    build,
                    selected + "|active=" + active,
                    bytes,
                    Selection.objects.ToArray(),
                    active);
            }

            public void RestoreSelectionOnly()
            {
                var restored = selectedObjects ?? Array.Empty<UnityEngine.Object>();
                Selection.objects = restored;
                Selection.activeObject = activeSelectionIndex >= 0
                    && activeSelectionIndex < restored.Length
                    ? restored[activeSelectionIndex]
                    : null;
            }

            public bool Equals(EditorStateSnapshot other) =>
                sceneSetup == other.sceneSetup
                && buildSettings == other.buildSettings
                && selection == other.selection
                && targetBytes == other.targetBytes;

            public override bool Equals(object obj) =>
                obj is EditorStateSnapshot other && Equals(other);

            public override int GetHashCode() =>
                HashCode.Combine(sceneSetup, buildSettings, selection, targetBytes);

            private static string Hash(string path)
            {
                if (!File.Exists(path)) return "<absent>";
                using var sha = System.Security.Cryptography.SHA256.Create();
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))
                    .Replace("-", string.Empty);
            }
        }
    }
}

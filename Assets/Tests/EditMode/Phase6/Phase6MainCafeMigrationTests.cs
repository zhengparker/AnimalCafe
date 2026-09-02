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
using AnimalCafe.EditorTools.Phase4;
using AnimalCafe.EditorTools.Phase5;
using AnimalCafe.EditorTools.Phase6;
using AnimalCafe.Input;
using AnimalCafe.Interaction;
using AnimalCafe.UI;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace AnimalCafe.Tests.EditMode.Phase6
{
    /// <summary>
    /// Public Task 8 transaction and authored-Scene tests. Each test owns one
    /// outer finally that restores both targets, Build Settings, Selection,
    /// open Scene order and all Task 8 static seams.
    /// </summary>
    internal sealed class Phase6MainCafeMigrationTests
    {
        private const string MainCafePath = "Assets/Scenes/MainCafe.unity";
        private const string ValidationPath =
            "Assets/Scenes/Validation/Phase6DecorationMode.unity";
        private const string UnrelatedFixturePath =
            "Assets/Scenes/Validation/Phase5UiFoundation.unity";
        private const string TemporaryRoot =
            "TEMP_P4_ManualReviewFixtures_DELETE_LATER";
        private const string BackupRoot =
            "Library/AnimalCafe/Phase6Task8SceneBackup";

        [TestCase(Phase6SceneSetupTarget.MainCafe)]
        [TestCase(Phase6SceneSetupTarget.Validation)]
        public void Configure_Target_RefusesDirtyTargetWithoutChangingBytesMetaOrCallerState(
            Phase6SceneSetupTarget target)
        {
            var scope = new FullEditorStateScope();
            try
            {
                EnsureTargetExists(target);
                var scene = EditorSceneManager.OpenScene(PathFor(target), OpenSceneMode.Additive);
                var marker = new GameObject("Task8_Dirty_Target_Marker");
                SceneManager.MoveGameObjectToScene(marker, scene);
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(scene.isDirty, Is.True);
                var before = TransactionSnapshot.Capture(target);

                var exception = Assert.Throws<InvalidOperationException>(() => Configure(target));

                Assert.That(exception.Message, Does.Contain("dirty").IgnoreCase);
                Assert.That(TransactionSnapshot.Capture(target), Is.EqualTo(before));
                Assert.That(scene.isDirty, Is.True);
                Assert.That(FindAll(scene, "Task8_Dirty_Target_Marker"), Has.Length.EqualTo(1));
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupDependency.ContentCatalog)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupDependency.ContentCatalog)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupDependency.EnvironmentPrefab)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupDependency.EnvironmentPrefab)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupDependency.UiPrefab)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupDependency.UiPrefab)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupDependency.Theme)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupDependency.Theme)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupDependency.GridMaterial)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupDependency.GridMaterial)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupDependency.CameraSettings)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupDependency.CameraSettings)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupDependency.InputActions)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupDependency.InputActions)]
        public void Configure_Target_InjectedMissingDependencyLeavesTargetsAndCallerStateUnchanged(
            Phase6SceneSetupTarget target,
            Phase6SceneSetupDependency missingDependency)
        {
            var scope = new FullEditorStateScope();
            try
            {
                var before = FullStateFingerprint.Capture();
                Phase6DecorationSceneSetup.DependencyResolverOverrideForTests = _ =>
                    Phase6DecorationSceneSetup.CreateMalformedDependencyForTests(
                        target,
                        missingDependency);

                var exception = Assert.Throws<InvalidOperationException>(() => Configure(target));

                Assert.That(exception.Message, Does.Contain("depend").IgnoreCase);
                Assert.That(FullStateFingerprint.Capture(), Is.EqualTo(before));
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe, "phase4")]
        [TestCase(Phase6SceneSetupTarget.Validation, "phase4")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "phase5")]
        [TestCase(Phase6SceneSetupTarget.Validation, "phase5")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "decoration-catalogue")]
        [TestCase(Phase6SceneSetupTarget.Validation, "decoration-catalogue")]
        public void Configure_Target_DependencyValidationIssueStopsBeforeBackupMutationOrSave(
            Phase6SceneSetupTarget target,
            string validator)
        {
            var scope = new FullEditorStateScope();
            try
            {
                var caller = CreateFirstPublishCallerFixture(scope);
                var before = FullStateFingerprint.Capture();
                var beforeBackupTree = BackupTreeFingerprint();
                var validatorCalled = false;
                var beforeMutationCalled = false;
                var saveCalled = false;

                switch (validator)
                {
                    case "phase4":
                        Phase6DecorationSceneSetup.Phase4ValidatorOverrideForTests = () =>
                        {
                            validatorCalled = true;
                            return new Phase4AssetValidationReport(
                                0,
                                1,
                                new[]
                                {
                                    new Phase4AssetValidationIssue(
                                        Phase4AssetIssueCode.MissingReference,
                                        "Assets/Art/Phase4/Test.asset",
                                        "Injected Task 8 dependency issue.")
                                });
                        };
                        break;
                    case "phase5":
                        Phase6DecorationSceneSetup.Phase5ValidatorOverrideForTests = () =>
                        {
                            validatorCalled = true;
                            return new Phase5UiFoundationValidationReport(
                                new[]
                                {
                                    new Phase5UiFoundationValidationIssue(
                                        Phase5UiFoundationIssueCode.MissingCanonicalAsset,
                                        "Assets/UI/Phase5/Test.prefab",
                                        "Test",
                                        "Injected Task 8 dependency issue.")
                                });
                        };
                        break;
                    case "decoration-catalogue":
                        Phase6DecorationSceneSetup.DecorationCatalogueValidatorOverrideForTests = _ =>
                        {
                            validatorCalled = true;
                            throw new InvalidOperationException(
                                "Injected Task 8 decoration catalogue issue.");
                        };
                        break;
                    default:
                        Assert.Fail("Unknown validator seam: " + validator);
                        break;
                }

                Phase6DecorationSceneSetup.FaultInjectorForTests = stage =>
                {
                    if (stage == Phase6SceneSetupStage.BeforeMutation)
                        beforeMutationCalled = true;
                };
                Phase6DecorationSceneSetup.SaveSceneObserverForTests = (_, _) =>
                    saveCalled = true;

                var exception = Assert.Throws<InvalidOperationException>(() => Configure(target));

                Assert.That(exception.Message, Does.Contain("depend").IgnoreCase
                    .Or.Contain("catalogue").IgnoreCase);
                Assert.That(validatorCalled, Is.True);
                Assert.That(beforeMutationCalled, Is.False,
                    "Dependency validation must finish before BeforeMutation.");
                Assert.That(saveCalled, Is.False);
                Assert.That(BackupTreeFingerprint(), Is.EqualTo(beforeBackupTree));
                Assert.That(FullStateFingerprint.Capture(), Is.EqualTo(before));
                caller.AssertPreserved();
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ConfigureValidationScene_BeforeMutationFiresBeforeAnyCandidateOrRootMutation()
        {
            var scope = new FullEditorStateScope();
            try
            {
                RemoveExactTarget(ValidationPath);
                var mainCafe = OpenTargetForTest(
                    Phase6SceneSetupTarget.MainCafe,
                    out _);
                var beforeHandles = SceneHandles();
                var beforeMainCafe = CaptureSceneObjectFingerprint(mainCafe);
                string observedMainCafe = null;
                ulong[] observedHandles = null;
                Phase6DecorationSceneSetup.FaultInjectorForTests = stage =>
                {
                    if (stage != Phase6SceneSetupStage.BeforeMutation)
                        return;
                    observedHandles = SceneHandles();
                    observedMainCafe = CaptureSceneObjectFingerprint(mainCafe);
                    throw new Task8InjectedFaultException(stage.ToString());
                };

                Assert.Throws<Task8InjectedFaultException>(
                    Phase6DecorationSceneSetup.ConfigureValidationScene);

                Assert.That(observedHandles, Is.EqualTo(beforeHandles),
                    "BeforeMutation must run before a Validation candidate Scene is created.");
                Assert.That(observedMainCafe, Is.EqualTo(beforeMainCafe),
                    "BeforeMutation must run before any loaded MainCafe root is touched.");
                Assert.That(CaptureSceneObjectFingerprint(mainCafe), Is.EqualTo(beforeMainCafe));
                Assert.That(SceneHandles(), Is.EqualTo(beforeHandles));
                Assert.That(File.Exists(ValidationPath), Is.False);
                Assert.That(File.Exists(ValidationPath + ".meta"), Is.False);
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void CreateValidationCandidate_UsesNewEmptyAdditiveSceneWithoutBorrowingLoadedMainCafe()
        {
            var scope = new FullEditorStateScope();
            Scene candidate = default;
            try
            {
                var mainCafe = OpenTargetForTest(
                    Phase6SceneSetupTarget.MainCafe,
                    out _);
                var mainCafeHandle = mainCafe.handle.GetRawData();
                var beforeMainCafe = CaptureSceneObjectFingerprint(mainCafe);

                candidate = Phase6DecorationSceneSetup.CreateValidationCandidateForTests();

                Assert.That(candidate.IsValid() && candidate.isLoaded, Is.True);
                Assert.That(candidate.path, Is.Empty);
                Assert.That(EditorSceneManager.IsPreviewScene(candidate), Is.True,
                    "A Validation candidate must use a unique independent empty working Scene.");
                Assert.That(candidate.handle.GetRawData(), Is.Not.EqualTo(mainCafeHandle));
                Assert.That(mainCafe.IsValid() && mainCafe.isLoaded, Is.True);
                Assert.That(CaptureSceneObjectFingerprint(mainCafe), Is.EqualTo(beforeMainCafe));
                Assert.That(mainCafe.GetRootGameObjects(), Is.Not.Empty);
            }
            finally
            {
                if (candidate.IsValid() && candidate.isLoaded)
                {
                    if (EditorSceneManager.IsPreviewScene(candidate))
                        EditorSceneManager.ClosePreviewScene(candidate);
                    else
                        EditorSceneManager.CloseScene(candidate, true);
                }
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ConfigureValidationScene_FirstPublishNeverMutatesOrReopensLoadedMainCafe()
        {
            var scope = new FullEditorStateScope();
            try
            {
                RemoveExactTarget(ValidationPath);
                var mainCafe = OpenTargetForTest(
                    Phase6SceneSetupTarget.MainCafe,
                    out _);
                var mainCafeHandle = mainCafe.handle.GetRawData();
                var beforeMainCafe = CaptureSceneObjectFingerprint(mainCafe);
                var beforeOrder = SceneOrder();
                var selected = Find(mainCafe, "Main Camera");
                Selection.objects = new UnityEngine.Object[] { selected };
                Selection.activeObject = selected;
                var selectedId = GlobalObjectId.GetGlobalObjectIdSlow(selected);

                Phase6DecorationSceneSetup.ConfigureValidationScene();

                Assert.That(mainCafe.IsValid() && mainCafe.isLoaded, Is.True);
                Assert.That(mainCafe.handle.GetRawData(), Is.EqualTo(mainCafeHandle));
                Assert.That(CaptureSceneObjectFingerprint(mainCafe), Is.EqualTo(beforeMainCafe));
                Assert.That(SceneOrder(), Is.EqualTo(beforeOrder));
                Assert.That(GlobalObjectId.GetGlobalObjectIdSlow(Selection.activeObject),
                    Is.EqualTo(selectedId));
                Assert.That(File.Exists(ValidationPath), Is.True);
                Assert.That(File.Exists(ValidationPath + ".meta"), Is.True);
                Assert.That(SceneManager.GetSceneByPath(ValidationPath).isLoaded, Is.False);
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe)]
        [TestCase(Phase6SceneSetupTarget.Validation)]
        public void Configure_Target_PreservesUnrelatedDirtyAssetDirtySceneSelectionAndOpenOrder(
            Phase6SceneSetupTarget target)
        {
            var scope = new FullEditorStateScope();
            try
            {
                var asset = ScriptableObject.CreateInstance<CameraSettings>();
                var assetPath = "Assets/Tests/EditMode/Phase6/__Task8TransactionSelection_"
                    + Guid.NewGuid().ToString("N") + ".asset";
                Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath), Is.Null);
                AssetDatabase.CreateAsset(asset, assetPath);
                scope.RegisterOwnedAsset(assetPath, asset);
                asset.PanSpeed = 42f;
                EditorUtility.SetDirty(asset);

                var unrelated = OpenUnrelatedFixtureForTest();
                var unrelatedObject = new GameObject("Task8_Unrelated_Selected");
                SceneManager.MoveGameObjectToScene(unrelatedObject, unrelated);
                unrelatedObject.transform.position = Vector3.one;
                EditorSceneManager.MarkSceneDirty(unrelated);
                Assert.That(unrelated.isDirty, Is.True);

                EnsureTargetExists(target);
                var targetScene = EditorSceneManager.OpenScene(PathFor(target), OpenSceneMode.Additive);
                var targetObject = Find(targetScene, "Main Camera");
                var targetId = GlobalObjectId.GetGlobalObjectIdSlow(targetObject);
                Selection.activeObject = unrelatedObject;
                Selection.objects = new UnityEngine.Object[] { asset, unrelatedObject, targetObject };
                var expectedActiveIndex = Array.IndexOf(
                    Selection.objects,
                    Selection.activeObject);
                var beforeOrder = SceneOrder();

                Configure(target);

                Assert.That(SceneOrder(), Is.EqualTo(beforeOrder));
                Assert.That(unrelated.isLoaded, Is.True);
                Assert.That(unrelated.isDirty, Is.True);
                Assert.That(asset.PanSpeed, Is.EqualTo(42f));
                Assert.That(EditorUtility.IsDirty(asset), Is.True);
                Assert.That(Selection.objects, Has.Length.EqualTo(3));
                Assert.That(Selection.objects[0], Is.SameAs(asset));
                Assert.That(Selection.objects[1], Is.SameAs(unrelatedObject));
                Assert.That(GlobalObjectId.GetGlobalObjectIdSlow(Selection.objects[2]),
                    Is.EqualTo(targetId));
                Assert.That(Array.IndexOf(Selection.objects, Selection.activeObject),
                    Is.EqualTo(expectedActiveIndex));
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupStage.BeforeMutation)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupStage.BeforeSave)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupStage.AfterSave)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupStage.BeforeMutation)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupStage.BeforeSave)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupStage.AfterSave)]
        public void Configure_Target_InjectedStageRestoresExactUnityMetaAndCallerState(
            Phase6SceneSetupTarget target,
            Phase6SceneSetupStage injectedStage)
        {
            var scope = new FullEditorStateScope();
            try
            {
                Configure(target);
                var targetScene = OpenTargetForTest(target, out _);
                ArrangeSaveRequiredInLoadedTarget(target, targetScene);
                var callerFixture = CreateDirtyCallerFixture(scope, target, targetScene);
                var before = FullStateFingerprint.Capture();
                Phase6DecorationSceneSetup.FaultInjectorForTests = stage =>
                {
                    if (stage == injectedStage)
                        throw new Task8InjectedFaultException(stage.ToString());
                };

                Assert.Throws<Task8InjectedFaultException>(() => Configure(target));

                Assert.That(FullStateFingerprint.Capture(), Is.EqualTo(before));
                callerFixture.AssertPreserved();
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneRestoreStage.BeforeStagingCopy)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneRestoreStage.AfterAssetRelease)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneRestoreStage.BeforeImport)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneRestoreStage.BeforeStagingCopy)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneRestoreStage.AfterAssetRelease)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneRestoreStage.BeforeImport)]
        public void Configure_Target_InjectedRestoreFailureRecoversExactSceneAndSelection(
            Phase6SceneSetupTarget target,
            Phase6SceneRestoreStage restoreStage)
        {
            var scope = new FullEditorStateScope();
            try
            {
                Configure(target);
                var targetScene = OpenTargetForTest(target, out _);
                ArrangeSaveRequiredInLoadedTarget(target, targetScene);
                var callerFixture = CreateDirtyCallerFixture(scope, target, targetScene);
                var beforeScene = Hash(PathFor(target));
                var beforeMeta = Hash(PathFor(target) + ".meta");
                var restoreFaultCalls = 0;
                Phase6DecorationSceneSetup.FaultInjectorForTests = stage =>
                {
                    if (stage == Phase6SceneSetupStage.AfterSave)
                        throw new Task8InjectedFaultException(stage.ToString());
                };
                Phase6DecorationSceneSetup.RestoreFaultInjectorForTests = stage =>
                {
                    if (stage == restoreStage && restoreFaultCalls++ == 0)
                        throw new Task8InjectedFaultException("restore-" + stage);
                };
                LogAssert.Expect(
                    LogType.Warning,
                    new Regex("Phase 6 rollback recovered from a Scene restore failure"));

                Assert.Throws<Task8InjectedFaultException>(() => Configure(target));

                Assert.That(Hash(PathFor(target)), Is.EqualTo(beforeScene));
                Assert.That(Hash(PathFor(target) + ".meta"), Is.EqualTo(beforeMeta));
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(PathFor(target)), Is.Not.Null);
                Assert.That(restoreFaultCalls, Is.EqualTo(1));
                callerFixture.AssertPreserved();
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe)]
        [TestCase(Phase6SceneSetupTarget.Validation)]
        public void Configure_Target_TwicePreservesBytesGlobalAndLocalIdsPrefabSourcesReferencesAndCounts(
            Phase6SceneSetupTarget target)
        {
            var scope = new FullEditorStateScope();
            try
            {
                Configure(target);
                var first = CanonicalTargetSnapshot.Capture(target);

                Configure(target);
                var second = CanonicalTargetSnapshot.Capture(target);

                Assert.That(second, Is.EqualTo(first));
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe, false)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, true)]
        [TestCase(Phase6SceneSetupTarget.Validation, false)]
        [TestCase(Phase6SceneSetupTarget.Validation, true)]
        public void Configure_Target_SuccessReloadsPersistedDiskAndRestoresExactCallerState(
            Phase6SceneSetupTarget target,
            bool forceSave)
        {
            var scope = new FullEditorStateScope();
            try
            {
                Configure(target);
                var targetScene = OpenTargetForTest(target, out _);
                if (forceSave)
                    ArrangeSaveRequiredInLoadedTarget(target, targetScene);
                var caller = CreateDirtyCallerFixture(scope, target, targetScene);
                Assert.That(SceneManager.SetActiveScene(targetScene), Is.True);
                var oldTargetHandle = targetScene.handle.GetRawData();
                var beforeOrder = SceneOrder();
                var beforeActivePath = SceneManager.GetActiveScene().path;
                var beforeSelectionIds = Selection.objects
                    .Select(GlobalObjectId.GetGlobalObjectIdSlow)
                    .ToArray();
                var beforeActiveIndex = Array.IndexOf(
                    Selection.objects,
                    Selection.activeObject);
                var saves = 0;
                Phase6DecorationSceneSetup.SaveSceneObserverForTests = (observed, _) =>
                {
                    if (observed == target) saves++;
                };

                Configure(target);

                var reloaded = SceneManager.GetSceneByPath(PathFor(target));
                Assert.That(reloaded.IsValid() && reloaded.isLoaded, Is.True);
                Assert.That(reloaded.handle.GetRawData(), Is.Not.EqualTo(oldTargetHandle),
                    "Success must close and reload the target from its persisted disk path.");
                Assert.That(reloaded.isDirty, Is.False);
                Assert.That(SceneOrder(), Is.EqualTo(beforeOrder));
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(beforeActivePath));
                Assert.That(Selection.objects.Select(GlobalObjectId.GetGlobalObjectIdSlow),
                    Is.EqualTo(beforeSelectionIds));
                Assert.That(Array.IndexOf(Selection.objects, Selection.activeObject),
                    Is.EqualTo(beforeActiveIndex));
                Assert.That(saves, Is.EqualTo(forceSave ? 1 : 0));
                caller.AssertPreserved();
                Assert.That(Phase6DecorationValidator.ValidateAll().Issues.Where(issue =>
                    string.Equals(issue.AssetPath, PathFor(target), StringComparison.Ordinal)),
                    Is.Empty,
                    "The reloaded persisted target must pass the public validator.");
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe)]
        [TestCase(Phase6SceneSetupTarget.Validation)]
        public void Configure_Target_HealthySecondRunInvokesZeroBeforeSaveSaveSceneAndAfterSave(
            Phase6SceneSetupTarget target)
        {
            var scope = new FullEditorStateScope();
            try
            {
                Configure(target);
                var stages = new List<Phase6SceneSetupStage>();
                var saves = 0;
                Phase6DecorationSceneSetup.FaultInjectorForTests = stages.Add;
                Phase6DecorationSceneSetup.SaveSceneObserverForTests = (_, _) => saves++;

                Configure(target);

                Assert.That(stages, Has.None.EqualTo(Phase6SceneSetupStage.BeforeSave));
                Assert.That(stages, Has.None.EqualTo(Phase6SceneSetupStage.AfterSave));
                Assert.That(saves, Is.Zero);
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe)]
        [TestCase(Phase6SceneSetupTarget.Validation)]
        public void Configure_Target_SaveRequiredRunInvokesEachStageAndExactlyOneSave(
            Phase6SceneSetupTarget target)
        {
            var scope = new FullEditorStateScope();
            try
            {
                ArrangeSaveRequired(target);
                var stages = new List<Phase6SceneSetupStage>();
                var saves = new List<(Phase6SceneSetupTarget Target, string Path)>();
                Phase6DecorationSceneSetup.FaultInjectorForTests = stages.Add;
                Phase6DecorationSceneSetup.SaveSceneObserverForTests =
                    (observedTarget, path) => saves.Add((observedTarget, path));

                Configure(target);

                Assert.That(stages, Is.EqualTo(new[]
                {
                    Phase6SceneSetupStage.BeforeMutation,
                    Phase6SceneSetupStage.BeforeSave,
                    Phase6SceneSetupStage.AfterSave
                }));
                Assert.That(saves, Has.Count.EqualTo(1));
                Assert.That(saves[0].Target, Is.EqualTo(target));
                Assert.That(saves[0].Path, Is.EqualTo(PathFor(target)));
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe, null)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupStage.BeforeMutation)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupStage.BeforeSave)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupStage.AfterSave)]
        [TestCase(Phase6SceneSetupTarget.Validation, null)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupStage.BeforeMutation)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupStage.BeforeSave)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupStage.AfterSave)]
        public void Configure_Target_SuccessAndFaultCloseCandidatesAndCleanRunArtifacts(
            Phase6SceneSetupTarget target,
            Phase6SceneSetupStage? injectedStage)
        {
            var scope = new FullEditorStateScope();
            try
            {
                ArrangeSaveRequired(target);
                var beforeTree = BackupTreeFingerprint();
                var beforeHandles = SceneHandles();
                if (injectedStage.HasValue)
                {
                    Phase6DecorationSceneSetup.FaultInjectorForTests = stage =>
                    {
                        if (stage == injectedStage.Value)
                            throw new Task8InjectedFaultException("cleanup");
                    };
                    Assert.Throws<Task8InjectedFaultException>(() => Configure(target));
                }
                else
                {
                    Configure(target);
                }

                Assert.That(SceneHandles(), Is.EqualTo(beforeHandles));
                Assert.That(BackupTreeFingerprint(), Is.EqualTo(beforeTree));
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe, "duplicate")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "wrong-prefab")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "extra-child")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "ui-extra-child")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "ui-extra-component")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "ui-active-drift")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "ui-rect-drift")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "ui-child-order-drift")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "environment-extra-component")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "environment-prefab-extra-child")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "environment-prefab-extra-component")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "environment-prefab-override")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "environment-child-order")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "decoration-space-extra-child")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "decoration-space-extra-component")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "decoration-space-child-order")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "preview-root-transform")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "action-extra-child")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "modal-extra-component")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "action-internal-active-drift")]
        [TestCase(Phase6SceneSetupTarget.MainCafe, "catalogue-internal-component")]
        [TestCase(Phase6SceneSetupTarget.Validation, "duplicate")]
        [TestCase(Phase6SceneSetupTarget.Validation, "wrong-prefab")]
        [TestCase(Phase6SceneSetupTarget.Validation, "extra-child")]
        [TestCase(Phase6SceneSetupTarget.Validation, "extra-root")]
        [TestCase(Phase6SceneSetupTarget.Validation, "ui-extra-child")]
        [TestCase(Phase6SceneSetupTarget.Validation, "ui-extra-component")]
        [TestCase(Phase6SceneSetupTarget.Validation, "ui-active-drift")]
        [TestCase(Phase6SceneSetupTarget.Validation, "ui-rect-drift")]
        [TestCase(Phase6SceneSetupTarget.Validation, "ui-child-order-drift")]
        [TestCase(Phase6SceneSetupTarget.Validation, "environment-extra-component")]
        [TestCase(Phase6SceneSetupTarget.Validation, "environment-prefab-extra-child")]
        [TestCase(Phase6SceneSetupTarget.Validation, "environment-prefab-extra-component")]
        [TestCase(Phase6SceneSetupTarget.Validation, "environment-prefab-override")]
        [TestCase(Phase6SceneSetupTarget.Validation, "environment-child-order")]
        [TestCase(Phase6SceneSetupTarget.Validation, "decoration-space-extra-child")]
        [TestCase(Phase6SceneSetupTarget.Validation, "decoration-space-extra-component")]
        [TestCase(Phase6SceneSetupTarget.Validation, "decoration-space-child-order")]
        [TestCase(Phase6SceneSetupTarget.Validation, "preview-root-transform")]
        [TestCase(Phase6SceneSetupTarget.Validation, "action-extra-child")]
        [TestCase(Phase6SceneSetupTarget.Validation, "modal-extra-component")]
        [TestCase(Phase6SceneSetupTarget.Validation, "action-internal-active-drift")]
        [TestCase(Phase6SceneSetupTarget.Validation, "catalogue-internal-component")]
        public void Configure_Target_RefusesUnknownSameNameWrongPrefabAndUnexplainedOwnedChildWithoutDeletingAnything(
            Phase6SceneSetupTarget target,
            string hostileKind)
        {
            var scope = new FullEditorStateScope();
            Scene hostileCandidate = default;
            try
            {
                if (target == Phase6SceneSetupTarget.Validation)
                {
                    RemoveExactTarget(ValidationPath);
                    Phase6DecorationSceneSetup.ConfigureValidationScene();
                    hostileCandidate = EditorSceneManager.OpenScene(
                        ValidationPath,
                        OpenSceneMode.Additive);
                    SeedHostile(hostileCandidate, hostileKind);
                    Assert.That(EditorSceneManager.SaveScene(hostileCandidate), Is.True);
                    EditorSceneManager.CloseScene(hostileCandidate, true);
                    hostileCandidate = default;
                }
                else
                {
                    Configure(target);
                    var scene = OpenTargetForTest(target, out var openedByTest);
                    SeedHostile(scene, hostileKind);
                    Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
                    if (openedByTest) EditorSceneManager.CloseScene(scene, true);
                }

                var unrelated = OpenUnrelatedFixtureForTest();
                var unrelatedObject = new GameObject("Task8_Hostile_Caller_Object");
                SceneManager.MoveGameObjectToScene(unrelatedObject, unrelated);
                EditorSceneManager.MarkSceneDirty(unrelated);
                Selection.activeObject = unrelatedObject;
                var before = CanonicalTargetSnapshot.Capture(target);
                var callerBefore = FullStateFingerprint.Capture();

                Assert.Throws<InvalidOperationException>(() => Configure(target));

                var after = CanonicalTargetSnapshot.Capture(target);
                Assert.That(after, Is.EqualTo(before));
                Assert.That(FullStateFingerprint.Capture(), Is.EqualTo(callerBefore));
                Assert.That(unrelated.isDirty, Is.True);
                Assert.That(unrelatedObject, Is.SameAs(Selection.activeObject));
            }
            finally
            {
                if (hostileCandidate.IsValid() && hostileCandidate.isLoaded)
                    EditorSceneManager.CloseScene(hostileCandidate, true);
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe, null)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupStage.BeforeMutation)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupStage.BeforeSave)]
        [TestCase(Phase6SceneSetupTarget.MainCafe, Phase6SceneSetupStage.AfterSave)]
        [TestCase(Phase6SceneSetupTarget.Validation, null)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupStage.BeforeMutation)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupStage.BeforeSave)]
        [TestCase(Phase6SceneSetupTarget.Validation, Phase6SceneSetupStage.AfterSave)]
        public void Configure_Target_RestoresOrderedMixedSelectionAndActiveIndexForRetainedTargetObjectOnSuccessAndAllFaultStages(
            Phase6SceneSetupTarget target,
            Phase6SceneSetupStage? injectedStage)
        {
            var scope = new FullEditorStateScope();
            try
            {
                Configure(target);
                var targetScene = OpenTargetForTest(target, out _);
                ArrangeSaveRequiredInLoadedTarget(target, targetScene);
                var callerFixture = CreateDirtyCallerFixture(scope, target, targetScene);

                if (injectedStage.HasValue)
                {
                    Phase6DecorationSceneSetup.FaultInjectorForTests = stage =>
                    {
                        if (stage == injectedStage.Value)
                            throw new Task8InjectedFaultException(stage.ToString());
                    };
                    Assert.Throws<Task8InjectedFaultException>(() => Configure(target));
                }
                else
                {
                    Configure(target);
                }

                callerFixture.AssertPreserved();
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConfigureMainCafe_SelectedTemporaryFixtureRootOrDescendantRefusesBeforeMutation(
            bool selectDescendant)
        {
            var scope = new FullEditorStateScope();
            try
            {
                var scene = OpenTargetForTest(Phase6SceneSetupTarget.MainCafe, out _);
                var root = FindAll(scene, TemporaryRoot).SingleOrDefault();
                if (root == null)
                {
                    root = new GameObject(TemporaryRoot);
                    SceneManager.MoveGameObjectToScene(root, scene);
                    var seededChild = new GameObject("Task8_Selected_Temporary_Child");
                    seededChild.transform.SetParent(root.transform, false);
                    Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
                }
                else if (root.transform.childCount == 0)
                {
                    var seededChild = new GameObject("Task8_Selected_Temporary_Child");
                    seededChild.transform.SetParent(root.transform, false);
                    Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
                }
                Selection.activeObject = selectDescendant
                    ? root.transform.GetChild(0).gameObject
                    : root;
                var before = FullStateFingerprint.Capture();
                var beforeMutationCalled = false;
                Phase6DecorationSceneSetup.FaultInjectorForTests = stage =>
                    beforeMutationCalled |= stage == Phase6SceneSetupStage.BeforeMutation;

                var exception = Assert.Throws<InvalidOperationException>(
                    Phase6DecorationSceneSetup.ConfigureMainCafe);

                Assert.That(exception.Message, Does.Contain("deselect").IgnoreCase);
                Assert.That(beforeMutationCalled, Is.False);
                Assert.That(FullStateFingerprint.Capture(), Is.EqualTo(before));
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ConfigureMainCafe_InstantiatesCanonicalPhase4EnvironmentAndAllowedFloorOverride()
        {
            WithConfigured(Phase6SceneSetupTarget.MainCafe, scene =>
            {
                AssertExactTransform(
                    Find(scene, "P4_Environment").transform,
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one);
                Assert.That(Find(scene, "P4_Environment").transform.parent, Is.Null);
                AssertPrefab(scene, "P4_Floor_8x8",
                    "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Floor_8x8.prefab",
                    Vector3.zero, Quaternion.identity);
                AssertPrefab(scene, "P4_Wall_BackLeft",
                    "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Wall_BackLeft_8x3.prefab",
                    new Vector3(0f, 0.5f, 4f), Quaternion.identity);
                AssertPrefab(scene, "P4_Wall_BackRight",
                    "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Wall_BackRight_8x3.prefab",
                    new Vector3(4f, 0.5f, 0f), Quaternion.Euler(0f, 90f, 0f));
                AssertPrefab(scene, "P4_Entrance",
                    "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Entrance_2x2.prefab",
                    new Vector3(0f, 0f, -4f), Quaternion.identity);
                AssertPrefab(scene, "P4_Window_BackRight_C3_R0",
                    "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Window_01.prefab",
                    new Vector3(-0.5f, 0.5f, -0.061f), Quaternion.identity);
                Assert.That(Find(scene, "P4_Floor_8x8").transform.Find("GridOverlay").gameObject.activeSelf,
                    Is.False);
            });
        }

        [Test]
        public void ConfigureMainCafe_BindsOneDecorationOwnerAndSouthwestRoots()
        {
            WithConfigured(Phase6SceneSetupTarget.MainCafe, scene =>
            {
                var owner = Find(scene, "Phase6_DecorationRuntime");
                Assert.That(FindAll<DecorationModeController>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<CafeLayoutRuntime>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<FurnitureSceneRegistry>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<FurniturePreviewView>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<GridHighlightView>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<InputSystemDecorationTouchSource>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<MouseDecorationInputSource>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<DecorationCameraDriver>(scene), Has.Length.EqualTo(1));
                Assert.That(owner.GetComponents<Component>().Select(item => item.GetType()),
                    Is.EquivalentTo(new[]
                    {
                        typeof(Transform), typeof(CafeLayoutRuntime),
                        typeof(DecorationModeController), typeof(FurnitureSceneRegistry),
                        typeof(FurniturePreviewView), typeof(GridHighlightView),
                        typeof(InputSystemDecorationTouchSource), typeof(DecorationCameraDriver),
                        typeof(MouseDecorationInputSource)
                    }));
                AssertExactTransform(
                    owner.transform,
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one);
                Assert.That(owner.transform.parent, Is.Null);
                var space = owner.transform.Find("DecorationSpaceRoot");
                AssertExactTransform(
                    space,
                    new Vector3(-4f, 0f, -4f),
                    Quaternion.identity,
                    Vector3.one,
                    local: true);
                Assert.That(space.parent, Is.SameAs(owner.transform));
                Assert.That(space.GetComponents<Component>().Select(item => item.GetType()),
                    Is.EqualTo(new[] { typeof(Transform) }));
                Assert.That(space.Cast<Transform>().Select(child => child.name),
                    Is.EqualTo(new[]
                    {
                        "GridVisualRoot", "FurnitureRepresentationRoot", "FurniturePreviewRoot"
                    }));
                foreach (var name in new[]
                         { "GridVisualRoot", "FurnitureRepresentationRoot", "FurniturePreviewRoot" })
                {
                    var child = space.Find(name);
                    Assert.That(child, Is.Not.Null, name);
                    Assert.That(child.localPosition, Is.EqualTo(Vector3.zero));
                    Assert.That(child.localRotation, Is.EqualTo(Quaternion.identity));
                    Assert.That(child.localScale, Is.EqualTo(Vector3.one));
                }
                Assert.That(space.Find("FurnitureRepresentationRoot").childCount, Is.Zero);
                AssertControllerBindings(owner.GetComponent<DecorationModeController>());
                AssertSharedDecorationBindings(scene);
            });
        }

        [Test]
        public void ConfigureMainCafe_ReusesPhase5UiAndAuthorsOneDecorationRightRail()
        {
            WithConfigured(Phase6SceneSetupTarget.MainCafe, scene =>
            {
                Assert.That(FindAll(scene, "UI Root"), Has.Length.EqualTo(1));
                Assert.That(FindAll<Canvas>(scene), Has.Length.EqualTo(3));
                Assert.That(FindAll<EventSystem>(scene), Has.Length.EqualTo(1));
                Assert.That(FindComponentsByFullName(
                    scene,
                    "UnityEngine.InputSystem.UI.InputSystemUIInputModule"), Has.Length.EqualTo(1));
                Assert.That(FindAll<StandaloneInputModule>(scene), Is.Empty);
                var hud = Find(scene, "HUD Layer").transform;
                Assert.That(hud.Find("Decoration Safe Area"), Is.Not.Null);
                Assert.That(hud.Find("Decoration Safe Area/RightRail"), Is.Not.Null);
                Assert.That(hud.Find(
                    "Decoration Safe Area/RightRail/DecorationModeButton"), Is.Not.Null);
                Assert.That(FindAll<DecorationCatalogueView>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<DecorationActionBarView>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<DecorationStoreModalView>(scene), Has.Length.EqualTo(1));
                AssertExactTask6Ui(scene);
            });
        }

        [Test]
        public void ConfigureMainCafe_AcceptsCanonicalPhase7SceneOwnedFloorRange()
        {
            WithConfigured(Phase6SceneSetupTarget.MainCafe, scene =>
            {
                var catalogue = FindAll<DecorationCatalogueView>(scene).Single();
                var range = FindAll<DecorationFloorRangeView>(scene).Single();

                Assert.That(catalogue.SurfaceFooterHost, Is.Not.Null);
                Assert.That(range.transform.parent,
                    Is.SameAs(catalogue.SurfaceFooterHost));
                Assert.That(range.GetComponentsInChildren<Button>(true),
                    Has.Length.EqualTo(2));
            });
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe)]
        [TestCase(Phase6SceneSetupTarget.Validation)]
        public void PublishedTarget_DirectReopenContainsPersistedTimeSelectedVisualsAndBindings(
            Phase6SceneSetupTarget target)
        {
            var scope = new FullEditorStateScope();
            Scene scene = default;
            var openedByTest = false;
            try
            {
                Assert.That(File.Exists(PathFor(target)), Is.True,
                    "This regression must inspect the frozen published Scene without setup.");
                scene = OpenTargetForTest(target, out openedByTest);
                var rail = Find(scene, "RightRail").transform;
                var controls = rail.GetComponent<TimeControlPanel>();
                Assert.That(controls, Is.Not.Null);
                var serialized = new SerializedObject(controls);
                var names = new[] { "PauseButton", "NormalButton", "FastButton" };
                var properties = new[]
                {
                    "pauseSelectedVisual", "normalSelectedVisual", "fastSelectedVisual"
                };

                for (var index = 0; index < names.Length; index++)
                {
                    var selected = rail.Find(names[index] + "/SelectedVisual");
                    Assert.That(selected, Is.Not.Null,
                        target + " frozen Scene is missing " + names[index] + "/SelectedVisual");
                    Assert.That(selected.gameObject.activeSelf, Is.EqualTo(index == 1),
                        target + " " + names[index] + " initial selected state");
                    Assert.That(serialized.FindProperty(properties[index]).objectReferenceValue,
                        Is.SameAs(selected.gameObject),
                        target + " " + properties[index] + " must persist the authored reference");
                }
            }
            finally
            {
                if (openedByTest && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe)]
        [TestCase(Phase6SceneSetupTarget.Validation)]
        public void Configure_Target_PersistsNullIndicatorReferenceRepairAndSecondRunDoesNotSave(
            Phase6SceneSetupTarget target)
        {
            var scope = new FullEditorStateScope();
            try
            {
                var scene = OpenTargetForTest(target, out _);
                var indicator = Find(scene, "GameTimeStatusIndicator")
                    .GetComponent<GameTimeStatusIndicator>();
                var serialized = new SerializedObject(indicator);
                serialized.FindProperty("rotatingVisual").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);

                var saves = 0;
                Phase6DecorationSceneSetup.SaveSceneObserverForTests = (_, _) => saves++;
                Configure(target);
                Assert.That(saves, Is.EqualTo(1),
                    "Repairing one persisted null indicator reference must save exactly once.");

                var repaired = OpenTargetForTest(target, out _);
                var repairedIndicator = Find(repaired, "GameTimeStatusIndicator")
                    .GetComponent<GameTimeStatusIndicator>();
                var repairedSerialized = new SerializedObject(repairedIndicator);
                var expected = Find(repaired, "GameTimeStatusIndicator").transform
                    .Find("RotatingVisual").GetComponent<RectTransform>();
                Assert.That(repairedSerialized.FindProperty("rotatingVisual").objectReferenceValue,
                    Is.SameAs(expected));

                saves = 0;
                Configure(target);
                Assert.That(saves, Is.Zero,
                    "A healthy second setup run must not save after the repair is persisted.");
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe)]
        [TestCase(Phase6SceneSetupTarget.Validation)]
        public void Configure_Target_PersistsRightRailSiblingRepairAndSecondRunDoesNotSave(
            Phase6SceneSetupTarget target)
        {
            var scope = new FullEditorStateScope();
            try
            {
                var scene = OpenTargetForTest(target, out _);
                var rail = Find(scene, "RightRail").transform;
                rail.Find("FastButton").SetSiblingIndex(0);
                Assert.That(rail.Cast<Transform>().Select(child => child.name),
                    Is.Not.EqualTo(new[]
                    {
                        "DecorationModeButton", "GameTimeStatusIndicator",
                        "PauseButton", "NormalButton", "FastButton"
                    }));
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);

                var saves = 0;
                Phase6DecorationSceneSetup.SaveSceneObserverForTests = (_, _) => saves++;
                Configure(target);
                Assert.That(saves, Is.EqualTo(1),
                    "Repairing one persisted RightRail sibling drift must save exactly once.");

                var repaired = OpenTargetForTest(target, out _);
                Assert.That(Find(repaired, "RightRail").transform.Cast<Transform>()
                        .Select(child => child.name),
                    Is.EqualTo(new[]
                    {
                        "DecorationModeButton", "GameTimeStatusIndicator",
                        "PauseButton", "NormalButton", "FastButton"
                    }));

                saves = 0;
                Configure(target);
                Assert.That(saves, Is.Zero,
                    "A healthy second setup run must not save after the order is persisted.");
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void Configure_Validation_RemovesInterruptedLegacyTimePanelBesideRightRail()
        {
            var scope = new FullEditorStateScope();
            try
            {
                var scene = OpenTargetForTest(Phase6SceneSetupTarget.Validation, out _);
                var rail = Find(scene, "RightRail").transform;
                var legacyPanel = new GameObject("TimePanel", typeof(RectTransform));
                legacyPanel.transform.SetParent(Find(scene, "HUD Layer").transform, false);
                foreach (var buttonName in new[] { "PauseButton", "NormalButton", "FastButton" })
                {
                    var duplicate = UnityEngine.Object.Instantiate(rail.Find(buttonName).gameObject);
                    duplicate.name = buttonName;
                    duplicate.transform.SetParent(legacyPanel.transform, false);
                }
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);

                Configure(Phase6SceneSetupTarget.Validation);

                var repaired = OpenTargetForTest(Phase6SceneSetupTarget.Validation, out _);
                Assert.That(FindAll(repaired, "TimePanel"), Is.Empty);
                Assert.That(FindAll(repaired, "PauseButton"), Has.Length.EqualTo(1));
                Assert.That(FindAll(repaired, "NormalButton"), Has.Length.EqualTo(1));
                Assert.That(FindAll(repaired, "FastButton"), Has.Length.EqualTo(1));
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [TestCase(Phase6SceneSetupTarget.MainCafe)]
        [TestCase(Phase6SceneSetupTarget.Validation)]
        public void Configure_Target_AuthorsOneOrderedDecorationRightRailInsideSafeArea(
            Phase6SceneSetupTarget target)
        {
            WithConfigured(target, scene =>
            {
                var safeArea = Find(scene, "Decoration Safe Area").transform;
                var rail = safeArea.Find("RightRail");
                Assert.That(rail, Is.Not.Null);
                Assert.That(rail.Cast<Transform>().Select(child => child.name),
                    Is.EqualTo(new[]
                    {
                        "DecorationModeButton",
                        "GameTimeStatusIndicator",
                        "PauseButton",
                        "NormalButton",
                        "FastButton"
                    }));

                var railRect = (RectTransform)rail;
                Assert.That(railRect.anchorMin, Is.EqualTo(Vector2.one));
                Assert.That(railRect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(railRect.pivot, Is.EqualTo(Vector2.one));
                Assert.That(railRect.anchoredPosition, Is.EqualTo(new Vector2(-24f, -24f)));
                Assert.That(railRect.sizeDelta, Is.EqualTo(new Vector2(180f, 336f)));

                var expectedY = new[] { 0f, -64f, -128f, -192f, -256f };
                for (var index = 0; index < rail.childCount; index++)
                {
                    var childRect = (RectTransform)rail.GetChild(index);
                    Assert.That(childRect.anchorMin, Is.EqualTo(Vector2.one), childRect.name);
                    Assert.That(childRect.anchorMax, Is.EqualTo(Vector2.one), childRect.name);
                    Assert.That(childRect.pivot, Is.EqualTo(Vector2.one), childRect.name);
                    Assert.That(childRect.anchoredPosition,
                        Is.EqualTo(new Vector2(0f, expectedY[index])), childRect.name);
                    Assert.That(childRect.sizeDelta,
                        Is.EqualTo(new Vector2(180f, 56f)), childRect.name);
                }

                var indicator = rail.Find("GameTimeStatusIndicator");
                Assert.That(indicator.GetComponent<GameTimeStatusIndicator>(), Is.Not.Null);
                Assert.That(indicator.GetComponent<Image>(), Is.Not.Null);
                Assert.That(indicator.GetComponent<Image>().raycastTarget, Is.False);

                var timePanel = rail.GetComponent<TimeControlPanel>();
                Assert.That(timePanel, Is.Not.Null);
                var serialized = new SerializedObject(timePanel);
                Assert.That(serialized.FindProperty("pauseButton").objectReferenceValue,
                    Is.SameAs(rail.Find("PauseButton").GetComponent<Button>()));
                Assert.That(serialized.FindProperty("normalButton").objectReferenceValue,
                    Is.SameAs(rail.Find("NormalButton").GetComponent<Button>()));
                Assert.That(serialized.FindProperty("fastButton").objectReferenceValue,
                    Is.SameAs(rail.Find("FastButton").GetComponent<Button>()));
                var speedNames = new[] { "PauseButton", "NormalButton", "FastButton" };
                for (var index = 0; index < speedNames.Length; index++)
                {
                    var selected = rail.Find(speedNames[index] + "/SelectedVisual");
                    Assert.That(selected, Is.Not.Null, speedNames[index]);
                    Assert.That(selected.GetComponent<Image>(), Is.Not.Null, speedNames[index]);
                    Assert.That(selected.GetComponent<Image>().raycastTarget, Is.False,
                        speedNames[index]);
                    Assert.That(selected.gameObject.activeSelf,
                        Is.EqualTo(index == 1), speedNames[index] + " initial selection");
                    var propertyName = index == 0
                        ? "pauseSelectedVisual"
                        : index == 1
                            ? "normalSelectedVisual"
                            : "fastSelectedVisual";
                    Assert.That(serialized.FindProperty(propertyName).objectReferenceValue,
                        Is.SameAs(selected.gameObject), propertyName);
                }
            });
        }

        [Test]
        public void ConfigureMainCafe_RemovesOnlyApprovedTemporaryRoot()
        {
            WithConfigured(Phase6SceneSetupTarget.MainCafe, scene =>
            {
                Assert.That(FindAll(scene, TemporaryRoot), Is.Empty);
                Assert.That(FindAll(scene, "Phase0_Runtime"), Has.Length.EqualTo(1));
                Assert.That(FindAll(scene, "Main Camera"), Has.Length.EqualTo(1));
                Assert.That(FindAll(scene, "Directional Light"), Has.Length.EqualTo(1));
                Assert.That(FindAll(scene, "RightRail"), Has.Length.EqualTo(1));
            });
        }

        [Test]
        public void ConfigureValidationScene_AuthorsExactPhase0CameraLightingEnvironmentDecorationAndUiBase()
        {
            WithConfigured(Phase6SceneSetupTarget.Validation, scene =>
            {
                Assert.That(scene.GetRootGameObjects().Select(root => root.name),
                    Is.EqualTo(new[]
                    {
                        "Main Camera",
                        "Directional Light",
                        "Phase0_Runtime",
                        "P4_Environment",
                        "Phase6_DecorationRuntime",
                        "UI Root",
                        "Phase6_ContractReferences"
                    }));
                var cameraObject = Find(scene, "Main Camera");
                var camera = cameraObject.GetComponent<UnityEngine.Camera>();
                Assert.That(cameraObject.transform.position, Is.EqualTo(new Vector3(-10f, 10f, -10f)));
                Assert.That(Quaternion.Angle(cameraObject.transform.rotation,
                    Quaternion.Euler(35.264f, 45f, 0f)), Is.LessThan(0.1f));
                Assert.That(camera.orthographic, Is.True);
                Assert.That(camera.orthographicSize, Is.EqualTo(7f));
                Assert.That(cameraObject.GetComponent<AudioListener>(), Is.Not.Null);
                Assert.That(cameraObject.GetComponent<UniversalAdditionalCameraData>(), Is.Not.Null);
                Assert.That(cameraObject.GetComponents<Component>().Select(item => item.GetType()),
                    Is.EquivalentTo(new[]
                    {
                        typeof(Transform), typeof(UnityEngine.Camera), typeof(AudioListener),
                        typeof(UniversalAdditionalCameraData)
                    }));
                Assert.That(FindAll<Light>(scene), Has.Length.EqualTo(1));
                var light = Find(scene, "Directional Light");
                Assert.That(light.transform.position, Is.EqualTo(new Vector3(0f, 3f, 0f)));
                Assert.That(Quaternion.Angle(
                    light.transform.rotation,
                    Quaternion.Euler(50f, -30f, 0f)), Is.LessThan(0.1f));
                Assert.That(light.GetComponent<Light>().intensity, Is.EqualTo(2f));
                Assert.That(light.GetComponents<Component>().Select(item => item.GetType()),
                    Is.EquivalentTo(new[]
                    {
                        typeof(Transform), typeof(Light),
                        typeof(UniversalAdditionalLightData)
                    }));
                Assert.That(FindAll<GameTimeService>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<MouseCameraInput>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<CafeCameraController>(scene), Has.Length.EqualTo(1));
                Assert.That(FindAll<SceneInteractionController>(scene), Has.Length.EqualTo(1));
                var phase0 = Find(scene, "Phase0_Runtime");
                Assert.That(phase0.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(phase0.transform.rotation, Is.EqualTo(Quaternion.identity));
                Assert.That(phase0.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(phase0.GetComponents<Component>().Select(item => item.GetType()),
                    Is.EquivalentTo(new[]
                    {
                        typeof(Transform), typeof(GameTimeService), typeof(MouseCameraInput),
                        typeof(CafeCameraController), typeof(SceneInteractionController)
                    }));
                AssertPhase0Bindings(phase0, camera);
                Assert.That(FindAll(scene, "P4_Environment"), Has.Length.EqualTo(1));
                Assert.That(FindAll(scene, "Phase6_DecorationRuntime"), Has.Length.EqualTo(1));
                AssertExactTransform(
                    Find(scene, "P4_Environment").transform,
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one);
                Assert.That(Find(scene, "P4_Environment").transform.parent, Is.Null);
                var decorationOwner = Find(scene, "Phase6_DecorationRuntime");
                AssertExactTransform(
                    decorationOwner.transform,
                    Vector3.zero,
                    Quaternion.identity,
                    Vector3.one);
                Assert.That(decorationOwner.transform.parent, Is.Null);
                var decorationSpace = decorationOwner.transform.Find("DecorationSpaceRoot");
                AssertExactTransform(
                    decorationSpace,
                    new Vector3(-4f, 0f, -4f),
                    Quaternion.identity,
                    Vector3.one,
                    local: true);
                Assert.That(decorationSpace.parent, Is.SameAs(decorationOwner.transform));
                Assert.That(decorationSpace.GetComponents<Component>().Select(item => item.GetType()),
                    Is.EqualTo(new[] { typeof(Transform) }));
                Assert.That(decorationSpace.Cast<Transform>().Select(child => child.name),
                    Is.EqualTo(new[]
                    {
                        "GridVisualRoot", "FurnitureRepresentationRoot", "FurniturePreviewRoot"
                    }));
                Assert.That(decorationOwner
                    .GetComponents<Component>().Select(item => item.GetType()),
                    Is.EquivalentTo(new[]
                    {
                        typeof(Transform), typeof(CafeLayoutRuntime),
                        typeof(DecorationModeController), typeof(FurnitureSceneRegistry),
                        typeof(FurniturePreviewView), typeof(GridHighlightView),
                        typeof(InputSystemDecorationTouchSource), typeof(DecorationCameraDriver),
                        typeof(MouseDecorationInputSource)
                    }));
                AssertSharedDecorationBindings(scene);
                AssertPrefab(scene, "P4_Floor_8x8",
                    "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Floor_8x8.prefab",
                    Vector3.zero, Quaternion.identity);
                AssertPrefab(scene, "P4_Wall_BackLeft",
                    "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Wall_BackLeft_8x3.prefab",
                    new Vector3(0f, 0.5f, 4f), Quaternion.identity);
                AssertPrefab(scene, "P4_Wall_BackRight",
                    "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Wall_BackRight_8x3.prefab",
                    new Vector3(4f, 0.5f, 0f), Quaternion.Euler(0f, 90f, 0f));
                AssertPrefab(scene, "P4_Entrance",
                    "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Entrance_2x2.prefab",
                    new Vector3(0f, 0f, -4f), Quaternion.identity);
                AssertPrefab(scene, "P4_Window_BackRight_C3_R0",
                    "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Window_01.prefab",
                    new Vector3(-0.5f, 0.5f, -0.061f), Quaternion.identity);
                Assert.That(Find(scene, "P4_Floor_8x8").transform.Find("GridOverlay")
                    .gameObject.activeSelf, Is.False);
                Assert.That(FindAll(scene, "UI Root"), Has.Length.EqualTo(1));
                Assert.That(Find(scene, "EventSystem").transform.parent.name, Is.EqualTo("UI Root"));
            });
        }

        [Test]
        public void ConfigureValidationScene_AuthorsExactRightRailButtonsIndicatorAndDirectChildEventSystem()
        {
            WithConfigured(Phase6SceneSetupTarget.Validation, scene =>
            {
                var panel = Find(scene, "RightRail");
                Assert.That(panel.transform.parent.name, Is.EqualTo("Decoration Safe Area"));
                var rect = (RectTransform)panel.transform;
                Assert.That(rect.anchoredPosition, Is.EqualTo(new Vector2(-24f, -24f)));
                Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(180f, 336f)));
                Assert.That(panel.GetComponent<TimeControlPanel>(), Is.Not.Null);
                Assert.That(ReadObjectReference(
                    panel.GetComponent<TimeControlPanel>(),
                    "gameTimeService"),
                    Is.SameAs(Find(scene, "Phase0_Runtime").GetComponent<GameTimeService>()));
                var names = new[] { "PauseButton", "NormalButton", "FastButton" };
                var labels = new[] { "Pause", "1x", "2x" };
                var ys = new[] { -128f, -192f, -256f };
                var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(
                    "Assets/UI/Phase5/Theme/AnimalCafeUiTheme.asset");
                for (var index = 0; index < names.Length; index++)
                {
                    var button = Find(scene, names[index]);
                    Assert.That(button.transform.parent, Is.SameAs(panel.transform));
                    Assert.That(((RectTransform)button.transform).anchoredPosition,
                        Is.EqualTo(new Vector2(0f, ys[index])));
                    Assert.That(((RectTransform)button.transform).sizeDelta,
                        Is.EqualTo(new Vector2(180f, 56f)));
                    Assert.That(button.GetComponentInChildren<TMP_Text>(true).text,
                        Is.EqualTo(labels[index]));
                    Assert.That(button.GetComponent<AnimalCafeButtonView>(), Is.Not.Null);
                    var unityButton = button.GetComponent<Button>();
                    Assert.That(unityButton, Is.Not.Null);
                    Assert.That(unityButton.onClick.GetPersistentEventCount(), Is.Zero);
                    var label = button.GetComponentInChildren<TMP_Text>(true);
                    Assert.That(label.transform.parent, Is.SameAs(button.transform));
                    Assert.That(label.raycastTarget, Is.False);
                    Assert.That(label.color, Is.EqualTo(Color.white));
                    Assert.That(label.font, Is.SameAs(theme.Typography.Label.FontAsset));
                    Assert.That(label.fontSize, Is.EqualTo(theme.Typography.Label.FontSize));
                    var buttonView = new SerializedObject(
                        button.GetComponent<AnimalCafeButtonView>());
                    Assert.That(buttonView.FindProperty("theme").objectReferenceValue,
                        Is.SameAs(theme));
                    Assert.That(buttonView.FindProperty("role").enumValueIndex,
                        Is.EqualTo((int)UiButtonRole.Primary));
                    Assert.That(button.GetComponents<Component>().Select(item => item.GetType()),
                        Is.SupersetOf(new[]
                        {
                            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                            typeof(Button), typeof(Shadow), typeof(AnimalCafeButtonView)
                        }));
                }
                var panelSerialized = new SerializedObject(panel.GetComponent<TimeControlPanel>());
                Assert.That(panelSerialized.FindProperty("pauseButton").objectReferenceValue,
                    Is.SameAs(Find(scene, "PauseButton").GetComponent<Button>()));
                Assert.That(panelSerialized.FindProperty("normalButton").objectReferenceValue,
                    Is.SameAs(Find(scene, "NormalButton").GetComponent<Button>()));
                Assert.That(panelSerialized.FindProperty("fastButton").objectReferenceValue,
                    Is.SameAs(Find(scene, "FastButton").GetComponent<Button>()));
                var eventSystem = Find(scene, "EventSystem");
                Assert.That(eventSystem.transform.parent.name, Is.EqualTo("UI Root"));
                var inputModule = eventSystem.GetComponents<MonoBehaviour>().Single(component =>
                    component.GetType().FullName
                    == "UnityEngine.InputSystem.UI.InputSystemUIInputModule");
                var actionsProperty = inputModule.GetType().GetProperty("actionsAsset");
                Assert.That(actionsProperty, Is.Not.Null);
                Assert.That(actionsProperty.GetValue(inputModule), Is.Not.Null);
                foreach (var actionName in new[]
                         { "point", "leftClick", "scrollWheel", "submit", "cancel" })
                {
                    var property = inputModule.GetType().GetProperty(actionName);
                    Assert.That(property, Is.Not.Null, actionName);
                    Assert.That(property.GetValue(inputModule), Is.Not.Null, actionName);
                }
                Assert.That(eventSystem.GetComponent<StandaloneInputModule>(), Is.Null);
                Assert.That(FindAll<Canvas>(scene).Select(canvas => canvas.name),
                    Is.EquivalentTo(new[] { "HUD Canvas", "Screen Canvas", "Toast Canvas" }));
                Assert.That(Find(scene, "HUD Layer").transform.parent.name,
                    Is.EqualTo("HUD Canvas"));
                Assert.That(Find(scene, "Panel Layer").transform.parent.name,
                    Is.EqualTo("Screen Canvas"));
                Assert.That(Find(scene, "Modal Layer").transform.parent.name,
                    Is.EqualTo("Screen Canvas"));
                Assert.That(Find(scene, "Toast Layer").transform.parent.name,
                    Is.EqualTo("Toast Canvas"));
                var uiRoot = Find(scene, "UI Root");
                Assert.That(uiRoot.GetComponents<UiGraphicRegistration>(), Has.Length.EqualTo(1));
                Assert.That(HierarchyPath(Find(scene, "RightRail").transform),
                    Is.EqualTo(
                        "UI Root/HUD Canvas/HUD Layer/Decoration Safe Area/RightRail"));
                Assert.That(HierarchyPath(Find(scene, "Decoration Safe Area").transform),
                    Is.EqualTo("UI Root/HUD Canvas/HUD Layer/Decoration Safe Area"));
                Assert.That(HierarchyPath(Find(scene, "DecorationModeButton").transform),
                    Is.EqualTo(
                        "UI Root/HUD Canvas/HUD Layer/Decoration Safe Area/RightRail/DecorationModeButton"));
                Assert.That(HierarchyPath(Find(scene, "PF_UI_DecorationCatalogue").transform),
                    Is.EqualTo("UI Root/Screen Canvas/Panel Layer/PF_UI_DecorationCatalogue"));
                Assert.That(HierarchyPath(Find(scene, "PF_UI_DecorationActionBar").transform),
                    Is.EqualTo("UI Root/Screen Canvas/Panel Layer/PF_UI_DecorationActionBar"));
                Assert.That(HierarchyPath(Find(scene, "PF_UI_DecorationStoreModal").transform),
                    Is.EqualTo("UI Root/Screen Canvas/Modal Layer/PF_UI_DecorationStoreModal"));
                var safeRect = Find(scene, "Decoration Safe Area").GetComponent<RectTransform>();
                Assert.That(safeRect.anchorMin, Is.EqualTo(Vector2.zero));
                Assert.That(safeRect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(safeRect.offsetMin, Is.EqualTo(Vector2.zero));
                Assert.That(safeRect.offsetMax, Is.EqualTo(Vector2.zero));
                var toggleRect = Find(scene, "DecorationModeButton").GetComponent<RectTransform>();
                Assert.That(toggleRect.anchorMin, Is.EqualTo(Vector2.one));
                Assert.That(toggleRect.anchorMax, Is.EqualTo(Vector2.one));
                Assert.That(toggleRect.pivot, Is.EqualTo(Vector2.one));
                Assert.That(toggleRect.sizeDelta, Is.EqualTo(new Vector2(180f, 56f)));
                Assert.That(toggleRect.rect.width, Is.GreaterThanOrEqualTo(48f));
                Assert.That(toggleRect.rect.height, Is.GreaterThanOrEqualTo(48f));
                AssertPrefabSource(scene, "UI Root",
                    "Assets/UI/Phase5/Prefabs/PF_UI_Root.prefab");
                AssertPrefabSource(scene, "Decoration Safe Area",
                    "Assets/UI/Phase5/Prefabs/PF_UI_SafeArea.prefab");
                AssertPrefabSource(scene, "DecorationModeButton",
                    "Assets/UI/Phase5/Prefabs/PF_UI_Button_Secondary_Default.prefab");
                AssertPrefabSource(scene, "PF_UI_DecorationCatalogue",
                    "Assets/UI/Phase6/Prefabs/PF_UI_DecorationCatalogue.prefab");
                AssertPrefabSource(scene, "PF_UI_DecorationActionBar",
                    "Assets/UI/Phase6/Prefabs/PF_UI_DecorationActionBar.prefab");
                AssertPrefabSource(scene, "PF_UI_DecorationStoreModal",
                    "Assets/UI/Phase6/Prefabs/PF_UI_DecorationStoreModal.prefab");
                AssertExactTask6Ui(scene);
            });
        }

        [Test]
        public void ConfigureValidationScene_AuthorsReferenceOnlyBlockedLockedRootWithNoGameplayBindings()
        {
            WithConfigured(Phase6SceneSetupTarget.Validation, scene =>
            {
                var root = Find(scene, "Phase6_ContractReferences");
                Assert.That(root.transform.position, Is.EqualTo(Vector3.zero));
                Assert.That(root.transform.rotation, Is.EqualTo(Quaternion.identity));
                Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(root.GetComponents<Component>().Select(item => item.GetType()),
                    Is.EqualTo(new[] { typeof(Transform) }));
                Assert.That(root.transform.childCount, Is.EqualTo(2));
                AssertReferenceOnly(root.transform.Find("BlockedArea_ReferenceOnly"),
                    new Vector3(4.25f, 0.05f, -0.5f), "Blocked - Reference Only",
                    Find(scene, "Main Camera").transform);
                AssertReferenceOnly(root.transform.Find("LockedArea_ReferenceOnly"),
                    new Vector3(4.25f, 0.05f, 1.5f), "Locked - Reference Only",
                    Find(scene, "Main Camera").transform);
            });
        }

        [Test]
        public void ConfigureValidationScene_SecondRunPreservesFullOwnedBaseAndContractReferenceIds()
        {
            var scope = new FullEditorStateScope();
            try
            {
                Phase6DecorationSceneSetup.ConfigureValidationScene();
                var first = CanonicalTargetSnapshot.Capture(Phase6SceneSetupTarget.Validation);
                Phase6DecorationSceneSetup.ConfigureValidationScene();
                var second = CanonicalTargetSnapshot.Capture(Phase6SceneSetupTarget.Validation);
                Assert.That(second, Is.EqualTo(first));
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ConfigureValidationScene_FirstPublishBeforeSaveFailureLeavesSceneAndMetaAbsent()
        {
            AssertFirstPublishRollback(Phase6SceneSetupStage.BeforeSave);
        }

        [Test]
        public void ConfigureValidationScene_FirstPublishAfterSaveFailureDeletesSceneAndMeta()
        {
            AssertFirstPublishRollback(Phase6SceneSetupStage.AfterSave);
        }

        [Test]
        public void SceneSetup_SourceContainsNoGlobalSaveBroadRefreshOrBuildSettingsWriter()
        {
            var scope = new FullEditorStateScope();
            try
            {
                foreach (var path in new[]
                         {
                              "Assets/Editor/Phase6/Phase6DecorationSceneSetup.cs",
                             "Assets/Editor/Phase6/Phase6DecorationValidator.cs"
                         })
                {
                    var source = File.ReadAllText(path);
                    AssertSourceHasNoCall(source, path, @"AssetDatabase\s*\.\s*SaveAssets\s*\(");
                    AssertSourceHasNoCall(source, path, @"AssetDatabase\s*\.\s*SaveAssetIfDirty\s*\(");
                    AssertSourceHasNoCall(source, path, @"AssetDatabase\s*\.\s*Refresh\s*\(");
                    AssertSourceHasNoCall(source, path,
                        @"EditorBuildSettings\s*\.\s*scenes\s*=");

                    if (path.EndsWith("Phase6DecorationSceneSetup.cs", StringComparison.Ordinal))
                    {
                        foreach (var forbiddenBroadCall in new[]
                                 {
                                     @"\bPhase0SceneSetup\b",
                                     @"\bPhase5UiFoundationSceneSetup\b",
                                     @"\bPhase0SceneSetup\s*\.\s*ConfigurePhase0Scene\s*\(",
                                     @"\bPhase5UiFoundationSceneSetup\s*\.\s*BuildScene(?:FromMenu)?\s*\(",
                                     @"\bPhase6DecorationAssetBuilder\s*\.\s*BuildAll\s*\(",
                                     @"\b[A-Za-z_][A-Za-z0-9_]*AssetBuilder\s*\.\s*Build[A-Za-z0-9_]*\s*\("
                                 })
                        {
                            AssertSourceHasNoCall(source, path, forbiddenBroadCall);
                        }
                    }
                }
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ConfigureTargets_PreservesSoleEnabledMainCafeAndKeepsValidationOutOfBuildSettings()
        {
            var scope = new FullEditorStateScope();
            try
            {
                var before = EditorBuildSettings.scenes
                    .Select((entry, index) => $"{index}|{entry.path}|{entry.enabled}")
                    .ToArray();

                Configure(Phase6SceneSetupTarget.MainCafe);
                Configure(Phase6SceneSetupTarget.Validation);

                var after = EditorBuildSettings.scenes
                    .Select((entry, index) => $"{index}|{entry.path}|{entry.enabled}")
                    .ToArray();
                Assert.That(after, Is.EqualTo(before));
                Assert.That(EditorBuildSettings.scenes.Where(entry => entry.enabled)
                    .Select(entry => entry.path),
                    Is.EqualTo(new[] { MainCafePath }));
                Assert.That(EditorBuildSettings.scenes.Select(entry => entry.path),
                    Does.Not.Contain(ValidationPath));
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        [Test]
        public void ApprovedTemporarySourcesHaveZeroLiveConsumersAndMoverRemainsLive()
        {
            var scope = new FullEditorStateScope();
            try
            {
                var retiredPaths = new[]
                {
                    "Assets/Editor/Phase4/MainCafeManualReviewFixtureSetup.cs",
                    "Assets/Editor/Phase4/MainCafeManualReviewFixtureSetup.cs.meta",
                    "Assets/Art/Phase4/Materials/M_TEMP_ManualReviewCube_Moving.mat",
                    "Assets/Art/Phase4/Materials/M_TEMP_ManualReviewCube_Moving.mat.meta",
                    "Assets/Art/Phase4/Materials/M_TEMP_ManualReviewCube_Static.mat",
                    "Assets/Art/Phase4/Materials/M_TEMP_ManualReviewCube_Static.mat.meta"
                };
                Assert.That(retiredPaths.Where(File.Exists), Is.Empty);

                var needles = new[]
                {
                    "MainCafeManualReviewFixtureSetup",
                    "M_TEMP_ManualReviewCube_Moving.mat",
                    "M_TEMP_ManualReviewCube_Static.mat",
                    "0b9fc7de73da78545944df31667ce283",
                    "026932902cbfa024e8c6e9d8e0972787",
                    "a4ad45d7c405f00419b7d47d8034c7de"
                };
                var liveFiles = EnumerateLiveConsumerFiles()
                    .Where(path => !path.EndsWith(
                        "Phase6MainCafeMigrationTests.cs",
                        StringComparison.Ordinal))
                    .ToArray();
                foreach (var needle in needles)
                {
                    Assert.That(liveFiles.Where(path =>
                        File.ReadAllText(path).Contains(needle, StringComparison.Ordinal)),
                        Is.Empty,
                        "Retired consumer remains for " + needle);
                }

                const string moverPath =
                    "Assets/Scripts/Diagnostics/ManualReviewPingPongMover.cs";
                Assert.That(File.Exists(moverPath), Is.True);
                Assert.That(File.Exists(moverPath + ".meta"), Is.True);
                var moverConsumers = liveFiles.Where(path => path != moverPath
                        && path != moverPath + ".meta"
                        && (File.ReadAllText(path).Contains(
                                "ManualReviewPingPongMover",
                                StringComparison.Ordinal)
                            || File.ReadAllText(path).Contains(
                                "02d646b789515c1479526a95632047f0",
                                StringComparison.Ordinal)))
                    .ToArray();
                Assert.That(moverConsumers, Is.Not.Empty,
                    "ManualReviewPingPongMover still supports live Phase 5/manual-review consumers.");
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        private static void AssertFirstPublishRollback(Phase6SceneSetupStage stage)
        {
            var scope = new FullEditorStateScope();
            try
            {
                RemoveExactTarget(ValidationPath);
                var caller = CreateFirstPublishCallerFixture(scope);
                var before = FullStateFingerprint.Capture();
                var beforeBackupTree = BackupTreeFingerprint();
                Phase6DecorationSceneSetup.FaultInjectorForTests = observed =>
                {
                    if (observed == stage)
                        throw new Task8InjectedFaultException(stage.ToString());
                };

                Assert.Throws<Task8InjectedFaultException>(
                    Phase6DecorationSceneSetup.ConfigureValidationScene);

                Assert.That(File.Exists(ValidationPath), Is.False);
                Assert.That(File.Exists(ValidationPath + ".meta"), Is.False);
                Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ValidationPath),
                    Is.Null);
                Assert.That(AssetDatabase.AssetPathToGUID(
                    ValidationPath,
                    AssetPathToGUIDOptions.OnlyExistingAssets), Is.Empty);
                Assert.That(FullStateFingerprint.Capture(), Is.EqualTo(before));
                Assert.That(BackupTreeFingerprint(), Is.EqualTo(beforeBackupTree));
                caller.AssertPreserved();
                Assert.That(Enumerable.Range(0, SceneManager.sceneCount)
                        .Select(SceneManager.GetSceneAt)
                        .Where(scene => string.Equals(
                            scene.path,
                            ValidationPath,
                            StringComparison.Ordinal)),
                    Is.Empty,
                    "First-publish rollback must not leak a Validation candidate handle.");
            }
            finally
            {
                ClearSeams();
                scope.Dispose();
            }
        }

        private static void AssertSourceHasNoCall(
            string source,
            string path,
            string pattern)
        {
            Assert.That(Regex.IsMatch(source, pattern, RegexOptions.CultureInvariant),
                Is.False,
                path + " contains forbidden token pattern " + pattern);
        }

        private static IEnumerable<string> EnumerateLiveConsumerFiles()
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".cs", ".unity", ".prefab", ".asset", ".mat", ".meta", ".md"
            };
            var assets = Directory.GetFiles("Assets", "*", SearchOption.AllDirectories)
                .Where(path => extensions.Contains(Path.GetExtension(path)));
            var beginnerGuide = "Docs/Phase4_Beginner_Guide.md";
            return File.Exists(beginnerGuide)
                ? assets.Concat(new[] { beginnerGuide })
                : assets;
        }

        private static void WithConfigured(
            Phase6SceneSetupTarget target,
            Action<Scene> assertion)
        {
            var scope = new FullEditorStateScope();
            Scene scene = default;
            var openedByTest = false;
            try
            {
                Configure(target);
                scene = OpenTargetForTest(target, out openedByTest);
                assertion(scene);
            }
            finally
            {
                if (openedByTest && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
                ClearSeams();
                scope.Dispose();
            }
        }

        private static void EnsureTargetExists(Phase6SceneSetupTarget target)
        {
            if (target == Phase6SceneSetupTarget.Validation && !File.Exists(ValidationPath))
                Phase6DecorationSceneSetup.ConfigureValidationScene();
        }

        private static void ArrangeSaveRequired(Phase6SceneSetupTarget target)
        {
            if (target == Phase6SceneSetupTarget.Validation)
            {
                RemoveExactTarget(ValidationPath);
                return;
            }

            var scene = OpenTargetForTest(Phase6SceneSetupTarget.MainCafe, out var openedByTest);
            if (FindAll(scene, TemporaryRoot).Length == 0)
            {
                var root = new GameObject(TemporaryRoot);
                SceneManager.MoveGameObjectToScene(root, scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (openedByTest)
                EditorSceneManager.CloseScene(scene, true);
        }

        private static void ArrangeSaveRequiredInLoadedTarget(
            Phase6SceneSetupTarget target,
            Scene targetScene)
        {
            var childName = target == Phase6SceneSetupTarget.MainCafe
                ? "Decoration Safe Area"
                : "LockedArea_ReferenceOnly";
            var targetObject = FindAll(targetScene, childName).FirstOrDefault();
            if (targetObject != null)
            {
                UnityEngine.Object.DestroyImmediate(targetObject);
                EditorSceneManager.SaveScene(targetScene);
            }
        }

        private static void SeedHostile(Scene scene, string hostileKind)
        {
            if (hostileKind == "duplicate")
            {
                var duplicate = new GameObject("Phase6_DecorationRuntime");
                SceneManager.MoveGameObjectToScene(duplicate, scene);
                return;
            }

            if (hostileKind == "wrong-prefab")
            {
                var environment = Find(scene, "P4_Environment");
                var floor = environment.transform.Find("P4_Floor_8x8");
                UnityEngine.Object.DestroyImmediate(floor.gameObject);
                var substitute = GameObject.CreatePrimitive(PrimitiveType.Cube);
                substitute.name = "P4_Floor_8x8";
                substitute.transform.SetParent(environment.transform, false);
                return;
            }

            if (hostileKind == "extra-root")
            {
                var extra = new GameObject("Unknown_Task8_Validation_Root");
                SceneManager.MoveGameObjectToScene(extra, scene);
                return;
            }

            if (hostileKind == "ui-extra-child")
            {
                var safeArea = Find(scene, "Decoration Safe Area");
                var extra = new GameObject("Unknown_Task8_Ui_Child", typeof(RectTransform));
                extra.transform.SetParent(safeArea.transform, false);
                return;
            }

            if (hostileKind == "ui-extra-component")
            {
                Find(scene, "PF_UI_DecorationCatalogue").AddComponent<BoxCollider>();
                return;
            }

            if (hostileKind == "ui-active-drift")
            {
                Find(scene, "PF_UI_DecorationActionBar").SetActive(false);
                return;
            }

            if (hostileKind == "ui-rect-drift")
            {
                var modalRect = Find(scene, "PF_UI_DecorationStoreModal")
                    .GetComponent<RectTransform>();
                modalRect.anchoredPosition += new Vector2(17f, -9f);
                return;
            }

            if (hostileKind == "ui-child-order-drift")
            {
                Find(scene, "PF_UI_DecorationActionBar").transform.SetSiblingIndex(0);
                return;
            }

            if (hostileKind == "environment-extra-component")
            {
                Find(scene, "P4_Environment").AddComponent<BoxCollider>();
                return;
            }

            if (hostileKind == "environment-prefab-extra-child")
            {
                var extra = new GameObject("Task8_ExtraWallChild");
                extra.transform.SetParent(Find(scene, "P4_Wall_BackLeft").transform, false);
                return;
            }

            if (hostileKind == "environment-prefab-extra-component")
            {
                Find(scene, "P4_Wall_BackLeft").AddComponent<BoxCollider>();
                return;
            }

            if (hostileKind == "environment-prefab-override")
            {
                var gridLine = Find(scene, "P4_Floor_8x8").transform.Find("GridOverlay")
                    .GetChild(0);
                gridLine.gameObject.SetActive(!gridLine.gameObject.activeSelf);
                return;
            }

            if (hostileKind == "environment-child-order")
            {
                Find(scene, "P4_Entrance").transform.SetSiblingIndex(0);
                return;
            }

            if (hostileKind == "decoration-space-extra-child")
            {
                var extra = new GameObject("Task8_ExtraDecorationChild");
                extra.transform.SetParent(Find(scene, "DecorationSpaceRoot").transform, false);
                return;
            }

            if (hostileKind == "decoration-space-extra-component")
            {
                Find(scene, "DecorationSpaceRoot").AddComponent<BoxCollider>();
                return;
            }

            if (hostileKind == "decoration-space-child-order")
            {
                Find(scene, "FurniturePreviewRoot").transform.SetSiblingIndex(0);
                return;
            }

            if (hostileKind == "preview-root-transform")
            {
                Find(scene, "FurniturePreviewRoot").transform.localRotation =
                    Quaternion.Euler(0f, 20f, 0f);
                return;
            }

            if (hostileKind == "action-extra-child")
            {
                var extra = new GameObject("Task8_ExtraActionChild", typeof(RectTransform));
                extra.transform.SetParent(
                    Find(scene, "PF_UI_DecorationActionBar").transform,
                    false);
                return;
            }

            if (hostileKind == "modal-extra-component")
            {
                Find(scene, "PF_UI_DecorationStoreModal").AddComponent<BoxCollider>();
                return;
            }

            if (hostileKind == "action-internal-active-drift")
            {
                Find(scene, "ActionPanel").SetActive(false);
                return;
            }

            if (hostileKind == "catalogue-internal-component")
            {
                Find(scene, "ExpandedSheet").AddComponent<BoxCollider>();
                return;
            }

            var owner = Find(scene, "Phase6_DecorationRuntime");
            var child = new GameObject("Unknown_Task8_Owned_Child");
            child.transform.SetParent(owner.transform, false);
        }

        private static void Configure(Phase6SceneSetupTarget target)
        {
            if (target == Phase6SceneSetupTarget.MainCafe)
                Phase6DecorationSceneSetup.ConfigureMainCafe();
            else
                Phase6DecorationSceneSetup.ConfigureValidationScene();
        }

        private static string PathFor(Phase6SceneSetupTarget target) =>
            target == Phase6SceneSetupTarget.MainCafe ? MainCafePath : ValidationPath;

        private static Scene OpenUnrelatedFixtureForTest()
        {
            Assert.That(File.Exists(UnrelatedFixturePath), Is.True);
            return EditorSceneManager.OpenScene(
                UnrelatedFixturePath,
                OpenSceneMode.Additive);
        }

        private static Scene OpenTargetForTest(
            Phase6SceneSetupTarget target,
            out bool openedByTest)
        {
            var path = PathFor(target);
            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var loaded = SceneManager.GetSceneAt(index);
                if (string.Equals(loaded.path, path, StringComparison.Ordinal))
                {
                    openedByTest = false;
                    return loaded;
                }
            }

            openedByTest = true;
            return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        }

        private static string[] SceneOrder() =>
            EditorSceneManager.GetSceneManagerSetup()
                .Select((entry, index) =>
                    $"{index}|{entry.path}|{entry.isLoaded}|{entry.isActive}")
                .ToArray();

        private static ulong[] SceneHandles() => Enumerable.Range(0, SceneManager.sceneCount)
            .Select(index => SceneManager.GetSceneAt(index).handle.GetRawData())
            .ToArray();

        private static string BackupTreeFingerprint()
        {
            if (!Directory.Exists(BackupRoot)) return "<absent>";
            var root = Path.GetFullPath(BackupRoot);
            var directories = Directory.GetDirectories(root, "*", SearchOption.AllDirectories)
                .Select(path => "D:" + path.Substring(root.Length).Replace('\\', '/'));
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Select(path => "F:" + path.Substring(root.Length).Replace('\\', '/')
                    + ":" + Hash(path));
            return string.Join("\n", directories.Concat(files)
                .OrderBy(value => value, StringComparer.Ordinal));
        }

        private static void AssertPrefab(
            Scene scene,
            string name,
            string sourcePath,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            var instance = Find(scene, name);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            Assert.That(source, Is.Not.Null, name + " must remain a Prefab instance.");
            var actualPath = AssetDatabase.GetAssetPath(source);
            Assert.That(actualPath, Is.EqualTo(sourcePath));
            Assert.That(AssetDatabase.AssetPathToGUID(actualPath),
                Is.EqualTo(ExpectedPrefabGuid(sourcePath)));
            Assert.That(instance.transform.localPosition, Is.EqualTo(localPosition));
            Assert.That(Quaternion.Angle(instance.transform.localRotation, localRotation),
                Is.LessThan(0.01f));
            Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));
        }

        private static void AssertExactTransform(
            Transform transform,
            Vector3 expectedPosition,
            Quaternion expectedRotation,
            Vector3 expectedScale,
            bool local = false)
        {
            Assert.That(transform, Is.Not.Null);
            Assert.That(local ? transform.localPosition : transform.position,
                Is.EqualTo(expectedPosition));
            Assert.That(Quaternion.Angle(
                    local ? transform.localRotation : transform.rotation,
                    expectedRotation),
                Is.LessThan(0.01f));
            Assert.That(transform.localScale, Is.EqualTo(expectedScale));
        }

        private static void AssertExactTask6Ui(Scene scene)
        {
            Assert.That(FindAll(scene, "UI Root"), Has.Length.EqualTo(1));
            Assert.That(FindAll<Canvas>(scene).Select(canvas => canvas.name),
                Is.EquivalentTo(new[] { "HUD Canvas", "Screen Canvas", "Toast Canvas" }));
            Assert.That(FindAll<EventSystem>(scene), Has.Length.EqualTo(1));
            var eventSystem = Find(scene, "EventSystem");
            Assert.That(HierarchyPath(eventSystem.transform), Is.EqualTo("UI Root/EventSystem"));
            var inputModules = FindComponentsByFullName(
                scene,
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule");
            Assert.That(inputModules, Has.Length.EqualTo(1));
            Assert.That(inputModules[0].gameObject, Is.SameAs(eventSystem));
            Assert.That(FindAll<StandaloneInputModule>(scene), Is.Empty);
            var actionsProperty = inputModules[0].GetType().GetProperty("actionsAsset");
            Assert.That(actionsProperty, Is.Not.Null);
            var actionsAsset = actionsProperty.GetValue(inputModules[0]) as UnityEngine.Object;
            Assert.That(actionsAsset, Is.Not.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(actionsAsset)),
                Is.EqualTo("ca9f5fa95ffab41fb9a615ab714db018"));
            foreach (var actionName in new[]
                     { "point", "leftClick", "scrollWheel", "submit", "cancel" })
            {
                var property = inputModules[0].GetType().GetProperty(actionName);
                Assert.That(property, Is.Not.Null, actionName);
                Assert.That(property.GetValue(inputModules[0]), Is.Not.Null, actionName);
            }

            var uiRoot = Find(scene, "UI Root");
            Assert.That(FindAll<UiGraphicRegistration>(scene), Has.Length.EqualTo(1));
            Assert.That(uiRoot.GetComponents<UiGraphicRegistration>(), Has.Length.EqualTo(1));
            AssertExactComponents(uiRoot,
                typeof(RectTransform), typeof(UiGraphicRegistration));
            AssertDirectChildren(uiRoot.transform,
                "HUD Canvas", "Screen Canvas", "Toast Canvas", "EventSystem");
            foreach (var canvasName in new[] { "HUD Canvas", "Screen Canvas", "Toast Canvas" })
            {
                AssertExactComponents(Find(scene, canvasName),
                    typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                    typeof(GraphicRaycaster));
            }

            AssertDirectChildren(Find(scene, "HUD Canvas").transform, "HUD Layer");
            var screenCanvas=Find(scene,"Screen Canvas").transform;
            if(screenCanvas.Find("Phase7_UIRuntime")!=null)
                AssertDirectChildren(
                    screenCanvas,
                    "Panel Layer",
                    "Modal Layer",
                    "Phase7_UIRuntime",
                    "PF_UI_Phase7DecorationExitModal");
            else AssertDirectChildren(screenCanvas,"Panel Layer","Modal Layer");
            AssertDirectChildren(Find(scene, "Toast Canvas").transform, "Toast Layer");
            foreach (var layerName in new[]
                     { "HUD Layer", "Panel Layer", "Modal Layer", "Toast Layer" })
            {
                AssertExactComponents(Find(scene, layerName), typeof(RectTransform));
            }
            foreach (var task8LayerName in new[]
                     { "HUD Layer", "Panel Layer", "Modal Layer" })
            {
                var layerRect = Find(scene, task8LayerName).GetComponent<RectTransform>();
                Assert.That(layerRect.anchorMin, Is.EqualTo(Vector2.zero), task8LayerName);
                Assert.That(layerRect.anchorMax, Is.EqualTo(Vector2.one), task8LayerName);
                Assert.That(layerRect.offsetMin, Is.EqualTo(Vector2.zero), task8LayerName);
                Assert.That(layerRect.offsetMax, Is.EqualTo(Vector2.zero), task8LayerName);
            }

            AssertExactComponents(eventSystem,
                typeof(Transform), typeof(EventSystem), inputModules[0].GetType());
            AssertDirectChildren(Find(scene, "HUD Layer").transform,
                "Decoration Safe Area");
            AssertDirectChildren(Find(scene, "Panel Layer").transform,
                "PF_UI_DecorationCatalogue", "PF_UI_DecorationActionBar");
            AssertDirectChildren(Find(scene, "Modal Layer").transform,
                "PF_UI_DecorationStoreModal");
            Assert.That(HierarchyPath(Find(scene, "RightRail").transform),
                Is.EqualTo(
                    "UI Root/HUD Canvas/HUD Layer/Decoration Safe Area/RightRail"));
            Assert.That(HierarchyPath(Find(scene, "Decoration Safe Area").transform),
                Is.EqualTo("UI Root/HUD Canvas/HUD Layer/Decoration Safe Area"));
            Assert.That(HierarchyPath(Find(scene, "DecorationModeButton").transform),
                Is.EqualTo(
                    "UI Root/HUD Canvas/HUD Layer/Decoration Safe Area/RightRail/DecorationModeButton"));
            Assert.That(HierarchyPath(Find(scene, "PF_UI_DecorationCatalogue").transform),
                Is.EqualTo("UI Root/Screen Canvas/Panel Layer/PF_UI_DecorationCatalogue"));
            Assert.That(HierarchyPath(Find(scene, "PF_UI_DecorationActionBar").transform),
                Is.EqualTo("UI Root/Screen Canvas/Panel Layer/PF_UI_DecorationActionBar"));
            Assert.That(HierarchyPath(Find(scene, "PF_UI_DecorationStoreModal").transform),
                Is.EqualTo("UI Root/Screen Canvas/Modal Layer/PF_UI_DecorationStoreModal"));

            var safeArea = Find(scene, "Decoration Safe Area");
            Assert.That(safeArea.activeSelf, Is.True);
            AssertExactComponents(safeArea,
                typeof(RectTransform), typeof(SafeAreaContainer));
            AssertDirectChildren(safeArea.transform, "RightRail");
            var safeRect = safeArea.GetComponent<RectTransform>();
            Assert.That(safeRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(safeRect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(safeRect.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(safeRect.anchoredPosition3D, Is.EqualTo(Vector3.zero));
            Assert.That(safeRect.sizeDelta, Is.EqualTo(Vector2.zero));
            Assert.That(safeRect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(safeRect.offsetMax, Is.EqualTo(Vector2.zero));
            Assert.That(Quaternion.Angle(safeRect.localRotation, Quaternion.identity),
                Is.LessThan(0.01f));
            Assert.That(safeRect.localScale, Is.EqualTo(Vector3.one));

            var toggle = Find(scene, "DecorationModeButton");
            var toggleRect = toggle.GetComponent<RectTransform>();
            Assert.That(toggleRect.anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(toggleRect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(toggleRect.pivot, Is.EqualTo(Vector2.one));
            Assert.That(toggleRect.sizeDelta, Is.EqualTo(new Vector2(180f, 56f)));
            Assert.That(Quaternion.Angle(toggleRect.localRotation, Quaternion.identity),
                Is.LessThan(0.01f));
            Assert.That(toggleRect.localScale, Is.EqualTo(Vector3.one));
            Assert.That(toggleRect.rect.width, Is.GreaterThanOrEqualTo(48f));
            Assert.That(toggleRect.rect.height, Is.GreaterThanOrEqualTo(48f));

            AssertPrefabSource(scene, "UI Root",
                "Assets/UI/Phase5/Prefabs/PF_UI_Root.prefab");
            AssertPrefabSource(scene, "Decoration Safe Area",
                "Assets/UI/Phase5/Prefabs/PF_UI_SafeArea.prefab");
            AssertPrefabSource(scene, "DecorationModeButton",
                "Assets/UI/Phase5/Prefabs/PF_UI_Button_Secondary_Default.prefab");
            AssertExactUiPrefabSubtree(
                scene,
                "DecorationModeButton",
                "Assets/UI/Phase5/Prefabs/PF_UI_Button_Secondary_Default.prefab",
                false,
                Phase5DecorationButtonManifest());
            var catalogueRoot=Find(scene,"PF_UI_DecorationCatalogue");
            var cataloguePath=AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(catalogueRoot));
            var phase7Upgrade=cataloguePath=="Assets/UI/Phase7/Prefabs/PF_UI_Phase7DecorationCatalogue.prefab";
            if(phase7Upgrade)
            {
                AssertExactCurrentPrefabSubtree(catalogueRoot,cataloguePath);
                var actionRoot=Find(scene,"PF_UI_DecorationActionBar");
                AssertExactCurrentPrefabSubtree(actionRoot,"Assets/UI/Phase7/Prefabs/PF_UI_Phase7DecorationActionBar.prefab");
                Assert.That(catalogueRoot.activeSelf,Is.True);Assert.That(actionRoot.activeSelf,Is.True);
            }
            else
            {
                AssertExactUiPrefabSubtree(scene,"PF_UI_DecorationCatalogue","Assets/UI/Phase6/Prefabs/PF_UI_DecorationCatalogue.prefab",true,CatalogueManifest());
                AssertExactUiPrefabSubtree(scene,"PF_UI_DecorationActionBar","Assets/UI/Phase6/Prefabs/PF_UI_DecorationActionBar.prefab",true,ActionBarManifest());
            }
            AssertExactUiPrefabSubtree(
                scene,
                "PF_UI_DecorationStoreModal",
                "Assets/UI/Phase6/Prefabs/PF_UI_DecorationStoreModal.prefab",
                true,
                StoreModalManifest());

            if(!phase7Upgrade){AssertClosedActiveUiRoot(catalogueRoot);AssertClosedActiveUiRoot(Find(scene,"PF_UI_DecorationActionBar"));}
            AssertClosedActiveUiRoot(Find(scene, "PF_UI_DecorationStoreModal"));
        }

        private static void AssertExactCurrentPrefabSubtree(GameObject instance,string prefabPath)
        {
            Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(instance)),Is.EqualTo(prefabPath));
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);Assert.That(source,Is.Not.Null);
            var actual=instance.GetComponentsInChildren<Transform>(true);var expected=source.GetComponentsInChildren<Transform>(true);
            if(prefabPath=="Assets/UI/Phase7/Prefabs/PF_UI_Phase7DecorationCatalogue.prefab")
                actual=actual.Where(item=>
                {
                    var path=RelativePath(instance.transform,item);
                    return path!="SurfaceFooterHost/FloorRange"
                        && !path.StartsWith("SurfaceFooterHost/FloorRange/",StringComparison.Ordinal);
                }).ToArray();
            Assert.That(actual.Select(item=>RelativePath(instance.transform,item)),Is.EqualTo(expected.Select(item=>RelativePath(source.transform,item))));
            for(var index=0;index<actual.Length;index++)Assert.That(actual[index].GetComponents<Component>().Select(item=>item?.GetType()),Is.EqualTo(expected[index].GetComponents<Component>().Select(item=>item?.GetType())));
        }

        private static void AssertClosedActiveUiRoot(GameObject root)
        {
            var group = root.GetComponent<CanvasGroup>();
            Assert.That(root.activeSelf, Is.True, root.name);
            Assert.That(group, Is.Not.Null, root.name);
            Assert.That(group.alpha, Is.EqualTo(0f), root.name);
            Assert.That(group.interactable, Is.False, root.name);
            Assert.That(group.blocksRaycasts, Is.False, root.name);
        }

        private static void AssertExactUiPrefabSubtree(
            Scene scene,
            string rootName,
            string prefabPath,
            bool compareRootRect,
            IReadOnlyList<UiNodeSpec> expected)
        {
            AssertPrefabSource(scene, rootName, prefabPath);
            var root = Find(scene, rootName);
            var actual = root.GetComponentsInChildren<Transform>(true);
            var actualPaths = actual.Select(transform => RelativePath(root.transform, transform))
                .ToArray();
            Assert.That(actualPaths, Is.EqualTo(expected.Select(item => item.Path).ToArray()),
                rootName + " hierarchy and sibling order");

            var sourceRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(sourceRoot, Is.Not.Null, prefabPath);
            for (var index = 0; index < expected.Count; index++)
            {
                var spec = expected[index];
                var target = actual[index].gameObject;
                var context = rootName + (string.IsNullOrEmpty(spec.Path)
                    ? string.Empty
                    : "/" + spec.Path);
                Assert.That(target.activeSelf, Is.EqualTo(spec.ActiveSelf),
                    context + " activeSelf");
                AssertExactComponents(target, ExpectedComponents(spec.Kind), context);

                var source = string.IsNullOrEmpty(spec.Path)
                    ? sourceRoot.transform
                    : sourceRoot.transform.Find(spec.Path);
                Assert.That(source, Is.Not.Null, context + " source path");
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(target),
                    Is.SameAs(source.gameObject), context + " source identity");
                if (compareRootRect || index != 0)
                {
                    AssertRectTransformMatches(
                        target.GetComponent<RectTransform>(),
                        source.GetComponent<RectTransform>(),
                        context);
                }
            }

            Assert.That(root.GetComponentsInChildren<Canvas>(true), Is.Empty, rootName);
            Assert.That(root.GetComponentsInChildren<EventSystem>(true), Is.Empty, rootName);
            Assert.That(root.GetComponentsInChildren<MonoBehaviour>(true).Count(component =>
                    component != null
                    && component.GetType().FullName
                    == "UnityEngine.InputSystem.UI.InputSystemUIInputModule"),
                Is.Zero,
                rootName);
            Assert.That(root.GetComponentsInChildren<StandaloneInputModule>(true),
                Is.Empty,
                rootName);
        }

        private static void AssertDirectChildren(Transform parent, params string[] expectedNames)
        {
            var actual = Enumerable.Range(0, parent.childCount)
                .Select(index => parent.GetChild(index).name)
                .ToArray();
            Assert.That(actual, Is.EqualTo(expectedNames), HierarchyPath(parent));
        }

        private static void AssertExactComponents(
            GameObject target,
            params Type[] expectedTypes) =>
            AssertExactComponents(target, expectedTypes, HierarchyPath(target.transform));

        private static void AssertExactComponents(
            GameObject target,
            IReadOnlyList<Type> expectedTypes,
            string context)
        {
            var actual = target.GetComponents<Component>()
                .Select(component => component == null ? null : component.GetType())
                .ToArray();
            Assert.That(actual, Is.EqualTo(expectedTypes.ToArray()),
                context + " exact component order");
        }

        private static void AssertRectTransformMatches(
            RectTransform actual,
            RectTransform expected,
            string context)
        {
            Assert.That(actual, Is.Not.Null, context);
            Assert.That(expected, Is.Not.Null, context + " source");
            Assert.That(actual.anchorMin, Is.EqualTo(expected.anchorMin), context + ".anchorMin");
            Assert.That(actual.anchorMax, Is.EqualTo(expected.anchorMax), context + ".anchorMax");
            Assert.That(actual.pivot, Is.EqualTo(expected.pivot), context + ".pivot");
            Assert.That(actual.anchoredPosition3D, Is.EqualTo(expected.anchoredPosition3D),
                context + ".anchoredPosition3D");
            Assert.That(actual.sizeDelta, Is.EqualTo(expected.sizeDelta), context + ".sizeDelta");
            Assert.That(Quaternion.Angle(actual.localRotation, expected.localRotation),
                Is.LessThan(0.01f), context + ".localRotation");
            Assert.That(actual.localScale, Is.EqualTo(expected.localScale),
                context + ".localScale");
        }

        private static string RelativePath(Transform root, Transform target)
        {
            if (target == root) return string.Empty;
            var names = new Stack<string>();
            for (var current = target; current != null && current != root; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }

        private static UiNodeSpec[] Phase5DecorationButtonManifest() => new[]
        {
            new UiNodeSpec(string.Empty, true, UiNodeKind.Phase5Button),
            new UiNodeSpec("Top Highlight", true, UiNodeKind.Image),
            new UiNodeSpec("Label", true, UiNodeKind.Text)
        };

        private static UiNodeSpec[] CatalogueManifest() => new[]
        {
            new UiNodeSpec(string.Empty, true, UiNodeKind.CatalogueRoot),
            new UiNodeSpec("ExpandedSheet", true, UiNodeKind.Panel),
            new UiNodeSpec("ExpandedSheet/Title", true, UiNodeKind.Text),
            new UiNodeSpec("ExpandedSheet/CollapseButton", true, UiNodeKind.Button),
            new UiNodeSpec("ExpandedSheet/CollapseButton/Label", true, UiNodeKind.Text),
            new UiNodeSpec("ExpandedSheet/Content", true, UiNodeKind.Rect),
            new UiNodeSpec("ExpandedSheet/Content/TileTemplate", false, UiNodeKind.CatalogueTile),
            new UiNodeSpec("ExpandedSheet/Content/TileTemplate/Thumbnail", true, UiNodeKind.Image),
            new UiNodeSpec("ExpandedSheet/Content/TileTemplate/Name", true, UiNodeKind.Text),
            new UiNodeSpec("ExpandedSheet/Content/TileTemplate/Footprint", true, UiNodeKind.Text),
            new UiNodeSpec("ExpandedSheet/Content/TileTemplate/WarningShape", true, UiNodeKind.Image),
            new UiNodeSpec("ExpandedSheet/Content/TileTemplate/WarningLabel", true, UiNodeKind.Text),
            new UiNodeSpec("CollapsedHandle", false, UiNodeKind.Button),
            new UiNodeSpec("CollapsedHandle/Label", true, UiNodeKind.Text)
        };

        private static UiNodeSpec[] ActionBarManifest() => new[]
        {
            new UiNodeSpec(string.Empty, true, UiNodeKind.ActionBarRoot),
            new UiNodeSpec("ActionPanel", true, UiNodeKind.ActionLayout),
            new UiNodeSpec("ActionPanel/StoreButton", true, UiNodeKind.Button),
            new UiNodeSpec("ActionPanel/StoreButton/Label", true, UiNodeKind.Text),
            new UiNodeSpec("ActionPanel/StoreButton/Tooltip", false, UiNodeKind.Image),
            new UiNodeSpec("ActionPanel/StoreButton/Tooltip/Label", true, UiNodeKind.Text),
            new UiNodeSpec("ActionPanel/CancelButton", true, UiNodeKind.Button),
            new UiNodeSpec("ActionPanel/CancelButton/Label", true, UiNodeKind.Text),
            new UiNodeSpec("ActionPanel/CancelButton/Tooltip", false, UiNodeKind.Image),
            new UiNodeSpec("ActionPanel/CancelButton/Tooltip/Label", true, UiNodeKind.Text),
            new UiNodeSpec("ActionPanel/RotateButton", true, UiNodeKind.Button),
            new UiNodeSpec("ActionPanel/RotateButton/Label", true, UiNodeKind.Text),
            new UiNodeSpec("ActionPanel/RotateButton/Tooltip", false, UiNodeKind.Image),
            new UiNodeSpec("ActionPanel/RotateButton/Tooltip/Label", true, UiNodeKind.Text),
            new UiNodeSpec("ActionPanel/ConfirmButton", true, UiNodeKind.Button),
            new UiNodeSpec("ActionPanel/ConfirmButton/Label", true, UiNodeKind.Text),
            new UiNodeSpec("ActionPanel/ConfirmButton/Tooltip", false, UiNodeKind.Image),
            new UiNodeSpec("ActionPanel/ConfirmButton/Tooltip/Label", true, UiNodeKind.Text),
            new UiNodeSpec("FeedbackToast", false, UiNodeKind.Toast),
            new UiNodeSpec("FeedbackToast/StateShape", true, UiNodeKind.Image),
            new UiNodeSpec("FeedbackToast/Message", true, UiNodeKind.Text)
        };

        private static UiNodeSpec[] StoreModalManifest() => new[]
        {
            new UiNodeSpec(string.Empty, true, UiNodeKind.StoreModalRoot),
            new UiNodeSpec("ModalBlocker", true, UiNodeKind.Button),
            new UiNodeSpec("SafeArea", true, UiNodeKind.SafeArea),
            new UiNodeSpec("SafeArea/Content", true, UiNodeKind.StoreContent),
            new UiNodeSpec("SafeArea/Content/Title", true, UiNodeKind.Text),
            new UiNodeSpec("SafeArea/Content/Body", true, UiNodeKind.Text),
            new UiNodeSpec("SafeArea/Content/CancelButton", true, UiNodeKind.Button),
            new UiNodeSpec("SafeArea/Content/CancelButton/Label", true, UiNodeKind.Text),
            new UiNodeSpec("SafeArea/Content/StoreButton", true, UiNodeKind.Button),
            new UiNodeSpec("SafeArea/Content/StoreButton/Label", true, UiNodeKind.Text)
        };

        private static Type[] ExpectedComponents(UiNodeKind kind) => kind switch
        {
            UiNodeKind.Rect => new[] { typeof(RectTransform) },
            UiNodeKind.Image => new[]
                { typeof(RectTransform), typeof(CanvasRenderer), typeof(Image) },
            UiNodeKind.Text => new[]
                { typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI) },
            UiNodeKind.Button => new[]
            {
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button),
                typeof(DecorationPointerBoundaryEventHook)
            },
            UiNodeKind.Panel => new[]
            {
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(DecorationPointerBoundaryEventHook), typeof(AnimalCafePanelView)
            },
            UiNodeKind.SafeArea => new[]
                { typeof(RectTransform), typeof(SafeAreaContainer) },
            UiNodeKind.Phase5Button => new[]
            {
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Shadow),
                typeof(Button), typeof(AnimalCafeButtonView)
            },
            UiNodeKind.CatalogueRoot => new[]
            {
                typeof(RectTransform), typeof(SafeAreaContainer), typeof(CanvasGroup),
                typeof(DecorationCatalogueView)
            },
            UiNodeKind.CatalogueTile => new[]
            {
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button),
                typeof(DecorationPointerBoundaryEventHook), typeof(DecorationCatalogueTileView)
            },
            UiNodeKind.ActionBarRoot => new[]
            {
                typeof(RectTransform), typeof(SafeAreaContainer), typeof(CanvasGroup),
                typeof(DecorationActionBarView)
            },
            UiNodeKind.ActionLayout => new[]
            {
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(HorizontalLayoutGroup)
            },
            UiNodeKind.Toast => new[]
            {
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(CanvasGroup)
            },
            UiNodeKind.StoreModalRoot => new[]
            {
                typeof(RectTransform), typeof(CanvasGroup), typeof(AnimalCafeModalView),
                typeof(DecorationStoreModalView)
            },
            UiNodeKind.StoreContent => new[]
            {
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(AnimalCafePanelView)
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        private readonly struct UiNodeSpec
        {
            public readonly string Path;
            public readonly bool ActiveSelf;
            public readonly UiNodeKind Kind;

            public UiNodeSpec(string path, bool activeSelf, UiNodeKind kind)
            {
                Path = path;
                ActiveSelf = activeSelf;
                Kind = kind;
            }
        }

        private enum UiNodeKind
        {
            Rect,
            Image,
            Text,
            Button,
            Panel,
            SafeArea,
            Phase5Button,
            CatalogueRoot,
            CatalogueTile,
            ActionBarRoot,
            ActionLayout,
            Toast,
            StoreModalRoot,
            StoreContent
        }

        private static void AssertControllerBindings(DecorationModeController controller)
        {
            var serialized = new SerializedObject(controller);
            var required = new[]
            {
                "layoutRuntime", "contentCatalog", "catalogueAsset", "targetCamera",
                "cameraSettings", "cameraController", "sceneInteraction", "floorCollider",
                "gridRoot", "furnitureRepresentationRoot", "furniturePreviewRoot",
                "gridVisualRoot", "gridMaterialTemplate", "uiTheme", "sceneRegistry",
                "previewView", "gridView", "cameraDriver", "catalogueView",
                "actionBarView", "storeModalView", "decorationModeButton",
                "decorationModeButtonLabel", "gameTimeServiceBehaviour", "touchSourceBehaviour",
                "mouseSourceBehaviour"
            };
            foreach (var name in required)
            {
                var property = serialized.FindProperty(name);
                Assert.That(property, Is.Not.Null, name);
                Assert.That(property.objectReferenceValue, Is.Not.Null, name);
            }
        }

        private static void AssertPhase0Bindings(
            GameObject phase0,
            UnityEngine.Camera camera)
        {
            var settings = AssetDatabase.LoadAssetAtPath<CameraSettings>(
                "Assets/Config/DefaultCameraSettings.asset");
            var mouse = phase0.GetComponent<MouseCameraInput>();
            var cameraController = phase0.GetComponent<CafeCameraController>();
            var interaction = phase0.GetComponent<SceneInteractionController>();
            Assert.That(ReadObjectReference(mouse, "settings"), Is.SameAs(settings));
            Assert.That(ReadObjectReference(cameraController, "targetCamera"), Is.SameAs(camera));
            Assert.That(ReadObjectReference(cameraController, "settings"), Is.SameAs(settings));
            Assert.That(ReadObjectReference(cameraController, "inputSourceBehaviour"),
                Is.SameAs(mouse));
            Assert.That(ReadObjectReference(interaction, "targetCamera"), Is.SameAs(camera));
            Assert.That(ReadObjectReference(interaction, "inputSourceBehaviour"),
                Is.SameAs(mouse));
        }

        private static void AssertSharedDecorationBindings(Scene scene)
        {
            var owner = Find(scene, "Phase6_DecorationRuntime");
            var controller = owner.GetComponent<DecorationModeController>();
            var runtime = owner.GetComponent<CafeLayoutRuntime>();
            var fc = AssetDatabase.LoadAssetAtPath<FurnitureContentCatalog>(
                "Assets/Art/Phase6/Catalogues/FC_Phase6Production.asset");
            var dc = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                "Assets/Art/Phase6/Catalogues/DC_Phase6Decoration.asset");
            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(
                "Assets/UI/Phase5/Theme/AnimalCafeUiTheme.asset");
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Art/Phase4/Environment/Materials/M_Environment_Grid_01.mat");
            var settings = AssetDatabase.LoadAssetAtPath<CameraSettings>(
                "Assets/Config/DefaultCameraSettings.asset");
            Assert.That(ReadObjectReference(runtime, "contentCatalog"), Is.SameAs(fc));
            Assert.That(ReadObjectReference(runtime, "entrancePortal"),
                Is.SameAs(Find(scene, "P4_Entrance")
                    .GetComponent<EntrancePortalAuthoring>()));
            Assert.That(ReadObjectReference(controller, "contentCatalog"), Is.SameAs(fc));
            Assert.That(ReadObjectReference(controller, "catalogueAsset"), Is.SameAs(dc));
            Assert.That(ReadObjectReference(controller, "cameraSettings"), Is.SameAs(settings));
            Assert.That(ReadObjectReference(controller, "layoutRuntime"), Is.SameAs(runtime));
            Assert.That(ReadObjectReference(controller, "targetCamera"),
                Is.SameAs(Find(scene, "Main Camera").GetComponent<UnityEngine.Camera>()));
            Assert.That(ReadObjectReference(controller, "cameraController"),
                Is.SameAs(Find(scene, "Phase0_Runtime").GetComponent<CafeCameraController>()));
            Assert.That(ReadObjectReference(controller, "sceneInteraction"),
                Is.SameAs(Find(scene, "Phase0_Runtime")
                    .GetComponent<SceneInteractionController>()));
            Assert.That(ReadObjectReference(controller, "floorCollider"),
                Is.SameAs(Find(scene, "P4_Floor_8x8").GetComponentInChildren<Collider>(true)));
            Assert.That(ReadObjectReference(controller, "gridMaterialTemplate"),
                Is.SameAs(material));
            Assert.That(ReadObjectReference(controller, "uiTheme"), Is.SameAs(theme));
            Assert.That(ReadObjectReference(controller, "gridRoot"),
                Is.SameAs(Find(scene, "DecorationSpaceRoot").transform));
            Assert.That(ReadObjectReference(controller, "furnitureRepresentationRoot"),
                Is.SameAs(Find(scene, "FurnitureRepresentationRoot").transform));
            Assert.That(ReadObjectReference(controller, "furniturePreviewRoot"),
                Is.SameAs(Find(scene, "FurniturePreviewRoot").transform));
            Assert.That(ReadObjectReference(controller, "gridVisualRoot"),
                Is.SameAs(Find(scene, "GridVisualRoot").transform));
            Assert.That(ReadObjectReference(controller, "sceneRegistry"),
                Is.SameAs(owner.GetComponent<FurnitureSceneRegistry>()));
            Assert.That(ReadObjectReference(controller, "previewView"),
                Is.SameAs(owner.GetComponent<FurniturePreviewView>()));
            Assert.That(ReadObjectReference(controller, "gridView"),
                Is.SameAs(owner.GetComponent<GridHighlightView>()));
            Assert.That(ReadObjectReference(controller, "cameraDriver"),
                Is.SameAs(owner.GetComponent<DecorationCameraDriver>()));
            Assert.That(ReadObjectReference(controller, "catalogueView"),
                Is.SameAs(Find(scene, "PF_UI_DecorationCatalogue")
                    .GetComponent<DecorationCatalogueView>()));
            Assert.That(ReadObjectReference(controller, "actionBarView"),
                Is.SameAs(Find(scene, "PF_UI_DecorationActionBar")
                    .GetComponent<DecorationActionBarView>()));
            Assert.That(ReadObjectReference(controller, "storeModalView"),
                Is.SameAs(Find(scene, "PF_UI_DecorationStoreModal")
                    .GetComponent<DecorationStoreModalView>()));
            var toggle = Find(scene, "DecorationModeButton");
            Assert.That(ReadObjectReference(controller, "decorationModeButton"),
                Is.SameAs(toggle.GetComponent<Button>()));
            Assert.That(ReadObjectReference(controller, "decorationModeButtonLabel"),
                Is.SameAs(toggle.GetComponentInChildren<TMP_Text>(true)));
            Assert.That(ReadObjectReference(controller, "gameTimeServiceBehaviour"),
                Is.SameAs(Find(scene, "Phase0_Runtime").GetComponent<GameTimeService>()));
            Assert.That(ReadObjectReference(controller, "touchSourceBehaviour"),
                Is.SameAs(owner.GetComponent<InputSystemDecorationTouchSource>()));
            Assert.That(ReadObjectReference(controller, "mouseSourceBehaviour"),
                Is.SameAs(owner.GetComponent<MouseDecorationInputSource>()));
            Assert.That(Find(scene, "FurnitureRepresentationRoot").transform.childCount,
                Is.Zero);
        }

        private static UnityEngine.Object ReadObjectReference(
            UnityEngine.Object owner,
            string fieldName)
        {
            var serialized = new SerializedObject(owner);
            var property = serialized.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null, owner.GetType().Name + "." + fieldName);
            return property.objectReferenceValue;
        }

        private static void AssertPrefabSource(
            Scene scene,
            string name,
            string expectedPath)
        {
            var gameObject = Find(scene, name);
            var source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
            Assert.That(source, Is.Not.Null, name + " must remain a Prefab instance.");
            var actualPath = AssetDatabase.GetAssetPath(source);
            Assert.That(actualPath, Is.EqualTo(expectedPath));
            Assert.That(AssetDatabase.AssetPathToGUID(actualPath),
                Is.EqualTo(ExpectedPrefabGuid(expectedPath)));
        }

        private static string ExpectedPrefabGuid(string path) => path switch
        {
            "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Floor_8x8.prefab" =>
                "ae71a0726a504f24b8d97d7e1f4b15fd",
            "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Wall_BackLeft_8x3.prefab" =>
                "e9324ba340ec5634591234b9c38befd0",
            "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Wall_BackRight_8x3.prefab" =>
                "3b0e2d354fbc57e4eb64d7c9c48c63ca",
            "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Entrance_2x2.prefab" =>
                "c99128042b5e8c04b837af3f4d42ae5c",
            "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Window_01.prefab" =>
                "f5a18fb1ec2e47c4cb018a16ca3a97b9",
            "Assets/UI/Phase5/Prefabs/PF_UI_Root.prefab" =>
                "f2fb88287e92d864997d99874d6dfdaa",
            "Assets/UI/Phase5/Prefabs/PF_UI_SafeArea.prefab" =>
                "f60e1cacdc594b84e98eab28d3070167",
            "Assets/UI/Phase5/Prefabs/PF_UI_Button_Secondary_Default.prefab" =>
                "9c746f33a5758cf41bad68f12aedbeff",
            "Assets/UI/Phase6/Prefabs/PF_UI_DecorationCatalogue.prefab" =>
                "7715fa9914e03e3448dfdaca77a9004b",
            "Assets/UI/Phase6/Prefabs/PF_UI_DecorationActionBar.prefab" =>
                "c107df1e47be10744ad4ccc31fcee90f",
            "Assets/UI/Phase6/Prefabs/PF_UI_DecorationStoreModal.prefab" =>
                "a6d341f22a31ecf4089ed87449eb0234",
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, null)
        };

        private static void AssertReferenceOnly(
            Transform reference,
            Vector3 localPosition,
            string text,
            Transform camera)
        {
            Assert.That(reference, Is.Not.Null);
            Assert.That(reference.localPosition, Is.EqualTo(localPosition));
            Assert.That(reference.localScale, Is.EqualTo(Vector3.one));
            Assert.That(reference.childCount, Is.Zero);
            var toCamera = (camera.position - reference.position).normalized;
            Assert.That(Vector3.Dot(-reference.forward, toCamera), Is.GreaterThan(0.999f));
            var label = reference.GetComponent<TextMeshPro>();
            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(
                "Assets/UI/Phase5/Theme/AnimalCafeUiTheme.asset");
            Assert.That(label.text, Is.EqualTo(text));
            Assert.That(label.font, Is.SameAs(theme.Typography.Label.FontAsset));
            Assert.That(label.fontSharedMaterial,
                Is.SameAs(theme.Typography.Label.FontAsset.material));
            Assert.That(label.alignment, Is.EqualTo(TextAlignmentOptions.BottomLeft));
            Assert.That(label.fontSize, Is.EqualTo(1.5f));
            var rect = reference.GetComponent<RectTransform>();
            Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(4f, 0.6f)));
            var expectedPivot = localPosition.z > 0f
                ? new Vector2(0.75f, 0f)
                : new Vector2(1f, 0f);
            Assert.That(rect.pivot, Is.EqualTo(expectedPivot));
            Assert.That(reference.GetComponents<Component>().Select(item => item.GetType()),
                Is.EquivalentTo(new[]
                {
                    typeof(RectTransform), typeof(TextMeshPro), typeof(MeshRenderer),
                    typeof(MeshFilter)
                }));
            Assert.That(reference.GetComponent<Collider>(), Is.Null);
        }

        private static GameObject Find(Scene scene, string name) =>
            FindAll(scene, name).Single();

        private static GameObject[] FindAll(Scene scene, string name) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name == name)
                .Select(transform => transform.gameObject)
                .ToArray();

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

        private static void ClearSeams()
        {
            Phase6DecorationSceneSetup.DependencyResolverOverrideForTests = null;
            Phase6DecorationSceneSetup.Phase4ValidatorOverrideForTests = null;
            Phase6DecorationSceneSetup.Phase5ValidatorOverrideForTests = null;
            Phase6DecorationSceneSetup.DecorationCatalogueValidatorOverrideForTests = null;
            Phase6DecorationSceneSetup.FaultInjectorForTests = null;
            Phase6DecorationSceneSetup.RestoreFaultInjectorForTests = null;
            Phase6DecorationSceneSetup.SaveSceneObserverForTests = null;
        }

        private static DirtyCallerFixture CreateDirtyCallerFixture(
            FullEditorStateScope scope,
            Phase6SceneSetupTarget target,
            Scene targetScene)
        {
            var asset = ScriptableObject.CreateInstance<CameraSettings>();
            var assetPath = "Assets/Tests/EditMode/Phase6/__Task8DirtyCaller_"
                + Guid.NewGuid().ToString("N") + ".asset";
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath), Is.Null);
            AssetDatabase.CreateAsset(asset, assetPath);
            scope.RegisterOwnedAsset(assetPath, asset);
            var savedAssetHash = Hash(assetPath);
            asset.PanSpeed = 37f;
            EditorUtility.SetDirty(asset);

            var unrelated = OpenUnrelatedFixtureForTest();
            var unrelatedObject = new GameObject("Task8_Unrelated_Dirty_Object");
            SceneManager.MoveGameObjectToScene(unrelatedObject, unrelated);
            unrelatedObject.transform.position = new Vector3(1.25f, 2.5f, 3.75f);
            EditorSceneManager.MarkSceneDirty(unrelated);
            var targetComponent = Find(targetScene, "Main Camera")
                .GetComponent<UnityEngine.Camera>();
            var targetId = GlobalObjectId.GetGlobalObjectIdSlow(targetComponent);
            var targetSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(PathFor(target));
            Assert.That(targetSceneAsset, Is.Not.Null);
            var targetSceneAssetId = GlobalObjectId.GetGlobalObjectIdSlow(targetSceneAsset);
            Selection.activeObject = targetSceneAsset;
            Selection.objects = new UnityEngine.Object[]
            {
                targetComponent,
                targetSceneAsset,
                asset,
                unrelatedObject
            };
            return new DirtyCallerFixture(
                asset,
                assetPath,
                savedAssetHash,
                unrelated,
                unrelatedObject,
                CaptureSceneObjectFingerprint(unrelated),
                targetId,
                targetSceneAssetId,
                Array.IndexOf(Selection.objects, Selection.activeObject));
        }

        private static FirstPublishCallerFixture CreateFirstPublishCallerFixture(
            FullEditorStateScope scope)
        {
            var asset = ScriptableObject.CreateInstance<CameraSettings>();
            var assetPath = "Assets/Tests/EditMode/Phase6/__Task8FirstPublishCaller_"
                + Guid.NewGuid().ToString("N") + ".asset";
            Assert.That(File.Exists(assetPath), Is.False);
            Assert.That(File.Exists(assetPath + ".meta"), Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath), Is.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(
                assetPath,
                AssetPathToGUIDOptions.OnlyExistingAssets), Is.Empty);
            AssetDatabase.CreateAsset(asset, assetPath);
            scope.RegisterOwnedAsset(assetPath, asset);
            var savedAssetHash = Hash(assetPath);
            var savedAssetMetaHash = Hash(assetPath + ".meta");
            asset.PanSpeed = 91f;
            EditorUtility.SetDirty(asset);

            var unrelated = OpenUnrelatedFixtureForTest();
            var unrelatedObject = new GameObject("Task8_FirstPublish_Dirty_Object");
            SceneManager.MoveGameObjectToScene(unrelatedObject, unrelated);
            unrelatedObject.transform.position = new Vector3(-2.25f, 4.5f, 6.75f);
            var light = unrelatedObject.AddComponent<Light>();
            light.intensity = 4.75f;
            EditorSceneManager.MarkSceneDirty(unrelated);
            Assert.That(SceneManager.SetActiveScene(unrelated), Is.True);

            var selection = new UnityEngine.Object[] { asset, unrelatedObject, light };
            Selection.activeObject = light;
            Selection.objects = selection;
            Assert.That(unrelated.isDirty, Is.True);
            Assert.That(EditorUtility.IsDirty(asset), Is.True);

            return new FirstPublishCallerFixture(
                asset,
                assetPath,
                savedAssetHash,
                savedAssetMetaHash,
                unrelated,
                unrelatedObject,
                light,
                CaptureSceneObjectFingerprint(unrelated),
                SceneOrder(),
                SceneHandles(),
                selection,
                Array.IndexOf(Selection.objects, Selection.activeObject));
        }

        private sealed class FirstPublishCallerFixture
        {
            private readonly CameraSettings asset;
            private readonly string assetPath;
            private readonly string savedAssetHash;
            private readonly string savedAssetMetaHash;
            private readonly Scene unrelatedScene;
            private readonly GameObject unrelatedObject;
            private readonly Light light;
            private readonly string sceneFingerprint;
            private readonly string[] sceneOrder;
            private readonly ulong[] sceneHandles;
            private readonly UnityEngine.Object[] selection;
            private readonly int activeSelectionIndex;

            public FirstPublishCallerFixture(
                CameraSettings asset,
                string assetPath,
                string savedAssetHash,
                string savedAssetMetaHash,
                Scene unrelatedScene,
                GameObject unrelatedObject,
                Light light,
                string sceneFingerprint,
                string[] sceneOrder,
                ulong[] sceneHandles,
                UnityEngine.Object[] selection,
                int activeSelectionIndex)
            {
                this.asset = asset;
                this.assetPath = assetPath;
                this.savedAssetHash = savedAssetHash;
                this.savedAssetMetaHash = savedAssetMetaHash;
                this.unrelatedScene = unrelatedScene;
                this.unrelatedObject = unrelatedObject;
                this.light = light;
                this.sceneFingerprint = sceneFingerprint;
                this.sceneOrder = sceneOrder;
                this.sceneHandles = sceneHandles;
                this.selection = selection;
                this.activeSelectionIndex = activeSelectionIndex;
            }

            public void AssertPreserved()
            {
                Assert.That(unrelatedScene.IsValid() && unrelatedScene.isLoaded, Is.True);
                Assert.That(unrelatedScene.isDirty, Is.True);
                Assert.That(SceneOrder(), Is.EqualTo(sceneOrder));
                Assert.That(SceneHandles(), Is.EqualTo(sceneHandles));
                Assert.That(SceneManager.GetActiveScene().handle,
                    Is.EqualTo(unrelatedScene.handle));
                Assert.That(CaptureSceneObjectFingerprint(unrelatedScene),
                    Is.EqualTo(sceneFingerprint));
                Assert.That(unrelatedObject.transform.position,
                    Is.EqualTo(new Vector3(-2.25f, 4.5f, 6.75f)));
                Assert.That(light.intensity, Is.EqualTo(4.75f));

                Assert.That(asset.PanSpeed, Is.EqualTo(91f));
                Assert.That(EditorUtility.IsDirty(asset), Is.True);
                Assert.That(AssetDatabase.GetAssetPath(asset), Is.EqualTo(assetPath));
                Assert.That(Hash(assetPath), Is.EqualTo(savedAssetHash));
                Assert.That(Hash(assetPath + ".meta"), Is.EqualTo(savedAssetMetaHash));

                Assert.That(Selection.objects, Has.Length.EqualTo(selection.Length));
                for (var index = 0; index < selection.Length; index++)
                    Assert.That(Selection.objects[index], Is.SameAs(selection[index]), index.ToString());
                Assert.That(Array.IndexOf(Selection.objects, Selection.activeObject),
                    Is.EqualTo(activeSelectionIndex));
            }
        }

        private sealed class DirtyCallerFixture
        {
            private readonly CameraSettings asset;
            private readonly string assetPath;
            private readonly string savedAssetHash;
            private readonly Scene unrelatedScene;
            private readonly GameObject unrelatedObject;
            private readonly string sceneFingerprint;
            private readonly GlobalObjectId targetId;
            private readonly GlobalObjectId targetSceneAssetId;
            private readonly int activeSelectionIndex;

            public DirtyCallerFixture(
                CameraSettings asset,
                string assetPath,
                string savedAssetHash,
                Scene unrelatedScene,
                GameObject unrelatedObject,
                string sceneFingerprint,
                GlobalObjectId targetId,
                GlobalObjectId targetSceneAssetId,
                int activeSelectionIndex)
            {
                this.asset = asset;
                this.assetPath = assetPath;
                this.savedAssetHash = savedAssetHash;
                this.unrelatedScene = unrelatedScene;
                this.unrelatedObject = unrelatedObject;
                this.sceneFingerprint = sceneFingerprint;
                this.targetId = targetId;
                this.targetSceneAssetId = targetSceneAssetId;
                this.activeSelectionIndex = activeSelectionIndex;
            }

            public void AssertPreserved()
            {
                Assert.That(unrelatedScene.IsValid() && unrelatedScene.isLoaded, Is.True);
                Assert.That(unrelatedScene.isDirty, Is.True);
                Assert.That(CaptureSceneObjectFingerprint(unrelatedScene),
                    Is.EqualTo(sceneFingerprint));
                Assert.That(asset.PanSpeed, Is.EqualTo(37f));
                Assert.That(EditorUtility.IsDirty(asset), Is.True);
                Assert.That(Hash(assetPath), Is.EqualTo(savedAssetHash));
                Assert.That(Selection.objects, Has.Length.EqualTo(4));
                Assert.That(GlobalObjectId.GetGlobalObjectIdSlow(Selection.objects[0]),
                    Is.EqualTo(targetId));
                Assert.That(GlobalObjectId.GetGlobalObjectIdSlow(Selection.objects[1]),
                    Is.EqualTo(targetSceneAssetId));
                Assert.That(Selection.objects[2], Is.SameAs(asset));
                Assert.That(Selection.objects[3], Is.SameAs(unrelatedObject));
                Assert.That(Array.IndexOf(Selection.objects, Selection.activeObject),
                    Is.EqualTo(activeSelectionIndex));
            }
        }

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

        private static void RemoveExactTarget(string path)
        {
            var retainedSelection = Selection.objects.Where(value =>
            {
                var scene = SceneFor(value);
                return !scene.IsValid()
                    || !string.Equals(scene.path, path, StringComparison.Ordinal);
            }).ToArray();
            Selection.objects = retainedSelection;
            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (string.Equals(scene.path, path, StringComparison.Ordinal))
                    EditorSceneManager.CloseScene(scene, true);
            }

            var wasImported = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null
                || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path));
            if (wasImported)
                AssetDatabase.DeleteAsset(path);
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path), Is.Null);
            Assert.That(AssetDatabase.AssetPathToGUID(
                path,
                AssetPathToGUIDOptions.OnlyExistingAssets), Is.Empty);
            Assert.That(File.Exists(path), Is.False);
            Assert.That(File.Exists(path + ".meta"), Is.False);
        }

        private static Scene SceneFor(UnityEngine.Object value)
        {
            if (value is GameObject gameObject) return gameObject.scene;
            if (value is Component component) return component.gameObject.scene;
            return default;
        }

        private sealed class FullEditorStateScope : IDisposable
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

            public FullEditorStateScope()
            {
                var callerDirty = Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt)
                    .FirstOrDefault(scene => scene.isDirty);
                if (callerDirty.IsValid())
                {
                    Assert.Ignore(
                        "Task 8 mutation tests will not run while the caller has a dirty Scene; "
                        + "save or close it first. Test-owned dirty Scenes are created after this gate.");
                }

                var callerTarget = Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt)
                    .FirstOrDefault(scene => IsTargetPath(scene.path));
                if (callerTarget.IsValid())
                {
                    Assert.Ignore(
                        "Task 8 mutation tests require both target Scenes to be closed by the caller; "
                        + "the harness will never close or reload a caller-owned target Scene.");
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
                var guid = string.IsNullOrEmpty(path)
                    ? string.Empty
                    : AssetDatabase.AssetPathToGUID(path);
                if (string.IsNullOrEmpty(path)
                    || string.IsNullOrEmpty(guid)
                    || asset == null
                    || !ReferenceEquals(
                        AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path),
                        asset))
                {
                    throw new InvalidOperationException(
                        "Only an asset successfully created by this test scope may be registered.");
                }

                ownedAssets.Add(new OwnedAsset(path, guid));
            }

            public void Dispose()
            {
                try
                {
                    ClearSeams();
                }
                finally
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
                                }
                                finally
                                {
                                    CleanupOwnedAssets();

                                    Assert.That(
                                        CaptureUnrelatedFingerprint(scenes),
                                        Is.EqualTo(unrelatedFingerprint),
                                        "Task 8 test cleanup changed caller-owned Scene order, dirtiness, "
                                        + "active Scene, or in-memory objects.");
                                }
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

        private static void CloseTestOwnedScenes(CapturedScene[] captured)
        {
            var capturedHandles = captured.Select(entry => entry.Handle).ToArray();
            for (var index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                var scene = SceneManager.GetSceneAt(index);
                var isTestOwned = !capturedHandles.Contains(scene.handle.GetRawData());
                if (!isTestOwned) continue;

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
            anchor.name = "Task8_TestCleanupAnchor";
            if (sceneBeingClosed == SceneManager.GetActiveScene())
                SceneManager.SetActiveScene(anchor);
        }

        private static void RestoreTarget(
            string path,
            FileState sceneFile,
            FileState metaFile)
        {
            var recoveryRoot = Path.Combine(
                "Library/AnimalCafe/Phase6MigrationTestRestore",
                Guid.NewGuid().ToString("N"));
            var recoveryScene = Path.Combine(recoveryRoot, "target.unity");
            var recoveryMeta = Path.Combine(recoveryRoot, "target.meta");
            Directory.CreateDirectory(recoveryRoot);
            sceneFile.Stage(recoveryScene);
            metaFile.Stage(recoveryMeta);
            var restored = false;
            try
            {
                if (File.Exists(path)
                    || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null)
                {
                    Assert.That(AssetDatabase.DeleteAsset(path), Is.True,
                        "Target Scene must be released through AssetDatabase before restoring its bytes.");
                }
                else if (File.Exists(path + ".meta"))
                {
                    File.Delete(path + ".meta");
                }

                sceneFile.RestoreFrom(recoveryScene, path);
                metaFile.RestoreFrom(recoveryMeta, path + ".meta");
                if (sceneFile.Existed)
                    ImportIfPresent(path);
                else
                {
                    Assert.That(File.Exists(path), Is.False);
                    Assert.That(File.Exists(path + ".meta"), Is.EqualTo(metaFile.Existed));
                    Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path), Is.Null);
                }
                sceneFile.AssertMatches(path);
                metaFile.AssertMatches(path + ".meta");
                restored = true;
            }
            finally
            {
                if (restored && Directory.Exists(recoveryRoot))
                {
                    Directory.Delete(recoveryRoot, true);
                    var recoveryParent = Path.GetDirectoryName(recoveryRoot);
                    if (Directory.Exists(recoveryParent)
                        && !Directory.EnumerateFileSystemEntries(recoveryParent).Any())
                    {
                        Directory.Delete(recoveryParent);
                    }
                }
            }

            foreach (var anchor in Enumerable.Range(0, SceneManager.sceneCount)
                         .Select(SceneManager.GetSceneAt)
                         .Where(scene => scene.name == "Task8_TestCleanupAnchor")
                         .ToArray())
            {
                if (SceneManager.sceneCount > 1)
                    EditorSceneManager.CloseScene(anchor, true);
            }
        }

        private static void RestoreActiveScene(CapturedScene[] captured)
        {
            var active = captured.FirstOrDefault(entry => entry.IsActive);
            if (active.Handle != 0)
            {
                var retained = Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt)
                    .FirstOrDefault(scene => scene.handle.GetRawData() == active.Handle);
                if (retained.IsValid())
                {
                    SceneManager.SetActiveScene(retained);
                    return;
                }

            }
        }

        private static bool IsTargetPath(string path) =>
            string.Equals(path, MainCafePath, StringComparison.Ordinal)
            || string.Equals(path, ValidationPath, StringComparison.Ordinal);

        private static string CaptureUnrelatedFingerprint(CapturedScene[] captured)
        {
            var capturedHandles = captured.Select(entry => entry.Handle).ToArray();
            var activeHandle = SceneManager.GetActiveScene().handle.GetRawData();
            var current = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(scene => capturedHandles.Contains(scene.handle.GetRawData()))
                .Select((scene, index) =>
                    $"{index}|{scene.handle.GetRawData()}|{scene.path}|{scene.name}|{scene.isLoaded}|"
                    + $"{scene.isDirty}|active={scene.handle.GetRawData() == activeHandle}|"
                    + string.Join(";", scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                        .Select(transform =>
                            $"{EntityId.ToULong(transform.GetEntityId())}:{transform.name}:"
                            + $"{transform.localPosition}:{transform.localRotation}:"
                            + $"{transform.localScale}:{transform.gameObject.activeSelf}:"
                            + string.Join(",", transform.GetComponents<Component>()
                                .Select(component => component == null
                                    ? "<missing>"
                                    : component.GetType().FullName)))));
            return string.Join("\n", current);
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
                var isTargetValue = scene.IsValid() && IsTargetPath(scene.path)
                    || !ReferenceEquals(value, null)
                    && IsTargetPath(AssetDatabase.GetAssetPath(value));
                return new SelectionEntry(
                    value,
                    isTargetValue ? GlobalObjectId.GetGlobalObjectIdSlow(value) : default,
                    isTargetValue);
            }

            public UnityEngine.Object Resolve() => useGlobal
                ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId)
                : direct;
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

            public void Stage(string recoveryPath)
            {
                if (existed)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(recoveryPath));
                    File.WriteAllBytes(recoveryPath, bytes);
                }
            }

            public void RestoreFrom(string recoveryPath, string destinationPath)
            {
                if (existed)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                    File.Move(recoveryPath, destinationPath);
                }
                else if (File.Exists(destinationPath))
                    File.Delete(destinationPath);
            }

            public void AssertMatches(string actualPath)
            {
                Assert.That(File.Exists(actualPath), Is.EqualTo(existed), actualPath);
                if (existed)
                    Assert.That(File.ReadAllBytes(actualPath), Is.EqualTo(bytes), actualPath);
            }
        }

        private static void ImportIfPresent(string path)
        {
            if (File.Exists(path))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        }

        private readonly struct TransactionSnapshot : IEquatable<TransactionSnapshot>
        {
            private readonly string files;
            private readonly string caller;

            private TransactionSnapshot(string files, string caller)
            {
                this.files = files;
                this.caller = caller;
            }

            public static TransactionSnapshot Capture(Phase6SceneSetupTarget target) =>
                new TransactionSnapshot(
                    Hash(PathFor(target)) + "|" + Hash(PathFor(target) + ".meta"),
                    FullStateFingerprint.Capture().CallerOnly);

            public bool Equals(TransactionSnapshot other) =>
                files == other.files && caller == other.caller;
            public override bool Equals(object obj) =>
                obj is TransactionSnapshot other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(files, caller);
        }

        private readonly struct FullStateFingerprint : IEquatable<FullStateFingerprint>
        {
            private readonly string value;
            public string CallerOnly { get; }

            private FullStateFingerprint(string value, string callerOnly)
            {
                this.value = value;
                CallerOnly = callerOnly;
            }

            public static FullStateFingerprint Capture()
            {
                var caller = string.Join("\n", SceneOrder())
                    + "\nBuild=" + string.Join(";", EditorBuildSettings.scenes
                        .Select((entry, index) => $"{index}:{entry.path}:{entry.enabled}"))
                    + "\nSelection=" + string.Join(";", Selection.objects
                        .Select((value, index) =>
                            $"{index}:{GlobalObjectId.GetGlobalObjectIdSlow(value)}:{value?.name}"))
                    + "\nActive=" + Array.IndexOf(Selection.objects, Selection.activeObject);
                var all = caller + "\nFiles="
                    + Hash(MainCafePath) + "|" + Hash(MainCafePath + ".meta") + "|"
                    + Hash(ValidationPath) + "|" + Hash(ValidationPath + ".meta");
                return new FullStateFingerprint(all, caller);
            }

            public bool Equals(FullStateFingerprint other) => value == other.value;
            public override bool Equals(object obj) =>
                obj is FullStateFingerprint other && Equals(other);
            public override int GetHashCode() => value?.GetHashCode() ?? 0;
        }

        private readonly struct CanonicalTargetSnapshot : IEquatable<CanonicalTargetSnapshot>
        {
            private readonly string value;
            private CanonicalTargetSnapshot(string value) => this.value = value;

            public static CanonicalTargetSnapshot Capture(Phase6SceneSetupTarget target)
            {
                var scene = OpenTargetForTest(target, out var openedByTest);
                try
                {
                    var objects = scene.GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                        .OrderBy(HierarchyPath, StringComparer.Ordinal)
                        .Select(transform =>
                        {
                            var go = transform.gameObject;
                            var source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                            var sourceGuid = string.Empty;
                            long sourceLocalId = 0;
                            if (source != null)
                            {
                                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                                    source,
                                    out sourceGuid,
                                    out sourceLocalId);
                            }
                            var id = GlobalObjectId.GetGlobalObjectIdSlow(go);
                            return $"{HierarchyPath(transform)}|{id}|{sourceGuid}|{sourceLocalId}|"
                                + $"{go.GetComponents<Component>().Length}|{transform.childCount}";
                        });
                    var serializedRefs = FindAll<DecorationModeController>(scene)
                        .SelectMany(SerializedReferences)
                        .OrderBy(item => item, StringComparer.Ordinal);
                    return new CanonicalTargetSnapshot(
                        Hash(PathFor(target)) + "|" + Hash(PathFor(target) + ".meta")
                        + "\n" + string.Join("\n", objects)
                        + "\nRefs=" + string.Join(";", serializedRefs));
                }
                finally
                {
                    if (openedByTest)
                        EditorSceneManager.CloseScene(scene, true);
                }
            }

            public bool Equals(CanonicalTargetSnapshot other) => value == other.value;
            public override bool Equals(object obj) =>
                obj is CanonicalTargetSnapshot other && Equals(other);
            public override int GetHashCode() => value?.GetHashCode() ?? 0;
        }

        private static IEnumerable<string> SerializedReferences(DecorationModeController controller)
        {
            var serialized = new SerializedObject(controller);
            var iterator = serialized.GetIterator();
            if (!iterator.NextVisible(true)) yield break;
            do
            {
                if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                {
                    var value = iterator.objectReferenceValue;
                    yield return iterator.propertyPath + "="
                        + (value == null ? "<null>" : GlobalObjectId.GetGlobalObjectIdSlow(value).ToString());
                }
            }
            while (iterator.NextVisible(false));
        }

        private static string HierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }

        private static string Hash(string path)
        {
            if (!File.Exists(path)) return "<absent>";
            using var sha = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path)))
                .Replace("-", string.Empty);
        }

        private sealed class Task8InjectedFaultException : Exception
        {
            public Task8InjectedFaultException(string stage) : base(stage) { }
        }

    }
}

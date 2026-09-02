using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.Camera;
using AnimalCafe.Content;
using AnimalCafe.Core.Time;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.EditorTools.Phase4;
using AnimalCafe.EditorTools.Phase5;
using AnimalCafe.Input;
using AnimalCafe.Interaction;
using AnimalCafe.UI;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Foundation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.Phase6
{
    internal enum Phase6SceneSetupTarget
    {
        MainCafe,
        Validation
    }

    internal enum Phase6SceneSetupStage
    {
        BeforeMutation,
        BeforeSave,
        AfterSave
    }

    internal enum Phase6SceneRestoreStage
    {
        BeforeStagingCopy,
        AfterAssetRelease,
        BeforeImport
    }

    internal enum Phase6SceneSetupDependency
    {
        ContentCatalog,
        EnvironmentPrefab,
        UiPrefab,
        Theme,
        GridMaterial,
        CameraSettings,
        InputActions
    }

    internal sealed class Phase6SceneSetupDependencySnapshot
    {
        internal Phase6SceneSetupDependencySnapshot(
            Phase6SceneSetupTarget target,
            FurnitureContentCatalog contentCatalog,
            DecorationCatalogueAsset decorationCatalogue,
            AnimalCafeUiTheme theme,
            Material gridMaterial,
            CameraSettings cameraSettings,
            UnityEngine.Object inputActions,
            GameObject floorPrefab,
            GameObject backLeftPrefab,
            GameObject backRightPrefab,
            GameObject entrancePrefab,
            GameObject windowPrefab,
            GameObject uiRootPrefab,
            GameObject safeAreaPrefab,
            GameObject buttonPrefab,
            GameObject cataloguePrefab,
            GameObject actionBarPrefab,
            GameObject storeModalPrefab)
        {
            Target = target;
            ContentCatalog = contentCatalog;
            DecorationCatalogue = decorationCatalogue;
            Theme = theme;
            GridMaterial = gridMaterial;
            CameraSettings = cameraSettings;
            InputActions = inputActions;
            FloorPrefab = floorPrefab;
            BackLeftPrefab = backLeftPrefab;
            BackRightPrefab = backRightPrefab;
            EntrancePrefab = entrancePrefab;
            WindowPrefab = windowPrefab;
            UiRootPrefab = uiRootPrefab;
            SafeAreaPrefab = safeAreaPrefab;
            ButtonPrefab = buttonPrefab;
            CataloguePrefab = cataloguePrefab;
            ActionBarPrefab = actionBarPrefab;
            StoreModalPrefab = storeModalPrefab;
        }

        internal Phase6SceneSetupTarget Target { get; }
        internal FurnitureContentCatalog ContentCatalog { get; }
        internal DecorationCatalogueAsset DecorationCatalogue { get; }
        internal AnimalCafeUiTheme Theme { get; }
        internal Material GridMaterial { get; }
        internal CameraSettings CameraSettings { get; }
        internal UnityEngine.Object InputActions { get; }
        internal GameObject FloorPrefab { get; }
        internal GameObject BackLeftPrefab { get; }
        internal GameObject BackRightPrefab { get; }
        internal GameObject EntrancePrefab { get; }
        internal GameObject WindowPrefab { get; }
        internal GameObject UiRootPrefab { get; }
        internal GameObject SafeAreaPrefab { get; }
        internal GameObject ButtonPrefab { get; }
        internal GameObject CataloguePrefab { get; }
        internal GameObject ActionBarPrefab { get; }
        internal GameObject StoreModalPrefab { get; }
    }

    /// <summary>
    /// Transactional authoring entry points for the Phase 6 production and validation Scenes.
    /// </summary>
    public static class Phase6DecorationSceneSetup
    {
        private const string Phase7CataloguePrefabPath="Assets/UI/Phase7/Prefabs/PF_UI_Phase7DecorationCatalogue.prefab";
        private const string Phase7ActionBarPrefabPath="Assets/UI/Phase7/Prefabs/PF_UI_Phase7DecorationActionBar.prefab";
        private const string FloorPrefabPath =
            "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Floor_8x8.prefab";
        private const string BackLeftPrefabPath =
            "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Wall_BackLeft_8x3.prefab";
        private const string BackRightPrefabPath =
            "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Wall_BackRight_8x3.prefab";
        private const string EntrancePrefabPath =
            "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Entrance_2x2.prefab";
        private const string WindowPrefabPath =
            "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Window_01.prefab";
        private const string UiRootPrefabPath =
            "Assets/UI/Phase5/Prefabs/PF_UI_Root.prefab";
        private const string SafeAreaPrefabPath =
            "Assets/UI/Phase5/Prefabs/PF_UI_SafeArea.prefab";
        private const string ButtonPrefabPath =
            "Assets/UI/Phase5/Prefabs/PF_UI_Button_Secondary_Default.prefab";
        private const string ThemePath =
            "Assets/UI/Phase5/Theme/AnimalCafeUiTheme.asset";
        private const string GridMaterialPath =
            "Assets/Art/Phase4/Environment/Materials/M_Environment_Grid_01.mat";
        private const string CameraSettingsPath =
            "Assets/Config/DefaultCameraSettings.asset";
        private const string DefaultInputActionsGuid =
            "ca9f5fa95ffab41fb9a615ab714db018";

        internal static Func<
            Phase6SceneSetupTarget,
            Phase6SceneSetupDependencySnapshot> DependencyResolverOverrideForTests
        {
            get;
            set;
        }

        internal static Action<Phase6SceneSetupStage> FaultInjectorForTests { get; set; }

        internal static Action<Phase6SceneRestoreStage> RestoreFaultInjectorForTests { get; set; }

        internal static Func<Phase4AssetValidationReport> Phase4ValidatorOverrideForTests
        {
            get;
            set;
        }

        internal static Func<Phase5UiFoundationValidationReport> Phase5ValidatorOverrideForTests
        {
            get;
            set;
        }

        internal static Action<DecorationCatalogueAsset>
            DecorationCatalogueValidatorOverrideForTests
        {
            get;
            set;
        }

        internal static Action<Phase6SceneSetupTarget, string> SaveSceneObserverForTests
        {
            get;
            set;
        }

        public static void ConfigureMainCafe()
        {
            ConfigureTarget(Phase6SceneSetupTarget.MainCafe);
        }

        public static void ConfigureValidationScene()
        {
            ConfigureTarget(Phase6SceneSetupTarget.Validation);
        }

        private static void ConfigureTarget(Phase6SceneSetupTarget target)
        {
            SceneTransaction transaction = null;
            try
            {
                transaction = SceneTransaction.Begin(target);
                var dependencies = (DependencyResolverOverrideForTests ?? ResolveDependencies)(target);
                ValidateDependencies(dependencies);
                ValidateDependencyAssets(dependencies);
                transaction.RefuseDirtyLoadedTarget();
                transaction.RefuseSelectedTemporaryFixture();
                transaction.CreateBackup();
                FaultInjectorForTests?.Invoke(Phase6SceneSetupStage.BeforeMutation);
                transaction.OpenCandidate(dependencies);
                var changed = ReconcileCandidate(transaction.Scene, target, dependencies);
                if (changed)
                    EditorSceneManager.MarkSceneDirty(transaction.Scene);
                var candidateReport = Phase6DecorationValidator
                    .ValidateCandidateSceneForTests(transaction.Scene, target);
                if (candidateReport.Issues.Count != 0)
                {
                    throw new InvalidOperationException(
                        "Phase 6 candidate validation failed: "
                        + string.Join("; ", candidateReport.Issues.Select(issue => issue.ToString())));
                }

                if (changed || transaction.IsFirstPublish)
                {
                    FaultInjectorForTests?.Invoke(Phase6SceneSetupStage.BeforeSave);
                    SaveSceneObserverForTests?.Invoke(target, transaction.TargetPath);
                    if (!EditorSceneManager.SaveScene(
                            transaction.Scene,
                            transaction.TargetPath,
                            false))
                    {
                        throw new InvalidOperationException(
                            "Unity could not save the Phase 6 target Scene.");
                    }
                    FaultInjectorForTests?.Invoke(Phase6SceneSetupStage.AfterSave);
                }

                transaction.ReloadPersistedTarget();
                var persisted = Phase6DecorationValidator.ValidateAll();
                var targetIssues = persisted.Issues.Where(issue =>
                    string.Equals(issue.AssetPath, transaction.TargetPath, StringComparison.Ordinal)
                    || IsSharedContentPath(issue.AssetPath)).ToArray();
                if (targetIssues.Length != 0)
                {
                    throw new InvalidOperationException(
                        "Persisted Phase 6 target validation failed: "
                        + string.Join("; ", targetIssues.Select(issue => issue.ToString())));
                }

                transaction.Complete();
            }
            catch
            {
                transaction?.Rollback();
                throw;
            }
            finally
            {
                transaction?.Dispose();
                DependencyResolverOverrideForTests = null;
                Phase4ValidatorOverrideForTests = null;
                Phase5ValidatorOverrideForTests = null;
                DecorationCatalogueValidatorOverrideForTests = null;
                FaultInjectorForTests = null;
                RestoreFaultInjectorForTests = null;
                SaveSceneObserverForTests = null;
            }
        }

        private static void ValidateDependencyAssets(
            Phase6SceneSetupDependencySnapshot dependencies)
        {
            var phase4 = (Phase4ValidatorOverrideForTests
                ?? Phase4AssetValidator.ValidateAll)();
            if (phase4.Issues.Count != 0)
            {
                throw new InvalidOperationException(
                    "Phase 4 dependency validation failed: "
                    + string.Join("; ", phase4.Issues.Select(issue =>
                        $"{issue.Code}|{issue.AssetPath}|{issue.Message}")));
            }

            var phase5 = (Phase5ValidatorOverrideForTests
                ?? Phase5UiFoundationValidator.ValidateCanonicalAssets)();
            if (phase5.Issues.Count != 0)
            {
                throw new InvalidOperationException(
                    "Phase 5 dependency validation failed: "
                    + string.Join("; ", phase5.Issues.Select(issue => issue.ToString())));
            }

            (DecorationCatalogueValidatorOverrideForTests
                ?? Phase6DecorationAssetBuilder.ValidateDecorationCatalogue)(
                    dependencies.DecorationCatalogue);
        }

        private static bool IsSharedContentPath(string path) =>
            path.StartsWith("Assets/Content/Furniture/", StringComparison.Ordinal)
            || path.StartsWith("Assets/Content/Decoration/", StringComparison.Ordinal);

        internal static Scene CreateValidationCandidateForTests()
        {
            var dependencies = ResolveDependencies(Phase6SceneSetupTarget.Validation);
            ValidateDependencies(dependencies);
            return CreateValidationCandidate(dependencies, null);
        }

        internal static Scene CreatePersistedValidationCandidateForTests()
        {
            var dependencies = ResolveDependencies(Phase6SceneSetupTarget.Validation);
            ValidateDependencies(dependencies);
            return CreateValidationCandidate(
                dependencies,
                Phase6DecorationValidator.ValidationPath);
        }

        private static Scene CreateValidationCandidate(
            Phase6SceneSetupDependencySnapshot dependencies,
            string persistentPath)
        {
            Scene scene;
            if (string.IsNullOrEmpty(persistentPath))
            {
                scene = OpenIndependentEmptyCandidate();
            }
            else
            {
                if (!CreateEmptySceneAsset(persistentPath))
                    throw new InvalidOperationException(
                        "Unity could not create the empty Phase 6 Validation Scene asset.");
                scene = EditorSceneManager.OpenScene(
                    persistentPath,
                    OpenSceneMode.Additive);
            }
            try
            {
                BuildValidationCandidate(scene, dependencies);
                return scene;
            }
            catch
            {
                if (EditorSceneManager.IsPreviewScene(scene))
                    EditorSceneManager.ClosePreviewScene(scene);
                else
                    EditorSceneManager.CloseScene(scene, true);
                throw;
            }
        }

        private static Scene OpenIndependentEmptyCandidate()
        {
            return EditorSceneManager.NewPreviewScene();
        }

        private static bool CreateEmptySceneAsset(string path)
        {
            var method = typeof(EditorSceneManager).GetMethod(
                "CreateSceneAsset",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(bool) },
                null);
            if (method == null)
                throw new MissingMethodException(
                    typeof(EditorSceneManager).FullName,
                    "CreateSceneAsset");
            return (bool)method.Invoke(null, new object[] { path, false });
        }

        internal static void ReconcileOwnedCandidateForTests(
            Scene candidate,
            Phase6SceneSetupTarget target)
        {
            if (!candidate.IsValid() || !candidate.isLoaded)
                throw new ArgumentException("Candidate Scene must be valid and loaded.", nameof(candidate));

            var dependencies = ResolveDependencies(target);
            ValidateDependencies(dependencies);
            var changed = ReconcileCandidate(candidate, target, dependencies);
            if (changed)
                EditorSceneManager.MarkSceneDirty(candidate);
        }

        private static bool ReconcileCandidate(
            Scene candidate,
            Phase6SceneSetupTarget target,
            Phase6SceneSetupDependencySnapshot dependencies)
        {
            EnsureNoHostileOwnedContent(candidate, target);
            if (target == Phase6SceneSetupTarget.Validation)
            {
                if (candidate.GetRootGameObjects().Length == 0)
                {
                    BuildValidationCandidate(candidate, dependencies);
                    return true;
                }

                return RepairValidationCandidate(candidate, dependencies);
            }

            return ReconcileMainCafe(candidate, dependencies);
        }

        private static bool ReconcileMainCafe(
            Scene scene,
            Phase6SceneSetupDependencySnapshot dependencies)
        {
            var changed = false;
            var camera = FindOne<UnityEngine.Camera>(scene, "Main Camera");
            var phase0 = FindOneTransform(scene, "Phase0_Runtime").gameObject;
            var environment = FindNamed(scene, "P4_Environment").SingleOrDefault();
            if (environment == null)
            {
                environment = CreateEnvironment(scene, dependencies);
                changed = true;
            }

            var ui = ResolveOrCreateDecorationUi(scene, dependencies, out var uiChanged);
            changed |= uiChanged;
            var owner = FindNamed(scene, "Phase6_DecorationRuntime").SingleOrDefault();
            if (owner == null)
            {
                CreateDecorationRuntime(scene, camera, phase0, environment, ui, dependencies);
                owner = FindNamed(scene, "Phase6_DecorationRuntime").Single();
                changed = true;
            }
            else
                changed |= BindDecorationUi(owner.GetComponent<DecorationModeController>(), ui);

            var mouseSource = owner.GetComponent<MouseDecorationInputSource>();
            if (mouseSource == null)
            {
                mouseSource = owner.AddComponent<MouseDecorationInputSource>();
                changed = true;
            }
            changed |= EnsureObjectReference(
                owner.GetComponent<DecorationModeController>(),
                "mouseSourceBehaviour",
                mouseSource);

            foreach (var temporary in FindNamed(
                         scene,
                         "TEMP_P4_ManualReviewFixtures_DELETE_LATER"))
            {
                UnityEngine.Object.DestroyImmediate(temporary);
                changed = true;
            }
            return changed;
        }

        private static bool BindDecorationUi(
            DecorationModeController controller,
            UiReferences ui)
        {
            if (controller == null)
                throw new InvalidOperationException("DecorationModeController is missing.");
            var changed = false;
            changed |= EnsureObjectReference(controller, "catalogueView", ui.Catalogue);
            changed |= EnsureObjectReference(controller, "actionBarView", ui.ActionBar);
            changed |= EnsureObjectReference(controller, "storeModalView", ui.StoreModal);
            changed |= EnsureObjectReference(controller, "decorationModeButton", ui.Toggle);
            changed |= EnsureObjectReference(controller, "decorationModeButtonLabel", ui.ToggleLabel);
            changed |= EnsureObjectReference(controller, "timeControlPanel", ui.TimeControls);
            return changed;
        }

        private static bool RepairValidationCandidate(
            Scene scene,
            Phase6SceneSetupDependencySnapshot dependencies)
        {
            var changed = false;
            var contractRoot = FindNamed(scene, "Phase6_ContractReferences").SingleOrDefault();
            if (contractRoot != null
                && contractRoot.transform.Find("LockedArea_ReferenceOnly") == null)
            {
                var camera = FindOne<UnityEngine.Camera>(scene, "Main Camera");
                CreateReference(
                    contractRoot.transform,
                    "LockedArea_ReferenceOnly",
                    new Vector3(4.25f, 0.05f, 1.5f),
                    "Locked - Reference Only",
                    camera.transform,
                    dependencies.Theme);
                changed = true;
            }
            if (contractRoot != null)
            {
                var camera = FindOne<UnityEngine.Camera>(scene, "Main Camera").transform;
                foreach (var name in new[]
                         {
                             "BlockedArea_ReferenceOnly",
                             "LockedArea_ReferenceOnly"
                         })
                {
                    var reference = contractRoot.transform.Find(name);
                    var label = reference?.GetComponent<TextMeshPro>();
                    if (label == null)
                        continue;
                    var rect = (RectTransform)reference;
                    var expectedRotation = Quaternion.LookRotation(
                        (reference.position - camera.position).normalized,
                        Vector3.up);
                    var expectedPivot = reference.localPosition.z > 0f
                        ? new Vector2(0.75f, 0f)
                        : new Vector2(1f, 0f);
                    if (label.alignment != TextAlignmentOptions.BottomLeft
                        || !Mathf.Approximately(label.fontSize, 1.5f)
                        || rect.sizeDelta != new Vector2(4f, 0.6f)
                        || rect.pivot != expectedPivot
                        || Quaternion.Angle(reference.rotation, expectedRotation) > 0.01f)
                    {
                        label.alignment = TextAlignmentOptions.BottomLeft;
                        label.fontSize = 1.5f;
                        rect.sizeDelta = new Vector2(4f, 0.6f);
                        rect.pivot = expectedPivot;
                        reference.rotation = expectedRotation;
                        changed = true;
                    }
                }
            }

            var ui = ResolveOrCreateDecorationUi(scene, dependencies, out var uiChanged);
            changed |= uiChanged;
            var owner = FindNamed(scene, "Phase6_DecorationRuntime").Single();
            var mouseSource = owner.GetComponent<MouseDecorationInputSource>();
            if (mouseSource == null)
            {
                mouseSource = owner.AddComponent<MouseDecorationInputSource>();
                changed = true;
            }
            var controller = owner.GetComponent<DecorationModeController>();
            changed |= EnsureObjectReference(controller, "mouseSourceBehaviour", mouseSource);
            changed |= BindDecorationUi(controller, ui);
            return changed;
        }

        private static UiReferences ResolveOrCreateDecorationUi(
            Scene scene,
            Phase6SceneSetupDependencySnapshot dependencies,
            out bool changed)
        {
            changed = false;
            var uiRoot = FindNamed(scene, "UI Root").SingleOrDefault()
                ?? throw new InvalidOperationException("MainCafe base dependency UI Root is missing.");
            var hudLayer = RequireTransform(uiRoot.transform, "HUD Canvas/HUD Layer");
            var panelLayer = RequireTransform(uiRoot.transform, "Screen Canvas/Panel Layer");
            var modalLayer = RequireTransform(uiRoot.transform, "Screen Canvas/Modal Layer");
            changed |= EnsureStretched((RectTransform)hudLayer);
            changed |= EnsureStretched((RectTransform)panelLayer);
            changed |= EnsureStretched((RectTransform)modalLayer);

            var safeArea = FindNamed(scene, "Decoration Safe Area").SingleOrDefault();
            if (safeArea == null)
            {
                safeArea = InstantiatePrefab(
                    dependencies.SafeAreaPrefab,
                    hudLayer,
                    "Decoration Safe Area",
                    Vector3.zero,
                    Quaternion.identity);
                safeArea.SetActive(true);
                Stretch((RectTransform)safeArea.transform);
                changed = true;
            }

            var toggleObject = safeArea.transform.Find("DecorationModeButton")?.gameObject
                ?? safeArea.transform.Find("RightRail/DecorationModeButton")?.gameObject;
            if (toggleObject == null)
            {
                toggleObject = InstantiatePrefab(
                    dependencies.ButtonPrefab,
                    safeArea.transform,
                    "DecorationModeButton",
                    Vector3.zero,
                    Quaternion.identity);
                toggleObject.SetActive(true);
                var rect = (RectTransform)toggleObject.transform;
                rect.anchorMin = Vector2.one;
                rect.anchorMax = Vector2.one;
                rect.pivot = Vector2.one;
                rect.anchoredPosition = new Vector2(-24f, -24f);
                rect.sizeDelta = new Vector2(180f, 56f);
                toggleObject.GetComponentInChildren<TMP_Text>(true).text = "Decoration";
                changed = true;
            }

            changed |= EnsureDecorationRightRail(
                scene,
                safeArea.transform,
                toggleObject,
                FindOne<GameTimeService>(scene, "Phase0_Runtime"),
                dependencies.Theme);

            var catalogueObject = FindNamed(scene, "PF_UI_DecorationCatalogue")
                .SingleOrDefault();
            if (catalogueObject == null)
            {
                catalogueObject = InstantiatePrefab(
                    dependencies.CataloguePrefab,
                    panelLayer,
                    "PF_UI_DecorationCatalogue",
                    Vector3.zero,
                    Quaternion.identity);
                changed = true;
            }
            if(!IsExactPhase7Upgrade(catalogueObject,Phase7CataloguePrefabPath))
                changed |= EnsureClosedActiveUiRoot(catalogueObject);
            var actionObject = FindNamed(scene, "PF_UI_DecorationActionBar")
                .SingleOrDefault();
            if (actionObject == null)
            {
                actionObject = InstantiatePrefab(
                    dependencies.ActionBarPrefab,
                    panelLayer,
                    "PF_UI_DecorationActionBar",
                    Vector3.zero,
                    Quaternion.identity);
                changed = true;
            }
            if(!IsExactPhase7Upgrade(actionObject,Phase7ActionBarPrefabPath))
                changed |= EnsureClosedActiveUiRoot(actionObject);
            var modalObject = FindNamed(scene, "PF_UI_DecorationStoreModal")
                .SingleOrDefault();
            if (modalObject == null)
            {
                modalObject = InstantiatePrefab(
                    dependencies.StoreModalPrefab,
                    modalLayer,
                    "PF_UI_DecorationStoreModal",
                    Vector3.zero,
                    Quaternion.identity);
                changed = true;
            }
            changed |= EnsureClosedActiveUiRoot(modalObject);

            return new UiReferences(
                uiRoot,
                catalogueObject.GetComponent<DecorationCatalogueView>(),
                actionObject.GetComponent<DecorationActionBarView>(),
                modalObject.GetComponent<DecorationStoreModalView>(),
                toggleObject.GetComponent<Button>(),
                toggleObject.GetComponentInChildren<TMP_Text>(true),
                safeArea.transform.Find("RightRail").GetComponent<TimeControlPanel>());
        }

        private static void EnsureNoHostileOwnedContent(
            Scene scene,
            Phase6SceneSetupTarget target)
        {
            foreach (var name in new[]
                     {
                         "P4_Environment", "Phase6_DecorationRuntime", "UI Root",
                         "Decoration Safe Area", "PF_UI_DecorationCatalogue",
                         "PF_UI_DecorationActionBar", "PF_UI_DecorationStoreModal"
                     })
            {
                if (FindNamed(scene, name).Length > 1)
                    throw new InvalidOperationException("Duplicate or unknown owned object: " + name + ".");
            }

            if (target == Phase6SceneSetupTarget.Validation)
            {
                var allowedRoots = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Main Camera", "Directional Light", "Phase0_Runtime", "P4_Environment",
                    "Phase6_DecorationRuntime", "UI Root", "Phase6_ContractReferences"
                };
                var unknownRoot = scene.GetRootGameObjects().FirstOrDefault(root =>
                    !allowedRoots.Contains(root.name));
                if (unknownRoot != null)
                    throw new InvalidOperationException(
                        "Unknown Validation Scene root: " + unknownRoot.name + ".");
            }

            var environment = FindNamed(scene, "P4_Environment").SingleOrDefault();
            if (environment != null)
            {
                var floor = environment.transform.Find("P4_Floor_8x8");
                if (floor != null && !HasPrefabSource(floor.gameObject, FloorPrefabPath))
                    throw new InvalidOperationException("P4_Floor_8x8 has the wrong Prefab source.");
            }

            var owner = FindNamed(scene, "Phase6_DecorationRuntime").SingleOrDefault();
            if (owner != null)
            {
                var directChildren = Enumerable.Range(0, owner.transform.childCount)
                    .Select(index => owner.transform.GetChild(index).name).ToArray();
                if (directChildren.Any(name => name != "DecorationSpaceRoot"))
                    throw new InvalidOperationException(
                        "Phase6_DecorationRuntime contains an unexplained owned child.");
            }

            ValidateExistingUiOwnership(scene);
        }

        private static void ValidateExistingUiOwnership(Scene scene)
        {
            var safe = FindNamed(scene, "Decoration Safe Area").SingleOrDefault();
            if (safe != null)
            {
                var legacy = safe.transform.childCount == 1
                    && safe.transform.GetChild(0).name == "DecorationModeButton";
                var canonical = safe.transform.childCount == 1
                    && safe.transform.GetChild(0).name == "RightRail";
                if (!HasPrefabSource(safe, SafeAreaPrefabPath)
                    || (!legacy && !canonical))
                    throw new InvalidOperationException("Decoration Safe Area contains hostile drift.");
            }

            var catalogue = FindNamed(scene, "PF_UI_DecorationCatalogue").SingleOrDefault();
            var action = FindNamed(scene, "PF_UI_DecorationActionBar").SingleOrDefault();
            var modal = FindNamed(scene, "PF_UI_DecorationStoreModal").SingleOrDefault();
            var phase7Catalogue=catalogue!=null&&IsExactPhase7Upgrade(catalogue,Phase7CataloguePrefabPath);
            var phase7Action=action!=null&&IsExactPhase7Upgrade(action,Phase7ActionBarPrefabPath);
            if (catalogue != null
                && (!HasPrefabSource(catalogue,
                        Phase6DecorationAssetPaths.DecorationCataloguePrefabPath)&&!phase7Catalogue
                    || catalogue.GetComponent<BoxCollider>() != null))
                throw new InvalidOperationException("Decoration catalogue UI contains hostile drift.");
            if (action != null
                && !HasPrefabSource(action,
                    Phase6DecorationAssetPaths.DecorationActionBarPrefabPath)&&!phase7Action)
                throw new InvalidOperationException("Decoration action bar UI contains hostile drift.");
            if (modal != null)
            {
                var rect = modal.GetComponent<RectTransform>();
                if (!HasPrefabSource(modal,
                        Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath)
                    || rect == null
                    || rect.anchoredPosition != Vector2.zero)
                    throw new InvalidOperationException("Decoration store modal UI contains hostile drift.");
            }
            if(phase7Catalogue&&phase7Action)
            {
                if(!catalogue.activeSelf||!action.activeSelf||action.transform.GetSiblingIndex()<catalogue.transform.GetSiblingIndex())
                    throw new InvalidOperationException("Canonical Phase 7 decoration UI activation or sibling order drifted.");
                return;
            }
            if (catalogue != null && action != null && modal != null)
            {
                var legacyInactive = !catalogue.activeSelf
                    && !action.activeSelf
                    && !modal.activeSelf;
                var canonicalClosed = IsClosedActiveUiRoot(catalogue)
                    && IsClosedActiveUiRoot(action)
                    && IsClosedActiveUiRoot(modal);
                if (!legacyInactive && !canonicalClosed)
                    throw new InvalidOperationException(
                        "Decoration UI activation or closed CanvasGroup state contains hostile drift.");
            }
            if (catalogue != null && action != null
                && action.transform.GetSiblingIndex() < catalogue.transform.GetSiblingIndex())
                throw new InvalidOperationException("Decoration panel sibling order drifted.");
        }

        private static bool HasPrefabSource(GameObject instance, string path) =>
            string.Equals(
                AssetDatabase.GetAssetPath(
                    PrefabUtility.GetCorrespondingObjectFromSource(instance)),
                path,
                StringComparison.Ordinal);

        private static bool IsExactPhase7Upgrade(GameObject instance,string path)
        {
            if(instance==null||!instance.activeSelf||!HasPrefabSource(instance,path))return false;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(source==null)return false;
            var instanceGraph = BuildComponentGraph(instance);
            if (string.Equals(path, Phase7CataloguePrefabPath, StringComparison.Ordinal))
            {
                // Phase 7 authors the Floor range controls into MainCafe under
                // the prefab's SurfaceFooterHost. Their exact structure is
                // validated by Phase7MainCafeMigrationTests; ignore only that
                // approved Scene-owned subtree while retaining hostile-drift
                // checks for every prefab-authored component.
                instanceGraph = instanceGraph.Where(signature =>
                    !IsPhase7SceneOwnedFloorRangeSignature(signature));
            }

            return instanceGraph.SequenceEqual(BuildComponentGraph(source),StringComparer.Ordinal);
        }

        private static bool IsPhase7SceneOwnedFloorRangeSignature(string signature)
        {
            const string root = "SurfaceFooterHost/FloorRange";
            return signature.StartsWith(root + "|", StringComparison.Ordinal)
                || signature.StartsWith(root + "/", StringComparison.Ordinal);
        }

        private static IEnumerable<string> BuildComponentGraph(GameObject root)=>root
            .GetComponentsInChildren<Transform>(true)
            .SelectMany(transform=>transform.GetComponents<Component>().Select(component=>
                GetRelativePath(root.transform,transform)+"|"+(component==null?"<missing>":component.GetType().FullName)))
            .OrderBy(signature=>signature,StringComparer.Ordinal);

        private static string GetRelativePath(Transform root,Transform current)
        {
            if(current==root)return string.Empty;var names=new Stack<string>();
            while(current!=null&&current!=root){names.Push(current.name);current=current.parent;}
            return string.Join("/",names);
        }

        private static GameObject[] FindNamed(Scene scene, string name) =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name == name)
                .Select(transform => transform.gameObject)
                .ToArray();

        private static Transform FindOneTransform(Scene scene, string name) =>
            FindNamed(scene, name).SingleOrDefault()?.transform
            ?? throw new InvalidOperationException(name + " is missing from the base Scene.");

        private static T FindOne<T>(Scene scene, string name) where T : Component =>
            FindOneTransform(scene, name).GetComponent<T>()
            ?? throw new InvalidOperationException(name + " is missing " + typeof(T).Name + ".");

        internal static Phase6SceneSetupDependencySnapshot CreateMalformedDependencyForTests(
            Phase6SceneSetupTarget target,
            Phase6SceneSetupDependency missingDependency)
        {
            var resolved = ResolveDependencies(target);
            return new Phase6SceneSetupDependencySnapshot(
                target,
                missingDependency == Phase6SceneSetupDependency.ContentCatalog
                    ? null
                    : resolved.ContentCatalog,
                resolved.DecorationCatalogue,
                missingDependency == Phase6SceneSetupDependency.Theme ? null : resolved.Theme,
                missingDependency == Phase6SceneSetupDependency.GridMaterial
                    ? null
                    : resolved.GridMaterial,
                missingDependency == Phase6SceneSetupDependency.CameraSettings
                    ? null
                    : resolved.CameraSettings,
                missingDependency == Phase6SceneSetupDependency.InputActions
                    ? null
                    : resolved.InputActions,
                missingDependency == Phase6SceneSetupDependency.EnvironmentPrefab
                    ? null
                    : resolved.FloorPrefab,
                resolved.BackLeftPrefab,
                resolved.BackRightPrefab,
                resolved.EntrancePrefab,
                resolved.WindowPrefab,
                missingDependency == Phase6SceneSetupDependency.UiPrefab
                    ? null
                    : resolved.UiRootPrefab,
                resolved.SafeAreaPrefab,
                resolved.ButtonPrefab,
                resolved.CataloguePrefab,
                resolved.ActionBarPrefab,
                resolved.StoreModalPrefab);
        }

        private static Phase6SceneSetupDependencySnapshot ResolveDependencies(
            Phase6SceneSetupTarget target)
        {
            var inputPath = AssetDatabase.GUIDToAssetPath(DefaultInputActionsGuid);
            return new Phase6SceneSetupDependencySnapshot(
                target,
                AssetDatabase.LoadAssetAtPath<FurnitureContentCatalog>(
                    Phase6DecorationAssetPaths.ProductionCataloguePath),
                AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                    Phase6DecorationAssetPaths.DecorationCataloguePath),
                AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(ThemePath),
                AssetDatabase.LoadAssetAtPath<Material>(GridMaterialPath),
                AssetDatabase.LoadAssetAtPath<CameraSettings>(CameraSettingsPath),
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(inputPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(FloorPrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(BackLeftPrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(BackRightPrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(EntrancePrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(WindowPrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(UiRootPrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(SafeAreaPrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(ButtonPrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    Phase6DecorationAssetPaths.DecorationCataloguePrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    Phase6DecorationAssetPaths.DecorationActionBarPrefabPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath));
        }

        private static void ValidateDependencies(Phase6SceneSetupDependencySnapshot dependencies)
        {
            if (dependencies == null
                || dependencies.ContentCatalog == null
                || dependencies.DecorationCatalogue == null
                || dependencies.Theme == null
                || dependencies.GridMaterial == null
                || dependencies.CameraSettings == null
                || dependencies.InputActions == null
                || dependencies.FloorPrefab == null
                || dependencies.BackLeftPrefab == null
                || dependencies.BackRightPrefab == null
                || dependencies.EntrancePrefab == null
                || dependencies.WindowPrefab == null
                || dependencies.UiRootPrefab == null
                || dependencies.SafeAreaPrefab == null
                || dependencies.ButtonPrefab == null
                || dependencies.CataloguePrefab == null
                || dependencies.ActionBarPrefab == null
                || dependencies.StoreModalPrefab == null)
            {
                throw new InvalidOperationException(
                    "Phase 6 Scene setup dependency snapshot is incomplete.");
            }
        }

        private static void BuildValidationCandidate(
            Scene scene,
            Phase6SceneSetupDependencySnapshot dependencies)
        {
            var camera = CreateCamera(scene);
            CreateDirectionalLight(scene);
            var phase0 = CreatePhase0Runtime(scene, camera, dependencies.CameraSettings);
            var environment = CreateEnvironment(scene, dependencies);
            var ui = CreateUi(scene, phase0.GetComponent<GameTimeService>(), dependencies);
            CreateDecorationRuntime(scene, camera, phase0, environment, ui, dependencies);
            FindOneTransform(scene, "Phase6_DecorationRuntime").SetSiblingIndex(4);
            CreateContractReferences(scene, camera.transform, dependencies.Theme);
        }

        private static UnityEngine.Camera CreateCamera(Scene scene)
        {
            var gameObject = CreateRoot(scene, "Main Camera");
            gameObject.tag = "MainCamera";
            gameObject.transform.SetPositionAndRotation(
                new Vector3(-10f, 10f, -10f),
                Quaternion.Euler(35.264f, 45f, 0f));
            var camera = gameObject.AddComponent<UnityEngine.Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 7f;
            gameObject.AddComponent<AudioListener>();
            gameObject.AddComponent<UniversalAdditionalCameraData>();
            return camera;
        }

        private static void CreateDirectionalLight(Scene scene)
        {
            var gameObject = CreateRoot(scene, "Directional Light");
            gameObject.transform.SetPositionAndRotation(
                new Vector3(0f, 3f, 0f),
                Quaternion.Euler(50f, -30f, 0f));
            var light = gameObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2f;
            gameObject.AddComponent<UniversalAdditionalLightData>();
        }

        private static GameObject CreatePhase0Runtime(
            Scene scene,
            UnityEngine.Camera camera,
            CameraSettings settings)
        {
            var root = CreateRoot(scene, "Phase0_Runtime");
            var time = root.AddComponent<GameTimeService>();
            var input = root.AddComponent<MouseCameraInput>();
            input.DragThresholdPixels = settings.DragThresholdPixels;
            SetObjectReference(input, "settings", settings);
            var cameraController = root.AddComponent<CafeCameraController>();
            SetObjectReference(cameraController, "targetCamera", camera);
            SetObjectReference(cameraController, "settings", settings);
            SetObjectReference(cameraController, "inputSourceBehaviour", input);
            var interaction = root.AddComponent<SceneInteractionController>();
            SetObjectReference(interaction, "targetCamera", camera);
            SetObjectReference(interaction, "inputSourceBehaviour", input);
            _ = time;
            return root;
        }

        private static GameObject CreateEnvironment(
            Scene scene,
            Phase6SceneSetupDependencySnapshot dependencies)
        {
            var root = CreateRoot(scene, "P4_Environment");
            var floor = InstantiatePrefab(
                dependencies.FloorPrefab,
                root.transform,
                "P4_Floor_8x8",
                Vector3.zero,
                Quaternion.identity);
            var overlay = floor.transform.Find("GridOverlay");
            if (overlay == null)
                throw new InvalidOperationException("Phase 4 Floor Prefab is missing GridOverlay.");
            overlay.gameObject.SetActive(false);

            var backLeft = InstantiatePrefab(
                dependencies.BackLeftPrefab,
                root.transform,
                "P4_Wall_BackLeft",
                new Vector3(0f, 0.5f, 4f),
                Quaternion.identity);
            var backRight = InstantiatePrefab(
                dependencies.BackRightPrefab,
                root.transform,
                "P4_Wall_BackRight",
                new Vector3(4f, 0.5f, 0f),
                Quaternion.Euler(0f, 90f, 0f));
            ConfigureWall(backLeft, "wall.back-left");
            ConfigureWall(backRight, "wall.back-right");
            InstantiatePrefab(
                dependencies.WindowPrefab,
                backRight.transform,
                "P4_Window_BackRight_C3_R0",
                new Vector3(-0.5f, 0.5f, -0.061f),
                Quaternion.identity);
            var entrance = InstantiatePrefab(
                dependencies.EntrancePrefab,
                root.transform,
                "P4_Entrance",
                new Vector3(0f, 0f, -4f),
                Quaternion.identity);
            var portal = GetOrAdd<EntrancePortalAuthoring>(entrance);
            SetString(portal, "entranceId", "entrance.main");
            SetInteger(portal, "originX", 3);
            SetInteger(portal, "originY", 0);
            return root;
        }

        private static void ConfigureWall(GameObject wall, string surfaceId)
        {
            var authoring = GetOrAdd<WallSurfaceAuthoring>(wall);
            SetString(authoring, "surfaceId", surfaceId);
            SetInteger(authoring, "columns", 8);
            SetInteger(authoring, "rows", 2);
            SetFloat(authoring, "slotSize", 1f);
            SetFloat(authoring, "gizmoDepthOffset", -0.055f);
        }

        private readonly struct UiReferences
        {
            internal UiReferences(
                GameObject root,
                DecorationCatalogueView catalogue,
                DecorationActionBarView actionBar,
                DecorationStoreModalView storeModal,
                Button toggle,
                TMP_Text toggleLabel,
                TimeControlPanel timeControls)
            {
                Root = root;
                Catalogue = catalogue;
                ActionBar = actionBar;
                StoreModal = storeModal;
                Toggle = toggle;
                ToggleLabel = toggleLabel;
                TimeControls = timeControls;
            }

            internal GameObject Root { get; }
            internal DecorationCatalogueView Catalogue { get; }
            internal DecorationActionBarView ActionBar { get; }
            internal DecorationStoreModalView StoreModal { get; }
            internal Button Toggle { get; }
            internal TMP_Text ToggleLabel { get; }
            internal TimeControlPanel TimeControls { get; }
        }

        private static UiReferences CreateUi(
            Scene scene,
            GameTimeService gameTime,
            Phase6SceneSetupDependencySnapshot dependencies)
        {
            var uiRoot = (GameObject)PrefabUtility.InstantiatePrefab(dependencies.UiRootPrefab, scene);
            uiRoot.name = "UI Root";
            uiRoot.SetActive(true);
            uiRoot.AddComponent<UiGraphicRegistration>();
            var hudLayer = RequireTransform(uiRoot.transform, "HUD Canvas/HUD Layer");
            var panelLayer = RequireTransform(uiRoot.transform, "Screen Canvas/Panel Layer");
            var modalLayer = RequireTransform(uiRoot.transform, "Screen Canvas/Modal Layer");
            Stretch((RectTransform)hudLayer);
            Stretch((RectTransform)panelLayer);
            Stretch((RectTransform)modalLayer);
            var safeArea = InstantiatePrefab(
                dependencies.SafeAreaPrefab,
                hudLayer,
                "Decoration Safe Area",
                Vector3.zero,
                Quaternion.identity);
            safeArea.SetActive(true);
            var safeRect = (RectTransform)safeArea.transform;
            Stretch(safeRect);

            var toggleObject = InstantiatePrefab(
                dependencies.ButtonPrefab,
                safeArea.transform,
                "DecorationModeButton",
                Vector3.zero,
                Quaternion.identity);
            toggleObject.SetActive(true);
            var toggleRect = (RectTransform)toggleObject.transform;
            toggleRect.anchorMin = Vector2.one;
            toggleRect.anchorMax = Vector2.one;
            toggleRect.pivot = Vector2.one;
            toggleRect.anchoredPosition = new Vector2(-24f, -24f);
            toggleRect.sizeDelta = new Vector2(180f, 56f);
            var toggle = toggleObject.GetComponent<Button>();
            var toggleLabel = toggleObject.GetComponentInChildren<TMP_Text>(true);
            toggleLabel.text = "Decoration";
            EnsureDecorationRightRail(
                scene,
                safeArea.transform,
                toggleObject,
                gameTime,
                dependencies.Theme);

            var catalogueObject = InstantiatePrefab(
                dependencies.CataloguePrefab,
                panelLayer,
                "PF_UI_DecorationCatalogue",
                Vector3.zero,
                Quaternion.identity);
            EnsureClosedActiveUiRoot(catalogueObject);
            var actionObject = InstantiatePrefab(
                dependencies.ActionBarPrefab,
                panelLayer,
                "PF_UI_DecorationActionBar",
                Vector3.zero,
                Quaternion.identity);
            EnsureClosedActiveUiRoot(actionObject);
            var modalObject = InstantiatePrefab(
                dependencies.StoreModalPrefab,
                modalLayer,
                "PF_UI_DecorationStoreModal",
                Vector3.zero,
                Quaternion.identity);
            EnsureClosedActiveUiRoot(modalObject);

            var eventSystem = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            eventSystem.transform.SetParent(uiRoot.transform, false);

            return new UiReferences(
                uiRoot,
                catalogueObject.GetComponent<DecorationCatalogueView>(),
                actionObject.GetComponent<DecorationActionBarView>(),
                modalObject.GetComponent<DecorationStoreModalView>(),
                toggle,
                toggleLabel,
                safeArea.transform.Find("RightRail").GetComponent<TimeControlPanel>());
        }

        private static bool EnsureDecorationRightRail(
            Scene scene,
            Transform safeArea,
            GameObject toggleObject,
            GameTimeService gameTime,
            AnimalCafeUiTheme theme)
        {
            var changed = false;
            var rail = safeArea.Find("RightRail")?.gameObject;
            if (rail == null)
            {
                rail = new GameObject("RightRail", typeof(RectTransform));
                rail.transform.SetParent(safeArea, false);
                changed = true;
            }

            changed |= EnsureTopRightRect(
                (RectTransform)rail.transform,
                new Vector2(-24f, -24f),
                new Vector2(180f, 336f));

            var legacyPanel = FindNamed(scene, "TimePanel").SingleOrDefault();
            var legacyControls = legacyPanel != null
                ? legacyPanel.GetComponent<TimeControlPanel>()
                : null;
            var pause = FindKnownTimeButton(scene, rail.transform, legacyPanel, "PauseButton");
            var normal = FindKnownTimeButton(scene, rail.transform, legacyPanel, "NormalButton");
            var fast = FindKnownTimeButton(scene, rail.transform, legacyPanel, "FastButton");

            changed |= EnsureRailChild(toggleObject.transform, rail.transform, 0f);
            if (pause == null)
            {
                pause = CreateTimeButton(rail.transform, "PauseButton", "Pause", -128f, theme);
                changed = true;
            }
            if (normal == null)
            {
                normal = CreateTimeButton(rail.transform, "NormalButton", "1x", -192f, theme);
                changed = true;
            }
            if (fast == null)
            {
                fast = CreateTimeButton(rail.transform, "FastButton", "2x", -256f, theme);
                changed = true;
            }

            changed |= EnsureRailChild(pause.transform, rail.transform, -128f);
            changed |= EnsureRailChild(normal.transform, rail.transform, -192f);
            changed |= EnsureRailChild(fast.transform, rail.transform, -256f);
            changed |= EnsureTimeSelectedVisual(
                pause, theme, initiallySelected: false, out var pauseSelected);
            changed |= EnsureTimeSelectedVisual(
                normal, theme, initiallySelected: true, out var normalSelected);
            changed |= EnsureTimeSelectedVisual(
                fast, theme, initiallySelected: false, out var fastSelected);

            var indicatorObject = rail.transform.Find("GameTimeStatusIndicator")?.gameObject;
            if (indicatorObject == null)
            {
                indicatorObject = new GameObject(
                    "GameTimeStatusIndicator",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(GameTimeStatusIndicator));
                indicatorObject.transform.SetParent(rail.transform, false);
                var background = indicatorObject.GetComponent<Image>();
                background.color = new Color(0f, 0f, 0f, 0f);
                background.raycastTarget = false;
                var rotating = new GameObject(
                    "RotatingVisual",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                rotating.transform.SetParent(indicatorObject.transform, false);
                var rotatingRect = (RectTransform)rotating.transform;
                rotatingRect.anchorMin = new Vector2(0.5f, 0.5f);
                rotatingRect.anchorMax = new Vector2(0.5f, 0.5f);
                rotatingRect.pivot = new Vector2(0.5f, 0.5f);
                rotatingRect.anchoredPosition = Vector2.zero;
                rotatingRect.sizeDelta = new Vector2(36f, 12f);
                var marker = rotating.GetComponent<Image>();
                marker.color = theme.Colors.Surface;
                marker.raycastTarget = false;
                changed = true;
            }
            changed |= EnsureRailChild(indicatorObject.transform, rail.transform, -64f);
            var indicator = indicatorObject.GetComponent<GameTimeStatusIndicator>();
            changed |= EnsureObjectReference(indicator, "gameTimeService", gameTime);
            changed |= EnsureObjectReference(indicator, "rotatingVisual",
                indicatorObject.transform.Find("RotatingVisual").GetComponent<RectTransform>());

            var controls = rail.GetComponent<TimeControlPanel>();
            if (controls == null)
            {
                controls = rail.AddComponent<TimeControlPanel>();
                changed = true;
            }
            changed |= EnsureObjectReference(controls, "gameTimeService", gameTime);
            changed |= EnsureObjectReference(controls, "pauseButton", pause);
            changed |= EnsureObjectReference(controls, "normalButton", normal);
            changed |= EnsureObjectReference(controls, "fastButton", fast);
            changed |= EnsureObjectReference(
                controls, "pauseSelectedVisual", pauseSelected);
            changed |= EnsureObjectReference(
                controls, "normalSelectedVisual", normalSelected);
            changed |= EnsureObjectReference(
                controls, "fastSelectedVisual", fastSelected);

            changed |= EnsureSiblingIndex(toggleObject.transform, 0);
            changed |= EnsureSiblingIndex(indicatorObject.transform, 1);
            changed |= EnsureSiblingIndex(pause.transform, 2);
            changed |= EnsureSiblingIndex(normal.transform, 3);
            changed |= EnsureSiblingIndex(fast.transform, 4);

            if (legacyControls != null && legacyControls != controls)
            {
                UnityEngine.Object.DestroyImmediate(legacyControls);
                changed = true;
            }
            if (legacyPanel != null && legacyPanel != rail)
            {
                UnityEngine.Object.DestroyImmediate(legacyPanel);
                changed = true;
            }

            return changed;
        }

        private static Button FindKnownTimeButton(
            Scene scene,
            Transform rail,
            GameObject legacyPanel,
            string buttonName)
        {
            var matches = FindNamed(scene, buttonName);
            var railMatches = matches.Where(candidate =>
                candidate.transform.IsChildOf(rail)).ToArray();
            if (railMatches.Length > 0)
                return railMatches.Single().GetComponent<Button>();

            if (legacyPanel != null)
            {
                var legacyMatches = matches.Where(candidate =>
                    candidate.transform.IsChildOf(legacyPanel.transform)).ToArray();
                if (legacyMatches.Length > 0)
                    return legacyMatches.Single().GetComponent<Button>();
            }

            return matches.SingleOrDefault()?.GetComponent<Button>();
        }

        private static bool EnsureSiblingIndex(Transform child, int siblingIndex)
        {
            if (child.GetSiblingIndex() == siblingIndex)
                return false;
            child.SetSiblingIndex(siblingIndex);
            return true;
        }

        private static bool EnsureRailChild(
            Transform child,
            Transform rail,
            float y)
        {
            var changed = false;
            if (child.parent != rail)
            {
                child.SetParent(rail, false);
                changed = true;
            }
            changed |= EnsureTopRightRect(
                (RectTransform)child,
                new Vector2(0f, y),
                new Vector2(180f, 56f));
            return changed;
        }

        private static bool EnsureTopRightRect(
            RectTransform rect,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            if (rect.anchorMin == Vector2.one
                && rect.anchorMax == Vector2.one
                && rect.pivot == Vector2.one
                && rect.anchoredPosition == anchoredPosition
                && rect.sizeDelta == sizeDelta
                && rect.localRotation == Quaternion.identity
                && rect.localScale == Vector3.one)
            {
                return false;
            }
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            return true;
        }

        private static Button CreateTimeButton(
            Transform parent,
            string name,
            string label,
            float y,
            AnimalCafeUiTheme theme)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = (RectTransform)gameObject.transform;
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(180f, 56f);
            rect.anchoredPosition = new Vector2(0f, y);
            var image = gameObject.AddComponent<Image>();
            var rounded = AssetDatabase.LoadAllAssetsAtPath(
                    "Assets/UI/Phase5/Sprites/S_UI_RoundedRect.asset")
                .OfType<Sprite>()
                .FirstOrDefault();
            image.sprite = rounded;
            image.type = rounded != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = theme.Colors.Accent;
            var button = gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var shadow = gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.20f, 0.12f, 0.06f, 0.24f);
            shadow.effectDistance = new Vector2(0f, -6f);
            shadow.useGraphicAlpha = true;
            gameObject.AddComponent<AnimalCafeButtonView>()
                .Configure(theme, UiButtonRole.Primary, button, image);

            var labelObject = new GameObject("Label", typeof(RectTransform));
            labelObject.transform.SetParent(gameObject.transform, false);
            var labelRect = (RectTransform)labelObject.transform;
            Stretch(labelRect);
            var text = labelObject.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.font = theme.Typography.Label.FontAsset;
            text.fontSize = theme.Typography.Label.FontSize;
            text.raycastTarget = false;
            return button;
        }

        private static bool EnsureTimeSelectedVisual(
            Button button,
            AnimalCafeUiTheme theme,
            bool initiallySelected,
            out GameObject selectedVisual)
        {
            var changed = false;
            var selected = button.transform.Find("SelectedVisual");
            if (selected == null)
            {
                selectedVisual = new GameObject(
                    "SelectedVisual",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                selectedVisual.transform.SetParent(button.transform, false);
                selected = selectedVisual.transform;
                changed = true;
            }
            else
            {
                selectedVisual = selected.gameObject;
            }

            var rect = (RectTransform)selected;
            var expectedMin = new Vector2(0f, 0f);
            var expectedMax = new Vector2(0f, 1f);
            var expectedPivot = new Vector2(0f, 0.5f);
            var expectedPosition = new Vector2(10f, 0f);
            var expectedSize = new Vector2(12f, -16f);
            if (rect.anchorMin != expectedMin
                || rect.anchorMax != expectedMax
                || rect.pivot != expectedPivot
                || rect.anchoredPosition != expectedPosition
                || rect.sizeDelta != expectedSize
                || rect.localRotation != Quaternion.identity
                || rect.localScale != Vector3.one)
            {
                rect.anchorMin = expectedMin;
                rect.anchorMax = expectedMax;
                rect.pivot = expectedPivot;
                rect.anchoredPosition = expectedPosition;
                rect.sizeDelta = expectedSize;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one;
                changed = true;
            }

            var image = selectedVisual.GetComponent<Image>();
            if (image == null)
            {
                image = selectedVisual.AddComponent<Image>();
                changed = true;
            }
            if (image.color != theme.Colors.Surface)
            {
                image.color = theme.Colors.Surface;
                changed = true;
            }
            if (image.raycastTarget)
            {
                image.raycastTarget = false;
                changed = true;
            }
            if (selectedVisual.activeSelf != initiallySelected)
            {
                selectedVisual.SetActive(initiallySelected);
                changed = true;
            }
            return changed;
        }

        private static void CreateDecorationRuntime(
            Scene scene,
            UnityEngine.Camera camera,
            GameObject phase0,
            GameObject environment,
            UiReferences ui,
            Phase6SceneSetupDependencySnapshot dependencies)
        {
            var root = CreateRoot(scene, "Phase6_DecorationRuntime");
            var layoutRuntime = root.AddComponent<CafeLayoutRuntime>();
            var controller = root.AddComponent<DecorationModeController>();
            var registry = root.AddComponent<FurnitureSceneRegistry>();
            var preview = root.AddComponent<FurniturePreviewView>();
            var grid = root.AddComponent<GridHighlightView>();
            var touch = root.AddComponent<InputSystemDecorationTouchSource>();
            var cameraDriver = root.AddComponent<DecorationCameraDriver>();
            var mouse = root.AddComponent<MouseDecorationInputSource>();

            var space = CreateChild(root.transform, "DecorationSpaceRoot");
            space.transform.localPosition = new Vector3(-4f, 0f, -4f);
            var gridVisual = CreateChild(space.transform, "GridVisualRoot");
            var representation = CreateChild(space.transform, "FurnitureRepresentationRoot");
            var previewRoot = CreateChild(space.transform, "FurniturePreviewRoot");

            var entrance = environment.transform.Find("P4_Entrance")
                .GetComponent<EntrancePortalAuthoring>();
            SetObjectReference(layoutRuntime, "contentCatalog", dependencies.ContentCatalog);
            SetObjectReference(layoutRuntime, "entrancePortal", entrance);

            SetObjectReference(controller, "layoutRuntime", layoutRuntime);
            SetObjectReference(controller, "contentCatalog", dependencies.ContentCatalog);
            SetObjectReference(controller, "catalogueAsset", dependencies.DecorationCatalogue);
            SetObjectReference(controller, "targetCamera", camera);
            SetObjectReference(controller, "cameraSettings", dependencies.CameraSettings);
            SetObjectReference(controller, "cameraController", phase0.GetComponent<CafeCameraController>());
            SetObjectReference(controller, "sceneInteraction", phase0.GetComponent<SceneInteractionController>());
            SetObjectReference(controller, "floorCollider",
                environment.transform.Find("P4_Floor_8x8").GetComponentInChildren<Collider>(true));
            SetObjectReference(controller, "gridRoot", space.transform);
            SetObjectReference(controller, "furnitureRepresentationRoot", representation.transform);
            SetObjectReference(controller, "furniturePreviewRoot", previewRoot.transform);
            SetObjectReference(controller, "gridVisualRoot", gridVisual.transform);
            SetObjectReference(controller, "gridMaterialTemplate", dependencies.GridMaterial);
            SetObjectReference(controller, "uiTheme", dependencies.Theme);
            SetObjectReference(controller, "sceneRegistry", registry);
            SetObjectReference(controller, "previewView", preview);
            SetObjectReference(controller, "gridView", grid);
            SetObjectReference(controller, "cameraDriver", cameraDriver);
            SetObjectReference(controller, "catalogueView", ui.Catalogue);
            SetObjectReference(controller, "actionBarView", ui.ActionBar);
            SetObjectReference(controller, "storeModalView", ui.StoreModal);
            SetObjectReference(controller, "decorationModeButton", ui.Toggle);
            SetObjectReference(controller, "decorationModeButtonLabel", ui.ToggleLabel);
            SetObjectReference(controller, "timeControlPanel", ui.TimeControls);
            SetObjectReference(controller, "gameTimeServiceBehaviour", phase0.GetComponent<GameTimeService>());
            SetObjectReference(controller, "touchSourceBehaviour", touch);
            SetObjectReference(controller, "mouseSourceBehaviour", mouse);
        }

        private static void CreateContractReferences(
            Scene scene,
            Transform camera,
            AnimalCafeUiTheme theme)
        {
            var root = CreateRoot(scene, "Phase6_ContractReferences");
            CreateReference(root.transform, "BlockedArea_ReferenceOnly",
                new Vector3(4.25f, 0.05f, -0.5f), "Blocked - Reference Only", camera, theme);
            CreateReference(root.transform, "LockedArea_ReferenceOnly",
                new Vector3(4.25f, 0.05f, 1.5f), "Locked - Reference Only", camera, theme);
        }

        private static void CreateReference(
            Transform parent,
            string name,
            Vector3 localPosition,
            string value,
            Transform camera,
            AnimalCafeUiTheme theme)
        {
            var gameObject = new GameObject(name, typeof(TextMeshPro));
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.rotation = Quaternion.LookRotation(
                (gameObject.transform.position - camera.position).normalized,
                Vector3.up);
            var text = gameObject.GetComponent<TextMeshPro>();
            text.text = value;
            text.font = theme.Typography.Label.FontAsset;
            text.fontSharedMaterial = theme.Typography.Label.FontAsset.material;
            text.fontSize = 1.5f;
            text.alignment = TextAlignmentOptions.BottomLeft;
            gameObject.transform.localPosition = localPosition;
            if (gameObject.transform is RectTransform rect)
            {
                rect.sizeDelta = new Vector2(4f, 0.6f);
                rect.pivot = localPosition.z > 0f
                    ? new Vector2(0.75f, 0f)
                    : new Vector2(1f, 0f);
                rect.anchoredPosition3D = localPosition;
            }
            gameObject.transform.rotation = Quaternion.LookRotation(
                (gameObject.transform.position - camera.position).normalized,
                Vector3.up);
        }

        private static GameObject CreateRoot(Scene scene, string name)
        {
            var gameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            SetIdentity(gameObject.transform);
            return gameObject;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            SetIdentity(gameObject.transform);
            return gameObject;
        }

        private static GameObject InstantiatePrefab(
            GameObject prefab,
            Transform parent,
            string name,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
                throw new InvalidOperationException("Could not instantiate Prefab " + prefab.name + ".");
            instance.name = name;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static Transform RequireTransform(Transform root, string path) =>
            root.Find(path) ?? throw new InvalidOperationException(
                $"Prefab '{root.name}' is missing '{path}'.");

        private static T GetOrAdd<T>(GameObject target) where T : Component =>
            target.GetComponent<T>() ?? target.AddComponent<T>();

        private static void SetIdentity(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = Vector3.zero;
            rect.sizeDelta = Vector2.zero;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
        }

        private static bool EnsureClosedActiveUiRoot(GameObject root)
        {
            var group = root.GetComponent<CanvasGroup>()
                ?? throw new InvalidOperationException(root.name + " is missing CanvasGroup.");
            var changed = false;
            if (!root.activeSelf)
            {
                root.SetActive(true);
                changed = true;
            }
            if (!Mathf.Approximately(group.alpha, 0f))
            {
                group.alpha = 0f;
                changed = true;
            }
            if (group.interactable)
            {
                group.interactable = false;
                changed = true;
            }
            if (group.blocksRaycasts)
            {
                group.blocksRaycasts = false;
                changed = true;
            }
            return changed;
        }

        private static bool IsClosedActiveUiRoot(GameObject root)
        {
            var group = root.GetComponent<CanvasGroup>();
            return root.activeSelf
                && group != null
                && Mathf.Approximately(group.alpha, 0f)
                && !group.interactable
                && !group.blocksRaycasts;
        }

        private static bool EnsureStretched(RectTransform rect)
        {
            if (rect.anchorMin == Vector2.zero
                && rect.anchorMax == Vector2.one
                && rect.pivot == new Vector2(0.5f, 0.5f)
                && rect.anchoredPosition3D == Vector3.zero
                && rect.sizeDelta == Vector2.zero
                && rect.offsetMin == Vector2.zero
                && rect.offsetMax == Vector2.zero
                && Quaternion.Angle(rect.localRotation, Quaternion.identity) < 0.01f
                && rect.localScale == Vector3.one)
            {
                return false;
            }

            Stretch(rect);
            return true;
        }

        private static void SetObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                ?? throw new InvalidOperationException(
                    $"{target.GetType().Name} has no serialized property '{propertyName}'.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool EnsureObjectReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                ?? throw new InvalidOperationException(
                    $"{target.GetType().Name} has no serialized property '{propertyName}'.");
            if (property.objectReferenceValue == value)
                return false;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static void SetString(UnityEngine.Object target, string propertyName, string value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                ?? throw new InvalidOperationException(propertyName);
            property.stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInteger(UnityEngine.Object target, string propertyName, int value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                ?? throw new InvalidOperationException(propertyName);
            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(UnityEngine.Object target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName)
                ?? throw new InvalidOperationException(propertyName);
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class SceneTransaction : IDisposable
        {
            private const string BackupRoot =
                "Library/AnimalCafe/Phase6Task8SceneBackup";

            private readonly Phase6SceneSetupTarget target;
            private readonly bool targetExisted;
            private readonly bool originalTargetLoaded;
            private readonly bool originalTargetDirty;
            private readonly int originalTargetIndex;
            private readonly ulong activeSceneHandle;
            private readonly UnityEngine.Object[] selection;
            private readonly bool[] selectionHadReference;
            private readonly GlobalObjectId?[] targetSelectionIds;
            private readonly int activeSelectionIndex;
            private readonly ulong previousSceneHandle;
            private readonly ulong nextSceneHandle;
            private readonly UnityEngine.Object[] dirtyPersistentAssets;
            private readonly DirtyAssetFileState[] dirtyAssetFiles;
            private readonly ulong[] dirtyUnrelatedSceneHandles;
            private string runFolder;
            private bool backupCreated;
            private bool completed;
            private bool rolledBack;

            private SceneTransaction(Phase6SceneSetupTarget target)
            {
                this.target = target;
                TargetPath = target == Phase6SceneSetupTarget.MainCafe
                    ? Phase6DecorationValidator.MainCafePath
                    : Phase6DecorationValidator.ValidationPath;
                targetExisted = File.Exists(TargetPath);
                var loadedScenes = Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt).ToArray();
                var existing = loadedScenes.FirstOrDefault(candidate =>
                    string.Equals(candidate.path, TargetPath, StringComparison.Ordinal));
                originalTargetLoaded = existing.IsValid() && existing.isLoaded;
                originalTargetDirty = originalTargetLoaded && existing.isDirty;
                originalTargetIndex = originalTargetLoaded
                    ? Array.FindIndex(loadedScenes, candidate => candidate.handle == existing.handle)
                    : -1;
                previousSceneHandle = originalTargetIndex > 0
                    ? loadedScenes[originalTargetIndex - 1].handle.GetRawData()
                    : 0UL;
                nextSceneHandle = originalTargetIndex >= 0
                    && originalTargetIndex + 1 < loadedScenes.Length
                    ? loadedScenes[originalTargetIndex + 1].handle.GetRawData()
                    : 0UL;
                activeSceneHandle = SceneManager.GetActiveScene().handle.GetRawData();
                selection = Selection.objects.ToArray();
                selectionHadReference = selection
                    .Select(value => !ReferenceEquals(value, null))
                    .ToArray();
                activeSelectionIndex = Array.IndexOf(selection, Selection.activeObject);
                targetSelectionIds = selection.Select(value =>
                    BelongsToTarget(value, TargetPath)
                        ? GlobalObjectId.GetGlobalObjectIdSlow(value)
                        : (GlobalObjectId?)null).ToArray();
                dirtyPersistentAssets = Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                    .Where(value => value != null
                        && EditorUtility.IsPersistent(value)
                        && EditorUtility.IsDirty(value))
                    .ToArray();
                dirtyAssetFiles = dirtyPersistentAssets
                    .Select(AssetDatabase.GetAssetPath)
                    .Where(path => !string.IsNullOrEmpty(path) && File.Exists(path))
                    .Distinct(StringComparer.Ordinal)
                    .Select(DirtyAssetFileState.Capture)
                    .ToArray();
                dirtyUnrelatedSceneHandles = loadedScenes.Where(candidate =>
                        candidate.isDirty
                        && !string.Equals(candidate.path, TargetPath, StringComparison.Ordinal))
                    .Select(candidate => candidate.handle.GetRawData())
                    .ToArray();
                if (originalTargetLoaded)
                    Scene = existing;
            }

            internal string TargetPath { get; }
            internal Scene Scene { get; private set; }
            internal bool IsFirstPublish => !targetExisted;

            internal static SceneTransaction Begin(Phase6SceneSetupTarget target) =>
                new SceneTransaction(target);

            internal void RefuseDirtyLoadedTarget()
            {
                if (originalTargetDirty)
                    throw new InvalidOperationException(
                        "The target Scene is dirty. Save or revert it before Phase 6 setup.");
            }

            internal void RefuseSelectedTemporaryFixture()
            {
                if (target != Phase6SceneSetupTarget.MainCafe)
                    return;
                foreach (var value in selection)
                {
                    var transform = value switch
                    {
                        GameObject gameObject => gameObject.transform,
                        Component component => component.transform,
                        _ => null
                    };
                    for (var current = transform; current != null; current = current.parent)
                    {
                        if (current.name == "TEMP_P4_ManualReviewFixtures_DELETE_LATER")
                        {
                            throw new InvalidOperationException(
                                "Deselect the temporary MainCafe fixture before Phase 6 setup and retry.");
                        }
                    }
                }
            }

            internal void CreateBackup()
            {
                runFolder = Path.Combine(BackupRoot, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(runFolder);
                CopyIfPresent(TargetPath, Path.Combine(runFolder, "target.unity"));
                CopyIfPresent(TargetPath + ".meta", Path.Combine(runFolder, "target.meta"));
                backupCreated = true;
            }

            internal void OpenCandidate(
                Phase6SceneSetupDependencySnapshot dependencies)
            {
                if (originalTargetLoaded)
                    return;
                if (targetExisted)
                {
                    Scene = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Additive);
                    return;
                }
                if (target != Phase6SceneSetupTarget.Validation)
                    throw new InvalidOperationException("MainCafe Scene is missing.");
                Scene = CreateValidationCandidate(dependencies, TargetPath);
            }

            internal void Complete()
            {
                if (originalTargetLoaded)
                {
                    RestoreTargetOrder();
                    RestoreActiveScene();
                    RestoreSelection();
                }
                else
                {
                    if (Scene.IsValid() && Scene.isLoaded)
                        EditorSceneManager.CloseScene(Scene, true);
                    RestoreActiveScene();
                    RestoreSelection();
                }
                RestoreCallerDirtyFlags();
                CleanupBackup();
                completed = true;
            }

            internal void ReloadPersistedTarget()
            {
                if (Scene.IsValid() && Scene.isLoaded)
                    EditorSceneManager.CloseScene(Scene, true);
                Scene = EditorSceneManager.OpenScene(TargetPath, OpenSceneMode.Additive);
            }

            internal void Rollback()
            {
                if (rolledBack || completed)
                    return;
                if (!backupCreated)
                {
                    RestoreCallerDirtyFlags();
                    rolledBack = true;
                    return;
                }

                if (Scene.IsValid() && Scene.isLoaded)
                    EditorSceneManager.CloseScene(Scene, true);
                RestoreFiles();
                if (originalTargetLoaded)
                {
                    Scene = EditorSceneManager.OpenScene(
                        TargetPath,
                        OpenSceneMode.Additive);
                    RestoreTargetOrder();
                }
                RestoreActiveScene();
                RestoreSelection();
                RestoreCallerDirtyFlags();
                rolledBack = true;
                CleanupBackup();
            }

            public void Dispose()
            {
                if (!completed && !rolledBack && backupCreated)
                    Rollback();
            }

            private void RestoreFiles()
            {
                if (targetExisted)
                {
                    try
                    {
                        RestoreExistingTarget(true);
                    }
                    catch (Exception firstRestoreFailure)
                    {
                        try
                        {
                            RestoreExistingTarget(false);
                            Debug.LogWarning(
                                "Phase 6 rollback recovered from a Scene restore failure: "
                                + firstRestoreFailure.Message);
                        }
                        catch (Exception recoveryFailure)
                        {
                            throw new AggregateException(
                                "Phase 6 rollback could not restore the target Scene. "
                                + "The disk backup was retained at '" + runFolder + "'.",
                                firstRestoreFailure,
                                recoveryFailure);
                        }
                    }
                    return;
                }

                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TargetPath) != null
                    || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(
                        TargetPath,
                        AssetPathToGUIDOptions.OnlyExistingAssets)))
                    AssetDatabase.DeleteAsset(TargetPath);
                if (File.Exists(TargetPath)) File.Delete(TargetPath);
                if (File.Exists(TargetPath + ".meta")) File.Delete(TargetPath + ".meta");
            }

            private void RestoreExistingTarget(bool invokeTestFaults)
            {
                var sceneBackup = Path.Combine(runFolder, "target.unity");
                var metaBackup = Path.Combine(runFolder, "target.meta");
                var stagedScene = Path.Combine(runFolder, "restore.unity");
                var stagedMeta = Path.Combine(runFolder, "restore.meta");

                if (invokeTestFaults)
                    RestoreFaultInjectorForTests?.Invoke(
                        Phase6SceneRestoreStage.BeforeStagingCopy);
                File.Copy(sceneBackup, stagedScene, true);
                if (File.Exists(metaBackup))
                    File.Copy(metaBackup, stagedMeta, true);
                else if (File.Exists(stagedMeta))
                    File.Delete(stagedMeta);
                VerifySameBytes(sceneBackup, stagedScene, "staged Scene");
                VerifyOptionalSameBytes(metaBackup, stagedMeta, "staged Scene metadata");

                ReleaseTargetAsset();
                if (invokeTestFaults)
                    RestoreFaultInjectorForTests?.Invoke(
                        Phase6SceneRestoreStage.AfterAssetRelease);

                File.Move(stagedScene, TargetPath);
                if (File.Exists(stagedMeta))
                    File.Move(stagedMeta, TargetPath + ".meta");
                if (invokeTestFaults)
                    RestoreFaultInjectorForTests?.Invoke(
                        Phase6SceneRestoreStage.BeforeImport);
                AssetDatabase.ImportAsset(
                    TargetPath,
                    ImportAssetOptions.ForceSynchronousImport
                    | ImportAssetOptions.ForceUpdate);

                VerifySameBytes(sceneBackup, TargetPath, "restored Scene");
                VerifyOptionalSameBytes(metaBackup, TargetPath + ".meta", "restored Scene metadata");
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetPath) == null)
                    throw new InvalidOperationException(
                        "Unity could not import the restored target Scene.");
            }

            private void ReleaseTargetAsset()
            {
                var targetIsKnown = File.Exists(TargetPath)
                    || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TargetPath) != null
                    || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(
                        TargetPath,
                        AssetPathToGUIDOptions.OnlyExistingAssets));
                if (targetIsKnown && !AssetDatabase.DeleteAsset(TargetPath))
                    throw new InvalidOperationException(
                        "Unity could not release the target Scene before rollback restore.");
                if (File.Exists(TargetPath) || File.Exists(TargetPath + ".meta"))
                    throw new IOException(
                        "Unity reported deleting the target Scene but retained its files.");
            }

            private static void VerifySameBytes(
                string expectedPath,
                string actualPath,
                string label)
            {
                if (!File.Exists(expectedPath)
                    || !File.Exists(actualPath)
                    || !File.ReadAllBytes(actualPath)
                        .SequenceEqual(File.ReadAllBytes(expectedPath)))
                {
                    throw new IOException(label + " does not match its rollback backup.");
                }
            }

            private static void VerifyOptionalSameBytes(
                string expectedPath,
                string actualPath,
                string label)
            {
                if (File.Exists(expectedPath) != File.Exists(actualPath))
                    throw new IOException(label + " existence does not match its rollback backup.");
                if (File.Exists(expectedPath))
                    VerifySameBytes(expectedPath, actualPath, label);
            }

            private void RestoreTargetOrder()
            {
                var next = FindLoadedScene(nextSceneHandle);
                if (next.IsValid() && next.isLoaded)
                {
                    EditorSceneManager.MoveSceneBefore(Scene, next);
                    return;
                }
                var previous = FindLoadedScene(previousSceneHandle);
                if (previous.IsValid() && previous.isLoaded)
                    EditorSceneManager.MoveSceneAfter(Scene, previous);
            }

            private void RestoreActiveScene()
            {
                Scene active;
                if (originalTargetLoaded && activeSceneHandle != 0UL
                    && FindLoadedScene(activeSceneHandle).IsValid() == false)
                    active = Scene;
                else
                    active = FindLoadedScene(activeSceneHandle);
                if (active.IsValid() && active.isLoaded)
                    SceneManager.SetActiveScene(active);
            }

            private void RestoreSelection()
            {
                var restored = new UnityEngine.Object[selection.Length];
                for (var index = 0; index < selection.Length; index++)
                {
                    restored[index] = targetSelectionIds[index].HasValue
                        ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(
                            targetSelectionIds[index].Value)
                        : selection[index];
                    if (selectionHadReference[index] && restored[index] == null)
                        throw new InvalidOperationException(
                            "Could not restore a selected target Scene object.");
                }
                Selection.objects = restored;
                if (activeSelectionIndex >= 0
                    && activeSelectionIndex < restored.Length
                    && Selection.activeObject != restored[activeSelectionIndex])
                    Selection.activeObject = restored[activeSelectionIndex];
            }

            private void RestoreCallerDirtyFlags()
            {
                foreach (var file in dirtyAssetFiles)
                    file.RestoreExactBytes();
                foreach (var asset in dirtyPersistentAssets)
                {
                    if (asset != null && !EditorUtility.IsDirty(asset))
                        EditorUtility.SetDirty(asset);
                }
                foreach (var handle in dirtyUnrelatedSceneHandles)
                {
                    var scene = FindLoadedScene(handle);
                    if (scene.IsValid() && scene.isLoaded && !scene.isDirty)
                        EditorSceneManager.MarkSceneDirty(scene);
                }
            }

            private readonly struct DirtyAssetFileState
            {
                private readonly string path;
                private readonly byte[] bytes;
                private readonly byte[] metaBytes;

                private DirtyAssetFileState(
                    string path,
                    byte[] bytes,
                    byte[] metaBytes)
                {
                    this.path = path;
                    this.bytes = bytes;
                    this.metaBytes = metaBytes;
                }

                internal static DirtyAssetFileState Capture(string path) =>
                    new DirtyAssetFileState(
                        path,
                        File.ReadAllBytes(path),
                        File.Exists(path + ".meta")
                            ? File.ReadAllBytes(path + ".meta")
                            : null);

                internal void RestoreExactBytes()
                {
                    if (!File.Exists(path)
                        || !File.ReadAllBytes(path).SequenceEqual(bytes))
                        File.WriteAllBytes(path, bytes);
                    if (metaBytes != null
                        && (!File.Exists(path + ".meta")
                            || !File.ReadAllBytes(path + ".meta").SequenceEqual(metaBytes)))
                        File.WriteAllBytes(path + ".meta", metaBytes);
                }
            }

            private void CleanupBackup()
            {
                if (string.IsNullOrEmpty(runFolder) || !Directory.Exists(runFolder))
                    return;
                try
                {
                    Directory.Delete(runFolder, true);
                    if (Directory.Exists(BackupRoot)
                        && !Directory.EnumerateFileSystemEntries(BackupRoot).Any())
                        Directory.Delete(BackupRoot);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "Phase 6 setup retained diagnostic backup '" + runFolder
                        + "': " + exception.Message);
                }
            }

            private static void CopyIfPresent(string source, string destination)
            {
                if (File.Exists(source))
                    File.Copy(source, destination, true);
            }

            private static bool BelongsToTarget(UnityEngine.Object value, string targetPath)
            {
                if (ReferenceEquals(value, null))
                    return false;
                if (string.Equals(
                        AssetDatabase.GetAssetPath(value),
                        targetPath,
                        StringComparison.Ordinal))
                    return true;
                var scene = value switch
                {
                    GameObject gameObject => gameObject.scene,
                    Component component => component.gameObject.scene,
                    _ => default
                };
                return scene.IsValid()
                    && string.Equals(scene.path, targetPath, StringComparison.Ordinal);
            }

            private static Scene FindLoadedScene(ulong handle)
            {
                if (handle == 0UL)
                    return default;
                for (var index = 0; index < SceneManager.sceneCount; index++)
                {
                    var candidate = SceneManager.GetSceneAt(index);
                    if (candidate.handle.GetRawData() == handle)
                        return candidate;
                }
                return default;
            }
        }
    }
}

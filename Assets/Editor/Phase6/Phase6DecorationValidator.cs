using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AnimalCafe.Camera;
using AnimalCafe.Content;
using AnimalCafe.Core.Time;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
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
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.Phase6
{
    public enum Phase6DecorationIssueCode
    {
        MissingMainCafeScene = 1,
        MissingValidationScene,
        MissingMainCamera,
        DuplicateMainCamera,
        MissingDirectionalLight,
        DuplicateDirectionalLight,
        MissingPhase0Runtime,
        DuplicatePhase0Runtime,
        MissingGameTimeService,
        DuplicateGameTimeService,
        MissingMouseCameraInput,
        DuplicateMouseCameraInput,
        MissingCafeCameraController,
        DuplicateCafeCameraController,
        MissingSceneInteractionController,
        DuplicateSceneInteractionController,
        RuntimeComponentLocationDrift,
        CameraSettingsBindingDrift,
        MissingEnvironmentRoot,
        DuplicateEnvironmentRoot,
        MissingEnvironmentPrefab,
        EnvironmentPrefabDrift,
        EnvironmentTransformDrift,
        FloorGridOverlayStateDrift,
        MissingDecorationOwner,
        DuplicateDecorationOwner,
        MissingGridRoot,
        DuplicateGridRoot,
        InvalidGridTransform,
        MissingCatalogueBinding,
        MismatchedCatalogueBinding,
        MissingUiReference,
        MissingUiRoot,
        DuplicateUiRoot,
        MissingCanvas,
        DuplicateCanvas,
        UnexpectedCanvas,
        MissingEventSystem,
        DuplicateEventSystem,
        MissingInputSystemUiModule,
        DuplicateInputSystemUiModule,
        MissingInputActions,
        UnexpectedStandaloneInputModule,
        MissingTimePanel,
        DuplicateTimePanel,
        TimeControlWiringDrift,
        MissingContractReferenceRoot,
        DuplicateContractReferenceRoot,
        ContractReferenceDrift,
        ContractReferenceGameplayBinding,
        MissingDefinition,
        MissingPrefab,
        MissingThumbnail,
        MissingHudToggle,
        UnexpectedSerializedRepresentation,
        TemporaryFixturePresent,
        UnexpectedInitialContent,
        MissingScript,
        BuildSettingsScopeDrift,
        RuntimeEditorReference,
        SaveBoundaryViolation
    }

    public sealed class Phase6DecorationValidationIssue :
        IEquatable<Phase6DecorationValidationIssue>
    {
        public Phase6DecorationValidationIssue(
            Phase6DecorationIssueCode code,
            string assetPath,
            string objectPath,
            string message)
        {
            Code = code;
            AssetPath = assetPath ?? string.Empty;
            ObjectPath = objectPath ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public Phase6DecorationIssueCode Code { get; }
        public string AssetPath { get; }
        public string ObjectPath { get; }
        public string Message { get; }

        public bool Equals(Phase6DecorationValidationIssue other)
        {
            return other != null
                && Code == other.Code
                && string.Equals(AssetPath, other.AssetPath, StringComparison.Ordinal)
                && string.Equals(ObjectPath, other.ObjectPath, StringComparison.Ordinal)
                && string.Equals(Message, other.Message, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) =>
            Equals(obj as Phase6DecorationValidationIssue);

        public override int GetHashCode() =>
            HashCode.Combine(Code, AssetPath, ObjectPath, Message);

        public override string ToString() =>
            $"{Code}|{AssetPath}|{ObjectPath}|{Message}";
    }

    public sealed class Phase6DecorationValidationReport
    {
        public Phase6DecorationValidationReport(
            IEnumerable<Phase6DecorationValidationIssue> issues)
        {
            var ordered = (issues ?? Array.Empty<Phase6DecorationValidationIssue>())
                .Where(issue => issue != null)
                .Distinct()
                .OrderBy(issue => issue.AssetPath, StringComparer.Ordinal)
                .ThenBy(issue => issue.ObjectPath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                .ToList();
            Issues = new ReadOnlyCollection<Phase6DecorationValidationIssue>(ordered);
        }

        public IReadOnlyList<Phase6DecorationValidationIssue> Issues { get; }
    }

    /// <summary>
    /// Read-only Phase 6 production contract validation.
    /// </summary>
    public static class Phase6DecorationValidator
    {
        internal const string MainCafePath = "Assets/Scenes/MainCafe.unity";
        internal const string ValidationPath =
            "Assets/Scenes/Validation/Phase6DecorationMode.unity";
        private const string ContentCatalogPath =
            "Assets/Art/Phase6/Catalogues/FC_Phase6Production.asset";
        private const string ContentCatalogGuid = "f5a3ceba61ecaf949aa330a98e4df68f";
        private const string DecorationCataloguePath =
            "Assets/Art/Phase6/Catalogues/DC_Phase6Decoration.asset";
        private const string DecorationCatalogueGuid = "f3e6bd456ce1cea46821a7f42a21c32a";
        private const string ThemePath =
            "Assets/UI/Phase5/Theme/AnimalCafeUiTheme.asset";
        private const string GridMaterialPath =
            "Assets/Art/Phase4/Environment/Materials/M_Environment_Grid_01.mat";
        private const string CameraSettingsPath =
            "Assets/Config/DefaultCameraSettings.asset";
        private const string DefaultInputActionsGuid =
            "ca9f5fa95ffab41fb9a615ab714db018";

        internal static Func<string, bool> SceneExistsOverrideForTests { get; set; }
        internal static EditorBuildSettingsScene[] BuildSettingsOverrideForTests { get; set; }
        internal static IReadOnlyDictionary<string, string> RuntimeSourceOverrideForTests
        {
            get;
            set;
        }
        internal static string[] MissingScriptPathsOverrideForTests { get; set; }

        public static Phase6DecorationValidationReport ValidateAll()
        {
            var issues = new List<Phase6DecorationValidationIssue>();
            AddMissingSceneIssues(issues);
            AddPersistedSceneIssues(issues, MainCafePath, Phase6SceneSetupTarget.MainCafe);
            AddPersistedSceneIssues(issues, ValidationPath, Phase6SceneSetupTarget.Validation);
            AddBuildSettingsIssues(issues);
            AddRuntimeSourceIssues(issues);
            AddMissingScriptIssues(issues);
            return new Phase6DecorationValidationReport(issues);
        }

        internal static Phase6DecorationValidationReport ValidateCandidateSceneForTests(
            Scene candidate,
            Phase6SceneSetupTarget target)
        {
            if (!candidate.IsValid() || !candidate.isLoaded)
                throw new ArgumentException("Candidate Scene must be valid and loaded.", nameof(candidate));

            return ValidateScene(candidate, target);
        }

        private static void AddPersistedSceneIssues(
            ICollection<Phase6DecorationValidationIssue> issues,
            string scenePath,
            Phase6SceneSetupTarget target)
        {
            var exists = SceneExistsOverrideForTests ?? File.Exists;
            if (!exists(scenePath) || !File.Exists(scenePath))
                return;

            var scene = SceneManager.GetSceneByPath(scenePath);
            var openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                foreach (var issue in ValidateScene(scene, target).Issues)
                    issues.Add(issue);
            }
            finally
            {
                if (openedHere && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Phase6DecorationValidationReport ValidateScene(
            Scene scene,
            Phase6SceneSetupTarget target)
        {
            var issues = new List<Phase6DecorationValidationIssue>();
            var assetPath = target == Phase6SceneSetupTarget.Validation
                ? ValidationPath
                : MainCafePath;
            var transforms = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .ToArray();

            ValidateNamedRoots(transforms, assetPath, target, issues);
            ValidateBaseServices(transforms, assetPath, issues);
            ValidateEnvironment(transforms, assetPath, issues);
            ValidateDecoration(transforms, assetPath, issues);
            ValidateUi(transforms, assetPath, issues);
            if (target == Phase6SceneSetupTarget.Validation)
                ValidateContractReferences(transforms, assetPath, issues);
            ValidateContent(transforms, assetPath, issues);
            ValidateCleanInitialState(transforms, assetPath, issues);
            ValidateMissingScripts(scene, assetPath, issues);
            return new Phase6DecorationValidationReport(issues);
        }

        private static void ValidateNamedRoots(
            Transform[] transforms,
            string assetPath,
            Phase6SceneSetupTarget target,
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            ValidateNamedCount(transforms, "Main Camera",
                Phase6DecorationIssueCode.MissingMainCamera,
                Phase6DecorationIssueCode.DuplicateMainCamera,
                assetPath, "Main Camera", issues);
            ValidateNamedCount(transforms, "Directional Light",
                Phase6DecorationIssueCode.MissingDirectionalLight,
                Phase6DecorationIssueCode.DuplicateDirectionalLight,
                assetPath, "Directional Light", issues);
            ValidateNamedCount(transforms, "Phase0_Runtime",
                Phase6DecorationIssueCode.MissingPhase0Runtime,
                Phase6DecorationIssueCode.DuplicatePhase0Runtime,
                assetPath, "Phase0_Runtime", issues);
            ValidateNamedCount(transforms, "P4_Environment",
                Phase6DecorationIssueCode.MissingEnvironmentRoot,
                Phase6DecorationIssueCode.DuplicateEnvironmentRoot,
                assetPath, "P4_Environment", issues);
            ValidateNamedCount(transforms, "Phase6_DecorationRuntime",
                Phase6DecorationIssueCode.MissingDecorationOwner,
                Phase6DecorationIssueCode.DuplicateDecorationOwner,
                assetPath, "Phase6_DecorationRuntime", issues);
            ValidateNamedCount(transforms, "UI Root",
                Phase6DecorationIssueCode.MissingUiRoot,
                Phase6DecorationIssueCode.DuplicateUiRoot,
                assetPath, "UI Root", issues);
            if (target == Phase6SceneSetupTarget.Validation)
            {
                ValidateNamedCount(transforms, "Phase6_ContractReferences",
                    Phase6DecorationIssueCode.MissingContractReferenceRoot,
                    Phase6DecorationIssueCode.DuplicateContractReferenceRoot,
                    assetPath, "Phase6_ContractReferences", issues);
            }
        }

        private static void ValidateBaseServices(
            Transform[] transforms,
            string assetPath,
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            ValidateService<GameTimeService>(transforms,
                Phase6DecorationIssueCode.MissingGameTimeService,
                Phase6DecorationIssueCode.DuplicateGameTimeService,
                assetPath, issues);
            ValidateService<MouseCameraInput>(transforms,
                Phase6DecorationIssueCode.MissingMouseCameraInput,
                Phase6DecorationIssueCode.DuplicateMouseCameraInput,
                assetPath, issues);
            ValidateService<CafeCameraController>(transforms,
                Phase6DecorationIssueCode.MissingCafeCameraController,
                Phase6DecorationIssueCode.DuplicateCafeCameraController,
                assetPath, issues);
            ValidateService<SceneInteractionController>(transforms,
                Phase6DecorationIssueCode.MissingSceneInteractionController,
                Phase6DecorationIssueCode.DuplicateSceneInteractionController,
                assetPath, issues);

            foreach (var component in transforms.SelectMany(transform =>
                         transform.GetComponents<Component>()).Where(component =>
                         component is GameTimeService
                         || component is MouseCameraInput
                         || component is CafeCameraController
                         || component is SceneInteractionController))
            {
                if (component.gameObject.name != "Phase0_Runtime")
                {
                    issues.Add(Issue(
                        Phase6DecorationIssueCode.RuntimeComponentLocationDrift,
                        assetPath,
                        ObjectPath(component.transform),
                        "Phase 0 runtime service is not owned by Phase0_Runtime."));
                }
            }

            var phase0 = transforms.FirstOrDefault(transform =>
                transform.name == "Phase0_Runtime");
            var cameraController = phase0 != null
                ? phase0.GetComponent<CafeCameraController>()
                : null;
            if (cameraController != null
                && (ReadReference(cameraController, "settings") == null
                    || ReadReference(cameraController, "targetCamera") == null
                    || ReadReference(cameraController, "inputSourceBehaviour") == null))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.CameraSettingsBindingDrift,
                    assetPath,
                    "Phase0_Runtime",
                    "Camera controller settings or input binding drifted."));
            }
        }

        private static void ValidateEnvironment(
            Transform[] transforms,
            string assetPath,
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            var roots = transforms.Where(transform => transform.name == "P4_Environment")
                .ToArray();
            if (roots.Length == 0)
                return;

            var root = roots[0];
            if (root.parent != null || !IsIdentity(root, Vector3.zero))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.EnvironmentTransformDrift,
                    assetPath, "P4_Environment", "Environment root transform drifted."));
            }
            if (!root.GetComponents<Component>().Select(component => component?.GetType())
                    .SequenceEqual(new[] { typeof(Transform) }))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.EnvironmentPrefabDrift,
                    assetPath,
                    "P4_Environment",
                    "Environment root component inventory drifted."));
            }

            var expected = new[]
            {
                ("P4_Floor_8x8", "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Floor_8x8.prefab", Vector3.zero, Quaternion.identity),
                ("P4_Wall_BackLeft", "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Wall_BackLeft_8x3.prefab", new Vector3(0f, .5f, 4f), Quaternion.identity),
                ("P4_Wall_BackRight", "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Wall_BackRight_8x3.prefab", new Vector3(4f, .5f, 0f), Quaternion.Euler(0f, 90f, 0f)),
                ("P4_Entrance", "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Entrance_2x2.prefab", new Vector3(0f, 0f, -4f), Quaternion.identity)
            };
            var expectedDirectChildren = expected.Select(item => item.Item1).ToArray();
            if (!root.Cast<Transform>().Select(child => child.name)
                    .SequenceEqual(expectedDirectChildren))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.EnvironmentPrefabDrift,
                    assetPath,
                    "P4_Environment",
                    "Environment direct-child hierarchy or order drifted."));
            }
            foreach (var child in root.Cast<Transform>().Where(child =>
                         !expectedDirectChildren.Contains(child.name, StringComparer.Ordinal)))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.EnvironmentPrefabDrift,
                    assetPath,
                    ObjectPath(child),
                    "Unexpected environment child is present."));
            }
            foreach (var item in expected)
            {
                var child = root.Find(item.Item1);
                var objectPath = "P4_Environment/" + item.Item1;
                if (child == null)
                {
                    issues.Add(Issue(Phase6DecorationIssueCode.MissingEnvironmentPrefab,
                        assetPath, objectPath, "Required environment Prefab is missing."));
                    continue;
                }

                var expectedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(item.Item2);
                var source = PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject);
                if (expectedAsset == null || source != expectedAsset)
                {
                    issues.Add(Issue(Phase6DecorationIssueCode.EnvironmentPrefabDrift,
                        assetPath, objectPath, "Environment Prefab source drifted."));
                }

                if (!Approximately(child.localPosition, item.Item3)
                    || Quaternion.Angle(child.localRotation, item.Item4) > .01f
                    || !Approximately(child.localScale, Vector3.one))
                {
                    issues.Add(Issue(Phase6DecorationIssueCode.EnvironmentTransformDrift,
                        assetPath, objectPath, "Environment Prefab transform drifted."));
                }
                ValidateEnvironmentPrefabOwnedManifest(child, assetPath, issues);
            }

            var floor = root.Find("P4_Floor_8x8");
            var overlay = floor != null ? floor.Find("GridOverlay") : null;
            if (overlay != null && overlay.gameObject.activeSelf)
            {
                issues.Add(Issue(Phase6DecorationIssueCode.FloorGridOverlayStateDrift,
                    assetPath, "P4_Environment/P4_Floor_8x8/GridOverlay",
                    "Phase 4 GridOverlay must remain disabled."));
            }

            var backRight = root.Find("P4_Wall_BackRight");
            const string windowName = "P4_Window_BackRight_C3_R0";
            const string windowAssetPath =
                "Assets/Art/Phase4/Environment/Prefabs/PF_Environment_Window_01.prefab";
            var window = backRight != null ? backRight.Find(windowName) : null;
            var windowObjectPath =
                "P4_Environment/P4_Wall_BackRight/P4_Window_BackRight_C3_R0";
            if (window == null)
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.MissingEnvironmentPrefab,
                    assetPath,
                    windowObjectPath,
                    "Required environment Window Prefab is missing."));
            }
            else
            {
                var expectedWindow = AssetDatabase.LoadAssetAtPath<GameObject>(windowAssetPath);
                var source = PrefabUtility.GetCorrespondingObjectFromSource(window.gameObject);
                if (expectedWindow == null || source != expectedWindow)
                {
                    issues.Add(Issue(
                        Phase6DecorationIssueCode.EnvironmentPrefabDrift,
                        assetPath,
                        windowObjectPath,
                        "Environment Window Prefab source drifted."));
                }

                if (!IsIdentity(window, new Vector3(-0.5f, 0.5f, -0.061f)))
                {
                    issues.Add(Issue(
                        Phase6DecorationIssueCode.EnvironmentTransformDrift,
                        assetPath,
                        windowObjectPath,
                        "Environment Window transform drifted."));
                }
                ValidateEnvironmentPrefabOwnedManifest(window, assetPath, issues);
            }
        }

        private static void ValidateDecoration(
            Transform[] transforms,
            string assetPath,
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            var owner = transforms.FirstOrDefault(transform =>
                transform.name == "Phase6_DecorationRuntime");
            if (owner == null)
                return;
            if (owner.parent != null || !IsIdentity(owner, Vector3.zero))
            {
                issues.Add(Issue(Phase6DecorationIssueCode.InvalidGridTransform,
                    assetPath, "Phase6_DecorationRuntime",
                    "Decoration runtime root transform drifted."));
            }

            var expectedOwnerComponents = new[]
            {
                typeof(Transform), typeof(CafeLayoutRuntime),
                typeof(DecorationModeController), typeof(FurnitureSceneRegistry),
                typeof(FurniturePreviewView), typeof(GridHighlightView),
                typeof(InputSystemDecorationTouchSource), typeof(DecorationCameraDriver),
                typeof(MouseDecorationInputSource)
            };
            if (!owner.GetComponents<Component>().Select(component => component?.GetType())
                    .SequenceEqual(expectedOwnerComponents))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.InvalidGridTransform,
                    assetPath,
                    "Phase6_DecorationRuntime",
                    "Decoration runtime component inventory drifted. Actual: "
                    + string.Join(", ", owner.GetComponents<Component>()
                        .Select(component => component == null
                            ? "<missing>"
                            : component.GetType().Name))));
            }

            foreach (var child in owner.Cast<Transform>().Where(child =>
                         child.name != "DecorationSpaceRoot"))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.InvalidGridTransform,
                    assetPath,
                    ObjectPath(child),
                    "Unexpected decoration runtime child is present."));
            }

            var space = owner.Find("DecorationSpaceRoot");
            if (space == null)
            {
                issues.Add(Issue(Phase6DecorationIssueCode.MissingGridRoot,
                    assetPath, "Phase6_DecorationRuntime/DecorationSpaceRoot",
                    "Decoration space root is missing."));
                return;
            }
            if (!IsIdentity(space, new Vector3(-4f, 0f, -4f)))
            {
                issues.Add(Issue(Phase6DecorationIssueCode.InvalidGridTransform,
                    assetPath, "Phase6_DecorationRuntime/DecorationSpaceRoot",
                    "Decoration space transform drifted."));
            }

            if (!space.GetComponents<Component>().Select(component => component?.GetType())
                    .SequenceEqual(new[] { typeof(Transform) }))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.InvalidGridTransform,
                    assetPath,
                    ObjectPath(space),
                    "Decoration space component inventory drifted."));
            }

            var expectedSpaceChildren = new[]
            {
                "GridVisualRoot", "FurnitureRepresentationRoot", "FurniturePreviewRoot"
            };
            if (!space.Cast<Transform>().Select(child => child.name)
                    .SequenceEqual(expectedSpaceChildren))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.InvalidGridTransform,
                    assetPath,
                    ObjectPath(space),
                    "Decoration space direct-child hierarchy or order drifted."));
            }
            foreach (var child in space.Cast<Transform>().Where(child =>
                         !expectedSpaceChildren.Contains(child.name, StringComparer.Ordinal)))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.InvalidGridTransform,
                    assetPath,
                    ObjectPath(child),
                    "Unexpected decoration space child is present."));
            }

            foreach (var childName in expectedSpaceChildren)
            {
                var child = space.Find(childName);
                if (child == null)
                    continue;
                if (!IsIdentity(child, Vector3.zero)
                    || !child.GetComponents<Component>().Select(component => component?.GetType())
                        .SequenceEqual(new[] { typeof(Transform) }))
                {
                    issues.Add(Issue(
                        Phase6DecorationIssueCode.InvalidGridTransform,
                        assetPath,
                        ObjectPath(child),
                        "Decoration child transform or component inventory drifted."));
                }
            }

            var grids = transforms.Where(transform => transform.name == "GridVisualRoot")
                .ToArray();
            if (grids.Length == 0)
                issues.Add(Issue(Phase6DecorationIssueCode.MissingGridRoot, assetPath,
                    "Phase6_DecorationRuntime/DecorationSpaceRoot/GridVisualRoot",
                    "Grid visual root is missing."));
            if (grids.Length > 1)
                issues.Add(Issue(Phase6DecorationIssueCode.DuplicateGridRoot, assetPath,
                    "Phase6_DecorationRuntime/DecorationSpaceRoot/GridVisualRoot",
                    "Grid visual root is duplicated."));
            foreach (var grid in grids)
            {
                if (grid.parent != space || !IsIdentity(grid, Vector3.zero))
                {
                    issues.Add(Issue(Phase6DecorationIssueCode.InvalidGridTransform,
                        assetPath,
                        "Phase6_DecorationRuntime/DecorationSpaceRoot/GridVisualRoot",
                        "Grid visual root transform drifted."));
                }
            }

            var layout = owner.GetComponent<CafeLayoutRuntime>();
            var controller = owner.GetComponent<DecorationModeController>();
            if (controller == null)
                return;
            var catalogue = ReadReference(controller, "catalogueAsset");
            if (catalogue == null)
                issues.Add(Issue(Phase6DecorationIssueCode.MissingCatalogueBinding,
                    assetPath, "Phase6_DecorationRuntime", "Decoration catalogue is missing."));
            if (layout != null
                && ReadReference(layout, "contentCatalog") != ReadReference(controller, "contentCatalog"))
            {
                issues.Add(Issue(Phase6DecorationIssueCode.MismatchedCatalogueBinding,
                    assetPath, "Phase6_DecorationRuntime",
                    "Layout and decoration controllers use different content catalogues."));
            }

            foreach (var field in new[]
                     {
                         "catalogueView", "actionBarView", "storeModalView",
                         "decorationModeButtonLabel", "timeControlPanel"
                     })
            {
                if (ReadReference(controller, field) == null)
                {
                    issues.Add(Issue(Phase6DecorationIssueCode.MissingUiReference,
                        assetPath, "Phase6_DecorationRuntime",
                        "Decoration UI reference is missing: " + field + "."));
                }
            }
            if (ReadReference(controller, "decorationModeButton") == null)
            {
                issues.Add(Issue(Phase6DecorationIssueCode.MissingHudToggle,
                    assetPath, "Phase6_DecorationRuntime",
                    "Decoration HUD toggle is missing."));
            }

            ValidateDecorationReferences(
                transforms,
                assetPath,
                owner,
                space,
                layout,
                controller,
                issues);

            var representation = space.Find("FurnitureRepresentationRoot");
            if (representation != null && representation.childCount != 0)
            {
                issues.Add(Issue(Phase6DecorationIssueCode.UnexpectedSerializedRepresentation,
                    assetPath,
                    "Phase6_DecorationRuntime/DecorationSpaceRoot/FurnitureRepresentationRoot",
                    "Serialized furniture representation must start empty."));
            }
        }

        private static void ValidateUi(
            Transform[] transforms,
            string assetPath,
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            var expectedCanvasNames = new[] { "HUD Canvas", "Screen Canvas", "Toast Canvas" };
            var canvases = transforms.Where(transform => transform.GetComponent<Canvas>() != null)
                .ToArray();
            foreach (var name in expectedCanvasNames)
            {
                var named = canvases.Where(canvas => canvas.name == name).ToArray();
                var path = "UI Root/" + name;
                if (named.Length == 0)
                    issues.Add(Issue(Phase6DecorationIssueCode.MissingCanvas,
                        assetPath, path, "Required Canvas is missing."));
                if (named.Length > 1)
                    issues.Add(Issue(Phase6DecorationIssueCode.DuplicateCanvas,
                        assetPath, path, "Canvas is duplicated."));
            }
            foreach (var canvas in canvases.Where(canvas =>
                         !expectedCanvasNames.Contains(canvas.name, StringComparer.Ordinal)))
            {
                issues.Add(Issue(Phase6DecorationIssueCode.UnexpectedCanvas,
                    assetPath, ObjectPath(canvas), "Unexpected Canvas is present."));
            }

            var eventSystems = transforms.Where(transform =>
                transform.GetComponent<EventSystem>() != null).ToArray();
            if (eventSystems.Length == 0)
                issues.Add(Issue(Phase6DecorationIssueCode.MissingEventSystem,
                    assetPath, "UI Root/EventSystem", "EventSystem is missing."));
            if (eventSystems.Length > 1)
                issues.Add(Issue(Phase6DecorationIssueCode.DuplicateEventSystem,
                    assetPath, "UI Root/EventSystem", "EventSystem is duplicated."));

            var uiRoot = transforms.FirstOrDefault(transform => transform.name == "UI Root");
            var eventSystem = eventSystems.FirstOrDefault(transform =>
                transform.name == "EventSystem");
            if (eventSystem != null && eventSystem.parent != uiRoot)
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.MissingEventSystem,
                    assetPath,
                    "UI Root/EventSystem",
                    "EventSystem must be a direct child of UI Root."));
            }

            var modules = transforms.SelectMany(transform =>
                    transform.GetComponents<InputSystemUIInputModule>()).ToArray();
            if (modules.Length == 0)
                issues.Add(Issue(Phase6DecorationIssueCode.MissingInputSystemUiModule,
                    assetPath, "UI Root/EventSystem", "Input System UI module is missing."));
            if (modules.Length > 1)
                issues.Add(Issue(Phase6DecorationIssueCode.DuplicateInputSystemUiModule,
                    assetPath, "UI Root/EventSystem", "Input System UI module is duplicated."));
            if (modules.Any(module => module.actionsAsset == null))
                issues.Add(Issue(Phase6DecorationIssueCode.MissingInputActions,
                    assetPath, "UI Root/EventSystem", "Input actions asset is missing."));
            var eventModule = eventSystem != null
                ? eventSystem.GetComponent<InputSystemUIInputModule>()
                : null;
            if (eventSystem != null && (eventModule == null
                || modules.Length != 1
                || modules[0] != eventModule))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.MissingInputSystemUiModule,
                    assetPath,
                    "UI Root/EventSystem",
                    "The sole Input System UI module must share the EventSystem object."));
            }
            if (eventModule != null)
            {
                var actionPath = AssetDatabase.GetAssetPath(eventModule.actionsAsset);
                var actionGuid = AssetDatabase.AssetPathToGUID(actionPath);
                if (!string.Equals(actionGuid, DefaultInputActionsGuid, StringComparison.Ordinal)
                    || eventModule.point == null
                    || eventModule.leftClick == null
                    || eventModule.scrollWheel == null
                    || eventModule.submit == null
                    || eventModule.cancel == null)
                {
                    issues.Add(Issue(
                        Phase6DecorationIssueCode.MissingInputActions,
                        assetPath,
                        "UI Root/EventSystem",
                        "DefaultInputActions GUID or required UI action references drifted."));
                }
            }
            if (transforms.Any(transform => transform.GetComponent<StandaloneInputModule>() != null))
                issues.Add(Issue(Phase6DecorationIssueCode.UnexpectedStandaloneInputModule,
                    assetPath, "UI Root/EventSystem", "Standalone input module is not allowed."));

            var timePanels = transforms.Where(transform => transform.name == "RightRail")
                .ToArray();
            const string timePath =
                "UI Root/HUD Canvas/HUD Layer/Decoration Safe Area/RightRail";
            if (timePanels.Length == 0)
                issues.Add(Issue(Phase6DecorationIssueCode.MissingTimePanel,
                    assetPath, timePath, "TimePanel is missing."));
            if (timePanels.Length > 1)
                issues.Add(Issue(Phase6DecorationIssueCode.DuplicateTimePanel,
                    assetPath, timePath, "TimePanel is duplicated."));
            if (timePanels.Length == 1
                && !HasExactRightRailContract(timePanels[0]))
            {
                issues.Add(Issue(Phase6DecorationIssueCode.TimeControlWiringDrift,
                    assetPath, timePath,
                    "RightRail order, components, geometry, copy, font, raycast, or bindings drifted."));
            }

            foreach (var uiRootName in new[]
                     {
                         "PF_UI_DecorationCatalogue",
                         "PF_UI_DecorationActionBar",
                         "PF_UI_DecorationStoreModal"
                     })
            {
                var root = transforms.FirstOrDefault(transform =>
                    transform.name == uiRootName);
                if (root == null)
                    continue;
                var group = root.GetComponent<CanvasGroup>();
                if (!root.gameObject.activeSelf
                    || group == null
                    || !Mathf.Approximately(group.alpha, 0f)
                    || group.interactable
                    || group.blocksRaycasts)
                {
                    issues.Add(Issue(
                        Phase6DecorationIssueCode.MissingUiReference,
                        assetPath,
                        ObjectPath(root),
                        "Decoration UI root must stay active and use its CanvasGroup for the closed state."));
                }
                ValidatePrefabOwnedManifest(root, assetPath, issues);
            }

            var safeArea = transforms.FirstOrDefault(transform =>
                transform.name == "Decoration Safe Area");
            if (safeArea != null)
            {
                var safeSource = PrefabUtility.GetCorrespondingObjectFromSource(
                    safeArea.gameObject)?.transform;
                if (safeSource == null
                    || !safeArea.GetComponents<Component>().Select(component => component?.GetType())
                        .SequenceEqual(safeSource.GetComponents<Component>()
                            .Select(component => component?.GetType())))
                {
                    issues.Add(Issue(
                        Phase6DecorationIssueCode.MissingUiReference,
                        assetPath,
                        ObjectPath(safeArea),
                        "Decoration Safe Area Prefab component inventory drifted."));
                }

                foreach (var child in safeArea.Cast<Transform>().Where(child =>
                             child.name != "RightRail"))
                {
                    issues.Add(Issue(
                        Phase6DecorationIssueCode.MissingUiReference,
                        assetPath,
                        ObjectPath(child),
                        "Unexpected Decoration Safe Area child is present."));
                }
                var button = safeArea.Find("RightRail/DecorationModeButton");
                if (button != null)
                    ValidatePrefabOwnedManifest(button, assetPath, issues);
            }
        }

        private static bool HasExactRightRailContract(Transform rail)
        {
            var expectedNames = new[]
            {
                "DecorationModeButton",
                "GameTimeStatusIndicator",
                "PauseButton",
                "NormalButton",
                "FastButton"
            };
            if (rail == null
                || !rail.Cast<Transform>().Select(child => child.name)
                    .SequenceEqual(expectedNames)
                || !HasExactComponents(rail.gameObject,
                    typeof(RectTransform), typeof(TimeControlPanel))
                || !HasExactRect(
                    rail as RectTransform,
                    Vector2.one,
                    Vector2.one,
                    Vector2.one,
                    new Vector2(-24f, -24f),
                    new Vector2(180f, 336f)))
            {
                return false;
            }

            var panel = rail.GetComponent<TimeControlPanel>();
            var gameTime = ReadReference(panel, "gameTimeService");
            var pause = rail.Find("PauseButton")?.GetComponent<Button>();
            var normal = rail.Find("NormalButton")?.GetComponent<Button>();
            var fast = rail.Find("FastButton")?.GetComponent<Button>();
            var pauseSelected = pause?.transform.Find("SelectedVisual")?.gameObject;
            var normalSelected = normal?.transform.Find("SelectedVisual")?.gameObject;
            var fastSelected = fast?.transform.Find("SelectedVisual")?.gameObject;
            if (panel == null
                || gameTime == null
                || ReadReference(panel, "pauseButton") != pause
                || ReadReference(panel, "normalButton") != normal
                || ReadReference(panel, "fastButton") != fast
                || ReadReference(panel, "pauseSelectedVisual") != pauseSelected
                || ReadReference(panel, "normalSelectedVisual") != normalSelected
                || ReadReference(panel, "fastSelectedVisual") != fastSelected)
            {
                return false;
            }

            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>(ThemePath);
            if (theme == null
                || !HasExactTimeButton(pause, "Pause", -128f, pauseSelected, false, theme)
                || !HasExactTimeButton(normal, "1x", -192f, normalSelected, true, theme)
                || !HasExactTimeButton(fast, "2x", -256f, fastSelected, false, theme))
            {
                return false;
            }

            var indicatorTransform = rail.Find("GameTimeStatusIndicator");
            var indicator = indicatorTransform?.GetComponent<GameTimeStatusIndicator>();
            var indicatorImage = indicatorTransform?.GetComponent<Image>();
            var rotating = indicatorTransform?.Find("RotatingVisual") as RectTransform;
            var rotatingImage = rotating?.GetComponent<Image>();
            return indicatorTransform != null
                && HasExactComponents(indicatorTransform.gameObject,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                    typeof(GameTimeStatusIndicator))
                && HasExactRect(
                    indicatorTransform as RectTransform,
                    Vector2.one,
                    Vector2.one,
                    Vector2.one,
                    new Vector2(0f, -64f),
                    new Vector2(180f, 56f))
                && indicatorImage != null
                && !indicatorImage.raycastTarget
                && rotating != null
                && HasExactComponents(rotating.gameObject,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                && HasExactRect(
                    rotating,
                    Vector2.one * .5f,
                    Vector2.one * .5f,
                    Vector2.one * .5f,
                    Vector2.zero,
                    new Vector2(36f, 12f))
                && rotatingImage != null
                && !rotatingImage.raycastTarget
                && ReadReference(indicator, "gameTimeService") == gameTime
                && ReadReference(indicator, "rotatingVisual") == rotating;
        }

        private static bool HasExactTimeButton(
            Button button,
            string expectedText,
            float expectedY,
            GameObject selectedVisual,
            bool initiallySelected,
            AnimalCafeUiTheme theme)
        {
            if (button == null
                || !HasExactComponents(button.gameObject,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                    typeof(Button), typeof(Shadow), typeof(AnimalCafeButtonView))
                || !HasExactRect(
                    button.transform as RectTransform,
                    Vector2.one,
                    Vector2.one,
                    Vector2.one,
                    new Vector2(0f, expectedY),
                    new Vector2(180f, 56f))
                || !button.transform.Cast<Transform>().Select(child => child.name)
                    .SequenceEqual(new[] { "Label", "SelectedVisual" }))
            {
                return false;
            }

            var image = button.GetComponent<Image>();
            var label = button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            var labelTransform = label?.transform as RectTransform;
            var selectedRect = selectedVisual != null
                ? selectedVisual.transform as RectTransform
                : null;
            var selectedImage = selectedVisual != null
                ? selectedVisual.GetComponent<Image>()
                : null;
            return image != null
                && image.raycastTarget
                && button.targetGraphic == image
                && label != null
                && HasExactComponents(label.gameObject,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI))
                && label.text == expectedText
                && label.alignment == TextAlignmentOptions.Center
                && label.font == theme.Typography.Label.FontAsset
                && Mathf.Approximately(label.fontSize, theme.Typography.Label.FontSize)
                && !label.raycastTarget
                && labelTransform != null
                && Approximately(labelTransform.anchorMin, Vector2.zero)
                && Approximately(labelTransform.anchorMax, Vector2.one)
                && Approximately(labelTransform.offsetMin, Vector2.zero)
                && Approximately(labelTransform.offsetMax, Vector2.zero)
                && selectedVisual != null
                && selectedVisual.activeSelf == initiallySelected
                && HasExactComponents(selectedVisual,
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image))
                && HasExactRect(
                    selectedRect,
                    new Vector2(0f, 0f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, .5f),
                    new Vector2(10f, 0f),
                    new Vector2(12f, -16f))
                && selectedImage != null
                && !selectedImage.raycastTarget;
        }

        private static bool HasExactComponents(GameObject owner, params Type[] expected)
        {
            return owner != null
                && owner.GetComponents<Component>()
                    .Select(component => component?.GetType())
                    .SequenceEqual(expected);
        }

        private static bool HasExactRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 sizeDelta)
        {
            return rect != null
                && Approximately(rect.anchorMin, anchorMin)
                && Approximately(rect.anchorMax, anchorMax)
                && Approximately(rect.pivot, pivot)
                && Approximately(rect.anchoredPosition, anchoredPosition)
                && Approximately(rect.sizeDelta, sizeDelta)
                && Quaternion.Angle(rect.localRotation, Quaternion.identity) <= .01f
                && Approximately(rect.localScale, Vector3.one);
        }

        private static void ValidateEnvironmentPrefabOwnedManifest(
            Transform instanceRoot,
            string assetPath,
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            var transforms = instanceRoot.GetComponentsInChildren<Transform>(true);
            foreach (var transform in transforms)
            {
                var sourceObject = PrefabUtility.GetCorrespondingObjectFromSource(
                    transform.gameObject);
                if (sourceObject == null)
                {
                    issues.Add(Issue(
                        Phase6DecorationIssueCode.EnvironmentPrefabDrift,
                        assetPath,
                        ObjectPath(transform),
                        "Unexpected environment Prefab-owned child is present."));
                    continue;
                }

                foreach (var component in transform.GetComponents<Component>())
                {
                    if (component == null
                        || PrefabUtility.GetCorrespondingObjectFromSource(component) != null
                        || IsAllowedEnvironmentAddedComponent(instanceRoot, transform, component))
                    {
                        continue;
                    }
                    issues.Add(Issue(
                        Phase6DecorationIssueCode.EnvironmentPrefabDrift,
                        assetPath,
                        ObjectPath(transform),
                        "Unexpected environment Prefab-owned component is present."));
                }

                if (transform == instanceRoot
                    || PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject))
                {
                    continue;
                }
                var sourceTransform = PrefabUtility.GetCorrespondingObjectFromSource(transform);
                if (sourceTransform != null
                    && (!Approximately(transform.localPosition, sourceTransform.localPosition)
                        || Quaternion.Angle(
                            transform.localRotation,
                            sourceTransform.localRotation) > .01f
                        || !Approximately(transform.localScale, sourceTransform.localScale)
                        || (transform.name != "GridOverlay"
                            && transform.gameObject.activeSelf
                            != sourceTransform.gameObject.activeSelf)))
                {
                    issues.Add(Issue(
                        Phase6DecorationIssueCode.EnvironmentPrefabDrift,
                        assetPath,
                        ObjectPath(transform),
                        "Environment Prefab-owned child transform override drifted."));
                }
            }

            foreach (var modification in PrefabUtility.GetPropertyModifications(
                         instanceRoot.gameObject) ?? Array.Empty<PropertyModification>())
            {
                if (IsAllowedEnvironmentModification(instanceRoot, modification))
                    continue;
                var modifiedTransform = FindInstanceTransformForSource(
                    instanceRoot,
                    modification.target) ?? instanceRoot;
                issues.Add(Issue(
                    Phase6DecorationIssueCode.EnvironmentPrefabDrift,
                    assetPath,
                    ObjectPath(modifiedTransform),
                    "Unexpected environment Prefab property override: "
                    + modification.propertyPath + "."));
            }
        }

        private static bool IsAllowedEnvironmentAddedComponent(
            Transform instanceRoot,
            Transform owner,
            Component component) =>
            owner == instanceRoot
            && ((instanceRoot.name.StartsWith("P4_Wall_", StringComparison.Ordinal)
                    && component is WallSurfaceAuthoring)
                || (instanceRoot.name == "P4_Entrance"
                    && component is EntrancePortalAuthoring));

        private static bool IsAllowedEnvironmentModification(
            Transform instanceRoot,
            PropertyModification modification)
        {
            var rootSource = PrefabUtility.GetCorrespondingObjectFromSource(
                instanceRoot.gameObject);
            var rootSourceTransform = rootSource != null ? rootSource.transform : null;
            if (modification.target == rootSource
                && modification.propertyPath == "m_Name")
            {
                return true;
            }
            if (modification.target == rootSourceTransform
                && (modification.propertyPath.StartsWith("m_LocalPosition.", StringComparison.Ordinal)
                    || modification.propertyPath.StartsWith("m_LocalRotation.", StringComparison.Ordinal)
                    || modification.propertyPath.StartsWith("m_LocalScale.", StringComparison.Ordinal)
                    || modification.propertyPath.StartsWith("m_LocalEulerAnglesHint.", StringComparison.Ordinal)
                    || modification.propertyPath == "m_RootOrder"
                    || modification.propertyPath == "m_ConstrainProportionsScale"))
            {
                return true;
            }

            var modifiedTransform = FindInstanceTransformForSource(
                instanceRoot,
                modification.target);
            return modifiedTransform != null
                && modifiedTransform.name == "GridOverlay"
                && modification.propertyPath == "m_IsActive";
        }

        private static Transform FindInstanceTransformForSource(
            Transform instanceRoot,
            UnityEngine.Object source)
        {
            if (source == null)
                return null;
            foreach (var transform in instanceRoot.GetComponentsInChildren<Transform>(true))
            {
                if (source is GameObject
                    && PrefabUtility.GetCorrespondingObjectFromSource(transform.gameObject) == source)
                {
                    return transform;
                }
                if (source is Component sourceComponent
                    && transform.GetComponents(sourceComponent.GetType()).Any(component =>
                        PrefabUtility.GetCorrespondingObjectFromSource(component) == source))
                {
                    return transform;
                }
            }
            return null;
        }

        private static void ValidateDecorationReferences(
            Transform[] transforms,
            string assetPath,
            Transform owner,
            Transform space,
            CafeLayoutRuntime layout,
            DecorationModeController controller,
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            var canonicalContent = AssetDatabase.LoadAssetAtPath<FurnitureContentCatalog>(
                ContentCatalogPath);
            var canonicalCatalogue = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                DecorationCataloguePath);
            var expectedEntrance = transforms.FirstOrDefault(transform =>
                transform.name == "P4_Entrance")?.GetComponent<EntrancePortalAuthoring>();
            if (layout == null
                || ReadReference(layout, "contentCatalog") != canonicalContent
                || ReadReference(layout, "entrancePortal") != expectedEntrance)
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.MismatchedCatalogueBinding,
                    assetPath,
                    "Phase6_DecorationRuntime",
                    "CafeLayoutRuntime canonical content or entrance binding drifted."));
            }

            if (controller == null)
                return;
            var expected = new (string Field, UnityEngine.Object Value)[]
            {
                ("contentCatalog", canonicalContent),
                ("catalogueAsset", canonicalCatalogue),
                ("cameraSettings", AssetDatabase.LoadAssetAtPath<CameraSettings>(CameraSettingsPath)),
                ("layoutRuntime", layout),
                ("targetCamera", transforms.FirstOrDefault(transform => transform.name == "Main Camera")
                    ?.GetComponent<UnityEngine.Camera>()),
                ("cameraController", transforms.FirstOrDefault(transform => transform.name == "Phase0_Runtime")
                    ?.GetComponent<CafeCameraController>()),
                ("sceneInteraction", transforms.FirstOrDefault(transform => transform.name == "Phase0_Runtime")
                    ?.GetComponent<SceneInteractionController>()),
                ("floorCollider", transforms.FirstOrDefault(transform => transform.name == "P4_Floor_8x8")
                    ?.GetComponentInChildren<Collider>(true)),
                ("gridRoot", space),
                ("furnitureRepresentationRoot", space?.Find("FurnitureRepresentationRoot")),
                ("furniturePreviewRoot", space?.Find("FurniturePreviewRoot")),
                ("gridVisualRoot", space?.Find("GridVisualRoot")),
                ("gridMaterialTemplate", AssetDatabase.LoadAssetAtPath<Material>(GridMaterialPath)),
                ("uiTheme", AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ThemePath)),
                ("sceneRegistry", owner.GetComponent<FurnitureSceneRegistry>()),
                ("previewView", owner.GetComponent<FurniturePreviewView>()),
                ("gridView", owner.GetComponent<GridHighlightView>()),
                ("cameraDriver", owner.GetComponent<DecorationCameraDriver>()),
                ("catalogueView", transforms.FirstOrDefault(transform =>
                    transform.name == "PF_UI_DecorationCatalogue")?.GetComponent<DecorationCatalogueView>()),
                ("actionBarView", transforms.FirstOrDefault(transform =>
                    transform.name == "PF_UI_DecorationActionBar")?.GetComponent<DecorationActionBarView>()),
                ("storeModalView", transforms.FirstOrDefault(transform =>
                    transform.name == "PF_UI_DecorationStoreModal")?.GetComponent<DecorationStoreModalView>()),
                ("decorationModeButton", transforms.FirstOrDefault(transform =>
                    transform.name == "DecorationModeButton")?.GetComponent<Button>()),
                ("decorationModeButtonLabel", transforms.FirstOrDefault(transform =>
                    transform.name == "DecorationModeButton")?.GetComponentInChildren<TMP_Text>(true)),
                ("timeControlPanel", transforms.FirstOrDefault(transform =>
                    transform.name == "RightRail")?.GetComponent<TimeControlPanel>()),
                ("gameTimeServiceBehaviour", transforms.FirstOrDefault(transform =>
                    transform.name == "Phase0_Runtime")?.GetComponent<GameTimeService>()),
                ("touchSourceBehaviour", owner.GetComponent<InputSystemDecorationTouchSource>()),
                ("mouseSourceBehaviour", owner.GetComponent<MouseDecorationInputSource>())
            };
            foreach (var item in expected)
            {
                if (ReadReference(controller, item.Field) == item.Value)
                    continue;
                var code = item.Field is "contentCatalog" or "catalogueAsset"
                    ? Phase6DecorationIssueCode.MismatchedCatalogueBinding
                    : Phase6DecorationIssueCode.MissingUiReference;
                issues.Add(Issue(
                    code,
                    assetPath,
                    "Phase6_DecorationRuntime",
                    "Canonical decoration binding drifted: " + item.Field + "."));
            }
        }

        private static void ValidatePrefabOwnedManifest(
            Transform instanceRoot,
            string assetPath,
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            var sourceRoot = PrefabUtility.GetCorrespondingObjectFromSource(
                instanceRoot.gameObject)?.transform;
            if (sourceRoot == null)
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.MissingUiReference,
                    assetPath,
                    ObjectPath(instanceRoot),
                    "Decoration UI root is not the canonical Prefab instance."));
                return;
            }

            ValidatePrefabOwnedNode(instanceRoot, sourceRoot, assetPath, issues, true);
        }

        private static void ValidatePrefabOwnedNode(
            Transform instance,
            Transform source,
            string assetPath,
            ICollection<Phase6DecorationValidationIssue> issues,
            bool isRoot)
        {
            var instanceChildren = instance.Cast<Transform>().ToArray();
            var sourceChildren = source.Cast<Transform>().ToArray();
            var sourceNames = sourceChildren.Select(child => child.name).ToArray();
            foreach (var extra in instanceChildren.Where(child =>
                         !sourceNames.Contains(child.name, StringComparer.Ordinal)))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.MissingUiReference,
                    assetPath,
                    ObjectPath(extra),
                    "Unexpected Task 6 Prefab-owned UI child is present."));
            }

            var instanceComponentTypes = instance.GetComponents<Component>()
                .Select(component => component?.GetType()).ToArray();
            var sourceComponentTypes = source.GetComponents<Component>()
                .Select(component => component?.GetType()).ToArray();
            if (!instanceComponentTypes.SequenceEqual(sourceComponentTypes))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.MissingUiReference,
                    assetPath,
                    ObjectPath(instance),
                    "Task 6 Prefab-owned UI component inventory drifted."));
            }

            var transformDrift = !isRoot && HasPrefabOwnedTransformDrift(instance, source);
            if (!isRoot
                && (instance.gameObject.activeSelf != source.gameObject.activeSelf
                    || transformDrift))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.MissingUiReference,
                    assetPath,
                    ObjectPath(instance),
                    "Task 6 Prefab-owned UI state or transform drifted."));
            }

            foreach (var sourceChild in sourceChildren)
            {
                var instanceChild = instance.Find(sourceChild.name);
                if (instanceChild == null)
                {
                    issues.Add(Issue(
                        Phase6DecorationIssueCode.MissingUiReference,
                        assetPath,
                        ObjectPath(instance) + "/" + sourceChild.name,
                        "Required Task 6 Prefab-owned UI child is missing."));
                    continue;
                }
                ValidatePrefabOwnedNode(instanceChild, sourceChild, assetPath, issues, false);
            }
        }

        private static bool HasPrefabOwnedTransformDrift(
            Transform instance,
            Transform source)
        {
            if (instance is RectTransform instanceRect
                && source is RectTransform sourceRect)
            {
                return !Approximately(instanceRect.anchorMin, sourceRect.anchorMin)
                    || !Approximately(instanceRect.anchorMax, sourceRect.anchorMax)
                    || !Approximately(instanceRect.pivot, sourceRect.pivot)
                    || !Approximately(
                        instanceRect.anchoredPosition,
                        sourceRect.anchoredPosition)
                    || !Approximately(instanceRect.sizeDelta, sourceRect.sizeDelta)
                    || Quaternion.Angle(instance.localRotation, source.localRotation) > .01f
                    || !Approximately(instance.localScale, source.localScale);
            }

            return !Approximately(instance.localPosition, source.localPosition)
                || Quaternion.Angle(instance.localRotation, source.localRotation) > .01f
                || !Approximately(instance.localScale, source.localScale);
        }

        private static void ValidateContractReferences(
            Transform[] transforms,
            string assetPath,
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            var root = transforms.FirstOrDefault(transform =>
                transform.name == "Phase6_ContractReferences");
            if (root == null)
                return;
            var expected = new[]
            {
                ("BlockedArea_ReferenceOnly", new Vector3(4.25f, .05f, -.5f), "Blocked - Reference Only", new Vector2(1f, 0f)),
                ("LockedArea_ReferenceOnly", new Vector3(4.25f, .05f, 1.5f), "Locked - Reference Only", new Vector2(.75f, 0f))
            };
            var camera = transforms.FirstOrDefault(transform =>
                transform.name == "Main Camera");
            foreach (var item in expected)
            {
                var child = root.Find(item.Item1);
                var path = "Phase6_ContractReferences/" + item.Item1;
                var text = child != null ? child.GetComponent<TextMeshPro>() : null;
                if (child == null
                    || !Approximately(child.localPosition, item.Item2)
                    || !Approximately(child.localScale, Vector3.one)
                    || text == null
                    || text.text != item.Item3
                    || text.alignment != TextAlignmentOptions.BottomLeft
                    || !Mathf.Approximately(text.fontSize, 1.5f)
                    || (child.GetComponent<RectTransform>().sizeDelta
                        - new Vector2(4f, 0.6f)).sqrMagnitude > 0.000001f
                    || (child.GetComponent<RectTransform>().pivot
                        - item.Item4).sqrMagnitude > 0.000001f
                    || (camera != null && Vector3.Dot(
                        -child.forward,
                        (camera.position - child.position).normalized) < 0.999f))
                {
                    issues.Add(Issue(Phase6DecorationIssueCode.ContractReferenceDrift,
                        assetPath, path,
                        "Reference-only contract object drifted: exists=" + (child != null)
                        + ", position=" + (child != null ? child.localPosition.ToString() : "<missing>")
                        + ", scale=" + (child != null ? child.localScale.ToString() : "<missing>")
                        + ", text='" + (text != null ? text.text : "<missing>") + "'."));
                }
                if (child != null && child.GetComponents<Component>().Any(component =>
                        component != null
                        && component is not Transform
                        && component is not TextMeshPro
                        && component is not MeshRenderer
                        && component is not MeshFilter))
                {
                    issues.Add(Issue(
                        Phase6DecorationIssueCode.ContractReferenceGameplayBinding,
                        assetPath, path,
                        "Reference-only contract object contains gameplay binding."));
                }
            }
            if (root.childCount != 2)
                issues.Add(Issue(Phase6DecorationIssueCode.ContractReferenceDrift,
                    assetPath, "Phase6_ContractReferences",
                    "Reference-only root inventory drifted."));
        }

        private static void ValidateContent(
            Transform[] transforms,
            string sceneAssetPath,
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            var controller = transforms.Select(transform =>
                    transform.GetComponent<DecorationModeController>())
                .FirstOrDefault(component => component != null);
            var decorationCatalogue = controller != null
                ? ReadReference(controller, "catalogueAsset") as DecorationCatalogueAsset
                : null;
            var canonicalDecoration = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                DecorationCataloguePath);
            if (decorationCatalogue != null
                && (!IsCanonicalAsset(
                        decorationCatalogue,
                        canonicalDecoration,
                        DecorationCataloguePath,
                        DecorationCatalogueGuid)))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.MismatchedCatalogueBinding,
                    sceneAssetPath,
                    "Phase6_DecorationRuntime",
                    "Decoration catalogue path, GUID, or object identity drifted."));
            }
            if (decorationCatalogue != null)
            {
                for (var index = 0; index < decorationCatalogue.Entries.Count; index++)
                {
                    var entry = decorationCatalogue.Entries[index];
                    var cataloguePath = AssetDatabase.GetAssetPath(decorationCatalogue);
                    if (entry.Definition == null)
                        issues.Add(Issue(Phase6DecorationIssueCode.MissingDefinition,
                            cataloguePath, $"entries[{index}].definition",
                            "Decoration catalogue entry has no definition."));
                    if (entry.Thumbnail == null)
                        issues.Add(Issue(Phase6DecorationIssueCode.MissingThumbnail,
                            cataloguePath, $"entries[{index}].thumbnail",
                            "Decoration catalogue entry has no thumbnail."));
                }
                ValidateDecorationCatalogueEntries(decorationCatalogue, issues);
            }

            var layout = transforms.Select(transform => transform.GetComponent<CafeLayoutRuntime>())
                .FirstOrDefault(component => component != null);
            var content = layout != null
                ? ReadReference(layout, "contentCatalog") as FurnitureContentCatalog
                : null;
            if (content == null)
                return;
            var canonicalContent = AssetDatabase.LoadAssetAtPath<FurnitureContentCatalog>(
                ContentCatalogPath);
            if (!IsCanonicalAsset(
                    content,
                    canonicalContent,
                    ContentCatalogPath,
                    ContentCatalogGuid))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.MismatchedCatalogueBinding,
                    sceneAssetPath,
                    "Phase6_DecorationRuntime",
                    "Furniture content catalogue path, GUID, or object identity drifted."));
            }
            var serialized = new SerializedObject(content);
            var entries = serialized.FindProperty("entries");
            for (var index = 0; index < entries.arraySize; index++)
            {
                var definition = entries.GetArrayElementAtIndex(index).objectReferenceValue
                    as FurnitureDefinitionAsset;
                if (definition != null && definition.Prefab == null)
                {
                    issues.Add(Issue(Phase6DecorationIssueCode.MissingPrefab,
                        AssetDatabase.GetAssetPath(definition), "prefab",
                        "Furniture definition has no Prefab."));
                }
            }
        }

        private static void ValidateDecorationCatalogueEntries(
            DecorationCatalogueAsset catalogue,
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            var definitionPaths = new[]
            {
                "Assets/Art/Phase4/Definitions/FD_Furniture_CounterModule_01.asset",
                "Assets/Art/Phase6/Definitions/FD_CounterPreset_1x2.asset",
                "Assets/Art/Phase6/Definitions/FD_CounterPreset_1x3.asset",
                "Assets/Art/Phase6/Definitions/FD_CounterPreset_2x3.asset"
            };
            var thumbnailPaths = new[]
            {
                "Assets/UI/Phase6/Thumbnails/TH_CounterPreset_1x1.png",
                "Assets/UI/Phase6/Thumbnails/TH_CounterPreset_1x2.png",
                "Assets/UI/Phase6/Thumbnails/TH_CounterPreset_1x3.png",
                "Assets/UI/Phase6/Thumbnails/TH_CounterPreset_2x3.png"
            };
            var cataloguePath = AssetDatabase.GetAssetPath(catalogue);
            var exact = catalogue.Entries.Count == definitionPaths.Length;
            for (var index = 0; index < Math.Min(
                         catalogue.Entries.Count,
                         definitionPaths.Length); index++)
            {
                var entry = catalogue.Entries[index];
                exact &= entry != null
                    && entry.Definition == AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(
                        definitionPaths[index])
                    && entry.Thumbnail == AssetDatabase.LoadAssetAtPath<Sprite>(
                        thumbnailPaths[index]);
            }
            if (!exact)
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.MismatchedCatalogueBinding,
                    cataloguePath,
                    "entries",
                    "Decoration catalogue accepted entries, order, or content drifted."));
            }
        }

        private static bool IsCanonicalAsset(
            UnityEngine.Object actual,
            UnityEngine.Object canonical,
            string expectedPath,
            string expectedGuid)
        {
            var actualPath = AssetDatabase.GetAssetPath(actual);
            return actual == canonical
                && string.Equals(actualPath, expectedPath, StringComparison.Ordinal)
                && string.Equals(
                    AssetDatabase.AssetPathToGUID(actualPath),
                    expectedGuid,
                    StringComparison.Ordinal);
        }

        private static void ValidateCleanInitialState(
            Transform[] transforms,
            string assetPath,
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            foreach (var transform in transforms.Where(transform =>
                         transform.name.StartsWith("TEMP_", StringComparison.Ordinal)))
            {
                issues.Add(Issue(Phase6DecorationIssueCode.TemporaryFixturePresent,
                    assetPath, ObjectPath(transform), "Temporary fixture is present."));
            }
            foreach (var transform in transforms.Where(transform =>
                         transform.name == "PF_Furniture_WorkTable_01"))
            {
                issues.Add(Issue(Phase6DecorationIssueCode.UnexpectedInitialContent,
                    assetPath, ObjectPath(transform), "Unexpected initial furniture is present."));
            }
        }

        private static void ValidateMissingScripts(
            Scene scene,
            string assetPath,
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                    issues.Add(Issue(Phase6DecorationIssueCode.MissingScript,
                        assetPath, ObjectPath(transform), "Serialized object contains a missing script."));
            }
        }

        private static void ValidateService<T>(
            Transform[] transforms,
            Phase6DecorationIssueCode missing,
            Phase6DecorationIssueCode duplicate,
            string assetPath,
            ICollection<Phase6DecorationValidationIssue> issues)
            where T : Component
        {
            var components = transforms.SelectMany(transform => transform.GetComponents<T>())
                .ToArray();
            if (components.Length == 0)
                issues.Add(Issue(missing, assetPath, "Phase0_Runtime",
                    typeof(T).Name + " is missing."));
            if (components.Length > 1)
            {
                foreach (var path in components.Select(component => ObjectPath(component.transform))
                             .Distinct(StringComparer.Ordinal))
                    issues.Add(Issue(duplicate, assetPath, path,
                        typeof(T).Name + " is duplicated."));
            }
        }

        private static void ValidateNamedCount(
            Transform[] transforms,
            string name,
            Phase6DecorationIssueCode missing,
            Phase6DecorationIssueCode duplicate,
            string assetPath,
            string objectPath,
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            var count = transforms.Count(transform => transform.name == name);
            if (count == 0)
                issues.Add(Issue(missing, assetPath, objectPath, name + " is missing."));
            if (count > 1)
                issues.Add(Issue(duplicate, assetPath, objectPath, name + " is duplicated."));
        }

        private static UnityEngine.Object ReadReference(
            UnityEngine.Object owner,
            string propertyName)
        {
            if (owner == null)
                return null;
            var property = new SerializedObject(owner).FindProperty(propertyName);
            return property?.objectReferenceValue;
        }

        private static bool IsIdentity(Transform transform, Vector3 localPosition) =>
            Approximately(transform.localPosition, localPosition)
            && Quaternion.Angle(transform.localRotation, Quaternion.identity) <= .01f
            && Approximately(transform.localScale, Vector3.one);

        private static bool Approximately(Vector3 left, Vector3 right) =>
            (left - right).sqrMagnitude <= .000001f;

        private static bool Approximately(Vector2 left, Vector2 right) =>
            (left - right).sqrMagnitude <= .000001f;

        private static string ObjectPath(Transform transform)
        {
            var names = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
                names.Push(current.name);
            return string.Join("/", names);
        }

        private static void AddMissingSceneIssues(
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            var exists = SceneExistsOverrideForTests ?? File.Exists;
            if (!exists(MainCafePath))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.MissingMainCafeScene,
                    MainCafePath,
                    string.Empty,
                    "MainCafe Scene is missing."));
            }

            if (!exists(ValidationPath))
            {
                issues.Add(Issue(
                    Phase6DecorationIssueCode.MissingValidationScene,
                    ValidationPath,
                    string.Empty,
                    "Phase 6 validation Scene is missing."));
            }
        }

        private static void AddBuildSettingsIssues(
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            var entries = BuildSettingsOverrideForTests ?? EditorBuildSettings.scenes;
            var enabled = entries.Where(entry => entry.enabled).ToArray();
            if (enabled.Length == 1
                && string.Equals(enabled[0].path, MainCafePath, StringComparison.Ordinal)
                && entries.All(entry => !string.Equals(
                    entry.path,
                    ValidationPath,
                    StringComparison.Ordinal)))
            {
                return;
            }

            issues.Add(Issue(
                Phase6DecorationIssueCode.BuildSettingsScopeDrift,
                "ProjectSettings/EditorBuildSettings.asset",
                string.Empty,
                "MainCafe must be the sole enabled Scene and Phase 6 validation must be absent."));
        }

        private static void AddRuntimeSourceIssues(
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            foreach (var pair in RuntimeSources())
            {
                if (Regex.IsMatch(pair.Value, @"\bUnityEditor\b", RegexOptions.CultureInvariant))
                {
                    issues.Add(Issue(
                        Phase6DecorationIssueCode.RuntimeEditorReference,
                        pair.Key,
                        string.Empty,
                        "Runtime source references UnityEditor."));
                }

                if (Regex.IsMatch(
                        pair.Value,
                        @"\b(?:File\s*\.\s*(?:WriteAllText|WriteAllBytes|Create|OpenWrite)|PlayerPrefs\s*\.|JsonUtility\s*\.\s*ToJson)\b",
                        RegexOptions.CultureInvariant))
                {
                    issues.Add(Issue(
                        Phase6DecorationIssueCode.SaveBoundaryViolation,
                        pair.Key,
                        string.Empty,
                        "Runtime source references a persistence writer API."));
                }
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> RuntimeSources()
        {
            if (RuntimeSourceOverrideForTests != null)
            {
                return RuntimeSourceOverrideForTests;
            }

            var roots = new[]
            {
                "Assets/Scripts/Decoration",
                "Assets/Scripts/UI/Decoration"
            };
            var paths = roots.Where(Directory.Exists)
                .SelectMany(root => Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                .Concat(new[]
                {
                    "Assets/Scripts/Interaction/SceneInteractionController.cs"
                })
                .Where(File.Exists)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal);
            return paths.Select(path =>
                new KeyValuePair<string, string>(path.Replace('\\', '/'), File.ReadAllText(path)));
        }

        private static void AddMissingScriptIssues(
            ICollection<Phase6DecorationValidationIssue> issues)
        {
            foreach (var encoded in MissingScriptPathsOverrideForTests ?? Array.Empty<string>())
            {
                var separator = encoded.IndexOf('|');
                var assetPath = separator >= 0 ? encoded.Substring(0, separator) : encoded;
                var objectPath = separator >= 0 ? encoded.Substring(separator + 1) : string.Empty;
                issues.Add(Issue(
                    Phase6DecorationIssueCode.MissingScript,
                    assetPath,
                    objectPath,
                    "Serialized object contains a missing script."));
            }
        }

        private static Phase6DecorationValidationIssue Issue(
            Phase6DecorationIssueCode code,
            string assetPath,
            string objectPath,
            string message) =>
            new Phase6DecorationValidationIssue(code, assetPath, objectPath, message);
    }
}

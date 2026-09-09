using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.Phase8
{
    public sealed class Phase8ValidationIssue
    {
        public Phase8ValidationIssue(string code, string message)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
            Message = message ?? throw new ArgumentNullException(nameof(message));
        }

        public string Code { get; }
        public string Message { get; }
    }

    public sealed class Phase8ValidationReport
    {
        public Phase8ValidationReport(IEnumerable<Phase8ValidationIssue> issues)
        {
            Issues = Array.AsReadOnly((issues ?? throw new ArgumentNullException(nameof(issues)))
                .OrderBy(issue => issue.Code, StringComparer.Ordinal)
                .ThenBy(issue => issue.Message, StringComparer.Ordinal)
                .ToArray());
        }

        public IReadOnlyList<Phase8ValidationIssue> Issues { get; }
    }

    public static class Phase8Validator
    {
        [MenuItem("AnimalCafe/Phase 8/Validate Open Scene")]
        public static Phase8ValidationReport ValidateOpenScene()
        {
            var issues = new List<Phase8ValidationIssue>();
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                issues.Add(new Phase8ValidationIssue(
                    "P8-SCENE-MISSING", "No valid loaded active Scene."));
                return new Phase8ValidationReport(issues);
            }

            ValidateAssets(issues);
            ValidateScene(scene, issues);
            var report = new Phase8ValidationReport(issues);
            if (report.Issues.Count == 0)
            {
                Debug.Log($"Phase 8 validation passed for '{scene.path}'.");
            }
            else
            {
                Debug.LogWarning($"Phase 8 validation found {report.Issues.Count} issue(s) in '{scene.path}'.");
            }
            return report;
        }

        private static void ValidateAssets(ICollection<Phase8ValidationIssue> issues)
        {
            try
            {
                var catalogue = RequireAsset<DecorationCatalogueAsset>(
                    Phase8AssetPaths.FurnitureCataloguePath);
                var rows = DecorationCatalogueModelBuilder.BuildFurnitureTab(catalogue);
                if (!rows.Select(row => row.CategoryId).SequenceEqual(new[]
                    {
                        "furniture", "cash-register", "coffee-machine"
                    })
                    || !rows.Select(row => row.Items.Count).SequenceEqual(new[] { 4, 1, 1 }))
                {
                    issues.Add(new Phase8ValidationIssue(
                        "P8-ASSET-CATALOGUE",
                        Phase8AssetPaths.FurnitureCataloguePath));
                }

                ValidateProductionDefinition(
                    catalogue,
                    "equipment.cash-register.01",
                    FurnitureFunctionType.CashRegister,
                    Phase8AssetPaths.CashRegisterDefinitionPath,
                    issues);
                ValidateProductionDefinition(
                    catalogue,
                    "equipment.coffee-machine.01",
                    FurnitureFunctionType.CoffeeMachine,
                    Phase8AssetPaths.CoffeeMachineDefinitionPath,
                    issues);
                ValidateCatalogueCompatibilityPrefab(issues);

                var content = RequireAsset<FurnitureContentCatalog>(
                    Phase8AssetPaths.ProductionContentCataloguePath);
                content.BuildRuntimeCatalog();
                var slots = content.BuildSurfaceSlotCatalog(new GridSettings(1f));
                var directions = content.BuildFunctionalDirectionCatalog();
                if (!content.TryGetDefinitionAsset(
                        "equipment.cash-register.01", out var cashRegister)
                    || !content.TryGetDefinitionAsset(
                        "equipment.coffee-machine.01", out var coffeeMachine)
                    || cashRegister == null
                    || coffeeMachine == null
                    || !directions.TryGetCashRegisterSides(
                        cashRegister.DefinitionId, out _)
                    || !directions.TryGetCoffeeMachineDirection(
                        coffeeMachine.DefinitionId, out _))
                {
                    issues.Add(new Phase8ValidationIssue(
                        "P8-ASSET-DIRECTIONS",
                        Phase8AssetPaths.ProductionContentCataloguePath));
                }

                var supportDefinitions = catalogue.Entries
                    .Where(entry => entry.Definition.FunctionType == FurnitureFunctionType.None)
                    .Select(entry => entry.Definition)
                    .ToArray();
                if (supportDefinitions.Any(definition =>
                        definition.Prefab.GetComponentsInChildren<SurfaceSlotMarker>(true).Length == 0)
                    || supportDefinitions.Any(definition =>
                        slots.GetForSupport(definition.DefinitionId).Count == 0))
                {
                    issues.Add(new Phase8ValidationIssue(
                        "P8-ASSET-SLOTS",
                        Phase8AssetPaths.ProductionContentCataloguePath));
                }
            }
            catch (Exception exception)
            {
                issues.Add(new Phase8ValidationIssue(
                    "P8-ASSET-EXCEPTION", exception.GetType().Name));
            }
        }

        private static void ValidateProductionDefinition(
            DecorationCatalogueAsset catalogue,
            string definitionId,
            FurnitureFunctionType expectedFunction,
            string expectedPath,
            ICollection<Phase8ValidationIssue> issues)
        {
            var matches = catalogue.Entries.Where(entry =>
                    entry?.Definition != null
                    && entry.Definition.DefinitionId == definitionId)
                .ToArray();
            if (matches.Length != 1
                || matches[0].Thumbnail == null
                || matches[0].Definition.FunctionType != expectedFunction
                || matches[0].Definition.AllowedPlacementSurfaces
                    != PlacementSurfaceType.FurnitureSurface
                || AssetDatabase.GetAssetPath(matches[0].Definition) != expectedPath
                || matches[0].Definition.Prefab == null
                || !AssetDatabase.GetAssetPath(matches[0].Definition.Prefab)
                    .StartsWith("Assets/Art/Phase4/Prefabs/", StringComparison.Ordinal))
            {
                issues.Add(new Phase8ValidationIssue(
                    "P8-ASSET-DEFINITION", definitionId));
            }
        }

        private static void ValidateScene(
            Scene scene,
            ICollection<Phase8ValidationIssue> issues)
        {
            try
            {
                if (scene.GetRootGameObjects().Count(root =>
                        root.name == Phase8AssetPaths.RuntimeRootName) != 1)
                {
                    issues.Add(new Phase8ValidationIssue(
                        "P8-SCENE-ROOTS", scene.path));
                }

                ValidateCount<DecorationModeController>(scene, 1, "P8-SCENE-CONTROLLER", issues);
                ValidateCount<CafeLayoutRuntime>(scene, 1, "P8-SCENE-LAYOUT", issues);
                ValidateCount<FurnitureSceneRegistry>(scene, 1, "P8-SCENE-FURNITURE-VIEW", issues);
                ValidateCount<SurfaceMountedSceneRegistry>(scene, 1, "P8-SCENE-MOUNTED-VIEW", issues);
                ValidateCount<SurfaceMountedPreviewView>(scene, 1, "P8-SCENE-PREVIEW-VIEW", issues);
                ValidateCount<PickUpPointIndicatorView>(scene, 1, "P8-SCENE-PICKUP-VIEW", issues);
                ValidateCount<DecorationCatalogueView>(scene, 1, "P8-SCENE-CATALOGUE-VIEW", issues);
                ValidateCount<DecorationActionBarView>(scene, 1, "P8-SCENE-ACTIONBAR-VIEW", issues);
                ValidateCount<ValidationMessageView>(scene, 1, "P8-SCENE-VALIDATION-VIEW", issues);
                ValidateCount<DecorationModeTabsView>(scene, 1, "P8-SCENE-PHASE7-UI", issues);
                ValidateCount<DecorationFloorRangeView>(scene, 1, "P8-SCENE-PHASE7-UI", issues);
                var canvasNames = FindAll<Canvas>(scene).Select(canvas => canvas.name).ToArray();
                if (canvasNames.Length != 3
                    || !canvasNames.OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(
                        new[] { "HUD Canvas", "Screen Canvas", "Toast Canvas" }))
                {
                    issues.Add(new Phase8ValidationIssue(
                        "P8-SCENE-CANVAS", scene.path));
                }

                var controllers = FindAll<DecorationModeController>(scene);
                if (controllers.Length == 1)
                {
                    ValidateControllerReferences(scene, controllers[0], issues);
                    ValidatePhase7UiCompatibility(scene, controllers[0], issues);
                }

                var runtimes = FindAll<CafeLayoutRuntime>(scene);
                if (runtimes.Length == 1)
                {
                    try
                    {
                        runtimes[0].Initialize();
                        if (runtimes[0].Layout == null
                            || runtimes[0].FunctionalSurfaceLayout == null
                            || runtimes[0].CurrentReadiness == null)
                        {
                            issues.Add(new Phase8ValidationIssue(
                                "P8-SCENE-RUNTIME-READINESS", scene.path));
                        }
                    }
                    catch (Exception exception)
                    {
                        issues.Add(new Phase8ValidationIssue(
                            "P8-SCENE-RUNTIME-READINESS",
                            scene.path + "|" + exception.GetType().Name));
                    }
                }

                var expectsDebug = scene.path == Phase8AssetPaths.ValidationScenePath;
                var debugViews = FindAll<InteractionAnchorDebugView>(scene);
                var debugRoots = FindAll<Transform>(scene).Count(item =>
                    item.name == Phase8AssetPaths.AnchorDebugRootName);
                if ((expectsDebug && (debugViews.Length != 1 || debugRoots != 1))
                    || (!expectsDebug && (debugViews.Length != 0 || debugRoots != 0)))
                {
                    issues.Add(new Phase8ValidationIssue(
                        "P8-SCENE-DEBUG-BOUNDARY", scene.path));
                }

                foreach (var rootName in new[]
                {
                    Phase8AssetPaths.MountedRepresentationRootName,
                    Phase8AssetPaths.PreviewRootName,
                    Phase8AssetPaths.PickUpIndicatorRootName
                })
                {
                    if (FindAll<Transform>(scene).Count(item => item.name == rootName) != 1)
                    {
                        issues.Add(new Phase8ValidationIssue(
                            "P8-SCENE-VIEW-ROOT", scene.path + "|" + rootName));
                    }
                }
            }
            catch (Exception exception)
            {
                issues.Add(new Phase8ValidationIssue(
                    "P8-SCENE-EXCEPTION", scene.path + "|" + exception.GetType().Name));
            }
        }

        private static void ValidateControllerReferences(
            Scene scene,
            DecorationModeController controller,
            ICollection<Phase8ValidationIssue> issues)
        {
            var serialized = new SerializedObject(controller);
            var mixedPath = AssetDatabase.GetAssetPath(serialized
                .FindProperty("phase8FurnitureCatalogueAsset")?.objectReferenceValue);
            var legacyPath = AssetDatabase.GetAssetPath(serialized
                .FindProperty("catalogueAsset")?.objectReferenceValue);
            var contentPath = AssetDatabase.GetAssetPath(serialized
                .FindProperty("contentCatalog")?.objectReferenceValue);
            var invalidSceneReferences =
                !MatchesUniqueSceneComponent<SurfaceMountedSceneRegistry>(
                    scene, serialized, "surfaceMountedSceneRegistry")
                || !MatchesUniqueSceneComponent<SurfaceMountedPreviewView>(
                    scene, serialized, "surfaceMountedPreviewView")
                || !MatchesUniqueSceneComponent<PickUpPointIndicatorView>(
                    scene, serialized, "pickUpPointIndicatorView")
                || !MatchesUniqueSceneComponent<ValidationMessageView>(
                    scene, serialized, "validationMessageView")
                || !MatchesUniqueNamedTransform(
                    scene, serialized, "surfaceMountedRepresentationRoot",
                    Phase8AssetPaths.MountedRepresentationRootName)
                || !MatchesUniqueNamedTransform(
                    scene, serialized, "functionalSurfacePreviewRoot",
                    Phase8AssetPaths.PreviewRootName)
                || !MatchesUniqueNamedTransform(
                    scene, serialized, "pickUpPointIndicatorRoot",
                    Phase8AssetPaths.PickUpIndicatorRootName);
            var invalidPrefabReferences =
                AssetDatabase.GetAssetPath(serialized
                    .FindProperty("functionalSurfacePreviewPrefab")
                    ?.objectReferenceValue)
                    != Phase8AssetPaths.FunctionalSurfacePreviewPrefabPath
                || AssetDatabase.GetAssetPath(serialized
                    .FindProperty("pickUpPointIndicatorPrefab")
                    ?.objectReferenceValue)
                    != Phase8AssetPaths.PickUpPointIndicatorPrefabPath;
            if (invalidSceneReferences
                || invalidPrefabReferences
                || mixedPath != Phase8AssetPaths.FurnitureCataloguePath
                || legacyPath != Phase8AssetPaths.LegacyDecorationCataloguePath
                || contentPath != Phase8AssetPaths.ProductionContentCataloguePath)
            {
                issues.Add(new Phase8ValidationIssue(
                    "P8-SCENE-REFERENCES", scene.path));
            }

            ValidatePrefabReference(serialized, "catalogueView",
                Phase8AssetPaths.CataloguePrefabPath, scene.path, issues);
            ValidatePrefabReference(serialized, "actionBarView",
                Phase8AssetPaths.ActionBarPrefabPath, scene.path, issues);

            var expectsDebug = scene.path == Phase8AssetPaths.ValidationScenePath;
            var debugVisible = serialized.FindProperty("interactionAnchorDebugVisible")?.boolValue
                ?? false;
            var debugView = serialized.FindProperty("interactionAnchorDebugView")
                ?.objectReferenceValue;
            var debugRoot = serialized.FindProperty("interactionAnchorDebugRoot")
                ?.objectReferenceValue;
            var employeeMaterial = serialized.FindProperty("employeeAnchorDebugMaterial")
                ?.objectReferenceValue;
            var customerMaterial = serialized.FindProperty("customerAnchorDebugMaterial")
                ?.objectReferenceValue;
            var expectedDebugView = UniqueSceneComponent<InteractionAnchorDebugView>(scene);
            var expectedDebugRoot = UniqueNamedTransform(
                scene, Phase8AssetPaths.AnchorDebugRootName);
            var invalidDebug = debugVisible != expectsDebug;
            if (expectsDebug)
            {
                invalidDebug |= expectedDebugView == null
                    || expectedDebugRoot == null
                    || debugView != expectedDebugView
                    || debugRoot != expectedDebugRoot
                    || AssetDatabase.GetAssetPath(employeeMaterial)
                        != Phase8AssetPaths.EmployeeAnchorDebugMaterialPath
                    || AssetDatabase.GetAssetPath(customerMaterial)
                        != Phase8AssetPaths.CustomerAnchorDebugMaterialPath;
            }
            else
            {
                invalidDebug |= debugView != null
                    || debugRoot != null
                    || employeeMaterial != null
                    || customerMaterial != null;
            }
            if (invalidDebug)
            {
                issues.Add(new Phase8ValidationIssue(
                    "P8-SCENE-DEBUG-REFERENCES", scene.path));
            }
        }

        private static void ValidateCatalogueCompatibilityPrefab(
            ICollection<Phase8ValidationIssue> issues)
        {
            var root = RequireAsset<GameObject>(Phase8AssetPaths.CataloguePrefabPath);
            var catalogue = root.GetComponent<DecorationCatalogueView>();
            var tabs = root.GetComponentsInChildren<DecorationModeTabsView>(true);
            var ranges = root.GetComponentsInChildren<DecorationFloorRangeView>(true);
            var buttons = ranges.Length == 1
                ? ranges[0].GetComponentsInChildren<Button>(true)
                : Array.Empty<Button>();
            var wholeRoom = buttons.FirstOrDefault(button =>
                button.name == "WholeRoomButton");
            var singleGrid = buttons.FirstOrDefault(button =>
                button.name == "SingleGridButton");
            var rangeSerialized = ranges.Length == 1
                ? new SerializedObject(ranges[0])
                : null;
            if (catalogue == null
                || catalogue.SurfaceFooterHost == null
                || tabs.Length != 1
                || ranges.Length != 1
                || ranges[0].transform.parent != catalogue.SurfaceFooterHost
                || GetRelativePath(root.transform, ranges[0].transform)
                    != "SurfaceFooterHost/FloorRange"
                || !buttons.Select(button => button.name).OrderBy(name => name)
                    .SequenceEqual(new[] { "SingleGridButton", "WholeRoomButton" })
                || rangeSerialized.FindProperty("wholeRoomButton")?.objectReferenceValue
                    != wholeRoom
                || rangeSerialized.FindProperty("singleGridButton")?.objectReferenceValue
                    != singleGrid)
            {
                issues.Add(new Phase8ValidationIssue(
                    "P8-ASSET-PHASE7-UI", Phase8AssetPaths.CataloguePrefabPath));
            }
        }

        private static void ValidatePhase7UiCompatibility(
            Scene scene,
            DecorationModeController controller,
            ICollection<Phase8ValidationIssue> issues)
        {
            var tabs = FindAll<DecorationModeTabsView>(scene);
            var ranges = FindAll<DecorationFloorRangeView>(scene);
            var catalogues = FindAll<DecorationCatalogueView>(scene);
            var serialized = new SerializedObject(controller);
            var invalid = tabs.Length != 1
                || ranges.Length != 1
                || catalogues.Length != 1;
            if (!invalid)
            {
                var buttons = ranges[0].GetComponentsInChildren<Button>(true);
                var wholeRoom = buttons.FirstOrDefault(button =>
                    button.name == "WholeRoomButton");
                var singleGrid = buttons.FirstOrDefault(button =>
                    button.name == "SingleGridButton");
                var rangeSerialized = new SerializedObject(ranges[0]);
                var rangeSource = PrefabUtility.GetCorrespondingObjectFromSource(
                    ranges[0]);
                invalid = ranges[0].transform.parent != catalogues[0].SurfaceFooterHost
                    || GetRelativePath(catalogues[0].transform, ranges[0].transform)
                        != "SurfaceFooterHost/FloorRange"
                    || AssetDatabase.GetAssetPath(rangeSource)
                        != Phase8AssetPaths.CataloguePrefabPath
                    || !buttons.Select(button => button.name).OrderBy(name => name)
                        .SequenceEqual(new[] { "SingleGridButton", "WholeRoomButton" })
                    || rangeSerialized.FindProperty("wholeRoomButton")?.objectReferenceValue
                        != wholeRoom
                    || rangeSerialized.FindProperty("singleGridButton")?.objectReferenceValue
                        != singleGrid
                    || serialized.FindProperty("modeTabsView")?.objectReferenceValue != tabs[0]
                    || serialized.FindProperty("floorRangeView")?.objectReferenceValue != ranges[0];
            }
            if (invalid)
            {
                issues.Add(new Phase8ValidationIssue(
                    "P8-SCENE-PHASE7-UI", scene.path));
            }
        }

        private static void ValidatePrefabReference(
            SerializedObject serialized,
            string propertyName,
            string expectedPath,
            string scenePath,
            ICollection<Phase8ValidationIssue> issues)
        {
            var value = serialized.FindProperty(propertyName)?.objectReferenceValue;
            var source = value != null
                ? PrefabUtility.GetCorrespondingObjectFromSource(value)
                : null;
            if (AssetDatabase.GetAssetPath(source) != expectedPath)
            {
                issues.Add(new Phase8ValidationIssue(
                    "P8-SCENE-UI-PREFAB", scenePath + "|" + propertyName));
            }
        }

        private static bool MatchesUniqueSceneComponent<T>(
            Scene scene,
            SerializedObject serialized,
            string propertyName) where T : Component
        {
            var expected = UniqueSceneComponent<T>(scene);
            return expected != null
                && serialized.FindProperty(propertyName)?.objectReferenceValue == expected;
        }

        private static T UniqueSceneComponent<T>(Scene scene) where T : Component
        {
            var matches = FindAll<T>(scene);
            return matches.Length == 1 ? matches[0] : null;
        }

        private static bool MatchesUniqueNamedTransform(
            Scene scene,
            SerializedObject serialized,
            string propertyName,
            string expectedName)
        {
            var expected = UniqueNamedTransform(scene, expectedName);
            return expected != null
                && serialized.FindProperty(propertyName)?.objectReferenceValue == expected;
        }

        private static Transform UniqueNamedTransform(Scene scene, string name)
        {
            var matches = FindAll<Transform>(scene)
                .Where(item => item.name == name)
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        private static void ValidateCount<T>(
            Scene scene,
            int expected,
            string code,
            ICollection<Phase8ValidationIssue> issues) where T : Component
        {
            if (FindAll<T>(scene).Length != expected)
            {
                issues.Add(new Phase8ValidationIssue(code, scene.path));
            }
        }

        private static T[] FindAll<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();

        private static string GetRelativePath(Transform root, Transform current)
        {
            if (current == root)
                return string.Empty;
            var names = new Stack<string>();
            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return current == root ? string.Join("/", names) : string.Empty;
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException($"Missing Asset '{path}'.");
    }
}

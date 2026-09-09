using System;
using System.IO;
using System.Linq;
using AnimalCafe.Decoration;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.Phase8
{
    public static class Phase8SceneSetup
    {
        [MenuItem("Tools/AnimalCafe/Phase 8/Configure Validation Scene")]
        public static void ConfigureValidationScene()
        {
            ConfigureValidationSceneCore(Phase8AssetPaths.Phase7ValidationScenePath,
                Phase8AssetPaths.ValidationScenePath);
            Debug.Log("Phase 8 validation Scene configured and validated.");
        }

        private static void ConfigureValidationSceneCore(string sourcePath, string targetPath)
        {
            RequireConfigurationPreconditions(targetPath, debugAnchors: true);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath) == null)
            {
                RequireAsset<SceneAsset>(sourcePath, "Phase 7 validation Scene");
                if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                {
                    throw new InvalidOperationException(
                        "Could not create the Phase 8 validation Scene through AssetDatabase.CopyAsset.");
                }
                AssetDatabase.ImportAsset(
                    targetPath,
                    ImportAssetOptions.ForceSynchronousImport);
            }

            ConfigureScene(targetPath, debugAnchors: true);
        }

        [MenuItem("Tools/AnimalCafe/Phase 8/Configure MainCafe")]
        public static void ConfigureMainCafe()
        {
            RequireAsset<SceneAsset>(Phase8AssetPaths.MainCafeScenePath, "MainCafe Scene");
            ConfigureScene(Phase8AssetPaths.MainCafeScenePath, debugAnchors: false);
            Debug.Log("MainCafe Phase 8 functional furniture wiring configured and validated.");
        }

        private static void ConfigureScene(string scenePath, bool debugAnchors)
        {
            RequireConfigurationPreconditions(scenePath, debugAnchors);
            var originalScenes = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt).ToArray();
            var existing = originalScenes.FirstOrDefault(item => item.path == scenePath);
            var wasPresent = existing.IsValid();
            var wasLoaded = wasPresent && existing.isLoaded;
            // Scene wiring must never save unrelated dirty assets as a side effect.
            RequireAsset<SceneAsset>(scenePath, "target Scene");
            var originalActive = SceneManager.GetActiveScene();
            var targetWasActive = wasLoaded && originalActive == existing;
            var originalIndex = Array.FindIndex(originalScenes, item => item.path == scenePath);
            var previous = originalIndex > 0 ? originalScenes[originalIndex - 1] : default;
            var next = originalIndex >= 0 && originalIndex + 1 < originalScenes.Length
                ? originalScenes[originalIndex + 1] : default;
            var backupFolder = Path.Combine("Library", "Phase8SceneSetup", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(backupFolder);
            File.Copy(scenePath, Path.Combine(backupFolder, "target.unity"));
            File.Copy(scenePath + ".meta", Path.Combine(backupFolder, "target.meta"));
            var scene = existing;
            Scene rollbackPlaceholder = default;
            var mayRemoveBackup = false;
            try
            {
                if (!wasLoaded)
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                var changed = ConfigureSceneGraph(scene, debugAnchors);
                SceneManager.SetActiveScene(scene);
                ValidateCandidate(scenePath);
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, scenePath))
                        throw new InvalidOperationException(
                            $"Could not save configured Scene '{scenePath}'.");
                }
                mayRemoveBackup = true;
            }
            catch (Exception configurationFailure)
            {
                try
                {
                    // The clean target's original file is also its in-memory rollback state.
                    // Reload only on failure; successful setup keeps the caller's Scene handle.
                    if (scene.IsValid() && scene.isLoaded)
                    {
                        if (Enumerable.Range(0, SceneManager.sceneCount)
                            .Select(SceneManager.GetSceneAt).Count(item => item.isLoaded) == 1)
                            rollbackPlaceholder = EditorSceneManager.NewScene(
                                NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                        if (!EditorSceneManager.CloseScene(scene, true))
                            throw new InvalidOperationException(
                                $"Unity could not unload '{scenePath}' for rollback.");
                    }
                    RestoreSceneFilesIfChanged(scenePath, backupFolder);
                    scene = wasPresent
                        ? EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive)
                        : default;
                    mayRemoveBackup = true;
                }
                catch (Exception rollbackFailure)
                {
                    throw new AggregateException(
                        $"Phase 8 setup could not restore '{scenePath}'. The original Scene backup is retained at '{backupFolder}'.",
                        configurationFailure, rollbackFailure);
                }
                throw;
            }
            finally
            {
                if (scene.IsValid())
                {
                    if (!wasPresent)
                        EditorSceneManager.CloseScene(scene, true);
                    else
                    {
                        if (!wasLoaded && scene.isLoaded)
                            EditorSceneManager.CloseScene(scene, false);
                        if (next.IsValid()) EditorSceneManager.MoveSceneBefore(scene, next);
                        else if (previous.IsValid()) EditorSceneManager.MoveSceneAfter(scene, previous);
                    }
                }
                var active = targetWasActive ? scene : originalActive;
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                if (rollbackPlaceholder.IsValid() && rollbackPlaceholder.isLoaded
                    && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(rollbackPlaceholder, true);
                if (mayRemoveBackup)
                {
                    try { Directory.Delete(backupFolder, true); }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"Phase 8 setup retained backup '{backupFolder}': {exception.Message}");
                    }
                }
            }
        }

        private static void RequireConfigurationPreconditions(string scenePath, bool debugAnchors)
        {
            var existing = SceneManager.GetSceneByPath(scenePath);
            if (existing.IsValid() && existing.isLoaded && existing.isDirty)
                throw new InvalidOperationException(
                    $"The target Scene '{scenePath}' is dirty. Save or revert it before Phase 8 setup.");

            // Both CopyAsset and SaveScene may flush other loaded assets. Built-in
            // resources and package imports are not editable project asset files.
            var dirtyAssets = Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                .Where(asset => asset != null && !(asset is SceneAsset) && EditorUtility.IsPersistent(asset)
                    && EditorUtility.IsDirty(asset))
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path)
                    && path.StartsWith("Assets/", StringComparison.Ordinal) && File.Exists(path))
                .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (dirtyAssets.Length != 0)
                throw new InvalidOperationException(
                    "Loaded assets are dirty. Save or revert these assets before Phase 8 Scene setup: "
                    + string.Join(", ", dirtyAssets));
            RequireConfigurationAssets(debugAnchors);
        }

        private static void RequireConfigurationAssets(bool debugAnchors)
        {
            try
            {
                RequireAsset<DecorationCatalogueAsset>(Phase8AssetPaths.FurnitureCataloguePath,
                    "Phase 8 furniture catalogue");
                foreach (var path in new[]
                {
                    Phase8AssetPaths.CataloguePrefabPath, Phase8AssetPaths.ActionBarPrefabPath,
                    Phase8AssetPaths.FunctionalSurfacePreviewPrefabPath,
                    Phase8AssetPaths.PickUpPointIndicatorPrefabPath
                })
                    RequireAsset<GameObject>(path, "Phase 8 Prefab");
                RequireAsset<TMP_FontAsset>(Phase8AssetPaths.UiFontPath, "Phase 6 UI Font");
                if (debugAnchors)
                {
                    RequireAsset<Material>(Phase8AssetPaths.EmployeeAnchorDebugMaterialPath,
                        "employee anchor debug Material");
                    RequireAsset<Material>(Phase8AssetPaths.CustomerAnchorDebugMaterialPath,
                        "customer anchor debug Material");
                }
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidOperationException(exception.Message
                    + " Run Tools/AnimalCafe/Phase 8/Build Assets first, then retry Scene setup.", exception);
            }
        }

        private static void ValidateCandidate(string scenePath)
        {
            var report = Phase8Validator.ValidateOpenScene();
            if (report.Issues.Count != 0)
                throw new InvalidOperationException(
                    $"Phase 8 Scene validation failed for '{scenePath}':\n"
                    + string.Join("\n", report.Issues.Select(issue => issue.Code + ": " + issue.Message)));
        }

        private static void RestoreSceneFilesIfChanged(string scenePath, string backupFolder)
        {
            var sceneBackup = Path.Combine(backupFolder, "target.unity");
            var metaBackup = Path.Combine(backupFolder, "target.meta");
            if (SameFileBytes(scenePath, sceneBackup) && SameFileBytes(scenePath + ".meta", metaBackup))
                return;

            // Reuse Phase 6's release-and-restore pattern so Unity also releases the
            // failed serialized asset before the original bytes and GUID are imported.
            var stagedScene = Path.Combine(backupFolder, "restore.unity");
            var stagedMeta = Path.Combine(backupFolder, "restore.meta");
            File.Copy(sceneBackup, stagedScene, true);
            File.Copy(metaBackup, stagedMeta, true);
            if (!AssetDatabase.DeleteAsset(scenePath))
                throw new IOException($"Unity could not release '{scenePath}' for rollback.");
            File.Move(stagedScene, scenePath);
            File.Move(stagedMeta, scenePath + ".meta");
            AssetDatabase.ImportAsset(scenePath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            if (!SameFileBytes(scenePath, sceneBackup) || !SameFileBytes(scenePath + ".meta", metaBackup)
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                throw new IOException($"Restored Scene '{scenePath}' does not match its backup.");
        }

        private static bool SameFileBytes(string first, string second) => File.Exists(first)
            && File.Exists(second) && File.ReadAllBytes(first).SequenceEqual(File.ReadAllBytes(second));

        private static bool ConfigureSceneGraph(Scene scene, bool debugAnchors)
        {
            var changed = false;
            var controller = FindAll<DecorationModeController>(scene).SingleOrDefault()
                ?? throw new InvalidOperationException(
                    $"'{scene.path}' must already contain one DecorationModeController.");
            if (FindAll<DecorationModeController>(scene).Length != 1)
            {
                throw new InvalidOperationException(
                    $"'{scene.path}' must contain exactly one DecorationModeController.");
            }

            ValidateLegacyReferences(controller, scene.path);
            var runtimeRoot = EnsureSingleRoot(scene, Phase8AssetPaths.RuntimeRootName,
                ref changed);
            var mountedRegistry = EnsureSingleComponent<SurfaceMountedSceneRegistry>(
                runtimeRoot, ref changed);
            var mountedPreview = EnsureSingleComponent<SurfaceMountedPreviewView>(
                runtimeRoot, ref changed);
            var pickUpIndicators = EnsureSingleComponent<PickUpPointIndicatorView>(
                runtimeRoot, ref changed);

            var decorationSpace = FindAll<Transform>(scene).SingleOrDefault(item =>
                item.name == "DecorationSpaceRoot")
                ?? throw new InvalidOperationException(
                    $"'{scene.path}' is missing the Phase 6 DecorationSpaceRoot.");
            var mountedRoot = EnsureSingleNamedChild(
                scene, decorationSpace, Phase8AssetPaths.MountedRepresentationRootName,
                ref changed);
            var previewRoot = EnsureSingleNamedChild(
                scene, decorationSpace, Phase8AssetPaths.PreviewRootName, ref changed);
            var indicatorRoot = EnsureSingleNamedChild(
                scene, decorationSpace, Phase8AssetPaths.PickUpIndicatorRootName, ref changed);

            var catalogueView = EnsureUiPrefab<DecorationCatalogueView>(
                scene, Phase8AssetPaths.CataloguePrefabPath, ref changed);
            var actionBarView = EnsureUiPrefab<DecorationActionBarView>(
                scene, Phase8AssetPaths.ActionBarPrefabPath, ref changed);
            var modeTabsView = catalogueView
                .GetComponentsInChildren<DecorationModeTabsView>(true)
                .SingleOrDefault()
                ?? throw new InvalidOperationException(
                    "Phase 8 Catalogue Prefab must contain one DecorationModeTabsView.");
            var floorRangeView = catalogueView
                .GetComponentsInChildren<DecorationFloorRangeView>(true)
                .SingleOrDefault()
                ?? throw new InvalidOperationException(
                    "Phase 8 Catalogue Prefab must contain one DecorationFloorRangeView.");
            var validationMessage = EnsureValidationMessage(scene, ref changed);

            InteractionAnchorDebugView debugView = null;
            Transform debugRoot = null;
            if (debugAnchors)
            {
                debugView = EnsureSingleComponent<InteractionAnchorDebugView>(
                    runtimeRoot, ref changed);
                debugRoot = EnsureSingleNamedChild(
                    scene, decorationSpace, Phase8AssetPaths.AnchorDebugRootName,
                    ref changed);
            }
            else
            {
                foreach (var view in FindAll<InteractionAnchorDebugView>(scene))
                {
                    if (view.gameObject == runtimeRoot)
                    {
                        UnityEngine.Object.DestroyImmediate(view);
                        changed = true;
                    }
                }
                foreach (var root in FindAll<Transform>(scene).Where(item =>
                             item.name == Phase8AssetPaths.AnchorDebugRootName).ToArray())
                {
                    UnityEngine.Object.DestroyImmediate(root.gameObject);
                    changed = true;
                }
            }

            var serialized = new SerializedObject(controller);
            changed |= SetObject(serialized, "phase8FurnitureCatalogueAsset",
                RequireAsset<DecorationCatalogueAsset>(
                    Phase8AssetPaths.FurnitureCataloguePath,
                    "Phase 8 furniture catalogue"));
            changed |= SetObject(serialized, "surfaceMountedSceneRegistry", mountedRegistry);
            changed |= SetObject(serialized, "surfaceMountedPreviewView", mountedPreview);
            changed |= SetObject(serialized, "pickUpPointIndicatorView", pickUpIndicators);
            changed |= SetObject(serialized, "validationMessageView", validationMessage);
            changed |= SetObject(serialized, "surfaceMountedRepresentationRoot", mountedRoot);
            changed |= SetObject(serialized, "functionalSurfacePreviewRoot", previewRoot);
            changed |= SetObject(serialized, "pickUpPointIndicatorRoot", indicatorRoot);
            changed |= SetObject(serialized, "functionalSurfacePreviewPrefab",
                RequireAsset<GameObject>(
                    Phase8AssetPaths.FunctionalSurfacePreviewPrefabPath,
                    "functional surface Preview Prefab"));
            changed |= SetObject(serialized, "pickUpPointIndicatorPrefab",
                RequireAsset<GameObject>(
                    Phase8AssetPaths.PickUpPointIndicatorPrefabPath,
                    "Pick-up Point indicator Prefab"));
            changed |= SetObject(serialized, "catalogueView", catalogueView);
            changed |= SetObject(serialized, "actionBarView", actionBarView);
            changed |= SetObject(serialized, "modeTabsView", modeTabsView);
            changed |= SetObject(serialized, "floorRangeView", floorRangeView);
            changed |= SetObject(serialized, "interactionAnchorDebugView", debugView);
            changed |= SetObject(serialized, "interactionAnchorDebugRoot", debugRoot);
            changed |= SetObject(serialized, "employeeAnchorDebugMaterial",
                debugAnchors
                    ? RequireAsset<Material>(
                        Phase8AssetPaths.EmployeeAnchorDebugMaterialPath,
                        "employee anchor debug Material")
                    : null);
            changed |= SetObject(serialized, "customerAnchorDebugMaterial",
                debugAnchors
                    ? RequireAsset<Material>(
                        Phase8AssetPaths.CustomerAnchorDebugMaterialPath,
                        "customer anchor debug Material")
                    : null);
            changed |= SetBool(serialized, "interactionAnchorDebugVisible", debugAnchors);
            changed |= serialized.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }

        private static void ValidateLegacyReferences(
            DecorationModeController controller,
            string scenePath)
        {
            var serialized = new SerializedObject(controller);
            var legacyPath = AssetDatabase.GetAssetPath(
                serialized.FindProperty("catalogueAsset").objectReferenceValue);
            if (legacyPath != Phase8AssetPaths.LegacyDecorationCataloguePath)
            {
                throw new InvalidOperationException(
                    $"'{scenePath}' must preserve its Phase 6 legacy catalogue reference; found '{legacyPath}'.");
            }
            var contentPath = AssetDatabase.GetAssetPath(
                serialized.FindProperty("contentCatalog").objectReferenceValue);
            if (contentPath != Phase8AssetPaths.ProductionContentCataloguePath)
            {
                throw new InvalidOperationException(
                    $"'{scenePath}' must reuse the Phase 6 production content catalogue; found '{contentPath}'.");
            }
        }

        private static GameObject EnsureSingleRoot(
            Scene scene,
            string name,
            ref bool changed)
        {
            var matches = scene.GetRootGameObjects().Where(root => root.name == name).ToArray();
            GameObject root;
            if (matches.Length == 0)
            {
                root = new GameObject(name);
                SceneManager.MoveGameObjectToScene(root, scene);
                changed = true;
            }
            else
            {
                root = matches[0];
                foreach (var duplicate in matches.Skip(1))
                {
                    UnityEngine.Object.DestroyImmediate(duplicate);
                    changed = true;
                }
            }

            if (root.transform.position != Vector3.zero)
            {
                root.transform.position = Vector3.zero;
                changed = true;
            }
            if (root.transform.rotation != Quaternion.identity)
            {
                root.transform.rotation = Quaternion.identity;
                changed = true;
            }
            if (root.transform.localScale != Vector3.one)
            {
                root.transform.localScale = Vector3.one;
                changed = true;
            }
            return root;
        }

        private static T EnsureSingleComponent<T>(GameObject owner, ref bool changed)
            where T : Component
        {
            var components = owner.GetComponents<T>();
            if (components.Length == 0)
            {
                changed = true;
                return owner.AddComponent<T>();
            }
            foreach (var duplicate in components.Skip(1))
            {
                UnityEngine.Object.DestroyImmediate(duplicate);
                changed = true;
            }
            return components[0];
        }

        private static Transform EnsureSingleNamedChild(
            Scene scene,
            Transform parent,
            string name,
            ref bool changed)
        {
            var matches = FindAll<Transform>(scene).Where(item => item.name == name).ToArray();
            Transform result;
            if (matches.Length == 0)
            {
                var created = new GameObject(name);
                created.transform.SetParent(parent, false);
                result = created.transform;
                changed = true;
            }
            else
            {
                result = matches[0];
                foreach (var duplicate in matches.Skip(1))
                {
                    UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
                    changed = true;
                }
                if (result.parent != parent)
                {
                    result.SetParent(parent, false);
                    changed = true;
                }
            }

            if (result.localPosition != Vector3.zero)
            {
                result.localPosition = Vector3.zero;
                changed = true;
            }
            if (result.localRotation != Quaternion.identity)
            {
                result.localRotation = Quaternion.identity;
                changed = true;
            }
            if (result.localScale != Vector3.one)
            {
                result.localScale = Vector3.one;
                changed = true;
            }
            return result;
        }

        private static T EnsureUiPrefab<T>(
            Scene scene,
            string prefabPath,
            ref bool changed) where T : Component
        {
            var existing = FindAll<T>(scene);
            if (existing.Length != 1)
            {
                throw new InvalidOperationException(
                    $"'{scene.path}' must contain exactly one {typeof(T).Name} before Phase 8 migration.");
            }

            var source = PrefabUtility.GetCorrespondingObjectFromSource(existing[0].gameObject);
            if (AssetDatabase.GetAssetPath(source) == prefabPath)
            {
                return existing[0];
            }

            var prefab = RequireAsset<GameObject>(prefabPath, typeof(T).Name + " Prefab");
            var oldObject = existing[0].gameObject;
            var state = RectState.Capture(oldObject);
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject
                ?? throw new InvalidOperationException($"Could not instantiate '{prefabPath}'.");
            state.Apply(instance);
            UnityEngine.Object.DestroyImmediate(oldObject);
            changed = true;
            return instance.GetComponent<T>()
                ?? throw new InvalidOperationException(
                    $"'{prefabPath}' is missing {typeof(T).Name}.");
        }

        private static ValidationMessageView EnsureValidationMessage(
            Scene scene,
            ref bool changed)
        {
            var existing = FindAll<ValidationMessageView>(scene);
            ValidationMessageView view;
            if (existing.Length == 0)
            {
                var parent = FindAll<Transform>(scene).SingleOrDefault(item =>
                    item.name == "Phase7_UIRuntime")
                    ?? FindAll<Canvas>(scene).Single().transform;
                var root = new GameObject(
                    Phase8AssetPaths.ValidationMessageName,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(ValidationMessageView));
                root.transform.SetParent(parent, false);
                var rect = (RectTransform)root.transform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -96f);
                rect.sizeDelta = new Vector2(720f, 64f);
                var background = root.GetComponent<Image>();
                background.color = new Color(0.12f, 0.10f, 0.08f, 0.94f);
                background.raycastTarget = false;

                var labelObject = new GameObject(
                    "Message", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(root.transform, false);
                var labelRect = (RectTransform)labelObject.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(20f, 8f);
                labelRect.offsetMax = new Vector2(-20f, -8f);
                var label = labelObject.GetComponent<TextMeshProUGUI>();
                label.font = RequireAsset<TMP_FontAsset>(
                    Phase8AssetPaths.UiFontPath, "Phase 6 UI Font");
                label.fontSize = 22f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;
                label.raycastTarget = false;
                view = root.GetComponent<ValidationMessageView>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("messageLabel").objectReferenceValue = label;
                serialized.FindProperty("background").objectReferenceValue = background;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                view.Clear();
                changed = true;
            }
            else
            {
                view = existing[0];
                foreach (var duplicate in existing.Skip(1))
                {
                    if (duplicate.gameObject.name == Phase8AssetPaths.ValidationMessageName)
                    {
                        UnityEngine.Object.DestroyImmediate(duplicate.gameObject);
                        changed = true;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"'{scene.path}' contains an unexpected duplicate ValidationMessageView.");
                    }
                }
            }
            changed |= Phase8FeedbackAssets.ConfigureMessageView(view);
            return view;
        }

        private static bool SetObject(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName)
                ?? throw new MissingFieldException(serialized.targetObject.GetType().Name,
                    propertyName);
            if (property.objectReferenceValue == value)
            {
                return false;
            }
            property.objectReferenceValue = value;
            return true;
        }

        private static bool SetBool(
            SerializedObject serialized,
            string propertyName,
            bool value)
        {
            var property = serialized.FindProperty(propertyName)
                ?? throw new MissingFieldException(serialized.targetObject.GetType().Name,
                    propertyName);
            if (property.boolValue == value)
            {
                return false;
            }
            property.boolValue = value;
            return true;
        }

        private static T[] FindAll<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();

        private static T RequireAsset<T>(string path, string label)
            where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException($"Missing {label} at '{path}'.");

        private readonly struct RectState
        {
            private readonly Transform parent;
            private readonly int siblingIndex;
            private readonly string name;
            private readonly bool active;
            private readonly Vector2 anchorMin;
            private readonly Vector2 anchorMax;
            private readonly Vector2 pivot;
            private readonly Vector2 anchoredPosition;
            private readonly Vector2 sizeDelta;
            private readonly Vector3 localScale;

            private RectState(GameObject source)
            {
                parent = source.transform.parent;
                siblingIndex = source.transform.GetSiblingIndex();
                name = source.name;
                active = source.activeSelf;
                var rect = source.transform as RectTransform;
                anchorMin = rect != null ? rect.anchorMin : Vector2.zero;
                anchorMax = rect != null ? rect.anchorMax : Vector2.one;
                pivot = rect != null ? rect.pivot : Vector2.one * 0.5f;
                anchoredPosition = rect != null ? rect.anchoredPosition : Vector2.zero;
                sizeDelta = rect != null ? rect.sizeDelta : Vector2.zero;
                localScale = source.transform.localScale;
            }

            public static RectState Capture(GameObject source) => new RectState(source);

            public void Apply(GameObject target)
            {
                target.name = name;
                target.transform.SetParent(parent, false);
                target.transform.SetSiblingIndex(Mathf.Min(siblingIndex,
                    target.transform.parent.childCount - 1));
                target.SetActive(active);
                target.transform.localScale = localScale;
                if (target.transform is RectTransform rect)
                {
                    rect.anchorMin = anchorMin;
                    rect.anchorMax = anchorMax;
                    rect.pivot = pivot;
                    rect.anchoredPosition = anchoredPosition;
                    rect.sizeDelta = sizeDelta;
                }
            }
        }
    }
}

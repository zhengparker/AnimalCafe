using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.Phase7;
using AnimalCafe.EditorTools.Phase8;
using AnimalCafe.UI;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.P8R;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static AnimalCafe.EditorTools.P8R.P8RFurnitureUiBuilder;

namespace AnimalCafe.EditorTools.P8R
{
    /// <summary>Targeted MainCafe presentation migration. No domain assets or global theme writes.
    /// 只迁移 MainCafe UI 展示，不写 domain assets 或全局 Theme。</summary>
    public static class P8RCompleteUiBuilder
    {
        public const string ExitPrefab = "Assets/UI/P8R/Prefabs/PF_UI_P8RExitModal.prefab";
        internal static Action AfterPrefabSaveForTests;
        internal static Action AfterSceneStyleForTests;

        [MenuItem("Tools/AnimalCafe/P8R/Complete All Existing UI")]
        public static void BuildApprovedCompleteUi()
        {
            Build(false);
        }

        [MenuItem("Tools/AnimalCafe/P8R/Refresh Complete Approved UI")]
        public static void RefreshApprovedCompleteUi()
        {
            Build(true);
        }

        [MenuItem("Tools/AnimalCafe/P8R/Refresh Approved Reference Layout")]
        public static void RefreshApprovedReferenceLayout() => Build(true, true);

        [MenuItem("Tools/AnimalCafe/P8R/Apply Approved Catalogue Cards")]
        public static void ApplyApprovedCatalogueCards()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before catalogue-card authoring.");
            RequireCleanLoadedAssets();
            var appearance = Require<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            var prefab = Require<GameObject>(P8RFurnitureUiPaths.CataloguePrefab);
            if (EditorUtility.IsDirty(appearance) || EditorUtility.IsDirty(prefab)
                || prefab.GetComponentsInChildren<Component>(true).Any(component => component != null && EditorUtility.IsDirty(component)))
                throw new InvalidOperationException("Save or revert P8R Appearance/catalogue changes before card authoring.");
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == P8RFurnitureUiPaths.CataloguePrefab)
                throw new InvalidOperationException("Close catalogue Prefab Mode before card authoring.");

            var root = PrefabUtility.LoadPrefabContents(P8RFurnitureUiPaths.CataloguePrefab);
            try
            {
                if (!StyleCatalogueCards(root, appearance)) return;
                PrefabUtility.SaveAsPrefabAsset(root, P8RFurnitureUiPaths.CataloguePrefab, out var saved);
                if (!saved) throw new InvalidOperationException("Could not save approved catalogue cards.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [MenuItem("Tools/AnimalCafe/P8R/Apply Approved Refined B Style Only")]
        public static void RefreshApprovedRefinedBStyle()
        {
            RequireClosedTargetPrefabStage();
            RequireCleanLoadedAssets();
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before B migration.");
            var appearance = Require<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            foreach (var key in P8RRefinedBAssets.ReplacementKeys) Require<Sprite>(P8RRefinedBAssets.PathFor(key));
            var active = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(Phase8AssetPaths.MainCafeScenePath);
            var wasPresent = scene.IsValid();
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(Phase8AssetPaths.MainCafeScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save MainCafe edits before B migration.");
                var controller = Find<DecorationModeController>(scene);
                if (!HasCompleteWiring(controller)) throw new InvalidOperationException("B styling requires intact P8R UI wiring.");
                RequireProtectedSceneStructure(controller);
                var prefabPaths = new[] { P8RFurnitureUiPaths.CataloguePrefab, P8RFurnitureUiPaths.ActionPrefab,
                    P8RFurnitureUiPaths.StorePrefab, ExitPrefab };
                // Check before registering Undo: even an empty hierarchy Undo marks the scene dirty.
                // 无变化时不注册 Undo、不保存，避免第二次应用产生无意义的场景改动。
                var needsStyle = !appearance.IsRefinedB || P8RRefinedBAssets.ReplacementKeys.Any(key =>
                    AssetDatabase.GetAssetPath(appearance.Sprite(key)) != P8RRefinedBAssets.PathFor(key))
                    || prefabPaths.Any(path => StyleBHierarchy(Require<GameObject>(path), appearance, false))
                    || scene.GetRootGameObjects().Any(root => StyleBHierarchy(root, appearance, false));
                if (!needsStyle && AfterPrefabSaveForTests == null && AfterSceneStyleForTests == null) return;
                var before = CaptureTargetBytes();
                Undo.IncrementCurrentGroup(); var undo = Undo.GetCurrentGroup();
                foreach (var root in scene.GetRootGameObjects()) Undo.RegisterFullObjectHierarchyUndo(root, "P8R B style only");
                try
                {
                    P8RRefinedBAssets.BindAppearance(appearance);
                    foreach (var path in prefabPaths)
                    {
                        var root = PrefabUtility.LoadPrefabContents(path);
                        try { if (StyleBHierarchy(root, appearance)) PrefabUtility.SaveAsPrefabAsset(root, path); }
                        finally { PrefabUtility.UnloadPrefabContents(root); }
                    }
                    AfterPrefabSaveForTests?.Invoke();
                    var changed = false;
                    foreach (var root in scene.GetRootGameObjects()) changed |= StyleBHierarchy(root, appearance);
                    AfterSceneStyleForTests?.Invoke();
                    if (!HasCompleteWiring(controller)) throw new InvalidOperationException("B styling changed P8R wiring.");
                    if (changed || scene.isDirty)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save B style.");
                    }
                    Undo.CollapseUndoOperations(undo);
                }
                catch
                {
                    Undo.RevertAllDownToGroup(undo);
                    RestoreTargetBytes(before);
                    // Entry rejects a dirty MainCafe. Undo restores values but can leave Unity's
                    // scene dirty bit set, so restore that known-clean transaction precondition.
                    ClearKnownCleanSceneDirtiness(scene);
                    throw;
                }
                Debug.Log("P8R B style applied; existing instances, RectTransforms, callbacks and domain references retained.");
            }
            finally
            {
                AfterPrefabSaveForTests = null; AfterSceneStyleForTests = null;
                if (opened) EditorSceneManager.CloseScene(scene, !wasPresent);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }

        private static bool StyleBHierarchy(GameObject root, P8RAppearance appearance, bool apply = true)
        {
            var changed = false;
            var pickups = root.GetComponentsInChildren<DecorationCatalogueView>(true)
                .Select(view => Reference<Button>(view, "pickUpPointButton")).Where(button => button != null).ToArray();
            Sprite Replacement(Sprite sprite, bool primary = false)
            {
                if (sprite == null || P8RRefinedBAssets.PathFor(sprite.name) == null) return sprite;
                var key = primary ? sprite.name.Replace("button_secondary_", "button_primary_") : sprite.name;
                return Require<Sprite>(P8RRefinedBAssets.PathFor(key));
            }
            foreach (var image in root.GetComponentsInChildren<Image>(true))
            {
                var next = Replacement(image.sprite, pickups.Any(button => button.image == image));
                if (next == image.sprite) continue;
                changed = true; if (!apply) continue;
                image.sprite = next; EditorUtility.SetDirty(image); changed = true;
                if (PrefabUtility.IsPartOfPrefabInstance(image)) PrefabUtility.RecordPrefabInstancePropertyModifications(image);
            }
            foreach (var button in root.GetComponentsInChildren<Selectable>(true))
            {
                var old = button.spriteState;
                var primary = pickups.Any(pickup => pickup == button);
                var next = new SpriteState { highlightedSprite = Replacement(old.highlightedSprite, primary),
                    pressedSprite = Replacement(old.pressedSprite, primary), selectedSprite = Replacement(old.selectedSprite, primary), disabledSprite = Replacement(old.disabledSprite) };
                if (old.Equals(next)) continue;
                changed = true; if (!apply) continue;
                button.spriteState = next; EditorUtility.SetDirty(button); changed = true;
                if (PrefabUtility.IsPartOfPrefabInstance(button)) PrefabUtility.RecordPrefabInstancePropertyModifications(button);
            }
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var button = text.GetComponentInParent<Button>(true);
                var key = button != null && button.image != null && button.image.sprite != null ? button.image.sprite.name : "";
                var emphasize = button != null ? key == "tab_selected" || key.StartsWith("button_primary")
                    : text.fontStyle.HasFlag(FontStyles.Bold) || text.name.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0;
                var serialized = new SerializedObject(text);
                var colorCache = serialized.FindProperty("m_fontColor32");
                var desiredCache = (Color)(Color32)P8RAppearance.Cocoa;
                if (text.fontStyle == (emphasize ? FontStyles.Bold : FontStyles.Normal)
                    && text.characterSpacing == 0 && text.color == P8RAppearance.Cocoa && colorCache.colorValue == desiredCache) continue;
                changed = true; if (!apply) continue;
                appearance.Typography(text, emphasize);
                // TMP serializes its vertex-color cache separately and only warms it when rendering nonempty text.
                // 同步颜色缓存（含隐藏/空标签），不运行 layout、不移动现有控件。
                serialized.Update(); colorCache.colorValue = desiredCache;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(text); changed = true;
                if (PrefabUtility.IsPartOfPrefabInstance(text)) PrefabUtility.RecordPrefabInstancePropertyModifications(text);
            }
            return changed;
        }

        private static void Build(bool refresh, bool referenceLayoutOnly = false)
        {
            RequireClosedTargetPrefabStage();
            RequireCleanLoadedAssets();
            var appearance = Require<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            ValidateResources(appearance);
            var active = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(Phase8AssetPaths.MainCafeScenePath);
            var wasPresent = scene.IsValid();
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(Phase8AssetPaths.MainCafeScenePath, OpenSceneMode.Additive);
            try
            {
                if (scene.isDirty) throw new InvalidOperationException("Save MainCafe edits before P8R authoring.");
                var controller = Find<DecorationModeController>(scene);
                if (!HasP8RWiring(controller))
                    throw new InvalidOperationException("Complete UI authoring requires intact P8R Furniture wiring.");
                var complete = HasCompleteWiring(controller);
                if (referenceLayoutOnly)
                {
                    if (!complete) throw new InvalidOperationException("Reference layout refresh requires intact complete P8R wiring.");
                    RequireProtectedSceneStructure(controller);
                }
                if (complete && !refresh) return;
                if (HasAnyCompleteWiring(controller) && !complete)
                    throw new InvalidOperationException("Complete P8R wiring is partial. Repair references before authoring.");
                // Resolve every existing scene/prefab reference before mutating any target.
                var hud = Find<TimeControlPanel>(scene);
                var readiness = Reference<ValidationMessageView>(controller, "validationMessageView");
                var oldExit = Reference<DecorationExitModalView>(controller, "exitModalView");
                if (readiness == null || oldExit == null || hud == null || oldExit.gameObject.scene != scene)
                    throw new InvalidOperationException("MainCafe HUD, Readiness and Exit must all exist.");
                foreach (var name in new[] { "pauseButton", "normalButton", "fastButton" })
                    if (Reference<Button>(hud, name) == null) throw new InvalidOperationException("Missing HUD button: " + name);
                Require<GameObject>(Phase7AssetPaths.ExitModalPrefabPath);
                foreach (var path in new[] { P8RFurnitureUiPaths.CataloguePrefab, P8RFurnitureUiPaths.ActionPrefab, P8RFurnitureUiPaths.StorePrefab })
                    Require<GameObject>(path);

                ValidateSceneTargets(scene, controller, hud, readiness, oldExit);
                ValidatePrefabTargets();
                var disk = CaptureTargetBytes();
                try
                {

                RefreshCopy(P8RFurnitureUiPaths.CataloguePrefab, root => StyleCompleteCatalogue(root, appearance));
                RefreshCopy(P8RFurnitureUiPaths.ActionPrefab, root =>
                {
                    StyleActionBar(root, appearance);
                    // The source is authored hidden; briefly activate only these prefab contents so
                    // Unity saves its native layout before scene instances produce driven overrides.
                    var activeSelf = root.activeSelf;
                    try { root.SetActive(true); SettleTargetUi(root); }
                    finally { root.SetActive(activeSelf); }
                });
                if (!referenceLayoutOnly)
                {
                    RefreshCopy(P8RFurnitureUiPaths.StorePrefab, root => StyleModal(root, appearance));
                    var exitRoot = PrefabUtility.LoadPrefabContents(Phase7AssetPaths.ExitModalPrefabPath);
                    try
                    {
                        exitRoot.name = "PF_UI_P8RExitModal";
                        StyleExit(exitRoot, appearance);
                        PrefabUtility.SaveAsPrefabAsset(exitRoot, ExitPrefab);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(exitRoot); }
                }
                AfterPrefabSaveForTests?.Invoke();

                Undo.IncrementCurrentGroup();
                var undo = Undo.GetCurrentGroup();
                foreach (var root in scene.GetRootGameObjects()) Undo.RegisterFullObjectHierarchyUndo(root, "P8R complete UI");
                try
                {
                    StyleHud(hud, controller, appearance);
                    StyleReadiness(readiness, appearance);
                    if (!referenceLayoutOnly)
                    {
                        var exit = (GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(ExitPrefab), oldExit.transform.parent);
                        Undo.RegisterCreatedObjectUndo(exit, "P8R Exit");
                        exit.transform.SetSiblingIndex(oldExit.transform.GetSiblingIndex());
                        exit.SetActive(oldExit.gameObject.activeSelf);
                        Set(controller, "exitModalView", exit.GetComponent<DecorationExitModalView>());
                        Undo.DestroyObjectImmediate(oldExit.gameObject);
                    }
                    SettleTargetUi(hud.transform.root.gameObject);
                    AfterSceneStyleForTests?.Invoke();
                    if (!HasCompleteWiring(controller)) throw new InvalidOperationException("Complete P8R candidate wiring validation failed.");
                    SceneManager.SetActiveScene(scene);
                    var report = Phase8Validator.ValidateOpenScene();
                    if (report.Issues.Count != 0) throw new InvalidOperationException(string.Join("; ", report.Issues.Select(i => i.Code + ": " + i.Message)));
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save MainCafe.");
                    Undo.CollapseUndoOperations(undo);
                }
                catch { Undo.RevertAllDownToGroup(undo); throw; }
                Debug.Log("P8R complete existing UI authored and wired; ready for behavioral/visual verification.");
                }
                catch
                {
                    RestoreTargetBytes(disk);
                    throw;
                }
            }
            finally
            {
                AfterPrefabSaveForTests = null;
                AfterSceneStyleForTests = null;
                if (opened) EditorSceneManager.CloseScene(scene, !wasPresent);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }

        private static void SettleTargetUi(GameObject uiRoot)
        {
            // Warm only this target's TMP caches and native layout before serialization.
            // 只刷新目标UI，避免下一Editor frame才补缓存；不全局刷新或手写驱动几何。
            foreach (var text in uiRoot.GetComponentsInChildren<TMP_Text>(true))
            {
                text.textStyle = text.textStyle;
                text.ForceMeshUpdate(true, true);
            }
            foreach (var canvas in uiRoot.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.GetComponentInChildren<TMP_Text>(true) != null)
                    canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
                        | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
            }
            // A Canvas without an ILayoutController is not a layout root: UGUI skips its subtree.
            // 直接刷新目标内真正的layout roots，保留enabled/active规则和Unity驱动值。
            foreach (var rect in uiRoot.GetComponentsInChildren<Behaviour>(true)
                .Where(item => item is ILayoutController && item.isActiveAndEnabled)
                .Select(item => item.transform as RectTransform).Where(rect => rect != null).Distinct())
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            // Persist native layout results now, rather than adding prefab overrides next frame.
            // 只记录真实layout controller驱动的实例Rect，不记录根Canvas随Editor窗口变化的几何。
            foreach (var rect in uiRoot.GetComponentsInChildren<RectTransform>(true))
                if (rect.drivenByObject is ILayoutController && PrefabUtility.IsPartOfPrefabInstance(rect))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rect);
        }

        public static bool HasAnyCompleteWiring(DecorationModeController controller)
        {
            if (controller == null) return false;
            var scene = controller.gameObject.scene;
            return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Component>(true))
                .Where(c => c is TimeControlPanel || c is ValidationMessageView || c is DecorationExitModalView)
                .Any(c => new SerializedObject(c).FindProperty("appearance")?.objectReferenceValue != null
                    || SourcePath(c) == ExitPrefab);
        }

        public static bool HasCompleteWiring(DecorationModeController controller)
        {
            if (!HasP8RWiring(controller)) return false;
            var scene = controller.gameObject.scene;
            var appearance = Require<P8RAppearance>(P8RFurnitureUiPaths.Appearance);
            var hud = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TimeControlPanel>(true)).SingleOrDefault();
            var feedback = Reference<ValidationMessageView>(controller, "validationMessageView");
            var exit = Reference<DecorationExitModalView>(controller, "exitModalView");
            var range = Reference<DecorationFloorRangeView>(controller, "floorRangeView");
            if (hud == null || feedback == null || exit == null || range == null) return false;
            if (!Owns(scene, controller, "decorationModeButtonLabel", "validationMessageView", "exitModalView", "floorRangeView", "timeControlPanel")
                || !Owns(scene, hud, "pauseButton", "normalButton", "fastButton", "gameTimeService")
                || !Owns(scene, feedback, "messageLabel", "statusIcon")
                || !Owns(scene, exit, "titleLabel", "bodyLabel", "continueButton", "discardButton", "modalCard")
                || !Inside(hud, "pauseButton", "normalButton", "fastButton")
                || Reference<TimeControlPanel>(controller, "timeControlPanel") != hud
                || !Owns(scene, range, "wholeRoomButton", "singleGridButton") || !Inside(range, "wholeRoomButton", "singleGridButton")
                || !Inside(feedback, "messageLabel", "statusIcon")
                || !Inside(exit, "titleLabel", "bodyLabel", "continueButton", "discardButton", "modalCard")) return false;
            return Reference<P8RAppearance>(hud, "appearance") == appearance
                && Reference<P8RAppearance>(feedback, "appearance") == appearance
                && Reference<Image>(feedback, "statusIcon") != null
                && Reference<P8RAppearance>(exit, "appearance") == appearance
                && Reference<TMP_Text>(exit, "titleLabel") != null && Reference<TMP_Text>(exit, "bodyLabel") != null
                && Reference<P8RAppearance>(range, "appearance") == appearance
                && SourcePath(exit) == ExitPrefab;
        }

        // All legacy paths must recognize an intact P8R graph or refuse a partial graph.
        // 旧 authoring 只能保留完整 P8R；不完整连接必须拒绝。
        public static bool GuardLegacy(DecorationModeController controller)
        {
            if (!HasAnyP8RWiring(controller)) return false;
            if (!HasP8RWiring(controller) || HasAnyCompleteWiring(controller) && !HasCompleteWiring(controller))
                throw new InvalidOperationException("P8R wiring is incomplete; legacy authoring cannot overwrite it.");
            RequireProtectedSceneStructure(controller);
            return true;
        }

        private static void RequireProtectedSceneStructure(DecorationModeController controller)
        {
            var report = AnimalCafe.EditorTools.Phase6.Phase6DecorationValidator.ValidateP8RNonPresentation(controller.gameObject.scene);
            if (report.Issues.Count != 0)
                throw new InvalidOperationException("P8R scene structure is invalid: " + string.Join("; ", report.Issues.Select(issue => issue.ToString())));
            foreach (var field in new[] { "catalogueView", "actionBarView", "storeModalView" })
            {
                var view = Reference<Component>(controller, field);
                var source = PrefabUtility.GetCorrespondingObjectFromSource(view.gameObject);
                if (source == null || !PrefabStructure(view.transform).SequenceEqual(PrefabStructure(source.transform)))
                    throw new InvalidOperationException("P8R prefab hierarchy/components changed: " + field);
            }
            if (!HasAnyCompleteWiring(controller)) return;
            var feedback = Reference<ValidationMessageView>(controller, "validationMessageView");
            var layer = feedback.transform.parent;
            if (layer == null || layer.name != "Phase8_FeedbackLayer" || layer.GetComponent<SafeAreaContainer>() == null
                || layer.GetSiblingIndex() != 0 || layer.parent == null || layer.parent.name != "Screen Canvas")
                throw new InvalidOperationException("P8R readiness SafeArea parent/order is invalid; legacy authoring cannot repair it.");
            var hud = Reference<TimeControlPanel>(controller, "timeControlPanel");
            if (hud.transform.parent == null || hud.transform.parent.GetComponent<SafeAreaContainer>() == null
                || !hud.transform.Cast<Transform>().Select(child => child.name).SequenceEqual(new[]
                    { "DecorationModeButton", "GameTimeStatusIndicator", "PauseButton", "NormalButton", "FastButton", "P8RModeBadge" }))
                throw new InvalidOperationException("P8R HUD SafeArea/child order is invalid; legacy authoring cannot repair it.");
        }

        private static IEnumerable<string> PrefabStructure(Transform root)
        {
            string Relative(Transform item) => item == root ? string.Empty : Relative(item.parent) + "/" + item.name;
            return root.GetComponentsInChildren<Transform>(true).Select(item => Relative(item) + "|"
                + string.Join(",", item.GetComponents<Component>().Select(component => component == null ? "missing" : component.GetType().FullName)));
        }

        public static bool GuardLegacyMainCafe()
        {
            var active = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(Phase8AssetPaths.MainCafeScenePath);
            var wasPresent = scene.IsValid();
            var opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(Phase8AssetPaths.MainCafeScenePath, OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DecorationModeController>(true)).SingleOrDefault();
                if (!HasAnyP8RWiring(controller)) return false;
                if (scene.isDirty) throw new InvalidOperationException("MainCafe is dirty. Save its edits before authoring.");
                return GuardLegacy(controller);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, !wasPresent);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
            }
        }

        private static T Find<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<T>(true)).Single();

        private static bool Owns(Scene scene, Component owner, params string[] fields) => fields.All(name =>
            new SerializedObject(owner).FindProperty(name)?.objectReferenceValue is Component c && c.gameObject.scene == scene);
        private static bool Inside(Component owner, params string[] fields) => fields.All(name =>
            new SerializedObject(owner).FindProperty(name)?.objectReferenceValue is Component c && c.transform.IsChildOf(owner.transform));

        private static void ValidateSceneTargets(Scene scene, DecorationModeController controller, TimeControlPanel hud,
            ValidationMessageView readiness, DecorationExitModalView exit)
        {
            if (!Owns(scene, controller, "decorationModeButtonLabel", "validationMessageView", "exitModalView")
                || !Owns(scene, hud, "pauseButton", "normalButton", "fastButton", "gameTimeService")
                || !Owns(scene, readiness, "messageLabel") || !Inside(readiness, "messageLabel")
                || !Owns(scene, exit, "continueButton", "discardButton", "modalCard")
                || !Inside(exit, "continueButton", "discardButton", "modalCard"))
                throw new InvalidOperationException("P8R scene targets must be complete and owned by MainCafe and their UI hierarchy.");
            if (!Reference<TMP_Text>(controller, "decorationModeButtonLabel").transform.IsChildOf(hud.transform)
                || readiness.GetComponent<ScrollRect>()?.viewport == null || readiness.GetComponent<Image>() == null)
                throw new InvalidOperationException("P8R HUD or Readiness hierarchy is incomplete.");
        }

        private static void RequireClosedTargetPrefabStage()
        {
            // Prefab Mode objects are non-persistent; the loaded-asset dirty guard cannot protect them.
            // Prefab Mode 的编辑对象不是 persistent asset，必须在打开场景或写文件前独立检查。
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && new[] { P8RFurnitureUiPaths.CataloguePrefab, P8RFurnitureUiPaths.ActionPrefab,
                    P8RFurnitureUiPaths.StorePrefab, ExitPrefab }.Contains(stage.assetPath))
                throw new InvalidOperationException("Close Prefab Mode before P8R authoring: " + stage.assetPath);
        }

        private static void ValidatePrefabTargets()
        {
            var catalogue = Require<GameObject>(P8RFurnitureUiPaths.CataloguePrefab).GetComponent<DecorationCatalogueView>();
            var action = Require<GameObject>(P8RFurnitureUiPaths.ActionPrefab).GetComponent<DecorationActionBarView>();
            var modal = Require<GameObject>(P8RFurnitureUiPaths.StorePrefab).GetComponent<DecorationStoreModalView>();
            RequireRefs(catalogue, "expandedRoot", "collapsedHandleButton", "collapseButton", "pickUpPointButton", "categoryRowTemplate", "categoryTileTemplate");
            RequireRefs(Reference<DecorationCatalogueTileView>(catalogue, "categoryTileTemplate"), "nameLabel", "usingCheck", "previewOutline", "noneIcon");
            RequireRefs(catalogue.GetComponentInChildren<DecorationFloorRangeView>(true), "wholeRoomButton", "singleGridButton");
            RequireRefs(catalogue.GetComponentInChildren<DecorationModeTabsView>(true), "furnitureButton", "floorButton", "wallButton", "wallDecorButton");
            RequireRefs(action, "presentationRoot", "storeButton", "cancelButton", "rotateButton", "confirmButton", "undoLastButton", "applyAllButton", "feedbackRoot", "feedbackStateShape");
            RequireRefs(modal, "titleLabel", "bodyLabel", "confirmButton", "cancelButton", "modalBlocker");
            var exit = Require<GameObject>(Phase7AssetPaths.ExitModalPrefabPath).GetComponent<DecorationExitModalView>();
            RequireRefs(exit, "modalCard", "continueButton", "discardButton");
            if (Reference<RectTransform>(exit, "modalCard").Find("Prompt")?.GetComponent<TMP_Text>() == null)
                throw new InvalidOperationException("Missing source Exit title.");
        }

        private static void RequireRefs(Component owner, params string[] names)
        {
            if (owner == null) throw new InvalidOperationException("Missing P8R prefab component.");
            var so = new SerializedObject(owner);
            foreach (var name in names)
                if (so.FindProperty(name)?.objectReferenceValue == null)
                    throw new InvalidOperationException("Missing P8R target reference: " + owner.name + "." + name);
        }

        private static Dictionary<string, byte[]> CaptureTargetBytes()
        {
            var paths = new[] { P8RFurnitureUiPaths.CataloguePrefab, P8RFurnitureUiPaths.ActionPrefab,
                P8RFurnitureUiPaths.StorePrefab, ExitPrefab, Phase8AssetPaths.MainCafeScenePath, P8RFurnitureUiPaths.Appearance };
            return paths.SelectMany(p => new[] { p, p + ".meta" }).ToDictionary(p => p, p => File.Exists(p) ? File.ReadAllBytes(p) : null);
        }

        private static void RestoreTargetBytes(Dictionary<string, byte[]> before)
        {
            // Byte-for-byte transaction rollback of these exact targets; never restore HEAD/global assets.
            // 仅还原本次事务开始前的精确目标文件，不还原 HEAD 或全局 assets。
            foreach (var entry in before)
            {
                if (entry.Value == null)
                {
                    if (!entry.Key.EndsWith(".meta", StringComparison.Ordinal) && File.Exists(entry.Key)) AssetDatabase.DeleteAsset(entry.Key);
                }
                else if (!File.Exists(entry.Key) || !File.ReadAllBytes(entry.Key).SequenceEqual(entry.Value)) File.WriteAllBytes(entry.Key, entry.Value);
            }
            foreach (var path in before.Keys.Where(p => (p.EndsWith(".prefab", StringComparison.Ordinal) || p.EndsWith(".asset", StringComparison.Ordinal)) && before[p] != null))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static void ClearKnownCleanSceneDirtiness(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            // Unity 6000 exposes this native binding as internal, not as a public MarkSceneClean API.
            // Keep the reflection exact and fail loudly if a future Editor removes or changes it.
            var clear = typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",
                BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(Scene) }, null);
            if (clear == null)
                throw new MissingMethodException(typeof(EditorSceneManager).FullName, "ClearSceneDirtiness(Scene)");
            clear.Invoke(null, new object[] { scene });
        }

        internal static void StyleCompleteCatalogue(GameObject root, P8RAppearance appearance)
        {
            StyleCatalogue(root, appearance);
            var view = root.GetComponent<DecorationCatalogueView>();
            var range = root.GetComponentInChildren<DecorationFloorRangeView>(true);
            Set(range, "appearance", appearance);
            var fields = new[] { "wholeRoomButton", "singleGridButton" };
            for (var i = 0; i < fields.Length; i++)
            {
                var button = Reference<Button>(range, fields[i]);
                StyleButton(button, appearance, i == 0 ? "whole_room" : "single_grid", "secondary", false);
                var rect = (RectTransform)button.transform;
                rect.sizeDelta = new Vector2(232, 60);
                rect.anchoredPosition = new Vector2(i == 0 ? -122 : 122, 0);
                button.transform.Find("Label").GetComponent<TMP_Text>().fontSize = 24;
            }
            range.SetSelected(SurfaceEditScope.WholeRoomFloor);
            var tile = Reference<DecorationCatalogueTileView>(view, "categoryTileTemplate");
            StyleCatalogueCards(root, appearance);
            var none = Reference<GameObject>(tile, "noneIcon");
            appearance.Paint(none.GetComponent<Image>(), "none_cocoa", false);
            foreach (var label in none.GetComponentsInChildren<TMP_Text>(true)) label.gameObject.SetActive(false);
        }

        private static bool StyleCatalogueCards(GameObject root, P8RAppearance appearance)
        {
            var view = root.GetComponent<DecorationCatalogueView>();
            if (view == null) throw new InvalidOperationException("Catalogue prefab is missing DecorationCatalogueView.");
            var tile = Reference<DecorationCatalogueTileView>(view, "categoryTileTemplate");
            if (tile == null) throw new InvalidOperationException("Catalogue prefab is missing its card template.");
            var before = CaptureCatalogueCard(tile);

            appearance.Paint(tile.GetComponent<Image>(), "card_base");
            // A pressed card is not an active preview; actual preview state has its own overlay.
            tile.GetComponent<Button>().spriteState = new SpriteState { pressedSprite = appearance.Sprite("card_base") };
            var preview = Reference<GameObject>(tile, "previewOutline");
            var oldOutline = preview.GetComponent<Image>();
            if (oldOutline != null) oldOutline.enabled = false;
            foreach (var legacyName in new[] { "PreviewDash", "DashTop", "DashBottom", "DashLeft", "DashRight" })
            {
                var legacy = preview.transform.Find(legacyName);
                if (legacy != null) legacy.gameObject.SetActive(false);
            }
            var dashTransform = preview.transform.Find("RoundedDash");
            if (dashTransform == null)
            {
                var child = new GameObject("RoundedDash", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(P8RRoundedDashGraphic));
                dashTransform = child.transform;
                dashTransform.SetParent(preview.transform, false);
            }
            var dashRect = (RectTransform)dashTransform;
            Stretch(dashRect);
            dashTransform.SetAsLastSibling();
            var roundedDash = dashTransform.GetComponent<P8RRoundedDashGraphic>();
            roundedDash.enabled = true;
            // Preserve the approved terracotta preview cue while replacing the raster seams.
            roundedDash.Configure(new Color32(178, 86, 39, 255));
            return before != CaptureCatalogueCard(tile);
        }

        private static string CaptureCatalogueCard(DecorationCatalogueTileView tile)
        {
            return string.Join("\n", tile.GetComponentsInChildren<Component>(true).Where(component => component != null)
                .Select(component => string.Join("/", component.GetComponentsInParent<Transform>(true).Reverse()
                        .Select(parent => parent.name))
                    + "|" + component.GetType().FullName
                    + "|" + component.gameObject.activeSelf
                    + "|" + component.transform.GetSiblingIndex()
                    + "|" + EditorJsonUtility.ToJson(component)));
        }

        private static void StyleHud(TimeControlPanel hud, DecorationModeController controller, P8RAppearance appearance)
        {
            Prepare(hud.gameObject, appearance);
            Set(hud, "appearance", appearance);
            var badge = ImageChild(hud.transform, "P8RModeBadge");
            appearance.Paint(badge, "button_secondary_normal");
            badge.raycastTarget = false;
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = badge.rectTransform.pivot = new Vector2(0, 1);
            badge.rectTransform.anchoredPosition = Vector2.zero; badge.rectTransform.sizeDelta = new Vector2(200, 56);
            var badgeText = badge.GetComponentInChildren<TMP_Text>(true);
            if (badgeText == null)
            {
                var child = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                child.transform.SetParent(badge.transform, false); badgeText = child.GetComponent<TMP_Text>();
            }
            Stretch(badgeText.rectTransform); badgeText.font = appearance.Font; badgeText.fontSize = 28;
            badgeText.color = P8RAppearance.Cocoa; badgeText.alignment = TextAlignmentOptions.MidlineGeoAligned;
            badgeText.raycastTarget = false; badgeText.richText = false;
            Set(hud, "modeBadgeLabel", badgeText);
            var decorate = Reference<TMP_Text>(controller, "decorationModeButtonLabel").GetComponentInParent<Button>();
            // A disabled legacy component still runs Awake; detach its theme only on this P8R instance.
            // 避免旧 Awake 把透明点击区域重新涂成白底，不改旧版源 prefab。
            var legacyStyle = decorate.GetComponent<AnimalCafeButtonView>();
            if (legacyStyle != null) Set(legacyStyle, "theme", null);
            var face = ImageChild(decorate.transform, "Face"); face.transform.SetAsFirstSibling();
            var hit = decorate.GetComponent<Image>(); hit.color = Color.clear; hit.sprite = null;
            hit.raycastTarget = true; hit.alphaHitTestMinimumThreshold = 0f;
            decorate.targetGraphic = face;
            StyleButton(decorate, appearance, "decorate", "secondary", true);
            foreach (var pair in new[] { ("pauseButton", "pause"), ("normalButton", "clock"), ("fastButton", "fast_forward") })
            {
                var button = Reference<Button>(hud, pair.Item1);
                StyleButton(button, appearance, pair.Item2, "secondary", true);
            }
            var buttons = new[] { decorate, Reference<Button>(hud, "pauseButton"), Reference<Button>(hud, "normalButton"), Reference<Button>(hud, "fastButton") };
            for (var i = 0; i < buttons.Length; i++)
            {
                var rect = (RectTransform)buttons[i].transform;
                rect.sizeDelta = new Vector2(200, 68);
                rect.anchoredPosition = new Vector2(0, -i * 76f);
                buttons[i].transform.Find("Label").GetComponent<TMP_Text>().fontSize = 28;
            }
            var indicator = hud.GetComponentInChildren<GameTimeStatusIndicator>(true);
            if (indicator != null)
            {
                var marker = indicator.transform.Find("RotatingVisual")?.GetComponent<Image>();
                appearance.Paint(marker, "clock_cocoa", false);
                if (marker != null) marker.rectTransform.sizeDelta = Vector2.one * 40;
                ((RectTransform)indicator.transform).anchoredPosition = new Vector2(-220, 0);
            }
            hud.SetDecorationPauseLock(false);
            hud.RefreshP8RModeBadge(false);
            // New instance overrides must survive reopening MainCafe, not just this authoring session.
            // 保存新增底板引用与透明点击层；旧 prefab 本身保持不变。
            PrefabUtility.RecordPrefabInstancePropertyModifications(decorate);
            PrefabUtility.RecordPrefabInstancePropertyModifications(hit);
            PrefabUtility.RecordPrefabInstancePropertyModifications(decorate.transform.Find("Label").gameObject);
        }

        private static void StyleReadiness(ValidationMessageView view, P8RAppearance appearance)
        {
            Prepare(view.gameObject, appearance);
            Set(view, "appearance", appearance);
            appearance.Paint(view.GetComponent<Image>(), "notice_info");
            var label = Reference<TMP_Text>(view, "messageLabel");
            label.fontSize = 24;
            var readinessRect = (RectTransform)view.transform;
            readinessRect.anchorMin = readinessRect.anchorMax = readinessRect.pivot = new Vector2(0, 1);
            readinessRect.anchoredPosition = new Vector2(24, -168); readinessRect.sizeDelta = new Vector2(660, 72);
            var scroll = view.GetComponent<ScrollRect>();
            if (scroll != null)
            {
                scroll.viewport.offsetMin = new Vector2(72, 12);
                scroll.viewport.offsetMax = new Vector2(-28, -12);
                if (scroll.verticalScrollbar != null)
                {
                    appearance.Paint(scroll.verticalScrollbar.GetComponent<Image>(), "scroll_track", false);
                    appearance.Paint(scroll.verticalScrollbar.handleRect.GetComponent<Image>(), "scroll_thumb_normal");
                }
            }
            var icon = ImageChild(view.transform, "P8RStatusIcon");
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0, 1);
            icon.rectTransform.pivot = new Vector2(0, 1);
            icon.rectTransform.anchoredPosition = new Vector2(12, -12);
            icon.rectTransform.sizeDelta = Vector2.one * 40;
            appearance.Paint(icon, "status_info", false);
            P8RButtonLayout.StatusIcon(icon);
            Set(view, "statusIcon", icon);
        }

        private static void StyleExit(GameObject root, P8RAppearance appearance)
        {
            Prepare(root, appearance);
            var view = root.GetComponent<DecorationExitModalView>();
            Set(view, "appearance", appearance);
            var card = Reference<RectTransform>(view, "modalCard");
            appearance.Paint(card.GetComponent<Image>(), "panel_cream");
            card.anchorMin = card.anchorMax = card.pivot = Vector2.one * .5f;
            card.anchoredPosition = Vector2.zero; card.sizeDelta = new Vector2(840, 560);
            var title = card.Find("Prompt").GetComponent<TMP_Text>();
            PlaceModalText(title, 40, 184, 84); title.text = appearance.Text("exit.title");
            var bodyObject = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            bodyObject.transform.SetParent(card, false);
            var body = bodyObject.GetComponent<TMP_Text>(); body.font = appearance.Font; body.color = P8RAppearance.Cocoa;
            body.richText = false; body.raycastTarget = false;
            PlaceModalText(body, 34, 26, 224); body.text = appearance.Text("exit.body");
            Set(view, "titleLabel", title); Set(view, "bodyLabel", body);
            var buttons = new[] { Reference<Button>(view, "continueButton"), Reference<Button>(view, "discardButton") };
            for (var i = 0; i < buttons.Length; i++)
            {
                StyleButton(buttons[i], appearance, i == 0 ? "back" : "cancel", i == 0 ? "secondary" : "destructive", false);
                buttons[i].transform.Find("Icon").gameObject.SetActive(false);
                var rect = (RectTransform)buttons[i].transform;
                rect.anchoredPosition = new Vector2(i == 0 ? -190 : 190, -190); rect.sizeDelta = new Vector2(352, 88);
                var label = buttons[i].GetComponentInChildren<TMP_Text>(true);
                Stretch(label.rectTransform); label.rectTransform.offsetMin = new Vector2(8, 4); label.rectTransform.offsetMax = new Vector2(-8, -4);
                label.fontSize = 30; label.text = appearance.Text(i == 0 ? "exit.continue" : "exit.discard");
            }
            var backdrop = root.transform.Find("Backdrop").GetComponent<Image>();
            appearance.Paint(backdrop, "modal_scrim", false); backdrop.preserveAspect = false; backdrop.raycastTarget = true;
        }

        private static void ValidateResources(P8RAppearance appearance)
        {
            foreach (var key in new[] { "panel_cream", "tab_selected", "tab_idle", "tab_unavailable", "none_cocoa",
                "preview_dash_tile_h", "preview_dash_tile_v", "status_success", "status_warning", "status_error",
                "notice_success", "notice_warning", "notice_error", "modal_scrim", "clock_cocoa", "lock_muted" })
                appearance.Sprite(key);
            foreach (var key in new[] { "floor.whole_summary_one", "floor.grid_summary_one" })
                if (appearance.Text(key) == key) throw new InvalidOperationException("Missing P8R text: " + key);
            foreach (var key in new[] { "category.furniture", "category.cash-register", "category.coffee-machine", "category.floor", "category.wallpaper", "category.paint", "category.wainscoting", "category.wall-decor", "category.windows", "action.apply", "action.undo", "action.apply_all", "action.whole_room", "action.single_grid", "action.decorate", "action.exit", "action.pause", "action.resume", "action.clock", "action.fast_forward", "action.lock", "item.wall-decor.monitor.01", "item.wall-decor.shiba-painting.01", "item.wall-decor.wood-shelf.01", "item.window.canonical.phase4", "item.window.tall-glass.1x2.01", "surface.ready", "surface.choose", "floor.whole_summary", "floor.grid_summary", "wall.Overlap", "wall.OutOfBounds", "wall.CrossCorner", "wall.SurfaceMissing", "wall.SurfaceMismatch", "feedback.Occupied", "feedback.OutsideUnlockedArea", "feedback.Locked", "feedback.Blocked", "feedback.EntranceClearance", "feedback.UnsupportedSurface", "feedback.MissingInstance", "feedback.WallOverlap", "feedback.WallOutOfBounds", "feedback.WallCrossCorner", "feedback.WallSurfaceMissing", "feedback.SelectWallTarget", "feedback.SelectFloorGridTarget", "feedback.NoValidInteractionAnchor", "exit.title", "exit.body", "exit.continue", "exit.discard", "readiness.ready", "readiness.warning", "readiness.blocked", "readiness.blocking", "readiness.warning_label", "readiness.layout", "readiness.Employee", "readiness.Customer", "readiness.CashRegister", "readiness.CoffeeMachine", "readiness.PickUpPoint", "readiness.expand", "readiness.collapse", "readiness.MissingCashRegister", "readiness.MissingCoffeeMachine", "readiness.MissingPickUpPoint", "readiness.MissingSupportFurniture", "readiness.MissingSurfaceSlot", "readiness.DuplicateSurfaceOccupancy", "readiness.AnchorOutOfBounds", "readiness.AnchorBlocked", "readiness.AnchorUnreachable", "readiness.NoCompleteReachableServiceCombination", "readiness.MissingFunctionalDirection", "readiness.InvalidFunctionalDefinition" })
                if (appearance.Text(key) == key) throw new InvalidOperationException("Missing P8R text: " + key);
        }
    }
}

using System.Linq;
using System.Collections;
using System.Reflection;
using AnimalCafe.Decoration;
using AnimalCafe.Core.Time;
using AnimalCafe.Layout;
using AnimalCafe.UI;
using AnimalCafe.UI.Feedback;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RCompleteUiTests
    {
        private const string Root = "Assets/UI/P8R/";
        [Test]
        public void Polish_PrepareDisablesBothLegacyHighlightNamesWithoutDisablingOtherArt()
        {
            var root = new GameObject("Local P8R fixture");
            try
            {
                foreach (var name in new[] { "Top Highlight", "TopHighlight", "Icon" })
                    new GameObject(name, typeof(RectTransform), typeof(Image)).transform.SetParent(root.transform, false);
                typeof(AnimalCafe.EditorTools.P8R.P8RFurnitureUiBuilder).GetMethod("Prepare", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, new object[] { root, AssetDatabase.LoadAssetAtPath<P8RAppearance>(Root + "P8RAppearance.asset") });
                Assert.That(root.transform.Find("Top Highlight").gameObject.activeSelf, Is.False);
                Assert.That(root.transform.Find("TopHighlight").gameObject.activeSelf, Is.False);
                Assert.That(root.transform.Find("Icon").gameObject.activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ReferenceRefresh_PreservesExistingExitInstanceAndOverrides()
        {
            const string path = "Assets/Scenes/MainCafe.unity";
            var original = System.IO.File.ReadAllBytes(path);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DecorationModeController>(true)).Single();
                var exit = Field<DecorationExitModalView>(controller, "exitModalView");
                var image = Field<RectTransform>(exit, "modalCard").GetComponent<Image>();
                image.color = Color.magenta; PrefabUtility.RecordPrefabInstancePropertyModifications(image);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                var overrides = PrefabUtility.GetPropertyModifications(exit.gameObject).Select(m => m.propertyPath + "=" + m.value).OrderBy(x => x).ToArray();
                var hook = typeof(AnimalCafe.EditorTools.P8R.P8RCompleteUiBuilder).GetField("AfterSceneStyleForTests", BindingFlags.NonPublic | BindingFlags.Static);
                hook.SetValue(null, (System.Action)(() =>
                {
                    Assert.That(Field<DecorationExitModalView>(controller, "exitModalView"), Is.SameAs(exit));
                    Assert.That(image.color, Is.EqualTo(Color.magenta));
                    Assert.That(PrefabUtility.GetPropertyModifications(exit.gameObject).Select(m => m.propertyPath + "=" + m.value).OrderBy(x => x), Is.EqualTo(overrides));
                    throw new System.InvalidOperationException("Reference preservation checked");
                }));
                Assert.That(() => AnimalCafe.EditorTools.P8R.P8RCompleteUiBuilder.RefreshApprovedReferenceLayout(),
                    Throws.TypeOf<System.InvalidOperationException>().With.Message.EqualTo("Reference preservation checked"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                System.IO.File.WriteAllBytes(path, original); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            }
        }
        [TestCase("exitModalView", "discardButton")]
        [TestCase("exitModalView", "continueButton")]
        [TestCase("exitModalView", "modalCard")]
        [TestCase("validationMessageView", "messageLabel")]
        public void MissingInteractiveReference_IsNeverAcceptedAsComplete(string component, string field)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainCafe.unity", OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DecorationModeController>(true)).Single();
                var owner = new SerializedObject(controller).FindProperty(component).objectReferenceValue;
                var so = new SerializedObject(owner); so.FindProperty(field).objectReferenceValue = null; so.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(AnimalCafe.EditorTools.P8R.P8RCompleteUiBuilder.HasCompleteWiring(controller), Is.False);
                Assert.That(() => AnimalCafe.EditorTools.P8R.P8RCompleteUiBuilder.GuardLegacy(controller), Throws.InvalidOperationException);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        [Test]
        public void HudSpeedAndDecorationLock_RefreshPngStatesFromEvents()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainCafe.unity", OpenSceneMode.Additive);
            try
            {
                var panel = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TimeControlPanel>(true)).Single();
                var service = Field<GameTimeService>(panel, "gameTimeService");
                typeof(GameTimeService).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(service, null);
                panel.Configure(service, Field<Button>(panel, "pauseButton"), Field<Button>(panel, "normalButton"), Field<Button>(panel, "fastButton"));
                service.SetFast();
                Assert.That(Field<Button>(panel, "fastButton").image.sprite?.name, Is.EqualTo("tab_selected"));
                panel.SetDecorationPauseLock(true);
                Assert.That(Field<Button>(panel, "pauseButton").GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("Paused"));
                Assert.That(Field<Button>(panel, "fastButton").interactable, Is.False);
                panel.SetDecorationPauseLock(false);
                service.SetNormal();
                Assert.That(Field<Button>(panel, "normalButton").image.sprite?.name, Is.EqualTo("tab_selected"));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void Readiness_EnglishDisclosureRetainsEveryCauseAndDetachedId()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainCafe.unity", OpenSceneMode.Additive);
            try
            {
                var view = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ValidationMessageView>(true)).Single();
                var failures = System.Enum.GetValues(typeof(LayoutReadinessFailureCode)).Cast<LayoutReadinessFailureCode>()
                    .Select((code, i) => Construct<LayoutReadinessFailure>(i % 2 == 0 ? LayoutReadinessSeverity.Blocking : LayoutReadinessSeverity.Warning,
                        code, (LayoutStationType?)LayoutStationType.CoffeeMachine, "private-" + i, "support-id", "slot-id",
                        (InteractionRole?)InteractionRole.Employee, (GridPosition?)new GridPosition(i, 2), "diagnostic-only")).ToArray();
                var summary = Construct<LayoutReadinessSummary>(0, 0);
                view.ShowReadiness(Construct<LayoutReadinessReport>(false, new StationReadiness[0], failures, summary, summary, summary));
                Assert.That(view.CurrentMessage, Does.StartWith("Confirmed Layout:"));
                Assert.That(view.FullReadinessMessage.Split('\n').Length, Is.GreaterThanOrEqualTo(13));
                Assert.That(view.FullReadinessMessage, Does.Contain("Coffee Machine / Employee (11, 2)"));
                Assert.That(view.FullReadinessMessage, Does.Not.Contain("private-"));
                Assert.That(view.DiagnosticIds, Does.Contain("private-11"));
                Assert.That(Field<Button>(view, "disclosureButton").image.sprite?.name, Is.EqualTo("button_secondary_normal"));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void ExitModal_ExplainsOnlyPreviewWillBeDiscarded()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/PF_UI_P8RExitModal.prefab");
            Assert.That(prefab, Is.Not.Null, "Remaining Exit presentation has not been authored.");
            var root = Object.Instantiate(prefab);
            try
            {
                var view = root.GetComponent<DecorationExitModalView>();
                view.Show();
                Assert.That(root.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text), Does.Contain("Discard this preview?"));
                Assert.That(root.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text), Does.Contain("Only this preview will be discarded. Your applied changes will stay."));
                Assert.That(Field<Button>(view, "discardButton").image.sprite?.name, Is.EqualTo("button_destructive_normal"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static T Construct<T>(params object[] args) => (T)System.Activator.CreateInstance(typeof(T),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null);

        [Test]
        public void LegacyAuthoringHelpers_DoNotDowngradeCompleteP8R()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainCafe.unity", OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DecorationModeController>(true)).Single();
                var feedback = Field<ValidationMessageView>(controller, "validationMessageView");
                var before = EditorJsonUtility.ToJson(feedback.GetComponent<Image>());
                Assert.That(AnimalCafe.EditorTools.Phase8.Phase8FeedbackAssets.ConfigureMessageView(feedback), Is.False);
                Assert.That(EditorJsonUtility.ToJson(feedback.GetComponent<Image>()), Is.EqualTo(before));
                var exit = Field<DecorationExitModalView>(controller, "exitModalView");
                var card = Field<RectTransform>(exit, "modalCard");
                before = EditorJsonUtility.ToJson(card);
                typeof(AnimalCafe.EditorTools.Phase7.Phase7SurfaceAssetBuilder).GetMethod("LayoutExitModal",
                    BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { exit });
                Assert.That(EditorJsonUtility.ToJson(card), Is.EqualTo(before));
                var hud = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TimeControlPanel>(true)).Single();
                var toggle = Field<TMP_Text>(controller, "decorationModeButtonLabel").GetComponentInParent<Button>().gameObject;
                var rail = typeof(AnimalCafe.EditorTools.Phase6.Phase6DecorationSceneSetup).GetMethod("EnsureDecorationRightRail", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(rail.Invoke(null, new object[] { scene, hud.transform.parent, toggle, Field<GameTimeService>(hud, "gameTimeService"), null }), Is.False);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        [TestCase("floor.dark-stone", DecorationCatalogueItemKind.Floor, "TH_floor_dark_stone")]
        [TestCase("floor.light-tile", DecorationCatalogueItemKind.Floor, "TH_floor_light_tile")]
        [TestCase("floor.warm-wood", DecorationCatalogueItemKind.Floor, "TH_floor_warm_wood")]
        [TestCase("paint.cream", DecorationCatalogueItemKind.WallSurface, "TH_paint_cream")]
        [TestCase("paint.sage", DecorationCatalogueItemKind.WallSurface, "TH_paint_sage")]
        [TestCase("paint.terracotta", DecorationCatalogueItemKind.WallSurface, "TH_paint_terracotta")]
        [TestCase("wallpaper.cream-floral", DecorationCatalogueItemKind.WallSurface, "TH_wallpaper_cream_floral")]
        [TestCase("wallpaper.sage-sprig", DecorationCatalogueItemKind.WallSurface, "TH_wallpaper_sage_sprig")]
        [TestCase("wainscoting.none", DecorationCatalogueItemKind.WallSurface, "TH_wainscoting_none")]
        [TestCase("wainscoting.sage-plain", DecorationCatalogueItemKind.WallSurface, "TH_wainscoting_sage_plain")]
        [TestCase("wainscoting.warm-white-rail", DecorationCatalogueItemKind.WallSurface, "TH_wainscoting_warm_white_rail")]
        [TestCase("wall-decor.monitor.01", DecorationCatalogueItemKind.WallMounted, "TH_wall_decor_monitor_01")]
        [TestCase("wall-decor.shiba-painting.01", DecorationCatalogueItemKind.WallMounted, "TH_wall_decor_shiba_painting_01")]
        [TestCase("wall-decor.wood-shelf.01", DecorationCatalogueItemKind.WallMounted, "TH_wall_decor_wood_shelf_01")]
        [TestCase("window.canonical.phase4", DecorationCatalogueItemKind.WallMounted, "TH_window_canonical_phase4")]
        [TestCase("window.tall-glass.1x2.01", DecorationCatalogueItemKind.WallMounted, "TH_window_tall_glass_1x2_01")]
        public void StableIdThumbnail_OverridesOnlyPresentation(string id, DecorationCatalogueItemKind kind, string expected)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/PF_UI_P8RCatalogue.prefab"));
            try
            {
                var tile = root.GetComponentsInChildren<DecorationCatalogueTileView>(true).First(t => Field<P8RAppearance>(t, "appearance") != null);
                var item = new DecorationCatalogueItemModel(id, "Legacy name", null, kind, false);
                tile.Bind(item, null);
                Assert.That(Field<Image>(tile, "thumbnailImage").sprite?.name, Is.EqualTo(expected));
                Assert.That(item.Thumbnail, Is.Null, "Display binding must not mutate domain data.");
                AssertVisibleNameFits(tile);
                tile.SetSurfaceState(true, true);
                Assert.That(Field<GameObject>(tile, "usingCheck").activeSelf, Is.True);
                Assert.That(Field<GameObject>(tile, "previewOutline").activeSelf, Is.True);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void FloorRange_SelectedTabDoesNotLookDisabled()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/PF_UI_P8RCatalogue.prefab"));
            try
            {
                var range = root.GetComponentInChildren<DecorationFloorRangeView>(true);
                range.SetSelected(SurfaceEditScope.SingleGridFloor);
                var selected = Field<Button>(range, "singleGridButton");
                Assert.That(selected.image.sprite?.name, Is.EqualTo("tab_selected"));
                Assert.That(selected.spriteState.disabledSprite?.name, Is.EqualTo("tab_selected"));
                Assert.That(selected.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("Single Grid"));
                range.SetSelected(SurfaceEditScope.WholeRoomFloor);
                Assert.That(selected.image.sprite?.name, Is.EqualTo("tab_idle"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SurfaceFooter_UsesApplyAndEnglishUtilities()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/PF_UI_P8RActionBar.prefab"));
            try
            {
                var view = root.GetComponent<DecorationActionBarView>();
                view.SetModeActions(DecorationModeKind.Floor, false);
                Assert.That(Field<Button>(view, "confirmButton").transform.Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo("Apply"));
                Assert.That(Field<Button>(view, "undoLastButton").transform.Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo("Undo"));
                Assert.That(Field<Button>(view, "applyAllButton").transform.Find("Label").GetComponent<TMP_Text>().text, Is.EqualTo("Apply All"));
                var appearance = Field<P8RAppearance>(view, "appearance");
                foreach (var scope in new[] { "whole", "grid" })
                {
                    var prefix = scope == "whole" ? "Whole Room" : "Single Grid";
                    Assert.That(appearance.Text("floor." + scope + "_summary_one").Replace("{count}", "1"),
                        Does.StartWith(prefix + " - 1 tile changed\n"));
                    foreach (var count in new[] { 0, 2, 64 })
                        Assert.That(appearance.Text("floor." + scope + "_summary").Replace("{count}", count.ToString()),
                            Does.StartWith(prefix + " - " + count + " tiles changed\n"));
                }
            }
            finally { Object.DestroyImmediate(root); }
        }
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);

        private static void AssertVisibleNameFits(DecorationCatalogueTileView tile)
        {
            var label = Field<TMP_Text>(tile, "nameLabel");
            if (!label.gameObject.activeSelf) return;
            Assert.That(label.GetPreferredValues(label.text, label.rectTransform.rect.width, Mathf.Infinity).y,
                Is.LessThanOrEqualTo(label.rectTransform.rect.height + .1f), label.text + " must fit without ellipsis/truncation.");
        }

        [Test]
        public void SixFurnitureBindings_RetainExactApprovedSpritesAndCompleteNames()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/PF_UI_P8RCatalogue.prefab"));
            try
            {
                var tile = root.GetComponentsInChildren<DecorationCatalogueTileView>(true).First(t => Field<P8RAppearance>(t, "appearance") != null);
                var catalogue = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(Root + "DC_P8RFurniture.asset");
                Assert.That(catalogue.Entries.Count, Is.EqualTo(6));
                foreach (var entry in catalogue.Entries)
                {
                    tile.Bind(new DecorationCatalogueItemModel(entry.Definition.DefinitionId, entry.Definition.DisplayName,
                        entry.Thumbnail, DecorationCatalogueItemKind.Furniture, false, entry.Definition), null);
                    Assert.That(Field<Image>(tile, "thumbnailImage").sprite, Is.SameAs(entry.Thumbnail));
                    Assert.That(tile.Definition, Is.SameAs(entry.Definition));
                    AssertVisibleNameFits(tile);
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CompleteBuild_RerunKeepsAllExactTargetBytesAndSceneSetup()
        {
            var paths = new[] { Root + "Prefabs/PF_UI_P8RCatalogue.prefab", Root + "Prefabs/PF_UI_P8RActionBar.prefab",
                Root + "Prefabs/PF_UI_P8RPutAwayModal.prefab", Root + "Prefabs/PF_UI_P8RExitModal.prefab", "Assets/Scenes/MainCafe.unity" }
                .SelectMany(p => new[] { p, p + ".meta" }).ToArray();
            var before = paths.ToDictionary(p => p, System.IO.File.ReadAllBytes);
            var setup = EditorSceneManager.GetSceneManagerSetup().Select(s => s.path + "|" + s.isLoaded + "|" + s.isActive).ToArray();
            AnimalCafe.EditorTools.P8R.P8RCompleteUiBuilder.BuildApprovedCompleteUi();
            AnimalCafe.EditorTools.P8R.P8RCompleteUiBuilder.BuildApprovedCompleteUi();
            foreach (var path in paths) Assert.That(System.IO.File.ReadAllBytes(path), Is.EqualTo(before[path]), path);
            Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(s => s.path + "|" + s.isLoaded + "|" + s.isActive), Is.EqualTo(setup));
        }

        [UnityTest]
        public IEnumerator AuthoredCompleteUi_ColdAndRepeatedFrameLoadKeepsSerializedBytesStable()
        {
            AnimalCafe.EditorTools.P8R.P8RFurnitureUiBuilder.RequireCleanLoadedAssets();
            var folder = "Assets/__P8RColdLoad_" + System.Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(folder));
            var path = folder + "/Source.unity";
            var renderedPath = folder + "/Rendered.unity";
            var active = SceneManager.GetActiveScene();
            var selection = Selection.objects;
            var selected = Selection.activeObject;
            Scene scene = default;
            try
            {
                Assert.That(AssetDatabase.CopyAsset("Assets/Scenes/MainCafe.unity", path), Is.True);
                var before = System.IO.File.ReadAllBytes(path);
                for (var i = 0; i < 2; i++)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    yield return null;
                    var dirtyBeforeSave = scene.isDirty;
                    // Serialize only a test-owned copy; driven Rect values are serialized by Unity itself.
                    // 只序列化测试副本，验证实际TMP缓存与驱动布局，不手写几何期待。
                    AnimalCafe.EditorTools.P8R.P8RFurnitureUiBuilder.RequireCleanLoadedAssets();
                    Assert.That(EditorSceneManager.SaveScene(scene, renderedPath, true), Is.True);
                    var serialized = System.IO.File.ReadAllBytes(renderedPath);
                    if (dirtyBeforeSave || !serialized.SequenceEqual(before))
                    {
                        const string evidence = ".superpowers/sdd/2026-09-11-p8r-all-ui/scene-load-diagnostic/";
                        System.IO.Directory.CreateDirectory(evidence);
                        System.IO.File.WriteAllBytes(evidence + "final-cold-source.unity", before);
                        System.IO.File.WriteAllBytes(evidence + "final-cold-loaded.unity", serialized);
                    }
                    Assert.That(new[] { dirtyBeforeSave, !serialized.SequenceEqual(before) }, Is.EqualTo(new[] { false, false }),
                        "Cold/repeated UI must preserve both dirty state and exact serialized bytes, iteration=" + i
                        + "; dirty=" + dirtyBeforeSave + "; serializedEqual=" + serialized.SequenceEqual(before));
                    Assert.That(EditorSceneManager.CloseScene(scene, true), Is.True);
                }
            }
            finally
            {
                if (scene.IsValid()) EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(folder);
                if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);
                Selection.objects = selection;
                Selection.activeObject = selected;
            }
        }

        private static string[] CapturePrefabOverrides(Scene scene) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(t => PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
            .SelectMany(t => (PrefabUtility.GetPropertyModifications(t.gameObject) ?? new PropertyModification[0])
                .Select(mod => GlobalObjectId.GetGlobalObjectIdSlow(t.gameObject) + ":" + GlobalObjectId.GetGlobalObjectIdSlow(mod.target)
                    + "/" + mod.propertyPath + "=" + mod.value + "/" + GlobalObjectId.GetGlobalObjectIdSlow(mod.objectReference)))
            .OrderBy(row => row).ToArray();

        [Test]
        public void FailedRefresh_RollsBackEverySavedPrefabAndSceneByte()
        {
            var paths = new[] { Root + "Prefabs/PF_UI_P8RCatalogue.prefab", Root + "Prefabs/PF_UI_P8RActionBar.prefab",
                Root + "Prefabs/PF_UI_P8RPutAwayModal.prefab", Root + "Prefabs/PF_UI_P8RExitModal.prefab", "Assets/Scenes/MainCafe.unity" };
            var original = paths.ToDictionary(p => p, System.IO.File.ReadAllBytes);
            var modalPath = paths[2];
            try
            {
                var modalRoot = PrefabUtility.LoadPrefabContents(modalPath);
                try
                {
                    modalRoot.GetComponent<DecorationStoreModalView>().ContentRect.GetComponent<Image>().color = Color.magenta;
                    PrefabUtility.SaveAsPrefabAsset(modalRoot, modalPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(modalRoot); }
                var before = paths.ToDictionary(p => p, System.IO.File.ReadAllBytes);
                var setup = EditorSceneManager.GetSceneManagerSetup().Select(s => s.path + "|" + s.isLoaded + "|" + s.isActive).ToArray();
                var hook = typeof(AnimalCafe.EditorTools.P8R.P8RCompleteUiBuilder).GetField("AfterPrefabSaveForTests", BindingFlags.NonPublic | BindingFlags.Static);
                hook.SetValue(null, (System.Action)(() => throw new System.InvalidOperationException("Injected failure after prefab saves")));
                Assert.That(() => AnimalCafe.EditorTools.P8R.P8RCompleteUiBuilder.RefreshApprovedCompleteUi(), Throws.InvalidOperationException);
                foreach (var path in paths) Assert.That(System.IO.File.ReadAllBytes(path), Is.EqualTo(before[path]), path);
                Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(s => s.path + "|" + s.isLoaded + "|" + s.isActive), Is.EqualTo(setup));
            }
            finally
            {
                foreach (var entry in original)
                {
                    System.IO.File.WriteAllBytes(entry.Key, entry.Value);
                    AssetDatabase.ImportAsset(entry.Key, ImportAssetOptions.ForceSynchronousImport);
                }
            }
        }

        [Test]
        public void LateFailedRefresh_RestoresLoadedHierarchyDirtyFlagSetupAndBytes()
        {
            var paths = new[] { Root + "Prefabs/PF_UI_P8RCatalogue.prefab", Root + "Prefabs/PF_UI_P8RActionBar.prefab",
                Root + "Prefabs/PF_UI_P8RPutAwayModal.prefab", Root + "Prefabs/PF_UI_P8RExitModal.prefab", "Assets/Scenes/MainCafe.unity" };
            var original = paths.ToDictionary(p => p, System.IO.File.ReadAllBytes);
            var scene = EditorSceneManager.OpenScene(paths[4], OpenSceneMode.Additive);
            try
            {
                // Simulate an intact, clean pre-style hierarchy whose status icon needs creation.
                // 仅本测试目标 scene：保留引用，但使用不同名称，迫使 builder 创建新 child。
                var readiness = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ValidationMessageView>(true)).Single();
                Field<Image>(readiness, "statusIcon").name = "BeforeStatusIndicator";
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(EditorSceneManager.SaveScene(scene), Is.True);
                Canvas.ForceUpdateCanvases();
                var before = paths.ToDictionary(p => p, System.IO.File.ReadAllBytes);
                var hierarchy = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .Select(HierarchyEntry).OrderBy(s => s).ToArray();
                var prefabOverrides = CapturePrefabOverrides(scene);
                var setup = EditorSceneManager.GetSceneManagerSetup().Select(s => s.path + "|" + s.isLoaded + "|" + s.isActive).ToArray();
                var hook = typeof(AnimalCafe.EditorTools.P8R.P8RCompleteUiBuilder).GetField("AfterSceneStyleForTests", BindingFlags.NonPublic | BindingFlags.Static);
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DecorationModeController>(true)).Single();
                var oldExit = Field<DecorationExitModalView>(controller, "exitModalView");
                var reachedHook = false;
                hook.SetValue(null, (System.Action)(() =>
                {
                    reachedHook = true;
                    Assert.That(readiness.transform.Find("P8RStatusIcon"), Is.Not.Null, "Real styling must have created the new child.");
                    Assert.That(Field<Image>(readiness, "statusIcon").name, Is.EqualTo("P8RStatusIcon"));
                    Assert.That(oldExit == null, Is.True, "Old Exit must already be replaced.");
                    Assert.That(Field<DecorationExitModalView>(controller, "exitModalView"), Is.Not.Null);
                    throw new System.InvalidOperationException("Injected late scene styling failure");
                }));
                Assert.That(() => AnimalCafe.EditorTools.P8R.P8RCompleteUiBuilder.RefreshApprovedCompleteUi(),
                    Throws.TypeOf<System.InvalidOperationException>().With.Message.EqualTo("Injected late scene styling failure"));
                Assert.That(reachedHook, Is.True);
                Canvas.ForceUpdateCanvases();
                Assert.That(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .Select(HierarchyEntry).OrderBy(s => s), Is.EqualTo(hierarchy));
                Assert.That(CapturePrefabOverrides(scene), Is.EqualTo(prefabOverrides));
                Assert.That(scene.isDirty, Is.False, "A failed migration must not dirty an originally clean loaded scene.");
                foreach (var path in paths) Assert.That(System.IO.File.ReadAllBytes(path), Is.EqualTo(before[path]), path);
                Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(s => s.path + "|" + s.isLoaded + "|" + s.isActive), Is.EqualTo(setup));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                foreach (var entry in original)
                {
                    System.IO.File.WriteAllBytes(entry.Key, entry.Value);
                    AssetDatabase.ImportAsset(entry.Key, ImportAssetOptions.ForceSynchronousImport);
                }
            }
        }

        private static string HierarchyEntry(Transform t)
        {
            var canvas = t.GetComponent<Canvas>();
            // Only root screen-space Canvas transforms are render-mode driven, not authored layout.
            // 仅 root screen-space Canvas 的派生 Transform 不比较；其序列化设置和全部 children 严格比较。
            var drivenRoot = canvas != null && canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace;
            var value = string.Join("/", t.GetComponentsInParent<Transform>(true).Reverse().Select(p => p.name))
                + "|" + t.gameObject.activeSelf + "|" + t.GetSiblingIndex()
                + "|" + string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().FullName).OrderBy(s => s))
                + (drivenRoot ? "|driven root" : "|" + t.localPosition + "|" + t.localScale
                    + (t is RectTransform rect ? "|" + rect.anchorMin + "|" + rect.anchorMax + "|" + rect.offsetMin + "|" + rect.offsetMax : ""))
                + (t.TryGetComponent<Image>(out var image) ? "|" + (image.sprite ? image.sprite.name : "") + "|" + image.color + "|" + image.raycastTarget : "")
                + (t.TryGetComponent<Button>(out var button) ? "|" + button.interactable + "|" + button.transition : "")
                + (t.TryGetComponent<TMP_Text>(out var text) ? "|" + text.text + "|" + text.fontSize + "|" + text.color : "");
            if (canvas != null) value += "|" + canvas.renderMode + "|" + canvas.enabled + "|" + canvas.pixelPerfect
                + "|" + canvas.planeDistance + "|" + canvas.overrideSorting + "|" + canvas.sortingLayerID + "|" + canvas.sortingOrder;
            if (t.TryGetComponent<CanvasScaler>(out var scaler)) value += "|" + scaler.enabled + "|" + scaler.uiScaleMode
                + "|" + scaler.referenceResolution + "|" + scaler.screenMatchMode + "|" + scaler.matchWidthOrHeight
                + "|" + scaler.scaleFactor + "|" + scaler.referencePixelsPerUnit + "|" + scaler.physicalUnit
                + "|" + scaler.defaultSpriteDPI + "|" + scaler.fallbackScreenDPI + "|" + scaler.dynamicPixelsPerUnit;
            return value;
        }

        [Test]
        public void EditingContext_ResizeKeepsExplanationHiddenAndReturnButtonInside()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/PF_UI_P8RCatalogue.prefab"));
            try
            {
                var rect = (RectTransform)root.transform;
                rect.sizeDelta = new Vector2(1600, 1920);
                var catalogue = root.GetComponent<DecorationCatalogueView>();
                catalogue.SetEditingContext("Current Preview: Coffee Machine - Not yet applied\nMove this item onto a counter surface slot. Apply or cancel your current preview before changing categories.", true);
                catalogue.ExplainEditingRestriction();
                rect.sizeDelta = new Vector2(840, 1920);
                typeof(DecorationCatalogueView).GetMethod("ApplyResponsiveExpandedBounds", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(catalogue, null);
                var label = Field<TMP_Text>(catalogue, "editingContextLabel");
                Assert.That(label.gameObject.activeSelf, Is.False);
                var back = Field<Button>(catalogue, "returnToEditingButton");
                Assert.That(back.gameObject.activeSelf, Is.True);
                var parent = (RectTransform)back.transform.parent;
                Assert.That(((RectTransform)back.transform).rect.width, Is.LessThan(parent.rect.width));
                Assert.That(parent.GetComponent<Image>().enabled, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CataloguePreview_UsesOneRoundedDashGraphicWithoutLegacyBorderLayers()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/PF_UI_P8RCatalogue.prefab"));
            try
            {
                var tile = root.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                    .First(t => Field<P8RAppearance>(t, "appearance") != null);
                var preview = Field<GameObject>(tile, "previewOutline");
                var roundedDashType = FindRoundedDashType();

                Assert.That(roundedDashType, Is.Not.Null,
                    "The preview frame needs one code-native rounded perimeter Graphic.");
                var roundedDashes = preview.GetComponentsInChildren(roundedDashType, true);
                Assert.That(roundedDashes, Has.Length.EqualTo(1),
                    "The state container owns exactly one child Graphic; Unity forbids two Graphics on the same object.");
                Assert.That(roundedDashes[0].transform.parent, Is.SameAs(preview.transform));
                Assert.That(preview.GetComponent<Image>().enabled, Is.False,
                    "The old continuous card_preview_outline must not show below the dashes.");
                foreach (var legacyName in new[] { "PreviewDash", "DashTop", "DashBottom", "DashLeft", "DashRight" })
                {
                    var legacy = preview.transform.Find(legacyName);
                    Assert.That(legacy == null || !legacy.gameObject.activeSelf, Is.True,
                        legacyName + " must not create a second border or a four-edge seam.");
                }

                var graphic = (MaskableGraphic)roundedDashes[0];
                Assert.That(graphic.raycastTarget, Is.False);
                Assert.That(new SerializedObject(graphic).FindProperty("m_Material").objectReferenceValue, Is.Null,
                    "The rounded frame uses normal uGUI mesh rendering, not a custom material or shader.");
                Assert.That(roundedDashType.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.Public | BindingFlags.DeclaredOnly), Is.Null, "Layout dirties the mesh; no polling Update loop.");
                Assert.That(roundedDashType.GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic |
                    BindingFlags.Public | BindingFlags.DeclaredOnly), Is.Null, "Layout dirties the mesh; no polling LateUpdate loop.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(216f, 256f, .5f, .5f)]
        [TestCase(320f, 180f, .25f, .7f)]
        public void RoundedDashGraphic_PopulatesAThickDashedAaPerimeterAtEveryCardShape(
            float width, float height, float pivotX, float pivotY)
        {
            var roundedDashType = FindRoundedDashType();
            Assert.That(roundedDashType, Is.Not.Null,
                "Expected AnimalCafe.UI.P8R.P8RRoundedDashGraphic before exercising its real mesh.");
            var go = new GameObject("Rounded dash mesh", typeof(RectTransform), typeof(CanvasRenderer));
            Mesh mesh = null;
            try
            {
                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(width, height);
                rect.pivot = new Vector2(pivotX, pivotY);
                var graphic = (MaskableGraphic)go.AddComponent(roundedDashType);
                var serialized = new SerializedObject(graphic);
                SetFloat(serialized, "strokeWidth", 4f);
                SetFloat(serialized, "cornerRadius", 12f);
                SetFloat(serialized, "dashLength", 16f);
                SetFloat(serialized, "gapLength", 11f);
                SetFloat(serialized, "inset", 4f);
                SetFloat(serialized, "antiAliasWidth", 1f);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                using (var vertices = new VertexHelper())
                {
                    var populate = roundedDashType.GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                        null, new[] { typeof(VertexHelper) }, null);
                    Assert.That(populate, Is.Not.Null);
                    Assert.That(populate.DeclaringType, Is.EqualTo(roundedDashType));
                    populate.Invoke(graphic, new object[] { vertices });
                    mesh = new Mesh();
                    vertices.FillMesh(mesh);
                }

                Assert.That(mesh.vertexCount, Is.GreaterThan(64), "Rounded dashes need enough geometry to follow all four curves.");
                Assert.That(mesh.colors32.Any(c => c.a == 255), Is.True);
                Assert.That(mesh.colors32.Any(c => c.a > 0 && c.a < 255), Is.True,
                    "A one-unit alpha fringe keeps the four-logical-unit stroke smooth at scaled resolutions.");
                var localRect = rect.rect;
                Assert.That(TriangleCovers(mesh, localRect.center), Is.False,
                    "The preview is an outline, never a card-filling overlay.");

                var topY = localRect.yMax - 4f;
                var covered = 0;
                var gaps = 0;
                var runs = 0;
                var previous = false;
                for (var x = localRect.xMin + 16f; x <= localRect.xMax - 16f; x += 1f)
                {
                    var current = TriangleCovers(mesh, new Vector2(x, topY));
                    if (current) covered++; else gaps++;
                    if (current && !previous) runs++;
                    previous = current;
                }
                Assert.That(covered, Is.GreaterThan(24), "The top edge must visibly wrap the card.");
                Assert.That(gaps, Is.GreaterThan(12), "The frame must remain dashed rather than becoming a solid ring.");
                Assert.That(runs, Is.GreaterThanOrEqualTo(3), "Dash spacing must repeat along the resized edge.");
            }
            finally
            {
                if (mesh != null) Object.DestroyImmediate(mesh);
                Object.DestroyImmediate(go);
            }
        }

        private static System.Type FindRoundedDashType()
        {
            return System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("AnimalCafe.UI.P8R.P8RRoundedDashGraphic"))
                .FirstOrDefault(type => type != null);
        }

        private static void SetFloat(SerializedObject serialized, string propertyName, float value)
        {
            var property = serialized.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName + " is part of the rounded-frame authoring contract.");
            property.floatValue = value;
        }

        private static bool TriangleCovers(Mesh mesh, Vector2 point)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = (Vector2)vertices[triangles[i]];
                var b = (Vector2)vertices[triangles[i + 1]];
                var c = (Vector2)vertices[triangles[i + 2]];
                var ab = Cross(b - a, point - a);
                var bc = Cross(c - b, point - b);
                var ca = Cross(a - c, point - c);
                if ((ab >= -.001f && bc >= -.001f && ca >= -.001f) ||
                    (ab <= .001f && bc <= .001f && ca <= .001f)) return true;
            }
            return false;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        [Test]
        public void ApplyApprovedCatalogueCards_PersistsOnlyCatalogueAndSecondRunIsAByteExactNoOp()
        {
            var method = typeof(AnimalCafe.EditorTools.P8R.P8RCompleteUiBuilder).GetMethod(
                "ApplyApprovedCatalogueCards", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "Card styling needs a catalogue-only authoring entry point.");

            var cataloguePath = AnimalCafe.EditorTools.P8R.P8RFurnitureUiPaths.CataloguePrefab;
            var originalCatalogue = System.IO.File.ReadAllBytes(cataloguePath);
            var originalMeta = System.IO.File.ReadAllBytes(cataloguePath + ".meta");
            var protectedPaths = new[]
            {
                AnimalCafe.EditorTools.P8R.P8RFurnitureUiPaths.ActionPrefab,
                AnimalCafe.EditorTools.P8R.P8RFurnitureUiPaths.StorePrefab,
                AnimalCafe.EditorTools.P8R.P8RCompleteUiBuilder.ExitPrefab,
                AnimalCafe.EditorTools.Phase8.Phase8AssetPaths.MainCafeScenePath,
                AnimalCafe.EditorTools.P8R.P8RFurnitureUiPaths.Appearance,
                "Assets/UI/P8R/RefinedB/card_base.png",
                "Assets/UI/P8R/RefinedB/card_base.png.meta",
                "Assets/UI/P8R/RefinedB/card_well.png",
                "Assets/UI/P8R/RefinedB/card_well.png.meta"
            };
            var protectedBytes = protectedPaths.ToDictionary(path => path, System.IO.File.ReadAllBytes);
            var sceneSetup = EditorSceneManager.GetSceneManagerSetup()
                .Select(entry => entry.path + "|" + entry.isLoaded + "|" + entry.isActive).ToArray();
            try
            {
                method.Invoke(null, null);
                var firstBytes = System.IO.File.ReadAllBytes(cataloguePath);
                var firstWrite = System.IO.File.GetLastWriteTimeUtc(cataloguePath);
                method.Invoke(null, null);

                Assert.That(System.IO.File.ReadAllBytes(cataloguePath), Is.EqualTo(firstBytes));
                Assert.That(System.IO.File.GetLastWriteTimeUtc(cataloguePath), Is.EqualTo(firstWrite),
                    "A byte-identical card prefab must not be saved again.");
                foreach (var entry in protectedBytes)
                    Assert.That(System.IO.File.ReadAllBytes(entry.Key), Is.EqualTo(entry.Value), entry.Key);
                Assert.That(EditorSceneManager.GetSceneManagerSetup()
                    .Select(entry => entry.path + "|" + entry.isLoaded + "|" + entry.isActive), Is.EqualTo(sceneSetup),
                    "Catalogue-card authoring must not open, dirty, save or replace MainCafe.");
            }
            finally
            {
                if (!System.IO.File.ReadAllBytes(cataloguePath).SequenceEqual(originalCatalogue))
                    System.IO.File.WriteAllBytes(cataloguePath, originalCatalogue);
                if (!System.IO.File.ReadAllBytes(cataloguePath + ".meta").SequenceEqual(originalMeta))
                    System.IO.File.WriteAllBytes(cataloguePath + ".meta", originalMeta);
                AssetDatabase.ImportAsset(cataloguePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
        }

        [Test]
        public void ForeignSceneReadiness_IsRejectedAndForeignSceneIsUnchanged()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/MainCafe.unity", OpenSceneMode.Additive);
            var foreign = EditorSceneManager.OpenScene(AnimalCafe.EditorTools.Phase8.Phase8AssetPaths.ValidationScenePath, OpenSceneMode.Additive);
            try
            {
                var controller = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DecorationModeController>(true)).Single();
                var foreignView = new GameObject("Foreign Readiness", typeof(RectTransform), typeof(ValidationMessageView)).GetComponent<ValidationMessageView>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(foreignView.gameObject, foreign);
                var before = EditorJsonUtility.ToJson(foreignView);
                typeof(DecorationModeController).GetField("validationMessageView", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, foreignView);
                Assert.That(AnimalCafe.EditorTools.P8R.P8RCompleteUiBuilder.HasCompleteWiring(controller), Is.False);
                Assert.That(() => AnimalCafe.EditorTools.P8R.P8RCompleteUiBuilder.RefreshApprovedCompleteUi(), Throws.InvalidOperationException);
                Assert.That(EditorJsonUtility.ToJson(foreignView), Is.EqualTo(before));
            }
            finally { EditorSceneManager.CloseScene(foreign, true); EditorSceneManager.CloseScene(scene, true); }
        }
    }
}

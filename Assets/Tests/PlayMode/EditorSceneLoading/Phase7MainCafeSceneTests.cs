#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Decoration.Input;
using AnimalCafe.Layout;
using AnimalCafe.UI.Decoration;
using TMPro;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnimalCafe.Tests.PlayMode.EditorSceneLoading
{
    public sealed class Phase7MainCafeSceneTests
    {
        private const string MainCafe = "Assets/Scenes/MainCafe.unity";
        private const string CanonicalWindow = "wall-mounted.main.window.canonical.01";

        [UnityTearDown]
        public IEnumerator RestoreCleanSceneAndInputOwners()
        {
            Time.timeScale = 1f;
            var active = SceneManager.GetActiveScene();
            var cleanup = SceneManager.CreateScene("Phase7Task11MainCafeCleanup");
            SceneManager.SetActiveScene(cleanup);
            if (active.IsValid() && active.isLoaded && active != cleanup)
            {
                var unload = SceneManager.UnloadSceneAsync(active);
                while (unload != null && !unload.isDone)
                    yield return null;
            }

            Assert.That(Object.FindObjectsByType<AnimalCafe.Decoration.Input.InputSystemDecorationTouchSource>(
                FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
            Assert.That(UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator Production_scene_has_one_exact_phase7_graph_and_preserves_phase4_and_phase6()
        {
            yield return LoadMainCafe();
            var controllers = Object.FindObjectsByType<DecorationModeController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(controllers, Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<WallSurfaceRegistry>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<WallMountedSceneRegistry>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<FloorSurfaceGridView>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(1));
            var fade = Object.FindObjectsByType<WallOcclusionFadeView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(fade, Has.Length.EqualTo(1), "MainCafe must own one production blocker-fade service.");
            var fadeType = typeof(WallOcclusionFadeView);
            Assert.That(fadeType.GetField("viewCamera", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(fade[0]), Is.Not.Null);
            Assert.That(fadeType.GetField("fadeMaterialTemplate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(fade[0]), Is.Not.Null);
            var walls = Object.FindObjectsByType<WallSurfaceAuthoring>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(walls.Select(x => x.SurfaceId), Is.EquivalentTo(new[] { "wall.back-left", "wall.back-right" }));
            Assert.That(walls.All(x => x.Columns == 8 && x.Rows == 2), Is.True);
            Assert.That(walls.All(x => x.transform.Find("WallVisual")?.GetComponent<Renderer>()
                ?.sharedMaterial.shader.name == "Universal Render Pipeline/Lit"), Is.True);
            Assert.That(Object.FindFirstObjectByType<DecorationModeTabsView>(FindObjectsInactive.Include).GetComponentsInChildren<Button>(true).Count(x => x.transform.parent.GetComponent<DecorationModeTabsView>() != null), Is.EqualTo(4));
            Assert.That(GameObject.Find("P4_Entrance"), Is.Not.Null);
            Assert.That(GameObject.Find("Phase6_DecorationRuntime"), Is.Not.Null);
            Assert.That(Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(x => x.name.StartsWith("TEST_ONLY_")), Is.False);
        }

        [UnityTest]
        public IEnumerator Production_walls_use_lit_collider_free_finish_layers_and_corner_depth()
        {
            yield return LoadMainCafe();

            var walls = Object.FindObjectsByType<WallSurfaceAuthoring>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(walls, Has.Length.EqualTo(2));
            foreach (var wall in walls)
            {
                var body = wall.transform.Find("WallVisual");
                var finish = wall.transform.Find("Phase7_WallFinish");
                var wainscoting = wall.transform.Find("Phase7_WainscotingFinish");
                Assert.That(body, Is.Not.Null, wall.SurfaceId);
                Assert.That(finish, Is.Not.Null, wall.SurfaceId);
                Assert.That(wainscoting, Is.Not.Null, wall.SurfaceId);

                var bodyRenderer = body.GetComponent<Renderer>();
                var finishRenderer = finish.GetComponent<Renderer>();
                Assert.That(bodyRenderer.sharedMaterial.shader.name,
                    Is.EqualTo("Universal Render Pipeline/Lit"), wall.SurfaceId);
                Assert.That(finishRenderer.sharedMaterial.shader.name,
                    Is.EqualTo("Universal Render Pipeline/Lit"), wall.SurfaceId);
                Assert.That(finishRenderer.sharedMaterial.GetTexture("_BumpMap"), Is.Not.Null, wall.SurfaceId);
                Assert.That(body.GetComponents<Collider>(), Has.Length.EqualTo(1), wall.SurfaceId);
                Assert.That(finish.GetComponentsInChildren<Collider>(true), Is.Empty, wall.SurfaceId);
                Assert.That(wainscoting.GetComponentsInChildren<Collider>(true), Is.Empty, wall.SurfaceId);
                Assert.That(finish.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true), Is.Empty, wall.SurfaceId);
                Assert.That(wainscoting.GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>(true), Is.Empty, wall.SurfaceId);

                var outward = -wall.transform.forward;
                Assert.That(Vector3.Dot(finish.position - body.position, outward), Is.GreaterThan(.05f),
                    wall.SurfaceId + " finish must sit visibly outside the structural wall face.");
                Assert.That(Vector3.Dot(wainscoting.position - finish.position, outward), Is.GreaterThan(.005f),
                    wall.SurfaceId + " wainscoting must form a real visual lip without changing collision.");
            }

            var corner = GameObject.Find("Phase7_InteriorCornerDepth");
            Assert.That(corner, Is.Not.Null);
            Assert.That(corner.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(corner.GetComponentInChildren<Renderer>().sharedMaterial.shader.name,
                Is.EqualTo("Universal Render Pipeline/Lit"));
        }

        [UnityTest]
        public IEnumerator Production_wall_fill_balances_fixed_camera_and_architectural_body_owns_room_shadow()
        {
            yield return LoadMainCafe();

            var fill = GameObject.Find("Phase7_WallFillLight")?.GetComponent<Light>();
            Assert.That(fill, Is.Not.Null);
            Assert.That(fill.type, Is.EqualTo(LightType.Directional));
            Assert.That(fill.intensity, Is.InRange(.2f, .8f));
            Assert.That(fill.shadows, Is.EqualTo(LightShadows.None));
            Assert.That(fill.renderingLayerMask, Is.EqualTo(2));

            foreach (var wall in Object.FindObjectsByType<WallSurfaceAuthoring>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var body = wall.transform.Find("WallVisual").GetComponent<Renderer>();
                var finish = wall.transform.Find("Phase7_WallFinish").GetComponent<Renderer>();
                var wainscoting = wall.transform.Find("Phase7_WainscotingFinish").GetComponent<Renderer>();
                var rail = wall.transform.Find("Phase7_WainscotingRailLip").GetComponent<Renderer>();
                var baseboard = wall.transform.Find("Phase7_WainscotingBaseboardLip").GetComponent<Renderer>();
                Assert.That(body.renderingLayerMask & 2u, Is.EqualTo(2u), wall.SurfaceId);
                Assert.That(finish.renderingLayerMask & 2u, Is.EqualTo(2u), wall.SurfaceId);
                Assert.That(wainscoting.renderingLayerMask & 2u, Is.EqualTo(2u), wall.SurfaceId);
                Assert.That(body.shadowCastingMode,
                    Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.On), wall.SurfaceId);
                Assert.That(finish.shadowCastingMode,
                    Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off), wall.SurfaceId);
                Assert.That(wainscoting.shadowCastingMode,
                    Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off), wall.SurfaceId);
                Assert.That(rail.shadowCastingMode,
                    Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off), wall.SurfaceId);
                Assert.That(baseboard.shadowCastingMode,
                    Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off), wall.SurfaceId);
                Assert.That(body.receiveShadows, Is.True, wall.SurfaceId);
                Assert.That(finish.receiveShadows, Is.True, wall.SurfaceId);
                Assert.That(wainscoting.receiveShadows, Is.True, wall.SurfaceId);
                Assert.That(rail.receiveShadows, Is.True, wall.SurfaceId);
                Assert.That(baseboard.receiveShadows, Is.True, wall.SurfaceId);
            }
        }

        [UnityTest]
        public IEnumerator Production_warm_rail_controls_visual_only_rail_and_baseboard_lips()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode();
            yield return null;
            Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
            var wall = Object.FindObjectsByType<WallSurfaceAuthoring>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.SurfaceId == "wall.back-left");
            Assert.That(controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.WallSurface, surfaceId: wall.SurfaceId)), Is.True);

            var rail = wall.transform.Find("Phase7_WainscotingRailLip")?.GetComponent<Renderer>();
            var baseboard = wall.transform.Find("Phase7_WainscotingBaseboardLip")?.GetComponent<Renderer>();
            Assert.That(rail, Is.Not.Null);
            Assert.That(baseboard, Is.Not.Null);
            Assert.That(rail.enabled, Is.False);
            Assert.That(baseboard.enabled, Is.False);
            Assert.That(rail.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(baseboard.GetComponentsInChildren<Collider>(true), Is.Empty);

            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            var tile = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Single(item => item.ItemId == "wainscoting.warm-white-rail");
            tile.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(rail.enabled, Is.True);
            Assert.That(baseboard.enabled, Is.True);
            var wainscoting = wall.transform.Find("Phase7_WainscotingFinish").GetComponent<Renderer>();
            Assert.That(rail.sharedMaterial, Is.SameAs(wainscoting.sharedMaterial));
            Assert.That(baseboard.sharedMaterial, Is.SameAs(wainscoting.sharedMaterial));
            var outward = -wall.transform.forward;
            Assert.That(Vector3.Dot(rail.transform.position - wainscoting.transform.position, outward),
                Is.GreaterThan(.015f));
            Assert.That(Vector3.Dot(baseboard.transform.position - wainscoting.transform.position, outward),
                Is.GreaterThan(.015f));

            controller.CancelActivePhase7Preview();
            Assert.That(rail.enabled, Is.False);
            Assert.That(baseboard.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator Closed_mode_phase7_ui_is_non_raycasting_then_enter_enables_tabs_and_catalogue()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>(FindObjectsInactive.Include);
            var group = catalogue.GetComponent<CanvasGroup>();
            var tabs = Object.FindFirstObjectByType<DecorationModeTabsView>(FindObjectsInactive.Include);
            var range = Object.FindFirstObjectByType<DecorationFloorRangeView>(FindObjectsInactive.Include);
            Assert.That(controller.IsOpen, Is.False);
            Assert.That(group.alpha, Is.EqualTo(0f).Within(.001f));
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(tabs.gameObject.activeSelf, Is.False);
            Assert.That(range.gameObject.activeSelf, Is.False);

            controller.EnterDecorationMode(); yield return null;
            Assert.That(controller.IsOpen, Is.True);
            Assert.That(group.alpha, Is.EqualTo(1f).Within(.001f));
            Assert.That(group.blocksRaycasts, Is.True);
            Assert.That(tabs.gameObject.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator Furniture_mode_keeps_legacy_definition_binding_and_collapse_as_top_ui_owner()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode(); yield return null;
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            Assert.That(catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Where(x => x.gameObject.activeInHierarchy).All(x => x.Definition != null), Is.True);
            var collapse = catalogue.transform.Find("ExpandedSheet/CollapseButton").GetComponent<Button>();
            var center = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)collapse.transform).TransformPoint(((RectTransform)collapse.transform).rect.center));
            var data = new PointerEventData(EventSystem.current) { position = center };
            var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.transform.IsChildOf(collapse.transform), Is.True,
                "top=" + string.Join(",", hits.Take(6).Select(x => x.gameObject.name)));
        }

        [UnityTest]
        public IEnumerator Furniture_mode_collapsed_handle_and_done_button_remain_top_ui_owners()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode(); yield return null;
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            catalogue.ShowCollapsedHandle();
            yield return new WaitForSecondsRealtime(.2f);
            var handle = catalogue.transform.Find("CollapsedHandle").GetComponent<Button>();
            AssertTop(handle);
            var done = (Button)typeof(DecorationModeController).GetField("decorationModeButton",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(controller);
            AssertTop(done);
        }

        [UnityTest]
        public IEnumerator Furniture_preview_actions_and_post_confirm_handle_are_top_ui_owners()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode(); yield return null;
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            var first = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Where(x => x.gameObject.activeInHierarchy && x.Definition != null)
                .OrderBy(x => x.name).First();
            first.GetComponent<Button>().onClick.Invoke();
            yield return new WaitForSecondsRealtime(.2f);
            var action = Object.FindFirstObjectByType<DecorationActionBarView>();
            var rotate = action.GetComponentsInChildren<Button>(true).Single(x => x.name == "RotateButton");
            var confirm = action.GetComponentsInChildren<Button>(true).Single(x => x.name == "ConfirmButton");
            AssertTop(rotate); AssertTop(confirm);
            confirm.onClick.Invoke(); yield return null;
            AssertTop(catalogue.transform.Find("CollapsedHandle").GetComponent<Button>());
        }

        [UnityTest]
        public IEnumerator Production_floor_range_buttons_use_the_shared_rounded_paper_style()
        {
            yield return LoadMainCafe();
            var range = Object.FindFirstObjectByType<DecorationFloorRangeView>(FindObjectsInactive.Include);
            var buttons = range.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.Select(button => button.name),
                Is.EquivalentTo(new[] { "WholeRoomButton", "SingleGridButton" }));

            foreach (var button in buttons)
            {
                Assert.That(button.image.sprite, Is.Not.Null, button.name);
                Assert.That(button.image.type, Is.EqualTo(Image.Type.Sliced), button.name);
                Assert.That(button.image.sprite.border.sqrMagnitude, Is.GreaterThan(0f), button.name);
                Assert.That(button.colors.normalColor.r, Is.GreaterThan(button.colors.normalColor.b),
                    button.name + " must use the warm paper state instead of default white.");
                Assert.That(button.colors.disabledColor.g, Is.GreaterThan(button.colors.disabledColor.r),
                    button.name + " selected state must use the shared sage highlight.");
            }
        }

        [UnityTest]
        public IEnumerator Rejected_floor_range_click_does_not_look_like_an_accepted_selection()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode();
            yield return null;
            Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);

            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            var floorTile = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .First(tile => tile.gameObject.activeInHierarchy && tile.ItemId.StartsWith("floor."));
            floorTile.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(controller.ActiveSurfacePreview.Scope, Is.EqualTo(SurfaceEditScope.WholeRoomFloor));

            var range = Object.FindFirstObjectByType<DecorationFloorRangeView>();
            var wholeRoom = range.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "WholeRoomButton");
            var singleGrid = range.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "SingleGridButton");
            singleGrid.Select();
            singleGrid.onClick.Invoke();
            yield return new WaitForSecondsRealtime(.15f);

            Assert.That(controller.FloorRange, Is.EqualTo(SurfaceEditScope.WholeRoomFloor));
            Assert.That(range.SelectedRange, Is.EqualTo(SurfaceEditScope.WholeRoomFloor));
            var focusedName=EventSystem.current?.currentSelectedGameObject?.name??"none";
            var wholeRoomTint=wholeRoom.targetGraphic.canvasRenderer.GetColor();
            var singleGridTint=singleGrid.targetGraphic.canvasRenderer.GetColor();
            Assert.That(Vector4.Distance(singleGridTint,wholeRoomTint), Is.GreaterThan(.25f),
                $"A rejected Single Grid click may keep UI focus, but it must not reuse the green active-range colour. " +
                $"Whole={wholeRoomTint}, interactable={wholeRoom.interactable}; " +
                $"Single={singleGridTint}, interactable={singleGrid.interactable}; " +
                $"Focused={focusedName}.");
        }

        [UnityTest]
        public IEnumerator Window_is_catalogue_only_session_confirmed_and_scene_reload_starts_empty()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode(); yield return null;
            var runtime = Object.FindFirstObjectByType<CafeLayoutRuntime>();
            var registry = Object.FindFirstObjectByType<WallMountedSceneRegistry>();
            Assert.That(runtime.WallMountedLayout.CaptureSnapshot().Instances, Is.Empty);
            Assert.That(registry.TryGet(CanonicalWindow, out _), Is.False);
            var authored = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.name == "P4_Window_BackRight_C3_R0");
            Assert.That(authored.gameObject.activeSelf, Is.False);
            Assert.That(Object.FindFirstObjectByType<DecorationModeTabsView>().RequestMode(DecorationModeKind.WallDecor), Is.True);
            yield return null;
            var windowTiles = Object.FindFirstObjectByType<DecorationCatalogueView>()
                .GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Where(tile => tile.ItemId != null && tile.ItemId.StartsWith("window.")).ToArray();
            Assert.That(windowTiles, Has.Length.EqualTo(2));
            windowTiles.Single(tile => tile.ItemId == "window.canonical.phase4").GetComponent<Button>().onClick.Invoke();
            yield return null;
            var projection = Object.FindFirstObjectByType<WallMountedPreviewView>();
            Assert.That(projection.CurrentProjection, Is.Not.Null,
                "Selecting a Window must immediately show its wall footprint.");
            Assert.That(projection.CurrentProjection.name, Does.Contain("ValidCheck"));
            Assert.That(projection.CurrentGhost, Is.Not.Null,
                "Selecting a Window must instantiate the real prefab as a visible ghost.");
            Assert.That(projection.CurrentGhost.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
            var gameplayCamera = Object.FindObjectsByType<UnityEngine.Camera>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.CompareTag("MainCamera"));
            var ghostViewport = gameplayCamera.WorldToViewportPoint(
                CombinedBounds(projection.CurrentGhost.GetComponentsInChildren<Renderer>(true)).center);
            Assert.That(ghostViewport.z, Is.GreaterThan(0f));
            Assert.That(ghostViewport.x, Is.InRange(.05f, .95f),
                "New wall decor must begin on a wall slot visible in the gameplay camera.");
            Assert.That(ghostViewport.y, Is.InRange(.05f, .95f));
            CaptureUiEvidence("outputs/phase7-ui-fix/MainCafe_WallDecor_Ghost.png",
                Object.FindFirstObjectByType<DecorationCatalogueView>());
            Assert.That(controller.TryConfirmPhase7Preview(), Is.True);
            var confirmedWindow = runtime.WallMountedLayout.CaptureSnapshot().Instances
                .Single(item => item.DefinitionId == "window.canonical.phase4");
            Assert.That(registry.TryGet(confirmedWindow.InstanceId, out var representation), Is.True);
            var confirmedSurface = Object.FindObjectsByType<WallSurfaceAuthoring>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.SurfaceId == confirmedWindow.SurfaceId);
            Assert.That(Vector3.Dot(representation.transform.forward, -confirmedSurface.transform.forward),
                Is.GreaterThan(.99f),
                "Confirmed Window must face outward instead of extending into the wall.");
            yield return LoadMainCafe();
            runtime = Object.FindFirstObjectByType<CafeLayoutRuntime>();
            Assert.That(runtime.WallMountedLayout.CaptureSnapshot().Instances, Is.Empty);
        }

        [UnityTest]
        public IEnumerator Wall_decor_preview_never_replaces_the_canonical_floor_material()
        {
            // Catches the production magenta-floor bug: wall occlusion sampling must
            // never treat the room Floor as a decoration blocker and swap its shader.
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            var floorCollider = (Collider)typeof(DecorationModeController)
                .GetField("floorCollider",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(controller);
            var floorRenderer = floorCollider.GetComponent<Renderer>();
            var sourceMaterials = floorRenderer.sharedMaterials;

            controller.EnterDecorationMode();
            yield return null;
            Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);
            var shiba = Object.FindFirstObjectByType<DecorationCatalogueView>()
                .GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Single(tile => tile.ItemId == "wall-decor.shiba-painting.01");
            shiba.GetComponent<Button>().onClick.Invoke();
            yield return null;

            Assert.That(floorRenderer.sharedMaterials, Is.EqualTo(sourceMaterials),
                "Wall Decor Preview must not pass the canonical Floor through the blocker fade shader.");
            Assert.That(floorRenderer.sharedMaterials.All(material =>
                    material != null
                    && material.shader != null
                    && material.shader.isSupported
                    && material.shader.name != "Hidden/InternalErrorShader"),
                Is.True,
                "The canonical Floor must never render magenta/error-shader during Wall Decor Preview.");
        }

        [UnityTest]
        public IEnumerator Newly_confirmed_wall_decor_is_immediately_classified_by_the_real_physics_path()
        {
            // Catches the false-green test gap where direct controller calls worked,
            // but the first real click after Confirm still hit the wall behind the item.
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode();
            yield return null;
            Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);

            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            var shiba = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Single(tile => tile.ItemId == "wall-decor.shiba-painting.01");
            shiba.GetComponent<Button>().onClick.Invoke();
            yield return null;

            var action = Object.FindFirstObjectByType<DecorationActionBarView>();
            action.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "ConfirmButton")
                .onClick.Invoke();

            var runtime = Object.FindFirstObjectByType<CafeLayoutRuntime>();
            var confirmed = runtime.WallMountedLayout.CaptureSnapshot().Instances
                .Single(item => item.DefinitionId == "wall-decor.shiba-painting.01");
            var registry = Object.FindFirstObjectByType<WallMountedSceneRegistry>();
            Assert.That(registry.TryGet(confirmed.InstanceId, out var representation), Is.True);
            var camera = Object.FindObjectsByType<UnityEngine.Camera>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.CompareTag("MainCamera"));
            var renderBounds = CombinedBounds(
                representation.GetComponentsInChildren<Renderer>(true));
            var screenPoint = camera.WorldToScreenPoint(renderBounds.center);
            Assert.That(screenPoint.z, Is.GreaterThan(0f));

            var classify = typeof(DecorationModeController).GetMethod(
                "ClassifyPrimaryBegan",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var hit = (DecorationTouchHit)classify.Invoke(
                controller,
                new object[] { (Vector2)screenPoint });

            Assert.That(hit.Kind, Is.EqualTo(DecorationTouchHitKind.WallMounted),
                "The first click after Confirm must hit the committed decor, not the wall behind it.");
            Assert.That(hit.TargetId, Is.EqualTo(confirmed.InstanceId));
        }

        [UnityTest]
        public IEnumerator Wall_decor_action_panel_follows_real_ghost_and_never_shows_rotate()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode();
            yield return null;
            Assert.That(controller.TryChangeMode(DecorationModeKind.WallDecor), Is.True);

            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            var monitor = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true)
                .Single(tile => tile.ItemId == "wall-decor.monitor.01");
            monitor.GetComponent<Button>().onClick.Invoke();
            yield return null;

            var preview = Object.FindFirstObjectByType<WallMountedPreviewView>();
            var action = Object.FindFirstObjectByType<DecorationActionBarView>();
            var presentationRoot = (RectTransform)typeof(DecorationActionBarView)
                .GetField("presentationRoot",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(action);
            var rotate = action.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "RotateButton");
            var visibleActionButtons = presentationRoot.Cast<Transform>()
                .Where(item => item.gameObject.activeSelf && item.GetComponent<Button>() != null)
                .OrderBy(item => item.GetSiblingIndex())
                .Select(item => item.GetComponent<Button>())
                .ToArray();
            Assert.That(preview.CurrentGhost, Is.Not.Null);
            Assert.That(action.IsVisible, Is.True);
            Assert.That(rotate.gameObject.activeSelf, Is.False,
                "Wall Decor must never expose Furniture's Rotate action.");
            CollectionAssert.AreEqual(new[] { "×", "✓" }, visibleActionButtons
                .Select(button => button.transform.Find("Label").GetComponent<TMP_Text>().text));
            Assert.That(visibleActionButtons.All(button =>
                Mathf.Approximately(((RectTransform)button.transform).rect.width, 48f)
                && Mathf.Approximately(((RectTransform)button.transform).rect.height, 48f)), Is.True,
                "Wall Decor must restore the compact icon actions instead of Surface-sized text buttons.");

            Canvas.ForceUpdateCanvases();
            var initialGhost = CombinedBounds(
                preview.CurrentGhost.GetComponentsInChildren<Renderer>(true)).center;
            var initialActionCenter = WorldRect(presentationRoot).center;
            var moved = false;
            var actionMoved = false;
            foreach (var surface in new[] { "wall.back-left", "wall.back-right" })
            {
                for (var row = 0; row < 2 && !actionMoved; row++)
                {
                    for (var column = 0; column < 8 && !actionMoved; column++)
                    {
                        var drag = controller.TryHandleSceneDrag(new DecorationTouchHit(
                            DecorationTouchHitKind.WallSlot,
                            surfaceId: surface,
                            wallSlotPosition: new WallSlotPosition(column, row)));
                        if (!drag || preview.CurrentGhost == null)
                        {
                            continue;
                        }

                        Canvas.ForceUpdateCanvases();
                        var ghostCenter = CombinedBounds(
                            preview.CurrentGhost.GetComponentsInChildren<Renderer>(true)).center;
                        moved |= Vector3.Distance(ghostCenter, initialGhost) > .1f;
                        actionMoved = Vector2.Distance(
                            WorldRect(presentationRoot).center,
                            initialActionCenter) > 1f;
                    }
                }
            }

            Assert.That(moved, Is.True,
                "The production wall ghost must move to another valid wall slot during this test.");
            Assert.That(actionMoved, Is.True,
                "The production Confirm/Cancel panel must follow the wall ghost instead of staying fixed.");
            Assert.That(rotate.gameObject.activeSelf, Is.False);
            controller.CancelActivePhase7Preview();
        }

        [UnityTest]
        public IEnumerator Production_catalogue_owns_nested_scroll_direction_and_active_tab_front()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode(); yield return null;
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            var tabs = Object.FindFirstObjectByType<DecorationModeTabsView>();
            Assert.That(tabs.RequestMode(DecorationModeKind.Wall), Is.True); yield return null;
            var active = tabs.GetComponentsInChildren<Button>(true).Single(x => x.name == "wallButton");
            Assert.That(active.transform.GetSiblingIndex(), Is.EqualTo(tabs.transform.childCount - 1));
            Assert.That(((RectTransform)active.transform).anchoredPosition.y,
                Is.GreaterThan(tabs.GetComponentsInChildren<Button>(true).Where(x => x != active).Max(x => ((RectTransform)x.transform).anchoredPosition.y)));

            var row = catalogue.CategoryRows[0].HorizontalScroll;
            catalogue.BeginNestedDrag(row);
            Assert.That(catalogue.UpdateNestedDrag(new Vector2(20f, 2f)), Is.EqualTo("Horizontal"));
            Assert.That(catalogue.NestedDragOwner, Is.SameAs(row));
            catalogue.EndNestedDrag();
            catalogue.BeginNestedDrag(row);
            Assert.That(catalogue.UpdateNestedDrag(new Vector2(2f, 20f)), Is.EqualTo("Vertical"));
            Assert.That(catalogue.NestedDragOwner, Is.SameAs(catalogue.VerticalScroll));
        }

        [UnityTest]
        public IEnumerator Production_floor_compact_stack_keeps_handle_clear_of_range_and_tabs()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode();
            yield return null;

            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            var tabs = Object.FindFirstObjectByType<DecorationModeTabsView>();
            Assert.That(tabs.RequestMode(DecorationModeKind.Floor), Is.True);
            yield return null;

            catalogue.SetSheetState(DecorationSheetState.CompactPreview, hasActivePreview: true);
            yield return new WaitForSecondsRealtime(.2f);
            Canvas.ForceUpdateCanvases();

            var handle = WorldRect(catalogue.CollapsedHandleRect);
            var range = Object.FindFirstObjectByType<DecorationFloorRangeView>();
            var rangeRects = range.GetComponentsInChildren<Button>(false)
                .Select(button => WorldRect((RectTransform)button.transform))
                .ToArray();
            var tabStrip = WorldRect((RectTransform)tabs.transform);
            var canvasScale = catalogue.GetComponentInParent<Canvas>().scaleFactor;
            var minimumGap = Mathf.Max(1f, 6f * canvasScale);

            Assert.That(rangeRects, Has.Length.EqualTo(2));
            Assert.That(rangeRects.Any(rect => rect.Overlaps(handle)), Is.False,
                $"Compact Catalogue handle must not cover Whole Room / Single Grid. handle={handle}, range={string.Join(",", rangeRects)}");
            Assert.That(handle.yMin, Is.GreaterThanOrEqualTo(rangeRects.Max(rect => rect.yMax) + minimumGap),
                "Compact order must be Floor range, Catalogue handle, then Tabs with a readable gap.");
            Assert.That(tabStrip.yMin, Is.GreaterThanOrEqualTo(handle.yMax + minimumGap),
                "The Catalogue handle must stay detached from the folder Tabs.");
        }

        [UnityTest]
        public IEnumerator Production_wall_categories_have_real_vertical_overflow_and_pointer_drag()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode();
            yield return null;

            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            var tabs = Object.FindFirstObjectByType<DecorationModeTabsView>();
            Assert.That(tabs.RequestMode(DecorationModeKind.Wall), Is.True);
            yield return null;
            Canvas.ForceUpdateCanvases();

            var vertical = catalogue.VerticalScroll;
            var content = vertical.content;
            var viewport = vertical.viewport;
            Assert.That(catalogue.CategoryRows, Has.Count.EqualTo(3));
            Assert.That(content.rect.height, Is.GreaterThanOrEqualTo(viewport.rect.height + 32f),
                $"Wall categories need visible vertical overflow. content={content.rect.height}, viewport={viewport.rect.height}");

            var row = catalogue.CategoryRows[0].HorizontalScroll;
            var rowRect = (RectTransform)row.transform;
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, rowRect.TransformPoint(rowRect.rect.center))
            };
            pointer.pressPosition = pointer.position;
            var before = content.anchoredPosition;

            ExecuteEvents.Execute(row.gameObject, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(row.gameObject, pointer, ExecuteEvents.beginDragHandler);
            for (var step = 0; step < 4; step++)
            {
                pointer.delta = new Vector2(0f, 20f);
                pointer.position += pointer.delta;
                ExecuteEvents.Execute(row.gameObject, pointer, ExecuteEvents.dragHandler);
            }
            ExecuteEvents.Execute(row.gameObject, pointer, ExecuteEvents.endDragHandler);

            Assert.That(Mathf.Abs(content.anchoredPosition.y - before.y), Is.GreaterThan(8f),
                "A vertical pointer drag that begins on an item row must move the parent category list.");
        }

        [UnityTest]
        public IEnumerator Production_catalogue_has_compact_card_gaps_rounded_panel_and_attached_collapsing_tabs()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode();
            yield return null;
            Canvas.ForceUpdateCanvases();

            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            var catalogueRect = (RectTransform)catalogue.transform;
            var tabs = Object.FindFirstObjectByType<DecorationModeTabsView>();
            var activeTab = tabs.GetComponentsInChildren<Button>(true)
                .Single(button => button.name == "furnitureButton");
            var expandedPanel = catalogue.transform.Find("ExpandedSheet").GetComponent<Image>();
            Assert.That(expandedPanel.sprite, Is.Not.Null);
            Assert.That(expandedPanel.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(expandedPanel.sprite.border.sqrMagnitude, Is.GreaterThan(0f));

            var cards = catalogue.CategoryRows[0].HorizontalScroll.content
                .GetComponentsInChildren<DecorationCatalogueTileView>(false)
                .Where(tile => tile.gameObject.activeInHierarchy)
                .Select(tile => WorldRect((RectTransform)tile.transform))
                .OrderBy(rect => rect.xMin)
                .ToArray();
            Assert.That(cards, Has.Length.GreaterThanOrEqualTo(4));
            for (var index = 1; index < cards.Length; index++)
            {
                var gap = cards[index].xMin - cards[index - 1].xMax;
                var gapRatio = gap / cards[index - 1].width;
                Assert.That(gapRatio, Is.InRange(.03f, .12f),
                    $"Real MainCafe card gap {index - 1}->{index} must stay visually compact, actual={gap}, ratio={gapRatio}.");
            }

            var panelRect = WorldRect((RectTransform)expandedPanel.transform);
            var firstRow = catalogue.CategoryRows[0].HorizontalScroll;
            var categoryTitle = firstRow.transform.Find("CategoryLabel").GetComponent<TMPro.TMP_Text>();
            var titleRect = WorldRect(categoryTitle.rectTransform);
            var contentInset = cards[0].xMin - panelRect.xMin;
            Assert.That(contentInset, Is.InRange(12f, 32f),
                $"Catalogue cards need a fixed readable inset inside the panel, actual={contentInset}.");
            Assert.That(categoryTitle.fontSize, Is.GreaterThanOrEqualTo(22f));
            Assert.That((categoryTitle.fontStyle & TMPro.FontStyles.Bold) != 0, Is.True,
                "Category titles must remain readable over the scene behind the translucent sheet.");
            Assert.That(titleRect.yMin, Is.GreaterThanOrEqualTo(cards[0].yMax - 1f),
                "Category title must own a separate row and never overlap its item cards.");
            var activeRect = WorldRect((RectTransform)activeTab.transform);
            Assert.That(activeRect.yMin, Is.LessThanOrEqualTo(panelRect.yMax),
                "Raised active tab must overlap the panel edge instead of leaving a visible gap.");
            Assert.That(activeRect.yMin, Is.GreaterThanOrEqualTo(panelRect.yMax - 16f),
                "Active tab should read as a folder tab, not sink deep into the panel.");
            CaptureUiEvidence("outputs/phase7-ui-fix/MainCafe_Catalogue_Expanded.png", catalogue);

            var catalogueStart = catalogueRect.anchoredPosition;
            var catalogueWorldStart = catalogueRect.position;
            var tabsStart = ((RectTransform)tabs.transform).position;
            catalogue.ShowCollapsedHandle();
            yield return null;
            var intermediate = catalogueRect.anchoredPosition.y;
            Assert.That(intermediate, Is.LessThan(catalogueStart.y),
                "A populated production catalogue must begin moving when collapsed.");
            yield return new WaitForSecondsRealtime(.2f);
            var catalogueDelta = catalogueRect.anchoredPosition.y - catalogueStart.y;
            var catalogueWorldDelta = catalogueRect.position.y - catalogueWorldStart.y;
            var tabsDelta = ((RectTransform)tabs.transform).position.y - tabsStart.y;
            Assert.That(catalogueDelta, Is.LessThan(-1f));
            Assert.That(tabsDelta, Is.EqualTo(catalogueWorldDelta).Within(.05f),
                "Tabs must travel with the real Bottom Sheet throughout collapse.");
            var collapsedTabRect = WorldRect((RectTransform)activeTab.transform);
            var viewport = catalogue.GetComponentInParent<Canvas>().rootCanvas.pixelRect;
            Assert.That(collapsedTabRect.yMin, Is.GreaterThanOrEqualTo(viewport.yMin));
            Assert.That(collapsedTabRect.yMax, Is.LessThanOrEqualTo(viewport.yMax));
            Assert.That(collapsedTabRect.center.y, Is.LessThanOrEqualTo(viewport.height * .2f),
                "Collapsed tabs must settle with the Bottom Sheet in the lower screen region instead of floating mid-screen.");
            var collapsedHandleRect = WorldRect(catalogue.CollapsedHandleRect);
            Assert.That(catalogue.CollapsedHandleRect.gameObject.activeInHierarchy, Is.True);
            Assert.That(collapsedHandleRect.yMin, Is.GreaterThanOrEqualTo(viewport.yMin),
                "The collapsed expand handle must remain visible after the sheet and tabs move down.");
            Assert.That(collapsedHandleRect.yMax, Is.LessThanOrEqualTo(viewport.yMax));
            CaptureUiEvidence("outputs/phase7-ui-fix/MainCafe_Catalogue_Collapsed.png", catalogue);
            // RenderTexture and Texture2D use deferred destruction in PlayMode.
            // Let that cleanup finish before the next responsive/raycast test starts.
            yield return null;
        }

        [UnityTest]
        public IEnumerator Responsive_matrix_keeps_compact_actions_visible_raycastable_and_inside_safe_area()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode(); yield return null;
            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            var action = Object.FindFirstObjectByType<DecorationActionBarView>();
            var tabs = Object.FindFirstObjectByType<DecorationModeTabsView>();
            var canvas = catalogue.GetComponentInParent<Canvas>().rootCanvas;
            var scaler = canvas.GetComponent<CanvasScaler>();
            var productionCamera = Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.CompareTag("MainCamera"));
            var actionButtons = action.GetComponentsInChildren<Button>(true);
            var actionRect = (RectTransform)actionButtons.Single(x => x.name == "CancelButton").transform.parent;
            var cases = new[]
            {
                new Rect(0, 0, 1080, 1920), new Rect(0, 0, 1080, 1920),
                new Rect(0, 0, 720, 1280), new Rect(24, 40, 672, 1184),
                new Rect(0, 0, 1080, 2400), new Rect(0, 72, 1080, 2256),
                new Rect(0, 0, 1920, 1080), new Rect(80, 24, 1760, 1032)
            };
            var originalReference = scaler.referenceResolution;
            var originalMode = scaler.uiScaleMode;
            var originalMatch = scaler.matchWidthOrHeight;
            var originalRenderMode = canvas.renderMode;
            var originalCanvasCamera = canvas.worldCamera;
            var originalTargetTexture = productionCamera.targetTexture;
            RenderTexture responsiveTarget = null;
            try
            {
                for (var i = 0; i < cases.Length; i += 2)
                {
                    var viewport = cases[i]; var safe = cases[i + 1];
                    if (responsiveTarget != null)
                    {
                        productionCamera.targetTexture = originalTargetTexture;
                        responsiveTarget.Release();
                        Object.Destroy(responsiveTarget);
                    }
                    responsiveTarget = new RenderTexture(
                        Mathf.RoundToInt(viewport.width), Mathf.RoundToInt(viewport.height), 24)
                    { name = $"TEST_RUNTIME_IT035_{viewport.width}x{viewport.height}" };
                    responsiveTarget.Create();
                    productionCamera.targetTexture = responsiveTarget;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = productionCamera;
                    canvas.planeDistance = 1f;
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    scaler.referenceResolution = viewport.size;
                    scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    scaler.matchWidthOrHeight = viewport.height >= viewport.width ? 0f : 1f;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    yield return null;
                    Assert.That(canvas.pixelRect.size, Is.EqualTo(viewport.size),
                        "IT-035 harness must apply the render-target viewport to the production Canvas.");
                    Assert.That(actionRect.IsChildOf(canvas.transform), Is.True,
                        "Production ActionBar must remain attached to the real Canvas hierarchy.");

                    // Each responsive case starts in the no-preview expanded state.
                    // The previous loop intentionally shows the compact ActionBar,
                    // so clear its raycast ownership before testing the tabs again.
                    action.Hide();
                    catalogue.ShowCatalogue();
                    catalogue.SetSheetState(DecorationSheetState.Expanded, false);
                    tabs.SetActive(DecorationModeKind.Wall);
                    yield return new WaitForSecondsRealtime(.2f);
                    Canvas.ForceUpdateCanvases();
                    var tabButtons = tabs.GetComponentsInChildren<Button>(true);
                    Assert.That(tabButtons, Has.Length.EqualTo(4));
                    Assert.That(tabButtons.All(button => button.gameObject.activeInHierarchy
                        && button.interactable && button.image != null && button.image.raycastTarget), Is.True);
                    foreach (var tab in tabButtons)
                    {
                        var bounds = WorldRect((RectTransform)tab.transform);
                        Assert.That(viewport.Contains(bounds.min) && viewport.Contains(bounds.max), Is.True,
                            $"viewport={viewport}, tab={tab.name}, bounds={bounds}");
                        AssertTop(tab);
                    }
                    var active = tabButtons.Single(button => button.name == "wallButton");
                    Assert.That(active.transform.GetSiblingIndex(), Is.EqualTo(tabs.transform.childCount - 1));
                    Assert.That(((RectTransform)active.transform).anchoredPosition.y,
                        Is.GreaterThan(tabButtons.Where(button => button != active)
                            .Max(button => ((RectTransform)button.transform).anchoredPosition.y)));

                    var row = catalogue.CategoryRows.First().HorizontalScroll;
                    catalogue.BeginNestedDrag(row);
                    Assert.That(catalogue.UpdateNestedDrag(new Vector2(24f, 2f)), Is.EqualTo("Horizontal"));
                    Assert.That(catalogue.NestedDragOwner, Is.SameAs(row));
                    catalogue.EndNestedDrag();
                    catalogue.BeginNestedDrag(row);
                    Assert.That(catalogue.UpdateNestedDrag(new Vector2(2f, 24f)), Is.EqualTo("Vertical"));
                    Assert.That(catalogue.NestedDragOwner, Is.SameAs(catalogue.VerticalScroll));
                    catalogue.EndNestedDrag();
                    Assert.That(row.viewport.gameObject.activeInHierarchy && row.enabled && row.horizontal && !row.vertical, Is.True);
                    Assert.That(catalogue.VerticalScroll.gameObject.activeInHierarchy
                        && catalogue.VerticalScroll.enabled && catalogue.VerticalScroll.vertical
                        && !catalogue.VerticalScroll.horizontal, Is.True);

                    catalogue.SetSheetState(DecorationSheetState.CompactPreview, true);
                    action.SetModeActions(DecorationModeKind.WallDecor, false);
                    action.Show(false, true, PlacementFeedbackKey.None);
                    action.SetPresentation(DecorationActionPresentation.New, safe.center, safe);
                    yield return new WaitForSecondsRealtime(.2f);
                    Canvas.ForceUpdateCanvases();
                    var cancel = actionButtons.Single(button => button.name == "CancelButton");
                    var confirm = actionButtons.Single(button => button.name == "ConfirmButton");
                    foreach (var button in new[] { cancel, confirm })
                    {
                        Assert.That(button.gameObject.activeInHierarchy && button.interactable
                            && button.image != null && button.image.raycastTarget, Is.True);
                        var bounds = WorldRect((RectTransform)button.transform);
                        Assert.That(safe.Contains(bounds.min) && safe.Contains(bounds.max), Is.True,
                            $"viewport={viewport}, safe={safe}, button={button.name}, bounds={bounds}");
                        AssertTop(button);
                    }
                    Assert.That(WorldRect((RectTransform)cancel.transform)
                        .Overlaps(WorldRect((RectTransform)confirm.transform)), Is.False,
                        $"viewport={viewport}: Cancel and Confirm must not overlap.");
                }
            }
            finally
            {
                scaler.uiScaleMode = originalMode;
                scaler.referenceResolution = originalReference;
                scaler.matchWidthOrHeight = originalMatch;
                canvas.renderMode = originalRenderMode;
                canvas.worldCamera = originalCanvasCamera;
                productionCamera.targetTexture = originalTargetTexture;
                if (responsiveTarget != null)
                {
                    responsiveTarget.Release();
                    Object.Destroy(responsiveTarget);
                }
                Canvas.ForceUpdateCanvases();
            }
        }

        [UnityTest]
        public IEnumerator Repeated_load_does_not_accumulate_cameras_or_render_textures()
        {
            yield return LoadMainCafe();
            var cameras = Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            var renderTextures = Resources.FindObjectsOfTypeAll<RenderTexture>().Count(x => x != null);
            yield return LoadMainCafe();
            Assert.That(Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None), Has.Length.EqualTo(cameras));
            Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Count(x => x != null), Is.EqualTo(renderTextures));
        }

        [UnityTest]
        public IEnumerator Exit_modal_uses_a_fullscreen_dim_layer_and_a_separate_rounded_card()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode();
            yield return null;

            var modal = Object.FindFirstObjectByType<DecorationExitModalView>(FindObjectsInactive.Include);
            modal.Show();
            Canvas.ForceUpdateCanvases();

            Assert.That(modal.transform.parent.name, Is.EqualTo("Screen Canvas"),
                "The exit Modal must be a full-screen Canvas layer, not a child of the bottom catalogue runtime.");
            var rootRect = (RectTransform)modal.transform;
            Assert.That(rootRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rootRect.anchorMax, Is.EqualTo(Vector2.one));

            var backdrop = modal.transform.Find("Backdrop")?.GetComponent<Image>();
            Assert.That(backdrop, Is.Not.Null, "The Modal needs a dedicated dim blocker behind its card.");
            Assert.That(backdrop.raycastTarget, Is.True);
            Assert.That(backdrop.color.a, Is.InRange(.35f, .75f));

            var card = modal.transform.Find("ModalCard")?.GetComponent<Image>();
            Assert.That(card, Is.Not.Null, "Continue and Discard must live on a separate warm Modal card.");
            Assert.That(card.sprite, Is.Not.Null);
            Assert.That(card.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(card.color.r, Is.GreaterThan(card.color.b),
                "The card must use the approved warm-paper palette instead of plain white.");

            var buttons = modal.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.Select(button => button.name),
                Is.EquivalentTo(new[] { "ContinueEditingButton", "DiscardChangesButton" }));
            Assert.That(buttons.All(button => button.transform.parent == card.transform), Is.True,
                "Modal choices must read as controls inside the card, not floating catalogue controls.");
        }

        [UnityTest]
        public IEnumerator Entering_floor_mode_fades_only_confirmed_furniture_before_any_floor_preview()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            var furnitureRenderers = GetConfirmedFurnitureRenderers();
            var wallRenderers = Object.FindObjectsByType<WallSurfaceAuthoring>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .SelectMany(wall => wall.GetComponentsInChildren<Renderer>(true))
                .Where(renderer => renderer.enabled)
                .ToArray();
            Assert.That(furnitureRenderers, Is.Not.Empty);
            Assert.That(wallRenderers, Is.Not.Empty);

            controller.EnterDecorationMode();
            yield return null;
            Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);

            Assert.That(furnitureRenderers.All(UsesFloorFadeMaterial), Is.True,
                "Every confirmed Furniture representation must fade as soon as Floor mode opens.");
            Assert.That(wallRenderers.Any(UsesFloorFadeMaterial), Is.False,
                "Floor readability must not fade Wall surfaces.");
            Assert.That(controller.ActiveSurfacePreview, Is.Null,
                "The fade must not depend on first choosing a floor style.");
        }

        [UnityTest]
        public IEnumerator Floor_furniture_fade_restores_exact_materials_and_property_blocks_on_switch_and_exit()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            var furnitureRenderers = GetConfirmedFurnitureRenderers();
            Assert.That(furnitureRenderers, Is.Not.Empty);
            var sourceMaterials = furnitureRenderers.ToDictionary(
                renderer => renderer,
                renderer => renderer.sharedMaterials.ToArray());
            const float sentinel = 29f;
            var sentinelRenderer = furnitureRenderers[0];
            var sourceBlock = new MaterialPropertyBlock();
            sourceBlock.SetFloat("_Task29Sentinel", sentinel);
            sentinelRenderer.SetPropertyBlock(sourceBlock);

            controller.EnterDecorationMode();
            yield return null;
            Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            Assert.That(furnitureRenderers.All(UsesFloorFadeMaterial), Is.True);

            Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
            AssertFurnitureRenderingRestored(furnitureRenderers, sourceMaterials, sentinelRenderer, sentinel);

            Assert.That(controller.TryChangeMode(DecorationModeKind.Floor), Is.True);
            Assert.That(furnitureRenderers.All(UsesFloorFadeMaterial), Is.True);
            controller.ExitDecorationMode();
            AssertFurnitureRenderingRestored(furnitureRenderers, sourceMaterials, sentinelRenderer, sentinel);
        }

        [UnityTest]
        public IEnumerator Production_fade_uses_real_camera_target_and_restores_blocker_materials_and_mpb()
        {
            yield return LoadMainCafe();
            var fade = Object.FindFirstObjectByType<WallOcclusionFadeView>();
            var camera = Object.FindObjectsByType<UnityEngine.Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.CompareTag("MainCamera"));
            var target = Object.FindObjectsByType<WallSurfaceAuthoring>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.SurfaceId == "wall.back-left").GetComponentInChildren<Renderer>(true);
            var targetMaterials = target.sharedMaterials;
            var targetBlock = new MaterialPropertyBlock(); targetBlock.SetFloat("_Task11TargetSentinel", 17f); target.SetPropertyBlock(targetBlock);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "TEST_RUNTIME_Task11CameraBlocker";
            blocker.AddComponent<OcclusionFadeRepresentationRoot>();
            var targetScreenPoint = camera.WorldToScreenPoint(target.bounds.center);
            var targetViewRay = camera.ScreenPointToRay(targetScreenPoint);
            var targetDistance = Vector3.Dot(
                target.bounds.center - targetViewRay.origin,
                targetViewRay.direction);
            blocker.transform.position = targetViewRay.GetPoint(targetDistance * .5f);
            blocker.transform.localScale = Vector3.one * .6f;
            var blockerRenderer = blocker.GetComponent<Renderer>();
            var sourceMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            blockerRenderer.sharedMaterials = new[] { sourceMaterial, sourceMaterial };
            var sourceMaterials = blockerRenderer.sharedMaterials;
            var sourceBlock = new MaterialPropertyBlock(); sourceBlock.SetFloat("_Task11BlockerSentinel", 23f); blockerRenderer.SetPropertyBlock(sourceBlock);
            try
            {
                fade.ConfigureTarget(target);
                fade.FadeBlockersForTarget();
                Assert.That(blockerRenderer.sharedMaterials.All(item => item.shader.name == "AnimalCafe/Phase7/OcclusionFadeDither"), Is.True);
                Assert.That(target.sharedMaterials, Is.EqualTo(targetMaterials), "Selected target must remain opaque.");
                var stillTarget = new MaterialPropertyBlock(); target.GetPropertyBlock(stillTarget);
                Assert.That(stillTarget.GetFloat("_Task11TargetSentinel"), Is.EqualTo(17f));

                fade.RestoreAllFades();
                Assert.That(blockerRenderer.sharedMaterials, Is.EqualTo(sourceMaterials));
                var restored = new MaterialPropertyBlock(); blockerRenderer.GetPropertyBlock(restored);
                Assert.That(restored.GetFloat("_Task11BlockerSentinel"), Is.EqualTo(23f));
            }
            finally
            {
                fade.RestoreAllFades();
                Object.Destroy(blocker);
                Object.Destroy(sourceMaterial);
            }
        }

        [UnityTest]
        public IEnumerator Production_wall_selection_highlights_only_target_and_restores_on_mode_switch()
        {
            yield return LoadMainCafe();
            var controller = Object.FindFirstObjectByType<DecorationModeController>();
            controller.EnterDecorationMode(); yield return null;
            Assert.That(controller.TryChangeMode(DecorationModeKind.Wall), Is.True);
            var target = Object.FindObjectsByType<WallSurfaceAuthoring>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.SurfaceId == "wall.back-left");
            var other = Object.FindObjectsByType<WallSurfaceAuthoring>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.SurfaceId == "wall.back-right");
            Assert.That(controller.TryHandleSceneTap(new DecorationTouchHit(
                DecorationTouchHitKind.WallSurface, surfaceId: target.SurfaceId)), Is.True);
            var targetBlock = new MaterialPropertyBlock(); target.GetComponentInChildren<Renderer>(true).GetPropertyBlock(targetBlock);
            var otherBlock = new MaterialPropertyBlock(); other.GetComponentInChildren<Renderer>(true).GetPropertyBlock(otherBlock);
            Assert.That(targetBlock.GetFloat("_SelectionHighlight"), Is.EqualTo(1f));
            Assert.That(otherBlock.GetFloat("_SelectionHighlight"), Is.EqualTo(0f));

            var catalogue = Object.FindFirstObjectByType<DecorationCatalogueView>();
            var tiles = catalogue.GetComponentsInChildren<DecorationCatalogueTileView>(true);
            var wallpaper = tiles.Single(item => item.ItemId == "wallpaper.cream-floral");
            wallpaper.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(wallpaper.transform.Find("PreviewOutline").gameObject.activeSelf, Is.True);
            Assert.That(controller.TryConfirmPhase7Preview(), Is.True);

            var wainscotingNone = tiles.Single(item => item.ItemId == "wainscoting.none");
            var rail = tiles.Single(item => item.ItemId == "wainscoting.warm-white-rail");
            rail.GetComponent<Button>().onClick.Invoke();
            yield return null;
            Assert.That(wallpaper.transform.Find("UsingCheck").gameObject.activeSelf, Is.True);
            Assert.That(wainscotingNone.transform.Find("UsingCheck").gameObject.activeSelf, Is.True);
            Assert.That(rail.transform.Find("PreviewOutline").gameObject.activeSelf, Is.True);
            CaptureUiEvidence("outputs/phase7-ui-fix/MainCafe_Wall_CurrentPreview.png", catalogue);
            controller.CancelActivePhase7Preview();

            Assert.That(controller.TryChangeMode(DecorationModeKind.Furniture), Is.True);
            target.GetComponentInChildren<Renderer>(true).GetPropertyBlock(targetBlock);
            Assert.That(targetBlock.GetFloat("_SelectionHighlight"), Is.EqualTo(0f));
        }

        private static IEnumerator LoadMainCafe()
        {
            EditorSceneManager.LoadSceneInPlayMode(MainCafe, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
        }

        private static Renderer[] GetConfirmedFurnitureRenderers()
        {
            var runtime = Object.FindFirstObjectByType<CafeLayoutRuntime>();
            var registry = Object.FindFirstObjectByType<FurnitureSceneRegistry>();
            var representations = runtime.Layout.FurnitureInstances.Select(instance =>
            {
                Assert.That(registry.TryGet(instance.InstanceId, out var representation), Is.True,
                    instance.InstanceId);
                return representation;
            }).ToArray();
            return representations.SelectMany(representation =>
                    representation.GetComponentsInChildren<Renderer>(true))
                .Where(renderer => renderer.enabled && renderer.sharedMaterials.Length > 0)
                .ToArray();
        }

        private static bool UsesFloorFadeMaterial(Renderer renderer)
        {
            return renderer.sharedMaterials.All(material =>
                material != null && material.shader.name == "AnimalCafe/Phase7/OcclusionFadeDither");
        }

        private static void AssertFurnitureRenderingRestored(
            IEnumerable<Renderer> renderers,
            IReadOnlyDictionary<Renderer, Material[]> sourceMaterials,
            Renderer sentinelRenderer,
            float sentinel)
        {
            foreach (var renderer in renderers)
            {
                Assert.That(renderer.sharedMaterials, Is.EqualTo(sourceMaterials[renderer]), renderer.name);
            }

            var restored = new MaterialPropertyBlock();
            sentinelRenderer.GetPropertyBlock(restored);
            Assert.That(restored.GetFloat("_Task29Sentinel"), Is.EqualTo(sentinel));
        }

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var canvas = rect.GetComponentInParent<Canvas>()?.rootCanvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var min = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
            var max = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Bounds CombinedBounds(Renderer[] renderers)
        {
            Assert.That(renderers, Is.Not.Null.And.Not.Empty);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }

        private static void CaptureUiEvidence(string path, DecorationCatalogueView catalogue)
        {
            var canvas = catalogue.GetComponentInParent<Canvas>().rootCanvas;
            var camera = Object.FindObjectsByType<UnityEngine.Camera>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.CompareTag("MainCamera"));
            var originalRenderMode = canvas.renderMode;
            var originalCamera = canvas.worldCamera;
            var originalPlaneDistance = canvas.planeDistance;
            var originalTarget = camera.targetTexture;
            var originalActive = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                target.Create();
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = originalTarget;
                canvas.renderMode = originalRenderMode;
                canvas.worldCamera = originalCamera;
                canvas.planeDistance = originalPlaneDistance;
                RenderTexture.active = originalActive;
                Canvas.ForceUpdateCanvases();
                target.Release();
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }

        private static void AssertTop(Button button)
        {
            var rect = (RectTransform)button.transform;
            var canvas = rect.GetComponentInParent<Canvas>()?.rootCanvas;
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var center = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var data = new PointerEventData(EventSystem.current) { position = center };
            var hits = new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.transform.IsChildOf(button.transform), Is.True,
                button.name + " top=" + string.Join(",", hits.Take(8).Select(x => x.gameObject.name)));
        }
    }
}
#endif

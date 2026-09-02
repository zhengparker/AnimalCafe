using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode
{
    /// <summary>
    /// Task 7 scene contracts. These tests intentionally use reflection for the
    /// first RED run: the components do not exist yet, so the runner records a
    /// real missing-runtime-component failure instead of a compiler-only error.
    /// Task 7 场景合同：首次 RED 使用 reflection，确保记录真实缺失组件失败。
    /// </summary>
    public sealed class Phase7SurfaceScenePlayModeTests
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (var index = owned.Count - 1; index >= 0; index--)
            {
                if (owned[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(owned[index]);
                }
            }

            owned.Clear();
        }

        [Test]
        public void SceneRendering_RequiresTask7RuntimeComponentsAndLayeredShader()
        {
            AssertRequiredComponent("AnimalCafe.Decoration.WallSurfaceRegistry");
            AssertRequiredComponent("AnimalCafe.Decoration.WallSurfaceView");
            AssertRequiredComponent("AnimalCafe.Decoration.FloorSurfaceGridView");
            AssertRequiredComponent("AnimalCafe.Decoration.WallMountedSceneRegistry");
            AssertRequiredComponent("AnimalCafe.Decoration.WallMountedPreviewView");
            AssertRequiredComponent("AnimalCafe.Decoration.WallOcclusionFadeView");

            Assert.That(Shader.Find("AnimalCafe/Phase7/WallSurfaceLayered"), Is.Not.Null,
                "Task 7 requires the layered wall shader asset.");
        }

        [Test]
        public void WallSurfaceView_RendersColumnTiledWallpaperAndDerivedWaistCutoffWithoutMaterialMutation()
        {
            var view = AddRequiredComponent("AnimalCafe.Decoration.WallSurfaceView");
            var wall = CreatePrimitive("CanonicalWall", PrimitiveType.Cube);
            wall.transform.localScale = new Vector3(8f, 3f, 0.1f);
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            SetField(authoring, "surfaceId", "wall.back-left");
            SetField(authoring, "columns", 8);

            var source = CreateMaterial();
            var renderer = wall.GetComponent<Renderer>();
            renderer.sharedMaterial = source;
            var before = renderer.sharedMaterial;
            var layout = CreateSurfaceLayout();

            Invoke(view, "Configure", authoring, renderer, 3f, 0.65f);
            Invoke(view, "RenderConfirmed", layout);

            Assert.That((Vector2)GetProperty(view, "WallpaperTiling"),
                Is.EqualTo(new Vector2(8f, 1f)));
            Assert.That((float)GetProperty(view, "WainscotingCutoff"),
                Is.EqualTo(0.65f / 3f).Within(0.0001f),
                "Cutoff must be derived from the shared waist and canonical wall height.");
            Assert.That(renderer.sharedMaterial, Is.SameAs(before),
                "Confirmed rendering must use MaterialPropertyBlock, never mutate source Material.");
        }

        [Test]
        public void WallSurfaceView_BindsActualWallpaperAndWainscotingDefinitionMapsThroughMaterialPropertyBlock()
        {
            var view = AddRequiredComponent("AnimalCafe.Decoration.WallSurfaceView");
            var wall = CreatePrimitive("DefinitionDrivenWall", PrimitiveType.Cube);
            wall.transform.localScale = new Vector3(8f, 3f, 0.1f);
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            SetField(authoring, "surfaceId", "wall.back-left");
            SetField(authoring, "columns", 8);
            var renderer = wall.GetComponent<Renderer>();
            var rendererMaterial = CreateMaterial();
            renderer.sharedMaterial = rendererMaterial;
            var wallpaperMaterial = CreateMaterial();
            var wainscotingMaterial = CreateMaterial();
            var wallpaperTexture = CreateTexture(Color.magenta);
            var wainscotingTexture = CreateTexture(Color.cyan);
            wallpaperMaterial.SetTexture("_BaseMap", wallpaperTexture);
            wallpaperMaterial.SetColor("_BaseColor", new Color(0.4f, 0.7f, 0.3f, 1f));
            wainscotingMaterial.SetTexture("_BaseMap", wainscotingTexture);
            wainscotingMaterial.SetColor("_BaseColor", new Color(0.8f, 0.6f, 0.2f, 1f));
            var lookup = new SurfaceStyleLookup(new[]
            {
                CreateStyle("wallpaper.cream", SurfaceStyleKind.Wallpaper, wallpaperMaterial),
                CreateStyle("wainscot.white", SurfaceStyleKind.Wainscoting, wainscotingMaterial)
            });

            view.GetType().GetMethod("Configure", new[]
            {
                typeof(WallSurfaceAuthoring), typeof(Renderer), typeof(float),
                typeof(SurfaceStyleLookup)
            })?.Invoke(view, new object[] { authoring, renderer, 3f, lookup });
            Invoke(view, "RenderConfirmed", CreateSurfaceLayout());

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetTexture("_BaseMap"), Is.SameAs(wallpaperTexture));
            Assert.That(block.GetTexture("_WainscotingMap"), Is.SameAs(wainscotingTexture));
            AssertColorApproximately(
                block.GetColor("_BaseColor"),
                wallpaperMaterial.GetColor("_BaseColor"));
            AssertColorApproximately(
                block.GetColor("_WainscotingColor"),
                wainscotingMaterial.GetColor("_BaseColor"));
            Assert.That(block.GetVector("_WallpaperTiling"), Is.EqualTo(new Vector4(8f, 1f, 0f, 0f)));
            Assert.That(renderer.sharedMaterial, Is.SameAs(rendererMaterial));
            Assert.That(wallpaperMaterial.GetTexture("_BaseMap"), Is.SameAs(wallpaperTexture));
            Assert.That(wainscotingMaterial.GetTexture("_BaseMap"), Is.SameAs(wainscotingTexture));
        }

        [Test]
        public void WallSurfaceView_ClearPreviewRestoresConfirmedVisualAndLatestConfirmedCache()
        {
            var view = (WallSurfaceView)AddRequiredComponent("AnimalCafe.Decoration.WallSurfaceView");
            var wall = CreatePrimitive("CachedWall", PrimitiveType.Cube);
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            SetField(authoring, "surfaceId", "wall.back-left");
            SetField(authoring, "columns", 8);
            var confirmedTexture = CreateTexture(Color.blue);
            var previewTexture = CreateTexture(Color.red);
            var confirmedMaterial = CreateMaterial();
            var previewMaterial = CreateMaterial();
            confirmedMaterial.SetTexture("_BaseMap", confirmedTexture);
            previewMaterial.SetTexture("_BaseMap", previewTexture);
            var lookup = new SurfaceStyleLookup(new[]
            {
                CreateStyle("wallpaper.cream", SurfaceStyleKind.Wallpaper, confirmedMaterial),
                CreateStyle("paint.sage", SurfaceStyleKind.Paint, previewMaterial),
                CreateStyle("wainscot.white", SurfaceStyleKind.Wainscoting, confirmedMaterial)
            });
            view.Configure(authoring, wall.GetComponent<Renderer>(), 3f, lookup);
            var confirmed = CreateSurfaceLayout();
            var preview = CreateSurfaceLayout("paint.sage", "floor.stone");

            view.RenderConfirmed(confirmed);
            Assert.That(ReadRendererTexture(wall.GetComponent<Renderer>()), Is.SameAs(confirmedTexture));
            view.RenderPreview(CreatePreview(preview));
            Assert.That(ReadRendererTexture(wall.GetComponent<Renderer>()), Is.SameAs(previewTexture));
            view.ClearPreview();
            Assert.That(ReadRendererTexture(wall.GetComponent<Renderer>()), Is.SameAs(confirmedTexture),
                "Cancel must restore the cached confirmed Wall visual.");

            view.RenderPreview(CreatePreview(preview));
            view.RenderConfirmed(preview);
            view.RenderPreview(CreatePreview(confirmed));
            view.ClearPreview();
            Assert.That(ReadRendererTexture(wall.GetComponent<Renderer>()), Is.SameAs(previewTexture),
                "After Confirm, later Clear must restore the latest confirmed Wall visual.");
        }

        [Test]
        public void DecorationModeController_InvalidWallStylePreservesPendingMultiLayerPreview()
        {
            var layout = CreateSurfaceLayout();
            var material = CreateMaterial();
            var thumbnail = Sprite.Create(
                CreateTexture(Color.white),
                new Rect(0f, 0f, 2f, 2f),
                Vector2.zero);
            owned.Add(thumbnail);
            var styles = new[]
            {
                CreateSessionStyle("wallpaper.cream", SurfaceStyleKind.Wallpaper, material, thumbnail),
                CreateSessionStyle("wallpaper.preview", SurfaceStyleKind.Wallpaper, material, thumbnail),
                CreateSessionStyle("wainscot.white", SurfaceStyleKind.Wainscoting, material, thumbnail),
                CreateSessionStyle("wainscot.preview", SurfaceStyleKind.Wainscoting, material, thumbnail),
                CreateSessionStyle("wains.none", SurfaceStyleKind.Wainscoting, null, thumbnail, true),
                CreateSessionStyle("floor.wood", SurfaceStyleKind.Floor, material, thumbnail)
            };
            var controller = CreateObject("WallController").AddComponent<DecorationModeController>();
            SetField(controller, "activeMode", DecorationModeKind.Wall);
            SetField(controller, "surfaceSession", new SurfaceDecorationSession(layout, styles));

            Assert.That(controller.TryBeginWallPreview(
                "wall.back-left", SurfaceStyleKind.Wallpaper, "wallpaper.preview"), Is.True);
            Assert.That(controller.TryBeginWallPreview(
                "wall.back-left", SurfaceStyleKind.Wainscoting, "wainscot.preview"), Is.True);
            var before = controller.ActiveSurfacePreview;
            var previewSnapshotBefore = JsonUtility.ToJson(before.ProposedSnapshot);
            var confirmedBefore = JsonUtility.ToJson(layout.CaptureSnapshot());

            Assert.That(controller.TryBeginWallPreview(
                "wall.back-left", SurfaceStyleKind.Floor, "floor.wood"), Is.False);

            var after = controller.ActiveSurfacePreview;
            Assert.That(after, Is.Not.Null);
            Assert.That(after.TargetWallSurfaceId, Is.EqualTo("wall.back-left"));
            Assert.That(JsonUtility.ToJson(after.ProposedSnapshot), Is.EqualTo(previewSnapshotBefore));
            Assert.That(JsonUtility.ToJson(layout.CaptureSnapshot()), Is.EqualTo(confirmedBefore));
            Assert.That(after.HasChanges, Is.True);
            Assert.That(after.PreviewWallBaseStyleId, Is.EqualTo("wallpaper.preview"));
            Assert.That(after.PreviewWallWainscotingStyleId, Is.EqualTo("wainscot.preview"));
            Assert.That(after.UsingWallBaseStyleId, Is.EqualTo("wallpaper.cream"));
            Assert.That(after.UsingWallWainscotingStyleId, Is.EqualTo("wainscot.white"));
        }

        [Test]
        public void WallSurfaceLayeredShader_UsesEightColumnWainscotingUvsAndSupportedCompiledPass()
        {
            var shader = Shader.Find("AnimalCafe/Phase7/WallSurfaceLayered");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True, "The layered Wall shader must compile for this runtime.");
            var material = new Material(shader);
            owned.Add(material);
            Assert.That(material.passCount, Is.GreaterThan(0));
            material.SetFloat("_WainscotingEnabled", 1f);
            material.SetFloat("_WainscotingCutoff", 1f);
            material.SetVector("_WallpaperTiling", new Vector4(8f, 1f, 0f, 0f));
            material.SetTexture("_WainscotingMap", CreateHorizontalStripeTexture());

            var shaderSource = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "Art/Phase7/Shaders/SH_WallSurfaceLayered.shader"));
            Assert.That(shaderSource, Does.Contain("float wallUvY = 1.0 - input.uv.y"),
                "Canonical wall UVs must be normalized so wainscoting remains on the visual bottom.");
            Assert.That(shaderSource,
                Does.Contain("float2(input.uv.x * _WallpaperTiling.x, wallUvY / max(_WainscotingCutoff, 0.0001))"),
                "Wainscoting must tile x by wall columns while only y is normalized by cutoff.");
            Assert.That(shaderSource, Does.Contain("_WainscotingColor"));
            Assert.That(shaderSource,
                Does.Contain("tex2D(_WainscotingMap, wainscotingUv) * _WainscotingColor"),
                "The wainscoting style tint written by WallSurfaceView must be consumed by the shader.");
        }

        [Test]
        public void WallSurfaceView_DerivesCanonicalHeightFromValidatedRendererBounds()
        {
            var view = (WallSurfaceView)AddRequiredComponent("AnimalCafe.Decoration.WallSurfaceView");
            var wall = CreatePrimitive("BoundDerivedWall", PrimitiveType.Cube);
            wall.transform.localScale = new Vector3(8f, 3.25f, 0.1f);
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            SetField(authoring, "surfaceId", "wall.back-left");
            SetField(authoring, "columns", 8);
            var material = CreateMaterial();
            material.SetTexture("_BaseMap", CreateTexture(Color.white));
            var lookup = new SurfaceStyleLookup(new[]
            {
                CreateStyle("wallpaper.cream", SurfaceStyleKind.Wallpaper, material),
                CreateStyle("wainscot.white", SurfaceStyleKind.Wainscoting, material)
            });

            var configureFromBounds = view.GetType().GetMethod("Configure", new[]
            {
                typeof(WallSurfaceAuthoring), typeof(Renderer), typeof(SurfaceStyleLookup)
            });
            Assert.That(configureFromBounds, Is.Not.Null,
                "Wall Surface configuration requires a renderer-bounds canonical-height overload.");
            configureFromBounds.Invoke(view, new object[]
            {
                authoring, wall.GetComponent<Renderer>(), lookup
            });
            view.RenderConfirmed(CreateSurfaceLayout());
            Assert.That(view.WainscotingCutoff,
                Is.EqualTo(CharacterScaleReference.SharedCharacterWaistHeightMeters / 3.25f)
                    .Within(0.0001f));
        }

        [Test]
        public void WallSurfaceView_AssignsArchitecturalShadowOwnershipWithoutAddingInteractionComponents()
        {
            // Catches the fence-like result where the thin Wains finish/lips cast their own
            // large shadows while the architectural Wall body does not cast the room shadow.
            var view = (WallSurfaceView)AddRequiredComponent("AnimalCafe.Decoration.WallSurfaceView");
            var wall = CreateObject("ShadowOwnershipWall");
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            SetField(authoring, "surfaceId", "wall.back-left");
            SetField(authoring, "columns", 8);

            var body = CreatePrimitive("WallVisual", PrimitiveType.Cube);
            body.transform.SetParent(wall.transform, false);
            var finish = CreatePrimitive("Phase7_WallFinish", PrimitiveType.Cube);
            finish.transform.SetParent(wall.transform, false);
            var wains = CreatePrimitive("Phase7_WainscotingFinish", PrimitiveType.Cube);
            wains.transform.SetParent(wall.transform, false);
            var rail = CreatePrimitive("Phase7_WainscotingRailLip", PrimitiveType.Cube);
            rail.transform.SetParent(wall.transform, false);
            var baseboard = CreatePrimitive("Phase7_WainscotingBaseboardLip", PrimitiveType.Cube);
            baseboard.transform.SetParent(wall.transform, false);
            var visuals = new[] { finish, wains, rail, baseboard };
            foreach (var visual in visuals)
            {
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.GetComponent<Renderer>().shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.On;
            }

            var bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var material = CreateMaterial();
            material.SetTexture("_BaseMap", CreateTexture(Color.white));
            var lookup = new SurfaceStyleLookup(new[]
            {
                CreateStyle("wallpaper.cream", SurfaceStyleKind.Wallpaper, material),
                CreateStyle("wainscot.white", SurfaceStyleKind.Wainscoting, material)
            });

            view.Configure(authoring, bodyRenderer, 3f, lookup);
            view.RenderConfirmed(CreateSurfaceLayout());

            Assert.That(bodyRenderer.shadowCastingMode,
                Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.On));
            foreach (var visual in visuals)
            {
                var renderer = visual.GetComponent<Renderer>();
                Assert.That(renderer.shadowCastingMode,
                    Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off), visual.name);
                Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty, visual.name);
                Assert.That(visual.GetComponentsInChildren<NavMeshObstacle>(true), Is.Empty, visual.name);
                Assert.That(visual.GetComponentsInChildren<Rigidbody>(true), Is.Empty, visual.name);
                Assert.That(visual.GetComponentsInChildren<MonoBehaviour>(true)
                        .Any(component => component is UnityEngine.EventSystems.IEventSystemHandler),
                    Is.False, visual.name + " must remain render-only.");
            }
        }

        [Test]
        public void WallSurfaceRegistry_PreservesStableRegistrationAcrossReenableAndPurgesDestroyedViews()
        {
            var registry = (WallSurfaceRegistry)AddRequiredComponent("AnimalCafe.Decoration.WallSurfaceRegistry");
            var viewObject = CreateObject("RegisteredWallView");
            var view = viewObject.AddComponent<WallSurfaceView>();
            var wall = CreatePrimitive("RegisteredWall", PrimitiveType.Cube);
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            SetField(authoring, "surfaceId", "wall.back-left");
            view.Configure(authoring, wall.GetComponent<Renderer>(), 3f, 0.65f);
            registry.Register(view);

            registry.enabled = false;
            registry.enabled = true;
            Assert.That(registry.TryGet("wall.back-left", out var retained), Is.True);
            Assert.That(retained, Is.SameAs(view));
            UnityEngine.Object.DestroyImmediate(viewObject);
            Assert.That(registry.TryGet("wall.back-left", out _), Is.False);
        }

        [Test]
        public void WallMountedSceneRegistry_PreservesStableRegistrationAcrossReenableAndPurgesDestroyedRepresentations()
        {
            var registry = (WallMountedSceneRegistry)AddRequiredComponent("AnimalCafe.Decoration.WallMountedSceneRegistry");
            var representation = CreateObject("RegisteredWallMountedRepresentation");
            registry.Register("wall-mounted.fixture", representation);

            registry.enabled = false;
            registry.enabled = true;
            Assert.That(registry.TryGet("wall-mounted.fixture", out var retained), Is.True);
            Assert.That(retained, Is.SameAs(representation));
            UnityEngine.Object.DestroyImmediate(representation);
            Assert.That(registry.TryGet("wall-mounted.fixture", out _), Is.False);
        }

        [Test]
        public void SceneRegistries_UseLayoutStableIdContractIncludingUnderscoresAndRepeatedSeparators()
        {
            var wallRegistry = (WallSurfaceRegistry)AddRequiredComponent("AnimalCafe.Decoration.WallSurfaceRegistry");
            var mountedRegistry = (WallMountedSceneRegistry)AddRequiredComponent("AnimalCafe.Decoration.WallMountedSceneRegistry");
            var wall = CreatePrimitive("RegistryValidationWall", PrimitiveType.Cube);
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            var view = CreateObject("RegistryValidationView").AddComponent<WallSurfaceView>();
            view.Configure(authoring, wall.GetComponent<Renderer>(), 3f, 0.65f);

            SetField(authoring, "surfaceId", "Wall.Back-Left");
            Assert.Throws<ArgumentException>(() => wallRegistry.Register(view));
            SetField(authoring, "surfaceId", "wall back-left");
            Assert.Throws<ArgumentException>(() => wallRegistry.Register(view));
            SetField(authoring, "surfaceId", "wall..back_left");
            wallRegistry.Register(view);

            var representation = CreateObject("RegistryValidationRepresentation");
            Assert.Throws<ArgumentException>(() => mountedRegistry.Register("Wall-Mounted.Fixture", representation));
            Assert.Throws<ArgumentException>(() => mountedRegistry.Register("wall mounted.fixture", representation));
            mountedRegistry.Register("decor__1..fixture", representation);
            Assert.That(wallRegistry.TryGet("wall..back_left", out var registeredWall), Is.True);
            Assert.That(registeredWall, Is.SameAs(view));
            Assert.That(mountedRegistry.TryGet("decor__1..fixture", out var registeredRepresentation), Is.True);
            Assert.That(registeredRepresentation, Is.SameAs(representation));

            Assert.DoesNotThrow(() => new WallMountedInstance(
                "decor__1..fixture", "wall.decor__1", "wall..back_left",
                new WallSlotPosition(0, 0), new WallFootprint(1, 1)));
        }

        [UnityTest]
        public IEnumerator WallOcclusionFadeView_PreservesTwoSubmeshMaterialAppearancesAndBindings()
        {
            var cameraObject = CreateObject("PerSlotFadeCamera", typeof(UnityEngine.Camera));
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var camera = cameraObject.GetComponent<UnityEngine.Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            var target = CreatePrimitive("PerSlotFadeTarget", PrimitiveType.Cube);
            var blocker = CreateObject("PerSlotFadeBlocker", typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider));
            blocker.transform.position = new Vector3(0f, 0f, -5f);
            blocker.GetComponent<MeshFilter>().sharedMesh = CreateTwoSubmeshPanel();

            var redMap = CreateTexture(Color.red);
            var blueMap = CreateTexture(Color.blue);
            var redSource = CreateMaterial();
            var blueSource = CreateMaterial();
            redSource.SetTexture("_BaseMap", redMap);
            redSource.SetColor("_BaseColor", new Color(0.8f, 0.5f, 0.5f, 1f));
            blueSource.SetTexture("_BaseMap", blueMap);
            blueSource.SetColor("_BaseColor", new Color(0.5f, 0.5f, 0.8f, 1f));
            var blockerRenderer = blocker.GetComponent<Renderer>();
            blockerRenderer.sharedMaterials = new[] { redSource, blueSource };

            var view = (WallOcclusionFadeView)AddRequiredComponent("AnimalCafe.Decoration.WallOcclusionFadeView");
            ConfigureFadeView(view, camera, target.GetComponent<Renderer>(), 1f);
            view.FadeBlockersForTarget();
            yield return null;

            var fadedSlots = blockerRenderer.sharedMaterials;
            Assert.That(fadedSlots, Has.Length.EqualTo(2));
            Assert.That(fadedSlots[0], Is.Not.SameAs(fadedSlots[1]));
            Assert.That(fadedSlots[0].GetTexture("_BaseMap"), Is.SameAs(redMap));
            Assert.That(fadedSlots[1].GetTexture("_BaseMap"), Is.SameAs(blueMap));
            AssertColorApproximately(fadedSlots[0].GetColor("_BaseColor"), redSource.GetColor("_BaseColor"));
            AssertColorApproximately(fadedSlots[1].GetColor("_BaseColor"), blueSource.GetColor("_BaseColor"));

            var pixels = CaptureCameraPixels(camera);
            Assert.That(CountDominantPixels(pixels, false), Is.GreaterThan(100));
            Assert.That(CountDominantBluePixels(pixels), Is.GreaterThan(100));
        }

        [UnityTest]
        public IEnumerator WallOcclusionFadeView_OrthographicOffCenterTargetFadesScreenSpaceBlocker()
        {
            var cameraObject = CreateObject("OffCenterFadeCamera", typeof(UnityEngine.Camera));
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var camera = cameraObject.GetComponent<UnityEngine.Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3f;

            var target = CreatePrimitive("OffCenterFadeTarget", PrimitiveType.Cube);
            target.transform.position = new Vector3(3f, 0f, 0f);
            var blocker = CreatePrimitive("OffCenterScreenSpaceBlocker", PrimitiveType.Cube);
            blocker.transform.position = new Vector3(3f, 0f, -5f);
            blocker.transform.localScale = Vector3.one * 0.6f;
            var sourceMaterial = CreateMaterial();
            blocker.GetComponent<Renderer>().sharedMaterial = sourceMaterial;

            var view = (WallOcclusionFadeView)AddRequiredComponent(
                "AnimalCafe.Decoration.WallOcclusionFadeView");
            ConfigureFadeView(view, camera, target.GetComponent<Renderer>(), 0.35f);

            view.FadeBlockersForTarget();
            yield return null;

            Assert.That(blocker.GetComponent<Renderer>().sharedMaterial.shader.name,
                Is.EqualTo("AnimalCafe/Phase7/OcclusionFadeDither"),
                "An off-center orthographic target must use its screen-space view ray.");
        }

        [UnityTest]
        public IEnumerator WallOcclusionFadeView_FadesBlockerCoveringWallEdgeWithoutCrossingWallCenter()
        {
            var cameraObject = CreateObject("PartialWallFadeCamera", typeof(UnityEngine.Camera));
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var camera = cameraObject.GetComponent<UnityEngine.Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4f;

            var target = CreatePrimitive("WideWallFadeTarget", PrimitiveType.Cube);
            target.transform.localScale = new Vector3(8f, 4f, 0.1f);
            var blocker = CreatePrimitive("LowerLeftFurnitureBlocker", PrimitiveType.Cube);
            blocker.transform.position = new Vector3(-2.4f, -1f, -5f);
            blocker.transform.localScale = new Vector3(2.2f, 1.6f, 1f);
            var sourceMaterial = CreateMaterial();
            blocker.GetComponent<Renderer>().sharedMaterial = sourceMaterial;

            var view = (WallOcclusionFadeView)AddRequiredComponent(
                "AnimalCafe.Decoration.WallOcclusionFadeView");
            ConfigureFadeView(view, camera, target.GetComponent<Renderer>(), 0.35f);

            view.FadeBlockersForTarget();
            yield return null;

            Assert.That(blocker.GetComponent<Renderer>().sharedMaterial.shader.name,
                Is.EqualTo("AnimalCafe/Phase7/OcclusionFadeDither"),
                "Furniture covering a visible part of the selected wall must fade even when it misses the wall-centre ray.");
        }

        [UnityTest]
        public IEnumerator WallOcclusionFadeView_RotatedWallUsesActualSurfacePlaneInsteadOfAabbNearFace()
        {
            var cameraObject = CreateObject("RotatedWallFadeCamera", typeof(UnityEngine.Camera));
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var camera = cameraObject.GetComponent<UnityEngine.Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4f;

            var target = CreatePrimitive("RotatedWallFadeTarget", PrimitiveType.Cube);
            target.transform.localScale = new Vector3(8f, 4f, 0.1f);
            target.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            var blocker = CreatePrimitive("FurnitureBetweenAabbAndWallPlane", PrimitiveType.Cube);
            blocker.transform.position = new Vector3(0f, 0f, -1.5f);
            blocker.transform.localScale = new Vector3(2f, 2f, 0.8f);
            var sourceMaterial = CreateMaterial();
            blocker.GetComponent<Renderer>().sharedMaterial = sourceMaterial;

            var view = (WallOcclusionFadeView)AddRequiredComponent(
                "AnimalCafe.Decoration.WallOcclusionFadeView");
            ConfigureFadeView(view, camera, target.GetComponent<Renderer>(), 0.35f);

            view.FadeBlockersForTarget();
            yield return null;

            Assert.That(blocker.GetComponent<Renderer>().sharedMaterial.shader.name,
                Is.EqualTo("AnimalCafe/Phase7/OcclusionFadeDither"),
                "A blocker in front of a rotated wall plane must fade even when it is behind the renderer AABB near face.");
        }

        [Test]
        public void WallSurfaceRegistry_PrunesDestroyedViewsBeforeRenderAndAllowsStaleReplacement()
        {
            var registry = (WallSurfaceRegistry)AddRequiredComponent("AnimalCafe.Decoration.WallSurfaceRegistry");
            var wall = CreatePrimitive("RegistryRenderWall", PrimitiveType.Cube);
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            SetField(authoring, "surfaceId", "wall.back-left");
            var firstRoot = CreateObject("DestroyedBeforeRenderView");
            var first = firstRoot.AddComponent<WallSurfaceView>();
            first.Configure(authoring, wall.GetComponent<Renderer>(), 3f, 0.65f);
            registry.Register(first);
            Assert.Throws<InvalidOperationException>(() => registry.Register(first));
            UnityEngine.Object.DestroyImmediate(firstRoot);

            Assert.DoesNotThrow(() => registry.RenderConfirmed(CreateSurfaceLayout()));
            var registeredViews = (IDictionary)GetField(registry, "viewsBySurfaceId");
            Assert.That(registeredViews.Count, Is.EqualTo(0),
                "RenderConfirmed must prune destroyed views before it dispatches rendering.");

            var replacement = CreateObject("ReplacementRegisteredWallView").AddComponent<WallSurfaceView>();
            replacement.Configure(authoring, wall.GetComponent<Renderer>(), 3f, 0.65f);
            registry.Register(replacement);
            Assert.That(registry.TryGet("wall.back-left", out var retained), Is.True);
            Assert.That(retained, Is.SameAs(replacement));
        }

        [Test]
        public void WallMountedSceneRegistry_RejectsLiveDuplicateAndAllowsDestroyedReplacement()
        {
            var registry = (WallMountedSceneRegistry)AddRequiredComponent("AnimalCafe.Decoration.WallMountedSceneRegistry");
            var first = CreateObject("FirstMountedRepresentation");
            registry.Register("wall-mounted.fixture", first);
            Assert.Throws<InvalidOperationException>(() => registry.Register(
                "wall-mounted.fixture", CreateObject("DuplicateMountedRepresentation")));
            UnityEngine.Object.DestroyImmediate(first);

            var replacement = CreateObject("ReplacementMountedRepresentation");
            registry.Register("wall-mounted.fixture", replacement);
            Assert.That(registry.TryGet("wall-mounted.fixture", out var retained), Is.True);
            Assert.That(retained, Is.SameAs(replacement));
        }

        [UnityTest]
        public IEnumerator WallMountedPreviewView_RendersDominantGreenRedAndHighContrastCheckCrossPixels()
        {
            var cameraObject = CreateObject("ProjectionPixelCamera", typeof(UnityEngine.Camera));
            cameraObject.transform.position = new Vector3(0f, 1f, -10f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var camera = cameraObject.GetComponent<UnityEngine.Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3f;
            camera.aspect = 640f / 360f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var wall = CreatePrimitive("ProjectionPixelWall", PrimitiveType.Cube);
            wall.transform.localScale = new Vector3(8f, 2f, 0.1f);
            wall.GetComponent<Renderer>().sharedMaterial = CreateProjectionMaterial(new Color(0.2f, 0.2f, 0.2f, 1f));
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            SetField(authoring, "surfaceId", "wall.back-left");
            SetField(authoring, "columns", 8);
            SetField(authoring, "rows", 2);
            var validView = (WallMountedPreviewView)AddRequiredComponent("AnimalCafe.Decoration.WallMountedPreviewView");
            var invalidView = (WallMountedPreviewView)AddRequiredComponent("AnimalCafe.Decoration.WallMountedPreviewView");
            validView.Configure(CreateObject("ValidProjectionRoot").transform,
                CreateProjectionMaterial(Color.green), CreateProjectionMaterial(Color.red));
            invalidView.Configure(CreateObject("InvalidProjectionRoot").transform,
                CreateProjectionMaterial(Color.green), CreateProjectionMaterial(Color.red));
            validView.ShowWallPreview(CreateWallPreview("wall.back-left", new WallSlotPosition(1, 0),
                new WallFootprint(2, 1), true), authoring, true, PlacementFeedbackKey.None);
            invalidView.ShowWallPreview(CreateWallPreview("wall.back-left", new WallSlotPosition(5, 0),
                new WallFootprint(2, 1), false), authoring, false, PlacementFeedbackKey.WallOverlap);

            var validRenderer = validView.CurrentProjection.GetComponent<Renderer>();
            var invalidRenderer = invalidView.CurrentProjection.GetComponent<Renderer>();
            Assert.That(validRenderer.bounds.size.x, Is.GreaterThan(1.9f));
            Assert.That(invalidRenderer.bounds.size.x, Is.GreaterThan(1.9f));
            Assert.That(validRenderer.enabled, Is.True);
            Assert.That(invalidRenderer.enabled, Is.True);
            Assert.That(validRenderer.sharedMaterial.color.g, Is.GreaterThan(0.8f));
            Assert.That(invalidRenderer.sharedMaterial.color.r, Is.GreaterThan(0.8f));
            var validViewport = camera.WorldToViewportPoint(validRenderer.bounds.center);
            var invalidViewport = camera.WorldToViewportPoint(invalidRenderer.bounds.center);
            Assert.That(validViewport.x, Is.InRange(0f, 1f));
            Assert.That(validViewport.y, Is.InRange(0f, 1f));
            Assert.That(validViewport.z, Is.GreaterThan(0f));
            Assert.That(invalidViewport.x, Is.InRange(0f, 1f));
            Assert.That(invalidViewport.y, Is.InRange(0f, 1f));
            Assert.That(invalidViewport.z, Is.GreaterThan(0f));

            var pixels = CaptureCameraPixels(camera);
            var debugDirectory = Path.Combine(Application.dataPath, "..", "outputs", "phase7-task7");
            Directory.CreateDirectory(debugDirectory);
            File.WriteAllBytes(Path.Combine(debugDirectory, "Task7_Projection_ValidInvalid_Technical.png"), pixels.EncodeToPNG());
            Assert.That(CountDominantPixels(pixels, true), Is.GreaterThan(100),
                "Valid projection must render clearly dominant green pixels.");
            Assert.That(CountDominantPixels(pixels, false), Is.GreaterThan(100),
                "Invalid projection must render clearly dominant red pixels.");
            Assert.That(CountHighContrastPixels(pixels), Is.GreaterThan(20),
                "Actual check and cross icon geometry must be high-contrast and visible.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator WallOcclusionFadeView_FadesOnlyScopedSiblingBlockerUnderSharedParent()
        {
            var cameraObject = CreateObject("ScopedFadeCamera", typeof(UnityEngine.Camera));
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var sharedParent = CreateObject("SharedFurnitureParent");
            var target = CreatePrimitive("ScopedTarget", PrimitiveType.Cube);
            target.transform.SetParent(sharedParent.transform, false);
            var targetMarker = ResolveRequiredType("AnimalCafe.Decoration.OcclusionFadeRepresentationRoot");
            target.AddComponent(targetMarker);
            var blocker = CreatePrimitive("ScopedBlocker", PrimitiveType.Cube);
            blocker.transform.SetParent(sharedParent.transform, false);
            blocker.transform.localPosition = new Vector3(0f, 0f, -5f);
            blocker.AddComponent(targetMarker);
            var sibling = CreatePrimitive("UnhitSiblingFurniture", PrimitiveType.Cube);
            sibling.transform.SetParent(sharedParent.transform, false);
            sibling.transform.localPosition = Vector3.right * 4f;
            sibling.AddComponent(targetMarker);
            var source = CreateMaterial();
            target.GetComponent<Renderer>().sharedMaterial = source;
            blocker.GetComponent<Renderer>().sharedMaterial = source;
            sibling.GetComponent<Renderer>().sharedMaterial = source;
            var view = (WallOcclusionFadeView)AddRequiredComponent("AnimalCafe.Decoration.WallOcclusionFadeView");
            ConfigureFadeView(view, cameraObject.GetComponent<UnityEngine.Camera>(), target.GetComponent<Renderer>(), 0.35f);

            view.FadeBlockersForTarget();
            yield return null;
            Assert.That(target.GetComponent<Renderer>().sharedMaterial, Is.SameAs(source));
            Assert.That(blocker.GetComponent<Renderer>().sharedMaterial.shader.name,
                Is.EqualTo("AnimalCafe/Phase7/OcclusionFadeDither"));
            Assert.That(sibling.GetComponent<Renderer>().sharedMaterial, Is.SameAs(source));
        }

        [UnityTest]
        public IEnumerator WallOcclusionFadeView_PreservesSourceTextureAndColorInActualFadedPixels()
        {
            var cameraObject = CreateObject("FadeAppearanceCamera", typeof(UnityEngine.Camera));
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var camera = cameraObject.GetComponent<UnityEngine.Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var target = CreatePrimitive("FadeAppearanceTarget", PrimitiveType.Cube);
            var blocker = CreatePrimitive("FadeAppearanceBlocker", PrimitiveType.Cube);
            blocker.transform.position = new Vector3(0f, 0f, -5f);
            blocker.transform.localScale = new Vector3(4f, 4f, 1f);
            var source = CreateScreenshotMaterial(Color.white);
            source.mainTexture = CreateHorizontalStripeTexture();
            blocker.GetComponent<Renderer>().sharedMaterial = source;
            var view = (WallOcclusionFadeView)AddRequiredComponent("AnimalCafe.Decoration.WallOcclusionFadeView");
            ConfigureFadeView(view, camera, target.GetComponent<Renderer>(), 0.35f);

            view.FadeBlockersForTarget();
            yield return null;
            var fadeMaterial = blocker.GetComponent<Renderer>().sharedMaterial;
            var pixels = CaptureCameraPixels(camera);
            Assert.That(CountDominantPixels(pixels, false), Is.GreaterThan(100),
                "Faded textured blocker must retain red texture pixels instead of becoming white.");
            Assert.That(CountDominantBluePixels(pixels), Is.GreaterThan(100),
                "Faded textured blocker must retain blue texture pixels instead of becoming white.");
            Assert.That(fadeMaterial.HasProperty("_BaseMap"), Is.True);
            Assert.That(fadeMaterial.GetTexture("_BaseMap"), Is.SameAs(source.mainTexture),
                "Every fade Material slot must bind the original visible texture.");
            AssertColorApproximately(fadeMaterial.GetColor("_BaseColor"), source.color);
        }

        [UnityTest]
        public IEnumerator FloorSurfaceGridView_StripsColliderAndNavMeshObstacleFromHostileTemplate()
        {
            var view = (FloorSurfaceGridView)AddRequiredComponent("AnimalCafe.Decoration.FloorSurfaceGridView");
            var floor = CreateObject("HostileTemplateFloor");
            var template = CreatePrimitive("HostileFloorTemplate", PrimitiveType.Quad);
            template.AddComponent<NavMeshObstacle>();
            template.GetComponent<Renderer>().sharedMaterial = CreateMaterial();
            view.Configure(floor.transform, template.GetComponent<Renderer>(), 1f);
            view.RenderConfirmed(CreateSurfaceLayout());
            yield return null;

            Assert.That(floor.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(floor.GetComponentsInChildren<NavMeshObstacle>(true), Is.Empty);
        }

        [Test]
        public void WallOcclusionFadeView_UsesInjectedFadeMaterialBindingInsteadOfShaderFind()
        {
            var configure = typeof(WallOcclusionFadeView).GetMethod("Configure", new[]
            {
                typeof(UnityEngine.Camera), typeof(Renderer), typeof(float), typeof(Material)
            });
            Assert.That(configure, Is.Not.Null,
                "Fade runtime needs a deterministic injected Material binding.");
            var source = File.ReadAllText(Path.Combine(Application.dataPath,
                "Scripts/Decoration/WallOcclusionFadeView.cs"));
            Assert.That(source, Does.Not.Contain("Shader.Find"),
                "Runtime fade must not depend solely on Shader.Find, which Player stripping can remove.");
        }

        [UnityTest]
        public IEnumerator Task7TechnicalScreenshot_CapturesIsolatedWallProjectionForReview()
        {
            var camera = CreateObject("Task7ScreenshotCamera", typeof(UnityEngine.Camera));
            camera.transform.position = new Vector3(0f, 1f, -10f);
            camera.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var unityCamera = camera.GetComponent<UnityEngine.Camera>();
            unityCamera.orthographic = true;
            unityCamera.orthographicSize = 3f;
            unityCamera.clearFlags = CameraClearFlags.SolidColor;
            unityCamera.backgroundColor = new Color(0.08f, 0.1f, 0.12f, 1f);
            var wall = CreatePrimitive("Task7ScreenshotWall", PrimitiveType.Cube);
            wall.transform.localScale = new Vector3(8f, 2f, 0.1f);
            wall.GetComponent<Renderer>().sharedMaterial = CreateScreenshotMaterial(new Color(0.45f, 0.52f, 0.62f, 1f));
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            SetField(authoring, "surfaceId", "wall.back-left");
            SetField(authoring, "columns", 8);
            SetField(authoring, "rows", 2);
            SetField(authoring, "slotSize", 1f);
            var view = (WallMountedPreviewView)AddRequiredComponent("AnimalCafe.Decoration.WallMountedPreviewView");
            var root = CreateObject("Task7ScreenshotProjectionRoot");
            var validMaterial = CreateScreenshotMaterial(Color.green);
            var invalidMaterial = CreateScreenshotMaterial(Color.red);
            view.Configure(root.transform, validMaterial, invalidMaterial);
            view.ShowWallPreview(
                CreateWallPreview("wall.back-left", new WallSlotPosition(2, 0),
                    new WallFootprint(2, 1), true),
                authoring,
                true,
                PlacementFeedbackKey.None);
            var projectionBounds = view.CurrentProjection.GetComponent<Renderer>().bounds;
            var outline = CreateObject("Task7ScreenshotProjectionBounds").AddComponent<LineRenderer>();
            outline.material = validMaterial;
            outline.startColor = Color.green;
            outline.endColor = Color.green;
            outline.startWidth = 0.04f;
            outline.endWidth = 0.04f;
            outline.positionCount = 5;
            var outlineZ = projectionBounds.center.z - 0.01f;
            outline.SetPositions(new[]
            {
                new Vector3(projectionBounds.min.x, projectionBounds.min.y, outlineZ),
                new Vector3(projectionBounds.max.x, projectionBounds.min.y, outlineZ),
                new Vector3(projectionBounds.max.x, projectionBounds.max.y, outlineZ),
                new Vector3(projectionBounds.min.x, projectionBounds.max.y, outlineZ),
                new Vector3(projectionBounds.min.x, projectionBounds.min.y, outlineZ)
            });

            var target = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32);
            owned.Add(target);
            var readback = new Texture2D(640, 360, TextureFormat.RGBA32, false);
            owned.Add(readback);
            unityCamera.targetTexture = target;
            unityCamera.Render();
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0f, 0f, 640f, 360f), 0, 0);
            readback.Apply();
            RenderTexture.active = null;
            unityCamera.targetTexture = null;
            var outputDirectory = Path.Combine(Application.dataPath, "..", "outputs", "phase7-task7");
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, "Task7_Projection_Technical.png");
            File.WriteAllBytes(outputPath, readback.EncodeToPNG());
            Assert.That(File.Exists(outputPath), Is.True);
            Assert.That(new FileInfo(outputPath).Length, Is.GreaterThan(0));
            yield return null;
        }

        [Test]
        public void FloorSurfaceGridView_Creates64ColliderFreeTilesAndStoresQuarterTurnPerGrid()
        {
            var view = AddRequiredComponent("AnimalCafe.Decoration.FloorSurfaceGridView");
            var floor = CreatePrimitive("CanonicalFloor", PrimitiveType.Cube);
            var canonicalCollider = floor.GetComponent<Collider>();
            var tileTemplate = CreatePrimitive("RenderOnlyTileTemplate", PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(tileTemplate.GetComponent<Collider>());
            tileTemplate.GetComponent<Renderer>().sharedMaterial = CreateMaterial();
            var layout = CreateSurfaceLayout();

            Invoke(view, "Configure", floor.transform, tileTemplate.GetComponent<Renderer>(), 1f);
            Invoke(view, "RenderConfirmed", layout);

            Assert.That((int)GetProperty(view, "RenderTileCount"), Is.EqualTo(64));
            Assert.That(floor.GetComponent<Collider>(), Is.SameAs(canonicalCollider));
            Assert.That(floor.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(1),
                "Render-only Floor tiles must not add gameplay Colliders.");
            Assert.That((int)Invoke(view, "GetQuarterTurns", new GridPosition(7, 7)), Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator FloorSurfaceGridView_CubeTemplateStillCreatesFlatRenderOnlyTiles()
        {
            var view = AddRequiredComponent("AnimalCafe.Decoration.FloorSurfaceGridView");
            var floorRoot = CreateObject("CanonicalGridRoot");
            var cubeTemplate = CreatePrimitive("AuthoredFloorVisual", PrimitiveType.Cube);
            cubeTemplate.GetComponent<Renderer>().sharedMaterial = CreateMaterial();

            Invoke(view, "Configure", floorRoot.transform, cubeTemplate.GetComponent<Renderer>(), 1f);
            Invoke(view, "RenderConfirmed", CreateSurfaceLayout());
            yield return null;

            var tile = floorRoot.transform.Find("FloorSurfaceTile_0_0");
            Assert.That(tile, Is.Not.Null);
            Assert.That(tile.GetComponentsInChildren<Collider>(true), Is.Empty,
                "Render-only Floor tiles must not add gameplay Colliders.");
            Assert.That(tile.GetComponent<Renderer>().bounds.size.y, Is.LessThan(0.001f),
                "A Floor texture tile must be a flat surface even when the authored Floor Renderer is a Cube.");
        }

        [Test]
        public void FloorSurfaceGridView_UsesCanonicalGridCentersAndDefinitionTextureWithShaderQuarterTurns()
        {
            var view = AddRequiredComponent("AnimalCafe.Decoration.FloorSurfaceGridView");
            var floorRoot = CreateObject("CanonicalGridRoot");
            floorRoot.transform.position = new Vector3(10f, 2f, -3f);
            var visual = CreatePrimitive("ScaledFloorVisual", PrimitiveType.Cube);
            visual.transform.SetParent(floorRoot.transform, false);
            visual.transform.localScale = new Vector3(8f, 1f, 8f);
            UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
            var authoritativeCollider = floorRoot.AddComponent<BoxCollider>();
            var template = CreatePrimitive("FloorRenderTemplate", PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(template.GetComponent<Collider>());
            var floorShader = Shader.Find("AnimalCafe/Phase7/FloorSurfaceTiled");
            Assert.That(floorShader, Is.Not.Null, "Floor quarter turns require a consuming runtime shader.");
            var renderMaterial = new Material(floorShader);
            owned.Add(renderMaterial);
            template.GetComponent<Renderer>().sharedMaterial = renderMaterial;
            var floorTexture = CreateTexture(Color.yellow);
            var floorDefinitionMaterial = CreateMaterial();
            floorDefinitionMaterial.SetTexture("_BaseMap", floorTexture);
            var lookup = new SurfaceStyleLookup(new[]
            {
                CreateStyle("floor.wood", SurfaceStyleKind.Floor, floorDefinitionMaterial)
            });
            var gridSpace = new DecorationGridSpace(
                new GridSettings(1f),
                new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8)));

            ((FloorSurfaceGridView)view).Configure(
                floorRoot.transform,
                gridSpace,
                template.GetComponent<Renderer>(),
                0.04f,
                lookup);
            Invoke(view, "RenderConfirmed", CreateSurfaceLayout());

            var tile = floorRoot.transform.Find("FloorSurfaceTile_7_7");
            Assert.That(tile, Is.Not.Null);
            Assert.That(tile.position,
                Is.EqualTo(floorRoot.transform.TransformPoint(
                    gridSpace.GetCellCenterLocal(new GridPosition(7, 7), 0.04f))));
            var block = new MaterialPropertyBlock();
            var tileRenderer = tile.GetComponent<Renderer>();
            tileRenderer.GetPropertyBlock(block);
            Assert.That(block.GetTexture("_BaseMap"), Is.SameAs(floorTexture));
            Assert.That(block.GetFloat("_SurfaceRotationQuarterTurns"), Is.EqualTo(3f));
            Assert.That(tileRenderer.sharedMaterial.shader.name,
                Is.EqualTo("AnimalCafe/Phase7/FloorSurfaceTiled"));
            Assert.That(floorRoot.GetComponentsInChildren<Collider>(true),
                Is.EqualTo(new[] { authoritativeCollider }));
        }

        [UnityTest]
        public IEnumerator FloorSurfaceGridView_ClearPreviewRestoresConfirmedTilesAndLatestConfirmedCache()
        {
            var view = (FloorSurfaceGridView)AddRequiredComponent("AnimalCafe.Decoration.FloorSurfaceGridView");
            var floor = CreateObject("CachedFloor");
            var template = CreatePrimitive("CachedFloorTileTemplate", PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(template.GetComponent<Collider>());
            var confirmedTexture = CreateTexture(Color.blue);
            var previewTexture = CreateTexture(Color.red);
            var confirmedMaterial = CreateMaterial();
            var previewMaterial = CreateMaterial();
            confirmedMaterial.SetTexture("_BaseMap", confirmedTexture);
            previewMaterial.SetTexture("_BaseMap", previewTexture);
            template.GetComponent<Renderer>().sharedMaterial = CreateMaterial();
            var lookup = new SurfaceStyleLookup(new[]
            {
                CreateStyle("floor.wood", SurfaceStyleKind.Floor, confirmedMaterial),
                CreateStyle("floor.stone", SurfaceStyleKind.Floor, previewMaterial)
            });
            var gridSpace = new DecorationGridSpace(
                new GridSettings(1f),
                new LayoutBounds(new GridPosition(0, 0), new GridSize(8, 8)));
            view.Configure(floor.transform, gridSpace, template.GetComponent<Renderer>(), 0.04f, lookup);
            var confirmed = CreateSurfaceLayout();
            var preview = CreateSurfaceLayout("wallpaper.cream", "floor.stone");

            view.RenderConfirmed(confirmed);
            Assert.That(ReadActiveFloorTileTexture(floor.transform, new GridPosition(0, 0)),
                Is.SameAs(confirmedTexture));
            view.RenderPreview(CreatePreview(preview));
            Assert.That(ReadActiveFloorTileTexture(floor.transform, new GridPosition(0, 0)),
                Is.SameAs(previewTexture));
            view.ClearPreview();
            yield return null;
            Assert.That((int)GetProperty(view, "RenderTileCount"), Is.EqualTo(64));
            Assert.That(ReadActiveFloorTileTexture(floor.transform, new GridPosition(0, 0)),
                Is.SameAs(confirmedTexture), "Cancel must restore cached confirmed Floor tiles.");

            view.RenderPreview(CreatePreview(preview));
            view.RenderConfirmed(preview);
            view.RenderPreview(CreatePreview(confirmed));
            view.ClearPreview();
            yield return null;
            Assert.That(ReadActiveFloorTileTexture(floor.transform, new GridPosition(0, 0)),
                Is.SameAs(previewTexture), "After Confirm, later Clear must restore latest confirmed Floor tiles.");
        }

        [UnityTest]
        public IEnumerator IT021_FloorSelectionFeedback_RendersOneOutlineAndOrderedColliderFreeChecksThenClears()
        {
            // Catches a production break where Scene feedback is missing, stale, unordered,
            // or accidentally becomes a Collider/NavMesh/input target.
            var view = (FloorSurfaceGridView)AddRequiredComponent("AnimalCafe.Decoration.FloorSurfaceGridView");
            var floorRoot = CreateObject("FeedbackFloorRoot");
            var template = CreatePrimitive("FeedbackTileTemplate", PrimitiveType.Quad);
            UnityEngine.Object.DestroyImmediate(template.GetComponent<Collider>());
            template.GetComponent<Renderer>().sharedMaterial = CreateMaterial();
            view.Configure(floorRoot.transform, template.GetComponent<Renderer>(), 1f);
            view.ConfigureSelectionFeedback(CreateMaterial());
            view.RenderConfirmed(CreateSurfaceLayout());

            view.RenderSelectionFeedback(
                new GridPosition(3, 2),
                new[] { new GridPosition(0, 0), new GridPosition(7, 7) });
            yield return null;

            var feedback = floorRoot.transform.Find("FloorSelectionFeedback");
            Assert.That(feedback, Is.Not.Null);
            Assert.That(feedback.Find("SelectedOutline_3_2"), Is.Not.Null,
                "The selected Floor needs one render-only outline.");
            Assert.That(feedback.Find("PreviewCheck_0_0"), Is.Not.Null);
            Assert.That(feedback.Find("PreviewCheck_7_7"), Is.Not.Null);
            Assert.That(feedback.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(feedback.GetComponentsInChildren<NavMeshObstacle>(true), Is.Empty);

            view.ClearSelectionFeedback();
            yield return null;
            Assert.That(floorRoot.transform.Find("FloorSelectionFeedback"), Is.Null,
                "Cancel, Confirm, mode exit, and OnDisable must leave no stale feedback objects.");
        }

        [Test]
        public void WallMountedPreviewView_ProjectsOutsideRendererFaceWithActualDistinctValidAndInvalidGeometry()
        {
            var view = AddRequiredComponent("AnimalCafe.Decoration.WallMountedPreviewView");
            var root = CreateObject("ProjectionRoot");
            var targetWall = CreatePrimitive("TargetWall", PrimitiveType.Cube);
            targetWall.transform.localScale = new Vector3(8f, 2f, 0.1f);
            var otherWall = CreatePrimitive("OtherWall", PrimitiveType.Cube);
            var authoring = targetWall.AddComponent<WallSurfaceAuthoring>();
            SetField(authoring, "surfaceId", "wall.back-left");
            SetField(authoring, "columns", 8);
            SetField(authoring, "rows", 2);
            SetField(authoring, "slotSize", 1f);
            var preview = CreateWallPreview("wall.back-left", new WallSlotPosition(2, 0),
                new WallFootprint(2, 1), true);

            var validMaterial = CreateMaterial();
            var invalidMaterial = CreateMaterial();
            Invoke(view, "Configure", root.transform, validMaterial, invalidMaterial);
            Invoke(view, "ShowWallPreview", preview, authoring, true, PlacementFeedbackKey.None, null);

            var projection = (GameObject)GetProperty(view, "CurrentProjection");
            Assert.That(projection, Is.Not.Null);
            Assert.That(projection.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(projection.GetComponent<Renderer>().bounds.size.x,
                Is.EqualTo(2f).Within(0.0001f));
            Assert.That(projection.GetComponent<Renderer>().bounds.size.y,
                Is.EqualTo(1f).Within(0.0001f));
            var surfaceBounds = targetWall.GetComponent<Renderer>().bounds;
            var projectionBounds = projection.GetComponent<Renderer>().bounds;
            Assert.That(projectionBounds.max.z,
                Is.LessThanOrEqualTo(surfaceBounds.min.z - 0.001f),
                "The projection must sit outside the renderer's visible face with an epsilon.");
            Assert.That(Vector3.Dot(projection.transform.TransformDirection(Vector3.back), -targetWall.transform.forward), Is.GreaterThan(0.99f),
                "The Quad's visible local -Z face must point toward the same visible side as its outward offset.");
            Assert.That(projection.GetComponent<Renderer>().sharedMaterial, Is.SameAs(validMaterial));
            var validIcon = Array.Find(
                projection.GetComponentsInChildren<MeshFilter>(),
                filter => filter.gameObject != projection);
            Assert.That(validIcon, Is.Not.Null, "Valid Preview requires actual check geometry.");
            var validVertices = validIcon.sharedMesh.vertices;
            Assert.That(validVertices, Has.Length.GreaterThan(3));
            Assert.That(projection.transform.IsChildOf(otherWall.transform), Is.False,
                "Projection must never render on a different Surface.");

            Invoke(view, "ShowWallPreview", preview, authoring, false,
                PlacementFeedbackKey.WallOverlap, null);
            var invalidProjection = (GameObject)GetProperty(view, "CurrentProjection");
            Assert.That(invalidProjection.GetComponent<Renderer>().sharedMaterial, Is.SameAs(invalidMaterial));
            var invalidIcon = Array.Find(
                invalidProjection.GetComponentsInChildren<MeshFilter>(),
                filter => filter.gameObject != invalidProjection);
            Assert.That(invalidIcon, Is.Not.Null, "Invalid Preview requires actual cross geometry.");
            Assert.That(invalidIcon.sharedMesh.vertices, Is.Not.EqualTo(validVertices),
                "Check and cross must be represented by different geometry, not only object names.");
        }

        [Test]
        public void WallMountedPreviewView_UsesACompleteFilledFootprintLikeFurnitureMode()
        {
            // Catches a visual regression where Wall Decor shows only an outline
            // instead of the same complete valid/invalid footprint used by Furniture.
            var view = (WallMountedPreviewView)AddRequiredComponent(
                "AnimalCafe.Decoration.WallMountedPreviewView");
            var root = CreateObject("FilledProjectionRoot");
            var wall = CreatePrimitive("FilledProjectionWall", PrimitiveType.Cube);
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            SetField(authoring, "surfaceId", "wall.back-left");
            SetField(authoring, "columns", 8);
            SetField(authoring, "rows", 2);
            SetField(authoring, "slotSize", 1f);
            view.Configure(root.transform, CreateMaterial(), CreateMaterial());

            view.ShowWallPreview(
                CreateWallPreview(
                    "wall.back-left",
                    new WallSlotPosition(2, 0),
                    new WallFootprint(1, 2),
                    true),
                authoring,
                true,
                PlacementFeedbackKey.None);

            var projection = view.CurrentProjection;
            var mesh = projection.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.name, Is.EqualTo("WallPreviewFootprintFill"));
            Assert.That(mesh.vertexCount, Is.EqualTo(4),
                "A complete rectangular footprint needs four filled corners.");
            Assert.That(mesh.triangles, Has.Length.EqualTo(6),
                "Two triangles must fill the entire one-grid by two-grid footprint.");
            Assert.That(projection.transform.localScale, Is.EqualTo(Vector3.one),
                "The fill mesh itself must own the exact one-grid by two-grid dimensions.");

            var vertices = mesh.vertices;
            Assert.That(vertices.Min(vertex => vertex.x), Is.EqualTo(-.5f).Within(.0001f));
            Assert.That(vertices.Max(vertex => vertex.x), Is.EqualTo(.5f).Within(.0001f));
            Assert.That(vertices.Min(vertex => vertex.y), Is.EqualTo(-1f).Within(.0001f));
            Assert.That(vertices.Max(vertex => vertex.y), Is.EqualTo(1f).Within(.0001f));
        }

        [Test]
        public void WallMountedPreviewView_ClearsCurrentProjectionWhenSurfaceIdDoesNotMatch()
        {
            var view = AddRequiredComponent("AnimalCafe.Decoration.WallMountedPreviewView");
            var root = CreateObject("MismatchProjectionRoot");
            var wall = CreatePrimitive("MismatchWall", PrimitiveType.Cube);
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            SetField(authoring, "surfaceId", "wall.back-left");
            Invoke(view, "Configure", root.transform, CreateMaterial(), CreateMaterial());
            Invoke(view, "ShowWallPreview", CreateWallPreview("wall.back-left", new WallSlotPosition(0, 0),
                new WallFootprint(1, 1), true), authoring, true, PlacementFeedbackKey.None, null);
            Assert.That(GetProperty(view, "CurrentProjection"), Is.Not.Null);

            Invoke(view, "ShowWallPreview", CreateWallPreview("wall.other", new WallSlotPosition(0, 0),
                new WallFootprint(1, 1), true), authoring, true, PlacementFeedbackKey.None, null);
            Assert.That(GetProperty(view, "CurrentProjection"), Is.Null,
                "A mismatched SurfaceId must never leave a stale projection visible.");
        }

        [Test]
        public void WallMountedPreviewView_InstantiatesColliderFreeRealPrefabGhostAndClearsIt()
        {
            var view = (WallMountedPreviewView)AddRequiredComponent("AnimalCafe.Decoration.WallMountedPreviewView");
            var root = CreateObject("GhostProjectionRoot");
            var wall = CreatePrimitive("GhostWall", PrimitiveType.Cube);
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            SetField(authoring, "surfaceId", "wall.back-left");
            SetField(authoring, "columns", 8); SetField(authoring, "rows", 2); SetField(authoring, "slotSize", 1f);
            var prefab = CreatePrimitive("PaintingPrefab", PrimitiveType.Cube);
            view.Configure(root.transform, CreateMaterial(), CreateMaterial());
            Invoke(view, "ShowWallPreview", CreateWallPreview("wall.back-left", new WallSlotPosition(1, 0),
                new WallFootprint(1, 2), true), authoring, true, PlacementFeedbackKey.None, prefab);
            var ghost = (GameObject)GetProperty(view, "CurrentGhost");
            Assert.That(ghost, Is.Not.Null);
            Assert.That(ghost.name, Does.Contain("PaintingPrefab"));
            Assert.That(ghost.GetComponentsInChildren<Collider>(true), Is.Empty,
                "Preview ghost must not add physics/nav blockage.");
            view.ClearPreview();
            Assert.That(GetProperty(view, "CurrentGhost"), Is.Null);
        }

        [TestCase(
            "Assets/Art/Phase7/Definitions/WD_WallDecor_Monitor_01.asset",
            "wall-decor.monitor.01",
            "Assets/Art/Phase7/Prefabs/PF_WallDecor_1x1_01.prefab")]
        [TestCase(
            "Assets/Art/Phase7/Definitions/WD_WallDecor_ShibaPainting_01.asset",
            "wall-decor.shiba-painting.01",
            "Assets/Art/Phase7/Prefabs/PF_WallDecor_1x2_01.prefab")]
        [TestCase(
            "Assets/Art/Phase7/Definitions/WD_WallDecor_WoodShelf_01.asset",
            "wall-decor.wood-shelf.01",
            "Assets/Art/Phase7/Prefabs/PF_WallDecor_2x1_01.prefab")]
        [TestCase(
            "Assets/Art/Phase7/Definitions/WD_Window_Canonical.asset",
            "window.canonical.phase4",
            "Assets/Art/Phase7/Prefabs/PF_Window_1x1_01.prefab")]
        [TestCase(
            "Assets/Art/Phase7/Definitions/WD_Window_TallGlass_1x2_01.asset",
            "window.tall-glass.1x2.01",
            "Assets/Art/Phase7/Prefabs/PF_Window_1x2_01.prefab")]
        public void WallMountedPreviewView_ProductionPrefabGhostIsRealUprightAndWallLocal(
            string definitionPath,
            string expectedDefinitionId,
            string expectedPrefabPath)
        {
            var definition = LoadEditorAsset<WallMountedDefinitionAsset>(definitionPath);
            Assert.That(definition, Is.Not.Null, definitionPath);
            Assert.That(definition.DefinitionId, Is.EqualTo(expectedDefinitionId));
            Assert.That(GetEditorAssetPath(definition.Prefab), Is.EqualTo(expectedPrefabPath));

            var sourceRenderers = definition.Prefab.GetComponentsInChildren<Renderer>(true);
            var sourceMaterials = sourceRenderers.SelectMany(item => item.sharedMaterials)
                .Where(item => item != null).Distinct().ToArray();
            var sourceMeshes = definition.Prefab.GetComponentsInChildren<MeshFilter>(true)
                .Select(item => item.sharedMesh).Where(item => item != null).Distinct().ToArray();
            Assert.That(sourceRenderers, Is.Not.Empty, expectedDefinitionId);
            Assert.That(sourceMaterials, Is.Not.Empty, expectedDefinitionId);
            Assert.That(sourceMeshes, Is.Not.Empty, expectedDefinitionId);

            var view = (WallMountedPreviewView)AddRequiredComponent(
                "AnimalCafe.Decoration.WallMountedPreviewView");
            var projectionRoot = CreateObject("ProductionGhostProjectionRoot");
            var wall = CreateObject("ProductionGhostWall");
            wall.transform.SetPositionAndRotation(
                Vector3.zero,
                Quaternion.Euler(0f, 37f, 0f));
            var wallVisual = CreatePrimitive("ProductionGhostWallVisual", PrimitiveType.Cube);
            wallVisual.transform.SetParent(wall.transform, false);
            wallVisual.transform.localPosition = Vector3.up;
            wallVisual.transform.localScale = new Vector3(8f, 2f, 0.1f);
            var finish = CreatePrimitive("Phase7_WallFinish", PrimitiveType.Cube);
            finish.transform.SetParent(wall.transform, false);
            finish.transform.localPosition = new Vector3(0f, 1f, -.08f);
            finish.transform.localScale = new Vector3(8f, 2f, .02f);
            UnityEngine.Object.DestroyImmediate(finish.GetComponent<Collider>());
            var rail = CreatePrimitive("Phase7_WainscotingRailLip", PrimitiveType.Cube);
            rail.transform.SetParent(wall.transform, false);
            rail.transform.localPosition = new Vector3(0f, .95f, -.14f);
            rail.transform.localScale = new Vector3(8f, .08f, .06f);
            UnityEngine.Object.DestroyImmediate(rail.GetComponent<Collider>());
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            SetField(authoring, "surfaceId", "wall.production");
            SetField(authoring, "columns", 8);
            SetField(authoring, "rows", 2);
            SetField(authoring, "slotSize", 1f);
            view.Configure(projectionRoot.transform, CreateMaterial(), CreateMaterial());

            var previewPosition = new WallSlotPosition(2, 0);

            view.ShowWallPreview(
                CreateWallPreview(
                    "wall.production",
                    previewPosition,
                    definition.Footprint,
                    true),
                authoring,
                true,
                PlacementFeedbackKey.None,
                definition.Prefab);

            var ghost = view.CurrentGhost;
            Assert.That(ghost, Is.Not.Null, expectedDefinitionId);
            Assert.That(ghost.name, Does.StartWith(definition.Prefab.name), expectedDefinitionId);
            var ghostLocalPosition = wall.transform.InverseTransformPoint(ghost.transform.position);
            Assert.That(ghostLocalPosition.z, Is.EqualTo(-.091f).Within(.0001f),
                expectedDefinitionId + " ghost must sit 1 mm outside the Base Wall Surface instead of floating at the decorative rail depth.");
            var projectionLocalPosition = wall.transform.InverseTransformPoint(
                view.CurrentProjection.transform.position);
            Assert.That(ghostLocalPosition.x,
                Is.EqualTo(projectionLocalPosition.x).Within(.0001f),
                expectedDefinitionId + " root must stay horizontally aligned with its footprint.");
            Assert.That(ghostLocalPosition.y,
                Is.EqualTo(projectionLocalPosition.y
                    - definition.Footprint.Height * authoring.SlotSize * .5f).Within(.0001f),
                expectedDefinitionId + " root must use the footprint lower edge, not its centre.");
            Assert.That(Quaternion.Angle(
                ghost.transform.rotation,
                wall.transform.rotation * Quaternion.Euler(0f, 180f, 0f)),
                Is.LessThan(0.01f),
                expectedDefinitionId + " must use the target Wall rotation plus one half turn.");
            Assert.That(Vector3.Dot(ghost.transform.up, Vector3.up), Is.GreaterThan(0.99f),
                expectedDefinitionId + " must remain upright instead of lying on the Floor.");
            Assert.That(Mathf.Abs(Vector3.Dot(
                    ghost.transform.forward,
                    wall.transform.forward)),
                Is.GreaterThan(0.99f),
                expectedDefinitionId + " must remain parallel/opposed to the target Wall normal.");

            var ghostRenderers = ghost.GetComponentsInChildren<Renderer>(true);
            Assert.That(ghostRenderers.Any(item => item.enabled && item.gameObject.activeInHierarchy),
                Is.True, expectedDefinitionId + " must retain visible production renderers.");
            var ghostMaterials = ghostRenderers.SelectMany(item => item.sharedMaterials)
                .Where(item => item != null).Distinct().ToArray();
            var ghostMeshes = ghost.GetComponentsInChildren<MeshFilter>(true)
                .Select(item => item.sharedMesh).Where(item => item != null).Distinct().ToArray();
            Assert.That(sourceMaterials.All(ghostMaterials.Contains), Is.True,
                expectedDefinitionId + " ghost must retain production Materials.");
            Assert.That(sourceMeshes.All(ghostMeshes.Contains), Is.True,
                expectedDefinitionId + " ghost must retain production meshes rather than a fallback.");

            var renderedBounds = CombineBounds(ghostRenderers);
            Assert.That(renderedBounds.size.y, Is.GreaterThan(0.15f),
                expectedDefinitionId + " rendered height must not collapse onto the Floor plane.");
            var confirmedRepresentation = UnityEngine.Object.Instantiate(definition.Prefab);
            owned.Add(confirmedRepresentation);
            confirmedRepresentation.transform.SetParent(wall.transform, false);
            confirmedRepresentation.transform.localPosition = new Vector3(
                (previewPosition.Column + definition.Footprint.Width * 0.5f) * authoring.SlotSize
                    - authoring.Columns * authoring.SlotSize * 0.5f,
                previewPosition.Row * authoring.SlotSize,
                0f);
            confirmedRepresentation.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            confirmedRepresentation.transform.localScale = Vector3.one;
            var confirmedBounds = CombineBounds(
                confirmedRepresentation.GetComponentsInChildren<Renderer>(true));
            Assert.That(Mathf.Abs(Vector3.Dot(
                    ghost.transform.position - confirmedRepresentation.transform.position,
                    wall.transform.up)),
                Is.LessThan(0.0001f),
                expectedDefinitionId + " Preview and Confirm roots must have no vertical jump.");
            Assert.That(Mathf.Abs(Vector3.Dot(
                    renderedBounds.center - confirmedBounds.center,
                    wall.transform.up)),
                Is.LessThan(0.0001f),
                expectedDefinitionId + " rendered Preview and Confirm placement must match vertically.");
            Assert.That(Quaternion.Angle(
                    ghost.transform.rotation,
                    confirmedRepresentation.transform.rotation),
                Is.LessThan(0.01f));
            Assert.That(ghost.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(ghost.GetComponentsInChildren<NavMeshObstacle>(true), Is.Empty);
            Assert.That(ghost.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(definition.Prefab.GetComponentsInChildren<Collider>(true), Is.Not.Empty,
                "Preview stripping must not mutate the prefab asset.");
        }

        [Test]
        public void WallMountedPreviewView_StripsEveryInteractionBodyFromCloneOnly()
        {
            var view = (WallMountedPreviewView)AddRequiredComponent(
                "AnimalCafe.Decoration.WallMountedPreviewView");
            var root = CreateObject("SafetyProjectionRoot");
            var wall = CreatePrimitive("SafetyWall", PrimitiveType.Cube);
            wall.transform.localScale = new Vector3(8f, 2f, 0.1f);
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            SetField(authoring, "surfaceId", "wall.safety");
            SetField(authoring, "columns", 8);
            SetField(authoring, "rows", 2);
            SetField(authoring, "slotSize", 1f);
            var previewSource = CreatePrimitive("SafetyPreviewSource", PrimitiveType.Cube);
            var sourceMaterial = previewSource.GetComponent<Renderer>().sharedMaterial;
            previewSource.AddComponent<Rigidbody>();
            previewSource.AddComponent<NavMeshObstacle>();
            view.Configure(root.transform, CreateMaterial(), CreateMaterial());

            view.ShowWallPreview(
                CreateWallPreview("wall.safety", new WallSlotPosition(1, 0),
                    new WallFootprint(1, 1), true),
                authoring,
                true,
                PlacementFeedbackKey.None,
                previewSource);

            Assert.That(view.CurrentGhost.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(view.CurrentGhost.GetComponentsInChildren<NavMeshObstacle>(true), Is.Empty);
            Assert.That(view.CurrentGhost.GetComponentsInChildren<Rigidbody>(true), Is.Empty,
                "A preview clone must never participate in physics.");
            Assert.That(view.CurrentGhost.GetComponentInChildren<Renderer>().sharedMaterial,
                Is.SameAs(sourceMaterial));
            Assert.That(previewSource.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(1));
            Assert.That(previewSource.GetComponentsInChildren<NavMeshObstacle>(true), Has.Length.EqualTo(1));
            Assert.That(previewSource.GetComponentsInChildren<Rigidbody>(true), Has.Length.EqualTo(1),
                "Safety stripping must not mutate the source prefab/instance.");
        }

        [UnityTest]
        public IEnumerator WallOcclusionFadeView_FadesOnlyRayBlockersAndRestoresOriginalPropertyBlocks()
        {
            var cameraObject = CreateObject("FadeCamera", typeof(UnityEngine.Camera));
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var target = CreatePrimitive("FadeTarget", PrimitiveType.Cube);
            target.transform.position = Vector3.zero;
            var blocker = CreatePrimitive("FadeBlocker", PrimitiveType.Cube);
            blocker.transform.position = new Vector3(0f, 0f, -5f);
            var untouched = CreatePrimitive("Untouched", PrimitiveType.Cube);
            untouched.transform.position = new Vector3(5f, 0f, -5f);
            var source = CreateMaterial();
            target.GetComponent<Renderer>().sharedMaterial = source;
            blocker.GetComponent<Renderer>().sharedMaterial = source;
            untouched.GetComponent<Renderer>().sharedMaterial = source;
            SetRendererColor(blocker.GetComponent<Renderer>(), new Color(0.2f, 0.3f, 0.4f, 0.8f));
            SetRendererColor(untouched.GetComponent<Renderer>(), new Color(0.7f, 0.4f, 0.2f, 0.9f));
            var blockerOriginal = ReadRendererColor(blocker.GetComponent<Renderer>());
            var untouchedOriginal = ReadRendererColor(untouched.GetComponent<Renderer>());
            var targetOriginal = ReadRendererColor(target.GetComponent<Renderer>());

            var view = AddRequiredComponent("AnimalCafe.Decoration.WallOcclusionFadeView");
            ConfigureFadeView((WallOcclusionFadeView)view, cameraObject.GetComponent<UnityEngine.Camera>(), target.GetComponent<Renderer>(), 0.35f);
            Invoke(view, "FadeBlockersForTarget");
            yield return null;

            Assert.That(ReadRendererColor(target.GetComponent<Renderer>()), Is.EqualTo(targetOriginal));
            Assert.That(ReadRendererColor(blocker.GetComponent<Renderer>()), Is.EqualTo(blockerOriginal),
                "Fade must preserve the source appearance MPB; the fade shader owns opacity separately.");
            var fadeBlock = new MaterialPropertyBlock();
            blocker.GetComponent<Renderer>().GetPropertyBlock(fadeBlock);
            Assert.That(fadeBlock.GetFloat("_FadeOpacity"), Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(ReadRendererColor(untouched.GetComponent<Renderer>()).a, Is.EqualTo(0.9f).Within(0.0001f));

            Invoke(view, "RestoreAllFades");
            Assert.That(ReadRendererColor(blocker.GetComponent<Renderer>()),
                Is.EqualTo(blockerOriginal));
            Assert.That(ReadRendererColor(untouched.GetComponent<Renderer>()),
                Is.EqualTo(untouchedOriginal));
        }

        [UnityTest]
        public IEnumerator WallOcclusionFadeView_FadesAllChildRenderersWithRecoverableOverrideWithoutTouchingTarget()
        {
            var camera = CreateObject("FormalFadeCamera", typeof(UnityEngine.Camera));
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var targetRoot = CreatePrimitive("FormalTargetRoot", PrimitiveType.Cube);
            targetRoot.transform.position = Vector3.zero;
            var target = targetRoot.GetComponent<Renderer>();
            var targetTexture = CreateTexture(Color.blue);
            SetRendererColor(target, new Color(0.1f, 0.2f, 0.3f, 0.8f));
            var targetBlock = new MaterialPropertyBlock();
            target.GetPropertyBlock(targetBlock);
            targetBlock.SetTexture("_BaseMap", targetTexture);
            targetBlock.SetVector("_Sentinel", new Vector4(2f, 3f, 5f, 7f));
            target.SetPropertyBlock(targetBlock);
            var targetOriginal = new MaterialPropertyBlock();
            target.GetPropertyBlock(targetOriginal);
            var blockerRoot = CreateObject("FormalBlockerRoot", typeof(BoxCollider));
            blockerRoot.transform.position = new Vector3(0f, 0f, -5f);
            var childA = CreatePrimitive("BlockerChildA", PrimitiveType.Cube);
            childA.transform.SetParent(blockerRoot.transform, false);
            var childB = CreatePrimitive("BlockerChildB", PrimitiveType.Cube);
            childB.transform.SetParent(blockerRoot.transform, false);
            childB.transform.localPosition = Vector3.up;
            var sourceA = CreateMaterial();
            var sourceB = CreateMaterial();
            childA.GetComponent<Renderer>().sharedMaterial = sourceA;
            childB.GetComponent<Renderer>().sharedMaterial = sourceB;
            var view = AddRequiredComponent("AnimalCafe.Decoration.WallOcclusionFadeView");

            ConfigureFadeView((WallOcclusionFadeView)view, camera.GetComponent<UnityEngine.Camera>(), target, 0.35f);
            Invoke(view, "FadeBlockersForTarget");
            yield return null;

            foreach (var renderer in blockerRoot.GetComponentsInChildren<Renderer>())
            {
                Assert.That(renderer.sharedMaterial.shader.name,
                    Is.EqualTo("AnimalCafe/Phase7/OcclusionFadeDither"));
                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);
                Assert.That(block.GetFloat("_FadeOpacity"), Is.EqualTo(0.35f));
            }
            var afterTarget = new MaterialPropertyBlock();
            target.GetPropertyBlock(afterTarget);
            Assert.That(afterTarget.GetTexture("_BaseMap"), Is.SameAs(targetTexture));
            Assert.That(afterTarget.GetVector("_Sentinel"), Is.EqualTo(new Vector4(2f, 3f, 5f, 7f)));

            Invoke(view, "RestoreAllFades");
            Assert.That(childA.GetComponent<Renderer>().sharedMaterial, Is.SameAs(sourceA));
            Assert.That(childB.GetComponent<Renderer>().sharedMaterial, Is.SameAs(sourceB));
        }

        [UnityTest]
        public IEnumerator WallOcclusionFadeView_RestoresAllChildMaterialsAndArbitraryPropertyBlocksForExplicitDisableAndTargetSwitch()
        {
            var camera = CreateObject("CleanupFadeCamera", typeof(UnityEngine.Camera));
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var target = CreatePrimitive("UntouchedFadeTarget", PrimitiveType.Cube);
            target.transform.position = Vector3.zero;
            var targetRenderer = target.GetComponent<Renderer>();
            var targetTexture = CreateTexture(Color.green);
            ApplySentinels(targetRenderer, targetTexture, 17f, new Vector4(2f, 3f, 5f, 7f), 0.23f);
            var blockerRoot = CreateObject("CleanupBlockerRoot", typeof(BoxCollider));
            blockerRoot.transform.position = new Vector3(0f, 0f, -5f);
            var childA = CreatePrimitive("CleanupBlockerA", PrimitiveType.Cube).GetComponent<Renderer>();
            childA.transform.SetParent(blockerRoot.transform, false);
            var childB = CreatePrimitive("CleanupBlockerB", PrimitiveType.Cube).GetComponent<Renderer>();
            childB.transform.SetParent(blockerRoot.transform, false);
            childB.transform.localPosition = Vector3.up;
            var sourceA0 = CreateMaterial();
            var sourceA1 = CreateMaterial();
            var sourceB = CreateMaterial();
            childA.sharedMaterials = new[] { sourceA0, sourceA1 };
            childB.sharedMaterial = sourceB;
            var childATexture = CreateTexture(Color.magenta);
            var childBTexture = CreateTexture(Color.cyan);
            ApplySentinels(childA, childATexture, 19f, new Vector4(11f, 13f, 17f, 19f), 0.81f);
            ApplySentinels(childB, childBTexture, 23f, new Vector4(23f, 29f, 31f, 37f), 0.73f);
            var view = (WallOcclusionFadeView)AddRequiredComponent("AnimalCafe.Decoration.WallOcclusionFadeView");

            ConfigureFadeView(view, camera.GetComponent<UnityEngine.Camera>(), targetRenderer, 0.35f);
            view.FadeBlockersForTarget();
            yield return null;
            AssertFadeOverride(childA);
            AssertFadeOverride(childB);
            AssertSentinels(targetRenderer, targetTexture, 17f, new Vector4(2f, 3f, 5f, 7f), 0.23f);

            view.RestoreAllFades();
            AssertRendererState(childA, new[] { sourceA0, sourceA1 }, childATexture, 19f,
                new Vector4(11f, 13f, 17f, 19f), 0.81f);
            AssertRendererState(childB, new[] { sourceB }, childBTexture, 23f,
                new Vector4(23f, 29f, 31f, 37f), 0.73f);

            view.FadeBlockersForTarget();
            view.enabled = false;
            yield return null;
            AssertRendererState(childA, new[] { sourceA0, sourceA1 }, childATexture, 19f,
                new Vector4(11f, 13f, 17f, 19f), 0.81f);

            view.enabled = true;
            ConfigureFadeView(view, camera.GetComponent<UnityEngine.Camera>(), targetRenderer, 0.35f);
            view.FadeBlockersForTarget();
            var replacementTarget = CreatePrimitive("ReplacementFadeTarget", PrimitiveType.Cube).GetComponent<Renderer>();
            replacementTarget.transform.position = Vector3.right * 3f;
            ConfigureFadeView(view, camera.GetComponent<UnityEngine.Camera>(), replacementTarget, 0.35f);
            AssertRendererState(childA, new[] { sourceA0, sourceA1 }, childATexture, 19f,
                new Vector4(11f, 13f, 17f, 19f), 0.81f);
            AssertRendererState(childB, new[] { sourceB }, childBTexture, 23f,
                new Vector4(23f, 29f, 31f, 37f), 0.73f);
        }

        [UnityTest]
        public IEnumerator WallOcclusionFadeView_RestoresFadesOnDestroyAndFaultAfterTargetIsDestroyed()
        {
            var camera = CreateObject("FaultFadeCamera", typeof(UnityEngine.Camera));
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var target = CreatePrimitive("FaultTarget", PrimitiveType.Cube);
            var blocker = CreatePrimitive("FaultBlocker", PrimitiveType.Cube);
            blocker.transform.position = new Vector3(0f, 0f, -5f);
            var blockerRenderer = blocker.GetComponent<Renderer>();
            var source = CreateMaterial();
            var texture = CreateTexture(Color.yellow);
            blockerRenderer.sharedMaterial = source;
            ApplySentinels(blockerRenderer, texture, 41f, new Vector4(41f, 43f, 47f, 53f), 0.67f);
            var view = (WallOcclusionFadeView)AddRequiredComponent("AnimalCafe.Decoration.WallOcclusionFadeView");

            ConfigureFadeView(view, camera.GetComponent<UnityEngine.Camera>(), target.GetComponent<Renderer>(), 0.35f);
            view.FadeBlockersForTarget();
            UnityEngine.Object.DestroyImmediate(view.gameObject);
            yield return null;
            AssertRendererState(blockerRenderer, new[] { source }, texture, 41f,
                new Vector4(41f, 43f, 47f, 53f), 0.67f);

            var faultView = (WallOcclusionFadeView)AddRequiredComponent("AnimalCafe.Decoration.WallOcclusionFadeView");
            var faultTarget = CreatePrimitive("DestroyedFaultTarget", PrimitiveType.Cube);
            ConfigureFadeView(faultView, camera.GetComponent<UnityEngine.Camera>(), faultTarget.GetComponent<Renderer>(), 0.35f);
            faultView.FadeBlockersForTarget();
            UnityEngine.Object.DestroyImmediate(faultTarget);
            Assert.Throws<InvalidOperationException>(() => faultView.FadeBlockersForTarget());
            AssertRendererState(blockerRenderer, new[] { source }, texture, 41f,
                new Vector4(41f, 43f, 47f, 53f), 0.67f);
        }

        private void AssertRequiredComponent(string fullName)
        {
            Assert.That(ResolveRequiredType(fullName), Is.Not.Null,
                $"Missing Task 7 runtime component '{fullName}'.");
        }

        private MonoBehaviour AddRequiredComponent(string fullName)
        {
            var root = CreateObject(fullName + "_Fixture");
            return (MonoBehaviour)root.AddComponent(ResolveRequiredType(fullName));
        }

        private Type ResolveRequiredType(string fullName)
        {
            var type = typeof(WallSurfaceAuthoring).Assembly.GetType(fullName);
            Assert.That(type, Is.Not.Null, $"Missing Task 7 runtime component '{fullName}'.");
            return type;
        }

        private GameObject CreatePrimitive(string name, PrimitiveType type)
        {
            var result = GameObject.CreatePrimitive(type);
            result.name = name;
            owned.Add(result);
            return result;
        }

        private GameObject CreateObject(string name, params Type[] types)
        {
            var result = new GameObject(name, types);
            owned.Add(result);
            return result;
        }

        private static T LoadEditorAsset<T>(string path) where T : UnityEngine.Object
        {
            var assetDatabase = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            Assert.That(assetDatabase, Is.Not.Null,
                "Production asset matrix requires the Editor PlayMode runner.");
            var load = assetDatabase.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "LoadAssetAtPath"
                    && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 1);
            return (T)load.MakeGenericMethod(typeof(T)).Invoke(null, new object[] { path });
        }

        private static string GetEditorAssetPath(UnityEngine.Object asset)
        {
            var assetDatabase = Type.GetType("UnityEditor.AssetDatabase, UnityEditor");
            Assert.That(assetDatabase, Is.Not.Null,
                "Production asset matrix requires the Editor PlayMode runner.");
            var getPath = assetDatabase.GetMethod(
                "GetAssetPath",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(UnityEngine.Object) },
                null);
            Assert.That(getPath, Is.Not.Null);
            return (string)getPath.Invoke(null, new object[] { asset });
        }

        private static Bounds CombineBounds(IReadOnlyList<Renderer> renderers)
        {
            Assert.That(renderers, Is.Not.Empty);
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Count; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }

            return bounds;
        }

        private Material CreateMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            owned.Add(material);
            return material;
        }

        private Texture2D CreateTexture(Color color)
        {
            var texture = new Texture2D(2, 2);
            texture.SetPixels(new[] { color, color, color, color });
            texture.Apply();
            owned.Add(texture);
            return texture;
        }

        private Texture2D CreateHorizontalStripeTexture()
        {
            var texture = new Texture2D(2, 2)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point
            };
            texture.SetPixels(new[] { Color.red, Color.blue, Color.red, Color.blue });
            texture.Apply();
            owned.Add(texture);
            return texture;
        }

        private Mesh CreateTwoSubmeshPanel()
        {
            var mesh = new Mesh { name = "TwoSubmeshPanel" };
            mesh.vertices = new[]
            {
                new Vector3(-2f, -1.5f, 0f), new Vector3(0f, -1.5f, 0f),
                new Vector3(0f, 1.5f, 0f), new Vector3(-2f, 1.5f, 0f),
                new Vector3(0f, -1.5f, 0f), new Vector3(2f, -1.5f, 0f),
                new Vector3(2f, 1.5f, 0f), new Vector3(0f, 1.5f, 0f)
            };
            mesh.uv = new[]
            {
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up,
                Vector2.zero, Vector2.right, Vector2.one, Vector2.up
            };
            mesh.subMeshCount = 2;
            // Clockwise from the camera-facing -Z side.
            mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.SetTriangles(new[] { 4, 6, 5, 4, 7, 6 }, 1);
            mesh.RecalculateBounds();
            owned.Add(mesh);
            return mesh;
        }

        private Material CreateScreenshotMaterial(Color color)
        {
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null, "The isolated screenshot fixture requires an unlit shader.");
            var material = new Material(shader) { color = color };
            material.mainTexture = Texture2D.whiteTexture;
            owned.Add(material);
            return material;
        }

        private Material CreateProjectionMaterial(Color color)
        {
            // Sprites/Default is a deterministic, texture-backed unlit fixture
            // for the RenderTexture contract; production receives its injected
            // valid/invalid materials unchanged.
            var shader = Shader.Find("Sprites/Default") ??
                Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader) { color = color };
            material.mainTexture = Texture2D.whiteTexture;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            owned.Add(material);
            return material;
        }

        private void ConfigureFadeView(
            WallOcclusionFadeView view,
            UnityEngine.Camera camera,
            Renderer target,
            float opacity)
        {
            var method = typeof(WallOcclusionFadeView).GetMethod("Configure", new[]
            {
                typeof(UnityEngine.Camera), typeof(Renderer), typeof(float), typeof(Material)
            });
            Assert.That(method, Is.Not.Null, "Fade requires injected Material Configure overload.");
            method.Invoke(view, new object[] { camera, target, opacity, CreateFadeTemplateMaterial() });
        }

        private Material CreateFadeTemplateMaterial()
        {
            var shader = Shader.Find("AnimalCafe/Phase7/OcclusionFadeDither");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader);
            owned.Add(material);
            return material;
        }

        private Texture2D CaptureCameraPixels(UnityEngine.Camera camera)
        {
            var target = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32);
            owned.Add(target);
            var pixels = new Texture2D(640, 360, TextureFormat.RGBA32, false);
            owned.Add(pixels);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0f, 0f, 640f, 360f), 0, 0);
            pixels.Apply();
            RenderTexture.active = null;
            camera.targetTexture = null;
            return pixels;
        }

        private static int CountDominantPixels(Texture2D pixels, bool green)
        {
            var count = 0;
            foreach (var color in pixels.GetPixels())
            {
                if (green && color.g > color.r * 1.5f && color.g > color.b * 1.2f && color.g > 0.3f)
                {
                    count++;
                }
                else if (!green && color.r > color.g * 1.5f && color.r > color.b * 1.2f && color.r > 0.3f)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountHighContrastPixels(Texture2D pixels)
        {
            var count = 0;
            foreach (var color in pixels.GetPixels())
            {
                if (color.r > 0.8f && color.g > 0.8f && color.b > 0.8f)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountDominantBluePixels(Texture2D pixels)
        {
            var count = 0;
            foreach (var color in pixels.GetPixels())
            {
                if (color.b > color.r * 1.5f && color.b > color.g * 1.5f && color.b > 0.15f)
                {
                    count++;
                }
            }

            return count;
        }


        private SurfaceStyleDefinitionAsset CreateStyle(
            string styleId,
            SurfaceStyleKind kind,
            Material material)
        {
            var definition = ScriptableObject.CreateInstance<SurfaceStyleDefinitionAsset>();
            SetField(definition, "styleId", styleId);
            SetField(definition, "displayName", styleId);
            SetField(definition, "kind", kind);
            SetField(definition, "material", material);
            owned.Add(definition);
            return definition;
        }

        private SurfaceStyleDefinitionAsset CreateSessionStyle(
            string styleId,
            SurfaceStyleKind kind,
            Material material,
            Sprite thumbnail,
            bool isNone = false)
        {
            var definition = CreateStyle(styleId, kind, material);
            SetField(definition, "thumbnail", thumbnail);
            SetField(definition, "isNoneOption", isNone);
            return definition;
        }

        private static RoomSurfaceLayout CreateSurfaceLayout(
            string wallBaseStyleId = "wallpaper.cream",
            string floorStyleId = "floor.wood")
        {
            var walls = new[]
            {
                new WallAppearance("wall.back-left", wallBaseStyleId, "wainscot.white"),
                new WallAppearance("wall.back-right", "paint.sage", null)
            };
            var floors = new List<FloorTileAppearance>();
            for (var x = 0; x < 8; x++)
            {
                for (var y = 0; y < 8; y++)
                {
                    floors.Add(new FloorTileAppearance(
                        new GridPosition(x, y),
                        floorStyleId,
                        x == 7 && y == 7 ? SurfaceRotation.Degrees270 : SurfaceRotation.Degrees0));
                }
            }

            return new RoomSurfaceLayout("room.main", walls, floors);
        }

        private static SurfacePreviewTransaction CreatePreview(RoomSurfaceLayout layout)
        {
            var constructor = typeof(SurfacePreviewTransaction).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(SurfaceEditScope), typeof(string), typeof(GridPosition?), typeof(string),
                    typeof(SurfaceRotation), typeof(bool), typeof(bool),
                    typeof(string), typeof(string), typeof(string), typeof(string),
                    typeof(string), typeof(string),
                    typeof(RoomSurfaceSnapshot)
                },
                null);
            Assert.That(constructor, Is.Not.Null);
            return (SurfacePreviewTransaction)constructor.Invoke(new object[]
            {
                SurfaceEditScope.Wall, "wall.back-left", null, "preview.style",
                SurfaceRotation.Degrees0, false, true,
                "confirmed.style", "preview.style",
                "confirmed.base", "preview.base",
                "wains.none", "preview.wains",
                layout.CaptureSnapshot()
            });
        }

        private static Texture ReadRendererTexture(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetTexture("_BaseMap");
        }

        private static Texture ReadActiveFloorTileTexture(Transform floorRoot, GridPosition position)
        {
            var expectedName = $"FloorSurfaceTile_{position.X}_{position.Y}";
            var tile = Array.Find(
                floorRoot.GetComponentsInChildren<Renderer>(true),
                renderer => renderer.gameObject.activeInHierarchy && renderer.name == expectedName);
            Assert.That(tile, Is.Not.Null, $"Active Floor tile '{expectedName}' was not rendered.");
            return ReadRendererTexture(tile);
        }

        private static WallMountedPlacementPreview CreateWallPreview(
            string surfaceId,
            WallSlotPosition position,
            WallFootprint footprint,
            bool valid)
        {
            var constructor = typeof(WallMountedPlacementPreview).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[]
                {
                    typeof(string), typeof(string), typeof(string), typeof(WallSlotPosition),
                    typeof(WallFootprint), typeof(WallPlacementResult), typeof(bool)
                },
                null);
            Assert.That(constructor, Is.Not.Null);
            return (WallMountedPlacementPreview)constructor.Invoke(new object[]
            {
                "wall.decor.fixture", null, surfaceId, position, footprint,
                valid ? WallPlacementResult.Success() : WallPlacementResult.Failure(WallPlacementFailureReason.Overlap),
                false
            });
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            var method = Array.Find(target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public),
                candidate => candidate.Name == methodName && candidate.GetParameters().Length == arguments.Length);
            Assert.That(method, Is.Not.Null, $"Missing required public API '{methodName}'.");
            return method.Invoke(target, arguments);
        }

        private static object GetProperty(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, $"Missing required public property '{propertyName}'.");
            return property.GetValue(target);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Fixture field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }

        private static object GetField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Fixture field '{fieldName}' was not found.");
            return field.GetValue(target);
        }

        private static void SetRendererColor(Renderer renderer, Color color)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        private static Color ReadRendererColor(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }

        private static void ApplySentinels(
            Renderer renderer,
            Texture texture,
            float number,
            Vector4 vector,
            float alpha)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetTexture("_SentinelTexture", texture);
            block.SetFloat("_SentinelFloat", number);
            block.SetVector("_SentinelVector", vector);
            block.SetColor("_BaseColor", new Color(0.2f, 0.4f, 0.6f, alpha));
            renderer.SetPropertyBlock(block);
        }

        private static void AssertFadeOverride(Renderer renderer)
        {
            Assert.That(renderer.sharedMaterial.shader.name,
                Is.EqualTo("AnimalCafe/Phase7/OcclusionFadeDither"));
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetFloat("_FadeOpacity"), Is.EqualTo(0.35f).Within(0.0001f));
        }

        private static void AssertRendererState(
            Renderer renderer,
            Material[] expectedMaterials,
            Texture expectedTexture,
            float expectedNumber,
            Vector4 expectedVector,
            float expectedAlpha)
        {
            Assert.That(renderer.sharedMaterials, Is.EqualTo(expectedMaterials));
            AssertSentinels(renderer, expectedTexture, expectedNumber, expectedVector, expectedAlpha);
        }

        private static void AssertSentinels(
            Renderer renderer,
            Texture expectedTexture,
            float expectedNumber,
            Vector4 expectedVector,
            float expectedAlpha)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Assert.That(block.GetTexture("_SentinelTexture"), Is.SameAs(expectedTexture));
            Assert.That(block.GetFloat("_SentinelFloat"), Is.EqualTo(expectedNumber));
            Assert.That(block.GetVector("_SentinelVector"), Is.EqualTo(expectedVector));
            Assert.That(block.GetColor("_BaseColor").a, Is.EqualTo(expectedAlpha).Within(0.0001f));
        }

        private static void AssertColorApproximately(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f));
        }
    }
}

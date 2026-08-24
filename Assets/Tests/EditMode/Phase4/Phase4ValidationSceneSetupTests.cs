using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AnimalCafe.Content;
using AnimalCafe.EditorTools.Phase4;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.Phase4
{
    public sealed class Phase4ValidationSceneSetupTests
    {
        private const string MainCafePath = "Assets/Scenes/MainCafe.unity";
        private const string ScenePath =
            "Assets/Scenes/Validation/Phase4CoreArchitecture.unity";
        private const string EnvironmentRoot = "Assets/Art/Phase4/Environment";

        [Test]
        public void ConfigureScene_CreatesExactEnvironmentContract()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();

            var floor = Find(scene, "P4_Floor_8x8");
            var backLeft = Find(scene, "P4_Wall_BackLeft");
            var backRight = Find(scene, "P4_Wall_BackRight");
            var entrance = Find(scene, "P4_Entrance");

            Assert.That(floor, Is.Not.Null);
            Assert.That(backLeft.GetComponent<WallSurfaceAuthoring>().Columns,
                Is.EqualTo(8));
            Assert.That(backRight.GetComponent<WallSurfaceAuthoring>().Rows,
                Is.EqualTo(2));
            Assert.That(
                entrance.GetComponent<EntrancePortalAuthoring>()
                    .CreateReservation().Size,
                Is.EqualTo(new GridSize(2, 2)));
        }

        [Test]
        public void ConfigureScene_CreatesPaletteBGeometryWithoutRaycastObstructions()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var floorVisual = Find(scene, "FloorVisual");
            var gridOverlay = Find(scene, "GridOverlay");
            var walls = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WallSurfaceAuthoring>(true))
                .ToArray();

            Assert.That(floorVisual, Is.Not.Null);
            var floorRenderer = floorVisual.GetComponent<Renderer>();
            Assert.That(floorRenderer, Is.Not.Null);
            Assert.That(floorRenderer.bounds.size.x, Is.EqualTo(8f).Within(0.001f));
            Assert.That(floorRenderer.bounds.size.z, Is.EqualTo(8f).Within(0.001f));
            Assert.That(floorRenderer.bounds.max.y, Is.EqualTo(0f).Within(0.001f));
            AssertColor(
                floorRenderer.sharedMaterial.GetColor("_BaseColor"),
                new Color32(0xF8, 0xE9, 0xA8, 0xFF));

            Assert.That(gridOverlay, Is.Not.Null);
            Assert.That(gridOverlay.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(
                gridOverlay.GetComponentsInChildren<Renderer>(true)
                    .Select(renderer => renderer.bounds.min.y),
                Is.All.GreaterThan(floorRenderer.bounds.max.y));

            Assert.That(walls, Has.Length.EqualTo(2));
            Assert.That(walls.Select(wall => wall.SurfaceId),
                Is.EquivalentTo(new[] { "wall.back-left", "wall.back-right" }));
            Assert.That(walls.Select(wall => wall.Columns), Is.All.EqualTo(8));
            Assert.That(walls.Select(wall => wall.Rows), Is.All.EqualTo(2));
            Assert.That(walls.Select(wall => wall.SlotSize), Is.All.EqualTo(1f));
            Assert.That(walls.Select(wall => wall.transform.position.y),
                Is.All.EqualTo(0.5f).Within(0.001f));
            Assert.That(walls.Select(wall =>
                    wall.transform.Find("WallVisual").GetComponent<Renderer>().bounds.size.y),
                Is.All.EqualTo(3f).Within(0.001f));

            var backLeftMaterial = Find(scene, "P4_Wall_BackLeft")
                .transform.Find("WallVisual").GetComponent<Renderer>().sharedMaterial;
            var backRightMaterial = Find(scene, "P4_Wall_BackRight")
                .transform.Find("WallVisual").GetComponent<Renderer>().sharedMaterial;
            AssertColor(backLeftMaterial.GetColor("_BaseColor"),
                new Color32(0xD2, 0xA6, 0x42, 0xFF));
            AssertColor(backRightMaterial.GetColor("_BaseColor"),
                new Color32(0xC7, 0x95, 0x2E, 0xFF));

            Physics.SyncTransforms();
            Assert.That(Physics.Raycast(
                new Vector3(0f, 5f, 0f),
                Vector3.down,
                out var hit,
                10f), Is.True);
            Assert.That(hit.collider.gameObject.name, Is.EqualTo("FloorVisual"));
        }

        [Test]
        public void ConfigureScene_CreatesWindowEntranceAndCameraContract()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var backRight = Find(scene, "P4_Wall_BackRight");
            var window = Find(scene, "P4_Window_BackRight_C3_R0");
            var entrance = Find(scene, "P4_Entrance");
            var portal = entrance.GetComponent<EntrancePortalAuthoring>();
            var cameras = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<UnityEngine.Camera>(true))
                .ToArray();

            Assert.That(window, Is.Not.Null);
            Assert.That(window.transform.parent, Is.EqualTo(backRight.transform));
            Assert.That(window.transform.localPosition.x, Is.EqualTo(-0.5f).Within(0.001f));
            Assert.That(window.transform.localPosition.y, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(window.transform.localPosition.z, Is.LessThan(-0.05f));

            Assert.That(portal.EntranceId, Is.EqualTo("entrance.main"));
            Assert.That(portal.Origin, Is.EqualTo(new GridPosition(3, 0)));
            Assert.That(entrance.transform.position.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(entrance.transform.position.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(entrance.transform.position.z, Is.EqualTo(-4f).Within(0.001f));
            Assert.That(entrance.GetComponentsInChildren<Collider>(true), Is.Empty,
                "Entrance light/line and clearance overlay must remain walkable.");
            Assert.That(Find(scene, "EntranceClearance_2x2"), Is.Not.Null);

            Assert.That(cameras, Has.Length.EqualTo(1));
            Assert.That(cameras[0].gameObject.name, Is.EqualTo("Main Camera"));
            Assert.That(cameras[0].orthographic, Is.True);
            Assert.That(cameras[0].transform.eulerAngles.x,
                Is.EqualTo(35.264f).Within(0.01f));
            Assert.That(cameras[0].transform.eulerAngles.y,
                Is.EqualTo(45f).Within(0.01f));
        }

        [Test]
        public void ConfigureScene_CreatesExactFixtureGroupsAndDisablesInvalidExamples()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var fixtureRoot = Find(scene, "P4_FixtureGroup");
            var counters = Find(scene, "AdjacentCounters_3");
            var longCounter = Find(scene, "LongCounter_1x3");
            var coffee = Find(scene, "CoffeeMachine_Rotations");
            var cash = Find(scene, "CashRegister_Rotations");
            var validWalls = Find(scene, "WallItems_Valid");
            var invalidWalls = Find(scene, "WallItems_Invalid");
            var showcase = Find(scene, "ProductionAssetShowcase");

            Assert.That(fixtureRoot, Is.Not.Null);
            Assert.That(fixtureRoot.transform.childCount, Is.EqualTo(7));
            Assert.That(counters.transform.childCount, Is.EqualTo(3));
            Assert.That(longCounter.transform.childCount, Is.EqualTo(1));
            AssertQuarterTurns(coffee.transform);
            AssertQuarterTurns(cash.transform);
            Assert.That(validWalls.transform.Cast<Transform>().Select(child => child.name),
                Is.EquivalentTo(new[]
                {
                    "WallItem_Valid_1x1",
                    "WallItem_Valid_1x2",
                    "WallItem_Valid_2x1"
                }));
            Assert.That(invalidWalls.activeSelf, Is.False);
            Assert.That(invalidWalls.transform.Cast<Transform>().Select(child => child.name),
                Does.Contain("WallItem_Invalid_Overlap"));
            Assert.That(invalidWalls.transform.Cast<Transform>().Select(child => child.name),
                Does.Contain("WallItem_Invalid_CornerCrossing"));
            Assert.That(showcase, Is.Not.Null);
            Assert.That(showcase.transform.Find("WorkTable"), Is.Not.Null);
            Assert.That(showcase.transform.Find("WorkTable/CeramicCup"), Is.Not.Null);
        }

        [Test]
        public void ConfigureScene_PlacesCeramicCupOnWorkTableSurfaceSlot()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var workTable = Find(scene, "ProductionAssetShowcase")
                .transform.Find("WorkTable").gameObject;
            var ceramicCup = workTable.transform.Find("CeramicCup").gameObject;
            var surfaceSlots = workTable.GetComponentsInChildren<SurfaceSlotMarker>(true);
            var expectedWorkTablePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase4ProductionAssetBuilder.WorkTablePrefabPath);
            var expectedCeramicCupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase4ProductionAssetBuilder.CeramicCupPrefabPath);

            Assert.That(ceramicCup.transform.parent, Is.EqualTo(workTable.transform));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(workTable),
                Is.EqualTo(expectedWorkTablePrefab));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(ceramicCup),
                Is.EqualTo(expectedCeramicCupPrefab));
            Assert.That(surfaceSlots, Has.Length.EqualTo(1),
                "WorkTable review fixture must provide one unambiguous SurfaceSlotMarker.");

            Physics.SyncTransforms();
            var surfacePlaneY = surfaceSlots[0].transform.position.y;
            var colliderBottomY = ceramicCup.GetComponentsInChildren<Collider>(true)
                .Min(collider => collider.bounds.min.y);
            var rendererBottomY = ceramicCup.GetComponentsInChildren<Renderer>(true)
                .Min(renderer => renderer.bounds.min.y);

            Assert.That(colliderBottomY, Is.EqualTo(surfacePlaneY).Within(0.001f));
            Assert.That(rendererBottomY, Is.EqualTo(surfacePlaneY).Within(0.001f));
        }

        [Test]
        public void ValidateOpenScene_AcceptsCeramicCupWhoseColliderAndRendererMeetSurfaceSlot()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();

            Assert.DoesNotThrow(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        [Test]
        public void ConfigureScene_QuarterTurnPresentationCountersDoNotOverlap()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var coffeeSupport = Find(scene, "P4_Coffee_Rotation_270")
                .transform.Find("SupportCounter").GetComponentInChildren<Renderer>().bounds;
            var cashSupport = Find(scene, "P4_CashRegister_Rotation_0")
                .transform.Find("SupportCounter").GetComponentInChildren<Renderer>().bounds;

            var overlapX = Mathf.Min(coffeeSupport.max.x, cashSupport.max.x) -
                Mathf.Max(coffeeSupport.min.x, cashSupport.min.x);
            var overlapY = Mathf.Min(coffeeSupport.max.y, cashSupport.max.y) -
                Mathf.Max(coffeeSupport.min.y, cashSupport.min.y);
            var overlapZ = Mathf.Min(coffeeSupport.max.z, cashSupport.max.z) -
                Mathf.Max(coffeeSupport.min.z, cashSupport.min.z);

            Assert.That(overlapX > 0.001f && overlapY > 0.001f && overlapZ > 0.001f,
                Is.False,
                "Coffee rotation 270 and Cash Register rotation 0 support Counters must " +
                "touch at most at the seam, never intersect with positive volume.");
            Assert.That(cashSupport.min.x - coffeeSupport.max.x,
                Is.EqualTo(0f).Within(0.001f),
                "The two support Counters should be tightly adjacent at one clean seam.");
        }

        [Test]
        public void ValidateOpenScene_RejectsMissingWorkTableReviewFixture()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            UnityEngine.Object.DestroyImmediate(Find(scene, "WorkTable"));

            Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        [Test]
        public void ValidateOpenScene_RejectsMissingCeramicCupReviewFixture()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            UnityEngine.Object.DestroyImmediate(Find(scene, "CeramicCup"));

            Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        [Test]
        public void ValidateOpenScene_RejectsHoveringCeramicCupWithoutMutatingProductionPrefabs()
        {
            var workTableHashBefore = ComputeSha256(
                Phase4ProductionAssetBuilder.WorkTablePrefabPath);
            var ceramicCupHashBefore = ComputeSha256(
                Phase4ProductionAssetBuilder.CeramicCupPrefabPath);
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var ceramicCup = Find(scene, "ProductionAssetShowcase")
                .transform.Find("WorkTable/CeramicCup");
            ceramicCup.position += Vector3.up * 0.25f;

            var exception = Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));

            Assert.That(exception.Message, Does.Contain("CeramicCup"));
            Assert.That(exception.Message, Does.Contain("SurfaceSlotMarker"));
            Assert.That(exception.Message, Does.Contain("markerY="));
            Assert.That(exception.Message, Does.Contain("colliderBottomY="));
            Assert.That(exception.Message, Does.Contain("rendererBottomY="));
            Assert.That(exception.Message, Does.Contain("colliderDeltaY="));
            Assert.That(exception.Message, Does.Contain("rendererDeltaY="));
            Assert.That(ComputeSha256(Phase4ProductionAssetBuilder.WorkTablePrefabPath),
                Is.EqualTo(workTableHashBefore));
            Assert.That(ComputeSha256(Phase4ProductionAssetBuilder.CeramicCupPrefabPath),
                Is.EqualTo(ceramicCupHashBefore));
        }

        [Test]
        public void BeginnerGuide_TimeControlsAndFormalPhase4Phase6FixturesUseMainCafe()
        {
            var guidePath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Docs/Phase4_Beginner_Guide.md");
            var guide = File.ReadAllText(guidePath);

            Assert.That(guide, Does.Contain("M76–M78 必须在 MainCafe 完成"));
            Assert.That(guide, Does.Contain("P4_Environment"));
            Assert.That(guide, Does.Contain("Phase6_DecorationRuntime"));
            Assert.That(guide, Does.Contain("DecorationModeButton"));
            Assert.That(guide, Does.Contain("初始 Counter"));
            Assert.That(guide, Does.Contain("旧的 temporary review cubes 已由 Phase 6 migration 移除"));
            Assert.That(guide, Does.Not.Contain("Create > 3D Object > Cube"));
            Assert.That(guide, Does.Contain("返回 Phase4CoreArchitecture"));
        }

        [Test]
        public void ConfigureScene_TwiceIsStableAndIsolated()
        {
            var mainCafeHashBefore = ComputeSha256(MainCafePath);
            var buildSettingsBefore = EditorBuildSettings.scenes
                .Select(entry => $"{entry.path}|{entry.enabled}")
                .ToArray();

            var first = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var firstSnapshot = CaptureHierarchy(first);
            var second = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var secondSnapshot = CaptureHierarchy(second);

            Assert.That(second.GetRootGameObjects().Count(root =>
                root.name == "Phase4ValidationRoot"), Is.EqualTo(1));
            Assert.That(FindAll(second, "P4_Floor_8x8"), Has.Length.EqualTo(1));
            Assert.That(second.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<WallSurfaceAuthoring>(true))
                    .Count(),
                Is.EqualTo(2));
            Assert.That(FindAll(second, "P4_Window_BackRight_C3_R0"), Has.Length.EqualTo(1));
            Assert.That(FindAll(second, "P4_Entrance"), Has.Length.EqualTo(1));
            Assert.That(FindAll(second, "Main Camera"), Has.Length.EqualTo(1));
            Assert.That(FindAll(second, "P4_FixtureGroup"), Has.Length.EqualTo(1));
            Assert.That(secondSnapshot, Is.EqualTo(firstSnapshot));
            Assert.That(ComputeSha256(MainCafePath), Is.EqualTo(mainCafeHashBefore));
            Assert.That(EditorBuildSettings.scenes
                    .Select(entry => $"{entry.path}|{entry.enabled}"),
                Is.EqualTo(buildSettingsBefore));
        }

        [Test]
        public void ConfigureScene_CreatesStableEnvironmentAssetsUnderOwnedFolder()
        {
            Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var expectedPaths = new[]
            {
                EnvironmentRoot + "/Materials/M_Environment_Floor_PaletteB.mat",
                EnvironmentRoot + "/Materials/M_Environment_Wall_BackLeft_PaletteB.mat",
                EnvironmentRoot + "/Materials/M_Environment_Wall_BackRight_PaletteB.mat",
                EnvironmentRoot + "/Materials/M_Environment_Window_01.mat",
                EnvironmentRoot + "/Materials/M_Environment_Entrance_01.mat",
                EnvironmentRoot + "/Materials/M_Environment_Grid_01.mat",
                EnvironmentRoot + "/Prefabs/PF_Environment_Floor_8x8.prefab",
                EnvironmentRoot + "/Prefabs/PF_Environment_Wall_BackLeft_8x3.prefab",
                EnvironmentRoot + "/Prefabs/PF_Environment_Wall_BackRight_8x3.prefab",
                EnvironmentRoot + "/Prefabs/PF_Environment_Window_01.prefab",
                EnvironmentRoot + "/Prefabs/PF_Environment_Entrance_2x2.prefab"
            };
            var guidsBefore = expectedPaths.Select(AssetDatabase.AssetPathToGUID).ToArray();

            Assert.That(expectedPaths.Select(path => AssetDatabase.LoadMainAssetAtPath(path)),
                Is.All.Not.Null);
            Assert.That(guidsBefore, Is.All.Not.Empty);

            Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();

            Assert.That(expectedPaths.Select(AssetDatabase.AssetPathToGUID),
                Is.EqualTo(guidsBefore));
        }

        [Test]
        public void BuildScene_ValidationFailureDoesNotOverwriteLastValidatedScene()
        {
            Phase4ValidationSceneSetup.BuildScene();
            var validatedHash = ComputeSha256(ScenePath);

            var saved = Phase4ValidationSceneSetup.TryBuildSceneForTests(
                _ => false);

            Assert.That(saved, Is.False);
            Assert.That(ComputeSha256(ScenePath), Is.EqualTo(validatedHash));
        }

        [Test]
        public void BuildScene_TwiceIsByteStableAndPreservesIsolation()
        {
            var mainCafeHashBefore = ComputeSha256(MainCafePath);
            var buildSettingsBefore = EditorBuildSettings.scenes
                .Select(entry => $"{entry.path}|{entry.enabled}")
                .ToArray();

            Phase4ValidationSceneSetup.BuildScene();
            var sceneHashBefore = ComputeSha256(ScenePath);
            var sceneGuidBefore = AssetDatabase.AssetPathToGUID(ScenePath);
            Phase4ValidationSceneSetup.BuildScene();

            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath),
                Is.Not.Null);
            Assert.That(ComputeSha256(ScenePath), Is.EqualTo(sceneHashBefore));
            Assert.That(AssetDatabase.AssetPathToGUID(ScenePath), Is.EqualTo(sceneGuidBefore));
            Assert.That(ComputeSha256(MainCafePath), Is.EqualTo(mainCafeHashBefore));
            Assert.That(EditorBuildSettings.scenes
                    .Select(entry => $"{entry.path}|{entry.enabled}"),
                Is.EqualTo(buildSettingsBefore));
        }

        [Test]
        public void ValidateOpenScene_RejectsMutatedWallContractBeforeSave()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            Assert.DoesNotThrow(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));

            var wall = Find(scene, "P4_Wall_BackRight")
                .GetComponent<WallSurfaceAuthoring>();
            var serialized = new SerializedObject(wall);
            serialized.FindProperty("rows").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        [Test]
        public void MenuCancelLeavesCurrentSceneAndValidatedFileUnchanged()
        {
            Phase4ValidationSceneSetup.BuildScene();
            var validatedHash = ComputeSha256(ScenePath);
            var currentScene = SceneManager.GetActiveScene();
            var currentScenePath = currentScene.path;
            var currentHierarchy = CaptureHierarchy(currentScene);

            var built = Phase4ValidationSceneSetup.TryBuildSceneFromMenu(
                () => false);

            Assert.That(built, Is.False);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(currentScenePath));
            Assert.That(CaptureHierarchy(SceneManager.GetActiveScene()),
                Is.EqualTo(currentHierarchy));
            Assert.That(ComputeSha256(ScenePath), Is.EqualTo(validatedHash));
        }

        [Test]
        public void ValidateOpenScene_RejectsMovedEntrance()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            Find(scene, "P4_Entrance").transform.position += Vector3.right;

            Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        [Test]
        public void ValidateOpenScene_RejectsMovedWall()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            Find(scene, "P4_Wall_BackLeft").transform.position += Vector3.right;

            Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        [Test]
        public void ValidateOpenScene_RejectsMissingWall()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            UnityEngine.Object.DestroyImmediate(Find(scene, "P4_Wall_BackRight"));

            Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        [Test]
        public void ValidateOpenScene_RejectsWrongWindowPrefab()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var window = Find(scene, "P4_Window_BackRight_C3_R0");
            ReplaceWithPrefab(
                window,
                Phase4ProductionAssetBuilder.WorkTablePrefabPath,
                "P4_Window_BackRight_C3_R0");

            Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        [Test]
        public void ValidateOpenScene_RejectsStalePalette()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var renderer = Find(scene, "FloorVisual").GetComponent<Renderer>();
            var staleMaterial = new Material(renderer.sharedMaterial);
            try
            {
                staleMaterial.SetColor("_BaseColor", Color.red);
                renderer.sharedMaterial = staleMaterial;

                Assert.Throws<InvalidOperationException>(() =>
                    Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(staleMaterial);
            }
        }

        [Test]
        public void ValidateOpenScene_RejectsMissingCoffeeRotationFixture()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var group = Find(scene, "CoffeeMachine_Rotations");
            UnityEngine.Object.DestroyImmediate(group.transform.GetChild(3).gameObject);

            Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        [Test]
        public void ConfigureScene_InvalidWallFixturesShowRealFailureGeometry()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var overlap = Find(scene, "WallItem_Invalid_Overlap");
            var overlapRenderers = overlap.GetComponentsInChildren<Renderer>(true);
            var corner = Find(scene, "WallItem_Invalid_CornerCrossing");
            var noRotation = Find(scene, "WallItem_Invalid_RotationForbidden");

            Assert.That(overlapRenderers, Has.Length.EqualTo(2));
            Assert.That(overlapRenderers[0].bounds.Intersects(overlapRenderers[1].bounds),
                Is.True);
            Assert.That(corner.transform.Cast<Transform>().Select(child => child.name),
                Is.EquivalentTo(new[]
                {
                    "CornerSurface_BackLeft",
                    "CornerSurface_BackRight",
                    "CrossingVisual"
                }));
            Assert.That(noRotation, Is.Not.Null);
            Assert.That(noRotation.transform.localEulerAngles.y,
                Is.EqualTo(45f).Within(0.001f));
        }

        [Test]
        public void ValidateOpenScene_RejectsWrongAdjacentCounterPrefab()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var counter = Find(scene, "P4_Counter_Module_0");
            ReplaceWithPrefab(
                counter,
                Phase4ProductionAssetBuilder.CoffeeMachinePrefabPath,
                "P4_Counter_Module_0");

            Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        [Test]
        public void ValidateOpenScene_RejectsWrongLongCounterPrefab()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var counter = Find(scene, "P4_Counter_1x3");
            ReplaceWithPrefab(
                counter,
                Phase4ProductionAssetBuilder.CounterPrefabPath,
                "P4_Counter_1x3");

            Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        [Test]
        public void ValidateOpenScene_RejectsCashRegisterInCoffeeFixture()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var fixture = Find(scene, "P4_Coffee_Rotation_0");
            var coffee = fixture.transform.Find("Coffee").gameObject;
            ReplaceWithPrefab(
                coffee,
                Phase4ProductionAssetBuilder.CashRegisterPrefabPath,
                "Coffee");

            Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        [Test]
        public void ValidateOpenScene_RejectsCoffeeMachineInCashRegisterFixture()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var fixture = Find(scene, "P4_CashRegister_Rotation_0");
            var cashRegister = fixture.transform.Find("CashRegister").gameObject;
            ReplaceWithPrefab(
                cashRegister,
                Phase4ProductionAssetBuilder.CoffeeMachinePrefabPath,
                "CashRegister");

            Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        [Test]
        public void ValidateOpenScene_RejectsWrongRotationSupportPrefab()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            var fixture = Find(scene, "P4_Coffee_Rotation_90");
            var support = fixture.transform.Find("SupportCounter").gameObject;
            ReplaceWithPrefab(
                support,
                Phase4ProductionAssetBuilder.WorkTablePrefabPath,
                "SupportCounter");

            Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        [Test]
        public void ValidateOpenScene_RejectsMovedCamera()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            Find(scene, "Main Camera").transform.position += Vector3.right;

            Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        [Test]
        public void ValidateOpenScene_RejectsMovedFloor()
        {
            var scene = Phase4ValidationSceneSetup.ConfigureOpenSceneForTests();
            Find(scene, "P4_Floor_8x8").transform.position += Vector3.forward;

            Assert.Throws<InvalidOperationException>(() =>
                Phase4ValidationSceneSetup.ValidateOpenSceneForTests(scene));
        }

        private static GameObject Find(Scene scene, string name)
        {
            return FindAll(scene, name).SingleOrDefault();
        }

        private static GameObject[] FindAll(Scene scene, string name)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(candidate => candidate.name == name)
                .Select(candidate => candidate.gameObject)
                .ToArray();
        }

        private static void AssertQuarterTurns(Transform group)
        {
            Assert.That(group, Is.Not.Null);
            Assert.That(group.childCount, Is.EqualTo(4));
            Assert.That(group.Cast<Transform>()
                    .Select(child => Mathf.RoundToInt(child.localEulerAngles.y)),
                Is.EqualTo(new[] { 0, 90, 180, 270 }));
        }

        private static GameObject ReplaceWithPrefab(
            GameObject original,
            string replacementPrefabPath,
            string replacementName)
        {
            var parent = original.transform.parent;
            var siblingIndex = original.transform.GetSiblingIndex();
            var localPosition = original.transform.localPosition;
            var localRotation = original.transform.localRotation;
            var localScale = original.transform.localScale;
            UnityEngine.Object.DestroyImmediate(original);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(replacementPrefabPath);
            Assert.That(prefab, Is.Not.Null,
                $"Mutation test requires prefab at {replacementPrefabPath}.");
            var replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            replacement.name = replacementName;
            replacement.transform.SetSiblingIndex(siblingIndex);
            replacement.transform.localPosition = localPosition;
            replacement.transform.localRotation = localRotation;
            replacement.transform.localScale = localScale;
            return replacement;
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(Vector4.Distance(actual, expected), Is.LessThan(0.0001f));
        }

        private static string[] CaptureHierarchy(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform =>
                    $"{GetHierarchyPath(transform)}|{transform.gameObject.activeSelf}|" +
                    $"{transform.localPosition}|{transform.localEulerAngles}|" +
                    $"{transform.localScale}|{ReadStableId(transform.gameObject)}")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static string ReadStableId(GameObject gameObject)
        {
            var wall = gameObject.GetComponent<WallSurfaceAuthoring>();
            if (wall != null)
            {
                return wall.SurfaceId;
            }

            var entrance = gameObject.GetComponent<EntrancePortalAuthoring>();
            return entrance != null ? entrance.EntranceId : string.Empty;
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }

            return path;
        }

        private static string ComputeSha256(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var absolutePath = Path.Combine(
                projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            using (var stream = File.OpenRead(absolutePath))
            using (var sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(stream))
                    .Replace("-", string.Empty);
            }
        }
    }
}

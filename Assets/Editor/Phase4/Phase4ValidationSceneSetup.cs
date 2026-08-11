using System;
using System.Globalization;
using System.IO;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace AnimalCafe.EditorTools.Phase4
{
    /// <summary>
    /// Builds the isolated Phase 4 environment fixture without touching gameplay Scenes.
    /// 构建独立的 Phase 4 环境验证 fixture，不修改 gameplay Scene。
    /// </summary>
    public static class Phase4ValidationSceneSetup
    {
        private const string ScenePath =
            "Assets/Scenes/Validation/Phase4CoreArchitecture.unity";
        private const string EnvironmentRoot = "Assets/Art/Phase4/Environment";
        private const string MaterialFolder = EnvironmentRoot + "/Materials";
        private const string PrefabFolder = EnvironmentRoot + "/Prefabs";

        private const string FloorMaterialPath =
            MaterialFolder + "/M_Environment_Floor_PaletteB.mat";
        private const string BackLeftMaterialPath =
            MaterialFolder + "/M_Environment_Wall_BackLeft_PaletteB.mat";
        private const string BackRightMaterialPath =
            MaterialFolder + "/M_Environment_Wall_BackRight_PaletteB.mat";
        private const string WindowMaterialPath =
            MaterialFolder + "/M_Environment_Window_01.mat";
        private const string EntranceMaterialPath =
            MaterialFolder + "/M_Environment_Entrance_01.mat";
        private const string GridMaterialPath =
            MaterialFolder + "/M_Environment_Grid_01.mat";

        private const string FloorPrefabPath =
            PrefabFolder + "/PF_Environment_Floor_8x8.prefab";
        private const string BackLeftPrefabPath =
            PrefabFolder + "/PF_Environment_Wall_BackLeft_8x3.prefab";
        private const string BackRightPrefabPath =
            PrefabFolder + "/PF_Environment_Wall_BackRight_8x3.prefab";
        private const string WindowPrefabPath =
            PrefabFolder + "/PF_Environment_Window_01.prefab";
        private const string EntrancePrefabPath =
            PrefabFolder + "/PF_Environment_Entrance_2x2.prefab";

        public static Scene ConfigureOpenSceneForTests()
        {
            EnsureEnvironmentAssets();

            var scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            scene.name = "Phase4CoreArchitecture";

            var root = new GameObject("Phase4ValidationRoot");
            CreateEnvironment(root.transform);
            CreateCameraRig(root.transform);
            CreateFixtureGroups(root.transform);
            return scene;
        }

        public static void BuildScene()
        {
            EnsureEnvironmentAssets();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                var existingScene = EditorSceneManager.OpenScene(
                    ScenePath,
                    OpenSceneMode.Single);
                try
                {
                    ValidateOpenSceneForTests(existingScene);
                    return;
                }
                catch (InvalidOperationException)
                {
                    // Rebuild a missing or invalid owned Scene below. The new
                    // Scene is still validated before its first save.
                }
            }

            var saved = TryBuildSceneForTests(scene =>
            {
                ValidateOpenSceneForTests(scene);
                return true;
            });
            if (!saved)
            {
                throw new InvalidOperationException(
                    "Phase 4 validation Scene did not pass its pre-save gate.");
            }
        }

        public static void BuildSceneFromMenu()
        {
            TryBuildSceneFromMenu(
                EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo);
        }

        internal static bool TryBuildSceneFromMenu(Func<bool> saveModifiedScenesPrompt)
        {
            if (saveModifiedScenesPrompt == null)
            {
                throw new ArgumentNullException(nameof(saveModifiedScenesPrompt));
            }

            if (!saveModifiedScenesPrompt())
            {
                return false;
            }

            BuildScene();
            return true;
        }

        internal static bool TryBuildSceneForTests(Func<Scene, bool> validationGate)
        {
            if (validationGate == null)
            {
                throw new ArgumentNullException(nameof(validationGate));
            }

            var scene = ConfigureOpenSceneForTests();
            if (!validationGate(scene))
            {
                return false;
            }

            EnsureFolder(Path.GetDirectoryName(ScenePath)?.Replace('\\', '/'));
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException(
                    $"Unity could not save the validated Phase 4 Scene at {ScenePath}.");
            }

            return true;
        }

        internal static void ValidateOpenSceneForTests(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException(
                    "Phase 4 validation requires a valid loaded Scene.");
            }

            var roots = scene.GetRootGameObjects();
            if (roots.Length != 1 || roots[0].name != "Phase4ValidationRoot")
            {
                throw new InvalidOperationException(
                    "Phase 4 Scene must contain exactly one Phase4ValidationRoot.");
            }

            var floor = RequireSingle(scene, "P4_Floor_8x8");
            var floorVisual = RequireChild(floor.transform, "FloorVisual");
            var floorRenderer = RequireComponent<Renderer>(floorVisual);
            var floorCollider = RequireComponent<BoxCollider>(floorVisual);
            RequireTransform(floor.transform, Vector3.zero, Vector3.zero, "Floor");
            RequirePrefabSource(floor, FloorPrefabPath, "Floor");
            RequireAssetIdentity(
                floorRenderer.sharedMaterial,
                FloorMaterialPath,
                "Floor Material");
            RequireApproximately(floorRenderer.bounds.size.x, 8f, "Floor width");
            RequireApproximately(floorRenderer.bounds.size.z, 8f, "Floor depth");
            RequireApproximately(floorRenderer.bounds.max.y, 0f, "Floor top");
            RequireColor(
                floorRenderer.sharedMaterial,
                new Color32(0xF8, 0xE9, 0xA8, 0xFF),
                "Floor Palette B");
            if (floorCollider.isTrigger)
            {
                throw new InvalidOperationException(
                    "The Floor selection Collider cannot be a trigger.");
            }

            var grid = RequireChild(floor.transform, "GridOverlay");
            if (grid.GetComponentsInChildren<Collider>(true).Length != 0)
            {
                throw new InvalidOperationException(
                    "Grid overlay cannot obstruct placement or selection raycasts.");
            }

            foreach (var renderer in grid.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.bounds.min.y <= floorRenderer.bounds.max.y)
                {
                    throw new InvalidOperationException(
                        "Grid overlay must be offset above the Floor to avoid Z-fighting.");
                }
            }

            var walls = roots[0].GetComponentsInChildren<WallSurfaceAuthoring>(true);
            if (walls.Length != 2 ||
                walls.Select(wall => wall.SurfaceId).Distinct(StringComparer.Ordinal).Count() != 2)
            {
                throw new InvalidOperationException(
                    "Phase 4 requires exactly two Walls with unique stable Surface IDs.");
            }

            foreach (var wall in walls)
            {
                if (wall.Columns != 8 || wall.Rows != 2 ||
                    !Mathf.Approximately(wall.SlotSize, 1f) ||
                    !Mathf.Approximately(wall.GizmoDepthOffset, -0.055f) ||
                    !Mathf.Approximately(wall.transform.position.y, 0.5f))
                {
                    throw new InvalidOperationException(
                        $"Wall {wall.name} must use an 8x2 one-metre Slot Grid from y=0.5m.");
                }

                var wallVisual = RequireChild(wall.transform, "WallVisual");
                RequireApproximately(
                    RequireComponent<Renderer>(wallVisual).bounds.size.y,
                    3f,
                    $"Wall {wall.name} physical height");
            }

            var backLeft = RequireSingle(scene, "P4_Wall_BackLeft");
            var backRight = RequireSingle(scene, "P4_Wall_BackRight");
            RequireTransform(
                backLeft.transform,
                new Vector3(0f, 0.5f, 4f),
                Vector3.zero,
                "Back-left Wall");
            RequireTransform(
                backRight.transform,
                new Vector3(4f, 0.5f, 0f),
                new Vector3(0f, 90f, 0f),
                "Back-right Wall");
            var backLeftRenderer = RequireComponent<Renderer>(
                RequireChild(backLeft.transform, "WallVisual"));
            var backRightRenderer = RequireComponent<Renderer>(
                RequireChild(backRight.transform, "WallVisual"));
            RequireApproximately(backLeftRenderer.bounds.size.x, 8f,
                "Back-left Wall width");
            RequireApproximately(backRightRenderer.bounds.size.z, 8f,
                "Back-right Wall width");
            RequireColor(backLeftRenderer.sharedMaterial,
                new Color32(0xD2, 0xA6, 0x42, 0xFF),
                "Back-left Wall Palette B");
            RequireColor(backRightRenderer.sharedMaterial,
                new Color32(0xC7, 0x95, 0x2E, 0xFF),
                "Back-right Wall Palette B");

            var window = RequireSingle(scene, "P4_Window_BackRight_C3_R0");
            RequirePrefabSource(window, WindowPrefabPath, "Window");
            if (window.transform.parent != backRight.transform ||
                !Mathf.Approximately(window.transform.localPosition.x, -0.5f) ||
                !Mathf.Approximately(window.transform.localPosition.y, 0.5f) ||
                window.transform.localPosition.z >= -0.05f)
            {
                throw new InvalidOperationException(
                    "The default Window must occupy Back-right lower-center Slot C3/R0 " +
                    "with a visible no-Z-fighting offset.");
            }

            ValidateWindowOccupancyContract();

            var entrance = RequireSingle(scene, "P4_Entrance");
            var portal = RequireComponent<EntrancePortalAuthoring>(entrance);
            var reservation = portal.CreateReservation();
            if (!string.Equals(portal.EntranceId, "entrance.main", StringComparison.Ordinal) ||
                reservation.Origin != new GridPosition(3, 0) ||
                reservation.Size != new GridSize(2, 2) ||
                entrance.GetComponentsInChildren<Collider>(true).Length != 0)
            {
                throw new InvalidOperationException(
                    "Entrance must keep stable ID entrance.main, exact walkable 2x2 " +
                    "clearance and no blocking Collider.");
            }

            RequireTransform(
                entrance.transform,
                new Vector3(0f, 0f, -4f),
                Vector3.zero,
                "Entrance");
            var entranceLine = RequireSingle(scene, "EntranceLine");
            RequireApproximately(
                RequireComponent<Renderer>(entranceLine).bounds.size.x,
                2f,
                "Entrance line width");
            var clearance = RequireSingle(scene, "EntranceClearance_2x2");
            if (clearance.transform.childCount != 4 ||
                !Mathf.Approximately(clearance.transform.localPosition.z, 1f))
            {
                throw new InvalidOperationException(
                    "Entrance clearance visual must be one exact 2x2 four-line outline.");
            }
            var cameras = roots[0].GetComponentsInChildren<UnityEngine.Camera>(true);
            if (cameras.Length != 1 ||
                cameras[0].gameObject.name != "Main Camera" ||
                !cameras[0].orthographic)
            {
                throw new InvalidOperationException(
                    "Phase 4 Scene requires exactly one orthographic validation Camera.");
            }

            var validationCamera = cameras[0];
            RequireTransform(
                validationCamera.transform,
                new Vector3(-10f, 10f, -10f),
                new Vector3(35.264f, 45f, 0f),
                "Main Camera");
            RequireApproximately(validationCamera.orthographicSize, 10f,
                "Main Camera orthographic size");
            RequireApproximately(validationCamera.nearClipPlane, 0.1f,
                "Main Camera near clip plane");
            RequireApproximately(validationCamera.farClipPlane, 100f,
                "Main Camera far clip plane");
            if (validationCamera.clearFlags != CameraClearFlags.SolidColor ||
                Vector4.Distance(
                    validationCamera.backgroundColor,
                    (Color)new Color32(0xF2, 0xE6, 0xB8, 0xFF)) >= 0.0001f)
            {
                throw new InvalidOperationException(
                    "Main Camera background does not match the fixed validation contract.");
            }

            ValidateFixtureContracts(scene);
            var invalidFixtures = RequireSingle(scene, "WallItems_Invalid");
            if (invalidFixtures.activeSelf)
            {
                throw new InvalidOperationException(
                    "Invalid fixtures must be disabled and excluded from production validation.");
            }

            var productionReport = Phase4AssetValidator.ValidateAll();
            if (productionReport.InvalidAssetCount != 0 ||
                productionReport.Issues.Count != 0)
            {
                throw new InvalidOperationException(
                    "Phase 4 production content must validate before the Scene is saved: " +
                    string.Join(" | ", productionReport.Issues.Select(issue =>
                        $"{issue.Code}:{issue.AssetPath}")));
            }
        }

        private static void ValidateFixtureContracts(Scene scene)
        {
            var root = RequireSingle(scene, "P4_FixtureGroup");
            if (root.transform.childCount != 7)
            {
                throw new InvalidOperationException(
                    "Phase 4 requires exactly seven fixture presentation groups.");
            }

            var adjacent = RequireSingle(scene, "AdjacentCounters_3");
            var adjacentNames = new[]
            {
                "P4_Counter_Module_0",
                "P4_Counter_Module_1",
                "P4_Counter_Module_2"
            };
            RequireExactDirectChildren(adjacent, adjacentNames);
            foreach (var counterName in adjacentNames)
            {
                RequirePrefabSource(
                    RequireChild(adjacent.transform, counterName),
                    Phase4ProductionAssetBuilder.CounterPrefabPath,
                    counterName);
            }

            var longCounter = RequireSingle(scene, "LongCounter_1x3");
            RequireExactDirectChildren(longCounter, new[] { "P4_Counter_1x3" });
            RequirePrefabSource(
                RequireChild(longCounter.transform, "P4_Counter_1x3"),
                Phase4ProductionAssetBuilder.LongCounterFixturePrefabPath,
                "P4_Counter_1x3");
            RequireQuarterTurns(
                scene,
                "CoffeeMachine_Rotations",
                "Coffee",
                Phase4ProductionAssetBuilder.CoffeeMachinePrefabPath);
            RequireQuarterTurns(
                scene,
                "CashRegister_Rotations",
                "CashRegister",
                Phase4ProductionAssetBuilder.CashRegisterPrefabPath);

            var showcase = RequireSingle(scene, "ProductionAssetShowcase");
            RequireExactDirectChildren(showcase, new[] { "WorkTable" });
            var workTable = RequireChild(showcase.transform, "WorkTable");
            RequirePrefabSource(
                workTable,
                Phase4ProductionAssetBuilder.WorkTablePrefabPath,
                "ProductionAssetShowcase/WorkTable");
            var ceramicCup = RequireChild(workTable.transform, "CeramicCup");
            RequirePrefabSource(
                ceramicCup,
                Phase4ProductionAssetBuilder.CeramicCupPrefabPath,
                "ProductionAssetShowcase/WorkTable/CeramicCup");
            RequireCeramicCupOnWorkTableSurface(workTable, ceramicCup);

            var valid = RequireSingle(scene, "WallItems_Valid");
            var validNames = valid.transform.Cast<Transform>()
                .Select(child => child.name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            var expectedValid = new[]
            {
                "WallItem_Valid_1x1",
                "WallItem_Valid_1x2",
                "WallItem_Valid_2x1"
            }.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            if (!validNames.SequenceEqual(expectedValid))
            {
                throw new InvalidOperationException(
                    "Valid Wall fixtures must contain exact 1x1, 1x2 and 2x1 examples.");
            }

            var invalid = RequireSingle(scene, "WallItems_Invalid");
            if (invalid.activeSelf || invalid.transform.childCount != 3)
            {
                throw new InvalidOperationException(
                    "Invalid Wall fixtures must contain three disabled examples.");
            }

            var overlap = RequireSingle(scene, "WallItem_Invalid_Overlap");
            var overlapRenderers = overlap.GetComponentsInChildren<Renderer>(true);
            if (overlapRenderers.Length != 2 ||
                !overlapRenderers[0].bounds.Intersects(overlapRenderers[1].bounds))
            {
                throw new InvalidOperationException(
                    "Overlap fixture must contain two actually intersecting Wall items.");
            }

            var corner = RequireSingle(scene, "WallItem_Invalid_CornerCrossing");
            if (corner.transform.childCount != 3 ||
                corner.transform.Find("CornerSurface_BackLeft") == null ||
                corner.transform.Find("CornerSurface_BackRight") == null ||
                corner.transform.Find("CrossingVisual") == null)
            {
                throw new InvalidOperationException(
                    "Corner fixture must show two isolated surfaces and one crossing item.");
            }

            var rotation = RequireSingle(scene, "WallItem_Invalid_RotationForbidden");
            RequireApproximately(rotation.transform.localEulerAngles.y, 45f,
                "Forbidden Wall rotation example");
        }

        private static void RequireExactDirectChildren(
            GameObject parent,
            string[] expectedNames)
        {
            var actualNames = parent.transform.Cast<Transform>()
                .Select(child => child.name)
                .ToArray();
            if (!actualNames.SequenceEqual(expectedNames))
            {
                throw new InvalidOperationException(
                    $"Fixture group {parent.name} must contain exact direct children: " +
                    string.Join(", ", expectedNames) + ".");
            }
        }

        private static void RequireQuarterTurns(
            Scene scene,
            string groupName,
            string equipmentName,
            string equipmentPrefabPath)
        {
            var group = RequireSingle(scene, groupName);
            var wrappers = group.transform.Cast<Transform>().ToArray();
            if (wrappers.Length != 4)
            {
                throw new InvalidOperationException(
                    $"Fixture group {groupName} requires exactly four rotations.");
            }

            for (var index = 0; index < wrappers.Length; index++)
            {
                var rotation = index * 90;
                var wrapper = wrappers[index];
                var expectedWrapperName = $"P4_{equipmentName}_Rotation_{rotation}";
                if (wrapper.name != expectedWrapperName ||
                    Mathf.RoundToInt(wrapper.localEulerAngles.y) != rotation)
                {
                    throw new InvalidOperationException(
                        $"Fixture group {groupName} requires exact named " +
                        "0/90/180/270 rotation wrappers.");
                }

                RequireExactDirectChildren(
                    wrapper.gameObject,
                    new[] { "SupportCounter", equipmentName });
                RequirePrefabSource(
                    RequireChild(wrapper, "SupportCounter"),
                    Phase4ProductionAssetBuilder.CounterPrefabPath,
                    expectedWrapperName + "/SupportCounter");
                RequirePrefabSource(
                    RequireChild(wrapper, equipmentName),
                    equipmentPrefabPath,
                    expectedWrapperName + "/" + equipmentName);
            }
        }

        private static void ValidateWindowOccupancyContract()
        {
            var layout = new WallSurfaceLayout("wall.back-right", 8, 2);
            var window = new WallMountedInstance(
                "window.default",
                "wall.window.01",
                "wall.back-right",
                new WallSlotPosition(3, 0),
                new WallFootprint(1, 1));
            if (!layout.TryPlace(window).Succeeded)
            {
                throw new InvalidOperationException(
                    "The default Window must use normal Wall occupancy rules.");
            }

            var overlap = new WallMountedInstance(
                "window.overlap",
                "wall.window.01",
                "wall.back-right",
                new WallSlotPosition(3, 0),
                new WallFootprint(1, 1));
            if (layout.TryPlace(overlap).FailureReason !=
                WallPlacementFailureReason.Overlap)
            {
                throw new InvalidOperationException(
                    "Window overlap must be rejected by normal Wall occupancy rules.");
            }

            var crossSurface = new WallMountedInstance(
                "window.cross-surface",
                "wall.window.01",
                "wall.back-left",
                new WallSlotPosition(7, 0),
                new WallFootprint(2, 1));
            if (layout.TryPlace(crossSurface).FailureReason !=
                WallPlacementFailureReason.SurfaceMismatch)
            {
                throw new InvalidOperationException(
                    "A Wall item cannot cross the corner into another Surface.");
            }
        }

        private static GameObject RequireSingle(Scene scene, string name)
        {
            var matches = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name == name)
                .Select(transform => transform.gameObject)
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Phase 4 Scene requires exactly one '{name}', found {matches.Length}.");
            }

            return matches[0];
        }

        private static GameObject RequireChild(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                throw new InvalidOperationException(
                    $"{parent.name} requires direct child '{name}'.");
            }

            return child.gameObject;
        }

        private static T RequireComponent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException(
                    $"{gameObject.name} requires {typeof(T).Name}.");
            }

            return component;
        }

        private static void RequireApproximately(float actual, float expected, string label)
        {
            if (Mathf.Abs(actual - expected) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"{label} must be {expected}, found {actual}.");
            }
        }

        private static void RequireColor(Material material, Color expected, string label)
        {
            if (material == null || material.shader == null ||
                material.shader.name != "Universal Render Pipeline/Lit" ||
                Vector4.Distance(material.GetColor("_BaseColor"), expected) >= 0.0001f)
            {
                throw new InvalidOperationException(
                    $"{label} Material does not match the approved URP Palette B contract.");
            }
        }

        private static void RequireTransform(
            Transform transform,
            Vector3 position,
            Vector3 eulerAngles,
            string label)
        {
            if (Vector3.Distance(transform.position, position) > 0.001f ||
                Quaternion.Angle(transform.rotation, Quaternion.Euler(eulerAngles)) > 0.01f ||
                Vector3.Distance(transform.lossyScale, Vector3.one) > 0.001f)
            {
                throw new InvalidOperationException(
                    $"{label} transform does not match the fixed environment contract.");
            }
        }

        private static void RequirePrefabSource(
            GameObject instance,
            string expectedPath,
            string label)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(instance) ||
                PrefabUtility.GetNearestPrefabInstanceRoot(instance) != instance)
            {
                throw new InvalidOperationException(
                    $"{label} must be a direct Prefab instance root.");
            }

            var actualPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
            RequireAssetPathIdentity(actualPath, expectedPath, label + " Prefab");
        }

        private static void RequireCeramicCupOnWorkTableSurface(
            GameObject workTable,
            GameObject ceramicCup)
        {
            if (ceramicCup.transform.parent != workTable.transform)
            {
                throw new InvalidOperationException(
                    "CeramicCup must remain a direct child of WorkTable.");
            }

            var surfaceSlots = workTable.GetComponentsInChildren<SurfaceSlotMarker>(true);
            if (surfaceSlots.Length != 1)
            {
                throw new InvalidOperationException(
                    "WorkTable must provide exactly one SurfaceSlotMarker for CeramicCup.");
            }

            var colliders = ceramicCup.GetComponentsInChildren<Collider>(true);
            var renderers = ceramicCup.GetComponentsInChildren<Renderer>(true);
            if (colliders.Length == 0 || renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    "CeramicCup must provide collider and rendered bounds for SurfaceSlotMarker validation.");
            }

            Physics.SyncTransforms();
            var surfacePlaneY = surfaceSlots[0].transform.position.y;
            var colliderBottomY = colliders.Min(collider => collider.bounds.min.y);
            var rendererBottomY = renderers.Min(renderer => renderer.bounds.min.y);
            var colliderDeltaY = colliderBottomY - surfacePlaneY;
            var rendererDeltaY = rendererBottomY - surfacePlaneY;
            if (Mathf.Abs(colliderDeltaY) > 0.001f ||
                Mathf.Abs(rendererDeltaY) > 0.001f)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "CeramicCup collider and rendered bottoms must meet the WorkTable " +
                        "SurfaceSlotMarker plane within 0.001m. markerY={0:F6}, " +
                        "colliderBottomY={1:F6}, rendererBottomY={2:F6}, " +
                        "colliderDeltaY={3:F6}, rendererDeltaY={4:F6}.",
                        surfacePlaneY,
                        colliderBottomY,
                        rendererBottomY,
                        colliderDeltaY,
                        rendererDeltaY));
            }
        }

        private static void RequireAssetIdentity(
            UnityEngine.Object asset,
            string expectedPath,
            string label)
        {
            if (asset == null)
            {
                throw new InvalidOperationException($"{label} is missing.");
            }

            RequireAssetPathIdentity(
                AssetDatabase.GetAssetPath(asset),
                expectedPath,
                label);
        }

        private static void RequireAssetPathIdentity(
            string actualPath,
            string expectedPath,
            string label)
        {
            var actualGuid = AssetDatabase.AssetPathToGUID(actualPath);
            var expectedGuid = AssetDatabase.AssetPathToGUID(expectedPath);
            if (!string.Equals(actualPath, expectedPath, StringComparison.Ordinal) ||
                string.IsNullOrEmpty(actualGuid) ||
                string.IsNullOrEmpty(expectedGuid) ||
                !string.Equals(actualGuid, expectedGuid, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{label} must use exact asset {expectedPath} with its stable GUID.");
            }
        }

        private static void EnsureEnvironmentAssets()
        {
            EnsureFolder(MaterialFolder);
            EnsureFolder(PrefabFolder);

            var floor = EnsureMaterial(
                FloorMaterialPath,
                new Color32(0xF8, 0xE9, 0xA8, 0xFF));
            var backLeft = EnsureMaterial(
                BackLeftMaterialPath,
                new Color32(0xD2, 0xA6, 0x42, 0xFF));
            var backRight = EnsureMaterial(
                BackRightMaterialPath,
                new Color32(0xC7, 0x95, 0x2E, 0xFF));
            var window = EnsureMaterial(
                WindowMaterialPath,
                new Color32(0xA8, 0xC7, 0xA1, 0xFF));
            var entrance = EnsureMaterial(
                EntranceMaterialPath,
                new Color32(0x79, 0xD2, 0xE6, 0xFF),
                true);
            var grid = EnsureMaterial(
                GridMaterialPath,
                new Color32(0xB8, 0x91, 0x35, 0xFF));

            BuildFloorPrefab(floor, grid);
            BuildWallPrefab(
                BackLeftPrefabPath,
                "PF_Environment_Wall_BackLeft_8x3",
                backLeft);
            BuildWallPrefab(
                BackRightPrefabPath,
                "PF_Environment_Wall_BackRight_8x3",
                backRight);
            BuildWindowPrefab(window);
            BuildEntrancePrefab(entrance);
        }

        private static Material EnsureMaterial(
            string path,
            Color color,
            bool emissive = false)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Phase 4 environment requires the URP Lit Shader.");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.name = Path.GetFileNameWithoutExtension(path);
            material.SetColor("_BaseColor", color);
            material.color = color;
            material.SetFloat("_Smoothness", 0f);
            if (emissive)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.5f);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static void BuildFloorPrefab(Material floor, Material grid)
        {
            BuildPrefab(FloorPrefabPath, "PF_Environment_Floor_8x8", root =>
            {
                var visual = CreateCube(
                    root.transform,
                    "FloorVisual",
                    new Vector3(0f, -0.05f, 0f),
                    new Vector3(8f, 0.1f, 8f),
                    floor,
                    true);
                visual.layer = 0;

                var overlay = CreateChild(root.transform, "GridOverlay");
                const float gridLineY = 0.012f;
                const float lineThickness = 0.015f;
                const float lineHeight = 0.008f;
                for (var index = 0; index <= 8; index++)
                {
                    var offset = -4f + index;
                    CreateCube(
                        overlay.transform,
                        $"GridLine_X_{index}",
                        new Vector3(offset, gridLineY, 0f),
                        new Vector3(lineThickness, lineHeight, 8f),
                        grid,
                        false);
                    CreateCube(
                        overlay.transform,
                        $"GridLine_Z_{index}",
                        new Vector3(0f, gridLineY, offset),
                        new Vector3(8f, lineHeight, lineThickness),
                        grid,
                        false);
                }
            });
        }

        private static void BuildWallPrefab(
            string path,
            string rootName,
            Material material)
        {
            BuildPrefab(path, rootName, root =>
            {
                CreateCube(
                    root.transform,
                    "WallVisual",
                    new Vector3(0f, 1f, 0f),
                    new Vector3(8f, 3f, 0.1f),
                    material,
                    true);
            });
        }

        private static void BuildWindowPrefab(Material material)
        {
            BuildPrefab(WindowPrefabPath, "PF_Environment_Window_01", root =>
            {
                const float frame = 0.08f;
                CreateCube(root.transform, "FrameTop",
                    new Vector3(0f, 0.41f, 0f), new Vector3(0.9f, frame, 0.08f),
                    material, false);
                CreateCube(root.transform, "FrameBottom",
                    new Vector3(0f, -0.41f, 0f), new Vector3(0.9f, frame, 0.08f),
                    material, false);
                CreateCube(root.transform, "FrameLeft",
                    new Vector3(-0.41f, 0f, 0f), new Vector3(frame, 0.9f, 0.08f),
                    material, false);
                CreateCube(root.transform, "FrameRight",
                    new Vector3(0.41f, 0f, 0f), new Vector3(frame, 0.9f, 0.08f),
                    material, false);
                var collider = root.AddComponent<BoxCollider>();
                collider.center = Vector3.zero;
                collider.size = new Vector3(0.9f, 0.9f, 0.08f);
            });
        }

        private static void BuildEntrancePrefab(Material material)
        {
            BuildPrefab(EntrancePrefabPath, "PF_Environment_Entrance_2x2", root =>
            {
                CreateCube(
                    root.transform,
                    "EntranceLine",
                    new Vector3(0f, 0.025f, 0f),
                    new Vector3(2f, 0.025f, 0.06f),
                    material,
                    false);

                var clearance = CreateChild(root.transform, "EntranceClearance_2x2");
                clearance.transform.localPosition = new Vector3(0f, 0f, 1f);
                CreateOutline(clearance.transform, material, 2f, 2f);
            });
        }

        private static void CreateOutline(
            Transform parent,
            Material material,
            float width,
            float depth)
        {
            const float y = 0.018f;
            const float thickness = 0.035f;
            const float height = 0.012f;
            CreateCube(parent, "OutlineFront", new Vector3(0f, y, -depth * 0.5f),
                new Vector3(width, height, thickness), material, false);
            CreateCube(parent, "OutlineBack", new Vector3(0f, y, depth * 0.5f),
                new Vector3(width, height, thickness), material, false);
            CreateCube(parent, "OutlineLeft", new Vector3(-width * 0.5f, y, 0f),
                new Vector3(thickness, height, depth), material, false);
            CreateCube(parent, "OutlineRight", new Vector3(width * 0.5f, y, 0f),
                new Vector3(thickness, height, depth), material, false);
        }

        private static void BuildPrefab(
            string path,
            string rootName,
            Action<GameObject> configure)
        {
            var root = new GameObject(rootName);
            try
            {
                configure(root);
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                {
                    throw new InvalidOperationException($"Could not save Prefab at {path}.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CreateEnvironment(Transform parent)
        {
            var environment = CreateChild(parent, "P4_Environment");
            InstantiatePrefab(
                FloorPrefabPath,
                environment.transform,
                "P4_Floor_8x8",
                Vector3.zero,
                Quaternion.identity);
            var backLeft = InstantiatePrefab(
                BackLeftPrefabPath,
                environment.transform,
                "P4_Wall_BackLeft",
                new Vector3(0f, 0.5f, 4f),
                Quaternion.identity);
            var backRight = InstantiatePrefab(
                BackRightPrefabPath,
                environment.transform,
                "P4_Wall_BackRight",
                new Vector3(4f, 0.5f, 0f),
                Quaternion.Euler(0f, 90f, 0f));
            ConfigureWallAuthoring(backLeft, "wall.back-left");
            ConfigureWallAuthoring(backRight, "wall.back-right");
            InstantiatePrefab(
                WindowPrefabPath,
                backRight.transform,
                "P4_Window_BackRight_C3_R0",
                new Vector3(-0.5f, 0.5f, -0.061f),
                Quaternion.identity);
            var entrance = InstantiatePrefab(
                EntrancePrefabPath,
                environment.transform,
                "P4_Entrance",
                new Vector3(0f, 0f, -4f),
                Quaternion.identity);
            var portal = entrance.AddComponent<EntrancePortalAuthoring>();
            SetString(portal, "entranceId", "entrance.main");
            SetInteger(portal, "originX", 3);
            SetInteger(portal, "originY", 0);
        }

        private static void ConfigureWallAuthoring(GameObject wall, string surfaceId)
        {
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            SetString(authoring, "surfaceId", surfaceId);
            SetInteger(authoring, "columns", 8);
            SetInteger(authoring, "rows", 2);
            SetFloat(authoring, "slotSize", 1f);
            SetFloat(authoring, "gizmoDepthOffset", -0.055f);
        }

        private static void CreateCameraRig(Transform parent)
        {
            var rig = CreateChild(parent, "P4_CameraRig");
            var cameraObject = CreateChild(rig.transform, "Main Camera");
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(-10f, 10f, -10f),
                Quaternion.Euler(35.264f, 45f, 0f));
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 10f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(0xF2, 0xE6, 0xB8, 0xFF);
            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.antialiasing =
                AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
            cameraData.renderPostProcessing = true;

            var lightObject = CreateChild(rig.transform, "P4_DirectionalLight");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.color = Color.white;
        }

        private static void CreateFixtureGroups(Transform parent)
        {
            var fixtureRoot = CreateChild(parent, "P4_FixtureGroup");
            fixtureRoot.transform.localPosition = new Vector3(-12f, 0f, 0f);

            var adjacent = CreateChild(fixtureRoot.transform, "AdjacentCounters_3");
            adjacent.transform.localPosition = new Vector3(-2f, 0f, 0f);
            for (var index = 0; index < 3; index++)
            {
                InstantiatePrefab(
                    Phase4ProductionAssetBuilder.CounterPrefabPath,
                    adjacent.transform,
                    $"P4_Counter_Module_{index}",
                    new Vector3(index, 0f, 0f),
                    Quaternion.identity);
            }

            var longCounter = CreateChild(fixtureRoot.transform, "LongCounter_1x3");
            longCounter.transform.localPosition = new Vector3(3f, 0f, 0f);
            InstantiatePrefab(
                Phase4ProductionAssetBuilder.LongCounterFixturePrefabPath,
                longCounter.transform,
                "P4_Counter_1x3",
                Vector3.zero,
                Quaternion.identity);

            var coffee = CreateChild(fixtureRoot.transform, "CoffeeMachine_Rotations");
            coffee.transform.localPosition = new Vector3(-2f, 0f, 3f);
            CreateRotations(
                coffee.transform,
                "Coffee",
                Phase4ProductionAssetBuilder.CoffeeMachinePrefabPath);

            var cash = CreateChild(fixtureRoot.transform, "CashRegister_Rotations");
            cash.transform.localPosition = new Vector3(3.5f, 0f, 3f);
            CreateRotations(
                cash.transform,
                "CashRegister",
                Phase4ProductionAssetBuilder.CashRegisterPrefabPath);

            var showcase = CreateChild(fixtureRoot.transform, "ProductionAssetShowcase");
            showcase.transform.localPosition = new Vector3(0f, 0f, -3f);
            var workTable = InstantiatePrefab(
                Phase4ProductionAssetBuilder.WorkTablePrefabPath,
                showcase.transform,
                "WorkTable",
                Vector3.zero,
                Quaternion.identity);
            var workTableSurfaceSlot = workTable
                .GetComponentsInChildren<SurfaceSlotMarker>(true)
                .Single();
            InstantiatePrefab(
                Phase4ProductionAssetBuilder.CeramicCupPrefabPath,
                workTable.transform,
                "CeramicCup",
                workTable.transform.InverseTransformPoint(
                    workTableSurfaceSlot.transform.position),
                Quaternion.identity);

            var validWalls = CreateChild(fixtureRoot.transform, "WallItems_Valid");
            validWalls.transform.localPosition = new Vector3(0f, 0f, 6f);
            CreateWallFixture(validWalls.transform, "WallItem_Valid_1x1",
                new Vector2Int(1, 1), new Vector3(-2f, 0.5f, 0f));
            CreateWallFixture(validWalls.transform, "WallItem_Valid_1x2",
                new Vector2Int(1, 2), new Vector3(0f, 1f, 0f));
            CreateWallFixture(validWalls.transform, "WallItem_Valid_2x1",
                new Vector2Int(2, 1), new Vector3(2f, 0.5f, 0f));

            var invalidWalls = CreateChild(fixtureRoot.transform, "WallItems_Invalid");
            invalidWalls.transform.localPosition = new Vector3(0f, 0f, 9f);
            CreateInvalidOverlapFixture(invalidWalls.transform);
            CreateInvalidCornerFixture(invalidWalls.transform);
            CreateInvalidRotationFixture(invalidWalls.transform);
            invalidWalls.SetActive(false);
        }

        private static void CreateInvalidOverlapFixture(Transform parent)
        {
            var fixture = CreateChild(parent, "WallItem_Invalid_Overlap");
            var material = RequireAsset<Material>(WindowMaterialPath);
            CreateCube(fixture.transform, "Occupant_A", Vector3.zero,
                new Vector3(0.9f, 0.9f, 0.08f), material, false);
            CreateCube(fixture.transform, "Occupant_B", new Vector3(0.2f, 0f, 0f),
                new Vector3(0.9f, 0.9f, 0.08f), material, false);
        }

        private static void CreateInvalidCornerFixture(Transform parent)
        {
            var fixture = CreateChild(parent, "WallItem_Invalid_CornerCrossing");
            fixture.transform.localPosition = new Vector3(3.5f, 0f, 0f);
            var material = RequireAsset<Material>(WindowMaterialPath);
            CreateCube(fixture.transform, "CornerSurface_BackLeft",
                new Vector3(-0.5f, 0.5f, 0f), new Vector3(1f, 1f, 0.05f),
                material, false);
            var right = CreateCube(fixture.transform, "CornerSurface_BackRight",
                new Vector3(0f, 0.5f, 0.5f), new Vector3(1f, 1f, 0.05f),
                material, false);
            right.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            CreateCube(fixture.transform, "CrossingVisual",
                new Vector3(-0.05f, 0.5f, 0.05f), new Vector3(0.8f, 0.8f, 0.08f),
                material, false);
        }

        private static void CreateInvalidRotationFixture(Transform parent)
        {
            var fixture = CreateChild(parent, "WallItem_Invalid_RotationForbidden");
            fixture.transform.localPosition = new Vector3(-3f, 0.5f, 0f);
            fixture.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            CreateCube(fixture.transform, "RotatedVisual", Vector3.zero,
                new Vector3(0.9f, 0.9f, 0.08f),
                RequireAsset<Material>(WindowMaterialPath), false);
        }

        private static void CreateRotations(
            Transform parent,
            string stem,
            string devicePrefabPath)
        {
            for (var index = 0; index < 4; index++)
            {
                var rotation = index * 90f;
                var fixture = CreateChild(parent, $"P4_{stem}_Rotation_{index * 90}");
                fixture.transform.localPosition = new Vector3(index * 1.5f, 0f, 0f);
                fixture.transform.localRotation = Quaternion.Euler(0f, rotation, 0f);
                InstantiatePrefab(
                    Phase4ProductionAssetBuilder.CounterPrefabPath,
                    fixture.transform,
                    "SupportCounter",
                    Vector3.zero,
                    Quaternion.identity);
                InstantiatePrefab(
                    devicePrefabPath,
                    fixture.transform,
                    stem,
                    new Vector3(0f, Phase4ProductionAssetBuilder.CounterTargetBounds.y, 0f),
                    Quaternion.identity);
            }
        }

        private static void CreateWallFixture(
            Transform parent,
            string name,
            Vector2Int size,
            Vector3 localPosition)
        {
            var fixture = CreateChild(parent, name);
            fixture.transform.localPosition = localPosition;
            var material = RequireAsset<Material>(WindowMaterialPath);
            CreateCube(
                fixture.transform,
                "Visual",
                Vector3.zero,
                new Vector3(size.x * 0.9f, size.y * 0.9f, 0.08f),
                material,
                false);
        }

        private static GameObject InstantiatePrefab(
            string path,
            Transform parent,
            string name,
            Vector3 localPosition,
            Quaternion localRotation)
        {
            var prefab = RequireAsset<GameObject>(path);
            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException($"Could not instantiate Prefab {path}.");
            }

            instance.name = name;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException($"Expected Phase 4 asset at {path}.");
            }

            return asset;
        }

        private static GameObject CreateCube(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool keepCollider)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            if (!keepCollider)
            {
                UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
            }

            return cube;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void SetString(
            UnityEngine.Object target,
            string propertyName,
            string value)
        {
            SetSerialized(target, propertyName, property => property.stringValue = value);
        }

        private static void SetInteger(
            UnityEngine.Object target,
            string propertyName,
            int value)
        {
            SetSerialized(target, propertyName, property => property.intValue = value);
        }

        private static void SetFloat(
            UnityEngine.Object target,
            string propertyName,
            float value)
        {
            SetSerialized(target, propertyName, property => property.floatValue = value);
        }

        private static void SetSerialized(
            UnityEngine.Object target,
            string propertyName,
            Action<SerializedProperty> assign)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    $"{target.GetType().Name} has no serialized property '{propertyName}'.");
            }

            assign(property);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidOperationException($"Cannot create asset folder {path}.");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

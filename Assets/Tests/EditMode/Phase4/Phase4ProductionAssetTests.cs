using System.IO;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.EditorTools.Phase4;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.Phase4
{
    public sealed class Phase4ProductionAssetTests
    {
        [Test]
        public void ProductionInputs_UseApprovedSourcesAndNotOldPosSource()
        {
            Assert.That(Phase4ProductionAssetBuilder.WorkTableSourcePath.Replace('\\', '/'),
                Does.EndWith("ArtSource/VisualPipeline/Benchmarks/Blender/SM_Benchmark_WorkTable_01.blend"));
            Assert.That(Phase4ProductionAssetBuilder.CashRegisterRawSourcePath.Replace('\\', '/'),
                Does.EndWith("Blender Model Item/vintage computer monitor 3d model.glb"));
            Assert.That(Phase4ProductionAssetBuilder.CashRegisterRawSourcePath,
                Does.Not.Contain("pos terminal"));
            Assert.That(Phase4ProductionAssetBuilder.WorkTableSourceSha256,
                Is.EqualTo("CDA670B6DEAF309225E1636AA3B07EEBECC6D7D8497939027BC05659C156F60A"));
            Assert.That(Phase4ProductionAssetBuilder.CashRegisterRawSourceSha256,
                Is.EqualTo("28859431416BD3D40D0C52D9F56DE9CD577566964094CD945B69C9120253321D"));
        }

        [Test]
        public void ProductionOutputs_UseExactBlendAndFbxNames()
        {
            Assert.That(Phase4ProductionAssetBuilder.CounterBlendPath,
                Is.EqualTo("ArtSource/Phase4/Blender/SM_Furniture_CounterModule_01.blend"));
            Assert.That(Phase4ProductionAssetBuilder.CashRegisterBlendPath,
                Is.EqualTo("ArtSource/Phase4/Blender/SM_Equipment_CashRegister_01.blend"));
            Assert.That(Phase4ProductionAssetBuilder.CounterFbxPath,
                Is.EqualTo("Assets/Art/Phase4/Models/SM_Furniture_CounterModule_01.fbx"));
            Assert.That(Phase4ProductionAssetBuilder.CashRegisterFbxPath,
                Is.EqualTo("Assets/Art/Phase4/Models/SM_Equipment_CashRegister_01.fbx"));
            Assert.That(Phase4ProductionAssetBuilder.AutomationScriptPath,
                Is.EqualTo("ArtSource/Phase4/Tools/BuildPhase4ProductionAssets.py"));
        }

        [Test]
        public void ProductionTargets_UseApprovedUnityAxisBounds()
        {
            Assert.That(Phase4ProductionAssetBuilder.CounterTargetBounds,
                Is.EqualTo(new Vector3(1.00f, 0.72f, 1.00f)));
            Assert.That(Phase4ProductionAssetBuilder.CashRegisterTargetBounds,
                Is.EqualTo(new Vector3(0.43f, 0.45f, 0.26f)));
            Assert.That(Phase4ProductionAssetBuilder.BoundsTolerance, Is.EqualTo(0.03f));
        }

        [Test]
        public void BoundsRule_AcceptsToleranceBoundaryAndRejectsBeyondIt()
        {
            var target = Phase4ProductionAssetBuilder.CashRegisterTargetBounds;

            Assert.That(Phase4ProductionAssetBuilder.IsWithinTargetBounds(
                target + new Vector3(0.03f, -0.03f, 0.03f), target), Is.True);
            Assert.That(Phase4ProductionAssetBuilder.IsWithinTargetBounds(
                target + new Vector3(0.031f, 0f, 0f), target), Is.False);
        }

        [Test]
        public void TextureRule_UsesExactTargetAndMaximumCases()
        {
            Assert.That(Phase4ProductionAssetBuilder.TargetTextureSize, Is.EqualTo(512));
            Assert.That(Phase4ProductionAssetBuilder.MaximumTextureSize, Is.EqualTo(1024));
            Assert.That(Phase4ProductionAssetBuilder.IsTextureSizeAllowed(512, 512), Is.True);
            Assert.That(Phase4ProductionAssetBuilder.IsTextureSizeAllowed(1024, 1024), Is.True);
            Assert.That(Phase4ProductionAssetBuilder.IsTextureSizeAllowed(1025, 1024), Is.False);
            Assert.That(Phase4ProductionAssetBuilder.IsTextureSizeAllowed(1024, 1025), Is.False);
            Assert.That(Phase4ProductionAssetBuilder.IsTextureSizeAllowed(0, 512), Is.False);
        }

        [Test]
        public void CashRegisterTriangleRule_UsesExactMaximum()
        {
            Assert.That(Phase4ProductionAssetBuilder.MaximumCashRegisterTriangles,
                Is.EqualTo(6000));
            Assert.That(Phase4ProductionAssetBuilder.IsCashRegisterTriangleCountAllowed(6000),
                Is.True);
            Assert.That(Phase4ProductionAssetBuilder.IsCashRegisterTriangleCountAllowed(6001),
                Is.False);
            Assert.That(Phase4ProductionAssetBuilder.IsCashRegisterTriangleCountAllowed(0),
                Is.False);
        }

        [Test]
        public void CounterProductionFiles_ExistAtExactBlendAndFbxPaths()
        {
            Assert.That(File.Exists(Path.Combine(
                Phase4ProductionAssetBuilder.ProjectRootPath,
                Phase4ProductionAssetBuilder.CounterBlendPath)), Is.True);
            Assert.That(File.Exists(Path.Combine(
                Phase4ProductionAssetBuilder.ProjectRootPath,
                Phase4ProductionAssetBuilder.CounterFbxPath)), Is.True);
        }

        [Test]
        public void CashRegisterProductionFiles_ExistAtExactBlendFbxAndTexturePaths()
        {
            Assert.That(File.Exists(Path.Combine(
                Phase4ProductionAssetBuilder.ProjectRootPath,
                Phase4ProductionAssetBuilder.CashRegisterBlendPath)), Is.True);
            Assert.That(File.Exists(Path.Combine(
                Phase4ProductionAssetBuilder.ProjectRootPath,
                Phase4ProductionAssetBuilder.CashRegisterFbxPath)), Is.True);
            Assert.That(File.Exists(Path.Combine(
                Phase4ProductionAssetBuilder.ProjectRootPath,
                Phase4ProductionAssetBuilder.CashRegisterTexturePath)), Is.True);
        }

        [Test]
        public void BuildProductionContent_PreservesImportedCounterAndCashRegisterFbxBytes()
        {
            var paths = new[]
            {
                Phase4ProductionAssetBuilder.CounterFbxPath,
                Phase4ProductionAssetBuilder.CashRegisterFbxPath
            };
            var before = paths.Select(path => File.ReadAllBytes(Path.Combine(
                Phase4ProductionAssetBuilder.ProjectRootPath,
                path))).ToArray();

            Phase4ProductionAssetBuilder.BuildProductionContent();

            var after = paths.Select(path => File.ReadAllBytes(Path.Combine(
                Phase4ProductionAssetBuilder.ProjectRootPath,
                path))).ToArray();
            for (var index = 0; index < paths.Length; index++)
            {
                Assert.That(after[index], Is.EqualTo(before[index]),
                    $"BuildProductionContent must not rewrite imported FBX source bytes at {paths[index]}.");
            }
        }

        [Test]
        public void ImportedCounterModel_MeetsBoundsAndRootScaleContracts()
        {
            AssertImportedModel(
                Phase4ProductionAssetBuilder.CounterFbxPath,
                Phase4ProductionAssetBuilder.CounterTargetBounds,
                requireCashTriangleRule: false);
        }

        [Test]
        public void ImportedCashRegisterModel_MeetsBoundsTrianglesAndRootScaleContracts()
        {
            AssertImportedModel(
                Phase4ProductionAssetBuilder.CashRegisterFbxPath,
                Phase4ProductionAssetBuilder.CashRegisterTargetBounds,
                requireCashTriangleRule: true);
        }

        [Test]
        public void CashRegisterProductionTexture_IsExact512Square()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                Phase4ProductionAssetBuilder.CashRegisterTexturePath);

            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(Phase4ProductionAssetBuilder.TargetTextureSize));
            Assert.That(texture.height, Is.EqualTo(Phase4ProductionAssetBuilder.TargetTextureSize));
        }

        [Test]
        public void BuildProductionContent_CreatesExactlyApprovedFurnitureDefinitions()
        {
            Phase4ProductionAssetBuilder.BuildProductionContent();

            var definitions = LoadAssetsInFolder<FurnitureDefinitionAsset>(
                Phase4ProductionAssetBuilder.DefinitionFolderPath);
            var ids = definitions.Select(definition => definition.DefinitionId).ToArray();

            Assert.That(ids, Is.EquivalentTo(new[]
            {
                "furniture.work-table.01",
                "furniture.counter.module.01",
                "equipment.coffee-machine.01",
                "equipment.cash-register.01"
            }));
            Assert.That(ids, Does.Not.Contain("item.ceramic-cup.01"));
        }

        [Test]
        public void BuildProductionContent_AuthorsExactDefinitionAndCatalogueContracts()
        {
            Phase4ProductionAssetBuilder.BuildProductionContent();

            AssertDefinition(
                Phase4ProductionAssetBuilder.WorkTableDefinitionPath,
                "furniture.work-table.01",
                1,
                1,
                PlacementSurfaceType.Floor,
                FurnitureFunctionType.None);
            AssertDefinition(
                Phase4ProductionAssetBuilder.CounterDefinitionPath,
                "furniture.counter.module.01",
                1,
                1,
                PlacementSurfaceType.Floor,
                FurnitureFunctionType.None);
            AssertDefinition(
                Phase4ProductionAssetBuilder.CoffeeMachineDefinitionPath,
                "equipment.coffee-machine.01",
                1,
                1,
                PlacementSurfaceType.FurnitureSurface,
                FurnitureFunctionType.CoffeeMachine);
            AssertDefinition(
                Phase4ProductionAssetBuilder.CashRegisterDefinitionPath,
                "equipment.cash-register.01",
                1,
                1,
                PlacementSurfaceType.FurnitureSurface,
                FurnitureFunctionType.CashRegister);

            var catalogue = AssetDatabase.LoadAssetAtPath<FurnitureContentCatalog>(
                Phase4ProductionAssetBuilder.CataloguePath);
            Assert.That(catalogue, Is.Not.Null);
            var runtimeCatalogue = catalogue.BuildRuntimeCatalog();
            Assert.That(runtimeCatalogue.Definitions.Select(definition => definition.Id),
                Is.EqualTo(new[]
                {
                    "furniture.work-table.01",
                    "furniture.counter.module.01",
                    "equipment.coffee-machine.01",
                    "equipment.cash-register.01"
                }));
            Assert.That(
                catalogue.TryGetPrefab("item.ceramic-cup.01", out _),
                Is.False);
        }

        [Test]
        public void BuildProductionContent_ModelBoundsDoNotRewriteOneByOneFootprints()
        {
            Phase4ProductionAssetBuilder.BuildProductionContent();

            var counterBounds = GetImportedModelBoundsSize(
                Phase4ProductionAssetBuilder.CounterFbxPath);
            var cashRegisterBounds = GetImportedModelBoundsSize(
                Phase4ProductionAssetBuilder.CashRegisterFbxPath);
            var counterDefinition = AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(
                Phase4ProductionAssetBuilder.CounterDefinitionPath);
            var cashRegisterDefinition = AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(
                Phase4ProductionAssetBuilder.CashRegisterDefinitionPath);

            AssertVectorApproximately(
                counterBounds,
                Phase4ProductionAssetBuilder.CounterTargetBounds,
                Phase4ProductionAssetBuilder.BoundsTolerance);
            AssertVectorApproximately(
                cashRegisterBounds,
                Phase4ProductionAssetBuilder.CashRegisterTargetBounds,
                Phase4ProductionAssetBuilder.BoundsTolerance);
            Assert.That(counterBounds, Is.Not.EqualTo(Vector3.one));
            Assert.That(cashRegisterBounds, Is.Not.EqualTo(Vector3.one));
            Assert.That(counterDefinition, Is.Not.Null);
            Assert.That(cashRegisterDefinition, Is.Not.Null);
            Assert.That(counterDefinition.ToRuntimeDefinition().Footprint,
                Is.EqualTo(new GridSize(1, 1)));
            Assert.That(cashRegisterDefinition.ToRuntimeDefinition().Footprint,
                Is.EqualTo(new GridSize(1, 1)));
        }

        [Test]
        public void BuildProductionContent_RegistersCupVisualAndWindowOnlyInTheirApprovedRoles()
        {
            Phase4ProductionAssetBuilder.BuildProductionContent();

            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase4ProductionAssetBuilder.CeramicCupPrefabPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(
                Phase4ProductionAssetBuilder.CeramicCupDefinitionPath), Is.Null);

            var window = AssetDatabase.LoadAssetAtPath<WallMountedDefinitionAsset>(
                Phase4ProductionAssetBuilder.WindowDefinitionPath);
            Assert.That(window, Is.Not.Null);
            Assert.That(window.DefinitionId, Is.EqualTo("wall.window.01"));
            Assert.That(window.DisplayName, Is.EqualTo("Window"));
            Assert.That(window.Footprint, Is.EqualTo(new WallFootprint(1, 1)));
            Assert.That(window.Prefab, Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase4ProductionAssetBuilder.WindowPrefabPath)));
        }

        [Test]
        public void BuildProductionContent_CreatesExactSpatialMarkers()
        {
            Phase4ProductionAssetBuilder.BuildProductionContent();

            var counter = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase4ProductionAssetBuilder.CounterPrefabPath);
            var counterSlots = counter.GetComponentsInChildren<SurfaceSlotMarker>(true);
            Assert.That(counterSlots.Select(slot => slot.SlotId),
                Is.EqualTo(new[] { "slot.0" }));

            var coffee = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase4ProductionAssetBuilder.CoffeeMachinePrefabPath);
            var coffeeForward = coffee.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform.name == "ForwardMarker")
                .ToArray();
            Assert.That(coffeeForward, Has.Length.EqualTo(1));
            Assert.That(coffee.transform.InverseTransformPoint(coffeeForward[0].position).z,
                Is.GreaterThan(0f));
            Assert.That(Vector3.Dot(coffeeForward[0].forward, coffee.transform.forward),
                Is.GreaterThan(0.999f));

            var cash = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase4ProductionAssetBuilder.CashRegisterPrefabPath);
            var cashMarkers = cash.GetComponentsInChildren<CashRegisterSideMarker>(true);
            Assert.That(cashMarkers, Has.Length.EqualTo(2));
            var employee = cashMarkers.Single(marker =>
                marker.SideType == CashRegisterSideType.Employee);
            var customer = cashMarkers.Single(marker =>
                marker.SideType == CashRegisterSideType.Customer);
            Assert.That(employee.LocalDirection, Is.EqualTo(CardinalDirection.North));
            Assert.That(customer.LocalDirection, Is.EqualTo(CardinalDirection.South));
            Assert.That(employee.transform.localPosition.z, Is.GreaterThan(0f));
            Assert.That(customer.transform.localPosition.z, Is.LessThan(0f));
            Assert.That(CashRegisterSideMarker.ReadSidesFrom(cash).EmployeeSide,
                Is.EqualTo(CardinalDirection.North));
        }

        [Test]
        public void BuildProductionContent_CreatesOneValidationOnlyLongCounterFixture()
        {
            Phase4ProductionAssetBuilder.BuildProductionContent();

            var fixture = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase4ProductionAssetBuilder.LongCounterFixturePrefabPath);
            Assert.That(fixture, Is.Not.Null);
            Assert.That(fixture.name, Is.EqualTo("PF_Validation_Counter_1x3_01"));
            Assert.That(fixture.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(fixture.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(fixture.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(fixture.GetComponentsInChildren<SurfaceSlotMarker>(true)
                .Select(marker => marker.SlotId),
                Is.EquivalentTo(new[] { "slot.0", "slot.1", "slot.2" }));
            Assert.That(LoadAssetsInFolder<FurnitureDefinitionAsset>(
                    Phase4ProductionAssetBuilder.DefinitionFolderPath)
                .Select(definition => definition.DefinitionId),
                Does.Not.Contain("furniture.counter.long.01"));
        }

        [Test]
        public void LongCounterSurfaceSlots_TranslationPreservesLocalMarkersAndMovesWorldPositions()
        {
            Phase4ProductionAssetBuilder.BuildProductionContent();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase4ProductionAssetBuilder.LongCounterFixturePrefabPath);
            var instance = Object.Instantiate(prefab);

            try
            {
                var markers = instance.GetComponentsInChildren<SurfaceSlotMarker>(true)
                    .OrderBy(marker => marker.SlotId, System.StringComparer.Ordinal)
                    .ToArray();
                var expectedLocalPositions = CreateExpectedLongCounterSlotPositions();
                var beforeWorldPositions = markers
                    .Select(marker => marker.transform.position)
                    .ToArray();
                AssertSlotLocalPositions(instance.transform, markers, expectedLocalPositions);

                var translation = new Vector3(4f, 0.5f, -2f);
                instance.transform.position += translation;

                AssertSlotLocalPositions(instance.transform, markers, expectedLocalPositions);
                for (var index = 0; index < markers.Length; index++)
                {
                    AssertVectorApproximately(
                        markers[index].transform.position,
                        beforeWorldPositions[index] + translation,
                        0.0001f);
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void LongCounterSurfaceSlots_QuarterTurnPreservesLocalMarkersAndRotatesWorldRelations()
        {
            Phase4ProductionAssetBuilder.BuildProductionContent();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase4ProductionAssetBuilder.LongCounterFixturePrefabPath);
            var instance = Object.Instantiate(prefab);

            try
            {
                var markers = instance.GetComponentsInChildren<SurfaceSlotMarker>(true)
                    .OrderBy(marker => marker.SlotId, System.StringComparer.Ordinal)
                    .ToArray();
                var expectedLocalPositions = CreateExpectedLongCounterSlotPositions();

                instance.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

                AssertSlotLocalPositions(instance.transform, markers, expectedLocalPositions);
                for (var index = 0; index < markers.Length; index++)
                {
                    AssertVectorApproximately(
                        markers[index].transform.position,
                        instance.transform.TransformPoint(expectedLocalPositions[index]),
                        0.0001f);
                }

                AssertVectorApproximately(
                    markers[1].transform.position - markers[0].transform.position,
                    Vector3.right,
                    0.0001f);
                AssertVectorApproximately(
                    markers[2].transform.position - markers[1].transform.position,
                    Vector3.right,
                    0.0001f);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BuildProductionContent_TwicePreservesGuidsAndExactCounts()
        {
            Phase4ProductionAssetBuilder.BuildProductionContent();
            var trackedPaths = Phase4ProductionAssetBuilder.ProductionAssetPaths.ToArray();
            var firstGuids = trackedPaths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            var firstMarkerContracts = CaptureProductionMarkerContracts();
            var firstCatalogueIds = CaptureCatalogueIds();

            Phase4ProductionAssetBuilder.BuildProductionContent();
            var secondGuids = trackedPaths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            var secondMarkerContracts = CaptureProductionMarkerContracts();
            var secondCatalogueIds = CaptureCatalogueIds();

            Assert.That(firstGuids, Has.All.Not.Empty);
            Assert.That(secondGuids, Is.EqualTo(firstGuids));
            Assert.That(firstMarkerContracts, Is.EqualTo(new[]
            {
                $"{Phase4ProductionAssetBuilder.CashRegisterPrefabPath}|CashRegisterSide|Customer|South",
                $"{Phase4ProductionAssetBuilder.CashRegisterPrefabPath}|CashRegisterSide|Employee|North",
                $"{Phase4ProductionAssetBuilder.CoffeeMachinePrefabPath}|ForwardMarker",
                $"{Phase4ProductionAssetBuilder.CounterPrefabPath}|SurfaceSlot|slot.0",
                $"{Phase4ProductionAssetBuilder.LongCounterFixturePrefabPath}|SurfaceSlot|slot.0",
                $"{Phase4ProductionAssetBuilder.LongCounterFixturePrefabPath}|SurfaceSlot|slot.1",
                $"{Phase4ProductionAssetBuilder.LongCounterFixturePrefabPath}|SurfaceSlot|slot.2",
                $"{Phase4ProductionAssetBuilder.WorkTablePrefabPath}|SurfaceSlot|slot.0"
            }.OrderBy(identity => identity, System.StringComparer.Ordinal).ToArray()));
            Assert.That(secondMarkerContracts, Is.EqualTo(firstMarkerContracts));
            Assert.That(firstCatalogueIds, Is.EqualTo(new[]
            {
                "furniture.work-table.01",
                "furniture.counter.module.01",
                "equipment.coffee-machine.01",
                "equipment.cash-register.01"
            }));
            Assert.That(secondCatalogueIds, Is.EqualTo(firstCatalogueIds));
            Assert.That(LoadAssetsInFolder<FurnitureDefinitionAsset>(
                Phase4ProductionAssetBuilder.DefinitionFolderPath), Has.Length.EqualTo(4));
            Assert.That(LoadAssetsInFolder<WallMountedDefinitionAsset>(
                Phase4ProductionAssetBuilder.DefinitionFolderPath), Has.Length.EqualTo(1));
            Assert.That(LoadAssetsInFolder<FurnitureContentCatalog>(
                Phase4ProductionAssetBuilder.CatalogueFolderPath), Has.Length.EqualTo(1));
        }

        [Test]
        public void ProductionContent_AllApprovedDefinitionsPassPhase4Validation()
        {
            Phase4ProductionAssetBuilder.BuildProductionContent();

            var report = Phase4AssetValidator.ValidateAll();

            Assert.That(report.AssetCount, Is.EqualTo(5));
            Assert.That(report.ValidAssetCount, Is.EqualTo(5));
            Assert.That(report.InvalidAssetCount, Is.Zero);
            Assert.That(report.Issues, Is.Empty);
        }

        private static void AssertImportedModel(
            string assetPath,
            Vector3 targetBounds,
            bool requireCashTriangleRule)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Assert.That(model, Is.Not.Null, $"Missing imported Model at {assetPath}.");

            var instance = Object.Instantiate(model);
            try
            {
                Assert.That(instance.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(instance.transform.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(instance.GetComponentsInChildren<UnityEngine.Camera>(true), Is.Empty);
                Assert.That(instance.GetComponentsInChildren<Light>(true), Is.Empty);

                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers, Has.Length.EqualTo(1));
                var bounds = renderers[0].bounds;
                Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.005f));
                Assert.That(bounds.center.x, Is.EqualTo(0f).Within(0.005f));
                Assert.That(bounds.center.z, Is.EqualTo(0f).Within(0.005f));
                Assert.That(
                    Phase4ProductionAssetBuilder.IsWithinTargetBounds(bounds.size, targetBounds),
                    Is.True,
                    $"{assetPath} bounds are {bounds.size}; target is {targetBounds}.");

                var mesh = renderers[0].GetComponent<MeshFilter>().sharedMesh;
                var triangleCount = Enumerable.Range(0, mesh.subMeshCount)
                    .Sum(subMesh => (long)mesh.GetIndexCount(subMesh) / 3L);
                if (requireCashTriangleRule)
                {
                    Assert.That(
                        Phase4ProductionAssetBuilder.IsCashRegisterTriangleCountAllowed(
                            (int)triangleCount),
                        Is.True,
                        $"Cash Register has {triangleCount} triangles.");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static Vector3 GetImportedModelBoundsSize(string assetPath)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Assert.That(model, Is.Not.Null, $"Missing imported Model at {assetPath}.");
            var instance = Object.Instantiate(model);
            try
            {
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers, Has.Length.EqualTo(1));
                return renderers[0].bounds.size;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static Vector3[] CreateExpectedLongCounterSlotPositions()
        {
            return new[]
            {
                new Vector3(0f, 0.72f, -1f),
                new Vector3(0f, 0.72f, 0f),
                new Vector3(0f, 0.72f, 1f)
            };
        }

        private static void AssertSlotLocalPositions(
            Transform root,
            SurfaceSlotMarker[] markers,
            Vector3[] expectedLocalPositions)
        {
            Assert.That(markers, Has.Length.EqualTo(expectedLocalPositions.Length));
            for (var index = 0; index < markers.Length; index++)
            {
                AssertVectorApproximately(
                    root.InverseTransformPoint(markers[index].transform.position),
                    expectedLocalPositions[index],
                    0.0001f);
            }
        }

        private static void AssertVectorApproximately(
            Vector3 actual,
            Vector3 expected,
            float tolerance)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(tolerance));
        }

        private static T[] LoadAssetsInFolder<T>(string folderPath)
            where T : Object
        {
            return AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folderPath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToArray();
        }

        private static string[] CaptureProductionMarkerContracts()
        {
            var prefabPaths = new[]
            {
                Phase4ProductionAssetBuilder.WorkTablePrefabPath,
                Phase4ProductionAssetBuilder.CounterPrefabPath,
                Phase4ProductionAssetBuilder.CoffeeMachinePrefabPath,
                Phase4ProductionAssetBuilder.CashRegisterPrefabPath,
                Phase4ProductionAssetBuilder.CeramicCupPrefabPath,
                Phase4ProductionAssetBuilder.WindowPrefabPath,
                Phase4ProductionAssetBuilder.LongCounterFixturePrefabPath
            };

            return prefabPaths.SelectMany(path =>
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Assert.That(prefab, Is.Not.Null, $"Missing production Prefab at {path}.");

                    var surfaceSlots = prefab.GetComponentsInChildren<SurfaceSlotMarker>(true)
                        .Select(marker => $"{path}|SurfaceSlot|{marker.SlotId}");
                    var forwardMarkers = prefab.GetComponentsInChildren<Transform>(true)
                        .Where(transform => transform.name == "ForwardMarker")
                        .Select(_ => $"{path}|ForwardMarker");
                    var cashSides = prefab.GetComponentsInChildren<CashRegisterSideMarker>(true)
                        .Select(marker =>
                            $"{path}|CashRegisterSide|{marker.SideType}|{marker.LocalDirection}");
                    return surfaceSlots.Concat(forwardMarkers).Concat(cashSides);
                })
                .OrderBy(identity => identity, System.StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] CaptureCatalogueIds()
        {
            var catalogue = AssetDatabase.LoadAssetAtPath<FurnitureContentCatalog>(
                Phase4ProductionAssetBuilder.CataloguePath);
            Assert.That(catalogue, Is.Not.Null);
            return catalogue.BuildRuntimeCatalog().Definitions
                .Select(definition => definition.Id)
                .ToArray();
        }

        private static void AssertDefinition(
            string path,
            string expectedId,
            int expectedWidth,
            int expectedDepth,
            PlacementSurfaceType expectedSurface,
            FurnitureFunctionType expectedFunction)
        {
            var definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(path);
            Assert.That(definition, Is.Not.Null, $"Missing Definition at {path}.");
            Assert.That(definition.DefinitionId, Is.EqualTo(expectedId));
            Assert.That(definition.FootprintWidth, Is.EqualTo(expectedWidth));
            Assert.That(definition.FootprintDepth, Is.EqualTo(expectedDepth));
            Assert.That(definition.AllowedPlacementSurfaces, Is.EqualTo(expectedSurface));
            Assert.That(definition.FunctionType, Is.EqualTo(expectedFunction));
            Assert.That(definition.Prefab, Is.Not.Null);
        }
    }
}

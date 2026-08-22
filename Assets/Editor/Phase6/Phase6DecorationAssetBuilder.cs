using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.Phase4;
using AnimalCafe.EditorTools.Phase5;
using AnimalCafe.Layout;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Decoration;
using AnimalCafe.UI.Foundation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.Phase6
{
    public static class Phase6DecorationAssetBuilder
    {
        public const int ThumbnailSize = 256;

        private static bool UiCandidateValidationFaultForTests;
        private static bool UiPostPublishValidationFaultForTests;
        private static int UiPublishFaultAfterWriteForTests = -1;

        private const string Phase5FontSourcePath =
            "Assets/UI/Phase5/Fonts/NotoSansSC-Regular.otf";
        private const string RequiredDecorationUiCharacters =
            "Furniture Catalogue Store Rotate Cancel Confirm − □ × ✓ " +
            "Store furniture? This removes it from the current layout. " +
            "You can place it again from the catalogue. " +
            "这里已有家具超出可装修区域这个区域尚未解锁这里不能放置家具" +
            "入口区域不能放置家具此处不支持落地家具家具状态已变化，请重新选择 " +
            "Counter Module Counter 1 x 2 Counter 1 x 3 Counter 2 x 3 " +
            "1 × 1 1 × 2 1 × 3 2 × 3 Unavailable Missing definition " +
            "Missing prefab Missing thumbnail 0123456789";
        private const string RequiredLongerDecorationUiCharacters =
            "Counter Module Plus 这里已有家具请重试 " +
            "This removes it from the current layout. You can place it again from the " +
            "catalogue. Keep it safe for your next layout.";
        private const string AllRequiredDecorationUiCharacters =
            RequiredDecorationUiCharacters + RequiredLongerDecorationUiCharacters;

        private const string CounterModelPath =
            "Assets/Art/Phase4/Models/SM_Furniture_CounterModule_01.fbx";
        private const string WorkTableDefinitionPath =
            "Assets/Art/Phase4/Definitions/FD_Furniture_WorkTable_01.asset";
        private const string CoffeeMachineDefinitionPath =
            "Assets/Art/Phase4/Definitions/FD_Equipment_CoffeeMachine_01.asset";
        private const string CashRegisterDefinitionPath =
            "Assets/Art/Phase4/Definitions/FD_Equipment_CashRegister_01.asset";

        [MenuItem("AnimalCafe/Phase 6/Build Decoration Assets")]
        public static void BuildAll()
        {
            EnsureFolder(Phase6DecorationAssetPaths.DefinitionFolderPath);
            EnsureFolder(Phase6DecorationAssetPaths.PrefabFolderPath);
            EnsureFolder(Phase6DecorationAssetPaths.CatalogueFolderPath);
            EnsureFolder(Phase6DecorationAssetPaths.ThumbnailFolderPath);

            BuildCounterPreset(
                Phase6DecorationAssetPaths.Counter1x2PrefabPath,
                "PF_CounterPreset_1x2",
                1,
                2);
            BuildCounterPreset(
                Phase6DecorationAssetPaths.Counter1x3PrefabPath,
                "PF_CounterPreset_1x3",
                1,
                3);
            BuildCounterPreset(
                Phase6DecorationAssetPaths.Counter2x3PrefabPath,
                "PF_CounterPreset_2x3",
                2,
                3);

            var oneByOne = RequireAsset<FurnitureDefinitionAsset>(
                Phase6DecorationAssetPaths.Counter1x1DefinitionPath,
                "Phase 4 Counter Definition");
            var oneByTwo = EnsureDefinition(
                Phase6DecorationAssetPaths.Counter1x2DefinitionPath,
                "counter.preset.1x2",
                "Counter 1 x 2",
                1,
                2,
                Phase6DecorationAssetPaths.Counter1x2PrefabPath);
            var oneByThree = EnsureDefinition(
                Phase6DecorationAssetPaths.Counter1x3DefinitionPath,
                "counter.preset.1x3",
                "Counter 1 x 3",
                1,
                3,
                Phase6DecorationAssetPaths.Counter1x3PrefabPath);
            var twoByThree = EnsureDefinition(
                Phase6DecorationAssetPaths.Counter2x3DefinitionPath,
                "counter.preset.2x3",
                "Counter 2 x 3",
                2,
                3,
                Phase6DecorationAssetPaths.Counter2x3PrefabPath);

            SaveGeneratedAssets(oneByTwo, oneByThree, twoByThree);
            var phase4Definitions = new[]
            {
                RequireAsset<FurnitureDefinitionAsset>(
                    WorkTableDefinitionPath, "Phase 4 Work Table Definition"),
                oneByOne,
                RequireAsset<FurnitureDefinitionAsset>(
                    CoffeeMachineDefinitionPath, "Phase 4 Coffee Machine Definition"),
                RequireAsset<FurnitureDefinitionAsset>(
                    CashRegisterDefinitionPath, "Phase 4 Cash Register Definition")
            };
            var presetDefinitions = new[] { oneByTwo, oneByThree, twoByThree };
            var productionDefinitions = phase4Definitions.Concat(presetDefinitions).ToArray();
            ValidateUniqueDefinitionIds(productionDefinitions);
            foreach (var presetDefinition in presetDefinitions)
            {
                ValidateCounterPresetContract(presetDefinition);
            }
            ValidatePhase4Contracts(productionDefinitions);

            var decorationDefinitions = new[] { oneByOne, oneByTwo, oneByThree, twoByThree };
            var prefabs = decorationDefinitions.Select(definition => definition.Prefab).ToArray();
            using (var thumbnailRenderer = new ThumbnailRenderer())
            {
                for (var index = 0; index < prefabs.Length; index++)
                {
                    thumbnailRenderer.Build(
                        prefabs[index],
                        Phase6DecorationAssetPaths.ThumbnailPaths[index]);
                }
            }

            var thumbnails = Phase6DecorationAssetPaths.ThumbnailPaths
                .Select(path => RequireAsset<Sprite>(path, "Decoration thumbnail Sprite"))
                .ToArray();
            var productionCatalogue = PublishProductionCatalogue(productionDefinitions);
            var decorationCatalogue = PublishDecorationCatalogue(
                decorationDefinitions, thumbnails);
            SaveGeneratedAssets(productionCatalogue, decorationCatalogue);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            BuildDecorationUiAssets();
        }

        public static void ValidateUniqueDefinitionIds(
            IEnumerable<FurnitureDefinitionAsset> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            var firstById = new Dictionary<string, FurnitureDefinitionAsset>(
                StringComparer.Ordinal);
            foreach (var definition in definitions)
            {
                if (definition == null)
                {
                    throw new ArgumentException(
                        "Furniture Definitions must not contain null references.",
                        nameof(definitions));
                }

                definition.ToRuntimeDefinition();
                if (firstById.TryGetValue(definition.DefinitionId, out var first))
                {
                    throw new ArgumentException(
                        $"Duplicate Definition ID '{definition.DefinitionId}' appears at " +
                        $"'{GetAssetIdentity(first)}' and '{GetAssetIdentity(definition)}'.",
                        nameof(definitions));
                }

                firstById.Add(definition.DefinitionId, definition);
            }
        }

        public static void ValidateDecorationCatalogue(DecorationCatalogueAsset catalogue)
        {
            if (catalogue == null)
            {
                throw new ArgumentNullException(nameof(catalogue));
            }

            var firstById = new Dictionary<string, FurnitureDefinitionAsset>(
                StringComparer.Ordinal);
            for (var index = 0; index < catalogue.Entries.Count; index++)
            {
                var entry = catalogue.Entries[index];
                if (entry == null || entry.Definition == null)
                {
                    throw new InvalidOperationException(
                        $"Decoration Catalogue entry at index {index} is missing its Definition reference.");
                }

                var definition = entry.Definition;
                definition.ToRuntimeDefinition();
                if (definition.Prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Decoration Definition '{definition.DefinitionId}' is missing its Prefab reference.");
                }

                if (entry.Thumbnail == null)
                {
                    throw new InvalidOperationException(
                        $"Decoration Definition '{definition.DefinitionId}' is missing its thumbnail reference.");
                }

                if (definition.AllowedPlacementSurfaces != PlacementSurfaceType.Floor)
                {
                    throw new InvalidOperationException(
                        $"Decoration Definition '{definition.DefinitionId}' must use the Floor placement surface.");
                }

                if (firstById.TryGetValue(definition.DefinitionId, out var first))
                {
                    throw new ArgumentException(
                        $"Duplicate Definition ID '{definition.DefinitionId}' is used by " +
                        $"'{first.name}' and '{definition.name}'.",
                        nameof(catalogue));
                }

                firstById.Add(definition.DefinitionId, definition);
            }
        }

        public static void ValidateCounterPresetContract(FurnitureDefinitionAsset definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            var root = definition.Prefab ?? throw new InvalidOperationException(
                $"Counter preset '{definition.DefinitionId}' is missing its Prefab.");
            var slots = root.GetComponentsInChildren<SurfaceSlotMarker>(true);
            var expectedCount = checked(definition.FootprintWidth * definition.FootprintDepth);
            if (slots.Length != expectedCount)
            {
                throw new InvalidOperationException(
                    $"Counter preset '{definition.DefinitionId}' requires {expectedCount} " +
                    $"surface slots; actual count is {slots.Length}.");
            }

            var slotsById = new Dictionary<string, SurfaceSlotMarker>(StringComparer.Ordinal);
            foreach (var slot in slots)
            {
                if (string.IsNullOrWhiteSpace(slot.SlotId) ||
                    !slotsById.TryAdd(slot.SlotId, slot))
                {
                    throw new InvalidOperationException(
                        $"Counter preset '{definition.DefinitionId}' slot IDs must be stable and unique.");
                }
            }

            const float tolerance = 0.0001f;
            const float surfaceY = 0.72f;
            for (var index = 0; index < expectedCount; index++)
            {
                var expectedId = $"slot.{index}";
                if (!slotsById.TryGetValue(expectedId, out var slot))
                {
                    throw new InvalidOperationException(
                        $"Counter preset '{definition.DefinitionId}' slot IDs must be stable and unique; " +
                        $"missing '{expectedId}'.");
                }

                var expectedPosition = new Vector3(
                    index % definition.FootprintWidth -
                        (definition.FootprintWidth - 1) * 0.5f,
                    surfaceY,
                    index / definition.FootprintWidth -
                        (definition.FootprintDepth - 1) * 0.5f);
                if (Vector3.Distance(slot.transform.localPosition, expectedPosition) > tolerance)
                {
                    var yMatches = Mathf.Abs(
                        slot.transform.localPosition.y - surfaceY) <= tolerance;
                    throw new InvalidOperationException(yMatches
                        ? $"Counter preset '{definition.DefinitionId}' slot '{expectedId}' " +
                            $"must match the approved footprint lattice at {expectedPosition}."
                        : $"Counter preset '{definition.DefinitionId}' slot '{expectedId}' " +
                            $"must use approved surface Y {surfaceY}.");
                }

                if (Quaternion.Angle(slot.transform.localRotation, Quaternion.identity) > 0.01f)
                {
                    throw new InvalidOperationException(
                        $"Counter preset '{definition.DefinitionId}' slot '{expectedId}' " +
                        "must use identity local rotation.");
                }
            }

            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                if (collider.enabled && !collider.isTrigger &&
                    collider.bounds.max.y > surfaceY + tolerance)
                {
                    throw new InvalidOperationException(
                        $"Counter preset '{definition.DefinitionId}' collider '{collider.name}' " +
                        $"extends above the surface slot plane at Y {surfaceY}.");
                }
            }
        }

        private static void BuildCounterPreset(
            string prefabPath,
            string rootName,
            int width,
            int depth)
        {
            var model = RequireAsset<GameObject>(CounterModelPath, "Phase 4 Counter Model");
            var sourcePrefab = RequireAsset<GameObject>(
                Phase6DecorationAssetPaths.Counter1x1PrefabPath,
                "Phase 4 Counter Prefab");
            var sourceRenderer = sourcePrefab.GetComponentInChildren<Renderer>(true) ??
                throw new InvalidOperationException(
                    "Phase 4 Counter Prefab must contain a Renderer.");
            var materials = sourceRenderer.sharedMaterials;
            var root = new GameObject(rootName);
            try
            {
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
                var slotIndex = 0;
                for (var z = 0; z < depth; z++)
                for (var x = 0; x < width; x++)
                {
                    var localPosition = new Vector3(
                        x - (width - 1) * 0.5f,
                        0f,
                        z - (depth - 1) * 0.5f);
                    var visual = UnityEngine.Object.Instantiate(model, root.transform);
                    visual.name = $"CounterVisual_{slotIndex}";
                    visual.transform.localPosition = localPosition;
                    visual.transform.localRotation = Quaternion.identity;
                    visual.transform.localScale = Vector3.one;
                    foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.sharedMaterials = materials;
                    }

                    AddSurfaceSlot(root.transform, $"slot.{slotIndex}", localPosition);
                    slotIndex++;
                }

                var bounds = GetVisibleBounds(root);
                var collider = root.AddComponent<BoxCollider>();
                collider.center = bounds.center;
                collider.size = bounds.size;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static FurnitureDefinitionAsset EnsureDefinition(
            string assetPath,
            string definitionId,
            string displayName,
            int width,
            int depth,
            string prefabPath)
        {
            var definition = AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<FurnitureDefinitionAsset>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var serialized = new SerializedObject(definition);
            serialized.FindProperty("definitionId").stringValue = definitionId;
            serialized.FindProperty("displayName").stringValue = displayName;
            serialized.FindProperty("footprintWidth").intValue = width;
            serialized.FindProperty("footprintDepth").intValue = depth;
            serialized.FindProperty("allowedPlacementSurfaces").intValue =
                (int)PlacementSurfaceType.Floor;
            serialized.FindProperty("functionType").intValue = (int)FurnitureFunctionType.None;
            serialized.FindProperty("prefab").objectReferenceValue =
                RequireAsset<GameObject>(prefabPath, "Phase 6 Counter Prefab");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            definition.name = Path.GetFileNameWithoutExtension(assetPath);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void AddSurfaceSlot(
            Transform root,
            string slotId,
            Vector3 horizontalPosition)
        {
            var slotObject = new GameObject(slotId);
            slotObject.transform.SetParent(root, false);
            slotObject.transform.localPosition = new Vector3(
                horizontalPosition.x,
                Phase4ProductionAssetBuilder.CounterTargetBounds.y,
                horizontalPosition.z);
            slotObject.transform.localRotation = Quaternion.identity;
            slotObject.transform.localScale = Vector3.one;
            var marker = slotObject.AddComponent<SurfaceSlotMarker>();
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("slotId").stringValue = slotId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class ThumbnailRenderer : IDisposable
        {
            private readonly Scene previewScene;
            private readonly RenderTexture renderTexture;
            private readonly Texture2D texture;
            private readonly GameObject cameraObject;
            private readonly GameObject lightObject;
            private readonly UnityEngine.Camera camera;

            public ThumbnailRenderer()
            {
                previewScene = EditorSceneManager.NewPreviewScene();
                renderTexture = new RenderTexture(
                    ThumbnailSize,
                    ThumbnailSize,
                    24,
                    RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear,
                    name = "Phase6DecorationThumbnailTarget"
                };
                texture = new Texture2D(
                    ThumbnailSize,
                    ThumbnailSize,
                    TextureFormat.RGBA32,
                    false,
                    false);
                cameraObject = new GameObject("Phase6ThumbnailCamera")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                SceneManager.MoveGameObjectToScene(cameraObject, previewScene);
                camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.allowHDR = false;
                camera.allowMSAA = false;
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 100f;
                camera.targetTexture = renderTexture;
                camera.overrideSceneCullingMask =
                    EditorSceneManager.GetSceneCullingMask(previewScene);

                lightObject = new GameObject("Phase6ThumbnailLight")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                SceneManager.MoveGameObjectToScene(lightObject, previewScene);
                lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.96f, 0.9f, 1f);
                light.intensity = 1.2f;
                light.shadows = LightShadows.None;

                renderTexture.Create();
            }

            public void Build(GameObject prefab, string assetPath)
            {
                GameObject instance = null;
                var previousActive = RenderTexture.active;
                try
                {
                    instance = PrefabUtility.InstantiatePrefab(prefab, previewScene) as GameObject ??
                        throw new InvalidOperationException(
                            $"Could not instantiate Prefab '{AssetDatabase.GetAssetPath(prefab)}'.");
                    instance.hideFlags = HideFlags.HideAndDontSave;

                    var bounds = GetVisibleBounds(instance);
                    cameraObject.transform.rotation = Quaternion.Euler(28f, 135f, 0f);
                    cameraObject.transform.position =
                        bounds.center - cameraObject.transform.forward *
                        (bounds.extents.magnitude + 5f);
                    camera.orthographicSize = CalculateOrthographicSize(camera, bounds) * 1.18f;

                    camera.Render();
                    RenderTexture.active = renderTexture;
                    texture.ReadPixels(
                        new Rect(0f, 0f, ThumbnailSize, ThumbnailSize),
                        0,
                        0,
                        false);
                    texture.Apply(false, false);
                    File.WriteAllBytes(GetAbsoluteProjectPath(assetPath), texture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previousActive;
                    if (instance != null)
                    {
                        UnityEngine.Object.DestroyImmediate(instance);
                    }
                }

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter ??
                    throw new InvalidOperationException(
                        $"Expected TextureImporter at '{assetPath}'.");
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.sRGBTexture = true;
                importer.maxTextureSize = ThumbnailSize;
                importer.SaveAndReimport();
            }

            public void Dispose()
            {
                if (cameraObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(cameraObject);
                }

                if (lightObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(lightObject);
                }
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(texture);
                EditorSceneManager.ClosePreviewScene(previewScene);
            }
        }

        private static float CalculateOrthographicSize(UnityEngine.Camera camera, Bounds bounds)
        {
            var maximumHorizontal = 0f;
            var maximumVertical = 0f;
            foreach (var corner in GetBoundsCorners(bounds))
            {
                var cameraLocal = camera.transform.InverseTransformPoint(corner);
                maximumHorizontal = Mathf.Max(maximumHorizontal, Mathf.Abs(cameraLocal.x));
                maximumVertical = Mathf.Max(maximumVertical, Mathf.Abs(cameraLocal.y));
            }

            return Mathf.Max(0.5f, maximumVertical, maximumHorizontal / camera.aspect);
        }

        private static IEnumerable<Vector3> GetBoundsCorners(Bounds bounds)
        {
            for (var x = -1; x <= 1; x += 2)
            for (var y = -1; y <= 1; y += 2)
            for (var z = -1; z <= 1; z += 2)
            {
                yield return bounds.center + Vector3.Scale(
                    bounds.extents,
                    new Vector3(x, y, z));
            }
        }

        private static Bounds GetVisibleBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                .ToArray();
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    $"'{root.name}' must contain at least one visible Renderer.");
            }

            var minimum = new Vector3(
                float.PositiveInfinity,
                float.PositiveInfinity,
                float.PositiveInfinity);
            var maximum = new Vector3(
                float.NegativeInfinity,
                float.NegativeInfinity,
                float.NegativeInfinity);
            foreach (var renderer in renderers)
            {
                foreach (var corner in GetBoundsCorners(renderer.bounds))
                {
                    var local = root.transform.InverseTransformPoint(corner);
                    minimum = Vector3.Min(minimum, local);
                    maximum = Vector3.Max(maximum, local);
                }
            }

            var bounds = new Bounds();
            bounds.SetMinMax(minimum, maximum);
            return bounds;
        }

        private static FurnitureContentCatalog PublishProductionCatalogue(
            IReadOnlyList<FurnitureDefinitionAsset> definitions)
        {
            var candidate = ScriptableObject.CreateInstance<FurnitureContentCatalog>();
            try
            {
                SetProductionCatalogueEntries(candidate, definitions);
                candidate.BuildRuntimeCatalog();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(candidate);
            }

            var catalogue = AssetDatabase.LoadAssetAtPath<FurnitureContentCatalog>(
                Phase6DecorationAssetPaths.ProductionCataloguePath);
            if (catalogue == null)
            {
                catalogue = ScriptableObject.CreateInstance<FurnitureContentCatalog>();
                AssetDatabase.CreateAsset(
                    catalogue,
                    Phase6DecorationAssetPaths.ProductionCataloguePath);
            }

            SetProductionCatalogueEntries(catalogue, definitions);
            EditorUtility.SetDirty(catalogue);
            return catalogue;
        }

        private static void SetProductionCatalogueEntries(
            FurnitureContentCatalog catalogue,
            IReadOnlyList<FurnitureDefinitionAsset> definitions)
        {
            var serialized = new SerializedObject(catalogue);
            var entries = serialized.FindProperty("entries");
            entries.arraySize = definitions.Count;
            for (var index = 0; index < definitions.Count; index++)
            {
                entries.GetArrayElementAtIndex(index).objectReferenceValue = definitions[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static DecorationCatalogueAsset PublishDecorationCatalogue(
            IReadOnlyList<FurnitureDefinitionAsset> definitions,
            IReadOnlyList<Sprite> thumbnails)
        {
            if (definitions.Count != thumbnails.Count)
            {
                throw new ArgumentException(
                    "Decoration catalogue definitions and thumbnails must have matching counts.");
            }

            var candidate = ScriptableObject.CreateInstance<DecorationCatalogueAsset>();
            try
            {
                SetDecorationCatalogueEntries(candidate, definitions, thumbnails);
                ValidateDecorationCatalogue(candidate);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(candidate);
            }

            var catalogue = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                Phase6DecorationAssetPaths.DecorationCataloguePath);
            if (catalogue == null)
            {
                catalogue = ScriptableObject.CreateInstance<DecorationCatalogueAsset>();
                AssetDatabase.CreateAsset(
                    catalogue,
                    Phase6DecorationAssetPaths.DecorationCataloguePath);
            }

            SetDecorationCatalogueEntries(catalogue, definitions, thumbnails);
            EditorUtility.SetDirty(catalogue);
            return catalogue;
        }

        private static void SetDecorationCatalogueEntries(
            DecorationCatalogueAsset catalogue,
            IReadOnlyList<FurnitureDefinitionAsset> definitions,
            IReadOnlyList<Sprite> thumbnails)
        {
            var serialized = new SerializedObject(catalogue);
            var entries = serialized.FindProperty("entries");
            entries.arraySize = definitions.Count;
            for (var index = 0; index < definitions.Count; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("definition").objectReferenceValue = definitions[index];
                entry.FindPropertyRelative("thumbnail").objectReferenceValue = thumbnails[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BuildDecorationUiAssets()
        {
            var candidate = CreateUiCandidateSet();
            try
            {
                ValidateUiCandidateSet(candidate);
                if (UiCandidateValidationFaultForTests)
                {
                    throw new InvalidOperationException(
                        "Injected Task 6 UI candidate validation fault.");
                }

                PublishUiCandidateSet(candidate);
            }
            finally
            {
                candidate.Dispose();
            }
        }

        private static UiCandidateSet CreateUiCandidateSet()
        {
            var source = RequireAsset<Font>(Phase5FontSourcePath, "Phase 5 Noto Sans SC source");
            var theme = RequireAsset<AnimalCafeUiTheme>(
                Phase5UiAssetPaths.ThemePath, "Phase 5 UI Theme");
            var font = TMP_FontAsset.CreateFontAsset(
                source,
                64,
                8,
                GlyphRenderMode.SDFAA,
                2048,
                2048,
                AtlasPopulationMode.Dynamic,
                true) ?? throw new InvalidOperationException(
                    "TMP failed to create the Task 6 UI font candidate.");
            font.name = "NotoSansSC-Phase6 SDF";
            if (!font.TryAddCharacters(
                    AllRequiredDecorationUiCharacters,
                    out var missing,
                    includeFontFeatures: true)
                || !string.IsNullOrEmpty(missing))
            {
                UnityEngine.Object.DestroyImmediate(font);
                throw new InvalidOperationException(
                    "Task 6 UI font candidate is missing required glyphs: " + missing);
            }

            CanonicalizeFontFeatureLookupFlags(font);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.material.name = "NotoSansSC-Phase6 Material";
            font.atlasTextures[0].name = "NotoSansSC-Phase6 Atlas";

            GameObject catalogue = null;
            GameObject actionBar = null;
            GameObject storeModal = null;
            try
            {
                catalogue = CreateCataloguePrefabCandidate(font, theme);
                actionBar = CreateActionBarPrefabCandidate(font, theme);
                storeModal = CreateStoreModalPrefabCandidate(font, theme);
                return new UiCandidateSet(font, catalogue, actionBar, storeModal);
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(catalogue);
                UnityEngine.Object.DestroyImmediate(actionBar);
                UnityEngine.Object.DestroyImmediate(storeModal);
                DestroyTransientFont(font);
                throw;
            }
        }

        private static GameObject CreateCataloguePrefabCandidate(
            TMP_FontAsset font,
            AnimalCafeUiTheme theme)
        {
            var root = CreateUiRoot("PF_UI_DecorationCatalogue");
            root.AddComponent<SafeAreaContainer>();
            var group = root.AddComponent<CanvasGroup>();
            var view = root.AddComponent<DecorationCatalogueView>();

            var expanded = CreateRect("ExpandedSheet", root.transform, new Vector2(980f, 720f));
            StretchHorizontal(expanded.GetComponent<RectTransform>(), 24f, 720f);
            var expandedImage = AddImage(expanded, theme.Colors.Surface, true);
            AddPointerHook(expanded);
            ConfigurePanel(expanded, theme, UiPanelStyle.LightFrost);
            var title = CreateText(
                "Title", expanded.transform, font, "Furniture Catalogue", 28f,
                new Vector2(720f, 64f));
            StretchHorizontalCentered(title.rectTransform, 100f, 64f, 300f);
            ApplyTextToken(title, theme.Typography.Heading, theme.Colors.Text);
            var collapse = CreateButton(
                "CollapseButton", expanded.transform, new Vector2(64f, 64f), "−", font, theme);
            AnchorTopRight(collapse.GetComponent<RectTransform>(), new Vector2(-48f, -48f));
            var content = CreateRect("Content", expanded.transform, new Vector2(920f, 600f));
            StretchHorizontal(content.GetComponent<RectTransform>(), 24f, 600f);
            SetAnchoredPosition(content.GetComponent<RectTransform>(), new Vector2(0f, 24f));

            var tileObject = CreateRect(
                "TileTemplate", content.transform, new Vector2(880f, 132f));
            StretchHorizontal(tileObject.GetComponent<RectTransform>(), 12f, 132f);
            var tileImage = AddImage(tileObject, theme.Colors.Accent, true);
            var tileButton = tileObject.AddComponent<Button>();
            tileButton.targetGraphic = tileImage;
            AddPointerHook(tileObject);
            var thumbnailObject = CreateRect(
                "Thumbnail", tileObject.transform, new Vector2(112f, 112f));
            AnchorLeft(thumbnailObject.GetComponent<RectTransform>(), new Vector2(68f, 0f));
            var thumbnail = AddImage(thumbnailObject, Color.white, false);
            var name = CreateText(
                "Name", tileObject.transform, font, "Counter Module", 24f,
                new Vector2(420f, 44f));
            StretchHorizontalCentered(name.rectTransform, 200f, 44f, 28f);
            ApplyTextToken(name, theme.Typography.Body, theme.Colors.Surface);
            var footprint = CreateText(
                "Footprint", tileObject.transform, font, "1 × 1", 20f,
                new Vector2(180f, 40f));
            AnchorRight(footprint.rectTransform, new Vector2(-100f, 28f));
            ApplyTextToken(footprint, theme.Typography.Label, theme.Colors.Surface);
            var warningShape = CreateRect(
                "WarningShape", tileObject.transform, new Vector2(20f, 44f));
            SetAnchoredPosition(warningShape.GetComponent<RectTransform>(), new Vector2(-270f, -34f));
            AddImage(warningShape, theme.Colors.Warning, false);
            var warning = CreateText(
                "WarningLabel", tileObject.transform, font, "Missing thumbnail", 18f,
                new Vector2(520f, 44f));
            ApplyTextToken(warning, theme.Typography.Label, theme.Colors.Surface);
            SetAnchoredPosition(warning.rectTransform, new Vector2(22f, -34f));
            var tile = tileObject.AddComponent<DecorationCatalogueTileView>();
            AssignObjectReferences(tile,
                ("button", tileButton),
                ("thumbnailImage", thumbnail),
                ("nameLabel", name),
                ("footprintLabel", footprint),
                ("warningLabel", warning),
                ("warningShape", warningShape));
            tileObject.SetActive(false);

            var collapsed = CreateRect(
                "CollapsedHandle", root.transform, new Vector2(240f, 64f));
            // The Catalogue root slides down by 220 logical units when collapsed.
            // Offset the handle upward so its final screen position remains at Safe Area bottom.
            AnchorBottomCenter(collapsed.GetComponent<RectTransform>(), new Vector2(0f, 252f));
            var collapsedImage = AddImage(
                collapsed, theme.Colors.Accent, true);
            var collapsedButton = collapsed.AddComponent<Button>();
            collapsedButton.targetGraphic = collapsedImage;
            AddPointerHook(collapsed);
            var collapsedLabel = CreateText(
                "Label", collapsed.transform, font, "Catalogue", 22f,
                new Vector2(200f, 48f));
            ApplyTextToken(collapsedLabel, theme.Typography.Label, theme.Colors.Surface);
            collapsed.SetActive(false);

            AssignObjectReferences(view,
                ("canvasGroup", group),
                ("expandedRoot", expanded),
                ("collapsedRoot", collapsed),
                ("collapseButton", collapse),
                ("collapsedHandleButton", collapsedButton),
                ("contentRoot", content.transform),
                ("tileTemplate", tile));
            root.SetActive(false);
            return root;
        }

        private static GameObject CreateActionBarPrefabCandidate(
            TMP_FontAsset font,
            AnimalCafeUiTheme theme)
        {
            var root = CreateUiRoot("PF_UI_DecorationActionBar");
            root.AddComponent<SafeAreaContainer>();
            var group = root.AddComponent<CanvasGroup>();
            var view = root.AddComponent<DecorationActionBarView>();
            var panel = CreateRect("ActionPanel", root.transform, new Vector2(216f, 48f));
            var panelImage = AddImage(panel, Color.clear, false);
            panelImage.material = null;
            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var store = CreateButton(
                "StoreButton", panel.transform, new Vector2(48f, 48f), "□", font, theme);
            var cancel = CreateButton(
                "CancelButton", panel.transform, new Vector2(48f, 48f), "×", font, theme);
            var rotate = CreateButton(
                "RotateButton", panel.transform, new Vector2(48f, 48f), "R", font, theme);
            var confirm = CreateButton(
                "ConfirmButton", panel.transform, new Vector2(48f, 48f), "✓", font, theme);
            foreach (var button in new[] { store, rotate, cancel, confirm })
            {
                button.GetComponent<RectTransform>().sizeDelta = new Vector2(48f, 48f);
            }
            AddActionSemantic(store, "Store", font, theme);
            AddActionSemantic(cancel, "Cancel", font, theme);
            AddActionSemantic(rotate, "Rotate", font, theme);
            AddActionSemantic(confirm, "Confirm", font, theme);
            var feedback = CreateRect("FeedbackToast", root.transform, new Vector2(480f, 64f));
            var feedbackRect = feedback.GetComponent<RectTransform>();
            feedbackRect.anchorMin = new Vector2(0.5f, 1f);
            feedbackRect.anchorMax = new Vector2(0.5f, 1f);
            feedbackRect.pivot = new Vector2(0.5f, 1f);
            feedbackRect.anchoredPosition = new Vector2(0f, -24f);
            var feedbackBackground = AddImage(feedback, theme.Colors.Surface, false);
            var feedbackColor = feedbackBackground.color;
            feedbackColor.a = 0.94f;
            feedbackBackground.color = feedbackColor;
            var feedbackGroup = feedback.AddComponent<CanvasGroup>();
            feedbackGroup.alpha = 0f;
            feedbackGroup.blocksRaycasts = false;
            feedbackGroup.interactable = false;
            var stateShape = CreateRect(
                "StateShape", feedback.transform, new Vector2(12f, 32f));
            AnchorLeft(stateShape.GetComponent<RectTransform>(), new Vector2(16f, 0f));
            AddImage(stateShape, theme.Colors.Warning, false);
            var message = CreateText(
                "Message", feedback.transform, font, "Space already occupied", 22f,
                new Vector2(428f, 48f));
            StretchHorizontalCentered(message.rectTransform, 36f, 48f, 0f);
            ApplyTextToken(message, theme.Typography.Body, theme.Colors.Text);
            feedback.SetActive(false);

            AssignObjectReferences(view,
                ("canvasGroup", group),
                ("presentationRoot", panel.GetComponent<RectTransform>()),
                ("storeButton", store),
                ("rotateButton", rotate),
                ("cancelButton", cancel),
                ("confirmButton", confirm),
                ("feedbackLabel", message),
                ("feedbackStateShape", stateShape),
                ("feedbackRoot", feedbackRect),
                ("feedbackCanvasGroup", feedbackGroup));
            root.SetActive(false);
            return root;
        }

        private static void AddActionSemantic(
            Button button,
            string label,
            TMP_FontAsset font,
            AnimalCafeUiTheme theme)
        {
            var tooltip = CreateRect(
                "Tooltip", button.transform, new Vector2(128f, 40f));
            var inwardOffset = label == "Store"
                ? 48f
                : label == "Confirm"
                    ? -48f
                    : 0f;
            SetAnchoredPosition(
                tooltip.GetComponent<RectTransform>(), new Vector2(inwardOffset, -54f));
            var background = AddImage(tooltip, theme.Colors.Surface, false);
            var backgroundColor = background.color;
            backgroundColor.a = .96f;
            background.color = backgroundColor;
            var tooltipLabel = CreateText(
                "Label", tooltip.transform, font, label, 16f, new Vector2(116f, 36f));
            ApplyTextToken(tooltipLabel, theme.Typography.Label, theme.Colors.Text);
            tooltipLabel.raycastTarget = false;
            tooltip.SetActive(false);

            var semantic = button.GetComponent<DecorationPointerBoundaryEventHook>();
            var serialized = new SerializedObject(semantic);
            serialized.FindProperty("semanticLabel").stringValue = label;
            serialized.FindProperty("tooltipRoot").objectReferenceValue = tooltip;
            serialized.FindProperty("tooltipLabel").objectReferenceValue = tooltipLabel;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateStoreModalPrefabCandidate(
            TMP_FontAsset font,
            AnimalCafeUiTheme theme)
        {
            var root = CreateUiRoot("PF_UI_DecorationStoreModal");
            var group = root.AddComponent<CanvasGroup>();
            var sharedModal = root.AddComponent<AnimalCafeModalView>();
            var view = root.AddComponent<DecorationStoreModalView>();

            var blocker = CreateRect(
                "ModalBlocker", root.transform, new Vector2(1080f, 1920f));
            StretchToParent(blocker.GetComponent<RectTransform>());
            var blockerImage = AddImage(blocker, new Color(0.08f, 0.08f, 0.08f, 0.52f), true);
            var blockerButton = blocker.AddComponent<Button>();
            blockerButton.targetGraphic = blockerImage;
            AddPointerHook(blocker);

            var safeArea = CreateRect("SafeArea", root.transform, new Vector2(1080f, 1920f));
            StretchToParent(safeArea.GetComponent<RectTransform>());
            safeArea.AddComponent<SafeAreaContainer>();
            var content = CreateRect("Content", safeArea.transform, new Vector2(840f, 540f));
            StretchHorizontal(content.GetComponent<RectTransform>(), 40f, 540f);
            AddImage(content, theme.Colors.Surface, false);
            ConfigurePanel(content, theme, UiPanelStyle.Solid);
            var title = CreateText(
                "Title", content.transform, font, "Store furniture?", 32f,
                new Vector2(740f, 72f));
            StretchHorizontalCentered(title.rectTransform, 50f, 72f, 178f);
            ApplyTextToken(title, theme.Typography.Heading, theme.Colors.Text);
            SetAnchoredPosition(title.rectTransform, new Vector2(0f, 178f));
            var body = CreateText(
                "Body", content.transform, font,
                "This removes it from the current layout. You can place it again from the catalogue.",
                24f,
                new Vector2(720f, 220f));
            StretchHorizontalCentered(body.rectTransform, 60f, 220f, 28f);
            ApplyTextToken(body, theme.Typography.Body, theme.Colors.Text);
            SetAnchoredPosition(body.rectTransform, new Vector2(0f, 28f));
            var cancel = CreateButton(
                "CancelButton", content.transform, new Vector2(260f, 72f), "Cancel", font, theme);
            var confirm = CreateButton(
                "StoreButton", content.transform, new Vector2(260f, 72f), "Store", font, theme);
            SetAnchoredPosition(cancel.GetComponent<RectTransform>(), new Vector2(-160f, -190f));
            SetAnchoredPosition(confirm.GetComponent<RectTransform>(), new Vector2(160f, -190f));

            sharedModal.BindPrefabReferences(confirm, cancel, blockerButton, group);
            AssignObjectReferences(view,
                ("modalView", sharedModal),
                ("confirmButton", confirm),
                ("cancelButton", cancel),
                ("modalBlocker", blockerButton),
                ("canvasGroup", group),
                ("titleLabel", title),
                ("bodyLabel", body));
            root.SetActive(false);
            return root;
        }

        private static void ValidateUiCandidateSet(UiCandidateSet candidate)
        {
            if (candidate.Font.atlasPopulationMode != AtlasPopulationMode.Static
                || candidate.Font.atlasTextures.Length != 1
                || candidate.Font.material == null
                || candidate.Font.material.mainTexture != candidate.Font.atlasTextures[0])
            {
                throw new InvalidOperationException(
                    "Task 6 UI font candidate must own one static atlas and material.");
            }

            var missing = Phase5UiFontCoverage.FindMissingUnicodeScalars(
                candidate.Font,
                AllRequiredDecorationUiCharacters);
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "Task 6 UI font candidate failed canonical glyph validation.");
            }

            ValidateUiCandidateRoot<DecorationCatalogueView>(candidate.CatalogueRoot);
            ValidateUiCandidateRoot<DecorationActionBarView>(candidate.ActionBarRoot);
            ValidateUiCandidateRoot<DecorationStoreModalView>(candidate.StoreModalRoot);
            ValidateUiSerializedReferences(candidate.CatalogueRoot.GetComponent<DecorationCatalogueView>(),
                "canvasGroup", "expandedRoot", "collapsedRoot", "collapseButton",
                "collapsedHandleButton", "contentRoot", "tileTemplate");
            ValidateUiSerializedReferences(candidate.ActionBarRoot.GetComponent<DecorationActionBarView>(),
                "canvasGroup", "storeButton", "rotateButton", "cancelButton",
                "confirmButton", "feedbackLabel", "feedbackStateShape",
                "feedbackRoot", "feedbackCanvasGroup");
            ValidateUiSerializedReferences(candidate.StoreModalRoot.GetComponent<DecorationStoreModalView>(),
                "modalView", "confirmButton", "cancelButton", "modalBlocker",
                "canvasGroup", "titleLabel", "bodyLabel");
            ValidateUiRootContracts(candidate.CatalogueRoot, candidate.Font, allowFullScreenBlocker: false);
            ValidateUiRootContracts(candidate.ActionBarRoot, candidate.Font, allowFullScreenBlocker: false);
            ValidateUiRootContracts(candidate.StoreModalRoot, candidate.Font, allowFullScreenBlocker: true);
            var theme = RequireAsset<AnimalCafeUiTheme>(
                Phase5UiAssetPaths.ThemePath, "Phase 5 UI Theme");
            ValidateCanonicalUiContract(candidate.CatalogueRoot, candidate.Font, theme);
            ValidateCanonicalUiContract(candidate.ActionBarRoot, candidate.Font, theme);
            ValidateCanonicalUiContract(candidate.StoreModalRoot, candidate.Font, theme);
        }

        private static void ValidateUiCandidateRoot<T>(GameObject root) where T : Component
        {
            if (root == null || root.GetComponent<T>() == null || root.activeSelf)
            {
                throw new InvalidOperationException(
                    "Task 6 UI candidate root is incomplete or not inactive.");
            }

            ValidateNoMissingScriptsRecursively(root);

            if (root.GetComponentsInChildren<Canvas>(true).Length != 0
                || root.GetComponentsInChildren<GraphicRaycaster>(true).Length != 0)
            {
                throw new InvalidOperationException(
                    "Task 6 UI candidates must remain Canvas-less and raycaster-less.");
            }

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font == null || text.fontSharedMaterial != text.font.material)
                {
                    throw new InvalidOperationException(
                        "Every Task 6 TMP text candidate must own its font material.");
                }
            }
        }

        private static void ValidateUiSerializedReferences(
            UnityEngine.Object target,
            params string[] fieldNames)
        {
            var serialized = new SerializedObject(target);
            foreach (var fieldName in fieldNames)
            {
                var property = serialized.FindProperty(fieldName);
                if (property == null || property.objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        target.GetType().Name + " is missing serialized reference '" +
                        fieldName + "'.");
                }
            }
        }

        private static void ValidateUiRootContracts(
            GameObject root,
            TMP_FontAsset font,
            bool allowFullScreenBlocker)
        {
            if (root.GetComponentsInChildren<SafeAreaContainer>(true).Length == 0)
            {
                throw new InvalidOperationException("Task 6 UI candidate requires Safe Area ownership.");
            }

            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.onClick.GetPersistentEventCount() != 0)
                {
                    throw new InvalidOperationException(
                        "Task 6 UI buttons must not persist prefab callbacks: " + button.name);
                }

                var rect = button.GetComponent<RectTransform>();
                if (rect.anchorMin.y == rect.anchorMax.y
                        && rect.rect.height < AnimalCafeUiTheme.MinimumTouchTargetSize
                    || rect.anchorMin.x == rect.anchorMax.x
                        && rect.rect.width < AnimalCafeUiTheme.MinimumTouchTargetSize)
                {
                    throw new InvalidOperationException(
                        "Task 6 UI touch target is below 48x48: " + button.name);
                }

                if (button.GetComponent<DecorationPointerBoundaryEventHook>() == null)
                {
                    throw new InvalidOperationException(
                        "Task 6 UI button is missing its pointer adapter: " + button.name);
                }
            }

            var hasRequiredModalBlocker = false;
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true)
                .Where(item => item.raycastTarget))
            {
                if (graphic.GetComponent<DecorationPointerBoundaryEventHook>() == null)
                {
                    throw new InvalidOperationException(
                        "Task 6 UI raycast target is missing its pointer adapter: " + graphic.name);
                }

                var rect = graphic.rectTransform;
                var fillsParent = rect.anchorMin == Vector2.zero
                    && rect.anchorMax == Vector2.one
                    && rect.offsetMin == Vector2.zero
                    && rect.offsetMax == Vector2.zero;
                if (fillsParent && (!allowFullScreenBlocker || graphic.name != "ModalBlocker"))
                {
                    throw new InvalidOperationException(
                        "Only the Task 6 ModalBlocker may be full-screen.");
                }

                hasRequiredModalBlocker |= fillsParent && graphic.name == "ModalBlocker";
            }

            if (allowFullScreenBlocker && !hasRequiredModalBlocker)
            {
                throw new InvalidOperationException(
                    "The Task 6 ModalBlocker must stretch across its complete prefab root.");
            }

            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font != font
                    || text.fontSharedMaterial != font.material
                    || text.enableAutoSizing
                    || text.textWrappingMode != TextWrappingModes.Normal)
                {
                    throw new InvalidOperationException(
                        "Task 6 TMP ownership/wrapping contract failed: " + text.name);
                }
            }
        }

        private static void ValidateCanonicalUiContract(
            GameObject root,
            TMP_FontAsset font,
            AnimalCafeUiTheme theme)
        {
            if (root.GetComponent<DecorationCatalogueView>() != null)
            {
                ValidateSurface(root.transform.Find("ExpandedSheet"), theme,
                    UiPanelStyle.LightFrost, mustRaycast: true);
                ValidateText(root, "ExpandedSheet/Title", "Furniture Catalogue",
                    theme.Typography.Heading, font, theme.Colors.Text);
                ValidateText(root, "CollapsedHandle/Label", "Catalogue",
                    theme.Typography.Label, font, theme.Colors.Surface);
                ValidateText(root, "ExpandedSheet/CollapseButton/Label", "−",
                    theme.Typography.Label, font, theme.Colors.Surface);
                ValidateText(root, "ExpandedSheet/Content/TileTemplate/Name", "Counter Module",
                    theme.Typography.Body, font, theme.Colors.Surface);
                ValidateText(root, "ExpandedSheet/Content/TileTemplate/Footprint", "1 × 1",
                    theme.Typography.Label, font, theme.Colors.Surface);
                ValidateText(root, "ExpandedSheet/Content/TileTemplate/WarningLabel",
                    "Missing thumbnail", theme.Typography.Label, font, theme.Colors.Surface);
                return;
            }

            if (root.GetComponent<DecorationActionBarView>() != null)
            {
                var actionPanel = root.transform.Find("ActionPanel");
                var panelImage = actionPanel?.GetComponent<Image>();
                if (panelImage == null || panelImage.raycastTarget
                    || panelImage.color.a != 0f
                    || panelImage.material != panelImage.defaultMaterial
                    || actionPanel.GetComponent<HorizontalLayoutGroup>() == null)
                {
                    throw new InvalidOperationException(
                        "Decoration action panel must be transparent and compact.");
                }
                ValidateText(root, "FeedbackToast/Message", "Space already occupied",
                    theme.Typography.Body, font, theme.Colors.Text);
                foreach (var item in new[]
                         {
                             (Name: "Store", Symbol: "□"),
                             (Name: "Cancel", Symbol: "×"),
                             (Name: "Rotate", Symbol: "R"),
                             (Name: "Confirm", Symbol: "✓")
                         })
                {
                    var buttonPath = "ActionPanel/" + item.Name + "Button";
                    ValidateText(root, buttonPath + "/Label", item.Symbol,
                        theme.Typography.Label, font, theme.Colors.Surface);
                    ValidateText(root, buttonPath + "/Tooltip/Label", item.Name,
                        theme.Typography.Label, font, theme.Colors.Text);
                    var button = root.transform.Find(buttonPath);
                    var semantic = button?.GetComponent<DecorationPointerBoundaryEventHook>();
                    var tooltip = button?.Find("Tooltip");
                    var semanticSerialized = semantic != null
                        ? new SerializedObject(semantic)
                        : null;
                    if (semanticSerialized == null
                        || tooltip == null
                        || semanticSerialized.FindProperty("semanticLabel").stringValue != item.Name
                        || semanticSerialized.FindProperty("tooltipRoot").objectReferenceValue
                        != tooltip?.gameObject
                        || semanticSerialized.FindProperty("tooltipLabel").objectReferenceValue
                        != tooltip?.Find("Label")?.GetComponent<TMP_Text>()
                        || tooltip.gameObject.activeSelf
                        || tooltip.GetComponentsInChildren<Graphic>(true)
                            .Any(graphic => graphic.raycastTarget))
                    {
                        throw new InvalidOperationException(
                            item.Name + " action semantic tooltip contract failed.");
                    }
                }
                return;
            }

            ValidateSurface(root.transform.Find("SafeArea/Content"), theme,
                UiPanelStyle.Solid, mustRaycast: false);
            ValidateText(root, "SafeArea/Content/Title", "Store furniture?",
                theme.Typography.Heading, font, theme.Colors.Text);
            ValidateText(root, "SafeArea/Content/Body",
                "This removes it from the current layout. You can place it again from the catalogue.",
                theme.Typography.Body, font, theme.Colors.Text);
            foreach (var label in new[] { "Cancel", "Store" })
            {
                ValidateText(root, "SafeArea/Content/" + label + "Button/Label", label,
                    theme.Typography.Label, font, theme.Colors.Surface);
            }
        }

        private static void ValidateSurface(
            Transform target,
            AnimalCafeUiTheme theme,
            UiPanelStyle style,
            bool mustRaycast,
            float alpha = 1f)
        {
            var image = target?.GetComponent<Image>();
            var panel = target?.GetComponent<AnimalCafePanelView>();
            var expectedMaterial = style == UiPanelStyle.LightFrost
                ? theme.Materials.LightFrost
                : theme.Materials.Solid;
            var serialized = panel == null ? null : new SerializedObject(panel);
            var expectedColor = theme.Colors.Surface;
            expectedColor.a = alpha;
            if (image == null
                || panel == null
                || image.raycastTarget != mustRaycast
                || image.material != expectedMaterial
                || image.color != expectedColor
                || serialized.FindProperty("requestedStyle").enumValueIndex != (int)style)
            {
                throw new InvalidOperationException(
                    "Task 6 UI surface does not match the Phase 5 Theme contract.");
            }
        }

        private static void ValidateText(
            GameObject root,
            string path,
            string expectedCopy,
            UiTextStyleToken token,
            TMP_FontAsset font,
            Color color)
        {
            var text = root.transform.Find(path)?.GetComponent<TMP_Text>();
            if (text == null
                || text.text != expectedCopy
                || text.font != font
                || text.fontSharedMaterial != font.material
                || !Mathf.Approximately(text.fontSize, token.FontSize)
                || text.fontStyle != token.FontStyle
                || !Mathf.Approximately(text.lineSpacing, token.LineSpacing)
                || text.color != color)
            {
                throw new InvalidOperationException(
                    "Task 6 UI canonical text contract failed at '" + path + "'.");
            }
        }

        private static void PublishUiCandidateSet(UiCandidateSet candidate)
        {
            var paths = new[]
            {
                Phase6DecorationAssetPaths.DecorationUiFontPath,
                Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
                Phase6DecorationAssetPaths.DecorationActionBarPrefabPath,
                Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath
            };
            var identities = CaptureExistingUiIdentities(paths);
            var backup = UiPublishBackup.Create(paths);
            var writeCount = 0;
            try
            {
                EnsureFolder(Phase6DecorationAssetPaths.UiRootFolderPath);
                EnsureFolder(Phase6DecorationAssetPaths.UiFontFolderPath);
                EnsureFolder(Phase6DecorationAssetPaths.UiPrefabFolderPath);
                var liveFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    Phase6DecorationAssetPaths.DecorationUiFontPath);
                if (liveFont == null)
                {
                    liveFont = candidate.Font;
                    AssetDatabase.CreateAsset(
                        liveFont,
                        Phase6DecorationAssetPaths.DecorationUiFontPath);
                    AssetDatabase.AddObjectToAsset(liveFont.atlasTextures[0], liveFont);
                    AssetDatabase.AddObjectToAsset(liveFont.material, liveFont);
                    SaveGeneratedAssets(
                        liveFont, liveFont.material, liveFont.atlasTextures[0]);
                    ReplaceCandidateFont(candidate, liveFont);
                    ThrowAfterRequestedUiPublishWrite(++writeCount);
                }
                else
                {
                    CopyFontCandidateInPlace(candidate.Font, liveFont);
                    ReplaceCandidateFont(candidate, liveFont);
                    SaveGeneratedAssets(
                        liveFont, liveFont.material, liveFont.atlasTextures[0]);
                    ThrowAfterRequestedUiPublishWrite(++writeCount);
                }

                SaveUiPrefab(
                    candidate.CatalogueRoot,
                    Phase6DecorationAssetPaths.DecorationCataloguePrefabPath);
                ThrowAfterRequestedUiPublishWrite(++writeCount);
                SaveUiPrefab(
                    candidate.ActionBarRoot,
                    Phase6DecorationAssetPaths.DecorationActionBarPrefabPath);
                ThrowAfterRequestedUiPublishWrite(++writeCount);
                SaveUiPrefab(
                    candidate.StoreModalRoot,
                    Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath);
                ThrowAfterRequestedUiPublishWrite(++writeCount);

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ValidatePublishedUiSet();
                if (UiPostPublishValidationFaultForTests)
                {
                    throw new InvalidOperationException(
                        "Injected Task 6 UI post-publish validation fault.");
                }
                ValidatePublishedUiIdentities(identities);
            }
            catch
            {
                backup.Restore();
                throw;
            }
            finally
            {
                backup.Dispose();
            }
        }

        private static void ValidateLiveUiFont(TMP_FontAsset font)
        {
            var subassets = AssetDatabase.LoadAllAssetsAtPath(
                Phase6DecorationAssetPaths.DecorationUiFontPath);
            var materials = subassets.OfType<Material>().ToArray();
            var atlases = subassets.OfType<Texture2D>().ToArray();
            var featureRecords = font.fontFeatureTable?.glyphPairAdjustmentRecords;
            if (font.atlasPopulationMode != AtlasPopulationMode.Static
                || materials.Length != 1
                || atlases.Length != 1
                || font.atlasTextures == null
                || font.atlasTextures.Length != 1
                || font.material != materials[0]
                || font.atlasTextures[0] != atlases[0]
                || materials[0].mainTexture != atlases[0]
                || Phase5UiFontCoverage.FindMissingUnicodeScalars(
                    font,
                    AllRequiredDecorationUiCharacters).Count != 0
                || featureRecords == null
                || featureRecords.Any(record => record.featureLookupFlags
                    != UnityEngine.TextCore.LowLevel.FontFeatureLookupFlags.None))
            {
                throw new InvalidOperationException(
                    "Existing Task 6 UI font does not match the fixed static font contract.");
            }

            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    font, out string fontGuid, out long fontLocalId)
                || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    materials[0], out string materialGuid, out long materialLocalId)
                || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    atlases[0], out string atlasGuid, out long atlasLocalId)
                || fontGuid != materialGuid
                || fontGuid != atlasGuid
                || fontLocalId == materialLocalId
                || fontLocalId == atlasLocalId
                || materialLocalId == atlasLocalId)
            {
                throw new InvalidOperationException(
                    "Task 6 UI font, material and atlas must be unique local objects in one asset.");
            }
        }

        private static void CanonicalizeFontFeatureLookupFlags(TMP_FontAsset font)
        {
            var records = font.fontFeatureTable.glyphPairAdjustmentRecords;
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                record.featureLookupFlags =
                    UnityEngine.TextCore.LowLevel.FontFeatureLookupFlags.None;
                records[index] = record;
            }
        }

        private static void CopyFontCandidateInPlace(
            TMP_FontAsset candidate,
            TMP_FontAsset live)
        {
            var subassets = AssetDatabase.LoadAllAssetsAtPath(
                Phase6DecorationAssetPaths.DecorationUiFontPath);
            var liveMaterial = subassets.OfType<Material>().Single();
            var liveAtlas = subassets.OfType<Texture2D>().Single();
            EditorUtility.CopySerialized(candidate.material, liveMaterial);
            if (candidate.atlasTextures[0].width != liveAtlas.width
                || candidate.atlasTextures[0].height != liveAtlas.height)
            {
                throw new InvalidOperationException(
                    "Task 6 atlas dimensions cannot be repaired in place safely.");
            }
            Graphics.CopyTexture(candidate.atlasTextures[0], liveAtlas);
            EditorUtility.CopySerialized(candidate, live);
            live.name = candidate.name;
            liveMaterial.name = candidate.material.name;
            liveAtlas.name = candidate.atlasTextures[0].name;
            liveMaterial.mainTexture = liveAtlas;

            var serialized = new SerializedObject(live);
            serialized.Update();
            serialized.FindProperty("m_Material").objectReferenceValue = liveMaterial;
            var atlases = serialized.FindProperty("m_AtlasTextures");
            atlases.arraySize = 1;
            atlases.GetArrayElementAtIndex(0).objectReferenceValue = liveAtlas;
            serialized.FindProperty("m_AtlasTextureIndex").intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            live.atlasPopulationMode = AtlasPopulationMode.Static;
            live.ReadFontAssetDefinition();
            EditorUtility.SetDirty(live);
            EditorUtility.SetDirty(liveMaterial);
            EditorUtility.SetDirty(liveAtlas);
            ValidateLiveUiFont(live);
        }

        private static void ValidatePublishedUiSet()
        {
            var font = RequireAsset<TMP_FontAsset>(
                Phase6DecorationAssetPaths.DecorationUiFontPath,
                "Task 6 UI Font");
            ValidateLiveUiFont(font);
            ValidatePublishedPrefab<DecorationCatalogueView>(
                Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
                "ExpandedSheet", "CollapsedHandle");
            ValidatePublishedPrefab<DecorationActionBarView>(
                Phase6DecorationAssetPaths.DecorationActionBarPrefabPath,
                "ActionPanel", "FeedbackToast");
            ValidatePublishedPrefab<DecorationStoreModalView>(
                Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath,
                "ModalBlocker", "SafeArea/Content");
            var theme = RequireAsset<AnimalCafeUiTheme>(
                Phase5UiAssetPaths.ThemePath, "Phase 5 UI Theme");
            ValidateCanonicalUiContract(RequireAsset<GameObject>(
                    Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
                    "Task 6 Catalogue Prefab"), font, theme);
            ValidateCanonicalUiContract(RequireAsset<GameObject>(
                    Phase6DecorationAssetPaths.DecorationActionBarPrefabPath,
                    "Task 6 Action Prefab"), font, theme);
            ValidateCanonicalUiContract(RequireAsset<GameObject>(
                    Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath,
                    "Task 6 Modal Prefab"), font, theme);
        }

        private static void ValidateNoMissingScriptsRecursively(GameObject root)
        {
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) != 0)
                {
                    throw new InvalidOperationException(
                        "Task 6 UI hierarchy contains a missing script at '" +
                        AnimationUtility.CalculateTransformPath(item, root.transform) + "'.");
                }
            }
        }

        private static void ValidatePublishedPrefab<T>(string path, params string[] requiredPaths)
            where T : Component
        {
            var root = RequireAsset<GameObject>(path, "Task 6 UI Prefab");
            if (root.GetComponent<T>() == null)
            {
                throw new InvalidOperationException(
                    "Published Task 6 UI prefab has a missing root view or script: " + path);
            }

            ValidateNoMissingScriptsRecursively(root);


            if (root.GetComponent<T>() is DecorationCatalogueView catalogue)
            {
                ValidateUiSerializedReferences(catalogue, "canvasGroup", "expandedRoot",
                    "collapsedRoot", "collapseButton", "collapsedHandleButton",
                    "contentRoot", "tileTemplate");
            }
            else if (root.GetComponent<T>() is DecorationActionBarView action)
            {
                ValidateUiSerializedReferences(action, "canvasGroup", "storeButton",
                    "presentationRoot", "rotateButton", "cancelButton", "confirmButton", "feedbackLabel",
                    "feedbackStateShape", "feedbackRoot", "feedbackCanvasGroup");
            }
            else if (root.GetComponent<T>() is DecorationStoreModalView modal)
            {
                ValidateUiSerializedReferences(modal, "modalView", "confirmButton",
                    "cancelButton", "modalBlocker", "canvasGroup", "titleLabel", "bodyLabel");
            }

            foreach (var required in requiredPaths)
            {
                if (root.transform.Find(required) == null)
                {
                    throw new InvalidOperationException(
                        "Published Task 6 UI prefab is missing '" + required + "'.");
                }
            }

            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.GetComponent<DecorationPointerBoundaryEventHook>() == null)
                {
                    throw new InvalidOperationException(
                        "Published Task 6 button is missing its pointer adapter: " + button.name);
                }
            }

            var font = RequireAsset<TMP_FontAsset>(
                Phase6DecorationAssetPaths.DecorationUiFontPath, "Task 6 UI Font");
            ValidateUiRootContracts(root, font,
                path == Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath);
        }

        private static void DestroyTransientFont(TMP_FontAsset font)
        {
            if (font == null || AssetDatabase.Contains(font))
            {
                return;
            }

            var material = font.material;
            var atlas = font.atlasTextures != null && font.atlasTextures.Length > 0
                ? font.atlasTextures[0]
                : null;
            UnityEngine.Object.DestroyImmediate(font);
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(atlas);
        }

        private static void ReplaceCandidateFont(UiCandidateSet candidate, TMP_FontAsset liveFont)
        {
            foreach (var root in candidate.Roots)
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = liveFont;
                text.fontSharedMaterial = liveFont.material;
            }
        }

        private static void SaveUiPrefab(GameObject root, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                ReconcileExistingUiPrefab(root, path);
                return;
            }

            var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (saved == null)
            {
                throw new InvalidOperationException(
                    "Could not publish Task 6 UI prefab at '" + path + "'.");
            }
        }

        private static void ReconcileExistingUiPrefab(GameObject candidate, string path)
        {
            var live = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var candidateByPath = BuildTransformMap(candidate.transform);
                var liveByPath = BuildTransformMap(live.transform);
                ReconcileKnownActionToastMigration(
                    candidate, live, path, candidateByPath, liveByPath);
                liveByPath = BuildTransformMap(live.transform);
                ReconcileKnownActionSemanticAdditions(
                    candidate, live, path, candidateByPath, liveByPath);
                liveByPath = BuildTransformMap(live.transform);
                RemoveKnownActionSemanticMissingScripts(candidate, live, path, liveByPath);
                if (!candidateByPath.Keys.SequenceEqual(liveByPath.Keys, StringComparer.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Task 6 UI prefab hierarchy drift is not safely reconcilable: " + path);
                }

                foreach (var item in candidateByPath)
                {
                    var source = item.Value.gameObject;
                    var target = liveByPath[item.Key].gameObject;
                    target.SetActive(source.activeSelf);
                    target.transform.SetSiblingIndex(source.transform.GetSiblingIndex());
                    CopyRect(source.GetComponent<RectTransform>(), target.GetComponent<RectTransform>());
                    CopyComponentIfPresent<Image>(source, target);
                    CopyComponentIfPresent<CanvasGroup>(source, target);
                    CopyComponentIfPresent<HorizontalLayoutGroup>(source, target);
                    CopyComponentIfPresent<SafeAreaContainer>(source, target);
                    CopyComponentIfPresent<AnimalCafePanelView>(source, target);
                    CopyComponentIfPresent<DecorationPointerBoundaryEventHook>(source, target);
                    CopyText(source, target);
                    CopyButton(source, target);
                }

                BindReconciledPrefabReferences(live);
                if (PrefabUtility.SaveAsPrefabAsset(live, path) == null)
                {
                    throw new InvalidOperationException(
                        "Could not reconcile Task 6 UI prefab at '" + path + "'.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(live);
            }
        }

        private static void ReconcileKnownActionToastMigration(
            GameObject candidate,
            GameObject live,
            string path,
            IReadOnlyDictionary<string, Transform> candidateByPath,
            IReadOnlyDictionary<string, Transform> liveByPath)
        {
            if (path != Phase6DecorationAssetPaths.DecorationActionBarPrefabPath
                || candidate.GetComponent<DecorationActionBarView>() == null
                || liveByPath.ContainsKey("FeedbackToast")
                || !liveByPath.TryGetValue("ActionPanel/Feedback", out var oldFeedback)
                || !candidateByPath.TryGetValue("FeedbackToast", out var candidateToast)
                || !liveByPath.TryGetValue("ActionPanel", out var livePanel))
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(oldFeedback.gameObject);
            var toast = UnityEngine.Object.Instantiate(
                candidateToast.gameObject, live.transform, false);
            toast.name = "FeedbackToast";

            var panelView = livePanel.GetComponent<AnimalCafePanelView>();
            if (panelView != null)
            {
                UnityEngine.Object.DestroyImmediate(panelView);
            }

            var panelHook = livePanel.GetComponent<DecorationPointerBoundaryEventHook>();
            if (panelHook != null)
            {
                UnityEngine.Object.DestroyImmediate(panelHook);
            }

            var layout = livePanel.GetComponent<HorizontalLayoutGroup>()
                ?? livePanel.gameObject.AddComponent<HorizontalLayoutGroup>();
            EditorUtility.CopySerialized(
                candidateByPath["ActionPanel"].GetComponent<HorizontalLayoutGroup>(),
                layout);
        }

        private static void ReconcileKnownActionSemanticAdditions(
            GameObject candidate,
            GameObject live,
            string path,
            IReadOnlyDictionary<string, Transform> candidateByPath,
            IReadOnlyDictionary<string, Transform> liveByPath)
        {
            if (path != Phase6DecorationAssetPaths.DecorationActionBarPrefabPath
                || candidate.GetComponent<DecorationActionBarView>() == null
                || !liveByPath.Keys.All(candidateByPath.ContainsKey))
            {
                return;
            }

            var expectedMissingPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var action in new[] { "Store", "Cancel", "Rotate", "Confirm" })
            {
                var tooltipPath = "ActionPanel/" + action + "Button/Tooltip";
                expectedMissingPaths.Add(tooltipPath);
                expectedMissingPaths.Add(tooltipPath + "/Label");
            }

            var actualMissingPaths = new HashSet<string>(
                candidateByPath.Keys.Where(key => !liveByPath.ContainsKey(key)),
                StringComparer.Ordinal);
            if (!actualMissingPaths.SetEquals(expectedMissingPaths))
            {
                return;
            }

            foreach (var action in new[] { "Store", "Cancel", "Rotate", "Confirm" })
            {
                var buttonPath = "ActionPanel/" + action + "Button";
                var liveButton = liveByPath[buttonPath];
                var candidateButton = candidateByPath[buttonPath];
                if (candidateButton.GetComponent<DecorationPointerBoundaryEventHook>() == null
                    || liveButton.GetComponent<DecorationPointerBoundaryEventHook>() == null)
                {
                    throw new InvalidOperationException(
                        "Task 10 action semantic component drift is not safely reconcilable: "
                        + buttonPath);
                }

                var tooltip = UnityEngine.Object.Instantiate(
                    candidateButton.Find("Tooltip").gameObject, liveButton, false);
                tooltip.name = "Tooltip";
            }
        }

        private static void RemoveKnownActionSemanticMissingScripts(
            GameObject candidate,
            GameObject live,
            string path,
            IReadOnlyDictionary<string, Transform> liveByPath)
        {
            if (path != Phase6DecorationAssetPaths.DecorationActionBarPrefabPath
                || candidate.GetComponent<DecorationActionBarView>() == null)
            {
                return;
            }

            var buttons = new List<GameObject>();
            foreach (var action in new[] { "Store", "Cancel", "Rotate", "Confirm" })
            {
                var buttonPath = "ActionPanel/" + action + "Button";
                if (!liveByPath.TryGetValue(buttonPath, out var button)
                    || button.Find("Tooltip/Label") == null
                    || button.GetComponent<DecorationPointerBoundaryEventHook>() == null)
                {
                    return;
                }
                buttons.Add(button.gameObject);
            }

            var missingCounts = buttons
                .Select(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount).ToArray();
            if (missingCounts.All(count => count == 0))
            {
                return;
            }
            if (!missingCounts.All(count => count == 1))
            {
                return;
            }

            foreach (var button in buttons)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(button);
            }
        }

        private static SortedDictionary<string, Transform> BuildTransformMap(Transform root)
        {
            var result = new SortedDictionary<string, Transform>(StringComparer.Ordinal)
            {
                [string.Empty] = root
            };
            foreach (var item in root.GetComponentsInChildren<Transform>(true))
            {
                if (item == root)
                {
                    continue;
                }

                var path = AnimationUtility.CalculateTransformPath(item, root);
                if (!result.TryAdd(path, item))
                {
                    throw new InvalidOperationException(
                        "Task 6 UI prefab hierarchy paths must be unique: " + path);
                }
            }

            return result;
        }

        private static void CopyRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.pivot = source.pivot;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        private static void CopyComponentIfPresent<T>(GameObject source, GameObject target)
            where T : Component
        {
            var sourceComponent = source.GetComponent<T>();
            var targetComponent = target.GetComponent<T>();
            if (sourceComponent == null && targetComponent == null)
            {
                return;
            }

            if (sourceComponent == null || targetComponent == null)
            {
                throw new InvalidOperationException(
                    "Task 6 UI component drift is not safely reconcilable: " + typeof(T).Name);
            }

            EditorUtility.CopySerialized(sourceComponent, targetComponent);
        }

        private static void CopyText(GameObject source, GameObject target)
        {
            var sourceText = source.GetComponent<TMP_Text>();
            var targetText = target.GetComponent<TMP_Text>();
            if (sourceText == null && targetText == null)
            {
                return;
            }

            if (sourceText == null || targetText == null)
            {
                throw new InvalidOperationException("Task 6 TMP hierarchy drift is not reconcilable.");
            }

            EditorUtility.CopySerialized(sourceText, targetText);
            targetText.font = sourceText.font;
            targetText.fontSharedMaterial = sourceText.fontSharedMaterial;
        }

        private static void CopyButton(GameObject source, GameObject target)
        {
            var sourceButton = source.GetComponent<Button>();
            var targetButton = target.GetComponent<Button>();
            if (sourceButton == null && targetButton == null)
            {
                return;
            }

            if (sourceButton == null || targetButton == null)
            {
                throw new InvalidOperationException("Task 6 Button hierarchy drift is not reconcilable.");
            }

            EditorUtility.CopySerialized(sourceButton, targetButton);
            targetButton.targetGraphic = target.GetComponent<Image>();
            targetButton.onClick = new Button.ButtonClickedEvent();
        }

        private static void BindReconciledPrefabReferences(GameObject root)
        {
            var catalogue = root.GetComponent<DecorationCatalogueView>();
            if (catalogue != null)
            {
                AssignObjectReferences(catalogue,
                    ("canvasGroup", root.GetComponent<CanvasGroup>()),
                    ("expandedRoot", root.transform.Find("ExpandedSheet").gameObject),
                    ("collapsedRoot", root.transform.Find("CollapsedHandle").gameObject),
                    ("collapseButton", root.transform.Find("ExpandedSheet/CollapseButton").GetComponent<Button>()),
                    ("collapsedHandleButton", root.transform.Find("CollapsedHandle").GetComponent<Button>()),
                    ("contentRoot", root.transform.Find("ExpandedSheet/Content")),
                    ("tileTemplate", root.transform.Find("ExpandedSheet/Content/TileTemplate")
                        .GetComponent<DecorationCatalogueTileView>()));
                var tile = root.transform.Find("ExpandedSheet/Content/TileTemplate");
                AssignObjectReferences(tile.GetComponent<DecorationCatalogueTileView>(),
                    ("button", tile.GetComponent<Button>()),
                    ("thumbnailImage", tile.Find("Thumbnail").GetComponent<Image>()),
                    ("nameLabel", tile.Find("Name").GetComponent<TMP_Text>()),
                    ("footprintLabel", tile.Find("Footprint").GetComponent<TMP_Text>()),
                    ("warningLabel", tile.Find("WarningLabel").GetComponent<TMP_Text>()),
                    ("warningShape", tile.Find("WarningShape").gameObject));
                return;
            }

            var action = root.GetComponent<DecorationActionBarView>();
            if (action != null)
            {
                var panel = root.transform.Find("ActionPanel");
                var feedback = root.transform.Find("FeedbackToast");
                AssignObjectReferences(action,
                    ("canvasGroup", root.GetComponent<CanvasGroup>()),
                    ("presentationRoot", panel.GetComponent<RectTransform>()),
                    ("storeButton", panel.Find("StoreButton").GetComponent<Button>()),
                    ("rotateButton", panel.Find("RotateButton").GetComponent<Button>()),
                    ("cancelButton", panel.Find("CancelButton").GetComponent<Button>()),
                    ("confirmButton", panel.Find("ConfirmButton").GetComponent<Button>()),
                    ("feedbackLabel", feedback.Find("Message").GetComponent<TMP_Text>()),
                    ("feedbackStateShape", feedback.Find("StateShape").gameObject),
                    ("feedbackRoot", feedback.GetComponent<RectTransform>()),
                    ("feedbackCanvasGroup", feedback.GetComponent<CanvasGroup>()));
                BindActionSemantic(panel.Find("StoreButton"), "Store");
                BindActionSemantic(panel.Find("CancelButton"), "Cancel");
                BindActionSemantic(panel.Find("RotateButton"), "Rotate");
                BindActionSemantic(panel.Find("ConfirmButton"), "Confirm");
                return;
            }

            var modal = root.GetComponent<DecorationStoreModalView>();
            var shared = root.GetComponent<AnimalCafeModalView>();
            var content = root.transform.Find("SafeArea/Content");
            var confirm = content.Find("StoreButton").GetComponent<Button>();
            var cancel = content.Find("CancelButton").GetComponent<Button>();
            var blocker = root.transform.Find("ModalBlocker").GetComponent<Button>();
            shared.BindPrefabReferences(confirm, cancel, blocker, root.GetComponent<CanvasGroup>());
            AssignObjectReferences(modal,
                ("modalView", shared),
                ("confirmButton", confirm),
                ("cancelButton", cancel),
                ("modalBlocker", blocker),
                ("canvasGroup", root.GetComponent<CanvasGroup>()),
                ("titleLabel", content.Find("Title").GetComponent<TMP_Text>()),
                ("bodyLabel", content.Find("Body").GetComponent<TMP_Text>()));
        }

        private static void BindActionSemantic(Transform button, string label)
        {
            var semantic = button.GetComponent<DecorationPointerBoundaryEventHook>();
            var tooltip = button.Find("Tooltip");
            if (semantic == null || tooltip == null)
            {
                throw new InvalidOperationException(
                    button.name + " semantic tooltip hierarchy is incomplete.");
            }

            var serialized = new SerializedObject(semantic);
            serialized.FindProperty("semanticLabel").stringValue = label;
            serialized.FindProperty("tooltipRoot").objectReferenceValue = tooltip.gameObject;
            serialized.FindProperty("tooltipLabel").objectReferenceValue =
                tooltip.Find("Label").GetComponent<TMP_Text>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Dictionary<string, UiIdentity> CaptureExistingUiIdentities(
            IEnumerable<string> paths)
        {
            var result = new Dictionary<string, UiIdentity>(StringComparer.Ordinal);
            foreach (var path in paths)
            {
                var main = AssetDatabase.LoadMainAssetAtPath(path);
                if (main != null)
                {
                    result.Add(path + "|main", UiIdentity.Capture(main));
                }

                if (path == Phase6DecorationAssetPaths.DecorationUiFontPath && main != null)
                {
                    var subassets = AssetDatabase.LoadAllAssetsAtPath(path)
                        .Where(item => item is TMP_FontAsset
                            || item is Material
                            || item is Texture2D).ToArray();
                    for (var index = 0; index < subassets.Length; index++)
                    {
                        var item = subassets[index];
                        result.Add(path + "|sub|" + index + "|" +
                            item.GetType().FullName,
                            UiIdentity.Capture(item));
                    }
                }

                if (main is GameObject root)
                {
                    var view = path == Phase6DecorationAssetPaths.DecorationCataloguePrefabPath
                        ? (Component)root.GetComponent<DecorationCatalogueView>()
                        : path == Phase6DecorationAssetPaths.DecorationActionBarPrefabPath
                            ? root.GetComponent<DecorationActionBarView>()
                            : root.GetComponent<DecorationStoreModalView>();
                    if (view != null)
                    {
                        result.Add(path + "|root-view", UiIdentity.Capture(view));
                    }
                }
            }

            return result;
        }

        private static void ValidatePublishedUiIdentities(
            IReadOnlyDictionary<string, UiIdentity> before)
        {
            var after = CaptureExistingUiIdentities(new[]
            {
                Phase6DecorationAssetPaths.DecorationUiFontPath,
                Phase6DecorationAssetPaths.DecorationCataloguePrefabPath,
                Phase6DecorationAssetPaths.DecorationActionBarPrefabPath,
                Phase6DecorationAssetPaths.DecorationStoreModalPrefabPath
            });
            foreach (var item in before)
            {
                if (!after.TryGetValue(item.Key, out var current)
                    || !current.Equals(item.Value))
                {
                    throw new InvalidOperationException(
                        "Task 6 UI publish changed stable identity at '" + item.Key + "'.");
                }
            }
        }

        private static void ThrowAfterRequestedUiPublishWrite(int writeCount)
        {
            if (UiPublishFaultAfterWriteForTests == writeCount)
            {
                throw new InvalidOperationException(
                    "Injected Task 6 UI publish fault after write " + writeCount + ".");
            }
        }

        private static GameObject CreateUiRoot(string name)
        {
            var root = new GameObject(name, typeof(RectTransform));
            StretchToParent(root.GetComponent<RectTransform>());
            return root;
        }

        private static GameObject CreateRect(string name, Transform parent, Vector2 size)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            child.GetComponent<RectTransform>().sizeDelta = size;
            return child;
        }

        private static Image AddImage(GameObject target, Color color, bool raycastTarget)
        {
            var image = target.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            Vector2 size,
            string label,
            TMP_FontAsset font,
            AnimalCafeUiTheme theme)
        {
            var target = CreateRect(name, parent, size);
            var image = AddImage(target, theme.Colors.Accent, true);
            var button = target.AddComponent<Button>();
            button.targetGraphic = image;
            AddPointerHook(target);
            var text = CreateText(
                "Label", target.transform, font, label,
                theme.Typography.Label.FontSize, size - new Vector2(16f, 8f));
            ApplyTextToken(text, theme.Typography.Label, theme.Colors.Surface);
            return button;
        }

        private static void ApplyTextToken(
            TMP_Text text,
            UiTextStyleToken token,
            Color color)
        {
            text.fontSize = token.FontSize;
            text.fontStyle = token.FontStyle;
            text.lineSpacing = token.LineSpacing;
            text.color = color;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.Normal;
        }

        private static DecorationPointerBoundaryEventHook AddPointerHook(GameObject target)
        {
            return target.AddComponent<DecorationPointerBoundaryEventHook>();
        }

        private static TextMeshProUGUI CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string text,
            float size,
            Vector2 dimensions)
        {
            var target = CreateRect(name, parent, dimensions);
            var label = target.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSharedMaterial = font.material;
            label.text = text;
            label.fontSize = size;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.alignment = TextAlignmentOptions.Midline;
            label.raycastTarget = false;
            label.color = new Color(0.18f, 0.15f, 0.12f, 1f);
            return label;
        }

        private static void SetAnchoredPosition(RectTransform rect, Vector2 position)
        {
            rect.anchoredPosition = position;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void StretchHorizontal(RectTransform rect, float margin, float height)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(-margin * 2f, height);
            rect.anchoredPosition = Vector2.zero;
        }

        private static void AnchorTopRight(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
        }

        private static void AnchorBottomCenter(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
        }

        private static void AnchorLeft(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
        }

        private static void AnchorRight(RectTransform rect, Vector2 offset)
        {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
        }

        private static void StretchHorizontalCentered(
            RectTransform rect,
            float margin,
            float height,
            float y)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(-margin * 2f, height);
            rect.anchoredPosition = new Vector2(0f, y);
        }

        private static void ConfigurePanel(
            GameObject target,
            AnimalCafeUiTheme theme,
            UiPanelStyle style)
        {
            var panel = target.AddComponent<AnimalCafePanelView>();
            panel.Configure(theme, style, new StrongFrostLease(isStrongFrostSupported: false));
        }

        private static void AssignObjectReferences(
            UnityEngine.Object target,
            params (string Name, UnityEngine.Object Value)[] references)
        {
            var serialized = new SerializedObject(target);
            foreach (var reference in references)
            {
                var property = serialized.FindProperty(reference.Name) ??
                    throw new InvalidOperationException(
                        target.GetType().Name + " is missing serialized field '" +
                        reference.Name + "'.");
                property.objectReferenceValue = reference.Value;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private readonly struct UiIdentity : IEquatable<UiIdentity>
        {
            private UiIdentity(string guid, long localId)
            {
                Guid = guid;
                LocalId = localId;
            }

            private string Guid { get; }
            private long LocalId { get; }

            public static UiIdentity Capture(UnityEngine.Object value)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                    value,
                    out string guid,
                    out long localId))
                {
                    throw new InvalidOperationException(
                        "Could not capture Task 6 UI asset identity.");
                }

                return new UiIdentity(guid, localId);
            }

            public bool Equals(UiIdentity other)
            {
                return string.Equals(Guid, other.Guid, StringComparison.Ordinal)
                    && LocalId == other.LocalId;
            }
        }

        private sealed class UiCandidateSet : IDisposable
        {
            public UiCandidateSet(
                TMP_FontAsset font,
                GameObject catalogueRoot,
                GameObject actionBarRoot,
                GameObject storeModalRoot)
            {
                Font = font;
                CatalogueRoot = catalogueRoot;
                ActionBarRoot = actionBarRoot;
                StoreModalRoot = storeModalRoot;
            }

            public TMP_FontAsset Font { get; }
            public GameObject CatalogueRoot { get; }
            public GameObject ActionBarRoot { get; }
            public GameObject StoreModalRoot { get; }
            public IEnumerable<GameObject> Roots
            {
                get
                {
                    yield return CatalogueRoot;
                    yield return ActionBarRoot;
                    yield return StoreModalRoot;
                }
            }

            public void Dispose()
            {
                foreach (var root in Roots)
                {
                    if (root != null)
                    {
                        UnityEngine.Object.DestroyImmediate(root);
                    }
                }

                if (Font != null && !AssetDatabase.Contains(Font))
                {
                    var material = Font.material;
                    var atlas = Font.atlasTextures != null && Font.atlasTextures.Length > 0
                        ? Font.atlasTextures[0]
                        : null;
                    UnityEngine.Object.DestroyImmediate(Font);
                    if (material != null && !AssetDatabase.Contains(material))
                    {
                        UnityEngine.Object.DestroyImmediate(material);
                    }

                    if (atlas != null && !AssetDatabase.Contains(atlas))
                    {
                        UnityEngine.Object.DestroyImmediate(atlas);
                    }
                }
            }
        }

        private sealed class UiPublishBackup : IDisposable
        {
            private const string BackupRoot =
                "Library/AnimalCafe/Phase6Task6BuildBackup";

            private readonly string folder;
            private readonly Dictionary<string, string> backups =
                new Dictionary<string, string>(StringComparer.Ordinal);
            private readonly HashSet<string> absent =
                new HashSet<string>(StringComparer.Ordinal);
            private readonly bool fontFolderExisted;
            private readonly bool prefabFolderExisted;

            private UiPublishBackup(string backupFolder)
            {
                folder = backupFolder;
                fontFolderExisted = AssetDatabase.IsValidFolder(
                    Phase6DecorationAssetPaths.UiFontFolderPath);
                prefabFolderExisted = AssetDatabase.IsValidFolder(
                    Phase6DecorationAssetPaths.UiPrefabFolderPath);
            }

            public static UiPublishBackup Create(IEnumerable<string> assetPaths)
            {
                var backup = new UiPublishBackup(
                    GetAbsoluteProjectPath(BackupRoot + "/" + Guid.NewGuid().ToString("N")));
                try
                {
                    Directory.CreateDirectory(backup.folder);
                    foreach (var assetPath in assetPaths)
                    foreach (var path in new[] { assetPath, assetPath + ".meta" })
                    {
                        var absolute = GetAbsoluteProjectPath(path);
                        if (!File.Exists(absolute))
                        {
                            backup.absent.Add(path);
                            continue;
                        }

                        var destination = Path.Combine(
                            backup.folder,
                            backupsSafeFileName(path));
                        File.Copy(absolute, destination, overwrite: false);
                        backup.backups.Add(path, destination);
                    }

                    return backup;
                }
                catch
                {
                    backup.Dispose();
                    throw;
                }
            }

            public void Restore()
            {
                foreach (var path in absent)
                {
                    var absolute = GetAbsoluteProjectPath(path);
                    if (File.Exists(absolute))
                    {
                        File.Delete(absolute);
                    }
                }

                foreach (var item in backups)
                {
                    File.Copy(item.Value, GetAbsoluteProjectPath(item.Key), overwrite: true);
                }

                foreach (var assetPath in backups.Keys
                    .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)))
                {
                    AssetDatabase.ImportAsset(assetPath,
                        ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                }

                if (!fontFolderExisted)
                {
                    AssetDatabase.DeleteAsset(Phase6DecorationAssetPaths.UiFontFolderPath);
                }

                if (!prefabFolderExisted)
                {
                    AssetDatabase.DeleteAsset(Phase6DecorationAssetPaths.UiPrefabFolderPath);
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            public void Dispose()
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                }
            }

            private static string backupsSafeFileName(string path)
            {
                return path.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
            }
        }

        private static void ValidatePhase4Contracts(
            IReadOnlyList<FurnitureDefinitionAsset> definitions)
        {
            var report = Phase4AssetValidator.ValidateAll(definitions);
            if (report.Issues.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "Phase 6 Counter presets failed Phase 4 asset validation:\n" +
                string.Join("\n", report.Issues.Select(issue =>
                    $"{issue.Code} | {issue.AssetPath} | {issue.Message}")));
        }

        private static void SaveGeneratedAssets(params UnityEngine.Object[] assets)
        {
            foreach (var asset in assets)
            {
                if (asset != null)
                {
                    AssetDatabase.SaveAssetIfDirty(asset);
                }
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            var segments = folderPath.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static string GetAssetIdentity(UnityEngine.Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            return string.IsNullOrEmpty(path) ? $"<unsaved:{asset.name}>" : path;
        }

        private static string GetAbsoluteProjectPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                throw new InvalidOperationException("Could not resolve the Unity project root.");
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static T RequireAsset<T>(string assetPath, string label)
            where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            return asset != null
                ? asset
                : throw new InvalidOperationException($"Missing {label} at '{assetPath}'.");
        }
    }
}

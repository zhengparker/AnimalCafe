using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.Phase7;
using AnimalCafe.UI.Decoration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.Phase8
{
    public static class Phase8AssetBuilder
    {
        private const int ThumbnailSize = 256;

        [MenuItem("Tools/AnimalCafe/Phase 8/Update Pick-up Indicator")]
        public static void UpdatePickUpIndicatorAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before updating the pick-up sign.");
            RequireNoDirtyProjectAssets();
            const string artworkPath = "Assets/UI/P8R/WorldMarkers/pickup_point.png";
            const string materialPath = "Assets/UI/Phase8/Materials/M_PickUpPoint_Indicator.mat";
            var prefabPath = Phase8AssetPaths.PickUpPointIndicatorPrefabPath;
            RequireAsset<GameObject>(prefabPath, "Pick-up Point indicator Prefab");
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? throw new InvalidOperationException("Pick-up sign requires the URP Unlit shader.");
            if (!File.Exists(artworkPath)) throw new InvalidOperationException("Approved pick-up artwork is missing: " + artworkPath);

            // Measure transparent padding without altering the approved PNG.
            // 保留原图，只裁 mesh UV 的透明空白；底部尖角而非整张画布对准 Slot。
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Rect uv;
            float aspect, tipFraction;
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(artworkPath))) throw new InvalidOperationException("Invalid pick-up PNG.");
                var pixels = source.GetPixels32();
                var minX = source.width; var minY = source.height; var maxX = -1; var maxY = -1;
                for (var y = 0; y < source.height; y++) for (var x = 0; x < source.width; x++)
                    if (pixels[y * source.width + x].a >= 32)
                    { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
                if (maxX < 0) throw new InvalidOperationException("Pick-up artwork is transparent.");
                double tipX = 0, weight = 0;
                for (var y = minY; y <= Math.Min(minY + 6, maxY); y++) for (var x = minX; x <= maxX; x++)
                {
                    var alpha = pixels[y * source.width + x].a;
                    if (alpha < 32) continue;
                    tipX += (x + .5) * alpha; weight += alpha;
                }
                minX = Math.Max(0, minX - 2); minY = Math.Max(0, minY - 2);
                maxX = Math.Min(source.width, maxX + 3); maxY = Math.Min(source.height, maxY + 3);
                uv = Rect.MinMaxRect((float)minX / source.width, (float)minY / source.height,
                    (float)maxX / source.width, (float)maxY / source.height);
                aspect = (float)(maxX - minX) / (maxY - minY);
                tipFraction = (float)((tipX / weight - minX) / (maxX - minX));
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }

            AssetDatabase.ImportAsset(artworkPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(artworkPath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput; importer.alphaIsTransparency = true;
            importer.sRGBTexture = true; importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = true; importer.mipmapFilter = TextureImporterMipFilter.BoxFilter;
            importer.mipMapsPreserveCoverage = false; importer.mipMapBias = 0;
            importer.filterMode = FilterMode.Trilinear; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048; importer.isReadable = false;
            importer.SaveAndReimport();
            var artwork = RequireAsset<Texture2D>(artworkPath, "Approved pick-up sign artwork");

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                // Keep the legacy visual-slot name and mesh local ID: drag/selection references stay valid.
                // 兼容原取餐拖动／选择路径；这个节点现在显示彩色牌，不再显示棱锥。
                var visual = root.transform.Find("InvertedSquarePyramid")
                    ?? throw new InvalidOperationException("Pick-up Prefab is missing its visual slot.");
                var mesh = visual.GetComponent<MeshFilter>()?.sharedMesh;
                var renderer = visual.GetComponent<MeshRenderer>();
                if (renderer == null || mesh == null || AssetDatabase.GetAssetPath(mesh) != prefabPath)
                    throw new InvalidOperationException("Pick-up sign requires the existing Prefab mesh subasset.");
                var material = RequireAsset<Material>(materialPath, "Pick-up sign material");
                material.shader = shader;
                material.SetTexture("_BaseMap", artwork); material.SetTextureScale("_BaseMap", Vector2.one);
                material.SetTextureOffset("_BaseMap", Vector2.zero); material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
                material.SetFloat("_AlphaClip", 0); material.SetFloat("_Cull", 0);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON"); material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetShaderPassEnabled("ShadowCaster", false); material.SetShaderPassEnabled("DepthOnly", false);
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                // Use the same validation as the URP inspector, including legacy texture/color aliases.
                // 提前完成 Unity 自动校验，避免下次加载时留下未保存的材质修正。
                BaseShaderGUI.SetMaterialKeywords(material);
                EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);

                const float height = .56f, bottom = .18f;
                var width = height * aspect; var left = -width * tipFraction;
                mesh.Clear();
                mesh.vertices = new[] { new Vector3(left, bottom, 0), new Vector3(left + width, bottom, 0),
                    new Vector3(left + width, bottom + height, 0), new Vector3(left, bottom + height, 0) };
                mesh.uv = new[] { new Vector2(uv.xMin, uv.yMin), new Vector2(uv.xMax, uv.yMin),
                    new Vector2(uv.xMax, uv.yMax), new Vector2(uv.xMin, uv.yMax) };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                EditorUtility.SetDirty(mesh); AssetDatabase.SaveAssetIfDirty(mesh);
                visual.localPosition = Vector3.zero; visual.localRotation = Quaternion.identity; visual.localScale = Vector3.one;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                if (visual.GetComponent<AnimalCafe.UI.P8R.P8RPickUpSignBillboard>() == null)
                    visual.gameObject.AddComponent<AnimalCafe.UI.P8R.P8RPickUpSignBillboard>();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Approved colorful pick-up sign applied; slot, footprint and Prefab identity retained.");
        }

        [MenuItem("Tools/AnimalCafe/Phase 8/Build Assets")]
        public static void BuildAssets()
        {
            RequireNoDirtyProjectAssets();
            EnsureFolder(Phase8AssetPaths.ThumbnailFolder);
            EnsureFolder(Phase8AssetPaths.CatalogueFolder);
            EnsureFolder(Phase8AssetPaths.MaterialFolder);
            var feedbackFont = Phase8FeedbackAssets.EnsureFont();

            foreach (var path in new[]
            {
                Phase8AssetPaths.CataloguePrefabPath,
                Phase8AssetPaths.ActionBarPrefabPath,
                Phase8AssetPaths.FunctionalSurfacePreviewPrefabPath,
                Phase8AssetPaths.PickUpPointIndicatorPrefabPath
            })
            {
                RequireAsset<GameObject>(path, "Task 6/7 Phase 8 UI Prefab");
            }
            EnsureCatalogueCompatibilityControls();
            Phase8FeedbackAssets.BindPrefabFonts(feedbackFont);

            var legacy = RequireAsset<DecorationCatalogueAsset>(
                Phase8AssetPaths.LegacyDecorationCataloguePath,
                "Phase 6 floor-only Decoration catalogue");
            var cashRegister = RequireAsset<FurnitureDefinitionAsset>(
                Phase8AssetPaths.CashRegisterDefinitionPath,
                "Phase 4 Cash Register definition");
            var coffeeMachine = RequireAsset<FurnitureDefinitionAsset>(
                Phase8AssetPaths.CoffeeMachineDefinitionPath,
                "Phase 4 Coffee Machine definition");
            RequireAsset<FurnitureContentCatalog>(
                Phase8AssetPaths.ProductionContentCataloguePath,
                "Phase 6 production Furniture content catalogue");

            BuildThumbnailIfMissing(cashRegister.Prefab,
                Phase8AssetPaths.CashRegisterThumbnailPath);
            BuildThumbnailIfMissing(coffeeMachine.Prefab,
                Phase8AssetPaths.CoffeeMachineThumbnailPath);
            var cashRegisterThumbnail = RequireAsset<Sprite>(
                Phase8AssetPaths.CashRegisterThumbnailPath,
                "Cash Register thumbnail");
            var coffeeMachineThumbnail = RequireAsset<Sprite>(
                Phase8AssetPaths.CoffeeMachineThumbnailPath,
                "Coffee Machine thumbnail");

            var definitions = legacy.Entries.Select(entry =>
                    entry?.Definition ?? throw new InvalidOperationException(
                        "Phase 6 Decoration catalogue contains a null definition."))
                .Concat(new[] { cashRegister, coffeeMachine })
                .ToArray();
            var thumbnails = legacy.Entries.Select(entry =>
                    entry?.Thumbnail ?? throw new InvalidOperationException(
                        "Phase 6 Decoration catalogue contains a null thumbnail."))
                .Concat(new[] { cashRegisterThumbnail, coffeeMachineThumbnail })
                .ToArray();

            var catalogue = AssetDatabase.LoadAssetAtPath<DecorationCatalogueAsset>(
                Phase8AssetPaths.FurnitureCataloguePath);
            if (catalogue == null)
            {
                catalogue = ScriptableObject.CreateInstance<DecorationCatalogueAsset>();
                AssetDatabase.CreateAsset(catalogue,
                    Phase8AssetPaths.FurnitureCataloguePath);
            }

            SetCatalogueEntries(catalogue, definitions, thumbnails);
            EnsureDebugMaterial(
                Phase8AssetPaths.EmployeeAnchorDebugMaterialPath,
                new Color(0.22f, 0.74f, 0.95f, 1f));
            EnsureDebugMaterial(
                Phase8AssetPaths.CustomerAnchorDebugMaterialPath,
                new Color(0.98f, 0.66f, 0.20f, 1f));

            var rows = DecorationCatalogueModelBuilder.BuildFurnitureTab(catalogue);
            var expectedIds = new[] { "furniture", "cash-register", "coffee-machine" };
            if (!rows.Select(row => row.CategoryId).SequenceEqual(expectedIds)
                || rows[0].Items.Count != legacy.Entries.Count
                || rows[1].Items.Count != 1
                || rows[2].Items.Count != 1)
            {
                throw new InvalidOperationException(
                    "Phase 8 mixed catalogue must build exactly Furniture, Cash Register, and Coffee Machine rows.");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("Phase 8 assets built and validated.");
        }

        private static void RequireNoDirtyProjectAssets()
        {
            // Imports and Prefab saves may flush loaded assets; reject before any side effects.
            // 先保护未保存的项目修改；下面各步骤只保存自己负责的目标资源。
            var dirtyAssets = Resources.FindObjectsOfTypeAll<UnityEngine.Object>()
                .Where(asset => asset != null && !(asset is SceneAsset)
                    && EditorUtility.IsPersistent(asset) && EditorUtility.IsDirty(asset))
                .Select(AssetDatabase.GetAssetPath)
                .Where(path => !string.IsNullOrEmpty(path)
                    && path.StartsWith("Assets/", StringComparison.Ordinal) && File.Exists(path))
                .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (dirtyAssets.Length != 0)
                throw new InvalidOperationException(
                    "Loaded assets are dirty. Save or revert these assets before Phase 8 Build Assets: "
                    + string.Join(", ", dirtyAssets));
        }

        private static void EnsureCatalogueCompatibilityControls()
        {
            var root = PrefabUtility.LoadPrefabContents(Phase8AssetPaths.CataloguePrefabPath);
            try
            {
                var catalogue = root.GetComponent<DecorationCatalogueView>()
                    ?? throw new InvalidOperationException(
                        "Phase 8 Catalogue Prefab is missing DecorationCatalogueView.");
                if (catalogue.SurfaceFooterHost == null)
                {
                    throw new InvalidOperationException(
                        "Phase 8 Catalogue Prefab is missing SurfaceFooterHost.");
                }

                var tabs = root.GetComponentsInChildren<DecorationModeTabsView>(true);
                if (tabs.Length != 1)
                {
                    throw new InvalidOperationException(
                        "Phase 8 Catalogue Prefab must contain exactly one DecorationModeTabsView.");
                }

                var ranges = root.GetComponentsInChildren<DecorationFloorRangeView>(true);
                if (ranges.Length > 1)
                {
                    throw new InvalidOperationException(
                        "Phase 8 Catalogue Prefab contains duplicate DecorationFloorRangeView components.");
                }

                var changed = false;
                DecorationFloorRangeView range;
                if (ranges.Length == 0)
                {
                    var rangeObject = new GameObject(
                        "FloorRange", typeof(RectTransform),
                        typeof(DecorationFloorRangeView));
                    rangeObject.transform.SetParent(catalogue.SurfaceFooterHost, false);
                    range = rangeObject.GetComponent<DecorationFloorRangeView>();
                    changed = true;
                }
                else
                {
                    range = ranges[0];
                    if (range.name != "FloorRange")
                    {
                        range.name = "FloorRange";
                        changed = true;
                    }
                    if (range.transform.parent != catalogue.SurfaceFooterHost)
                    {
                        range.transform.SetParent(catalogue.SurfaceFooterHost, false);
                        changed = true;
                    }
                }

                var existingButtons = range.GetComponentsInChildren<Button>(true);
                if (existingButtons.Any(button =>
                        button.name != "WholeRoomButton"
                        && button.name != "SingleGridButton"))
                {
                    throw new InvalidOperationException(
                        "Phase 8 Floor range contains an unexpected Button.");
                }

                var wholeRoom = EnsureRangeButton(
                    range.transform, "WholeRoomButton", ref changed);
                var singleGrid = EnsureRangeButton(
                    range.transform, "SingleGridButton", ref changed);
                var serialized = new SerializedObject(range);
                changed |= SetObjectReference(serialized, "wholeRoomButton", wholeRoom);
                changed |= SetObjectReference(serialized, "singleGridButton", singleGrid);
                changed |= serialized.ApplyModifiedPropertiesWithoutUndo();
                if (changed)
                {
                    LayoutFloorRange(range, wholeRoom, singleGrid);
                    EditorUtility.SetDirty(range);
                    PrefabUtility.SaveAsPrefabAsset(root, Phase8AssetPaths.CataloguePrefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Button EnsureRangeButton(
            Transform parent,
            string name,
            ref bool changed)
        {
            var matches = parent.GetComponentsInChildren<Button>(true)
                .Where(button => button.name == name).ToArray();
            if (matches.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Phase 8 Floor range contains duplicate '{name}' buttons.");
            }
            if (matches.Length == 1)
            {
                return matches[0];
            }

            changed = true;
            return Phase7SurfaceAssetBuilder.CreateButton(parent, name);
        }

        private static void LayoutFloorRange(
            DecorationFloorRangeView range,
            Button wholeRoom,
            Button singleGrid)
        {
            var root = (RectTransform)range.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            root.localScale = Vector3.one;
            var roundedSprite = RequireAsset<Sprite>(
                Phase7AssetPaths.RoundedCatalogueCardSpritePath,
                "Phase 7 rounded catalogue card Sprite");
            var buttons = new[] { wholeRoom, singleGrid };
            for (var index = 0; index < buttons.Length; index++)
            {
                var button = buttons[index];
                Phase7SurfaceAssetBuilder.EnsureButtonLabel(button);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = new Vector2(.5f, 0f);
                rect.anchorMax = new Vector2(.5f, 0f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(-70f + index * 140f, 96f);
                rect.sizeDelta = new Vector2(132f, 52f);
                rect.localScale = Vector3.one;
                button.image.sprite = roundedSprite;
                button.image.type = Image.Type.Sliced;
                button.image.color = Color.white;
                var colors = button.colors;
                colors.normalColor = new Color(1f, .91f, .72f, 1f);
                colors.highlightedColor = new Color(1f, .94f, .80f, 1f);
                colors.pressedColor = new Color(.91f, .80f, .61f, 1f);
                colors.selectedColor = colors.highlightedColor;
                colors.disabledColor = new Color(.28f, .43f, .31f, 1f);
                colors.colorMultiplier = 1f;
                colors.fadeDuration = .1f;
                button.colors = colors;
                var shadow = button.GetComponent<Shadow>()
                    ?? button.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(.12f, .10f, .08f, .28f);
                shadow.effectDistance = new Vector2(0f, -4f);
                shadow.useGraphicAlpha = true;
            }
        }

        private static bool SetObjectReference(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName)
                ?? throw new InvalidOperationException(
                    $"Missing serialized property '{propertyName}'.");
            if (property.objectReferenceValue == value)
            {
                return false;
            }
            property.objectReferenceValue = value;
            return true;
        }

        private static void SetCatalogueEntries(
            DecorationCatalogueAsset catalogue,
            IReadOnlyList<FurnitureDefinitionAsset> definitions,
            IReadOnlyList<Sprite> thumbnails)
        {
            if (definitions.Count != thumbnails.Count)
            {
                throw new ArgumentException(
                    "Definitions and thumbnails must have matching counts.");
            }

            var serialized = new SerializedObject(catalogue);
            var entries = serialized.FindProperty("entries");
            entries.arraySize = definitions.Count;
            for (var index = 0; index < definitions.Count; index++)
            {
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("definition").objectReferenceValue =
                    definitions[index];
                entry.FindPropertyRelative("thumbnail").objectReferenceValue =
                    thumbnails[index];
            }

            if (serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                EditorUtility.SetDirty(catalogue);
                AssetDatabase.SaveAssetIfDirty(catalogue);
            }
        }

        private static void BuildThumbnailIfMissing(GameObject prefab, string assetPath)
        {
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    $"Production definition for '{assetPath}' is missing its Prefab.");
            }

            if (AssetDatabase.LoadAssetAtPath<Sprite>(assetPath) == null)
            {
                using var renderer = new ThumbnailRenderer();
                renderer.Build(prefab, assetPath);
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter
                ?? throw new InvalidOperationException(
                    $"Expected TextureImporter at '{assetPath}'.");
            var changed = importer.textureType != TextureImporterType.Sprite
                || importer.spriteImportMode != SpriteImportMode.Single
                || !importer.alphaIsTransparency
                || importer.mipmapEnabled
                || importer.textureCompression != TextureImporterCompression.Uncompressed
                || importer.wrapMode != TextureWrapMode.Clamp
                || importer.maxTextureSize != ThumbnailSize;
            if (!changed)
            {
                return;
            }

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

        private static void EnsureDebugMaterial(string path, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard")
                    ?? throw new InvalidOperationException(
                        "No supported shader is available for anchor debug materials.");
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }

            var changed = false;
            if (material.HasProperty("_BaseColor")
                && material.GetColor("_BaseColor") != color)
            {
                material.SetColor("_BaseColor", color);
                changed = true;
            }
            if (material.HasProperty("_Color") && material.GetColor("_Color") != color)
            {
                material.SetColor("_Color", color);
                changed = true;
            }
            if (changed)
            {
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException($"Invalid Asset folder path '{path}'.");
            }
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static T RequireAsset<T>(string path, string label) where T : UnityEngine.Object
        {
            return AssetDatabase.LoadAssetAtPath<T>(path)
                ?? throw new InvalidOperationException($"Missing {label} at '{path}'.");
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
                    ThumbnailSize, ThumbnailSize, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear,
                    name = "Phase8FurnitureThumbnailTarget"
                };
                texture = new Texture2D(
                    ThumbnailSize, ThumbnailSize, TextureFormat.RGBA32, false, false);
                cameraObject = new GameObject("Phase8ThumbnailCamera")
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

                lightObject = new GameObject("Phase8ThumbnailLight")
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
                var previous = RenderTexture.active;
                try
                {
                    instance = PrefabUtility.InstantiatePrefab(prefab, previewScene) as GameObject
                        ?? throw new InvalidOperationException(
                            $"Could not instantiate production Prefab '{AssetDatabase.GetAssetPath(prefab)}'.");
                    instance.hideFlags = HideFlags.HideAndDontSave;
                    var bounds = GetVisibleBounds(instance);
                    cameraObject.transform.rotation = Quaternion.Euler(28f, 135f, 0f);
                    cameraObject.transform.position = bounds.center
                        - cameraObject.transform.forward * (bounds.extents.magnitude + 5f);
                    camera.orthographicSize =
                        CalculateOrthographicSize(camera, bounds) * 1.18f;
                    camera.Render();
                    RenderTexture.active = renderTexture;
                    texture.ReadPixels(
                        new Rect(0f, 0f, ThumbnailSize, ThumbnailSize), 0, 0, false);
                    texture.Apply(false, false);
                    File.WriteAllBytes(GetAbsoluteProjectPath(assetPath), texture.EncodeToPNG());
                }
                finally
                {
                    RenderTexture.active = previous;
                    if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                }

                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            public void Dispose()
            {
                if (cameraObject != null) UnityEngine.Object.DestroyImmediate(cameraObject);
                if (lightObject != null) UnityEngine.Object.DestroyImmediate(lightObject);
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                UnityEngine.Object.DestroyImmediate(texture);
                EditorSceneManager.ClosePreviewScene(previewScene);
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
                    $"Production Prefab '{root.name}' has no visible Renderer.");
            }

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
            return bounds;
        }

        private static float CalculateOrthographicSize(
            UnityEngine.Camera camera,
            Bounds bounds)
        {
            var maximumHorizontal = 0f;
            var maximumVertical = 0f;
            foreach (var corner in GetBoundsCorners(bounds))
            {
                var local = camera.transform.InverseTransformPoint(corner);
                maximumHorizontal = Mathf.Max(maximumHorizontal, Mathf.Abs(local.x));
                maximumVertical = Mathf.Max(maximumVertical, Mathf.Abs(local.y));
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
                    bounds.extents, new Vector3(x, y, z));
            }
        }

        private static string GetAbsoluteProjectPath(string assetPath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Could not resolve Unity project root.");
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}

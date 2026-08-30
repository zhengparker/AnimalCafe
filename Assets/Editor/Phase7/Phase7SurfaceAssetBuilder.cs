using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.UI.Decoration;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.Phase7
{
    public static class Phase7SurfaceAssetBuilder
    {
        private static readonly (string Id, string Name, SurfaceStyleKind Kind, string Material)[] Styles =
        {
            ("floor.dark-stone", "Dark Stone", SurfaceStyleKind.Floor, "M_Floor_DarkStone_03"),
            ("floor.light-tile", "Light Tile", SurfaceStyleKind.Floor, "M_Floor_LightTile_02"),
            ("floor.warm-wood", "Warm Wood", SurfaceStyleKind.Floor, "M_Floor_WarmWood_01"),
            ("paint.cream", "Cream", SurfaceStyleKind.Paint, "M_Paint_Cream_01"),
            ("paint.sage", "Sage", SurfaceStyleKind.Paint, "M_Paint_Sage_02"),
            ("paint.terracotta", "Terracotta", SurfaceStyleKind.Paint, "M_Paint_Terracotta_03"),
            ("wainscoting.sage-plain", "Sage Plain", SurfaceStyleKind.Wainscoting, "M_Wainscoting_SagePlain_02"),
            ("wainscoting.warm-white-rail", "Warm White Rail", SurfaceStyleKind.Wainscoting, "M_Wainscoting_WarmWhiteRail_01"),
            ("wallpaper.cream-floral", "Cream Floral", SurfaceStyleKind.Wallpaper, "M_Wallpaper_CreamFloral_01"),
            ("wallpaper.sage-sprig", "Sage Sprig", SurfaceStyleKind.Wallpaper, "M_Wallpaper_SageSprig_02")
        };

        [MenuItem("AnimalCafe/Phase 7/Build Or Update Assets")]
        public static void BuildOrUpdateAssets()
        {
            var dependencyPaths=new[]{"Assets/Settings/UniversalRenderPipelineGlobalSettings.asset","Assets/Art/Phase4/Environment/Materials/M_Environment_Entrance_01.mat"};
            var dependencySnapshots=dependencyPaths.ToDictionary(path=>path,path=>File.ReadAllBytes(path),StringComparer.Ordinal);
            try
            {
            foreach (var folder in new[] { Phase7AssetPaths.Root, Phase7AssetPaths.TextureFolder,
                         Phase7AssetPaths.MaterialFolder, Phase7AssetPaths.DefinitionFolder,
                         Phase7AssetPaths.CatalogueFolder, Phase7AssetPaths.PlaceholderPrefabFolder,
                         Phase7AssetPaths.UiRoot, Phase7AssetPaths.ThumbnailFolder,
                         Phase7AssetPaths.UiPrefabFolder }) EnsureFolder(folder);

            BuildFloorTexturesAndMaterials();
            BuildWallFinishMaterials();
            var definitions = Styles.Select(BuildStyle).ToArray();
            var none = EnsureAsset<SurfaceStyleDefinitionAsset>(Phase7AssetPaths.WainscotingNoneDefinitionPath);
            Set(none, "styleId", "wainscoting.none"); Set(none, "displayName", "None");
            Set(none, "kind", (int)SurfaceStyleKind.Wainscoting); Set(none, "isNoneOption", true);
            Set(none,"worldTileWidthMeters",1f);Set(none,"worldTileHeightMeters",CharacterScaleReference.SharedCharacterWaistHeightMeters);Set(none,"verticalMapping",(int)SurfaceStyleVerticalMapping.WaistReference);
            Set(none, "material", null); Set(none, "thumbnail", BuildThumbnail("wainscoting.none", new Color32(235,231,219,255), true));

            BuildCatalogue(Phase7AssetPaths.PaintCataloguePath, SurfaceStyleKind.Paint,
                definitions.Where(x => x.Kind == SurfaceStyleKind.Paint));
            BuildCatalogue(Phase7AssetPaths.WallpaperCataloguePath, SurfaceStyleKind.Wallpaper,
                definitions.Where(x => x.Kind == SurfaceStyleKind.Wallpaper));
            BuildCatalogue(Phase7AssetPaths.WainscotingCataloguePath, SurfaceStyleKind.Wainscoting,
                definitions.Where(x => x.Kind == SurfaceStyleKind.Wainscoting).Append(none));
            BuildCatalogue(Phase7AssetPaths.FloorCataloguePath, SurfaceStyleKind.Floor,
                definitions.Where(x => x.Kind == SurfaceStyleKind.Floor));
            Phase7FormalAssetIntake.Build();
            BuildProjectionMaterials();
            BuildLayeredWallMaterial();
            BuildOcclusionFadeMaterial();
            BuildUiPrefabs();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                foreach(var path in dependencyPaths)
                    if(!File.ReadAllBytes(path).SequenceEqual(dependencySnapshots[path]))File.WriteAllBytes(path,dependencySnapshots[path]);
            }
        }

        private static SurfaceStyleDefinitionAsset BuildStyle((string Id, string Name, SurfaceStyleKind Kind, string Material) item)
        {
            var path = $"{Phase7AssetPaths.DefinitionFolder}/SS_{item.Id.Replace('.', '_').Replace('-', '_')}.asset";
            var asset = EnsureAsset<SurfaceStyleDefinitionAsset>(path);
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{Phase7AssetPaths.MaterialFolder}/{item.Material}.mat");
            if (material == null) throw new InvalidOperationException("Missing Phase 7 material: " + item.Material);
            Set(asset, "styleId", item.Id); Set(asset, "displayName", item.Name); Set(asset, "kind", (int)item.Kind);
            var width=item.Kind==SurfaceStyleKind.Paint?0f:1f;
            var height=item.Kind==SurfaceStyleKind.Floor?1f:item.Kind==SurfaceStyleKind.Wainscoting?CharacterScaleReference.SharedCharacterWaistHeightMeters:0f;
            var mapping=item.Kind==SurfaceStyleKind.Floor?SurfaceStyleVerticalMapping.OneGrid:item.Kind==SurfaceStyleKind.Wallpaper?SurfaceStyleVerticalMapping.FullWall:item.Kind==SurfaceStyleKind.Wainscoting?SurfaceStyleVerticalMapping.WaistReference:SurfaceStyleVerticalMapping.NotApplicable;
            Set(asset,"worldTileWidthMeters",width);Set(asset,"worldTileHeightMeters",height);Set(asset,"verticalMapping",(int)mapping);
            var swatchColor=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):
                material.HasProperty("_Color")?material.GetColor("_Color"):Color.white;
            Set(asset, "material", material); Set(asset, "thumbnail", BuildStyleThumbnail(item.Id, material, swatchColor)); Set(asset, "isNoneOption", false);
            return asset;
        }

        private static void BuildFloorTexturesAndMaterials()
        {
            var data = new[] { ("WarmWood_01", new Color32(190,133,81,255)), ("LightTile_02", new Color32(220,211,184,255)), ("DarkStone_03", new Color32(75,78,77,255)) };
            foreach (var item in data)
            {
                var texturePath = $"{Phase7AssetPaths.TextureFolder}/T_Floor_{item.Item1}.png";
                WritePattern(texturePath, item.Item2, false);
                var texture = (Texture)ImportSpriteOrTexture(texturePath, false);
                var matPath = $"{Phase7AssetPaths.MaterialFolder}/M_Floor_{item.Item1}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) { mat = new Material(Shader.Find("AnimalCafe/Phase7/FloorSurfaceTiled") ?? Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, matPath); }
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture); else mat.mainTexture = texture;
                mat.color = Color.white; EditorUtility.SetDirty(mat);AssetDatabase.SaveAssetIfDirty(mat);
            }
        }

        private static void BuildWallFinishMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit")
                ?? throw new InvalidOperationException("URP/Lit Shader is required for dimensional Wall finishes.");
            Texture2D paintNormal = null;
            foreach (var item in Styles.Where(style => style.Kind == SurfaceStyleKind.Paint
                                                        || style.Kind == SurfaceStyleKind.Wallpaper
                                                        || style.Kind == SurfaceStyleKind.Wainscoting))
            {
                var materialPath = $"{Phase7AssetPaths.MaterialFolder}/{item.Material}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath)
                    ?? throw new InvalidOperationException("Missing Phase 7 Wall finish Material: " + materialPath);
                material.shader = lit;

                Texture2D normal;
                if (item.Kind == SurfaceStyleKind.Paint)
                {
                    paintNormal ??= BuildTileableNormalTexture(
                        $"{Phase7AssetPaths.TextureFolder}/T_WallNormal_PaintFine_01.png",
                        null,
                        2.25f,
                        .045f);
                    normal = paintNormal;
                }
                else
                {
                    var source = material.GetTexture("_BaseMap") as Texture2D;
                    var safeId = item.Id.Replace('.', '_').Replace('-', '_');
                    normal = BuildTileableNormalTexture(
                        $"{Phase7AssetPaths.TextureFolder}/T_WallNormal_{safeId}.png",
                        source,
                        item.Kind == SurfaceStyleKind.Wainscoting ? 7.5f : 4.25f,
                        item.Kind == SurfaceStyleKind.Wainscoting ? 0f : .065f);
                }

                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", item.Kind == SurfaceStyleKind.Paint
                    ? .34f
                    : item.Kind == SurfaceStyleKind.Wainscoting ? .22f : .52f);
                if (item.Kind == SurfaceStyleKind.Wainscoting)
                    material.SetFloat("_Parallax", .05f);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", item.Kind == SurfaceStyleKind.Paint
                    ? .17f
                    : item.Kind == SurfaceStyleKind.Wainscoting ? .24f : .10f);
                material.SetFloat("_ReceiveShadows", 1f);
                material.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
            }

            BuildArchitecturalWallMaterial(
                Phase7AssetPaths.WallBodyMaterialPath,
                lit,
                new Color32(224, 213, 187, 255),
                paintNormal,
                .22f,
                .28f);
            BuildArchitecturalWallMaterial(
                Phase7AssetPaths.WallCornerMaterialPath,
                lit,
                new Color32(177, 161, 132, 255),
                paintNormal,
                .18f,
                .36f);
        }

        private static void BuildArchitecturalWallMaterial(
            string path,
            Shader shader,
            Color color,
            Texture normal,
            float smoothness,
            float normalStrength)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", normalStrength);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_ReceiveShadows", 1f);
            material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }

        private static Texture2D BuildTileableNormalTexture(
            string path,
            Texture2D sourceTexture,
            float slopeStrength,
            float proceduralContribution)
        {
            const int size = 128;
            Texture2D source = null;
            var heights = new float[size * size];
            try
            {
                if (sourceTexture != null)
                {
                    var sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
                    source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (string.IsNullOrEmpty(sourcePath) || !source.LoadImage(File.ReadAllBytes(sourcePath)))
                        throw new InvalidOperationException("Unable to read Wall finish texture: " + sourcePath);
                }

                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var authored = source == null
                        ? .5f
                        : source.GetPixelBilinear((x + .5f) / size, (y + .5f) / size).grayscale;
                    var fine = Mathf.Sin(x * Mathf.PI / 4f) * Mathf.Sin(y * Mathf.PI / 4f);
                    var broad = Mathf.Cos(x * Mathf.PI / 16f) * Mathf.Sin(y * Mathf.PI / 16f);
                    heights[y * size + x] = authored
                        + (fine * .65f + broad * .35f) * proceduralContribution;
                }

                var normalTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var left = heights[y * size + (x + size - 1) % size];
                    var right = heights[y * size + (x + 1) % size];
                    var down = heights[((y + size - 1) % size) * size + x];
                    var up = heights[((y + 1) % size) * size + x];
                    var normal = new Vector3((left - right) * slopeStrength,
                        (down - up) * slopeStrength, 1f).normalized;
                    pixels[y * size + x] = new Color(normal.x * .5f + .5f,
                        normal.y * .5f + .5f, normal.z * .5f + .5f, 1f);
                }

                normalTexture.SetPixels(pixels);
                normalTexture.Apply();
                var bytes = normalTexture.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(normalTexture);
                if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
                    File.WriteAllBytes(path, bytes);
                AssetDatabase.ImportAsset(path,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.sRGBTexture = false;
                importer.SaveAndReimport();
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            finally
            {
                if (source != null) UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static Sprite BuildThumbnail(string id, Color color, bool crossed)
        {
            var path = $"{Phase7AssetPaths.ThumbnailFolder}/TH_{id.Replace('.', '_').Replace('-', '_')}.png";
            WritePattern(path, color, crossed);
            return ImportSpriteOrTexture(path, true) as Sprite;
        }

        private static Sprite BuildStyleThumbnail(string id, Material material, Color fallbackColor)
        {
            var sourceTexture = material.HasProperty("_BaseMap")
                ? material.GetTexture("_BaseMap")
                : material.mainTexture;
            var sourcePath = AssetDatabase.GetAssetPath(sourceTexture);
            if (sourceTexture == null || string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                return BuildThumbnail(id, fallbackColor, false);
            }

            var thumbnailPath = $"{Phase7AssetPaths.ThumbnailFolder}/TH_{id.Replace('.', '_').Replace('-', '_')}.png";
            var bytes = BuildPreviewBytes(File.ReadAllBytes(sourcePath), 256);
            if (!File.Exists(thumbnailPath) || !File.ReadAllBytes(thumbnailPath).SequenceEqual(bytes))
            {
                File.WriteAllBytes(thumbnailPath, bytes);
            }

            return ImportSpriteOrTexture(thumbnailPath, true) as Sprite;
        }

        private static byte[] BuildPreviewBytes(byte[] sourceBytes, int maximumSize)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Texture2D preview = null;
            try
            {
                if (!source.LoadImage(sourceBytes))
                {
                    throw new InvalidOperationException("Unable to read authored surface texture for UI preview.");
                }

                var scale = Mathf.Min(1f, maximumSize / (float)Mathf.Max(source.width, source.height));
                var width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
                var height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
                preview = new Texture2D(width, height, TextureFormat.RGBA32, false);
                var pixels = new Color[width * height];
                for (var y = 0; y < height; y++)
                for (var x = 0; x < width; x++)
                    pixels[y * width + x] = source.GetPixelBilinear(
                        (x + .5f) / width,
                        (y + .5f) / height);
                preview.SetPixels(pixels);
                preview.Apply();
                return preview.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                if (preview != null) UnityEngine.Object.DestroyImmediate(preview);
            }
        }

        private static void WritePattern(string path, Color color, bool crossed)
        {
            const int size = 64; var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = Enumerable.Repeat(color, size * size).ToArray();
            for (var y=0; y<size; y++) for (var x=0; x<size; x++)
            {
                if ((x % 16 == 0 || y % 16 == 0) && !crossed) pixels[y*size+x] = Color.Lerp(color, Color.black, .12f);
                var dx=x-(size-1)*.5f;var dy=y-(size-1)*.5f;var radius=Math.Sqrt(dx*dx+dy*dy);
                if (crossed && (Math.Abs(x-y)<3 || Math.Abs((size-1-x)-y)<3 || Math.Abs(radius-22f)<2.5f)) pixels[y*size+x] = new Color(.55f,.2f,.18f,1f);
            }
            tex.SetPixels(pixels); tex.Apply(); var bytes = tex.EncodeToPNG(); UnityEngine.Object.DestroyImmediate(tex);
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes)) File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        private static UnityEngine.Object ImportSpriteOrTexture(string path, bool sprite)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
            if(sprite) importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            return sprite ? AssetDatabase.LoadAssetAtPath<Sprite>(path) : AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void BuildCatalogue(string path, SurfaceStyleKind kind, IEnumerable<SurfaceStyleDefinitionAsset> entries)
        {
            var asset = EnsureAsset<SurfaceStyleCatalogueAsset>(path); Set(asset, "kind", (int)kind);
            SetArray(asset, "entries", entries.OrderBy(x => x.StyleId, StringComparer.Ordinal).Cast<UnityEngine.Object>().ToArray());
        }

        private static void BuildMountedPlaceholders()
        {
            var definitions = new List<WallMountedDefinitionAsset>();
            var sizes = new[] { ("wall-decor.placeholder.1x1",1,1), ("wall-decor.placeholder.1x2",1,2), ("wall-decor.placeholder.2x1",2,1) };
            foreach (var item in sizes)
            {
                var suffix=item.Item1.Split('.').Last(); var prefabPath=$"{Phase7AssetPaths.PlaceholderPrefabFolder}/PF_PLACEHOLDER_WallDecor_{suffix}.prefab";
                var placeholderTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(
                    $"{Phase7AssetPaths.TextureFolder}/T_Floor_WarmWood_01.png");
                var placeholderMaterial=EnsurePlaceholderMaterial(placeholderTexture);
                var prefab=BuildPlaceholderPrefab(prefabPath,item.Item2,item.Item3,placeholderMaterial);
                var def=EnsureAsset<WallMountedDefinitionAsset>($"{Phase7AssetPaths.DefinitionFolder}/WD_PLACEHOLDER_{suffix}.asset");
                Set(def,"definitionId",item.Item1); Set(def,"displayName","Placeholder " + suffix); Set(def,"footprintWidth",item.Item2); Set(def,"footprintHeight",item.Item3);
                Set(def,"prefab",prefab); Set(def,"thumbnail",BuildThumbnail(item.Item1,new Color32(181,132,91,255),false)); Set(def,"maxVisualDepth",.18f); definitions.Add(def);
            }
            var catalogue=EnsureAsset<WallMountedCatalogueAsset>(Phase7AssetPaths.WallMountedProductionCataloguePath); Set(catalogue,"kind",(int)WallMountedCatalogueKind.WallDecor); SetArray(catalogue,"entries",definitions.Cast<UnityEngine.Object>().ToArray());

            var window=EnsureAsset<WallMountedDefinitionAsset>($"{Phase7AssetPaths.DefinitionFolder}/WD_Window_Canonical.asset");
            Set(window,"definitionId","window.canonical.phase4"); Set(window,"displayName","Window"); Set(window,"footprintWidth",2); Set(window,"footprintHeight",1);
            Set(window,"prefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase4/Prefabs/PF_Wall_Window_01.prefab")); Set(window,"thumbnail",BuildThumbnail("window.canonical.phase4",new Color32(145,190,202,255),false)); Set(window,"maxVisualDepth",.2f);
            var windows=EnsureAsset<WallMountedCatalogueAsset>(Phase7AssetPaths.WindowCataloguePath); Set(windows,"kind",(int)WallMountedCatalogueKind.Windows); SetArray(windows,"entries",new UnityEngine.Object[]{window});
        }

        private static Material EnsurePlaceholderMaterial(Texture texture)
        {
            var path=$"{Phase7AssetPaths.MaterialFolder}/M_PLACEHOLDER_WallDecor.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.SetTexture("_BaseMap",texture); material.SetColor("_BaseColor",new Color(.71f,.52f,.36f,1f));EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);return material;
        }

        private static GameObject BuildPlaceholderPrefab(string path, int width, int height, Material material)
        {
            var root=new GameObject(Path.GetFileNameWithoutExtension(path));
            try { var visual=GameObject.CreatePrimitive(PrimitiveType.Cube); visual.name="PLACEHOLDER_VISUAL_NOT_FORMAL_ASSET"; visual.transform.SetParent(root.transform,false); visual.transform.localPosition=new Vector3(0,height*.45f,0f); visual.transform.localScale=new Vector3(width*.9f,height*.9f,.16f); visual.GetComponent<Renderer>().sharedMaterial=material; var c=visual.GetComponent<Collider>(); c.isTrigger=true; return PrefabUtility.SaveAsPrefabAsset(root,path); }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BuildUiPrefabs()
        {
            CloneUiPrefab("Assets/UI/Phase6/Prefabs/PF_UI_DecorationCatalogue.prefab",Phase7AssetPaths.CataloguePrefabPath);
            CloneUiPrefab("Assets/UI/Phase6/Prefabs/PF_UI_DecorationActionBar.prefab",Phase7AssetPaths.ActionBarPrefabPath);
            ReconcileCataloguePrefab();
            ReconcileActionBarPrefab();
            BuildExitModalPrefab();
        }
        private static void BuildLayeredWallMaterial()
        {
            var shader=Shader.Find("AnimalCafe/Phase7/WallSurfaceLayered")??throw new InvalidOperationException("Missing layered wall Shader.");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Phase7AssetPaths.LayeredWallMaterialPath);
            if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,Phase7AssetPaths.LayeredWallMaterialPath);}
            material.shader=shader;material.SetColor("_BaseColor",Color.white);material.SetColor("_WainscotingColor",Color.white);material.SetFloat("_WainscotingEnabled",0f);material.SetFloat("_WainscotingCutoff",CharacterScaleReference.GetNormalizedWainscotingCutoff(2f));material.SetVector("_WallpaperTiling",new Vector4(8f,1f,0f,0f));EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
        }

        private static void BuildOcclusionFadeMaterial()
        {
            var shader = Shader.Find("AnimalCafe/Phase7/OcclusionFadeDither")
                ?? throw new InvalidOperationException("Missing occlusion fade Shader.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Phase7AssetPaths.OcclusionFadeMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, Phase7AssetPaths.OcclusionFadeMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }

        private static void ReconcileCataloguePrefab()
        {
            var roundedCardSprite = BuildRoundedCatalogueCardSprite();
            var roundedPanelSprite = BuildRoundedSprite(
                Phase7AssetPaths.RoundedCataloguePanelSpritePath, 64, 20f);
            var previewOutlineSprite = BuildRoundedOutlineSprite(
                Phase7AssetPaths.RoundedCataloguePreviewOutlineSpritePath, 64, 12f, 5f);
            var root=PrefabUtility.LoadPrefabContents(Phase7AssetPaths.CataloguePrefabPath);
            try
            {
                var view=root.GetComponentInChildren<DecorationCatalogueView>(true)??throw new InvalidOperationException("Phase7 Catalogue prefab requires DecorationCatalogueView.");var viewSo=new SerializedObject(view);var expanded=(GameObject)viewSo.FindProperty("expandedRoot").objectReferenceValue;var collapsed=(GameObject)viewSo.FindProperty("collapsedRoot").objectReferenceValue;var collapsedRect=(RectTransform)collapsed.transform;collapsedRect.anchoredPosition=new Vector2(collapsedRect.anchoredPosition.x,668f);collapsedRect.sizeDelta=new Vector2(240f,48f);
                var expandedImage=expanded.GetComponent<Image>();expandedImage.sprite=roundedPanelSprite;expandedImage.type=Image.Type.Sliced;
                var existingHost=view.GetComponentsInChildren<Transform>(true).FirstOrDefault(item=>item.name=="Phase7CategoryCatalogue");if(existingHost!=null&&existingHost.parent!=expanded.transform)existingHost.SetParent(expanded.transform,false);
                var host=EnsureRect(expanded.transform,"Phase7CategoryCatalogue",new Vector2(0f,0f),new Vector2(0f,520f));Stretch(host);host.offsetMin=new Vector2(40f,160f);host.offsetMax=new Vector2(-40f,-96f);
                var verticalObject=EnsureObject(host,"VerticalScroll",typeof(RectTransform),typeof(Image),typeof(ScrollRect));var verticalImage=verticalObject.GetComponent<Image>();verticalImage.color=new Color(1f,.97f,.9f,0f);verticalImage.raycastTarget=true;var vertical=verticalObject.GetComponent<ScrollRect>();vertical.horizontal=false;vertical.vertical=true;
                Stretch((RectTransform)verticalObject.transform);var viewport=EnsureRect(verticalObject.transform,"Viewport",Vector2.zero,Vector2.zero);Stretch(viewport);if(viewport.GetComponent<RectMask2D>()==null)viewport.gameObject.AddComponent<RectMask2D>();var categoryContent=EnsureRect(viewport,"CategoryContent",Vector2.zero,new Vector2(0f,480f));categoryContent.anchorMin=new Vector2(0f,1f);categoryContent.anchorMax=new Vector2(1f,1f);categoryContent.pivot=new Vector2(.5f,1f);var verticalLayout=categoryContent.GetComponent<VerticalLayoutGroup>()??categoryContent.gameObject.AddComponent<VerticalLayoutGroup>();verticalLayout.padding=new RectOffset(0,0,0,0);verticalLayout.spacing=8f;verticalLayout.childAlignment=TextAnchor.UpperLeft;verticalLayout.childForceExpandWidth=true;verticalLayout.childForceExpandHeight=false;verticalLayout.childControlWidth=true;verticalLayout.childControlHeight=true;var verticalFitter=categoryContent.GetComponent<ContentSizeFitter>()??categoryContent.gameObject.AddComponent<ContentSizeFitter>();verticalFitter.horizontalFit=ContentSizeFitter.FitMode.Unconstrained;verticalFitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
                vertical.viewport=viewport;vertical.content=categoryContent;
                var templates=EnsureRect(host,"Templates",Vector2.zero,Vector2.zero);
                var row=EnsureObject(templates,"CategoryRowTemplate",typeof(RectTransform),typeof(ScrollRect),typeof(LayoutElement));var rowRect=(RectTransform)row.transform;rowRect.sizeDelta=new Vector2(900f,168f);var rowLayout=row.GetComponent<LayoutElement>();rowLayout.preferredHeight=168f;rowLayout.minHeight=168f;
                var rowLabel=EnsureText(row.transform,"CategoryLabel","Category");var rowLabelRect=(RectTransform)rowLabel.transform;rowLabelRect.anchorMin=new Vector2(0f,1f);rowLabelRect.anchorMax=Vector2.one;rowLabelRect.pivot=new Vector2(.5f,1f);rowLabelRect.offsetMin=new Vector2(0f,-32f);rowLabelRect.offsetMax=Vector2.zero;rowLabel.alignment=TextAlignmentOptions.MidlineLeft;rowLabel.fontSize=32f;rowLabel.fontStyle=FontStyles.Bold;rowLabel.color=new Color(.22f,.16f,.11f,1f);
                var rowViewport=EnsureRect(row.transform,"Viewport",Vector2.zero,new Vector2(900f,136f));rowViewport.anchorMin=Vector2.zero;rowViewport.anchorMax=Vector2.one;rowViewport.offsetMin=Vector2.zero;rowViewport.offsetMax=new Vector2(0f,-32f);var rowContent=EnsureRect(rowViewport,"Content",Vector2.zero,new Vector2(0f,128f));rowContent.anchorMin=new Vector2(0f,.5f);rowContent.anchorMax=new Vector2(0f,.5f);rowContent.pivot=new Vector2(0f,.5f);rowContent.anchoredPosition=Vector2.zero;var horizontal=rowContent.GetComponent<HorizontalLayoutGroup>()??rowContent.gameObject.AddComponent<HorizontalLayoutGroup>();horizontal.spacing=8f;horizontal.childAlignment=TextAnchor.MiddleLeft;horizontal.childForceExpandWidth=false;horizontal.childForceExpandHeight=false;horizontal.childControlWidth=true;horizontal.childControlHeight=true;var fitter=rowContent.GetComponent<ContentSizeFitter>()??rowContent.gameObject.AddComponent<ContentSizeFitter>();fitter.horizontalFit=ContentSizeFitter.FitMode.PreferredSize;fitter.verticalFit=ContentSizeFitter.FitMode.Unconstrained;
                var rowScroll=row.GetComponent<ScrollRect>();rowScroll.horizontal=true;rowScroll.vertical=false;rowScroll.viewport=rowViewport;rowScroll.content=rowContent;row.SetActive(false);
                var tileObject=EnsureObject(templates,"CategoryTileTemplate",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button),typeof(DecorationCatalogueTileView),typeof(LayoutElement));var tileRect=(RectTransform)tileObject.transform;tileRect.sizeDelta=new Vector2(128f,128f);var tileLayout=tileObject.GetComponent<LayoutElement>();tileLayout.preferredWidth=128f;tileLayout.preferredHeight=128f;tileLayout.minWidth=128f;tileLayout.minHeight=128f;var tileImage=tileObject.GetComponent<Image>();tileImage.sprite=roundedCardSprite;tileImage.type=Image.Type.Sliced;tileImage.color=new Color(1f,.91f,.72f,1f);
                var thumbnail=EnsureImage(tileObject.transform,"Thumbnail");var thumbnailRect=(RectTransform)thumbnail.transform;thumbnailRect.anchorMin=new Vector2(0f,.22f);thumbnailRect.anchorMax=Vector2.one;thumbnailRect.offsetMin=new Vector2(6f,4f);thumbnailRect.offsetMax=new Vector2(-6f,-6f);thumbnail.preserveAspect=true;
                var name=EnsureText(tileObject.transform,"Name","Item");var nameRect=(RectTransform)name.transform;nameRect.anchorMin=Vector2.zero;nameRect.anchorMax=new Vector2(1f,.22f);nameRect.offsetMin=new Vector2(4f,1f);nameRect.offsetMax=new Vector2(-4f,-1f);name.alignment=TextAlignmentOptions.Center;name.fontSize=11f;name.enableWordWrapping=false;name.overflowMode=TextOverflowModes.Truncate;
                var usingCheckImage=EnsureImage(tileObject.transform,"UsingCheck");var usingCheckRect=usingCheckImage.rectTransform;usingCheckRect.anchorMin=Vector2.one*.5f;usingCheckRect.anchorMax=Vector2.one*.5f;usingCheckRect.pivot=Vector2.one*.5f;usingCheckRect.anchoredPosition=Vector2.zero;usingCheckRect.sizeDelta=new Vector2(44f,44f);usingCheckImage.sprite=roundedCardSprite;usingCheckImage.type=Image.Type.Sliced;usingCheckImage.color=new Color(1f,.95f,.78f,.94f);usingCheckImage.raycastTarget=false;var checkLabel=EnsureText(usingCheckImage.transform,"CheckLabel","✓");Stretch(checkLabel.rectTransform);checkLabel.alignment=TextAlignmentOptions.Center;checkLabel.fontSize=32f;checkLabel.fontStyle=FontStyles.Bold;checkLabel.color=new Color(.14f,.48f,.25f,1f);checkLabel.raycastTarget=false;
                var previewImage=EnsureImage(tileObject.transform,"PreviewOutline");Stretch(previewImage.rectTransform);previewImage.rectTransform.offsetMin=new Vector2(2f,2f);previewImage.rectTransform.offsetMax=new Vector2(-2f,-2f);previewImage.sprite=previewOutlineSprite;previewImage.type=Image.Type.Sliced;previewImage.color=new Color(.78f,.32f,.20f,1f);previewImage.raycastTarget=false;
                var noneImage=EnsureImage(tileObject.transform,"NoneIcon");Stretch(noneImage.rectTransform);noneImage.sprite=null;noneImage.color=Color.clear;noneImage.raycastTarget=false;noneImage.enabled=false;
                var usingCheck=usingCheckImage.gameObject;var preview=previewImage.gameObject;var noneIcon=noneImage.gameObject;
                var tile=tileObject.GetComponent<DecorationCatalogueTileView>();tile.ConfigureRuntimeViews(tileObject.GetComponent<Button>(),thumbnail,name,usingCheck,preview,noneIcon);tileObject.SetActive(false);
                foreach(var legacyTile in view.GetComponentsInChildren<DecorationCatalogueTileView>(true))if(legacyTile!=tile)UnityEngine.Object.DestroyImmediate(legacyTile.gameObject);
                var legacyTitle=expanded.transform.Find("Title");
                if(legacyTitle!=null)UnityEngine.Object.DestroyImmediate(legacyTitle.gameObject);
                var collapsedLabel=EnsureText(collapsed.transform,"Label","Catalogue");Stretch(collapsedLabel.rectTransform);collapsedLabel.alignment=TextAlignmentOptions.Center;collapsedLabel.fontSize=24f;collapsedLabel.fontStyle=FontStyles.Bold;collapsedLabel.raycastTarget=false;
                var surfaceFooter=EnsureRect(root.transform,"SurfaceFooterHost",new Vector2(0f,24f),new Vector2(-48f,128f));surfaceFooter.anchorMin=new Vector2(0f,0f);surfaceFooter.anchorMax=new Vector2(1f,0f);surfaceFooter.pivot=new Vector2(.5f,0f);surfaceFooter.anchoredPosition=new Vector2(0f,24f);surfaceFooter.sizeDelta=new Vector2(-48f,128f);
                var tabsObject=EnsureObject(root.transform,"ModeTabs",typeof(RectTransform),typeof(DecorationModeTabsView));
                var tabs=tabsObject.GetComponent<DecorationModeTabsView>();LayoutModeTabs(tabs,roundedCardSprite);
                tabs.transform.SetAsLastSibling();
                var so=new SerializedObject(view);so.FindProperty("verticalScroll").objectReferenceValue=vertical;so.FindProperty("categoryContent").objectReferenceValue=categoryContent;so.FindProperty("categoryRowTemplate").objectReferenceValue=row;so.FindProperty("categoryTileTemplate").objectReferenceValue=tile;so.FindProperty("tileTemplate").objectReferenceValue=tile;so.FindProperty("sheetActionRoot").objectReferenceValue=surfaceFooter.gameObject;so.FindProperty("surfaceFooterHost").objectReferenceValue=surfaceFooter;so.FindProperty("surfaceFooterExpandedAnchoredPosition").vector2Value=new Vector2(0f,24f);so.FindProperty("collapsedAnchoredPosition").vector2Value=new Vector2(0f,-490f);so.FindProperty("hiddenAnchoredPosition").vector2Value=new Vector2(0f,-780f);var collapseButton=so.FindProperty("collapseButton").objectReferenceValue as Button;if(collapseButton!=null)((RectTransform)collapseButton.transform).anchoredPosition=new Vector2(-80f,-80f);so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,Phase7AssetPaths.CataloguePrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static void ReconcileActionBarPrefab()
        {
            var root=PrefabUtility.LoadPrefabContents(Phase7AssetPaths.ActionBarPrefabPath);
            try
            {
                var view=root.GetComponentInChildren<DecorationActionBarView>(true);
                if(view==null)
                    throw new InvalidOperationException("Phase7 Action Bar prefab requires DecorationActionBarView.");
                var viewSo=new SerializedObject(view);
                viewSo.FindProperty("useReadableActionLabels").boolValue=true;
                var panel=(RectTransform)viewSo.FindProperty("presentationRoot").objectReferenceValue;
                if(panel==null)throw new InvalidOperationException("Phase7 Action Bar prefab requires ActionPanel.");

                var undo=EnsureActionButton(panel,"UndoLastButton","Undo Last");
                var applyAll=EnsureActionButton(panel,"ApplyAllButton","Apply All");
                viewSo.FindProperty("undoLastButton").objectReferenceValue=undo;
                viewSo.FindProperty("applyAllButton").objectReferenceValue=applyAll;

                var roundedSprite=AssetDatabase.LoadAssetAtPath<Sprite>(Phase7AssetPaths.RoundedCatalogueCardSpritePath);
                foreach(var button in view.GetComponentsInChildren<Button>(true))
                {
                    EnsureButtonLabel(button);
                    if(button.image!=null){button.image.sprite=roundedSprite;button.image.type=Image.Type.Sliced;}
                }
                viewSo.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,Phase7AssetPaths.ActionBarPrefabPath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }

        private static Button EnsureActionButton(Transform parent,string name,string semanticLabel)
        {
            var button=parent.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(item=>item.name==name)??CreateButton(parent,name);
            if(button.transform.parent!=parent)button.transform.SetParent(parent,false);
            EnsureButtonLabel(button);
            var label=button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if(label!=null)label.text=semanticLabel;
            var rect=(RectTransform)button.transform;
            rect.sizeDelta=new Vector2(88f,52f);
            rect.localScale=Vector3.one;
            var hook=button.GetComponent<DecorationPointerBoundaryEventHook>()
                     ??button.gameObject.AddComponent<DecorationPointerBoundaryEventHook>();
            var hookSo=new SerializedObject(hook);
            hookSo.FindProperty("semanticLabel").stringValue=semanticLabel;
            hookSo.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }
        private static Sprite BuildRoundedCatalogueCardSprite()
        {
            return BuildRoundedSprite(Phase7AssetPaths.RoundedCatalogueCardSpritePath,32,8f);
        }
        private static Sprite BuildRoundedSprite(string path,int size,float radius)
        {
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            var pixels=new Color[size*size];
            for(var y=0;y<size;y++)for(var x=0;x<size;x++)
            {
                var dx=Mathf.Max(0f,radius-Mathf.Min(x,size-1-x));var dy=Mathf.Max(0f,radius-Mathf.Min(y,size-1-y));
                pixels[y*size+x]=dx*dx+dy*dy<=radius*radius?Color.white:Color.clear;
            }
            texture.SetPixels(pixels);texture.Apply();var bytes=texture.EncodeToPNG();UnityEngine.Object.DestroyImmediate(texture);
            if(!File.Exists(path)||!File.ReadAllBytes(path).SequenceEqual(bytes))File.WriteAllBytes(path,bytes);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spriteBorder=Vector4.one*radius;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static Sprite BuildRoundedOutlineSprite(string path,int size,float radius,float thickness)
        {
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);
            var pixels=new Color[size*size];
            for(var y=0;y<size;y++)for(var x=0;x<size;x++)
            {
                var outer=InsideRoundedRect(x,y,size,radius);
                var inner=InsideRoundedRect(x-thickness,y-thickness,size-thickness*2f,Mathf.Max(0f,radius-thickness));
                pixels[y*size+x]=outer&&!inner?Color.white:Color.clear;
            }
            texture.SetPixels(pixels);texture.Apply();var bytes=texture.EncodeToPNG();UnityEngine.Object.DestroyImmediate(texture);
            if(!File.Exists(path)||!File.ReadAllBytes(path).SequenceEqual(bytes))File.WriteAllBytes(path,bytes);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spriteBorder=Vector4.one*radius;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        private static bool InsideRoundedRect(float x,float y,float size,float radius)
        {
            if(size<=0f||x<0f||y<0f||x>size-1f||y>size-1f)return false;
            var dx=Mathf.Max(0f,radius-Mathf.Min(x,size-1f-x));var dy=Mathf.Max(0f,radius-Mathf.Min(y,size-1f-y));
            return dx*dx+dy*dy<=radius*radius;
        }
        private static GameObject EnsureObject(Transform parent,string name,params Type[] components)
        {var child=parent.Find(name)?.gameObject;if(child==null){child=new GameObject(name,components);child.transform.SetParent(parent,false);}foreach(var type in components)if(child.GetComponent(type)==null)child.AddComponent(type);return child;}
        private static RectTransform EnsureRect(Transform parent,string name,Vector2 position,Vector2 size)
        {var go=EnsureObject(parent,name,typeof(RectTransform));var rect=(RectTransform)go.transform;rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=Vector3.one;return rect;}
        private static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;rect.localScale=Vector3.one;}
        private static TextMeshProUGUI EnsureText(Transform parent,string name,string value)
        {var go=EnsureObject(parent,name,typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));var text=go.GetComponent<TextMeshProUGUI>();text.text=value;text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Phase6/Fonts/NotoSansSC-Phase6 SDF.asset");text.color=new Color(.12f,.10f,.08f,1f);return text;}
        private static Image EnsureImage(Transform parent,string name)=>EnsureObject(parent,name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();
        private static void BuildProjectionMaterials()
        {
            BuildColorMaterial(Phase7AssetPaths.ProjectionValidMaterialPath,new Color(.18f,.72f,.32f,.65f));
            BuildColorMaterial(Phase7AssetPaths.ProjectionInvalidMaterialPath,new Color(.84f,.20f,.18f,.65f));
        }
        private static void BuildColorMaterial(string path,Color color)
        { var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(material,path);}if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);if(material.HasProperty("_Color"))material.SetColor("_Color",color);EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material); }
        private static void CloneUiPrefab(string source,string destination)
        { if(AssetDatabase.LoadAssetAtPath<GameObject>(destination)!=null)return;var root=PrefabUtility.LoadPrefabContents(source);try{root.name=Path.GetFileNameWithoutExtension(destination);PrefabUtility.SaveAsPrefabAsset(root,destination);}finally{PrefabUtility.UnloadPrefabContents(root);} }
        private static void BuildExitModalPrefab()
        {
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(Phase7AssetPaths.ExitModalPrefabPath);
            var root=existing==null?new GameObject("PF_UI_Phase7DecorationExitModal",typeof(RectTransform),typeof(CanvasGroup),typeof(DecorationExitModalView)):PrefabUtility.LoadPrefabContents(Phase7AssetPaths.ExitModalPrefabPath);
            try
            {
                LayoutExitModal(root.GetComponent<DecorationExitModalView>());
                PrefabUtility.SaveAsPrefabAsset(root,Phase7AssetPaths.ExitModalPrefabPath);
            }
            finally{if(existing==null)UnityEngine.Object.DestroyImmediate(root);else PrefabUtility.UnloadPrefabContents(root);}
        }

        internal static void LayoutExitModal(DecorationExitModalView view)
        {
            if(view==null)throw new ArgumentNullException(nameof(view));
            var root=view.transform;
            var rootRect=(RectTransform)root;
            rootRect.anchorMin=Vector2.zero;rootRect.anchorMax=Vector2.one;rootRect.pivot=Vector2.one*.5f;
            rootRect.offsetMin=Vector2.zero;rootRect.offsetMax=Vector2.zero;rootRect.localScale=Vector3.one;
            var group=view.GetComponent<CanvasGroup>()??view.gameObject.AddComponent<CanvasGroup>();
            group.alpha=1f;group.interactable=true;group.blocksRaycasts=true;

            var roundedSprite=AssetDatabase.LoadAssetAtPath<Sprite>(Phase7AssetPaths.RoundedCatalogueCardSpritePath)
                ??throw new InvalidOperationException("Missing Phase 7 rounded Modal sprite.");
            var backdrop=EnsureImage(root,"Backdrop");Stretch(backdrop.rectTransform);
            backdrop.color=new Color(.08f,.05f,.03f,.55f);backdrop.raycastTarget=true;
            backdrop.transform.SetAsFirstSibling();

            var card=EnsureImage(root,"ModalCard");var cardRect=card.rectTransform;
            cardRect.anchorMin=cardRect.anchorMax=new Vector2(.5f,.72f);cardRect.pivot=Vector2.one*.5f;
            cardRect.anchoredPosition=Vector2.zero;cardRect.sizeDelta=new Vector2(440f,220f);cardRect.localScale=Vector3.one;
            card.sprite=roundedSprite;card.type=Image.Type.Sliced;card.color=new Color(1f,.94f,.80f,1f);card.raycastTarget=true;
            var cardShadow=card.GetComponent<Shadow>()??card.gameObject.AddComponent<Shadow>();
            cardShadow.effectColor=new Color(.12f,.10f,.08f,.32f);cardShadow.effectDistance=new Vector2(0f,-10f);cardShadow.useGraphicAlpha=true;

            var prompt=EnsureText(card.transform,"Prompt","Keep editing?");var promptRect=prompt.rectTransform;
            promptRect.anchorMin=promptRect.anchorMax=new Vector2(.5f,.5f);promptRect.pivot=Vector2.one*.5f;
            promptRect.anchoredPosition=new Vector2(0f,48f);promptRect.sizeDelta=new Vector2(360f,64f);promptRect.localScale=Vector3.one;
            prompt.alignment=TextAlignmentOptions.Center;prompt.fontSize=28f;prompt.fontStyle=FontStyles.Bold;prompt.raycastTarget=false;

            var continueButton=root.GetComponentsInChildren<Button>(true).FirstOrDefault(button=>button.name=="ContinueEditingButton")
                ??CreateButton(card.transform,"ContinueEditingButton");
            var discardButton=root.GetComponentsInChildren<Button>(true).FirstOrDefault(button=>button.name=="DiscardChangesButton")
                ??CreateButton(card.transform,"DiscardChangesButton");
            var buttons=new[]{continueButton,discardButton};
            for(var i=0;i<buttons.Length;i++)
            {
                var button=buttons[i];button.transform.SetParent(card.transform,false);EnsureButtonLabel(button);
                var rect=(RectTransform)button.transform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=Vector2.one*.5f;
                rect.anchoredPosition=new Vector2(i==0?-102f:102f,-48f);rect.sizeDelta=new Vector2(180f,58f);rect.localScale=Vector3.one;
                button.image.sprite=roundedSprite;button.image.type=Image.Type.Sliced;
                button.image.color=i==0?new Color(.28f,.43f,.31f,1f):new Color(.94f,.76f,.62f,1f);
                var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(1f,1f,1f,.92f);
                colors.pressedColor=new Color(.84f,.84f,.84f,1f);colors.selectedColor=colors.normalColor;colors.disabledColor=new Color(.65f,.65f,.65f,.72f);colors.fadeDuration=.1f;button.colors=colors;
                var label=button.GetComponentInChildren<TextMeshProUGUI>(true);
                if(label!=null)label.color=i==0?new Color(1f,.96f,.86f,1f):new Color(.22f,.16f,.11f,1f);
                var shadow=button.GetComponent<Shadow>()??button.gameObject.AddComponent<Shadow>();
                shadow.effectColor=new Color(.12f,.10f,.08f,.22f);shadow.effectDistance=new Vector2(0f,-4f);shadow.useGraphicAlpha=true;
            }

            card.transform.SetAsLastSibling();
            var so=new SerializedObject(view);
            so.FindProperty("continueButton").objectReferenceValue=continueButton;
            so.FindProperty("discardButton").objectReferenceValue=discardButton;
            so.FindProperty("modalCard").objectReferenceValue=cardRect;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        internal static Button CreateButton(Transform parent,string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
            var button=go.GetComponent<Button>();EnsureButtonLabel(button);return button;
        }

        internal static void LayoutModeTabs(DecorationModeTabsView view)
        {
            LayoutModeTabs(view,AssetDatabase.LoadAssetAtPath<Sprite>(
                Phase7AssetPaths.RoundedCatalogueCardSpritePath));
        }

        internal static void LayoutModeTabs(DecorationModeTabsView view,Sprite roundedSprite)
        {
            var root=(RectTransform)view.transform;root.anchorMin=new Vector2(0f,0f);root.anchorMax=new Vector2(1f,0f);root.pivot=new Vector2(.5f,0f);root.anchoredPosition=new Vector2(0f,700f);root.sizeDelta=new Vector2(-48f,72f);root.localScale=Vector3.one;
            var so=new SerializedObject(view);var names=new[]{"furnitureButton","floorButton","wallButton","wallDecorButton"};
            for(var i=0;i<names.Length;i++)
            {
                var button=view.GetComponentsInChildren<Button>(true).FirstOrDefault(item=>item.name==names[i])??CreateButton(view.transform,names[i]);
                EnsureButtonLabel(button);so.FindProperty(names[i]).objectReferenceValue=button;
                var rect=(RectTransform)button.transform;rect.anchorMin=new Vector2(0f,0f);rect.anchorMax=new Vector2(0f,0f);rect.pivot=new Vector2(.5f,0f);rect.anchoredPosition=new Vector2(84f+i*116f,0f);rect.sizeDelta=new Vector2(132f,52f);rect.localScale=Vector3.one;
                button.image.sprite=roundedSprite;button.image.type=Image.Type.Sliced;
                var shadow=button.GetComponent<Shadow>()??button.gameObject.AddComponent<Shadow>();shadow.effectColor=new Color(.12f,.10f,.08f,.28f);shadow.effectDistance=new Vector2(0f,-4f);shadow.useGraphicAlpha=true;
            }
            so.ApplyModifiedPropertiesWithoutUndo();view.transform.SetAsLastSibling();
        }

        internal static void EnsureButtonLabel(Button button)
        {
            var label=button.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if(label==null){var labelObject=new GameObject("Label",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));labelObject.transform.SetParent(button.transform,false);label=labelObject.GetComponent<TextMeshProUGUI>();}
            var labelRect=(RectTransform)label.transform;labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=Vector2.zero;labelRect.offsetMax=Vector2.zero;labelRect.localScale=Vector3.one;
            label.text=ButtonLabel(button.name);label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.fontSize=18f;
            label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Phase6/Fonts/NotoSansSC-Phase6 SDF.asset");label.color=new Color(.12f,.10f,.08f,1f);
        }

        private static string ButtonLabel(string name)
        {
            switch(name)
            {
                case "furnitureButton": return "Furniture";
                case "floorButton": return "Floor";
                case "wallButton": return "Wall";
                case "wallDecorButton": return "Wall Decor";
                case "WholeRoomButton": return "Whole Room";
                case "SingleGridButton": return "Single Grid";
                case "StoreButton": return "Store";
                case "UndoLastButton": return "Undo Last";
                case "ApplyAllButton": return "Apply All";
                case "RotateButton": return "Rotate";
                case "CancelButton": return "Cancel";
                case "ConfirmButton": return "Confirm";
                case "ContinueEditingButton": return "Continue";
                case "DiscardChangesButton": return "Discard";
                default:return name;
            }
        }

        private static T EnsureAsset<T>(string path) where T:ScriptableObject
        { var asset=AssetDatabase.LoadAssetAtPath<T>(path); if(asset!=null)return asset; asset=ScriptableObject.CreateInstance<T>(); asset.name=Path.GetFileNameWithoutExtension(path); AssetDatabase.CreateAsset(asset,path); return asset; }
        private static void Set(UnityEngine.Object target,string name,object value)
        { var so=new SerializedObject(target); var p=so.FindProperty(name) ?? throw new InvalidOperationException(name); if(value is string s)p.stringValue=s; else if(value is int i)p.intValue=i; else if(value is float f)p.floatValue=f; else if(value is bool b)p.boolValue=b; else p.objectReferenceValue=(UnityEngine.Object)value; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target); }
        private static void SetArray(UnityEngine.Object target,string name,UnityEngine.Object[] values)
        { var so=new SerializedObject(target); var p=so.FindProperty(name); p.arraySize=values.Length; for(var i=0;i<values.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=values[i]; so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target);AssetDatabase.SaveAssetIfDirty(target); }
        private static void EnsureFolder(string path)
        { if(AssetDatabase.IsValidFolder(path))return; var parent=Path.GetDirectoryName(path).Replace('\\','/'); EnsureFolder(parent); AssetDatabase.CreateFolder(parent,Path.GetFileName(path)); }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using System.Security.Cryptography;

namespace AnimalCafe.EditorTools.Phase7
{
    public sealed class Phase7ValidationIssue { public Phase7ValidationIssue(string code,string message){Code=code;Message=message;} public string Code{get;} public string Message{get;} }
    public sealed class Phase7ValidationReport { public Phase7ValidationReport(IEnumerable<Phase7ValidationIssue> issues){Issues=issues.ToArray();} public IReadOnlyList<Phase7ValidationIssue> Issues{get;} public bool IsValid=>Issues.Count==0; }
    public static class Phase7Validator
    {
        private static readonly (string Path,SurfaceStyleKind Kind,string[] Ids)[] ExpectedCatalogues={
            (Phase7AssetPaths.FloorCataloguePath,SurfaceStyleKind.Floor,new[]{"floor.dark-stone","floor.light-tile","floor.warm-wood"}),
            (Phase7AssetPaths.PaintCataloguePath,SurfaceStyleKind.Paint,new[]{"paint.cream","paint.sage","paint.terracotta"}),
            (Phase7AssetPaths.WainscotingCataloguePath,SurfaceStyleKind.Wainscoting,new[]{"wainscoting.none","wainscoting.sage-plain","wainscoting.warm-white-rail"}),
            (Phase7AssetPaths.WallpaperCataloguePath,SurfaceStyleKind.Wallpaper,new[]{"wallpaper.cream-floral","wallpaper.sage-sprig"})};
        public static Phase7ValidationReport ValidateAll()
        {
            var issues=new List<Phase7ValidationIssue>();ValidateFormalProvenance(issues);ValidateCatalogues(issues);ValidateTexturesAndMaterials(issues);ValidateMounted(issues);ValidatePrefabs(issues);ValidateScenes(issues);ValidatePlayerBoundary(issues);
            if(EditorBuildSettings.scenes.Any(x=>x.enabled&&x.path==Phase7AssetPaths.ValidationScenePath))Add(issues,"P7-BUILD-VALIDATION","Validation Scene must be excluded.");
            if(EditorBuildSettings.scenes.Count(x=>x.enabled&&x.path==Phase7AssetPaths.MainCafeScenePath)!=1)Add(issues,"P7-BUILD-MAINCAFE","MainCafe must be the unique enabled production Scene entry.");
            return new Phase7ValidationReport(issues.OrderBy(x=>x.Code,StringComparer.Ordinal).ThenBy(x=>x.Message,StringComparer.Ordinal));
        }
        public static IReadOnlyList<Phase7ValidationIssue> ValidateSurfaceDefinition(SurfaceStyleDefinitionAsset d)
        {
            var r=new List<Phase7ValidationIssue>();if(d==null){Add(r,"P7-SURFACE-NULL","null");return r;}if(string.IsNullOrWhiteSpace(d.StyleId))Add(r,"P7-SURFACE-ID",d.name);if(string.IsNullOrWhiteSpace(d.DisplayName))Add(r,"P7-SURFACE-NAME",d.name);
            if(d.IsNoneOption&&d.Kind!=SurfaceStyleKind.Wainscoting)Add(r,"P7-SURFACE-NONE-KIND",d.name);if(d.IsNoneOption&&d.Material!=null)Add(r,"P7-SURFACE-NONE-MATERIAL",d.name);if(!d.IsNoneOption&&d.Material==null)Add(r,"P7-SURFACE-MATERIAL",d.name);if(d.Thumbnail==null)Add(r,"P7-SURFACE-THUMBNAIL",d.name);
            var metadataValid=d.Kind==SurfaceStyleKind.Floor&&d.VerticalMapping==SurfaceStyleVerticalMapping.OneGrid&&Mathf.Approximately(d.WorldTileWidthMeters,1f)&&Mathf.Approximately(d.WorldTileHeightMeters,1f)
                ||d.Kind==SurfaceStyleKind.Wallpaper&&d.VerticalMapping==SurfaceStyleVerticalMapping.FullWall&&Mathf.Approximately(d.WorldTileWidthMeters,1f)&&Mathf.Approximately(d.WorldTileHeightMeters,0f)
                ||d.Kind==SurfaceStyleKind.Wainscoting&&d.VerticalMapping==SurfaceStyleVerticalMapping.WaistReference&&Mathf.Approximately(d.WorldTileWidthMeters,1f)&&Mathf.Approximately(d.WorldTileHeightMeters,CharacterScaleReference.SharedCharacterWaistHeightMeters)
                ||d.Kind==SurfaceStyleKind.Paint&&d.VerticalMapping==SurfaceStyleVerticalMapping.NotApplicable&&Mathf.Approximately(d.WorldTileWidthMeters,0f)&&Mathf.Approximately(d.WorldTileHeightMeters,0f);
            if(!metadataValid)Add(r,"P7-SURFACE-WORLD-METADATA",d.name+" has invalid kind-specific world mapping metadata.");return r;
        }
        private static void ValidateCatalogues(ICollection<Phase7ValidationIssue> issues)
        {
            var globalIds=new HashSet<string>(StringComparer.Ordinal);foreach(var expected in ExpectedCatalogues){var catalogue=AssetDatabase.LoadAssetAtPath<SurfaceStyleCatalogueAsset>(expected.Path);if(catalogue==null){Add(issues,"P7-CATALOGUE-MISSING",expected.Path);continue;}if(catalogue.Kind!=expected.Kind)Add(issues,"P7-CATALOGUE-KIND",expected.Path);
                var ids=catalogue.Entries.Where(x=>x!=null).Select(x=>x.StyleId).ToArray();if(!ids.SequenceEqual(expected.Ids,StringComparer.Ordinal))Add(issues,"P7-CATALOGUE-ORDER",expected.Path+": "+string.Join(",",ids));foreach(var entry in catalogue.Entries){foreach(var issue in ValidateSurfaceDefinition(entry))issues.Add(issue);if(entry!=null&&entry.Kind!=expected.Kind)Add(issues,"P7-CATALOGUE-ENTRY-KIND",entry.StyleId);if(entry!=null&&!globalIds.Add(entry.StyleId))Add(issues,"P7-CATALOGUE-DUPLICATE-ID",entry.StyleId);}}
        }
        private static void ValidateTexturesAndMaterials(ICollection<Phase7ValidationIssue> issues)
        {
            foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Phase7AssetPaths.TextureFolder})){var path=AssetDatabase.GUIDToAssetPath(guid);if(Path.GetFileName(path).StartsWith("T_WallDecor_",StringComparison.Ordinal))continue;var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null||importer.wrapMode!=TextureWrapMode.Repeat)Add(issues,"P7-TEXTURE-WRAP",path);if(texture==null||texture.width!=texture.height)Add(issues,"P7-TEXTURE-ONE-GRID",path+" must be square one-grid source metadata.");}
            foreach(var expected in ExpectedCatalogues){var catalogue=AssetDatabase.LoadAssetAtPath<SurfaceStyleCatalogueAsset>(expected.Path);if(catalogue==null)continue;foreach(var entry in catalogue.Entries.Where(x=>x!=null&&!x.IsNoneOption)){var material=entry.Material;if(material==null)continue;var hasBaseMap=material.HasProperty("_BaseMap");var texture=hasBaseMap?material.GetTexture("_BaseMap"):material.HasProperty("_MainTex")?material.GetTexture("_MainTex"):null;if(texture==null&&entry.Kind!=SurfaceStyleKind.Paint)Add(issues,"P7-MATERIAL-TEXTURE",entry.StyleId);var scale=hasBaseMap?material.GetTextureScale("_BaseMap"):material.HasProperty("_MainTex")?material.GetTextureScale("_MainTex"):Vector2.one;if(scale!=Vector2.one)Add(issues,"P7-MATERIAL-TILING",entry.StyleId);if(entry.Kind==SurfaceStyleKind.Floor&&material.shader.name!="AnimalCafe/Phase7/FloorSurfaceTiled")Add(issues,"P7-MATERIAL-SHADER",entry.StyleId);}}
            if(!Mathf.Approximately(CharacterScaleReference.SharedCharacterWaistHeightMeters,.65f)||!Mathf.Approximately(CharacterScaleReference.GetNormalizedWainscotingCutoff(2f),.325f))Add(issues,"P7-WAINSCOTING-CUTOFF","Cutoff must derive from named 0.65m shared waist reference and canonical wall height.");
        }
        private static readonly Dictionary<string,(int W,int H)> MountedFootprints=new Dictionary<string,(int,int)>(StringComparer.Ordinal){{"wall-decor.monitor.01",(1,1)},{"wall-decor.shiba-painting.01",(1,2)},{"wall-decor.wood-shelf.01",(2,1)},{"window.canonical.phase4",(1,1)},{"window.tall-glass.1x2.01",(1,2)}};
        private static readonly Dictionary<string,string> MountedThumbnailSha256=new Dictionary<string,string>(StringComparer.Ordinal){
            {"wall-decor.monitor.01","142B111FB04675EE27285C8A8013E28F6873CCB654141AA6BAE3BA2E75A31BFC"},
            {"wall-decor.shiba-painting.01","C0A28E35E331CF9CF17D93F6C5F37AE72C934BD19AA77231036CDF5FE669488A"},
            {"wall-decor.wood-shelf.01","AEFE4661DABB261DDA52D2963DE3DD131BBDA532760EFABEC113EB437AFAC5C8"},
            {"window.canonical.phase4","8B424C22DEE51E232B44812D4FD522266F468EB09E5584490002BEA029D2645B"},
            {"window.tall-glass.1x2.01","7E56B6D9E61C4DA5252E395E19FD631A0AB12D721E774FEC9560D181B4A5EB38"}};
        private static void ValidateFormalProvenance(ICollection<Phase7ValidationIssue> issues)
        {
            foreach(var pair in Phase7FormalAssetIntake.SourceSha256.Where(pair=>!pair.Key.EndsWith(".png",StringComparison.OrdinalIgnoreCase)))ValidateHash(Phase7AssetPaths.RawSourceFolder+"/"+pair.Key,pair.Value,"P7-PROVENANCE-RAW",issues);
            ValidateHash(Phase7AssetPaths.TextureFolder+"/T_WallDecor_ShibaPortrait_v01.png",Phase7FormalAssetIntake.SourceSha256["T_WallDecor_ShibaPortrait_v01.png"],"P7-PROVENANCE-RAW",issues);
            foreach(var pair in Phase7FormalAssetIntake.DerivedSha256)ValidateHash("ArtSource/Phase7/Derived/"+pair.Key,pair.Value,"P7-PROVENANCE-DERIVED",issues);
            var manifest=File.Exists(Phase7AssetPaths.ProvenanceManifestPath)?File.ReadAllText(Phase7AssetPaths.ProvenanceManifestPath):string.Empty;
            foreach(var hash in Phase7FormalAssetIntake.SourceSha256.Values.Concat(Phase7FormalAssetIntake.DerivedSha256.Values))if(!manifest.Contains(hash))Add(issues,"P7-PROVENANCE-MANIFEST",hash);
        }
        private static void ValidateHash(string path,string expected,string code,ICollection<Phase7ValidationIssue> issues){if(!File.Exists(path)){Add(issues,code,path);return;}using var sha=SHA256.Create();var actual=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");if(!actual.Equals(expected,StringComparison.OrdinalIgnoreCase))Add(issues,code,path);}
        private static void ValidateMounted(ICollection<Phase7ValidationIssue> issues){ValidateMountedCatalogue(Phase7AssetPaths.WallMountedProductionCataloguePath,WallMountedCatalogueKind.WallDecor,new[]{"wall-decor.monitor.01","wall-decor.shiba-painting.01","wall-decor.wood-shelf.01"},issues);ValidateMountedCatalogue(Phase7AssetPaths.WindowCataloguePath,WallMountedCatalogueKind.Windows,new[]{"window.canonical.phase4","window.tall-glass.1x2.01"},issues);}
        private static void ValidateMountedCatalogue(string path,WallMountedCatalogueKind kind,string[] expectedIds,ICollection<Phase7ValidationIssue> issues)
        {
            var catalogue=AssetDatabase.LoadAssetAtPath<WallMountedCatalogueAsset>(path);if(catalogue==null){Add(issues,"P7-MOUNTED-CATALOGUE",path);return;}if(catalogue.Kind!=kind)Add(issues,"P7-MOUNTED-CATALOGUE-KIND",path);var ids=catalogue.Entries.Where(x=>x!=null).Select(x=>x.DefinitionId).ToArray();if(!ids.SequenceEqual(expectedIds,StringComparer.Ordinal))Add(issues,"P7-MOUNTED-ORDER",path);
            foreach(var d in catalogue.Entries){if(d==null){Add(issues,"P7-MOUNTED-NULL",path);continue;}if(d.Prefab==null){Add(issues,"P7-MOUNTED-PREFAB",d.name);continue;}if(!MountedFootprints.TryGetValue(d.DefinitionId,out var footprint)||d.FootprintWidth!=footprint.W||d.FootprintHeight!=footprint.H)Add(issues,"P7-MOUNTED-FOOTPRINT",d.DefinitionId);if(d.Thumbnail==null||d.Thumbnail.texture.width!=256||d.Thumbnail.texture.height!=256)Add(issues,"P7-MOUNTED-THUMBNAIL",d.name);else{var thumbnailPath=AssetDatabase.GetAssetPath(d.Thumbnail);if(MountedThumbnailSha256.TryGetValue(d.DefinitionId,out var thumbnailHash))ValidateHash(thumbnailPath,thumbnailHash,"P7-MOUNTED-THUMBNAIL-HASH",issues);ValidateMountedThumbnailPresentation(thumbnailPath,d.DefinitionId,issues);}if(d.MaxVisualDepth<0f||d.MaxVisualDepth>WallMountedDefinitionAsset.MaximumVisualDepth)Add(issues,"P7-MOUNTED-DEPTH",d.name);var root=d.Prefab.transform;if(root.localPosition!=Vector3.zero||root.localRotation!=Quaternion.identity||root.localScale!=Vector3.one)Add(issues,"P7-MOUNTED-ROOT",d.DefinitionId);if(root.childCount!=1||root.GetChild(0).name!="Visual")Add(issues,"P7-MOUNTED-VISUAL-WRAPPER",d.DefinitionId);var rs=d.Prefab.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)Add(issues,"P7-MOUNTED-RENDERER",d.DefinitionId);else{var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);if(Mathf.Abs(b.min.y)>.015f||b.min.z<-.015f||b.size.z>.365f||b.size.x>d.FootprintWidth+.03f||b.size.y>d.FootprintHeight+.03f)Add(issues,"P7-MOUNTED-BOUNDS",d.DefinitionId);if(b.size.z>d.MaxVisualDepth+.015f)Add(issues,"P7-MOUNTED-DEPTH-CONSISTENCY",d.DefinitionId);}var colliders=d.Prefab.GetComponentsInChildren<Collider>(true);if(colliders.Length==0||colliders.Any(x=>!x.isTrigger||x.gameObject.layer!=0))Add(issues,"P7-MOUNTED-SELECTION-COLLIDER",d.DefinitionId);if(d.Prefab.GetComponentsInChildren<NavMeshObstacle>(true).Length>0)Add(issues,"P7-MOUNTED-NAV",d.DefinitionId);if(d.Prefab.GetComponentsInChildren<Rigidbody>(true).Length>0)Add(issues,"P7-MOUNTED-RIGIDBODY",d.DefinitionId);var meshes=d.Prefab.GetComponentsInChildren<MeshFilter>(true);if(meshes.Length==0||meshes.All(x=>x.sharedMesh==null||x.sharedMesh.uv.Length==0))Add(issues,"P7-MOUNTED-MESH-UV",d.DefinitionId);if(rs.SelectMany(x=>x.sharedMaterials).Any(x=>x==null))Add(issues,"P7-MOUNTED-MATERIAL",d.DefinitionId);if(d.DefinitionId=="wall-decor.shiba-painting.01"&&!rs.SelectMany(x=>x.sharedMaterials).Any(IsTransparentPortrait))Add(issues,"P7-PAINTING-ALPHA",d.DefinitionId);if(d.DefinitionId.StartsWith("window.")&&!rs.SelectMany(x=>x.sharedMaterials).Any(x=>x!=null&&x.name.Contains("Glass")&&x.renderQueue>=3000&&Mathf.Approximately(x.GetFloat("_ZWrite"),0f)))Add(issues,"P7-WINDOW-GLASS",d.DefinitionId);}
        }
        private static void ValidateMountedThumbnailPresentation(string path,string id,ICollection<Phase7ValidationIssue> issues)
        {
            if(!File.Exists(path))return;
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try
            {
                if(!texture.LoadImage(File.ReadAllBytes(path))){Add(issues,"P7-MOUNTED-THUMBNAIL-BACKDROP",id);return;}
                var border=Mathf.Max(4,Mathf.RoundToInt(Mathf.Min(texture.width,texture.height)*24f/256f));
                var borderPixels=0;var transparentBorderPixels=0;var visibleItemPixels=0;
                for(var y=0;y<texture.height;y++)for(var x=0;x<texture.width;x++)
                {
                    var colour=texture.GetPixel(x,y);
                    if(x<border||x>=texture.width-border||y<border||y>=texture.height-border)
                    {
                        borderPixels++;
                        if(colour.a<.05f)transparentBorderPixels++;
                    }
                    if(x>=texture.width/5&&x<texture.width*4/5&&y>=texture.height/5&&y<texture.height*4/5&&colour.a>.1f)
                        visibleItemPixels++;
                }
                if(borderPixels==0||transparentBorderPixels/(float)borderPixels<=.80f||visibleItemPixels<=texture.width*texture.height*.005f)
                    Add(issues,"P7-MOUNTED-THUMBNAIL-BACKDROP",id+" must show a mounted-angle prefab cutout on genuine transparency without a wall, floor, black, or checkerboard backdrop.");
            }
            finally{UnityEngine.Object.DestroyImmediate(texture);}
        }
        private static bool IsTransparentPortrait(Material m)=>m!=null&&m.name.Contains("ShibaPortrait")&&m.renderQueue>=3000&&Mathf.Approximately(m.GetFloat("_Surface"),1f)&&Mathf.Approximately(m.GetFloat("_ZWrite"),0f)&&m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT");
        private static void ValidatePrefabs(ICollection<Phase7ValidationIssue> issues)
        {
            foreach(var path in new[]{Phase7AssetPaths.CataloguePrefabPath,Phase7AssetPaths.ActionBarPrefabPath,Phase7AssetPaths.ExitModalPrefabPath}.Concat(AssetDatabase.FindAssets("t:Prefab",new[]{Phase7AssetPaths.PlaceholderPrefabFolder}).Select(AssetDatabase.GUIDToAssetPath))){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null){Add(issues,"P7-PREFAB-MISSING",path);continue;}if(prefab.GetComponentsInChildren<UnityEngine.Camera>(true).Any(camera=>camera.targetTexture!=null))Add(issues,"P7-PREFAB-CAMERA-RT",path);if(prefab.GetComponentsInChildren<UnityEngine.UI.RawImage>(true).Any(image=>image.texture is RenderTexture))Add(issues,"P7-PREFAB-RAWIMAGE-RT",path);if(prefab.GetComponentsInChildren<Renderer>(true).SelectMany(x=>x.sharedMaterials).Where(x=>x!=null).SelectMany(x=>x.GetTexturePropertyNames().Select(x.GetTexture)).Any(x=>x is RenderTexture))Add(issues,"P7-PREFAB-RENDERTEXTURE",path);foreach(var component in prefab.GetComponentsInChildren<Component>(true).Where(x=>x!=null)){var so=new SerializedObject(component);var iterator=so.GetIterator();while(iterator.NextVisible(true))if(iterator.propertyType==SerializedPropertyType.ObjectReference&&iterator.objectReferenceValue is RenderTexture)Add(issues,"P7-PREFAB-SERIALIZED-RT",path+":"+component.name+"."+iterator.propertyPath);}}
        }
        private static void ValidateScenes(ICollection<Phase7ValidationIssue> issues)
        {
            var expectedWallBodyMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                Phase7AssetPaths.WallBodyMaterialPath);
            var finishNames = new[]
            {
                "Phase7_WallFinish",
                "Phase7_WainscotingFinish",
                "Phase7_WainscotingRailLip",
                "Phase7_WainscotingBaseboardLip"
            };
            foreach(var path in new[]{Phase7AssetPaths.MainCafeScenePath,Phase7AssetPaths.ValidationScenePath})
            {
                var existing=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).FirstOrDefault(x=>x.path==path);var opened=!existing.IsValid();var scene=opened?EditorSceneManager.OpenScene(path,OpenSceneMode.Additive):existing;
                try
                {
                    var all=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<Transform>(true)).ToArray();var walls=all.Select(x=>x.GetComponent<WallSurfaceAuthoring>()).Where(x=>x!=null).OrderBy(x=>x.SurfaceId,StringComparer.Ordinal).ToArray();
                    if(!walls.Select(x=>x.SurfaceId).SequenceEqual(new[]{"wall.back-left","wall.back-right"},StringComparer.Ordinal)||walls.Any(x=>x.Columns!=8||x.Rows!=2||!Mathf.Approximately(x.SlotSize,1f)))Add(issues,"P7-SCENE-WALL-SLOTS",path);
                    foreach(var wall in walls)
                    {
                        var body = wall.transform.Find("WallVisual");
                        var bodyRenderer = body == null ? null : body.GetComponent<Renderer>();
                        if (bodyRenderer == null
                            || expectedWallBodyMaterial == null
                            || bodyRenderer.sharedMaterial != expectedWallBodyMaterial
                            || bodyRenderer.sharedMaterial.shader.name != "Universal Render Pipeline/Lit")
                            Add(issues,"P7-SCENE-WALL-BODY-MATERIAL",path+":"+wall.SurfaceId);
                        foreach (var finishName in finishNames)
                        {
                            var finish = wall.transform.Find(finishName);
                            var finishRenderer = finish == null ? null : finish.GetComponent<Renderer>();
                            if (finishRenderer == null)
                                Add(issues,"P7-SCENE-WALL-FINISH",path+":"+wall.SurfaceId+":"+finishName);
                            else if (finishRenderer.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                                Add(issues,"P7-SCENE-WALL-FINISH-SHADOW",path+":"+wall.SurfaceId+":"+finishName);
                        }
                        var runtimeWall = UnityEngine.Object.Instantiate(wall.gameObject);
                        try
                        {
                            var runtimeAuthoring = runtimeWall.GetComponent<WallSurfaceAuthoring>();
                            var runtimeBody = runtimeWall.transform.Find("WallVisual")?.GetComponent<Renderer>();
                            var runtimeView = runtimeWall.GetComponent<WallSurfaceView>()
                                ?? runtimeWall.AddComponent<WallSurfaceView>();
                            if (runtimeAuthoring == null || runtimeBody == null)
                            {
                                Add(issues,"P7-SCENE-WALL-BODY-SHADOW",path+":"+wall.SurfaceId);
                                continue;
                            }
                            runtimeView.Configure(
                                runtimeAuthoring,
                                runtimeBody,
                                runtimeBody.bounds.size.y,
                                CharacterScaleReference.SharedCharacterWaistHeightMeters);
                            if (runtimeBody.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.On)
                                Add(issues,"P7-SCENE-WALL-BODY-SHADOW",path+":"+wall.SurfaceId);
                            foreach (var finishName in finishNames)
                            {
                                var runtimeFinish = runtimeWall.transform.Find(finishName)?.GetComponent<Renderer>();
                                if (runtimeFinish != null
                                    && runtimeFinish.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                                    Add(issues,"P7-SCENE-WALL-FINISH-SHADOW",path+":"+wall.SurfaceId+":"+finishName+":runtime");
                            }
                        }
                        finally
                        {
                            UnityEngine.Object.DestroyImmediate(runtimeWall);
                        }
                    }
                    if(all.Count(x=>x.name=="Phase7_InteriorRuntime")!=1||all.Count(x=>x.name=="Phase7_UIRuntime")!=1)Add(issues,"P7-SCENE-ROOTS",path);var runtime=all.SingleOrDefault(x=>x.name=="Phase7_InteriorRuntime");if(runtime==null||runtime.GetComponents<WallSurfaceRegistry>().Length!=1||runtime.GetComponents<WallMountedSceneRegistry>().Length!=1||runtime.GetComponents<FloorSurfaceGridView>().Length!=1)Add(issues,"P7-SCENE-REGISTRIES",path);
                }
                finally{if(opened&&scene.IsValid()&&scene.isLoaded)EditorSceneManager.CloseScene(scene,true);}
            }
        }
        private static void ValidatePlayerBoundary(ICollection<Phase7ValidationIssue> issues){foreach(var path in Directory.GetFiles("Assets/Scripts","*.cs",SearchOption.AllDirectories))if(File.ReadAllText(path).Contains("using UnityEditor"))Add(issues,"P7-RUNTIME-UNITYEDITOR",path.Replace('\\','/'));}
        private static void Add(ICollection<Phase7ValidationIssue> issues,string code,string message)=>issues.Add(new Phase7ValidationIssue(code,message));
    }
}

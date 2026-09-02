using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AnimalCafe.Content;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace AnimalCafe.EditorTools.Phase7
{
    public static class Phase7FormalAssetIntake
    {
        private const string StudioOwnerDropRoot=@"E:\Unity\Project\AnimalCafe\Blender Model Item\Phase 7";
        public static readonly IReadOnlyDictionary<string,string> SourceSha256=new Dictionary<string,string>(StringComparer.Ordinal){
            ["Wall monitor 3d model.glb"]="866778921ECAAD30C0FC3FFACB746688952EBDE363EF25F7DA38B8273BF31941",
            ["wooden shelf 3d model.glb"]="D05373DD26111ED43DC38BD7F90EB3508ACBE0A300E9E981AF325A547C765F21",
            ["wooden paint 3d model_fixed_v01.glb"]="C699199DD046956939F318CEBCBDFDE0C595EF029190F598D42A72AEA67B7DF4",
            ["T_WallDecor_ShibaPortrait_v01.png"]="69C35321DF0110F062A8631FE92D1486DBCE8692EF1DE6EE1BCF8B515EBA52F7",
            ["tall glass window 3d model_1x1_fixed_v01.glb"]="64CB2BC212A0278FB9E5E5297EDB9B6780199D7BBFD5126D1F457A89C8B9FF1C",
            ["tall glass window 3d model_1x2_fixed_v01.glb"]="0494E87CE27CD0ADF2FE204B28EA64D352FDA66BACE8C513CFA7BE9C8EB6E1EF"};
        public static readonly IReadOnlyDictionary<string,string> DerivedSha256=new Dictionary<string,string>(StringComparer.Ordinal){
            ["SM_WallDecor_Monitor_01.fbx"]="17FAC5E8A66E70426EE8BC6552E5F45C131895657E901DB4CD127BB97417A34F",
            ["SM_WallDecor_Monitor_01_computer_monitor_3d_model_basecolor_jpg.png"]="C64E0A088058C541E37EB8FA50D256A6A99FC478EDC0EF4DE0D8570A1C187758",
            ["SM_WallDecor_ShibaPainting_01.fbx"]="C13DF59FDF41B3DD2E11E9CC4AA903C772E40049C8D59BBD2A8CE300CEC2B3F5",
            ["SM_WallDecor_ShibaPainting_01_wooden_paint_3d_model_basecolor.png"]="81A07293E9118F85C8AF32993A083C2F4FA2953023472334220A670B73635E18",
            ["SM_WallDecor_WoodShelf_01.fbx"]="AD6956326C0F2EC7C326CC4ED80C335DEE30771A5CD9D37B3D982D8C34F2E163",
            ["SM_WallDecor_WoodShelf_01_wooden_sofa_3d_model_basecolor_jpg.png"]="3C0C0FCA116DD5DCD5C1FE9E654826DF132D42E7CA48B1D146564559DAACB19A",
            ["SM_Window_TallGlass_1x1_01.fbx"]="CBBC5B42A9C42A0DB4F0A3F2F5EB741EEF541937C600B804423644AF2468D6CF",
            ["SM_Window_TallGlass_1x1_01_tall_glass_window_3d_model_basecolor.png"]="A61AD47A311286ABBE75D3E95BAF9E428F8971B014ABB8FAFF0571B5E500BEF2",
            ["SM_Window_TallGlass_1x2_01.fbx"]="BEAB8705B4F82FE878A8979B921AFCEA60560A728A592C9A5192E0A2C394EA2C",
            ["SM_Window_TallGlass_1x2_01_tall_glass_window_3d_model_basecolor.png"]="A61AD47A311286ABBE75D3E95BAF9E428F8971B014ABB8FAFF0571B5E500BEF2"};
        public static IReadOnlyList<string> RepositoryAuthorityPaths =>
            SourceSha256.Keys.Where(name=>!name.EndsWith(".png",StringComparison.OrdinalIgnoreCase))
                .Select(name=>Phase7AssetPaths.RawSourceFolder+"/"+name)
                .Concat(new[]{Phase7AssetPaths.TextureFolder+"/T_WallDecor_ShibaPortrait_v01.png"})
                .Concat(DerivedSha256.Keys.Select(name=>"ArtSource/Phase7/Derived/"+name))
                .ToArray();
        private sealed class Item{public string Source,Model,BaseColor,Prefab,Definition,Id,Name;public int W,H;public bool Rotate,RotateX,FlipFront,Monitor;}
        private static readonly Item[] Items={
            new Item{Source="Wall monitor 3d model.glb",Model="SM_WallDecor_Monitor_01.fbx",BaseColor="SM_WallDecor_Monitor_01_computer_monitor_3d_model_basecolor_jpg.png",Prefab="PF_WallDecor_1x1_01",Definition="WD_WallDecor_Monitor_01",Id="wall-decor.monitor.01",Name="Monitor",W=1,H=1,RotateX=true,FlipFront=true,Monitor=true},
            new Item{Source="wooden paint 3d model_fixed_v01.glb",Model="SM_WallDecor_ShibaPainting_01.fbx",BaseColor="SM_WallDecor_ShibaPainting_01_wooden_paint_3d_model_basecolor.png",Prefab="PF_WallDecor_1x2_01",Definition="WD_WallDecor_ShibaPainting_01",Id="wall-decor.shiba-painting.01",Name="Shiba Painting",W=1,H=2},
            new Item{Source="wooden shelf 3d model.glb",Model="SM_WallDecor_WoodShelf_01.fbx",BaseColor="SM_WallDecor_WoodShelf_01_wooden_sofa_3d_model_basecolor_jpg.png",Prefab="PF_WallDecor_2x1_01",Definition="WD_WallDecor_WoodShelf_01",Id="wall-decor.wood-shelf.01",Name="Wood Shelf",W=2,H=1},
            new Item{Source="tall glass window 3d model_1x1_fixed_v01.glb",Model="SM_Window_TallGlass_1x1_01.fbx",BaseColor="SM_Window_TallGlass_1x1_01_tall_glass_window_3d_model_basecolor.png",Prefab="PF_Window_1x1_01",Definition="WD_Window_Canonical",Id="window.canonical.phase4",Name="Tall Glass Window",W=1,H=1,Rotate=true},
            new Item{Source="tall glass window 3d model_1x2_fixed_v01.glb",Model="SM_Window_TallGlass_1x2_01.fbx",BaseColor="SM_Window_TallGlass_1x2_01_tall_glass_window_3d_model_basecolor.png",Prefab="PF_Window_1x2_01",Definition="WD_Window_TallGlass_1x2_01",Id="window.tall-glass.1x2.01",Name="Tall Glass Window 1x2",W=1,H=2,Rotate=true}};

        public static void Build()
        {
            EnsureFolder(Phase7AssetPaths.ModelFolder);EnsureFolder(Phase7AssetPaths.RawSourceFolder);EnsureFolder(Phase7AssetPaths.FormalPrefabFolder);EnsureFolder("ArtSource/Phase7");
            CopyRepositorySources();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var defs=Items.Select(BuildItem).ToArray();
            Publish(Phase7AssetPaths.WallMountedProductionCataloguePath,WallMountedCatalogueKind.WallDecor,defs.Take(3));Publish(Phase7AssetPaths.WindowCataloguePath,WallMountedCatalogueKind.Windows,defs.Skip(3));
            WriteManifest();AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        [MenuItem("AnimalCafe/Phase 7/Intake Studio Owner Formal Sources")]
        public static void IntakeStudioOwnerSources()
        {
            EnsureFolder(Phase7AssetPaths.RawSourceFolder);
            foreach(var pair in SourceSha256)
            {
                var source=Path.Combine(StudioOwnerDropRoot,pair.Key);
                if(!File.Exists(source)||!Hash(source).Equals(pair.Value,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Missing or changed Studio Owner source: "+pair.Key);
                var dest=pair.Key.EndsWith(".png",StringComparison.OrdinalIgnoreCase)
                    ? Phase7AssetPaths.TextureFolder+"/T_WallDecor_ShibaPortrait_v01.png"
                    : Phase7AssetPaths.RawSourceFolder+"/"+pair.Key;
                File.Copy(source,Path.GetFullPath(dest),true);
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        private static void CopyRepositorySources()
        {
            foreach(var pair in SourceSha256.Where(pair=>!pair.Key.EndsWith(".png",StringComparison.OrdinalIgnoreCase)))
            {
                var source=Phase7AssetPaths.RawSourceFolder+"/"+pair.Key;
                if(!File.Exists(source)||!Hash(source).Equals(pair.Value,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Missing or changed repository raw source: "+pair.Key);
            }
            var portrait=Phase7AssetPaths.TextureFolder+"/T_WallDecor_ShibaPortrait_v01.png";
            if(!File.Exists(portrait)||!Hash(portrait).Equals(SourceSha256["T_WallDecor_ShibaPortrait_v01.png"],StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Missing or changed repository portrait source.");
            foreach(var item in Items)
            {
                foreach(var file in new[]{item.Model,item.BaseColor})
                {
                    var source="ArtSource/Phase7/Derived/"+file;
                    if(!File.Exists(source)||!Hash(source).Equals(DerivedSha256[file],StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Missing or changed deterministic derived asset: "+file);
                }
                File.Copy("ArtSource/Phase7/Derived/"+item.Model,Path.GetFullPath(Phase7AssetPaths.ModelFolder+"/"+item.Model),true);
                File.Copy("ArtSource/Phase7/Derived/"+item.BaseColor,Path.GetFullPath(BaseColorPath(item)),true);
            }
        }
        private static string BaseColorPath(Item item)=>Phase7AssetPaths.TextureFolder+"/T_"+Path.GetFileNameWithoutExtension(item.Model)+"_BaseColor.png";
        private static WallMountedDefinitionAsset BuildItem(Item item)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Phase7AssetPaths.ModelFolder+"/"+item.Model);if(model==null)throw new InvalidOperationException("GLB import failed: "+item.Model);
            var root=new GameObject(item.Prefab);
            try{var visual=UnityEngine.Object.Instantiate(model,root.transform);visual.name="Visual";visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=item.Rotate?Quaternion.Euler(0,-90,0):item.RotateX?Quaternion.Euler(-90,item.FlipFront?180f:0f,0):item.FlipFront?Quaternion.Euler(0,180f,0):Quaternion.identity;visual.transform.localScale=Vector3.one;RemovePhysics(visual);ApplyTechnicalMaterials(visual,item);Normalize(visual,item);var b=BoundsOf(root);var c=root.AddComponent<BoxCollider>();c.center=b.center;c.size=b.size;c.isTrigger=true;var prefab=PrefabUtility.SaveAsPrefabAsset(root,Phase7AssetPaths.FormalPrefabFolder+"/"+item.Prefab+".prefab");var d=EnsureAsset<WallMountedDefinitionAsset>(Phase7AssetPaths.DefinitionFolder+"/"+item.Definition+".asset");Set(d,"definitionId",item.Id);Set(d,"displayName",item.Name);Set(d,"footprintWidth",item.W);Set(d,"footprintHeight",item.H);Set(d,"prefab",prefab);var thumbnailPath=$"{Phase7AssetPaths.ThumbnailFolder}/TH_{item.Id.Replace('.','_').Replace('-','_')}.png";var thumbnail=AssetDatabase.LoadAssetAtPath<Sprite>(thumbnailPath);if(thumbnail==null)throw new InvalidOperationException("Missing authored wall-mounted thumbnail: "+thumbnailPath);Set(d,"thumbnail",thumbnail);Set(d,"maxVisualDepth",Mathf.Min(.35f,BoundsOf(prefab).size.z));return d;}finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        private static void Normalize(GameObject visual,Item item){var b=BoundsOf(visual);var faceScale=Mathf.Min((item.W*.94f)/Mathf.Max(.0001f,b.size.x),(item.H*.94f)/Mathf.Max(.0001f,b.size.y));visual.transform.localScale*=faceScale;b=BoundsOf(visual);var targetDepth=item.Monitor?WallMountedDefinitionAsset.MaximumVisualDepth*.99f:.34f;var depthFactor=Mathf.Min(1f,targetDepth/Mathf.Max(.0001f,b.size.z));var scale=visual.transform.localScale;if(item.Rotate)scale.x*=depthFactor;else if(item.RotateX)scale.y*=depthFactor;else scale.z*=depthFactor;visual.transform.localScale=scale;b=BoundsOf(visual);visual.transform.localPosition+=new Vector3(-b.center.x,-b.min.y,-b.min.z);}
        private static void ApplyTechnicalMaterials(GameObject visual,Item item)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit")??throw new InvalidOperationException("URP/Lit missing");
            var baseTexturePath=BaseColorPath(item);var baseImporter=AssetImporter.GetAtPath(baseTexturePath) as TextureImporter;if(baseImporter!=null){baseImporter.wrapMode=TextureWrapMode.Repeat;baseImporter.mipmapEnabled=true;baseImporter.SaveAndReimport();}
            var baseMaterial=EnsureMaterial(Phase7AssetPaths.MaterialFolder+"/M_"+Path.GetFileNameWithoutExtension(item.Model)+"_BaseColor.mat",shader);baseMaterial.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(baseTexturePath));baseMaterial.SetColor("_BaseColor",Color.white);baseMaterial.SetFloat("_Surface",0f);baseMaterial.renderQueue=-1;EditorUtility.SetDirty(baseMaterial);
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))renderer.sharedMaterials=Enumerable.Repeat(baseMaterial,Math.Max(1,renderer.sharedMaterials.Length)).ToArray();
            if(item.Id=="wall-decor.shiba-painting.01")
            {
                var texturePath=Phase7AssetPaths.TextureFolder+"/T_WallDecor_ShibaPortrait_v01.png";var importer=AssetImporter.GetAtPath(texturePath) as TextureImporter;if(importer!=null){importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;importer.SaveAndReimport();}
                var material=EnsureMaterial(Phase7AssetPaths.MaterialFolder+"/M_WallDecor_ShibaPortrait_01.mat",shader);material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));material.SetColor("_BaseColor",Color.white);ConfigureAlphaBlend(material);EditorUtility.SetDirty(material);
                var targets=visual.GetComponentsInChildren<Renderer>(true).Where(x=>x.name.IndexOf("Canvas",StringComparison.OrdinalIgnoreCase)>=0).ToArray();if(targets.Length==0)targets=visual.GetComponentsInChildren<Renderer>(true);foreach(var renderer in targets)renderer.sharedMaterials=Enumerable.Repeat(material,Math.Max(1,renderer.sharedMaterials.Length)).ToArray();
            }
            if(item.Id.StartsWith("window.",StringComparison.Ordinal))
            {
                var glass=EnsureMaterial(Phase7AssetPaths.MaterialFolder+"/M_Window_TallGlass_Transparent.mat",shader);glass.name="M_Window_TallGlass_Transparent";glass.SetColor("_BaseColor",new Color(.72f,.9f,.96f,.28f));glass.SetFloat("_Surface",1f);glass.SetFloat("_Blend",0f);glass.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);glass.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);glass.SetInt("_ZWrite",0);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;EditorUtility.SetDirty(glass);
                foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true).Where(x=>x.name.IndexOf("Glass",StringComparison.OrdinalIgnoreCase)>=0))renderer.sharedMaterials=Enumerable.Repeat(glass,Math.Max(1,renderer.sharedMaterials.Length)).ToArray();
            }
        }
        private static void ConfigureAlphaBlend(Material material){material.SetFloat("_Surface",1f);material.SetFloat("_Blend",0f);material.SetFloat("_AlphaClip",0f);material.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);material.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);material.SetInt("_ZWrite",0);material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.DisableKeyword("_ALPHATEST_ON");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");material.DisableKeyword("_ALPHAMODULATE_ON");material.SetOverrideTag("RenderType","Transparent");material.SetShaderPassEnabled("DepthOnly",false);material.SetShaderPassEnabled("SHADOWCASTER",false);material.renderQueue=3000;}
        private static Material EnsureMaterial(string path,Shader shader){var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material)return material;material=new Material(shader);AssetDatabase.CreateAsset(material,path);return material;}
        private static void RemovePhysics(GameObject root){foreach(var x in root.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(x);foreach(var x in root.GetComponentsInChildren<Rigidbody>(true))UnityEngine.Object.DestroyImmediate(x);foreach(var x in root.GetComponentsInChildren<NavMeshObstacle>(true))UnityEngine.Object.DestroyImmediate(x);}
        private static Bounds BoundsOf(GameObject root){var rs=root.GetComponentsInChildren<Renderer>(true);if(rs.Length==0)throw new InvalidOperationException(root.name+" has no Renderer");var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;}
        private static void Publish(string path,WallMountedCatalogueKind kind,IEnumerable<WallMountedDefinitionAsset> defs){var c=EnsureAsset<WallMountedCatalogueAsset>(path);Set(c,"kind",(int)kind);SetArray(c,"entries",defs.Cast<UnityEngine.Object>().ToArray());}
        private static void WriteManifest(){var raw=SourceSha256.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>"    { \"source\": \""+x.Key+"\", \"sha256\": \""+x.Value+"\" }");var derived=DerivedSha256.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>"    { \"derived\": \""+x.Key+"\", \"sha256\": \""+x.Value+"\" }");File.WriteAllText(Phase7AssetPaths.ProvenanceManifestPath,"{\n  \"sourceRoot\": \"Blender Model Item/Phase 7\",\n  \"licenseBasis\": \"StudioOwnerSupplied\",\n  \"repositoryAuthority\": \"Assets/Art/Phase7/RawSources + ArtSource/Phase7/Derived\",\n  \"nonDestructiveWrapperIntake\": true,\n  \"sources\": [\n"+string.Join(",\n",raw)+"\n  ],\n  \"derived\": [\n"+string.Join(",\n",derived)+"\n  ]\n}\n");}
        private static string Hash(string path){using var s=File.OpenRead(path);using var h=SHA256.Create();return BitConverter.ToString(h.ComputeHash(s)).Replace("-","");}
        private static void EnsureFolder(string path){var p=path.Split('/');var cur=p[0];for(var i=1;i<p.Length;i++){var next=cur+"/"+p[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(cur,p[i]);cur=next;}}
        private static T EnsureAsset<T>(string path)where T:ScriptableObject{var a=AssetDatabase.LoadAssetAtPath<T>(path);if(a)return a;a=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(a,path);return a;}
        private static void Set(UnityEngine.Object o,string n,object v){var so=new SerializedObject(o);var p=so.FindProperty(n);if(v is string s)p.stringValue=s;else if(v is int i)p.intValue=i;else if(v is float f)p.floatValue=f;else p.objectReferenceValue=v as UnityEngine.Object;so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(o);}
        private static void SetArray(UnityEngine.Object o,string n,UnityEngine.Object[] vs){var so=new SerializedObject(o);var p=so.FindProperty(n);p.arraySize=vs.Length;for(var i=0;i<vs.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=vs[i];so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(o);}
    }
}

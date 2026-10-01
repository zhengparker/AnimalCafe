using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnimalCafe.Core.Time;
using AnimalCafe.Navigation;
using AnimalCafe.Decoration;
using AnimalCafe.Content;
using AnimalCafe.UI;
using AnimalCafe.EditorTools.AssetPipeline;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.Phase11
{
    public static class Phase11NavigationSceneSetup
    {
        public const string ScenePath="Assets/Scenes/Validation/Phase11Navigation.unity";
        public static void WireResumeBlockFonts()
        {
            RequireCleanAssets();
            var previous=SceneManager.GetActiveScene();
            var source=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Phase5/Fonts/NotoSansSC-Regular.otf");
            if(source==null) throw new InvalidOperationException("Missing existing Chinese source font");
            foreach(var path in new[]{ScenePath,"Assets/Scenes/MainCafe.unity"})
            {
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    foreach(var panel in All<TimeControlPanel>(scene)) Set(panel,"resumeBlockSourceFont",source);
                    EditorSceneManager.MarkSceneDirty(scene);
                    if(!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Font wiring save failed");
                }
                finally { EditorSceneManager.CloseScene(scene,true); }
            }
            if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous);
            Debug.Log("AC_PHASE11_REASON_FONT_WIRED");
        }
        [MenuItem("Tools/AnimalCafe/Phase 11/Wire Business Navigation")]
        public static void WireBusinessNavigation()
        {
            RequireCleanAssets();
            var previous=SceneManager.GetActiveScene();
            var target=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            var source=EditorSceneManager.OpenScene("Assets/Scenes/MainCafe.unity",OpenSceneMode.Additive);
            try
            {
                if(All<DecorationModeController>(target).Any()) throw new InvalidOperationException("Validation already wired");
                var fixture=All<NavigationValidationController>(target).Single();
                var world=All<NavigationWorld>(target).Single();
                var oldTime=All<GameTimeService>(target).Single();
                var oldCamera=All<UnityEngine.Camera>(target).Single();
                var viewPosition=oldCamera.transform.position; var viewRotation=oldCamera.transform.rotation;
                var viewSize=oldCamera.orthographicSize;
                UnityEngine.Object.DestroyImmediate(All<EventSystem>(target).Single().gameObject);
                UnityEngine.Object.DestroyImmediate(oldCamera.gameObject);
                var controller=All<DecorationModeController>(source).Single();
                var grid=Read<Transform>(controller,"gridRoot");
                var offset=new Vector3(3,0,0)-grid.position;
                foreach(var root in source.GetRootGameObjects())
                {
                    SceneManager.MoveGameObjectToScene(root,target);
                    if(!(root.transform is RectTransform)) root.transform.position+=offset;
                }
                var environment=target.GetRootGameObjects().Single(r=>r.name=="P4_Environment");
                foreach(var collider in environment.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
                foreach(var renderer in environment.GetComponentsInChildren<Renderer>(true)) renderer.enabled=false;
                var time=All<GameTimeService>(target).Single(t=>t!=oldTime);
                foreach(var panel in All<TimeControlPanel>(target))
                    panel.Configure(time,Read<Button>(panel,"pauseButton"),Read<Button>(panel,"normalButton"),Read<Button>(panel,"fastButton"));
                UnityEngine.Object.DestroyImmediate(oldTime);
                var camera=All<UnityEngine.Camera>(target).Single();
                camera.transform.SetPositionAndRotation(viewPosition,viewRotation); camera.orthographicSize=viewSize;
                camera.rect=new Rect(.26f,0,.74f,1);
                Set(controller,"floorCollider",fixture.Floor);
                var bridge=Wire(controller,world,fixture.Floor,fixture.Solids.Cast<Collider>().ToArray(),true,time);
                Set(fixture,"decorationController",controller); Set(fixture,"businessRuntime",Read<CafeLayoutRuntime>(controller,"layoutRuntime"));
                Set(fixture,"businessCatalog",Read<FurnitureContentCatalog>(controller,"contentCatalog"));
                Set(fixture,"businessBridge",bridge); Set(fixture,"gameTimeService",time);
                // Fixture camera needs wider bounds; the existing controller receives a runtime settings copy.
                Set(fixture,"fixtureCamera",camera); Set(fixture,"cameraController",Read<AnimalCafe.Camera.CafeCameraController>(controller,"cameraController"));
                Set(fixture,"cameraSettings",Read<AnimalCafe.Camera.CameraSettings>(controller,"cameraSettings"));
                var fixtureLabel=All<TMP_Text>(target).FirstOrDefault(t=>t.text.Contains("Fixture only;"));
                if(fixtureLabel!=null) fixtureLabel.text="Scenarios walk from current positions.\nDecoration uses the real P8 service area.\nBlocked paths keep business paused.\nLeave Play to reload authored starts.";
                EditorSceneManager.MarkSceneDirty(target);
                if(!EditorSceneManager.SaveScene(target)) throw new InvalidOperationException("Validation save failed");
            }
            finally
            {
                EditorSceneManager.CloseScene(source,true); EditorSceneManager.CloseScene(target,true);
                if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            var main=EditorSceneManager.OpenScene("Assets/Scenes/MainCafe.unity",OpenSceneMode.Additive);
            try
            {
                var controller=All<DecorationModeController>(main).Single();
                var root=new GameObject("Phase11 Passive Navigation"); SceneManager.MoveGameObjectToScene(root,main);
                var world=root.AddComponent<NavigationWorld>();
                var floor=Read<Collider>(controller,"floorCollider") as BoxCollider;
                Wire(controller,world,floor,Array.Empty<Collider>(),false,All<GameTimeService>(main).Single());
                EditorSceneManager.MarkSceneDirty(main);
                if(!EditorSceneManager.SaveScene(main)) throw new InvalidOperationException("MainCafe save failed");
            }
            finally { EditorSceneManager.CloseScene(main,true); if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous); }
            Debug.Log("AC_PHASE11_BUSINESS_WIRED");
        }
        private static NavigationDecorationBridge Wire(DecorationModeController controller,NavigationWorld world,BoxCollider floor,Collider[] solids,bool enforce,GameTimeService time)
        {
            var adapter=world.gameObject.AddComponent<NavigationLayoutAdapter>();
            var bridge=world.gameObject.AddComponent<NavigationDecorationBridge>();
            var walls=ReadArray<WallSurfaceAuthoring>(controller,"phase7WallAuthoring");
            var definitions=AssetDatabase.FindAssets("t:WallMountedDefinitionAsset").Select(g=>AssetDatabase.LoadAssetAtPath<WallMountedDefinitionAsset>(AssetDatabase.GUIDToAssetPath(g))).ToArray();
            adapter.Configure(Read<CafeLayoutRuntime>(controller,"layoutRuntime"),Read<FurnitureContentCatalog>(controller,"contentCatalog"),Read<Transform>(controller,"gridRoot"),floor,solids,world,enforce,definitions,walls);
            adapter.ConfigureRepresentationRoots(Read<Transform>(controller,"furnitureRepresentationRoot"),Read<Transform>(controller,"surfaceMountedRepresentationRoot"));
            bridge.Configure(adapter); bridge.ConfigureTime(time); Set(controller,"navigationBridge",bridge);
            return bridge;
        }
        private static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        private static T Read<T>(UnityEngine.Object owner,string field) where T:UnityEngine.Object => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        private static T[] ReadArray<T>(UnityEngine.Object owner,string field) where T:UnityEngine.Object
        { var array=new SerializedObject(owner).FindProperty(field); return Enumerable.Range(0,array.arraySize).Select(i=>(T)array.GetArrayElementAtIndex(i).objectReferenceValue).ToArray(); }
        private static void Set(UnityEngine.Object owner,string field,UnityEngine.Object value)
        { var data=new SerializedObject(owner); data.FindProperty(field).objectReferenceValue=value; data.ApplyModifiedPropertiesWithoutUndo(); }
        [MenuItem("Tools/AnimalCafe/Phase 11/Create Navigation Validation Scene")]
        public static void CreateValidationScene()
        {
            RequireCleanAssets();
            foreach(var species in new[]{"Shiba","Westie"})
            {
                var path=Phase11CharacterBuilder.CharacterRoot+"/"+species+"/PF_"+species+"_Navigation.prefab";
                var prefab=PrefabUtility.LoadPrefabContents(path);
                try { if(prefab.GetComponent<NavigationWalkPresenter>()==null) prefab.AddComponent<NavigationWalkPresenter>();
                    PrefabUtility.SaveAsPrefabAsset(prefab,path); }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            var previous=SceneManager.GetActiveScene();
            var mode=string.IsNullOrEmpty(previous.path) && previous.rootCount==0 ? NewSceneMode.Single : NewSceneMode.Additive;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,mode);
            SceneManager.SetActiveScene(scene);
            try
            {
                var sourceRoot=new GameObject("FixtureSources_Task5ReplaceWithConfirmedLayout").transform;
                var floor=Box("Floor",new Vector3(0,-.1f,0),new Vector3(24,.2f,18),sourceRoot,new Color(.68f,.74f,.69f));
                var solids=new List<BoxCollider>();
                solids.Add(Box("NorthWall",new Vector3(0,.7f,8.8f),new Vector3(24,1.4f,.2f),sourceRoot,Color.gray));
                solids.Add(Box("WestWall",new Vector3(-11.8f,.7f,0),new Vector3(.2f,1.4f,18),sourceRoot,Color.gray));
                Corridor(-8,1,sourceRoot,solids); Corridor(-5,.85f,sourceRoot,solids);
                Counter("Assets/Art/Phase4/Prefabs/PF_Furniture_CounterModule_01.prefab",new Vector3(4,0,1),sourceRoot,solids);
                Counter("Assets/Art/Phase4/Prefabs/PF_Validation_Counter_1x3_01.prefab",Vector3.zero,sourceRoot,solids);
                var actors=new NavigationActor[8];
                for(var i=0;i<actors.Length;i++)
                {
                    var species=i%2==0 ? "Shiba" : "Westie";
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Phase11CharacterBuilder.CharacterRoot+"/"+species+"/PF_"+species+"_Navigation.prefab");
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                    go.name=species+"_"+(i+1); go.transform.position=new Vector3(-9+i*2.6f,0,-7);
                    actors[i]=go.GetComponent<NavigationActor>();
                    actors[i].Configure(go.name,actors[i].Agent,actors[i].Proxy,actors[i].ModelAnimator,actors[i].ModelRoot);
                }
                var services=new GameObject("NavigationFixture"); var world=services.AddComponent<NavigationWorld>();
                var time=services.AddComponent<GameTimeService>(); var controller=services.AddComponent<NavigationValidationController>();
                var canvasGo=new GameObject("ValidationPanel",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
                canvasGo.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
                var scaler=canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1600,1000); scaler.matchWidthOrHeight=.5f;
                var panel=new GameObject("Controls",typeof(RectTransform),typeof(Image)); panel.transform.SetParent(canvasGo.transform,false);
                var rect=(RectTransform)panel.transform; rect.anchorMin=new Vector2(0,0); rect.anchorMax=new Vector2(0,1); rect.pivot=new Vector2(0,1);
                rect.sizeDelta=new Vector2(410,0); rect.anchoredPosition=Vector2.zero; panel.GetComponent<Image>().color=new Color(.07f,.1f,.13f,.94f);
                Label(panel.transform,"Phase 11 Navigation\n1 m / 0.85 m corridors at left",new Vector2(15,-12),new Vector2(380,70),23);
                var y=-92f;
                foreach(var id in new[]{"straight","detour","crossing","narrow-wait","same-target","recovery","crowd8","blocked85"})
                { var button=Button(panel.transform,id,new Vector2(15,y),new Vector2(180,40));
                    UnityEventTools.AddStringPersistentListener(button.onClick,controller.RunScenario,id); y-=47; }
                var pause=Button(panel.transform,"Pause",new Vector2(15,y),new Vector2(110,40));
                var normal=Button(panel.transform,"1x",new Vector2(140,y),new Vector2(110,40));
                var fast=Button(panel.transform,"2x",new Vector2(265,y),new Vector2(110,40));
                panel.AddComponent<TimeControlPanel>().Configure(time,pause,normal,fast); y-=48;
                var cancel=Button(panel.transform,"Cancel requests",new Vector2(15,y),new Vector2(360,40));
                UnityEventTools.AddPersistentListener(cancel.onClick,controller.CancelAll); y-=52;
                var status=Label(panel.transform,"Ready",new Vector2(15,y),new Vector2(380,230),17); y-=245;
                Label(panel.transform,"Scenarios walk to staging positions.\nCancel keeps actors where they stopped.\nTo restore authored starts: leave Play, reopen.\nRecovery: occupied target, retry, walk fallback.\nFixture only; business integration pending.",new Vector2(15,y),new Vector2(380,140),16);
                controller.Configure(world,actors,floor,solids.ToArray(),status);
                var eventSystem=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
                var camera=new GameObject("Main Camera",typeof(UnityEngine.Camera)); camera.tag="MainCamera";
                camera.transform.position=new Vector3(18,23,-24); camera.transform.LookAt(new Vector3(-1,0,0));
                var cam=camera.GetComponent<UnityEngine.Camera>(); cam.orthographic=true; cam.orthographicSize=12.7f;
                cam.backgroundColor=new Color(.15f,.19f,.23f); cam.clearFlags=CameraClearFlags.SolidColor;
                cam.rect=new Rect(.26f,0,.74f,1);
                var light=new GameObject("Sun",typeof(Light)); light.GetComponent<Light>().type=LightType.Directional;
                light.GetComponent<Light>().intensity=1.7f; light.transform.rotation=Quaternion.Euler(50,-30,0);
                RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
                if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new InvalidOperationException("Scene save failed");
                Debug.Log("AC_PHASE11_SCENE_BUILT "+ScenePath);
            }
            finally { EditorSceneManager.CloseScene(scene,true); if(previous.IsValid()&&previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }
        private static void RequireCleanAssets()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play before authoring the fixture");
            for(var i=0;i<SceneManager.sceneCount;i++)
            {
                var scene=SceneManager.GetSceneAt(i);
                if(scene.isDirty || scene.path==ScenePath) throw new InvalidOperationException("Save/revert dirty scenes and close the validation scene before rebuilding");
            }
            var dirty=Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Where(a=>a!=null&&!(a is SceneAsset)&&EditorUtility.IsPersistent(a)&&EditorUtility.IsDirty(a)&&!ProjectAssetEditSafety.IsGeneratedDynamicFontAtlas(a))
                .Select(AssetDatabase.GetAssetPath).Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)&&File.Exists(p)).Distinct().ToArray();
            if(dirty.Length>0) throw new InvalidOperationException("Save/revert loaded dirty assets: "+string.Join(", ",dirty));
        }
        private static void Corridor(float x,float width,Transform parent,List<BoxCollider> solids)
        {
            foreach(var side in new[]{-1,1}) solids.Add(Box("Corridor_"+width+"_"+side,new Vector3(x+side*(width/2+.1f),.7f,0),new Vector3(.2f,1.4f,5),parent,new Color(.55f,.59f,.66f)));
        }
        private static BoxCollider Box(string name,Vector3 position,Vector3 size,Transform parent,Color color)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(parent,false);
            go.transform.position=position; go.transform.localScale=size;
            // Built-in material plus per-object color is authored as a scene material reference.
            var renderer=go.GetComponent<Renderer>(); var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color=color; renderer.sharedMaterial=material;
            return go.GetComponent<BoxCollider>();
        }
        private static void Counter(string path,Vector3 position,Transform parent,List<BoxCollider> solids)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            go.transform.SetParent(parent,false); go.transform.position=position;
            solids.AddRange(go.GetComponentsInChildren<BoxCollider>());
        }
        private static TMP_Text Label(Transform parent,string text,Vector2 position,Vector2 size,float fontSize)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform; rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1); rect.anchoredPosition=position; rect.sizeDelta=size;
            var label=go.GetComponent<TextMeshProUGUI>(); label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/UI/Phase8/Fonts/NotoSansSC-Phase8 SDF.asset");
            label.text=text; label.fontSize=fontSize; label.color=Color.white; label.raycastTarget=false; return label;
        }
        private static Button Button(Transform parent,string name,Vector2 position,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(parent,false);
            var rect=(RectTransform)go.transform; rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1); rect.anchoredPosition=position; rect.sizeDelta=size;
            go.GetComponent<Image>().color=new Color(.20f,.32f,.38f); var button=go.GetComponent<Button>(); button.targetGraphic=go.GetComponent<Image>();
            var label=Label(go.transform,name,Vector2.zero,size,19); label.alignment=TextAlignmentOptions.Center; return button;
        }
    }
}

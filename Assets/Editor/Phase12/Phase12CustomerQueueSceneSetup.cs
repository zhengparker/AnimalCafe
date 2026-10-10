using System;
using System.Linq;
using AnimalCafe.Customers;
using AnimalCafe.Navigation;
using AnimalCafe.Decoration;
using AnimalCafe.Core.Time;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace AnimalCafe.EditorTools.Phase12
{
    public static class Phase12CustomerQueueSceneSetup
    {
        public const string ScenePath="Assets/Scenes/Validation/Phase12CustomerQueue.unity";
        [MenuItem("Tools/AnimalCafe/Phase 12/Create Customer Queue Validation Scene")]
        public static void CreateValidationScene() => BuildScene(true);
        [MenuItem("Tools/AnimalCafe/Phase 12/Wire MainCafe Customer Flow")]
        public static void WireMainCafe() => BuildScene(false);
        public static void BuildAll() { CreateValidationScene(); WireMainCafe(); }
        private static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        private static T Read<T>(UnityEngine.Object owner,string field) where T:UnityEngine.Object => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        private static void Set(UnityEngine.Object owner,string field,UnityEngine.Object value)
        { var data=new SerializedObject(owner); data.FindProperty(field).objectReferenceValue=value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Flag(UnityEngine.Object owner,string field,bool value)
        { var data=new SerializedObject(owner); data.FindProperty(field).boolValue=value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void BuildScene(bool validation)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play before creating scene");
            var previous=SceneManager.GetActiveScene();
            if(previous.IsValid() && previous.isDirty) throw new InvalidOperationException("Save your active scene first");
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/MainCafe.unity",OpenSceneMode.Additive);
            try
            {
                var decoration=All<DecorationModeController>(scene).Single();
                var runtime=Read<CafeLayoutRuntime>(decoration,"layoutRuntime"); var grid=Read<Transform>(decoration,"gridRoot");
                var world=All<NavigationWorld>(scene).Single(); var adapter=All<NavigationLayoutAdapter>(scene).Single(); var bridge=All<NavigationDecorationBridge>(scene).Single();
                Flag(adapter,"enforceBusinessReadiness",validation); bridge.Configure(adapter);
                var time=All<GameTimeService>(scene).Single(); bridge.ConfigureTime(time);
                var root=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="Phase12 Customer Flow");
                if(root==null) { root=new GameObject("Phase12 Customer Flow"); SceneManager.MoveGameObjectToScene(root,scene); }
                var flow=root.GetComponent<CustomerFlowController>() ?? root.AddComponent<CustomerFlowController>();
                var spawn=Marker(root.transform,"Entrance Spawn",grid.TransformPoint(new Vector3(3.5f,0,.65f)));
                var exit=Marker(root.transform,"Entrance Exit",grid.TransformPoint(new Vector3(4.5f,0,.65f)));
                var prefabs=new[]{"Shiba","Westie"}.Select(s=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase11/Characters/"+s+"/PF_"+s+"_Navigation.prefab").GetComponent<NavigationActor>()).ToArray();
                flow.Configure(world,adapter,time,prefabs,spawn,exit,null,runtime,grid,decoration);
                if(validation) BuildHud(root,scene,flow,runtime,decoration,bridge,time);
                else
                {
                    var old=root.GetComponent<CustomerQueueValidationController>(); if(old!=null) UnityEngine.Object.DestroyImmediate(old);
                    var hud=root.transform.Find("P12 Validation HUD"); if(hud!=null) UnityEngine.Object.DestroyImmediate(hud.gameObject);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene,validation?ScenePath:"Assets/Scenes/MainCafe.unity")) throw new InvalidOperationException("P12 scene save failed");
                Debug.Log("AC_P12_SCENE_READY "+scene.path);
            }
            finally { EditorSceneManager.CloseScene(scene,true); if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }
        private static Transform Marker(Transform parent,string name,Vector3 point)
        { var marker=parent.Find(name); if(marker==null) { marker=new GameObject(name).transform; marker.SetParent(parent); } marker.position=point; return marker; }
        private static void BuildHud(GameObject root,Scene scene,CustomerFlowController flow,CafeLayoutRuntime runtime,
            DecorationModeController decoration,NavigationDecorationBridge bridge,GameTimeService time)
        {
            var existing=root.transform.Find("P12 Validation HUD"); if(existing!=null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var canvasGo=new GameObject("P12 Validation HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster)); canvasGo.transform.SetParent(root.transform);
            var canvas=canvasGo.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=50;
            var scaler=canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1080,1920); scaler.matchWidthOrHeight=.5f;
            var panel=new GameObject("Panel",typeof(RectTransform),typeof(Image)); panel.transform.SetParent(canvasGo.transform,false);
            var rect=panel.GetComponent<RectTransform>(); rect.anchorMin=new Vector2(0,0); rect.anchorMax=new Vector2(1,0); rect.pivot=new Vector2(.5f,0); rect.sizeDelta=new Vector2(0,340); panel.GetComponent<Image>().color=new Color(.08f,.1f,.12f,.93f); panel.GetComponent<Image>().raycastTarget=false;
            var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/UI/Phase5/Fonts/NotoSansSC-Regular.otf");
            var label=Label(panel.transform,"Status",font); label.alignment=TextAnchor.UpperLeft; label.fontSize=22;
            Place(label.rectTransform,new Vector2(20,-20),new Vector2(1040,210));
            var leave=Button(panel.transform,"LetFrontLeave","让队首离开",font,new Vector2(20,-250));
            var retry=Button(panel.transform,"Retry","修复后重试",font,new Vector2(350,-250));
            var controller=root.GetComponent<CustomerQueueValidationController>() ?? root.AddComponent<CustomerQueueValidationController>();
            Set(controller,"flow",flow); Set(controller,"runtime",runtime); Set(controller,"catalog",Read<UnityEngine.Object>(decoration,"contentCatalog"));
            Set(controller,"decoration",decoration); Set(controller,"bridge",bridge); Set(controller,"time",time); Set(controller,"status",label); Set(controller,"validationHud",canvasGo);
            Set(controller,"leaveButton",leave); Set(controller,"retryButton",retry); Flag(controller,"seedValidationLayout",true);
        }
        private static Text Label(Transform parent,string name,Font font)
        { var go=new GameObject(name,typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false); var text=go.GetComponent<Text>(); text.font=font; text.color=Color.white; text.raycastTarget=false; return text; }
        private static void Place(RectTransform rect,Vector2 position,Vector2 size)
        { rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1); rect.anchoredPosition=position; rect.sizeDelta=size; }
        private static Button Button(Transform parent,string name,string caption,Font font,Vector2 position)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button)); go.transform.SetParent(parent,false);
            Place(go.GetComponent<RectTransform>(),position,new Vector2(300,65)); go.GetComponent<Image>().color=new Color(.25f,.38f,.48f);
            var label=Label(go.transform,"Label",font); label.text=caption; label.fontSize=26; label.alignment=TextAnchor.MiddleCenter;
            var rect=label.rectTransform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero;
            return go.GetComponent<Button>();
        }
    }
}


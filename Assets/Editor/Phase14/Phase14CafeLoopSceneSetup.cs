using System;
using System.IO;
using System.Linq;
using AnimalCafe.CafeLoop;
using AnimalCafe.Content;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Navigation;
using AnimalCafe.UI.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace AnimalCafe.EditorTools.Phase14
{
    public static class Phase14CafeLoopSceneSetup
    {
        public const string MainPath="Assets/Scenes/MainCafe.unity";
        public const string ScenePath="Assets/Scenes/Validation/Phase14CafeLoop.unity";
        public const string SharedScenePath="Assets/Scenes/Validation/Phase14SharedPickup.unity";
        [MenuItem("Tools/AnimalCafe/Phase 14/Install Cafe Loop in MainCafe")] public static void BuildMainCafe() => Build(MainPath,false);
        [MenuItem("Tools/AnimalCafe/Phase 14/Create Cafe Loop Validation Scene")] public static void BuildValidationScene() => Build(ScenePath,false);
        [MenuItem("Tools/AnimalCafe/Phase 14/Create Shared Pickup Validation Scene")] public static void BuildSharedValidationScene() => Build(SharedScenePath,true);
        private static void Build(string path,bool shared)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play before installing P14.");
            for(var i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save dirty Scenes before installing P14.");
            var previous=SceneManager.GetActiveScene();
            if(path!=MainPath && !File.Exists(path) && !AssetDatabase.CopyAsset(MainPath,path))
                throw new InvalidOperationException("Could not copy independent P14 validation Scene.");
            var loaded=SceneManager.GetSceneByPath(path); var close=!(loaded.IsValid() && loaded.isLoaded);
            var scene=close?EditorSceneManager.OpenScene(path,OpenSceneMode.Additive):loaded;
            try
            {
                SceneManager.SetActiveScene(scene);
                var customers=All<CustomerFlowController>(scene).Single(); var employees=All<EmployeeFlowController>(scene).Single();
                var cups=All<PickupCupDisplay>(scene).Single(); var decoration=All<DecorationModeController>(scene).Single();
                ConfigureCustomerCarry(customers);
                var time=All<GameTimeService>(scene).Single(); var adapter=All<NavigationLayoutAdapter>(scene).Single();
                var root=scene.GetRootGameObjects().SingleOrDefault(r=>r.name=="P14 Cafe Loop");
                if(root!=null && root.GetComponent<CafeLoopController>()==null) throw new InvalidOperationException("Unrecognized P14 root.");
                if(root==null) { root=new GameObject("P14 Cafe Loop"); SceneManager.MoveGameObjectToScene(root,scene); }
                var owner=root.GetComponent<CafeLoopController>() ?? root.AddComponent<CafeLoopController>();
                owner.Configure(customers,employees,cups,time,adapter,decoration);
                var tools=root.GetComponent<CafeLoopValidationController>() ?? root.AddComponent<CafeLoopValidationController>();
                tools.Configure(owner);
                Set(tools,"customers",customers); Set(tools,"employees",employees); Set(tools,"time",time);
                Set(tools,"adapter",adapter); Set(tools,"decoration",decoration);
                Set(tools,"runtime",All<CafeLayoutRuntime>(scene).Single());
                Set(tools,"catalog",Ref<FurnitureContentCatalog>(decoration,"contentCatalog"));
                Set(tools,"blockerPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase11/Characters/Shiba/PF_Shiba_Navigation.prefab").GetComponent<NavigationActor>());
                Set(tools,"departure",Ref<Transform>(customers,"exit"));
                Flag(tools,"seedValidationLayout",path!=MainPath); Flag(tools,"sharedFixture",shared);
                var old=All<EmployeeTaskValidationController>(scene).Single(); old.enabled=false;
                var hud=Ref<GameObject>(old,"hud"); var panel=hud.transform.Find("Panel"); var status=panel.Find("Status").GetComponent<Text>();
                hud.GetComponent<CanvasScaler>().matchWidthOrHeight=1; // Height fit keeps the readonly HUD compact in landscape.
                foreach(var button in panel.GetComponentsInChildren<Button>(true)) button.gameObject.SetActive(false);
                var safe=panel.GetComponent<SafeAreaContainer>() ?? panel.gameObject.AddComponent<SafeAreaContainer>();
                safe.AutoApplyRuntimeSafeArea=true;
                var pr=(RectTransform)panel; pr.anchorMin=Vector2.zero; pr.anchorMax=Vector2.one; pr.offsetMin=pr.offsetMax=Vector2.zero;
                panel.GetComponent<Image>().color=Color.clear; panel.GetComponent<Image>().raycastTarget=false;
                var background=panel.Find("P14 Readonly Background");
                if(background==null) { var go=new GameObject("P14 Readonly Background",typeof(RectTransform),typeof(Image)); go.transform.SetParent(panel,false); background=go.transform; }
                background.SetAsFirstSibling(); var br=(RectTransform)background; br.anchorMin=Vector2.zero; br.anchorMax=new Vector2(1,0);
                br.pivot=new Vector2(.5f,0); br.offsetMin=new Vector2(8,8); br.offsetMax=new Vector2(-8,192);
                background.GetComponent<Image>().color=new Color(.08f,.1f,.12f,.93f); background.GetComponent<Image>().raycastTarget=false;
                var sr=status.rectTransform; sr.anchorMin=Vector2.zero; sr.anchorMax=new Vector2(1,0); sr.pivot=new Vector2(.5f,0);
                sr.offsetMin=new Vector2(14,14); sr.offsetMax=new Vector2(-14,186);
                status.fontSize=12; status.resizeTextForBestFit=false; status.horizontalOverflow=HorizontalWrapMode.Wrap;
                status.verticalOverflow=VerticalWrapMode.Truncate; status.raycastTarget=false; status.alignment=TextAnchor.UpperLeft;
                var view=root.GetComponent<CafeLoopStatusView>() ?? root.AddComponent<CafeLoopStatusView>();
                Set(view,"status",status); Set(view,"hud",hud); view.Bind(owner);
                if(path!=MainPath) customers.AutoSpawn=true;
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene,path)) throw new InvalidOperationException("P14 Scene save failed.");
                Debug.Log("AC_P14_SCENE_READY "+path);
            }
            finally { if(close) EditorSceneManager.CloseScene(scene,true); if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }
        private static T[] All<T>(Scene scene) where T:Component => scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
        private static void ConfigureCustomerCarry(CustomerFlowController customers)
        {
            var data=new SerializedObject(customers); var prefabs=data.FindProperty("prefabs");
            var appearances=new CustomerCarryPresenter.Appearance[prefabs.arraySize];
            for(var i=0;i<appearances.Length;i++)
            {
                var prefab=(NavigationActor)prefabs.GetArrayElementAtIndex(i).objectReferenceValue;
                var species=prefab.ActorId;
                var employee=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase13/Employees/"+species+"/PF_"+species+"_Employee.prefab");
                if(employee==null) throw new InvalidOperationException("Accepted Carry presentation unavailable for "+species);
                var source=new SerializedObject(employee.GetComponent<EmployeeVisualPresenter>());
                appearances[i]=new CustomerCarryPresenter.Appearance
                {
                    CustomerPrefab=EnsureCustomerPrefab(species,employee),CarryController=employee.GetComponent<NavigationActor>().ModelAnimator.runtimeAnimatorController,
                    CupLocalPosition=source.FindProperty("cupLocalPosition").vector3Value,
                    CupLocalRotation=source.FindProperty("cupLocalRotation").quaternionValue,
                    CupLocalScale=source.FindProperty("cupLocalScale").vector3Value
                };
                prefabs.GetArrayElementAtIndex(i).objectReferenceValue=appearances[i].CustomerPrefab;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            customers.ConfigureCollectedCarry(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase4/Prefabs/PF_Item_CeramicCup_01.prefab"),appearances);
            EditorUtility.SetDirty(customers);
        }
        private static NavigationActor EnsureCustomerPrefab(string species,GameObject employee)
        {
            const string folder="Assets/Art/Phase14/Customers";
            var path=folder+"/PF_"+species+"_P14Customer.prefab";
            var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(existing!=null) return existing.GetComponent<NavigationActor>();
            if(!AssetDatabase.IsValidFolder("Assets/Art/Phase14")) AssetDatabase.CreateFolder("Assets/Art","Phase14");
            if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Art/Phase14","Customers");
            // Reuse the complete accepted rig/body; never replace models on a live customer.
            var root=PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(employee));
            try
            {
                UnityEngine.Object.DestroyImmediate(root.GetComponent<EmployeeVisualPresenter>());
                root.name="PF_"+species+"_P14Customer";
                PrefabUtility.SaveAsPrefabAsset(root,path,out var saved);
                if(!saved) throw new InvalidOperationException("P14 customer prefab save failed: "+species);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<NavigationActor>();
        }
        private static T Ref<T>(UnityEngine.Object owner,string field) where T:UnityEngine.Object
            => (T)new SerializedObject(owner).FindProperty(field).objectReferenceValue;
        private static void Set(UnityEngine.Object owner,string field,UnityEngine.Object value)
        { var data=new SerializedObject(owner); data.FindProperty(field).objectReferenceValue=value; data.ApplyModifiedPropertiesWithoutUndo(); }
        private static void Flag(UnityEngine.Object owner,string field,bool value)
        { var data=new SerializedObject(owner); data.FindProperty(field).boolValue=value; data.ApplyModifiedPropertiesWithoutUndo(); }
    }
}

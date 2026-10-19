using System;
using System.IO;
using AnimalCafe.Navigation;
using AnimalCafe.Employees;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object=UnityEngine.Object;

namespace AnimalCafe.EditorTools.Phase13
{
    public static class Phase13EmployeeAssetBuilder
    {
        public const string Root = "Assets/Art/Phase13/Employees";
        [MenuItem("AnimalCafe/Phase 13/Build Employee Assets")]
        public static void Build()
        {
            foreach(var species in new[]{"Shiba","Westie"}) BuildOne(species,species=="Shiba"?30:24);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("AC_PHASE13_EMPLOYEE_ASSETS_BUILT Shiba Westie");
        }
        static void BuildOne(string species,int fps)
        {
            var folder=Root+"/"+species;
            var walk=Import(folder+"/SM_"+species+"_Employee_Walk.fbx","Walk",21);
            var work=Import(folder+"/SM_"+species+"_Work_Generic_Busy.fbx","Working",37);
            var carry=Import(folder+"/SM_"+species+"_Carry_Cup.fbx","Carry",2);
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/SM_"+species+"_Employee_Walk.fbx");
            var mask=BuildMask(model);
            var controller=BuildController(folder,species,fps,walk,work,carry,mask);
            // Clone accepted P11 authoring: never loosen collision, navigation or body dimensions.
            var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase11/Characters/"+species+"/PF_"+species+"_Navigation.prefab");
            if(original==null) throw new InvalidOperationException("Missing accepted navigation prefab");
            var root=Object.Instantiate(original); root.name="PF_"+species+"_Employee";
            try
            {
                var actor=root.GetComponent<NavigationActor>(); var modelRoot=actor.ModelRoot;
                var material=actor.ModelAnimator.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;
                for(var i=modelRoot.childCount-1;i>=0;i--) Object.DestroyImmediate(modelRoot.GetChild(i).gameObject);
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(model); instance.transform.SetParent(modelRoot,false);
                foreach(var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.sharedMaterial=material;
                var animator=instance.GetComponent<Animator>();
                animator.runtimeAnimatorController=controller; animator.applyRootMotion=false;
                animator.updateMode=AnimatorUpdateMode.Normal; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                actor.Configure(species,actor.Agent,actor.Proxy,animator,modelRoot);
                var visual=root.AddComponent<EmployeeVisualPresenter>();
                var cup=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase4/Prefabs/PF_Item_CeramicCup_01.prefab");
                visual.Configure(actor,cup);
                carry.SampleAnimation(instance,0);
                var socket=instance.transform.Find("AnimalCafe_MasterRig/root/pelvis/chest/upper_arm.R/forearm.R/hand.R/hand_socket.R");
                if(socket==null) throw new InvalidOperationException("Imported hand_socket.R missing");
                var reference=JsonUtility.FromJson<CarryReference>(File.ReadAllText(folder+"/carry-reference.json"));
                var hand=socket.parent;
                var referenceBottom=ConvertReference(reference,instance.transform,hand.position,socket.position);
                // Existing prefab is bottom-pivot, height .16 vs reference .10: preserve rim height.
                var cupHeight=cup.GetComponent<BoxCollider>().size.y;
                var desiredBottom=referenceBottom-Vector3.up*(cupHeight-.10f);
                var scale=socket.lossyScale;
                visual.ConfigureAttachment(socket.InverseTransformPoint(desiredBottom),Quaternion.Inverse(socket.rotation)*root.transform.rotation,
                    new Vector3(1/scale.x,1/scale.y,1/scale.z));
                Debug.Log("AC_P13_CUP_FIT "+species+" socket="+socket.position.ToString("F6")+" bottom="+desiredBottom.ToString("F6")+" local="+socket.InverseTransformPoint(desiredBottom).ToString("F6"));
                walk.SampleAnimation(instance,0);
                PrefabUtility.SaveAsPrefabAsset(root,folder+"/PF_"+species+"_Employee.prefab",out var saved);
                if(!saved) throw new InvalidOperationException("Employee prefab save failed");
            }
            finally { Object.DestroyImmediate(root); }
        }
        [Serializable] sealed class CarryReference { public float[] bottom; public float[] hand; public float[] socket; }
        static Vector3 ConvertReference(CarryReference data,Transform model,Vector3 actualHand,Vector3 actualSocket)
        {
            // Match actual imported points, rather than assuming Blender bone-local axes survive FBX.
            var permutations=new[]{new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0}};
            var error=float.PositiveInfinity; var result=Vector3.zero;
            foreach(var p in permutations) for(var bits=0;bits<8;bits++)
            {
                Vector3 Convert(float[] v) => model.TransformPoint(new Vector3(v[p[0]]*((bits&1)==0?1:-1),v[p[1]]*((bits&2)==0?1:-1),v[p[2]]*((bits&4)==0?1:-1)));
                var candidate=(Convert(data.hand)-actualHand).sqrMagnitude+(Convert(data.socket)-actualSocket).sqrMagnitude;
                if(candidate<error) { error=candidate; result=Convert(data.bottom); }
            }
            if(error>.000001f) throw new InvalidOperationException("FBX Cup coordinate conversion unverified, squared error="+error);
            return result;
        }
        static AnimationClip Import(string path,string name,int last)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            if(importer==null) throw new FileNotFoundException("Export employee FBX first",path);
            importer.animationType=ModelImporterAnimationType.Generic; importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation=true; importer.materialImportMode=ModelImporterMaterialImportMode.None;
            importer.globalScale=1; importer.useFileScale=true; importer.bakeAxisConversion=true;
            importer.optimizeGameObjects=false;
            // Match accepted P11 Walk compression exactly; Work/Carry keep fully sampled poses.
            importer.animationCompression=name=="Walk"?ModelImporterAnimationCompression.KeyframeReduction:ModelImporterAnimationCompression.Off;
            var clips=importer.defaultClipAnimations;
            if(clips.Length==0) throw new InvalidOperationException("No source clip in "+path);
            var clip=clips[0]; clip.name=name; clip.firstFrame=1; clip.lastFrame=last; clip.loopTime=true; clip.loopPose=true;
            importer.clipAnimations=new[]{clip}; importer.SaveAndReimport();
            foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path)) if(asset is AnimationClip animation && animation.name==name) return animation;
            throw new InvalidOperationException("Imported clip missing: "+path);
        }
        static AvatarMask BuildMask(GameObject model)
        {
            var path=Root+"/AM_Employee_CarryUpperBody.mask";
            var mask=AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if(mask==null) { mask=new AvatarMask(); AssetDatabase.CreateAsset(mask,path); }
            mask.transformCount=0; mask.AddTransformPath(model.transform,true);
            // Unity imported hierarchy paths are relative to the Animator/model root.
            for(var i=0;i<mask.transformCount;i++) {
                var pathName=mask.GetTransformPath(i);
                // AddTransformPath includes the imported model root name; strip it once.
                if(pathName==model.name) pathName="";
                else if(pathName.StartsWith(model.name+"/",StringComparison.Ordinal)) pathName=pathName.Substring(model.name.Length+1);
                mask.SetTransformPath(i,pathName);
                const string chest="AnimalCafe_MasterRig/root/pelvis/chest";
                mask.SetTransformActive(i,pathName==chest || pathName.StartsWith(chest+"/",StringComparison.Ordinal));
            }
            EditorUtility.SetDirty(mask); return mask;
        }
        static AnimatorController BuildController(string folder,string species,int fps,AnimationClip walk,AnimationClip work,AnimationClip carry,AvatarMask mask)
        {
            var path=folder+"/AC_"+species+"_Employee.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path) ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            var hold=Array.Find(AssetDatabase.LoadAllAssetsAtPath(path),a=>a is AnimationClip && a.name=="Hold") as AnimationClip;
            if(hold==null) { hold=new AnimationClip{name="Hold",frameRate=fps}; AssetDatabase.AddObjectToAsset(hold,controller); }
            foreach(var b in AnimationUtility.GetCurveBindings(hold)) AnimationUtility.SetEditorCurve(hold,b,null);
            foreach(var b in AnimationUtility.GetCurveBindings(walk)) AnimationUtility.SetEditorCurve(hold,b,AnimationCurve.Constant(0,1f/fps,AnimationUtility.GetEditorCurve(walk,b).Evaluate(0)));
            controller.parameters=new[]{new AnimatorControllerParameter{name="WalkSpeed",type=AnimatorControllerParameterType.Float,defaultFloat=1}};
            if(controller.layers.Length==1) controller.AddLayer("CarryUpperBody");
            var layers=controller.layers;
            foreach(var layer in layers) foreach(var s in layer.stateMachine.states) layer.stateMachine.RemoveState(s.state);
            var holdState=layers[0].stateMachine.AddState("Hold"); holdState.motion=hold; layers[0].stateMachine.defaultState=holdState;
            var walkState=layers[0].stateMachine.AddState("Walk"); walkState.motion=walk; walkState.speedParameter="WalkSpeed"; walkState.speedParameterActive=true;
            layers[0].stateMachine.AddState("Working").motion=work;
            var carryState=layers[1].stateMachine.AddState("Carry"); carryState.motion=carry; layers[1].stateMachine.defaultState=carryState;
            layers[1].avatarMask=mask; layers[1].blendingMode=AnimatorLayerBlendingMode.Override; layers[1].defaultWeight=0;
            controller.layers=layers; EditorUtility.SetDirty(hold); EditorUtility.SetDirty(controller); return controller;
        }
    }
}

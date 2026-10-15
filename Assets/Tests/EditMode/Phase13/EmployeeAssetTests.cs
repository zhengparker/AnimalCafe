using System;
using AnimalCafe.Navigation;
using AnimalCafe.Employees;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase13
{
    public sealed class EmployeeAssetTests
    {
        public const string Root = "Assets/Art/Phase13/Employees/";
        const string Rig = "AnimalCafe_MasterRig/root/pelvis/";
        [TestCase("Shiba",30f)] [TestCase("Westie",24f)]
        public void EmployeeAssetsContainWorkAndCarrySocket(string species, float fps)
        {
            var prefab = Require<GameObject>(Root+species+"/PF_"+species+"_Employee.prefab");
            var actor = prefab.GetComponent<NavigationActor>();
            Assert.That(prefab.GetComponent<EmployeeVisualPresenter>(), Is.Not.Null);
            Assert.That(actor.ModelAnimator.applyRootMotion, Is.False);
            Assert.That(actor.ModelAnimator.updateMode, Is.EqualTo(AnimatorUpdateMode.Normal));
            var handSocket = actor.ModelAnimator.transform.Find(Rig+"chest/upper_arm.R/forearm.R/hand.R/hand_socket.R");
            Assert.That(handSocket, Is.Not.Null); Assert.That(handSocket.name, Is.EqualTo("hand_socket.R"));
            Assert.That(Array.Exists(prefab.GetComponentsInChildren<Transform>(true),t=>t.name=="CarryProp_Cup"),Is.False);
            var work = Clip(species,"Work_Generic_Busy","Working");
            Assert.That(work.isLooping, Is.True);
            Assert.That(work.length, Is.EqualTo(36f/fps).Within(.001f));
            var carry = Clip(species,"Carry_Cup","Carry");
            foreach(var path in new[]{"chest","chest/head","chest/upper_arm.L","chest/upper_arm.R"})
                Assert.That(Array.Exists(AnimationUtility.GetCurveBindings(carry),b=>b.path==Rig+path), Is.True,"Full baked Carry pose: "+path);
            var model = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var instanceActor = model.GetComponent<NavigationActor>();
                var animator = instanceActor.ModelAnimator;
                var hand = animator.transform.Find(Rig+"chest/upper_arm.R");
                work.SampleAnimation(animator.gameObject,0); var first=hand.localRotation;
                work.SampleAnimation(animator.gameObject,.4f); Assert.That(Quaternion.Angle(first,hand.localRotation),Is.GreaterThan(1),"Working must animate");
                work.SampleAnimation(animator.gameObject,0); first=hand.localRotation;
                work.SampleAnimation(animator.gameObject,work.length); Assert.That(Quaternion.Angle(first,hand.localRotation),Is.LessThan(.1f),"Work seam");
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
        }
        [Test] public void CarryMaskExcludesRootPelvisAndLegs()
        {
            var mask = Require<AvatarMask>(Root+"AM_Employee_CarryUpperBody.mask");
            var active=0; var lower=0;
            for(var i=0;i<mask.transformCount;i++)
            {
                var path=mask.GetTransformPath(i);
                var chest=path==Rig+"chest" || path.StartsWith(Rig+"chest/",StringComparison.Ordinal);
                Assert.That(mask.GetTransformActive(i),Is.EqualTo(chest),path);
                if(chest) active++; else lower++;
            }
            Assert.That(active,Is.GreaterThanOrEqualTo(12)); Assert.That(lower,Is.GreaterThanOrEqualTo(10));
        }
        [TestCase("Shiba")] [TestCase("Westie")]
        public void EmployeePreservesNavigationBodyAndAcceptedWalk(string species)
        {
            var employee=Require<GameObject>(Root+species+"/PF_"+species+"_Employee.prefab").GetComponent<NavigationActor>();
            var original=Require<GameObject>("Assets/Art/Phase11/Characters/"+species+"/PF_"+species+"_Navigation.prefab").GetComponent<NavigationActor>();
            Assert.That(employee.Proxy.radius,Is.EqualTo(.45f)); Assert.That(employee.Proxy.height,Is.EqualTo(1.3f));
            Assert.That(employee.Proxy.center,Is.EqualTo(original.Proxy.center));
            Assert.That(employee.Agent.speed,Is.EqualTo(original.Agent.speed)); Assert.That(employee.Agent.stoppingDistance,Is.EqualTo(.08f));
            Assert.That(employee.Settings.CollisionSkin,Is.EqualTo(.01f)); Assert.That(employee.ModelRoot.localScale,Is.EqualTo(Vector3.one));
            Assert.That(employee.ModelRoot.localPosition.y,Is.EqualTo(original.ModelRoot.localPosition.y).Within(.0001f));
            Assert.That(employee.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial,Is.EqualTo(original.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial));
            var walk=Clip(species,"Employee_Walk","Walk");
            var originalWalk=Array.Find(original.ModelAnimator.runtimeAnimatorController.animationClips,c=>c.name=="Walk");
            var a=UnityEngine.Object.Instantiate(employee.gameObject); var b=UnityEngine.Object.Instantiate(original.gameObject);
            try { for(var sample=0;sample<=20;sample++) {
                walk.SampleAnimation(a.GetComponent<NavigationActor>().ModelAnimator.gameObject,sample/walk.frameRate);
                originalWalk.SampleAnimation(b.GetComponent<NavigationActor>().ModelAnimator.gameObject,sample/originalWalk.frameRate);
                foreach(var bone in new[]{"pelvis","chest","upper_arm.L","upper_arm.R","thigh.L","shin.R"}) {
                    var ta=Array.Find(a.GetComponentsInChildren<Transform>(),t=>t.name==bone); var tb=Array.Find(b.GetComponentsInChildren<Transform>(),t=>t.name==bone);
                    Assert.That(Quaternion.Angle(ta.localRotation,tb.localRotation),Is.LessThan(.1f),species+" preserved Walk "+bone);
                }
            }} finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); }
        }
        static T Require<T>(string path) where T:UnityEngine.Object { var asset=AssetDatabase.LoadAssetAtPath<T>(path); Assert.That(asset,Is.Not.Null,"Missing employee asset: "+path); return asset; }
        static AnimationClip Clip(string species,string suffix,string clipName) {
            var clip=Array.Find(AssetDatabase.LoadAllAssetsAtPath(Root+species+"/SM_"+species+"_"+suffix+".fbx"),a=>a is AnimationClip && a.name==clipName) as AnimationClip;
            Assert.That(clip,Is.Not.Null,clipName); return clip;
        }
    }
}

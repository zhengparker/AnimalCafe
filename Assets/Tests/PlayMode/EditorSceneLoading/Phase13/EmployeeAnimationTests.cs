#if UNITY_EDITOR
using System;
using System.Collections;
using AnimalCafe.Navigation;
using AnimalCafe.Employees;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace AnimalCafe.Tests.PlayMode.Phase13
{
    public sealed class EmployeeAnimationTests
    {
        GameObject instance; RuntimeAnimatorController fixtureController;
        float savedScale;
        // Own the fixture clock; a preceding navigation test may correctly leave Pause.
        // 仅初始化本测试的1x时钟，结束恢复原值，不改变生产暂停规则。
        [SetUp] public void Setup() { savedScale=Time.timeScale; Time.timeScale=1; }
        [TearDown] public void Cleanup() { if(instance!=null) Object.DestroyImmediate(instance); if(fixtureController!=null) Object.DestroyImmediate(fixtureController); Time.timeScale=savedScale; }
        NavigationActor Load(string species,bool employee=true) {
            var path=employee?"Assets/Art/Phase13/Employees/"+species+"/PF_"+species+"_Employee.prefab":"Assets/Art/Phase11/Characters/"+species+"/PF_"+species+"_Navigation.prefab";
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(prefab,Is.Not.Null,path);
            instance=Object.Instantiate(prefab); return instance.GetComponent<NavigationActor>();
        }
        [UnityTest] public IEnumerator DefaultNavigationPresenterStillSelectsWalkHold() {
            var actor=Load("Shiba",false); var motion=actor.GetComponent<NavigationWalkPresenter>();
            motion.SetMotion(1.2f); yield return null; Assert.That(actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Walk"),Is.True);
            motion.SetMotion(0); yield return null; Assert.That(actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Hold"),Is.True);
        }
        [UnityTest] public IEnumerator WorkingCannotBeOverwrittenByMotionZero() {
            // Real Animator fixture allows a behavioral RED before employee assets exist.
            var actor=Load("Shiba",false); var controller=new AnimatorController(); controller.AddLayer("Base Layer");
            var clip=actor.ModelAnimator.runtimeAnimatorController.animationClips[0];
            foreach(var name in new[]{"Hold","Walk","Working"}) controller.layers[0].stateMachine.AddState(name).motion=clip;
            fixtureController=controller; actor.ModelAnimator.runtimeAnimatorController=controller;
            var visual=instance.AddComponent<EmployeeVisualPresenter>(); visual.Configure(actor,null);
            yield return null; visual.SetMode(EmployeeVisualMode.Working); actor.GetComponent<NavigationWalkPresenter>().SetMotion(0);
            yield return null; Assert.That(actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Working"),Is.True);
            Assert.That(actor.ModelAnimator.speed,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator BothSpeciesCarryKeepsUpperPoseAndRealWalkWithOwnedCup() {
            foreach(var species in new[]{"Shiba","Westie"}) {
                var actor=Load(species); var visual=instance.GetComponent<EmployeeVisualPresenter>(); var motion=instance.GetComponent<NavigationWalkPresenter>();
                var cupPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase4/Prefabs/PF_Item_CeramicCup_01.prefab");
                visual.Configure(actor,cupPrefab); visual.SetMode(EmployeeVisualMode.Carrying); Assert.That(visual.TryShowHeldCup(out var reason),Is.True,reason);
                motion.SetMotion(1.2f); yield return null;
                var animator=actor.ModelAnimator; Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"),Is.True);
                Assert.That(animator.GetLayerWeight(1),Is.EqualTo(1));
                var upperNames=new[]{"chest","head","upper_arm.L","upper_arm.R","forearm.L","forearm.R"};
                var reference=Object.Instantiate(animator.gameObject); reference.GetComponent<Animator>().enabled=false;
                try {
                    var carry=Array.Find(animator.runtimeAnimatorController.animationClips,c=>c.name=="Carry"); carry.SampleAnimation(reference,0);
                    foreach(var boneName in upperNames) {
                        var expected=Array.Find(reference.GetComponentsInChildren<Transform>(),t=>t.name==boneName);
                        var actual=Array.Find(animator.GetComponentsInChildren<Transform>(),t=>t.name==boneName);
                        Assert.That(Quaternion.Angle(actual.localRotation,expected.localRotation),Is.LessThan(.1f),species+" full source Carry "+boneName);
                    }
                } finally { Object.DestroyImmediate(reference); }
                var bones=animator.GetComponentsInChildren<Transform>(); var hand=Array.Find(bones,t=>t.name=="hand.R"); var leg=Array.Find(bones,t=>t.name=="thigh.L");
                var upper=hand.localRotation; var lower=leg.localRotation;
                yield return new WaitForSeconds(.2f);
                Assert.That(Quaternion.Angle(upper,hand.localRotation),Is.LessThan(.1f)); Assert.That(Quaternion.Angle(lower,leg.localRotation),Is.GreaterThan(1));
                var socket=Array.Find(bones,t=>t.name=="hand_socket.R"); Assert.That(socket.childCount,Is.EqualTo(1)); var cup=socket.GetChild(0);
                Assert.That(Vector3.Dot(cup.up,Vector3.up),Is.GreaterThan(.999f));
                Assert.That(Vector3.Distance(cup.lossyScale,Vector3.one),Is.LessThan(.001f),"Preserve actual existing Cup size under FBX bone scale");
                Assert.That(Vector3.Distance(cup.position,hand.position),Is.InRange(.02f,.3f),"Cup bottom near hand, not model origin");
                foreach(var collider in cup.GetComponentsInChildren<Collider>()) Assert.That(collider.enabled,Is.False);
                Assert.That(visual.TryShowHeldCup(out reason),Is.True,reason); Assert.That(socket.childCount,Is.EqualTo(1));
                visual.HideHeldCup(); yield return null; Assert.That(socket.childCount,Is.Zero);
                visual.SetMode(EmployeeVisualMode.Working); motion.SetMotion(0); yield return null; Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Working"),Is.True); Assert.That(animator.GetLayerWeight(1),Is.Zero);
                Object.DestroyImmediate(instance); instance=null;
            }
        }
        [UnityTest] public IEnumerator ReenableResyncsWithoutDuplicateMotionOwnershipOrInactivePlay() {
            var actor=Load("Westie"); var visual=instance.GetComponent<EmployeeVisualPresenter>(); visual.SetMode(EmployeeVisualMode.Working); yield return null;
            for(var i=0;i<3;i++) { instance.SetActive(false); actor.GetComponent<NavigationWalkPresenter>().SetMotion(0); visual.SetMode(EmployeeVisualMode.Working); yield return null; instance.SetActive(true); yield return null; }
            Assert.That(actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Working"),Is.True);
            actor.GetComponent<NavigationWalkPresenter>().SetMotion(0); yield return null; Assert.That(actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Working"),Is.True);
        }
        [UnityTest] public IEnumerator PauseAndFastScaleWorkingExactlyOnce() {
            var previousCapture=Time.captureDeltaTime;
            try {
                // Compare equal complete game frames, not Editor wall-clock scheduling.
                // 固定采样步长并恢复原设置；Animator仍由真实PlayerLoop推进，不手动Update。
                Time.captureDeltaTime=1f/60f;
                var actor=Load("Shiba"); var visual=instance.GetComponent<EmployeeVisualPresenter>();
                visual.SetMode(EmployeeVisualMode.Working); yield return null;
                var animator=actor.ModelAnimator; var runningDelta=Time.deltaTime;
                Time.timeScale=0;
                // Drain the already evaluated running frame before measuring a paused frame.
                yield return null; Assert.That(Time.deltaTime,Is.EqualTo(0));
                var before=animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                yield return new WaitForSecondsRealtime(.15f);
                TestContext.WriteLine("PAUSE runningDelta="+runningDelta.ToString("R")+" pausedDelta="+Time.deltaTime.ToString("R")+
                    " normalizedDrift="+(animator.GetCurrentAnimatorStateInfo(0).normalizedTime-before).ToString("R"));
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,Is.EqualTo(before).Within(.001f));
                const int frames=30;
                Time.timeScale=1; yield return null;
                before=animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                var normalDelta=0f;
                for(var i=0;i<frames;i++) { yield return null; normalDelta+=Time.deltaTime; }
                var normalAdvance=animator.GetCurrentAnimatorStateInfo(0).normalizedTime-before;
                Time.timeScale=2; yield return null;
                before=animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                var fastDelta=0f;
                for(var i=0;i<frames;i++) { yield return null; fastDelta+=Time.deltaTime; }
                var fastAdvance=animator.GetCurrentAnimatorStateInfo(0).normalizedTime-before;
                TestContext.WriteLine("FRAME_CLOCK frames="+frames+" captureDelta="+Time.captureDeltaTime.ToString("R")+
                    " normalAdvance="+normalAdvance.ToString("R")+" normalDelta="+normalDelta.ToString("R")+
                    " fastAdvance="+fastAdvance.ToString("R")+" fastDelta="+fastDelta.ToString("R"));
                Assert.That(normalAdvance,Is.GreaterThan(0));
                Assert.That(fastAdvance/normalAdvance,Is.InRange(1.7f,2.3f));
                Assert.That(fastDelta/normalDelta,Is.EqualTo(2).Within(.001f));
                Assert.That((fastAdvance/fastDelta)/(normalAdvance/normalDelta),Is.EqualTo(1).Within(.01f));
                Assert.That(animator.speed,Is.EqualTo(1));
                Assert.That(animator.updateMode,Is.EqualTo(AnimatorUpdateMode.Normal));
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Working"),Is.True);
            } finally { Time.captureDeltaTime=previousCapture; }
        }
    }
}
#endif

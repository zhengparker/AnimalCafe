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
        [TearDown] public void Cleanup() { if(instance!=null) Object.DestroyImmediate(instance); if(fixtureController!=null) Object.DestroyImmediate(fixtureController); Time.timeScale=1; }
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
            var actor=Load("Shiba"); var visual=instance.GetComponent<EmployeeVisualPresenter>(); visual.SetMode(EmployeeVisualMode.Working); yield return null;
            var runningDelta=Time.deltaTime;
            Time.timeScale=0;
            // Drain the already evaluated running frame before measuring a paused frame.
            // 等切换帧结束后取暂停基线；保留原精度，不把旧帧delta误算为暂停推进。
            yield return null; Assert.That(Time.deltaTime,Is.EqualTo(0));
            var before=actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime; yield return new WaitForSecondsRealtime(.15f);
            TestContext.WriteLine("PAUSE runningDelta="+runningDelta.ToString("R")+" pausedDelta="+Time.deltaTime.ToString("R")+
                " normalizedDrift="+(actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime-before).ToString("R"));
            Assert.That(actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime,Is.EqualTo(before).Within(.001f));
            Time.timeScale=1; before=actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime; var start=Time.realtimeSinceStartup; yield return new WaitForSecondsRealtime(.25f);
            var normal=(actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime-before)/(Time.realtimeSinceStartup-start);
            Time.timeScale=2; before=actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime; start=Time.realtimeSinceStartup; yield return new WaitForSecondsRealtime(.25f);
            var fast=(actor.ModelAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime-before)/(Time.realtimeSinceStartup-start);
            Assert.That(fast/normal,Is.InRange(1.7f,2.3f));
        }
    }
}
#endif

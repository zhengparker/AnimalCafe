using System;
using AnimalCafe.Navigation;
using UnityEngine;

namespace AnimalCafe.Customers
{
    // Visit-owned presentation only / 实际移动和领取事实由原有服务负责。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavigationActor),typeof(NavigationWalkPresenter))]
    public sealed class CustomerCarryPresenter:MonoBehaviour
    {
        [Serializable] public sealed class Appearance
        {
            public NavigationActor CustomerPrefab;
            public RuntimeAnimatorController CarryController;
            public Vector3 CupLocalPosition;
            public Quaternion CupLocalRotation=Quaternion.identity;
            public Vector3 CupLocalScale=Vector3.one;
        }
        private NavigationActor actor;
        private NavigationWalkPresenter navigation;
        private Animator animator;
        private Appearance appearance;
        private GameObject cupPrefab,heldCup;
        private bool collected,subscribed,needsSync,reportedUnavailable;
        private float walkDuration;
        private string state;

        public void Configure(NavigationActor owner,Appearance configuration,GameObject cup)
        { actor=owner; appearance=configuration; cupPrefab=cup; }
        public void ConfirmCollected()
        {
            if(collected) return;
            collected=true;
            BindCollected();
        }
        private void OnEnable() { if(collected) BindCollected(); }
        private void OnDisable()
        {
            Unsubscribe(); needsSync=true;
            // Retain the exact cup through disable/Blocked/Done, including inactive parent bodies.
        }
        private void OnDestroy() { Unsubscribe(); if(heldCup!=null) Destroy(heldCup); }
        private void Unsubscribe()
        {
            if(subscribed && navigation!=null) navigation.MotionChanged-=OnMotion;
            subscribed=false;
        }
        private void BindCollected()
        {
            if(!isActiveAndEnabled) return;
            if(actor==null || appearance==null || appearance.CarryController==null || cupPrefab==null)
            { ReportUnavailable("Actor / Carry appearance / Cup prefab missing"); return; }
            animator=actor.ModelAnimator; navigation=actor.GetComponent<NavigationWalkPresenter>();
            if(animator==null || navigation==null) { ReportUnavailable("Animator / NavigationWalkPresenter missing"); return; }
            if(animator.runtimeAnimatorController!=appearance.CarryController) animator.runtimeAnimatorController=appearance.CarryController;
            animator.applyRootMotion=false; animator.updateMode=AnimatorUpdateMode.Normal; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            foreach(var clip in appearance.CarryController.animationClips) if(clip.name=="Walk") walkDuration=clip.length;
            navigation.SetExternalAnimationDriver(true);
            if(!subscribed) { navigation.MotionChanged+=OnMotion; subscribed=true; }
            if(heldCup==null)
            {
                var socket=animator.transform.Find("AnimalCafe_MasterRig/root/pelvis/chest/upper_arm.R/forearm.R/hand.R/hand_socket.R");
                if(socket==null) { ReportUnavailable("hand_socket.R missing from configured rig"); return; }
                heldCup=Instantiate(cupPrefab,socket,false); heldCup.name="CustomerHeldCup";
                heldCup.transform.localPosition=appearance.CupLocalPosition;
                heldCup.transform.localRotation=appearance.CupLocalRotation;
                heldCup.transform.localScale=appearance.CupLocalScale;
                foreach(var collider in heldCup.GetComponentsInChildren<Collider>(true)) collider.enabled=false;
            }
            needsSync=true; Apply();
        }
        private void ReportUnavailable(string reason)
        {
            if(reportedUnavailable) return;
            reportedUnavailable=true;
            Debug.LogWarning("Customer collected cup unavailable: "+reason+". Check P14 customer appearance configuration; actual exit continues.",this);
        }
        private void OnMotion(float speed) => Apply();
        private void LateUpdate() { if(needsSync) Apply(); }
        private void Apply()
        {
            if(!collected || !isActiveAndEnabled || animator==null || !animator.isActiveAndEnabled)
            { needsSync=true; return; }
            var moving=navigation.ActualSpeed>.05f;
            // The Animator consumes scaled delta once; Carry stays stable while only Walk is speed-matched.
            animator.speed=1;
            animator.SetFloat("WalkSpeed",moving && walkDuration>0?navigation.ActualSpeed*walkDuration/navigation.TravelPerCycle:1);
            var next=moving?"Walk":"Hold"; // Never enter the employee-only Working state.
            if(needsSync || state!=next) { animator.Play(next,0,0); state=next; }
            animator.SetLayerWeight(1,1);
            if(needsSync) animator.Play("Carry",1,0);
            needsSync=false;
        }
    }
}

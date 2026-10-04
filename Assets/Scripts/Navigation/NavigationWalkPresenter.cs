using UnityEngine;
namespace AnimalCafe.Navigation
{
    // Presentation only: actual displacement comes exclusively from NavigationWorld.
    [RequireComponent(typeof(NavigationActor))]
    public sealed class NavigationWalkPresenter : MonoBehaviour
    {
        [SerializeField, Min(.01f)] private float travelPerCycle = .8f;
        private Animator animator;
        private float walkDuration;
        private bool walking;
        private bool needsStateSync = true;
        public float ActualSpeed { get; private set; }
        public float TravelPerCycle => travelPerCycle;
        public float WalkDuration => walkDuration;
        private void Awake()
        {
            animator = GetComponent<NavigationActor>().ModelAnimator;
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name == "Walk") walkDuration = clip.length;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        public void SetMotion(float actualSpeed)
        {
            ActualSpeed = float.IsNaN(actualSpeed) || float.IsInfinity(actualSpeed) ? 0 : Mathf.Max(0, actualSpeed);
            if (animator == null || walkDuration <= 0) return;
            // Inactive Animators can reset to Hold; resync once after they return.
            // 不对 inactive Animator 调用 Play；恢复后按实际速度重新选择状态。
            if (!animator.isActiveAndEnabled) { needsStateSync = true; return; }
            var nextWalking = ActualSpeed > .05f;
            // A cycle travels 0.8 m by tuning convention; use each source clip's real duration.
            // Animator already consumes scaled deltaTime, so do not multiply timeScale here.
            animator.speed = nextWalking ? ActualSpeed * walkDuration / travelPerCycle : 1;
            if (!needsStateSync && nextWalking == walking) return;
            needsStateSync = false;
            walking = nextWalking;
            animator.Play(walking ? "Walk" : "Hold", 0, 0);
        }
    }
}

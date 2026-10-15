using AnimalCafe.Navigation;
using UnityEngine;
namespace AnimalCafe.Employees
{
    // One animation owner per employee. NavigationWorld remains the displacement owner.
    [RequireComponent(typeof(NavigationWalkPresenter))]
    public sealed class EmployeeVisualPresenter : MonoBehaviour
    {
        [SerializeField] private NavigationActor actor;
        [SerializeField] private GameObject cupPrefab;
        [SerializeField] private Vector3 cupLocalPosition;
        [SerializeField] private Quaternion cupLocalRotation = Quaternion.identity;
        [SerializeField] private Vector3 cupLocalScale = Vector3.one;
        private NavigationWalkPresenter navigation;
        private Animator animator;
        private Transform socket;
        private GameObject heldCup;
        private EmployeeVisualMode mode;
        private string selectedState;
        private bool subscribed;
        private bool needsSync = true;
        private float walkDuration;

        public void Configure(NavigationActor navigationActor, GameObject presentationCupPrefab)
        {
            ReleaseNavigation();
            if (actor != navigationActor || cupPrefab != presentationCupPrefab) HideHeldCup();
            actor = navigationActor; cupPrefab = presentationCupPrefab;
            Bind();
        }
        private void Awake() { if (actor == null) actor = GetComponent<NavigationActor>(); Bind(); }
        private void OnEnable() => Bind();
        private void OnDisable() { ReleaseNavigation(); needsSync = true; HideHeldCup(); }
        private void OnDestroy() { ReleaseNavigation(); HideHeldCup(); }
        private void Bind()
        {
            if (actor == null) return;
            animator = actor.ModelAnimator;
            navigation = actor.GetComponent<NavigationWalkPresenter>();
            socket = animator == null ? null : animator.transform.Find("AnimalCafe_MasterRig/root/pelvis/chest/upper_arm.R/forearm.R/hand.R/hand_socket.R");
            walkDuration = 0;
            if (animator != null)
            {
                animator.applyRootMotion = false; animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (animator.runtimeAnimatorController != null)
                    foreach (var clip in animator.runtimeAnimatorController.animationClips)
                        if (clip.name == "Walk") walkDuration = clip.length;
            }
            if (!isActiveAndEnabled || navigation == null) return;
            navigation.SetExternalAnimationDriver(true);
            if (!subscribed) { navigation.MotionChanged += OnMotion; subscribed = true; }
            needsSync = true; Apply();
        }
        private void ReleaseNavigation()
        {
            if (navigation == null || !subscribed) return;
            navigation.MotionChanged -= OnMotion; subscribed = false;
            // No fallback Play while disabled: ownership stays reserved for this employee.
        }
        public void SetMode(EmployeeVisualMode nextMode) { mode = nextMode; Apply(); }
        private void OnMotion(float speed) => Apply();
        private void LateUpdate() { if (needsSync) Apply(); }
        private void Apply()
        {
            if (!isActiveAndEnabled || animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            { needsSync = true; return; }
            var moving = navigation != null && navigation.ActualSpeed > .05f;
            var working = mode == EmployeeVisualMode.Working;
            var next = working ? "Working" : moving ? "Walk" : "Hold";
            // Animator consumes scaled delta once. Layer speed parameter leaves Carry pose stable.
            animator.speed = 1;
            var speed = moving && walkDuration > 0 ? navigation.ActualSpeed * walkDuration / navigation.TravelPerCycle : 1;
            var hasWalkSpeed = false;
            foreach (var parameter in animator.parameters)
                if (parameter.name == "WalkSpeed") { hasWalkSpeed = true; break; }
            if (hasWalkSpeed) animator.SetFloat("WalkSpeed", speed);
            else if (!working) animator.speed = speed;
            if (needsSync || selectedState != next) { animator.Play(next,0,0); selectedState = next; }
            if (animator.layerCount > 1)
            {
                animator.SetLayerWeight(1, mode == EmployeeVisualMode.Carrying ? 1 : 0);
                if (needsSync) animator.Play("Carry",1,0);
            }
            needsSync = false;
        }
        public bool TryShowHeldCup(out string reason)
        {
            reason = null;
            if (heldCup != null) return true;
            if (!isActiveAndEnabled || socket == null || cupPrefab == null) { reason = "Employee hand socket / Cup prefab unavailable"; return false; }
            var candidate = Instantiate(cupPrefab, socket, false);
            if (candidate.GetComponentInChildren<Renderer>(true) == null)
            { Destroy(candidate); reason = "Cup visual unavailable"; return false; }
            foreach (var collider in candidate.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            candidate.name = "EmployeeHeldCup";
            candidate.transform.localPosition = cupLocalPosition;
            candidate.transform.localRotation = cupLocalRotation;
            candidate.transform.localScale = cupLocalScale;
            heldCup = candidate; return true;
        }
        public void HideHeldCup()
        {
            if (heldCup == null) return;
            var owned = heldCup; heldCup = null;
            owned.SetActive(false); Destroy(owned);
        }
#if UNITY_EDITOR
        public void ConfigureAttachment(Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
        { cupLocalPosition = localPosition; cupLocalRotation = localRotation; cupLocalScale = localScale; }
#endif
    }
}

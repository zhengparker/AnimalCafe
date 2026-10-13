using UnityEngine;
using UnityEngine.AI;

namespace AnimalCafe.Navigation
{
    // Unity scene references for one navigation character. NavigationWorld owns movement.
    public sealed class NavigationActor : MonoBehaviour
    {
        [SerializeField] private string actorId;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private CapsuleCollider proxy;
        [SerializeField] private NavigationSettings settings = new NavigationSettings();
        [SerializeField] private Animator modelAnimator;
        [SerializeField] private Transform modelRoot;

        public string ActorId => actorId;
        public NavMeshAgent Agent => agent;
        public CapsuleCollider Proxy => proxy;
        public NavigationSettings Settings => settings;
        public Animator ModelAnimator => modelAnimator;
        public Transform ModelRoot => modelRoot;
        internal NavigationWorld World { get; set; }
        // P12运行时选择；不写入prefab/Save，注册后不能切换 / runtime opt-in only.
        public bool SteadyPathMotion { get; private set; }
        public bool TryEnableSteadyPathMotion()
        {
            if (World != null || SteadyPathMotion) return false;
            SteadyPathMotion = true;
            return true;
        }
        private bool runtimeIdentityInitialized;
        // 一次性 visit 身份；注册后不可改变 / identity is immutable for a visit.
        public bool TryInitializeRuntimeId(string id)
        {
            if (runtimeIdentityInitialized || World != null || string.IsNullOrWhiteSpace(id) || id != id.Trim())
                return false;
            actorId = id;
            runtimeIdentityInitialized = true;
            return true;
        }
        private NavigationWalkPresenter presenter;
        internal void SetMotion(float speed)
        {
            if (presenter == null) presenter = GetComponent<NavigationWalkPresenter>();
            if (presenter != null) presenter.SetMotion(speed);
        }

        private void Awake()
        {
            // NavMeshAgent's updatePosition/updateRotation are runtime-only flags.
            // Disable them each time a prefab instance is created, before World enables Agent.
            if (agent == null) return;
            agent.updatePosition = false;
            agent.updateRotation = false;
        }

        private void OnDisable() => World?.SetActorAvailable(this, false);
        private void OnEnable() => World?.SetActorAvailable(this, true);
        private void OnDestroy() => World?.Unregister(this);

#if UNITY_EDITOR
        public void Configure(string id, NavMeshAgent navAgent, CapsuleCollider collider,
            Animator animator, Transform model)
        {
            actorId = id;
            agent = navAgent;
            proxy = collider;
            settings = new NavigationSettings();
            modelAnimator = animator;
            modelRoot = model;
        }
#endif
    }
}

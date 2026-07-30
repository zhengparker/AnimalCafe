using AnimalCafe.Core.Events;
using AnimalCafe.Input;
using UnityEngine;

namespace AnimalCafe.Interaction
{
    /// <summary>
    /// 将 tap screen position 转换为单一 scene selection。
    /// Converts a tap position into a single scene selection.
    /// </summary>
    public sealed class SceneInteractionController : MonoBehaviour
    {
        [SerializeField]
        private UnityEngine.Camera targetCamera;

        [SerializeField]
        private MonoBehaviour inputSourceBehaviour;

        [SerializeField]
        private LayerMask selectableLayers = ~0;

        private ICameraInputSource inputSource;

        public ISelectable CurrentSelection { get; private set; }

        private void Start()
        {
            inputSource ??= inputSourceBehaviour as ICameraInputSource;
            if (targetCamera == null || inputSource == null)
            {
                Debug.LogError(
                    "[SceneInteractionController] Camera and input source are required.",
                    this);
                enabled = false;
            }
        }

        private void Update()
        {
            ClearInvalidSelection();
            if (inputSource == null)
            {
                return;
            }

            var inputFrame = inputSource.ReadFrame();
            if (inputFrame.TapReleased)
            {
                TrySelectAt(inputFrame.PointerPosition);
            }
        }

        private void OnDisable()
        {
            ClearSelection();
        }

        public void Configure(
            UnityEngine.Camera camera,
            ICameraInputSource cameraInputSource)
        {
            targetCamera = camera;
            inputSource = cameraInputSource;
        }

        public bool TrySelectAt(Vector2 screenPosition)
        {
            if (targetCamera == null)
            {
                ClearSelection();
                return false;
            }

            var ray = targetCamera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out var hit, Mathf.Infinity, selectableLayers))
            {
                ClearSelection();
                return false;
            }

            var selectable = FindSelectable(hit.collider);
            SetSelection(selectable);
            return selectable != null;
        }

        public void ClearSelection()
        {
            SetSelection(null);
        }

        private void SetSelection(ISelectable next)
        {
            ClearInvalidSelection();
            if (ReferenceEquals(CurrentSelection, next))
            {
                return;
            }

            var previous = CurrentSelection;
            previous?.Deselect();
            CurrentSelection = next;
            CurrentSelection?.Select();
            GameEventBus.PublishSelectionChanged(
                previous as UnityEngine.Object,
                CurrentSelection as UnityEngine.Object);
        }

        private void ClearInvalidSelection()
        {
            if (CurrentSelection is UnityEngine.Object unityObject
                && unityObject == null)
            {
                var previous = unityObject;
                CurrentSelection = null;
                GameEventBus.PublishSelectionChanged(previous, null);
                return;
            }

            if (CurrentSelection is not Behaviour behaviour
                || (behaviour.isActiveAndEnabled
                    && behaviour.gameObject.activeInHierarchy))
            {
                return;
            }

            var disabledSelection = CurrentSelection;
            disabledSelection.Deselect();
            CurrentSelection = null;
            GameEventBus.PublishSelectionChanged(
                disabledSelection as UnityEngine.Object,
                null);
        }

        private static ISelectable FindSelectable(Collider hitCollider)
        {
            var behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>();
            foreach (var behaviour in behaviours)
            {
                if (behaviour is ISelectable selectable)
                {
                    return selectable;
                }
            }

            return null;
        }
    }
}

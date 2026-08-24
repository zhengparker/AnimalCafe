using System;
using System.Collections.Generic;
using AnimalCafe.Core.Events;
using AnimalCafe.Input;
using AnimalCafe.UI.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;

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
        private IUiPointerBoundary uiPointerBoundary;
        private readonly HashSet<int> pendingScenePointerPresses = new();
        private readonly HashSet<int> activePointerIds = new();
        private readonly HashSet<int> suppressedPointerIds = new();
        private readonly HashSet<long> inputSuppressionTokens = new();
        private long nextInputSuppressionToken;
        private bool waitForFreshPointerPress;

        public ISelectable CurrentSelection { get; private set; }

        public IDisposable AcquireInputSuppression(object owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (inputSuppressionTokens.Count == 0)
            {
                ReleaseActivePointerOwnership();
            }

            var token = NextInputSuppressionToken();
            inputSuppressionTokens.Add(token);
            return new InputSuppressionLease(this, token);
        }

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

        private void LateUpdate()
        {
            if (inputSuppressionTokens.Count == 0)
            {
                ClearInvalidSelection();
            }

            if (inputSource == null)
            {
                return;
            }

            var inputFrame = inputSource.ReadFrame();

            if (inputSuppressionTokens.Count > 0)
            {
                DrainSuppressedPointerFrame(inputFrame);
                return;
            }

            if (waitForFreshPointerPress)
            {
                if (!inputFrame.PointerPressed)
                {
                    DrainSuppressedPointerFrame(inputFrame);
                    return;
                }

                waitForFreshPointerPress = false;
                suppressedPointerIds.Remove(inputFrame.PointerId);
            }

            RegisterPendingScenePointerPresses();
            if (inputFrame.PointerPressed)
            {
                suppressedPointerIds.Remove(inputFrame.PointerId);
            }

            if (uiPointerBoundary != null && inputFrame.PointerPressed)
            {
                activePointerIds.Add(inputFrame.PointerId);
                var pointerOverUi = EventSystem.current != null
                    && EventSystem.current.IsPointerOverGameObject(
                        inputFrame.PointerId);
                if (!pointerOverUi)
                {
                    pendingScenePointerPresses.Add(inputFrame.PointerId);
                }
            }

            if (inputFrame.TapReleased)
            {
                if (!suppressedPointerIds.Contains(inputFrame.PointerId))
                {
                    var canProcessScenePointer = uiPointerBoundary != null
                        ? uiPointerBoundary.CanProcessScenePointer(
                            inputFrame.PointerId)
                        : EventSystem.current == null
                          || !EventSystem.current.IsPointerOverGameObject();
                    if (canProcessScenePointer)
                    {
                        TrySelectAt(inputFrame.PointerPosition);
                    }
                }
            }

            // Clear only after Scene has made its release decision, including drags.
            if (inputFrame.PointerReleased)
            {
                uiPointerBoundary?.ReleasePointer(inputFrame.PointerId);
                pendingScenePointerPresses.Remove(inputFrame.PointerId);
                activePointerIds.Remove(inputFrame.PointerId);
                suppressedPointerIds.Remove(inputFrame.PointerId);
            }
        }

        private void OnDisable()
        {
            ReleaseActivePointerOwnership();
            if (inputSuppressionTokens.Count == 0)
            {
                ClearSelection();
            }
        }

        public void Configure(
            UnityEngine.Camera camera,
            ICameraInputSource cameraInputSource)
        {
            ReleaseActivePointerOwnership();
            targetCamera = camera;
            inputSource = cameraInputSource;
            inputSourceBehaviour = cameraInputSource as MonoBehaviour;
            uiPointerBoundary = null;
        }

        public void Configure(
            UnityEngine.Camera camera,
            ICameraInputSource cameraInputSource,
            IUiPointerBoundary pointerBoundary)
        {
            ReleaseActivePointerOwnership();
            targetCamera = camera;
            inputSource = cameraInputSource;
            inputSourceBehaviour = cameraInputSource as MonoBehaviour;
            uiPointerBoundary = pointerBoundary;
        }

        private void RegisterPendingScenePointerPresses()
        {
            if (uiPointerBoundary == null || pendingScenePointerPresses.Count == 0)
            {
                return;
            }

            foreach (var pointerId in pendingScenePointerPresses)
            {
                uiPointerBoundary.RegisterScenePointerPress(pointerId);
            }

            pendingScenePointerPresses.Clear();
        }

        private void ReleaseActivePointerOwnership()
        {
            if (uiPointerBoundary != null)
            {
                foreach (var pointerId in activePointerIds)
                {
                    uiPointerBoundary.ReleasePointer(pointerId);
                    suppressedPointerIds.Add(pointerId);
                }
            }

            if (uiPointerBoundary == null)
            {
                foreach (var pointerId in activePointerIds)
                {
                    suppressedPointerIds.Add(pointerId);
                }
            }

            activePointerIds.Clear();
            pendingScenePointerPresses.Clear();
        }

        public bool TrySelectAt(Vector2 screenPosition)
        {
            if (inputSuppressionTokens.Count > 0 || waitForFreshPointerPress)
            {
                return false;
            }

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

        private long NextInputSuppressionToken()
        {
            do
            {
                unchecked
                {
                    nextInputSuppressionToken++;
                }
            }
            while (inputSuppressionTokens.Contains(nextInputSuppressionToken));

            return nextInputSuppressionToken;
        }

        private void ReleaseInputSuppression(long token)
        {
            if (!inputSuppressionTokens.Remove(token)
                || inputSuppressionTokens.Count > 0)
            {
                return;
            }

            // The release which closed Decoration mode may arrive after the lease.
            // Only a later fresh press is allowed to restore Scene interaction.
            waitForFreshPointerPress = true;
        }

        private void DrainSuppressedPointerFrame(CameraInputFrame inputFrame)
        {
            if (inputFrame.PointerPressed)
            {
                suppressedPointerIds.Add(inputFrame.PointerId);
            }

            if (!inputFrame.PointerReleased)
            {
                return;
            }

            uiPointerBoundary?.ReleasePointer(inputFrame.PointerId);
            pendingScenePointerPresses.Remove(inputFrame.PointerId);
            activePointerIds.Remove(inputFrame.PointerId);
            suppressedPointerIds.Remove(inputFrame.PointerId);
        }

        private sealed class InputSuppressionLease : IDisposable
        {
            private SceneInteractionController owner;
            private readonly long token;

            public InputSuppressionLease(
                SceneInteractionController controller,
                long suppressionToken)
            {
                owner = controller;
                token = suppressionToken;
            }

            public void Dispose()
            {
                var currentOwner = owner;
                if (currentOwner == null)
                {
                    return;
                }

                owner = null;
                currentOwner.ReleaseInputSuppression(token);
            }
        }
    }
}

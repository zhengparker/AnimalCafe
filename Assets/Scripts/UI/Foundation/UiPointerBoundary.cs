using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AnimalCafe.UI.Foundation
{
    public enum UiPointerOwnership
    {
        None,
        Ui,
        Scene
    }

    /// <summary>
    /// Query used by scene interaction before consuming a pointer release.
    /// åœºæ™¯äº¤äº’åœ¨å¤„ç† pointer release å‰ä½¿ç”¨çš„æŸ¥è¯¢ contractã€‚
    /// </summary>
    public interface IUiPointerBoundary
    {
        void RegisterScenePointerPress(int pointerId);

        bool CanProcessScenePointer(int pointerId);

        void ReleasePointer(int pointerId);
    }

    /// <summary>
    /// Registration contract used by UI event hooks on pointer press.
    /// UI event hook åœ¨ pointer press æ—¶ä½¿ç”¨çš„ç™»è®° contractã€‚
    /// </summary>
    public interface IUiPointerOwnershipRegistrar : IUiPointerBoundary
    {
        void RegisterUiPointerPress(int pointerId);
    }

    /// <summary>
    /// Keeps pointer ownership stable from press through release.
    /// ä»Ž press åˆ° release ä¿æŒæ¯ä¸ª pointer çš„ owner ä¸å˜ã€‚
    /// </summary>
    public sealed class UiPointerBoundary : IUiPointerOwnershipRegistrar
    {
        private readonly Dictionary<int, UiPointerOwnership> ownershipByPointer = new();
        private int sceneBlockCount;

        public UiPointerOwnership GetOwnership(int pointerId)
        {
            return ownershipByPointer.TryGetValue(pointerId, out var ownership)
                ? ownership
                : UiPointerOwnership.None;
        }

        public void RegisterUiPointerPress(int pointerId)
        {
            RegisterPress(pointerId, UiPointerOwnership.Ui);
        }

        public void RegisterScenePointerPress(int pointerId)
        {
            RegisterPress(pointerId, UiPointerOwnership.Scene);
        }

        public bool CanProcessScenePointer(int pointerId)
        {
            return sceneBlockCount == 0
                && GetOwnership(pointerId) != UiPointerOwnership.Ui;
        }

        public void ReleasePointer(int pointerId)
        {
            ownershipByPointer.Remove(pointerId);
        }

        public IDisposable AcquireSceneBlock()
        {
            sceneBlockCount++;
            return new SceneBlockHandle(this);
        }

        /// <summary>
        /// Toast is presentation-only and deliberately never becomes a pointer owner.
        /// Toast ä»…ç”¨äºŽå±•ç¤ºï¼Œæ•…æ„ä¸å–å¾— pointer ownershipã€‚
        /// </summary>
        public void NotifyToastShown()
        {
        }

        private void RegisterPress(int pointerId, UiPointerOwnership ownership)
        {
            if (!ownershipByPointer.ContainsKey(pointerId))
            {
                ownershipByPointer.Add(pointerId, ownership);
            }
        }

        private void ReleaseSceneBlock()
        {
            if (sceneBlockCount > 0)
            {
                sceneBlockCount--;
            }
        }

        private sealed class SceneBlockHandle : IDisposable
        {
            private UiPointerBoundary boundary;

            public SceneBlockHandle(UiPointerBoundary owner)
            {
                boundary = owner;
            }

            public void Dispose()
            {
                if (boundary == null)
                {
                    return;
                }

                boundary.ReleaseSceneBlock();
                boundary = null;
            }
        }
    }

    /// <summary>
    /// Attach to a UI raycast target so its press claims the pointer for UI.
    /// æŒ‚åˆ° UI raycast targetï¼Œç”± press ä¸º UI å£°æ˜Ž pointer ownershipã€‚
    /// </summary>
    public sealed class UiPointerBoundaryEventHook : MonoBehaviour,
        IPointerDownHandler
    {
        private IUiPointerOwnershipRegistrar boundary;

        public void Configure(IUiPointerOwnershipRegistrar pointerBoundary)
        {
            boundary = pointerBoundary;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            boundary?.RegisterUiPointerPress(eventData.pointerId);
        }
    }
}

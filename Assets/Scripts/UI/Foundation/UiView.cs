using System;

namespace AnimalCafe.UI.Foundation
{
    public enum UiViewKind
    {
        Hud,
        MainPanel,
        Modal,
        BottomSheet
    }

    public enum UiPausePolicy
    {
        ContinueGame,
        PauseGame
    }

    public enum UiOutsideDismissPolicy
    {
        NotDismissible,
        Dismissible
    }

    /// <summary>
    /// A pure lifecycle contract shared by future Panel, Modal and Bottom Sheet components.
    /// 纯状态 contract；实际 Prefab、动画与 input 行为在后续任务接入。
    /// </summary>
    public sealed class UiView
    {
        public UiView(
            string viewId,
            UiViewKind kind,
            UiPausePolicy pausePolicy,
            UiOutsideDismissPolicy outsideDismissPolicy)
        {
            if (string.IsNullOrWhiteSpace(viewId))
            {
                throw new ArgumentException("A UI view requires an identifier.", nameof(viewId));
            }

            ViewId = viewId;
            Kind = kind;
            PausePolicy = pausePolicy;
            OutsideDismissPolicy = outsideDismissPolicy;
        }

        public string ViewId { get; }
        public UiViewKind Kind { get; }
        public UiPausePolicy PausePolicy { get; }
        public UiOutsideDismissPolicy OutsideDismissPolicy { get; }
        public bool IsOpen { get; private set; }
        public bool IsDestroyed { get; private set; }

        public void Open()
        {
            if (!IsDestroyed)
            {
                IsOpen = true;
            }
        }

        public void Close()
        {
            IsOpen = false;
        }

        public void Destroy()
        {
            IsOpen = false;
            IsDestroyed = true;
        }
    }
}

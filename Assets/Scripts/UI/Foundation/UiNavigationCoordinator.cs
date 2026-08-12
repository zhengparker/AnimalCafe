using System;
using System.Collections.Generic;

namespace AnimalCafe.UI.Foundation
{
    /// <summary>
    /// Owns navigation order only. HUD is deliberately absent, so Back never closes it.
    /// 只管理导航层级；HUD 不会注册到这里，因此不会被 Back 关闭。
    /// </summary>
    public sealed class UiNavigationCoordinator
    {
        private readonly List<UiView> modalStack = new List<UiView>();
        private UiView activeMainPanel;
        private UiView activeBottomSheet;

        public UiView ActiveMainPanel
        {
            get
            {
                RemoveStaleViews();
                return activeMainPanel;
            }
        }

        public UiView ActiveBottomSheet
        {
            get
            {
                RemoveStaleViews();
                return activeBottomSheet;
            }
        }

        public bool IsTopModal(UiView view)
        {
            RemoveStaleViews();
            return view != null
                && modalStack.Count > 0
                && ReferenceEquals(modalStack[modalStack.Count - 1], view);
        }

        public UiViewHandle OpenMainPanel(UiView view)
        {
            EnsureKind(view, UiViewKind.MainPanel);
            RemoveStaleViews();

            if (!ReferenceEquals(activeMainPanel, view))
            {
                CloseView(activeMainPanel);
                activeMainPanel = view;
            }

            view.Open();
            return new UiViewHandle(this, view);
        }

        public UiViewHandle PushModal(UiView view)
        {
            EnsureKind(view, UiViewKind.Modal);
            RemoveStaleViews();

            modalStack.Add(view);
            view.Open();
            return new UiViewHandle(this, view);
        }

        public UiViewHandle OpenBottomSheet(UiView view)
        {
            EnsureKind(view, UiViewKind.BottomSheet);
            RemoveStaleViews();

            if (!ReferenceEquals(activeBottomSheet, view))
            {
                CloseView(activeBottomSheet);
                activeBottomSheet = view;
            }

            view.Open();
            return new UiViewHandle(this, view);
        }

        public bool TryHandleBack()
        {
            RemoveStaleViews();

            if (modalStack.Count > 0)
            {
                CloseRegisteredView(modalStack[modalStack.Count - 1]);
                return true;
            }

            if (activeBottomSheet != null)
            {
                CloseRegisteredView(activeBottomSheet);
                return true;
            }

            if (activeMainPanel != null)
            {
                CloseRegisteredView(activeMainPanel);
                return true;
            }

            return false;
        }

        public bool RequestOutsideDismiss()
        {
            RemoveStaleViews();

            var topView = modalStack.Count > 0
                ? modalStack[modalStack.Count - 1]
                : activeBottomSheet;

            if (topView == null || topView.OutsideDismissPolicy != UiOutsideDismissPolicy.Dismissible)
            {
                return false;
            }

            CloseRegisteredView(topView);
            return true;
        }

        internal void CloseRegisteredView(UiView view)
        {
            if (view == null)
            {
                return;
            }

            modalStack.Remove(view);

            if (ReferenceEquals(activeBottomSheet, view))
            {
                activeBottomSheet = null;
            }

            if (ReferenceEquals(activeMainPanel, view))
            {
                activeMainPanel = null;
            }

            CloseView(view);
            RemoveStaleViews();
        }

        private void RemoveStaleViews()
        {
            modalStack.RemoveAll(IsStale);

            if (IsStale(activeBottomSheet))
            {
                activeBottomSheet = null;
            }

            if (IsStale(activeMainPanel))
            {
                activeMainPanel = null;
            }
        }

        private void EnsureKind(UiView view, UiViewKind expectedKind)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (view.Kind != expectedKind)
            {
                throw new ArgumentException(
                    "Expected a " + expectedKind + " view, but received " + view.Kind + ".",
                    nameof(view));
            }
        }

        private bool IsStale(UiView view)
        {
            return view == null || view.IsDestroyed || !view.IsOpen;
        }

        private void CloseView(UiView view)
        {
            if (view != null)
            {
                view.Close();
            }
        }
    }

    /// <summary>
    /// A close handle is intentionally idempotent, including after its view is destroyed.
    /// Close 可重复调用；view 被销毁后调用同样安全。
    /// </summary>
    public sealed class UiViewHandle
    {
        private readonly UiNavigationCoordinator coordinator;
        private readonly UiView view;

        internal UiViewHandle(UiNavigationCoordinator coordinator, UiView view)
        {
            this.coordinator = coordinator;
            this.view = view;
        }

        public void Close()
        {
            coordinator.CloseRegisteredView(view);
        }
    }
}

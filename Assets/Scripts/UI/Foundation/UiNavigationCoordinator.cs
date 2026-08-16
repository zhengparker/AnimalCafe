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
        private readonly Dictionary<UiView, PresentationRegistration> registrations =
            new Dictionary<UiView, PresentationRegistration>();
        private UiView activeMainPanel;
        private UiView activeBottomSheet;
        private int nextRegistrationToken;

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
                CloseRegisteredView(activeMainPanel);
                activeMainPanel = view;
            }

            view.Open();
            return Register(view, null, allowBack: true, allowOutside: false);
        }

        public UiViewHandle PushModal(UiView view)
        {
            return PushModal(view, null, allowBack: true, allowOutside: true);
        }

        public UiViewHandle PushModal(
            UiView view,
            Action onClosed,
            bool allowBack,
            bool allowOutside)
        {
            EnsureKind(view, UiViewKind.Modal);
            RemoveStaleViews();

            modalStack.Add(view);
            view.Open();
            return Register(view, onClosed, allowBack, allowOutside);
        }

        public UiViewHandle OpenBottomSheet(UiView view)
        {
            return OpenBottomSheet(view, null, allowBack: true, allowOutside: true);
        }

        public UiViewHandle OpenBottomSheet(
            UiView view,
            Action onClosed,
            bool allowBack,
            bool allowOutside)
        {
            EnsureKind(view, UiViewKind.BottomSheet);
            RemoveStaleViews();

            if (!ReferenceEquals(activeBottomSheet, view))
            {
                CloseRegisteredView(activeBottomSheet);
                activeBottomSheet = view;
            }

            view.Open();
            return Register(view, onClosed, allowBack, allowOutside);
        }

        public bool TryHandleBack()
        {
            RemoveStaleViews();

            if (modalStack.Count > 0)
            {
                var modal = modalStack[modalStack.Count - 1];
                if (!AllowsBack(modal))
                {
                    return false;
                }

                CloseRegisteredView(modal);
                return true;
            }

            if (activeBottomSheet != null)
            {
                if (!AllowsBack(activeBottomSheet))
                {
                    return false;
                }

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

        public bool TryHandleBack(UiView expectedTopView)
        {
            RemoveStaleViews();
            if (!ReferenceEquals(GetTopContainer(), expectedTopView) || !AllowsBack(expectedTopView))
            {
                return false;
            }

            CloseRegisteredView(expectedTopView);
            return true;
        }

        public bool RequestOutsideDismiss()
        {
            RemoveStaleViews();

            var topView = modalStack.Count > 0
                ? modalStack[modalStack.Count - 1]
                : activeBottomSheet;

            if (topView == null
                || topView.OutsideDismissPolicy != UiOutsideDismissPolicy.Dismissible
                || !AllowsOutside(topView))
            {
                return false;
            }

            CloseRegisteredView(topView);
            return true;
        }

        public bool RequestOutsideDismiss(UiView expectedTopView)
        {
            RemoveStaleViews();
            if (!ReferenceEquals(GetTopContainer(), expectedTopView)
                || expectedTopView == null
                || expectedTopView.OutsideDismissPolicy != UiOutsideDismissPolicy.Dismissible
                || !AllowsOutside(expectedTopView))
            {
                return false;
            }

            CloseRegisteredView(expectedTopView);
            return true;
        }

        internal void CloseRegisteredView(UiView view)
        {
            CloseRegisteredView(view, null);
        }

        internal void CloseRegisteredView(UiView view, int? expectedToken)
        {
            if (view == null)
            {
                return;
            }

            if (expectedToken.HasValue
                && (!registrations.TryGetValue(view, out var current)
                    || current.Token != expectedToken.Value))
            {
                return;
            }

            registrations.TryGetValue(view, out var registration);
            registrations.Remove(view);
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
            registration?.OnClosed?.Invoke();
        }

        private void RemoveStaleViews()
        {
            for (var index = modalStack.Count - 1; index >= 0; index--)
            {
                if (IsStale(modalStack[index]))
                {
                    CloseRegisteredView(modalStack[index]);
                }
            }

            if (IsStale(activeBottomSheet))
            {
                CloseRegisteredView(activeBottomSheet);
            }

            if (IsStale(activeMainPanel))
            {
                CloseRegisteredView(activeMainPanel);
            }
        }

        private UiViewHandle Register(
            UiView view,
            Action onClosed,
            bool allowBack,
            bool allowOutside)
        {
            var token = ++nextRegistrationToken;
            registrations[view] = new PresentationRegistration(
                token, onClosed, allowBack, allowOutside);
            return new UiViewHandle(this, view, token);
        }

        private UiView GetTopContainer()
        {
            if (modalStack.Count > 0)
            {
                return modalStack[modalStack.Count - 1];
            }

            return activeBottomSheet ?? activeMainPanel;
        }

        private bool AllowsBack(UiView view)
        {
            return !registrations.TryGetValue(view, out var registration)
                || registration.AllowBack;
        }

        private bool AllowsOutside(UiView view)
        {
            return !registrations.TryGetValue(view, out var registration)
                || registration.AllowOutside;
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

        private sealed class PresentationRegistration
        {
            public PresentationRegistration(
                int token,
                Action onClosed,
                bool allowBack,
                bool allowOutside)
            {
                Token = token;
                OnClosed = onClosed;
                AllowBack = allowBack;
                AllowOutside = allowOutside;
            }

            public int Token { get; }
            public Action OnClosed { get; }
            public bool AllowBack { get; }
            public bool AllowOutside { get; }
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
        private readonly int registrationToken;

        internal UiViewHandle(UiNavigationCoordinator coordinator, UiView view, int registrationToken)
        {
            this.coordinator = coordinator;
            this.view = view;
            this.registrationToken = registrationToken;
        }

        public void Close()
        {
            coordinator.CloseRegisteredView(view, registrationToken);
        }
    }
}

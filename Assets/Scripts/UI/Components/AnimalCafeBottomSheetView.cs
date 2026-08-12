using System;
using AnimalCafe.UI.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.Components
{
    /// <summary>
    /// Connects an ordinary Bottom Sheet to outside dismissal and shared Back.
    /// 将普通 Bottom Sheet 接入 outside dismiss 与 shared Back。
    /// </summary>
    public sealed class AnimalCafeBottomSheetView : MonoBehaviour
    {
        private UiNavigationCoordinator navigation;
        private UiView view;
        private Button outsideButton;
        private UiViewHandle navigationHandle;

        public void Configure(
            UiNavigationCoordinator coordinator,
            UiView bottomSheetView,
            Button outside)
        {
            outsideButton?.onClick.RemoveListener(HandleOutside);
            navigation = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            view = bottomSheetView ?? throw new ArgumentNullException(nameof(bottomSheetView));
            if (view.Kind != UiViewKind.BottomSheet)
            {
                throw new ArgumentException(
                    "Bottom Sheet component requires a BottomSheet UiView.", nameof(bottomSheetView));
            }

            outsideButton = outside ?? throw new ArgumentNullException(nameof(outside));
            outsideButton.onClick.AddListener(HandleOutside);
        }

        public void Open()
        {
            navigationHandle?.Close();
            navigationHandle = navigation.OpenBottomSheet(view);
        }

        public bool TryHandleBack()
        {
            return navigation.TryHandleBack();
        }

        private void HandleOutside()
        {
            navigation.RequestOutsideDismiss();
        }

        private void Close()
        {
            navigationHandle?.Close();
            navigationHandle = null;
        }

        private void OnDestroy()
        {
            Close();
            outsideButton?.onClick.RemoveListener(HandleOutside);
        }
    }
}

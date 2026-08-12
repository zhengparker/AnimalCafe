using System;
using AnimalCafe.UI.Foundation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnimalCafe.UI.Components
{
    /// <summary>
    /// Applies semantic Button role/state styling for Touch input. No Hover state.
    /// 为 Touch Button 应用语义 role/state；不提供 Hover。
    /// </summary>
    public sealed class AnimalCafeButtonView : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler
    {
        private AnimalCafeUiTheme theme;
        private UiButtonRole role;
        private Button button;
        private Image background;
        private bool isPointerDown;

        public UiButtonState CurrentState { get; private set; }

        public void Configure(
            AnimalCafeUiTheme uiTheme,
            UiButtonRole buttonRole,
            Button targetButton,
            Image targetBackground)
        {
            theme = uiTheme ?? throw new ArgumentNullException(nameof(uiTheme));
            button = targetButton ?? throw new ArgumentNullException(nameof(targetButton));
            background = targetBackground ?? throw new ArgumentNullException(nameof(targetBackground));
            role = buttonRole;
            isPointerDown = false;
            RefreshState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button != null && button.IsInteractable())
            {
                isPointerDown = true;
                RefreshState();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPointerDown = false;
            RefreshState();
        }

        private void Update()
        {
            RefreshState();
        }

        private void OnDisable()
        {
            isPointerDown = false;
        }

        private void RefreshState()
        {
            if (theme == null || button == null || background == null)
            {
                return;
            }

            CurrentState = !button.IsInteractable()
                ? UiButtonState.Disabled
                : isPointerDown
                    ? UiButtonState.Pressed
                    : UiButtonState.Default;

            var defaultColor = role switch
            {
                UiButtonRole.Primary => theme.Colors.Accent,
                UiButtonRole.Secondary => theme.Colors.Surface,
                UiButtonRole.Destructive => theme.Colors.Destructive,
                _ => theme.Colors.Surface
            };

            background.color = CurrentState switch
            {
                UiButtonState.Disabled => theme.Colors.Disabled,
                UiButtonState.Pressed => Color.Lerp(defaultColor, Color.black, 0.15f),
                _ => defaultColor
            };
        }
    }
}

using System;
using AnimalCafe.Decoration;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.Decoration
{
    public sealed class DecorationFloorRangeView : MonoBehaviour
    {
        [SerializeField] private AnimalCafe.UI.P8R.P8RAppearance appearance;
        [SerializeField] private Button wholeRoomButton;
        [SerializeField] private Button singleGridButton;
        private bool listenersBound;
        private bool refreshingLayout;

        public event Func<SurfaceEditScope, bool> RangeRequested;
        public SurfaceEditScope SelectedRange { get; private set; } =
            SurfaceEditScope.WholeRoomFloor;

        public void RefreshMobileLayout() => ApplySelectedVisual();

        public void RefreshCatalogueVisibility()
        {
            if (appearance == null) return;
            var catalogue = GetComponentInParent<DecorationCatalogueView>();
            var visible = catalogue == null || catalogue.IsExpandedPanelVisible;
            // Range choices belong to the open panel; preview actions stay in their own footer.
            // 范围选项随目录收起；Apply/Cancel 等预览操作保持独立。
            var group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = visible ? 1f : 0f;
            group.interactable = group.blocksRaycasts = visible;
        }


        public void Configure(Button wholeRoom, Button singleGrid)
        {
            UnbindListeners();
            wholeRoomButton = wholeRoom;
            singleGridButton = singleGrid;
            if (isActiveAndEnabled)
            {
                BindListeners();
            }
            ApplySelectedVisual();
        }

        public void SetSelected(SurfaceEditScope range)
        {
            if (range != SurfaceEditScope.WholeRoomFloor
                && range != SurfaceEditScope.SingleGridFloor)
            {
                throw new ArgumentOutOfRangeException(nameof(range));
            }

            SelectedRange = range;
            ApplySelectedVisual();
        }

        private void OnEnable()
        {
            AnimalCafe.UI.P8R.P8RMobileMetrics.Changed += ApplySelectedVisual;
            BindListeners();
            ApplySelectedVisual();
        }

        private void OnDisable()
        {
            AnimalCafe.UI.P8R.P8RMobileMetrics.Changed -= ApplySelectedVisual;
            UnbindListeners();
        }
        private void OnRectTransformDimensionsChange()
        {
            if (appearance != null && !refreshingLayout) ApplySelectedVisual();
        }
        private void OnDestroy() => UnbindListeners();

        private void BindListeners()
        {
            if (listenersBound)
            {
                return;
            }

            wholeRoomButton?.onClick.AddListener(HandleWholeRoom);
            singleGridButton?.onClick.AddListener(HandleSingleGrid);
            listenersBound = true;
        }

        private void UnbindListeners()
        {
            if (!listenersBound)
            {
                return;
            }

            wholeRoomButton?.onClick.RemoveListener(HandleWholeRoom);
            singleGridButton?.onClick.RemoveListener(HandleSingleGrid);
            listenersBound = false;
        }

        private void HandleWholeRoom() => Request(SurfaceEditScope.WholeRoomFloor);
        private void HandleSingleGrid() => Request(SurfaceEditScope.SingleGridFloor);

        private void Request(SurfaceEditScope requested)
        {
            if (RangeRequested != null)
            {
                foreach (Func<SurfaceEditScope, bool> gate in RangeRequested.GetInvocationList())
                {
                    if (!gate(requested))
                    {
                        return;
                    }
                }
            }

            SetSelected(requested);
        }

        private void ApplySelectedVisual()
        {
            if (wholeRoomButton != null)
            {
                wholeRoomButton.interactable = SelectedRange != SurfaceEditScope.WholeRoomFloor;
            }
            if (singleGridButton != null)
            {
                singleGridButton.interactable = SelectedRange != SurfaceEditScope.SingleGridFloor;
            }
            if (appearance != null)
            {
                if (refreshingLayout) return;
                refreshingLayout = true;
                var root = (RectTransform)transform;
                var width = transform.parent is RectTransform host ? host.rect.width : 800f;
                var measure = wholeRoomButton != null ? wholeRoomButton.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
                var catalogue = GetComponentInParent<DecorationCatalogueView>();
                var layout = AnimalCafe.UI.P8R.P8RSurfaceFooterLayout.Measure(this, width, appearance, measure, true,
                    catalogue == null || catalogue.HasActivePreview);
                root.anchorMin = root.anchorMax = new Vector2(.5f, 0);
                root.pivot = new Vector2(.5f, 0);
                root.anchoredPosition = Vector2.zero;
                root.sizeDelta = new Vector2(width, layout.Height);
                var buttons = new[] { wholeRoomButton, singleGridButton };
                for (var i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] == null) continue;
                    var rect = (RectTransform)buttons[i].transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
                    rect.pivot = Vector2.one * .5f; rect.anchoredPosition = layout.Centers[i];
                    rect.sizeDelta = new Vector2(layout.Widths[i], layout.RowHeight);
                }
                appearance.Tab(wholeRoomButton, "whole_room", SelectedRange == SurfaceEditScope.WholeRoomFloor);
                appearance.Tab(singleGridButton, "single_grid", SelectedRange == SurfaceEditScope.SingleGridFloor);
                // A selected range is disabled only to make repeated selection a no-op, not unavailable.
                // 已选中范围仍使用批准的彩色图；只保持现有禁止重复点击的语义。
                for (var i = 0; i < buttons.Length; i++)
                {
                    var icon = buttons[i]?.transform.Find("Icon")?.GetComponent<Image>();
                    if (icon != null) appearance.Paint(icon, i == 0 ? "whole_room_cocoa" : "single_grid_cocoa", false);
                    var label = buttons[i]?.GetComponentInChildren<TMPro.TMP_Text>(true);
                    if (label != null) label.fontStyle = TMPro.FontStyles.Bold;
                    AnimalCafe.UI.P8R.P8RButtonLayout.SurfaceButton(buttons[i]);
                    if (buttons[i] != null && buttons[i].image != null)
                        buttons[i].image.rectTransform.sizeDelta = new Vector2(layout.Widths[i] - AnimalCafe.UI.P8R.P8RMobileMetrics.For(this).Units(4),
                            AnimalCafe.UI.P8R.P8RMobileMetrics.For(this).Units(32));
                }
                RefreshCatalogueVisibility();
                refreshingLayout = false;
            }
        }
    }
}

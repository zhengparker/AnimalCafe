using System;
using AnimalCafe.Decoration;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.Decoration
{
    public sealed class DecorationFloorRangeView : MonoBehaviour
    {
        [SerializeField] private Button wholeRoomButton;
        [SerializeField] private Button singleGridButton;
        private bool listenersBound;

        public event Func<SurfaceEditScope, bool> RangeRequested;
        public SurfaceEditScope SelectedRange { get; private set; } =
            SurfaceEditScope.WholeRoomFloor;

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
            BindListeners();
            ApplySelectedVisual();
        }

        private void OnDisable() => UnbindListeners();
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
        }
    }
}

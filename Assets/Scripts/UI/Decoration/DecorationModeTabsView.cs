using System;
using System.Collections.Generic;
using AnimalCafe.Decoration;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.Decoration
{
    public sealed class DecorationModeTabsView : MonoBehaviour
    {
        [SerializeField] private Button furnitureButton;
        [SerializeField] private Button floorButton;
        [SerializeField] private Button wallButton;
        [SerializeField] private Button wallDecorButton;
        [SerializeField,Min(1f)] private float activeRaiseOffset=12f;
        private readonly Dictionary<Button,float> inactiveAnchoredY=new Dictionary<Button,float>();
        public event Action<DecorationModeKind> Selected;
        public event Func<DecorationModeKind, bool> ModeRequested;
        public DecorationModeKind ActiveMode { get; private set; } = DecorationModeKind.Furniture;
        public void SetActive(DecorationModeKind mode)
        {
            if (!Enum.IsDefined(typeof(DecorationModeKind), mode)) return;
            ActiveMode = mode;
            ApplyActiveVisual(mode);
            Selected?.Invoke(mode);
        }
        public bool RequestMode(DecorationModeKind mode)
        {
            if (!Enum.IsDefined(typeof(DecorationModeKind), mode)) return false;
            if (ModeRequested != null)
            {
                foreach (Func<DecorationModeKind, bool> request in ModeRequested.GetInvocationList())
                    if (!request(mode)) return false;
            }
            SetActive(mode); return true;
        }
        private Button Get(DecorationModeKind mode) => mode == DecorationModeKind.Furniture ? furnitureButton : mode == DecorationModeKind.Floor ? floorButton : mode == DecorationModeKind.Wall ? wallButton : wallDecorButton;
        private void Awake()
        {
            furnitureButton?.onClick.AddListener(HandleFurnitureSelected);
            floorButton?.onClick.AddListener(HandleFloorSelected);
            wallButton?.onClick.AddListener(HandleWallSelected);
            wallDecorButton?.onClick.AddListener(HandleWallDecorSelected);
            ApplyActiveVisual(DecorationModeKind.Furniture);
        }
        private void OnEnable()=>ApplyActiveVisual(ActiveMode);
        private void OnDisable()=>RestoreInactivePositions();
        private void OnDestroy()
        {
            furnitureButton?.onClick.RemoveListener(HandleFurnitureSelected);
            floorButton?.onClick.RemoveListener(HandleFloorSelected);
            wallButton?.onClick.RemoveListener(HandleWallSelected);
            wallDecorButton?.onClick.RemoveListener(HandleWallDecorSelected);
        }
        private void ApplyActiveVisual(DecorationModeKind mode)
        {
            var button = Get(mode);
            foreach(var candidate in new[]{furnitureButton,floorButton,wallButton,wallDecorButton})
                if(candidate!=null)
                {
                    var rect=candidate.transform as RectTransform;if(rect!=null&&!inactiveAnchoredY.ContainsKey(candidate))inactiveAnchoredY[candidate]=rect.anchoredPosition.y;
                    if(rect!=null&&inactiveAnchoredY.TryGetValue(candidate,out var baseline)){var position=rect.anchoredPosition;position.y=baseline+(candidate==button?activeRaiseOffset:0f);rect.anchoredPosition=position;}
                    if(candidate.image!=null)candidate.image.color=candidate==button?new Color(.28f,.48f,.34f,1f):new Color(.86f,.82f,.70f,1f);
                }
            if (button != null) button.transform.SetSiblingIndex(transform.childCount - 1);
        }
        private void RestoreInactivePositions(){foreach(var item in inactiveAnchoredY){if(item.Key==null)continue;var rect=item.Key.transform as RectTransform;if(rect==null)continue;var position=rect.anchoredPosition;position.y=item.Value;rect.anchoredPosition=position;}}
        private void HandleFurnitureSelected() => RequestMode(DecorationModeKind.Furniture);
        private void HandleFloorSelected() => RequestMode(DecorationModeKind.Floor);
        private void HandleWallSelected() => RequestMode(DecorationModeKind.Wall);
        private void HandleWallDecorSelected() => RequestMode(DecorationModeKind.WallDecor);
    }
}

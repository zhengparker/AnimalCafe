using System;
using System.Collections;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Core.Time;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.Customers
{
    // P12验证入口；seed只在独立验证scene启用，不修改正式Save。
    public sealed class CustomerQueueValidationController : MonoBehaviour
    {
        [SerializeField] private CustomerFlowController flow;
        [SerializeField] private CafeLayoutRuntime runtime;
        [SerializeField] private FurnitureContentCatalog catalog;
        [SerializeField] private DecorationModeController decoration;
        [SerializeField] private NavigationDecorationBridge bridge;
        [SerializeField] private GameTimeService time;
        [SerializeField] private Text status;
        [SerializeField] private GameObject validationHud;
        [SerializeField] private Button leaveButton,retryButton;
        [SerializeField] private bool seedValidationLayout;
        private IDisposable startupBlock;
        private IEnumerator Start()
        {
            if(leaveButton!=null) leaveButton.onClick.AddListener(Leave);
            if(retryButton!=null) retryButton.onClick.AddListener(Retry);
            if(!seedValidationLayout) yield break;
            startupBlock=time.AcquireResumeBlock(this,"正在准备P12验证布局");
            yield return null; // 先让已有Decoration完成初始化。
            try
            {
                runtime.Initialize();
                var support=runtime.Layout.FurnitureInstances.OrderBy(f=>f.InstanceId,StringComparer.Ordinal).First();
                var slot=catalog.BuildSurfaceSlotCatalog(runtime.Layout.GridSettings).GetForSupport(support.DefinitionId).First().SlotId;
                Require(runtime.Layout.PlaceFurniture(FurnitureInstance.Restore("12000000000000000000000000000002",support.DefinitionId,new GridPosition(4,3),FurnitureRotation.Degrees0)).Succeeded);
                Require(runtime.Layout.PlaceFurniture(FurnitureInstance.Restore("12000000000000000000000000000003",support.DefinitionId,new GridPosition(6,3),FurnitureRotation.Degrees0)).Succeeded);
                Require(runtime.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance("12000000000000000000000000000004","equipment.cash-register.01",new SurfaceSlotAddress(support.InstanceId,slot),FurnitureRotation.Degrees0)).Succeeded);
                Require(runtime.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance("12000000000000000000000000000005","equipment.coffee-machine.01",new SurfaceSlotAddress("12000000000000000000000000000002",slot),FurnitureRotation.Degrees0)).Succeeded);
                Require(runtime.FunctionalSurfaceLayout.PlacePickUp(new PickUpPointInstance("12000000000000000000000000000006",new SurfaceSlotAddress("12000000000000000000000000000003",slot))).Succeeded);
                decoration.RefreshConfirmedNavigationPresentation();
            }
            finally { startupBlock?.Dispose(); startupBlock=null; }
            bridge.ResumeAfterRepair(); time.TrySetAutomaticSpeed(GameSpeed.Normal);
        }
        private static void Require(bool value) { if(!value) throw new InvalidOperationException("P12 validation seed rejected"); }
        private void Leave() => flow.TryLetFrontLeave();
        private void Retry() { if(bridge!=null) bridge.ResumeAfterRepair(); flow.ResumeBlockedVisits(); }
        private void Update()
        {
            // 装修工具使用同一底部空间；只隐藏HUD，controller保留启用。
            if(validationHud!=null) validationHud.SetActive(decoration==null || !decoration.IsOpen);
            if(status==null || flow==null) return;
            var ledgers=flow.Capacity?.GetCapacities();
            status.text="P12 顾客与队伍\n"+flow.Status+"\n下次到店："+flow.RemainingSeconds.ToString("F1")+" 游戏秒\n"+
                "Register: "+flow.SelectedRegisterId+"\n"+(ledgers==null?"等待容量":string.Join("  ",ledgers.Select(l=>l.Kind+" "+l.Used+"/"+l.Limit)))+
                "\n"+string.Join(" | ",flow.Snapshot.Select(v=>v.VisitId.Substring(Math.Max(0,v.VisitId.Length-8))+" "+v.State));
            if(leaveButton!=null) leaveButton.interactable=decoration==null || !decoration.IsOpen;
        }
        private void OnDestroy()
        { startupBlock?.Dispose(); if(leaveButton!=null) leaveButton.onClick.RemoveListener(Leave); if(retryButton!=null) retryButton.onClick.RemoveListener(Retry); }
    }
}


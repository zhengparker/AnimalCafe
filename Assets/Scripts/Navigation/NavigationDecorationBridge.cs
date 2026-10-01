using System;
using System.Collections.Generic;
using AnimalCafe.Decoration;
using AnimalCafe.Core.Time;
using AnimalCafe.UI.Foundation;
using UnityEngine;
namespace AnimalCafe.Navigation
{
    // Optional scene integration. Legacy scenes have no bridge and retain their current flow.
    [DefaultExecutionOrder(-100)]
    public sealed class NavigationDecorationBridge : MonoBehaviour
    {
        public const string ActorOverlapMessage = "这里有角色，请换个位置。";
        [SerializeField] private NavigationLayoutAdapter adapter;
        [SerializeField] private bool enforceBusinessReadiness;
        [SerializeField] private GameTimeService gameTimeService;
        private UiPauseCoordinator pauseCoordinator;
        private IDisposable resumeBlock;
        private NavigationWorld configuredWorld;
        private GameTimeService lastValidTimeOwner;
        public const string BlockedMessage = "路径被挡住了，请进入装修调整。";
        public bool EnforceBusinessReadiness => enforceBusinessReadiness;
        public NavigationLayoutAdapter Adapter => adapter;
        private void Awake()
        { configuredWorld=adapter != null ? adapter.World : null; if(gameTimeService!=null) lastValidTimeOwner=gameTimeService; }
        public void Configure(NavigationLayoutAdapter value)
        { adapter=value; configuredWorld=value != null ? value.World : null; enforceBusinessReadiness=value != null && value.EnforceBusinessReadiness; }
        public void ConfigureTime(GameTimeService time, UiPauseCoordinator coordinator = null)
        { gameTimeService=time; if(time!=null) lastValidTimeOwner=time; if(coordinator != null) pauseCoordinator=coordinator; }
        private bool HasBusinessConfiguration()
        {
            if(!isActiveAndEnabled || gameTimeService==null || !gameTimeService.isActiveAndEnabled ||
                adapter==null || configuredWorld==null || adapter.World!=configuredWorld) return false;
            return adapter.ValidateBusinessConfiguration(out _);
        }
        public bool PrepareExit()
        {
            if(!enforceBusinessReadiness) return true;
            if(!HasBusinessConfiguration()) { FailClosed(); return false; }
            if(resumeBlock == null) resumeBlock=gameTimeService.AcquireResumeBlock(this,BlockedMessage);
            adapter.RefreshConfirmedLayout();
            var readiness=adapter.RebuildAndValidate();
            if(!HasBusinessConfiguration()) { FailClosed(); return false; }
            if(!readiness.CanResume) { configuredWorld.HoldUnavailableResumeBlock(lastValidTimeOwner); return false; }
            configuredWorld.ReleaseValidatedResumeBlock();
            resumeBlock.Dispose(); resumeBlock=null;
            return !gameTimeService.IsResumeBlocked;
        }
        public bool ResumeAfterRepair()
        {
            if(!PrepareExit()) return false;
            pauseCoordinator?.TryRestorePendingSpeed();
            return gameTimeService != null && !gameTimeService.IsResumeBlocked;
        }
        public void SuspendForDecorationShutdown()
        {
            // Teardown must hold the pause gate without baking replacement navigation data.
            // 销毁/禁用时只暂停并释放；正常退出仍使用 PrepareExit 验证和恢复。
            if(enforceBusinessReadiness) FailClosed();
        }
        private void Update()
        {
            if(enforceBusinessReadiness && !HasBusinessConfiguration()) FailClosed();
        }
        private void FailClosed()
        {
            // Stop requests before releasing owned data; the surviving world keeps the gate.
            // Keep the last valid owner only to pause safely after a reference is lost.
            // A destroyed owner compares null; still suspend world, never create another time service.
            if(configuredWorld!=null) configuredWorld.HoldUnavailableResumeBlock(lastValidTimeOwner);
            if(adapter!=null) adapter.ReleaseOwnedNavigation();
            if(configuredWorld != null) { resumeBlock?.Dispose(); resumeBlock=null; }
            else if(lastValidTimeOwner != null && resumeBlock == null)
                resumeBlock=lastValidTimeOwner.AcquireResumeBlock(this,BlockedMessage);
        }
        private void OnDisable() { if(enforceBusinessReadiness) FailClosed(); }
        private void OnDestroy() { resumeBlock?.Dispose(); resumeBlock=null; }
        public bool CanConfirm(IReadOnlyList<NavigationSolidPose> candidateSolids,out string reason)
        {
            reason="";
            if(adapter==null || adapter.World==null) { reason="Navigation layout unavailable"; return false; }
            if(!adapter.ValidateRepresentationRoots(out reason)) return false;
            return CheckActorOverlap(candidateSolids,out reason);
        }
        private bool CheckActorOverlap(IReadOnlyList<NavigationSolidPose> candidateSolids,out string reason)
        {
            reason="";
            if(candidateSolids==null) { reason="Navigation geometry unavailable"; return false; }
            var probe=new GameObject("Navigation placement probe") { hideFlags=HideFlags.HideAndDontSave };
            probe.layer=2; var box=probe.AddComponent<BoxCollider>(); box.isTrigger=true;
            try
            {
                foreach(var solid in candidateSolids)
                {
                    box.size=solid.Size;
                    foreach(var actor in adapter.World.RegisteredActors)
                    {
                        if(actor==null || actor.Proxy==null) { reason="Navigation actor unavailable"; return false; }
                        if(Physics.ComputePenetration(box,solid.Position,solid.Rotation,actor.Proxy,
                            actor.Proxy.transform.position,actor.Proxy.transform.rotation,out _,out var depth) && depth>.001f)
                        { reason=ActorOverlapMessage; return false; }
                    }
                }
                return true;
            }
            finally { probe.SetActive(false); if(Application.isPlaying) Destroy(probe); else DestroyImmediate(probe); }
        }
        // Validate before deriving candidates so a bad root cannot produce an overlap-looking error.
        private bool CanConfirmCandidate(Func<IReadOnlyList<NavigationSolidPose>> candidate,out string reason)
        {
            if(adapter==null) { reason="Navigation layout unavailable"; return false; }
            if(!adapter.ValidateRepresentationRoots(out reason)) return false;
            return CanConfirm(candidate(),out reason);
        }
        public bool CanConfirmFurniture(FurniturePlacementPreview preview,out string reason) => CanConfirmCandidate(()=>adapter.FurnitureCandidate(preview),out reason);
        public bool CanConfirmMounted(FunctionalSurfacePlacementPreview preview,out string reason) => CanConfirmCandidate(()=>adapter.MountedCandidate(preview),out reason);
        public bool CanConfirmWall(WallMountedPlacementPreview preview,out string reason) => CanConfirmCandidate(()=>adapter.WallCandidate(preview),out reason);
        // Preview shares the overlap rule, but does not publish readiness or suspend movement.
        // 仅在已有 Preview 刷新时调用；不增加每帧查询，不改变 domain 放置结果。
        private bool CanPreviewCandidate(Func<IReadOnlyList<NavigationSolidPose>> candidate)
        {
            if(adapter==null || adapter.World==null || !adapter.CheckRepresentationRoots(out _)) return false;
            try { return CheckActorOverlap(candidate(),out _); }
            catch(InvalidOperationException)
            {
                // Missing preview geometry renders invalid; Confirm keeps its existing validation.
                // 预览几何不可用时显示无效，Confirm 的验证行为保持不变。
                return false;
            }
        }
        public bool CanPreviewFurniture(FurniturePlacementPreview preview) => CanPreviewCandidate(()=>adapter.FurnitureCandidate(preview));
        public bool CanPreviewMounted(FunctionalSurfacePlacementPreview preview) => CanPreviewCandidate(()=>adapter.MountedCandidate(preview));
        public bool CanPreviewWall(WallMountedPlacementPreview preview) => CanPreviewCandidate(()=>adapter.WallCandidate(preview));
        public void ConfirmedLayoutChanged() { if(adapter!=null) adapter.RefreshConfirmedLayout(); }
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using UnityEngine;

namespace AnimalCafe.CafeLoop
{
    public sealed class CafeLoopValidationController:MonoBehaviour
    {
        [SerializeField] private CafeLoopController owner;
        [SerializeField] private CustomerFlowController customers;
        [SerializeField] private EmployeeFlowController employees;
        [SerializeField] private GameTimeService time;
        [SerializeField] private NavigationLayoutAdapter adapter;
        [SerializeField] private DecorationModeController decoration;
        [SerializeField] private CafeLayoutRuntime runtime;
        [SerializeField] private FurnitureContentCatalog catalog;
        [SerializeField] private NavigationActor blockerPrefab;
        [SerializeField] private Transform departure;
        [SerializeField] private bool seedValidationLayout,sharedFixture;
        private readonly List<NavigationActor> blockers=new List<NavigationActor>();
        private IDisposable startupBlock;
        public string Feedback { get; private set; }
        public void Configure(CafeLoopController value) { owner=value; }
        private IEnumerator Start()
        {
            if(!seedValidationLayout) yield break;
            startupBlock=time.AcquireResumeBlock(this,"正在准备P14独立验证布局");
            yield return null;
            try
            {
                runtime.Initialize();
                if(runtime.FunctionalSurfaceLayout.MountedInstances.Count!=0 || runtime.FunctionalSurfaceLayout.PickUpPoints.Count!=0)
                    throw new InvalidOperationException("Fixture seed requires its own empty initial layout.");
                var initial=runtime.Layout.FurnitureInstances.Single();
                var slot=catalog.BuildSurfaceSlotCatalog(runtime.Layout.GridSettings).GetForSupport(initial.DefinitionId).First().SlotId;
                Require(runtime.Layout.MoveFurniture(initial.InstanceId,new GridPosition(1,sharedFixture?2:5)).Succeeded);
                var machine=sharedFixture?new GridPosition(6,3):new GridPosition(4,6); var pickup=new GridPosition(sharedFixture?7:6,6);
                Require(runtime.Layout.PlaceFurniture(FurnitureInstance.Restore("14000000000000000000000000000002",initial.DefinitionId,machine,FurnitureRotation.Degrees0)).Succeeded);
                Require(runtime.Layout.PlaceFurniture(FurnitureInstance.Restore("14000000000000000000000000000003",initial.DefinitionId,pickup,FurnitureRotation.Degrees0)).Succeeded);
                if(sharedFixture)
                    foreach(var pair in new[]{(id:"14000000000000000000000000000007",y:7),(id:"14000000000000000000000000000008",y:5)})
                        Require(runtime.Layout.PlaceFurniture(FurnitureInstance.Restore(pair.id,initial.DefinitionId,new GridPosition(7,pair.y),FurnitureRotation.Degrees0)).Succeeded);
                Require(runtime.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance("14000000000000000000000000000004","equipment.cash-register.01",new SurfaceSlotAddress(initial.InstanceId,slot),sharedFixture?FurnitureRotation.Degrees90:FurnitureRotation.Degrees0)).Succeeded);
                Require(runtime.FunctionalSurfaceLayout.PlaceMounted(new SurfaceMountedInstance("14000000000000000000000000000005","equipment.coffee-machine.01",new SurfaceSlotAddress("14000000000000000000000000000002",slot),FurnitureRotation.Degrees0)).Succeeded);
                Require(runtime.FunctionalSurfaceLayout.PlacePickUp(new PickUpPointInstance("14000000000000000000000000000006",new SurfaceSlotAddress("14000000000000000000000000000003",slot))).Succeeded);
                adapter.RequireBusinessReadiness(); var bridge=adapter.GetComponent<NavigationDecorationBridge>();
                bridge.Configure(adapter); bridge.ConfigureTime(time); decoration.RefreshConfirmedNavigationPresentation();
                bridge.ResumeAfterRepair(); PublishFeedback("P14 fixture validated: "+(sharedFixture?"shared":"separate"));
            }
            finally { startupBlock?.Dispose(); startupBlock=null; }
            for(var frame=0;frame<240 && time.IsResumeBlocked;frame++) yield return null;
            if(!time.IsResumeBlocked && time.LastExplicitSpeed!=GameSpeed.Paused) time.TrySetAutomaticSpeed(GameSpeed.Normal);
        }
        private static void Require(bool value) { if(!value) throw new InvalidOperationException("P14 formal fixture seed rejected."); }
        // Context menu actions need a visible result in the ordinary Console / 菜单结果在Console可见。
        private void PublishFeedback(string value) { Feedback=value; Debug.Log("[P14 tool] "+value,this); }
        private bool PlayOnly()
        { if(Application.isPlaying) return true; PublishFeedback("验证mutation仅在Play可用 / Enter Play first"); return false; }
        [ContextMenu("P14/Resume Blocked Flow")] public void ResumeBlockedFlow()
        {
            if(!PlayOnly()) return;
            if(owner==null) { PublishFeedback("P14 owner unavailable"); return; }
            owner.ResumeBlockedFlow(); PublishFeedback("Blocked flow resume requested; inspect the current snapshot for remaining gates");
        }
        [ContextMenu("P14/Hold Arrivals")] public void HoldArrivals()
        {
            if(!PlayOnly()) return;
            if(customers==null) { PublishFeedback("Customer flow unavailable"); return; }
            customers.AutoSpawn=false; PublishFeedback("New arrivals held");
        }
        [ContextMenu("P14/Resume Arrivals")] public void ResumeArrivals()
        {
            if(!PlayOnly()) return;
            if(customers==null) { PublishFeedback("Customer flow unavailable"); return; }
            customers.AutoSpawn=true; PublishFeedback("New arrivals resumed");
        }
        [ContextMenu("P14/Hold Collection")] public void HoldCollection()
        {
            if(!PlayOnly()) return;
            if(owner==null) { PublishFeedback("P14 owner unavailable"); return; }
            owner.HoldCollection=true; PublishFeedback("Collection held");
        }
        [ContextMenu("P14/Resume Collection")] public void ResumeCollection()
        {
            if(!PlayOnly()) return;
            if(owner==null) { PublishFeedback("P14 owner unavailable"); return; }
            owner.HoldCollection=false; PublishFeedback("Collection resumed");
        }
        [ContextMenu("P14/Print Snapshot")] public void PrintSnapshot()
        {
            if(owner==null) { Debug.Log("P14 owner unavailable",this); return; }
            var value=owner.ViewSnapshot; var business=value.Business;
            var snapshot="P14 "+business.SessionId+" epoch="+business.Epoch+"\n"+CafeLoopStatusView.Format(value)+"\n"+
                string.Join("\n",business.Orders.Select(p=>p.Key.SessionId+"/"+p.Key.Epoch+"/"+p.Key.OrderId+" owner="+p.Value.CustomerId+" "+p.Value.State))+"\n"+
                string.Join("\n",value.Counter.Select((q,i)=>"Counter #"+(i+1)+" "+q.VisitId+" "+q.Position+" arrived="+q.Arrived))+"\n"+
                string.Join("\n",value.Pickup.Select((q,i)=>"Pickup #"+(i+1)+" "+q.VisitId+" "+q.Position+" arrived="+q.Arrived))+"\n"+
                string.Join("\n",value.Employees.Select(e=>e.ActorId+" "+e.Role+" "+e.State+" handle="+e.HandleId+" remaining="+e.RemainingWorkSeconds+" failure="+e.Failure))+"\n"+
                string.Join("\n",business.Employees.Select(w=>"CHECKPOINT "+w.Role+" visit="+w.VisitId+" order="+(w.Order.HasValue?Key(w.Order.Value):"—")+" stage="+w.Stage+" remaining="+w.RemainingWorkSeconds))+"\n"+
                string.Join("\n",business.Delivery.Select(d=>"DELIVERY "+Key(d.Key)+" visit="+d.VisitId+" address="+d.Address.SupportFurnitureInstanceId+"/"+d.Address.SlotId+" sequence="+d.DeliverySequence));
            Debug.Log(snapshot,this);
        }
        [ContextMenu("P14/Block Current Delivery Route")] public void BlockCurrentDeliveryRoute() => BlockEmployee(EmployeeState.CarryingToPickUp);
        [ContextMenu("P14/Block Current Return Route")] public void BlockCurrentReturnRoute() => BlockEmployee(EmployeeState.Returning);
        private void BlockEmployee(EmployeeState expected)
        {
            if(!Application.isPlaying || employees==null || adapter==null || !employees.TryGetBinding(EmployeeRole.Barista,out var binding))
            { PublishFeedback("No current Barista route"); return; }
            var employee=employees.Snapshot.FirstOrDefault(e=>e.Role==EmployeeRole.Barista);
            if(employee.State!=expected) { PublishFeedback("Barista is not on the selected route"); return; }
            if(expected==EmployeeState.CarryingToPickUp && employees.TryGetDeliveryTarget(out var actual)) BlockAt(actual);
            else BlockAt(expected==EmployeeState.CarryingToPickUp?binding.PickUpTarget.Value:binding.WorkTarget);
        }
        [ContextMenu("P14/Block Current Exit Route")] public void BlockCurrentExitRoute()
        {
            if(!PlayOnly()) return;
            if(owner==null || departure==null || !owner.Snapshot.CollectedExitVisitIds.Any(id=>owner.Snapshot.Visits.Any(v=>v.VisitId==id && !v.ExitArrived)))
            { PublishFeedback("No collected customer exit route"); return; }
            BlockAt(new NavigationTarget(departure.position));
        }
        private void BlockAt(NavigationTarget target)
        {
            const string rejected="Unsafe or unavailable blocker placement; no actor was moved.";
            if(!Application.isPlaying || adapter?.World==null || blockerPrefab==null) { PublishFeedback(rejected); return; }
            var actor=Instantiate(blockerPrefab,target.Position,target.Facing.HasValue?Quaternion.LookRotation(target.Facing.Value):Quaternion.identity,transform);
            if(!actor.TryInitializeRuntimeId("p14-blocker-"+Guid.NewGuid().ToString("N")) || !actor.TryEnableSteadyPathMotion() || !adapter.World.Register(actor))
            { actor.gameObject.SetActive(false); Destroy(actor.gameObject); PublishFeedback(rejected); return; }
            blockers.Add(actor); PublishFeedback("Registered owned stationary blocker "+actor.ActorId);
        }
        [ContextMenu("P14/Remove Test Blockers")] public void RemoveTestBlockers()
        { if(PlayOnly()) { RemoveOwnedBlockers(); PublishFeedback("Owned test blockers removed"); } }
        private void RemoveOwnedBlockers()
        {
            foreach(var actor in blockers)
            { if(actor==null) continue; adapter?.World?.Unregister(actor); actor.gameObject.SetActive(false); Destroy(actor.gameObject); }
            blockers.Clear();
        }
        [ContextMenu("P14/Replay Last Business Callback")] public void ReplayLastBusinessCallback()
        {
            if(!PlayOnly()) return;
            if(owner==null || owner.RetiredTaskEvents.Count==0) { PublishFeedback("No known retired callback"); return; }
            var oldEpoch=owner.RetiredTaskEvents.Where(e=>e.Token.Epoch<owner.Snapshot.Epoch).ToArray();
            var value=oldEpoch.Length>0?oldEpoch.Last():owner.RetiredTaskEvents.Last(); owner.TryReplayRetiredTask(value,out var reason);
            PublishFeedback(reason+" source="+(oldEpoch.Length>0?"old-epoch":"retired-attempt/task")+" token="+value.Token.SessionId+"/"+value.Token.Epoch+"/"+value.Token.Sequence+"/"+value.Token.Attempt+
                " actor="+value.ActorId+" handle="+value.HandleId+" request="+value.RequestId+" generation="+value.Generation+
                " revision="+value.LayoutRevision+" kind="+value.Kind);
        }
        private static string Key(CafeOrderKey key) => key.SessionId+"/"+key.Epoch+"/"+key.OrderId;
        private void OnDestroy() { startupBlock?.Dispose(); startupBlock=null; RemoveOwnedBlockers(); }
    }
}

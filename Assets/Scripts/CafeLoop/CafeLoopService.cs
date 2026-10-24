using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using AnimalCafe.Orders;
using AnimalCafe.Customers;

namespace AnimalCafe.CafeLoop
{
    // 单线程经营事实；port只在业务提交之前查询、之后发副作用。
    // Order + local fact commits never invoke Scene ports.
    public sealed class CafeLoopService : IDisposable
    {
        private sealed class Work
        {
            internal CafeTaskToken Token;
            internal CafeWorkCheckpoint Checkpoint;
            internal string ActorId;
            internal long Handle;
            internal int Generation,LayoutRevision=-1;
            internal bool Starting,Accepted,Blocked,Observed,SubmissionPending;
            internal CafeTaskEvent? Arrival;
        }
        private readonly string sessionId;
        private readonly ICafeLoopEnvironment environment;
        private OrderService orders=new OrderService();
        private CafeDeliveryStore deliveries;
        private readonly Dictionary<string,CafeOrderKey> paid=new Dictionary<string,CafeOrderKey>(StringComparer.Ordinal);
        private readonly List<string> paidFifo=new List<string>();
        private readonly HashSet<string> collected=new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> pickupMoves=new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> exitMoves=new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> blockedCollections=new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<CafeTaskEvent> events=new Queue<CafeTaskEvent>();
        private IReadOnlyList<CafeVisitSnapshot> visits=Array.Empty<CafeVisitSnapshot>();
        private Work cashier,barista;
        private long epoch=1,sequence,deliverySequence,completedVersion,projectionVersion;
        private bool disposed,ticking,permit,projecting,projectionDirty,resuming;
        private string failure="",visualFailure="";
        // Narrow private operations default to real P9; rejection tests do not expose SetState.
        private Func<OrderService,string,string,OrderResult> createOrder=(service,visit,product)=>service.Create(visit,product);
        private Func<OrderService,long,string,OrderResult> completeOrder=(service,order,visit)=>service.Complete(order,visit);

        public CafeLoopService(string sessionId,ICafeLoopEnvironment environment)
        {
            CafeContractRules.Identity(sessionId,nameof(sessionId));
            this.sessionId=sessionId; this.environment=environment ?? throw new ArgumentNullException(nameof(environment));
            deliveries=new CafeDeliveryStore(sessionId,epoch);
        }
        public CafeLoopSnapshot Snapshot => new CafeLoopSnapshot(sessionId,epoch,
            orders.GetOrders().ToDictionary(o=>new CafeOrderKey(sessionId,epoch,o.OrderId),o=>o),
            paidFifo.ToArray(),collected.OrderBy(id=>id,StringComparer.Ordinal).ToArray(),deliveries.Snapshot,visits,
            new[]{cashier,barista}.Where(w=>w!=null).Select(w=>w.Checkpoint).ToArray(),
            disposed?"Disposed":HasBlocked?"Blocked":projectionDirty?"视觉待刷新":"营业中",
            string.IsNullOrEmpty(failure)?visualFailure:failure);
        private bool HasBlocked => cashier?.Blocked==true || barista?.Blocked==true ||
            pickupMoves.Count>0 || exitMoves.Count>0 || blockedCollections.Count>0;
        private Work Current(EmployeeRole role) => role==EmployeeRole.Cashier?cashier:barista;
        private bool Live(Work work) => !disposed && work!=null && work.Token.Epoch==epoch && ReferenceEquals(Current(work.Checkpoint.Role),work);
        private bool Gate(long expectedEpoch)
        {
            if(disposed || epoch!=expectedEpoch) return false;
            var open=environment.CanProgress;
            return open && !disposed && epoch==expectedEpoch; // Query may reenter Done.
        }
        private bool CanCommit(long expectedEpoch) => permit && Gate(expectedEpoch);
        // Pure fact query for the finite runner -> next service Tick handoff only.
        internal bool HasQueuedCashierCompletion(string visitId) => Live(cashier) && !cashier.Blocked &&
            cashier.Checkpoint.VisitId==visitId && events.Any(e=>e.Role==EmployeeRole.Cashier &&
                e.Kind==CafeTaskEventKind.ServiceFinished && e.Checkpoint.Stage==CafeWorkStage.Finished && Matches(cashier,e));

        public void Tick(float scaledDeltaTime)
        {
            if(float.IsNaN(scaledDeltaTime) || float.IsInfinity(scaledDeltaTime) || scaledDeltaTime<0)
                throw new ArgumentOutOfRangeException(nameof(scaledDeltaTime));
            if(disposed || ticking) return;
            ticking=true;
            try
            {
                var tickEpoch=epoch;
                visits=(environment.Visits ?? Array.Empty<CafeVisitSnapshot>()).ToArray();
                // Exit is actual lifecycle evidence, valid even while paused; absence is not exit.
                foreach(var visit in visits.Where(v=>v.ExitArrived))
                { collected.Remove(visit.VisitId); exitMoves.Remove(visit.VisitId); }
                permit=scaledDeltaTime>0 && Gate(tickEpoch);
                if(!permit) return;
                StartAcceptedPreparation(barista);
                var submissionChecked=false;
                var count=events.Count;
                for(var i=0;i<count && CanCommit(tickEpoch);i++)
                {
                    var value=events.Dequeue(); var work=Current(value.Role);
                    if(!Matches(work,value) || work.Blocked) continue;
                    if(value.Kind==CafeTaskEventKind.ServiceFinished)
                    {
                        work.SubmissionPending=true;
                        if(!submissionChecked) { submissionChecked=true; CreatePaidOrder(work); }
                    }
                    else if(value.Kind==CafeTaskEventKind.Returned && BusinessDelivered(work)) barista=null;
                }
                if(!CanCommit(tickEpoch)) return;
                if(!submissionChecked && cashier?.SubmissionPending==true && !cashier.Blocked) CreatePaidOrder(cashier);
                if(!CanCommit(tickEpoch)) return;
                if(cashier==null) ScheduleCashier(tickEpoch);
                if(CanCommit(tickEpoch) && barista==null) ScheduleBarista(tickEpoch);
                if(!CanCommit(tickEpoch)) return;
                var head=CollectionHead();
                if(head!=null && !blockedCollections.Contains(head)) TryCollect(head,out _);
                if(projectionDirty && !disposed) Project();
            }
            finally { ticking=false; }
        }
        private void ScheduleCashier(long expectedEpoch)
        {
            if(!environment.IsEmployeeIdle(EmployeeRole.Cashier) || !CanCommit(expectedEpoch)) return;
            var visit=visits.FirstOrDefault(v=>v.IsEnabled && v.CounterArrived && !v.ExitArrived &&
                v.NavigationState==CustomerVisitState.Queued && !paid.ContainsKey(v.VisitId) && !collected.Contains(v.VisitId));
            if(string.IsNullOrWhiteSpace(visit.VisitId)) return;
            if(!environment.TryReservePickup(visit.VisitId,out _) || !CanCommit(expectedEpoch)) return;
            var actor=environment.EmployeeId(EmployeeRole.Cashier);
            if(!CanCommit(expectedEpoch) || string.IsNullOrWhiteSpace(actor)) return;
            cashier=NewWork(actor,new CafeWorkCheckpoint(EmployeeRole.Cashier,visit.VisitId,null,CafeWorkStage.Working,1));
            Dispatch(cashier);
        }
        private void ScheduleBarista(long expectedEpoch)
        {
            if(!environment.IsEmployeeIdle(EmployeeRole.Barista) || !CanCommit(expectedEpoch)) return;
            var actor=environment.EmployeeId(EmployeeRole.Barista);
            if(!CanCommit(expectedEpoch) || string.IsNullOrWhiteSpace(actor) || orders.GetWaitingOrderIds().Count==0) return;
            // Claimed stays claimed if task dispatch rejects; do not claim another order.
            var result=orders.ClaimNext(actor);
            if(!result.Succeeded) { failure="Claim拒绝："+result.FailureReason; return; }
            var key=new CafeOrderKey(sessionId,epoch,result.Order.OrderId);
            barista=NewWork(actor,new CafeWorkCheckpoint(EmployeeRole.Barista,result.Order.CustomerId,key,CafeWorkStage.Working,3));
            Dispatch(barista);
        }
        private Work NewWork(string actor,CafeWorkCheckpoint checkpoint)
        {
            if(sequence==long.MaxValue) throw new InvalidOperationException("Task sequence exhausted");
            return new Work{ActorId=actor,Checkpoint=checkpoint,Token=new CafeTaskToken(sessionId,epoch,++sequence,1)};
        }
        private void Dispatch(Work work)
        {
            if(!Live(work)) return;
            var startEpoch=epoch; var startToken=work.Token; work.Starting=true;
            bool accepted; string reason;
            try { accepted=environment.TryStartWork(work.Token,work.Checkpoint,out reason); }
            catch(Exception error) { accepted=false; reason=error.Message; }
            if(!Live(work) || epoch!=startEpoch || !work.Token.Equals(startToken)) return;
            work.Starting=false; work.Accepted=accepted || work.Observed;
            if(!accepted)
            {
                if(!work.Observed) Block(work,"任务派发拒绝："+reason);
                return; // Never overwrite a synchronous callback's own outcome.
            }
            StartAcceptedPreparation(work);
        }
        private void StartAcceptedPreparation(Work work)
        {
            if(!Live(work) || !work.Accepted || work.Blocked || !CanCommit(work.Token.Epoch)) return;
            if(work.Checkpoint.Role==EmployeeRole.Barista && work.Checkpoint.Order.HasValue &&
                orders.TryGetOrder(work.Checkpoint.Order.Value.OrderId,out var order) && order.State==OrderState.Claimed)
            {
                var result=orders.StartPreparation(order.OrderId,work.ActorId);
                if(!result.Succeeded) Block(work,"制作开始拒绝："+result.FailureReason);
            }
        }
        public void ReportTask(CafeTaskEvent value)
        {
            if(disposed || !Enum.IsDefined(typeof(EmployeeRole),value.Role)) return;
            var work=Current(value.Role);
            if(!Matches(work,value) || (!work.Starting && !work.Accepted) || value.HandleId<=0 ||
                value.Generation<=0 || value.LayoutRevision<0 || value.Checkpoint.Role!=value.Role ||
                value.Checkpoint.VisitId!=work.Checkpoint.VisitId || !Nullable.Equals(value.Checkpoint.Order,work.Checkpoint.Order) ||
                !Enum.IsDefined(typeof(CafeTaskEventKind),value.Kind) || !Enum.IsDefined(typeof(CafeWorkStage),value.Checkpoint.Stage)) return;
            if(work.Handle!=0 && work.Handle!=value.HandleId || value.Generation<work.Generation) return;
            if(work.LayoutRevision>=0 && value.LayoutRevision!=work.LayoutRevision &&
                !(value.Kind==CafeTaskEventKind.Blocked && value.LayoutRevision>work.LayoutRevision)) return;
            var saved=value.Checkpoint;
            if(saved.Role==EmployeeRole.Cashier && saved.Stage!=CafeWorkStage.Working && saved.Stage!=CafeWorkStage.Finished) return;
            var delivered=BusinessDelivered(work);
            if(saved.Role==EmployeeRole.Barista && !delivered && saved.Stage>=CafeWorkStage.Delivered) return;
            if(saved.Stage==CafeWorkStage.Working && (saved.RemainingWorkSeconds>work.Checkpoint.RemainingWorkSeconds ||
                work.Checkpoint.Stage!=CafeWorkStage.Working)) return;
            if(saved.Role==EmployeeRole.Barista && delivered)
            {
                // Returning/Finished是已保存的实际进度，旧Prepared不能使恢复倒退。
                // Keep the highest actual stage; Task4 refuses a regressed checkpoint.
                var stage=(CafeWorkStage)Math.Max((int)CafeWorkStage.Delivered,
                    Math.Max((int)work.Checkpoint.Stage,(int)saved.Stage));
                saved=new CafeWorkCheckpoint(saved.Role,saved.VisitId,saved.Order,stage,0);
            }
            work.Handle=value.HandleId; work.Generation=value.Generation; work.LayoutRevision=value.LayoutRevision; work.Observed=true;
            // Only the runner counts time; Progress/Blocked may save while paused.
            work.Checkpoint=saved;
            if(value.Kind==CafeTaskEventKind.Blocked) { Block(work,value.Failure ?? "员工受阻"); return; }
            if(value.Kind==CafeTaskEventKind.DeliveryArrived && value.RequestId>0 && !delivered && value.Role==EmployeeRole.Barista &&
                value.Checkpoint.Stage==CafeWorkStage.Prepared) work.Arrival=value;
            else if(value.Kind==CafeTaskEventKind.ServiceFinished && value.Role==EmployeeRole.Cashier && saved.Stage==CafeWorkStage.Finished ||
                value.Kind==CafeTaskEventKind.Returned && value.Role==EmployeeRole.Barista && saved.Stage==CafeWorkStage.Finished && delivered)
                events.Enqueue(value);
        }
        private bool Matches(Work work,CafeTaskEvent value) => Live(work) && work.Token.Equals(value.Token) &&
            value.Token.SessionId==sessionId && value.ActorId==work.ActorId && value.Role==work.Checkpoint.Role;
        private bool BusinessDelivered(Work work) => work?.Checkpoint.Order is CafeOrderKey key && key.Epoch==epoch &&
            orders.TryGetOrder(key.OrderId,out var order) && (order.State==OrderState.ReadyForPickup || order.State==OrderState.Completed);
        private void Block(Work work,string reason) { if(!Live(work)) return; work.Blocked=true; failure=reason; }
        private void CreatePaidOrder(Work work)
        {
            var commitEpoch=epoch; var id=work.Checkpoint.VisitId;
            if(!Live(work) || work.Blocked || !work.SubmissionPending || paid.ContainsKey(id) || !CanCommit(commitEpoch)) return;
            var visit=visits.FirstOrDefault(v=>v.VisitId==id);
            if(!visit.IsEnabled || !visit.CounterArrived || visit.ExitArrived || visit.NavigationState!=CustomerVisitState.Queued) return;
            // Finished service can wait before Create for a temporary physical gate. A real
            // Create rejection remains Blocked; no new task, fee or timer is issued here.
            if(!environment.TryReservePickup(id,out _)) return;
            if(!Live(work) || !CanCommit(commitEpoch)) return;
            var result=createOrder(orders,id,"coffee");
            if(!result.Succeeded) { Block(work,"Create拒绝："+result.FailureReason); return; }
            var key=new CafeOrderKey(sessionId,epoch,result.Order.OrderId);
            paid.Add(id,key); paidFifo.Add(id); cashier=null; // Publish before the movement port.
            pickupMoves.Add(id); MovePickup(id,commitEpoch);
        }
        private void MovePickup(string id,long expectedEpoch)
        {
            if(!Gate(expectedEpoch) || !paid.ContainsKey(id)) return;
            var moved=environment.TryMoveToPickup(id,out var reason);
            if(disposed || epoch!=expectedEpoch) return;
            if(moved) pickupMoves.Remove(id); else failure="顾客转移受阻："+reason;
        }
        private void MoveExit(string id,long expectedEpoch)
        {
            if(!Gate(expectedEpoch) || !collected.Contains(id)) return;
            var moved=environment.TryMoveToExit(id,true,out var reason);
            if(disposed || epoch!=expectedEpoch) return;
            if(moved) exitMoves.Remove(id); else failure="离场受阻："+reason;
        }
        public bool TryCommitDelivery(CafeTaskEvent arrival,SurfaceSlotAddress address,out string reason)
        {
            reason="当前实际交付身份/进度无效"; var work=barista; var commitEpoch=epoch;
            if(!ValidDelivery(work,arrival) || !CanCommit(commitEpoch) || deliverySequence==long.MaxValue) return false;
            var value=new CafeDeliverySnapshot(work.Checkpoint.Order.Value,work.Checkpoint.VisitId,address,deliverySequence+1);
            if(!deliveries.CanCommit(value)) { reason="交付key/epoch/slot/sequence无效"; return false; }
            if(!environment.IsCurrentDeliveryArrival(arrival,address)) return false;
            if(!ReferenceEquals(barista,work) || !CanCommit(commitEpoch) || !ValidDelivery(work,arrival) || !deliveries.CanCommit(value)) return false;
            // No port/event/visual call in this irreversible P9 + local commit.
            var ready=orders.MarkReadyForPickup(value.Key.OrderId,work.ActorId);
            if(!ready.Succeeded) { reason="Ready拒绝："+ready.FailureReason; Block(work,reason); return false; }
            if(!deliveries.CommitDelivery(value)) throw new InvalidOperationException("Validated delivery commit failed");
            deliverySequence++; work.Arrival=null;
            work.Checkpoint=new CafeWorkCheckpoint(EmployeeRole.Barista,value.VisitId,value.Key,CafeWorkStage.Delivered,0);
            reason=""; RequestProjection();
            return true; // Reentrant Done/disable cannot undo an already committed Ready.
        }
        private bool ValidDelivery(Work work,CafeTaskEvent arrival) => Matches(work,arrival) && !work.Blocked &&
            work.Arrival.HasValue && work.Arrival.Value.Equals(arrival) && work.Handle==arrival.HandleId &&
            work.Generation==arrival.Generation && work.LayoutRevision==arrival.LayoutRevision && arrival.RequestId>0 &&
            arrival.Kind==CafeTaskEventKind.DeliveryArrived && work.Checkpoint.Order.HasValue &&
            paid.TryGetValue(work.Checkpoint.VisitId,out var key) && key.Equals(work.Checkpoint.Order.Value) &&
            orders.TryGetOrder(key.OrderId,out var order) && order.CustomerId==work.Checkpoint.VisitId &&
            order.ClaimantId==work.ActorId && order.State==OrderState.Preparing;
        private string CollectionHead() => paidFifo.FirstOrDefault(id=>paid.TryGetValue(id,out var key) &&
            orders.TryGetOrder(key.OrderId,out var order) && order.State!=OrderState.Completed);
        public bool TryCollect(string visitId,out string reason)
        {
            reason="不是已Ready的正确队首或实际服务点尚未到达"; var commitEpoch=epoch;
            if(string.IsNullOrWhiteSpace(visitId) || !CanCommit(commitEpoch) || blockedCollections.Contains(visitId) ||
                CollectionHead()!=visitId || !paid.TryGetValue(visitId,out var key) || !deliveries.Contains(key) ||
                !orders.TryGetOrder(key.OrderId,out var order) || order.CustomerId!=visitId || order.State!=OrderState.ReadyForPickup) return false;
            var actual=(environment.Visits ?? Array.Empty<CafeVisitSnapshot>()).FirstOrDefault(v=>v.VisitId==visitId);
            if(!actual.IsEnabled || !actual.PickupArrived || actual.ExitArrived || !environment.CanCollect(visitId,key)) return false;
            if(!CanCommit(commitEpoch) || CollectionHead()!=visitId || !deliveries.Contains(key) ||
                !orders.TryGetOrder(key.OrderId,out order) || order.State!=OrderState.ReadyForPickup) return false;
            var completed=completeOrder(orders,key.OrderId,visitId);
            if(!completed.Succeeded)
            { blockedCollections.Add(visitId); failure=reason="Complete拒绝："+completed.FailureReason; return false; }
            deliveries.CommitCollection(key); collected.Add(visitId); exitMoves.Add(visitId);
            reason=""; RequestProjection();
            if(epoch==commitEpoch && !disposed) MoveExit(visitId,commitEpoch);
            return true;
        }
        private void RequestProjection() { projectionDirty=true; projectionVersion++; Project(); }
        private void Project()
        {
            if(projecting) return;
            projecting=true; var version=projectionVersion;
            try
            {
                environment.ProjectDeliveries(deliveries.Snapshot);
                if(version==projectionVersion) { projectionDirty=false; visualFailure=""; }
            }
            catch(Exception error) { projectionDirty=true; visualFailure="renderer / "+error.Message; }
            finally { projecting=false; }
        }
        public void ClearOrdersForDecoration(long version)
        {
            if(version<=0) throw new ArgumentOutOfRangeException(nameof(version));
            if(disposed || version<=completedVersion) return;
            if(epoch==long.MaxValue) throw new InvalidOperationException("Order epoch exhausted");
            // Retire identities before external projection; collected exits survive.
            completedVersion=version; epoch++; permit=false; cashier=barista=null; events.Clear();
            orders=new OrderService(); deliveries=new CafeDeliveryStore(sessionId,epoch); paid.Clear(); paidFifo.Clear();
            pickupMoves.Clear(); exitMoves.Clear(); blockedCollections.Clear(); failure="";
            RequestProjection();
        }
        public void ResumeBlocked()
        {
            if(disposed || resuming) return;
            resuming=true;
            try
            {
                var resumeEpoch=epoch;
                if(!Gate(resumeEpoch)) return;
                failure=""; blockedCollections.Clear();
                foreach(var work in new[]{cashier,barista})
                {
                    if(!Live(work) || !work.Blocked) continue;
                    if(work.Token.Attempt==int.MaxValue) { Block(work,"Task attempt exhausted"); continue; }
                    var checkpoint=work.Checkpoint;
                    if(BusinessDelivered(work) && checkpoint.Stage<CafeWorkStage.Delivered)
                        checkpoint=new CafeWorkCheckpoint(checkpoint.Role,checkpoint.VisitId,checkpoint.Order,CafeWorkStage.Delivered,0);
                    // 每次attempt使用新上下文；旧Dispatch返回不能改写被拒的新attempt。
                    // Fresh reference retires outer synchronous callbacks before another port call.
                    var restored=new Work{ActorId=work.ActorId,Checkpoint=checkpoint,
                        Token=new CafeTaskToken(sessionId,epoch,work.Token.Sequence,work.Token.Attempt+1)};
                    if(checkpoint.Role==EmployeeRole.Cashier) cashier=restored; else barista=restored;
                    Dispatch(restored);
                    if(!Gate(resumeEpoch)) return;
                }
                foreach(var id in pickupMoves.ToArray()) { MovePickup(id,resumeEpoch); if(!Gate(resumeEpoch)) return; }
                foreach(var id in exitMoves.ToArray()) { MoveExit(id,resumeEpoch); if(!Gate(resumeEpoch)) return; }
            }
            finally { resuming=false; }
        }
        public void Dispose()
        {
            if(disposed) return;
            disposed=true; permit=false; cashier=barista=null; events.Clear(); deliveries.Clear();
            orders=new OrderService(); paid.Clear(); paidFifo.Clear(); collected.Clear(); pickupMoves.Clear(); exitMoves.Clear();
            blockedCollections.Clear(); visits=Array.Empty<CafeVisitSnapshot>(); failure=""; RequestProjection();
        }
    }
}

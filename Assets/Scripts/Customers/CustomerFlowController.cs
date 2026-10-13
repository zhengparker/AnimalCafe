using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using AnimalCafe.Navigation;
using AnimalCafe.Core.Time;
using AnimalCafe.Capacity;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
namespace AnimalCafe.Customers
{
    public sealed class CustomerFlowController : MonoBehaviour
    {
        [SerializeField] private NavigationWorld world;
        [SerializeField] private NavigationLayoutAdapter adapter;
        [SerializeField] private GameTimeService time;
        [SerializeField] private NavigationActor[] prefabs;
        [SerializeField] private Transform spawn,exit,validationHead,gridRoot;
        [SerializeField] private CafeLayoutRuntime runtime;
        [SerializeField] private DecorationModeController decoration;
        [SerializeField] private bool autoSpawn=true;
        private sealed class Visit
        {
            public string Id; public NavigationActor Actor; public CustomerAdmission Admission;
            public CustomerVisitState State,ResumeState; public long RequestId;
            public int Generation; public QueueAssignment Assignment; public Vector3 CounterPosition,AdvanceStart;
            public bool QueueLeft;
            public NavigationTarget FinalTarget;
            public Queue<Vector3> Route;
            public int Segments,Replans;
            public string Failure;
        }
        private readonly Dictionary<string,Visit> visits=new Dictionary<string,Visit>();
        private CustomerSpawnClock clock;
        private CounterQueueService queue;
        private int revision=-1;
        private long decorationVersion;
        private bool suspended,disposed,recoveringQueue,admissionRejected,admissionRouteUnavailable;
        private string session;
        private long nextVisit;
        private Vector3 queueFacing;
        public bool AutoSpawn { get=>autoSpawn; set=>autoSpawn=value; }
        public CapacityService Capacity { get; private set; }
        public string Status { get; private set; }="等待营业布局";
        public string SelectedRegisterId { get; private set; }="";
        public float RemainingSeconds => clock?.RemainingSeconds ?? 0;
        public IReadOnlyList<CustomerVisitSnapshot> Snapshot => visits.Values.OrderBy(v=>v.Id,StringComparer.Ordinal)
            .Select(v=>new CustomerVisitSnapshot(v.Id,v.State,v.Actor==null?Vector3.zero:v.Actor.transform.position)).ToList().AsReadOnly();
        public void Configure(NavigationWorld value,NavigationLayoutAdapter layout,GameTimeService gameTime,
            NavigationActor[] characters,Transform entrance,Transform departure,Transform head,
            CafeLayoutRuntime business=null,Transform grid=null,DecorationModeController decor=null)
        {
            if(visits.Count>0) throw new InvalidOperationException("Cannot reconfigure an active customer session");
            world=value; adapter=layout; time=gameTime; prefabs=characters; spawn=entrance; exit=departure;
            validationHead=head; runtime=business; gridRoot=grid; decoration=decor;
            decorationVersion=decoration==null?0:decoration.CompletedLayoutChangeVersion;
            revision=-1; Capacity=null; queue=null; admissionRejected=admissionRouteUnavailable=false;
        }
        private void Awake()
        {
            session=Guid.NewGuid().ToString("N"); var random=new System.Random();
            clock=new CustomerSpawnClock(()=> (float)random.NextDouble());
        }
        private void Update() => Step(Time.deltaTime);
        private bool Configured => !disposed && world!=null && adapter!=null && time!=null && spawn!=null && exit!=null &&
            prefabs!=null && prefabs.Length>0 && prefabs.All(p=>p!=null);
        public void Step(float delta)
        {
            if(float.IsNaN(delta) || float.IsInfinity(delta) || delta<0) throw new ArgumentOutOfRangeException(nameof(delta));
            if(!disposed && decoration!=null && decorationVersion!=decoration.CompletedLayoutChangeVersion)
            {
                decorationVersion=decoration.CompletedLayoutChangeVersion;
                ResetCustomersAfterDecoration();
            }
            if(!Configured || !world.isActiveAndEnabled || !adapter.isActiveAndEnabled)
            { Status="营业组件不可用"; HoldVisits(); clock?.Tick(0,false,false); return; }
            var decorating=decoration!=null && decoration.IsOpen;
            if(decorating) { HoldVisits(); clock.Tick(0,false,true); Status="装修中，暂停生成"; return; }
            if(runtime!=null && runtime.Layout==null) { Status="等待营业布局"; return; }
            if(runtime!=null && !adapter.EnforceBusinessReadiness)
            {
                if(runtime.CurrentReadiness==null || !runtime.CurrentReadiness.CanOpenForBusiness)
                { Status="请先布置可营业的服务布局"; clock.Tick(0,false,false); return; }
                adapter.RequireBusinessReadiness();
                var bridge=adapter.GetComponent<NavigationDecorationBridge>();
                if(bridge!=null) { bridge.Configure(adapter); bridge.ConfigureTime(time); }
            }
            if(Capacity==null) Capacity=new CapacityService(runtime==null?64:FloorCapacitySource.CountInteriorCells(runtime.Layout));
            if(!adapter.CurrentReadiness.CanResume || !world.LayoutAvailable)
            {
                HoldVisits(); clock.Tick(0,false,false);
                if(!adapter.RebuildAndValidate().CanResume || !world.LayoutAvailable)
                { if(adapter.EnforceBusinessReadiness) world.HoldUnavailableResumeBlock(time); Status="路径不可用，请修复布局"; return; }
                world.ReleaseValidatedResumeBlock();
            }
            if(revision!=adapter.Revision || queue==null || suspended)
            {
                // 即使adapter已同步rebuild，也必须退休上一revision的已终止请求。
                if(queue!=null) HoldVisits();
                if(runtime!=null) Capacity.UpdateFloorCellCount(FloorCapacitySource.CountInteriorCells(runtime.Layout));
                if(!BuildQueue()) { HoldVisits(); clock.Tick(0,false,false); return; }
                revision=adapter.Revision; suspended=false;
                ResumeBlockedVisits();
            }
            foreach(var visit in visits.Values.ToArray())
            {
                if(visit.Actor==null) { RemoveVisit(visit.Id); continue; }
                if(visit.ResumeState==CustomerVisitState.Exiting && !visit.QueueLeft &&
                    Vector3.Distance(visit.Actor.transform.position,visit.CounterPosition)>1.1f)
                { visit.QueueLeft=true; visit.Admission.LeaveQueue(); queue.Remove(visit.Id); }
            }
            var blocked=visits.Values.Any(v=>v.State==CustomerVisitState.Blocked);
            if((!blocked || recoveringQueue) && queue.TryGetNextAdvance(out var assignment,CanBeginAdvance) && visits.TryGetValue(assignment.VisitId,out var next))
            { next.Assignment=assignment; Send(next,CustomerVisitState.Advancing,QueueTarget(assignment)); }
            blocked=visits.Values.Any(v=>v.State==CustomerVisitState.Blocked); // 同步提交/恢复可能已经改变状态。
            // 满员等营业条件取消本轮；临时入口/队尾/路径门禁由TrySpawn保留到期机会。
            var canWait=!blocked && Capacity.CanAdmit && queue.Snapshot.Count<queue.AvailableSlotCount;
            if(autoSpawn && clock.Tick(delta,canWait,false) && TrySpawn()) clock.CompleteAdmission();
            else if(!autoSpawn) clock.Tick(0,false,false);
            var failed=visits.Values.FirstOrDefault(v=>v.State==CustomerVisitState.Blocked && !string.IsNullOrEmpty(v.Failure));
            if(failed!=null) Status="顾客路径受阻（"+failed.Failure+"），请修复布局后重试";
            else if(queue.AvailableSlotCount<queue.Snapshot.Count)
                Status="队伍可用站位不足（"+queue.AvailableSlotCount+"/"+queue.Snapshot.Count+"），请调整布局";
            else if(blocked) Status="队伍正在恢复，等待前方顾客";
            else if(!Capacity.CanAdmit) Status="容量已满，暂停生成";
            else if(!queue.CanJoin) Status="队伍空间不足或正在前移";
            else if(!EntryClear()) Status="入口暂被占用";
            else if(!TailClear()) Status="队尾站位暂被占用";
            else if(admissionRouteUnavailable) Status="入店通道暂不可用，等待通道腾空后再尝试";
            else if(admissionRejected) Status="本次入店失败，等待入店重试";
            else Status="营业中";
        }
        private bool BuildQueue()
        {
            Vector3 head,outward;
            var excluded=new List<Vector3>();
            if(runtime!=null)
            {
                if(gridRoot==null || runtime.CurrentReadiness==null) { Status="服务布局未初始化"; return false; }
                var cash=runtime.CurrentReadiness.Stations.Where(s=>s.IsValid && s.FunctionType==LayoutStationType.CashRegister)
                    .OrderBy(s=>s.InstanceId,StringComparer.Ordinal).FirstOrDefault();
                if(cash==null || !cash.Anchors.TryGetAnchor(InteractionRole.Customer,out var customer))
                { Status="没有可用收银台"; return false; }
                SelectedRegisterId=cash.InstanceId;
                head=WorldAnchor(customer); queueFacing=Direction(customer.Facing); outward=-queueFacing;
                foreach(var station in runtime.CurrentReadiness.Stations)
                    foreach(var anchor in station.Anchors.Anchors)
                        // 空Employee anchor可排队；实际角色仍由World碰撞和路线避让保护。
                        if(station.FunctionType==LayoutStationType.PickUpPoint && anchor.Role==InteractionRole.Customer)
                            excluded.Add(WorldAnchor(anchor));
            }
            else
            {
                if(validationHead==null) { Status="缺少验证队首"; return false; }
                head=validationHead.position; outward=validationHead.forward; queueFacing=-outward; SelectedRegisterId="validation.head";
            }
            // Pick-up顾客位置按身体与到站误差留空，不把队伍间距当作禁站半径。
            var pickupClearance=prefabs.Max(p=>2*p.Settings.AgentRadius+p.Settings.CollisionSkin+
                p.Settings.Epsilon+p.Settings.ArrivalDistance);
            var queueClearance=prefabs.Max(p=>2*p.Settings.AgentRadius+p.Settings.CollisionSkin+p.Settings.Epsilon);
            // 出生点按身体保护；等待目标另计到站误差 / physical spawn clearance plus arrival allowance.
            var entryClearance=queueClearance+prefabs.Max(p=>p.Settings.ArrivalDistance);
            var slots=new CounterQueuePlanner().Build(head,outward,Capacity.Limits.CounterQueue,(p,previous)=>
            {
                if(!adapter.IsPointWalkable(p) || (previous!=null && !adapter.HasClearPath(previous.Value,p)) ||
                    !adapter.HasClearPath(spawn.position,p) || excluded.Any(e=>Vector3.Distance(e,p)<pickupClearance) ||
                    InEntrance(p) || Vector3.Distance(exit.position,p)<1.2f) return false;
                return true;
            },(p,prefix)=>
                // 只验证当前分支；回退后不保留已放弃的虚拟占位。
                // 前方slots都有人时，后客仍须从入口绕行抵达。
                (prefix.Count==0 || Vector3.Distance(p,spawn.position)>=entryClearance) &&
                    new CustomerRoutePlanner().Build(spawn.position,p,prefix,queueClearance,adapter.GetCompletePath)!=null,
                (from,requested)=>
                {
                    var path=adapter.GetCompletePath(from,requested);
                    return path==null || path.Count==0?(Vector3?)null:path[path.Count-1];
                });
            if(queue==null) queue=new CounterQueueService(slots); else queue.UpdateSlots(slots);
            if(slots.Count==0) { Status="队伍没有可用站位"; return visits.Count>0; }
            return true;
        }
        private Vector3 WorldAnchor(InteractionAnchor a) => gridRoot.TransformPoint(new Vector3((a.Position.X+.5f)*runtime.Layout.GridSettings.CellSize,0,(a.Position.Y+.5f)*runtime.Layout.GridSettings.CellSize));
        private Vector3 Direction(CardinalDirection d) => gridRoot.TransformDirection(new[]{Vector3.forward,Vector3.right,Vector3.back,Vector3.left}[(int)d]);
        private bool InEntrance(Vector3 p)
        {
            if(runtime==null || gridRoot==null) return Vector3.Distance(p,spawn.position)<1.2f;
            var local=gridRoot.InverseTransformPoint(p); var size=runtime.Layout.GridSettings.CellSize;
            return runtime.Layout.Reservations.Any(r=>r.Type==LayoutReservationType.EntranceClearance &&
                local.x>=r.Origin.X*size-.46f && local.x<=(r.Origin.X+r.Size.Width)*size+.46f &&
                local.z>=r.Origin.Y*size-.46f && local.z<=(r.Origin.Y+r.Size.Height)*size+.46f);
        }
        private bool EntryClear()
        {
            if(!adapter.IsPointWalkable(spawn.position)) return false;
            var incomingRadius=prefabs.Max(p=>p.Settings.AgentRadius+p.Settings.CollisionSkin+p.Settings.Epsilon);
            return world.RegisteredActors.All(a=>a!=null &&
                Vector3.Distance(a.transform.position,spawn.position)>=a.Settings.AgentRadius+incomingRadius);
        }
        private bool TailClear()
        {
            if(!queue.NextAdmissionPosition.HasValue) return false;
            // 到站存在误差；用代理半径+碰撞余量+到站误差判断，不把slot间距当占用半径。
            var admissionRadius=prefabs.Max(p=>p.Settings.AgentRadius+p.Settings.CollisionSkin+
                p.Settings.Epsilon+p.Settings.ArrivalDistance);
            var position=queue.NextAdmissionPosition.Value;
            // 活动入店前客可预测腾位；TrySpawn仍须验证剩余路线，不移除实际身体。
            // A temporary entry occupant is provisional until the route preflight approves.
            return world.RegisteredActors.All(a=>a!=null &&
                (Vector3.Distance(a.transform.position,position)>=a.Settings.AgentRadius+admissionRadius ||
                 IsMovingEntryLeader(a) && Vector3.Distance(visits[a.ActorId].Assignment.Position,position)>=a.Settings.AgentRadius+admissionRadius));
        }
        public bool TrySpawn()
        {
            if(!isActiveAndEnabled || !Configured || !world.isActiveAndEnabled || !world.LayoutAvailable || queue==null ||
                (decoration!=null && decoration.IsOpen) || !queue.CanJoin || !Capacity.CanAdmit ||
                visits.Values.Any(v=>v.State==CustomerVisitState.Blocked) || !EntryClear() || !TailClear()) return false;
            // 在reserve/create前证明路线绕得开已有顾客，不发布一个已知会堵住的visit。
            admissionRouteUnavailable=!CanStartAdmission(queue.NextAdmissionPosition.Value,prefabs[(int)(nextVisit%prefabs.Length)].Settings);
            if(admissionRouteUnavailable) return false;
            var tail=queue.Snapshot.Count; var id=session+"-"+(++nextVisit).ToString("D8");
            admissionRejected=true; // 失败原因与到期机会保留到成功接纳。
            if(!CustomerAdmission.TryCreate(Capacity,id,out var lease)) return false;
            NavigationActor actor=null; Visit visit=null;
            try
            {
                actor=Instantiate(prefabs[(int)((nextVisit-1)%prefabs.Length)],spawn.position,spawn.rotation,transform);
                if(!actor.TryInitializeRuntimeId(id) || !actor.TryEnableSteadyPathMotion() || !world.Register(actor) || !queue.TryJoin(id)) return false;
                visit=new Visit{Id=id,Actor=actor,Admission=lease,Assignment=queue.Snapshot[tail]};
                visits.Add(id,visit);
                if(!lease.MarkInside()) { visits.Remove(id); queue.Remove(id); visit=null; return false; }
                admissionRejected=admissionRouteUnavailable=false;
                Send(visit,CustomerVisitState.Entering,QueueTarget(visit.Assignment));
                return true;
            }
            finally
            {
                if(visit==null)
                {
                    queue.Remove(id); if(actor!=null) { world.Unregister(actor); Destroy(actor.gameObject); }
                    lease.Dispose();
                }
            }
        }
        private NavigationTarget QueueTarget(QueueAssignment assignment)
        {
            var facing=queueFacing;
            if(assignment.SlotIndex>0)
            {
                // 排队朝向沿slot链指向前一位；队首保留收银台方向。
                var previous=queue.Snapshot.FirstOrDefault(a=>a.SlotIndex==assignment.SlotIndex-1);
                var towardPrevious=previous.Position-assignment.Position; towardPrevious.y=0;
                if(previous.VisitId!=null && towardPrevious.sqrMagnitude>.001f) facing=towardPrevious.normalized;
            }
            return new NavigationTarget(assignment.Position,facing);
        }
        private void Send(Visit visit,CustomerVisitState phase,NavigationTarget target)
        {
            if(phase==CustomerVisitState.Advancing) visit.AdvanceStart=visit.Actor.transform.position;
            visit.Generation++; visit.RequestId=0; visit.State=visit.ResumeState=phase;
            visit.FinalTarget=target; visit.Segments=visit.Replans=0; visit.Failure=null;
            var route=BuildRoute(visit.Actor.transform.position,target.Position,visit.Id,visit.Actor.Settings);
            if(route==null || route.Count>24) { BlockVisit(visit,phase,"没有安全通道"); return; }
            visit.Route=new Queue<Vector3>(route); SendRouteSegment(visit,phase);
        }
        private IReadOnlyList<Vector3> BuildRoute(Vector3 from,Vector3 to,string ignore,NavigationSettings settings,int? advancingSlot=null,bool prospectiveAdmission=false)
        {
            var occupied=RouteObstacles(ignore,advancingSlot,prospectiveAdmission);
            return new CustomerRoutePlanner().Build(from,to,occupied,RouteClearance(settings),adapter.GetCompletePath);
        }
        private Vector3[] RouteObstacles(string ignore,int? advancingSlot=null,bool prospectiveAdmission=false)
            => world.RegisteredActors.Where(a=>a!=null && a.ActorId!=ignore && !IsMovingQueueLeader(a,ignore,advancingSlot) &&
                    !(prospectiveAdmission && IsMovingEntryLeader(a))).Select(a=>a.transform.position)
                .Concat(visits.Values.Where(v=>v.Id!=ignore && ReservesRouteTarget(v,ignore,advancingSlot))
                    .Select(v=>v.Assignment.Position)).Distinct().ToArray();
        private bool IsMovingQueueLeader(NavigationActor actor,string followerId,int? advancingSlot)
        {
            // 同队前移或连续入店的活动前客可预测腾位，保留其目标slot。
            // 实际身体不移除：P11 World每一步照常检查全部角色并限制安全位移。
            if(followerId==null || !visits.TryGetValue(followerId,out var follower) || !visits.TryGetValue(actor.ActorId,out var leader) ||
                leader.RequestId==0 || !actor.isActiveAndEnabled || leader.Assignment.SlotIndex>=(advancingSlot??follower.Assignment.SlotIndex)) return false;
            return leader.State==CustomerVisitState.Advancing && (advancingSlot.HasValue || follower.State==CustomerVisitState.Advancing) ||
                follower.State==CustomerVisitState.Entering && leader.State==CustomerVisitState.Entering;
        }
        private bool IsMovingEntryLeader(NavigationActor actor)
            => actor.isActiveAndEnabled && visits.TryGetValue(actor.ActorId,out var leader) &&
                leader.State==CustomerVisitState.Entering && leader.RequestId!=0;
        private bool ReservesRouteTarget(Visit other,string requesterId,int? advancingSlot)
        {
            if(other.State!=CustomerVisitState.Entering && other.State!=CustomerVisitState.Advancing) return false;
            if(requesterId==null || !visits.TryGetValue(requesterId,out var requester)) return true;
            // 后客尚未到达的目标不挡前客/离场路线；实际身体始终保留。
            if(requester.State==CustomerVisitState.Exiting) return false;
            return !advancingSlot.HasValue && requester.State!=CustomerVisitState.Advancing && requester.State!=CustomerVisitState.Entering ||
                other.Assignment.SlotIndex<(advancingSlot??requester.Assignment.SlotIndex);
        }
        private bool CanStartAdmission(Vector3 target,NavigationSettings settings)
        {
            var route=BuildRoute(spawn.position,target,null,settings,prospectiveAdmission:true);
            if(route==null || route.Count>24) return false;
            var incoming=ExpandRoute(spawn.position,route);
            if(incoming==null) return false;
            var clearance=RouteClearance(settings)+2*settings.MaxSpeed*
                Mathf.Min(Time.deltaTime,settings.MaxSubstepSeconds*settings.MaxSubstepsPerFrame);
            // 创建/预留前让已有任务先通过；新尾位不能堵住前客的剩余路线。
            // Validate remaining owned paths, then keep P11's continuous body guards.
            foreach(var earlier in visits.Values.Where(v=>v.RequestId!=0 && v.Actor!=null && v.Actor.isActiveAndEnabled))
            {
                var targets=new[]{earlier.Actor.Agent.destination}.Concat(earlier.Route??Enumerable.Empty<Vector3>());
                var remaining=ExpandRoute(earlier.Actor.transform.position,targets);
                if(remaining==null) return false;
                var targetClear=CustomerRoutePlanner.IsClear(remaining,new[]{target},clearance);
                var follows=earlier.State==CustomerVisitState.Entering &&
                    CanFollowEntryBend(incoming,remaining,clearance,settings.ArrivalDistance+settings.Epsilon) &&
                    CustomerRoutePlanner.IsClear(new[]{remaining[remaining.Count-1]},new[]{target},clearance) &&
                    (targetClear || FollowerReachesTailAfterLeader(incoming,remaining,target,clearance,
                        settings.MaxSpeed,earlier.Actor.Settings.MaxSpeed));
                // 同向跟随按先后经过尾位；前客最终目标仍须留空，实际身体由World逐步保护。
                if(!targetClear &&
                    !follows && !EntryVacatesTail(earlier,remaining,target,clearance)) return false;
                if(RoutesConflict(incoming,remaining,clearance) && !follows) return false;
            }
            return true;
        }
        private static bool FollowerReachesTailAfterLeader(IReadOnlyList<Vector3> follower,IReadOnlyList<Vector3> leader,
            Vector3 tail,float clearance,float followerSpeed,float leaderSpeed)
        {
            var followerDistance=0f; var leadingDistance=0f; var lastOccupiedDistance=0f;
            for(var i=1;i<follower.Count;i++) followerDistance+=Vector3.Distance(follower[i-1],follower[i]);
            for(var i=1;i<leader.Count;i++)
            {
                var delta=leader[i]-leader[i-1]; delta.y=0; var length=delta.magnitude;
                if(length>0 && !CustomerRoutePlanner.IsClear(new[]{leader[i-1],leader[i]},new[]{tail},clearance))
                {
                    var offset=tail-leader[i-1]; offset.y=0;
                    var closest=Mathf.Clamp01(Vector3.Dot(offset,delta)/delta.sqrMagnitude)*length;
                    // 最近点再走一个clearance必已腾位；跨段时后续段继续验证。
                    lastOccupiedDistance=leadingDistance+Mathf.Min(length,closest+clearance);
                }
                leadingDistance+=length;
            }
            // 仅用于有序同向跟随；留一段身体间距的时间余量，实际运动仍检查所有角色。
            return leaderSpeed>0 && followerSpeed>0 &&
                lastOccupiedDistance/leaderSpeed+clearance/Mathf.Min(leaderSpeed,followerSpeed)<=followerDistance/followerSpeed;
        }
        private static bool EntryVacatesTail(Visit earlier,IReadOnlyList<Vector3> path,Vector3 tail,float clearance)
        {
            if(earlier.State!=CustomerVisitState.Entering || path.Count<2 ||
                CustomerRoutePlanner.IsClear(new[]{path[0]},new[]{tail},clearance)) return false;
            // 只允许前客从临时占位向外腾空，退出后不得再穿回新队尾。
            // Ignore only the vacating entry prefix, not a future crossing or parked body.
            for(var i=1;i<path.Count;i++)
            {
                var offset=path[i-1]-tail; var step=path[i]-path[i-1]; offset.y=step.y=0;
                if(Vector3.Dot(offset,step)<-.000001f) return false;
                var end=path[i]-tail; end.y=0;
                if(end.magnitude>=clearance)
                    return CustomerRoutePlanner.IsClear(path.Skip(i).ToArray(),new[]{tail},clearance);
            }
            return false;
        }
        private bool CanBeginAdvance(QueueAssignment assignment)
        {
            if(!visits.TryGetValue(assignment.VisitId,out var visit) || visit.Actor==null) return false;
            var leaders=visits.Values.Where(v=>v.RequestId!=0 && v.Actor!=null && v.Actor.isActiveAndEnabled &&
                (v.State==CustomerVisitState.Exiting || v.State==CustomerVisitState.Advancing &&
                    v.Assignment.SlotIndex<assignment.SlotIndex)).ToArray();
            if(leaders.Length==0) return true;
            var route=BuildRoute(visit.Actor.transform.position,assignment.Position,visit.Id,visit.Actor.Settings,assignment.SlotIndex);
            if(route==null) return false; // 活动前客腾位前先等待，不把临时占路立即判失败。
            var path=ExpandRoute(visit.Actor.transform.position,route);
            if(path==null) return false;
            var clearance=RouteClearance(visit.Actor.Settings)+2*visit.Actor.Settings.MaxSpeed*
                Mathf.Min(Time.deltaTime,visit.Actor.Settings.MaxSubstepSeconds*visit.Actor.Settings.MaxSubstepsPerFrame);
            foreach(var leader in leaders)
            {
                var targets=new[]{leader.Actor.Agent.destination}.Concat(leader.Route??Enumerable.Empty<Vector3>());
                var leadingPath=ExpandRoute(leader.Actor.transform.position,targets);
                if(leadingPath==null || RoutesConflict(path,leadingPath,clearance)) return false;
                // 同向逐格跟进可沿前客刚离开的站位走；交叉拐弯先等待安全空间。
                if(leader.State==CustomerVisitState.Advancing &&
                    Vector3.Distance(assignment.Position,leader.AdvanceStart)<=visit.Actor.Settings.ArrivalDistance+.001f) continue;
                // 后客不能先停在前客尚需经过的通道上，即使两条路径同向。
                if(!CustomerRoutePlanner.IsClear(leadingPath,new[]{assignment.Position},clearance)) return false;
            }
            return true;
        }
        private IReadOnlyList<Vector3> ExpandRoute(Vector3 start,IEnumerable<Vector3> targets)
        {
            var points=new List<Vector3>{start};
            foreach(var target in targets)
            {
                var path=adapter.GetCompletePath(points[points.Count-1],target);
                if(path==null || path.Count==0) return null;
                points.AddRange(path.Skip(1));
            }
            return points;
        }
        private static bool RoutesConflict(IReadOnlyList<Vector3> follower,IReadOnlyList<Vector3> leader,float clearance)
        {
            for(var i=1;i<follower.Count;i++) for(var j=1;j<leader.Count;j++)
            {
                var a=follower[i]-follower[i-1]; var b=leader[j]-leader[j-1]; a.y=b.y=0;
                if(a.sqrMagnitude<.000001f || b.sqrMagnitude<.000001f || Vector3.Dot(a.normalized,b.normalized)>=.5f) continue;
                var offset=leader[j-1]-follower[i-1]; var cross=a.x*b.z-a.z*b.x;
                if(Mathf.Abs(cross)>.000001f)
                {
                    var t=(offset.x*b.z-offset.z*b.x)/cross; var u=(offset.x*a.z-offset.z*a.x)/cross;
                    if(t>=0 && t<=1 && u>=0 && u<=1) return true;
                }
                if(!CustomerRoutePlanner.IsClear(new[]{follower[i-1],follower[i]},new[]{leader[j-1],leader[j]},clearance) ||
                    !CustomerRoutePlanner.IsClear(new[]{leader[j-1],leader[j]},new[]{follower[i-1],follower[i]},clearance)) return true;
            }
            return false;
        }
        private static bool CanFollowEntryBend(IReadOnlyList<Vector3> follower,IReadOnlyList<Vector3> leader,float clearance,float tolerance)
        {
            if(follower.Count<2 || leader.Count<2) return false;
            var segment=0; var progress=0f;
            // 同一弯道按顺序跟随：前客必须已在后客路线前方，不能借此放行横穿/对向。
            // Match the shared corridor in order; P11 still guards every actual body displacement.
            for(var i=1;i<follower.Count;i++)
            {
                var delta=follower[i]-follower[i-1]; delta.y=0;
                var ahead=leader[0]-follower[i-1]; ahead.y=0;
                var t=delta.sqrMagnitude<.000001f?0:Mathf.Clamp01(Vector3.Dot(ahead,delta)/delta.sqrMagnitude);
                if(ahead.magnitude>0 && (ahead-delta*t).magnitude<=tolerance &&
                    progress+delta.magnitude*t>=clearance && SameDirection(delta,leader[1]-leader[0]))
                { segment=i; break; }
                progress+=delta.magnitude;
            }
            if(segment==0) return false;
            var branch=leader.Count-1;
            for(var j=1;j<leader.Count-1;j++)
            {
                // Native paths may split a straight edge; never skip a follower bend.
                if(!MatchesEdge(follower,segment,leader[j-1],leader[j],tolerance))
                {
                    if(segment+1<follower.Count && MatchesEdge(follower,segment+1,leader[j-1],leader[j],tolerance)) segment++;
                    else { branch=j; break; }
                }
            }
            // 末端绕开前方站位时可能多一个拐点；只允许共同走廊后的两段有限分流。
            var fork=leader[branch-1];
            if(CustomerRoutePlanner.IsClear(new[]{follower[segment-1],follower[segment]},new[]{fork},tolerance)) return false;
            if(Vector3.Distance(fork,follower[segment])<=tolerance && segment<follower.Count-1) segment++;
            if(leader.Count-branch>2 || follower.Count-segment>2) return false;
            var forward=leader[branch]-fork; forward.y=0;
            if(forward.sqrMagnitude<.000001f) return false;
            for(var i=branch;i<leader.Count;i++)
            {
                var delta=leader[i]-leader[i-1]; delta.y=0;
                if(delta.sqrMagnitude<.000001f || Vector3.Dot(delta.normalized,forward.normalized)<.5f ||
                    Vector3.Dot(leader[i-1]-fork,delta)<-.000001f) return false;
            }
            for(var i=segment;i<follower.Count;i++)
            {
                var from=i==segment?fork:follower[i-1]; var delta=follower[i]-from; delta.y=0;
                if(delta.sqrMagnitude<.000001f || Vector3.Dot(delta.normalized,forward.normalized)<.5f ||
                    Vector3.Dot(from-fork,delta)<-.000001f) return false;
            }
            // A genuine interior crossing remains a conflict even if the paths later join.
            for(var i=1;i<follower.Count;i++) for(var j=1;j<leader.Count;j++)
            {
                var a=follower[i]-follower[i-1]; var b=leader[j]-leader[j-1];
                var offset=leader[j-1]-follower[i-1]; var cross=a.x*b.z-a.z*b.x;
                if(Mathf.Abs(cross)<.000001f) continue;
                var t=(offset.x*b.z-offset.z*b.x)/cross; var u=(offset.x*a.z-offset.z*a.x)/cross;
                if(t>.001f && t<.999f && u>.001f && u<.999f) return false;
            }
            return true;
        }
        private static bool MatchesEdge(IReadOnlyList<Vector3> path,int segment,Vector3 from,Vector3 to,float tolerance)
            => SameDirection(path[segment]-path[segment-1],to-from) &&
                !CustomerRoutePlanner.IsClear(new[]{path[segment-1],path[segment]},new[]{from},tolerance) &&
                !CustomerRoutePlanner.IsClear(new[]{path[segment-1],path[segment]},new[]{to},tolerance);
        private static bool SameDirection(Vector3 a,Vector3 b)
        { a.y=b.y=0; return a.sqrMagnitude>.000001f && b.sqrMagnitude>.000001f && Vector3.Dot(a.normalized,b.normalized)>=.9f; }
        private float RouteClearance(NavigationSettings settings)
            // 路径验证针对已知角色中心；ArrivalDistance不是身体半径。
            // 实际移动仍由P11连续碰撞约束，slot/admission另保留到站误差。
            => settings.AgentRadius+world.RegisteredActors.Where(a=>a!=null).Select(a=>a.Settings.AgentRadius)
                .DefaultIfEmpty(settings.AgentRadius).Max()+settings.CollisionSkin+settings.Epsilon;
        private void BlockVisit(Visit visit,CustomerVisitState phase,string reason)
        {
            visit.RequestId=0; visit.State=CustomerVisitState.Blocked; recoveringQueue=false;
            visit.Failure=(phase==CustomerVisitState.Exiting?"离场":phase==CustomerVisitState.Advancing?"前移":"入店")+"："+reason;
            if(phase==CustomerVisitState.Advancing)
                foreach(var follower in visits.Values.Where(v=>v.State==CustomerVisitState.Advancing &&
                    v.Assignment.SlotIndex>visit.Assignment.SlotIndex).ToArray())
                    HoldVisit(follower); // 前客失败，退休并发后客请求，保留实际位置/容量。
        }
        private void SendRouteSegment(Visit visit,CustomerVisitState phase)
        {
            if(++visit.Segments>32) { BlockVisit(visit,phase,"绕行次数已达上限"); return; }
            var point=visit.Route.Peek();
            // 其他顾客可能已移动；每段从实际位置验证，必要时有限重算整条剩余路线。
            if(!CustomerRoutePlanner.IsClear(adapter.GetCompletePath(visit.Actor.transform.position,point),RouteObstacles(visit.Id),RouteClearance(visit.Actor.Settings)))
            {
                var replanned=++visit.Replans<=3?BuildRoute(visit.Actor.transform.position,visit.FinalTarget.Position,visit.Id,visit.Actor.Settings):null;
                if(replanned==null || replanned.Count>24) { BlockVisit(visit,phase,"通道被占用"); return; }
                visit.Route=new Queue<Vector3>(replanned);
            }
            point=visit.Route.Dequeue(); var final=visit.Route.Count==0;
            var target=final?visit.FinalTarget:new NavigationTarget(point);
            var path=adapter.GetCompletePath(visit.Actor.transform.position,target.Position);
            if(path==null || path.Count==0) { BlockVisit(visit,phase,"路径不可达"); return; }
            var endpoint=path[path.Count-1];
            var generation=++visit.Generation; var layout=adapter.Revision;
            visit.RequestId=0; visit.State=visit.ResumeState=phase;
            var start=world.Service.MoveTo(visit.Id,target,result=>
            {
                if(!visits.ContainsKey(visit.Id) || generation!=visit.Generation || layout!=adapter.Revision) return;
                visit.RequestId=0;
                if(result.Status!=MovementStatus.Arrived) { BlockVisit(visit,phase,result.Reason==NavigationFailure.MovementTimeout?"移动超时":result.Status==MovementStatus.Recovered?"原目标尚未到达":"路径不可达"); return; }
                // 临时waypoint沿用Service到站容差；完整skin可能使拐点无法精确抵达。
                // 下一段从实际pose重新查询/验证，不把临时Arrived当作Queued或瞬移到拐点。
                var remaining=result.FinalPosition-endpoint; remaining.y=0;
                if(!final && remaining.magnitude>visit.Actor.Settings.ArrivalDistance)
                { BlockVisit(visit,phase,"尚未抵达绕行拐点"); return; }
                if(!final) { SendRouteSegment(visit,phase); return; }
                if(phase==CustomerVisitState.Exiting) { RemoveVisit(visit.Id); return; }
                if(queue.Complete(visit.Id,visit.Assignment.Generation) && visit.Admission.MarkQueued()) visit.State=CustomerVisitState.Queued;
                else BlockVisit(visit,phase,"站位提交失败");
            });
            // 同步callback可能已经将状态置为Blocked/Removed，不用返回值复活。
            if(visits.ContainsKey(visit.Id) && visit.Generation==generation && visit.State==phase)
            { if(start.Accepted) visit.RequestId=start.RequestId; else BlockVisit(visit,phase,"移动请求未接受"); }
        }
        public bool TryLetFrontLeave()
        {
            if(!isActiveAndEnabled || !Configured || !world.LayoutAvailable || queue==null || (decoration!=null && decoration.IsOpen) ||
                visits.Values.Any(v=>v.State==CustomerVisitState.Entering || v.State==CustomerVisitState.Advancing ||
                    v.State==CustomerVisitState.Exiting) || // 拦截活动移动；原位Blocked尾客仍允许安全队首离场。
                !queue.TryBeginFrontExit(out var id) || !visits.TryGetValue(id,out var visit)) return false;
            visit.CounterPosition=visit.Actor.transform.position; visit.Admission.CancelUnusedPickup();
            Send(visit,CustomerVisitState.Exiting,new NavigationTarget(exit.position)); return true;
        }
        public void RemoveVisit(string id)
        {
            if(!visits.TryGetValue(id,out var visit)) return;
            visits.Remove(id); visit.Generation++; visit.State=CustomerVisitState.Removed;
            if(visit.RequestId!=0) world?.Service.Cancel(visit.RequestId);
            queue?.Remove(id); if(visit.Actor!=null) { world?.Unregister(visit.Actor); Destroy(visit.Actor.gameObject); }
            visit.Admission.Dispose();
        }
        private void ResetCustomersAfterDecoration()
        {
            // 只退休本控制器拥有的顾客；员工/其他World角色独立保留 / customers only.
            foreach(var id in visits.Keys.ToArray()) RemoveVisit(id);
            queue=null; revision=-1; suspended=true; recoveringQueue=false;
            admissionRejected=admissionRouteUnavailable=false; SelectedRegisterId="";
            clock?.Tick(0,false,false);
            // 保留Capacity对象与递增visit ID；下一次有效营业重新抽取完整随机间隔。
        }
        private void HoldVisits()
        {
            suspended=true;
            foreach(var visit in visits.Values.ToArray())
            {
                if(visit.State==CustomerVisitState.Queued || visit.State==CustomerVisitState.Blocked) continue;
                HoldVisit(visit);
            }
        }
        private void HoldVisit(Visit visit)
        {
            visit.Generation++; var request=visit.RequestId; visit.RequestId=0; visit.State=CustomerVisitState.Blocked;
            if(request!=0) world?.Service.Cancel(request);
        }
        public void ResumeBlockedVisits()
        {
            if(!isActiveAndEnabled || !Configured || !world.LayoutAvailable || queue==null) return;
            HoldVisits(); suspended=false; queue.RestartAssignments(); recoveringQueue=true;
            foreach(var visit in visits.Values.ToArray())
            {
                if(visit.Actor==null || !visit.Actor.isActiveAndEnabled) continue;
                if(visit.ResumeState==CustomerVisitState.Exiting)
                    Send(visit,CustomerVisitState.Exiting,new NavigationTarget(exit.position));
                else if(visit.State!=CustomerVisitState.Queued) visit.State=CustomerVisitState.Blocked;
            }
        }
        private void OnDisable() { HoldVisits(); clock?.Tick(0,false,false); }
        private void OnEnable() { suspended=true; }
        private void OnDestroy()
        { disposed=true; foreach(var id in visits.Keys.ToArray()) RemoveVisit(id); Capacity=null; queue=null; }
    }
}


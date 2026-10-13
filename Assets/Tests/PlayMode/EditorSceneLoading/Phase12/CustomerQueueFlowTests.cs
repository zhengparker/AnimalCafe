using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using AnimalCafe.Customers;
using AnimalCafe.Navigation;
using AnimalCafe.Core.Time;

namespace AnimalCafe.Tests.PlayMode.Phase12Integration
{
    public class CustomerQueueFlowTests
    {
        private GameObject root;
        private NavigationWorld world;
        private NavigationLayoutAdapter adapter;
        private CustomerFlowController flow;
        private Transform spawn,exit,head;
        private float savedScale,savedCapture;
        [SetUp] public void Setup()
        {
            savedScale=Time.timeScale; savedCapture=Time.captureDeltaTime; Time.captureDeltaTime=1f/60;
            root=new GameObject("P12 flow fixture"); world=root.AddComponent<NavigationWorld>();
            adapter=root.AddComponent<NavigationLayoutAdapter>(); var time=root.AddComponent<GameTimeService>();
            var floorGo=new GameObject("floor"); floorGo.transform.SetParent(root.transform); floorGo.transform.position=Vector3.down*.1f;
            var floor=floorGo.AddComponent<BoxCollider>(); floor.size=new Vector3(18,.2f,18);
            adapter.Configure(null,null,null,floor,new Collider[0],world,false);
            Assert.That(adapter.RebuildAndValidate().CanResume,Is.True);
            spawn=Marker("spawn",new Vector3(-4,0,4)); exit=Marker("exit",new Vector3(-4,0,-4)); head=Marker("head",Vector3.zero);
            var prefabs=new[]{"Shiba","Westie"}.Select(s=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase11/Characters/"+s+"/PF_"+s+"_Navigation.prefab").GetComponent<NavigationActor>()).ToArray();
            flow=root.AddComponent<CustomerFlowController>(); flow.Configure(world,adapter,time,prefabs,spawn,exit,head); flow.AutoSpawn=false;
            flow.Step(0);
        }
        private Transform Marker(string name,Vector3 point)
        { var go=new GameObject(name); go.transform.SetParent(root.transform); go.transform.position=point; return go.transform; }
        [TearDown] public void Cleanup()
        { if(root!=null) Object.DestroyImmediate(root); Time.captureDeltaTime=savedCapture; Time.timeScale=savedScale; }
        [Test] public void InvalidSpawnDoesNotLeavePartialActorOrReservations()
        {
            Assert.That(flow.Capacity,Is.Not.Null); spawn.position=Vector3.one*1000;
            Assert.That(flow.TrySpawn(),Is.False); Assert.That(world.RegisteredActors.Count,Is.Zero);
            foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
        }
        [UnityTest] public IEnumerator RealPrefabsQueueAndFrontExitReleasesAllCapacity()
        {
            Assert.That(flow.TrySpawn(),Is.True);
            yield return Until(()=>flow.Snapshot.Count==1 && flow.Snapshot[0].State==CustomerVisitState.Queued);
            Assert.That(flow.Capacity.GetCapacities()[0].Occupied,Is.EqualTo(1));
            Assert.That(flow.TrySpawn(),Is.True);
            yield return Until(()=>flow.Snapshot.All(s=>s.State==CustomerVisitState.Queued));
            Assert.That(world.RegisteredActors.Select(a=>a.ActorId).Distinct().Count(),Is.EqualTo(2));
            Assert.That(flow.TryLetFrontLeave(),Is.True); Assert.That(flow.TryLetFrontLeave(),Is.False);
            yield return Until(()=>flow.Snapshot.Count==1 && flow.Snapshot[0].State==CustomerVisitState.Queued);
            Assert.That(flow.Capacity.GetCapacities()[0].Occupied,Is.EqualTo(1));
            Assert.That(flow.TryLetFrontLeave(),Is.True); yield return Until(()=>flow.Snapshot.Count==0);
            foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
        }
        [UnityTest] public IEnumerator ExitFailureKeepsInsideActorAndTotalThenRepairResumes()
        {
            Assert.That(flow.TrySpawn(),Is.True); yield return Until(()=>flow.Snapshot[0].State==CustomerVisitState.Queued);
            exit.position=Vector3.one*1000; Assert.That(flow.TryLetFrontLeave(),Is.True);
            yield return Until(()=>flow.Snapshot[0].State==CustomerVisitState.Blocked);
            Assert.That(flow.Capacity.GetCapacities()[0].Occupied,Is.EqualTo(1));
            exit.position=new Vector3(-4,0,-4); flow.ResumeBlockedVisits();
            yield return Until(()=>flow.Snapshot.Count==0);
        }
        [UnityTest] public IEnumerator DisableKeepsOccupancyAndEnableRevalidates()
        {
            Assert.That(flow.TrySpawn(),Is.True); yield return Until(()=>flow.Snapshot[0].State==CustomerVisitState.Queued);
            var before=world.RegisteredActors[0].transform.position; flow.enabled=false; yield return null;
            Assert.That(world.RegisteredActors.Count,Is.EqualTo(1)); Assert.That(flow.Capacity.GetCapacities()[0].Occupied,Is.EqualTo(1));
            Assert.That(world.RegisteredActors[0].transform.position,Is.EqualTo(before));
            flow.enabled=true; flow.Step(0); Assert.That(flow.TryLetFrontLeave(),Is.True);
            yield return Until(()=>flow.Snapshot.Count==0);
        }
        [UnityTest] public IEnumerator LayoutRevisionDuringEntryKeepsVisitAndReplansWithoutTeleport()
        {
            Assert.That(flow.TrySpawn(),Is.True); yield return null;
            var actor=world.RegisteredActors[0]; var previous=actor.transform.position;
            world.SuspendLayout(adapter.Revision+1); flow.Step(0);
            Assert.That(flow.Snapshot.Count,Is.EqualTo(1));
            Assert.That(flow.Capacity.GetCapacities()[0].Occupied,Is.EqualTo(1));
            Assert.That(Vector3.Distance(previous,actor.transform.position),Is.LessThan(.001f));
            adapter.ReleaseOwnedNavigation(); Assert.That(adapter.RebuildAndValidate().CanResume,Is.True);
            flow.Step(0); yield return Until(()=>flow.Snapshot[0].State==CustomerVisitState.Queued);
        }
        [UnityTest] public IEnumerator RemovingVisitTwiceClearsActorAndCapacity()
        {
            Assert.That(flow.TrySpawn(),Is.True); var id=flow.Snapshot[0].VisitId;
            flow.RemoveVisit(id); flow.RemoveVisit(id); yield return null;
            Assert.That(world.RegisteredActors.Count,Is.Zero);
            Assert.That(flow.Snapshot.Count,Is.Zero);
            foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
        }
        [UnityTest] public IEnumerator IndependentContinuousSeparationDuringEntryAndExit()
        {
            Assert.That(flow.TrySpawn(),Is.True); yield return Until(()=>flow.Snapshot[0].State==CustomerVisitState.Queued);
            Assert.That(flow.TrySpawn(),Is.True);
            for(var frame=0;frame<1000 && !flow.Snapshot.All(s=>s.State==CustomerVisitState.Queued);frame++)
            {
                var actors=world.RegisteredActors.ToArray(); var old=actors.Select(a=>a.transform.position).ToArray();
                yield return null;
                for(var i=0;i<actors.Length;i++) for(var j=0;j<i;j++)
                {
                    var relative=old[i]-old[j]; var delta=(actors[i].transform.position-old[i])-(actors[j].transform.position-old[j]);
                    var t=delta.sqrMagnitude<1e-12f?0:Mathf.Clamp01(-Vector3.Dot(relative,delta)/delta.sqrMagnitude);
                    Assert.That((relative+delta*t).magnitude,Is.GreaterThanOrEqualTo(.899f));
                }
            }
            Assert.That(flow.Snapshot.All(s=>s.State==CustomerVisitState.Queued),Is.True);
        }
        private IEnumerator Until(System.Func<bool> done)
        {
            for(var i=0;i<2400 && !done();i++) yield return null;
            Assert.That(done(),Is.True,flow.Status+" / "+string.Join(",",flow.Snapshot.Select(s=>s.State.ToString())));
        }
        [UnityTest] public IEnumerator ConcurrentAdmissionDuringEarlierEntryMovesBothAndDrains()
        { yield return ConcurrentEntryScenario(false); }
        [UnityTest] public IEnumerator ConcurrentAdmissionPauseDisableKeepsActorsAndResumes()
        { yield return ConcurrentEntryScenario(true); }
        private IEnumerator ConcurrentEntryScenario(bool interrupt)
        {
            spawn.position=new Vector3(-6,0,0); // 独立安全入口方向，尾位不堵前客的对角路线。
            flow.Capacity.UpdateFloorCellCount(16); // 两个Counter名额，自动入店不能掩盖重复预留。
            Assert.That(flow.TrySpawn(),Is.True);
            var first=world.RegisteredActors.Single();
            yield return Until(()=>Vector3.Distance(first.transform.position,spawn.position)>1.5f);
            Assert.That(flow.Snapshot.Single().State,Is.EqualTo(CustomerVisitState.Entering));
            UseOneSecondAutoSpawn();
            yield return ObserveConcurrentAdmission(CustomerVisitState.Entering,2,
                ()=>flow.Snapshot.Count==2 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued),interrupt);
            yield return DrainConcurrentAdmissionQueue();
        }
        [UnityTest] public IEnumerator ConcurrentAdmissionWaitsWhenTailWouldBlockEarlierEntryThenRecovers()
        {
            Assert.That(flow.TrySpawn(),Is.True); var first=world.RegisteredActors.Single();
            yield return Until(()=>Vector3.Distance(first.transform.position,spawn.position)>1.5f);
            Assert.That(flow.Snapshot.Single().State,Is.EqualTo(CustomerVisitState.Entering));
            var before=flow.Capacity.GetCapacities().Select(c=>c.Used).ToArray();
            Assert.That(flow.TrySpawn(),Is.False,"Do not park a new tail on the earlier customer's remaining route");
            CollectionAssert.AreEqual(before,flow.Capacity.GetCapacities().Select(c=>c.Used).ToArray());
            Assert.That(world.RegisteredActors.Count,Is.EqualTo(1));
            yield return Until(()=>flow.Snapshot.Single().State==CustomerVisitState.Queued);
            Assert.That(flow.TrySpawn(),Is.True);
            yield return Until(()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
            yield return DrainConcurrentAdmissionQueue();
        }
        [UnityTest] public IEnumerator ConcurrentAdmissionDuringExitMovesBothAndDrains()
        {
            exit.position=new Vector3(-7,0,-7);
            Assert.That(flow.TrySpawn(),Is.True); yield return Until(()=>flow.Snapshot.Single().State==CustomerVisitState.Queued);
            var leaving=flow.Snapshot.Single().VisitId;
            Assert.That(flow.TryLetFrontLeave(),Is.True);
            yield return Until(()=>flow.Capacity.GetCapacities()[1].Used==0 && flow.Snapshot.Single().Position.magnitude>2);
            Assert.That(flow.Snapshot.Single().State,Is.EqualTo(CustomerVisitState.Exiting));
            UseOneSecondAutoSpawn();
            yield return ObserveConcurrentAdmission(CustomerVisitState.Exiting,2,
                ()=>flow.Snapshot.Count==1 && flow.Snapshot[0].VisitId!=leaving && flow.Snapshot[0].State==CustomerVisitState.Queued);
            yield return DrainConcurrentAdmissionQueue();
        }
        [UnityTest] public IEnumerator ConcurrentAdmissionDuringAdvanceUsesVacatedTailAndDrains()
        {
            exit.position=new Vector3(-7,0,-7);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var queue=(CounterQueueService)typeof(CustomerFlowController).GetField("queue",flags).GetValue(flow);
            queue.UpdateSlots(new[]{Vector3.zero,Vector3.forward*3,Vector3.forward*6});
            for(var i=0;i<2;i++)
            { Assert.That(flow.TrySpawn(),Is.True); yield return Until(()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued)); }
            var leaving=flow.Snapshot[0].VisitId;
            Assert.That(flow.TryLetFrontLeave(),Is.True);
            yield return Until(()=>flow.Snapshot.Any(v=>v.State==CustomerVisitState.Advancing && v.Position.z<1.85f && v.Position.z>.2f));
            Assert.That(flow.TrySpawn(),Is.True,"Safe vacated tail must admit while an earlier customer is still advancing");
            yield return ObserveConcurrentAdmission(CustomerVisitState.Advancing,3,
                ()=>flow.Snapshot.Count==2 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
            Assert.That(flow.Snapshot.Any(v=>v.VisitId==leaving),Is.False);
            Assert.That(Vector3.Distance(flow.Snapshot[0].Position,Vector3.zero),Is.LessThanOrEqualTo(.081f));
            Assert.That(Vector3.Distance(flow.Snapshot[1].Position,Vector3.forward*3),Is.LessThanOrEqualTo(.081f));
            yield return DrainConcurrentAdmissionQueue();
        }
        [UnityTest] public IEnumerator ConcurrentAdmissionWaitsForOpposingExitRouteWithoutReservingCapacity()
        {
            exit.position=spawn.position; // 进出共用路线，验证新入店必须让已有离场先通过。
            Assert.That(flow.TrySpawn(),Is.True); yield return Until(()=>flow.Snapshot.Single().State==CustomerVisitState.Queued);
            Assert.That(flow.TryLetFrontLeave(),Is.True);
            yield return Until(()=>flow.Snapshot.Single().Position.magnitude>1.5f);
            var before=flow.Capacity.GetCapacities().Select(c=>c.Used).ToArray();
            Assert.That(flow.TrySpawn(),Is.False);
            CollectionAssert.AreEqual(before,flow.Capacity.GetCapacities().Select(c=>c.Used).ToArray());
            Assert.That(flow.Snapshot.Count,Is.EqualTo(1));
            yield return Until(()=>flow.Snapshot.Count==0);
            Assert.That(flow.TrySpawn(),Is.True,"Temporary route conflict must recover without layout edits");
            yield return Until(()=>flow.Snapshot.Single().State==CustomerVisitState.Queued);
            yield return DrainConcurrentAdmissionQueue();
        }
        [UnityTest] public IEnumerator ConcurrentAdmissionWaitsForCrossingEntryRouteWithoutReservingCapacity()
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var queue=(CounterQueueService)typeof(CustomerFlowController).GetField("queue",flags).GetValue(flow);
            queue.UpdateSlots(new[]{new Vector3(3,0,0),new Vector3(-3,0,-4)});
            spawn.position=new Vector3(-6,0,0);
            Assert.That(flow.TrySpawn(),Is.True);
            yield return Until(()=>flow.Snapshot.Single().Position.x>-4.5f);
            Assert.That(flow.Snapshot.Single().State,Is.EqualTo(CustomerVisitState.Entering));
            spawn.position=new Vector3(-3,0,4); // 新路线横穿仍在入店的前客，不是同弯跟随。
            var before=flow.Capacity.GetCapacities().Select(c=>c.Used).ToArray();
            Assert.That(flow.TrySpawn(),Is.False);
            CollectionAssert.AreEqual(before,flow.Capacity.GetCapacities().Select(c=>c.Used).ToArray());
            Assert.That(world.RegisteredActors.Count,Is.EqualTo(1));
            yield return Until(()=>flow.Snapshot.Single().State==CustomerVisitState.Queued);
            Assert.That(flow.TrySpawn(),Is.True);
            yield return Until(()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
            yield return DrainConcurrentAdmissionQueue();
        }
        [UnityTest] public IEnumerator ConcurrentAdmissionAtTemporarilyOccupiedTailFollowsMovingEntryAndDrains()
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var queue=(CounterQueueService)typeof(CustomerFlowController).GetField("queue",flags).GetValue(flow);
            queue.UpdateSlots(new[]{Vector3.zero,new Vector3(-3,0,0)});
            spawn.position=new Vector3(-6,0,0);
            Assert.That(flow.TrySpawn(),Is.True);
            yield return Until(()=>flow.Snapshot.Single().Position.x>-2.5f);
            Assert.That(flow.Snapshot.Single().State,Is.EqualTo(CustomerVisitState.Entering));
            Assert.That(Vector3.Distance(flow.Snapshot.Single().Position,new Vector3(-3,0,0)),Is.LessThan(.98f));
            Assert.That(flow.TrySpawn(),Is.True,"An active entry moving away from the future tail can be followed safely");
            yield return ObserveConcurrentAdmission(CustomerVisitState.Entering,2,
                ()=>flow.Snapshot.Count==2 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
            yield return DrainConcurrentAdmissionQueue();
        }
        private void UseOneSecondAutoSpawn()
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(CustomerFlowController).GetField("clock",flags).SetValue(flow,new CustomerSpawnClock(()=>0));
            flow.AutoSpawn=true;
        }
        [UnityTest] public IEnumerator DueArrivalWaitsForEntranceThenSpawnsWithoutAnotherCountdown()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase11/Characters/Shiba/PF_Shiba_Navigation.prefab");
            var blocker=Object.Instantiate(prefab,spawn.position,Quaternion.identity,root.transform).GetComponent<NavigationActor>();
            Assert.That(blocker.TryInitializeRuntimeId("entrance-worker"),Is.True);
            Assert.That(world.Register(blocker),Is.True);
            UseOneSecondAutoSpawn(); flow.Step(1.01f);
            Assert.That(flow.Snapshot.Count,Is.Zero);
            Assert.That(flow.RemainingSeconds,Is.Zero);
            foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
            var time=root.GetComponent<GameTimeService>(); time.TrySetSpeed(GameSpeed.Paused);
            world.Unregister(blocker); Object.DestroyImmediate(blocker.gameObject);
            yield return null;
            Assert.That(flow.Snapshot.Count,Is.Zero,"A due arrival must not spawn while paused");
            time.TrySetSpeed(GameSpeed.Normal); flow.Step(.001f); flow.AutoSpawn=false;
            Assert.That(flow.Snapshot.Count,Is.EqualTo(1),"A cleared entrance must release the due arrival without drawing another interval");
            Assert.That(flow.RemainingSeconds,Is.EqualTo(1));
            yield return Until(()=>flow.Snapshot.Single().State==CustomerVisitState.Queued);
            yield return DrainConcurrentAdmissionQueue();
        }
        [UnityTest] public IEnumerator SafeFollowingEntryMayApproachFutureTailBeforePredecessorPassesIt()
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var queue=(CounterQueueService)typeof(CustomerFlowController).GetField("queue",flags).GetValue(flow);
            queue.UpdateSlots(new[]{Vector3.zero,new Vector3(-3,0,0)});
            spawn.position=new Vector3(-6,0,0);
            Assert.That(flow.TrySpawn(),Is.True);
            yield return Until(()=>flow.Snapshot.Single().Position.x>-3.75f);
            Assert.That(flow.Snapshot.Single().Position.x,Is.LessThan(-3),"The predecessor has not passed the future tail yet");
            Assert.That(flow.TrySpawn(),Is.True,"A same-direction follower can safely arrive after its predecessor passes the tail");
            yield return ObserveConcurrentAdmission(CustomerVisitState.Entering,2,
                ()=>flow.Snapshot.Count==2 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
            yield return DrainConcurrentAdmissionQueue();
        }
        private IEnumerator ObserveConcurrentAdmission(CustomerVisitState earlierPhase,int stopAtCount,System.Func<bool> done,bool interrupt=false)
        {
            var overlap=0; var settled=0; var interrupted=false;
            for(var frame=0;frame<2400 && settled<2;frame++)
            {
                if(flow.Snapshot.Count>=stopAtCount) flow.AutoSpawn=false;
                settled=done()?settled+1:0;
                var actors=world.RegisteredActors.ToArray(); var old=actors.Select(a=>a.transform.position).ToArray();
                var before=flow.Snapshot.ToDictionary(v=>v.VisitId,v=>v.State);
                yield return null;
                var entering=0; var earlier=0;
                for(var i=0;i<actors.Length;i++)
                {
                    if(actors[i]==null) continue;
                    var delta=actors[i].transform.position-old[i];
                    Assert.That(delta.magnitude,Is.LessThanOrEqualTo(actors[i].Settings.MaxSpeed*Time.deltaTime+.002f),"No teleport during admission");
                    if(delta.magnitude>.001f && before.TryGetValue(actors[i].ActorId,out var phase))
                    { if(phase==CustomerVisitState.Entering) entering++; if(phase==earlierPhase) earlier++; }
                    for(var j=0;j<i;j++)
                    {
                        if(actors[j]==null) continue;
                        var relative=old[i]-old[j]; var change=delta-(actors[j].transform.position-old[j]);
                        var t=change.sqrMagnitude<1e-12f?0:Mathf.Clamp01(-Vector3.Dot(relative,change)/change.sqrMagnitude);
                        Assert.That((relative+change*t).magnitude,Is.GreaterThanOrEqualTo(.909f),"Full continuous body separation");
                    }
                }
                if(entering>0 && (earlierPhase==CustomerVisitState.Entering?entering>=2:earlier>0)) overlap++;
                Assert.That(flow.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked),Is.False,flow.Status);
                if(interrupt && !interrupted && overlap>=12)
                {
                    interrupted=true; var time=root.GetComponent<GameTimeService>(); time.TrySetSpeed(GameSpeed.Paused);
                    var ids=flow.Snapshot.Select(v=>v.VisitId).ToArray();
                    var poses=world.RegisteredActors.Select(a=>a.transform.position).ToArray();
                    var tokens=flow.Capacity.GetCapacities().Select(c=>c.Used).ToArray();
                    for(var paused=0;paused<5;paused++) yield return null;
                    CollectionAssert.AreEqual(poses,world.RegisteredActors.Select(a=>a.transform.position).ToArray());
                    flow.enabled=false; time.TrySetSpeed(GameSpeed.Normal); yield return null; yield return null;
                    CollectionAssert.AreEqual(poses,world.RegisteredActors.Select(a=>a.transform.position).ToArray());
                    CollectionAssert.AreEqual(tokens,flow.Capacity.GetCapacities().Select(c=>c.Used).ToArray());
                    flow.enabled=true; flow.Step(0);
                    CollectionAssert.AreEqual(ids,flow.Snapshot.Select(v=>v.VisitId).ToArray());
                }
            }
            flow.AutoSpawn=false;
            Assert.That(settled,Is.EqualTo(2),flow.Status);
            Assert.That(overlap,Is.GreaterThanOrEqualTo(12),"Incoming and earlier customer must actually move together, not just share labels");
            if(interrupt) Assert.That(interrupted,Is.True);
            foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.EqualTo(ledger.Reserved+ledger.Occupied));
        }
        private IEnumerator DrainConcurrentAdmissionQueue()
        {
            flow.AutoSpawn=false; var expected=flow.Snapshot.Select(v=>v.VisitId).ToArray();
            foreach(var id in expected)
            {
                Assert.That(flow.Snapshot[0].VisitId,Is.EqualTo(id));
                Assert.That(flow.TryLetFrontLeave(),Is.True); Assert.That(flow.TryLetFrontLeave(),Is.False);
                yield return Until(()=>!flow.Snapshot.Any(v=>v.VisitId==id) && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
            }
            foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
        }
        [UnityTest] public IEnumerator StraightQueueFollowersMoveTogetherBeforeLeaderArrives()
        {
            for(var i=0;i<4;i++)
            { Assert.That(flow.TrySpawn(),Is.True); yield return Until(()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued)); }
            var remainingIds=flow.Snapshot.Skip(1).Select(v=>v.VisitId).ToArray();
            Assert.That(flow.TryLetFrontLeave(),Is.True);
            yield return ObserveParallelAdvances(()=>flow.Snapshot.Count==3 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued),false);
            CollectionAssert.AreEqual(remainingIds,flow.Snapshot.Select(v=>v.VisitId).ToArray());
            AssertCurrentStraightQueue(Vector3.zero);
        }
        [UnityTest] public IEnumerator LayoutReflowMovesIndependentlyAndSurvivesPauseDisable()
        {
            for(var i=0;i<3;i++)
            { Assert.That(flow.TrySpawn(),Is.True); yield return Until(()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued)); }
            var ids=flow.Snapshot.Select(v=>v.VisitId).ToArray();
            flow.enabled=false; // validationHead不是layout实体，需显式触发重排。
            head.position=new Vector3(3,0,0);
            adapter.RefreshConfirmedLayout(); Assert.That(adapter.RebuildAndValidate().CanResume,Is.True);
            flow.enabled=true; flow.Step(0);
            Assert.That(flow.Snapshot.Any(v=>v.State==CustomerVisitState.Advancing),Is.True,"Fixture must submit a real reflow, not observe old Queued slots");
            yield return ObserveParallelAdvances(()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued),true);
            CollectionAssert.AreEqual(ids,flow.Snapshot.Select(v=>v.VisitId).ToArray());
            AssertCurrentStraightQueue(new Vector3(3,0,0));
            for(var i=0;i<3;i++)
            { Assert.That(flow.TryLetFrontLeave(),Is.True); var count=2-i; yield return Until(()=>flow.Snapshot.Count==count && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued)); }
            foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
        }
        [UnityTest] public IEnumerator FailedConcurrentLeaderHoldsFollowersUntilRepair()
        {
            for(var i=0;i<3;i++)
            { Assert.That(flow.TrySpawn(),Is.True); yield return Until(()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued)); }
            flow.enabled=false; head.position=new Vector3(3,0,0); flow.enabled=true; flow.Step(0);
            yield return Until(()=>flow.Snapshot.Count(v=>v.State==CustomerVisitState.Advancing)>=2);
            var moving=flow.Snapshot.Where(v=>v.State==CustomerVisitState.Advancing).Select(v=>v.VisitId).ToArray();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var visits=(System.Collections.IDictionary)typeof(CustomerFlowController).GetField("visits",flags).GetValue(flow);
            var leader=visits[moving[0]];
            var request=(long)leader.GetType().GetField("RequestId").GetValue(leader);
            Assert.That(request,Is.GreaterThan(0)); world.Service.Cancel(request); // 真实Cancel回调，不模拟movement结果。
            Assert.That(flow.Snapshot.Where(v=>moving.Contains(v.VisitId)).All(v=>v.State==CustomerVisitState.Blocked),Is.True,
                "A failed leader must hold active followers and retire their requests");
            var poses=flow.Snapshot.Select(v=>v.Position).ToArray(); yield return null; yield return null;
            CollectionAssert.AreEqual(poses,flow.Snapshot.Select(v=>v.Position).ToArray());
            Assert.That(flow.Capacity.GetCapacities()[0].Occupied,Is.EqualTo(3));
            flow.ResumeBlockedVisits();
            yield return Until(()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
            AssertCurrentStraightQueue(new Vector3(3,0,0));
        }
        private void AssertCurrentStraightQueue(Vector3 expectedHead)
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var queue=(CounterQueueService)typeof(CustomerFlowController).GetField("queue",flags).GetValue(flow);
            var slots=(Vector3[])typeof(CounterQueueService).GetField("slots",flags).GetValue(queue);
            Assert.That(Vector3.Distance(slots[0],expectedHead),Is.LessThan(.001f),"Reflow must use the actual NEW head");
            for(var i=0;i<flow.Snapshot.Count;i++)
            {
                Assert.That(slots[i].x,Is.EqualTo(expectedHead.x).Within(.001f));
                if(i>0) Assert.That(slots[i].z-slots[i-1].z,Is.GreaterThanOrEqualTo(.999f));
                AssertQueuePose(i,slots[i],Vector3.back); // 实际到新站位，不能只看Queued标签。
            }
        }
        private IEnumerator ObserveParallelAdvances(System.Func<bool> done,bool interrupt)
        {
            var overlapFrames=0; var settled=0; var interrupted=false;
            for(var frame=0;frame<3600 && settled<2;frame++)
            {
                settled=done()?settled+1:0;
                var actors=world.RegisteredActors.ToArray(); var old=actors.Select(a=>a.transform.position).ToArray();
                yield return null;
                var moving=0;
                for(var i=0;i<actors.Length;i++)
                {
                    if(actors[i]==null) continue;
                    var delta=actors[i].transform.position-old[i];
                    Assert.That(delta.magnitude,Is.LessThanOrEqualTo(actors[i].Settings.MaxSpeed*Time.deltaTime+.002f));
                    if(delta.magnitude>.001f && flow.Snapshot.Any(v=>v.VisitId==actors[i].ActorId && v.State==CustomerVisitState.Advancing)) moving++;
                    for(var j=0;j<i;j++)
                    {
                        if(actors[j]==null) continue;
                        var relative=old[i]-old[j]; var relativeDelta=delta-(actors[j].transform.position-old[j]);
                        var t=relativeDelta.sqrMagnitude<1e-12f?0:Mathf.Clamp01(-Vector3.Dot(relative,relativeDelta)/relativeDelta.sqrMagnitude);
                        Assert.That((relative+relativeDelta*t).magnitude,Is.GreaterThanOrEqualTo(.909f),"Concurrent motion must retain full body separation");
                    }
                }
                if(moving>=2) overlapFrames++;
                var ordered=flow.Snapshot.Where(v=>v.State!=CustomerVisitState.Exiting).ToArray();
                for(var i=1;i<ordered.Length;i++)
                    Assert.That(ordered[i].Position.z-ordered[i-1].Position.z,Is.GreaterThanOrEqualTo(.909f),"Following must not overtake the earlier customer");
                if(interrupt && !interrupted && overlapFrames>=6)
                {
                    interrupted=true; var time=root.GetComponent<GameTimeService>(); time.TrySetSpeed(GameSpeed.Paused);
                    var poses=world.RegisteredActors.Select(a=>a.transform.position).ToArray();
                    for(var i=0;i<5;i++) yield return null;
                    CollectionAssert.AreEqual(poses,world.RegisteredActors.Select(a=>a.transform.position).ToArray());
                    flow.enabled=false; time.TrySetSpeed(GameSpeed.Normal); yield return null; yield return null;
                    CollectionAssert.AreEqual(poses,world.RegisteredActors.Select(a=>a.transform.position).ToArray());
                    flow.enabled=true; flow.Step(0); // 旧请求退休后，每位从实际位置重新恢复。
                }
            }
            Assert.That(settled,Is.EqualTo(2),flow.Status);
            Assert.That(overlapFrames,Is.GreaterThanOrEqualTo(12),"Two actors must actually move together before arrival, not just share a state label");
            if(interrupt) Assert.That(interrupted,Is.True);
        }
        [UnityTest] public IEnumerator RebuiltLayoutReassignsMovingCustomerToNewHead()
        {
            Assert.That(flow.TrySpawn(),Is.True); yield return null;
            var actor=world.RegisteredActors[0]; var before=actor.transform.position;
            head.position=new Vector3(3,0,0);
            var wall=new GameObject("old head blocked"); wall.transform.SetParent(root.transform);
            wall.transform.position=new Vector3(0,.7f,0); var obstacle=wall.AddComponent<BoxCollider>(); obstacle.size=new Vector3(1,1.4f,1);
            var floor=root.transform.Find("floor").GetComponent<BoxCollider>();
            adapter.Configure(null,null,null,floor,new[]{obstacle},world,false);
            adapter.RefreshConfirmedLayout(); Assert.That(adapter.RebuildAndValidate().CanResume,Is.True);
            flow.Step(0); Assert.That(Vector3.Distance(before,actor.transform.position),Is.LessThan(.001f));
            yield return Until(()=>flow.Snapshot[0].State==CustomerVisitState.Queued);
            Assert.That(Vector3.Distance(actor.transform.position,head.position),Is.LessThanOrEqualTo(.081f));
        }
        [UnityTest] public IEnumerator DynamicallyOccupiedTailRejectsAdmissionBeforeCapacityReservation()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase11/Characters/Shiba/PF_Shiba_Navigation.prefab");
            var other=Object.Instantiate(prefab,head.position,Quaternion.identity,root.transform).GetComponent<NavigationActor>();
            other.TryInitializeRuntimeId("foreign-occupant"); Assert.That(world.Register(other),Is.True);
            flow.Step(0); Assert.That(flow.Status,Does.Contain("队尾"));
            Assert.That(flow.TrySpawn(),Is.False);
            Assert.That(flow.Snapshot.Count,Is.Zero);
            foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
            yield return null;
        }
        [UnityTest] public IEnumerator ThirtyGameMinuteControlledLifecycleSoakKeepsCapacityBounded()
        {
            Time.captureDeltaTime=.05f; flow.AutoSpawn=true;
            for(var frame=0;frame<36000;frame++)
            {
                flow.TryLetFrontLeave(); yield return null;
                foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.LessThanOrEqualTo(ledger.Limit));
                Assert.That(flow.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked),Is.False,flow.Status);
            }
            flow.AutoSpawn=false;
            for(var frame=0;frame<4000 && flow.Snapshot.Count>0;frame++) { flow.TryLetFrontLeave(); yield return null; }
            Assert.That(flow.Snapshot.Count,Is.Zero,flow.Status);
            Assert.That(flow.Capacity.GetReservations().Count,Is.GreaterThan(60),"Multiple visits must complete, not an idle soak");
            foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
            Debug.Log("P12_SOAK released ledger records="+flow.Capacity.GetReservations().Count);
        }
        [UnityTest] public IEnumerator QueueDrainsAtTwentyFpsWithoutAnyConcurrentAdmission()
        {
            Time.captureDeltaTime=.05f;
            for(var count=0;count<6;count++)
            { Assert.That(flow.TrySpawn(),Is.True); yield return UntilWithContinuousBodyChecks(()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued)); }
            var arrivalOrder=flow.Snapshot.Select(v=>v.VisitId).ToArray();
            for(var count=6;count>0;count--)
            {
                var before=world.RegisteredActors.Select(a=>a.transform.position).ToArray();
                Assert.That(flow.TryLetFrontLeave(),Is.True);
                CollectionAssert.AreEqual(before,world.RegisteredActors.Select(a=>a.transform.position).ToArray(),"Starting departure must not change any actual pose");
                yield return UntilWithContinuousBodyChecks(()=>flow.Snapshot.Count==count-1 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
                CollectionAssert.AreEqual(arrivalOrder.Skip(7-count),flow.Snapshot.Select(v=>v.VisitId),"Recovery must preserve FIFO visit identity");
            }
            foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
        }
        private IEnumerator UntilWithContinuousBodyChecks(System.Func<bool> done)
        {
            for(var frame=0;frame<2400 && !done();frame++)
            {
                var actors=world.RegisteredActors.ToArray(); var old=actors.Select(a=>a.transform.position).ToArray();
                yield return null;
                for(var i=0;i<actors.Length;i++)
                {
                    if(actors[i]==null) continue;
                    var delta=actors[i].transform.position-old[i];
                    Assert.That(delta.magnitude,Is.LessThanOrEqualTo(actors[i].Settings.MaxSpeed*Time.deltaTime+.002f),"Recovery must not teleport");
                    Assert.That(adapter.IsPointWalkable(actors[i].transform.position),Is.True,"Stay on the owned NavMesh");
                    for(var j=0;j<i;j++)
                    {
                        if(actors[j]==null) continue;
                        var relative=old[i]-old[j]; var change=delta-(actors[j].transform.position-old[j]);
                        var t=change.sqrMagnitude<1e-12f?0:Mathf.Clamp01(-Vector3.Dot(relative,change)/change.sqrMagnitude);
                        Assert.That((relative+change*t).magnitude,Is.GreaterThanOrEqualTo(.909f),"Full continuous body separation during recovery");
                    }
                }
                Assert.That(flow.Snapshot.Any(v=>v.State==CustomerVisitState.Blocked),Is.False,flow.Status);
            }
            Assert.That(done(),Is.True,flow.Status);
        }
        [UnityTest] public IEnumerator ClockUsesAlreadyScaledDeltaAtAllSupportedFrameRates()
        {
            var time=root.GetComponent<GameTimeService>();
            foreach(var fps in new[]{15,20,30,60,120}) foreach(var speed in new[]{GameSpeed.Normal,GameSpeed.Fast})
            {
                flow.AutoSpawn=false; flow.Step(0); Time.captureDeltaTime=1f/fps;
                time.TrySetSpeed(speed); flow.AutoSpawn=true; flow.Step(0);
                var before=flow.RemainingSeconds; var elapsed=0f;
                for(var frame=0;frame<2;frame++) { yield return null; elapsed+=Time.deltaTime; }
                Assert.That(flow.RemainingSeconds,Is.EqualTo(before-elapsed).Within(.001f),"No double scaling at "+fps+" / "+speed);
            }
            time.TrySetSpeed(GameSpeed.Paused); var remaining=flow.RemainingSeconds;
            yield return null; yield return null;
            Assert.That(flow.RemainingSeconds,Is.EqualTo(remaining).Within(.001f));
        }
        [UnityTest] public IEnumerator RegistrationRejectionRollsBackPublishedPreparation()
        {
            var template=new GameObject("invalid actor template").AddComponent<NavigationActor>();
            try
            {
                flow.Configure(world,adapter,root.GetComponent<GameTimeService>(),new[]{template},spawn,exit,head);
                flow.Step(0); Assert.That(flow.TrySpawn(),Is.False);
                yield return null;
                Assert.That(flow.Snapshot.Count,Is.Zero); Assert.That(world.RegisteredActors.Count,Is.Zero);
                Assert.That(root.GetComponentsInChildren<NavigationActor>(true).Length,Is.Zero);
                Assert.That(flow.Capacity.GetReservations().Count,Is.EqualTo(3),"Reached reserve before Register rejection");
                flow.Step(0); Assert.That(flow.Status,Does.Contain("入店失败"),"Rejected attempt needs a visible reason");
                foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
            }
            finally { Object.DestroyImmediate(template.gameObject); }
        }
        [UnityTest] public IEnumerator CounterReleaseOccursAtRealLeaveBeforeExitArrival()
        {
            Assert.That(flow.TrySpawn(),Is.True); yield return Until(()=>flow.Snapshot[0].State==CustomerVisitState.Queued);
            Assert.That(flow.TryLetFrontLeave(),Is.True);
            yield return Until(()=>flow.Capacity.GetCapacities()[1].Used==0);
            Assert.That(flow.Snapshot.Count,Is.EqualTo(1));
            Assert.That(flow.Snapshot[0].State,Is.EqualTo(CustomerVisitState.Exiting));
            Assert.That(flow.Capacity.GetCapacities()[0].Occupied,Is.EqualTo(1));
            Assert.That(flow.Capacity.GetCapacities()[2].Used,Is.Zero);
            yield return Until(()=>flow.Snapshot.Count==0);
        }
        [UnityTest] public IEnumerator ThreeCustomersShrinkToTwoSlotsDrainAndRecover()
        {
            for(var i=0;i<3;i++)
            { Assert.That(flow.TrySpawn(),Is.True); yield return Until(()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued)); }
            var ids=flow.Snapshot.Select(v=>v.VisitId).ToArray();
            flow.Capacity.UpdateFloorCellCount(16);
            Assert.That(flow.Capacity.Limits.CounterQueue,Is.EqualTo(2));
            adapter.RefreshConfirmedLayout(); Assert.That(adapter.RebuildAndValidate().CanResume,Is.True); flow.Step(0);
            Assert.That(flow.Snapshot.Count,Is.EqualTo(3)); Assert.That(flow.TrySpawn(),Is.False);
            Assert.That(root.GetComponent<GameTimeService>().IsResumeBlocked,Is.False);
            for(var i=0;i<3;i++)
            {
                yield return Until(()=>flow.Snapshot[0].State==CustomerVisitState.Queued);
                Assert.That(flow.Snapshot[0].VisitId,Is.EqualTo(ids[i])); Assert.That(flow.TryLetFrontLeave(),Is.True);
                var expected=2-i; yield return Until(()=>flow.Snapshot.Count==expected && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
            }
            foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
            Assert.That(flow.TrySpawn(),Is.True);
        }

        [UnityTest] public IEnumerator NoNewQueueSlotsReportsSpaceAndRetainsActualCustomer()
        {
            Assert.That(flow.TrySpawn(),Is.True); yield return Until(()=>flow.Snapshot.Single().State==CustomerVisitState.Queued);
            var before=flow.Snapshot.Single();
            head.position=Vector3.one*1000; flow.enabled=false; flow.enabled=true; flow.Step(0);
            Assert.That(flow.Status,Does.Contain("队伍可用站位不足（0/1）"));
            Assert.That(flow.Status,Does.Not.Contain("路径受阻"));
            Assert.That(flow.Snapshot.Single().VisitId,Is.EqualTo(before.VisitId));
            Assert.That(flow.Snapshot.Single().Position,Is.EqualTo(before.Position));
            Assert.That(flow.Capacity.GetCapacities()[0].Occupied,Is.EqualTo(1));
            Assert.That(flow.TrySpawn(),Is.False);
        }
        [UnityTest] public IEnumerator ShrinkDuringThirdEntryKeepsBlockedTailButAllowsSafeFrontExit()
        {
            for(var i=0;i<2;i++)
            { Assert.That(flow.TrySpawn(),Is.True); yield return Until(()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued)); }
            Assert.That(flow.TrySpawn(),Is.True); yield return null;
            Assert.That(flow.Snapshot[2].State,Is.EqualTo(CustomerVisitState.Entering));
            var ids=flow.Snapshot.Select(v=>v.VisitId).ToArray();
            flow.Capacity.UpdateFloorCellCount(16);
            Assert.That(flow.Capacity.Limits.CounterQueue,Is.EqualTo(2));
            root.transform.Find("floor").GetComponent<BoxCollider>().size=new Vector3(17,.2f,18);
            adapter.RefreshConfirmedLayout(); Assert.That(adapter.RebuildAndValidate().CanResume,Is.True); flow.Step(0);
            yield return Until(()=>flow.Snapshot[0].State==CustomerVisitState.Queued && flow.Snapshot[1].State==CustomerVisitState.Queued);
            Assert.That(flow.Snapshot[2].State,Is.EqualTo(CustomerVisitState.Blocked));
            Assert.That(flow.Capacity.GetCapacities()[0].Occupied,Is.EqualTo(3));
            Assert.That(flow.TrySpawn(),Is.False);
            Assert.That(flow.TryLetFrontLeave(),Is.True,"A stationary blocked tail must not lock a safe queued head");
            yield return Until(()=>flow.Snapshot.Count==2 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
            Assert.That(flow.Snapshot[0].VisitId,Is.EqualTo(ids[1]));
            for(var i=0;i<2;i++)
            {
                Assert.That(flow.TryLetFrontLeave(),Is.True);
                var remaining=1-i;
                yield return Until(()=>flow.Snapshot.Count==remaining && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
            }
            foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
        }
        [UnityTest] public IEnumerator SharedLaneWaitsForEntryButAdvancesAfterRealHeadDeparture()
        {
            Assert.That(flow.TrySpawn(),Is.True); yield return Until(()=>flow.Snapshot[0].State==CustomerVisitState.Queued);
            Assert.That(flow.TrySpawn(),Is.True);
            Assert.That(flow.TryLetFrontLeave(),Is.False,"Do not start opposite traffic while another visit is entering");
            Assert.That(flow.TrySpawn(),Is.False);
            yield return Until(()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued));
            Assert.That(flow.TryLetFrontLeave(),Is.True);
            yield return Until(()=>flow.Snapshot.Any(v=>v.State==CustomerVisitState.Advancing));
            Assert.That(flow.Snapshot[0].State,Is.EqualTo(CustomerVisitState.Exiting));
            Assert.That(flow.Capacity.GetCapacities()[0].Occupied,Is.EqualTo(2));
            Assert.That(flow.TryLetFrontLeave(),Is.False);
            Assert.That(flow.TrySpawn(),Is.False);
            yield return Until(()=>flow.Snapshot.Count==1 && flow.Snapshot[0].State==CustomerVisitState.Queued);
            Assert.That(flow.TryLetFrontLeave(),Is.True); yield return Until(()=>flow.Snapshot.Count==0);
            foreach(var ledger in flow.Capacity.GetCapacities()) Assert.That(ledger.Used,Is.Zero);
        }
        [UnityTest] public IEnumerator RealCornerQueueSeparatesDuringEntryForwardAndExit()
        {
            var wall=new GameObject("queue corner obstacle"); wall.transform.SetParent(root.transform);
            wall.transform.position=new Vector3(0,.7f,1.2f); var obstacle=wall.AddComponent<BoxCollider>(); obstacle.size=new Vector3(.4f,1.4f,.4f);
            adapter.Configure(null,null,null,root.transform.Find("floor").GetComponent<BoxCollider>(),new[]{obstacle},world,false);
            adapter.RefreshConfirmedLayout(); Assert.That(adapter.RebuildAndValidate().CanResume,Is.True); flow.Step(0);
            for(var i=0;i<3;i++)
            {
                Assert.That(flow.TrySpawn(),Is.True);
                yield return ObserveSweepsUntil(()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued),obstacle);
            }
            Assert.That(flow.Snapshot[1].Position.x,Is.LessThanOrEqualTo(-.999f),"Blocked forward chooses relative left with at least 1.0m spacing");
            for(var i=0;i<3;i++)
            {
                Assert.That(flow.TryLetFrontLeave(),Is.True); var count=2-i;
                yield return ObserveSweepsUntil(()=>flow.Snapshot.Count==count && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued),obstacle);
            }
        }
        [UnityTest] public IEnumerator LeftCornerFacesPreviousSlotOnEntryAndAfterAdvance()
        { yield return CornerFacesPreviousSlot(false); }

        [UnityTest] public IEnumerator RightCornerFacesPreviousSlotOnEntryAndAfterAdvance()
        { yield return CornerFacesPreviousSlot(true); }

        private IEnumerator CornerFacesPreviousSlot(bool turnRight)
        {
            // Literal geometry catches the old shared register-facing direction.
            var corner=new GameObject("facing corner"); corner.transform.SetParent(root.transform);
            corner.transform.position=new Vector3(0,.7f,1.2f);
            var obstacle=corner.AddComponent<BoxCollider>(); obstacle.size=new Vector3(.4f,1.4f,.4f);
            var solids=new System.Collections.Generic.List<Collider>{obstacle};
            if(turnRight)
            {
                var leftBlock=new GameObject("exclude left slot"); leftBlock.transform.SetParent(root.transform);
                leftBlock.transform.position=new Vector3(-1.1f,.7f,0);
                var box=leftBlock.AddComponent<BoxCollider>(); box.size=new Vector3(.4f,1.4f,.4f); solids.Add(box);
            }
            adapter.Configure(null,null,null,root.transform.Find("floor").GetComponent<BoxCollider>(),solids.ToArray(),world,false);
            adapter.RefreshConfirmedLayout(); Assert.That(adapter.RebuildAndValidate().CanResume,Is.True); flow.Step(0);
            if(turnRight) spawn.position=new Vector3(4,0,4);
            var side=turnRight?1f:-1f;
            var facing=turnRight?Vector3.left:Vector3.right;
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var queue=(CounterQueueService)typeof(CustomerFlowController).GetField("queue",flags).GetValue(flow);
            var slots=(Vector3[])typeof(CounterQueueService).GetField("slots",flags).GetValue(queue);
            Assert.That(slots.Length,Is.GreaterThanOrEqualTo(3));
            for(var j=1;j<3;j++)
            {
                Assert.That(Mathf.Abs(slots[j].z),Is.LessThan(.001f));
                Assert.That(slots[j].x*side,Is.GreaterThan(0),"Preserve the expected left/right turn");
                Assert.That(Vector3.Distance(slots[j-1],slots[j]),Is.InRange(.999f,1.251f));
            }
            for(var i=0;i<3;i++)
            {
                Assert.That(flow.TrySpawn(),Is.True,flow.Status);
                yield return ObserveSweepsUntil(()=>flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued),obstacle);
                AssertQueuePose(0,Vector3.zero,Vector3.back);
                for(var j=1;j<=i;j++) AssertQueuePose(j,slots[j],facing);
            }
            Assert.That(flow.TryLetFrontLeave(),Is.True);
            yield return ObserveSweepsUntil(()=>flow.Snapshot.Count==2 && flow.Snapshot.All(v=>v.State==CustomerVisitState.Queued),obstacle);
            AssertQueuePose(0,Vector3.zero,Vector3.back);
            AssertQueuePose(1,slots[1],facing);
        }
        private void AssertQueuePose(int index,Vector3 position,Vector3 facing)
        {
            var visit=flow.Snapshot[index];
            var actor=world.RegisteredActors.Single(a=>a.ActorId==visit.VisitId);

            Assert.That(Vector3.Angle(actor.transform.forward,facing),Is.LessThanOrEqualTo(5.1f),"Queue slot "+index+" must face the preceding slot (head faces register)");
            Assert.That(Vector3.Distance(actor.transform.position,position),Is.LessThanOrEqualTo(.081f));
        }
        private IEnumerator ObserveSweepsUntil(System.Func<bool> done,BoxCollider obstacle)
        {
            for(var frame=0;frame<3600 && !done();frame++)
            {
                var actors=world.RegisteredActors.ToArray(); var old=actors.Select(a=>a.transform.position).ToArray();
                yield return null;
                for(var i=0;i<actors.Length;i++)
                {
                    if(actors[i]==null) continue;
                    var current=actors[i].transform.position;
                    for(var sample=0;sample<=8;sample++)
                    {
                        var point=Vector3.Lerp(old[i],current,sample/8f); point.y=.7f;
                        Assert.That(Vector3.Distance(point,obstacle.ClosestPoint(point)),Is.GreaterThanOrEqualTo(.449f));
                    }
                    for(var j=0;j<i;j++)
                    {
                        if(actors[j]==null) continue;
                        var relative=old[i]-old[j]; var delta=(current-old[i])-(actors[j].transform.position-old[j]);
                        var t=delta.sqrMagnitude<1e-12f?0:Mathf.Clamp01(-Vector3.Dot(relative,delta)/delta.sqrMagnitude);
                        Assert.That((relative+delta*t).magnitude,Is.GreaterThanOrEqualTo(.899f));
                    }
                }
            }
            Assert.That(done(),Is.True,flow.Status);
        }

        [UnityTest] public IEnumerator WorldDisableKeepsVisitAndEnableRevalidatesActualPose()
        {
            Assert.That(flow.TrySpawn(),Is.True); yield return null;
            var actor=world.RegisteredActors[0]; var before=actor.transform.position;
            world.enabled=false; flow.Step(0); yield return null;
            Assert.That(flow.Snapshot.Count,Is.EqualTo(1));
            Assert.That(flow.Capacity.GetCapacities()[0].Occupied,Is.EqualTo(1));
            Assert.That(Vector3.Distance(before,actor.transform.position),Is.LessThan(.001f));
            world.enabled=true; flow.Step(0);
            yield return Until(()=>flow.Snapshot[0].State==CustomerVisitState.Queued);
        }
        [Test] public void FourRuntimeInstancesOfBothSpeciesRegisterTogether()
        {
            for(var i=0;i<4;i++)
            {
                var species=i<2?"Shiba":"Westie";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase11/Characters/"+species+"/PF_"+species+"_Navigation.prefab");
                var actor=Object.Instantiate(prefab,new Vector3(4+i%2*2,0,-2+i/2*2),Quaternion.identity,root.transform).GetComponent<NavigationActor>();
                Assert.That(actor.TryInitializeRuntimeId("four-clones-"+i),Is.True); Assert.That(world.Register(actor),Is.True);
            }
            Assert.That(world.RegisteredActors.Count,Is.EqualTo(4));
            Assert.That(world.RegisteredActors.Select(a=>a.ActorId).Distinct().Count(),Is.EqualTo(4));
        }

    }
}


#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.BusinessDay;
using AnimalCafe.CafeLoop;
using AnimalCafe.Capacity;
using AnimalCafe.Core.Time;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Navigation;
using AnimalCafe.Orders;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode.Phase15
{
    public sealed class BusinessDayIntegrationTests:BusinessDayTestSupport
    {
        private BusinessDayController StartDay(FakeClock clock)
        {
            RequirePolicy(); var day=loop.gameObject.AddComponent<BusinessDayController>();
            day.Initialize(loop,new BusinessDaySettings(10,5),clock.Read,true); return day;
        }
        private BusinessDayDrainResult Drain() => loop.GetDrainSnapshot();
        private bool AtStations(EmployeeFlowController employees,out string reason) => employees.AreEmployeesAtWorkStations(out reason);
        private T Field<T>(object owner,string name) => (T)owner.GetType()
            .GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(owner);
        private IEnumerator Until(Func<bool> done)
        {
            for(var frame=0;frame<5000 && !done();frame++) yield return null;
            Assert.That(done(),Is.True,"real service milestone timeout: "+loop.ViewSnapshot.Reception);
        }

        [UnityTest] public IEnumerator QueuedRuntimeCorePermissionPreventsDrain()
            => RuntimeCorePermissionPreventsDrain(false);
        [UnityTest] public IEnumerator PreparedRuntimeCorePermissionPreventsDrain()
            => RuntimeCorePermissionPreventsDrain(true);

        private IEnumerator RuntimeCorePermissionPreventsDrain(bool prepared)
        {
            // Component wiring with one public-Core model intent on actual Scene facts.
            // 只验证真实组件接线；不声称真实业务、Native运动、付款或driver预算链。
            var core=Field<ServiceTrafficCoordinator>(loop,"traffic");var employees=Find<EmployeeFlowController>();
            Assert.That(core,Is.Not.Null,"FIXTURE NOT_PROVEN: actual Cafe Loop traffic owner is required");
            // Share the original 5000 normal-frame window with Prepared readiness below.
            // 先等真实 idle 的清理；不退休员工、不清 Entry，也不额外驱动 Core。
            var readinessFrames=0;
            void IdleBusinessRemainsEmpty()
            {
                Assert.That(ReferenceEquals(Field<ServiceTrafficCoordinator>(loop,"traffic"),core),Is.True,"FIXTURE NOT_PROVEN: traffic owner changed");
                Assert.That(customers.AutoSpawn,Is.False,"FIXTURE NOT_PROVEN: manual component-model fixture requires AutoSpawn false");
                Assert.That(loop.CanProgress,Is.True,"FIXTURE NOT_PROVEN: normal runtime driver must remain available");
                Assert.That(employees.Snapshot.All(e=>e.State==EmployeeState.Idle),Is.True,"FIXTURE NOT_PROVEN: employees must remain genuinely Idle");
                Assert.That(customers.Snapshot,Is.Empty);Assert.That(customers.CounterAssignments,Is.Empty);Assert.That(customers.PickupAssignments,Is.Empty);
                Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                Assert.That(loop.Snapshot.Orders,Is.Empty);Assert.That(loop.Snapshot.Delivery,Is.Empty);
                Assert.That(Find<PickupCupDisplay>().VisibleOrders,Is.Empty);
            }
            LogDrainFixtureTraffic(core,"initial");
            while(readinessFrames<5000 && (core.HasPendingWork || !employees.AreEmployeesAtWorkStations(out _)))
            {
                IdleBusinessRemainsEmpty();
                yield return null;readinessFrames++;
            }
            if(core.HasPendingWork || !employees.AreEmployeesAtWorkStations(out _))LogDrainFixtureTraffic(core,"timeout");
            IdleBusinessRemainsEmpty();
            Assert.That(core.HasPendingWork,Is.False,"FIXTURE NOT_PROVEN: idle runtime must have no pending traffic first");
            Assert.That(Drain().IsDrained,Is.True,Drain().Reason);
            var snapshot=Field<TrafficWorldSnapshot>(core,"snapshot");
            var actor=world.RegisteredActors.Single(a=>a.ActorId==employees.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).ActorId);
            var body=snapshot.Bodies.Single(b=>ReferenceEquals(b.ActorLife,actor));
            Assert.That(body.Enabled && body.ActorActive,Is.True);
            var key=new TrafficIntentKey(body.Owner.Session,body.Owner.Epoch,actor.ActorId,"drain-component-model",1,
                body.Owner.LayoutRevision,TestContext.CurrentContext.Test.Name);
            var request=new TrafficRequest(key,Time.frameCount,snapshot,actor.transform.position,actor.transform.position,
                actor.Settings.AgentRadius,"drain-model/"+actor.ActorId,safetyMargin:actor.Settings.CollisionSkin+actor.Settings.Epsilon,
                planPrimaryRoute:true);
            void OtherDrainFactsRemainEmpty()
            {
                Assert.That(customers.Snapshot,Is.Empty);Assert.That(customers.CounterAssignments,Is.Empty);Assert.That(customers.PickupAssignments,Is.Empty);
                Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                Assert.That(loop.Snapshot.Orders,Is.Empty);Assert.That(loop.Snapshot.Delivery,Is.Empty);
                Assert.That(Find<PickupCupDisplay>().VisibleOrders,Is.Empty);
                Assert.That(employees.AreEmployeesAtWorkStations(out var reason),Is.True,reason);
                Assert.That(Field<System.Collections.IDictionary>(world.Service,"byActor").Contains(actor.ActorId),Is.False,
                    "MODEL SCOPE: no actual Native movement request is issued for this existing actor");
            }
            try
            {
                var decision=core.EvaluateMovement(request); // One legal public model submission, never a private Entry write.
                Assert.That(decision.Kind,Is.EqualTo(TrafficDecisionKind.DeferredByBudget),"FIXTURE NOT_PROVEN: actual initial ready-batch queue required");
                if(prepared)
                {
                    for(var frame=readinessFrames;frame<5000 && decision.Kind!=TrafficDecisionKind.Granted;frame++)
                    {
                        Assert.That(decision.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.None),"FIXTURE NOT_PROVEN: "+decision.Reason);
                        Assert.That(decision.Kind==TrafficDecisionKind.DeferredByBudget || decision.Kind==TrafficDecisionKind.WaitingForTraffic,Is.True);
                        yield return null;decision=core.GetDecision(key);
                    }
                    Assert.That(decision.Kind,Is.EqualTo(TrafficDecisionKind.Granted),"FIXTURE NOT_PROVEN: model permission did not reach actual Prepared");
                }
                Assert.That(decision.Intent,Is.EqualTo(key));Assert.That(decision.AcquiredSequence,Is.Zero);
                Assert.That(core.HasActiveGrant(key),Is.False);Assert.That(core.HasPendingWork,Is.True);
                OtherDrainFactsRemainEmpty();
                TestContext.WriteLine("DRAIN_COMPONENT_PUBLIC_CORE_MODEL stage="+(prepared?"Prepared":"Queued")+
                    " kind="+decision.Kind+" requestSequence="+decision.RequestSequence+" acquired="+decision.AcquiredSequence+
                    " pending="+core.HasPendingWork+" businessOwnersEmpty=True actualNativeIssued=False");
                Assert.That(Drain().IsDrained,Is.False,"PRODUCT: CafeLoopController.GetDrainSnapshot must include its actual Core pending work");
                Assert.That(Drain().Reason,Is.Not.Empty);
                var time=Find<GameTimeService>();time.SetPaused();
                Assert.That(Drain().IsDrained,Is.False,"PRODUCT: Scene Pause cannot erase an actual queued/prepared traffic permission");
                core.Retire(key); // Public retirement of this model permission only; no admission or business cancellation.
                var retired=core.GetDecision(key);
                Assert.That(retired.Kind,Is.EqualTo(TrafficDecisionKind.Cancelled));
                Assert.That(retired.FailureDisposition,Is.EqualTo(TrafficFailureDisposition.Terminal));
                Assert.That(retired.Reason,Is.EqualTo("intent-retired"));
                Assert.That(core.HasPendingWork,Is.False);OtherDrainFactsRemainEmpty();
                Assert.That(Drain().IsDrained,Is.True,"PRODUCT: idle Scene plus retained retired history must allow drain");
                Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
            }
            finally {core.Retire(key);}
        }

        private void LogDrainFixtureTraffic(ServiceTrafficCoordinator core,string stage)
        {
            // Read stored facts only: never replay a predicate, query, or mutate runtime state.
            // 只在初始/超时输出；字段不可读时保留 UNKNOWN，不替换原 assertion。
            object Read(object owner,string name)
            {
                if(owner==null)return null;
                try
                {
                    var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
                    var field=owner.GetType().GetField(name,flags);
                    if(field!=null)return field.GetValue(owner);
                    var property=owner.GetType().GetProperty(name,flags);
                    return property!=null?property.GetValue(owner):"UNKNOWN:missing-"+name;
                }
                catch(Exception error){return "UNKNOWN:"+error.GetType().Name;}
            }
            string Ref(object value)=>value==null?"null":value.GetType().Name+"#"+System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            string Facts(object value,params string[] names)=>string.Join(" ",names.Select(name=>name+"="+(Read(value,name)??"null")));
            string Path(object value)=>value is Vector3[] points?"["+string.Join(";",points.Select(point=>point.ToString("F9")))+"]":value==null?"null":"UNKNOWN";
            try
            {
                var entries=Read(core,"entries") as System.Collections.IDictionary;
                var work=(Read(core,"workEntries") as System.Collections.IEnumerable)?.Cast<object>().ToArray();
                var completing=Read(core,"completingHandoff");
                TestContext.WriteLine("DRAIN_FIXTURE_PENDING stage="+stage+" frame="+Time.frameCount+" core="+Ref(core)+
                    " pending="+core.HasPendingWork+" indexedCount="+(work==null?"UNKNOWN":work.Length.ToString())+
                    " entries="+(entries==null?"UNKNOWN":entries.Count.ToString())+" completing="+Ref(completing)+
                    " "+Facts(core,"closed","paused","session","epoch","layoutRevision"));
                if(entries==null){TestContext.WriteLine("DRAIN_FIXTURE_PENDING entries=UNKNOWN");return;}
                foreach(System.Collections.DictionaryEntry pair in entries)
                {
                    var entry=pair.Value;var decision=Read(entry,"Decision");var leg=Read(entry,"Leg");var handoff=Read(entry,"PendingHandoff");
                    TestContext.WriteLine("DRAIN_FIXTURE_PENDING stage="+stage+" entry="+Ref(entry)+
                        " key={"+Facts(pair.Key,"Session","Epoch","ActorId","IntentId","Generation","LayoutRevision","Context")+"}"+
                        " indexed="+(work==null?"UNKNOWN":work.Any(item=>ReferenceEquals(item,entry)).ToString())+
                        " completing="+ReferenceEquals(completing,entry)+" "+Facts(entry,"Queued","Prepared","Active","Retired","Discarded","FirstFrame","Queries","RequestSequence","AcquiredSequence")+
                        " decision={"+Facts(decision,"Kind","FailureDisposition","Reason","RequestSequence","AcquiredSequence")+"}"+
                        " handoff="+Ref(handoff)+" "+Facts(handoff,"Remaining")+
                        " journey="+Path(Read(entry,"Journey"))+" journeyPermit="+Ref(Read(entry,"JourneyPermit"))+
                        " leg="+Ref(leg)+" {"+Facts(leg,"Index","Generation","NeedsQuery","Committed","Bound","Completed","Suspended","ReprepareAllowed")+"}"+
                        " stamp={"+Facts(Read(leg,"Stamp"),"RequestId","LegGeneration")+"}"+
                        " path="+Path(Read(leg,"Path"))+" suffix="+Path(Read(leg,"Suffix")));
                }
            }
            catch(Exception error){TestContext.WriteLine("DRAIN_FIXTURE_PENDING stage="+stage+" status=UNKNOWN error="+error.GetType().Name);}
        }

        private void RotateMachine(bool confirm=true)
        {
            var decor=Find<DecorationModeController>();
            var machine=Find<CafeLayoutRuntime>().FunctionalSurfaceLayout.MountedInstances
                .Single(m=>m.DefinitionId=="equipment.coffee-machine.01");
            Assert.That(decor.TryBeginExistingFunctionalSurfacePreview(FunctionalSurfacePreviewKind.MountedEquipment,
                machine.InstanceId),Is.True);
            Assert.That(decor.TryRotateFunctionalSurfacePreview(),Is.True);
            if(confirm) Assert.That(decor.TryConfirmFunctionalSurfacePreview(),Is.True);
            else decor.CancelFunctionalSurfacePreview();
        }
        private void Preparing(long version) => typeof(CafeLoopController).GetMethod("OnChangedLayoutExitPreparing",
            BindingFlags.Instance|BindingFlags.NonPublic).Invoke(loop,new object[]{version});

        [UnityTest] public IEnumerator ConfirmedLayoutChangeRecordsInterruptionBeforeClear_Open()
            => ConfirmedLayoutChangeRecordsInterruptionBeforeClearBody(false);
        [UnityTest] public IEnumerator ConfirmedLayoutChangeRecordsInterruptionBeforeClear_Closing()
            => ConfirmedLayoutChangeRecordsInterruptionBeforeClearBody(true);
        private IEnumerator ConfirmedLayoutChangeRecordsInterruptionBeforeClearBody(bool closing)
        {
            var clock=new FakeClock(); var day=StartDay(clock); var epoch=loop.Snapshot.Epoch;
            yield return PrepareUnbornAdmission();
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            if(closing) { clock.Now=10; loop.Step(0); }
            var decor=Find<DecorationModeController>(); var time=Find<GameTimeService>();
            decor.EnterDecorationMode(); RotateMachine(); var observed=false;
            // Resume block会同步通知listener；此时旧epoch尚在，记账必须已先完成。
            void Observe()
            {
                if(loop.Snapshot.Epoch!=epoch) return;
                observed=true;
                Assert.That(day.Snapshot.LayoutInterruptionCount,Is.EqualTo(1));
                Assert.That(customers.Snapshot.Count,Is.EqualTo(1));
            }
            time.ResumeAvailabilityChanged+=Observe;
            try { decor.ExitDecorationMode(); }
            finally { time.ResumeAvailabilityChanged-=Observe; }
            Assert.That(observed,Is.True); Assert.That(loop.Snapshot.Epoch,Is.EqualTo(epoch+1));
            Assert.That(customers.Snapshot,Is.Empty);
            Assert.That(day.Snapshot.Phase,Is.EqualTo(closing?BusinessDayPhase.Closing:BusinessDayPhase.Open));
            Assert.That(day.Snapshot.LastClosedDay,Is.Null,"清场本身不能立即切Rest");
        }

        [Test] public void PreviewOrCancelDoesNotRecordInterruption()
        {
            var day=StartDay(new FakeClock()); var decor=Find<DecorationModeController>(); var epoch=loop.Snapshot.Epoch;
            decor.EnterDecorationMode(); RotateMachine(false); decor.ExitDecorationMode();
            decor.EnterDecorationMode(); decor.ExitDecorationMode();
            Assert.That(day.Snapshot.LayoutInterruptionCount,Is.Zero);
            Assert.That(loop.Snapshot.Epoch,Is.EqualTo(epoch));
        }

        [Test] public void RepeatedVersionIsIdempotentAndDoesNotPublishRecursiveSnapshot()
        {
            var day=StartDay(new FakeClock()); var decor=Find<DecorationModeController>(); var publications=0;
            day.SnapshotChanged+=_=>{ publications++; day.RecordLayoutInterruption(1); Preparing(1); };
            decor.EnterDecorationMode(); RotateMachine(); decor.ExitDecorationMode();
            var epoch=loop.Snapshot.Epoch; Preparing(1); Preparing(1);
            day.RecordLayoutInterruption(1); day.RecordLayoutInterruption(1);
            Assert.That(day.Snapshot.LayoutInterruptionCount,Is.EqualTo(1));
            Assert.That(publications,Is.Zero,"事实记录不自行发布Snapshot；唯一driver负责观察");
            loop.Step(0);
            Assert.That(publications,Is.InRange(1,2));
            Assert.That(loop.Snapshot.Epoch,Is.EqualTo(epoch));
            Assert.That(day.Snapshot.LayoutInterruptionCount,Is.EqualTo(1));
        }

        [Test] public void CleanupFailurePreventsRestAndKeepsInterruption()
        {
            var clock=new FakeClock(); var day=StartDay(clock); var decor=Find<DecorationModeController>();
            decor.EnterDecorationMode(); RotateMachine();
            var field=typeof(CafeLoopController).GetField("cups",BindingFlags.Instance|BindingFlags.NonPublic);
            var cups=field.GetValue(loop); field.SetValue(loop,null);
            try { decor.ExitDecorationMode(); }
            finally { field.SetValue(loop,cups); }
            clock.Now=100; loop.Step(0); loop.ResumeBlockedFlow(); loop.Step(0);
            Assert.That(day.Snapshot.LayoutInterruptionCount,Is.EqualTo(1));
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(day.Snapshot.LastClosedDay,Is.Null);
            Assert.That(Drain().IsDrained,Is.False); Assert.That(loop.Snapshot.Failure,Does.Contain("清场失败"));
        }

        [UnityTest] public IEnumerator NextDayRetainsLastInterruptedClose()
        {
            var clock=new FakeClock(); var day=StartDay(clock); var decor=Find<DecorationModeController>();
            decor.EnterDecorationMode(); RotateMachine(); decor.ExitDecorationMode();
            clock.Now=10; loop.Step(0);
            yield return Until(()=>Drain().IsDrained); loop.Step(0);
            var closed=day.Snapshot.LastClosedDay;
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Rest));
            Assert.That(closed.EndKind,Is.EqualTo(BusinessDayEndKind.LayoutInterrupted));
            Assert.That(closed.LayoutInterruptionCount,Is.EqualTo(1));
            clock.Now=15; loop.Step(0); loop.Step(0);
            Assert.That(day.Snapshot.DayNumber,Is.EqualTo(2));
            Assert.That(day.Snapshot.LayoutInterruptionCount,Is.Zero);
            Assert.That(day.Snapshot.LastClosedDay,Is.SameAs(closed));
        }

        [UnityTest] public IEnumerator DisabledProviderRecordsCanonicalLayoutThenResumesSameClock()
        {
            var clock=new FakeClock(); var day=StartDay(clock); day.enabled=false;
            var decor=Find<DecorationModeController>(); decor.EnterDecorationMode(); RotateMachine(); decor.ExitDecorationMode();
            Assert.That(day.Snapshot.LayoutInterruptionCount,Is.EqualTo(1));
            clock.Now=10; loop.Step(0);
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Open),"disabled不能成为第二个driver");
            AssertRejectedWithoutAllocation();
            day.enabled=true; loop.Step(0); yield return Until(()=>Drain().IsDrained); loop.Step(0);
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Rest));
            Assert.That(day.Snapshot.DayNumber,Is.EqualTo(1));
            Assert.That(day.Snapshot.RemainingSeconds,Is.EqualTo(5));
            Assert.That(day.Snapshot.LastClosedDay.EndKind,Is.EqualTo(BusinessDayEndKind.LayoutInterrupted));
        }

        [TestCase(false)][TestCase(true)] public void MissingRequiredRecorderRejectsCleanupWithoutNormalClose(bool destroyed)
        {
            RequirePolicy();
            if(destroyed) UnityEngine.Object.DestroyImmediate(StartDay(new FakeClock()));
            var epoch=loop.Snapshot.Epoch; var decor=Find<DecorationModeController>();
            decor.EnterDecorationMode(); RotateMachine(); decor.ExitDecorationMode();
            Assert.That(loop.Snapshot.Epoch,Is.EqualTo(epoch),"无法记账时不得退休旧业务");
            Assert.That(loop.Snapshot.Failure,Does.Contain("recorder"));
            Assert.That(Drain().IsDrained,Is.False); AssertRejectedWithoutAllocation();
            var clock=new FakeClock(); var day=StartDay(clock); clock.Now=100; loop.Step(0);
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(day.Snapshot.LastClosedDay,Is.Null,"后来出现provider也不能把未知清场当正常完成");
        }

        [Test] public void StaleRecorderDestroyCannotRemoveNewOwner()
        {
            var old=StartDay(new FakeClock()); old.enabled=false;
            var clock=new FakeClock(); var current=StartDay(clock); UnityEngine.Object.DestroyImmediate(old);
            Assert.That(current.CanAdmitNow(),Is.True);
            var decor=Find<DecorationModeController>(); decor.EnterDecorationMode(); RotateMachine(); decor.ExitDecorationMode();
            Assert.That(current.Snapshot.LayoutInterruptionCount,Is.EqualTo(1));
            Assert.That(loop.Snapshot.Failure,Is.Empty);
        }

        [UnityTest] public IEnumerator RestDecorationDoesNotRewriteClosedResultOrPrecountNextDay()
        {
            var clock=new FakeClock(); var day=StartDay(clock); clock.Now=10; loop.Step(0);
            var closed=day.Snapshot.LastClosedDay; var decor=Find<DecorationModeController>();
            decor.EnterDecorationMode(); RotateMachine(); decor.ExitDecorationMode();
            Assert.That(day.Snapshot.LayoutInterruptionCount,Is.Zero);
            Assert.That(day.Snapshot.LastClosedDay,Is.SameAs(closed));
            Assert.That(closed.EndKind,Is.EqualTo(BusinessDayEndKind.CompletedDrain));
            yield return Until(()=>Drain().IsDrained); clock.Now=15; loop.Step(0); Preparing(1);
            Assert.That(day.Snapshot.DayNumber,Is.EqualTo(2));
            Assert.That(day.Snapshot.LayoutInterruptionCount,Is.Zero);
        }

        [UnityTest] public IEnumerator TwoAutomaticCyclesKeepEmployeesAndHistoricalPaidFacts()
        {
            var clock=new FakeClock(); var day=StartDay(clock);
            var bodies=world.RegisteredActors.ToArray(); var ids=Find<EmployeeFlowController>().Snapshot.Select(e=>e.ActorId).ToArray();
            for(var cycle=0;cycle<2;cycle++)
            {
                yield return PrepareUnbornAdmission();
                Assert.That(customers.TrySpawn(),Is.True,customers.Status);
                clock.Now=cycle==0?10:25;
                yield return Until(()=>customers.Snapshot.Count==0 && loop.Snapshot.Orders.Count==cycle+1 && Drain().IsDrained);
                loop.Step(0); Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Rest));
                Assert.That(day.Snapshot.LastClosedDay.DayNumber,Is.EqualTo(cycle+1));
                Assert.That(loop.Snapshot.PaidVisitIds.Count,Is.EqualTo(cycle+1));
                Assert.That(loop.Snapshot.Orders.Values.All(o=>o.State==OrderState.Completed),Is.True);
                CollectionAssert.AreEquivalent(bodies,world.RegisteredActors);
                CollectionAssert.AreEquivalent(ids,Find<EmployeeFlowController>().Snapshot.Select(e=>e.ActorId));
                clock.Now+=5; loop.Step(0); loop.Step(0);
                Assert.That(day.Snapshot.DayNumber,Is.EqualTo(cycle+2));
                Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Open));
                Assert.That(loop.Snapshot.PaidVisitIds.Count,Is.EqualTo(cycle+1),"Rest不能虚构新接待或收益");
            }
        }

        [UnityTest] public IEnumerator FastPauseAndDecorationDoNotStopRealDeadlineOrAdvancePausedWork()
        {
            var clock=new FakeClock(); var day=StartDay(clock); var time=Find<GameTimeService>();
            time.TrySetSpeed(GameSpeed.Fast); yield return PrepareUnbornAdmission();
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            yield return Until(()=>Find<EmployeeFlowController>().Snapshot.Any(e=>e.Role==EmployeeRole.Cashier && e.State==EmployeeState.Working));
            time.SetPaused(); var employees=Find<EmployeeFlowController>();
            Assert.That(employees.TryCaptureOrderCheckpoint(EmployeeRole.Cashier,out var before),Is.True);
            var poses=world.RegisteredActors.Select(a=>a.transform.position).ToArray();
            var decor=Find<DecorationModeController>(); decor.EnterDecorationMode(); clock.Now=10;
            for(var frame=0;frame<5;frame++) yield return null;
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(day.Snapshot.LastClosedDay,Is.Null); Assert.That(loop.Snapshot.PaidVisitIds,Is.Empty);
            Assert.That(employees.TryCaptureOrderCheckpoint(EmployeeRole.Cashier,out var after),Is.True);
            Assert.That(after,Is.EqualTo(before));
            CollectionAssert.AreEqual(poses,world.RegisteredActors.Select(a=>a.transform.position));
            decor.ExitDecorationMode(); Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
        }

        [Test] public void PausedEmptyCafeCanRestWithoutUnpausing()
        {
            var clock=new FakeClock(); var day=StartDay(clock); var time=Find<GameTimeService>();
            time.SetPaused(); clock.Now=10; loop.Step(0);
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Rest));
            Assert.That(day.Snapshot.LastClosedDay.EndKind,Is.EqualTo(BusinessDayEndKind.CompletedDrain));
            Assert.That(time.CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
            Assert.That(Drain().IsDrained,Is.True);
        }

        [TestCase(false)][TestCase(true)] public void RestExpiryCannotBypassPauseOrUnavailableLayout(bool unavailableLayout)
        {
            var clock=new FakeClock(); var day=StartDay(clock); clock.Now=10; loop.Step(0);
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Rest));
            if(unavailableLayout) Find<NavigationLayoutAdapter>().enabled=false;
            else Find<GameTimeService>().SetPaused();
            clock.Now=15; loop.Step(0);
            Assert.That(day.Snapshot.DayNumber,Is.EqualTo(2));
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Open),"real clock continues while Scene remains gated");
            Assert.That(loop.CanProgress,Is.False); AssertRejectedWithoutAllocation();
            if(!unavailableLayout) Assert.That(Find<GameTimeService>().CurrentSpeed,Is.EqualTo(GameSpeed.Paused));
        }

        [Test] public void VisibleCupIsAnAdditionalCheckWhenLogicalDeliveryIsEmpty()
        {
            Assert.That(Find<EmployeeFlowController>().TryGetBinding(EmployeeRole.Barista,out var binding),Is.True);
            var key=new CafeOrderKey(loop.Snapshot.SessionId,loop.Snapshot.Epoch,1);
            var display=Find<PickupCupDisplay>();
            Assert.That(display.TryProject(new[]{new CafeDeliverySnapshot(key,"visual-orphan",binding.CupAddress.Value,1)},out var reason),Is.True,reason);
            Assert.That(loop.Snapshot.Delivery,Is.Empty); Assert.That(customers.Snapshot,Is.Empty);
            var cup=display.VisibleOrders.Single().Value;
            Assert.That(Drain().IsDrained,Is.False); Assert.That(Drain().Reason,Does.Contain("可见桌杯"));
            Assert.That(display.VisibleOrders.Single().Value,Is.SameAs(cup),"query cannot clean presentation");
        }

        [UnityTest] public IEnumerator CompletedCustomerStillExitingPreventsRest()
        {
            var clock=new FakeClock(); var day=StartDay(clock);
            yield return PrepareUnbornAdmission();
            Assert.That(customers.TrySpawn(),Is.True,customers.Status); clock.Now=10;
            yield return Until(()=>loop.Snapshot.Orders.Values.Any(o=>o.State==OrderState.Completed));
            Assert.That(customers.Snapshot.Count,Is.EqualTo(1),"Completed is before actual exit");
            Assert.That(customers.Capacity.GetCapacities().Single(c=>c.Kind==CapacityKind.TotalCustomers).Used,Is.EqualTo(1));
            Assert.That(loop.Snapshot.CollectedExitVisitIds.Count,Is.EqualTo(1));
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(Drain().Reason,Does.Contain("顾客"));
        }

        [UnityTest] public IEnumerator ReturningBaristaPreventsRest()
        {
            var clock=new FakeClock(); var day=StartDay(clock); var employees=Find<EmployeeFlowController>();
            yield return PrepareUnbornAdmission();
            Assert.That(customers.TrySpawn(),Is.True,customers.Status); clock.Now=10;
            yield return Until(()=>employees.Snapshot.Any(e=>e.Role==EmployeeRole.Barista && e.State==EmployeeState.Returning));
            Assert.That(employees.Snapshot.Single(e=>e.Role==EmployeeRole.Barista).HandleId,Is.GreaterThan(0));
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(AtStations(employees,out var reason),Is.False);
            Assert.That(reason,Does.Contain("Barista"));
        }

        [TestCase(EmployeeRole.Cashier,"position")][TestCase(EmployeeRole.Barista,"position")]
        [TestCase(EmployeeRole.Cashier,"facing")][TestCase(EmployeeRole.Barista,"facing")]
        [TestCase(EmployeeRole.Cashier,"speed")][TestCase(EmployeeRole.Barista,"speed")]
        [TestCase(EmployeeRole.Cashier,"disabled")][TestCase(EmployeeRole.Barista,"disabled")]
        public void IdleLabelCannotReplaceRealStationFacts(EmployeeRole role,string fault)
        {
            var employees=Find<EmployeeFlowController>();
            Assert.That(AtStations(employees,out var readyReason),Is.True,readyReason);
            var state=employees.Snapshot.Single(e=>e.Role==role);
            var actor=world.RegisteredActors.Single(a=>a.ActorId==state.ActorId);
            Assert.That(employees.TryGetBinding(role,out var binding),Is.True);
            if(fault=="position") actor.transform.position=binding.WorkTarget.Position+Vector3.right*(actor.Settings.ArrivalDistance+.01f);
            if(fault=="facing") actor.transform.rotation=Quaternion.LookRotation(-binding.WorkTarget.Facing.Value);
            if(fault=="speed") actor.GetComponent<NavigationWalkPresenter>().SetMotion(actor.Settings.ArrivalSpeed+.01f);
            if(fault=="disabled") actor.enabled=false;
            var position=actor.transform.position; var rotation=actor.transform.rotation;
            Assert.That(AtStations(employees,out var reason),Is.False); Assert.That(reason,Does.Contain(role.ToString()));
            Assert.That(Drain().IsDrained,Is.False);
            Assert.That(actor.transform.position,Is.EqualTo(position)); Assert.That(actor.transform.rotation,Is.EqualTo(rotation));
            Assert.That(employees.Snapshot.Single(e=>e.Role==role).HandleId,Is.EqualTo(state.HandleId),"query cannot dispatch movement");
        }

        [TestCase("held")][TestCase("layoutPending")][TestCase("layoutFailed")]
        public void UnstableLifecycleCannotDrain(string field)
        {
            typeof(CafeLoopController).GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(loop,true);
            Assert.That(Drain().IsDrained,Is.False); Assert.That(Drain().Reason,Is.Not.Empty);
        }

        [UnityTest] public IEnumerator LogicalDeliveryStillPreventsDrainWhenVisibleCupIsMissing()
        {
            loop.HoldCollection=true;
            yield return PrepareUnbornAdmission();
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            yield return Until(()=>loop.Snapshot.Delivery.Count==1);
            Find<PickupCupDisplay>().Clear();
            Assert.That(Find<PickupCupDisplay>().VisibleOrders,Is.Empty);
            Assert.That(loop.Snapshot.Delivery.Count,Is.EqualTo(1));
            Assert.That(Drain().IsDrained,Is.False);
            Assert.That(Drain().Reason,Does.Contain("逻辑Delivery"));
            Assert.That(loop.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.ReadyForPickup));
        }

        [TestCase(CapacityKind.TotalCustomers,false)][TestCase(CapacityKind.CounterQueue,false)][TestCase(CapacityKind.PickUp,false)]
        [TestCase(CapacityKind.TotalCustomers,true)][TestCase(CapacityKind.CounterQueue,true)][TestCase(CapacityKind.PickUp,true)]
        public void ReservationOrCallbackPreventsRest(CapacityKind kind,bool occupied)
        {
            var reservation=customers.Capacity.TryReserve("pending",new[]{kind}); Assert.That(reservation.Succeeded,Is.True);
            if(occupied) Assert.That(customers.Capacity.Occupy(reservation.Reservations.Single().Token,"pending").Succeeded,Is.True);
            Assert.That(customers.Snapshot,Is.Empty); var clock=new FakeClock(); var day=StartDay(clock);
            clock.Now=10; loop.Step(0);
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(Drain().Reason,Does.Contain("容量"));
        }

        [TestCase(false)][TestCase(true)] public void LeftoverAssignmentCannotBeHiddenByEmptyVisits(bool pickup)
        {
            if(pickup)
            {
                var queue=new PickupQueueService(new[]{Vector3.zero},Vector3.right,true);
                Assert.That(queue.TryReserve("orphan",out _),Is.True);
                Assert.That(queue.TryBeginTransfer("orphan",out _),Is.True);
                typeof(CustomerFlowController).GetField("pickupQueue",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(customers,queue);
                Assert.That(customers.PickupAssignments.Count,Is.EqualTo(1));
            }
            else
            {
                Assert.That(Field<CounterQueueService>(customers,"queue").TryJoin("orphan"),Is.True);
                Assert.That(customers.CounterAssignments.Count,Is.EqualTo(1));
            }
            Assert.That(customers.Snapshot,Is.Empty);
            Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
            Assert.That(Drain().Reason,Does.Contain("队伍"));
        }

        [Test] public void PendingCallbackWithoutVisibleBusinessPreventsRest()
        {
            var events=Field<Queue<CafeTaskEvent>>(Field<CafeLoopService>(loop,"service"),"events"); events.Enqueue(default);
            var clock=new FakeClock(); var day=StartDay(clock); Find<GameTimeService>().SetPaused(); clock.Now=10;
            loop.Step(0);
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(Drain().Reason,Does.Contain("callback")); Assert.That(events.Count,Is.EqualTo(1));
        }

        [TestCase(EmployeeRole.Cashier,false)][TestCase(EmployeeRole.Barista,false)]
        [TestCase(EmployeeRole.Cashier,true)][TestCase(EmployeeRole.Barista,true)]
        public void MissingBindingOrBlockedEmployeeCannotCountAsAtStation(EmployeeRole role,bool blocked)
        {
            var employees=Find<EmployeeFlowController>();
            var employee=Field<IList>(employees,"employees").Cast<object>().Single(value=>Field<EmployeeRole>(value,"Role")==role);
            if(blocked) typeof(EmployeeTaskRunner).GetMethod("RejectBinding",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(Field<EmployeeTaskRunner>(employee,"Runner"),new object[]{"test blocked"});
            else employee.GetType().GetField("Binding",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(employee,null);
            Assert.That(AtStations(employees,out var reason),Is.False); Assert.That(reason,Does.Contain(role.ToString()));
            Assert.That(Drain().IsDrained,Is.False);
        }

        [UnityTest] public IEnumerator DisabledLiveCustomerStillOwnsBodyAndCapacity()
        {
            var clock=new FakeClock(); var day=StartDay(clock); yield return PrepareUnbornAdmission();
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            var actor=world.RegisteredActors.Single(a=>a.ActorId==customers.Snapshot.Single().VisitId);
            actor.enabled=false; clock.Now=10; yield return null;
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(customers.Snapshot.Count,Is.EqualTo(1));
            Assert.That(customers.Capacity.GetCapacities().Single(c=>c.Kind==CapacityKind.TotalCustomers).Used,Is.EqualTo(1));
            Assert.That(Drain().Reason,Does.Contain("顾客"));
        }

        [UnityTest] public IEnumerator ExplicitRecoveryCompletesOriginalVisitThenStartsFullRestAtActualDrain()
        {
            var service=Field<CafeLoopService>(loop,"service");
            var create=typeof(CafeLoopService).GetField("createOrder",BindingFlags.Instance|BindingFlags.NonPublic);
            var original=create.GetValue(service); var rejected=0;
            create.SetValue(service,new Func<OrderService,string,string,OrderResult>((orders,visit,product)=>
            { rejected++; return orders.Create("",product); }));
            var clock=new FakeClock(); var day=StartDay(clock); yield return PrepareUnbornAdmission();
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            var visit=customers.Snapshot.Single().VisitId; clock.Now=10;
            yield return Until(()=>rejected==1);
            clock.Now=100; for(var frame=0;frame<5;frame++) yield return null;
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(day.Snapshot.LastClosedDay,Is.Null); Assert.That(rejected,Is.EqualTo(1),"no automatic retry");
            Assert.That(service.Snapshot.Orders,Is.Empty);
            create.SetValue(service,original); loop.ResumeBlockedFlow();
            yield return Until(()=>Drain().IsDrained); loop.Step(0);
            Assert.That(service.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.Completed));
            Assert.That(service.Snapshot.Orders.Values.Single().CustomerId,Is.EqualTo(visit));
            Assert.That(service.Snapshot.PaidVisitIds,Is.EqualTo(new[]{visit}));
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Rest));
            Assert.That(day.Snapshot.RemainingSeconds,Is.EqualTo(5));
            clock.Now=104; loop.Step(0); Assert.That(day.Snapshot.RemainingSeconds,Is.EqualTo(1));
        }

        [Test] public void MissingLedgerIsNotEmpty()
        {
            var capacity=typeof(AnimalCafe.Customers.CustomerFlowController).GetProperty("Capacity");
            var ledger=customers.Capacity;
            try
            {
                capacity.SetValue(customers,null);
                Assert.That(Drain().IsDrained,Is.False); Assert.That(Drain().Reason,Does.Contain("ledger"));
            }
            finally { capacity.SetValue(customers,ledger); }
        }

        [UnityTest] public IEnumerator HistoricalCompletedOrdersAllowRest()
        {
            var clock=new FakeClock(); var day=StartDay(clock);
            yield return PrepareUnbornAdmission();
            Assert.That(customers.TrySpawn(),Is.True,customers.Status); clock.Now=10;
            yield return Until(()=>customers.Snapshot.Count==0 && loop.Snapshot.Orders.Count==1 &&
                Find<EmployeeFlowController>().Snapshot.All(e=>e.State==EmployeeState.Idle));
            loop.Step(0);
            Assert.That(loop.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.Completed));
            Assert.That(loop.Snapshot.PaidVisitIds.Count,Is.EqualTo(1));
            Assert.That(customers.CounterAssignments,Is.Empty); Assert.That(customers.PickupAssignments,Is.Empty);
            Assert.That(customers.Capacity.GetCapacities().Select(c=>c.Used),Is.EqualTo(new[]{0,0,0}));
            Assert.That(loop.Snapshot.Delivery,Is.Empty); Assert.That(Find<PickupCupDisplay>().VisibleOrders,Is.Empty);
            Assert.That(loop.Snapshot.CollectedExitVisitIds,Is.Empty);
            Assert.That(Drain().IsDrained,Is.True,Drain().Reason);
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Rest));
            Assert.That(day.Snapshot.RemainingSeconds,Is.EqualTo(5));
        }
    }
}
#endif

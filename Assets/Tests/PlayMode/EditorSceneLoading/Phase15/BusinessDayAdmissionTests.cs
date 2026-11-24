#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using AnimalCafe.BusinessDay;
using AnimalCafe.Core.Time;
using AnimalCafe.Orders;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace AnimalCafe.Tests.PlayMode.Phase15
{
    public sealed class BusinessDayAdmissionTests:BusinessDayTestSupport
    {
        private BusinessDayController StartDay(FakeClock clock)
        {
            RequirePolicy();
            var day=loop.gameObject.AddComponent<BusinessDayController>();
            day.Initialize(loop,new BusinessDaySettings(10,5),clock.Read,true);
            return day;
        }

        [Test] public void ExpiredDeadlineRejectsDirectSpawnBeforeDriver()
        {
            var clock=new FakeClock(); var day=StartDay(clock);
            Assert.That(day.CanAdmitNow(),Is.True);
            clock.Now=10; // 尚未yield/Step，旧driver缓存仍是允许接待。
            AssertRejectedWithoutAllocation();
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Open));
        }

        [Test] public void MissingProviderAtLoadFailsClosedWhenRequired()
        {
            RequirePolicy();
            AssertRejectedWithoutAllocation();
            Assert.That(customers.Status,Does.Contain("policy"));
        }

        [UnityTest] public IEnumerator LegacySceneWithoutRequirementStillAdmits()
        {
            RequirePolicy(false);
            yield return PrepareUnbornAdmission();
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            Assert.That(customers.Snapshot.Count,Is.EqualTo(1));
        }

        [UnityTest] public IEnumerator LegacyLoopDisableResumeStillAdmits()
        {
            loop.enabled=false; loop.enabled=true; loop.ResumeBlockedFlow(); loop.Step(0);
            yield return PrepareUnbornAdmission();
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
        }

        [Test] public void MissingPolicyFailsClosedBeforeIntegratedModeEstablished()
        {
            RequirePolicy(); customers.SetIntegratedMode(false);
            AssertRejectedWithoutAllocation();
        }

        [Test] public void DisabledOrDestroyedProviderCannotAdmit()
        {
            var clock=new FakeClock(); var day=StartDay(clock);
            day.enabled=false; AssertRejectedWithoutAllocation();
            day.enabled=true;
            Assert.That(day.CanAdmitNow(),Is.True,"Enable must preserve and restore the same day");
            Object.DestroyImmediate(day);
            AssertRejectedWithoutAllocation();
        }

        [UnityTest] public IEnumerator StaleOwnerUnregisterCannotRemoveCurrentPolicy()
        {
            RequirePolicy();
            var stale=loop.gameObject.AddComponent<BusinessDayController>();
            customers.RegisterAdmissionPolicy(stale,()=>true);
            customers.UnregisterAdmissionPolicy(stale);
            var current=loop.gameObject.AddComponent<BusinessDayController>();
            customers.RegisterAdmissionPolicy(current,()=>false);
            customers.UnregisterAdmissionPolicy(stale);
            AssertRejectedWithoutAllocation();
            customers.UnregisterAdmissionPolicy(current);
            customers.RegisterAdmissionPolicy(stale,()=>true);
            yield return PrepareUnbornAdmission();
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
        }

        [UnityTest] public IEnumerator ActiveOwnerCompetitionIsRejectedAndSameOwnerRegistrationIsIdempotent()
        {
            var clock=new FakeClock(); var day=StartDay(clock);
            customers.RegisterAdmissionPolicy(day,()=>false);
            var other=loop.gameObject.AddComponent<BusinessDayController>();
            Assert.Throws<InvalidOperationException>(()=>customers.RegisterAdmissionPolicy(other,()=>false));
            yield return PrepareUnbornAdmission();
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
        }

        [Test] public void ThrowingPolicyRejectsBeforeAllocatingAndExplainsFailure()
        {
            RequirePolicy();
            customers.RegisterAdmissionPolicy(loop,()=>throw new InvalidOperationException("clock offline"));
            AssertRejectedWithoutAllocation();
            Assert.That(customers.Status,Does.Contain("clock offline"));
        }

        [Test] public void DisabledOwnerReplacementAndStaleUnregisterPreserveCurrentPolicy()
        {
            var stale=loop.gameObject.AddComponent<BusinessDayController>();
            customers.RegisterAdmissionPolicy(stale,()=>true); stale.enabled=false;
            var current=loop.gameObject.AddComponent<BusinessDayController>();
            customers.RegisterAdmissionPolicy(current,()=>false);
            customers.UnregisterAdmissionPolicy(stale);
            AssertRejectedWithoutAllocation();
        }

        [Test] public void UniqueDriverObservesDeadlineWhileScenePausedAndUsesRealEmptyDrain()
        {
            var clock=new FakeClock(); var day=StartDay(clock);
            Find<GameTimeService>().SetPaused(); clock.Now=10;
            loop.Step(0);
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Rest));
            Assert.That(day.Snapshot.LastClosedDay.DayNumber,Is.EqualTo(1));
            Assert.That(day.Snapshot.LastClosedDay.EndKind,Is.EqualTo(BusinessDayEndKind.CompletedDrain));
            Assert.That(day.Snapshot.RemainingSeconds,Is.EqualTo(5));
            AssertRejectedWithoutAllocation();
        }

        [Test] public void LayoutPendingEarlyReturnStillObservesDeadlineAndReportsNotReady()
        {
            var clock=new FakeClock(); var day=StartDay(clock);
            typeof(AnimalCafe.CafeLoop.CafeLoopController).GetField("layoutPending",
                System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(loop,true);
            clock.Now=10; loop.Step(0);
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Closing));
            Assert.That(day.Snapshot.DrainReason,Does.Contain("装修"));
            Assert.That(day.Snapshot.LastClosedDay,Is.Null);
        }

        [Test] public void ReinitializeAndReenablePreserveDeadlineAndDay()
        {
            var clock=new FakeClock(); var day=StartDay(clock);
            clock.Now=9; day.Initialize(loop,new BusinessDaySettings(100,100),()=>0,true);
            day.enabled=false; day.enabled=true; clock.Now=10;
            AssertRejectedWithoutAllocation(); loop.Step(0);
            Assert.That(day.Snapshot.DayNumber,Is.EqualTo(1));
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Rest));
            Assert.That(day.Snapshot.LastClosedDay.DayNumber,Is.EqualTo(1));
            Assert.That(day.Snapshot.RemainingSeconds,Is.EqualTo(5),"reinitialize cannot replace original duration");
        }

        [UnityTest] public IEnumerator ControllerHasNoIndependentDriver()
        {
            var clock=new FakeClock(); var day=StartDay(clock);
            var ticks=0; day.SnapshotChanged+=_=>ticks++;
            clock.Now=10;
            // Disable loop revokes its registration; the day component stays enabled.
            loop.enabled=false; yield return null; yield return null;
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Open));
            Assert.That(ticks,Is.Zero);
            AssertRejectedWithoutAllocation();
        }

        [Test] public void SnapshotListenerCannotReenterAndDriveTwice()
        {
            var clock=new FakeClock(); var day=StartDay(clock); var events=0;
            day.SnapshotChanged+=_=>{ events++; loop.Step(0); };
            clock.Now=10; loop.Step(0);
            Assert.That(day.Snapshot.TransitionSequence,Is.EqualTo(2));
            Assert.That(events,Is.InRange(1,2));
        }


        [UnityTest]
        public IEnumerator StoppedAutomaticAdmissionReleasesPreparationAndManualAdmissionStillWorks()
        {
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var pending=typeof(AnimalCafe.Customers.CustomerFlowController).GetField("pendingAdmissionAnchor",flags);
            var clockField=typeof(AnimalCafe.Customers.CustomerFlowController).GetField("clock",flags);
            var originalClock=clockField.GetValue(customers);
            var coordinator=AnimalCafe.Tests.PlayMode.Phase14.CafeLoopRuntimeProof.Field<AnimalCafe.Navigation.ServiceTrafficCoordinator>(loop,"traffic");
            try
            {
                clockField.SetValue(customers,new AnimalCafe.Customers.CustomerSpawnClock(()=>0));
                customers.AutoSpawn=true;
                loop.Step(.001f);
                var automatic=pending.GetValue(customers);
                Assert.That(automatic,Is.Not.Null,"FIXTURE: original automatic tick must submit the unborn request");
                var key=AnimalCafe.Tests.PlayMode.Phase14.CafeLoopRuntimeProof.Field<AnimalCafe.Navigation.TrafficIntentKey>(automatic,"Key");
                Assert.That(coordinator.GetDecision(key).AcquiredSequence,Is.Zero);
                Assert.That(customers.Snapshot,Is.Empty);
                Assert.That(customers.Capacity.GetReservations(),Is.Empty);

                customers.AutoSpawn=false;
                Assert.That(pending.GetValue(customers),Is.Null,"Stopped automatic arrivals must release the unborn parking proposal");
                Assert.That(coordinator.GetDecision(key).FailureDisposition,Is.EqualTo(AnimalCafe.Navigation.TrafficFailureDisposition.Terminal));
                loop.Step(0);
                Assert.That(coordinator.HasActiveGrant(key),Is.False);
                Assert.That(customers.Snapshot,Is.Empty);
                Assert.That(customers.Capacity.GetReservations(),Is.Empty);


                var observer=typeof(AnimalCafe.Customers.CustomerFlowController).GetField("AdmissionPreparationObserver",flags);
                var previousObserver=observer.GetValue(customers);
                AnimalCafe.Navigation.TrafficIntentKey? interrupted=null;
                try
                {
                    observer.SetValue(customers,(Action<AnimalCafe.Navigation.TrafficRequest>)(request=>
                    {
                        interrupted=request.Intent;
                        customers.AutoSpawn=false;
                    }));
                    customers.AutoSpawn=true;
                    loop.Step(.001f);
                }
                finally { observer.SetValue(customers,previousObserver); }
                Assert.That(interrupted.HasValue,Is.True,"FIXTURE: stop must run inside original preparation callback");
                Assert.That(pending.GetValue(customers),Is.Null);
                Assert.That(AnimalCafe.Tests.PlayMode.Phase14.CafeLoopRuntimeProof.Field<IDictionary>(coordinator,"entries").Contains(interrupted.Value),Is.False,
                    "Interrupted original call cannot submit an orphan");

                // Manual TrySpawn remains a real, independent request while automatic arrivals are held.
                // 停止自动接客后，手动申请仍走原准备和提交入口。
                Assert.That(customers.TrySpawn(),Is.False);
                var manual=pending.GetValue(customers);
                Assert.That(manual,Is.Not.Null);
                var manualKey=AnimalCafe.Tests.PlayMode.Phase14.CafeLoopRuntimeProof.Field<AnimalCafe.Navigation.TrafficIntentKey>(manual,"Key");
                Assert.That(manualKey,Is.Not.EqualTo(key));
                customers.AutoSpawn=true;
                customers.AutoSpawn=false;
                Assert.That(pending.GetValue(customers),Is.SameAs(manual),"Manual request keeps its original age and budget");
                yield return PrepareUnbornAdmission();
                Assert.That(customers.TrySpawn(),Is.True,customers.Status);
                Assert.That(customers.Snapshot.Count,Is.EqualTo(1));
            }
            finally
            {
                customers.AutoSpawn=false;
                clockField.SetValue(customers,originalClock);
            }
        }
        [UnityTest] public IEnumerator ClosingKeepsExistingRealCustomerServiceRunning()
        {
            var clock=new FakeClock(); var day=StartDay(clock);
            yield return PrepareUnbornAdmission();
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            var admitted=customers.Snapshot.Single().VisitId;
            clock.Now=10; customers.AutoSpawn=true;
            for(var frame=0;frame<5000 && (loop.Snapshot.Orders.Count==0 || customers.Snapshot.Count>0);frame++)
                yield return null;
            customers.AutoSpawn=false;
            for(var frame=0;frame<5000 && !loop.GetDrainSnapshot().IsDrained;frame++) yield return null;
            loop.Step(0);
            Assert.That(day.Snapshot.Phase,Is.EqualTo(BusinessDayPhase.Rest),loop.GetDrainSnapshot().Reason);
            Assert.That(day.Snapshot.LastClosedDay.EndKind,Is.EqualTo(BusinessDayEndKind.CompletedDrain));
            Assert.That(loop.Snapshot.Orders.Count,Is.EqualTo(1));
            var order=loop.Snapshot.Orders.Values.Single();
            Assert.That(order.CustomerId,Is.EqualTo(admitted));
            Assert.That(order.State,Is.EqualTo(OrderState.Completed));
            CollectionAssert.AreEquivalent(new[]{admitted},loop.Snapshot.PaidVisitIds);
            Assert.That(customers.Snapshot,Is.Empty);
            Assert.That(customers.Capacity.GetCapacities().All(capacity=>capacity.Used==0),Is.True);
            Assert.That(world.RegisteredActors.Count,Is.EqualTo(2));
        }
    }
}
#endif

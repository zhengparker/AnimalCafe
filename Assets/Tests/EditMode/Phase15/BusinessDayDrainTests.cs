using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.CafeLoop;
using AnimalCafe.Customers;
using AnimalCafe.Employees;
using AnimalCafe.Orders;
using AnimalCafe.Tests.EditMode.Phase14;
using NUnit.Framework;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase15
{
    public sealed class BusinessDayDrainTests
    {
        private CafeLoopEnvironmentFake env;
        private CafeLoopService service;
        [SetUp] public void Setup()
        { env=new CafeLoopEnvironmentFake(); service=new CafeLoopService("drain",env); }
        [TearDown] public void Cleanup() => service.Dispose();
        private bool Pending()
        {
            var property=typeof(CafeLoopService).GetProperty("HasPendingBusinessWork",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(property,Is.Not.Null,"drain must inspect live service facts");
            return (bool)property.GetValue(service);
        }
        private T Field<T>(string name) => (T)typeof(CafeLoopService)
            .GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(service);

        [Test] public void EmptyServiceHasNoPendingWorkEvenWhenPaused()
        { env.Gate=false; Assert.That(Pending(),Is.False); }

        [Test] public void AcceptedCashierWorkPreventsDrainBeforeAnyOrderExists()
        {
            env.VisitValues.Add(new CafeVisitSnapshot("a",CustomerVisitState.Queued,Vector3.zero,counterArrived:true));
            service.Tick(.1f);
            Assert.That(service.Snapshot.Orders,Is.Empty);
            Assert.That(service.Snapshot.Employees.Count,Is.EqualTo(1));
            Assert.That(Pending(),Is.True);
        }

        [TestCase("pickupMoves")][TestCase("exitMoves")][TestCase("blockedCollections")][TestCase("collected")]
        public void RecoveryAndCollectedFactsCannotDisappearFromDrain(string field)
        {
            // 注入真实待恢复集合：query不能顺便清理，也不能以Pause为由忽略。
            var identities=Field<HashSet<string>>(field); identities.Add("a"); env.Gate=false;
            Assert.That(Pending(),Is.True); Assert.That(identities,Does.Contain("a"));
        }

        [Test] public void QueuedCallbackPreventsDrainWithoutActiveWork()
        {
            var callbacks=Field<Queue<CafeTaskEvent>>("events"); callbacks.Enqueue(default);
            Assert.That(Pending(),Is.True); Assert.That(callbacks.Count,Is.EqualTo(1));
        }

        [TestCase(OrderState.Waiting)][TestCase(OrderState.Failed)]
        public void NonCompletedOrderPreventsDrain(OrderState state)
        {
            var orders=Field<OrderService>("orders"); var result=orders.Create("a","coffee");
            Assert.That(result.Succeeded,Is.True);
            if(state==OrderState.Failed) Assert.That(orders.Fail(result.Order.OrderId,"test failure").Succeeded,Is.True);
            Assert.That(Pending(),Is.True);
        }

        private CafeTaskEvent TaskEvent(EmployeeRole role,CafeTaskEventKind kind,CafeWorkStage stage,int generation=1,long request=0)
        {
            var index=env.StartedCheckpoints.FindLastIndex(value=>value.Role==role);
            Assert.That(index,Is.GreaterThanOrEqualTo(0)); var saved=env.StartedCheckpoints[index];
            return new CafeTaskEvent(env.StartedTokens[index],role,env.EmployeeId(role),index+1,request,generation,7,kind,
                new CafeWorkCheckpoint(role,saved.VisitId,saved.Order,stage,0));
        }

        [Test] public void CompletedHistoryWaitsForActualExitAndReturnedCallbackConsumption()
        {
            env.VisitValues.Add(new CafeVisitSnapshot("a",CustomerVisitState.Queued,Vector3.zero,counterArrived:true,layoutRevision:7));
            service.Tick(.1f);
            service.ReportTask(TaskEvent(EmployeeRole.Cashier,CafeTaskEventKind.ServiceFinished,CafeWorkStage.Finished));
            env.IdleRoles.Add(EmployeeRole.Cashier); service.Tick(.1f);
            service.ReportTask(TaskEvent(EmployeeRole.Barista,CafeTaskEventKind.Prepared,CafeWorkStage.Prepared));
            var arrival=TaskEvent(EmployeeRole.Barista,CafeTaskEventKind.DeliveryArrived,CafeWorkStage.Prepared,2,71);
            service.ReportTask(arrival);
            Assert.That(service.TryCommitDelivery(arrival,CafeDeliveryStoreTests.Slot,out var reason),Is.True,reason);
            env.VisitValues[0]=new CafeVisitSnapshot("a",CustomerVisitState.PickupService,Vector3.zero,pickupArrived:true,layoutRevision:7);
            Assert.That(service.TryCollect("a",out reason),Is.True,reason);
            Assert.That(service.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.Completed));
            Assert.That(service.Snapshot.CollectedExitVisitIds,Is.EqualTo(new[]{"a"})); Assert.That(Pending(),Is.True);
            env.VisitValues[0]=new CafeVisitSnapshot("a",CustomerVisitState.Exiting,Vector3.zero,exitArrived:true,layoutRevision:7);
            service.Tick(0);
            Assert.That(service.Snapshot.CollectedExitVisitIds,Is.Empty); Assert.That(Pending(),Is.True,"Barista is still returning");
            service.ReportTask(TaskEvent(EmployeeRole.Barista,CafeTaskEventKind.Returned,CafeWorkStage.Finished,3,72));
            Assert.That(Pending(),Is.True,"Returned callback must be consumed by the real driver");
            service.Tick(.1f);
            Assert.That(Pending(),Is.False); Assert.That(service.Snapshot.PaidVisitIds,Is.EqualTo(new[]{"a"}));
            Assert.That(service.Snapshot.Orders.Values.Single().State,Is.EqualTo(OrderState.Completed));
        }
    }
}

#if UNITY_EDITOR
using System.Linq;
using AnimalCafe.CafeLoop;
using AnimalCafe.Customers;
using AnimalCafe.Decoration;
using AnimalCafe.Employees;
using AnimalCafe.Navigation;
using AnimalCafe.Core.Time;
using AnimalCafe.BusinessDay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using System.Collections;

namespace AnimalCafe.Tests.PlayMode.Phase15
{
    public sealed class BusinessDayUiSceneTests : BusinessDayTestSupport
    {
        private static T PresentationField<T>(object owner,string name)=>(T)owner.GetType().GetField(name,
            System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public).GetValue(owner);
        private BusinessDayStatusView BindFormalPresentation(BusinessDayController day,out GameObject go)
        {
            go=new GameObject("Existing formal status test",typeof(RectTransform),typeof(Text),typeof(BusinessDayStatusView));
            var view=go.GetComponent<BusinessDayStatusView>();var serialized=new SerializedObject(view);
            serialized.FindProperty("status").objectReferenceValue=go.GetComponent<Text>();serialized.ApplyModifiedPropertiesWithoutUndo();
            view.Bind(day);view.Bind(day);return view;
        }
        [UnityTest] public IEnumerator FormalOpenPlanningFollowsActualQueuedCoreDayEvent()=>FormalPlanningEvent(false);
        [UnityTest] public IEnumerator FormalClosingPlanningFollowsActualQueuedCoreDayEvent()=>FormalPlanningEvent(true);
        private IEnumerator FormalPlanningEvent(bool closing)
        {
            // Actual Scene wiring, one public-Core queued model; no claim of business/Native motion or driver budget execution.
            // 真实组件 + 单请求公开 Core 模型；不伪造 Actor/Native/phase/Entry，也不发出实际移动。
            var core=PresentationField<ServiceTrafficCoordinator>(loop,"traffic");var staff=Find<EmployeeFlowController>();
            Assert.That(core,Is.Not.Null);Assert.That(core.HasPendingWork,Is.False);Assert.That(staff.AreEmployeesAtWorkStations(out var reason),Is.True,reason);
            Assert.That(customers.AutoSpawn,Is.False);Assert.That(customers.Snapshot,Is.Empty);Assert.That(loop.Snapshot.Orders,Is.Empty);
            var snapshot=PresentationField<TrafficWorldSnapshot>(core,"snapshot");
            var actor=world.RegisteredActors.Single(a=>a.ActorId==staff.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).ActorId);
            var body=snapshot.Bodies.Single(b=>ReferenceEquals(b.ActorLife,actor));
            var key=new TrafficIntentKey(body.Owner.Session,body.Owner.Epoch,actor.ActorId,"ui-component-model",1,body.Owner.LayoutRevision,TestContext.CurrentContext.Test.Name);
            var request=new TrafficRequest(key,Time.frameCount,snapshot,actor.transform.position,actor.transform.position,actor.Settings.AgentRadius,
                "ui-model/"+actor.ActorId,safetyMargin:actor.Settings.CollisionSkin+actor.Settings.Epsilon,planPrimaryRoute:true);
            var clock=new FakeClock();RequirePolicy();var day=loop.gameObject.AddComponent<BusinessDayController>();
            day.Initialize(loop,new BusinessDaySettings(10,5),clock.Read,false);Assert.That(day.IsValidationMode,Is.False);
            var view=BindFormalPresentation(day,out var go);var events=0;
            System.Action<BusinessDaySnapshot> changed=_=>events++;day.SnapshotChanged+=changed;
            try
            {
                Assert.That(view.CurrentText,Does.Not.Contain("规划"));
                var original=core.EvaluateMovement(request);
                Assert.That(original.Kind,Is.EqualTo(TrafficDecisionKind.DeferredByBudget),"FIXTURE: the actual same public Core has an unissued queued model permission");
                Assert.That(original.AcquiredSequence,Is.Zero);Assert.That(core.HasActiveGrant(key),Is.False);Assert.That(core.HasPendingWork,Is.True);
                var table=PresentationField<System.Collections.IDictionary>(core,"entries");var entry=table[key];
                var names=new[]{"FirstFrame","Queries","RequestSequence","AcquiredSequence"};var accounting=names.Select(n=>PresentationField<object>(entry,n)).ToArray();
                if(closing)clock.Now=10;
                // Same public Day publication path used by its normal driver; no Core ProcessReady/Tick/pump here.
                day.BeforeService();day.AfterService(loop.GetDrainSnapshot());
                Assert.That(events,Is.GreaterThanOrEqualTo(2));Assert.That(day.Snapshot.Phase,Is.EqualTo(closing?BusinessDayPhase.Closing:BusinessDayPhase.Open));
                var eventText=view.CurrentText;var actual=loop.ReadServicePresentation();var forwarded=day.ServicePresentation;
                Assert.That(table[key],Is.SameAs(entry));Assert.That(core.GetDecision(key),Is.SameAs(original));
                CollectionAssert.AreEqual(accounting,names.Select(n=>PresentationField<object>(entry,n)).ToArray());
                Assert.That(PresentationField<System.Collections.IDictionary>(world.Service,"byActor").Contains(actor.ActorId),Is.False);
                Assert.That(customers.Snapshot,Is.Empty);Assert.That(loop.Snapshot.Orders,Is.Empty);Assert.That(customers.Capacity.GetCapacities().All(c=>c.Used==0),Is.True);
                Assert.That(eventText,Does.Not.Contain(actor.ActorId).And.Not.Contain(key.Session).And.Not.Contain(key.Context));
                TestContext.WriteLine("FORMAL_UI_PUBLIC_CORE_MODEL phase="+day.Snapshot.Phase+" actualDecision="+original.Kind+" acquired="+original.AcquiredSequence+" dayEvents="+events+" actualNativeIssued=False");
                Assert.That(eventText,Does.Contain("规划"),"PRODUCT: existing Day event must present real queued planning on the formal player card");
                Assert.That(actual.Kind,Is.EqualTo(CafeServicePresentationKind.Planning));Assert.That(actual.IsAvailable,Is.True);
                Assert.That(forwarded.Kind,Is.EqualTo(actual.Kind));
                for(var i=0;i<3;i++){view.enabled=false;view.enabled=true;view.Bind(day);}
                Assert.That(core.GetDecision(key),Is.SameAs(original));CollectionAssert.AreEqual(accounting,names.Select(n=>PresentationField<object>(entry,n)).ToArray());
                view.enabled=false;var frozen=view.CurrentText;core.Retire(key); // Only this public model permission, never an employee's real intent.
                day.BeforeService();day.AfterService(loop.GetDrainSnapshot());Assert.That(view.CurrentText,Is.EqualTo(frozen));
                view.enabled=true;Assert.That(view.CurrentText,Does.Not.Contain("规划"));
            }
            finally{day.SnapshotChanged-=changed;core.Retire(key);Object.DestroyImmediate(go);}
            yield break;
        }

        [UnityTest] public IEnumerator FormalOpenBlockedUsesActualEmployeeFailureDayEvent()=>FormalBlockedEvent(false);
        [UnityTest] public IEnumerator FormalClosingBlockedUsesActualEmployeeFailureDayEvent()=>FormalBlockedEvent(true);
        private IEnumerator FormalBlockedEvent(bool closing)
        {
            var staff=Find<EmployeeFlowController>();var adapter=Find<NavigationLayoutAdapter>();var layout=Find<CafeLayoutRuntime>();
            var decor=Find<DecorationModeController>();var time=Find<GameTimeService>();var cups=Find<PickupCupDisplay>();
            var grid=(Transform)new SerializedObject(decor).FindProperty("gridRoot").objectReferenceValue;
            var clock=new FakeClock();RequirePolicy();var day=loop.gameObject.AddComponent<BusinessDayController>();
            day.Initialize(loop,new BusinessDaySettings(10,5),clock.Read,false);var view=BindFormalPresentation(day,out var go);
            var events=0;System.Action<BusinessDaySnapshot> changed=_=>events++;day.SnapshotChanged+=changed;
            try
            {
                // Existing public missing-prefab fixture: the actual employee fails, never a private Blocked write.
                var barista=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase13/Employees/Westie/PF_Westie_Employee.prefab").GetComponent<NavigationActor>();
                staff.Configure(world,adapter,layout,decor,time,customers,grid,null,barista,cups);
                yield return null;yield return null;
                for(var frame=0;frame<600 && staff.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).State!=EmployeeState.Blocked;frame++)yield return null;
                Assert.That(staff.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).State,Is.EqualTo(EmployeeState.Blocked),"FIXTURE: original public missing-prefab recipe must actually fail Cashier");
                Assert.That(loop.CanProgress,Is.False);Assert.That(customers.AutoSpawn,Is.False);Assert.That(customers.Snapshot,Is.Empty);Assert.That(loop.Snapshot.Orders,Is.Empty);
                Assert.That(world.RegisteredActors.Any(a=>a.ActorId==loop.EmployeeId(EmployeeRole.Cashier)),Is.False);
                if(closing)
                {
                    clock.Now=10;
                    for(var frame=0;frame<600 && day.Snapshot.Phase!=BusinessDayPhase.Closing;frame++)yield return null;
                }
                Assert.That(day.Snapshot.Phase,Is.EqualTo(closing?BusinessDayPhase.Closing:BusinessDayPhase.Open));Assert.That(events,Is.GreaterThan(0));
                var before=day.Snapshot;var actual=view.CurrentText;var bodies=world.RegisteredActors.ToArray();
                TestContext.WriteLine("FORMAL_UI_ACTUAL_BLOCKED phase="+before.Phase+" cashier="+staff.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).State+" dayEvents="+events);
                Assert.That(actual,Does.Contain("服务受阻"),"PRODUCT: real employee Blocked must be readable in the formal card through existing Day events");
                Assert.That(actual,Does.Contain("营业条件").And.Not.Contain(loop.EmployeeId(EmployeeRole.Cashier)).And.Not.Contain("epoch"));
                Assert.That(loop.ReadServicePresentation().Kind,Is.EqualTo(CafeServicePresentationKind.Blocked));
                Assert.That(day.ServicePresentation.Kind,Is.EqualTo(CafeServicePresentationKind.Blocked));
                for(var i=0;i<3;i++){view.enabled=false;view.enabled=true;view.Bind(day);}
                CollectionAssert.AreEquivalent(bodies,world.RegisteredActors);Assert.That(day.Snapshot.TransitionSequence,Is.EqualTo(before.TransitionSequence));
                Assert.That(staff.Snapshot.Single(e=>e.Role==EmployeeRole.Cashier).State,Is.EqualTo(EmployeeState.Blocked));Assert.That(loop.Snapshot.Orders,Is.Empty);
            }
            finally{day.SnapshotChanged-=changed;Object.DestroyImmediate(go);}
        }

        [Test] public void StatusRefreshIsReadOnlyAndShowsInterruption()
        {
            var clock = new FakeClock(); RequirePolicy();
            var day = loop.gameObject.AddComponent<BusinessDayController>();
            day.Initialize(loop, new BusinessDaySettings(10, 5), clock.Read, true);
            var go = new GameObject("Passive status", typeof(RectTransform), typeof(Text), typeof(BusinessDayStatusView));
            var view = go.GetComponent<BusinessDayStatusView>();
            var serialized = new SerializedObject(view); serialized.FindProperty("status").objectReferenceValue = go.GetComponent<Text>(); serialized.ApplyModifiedPropertiesWithoutUndo();
            try
            {
                view.Bind(day); view.Bind(day);
                Assert.That(view.CurrentText, Does.Contain("Open").And.Contain("Validation"));
                day.RecordLayoutInterruption(1); clock.Now = 10;
                day.BeforeService(); day.AfterService(new BusinessDayDrainResult(false, "Blocked: actual service reason"));
                var before = day.Snapshot; var orders = loop.Snapshot.Orders.Count;
                for (var i = 0; i < 8; i++) { view.enabled = false; view.enabled = true; view.Bind(day); }
                Assert.That(view.CurrentText, Does.Contain("Closing").And.Contain("Blocked: actual service reason").And.Contain("LayoutInterrupted"));
                Assert.That(day.Snapshot.TransitionSequence, Is.EqualTo(before.TransitionSequence));
                Assert.That(day.Snapshot.LastClosedDay, Is.Null);
                Assert.That(loop.Snapshot.Orders.Count, Is.EqualTo(orders));
                view.enabled = false; var frozen = view.CurrentText;
                day.AfterService(new BusinessDayDrainResult(true, ""));
                Assert.That(view.CurrentText, Is.EqualTo(frozen), "Disabled view must unsubscribe");
                view.enabled = true; Assert.That(view.CurrentText, Does.Contain("Rest"));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
#endif

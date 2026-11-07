#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.CafeLoop;
using AnimalCafe.Customers;
using AnimalCafe.Employees;
using AnimalCafe.Navigation;
using AnimalCafe.Orders;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode.Phase14
{
    public sealed partial class CafePickupSideTests
    {
        // Real selected route + body + accepted native request; no safety setting is changed.
        [UnityTest] public IEnumerator SingleSideSelectedUnissuedDeliverySegmentRecognizesFiniteMovingBodyAndKeepsOriginalWork()
        {
            Assert.That(employees.TryGetBinding(EmployeeRole.Barista,out var initialBinding),Is.True);
            var shaping=Blocker((initialBinding.WorkTarget.Position+initialBinding.PickUpTarget.Value.Position)/2,
                "delivery-original-route-shaping-body");
            yield return CafeLoopRuntimeProof.PreparePositiveAdmission(owner,customers,world,6000);
            Assert.That(customers.TrySpawn(),Is.True,customers.Status);
            var visit=customers.Snapshot.Single().VisitId;
            EmployeeTaskRunner runner=null;NavigationActor barista=null;NavigationTarget[] route=null;var index=0;
            var bodyPosition=Vector3.zero;var routeLogged=false;
            bool AtPriorArrival()
            {
                runner=CafeLoopRuntimeProof.Field<IEnumerable>(employees,"employees").Cast<object>()
                    .Select(e=>CafeLoopRuntimeProof.Field<EmployeeTaskRunner>(e,"Runner"))
                    .Single(r=>r!=null && r.Snapshot.Role==EmployeeRole.Barista);
                if(runner.Snapshot.State!=EmployeeState.CarryingToPickUp)return false;
                route=CafeLoopRuntimeProof.Field<NavigationTarget[]>(runner,"deliveryRoute");
                index=CafeLoopRuntimeProof.Field<int>(runner,"deliveryRouteIndex");
                if(route==null || index+1>=route.Length)return false;
                barista=world.RegisteredActors.Single(a=>a.ActorId==runner.Snapshot.ActorId);
                if(!routeLogged){routeLogged=true;TestContext.WriteLine("P14_SELECTED_ROUTE "+string.Join(";",route.Select(t=>t.Position.ToString("F6"))));}
                bodyPosition=barista.transform.position+(route[index+1].Position-barista.transform.position).normalized*1.06f;
                return Vector3.Distance(barista.transform.position,route[index].Position)<.16f &&
                    adapter.IsPointWalkable(bodyPosition) && world.RegisteredActors.All(a=>Vector3.Distance(a.transform.position,bodyPosition)>.92f) &&
                    Vector3.Distance(bodyPosition,route[index+1].Position)<.85f;
            }
            yield return Until(AtPriorArrival);
            var target=route[index+1];var handle=runner.Snapshot.HandleId;var saved=runner.CaptureCheckpoint();
            var queue=CafeLoopRuntimeProof.Field<PickupQueueService>(customers,"pickupQueue");
            Assert.That(queue.TryGetServiceLease(barista.ActorId,out var lease),Is.True);
            Assert.That(customers.IsPickupDeliverySegmentClear(barista.ActorId,target),Is.True,"Selected unissued leg is initially safe");
            var body=Blocker(bodyPosition,"delivery-segment-moving-body");
            Assert.That(customers.IsPickupDeliverySegmentClear(barista.ActorId,target),Is.False,"Original segment guard must reject the real body");
            var path=adapter.GetCompletePath(barista.transform.position,target.Position);
            Assert.That(path,Is.Not.Null);
            bool Traffic()=>(bool)typeof(CustomerFlowController).GetMethod("HasDeliveryTraffic",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(customers,new object[]{barista,new IReadOnlyList<Vector3>[]{path}});
            bool Wait()=>(bool)typeof(CustomerFlowController).GetMethod("ShouldWaitForPickupDelivery",BindingFlags.Instance|BindingFlags.NonPublic)
                .Invoke(customers,new object[]{barista.ActorId,target});
            Assert.That(Traffic(),Is.False,"A parked body alone cannot authorize traffic waiting");
            Assert.That(Wait(),Is.False,"A stationary hard refusal still belongs to the original guard");
            // Find a clear real destination away from the original selected leg.
            var destinations=Enumerable.Range(0,8).Select(i=>Quaternion.Euler(0,i*45,0)*Vector3.forward)
                .SelectMany(d=>new[]{.5f,1.1f,2.2f}.Select(length=>bodyPosition+d*length)).Where(p=>adapter.IsPointWalkable(p) &&
                    world.RegisteredActors.Where(a=>a!=body).All(a=>Vector3.Distance(a.transform.position,p)>.92f) &&
                    CustomerRoutePlanner.IsClear(new[]{barista.transform.position}.Concat(route.Skip(index).Select(t=>t.Position)).ToArray(),new[]{p},.96f))
                .Where(p=>{var candidate=adapter.GetCompletePath(body.transform.position,p);return candidate!=null &&
                    CustomerRoutePlanner.IsClear(candidate,world.RegisteredActors.Where(a=>a!=body).Select(a=>a.transform.position).ToArray(),.911f);}).ToArray();
            Assert.That(destinations,Is.Not.Empty,"Fixture must issue a clear independent complete path");
            MovementResult? completed=null;
            var moving=world.Service.MoveTo(body.ActorId,new NavigationTarget(destinations[0]),r=>completed=r);
            Assert.That(moving.Accepted,Is.True);Assert.That(completed.HasValue,Is.False);
            Assert.That(body.Agent.hasPath,Is.True);Assert.That(body.Agent.isStopped,Is.False);
            Assert.That(Traffic(),Is.True,"Same real body now owns a finite relevant moving path");
            Assert.That(Wait(),Is.True,"Owned selected delivery must classify this exact finite obstruction");
            TestContext.WriteLine("P14_OUTBOUND_TRAFFIC guard=False finiteTraffic=True handle="+handle+" index="+index+" target="+target.Position.ToString("F6")+
                " lease="+lease.ToString("F6")+" body="+body.transform.position.ToString("F6")+" request="+moving.RequestId+" destination="+destinations[0].ToString("F6"));
            var waited=false;
            for(var frame=0;frame<600 && !completed.HasValue;frame++)
            {
                Assert.That(runner.Snapshot.State,Is.Not.EqualTo(EmployeeState.Blocked),"Transient traffic must preserve this selected Prepared attempt");
                if(CafeLoopRuntimeProof.Field<int>(runner,"deliveryRouteIndex")==index+1 &&
                    CafeLoopRuntimeProof.Field<object>(runner,"moveLeg")==null)
                {
                    waited=true;Assert.That(runner.Snapshot.HandleId,Is.EqualTo(handle));
                    Assert.That(runner.CaptureCheckpoint(),Is.EqualTo(saved));
                    Assert.That(queue.TryGetServiceLease(barista.ActorId,out var held),Is.True);Assert.That(held,Is.EqualTo(lease));
                    Assert.That(barista.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="EmployeeHeldCup"),Is.True);
                }
                yield return null;
            }
            Assert.That(waited,Is.True,"The actual prior leg must arrive while the next guard is obstructed");
            Assert.That(completed.HasValue,Is.True);Assert.That(completed.Value.Status,Is.EqualTo(MovementStatus.Arrived));
            world.Unregister(body);body.gameObject.SetActive(false);UnityEngine.Object.Destroy(body.gameObject);
            world.Unregister(shaping);shaping.gameObject.SetActive(false);UnityEngine.Object.Destroy(shaping.gameObject);
            tools.ResumeCollection();
            yield return Until(()=>owner.Snapshot.Orders.Values.Single().State==OrderState.Completed &&
                customers.Capacity.GetCapacities().All(c=>c.Used==0) && employees.Snapshot.All(e=>e.State==EmployeeState.Idle));
            Assert.That(owner.Snapshot.PaidVisitIds.Count,Is.EqualTo(1));
            Assert.That(owner.Snapshot.Orders.Values.Single().CustomerId,Is.EqualTo(visit));
        }
    }
}
#endif

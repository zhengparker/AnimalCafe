using System;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using AnimalCafe.Customers;
using NUnit.Framework;
using UnityEngine;
namespace AnimalCafe.Tests.EditMode.Phase14
{
    public sealed class PickupSidesTests
    {
        const string Support="aa17d8fa59f64be0a6689666ce4a28d2";
        static CafeLayout Layout(int height=1,FurnitureRotation rotation=FurnitureRotation.Degrees0)
        {
            var catalog=new FurnitureDefinitionCatalog(new[]{new FurnitureDefinition("counter","Counter",new GridSize(1,height),PlacementSurfaceType.Floor),new FurnitureDefinition("block","Block",new GridSize(1,1),PlacementSurfaceType.Floor)});
            var value=new CafeLayout(new GridSettings(1),catalog); value.AddRegion(new LayoutRegion("room",new GridPosition(0,0),new GridSize(10,10),LayoutZoneType.Interior));
            Assert.That(value.PlaceFurniture(FurnitureInstance.Restore(Support,"counter",new GridPosition(4,4),rotation)).Succeeded,Is.True); return value;
        }
        static PickUpPointInstance Pickup(string slot="slot.0")=>new PickUpPointInstance("ba17d8fa59f64be0a6689666ce4a28d2",new SurfaceSlotAddress(Support,slot));
        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void AllExposedReachableDirectionsAreReturnedInStableCardinalOrder(int count)
        {
            var layout=Layout(); var slots=new SurfaceSlotCatalog(new[]{new SurfaceSlotDefinition("counter","slot.0",new GridPosition(0,0))});
            var expected=new[]{new GridPosition(4,5),new GridPosition(5,4),new GridPosition(4,3),new GridPosition(3,4)}.Take(count).ToArray();
            var resolver=new InteractionAnchorResolver(); var candidates=resolver.ResolvePickUpCandidates(Pickup(),layout,slots,p=>expected.Contains(p));
            CollectionAssert.AreEqual(expected,candidates.Select(c=>c.Position)); Assert.That(candidates.All(c=>c.Role==InteractionRole.Customer),Is.True);
            CollectionAssert.AreEqual(expected,resolver.ResolvePickUpCandidates(Pickup(),layout,slots,p=>expected.Contains(p),InteractionRole.Employee).Select(c=>c.Position));
            Assert.That(resolver.ResolvePickUp(Pickup(),layout,slots,p=>expected.Contains(p)).Anchors.Count,Is.EqualTo(2),"Old pair API remains compatible");
        }
        [TestCase(FurnitureRotation.Degrees0)] [TestCase(FurnitureRotation.Degrees90)] [TestCase(FurnitureRotation.Degrees180)] [TestCase(FurnitureRotation.Degrees270)]
        public void JoinedTablesWallsRotatedLongSupportAndEachSlotExcludeInnerCells(FurnitureRotation rotation)
        {
            var layout=Layout(3,rotation); var slots=new SurfaceSlotCatalog(Enumerable.Range(0,3).Select(i=>new SurfaceSlotDefinition("counter","slot."+i,new GridPosition(0,i))));
            foreach(var i in Enumerable.Range(0,3))
            {
                bool Clear(GridPosition p)=>layout.IsInsideUnlockedRegion(p) && !layout.TryGetOccupant(p,out _) && !layout.HasReservation(p,LayoutReservationType.Blocked);
                var anchors=new InteractionAnchorResolver().ResolvePickUpCandidates(Pickup("slot."+i),layout,slots,Clear);
                Assert.That(anchors.Count,Is.EqualTo(i==1?2:3),"No direction through another module of the same table");
                var blocked=anchors[0].Position; layout.AddReservation(new LayoutReservation("wall."+i,LayoutReservationType.Blocked,blocked,new GridSize(1,1)));
                Assert.That(new InteractionAnchorResolver().ResolvePickUpCandidates(Pickup("slot."+i),layout,slots,Clear).Any(a=>a.Position==blocked),Is.False);
                layout=Layout(3,rotation);
                var furniture=FurnitureInstance.Restore(("c"+i+"17d8fa59f64be0a6689666ce4a28d2").Substring(0,32),"block",blocked,FurnitureRotation.Degrees0);
                Assert.That(layout.PlaceFurniture(furniture).Succeeded,Is.True);
                Assert.That(new InteractionAnchorResolver().ResolvePickUpCandidates(Pickup("slot."+i),layout,slots,Clear).Any(a=>a.Position==blocked),Is.False);
                layout.RemoveFurniture(furniture.InstanceId);
            }
        }
        [Test] public void DifferentSidesCanBeHeldTogetherButSameSideAndSwitchingAreRejected()
        {
            var a=new Vector3(0,0,-2); var b=new Vector3(2,0,-2); var c=new Vector3(-2,0,-2);
            var q=new PickupQueueService(new[]{Vector3.zero,Vector3.forward*1.1f},new[]{a,b,c});
            Assert.That(q.TryReserve("head",out _),Is.True); Assert.That(q.TryBeginTransfer("head",out var head),Is.True); Assert.That(q.MarkArrived("head",head.Generation),Is.True);
            Assert.That(q.TryReserve("tail",out _),Is.True); Assert.That(q.TryBeginTransfer("tail",out var tail),Is.True); Assert.That(q.MarkArrived("tail",tail.Generation),Is.True);
            Assert.That(q.TryAcquireDelivery("barista",a),Is.True);
            Assert.That(q.TryAcquireCollection("head",a),Is.False);
            Assert.That(q.TryAcquireCollection("head",b),Is.True,"Free alternative side must not wait for the employee to return");
            var service=q.Snapshot[0]; Assert.That(service.Position,Is.EqualTo(b)); Assert.That(service.Arrived,Is.False); Assert.That(q.MarkArrived("head",head.Generation),Is.False);
            Assert.That(q.TryAcquireCollection("head",c),Is.False,"A selected side is stable through an attempt"); Assert.That(q.TryAcquireDelivery("barista",c),Is.False);
            Assert.That(q.TryAcquireCollection("tail",c),Is.False,"All sides share one paid FIFO");
            q.Remove("head"); Assert.That(q.TryReserve("head",out _),Is.False); Assert.That(q.TryAcquireDelivery("head",a),Is.False); Assert.That(q.TryAcquireCollection("tail",b),Is.False,"Remove does not release the departed body lease");
            q.ReleaseServicePosition("wrong"); Assert.That(q.TryAcquireDelivery("other",a),Is.False);
            q.ReleaseServicePosition("head"); Assert.That(q.TryAcquireCollection("tail",b),Is.True);
            q.ReleaseServicePosition("barista"); Assert.That(q.TryAcquireDelivery("barista",b),Is.False); Assert.That(q.TryAcquireDelivery("barista",a),Is.True);
        }
    }
}

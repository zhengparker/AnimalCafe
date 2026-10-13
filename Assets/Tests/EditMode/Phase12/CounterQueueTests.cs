using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using AnimalCafe.Customers;

namespace AnimalCafe.Tests.EditMode.Phase12
{
    public class CounterQueueTests
    {
        [Test] public void AdmissionCanReserveStableTailWhileEarlierCustomerAdvances()
        {
            var queue=new CounterQueueService(new[]{Vector3.zero,Vector3.forward*3,Vector3.forward*6});
            Assert.That(queue.TryJoin("a"),Is.True); Assert.That(queue.Complete("a",1),Is.True);
            Assert.That(queue.TryJoin("b"),Is.True); Assert.That(queue.Complete("b",1),Is.True);
            Assert.That(queue.Remove("a"),Is.True);
            Assert.That(queue.CanJoin,Is.False,"Unassigned old slots must not publish a duplicate tail");
            Assert.That(queue.TryGetNextAdvance(out var advance),Is.True);
            Assert.That(queue.CanJoin,Is.True,"A stable tail is reservable before the earlier customer arrives");
            Assert.That(queue.TryJoin("c"),Is.True);
            Assert.That(queue.Snapshot[1].Position,Is.EqualTo(Vector3.forward*3));
            Assert.That(queue.Complete("b",1),Is.False,"Admission must not change assignment generations");
            Assert.That(queue.Complete("b",advance.Generation),Is.True);
            Assert.That(queue.Complete("c",1),Is.True);
            CollectionAssert.AreEqual(new[]{"b","c"},queue.Snapshot.Select(a=>a.VisitId).ToArray());
        }

        [Test] public void ExitingHeadKeepsItsSlotUntilRemovedBeforeAdmission()
        {
            var queue=new CounterQueueService(new[]{Vector3.zero,Vector3.forward*3});
            Assert.That(queue.TryJoin("a"),Is.True); Assert.That(queue.Complete("a",1),Is.True);
            Assert.That(queue.TryBeginFrontExit(out _),Is.True);
            Assert.That(queue.TryJoin("b"),Is.False);
            Assert.That(queue.Remove("a"),Is.True);
            Assert.That(queue.TryJoin("b"),Is.True);
            Assert.That(queue.Snapshot.Single().Position,Is.EqualTo(Vector3.zero));
        }

        [Test] public void DefaultSpacingFitsThirdCustomerBehindSecondAtSafeFloorEdge()
        {
            var slots=new CounterQueuePlanner().Build(new Vector3(2.5f,0,2.5f),Vector3.back,3,
                (p,previous)=>p.x>=.5f && p.z>=.5f);
            CollectionAssert.AreEqual(new[]{new Vector3(2.5f,0,2.5f),new Vector3(2.5f,0,1.5f),
                new Vector3(2.5f,0,.5f)},slots,"Third customer should use the safe cell behind the second before turning");
        }
        [Test] public void StraightQueueStartsAtHeadWithSafeSpacing()
        {
            var slots = new CounterQueuePlanner(1.1f).Build(Vector3.zero, Vector3.forward, 3, (p, previous) => true);
            Assert.That(slots.Count, Is.EqualTo(3));
            Assert.That(slots[0], Is.EqualTo(Vector3.zero));
            Assert.That(slots[2], Is.EqualTo(Vector3.forward * 2.2f));
        }
        [Test] public void ObstacleChoosesLeftBeforeRightAndStopsWhenNoSpace()
        {
            var slots = new CounterQueuePlanner(1.1f).Build(Vector3.zero, Vector3.forward, 3,
                (p, previous) => p.z >= 0 && p.z < 1 && p.x >= -1.3f && p.x <= 1.3f);
            Assert.That(slots.Count, Is.EqualTo(2));
            Assert.That(slots[1].x,Is.LessThanOrEqualTo(-1.1f),"Equal length edge-aligned branches still prefer left");
            Assert.That(slots[1].x,Is.GreaterThanOrEqualTo(-1.3f));
            Assert.That(Mathf.Abs(slots[1].z),Is.LessThan(.001f));
        }
        [Test] public void SlotsNeverLoopOrCrowdNonAdjacentSlots()
        {
            var slots = new CounterQueuePlanner(1.1f).Build(Vector3.zero, Vector3.forward, 100,
                (p, previous) => Mathf.Abs(p.x) <= 2 && Mathf.Abs(p.z) <= 2);
            Assert.That(slots.Count, Is.GreaterThanOrEqualTo(2));
            for (var i = 0; i < slots.Count; i++)
                for (var j = 0; j < i; j++) Assert.That(Vector3.Distance(slots[i], slots[j]), Is.GreaterThanOrEqualTo(1.099f));
            Assert.That(slots.Count, Is.LessThan(100));
        }
        [Test] public void RotatedHeadRetainsRelativeLeftPriority()
        {
            var rotation = Quaternion.Euler(0, 90, 0);
            var slots = new CounterQueuePlanner(1.1f).Build(Vector3.zero, rotation * Vector3.forward, 2,
                (p, previous) => !previous.HasValue || Vector3.Distance(p, rotation * Vector3.forward * 1.1f) > .1f);
            Assert.That(slots.Count, Is.EqualTo(2));
            Assert.That(Vector3.Distance(slots[1], rotation * Vector3.left * 1.1f), Is.LessThan(.001f));
        }
        [Test] public void InvalidSpacingOrDirectionIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CounterQueuePlanner(.999f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CounterQueuePlanner(1.1f).Build(Vector3.zero, Vector3.zero, 2, (p, q) => true));
        }
        [TestCase(0)] [TestCase(90)] [TestCase(180)] [TestCase(270)]
        public void BoundaryDeadEndRetriesTheOtherTurnWithoutCrowding(int yaw)
        {
            var rotation=Quaternion.Euler(0,yaw,0);
            var cells=new[]{Vector3.zero,Vector3.forward,Vector3.forward+Vector3.left,
                Vector3.forward+Vector3.right,Vector3.forward+Vector3.right*2,
                Vector3.forward*2+Vector3.right*2,Vector3.forward*3+Vector3.right*2};
            var allowed=cells.Select(p=>rotation*(p*1.1f)).ToArray();
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,rotation*Vector3.forward,6,
                (p,previous)=>allowed.Any(a=>Vector3.Distance(a,p)<.001f));
            Assert.That(slots.Count,Is.EqualTo(6),"The first left branch stops at three; the existing right branch has six safe slots");
            Assert.That(Vector3.Distance(slots[2],rotation*((Vector3.forward+Vector3.right)*1.1f)),Is.LessThan(.001f));
            for(var i=1;i<slots.Count;i++)
            {
                Assert.That(Vector3.Distance(slots[i-1],slots[i]),Is.EqualTo(1.1f).Within(.001f));
                for(var j=0;j<i;j++) Assert.That(Vector3.Distance(slots[j],slots[i]),Is.GreaterThanOrEqualTo(1.099f));
            }
        }
        [Test] public void BoundarySearchIsFiniteAndDeterministicWhenCapacityCannotFit()
        {
            var checks=0;
            Func<Vector3,Vector3?,bool> legal=(p,previous)=>
            { checks++; return Mathf.Abs(p.x)<=2.21f && Mathf.Abs(p.z)<=2.21f; };
            var planner=new CounterQueuePlanner(1.1f);
            var first=planner.Build(Vector3.zero,Vector3.forward,100,legal);
            Assert.That(checks,Is.LessThanOrEqualTo(257),"At most 256 candidates plus the head; no unbounded search");
            checks=0;
            CollectionAssert.AreEqual(first,planner.Build(Vector3.zero,Vector3.forward,100,legal));
            Assert.That(checks,Is.LessThanOrEqualTo(257));
            Assert.That(first.Count,Is.LessThan(100));
            Assert.That(first.Count,Is.GreaterThan(1));
        }
        [Test] public void BacktrackingReachabilityUsesOnlyTheCurrentQueuePrefix()
        {
            var left=(Vector3.forward+Vector3.left)*1.1f;
            var right=(Vector3.forward+Vector3.right)*1.1f;
            var allowed=new[]{Vector3.zero,Vector3.forward*1.1f,left,right,
                (Vector3.forward+Vector3.right*2)*1.1f,(Vector3.forward*2+Vector3.right*2)*1.1f};
            var testedLeft=false; var retriedRight=false;
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,Vector3.forward,5,
                (p,previous)=>allowed.Any(a=>Vector3.Distance(a,p)<.001f),(p,prefix)=>
                {
                    Assert.That(prefix.Any(a=>Vector3.Distance(a,p)<.001f),Is.False,"Candidate is not an already occupied slot");
                    if(Vector3.Distance(p,left)<.001f) testedLeft=true;
                    if(testedLeft && Vector3.Distance(p,right)<.001f)
                    {
                        retriedRight=true;
                        Assert.That(prefix.Count,Is.EqualTo(2));
                        Assert.That(prefix.Any(a=>Vector3.Distance(a,left)<.001f),Is.False,"Discarded turn must not remain a virtual actor");
                    }
                    return true;
                });
            Assert.That(testedLeft && retriedRight,Is.True);
            Assert.That(slots.Count,Is.EqualTo(5));
        }
        [Test] public void ForwardAssignmentsCanOverlapAndKeepFifo()
        {
            var queue = new CounterQueueService(new[] { Vector3.zero, Vector3.forward * 1.1f, Vector3.forward * 2.2f });
            Assert.That(queue.TryJoin("a"), Is.True); Assert.That(queue.TryJoin("b"), Is.True); Assert.That(queue.TryJoin("c"), Is.True);
            Assert.That(queue.TryJoin("d"), Is.False);
            Assert.That(queue.TryBeginFrontExit(out _), Is.False);
            foreach (var a in queue.Snapshot) queue.Complete(a.VisitId, a.Generation);
            Assert.That(queue.TryBeginFrontExit(out var front), Is.True); Assert.That(front, Is.EqualTo("a"));
            Assert.That(queue.TryBeginFrontExit(out _), Is.False);
            queue.Remove("a");
            var held=queue.Snapshot.ToArray();
            Assert.That(queue.TryGetNextAdvance(out _,candidate=>false),Is.False,"Conflicting route waits before assignment commit");
            CollectionAssert.AreEqual(held,queue.Snapshot,"Waiting must retain each target and generation");
            Assert.That(queue.TryGetNextAdvance(out var next), Is.True); Assert.That(next.VisitId, Is.EqualTo("b"));
            Assert.That(queue.TryGetNextAdvance(out var follower), Is.True,"Follower assignment must not wait for leader Complete");
            Assert.That(follower.VisitId, Is.EqualTo("c"));
            Assert.That(queue.TryGetNextAdvance(out _), Is.False);
            Assert.That(queue.Complete("b", next.Generation - 1), Is.False);
            Assert.That(queue.Complete("c", follower.Generation), Is.True,"Callbacks belong to each assignment");
            Assert.That(queue.TryBeginFrontExit(out _), Is.False,"Front has not actually arrived");
            Assert.That(queue.Complete("b", next.Generation), Is.True);
            Assert.That(queue.Snapshot[0].VisitId, Is.EqualTo("b"));
        }
        [TestCase(0)] [TestCase(90)] [TestCase(180)] [TestCase(270)]
        public void EdgeRowUsesRemainingSpaceWithUniformMinimumSpacing(int yaw)
        {
            var rotation=Quaternion.Euler(0,yaw,0); var inverse=Quaternion.Inverse(rotation);
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,rotation*Vector3.forward,6,(p,previous)=>
            { var local=inverse*p; return Mathf.Abs(local.x)<.01f && local.z>=-.001f && local.z<=4; });
            Assert.That(slots.Count,Is.EqualTo(4));
            Assert.That(slots[0],Is.EqualTo(Vector3.zero),"Head must remain anchored");
            Assert.That((inverse*slots.Last()).z,Is.GreaterThan(3.97f),"Old fixed spacing leaves the safe 3.3-to-4.0 edge strip unused");
            var gap=Vector3.Distance(slots[0],slots[1]);
            Assert.That(gap,Is.GreaterThanOrEqualTo(1.1f));
            for(var i=1;i<slots.Count;i++) Assert.That(Vector3.Distance(slots[i-1],slots[i]),Is.EqualTo(gap).Within(.001f));
        }
        [Test] public void EdgeAlignmentRejectsUnreachableEndAndRestoresTheOriginalRow()
        {
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,Vector3.forward,6,
                (p,previous)=>Mathf.Abs(p.x)<.01f && p.z>=0 && p.z<=4,(p,prefix)=>p.z<3.6f);
            Assert.That(slots.Count,Is.EqualTo(4));
            for(var i=0;i<slots.Count;i++) Assert.That(Vector3.Distance(slots[i],Vector3.forward*(i*1.1f)),Is.LessThan(.001f));
        }
        [Test] public void EdgeSpaceSmallerThanMinimumCannotMoveHeadOrAddACustomer()
        {
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,Vector3.forward,4,
                (p,previous)=>Mathf.Abs(p.x)<.01f && p.z>=0 && p.z<=.9f);
            CollectionAssert.AreEqual(new[]{Vector3.zero},slots);
        }
        [Test] public void EdgeAdjustmentKeepsShortRowsCompact()
        {
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,Vector3.forward,3,
                (p,previous)=>Mathf.Abs(p.x)<.01f && p.z>=0 && p.z<=2);
            Assert.That(slots.Count,Is.EqualTo(2));
            Assert.That(Vector3.Distance(slots[0],slots[1]),Is.EqualTo(1.1f).Within(.001f),"An edge adjustment over 25% must restore the compact original row");
        }
        [Test] public void FullQueueKeepsItsLastStraightSegmentWithoutAnExtraEdgeExtension()
        {
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,Vector3.forward,4,
                (p,previous)=>Mathf.Abs(p.x)<.01f && p.z>=0 && p.z<=4);
            Assert.That(slots.Count,Is.EqualTo(4));
            Assert.That(slots.Last().z,Is.EqualTo(3.3f).Within(.001f),"Capacity is already filled; do not stretch a segment that has no next turn");
        }
        [Test] public void EdgeSamplingHeightKeepsTheRowOnItsOriginalHorizontalPlane()
        {
            var slots=new CounterQueuePlanner(1.1f).Build(Vector3.zero,Vector3.forward,6,
                (p,previous)=>Mathf.Abs(p.x)<.01f && p.z>=0 && p.z<=4,null,
                (from,point)=>point+Vector3.up*.025f);
            Assert.That(slots.Last().z,Is.GreaterThan(3.97f));
            Assert.That(slots.All(p=>Mathf.Abs(p.y)<.001f),Is.True);
        }
        [Test] public void ShrinkingSlotsKeepsPeopleAndAllowsSafeFrontExit()
        {
            var queue = new CounterQueueService(new[] { Vector3.zero, Vector3.forward * 1.1f, Vector3.forward * 2.2f });
            queue.TryJoin("a"); queue.TryJoin("b"); queue.TryJoin("c");
            foreach (var a in queue.Snapshot) queue.Complete(a.VisitId, a.Generation);
            queue.UpdateSlots(new[] { Vector3.zero, Vector3.forward * 1.1f });
            Assert.That(queue.Snapshot.Count, Is.EqualTo(3)); Assert.That(queue.CanJoin, Is.False);
            Assert.That(queue.TryBeginFrontExit(out var front), Is.True); Assert.That(front, Is.EqualTo("a"));
        }
        [Test] public void RestartConcurrentAssignmentsRejectsEachOldGeneration()
        {
            var queue=new CounterQueueService(new[]{Vector3.zero,Vector3.forward*1.1f});
            queue.TryJoin("a"); queue.TryJoin("b");
            foreach(var assignment in queue.Snapshot) queue.Complete(assignment.VisitId,assignment.Generation);
            queue.UpdateSlots(new[]{Vector3.right*3,Vector3.right*3+Vector3.forward*1.1f});
            Assert.That(queue.TryGetNextAdvance(out var first),Is.True);
            Assert.That(queue.TryGetNextAdvance(out var second),Is.True);
            Assert.That(queue.CanJoin,Is.False); Assert.That(queue.TryBeginFrontExit(out _),Is.False);
            queue.RestartAssignments();
            Assert.That(queue.TryGetNextAdvance(out var resumedFirst),Is.True);
            Assert.That(queue.TryGetNextAdvance(out var resumedSecond),Is.True);
            Assert.That(queue.Complete("a",first.Generation),Is.False);
            Assert.That(queue.Complete("b",second.Generation),Is.False);
            Assert.That(queue.Complete("b",resumedSecond.Generation),Is.True);
            Assert.That(queue.Complete("a",resumedFirst.Generation),Is.True);
            CollectionAssert.AreEqual(new[]{"a","b"},queue.Snapshot.Select(a=>a.VisitId).ToArray());
        }
    }
}

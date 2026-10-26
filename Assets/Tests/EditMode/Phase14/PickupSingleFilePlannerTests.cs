using System.Linq;
using System;
using System.Collections.Generic;
using System.Reflection;
using AnimalCafe.Layout;
using AnimalCafe.Customers;
using NUnit.Framework;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase14
{
    public sealed class PickupSingleFilePlannerTests
    {
        [Test] public void ReconfigureWithoutActiveVisitsClearsPreviousPlanningTrace()
        {
            var root=new GameObject("trace lifecycle");
            try
            {
                var controller=root.AddComponent<CustomerFlowController>();
                var records=(System.Collections.IList)typeof(CustomerFlowController).GetField("pickupPlanningRejections",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(controller);
                // A previous immutable observation is a marker; reconfiguration must retire the entire latest-attempt list.
                var recordType=typeof(CustomerFlowController).Assembly.GetType("AnimalCafe.Customers.QueueForwardRejection");
                records.Add(Activator.CreateInstance(recordType,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public,null,
                    new object[]{Vector3.forward,Vector3.zero,Vector3.forward,new[]{Vector3.zero},"isLegal"},null));
                Assert.That(records.Count,Is.EqualTo(1));
                controller.Configure(null,null,null,Array.Empty<AnimalCafe.Navigation.NavigationActor>(),null,null,null);
                Assert.That(records.Count,Is.Zero);
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void SmallRotatedRoomExitCandidatesUseGridCoordinatesAndKeepThreeWaypointBound()
        {
            var root=new GameObject("rotated 4x2 room");
            try
            {
                root.transform.position=new Vector3(10,0,-7);root.transform.rotation=Quaternion.Euler(0,90,0);
                var method=typeof(CustomerFlowController).GetMethod("PickupExitBoundaryCandidates",BindingFlags.Static|BindingFlags.NonPublic);
                var result=(Vector3[][])method.Invoke(null,new object[]{root.transform,new[]{new GridPosition(0,0),new GridPosition(3,1)},1f,
                    new Vector3(11,0,-8),new Vector3(10.5f,0,-9),.45f});
                Assert.That(result.Length,Is.EqualTo(4));Assert.That(result.All(r=>r.Length==3),Is.True);
                var first=new[]{new Vector3(11,0,-7.47f),new Vector3(10.5f,0,-7.47f),new Vector3(10.5f,0,-9)};
                for(var i=0;i<3;i++)Assert.That(Vector3.Distance(result[0][i],first[i]),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(result[1][0],new Vector3(11,0,-10.53f)),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(result[2][0],new Vector3(10.47f,0,-8)),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(result[3][0],new Vector3(11.53f,0,-8)),Is.LessThan(.001f));
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }

        [Test] public void OriginalGateTraceDoesNotAddCandidatesOrChangeBudgetAndSlots()
        {
            var plain=new CounterQueuePlanner(1.1f,preserveStraightPrefix:true);
            var traced=new CounterQueuePlanner(1.1f,preserveStraightPrefix:true);
            var originalCalls=new List<Vector3>();var observedCalls=new List<Vector3>();
            var snapshots=new List<Vector3[]>();var categories=new List<string>();
            Action<Vector3,Vector3,Vector3,IReadOnlyList<Vector3>,string> observer=(point,previous,direction,prefix,reason)=>{
                snapshots.Add(prefix.ToArray());categories.Add(reason);
                Assert.That(Vector3.Distance(point,previous+direction*1.1f),Is.LessThan(.001f));
            };
            typeof(CounterQueuePlanner).GetField("RejectedForward",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(traced,observer);
            bool Legal(Vector3 p)=>Mathf.Abs(p.x)<2 && Mathf.Abs(p.z)<2;
            var expected=plain.Build(Vector3.zero,Vector3.forward,100,(p,previous)=>{originalCalls.Add(p);return Legal(p);});
            var actual=traced.Build(Vector3.zero,Vector3.forward,100,(p,previous)=>{observedCalls.Add(p);return Legal(p);});
            CollectionAssert.AreEqual(expected,actual);CollectionAssert.AreEqual(originalCalls,observedCalls);
            Assert.That(observedCalls.Count,Is.LessThanOrEqualTo(257),"Head plus original 256 candidate budget");
            Assert.That(categories,Is.Not.Empty);Assert.That(categories.All(c=>c=="isLegal" || c=="duplicate-spacing" || c=="canReach"),Is.True);
            Assert.That(snapshots.All(p=>p.Length>0 && p[0]==Vector3.zero),Is.True);
        }

        // A longer lateral branch must never replace this already-safe straight prefix.
        [TestCase(0)] [TestCase(90)] [TestCase(180)] [TestCase(270)]
        public void SafeStraightPrefixWinsOverLargerLateralCapacity(int yaw)
        {
            var rotation=Quaternion.Euler(0,yaw,0);
            var cells=new[]{Vector3.zero,Vector3.forward,Vector3.forward*2,
                Vector3.left,Vector3.left*2,Vector3.left*3,Vector3.left*4,Vector3.left*5};
            var legal=cells.Select(p=>rotation*p*1.1f).ToArray();
            var result=new CounterQueuePlanner(1.1f,preserveStraightPrefix:true).Build(Vector3.zero,rotation*Vector3.forward,6,
                (p,previous)=>legal.Any(a=>Vector3.Distance(a,p)<.001f));
            Assert.That(result.Count,Is.EqualTo(3),"Short safe queue limits admission instead of spreading sideways");
            for(var i=0;i<3;i++)Assert.That(Vector3.Distance(result[i],rotation*Vector3.forward*(i*1.1f)),Is.LessThan(.001f));
        }

        [Test] public void FirstStraightRejectedByRealRouteGuardAllowsLegalTurn()
        {
            var result=new CounterQueuePlanner(1.1f,preserveStraightPrefix:true).Build(Vector3.zero,Vector3.forward,3,
                (p,previous)=>p.x>=-2.201f && p.x<=.001f && p.z>=-.001f && p.z<=1.101f,
                (p,prefix)=>p.z<.01f); // Delivery corridor rejects the straight candidate.
            Assert.That(result.Count,Is.EqualTo(3));
            Assert.That(Vector3.Distance(result[1],Vector3.left*1.1f),Is.LessThan(.001f));
            Assert.That(Vector3.Distance(result[2],Vector3.left*2.2f),Is.LessThan(.001f));
        }
    }
}

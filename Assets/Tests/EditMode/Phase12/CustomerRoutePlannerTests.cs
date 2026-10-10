using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AnimalCafe.Customers;

namespace AnimalCafe.Tests.EditMode.Phase12
{
    public class CustomerRoutePlannerTests
    {
        [Test] public void DetourSegmentsKeepClearOfTwoQueuedBodies()
        {
            var start=new Vector3(-2,0,-2); var goal=new Vector3(2,0,2);
            var occupied=new[]{new Vector3(0,0,-.55f),new Vector3(0,0,.55f)};
            var route=new CustomerRoutePlanner().Build(start,goal,occupied,1,(a,b)=>new[]{a,b});
            Assert.That(route,Is.Not.Null); Assert.That(route.Count,Is.GreaterThan(1));
            var previous=start;
            foreach(var point in route)
            {
                for(var sample=0;sample<=1000;sample++)
                    foreach(var body in occupied)
                        Assert.That(Vector3.Distance(Vector3.Lerp(previous,point,sample/1000f),body),Is.GreaterThanOrEqualTo(.999f));
                previous=point;
            }
            Assert.That(previous,Is.EqualTo(goal));
        }
        [Test] public void FullyBlockedCorridorReturnsNoRouteWithBoundedQueries()
        {
            var calls=0;
            IReadOnlyList<Vector3> Corridor(Vector3 a,Vector3 b)
            { calls++; return Mathf.Abs(a.z)<=.45f && Mathf.Abs(b.z)<=.45f?new[]{a,b}:null; }
            var route=new CustomerRoutePlanner().Build(Vector3.left*2,Vector3.right*2,new[]{Vector3.zero},1,Corridor);
            Assert.That(route,Is.Null); Assert.That(calls,Is.LessThanOrEqualTo(324));
        }
        [Test] public void NativePathCornersMustAlsoAvoidOccupiedBodies()
        {
            Assert.That(CustomerRoutePlanner.IsClear(new[]{Vector3.left*2,Vector3.zero,Vector3.right*2},new[]{Vector3.zero},1),Is.False);
            Assert.That(CustomerRoutePlanner.IsClear(new[]{new Vector3(-2,0,2),new Vector3(2,0,2)},new[]{Vector3.zero},1),Is.True);
        }
    }
}

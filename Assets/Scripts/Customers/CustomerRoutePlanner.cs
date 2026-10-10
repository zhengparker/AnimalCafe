using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AnimalCafe.Customers
{
    // 有限的临时绕行查询；只产出waypoints，移动/碰撞仍完全由P11执行。
    public sealed class CustomerRoutePlanner
    {
        public IReadOnlyList<Vector3> Build(Vector3 start,Vector3 goal,IReadOnlyList<Vector3> occupied,
            float clearance,Func<Vector3,Vector3,IReadOnlyList<Vector3>> getPath)
        {
            if(occupied==null || getPath==null) throw new ArgumentNullException();
            if(float.IsNaN(clearance) || float.IsInfinity(clearance) || clearance<=0) throw new ArgumentOutOfRangeException(nameof(clearance));
            var direct=getPath(start,goal);
            if(direct==null) return null;
            if(IsClear(direct,occupied,clearance)) return new[]{goal};

            var nodes=new List<Vector3>{start,goal};
            var radius=clearance/Mathf.Cos(Mathf.PI/8)+.06f;
            // 两圈候选兼顾身体近侧与家具外侧；最多130节点，不无界搜索。
            foreach(var center in occupied.OrderBy(p=>DistanceToSegment(p,start,goal)).Take(8))
                foreach(var ring in new[]{radius,radius*2})
                    for(var i=0;i<8;i++)
                    {
                        var angle=i*Mathf.PI/4;
                        var point=center+new Vector3(Mathf.Cos(angle)*ring,0,Mathf.Sin(angle)*ring);
                        point.y=start.y;
                        if(occupied.All(p=>HorizontalDistance(point,p)>=clearance)) nodes.Add(point);
                    }
            var cost=Enumerable.Repeat(float.PositiveInfinity,nodes.Count).ToArray();
            var parent=Enumerable.Repeat(-1,nodes.Count).ToArray();
            var closed=new bool[nodes.Count]; cost[0]=0;
            for(var step=0;step<nodes.Count;step++)
            {
                var current=-1; var best=float.PositiveInfinity;
                for(var i=0;i<nodes.Count;i++)
                {
                    var score=cost[i]+HorizontalDistance(nodes[i],goal);
                    if(!closed[i] && score<best) { best=score; current=i; }
                }
                if(current<0) return null;
                if(current==1)
                {
                    var route=new List<Vector3>();
                    for(var i=1;i!=0;i=parent[i]) route.Add(nodes[i]);
                    route.Reverse(); return route.AsReadOnly();
                }
                closed[current]=true;
                for(var next=1;next<nodes.Count;next++)
                {
                    if(closed[next] || next==current) continue;
                    var lowerBound=cost[current]+HorizontalDistance(nodes[current],nodes[next]);
                    if(lowerBound>=cost[next]) continue;
                    var path=getPath(nodes[current],nodes[next]);
                    if(path==null || !IsClear(path,occupied,clearance)) continue;
                    var length=0f;
                    for(var j=1;j<path.Count;j++) length+=HorizontalDistance(path[j-1],path[j]);
                    if(cost[current]+length>=cost[next]) continue;
                    cost[next]=cost[current]+length; parent[next]=current;
                }
            }
            return null;
        }
        public static bool IsClear(IReadOnlyList<Vector3> path,IReadOnlyList<Vector3> occupied,float clearance)
        {
            if(path==null || path.Count==0) return false;
            foreach(var point in occupied)
            {
                if(path.Count==1 && HorizontalDistance(point,path[0])<clearance) return false;
                for(var i=1;i<path.Count;i++)
                    if(DistanceToSegment(point,path[i-1],path[i])<clearance) return false;
            }
            return true;
        }
        private static float HorizontalDistance(Vector3 a,Vector3 b)
        { a.y=b.y=0; return Vector3.Distance(a,b); }
        private static float DistanceToSegment(Vector3 point,Vector3 start,Vector3 end)
        {
            point.y=start.y=end.y=0; var delta=end-start;
            var t=delta.sqrMagnitude<1e-10f?0:Mathf.Clamp01(Vector3.Dot(point-start,delta)/delta.sqrMagnitude);
            return Vector3.Distance(point,start+delta*t);
        }
    }
}

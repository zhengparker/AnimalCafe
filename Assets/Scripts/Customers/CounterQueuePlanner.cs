using System;
using System.Collections.Generic;
using UnityEngine;
namespace AnimalCafe.Customers
{
    public sealed class CounterQueuePlanner
    {
        private const int CandidateBudget=256;
        private readonly float spacing;
        public CounterQueuePlanner(float spacing=1.0f)
        {
            if (float.IsNaN(spacing) || float.IsInfinity(spacing) || spacing<1.0f)
                throw new ArgumentOutOfRangeException(nameof(spacing));
            this.spacing=spacing;
        }
        public IReadOnlyList<Vector3> Build(Vector3 head, Vector3 outward, int maxSlots,
            Func<Vector3,Vector3?,bool> isLegal,Func<Vector3,IReadOnlyList<Vector3>,bool> canReach=null,
            Func<Vector3,Vector3,Vector3?> sampleEdge=null)
        {
            if(!Finite(head) || !Finite(outward) || Mathf.Abs(outward.y)>.001f || outward.sqrMagnitude<.01f || maxSlots<0)
                throw new ArgumentOutOfRangeException(nameof(outward));
            if(isLegal==null) throw new ArgumentNullException(nameof(isLegal));
            var result=new List<Vector3>();
            var prefix=result.AsReadOnly();
            if(maxSlots==0 || !isLegal(head,null) || canReach!=null && !canReach(head,prefix)) return prefix;
            result.Add(head); var best=new List<Vector3>(result); var bestAligned=false; var checkedCandidates=0;
            // 所有候选、边缘探测和重排复验共用256次预算；只改目标，不移动角色。
            bool Legal(Vector3 point,Vector3 previous)
            {
                if(checkedCandidates>=CandidateBudget) return false;
                checkedCandidates++; return isLegal(point,previous);
            }
            bool Reachable(Vector3 point)
                => !result.Exists(p=>Vector3.Distance(p,point)<spacing-.001f) &&
                    (canReach==null || canReach(point,prefix));
            bool AlignEdge(Vector3 direction)
            {
                var last=result.Count-1;
                if(last<1) return false; // 不移动队首，也不靠压缩间距新增角色。
                var start=last-1;
                while(start>0 && Vector3.Dot((result[start]-result[start-1]).normalized,direction)>.999f) start--;
                if(Vector3.Dot((result[last]-result[start]).normalized,direction)<.999f) return false;
                var tail=result[last]; var low=0f; var high=spacing;
                for(var probe=0;probe<8 && checkedCandidates<CandidateBudget;probe++)
                {
                    var middle=(low+high)*.5f;
                    if(Legal(tail+direction*middle,tail)) low=middle; else high=middle;
                }
                var requested=tail+direction*low;
                var sampled=sampleEdge==null?(Vector3?)requested:sampleEdge(tail,requested);
                if(!sampled.HasValue || !Finite(sampled.Value)) return false;
                // 从实际owned路径endpoint向内留2cm，不使用NavMesh采样容差外的原请求点。
                var end=sampled.Value-direction*.02f;
                end.y=tail.y; // 站位在同一水平面，owned路径的微小高度差不改变队形。
                var extension=end-tail;
                if(Vector3.Dot(extension,direction)<.05f || Vector3.Cross(extension,direction).magnitude>.01f) return false;
                var anchor=result[start]; var gap=Vector3.Distance(anchor,end)/(last-start);
                // 边缘微调最多拉开25%；短段需要大幅拉开时保留原队形。
                if(gap<spacing || gap>spacing*1.25f) return false;
                var saved=result.ToArray();
                result.RemoveRange(start+1,last-start);
                for(var i=start+1;i<=last;i++)
                {
                    var point=Vector3.Lerp(anchor,end,(float)(i-start)/(last-start));
                    if(!Legal(point,result[result.Count-1]) || !Reachable(point))
                    { result.Clear(); result.AddRange(saved); return false; }
                    result.Add(point);
                }
                return true;
            }
            // 先沿直线利用安全边缘，再转弯；失败时完整撤回这一分支。
            bool Extend(Vector3 direction,bool mayAlign=true,bool aligned=false)
            {
                if(result.Count>best.Count || aligned && !bestAligned && result.Count==best.Count)
                { best=new List<Vector3>(result); bestAligned=aligned; }
                // 名额已满，不为了下一步不存在的转弯继续拉长最后直段。
                if(result.Count>=maxSlots) return true;
                if(checkedCandidates>=CandidateBudget) return false;
                var previous=result[result.Count-1];
                var forward=previous+direction*spacing;
                var forwardLegal=Legal(forward,previous);
                if(!forwardLegal && mayAlign && checkedCandidates<CandidateBudget)
                {
                    var saved=result.ToArray();
                    if(AlignEdge(direction))
                    {
                        if(Extend(direction,false,true)) return true;
                        result.Clear(); result.AddRange(saved);
                    }
                }
                foreach(var candidateDirection in new[]{direction,Quaternion.Euler(0,-90,0)*direction,Quaternion.Euler(0,90,0)*direction})
                {
                    if(checkedCandidates>=CandidateBudget) return false;
                    var candidate=previous+candidateDirection*spacing;
                    var sameDirection=candidateDirection==direction;
                    if(!(sameDirection?forwardLegal:Legal(candidate,previous)) || !Reachable(candidate)) continue;
                    // prefix只含当前分支；被放弃的站位不会继续充当虚拟障碍。
                    result.Add(candidate);
                    if(Extend(candidateDirection,true,aligned)) return true;
                    result.RemoveAt(result.Count-1);
                }
                return false;
            }
            Extend(outward.normalized);
            return best.AsReadOnly();
        }
        private static bool Finite(Vector3 p) => !float.IsNaN(p.x+p.y+p.z) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
    }
}

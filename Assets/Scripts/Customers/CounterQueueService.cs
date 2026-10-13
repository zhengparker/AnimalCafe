using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
namespace AnimalCafe.Customers
{
    public sealed class CounterQueueService
    {
        private sealed class Entry { public string Id; public int Slot,Generation=1; public Vector3 Position; public bool Arrived,NeedsReplan,Moving; }
        private readonly List<Entry> entries=new List<Entry>();
        private Vector3[] slots;
        private string exiting;
        public CounterQueueService(IReadOnlyList<Vector3> slots) { UpdateSlots(slots); }
        // 已分配的FIFO目标稳定即可预留尾位；实际身体/路线由Flow验证。
        // Advancing is not a global admission lock; unassigned slots still are.
        public bool CanJoin => entries.Count<slots.Length && exiting==null &&
            entries.Select((e,i)=>e.Slot==i && e.Position==slots[i]).All(x=>x);
        public IReadOnlyList<QueueAssignment> Snapshot => entries.Select(Assignment).ToList().AsReadOnly();
        public int AvailableSlotCount => slots.Length;
        public Vector3? NextAdmissionPosition => CanJoin ? slots[entries.Count] : (Vector3?)null;
        // 旧请求先由flow撤销。此标记允许按FIFO分配新目标，不伪造实际Arrived。
        public void RestartAssignments()
        { foreach(var entry in entries) { entry.Moving=false; entry.NeedsReplan=!entry.Arrived; } }
        private static QueueAssignment Assignment(Entry e) => new QueueAssignment(e.Id,e.Slot,e.Generation,e.Position,e.Arrived);
        public bool TryJoin(string id)
        {
            if(string.IsNullOrWhiteSpace(id) || id!=id.Trim() || entries.Exists(e=>e.Id==id) || !CanJoin) return false;
            entries.Add(new Entry{Id=id,Slot=entries.Count,Position=slots[entries.Count]}); return true;
        }
        public bool TryBeginFrontExit(out string id)
        {
            id=null;
            if(entries.Count==0 || !entries[0].Arrived || entries.Any(e=>e.Moving) || exiting!=null) return false;
            exiting=id=entries[0].Id; return true;
        }
        public bool Remove(string id)
        {
            var entry=entries.Find(e=>e.Id==id); if(entry==null) return false;
            entries.Remove(entry); if(exiting==id) exiting=null; return true;
        }
        public bool Complete(string id,int generation)
        {
            var entry=entries.Find(e=>e.Id==id);
            if(entry==null || entry.Generation!=generation || id==exiting) return false;
            entry.Arrived=true; entry.Moving=false; return true;
        }
        public bool TryGetNextAdvance(out QueueAssignment assignment,Func<QueueAssignment,bool> canAdvance=null)
        {
            assignment=default; if(exiting!=null) return false;
            for(var i=0;i<entries.Count;i++)
            {
                var entry=entries[i];
                // FIFO分配目标，每位独立完成；已开始的前客不再锁住后客。
                if(entry.Moving) continue;
                if(!entry.Arrived && !entry.NeedsReplan) return false;
                if(i>=slots.Length) return false; // 保留超额顾客的原位，不驱逐。
                if(!entry.NeedsReplan && entry.Slot==i && entry.Position==slots[i]) continue;
                var candidate=new QueueAssignment(entry.Id,i,entry.Generation+1,slots[i],false);
                if(canAdvance!=null && !canAdvance(candidate)) return false; // 冲突暂未腾空，不提交半份assignment。
                entry.Slot=i; entry.Position=slots[i]; entry.Generation++; entry.Arrived=false;
                entry.NeedsReplan=false;
                entry.Moving=true; assignment=Assignment(entry); return true;
            }
            return false;
        }
        public void UpdateSlots(IReadOnlyList<Vector3> value)
        {
            if(value==null) throw new ArgumentNullException(nameof(value));
            slots=value.ToArray();
            foreach(var p in slots) if(float.IsNaN(p.x+p.y+p.z) || float.IsInfinity(p.x+p.y+p.z)) throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}

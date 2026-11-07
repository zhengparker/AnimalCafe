using System.Collections.Generic;
using System;
using System.Linq;
using AnimalCafe.Navigation;
using UnityEngine;
namespace AnimalCafe.Customers
{
    public sealed class CounterQueueService
    {
        // Prepare against a copy; this Queue remains the only canonical FIFO owner.
        // 候选只在副本上验证；正式提交仍核对原Queue与单调Version。
        public enum MutationKind { Join, Advance }
        public long Version { get; private set; }
        private long preparationRevision;
        // Entry identity outlives unrelated metadata versions, but never a remove/rejoin replacement.
        // 只读引用原canonical owner；同值remove/rejoin仍是不同的Entry。
        public sealed class AssignmentClaim : ITrafficClaimCheckpoint
        {
            private readonly CounterQueueService owner;
            private readonly Entry entry;
            private readonly bool arrived,moving,needsReplan,ownExiting;
            private readonly long preparationRevision;
            internal AssignmentClaim(CounterQueueService owner,QueueAssignment assignment)
            {
                this.owner=owner;Assignment=assignment;entry=owner.entries.Find(e=>e.Id==assignment.VisitId);
                arrived=entry!=null && entry.Arrived;moving=entry!=null && entry.Moving;needsReplan=entry!=null && entry.NeedsReplan;
                ownExiting=owner.exiting==assignment.VisitId;preparationRevision=owner.preparationRevision;
                CapturedOwnerVersion=owner.Version;
            }
            public object CanonicalOwner => owner;
            public string ResourceId => "counter/"+Assignment.VisitId;
            public long CapturedOwnerVersion { get; }
            private bool SemanticCurrent => IsCurrent && entry.Arrived==arrived && entry.Moving==moving && entry.NeedsReplan==needsReplan &&
                (owner.exiting==Assignment.VisitId)==ownExiting && owner.preparationRevision==preparationRevision;
            public bool IsSameCurrentClaim(ITrafficClaimCheckpoint current) => current is AssignmentClaim other &&
                ReferenceEquals(owner,other.owner) && ReferenceEquals(entry,other.entry) &&
                CapturedOwnerVersion<=other.CapturedOwnerVersion && other.CapturedOwnerVersion==owner.Version &&
                SemanticCurrent && other.SemanticCurrent;
            public QueueAssignment Assignment { get; }
            public bool IsCurrent => entry!=null && ReferenceEquals(owner.entries.Find(e=>e.Id==Assignment.VisitId),entry) &&
                entry.Generation==Assignment.Generation && entry.Slot==Assignment.SlotIndex && entry.Position==Assignment.Position;
            // Lineage is separate from one assignment generation. / 同一预留Entry不等于同一代目标。
            public bool IsSameReservation(AssignmentClaim other) => other!=null && entry!=null &&
                ReferenceEquals(owner,other.owner) && ReferenceEquals(entry,other.entry) && owner.entries.Contains(entry);
        }
        public bool TryCaptureAssignment(string id,out AssignmentClaim claim)
        {
            claim=null;var entry=entries.Find(e=>e.Id==id);if(entry==null)return false;
            claim=new AssignmentClaim(this,Assignment(entry));return true;
        }
        public sealed class PreparedMutation : IPreparedTrafficMutation
        {
            private readonly CounterQueueService owner;
            internal readonly MutationKind Kind;
            internal readonly string Id;
            internal readonly long ExpectedVersion;
            internal bool Consumed;
            internal PreparedMutation(CounterQueueService owner,MutationKind kind,string id,long version,QueueAssignment assignment)
            { this.owner=owner;Kind=kind;Id=id;ExpectedVersion=version;Assignment=assignment; }
            public QueueAssignment Assignment { get; }
            public bool IsCurrent => !Consumed && owner.Version==ExpectedVersion;
            internal bool BelongsTo(CounterQueueService queue) => ReferenceEquals(owner,queue);
            public bool TryApply(out ITrafficMutationReceipt receipt) => owner.TryApplyPrepared(this,out receipt);
        }
        public bool TryPrepareMutation(MutationKind kind,string ownerId,long expectedVersion,out PreparedMutation prepared)
        {
            prepared=null;
            if(expectedVersion!=Version || !ValidId(ownerId) || !Enum.IsDefined(typeof(MutationKind),kind)) return false;
            var preview=new CounterQueueService(this);
            if(!preview.Apply(kind,ownerId,out var assignment)) return false;
            prepared=new PreparedMutation(this,kind,ownerId,expectedVersion,assignment);return true;
        }
        public bool TryApplyPrepared(PreparedMutation prepared,out ITrafficMutationReceipt receipt)
        {
            receipt=null;
            if(prepared==null || !prepared.BelongsTo(this) || !prepared.IsCurrent) return false;
            var undo=new MutationReceipt(this,prepared.Id);
            prepared.Consumed=true;
            if(!Apply(prepared.Kind,prepared.Id,out _)) return false;
            undo.Applied(Version);receipt=undo;return true;
        }
        private bool Apply(MutationKind kind,string id,out QueueAssignment assignment)
        {
            assignment=default;
            if(kind==MutationKind.Join)
            {
                if(!TryJoin(id))return false;
                assignment=Assignment(entries[entries.Count-1]);return true;
            }
            return kind==MutationKind.Advance && TryGetNextAdvance(out assignment,candidate=>candidate.VisitId==id);
        }
        private CounterQueueService(CounterQueueService source)
        {
            slots=source.slots;exiting=source.exiting;Version=source.Version;preparationRevision=source.preparationRevision;
            entries.AddRange(source.entries.Select(Copy));
        }
        private static Entry Copy(Entry entry) => entry==null?null:new Entry
        {Id=entry.Id,Slot=entry.Slot,Generation=entry.Generation,Position=entry.Position,Arrived=entry.Arrived,NeedsReplan=entry.NeedsReplan,Moving=entry.Moving};
        private static bool ValidId(string id) => !string.IsNullOrWhiteSpace(id) && id==id.Trim();
        private sealed class MutationReceipt : ITrafficMutationReceipt
        {
            private readonly CounterQueueService owner;
            private readonly string id;
            private readonly Entry original;
            private readonly int originalIndex;
            private long appliedVersion;
            private bool retired;
            internal MutationReceipt(CounterQueueService owner,string id)
            {
                this.owner=owner;this.id=id;
                originalIndex=owner.entries.FindIndex(entry=>entry.Id==id);
                original=originalIndex<0?null:Copy(owner.entries[originalIndex]);
            }
            internal void Applied(long version) { appliedVersion=version; }
            public bool IsCurrent => !retired && owner.Version==appliedVersion;
            public void RollbackOwnMutation()
            {
                if(retired)return;
                if(!IsCurrent)throw new InvalidOperationException("Cannot roll back another Queue mutation.");
                retired=true;
                owner.entries.RemoveAll(entry=>entry.Id==id);
                if(original!=null)owner.entries.Insert(originalIndex,Copy(original));
                // Undo only our unpublished candidate; never revive an older prepared version.
                // 只撤销本次候选；Version不倒退，旧许可不能因rollback复活。
                owner.Version++;
            }
        }

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
        {
            preparationRevision++;
            foreach(var entry in entries) { entry.Moving=false; entry.NeedsReplan=!entry.Arrived; }
            Version++; // Explicit restart invalidates in-flight preparation even when targets stay unchanged.
        }
        // Integrated recovery restarts only genuinely blocked Counter entries; healthy claim semantics stay valid.
        // 仍递增Version使旧未提交preview失效；不改其他条目，也不递增全员preparationRevision。
        internal void RestartAssignments(IReadOnlyCollection<string> visitIds)
        {
            if(visitIds==null)throw new ArgumentNullException(nameof(visitIds));
            var retry=entries.Where(entry=>visitIds.Contains(entry.Id)).ToArray();
            if(retry.Length==0)return;
            foreach(var entry in retry){entry.Moving=false;entry.NeedsReplan=!entry.Arrived;}
            Version++;
        }
        private static QueueAssignment Assignment(Entry e) => new QueueAssignment(e.Id,e.Slot,e.Generation,e.Position,e.Arrived);
        public bool TryJoin(string id)
        {
            if(!ValidId(id) || entries.Exists(e=>e.Id==id) || !CanJoin) return false;
            entries.Add(new Entry{Id=id,Slot=entries.Count,Position=slots[entries.Count]});Version++;return true;
        }
        public bool TryBeginFrontExit(out string id)
        {
            id=null;
            if(entries.Count==0 || !entries[0].Arrived || entries.Any(e=>e.Moving) || exiting!=null) return false;
            exiting=id=entries[0].Id;Version++;return true;
        }
        public bool Remove(string id)
        {
            var entry=entries.Find(e=>e.Id==id); if(entry==null) return false;
            entries.Remove(entry); if(exiting==id) exiting=null;Version++;return true;
        }
        public bool Complete(string id,int generation)
        {
            var entry=entries.Find(e=>e.Id==id);
            if(entry==null || entry.Generation!=generation || id==exiting) return false;
            if(!entry.Arrived || entry.Moving) { entry.Arrived=true;entry.Moving=false;Version++; }
            return true;
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
                var expectedVersion=Version;
                if(canAdvance!=null && !canAdvance(candidate)) return false; // 冲突暂未腾空，不提交半份assignment。
                // User callbacks may remove/replace this owner, change slots, or restart assignments.
                // callback返回true仍须复核；不能发布失效Entry的assignment。
                if(Version!=expectedVersion || !ReferenceEquals(entries.Find(e=>e.Id==entry.Id),entry)) return false;
                entry.Slot=i; entry.Position=slots[i]; entry.Generation++; entry.Arrived=false;
                entry.NeedsReplan=false;
                entry.Moving=true;Version++;assignment=Assignment(entry);return true;
            }
            return false;
        }
        public void UpdateSlots(IReadOnlyList<Vector3> value)
        {
            if(value==null) throw new ArgumentNullException(nameof(value));
            var candidate=value.ToArray();
            foreach(var p in candidate) if(float.IsNaN(p.x+p.y+p.z) || float.IsInfinity(p.x+p.y+p.z)) throw new ArgumentOutOfRangeException(nameof(value));
            if(slots!=null && slots.SequenceEqual(candidate))return;
            slots=candidate;Version++;
        }
    }
}

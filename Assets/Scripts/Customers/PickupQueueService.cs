using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Navigation;
using UnityEngine;

namespace AnimalCafe.Customers
{
    public sealed class PickupQueueService
    {
        // Preparation belongs to one canonical Queue and never publishes business events.
        // 先用独立副本验证操作；正式apply仍核对原owner与单调version。
        public enum MutationKind { Reserve, Transfer, Advance, BeginYield, ReturnYield, DeliveryLease, CollectionLease, YieldedCollectionLease }
        public long Version { get; private set; }
        // Read-only canonical identity; no second reservation or lease allocator.
        // 只读原Entry及当时的collection lease，不复用已Consumed的mutation。
        public sealed class ReservationClaim : ITrafficClaimCheckpoint
        {
            private readonly PickupQueueService owner;
            private readonly Entry entry;
            private readonly bool collectionLease;
            private readonly bool paid,arrived,advancing,yielding,hasCollectionLease;
            private readonly Vector3 ownCollectionPosition;
            internal ReservationClaim(PickupQueueService owner,QueueAssignment assignment)
            {
                this.owner=owner;Assignment=assignment;
                owner.reservations.TryGetValue(assignment.VisitId,out entry);
                collectionLease=owner.collectionOwner==assignment.VisitId && owner.collectionPosition==assignment.Position;
                paid=entry!=null && entry.Paid;arrived=entry!=null && entry.Arrived;
                advancing=entry!=null && entry.Advancing;yielding=entry!=null && entry.Yielding;
                hasCollectionLease=owner.collectionOwner==assignment.VisitId;
                ownCollectionPosition=hasCollectionLease?owner.collectionPosition:default(Vector3);
                CapturedOwnerVersion=owner.Version;
            }
            public object CanonicalOwner => owner;
            public string ResourceId => "pickup/"+Assignment.VisitId;
            public long CapturedOwnerVersion { get; }
            private bool SemanticCurrent => IsCurrent && entry.Paid==paid && entry.Arrived==arrived &&
                entry.Advancing==advancing && entry.Yielding==yielding &&
                (owner.collectionOwner==Assignment.VisitId)==hasCollectionLease &&
                (!hasCollectionLease || owner.collectionPosition==ownCollectionPosition);
            public bool IsSameCurrentClaim(ITrafficClaimCheckpoint current) => current is ReservationClaim other &&
                ReferenceEquals(owner,other.owner) && ReferenceEquals(entry,other.entry) &&
                CapturedOwnerVersion<=other.CapturedOwnerVersion && other.CapturedOwnerVersion==owner.Version &&
                SemanticCurrent && other.SemanticCurrent;
            public QueueAssignment Assignment { get; }
            private bool OwnsEntry => entry!=null && owner.reservations.TryGetValue(Assignment.VisitId,out var current) && ReferenceEquals(current,entry);
            public bool IsCurrent => OwnsEntry && entry.Generation==Assignment.Generation &&
                entry.Slot==Assignment.SlotIndex && entry.Target==Assignment.Position;
            // Actual reacquisition already advances Entry.Generation; an old lease cannot revive.
            // release清owner，重新acquire已有新generation；false本身不证明实际release。
            public bool IsCurrentCollectionLease => collectionLease && IsCurrent &&
                owner.collectionOwner==Assignment.VisitId && owner.collectionPosition==Assignment.Position;
            public bool IsSameReservation(ReservationClaim other) => other!=null &&
                ReferenceEquals(owner,other.owner) && ReferenceEquals(entry,other.entry) && OwnsEntry;
        }
        public bool TryCaptureReservation(string visitId,out ReservationClaim claim)
        {
            claim=null;if(!ValidId(visitId) || !reservations.TryGetValue(visitId,out var entry))return false;
            claim=new ReservationClaim(this,Assignment(entry));return true;
        }
        // Identity evidence belongs to one actual lease, independently of unrelated Queue.Version changes.
        // token只证明真实lease身份；一旦retired便不复活，不参与调度政策。
        private sealed class DeliveryLeaseLifetime
        {
            internal DeliveryLeaseLifetime() { }
            internal bool Retired;
        }
        public sealed class DeliveryLeaseClaim
        {
            private readonly PickupQueueService owner;
            private readonly DeliveryLeaseLifetime lifetime;
            private DeliveryLeaseClaim(PickupQueueService owner)
            {
                this.owner=owner;lifetime=owner.deliveryLifetime;
                ActorId=owner.deliveryOwner;Position=owner.deliveryPosition;
            }
            public string ActorId { get; }
            public Vector3 Position { get; }
            public bool IsCurrent => !lifetime.Retired && ReferenceEquals(owner.deliveryLifetime,lifetime) && owner.deliveryOwner==ActorId;
            public bool HasRetiredLifetime => lifetime.Retired;
            public bool IsSameLease(DeliveryLeaseClaim other) => other!=null &&
                ReferenceEquals(owner,other.owner) && ReferenceEquals(lifetime,other.lifetime);
            internal static bool TryCapture(PickupQueueService owner,string employeeId,out DeliveryLeaseClaim claim)
            {
                claim=null;
                if(!ValidId(employeeId) || owner.deliveryOwner!=employeeId || owner.deliveryLifetime==null || owner.deliveryLifetime.Retired)return false;
                claim=new DeliveryLeaseClaim(owner);return true;
            }
        }
        public bool TryCaptureDeliveryLease(string employeeId,out DeliveryLeaseClaim claim)
            => DeliveryLeaseClaim.TryCapture(this,employeeId,out claim);

        // Ordinary AcquireCollection eligibility, with the actual delivery lease as the sole blocker.
        // 只读原head/lease证据；不是Preview、Collection许可或独立进展证明。
        internal bool TryCaptureCollectionReleasePrerequisite(string visitId,Vector3 target,
            out ReservationClaim reservation,out DeliveryLeaseClaim delivery)
        {
            reservation=null;delivery=null;
            if(!ValidId(visitId) || !Finite(target) || paidQueue.Count==0 || paidQueue[0].Id!=visitId ||
                ServiceSides!=null && !ServiceSides.Contains(target) || collectionOwner!=null)return false;
            var head=paidQueue[0];
            if(!head.Arrived || head.Yielding || deliveryOwner==null ||
                !(ServiceSides==null?sharedServicePosition:deliveryPosition==target) ||
                !reservations.TryGetValue(visitId,out var actual) || !ReferenceEquals(actual,head) ||
                !DeliveryLeaseClaim.TryCapture(this,deliveryOwner,out var held))return false;
            reservation=new ReservationClaim(this,Assignment(head));delivery=held;return true;
        }

        public sealed class PreparedMutation : IPreparedTrafficMutation
        {
            private readonly PickupQueueService owner;
            internal readonly MutationKind Kind;
            internal readonly string Id;
            internal readonly Vector3? Target;
            internal readonly long ExpectedVersion;
            internal bool Consumed;
            internal PreparedMutation(PickupQueueService owner,MutationKind kind,string id,long version,Vector3? target,QueueAssignment assignment)
            { this.owner=owner;Kind=kind;Id=id;ExpectedVersion=version;Target=target;Assignment=assignment; }
            public QueueAssignment Assignment { get; }
            public bool IsCurrent => !Consumed && owner.Version==ExpectedVersion;
            internal bool BelongsTo(PickupQueueService queue) => ReferenceEquals(owner,queue);
            public bool TryApply(out ITrafficMutationReceipt receipt) => owner.TryApplyPrepared(this,out receipt);
        }
        public bool TryPrepareMutation(MutationKind kind,string ownerId,long expectedVersion,
            out PreparedMutation prepared,Vector3? target=null)
        {
            prepared=null;
            if(expectedVersion!=Version || !ValidId(ownerId) || !Enum.IsDefined(typeof(MutationKind),kind) || target.HasValue && !Finite(target.Value)) return false;
            var preview=CopyForPreview();
            if(!preview.Apply(kind,ownerId,target,out var assignment)) return false;
            prepared=new PreparedMutation(this,kind,ownerId,expectedVersion,target,assignment);return true;
        }
        public bool TryApplyPrepared(PreparedMutation prepared,out ITrafficMutationReceipt receipt)
        {
            receipt=null;
            if(prepared==null || !prepared.BelongsTo(this) || !prepared.IsCurrent) return false;
            var undo=new MutationReceipt(this,prepared.Id);
            prepared.Consumed=true;
            if(!Apply(prepared.Kind,prepared.Id,prepared.Target,out _)) return false;
            undo.Applied(Version);receipt=undo;return true;
        }
        private bool Apply(MutationKind kind,string id,Vector3? target,out QueueAssignment assignment)
        {
            assignment=default;
            switch(kind)
            {
                case MutationKind.Reserve: return TryReserve(id,out assignment);
                case MutationKind.Transfer: return TryBeginTransfer(id,out assignment);
                case MutationKind.Advance: return TryGetNextAdvance(out assignment,a=>a.VisitId==id);
                case MutationKind.BeginYield: return target.HasValue && TryBeginYield(id,target.Value,out assignment);
                case MutationKind.ReturnYield: return TryReturnYielded(id,out assignment);
                case MutationKind.DeliveryLease:
                    if(!target.HasValue || !TryAcquireDelivery(id,target.Value)) return false;
                    assignment=new QueueAssignment(id,-1,generation,target.Value,false);return true;
                case MutationKind.CollectionLease:
                case MutationKind.YieldedCollectionLease:
                    if(!target.HasValue || !(kind==MutationKind.CollectionLease?TryAcquireCollection(id,target.Value):TryAcquireYieldedCollection(id,target.Value))) return false;
                    return TryGetReservation(id,out assignment);
                default: return false;
            }
        }
        private PickupQueueService CopyForPreview()
        {
            var copy=new PickupQueueService(this);
            foreach(var pair in reservations) copy.reservations.Add(pair.Key,Copy(pair.Value));
            foreach(var entry in paidQueue) copy.paidQueue.Add(copy.reservations[entry.Id]);
            return copy;
        }
        private PickupQueueService(PickupQueueService source)
        {
            waitSlots=source.waitSlots;servicePosition=source.servicePosition;sharedServicePosition=source.sharedServicePosition;serviceSides=source.serviceSides;
            generation=source.generation;Version=source.Version;deliveryOwner=source.deliveryOwner;collectionOwner=source.collectionOwner;
            deliveryPosition=source.deliveryPosition;collectionPosition=source.collectionPosition;
            // Preview/copy release must never retire the canonical Queue's token.
            // 副本持有独立lifetime；不共享可retire的原token。
            if(source.deliveryLifetime!=null && !source.deliveryLifetime.Retired)deliveryLifetime=new DeliveryLeaseLifetime();
        }
        private static Entry Copy(Entry entry) => entry==null?null:new Entry
        {Id=entry.Id,Slot=entry.Slot,Generation=entry.Generation,Target=entry.Target,Paid=entry.Paid,Arrived=entry.Arrived,Advancing=entry.Advancing,Yielding=entry.Yielding};
        private sealed class MutationReceipt : ITrafficMutationReceipt
        {
            private readonly PickupQueueService owner;
            private readonly string id;
            private readonly Entry original;
            private readonly int paidIndex;
            private readonly bool delivery,collection;
            private readonly DeliveryLeaseLifetime originalDeliveryLifetime;
            private readonly Vector3 deliveryPose,collectionPose;
            private readonly long beforeVersion;
            private long appliedVersion;
            private bool retired;
            internal MutationReceipt(PickupQueueService owner,string id)
            {
                this.owner=owner;this.id=id;beforeVersion=owner.Version;
                owner.reservations.TryGetValue(id,out var entry);original=Copy(entry);paidIndex=owner.paidQueue.FindIndex(e=>e.Id==id);
                delivery=owner.deliveryOwner==id;collection=owner.collectionOwner==id;
                originalDeliveryLifetime=delivery?owner.deliveryLifetime:null;
                deliveryPose=owner.deliveryPosition;collectionPose=owner.collectionPosition;
            }
            internal void Applied(long version) { appliedVersion=version; }
            public bool IsCurrent => !retired && owner.Version==appliedVersion;
            public void RollbackOwnMutation()
            {
                if(retired) return;
                if(!IsCurrent) throw new InvalidOperationException("Cannot roll back another Queue mutation.");
                retired=true;if(appliedVersion==beforeVersion) return;
                owner.reservations.Remove(id);owner.paidQueue.RemoveAll(e=>e.Id==id);
                if(original!=null)
                { var restored=Copy(original);owner.reservations.Add(id,restored);if(paidIndex>=0)owner.paidQueue.Insert(paidIndex,restored); }
                var preserveDeliveryLifetime=delivery && owner.deliveryOwner==id && originalDeliveryLifetime!=null &&
                    !originalDeliveryLifetime.Retired && ReferenceEquals(owner.deliveryLifetime,originalDeliveryLifetime);
                if(owner.deliveryOwner==id)
                {if(!preserveDeliveryLifetime)owner.RetireDeliveryLifetime();owner.deliveryOwner=null;}
                if(owner.collectionOwner==id)owner.collectionOwner=null;
                if(delivery)
                {
                    owner.deliveryOwner=id;owner.deliveryPosition=deliveryPose;
                    // Only uninterrupted identity can survive restoration; retired evidence stays retired.
                    // 真正退休后的restore另建lifetime，不重用旧claim身份。
                    if(!preserveDeliveryLifetime)owner.deliveryLifetime=new DeliveryLeaseLifetime();
                }
                if(collection){owner.collectionOwner=id;owner.collectionPosition=collectionPose;}
                // Global generation and Version never move backwards; old proposals cannot become current again.
                owner.Version++;
            }
        }

        private sealed class Entry
        {
            public string Id;
            public int Slot;
            public int Generation;
            public Vector3 Target;
            public bool Paid;
            public bool Arrived;
            public bool Advancing;
            public bool Yielding;
        }

        private readonly Vector3[] waitSlots;
        private readonly Vector3 servicePosition;
        private readonly bool sharedServicePosition;
        private readonly Dictionary<string, Entry> reservations = new Dictionary<string, Entry>();
        private readonly List<Entry> paidQueue = new List<Entry>();
        private string deliveryOwner;
        private DeliveryLeaseLifetime deliveryLifetime;
        private string collectionOwner;
        private readonly Vector3[] serviceSides;
        private Vector3[] ServiceSides => serviceSides;
        private Vector3 deliveryPosition,collectionPosition;
        private int generation;

        private void RetireDeliveryLifetime()
        {
            if(deliveryLifetime!=null)deliveryLifetime.Retired=true;
            deliveryLifetime=null;
        }

        public PickupQueueService(IReadOnlyList<Vector3> waitSlots,IReadOnlyList<Vector3> servicePositions)
            :this(waitSlots,FirstSide(servicePositions),false)
        {
            serviceSides=servicePositions.ToArray();
            if(serviceSides.Any(p=>!Finite(p)) || serviceSides.Distinct().Count()!=serviceSides.Length)
                throw new ArgumentException("Service sides must be finite and distinct.",nameof(servicePositions));
            if(serviceSides.Length==1 && this.waitSlots.Contains(serviceSides[0]))
                throw new ArgumentException("A single service side needs a separate waiting slot.",nameof(waitSlots));
        }
        private static Vector3 FirstSide(IReadOnlyList<Vector3> positions)
        {
            if(positions==null) throw new ArgumentNullException(nameof(positions));
            if(positions.Count==0) throw new ArgumentException("At least one service side is required.",nameof(positions));
            return positions[0];
        }
        public IReadOnlyDictionary<string,Vector3> ServiceLeases
        {
            get
            {
                var result=new Dictionary<string,Vector3>();
                if(deliveryOwner!=null) result.Add(deliveryOwner,deliveryPosition);
                if(collectionOwner!=null) result.Add(collectionOwner,collectionPosition);
                return result;
            }
        }
        public bool TryGetServiceLease(string actorId,out Vector3 position)
        {
            position=default;
            if(!ValidId(actorId)) return false;
            if(actorId==deliveryOwner) { position=deliveryPosition; return true; }
            if(actorId==collectionOwner) { position=collectionPosition; return true; }
            return false;
        }

        public PickupQueueService(IReadOnlyList<Vector3> waitSlots, Vector3 servicePosition,
            bool sharedServicePosition)
        {
            if (waitSlots == null) throw new ArgumentNullException(nameof(waitSlots));
            if (!Finite(servicePosition)) throw new ArgumentOutOfRangeException(nameof(servicePosition));
            this.waitSlots = waitSlots.ToArray();
            for (var i = 0; i < this.waitSlots.Length; i++)
            {
                var slot = this.waitSlots[i];
                if (!Finite(slot)) throw new ArgumentOutOfRangeException(nameof(waitSlots));
                if (sharedServicePosition && slot == servicePosition)
                    throw new ArgumentException("共享服务点不能充当等候位 / Shared service needs a separate wait slot.", nameof(waitSlots));
                for (var j = 0; j < i; j++)
                    if (slot == this.waitSlots[j])
                        throw new ArgumentException("等候位不能重复 / Duplicate wait slot.", nameof(waitSlots));
            }
            this.servicePosition = servicePosition;
            this.sharedServicePosition = sharedServicePosition;
        }

        // 预留不等于付款；snapshot只按成功点单顺序列队 / Paid FIFO only.
        public IReadOnlyList<QueueAssignment> Snapshot => paidQueue.Select(Assignment).ToList().AsReadOnly();
        // 不把前方空位分给新尾客；前移的旧身体实际到位后才重新开放admission。
        // Gaps belong to existing customers, not a new arrival.
        public bool CanReserve => reservations.Count < waitSlots.Length &&
            !reservations.Values.Any(entry => entry.Advancing) &&
            Enumerable.Range(0, reservations.Count).All(slot => reservations.Values.Any(entry => entry.Slot == slot));

        public bool TryGetNextAdvance(out QueueAssignment assignment, Func<QueueAssignment, bool> canAdvance = null)
        {
            assignment = default;
            // Remove不代表service身体已离位 / Wait for the actual lease release.
            if (collectionOwner != null) return false;
            foreach (var entry in paidQueue)
            {
                if (entry.Advancing) continue; // 已发出前客目标后，允许上层检查后客实际路线。
                if (!entry.Arrived) return false;
                for (var slot = 0; slot < entry.Slot; slot++)
                {
                    // 空位也不能越过尚在前移的付费前客 / Preserve physical paid FIFO.
                    if (paidQueue.TakeWhile(earlier => earlier != entry).Any(earlier => earlier.Slot >= slot)) continue;
                    if (reservations.Values.Any(reserved => reserved.Slot == slot)) continue;
                    var candidate = new QueueAssignment(entry.Id, slot, generation + 1, waitSlots[slot], false);
                    // NavWorld/旧身体/路径检查拒绝时，保留原目标和generation，绝不半提交。
                    var expectedVersion=Version;
                    if (canAdvance != null && !canAdvance(candidate)) return false;
                    if(Version!=expectedVersion || !reservations.TryGetValue(entry.Id,out var current) || !ReferenceEquals(current,entry)) return false;
                    entry.Slot = slot;
                    entry.Target = waitSlots[slot];
                    entry.Generation = ++generation;
                    entry.Arrived = false;
                    entry.Advancing = true;
                    Version++;
                    assignment = Assignment(entry);
                    return true;
                }
            }
            return false;
        }

        // A route certificate may expire while the real parking reservation remains.
        // 只读真实预留；查询不存在的顾客时绝不新建预留。
        public bool TryGetReservation(string visitId, out QueueAssignment assignment)
        {
            assignment = default;
            if (!ValidId(visitId) || !reservations.TryGetValue(visitId, out var entry)) return false;
            assignment = Assignment(entry);
            return true;
        }

        public bool TryReserve(string visitId, out QueueAssignment assignment)
        {
            assignment = default;
            if (!ValidId(visitId)) return false;
            if (reservations.TryGetValue(visitId, out var existing))
            { assignment = Assignment(existing); return true; }
            // 已退休的visit不能继承尚未实际让出的旧lease / No lease identity reuse.
            if (visitId == collectionOwner || visitId == deliveryOwner) return false;
            if (!CanReserve) return false;
            var slot = reservations.Count;
            var entry = new Entry
            { Id = visitId, Slot = slot, Generation = ++generation, Target = waitSlots[slot] };
            reservations.Add(visitId, entry);
            Version++;
            assignment = Assignment(entry);
            return true;
        }

        public bool TryBeginTransfer(string visitId, out QueueAssignment assignment)
        {
            assignment = default;
            if (!ValidId(visitId) || !reservations.TryGetValue(visitId, out var entry)) return false;
            if (!entry.Paid)
            {
                // 上层成功提交付款/Order后调用；早期reserve顺序不能改变FIFO。
                entry.Paid = true;
                entry.Generation = ++generation;
                paidQueue.Add(entry);
                Version++;
            }
            assignment = Assignment(entry);
            return true;
        }

        public bool MarkArrived(string visitId, int generation)
        {
            if (!ValidId(visitId) || !reservations.TryGetValue(visitId, out var entry) ||
                !entry.Paid || entry.Generation != generation) return false;
            if(entry.Arrived && !entry.Advancing) return true;
            entry.Arrived = true;
            entry.Advancing = false;
            Version++;
            return true;
        }

        // 临时目标仍属于原slot；不释放FIFO/容量，也不允许新客抢占。
        internal bool TryBeginYield(string visitId,Vector3 target,out QueueAssignment assignment)
        {
            assignment=default;
            if(!Finite(target) || !reservations.TryGetValue(visitId,out var entry) || !entry.Paid ||
                !entry.Arrived || entry.Advancing || entry.Yielding || visitId==collectionOwner || visitId==deliveryOwner) return false;
            entry.Yielding=true;entry.Advancing=true;entry.Arrived=false;entry.Target=target;entry.Generation=++generation;
            Version++;
            assignment=Assignment(entry);return true;
        }
        internal bool MarkYieldArrived(string visitId,int expectedGeneration)
        {
            if(!reservations.TryGetValue(visitId,out var entry) || !entry.Yielding || entry.Generation!=expectedGeneration) return false;
            if(!entry.Arrived){entry.Arrived=true;Version++;}return true; // Advancing stays true until the original slot is reached again.
        }
        internal bool TryReturnYielded(string visitId,out QueueAssignment assignment)
        {
            assignment=default;
            if(!reservations.TryGetValue(visitId,out var entry) || !entry.Yielding || !entry.Arrived) return false;
            entry.Target=waitSlots[entry.Slot];entry.Yielding=false;entry.Arrived=false;entry.Generation=++generation;
            Version++;
            assignment=Assignment(entry);return true;
        }
        internal bool TryGetYieldReturn(string visitId,out QueueAssignment assignment)
        {
            assignment=default;
            if(!reservations.TryGetValue(visitId,out var entry) || !entry.Yielding || !entry.Arrived) return false;
            assignment=new QueueAssignment(entry.Id,entry.Slot,generation+1,waitSlots[entry.Slot],false);return true;
        }

        public bool TryAcquireDelivery(string employeeId) => TryAcquireDelivery(employeeId,servicePosition);
        public bool TryAcquireDelivery(string employeeId,Vector3 position)
        {
            if (!ValidId(employeeId) || reservations.ContainsKey(employeeId) || employeeId == collectionOwner)
                return false;
            if(ServiceSides!=null && !ServiceSides.Contains(position)) return false;
            if (deliveryOwner != null) return deliveryOwner == employeeId && (ServiceSides==null || deliveryPosition==position);
            if (collectionOwner != null && (ServiceSides==null?sharedServicePosition:collectionPosition==position)) return false;
            deliveryLifetime=new DeliveryLeaseLifetime();
            deliveryOwner = employeeId;
            deliveryPosition=position;
            Version++;
            return true;
        }

        public bool TryAcquireCollection(string visitId) => TryAcquireCollection(visitId,servicePosition);
        public bool TryAcquireCollection(string visitId,Vector3 position)
            => AcquireCollection(visitId,position,false);
        internal bool TryAcquireYieldedCollection(string visitId,Vector3 position)
        {
            var before=Version;
            if(!reservations.TryGetValue(visitId,out var entry) || !entry.Yielding || !AcquireCollection(visitId,position,true)) return false;
            entry.Yielding=false;if(Version==before)Version++;return true;
        }
        private bool AcquireCollection(string visitId,Vector3 position,bool allowYielded)
        {
            if (!ValidId(visitId) || paidQueue.Count == 0 || paidQueue[0].Id != visitId) return false;
            if(ServiceSides!=null && !ServiceSides.Contains(position)) return false;
            if (collectionOwner != null) return collectionOwner == visitId && (ServiceSides==null || collectionPosition==position);
            var head = paidQueue[0];
            if (!head.Arrived || head.Yielding && !allowYielded || deliveryOwner!=null && (ServiceSides==null?sharedServicePosition:deliveryPosition==position)) return false;
            collectionOwner = visitId;
            collectionPosition=position;
            // lease仅授予移动权限；旧wait到达回执不能冒充service到达。
            // The original wait slot stays reserved until actual departure/removal.
            head.Target = position;
            head.Generation = ++generation;
            head.Arrived = false;
            Version++;
            return true;
        }

        // 只能由实际离位事件调用；放杯、Remove和错误actor都不能提前放行身体。
        // Delivery commit does not release the physical service lease.
        public void ReleaseServicePosition(string actorId)
        {
            if (!ValidId(actorId)) return;
            var changed=deliveryOwner==actorId || collectionOwner==actorId;
            if (deliveryOwner == actorId) { RetireDeliveryLifetime(); deliveryOwner = null; }
            if (collectionOwner == actorId) collectionOwner = null;
            if(changed)Version++;
        }

        public bool Remove(string visitId)
        {
            if (!ValidId(visitId) || !reservations.TryGetValue(visitId, out var entry)) return false;
            reservations.Remove(visitId);
            paidQueue.Remove(entry);
            Version++;
            return true;
        }

        private static QueueAssignment Assignment(Entry entry)
            => new QueueAssignment(entry.Id, entry.Slot, entry.Generation, entry.Target, entry.Arrived);
        private static bool ValidId(string id) => !string.IsNullOrWhiteSpace(id) && id == id.Trim();
        private static bool Finite(Vector3 point)
            => !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
               !float.IsNaN(point.y) && !float.IsInfinity(point.y) &&
               !float.IsNaN(point.z) && !float.IsInfinity(point.z);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AnimalCafe.Customers
{
    public sealed class PickupQueueService
    {
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
        private string collectionOwner;
        private readonly Vector3[] serviceSides;
        private Vector3 deliveryPosition,collectionPosition;
        private int generation;

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
                    if (canAdvance != null && !canAdvance(candidate)) return false;
                    entry.Slot = slot;
                    entry.Target = waitSlots[slot];
                    entry.Generation = ++generation;
                    entry.Arrived = false;
                    entry.Advancing = true;
                    assignment = Assignment(entry);
                    return true;
                }
            }
            return false;
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
            }
            assignment = Assignment(entry);
            return true;
        }

        public bool MarkArrived(string visitId, int generation)
        {
            if (!ValidId(visitId) || !reservations.TryGetValue(visitId, out var entry) ||
                !entry.Paid || entry.Generation != generation) return false;
            entry.Arrived = true;
            entry.Advancing = false;
            return true;
        }

        // 临时目标仍属于原slot；不释放FIFO/容量，也不允许新客抢占。
        internal bool TryBeginYield(string visitId,Vector3 target,out QueueAssignment assignment)
        {
            assignment=default;
            if(!Finite(target) || !reservations.TryGetValue(visitId,out var entry) || !entry.Paid ||
                !entry.Arrived || entry.Advancing || entry.Yielding || visitId==collectionOwner || visitId==deliveryOwner) return false;
            entry.Yielding=true;entry.Advancing=true;entry.Arrived=false;entry.Target=target;entry.Generation=++generation;
            assignment=Assignment(entry);return true;
        }
        internal bool MarkYieldArrived(string visitId,int expectedGeneration)
        {
            if(!reservations.TryGetValue(visitId,out var entry) || !entry.Yielding || entry.Generation!=expectedGeneration) return false;
            entry.Arrived=true;return true; // Advancing stays true until the original slot is reached again.
        }
        internal bool TryReturnYielded(string visitId,out QueueAssignment assignment)
        {
            assignment=default;
            if(!reservations.TryGetValue(visitId,out var entry) || !entry.Yielding || !entry.Arrived) return false;
            entry.Target=waitSlots[entry.Slot];entry.Yielding=false;entry.Arrived=false;entry.Generation=++generation;
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
            if(serviceSides!=null && !serviceSides.Contains(position)) return false;
            if (deliveryOwner != null) return deliveryOwner == employeeId && (serviceSides==null || deliveryPosition==position);
            if (collectionOwner != null && (serviceSides==null?sharedServicePosition:collectionPosition==position)) return false;
            deliveryOwner = employeeId;
            deliveryPosition=position;
            return true;
        }

        public bool TryAcquireCollection(string visitId) => TryAcquireCollection(visitId,servicePosition);
        public bool TryAcquireCollection(string visitId,Vector3 position)
            => AcquireCollection(visitId,position,false);
        internal bool TryAcquireYieldedCollection(string visitId,Vector3 position)
        {
            if(!reservations.TryGetValue(visitId,out var entry) || !entry.Yielding || !AcquireCollection(visitId,position,true)) return false;
            entry.Yielding=false;return true;
        }
        private bool AcquireCollection(string visitId,Vector3 position,bool allowYielded)
        {
            if (!ValidId(visitId) || paidQueue.Count == 0 || paidQueue[0].Id != visitId) return false;
            if(serviceSides!=null && !serviceSides.Contains(position)) return false;
            if (collectionOwner != null) return collectionOwner == visitId && (serviceSides==null || collectionPosition==position);
            var head = paidQueue[0];
            if (!head.Arrived || head.Yielding && !allowYielded || deliveryOwner!=null && (serviceSides==null?sharedServicePosition:deliveryPosition==position)) return false;
            collectionOwner = visitId;
            collectionPosition=position;
            // lease仅授予移动权限；旧wait到达回执不能冒充service到达。
            // The original wait slot stays reserved until actual departure/removal.
            head.Target = position;
            head.Generation = ++generation;
            head.Arrived = false;
            return true;
        }

        // 只能由实际离位事件调用；放杯、Remove和错误actor都不能提前放行身体。
        // Delivery commit does not release the physical service lease.
        public void ReleaseServicePosition(string actorId)
        {
            if (!ValidId(actorId)) return;
            if (deliveryOwner == actorId) deliveryOwner = null;
            if (collectionOwner == actorId) collectionOwner = null;
        }

        public bool Remove(string visitId)
        {
            if (!ValidId(visitId) || !reservations.TryGetValue(visitId, out var entry)) return false;
            reservations.Remove(visitId);
            paidQueue.Remove(entry);
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

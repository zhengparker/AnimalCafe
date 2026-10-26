using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Layout;

namespace AnimalCafe.CafeLoop
{
    public sealed class CafeDeliveryStore
    {
        private readonly string sessionId;
        private readonly long epoch;
        private readonly Dictionary<CafeOrderKey,CafeDeliverySnapshot> entries=new Dictionary<CafeOrderKey,CafeDeliverySnapshot>();
        public CafeDeliveryStore(string sessionId,long epoch)
        {
            CafeContractRules.Identity(sessionId,nameof(sessionId)); CafeContractRules.Positive(epoch,nameof(epoch));
            this.sessionId=sessionId; this.epoch=epoch;
        }
        public IReadOnlyList<CafeDeliverySnapshot> Snapshot => entries.Values.OrderBy(d=>d.DeliverySequence).ToList().AsReadOnly();
        public bool Contains(CafeOrderKey key) => entries.ContainsKey(key);
        // Order不可逆转换之前使用同一份纯校验 / Preflight before Ready/Complete.
        internal bool CanCommit(CafeDeliverySnapshot value)
        {
            if(value.Key.SessionId!=sessionId || value.Key.Epoch!=epoch || value.Key.OrderId<=0 ||
                string.IsNullOrWhiteSpace(value.VisitId) || value.VisitId!=value.VisitId.Trim() || value.DeliverySequence<=0 ||
                Contains(value.Key) || entries.Values.Any(d=>d.DeliverySequence==value.DeliverySequence)) return false;
            try { SurfaceSlotAddress.Validate(value.Address,nameof(value)); }
            catch(ArgumentException) { return false; }
            return true;
        }
        internal bool CommitDelivery(CafeDeliverySnapshot value)
        { if(!CanCommit(value)) return false; entries.Add(value.Key,value); return true; }
        internal bool CommitCollection(CafeOrderKey key) => entries.Remove(key);
        internal void Clear() { entries.Clear(); }
    }
}

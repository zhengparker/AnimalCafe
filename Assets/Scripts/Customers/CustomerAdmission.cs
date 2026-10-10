using System;
using AnimalCafe.Capacity;
using System.Linq;
namespace AnimalCafe.Customers
{
    public sealed class CustomerAdmission : IDisposable
    {
        private readonly CapacityService capacity;
        private readonly string owner;
        private readonly CapacityToken[] tokens;
        private bool disposed,inside,queued,queueLeft,pickupCancelled;
        private CustomerAdmission(CapacityService service,string id,CapacityToken[] reservations)
        { capacity=service; owner=id; tokens=reservations; }
        public static bool TryCreate(CapacityService capacity,string owner,out CustomerAdmission lease)
        {
            lease=null; if(capacity==null) return false;
            var result=capacity.TryReserveAdmission(owner); if(!result.Succeeded) return false;
            var tokens=new CapacityToken[3];
            foreach(var r in result.Reservations) tokens[(int)r.Kind]=r.Token;
            lease=new CustomerAdmission(capacity,owner,tokens); return true;
        }
        public bool MarkInside()
        { if(disposed) return false; if(inside) return true; return inside=capacity.Occupy(tokens[0],owner).Succeeded; }
        public bool MarkQueued()
        { if(disposed || queueLeft) return false; if(queued) return true; return queued=capacity.Occupy(tokens[1],owner).Succeeded; }
        public void LeaveQueue()
        { if(!disposed && !queueLeft) queueLeft=capacity.Release(tokens[1],owner).Succeeded; }
        public void CancelUnusedPickup()
        { if(!disposed && !pickupCancelled) pickupCancelled=capacity.Release(tokens[2],owner).Succeeded; }
        // Dispose仅用于创建rollback、真实Exit Arrived或session teardown。
        public void Dispose()
        {
            if(disposed) return;
            foreach(var token in tokens) capacity.Release(token,owner);
            disposed=true;
        }
    }
}

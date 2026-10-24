using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.CafeLoop;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase14
{
    public class CafeDeliveryStoreTests
    {
        internal static readonly SurfaceSlotAddress Slot = new SurfaceSlotAddress("11111111111111111111111111111111", "slot.0");
        private static object Call(CafeDeliveryStore store,string name,params object[] args)
            => typeof(CafeDeliveryStore).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(store,args);
        [Test] public void FiveDeliveriesRetainAllBusinessInventoryAndMiddleRemovalKeepsOwnership()
        {
            var store=new CafeDeliveryStore("s",1);
            for(var i=1;i<=5;i++) Assert.That(Call(store,"CommitDelivery",new CafeDeliverySnapshot(
                new CafeOrderKey("s",1,i),"v"+i,Slot,i)),Is.True);
            Assert.That(store.Snapshot.Count,Is.EqualTo(5),"The three-cup rendering cap is not business capacity");
            Assert.That(Call(store,"CommitCollection",new CafeOrderKey("s",1,2)),Is.True);
            CollectionAssert.AreEqual(new long[]{1,3,4,5},store.Snapshot.Select(d=>d.Key.OrderId));
            Assert.That(store.Contains(new CafeOrderKey("s",1,2)),Is.False);
            Assert.That(store.Contains(new CafeOrderKey("s",1,5)),Is.True);
        }
        [Test] public void DuplicateKeysAndForeignEpochCannotOverwriteExistingDelivery()
        {
            var store=new CafeDeliveryStore("s",2); var key=new CafeOrderKey("s",2,1);
            var original=new CafeDeliverySnapshot(key,"owner",Slot,1);
            Assert.That(Call(store,"CommitDelivery",original),Is.True);
            Assert.That(Call(store,"CommitDelivery",new CafeDeliverySnapshot(key,"wrong",Slot,2)),Is.False);
            Assert.That(Call(store,"CommitDelivery",new CafeDeliverySnapshot(new CafeOrderKey("s",1,2),"old",Slot,3)),Is.False);
            Assert.That(Call(store,"CommitDelivery",new CafeDeliverySnapshot(new CafeOrderKey("other",2,2),"other",Slot,4)),Is.False);
            Assert.That(store.Snapshot.Single(),Is.EqualTo(original));
        }
        [Test] public void SnapshotIsDetachedReadonlyAndClearingDoesNotRewriteIt()
        {
            var store=new CafeDeliveryStore("s",1); var key=new CafeOrderKey("s",1,1);
            Assert.That(Call(store,"CommitDelivery",new CafeDeliverySnapshot(key,"v",Slot,1)),Is.True);
            var snapshot=store.Snapshot;
            Assert.Throws<NotSupportedException>(()=>((IList<CafeDeliverySnapshot>)snapshot).Clear());
            Call(store,"Clear"); Assert.That(store.Snapshot,Is.Empty);
            Assert.That(snapshot.Count,Is.EqualTo(1));
            Assert.That(Call(store,"CommitCollection",key),Is.False);
        }
    }
}

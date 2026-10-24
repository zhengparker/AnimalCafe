using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.CafeLoop;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace AnimalCafe.Tests.EditMode.Phase14
{
    public sealed class CafeCupProjectionTests
    {
        private GameObject root, support;
        private PickupCupDisplay display;
        private SurfaceSlotAddress address=new SurfaceSlotAddress("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1","slot.0");
        private CafeDeliverySnapshot Delivery(int id,long epoch=1,long? sequence=null) => new CafeDeliverySnapshot(
            new CafeOrderKey("projection",epoch,id),"visit-"+id,address,sequence ?? id);
        [SetUp] public void Setup()
        {
            root=new GameObject("P14 projection"); support=new GameObject("P14 surface");
            display=root.AddComponent<PickupCupDisplay>();
            display.Configure(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Phase4/Prefabs/PF_Item_CeramicCup_01.prefab"),
                a=>a.Equals(address)?support.transform:null); display.SetIntegratedMode(true);
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(root); Object.DestroyImmediate(support); }
        [Test] public void ProjectionShowsThreeAndRefillsCorrectKey()
        {
            var five=Enumerable.Range(1,5).Select(id=>Delivery(id)).ToArray();
            Assert.That(display.TryProject(five,out var reason),Is.True,reason);
            CollectionAssert.AreEquivalent(new[]{Delivery(1).Key,Delivery(2).Key,Delivery(3).Key},display.VisibleOrders.Keys);
            var first=display.VisibleOrders[Delivery(1).Key]; var third=display.VisibleOrders[Delivery(3).Key];
            Assert.That(display.TryProject(five.Where(d=>d.Key.OrderId!=2).ToArray(),out reason),Is.True,reason);
            CollectionAssert.AreEquivalent(new[]{Delivery(1).Key,Delivery(3).Key,Delivery(4).Key},display.VisibleOrders.Keys);
            Assert.That(display.VisibleOrders[Delivery(1).Key],Is.SameAs(first));
            Assert.That(display.VisibleOrders[Delivery(3).Key],Is.SameAs(third));
            Assert.That(support.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("PickupCup_")),Is.EqualTo(3));
            Assert.That(root.transform.childCount,Is.Zero,"No inactive overflow or staging inventory");
            Assert.That(display.VisibleCount(address),Is.EqualTo(3));
        }
        [Test] public void InputOrderDoesNotOverrideDeliverySequence()
        {
            Assert.That(display.TryProject(new[]{Delivery(5),Delivery(3),Delivery(1),Delivery(4),Delivery(2)},out _),Is.True);
            CollectionAssert.AreEquivalent(new[]{Delivery(1).Key,Delivery(2).Key,Delivery(3).Key},display.VisibleOrders.Keys);
        }
        [Test] public void TenLogicalDeliveriesNeverCreateHiddenOverflowAndRefillOldestKey()
        {
            var ten=Enumerable.Range(1,10).Select(i=>Delivery(i)).ToArray();
            for(var count=1;count<=10;count++)
            {
                Assert.That(display.TryProject(ten.Take(count).ToArray(),out var reason),Is.True,reason);
                Assert.That(display.VisibleOrders.Count,Is.EqualTo(Math.Min(count,3)));
                Assert.That(support.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("PickupCup_")),Is.EqualTo(Math.Min(count,3)));
            }
            Assert.That(display.TryProject(ten.Skip(2).ToArray(),out _),Is.True);
            CollectionAssert.AreEquivalent(new[]{Delivery(3).Key,Delivery(4).Key,Delivery(5).Key},display.VisibleOrders.Keys);
            Assert.That(root.transform.childCount,Is.Zero);
        }
        [Test] public void SameNumericOrderInNewEpochReplacesOldCup()
        {
            Assert.That(display.TryProject(new[]{Delivery(1)},out _),Is.True); var old=display.VisibleOrders[Delivery(1).Key];
            Assert.That(display.TryProject(new[]{Delivery(1,2)},out _),Is.True);
            Assert.That(display.VisibleOrders.ContainsKey(Delivery(1).Key),Is.False);
            Assert.That(display.VisibleOrders[Delivery(1,2).Key],Is.Not.SameAs(old));
            Assert.That(old==null,Is.True); Assert.That(display.VisibleCount(address),Is.EqualTo(1));
        }
        [Test] public void RepeatedProjectionKeepsObjectsAndPassiveVisualContract()
        {
            Assert.That(display.TryProject(new[]{Delivery(1),Delivery(2)},out _),Is.True);
            var saved=display.VisibleOrders.ToArray();
            Assert.That(display.TryProject(new[]{Delivery(1),Delivery(2)},out _),Is.True);
            foreach(var pair in saved)
            {
                Assert.That(display.VisibleOrders[pair.Key],Is.SameAs(pair.Value));
                Assert.That(pair.Value.GetComponentsInChildren<Collider>(true),Is.Empty);
                Assert.That(pair.Value.GetComponentsInChildren<MonoBehaviour>(true),Is.Empty);
            }
            Assert.That(saved[0].Value.transform.localPosition,Is.EqualTo(new Vector3(-.12f,0,-.08f)));
        }
        [Test] public void DuplicateKeyIsRejectedWithoutChangingVisibleOwnership()
        {
            Assert.That(display.TryProject(new[]{Delivery(1)},out _),Is.True); var saved=display.VisibleOrders[Delivery(1).Key];
            Assert.That(display.TryProject(new[]{Delivery(1),Delivery(1)},out var reason),Is.False);
            Assert.That(reason,Is.Not.Empty); Assert.That(display.VisibleOrders[Delivery(1).Key],Is.SameAs(saved));
        }
        [Test] public void MissingSlotRemovesStaleVisualAndReportsFailure()
        {
            Assert.That(display.TryProject(new[]{Delivery(1)},out _),Is.True); Object.DestroyImmediate(support);
            Assert.That(display.TryProject(new[]{Delivery(1)},out var reason),Is.False);
            Assert.That(reason,Is.Not.Empty); Assert.That(display.VisibleOrders,Is.Empty);
        }
        [Test] public void P14ProjectionRejectsLegacyDeliveryAndClearsOnlyItsOwnCups()
        {
            var unrelated=new GameObject("Unrelated surface item"); unrelated.transform.SetParent(support.transform);
            Assert.That(display.TryProject(new[]{Delivery(1)},out _),Is.True);
            Assert.That(display.TryDeliver(address,out var reason),Is.False); Assert.That(reason,Is.Not.Empty);
            Assert.That(display.TryProject(Array.Empty<CafeDeliverySnapshot>(),out reason),Is.True,reason);
            Assert.That(display.VisibleOrders,Is.Empty); Assert.That(unrelated,Is.Not.Null);
            Assert.That(support.transform.childCount,Is.EqualTo(1));
        }
    }
}

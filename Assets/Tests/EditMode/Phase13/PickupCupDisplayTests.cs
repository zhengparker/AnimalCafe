using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.EditMode.Phase13
{
    public sealed class PickupCupDisplayTests
    {
        private const string CupPath = "Assets/Art/Phase4/Prefabs/PF_Item_CeramicCup_01.prefab";
        private readonly List<GameObject> owned = new List<GameObject>();
        private readonly Dictionary<SurfaceSlotAddress, Transform> confirmed =
            new Dictionary<SurfaceSlotAddress, Transform>();
        private readonly SurfaceSlotAddress address = new SurfaceSlotAddress(
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1", "slot.left");
        private PickupCupDisplay display;
        private GameObject prefab;
        private Transform slot;

        [SetUp]
        public void SetUp()
        {
            display = Own("Display").AddComponent<PickupCupDisplay>();
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CupPath);
            Assert.That(prefab, Is.Not.Null, "Use the existing real bottom-pivot Cup prefab.");
            slot = AddSlot(address, Own("Support").transform, new Vector3(.5f, .84f, 0));
            Configure(prefab);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in owned)
                if (item != null) Object.DestroyImmediate(item);
            owned.Clear();
            confirmed.Clear();
        }

        [Test]
        public void TenDeliveriesShowOnlyThreeCups()
        {
            for (var i = 1; i <= 10; i++)
            {
                Assert.That(Deliver(address), Is.True);
                Assert.That(Count(address), Is.EqualTo(Math.Min(i, 3)));
                Assert.That(Cups(slot).Length, Is.EqualTo(Math.Min(i, 3)), "No hidden overflow objects.");
            }
        }

        [Test]
        public void ThreeOffsetsAreTriangular()
        {
            for (var i = 0; i < 3; i++) Assert.That(Deliver(address), Is.True);
            var offsets = Cups(slot).Select(t => t.localPosition).ToArray();
            Assert.That(offsets.Length, Is.EqualTo(3), "Three real visible cups must exist.");
            Assert.That(Vector3.Cross(offsets[1] - offsets[0], offsets[2] - offsets[0]).sqrMagnitude,
                Is.GreaterThan(.0001f));
            Assert.That(offsets[0], Is.EqualTo(new Vector3(-.12f, 0, -.08f)));
            Assert.That(offsets[1], Is.EqualTo(new Vector3(.12f, 0, -.08f)));
            Assert.That(offsets[2], Is.EqualTo(new Vector3(0, 0, .12f)));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void FailedInstantiationDoesNotCommitCount(int previousCups)
        {
            var source = Object.Instantiate(prefab);
            owned.Add(source);
            Configure(source);
            for (var i = 0; i < previousCups; i++) Assert.That(Deliver(address), Is.True);
            // A genuinely destroyed Unity source is invalid; no synthetic production factory.
            Object.DestroyImmediate(source);
            Assert.That(Deliver(address), Is.False);
            Assert.That(Count(address), Is.EqualTo(previousCups));
            Assert.That(Cups(slot).Length, Is.EqualTo(previousCups));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void InvalidVisualRollsBackTemporaryClone(int previousCups)
        {
            var source = Object.Instantiate(prefab);
            owned.Add(source);
            Configure(source);
            for (var i = 0; i < previousCups; i++) Assert.That(Deliver(address), Is.True);
            // Break only the source; existing delivered instances remain valid.
            foreach (var renderer in source.GetComponentsInChildren<Renderer>(true))
                Object.DestroyImmediate(renderer);
            Assert.That(Deliver(address), Is.False);
            Assert.That(Count(address), Is.EqualTo(previousCups));
            Assert.That(slot.childCount, Is.EqualTo(previousCups));
            Assert.That(display.transform.childCount, Is.Zero, "No staging objects after failure.");
        }

        [Test]
        public void DifferentModulesDoNotShareCups()
        {
            var right = new SurfaceSlotAddress(address.SupportFurnitureInstanceId, "slot.right");
            var other = new SurfaceSlotAddress("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb1", "slot.left");
            var rightSlot = AddSlot(right, slot.parent, new Vector3(-.5f, .84f, 0));
            var otherSlot = AddSlot(other, Own("Second support").transform, new Vector3(0, 1.1f, 0));
            Assert.That(Deliver(address), Is.True);
            Assert.That(Deliver(right), Is.True);
            Assert.That(Deliver(right), Is.True);
            Assert.That(Deliver(other), Is.True);
            Assert.That(Count(address), Is.EqualTo(1));
            Assert.That(Cups(slot).Length, Is.EqualTo(1));
            Assert.That(Count(right), Is.EqualTo(2));
            Assert.That(Cups(rightSlot).Length, Is.EqualTo(2));
            Assert.That(Count(other), Is.EqualTo(1));
            Assert.That(Cups(otherSlot).Length, Is.EqualTo(1));
        }

        [Test]
        public void MovedSupportKeepsLocalPlacement()
        {
            var grid = Own("Rotated grid").transform;
            slot.parent.SetParent(grid, false);
            grid.SetPositionAndRotation(new Vector3(10, 2, 5), Quaternion.Euler(0, 37, 0));
            for (var i = 0; i < 3; i++) Assert.That(Deliver(address), Is.True);
            var cups = Cups(slot);
            var offsets = cups.Select(t => t.localPosition).ToArray();
            slot.parent.localPosition = new Vector3(3, 1, 4);
            slot.parent.localRotation = Quaternion.Euler(0, 90, 0);
            display.RefreshConfirmedSupports();
            for (var i = 0; i < cups.Length; i++)
            {
                Assert.That(cups[i].parent, Is.SameAs(slot));
                Assert.That(Vector3.Distance(cups[i].position, slot.TransformPoint(offsets[i])), Is.LessThan(.00001f));
                Assert.That(Quaternion.Angle(cups[i].rotation, slot.rotation), Is.LessThan(.001f));
            }
        }

        [Test]
        public void RealCupBottomMeetsSlotSurface()
        {
            slot.parent.position = new Vector3(3, 2, 4);
            Assert.That(Deliver(address), Is.True);
            var cup = Cups(slot).Single();
            var bottom = cup.GetComponentsInChildren<Renderer>().Min(r => r.bounds.min.y);
            Assert.That(Mathf.Abs(bottom - slot.position.y), Is.LessThan(.001f));
        }

        [Test]
        public void RuntimeVisualCannotRegisterBusinessOrBlockNavigation()
        {
            var source = Object.Instantiate(prefab);
            owned.Add(source);
            source.SetActive(false);
            Configure(source);
            Assert.That(Deliver(address), Is.True);
            var visual = Cups(slot).Single();
            Assert.That(visual.gameObject.activeSelf, Is.True);
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(visual.GetComponentsInChildren<NavMeshObstacle>(true), Is.Empty);
            Assert.That(visual.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            Assert.That(prefab.GetComponent<Collider>().enabled, Is.True, "Original Cup contract stays intact.");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void UnsupportedCupCannotActivateBusinessOrNavigationComponents(bool business)
        {
            var source = Object.Instantiate(prefab);
            owned.Add(source);
            source.SetActive(false);
            if (business) source.AddComponent<SurfaceSlotMarker>();
            else source.AddComponent<NavMeshObstacle>();
            Configure(source);
            Assert.That(Deliver(address), Is.False);
            Assert.That(Count(address), Is.Zero);
            Assert.That(slot.childCount, Is.Zero);
            Assert.That(display.transform.childCount, Is.Zero);
        }

        [Test]
        public void SaturatedDisplayStillValidatesConfirmedSlot()
        {
            for (var i = 0; i < 3; i++) Assert.That(Deliver(address), Is.True);
            confirmed.Remove(address); // Old Scene representation deliberately remains alive.
            Assert.That(slot, Is.Not.Null);
            Assert.That(Deliver(address), Is.False);
            Assert.That(Count(address), Is.Zero);
            Assert.That(slot.childCount, Is.Zero);
        }

        [Test]
        public void RefreshReparentsToReplacementAndCleansOnlyInvalidModule()
        {
            var other = new SurfaceSlotAddress("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb1", "slot.left");
            var otherSlot = AddSlot(other, Own("Other support").transform, Vector3.up);
            Assert.That(Deliver(address), Is.True);
            Assert.That(Deliver(other), Is.True);
            var cup = Cups(slot).Single();
            var oldSlot = slot;
            slot = AddSlot(address, Own("Replacement support").transform, new Vector3(0, 1.4f, 0));
            confirmed.Remove(other);
            display.RefreshConfirmedSupports();
            Assert.That(cup.parent, Is.SameAs(slot));
            Assert.That(Count(address), Is.EqualTo(1));
            Assert.That(oldSlot.childCount, Is.Zero);
            Assert.That(Count(other), Is.Zero);
            Assert.That(otherSlot.childCount, Is.Zero);
        }

        [Test]
        public void DestroyedSupportIsRemovedOnRefresh()
        {
            Assert.That(Deliver(address), Is.True);
            Object.DestroyImmediate(slot.parent.gameObject);
            display.RefreshConfirmedSupports();
            Assert.That(Count(address), Is.Zero);
            Assert.That(Deliver(address), Is.False);
        }

        [Test]
        public void MissingConfigurationAndDefaultAddressFailClearly()
        {
            Configure(null);
            Assert.That(Deliver(address), Is.False);
            Configure(prefab);
            Assert.That(Deliver(default), Is.False);
            Assert.That(slot.childCount, Is.Zero);
        }

        [Test]
        public void ClearRemovesOnlyOwnedCupsAndIsIdempotent()
        {
            var unrelated = new GameObject("Unrelated surface item");
            unrelated.transform.SetParent(slot, false);
            Assert.That(Deliver(address), Is.True);
            display.Clear();
            display.Clear();
            Assert.That(Count(address), Is.Zero);
            Assert.That(slot.childCount, Is.EqualTo(1));
            Assert.That(unrelated, Is.Not.Null);
            Assert.That(Deliver(address), Is.True);
            display.Clear();
            Assert.That(slot, Is.Not.Null);
            Assert.That(slot.childCount, Is.EqualTo(1));
        }

        private void Configure(GameObject source)
        {
            display.Configure(source, a =>
                confirmed.TryGetValue(a, out var value) ? value : null);
        }

        private bool Deliver(SurfaceSlotAddress target)
        {
            var result = display.TryDeliver(target, out var reason);
            Assert.That(reason, result ? Is.Null.Or.Empty : Is.Not.Null.And.Not.Empty);
            return result;
        }

        private int Count(SurfaceSlotAddress target) => display.VisibleCount(target);

        private Transform AddSlot(SurfaceSlotAddress target, Transform parent, Vector3 local)
        {
            var marker = new GameObject(target.SlotId).AddComponent<SurfaceSlotMarker>();
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = local;
            // Persist serialized changes on the real marker fixture.
            var serialized = new SerializedObject(marker);
            serialized.FindProperty("slotId").stringValue = target.SlotId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            confirmed[target] = marker.transform;
            return marker.transform;
        }

        private GameObject Own(string name)
        {
            var item = new GameObject(name);
            owned.Add(item);
            return item;
        }

        private static Transform[] Cups(Transform parent) => parent.Cast<Transform>().ToArray();
    }
}

using System.Linq;
using AnimalCafe.Content;
using AnimalCafe.EditorTools.Phase4;
using AnimalCafe.EditorTools.Phase6;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase13
{
    /// <summary>42cm counter contract / 已批准桌高与真实摆放挂点回归。</summary>
    public sealed class CounterHeightRegressionTests
    {
        [TestCase(Phase6DecorationAssetPaths.Counter1x1PrefabPath, 1, 1)]
        [TestCase(Phase6DecorationAssetPaths.Counter1x2PrefabPath, 1, 2)]
        [TestCase(Phase6DecorationAssetPaths.Counter1x3PrefabPath, 1, 3)]
        [TestCase(Phase6DecorationAssetPaths.Counter2x3PrefabPath, 2, 3)]
        [TestCase(Phase4ProductionAssetBuilder.LongCounterFixturePrefabPath, 1, 3)]
        public void CounterVisualColliderAndEverySlotShareApproved42cmPlane(
            string path, int width, int depth)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            var root = Object.Instantiate(prefab);
            try
            {
                Assert.That(root.transform.localScale, Is.EqualTo(Vector3.one));
                var renderers = root.GetComponentsInChildren<Renderer>();
                Assert.That(renderers, Is.Not.Empty);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                Assert.That(bounds.min.y, Is.EqualTo(0).Within(.001f), "Cabinet must stay on floor");
                Assert.That(bounds.max.y, Is.EqualTo(.42f).Within(.001f), "Owner chose a 30cm reduction");
                Assert.That(bounds.size.x, Is.EqualTo(width).Within(.001f));
                Assert.That(bounds.size.z, Is.EqualTo(depth).Within(.001f));
                var slots = root.GetComponentsInChildren<SurfaceSlotMarker>();
                Assert.That(slots.Length, Is.EqualTo(width * depth));
                Assert.That(slots.Select(s => s.SlotId).Distinct().Count(), Is.EqualTo(slots.Length));
                foreach (var slot in slots)
                {
                    Assert.That(slot.transform.position.y, Is.EqualTo(.42f).Within(.001f), slot.SlotId);
                    Assert.That(slot.transform.lossyScale, Is.EqualTo(Vector3.one), "Equipment cannot inherit cabinet compression");
                }
                var collider = root.GetComponent<BoxCollider>();
                Assert.That(collider, Is.Not.Null);
                Assert.That(collider.bounds.min.y, Is.EqualTo(0).Within(.001f));
                Assert.That(collider.bounds.max.y, Is.EqualTo(.42f).Within(.001f));
                Assert.That(collider.size.x, Is.EqualTo(width).Within(.001f));
                Assert.That(collider.size.z, Is.EqualTo(depth).Within(.001f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}

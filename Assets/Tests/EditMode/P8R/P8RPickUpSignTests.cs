using System;
using System.Linq;
using System.Reflection;
using AnimalCafe.Decoration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RPickUpSignTests
    {
        private const string Prefab = "Assets/UI/Phase8/Prefabs/PF_UI_PickUpPointIndicator.prefab";
        private const string Artwork = "Assets/UI/P8R/WorldMarkers/pickup_point.png";

        [Test]
        public void ProductionSign_UsesApprovedColorArtworkWithoutAnOpaqueRectangleOrShadow()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
            var visual = prefab.transform.Find("InvertedSquarePyramid"); // Stable legacy visual slot.
            var renderer = visual.GetComponent<MeshRenderer>();
            var material = renderer.sharedMaterial;
            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")), Is.EqualTo(Artwork));
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));
            Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.white));
            Assert.That(material.GetFloat("_Surface"), Is.EqualTo(1));
            Assert.That(material.GetFloat("_ZWrite"), Is.Zero);
            Assert.That(material.GetFloat("_AlphaClip"), Is.Zero, "Retain soft alpha edges.");
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(renderer.receiveShadows, Is.False);
            var mesh = visual.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(mesh.vertexCount, Is.EqualTo(4));
            Assert.That(mesh.triangles.Length, Is.EqualTo(6));
            Assert.That(mesh.bounds.size.z, Is.Zero);
            Assert.That(mesh.bounds.size.y, Is.EqualTo(.56f).Within(.001f));
            Assert.That(mesh.bounds.min.y, Is.EqualTo(.18f).Within(.001f));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(visual.GetComponents<MonoBehaviour>().Select(c => c.GetType().Name),
                Does.Contain("P8RPickUpSignBillboard"));
            var importer = (TextureImporter)AssetImporter.GetAtPath(Artwork);
            Assert.That(importer.alphaIsTransparency && importer.mipmapEnabled, Is.True);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Trilinear));
        }

        [Test]
        public void SignFacing_KeepsTheSlotAnchorAndFootprintFixedWhenSupportRotates()
        {
            var type = typeof(PickUpPointIndicatorView).Assembly.GetType("AnimalCafe.UI.P8R.P8RPickUpSignBillboard");
            Assert.That(type, Is.Not.Null, "The sign needs a camera-facing visual, not a camera-facing footprint.");
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));
            var cameraObject = new GameObject("SignFacingTestCamera");
            try
            {
                var camera = cameraObject.AddComponent<UnityEngine.Camera>();
                camera.transform.rotation = Quaternion.Euler(38, 45, 0);
                root.transform.SetPositionAndRotation(new Vector3(3, .72f, 4), Quaternion.Euler(0, 90, 0));
                var visual = root.transform.Find("InvertedSquarePyramid");
                var footprint = root.transform.Find("Footprint");
                var before = visual.position;
                var footprintRotation = footprint.rotation;
                var component = visual.GetComponent(type);
                Assert.That(component, Is.Not.Null);
                type.GetMethod("FaceCamera").Invoke(component, new object[] { camera });
                Assert.That(Quaternion.Angle(visual.rotation, camera.transform.rotation), Is.LessThan(.001f));
                Assert.That(visual.position, Is.EqualTo(before));
                Assert.That(footprint.rotation, Is.EqualTo(footprintRotation));
                Assert.That(Quaternion.Angle(root.transform.rotation, Quaternion.Euler(0, 90, 0)), Is.LessThan(.001f));
                type.GetMethod("FaceCamera").Invoke(component, new object[] { null });
                Assert.That(visual.position, Is.EqualTo(before), "Missing camera must be harmless.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(cameraObject); }
        }
    }
}

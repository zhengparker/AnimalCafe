#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace AnimalCafe.Tests.PlayMode
{
    public sealed class Phase8FootprintLightPlayModeTests
    {
        private readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            foreach (var item in owned.Where(item => item != null).Reverse()) UnityEngine.Object.Destroy(item);
            owned.Clear();
            yield return null;
        }

        [Test]
        public void GridLight_ChangesOnlyFill_PreservesColorBoundariesAndReusesItsPool()
        {
            var root = Own(new GameObject("FootprintLightGrid"));
            var view = root.AddComponent<GridHighlightView>();
            var theme = Own(ScriptableObject.CreateInstance<AnimalCafeUiTheme>());
            var legacy = Own(new Material(Shader.Find("Universal Render Pipeline/Unlit")));
            var space = new DecorationGridSpace(new GridSettings(1f),
                new LayoutBounds(new GridPosition(0, 0), new GridSize(4, 4)));
            view.Configure(root.transform, space, legacy, theme);
            view.ShowGrid(space.Settings);
            view.ShowFootprint(new[] { new GridPosition(0, 0), new GridPosition(1, 0) }, true);
            var first = root.transform.Find("FootprintCell_00");
            var fill = first.Find("Fill").GetComponent<Renderer>();
            var boundsBefore = fill.bounds;
            ConfigureLight(view);
            var light = LightMaterial();
            Assert.That(fill.sharedMaterial, Is.SameAs(light), "Already pooled fills must adopt the dedicated light material.");
            Assert.That(fill.bounds, Is.EqualTo(boundsBefore), "Switching feedback style must not expand the grid footprint.");
            Assert.That(fill.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(fill.receiveShadows, Is.False);
            AssertColor(fill, theme.Colors.Accent);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true).Where(r => r.name != "Fill"))
                Assert.That(renderer.sharedMaterial, Is.SameAs(legacy), "BaseGrid and white state marks must stay isolated.");
            view.ShowFootprint(new[] { new GridPosition(0, 1), new GridPosition(1, 1), new GridPosition(2, 1) }, false);
            Assert.That(root.transform.Find("FootprintCell_00"), Is.SameAs(first));
            AssertColor(fill, theme.Colors.Destructive);
            Assert.That(root.transform.Find("FootprintCell_02/Fill").GetComponent<Renderer>().sharedMaterial, Is.SameAs(light));
            Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
            view.ClearFootprint();
            Assert.That(first.gameObject.activeSelf, Is.False);
            view.ShowFootprint(new[] { new GridPosition(3, 3) }, true);
            Assert.That(root.transform.Cast<Transform>().Count(t => t.name.StartsWith("FootprintCell_")), Is.EqualTo(3));
            view.HideGrid();
            Assert.That(root.GetComponentsInChildren<Renderer>(false), Is.Empty);
        }

        [Test]
        public void WallLight_PreservesExactFootprintAndWhiteFeedbackIconAcrossValidityChanges()
        {
            var root = Own(new GameObject("FootprintLightWall"));
            var view = root.AddComponent<WallMountedPreviewView>();
            var wall = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
            var authoring = wall.AddComponent<WallSurfaceAuthoring>();
            Set(authoring, "surfaceId", "wall.light-test");
            Set(authoring, "columns", 4);
            Set(authoring, "rows", 3);
            var valid = Own(new Material(Shader.Find("Universal Render Pipeline/Unlit")));
            var invalid = Own(new Material(valid));
            valid.SetColor("_BaseColor", new Color(.18f, .72f, .32f, .65f));
            invalid.SetColor("_BaseColor", new Color(.84f, .20f, .18f, .65f));
            view.Configure(root.transform, valid, invalid);
            ConfigureLight(view);
            view.ShowWallPreview(Preview(true), authoring, true, PlacementFeedbackKey.None);
            var projection = view.CurrentProjection;
            var fill = projection.GetComponent<Renderer>();
            var mesh = projection.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(fill.sharedMaterial, Is.SameAs(LightMaterial()));
            Assert.That(mesh.bounds.size, Is.EqualTo(new Vector3(1, 2, 0)));
            Assert.That(fill.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(fill.receiveShadows, Is.False);
            AssertColor(fill, valid.GetColor("_BaseColor"));
            var icon = projection.transform.Find("ProjectionFeedbackIcon").GetComponent<Renderer>();
            Assert.That(icon.sharedMaterial, Is.SameAs(valid));
            AssertColor(icon, Color.white);
            view.ShowWallPreview(Preview(false), authoring, false, PlacementFeedbackKey.WallOverlap);
            Assert.That(view.CurrentProjection, Is.SameAs(projection));
            Assert.That(projection.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
            Assert.That(fill.sharedMaterial, Is.SameAs(LightMaterial()));
            AssertColor(fill, invalid.GetColor("_BaseColor"));
            Assert.That(icon.sharedMaterial, Is.SameAs(invalid));
            AssertColor(icon, Color.white);
            view.ClearPreview();
            Assert.That(view.CurrentProjection, Is.Null);
        }

        private static WallMountedPlacementPreview Preview(bool valid)
        {
            var constructor = typeof(WallMountedPlacementPreview).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();
            return (WallMountedPlacementPreview)constructor.Invoke(new object[]
            {
                "wall-decor.light-test", null, "wall.light-test", new WallSlotPosition(1, 0), new WallFootprint(1, 2),
                valid ? WallPlacementResult.Success() : WallPlacementResult.Failure(WallPlacementFailureReason.Overlap), false
            });
        }

        private static void ConfigureLight(object view)
        {
            // Reflection lets the baseline compile so RED proves the missing rendering behavior.
            // 基线先编译，通过缺失能力的断言呈现有效 RED。
            var method = view.GetType().GetMethod("ConfigureFootprintLight", new[] { typeof(Material) });
            Assert.That(method, Is.Not.Null, "Only footprint fills need an independent light material configuration.");
            method.Invoke(view, new object[] { LightMaterial() });
        }

        private static Material LightMaterial()
        {
            // Load the real asset without adding a UnityEditor assembly reference.
            // 通过反射读取真实材质，保持 PlayMode assembly 的 Player-compatible 边界。
            var assetDatabase = Type.GetType("UnityEditor.AssetDatabase, UnityEditor.CoreModule");
            Assert.That(assetDatabase, Is.Not.Null,
                "Production material tests require the Unity Editor PlayMode runner.");
            var load = assetDatabase.GetMethod("LoadAssetAtPath", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(string), typeof(Type) }, null);
            Assert.That(load, Is.Not.Null, "UnityEditor.AssetDatabase.LoadAssetAtPath(string, Type) was not found.");
            var material = load.Invoke(null, new object[]
            {
                "Assets/UI/Phase8/Materials/M_FootprintLight.mat", typeof(Material)
            });
            Assert.That(material, Is.InstanceOf<Material>());
            return (Material)material;
        }

        private static void AssertColor(Renderer renderer, Color expected)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            var actual = block.GetColor("_BaseColor");
            // Material / MPB 颜色往返允许浮点误差，与现有 Phase7 测试一致。
            // Keep round-trip deltas visible; do not hide a semantic color change.
            TestContext.WriteLine($"{renderer.name} color delta RGBA: " +
                $"{actual.r - expected.r:R}, {actual.g - expected.g:R}, " +
                $"{actual.b - expected.b:R}, {actual.a - expected.a:R}");
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f), "Red channel");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f), "Green channel");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f), "Blue channel");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f), "Alpha channel");
        }

        private T Own<T>(T value) where T : UnityEngine.Object { owned.Add(value); return value; }
        private static void Set(object target, string name, object value) => target.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
#endif

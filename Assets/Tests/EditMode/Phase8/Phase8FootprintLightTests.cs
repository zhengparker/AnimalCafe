using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class Phase8FootprintLightTests
    {
        private const string MaterialPath = "Assets/UI/Phase8/Materials/M_FootprintLight.mat";
        private const string MountedPath = "Assets/UI/Phase8/Prefabs/PF_UI_FunctionalSurfacePreview.prefab";
        private const string PickUpPath = "Assets/UI/Phase8/Prefabs/PF_UI_PickUpPointIndicator.prefab";
        private static readonly string[] AuthoringPaths = { MaterialPath, MountedPath, PickUpPath };

        [Test]
        public void RefreshFootprintLightAssets_RejectsDirtyMaterialBeforeChangingEitherPrefab()
        {
            AssertAuthoringTargetsClean();
            var material = RequireMaterial();
            var originalOpacity = material.GetFloat("_FootprintOpacity");
            var originalMaterial = EditorJsonUtility.ToJson(material);
            var originalFiles = CaptureAuthoringFiles();
            var originalFootprints = CaptureFootprintSemantics();
            var fixtureOpacity = originalOpacity + .123f;
            try
            {
                material.SetFloat("_FootprintOpacity", fixtureOpacity);
                EditorUtility.SetDirty(material);
                Assert.That(EditorUtility.IsDirty(material), Is.True, "The fixture must reach the dirty-target guard.");

                var failure = Assert.Throws<InvalidOperationException>(RefreshFootprintLightAssets);

                Assert.That(failure.Message, Does.Contain(MaterialPath));
                Assert.That(EditorUtility.IsDirty(material), Is.True, "Rejection must preserve the unsaved edit.");
                Assert.That(material.GetFloat("_FootprintOpacity"), Is.EqualTo(fixtureOpacity));
                AssertAuthoringFilesUnchanged(originalFiles);
                Assert.That(CaptureFootprintSemantics(), Is.EqualTo(originalFootprints),
                    "Preflight must reject the dirty material before touching either prefab.");
            }
            finally
            {
                // Restore only this fixture's one parameter and dirty flag; never SaveAssets.
                // 目标起初必须 clean，只还原本测试改动，不保存或撤销其他资产。
                material.SetFloat("_FootprintOpacity", originalOpacity);
                EditorUtility.ClearDirty(material);
            }
            Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(originalMaterial));
            AssertAuthoringTargetsClean();
            AssertAuthoringFilesUnchanged(originalFiles);
        }

        [Test]
        public void RefreshFootprintLightAssets_TwicePreservesTargetBytesGuidsAndLocalTransforms()
        {
            AssertAuthoringTargetsClean();
            var originalGuids = AuthoringPaths.Select(path => AssetDatabase.AssetPathToGUID(path)).ToArray();
            RefreshFootprintLightAssets();
            Assert.That(AuthoringPaths.Select(path => AssetDatabase.AssetPathToGUID(path)).ToArray(), Is.EqualTo(originalGuids));
            AssertAuthoringTargetsClean();
            var firstFiles = CaptureAuthoringFiles();
            var firstFootprints = CaptureFootprintSemantics();

            RefreshFootprintLightAssets();

            AssertAuthoringFilesUnchanged(firstFiles);
            Assert.That(AuthoringPaths.Select(path => AssetDatabase.AssetPathToGUID(path)).ToArray(), Is.EqualTo(originalGuids));
            Assert.That(CaptureFootprintSemantics(), Is.EqualTo(firstFootprints),
                "The second authoring pass must not drift footprint position, rotation, scale, mesh, or material.");
            AssertAuthoringTargetsClean();
            var material = RequireMaterial();
            var shaderPath = "Assets/UI/Phase8/Shaders/SH_FootprintLight.shader";
            Assert.That(AssetDatabase.GetAssetPath(material.shader), Is.EqualTo(shaderPath));
            foreach (var path in new[] { MountedPath, PickUpPath })
            {
                var footprint = AssetDatabase.LoadAssetAtPath<GameObject>(path).transform.Find("Footprint");
                Assert.That(footprint.GetComponent<Renderer>().sharedMaterial, Is.SameAs(material));
                Assert.That(AssetDatabase.GetDependencies(path), Does.Contain(shaderPath),
                    "Repeated authoring must retain the serialized Player shader dependency.");
            }
        }

        [Test]
        public void ProductionFootprints_ReferenceTransparentDepthTestedMaterial_WithoutChangingSharedSelectionMaterial()
        {
            var material = RequireMaterial();
            Assert.That(material.shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            Assert.That(material.renderQueue, Is.GreaterThanOrEqualTo((int)RenderQueue.Transparent));
            Assert.That(material.GetFloat("_ZWrite"), Is.Zero, "A light overlay must not occlude later transparent surfaces.");
            Assert.That(material.GetFloat("_ZTest"), Is.EqualTo((float)CompareFunction.LessEqual));
            Assert.That(material.GetFloat("_FootprintOpacity"), Is.InRange(.1f, .8f));
            Assert.That(material.GetFloat("_EdgeSoftness"), Is.GreaterThan(0f));
            foreach (var path in new[] { MountedPath, PickUpPath })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var footprint = prefab.transform.Find("Footprint").GetComponent<Renderer>();
                Assert.That(footprint.sharedMaterial, Is.SameAs(material));
                Assert.That(AssetDatabase.GetDependencies(path), Does.Contain(AssetDatabase.GetAssetPath(material.shader)),
                    "The production prefab must retain its shader in Player builds through serialized assets.");
                Assert.That(footprint.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(footprint.receiveShadows, Is.False);
            }
            var legacy = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Phase7/Materials/M_WallProjection_Valid.mat");
            Assert.That(legacy, Is.Not.SameAs(material));
            Assert.That(ReadAuthoredFloat(legacy, "_Surface"), Is.Zero, "Floor selection and legacy marks must retain their authored material.");
            Assert.That(ReadAuthoredFloat(legacy, "_ZWrite"), Is.EqualTo(1f));
            var pickup = AssetDatabase.LoadAssetAtPath<GameObject>(PickUpPath);
            var modelMaterial = pickup.transform.Find("InvertedSquarePyramid").GetComponent<Renderer>().sharedMaterial;
            Assert.That(modelMaterial, Is.Not.SameAs(material));
            Assert.That(modelMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"), "The colored sign is not the footprint light shader.");
            Assert.That(modelMaterial.GetTexture("_BaseMap"), Is.Not.Null, "Keep approved sign artwork separate from validity colors.");
        }

        [TestCase(MountedPath)]
        [TestCase(PickUpPath)]
        public void FootprintMesh_IsAFlatOneByOneSurfaceWithoutSolidSideFaces(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var footprint = prefab.transform.Find("Footprint");
            var mesh = footprint.GetComponent<MeshFilter>().sharedMesh;
            var points = mesh.vertices.Select(point => prefab.transform.InverseTransformPoint(footprint.TransformPoint(point))).ToArray();
            Assert.That(points.Max(p => p.x) - points.Min(p => p.x), Is.EqualTo(1f).Within(.0001f));
            Assert.That(points.Max(p => p.z) - points.Min(p => p.z), Is.EqualTo(1f).Within(.0001f));
            Assert.That(points.Max(p => p.y) - points.Min(p => p.y), Is.LessThan(.001f),
                "Footprint must be a projection plane, not the old 4 cm thick cube.");
            Assert.That(mesh.triangles, Has.Length.EqualTo(6));
            Assert.That(footprint.GetComponents<Collider>(), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LightShader_PreservesTheSurfaceAndFadesInward_WithOpaqueThemeTint(bool invalid)
        {
            var material = new Material(RequireMaterial());
            var target = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            try
            {
                target.Create();
                RenderTexture.active = target;
                var background = new Color(.2f, .2f, .2f, 1f);
                GL.Clear(false, true, background);
                material.SetColor("_BaseColor", invalid ? new Color(1, 0, 0, 1) : new Color(0, 1, 0, 1));
                Graphics.Blit(Texture2D.whiteTexture, target, material);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                readback.Apply();
                var center = readback.GetPixel(32, 32);
                var edge = readback.GetPixel(0, 32);
                var strength = invalid ? center.r : center.g;
                var edgeStrength = invalid ? edge.r : edge.g;
                Assert.That(strength, Is.GreaterThan(.25f), "A valid or invalid footprint must remain visibly tinted.");
                Assert.That(strength, Is.LessThan(.95f), "Theme alpha=1 must not turn the light into an opaque board.");
                Assert.That(edgeStrength, Is.LessThan(strength - .04f), "The edge must fade inside the existing quad boundary.");
                Assert.That(edgeStrength, Is.LessThan(.3f), "The footprint boundary must approach the underlying surface.");
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(readback);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [TestCase("ThemeValid")]
        [TestCase("ThemeInvalid")]
        [TestCase("WallValid")]
        [TestCase("WallInvalid")]
        public void LightShader_BrightensAuthoredStateColors_WithoutIncreasingOpacityOrLosingSoftEdges(string source)
        {
            var invalid = source.EndsWith("Invalid", StringComparison.Ordinal);
            var tint = ReadAuthoredTint(source);
            var production = new Material(RequireMaterial());
            var legacy = new Material(production);
            try
            {
                production.SetColor("_BaseColor", tint);
                legacy.SetColor("_BaseColor", tint);
                // Missing controls on the old shader must fail via identical pixels, not an API error.
                // 旧 shader 没有新参数时不设置，让真实画面相同产生有效 RED。
                if (legacy.HasProperty("_TintBrightness")) legacy.SetFloat("_TintBrightness", 1f);
                if (legacy.HasProperty("_TintSaturation")) legacy.SetFloat("_TintSaturation", 1f);
                if (legacy.HasProperty("_LightIntensity")) legacy.SetFloat("_LightIntensity", 1f);
                var darkBackground = new Color(.1f, .1f, .1f, 1f);
                var lightBackground = new Color(.7f, .7f, .7f, 1f);
                var dark = ReadLightPixels(production, darkBackground);
                var light = ReadLightPixels(production, lightBackground);
                var control = ReadLightPixels(legacy, darkBackground);
                var separation = invalid
                    ? dark.center.r - Mathf.Max(dark.center.g, dark.center.b)
                    : dark.center.g - Mathf.Max(dark.center.r, dark.center.b);
                var controlSeparation = invalid
                    ? control.center.r - Mathf.Max(control.center.g, control.center.b)
                    : control.center.g - Mathf.Max(control.center.r, control.center.b);
                TestContext.WriteLine($"{source}: center brightness {control.center.grayscale:R} -> " +
                    $"{dark.center.grayscale:R}; state-color separation {controlSeparation:R} -> {separation:R}");
                Assert.That(dark.center.grayscale, Is.GreaterThan(control.center.grayscale + .03f),
                    "The authored state tint must become visibly brighter than the legacy control.");
                Assert.That(separation, Is.GreaterThan(controlSeparation + .025f),
                    "Brightening must preserve and strengthen the existing green/red direction.");
                Assert.That(separation, Is.GreaterThan(.05f));
                Assert.That(production.GetFloat("_FootprintOpacity"), Is.EqualTo(.45f).Within(.0001f));
                Assert.That(production.GetFloat("_EdgeSoftness"), Is.EqualTo(.12f).Within(.0001f));
                for (var channel = 0; channel < 3; channel++)
                {
                    // Background differences cancel the tint and expose the actual blend opacity.
                    // 深浅底色差分抵消 tint，抓出通过增加不透明度冒充提亮的修改。
                    var centerTransmission = (light.center[channel] - dark.center[channel]) / .6f;
                    var edgeTransmission = (light.edge[channel] - dark.edge[channel]) / .6f;
                    Assert.That(centerTransmission, Is.EqualTo(.55f).Within(.02f),
                        $"RGB channel {channel} must still show 55% of the underlying surface.");
                    Assert.That(edgeTransmission, Is.GreaterThan(.95f),
                        $"RGB channel {channel} must fade back to the surface at the inside edge.");
                    Assert.That(Mathf.Abs(dark.edge[channel] - darkBackground[channel]), Is.LessThan(.025f));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(legacy);
                UnityEngine.Object.DestroyImmediate(production);
            }
        }

        [TestCase("ThemeValid")]
        [TestCase("ThemeInvalid")]
        [TestCase("WallValid")]
        [TestCase("WallInvalid")]
        public void SignalLight_EmitsBrighterStateColor_WithoutChangingBlendOrWhiteningBrightSurfaces(string source)
        {
            var production = new Material(RequireMaterial());
            var previousVersion = new Material(production);
            var dominant = source.EndsWith("Invalid", StringComparison.Ordinal) ? 0 : 1;
            try
            {
                var tint = ReadAuthoredTint(source);
                production.SetColor("_BaseColor", tint);
                previousVersion.SetColor("_BaseColor", tint);
                previousVersion.SetFloat("_TintBrightness", 4f);
                previousVersion.SetFloat("_TintSaturation", 1.5f);
                if (previousVersion.HasProperty("_LightIntensity")) previousVersion.SetFloat("_LightIntensity", 1f);
                var dark = ReadLightPixels(production, new Color(.1f, .1f, .1f, 1f));
                var light = ReadLightPixels(production, new Color(.7f, .7f, .7f, 1f));
                var control = ReadLightPixels(previousVersion, new Color(.1f, .1f, .1f, 1f));
                TestContext.WriteLine($"{source}: signal channel {control.center[dominant]:R} -> " +
                    $"{dark.center[dominant]:R}; bright-background HDR channel {light.center[dominant]:R}");
                Assert.That(dark.center[dominant], Is.GreaterThan(control.center[dominant] + .08f),
                    "Signal-light feedback must brighten beyond the previous capped version, not only the original dark tint.");
                Assert.That(light.center[dominant], Is.GreaterThan(1f),
                    "Float readback must retain the emitted highlight before display clipping.");
                Assert.That(light.center[dominant], Is.LessThan(1.25f), "Keep this a modest light, not an extreme HDR source.");
                for (var channel = 0; channel < 3; channel++)
                {
                    Assert.That((light.center[channel] - dark.center[channel]) / .6f,
                        Is.EqualTo(.55f).Within(.02f), "Preserve 55% substrate contribution BEFORE display clipping.");
                    Assert.That((light.edge[channel] - dark.edge[channel]) / .6f, Is.GreaterThan(.95f));
                    Assert.That(Mathf.Abs(dark.edge[channel] - .1f), Is.LessThan(.025f));
                }
                // Keep a real LDR check too: float blending alone cannot prove the final image stays colorful.
                // 保留亮灰/暖底的低动态范围测试，不将 HDR 混色通过误当成所有亮底都保留完整底纹。
                foreach (var background in new[] { new Color(.7f, .7f, .7f, 1f), new Color(.7f, .65f, .6f, 1f) })
                {
                    var ldr = ReadLightPixels(production, background, hdr: false);
                    var oldLdr = ReadLightPixels(previousVersion, background, hdr: false);
                    var otherChannels = Enumerable.Range(0, 3).Where(channel => channel != dominant).ToArray();
                    Assert.That(ldr.center[dominant], Is.GreaterThan(oldLdr.center[dominant] + .03f));
                    Assert.That(ldr.center[dominant] - otherChannels.Max(channel => ldr.center[channel]), Is.GreaterThan(.5f),
                        "On bright surfaces, red/green must stay distinct instead of bleaching to white.");
                    foreach (var channel in otherChannels)
                    {
                        Assert.That(ldr.center[channel], Is.LessThan(.45f));
                        Assert.That(ldr.center[channel], Is.LessThanOrEqualTo(oldLdr.center[channel] + .015f));
                    }
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(previousVersion);
                UnityEngine.Object.DestroyImmediate(production);
            }
        }

        private static Color ReadAuthoredTint(string source)
        {
            if (source.StartsWith("Wall", StringComparison.Ordinal))
            {
                var path = source == "WallInvalid" ? "M_WallProjection_Invalid.mat" : "M_WallProjection_Valid.mat";
                path = "Assets/Art/Phase7/Materials/" + path;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material, Is.Not.Null);
                var dirty = EditorUtility.IsDirty(material);
                var json = EditorJsonUtility.ToJson(material);
                var bytes = File.ReadAllBytes(path);
                // GetColor lazily synchronizes URP's legacy _Color and dirties newly authored assets.
                // 直接读真实author颜色的serialized值，不让测试读取改变共享素材。
                using var serialized = new SerializedObject(material);
                var colors = serialized.FindProperty("m_SavedProperties.m_Colors");
                var found = false;
                var color = default(Color);
                for (var i = 0; i < colors.arraySize; i++)
                {
                    var pair = colors.GetArrayElementAtIndex(i);
                    if (pair.FindPropertyRelative("first").stringValue != "_BaseColor") continue;
                    color = pair.FindPropertyRelative("second").colorValue;
                    found = true;
                    break;
                }
                Assert.That(found, Is.True, "The real authored wall state must contain _BaseColor.");
                Assert.That(EditorUtility.IsDirty(material), Is.EqualTo(dirty));
                Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(json));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
                return color;
            }
            var theme = AssetDatabase.LoadAssetAtPath<AnimalCafeUiTheme>("Assets/UI/Phase5/Theme/AnimalCafeUiTheme.asset");
            Assert.That(theme, Is.Not.Null);
            return source == "ThemeInvalid" ? theme.Colors.Destructive : theme.Colors.Accent;
        }

        private static float ReadAuthoredFloat(Material material, string key)
        {
            var dirty = EditorUtility.IsDirty(material);
            var json = EditorJsonUtility.ToJson(material);
            var path = AssetDatabase.GetAssetPath(material);
            var bytes = File.ReadAllBytes(path);
            using var serialized = new SerializedObject(material);
            var values = serialized.FindProperty("m_SavedProperties.m_Floats");
            var found = false;
            var value = 0f;
            for (var i = 0; i < values.arraySize; i++)
            {
                var pair = values.GetArrayElementAtIndex(i);
                if (pair.FindPropertyRelative("first").stringValue != key) continue;
                value = pair.FindPropertyRelative("second").floatValue;
                found = true;
                break;
            }
            Assert.That(found, Is.True, "The authored material must contain " + key);
            Assert.That(EditorUtility.IsDirty(material), Is.EqualTo(dirty));
            Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(json));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            return value;
        }

        private static (Color center, Color edge) ReadLightPixels(Material material, Color background, bool hdr = true)
        {
            var target = new RenderTexture(64, 64, 0, hdr ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            var readback = new Texture2D(64, 64, hdr ? TextureFormat.RGBAFloat : TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            try
            {
                target.Create();
                RenderTexture.active = target;
                GL.Clear(false, true, background);
                Graphics.Blit(Texture2D.whiteTexture, target, material);
                RenderTexture.active = target;
                readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                readback.Apply();
                return (readback.GetPixel(32, 32), readback.GetPixel(0, 32));
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(readback);
            }
        }

        private static void RefreshFootprintLightAssets()
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("AnimalCafe.EditorTools.Phase8.Phase8FootprintLightAssets"))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null);
            var method = type.GetMethod("RefreshFootprintLightAssets", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            try { method.Invoke(null, null); }
            catch (TargetInvocationException exception) { throw exception.InnerException; }
        }

        private static void AssertAuthoringTargetsClean()
        {
            foreach (var path in AuthoringPaths)
            {
                var assets = AssetDatabase.LoadAllAssetsAtPath(path);
                Assert.That(assets, Is.Not.Empty, path);
                foreach (var asset in assets)
                {
                    Assert.That(EditorUtility.IsDirty(asset), Is.False,
                        "Do not consume pre-existing Owner edits: " + path);
                    if (asset is GameObject root)
                        foreach (var component in root.GetComponentsInChildren<Component>(true))
                            Assert.That(EditorUtility.IsDirty(component), Is.False, path + " / " + component.name);
                }
            }
        }

        private static Dictionary<string, byte[]> CaptureAuthoringFiles() => AuthoringPaths
            .SelectMany(path => new[] { path, path + ".meta" })
            .ToDictionary(path => path, File.ReadAllBytes);

        private static void AssertAuthoringFilesUnchanged(Dictionary<string, byte[]> snapshot)
        {
            foreach (var entry in snapshot)
                Assert.That(File.ReadAllBytes(entry.Key), Is.EqualTo(entry.Value), entry.Key);
        }

        private static string[] CaptureFootprintSemantics() => new[] { MountedPath, PickUpPath }
            .Select(path => AssetDatabase.LoadAssetAtPath<GameObject>(path).transform.Find("Footprint"))
            .Select(footprint => string.Join("\n", footprint.GetComponents<Component>()
                .Select(component => EditorJsonUtility.ToJson(component))))
            .ToArray();

        private static Material RequireMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Assert.That(material, Is.Not.Null, "The approved footprint light requires its own production Material.");
            return material;
        }
    }
}

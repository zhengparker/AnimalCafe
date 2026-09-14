using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalCafe.EditorTools.Phase8
{
    /// <summary>Bounded P8 footprint authoring; never rebuilds or saves a Scene.</summary>
    public static class Phase8FootprintLightAssets
    {
        public const string ShaderPath = "Assets/UI/Phase8/Shaders/SH_FootprintLight.shader";
        public const string MaterialPath = "Assets/UI/Phase8/Materials/M_FootprintLight.mat";
        private static readonly string[] PrefabPaths =
        {
            Phase8AssetPaths.FunctionalSurfacePreviewPrefabPath,
            Phase8AssetPaths.PickUpPointIndicatorPrefabPath
        };

        public static void RefreshFootprintLightAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before updating footprint assets.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath)
                ?? throw new InvalidOperationException("Missing footprint light Shader: " + ShaderPath);
            if (ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("Resolve footprint light Shader errors before updating assets.");
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx")
                ?? throw new InvalidOperationException("Unity's built-in Quad mesh is unavailable.");

            // Preflight every target before the first write; do not consume unsaved Owner edits.
            // 写入前检查所有指定目标，只更新这一材质和两个 prefab。
            foreach (var path in PrefabPaths.Append(MaterialPath)) EnsureCleanTarget(path);
            foreach (var path in PrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path)
                    ?? throw new InvalidOperationException("Missing footprint Prefab: " + path);
                RequireFootprint(prefab);
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "M_FootprintLight" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            var beforeMaterial = EditorJsonUtility.ToJson(material);
            material.shader = shader;
            material.SetColor("_BaseColor", new Color(.18f, .72f, .32f, 1f));
            material.SetFloat("_TintBrightness", 6f);
            material.SetFloat("_TintSaturation", 3f);
            material.SetFloat("_LightIntensity", 1.5f);
            material.SetFloat("_FootprintOpacity", .45f);
            material.SetFloat("_EdgeSoftness", .12f);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            material.renderQueue = (int)RenderQueue.Transparent;
            if (beforeMaterial != EditorJsonUtility.ToJson(material)) EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);

            foreach (var path in PrefabPaths)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var footprint = RequireFootprint(root);
                    var filter = footprint.GetComponent<MeshFilter>();
                    var renderer = footprint.GetComponent<MeshRenderer>();
                    var before = Capture(footprint);
                    if (filter.sharedMesh != quad)
                    {
                        var vertices = filter.sharedMesh.vertices.Select(point =>
                            root.transform.InverseTransformPoint(footprint.TransformPoint(point))).ToArray();
                        var min = vertices.Aggregate(Vector3.Min);
                        var max = vertices.Aggregate(Vector3.Max);
                        // Retain the old top surface; repeated authoring does not accumulate rotation rounding.
                        // 保留原厚板顶面与模型尖端间距；重复执行不累加旋转浮点误差。
                        footprint.localPosition = new Vector3((min.x + max.x) * .5f, max.y, (min.z + max.z) * .5f);
                        footprint.localRotation = Quaternion.Euler(90f, 0f, 0f);
                        footprint.localScale = new Vector3(max.x - min.x, max.z - min.z, 1f);
                        filter.sharedMesh = quad;
                    }
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    if (before != Capture(footprint)) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            Debug.Log("P8 footprint light updated: one Material and two footprint Prefabs; no Scene saved.");
        }

        private static void EnsureCleanTarget(string path)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path && stage.scene.isDirty)
                throw new InvalidOperationException("Save or discard Prefab Stage edits before refreshing: " + path);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (EditorUtility.IsDirty(asset)
                    || asset is GameObject root && root.GetComponentsInChildren<Component>(true).Any(EditorUtility.IsDirty))
                    throw new InvalidOperationException("Save or discard your asset edits before refreshing: " + path);
            }
        }

        private static Transform RequireFootprint(GameObject prefab)
        {
            var footprint = prefab.transform.Find("Footprint");
            if (footprint == null || footprint.GetComponent<MeshFilter>()?.sharedMesh == null
                || footprint.GetComponent<MeshRenderer>() == null || footprint.GetComponent<Collider>() != null)
                throw new InvalidOperationException("Expected one collider-free Footprint mesh in " + prefab.name);
            return footprint;
        }

        private static string Capture(Transform footprint) => string.Join("\n",
            footprint.GetComponents<Component>().Select(component => EditorJsonUtility.ToJson(component)));
    }
}

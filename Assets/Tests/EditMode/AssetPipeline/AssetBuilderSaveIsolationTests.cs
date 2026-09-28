using System.IO;
using System.Collections.Generic;
using AnimalCafe.EditorTools.P8R;
using AnimalCafe.EditorTools.AssetPipeline;
using AnimalCafe.EditorTools.Phase7;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.AssetPipeline
{
    public sealed class AssetBuilderSaveIsolationTests
    {
        [TestCase("benchmark")]
        [TestCase("phase7")]
        [TestCase("formal")]
        public void Build_SavesGeneratedMaterialButPreservesUnrelatedUnsavedEdit(string entry)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                Assert.Ignore("Close caller-owned Prefab Mode before running asset builder tests.");
            P8RFurnitureUiBuilder.RequireCleanLoadedAssets();
            var roots = entry == "benchmark"
                ? new[] { "Assets/Art/VisualPipeline/Benchmarks" }
                : new[] { "Assets/Art/Phase7", "Assets/UI/Phase7" };
            var snapshot = CaptureFiles(roots);
            foreach (var dependency in new[] {
                "ArtSource/Phase7/FormalAssetProvenance.json",
                "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset",
                "Assets/Art/Phase4/Environment/Materials/M_Environment_Entrance_01.mat",
                "Assets/UI/Phase6/Fonts/NotoSansSC-Phase6 SDF.asset" })
                if (File.Exists(dependency)) snapshot[dependency] = File.ReadAllBytes(dependency);
            var path = "Assets/Tests/BuilderSaveIsolation_" + System.Guid.NewGuid().ToString("N") + ".mat";
            var targetPath = entry == "benchmark"
                ? "Assets/Art/VisualPipeline/Benchmarks/Materials/M_Benchmark_WorkTableOriginal_01.mat"
                : "Assets/Art/Phase7/Materials/M_SM_WallDecor_Monitor_01_BaseColor.mat";
            var target = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            Assert.That(target, Is.Not.Null);
            Assert.That(EditorUtility.IsDirty(target), Is.False, "Preserve caller-owned unsaved target edits.");
            var unrelated = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                // Persist a deliberately stale generated value; the builder must save its correction.
                // 自有资源需要真正写回修正，无关资源的未保存编辑则必须保留。
                target.SetColor("_BaseColor", Color.magenta);
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssetIfDirty(target);
                unrelated.SetColor("_BaseColor", Color.red);
                AssetDatabase.CreateAsset(unrelated, path);
                AssetDatabase.SaveAssetIfDirty(unrelated);
                var unrelatedBefore = File.ReadAllBytes(path);
                unrelated.SetColor("_BaseColor", Color.green);
                EditorUtility.SetDirty(unrelated);
                Assert.That(EditorUtility.IsDirty(unrelated), Is.True);

                if (entry == "benchmark") BenchmarkAssetProductionBuilder.Build();
                else if (entry == "phase7") Phase7SurfaceAssetBuilder.BuildOrUpdateAssets();
                else Phase7FormalAssetIntake.Build();

                // Reimport the owned material to verify disk persistence, not just in-memory values.
                // 重载自有 Material，验证磁盘上的值，而不是仅检查内存。
                AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Assert.That(AssetDatabase.LoadAssetAtPath<Material>(targetPath).GetColor("_BaseColor"), Is.EqualTo(Color.white));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(unrelatedBefore),
                    "A builder must not save another asset's unsaved edits.");
                Assert.That(EditorUtility.IsDirty(unrelated), Is.True);
                Assert.That(unrelated.GetColor("_BaseColor"), Is.EqualTo(Color.green));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
                if (unrelated != null && !EditorUtility.IsPersistent(unrelated))
                    Object.DestroyImmediate(unrelated);
                // Restore the whole generated set, not just the material used by the assertion.
                // 恢复整组生成资源，避免测试改变其他自有资源的磁盘内容。
                foreach (var root in roots)
                    foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                        if (!snapshot.ContainsKey(file)) File.Delete(file);
                foreach (var file in snapshot)
                    File.WriteAllBytes(file.Key, file.Value);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
        }

        private static Dictionary<string, byte[]> CaptureFiles(string[] roots)
        {
            var result = new Dictionary<string, byte[]>();
            foreach (var root in roots)
            {
                Assert.That(Directory.Exists(root), Is.True, "Generated fixtures must exist before testing.");
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                    result.Add(file, File.ReadAllBytes(file));
            }
            return result;
        }
    }
}

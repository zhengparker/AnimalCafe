using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using AnimalCafe.Decoration;
using AnimalCafe.EditorTools.P8R;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnimalCafe.Tests.EditMode
{
    public sealed class LegacyMainCafeFixtureTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void LegacyFixture_RestoresOnlyMainEntryAndKeepsUnrelatedDirtySceneAndSelection(bool loadedMain)
        {
            VerifyCallerStateRestoration(loadedMain, false);
        }

        [Test]
        public void LegacyFixture_MappedReadersKeepOldBytesDuringInstallAndExactRestoration()
        {
            VerifyCallerStateRestoration(true, true);
        }

        [Test]
        public void LegacyFixture_InstallReplacesMappedOwnedSceneAndPreservesMeta()
        {
            var path = "Assets/__LegacyMapped_" + Guid.NewGuid().ToString("N") + ".unity";
            try
            {
                Assert.That(AssetDatabase.CopyAsset("Assets/Scenes/MainCafe.unity", path), Is.True);
                var original = File.ReadAllBytes(path);
                var meta = File.ReadAllBytes(path + ".meta");
                using (var reader = new MappedReader(path))
                using (var metaReader = new MappedReader(path + ".meta"))
                {
                    LegacyMainCafeFixture.Install(path);
                    Assert.That(File.ReadAllBytes(path), Is.Not.EqualTo(original));
                    Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(meta));
                    reader.AssertBytes(original);
                    metaReader.AssertBytes(meta);
                    var legacy = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    var controller = legacy.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true)).Single();
                    Assert.That(P8RFurnitureUiBuilder.HasAnyP8RWiring(controller), Is.False);
                }
            }
            finally
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(path);
            }
        }

        private static void VerifyCallerStateRestoration(bool loadedMain, bool mappedReaders)
        {
            const string mainPath = "Assets/Scenes/MainCafe.unity";
            var callerPath = "Assets/__LegacyCaller_" + Guid.NewGuid().ToString("N") + ".unity";
            var previousActive = SceneManager.GetActiveScene();
            var previousSelection = Selection.objects.Select(value => (Value: value, Id: GlobalObjectId.GetGlobalObjectIdSlow(value))).ToArray();
            var previousSelected = (Value: Selection.activeObject, Id: GlobalObjectId.GetGlobalObjectIdSlow(Selection.activeObject));
            var originalScenes = Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray();
            var originalMain = SceneManager.GetSceneByPath(mainPath);
            Assert.That(!originalMain.IsValid() || !originalMain.isLoaded || !originalMain.isDirty, Is.True,
                "Test refuses a caller-owned dirty MainCafe before taking cleanup ownership.");
            var hadMain = originalMain.IsValid();
            var originallyLoaded = hadMain && originalMain.isLoaded;
            var originallyActive = previousActive.path == mainPath;
            var originalIndex = Array.FindIndex(originalScenes, item => item.path == mainPath);
            Scene caller = default;
            try
            {
                Assert.That(AssetDatabase.CopyAsset("Assets/Scenes/SampleScene.unity", callerPath), Is.True);
                caller = EditorSceneManager.OpenScene(callerPath, OpenSceneMode.Additive);
                var marker = new GameObject("Unsaved caller marker");
                SceneManager.MoveGameObjectToScene(marker, caller);
                marker.transform.position = new Vector3(2, 3, 4);
                EditorSceneManager.MarkSceneDirty(caller);
                var main = originalMain;
                if (main.IsValid() && main.isLoaded)
                    Assert.That(main.isDirty, Is.False, "Test refuses a caller-owned dirty MainCafe.");
                if (!main.IsValid() || !main.isLoaded)
                {
                    main = EditorSceneManager.OpenScene(mainPath, OpenSceneMode.Additive);
                }
                Assert.That(main.isDirty, Is.False, "Only a clean MainCafe may be temporarily replaced.");
                if (!loadedMain) EditorSceneManager.CloseScene(main, false);
                var selectedMain = loadedMain ? main.GetRootGameObjects().First() : null;
                Selection.objects = selectedMain == null ? new UnityEngine.Object[] { marker }
                    : new UnityEngine.Object[] { marker, selectedMain, marker.transform };
                Selection.activeObject = marker.transform;
                var selectedIds = Selection.objects.Select(GlobalObjectId.GetGlobalObjectIdSlow).ToArray();
                var selectedIndex = Array.IndexOf(Selection.objects, Selection.activeObject);
                var activeSelectedId = GlobalObjectId.GetGlobalObjectIdSlow(Selection.activeObject);
                SceneManager.SetActiveScene(loadedMain ? main : caller);
                var beforeSetup = EditorSceneManager.GetSceneManagerSetup().Select(item => item.path + "|" + item.isLoaded + "|" + item.isActive).ToArray();
                var beforeBytes = File.ReadAllBytes(mainPath);
                var beforeMeta = File.ReadAllBytes(mainPath + ".meta");
                var callerHandle = caller.handle;
                // Read-only maps allow replacement, but Windows refuses a truncating write.
                // 旧 view 必须继续读取旧 bytes；同时验证真实 fixture 的安装和恢复。
                using (var originalReader = mappedReaders ? new MappedReader(mainPath) : null)
                using (var metaReader = mappedReaders ? new MappedReader(mainPath + ".meta") : null)
                {
                    using (var fixture = new LegacyMainCafeFixture())
                    {
                        var legacy = EditorSceneManager.OpenScene(mainPath, OpenSceneMode.Additive);
                        var controller = legacy.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<DecorationModeController>(true)).Single();
                        Assert.That(P8RFurnitureUiBuilder.HasAnyP8RWiring(controller), Is.False);
                        if (mappedReaders)
                        {
                            var legacyBytes = File.ReadAllBytes(mainPath);
                            Assert.That(legacyBytes, Is.Not.EqualTo(beforeBytes), "Install must publish legacy bytes.");
                            originalReader.AssertBytes(beforeBytes);
                            using (var legacyReader = new MappedReader(mainPath))
                            {
                                fixture.Dispose();
                                Assert.That(File.ReadAllBytes(mainPath), Is.EqualTo(beforeBytes));
                                legacyReader.AssertBytes(legacyBytes);
                            }
                        }
                    }
                    originalReader?.AssertBytes(beforeBytes);
                    metaReader?.AssertBytes(beforeMeta);
                }
                Assert.That(File.ReadAllBytes(mainPath), Is.EqualTo(beforeBytes));
                Assert.That(File.ReadAllBytes(mainPath + ".meta"), Is.EqualTo(beforeMeta));
                Assert.That(caller.handle, Is.EqualTo(callerHandle));
                Assert.That(caller.isLoaded && caller.isDirty, Is.True);
                Assert.That(marker != null && marker.transform.position == new Vector3(2, 3, 4), Is.True);
                Assert.That(EditorSceneManager.GetSceneManagerSetup().Select(item => item.path + "|" + item.isLoaded + "|" + item.isActive), Is.EqualTo(beforeSetup));
                Assert.That(Selection.objects.Select(GlobalObjectId.GetGlobalObjectIdSlow), Is.EqualTo(selectedIds));
                Assert.That(Array.IndexOf(Selection.objects, Selection.activeObject), Is.EqualTo(selectedIndex));
                Assert.That(GlobalObjectId.GetGlobalObjectIdSlow(Selection.activeObject), Is.EqualTo(activeSelectedId));
            }
            finally
            {
                var main = SceneManager.GetSceneByPath(mainPath);
                if (main.IsValid()) EditorSceneManager.CloseScene(main, true);
                if (hadMain)
                {
                    main = EditorSceneManager.OpenScene(mainPath, OpenSceneMode.Additive);
                    var next = originalScenes.Skip(originalIndex + 1).FirstOrDefault(item => item.IsValid());
                    var previous = originalScenes.Take(originalIndex).LastOrDefault(item => item.IsValid());
                    if (next.IsValid()) EditorSceneManager.MoveSceneBefore(main, next);
                    else if (previous.IsValid()) EditorSceneManager.MoveSceneAfter(main, previous);
                    if (originallyActive && originallyLoaded) SceneManager.SetActiveScene(main);
                    if (!originallyLoaded) EditorSceneManager.CloseScene(main, false);
                }
                if (caller.IsValid() && caller.isLoaded) EditorSceneManager.CloseScene(caller, true);
                AssetDatabase.DeleteAsset(callerPath);
                if (!originallyActive && previousActive.IsValid() && previousActive.isLoaded) SceneManager.SetActiveScene(previousActive);
                var selection = previousSelection.Select(item => item.Value != null ? item.Value
                    : GlobalObjectId.GlobalObjectIdentifierToObjectSlow(item.Id)).ToArray();
                Selection.objects = selection;
                Selection.activeObject = previousSelected.Value != null ? previousSelected.Value
                    : GlobalObjectId.GlobalObjectIdentifierToObjectSlow(previousSelected.Id);
            }
        }

        private sealed class MappedReader : IDisposable
        {
            private readonly FileStream stream;
            private readonly MemoryMappedFile mapping;
            private readonly MemoryMappedViewAccessor view;

            public MappedReader(string path)
            {
                stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                try
                {
                    mapping = MemoryMappedFile.CreateFromFile(stream, null, 0, MemoryMappedFileAccess.Read,
                        HandleInheritability.None, true);
                    view = mapping.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                }
                catch
                {
                    mapping?.Dispose();
                    stream.Dispose();
                    throw;
                }
            }

            public void AssertBytes(byte[] expected)
            {
                var actual = new byte[expected.Length];
                Assert.That(view.ReadArray(0, actual, 0, actual.Length), Is.EqualTo(actual.Length));
                Assert.That(actual, Is.EqualTo(expected), "A retained read-only view must keep its original bytes.");
            }

            public void Dispose()
            {
                view.Dispose();
                mapping.Dispose();
                stream.Dispose();
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using AnimalCafe.EditorTools.Phase7;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase7
{
    public sealed class Phase7PublicationIoTests
    {
        [Test]
        public void CopyRepositorySources_PublishesChangedCanonicalBytesWithMappedReaderAndPreservesAllMeta()
        {
            var expected = DerivedDestinations();
            var snapshots = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var guids = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in expected)
            {
                Assert.That(File.ReadAllBytes(item.Key), Is.EqualTo(item.Value), "Refuse noncanonical destination: " + item.Key);
                snapshots.Add(item.Key, File.ReadAllBytes(item.Key));
                snapshots.Add(item.Key + ".meta", File.ReadAllBytes(item.Key + ".meta"));
                guids.Add(item.Key, AssetDatabase.AssetPathToGUID(item.Key));
                Assert.That(guids[item.Key], Is.Not.Empty);
            }
            var target = "Assets/Art/Phase7/Models/SM_WallDecor_Monitor_01.fbx";
            var oldBytes = new byte[] { 19, 7, 25, 0, 201, 42 };
            var temporaryBefore = PublicationTemporaries(expected.Keys);
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                // Never import the temporary noncanonical model; restore before refresh is allowed.
                // 只在本次 snapshot 的保护窗口内替换 bytes，finally 精确恢复全部目标。
                RestoreBytes(target, oldBytes);
                using (var reader = new MappedReader(target))
                using (var metaReader = new MappedReader(target + ".meta"))
                {
                    InvokePrivate(typeof(Phase7FormalAssetIntake), "CopyRepositorySources");
                    foreach (var item in expected)
                    {
                        Assert.That(File.ReadAllBytes(item.Key), Is.EqualTo(item.Value), item.Key);
                        Assert.That(File.ReadAllBytes(item.Key + ".meta"), Is.EqualTo(snapshots[item.Key + ".meta"]));
                        Assert.That(AssetDatabase.AssetPathToGUID(item.Key), Is.EqualTo(guids[item.Key]));
                    }
                    reader.AssertBytes(oldBytes);
                    metaReader.AssertBytes(snapshots[target + ".meta"]);
                    Assert.That(PublicationTemporaries(expected.Keys), Is.EqualTo(temporaryBefore));
                }
            }
            finally
            {
                try
                {
                    foreach (var item in snapshots)
                        if (!File.ReadAllBytes(item.Key).SequenceEqual(item.Value)) RestoreBytes(item.Key, item.Value);
                    foreach (var item in snapshots)
                        Assert.That(File.ReadAllBytes(item.Key), Is.EqualTo(item.Value), "Exact restoration: " + item.Key);
                    foreach (var item in guids)
                        Assert.That(AssetDatabase.AssetPathToGUID(item.Key), Is.EqualTo(item.Value));
                    Assert.That(PublicationTemporaries(expected.Keys), Is.EqualTo(temporaryBefore));
                }
                finally { AssetDatabase.AllowAutoRefresh(); }
            }
        }

        [Test]
        public void NormalizeSerializedWhitespace_PublishesChangedUtf8WithoutBomWithMappedReaderAndSameGuid()
        {
            var path = "Assets/__P7Normalize_" + Guid.NewGuid().ToString("N") + ".txt";
            var original = "café 中文  \r\nnext\t\nlast  ";
            var expected = new UTF8Encoding(false).GetBytes("café 中文\r\nnext\nlast");
            try
            {
                File.WriteAllText(path, original, new UTF8Encoding(true));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var before = File.ReadAllBytes(path);
                var meta = File.ReadAllBytes(path + ".meta");
                var guid = AssetDatabase.AssetPathToGUID(path);
                var temporaryBefore = PublicationTemporaries(new[] { path });
                using (var reader = new MappedReader(path))
                using (var metaReader = new MappedReader(path + ".meta"))
                {
                    InvokePrivate(typeof(Phase7DecorationSceneSetup), "NormalizeSerializedWhitespace", path);
                    Assert.That(File.ReadAllBytes(path), Is.EqualTo(expected));
                    Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(meta));
                    Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                    Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(path).text, Is.EqualTo(Encoding.UTF8.GetString(expected)),
                        "Normalization must import the published text synchronously.");
                    reader.AssertBytes(before);
                    metaReader.AssertBytes(meta);
                    Assert.That(PublicationTemporaries(new[] { path }), Is.EqualTo(temporaryBefore));
                }
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BytePublication_PublishesExactBytesForMissingAndExistingDestination(bool exists)
        {
            WithOwnedDirectory(folder =>
            {
                var path = Path.Combine(folder, "publication.bin");
                if (exists) File.WriteAllBytes(path, new byte[] { 3, 2, 1 });
                var expected = new byte[] { 0, 255, 9, 10, 201 };
                PublishBytes(path, expected);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(expected));
                Assert.That(Directory.GetFiles(folder), Is.EqualTo(new[] { path }));
            });
        }

        [Test]
        public void BytePublication_BlockedReplacePropagatesAndPreservesDestinationWithoutTemporaryLeak()
        {
            WithOwnedDirectory(folder =>
            {
                var path = Path.Combine(folder, "blocked.bin");
                var before = new byte[] { 11, 22, 33 };
                File.WriteAllBytes(path, before);
                // Deny delete-sharing deliberately: replacement must fail without a truncating fallback.
                // 锁不允许 Replace 时，异常必须保留，原文件 bytes 不变。
                using (var blocker = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    Assert.Throws<IOException>(() => PublishBytes(path, new byte[] { 99 }));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
                Assert.That(Directory.GetFiles(folder), Is.EqualTo(new[] { path }));
            });
        }

        [Test]
        public void BytePublication_MissingParentPropagatesWithoutCreatingDestination()
        {
            WithOwnedDirectory(folder =>
            {
                var path = Path.Combine(folder, "missing", "publication.bin");
                Assert.Throws<DirectoryNotFoundException>(() => PublishBytes(path, new byte[] { 1 }));
                Assert.That(Directory.GetFileSystemEntries(folder), Is.Empty);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NormalizeSerializedWhitespace_RespectsNoOpAndPropagatesBlockedChangedWrite(bool changed)
        {
            WithOwnedDirectory(folder =>
            {
                var path = Path.Combine(folder, "normalize.txt");
                File.WriteAllText(path, changed ? "old  \r\n" : "old\r\n", new UTF8Encoding(true));
                var before = File.ReadAllBytes(path);
                using (var blocker = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (changed)
                        Assert.Throws<IOException>(() => InvokePrivate(typeof(Phase7DecorationSceneSetup), "NormalizeSerializedWhitespace", path));
                    else
                        Assert.DoesNotThrow(() => InvokePrivate(typeof(Phase7DecorationSceneSetup), "NormalizeSerializedWhitespace", path));
                }
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before), "Failure/no-op must preserve BOM, newlines and exact bytes.");
                Assert.That(Directory.GetFiles(folder), Is.EqualTo(new[] { path }));
            });
        }

        private static void PublishBytes(string path, byte[] bytes)
        {
            var type = typeof(Phase7FormalAssetIntake).Assembly.GetType("AnimalCafe.EditorTools.Phase7.Phase7BytePublication");
            Assert.That(type, Is.Not.Null);
            InvokePrivate(type, "Write", path, bytes);
        }

        private static void WithOwnedDirectory(Action<string> test)
        {
            var folder = Path.Combine(Path.GetTempPath(), "AnimalCafe-P7-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try { test(folder); }
            finally { Directory.Delete(folder, true); }
        }

        private static Dictionary<string, byte[]> DerivedDestinations()
        {
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var model in Phase7FormalAssetIntake.DerivedSha256.Keys.Where(name => name.EndsWith(".fbx", StringComparison.Ordinal)))
            {
                var stem = Path.GetFileNameWithoutExtension(model);
                var texture = Phase7FormalAssetIntake.DerivedSha256.Keys.Single(name => name.StartsWith(stem + "_", StringComparison.Ordinal));
                foreach (var pair in new[] {
                    new KeyValuePair<string,string>(model, "Assets/Art/Phase7/Models/" + model),
                    new KeyValuePair<string,string>(texture, "Assets/Art/Phase7/Textures/T_" + stem + "_BaseColor.png") })
                {
                    var bytes = File.ReadAllBytes("ArtSource/Phase7/Derived/" + pair.Key);
                    using (var hash = SHA256.Create())
                        Assert.That(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""),
                            Is.EqualTo(Phase7FormalAssetIntake.DerivedSha256[pair.Key]));
                    result.Add(pair.Value, bytes);
                }
            }
            Assert.That(result.Count, Is.EqualTo(10));
            return result;
        }

        private static string[] PublicationTemporaries(IEnumerable<string> paths) => paths
            .Select(Path.GetDirectoryName).Distinct().SelectMany(folder => Directory.GetFiles(folder, ".*.tmp"))
            .OrderBy(path => path, StringComparer.Ordinal).ToArray();

        private static void InvokePrivate(Type type, string methodName, params object[] arguments)
        {
            var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, "Expected real P7 method " + methodName);
            try { method.Invoke(null, arguments); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
        }

        private static void RestoreBytes(string path, byte[] bytes)
        {
            var temporary = Path.Combine(Path.GetDirectoryName(path), "." + Path.GetFileName(path) + ".p7-fixture-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    stream.Write(bytes, 0, bytes.Length);
                File.Replace(temporary, path, null);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
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
                    mapping = MemoryMappedFile.CreateFromFile(stream, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
                    view = mapping.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
                }
                catch { mapping?.Dispose(); stream.Dispose(); throw; }
            }

            public void AssertBytes(byte[] expected)
            {
                var actual = new byte[expected.Length];
                Assert.That(view.ReadArray(0, actual, 0, actual.Length), Is.EqualTo(actual.Length));
                Assert.That(actual, Is.EqualTo(expected), "Retained view must keep the old bytes.");
            }

            public void Dispose() { view.Dispose(); mapping.Dispose(); stream.Dispose(); }
        }
    }
}

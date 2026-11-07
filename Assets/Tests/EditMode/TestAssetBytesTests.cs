using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode
{
    public sealed class TestAssetBytesTests
    {
        private string directory;
        private string target;

        [SetUp]
        public void SetUp()
        {
            // Only this fixture's unique Temp directory is owned. 仅操作独有测试目录。
            directory = Path.Combine(Path.GetFullPath("Temp"), "TestAssetBytes-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            target = Path.Combine(directory, "snapshot.bin");
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(directory, true);
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void Restore_MissingOrExistingTarget_PublishesExactBytes(bool existing, bool empty)
        {
            if (existing) File.WriteAllBytes(target, new byte[] { 8, 7, 6 });
            var expected = empty ? new byte[0] : new byte[] { 1, 2, 3, 4 };

            TestAssetBytes.Restore(target, expected);

            Assert.That(File.ReadAllBytes(target), Is.EqualTo(expected));
            Assert.That(Directory.GetFiles(directory), Is.EquivalentTo(new[] { target }));
        }

        [Test]
        [Platform("Win")]
        public void Restore_ChangedMappedTarget_PublishesNewBytesAndPreservesOldView()
        {
            File.WriteAllBytes(target, new byte[] { 8, 7, 6 });
            using (var reader = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var mapping = MemoryMappedFile.CreateFromFile(reader, null, 0, MemoryMappedFileAccess.Read,
                HandleInheritability.None, true))
            using (var view = mapping.CreateViewAccessor(0, 3, MemoryMappedFileAccess.Read))
            {
                // A truncate write fails with Windows IO1224 while this real mapping stays open.
                // 保持真实 mapped view 打开，验证恢复无需截断原文件。
                TestAssetBytes.Restore(target, new byte[] { 1, 2, 3, 4 });

                Assert.That(File.ReadAllBytes(target), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
                Assert.That(new[] { view.ReadByte(0), view.ReadByte(1), view.ReadByte(2) }, Is.EqualTo(new byte[] { 8, 7, 6 }));
            }
            Assert.That(Directory.GetFiles(directory), Is.EquivalentTo(new[] { target }));
        }

        [Test]
        [Platform("Win")]
        public void Restore_IdenticalBytesUnderReaderDenyingReplacement_DoesNotWrite()
        {
            File.WriteAllBytes(target, new byte[] { 8, 7, 6 });
            var timestamp = File.GetLastWriteTimeUtc(target);
            using (var reader = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
                TestAssetBytes.Restore(target, new byte[] { 8, 7, 6 });

            Assert.That(File.ReadAllBytes(target), Is.EqualTo(new byte[] { 8, 7, 6 }));
            Assert.That(File.GetLastWriteTimeUtc(target), Is.EqualTo(timestamp));
            Assert.That(Directory.GetFiles(directory), Is.EquivalentTo(new[] { target }));
        }

        [Test]
        [Platform("Win")]
        public void Restore_DeniedReplacement_PropagatesAndKeepsOldBytesAndForeignTemp()
        {
            File.WriteAllBytes(target, new byte[] { 8, 7, 6 });
            var foreignTemp = Path.Combine(directory, ".snapshot.bin.foreign.tmp");
            File.WriteAllBytes(foreignTemp, new byte[] { 5, 4 });

            using (var reader = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Throws<IOException>(() => TestAssetBytes.Restore(target, new byte[] { 1, 2, 3, 4 }));

            Assert.That(File.ReadAllBytes(target), Is.EqualTo(new byte[] { 8, 7, 6 }));
            Assert.That(File.ReadAllBytes(foreignTemp), Is.EqualTo(new byte[] { 5, 4 }));
            Assert.That(Directory.GetFiles(directory), Is.EquivalentTo(new[] { target, foreignTemp }));
        }

        [Test]
        public void Restore_MissingParent_PropagatesWithoutCreatingFiles()
        {
            var missingTarget = Path.Combine(directory, "missing", "snapshot.bin");

            Assert.Throws<DirectoryNotFoundException>(() => TestAssetBytes.Restore(missingTarget, new byte[] { 1, 2 }));

            Assert.That(Directory.GetFileSystemEntries(directory), Is.Empty);
        }

        [Test]
        public void Restore_Success_LeavesForeignTempUntouched()
        {
            File.WriteAllBytes(target, new byte[] { 8, 7, 6 });
            var foreignTemp = Path.Combine(directory, ".snapshot.bin.foreign.tmp");
            File.WriteAllBytes(foreignTemp, new byte[] { 5, 4 });

            TestAssetBytes.Restore(target, new byte[] { 1, 2, 3, 4 });

            Assert.That(File.ReadAllBytes(target), Is.EqualTo(new byte[] { 1, 2, 3, 4 }));
            Assert.That(File.ReadAllBytes(foreignTemp), Is.EqualTo(new byte[] { 5, 4 }));
            Assert.That(Directory.GetFiles(directory), Is.EquivalentTo(new[] { target, foreignTemp }));
        }
    }
}

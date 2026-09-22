using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RUiAssetImporterTests
    {
        private const string Root = "Assets/UI/P8R";
        private const string ManifestPath = "Assets/Editor/P8R/ImportManifest.json";

        // Independent manifest reader: expected values do not come from importer code.
        // 独立读取已批准的素材规格，不用 importer 计算自己的预期值。
        public static IEnumerable<TestCaseData> Sprites()
        {
            return ReadManifest().files.Select(entry =>
                new TestCaseData(entry.file).SetName("P8R_ImportedSprite_" + entry.file.Replace("/", "_")));
        }

        [TestCaseSource(nameof(Sprites))]
        public void ImportedSprite_MatchesApprovedPngAndUiSettings(string relativePath)
        {
            var entry = ReadManifest().files.Single(item => item.file == relativePath);
            var path = Root + "/" + relativePath;
            Assert.That(File.Exists(path), Is.True, "Approved runtime PNG has not been imported: " + path);
            Assert.That(Hash(path), Is.EqualTo(entry.sha256), "Import must preserve the approved PNG bytes.");
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
            Assert.That(importer.alphaIsTransparency, Is.True);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.FromInput));
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100f));
            Assert.That(importer.wrapMode, Is.EqualTo(
                entry.kind == "tile" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp));
            foreach (var platform in new[] { "Standalone", "Android", "iPhone" })
                Assert.That(importer.GetPlatformTextureSettings(platform).overridden, Is.False, platform);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite, Is.Not.Null, "PNG must be usable by a uGUI Image.");
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(entry.width, entry.height)));
            Assert.That(sprite.texture.width, Is.EqualTo(entry.width));
            Assert.That(sprite.texture.height, Is.EqualTo(entry.height));
            Assert.That(sprite.border, Is.EqualTo(new Vector4(
                entry.border[0], entry.border[1], entry.border[2], entry.border[3])));
            Assert.That(AssetDatabase.AssetPathToGUID(path), Has.Length.EqualTo(32));
        }

        [Test]
        public void ImportedSet_ContainsOnlyApprovedRuntimeAssets()
        {
            var manifest = ReadManifest();
            Assert.That(manifest.files.Length, Is.EqualTo(154));
            var groups = new Dictionary<string, int>
            {
                { "Panels", 3 }, { "Buttons", 7 }, { "Cards_Tabs", 8 }, { "Icons", 93 },
                { "Feedback", 10 }, { "Auxiliary", 11 }, { "Thumbnails", 22 }
            };
            foreach (var group in groups)
                Assert.That(manifest.files.Count(item => item.file.StartsWith(group.Key + "/", StringComparison.Ordinal)),
                    Is.EqualTo(group.Value), group.Key);
            Assert.That(Directory.Exists(Root), Is.True, "Runtime sprite directory is missing.");
            var actual = groups.Keys.SelectMany(group => Directory.GetFiles(Root + "/" + group, "*", SearchOption.AllDirectories))
                .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .Select(path => path.Replace('\\', '/')).OrderBy(path => path).ToArray();
            Assert.That(actual, Is.EqualTo(manifest.files.Select(item => Root + "/" + item.file).OrderBy(path => path)));
        }

        [Test]
        public void Reimport_TwicePreservesPngMetaAndGuid()
        {
            using (var source = new SourceFixture())
            {
                // Extra files must not be swept into Assets just because they exist in the source.
                Directory.CreateDirectory(Path.Combine(source.Path, "Examples"));
                File.WriteAllText(Path.Combine(source.Path, "Examples", "not-runtime.txt"), "fixture");
                var original = Snapshot();
                var sourceOriginal = SourceSnapshot(source.Path);
                Assert.That(InvokeImport(source.Path), Is.EqualTo(154));
                Assert.That(InvokeImport(source.Path), Is.EqualTo(154));
                Assert.That(Snapshot(), Is.EqualTo(original), "A no-op import must preserve PNG and meta bytes.");
                Assert.That(SourceSnapshot(source.Path), Is.EqualTo(sourceOriginal), "Source package is read-only.");
                Assert.That(Directory.Exists(Root + "/Examples"), Is.False);
            }
        }

        [TestCase("missing")]
        [TestCase("corrupt")]
        public void Import_RejectsInvalidSourceBeforeChangingTargets(string failure)
        {
            using (var source = new SourceFixture())
            {
                var path = Path.Combine(source.Path, ReadManifest().files.Last().file);
                if (failure == "missing") File.Delete(path); // This fixture owns only its temporary copy.
                else File.WriteAllBytes(path, new byte[] { 0, 1, 2, 3 });
                var original = Snapshot();
                var error = Assert.Throws<InvalidOperationException>(() => InvokeImport(source.Path));
                Assert.That(error.Message, Does.Contain(ReadManifest().files.Last().file));
                Assert.That(Snapshot(), Is.EqualTo(original));
            }
        }

        [Test]
        public void Import_RejectsChangedTargetWithoutOverwritingIt()
        {
            using (var source = new SourceFixture())
            {
                var path = Root + "/" + ReadManifest().files.Last().file;
                var png = File.ReadAllBytes(path);
                var changed = png.Concat(new byte[] { 23 }).ToArray();
                try
                {
                    File.WriteAllBytes(path, changed);
                    var original = Snapshot();
                    var error = Assert.Throws<InvalidOperationException>(() => InvokeImport(source.Path));
                    Assert.That(error.Message, Does.Contain(path));
                    Assert.That(Snapshot(), Is.EqualTo(original));
                }
                finally
                {
                    File.WriteAllBytes(path, png);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                }
            }
        }

        [Test]
        public void Import_RejectsUnsavedImporterAndPreservesUserSetting()
        {
            using (var source = new SourceFixture())
            {
                var path = Root + "/" + ReadManifest().files.Last().file;
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var ppu = importer.spritePixelsPerUnit;
                Assert.That(EditorUtility.IsDirty(importer), Is.False, "Fixture must begin clean.");
                var original = Snapshot();
                try
                {
                    importer.spritePixelsPerUnit = 123f;
                    EditorUtility.SetDirty(importer);
                    var error = Assert.Throws<InvalidOperationException>(() => InvokeImport(source.Path));
                    Assert.That(error.Message, Does.Contain(path));
                    Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(123f));
                    Assert.That(EditorUtility.IsDirty(importer), Is.True);
                    Assert.That(Snapshot(), Is.EqualTo(original));
                }
                finally
                {
                    importer.spritePixelsPerUnit = ppu;
                    EditorUtility.ClearDirty(importer);
                }
            }
        }

        [TestCase("traversal")]
        [TestCase("unknown-kind")]
        [TestCase("duplicate")]
        [TestCase("border")]
        [TestCase("dimensions")]
        public void Import_RejectsInvalidManifestBeforeChangingTargets(string failure)
        {
            using (var source = new SourceFixture())
            {
                var originalManifest = File.ReadAllBytes(ManifestPath);
                var manifest = ReadManifest();
                var entry = manifest.files.Last();
                if (failure == "traversal") entry.file = "../escape.png";
                if (failure == "unknown-kind") entry.kind = "example";
                if (failure == "duplicate") entry.file = manifest.files[0].file;
                if (failure == "border") entry.border = new[] { 9999, 0, 0, 0 };
                if (failure == "dimensions") entry.width++;
                var original = Snapshot();
                try
                {
                    File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest));
                    Assert.Throws<InvalidOperationException>(() => InvokeImport(source.Path));
                    Assert.That(Snapshot(), Is.EqualTo(original));
                }
                finally
                {
                    File.WriteAllBytes(ManifestPath, originalManifest);
                }
            }
        }


        [Test]
        public void Import_RejectsSavedImporterChangesWithoutResettingThem()
        {
            using (var source = new SourceFixture())
            {
                var path = Root + "/" + ReadManifest().files.Last().file;
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var ppu = importer.spritePixelsPerUnit;
                var originalMeta = File.ReadAllBytes(path + ".meta");
                try
                {
                    importer.spritePixelsPerUnit = 123f;
                    importer.SaveAndReimport();
                    Assert.That(EditorUtility.IsDirty(importer), Is.False);
                    var changed = Snapshot();
                    var error = Assert.Throws<InvalidOperationException>(() => InvokeImport(source.Path));
                    Assert.That(error.Message, Does.Contain(path));
                    Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(123f));
                    Assert.That(Snapshot(), Is.EqualTo(changed));
                }
                finally
                {
                    importer.spritePixelsPerUnit = ppu;
                    importer.SaveAndReimport();
                }
                Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(originalMeta));
            }
        }

        [Test]
        public void Import_LateSourceFailureDoesNotCreateMissingFirstTarget()
        {
            using (var source = new SourceFixture())
            {
                var entries = ReadManifest().files;
                var first = Root + "/" + entries.First().file;
                var backup = Path.Combine(source.Path, "first-target-backup.png");
                var pngMoved = false;
                var metaMoved = false;
                try
                {
                    File.Move(first, backup);
                    pngMoved = true;
                    File.Move(first + ".meta", backup + ".meta");
                    metaMoved = true;
                    File.WriteAllBytes(Path.Combine(source.Path, entries.Last().file), new byte[] { 0, 1, 2 });
                    var before = Snapshot();
                    Assert.Throws<InvalidOperationException>(() => InvokeImport(source.Path));
                    Assert.That(File.Exists(first), Is.False, "Preflight must finish before the first copy.");
                    Assert.That(File.Exists(first + ".meta"), Is.False);
                    Assert.That(Snapshot(), Is.EqualTo(before));
                }
                finally
                {
                    // Restore only this fixture's temporarily moved, newly imported test target.
                    if (pngMoved)
                    {
                        if (File.Exists(first)) File.Delete(first);
                        File.Move(backup, first);
                    }
                    if (metaMoved)
                    {
                        if (File.Exists(first + ".meta")) File.Delete(first + ".meta");
                        File.Move(backup + ".meta", first + ".meta");
                    }
                    AssetDatabase.ImportAsset(first, ImportAssetOptions.ForceSynchronousImport);
                }
            }
        }

        private static int InvokeImport(string source)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("AnimalCafe.EditorTools.P8R.P8RUiAssetImporter"))
                .FirstOrDefault(candidate => candidate != null);
            Assert.That(type, Is.Not.Null, "P8RUiAssetImporter has not been implemented.");
            var method = type.GetMethod("ImportFromFolder", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            try { return (int)method.Invoke(null, new object[] { source }); }
            catch (TargetInvocationException exception) when (exception.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                throw;
            }
        }

        private static Manifest ReadManifest()
        {
            return JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
        }

        private static string[] Snapshot()
        {
            return Directory.GetFiles(Root, "*", SearchOption.AllDirectories).OrderBy(path => path)
                .Select(path => path + ":" + Hash(path)).ToArray();
        }

        private static string[] SourceSnapshot(string root)
        {
            return Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(path => path)
                .Select(path => path + ":" + Hash(path)).ToArray();
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private sealed class SourceFixture : IDisposable
        {
            public string Path { get; } = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                "Library", "P8RUiImportTests", Guid.NewGuid().ToString("N")));

            public SourceFixture()
            {
                foreach (var entry in ReadManifest().files)
                {
                    var source = Root + "/" + entry.file;
                    Assert.That(File.Exists(source), Is.True, "Integration prerequisite is missing: " + source);
                }
                foreach (var entry in ReadManifest().files)
                {
                    var target = System.IO.Path.Combine(Path, entry.file);
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target));
                    File.Copy(Root + "/" + entry.file, target);
                }
            }

            public void Dispose()
            {
                // Delete only this fixture's UUID directory; never touch user source/assets.
                var parent = System.IO.Path.GetFullPath(System.IO.Path.Combine("Library", "P8RUiImportTests"))
                    + System.IO.Path.DirectorySeparatorChar;
                if (!Path.StartsWith(parent, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Fixture cleanup escaped its owned directory.");
                if (File.Exists(System.IO.Path.Combine(Path, "first-target-backup.png")) ||
                    File.Exists(System.IO.Path.Combine(Path, "first-target-backup.png.meta")))
                    throw new InvalidOperationException("Retained target backup for recovery; do not delete: " + Path);
                if (Directory.Exists(Path)) Directory.Delete(Path, true);
            }
        }

#pragma warning disable CS0649
        [Serializable] private sealed class Manifest
        {
            public string version;
            public string sourceManifestSha256;
            public Entry[] files;
        }
        [Serializable] private sealed class Entry
        {
            public string file;
            public string kind;
            public int width;
            public int height;
            public int[] border;
            public string sha256;
        }
#pragma warning restore CS0649
    }
}

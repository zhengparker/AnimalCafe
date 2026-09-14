using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.P8R
{
    /// <summary>
    /// Import only the approved P1-8 art manifest; never modify scenes, prefabs or source PNGs.
    /// 只导入批准清单内的 UI 素材；先完整检查，再写入，发现冲突不覆盖。
    /// </summary>
    public static class P8RUiAssetImporter
    {
        private const string TargetRoot = "Assets/UI/P8R";
        private const string ManifestPath = "Assets/Editor/P8R/ImportManifest.json";
        private static readonly string[] Groups =
            { "Panels", "Buttons", "Cards_Tabs", "Icons", "Feedback", "Auxiliary", "Thumbnails" };
        private static readonly string[] Kinds =
            { "9slice_master", "sliced_aux", "icon", "badge", "fill", "sprite", "thumbnail", "tile" };

        [MenuItem("AnimalCafe/P8R/Import Approved UI Sprites...")]
        public static void ImportUsingFolderPicker()
        {
            var source = EditorUtility.OpenFolderPanel("Select the approved UI Asset package", "", "");
            if (string.IsNullOrEmpty(source)) return;
            Debug.Log($"P8R UI sprites ready: {ImportFromFolder(source)}. Scenes/prefabs were not changed.");
        }

        // Batch: -executeMethod ...ImportFromCommandLine -p8rUiSource "<folder>"
        public static void ImportFromCommandLine()
        {
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, "-p8rUiSource");
            if (index < 0 || index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-"))
                throw new InvalidOperationException("Provide -p8rUiSource with the approved UI Asset folder.");
            Debug.Log($"P8R_UI_IMPORT_OK count={ImportFromFolder(arguments[index + 1])}; scenes/prefabs/source unchanged.");
        }

        public static int ImportFromFolder(string sourceFolder)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before importing UI sprites.");
            if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
                throw new InvalidOperationException("The UI Asset source folder does not exist.");
            var sourceRoot = Path.GetFullPath(sourceFolder);
            var entries = ReadApprovedManifest();

            // Preflight all 154 entries before copying. No global SaveAssets or automatic startup hooks.
            // 先检查整套素材，避免复制到一半才发现缺图、损坏或用户文件冲突。
            foreach (var entry in entries)
            {
                var source = Path.Combine(sourceRoot, entry.file);
                var target = TargetRoot + "/" + entry.file;
                if (!File.Exists(source))
                    throw new InvalidOperationException("Missing source PNG: " + entry.file);
                if (Hash(source) != entry.sha256)
                    throw new InvalidOperationException("Source PNG differs from the approved manifest: " + entry.file);
                ValidatePng(source, entry);
                if (File.Exists(target) && Hash(target) != entry.sha256)
                    throw new InvalidOperationException("Target differs; nothing was overwritten: " + target);
                if (!File.Exists(target) && File.Exists(target + ".meta"))
                    throw new InvalidOperationException("Orphan meta requires review before import: " + target);
                var importer = AssetImporter.GetAtPath(target);
                if (importer != null && EditorUtility.IsDirty(importer))
                    throw new InvalidOperationException("Unsaved importer settings must be resolved first: " + target);
                if (File.Exists(target) &&
                    (!(importer is TextureImporter textureImporter) || !HasApprovedSettings(textureImporter, entry)))
                    throw new InvalidOperationException("Saved importer settings differ; nothing was overwritten: " + target);
            }

            foreach (var entry in entries)
            {
                var target = TargetRoot + "/" + entry.file;
                // Existing files already passed validation. Do not even assign identical settings:
                // Unity setters can dirty an importer without changing its serialized JSON.
                // 已有素材只验证不写入，保护已保存的设置并保证重跑无副作用。
                if (File.Exists(target)) continue;
                EnsureFolder(Path.GetDirectoryName(target).Replace('\\', '/'));
                File.Copy(Path.Combine(sourceRoot, entry.file), target, false);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(target) as TextureImporter;
                if (importer == null)
                    throw new InvalidOperationException("Unity did not create a TextureImporter: " + target);
                Configure(importer, entry);
            }
            return entries.Length;
        }

        private static Entry[] ReadApprovedManifest()
        {
            if (!File.Exists(ManifestPath))
                throw new InvalidOperationException("UI import manifest is missing: " + ManifestPath);
            Manifest manifest;
            try { manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath)); }
            catch (ArgumentException error)
            {
                throw new InvalidOperationException("UI import manifest is not valid JSON.", error);
            }
            if (manifest == null || manifest.version != "1.0" || manifest.files == null || manifest.files.Length != 154)
                throw new InvalidOperationException("Expected the approved v1.0 manifest with 154 runtime PNGs.");

            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in manifest.files)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.file))
                    throw new InvalidOperationException("Manifest contains an empty file entry.");
                var parts = entry.file.Split('/');
                if (parts.Length < 2 || !Groups.Contains(parts[0]) ||
                    parts.Any(part => string.IsNullOrWhiteSpace(part) || part == "." || part == ".." ||
                        part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.Contains("\\")) ||
                    !entry.file.EndsWith(".png", StringComparison.Ordinal) || !paths.Add(entry.file))
                    throw new InvalidOperationException("Unsafe or duplicate runtime PNG path: " + entry.file);
                if (!Kinds.Contains(entry.kind))
                    throw new InvalidOperationException("Non-runtime or unknown asset kind: " + entry.file);
                if (entry.width < 1 || entry.height < 1 || entry.width > 2048 || entry.height > 2048 ||
                    entry.border == null || entry.border.Length != 4 ||
                    entry.border.Any(value => value < 0 || value > 2048) ||
                    entry.border[0] + entry.border[2] > entry.width ||
                    entry.border[1] + entry.border[3] > entry.height)
                    throw new InvalidOperationException("Invalid PNG dimensions or Sprite Border: " + entry.file);
                // Auxiliary bars may have zero centre size on one axis.
                // 单轴辅助条允许两端Border之和等于该轴尺寸。
                if (entry.sha256 == null || !Regex.IsMatch(entry.sha256, "^[a-f0-9]{64}$"))
                    throw new InvalidOperationException("Invalid SHA-256 in manifest: " + entry.file);
            }
            return manifest.files;
        }

        private static void ValidatePng(string path, Entry entry)
        {
            byte[] header;
            using (var stream = File.OpenRead(path))
            using (var reader = new BinaryReader(stream))
                header = reader.ReadBytes(26);
            var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (header.Length != 26 || !header.Take(8).SequenceEqual(signature) ||
                header[12] != 'I' || header[13] != 'H' || header[14] != 'D' || header[15] != 'R' ||
                ReadBigEndian(header, 16) != entry.width || ReadBigEndian(header, 20) != entry.height ||
                header[24] != 8 || header[25] != 6)
                throw new InvalidOperationException("Expected approved RGBA8 PNG dimensions: " + entry.file);
        }

        private static int ReadBigEndian(byte[] bytes, int index)
        {
            return (bytes[index] << 24) | (bytes[index + 1] << 16) |
                   (bytes[index + 2] << 8) | bytes[index + 3];
        }

        private static bool HasApprovedSettings(TextureImporter importer, Entry entry)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            return importer.textureType == TextureImporterType.Sprite &&
                importer.spriteImportMode == SpriteImportMode.Single &&
                importer.spritePixelsPerUnit == 100f &&
                importer.spriteBorder == new Vector4(entry.border[0], entry.border[1], entry.border[2], entry.border[3]) &&
                importer.alphaSource == TextureImporterAlphaSource.FromInput &&
                importer.alphaIsTransparency && importer.sRGBTexture && !importer.mipmapEnabled && !importer.isReadable &&
                importer.filterMode == FilterMode.Bilinear &&
                importer.textureCompression == TextureImporterCompression.Uncompressed &&
                importer.maxTextureSize == 2048 && importer.npotScale == TextureImporterNPOTScale.None &&
                importer.wrapMode == (entry.kind == "tile" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp) &&
                settings.spriteMeshType == SpriteMeshType.FullRect &&
                settings.spriteAlignment == (int)SpriteAlignment.Center && settings.spritePivot == new Vector2(.5f, .5f) &&
                new[] { "Standalone", "Android", "iPhone" }
                    .All(platform => !importer.GetPlatformTextureSettings(platform).overridden);
        }

        private static void Configure(TextureImporter importer, Entry entry)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f; // Match CanvasScaler, not the touch-target size.
            importer.spriteBorder = new Vector4(entry.border[0], entry.border[1], entry.border[2], entry.border[3]);
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = entry.kind == "tile" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(.5f, .5f);
            importer.SetTextureSettings(settings);
            foreach (var platform in new[] { "Standalone", "Android", "iPhone" })
                if (importer.GetPlatformTextureSettings(platform).overridden)
                    importer.ClearPlatformTextureSettings(platform);

            // Only freshly copied PNGs reach this method; never save other dirty assets.
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(folder))))
                throw new InvalidOperationException("Unity could not create the import folder: " + folder);
        }

        private static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

#pragma warning disable CS0649
        [Serializable] private sealed class Manifest
        {
            public string version;
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

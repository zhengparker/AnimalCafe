using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.AssetPipeline
{
    public sealed class BenchmarkAssetTestFactory : IDisposable
    {
        public const string GeneratedFolderPath = "Assets/Tests/Generated/AssetPipeline";

        public const string BenchmarkPrefabFolderPath =
            "Assets/Art/VisualPipeline/Benchmarks/Prefabs";

        private readonly List<string> ownedAssetPaths = new List<string>();
        private readonly List<string> ownedFolderPaths = new List<string>();
        private bool disposed;

        public GameObject CreatePrefab(
            string prefabName,
            Vector3 bounds,
            int triangleCount)
        {
            ValidatePrefabName(prefabName);

            return CreatePrefabAtPath(
                $"{GeneratedFolderPath}/{prefabName}.prefab",
                bounds,
                triangleCount,
                null);
        }

        public GameObject CreatePrefabAtPath(
            string prefabPath,
            Vector3 bounds,
            int triangleCount,
            Action<GameObject> configure = null)
        {
            if (string.IsNullOrWhiteSpace(prefabPath) ||
                !prefabPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !prefabPath.EndsWith(".prefab", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Prefab path must be an Assets-relative .prefab path.",
                    nameof(prefabPath));
            }

            if (triangleCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(triangleCount));
            }

            ThrowIfPrefabAlreadyExists(prefabPath);

            EnsureGeneratedFolder();
            EnsureAssetFolders(Path.GetDirectoryName(prefabPath)?.Replace('\\', '/'));

            var prefabName = Path.GetFileNameWithoutExtension(prefabPath);
            var root = new GameObject(prefabName);
            try
            {
                var mesh = CreateMesh(bounds, triangleCount);
                var artifactName = ToSafeArtifactName(prefabPath);
                var meshPath = $"{GeneratedFolderPath}/{artifactName}_Mesh.asset";
                AssetDatabase.CreateAsset(mesh, meshPath);
                TrackOwnedAsset(meshPath);

                var material = CreateUrpLitMaterial();
                var materialPath = $"{GeneratedFolderPath}/{artifactName}_Material.mat";
                AssetDatabase.CreateAsset(material, materialPath);
                TrackOwnedAsset(materialPath);

                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                var meshFilter = visual.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;
                var meshRenderer = visual.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, bounds.y * 0.5f, 0f);
                collider.size = bounds;

                var forwardMarker = new GameObject("ForwardMarker");
                forwardMarker.transform.SetParent(root.transform, false);
                forwardMarker.transform.localPosition = new Vector3(0f, 0.05f, 0.25f);
                forwardMarker.transform.localRotation = Quaternion.identity;

                configure?.Invoke(root);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                TrackOwnedAsset(prefabPath);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public void DeleteGeneratedAssets()
        {
            if (disposed)
            {
                return;
            }

            for (var index = ownedAssetPaths.Count - 1; index >= 0; index--)
            {
                AssetDatabase.DeleteAsset(ownedAssetPaths[index]);
            }

            AssetDatabase.Refresh();
            foreach (var folderPath in ownedFolderPaths.OrderByDescending(path => path.Length))
            {
                DeleteOwnedEmptyAssetFolder(folderPath);
            }

            AssetDatabase.Refresh();
            disposed = true;
        }

        public void Dispose()
        {
            DeleteGeneratedAssets();
        }

        private static Mesh CreateMesh(Vector3 bounds, int triangleCount)
        {
            var mesh = new Mesh
            {
                name = "GeneratedBenchmarkMesh"
            };
            var vertices = new Vector3[triangleCount * 3];
            var triangles = new int[triangleCount * 3];
            var minimum = new Vector3(-bounds.x * 0.5f, 0f, -bounds.z * 0.5f);
            var maximum = new Vector3(bounds.x * 0.5f, bounds.y, bounds.z * 0.5f);

            for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
            {
                var vertexIndex = triangleIndex * 3;
                vertices[vertexIndex] = minimum;
                vertices[vertexIndex + 1] = new Vector3(maximum.x, minimum.y, minimum.z);
                vertices[vertexIndex + 2] = new Vector3(minimum.x, maximum.y, maximum.z);
                triangles[vertexIndex] = vertexIndex;
                triangles[vertexIndex + 1] = vertexIndex + 1;
                triangles[vertexIndex + 2] = vertexIndex + 2;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateUrpLitMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("URP Lit shader is required for test fixtures.");
            }

            return new Material(shader);
        }

        private static void ValidatePrefabName(string prefabName)
        {
            if (string.IsNullOrWhiteSpace(prefabName) ||
                prefabName.IndexOfAny(new[] { '/', '\\' }) >= 0 ||
                prefabName.Contains("..") ||
                Path.HasExtension(prefabName) ||
                prefabName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                throw new ArgumentException(
                    "Prefab name must be a plain filename without path separators, traversal, or an extension.",
                    nameof(prefabName));
            }
        }

        private void EnsureGeneratedFolder()
        {
            EnsureAssetFolders(GeneratedFolderPath);
        }

        private void EnsureAssetFolders(string assetFolderPath)
        {
            if (string.IsNullOrEmpty(assetFolderPath) || AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return;
            }

            var parent = Path.GetDirectoryName(assetFolderPath)?.Replace('\\', '/');
            EnsureAssetFolders(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolderPath));
            TrackOwnedFolder(assetFolderPath);
        }

        private void ThrowIfPrefabAlreadyExists(string prefabPath)
        {
            var absolutePath = ToAbsoluteAssetPath(prefabPath);
            if (AssetDatabase.LoadMainAssetAtPath(prefabPath) != null || File.Exists(absolutePath))
            {
                throw new InvalidOperationException(
                    "Fixture Prefab path must be absent before creating test content.");
            }
        }

        private void TrackOwnedAsset(string assetPath)
        {
            ownedAssetPaths.Add(assetPath);
        }

        private void TrackOwnedFolder(string assetFolderPath)
        {
            ownedFolderPaths.Add(assetFolderPath);
        }

        private static void DeleteOwnedEmptyAssetFolder(string assetFolderPath)
        {
            var absolutePath = ToAbsoluteAssetPath(assetFolderPath);
            if (!AssetDatabase.IsValidFolder(assetFolderPath) ||
                !Directory.Exists(absolutePath))
            {
                return;
            }

            if (Directory.GetFileSystemEntries(absolutePath).Length == 0)
            {
                AssetDatabase.DeleteAsset(assetFolderPath);
            }
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
        }

        private static string ToSafeArtifactName(string prefabPath)
        {
            var builder = new StringBuilder(prefabPath.Length);
            foreach (var character in prefabPath)
            {
                builder.Append(char.IsLetterOrDigit(character) ? character : '_');
            }

            return builder.ToString();
        }
    }
}

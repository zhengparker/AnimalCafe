using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private readonly string fixtureFolderPath =
            $"Assets/Tests/BenchmarkAssetFixture_{Guid.NewGuid():N}";
        private bool disposed;

        public string FixtureFolderPath => fixtureFolderPath;

        public GameObject CreatePrefab(
            string prefabName,
            Vector3 bounds,
            int triangleCount)
        {
            ValidatePrefabName(prefabName);

            return CreatePrefabAtPath(
                $"{fixtureFolderPath}/{prefabName}.prefab",
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

            var callFolderPath = CreateUniqueCallFolderPath();
            EnsureAssetFolders(callFolderPath);
            EnsureAssetFolders(Path.GetDirectoryName(prefabPath)?.Replace('\\', '/'));

            var prefabName = Path.GetFileNameWithoutExtension(prefabPath);
            var root = new GameObject(prefabName);
            try
            {
                var mesh = CreateMesh(bounds, triangleCount);
                var meshPath = $"{callFolderPath}/Mesh.asset";
                TrackOwnedAsset(meshPath);
                AssetDatabase.CreateAsset(mesh, meshPath);

                var material = CreateUrpLitMaterial();
                var materialPath = $"{callFolderPath}/Material.mat";
                TrackOwnedAsset(materialPath);
                AssetDatabase.CreateAsset(material, materialPath);

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
                TrackOwnedAsset(prefabPath);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public Mesh CreateMeshAsset(Vector3 bounds, int triangleCount)
        {
            if (triangleCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(triangleCount));
            }

            var mesh = CreateMesh(bounds, triangleCount);
            CreateOwnedAsset(mesh, "Mesh.asset");
            return mesh;
        }

        public Material CreateMaterialAsset(Shader shader)
        {
            if (shader == null)
            {
                throw new ArgumentNullException(nameof(shader));
            }

            var material = new Material(shader);
            CreateOwnedAsset(material, "Material.mat");
            return material;
        }

        public Texture2D CreateTextureAsset(int width, int height)
        {
            var texture = new Texture2D(width, height)
            {
                name = "GeneratedBenchmarkTexture"
            };
            CreateOwnedAsset(texture, "Texture.asset");
            return texture;
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

        private void CreateOwnedAsset(UnityEngine.Object asset, string fileName)
        {
            var callFolderPath = CreateUniqueCallFolderPath();
            EnsureAssetFolders(callFolderPath);
            var assetPath = $"{callFolderPath}/{fileName}";
            TrackOwnedAsset(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);
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

        private string CreateUniqueCallFolderPath()
        {
            string callFolderPath;
            do
            {
                callFolderPath = $"{fixtureFolderPath}/{Guid.NewGuid():N}";
            }
            while (AssetDatabase.IsValidFolder(callFolderPath) ||
                   Directory.Exists(ToAbsoluteAssetPath(callFolderPath)));

            return callFolderPath;
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
            if (!ownedAssetPaths.Contains(assetPath))
            {
                ownedAssetPaths.Add(assetPath);
            }
        }

        private void TrackOwnedFolder(string assetFolderPath)
        {
            if (!ownedFolderPaths.Contains(assetFolderPath))
            {
                ownedFolderPaths.Add(assetFolderPath);
            }
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

    }
}

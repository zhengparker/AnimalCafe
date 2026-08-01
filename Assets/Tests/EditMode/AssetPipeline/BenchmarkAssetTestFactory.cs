using System;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.AssetPipeline
{
    public static class BenchmarkAssetTestFactory
    {
        public const string GeneratedFolderPath = "Assets/Tests/Generated/AssetPipeline";

        public static GameObject CreatePrefab(
            string prefabName,
            Vector3 bounds,
            int triangleCount)
        {
            if (string.IsNullOrWhiteSpace(prefabName))
            {
                throw new ArgumentException("A prefab name is required.", nameof(prefabName));
            }

            if (triangleCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(triangleCount));
            }

            EnsureGeneratedFolder();

            var root = new GameObject(prefabName);
            try
            {
                var mesh = CreateMesh(bounds, triangleCount);
                var meshPath = $"{GeneratedFolderPath}/{prefabName}_Mesh.asset";
                AssetDatabase.CreateAsset(mesh, meshPath);

                var material = CreateUrpLitMaterial();
                var materialPath = $"{GeneratedFolderPath}/{prefabName}_Material.mat";
                AssetDatabase.CreateAsset(material, materialPath);

                var meshFilter = root.AddComponent<MeshFilter>();
                meshFilter.sharedMesh = mesh;
                var meshRenderer = root.AddComponent<MeshRenderer>();
                meshRenderer.sharedMaterial = material;
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, bounds.y * 0.5f, 0f);
                collider.size = bounds;

                var forwardMarker = new GameObject("ForwardMarker");
                forwardMarker.transform.SetParent(root.transform, false);
                forwardMarker.transform.localPosition = new Vector3(0f, 0.05f, 0.25f);
                forwardMarker.transform.localRotation = Quaternion.identity;

                var prefabPath = $"{GeneratedFolderPath}/{prefabName}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void DeleteGeneratedAssets()
        {
            AssetDatabase.DeleteAsset(GeneratedFolderPath);
            AssetDatabase.Refresh();
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

        private static void EnsureGeneratedFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Tests/Generated"))
            {
                AssetDatabase.CreateFolder("Assets/Tests", "Generated");
            }

            if (!AssetDatabase.IsValidFolder(GeneratedFolderPath))
            {
                AssetDatabase.CreateFolder("Assets/Tests/Generated", "AssetPipeline");
            }
        }
    }
}

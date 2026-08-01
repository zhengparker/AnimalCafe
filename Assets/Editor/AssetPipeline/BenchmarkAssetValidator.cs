using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.AssetPipeline
{
    public static class BenchmarkAssetValidator
    {
        private const string BenchmarkPrefabFolderPath =
            "Assets/Art/VisualPipeline/Benchmarks/Prefabs";

        private const float TransformTolerance = 0.0001f;
        private const float FloorToleranceMeters = 0.005f;
        private const float MinimumForwardZ = 0.01f;
        private const float ForwardAngleToleranceDegrees = 1f;

        public static BenchmarkAssetValidationReport ValidatePrefab(
            string assetPath,
            BenchmarkAssetKind kind)
        {
            var issues = new List<BenchmarkAssetValidationIssue>();
            ValidatePathAndName(assetPath, kind, issues);

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(assetPath);
                ValidateRootTransform(root, assetPath, issues);
                ValidateVisibleBounds(root, assetPath, kind, issues);
                ValidateForwardMarker(root, assetPath, issues);
                ValidateRendering(root, assetPath, kind, issues);
                ValidateLods(root, assetPath, kind, issues);
            }
            catch (Exception exception)
            {
                AddIssue(
                    issues,
                    BenchmarkAssetIssueCode.InvalidAssetPath,
                    assetPath,
                    $"Could not load Prefab contents: {exception.Message}");
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return new BenchmarkAssetValidationReport(issues);
        }

        private static void ValidatePathAndName(
            string assetPath,
            BenchmarkAssetKind kind,
            ICollection<BenchmarkAssetValidationIssue> issues)
        {
            var expectedFileName = $"PF_Benchmark_{kind}_01.prefab";
            var expectedPath = $"{BenchmarkPrefabFolderPath}/{expectedFileName}";
            var fileName = string.IsNullOrEmpty(assetPath)
                ? string.Empty
                : Path.GetFileName(assetPath);

            if (!string.Equals(assetPath, expectedPath, StringComparison.Ordinal))
            {
                AddIssue(
                    issues,
                    BenchmarkAssetIssueCode.InvalidAssetPath,
                    assetPath,
                    "Prefab path must exactly match the approved slash-separated benchmark path.");

                if (!string.Equals(fileName, expectedFileName, StringComparison.Ordinal) ||
                    !IsAscii(fileName) ||
                    fileName.IndexOf(' ') >= 0)
                {
                    AddIssue(
                        issues,
                        BenchmarkAssetIssueCode.InvalidName,
                        assetPath,
                        $"Prefab filename must be exactly {expectedFileName}.");
                }
            }
        }

        private static void ValidateRootTransform(
            GameObject root,
            string assetPath,
            ICollection<BenchmarkAssetValidationIssue> issues)
        {
            var transform = root.transform;
            if ((transform.localPosition - Vector3.zero).sqrMagnitude > TransformTolerance * TransformTolerance ||
                Quaternion.Angle(transform.localRotation, Quaternion.identity) > TransformTolerance ||
                (transform.localScale - Vector3.one).sqrMagnitude > TransformTolerance * TransformTolerance)
            {
                AddIssue(
                    issues,
                    BenchmarkAssetIssueCode.RootTransformNotIdentity,
                    assetPath,
                    "Prefab root transform must use identity position, rotation, and scale.");
            }
        }

        private static void ValidateVisibleBounds(
            GameObject root,
            string assetPath,
            BenchmarkAssetKind kind,
            ICollection<BenchmarkAssetValidationIssue> issues)
        {
            if (!TryGetRootLocalRendererBounds(root, out var visibleBounds))
            {
                AddIssue(
                    issues,
                    BenchmarkAssetIssueCode.BoundsOutsideTolerance,
                    assetPath,
                    "Prefab must contain at least one enabled visible Renderer.");
                return;
            }

            var rules = BenchmarkAssetRules.For(kind);
            if (!IsWithinTolerance(visibleBounds.size.x, rules.TargetSize.x, rules.BoundsTolerance) ||
                !IsWithinTolerance(visibleBounds.size.y, rules.TargetSize.y, rules.BoundsTolerance) ||
                !IsWithinTolerance(visibleBounds.size.z, rules.TargetSize.z, rules.BoundsTolerance) ||
                visibleBounds.min.y > FloorToleranceMeters)
            {
                AddIssue(
                    issues,
                    BenchmarkAssetIssueCode.BoundsOutsideTolerance,
                    assetPath,
                    "Visible Renderer bounds must match the approved size and rest on the floor.");
            }

            if (visibleBounds.min.y < -FloorToleranceMeters)
            {
                AddIssue(
                    issues,
                    BenchmarkAssetIssueCode.BelowGround,
                    assetPath,
                    "Visible Renderer bounds extend below the floor tolerance.");
            }
        }

        private static void ValidateForwardMarker(
            GameObject root,
            string assetPath,
            ICollection<BenchmarkAssetValidationIssue> issues)
        {
            var markers = root.GetComponentsInChildren<Transform>(true)
                .Where(candidate => candidate.name == "ForwardMarker")
                .ToArray();
            if (markers.Length != 1)
            {
                AddInvalidForwardMarkerIssue(issues, assetPath, "Prefab must contain exactly one ForwardMarker descendant.");
                return;
            }

            var marker = markers[0];
            var rootLocalPosition = root.transform.InverseTransformPoint(marker.position);
            var rootLocalForward = root.transform.InverseTransformDirection(marker.forward);
            if (rootLocalPosition.z <= MinimumForwardZ ||
                Vector3.Angle(rootLocalForward, Vector3.forward) > ForwardAngleToleranceDegrees ||
                marker.GetComponent<Renderer>() != null ||
                marker.GetComponent<MeshFilter>() != null ||
                marker.GetComponent<Collider>() != null)
            {
                AddInvalidForwardMarkerIssue(
                    issues,
                    assetPath,
                    "ForwardMarker must point along root-local +Z, sit in front of the origin, and remain non-visible.");
            }
        }

        private static void ValidateRendering(
            GameObject root,
            string assetPath,
            BenchmarkAssetKind kind,
            ICollection<BenchmarkAssetValidationIssue> issues)
        {
            var rules = BenchmarkAssetRules.For(kind);
            var renderers = GetEnabledRenderers(root).ToArray();
            var triangleRenderers = GetTriangleBudgetRenderers(root, kind, renderers);
            if (CountUniqueMeshTriangles(triangleRenderers, assetPath, issues) > rules.MaxLod0Triangles)
            {
                AddIssue(
                    issues,
                    BenchmarkAssetIssueCode.TriangleBudgetExceeded,
                    assetPath,
                    $"LOD0 triangle count exceeds the {rules.MaxLod0Triangles} triangle budget.");
            }

            var materialSlots = 0;
            var uniqueMaterials = new HashSet<Material>();
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        AddIssue(
                            issues,
                            BenchmarkAssetIssueCode.MissingMaterial,
                            assetPath,
                            "Enabled Renderer contains a null shared Material slot.");
                        continue;
                    }

                    materialSlots++;
                    uniqueMaterials.Add(material);
                    ValidateMaterial(material, assetPath, issues);
                }
            }

            if (materialSlots > rules.MaxMaterialSlots)
            {
                AddIssue(
                    issues,
                    BenchmarkAssetIssueCode.MaterialSlotBudgetExceeded,
                    assetPath,
                    $"Prefab uses {materialSlots} shared Material slots and {uniqueMaterials.Count} unique shared Materials; the budget is {rules.MaxMaterialSlots} slots.");
            }
        }

        private static void ValidateMaterial(
            Material material,
            string assetPath,
            ICollection<BenchmarkAssetValidationIssue> issues)
        {
            if (material.shader == null || material.shader.name != "Universal Render Pipeline/Lit")
            {
                AddIssue(
                    issues,
                    BenchmarkAssetIssueCode.InvalidShader,
                    assetPath,
                    "Materials must use the Universal Render Pipeline/Lit shader.");
            }
            else if (!material.HasProperty("_Surface") || material.GetFloat("_Surface") != 0f)
            {
                AddIssue(
                    issues,
                    BenchmarkAssetIssueCode.TransparentMaterial,
                    assetPath,
                    "URP Lit Materials must use the opaque _Surface value of 0.");
            }

            for (var propertyIndex = 0; propertyIndex < ShaderUtil.GetPropertyCount(material.shader); propertyIndex++)
            {
                if (ShaderUtil.GetPropertyType(material.shader, propertyIndex) != ShaderUtil.ShaderPropertyType.TexEnv)
                {
                    continue;
                }

                var texture = material.GetTexture(ShaderUtil.GetPropertyName(material.shader, propertyIndex));
                if (texture == null || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(texture)))
                {
                    continue;
                }

                if (texture.width > 512 || texture.height > 512)
                {
                    AddIssue(
                        issues,
                        BenchmarkAssetIssueCode.TextureBudgetExceeded,
                        assetPath,
                        "Project Texture references must not exceed 512 by 512 pixels.");
                }
            }
        }

        private static void ValidateLods(
            GameObject root,
            string assetPath,
            BenchmarkAssetKind kind,
            ICollection<BenchmarkAssetValidationIssue> issues)
        {
            if (!BenchmarkAssetRules.For(kind).RequiresLodGroup)
            {
                return;
            }

            var lodGroups = root.GetComponentsInChildren<LODGroup>(true);
            if (lodGroups.Length != 1)
            {
                AddIssue(
                    issues,
                    BenchmarkAssetIssueCode.MissingLodGroup,
                    assetPath,
                    "Coffee Machine requires exactly one LODGroup.");
                return;
            }

            var lods = lodGroups[0].GetLODs();
            if (lods.Length < 2 || !HasOnlyNonNullRenderers(lods[0]) || !HasOnlyNonNullRenderers(lods[1]))
            {
                AddIssue(
                    issues,
                    BenchmarkAssetIssueCode.MissingLod1,
                    assetPath,
                    "Coffee Machine requires non-empty LOD0 and LOD1 renderer levels without null Renderers.");
                return;
            }

            if (lods[0].renderers.Intersect(lods[1].renderers).Any())
            {
                AddIssue(
                    issues,
                    BenchmarkAssetIssueCode.LodReductionInsufficient,
                    assetPath,
                    "LOD0 and LOD1 must not reuse the same Renderer.");
                return;
            }

            var rules = BenchmarkAssetRules.For(kind);
            var lod0Triangles = CountUniqueMeshTriangles(lods[0].renderers, assetPath, issues);
            var lod1Triangles = CountUniqueMeshTriangles(lods[1].renderers, assetPath, issues);
            if (lod1Triangles > rules.MaxLod1Triangles)
            {
                AddIssue(
                    issues,
                    BenchmarkAssetIssueCode.LodTriangleBudgetExceeded,
                    assetPath,
                    $"LOD1 triangle count exceeds the {rules.MaxLod1Triangles} triangle budget.");
            }

            if (lod0Triangles == 0 || (float)lod1Triangles / lod0Triangles > rules.MaxLod1TriangleRatio)
            {
                AddIssue(
                    issues,
                    BenchmarkAssetIssueCode.LodReductionInsufficient,
                    assetPath,
                    $"LOD1 must use no more than {rules.MaxLod1TriangleRatio:P0} of LOD0 triangles.");
            }
        }

        private static IEnumerable<Renderer> GetEnabledRenderers(GameObject root)
        {
            return root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer =>
                    renderer.enabled &&
                    renderer.gameObject.activeInHierarchy &&
                    !IsForwardMarkerDescendant(renderer.transform));
        }

        private static bool IsForwardMarkerDescendant(Transform transform)
        {
            while (transform != null)
            {
                if (transform.name == "ForwardMarker")
                {
                    return true;
                }

                transform = transform.parent;
            }

            return false;
        }

        private static IEnumerable<Renderer> GetTriangleBudgetRenderers(
            GameObject root,
            BenchmarkAssetKind kind,
            IEnumerable<Renderer> enabledRenderers)
        {
            if (!BenchmarkAssetRules.For(kind).RequiresLodGroup)
            {
                return enabledRenderers;
            }

            var lodGroups = root.GetComponentsInChildren<LODGroup>(true);
            if (lodGroups.Length != 1)
            {
                return enabledRenderers;
            }

            var lods = lodGroups[0].GetLODs();
            if (lods.Length == 0)
            {
                return enabledRenderers;
            }

            var lodRenderers = new HashSet<Renderer>(lods
                .Where(lod => lod.renderers != null)
                .SelectMany(lod => lod.renderers)
                .Where(renderer => renderer != null));
            return lods[0].renderers
                .Where(renderer => renderer != null)
                .Concat(enabledRenderers.Where(renderer => !lodRenderers.Contains(renderer)));
        }

        private static bool HasOnlyNonNullRenderers(LOD lod)
        {
            return lod.renderers != null && lod.renderers.Length > 0 && lod.renderers.All(renderer => renderer != null);
        }

        private static int CountUniqueMeshTriangles(
            IEnumerable<Renderer> renderers,
            string assetPath,
            ICollection<BenchmarkAssetValidationIssue> issues)
        {
            var meshes = new HashSet<Mesh>();
            foreach (var renderer in renderers)
            {
                var mesh = GetMesh(renderer);
                if (mesh == null)
                {
                    AddIssue(
                        issues,
                        BenchmarkAssetIssueCode.MissingMesh,
                        assetPath,
                        "Renderer must reference a Mesh.");
                    continue;
                }

                meshes.Add(mesh);
            }

            return meshes.Sum(mesh => mesh.triangles.Length / 3);
        }

        private static Mesh GetMesh(Renderer renderer)
        {
            var meshFilter = renderer.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                return meshFilter.sharedMesh;
            }

            var skinnedMeshRenderer = renderer as SkinnedMeshRenderer;
            return skinnedMeshRenderer == null ? null : skinnedMeshRenderer.sharedMesh;
        }

        private static bool TryGetRootLocalRendererBounds(GameObject root, out Bounds combinedBounds)
        {
            var hasBounds = false;
            combinedBounds = default;
            var rootWorldToLocal = root.transform.worldToLocalMatrix;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                foreach (var rendererLocalCorner in GetBoundsCorners(renderer.localBounds))
                {
                    var worldCorner = renderer.localToWorldMatrix.MultiplyPoint3x4(rendererLocalCorner);
                    var rootLocalCorner = rootWorldToLocal.MultiplyPoint3x4(worldCorner);
                    if (hasBounds)
                    {
                        combinedBounds.Encapsulate(rootLocalCorner);
                    }
                    else
                    {
                        combinedBounds = new Bounds(rootLocalCorner, Vector3.zero);
                        hasBounds = true;
                    }
                }
            }

            return hasBounds;
        }

        private static IEnumerable<Vector3> GetBoundsCorners(Bounds bounds)
        {
            var minimum = bounds.min;
            var maximum = bounds.max;
            yield return new Vector3(minimum.x, minimum.y, minimum.z);
            yield return new Vector3(minimum.x, minimum.y, maximum.z);
            yield return new Vector3(minimum.x, maximum.y, minimum.z);
            yield return new Vector3(minimum.x, maximum.y, maximum.z);
            yield return new Vector3(maximum.x, minimum.y, minimum.z);
            yield return new Vector3(maximum.x, minimum.y, maximum.z);
            yield return new Vector3(maximum.x, maximum.y, minimum.z);
            yield return new Vector3(maximum.x, maximum.y, maximum.z);
        }

        private static bool IsWithinTolerance(float value, float target, float tolerance)
        {
            var minimum = target * (1f - tolerance);
            var maximum = target * (1f + tolerance);
            return value >= minimum && value <= maximum;
        }

        private static bool IsAscii(string value)
        {
            return value.All(character => character <= 127);
        }

        private static void AddInvalidForwardMarkerIssue(
            ICollection<BenchmarkAssetValidationIssue> issues,
            string assetPath,
            string message)
        {
            AddIssue(issues, BenchmarkAssetIssueCode.InvalidForwardMarker, assetPath, message);
        }

        private static void AddIssue(
            ICollection<BenchmarkAssetValidationIssue> issues,
            BenchmarkAssetIssueCode code,
            string assetPath,
            string message)
        {
            if (issues.Any(issue => issue.Code == code))
            {
                return;
            }

            issues.Add(new BenchmarkAssetValidationIssue(code, assetPath, message));
        }
    }
}

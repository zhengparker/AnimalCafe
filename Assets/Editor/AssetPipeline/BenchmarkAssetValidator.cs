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

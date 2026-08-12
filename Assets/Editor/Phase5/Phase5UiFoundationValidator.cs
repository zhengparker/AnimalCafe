using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Foundation;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AnimalCafe.EditorTools.Phase5
{
    /// <summary>
    /// Checks scene-authored Phase 5 UI foundation invariants without mutating the scene.
    /// 只检查、不修改 Phase 5 UI foundation 的场景 authored invariants。
    /// </summary>
    public static class Phase5UiFoundationValidator
    {
        public static Phase5UiFoundationValidationReport Validate(
            Scene scene,
            AnimalCafeUiTheme theme)
        {
            if (!scene.IsValid()) throw new ArgumentException("Scene must be valid.", nameof(scene));

            var issues = new List<Phase5UiFoundationValidationIssue>();
            var scenePath = scene.path ?? string.Empty;
            var roots = scene.GetRootGameObjects();
            var transforms = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var uiRoots = transforms.Where(transform => transform.name == "UI Root").ToArray();
            if (uiRoots.Length == 0)
            {
                AddIssue(Phase5UiFoundationIssueCode.MissingUiRoot, scenePath, "UI Root",
                    "Exactly one UI Root is required.", issues);
            }
            ValidateDuplicates(
                uiRoots,
                Phase5UiFoundationIssueCode.DuplicateUiRoot,
                scenePath,
                issues);
            ValidateCanvasInventory(transforms, scenePath, issues);
            var eventSystems = transforms.Where(transform => transform.GetComponent<EventSystem>() != null).ToArray();
            if (eventSystems.Length == 0)
            {
                AddIssue(Phase5UiFoundationIssueCode.MissingEventSystem, scenePath, "EventSystem",
                    "Exactly one EventSystem is required.", issues);
            }
            ValidateDuplicates(
                eventSystems,
                Phase5UiFoundationIssueCode.DuplicateEventSystem,
                scenePath,
                issues);

            if (uiRoots.Length > 0)
            {
                ValidateLogicalLayerInventory(uiRoots[0], scenePath, issues);
            }

            ValidateTheme(theme, issues);
            ValidateTouchTargets(roots, scenePath, issues);
            ValidateRaycastPolicies(roots, scenePath, issues);
            ValidateStrongFrostOwners(roots, scenePath, issues);
            return new Phase5UiFoundationValidationReport(issues);
        }

        public static Phase5UiFoundationValidationReport ValidateCanonicalAssets(
            IEnumerable<string> canonicalPaths,
            IEnumerable<string> discoveredPaths)
        {
            if (canonicalPaths == null) throw new ArgumentNullException(nameof(canonicalPaths));
            if (discoveredPaths == null) throw new ArgumentNullException(nameof(discoveredPaths));
            var canonical = canonicalPaths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct().ToArray();
            var discovered = discoveredPaths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct().ToArray();
            var issues = new List<Phase5UiFoundationValidationIssue>();
            foreach (var path in canonical.Where(path => !discovered.Contains(path)))
            {
                AddIssue(Phase5UiFoundationIssueCode.MissingCanonicalAsset, path, path,
                    "Canonical Phase 5 asset is missing.", issues);
            }

            var canonicalNames = canonical.Select(System.IO.Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var group in discovered.Where(path => canonicalNames.Contains(System.IO.Path.GetFileName(path)))
                         .GroupBy(System.IO.Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                         .Where(group => group.Count() > 1))
            {
                var approved = canonical.FirstOrDefault(path =>
                    string.Equals(System.IO.Path.GetFileName(path), group.Key, StringComparison.OrdinalIgnoreCase));
                foreach (var duplicate in group.Where(path => path != approved))
                {
                    AddIssue(Phase5UiFoundationIssueCode.DuplicateCanonicalAsset, duplicate, duplicate,
                        "Duplicate or misplaced canonical Phase 5 asset is not allowed.", issues);
                }
            }
            return new Phase5UiFoundationValidationReport(issues);
        }

        public static Phase5UiFoundationValidationReport ValidateCanonicalAssets()
        {
            var discovered = Phase5UiAssetPaths.AllGeneratedAssetPaths
                .SelectMany(path => AssetDatabase.FindAssets(
                        System.IO.Path.GetFileNameWithoutExtension(path))
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(candidate => string.Equals(
                        System.IO.Path.GetFileName(candidate),
                        System.IO.Path.GetFileName(path),
                        StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return ValidateCanonicalAssets(Phase5UiAssetPaths.AllGeneratedAssetPaths, discovered);
        }

        private static void ValidateCanvasInventory(
            IEnumerable<Transform> transforms,
            string scenePath,
            ICollection<Phase5UiFoundationValidationIssue> issues)
        {
            var approved = new[] { "HUD Canvas", "Screen Canvas", "Toast Canvas" };
            var canvases = transforms.Where(transform => transform.GetComponent<Canvas>() != null).ToArray();
            foreach (var name in approved)
            {
                var matches = canvases.Where(canvas => canvas.name == name).ToArray();
                if (matches.Length == 0)
                    AddIssue(Phase5UiFoundationIssueCode.MissingCanvas, scenePath, "UI Root/" + name,
                        "Required Canvas is missing.", issues);
                ValidateDuplicates(matches, Phase5UiFoundationIssueCode.DuplicateCanvas, scenePath, issues);
            }
            foreach (var canvas in canvases.Where(canvas => !approved.Contains(canvas.name)))
                AddIssue(Phase5UiFoundationIssueCode.UnexpectedCanvas, scenePath, HierarchyPath(canvas),
                    "Unexpected Canvas is not allowed.", issues);
        }

        private static void ValidateLogicalLayerInventory(
            Transform root,
            string scenePath,
            ICollection<Phase5UiFoundationValidationIssue> issues)
        {
            var approved = new Dictionary<string, string[]>
            {
                ["HUD Canvas"] = new[] { "HUD Layer" },
                ["Screen Canvas"] = new[] { "Panel Layer", "Modal Layer" },
                ["Toast Canvas"] = new[] { "Toast Layer" }
            };
            foreach (var pair in approved)
            {
                var canvas = root.Find(pair.Key);
                if (canvas == null) continue;
                foreach (var layerName in pair.Value)
                {
                    var matches = canvas.Cast<Transform>().Where(child => child.name == layerName).ToArray();
                    if (matches.Length == 0)
                        AddIssue(Phase5UiFoundationIssueCode.MissingLogicalLayer, scenePath,
                            HierarchyPath(root) + "/" + pair.Key + "/" + layerName,
                            "Required logical UI layer is missing.", issues);
                    ValidateDuplicates(matches, Phase5UiFoundationIssueCode.DuplicateLogicalLayer, scenePath, issues);
                }
                foreach (Transform child in canvas)
                {
                    if (!pair.Value.Contains(child.name))
                        AddIssue(Phase5UiFoundationIssueCode.UnexpectedLogicalLayer, scenePath,
                            HierarchyPath(child), "Unexpected logical UI layer is not allowed.", issues);
                }
            }
        }

        private static void AddIssue(
            Phase5UiFoundationIssueCode code,
            string assetPath,
            string objectPath,
            string message,
            ICollection<Phase5UiFoundationValidationIssue> issues) =>
            issues.Add(new Phase5UiFoundationValidationIssue(code, assetPath, objectPath, message));

        private static void ValidateDuplicates(
            IEnumerable<Transform> candidates,
            Phase5UiFoundationIssueCode code,
            string scenePath,
            ICollection<Phase5UiFoundationValidationIssue> issues)
        {
            foreach (var group in candidates.GroupBy(candidate => candidate.name).Where(group => group.Count() > 1))
            {
                var index = 0;
                foreach (var duplicate in group.Skip(1))
                {
                    issues.Add(new Phase5UiFoundationValidationIssue(
                        code,
                        scenePath,
                        HierarchyPath(duplicate, index + 1),
                        $"Duplicate {group.Key} is not allowed."));
                    index++;
                }
            }
        }

        private static void ValidateRequiredLayer(
            Transform root,
            string relativePath,
            string scenePath,
            ICollection<Phase5UiFoundationValidationIssue> issues)
        {
            if (root.Find(relativePath) != null) return;
            issues.Add(new Phase5UiFoundationValidationIssue(
                Phase5UiFoundationIssueCode.MissingLogicalLayer,
                scenePath,
                HierarchyPath(root) + "/" + relativePath,
                "Required logical UI layer is missing."));
        }

        private static void ValidateTheme(
            AnimalCafeUiTheme theme,
            ICollection<Phase5UiFoundationValidationIssue> issues)
        {
            if (theme == null)
            {
                issues.Add(new Phase5UiFoundationValidationIssue(
                    Phase5UiFoundationIssueCode.MissingThemeToken,
                    Phase5UiAssetPaths.ThemePath,
                    "Theme",
                    "Phase 5 UI theme is missing."));
                return;
            }

            var tokenIssues = new List<string>();
            theme.Validate(tokenIssues);
            foreach (var tokenIssue in tokenIssues)
            {
                var objectPath = TokenObjectPath(tokenIssue);
                issues.Add(new Phase5UiFoundationValidationIssue(
                    Phase5UiFoundationIssueCode.MissingThemeToken,
                    Phase5UiAssetPaths.ThemePath,
                    objectPath,
                    tokenIssue));
            }

            ValidateFont(theme.Typography.Heading.FontAsset, "Typography/Heading", issues);
            ValidateFont(theme.Typography.Body.FontAsset, "Typography/Body", issues);
            ValidateFont(theme.Typography.Label.FontAsset, "Typography/Label", issues);
        }

        private static void ValidateFont(
            TMPro.TMP_FontAsset font,
            string objectPath,
            ICollection<Phase5UiFoundationValidationIssue> issues)
        {
            if (font != null) return;
            issues.Add(new Phase5UiFoundationValidationIssue(
                Phase5UiFoundationIssueCode.MissingFont,
                Phase5UiAssetPaths.ThemePath,
                objectPath,
                "Theme typography font is missing."));
        }

        private static void ValidateTouchTargets(
            IEnumerable<GameObject> roots,
            string scenePath,
            ICollection<Phase5UiFoundationValidationIssue> issues)
        {
            foreach (var button in roots.SelectMany(root => root.GetComponentsInChildren<Button>(true)))
            {
                var size = button.GetComponent<RectTransform>().rect.size;
                if (size.x >= AnimalCafeUiTheme.MinimumTouchTargetSize
                    && size.y >= AnimalCafeUiTheme.MinimumTouchTargetSize) continue;
                issues.Add(new Phase5UiFoundationValidationIssue(
                    Phase5UiFoundationIssueCode.TouchTargetBelowMinimum,
                    scenePath,
                    HierarchyPath(button.transform),
                    "Interactive target must be at least 48x48."));
            }
        }

        private static void ValidateRaycastPolicies(
            IEnumerable<GameObject> roots,
            string scenePath,
            ICollection<Phase5UiFoundationValidationIssue> issues)
        {
            foreach (var toastCanvas in roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true))
                         .Where(canvas => canvas.name == "Toast Canvas"))
            {
                var raycaster = toastCanvas.GetComponent<GraphicRaycaster>();
                if (raycaster == null || !raycaster.enabled) continue;
                issues.Add(new Phase5UiFoundationValidationIssue(
                    Phase5UiFoundationIssueCode.InvalidRaycastPolicy,
                    scenePath,
                    HierarchyPath(toastCanvas.transform),
                    "Toast Canvas must not block scene or UI input."));
            }
        }

        private static void ValidateStrongFrostOwners(
            IEnumerable<GameObject> roots,
            string scenePath,
            ICollection<Phase5UiFoundationValidationIssue> issues)
        {
            var strongOwners = roots.SelectMany(root => root.GetComponentsInChildren<AnimalCafePanelView>(true))
                .Where(panel => panel.isActiveAndEnabled && panel.ResolvedStyle == UiPanelStyle.StrongFrost)
                .ToArray();
            if (strongOwners.Length <= 1) return;
            foreach (var owner in strongOwners)
            {
                issues.Add(new Phase5UiFoundationValidationIssue(
                    Phase5UiFoundationIssueCode.MultipleStrongFrostOwners,
                    scenePath,
                    HierarchyPath(owner.transform),
                    "At most one resolved Strong Frost owner is allowed."));
            }
        }

        private static string TokenObjectPath(string tokenIssue)
        {
            var separator = tokenIssue.IndexOf(": ", StringComparison.Ordinal);
            if (separator < 0) return "Theme";

            var segments = tokenIssue[(separator + 2)..].Trim().Split('/').Skip(1).ToArray();
            if (segments.Length == 1 && segments[0] is "Heading" or "Body" or "Label")
                return "Typography/" + segments[0];
            return segments.Length > 0 ? string.Join("/", segments) : "Theme";
        }

        private static string HierarchyPath(Transform transform, int duplicateIndex = -1)
        {
            var segments = new Stack<string>();
            for (var current = transform; current != null; current = current.parent)
                segments.Push(current.name);
            var path = string.Join("/", segments);
            return duplicateIndex >= 0 ? path + "[" + duplicateIndex + "]" : path;
        }
    }
}

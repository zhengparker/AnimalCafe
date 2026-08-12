using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.UI.Components;
using AnimalCafe.UI.Foundation;
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
            ValidateDuplicates(
                roots.Where(root => root.name == "UI Root").Select(root => root.transform),
                Phase5UiFoundationIssueCode.DuplicateUiRoot,
                scenePath,
                issues);
            ValidateDuplicates(
                roots.SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).Select(canvas => canvas.transform)
                    .Where(canvas => canvas.name is "HUD Canvas" or "Screen Canvas" or "Toast Canvas"),
                Phase5UiFoundationIssueCode.DuplicateCanvas,
                scenePath,
                issues);
            ValidateDuplicates(
                roots.SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).Select(system => system.transform),
                Phase5UiFoundationIssueCode.DuplicateEventSystem,
                scenePath,
                issues);

            var primaryRoot = roots.FirstOrDefault(root => root.name == "UI Root");
            if (primaryRoot != null)
            {
                ValidateRequiredLayer(primaryRoot.transform, "HUD Canvas/HUD Layer", scenePath, issues);
                ValidateRequiredLayer(primaryRoot.transform, "Screen Canvas/Panel Layer", scenePath, issues);
                ValidateRequiredLayer(primaryRoot.transform, "Screen Canvas/Modal Layer", scenePath, issues);
                ValidateRequiredLayer(primaryRoot.transform, "Toast Canvas/Toast Layer", scenePath, issues);
            }

            ValidateTheme(theme, issues);
            ValidateTouchTargets(roots, scenePath, issues);
            ValidateRaycastPolicies(roots, scenePath, issues);
            ValidateStrongFrostOwners(roots, scenePath, issues);
            return new Phase5UiFoundationValidationReport(issues);
        }

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
                root.name + "/" + relativePath,
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
            if (tokenIssue.Contains("HEADING")) return "Typography/Heading";
            if (tokenIssue.Contains("BODY")) return "Typography/Body";
            if (tokenIssue.Contains("LABEL")) return "Typography/Label";
            return "Theme";
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

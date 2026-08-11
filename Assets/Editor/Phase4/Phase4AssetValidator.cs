using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Content;
using UnityEditor;
using UnityEngine;

namespace AnimalCafe.EditorTools.Phase4
{
    public static class Phase4AssetValidator
    {
        public static Phase4AssetValidationReport ValidateAll()
        {
            var definitions = AssetDatabase
                .FindAssets("t:FurnitureDefinitionAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<FurnitureDefinitionAsset>)
                .Where(definition => definition != null)
                .ToArray();
            var wallDefinitions = AssetDatabase
                .FindAssets("t:WallMountedDefinitionAsset")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<WallMountedDefinitionAsset>)
                .Where(definition => definition != null)
                .ToArray();

            var prefabPaths = AssetDatabase
                .FindAssets("t:Prefab")
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();
            var prefabPathSet = new HashSet<string>(prefabPaths, StringComparer.Ordinal);
            var nestedPrefabPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prefabPath in prefabPaths)
            {
                foreach (var dependencyPath in AssetDatabase.GetDependencies(
                    prefabPath,
                    true))
                {
                    if (!string.Equals(prefabPath, dependencyPath, StringComparison.Ordinal) &&
                        prefabPathSet.Contains(dependencyPath))
                    {
                        nestedPrefabPaths.Add(dependencyPath);
                    }
                }
            }

            var prefabRoots = prefabPaths
                .Where(prefabPath => !nestedPrefabPaths.Contains(prefabPath))
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .Where(root => root != null)
                .ToArray();
            var wallSurfaces = prefabRoots
                .SelectMany(root => root.GetComponentsInChildren<WallSurfaceAuthoring>(true))
                .ToArray();
            var entrances = prefabRoots
                .SelectMany(root => root.GetComponentsInChildren<EntrancePortalAuthoring>(true))
                .ToArray();

            return CombineReports(
                ValidateAll(definitions),
                ValidateWallContent(
                    wallSurfaces,
                    wallDefinitions,
                    Array.Empty<AnimalCafe.Layout.WallMountedInstance>()),
                ValidateEntrances(
                    entrances,
                    new AnimalCafe.Layout.GridSize(8, 8)));
        }

        public static Phase4AssetValidationReport ValidateAll(
            IEnumerable<FurnitureDefinitionAsset> definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            var definitionArray = definitions.ToArray();
            var duplicateIds = new HashSet<string>(
                definitionArray
                    .Where(definition => definition != null)
                    .GroupBy(definition => definition.DefinitionId, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key),
                StringComparer.Ordinal);

            var validAssetCount = 0;
            var invalidAssetCount = 0;
            var issues = new List<Phase4AssetValidationIssue>();
            foreach (var definition in definitionArray)
            {
                var definitionReport = ValidateFurnitureDefinition(definition);
                issues.AddRange(definitionReport.Issues);

                var hasDuplicateId = definition != null &&
                    duplicateIds.Contains(definition.DefinitionId);
                if (hasDuplicateId)
                {
                    AddIssue(
                        issues,
                        Phase4AssetIssueCode.DuplicateDefinitionId,
                        GetDefinitionAssetPath(definition),
                        $"Furniture Definition ID '{definition.DefinitionId}' is duplicated.");
                }

                if (definitionReport.InvalidAssetCount > 0 || hasDuplicateId)
                {
                    invalidAssetCount++;
                }
                else
                {
                    validAssetCount++;
                }
            }

            return new Phase4AssetValidationReport(
                validAssetCount,
                invalidAssetCount,
                issues);
        }

        public static Phase4AssetValidationReport ValidateFurnitureDefinition(
            FurnitureDefinitionAsset definition)
        {
            var issues = new List<Phase4AssetValidationIssue>();
            if (definition == null)
            {
                AddIssue(
                    issues,
                    Phase4AssetIssueCode.MissingReference,
                    "<null FurnitureDefinitionAsset>",
                    "Furniture Definition reference is missing.");
                return CreateSingleAssetReport(issues);
            }

            var assetPath = GetDefinitionAssetPath(definition);
            try
            {
                definition.ToRuntimeDefinition();
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException ||
                exception is OverflowException)
            {
                AddIssue(
                    issues,
                    Phase4AssetIssueCode.InvalidDefinition,
                    assetPath,
                    $"Furniture Definition is invalid: {exception.Message}");
            }

            if (definition.Prefab == null)
            {
                AddIssue(
                    issues,
                    Phase4AssetIssueCode.MissingPrefab,
                    assetPath,
                    "Furniture Definition must reference a Prefab.");
            }
            else
            {
                ValidateCounterSurfaceSlots(definition, assetPath, issues);
                ValidateWorkTableSurfaceSlot(definition, assetPath, issues);
                ValidateCoffeeMachineForward(definition, assetPath, issues);
                ValidateCashRegisterSides(definition, assetPath, issues);
                ValidateTechnicalAsset(
                    definition.Prefab,
                    assetPath,
                    definition.DefinitionId,
                    issues);
            }

            return CreateSingleAssetReport(issues);
        }

        private static void ValidateTechnicalAsset(
            GameObject root,
            string definitionAssetPath,
            string definitionId,
            ICollection<Phase4AssetValidationIssue> issues)
        {
            var prefabAssetPath = AssetDatabase.GetAssetPath(root);
            if (string.IsNullOrEmpty(prefabAssetPath))
            {
                prefabAssetPath = $"<unsaved-prefab:{root.name}>";
            }

            var technicalIssues = new List<Phase4AssetValidationIssue>();
            CollectTechnicalAssetIssues(root, prefabAssetPath, technicalIssues);
            foreach (var issue in technicalIssues)
            {
                AddIssueOnce(
                    issues,
                    issue.Code,
                    issue.AssetPath,
                    $"Definition '{definitionId}' ({definitionAssetPath}): {issue.Message}");
            }
        }

        private static void CollectTechnicalAssetIssues(
            GameObject root,
            string assetPath,
            ICollection<Phase4AssetValidationIssue> issues)
        {
            if (root.transform.localPosition != Vector3.zero)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.TechnicalAssetContract,
                    assetPath,
                    $"Prefab root localPosition must be (0, 0, 0); actual value is {root.transform.localPosition}.");
            }

            if (Quaternion.Angle(root.transform.localRotation, Quaternion.identity) > 0.01f)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.TechnicalAssetContract,
                    assetPath,
                    $"Prefab root localRotation must be identity; actual Euler angles are {root.transform.localEulerAngles}.");
            }

            if (root.transform.localScale != Vector3.one)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.TechnicalAssetContract,
                    assetPath,
                    $"Prefab root localScale must be (1, 1, 1); actual value is {root.transform.localScale}.");
            }

            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var camera in root.GetComponentsInChildren<UnityEngine.Camera>(true))
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.TechnicalAssetContract,
                    assetPath,
                    $"GameObject '{GetHierarchyIdentity(root, camera.transform)}' contains a Camera component, including inactive descendants.");
            }

            foreach (var light in root.GetComponentsInChildren<Light>(true))
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.TechnicalAssetContract,
                    assetPath,
                    $"GameObject '{GetHierarchyIdentity(root, light.transform)}' contains a Light component, including inactive descendants.");
            }

            foreach (var transform in allTransforms.Where(transform => string.Equals(
                transform.name,
                "Cube",
                StringComparison.Ordinal)))
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.TechnicalAssetContract,
                    assetPath,
                    $"Raw export GameObject '{GetHierarchyIdentity(root, transform)}' must be renamed or removed.");
            }

            foreach (var transform in allTransforms)
            {
                var missingScriptCount = GameObjectUtility
                    .GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                if (missingScriptCount > 0)
                {
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.MissingReference,
                        assetPath,
                        $"GameObject '{GetHierarchyIdentity(root, transform)}' has {missingScriptCount} missing script reference(s).");
                }
            }

            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                .ToArray();
            if (renderers.Length == 0)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.MissingReference,
                    assetPath,
                    "Production Prefab must contain a visible Model renderer.");
            }

            var meshes = new HashSet<Mesh>();
            foreach (var renderer in renderers)
            {
                var mesh = GetRendererMesh(renderer);
                if (mesh == null)
                {
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.MissingReference,
                        assetPath,
                        $"Renderer '{GetHierarchyIdentity(root, renderer.transform)}' must reference a Mesh.");
                }
                else
                {
                    meshes.Add(mesh);
                }

                var materials = renderer.sharedMaterials;
                if (materials.Length == 0)
                {
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.MissingReference,
                        assetPath,
                        $"Renderer '{GetHierarchyIdentity(root, renderer.transform)}' has no Material slots.");
                }

                for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    var material = materials[materialIndex];
                    if (material == null)
                    {
                        AddIssueOnce(
                            issues,
                            Phase4AssetIssueCode.MissingReference,
                            assetPath,
                            $"Renderer '{GetHierarchyIdentity(root, renderer.transform)}' Material slot {materialIndex} is missing its Material reference.");
                        continue;
                    }

                    var shader = material.shader;
                    if (shader == null)
                    {
                        AddIssueOnce(
                            issues,
                            Phase4AssetIssueCode.MissingReference,
                            assetPath,
                            $"Renderer '{GetHierarchyIdentity(root, renderer.transform)}' Material slot {materialIndex} ('{material.name}') is missing its Shader reference.");
                        continue;
                    }

                    if (!string.Equals(
                        shader.name,
                        "Universal Render Pipeline/Lit",
                        StringComparison.Ordinal))
                    {
                        AddIssueOnce(
                            issues,
                            Phase4AssetIssueCode.TechnicalAssetContract,
                            assetPath,
                            $"Material '{material.name}' must use shader 'Universal Render Pipeline/Lit'; actual shader is '{shader.name}'.");
                    }

                    if (!material.HasProperty("_Surface"))
                    {
                        AddIssueOnce(
                            issues,
                            Phase4AssetIssueCode.TechnicalAssetContract,
                            assetPath,
                            $"Material '{material.name}' shader is missing required field '_Surface'.");
                    }
                    else if (material.GetFloat("_Surface") != 0f)
                    {
                        AddIssueOnce(
                            issues,
                            Phase4AssetIssueCode.TechnicalAssetContract,
                            assetPath,
                            $"Material '{material.name}' field '_Surface' must be 0 (Opaque); actual value is {material.GetFloat("_Surface")}.");
                    }

                    var baseMap = material.HasProperty("_BaseMap")
                        ? material.GetTexture("_BaseMap")
                        : null;
                    if (baseMap == null)
                    {
                        AddIssueOnce(
                            issues,
                            Phase4AssetIssueCode.MissingReference,
                            assetPath,
                            $"Renderer '{GetHierarchyIdentity(root, renderer.transform)}' Material slot {materialIndex} ('{material.name}') field '_BaseMap' is missing its Texture reference.");
                    }
                    else if (baseMap.width > 1024 || baseMap.height > 1024)
                    {
                        AddIssueOnce(
                            issues,
                            Phase4AssetIssueCode.TechnicalAssetContract,
                            assetPath,
                            $"Texture '{baseMap.name}' is {baseMap.width} x {baseMap.height}; maximum Phase 4 size is 1024 x 1024.");
                    }
                }
            }

            long triangleCount = 0;
            foreach (var mesh in meshes)
            {
                for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    // GetIndexCount works for imported Meshes with Read/Write disabled.
                    triangleCount += (long)mesh.GetIndexCount(subMesh) / 3;
                }
            }

            if (triangleCount > 6000)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.TechnicalAssetContract,
                    assetPath,
                    $"Visible Model triangle count is {triangleCount}; maximum Phase 4 count is 6000.");
            }

            var colliders = root.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.TechnicalAssetContract,
                    assetPath,
                    "Production Prefab must contain at least one Collider.");
            }

            var hasVisibleBounds = TryGetRootLocalRendererBounds(root, renderers, out var visibleBounds);
            foreach (var collider in colliders)
            {
                if (!(collider is BoxCollider) &&
                    !(collider is SphereCollider) &&
                    !(collider is CapsuleCollider))
                {
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.TechnicalAssetContract,
                        assetPath,
                        $"Collider '{GetHierarchyIdentity(root, collider.transform)}' uses unsupported type '{collider.GetType().Name}'; use BoxCollider, SphereCollider, or CapsuleCollider.");
                }

                if (collider.isTrigger)
                {
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.TechnicalAssetContract,
                        assetPath,
                        $"Collider '{GetHierarchyIdentity(root, collider.transform)}' field 'isTrigger' must be false; actual value is true.");
                }

                if (hasVisibleBounds &&
                    TryGetRootLocalColliderBounds(root, collider, out var colliderBounds))
                {
                    if (BoundsExtendBeyond(colliderBounds, visibleBounds, 0.05f))
                    {
                        AddIssueOnce(
                            issues,
                            Phase4AssetIssueCode.TechnicalAssetContract,
                            assetPath,
                            $"Collider '{GetHierarchyIdentity(root, collider.transform)}' bounds {colliderBounds} extend beyond visible Model bounds {visibleBounds} by more than 0.05m.");
                    }
                }
            }

            if (hasVisibleBounds)
            {
                if (Math.Abs(visibleBounds.min.y) > 0.05f ||
                    Math.Abs(visibleBounds.center.x) > 0.05f ||
                    Math.Abs(visibleBounds.center.z) > 0.05f)
                {
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.TechnicalAssetContract,
                        assetPath,
                        $"Visible Model pivot/bounds must be bottom-center within 0.05m; actual min.y={visibleBounds.min.y}, center.x={visibleBounds.center.x}, center.z={visibleBounds.center.z}.");
                }
            }
        }

        private static Mesh GetRendererMesh(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                return skinnedMeshRenderer.sharedMesh;
            }

            var meshFilter = renderer.GetComponent<MeshFilter>();
            return meshFilter == null ? null : meshFilter.sharedMesh;
        }

        private static string GetHierarchyIdentity(GameObject root, Transform target)
        {
            var segments = new Stack<string>();
            var current = target;
            while (current != null && current != root.transform)
            {
                segments.Push($"{current.name}[{current.GetSiblingIndex()}]");
                current = current.parent;
            }

            var relativePath = segments.Count == 0
                ? string.Empty
                : "/" + string.Join("/", segments);
            return $"{root.name}[root]{relativePath}";
        }

        private static bool TryGetRootLocalRendererBounds(
            GameObject root,
            IEnumerable<Renderer> renderers,
            out Bounds combinedBounds)
        {
            var hasBounds = false;
            combinedBounds = default;
            foreach (var renderer in renderers)
            {
                foreach (var localCorner in GetBoundsCorners(renderer.localBounds))
                {
                    var worldCorner = renderer.localToWorldMatrix.MultiplyPoint3x4(localCorner);
                    var rootLocalCorner = root.transform.InverseTransformPoint(worldCorner);
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

        private static bool TryGetRootLocalColliderBounds(
            GameObject root,
            Collider collider,
            out Bounds rootLocalBounds)
        {
            var hasBounds = false;
            rootLocalBounds = default;
            foreach (var worldCorner in GetBoundsCorners(collider.bounds))
            {
                var rootLocalCorner = root.transform.InverseTransformPoint(worldCorner);
                if (hasBounds)
                {
                    rootLocalBounds.Encapsulate(rootLocalCorner);
                }
                else
                {
                    rootLocalBounds = new Bounds(rootLocalCorner, Vector3.zero);
                    hasBounds = true;
                }
            }

            return hasBounds;
        }

        private static bool BoundsExtendBeyond(
            Bounds candidate,
            Bounds allowed,
            float tolerance)
        {
            return candidate.min.x < allowed.min.x - tolerance ||
                candidate.max.x > allowed.max.x + tolerance ||
                candidate.min.y < allowed.min.y - tolerance ||
                candidate.max.y > allowed.max.y + tolerance ||
                candidate.min.z < allowed.min.z - tolerance ||
                candidate.max.z > allowed.max.z + tolerance;
        }

        public static Phase4AssetValidationReport ValidateWallContent(
            IEnumerable<WallSurfaceAuthoring> surfaces,
            IEnumerable<AnimalCafe.Layout.WallMountedInstance> mountedItems)
        {
            return ValidateWallContentCore(
                surfaces,
                Array.Empty<WallMountedDefinitionAsset>(),
                mountedItems,
                false);
        }

        public static Phase4AssetValidationReport ValidateWallContent(
            IEnumerable<WallSurfaceAuthoring> surfaces,
            IEnumerable<WallMountedDefinitionAsset> definitions,
            IEnumerable<AnimalCafe.Layout.WallMountedInstance> mountedItems)
        {
            return ValidateWallContentCore(surfaces, definitions, mountedItems, true);
        }

        private static Phase4AssetValidationReport ValidateWallContentCore(
            IEnumerable<WallSurfaceAuthoring> surfaces,
            IEnumerable<WallMountedDefinitionAsset> definitions,
            IEnumerable<AnimalCafe.Layout.WallMountedInstance> mountedItems,
            bool validateDefinitionReferences)
        {
            if (surfaces == null)
            {
                throw new ArgumentNullException(nameof(surfaces));
            }

            if (mountedItems == null)
            {
                throw new ArgumentNullException(nameof(mountedItems));
            }

            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            var surfaceArray = surfaces.ToArray();
            var definitionArray = definitions.ToArray();
            var mountedItemArray = mountedItems.ToArray();
            var issues = new List<Phase4AssetValidationIssue>();
            var validDefinitions = new Dictionary<string, WallMountedDefinitionAsset>(
                StringComparer.Ordinal);
            var duplicateDefinitionIds = new HashSet<string>(
                definitionArray
                    .Where(definition => definition != null)
                    .GroupBy(definition => definition.DefinitionId, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key),
                StringComparer.Ordinal);
            var invalidDefinitionCount = 0;
            foreach (var definition in definitionArray)
            {
                if (definition == null)
                {
                    invalidDefinitionCount++;
                    AddIssue(
                        issues,
                        Phase4AssetIssueCode.MissingReference,
                        "<null WallMountedDefinitionAsset>",
                        "Wall Mounted Definition reference is missing.");
                    continue;
                }

                var definitionPath = GetWallDefinitionAssetPath(definition);
                var isValidDefinition = true;
                AnimalCafe.Layout.WallFootprint footprint = default;
                try
                {
                    footprint = definition.Footprint;
                    new AnimalCafe.Layout.WallMountedInstance(
                        "validation.instance",
                        definition.DefinitionId,
                        "validation.surface",
                        new AnimalCafe.Layout.WallSlotPosition(0, 0),
                        footprint);
                }
                catch (Exception exception) when (
                    exception is ArgumentException ||
                    exception is ArgumentOutOfRangeException ||
                    exception is OverflowException)
                {
                    isValidDefinition = false;
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.InvalidDefinition,
                        definitionPath,
                        $"Wall Mounted Definition ID or footprint is invalid: {exception.Message}");
                }

                if (string.IsNullOrWhiteSpace(definition.DisplayName))
                {
                    isValidDefinition = false;
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.InvalidDefinition,
                        definitionPath,
                        "Wall Mounted Definition DisplayName must not be empty.");
                }

                if (definition.Prefab == null)
                {
                    isValidDefinition = false;
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.MissingPrefab,
                        definitionPath,
                        "Wall Mounted Definition must reference a Prefab.");
                }
                else
                {
                    var technicalIssues = new List<Phase4AssetValidationIssue>();
                    ValidateTechnicalAsset(
                        definition.Prefab,
                        definitionPath,
                        definition.DefinitionId,
                        technicalIssues);
                    if (technicalIssues.Count > 0)
                    {
                        isValidDefinition = false;
                        foreach (var technicalIssue in technicalIssues)
                        {
                            AddIssueOnce(
                                issues,
                                technicalIssue.Code,
                                technicalIssue.AssetPath,
                                technicalIssue.Message);
                        }
                    }
                }

                if (duplicateDefinitionIds.Contains(definition.DefinitionId))
                {
                    isValidDefinition = false;
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.DuplicateDefinitionId,
                        definitionPath,
                        $"Wall Mounted Definition ID '{definition.DefinitionId}' is duplicated.");
                }

                if (isValidDefinition)
                {
                    validDefinitions.Add(definition.DefinitionId, definition);
                }
                else
                {
                    invalidDefinitionCount++;
                }
            }

            var invalidSurfaces = new HashSet<WallSurfaceAuthoring>();
            var duplicateIds = new HashSet<string>(
                surfaceArray
                    .Where(surface => surface != null)
                    .GroupBy(surface => surface.SurfaceId, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key),
                StringComparer.Ordinal);
            var layouts = new Dictionary<string, AnimalCafe.Layout.WallSurfaceLayout>(
                StringComparer.Ordinal);

            foreach (var surface in surfaceArray)
            {
                if (surface == null)
                {
                    AddIssue(
                        issues,
                        Phase4AssetIssueCode.MissingReference,
                        "<null WallSurfaceAuthoring>",
                        "Wall Surface reference is missing.");
                    continue;
                }

                var assetPath = GetComponentAssetPath(surface, surface.SurfaceId);
                var isValid = surface.Columns == 8 &&
                    surface.Rows == 2 &&
                    Math.Abs(surface.SlotSize - 1f) <= 0.0001f &&
                    !duplicateIds.Contains(surface.SurfaceId);
                try
                {
                    new AnimalCafe.Layout.WallSurfaceLayout(
                        surface.SurfaceId,
                        surface.Columns,
                        surface.Rows);
                }
                catch (ArgumentException)
                {
                    isValid = false;
                }

                if (!isValid)
                {
                    invalidSurfaces.Add(surface);
                    AddIssue(
                        issues,
                        Phase4AssetIssueCode.InvalidWallSurface,
                        assetPath,
                        "Production Wall Surface requires a unique stable ID and exact 8 x 2 x 1m slot dimensions.");
                    continue;
                }

                layouts.Add(
                    surface.SurfaceId,
                    new AnimalCafe.Layout.WallSurfaceLayout(
                        surface.SurfaceId,
                        surface.Columns,
                        surface.Rows));
            }

            var invalidPlacementCount = 0;
            foreach (var item in mountedItemArray)
            {
                var isValidPlacement = item != null;
                if (item == null)
                {
                    AddIssue(
                        issues,
                        Phase4AssetIssueCode.InvalidWallPlacement,
                        "<null WallMountedInstance>",
                        "Wall Mounted Instance reference is missing.");
                }

                if (item != null && validateDefinitionReferences)
                {
                    if (!validDefinitions.TryGetValue(item.DefinitionId, out var definition))
                    {
                        isValidPlacement = false;
                        AddIssueOnce(
                            issues,
                            Phase4AssetIssueCode.InvalidWallPlacement,
                            $"<wall-placement:{item.InstanceId}>",
                            $"Wall placement '{item.InstanceId}' references missing or invalid definition '{item.DefinitionId}'.");
                    }
                    else
                    {
                        var definitionFootprint = definition.Footprint;
                        if (item.Footprint.Width != definitionFootprint.Width ||
                            item.Footprint.Height != definitionFootprint.Height)
                        {
                            isValidPlacement = false;
                            AddIssueOnce(
                                issues,
                                Phase4AssetIssueCode.InvalidWallPlacement,
                                $"<wall-placement:{item.InstanceId}>",
                                $"Wall placement '{item.InstanceId}' footprint {item.Footprint.Width} x {item.Footprint.Height} does not match definition '{item.DefinitionId}' footprint {definitionFootprint.Width} x {definitionFootprint.Height}.");
                        }
                    }
                }

                if (item != null && isValidPlacement)
                {
                    if (!layouts.TryGetValue(item.SurfaceId, out var layout))
                    {
                        isValidPlacement = false;
                        AddIssueOnce(
                            issues,
                            Phase4AssetIssueCode.InvalidWallPlacement,
                            $"<wall-placement:{item.InstanceId}>",
                            $"Wall placement '{item.InstanceId}' references unknown or invalid surface '{item.SurfaceId}'.");
                    }
                    else
                    {
                        var placementResult = layout.TryPlace(item);
                        if (!placementResult.Succeeded)
                        {
                            isValidPlacement = false;
                            AddIssueOnce(
                                issues,
                                Phase4AssetIssueCode.InvalidWallPlacement,
                                $"<wall-placement:{item.InstanceId}>",
                                $"Wall placement '{item.InstanceId}' failed with {placementResult.FailureReason} on surface '{item.SurfaceId}'.");
                        }
                    }
                }

                if (!isValidPlacement)
                {
                    invalidPlacementCount++;
                }
            }

            var nullCount = surfaceArray.Count(surface => surface == null);
            var invalidSurfaceCount = invalidSurfaces.Count + nullCount;
            return new Phase4AssetValidationReport(
                surfaceArray.Length - invalidSurfaceCount +
                    definitionArray.Length - invalidDefinitionCount +
                    mountedItemArray.Length - invalidPlacementCount,
                invalidSurfaceCount + invalidDefinitionCount + invalidPlacementCount,
                issues);
        }

        public static Phase4AssetValidationReport ValidateEntrances(
            IEnumerable<EntrancePortalAuthoring> entrances,
            AnimalCafe.Layout.GridSize layoutSize)
        {
            if (entrances == null)
            {
                throw new ArgumentNullException(nameof(entrances));
            }

            var entranceArray = entrances.ToArray();
            var issues = new List<Phase4AssetValidationIssue>();
            var duplicateIds = new HashSet<string>(
                entranceArray
                    .Where(entrance => entrance != null)
                    .GroupBy(entrance => entrance.EntranceId, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key),
                StringComparer.Ordinal);
            var invalidCount = 0;

            foreach (var entrance in entranceArray)
            {
                if (entrance == null)
                {
                    invalidCount++;
                    AddIssue(
                        issues,
                        Phase4AssetIssueCode.MissingReference,
                        "<null EntrancePortalAuthoring>",
                        "Entrance Portal reference is missing.");
                    continue;
                }

                var isValid = !duplicateIds.Contains(entrance.EntranceId);
                try
                {
                    var reservation = entrance.CreateReservation();
                    var origin = reservation.Origin;
                    isValid &= reservation.Size == new AnimalCafe.Layout.GridSize(2, 2) &&
                        reservation.Type == AnimalCafe.Layout.LayoutReservationType.EntranceClearance &&
                        origin.X >= 0 &&
                        origin.Y >= 0 &&
                        (long)origin.X + reservation.Size.Width <= layoutSize.Width &&
                        (long)origin.Y + reservation.Size.Height <= layoutSize.Height;
                }
                catch (ArgumentException)
                {
                    isValid = false;
                }

                var blockingCollider = entrance
                    .GetComponentsInChildren<Collider>(true)
                    .Any(collider => ColliderOverlapsEntranceClearance(
                        entrance.transform,
                        collider));
                isValid &= !blockingCollider;

                if (!isValid)
                {
                    invalidCount++;
                    AddIssue(
                        issues,
                        Phase4AssetIssueCode.InvalidEntrance,
                        GetComponentAssetPath(entrance, entrance.EntranceId),
                        "Entrance requires a unique stable ID, an in-bounds 2 x 2 clearance, and no Collider blocking that clearance.");
                }
            }

            return new Phase4AssetValidationReport(
                entranceArray.Length - invalidCount,
                invalidCount,
                issues);
        }

        private static void ValidateCashRegisterSides(
            FurnitureDefinitionAsset definition,
            string assetPath,
            ICollection<Phase4AssetValidationIssue> issues)
        {
            if (!IsFamily(definition, "equipment.cash-register.") &&
                definition.FunctionType != AnimalCafe.Layout.FurnitureFunctionType.CashRegister)
            {
                return;
            }

            if (definition.AllowedPlacementSurfaces !=
                AnimalCafe.Layout.PlacementSurfaceType.FurnitureSurface)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.InvalidCashRegisterSides,
                    assetPath,
                    $"Cash Register AllowedPlacementSurfaces must be FurnitureSurface; actual value is {definition.AllowedPlacementSurfaces}.");
            }

            if (definition.FunctionType != AnimalCafe.Layout.FurnitureFunctionType.CashRegister)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.InvalidCashRegisterSides,
                    assetPath,
                    $"Cash Register FunctionType must be CashRegister; actual value is {definition.FunctionType}.");
            }

            ValidateSingleSlotFit(
                definition,
                assetPath,
                Phase4AssetIssueCode.InvalidCashRegisterSides,
                "Cash Register",
                issues);
            try
            {
                CashRegisterSideMarker.ReadSidesFrom(definition.Prefab);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is ArgumentOutOfRangeException)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.InvalidCashRegisterSides,
                    assetPath,
                    $"Cash Register side markers are invalid: {exception.Message}");
            }
        }

        private static void ValidateCoffeeMachineForward(
            FurnitureDefinitionAsset definition,
            string assetPath,
            ICollection<Phase4AssetValidationIssue> issues)
        {
            if (!IsFamily(definition, "equipment.coffee-machine.") &&
                definition.FunctionType != AnimalCafe.Layout.FurnitureFunctionType.CoffeeMachine)
            {
                return;
            }

            var root = definition.Prefab;
            var markers = root.GetComponentsInChildren<Transform>(true)
                .Where(transform => string.Equals(
                    transform.name,
                    "ForwardMarker",
                    StringComparison.Ordinal))
                .ToArray();
            if (markers.Length != 1)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.InvalidCoffeeMachineForward,
                    assetPath,
                    $"Coffee Machine requires exactly one ForwardMarker, including inactive descendants; actual count is {markers.Length}.");
            }
            else
            {
                var marker = markers.Single();
                var localPosition = root.transform.InverseTransformPoint(marker.position);
                if (localPosition.z <= 0f || Math.Abs(localPosition.x) > 0.01f)
                {
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.InvalidCoffeeMachineForward,
                        assetPath,
                        $"ForwardMarker must be centered on positive local +Z; actual localPosition is {localPosition}.");
                }

                var forwardDot = Vector3.Dot(marker.forward, root.transform.forward);
                if (forwardDot < 0.999f)
                {
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.InvalidCoffeeMachineForward,
                        assetPath,
                        $"ForwardMarker must face prefab local +Z; actual forward dot is {forwardDot}.");
                }

                var markerRendererCount = marker.GetComponentsInChildren<Renderer>(true).Length;
                var markerMeshFilterCount = marker.GetComponentsInChildren<MeshFilter>(true).Length;
                var markerColliderCount = marker.GetComponentsInChildren<Collider>(true).Length;
                if (markerRendererCount > 0 || markerMeshFilterCount > 0 || markerColliderCount > 0)
                {
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.InvalidCoffeeMachineForward,
                        assetPath,
                        $"ForwardMarker subtree must be invisible and non-colliding; actual Renderer={markerRendererCount}, MeshFilter={markerMeshFilterCount}, Collider={markerColliderCount}.");
                }
            }

            if (definition.AllowedPlacementSurfaces !=
                AnimalCafe.Layout.PlacementSurfaceType.FurnitureSurface)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.InvalidCoffeeMachineForward,
                    assetPath,
                    $"Coffee Machine AllowedPlacementSurfaces must be FurnitureSurface; actual value is {definition.AllowedPlacementSurfaces}.");
            }

            if (definition.FunctionType != AnimalCafe.Layout.FurnitureFunctionType.CoffeeMachine)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.InvalidCoffeeMachineForward,
                    assetPath,
                    $"Coffee Machine FunctionType must be CoffeeMachine; actual value is {definition.FunctionType}.");
            }

            var cashMarkerCount = root.GetComponentsInChildren<CashRegisterSideMarker>(true).Length;
            if (cashMarkerCount > 0)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.InvalidCoffeeMachineForward,
                    assetPath,
                    $"Coffee Machine must not contain CashRegisterSideMarker components; actual count is {cashMarkerCount}.");
            }

            ValidateSingleSlotFit(
                definition,
                assetPath,
                Phase4AssetIssueCode.InvalidCoffeeMachineForward,
                "Coffee Machine",
                issues);
        }

        private static void ValidateCounterSurfaceSlots(
            FurnitureDefinitionAsset definition,
            string assetPath,
            ICollection<Phase4AssetValidationIssue> issues)
        {
            if (string.IsNullOrEmpty(definition.DefinitionId) ||
                !definition.DefinitionId.StartsWith(
                    "furniture.counter.",
                    StringComparison.Ordinal))
            {
                return;
            }

            var slots = definition.Prefab
                .GetComponentsInChildren<SurfaceSlotMarker>(true);
            var expectedCount = (long)definition.FootprintWidth * definition.FootprintDepth;
            var slotIds = new HashSet<string>(StringComparer.Ordinal);
            if (definition.AllowedPlacementSurfaces != AnimalCafe.Layout.PlacementSurfaceType.Floor)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.InvalidSurfaceSlot,
                    assetPath,
                    $"Counter AllowedPlacementSurfaces must be Floor; actual value is {definition.AllowedPlacementSurfaces}.");
            }

            if (definition.FunctionType != AnimalCafe.Layout.FurnitureFunctionType.None)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.InvalidSurfaceSlot,
                    assetPath,
                    $"Counter FunctionType must be None; actual value is {definition.FunctionType}.");
            }

            if (slots.LongLength != expectedCount)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.InvalidSurfaceSlot,
                    assetPath,
                    $"Counter requires one SurfaceSlotMarker per footprint cell; expected {expectedCount}, actual {slots.LongLength}.");
            }

            var localPositions = new List<Vector3>();
            foreach (var slot in slots)
            {
                var localPosition = definition.Prefab.transform
                    .InverseTransformPoint(slot.transform.position);
                localPositions.Add(localPosition);
                var slotFitsFootprint =
                    Math.Abs(localPosition.x) <= definition.FootprintWidth * 0.5f - 0.5f + 0.0001f &&
                    Math.Abs(localPosition.z) <= definition.FootprintDepth * 0.5f - 0.5f + 0.0001f &&
                    localPosition.y > 0f;
                var hasStableUniqueId = !string.IsNullOrWhiteSpace(slot.SlotId) &&
                    slotIds.Add(slot.SlotId);
                var hasDefaultLocalRotation = Quaternion.Angle(
                    definition.Prefab.transform.rotation,
                    slot.transform.rotation) <= 0.01f;

                if (!slotFitsFootprint)
                {
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.InvalidSurfaceSlot,
                        assetPath,
                        $"Counter Surface Slot '{slot.SlotId}' at {localPosition} must fit a full 1m x 1m slot inside footprint {definition.FootprintWidth} x {definition.FootprintDepth} and remain above the base.");
                }

                if (!hasStableUniqueId)
                {
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.InvalidSurfaceSlot,
                        assetPath,
                        $"Counter Surface Slot ID '{slot.SlotId}' is empty or duplicated.");
                }

                if (!hasDefaultLocalRotation)
                {
                    AddIssueOnce(
                        issues,
                        Phase4AssetIssueCode.InvalidSurfaceSlot,
                        assetPath,
                        $"Counter Surface Slot '{slot.SlotId}' must have default local rotation; actual Euler angles are {slot.transform.localEulerAngles}.");
                }

                foreach (var collider in definition.Prefab.GetComponentsInChildren<Collider>(true))
                {
                    if (!collider.enabled || collider.isTrigger ||
                        !TryGetRootLocalColliderBounds(
                            definition.Prefab,
                            collider,
                            out var colliderBounds))
                    {
                        continue;
                    }

                    var overlapsSlotArea =
                        colliderBounds.min.x < localPosition.x + 0.5f &&
                        colliderBounds.max.x > localPosition.x - 0.5f &&
                        colliderBounds.min.z < localPosition.z + 0.5f &&
                        colliderBounds.max.z > localPosition.z - 0.5f;
                    if (overlapsSlotArea && colliderBounds.max.y > localPosition.y + 0.0001f)
                    {
                        AddIssueOnce(
                            issues,
                            Phase4AssetIssueCode.InvalidSurfaceSlot,
                            assetPath,
                            $"Counter Collider '{GetHierarchyIdentity(definition.Prefab, collider.transform)}' extends above Surface Slot '{slot.SlotId}' plane at local Y {localPosition.y} and blocks its 1m x 1m placement area.");
                    }
                }
            }

            for (var first = 0; first < slots.Length; first++)
            {
                for (var second = first + 1; second < slots.Length; second++)
                {
                    if (Math.Abs(localPositions[first].x - localPositions[second].x) < 0.9999f &&
                        Math.Abs(localPositions[first].z - localPositions[second].z) < 0.9999f)
                    {
                        AddIssueOnce(
                            issues,
                            Phase4AssetIssueCode.InvalidSurfaceSlot,
                            assetPath,
                            $"Counter Surface Slots '{slots[first].SlotId}' and '{slots[second].SlotId}' overlap their 1m x 1m areas.");
                    }
                }
            }
        }

        private static void ValidateWorkTableSurfaceSlot(
            FurnitureDefinitionAsset definition,
            string assetPath,
            ICollection<Phase4AssetValidationIssue> issues)
        {
            if (!IsFamily(definition, "furniture.work-table."))
            {
                return;
            }

            var slots = definition.Prefab.GetComponentsInChildren<SurfaceSlotMarker>(true);
            if (slots.Length != 1)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.InvalidSurfaceSlot,
                    assetPath,
                    $"Work Table requires exactly one SurfaceSlotMarker, including inactive descendants; actual count is {slots.Length}.");
            }

            if (definition.AllowedPlacementSurfaces != AnimalCafe.Layout.PlacementSurfaceType.Floor)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.InvalidSurfaceSlot,
                    assetPath,
                    $"Work Table AllowedPlacementSurfaces must be Floor; actual value is {definition.AllowedPlacementSurfaces}.");
            }

            if (definition.FunctionType != AnimalCafe.Layout.FurnitureFunctionType.None)
            {
                AddIssueOnce(
                    issues,
                    Phase4AssetIssueCode.InvalidSurfaceSlot,
                    assetPath,
                    $"Work Table FunctionType must be None; actual value is {definition.FunctionType}.");
            }
        }

        private static void ValidateSingleSlotFit(
            FurnitureDefinitionAsset definition,
            string assetPath,
            Phase4AssetIssueCode issueCode,
            string familyName,
            ICollection<Phase4AssetValidationIssue> issues)
        {
            if (definition.FootprintWidth != 1 || definition.FootprintDepth != 1)
            {
                AddIssueOnce(
                    issues,
                    issueCode,
                    assetPath,
                    $"{familyName} footprint must be exactly 1 x 1; actual footprint is {definition.FootprintWidth} x {definition.FootprintDepth}.");
            }

            var renderers = definition.Prefab.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                .ToArray();
            if (TryGetRootLocalRendererBounds(definition.Prefab, renderers, out var bounds) &&
                (bounds.min.x < -0.5001f || bounds.max.x > 0.5001f ||
                 bounds.min.z < -0.5001f || bounds.max.z > 0.5001f))
            {
                AddIssueOnce(
                    issues,
                    issueCode,
                    assetPath,
                    $"{familyName} visible Model bounds must fit one 1m x 1m slot; actual root-local bounds are {bounds}.");
            }
        }

        private static bool IsFamily(
            FurnitureDefinitionAsset definition,
            string definitionIdPrefix)
        {
            return !string.IsNullOrEmpty(definition.DefinitionId) &&
                definition.DefinitionId.StartsWith(definitionIdPrefix, StringComparison.Ordinal);
        }

        private static Phase4AssetValidationReport CreateSingleAssetReport(
            IReadOnlyCollection<Phase4AssetValidationIssue> issues)
        {
            return new Phase4AssetValidationReport(
                issues.Count == 0 ? 1 : 0,
                issues.Count == 0 ? 0 : 1,
                issues);
        }

        private static Phase4AssetValidationReport CombineReports(
            params Phase4AssetValidationReport[] reports)
        {
            return new Phase4AssetValidationReport(
                reports.Sum(report => report.ValidAssetCount),
                reports.Sum(report => report.InvalidAssetCount),
                reports.SelectMany(report => report.Issues));
        }

        private static string GetDefinitionAssetPath(FurnitureDefinitionAsset definition)
        {
            var assetPath = AssetDatabase.GetAssetPath(definition);
            return string.IsNullOrEmpty(assetPath)
                ? $"<unsaved:{definition.DefinitionId ?? "missing-id"}>"
                : assetPath;
        }

        private static string GetWallDefinitionAssetPath(WallMountedDefinitionAsset definition)
        {
            var assetPath = AssetDatabase.GetAssetPath(definition);
            return string.IsNullOrEmpty(assetPath)
                ? $"<unsaved:{definition.DefinitionId ?? "missing-id"}>"
                : assetPath;
        }

        private static string GetComponentAssetPath(Component component, string stableId)
        {
            var assetPath = AssetDatabase.GetAssetPath(component.gameObject);
            return string.IsNullOrEmpty(assetPath)
                ? $"<unsaved:{stableId ?? component.name}>"
                : assetPath;
        }

        private static bool ColliderOverlapsEntranceClearance(
            Transform entranceRoot,
            Collider collider)
        {
            var worldBounds = collider.bounds;
            var minimum = new Vector3(float.PositiveInfinity, 0f, float.PositiveInfinity);
            var maximum = new Vector3(float.NegativeInfinity, 0f, float.NegativeInfinity);
            foreach (var corner in GetBoundsCorners(worldBounds))
            {
                var localCorner = entranceRoot.InverseTransformPoint(corner);
                minimum.x = Math.Min(minimum.x, localCorner.x);
                minimum.z = Math.Min(minimum.z, localCorner.z);
                maximum.x = Math.Max(maximum.x, localCorner.x);
                maximum.z = Math.Max(maximum.z, localCorner.z);
            }

            return minimum.x < 1f && maximum.x > -1f &&
                minimum.z < 1f && maximum.z > -1f;
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

        private static void AddIssue(
            ICollection<Phase4AssetValidationIssue> issues,
            Phase4AssetIssueCode code,
            string assetPath,
            string message)
        {
            issues.Add(new Phase4AssetValidationIssue(code, assetPath, message));
        }

        private static void AddIssueOnce(
            ICollection<Phase4AssetValidationIssue> issues,
            Phase4AssetIssueCode code,
            string assetPath,
            string message)
        {
            if (issues.Any(issue => issue.Code == code &&
                string.Equals(issue.AssetPath, assetPath, StringComparison.Ordinal) &&
                string.Equals(issue.Message, message, StringComparison.Ordinal)))
            {
                return;
            }

            AddIssue(issues, code, assetPath, message);
        }
    }
}

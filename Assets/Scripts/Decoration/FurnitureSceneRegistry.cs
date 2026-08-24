using System;
using System.Collections.Generic;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    public enum FurnitureSceneIssueCode
    {
        MissingDefinition,
        MissingPrefab,
        DuplicateInstanceId
    }

    public sealed class FurnitureSceneIssue
    {
        public const string MissingDefinitionLogCode = "P6_SCENE_MISSING_DEFINITION";
        public const string MissingPrefabLogCode = "P6_SCENE_MISSING_PREFAB";
        public const string DuplicateInstanceIdLogCode = "P6_SCENE_DUPLICATE_INSTANCE_ID";

        public FurnitureSceneIssue(
            FurnitureSceneIssueCode code,
            string instanceId,
            string definitionId,
            string message)
        {
            Code = code;
            InstanceId = instanceId ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            Message = message ?? string.Empty;
            LogCode = GetLogCode(code);
        }

        public FurnitureSceneIssueCode Code { get; }
        public string LogCode { get; }
        public string InstanceId { get; }
        public string DefinitionId { get; }
        public string Message { get; }

        public override string ToString()
        {
            return $"[{LogCode}] {Message}";
        }

        private static string GetLogCode(FurnitureSceneIssueCode code)
        {
            switch (code)
            {
                case FurnitureSceneIssueCode.MissingDefinition:
                    return MissingDefinitionLogCode;
                case FurnitureSceneIssueCode.MissingPrefab:
                    return MissingPrefabLogCode;
                case FurnitureSceneIssueCode.DuplicateInstanceId:
                    return DuplicateInstanceIdLogCode;
                default:
                    throw new ArgumentOutOfRangeException(nameof(code), code, null);
            }
        }
    }

    /// <summary>
    /// Owns exactly one formal Scene representation for each runtime furniture
    /// Instance ID. CafeLayout remains the formal data source.
    /// æ¯ä¸ª runtime Furniture Instance ID åªæ‹¥æœ‰ä¸€ä¸ªæ­£å¼ Scene representationã€‚
    /// </summary>
    public sealed class FurnitureSceneRegistry : MonoBehaviour
    {
        private readonly Dictionary<string, RepresentationRecord> recordsById =
            new Dictionary<string, RepresentationRecord>(StringComparer.Ordinal);
        private readonly List<FurnitureSceneIssue> issues =
            new List<FurnitureSceneIssue>();
        private IReadOnlyList<FurnitureSceneIssue> readOnlyIssues;

        private FurnitureContentCatalog contentCatalog;
        private Transform representationRoot;
        private DecorationGridSpace gridSpace;
        private bool isConfigured;

        public IReadOnlyList<FurnitureSceneIssue> LastIssues =>
            readOnlyIssues ??= issues.AsReadOnly();

        public void Configure(
            FurnitureContentCatalog contentCatalog,
            Transform root,
            DecorationGridSpace gridSpace)
        {
            this.contentCatalog = contentCatalog ??
                throw new ArgumentNullException(nameof(contentCatalog));
            representationRoot = root ?? throw new ArgumentNullException(nameof(root));
            this.gridSpace = gridSpace;
            isConfigured = true;
            ClearOwnedRepresentations();
            issues.Clear();
        }

        public void Rebuild(IReadOnlyList<FurnitureInstance> instances)
        {
            EnsureConfigured();
            if (instances == null)
            {
                throw new ArgumentNullException(nameof(instances));
            }

            issues.Clear();
            var countsById = new Dictionary<string, int>(StringComparer.Ordinal);
            var firstById = new Dictionary<string, FurnitureInstance>(StringComparer.Ordinal);

            for (var index = 0; index < instances.Count; index++)
            {
                var instance = instances[index];
                if (instance == null)
                {
                    throw new ArgumentException(
                        $"Furniture instance at index {index} must not be null.",
                        nameof(instances));
                }

                countsById.TryGetValue(instance.InstanceId, out var count);
                countsById[instance.InstanceId] = count + 1;
                if (count == 0)
                {
                    firstById.Add(instance.InstanceId, instance);
                }
            }

            var validCandidates =
                new Dictionary<string, SceneCandidate>(StringComparer.Ordinal);

            foreach (var pair in firstById)
            {
                var instance = pair.Value;
                if (countsById[pair.Key] > 1)
                {
                    AddIssue(
                        FurnitureSceneIssueCode.DuplicateInstanceId,
                        instance.InstanceId,
                        instance.DefinitionId,
                        $"Instance ID '{instance.InstanceId}' appears more than once.");
                    continue;
                }

                if (!contentCatalog.TryGetDefinitionAsset(
                    instance.DefinitionId,
                    out var definitionAsset))
                {
                    AddIssue(
                        FurnitureSceneIssueCode.MissingDefinition,
                        instance.InstanceId,
                        instance.DefinitionId,
                        $"Instance '{instance.InstanceId}' references missing Definition " +
                        $"'{instance.DefinitionId}'.");
                    continue;
                }

                if (definitionAsset.Prefab == null)
                {
                    AddIssue(
                        FurnitureSceneIssueCode.MissingPrefab,
                        instance.InstanceId,
                        instance.DefinitionId,
                        $"Instance '{instance.InstanceId}' Definition " +
                        $"'{instance.DefinitionId}' has no Prefab.");
                    continue;
                }

                validCandidates.Add(
                    instance.InstanceId,
                    new SceneCandidate(instance, definitionAsset));
            }

            RemoveRecordsAbsentFrom(validCandidates);

            foreach (var pair in validCandidates)
            {
                var candidate = pair.Value;
                if (!recordsById.TryGetValue(pair.Key, out var record) ||
                    record.Representation == null ||
                    record.Prefab != candidate.Definition.Prefab ||
                    !string.Equals(
                        record.DefinitionId,
                        candidate.Instance.DefinitionId,
                        StringComparison.Ordinal))
                {
                    if (record != null && record.Representation != null)
                    {
                        record.Representation.SetActive(false);
                        DestroyOwnedObject(record.Representation);
                    }

                    var representation = Instantiate(
                        candidate.Definition.Prefab,
                        representationRoot,
                        false);
                    representation.name = "Furniture_" + candidate.Instance.InstanceId;
                    representation.SetActive(true);
                    record = new RepresentationRecord(
                        candidate.Instance.DefinitionId,
                        candidate.Definition.Prefab,
                        representation);
                    recordsById[pair.Key] = record;
                }

                ApplyTransform(record.Representation.transform, candidate);
            }
        }

        public bool TryGet(string instanceId, out GameObject representation)
        {
            if (instanceId != null &&
                recordsById.TryGetValue(instanceId, out var record) &&
                record.Representation != null)
            {
                representation = record.Representation;
                return true;
            }

            representation = null;
            return false;
        }

        public bool SetRepresentationVisible(string instanceId, bool visible)
        {
            if (instanceId == null)
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            if (!recordsById.TryGetValue(instanceId, out var record) ||
                record.Representation == null)
            {
                return false;
            }

            record.Representation.SetActive(visible);
            return true;
        }

        public bool TryGetInstanceId(Component hitComponent, out string instanceId)
        {
            if (hitComponent != null)
            {
                foreach (var pair in recordsById)
                {
                    var representation = pair.Value.Representation;
                    if (representation != null &&
                        hitComponent.transform.IsChildOf(representation.transform))
                    {
                        instanceId = pair.Key;
                        return true;
                    }
                }
            }

            instanceId = null;
            return false;
        }

        public void Remove(string instanceId)
        {
            if (instanceId == null)
            {
                throw new ArgumentNullException(nameof(instanceId));
            }

            if (!recordsById.TryGetValue(instanceId, out var record))
            {
                return;
            }

            recordsById.Remove(instanceId);
            if (record.Representation != null)
            {
                record.Representation.SetActive(false);
            }
            DestroyOwnedObject(record.Representation);
        }

        private void OnDisable()
        {
            ClearOwnedRepresentations();
            issues.Clear();
        }

        private void OnDestroy()
        {
            ClearOwnedRepresentations();
        }

        private void ApplyTransform(Transform target, SceneCandidate candidate)
        {
            var cells = GetCurrentCells(candidate.Instance, candidate.Definition);
            target.localScale = candidate.Definition.Prefab.transform.localScale;
            target.localPosition = gridSpace.GetFootprintCenterLocal(cells);
            target.localRotation = gridSpace.GetLocalRotation(candidate.Instance.Rotation);
            target.gameObject.SetActive(true);
        }

        private static IReadOnlyList<GridPosition> GetCurrentCells(
            FurnitureInstance instance,
            FurnitureDefinitionAsset definition)
        {
            var width = definition.FootprintWidth;
            var depth = definition.FootprintDepth;
            switch (instance.Rotation)
            {
                case FurnitureRotation.Degrees0:
                case FurnitureRotation.Degrees180:
                    break;
                case FurnitureRotation.Degrees90:
                case FurnitureRotation.Degrees270:
                    var originalWidth = width;
                    width = depth;
                    depth = originalWidth;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(instance.Rotation),
                        instance.Rotation,
                        "Rotation must be a defined quarter turn.");
            }

            var cells = new List<GridPosition>(width * depth);
            for (var x = 0; x < width; x++)
            {
                for (var y = 0; y < depth; y++)
                {
                    cells.Add(new GridPosition(
                        checked(instance.Position.X + x),
                        checked(instance.Position.Y + y)));
                }
            }

            return cells;
        }

        private void RemoveRecordsAbsentFrom(
            IReadOnlyDictionary<string, SceneCandidate> validCandidates)
        {
            var removedIds = new List<string>();
            foreach (var pair in recordsById)
            {
                if (!validCandidates.ContainsKey(pair.Key))
                {
                    removedIds.Add(pair.Key);
                }
            }

            foreach (var removedId in removedIds)
            {
                var record = recordsById[removedId];
                recordsById.Remove(removedId);
                if (record.Representation != null)
                {
                    record.Representation.SetActive(false);
                }
                DestroyOwnedObject(record.Representation);
            }
        }

        private void AddIssue(
            FurnitureSceneIssueCode code,
            string instanceId,
            string definitionId,
            string message)
        {
            var issue = new FurnitureSceneIssue(
                code,
                instanceId,
                definitionId,
                message);
            issues.Add(issue);
            Debug.LogError(issue.ToString(), this);
        }

        private void EnsureConfigured()
        {
            if (!isConfigured || contentCatalog == null || representationRoot == null)
            {
                throw new InvalidOperationException(
                    "FurnitureSceneRegistry must be configured before use.");
            }
        }

        private void ClearOwnedRepresentations()
        {
            foreach (var record in recordsById.Values)
            {
                if (record.Representation != null)
                {
                    record.Representation.SetActive(false);
                }
                DestroyOwnedObject(record.Representation);
            }

            recordsById.Clear();
        }

        private static void DestroyOwnedObject(UnityEngine.Object ownedObject)
        {
            if (ownedObject != null)
            {
                UnityEngine.Object.Destroy(ownedObject);
            }
        }

        private sealed class RepresentationRecord
        {
            public RepresentationRecord(
                string definitionId,
                GameObject prefab,
                GameObject representation)
            {
                DefinitionId = definitionId;
                Prefab = prefab;
                Representation = representation;
            }

            public string DefinitionId { get; }
            public GameObject Prefab { get; }
            public GameObject Representation { get; }
        }

        private readonly struct SceneCandidate
        {
            public SceneCandidate(
                FurnitureInstance instance,
                FurnitureDefinitionAsset definition)
            {
                Instance = instance;
                Definition = definition;
            }

            public FurnitureInstance Instance { get; }
            public FurnitureDefinitionAsset Definition { get; }
        }
    }
}

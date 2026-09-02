using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnimalCafe.Decoration
{
    /// <summary>
    /// Temporarily fades decoration representations and restores their exact Materials and MPBs.
    /// </summary>
    public sealed class WallOcclusionFadeView : MonoBehaviour
    {
        private const int TargetSamplesPerAxis = 5;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");

        [SerializeField] private Material fadeMaterialTemplate;
        [SerializeField] private UnityEngine.Camera viewCamera;
        [SerializeField, Range(0f, 1f)] private float fadeOpacity = 0.35f;

        private readonly Dictionary<Renderer, RendererFadeState> originalStates =
            new Dictionary<Renderer, RendererFadeState>();
        private MaterialPropertyBlock workingBlock;

        private Renderer targetRenderer;
        private Transform targetRepresentationRoot;
        private Transform nonDecorationBlockerRoot;
        private bool blockersCurrentForTarget;

        public void SetNonDecorationBlockerRoot(Transform root)
        {
            if (ReferenceEquals(nonDecorationBlockerRoot, root))
            {
                return;
            }

            RestoreAllFades();
            nonDecorationBlockerRoot = root;
        }

        public void ConfigureTarget(Renderer targetRenderer)
        {
            if (targetRenderer == null)
            {
                throw new ArgumentNullException(nameof(targetRenderer));
            }

            var representationRoot = ResolveRepresentationRoot(targetRenderer.transform);
            if (this.targetRenderer == targetRenderer
                && targetRepresentationRoot == representationRoot)
            {
                workingBlock ??= new MaterialPropertyBlock();
                EnsureConfigured();
                return;
            }

            RestoreAllFades();
            this.targetRenderer = targetRenderer;
            targetRepresentationRoot = representationRoot;
            workingBlock ??= new MaterialPropertyBlock();
            EnsureConfigured();
        }

        public void Configure(
            UnityEngine.Camera viewCamera,
            Renderer targetRenderer,
            float fadeOpacity)
        {
            Configure(viewCamera, targetRenderer, fadeOpacity, fadeMaterialTemplate);
        }

        /// <summary>Configures a deterministic fade Material binding for runtime or tests.</summary>
        public void Configure(
            UnityEngine.Camera viewCamera,
            Renderer targetRenderer,
            float fadeOpacity,
            Material fadeMaterialTemplate)
        {
            RestoreAllFades();
            this.viewCamera = viewCamera ?? throw new ArgumentNullException(nameof(viewCamera));
            this.targetRenderer = targetRenderer ?? throw new ArgumentNullException(nameof(targetRenderer));
            if (fadeOpacity < 0f || fadeOpacity > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(fadeOpacity));
            }

            this.fadeOpacity = fadeOpacity;
            this.fadeMaterialTemplate = fadeMaterialTemplate ??
                throw new ArgumentNullException(nameof(fadeMaterialTemplate));
            targetRepresentationRoot = ResolveRepresentationRoot(targetRenderer.transform);
            workingBlock = new MaterialPropertyBlock();
        }

        public void FadeBlockersForTarget()
        {
            try
            {
                EnsureConfigured();
                if (blockersCurrentForTarget)
                {
                    return;
                }

                RestoreAllFades();
                if (!TryGetTargetScreenRect(out var targetScreenRect))
                {
                    blockersCurrentForTarget = true;
                    return;
                }

                // Direct Preview updates can move objects before the next physics step.
                // Refresh first, then sample the selected wall's visible screen area.
                // A single centre ray misses furniture that covers only a wall edge.
                Physics.SyncTransforms();
                var fadedRoots = new HashSet<Transform>();
                for (var row = 0; row < TargetSamplesPerAxis; row++)
                {
                    var normalizedY = (row + 0.5f) / TargetSamplesPerAxis;
                    for (var column = 0; column < TargetSamplesPerAxis; column++)
                    {
                        var normalizedX = (column + 0.5f) / TargetSamplesPerAxis;
                        var screenPoint = new Vector3(
                            Mathf.Lerp(targetScreenRect.xMin, targetScreenRect.xMax, normalizedX),
                            Mathf.Lerp(targetScreenRect.yMin, targetScreenRect.yMax, normalizedY));
                        var viewRay = viewCamera.ScreenPointToRay(screenPoint);
                        if (!TryGetTargetSurfaceDistance(viewRay, out var targetDistance))
                        {
                            continue;
                        }

                        var hits = Physics.RaycastAll(
                            viewRay,
                            targetDistance,
                            Physics.DefaultRaycastLayers,
                            QueryTriggerInteraction.Ignore);
                        foreach (var hit in hits)
                        {
                            if (nonDecorationBlockerRoot != null
                                && (hit.collider.transform == nonDecorationBlockerRoot
                                    || hit.collider.transform.IsChildOf(nonDecorationBlockerRoot)))
                            {
                                continue;
                            }

                            var blockerRoot = ResolveRepresentationRoot(hit.collider.transform);
                            if (blockerRoot == targetRepresentationRoot
                                || !fadedRoots.Add(blockerRoot))
                            {
                                continue;
                            }

                            foreach (var blocker in blockerRoot.GetComponentsInChildren<Renderer>(true))
                            {
                                if (blocker.enabled)
                                {
                                    Fade(blocker);
                                }
                            }
                        }
                    }
                }
                blockersCurrentForTarget = true;
            }
            catch
            {
                RestoreAllFades();
                throw;
            }
        }

        private bool TryGetTargetSurfaceDistance(Ray viewRay, out float targetDistance)
        {
            // Renderer.bounds is a world-axis-aligned box. On an isometric/rotated wall
            // its near corner can be metres closer than the visible wall plane, which
            // incorrectly excludes real furniture that is still in front of the wall.
            var wallPlane = new Plane(targetRenderer.transform.forward, targetRenderer.bounds.center);
            return wallPlane.Raycast(viewRay, out targetDistance)
                && targetDistance > Mathf.Epsilon;
        }

        /// <summary>
        /// Fades each supplied representation as one rendering-only group.
        /// This does not change Colliders, occupancy, transforms, or saved layout data.
        /// </summary>
        public void FadeRepresentations(IEnumerable<Transform> representationRoots)
        {
            if (representationRoots == null)
            {
                throw new ArgumentNullException(nameof(representationRoots));
            }

            try
            {
                EnsureFadeMaterialConfigured();
                RestoreAllFades();
                workingBlock ??= new MaterialPropertyBlock();
                var fadedRenderers = new HashSet<Renderer>();
                foreach (var representationRoot in representationRoots)
                {
                    if (representationRoot == null)
                    {
                        continue;
                    }

                    foreach (var renderer in representationRoot.GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer.enabled && fadedRenderers.Add(renderer))
                        {
                            Fade(renderer);
                        }
                    }
                }
            }
            catch
            {
                RestoreAllFades();
                throw;
            }
        }

        private bool TryGetTargetScreenRect(out Rect screenRect)
        {
            var bounds = targetRenderer.bounds;
            var minimum = bounds.min;
            var maximum = bounds.max;
            var minScreen = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maxScreen = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var hasVisibleCorner = false;
            for (var x = 0; x < 2; x++)
            {
                for (var y = 0; y < 2; y++)
                {
                    for (var z = 0; z < 2; z++)
                    {
                        var worldCorner = new Vector3(
                            x == 0 ? minimum.x : maximum.x,
                            y == 0 ? minimum.y : maximum.y,
                            z == 0 ? minimum.z : maximum.z);
                        var projected = viewCamera.WorldToScreenPoint(worldCorner);
                        if (projected.z <= 0f)
                        {
                            continue;
                        }

                        hasVisibleCorner = true;
                        minScreen = Vector2.Min(minScreen, projected);
                        maxScreen = Vector2.Max(maxScreen, projected);
                    }
                }
            }

            screenRect = hasVisibleCorner
                ? Rect.MinMaxRect(minScreen.x, minScreen.y, maxScreen.x, maxScreen.y)
                : default;
            return hasVisibleCorner;
        }

        public void RestoreAllFades()
        {
            foreach (var pair in originalStates)
            {
                if (pair.Key != null)
                {
                    pair.Key.sharedMaterials = pair.Value.SourceMaterials;
                    pair.Key.SetPropertyBlock(pair.Value.PropertyBlock);
                }

                foreach (var fadeMaterial in pair.Value.FadeMaterials)
                {
                    if (fadeMaterial != null)
                    {
                        Destroy(fadeMaterial);
                    }
                }
            }

            originalStates.Clear();
            blockersCurrentForTarget = false;
        }

        private void OnDisable()
        {
            RestoreAllFades();
        }

        private void OnDestroy()
        {
            RestoreAllFades();
        }

        private void Fade(Renderer blocker)
        {
            if (!originalStates.TryGetValue(blocker, out var state))
            {
                var original = new MaterialPropertyBlock();
                blocker.GetPropertyBlock(original);
                var sourceMaterials = blocker.sharedMaterials;
                var fadeMaterials = new Material[Mathf.Max(1, sourceMaterials.Length)];
                for (var index = 0; index < fadeMaterials.Length; index++)
                {
                    fadeMaterials[index] = new Material(fadeMaterialTemplate);
                    var sourceMaterial = index < sourceMaterials.Length ? sourceMaterials[index] : null;
                    ApplySourceAppearance(sourceMaterial, fadeMaterials[index]);
                }

                state = new RendererFadeState(original, sourceMaterials, fadeMaterials);
                originalStates.Add(blocker, state);
                blocker.sharedMaterials = fadeMaterials;
            }

            workingBlock.Clear();
            blocker.GetPropertyBlock(workingBlock);
            workingBlock.SetFloat("_FadeOpacity", fadeOpacity);
            blocker.SetPropertyBlock(workingBlock);
        }

        private static void ApplySourceAppearance(Material source, Material fadeMaterial)
        {
            if (source == null)
            {
                return;
            }

            var texture = source.HasProperty(BaseMapId)
                ? source.GetTexture(BaseMapId)
                : source.HasProperty(MainTextureId)
                    ? source.GetTexture(MainTextureId)
                    : source.mainTexture;
            if (texture != null)
            {
                fadeMaterial.SetTexture(BaseMapId, texture);
            }

            var color = source.HasProperty(BaseColorId)
                ? source.GetColor(BaseColorId)
                : source.HasProperty(ColorId)
                    ? source.GetColor(ColorId)
                    : source.color;
            fadeMaterial.SetColor(BaseColorId, color);
        }

        private static Transform ResolveRepresentationRoot(Transform leaf)
        {
            var marker = leaf.GetComponentInParent<OcclusionFadeRepresentationRoot>();
            return marker == null ? leaf : marker.transform;
        }

        private void EnsureConfigured()
        {
            EnsureFadeMaterialConfigured();
            if (viewCamera == null || targetRenderer == null ||
                targetRepresentationRoot == null)
            {
                throw new InvalidOperationException("WallOcclusionFadeView must be configured first.");
            }
        }

        private void EnsureFadeMaterialConfigured()
        {
            if (fadeMaterialTemplate == null)
            {
                throw new InvalidOperationException(
                    "WallOcclusionFadeView requires a fade Material before rendering can fade.");
            }
        }

        private sealed class RendererFadeState
        {
            public RendererFadeState(
                MaterialPropertyBlock propertyBlock,
                Material[] sourceMaterials,
                Material[] fadeMaterials)
            {
                PropertyBlock = propertyBlock;
                SourceMaterials = sourceMaterials;
                FadeMaterials = fadeMaterials;
            }

            public MaterialPropertyBlock PropertyBlock { get; }
            public Material[] SourceMaterials { get; }
            public Material[] FadeMaterials { get; }
        }
    }
}

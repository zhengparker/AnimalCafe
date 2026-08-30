using System;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using UnityEngine;
using UnityEngine.Rendering;

namespace AnimalCafe.Decoration
{
    /// <summary>Renders one wall's appearance through MaterialPropertyBlock only.</summary>
    public sealed class WallSurfaceView : MonoBehaviour
    {
        private static readonly int WallpaperTilingId = Shader.PropertyToID("_WallpaperTiling");
        private static readonly int BaseMapScaleOffsetId = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int WainscotingCutoffId = Shader.PropertyToID("_WainscotingCutoff");
        private static readonly int WainscotingEnabledId = Shader.PropertyToID("_WainscotingEnabled");
        private static readonly int SelectionHighlightId = Shader.PropertyToID("_SelectionHighlight");

        private MaterialPropertyBlock propertyBlock;
        private WallSurfaceAuthoring authoring;
        private Renderer targetRenderer;
        private Renderer finishRenderer;
        private Renderer wainscotingRenderer;
        private Renderer wainscotingRailRenderer;
        private Renderer wainscotingBaseboardRenderer;
        private float canonicalWallHeight;
        private SurfaceStyleLookup styleLookup;
        private RoomSurfaceSnapshot confirmedSnapshot;
        private SurfaceStyleDefinitionAsset activeBaseStyle;
        private SurfaceStyleDefinitionAsset activeWainscotingStyle;

        public string SurfaceId => authoring == null ? null : authoring.SurfaceId;
        public Vector2 WallpaperTiling { get; private set; }
        public float WainscotingCutoff { get; private set; }
        public bool IsSelected { get; private set; }

        public void SetSelected(bool selected)
        {
            EnsureConfigured();
            IsSelected = selected;
            if (UsesDimensionalFinishLayers && activeBaseStyle != null)
            {
                ApplyDimensionalAppearance(activeBaseStyle, activeWainscotingStyle);
                return;
            }

            ApplySelectionState(targetRenderer, targetRenderer.sharedMaterial);
        }

        public void Configure(
            WallSurfaceAuthoring authoring,
            Renderer targetRenderer,
            float canonicalWallHeightMeters,
            float sharedWaistHeightMeters)
        {
            this.authoring = authoring ?? throw new ArgumentNullException(nameof(authoring));
            this.targetRenderer = targetRenderer ?? throw new ArgumentNullException(nameof(targetRenderer));
            finishRenderer = authoring.transform.Find("Phase7_WallFinish")?.GetComponent<Renderer>();
            wainscotingRenderer = authoring.transform.Find("Phase7_WainscotingFinish")?.GetComponent<Renderer>();
            wainscotingRailRenderer = authoring.transform.Find("Phase7_WainscotingRailLip")?.GetComponent<Renderer>();
            wainscotingBaseboardRenderer = authoring.transform.Find("Phase7_WainscotingBaseboardLip")?.GetComponent<Renderer>();
            ConfigureShadowOwnership();
            if (canonicalWallHeightMeters <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(canonicalWallHeightMeters));
            }

            if (!Mathf.Approximately(
                    sharedWaistHeightMeters,
                    CharacterScaleReference.SharedCharacterWaistHeightMeters))
            {
                throw new ArgumentOutOfRangeException(nameof(sharedWaistHeightMeters),
                    "Use CharacterScaleReference.SharedCharacterWaistHeightMeters.");
            }

            canonicalWallHeight = canonicalWallHeightMeters;
            propertyBlock = new MaterialPropertyBlock();
            WallpaperTiling = new Vector2(authoring.Columns, 1f);
            WainscotingCutoff = CharacterScaleReference.GetNormalizedWainscotingCutoff(
                canonicalWallHeight);
        }

        public void Configure(
            WallSurfaceAuthoring authoring,
            Renderer targetRenderer,
            float canonicalWallHeightMeters,
            SurfaceStyleLookup styleLookup)
        {
            if (styleLookup == null)
            {
                throw new ArgumentNullException(nameof(styleLookup));
            }

            Configure(
                authoring,
                targetRenderer,
                canonicalWallHeightMeters,
                CharacterScaleReference.SharedCharacterWaistHeightMeters);
            this.styleLookup = styleLookup;
        }

        /// <summary>Uses the configured renderer bounds as the canonical Wall height.</summary>
        public void Configure(
            WallSurfaceAuthoring authoring,
            Renderer targetRenderer,
            SurfaceStyleLookup styleLookup)
        {
            if (targetRenderer == null)
            {
                throw new ArgumentNullException(nameof(targetRenderer));
            }

            var canonicalWallHeightMeters = targetRenderer.bounds.size.y;
            if (canonicalWallHeightMeters <= Mathf.Epsilon)
            {
                throw new ArgumentOutOfRangeException(nameof(targetRenderer),
                    "Wall renderer bounds must have a positive height.");
            }

            Configure(authoring, targetRenderer, canonicalWallHeightMeters, styleLookup);
        }

        public void RenderConfirmed(RoomSurfaceLayout layout)
        {
            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            EnsureConfigured();
            confirmedSnapshot = layout.CaptureSnapshot();
            RenderLayout(RoomSurfaceLayout.FromSnapshot(confirmedSnapshot));
        }

        public void RenderPreview(SurfacePreviewTransaction preview)
        {
            if (preview == null)
            {
                throw new ArgumentNullException(nameof(preview));
            }

            EnsureConfigured();
            RenderLayout(RoomSurfaceLayout.FromSnapshot(preview.ProposedSnapshot));
        }

        public void ClearPreview()
        {
            if (confirmedSnapshot == null)
            {
                return;
            }

            EnsureConfigured();
            RenderLayout(RoomSurfaceLayout.FromSnapshot(confirmedSnapshot));
        }

        private void RenderLayout(RoomSurfaceLayout layout)
        {
            if (!layout.TryGetWall(authoring.SurfaceId, out var appearance))
            {
                throw new ArgumentException("Layout does not contain this wall Surface ID.", nameof(layout));
            }

            ApplyAppearance(appearance.BaseStyleId, appearance.WainscotingStyleId);
        }

        private void ApplyAppearance(string baseStyleId, string wainscotingStyleId)
        {
            if (styleLookup == null)
            {
                // Compatibility overload for the first isolated Task 7 fixture.
                ApplyPropertyBlock(null, null, false);
                return;
            }

            var baseStyle = styleLookup.GetRequired(baseStyleId, GetBaseKind(baseStyleId));
            var wainscoting = wainscotingStyleId == null
                ? null
                : styleLookup.GetRequired(wainscotingStyleId, SurfaceStyleKind.Wainscoting);
            if (wainscoting != null && wainscoting.IsNoneOption) wainscoting = null;
            ApplyPropertyBlock(baseStyle, wainscoting, wainscoting != null);
        }

        private void ApplyPropertyBlock(
            SurfaceStyleDefinitionAsset baseStyle,
            SurfaceStyleDefinitionAsset wainscoting,
            bool hasWainscoting)
        {
            WallpaperTiling = new Vector2(authoring.Columns, 1f);
            WainscotingCutoff = CharacterScaleReference.GetNormalizedWainscotingCutoff(
                canonicalWallHeight);
            activeBaseStyle = baseStyle;
            activeWainscotingStyle = hasWainscoting ? wainscoting : null;
            if (UsesDimensionalFinishLayers && baseStyle != null)
            {
                ApplyDimensionalAppearance(baseStyle, activeWainscotingStyle);
                return;
            }

            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(WallpaperTilingId, WallpaperTiling);
            propertyBlock.SetFloat(WainscotingCutoffId, WainscotingCutoff);
            propertyBlock.SetFloat(WainscotingEnabledId, hasWainscoting ? 1f : 0f);
            propertyBlock.SetFloat(SelectionHighlightId, IsSelected ? 1f : 0f);
            if (baseStyle != null)
            {
                propertyBlock.SetTexture("_BaseMap", GetTexture(baseStyle.Material) ?? Texture2D.whiteTexture);
                propertyBlock.SetColor("_BaseColor", GetColor(baseStyle.Material));
            }

            if (wainscoting != null)
            {
                propertyBlock.SetTexture("_WainscotingMap", GetTexture(wainscoting.Material) ?? Texture2D.whiteTexture);
                propertyBlock.SetColor("_WainscotingColor", GetColor(wainscoting.Material));
            }
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private bool UsesDimensionalFinishLayers =>
            finishRenderer != null && wainscotingRenderer != null && finishRenderer != targetRenderer;

        private void ConfigureShadowOwnership()
        {
            targetRenderer.shadowCastingMode = ShadowCastingMode.On;
            targetRenderer.receiveShadows = true;
            ConfigureRenderOnlyFinish(finishRenderer);
            ConfigureRenderOnlyFinish(wainscotingRenderer);
            ConfigureRenderOnlyFinish(wainscotingRailRenderer);
            ConfigureRenderOnlyFinish(wainscotingBaseboardRenderer);
        }

        private static void ConfigureRenderOnlyFinish(Renderer renderer)
        {
            if (renderer == null) return;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private void ApplyDimensionalAppearance(
            SurfaceStyleDefinitionAsset baseStyle,
            SurfaceStyleDefinitionAsset wainscoting)
        {
            finishRenderer.enabled = true;
            finishRenderer.sharedMaterial = baseStyle.Material;
            ApplyLitFinishBlock(finishRenderer, baseStyle.Material);

            wainscotingRenderer.enabled = wainscoting != null;
            if (wainscoting != null)
            {
                wainscotingRenderer.sharedMaterial = wainscoting.Material;
                ApplyLitFinishBlock(wainscotingRenderer, wainscoting.Material);
            }

            var showArchitecturalLips = wainscoting != null
                && string.Equals(wainscoting.StyleId, "wainscoting.warm-white-rail",
                    StringComparison.Ordinal);
            ApplyArchitecturalLip(wainscotingRailRenderer, showArchitecturalLips, wainscoting);
            ApplyArchitecturalLip(wainscotingBaseboardRenderer, showArchitecturalLips, wainscoting);

            ApplySelectionState(targetRenderer, targetRenderer.sharedMaterial);
        }

        private void ApplyArchitecturalLip(
            Renderer renderer,
            bool visible,
            SurfaceStyleDefinitionAsset style)
        {
            if (renderer == null) return;
            renderer.enabled = visible;
            if (!visible || style == null) return;
            renderer.sharedMaterial = style.Material;
            ApplyLitFinishBlock(renderer, style.Material);
        }

        private void ApplyLitFinishBlock(Renderer renderer, Material sourceMaterial)
        {
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetVector(BaseMapScaleOffsetId,
                new Vector4(WallpaperTiling.x, WallpaperTiling.y, 0f, 0f));
            propertyBlock.SetFloat(SelectionHighlightId, IsSelected ? 1f : 0f);
            var color = GetColor(sourceMaterial);
            if (IsSelected)
                color = Color.Lerp(color, new Color(.38f, .72f, .48f, color.a), .22f);
            propertyBlock.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(propertyBlock);
        }

        private void ApplySelectionState(Renderer renderer, Material sourceMaterial)
        {
            if (renderer == null) return;
            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(SelectionHighlightId, IsSelected ? 1f : 0f);
            if (sourceMaterial != null && sourceMaterial.HasProperty(BaseColorId))
            {
                var color = sourceMaterial.GetColor(BaseColorId);
                if (IsSelected)
                    color = Color.Lerp(color, new Color(.38f, .72f, .48f, color.a), .18f);
                propertyBlock.SetColor(BaseColorId, color);
            }
            renderer.SetPropertyBlock(propertyBlock);
        }

        private static SurfaceStyleKind GetBaseKind(string styleId)
        {
            return styleId != null && styleId.StartsWith("wallpaper.", StringComparison.Ordinal)
                ? SurfaceStyleKind.Wallpaper
                : SurfaceStyleKind.Paint;
        }

        private static Texture GetTexture(Material material)
        {
            return material.HasProperty("_BaseMap")
                ? material.GetTexture("_BaseMap")
                : material.GetTexture("_MainTex");
        }

        private static Color GetColor(Material material)
        {
            return material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : material.color;
        }

        private void EnsureConfigured()
        {
            if (authoring == null || targetRenderer == null || canonicalWallHeight <= 0f)
            {
                throw new InvalidOperationException("WallSurfaceView must be configured before rendering.");
            }
        }
    }
}

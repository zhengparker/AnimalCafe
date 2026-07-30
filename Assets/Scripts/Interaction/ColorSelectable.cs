using UnityEngine;

namespace AnimalCafe.Interaction
{
    /// <summary>
    /// 使用 MaterialPropertyBlock 提供不修改 shared material 的选择变色。
    /// Selection color feedback without modifying the shared material.
    /// </summary>
    public sealed class ColorSelectable : MonoBehaviour, ISelectable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField]
        private Renderer targetRenderer;

        [SerializeField]
        private Color selectedColor = new(1f, 0.75f, 0.1f, 1f);

        private MaterialPropertyBlock propertyBlock;
        private Color originalColor = Color.white;
        private int activeColorProperty = BaseColorId;
        private bool isInitialized;
        private bool hasLoggedMissingRendererWarning;
        private bool hasLoggedUnsupportedMaterialWarning;

        public bool IsSelected { get; private set; }

        private void Awake()
        {
            // Scene components may still be deserializing during Awake.
            // Initialization is completed lazily in Start or on first selection.
        }

        private void Start()
        {
            if (TryInitializeRenderer())
            {
                return;
            }

            if (targetRenderer == null)
            {
                LogMissingRendererWarningOnce();
                enabled = false;
            }
        }

        private bool TryInitializeRenderer()
        {
            if (isInitialized)
            {
                return true;
            }

            targetRenderer ??= GetComponentInChildren<Renderer>();
            if (targetRenderer == null)
            {
                return false;
            }

            propertyBlock = new MaterialPropertyBlock();
            var material = targetRenderer.sharedMaterial;
            if (material == null)
            {
                LogUnsupportedMaterialWarningOnce();
                return false;
            }

            if (material.HasProperty(BaseColorId))
            {
                activeColorProperty = BaseColorId;
                originalColor = material.GetColor(BaseColorId);
            }
            else if (material.HasProperty(ColorId))
            {
                activeColorProperty = ColorId;
                originalColor = material.GetColor(ColorId);
            }
            else
            {
                LogUnsupportedMaterialWarningOnce();
                return false;
            }

            isInitialized = true;
            enabled = true;
            return true;
        }

        private void LogMissingRendererWarningOnce()
        {
            if (hasLoggedMissingRendererWarning)
            {
                return;
            }

            hasLoggedMissingRendererWarning = true;
            Debug.LogWarning("[ColorSelectable] A Renderer is required.", this);
        }

        private void LogUnsupportedMaterialWarningOnce()
        {
            if (hasLoggedUnsupportedMaterialWarning)
            {
                return;
            }

            hasLoggedUnsupportedMaterialWarning = true;
            Debug.LogWarning(
                "[ColorSelectable] Renderer material must expose _BaseColor or _Color.",
                this);
        }

        private void OnDisable()
        {
            Deselect();
        }

        public void Select()
        {
            if (IsSelected || !TryInitializeRenderer())
            {
                return;
            }

            IsSelected = true;
            ApplyColor(selectedColor);
        }

        public void Deselect()
        {
            if (!IsSelected)
            {
                return;
            }

            IsSelected = false;
            if (targetRenderer != null)
            {
                ApplyColor(originalColor);
            }
        }

        private void ApplyColor(Color color)
        {
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(activeColorProperty, color);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}

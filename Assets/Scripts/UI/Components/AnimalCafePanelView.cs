using System;
using AnimalCafe.UI.Foundation;
using UnityEngine;
using UnityEngine.UI;

namespace AnimalCafe.UI.Components
{
    /// <summary>
    /// Resolves a Panel style to Theme material and owns any Strong Frost lease.
    /// 将 Panel style 绑定到 Theme material，并管理 Strong Frost lease。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class AnimalCafePanelView : MonoBehaviour
    {
        private StrongFrostLease.StrongFrostLeaseHandle frostHandle;
        private AnimalCafeUiTheme configuredTheme;
        private UiPanelStyle requestedStyle;
        private StrongFrostLease configuredLease;
        private bool isConfigured;

        public UiPanelStyle ResolvedStyle { get; private set; }

        public void Configure(
            AnimalCafeUiTheme theme,
            UiPanelStyle requestedStyle,
            StrongFrostLease frostLease)
        {
            if (theme == null)
            {
                throw new ArgumentNullException(nameof(theme));
            }

            if (frostLease == null)
            {
                throw new ArgumentNullException(nameof(frostLease));
            }

            ReleaseFrost();
            configuredTheme = theme;
            this.requestedStyle = requestedStyle;
            configuredLease = frostLease;
            isConfigured = true;
            ResolveStyle(isActiveAndEnabled);
        }

        private void OnEnable()
        {
            if (isConfigured)
            {
                ResolveStyle(acquireStrongLease: true);
            }
        }

        private void OnDisable()
        {
            ReleaseFrost();
        }

        private void OnDestroy()
        {
            ReleaseFrost();
        }

        internal void ReleaseForClosedView()
        {
            ReleaseFrost();
        }

        internal void AcquireForOpenView()
        {
            if (isConfigured && isActiveAndEnabled)
            {
                ResolveStyle(acquireStrongLease: true);
            }
        }

        private void ResolveStyle(bool acquireStrongLease)
        {
            ReleaseFrost();
            ResolvedStyle = requestedStyle;
            if (requestedStyle == UiPanelStyle.StrongFrost)
            {
                if (acquireStrongLease)
                {
                    frostHandle = configuredLease.Acquire(this);
                    ResolvedStyle = frostHandle.ResolvedStyle;
                }
                else
                {
                    ResolvedStyle = UiPanelStyle.LightFrost;
                }
            }

            var materials = configuredTheme.Materials;
            GetComponent<Image>().material = requestedStyle == UiPanelStyle.StrongFrost
                && ResolvedStyle == UiPanelStyle.LightFrost
                    ? materials.StrongFrostFallback
                    : ResolvedStyle switch
                    {
                        UiPanelStyle.Solid => materials.Solid,
                        UiPanelStyle.LightFrost => materials.LightFrost,
                        UiPanelStyle.StrongFrost => materials.StrongFrost,
                        _ => materials.Solid
                    };
        }

        private void ReleaseFrost()
        {
            frostHandle?.Dispose();
            frostHandle = null;
        }
    }
}

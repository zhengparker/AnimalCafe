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
            ResolvedStyle = requestedStyle;
            if (requestedStyle == UiPanelStyle.StrongFrost)
            {
                frostHandle = frostLease.Acquire(this);
                ResolvedStyle = frostHandle.ResolvedStyle;
            }

            var materials = theme.Materials;
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

        private void OnDisable()
        {
            ReleaseFrost();
        }

        private void OnDestroy()
        {
            ReleaseFrost();
        }

        private void ReleaseFrost()
        {
            frostHandle?.Dispose();
            frostHandle = null;
        }
    }
}

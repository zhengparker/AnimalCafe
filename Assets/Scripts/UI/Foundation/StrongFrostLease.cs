using System;

namespace AnimalCafe.UI.Foundation
{
    /// <summary>
    /// Grants at most one Strong Frost owner without depending on shader details.
    /// 独立于 shader 的单 owner Strong Frost lease；其余请求安全降级为 Light Frost。
    /// </summary>
    public sealed class StrongFrostLease
    {
        private readonly bool isStrongFrostSupported;
        private object strongOwner;

        public StrongFrostLease(bool isStrongFrostSupported)
        {
            this.isStrongFrostSupported = isStrongFrostSupported;
        }

        public StrongFrostLeaseHandle Acquire(object owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (strongOwner is UnityEngine.Behaviour staleBehaviour
                && (!staleBehaviour || !staleBehaviour.isActiveAndEnabled))
            {
                strongOwner = null;
            }

            if (isStrongFrostSupported && strongOwner == null)
            {
                strongOwner = owner;
                return new StrongFrostLeaseHandle(this, owner, UiPanelStyle.StrongFrost);
            }

            return new StrongFrostLeaseHandle(null, owner, UiPanelStyle.LightFrost);
        }

        private void Release(object owner)
        {
            if (ReferenceEquals(strongOwner, owner))
            {
                strongOwner = null;
            }
        }

        public sealed class StrongFrostLeaseHandle : IDisposable
        {
            private StrongFrostLease lease;
            private object owner;

            internal StrongFrostLeaseHandle(
                StrongFrostLease lease,
                object owner,
                UiPanelStyle resolvedStyle)
            {
                this.lease = lease;
                this.owner = owner;
                ResolvedStyle = resolvedStyle;
            }

            public UiPanelStyle ResolvedStyle { get; }

            public void Dispose()
            {
                lease?.Release(owner);
                lease = null;
                owner = null;
            }
        }
    }
}

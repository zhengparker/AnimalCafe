using AnimalCafe.UI.Foundation;
using NUnit.Framework;
using UnityEngine;

namespace AnimalCafe.Tests.Phase5
{
    public sealed class StrongFrostLeaseDestroyedOwnerTests
    {
        [Test]
        public void Acquire_ReclaimsDestroyedBehaviourOwnerWithoutMissingReferenceException()
        {
            var lease = new StrongFrostLease(true);
            var destroyedOwner = new GameObject("Destroyed Strong Frost Owner").AddComponent<CanvasGroup>();
            lease.Acquire(destroyedOwner);
            Object.DestroyImmediate(destroyedOwner.gameObject);
            var nextOwner = new GameObject("Next Strong Frost Owner");
            try
            {
                var handle = lease.Acquire(nextOwner);
                Assert.That(handle.ResolvedStyle, Is.EqualTo(UiPanelStyle.StrongFrost));
                handle.Dispose();
            }
            finally { Object.DestroyImmediate(nextOwner); }
        }
    }
}

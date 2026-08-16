using AnimalCafe.UI.Foundation;
using NUnit.Framework;

namespace AnimalCafe.Tests.Phase5
{
    public sealed class UiTransitionAndStrongFrostTests
    {
        [Test]
        public void AT030_ReducedMotionEnabled_SkipsNonEssentialTransition()
        {
            var runner = new UiTransitionRunner(() => true);

            var duration = runner.ResolveDuration(0.22f, isEssential: false);

            Assert.That(duration, Is.Zero);
        }

        [Test]
        public void AT031_SecondStrongFrostRequest_UsesLightFallbackUntilFirstOwnerReleases()
        {
            var leases = new StrongFrostLease(isStrongFrostSupported: true);
            var firstOwner = new object();
            var secondOwner = new object();

            var first = leases.Acquire(firstOwner);
            var second = leases.Acquire(secondOwner);

            Assert.That(first.ResolvedStyle, Is.EqualTo(UiPanelStyle.StrongFrost));
            Assert.That(second.ResolvedStyle, Is.EqualTo(UiPanelStyle.LightFrost));

            first.Dispose();
            var secondRetry = leases.Acquire(secondOwner);

            Assert.That(secondRetry.ResolvedStyle, Is.EqualTo(UiPanelStyle.StrongFrost));

            second.Dispose();
            secondRetry.Dispose();
        }

        [Test]
        public void AT032_StrongFrostUnsupported_AlwaysUsesReadableLightFallback()
        {
            var leases = new StrongFrostLease(isStrongFrostSupported: false);

            var result = leases.Acquire(new object());

            Assert.That(result.ResolvedStyle, Is.EqualTo(UiPanelStyle.LightFrost));

            result.Dispose();
        }
    }
}

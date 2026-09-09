using AnimalCafe.Decoration;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class Phase8ControllerContractTests
    {
        [TestCase(FunctionalSurfacePlacementFailureReason.MissingDefinition)]
        [TestCase(FunctionalSurfacePlacementFailureReason.UnsupportedFunction)]
        [TestCase(FunctionalSurfacePlacementFailureReason.UnsupportedPlacementSurface)]
        [TestCase(FunctionalSurfacePlacementFailureReason.MissingSupportFurniture)]
        [TestCase(FunctionalSurfacePlacementFailureReason.MissingSurfaceSlot)]
        [TestCase(FunctionalSurfacePlacementFailureReason.SlotOccupied)]
        [TestCase(FunctionalSurfacePlacementFailureReason.InstanceAlreadyPlaced)]
        [TestCase(FunctionalSurfacePlacementFailureReason.InstanceNotFound)]
        [TestCase(FunctionalSurfacePlacementFailureReason.InvalidRotation)]
        [TestCase(FunctionalSurfacePlacementFailureReason.InvalidInstance)]
        [TestCase(FunctionalSurfacePlacementFailureReason.InvalidSurfaceSlotAddress)]
        [TestCase(FunctionalSurfacePlacementFailureReason.NoValidInteractionAnchor)]
        [TestCase(FunctionalSurfacePlacementFailureReason.UnsupportedAction)]
        public void Feedback_EveryStableFunctionalFailureHasPlayerFacingChineseText(
            FunctionalSurfacePlacementFailureReason reason)
        {
            var message = PlacementFeedbackMapper.GetPlayerMessage(
                FunctionalSurfacePlacementResult.Failure(reason),
                new[] { "support.1", "pickup.1" });

            Assert.That(message, Is.Not.Null.And.Not.Empty);
            Assert.That(message, Does.Not.Contain("support.1").And.Not.Contain("pickup.1"),
                "Raw diagnostic IDs must stay out of player-facing messages.");
            Assert.That(message, Does.Match("[\\u4e00-\\u9fff]"));
        }

        [Test]
        public void Feedback_NoValidPickUpAnchorUsesSpecificPlayerFacingReason()
        {
            var result = FunctionalSurfacePlacementResult.Failure(
                FunctionalSurfacePlacementFailureReason.NoValidInteractionAnchor);

            var message = PlacementFeedbackMapper.GetPlayerMessage(result);

            Assert.That(PlacementFeedbackMapper.Map(result),
                Is.EqualTo(PlacementFeedbackKey.NoValidInteractionAnchor));
            Assert.That(message, Is.EqualTo("取餐点周围没有可用的互动位置"));
        }

        [TestCase(LayoutReadinessFailureCode.MissingCashRegister)]
        [TestCase(LayoutReadinessFailureCode.MissingCoffeeMachine)]
        [TestCase(LayoutReadinessFailureCode.MissingPickUpPoint)]
        [TestCase(LayoutReadinessFailureCode.MissingSupportFurniture)]
        [TestCase(LayoutReadinessFailureCode.MissingSurfaceSlot)]
        [TestCase(LayoutReadinessFailureCode.DuplicateSurfaceOccupancy)]
        [TestCase(LayoutReadinessFailureCode.AnchorOutOfBounds)]
        [TestCase(LayoutReadinessFailureCode.AnchorBlocked)]
        [TestCase(LayoutReadinessFailureCode.AnchorUnreachable)]
        [TestCase(LayoutReadinessFailureCode.NoCompleteReachableServiceCombination)]
        public void Feedback_EveryStableReadinessFailureHasConciseChineseText(
            LayoutReadinessFailureCode code)
        {
            var message = PlacementFeedbackMapper.GetPlayerMessage(code, "station.1");

            Assert.That(message, Is.Not.Null.And.Not.Empty);
            Assert.That(message, Does.Not.Contain("station.1"));
            Assert.That(message, Does.Match("[\\u4e00-\\u9fff]"));
        }

    }
}

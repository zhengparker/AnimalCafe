using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Layout;
using NUnit.Framework;

namespace AnimalCafe.Tests.EditMode.Phase8
{
    public sealed class LayoutReadinessEvaluatorTests
    {
        private const string SupportDefinitionId = "furniture.counter.one";
        private const string BlockerDefinitionId = "furniture.blocker";
        private const string CashRegisterDefinitionId = "equipment.cash-register";
        private const string CoffeeMachineDefinitionId = "equipment.coffee-machine";
        private const string DirectionlessDefinitionId = "equipment.cash-register.directionless";
        private const string SlotId = "slot.center";

        private const string CashSupportId = "11111111111111111111111111111111";
        private const string CoffeeSupportId = "22222222222222222222222222222222";
        private const string PickUpSupportId = "33333333333333333333333333333333";
        private const string ExtraSupportId = "44444444444444444444444444444444";
        private const string OtherSupportId = "55555555555555555555555555555555";
        private const string CashInstanceId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string CoffeeInstanceId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string PickUpInstanceId = "cccccccccccccccccccccccccccccccc";
        private const string ExtraCashInstanceId = "dddddddddddddddddddddddddddddddd";
        private const string ExtraCoffeeInstanceId = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
        private const string ExtraPickUpInstanceId = "ffffffffffffffffffffffffffffffff";
        private const string UnmatchedCashInstanceId = "01010101010101010101010101010101";
        private const string WinningCashInstanceId = "02020202020202020202020202020202";
        private const string InvalidCashInstanceId = "03030303030303030303030303030303";

        [Test]
        public void CafeLayout_ReadOnlyQueriesExposeUnlockedAndReservationCells()
        {
            var scenario = CreateScenario();
            scenario.CafeLayout.AddReservation(new LayoutReservation(
                "blocked.query",
                LayoutReservationType.Blocked,
                new GridPosition(3, 3),
                new GridSize(1, 1)));

            Assert.That(
                scenario.CafeLayout.IsInsideUnlockedRegion(new GridPosition(3, 3)),
                Is.True);
            Assert.That(
                scenario.CafeLayout.IsInsideUnlockedRegion(new GridPosition(20, 20)),
                Is.False);
            Assert.That(
                scenario.CafeLayout.HasReservation(
                    new GridPosition(0, 0),
                    LayoutReservationType.EntranceClearance),
                Is.True);
            Assert.That(
                scenario.CafeLayout.HasReservation(
                    new GridPosition(3, 3),
                    LayoutReservationType.Blocked),
                Is.True);
            Assert.That(
                scenario.CafeLayout.HasReservation(
                    new GridPosition(3, 3),
                    LayoutReservationType.EntranceClearance),
                Is.False);
        }

        [Test]
        public void Evaluate_DenseEntranceClearanceFloodFillsWithinStableBudget()
        {
            const int size = 72;
            var scenario = CreateScenario(addDefaultRegion: false, addEntrance: false);
            AddRegion(scenario, "region.stress", 0, 0, size, size);
            scenario.CafeLayout.AddReservation(new LayoutReservation(
                "entrance.stress",
                LayoutReservationType.EntranceClearance,
                new GridPosition(0, 0),
                new GridSize(size, size)));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var report = Evaluate(scenario);
            stopwatch.Stop();

            Assert.That(report.CanOpenForBusiness, Is.False);
            Assert.That(report.Stations, Is.Empty);
            Assert.That(report.Failures.Select(failure => failure.Code),
                Is.EqualTo(new[]
                {
                    LayoutReadinessFailureCode.MissingCashRegister,
                    LayoutReadinessFailureCode.MissingCoffeeMachine,
                    LayoutReadinessFailureCode.MissingPickUpPoint
                }));
            Assert.That(
                stopwatch.Elapsed,
                Is.LessThan(System.TimeSpan.FromSeconds(2)),
                $"Dense Entrance Clearance evaluation took {stopwatch.Elapsed}.");
        }

        [Test]
        public void Evaluate_OneCompleteReachableServiceCombinationCanOpenBusiness()
        {
            var scenario = CreateScenario();
            PlaceCompleteCombination(scenario);

            var report = Evaluate(scenario);

            Assert.That(report.CanOpenForBusiness, Is.True);
            AssertSummary(report.CashRegisters, 1, 1, 0);
            AssertSummary(report.CoffeeMachines, 1, 1, 0);
            AssertSummary(report.PickUpPoints, 1, 1, 0);
            Assert.That(report.Failures, Is.Empty);
            Assert.That(report.Stations.Select(station => station.InstanceId), Is.EqualTo(new[]
            {
                CashInstanceId,
                CoffeeInstanceId,
                PickUpInstanceId
            }));
            Assert.That(report.Stations.All(station => station.IsValid), Is.True);
            var sharedComponent = report.Stations[0].ReachableComponentIds.Single();
            Assert.That(
                report.Stations.All(station =>
                    station.ReachableComponentIds.Contains(sharedComponent)),
                Is.True);
        }

        [Test]
        public void Evaluate_ValidCombinationKeepsInvalidExtraAsWarningAndReturnsAllStationsSorted()
        {
            var scenario = CreateScenario();
            PlaceCompleteCombination(scenario);
            PlaceSupport(scenario, ExtraSupportId, 14, 5);
            Assert.That(scenario.FunctionalLayout.PlaceMounted(new SurfaceMountedInstance(
                ExtraCashInstanceId,
                CashRegisterDefinitionId,
                Address(ExtraSupportId),
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);
            scenario.CafeLayout.AddReservation(new LayoutReservation(
                "blocked.extra-register",
                LayoutReservationType.Blocked,
                new GridPosition(14, 6),
                new GridSize(1, 1)));

            var report = Evaluate(scenario);

            Assert.That(report.CanOpenForBusiness, Is.True);
            AssertSummary(report.CashRegisters, 2, 1, 1);
            Assert.That(report.Stations.Select(station => station.InstanceId), Is.EqualTo(new[]
            {
                CashInstanceId,
                ExtraCashInstanceId,
                CoffeeInstanceId,
                PickUpInstanceId
            }));
            var extra = report.Stations.Single(
                station => station.InstanceId == ExtraCashInstanceId);
            Assert.That(extra.IsValid, Is.False);
            Assert.That(extra.Failures.Select(failure => failure.Code),
                Is.EqualTo(new[] { LayoutReadinessFailureCode.AnchorBlocked }));
            Assert.That(report.Failures, Has.Count.EqualTo(1));
            Assert.That(report.Failures[0].Severity,
                Is.EqualTo(LayoutReadinessSeverity.Warning));
            Assert.That(report.Failures[0].FunctionType,
                Is.EqualTo(LayoutStationType.CashRegister));
            Assert.That(report.Failures[0].InstanceId,
                Is.EqualTo(ExtraCashInstanceId));
            Assert.That(report.Failures[0].Role,
                Is.EqualTo(InteractionRole.Employee));
            Assert.That(report.Failures[0].Position,
                Is.EqualTo(new GridPosition(14, 6)));
        }

        [TestCase(LayoutReadinessFailureCode.MissingSupportFurniture)]
        [TestCase(LayoutReadinessFailureCode.MissingSurfaceSlot)]
        [TestCase(LayoutReadinessFailureCode.DuplicateSurfaceOccupancy)]
        public void Evaluate_ValidCombinationKeepsCorruptExtraBindingBlocking(
            LayoutReadinessFailureCode expectedCode)
        {
            var scenario = CreateScenario();
            PlaceCompleteCombination(scenario);
            PlaceSupport(scenario, ExtraSupportId, 14, 5);
            var address = expectedCode == LayoutReadinessFailureCode.MissingSupportFurniture
                ? Address(OtherSupportId)
                : expectedCode == LayoutReadinessFailureCode.MissingSurfaceSlot
                    ? new SurfaceSlotAddress(ExtraSupportId, "slot.missing")
                    : Address(ExtraSupportId);
            InjectMounted(scenario.FunctionalLayout, new SurfaceMountedInstance(
                ExtraCashInstanceId,
                CashRegisterDefinitionId,
                address,
                FurnitureRotation.Degrees0));
            if (expectedCode == LayoutReadinessFailureCode.DuplicateSurfaceOccupancy)
            {
                InjectPickUp(scenario.FunctionalLayout, new PickUpPointInstance(
                    ExtraPickUpInstanceId,
                    address));
            }

            var report = Evaluate(scenario);

            Assert.That(report.CanOpenForBusiness, Is.False);
            Assert.That(report.CashRegisters.ValidCount, Is.EqualTo(1));
            Assert.That(report.CoffeeMachines.ValidCount, Is.EqualTo(1));
            Assert.That(report.PickUpPoints.ValidCount, Is.EqualTo(1));
            var failure = report.Failures.Single(item =>
                item.InstanceId == ExtraCashInstanceId && item.Code == expectedCode);
            Assert.That(failure.Severity, Is.EqualTo(LayoutReadinessSeverity.Blocking));
            Assert.That(failure.SupportFurnitureInstanceId,
                Is.EqualTo(address.SupportFurnitureInstanceId));
            Assert.That(failure.SurfaceSlotId, Is.EqualTo(address.SlotId));
        }

        [Test]
        public void Evaluate_CorruptExtraDoesNotUpgradeStructurallyValidUnavailableExtraToBlocking()
        {
            var scenario = CreateScenario();
            PlaceCompleteCombination(scenario);
            InjectMounted(scenario.FunctionalLayout, new SurfaceMountedInstance(
                ExtraCashInstanceId,
                CashRegisterDefinitionId,
                Address(OtherSupportId),
                FurnitureRotation.Degrees0));
            PlaceSupport(scenario, ExtraSupportId, 14, 5);
            PlaceMounted(scenario, ExtraCoffeeInstanceId,
                CoffeeMachineDefinitionId, ExtraSupportId);
            scenario.CafeLayout.AddReservation(new LayoutReservation(
                "blocked.extra-coffee",
                LayoutReservationType.Blocked,
                new GridPosition(14, 6),
                new GridSize(1, 1)));

            var report = Evaluate(scenario);

            Assert.That(report.CanOpenForBusiness, Is.False);
            Assert.That(report.Failures.Single(item =>
                    item.InstanceId == ExtraCashInstanceId).Severity,
                Is.EqualTo(LayoutReadinessSeverity.Blocking));
            var unavailable = report.Failures.Single(item =>
                item.InstanceId == ExtraCoffeeInstanceId);
            Assert.That(unavailable.Code, Is.EqualTo(LayoutReadinessFailureCode.AnchorBlocked));
            Assert.That(unavailable.Severity, Is.EqualTo(LayoutReadinessSeverity.Warning));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Evaluate_MountedDefinitionMissingDirectionCatalogEntryReportsBlockingFailure(
            bool hasCompleteCombination)
        {
            var scenario = CreateScenario();
            if (hasCompleteCombination)
            {
                PlaceCompleteCombination(scenario);
            }

            PlaceSupport(scenario, ExtraSupportId, 14, 5);
            PlaceMounted(scenario, ExtraCashInstanceId,
                DirectionlessDefinitionId, ExtraSupportId);

            var report = Evaluate(scenario);

            Assert.That(report.Failures.Count(item => item.InstanceId == ExtraCashInstanceId),
                Is.EqualTo(1), "A confirmed mounted instance must not disappear from readiness diagnostics.");
            var failure = report.Failures.Single(item => item.InstanceId == ExtraCashInstanceId);
            Assert.That(failure.Severity, Is.EqualTo(LayoutReadinessSeverity.Blocking));
            Assert.That(failure.SupportFurnitureInstanceId, Is.EqualTo(ExtraSupportId));
            Assert.That(failure.SurfaceSlotId, Is.EqualTo(SlotId));
            Assert.That(failure.Message, Does.Contain(DirectionlessDefinitionId));
            Assert.That(failure.Code, Is.EqualTo(LayoutReadinessFailureCode.MissingFunctionalDirection));
            Assert.That(failure.FunctionType, Is.EqualTo(LayoutStationType.CashRegister));
            var station = report.Stations.Single(item => item.InstanceId == ExtraCashInstanceId);
            Assert.That(station.IsValid, Is.False);
            Assert.That(station.Failures, Does.Contain(failure));
            AssertSummary(report.CashRegisters,
                hasCompleteCombination ? 2 : 1,
                hasCompleteCombination ? 1 : 0,
                1);
            Assert.That(report.CanOpenForBusiness, Is.False);
        }

        [TestCase("equipment.unknown")]
        [TestCase(SupportDefinitionId)]
        public void Evaluate_CorruptMountedDefinitionReportsBlockingFailureWithoutInventingStationType(
            string definitionId)
        {
            var scenario = CreateScenario();
            PlaceCompleteCombination(scenario);
            PlaceSupport(scenario, ExtraSupportId, 14, 5);
            InjectMounted(scenario.FunctionalLayout, new SurfaceMountedInstance(
                ExtraCashInstanceId,
                definitionId,
                Address(ExtraSupportId),
                FurnitureRotation.Degrees0));

            var report = Evaluate(scenario);

            Assert.That(report.Failures.Count(item => item.InstanceId == ExtraCashInstanceId),
                Is.EqualTo(1));
            var failure = report.Failures.Single(item => item.InstanceId == ExtraCashInstanceId);
            Assert.That(failure.FunctionType, Is.Null);
            Assert.That(failure.Severity, Is.EqualTo(LayoutReadinessSeverity.Blocking));
            Assert.That(failure.SupportFurnitureInstanceId, Is.EqualTo(ExtraSupportId));
            Assert.That(failure.SurfaceSlotId, Is.EqualTo(SlotId));
            Assert.That(failure.Message, Does.Contain(definitionId));
            Assert.That(failure.Code, Is.EqualTo(LayoutReadinessFailureCode.InvalidFunctionalDefinition));
            Assert.That(report.Stations, Has.Count.EqualTo(3));
            Assert.That(report.CanOpenForBusiness, Is.False);
        }

        [Test]
        public void Evaluate_LaterSameTypeStationCanWinWhileEarlierUnmatchedRemainsValidAndInvalidExtraWarns()
        {
            const string unmatchedSupportId = "66666666666666666666666666666666";
            const string winningSupportId = "77777777777777777777777777777777";
            const string invalidSupportId = "88888888888888888888888888888888";
            var scenario = CreateScenario(addDefaultRegion: false, addEntrance: false);
            AddRegion(scenario, "region.unmatched", 0, 0, 8, 8);
            AddRegion(scenario, "region.winning", 20, 0, 20, 10);
            AddEntrance(scenario, "entrance.unmatched", 0, 0);
            AddEntrance(scenario, "entrance.winning", 20, 0);

            PlaceSupport(scenario, unmatchedSupportId, 3, 3);
            PlaceMounted(
                scenario,
                UnmatchedCashInstanceId,
                CashRegisterDefinitionId,
                unmatchedSupportId);
            PlaceSupport(scenario, winningSupportId, 23, 4);
            PlaceMounted(
                scenario,
                WinningCashInstanceId,
                CashRegisterDefinitionId,
                winningSupportId);
            PlaceSupport(scenario, CoffeeSupportId, 27, 4);
            PlaceMounted(
                scenario,
                CoffeeInstanceId,
                CoffeeMachineDefinitionId,
                CoffeeSupportId);
            PlaceSupport(scenario, PickUpSupportId, 31, 4);
            PlacePickUp(scenario, PickUpInstanceId, PickUpSupportId);
            PlaceSupport(scenario, invalidSupportId, 35, 4);
            PlaceMounted(
                scenario,
                InvalidCashInstanceId,
                CashRegisterDefinitionId,
                invalidSupportId);
            PlaceBlocker(scenario, "99999999999999999999999999999999", 35, 5);

            var report = Evaluate(scenario);

            Assert.That(report.CanOpenForBusiness, Is.True);
            AssertSummary(report.CashRegisters, 3, 2, 1);
            AssertSummary(report.CoffeeMachines, 1, 1, 0);
            AssertSummary(report.PickUpPoints, 1, 1, 0);
            Assert.That(report.Stations.Select(station => station.InstanceId), Is.EqualTo(new[]
            {
                UnmatchedCashInstanceId,
                WinningCashInstanceId,
                InvalidCashInstanceId,
                CoffeeInstanceId,
                PickUpInstanceId
            }));

            var unmatched = report.Stations.Single(station =>
                station.InstanceId == UnmatchedCashInstanceId);
            var winning = report.Stations.Single(station =>
                station.InstanceId == WinningCashInstanceId);
            var coffee = report.Stations.Single(station =>
                station.InstanceId == CoffeeInstanceId);
            var pickUp = report.Stations.Single(station =>
                station.InstanceId == PickUpInstanceId);
            var invalid = report.Stations.Single(station =>
                station.InstanceId == InvalidCashInstanceId);

            Assert.That(unmatched.IsValid, Is.True);
            Assert.That(unmatched.Failures, Is.Empty);
            Assert.That(winning.IsValid, Is.True);
            var winningComponent = winning.ReachableComponentIds.Single();
            Assert.That(unmatched.ReachableComponentIds,
                Has.No.Member(winningComponent));
            Assert.That(coffee.ReachableComponentIds,
                Does.Contain(winningComponent));
            Assert.That(pickUp.ReachableComponentIds,
                Does.Contain(winningComponent));
            Assert.That(invalid.IsValid, Is.False);
            Assert.That(invalid.Failures.Single().Severity,
                Is.EqualTo(LayoutReadinessSeverity.Warning));
            Assert.That(invalid.Failures.Single().Code,
                Is.EqualTo(LayoutReadinessFailureCode.AnchorBlocked));
            Assert.That(report.Failures, Has.Count.EqualTo(1));
        }

        [Test]
        public void Evaluate_CorruptBindingsReturnStableSpecificFailuresWithoutThrowing()
        {
            var scenario = CreateScenario();
            PlaceSupport(scenario, CashSupportId, 2, 5);
            Assert.That(scenario.FunctionalLayout.PlaceMounted(new SurfaceMountedInstance(
                CashInstanceId,
                CashRegisterDefinitionId,
                Address(CashSupportId),
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);

            InjectMounted(scenario.FunctionalLayout, new SurfaceMountedInstance(
                ExtraCashInstanceId,
                CashRegisterDefinitionId,
                Address(OtherSupportId),
                FurnitureRotation.Degrees0));
            InjectMounted(scenario.FunctionalLayout, new SurfaceMountedInstance(
                ExtraCoffeeInstanceId,
                CoffeeMachineDefinitionId,
                new SurfaceSlotAddress(CashSupportId, "slot.missing"),
                FurnitureRotation.Degrees0));
            InjectPickUp(scenario.FunctionalLayout, new PickUpPointInstance(
                ExtraPickUpInstanceId,
                Address(CashSupportId)));

            LayoutReadinessReport report = null;
            Assert.DoesNotThrow(() => report = Evaluate(scenario));

            Assert.That(report, Is.Not.Null);
            Assert.That(report.Stations, Has.Count.EqualTo(4));
            Assert.That(report.Failures.Select(failure => failure.Code),
                Is.EqualTo(new[]
                {
                    LayoutReadinessFailureCode.DuplicateSurfaceOccupancy,
                    LayoutReadinessFailureCode.MissingSupportFurniture,
                    LayoutReadinessFailureCode.MissingSurfaceSlot,
                    LayoutReadinessFailureCode.DuplicateSurfaceOccupancy
                }));
            var missingSupport = report.Failures.Single(
                failure => failure.Code == LayoutReadinessFailureCode.MissingSupportFurniture);
            Assert.That(missingSupport.InstanceId, Is.EqualTo(ExtraCashInstanceId));
            Assert.That(missingSupport.SupportFurnitureInstanceId,
                Is.EqualTo(OtherSupportId));
            Assert.That(missingSupport.SurfaceSlotId, Is.EqualTo(SlotId));
            var missingSlot = report.Failures.Single(
                failure => failure.Code == LayoutReadinessFailureCode.MissingSurfaceSlot);
            Assert.That(missingSlot.SupportFurnitureInstanceId,
                Is.EqualTo(CashSupportId));
            Assert.That(missingSlot.SurfaceSlotId, Is.EqualTo("slot.missing"));
            Assert.That(report.Failures.All(failure =>
                failure.Severity == LayoutReadinessSeverity.Blocking), Is.True);
        }

        [Test]
        public void Evaluate_ClassifiesOutsideBlockedAndUnreachableAnchors()
        {
            var scenario = CreateScenario(addDefaultRegion: false, addEntrance: false);
            AddRegion(scenario, "region.main", 0, 0, 20, 10);
            AddRegion(scenario, "region.isolated", 30, 0, 5, 5);
            AddEntrance(scenario, "entrance.main", 0, 0);

            PlaceSupport(scenario, CashSupportId, 2, 0);
            PlaceMounted(scenario, CashInstanceId, CashRegisterDefinitionId, CashSupportId);

            PlaceSupport(scenario, ExtraSupportId, 6, 5);
            PlaceMounted(scenario, ExtraCashInstanceId, CashRegisterDefinitionId, ExtraSupportId);
            scenario.CafeLayout.AddReservation(new LayoutReservation(
                "blocked.employee",
                LayoutReservationType.Blocked,
                new GridPosition(6, 6),
                new GridSize(1, 1)));

            PlaceSupport(scenario, CoffeeSupportId, 32, 2);
            PlaceMounted(scenario, UnmatchedCashInstanceId, CashRegisterDefinitionId, CoffeeSupportId);

            var report = Evaluate(scenario);

            Assert.That(
                StationFailureCodes(report, CashInstanceId),
                Does.Contain(LayoutReadinessFailureCode.AnchorOutOfBounds));
            Assert.That(
                StationFailureCodes(report, ExtraCashInstanceId),
                Does.Contain(LayoutReadinessFailureCode.AnchorBlocked));
            Assert.That(
                StationFailureCodes(report, UnmatchedCashInstanceId),
                Does.Contain(LayoutReadinessFailureCode.AnchorUnreachable));
            Assert.That(report.Failures.Where(failure => failure.Position.HasValue)
                .Select(failure => failure.Position.Value),
                Does.Contain(new GridPosition(2, -1)));
            Assert.That(report.CanOpenForBusiness, Is.False);
        }

        [Test]
        public void Evaluate_ConfirmedFloorFurnitureOnRequiredAnchorIsBlocked()
        {
            var scenario = CreateScenario();
            PlaceSupport(scenario, CashSupportId, 5, 4);
            PlaceMounted(
                scenario,
                CashInstanceId,
                CashRegisterDefinitionId,
                CashSupportId);
            PlaceBlocker(scenario, "60606060606060606060606060606060", 5, 5);

            var report = Evaluate(scenario);

            Assert.That(
                scenario.CafeLayout.TryGetOccupant(
                    new GridPosition(5, 5),
                    out var occupant),
                Is.True);
            Assert.That(occupant, Is.EqualTo("60606060606060606060606060606060"));
            var failure = report.Stations.Single(station =>
                station.InstanceId == CashInstanceId).Failures.Single(item =>
                item.Code == LayoutReadinessFailureCode.AnchorBlocked);
            Assert.That(failure.Role, Is.EqualTo(InteractionRole.Employee));
            Assert.That(failure.Position, Is.EqualTo(new GridPosition(5, 5)));
        }

        [Test]
        public void Evaluate_ConfirmedFloorFurnitureBarrierMakesAnchorUnreachable()
        {
            var scenario = CreateScenario(addDefaultRegion: false, addEntrance: false);
            AddRegion(scenario, "region.barrier", 0, 0, 10, 8);
            AddEntrance(scenario, "entrance.barrier", 0, 3);
            PlaceSupport(scenario, CoffeeSupportId, 7, 3);
            PlaceMounted(
                scenario,
                CashInstanceId,
                CashRegisterDefinitionId,
                CoffeeSupportId);
            for (var y = 0; y < 8; y++)
            {
                PlaceBlocker(scenario, (100 + y).ToString("x32"), 4, y);
            }

            var report = Evaluate(scenario);

            var station = report.Stations.Single(item =>
                item.InstanceId == CashInstanceId);
            Assert.That(station.IsValid, Is.False);
            Assert.That(station.Failures.Select(failure => failure.Code),
                Is.EqualTo(new[] { LayoutReadinessFailureCode.AnchorUnreachable }));
            var failure = station.Failures.Single();
            Assert.That(failure.Role, Is.EqualTo(InteractionRole.Customer));
            Assert.That(failure.Position, Is.EqualTo(new GridPosition(7, 2)));
        }

        [TestCase(false, true, true, LayoutReadinessFailureCode.MissingCashRegister)]
        [TestCase(true, false, true, LayoutReadinessFailureCode.MissingCoffeeMachine)]
        [TestCase(true, true, false, LayoutReadinessFailureCode.MissingPickUpPoint)]
        public void Evaluate_MissingRequiredCapabilityReturnsItsBlockingFailure(
            bool addCash,
            bool addCoffee,
            bool addPickUp,
            LayoutReadinessFailureCode expectedCode)
        {
            var scenario = CreateScenario();
            if (addCash)
            {
                PlaceSupport(scenario, CashSupportId, 2, 5);
                PlaceMounted(scenario, CashInstanceId, CashRegisterDefinitionId, CashSupportId);
            }

            if (addCoffee)
            {
                PlaceSupport(scenario, CoffeeSupportId, 6, 5);
                PlaceMounted(scenario, CoffeeInstanceId, CoffeeMachineDefinitionId, CoffeeSupportId);
            }

            if (addPickUp)
            {
                PlaceSupport(scenario, PickUpSupportId, 10, 5);
                PlacePickUp(scenario, PickUpInstanceId, PickUpSupportId);
            }

            var report = Evaluate(scenario);

            Assert.That(report.CanOpenForBusiness, Is.False);
            var failure = report.Failures.Single(item => item.Code == expectedCode);
            Assert.That(failure.Severity, Is.EqualTo(LayoutReadinessSeverity.Blocking));
            Assert.That(failure.InstanceId, Is.Null);
            Assert.That(failure.Message, Is.Not.Empty);
        }

        [Test]
        public void Evaluate_AllInstancesOfCapabilityInvalidKeepsEveryStationFailure()
        {
            var scenario = CreateScenario();
            PlaceSupport(scenario, CashSupportId, 2, 5);
            PlaceMounted(scenario, CashInstanceId, CashRegisterDefinitionId, CashSupportId);
            PlaceSupport(scenario, ExtraSupportId, 4, 5);
            PlaceMounted(scenario, ExtraCashInstanceId, CashRegisterDefinitionId, ExtraSupportId);
            PlaceSupport(scenario, CoffeeSupportId, 8, 5);
            PlaceMounted(scenario, CoffeeInstanceId, CoffeeMachineDefinitionId, CoffeeSupportId);
            PlaceSupport(scenario, PickUpSupportId, 12, 5);
            PlacePickUp(scenario, PickUpInstanceId, PickUpSupportId);
            scenario.CafeLayout.AddReservation(new LayoutReservation(
                "blocked.cash.one",
                LayoutReservationType.Blocked,
                new GridPosition(2, 6),
                new GridSize(1, 1)));
            scenario.CafeLayout.AddReservation(new LayoutReservation(
                "blocked.cash.two",
                LayoutReservationType.Blocked,
                new GridPosition(4, 6),
                new GridSize(1, 1)));

            var report = Evaluate(scenario);

            Assert.That(report.CanOpenForBusiness, Is.False);
            AssertSummary(report.CashRegisters, 2, 0, 2);
            Assert.That(report.Failures.Count(failure =>
                failure.Code == LayoutReadinessFailureCode.AnchorBlocked), Is.EqualTo(2));
            Assert.That(report.Failures.Where(failure =>
                failure.Code == LayoutReadinessFailureCode.AnchorBlocked)
                .All(failure => failure.Severity == LayoutReadinessSeverity.Blocking),
                Is.True);
        }

        [Test]
        public void Evaluate_CounterBarrierAllowsSeparateCustomerAndEmployeeNetworks()
        {
            var scenario = CreateDualNetworkScenario();

            var report = Evaluate(scenario);

            Assert.That(report.CanOpenForBusiness, Is.True);
            Assert.That(report.Failures, Is.Empty);
            Assert.That(report.Stations.All(station => station.IsValid), Is.True);
            Assert.That(report.Stations.Single(station => station.InstanceId == CashInstanceId)
                .ReachableComponentIds, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(report.Stations.Single(station => station.InstanceId == CoffeeInstanceId)
                .ReachableComponentIds, Is.EqualTo(new[] { 1 }));
            Assert.That(report.Stations.Single(station => station.InstanceId == PickUpInstanceId)
                .ReachableComponentIds, Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void Evaluate_CustomerAnchorDisconnectedFromEntranceFailsOnlyCustomerReachability()
        {
            var scenario = CreateDualNetworkScenario();
            scenario.CafeLayout.AddReservation(new LayoutReservation(
                "blocked.customer-divider",
                LayoutReservationType.Blocked,
                new GridPosition(4, 0),
                new GridSize(1, 3)));

            var report = Evaluate(scenario);

            Assert.That(report.CanOpenForBusiness, Is.False);
            var pickUp = report.Stations.Single(station => station.InstanceId == PickUpInstanceId);
            Assert.That(pickUp.Failures.Select(failure => failure.Code),
                Is.EqualTo(new[] { LayoutReadinessFailureCode.AnchorUnreachable }));
            Assert.That(pickUp.Failures.Single().Role, Is.EqualTo(InteractionRole.Customer));
            Assert.That(pickUp.Failures.Single().Position, Is.EqualTo(new GridPosition(6, 2)));
            Assert.That(report.Stations.Single(station => station.InstanceId == CoffeeInstanceId)
                .IsValid, Is.True);
        }

        [Test]
        public void Evaluate_EmployeeAnchorsInDifferentWalkableComponentsCannotFormCombination()
        {
            var scenario = CreateDualNetworkScenario();
            scenario.CafeLayout.AddReservation(new LayoutReservation(
                "blocked.employee-divider",
                LayoutReservationType.Blocked,
                new GridPosition(4, 4),
                new GridSize(1, 3)));

            var report = Evaluate(scenario);

            Assert.That(report.Stations.All(station => station.IsValid), Is.True);
            Assert.That(report.CanOpenForBusiness, Is.False);
            Assert.That(report.Failures.Select(failure => failure.Code),
                Is.EqualTo(new[]
                {
                    LayoutReadinessFailureCode.NoCompleteReachableServiceCombination
                }));
        }

        [Test]
        public void Evaluate_DualNetworkCombinationKeepsInvalidExtraStationAsWarning()
        {
            var scenario = CreateDualNetworkScenario(addInvalidExtra: true);
            scenario.CafeLayout.AddReservation(new LayoutReservation(
                "blocked.extra-register-anchor",
                LayoutReservationType.Blocked,
                new GridPosition(7, 4),
                new GridSize(1, 1)));

            var report = Evaluate(scenario);

            Assert.That(report.CanOpenForBusiness, Is.True);
            AssertSummary(report.CashRegisters, 2, 1, 1);
            AssertSummary(report.CoffeeMachines, 1, 1, 0);
            AssertSummary(report.PickUpPoints, 1, 1, 0);
            Assert.That(report.Failures, Has.Count.EqualTo(1));
            var failure = report.Failures.Single();
            Assert.That(failure.Severity, Is.EqualTo(LayoutReadinessSeverity.Warning));
            Assert.That(failure.Code, Is.EqualTo(LayoutReadinessFailureCode.AnchorBlocked));
            Assert.That(failure.InstanceId, Is.EqualTo(ExtraCashInstanceId));
            Assert.That(failure.Role, Is.EqualTo(InteractionRole.Employee));
            Assert.That(failure.Position, Is.EqualTo(new GridPosition(7, 4)));
        }

        [Test]
        public void Evaluate_IndividuallyValidStationsInDisconnectedComponentsCannotOpen()
        {
            var scenario = CreateScenario(addDefaultRegion: false, addEntrance: false);
            AddRegion(scenario, "region.cash", 0, 0, 5, 5);
            AddRegion(scenario, "region.coffee", 10, 0, 5, 5);
            AddRegion(scenario, "region.pickup", 20, 0, 5, 5);
            AddEntrance(scenario, "entrance.cash", 0, 0);
            AddEntrance(scenario, "entrance.coffee", 10, 0);
            AddEntrance(scenario, "entrance.pickup", 20, 0);
            PlaceSupport(scenario, CashSupportId, 2, 2);
            PlaceMounted(scenario, CashInstanceId, CashRegisterDefinitionId, CashSupportId);
            PlaceSupport(scenario, CoffeeSupportId, 12, 2);
            PlaceMounted(scenario, CoffeeInstanceId, CoffeeMachineDefinitionId, CoffeeSupportId);
            PlaceSupport(scenario, PickUpSupportId, 22, 2);
            PlacePickUp(scenario, PickUpInstanceId, PickUpSupportId);

            var report = Evaluate(scenario);

            Assert.That(report.Stations.All(station => station.IsValid), Is.True);
            AssertSummary(report.CashRegisters, 1, 1, 0);
            AssertSummary(report.CoffeeMachines, 1, 1, 0);
            AssertSummary(report.PickUpPoints, 1, 1, 0);
            Assert.That(report.CanOpenForBusiness, Is.False);
            var failure = report.Failures.Single(item =>
                item.Code == LayoutReadinessFailureCode.NoCompleteReachableServiceCombination);
            Assert.That(failure.Severity, Is.EqualTo(LayoutReadinessSeverity.Blocking));
        }

        [Test]
        public void Evaluate_LayoutChangeProducesFreshReadinessInsteadOfReusingOldResult()
        {
            var scenario = CreateScenario(addDefaultRegion: false, addEntrance: false);
            AddRegion(scenario, "region.main", 0, 0, 16, 10);
            AddRegion(scenario, "region.isolated", 20, 0, 10, 10);
            AddEntrance(scenario, "entrance.main", 0, 0);
            PlaceCompleteCombination(scenario);
            var evaluator = new LayoutReadinessEvaluator();
            var before = evaluator.Evaluate(
                scenario.CafeLayout,
                scenario.FunctionalLayout,
                scenario.Slots,
                scenario.Directions);
            Assert.That(before.CanOpenForBusiness, Is.True);

            Assert.That(
                scenario.CafeLayout.MoveFurniture(
                    CoffeeSupportId,
                    new GridPosition(24, 5)).Succeeded,
                Is.True);

            var after = evaluator.Evaluate(
                scenario.CafeLayout,
                scenario.FunctionalLayout,
                scenario.Slots,
                scenario.Directions);

            Assert.That(after, Is.Not.SameAs(before));
            Assert.That(before.CanOpenForBusiness, Is.True);
            Assert.That(after.CanOpenForBusiness, Is.False);
            Assert.That(after.Stations.Single(station =>
                station.InstanceId == CoffeeInstanceId).IsValid, Is.True);
            Assert.That(after.Failures.Select(failure => failure.Code),
                Is.EqualTo(new[]
                {
                    LayoutReadinessFailureCode.NoCompleteReachableServiceCombination
                }));
        }

        private static Scenario CreateScenario(
            bool addDefaultRegion = true,
            bool addEntrance = true)
        {
            var definitions = new FurnitureDefinitionCatalog(new[]
            {
                new FurnitureDefinition(
                    SupportDefinitionId,
                    "One-cell Counter",
                    new GridSize(1, 1),
                    PlacementSurfaceType.Floor),
                new FurnitureDefinition(
                    BlockerDefinitionId,
                    "Blocker",
                    new GridSize(1, 1),
                    PlacementSurfaceType.Floor),
                new FurnitureDefinition(
                    CashRegisterDefinitionId,
                    "Cash Register",
                    new GridSize(1, 1),
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CashRegister),
                new FurnitureDefinition(
                    CoffeeMachineDefinitionId,
                    "Coffee Machine",
                    new GridSize(1, 1),
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CoffeeMachine),
                new FurnitureDefinition(
                    DirectionlessDefinitionId,
                    "Directionless Cash Register",
                    new GridSize(1, 1),
                    PlacementSurfaceType.FurnitureSurface,
                    FurnitureFunctionType.CashRegister)
            });
            var cafeLayout = new CafeLayout(new GridSettings(1f), definitions);
            var slots = new SurfaceSlotCatalog(new[]
            {
                new SurfaceSlotDefinition(
                    SupportDefinitionId,
                    SlotId,
                    new GridPosition(0, 0))
            });
            var directions = new FunctionalDirectionCatalog(
                new Dictionary<string, CashRegisterSides>
                {
                    [CashRegisterDefinitionId] = new CashRegisterSides(
                        CardinalDirection.North,
                        CardinalDirection.South)
                },
                new Dictionary<string, CardinalDirection>
                {
                    [CoffeeMachineDefinitionId] = CardinalDirection.North
                });
            var scenario = new Scenario(
                cafeLayout,
                new FunctionalSurfaceLayout(cafeLayout, definitions, slots),
                slots,
                directions);

            if (addDefaultRegion)
            {
                AddRegion(scenario, "region.main", 0, 0, 20, 12);
            }

            if (addEntrance)
            {
                AddEntrance(scenario, "entrance.main", 0, 0);
            }

            return scenario;
        }

        private static Scenario CreateDualNetworkScenario(bool addInvalidExtra = false)
        {
            var scenario = CreateScenario(addDefaultRegion: false, addEntrance: false);
            AddRegion(scenario, "region.dual-network", 0, 0, 9, 7);
            AddEntrance(scenario, "entrance.dual-network", 0, 0);

            for (var x = 0; x < 9; x++)
            {
                if (x == 2 || x == 3 || x == 6 || (addInvalidExtra && x == 7))
                {
                    continue;
                }

                PlaceBlocker(scenario, (700 + x).ToString("x32"), x, 3);
            }

            PlaceSupport(scenario, CashSupportId, 2, 3);
            PlaceMounted(scenario, CashInstanceId, CashRegisterDefinitionId, CashSupportId);
            PlaceSupport(scenario, CoffeeSupportId, 3, 3);
            PlaceMounted(scenario, CoffeeInstanceId, CoffeeMachineDefinitionId, CoffeeSupportId);
            PlaceSupport(scenario, PickUpSupportId, 6, 3);
            PlacePickUp(scenario, PickUpInstanceId, PickUpSupportId);

            if (addInvalidExtra)
            {
                PlaceSupport(scenario, ExtraSupportId, 7, 3);
                PlaceMounted(
                    scenario,
                    ExtraCashInstanceId,
                    CashRegisterDefinitionId,
                    ExtraSupportId);
            }

            return scenario;
        }

        private static void PlaceCompleteCombination(Scenario scenario)
        {
            PlaceSupport(scenario, CashSupportId, 2, 5);
            PlaceMounted(scenario, CashInstanceId, CashRegisterDefinitionId, CashSupportId);
            PlaceSupport(scenario, CoffeeSupportId, 6, 5);
            PlaceMounted(scenario, CoffeeInstanceId, CoffeeMachineDefinitionId, CoffeeSupportId);
            PlaceSupport(scenario, PickUpSupportId, 10, 5);
            PlacePickUp(scenario, PickUpInstanceId, PickUpSupportId);
        }

        private static void AddRegion(
            Scenario scenario,
            string id,
            int x,
            int y,
            int width,
            int height)
        {
            scenario.CafeLayout.AddRegion(new LayoutRegion(
                id,
                new GridPosition(x, y),
                new GridSize(width, height),
                LayoutZoneType.Interior));
        }

        private static void AddEntrance(
            Scenario scenario,
            string id,
            int x,
            int y)
        {
            scenario.CafeLayout.AddReservation(new LayoutReservation(
                id,
                LayoutReservationType.EntranceClearance,
                new GridPosition(x, y),
                new GridSize(1, 1)));
        }

        private static void PlaceSupport(
            Scenario scenario,
            string instanceId,
            int x,
            int y)
        {
            Assert.That(scenario.CafeLayout.PlaceFurniture(FurnitureInstance.Restore(
                instanceId,
                SupportDefinitionId,
                new GridPosition(x, y),
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);
        }

        private static void PlaceBlocker(
            Scenario scenario,
            string instanceId,
            int x,
            int y)
        {
            Assert.That(scenario.CafeLayout.PlaceFurniture(FurnitureInstance.Restore(
                instanceId,
                BlockerDefinitionId,
                new GridPosition(x, y),
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);
        }

        private static void PlaceMounted(
            Scenario scenario,
            string instanceId,
            string definitionId,
            string supportId)
        {
            Assert.That(scenario.FunctionalLayout.PlaceMounted(new SurfaceMountedInstance(
                instanceId,
                definitionId,
                Address(supportId),
                FurnitureRotation.Degrees0)).Succeeded,
                Is.True);
        }

        private static void PlacePickUp(
            Scenario scenario,
            string instanceId,
            string supportId)
        {
            Assert.That(scenario.FunctionalLayout.PlacePickUp(new PickUpPointInstance(
                instanceId,
                Address(supportId))).Succeeded,
                Is.True);
        }

        private static SurfaceSlotAddress Address(string supportId)
        {
            return new SurfaceSlotAddress(supportId, SlotId);
        }

        private static LayoutReadinessReport Evaluate(Scenario scenario)
        {
            return new LayoutReadinessEvaluator().Evaluate(
                scenario.CafeLayout,
                scenario.FunctionalLayout,
                scenario.Slots,
                scenario.Directions);
        }

        private static IReadOnlyList<LayoutReadinessFailureCode> StationFailureCodes(
            LayoutReadinessReport report,
            string instanceId)
        {
            return report.Stations.Single(station => station.InstanceId == instanceId)
                .Failures.Select(failure => failure.Code).ToArray();
        }

        private static void AssertSummary(
            LayoutReadinessSummary summary,
            int total,
            int valid,
            int invalid)
        {
            Assert.That(summary.TotalCount, Is.EqualTo(total));
            Assert.That(summary.ValidCount, Is.EqualTo(valid));
            Assert.That(summary.InvalidCount, Is.EqualTo(invalid));
        }

        private static void InjectMounted(
            FunctionalSurfaceLayout layout,
            SurfaceMountedInstance instance)
        {
            var field = typeof(FunctionalSurfaceLayout).GetField(
                "mountedInstances",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var list = (List<SurfaceMountedInstance>)field.GetValue(layout);
            list.Add(instance);
        }

        private static void InjectPickUp(
            FunctionalSurfaceLayout layout,
            PickUpPointInstance instance)
        {
            var field = typeof(FunctionalSurfaceLayout).GetField(
                "pickUpPoints",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var list = (List<PickUpPointInstance>)field.GetValue(layout);
            list.Add(instance);
        }

        private sealed class Scenario
        {
            public CafeLayout CafeLayout { get; }
            public FunctionalSurfaceLayout FunctionalLayout { get; }
            public SurfaceSlotCatalog Slots { get; }
            public FunctionalDirectionCatalog Directions { get; }

            public Scenario(
                CafeLayout cafeLayout,
                FunctionalSurfaceLayout functionalLayout,
                SurfaceSlotCatalog slots,
                FunctionalDirectionCatalog directions)
            {
                CafeLayout = cafeLayout;
                FunctionalLayout = functionalLayout;
                Slots = slots;
                Directions = directions;
            }
        }
    }
}

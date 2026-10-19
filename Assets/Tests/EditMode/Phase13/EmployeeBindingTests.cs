using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Content;
using AnimalCafe.Employees;
using AnimalCafe.Layout;
using AnimalCafe.Navigation;
using NUnit.Framework;
using UnityEngine;

namespace AnimalCafe.Tests.EditMode.Phase13
{
    public sealed class EmployeeBindingTests
    {
        private const string CashA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa1";
        private const string CashB = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa2";
        private const string CoffeeA = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb1";
        private const string CoffeeB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb2";
        private const string PickupA = "ccccccccccccccccccccccccccccccc1";
        private const string PickupB = "ccccccccccccccccccccccccccccccc2";
        private const string Counter = "furniture.counter";
        private const string CashDefinition = "equipment.cash-register";
        private const string CoffeeDefinition = "equipment.coffee-machine";
        private const string Slot = "slot.center";

        [TestCase(false)]
        [TestCase(true)]
        public void SelectedRegisterWinsRegardlessOfEnumerationOrder(bool reverse)
        {
            var report = new Scenario().Report(reverse);
            Assert.That(Resolve(EmployeeRole.Cashier, report, CashB, (a, b) => true,
                out var binding, out var reason), Is.True, reason);
            Assert.That(binding.WorkStationId, Is.EqualTo(CashB));
            Assert.That(binding.Role, Is.EqualTo(EmployeeRole.Cashier));
            Assert.That(binding.LayoutRevision, Is.EqualTo(7));
            Assert.That(binding.WorkTarget.Position, Is.EqualTo(new Vector3(4, 0, 6)));
            Assert.That(binding.PickUpId, Is.Null.Or.Empty);
            Assert.That(binding.PickUpTarget, Is.Null);
            Assert.That(binding.CupAddress, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EmptySelectedRegisterUsesP12OrdinalChoice(bool reverse)
        {
            Assert.That(Resolve(EmployeeRole.Cashier, new Scenario().Report(reverse), "",
                (a, b) => true, out var binding, out var reason), Is.True, reason);
            Assert.That(binding.WorkStationId, Is.EqualTo(CashA));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BaristaChoosesFirstReachablePair(bool reverse)
        {
            // 首台 CM 到首个 Pick-up 无完整路径；不能直接使用首候选或跳过该 CM。
            bool Reach(Vector3 from, Vector3 to) =>
                !(from.x == 6 && to.x == 10);
            Assert.That(Resolve(EmployeeRole.Barista, new Scenario().Report(reverse), CashB,
                Reach, out var binding, out var reason), Is.True, reason);
            Assert.That(binding.WorkStationId, Is.EqualTo(CoffeeA));
            Assert.That(binding.PickUpId, Is.EqualTo(PickupB));
            Assert.That(binding.LayoutRevision, Is.EqualTo(7));
            Assert.That(binding.WorkTarget.Position, Is.EqualTo(new Vector3(6, 0, 6)));
            Assert.That(binding.PickUpTarget.Value.Position, Is.EqualTo(new Vector3(12, 0, 6)));
            Assert.That(binding.CupAddress.Value, Is.EqualTo(new SurfaceSlotAddress(Scenario.SupportId(12), Slot)));
        }

        [Test]
        public void BaristaSkipsCoffeeMachineWhenNoPickupHasCompleteRoundTrip()
        {
            Assert.That(Resolve(EmployeeRole.Barista, new Scenario().Report(true), "",
                (a, b) => a.x != 6 && b.x != 6, out var binding, out var reason), Is.True, reason);
            Assert.That(binding.WorkStationId, Is.EqualTo(CoffeeB));
            Assert.That(binding.PickUpId, Is.EqualTo(PickupA));
        }

        [Test]
        public void ReturnPathMustAlsoBeComplete()
        {
            Assert.That(Resolve(EmployeeRole.Barista, new Scenario().Report(), "",
                (a, b) => a.x < 10, out var binding, out var reason), Is.False);
            Assert.That(binding, Is.Null);
            Assert.That(reason, Is.Not.Empty);
        }

        [Test]
        public void DeletedPickupRepresentationCannotKeepBindingAlive()
        {
            var scenario = new Scenario();
            Assert.That(Resolve(EmployeeRole.Barista, scenario.Report(), "", (a, b) => true,
                out var oldBinding, out _), Is.True);
            var representation = new GameObject("Stale Pick-up Representation");
            try
            {
                Assert.That(scenario.Functional.RemovePickUp(PickupA).Succeeded, Is.True);
                Assert.That(scenario.Functional.RemovePickUp(PickupB).Succeeded, Is.True);
                var missingStationAccepted = Resolve(EmployeeRole.Barista, scenario.Report(), "",
                    (a, b) => true, out var binding, out var reason);
                Assert.That(missingStationAccepted, Is.False);
                Assert.That(binding, Is.Null);
                Assert.That(reason, Is.Not.Empty);
                Assert.That(oldBinding.PickUpId, Is.EqualTo(PickupA));
                Assert.That(representation, Is.Not.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(representation); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrInvalidSelectedRegisterNeverFallsBackToAnotherRegister(bool invalid)
        {
            var scenario = new Scenario();
            if (invalid) scenario.BlockEmployee(4);
            Assert.That(Resolve(EmployeeRole.Cashier, scenario.Report(), invalid ? CashB : PickupA,
                (a, b) => true, out var binding, out var reason), Is.False);
            Assert.That(binding, Is.Null);
            Assert.That(reason, Is.Not.Empty);
        }

        [Test]
        public void InvalidStationIsSkippedEvenWhenPathQueryAcceptsItsPosition()
        {
            var scenario = new Scenario();
            scenario.BlockEmployee(6);
            Assert.That(Resolve(EmployeeRole.Barista, scenario.Report(), "", (a, b) => true,
                out var binding, out var reason), Is.True, reason);
            Assert.That(binding.WorkStationId, Is.EqualTo(CoffeeB));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void InvalidResolverInputReturnsExplicitFailure(int invalid)
        {
            var accepted = new EmployeeStationBindingResolver().TryResolve(
                invalid == 0 ? (EmployeeRole)99 : EmployeeRole.Cashier,
                invalid == 1 ? null : new Scenario().Report(), "", invalid == 2 ? -1 : 7,
                invalid == 3 ? null : ToTarget,
                invalid == 4 ? null : (Func<Vector3, Vector3, bool>)((a, b) => true),
                out var binding, out var reason);
            Assert.That(accepted, Is.False);
            Assert.That(binding, Is.Null);
            Assert.That(reason, Is.Not.Empty);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void BindingRejectsIncompleteRoleContract(int invalid)
        {
            Assert.Catch<ArgumentException>(() => new EmployeeStationBinding(
                invalid == 0 ? (EmployeeRole)99 : EmployeeRole.Barista,
                invalid == 1 ? "" : CoffeeA,
                invalid == 2 ? "" : PickupA,
                new NavigationTarget(Vector3.zero),
                invalid == 3 ? null : new NavigationTarget(Vector3.one),
                invalid == 4 ? default(SurfaceSlotAddress) : new SurfaceSlotAddress(Scenario.SupportId(10), Slot),
                invalid == 5 ? -1 : 7));
        }

        [Test]
        public void CashierBindingRejectsPickupFields()
        {
            Assert.Catch<ArgumentException>(() => new EmployeeStationBinding(EmployeeRole.Cashier,
                CashA, PickupA, new NavigationTarget(Vector3.zero), new NavigationTarget(Vector3.one),
                new SurfaceSlotAddress(Scenario.SupportId(10), Slot), 7));
        }

        private static bool Resolve(EmployeeRole role, LayoutReadinessReport report, string selected,
            Func<Vector3, Vector3, bool> canReach, out EmployeeStationBinding binding, out string reason)
            => new EmployeeStationBindingResolver().TryResolve(role, report, selected, 7,
                ToTarget, canReach, out binding, out reason);

        private static NavigationTarget ToTarget(InteractionAnchor anchor)
            => new NavigationTarget(new Vector3(anchor.Position.X, 0, anchor.Position.Y));

        private sealed class Scenario
        {
            private readonly CafeLayout layout;
            private readonly SurfaceSlotCatalog slots;
            private readonly FunctionalDirectionCatalog directions;
            public FunctionalSurfaceLayout Functional { get; }
            public static string SupportId(int x) => x.ToString("x32");

            public Scenario()
            {
                var definitions = new FurnitureDefinitionCatalog(new[]
                {
                    new FurnitureDefinition(Counter, "Counter", new GridSize(1, 1), PlacementSurfaceType.Floor),
                    new FurnitureDefinition(CashDefinition, "Register", new GridSize(1, 1),
                        PlacementSurfaceType.FurnitureSurface, FurnitureFunctionType.CashRegister),
                    new FurnitureDefinition(CoffeeDefinition, "Coffee", new GridSize(1, 1),
                        PlacementSurfaceType.FurnitureSurface, FurnitureFunctionType.CoffeeMachine)
                });
                layout = new CafeLayout(new GridSettings(1), definitions);
                layout.AddRegion(new LayoutRegion("region.main", new GridPosition(0, 0),
                    new GridSize(20, 12), LayoutZoneType.Interior));
                layout.AddReservation(new LayoutReservation("entrance", LayoutReservationType.EntranceClearance,
                    new GridPosition(0, 0), new GridSize(1, 1)));
                slots = new SurfaceSlotCatalog(new[] { new SurfaceSlotDefinition(Counter, Slot, new GridPosition(0, 0)) });
                directions = new FunctionalDirectionCatalog(
                    new Dictionary<string, CashRegisterSides> { [CashDefinition] = new CashRegisterSides(CardinalDirection.North, CardinalDirection.South) },
                    new Dictionary<string, CardinalDirection> { [CoffeeDefinition] = CardinalDirection.North });
                Functional = new FunctionalSurfaceLayout(layout, definitions, slots);
                // 正式布局也倒序插入；report 再分别正序/倒序传入 resolver。
                Add(12, PickupB, null); Add(10, PickupA, null);
                Add(8, CoffeeB, CoffeeDefinition); Add(6, CoffeeA, CoffeeDefinition);
                Add(4, CashB, CashDefinition); Add(2, CashA, CashDefinition);
            }

            public void BlockEmployee(int x) => layout.AddReservation(new LayoutReservation(
                "blocked." + x, LayoutReservationType.Blocked, new GridPosition(x, 6), new GridSize(1, 1)));

            public LayoutReadinessReport Report(bool reverse = false)
            {
                var report = new LayoutReadinessEvaluator().Evaluate(layout, Functional, slots, directions);
                if (!reverse) return report;
                // 只倒转 immutable report 的枚举顺序，站位数据仍来自真实 evaluator。
                return (LayoutReadinessReport)Activator.CreateInstance(typeof(LayoutReadinessReport),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[]
                    {
                        report.CanOpenForBusiness, report.Stations.Reverse(), report.Failures,
                        report.CashRegisters, report.CoffeeMachines, report.PickUpPoints
                    }, null);
            }

            private void Add(int x, string id, string definition)
            {
                Assert.That(layout.PlaceFurniture(FurnitureInstance.Restore(SupportId(x), Counter,
                    new GridPosition(x, 5), FurnitureRotation.Degrees0)).Succeeded, Is.True);
                var address = new SurfaceSlotAddress(SupportId(x), Slot);
                Assert.That(definition == null
                    ? Functional.PlacePickUp(new PickUpPointInstance(id, address)).Succeeded
                    : Functional.PlaceMounted(new SurfaceMountedInstance(id, definition, address, FurnitureRotation.Degrees0)).Succeeded,
                    Is.True);
            }
        }
    }
}

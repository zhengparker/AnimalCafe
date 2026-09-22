using System;
using System.Linq;
using System.Reflection;
using AnimalCafe.Layout;
using AnimalCafe.UI.P8R;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnimalCafe.Tests.EditMode.P8R
{
    public sealed class P8RReadinessCopyTests
    {
        private P8RAppearance appearance;

        [SetUp]
        public void LoadAppearance()
        {
            appearance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<P8RAppearance>(
                "Assets/UI/P8R/P8RAppearance.asset"));
        }

        [TearDown]
        public void Cleanup() => Object.DestroyImmediate(appearance);

        [Test]
        public void Summary_CountsDistinctBlockingIssuesWithoutCountingSuggestions()
        {
            var blocked = Failure(LayoutReadinessFailureCode.AnchorBlocked);
            var report = Report(false, new[]
            {
                blocked, Failure(LayoutReadinessFailureCode.AnchorBlocked),
                Failure(LayoutReadinessFailureCode.MissingCoffeeMachine, type: LayoutStationType.CoffeeMachine,
                    instance: null, role: null, position: null),
                Failure(LayoutReadinessFailureCode.AnchorUnreachable, LayoutReadinessSeverity.Warning)
            });

            Assert.That(appearance.ReadinessSummary(report), Is.EqualTo("Can't open yet · 2 issues"));
            Assert.That(report.Failures.Count, Is.EqualTo(4), "Formatting must not mutate the diagnostic report.");
        }

        [TestCase(1, "Can open · 1 suggestion")]
        [TestCase(2, "Can open · 2 suggestions")]
        public void Summary_WarningsNeverSayBusinessIsBlocked(int count, string expected)
        {
            var failures = new[]
            {
                Failure(LayoutReadinessFailureCode.AnchorBlocked, LayoutReadinessSeverity.Warning),
                Failure(LayoutReadinessFailureCode.AnchorUnreachable, LayoutReadinessSeverity.Warning)
            }.Take(count).ToArray();
            Assert.That(appearance.ReadinessSummary(Report(true, failures)), Is.EqualTo(expected));
        }

        [Test]
        public void Details_DeduplicatesExactRecordsAndGroupsSuggestionsAfterBlocking()
        {
            var warning = Failure(LayoutReadinessFailureCode.AnchorUnreachable, LayoutReadinessSeverity.Warning,
                LayoutStationType.CoffeeMachine, "coffee-b", InteractionRole.Employee);
            var report = Report(false, new[]
            {
                warning, Failure(LayoutReadinessFailureCode.AnchorBlocked),
                Failure(LayoutReadinessFailureCode.AnchorBlocked)
            });

            Assert.That(appearance.ReadinessSummary(report), Is.EqualTo("Can't open yet · 1 issue"));
            Assert.That(appearance.ReadinessDetails(report), Is.EqualTo(
                "Cash Register · Customer side\nClear space for the customer.\n\nSuggestions\n"
                + "Coffee Machine · Employee side\nClear a path to this item."));
        }

        [TestCase("instance")]
        [TestCase("role")]
        [TestCase("position")]
        [TestCase("support")]
        [TestCase("slot")]
        [TestCase("message")]
        public void Details_DoesNotMergeDistinctIssueIdentity(string difference)
        {
            var first = Failure(LayoutReadinessFailureCode.AnchorBlocked);
            var second = Failure(LayoutReadinessFailureCode.AnchorBlocked,
                instance: difference == "instance" ? "cash-b" : "cash-a",
                role: difference == "role" ? InteractionRole.Employee : InteractionRole.Customer,
                position: difference == "position" ? new GridPosition(5, 4) : new GridPosition(4, 4),
                support: difference == "support" ? "counter-b" : "counter-a",
                slot: difference == "slot" ? "slot-b" : "slot-a",
                message: difference == "message" ? "A second diagnostic cause." : "Original diagnostic cause.");
            var report = Report(false, new[] { first, second });

            Assert.That(appearance.ReadinessSummary(report), Is.EqualTo("Can't open yet · 2 issues"));
            Assert.That(appearance.ReadinessDetails(report).Split('\n')
                .Count(line => line.StartsWith("Clear space for the ", StringComparison.Ordinal)), Is.EqualTo(2));
        }

        [Test]
        public void Details_NumbersSameTypeInstancesStablyIncludingHealthyStations()
        {
            var failures = new[]
            {
                Failure(LayoutReadinessFailureCode.AnchorBlocked, instance: "cash-c"),
                Failure(LayoutReadinessFailureCode.AnchorUnreachable, instance: "cash-b")
            };
            var stations = new[] { Station("cash-c"), Station("cash-a"), Station("cash-b") };
            var report = Report(false, failures, stations);
            var reversed = Report(false, failures.Reverse().ToArray(), stations.Reverse().ToArray());

            var details = appearance.ReadinessDetails(report);
            Assert.That(details, Does.Contain("Cash Register #2 · Customer side\nClear a path to this item."));
            Assert.That(details, Does.Contain("Cash Register #3 · Customer side\nClear space for the customer."));
            Assert.That(details, Does.Not.Contain("cash-"));
            Assert.That(details, Does.Not.Contain("#1"));
            Assert.That(appearance.ReadinessDetails(reversed), Is.EqualTo(details));
        }

        [TestCase(LayoutReadinessFailureCode.MissingCashRegister, LayoutStationType.CashRegister, "Add a Cash Register.")]
        [TestCase(LayoutReadinessFailureCode.MissingCoffeeMachine, LayoutStationType.CoffeeMachine, "Add a Coffee Machine.")]
        [TestCase(LayoutReadinessFailureCode.MissingPickUpPoint, LayoutStationType.PickUpPoint, "Add a Pickup Point.")]
        [TestCase(LayoutReadinessFailureCode.MissingSupportFurniture, LayoutStationType.CashRegister, "Place this item on a counter.")]
        [TestCase(LayoutReadinessFailureCode.MissingSurfaceSlot, LayoutStationType.CoffeeMachine, "Move this item to an available counter spot.")]
        [TestCase(LayoutReadinessFailureCode.DuplicateSurfaceOccupancy, LayoutStationType.CoffeeMachine, "Move one item to another counter spot.")]
        [TestCase(LayoutReadinessFailureCode.AnchorOutOfBounds, LayoutStationType.CashRegister, "Move this item so people can stand inside the room.")]
        [TestCase(LayoutReadinessFailureCode.AnchorBlocked, LayoutStationType.CashRegister, "Clear space beside this item.")]
        [TestCase(LayoutReadinessFailureCode.AnchorUnreachable, LayoutStationType.CoffeeMachine, "Clear a path to this item.")]
        [TestCase(LayoutReadinessFailureCode.NoCompleteReachableServiceCombination, null, "Connect the register, coffee machine and pickup point with clear paths.")]
        [TestCase(LayoutReadinessFailureCode.MissingFunctionalDirection, LayoutStationType.CashRegister, "This item is currently unavailable. Use another item.")]
        [TestCase(LayoutReadinessFailureCode.InvalidFunctionalDefinition, null, "This item is currently unavailable. Use another item.")]
        public void Details_MapsEachFailureToAnActionWithoutTechnicalCoordinates(
            LayoutReadinessFailureCode code, LayoutStationType? type, string expectedAction)
        {
            var details = appearance.ReadinessDetails(Report(false, new[]
            {
                Failure(code, type: type, role: null)
            }));
            var lines = details.Split('\n');
            Assert.That(lines.Length, Is.EqualTo(2), "Each issue has one subject and one action.");
            Assert.That(lines[1], Is.EqualTo(expectedAction));
            Assert.That(details, Does.Not.Contain("Blocking:"));
            Assert.That(details, Does.Not.Contain("(4, 4)"));
            Assert.That(details, Does.Not.Contain("cash-a"));
        }

        [Test]
        public void Details_ReadyReportHasNoIssuesToExpand()
        {
            Assert.That(appearance.ReadinessDetails(Report(true, Array.Empty<LayoutReadinessFailure>())), Is.Empty);
        }

        [TestCase(InteractionRole.Customer, "Cash Register · Customer side\nClear space for the customer.")]
        [TestCase(InteractionRole.Employee, "Cash Register · Employee side\nClear space for the employee.")]
        public void Details_BlockedRoleNamesThePersonWhoNeedsSpace(InteractionRole role, string expected)
        {
            Assert.That(appearance.ReadinessDetails(Report(false, new[]
            {
                Failure(LayoutReadinessFailureCode.AnchorBlocked, role: role)
            })), Is.EqualTo(expected));
        }

        [Test]
        public void RichDetails_EmphasizesSubjectAndEscapesTextContent()
        {
            var translated = new TextAsset("{\"entries\":["
                + "{\"key\":\"readiness.CashRegister\",\"value\":\"Cash <sprite=0> & Co\"},"
                + "{\"key\":\"readiness.action.AnchorBlocked\",\"value\":\"Clear <b>space</b>.\"}]}");
            try
            {
                typeof(P8RAppearance).GetField("english", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(appearance, translated);
                var method = typeof(P8RAppearance).GetMethod("ReadinessDetails",
                    new[] { typeof(LayoutReadinessReport), typeof(bool) });
                Assert.That(method, Is.Not.Null, "The View needs an opt-in rich-text formatter.");
                var report = Report(false, new[] { Failure(LayoutReadinessFailureCode.AnchorBlocked, role: null) });
                var details = (string)method.Invoke(appearance, new object[] { report, true });
                Assert.That(details, Is.EqualTo("<b>Cash &lt;sprite=0&gt; &amp; Co</b>\nClear &lt;b&gt;space&lt;/b&gt;."));
                Assert.That((string)method.Invoke(appearance, new object[] { report, false }),
                    Is.EqualTo("Cash <sprite=0> & Co\nClear <b>space</b>."));
            }
            finally { Object.DestroyImmediate(translated); }
        }

        [Test]
        public void Diagnostics_PreserveOriginalRecordsCoordinatesAndRawCauses()
        {
            var method = typeof(P8RAppearance).GetMethod("ReadinessDiagnosticMessage",
                new[] { typeof(LayoutReadinessReport) });
            Assert.That(method, Is.Not.Null, "Short UI copy must have a separate complete diagnostic message.");
            var report = Report(false, new[]
            {
                Failure(LayoutReadinessFailureCode.AnchorBlocked),
                Failure(LayoutReadinessFailureCode.AnchorBlocked)
            });

            var diagnostic = (string)method.Invoke(appearance, new object[] { report });
            Assert.That(diagnostic, Does.StartWith("Confirmed Layout: Needs attention"));
            Assert.That(diagnostic.Split('\n').Count(line => line.Contains("Cash Register / Customer (4, 4)")), Is.EqualTo(2));
            Assert.That(diagnostic, Does.Contain("The interaction space is blocked."));
            Assert.That(diagnostic, Does.Contain("Original diagnostic cause."));
        }

        // Real report objects keep the formatter boundary isolated from placement/pathfinding.
        // 使用真实 report 数据；这里验证呈现契约，不重复测试营业判定规则。
        private static LayoutReadinessFailure Failure(LayoutReadinessFailureCode code,
            LayoutReadinessSeverity severity = LayoutReadinessSeverity.Blocking,
            LayoutStationType? type = LayoutStationType.CashRegister,
            string instance = "cash-a", InteractionRole? role = InteractionRole.Customer,
            GridPosition? position = null, string support = "counter-a", string slot = "slot-a",
            string message = "Original diagnostic cause.") => Construct<LayoutReadinessFailure>(
                severity, code, type, instance, support, slot, role, position ?? new GridPosition(4, 4), message);

        private static StationReadiness Station(string instance) => Construct<StationReadiness>(
            LayoutStationType.CashRegister, instance, "counter-a", "slot-a", ResolvedStationAnchors.Empty,
            Array.Empty<int>(), Array.Empty<LayoutReadinessFailure>());

        private static LayoutReadinessReport Report(bool canOpen, LayoutReadinessFailure[] failures,
            StationReadiness[] stations = null)
        {
            var summary = Construct<LayoutReadinessSummary>(0, 0);
            return Construct<LayoutReadinessReport>(canOpen, stations ?? Array.Empty<StationReadiness>(),
                failures, summary, summary, summary);
        }

        private static T Construct<T>(params object[] arguments) => (T)Activator.CreateInstance(typeof(T),
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, arguments, null);
    }
}

using System;
using System.Collections.Generic;

namespace AnimalCafe.Layout
{
    public sealed class LayoutReadinessReport
    {
        public bool CanOpenForBusiness { get; }
        public IReadOnlyList<StationReadiness> Stations { get; }
        public IReadOnlyList<LayoutReadinessFailure> Failures { get; }
        public LayoutReadinessSummary CashRegisters { get; }
        public LayoutReadinessSummary CoffeeMachines { get; }
        public LayoutReadinessSummary PickUpPoints { get; }

        internal LayoutReadinessReport(
            bool canOpenForBusiness,
            IEnumerable<StationReadiness> stations,
            IEnumerable<LayoutReadinessFailure> failures,
            LayoutReadinessSummary cashRegisters,
            LayoutReadinessSummary coffeeMachines,
            LayoutReadinessSummary pickUpPoints)
        {
            CanOpenForBusiness = canOpenForBusiness;
            Stations = new List<StationReadiness>(
                stations ?? throw new ArgumentNullException(nameof(stations))).AsReadOnly();
            Failures = new List<LayoutReadinessFailure>(
                failures ?? throw new ArgumentNullException(nameof(failures))).AsReadOnly();
            CashRegisters = cashRegisters ??
                throw new ArgumentNullException(nameof(cashRegisters));
            CoffeeMachines = coffeeMachines ??
                throw new ArgumentNullException(nameof(coffeeMachines));
            PickUpPoints = pickUpPoints ??
                throw new ArgumentNullException(nameof(pickUpPoints));
        }
    }
}

using System;
using System.Collections.Generic;

namespace AnimalCafe.Capacity
{
    public sealed class CapacityResult
    {
        public bool Succeeded { get; }
        public CapacityFailureReason FailureReason { get; }
        public IReadOnlyList<ReservationSnapshot> Reservations { get; }

        internal CapacityResult(bool succeeded, CapacityFailureReason failureReason,
            IReadOnlyList<ReservationSnapshot> reservations)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            // 固定结果集合 / freeze result collection，不泄露调用方可变 List。
            var copy = new ReservationSnapshot[reservations.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = reservations[i];
            }
            Reservations = Array.AsReadOnly(copy);
        }
    }
}

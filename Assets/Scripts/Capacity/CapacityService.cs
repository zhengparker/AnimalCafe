using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimalCafe.Capacity
{
    public sealed class CapacityService
    {
        private sealed class Record
        {
            internal CapacityToken Token;
            internal string OwnerId;
            internal CapacityKind Kind;
            internal ReservationState State;

            internal ReservationSnapshot Snapshot()
            {
                return new ReservationSnapshot(Token, OwnerId, Kind, State);
            }
        }

        private static readonly CapacityKind[] AdmissionKinds =
        {
            CapacityKind.TotalCustomers, CapacityKind.CounterQueue, CapacityKind.PickUp
        };
        private readonly List<Record> _records = new List<Record>();
        private readonly CapacityRules _rules;
        private long _nextTokenId;

        public CapacityLimits Limits { get; private set; }
        public bool CanAdmit
        {
            get
            {
                var capacities = GetCapacities();
                return capacities.All(x => x.OverCapacity == 0 && x.Available > 0);
            }
        }

        public CapacityService(int floorCellCount, CapacityRules rules = null,
            long firstTokenId = 1)
        {
            if (firstTokenId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(firstTokenId));
            }
            _rules = rules ?? new CapacityRules();
            Limits = _rules.CalculateLimits(floorCellCount);
            _nextTokenId = firstTokenId;
        }

        public CapacityResult UpdateFloorCellCount(int floorCellCount)
        {
            if (floorCellCount < 0)
            {
                return Failure(CapacityFailureReason.InvalidFloorCellCount);
            }
            // 一次替换全部额度；已签发的 token 和状态不受面积变化影响。
            Limits = _rules.CalculateLimits(floorCellCount);
            return new CapacityResult(true, CapacityFailureReason.None,
                Array.Empty<ReservationSnapshot>());
        }

        public CapacityResult TryReserveAdmission(string ownerId)
        {
            return TryReserve(ownerId, AdmissionKinds);
        }

        public CapacityResult TryReserve(string ownerId, IReadOnlyList<CapacityKind> kinds)
        {
            if (string.IsNullOrWhiteSpace(ownerId) ||
                char.IsWhiteSpace(ownerId[0]) ||
                char.IsWhiteSpace(ownerId[ownerId.Length - 1]))
            {
                return Failure(CapacityFailureReason.InvalidOwnerId);
            }

            // 先复制并检查整个 batch / validate entire batch before any write。
            if (kinds == null || kinds.Count < 1 || kinds.Count > 3)
            {
                return Failure(CapacityFailureReason.InvalidKinds);
            }
            var requested = new CapacityKind[kinds.Count];
            for (var i = 0; i < kinds.Count; i++)
            {
                var kind = kinds[i];
                if (kind < CapacityKind.TotalCustomers || kind > CapacityKind.PickUp)
                {
                    return Failure(CapacityFailureReason.InvalidKinds);
                }
                requested[i] = kind;
            }
            Array.Sort(requested);
            for (var i = 1; i < requested.Length; i++)
            {
                if (requested[i] == requested[i - 1])
                {
                    return Failure(CapacityFailureReason.InvalidKinds);
                }
            }

            foreach (var kind in requested)
            {
                if (_records.Any(x => x.OwnerId == ownerId && x.Kind == kind &&
                    x.State != ReservationState.Released))
                {
                    return Failure(CapacityFailureReason.OwnerAlreadyReserved);
                }
            }

            var capacities = GetCapacities();
            if (capacities.Any(x => x.OverCapacity > 0))
            {
                return Failure(CapacityFailureReason.CapacityOverLimit);
            }
            foreach (var kind in requested)
            {
                if (capacities[(int)kind].Available < 1)
                {
                    return Failure(CapacityFailureReason.InsufficientCapacity);
                }
            }
            if (_nextTokenId == 0 || long.MaxValue - _nextTokenId + 1 < requested.Length)
            {
                return Failure(CapacityFailureReason.TokenIdExhausted);
            }

            var issued = new ReservationSnapshot[requested.Length];
            var pending = new Record[requested.Length];
            var nextTokenId = _nextTokenId;
            for (var i = 0; i < requested.Length; i++)
            {
                var id = nextTokenId;
                // 0 仅是耗尽标记 / exhausted sentinel；永不签发或回绕。
                nextTokenId = id == long.MaxValue ? 0 : id + 1;
                var record = new Record
                {
                    Token = new CapacityToken(id, this),
                    OwnerId = ownerId,
                    Kind = requested[i],
                    State = ReservationState.Reserved
                };
                pending[i] = record;
                issued[i] = record.Snapshot();
            }
            // 全部记录就绪后一次发布 / publish only after all records are ready。
            _records.AddRange(pending);
            _nextTokenId = nextTokenId;
            return new CapacityResult(true, CapacityFailureReason.None, issued);
        }

        public IReadOnlyList<CapacitySnapshot> GetCapacities()
        {
            var reserved = new int[3];
            var occupied = new int[3];
            foreach (var record in _records)
            {
                if (record.State == ReservationState.Reserved)
                {
                    reserved[(int)record.Kind]++;
                }
                else if (record.State == ReservationState.Occupied)
                {
                    occupied[(int)record.Kind]++;
                }
            }
            return Array.AsReadOnly(new[]
            {
                new CapacitySnapshot(CapacityKind.TotalCustomers,
                    Limits.TotalCustomers, reserved[0], occupied[0]),
                new CapacitySnapshot(CapacityKind.CounterQueue,
                    Limits.CounterQueue, reserved[1], occupied[1]),
                new CapacitySnapshot(CapacityKind.PickUp,
                    Limits.PickUp, reserved[2], occupied[2])
            });
        }

        public CapacityResult Occupy(CapacityToken token, string ownerId)
        {
            var record = FindAuthorizedRecord(token, ownerId, out var reason);
            if (record == null)
            {
                return Failure(reason);
            }
            if (record.State != ReservationState.Reserved)
            {
                return Failure(CapacityFailureReason.InvalidTransition);
            }
            record.State = ReservationState.Occupied;
            return new CapacityResult(true, CapacityFailureReason.None,
                new[] { record.Snapshot() });
        }

        public CapacityResult Release(CapacityToken token, string ownerId)
        {
            var record = FindAuthorizedRecord(token, ownerId, out var reason);
            if (record == null)
            {
                return Failure(reason);
            }
            if (record.State == ReservationState.Released)
            {
                return new CapacityResult(true, CapacityFailureReason.None,
                    new[] { record.Snapshot() });
            }
            if (record.State != ReservationState.Reserved &&
                record.State != ReservationState.Occupied)
            {
                return Failure(CapacityFailureReason.InvalidTransition);
            }
            record.State = ReservationState.Released;
            return new CapacityResult(true, CapacityFailureReason.None,
                new[] { record.Snapshot() });
        }

        private Record FindAuthorizedRecord(CapacityToken token, string ownerId,
            out CapacityFailureReason reason)
        {
            if (string.IsNullOrWhiteSpace(ownerId) ||
                char.IsWhiteSpace(ownerId[0]) ||
                char.IsWhiteSpace(ownerId[ownerId.Length - 1]))
            {
                reason = CapacityFailureReason.InvalidOwnerId;
                return null;
            }
            if (token == null || !ReferenceEquals(token.Issuer, this))
            {
                reason = CapacityFailureReason.InvalidToken;
                return null;
            }
            var record = _records.FirstOrDefault(x => ReferenceEquals(x.Token, token));
            if (record == null)
            {
                reason = CapacityFailureReason.InvalidToken;
                return null;
            }
            if (!string.Equals(record.OwnerId, ownerId, StringComparison.Ordinal))
            {
                reason = CapacityFailureReason.WrongOwner;
                return null;
            }
            reason = CapacityFailureReason.None;
            return record;
        }

        public IReadOnlyList<ReservationSnapshot> GetReservations()
        {
            return Array.AsReadOnly(_records.Select(x => x.Snapshot()).ToArray());
        }

        private static CapacityResult Failure(CapacityFailureReason reason)
        {
            return new CapacityResult(false, reason, Array.Empty<ReservationSnapshot>());
        }
    }
}

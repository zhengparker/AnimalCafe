using System;
using System.Collections.Generic;
using System.Linq;
using AnimalCafe.Content;

namespace AnimalCafe.Layout
{
    public sealed class LayoutReadinessEvaluator
    {
        private static readonly GridPosition[] NeighborOffsets =
        {
            new GridPosition(0, 1),
            new GridPosition(1, 0),
            new GridPosition(0, -1),
            new GridPosition(-1, 0)
        };

        private readonly InteractionAnchorResolver anchorResolver =
            new InteractionAnchorResolver();

        public LayoutReadinessReport Evaluate(
            CafeLayout cafeLayout,
            FunctionalSurfaceLayout functionalLayout,
            SurfaceSlotCatalog slots,
            FunctionalDirectionCatalog directions)
        {
            if (cafeLayout == null) throw new ArgumentNullException(nameof(cafeLayout));
            if (functionalLayout == null) throw new ArgumentNullException(nameof(functionalLayout));
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            if (directions == null) throw new ArgumentNullException(nameof(directions));

            Func<GridPosition, bool> isWalkable = position =>
                cafeLayout.IsInsideUnlockedRegion(position) &&
                !cafeLayout.HasReservation(position, LayoutReservationType.Blocked) &&
                !cafeLayout.TryGetOccupant(position, out _);

            var componentByCell = BuildWalkableComponentMap(
                cafeLayout,
                isWalkable);
            var entranceComponentIds = FindEntranceComponentIds(
                cafeLayout,
                componentByCell);
            var duplicateAddresses = FindDuplicateAddresses(functionalLayout);
            var failures = new List<LayoutReadinessFailure>();
            var candidates = BuildAllCandidates(
                cafeLayout,
                functionalLayout,
                slots,
                directions,
                isWalkable,
                componentByCell,
                entranceComponentIds,
                duplicateAddresses,
                failures);
            candidates.Sort(CompareCandidates);

            var cashSummary = CreateSummary(candidates, LayoutStationType.CashRegister);
            var coffeeSummary = CreateSummary(candidates, LayoutStationType.CoffeeMachine);
            var pickUpSummary = CreateSummary(candidates, LayoutStationType.PickUpPoint);
            var hasCompleteCombination = HasCompleteCombination(candidates);
            var canOpenForBusiness =
                cashSummary.ValidCount > 0 &&
                coffeeSummary.ValidCount > 0 &&
                pickUpSummary.ValidCount > 0 &&
                hasCompleteCombination;

            var stations = new List<StationReadiness>();
            var stationFailureSeverity = canOpenForBusiness
                ? LayoutReadinessSeverity.Warning
                : LayoutReadinessSeverity.Blocking;

            foreach (var candidate in candidates)
            {
                var stationFailures = candidate.CreateFailures(stationFailureSeverity);
                stationFailures.Sort(CompareFailures);
                var station = new StationReadiness(
                    candidate.FunctionType,
                    candidate.InstanceId,
                    candidate.Address.SupportFurnitureInstanceId,
                    candidate.Address.SlotId,
                    candidate.Anchors,
                    candidate.ReachableComponentIds,
                    stationFailures);
                stations.Add(station);
                failures.AddRange(station.Failures);
            }

            AddMissingCapabilityFailures(
                failures,
                cashSummary,
                coffeeSummary,
                pickUpSummary);
            if (cashSummary.ValidCount > 0 &&
                coffeeSummary.ValidCount > 0 &&
                pickUpSummary.ValidCount > 0 &&
                !hasCompleteCombination)
            {
                failures.Add(CreateGlobalFailure(
                    LayoutReadinessFailureCode.NoCompleteReachableServiceCombination,
                    null,
                    "No complete Cash Register, Coffee Machine, and Pick-up Point combination satisfies both customer and employee reachability networks."));
            }

            failures.Sort(CompareFailures);
            // 可用组合只降低普通站位问题；损坏 binding / definition 仍须阻止营业。
            canOpenForBusiness = canOpenForBusiness &&
                !failures.Any(failure => failure.Severity == LayoutReadinessSeverity.Blocking);
            return new LayoutReadinessReport(
                canOpenForBusiness,
                stations,
                failures,
                cashSummary,
                coffeeSummary,
                pickUpSummary);
        }

        private List<StationCandidate> BuildAllCandidates(
            CafeLayout cafeLayout,
            FunctionalSurfaceLayout functionalLayout,
            SurfaceSlotCatalog slots,
            FunctionalDirectionCatalog directions,
            Func<GridPosition, bool> isWalkable,
            IReadOnlyDictionary<GridPosition, int> componentByCell,
            ISet<int> entranceComponentIds,
            ISet<SurfaceSlotAddress> duplicateAddresses,
            ICollection<LayoutReadinessFailure> failures)
        {
            var candidates = new List<StationCandidate>();

            foreach (var instance in functionalLayout.MountedInstances)
            {
                if (!functionalLayout.TryGetDefinition(instance.DefinitionId, out var definition) ||
                    (definition.FunctionType != FurnitureFunctionType.CashRegister &&
                     definition.FunctionType != FurnitureFunctionType.CoffeeMachine) ||
                    (definition.AllowedPlacementSurfaces & PlacementSurfaceType.FurnitureSurface) == 0)
                {
                    failures.Add(new LayoutReadinessFailure(
                        LayoutReadinessSeverity.Blocking,
                        LayoutReadinessFailureCode.InvalidFunctionalDefinition,
                        null,
                        instance.InstanceId,
                        instance.Address.SupportFurnitureInstanceId,
                        instance.Address.SlotId,
                        null,
                        null,
                        $"Mounted instance '{instance.InstanceId}' refers to missing or incompatible functional definition '{instance.DefinitionId}'."));
                    continue;
                }

                // 类型来自正式 definition；缺少 direction 不能让已确认设备从 report 消失。
                var functionType = definition.FunctionType == FurnitureFunctionType.CashRegister
                    ? LayoutStationType.CashRegister
                    : LayoutStationType.CoffeeMachine;
                candidates.Add(BuildMountedCandidate(
                    functionType,
                    instance,
                    cafeLayout,
                    slots,
                    directions,
                    isWalkable,
                    componentByCell,
                    entranceComponentIds,
                    duplicateAddresses.Contains(instance.Address)));
            }

            foreach (var instance in functionalLayout.PickUpPoints)
            {
                candidates.Add(BuildPickUpCandidate(
                    instance,
                    cafeLayout,
                    slots,
                    isWalkable,
                    componentByCell,
                    entranceComponentIds,
                    duplicateAddresses.Contains(instance.Address)));
            }

            return candidates;
        }

        private StationCandidate BuildMountedCandidate(
            LayoutStationType functionType,
            SurfaceMountedInstance instance,
            CafeLayout cafeLayout,
            SurfaceSlotCatalog slots,
            FunctionalDirectionCatalog directions,
            Func<GridPosition, bool> isWalkable,
            IReadOnlyDictionary<GridPosition, int> componentByCell,
            ISet<int> entranceComponentIds,
            bool hasDuplicateAddress)
        {
            var candidate = new StationCandidate(
                functionType,
                instance.InstanceId,
                instance.Address);
            AddDuplicateFailure(candidate, hasDuplicateAddress);
            if (!ValidateBinding(candidate, cafeLayout, slots))
            {
                return candidate;
            }

            var hasDirection = functionType == LayoutStationType.CashRegister
                ? directions.TryGetCashRegisterSides(instance.DefinitionId, out _)
                : directions.TryGetCoffeeMachineDirection(instance.DefinitionId, out _);
            if (!hasDirection)
            {
                candidate.AddFailure(
                    LayoutReadinessFailureCode.MissingFunctionalDirection,
                    null,
                    null,
                    $"{DisplayName(functionType)} '{instance.InstanceId}' has no authored direction for definition '{instance.DefinitionId}'.");
                return candidate;
            }

            try
            {
                candidate.Anchors = anchorResolver.ResolveMounted(
                    instance,
                    cafeLayout,
                    slots,
                    directions);
            }
            catch (InvalidOperationException)
            {
                candidate.AddFailure(
                    LayoutReadinessFailureCode.AnchorOutOfBounds,
                    null,
                    null,
                    $"{DisplayName(functionType)} '{instance.InstanceId}' has an anchor outside the supported Grid range.");
                return candidate;
            }

            if (candidate.Anchors.Anchors.Count == 0)
            {
                candidate.AddFailure(
                    LayoutReadinessFailureCode.AnchorOutOfBounds,
                    null,
                    null,
                    $"{DisplayName(functionType)} '{instance.InstanceId}' could not resolve its required anchors.");
                return candidate;
            }

            ValidateAnchors(
                candidate,
                cafeLayout,
                componentByCell,
                entranceComponentIds);
            return candidate;
        }

        private StationCandidate BuildPickUpCandidate(
            PickUpPointInstance instance,
            CafeLayout cafeLayout,
            SurfaceSlotCatalog slots,
            Func<GridPosition, bool> isWalkable,
            IReadOnlyDictionary<GridPosition, int> componentByCell,
            ISet<int> entranceComponentIds,
            bool hasDuplicateAddress)
        {
            var candidate = new StationCandidate(
                LayoutStationType.PickUpPoint,
                instance.InstanceId,
                instance.Address);
            AddDuplicateFailure(candidate, hasDuplicateAddress);
            if (!ValidateBinding(candidate, cafeLayout, slots))
            {
                return candidate;
            }

            try
            {
                candidate.Anchors = anchorResolver.ResolvePickUp(
                    instance,
                    cafeLayout,
                    slots,
                    isWalkable);
            }
            catch (InvalidOperationException)
            {
                candidate.AddFailure(
                    LayoutReadinessFailureCode.AnchorOutOfBounds,
                    null,
                    null,
                    $"Pick-up Point '{instance.InstanceId}' has an anchor outside the supported Grid range.");
                return candidate;
            }

            if (candidate.Anchors.Anchors.Count == 0)
            {
                candidate.AddFailure(
                    LayoutReadinessFailureCode.AnchorBlocked,
                    null,
                    null,
                    $"Pick-up Point '{instance.InstanceId}' has no walkable adjacent anchor cell.");
                return candidate;
            }

            ValidateAnchors(
                candidate,
                cafeLayout,
                componentByCell,
                entranceComponentIds);
            return candidate;
        }

        private static void AddDuplicateFailure(
            StationCandidate candidate,
            bool hasDuplicateAddress)
        {
            if (!hasDuplicateAddress)
            {
                return;
            }

            candidate.AddFailure(
                LayoutReadinessFailureCode.DuplicateSurfaceOccupancy,
                null,
                null,
                $"Surface Slot '{candidate.Address.SlotId}' on support '{candidate.Address.SupportFurnitureInstanceId}' is bound to more than one functional point.");
        }

        private static bool ValidateBinding(
            StationCandidate candidate,
            CafeLayout cafeLayout,
            SurfaceSlotCatalog slots)
        {
            if (!cafeLayout.TryGetFurnitureInstance(
                candidate.Address.SupportFurnitureInstanceId,
                out var support))
            {
                candidate.AddFailure(
                    LayoutReadinessFailureCode.MissingSupportFurniture,
                    null,
                    null,
                    $"{DisplayName(candidate.FunctionType)} '{candidate.InstanceId}' refers to missing support furniture '{candidate.Address.SupportFurnitureInstanceId}'.");
                return false;
            }

            if (!slots.TryGet(support.DefinitionId, candidate.Address.SlotId, out _))
            {
                candidate.AddFailure(
                    LayoutReadinessFailureCode.MissingSurfaceSlot,
                    null,
                    null,
                    $"{DisplayName(candidate.FunctionType)} '{candidate.InstanceId}' refers to missing Surface Slot '{candidate.Address.SlotId}' on support '{support.InstanceId}'.");
                return false;
            }

            return true;
        }

        private void ValidateAnchors(
            StationCandidate candidate,
            CafeLayout cafeLayout,
            IReadOnlyDictionary<GridPosition, int> componentByCell,
            ISet<int> entranceComponentIds)
        {
            candidate.EmployeeComponentIds.Clear();
            candidate.CustomerComponentIds.Clear();

            foreach (var anchor in candidate.Anchors.Anchors)
            {
                if (!cafeLayout.IsInsideUnlockedRegion(anchor.Position))
                {
                    candidate.AddFailure(
                        LayoutReadinessFailureCode.AnchorOutOfBounds,
                        anchor.Role,
                        anchor.Position,
                        $"{DisplayName(candidate.FunctionType)} '{candidate.InstanceId}' {anchor.Role} anchor is outside the unlocked layout at ({anchor.Position.X}, {anchor.Position.Y}).");
                    continue;
                }

                if (cafeLayout.HasReservation(
                        anchor.Position,
                        LayoutReservationType.Blocked) ||
                    cafeLayout.TryGetOccupant(anchor.Position, out _))
                {
                    candidate.AddFailure(
                        LayoutReadinessFailureCode.AnchorBlocked,
                        anchor.Role,
                        anchor.Position,
                        $"{DisplayName(candidate.FunctionType)} '{candidate.InstanceId}' {anchor.Role} anchor is blocked at ({anchor.Position.X}, {anchor.Position.Y}).");
                    continue;
                }

                if (!componentByCell.TryGetValue(anchor.Position, out var componentId))
                {
                    candidate.AddFailure(
                        LayoutReadinessFailureCode.AnchorUnreachable,
                        anchor.Role,
                        anchor.Position,
                        $"{DisplayName(candidate.FunctionType)} '{candidate.InstanceId}' {anchor.Role} anchor has no walkable component at ({anchor.Position.X}, {anchor.Position.Y}).");
                    continue;
                }

                if (anchor.Role == InteractionRole.Customer &&
                    !entranceComponentIds.Contains(componentId))
                {
                    candidate.AddFailure(
                        LayoutReadinessFailureCode.AnchorUnreachable,
                        anchor.Role,
                        anchor.Position,
                        $"{DisplayName(candidate.FunctionType)} '{candidate.InstanceId}' Customer anchor is not reachable from an Entrance Clearance cell at ({anchor.Position.X}, {anchor.Position.Y}).");
                    continue;
                }

                var roleComponents = anchor.Role == InteractionRole.Employee
                    ? candidate.EmployeeComponentIds
                    : candidate.CustomerComponentIds;
                if (!roleComponents.Contains(componentId))
                {
                    roleComponents.Add(componentId);
                }
            }

            candidate.ReachableComponentIds.Clear();
            candidate.ReachableComponentIds.AddRange(candidate.EmployeeComponentIds);
            foreach (var componentId in candidate.CustomerComponentIds)
            {
                if (!candidate.ReachableComponentIds.Contains(componentId))
                {
                    candidate.ReachableComponentIds.Add(componentId);
                }
            }
            candidate.EmployeeComponentIds.Sort();
            candidate.CustomerComponentIds.Sort();
            candidate.ReachableComponentIds.Sort();
        }

        private static IReadOnlyDictionary<GridPosition, int> BuildWalkableComponentMap(
            CafeLayout cafeLayout,
            Func<GridPosition, bool> isWalkable)
        {
            var walkableCells = new HashSet<GridPosition>();
            foreach (var region in cafeLayout.UnlockedRegions)
            {
                var right = (long)region.Origin.X + region.Size.Width;
                var top = (long)region.Origin.Y + region.Size.Height;
                for (var x = (long)region.Origin.X; x < right; x++)
                {
                    for (var y = (long)region.Origin.Y; y < top; y++)
                    {
                        if (x < int.MinValue || x > int.MaxValue ||
                            y < int.MinValue || y > int.MaxValue)
                        {
                            continue;
                        }

                        var cell = new GridPosition((int)x, (int)y);
                        if (isWalkable(cell))
                        {
                            walkableCells.Add(cell);
                        }
                    }
                }
            }

            var orderedCells = walkableCells
                .OrderBy(cell => cell.X)
                .ThenBy(cell => cell.Y)
                .ToList();
            var componentByCell = new Dictionary<GridPosition, int>();
            var nextComponentId = 0;
            foreach (var cell in orderedCells)
            {
                if (componentByCell.ContainsKey(cell))
                {
                    continue;
                }

                FloodFillComponent(
                    cell,
                    nextComponentId,
                    isWalkable,
                    componentByCell);
                nextComponentId++;
            }

            return componentByCell;
        }

        private static ISet<int> FindEntranceComponentIds(
            CafeLayout cafeLayout,
            IReadOnlyDictionary<GridPosition, int> componentByCell)
        {
            var result = new HashSet<int>();
            foreach (var reservation in cafeLayout.Reservations)
            {
                if (reservation.Type != LayoutReservationType.EntranceClearance)
                {
                    continue;
                }

                var right = (long)reservation.Origin.X + reservation.Size.Width;
                var top = (long)reservation.Origin.Y + reservation.Size.Height;
                for (var x = (long)reservation.Origin.X; x < right; x++)
                {
                    for (var y = (long)reservation.Origin.Y; y < top; y++)
                    {
                        if (x < int.MinValue || x > int.MaxValue ||
                            y < int.MinValue || y > int.MaxValue)
                        {
                            continue;
                        }

                        if (componentByCell.TryGetValue(
                            new GridPosition((int)x, (int)y),
                            out var componentId))
                        {
                            result.Add(componentId);
                        }
                    }
                }
            }

            return result;
        }

        private static void FloodFillComponent(
            GridPosition start,
            int componentId,
            Func<GridPosition, bool> isWalkable,
            IDictionary<GridPosition, int> componentByCell)
        {
            var pending = new Queue<GridPosition>();
            componentByCell.Add(start, componentId);
            pending.Enqueue(start);

            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                foreach (var offset in NeighborOffsets)
                {
                    if (!TryAdd(current, offset, out var neighbor) ||
                        componentByCell.ContainsKey(neighbor) ||
                        !isWalkable(neighbor))
                    {
                        continue;
                    }

                    componentByCell.Add(neighbor, componentId);
                    pending.Enqueue(neighbor);
                }
            }
        }

        private static bool TryAdd(
            GridPosition position,
            GridPosition offset,
            out GridPosition result)
        {
            var x = (long)position.X + offset.X;
            var y = (long)position.Y + offset.Y;
            if (x < int.MinValue || x > int.MaxValue ||
                y < int.MinValue || y > int.MaxValue)
            {
                result = default;
                return false;
            }

            result = new GridPosition((int)x, (int)y);
            return true;
        }

        private static ISet<SurfaceSlotAddress> FindDuplicateAddresses(
            FunctionalSurfaceLayout functionalLayout)
        {
            var counts = new Dictionary<SurfaceSlotAddress, int>();
            foreach (var instance in functionalLayout.MountedInstances)
            {
                Increment(counts, instance.Address);
            }

            foreach (var instance in functionalLayout.PickUpPoints)
            {
                Increment(counts, instance.Address);
            }

            return new HashSet<SurfaceSlotAddress>(
                counts.Where(pair => pair.Value > 1).Select(pair => pair.Key));
        }

        private static void Increment(
            IDictionary<SurfaceSlotAddress, int> counts,
            SurfaceSlotAddress address)
        {
            counts[address] = counts.TryGetValue(address, out var current)
                ? current + 1
                : 1;
        }

        private static LayoutReadinessSummary CreateSummary(
            IEnumerable<StationCandidate> candidates,
            LayoutStationType functionType)
        {
            var matching = candidates
                .Where(candidate => candidate.FunctionType == functionType)
                .ToList();
            return new LayoutReadinessSummary(
                matching.Count,
                matching.Count(candidate => candidate.IsValid));
        }

        private static bool HasCompleteCombination(
            IEnumerable<StationCandidate> candidates)
        {
            var valid = candidates.Where(candidate => candidate.IsValid).ToList();
            var cashRegisters = valid.Where(candidate =>
                candidate.FunctionType == LayoutStationType.CashRegister);
            var pickUpPoints = valid.Where(candidate =>
                candidate.FunctionType == LayoutStationType.PickUpPoint).ToList();
            var coffeeEmployeeComponents = ComponentsForRole(
                valid,
                LayoutStationType.CoffeeMachine,
                InteractionRole.Employee);

            foreach (var cashRegister in cashRegisters)
            {
                foreach (var pickUpPoint in pickUpPoints)
                {
                    if (!SharesComponent(
                        cashRegister.CustomerComponentIds,
                        pickUpPoint.CustomerComponentIds))
                    {
                        continue;
                    }

                    var sharedEmployeeComponents = new HashSet<int>(
                        cashRegister.EmployeeComponentIds);
                    sharedEmployeeComponents.IntersectWith(
                        pickUpPoint.EmployeeComponentIds);
                    sharedEmployeeComponents.IntersectWith(
                        coffeeEmployeeComponents);
                    if (sharedEmployeeComponents.Count > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static HashSet<int> ComponentsForRole(
            IEnumerable<StationCandidate> candidates,
            LayoutStationType functionType,
            InteractionRole role)
        {
            return new HashSet<int>(candidates
                .Where(candidate => candidate.FunctionType == functionType)
                .SelectMany(candidate => role == InteractionRole.Employee
                    ? candidate.EmployeeComponentIds
                    : candidate.CustomerComponentIds));
        }

        private static bool SharesComponent(
            IEnumerable<int> first,
            IEnumerable<int> second)
        {
            var shared = new HashSet<int>(first);
            shared.IntersectWith(second);
            return shared.Count > 0;
        }

        private static void AddMissingCapabilityFailures(
            ICollection<LayoutReadinessFailure> failures,
            LayoutReadinessSummary cashSummary,
            LayoutReadinessSummary coffeeSummary,
            LayoutReadinessSummary pickUpSummary)
        {
            if (cashSummary.TotalCount == 0)
            {
                failures.Add(CreateGlobalFailure(
                    LayoutReadinessFailureCode.MissingCashRegister,
                    LayoutStationType.CashRegister,
                    "At least one Cash Register is required to open for business."));
            }

            if (coffeeSummary.TotalCount == 0)
            {
                failures.Add(CreateGlobalFailure(
                    LayoutReadinessFailureCode.MissingCoffeeMachine,
                    LayoutStationType.CoffeeMachine,
                    "At least one Coffee Machine is required to open for business."));
            }

            if (pickUpSummary.TotalCount == 0)
            {
                failures.Add(CreateGlobalFailure(
                    LayoutReadinessFailureCode.MissingPickUpPoint,
                    LayoutStationType.PickUpPoint,
                    "At least one Pick-up Point is required to open for business."));
            }
        }

        private static LayoutReadinessFailure CreateGlobalFailure(
            LayoutReadinessFailureCode code,
            LayoutStationType? functionType,
            string message)
        {
            return new LayoutReadinessFailure(
                LayoutReadinessSeverity.Blocking,
                code,
                functionType,
                null,
                null,
                null,
                null,
                null,
                message);
        }

        private static int CompareCandidates(
            StationCandidate left,
            StationCandidate right)
        {
            var functionComparison = left.FunctionType.CompareTo(right.FunctionType);
            return functionComparison != 0
                ? functionComparison
                : string.Compare(left.InstanceId, right.InstanceId, StringComparison.Ordinal);
        }

        private static bool IsStructuralFailure(LayoutReadinessFailureCode code)
        {
            return code == LayoutReadinessFailureCode.MissingSupportFurniture ||
                   code == LayoutReadinessFailureCode.MissingSurfaceSlot ||
                   code == LayoutReadinessFailureCode.DuplicateSurfaceOccupancy ||
                   code == LayoutReadinessFailureCode.MissingFunctionalDirection ||
                   code == LayoutReadinessFailureCode.InvalidFunctionalDefinition;
        }

        private static int CompareFailures(
            LayoutReadinessFailure left,
            LayoutReadinessFailure right)
        {
            var leftFunction = left.FunctionType.HasValue
                ? (int)left.FunctionType.Value
                : int.MaxValue;
            var rightFunction = right.FunctionType.HasValue
                ? (int)right.FunctionType.Value
                : int.MaxValue;
            var comparison = leftFunction.CompareTo(rightFunction);
            if (comparison != 0) return comparison;

            comparison = string.Compare(
                left.InstanceId,
                right.InstanceId,
                StringComparison.Ordinal);
            if (comparison != 0) return comparison;

            comparison = left.Code.CompareTo(right.Code);
            if (comparison != 0) return comparison;

            comparison = Nullable.Compare(left.Role, right.Role);
            if (comparison != 0) return comparison;

            comparison = ComparePositions(left.Position, right.Position);
            if (comparison != 0) return comparison;

            comparison = string.Compare(
                left.SupportFurnitureInstanceId,
                right.SupportFurnitureInstanceId,
                StringComparison.Ordinal);
            return comparison != 0
                ? comparison
                : string.Compare(
                    left.SurfaceSlotId,
                    right.SurfaceSlotId,
                    StringComparison.Ordinal);
        }

        private static int ComparePositions(
            GridPosition? left,
            GridPosition? right)
        {
            if (!left.HasValue) return right.HasValue ? -1 : 0;
            if (!right.HasValue) return 1;

            var xComparison = left.Value.X.CompareTo(right.Value.X);
            return xComparison != 0
                ? xComparison
                : left.Value.Y.CompareTo(right.Value.Y);
        }

        private static string DisplayName(LayoutStationType functionType)
        {
            switch (functionType)
            {
                case LayoutStationType.CashRegister:
                    return "Cash Register";
                case LayoutStationType.CoffeeMachine:
                    return "Coffee Machine";
                case LayoutStationType.PickUpPoint:
                    return "Pick-up Point";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(functionType),
                        functionType,
                        "Function type must be defined.");
            }
        }

        private sealed class StationCandidate
        {
            private readonly List<FailureDraft> failures =
                new List<FailureDraft>();

            public LayoutStationType FunctionType { get; }
            public string InstanceId { get; }
            public SurfaceSlotAddress Address { get; }
            public ResolvedStationAnchors Anchors { get; set; }
            public List<int> ReachableComponentIds { get; }
            public List<int> EmployeeComponentIds { get; }
            public List<int> CustomerComponentIds { get; }
            public bool IsValid => failures.Count == 0;

            public StationCandidate(
                LayoutStationType functionType,
                string instanceId,
                SurfaceSlotAddress address)
            {
                FunctionType = functionType;
                InstanceId = instanceId;
                Address = address;
                Anchors = ResolvedStationAnchors.Empty;
                ReachableComponentIds = new List<int>();
                EmployeeComponentIds = new List<int>();
                CustomerComponentIds = new List<int>();
            }

            public void AddFailure(
                LayoutReadinessFailureCode code,
                InteractionRole? role,
                GridPosition? position,
                string message)
            {
                failures.Add(new FailureDraft(code, role, position, message));
            }

            public List<LayoutReadinessFailure> CreateFailures(
                LayoutReadinessSeverity severity)
            {
                return failures.Select(failure => new LayoutReadinessFailure(
                    IsStructuralFailure(failure.Code) ? LayoutReadinessSeverity.Blocking : severity,
                    failure.Code,
                    FunctionType,
                    InstanceId,
                    Address.SupportFurnitureInstanceId,
                    Address.SlotId,
                    failure.Role,
                    failure.Position,
                    failure.Message)).ToList();
            }
        }

        private sealed class FailureDraft
        {
            public LayoutReadinessFailureCode Code { get; }
            public InteractionRole? Role { get; }
            public GridPosition? Position { get; }
            public string Message { get; }

            public FailureDraft(
                LayoutReadinessFailureCode code,
                InteractionRole? role,
                GridPosition? position,
                string message)
            {
                Code = code;
                Role = role;
                Position = position;
                Message = message;
            }
        }
    }
}

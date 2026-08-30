using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AnimalCafe.Layout
{
    public sealed class WallMountedLayout
    {
        private readonly Dictionary<string, WallSurfaceLayout> surfacesById;

        // Kept private: tests replace this only to exercise the unexpected
        // commit-failure rollback required by the atomic Move contract.
        private Func<WallSurfaceLayout, WallMountedInstance, WallPlacementResult>
            destinationCommit;

        public IReadOnlyDictionary<string, WallSurfaceLayout> Surfaces =>
            CreateDetachedSurfaceView();

        public WallMountedLayout(IEnumerable<WallSurfaceLayout> surfaces)
        {
            if (surfaces == null)
            {
                throw new ArgumentNullException(nameof(surfaces));
            }

            var sourceSurfaces = new List<WallSurfaceLayout>();
            var surfaceIds = new HashSet<string>(StringComparer.Ordinal);
            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sourceSurface in surfaces)
            {
                if (sourceSurface == null)
                {
                    throw new ArgumentException(
                        "Wall mounted layouts cannot contain null surfaces.",
                        nameof(surfaces));
                }

                if (!surfaceIds.Add(sourceSurface.SurfaceId))
                {
                    throw new ArgumentException(
                        "Each wall mounted surface ID must be unique.",
                        nameof(surfaces));
                }

                foreach (var item in sourceSurface.MountedItems)
                {
                    if (!instanceIds.Add(item.InstanceId))
                    {
                        throw new ArgumentException(
                            "Each mounted instance ID must be globally unique.",
                            nameof(surfaces));
                    }
                }

                sourceSurfaces.Add(sourceSurface);
            }

            var ownedSurfaces = new Dictionary<string, WallSurfaceLayout>(
                StringComparer.Ordinal);
            foreach (var sourceSurface in sourceSurfaces)
            {
                ownedSurfaces.Add(
                    sourceSurface.SurfaceId,
                    sourceSurface.CreateDetachedCopy());
            }

            surfacesById = ownedSurfaces;
            destinationCommit = (surface, item) => surface.TryPlace(item);
        }

        public WallPlacementResult ValidatePlacement(
            string definitionId,
            string surfaceId,
            WallSlotPosition position,
            WallFootprint footprint,
            string ignoredInstanceId = null)
        {
            WallMountedInstance.ValidateId(definitionId, nameof(definitionId));
            WallMountedInstance.ValidateId(surfaceId, nameof(surfaceId));

            if (ignoredInstanceId != null)
            {
                WallMountedInstance.ValidateId(
                    ignoredInstanceId,
                    nameof(ignoredInstanceId));
                if (!TryFindInstance(
                    ignoredInstanceId,
                    out _,
                    out _))
                {
                    return WallPlacementResult.Failure(
                        WallPlacementFailureReason.ItemNotFound);
                }
            }

            if (!surfacesById.TryGetValue(surfaceId, out var surface))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.SurfaceMismatch);
            }

            var candidate = new WallMountedInstance(
                ignoredInstanceId ?? "validation.candidate",
                definitionId,
                surfaceId,
                position,
                footprint);

            return surface.ValidatePlacement(candidate, ignoredInstanceId);
        }

        public WallPlacementResult Place(WallMountedInstance item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (!surfacesById.TryGetValue(item.SurfaceId, out var surface))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.SurfaceMismatch);
            }

            if (TryFindInstance(item.InstanceId, out _, out _))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.ItemAlreadyPlaced);
            }

            return surface.TryPlace(item);
        }

        public WallPlacementResult Move(
            string instanceId,
            string destinationSurfaceId,
            WallSlotPosition position)
        {
            WallMountedInstance.ValidateId(instanceId, nameof(instanceId));
            WallMountedInstance.ValidateId(
                destinationSurfaceId,
                nameof(destinationSurfaceId));

            if (!TryFindInstance(
                instanceId,
                out var sourceSurface,
                out var current))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.ItemNotFound);
            }

            if (!surfacesById.TryGetValue(
                destinationSurfaceId,
                out var destinationSurface))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.SurfaceMismatch);
            }

            if (ReferenceEquals(sourceSurface, destinationSurface))
            {
                return sourceSurface.TryMove(instanceId, position);
            }

            var candidate = current.WithPlacement(destinationSurfaceId, position);
            var validation = destinationSurface.ValidatePlacement(candidate);
            if (!validation.Succeeded)
            {
                return validation;
            }

            var sourceState = sourceSurface.CaptureOrderedState();
            var destinationState = destinationSurface.CaptureOrderedState();

            var sourceRemoval = sourceSurface.TryRemove(instanceId);
            if (!sourceRemoval.Succeeded)
            {
                return sourceRemoval;
            }

            WallPlacementResult destinationResult;
            try
            {
                destinationResult = destinationCommit(
                    destinationSurface,
                    candidate);
            }
            catch (Exception originalFailure)
            {
                try
                {
                    RestoreCrossSurfaceStates(
                        sourceSurface,
                        destinationSurface,
                        sourceState,
                        destinationState);
                }
                catch (Exception restorationFailure)
                {
                    var wrapped = new InvalidOperationException(
                        "The wall mounted move failed and exact restoration also failed.",
                        originalFailure);
                    wrapped.Data["RestorationFailure"] = restorationFailure;
                    throw wrapped;
                }

                throw;
            }

            if (!destinationResult.Succeeded)
            {
                try
                {
                    RestoreCrossSurfaceStates(
                        sourceSurface,
                        destinationSurface,
                        sourceState,
                        destinationState);
                }
                catch (Exception restorationFailure)
                {
                    throw new InvalidOperationException(
                        "The failed wall mounted move could not restore its exact state.",
                        restorationFailure);
                }
            }

            return destinationResult;
        }

        public WallPlacementResult Remove(string instanceId)
        {
            WallMountedInstance.ValidateId(instanceId, nameof(instanceId));

            if (!TryFindInstance(
                instanceId,
                out var surface,
                out _))
            {
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.ItemNotFound);
            }

            return surface.TryRemove(instanceId);
        }

        public bool TryGetInstance(
            string instanceId,
            out WallMountedInstance item)
        {
            WallMountedInstance.ValidateId(instanceId, nameof(instanceId));
            return TryFindInstance(instanceId, out _, out item);
        }

        public WallMountedLayoutSnapshot CaptureSnapshot()
        {
            return new WallMountedLayoutSnapshot
            {
                Surfaces = surfacesById
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new WallMountedSurfaceSnapshotEntry
                    {
                        SurfaceId = pair.Value.SurfaceId,
                        Columns = pair.Value.ColumnCount,
                        Rows = pair.Value.RowCount
                    })
                    .ToList(),
                Instances = surfacesById.Values
                    .SelectMany(surface => surface.MountedItems)
                    .OrderBy(item => item.InstanceId, StringComparer.Ordinal)
                    .Select(item => new WallMountedInstanceSnapshotEntry
                    {
                        InstanceId = item.InstanceId,
                        DefinitionId = item.DefinitionId,
                        SurfaceId = item.SurfaceId,
                        Column = item.Position.Column,
                        Row = item.Position.Row,
                        FootprintWidth = item.Footprint.Width,
                        FootprintHeight = item.Footprint.Height
                    })
                    .ToList()
            };
        }

        public static WallMountedLayout FromSnapshot(
            WallMountedLayoutSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (snapshot.Surfaces == null)
            {
                throw new ArgumentException(
                    "Snapshot surfaces are required.",
                    nameof(snapshot));
            }

            if (snapshot.Instances == null)
            {
                throw new ArgumentException(
                    "Snapshot instances are required.",
                    nameof(snapshot));
            }

            var surfaceCandidates = new List<WallSurfaceLayout>(
                snapshot.Surfaces.Count);
            var surfaceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in snapshot.Surfaces)
            {
                if (entry == null)
                {
                    throw new ArgumentException(
                        "Snapshot surfaces cannot contain null entries.",
                        nameof(snapshot));
                }

                var surface = new WallSurfaceLayout(
                    entry.SurfaceId,
                    entry.Columns,
                    entry.Rows);
                if (!surfaceIds.Add(surface.SurfaceId))
                {
                    throw new ArgumentException(
                        "Snapshot surface IDs must be unique.",
                        nameof(snapshot));
                }

                surfaceCandidates.Add(surface);
            }

            var itemCandidates = new List<WallMountedInstance>(
                snapshot.Instances.Count);
            var instanceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in snapshot.Instances)
            {
                if (entry == null)
                {
                    throw new ArgumentException(
                        "Snapshot instances cannot contain null entries.",
                        nameof(snapshot));
                }

                var item = new WallMountedInstance(
                    entry.InstanceId,
                    entry.DefinitionId,
                    entry.SurfaceId,
                    new WallSlotPosition(entry.Column, entry.Row),
                    new WallFootprint(
                        entry.FootprintWidth,
                        entry.FootprintHeight));

                if (!instanceIds.Add(item.InstanceId))
                {
                    throw new ArgumentException(
                        "Snapshot instance IDs must be unique.",
                        nameof(snapshot));
                }

                if (!surfaceIds.Contains(item.SurfaceId))
                {
                    throw new ArgumentException(
                        "Snapshot instance refers to an unknown wall surface.",
                        nameof(snapshot));
                }

                itemCandidates.Add(item);
            }

            var layout = new WallMountedLayout(surfaceCandidates);
            foreach (var item in itemCandidates.OrderBy(
                candidate => candidate.InstanceId,
                StringComparer.Ordinal))
            {
                var result = layout.Place(item);
                if (!result.Succeeded)
                {
                    throw new ArgumentException(
                        "Snapshot contains an invalid mounted attachment.",
                        nameof(snapshot));
                }
            }

            return layout;
        }

        private bool TryFindInstance(
            string instanceId,
            out WallSurfaceLayout surface,
            out WallMountedInstance item)
        {
            foreach (var candidateSurface in surfacesById
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => pair.Value))
            {
                foreach (var candidateItem in candidateSurface.MountedItems)
                {
                    if (string.Equals(
                        candidateItem.InstanceId,
                        instanceId,
                        StringComparison.Ordinal))
                    {
                        surface = candidateSurface;
                        item = candidateItem;
                        return true;
                    }
                }
            }

            surface = null;
            item = null;
            return false;
        }

        private IReadOnlyDictionary<string, WallSurfaceLayout>
            CreateDetachedSurfaceView()
        {
            var detached = new Dictionary<string, WallSurfaceLayout>(
                StringComparer.Ordinal);
            foreach (var surface in surfacesById.OrderBy(
                pair => pair.Key,
                StringComparer.Ordinal))
            {
                detached.Add(surface.Key, surface.Value.CreateDetachedCopy());
            }

            return new ReadOnlyDictionary<string, WallSurfaceLayout>(detached);
        }

        private static void RestoreCrossSurfaceStates(
            WallSurfaceLayout sourceSurface,
            WallSurfaceLayout destinationSurface,
            WallSurfaceLayout.OrderedState sourceState,
            WallSurfaceLayout.OrderedState destinationState)
        {
            Exception sourceFailure = null;
            try
            {
                sourceSurface.RestoreOrderedState(sourceState);
            }
            catch (Exception exception)
            {
                sourceFailure = exception;
            }

            Exception destinationFailure = null;
            try
            {
                destinationSurface.RestoreOrderedState(destinationState);
            }
            catch (Exception exception)
            {
                destinationFailure = exception;
            }

            if (sourceFailure != null && destinationFailure != null)
            {
                throw new AggregateException(sourceFailure, destinationFailure);
            }

            if (sourceFailure != null)
            {
                throw sourceFailure;
            }

            if (destinationFailure != null)
            {
                throw destinationFailure;
            }
        }
    }
}

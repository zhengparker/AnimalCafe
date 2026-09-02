using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AnimalCafe.Layout;
using NUnit.Framework;
using UnityEngine;

namespace AnimalCafe.Tests.Phase7
{
    public sealed class WallMountedLayoutTests
    {
        [TestCase(1, 1)]
        [TestCase(2, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 2)]
        [TestCase(3, 2)]
        public void ValidateAndPlace_FootprintMatrix_IsNonMutatingThenOccupiesExactArea(
            int width,
            int height)
        {
            var layout = CreateLayout();
            var before = SnapshotLayout(layout);
            var footprint = new WallFootprint(width, height);

            var validation = layout.ValidatePlacement(
                "wall.decor.fixture",
                "wall.back-left",
                new WallSlotPosition(0, 0),
                footprint);

            Assert.That(validation.Succeeded, Is.True);
            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
            Assert.That(
                layout.Surfaces["wall.back-left"].GetFootprintSlots(CreateItem(
                    "inspection.candidate",
                    "wall.decor.fixture",
                    "wall.back-left",
                    0,
                    0,
                    width,
                    height)),
                Has.Count.EqualTo(width * height));

            var placement = layout.Place(CreateItem(
                $"decor.{width}x{height}",
                "wall.decor.fixture",
                "wall.back-left",
                0,
                0,
                width,
                height));

            Assert.That(placement.Succeeded, Is.True);
            Assert.That(layout.Surfaces["wall.back-left"].OccupiedSlotCount,
                Is.EqualTo(width * height));
        }

        [TestCaseSource(nameof(BoundaryCases))]
        public void ValidatePlacement_BoundaryMatrix_RequiresWholeFootprintOnOneSurface(
            int width,
            int height,
            int column,
            int row,
            bool expectedSuccess)
        {
            var layout = CreateLayout();
            var before = SnapshotLayout(layout);

            var result = layout.ValidatePlacement(
                "wall.decor.fixture",
                "wall.back-left",
                new WallSlotPosition(column, row),
                new WallFootprint(width, height));

            Assert.That(result.Succeeded, Is.EqualTo(expectedSuccess));
            Assert.That(
                result.FailureReason,
                Is.EqualTo(expectedSuccess
                    ? WallPlacementFailureReason.None
                    : WallPlacementFailureReason.OutOfBounds));
            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
        }

        [TestCase(2, 1, 1)]
        [TestCase(2, 0, 2)]
        public void Place_PartialAndFullOverlap_AreRejectedWithoutMutation(
            int candidateColumn,
            int candidateRow,
            int candidateHeight)
        {
            var layout = CreateLayout();
            Assert.That(layout.Place(CreateItem(
                "window.01",
                "window.basic",
                "wall.back-left",
                2,
                0,
                1,
                2)).Succeeded, Is.True);
            var before = SnapshotLayout(layout);

            var result = layout.Place(CreateItem(
                "decor.01",
                "wall.decor.fixture",
                "wall.back-left",
                candidateColumn,
                candidateRow,
                1,
                candidateHeight));

            Assert.That(result.FailureReason,
                Is.EqualTo(WallPlacementFailureReason.Overlap));
            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
        }

        [Test]
        public void ValidateAndPlace_OverlapAndUnknownSurface_DoNotMutateEitherSurface()
        {
            var layout = CreateLayout();
            Assert.That(layout.Place(CreateItem(
                "window.01",
                "window.basic",
                "wall.back-left",
                2,
                0,
                1,
                2)).Succeeded, Is.True);
            var before = SnapshotLayout(layout);

            var overlapValidation = layout.ValidatePlacement(
                "wall.decor.fixture",
                "wall.back-left",
                new WallSlotPosition(2, 1),
                new WallFootprint(1, 1));
            var overlapPlace = layout.Place(CreateItem(
                "decor.01",
                "wall.decor.fixture",
                "wall.back-left",
                2,
                1,
                1,
                1));
            var missingValidation = layout.ValidatePlacement(
                "wall.decor.fixture",
                "wall.unknown",
                new WallSlotPosition(0, 0),
                new WallFootprint(1, 1));
            var missingPlace = layout.Place(CreateItem(
                "decor.02",
                "wall.decor.fixture",
                "wall.unknown",
                0,
                0,
                1,
                1));

            Assert.That(overlapValidation.FailureReason,
                Is.EqualTo(WallPlacementFailureReason.Overlap));
            Assert.That(overlapPlace.FailureReason,
                Is.EqualTo(WallPlacementFailureReason.Overlap));
            Assert.That(missingValidation.FailureReason,
                Is.EqualTo(WallPlacementFailureReason.SurfaceMismatch));
            Assert.That(missingPlace.FailureReason,
                Is.EqualTo(WallPlacementFailureReason.SurfaceMismatch));
            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
        }

        [Test]
        public void WindowAndWallDecor_ShareTheSameOccupancyInBothDirections()
        {
            AssertMutualOverlapRejected("window.basic", "wall.decor.fixture");
            AssertMutualOverlapRejected("wall.decor.fixture", "window.basic");
        }

        [Test]
        public void Constructor_ClonesInputSurfacesSoLaterInputMutationCannotBypassGlobalState()
        {
            var left = new WallSurfaceLayout("wall.back-left", 8, 2);
            var right = new WallSurfaceLayout("wall.back-right", 8, 2);
            Assert.That(left.TryPlace(CreateItem(
                "decor.01",
                "wall.decor.fixture",
                "wall.back-left",
                1,
                0,
                2,
                1)).Succeeded, Is.True);
            var layout = new WallMountedLayout(new[] { left, right });
            var before = SnapshotLayout(layout);

            Assert.That(left.TryRemove("decor.01").Succeeded, Is.True);
            Assert.That(left.TryPlace(CreateItem(
                "intruder.01",
                "wall.decor.fixture",
                "wall.back-left",
                6,
                1,
                1,
                1)).Succeeded, Is.True);

            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
            Assert.That(layout.TryGetInstance("decor.01", out _), Is.True);
            Assert.That(layout.TryGetInstance("intruder.01", out _), Is.False);
        }

        [Test]
        public void Surfaces_ReturnsDetachedValuesSoCallerMutationCannotBypassGlobalState()
        {
            var layout = CreateRollbackLayout();
            var before = SnapshotLayout(layout);
            var exposedLeft = layout.Surfaces["wall.back-left"];

            Assert.That(exposedLeft.TryRemove("source.moving").Succeeded, Is.True);
            Assert.That(exposedLeft.TryMove(
                "source.first",
                new WallSlotPosition(7, 1)).Succeeded, Is.True);
            Assert.That(exposedLeft.TryPlace(CreateItem(
                "intruder.01",
                "wall.decor.fixture",
                "wall.back-left",
                3,
                1,
                1,
                1)).Succeeded, Is.True);

            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
            Assert.That(layout.TryGetInstance("source.moving", out _), Is.True);
            Assert.That(layout.TryGetInstance("intruder.01", out _), Is.False);
        }

        [Test]
        public void Constructor_DuplicateInstanceAcrossSurfacesRejectsWithoutMutatingInputs()
        {
            var left = new WallSurfaceLayout("wall.back-left", 8, 2);
            var right = new WallSurfaceLayout("wall.back-right", 8, 2);
            Assert.That(left.TryPlace(CreateItem(
                "duplicate.01",
                "wall.decor.fixture",
                "wall.back-left",
                0,
                0,
                1,
                1)).Succeeded, Is.True);
            Assert.That(right.TryPlace(CreateItem(
                "duplicate.01",
                "window.basic",
                "wall.back-right",
                7,
                1,
                1,
                1)).Succeeded, Is.True);
            var leftBefore = SnapshotSurface("wall.back-left", left).ToArray();
            var rightBefore = SnapshotSurface("wall.back-right", right).ToArray();
            WallMountedLayout result = null;

            Assert.That(
                () => result = new WallMountedLayout(new[] { left, right }),
                Throws.ArgumentException);

            Assert.That(result, Is.Null);
            Assert.That(SnapshotSurface("wall.back-left", left), Is.EqualTo(leftBefore));
            Assert.That(SnapshotSurface("wall.back-right", right), Is.EqualTo(rightBefore));
        }

        [Test]
        public void Move_SameWall_PreservesIdentityAndTransfersOnlyItsSlots()
        {
            var layout = CreateLayout();
            Assert.That(layout.Place(CreateItem(
                "decor.01",
                "wall.decor.fixture",
                "wall.back-left",
                0,
                0,
                2,
                1)).Succeeded, Is.True);

            var result = layout.Move(
                "decor.01",
                "wall.back-left",
                new WallSlotPosition(4, 1));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(layout.TryGetInstance("decor.01", out var moved), Is.True);
            Assert.That(moved.InstanceId, Is.EqualTo("decor.01"));
            Assert.That(moved.DefinitionId, Is.EqualTo("wall.decor.fixture"));
            Assert.That(moved.SurfaceId, Is.EqualTo("wall.back-left"));
            Assert.That(moved.Position, Is.EqualTo(new WallSlotPosition(4, 1)));
            Assert.That(layout.Surfaces["wall.back-left"].OccupiedSlotCount, Is.EqualTo(2));
            Assert.That(layout.Surfaces["wall.back-left"].TryGetOccupant(
                new WallSlotPosition(0, 0), out _), Is.False);
        }

        [Test]
        public void ValidatePlacement_IgnoredExistingInstance_AllowsItsOwnSlotsWithoutMutation()
        {
            var layout = CreateLayout();
            Assert.That(layout.Place(CreateItem(
                "decor.01",
                "wall.decor.fixture",
                "wall.back-left",
                1,
                0,
                2,
                1)).Succeeded, Is.True);
            var before = SnapshotLayout(layout);

            var result = layout.ValidatePlacement(
                "wall.decor.fixture",
                "wall.back-left",
                new WallSlotPosition(1, 0),
                new WallFootprint(2, 1),
                "decor.01");

            Assert.That(result.Succeeded, Is.True);
            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Move_SameWallInvalidDestination_PreservesExactSourceAndOccupancy(
            bool useOverlap)
        {
            var layout = CreateLayout();
            Assert.That(layout.Place(CreateItem(
                "decor.01",
                "wall.decor.fixture",
                "wall.back-left",
                0,
                0,
                2,
                1)).Succeeded, Is.True);
            if (useOverlap)
            {
                Assert.That(layout.Place(CreateItem(
                    "window.01",
                    "window.basic",
                    "wall.back-left",
                    4,
                    0,
                    1,
                    2)).Succeeded, Is.True);
            }

            var before = SnapshotLayout(layout);
            var result = layout.Move(
                "decor.01",
                "wall.back-left",
                useOverlap
                    ? new WallSlotPosition(4, 1)
                    : new WallSlotPosition(7, 0));

            Assert.That(result.FailureReason, Is.EqualTo(
                useOverlap
                    ? WallPlacementFailureReason.Overlap
                    : WallPlacementFailureReason.OutOfBounds));
            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
        }

        [Test]
        public void Move_CrossWall_CommitsExactlyOneInstanceAfterDestinationValidation()
        {
            var layout = CreateLayout();
            Assert.That(layout.Place(CreateItem(
                "decor.01",
                "wall.decor.fixture",
                "wall.back-left",
                1,
                0,
                2,
                1)).Succeeded, Is.True);

            var result = layout.Move(
                "decor.01",
                "wall.back-right",
                new WallSlotPosition(5, 1));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(layout.TryGetInstance("decor.01", out var moved), Is.True);
            Assert.That(moved.SurfaceId, Is.EqualTo("wall.back-right"));
            Assert.That(moved.Position, Is.EqualTo(new WallSlotPosition(5, 1)));
            Assert.That(layout.Surfaces["wall.back-left"].MountedItems, Is.Empty);
            Assert.That(layout.Surfaces["wall.back-left"].OccupiedSlotCount, Is.Zero);
            Assert.That(layout.Surfaces["wall.back-right"].MountedItems, Has.Count.EqualTo(1));
            Assert.That(layout.Surfaces["wall.back-right"].OccupiedSlotCount, Is.EqualTo(2));
        }

        [TestCase("wall.back-right", 3, 0, WallPlacementFailureReason.Overlap)]
        [TestCase("wall.back-right", 7, 0, WallPlacementFailureReason.OutOfBounds)]
        [TestCase("wall.unknown", 0, 0, WallPlacementFailureReason.SurfaceMismatch)]
        public void Move_CrossWallInvalidDestination_RollsBackBothSurfaceSnapshots(
            string destinationSurfaceId,
            int column,
            int row,
            WallPlacementFailureReason expectedFailure)
        {
            var layout = CreateLayout();
            Assert.That(layout.Place(CreateItem(
                "decor.01",
                "wall.decor.fixture",
                "wall.back-left",
                1,
                0,
                2,
                1)).Succeeded, Is.True);
            Assert.That(layout.Place(CreateItem(
                "window.01",
                "window.basic",
                "wall.back-right",
                3,
                0,
                1,
                2)).Succeeded, Is.True);
            var before = SnapshotLayout(layout);

            var result = layout.Move(
                "decor.01",
                destinationSurfaceId,
                new WallSlotPosition(column, row));

            Assert.That(result.FailureReason, Is.EqualTo(expectedFailure));
            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
        }

        [Test]
        public void Move_DestinationCommitFalseWithoutMutation_RestoresExactOrderedStates()
        {
            var layout = CreateRollbackLayout();
            var before = SnapshotLayout(layout);

            SetDestinationCommitOverride(layout, (_, __) =>
                WallPlacementResult.Failure(
                    WallPlacementFailureReason.ItemAlreadyPlaced));

            var result = layout.Move(
                "source.moving",
                "wall.back-right",
                new WallSlotPosition(3, 0));

            Assert.That(result.FailureReason,
                Is.EqualTo(WallPlacementFailureReason.ItemAlreadyPlaced));
            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
        }

        [Test]
        public void Move_DestinationCommitFalseAfterMutation_RestoresExactOrderedStates()
        {
            var layout = CreateRollbackLayout();
            var before = SnapshotLayout(layout);
            SetDestinationCommitOverride(layout, (surface, candidate) =>
            {
                Assert.That(surface.TryPlace(candidate).Succeeded, Is.True);
                Assert.That(surface.TryMove(
                    "destination.last",
                    new WallSlotPosition(5, 1)).Succeeded, Is.True);
                return WallPlacementResult.Failure(
                    WallPlacementFailureReason.ItemAlreadyPlaced);
            });

            var result = layout.Move(
                "source.moving",
                "wall.back-right",
                new WallSlotPosition(3, 0));

            Assert.That(result.FailureReason,
                Is.EqualTo(WallPlacementFailureReason.ItemAlreadyPlaced));
            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Move_DestinationCommitExceptionAfterMutation_RestoresAndRethrowsOriginal(
            bool alsoMutateExistingDestination)
        {
            var layout = CreateRollbackLayout();
            var before = SnapshotLayout(layout);
            var injected = new InvalidOperationException(
                "Injected commit failure after destination mutation.");
            SetDestinationCommitOverride(layout, (surface, candidate) =>
            {
                Assert.That(surface.TryPlace(candidate).Succeeded, Is.True);
                if (alsoMutateExistingDestination)
                {
                    Assert.That(surface.TryMove(
                        "destination.first",
                        new WallSlotPosition(1, 1)).Succeeded, Is.True);
                }

                throw injected;
            });

            var thrown = Assert.Throws<InvalidOperationException>(() => layout.Move(
                "source.moving",
                "wall.back-right",
                new WallSlotPosition(3, 0)));

            Assert.That(thrown, Is.SameAs(injected));
            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
        }

        [Test]
        public void SingleSurfaceApi_CannotRepresentCrossCornerFootprint()
        {
            var layout = CreateLayout();
            var item = CreateItem(
                "decor.01",
                "wall.decor.fixture",
                "wall.back-left",
                7,
                0,
                2,
                1);

            Assert.That(item.SurfaceId, Is.EqualTo("wall.back-left"));
            Assert.That(layout.Place(item).FailureReason,
                Is.EqualTo(WallPlacementFailureReason.OutOfBounds));
            Assert.That(layout.Surfaces.Values.Sum(surface => surface.OccupiedSlotCount),
                Is.Zero);
        }

        [Test]
        public void RemoveStore_ReleasesSlotsIsIdempotentAndDefinitionCanBePlacedAgain()
        {
            var layout = CreateLayout();
            Assert.That(layout.Place(CreateItem(
                "decor.01",
                "wall.decor.fixture",
                "wall.back-left",
                2,
                0,
                2,
                2)).Succeeded, Is.True);

            Assert.That(layout.Remove("decor.01").Succeeded, Is.True);
            var afterFirstRemove = SnapshotLayout(layout);
            Assert.That(layout.Remove("decor.01").FailureReason,
                Is.EqualTo(WallPlacementFailureReason.ItemNotFound));
            Assert.That(SnapshotLayout(layout), Is.EqualTo(afterFirstRemove));
            Assert.That(layout.Place(CreateItem(
                "decor.02",
                "wall.decor.fixture",
                "wall.back-right",
                2,
                0,
                2,
                2)).Succeeded, Is.True);
        }

        [Test]
        public void CaptureSnapshot_JsonRoundTrip_IsDeterministicAndRebuildsOccupancy()
        {
            var layout = CreateLayout();
            Assert.That(layout.Place(CreateItem(
                "window.02",
                "window.basic",
                "wall.back-right",
                6,
                0,
                1,
                2)).Succeeded, Is.True);
            Assert.That(layout.Place(CreateItem(
                "decor.01",
                "wall.decor.fixture",
                "wall.back-left",
                1,
                1,
                3,
                1)).Succeeded, Is.True);

            var first = layout.CaptureSnapshot();
            var firstJson = JsonUtility.ToJson(first);
            var secondJson = JsonUtility.ToJson(layout.CaptureSnapshot());
            var deserialized = JsonUtility.FromJson<WallMountedLayoutSnapshot>(firstJson);
            var restored = WallMountedLayout.FromSnapshot(deserialized);

            Assert.That(secondJson, Is.EqualTo(firstJson));
            Assert.That(first.Surfaces.Select(entry => entry.SurfaceId), Is.EqualTo(
                new[] { "wall.back-left", "wall.back-right" }));
            Assert.That(first.Surfaces.Select(entry =>
                $"{entry.Columns}x{entry.Rows}"), Is.EqualTo(
                new[] { "8x2", "8x2" }));
            Assert.That(first.Instances.Select(entry => entry.InstanceId), Is.EqualTo(
                new[] { "decor.01", "window.02" }));
            Assert.That(first.Instances.Select(entry => string.Join(
                "|",
                entry.InstanceId,
                entry.DefinitionId,
                entry.SurfaceId,
                entry.Column,
                entry.Row,
                entry.FootprintWidth,
                entry.FootprintHeight)), Is.EqualTo(new[]
                {
                    "decor.01|wall.decor.fixture|wall.back-left|1|1|3|1",
                    "window.02|window.basic|wall.back-right|6|0|1|2"
                }));
            Assert.That(SnapshotLayout(restored), Is.EqualTo(SnapshotLayout(layout)));
            Assert.That(restored.Surfaces["wall.back-left"].OccupiedSlotCount, Is.EqualTo(3));
            Assert.That(restored.Surfaces["wall.back-right"].OccupiedSlotCount, Is.EqualTo(2));
        }

        [Test]
        public void FromSnapshot_InvalidMatricesRejectWithoutMutatingInputOrReturningPartialLayout()
        {
            AssertSnapshotRejected(CreateSnapshotWithDuplicateSurface());
            AssertSnapshotRejected(CreateSnapshotWithDuplicateInstance());
            AssertSnapshotRejected(CreateSnapshotWithUnknownSurfaceAttachment());
            AssertSnapshotRejected(CreateSnapshotWithInvalidSurfaceIdPattern());
            AssertSnapshotRejected(CreateSnapshotWithOutOfBoundsAttachment());
            AssertSnapshotRejected(CreateSnapshotWithOverlappingAttachment());
        }

        private static IEnumerable<TestCaseData> BoundaryCases
        {
            get
            {
                var footprints = new[]
                {
                    new[] { 1, 1 },
                    new[] { 2, 1 },
                    new[] { 1, 2 },
                    new[] { 2, 2 },
                    new[] { 3, 2 }
                };

                foreach (var footprint in footprints)
                {
                    var width = footprint[0];
                    var height = footprint[1];
                    var maxColumn = 8 - width;
                    var maxRow = 2 - height;
                    var edgeColumn = maxColumn / 2;
                    var edgeRow = maxRow / 2;

                    yield return BoundaryCase(width, height, 0, edgeRow, true, "left");
                    yield return BoundaryCase(
                        width, height, maxColumn, edgeRow, true, "right");
                    yield return BoundaryCase(
                        width, height, edgeColumn, 0, true, "bottom");
                    yield return BoundaryCase(
                        width, height, edgeColumn, maxRow, true, "top");
                    yield return BoundaryCase(width, height, 0, 0, true, "bottom-left");
                    yield return BoundaryCase(
                        width, height, maxColumn, 0, true, "bottom-right");
                    yield return BoundaryCase(
                        width, height, 0, maxRow, true, "top-left");
                    yield return BoundaryCase(
                        width, height, maxColumn, maxRow, true, "top-right");

                    yield return BoundaryCase(width, height, -1, edgeRow, false, "left-out");
                    yield return BoundaryCase(
                        width, height, maxColumn + 1, edgeRow, false, "right-out");
                    yield return BoundaryCase(
                        width, height, edgeColumn, -1, false, "bottom-out");
                    yield return BoundaryCase(
                        width, height, edgeColumn, maxRow + 1, false, "top-out");
                    yield return BoundaryCase(
                        width, height, -1, -1, false, "bottom-left-out");
                    yield return BoundaryCase(
                        width, height, maxColumn + 1, -1, false, "bottom-right-out");
                    yield return BoundaryCase(
                        width, height, -1, maxRow + 1, false, "top-left-out");
                    yield return BoundaryCase(
                        width,
                        height,
                        maxColumn + 1,
                        maxRow + 1,
                        false,
                        "top-right-out");
                }
            }
        }

        private static TestCaseData BoundaryCase(
            int width,
            int height,
            int column,
            int row,
            bool expectedSuccess,
            string boundaryName)
        {
            return new TestCaseData(width, height, column, row, expectedSuccess)
                .SetName(
                    $"ValidatePlacement_{width}x{height}_{boundaryName}_{column}_{row}_{expectedSuccess}");
        }

        private static WallMountedLayout CreateLayout()
        {
            return new WallMountedLayout(new[]
            {
                new WallSurfaceLayout("wall.back-right", 8, 2),
                new WallSurfaceLayout("wall.back-left", 8, 2)
            });
        }

        private static WallMountedLayout CreateRollbackLayout()
        {
            var left = new WallSurfaceLayout("wall.back-left", 8, 2);
            var right = new WallSurfaceLayout("wall.back-right", 8, 2);
            Assert.That(left.TryPlace(CreateItem(
                "source.first", "wall.decor.fixture", "wall.back-left",
                0, 0, 1, 1)).Succeeded, Is.True);
            Assert.That(left.TryPlace(CreateItem(
                "source.moving", "wall.decor.fixture", "wall.back-left",
                2, 0, 1, 2)).Succeeded, Is.True);
            Assert.That(left.TryPlace(CreateItem(
                "source.last", "wall.decor.fixture", "wall.back-left",
                6, 1, 1, 1)).Succeeded, Is.True);
            Assert.That(right.TryPlace(CreateItem(
                "destination.first", "window.basic", "wall.back-right",
                0, 0, 1, 1)).Succeeded, Is.True);
            Assert.That(right.TryPlace(CreateItem(
                "destination.last", "wall.decor.fixture", "wall.back-right",
                7, 1, 1, 1)).Succeeded, Is.True);

            return new WallMountedLayout(new[] { right, left });
        }

        private static WallMountedInstance CreateItem(
            string instanceId,
            string definitionId,
            string surfaceId,
            int column,
            int row,
            int width,
            int height)
        {
            return new WallMountedInstance(
                instanceId,
                definitionId,
                surfaceId,
                new WallSlotPosition(column, row),
                new WallFootprint(width, height));
        }

        private static string[] SnapshotLayout(WallMountedLayout layout)
        {
            return layout.Surfaces
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .SelectMany(pair => SnapshotSurface(pair.Key, pair.Value))
                .ToArray();
        }

        private static IEnumerable<string> SnapshotSurface(
            string surfaceId,
            WallSurfaceLayout surface)
        {
            yield return
                $"surface|{surfaceId}|{surface.ColumnCount}|{surface.RowCount}|{surface.OccupiedSlotCount}";

            foreach (var item in surface.MountedItems)
            {
                yield return string.Join(
                    "|",
                    "item",
                    item.InstanceId,
                    item.DefinitionId,
                    item.SurfaceId,
                    item.Position.Column,
                    item.Position.Row,
                    item.Footprint.Width,
                    item.Footprint.Height);
            }

            for (var column = 0; column < surface.ColumnCount; column++)
            {
                for (var row = 0; row < surface.RowCount; row++)
                {
                    var position = new WallSlotPosition(column, row);
                    if (surface.TryGetOccupant(position, out var owner))
                    {
                        yield return $"slot|{surfaceId}|{column}|{row}|{owner}";
                    }
                }
            }
        }

        private static void AssertMutualOverlapRejected(
            string firstDefinitionId,
            string secondDefinitionId)
        {
            var layout = CreateLayout();
            Assert.That(layout.Place(CreateItem(
                "first.01",
                firstDefinitionId,
                "wall.back-left",
                3,
                0,
                1,
                2)).Succeeded, Is.True);
            var before = SnapshotLayout(layout);

            var result = layout.Place(CreateItem(
                "second.01",
                secondDefinitionId,
                "wall.back-left",
                3,
                1,
                1,
                1));

            Assert.That(result.FailureReason,
                Is.EqualTo(WallPlacementFailureReason.Overlap));
            Assert.That(SnapshotLayout(layout), Is.EqualTo(before));
        }

        private static void SetDestinationCommitOverride(
            WallMountedLayout layout,
            Func<WallSurfaceLayout, WallMountedInstance, WallPlacementResult> value)
        {
            var field = typeof(WallMountedLayout).GetField(
                "destinationCommit",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "AT-020 requires a private fault-injection seam.");
            field.SetValue(layout, value);
        }

        private static WallMountedLayoutSnapshot CreateValidSnapshot()
        {
            return new WallMountedLayoutSnapshot
            {
                Surfaces = new List<WallMountedSurfaceSnapshotEntry>
                {
                    new WallMountedSurfaceSnapshotEntry
                    {
                        SurfaceId = "wall.back-left",
                        Columns = 8,
                        Rows = 2
                    },
                    new WallMountedSurfaceSnapshotEntry
                    {
                        SurfaceId = "wall.back-right",
                        Columns = 8,
                        Rows = 2
                    }
                },
                Instances = new List<WallMountedInstanceSnapshotEntry>
                {
                    new WallMountedInstanceSnapshotEntry
                    {
                        InstanceId = "decor.01",
                        DefinitionId = "wall.decor.fixture",
                        SurfaceId = "wall.back-left",
                        Column = 1,
                        Row = 0,
                        FootprintWidth = 2,
                        FootprintHeight = 1
                    }
                }
            };
        }

        private static WallMountedLayoutSnapshot CreateSnapshotWithDuplicateSurface()
        {
            var snapshot = CreateValidSnapshot();
            snapshot.Surfaces.Add(new WallMountedSurfaceSnapshotEntry
            {
                SurfaceId = "wall.back-left",
                Columns = 8,
                Rows = 2
            });
            return snapshot;
        }

        private static WallMountedLayoutSnapshot CreateSnapshotWithDuplicateInstance()
        {
            var snapshot = CreateValidSnapshot();
            snapshot.Instances.Add(new WallMountedInstanceSnapshotEntry
            {
                InstanceId = "decor.01",
                DefinitionId = "wall.decor.fixture",
                SurfaceId = "wall.back-right",
                Column = 0,
                Row = 0,
                FootprintWidth = 1,
                FootprintHeight = 1
            });
            return snapshot;
        }

        private static WallMountedLayoutSnapshot CreateSnapshotWithUnknownSurfaceAttachment()
        {
            var snapshot = CreateValidSnapshot();
            snapshot.Instances[0].SurfaceId = "wall.unknown";
            return snapshot;
        }

        private static WallMountedLayoutSnapshot CreateSnapshotWithInvalidSurfaceIdPattern()
        {
            var snapshot = CreateValidSnapshot();
            snapshot.Instances[0].SurfaceId = "Wall Back";
            return snapshot;
        }

        private static WallMountedLayoutSnapshot CreateSnapshotWithOutOfBoundsAttachment()
        {
            var snapshot = CreateValidSnapshot();
            snapshot.Instances[0].Column = 7;
            snapshot.Instances[0].FootprintWidth = 2;
            return snapshot;
        }

        private static WallMountedLayoutSnapshot CreateSnapshotWithOverlappingAttachment()
        {
            var snapshot = CreateValidSnapshot();
            snapshot.Instances.Add(new WallMountedInstanceSnapshotEntry
            {
                InstanceId = "window.01",
                DefinitionId = "window.basic",
                SurfaceId = "wall.back-left",
                Column = 2,
                Row = 0,
                FootprintWidth = 1,
                FootprintHeight = 2
            });
            return snapshot;
        }

        private static void AssertSnapshotRejected(WallMountedLayoutSnapshot snapshot)
        {
            var before = JsonUtility.ToJson(snapshot);
            WallMountedLayout result = null;

            Assert.That(
                () => result = WallMountedLayout.FromSnapshot(snapshot),
                Throws.InstanceOf<ArgumentException>());

            Assert.That(result, Is.Null);
            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(before));
        }
    }
}
